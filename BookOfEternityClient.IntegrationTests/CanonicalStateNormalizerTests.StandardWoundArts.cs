using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests
{
    [Theory]
    [InlineData("spiritual_resilience", 0)]
    [InlineData("spiritual_resilience", 1)]
    [InlineData("spiritual_healing", 0)]
    [InlineData("spiritual_healing", 1)]
    public async Task StandardWoundArt_NormalizeAccumulatedStatePersistsOrdinaryProgressionOnce(
        string id,
        int currentTier)
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
                            ["spiritual_resilience"] = id == "spiritual_resilience" ? currentTier : 0,
                            ["spiritual_healing"] = id == "spiritual_healing" ? currentTier : 0
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
        var expectedTier = currentTier + 1;
        var expectedSpend = 10 * expectedTier;
        Assert.Equal(expectedTier, profile["standardArts"]![id]!.GetValue<int>());
        Assert.Equal(1012 - expectedSpend, profile["currencies"]!["inkFeathers"]!.GetValue<int>());
        Assert.Equal("chaos:17", profile["progressionStrategy"]!["lastAutoProgressionCycleKey"]!.GetValue<string>());
        var entry = Assert.Single(Assert.IsType<JsonArray>(profile["progressionLedger"]).OfType<JsonObject>());
        Assert.Equal("client_auto_strategy", entry["source"]!.GetValue<string>());
        Assert.Equal(expectedSpend, entry["spending"]!["inkFeathers"]!.GetValue<int>());
        Assert.Equal($"{id}:{currentTier}->{expectedTier}", Assert.Single(entry["upgrades"]!.AsArray())!.GetValue<string>());
        Assert.IsAssignableFrom<JsonValue>(profile["standardArts"]![id]);
    }

    [Theory]
    [InlineData("spiritual_resilience", "__missing__")]
    [InlineData("spiritual_resilience", "null")]
    [InlineData("spiritual_resilience", "\"1\"")]
    [InlineData("spiritual_resilience", "true")]
    [InlineData("spiritual_resilience", "1.5")]
    [InlineData("spiritual_resilience", "-1")]
    [InlineData("spiritual_resilience", "6")]
    [InlineData("spiritual_resilience", "{ \"tier\": 1, \"experience\": 0 }")]
    [InlineData("spiritual_healing", "__missing__")]
    [InlineData("spiritual_healing", "null")]
    [InlineData("spiritual_healing", "\"1\"")]
    [InlineData("spiritual_healing", "true")]
    [InlineData("spiritual_healing", "1.5")]
    [InlineData("spiritual_healing", "-1")]
    [InlineData("spiritual_healing", "6")]
    [InlineData("spiritual_healing", "{ \"tier\": 1, \"experience\": 0 }")]
    public async Task StandardWoundArt_InvalidCurrentTierIsNotRepairedByAutomaticProgression(
        string id,
        string rawValue)
    {
        await WriteCorrelatedWoundArtProgressionReportAsync(cycleOrdinal: 41);
        var root = BuildCompleteWoundArtProfileRoot(id);
        var profile = Assert.IsType<JsonObject>(root["profiles"]![0]);
        var arts = Assert.IsType<JsonObject>(profile["standardArts"]);
        SetInvalidCurrentTier(arts, id, rawValue);
        var existedBefore = arts.ContainsKey(id);
        var valueBefore = arts[id]?.DeepClone();
        await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, root.ToJsonString());

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

        var normalizedRoot = JsonNode.Parse(
            (await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        var normalizedProfile = Assert.IsType<JsonObject>(normalizedRoot["profiles"]![0]);
        AssertInvalidTierPreserved(normalizedProfile, id, existedBefore, valueBefore);
        Assert.Equal(1012, normalizedProfile["currencies"]!["inkFeathers"]!.GetValue<int>());
        var entry = Assert.Single(
            Assert.IsType<JsonArray>(normalizedProfile["progressionLedger"]).OfType<JsonObject>());
        Assert.Equal(0, entry["spending"]!["inkFeathers"]!.GetValue<int>());
        Assert.Empty(Assert.IsType<JsonArray>(entry["upgrades"]));
        Assert.False(normalizedRoot.ContainsKey(AfterlifeEntityProfileState.LastInvalidProgressionOverrideProperty));
        await AssertProductionValidatorStillRejectsCurrentTierAsync(id, rawValue);
    }

    [Theory]
    [InlineData("spiritual_resilience", "__missing__")]
    [InlineData("spiritual_resilience", "null")]
    [InlineData("spiritual_resilience", "\"1\"")]
    [InlineData("spiritual_resilience", "true")]
    [InlineData("spiritual_resilience", "1.5")]
    [InlineData("spiritual_resilience", "-1")]
    [InlineData("spiritual_resilience", "6")]
    [InlineData("spiritual_resilience", "{ \"tier\": 1, \"experience\": 0 }")]
    [InlineData("spiritual_healing", "__missing__")]
    [InlineData("spiritual_healing", "null")]
    [InlineData("spiritual_healing", "\"1\"")]
    [InlineData("spiritual_healing", "true")]
    [InlineData("spiritual_healing", "1.5")]
    [InlineData("spiritual_healing", "-1")]
    [InlineData("spiritual_healing", "6")]
    [InlineData("spiritual_healing", "{ \"tier\": 1, \"experience\": 0 }")]
    public async Task StandardWoundArt_InvalidCurrentTierRejectsExplicitOverrideAtomically(
        string id,
        string rawValue)
    {
        var root = BuildCompleteWoundArtProfileRoot(id);
        var profile = Assert.IsType<JsonObject>(root["profiles"]![0]);
        var arts = Assert.IsType<JsonObject>(profile["standardArts"]);
        SetInvalidCurrentTier(arts, id, rawValue);
        var existedBefore = arts.ContainsKey(id);
        var valueBefore = arts[id]?.DeepClone();
        root[AfterlifeEntityProfileState.ProgressionOverridesProperty] = BuildWoundArtOverride(
            id,
            includeCurrencyDelta: true);
        await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, root.ToJsonString());

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

        var normalizedRoot = JsonNode.Parse(
            (await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        var normalizedProfile = Assert.IsType<JsonObject>(normalizedRoot["profiles"]![0]);
        AssertInvalidTierPreserved(normalizedProfile, id, existedBefore, valueBefore);
        Assert.Equal(1000, normalizedProfile["currencies"]!["inkFeathers"]!.GetValue<int>());
        Assert.Empty(Assert.IsType<JsonArray>(normalizedProfile["progressionLedger"]));
        Assert.Equal(
            "invalid_standard_art_delta",
            normalizedRoot[AfterlifeEntityProfileState.LastInvalidProgressionOverrideReasonProperty]!.GetValue<string>());
        Assert.False(normalizedRoot.ContainsKey(AfterlifeEntityProfileState.ProgressionOverridesProperty));
        await AssertProductionValidatorStillRejectsCurrentTierAsync(id, rawValue);
    }

    [Theory]
    [InlineData("spiritual_resilience")]
    [InlineData("spiritual_healing")]
    public async Task StandardWoundArt_ValidZeroCurrentTierAcceptsExplicitOverrideAtomically(string id)
    {
        var root = BuildCompleteWoundArtProfileRoot(id);
        root[AfterlifeEntityProfileState.ProgressionOverridesProperty] = BuildWoundArtOverride(
            id,
            includeCurrencyDelta: true);
        await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, root.ToJsonString());

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

        var normalizedRoot = JsonNode.Parse(
            (await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        var profile = Assert.IsType<JsonObject>(normalizedRoot["profiles"]![0]);
        Assert.Equal(1, profile["standardArts"]![id]!.GetValue<int>());
        Assert.Equal(975, profile["currencies"]!["inkFeathers"]!.GetValue<int>());
        Assert.Single(Assert.IsType<JsonArray>(profile["progressionLedger"]));
        Assert.False(normalizedRoot.ContainsKey(AfterlifeEntityProfileState.LastInvalidProgressionOverrideProperty));
    }

    [Theory]
    [InlineData("spiritual_resilience", false)]
    [InlineData("spiritual_resilience", true)]
    [InlineData("spiritual_healing", false)]
    [InlineData("spiritual_healing", true)]
    public async Task StandardWoundArt_ValidMaximumTierRemainsCapped(string id, bool explicitOverride)
    {
        var root = BuildCompleteWoundArtProfileRoot(id, currentTier: 5);
        if (explicitOverride)
        {
            root[AfterlifeEntityProfileState.ProgressionOverridesProperty] = BuildWoundArtOverride(
                id,
                includeCurrencyDelta: false);
        }
        else
        {
            await WriteCorrelatedWoundArtProgressionReportAsync(cycleOrdinal: 42);
        }
        await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, root.ToJsonString());

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

        var normalizedRoot = JsonNode.Parse(
            (await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        var profile = Assert.IsType<JsonObject>(normalizedRoot["profiles"]![0]);
        Assert.Equal(5, profile["standardArts"]![id]!.GetValue<int>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StandardWoundArt_OldOptionalArtOmissionRetainsProgressionSemantics(bool explicitOverride)
    {
        const string oldArtId = "guard";
        var root = BuildCompleteWoundArtProfileRoot(oldArtId);
        Assert.IsType<JsonObject>(root["profiles"]![0]!["standardArts"]).Remove(oldArtId);
        if (explicitOverride)
        {
            root[AfterlifeEntityProfileState.ProgressionOverridesProperty] = BuildWoundArtOverride(
                oldArtId,
                includeCurrencyDelta: false);
        }
        else
        {
            await WriteCorrelatedWoundArtProgressionReportAsync(cycleOrdinal: 43);
        }
        await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, root.ToJsonString());

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

        var normalizedRoot = JsonNode.Parse(
            (await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        var profile = Assert.IsType<JsonObject>(normalizedRoot["profiles"]![0]);
        Assert.Equal(1, profile["standardArts"]![oldArtId]!.GetValue<int>());
    }

    [Theory]
    [InlineData("spiritual_resilience", false)]
    [InlineData("spiritual_resilience", true)]
    [InlineData("spiritual_healing", false)]
    [InlineData("spiritual_healing", true)]
    public async Task StandardWoundArt_InvalidCurrentTierAlternateCaseTargetCannotCreateAlias(
        string id,
        bool explicitOverride)
    {
        var root = BuildCompleteWoundArtProfileRoot(id);
        var profile = Assert.IsType<JsonObject>(root["profiles"]![0]);
        var arts = Assert.IsType<JsonObject>(profile["standardArts"]);
        arts.Remove(id);
        var alternateCaseId = id.ToUpperInvariant();
        profile["progressionStrategy"]!["priorityOrder"] = new JsonArray(alternateCaseId);
        if (explicitOverride)
        {
            root[AfterlifeEntityProfileState.ProgressionOverridesProperty] = BuildWoundArtOverride(
                alternateCaseId,
                includeCurrencyDelta: true);
        }
        else
        {
            await WriteCorrelatedWoundArtProgressionReportAsync(cycleOrdinal: 44);
        }
        await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, root.ToJsonString());

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

        var normalizedRoot = JsonNode.Parse(
            (await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        var normalizedProfile = Assert.IsType<JsonObject>(normalizedRoot["profiles"]![0]);
        var normalizedArts = Assert.IsType<JsonObject>(normalizedProfile["standardArts"]);
        Assert.False(normalizedArts.ContainsKey(id));
        Assert.False(normalizedArts.ContainsKey(alternateCaseId));
    }

    private async Task WriteCorrelatedWoundArtProgressionReportAsync(int cycleOrdinal)
    {
        await _fs.WriteFileAtomicAsync(
            ProgressionScheduleService.ReportPath,
            new JsonObject
            {
                ["progressionProcessingReport"] = new JsonObject
                {
                    ["sessionId"] = $"session_standard_wound_art_{cycleOrdinal}",
                    ["requestId"] = $"request_standard_wound_art_{cycleOrdinal}",
                    ["turnNumber"] = cycleOrdinal,
                    ["chaosSeaCyclesProcessed"] = 1,
                    ["guardianProjectCyclesProcessed"] = 1,
                    ["newLastChaosSeaSimulationOrdinal"] = cycleOrdinal,
                    ["newLastGuardianProjectCycleOrdinal"] = cycleOrdinal
                }
            }.ToJsonString());
        await CorrelateAfterlifeProgressionReportWithTurnRequestAsync();
    }

    private static JsonObject BuildCompleteWoundArtProfileRoot(string priorityArtId, int currentTier = 0)
    {
        var root = JsonNode.Parse(AfterlifeEntityProfileValidationTests.BuildValidProfileJson())!.AsObject();
        var profile = Assert.IsType<JsonObject>(root["profiles"]![0]);
        profile["currencies"] = new JsonObject { ["inkFeathers"] = 1000, ["lightSparks"] = 0 };
        profile["standardArts"]!["spiritual_resilience"] = 0;
        profile["standardArts"]!["spiritual_healing"] = 0;
        if (AfterlifeSpiritualConflictState.RequiredWoundArtIds.Contains(priorityArtId, StringComparer.OrdinalIgnoreCase))
            profile["standardArts"]![priorityArtId] = currentTier;
        profile["progressionStrategy"] = new JsonObject
        {
            ["strategyId"] = "strategy_standard_wound_art_authority",
            ["summary"] = "Развивать выбранное обычное искусство.",
            ["priorityOrder"] = new JsonArray(priorityArtId),
            ["resourceReserve"] = new JsonObject { ["inkFeathers"] = 0, ["lightSparks"] = 0 },
            ["allowedSpends"] = new JsonArray("standardArts"),
            ["forbiddenSpends"] = new JsonArray("specialArts")
        };
        profile["progressionLedger"] = new JsonArray();
        return root;
    }

    private static JsonArray BuildWoundArtOverride(string id, bool includeCurrencyDelta)
    {
        var entry = new JsonObject
        {
            ["actorType"] = "guardian",
            ["actorId"] = "guardian_mirror",
            ["cycleKey"] = "chaos:standard_wound_art_override",
            ["reason"] = "Проверка текущего скалярного тира.",
            ["summary"] = "Обычный тир изменяется только из валидного текущего значения.",
            ["standardArtTierDeltas"] = new JsonObject { [id] = 1 }
        };
        if (includeCurrencyDelta)
            entry["currencyDeltas"] = new JsonObject { ["inkFeathers"] = -25 };
        return new JsonArray(entry);
    }

    private static void SetInvalidCurrentTier(JsonObject arts, string id, string rawValue)
    {
        if (rawValue == "__missing__")
        {
            arts.Remove(id);
            return;
        }

        arts[id] = JsonNode.Parse(rawValue);
    }

    private static void AssertInvalidTierPreserved(
        JsonObject profile,
        string id,
        bool existedBefore,
        JsonNode? valueBefore)
    {
        var arts = Assert.IsType<JsonObject>(profile["standardArts"]);
        Assert.Equal(existedBefore, arts.ContainsKey(id));
        if (existedBefore)
            Assert.True(JsonNode.DeepEquals(valueBefore, arts[id]));
    }

    private async Task AssertProductionValidatorStillRejectsCurrentTierAsync(string id, string rawValue)
    {
        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(IntegrationValidationProfiles.AfterlifeEntityProfile);
        var expectedCode = rawValue == "__missing__"
            ? "afterlife_entity_profile_missing_standard_art_tier"
            : "afterlife_entity_profile_invalid_standard_art_tier";
        Assert.Contains(issues, issue =>
            issue.Code == expectedCode &&
            issue.FilePath.EndsWith($".standardArts.{id}", StringComparison.Ordinal));
    }
}
