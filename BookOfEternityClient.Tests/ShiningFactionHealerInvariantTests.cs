using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ShiningFactionHealerInvariantTests
{
    [Theory]
    [InlineData("active", true)] [InlineData("weakened", true)] [InlineData("leaderless", true)]
    [InlineData("broken", false)] [InlineData("dissolved", false)] [InlineData("unknown", true)]
    [InlineData("missing", true)] [InlineData("null", true)] [InlineData("scalar", true)] [InlineData("array", true)]
    public void OnlySupportedHistoricalLifecycleExemptsTheHealerWithoutChangingState(string shape, bool required)
    {
        var faction = new JsonObject { ["visibility"] = "hidden" };
        if (shape != "missing") faction["factionLifecycle"] = shape switch
        {
            "null" => null, "scalar" => JsonValue.Create("broken"), "array" => new JsonArray("broken"),
            _ => new JsonObject { ["state"] = shape }
        };
        var before = faction.ToJsonString();
        Assert.Equal(required, ShiningFactionHealingSupport.RequiresLivingHealer(faction));
        Assert.Equal(!required, ShiningAbodeState.IsFactionDefeated(faction));
        Assert.Equal(before, faction.ToJsonString());
    }

    [Fact]
    public void CanonicalHiddenHealerNeverBecomesVisibleThroughTheExistingCapabilityReader()
    {
        var resident = new JsonObject { ["residentId"] = "healer", ["ascensionState"] = "ascended",
            ["shiningFactionId"] = "faction", ["residentRole"] = "healing_support", ["isPresent"] = false };
        var profile = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile("resident", "healer", "Shining Abode", 42);
        profile["standardArts"]!["spiritual_healing"] = 5;
        profile["isPlayerVisible"] = false;
        var before = profile.ToJsonString();
        Assert.True(ShiningFactionHealingSupport.TryReadCanonicalHealerTierFromValidatedProfile(resident, profile, out var tier));
        Assert.Equal(5, tier);
        Assert.False(ShiningFactionHealingSupport.TryReadHealerTier(resident, profile, out tier));
        Assert.Equal(0, tier);
        Assert.Empty(ShiningFactionHealingSupport.ProjectVisibleRoster(new JsonObject { ["factionId"] = "faction" },
            new JsonObject { ["entries"] = new JsonArray(resident) }, new JsonObject { ["profiles"] = new JsonArray(profile) }));
        Assert.Equal(before, profile.ToJsonString());
    }
}
