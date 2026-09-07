using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests
{
    [Theory]
    [InlineData("spiritual_resilience")]
    [InlineData("spiritual_healing")]
    public async Task StandardWoundArt_NormalizeAccumulatedStatePersistsOrdinaryProgressionOnce(string id)
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            ProgressionScheduleService.ReportPath,
            """
            {
              "progressionProcessingReport": {
                "sessionId": "session_standard_wound_art",
                "requestId": "request_standard_wound_art",
                "turnNumber": 17,
                "chaosSeaCyclesProcessed": 1,
                "guardianProjectCyclesProcessed": 1,
                "newLastChaosSeaSimulationOrdinal": 17,
                "newLastGuardianProjectCycleOrdinal": 17
              }
            }
            """);
        await CorrelateAfterlifeProgressionReportWithTurnRequestAsync();
        await _fs.WriteFileAtomicAsync(
            AfterlifeEntityProfileState.StatePath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["profiles"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["actorType"] = "guardian",
                        ["actorId"] = "guardian_standard_wound_art",
                        ["displayName"] = "Хранитель целостности",
                        ["realm"] = "Chaos Sea",
                        ["currencies"] = new JsonObject { ["inkFeathers"] = 1000, ["lightSparks"] = 0 },
                        ["progression"] = new JsonObject
                        {
                            ["enlightenment"] = new JsonObject { ["experience"] = 0, ["tier"] = 0 },
                            ["radiance"] = new JsonObject { ["experience"] = 0, ["tier"] = 0 }
                        },
                        ["standardArts"] = new JsonObject
                        {
                            ["spiritual_resilience"] = id == "spiritual_resilience" ? 1 : 0,
                            ["spiritual_healing"] = id == "spiritual_healing" ? 1 : 0
                        },
                        ["specialArts"] = new JsonArray(),
                        ["customStates"] = new JsonArray(),
                        ["soulDissipationTier"] = 0,
                        ["progressionStrategy"] = new JsonObject
                        {
                            ["strategyId"] = "strategy_standard_wound_art",
                            ["summary"] = "Развивать выбранное обычное искусство.",
                            ["priorityOrder"] = new JsonArray(id),
                            ["resourceReserve"] = new JsonObject { ["inkFeathers"] = 0, ["lightSparks"] = 0 },
                            ["allowedSpends"] = new JsonArray("standardArts"),
                            ["forbiddenSpends"] = new JsonArray("specialArts")
                        },
                        ["progressionLedger"] = new JsonArray(),
                        ["ledger"] = new JsonArray()
                    }
                }
            }.ToJsonString());

        await normalizer.NormalizeAccumulatedStateAsync();
        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        var profile = Assert.IsType<JsonObject>(root["profiles"]![0]);
        Assert.Equal(2, profile["standardArts"]![id]!.GetValue<int>());
        Assert.Equal(992, profile["currencies"]!["inkFeathers"]!.GetValue<int>());
        Assert.Equal("chaos:17", profile["progressionStrategy"]!["lastAutoProgressionCycleKey"]!.GetValue<string>());
        var entry = Assert.Single(Assert.IsType<JsonArray>(profile["progressionLedger"]).OfType<JsonObject>());
        Assert.Equal("client_auto_strategy", entry["source"]!.GetValue<string>());
        Assert.Equal(20, entry["spending"]!["inkFeathers"]!.GetValue<int>());
        Assert.Equal($"{id}:1->2", Assert.Single(entry["upgrades"]!.AsArray())!.GetValue<string>());
        Assert.IsAssignableFrom<JsonValue>(profile["standardArts"]![id]);
    }
}
