using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Immutable copied DATA, never a filesystem access capability.</summary>
internal sealed record MortalTurnAuditDocument(string Id, string Role, string Path, string Text);

/// <summary>
/// A complete attempt-local model input. Capture authentication and final fresh
/// ownership are responsibilities of the caller, not of a digest or model verdict.
/// </summary>
internal sealed class MortalTurnAuditPacket
{
    internal IReadOnlyList<MortalTurnAuditDocument> Documents { get; }
    internal string InputJson { get; }
    internal string Digest { get; }

    internal MortalTurnAuditPacket(string session, string generation, string request, int turn, int attempt,
        IEnumerable<MortalTurnAuditDocument> documents)
    {
        if (new[] { session, generation, request }.Any(string.IsNullOrWhiteSpace) || turn < 0 || attempt < 1)
            throw new ArgumentException("Audit requires an exact session, generation, request, turn and attempt.");
        ArgumentNullException.ThrowIfNull(documents);
        var copies = documents.OrderBy(d => d.Id, StringComparer.Ordinal).ToArray();
        if (copies.Length == 0 || copies.Select(d => d.Id).Distinct(StringComparer.Ordinal).Count() != copies.Length ||
            copies.Any(d => string.IsNullOrWhiteSpace(d.Id) || string.IsNullOrWhiteSpace(d.Path) || d.Text is null ||
                d.Role is not ("original" or "candidate" or "narrative" or "rules")) ||
            new[] { "original", "candidate", "narrative" }.Any(role => !copies.Any(d => d.Role == role)))
            throw new ArgumentException("Audit requires unique documents and original, candidate and narrative coverage.");
        // System.Text.Json replaces isolated UTF-16 surrogates. Reject them
        // before serialization so different original input cannot share a digest.
        var strictUtf8 = new UTF8Encoding(false, true);
        try
        {
            foreach (var text in new[] { session, generation, request }.Concat(
                copies.SelectMany(d => new[] { d.Id, d.Role, d.Path, d.Text })))
                _ = strictUtf8.GetByteCount(text);
        }
        catch (EncoderFallbackException error)
        {
            throw new ArgumentException("Audit input must be well-formed Unicode.", nameof(documents), error);
        }
        Documents = new ReadOnlyCollection<MortalTurnAuditDocument>(copies);
        // Ordered document identities make collection order irrelevant, but preserve
        // every text byte represented by the .NET string, role and source coordinate.
        InputJson = JsonSerializer.Serialize(new
        {
            schemaVersion = 1, session, generation, request, turn, attempt,
            documents = copies.Select(d => new { id = d.Id, role = d.Role, path = d.Path, text = d.Text })
        });
        Digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(InputJson))).ToLowerInvariant();
    }
}

/// <summary>Semantic consistency DATA only; never permission to publish or mutate.</summary>
internal sealed record MortalTurnAuditResult(bool IsConsistent, string Error);

