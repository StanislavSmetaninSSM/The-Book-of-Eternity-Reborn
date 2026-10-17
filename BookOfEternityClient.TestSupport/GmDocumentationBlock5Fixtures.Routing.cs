using System.Text.RegularExpressions;

namespace BookOfEternityClient.Tests;

internal sealed partial class GmDocumentationBlock5Fixtures
{
    internal const string MainRouter = "CLI_Rules_Index.md";
    internal const string WoundContract = "OtherGuides/Wound_Materialization_Contract.md";
    internal const string EffectContract = "OtherGuides/Effect_Materialization_Contract.md";
    internal static readonly string[] RequiredProfilePrerequisites = [MainRouter, WoundContract, EffectContract];
    internal static readonly string[] RouteIds = ["Units_Ids", "Characteristics_Progression", "Combat_Foundations", "Items_Resources", "Narrative_Social", "Wounds", "World_Chronology", "bootstrap", "receipt_only", "repair", "union_combat_wound", "union_item_social"];
    internal sealed record Dependency(string Condition, string[] Topics, string[] Documents);
    // Approved contract's conditional table, not a runtime context selector. Link availability
    // is not an instruction to activate these dependencies outside the stated condition.
    internal static Dependency[] Dependencies(string topic) => topic switch
    {
        "Units_Ids" => [],
        "Characteristics_Progression" => [new("actual calculation uses units or identities", ["Units_Ids"], [])],
        "Combat_Foundations" =>
        [
            new("actual combat work uses units or IDs", ["Units_Ids"], []),
            new("actual characteristic or derived-parameter calculation is required", ["Characteristics_Progression"], []),
            new("actual cost or equipment use is required", ["Items_Resources"], []),
            new("an independently issued wound contract is present", ["Wounds"], [])
        ],
        "Items_Resources" =>
        [
            new("actual accepted item identities or quantities are required", ["Units_Ids"], []),
            new("actual characteristic bonuses are required", ["Characteristics_Progression"], []),
            new("an actual combat mechanic is required", ["Combat_Foundations"], [])
        ],
        "Narrative_Social" =>
        [
            new("an actual characteristic check is required", ["Characteristics_Progression"], []),
            new("actual NPC creation is required", ["Units_Ids"], []),
            new("actual combat is required", ["Combat_Foundations"], [])
        ],
        "Wounds" => [new("actual effect definitions are required", [], [EffectContract])],
        "World_Chronology" => [new("an actual associated decision or check is required", ["Narrative_Social"], [])],
        _ => throw new ArgumentOutOfRangeException(nameof(topic))
    };
    internal static string[] ExpectedTopics(string id) => id switch
    {
        "bootstrap" or "receipt_only" => [], "repair" => ["Items_Resources"],
        "union_combat_wound" => ["Units_Ids", "Combat_Foundations", "Wounds"],
        "union_item_social" => ["Units_Ids", "Items_Resources", "Narrative_Social"],
        "Units_Ids" or "Characteristics_Progression" or "Combat_Foundations" or "Items_Resources" or "Narrative_Social" or "Wounds" or "World_Chronology" => [id],
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };
    private static string Relative(string from, string target) => Path.GetRelativePath(
        string.IsNullOrEmpty(Path.GetDirectoryName(from)) ? "." : Path.GetDirectoryName(from)!, target).Replace('\\', '/');
    private static string Link(string from, string target, string label) => "[" + label + "](" + Relative(from, target) + ")";
    private static string Links(IEnumerable<string> topics, string from = MainRouter) => string.Join(' ', topics.SelectMany(topic => new[]
        { Link(from, "Rules/Block_5_" + topic + ".txt", "rules"), Link(from, "Examples/E_Block_5_" + topic + ".txt", "examples") }));
    private static string DependencyText(string topic, string from)
    {
        var clauses = Dependencies(topic).Select(dependency => "Only if " + dependency.Condition + ": " +
            string.Join(' ', new[] { Links(dependency.Topics, from) }.Concat(dependency.Documents.Select(path => Link(from, path, "effect contract"))).Where(text => text.Length > 0)));
        return "Conditional dependencies (never widen a restricted packet): " +
            (Dependencies(topic).Length == 0 ? "none beyond the applicable current contract" : string.Join("; ", clauses)) + ".";
    }
    private static string MandatoryText(string topic, string from) => topic == "Wounds"
        ? "Mandatory for this issued Wounds contract: " + Link(from, WoundContract, "wound contract") + "."
        : "Mandatory prerequisites: only the inherited context and the current issued contract.";
    private string OrdinaryRouterRow(System.Text.Json.Nodes.JsonNode topic)
    {
        var id = S(topic, "topic");
        return "| b5_" + id + " | " + S(topic, "activation") + " | " + Links([id]) + " | " +
            MandatoryText(id, MainRouter) + " " + DependencyText(id, MainRouter) + " |";
    }
    private static string SpecialRouteRows =>
        "| b5_bootstrap | Existing bootstrap manifest/README restriction; no ordinary topic body | none | none |\n" +
        "| b5_receipt_only | Current receipt-only packet; no ordinary topics | none | none |\n" +
        "| b5_repair | Current repair packet explicitly names Items_Resources only; no implicit dependency broadening | " + Links(["Items_Resources"]) + " | packet-only |\n" +
        "| b5_union_combat_wound | Current combat percentage-point action AND independent issued wound opportunity; no characteristic calculation | " + Links(["Units_Ids", "Combat_Foundations", "Wounds"]) + " | union |\n" +
        "| b5_union_item_social | Current accepted item identity transfer AND NPC reaction; no action check or combat | " + Links(["Units_Ids", "Items_Resources", "Narrative_Social"]) + " | union |\n";
    internal IReadOnlyList<string> VerifyRouter(string document)
    {
        var issues = new List<string>();
        if (Regex.IsMatch(document, @"\b(?:Read|Open)\s+(?:all|every)\s+(?:seven|Block_5|Block5|topic bodies)", RegexOptions.IgnoreCase)) issues.Add("route.universal");
        foreach (var id in RouteIds)
        {
            var rows = document.Split('\n').Where(line => line.StartsWith("| b5_" + id + " |", StringComparison.Ordinal)).ToArray();
            if (rows.Length != 1) { issues.Add("route.row:" + id); continue; }
            var topic = Topics.SingleOrDefault(row => S(row, "topic") == id);
            if (topic is not null)
            {
                if (rows[0] != OrdinaryRouterRow(topic)) issues.Add("route.conditions:" + id);
                continue;
            }
            // Restricted packets and illustrated unions never acquire all potential ordinary dependencies.
            var actual = Regex.Matches(rows[0], @"\[[^\]]*\]\(([^)]+)\)").Select(match => match.Groups[1].Value).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var expected = ExpectedTopics(id).SelectMany(value => new[] { "Rules/Block_5_" + value + ".txt", "Examples/E_Block_5_" + value + ".txt" }).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (!actual.SequenceEqual(expected)) issues.Add((id is "bootstrap" or "receipt_only" or "repair" ? "route.restricted:" : "route.edges:") + id);
            var expectedRow = SpecialRouteRows.Split('\n').Single(line => line.StartsWith("| b5_" + id + " |", StringComparison.Ordinal));
            if (rows[0] != expectedRow) issues.Add((id is "bootstrap" or "receipt_only" or "repair" ? "route.restricted:" : "route.edges:") + id);
        }
        return issues;
    }
    internal IReadOnlyList<string> VerifyHeaders(IReadOnlyDictionary<string, byte[]> docs)
    {
        var issues = new List<string>();
        foreach (var topic in Topics)
        foreach (var kind in new[] { "rulePath", "examplePath" })
        {
            var path = S(topic, kind); var id = S(topic, "topic");
            if (!docs.TryGetValue(path, out var bytes)) { issues.Add("header.missing:" + path); continue; }
            var text = Text(bytes); var boundary = text.IndexOf("-->\n", StringComparison.Ordinal);
            if (boundary < 0) { issues.Add("header.boundary:" + path); continue; }
            var header = text[..boundary];
            foreach (var marker in new[] { "Read when: " + S(topic, "activation"), "Prerequisites:", "Author here:", "Never author:", "Worked examples:", Path.GetFileName(S(topic, "examplePath")) })
                if (!header.Contains(marker, StringComparison.Ordinal)) issues.Add("header.context:" + path + ":" + marker);
            if (!header.Contains("Main router: " + Link(path, MainRouter, "CLI rules index") + ".", StringComparison.Ordinal)) issues.Add("header.router:" + path);
            if (!header.Contains(MandatoryText(id, path), StringComparison.Ordinal)) issues.Add("header.prerequisite:" + path);
            if (!header.Contains(DependencyText(id, path), StringComparison.Ordinal)) issues.Add("header.dependencies:" + path);
            if (!text.StartsWith(Header(topic, kind == "rulePath"), StringComparison.Ordinal)) issues.Add("header.contract:" + path);
        }
        return issues;
    }
    internal Dictionary<string, byte[]> CompleteClosureFixture()
    {
        // Real source bodies seed the memory-only link verifier; this is never publication proof.
        var docs = GmDocumentationDeliveryScenario.Expected.ToDictionary(doc => doc.Path,
            doc => File.ReadAllBytes(Path.Combine(_repo, doc.Path)), StringComparer.Ordinal);
        docs[MainRouter] = Bytes(Text(docs[MainRouter]) + RouterAddition);
        foreach (var pair in CompleteFixture()) docs[pair.Key] = pair.Value;
        return docs;
    }
}
