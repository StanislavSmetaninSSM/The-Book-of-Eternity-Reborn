using System.Text.RegularExpressions;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundPlayerStatusDocumentationTests
{
    [Fact]
    public void SharedManifestModelsExistingQteCoverageAndStillRejectsUnknownProperties()
    {
        var manifest = ExampleValidationManifest.Load();
        Assert.Equal("mortal_qte_physical_opportunity_projection_v1", manifest.QtePhysicalOpportunityProjectionCoverage.ContractId);
        Assert.Equal("input/turn_request.json.woundOpportunities", manifest.QtePhysicalOpportunityProjectionCoverage.InputSurface);
        Assert.Equal("mortal_qte_physical_decision_draft_v1", manifest.QtePhysicalDecisionDraftCoverage.ContractId);
        Assert.Equal("output/mortal_wound_decisions.json", manifest.QtePhysicalDecisionDraftCoverage.OutputSurface);
        Assert.Equal(new[] { "mortal_qte_physical_none_decision_v1", "mortal_qte_physical_materialize_decision_v1",
            "mortal_qte_physical_materialize_scene_v1" }, manifest.QtePhysicalDecisionDraftCoverage.Examples);
        foreach (var raw in new[]
        {
            "{\"version\":1,\"unexpectedCoverage\":{}}",
            "{\"version\":1,\"qtePhysicalOpportunityProjectionCoverage\":{\"unexpected\":true}}",
            "{\"version\":1,\"qtePhysicalDecisionDraftCoverage\":{\"unexpected\":true}}"
        })
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ExampleValidationManifest>(raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }));
    }

    [Fact]
    public void CurrentReadonlyStatusIsSynchronizedForGm()
    {
        const string marker = "wound_player_status_readonly_v1";
        var files = new[]
        {
            "Rules/Block_CLI_Operations.txt", "CLI_API_Specification.md",
            "OtherGuides/Wound_Materialization_Contract.md", "OtherGuides/Afterlife_Contract_Matrix.md",
            "Examples/E_CLI_Wound_Materialization.txt", "Examples/E_CLI_Afterlife_Turns.txt"
        };
        foreach (var file in files)
        {
            var text = Regex.Replace(File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, file)), @"\s+", " ");
            foreach (var token in new[] { marker, "/статус", "/status", "client-owned read-only", "diagnosis reveal gate", "История ран" })
                Assert.True(text.Contains(token, StringComparison.Ordinal), file + " lacks " + token);
        }
        var manifest = ExampleValidationManifest.Load();
        var coverage = Assert.Single(manifest.EffectMaterializationCoverage, entry => entry.ContractId == marker);
        Assert.Equal("focused-fragment", coverage.ValidationKind);
        Assert.Equal("E_CLI_Wound_Materialization.txt", coverage.File);
        Assert.Equal(new[] { "Mortal World", "Chaos Sea", "Shining Abode" }, coverage.Realms);
        Assert.Contains("WoundPlayerStatusTests", coverage.FocusedFragmentReason);
        Assert.Contains("not a full GM response", coverage.CoverageLimit);
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", coverage.File));
        Assert.All(coverage.RequiredText, token => Assert.Contains(token, example));
        var api = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "CLI_API_Specification.md"));
        Assert.DoesNotContain("\"playerWoundChanges\"", api);
        Assert.DoesNotContain("\"NPCWoundChanges\"", api);
        Assert.Contains("transient strict `woundDecisions`", api);
        Assert.Contains("never direct GM output", api);
    }
}
