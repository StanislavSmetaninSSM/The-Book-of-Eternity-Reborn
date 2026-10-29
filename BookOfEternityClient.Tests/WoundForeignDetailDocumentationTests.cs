using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>The worked authorable fragment projects known facts without granting discovery or a route action.</summary>
public sealed class WoundForeignDetailDocumentationTests
{
    /// <summary>Both effective GM entrypoints and the manifest lead to a production-validated read-only example.</summary>
    [Fact]
    public void WorkedKnownCardFragmentUsesCurrentKnowledgeAndSynchronizedGmGuidance()
    {
        const string marker = "wound_foreign_detail_v1";
        foreach (var file in new[] { "OtherGuides/Wound_Materialization_Contract.md", "OtherGuides/Afterlife_Contract_Matrix.md",
                     "CLI_API_Specification.md", "Rules/Block_CLI_Operations.txt", "TaskGuides/CLI_Step_Main.txt" })
            Assert.Contains(marker, File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, file)));
        var daemon = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient/game_master_daemon.ps1"));
        var assignments = Regex.Matches(daemon, "\\$script:WoundMaterializationDirective = \\\"[^\\\"]*\\\"");
        Assert.Equal(2, assignments.Count); Assert.All(assignments.Cast<Match>(), a => Assert.Contains(marker, a.Value));
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, e => e.ContractId == "spiritual_resident_help_v6");
        Assert.Contains(marker, entry.RequiredText);
        var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        var start = text.IndexOf(marker + " — worked", StringComparison.Ordinal); Assert.True(start >= 0);
        var match = Regex.Match(text[start..], "```json\\s*([\\s\\S]*?)```", RegexOptions.CultureInvariant); Assert.True(match.Success);
        var fragment = JsonNode.Parse(match.Groups[1].Value)!.AsObject();
        var data = WoundForeignDetailTestData.Create(adjust: wound =>
        { wound["display"] = fragment["display"]!.DeepClone(); wound["treatment"] = fragment["treatment"]!.DeepClone(); });
        var catalog = WoundCarrierCatalog.Build(data.Input.Carriers); Assert.Empty(catalog.Issues);
        var context = new WoundPlayerReadContext(data.Input, catalog, new JsonObject { ["currentRealm"] = "Chaos Sea" }, null,
            WoundPlayerProjection.Build(data.Input, catalog));
        var target = Assert.Single(WoundTargetSelectionService.Build(context, data.Guardians, data.Residents), t => t.Owner.OwnerKind == "guardian");
        var result = WoundPlayerProjection.ProjectKnownTarget(context, target); Assert.True(result.IsAvailable);
        var wound = Assert.Single(result.Active);
        Assert.Contains(wound.Facts, f => f.Value == "Заметная потеря устойчивости");
        Assert.Equal("Поддержать духовную ткань", Assert.Single(wound.Methods).Name);
        Assert.Contains("рекомендация", wound.Methods[0].Policy);
        Assert.Equal("Уточнить состояние духовной ткани", Assert.Single(wound.Diagnosis));
        var rendered = JsonSerializer.Serialize(result, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        foreach (var secret in new[] { "НЕРАСКРЫТЫЙ_РИТУАЛ", "ЧАСТНОЕ_УСЛОВИЕ", "undiscovered_rite", "known_support", "wound_foreign_detail" })
            Assert.DoesNotContain(secret, rendered);
        Assert.Empty(result.History);
    }
}
