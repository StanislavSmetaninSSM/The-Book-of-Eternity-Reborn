using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace BookOfEternityClient.Tests;

// Pure #1557 B byte/XML fixtures. No engine, environment override, process or publication authority.
internal sealed partial class GmDocumentationBlock5Fixtures
{
    internal const string Folder = "specs/1557-gm-doc-modularization/";
    private readonly string _repo;
    internal readonly JsonObject Map;
    internal readonly Dictionary<string, byte[]> Baselines = new(StringComparer.Ordinal);
    internal GmDocumentationBlock5Fixtures(string repo)
    {
        _repo = repo; Map = JsonNode.Parse(File.ReadAllBytes(Path.Combine(repo, Folder + "block5-content-map.json")))!.AsObject();
        foreach (var row in Map["baselineArtifacts"]!.AsArray())
            Baselines.Add(S(row!, "source"), Inflate(Path.Combine(repo, S(row!, "storedPath"))));
    }
    internal static string S(JsonNode row, string key) => row[key]!.GetValue<string>();
    internal static int I(JsonNode row, string key) => row[key]!.GetValue<int>();
    internal static byte[] Bytes(string value) => new UTF8Encoding(false, true).GetBytes(value);
    internal static string Text(byte[] value) => new UTF8Encoding(false, true).GetString(value);
    internal static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    private static byte[] Inflate(string path)
    {
        using var input = new GZipStream(File.OpenRead(path), CompressionMode.Decompress);
        using var output = new MemoryStream(); input.CopyTo(output); return output.ToArray();
    }
    internal IEnumerable<JsonNode> Sources => Map["sources"]!.AsArray().Select(row => row!);
    internal IEnumerable<JsonNode> Topics => Map["topics"]!.AsArray().Select(row => row!);
    internal IEnumerable<JsonNode> Groups => Sources.SelectMany(row => row["blocks"]!.AsArray().Select(item => item!));
    internal IEnumerable<JsonNode> Pieces => Sources.SelectMany(row => row["blocks"]!.AsArray().Concat(row["framing"]!.AsArray()).Select(item => item!));
    internal static string Start(JsonNode row) => "<!-- GM1557-B " + S(row, "id") + " BEGIN -->";
    internal static string End(JsonNode row) => "<!-- GM1557-B " + S(row, "id") + " END -->";
    internal byte[] Original(JsonNode row) => Baselines[S(row, "sourcePath")][I(row, "start")..I(row, "end")];
    private string Wrapped(JsonNode row) => Start(row) + Text(Original(row)) + End(row);
    // Keep nonadjacent source groups separated for the unchanged flat extractor context.
    private const string NoncontiguousExampleBoundary =
        "\n" +
        "<!-- GM1557-B nonsemantic original-source boundary\n" +
        "Source: Examples/E_Block_5.txt; original SHA256: c244c3051f5e04bb39c973a08cb270a43500f1ba16c29cc451f134ca53a48d04.\n" +
        "Previous original group: BE001; UTF-8 interval [345,1548); 1203 bytes; SHA256: 0443be92b9d5198d7f027aabebef716b9295aa7e44ff0e9e07c8455679198ad6.\n" +
        "Following original group: BE019; UTF-8 interval [267586,268638); 1052 bytes; SHA256: 8b94a391c0a6219e9d3258f6b44236ced4cc641fba16b6bc07f31df013c5e8ec.\n" +
        "The original groups were separated by 266038 UTF-8 bytes assigned to other topic owners. Their original byte slices and source order remain unchanged inside the adjacent marked regions.\n" +
        "This comment belongs only to modularization framing and preserves textual separation between the two nonadjacent source groups. The intervening topic bodies are not copied or repeated here.\n" +
        "-->\n";
    internal string[] ModulePaths => Topics.SelectMany(row => new[] { S(row, "rulePath"), S(row, "examplePath") }).ToArray();
    internal string[] AllPaths => Sources.Select(row => S(row, "path")).Concat(ModulePaths).ToArray();
    internal IReadOnlyList<string> VerifyBaseline()
    {
        var issues = new List<string>();
        foreach (var source in Sources)
        {
            var path = S(source, "path"); var bytes = Baselines[path];
            if (bytes.Length != I(source, "bytes") || Hash(bytes) != S(source, "sha256")) issues.Add("baseline.hash:" + path);
            var cursor = 0;
            foreach (var row in Pieces.Where(row => S(row, "sourcePath") == path).OrderBy(row => I(row, "start")))
            {
                if (I(row, "start") != cursor || I(row, "end") <= cursor) issues.Add("baseline.partition:" + path);
                if (Hash(Original(row)) != S(row, "sha256")) issues.Add("baseline.piece:" + S(row, "id"));
                cursor = I(row, "end");
            }
            if (cursor != bytes.Length) issues.Add("baseline.partition:" + path);
            var xml = XDocument.Parse(Text(bytes), LoadOptions.PreserveWhitespace);
            if (xml.Root?.Name != "instructionblock" || (string?)xml.Root.Attribute("id") != "5") issues.Add("baseline.context:" + path);
            if (xml.Descendants("rule").Count() != I(source, "ruleNodes") || xml.Descendants("example").Count() != I(source, "examples") ||
                xml.DescendantNodes().OfType<XCData>().Count() != I(source, "cdataSections")) issues.Add("baseline.xml_count:" + path);
            foreach (var group in source["blocks"]!.AsArray())
                if (OwnerFor(S(group!, "rootRuleId")) != S(group!, "topic")) issues.Add("baseline.owner:" + S(group!, "id"));
        }
        return issues;
    }
    internal static string OwnerFor(string id)
    {
        if (id == "5.mortal_item_materialization_v1") return "Items_Resources";
        var pieces = id.Split('.'); if (pieces.Length < 2 || pieces[0] != "5") throw new InvalidDataException("Foreign ID needs its actual XML ancestor: " + id);
        return pieces[1] switch
        {
            "0" or "8" => "Units_Ids", "1" or "7" or "10" or "24" => "Characteristics_Progression",
            "2" or "3" or "4" or "5" or "6" or "16" or "19" => "Combat_Foundations",
            "9" or "11" or "13" or "14" or "15" or "23" => "Items_Resources",
            "12" or "17" or "18" => "Narrative_Social", "20" => "Wounds",
            "21" or "22" or "25" => "World_Chronology", _ => throw new InvalidDataException("Unknown Block5 owner: " + id)
        };
    }
    private string Header(JsonNode topic, bool rules)
    {
        var pair = rules ? "../" + S(topic, "examplePath") : Path.GetFileName(S(topic, "examplePath"));
        var rule = rules ? Path.GetFileName(S(topic, "rulePath")) : "../" + S(topic, "rulePath");
        return "<!-- GM1557 Block5 " + S(topic, "topic") + "\nRead when: " + S(topic, "activation") +
            "\nMain router: " + Link(S(topic, rules ? "rulePath" : "examplePath"), MainRouter, "CLI rules index") + "." +
            "\nPrerequisites: inherited [rule context](" + (rules ? "Block_5.txt" : "../Rules/Block_5.txt") +
            ") and [example context](" + (rules ? "../Examples/E_Block_5.txt" : "E_Block_5.txt") +
            "); only actual current-request dependencies in the conditional router.\nAuthor here: only current-request-authorized outputs under the unchanged contracts." +
            "\nNever author: invented client-owned identities/seals or unissued receipts; no canonical/pending mutation outside the current contract. Lawful named owner/output surfaces remain governed by the existing contract." +
            "\n" + MandatoryText(S(topic, "topic"), S(topic, rules ? "rulePath" : "examplePath")) +
            "\n" + DependencyText(S(topic, "topic"), S(topic, rules ? "rulePath" : "examplePath")) +
            "\nWorked examples: [paired examples](" + pair + "); [paired rules](" + rule + ").\n-->\n";
    }
    internal string Navigation(string source)
    {
        return "\n# Block 5 conditional topic navigation\n[Main router](../CLI_Rules_Index.md).\nRead only current applicable topics and their current-contract dependencies. Bootstrap, receipt-only and exact repair retain their packet restrictions.\n" +
            "IDs below retain original source order within each owner, including foreign descendants.\n" +
            "| Topic | Exact original rule IDs | Rules | Worked examples |\n|---|---|---|---|\n" + string.Join('\n', Topics.Select(topic => NavigatorRow(source, topic))) + "\n";
    }
    internal string RouterAddition => "\n## Block_5 conditional topic routing\n[Rule context](Rules/Block_5.txt); [example context](Examples/E_Block_5.txt).\n" +
        "Read only topics activated by the current request and its actual dependencies; preserve bootstrap/receipt/repair restrictions. Availability never mandates every topic.\n" +
        "| Topic | Read when | Owned pair | Exact conditional prerequisites |\n|---|---|---|---|\n" + string.Join('\n', Topics.Select(OrdinaryRouterRow)) + "\n" + SpecialRouteRows;
    internal Dictionary<string, byte[]> CompleteFixture()
    {
        var docs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var source in Sources)
        {
            var frames = source["framing"]!.AsArray().OrderBy(row => I(row!, "start")).ToArray();
            docs.Add(S(source, "path"), Bytes(Wrapped(frames[0]!) + Navigation(S(source, "path")) + string.Concat(frames.Skip(1).Select(row => Wrapped(row!)))));
        }
        foreach (var topic in Topics)
        foreach (var rules in new[] { true, false })
        {
            var path = S(topic, rules ? "rulePath" : "examplePath");
            var groups = Groups.Where(row => S(row, "destination") == path).OrderBy(row => I(row, "start")).ToArray();
            var text = Header(topic, rules) + "<instructionblock id=\"5\">\n";
            if (rules)
            {
                text += "<content type=\"ruleset\">\n" + string.Concat(groups.Where(row => row["inheritedAncestors"]!.AsArray().Any(a => S(a!, "tag") == "content")).Select(Wrapped)) + "\n</content>\n";
                text += string.Concat(groups.Where(row => !row["inheritedAncestors"]!.AsArray().Any(a => S(a!, "tag") == "content")).Select(Wrapped));
            }
            else text += string.Concat(groups.Select((row, index) =>
                (index > 0 && S(groups[index - 1], "id") == "BE001" && S(row, "id") == "BE019" ? NoncontiguousExampleBoundary : "") + Wrapped(row)));
            docs.Add(path, Bytes(text + "\n</instructionblock>\n"));
        }
        return docs;
    }
    internal Dictionary<string, byte[]> ActualDocuments()
    {
        var paths = AllPaths.ToHashSet(StringComparer.Ordinal);
        foreach (var pair in new[] { ("Rules", "Block_5_"), ("Examples", "E_Block_5_") })
            foreach (var path in Directory.EnumerateFiles(Path.Combine(_repo, pair.Item1), pair.Item2 + "*.txt", SearchOption.TopDirectoryOnly))
                paths.Add(Path.GetRelativePath(_repo, path).Replace('\\', '/'));
        return paths.Where(path => File.Exists(Path.Combine(_repo, path))).ToDictionary(path => path, path => File.ReadAllBytes(Path.Combine(_repo, path)), StringComparer.Ordinal);
    }
    internal IReadOnlyList<string> VerifyMoved(IReadOnlyDictionary<string, byte[]> docs)
    {
        var issues = new List<string>(VerifyBaseline()); var complete = CompleteFixture();
        foreach (var path in docs.Keys.Except(AllPaths, StringComparer.Ordinal)) issues.Add(path.Contains("/nested/", StringComparison.Ordinal) ? "layout.not_flat:" + path : "destination.orphan:" + path);
        foreach (var row in Pieces)
        {
            var path = S(row, "destination"); if (!docs.TryGetValue(path, out var data)) { issues.Add("destination.missing:" + path); continue; }
            var text = Text(data); var begin = text.IndexOf(Start(row), StringComparison.Ordinal);
            if (begin < 0) { issues.Add("payload.missing:" + S(row, "id")); continue; }
            if (text.IndexOf(Start(row), begin + Start(row).Length, StringComparison.Ordinal) >= 0) issues.Add("payload.duplicate:" + S(row, "id"));
            begin += Start(row).Length; var end = text.IndexOf(End(row), begin, StringComparison.Ordinal);
            if (end < 0) issues.Add("payload.missing_end:" + S(row, "id"));
            else if (Hash(Bytes(text[begin..end])) != S(row, "sha256")) issues.Add("payload.unlisted:" + S(row, "id"));
        }
        foreach (var pair in complete)
            if (docs.TryGetValue(pair.Key, out var actual) && !actual.SequenceEqual(pair.Value)) issues.Add("wrapper.or_order:" + pair.Key);
        issues.AddRange(VerifyXml(docs)); return issues;
    }
    private IReadOnlyList<string> VerifyXml(IReadOnlyDictionary<string, byte[]> docs)
    {
        var issues = new List<string>();
        foreach (var topic in Topics)
        foreach (var rules in new[] { true, false })
        {
            var path = S(topic, rules ? "rulePath" : "examplePath"); if (!docs.TryGetValue(path, out var bytes)) continue;
            XDocument xml;
            try { xml = XDocument.Parse(Text(bytes), LoadOptions.PreserveWhitespace); }
            catch (System.Xml.XmlException) { issues.Add("xml.invalid:" + path); continue; }
            if (xml.Root?.Name != "instructionblock" || (string?)xml.Root.Attribute("id") != "5") issues.Add("xml.context:" + path);
            var baseline = XDocument.Parse(Text(Baselines[rules ? "Rules/Block_5.txt" : "Examples/E_Block_5.txt"]), LoadOptions.PreserveWhitespace);
            var expectedNodes = baseline.Descendants("rule").Where(node => OwnerFor((string)(node.Ancestors("rule").LastOrDefault() ?? node).Attribute("id")!) == S(topic, "topic")).ToArray();
            var actualNodes = xml.Descendants("rule").ToArray(); var actualIds = actualNodes.Select(node => (string)node.Attribute("id")!).ToArray();
            if (actualIds.Distinct(StringComparer.Ordinal).Count() != actualIds.Length) issues.Add("xml.duplicate_id:" + path);
            foreach (var node in actualNodes)
            {
                var id = (string)node.Attribute("id")!; var expected = baseline.Descendants("rule").FirstOrDefault(value => (string?)value.Attribute("id") == id);
                if (expected is null) { issues.Add("xml.unknown_id:" + id); continue; }
                var oldTop = (string)(expected.Ancestors("rule").LastOrDefault() ?? expected).Attribute("id")!;
                var newTop = (string)(node.Ancestors("rule").LastOrDefault() ?? node).Attribute("id")!;
                if (OwnerFor(oldTop) != S(topic, "topic")) issues.Add("xml.owner:" + id);
                if (oldTop != newTop || !expected.Ancestors("rule").Select(a => (string?)a.Attribute("id")).SequenceEqual(node.Ancestors("rule").Select(a => (string?)a.Attribute("id")))) issues.Add("xml.ancestor:" + id);
                if (expected.Parent?.Name != node.Parent?.Name) issues.Add("xml.context:" + id);
            }
            if (!expectedNodes.Select(node => (string?)node.Attribute("id")).SequenceEqual(actualIds)) issues.Add("xml.rule_order_or_missing:" + path);
            var expectedExamples = expectedNodes.Where(node => !node.Ancestors("rule").Any()).SelectMany(node => node.Descendants("example")).ToArray();
            var actualExamples = xml.Descendants("example").ToArray();
            if (!expectedExamples.Select(node => node.Element("title")?.Value).SequenceEqual(actualExamples.Select(node => node.Element("title")?.Value))) issues.Add("xml.example_order:" + path);
            if (!expectedExamples.Select(node => (string?)node.Attribute("type")).SequenceEqual(actualExamples.Select(node => (string?)node.Attribute("type")))) issues.Add("xml.example_class:" + path);
        }
        return issues;
    }
    internal IReadOnlyList<string> VerifyActualExtractedInputs()
    {
        var issues = new List<string>(); var paths = ModulePaths.Where(path => path.StartsWith("Examples/", StringComparison.Ordinal)).Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal);
        var actual = ExampleSnippetExtractor.ExtractAll().Where(row => paths.Contains(row.File)).ToArray();
        var expected = Map["actualExistingExtractor"]!["rows"]!.AsArray().Select(row => row!).OrderBy(row => I(row, "ordinal")).ToArray();
        if (actual.Length != expected.Length) issues.Add("extractor.count");
        var rebound = new List<int>();
        foreach (var row in expected)
        {
            var matches = actual.Where(item => item.File == Path.GetFileName(S(row, "destination")) &&
                item.Kind == S(row, "kind") && Bytes(item.RawText).Length == I(row, "utf8Bytes") &&
                Hash(Bytes(item.RawText)) == S(row, "bodySha256") && item.Expected.ToString() == S(row, "expected")).ToArray();
            if (matches.Length != 1) issues.Add("extractor.occurrence:" + I(row, "ordinal"));
            else rebound.Add(I(row, "ordinal"));
        }
        if (!rebound.SequenceEqual(Enumerable.Range(1, expected.Length))) issues.Add("extractor.original_source_ordinals");
        // ExtractAll sorts physical filenames. Compare each module's real occurrence order with
        // the independent original-source ordinal map, never with the old global filename order.
        foreach (var group in expected.GroupBy(row => Path.GetFileName(S(row, "destination")), StringComparer.Ordinal))
        {
            var expectedTuples = group.Select(row => (S(row, "kind"), S(row, "bodySha256"), S(row, "expected"))).ToArray();
            var real = actual.Where(item => item.File == group.Key).ToArray();
            if (!expectedTuples.SequenceEqual(real.Select(item => (item.Kind, Hash(Bytes(item.RawText)), item.Expected.ToString()))) ||
                !real.Select(item => item.Line).SequenceEqual(real.Select(item => item.Line).Order()))
                issues.Add("extractor.module_order:" + group.Key);
        }
        return issues;
    }
    internal IReadOnlyList<string> VerifyActualManifest(JsonObject manifest)
    {
        var issues = new List<string>(); var snippets = ExampleSnippetExtractor.ExtractAll().ToArray();
        var locators = new Dictionary<(string Family, int Index), (string File, int Line)>();
        foreach (var row in Map["manifestRows"]!.AsArray())
        {
            var family = S(row!, "family"); var index = I(row!, "index"); var file = "E_Block_5_Units_Ids.txt";
            var source = snippets.Where(item => item.File == file && Hash(Bytes(item.RawText)) == S(row, "bodySha256")).ToArray();
            if (source.Length != 1) { issues.Add("manifest.input_missing:" + index); continue; }
            locators.Add((family, index), (file, source[0].Line));
        }
        issues.AddRange(VerifyManifestSemantics(manifest, locators));
        return issues;
    }
    internal JsonObject CompleteManifestBaseline()
    {
        var artifact = Map["manifestBaselineArtifact"]!;
        var stored = File.ReadAllBytes(Path.Combine(_repo, S(artifact, "storedPath")));
        var raw = Inflate(Path.Combine(_repo, S(artifact, "storedPath")));
        if (Hash(stored) != S(artifact, "storedSha256") || raw.Length != I(artifact, "bytes") || Hash(raw) != S(artifact, "sha256"))
            throw new InvalidDataException("manifest.baseline_hash");
        return JsonNode.Parse(raw)!.AsObject();
    }
    internal IReadOnlyList<string> VerifyManifestSemantics(JsonObject manifest,
        IReadOnlyDictionary<(string Family, int Index), (string File, int Line)> locators)
    {
        var issues = new List<string>(); var baseline = CompleteManifestBaseline(); var normalized = manifest.DeepClone().AsObject();
        foreach (var row in Map["manifestRows"]!.AsArray())
        {
            var family = S(row!, "family"); var index = I(row!, "index");
            if (!locators.TryGetValue((family, index), out var locator) || locator.File != "E_Block_5_Units_Ids.txt" || locator.Line <= 0)
            { issues.Add("manifest.locator_binding:" + index); continue; }
            if (normalized[family] is not JsonArray array || index >= array.Count || array[index] is not JsonObject actual ||
                actual["file"]?.GetValue<string>() != locator.File || actual["line"]?.GetValue<int>() != locator.Line)
            { issues.Add("manifest.locator_or_semantics:" + index); continue; }
            // Invert ONLY the two allowed file/line fields. Reasons, row order, all other
            // members/rows/families and unexpected extras remain in the full comparison.
            actual["file"] = row!["before"]!["file"]!.DeepClone();
            actual["line"] = row["before"]!["line"]!.DeepClone();
        }
        if (!JsonNode.DeepEquals(baseline, normalized)) issues.Add("manifest.complete_semantics_or_order");
        return issues;
    }
}
