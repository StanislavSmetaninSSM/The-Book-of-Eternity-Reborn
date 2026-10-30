using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Existing menu DATA only; no signed turn, draw, cycle or publication claim.</summary>
public sealed class SpiritualWoundDiagnosisMenuTests
{
    private const string DiagnosisName = "Осмотреть духовную ткань";
    private const string ComplicationId = "hidden_spiritual_instability";

    [Theory]
    [InlineData("chaos_sea")]
    [InlineData("shining_abode")]
    public void CurrentPublicDiagnosisIsExecutableAtHealingZeroWhenItsRequirementIsPresent(string realm)
    {
        var node = WoundContractTestData.CreateSpiritualActiveWound(realm: realm,
            ownerId: "player_soul", carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath);
        node["complications"] = new JsonArray(new JsonObject
        {
            ["complicationId"] = ComplicationId, ["kind"] = "spiritual_instability", ["state"] = "active",
            ["displayName"] = "Скрытая неустойчивость духовной ткани", ["treatmentDifficultyModifier"] = 0,
            ["ownedEffectIds"] = new JsonArray(), ["visibility"] = "hidden"
        });
        node["treatment"]!["diagnosisPaths"] = new JsonArray(new JsonObject
        {
            ["diagnosisPathId"] = "inspect_spiritual_tissue", ["displayName"] = DiagnosisName,
            ["visibility"] = "public", ["requiresKnownFacts"] = new JsonArray(),
            ["requirements"] = new JsonArray(new JsonObject
            { ["kind"] = "source_capability", ["capabilityRef"] = "spiritual_healing", ["actorRole"] = "provider" }),
            ["check"] = new JsonObject(), ["reveals"] = new JsonArray("complication:" + ComplicationId),
            ["failurePolicy"] = "no_reveal"
        });
        var input = WoundPlayerProjectionTestData.InputInRealm(realm, node);
        var profile = Assert.IsType<JsonObject>(Assert.Single(input.Carriers.AfterlifeProfiles!["profiles"]!.AsArray()));
        profile["standardArts"] = new JsonObject { ["spiritual_healing"] = 0 };
        var soul = new JsonObject
        {
            ["currentRealm"] = realm == "chaos_sea" ? "Chaos Sea" : "Shining Abode",
            ["afterlifeCombatProfile"] = new JsonObject { ["artTiers"] = new JsonObject { ["spiritual_healing"] = 0 } }
        };
        var catalog = WoundCarrierCatalog.Build(input.Carriers);
        Assert.Empty(catalog.Issues);
        Assert.Empty(input.History.ValidateAgreement(input.Identities, catalog));
        var wound = Assert.Single(catalog.Occurrences).Wound;
        Assert.Equal("active", wound.Lifecycle); Assert.Equal(2, wound.Severity.Rank);
        Assert.Equal("hidden", Assert.Single(wound.Complications).Visibility);
        var path = Assert.Single(wound.Treatment.DiagnosisPaths);
        Assert.Equal("public", path.Visibility); Assert.Empty(path.RequiresKnownFacts);
        Assert.Equal(new[] { "complication:" + ComplicationId }, path.Reveals);
        var requirement = Assert.Single(path.Requirements);
        Assert.Equal("source_capability", requirement.GetProperty("kind").GetString());
        Assert.Equal("spiritual_healing", requirement.GetProperty("capabilityRef").GetString());
        Assert.Equal("provider", requirement.GetProperty("actorRole").GetString());
        var source = Assert.IsType<SpiritualHealingPreparedSource>(SpiritualHealingResolver.PrepareSource(
            new(wound, wound.Owner, profile, soul, input.Effects)).Source);
        Assert.Equal(0, source.HealingTier);
        var context = new WoundPlayerReadContext(input, catalog, soul, null, WoundPlayerProjection.Build(input, catalog));
        Assert.True(context.Projection.IsAvailable); Assert.Null(context.SpiritualConflict);
        Assert.Contains(Assert.Single(context.Projection.Active).Diagnosis, method => method.Name == DiagnosisName);
        string Snapshot() => input.Carriers.AfterlifeProfiles!.ToJsonString() + soul.ToJsonString()
            + WoundIdentityState.SerializeCanonical(input.Identities) + WoundHistoryState.SerializeCanonical(input.History);
        var before = Snapshot();
        var state = WoundApplicationService.CreateState("diagnosis_menu_generation", context);
        Assert.Equal(before, Snapshot());
        Assert.Contains(state.Choices, choice => !choice.Available && choice.Name == wound.Display.Name);

        // Intended causal RED: current production exposes this path only as a
        // read-only recommendation and supplies no executable diagnostic choice.
        Assert.Contains(state.Choices, choice => choice.Available && choice.Name == DiagnosisName);
    }
}