internal static class MortalTurnAuditContract
{
    internal static MortalTurnAuditResult Validate(MortalTurnAuditPacket packet, string response)
    {
        try
        {
            using var parsed = JsonDocument.Parse(response);
            var root = parsed.RootElement;
            NoDuplicates(root);
            Shape(root, "schemaVersion", "packetDigest", "verdict", "reviewedDocuments", "findings");
            if (root.GetProperty("schemaVersion").GetInt32() != 1 ||
                Text(root, "packetDigest") != packet.Digest)
                return Hold("Audit response belongs to a different packet or protocol.");
            var verdict = Text(root, "verdict");
            if (verdict is not ("pass" or "fail" or "uncertain"))
                return Hold("Unknown audit verdict.");
            var reviewed = root.GetProperty("reviewedDocuments").EnumerateArray().Select(x => x.GetString()).ToArray();
            if (reviewed.Length != packet.Documents.Count ||
                reviewed.Distinct(StringComparer.Ordinal).Count() != reviewed.Length ||
                packet.Documents.Any(d => !reviewed.Contains(d.Id, StringComparer.Ordinal)))
                return Hold("Audit did not review the complete packet.");

            // Parse each supplied state document once per response. This is a local
            // DATA index, not a permission cache; it never reads a path from the reply.
            var state = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var document in packet.Documents.Where(d => d.Role is "original" or "candidate"))
            {
                using var json = JsonDocument.Parse(document.Text);
                NoDuplicates(json.RootElement);
                state.Add(document.Id, json.RootElement.Clone());
            }
            var adverse = false;
            foreach (var finding in root.GetProperty("findings").EnumerateArray())
            {
                Shape(finding, "direction", "quoteDocumentId", "quote", "assessment", "reason", "evidence");
                var quoteId = Text(finding, "quoteDocumentId");
                var direction = Text(finding, "direction");
                if (direction is not ("narrativeToState" or "stateToNarrative")) return Hold("Unknown finding direction.");
                var quote = finding.GetProperty("quote").GetString();
                var narrative = packet.Documents.SingleOrDefault(d => d.Id == quoteId && d.Role == "narrative");
                if (narrative is null || quote is null ||
                    (quote.Length != 0 && !narrative.Text.Contains(quote, StringComparison.Ordinal)))
                    return Hold("Audit quote is not present in the bound narrative.");
                _ = Text(finding, "reason");
                var assessment = Text(finding, "assessment");
                if (assessment is not ("supported" or "nonasserted" or "missing" or "contradicted" or "uncertain"))
                    return Hold("Unknown claim assessment.");
                if (string.IsNullOrWhiteSpace(quote) &&
                    (direction != "stateToNarrative" || assessment is not ("missing" or "uncertain")))
                    return Hold("A prose-origin finding requires an exact nonempty quote.");
                adverse |= assessment is "missing" or "contradicted" or "uncertain";
                var count = 0;
                foreach (var evidence in finding.GetProperty("evidence").EnumerateArray())
                {
                    Shape(evidence, "documentId", "pointer", "value");
                    var id = Text(evidence, "documentId");
                    var pointer = evidence.GetProperty("pointer").GetString();
                    if (pointer is null || !state.TryGetValue(id, out var document) ||
                        !TryResolve(document, pointer, out var actual) ||
                        !JsonNode.DeepEquals(JsonNode.Parse(actual.GetRawText()), JsonNode.Parse(evidence.GetProperty("value").GetRawText())))
                        return Hold("Audit evidence does not resolve to the exact packet state.");
                    count++;
                }
                if ((assessment == "supported" || direction == "stateToNarrative") && count == 0)
                    return Hold("Supported mechanical claim requires canonical evidence.");
            }
            return verdict == "pass" && !adverse
                ? new(true, "")
                : Hold("Independent audit found a discrepancy or could not determine consistency.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or OverflowException or ArgumentException)
        {
            return Hold("Malformed independent audit response or packet state.");
        }
    }

    private static MortalTurnAuditResult Hold(string reason) => new(false, reason);

    private static string Text(JsonElement value, string property)
    {
        var text = value.GetProperty(property).GetString();
        if (string.IsNullOrWhiteSpace(text)) throw new JsonException("Required nonempty text.");
        return text;
    }

    private static void Shape(JsonElement value, params string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new JsonException("Expected object.");
        var actual = value.EnumerateObject().Select(p => p.Name).ToArray();
        if (actual.Length != names.Length || actual.Distinct(StringComparer.Ordinal).Count() != actual.Length ||
            names.Any(n => !actual.Contains(n, StringComparer.Ordinal)))
            throw new JsonException("Unexpected, missing or duplicate property.");
    }

    private static void NoDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException("Duplicate property.");
                NoDuplicates(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var child in value.EnumerateArray()) NoDuplicates(child);
    }

    private static bool TryResolve(JsonElement root, string pointer, out JsonElement value)
    {
        value = root;
        if (pointer.Length == 0) return true;
        if (pointer[0] != '/') return false;
        foreach (var encoded in pointer[1..].Split('/'))
        {
            var token = new StringBuilder();
            for (var i = 0; i < encoded.Length; i++)
            {
                if (encoded[i] != '~') { token.Append(encoded[i]); continue; }
                if (++i >= encoded.Length || encoded[i] is not ('0' or '1')) return false;
                token.Append(encoded[i] == '0' ? '~' : '/');
            }
            var key = token.ToString();
            if (value.ValueKind == JsonValueKind.Object)
            {
                if (!value.TryGetProperty(key, out value)) return false;
            }
            else if (value.ValueKind == JsonValueKind.Array)
            {
                if (key.Length == 0 || (key.Length > 1 && key[0] == '0') ||
                    !int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var index) ||
                    index >= value.GetArrayLength()) return false;
                value = value[index];
            }
            else return false;
        }
        return true;
    }
}
