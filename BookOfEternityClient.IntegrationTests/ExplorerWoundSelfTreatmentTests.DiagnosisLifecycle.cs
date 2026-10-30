using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    [Theory]
    [InlineData("resident", 21, 36)]
    [InlineData("radiant_actor", 33, 33)]
    public void DiagnosisLifecycleFaultRetainsOnlyItsExpectedDerivedStrengthAndIsAlreadyCanonical(
        string kind, int originalStrength, int expectedStrength)
    {
        // File-free normalization DATA, not an accepted world or an authority.
        var faction = FactionMaterializationValidationTests.BuildCompleteNativeShiningFaction();
        var factionId = faction["factionId"]!.GetValue<string>();
        var residents = new JsonObject();
        GuardianAbodeResidentState.NormalizeShape(residents);
        residents["entries"]!.AsArray().Add(FactionMaterializationValidationTests.BuildRouteResident("lifecycle_healer", factionId));
        if (kind == "resident")
        {
            residents["entries"]!.AsArray().Add(FactionMaterializationValidationTests.BuildRouteResident("lifecycle_patient", factionId));
            faction["factionLifecycle"]!["state"] = "leaderless";
            faction["leadership"]!["leadershipState"] = "vacant";
            faction["leadership"]!["headActorType"] = null; faction["leadership"]!["headActorId"] = null;
        }
        faction["materialization"]!["capabilities"]!["hasResidentAffiliations"] = true;
        faction["materialization"]!["sections"]!["residentAffiliations"] = new JsonObject { ["state"] = "populated" };
        var shining = ShiningAbodeState.CreateDefaultState();
        shining["factions"] = new JsonArray(faction);
        GuardianAbodeResidentState.NormalizeShape(residents);
        ShiningAbodeState.NormalizeStateRoot(shining, residents);
        Assert.Equal(kind == "resident" ? "leaderless" : "active", faction["factionLifecycle"]!["state"]!.GetValue<string>());
        Assert.Equal(30, faction["baseStrength"]!.GetValue<int>());
        Assert.Equal(0, faction["investCountThisAscension"]!.GetValue<int>());
        Assert.Empty(faction["projects"]!.AsArray());
        Assert.Equal(kind == "resident" ? 2 : 1, ShiningAbodeState.CountAscendedResidentsForFaction(residents, factionId));
        Assert.Equal(originalStrength, faction["factionStrength"]!.GetValue<int>());
        var before = shining.ToJsonString() + residents.ToJsonString();

        var stateOnly = shining.DeepClone().AsObject();
        stateOnly["factions"]![0]!["factionLifecycle"]!["state"] = "weakened";
        var changed = BuildCanonicalDiagnosisLifecycleDrift(shining, residents);
        Assert.Equal("weakened", changed["factions"]![0]!["factionLifecycle"]!["state"]!.GetValue<string>());
        Assert.Equal(expectedStrength, changed["factions"]![0]!["factionStrength"]!.GetValue<int>());
        Assert.Equal(kind == "radiant_actor", JsonNode.DeepEquals(stateOnly, changed));
        var normalized = changed.DeepClone().AsObject();
        ShiningAbodeState.NormalizeStateRoot(normalized, residents);
        Assert.True(JsonNode.DeepEquals(changed, normalized));
        Assert.Equal(before, shining.ToJsonString() + residents.ToJsonString());
    }

    private static JsonObject BuildCanonicalDiagnosisLifecycleDrift(JsonObject shining, JsonObject residents)
    {
        var changed = shining.DeepClone().AsObject();
        var faction = Assert.IsType<JsonObject>(Assert.Single(changed["factions"]!.AsArray()));
        var previousState = faction["factionLifecycle"]!["state"]!.GetValue<string>();
        Assert.Contains(previousState, new[] { "leaderless", "active", "weakened" });
        var requested = previousState == "weakened" ? "active" : "weakened";
        var strength = faction["factionStrength"]!.GetValue<int>() + (previousState == "leaderless" ? 15 : 0);
        faction["factionLifecycle"]!["state"] = requested;
        var expected = changed.DeepClone().AsObject();
        expected["factions"]![0]!["factionStrength"] = strength;
        ShiningAbodeState.NormalizeStateRoot(changed, residents);
        Assert.Equal(requested, faction["factionLifecycle"]!["state"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(expected, changed),
            "The detached lifecycle fault may normalize only its independently expected leaderless strength penalty.");
        return changed;
    }
}
