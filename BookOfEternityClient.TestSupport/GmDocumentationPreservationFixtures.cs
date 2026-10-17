using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace BookOfEternityClient.Tests;

// Pure document fixtures. No engine, authority, provider, process, archive or publication APIs.
internal sealed class GmDocumentationPreservationFixtures
{
    internal static readonly string[] Topics = ["L", "S", "G", "A", "H", "C", "E", "P", "M", "W"];
    private static readonly IReadOnlyDictionary<string, string> FrozenSources = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["OtherGuides/Afterlife_Contract_Matrix.md"] = "3e85d05ae513b2fa8215ef54b153347a0408ff7459a364cfd840a0c459d7993b",
        ["Examples/E_CLI_Afterlife_Turns.txt"] = "2865180ed71387a21b30763c00f9fd26c97a1c3937ad4addcbbb572d69c9ed49",
        ["CLI_Rules_Index.md"] = "c33838a3a8a0d1dd5ff3305698d6a9ec861c2e3513775f2d0e7b39881f196f29"
    };
    internal readonly JsonObject Map;
    internal readonly JsonObject Navigation;
    private readonly string _repo;
    internal GmDocumentationPreservationFixtures(string repo)
    {
        _repo = repo;
        Map = ReadJson("specs/1557-gm-doc-modularization/content-map.json");
        Navigation = ReadJson("specs/1557-gm-doc-modularization/navigation-edits.json");
    }
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    internal static byte[] Bytes(string text) => new UTF8Encoding(false, true).GetBytes(text);
    internal static string Text(byte[] bytes) => new UTF8Encoding(false, true).GetString(bytes);
    internal JsonObject ReadJson(string path) => JsonNode.Parse(File.ReadAllBytes(Path.Combine(_repo, path)))!.AsObject();
    internal IEnumerable<JsonObject> Blocks => Map["blocks"]!.AsArray().Select(node => node!.AsObject());
    internal IEnumerable<JsonObject> Edits => Navigation["edits"]!.AsArray().Select(node => node!.AsObject());
    internal string[] Destinations => Map["destinationWrappers"]!.AsObject().Select(pair => pair.Key).ToArray();
    internal Dictionary<string, byte[]> ReadActualDestinations()
    {
        var paths = Destinations.Where(path => File.Exists(Path.Combine(_repo, path))).ToList();
        foreach (var (folder, prefix) in new[] { ("OtherGuides", "Afterlife_"), ("Examples", "E_CLI_Afterlife_") })
            paths.AddRange(Directory.EnumerateFiles(Path.Combine(_repo, folder), "*", SearchOption.TopDirectoryOnly)
                .Where(path => Path.GetFileName(path).StartsWith(prefix, StringComparison.Ordinal))
                .Select(path => Path.GetRelativePath(_repo, path).Replace('\\', '/')));
        return paths.Distinct(StringComparer.Ordinal).ToDictionary(path => path,
            path => File.ReadAllBytes(Path.Combine(_repo, path)), StringComparer.Ordinal);
    }
    internal static string S(JsonNode node, string key) => node[key]!.GetValue<string>();
    internal static int I(JsonNode node, string key) => node[key]!.GetValue<int>();
    internal static string[] Strings(JsonNode node) => node.AsArray().Select(value => value!.GetValue<string>()).ToArray();

    internal IReadOnlyList<string> VerifyMap()
    {
        var issues = new List<string>();
        var sources = Map["sources"]!.AsArray();
        if (sources.Count != FrozenSources.Count) issues.Add("map.sources");
        var ids = Blocks.Select(block => S(block, "id")).ToArray();
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length) issues.Add("map.duplicate_owner");
        foreach (var source in sources)
        {
            var path = S(source!, "path");
            if (!FrozenSources.TryGetValue(path, out var hash) || hash != S(source!, "sha256")) issues.Add("map.baseline");
            var cursor = 0;
            foreach (var block in Blocks.Where(block => S(block, "source") == path).OrderBy(block => I(block, "ordinal")))
            {
                if (I(block, "start") != cursor || I(block, "end") <= cursor) issues.Add("map.partition");
                cursor = I(block, "end");
                if (!Destinations.Contains(S(block, "destination"), StringComparer.Ordinal)) issues.Add("map.owner_missing");
                if (S(block, "destination").Contains("..", StringComparison.Ordinal)) issues.Add("map.escaped_owner");
            }
            if (cursor != I(source!, "length")) issues.Add("map.partition");
        }
        foreach (var group in Blocks.GroupBy(block => S(block, "destination")))
            if (!group.OrderBy(block => I(block, "destinationOrdinal")).Select(block => I(block, "destinationOrdinal"))
                    .SequenceEqual(Enumerable.Range(1, group.Count()))) issues.Add("map.destination_order");
        return issues;
    }

    internal Dictionary<string, byte[]> CreateCompleteFixture()
    {
        // Before moves, derive owned negative fixtures from exact frozen source bytes.
        // After moves, only an already-valid actual candidate may seed the same mutation controls.
        var sources = FrozenSources.Keys.ToDictionary(path => path, path => File.ReadAllBytes(Path.Combine(_repo, path)), StringComparer.Ordinal);
        if (sources.Any(pair => Hash(pair.Value) != FrozenSources[pair.Key]))
        {
            var actual = ReadActualDestinations();
            var errors = VerifyMoved(actual);
            if (errors.Count != 0) throw new InvalidDataException("Negative fixture seed requires a valid actual candidate: " + string.Join(";", errors));
            return actual;
        }
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var destination in Destinations)
        {
            using var stream = new MemoryStream();
            stream.Write(Bytes(Map["destinationWrappers"]![destination]!.GetValue<string>()));
            foreach (var block in Blocks.Where(block => S(block, "destination") == destination).OrderBy(block => I(block, "destinationOrdinal")))
            {
                stream.Write(Bytes(S(block, "contextWrapper")));
                stream.Write(Bytes(S(block, "startMarker")));
                var original = sources[S(block, "source")][I(block, "start")..I(block, "end")];
                if (Hash(original) != S(block, "sha256")) throw new InvalidDataException("Frozen block mismatch: " + S(block, "id"));
                stream.Write(Forward(block, original));
                stream.Write(Bytes(S(block, "endMarker")));
            }
            result.Add(destination, stream.ToArray());
        }
        return result;
    }

    private IEnumerable<JsonObject> BlockEdits(JsonObject block) => Edits.Where(edit => S(edit, "source") == S(block, "source") &&
        I(edit, "start") >= I(block, "start") && I(edit, "end") <= I(block, "end"));
    private byte[] Forward(JsonObject block, byte[] original)
    {
        foreach (var edit in BlockEdits(block).OrderByDescending(edit => I(edit, "start")))
        {
            var offset = I(edit, "start") - I(block, "start");
            var before = Convert.FromBase64String(S(edit, "beforeBase64"));
            var after = Convert.FromBase64String(S(edit, "afterBase64"));
            if (!original.AsSpan(offset, before.Length).SequenceEqual(before)) throw new InvalidDataException("Navigation before-image mismatch.");
            original = original[..offset].Concat(after).Concat(original[(offset + before.Length)..]).ToArray();
        }
        return original;
    }
    private byte[] Reverse(JsonObject block, byte[] payload, List<string> issues)
    {
        foreach (var edit in BlockEdits(block).OrderBy(edit => I(edit, "start")))
        {
            var after = Convert.FromBase64String(S(edit, "afterBase64"));
            var before = Convert.FromBase64String(S(edit, "beforeBase64"));
            var index = payload.AsSpan().IndexOf(after);
            if (index < 0 || payload.AsSpan(index + after.Length).IndexOf(after) >= 0)
            { issues.Add("navigation.inverse:" + S(edit, "id")); return payload; }
            payload = payload[..index].Concat(before).Concat(payload[(index + after.Length)..]).ToArray();
        }
        return payload;
    }
    internal IReadOnlyList<string> VerifyMoved(IReadOnlyDictionary<string, byte[]> candidate)
    {
        var issues = new List<string>(VerifyMap());
        var originals = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var extra in candidate.Keys.Except(Destinations, StringComparer.Ordinal))
        {
            var recorded = Map["existingScopedNonMovedFiles"]!.AsArray().FirstOrDefault(row => S(row!, "path") == extra);
            if (recorded is null || Hash(candidate[extra]) != S(recorded, "sha256")) issues.Add("destination.orphan:" + extra);
        }
        foreach (var destination in Destinations)
        {
            if (!candidate.TryGetValue(destination, out var document)) { issues.Add("destination.missing:" + destination); continue; }
            var wrapper = Bytes(Map["destinationWrappers"]![destination]!.GetValue<string>());
            if (!document.AsSpan().StartsWith(wrapper)) { issues.Add("wrapper.unlisted:" + destination); continue; }
            var cursor = wrapper.Length;
            foreach (var block in Blocks.Where(block => S(block, "destination") == destination).OrderBy(block => I(block, "destinationOrdinal")))
            {
                var id = S(block, "id");
                var start = Bytes(S(block, "startMarker")); var end = Bytes(S(block, "endMarker"));
                var context = Bytes(S(block, "contextWrapper"));
                if (document.AsSpan().IndexOf(start) < 0) { issues.Add("payload.missing:" + id); continue; }
                var first = document.AsSpan().IndexOf(start);
                if (document.AsSpan(first + start.Length).IndexOf(start) >= 0) { issues.Add("payload.duplicate:" + id); continue; }
                if (cursor > document.Length || !document.AsSpan(cursor).StartsWith(context)) { issues.Add("context.missing:" + id); continue; }
                cursor += context.Length;
                if (!document.AsSpan(cursor).StartsWith(start)) { issues.Add("payload.order:" + id); continue; }
                cursor += start.Length;
                var tail = document.AsSpan(cursor).IndexOf(end);
                if (tail < 0) { issues.Add("payload.missing_end:" + id); continue; }
                var payload = document[cursor..(cursor + tail)]; cursor += tail + end.Length;
                var expectedSnippets = Map["snippetInventory"]!.AsArray().Where(row => S(row!, "block") == id).ToArray();
                var actualSnippets = ExtractSnippets(payload);
                if (actualSnippets.Count != expectedSnippets.Length) issues.Add("snippet.count:" + id);
                else for (var index = 0; index < actualSnippets.Count; index++)
                {
                    if (actualSnippets[index].Hash != S(expectedSnippets[index]!, "bodySha256")) issues.Add("snippet.order:" + id);
                    if (actualSnippets[index].Expected != S(expectedSnippets[index]!, "expected")) issues.Add("snippet.classification:" + id);
                }
                var reversed = Reverse(block, payload, issues);
                if (Hash(reversed) != S(block, "sha256")) issues.Add("payload.unlisted_edit:" + id);
                originals[id] = reversed;
            }
            if (cursor != document.Length) issues.Add("wrapper.unlisted:" + destination);
        }
        foreach (var source in Map["sources"]!.AsArray())
        {
            var sourceBlocks = Blocks.Where(block => S(block, "source") == S(source!, "path")).OrderBy(block => I(block, "ordinal")).ToArray();
            if (sourceBlocks.All(block => originals.ContainsKey(S(block, "id"))))
            {
                var reassembled = sourceBlocks.SelectMany(block => originals[S(block, "id")]).ToArray();
                if (reassembled.Length != I(source!, "length") || Hash(reassembled) != S(source!, "sha256")) issues.Add("source.reassembly:" + S(source!, "path"));
            }
        }
        return issues;
    }
    internal sealed record Snippet(string Hash, string Expected, int Start, int End);
    internal static List<Snippet> ExtractSnippets(byte[] payload)
    {
        var text = Text(payload); var lines = text.Split('\n'); var offsets = new int[lines.Length];
        for (var n = 1; n < lines.Length; n++) offsets[n] = offsets[n - 1] + Bytes(lines[n - 1] + "\n").Length;
        var result = new List<Snippet>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (!Regex.IsMatch(lines[i], @"^\s*```json\s*$", RegexOptions.IgnoreCase)) continue;
            var end = i + 1;
            while (end < lines.Length && !Regex.IsMatch(lines[end], @"^\s*```\s*$")) end++;
            if (end == lines.Length) throw new InvalidDataException("Incomplete JSON fence.");
            var context = string.Join('\n', lines.Skip(Math.Max(0, i - 12)).Take(i - Math.Max(0, i - 12) + 1));
            var invalid = new[] { "INVALID", "INCORRECT", "VIOLATION", "НЕПРАВ" }.Any(marker => context.Contains(marker, StringComparison.OrdinalIgnoreCase));
            result.Add(new Snippet(Hash(payload[offsets[i + 1]..offsets[end]]), invalid ? "Invalid" : "Valid", offsets[i],
                end + 1 < offsets.Length ? offsets[end + 1] : payload.Length)); i = end;
        }
        return result;
    }
    internal static byte[] ReplaceOnce(byte[] document, byte[] before, byte[] after)
    {
        var index = document.AsSpan().IndexOf(before);
        if (index < 0) throw new InvalidDataException("Mutation target absent.");
        return document[..index].Concat(after).Concat(document[(index + before.Length)..]).ToArray();
    }
    internal IReadOnlyList<string> VerifyManifest(JsonObject manifest)
    {
        var issues = new List<string>();
        foreach (var family in Map["manifestSource"]!["affectedFamilyCounts"]!.AsObject())
        {
            if (manifest[family.Key] is not JsonArray rows || rows.Count != family.Value!.GetValue<int>())
                issues.Add("manifest.family_count:" + family.Key);
            if (manifest[family.Key] is JsonArray identityRows)
            {
                var identities = identityRows.OfType<JsonObject>().Select(row => row["contractId"]?.GetValue<string>() ?? row["id"]?.GetValue<string>())
                    .Where(value => value is not null).ToArray();
                if (identities.Distinct(StringComparer.Ordinal).Count() != identities.Length) issues.Add("manifest.duplicate:" + family.Key);
            }
        }
        foreach (var recorded in Map["manifestSource"]!["rows"]!.AsArray())
        {
            var row = recorded!.AsObject(); var family = S(row, "family"); var index = I(row, "index");
            if (manifest[family] is not JsonArray rows || index >= rows.Count || rows[index] is not JsonObject actual)
            { issues.Add("manifest.missing:" + S(row, "identity")); continue; }
            var semantics = actual.DeepClone().AsObject();
            foreach (var locator in row["beforeLocators"]!.AsObject()) semantics.Remove(locator.Key);
            if (!JsonNode.DeepEquals(semantics, JsonNode.Parse(S(row, "semanticJson")))) issues.Add("manifest.semantic:" + S(row, "identity"));
            foreach (var locator in row["beforeLocators"]!.AsObject())
            {
                if (locator.Value is JsonValue && actual[locator.Key]?.GetValue<string>() != Path.GetFileName(S(row, "destination")))
                    issues.Add("manifest.locator:" + S(row, "identity"));
            }
        }
        return issues;
    }
    internal JsonObject CreateCompleteManifestFixture()
    {
        var actual = ReadJson("Examples/example_validation_manifest.json");
        foreach (var recorded in Map["manifestSource"]!["rows"]!.AsArray())
        {
            var row = recorded!.AsObject();
            foreach (var locator in row["beforeLocators"]!.AsObject())
                actual[S(row, "family")]![I(row, "index")]![locator.Key] = Path.GetFileName(S(row, "destination"));
        }
        return actual;
    }
    internal static string[] RouteTopics(string document, string id, int column)
    {
        var rows = document.Split('\n').Where(line => line.StartsWith("| " + id + " |", StringComparison.Ordinal)).ToArray();
        if (rows.Length != 1) throw new InvalidDataException("route.missing_or_duplicate:" + id);
        var cells = rows[0].Split('|', StringSplitOptions.None).Select(value => value.Trim()).ToArray();
        if (cells.Length != 7) throw new InvalidDataException("route.shape:" + id);
        return cells[column] == "-" ? [] : cells[column].Split(',').Order(StringComparer.Ordinal).ToArray();
    }
    internal static IReadOnlyList<string> VerifyRoute(string document, string id, string expectedPredicate, string[] required, bool core)
    {
        var errors = new List<string>();
        try
        {
            if (!RouteTopics(document, id, 3).SequenceEqual(required.Order(StringComparer.Ordinal))) errors.Add("route.required:" + id);
            if (!RouteTopics(document, id, 4).SequenceEqual(Topics.Except(required, StringComparer.Ordinal).Order(StringComparer.Ordinal))) errors.Add("route.excluded:" + id);
            var row = document.Split('\n').Single(line => line.StartsWith("| " + id + " |", StringComparison.Ordinal));
            if (row.Split('|')[2].Trim() != expectedPredicate) errors.Add("route.predicate:" + id);
            if (row.Split('|')[5].Trim() != core.ToString().ToLowerInvariant()) errors.Add("route.core:" + id);
            if (Regex.IsMatch(document, @"(?:read|open)\s+all\s+(?:afterlife\s+)?(?:modules|topic bodies)\s+(?:for|on|every)", RegexOptions.IgnoreCase)) errors.Add("route.universal_read");
            if (id == "wound_frontier" && (!document.Contains("Spiritual frontier core is correlation/terminal only", StringComparison.Ordinal) ||
                document.Contains("Frontier must read ordinary turn_request", StringComparison.Ordinal))) errors.Add("route.frontier_scope");
            if (id == "bootstrap" && !document.Contains("Bootstrap reads manifest/README only.", StringComparison.Ordinal)) errors.Add("route.precedence:" + id);
            if (id == "receipt_only" && !document.Contains("Dedicated receipt-only uses its exact safe packet/helper, without turn_request, ordinary scene/story/progression or module/core bodies.", StringComparison.Ordinal)) errors.Add("route.precedence:" + id);
            if (id == "repair" && !document.Contains("Validation repair follows the current permitted-read packet and does not become a new ordinary turn.", StringComparison.Ordinal)) errors.Add("route.precedence:" + id);
        }
        catch (InvalidDataException error) { errors.Add(error.Message); }
        return errors;
    }

    internal IEnumerable<JsonObject> D01Edits => Edits.Where(edit => S(edit, "kind").StartsWith("D01", StringComparison.Ordinal));
    internal string ActiveEditOwner(JsonObject edit)
    {
        var owners = Blocks.Where(block => S(block, "source") == S(edit, "source") &&
            I(block, "start") <= I(edit, "start") && I(block, "end") >= I(edit, "end")).ToArray();
        if (owners.Length > 1) throw new InvalidDataException("d01.ambiguous_owner:" + S(edit, "id"));
        return owners.Length == 1 ? S(owners[0], "destination") : S(edit, "source");
    }
    internal Dictionary<string, byte[]> ReadActualD01Scopes()
    {
        var actual = ReadActualDestinations();
        foreach (var source in D01Edits.Select(edit => S(edit, "source")).Distinct(StringComparer.Ordinal))
            if (File.Exists(Path.Combine(_repo, source))) actual[source] = File.ReadAllBytes(Path.Combine(_repo, source));
        return actual;
    }
    internal Dictionary<string, byte[]> CreateCompleteD01Fixture()
    {
        var result = CreateCompleteFixture();
        foreach (var edit in D01Edits.Where(edit => ActiveEditOwner(edit) == S(edit, "source")))
        {
            var source = S(edit, "source"); var raw = File.ReadAllBytes(Path.Combine(_repo, source));
            var before = Convert.FromBase64String(S(edit, "beforeBase64"));
            var after = Convert.FromBase64String(S(edit, "afterBase64"));
            if (raw.AsSpan().IndexOf(before) >= 0)
            {
                var offset = I(edit, "start");
                if (!raw.AsSpan(offset, before.Length).SequenceEqual(before)) throw new InvalidDataException("D01 frozen before-image mismatch.");
                raw = raw[..offset].Concat(after).Concat(raw[(offset + before.Length)..]).ToArray();
            }
            else if (raw.AsSpan().IndexOf(after) < 0) throw new InvalidDataException("D01 seed requires exact approved before/after image.");
            result[source] = raw;
        }
        return result;
    }
    internal IReadOnlyList<string> VerifyD01(IReadOnlyDictionary<string, byte[]> candidate)
    {
        var issues = new List<string>();
        foreach (var edit in D01Edits)
        {
            var id = S(edit, "id"); var owner = ActiveEditOwner(edit);
            var after = Convert.FromBase64String(S(edit, "afterBase64"));
            var before = Convert.FromBase64String(S(edit, "beforeBase64"));
            if (!candidate.TryGetValue(owner, out var active)) issues.Add("d01.owner_missing:" + id);
            else if (active.AsSpan().IndexOf(after) < 0) issues.Add("d01.after_missing:" + id);
            if (!candidate.ContainsKey(S(edit, "source"))) issues.Add("d01.navigation_missing:" + id);
            foreach (var scope in candidate)
                if (scope.Value.AsSpan().IndexOf(before) >= 0) issues.Add("d01.restored_grant:" + id + ":" + scope.Key);
            if (owner != S(edit, "source") && candidate.TryGetValue(S(edit, "source"), out var navigation) &&
                navigation.AsSpan().IndexOf(after) >= 0) issues.Add("d01.duplicate_active_body:" + id);
        }
        return issues;
    }
}
