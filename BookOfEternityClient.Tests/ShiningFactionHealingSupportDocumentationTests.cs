using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ShiningFactionHealingSupportDocumentationTests
{
    private const string Marker = "shining_healer_role_v1";
    private const string InvariantMarker = "shining_faction_healer_invariant_v1";

    [Fact]
    public void EffectiveGuidanceAndManifestDescribeTheExistingRoleWithoutAServiceGrant()
    {
        foreach (var file in new[] { "OtherGuides/Wound_Materialization_Contract.md", "OtherGuides/Afterlife_Contract_Matrix.md",
            "Rules/Block_32_Guardians.txt", "CLI_API_Specification.md", "TaskGuides/CLI_Step_Main.txt" })
        {
            var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, file));
            Assert.Contains(Marker, text); Assert.Contains("residentRole=\"healing_support\"", text);
            Assert.Contains(InvariantMarker, text); Assert.Contains("broken/dissolved", text);
        }
        Assert.Contains(InvariantMarker, File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Rules/Block_CLI_Operations.txt")));
        foreach (var file in new[] { "Examples/E_CLI_Afterlife_Turns.txt", "OtherGuides/Afterlife_Contract_Matrix.md" })
            Assert.Contains("`descent_support`, `healing_support`", File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, file)));
        var daemon = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient/game_master_daemon.ps1"));
        var directives = Regex.Matches(daemon, "\\$script:WoundMaterializationDirective = \\\"[^\\\"]*\\\"");
        Assert.Equal(2, directives.Count);
        Assert.All(directives.Cast<Match>(), d => Assert.Contains(Marker, d.Value));
        Assert.All(directives.Cast<Match>(), d => Assert.Contains(InvariantMarker, d.Value));
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, e => e.ContractId == Marker);
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        Assert.All(entry.RequiredText, token => Assert.Contains(token, example));
    }

    [Fact]
    public void WorkedRosterAndArtExcerptsUseTheProductionCapabilityWithoutReplacingOtherArts()
    {
        var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples/E_CLI_Afterlife_Turns.txt"));
        var start = text.IndexOf(Marker + " — worked", StringComparison.Ordinal); Assert.True(start >= 0);
        var end = text.IndexOf("end_shining_healer_role_v1", start, StringComparison.Ordinal); Assert.True(end > start);
        var fragments = Regex.Matches(text[start..end], "```json\\s*([\\s\\S]*?)```", RegexOptions.CultureInvariant)
            .Cast<Match>().Select(m => JsonNode.Parse(m.Groups[1].Value)!.AsObject()).ToArray();
        Assert.Equal(3, fragments.Length);
        var resident = fragments[0]; var excerpt = fragments[1];
        ShiningAbodeState.NormalizeResidentShiningFields(resident, shiningRoot: null);
        Assert.Equal("healing_support", resident["residentRole"]!.GetValue<string>());
        var profile = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile("resident", "resident_iva", "Shining Abode", 42);
        var guard = profile["standardArts"]!["guard"]!.GetValue<int>();
        foreach (var (key, value) in excerpt)
        {
            if (key == "standardArts")
                foreach (var (art, tier) in value!.AsObject()) profile["standardArts"]![art] = tier!.DeepClone();
            else profile[key] = value!.DeepClone();
        }
        Assert.True(ShiningFactionHealingSupport.TryReadHealerTier(resident, profile, out var healing));
        Assert.Equal(3, healing); Assert.Equal(guard, profile["standardArts"]!["guard"]!.GetValue<int>());
        Assert.False(profile.ContainsKey("healingServiceProfile"));
        var roster = ShiningFactionHealingSupport.ProjectVisibleRoster(
            new JsonObject { ["factionId"] = "faction_dawn" },
            new JsonObject { ["entries"] = new JsonArray(resident.DeepClone()) },
            new JsonObject { ["profiles"] = new JsonArray(profile) });
        var visible = Assert.Single(roster);
        Assert.Equal("Светлая Ива", visible.Name); Assert.Equal("целитель", visible.Role); Assert.Equal(3, visible.HealingTier);
        var historical = fragments[2];
        var history = historical["chronicle"]!.ToJsonString();
        Assert.Equal(45, historical["factionLifecycle"]!["defeatedAtTurn"]!.GetValue<int>());
        Assert.False(string.IsNullOrWhiteSpace(historical["factionLifecycle"]!["defeatReason"]!.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(historical["factionLifecycle"]!["remnantsSummary"]!.GetValue<string>()));
        Assert.Equal("vacant", historical["leadership"]!["leadershipState"]!.GetValue<string>());
        Assert.Null(historical["leadership"]!["headActorType"]); Assert.Null(historical["leadership"]!["headActorId"]);
        Assert.Equal(0, historical["factionStrength"]!.GetValue<int>()); Assert.Null(historical["tradeInventory"]);
        Assert.False(ShiningFactionHealingSupport.RequiresLivingHealer(historical));
        historical["factionLifecycle"]!["state"] = "dissolved";
        Assert.False(ShiningFactionHealingSupport.RequiresLivingHealer(historical));
        historical["factionLifecycle"]!["state"] = "active"; historical["visibility"] = "hidden";
        Assert.True(ShiningFactionHealingSupport.RequiresLivingHealer(historical));
        Assert.Equal(history, historical["chronicle"]!.ToJsonString());
    }
}
