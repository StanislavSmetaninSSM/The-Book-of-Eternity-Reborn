using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeHealingServiceDocumentationTests
{
    [Fact]
    public void ServiceGuidanceAndBothEffectiveDaemonAssignmentsUseTheCurrentOwnedRoute()
    {
        const string marker = "spiritual_public_service_v7";
        foreach (var file in new[] { "OtherGuides/Wound_Materialization_Contract.md", "OtherGuides/Afterlife_Contract_Matrix.md",
            "CLI_API_Specification.md", "Rules/Block_CLI_Operations.txt", "Rules/Block_32_Guardians.txt", "TaskGuides/CLI_Step_Main.txt",
            "BookOfEternityClient/system_guardians/built_in/elyara/dossier.md" })
            Assert.Contains(marker, File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, file)));
        var daemon = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient/game_master_daemon.ps1"));
        var directives = Regex.Matches(daemon, "\\$script:WoundMaterializationDirective = \\\"[^\\\"]*\\\"");
        Assert.Equal(2, directives.Count); Assert.All(directives.Cast<Match>(), d => Assert.Contains(marker, d.Value));
        var registry = AfterlifeContractRegistry.Find(AcceptedMechanicsPlan.WoundCommandPath)!;
        Assert.True(registry.IsKnownClientOwnedSurface); Assert.True(registry.IsPrivateMechanicsSurface);
        Assert.Contains("service-v7", registry.Authority);
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot,
            "BookOfEternityClient/system_guardians/built_in/elyara/manifest.json")))!["healingService"]!;
        Assert.Equal(5, manifest["spiritualHealingTier"]!.GetValue<int>());
        Assert.Equal(100, manifest["publicPriceMultiplierPercent"]!.GetValue<int>());
        Assert.Equal(AfterlifeWoundTreatmentAuthority.ElyaraAbodeId, manifest["initialLocationId"]!.GetValue<string>());
    }

    [Fact]
    public void WorkedPublicProfileAndRequestedEmptyGraphResponseUseProductionContracts()
    {
        const string marker = "spiritual_public_service_v7";
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, e => e.ContractId == marker);
        var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        Assert.All(entry.RequiredText, token => Assert.Contains(token, text));
        var start = text.IndexOf(marker + " — worked", StringComparison.Ordinal); Assert.True(start >= 0);
        var end = text.IndexOf("wound_target_navigation_v1 — worked", start, StringComparison.Ordinal); Assert.True(end > start);
        var fragments = Regex.Matches(text[start..end], "```json\\s*([\\s\\S]*?)```", RegexOptions.CultureInvariant)
            .Cast<Match>().Select(m => JsonNode.Parse(m.Groups[1].Value)!.AsObject()).ToArray();
        Assert.Equal(2, fragments.Length);
        AfterlifeHealingServiceContract.RequireElyaraInvariant(fragments[0]);
        Assert.NotNull(AfterlifeHealingServiceContract.ReadPublicCurrencyProfile(AfterlifeHealingServiceTestData.Provider(), fragments[0]));
        var response = JsonSerializer.Deserialize<GameResponse>(fragments[1].ToJsonString())!;
        Assert.NotNull(response.SpiritualHealingGraphs); Assert.Empty(response.SpiritualHealingGraphs!);
        Assert.True(SpiritualHealingOutcomeMath.Calculate(new(5, 1, 20, 0, 0))!.HealsWound);
        Assert.Equal(SpiritualHealingResultBand.NoImprovement, SpiritualHealingOutcomeMath.Calculate(new(5, 1, 1, 0, 0))!.ResultBand);
        Assert.Equal(SpiritualHealingResultBand.RecoveryPoint, SpiritualHealingOutcomeMath.Calculate(new(5, 3, 2, 0, 0))!.ResultBand);
    }
}
