using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualHealingArtTests
{
    [Theory]
    [InlineData("spiritual_resilience", "Духовная стойкость")]
    [InlineData("spiritual_healing", "Духовное исцеление")]
    public void StandardWoundArt_IsVisibleAtZeroWithOrdinaryDefinition(string id, string name)
    {
        var art = Assert.Single(AfterlifeSpiritualConflictState.SpiritualArts,
            item => item.ArtId == id);
        Assert.Equal(name, art.DisplayName);
        Assert.Equal(1, art.MinUnlockTier);
        Assert.Contains(id, AfterlifeEntityProfileState.StandardArtIds);
        Assert.DoesNotContain(id, AfterlifeSpiritualConflictState.OperationTypes);
        Assert.DoesNotContain(id, AfterlifeEntityProfileState.SpecialArtBaseOperations);
        var profile = AfterlifeSpiritualConflictState.CreateDefaultCombatProfile();
        var tiers = Assert.IsType<JsonObject>(profile["artTiers"]);
        Assert.Equal(0, Assert.IsAssignableFrom<JsonValue>(tiers[id]).GetValue<int>());
    }

    [Theory]
    [InlineData("spiritual_resilience", 1, 125, 8)]
    [InlineData("spiritual_resilience", 2, 175, 11)]
    [InlineData("spiritual_resilience", 3, 225, 14)]
    [InlineData("spiritual_resilience", 4, 275, 17)]
    [InlineData("spiritual_resilience", 5, 325, 20)]
    [InlineData("spiritual_healing", 1, 125, 8)]
    [InlineData("spiritual_healing", 2, 175, 11)]
    [InlineData("spiritual_healing", 3, 225, 14)]
    [InlineData("spiritual_healing", 4, 275, 17)]
    [InlineData("spiritual_healing", 5, 325, 20)]
    public void StandardWoundArt_UsesOrdinaryCosts(string id, int tier, int ink, int sparks)
    {
        var art = Assert.Single(AfterlifeSpiritualConflictState.SpiritualArts,
            item => item.ArtId == id);
        Assert.Equal(ink, AfterlifeTrainingCostPolicy.ComputeStandardArtBaseInkFeatherCost(art, tier));
        Assert.Equal(sparks, AfterlifeTrainingCostPolicy.ComputeStandardArtBaseLightSparkCost(art, tier));
        Assert.Equal(ink * 4, AfterlifeTrainingCostPolicy.ComputeSelfStandardArtInkFeatherCost(art, tier));
        Assert.Equal(sparks * 4, AfterlifeTrainingCostPolicy.ComputeSelfStandardArtLightSparkCost(art, tier));
        Assert.Equal(ink, AfterlifeTrainingCostPolicy.ComputeMentorCost(ink, 0));
        Assert.Equal(ink * 80 / 100, AfterlifeTrainingCostPolicy.ComputeMentorCost(ink, 30));
        Assert.Equal(ink * 60 / 100, AfterlifeTrainingCostPolicy.ComputeMentorCost(ink, 60));
    }

    [Fact]
    public void StandardWoundArts_DefaultProfilesAreDetached()
    {
        var first = AfterlifeSpiritualConflictState.CreateDefaultCombatProfile();
        var second = AfterlifeSpiritualConflictState.CreateDefaultCombatProfile();
        Assert.Equal(0, first["artTiers"]!["spiritual_healing"]?.GetValue<int>());
        first["artTiers"]!["spiritual_healing"] = 4;
        Assert.Equal(0, second["artTiers"]!["spiritual_healing"]?.GetValue<int>());
    }

    [Theory]
    [InlineData("spiritual_resilience", 0)]
    [InlineData("spiritual_resilience", 1)]
    [InlineData("spiritual_resilience", 2)]
    [InlineData("spiritual_resilience", 3)]
    [InlineData("spiritual_resilience", 4)]
    [InlineData("spiritual_resilience", 5)]
    [InlineData("spiritual_healing", 0)]
    [InlineData("spiritual_healing", 1)]
    [InlineData("spiritual_healing", 2)]
    [InlineData("spiritual_healing", 3)]
    [InlineData("spiritual_healing", 4)]
    [InlineData("spiritual_healing", 5)]
    public void StandardWoundArt_PlayerProfileMirrorsSoulScalarAndCannotAutoProgress(string id, int tier)
    {
        var profiles = new JsonObject
        {
            ["profiles"] = new JsonArray
            {
                new JsonObject
                {
                    ["actorType"] = "player_soul",
                    ["actorId"] = "player_soul",
                    ["currencies"] = new JsonObject { ["inkFeathers"] = 999, ["lightSparks"] = 9 },
                    ["standardArts"] = new JsonObject
                    {
                        [id] = new JsonObject { ["tier"] = 5, ["experience"] = 99 },
                        [$"{id}_experience"] = 99
                    },
                    ["progressionStrategy"] = new JsonObject
                    {
                        ["autoProgressionEnabled"] = true,
                        ["priorityOrder"] = new JsonArray(id),
                        ["lastAutoProgressionCycleKey"] = "chaos:4"
                    },
                    ["progressionLedger"] = new JsonArray
                    {
                        new JsonObject { ["source"] = "client_auto_strategy", ["cycleKey"] = "chaos:4" }
                    }
                }
            }
        };
        var soul = new JsonObject
        {
            ["currentRealm"] = "Chaos Sea",
            ["inkFeathers"] = new JsonObject { ["current"] = 17 },
            [AfterlifeSpiritualConflictState.SoulStateProfileProperty] = new JsonObject
            {
                ["artTiers"] = AfterlifeSpiritualConflictState.CreateDefaultArtTiers()
            }
        };
        soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]![id] = tier;

        AfterlifeEntityProfileState.ApplyPlayerSoulProfileClientAuthority(profiles, soul, null);

        var player = Assert.IsType<JsonObject>(profiles["profiles"]![0]);
        var standardArts = Assert.IsType<JsonObject>(player["standardArts"]);
        Assert.Equal(tier, Assert.IsAssignableFrom<JsonValue>(standardArts[id]).GetValue<int>());
        Assert.DoesNotContain(standardArts, property =>
            property.Key.Contains("experience", StringComparison.OrdinalIgnoreCase));
        Assert.False(player["progressionStrategy"]!["autoProgressionEnabled"]!.GetValue<bool>());
        Assert.Null(player["progressionStrategy"]!["lastAutoProgressionCycleKey"]);
        Assert.Empty(Assert.IsType<JsonArray>(player["progressionLedger"]));

        var progressed = AfterlifeEntityProfileState.ProjectCanonicalRoot(
            profiles,
            previousRoot: null,
            BuildProgressionReport("chaos", cycleOrdinal: 5));
        var afterCycle = Assert.IsType<JsonObject>(progressed["profiles"]![0]);
        Assert.Equal(tier, afterCycle["standardArts"]![id]!.GetValue<int>());
        Assert.Equal(17, afterCycle["currencies"]!["inkFeathers"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("spiritual_resilience", 0, "chaos")]
    [InlineData("spiritual_resilience", 1, "guardian")]
    [InlineData("spiritual_resilience", 2, "resident")]
    [InlineData("spiritual_resilience", 3, "shining")]
    [InlineData("spiritual_resilience", 4, "chaos")]
    [InlineData("spiritual_resilience", 5, "shining")]
    [InlineData("spiritual_healing", 0, "chaos")]
    [InlineData("spiritual_healing", 1, "guardian")]
    [InlineData("spiritual_healing", 2, "resident")]
    [InlineData("spiritual_healing", 3, "shining")]
    [InlineData("spiritual_healing", 4, "chaos")]
    [InlineData("spiritual_healing", 5, "shining")]
    public void StandardWoundArt_EntityStrategyUsesOrdinaryScalarProgression(
        string id,
        int oldTier,
        string cycleKind)
    {
        var isShining = cycleKind == "shining";
        var current = BuildEntityProfileRoot(id, oldTier, isShining);

        var projected = AfterlifeEntityProfileState.ProjectCanonicalRoot(
            current,
            previousRoot: null,
            BuildProgressionReport(cycleKind, cycleOrdinal: 9));

        var profile = Assert.IsType<JsonObject>(projected["profiles"]![0]);
        var expectedTier = oldTier == 5 ? 5 : oldTier + 1;
        var expectedSpend = oldTier == 5 ? 0 : 10 * (oldTier + 1);
        var expectedIncome = isShining ? 6 : 12;
        Assert.Equal(expectedTier, profile["standardArts"]![id]!.GetValue<int>());
        Assert.Equal(1000 + expectedIncome - expectedSpend, profile["currencies"]!["inkFeathers"]!.GetValue<int>());
        Assert.IsAssignableFrom<JsonValue>(profile["standardArts"]![id]);
        Assert.DoesNotContain(Assert.IsType<JsonObject>(profile["standardArts"]), property =>
            property.Key.Contains("experience", StringComparison.OrdinalIgnoreCase));

        var entry = Assert.Single(Assert.IsType<JsonArray>(profile["progressionLedger"]).OfType<JsonObject>());
        Assert.Equal("client_auto_strategy", entry["source"]!.GetValue<string>());
        Assert.Equal(expectedSpend, entry["spending"]!["inkFeathers"]!.GetValue<int>());
        var upgrades = Assert.IsType<JsonArray>(entry["upgrades"]);
        if (oldTier == 5)
            Assert.Empty(upgrades);
        else
            Assert.Equal($"{id}:{oldTier}->{oldTier + 1}", Assert.Single(upgrades)!.GetValue<string>());
    }

    [Theory]
    [InlineData("spiritual_resilience")]
    [InlineData("spiritual_healing")]
    public void StandardWoundArt_ExplicitTierDeltaUsesOrdinaryOverride(string id)
    {
        var current = BuildEntityProfileRoot(id, oldTier: 0, isShining: false);
        current[AfterlifeEntityProfileState.ProgressionOverridesProperty] = new JsonArray
        {
            new JsonObject
            {
                ["actorType"] = "guardian",
                ["actorId"] = "guardian_wound_art",
                ["cycleKey"] = "chaos:11",
                ["reason"] = "Сущность развила обычное искусство после принятой сцены.",
                ["summary"] = "Обычный скалярный тир увеличен.",
                ["standardArtTierDeltas"] = new JsonObject { [id] = 1 }
            }
        };

        var projected = AfterlifeEntityProfileState.ProjectCanonicalRoot(current, null, null);

        var profile = Assert.IsType<JsonObject>(projected["profiles"]![0]);
        Assert.Equal(1, profile["standardArts"]![id]!.GetValue<int>());
        var entry = Assert.Single(Assert.IsType<JsonArray>(profile["progressionLedger"]).OfType<JsonObject>());
        Assert.Equal("gm_override", entry["source"]!.GetValue<string>());
        Assert.Equal("chaos:11", entry["cycleKey"]!.GetValue<string>());
    }

    [Fact]
    public void StandardWoundArt_FreshSoulBootstrapUsesSharedDefaultCombatProfile()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "BookOfEternityClient",
            "Core",
            "GameEngine",
            "GameEngine.MainMenu.cs"));

        Assert.Contains(
            "[AfterlifeSpiritualConflictState.SoulStateProfileProperty] = AfterlifeSpiritualConflictState.CreateDefaultCombatProfile()",
            source,
            StringComparison.Ordinal);
    }

    private static JsonObject BuildEntityProfileRoot(string id, int oldTier, bool isShining) =>
        new()
        {
            ["schemaVersion"] = 1,
            ["profiles"] = new JsonArray
            {
                new JsonObject
                {
                    ["actorType"] = "guardian",
                    ["actorId"] = "guardian_wound_art",
                    ["realm"] = isShining ? "Shining Abode" : "Chaos Sea",
                    ["currencies"] = new JsonObject { ["inkFeathers"] = 1000, ["lightSparks"] = 100 },
                    ["standardArts"] = BuildCompleteWoundArts(id, oldTier),
                    ["progressionStrategy"] = new JsonObject
                    {
                        ["autoProgressionEnabled"] = true,
                        ["priorityOrder"] = new JsonArray(id),
                        ["resourceReserve"] = new JsonObject { ["inkFeathers"] = 0, ["lightSparks"] = 0 },
                        ["allowedSpends"] = new JsonArray("standardArts"),
                        ["forbiddenSpends"] = new JsonArray("specialArts")
                    },
                    ["progressionLedger"] = new JsonArray()
                }
            }
        };

    private static JsonObject BuildCompleteWoundArts(string id, int tier)
    {
        var arts = AfterlifeSpiritualConflictState.CreateDefaultArtTiers();
        arts[id] = tier;
        return arts;
    }

    private static JsonObject BuildProgressionReport(string cycleKind, int cycleOrdinal)
    {
        var report = new JsonObject();
        switch (cycleKind)
        {
            case "guardian":
                report["guardianProjectCyclesProcessed"] = 1;
                report["newLastGuardianProjectCycleOrdinal"] = cycleOrdinal;
                break;
            case "resident":
                report["residentAgencyCyclesProcessed"] = 1;
                report["newLastResidentAgencyCycleOrdinal"] = cycleOrdinal;
                break;
            case "shining":
                report["shiningAbodeCyclesProcessed"] = 1;
                report["newLastShiningAbodeCycleOrdinal"] = cycleOrdinal;
                break;
            default:
                report["chaosSeaCyclesProcessed"] = 1;
                report["newLastChaosSeaSimulationOrdinal"] = cycleOrdinal;
                break;
        }

        return new JsonObject { ["progressionProcessingReport"] = report };
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "specs")) &&
                File.Exists(Path.Combine(
                    directory.FullName,
                    "BookOfEternityClient.Tests",
                    "BookOfEternityClient.Tests.csproj")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
