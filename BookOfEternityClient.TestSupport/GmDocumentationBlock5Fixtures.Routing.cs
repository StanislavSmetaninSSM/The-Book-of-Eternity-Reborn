using System.Text.RegularExpressions;

namespace BookOfEternityClient.Tests;

internal sealed partial class GmDocumentationBlock5Fixtures
{
    internal static readonly string[] RouteIds = ["Units_Ids", "Characteristics_Progression", "Combat_Foundations", "Items_Resources", "Narrative_Social", "Wounds", "World_Chronology", "bootstrap", "receipt_only", "repair", "union_combat_wound", "union_item_social"];
    internal static string[] ExpectedTopics(string id) => id switch
    {
        "bootstrap" or "receipt_only" => [], "repair" => ["Items_Resources"],
        "union_combat_wound" => ["Units_Ids", "Combat_Foundations", "Wounds"],
        "union_item_social" => ["Units_Ids", "Items_Resources", "Narrative_Social"],
        "Units_Ids" or "Characteristics_Progression" or "Combat_Foundations" or "Items_Resources" or "Narrative_Social" or "Wounds" or "World_Chronology" => [id],
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };
    private static string Links(IEnumerable<string> topics) => string.Join(' ', topics.SelectMany(topic => new[]
        { "[rules](Rules/Block_5_" + topic + ".txt)", "[examples](Examples/E_Block_5_" + topic + ".txt)" }));
    private static string SpecialRouteRows =>
        "| b5_bootstrap | Existing bootstrap manifest/README restriction; no ordinary topic body | none | none |\n" +
        "| b5_receipt_only | Current receipt-only packet; no ordinary topics | none | none |\n" +
        "| b5_repair | Current repair packet explicitly names Items_Resources only; no implicit dependency broadening | " + Links(["Items_Resources"]) + " | packet-only |\n" +
        "| b5_union_combat_wound | Current combat percentage-point action AND independent issued wound opportunity; no characteristic calculation | " + Links(["Units_Ids", "Combat_Foundations", "Wounds"]) + " | union |\n" +
        "| b5_union_item_social | Current accepted item identity transfer AND NPC reaction; no action check or combat | " + Links(["Units_Ids", "Items_Resources", "Narrative_Social"]) + " | union |\n";
    internal static IReadOnlyList<string> VerifyRouter(string document)
    {
        var issues = new List<string>();
        if (Regex.IsMatch(document, @"\b(?:Read|Open)\s+(?:all|every)\s+(?:seven|Block_5|Block5|topic bodies)", RegexOptions.IgnoreCase)) issues.Add("route.universal");
        foreach (var id in RouteIds)
        {
            var rows = document.Split('\n').Where(line => line.StartsWith("| b5_" + id + " |", StringComparison.Ordinal)).ToArray();
            if (rows.Length != 1) { issues.Add("route.row:" + id); continue; }
            var actual = Regex.Matches(rows[0], @"\[[^\]]*\]\(([^)]+)\)").Select(match => match.Groups[1].Value).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var expected = ExpectedTopics(id).SelectMany(topic => new[] { "Rules/Block_5_" + topic + ".txt", "Examples/E_Block_5_" + topic + ".txt" }).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (!actual.SequenceEqual(expected)) issues.Add((id is "bootstrap" or "receipt_only" or "repair" ? "route.restricted:" : "route.edges:") + id);
        }
        return issues;
    }
    internal IReadOnlyList<string> VerifyHeaders(IReadOnlyDictionary<string, byte[]> docs)
    {
        var issues = new List<string>();
        foreach (var topic in Topics)
        foreach (var kind in new[] { "rulePath", "examplePath" })
        {
            var path = S(topic, kind);
            if (!docs.TryGetValue(path, out var bytes)) { issues.Add("header.missing:" + path); continue; }
            var text = Text(bytes); var boundary = text.IndexOf("-->\n", StringComparison.Ordinal);
            if (boundary < 0) { issues.Add("header.boundary:" + path); continue; }
            var header = text[..boundary];
            foreach (var marker in new[] { "Read when: " + S(topic, "activation"), "Prerequisites:", "Author here:", "Never author:", "Worked examples:", Path.GetFileName(S(topic, "examplePath")) })
                if (!header.Contains(marker, StringComparison.Ordinal)) issues.Add("header.context:" + path + ":" + marker);
        }
        return issues;
    }
    internal Dictionary<string, byte[]> CompleteClosureFixture()
    {
        // Real source bodies seed the memory-only link verifier; this is never publication proof.
        var docs = GmDocumentationDeliveryScenario.Expected.ToDictionary(doc => doc.Path,
            doc => File.ReadAllBytes(Path.Combine(_repo, doc.Path)), StringComparer.Ordinal);
        docs["CLI_Rules_Index.md"] = Bytes(Text(docs["CLI_Rules_Index.md"]) + RouterAddition);
        foreach (var pair in CompleteFixture()) docs[pair.Key] = pair.Value;
        return docs;
    }
}
