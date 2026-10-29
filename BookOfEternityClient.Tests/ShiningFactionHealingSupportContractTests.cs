using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ShiningFactionHealingSupportContractTests
{
    [Fact]
    public void AuthoredHealingRoleSurvivesExistingNormalization()
    {
        var resident = new JsonObject
        {
            ["ascensionState"] = "ascended",
            ["shiningFactionId"] = "faction_healing_support",
            ["residentRole"] = "archive_support",
            ["isPresent"] = false // Chaos abode presence is not Shining membership.
        };
        ShiningAbodeState.NormalizeResidentShiningFields(resident, shiningRoot: null);
        Assert.Equal("archive_support", resident["residentRole"]!.GetValue<string>());

        resident["residentRole"] = "healing_support";
        ShiningAbodeState.NormalizeResidentShiningFields(resident, shiningRoot: null);
        Assert.Equal("healing_support", resident["residentRole"]!.GetValue<string>());
        Assert.False(resident.ContainsKey("standardArts"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void ExactHealerCapabilityDoesNotCreateAService(int tier)
    {
        var resident = Resident("healer"); var profile = Profile("healer", tier);
        var before = profile.ToJsonString();
        Assert.True(ShiningFactionHealingSupport.TryReadHealerTier(resident, profile, out var actual));
        Assert.Equal(tier, actual); Assert.Equal(before, profile.ToJsonString());
        Assert.False(profile.ContainsKey("healingServiceProfile"));
        profile["lifeState"] = "alive";
        profile["healingServiceProfile"] = new JsonObject
        {
            ["providerId"] = "healer", ["realm"] = "shining_abode",
            ["locationRef"] = profile["locationId"]!.DeepClone(), ["visibility"] = "public",
            ["availability"] = "unavailable", ["minimumRelationship"] = 25,
            ["priceMultiplierPercent"] = 100, ["compensationKinds"] = new JsonArray("favor"),
            ["accessConditions"] = new JsonArray("Согласие фракции")
        };
        Assert.True(ShiningFactionHealingSupport.TryReadHealerTier(resident, profile, out actual));
        Assert.Equal(tier, actual);
        Assert.Null(AfterlifeHealingServiceContract.ReadPublicCurrencyProfile(
            new("shining_abode", "resident", "healer", WoundCarrierCatalog.AfterlifeProfilesPath), profile));
    }

    [Theory]
    [InlineData("zero")] [InlineData("six")] [InlineData("string")] [InlineData("missing_art")]
    [InlineData("wrong_kind")] [InlineData("wrong_id")] [InlineData("wrong_realm")]
    [InlineData("hidden")] [InlineData("dead")] [InlineData("invalid_life")]
    [InlineData("null_life")] [InlineData("malformed_envelope")]
    public void InvalidCurrentProfileCannotProveHealingSupport(string change)
    {
        var profile = Profile("healer", 3);
        switch (change)
        {
            case "zero": profile["standardArts"]!["spiritual_healing"] = 0; break;
            case "six": profile["standardArts"]!["spiritual_healing"] = 6; break;
            case "string": profile["standardArts"]!["spiritual_healing"] = "3"; break;
            case "missing_art": profile["standardArts"]!.AsObject().Remove("spiritual_healing"); break;
            case "wrong_kind": profile["actorType"] = "guardian"; break;
            case "wrong_id": profile["actorId"] = "other"; break;
            case "wrong_realm": profile["realm"] = "Chaos Sea"; break;
            case "hidden": profile["isPlayerVisible"] = false; break;
            case "dead": profile["lifeState"] = "dead"; break;
            case "invalid_life": profile["lifeState"] = true; break;
            case "null_life": profile["lifeState"] = null; break;
            case "malformed_envelope": profile["materialization"]!["state"] = "pending"; break;
        }
        var before = profile.ToJsonString();
        Assert.False(ShiningFactionHealingSupport.TryReadHealerTier(Resident("healer"), profile, out _));
        Assert.Equal(before, profile.ToJsonString());
    }

    [Theory]
    [InlineData("not_ascended")] [InlineData("foreign_faction")] [InlineData("other_role")]
    public void OnlyTheExactCurrentHealerAffiliationQualifies(string change)
    {
        var resident = Resident("healer");
        Assert.True(ShiningFactionHealingSupport.IsCurrentHealerResident(resident, "faction_healing_support"));
        switch (change)
        {
            case "not_ascended": resident["ascensionState"] = "not_ascended"; break;
            case "foreign_faction": resident["shiningFactionId"] = "other"; break;
            case "other_role": resident["residentRole"] = "archive_support"; break;
        }
        Assert.False(ShiningFactionHealingSupport.IsCurrentHealerResident(resident, "faction_healing_support"));
    }

    [Fact]
    public void VisibleRosterUsesDetachedExactProfilesWithoutInventingAuthority()
    {
        var residents = new JsonObject { ["entries"] = new JsonArray(Resident("first"), Resident("second")) };
        var first = Profile("first", 1); var second = Profile("second", 5);
        var profiles = new JsonObject { ["profiles"] = new JsonArray(first, second) };
        var faction = new JsonObject { ["factionId"] = "faction_healing_support" };
        var before = residents.ToJsonString() + profiles.ToJsonString();
        var result = ShiningFactionHealingSupport.ProjectVisibleRoster(faction, residents, profiles);
        Assert.Equal(new int?[] { 1, 5 }, result.Select(r => r.HealingTier));
        Assert.All(result, r => Assert.Equal("целитель", r.Role));
        Assert.Equal(before, residents.ToJsonString() + profiles.ToJsonString());
        second["standardArts"]!["spiritual_healing"] = 3;
        Assert.Equal(3, ShiningFactionHealingSupport.ProjectVisibleRoster(faction, residents, profiles)[1].HealingTier);
        Assert.Equal(5, result[1].HealingTier); // Exported strings/integers retain no mutable profile.
        second["visibility"] = "hidden";
        Assert.Single(ShiningFactionHealingSupport.ProjectVisibleRoster(faction, residents, profiles));
    }

    [Theory]
    [InlineData("duplicate_resident")] [InlineData("duplicate_profile")] [InlineData("guardian_only")]
    public void AmbiguousOrWrongKindProfilesNeverSupplyRosterDetails(string change)
    {
        var residents = new JsonObject { ["entries"] = new JsonArray(Resident("healer")) };
        var profile = Profile("healer", 3);
        var profiles = new JsonObject { ["profiles"] = new JsonArray(profile) };
        if (change == "duplicate_resident") residents["entries"]!.AsArray().Add(Resident("healer"));
        else if (change == "duplicate_profile") profiles["profiles"]!.AsArray().Add(profile.DeepClone());
        else profile["actorType"] = "guardian";
        Assert.Empty(ShiningFactionHealingSupport.ProjectVisibleRoster(
            new JsonObject { ["factionId"] = "faction_healing_support" }, residents, profiles));
    }

    private static JsonObject Resident(string id) => new()
    {
        ["residentId"] = id, ["displayName"] = "Целитель " + id,
        ["ascensionState"] = "ascended", ["shiningFactionId"] = "faction_healing_support",
        ["residentRole"] = "healing_support", ["isPresent"] = false
    };

    private static JsonObject Profile(string id, int tier)
    {
        var profile = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile("resident", id, "Shining Abode", 42);
        profile["standardArts"]!["spiritual_healing"] = tier;
        return profile;
    }
}
