using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class GmDocumentationBlock5Tests
{
    private readonly ITestOutputHelper _output;
    public GmDocumentationBlock5Tests(ITestOutputHelper output) => _output = output;
    private static GmDocumentationBlock5Fixtures Fixture() => new(TestRepoPaths.RepoRoot);
    private static string Read(string path) => File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path));
    private static string S(JsonNode row, string key) => GmDocumentationBlock5Fixtures.S(row, key);
    private static int I(JsonNode row, string key) => GmDocumentationBlock5Fixtures.I(row, key);
    private static byte[] Bytes(string text) => GmDocumentationBlock5Fixtures.Bytes(text);
    private static string Text(byte[] bytes) => GmDocumentationBlock5Fixtures.Text(bytes);
    private static string Marked(GmDocumentationBlock5Fixtures f, JsonNode row) => GmDocumentationBlock5Fixtures.Start(row) + Text(f.Original(row)) + GmDocumentationBlock5Fixtures.End(row);

    [Fact]
    public void CurrentBaselineRetainsEveryByteXmlAncestorAndCompleteExample()
    {
        var f = Fixture(); Assert.Empty(f.VerifyBaseline());
        Assert.Equal(55, f.Pieces.Count()); Assert.Equal(50, f.Groups.Count());
        Assert.Equal(144, f.Sources.First()["ruleNodes"]!.GetValue<int>());
        Assert.Equal(51, f.Sources.Last()["examples"]!.GetValue<int>());
        Assert.Empty(f.VerifyMoved(f.CompleteFixture())); // Memory fixture, never an actual move claim.
    }

    [Fact]
    public void ActualOwnedBodiesReachTheMissingModuleOracleAfterFullBaselineContext()
    {
        var f = Fixture(); Assert.Empty(f.VerifyBaseline()); Assert.Empty(f.VerifyMoved(f.CompleteFixture()));
        Assert.Equal(9, f.Map["actualExistingExtractor"]!["rows"]!.AsArray().Count);
        Assert.Equal(2, f.Map["manifestRows"]!.AsArray().Count);
        var docs = f.ActualDocuments();
        _output.WriteLine(JsonSerializer.Serialize(new { Kind = "BLOCK5_REAL_FILESYSTEM_AFTER_COMPLETE_BASELINE_BEFORE_MOVE_ORACLE", Sources = f.Sources.Select(s => new { Path = S(s, "path"), Bytes = I(s, "bytes"), Hash = S(s, "sha256"), Rules = I(s, "ruleNodes"), Examples = I(s, "examples") }), Missing = f.AllPaths.Except(docs.Keys).ToArray(), ActualPaths = docs.Keys.Order().ToArray(), MutableRoots = "N/A readonly source" }));
        Assert.Empty(f.VerifyMoved(docs));
    }

    [Fact]
    public void ActualFlatExtractorInputsMapToAllNineOriginalSourceOccurrences()
    {
        var f = Fixture(); Assert.Empty(f.VerifyBaseline());
        Assert.Empty(f.VerifyActualExtractedInputs());
        // Original source ordinals bind equality; physical filename sorting is intentionally different.
    }

    [Fact]
    public void ActualManifestChangesOnlyTheTwoExactBlock5ExemptionLocators()
    {
        var f = Fixture(); Assert.Empty(f.VerifyBaseline());
        Assert.Empty(f.VerifyActualManifest(JsonNode.Parse(Read("Examples/example_validation_manifest.json"))!.AsObject()));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("cdata")]
    [InlineData("reorder_examples")]
    [InlineData("classification")]
    [InlineData("component_owner")]
    [InlineData("foreign_ancestor")]
    [InlineData("context")]
    [InlineData("supplement")]
    [InlineData("nested_file")]
    public void IndependentFullXmlMutationsReachTheirSpecificPreservationOracles(string mutation)
    {
        var f = Fixture(); Assert.Empty(f.VerifyBaseline()); var docs = f.CompleteFixture(); Assert.Empty(f.VerifyMoved(docs));
        var group = f.Groups.First(); var path = S(group, "destination"); var expected = "payload.missing:";
        if (mutation == "missing") docs[path] = Bytes(Text(docs[path]).Replace(Marked(f, group), "", StringComparison.Ordinal));
        else if (mutation == "duplicate") { docs[path] = Bytes(Text(docs[path]).Replace(Marked(f, group), Marked(f, group) + Marked(f, group), StringComparison.Ordinal)); expected = "payload.duplicate:"; }
        else if (mutation == "cdata") { docs[path] = Bytes(Text(docs[path]).Replace("152 - 45 = 107", "152 - 45 = 108", StringComparison.Ordinal)); expected = "payload.unlisted:"; }
        else if (mutation == "reorder_examples")
        {
            var nodes = f.Sources.Last()["orderedExampleNodes"]!.AsArray().Where(row => S(row!, "topic") == "Narrative_Social").Take(2).ToArray();
            path = "Examples/E_Block_5_Narrative_Social.txt"; var baseline = f.Baselines["Examples/E_Block_5.txt"];
            var a = Text(baseline[I(nodes[0]!, "start")..I(nodes[0]!, "end")]); var b = Text(baseline[I(nodes[1]!, "start")..I(nodes[1]!, "end")]);
            var text = Text(docs[path]); var first = text.IndexOf(a, StringComparison.Ordinal); var second = text.IndexOf(b, StringComparison.Ordinal); Assert.True(first >= 0 && second > first);
            docs[path] = Bytes(text[..first] + b + text[(first + a.Length)..second] + a + text[(second + b.Length)..]); expected = "xml.example_order:";
        }
        else if (mutation == "classification")
        {
            path = "Examples/E_Block_5_Units_Ids.txt"; var text = Text(docs[path]); var index = text.IndexOf("type=\"good\"", StringComparison.Ordinal); Assert.True(index >= 0);
            docs[path] = Bytes(text[..index] + "type=\"bad\"" + text[(index + "type=\"good\"".Length)..]); expected = "xml.example_class:";
        }
        else if (mutation == "component_owner")
        {
            group = f.Groups.Single(row => S(row, "sourcePath") == "Rules/Block_5.txt" && S(row, "rootRuleId") == "5.10"); path = S(group, "destination");
            docs[path] = Bytes(Text(docs[path]).Replace(Marked(f, group), "", StringComparison.Ordinal));
            var wrong = "Rules/Block_5_Units_Ids.txt"; var text = Text(docs[wrong]); var index = text.LastIndexOf("</content>", StringComparison.Ordinal); Assert.True(index >= 0);
            docs[wrong] = Bytes(text[..index] + Marked(f, group) + text[index..]); expected = "xml.owner:";
        }
        else if (mutation == "foreign_ancestor")
        {
            path = "Rules/Block_5_Narrative_Social.txt"; var text = Text(docs[path]); var start = text.IndexOf("<rule id=\"19.D.0\">", StringComparison.Ordinal); var end = text.IndexOf("</rule>", start, StringComparison.Ordinal) + "</rule>".Length; Assert.True(start >= 0 && end > start);
            var node = text[start..end]; text = text.Remove(start, end - start); var index = text.LastIndexOf("</content>", StringComparison.Ordinal);
            docs[path] = Bytes(text[..index] + node + text[index..]); expected = "xml.ancestor:";
        }
        else if (mutation == "context") { docs[path] = Bytes(Text(docs[path]).Replace("<instructionblock id=\"5\">", "<instructionblock id=\"6\">", StringComparison.Ordinal)); expected = "xml.context:"; }
        else if (mutation == "supplement")
        {
            group = f.Groups.Single(row => S(row, "rootRuleId") == "5.mortal_item_materialization_v1"); path = S(group, "destination");
            docs[path] = Bytes(Text(docs[path]).Replace(Marked(f, group), "", StringComparison.Ordinal)); expected = "xml.rule_order_or_missing:";
        }
        else
        {
            path = "Examples/E_Block_5_Units_Ids.txt"; docs.Add("Examples/nested/E_Block_5_Units_Ids.txt", docs[path]); docs.Remove(path); expected = "layout.not_flat:";
        }
        Assert.Contains(f.VerifyMoved(docs), issue => issue.StartsWith(expected, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Units_Ids")]
    [InlineData("Characteristics_Progression")]
    [InlineData("Combat_Foundations")]
    [InlineData("Items_Resources")]
    [InlineData("Narrative_Social")]
    [InlineData("Wounds")]
    [InlineData("World_Chronology")]
    [InlineData("bootstrap")]
    [InlineData("receipt_only")]
    [InlineData("repair")]
    [InlineData("union_combat_wound")]
    [InlineData("union_item_social")]
    public void ActualConditionalRowsPreserveTopicUnionAndRestrictedScope(string route)
    {
        var f = Fixture(); var router = Read("CLI_Rules_Index.md");
        Assert.Contains("| b5_" + route + " |", router, StringComparison.Ordinal);
        Assert.Empty(GmDocumentationBlock5Fixtures.VerifyRouter(router));
        Assert.Empty(f.VerifyHeaders(f.ActualDocuments()));
    }

    [Theory]
    [InlineData("universal")]
    [InlineData("lost_union")]
    [InlineData("repair_expansion")]
    public void RouteMutationsCannotBroadenScopeOrDropTheSecondContract(string mutation)
    {
        var f = Fixture(); var router = f.RouterAddition; Assert.Empty(GmDocumentationBlock5Fixtures.VerifyRouter(router));
        string expected;
        if (mutation == "universal") { router += "\nRead all seven topic bodies every turn.\n"; expected = "route.universal"; }
        else
        {
            var id = mutation == "lost_union" ? "union_combat_wound" : "repair";
            var line = router.Split('\n').Single(value => value.StartsWith("| b5_" + id + " |", StringComparison.Ordinal));
            var changed = mutation == "lost_union" ? line.Replace("[rules](Rules/Block_5_Wounds.txt)", "", StringComparison.Ordinal) : line + " [extra](Rules/Block_5_Combat_Foundations.txt)";
            router = router.Replace(line, changed, StringComparison.Ordinal); expected = mutation == "lost_union" ? "route.edges:" : "route.restricted:";
        }
        Assert.Contains(GmDocumentationBlock5Fixtures.VerifyRouter(router), issue => issue.StartsWith(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void CurrentRealPublicationInventoryIsIndependentExact52WhileASeedStays36()
    {
        var f = Fixture(); var current = GmDocumentationDeliveryScenario.CurrentExpected;
        Assert.Equal(36, GmDocumentationDeliveryScenario.Expected.Length); Assert.Equal(52, current.Length);
        Assert.Equal(52, current.Select(row => row.Path).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(f.AllPaths.Order(StringComparer.Ordinal), GmDocumentationDeliveryScenario.Block5Expected.Select(row => row.Path).Order(StringComparer.Ordinal));
        Assert.All(GmDocumentationDeliveryScenario.Block5Expected, row => Assert.StartsWith("block5_", row.Role, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("wrong_relative")]
    [InlineData("escape")]
    public void NewPhysicalRelativeLinkRequirementsRefuseWrongExistingTargetsAndEscape(string mutation)
    {
        var f = Fixture(); var docs = f.CompleteClosureFixture(); Assert.Empty(GmDocumentationDeliveryRules.VerifyClosure(docs, requiredDocs: GmDocumentationDeliveryScenario.CurrentExpected));
        const string path = "Rules/Block_5_Wounds.txt"; var target = mutation == "escape" ? "../../outside.md" : "Examples/E_Block_5_Wounds.txt";
        Assert.True(docs.ContainsKey("Examples/E_Block_5_Wounds.txt"));
        docs[path] = Bytes(Text(docs[path]).Replace("../Examples/E_Block_5_Wounds.txt", target, StringComparison.Ordinal));
        var expected = mutation == "escape" ? "delivery.link_escape:" : "delivery.link_missing:";
        Assert.Contains(GmDocumentationDeliveryRules.VerifyClosure(docs, requiredDocs: GmDocumentationDeliveryScenario.CurrentExpected), issue => issue.StartsWith(expected, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("narrative_draft_codex", "NarrativeDraft")]
    [InlineData("analysis_codex", "Analysis")]
    [InlineData("inventory_content_codex", "InventoryContent")]
    [InlineData("skill_content_codex", "SkillContent")]
    [InlineData("npc_content_codex", "NpcContent")]
    public void FiveRealProfilesAllowPairedBReadingWithoutWriteOrTaskExpansion(string worker, string task)
    {
        var profile = Assert.Single(GmWorkerBridgeProfileTemplates.CreateDefaultTemplates(), row => row.WorkerId == worker);
        var validation = GmWorkerContractValidator.ValidateProfile(profile); Assert.True(validation.IsValid, string.Join(';', validation.Errors));
        Assert.False(profile.Enabled); Assert.True(profile.Permissions.ProposalOnly); Assert.False(profile.Permissions.RequiresValidation);
        Assert.Empty(profile.Permissions.ProposalWritePaths); Assert.Equal(task, Assert.Single(profile.Permissions.TaskTypes).ToString());
        foreach (var path in Fixture().AllPaths)
            Assert.True(profile.Permissions.ReadPaths.Any(pattern => GmWorkerContractValidator.PathMatches(pattern, path)), worker + ":" + path);
        Assert.False(profile.Permissions.ReadPaths.Any(pattern => GmWorkerContractValidator.PathMatches(pattern, "Examples/unrelated-new-authority.txt")));
    }
}
