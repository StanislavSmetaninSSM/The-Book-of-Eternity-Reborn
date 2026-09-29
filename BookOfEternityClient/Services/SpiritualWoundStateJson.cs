using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>
/// Supplies strict JSON primitives for spiritual state; hashes provide comparison, not authority.
/// </summary>
internal static class SpiritualWoundStateJson
{
    /// <summary>
    /// Reads one object after rejecting duplicate properties at every depth.
    /// </summary>
    /// <param name="json">
    /// Serialized state; missing, blank or malformed content is invalid.
    /// </param>
    /// <returns>
    /// A detached mutable parse tree.
    /// </returns>
    internal static JsonObject Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new FormatException("Missing state.");
        using var document = JsonDocument.Parse(json);
        CheckDuplicates(document.RootElement);
        return JsonNode.Parse(json) as JsonObject ?? throw new FormatException("Object required.");
    }

    /// <summary>
    /// Requires exactly the listed property names, including explicitly nullable properties.
    /// </summary>
    /// <param name="node">
    /// Object to inspect.
    /// </param>
    /// <param name="fields">
    /// Space-separated complete property allowlist.
    /// </param>
    internal static void Closed(JsonObject node, string fields)
    {
        var expected = fields.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Require(node.Count == expected.Length && expected.All(node.ContainsKey), "Closed object required.");
    }

    /// <summary>
    /// Reads a strict signed integer within an inclusive interval.
    /// </summary>
    /// <param name="node">
    /// Integer node; fractional representations and strings are invalid.
    /// </param>
    /// <param name="minimum">
    /// Inclusive minimum, zero by default.
    /// </param>
    /// <param name="maximum">
    /// Inclusive maximum, the signed 32-bit maximum by default.
    /// </param>
    /// <returns>
    /// The checked integer.
    /// </returns>
    internal static long Integer(JsonNode? node, long minimum = 0, long maximum = int.MaxValue)
    {
        var raw = node?.ToJsonString();
        if (raw is null || !long.TryParse(raw, NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var value) || value < minimum || value > maximum)
            throw new FormatException("Integer outside its allowed range.");
        return value;
    }

    /// <summary>
    /// Reads an exact normalized identifier without invisible characters.
    /// </summary>
    /// <param name="node">
    /// Nonempty JSON identifier string.
    /// </param>
    /// <returns>
    /// The unchanged identifier.
    /// </returns>
    internal static string Text(JsonNode? node)
    {
        if (node is not JsonValue value || !value.TryGetValue<string>(out var text) ||
            !ResourceMaterializationContract.IsExactIdentifier(text))
            throw new FormatException("Exact identifier required.");
        return text;
    }

    /// <summary>
    /// Reads the canonical lowercase SHA-256 comparison form.
    /// </summary>
    /// <param name="node">
    /// Fingerprint string.
    /// </param>
    /// <returns>
    /// The verified fingerprint text.
    /// </returns>
    internal static string Fingerprint(JsonNode? node)
    {
        var text = Text(node);
        Require(text.Length == 71 && text.StartsWith("sha256:", StringComparison.Ordinal) &&
            text[7..].All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'), "Invalid SHA-256 fingerprint.");
        return text;
    }

    /// <summary>
    /// Decodes canonical base64 without accepting whitespace or alternative padding bits.
    /// </summary>
    /// <param name="node">
    /// Required base64 string; an empty string represents an existing empty payload.
    /// </param>
    /// <returns>
    /// A newly allocated byte array with the exact decoded contents.
    /// </returns>
    internal static byte[] DecodeBase64(JsonNode? node)
    {
        if (node is not JsonValue value || !value.TryGetValue<string>(out var text) || text is null)
            throw new FormatException("Base64 string required.");
        var bytes = Convert.FromBase64String(text);
        Require(Convert.ToBase64String(bytes) == text, "Canonical base64 required.");
        return bytes;
    }

    /// <summary>
    /// Checks strict UTF-8 JSON object syntax and duplicate properties while retaining exact bytes.
    /// The owning payload parser remains responsible for its semantic contract.
    /// </summary>
    /// <param name="node">
    /// Canonical base64 containing one UTF-8 JSON object with an optional single leading preamble.
    /// </param>
    /// <returns>
    /// Original decoded bytes without normalization or an additional payload size limit.
    /// </returns>
    internal static byte[] DecodeJsonObjectBytes(JsonNode? node)
    {
        var bytes = DecodeBase64(node);
        try
        {
            Parse(DecodeUtf8JsonText(bytes));
        }
        catch (DecoderFallbackException error)
        {
            throw new FormatException("Strict UTF-8 JSON required.", error);
        }
        return bytes;
    }

    /// <summary>
    /// Decodes strict UTF-8 while consuming at most one leading preamble from a canonical text writer.
    /// </summary>
    /// <param name="bytes">
    /// Exact physical JSON bytes, retained unchanged for fingerprint and before-image comparisons.
    /// </param>
    /// <returns>
    /// Decoded text for strict JSON parsing; malformed UTF-8 throws and any second preamble remains in the text.
    /// </returns>
    internal static string DecodeUtf8JsonText(byte[] bytes)
    {
        var offset = bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()) ? 3 : 0;
        return new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
    }

    /// <summary>
    /// Reads a closed image using existing canonical before-image fingerprint and detachment rules.
    /// Raw file bytes are not parsed as JSON or normalized.
    /// </summary>
    /// <param name="image">
    /// Required image object from a duplicate-checked owning root.
    /// </param>
    /// <param name="expectedRole">
    /// Exact role, original_before or candidate_after, required by the containing collection.
    /// </param>
    /// <param name="registeredPaths">
    /// Trusted registered snapshot inventory supplied by the owner, never by serialized packet data.
    /// Paths are compared ordinally even when the collection uses another comparer.
    /// </param>
    /// <returns>
    /// Detached exact image; an absent file retains its distinct absent fingerprint.
    /// </returns>
    internal static CanonicalBeforeImage ReadImage(
        JsonObject image, string expectedRole, IEnumerable<string> registeredPaths)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(registeredPaths);
        if (expectedRole is not ("original_before" or "candidate_after"))
            throw new ArgumentException("Unknown image role.", nameof(expectedRole));
        Closed(image, "path role existed contentBase64 contentFingerprint");
        var path = Text(image["path"]);
        Require(registeredPaths.Any(registered => string.Equals(registered, path, StringComparison.Ordinal)),
            "Image path is not registered.");
        Require(Text(image["role"]) == expectedRole, "Image role differs from its containing collection.");
        if (image["existed"] is not JsonValue value || !value.TryGetValue<bool>(out var existed))
            throw new FormatException("Exact existence flag required.");
        byte[]? bytes = null;
        if (existed) bytes = DecodeBase64(image["contentBase64"]);
        else Require(image["contentBase64"] is null, "Absent image cannot contain bytes.");
        var result = new CanonicalBeforeImage(existed, bytes);
        Require(Fingerprint(image["contentFingerprint"]) == result.Fingerprint, "Image fingerprint differs.");
        return result;
    }

    /// <summary>
    /// Canonicalizes property order while retaining array order and scalar representations.
    /// </summary>
    /// <param name="node">
    /// JSON tree to serialize, including <see langword="null"/>.
    /// </param>
    /// <returns>
    /// Compact recursively ordered JSON.
    /// </returns>
    internal static string Canonical(JsonNode? node)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, node);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Computes a versioned domain-separated digest, excluding specified derived fields.
    /// </summary>
    /// <param name="node">
    /// Payload copied before hashing.
    /// </param>
    /// <param name="domain">
    /// Caller-selected domain suffix.
    /// </param>
    /// <param name="excluded">
    /// Top-level derived fields excluded to avoid circular identities.
    /// </param>
    /// <returns>
    /// Lowercase SHA-256 fingerprint.
    /// </returns>
    internal static string Hash(JsonObject node, string domain, params string[] excluded)
    {
        var copy = node.DeepClone().AsObject();
        foreach (var field in excluded) copy.Remove(field);
        var bytes = Encoding.UTF8.GetBytes("book_of_eternity.spiritual_wound." + domain + ":v1\n" + Canonical(copy));
        return "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    /// <summary>
    /// Rejects a violated state invariant with a parser-level diagnostic.
    /// </summary>
    /// <param name="condition">
    /// Required invariant.
    /// </param>
    /// <param name="message">
    /// Non-sensitive diagnostic text.
    /// </param>
    internal static void Require(bool condition, string message)
    {
        if (!condition) throw new FormatException(message);
    }

    /// <summary>
    /// Rejects duplicate names before a node parser can discard them.
    /// </summary>
    /// <param name="element">
    /// Raw JSON subtree.
    /// </param>
    private static void CheckDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                Require(names.Add(property.Name), "Duplicate JSON property.");
                CheckDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) CheckDuplicates(child);
    }

    /// <summary>
    /// Writes deterministic object ordering without changing array order.
    /// </summary>
    /// <param name="writer">
    /// Active writer.
    /// </param>
    /// <param name="node">
    /// JSON subtree, including <see langword="null"/>.
    /// </param>
    private static void Write(Utf8JsonWriter writer, JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            writer.WriteStartObject();
            foreach (var pair in obj.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(pair.Key);
                Write(writer, pair.Value);
            }
            writer.WriteEndObject();
        }
        else if (node is JsonArray array)
        {
            writer.WriteStartArray();
            foreach (var item in array) Write(writer, item);
            writer.WriteEndArray();
        }
        else if (node is null) writer.WriteNullValue();
        else node.WriteTo(writer);
    }
}
