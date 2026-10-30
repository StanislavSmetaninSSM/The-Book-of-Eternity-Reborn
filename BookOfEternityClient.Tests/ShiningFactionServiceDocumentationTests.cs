using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ShiningFactionServiceDocumentationTests
{
    private const string Marker = "shining_faction_service_v7";

    [Fact]
    public void EffectiveGuidanceAndBothDaemonDirectivesNameExactResidentServiceAndKeepPrivateAuthority()
    {
        foreach (var file in new[] { "OtherGuides/Wound_Materialization_Contract.md", "OtherGuides/Afterlife_Contract_Matrix.md",
            "CLI_API_Specification.md", "Rules/Block_CLI_Operations.txt", "Rules/Block_32_Guardians.txt", "TaskGuides/CLI_Step_Main.txt" })
        {
            var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, file));
            var start = text.IndexOf(Marker + ":", StringComparison.Ordinal); Assert.True(start >= 0);
            foreach (var token in new[] { "leaderless", "broken/dissolved", "H>=W", "ORIGINAL", "client_auto_strategy",
                "spiritualResidentThoughtJournalUpdates", "signed", "rollback", "unfinished", "progressionHistory", "historical materialization envelope remains immutable" })
                Assert.Contains(token, text[start..]);
        }
        XDocument.Parse(File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Rules/Block_32_Guardians.txt")));
        var daemon = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient/game_master_daemon.ps1"));
        var directives = Regex.Matches(daemon, "\\$script:WoundMaterializationDirective = \\\"[^\\\"]*\\\"");
        Assert.Equal(2, directives.Count); Assert.All(directives.Cast<Match>(), d =>
        {
            Assert.Contains(Marker, d.Value);
            Assert.Contains("historical materialization envelope remains immutable", d.Value);
        });
        var registry = AfterlifeContractRegistry.Find(AcceptedMechanicsPlan.WoundCommandPath)!;
        Assert.True(registry.IsKnownClientOwnedSurface); Assert.True(registry.IsPrivateMechanicsSurface);
        Assert.Contains("Shining faction resident service-v7", registry.Authority);
    }

    [Fact]
    public void WorkedFactionServiceUsesActualProfileResponseAppendAndOutcomeContracts()
    {
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, e => e.ContractId == Marker);
        var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        var start = text.IndexOf(Marker + " — worked", StringComparison.Ordinal); Assert.True(start >= 0);
        var end = text.IndexOf("end_" + Marker, start, StringComparison.Ordinal); Assert.True(end > start);
        var fragment = text[start..end]; Assert.All(entry.RequiredText, token => Assert.Contains(token, fragment));
        var roots = Regex.Matches(fragment, "```json\\s*([\\s\\S]*?)```", RegexOptions.CultureInvariant)
            .Cast<Match>().Select(m => JsonNode.Parse(m.Groups[1].Value)!.AsObject()).ToArray();
        Assert.Equal(2, roots.Length);
        var actor = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile("resident", "resident_public_healer_0", "Shining Abode", 42);
        actor["lifeState"] = "alive"; actor["visibility"] = "public"; actor["locationId"] = "hall_elyara_companion";
        actor["standardArts"]!["spiritual_healing"] = 3; actor["healingServiceProfile"] = roots[0].DeepClone();
        var provider = new WoundOwnerCoordinate("shining_abode", "resident", "resident_public_healer_0", WoundCarrierCatalog.AfterlifeProfilesPath);
        var profile = Assert.IsType<AfterlifeHealingServiceProfile>(AfterlifeHealingServiceContract.ReadPublicCurrencyProfile(provider, actor));
        Assert.Equal(3, profile.HealingTier); Assert.Equal(101, profile.MultiplierPercent);
        var response = JsonSerializer.Deserialize<GameResponse>(roots[1].ToJsonString())!;
        Assert.NotNull(response.SpiritualHealingGraphs); Assert.Empty(response.SpiritualHealingGraphs!);
        Assert.Single(response.SpiritualResidentThoughtJournalUpdates!);
        var rows = roots[1][SpiritualTreatmentParticipation.ResidentThoughtResponseProperty]!.AsArray();
        Assert.Single(SpiritualTreatmentParticipation.ValidateResidentThoughtAppend(rows, provider.OwnerId, 43,
            new JsonObject { [GuardianAbodeResidentState.ThoughtJournalProperty] = new JsonArray() }));
        Assert.True(SpiritualHealingOutcomeMath.Calculate(new(3, 2, 20, 0, 0))!.HealsWound);
        Assert.Equal(SpiritualHealingResultBand.NoImprovement, SpiritualHealingOutcomeMath.Calculate(new(3, 2, 1, 0, 0))!.ResultBand);
        Assert.Null(response.MetaStateUpdates);
    }
}
