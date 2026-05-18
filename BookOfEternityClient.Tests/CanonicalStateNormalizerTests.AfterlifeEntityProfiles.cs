using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests
{
    [Fact]
    public async Task NormalizeAccumulatedStateAsync_ProjectsAfterlifeEntityProfileUpdates()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            AfterlifeEntityProfileState.StatePath,
            """
            {
              "schemaVersion": 1,
              "afterlifeEntityProfileUpdates": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  "displayName": "Хранитель Зеркал",
                  "realm": "Chaos Sea",
                  "locationName": "Зеркальная Обитель",
                  "currencies": { "inkFeathers": 120, "lightSparks": 0 },
                  "progression": {
                    "enlightenment": { "experience": 48, "tier": 4 },
                    "radiance": { "experience": 0, "tier": 0 }
                  },
                  "standardArts": { "pressure": 2, "guard": 1 },
                  "specialArts": [
                    {
                      "artId": "mirror_guard",
                      "displayName": "Зеркальная Защита",
                      "ownerActorType": "guardian",
                      "ownerActorId": "guardian_mirror",
                      "baseOperation": "guard",
                      "tier": 1,
                      "costMultiplierPercent": 150,
                      "upgradeCost": { "inkFeathers": 30, "lightSparks": 0 },
                      "canTeachPlayer": true,
                      "trainingConditions": ["Провести сцену обучения с Хранителем Зеркал."],
                      "effectSummary": "При успехе отражает часть давления в сторону противника."
                    }
                  ],
                  "soulDissipationTier": 1,
                  "progressionStrategy": {
                    "strategyId": "strategy_guardian_mirror",
                    "summary": "Сначала укрепляет защиту, затем давление.",
                    "priorityOrder": ["guard", "pressure"],
                    "lastUpdatedAtTurn": 22
                  },
                  "warnings": ["ОПАСНО: может развеять душу после победы, если решит это сделать."],
                  "ledger": [
                    {
                      "entryId": "profile_ledger_001",
                      "turnNumber": 22,
                      "reason": "initial_profile",
                      "summary": "Профиль создан при встрече с хранителем."
                    }
                  ]
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse(await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!.AsObject();
        Assert.False(root.ContainsKey(AfterlifeEntityProfileState.UpdateProperty));
        Assert.Equal(1, root["schemaVersion"]?.GetValue<int>());

        var profiles = Assert.IsType<JsonArray>(root["profiles"]);
        var profile = Assert.Single(profiles.OfType<JsonObject>());
        Assert.Equal("guardian_mirror", profile["actorId"]?.GetValue<string>());
        Assert.Equal("Хранитель Зеркал", profile["displayName"]?.GetValue<string>());
        Assert.Equal(120, profile["currencies"]?["inkFeathers"]?.GetValue<int>());
        Assert.Equal(2, profile["standardArts"]?["pressure"]?.GetValue<int>());
        Assert.Equal(1, profile["soulDissipationTier"]?.GetValue<int>());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_AppliesAfterlifeEntityCustomStateChanges()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            AfterlifeEntityProfileState.StatePath,
            """
            {
              "schemaVersion": 1,
              "profiles": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  "displayName": "Хранитель Зеркал",
                  "realm": "Chaos Sea",
                  "currencies": { "inkFeathers": 120, "lightSparks": 0 },
                  "progression": {
                    "enlightenment": { "experience": 48, "tier": 4 },
                    "radiance": { "experience": 0, "tier": 0 }
                  },
                  "standardArts": { "pressure": 2, "guard": 1 },
                  "specialArts": [],
                  "customStates": [
                    {
                      "stateId": "mirror_fever",
                      "stateName": "Зеркальная лихорадка",
                      "currentValue": 2,
                      "minValue": 0,
                      "maxValue": 5,
                      "description": "Старое состояние, которое больше не актуально.",
                      "progressionRule": { "changePerTurn": 0, "description": "Не меняется автоматически." },
                      "thresholds": []
                    }
                  ],
                  "soulDissipationTier": 1,
                  "progressionStrategy": {
                    "strategyId": "strategy_guardian_mirror",
                    "summary": "Сначала укрепляет защиту, затем давление.",
                    "priorityOrder": ["guard", "pressure"]
                  },
                  "ledger": []
                }
              ],
              "afterlifeEntityCustomStateChanges": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  "statesToAddOrUpdate": [
                    {
                      "stateId": "echo_hunger",
                      "stateName": "Голод эха",
                      "currentValue": 3,
                      "minValue": 0,
                      "maxValue": 10,
                      "description": "Сущность тянется к повторяющимся клятвам.",
                      "progressionRule": { "changePerTurn": 1, "description": "Растёт после сцен с повторением клятв." },
                      "thresholds": [
                        {
                          "levelName": "Навязчивое эхо",
                          "triggerCondition": "currentValue >= 6",
                          "triggerValue": 6,
                          "associatedEffects": []
                        }
                      ]
                    }
                  ],
                  "statesToRemove": ["mirror_fever"]
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse(await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!.AsObject();
        Assert.False(root.ContainsKey("afterlifeEntityCustomStateChanges"));

        var profile = Assert.Single(root["profiles"]!.AsArray().OfType<JsonObject>());
        var states = Assert.IsType<JsonArray>(profile["customStates"]);
        var state = Assert.Single(states.OfType<JsonObject>());
        Assert.Equal("echo_hunger", state["stateId"]?.GetValue<string>());
        Assert.Equal("Голод эха", state["stateName"]?.GetValue<string>());
        Assert.Equal(3, state["currentValue"]?.GetValue<int>());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_AppliesDeterministicEntityProgressionFromAfterlifeReport()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            ProgressionScheduleService.ReportPath,
            """
            {
              "progressionProcessingReport": {
                "sessionId": "session_1",
                "requestId": "request_1",
                "turnNumber": 7,
                "chaosSeaCyclesProcessed": 1,
                "guardianProjectCyclesProcessed": 1,
                "newLastChaosSeaSimulationOrdinal": 5,
                "newLastGuardianProjectCycleOrdinal": 5
              }
            }
            """);
        await _fs.WriteFileAtomicAsync(
            AfterlifeEntityProfileState.StatePath,
            """
            {
              "schemaVersion": 1,
              "profiles": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  "displayName": "Хранитель Зеркал",
                  "realm": "Chaos Sea",
                  "currencies": { "inkFeathers": 0, "lightSparks": 0 },
                  "progression": {
                    "enlightenment": { "experience": 0, "tier": 0 },
                    "radiance": { "experience": 0, "tier": 0 }
                  },
                  "standardArts": { "guard": 0 },
                  "specialArts": [],
                  "customStates": [],
                  "soulDissipationTier": 0,
                  "progressionStrategy": {
                    "strategyId": "strategy_guardian_mirror",
                    "summary": "Качать защиту.",
                    "priorityOrder": ["guard"]
                  },
                  "ledger": []
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse(await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!.AsObject();
        var profile = Assert.Single(root["profiles"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(1, profile["standardArts"]?["guard"]?.GetValue<int>());
        Assert.Equal(2, profile["currencies"]?["inkFeathers"]?.GetValue<int>());
        Assert.Equal("chaos:5", profile["progressionStrategy"]?["lastAutoProgressionCycleKey"]?.GetValue<string>());

        var ledger = Assert.IsType<JsonArray>(profile["progressionLedger"]);
        var entry = Assert.Single(ledger.OfType<JsonObject>());
        Assert.Equal("client_auto_strategy", entry["source"]?.GetValue<string>());
        Assert.Equal("chaos:5", entry["cycleKey"]?.GetValue<string>());
        Assert.Equal(12, entry["income"]?["inkFeathers"]?.GetValue<int>());
        Assert.Equal(10, entry["spending"]?["inkFeathers"]?.GetValue<int>());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_AppliesEntityProgressionPerAfterlifeContour()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            ProgressionScheduleService.ReportPath,
            """
            {
              "progressionProcessingReport": {
                "sessionId": "session_mixed",
                "requestId": "request_mixed",
                "turnNumber": 30,
                "guardianProjectCyclesProcessed": 1,
                "newLastGuardianProjectCycleOrdinal": 20,
                "shiningAbodeCyclesProcessed": 1,
                "newLastShiningAbodeCycleOrdinal": 30
              }
            }
            """);
        await _fs.WriteFileAtomicAsync(
            AfterlifeEntityProfileState.StatePath,
            """
            {
              "schemaVersion": 1,
              "profiles": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_chaos",
                  "displayName": "Хранитель Хаоса",
                  "realm": "Chaos Sea",
                  "currencies": { "inkFeathers": 0, "lightSparks": 0 },
                  "progression": {
                    "enlightenment": { "experience": 0, "tier": 0 },
                    "radiance": { "experience": 0, "tier": 0 }
                  },
                  "standardArts": { "guard": 0 },
                  "specialArts": [],
                  "customStates": [],
                  "soulDissipationTier": 0,
                  "progressionStrategy": {
                    "strategyId": "strategy_chaos_guardian",
                    "summary": "Качать защиту.",
                    "priorityOrder": ["guard"]
                  },
                  "ledger": []
                },
                {
                  "actorType": "radiant_actor",
                  "actorId": "resident_shining",
                  "displayName": "Сияющий резидент",
                  "realm": "Shining Abode",
                  "currencies": { "inkFeathers": 0, "lightSparks": 0 },
                  "progression": {
                    "enlightenment": { "experience": 0, "tier": 0 },
                    "radiance": { "experience": 0, "tier": 0 }
                  },
                  "standardArts": { "guard": 0 },
                  "specialArts": [],
                  "customStates": [],
                  "soulDissipationTier": 0,
                  "progressionStrategy": {
                    "strategyId": "strategy_shining_resident",
                    "summary": "Качать сияние.",
                    "priorityOrder": ["radiance"]
                  },
                  "ledger": []
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse(await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!.AsObject();
        var profiles = root["profiles"]!.AsArray().OfType<JsonObject>().ToDictionary(
            profile => profile["actorId"]!.GetValue<string>(),
            StringComparer.OrdinalIgnoreCase);

        var chaosProfile = profiles["guardian_chaos"];
        Assert.Equal(1, chaosProfile["standardArts"]?["guard"]?.GetValue<int>());
        Assert.Equal(2, chaosProfile["currencies"]?["inkFeathers"]?.GetValue<int>());
        Assert.Equal(0, chaosProfile["currencies"]?["lightSparks"]?.GetValue<int>());
        Assert.Equal("chaos:20", chaosProfile["progressionStrategy"]?["lastAutoProgressionCycleKey"]?.GetValue<string>());
        var chaosLedger = Assert.Single(chaosProfile["progressionLedger"]!.AsArray().OfType<JsonObject>());
        Assert.Equal("chaos:20", chaosLedger["cycleKey"]?.GetValue<string>());
        Assert.Equal(12, chaosLedger["income"]?["inkFeathers"]?.GetValue<int>());
        Assert.Equal(0, chaosLedger["income"]?["lightSparks"]?.GetValue<int>());

        var shiningProfile = profiles["resident_shining"];
        Assert.Equal(20, shiningProfile["progression"]?["radiance"]?["experience"]?.GetValue<int>());
        Assert.Equal(1, shiningProfile["progression"]?["radiance"]?["tier"]?.GetValue<int>());
        Assert.Equal(6, shiningProfile["currencies"]?["inkFeathers"]?.GetValue<int>());
        Assert.Equal(0, shiningProfile["currencies"]?["lightSparks"]?.GetValue<int>());
        Assert.Equal("shining:30", shiningProfile["progressionStrategy"]?["lastAutoProgressionCycleKey"]?.GetValue<string>());
        var shiningLedger = Assert.Single(shiningProfile["progressionLedger"]!.AsArray().OfType<JsonObject>());
        Assert.Equal("shining:30", shiningLedger["cycleKey"]?.GetValue<string>());
        Assert.Equal(6, shiningLedger["income"]?["inkFeathers"]?.GetValue<int>());
        Assert.Equal(1, shiningLedger["income"]?["lightSparks"]?.GetValue<int>());
        Assert.Equal(1, shiningLedger["spending"]?["lightSparks"]?.GetValue<int>());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_AutoProgressionUpgradesSpecialArtByStrategy()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            ProgressionScheduleService.ReportPath,
            """
            {
              "progressionProcessingReport": {
                "chaosSeaCyclesProcessed": 1,
                "newLastChaosSeaSimulationOrdinal": 8
              }
            }
            """);
        await _fs.WriteFileAtomicAsync(
            AfterlifeEntityProfileState.StatePath,
            """
            {
              "schemaVersion": 1,
              "profiles": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  "displayName": "Хранитель Зеркал",
                  "realm": "Chaos Sea",
                  "currencies": { "inkFeathers": 18, "lightSparks": 0 },
                  "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
                  "standardArts": { "guard": 0 },
                  "specialArts": [
                    {
                      "artId": "mirror_guard",
                      "displayName": "Зеркальная Защита",
                      "ownerActorType": "guardian",
                      "ownerActorId": "guardian_mirror",
                      "baseOperation": "guard",
                      "tier": 1,
                      "costMultiplierPercent": 150,
                      "upgradeCost": { "inkFeathers": 30, "lightSparks": 0 },
                      "canTeachPlayer": true,
                      "trainingConditions": ["Провести сцену обучения с Хранителем Зеркал."],
                      "effectSummary": "При успехе отражает часть давления."
                    }
                  ],
                  "customStates": [],
                  "soulDissipationTier": 0,
                  "progressionStrategy": {
                    "strategyId": "strategy_guardian_mirror",
                    "summary": "Качать особую защиту.",
                    "priorityOrder": ["mirror_guard"]
                  },
                  "ledger": []
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse(await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!.AsObject();
        var profile = Assert.Single(root["profiles"]!.AsArray().OfType<JsonObject>());
        var art = Assert.Single(profile["specialArts"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(2, art["tier"]?.GetValue<int>());
        Assert.Equal(0, profile["currencies"]?["inkFeathers"]?.GetValue<int>());

        var ledger = Assert.IsType<JsonArray>(profile["progressionLedger"]);
        var entry = Assert.Single(ledger.OfType<JsonObject>());
        var upgrades = Assert.IsType<JsonArray>(entry["upgrades"]);
        Assert.Contains(upgrades.OfType<JsonValue>(), item => item.GetValue<string>() == "specialArt:mirror_guard:1->2");
        Assert.Equal(30, entry["spending"]?["inkFeathers"]?.GetValue<int>());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_AutoProgressionUpgradesSoulDissipationByStrategy()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            ProgressionScheduleService.ReportPath,
            """
            {
              "progressionProcessingReport": {
                "chaosSeaCyclesProcessed": 1,
                "newLastChaosSeaSimulationOrdinal": 9
              }
            }
            """);
        await _fs.WriteFileAtomicAsync(
            AfterlifeEntityProfileState.StatePath,
            """
            {
              "schemaVersion": 1,
              "profiles": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  "displayName": "Хранитель Зеркал",
                  "realm": "Chaos Sea",
                  "currencies": { "inkFeathers": 38, "lightSparks": 0 },
                  "progression": { "enlightenment": { "experience": 60, "tier": 3 }, "radiance": { "experience": 0, "tier": 0 } },
                  "standardArts": { "guard": 0 },
                  "specialArts": [],
                  "customStates": [],
                  "soulDissipationTier": 0,
                  "progressionStrategy": {
                    "strategyId": "strategy_guardian_mirror",
                    "summary": "Качать развеивание души.",
                    "priorityOrder": ["soul_dissipation"]
                  },
                  "ledger": []
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse(await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!.AsObject();
        var profile = Assert.Single(root["profiles"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(1, profile["soulDissipationTier"]?.GetValue<int>());
        Assert.Equal(0, profile["currencies"]?["inkFeathers"]?.GetValue<int>());

        var ledger = Assert.IsType<JsonArray>(profile["progressionLedger"]);
        var entry = Assert.Single(ledger.OfType<JsonObject>());
        var upgrades = Assert.IsType<JsonArray>(entry["upgrades"]);
        Assert.Contains(upgrades.OfType<JsonValue>(), item => item.GetValue<string>() == "soulDissipation:0->1");
        Assert.Equal(50, entry["spending"]?["inkFeathers"]?.GetValue<int>());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_AppliesEntityProgressionOverrideWithLedger()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            AfterlifeEntityProfileState.StatePath,
            """
            {
              "schemaVersion": 1,
              "profiles": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  "displayName": "Хранитель Зеркал",
                  "realm": "Chaos Sea",
                  "currencies": { "inkFeathers": 10, "lightSparks": 1 },
                  "progression": {
                    "enlightenment": { "experience": 0, "tier": 0 },
                    "radiance": { "experience": 0, "tier": 0 }
                  },
                  "standardArts": { "pressure": 0 },
                  "specialArts": [],
                  "customStates": [],
                  "soulDissipationTier": 0,
                  "progressionStrategy": {
                    "strategyId": "strategy_guardian_mirror",
                    "summary": "Качать давление.",
                    "priorityOrder": ["pressure"]
                  },
                  "ledger": [],
                  "afterlifeEntityProgressionOverrides": []
                }
              ],
              "afterlifeEntityProgressionOverrides": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  "cycleKey": "chaos:6",
                  "reason": "GM решил, что хранитель сделал рывок после сцены.",
                  "currencyDeltas": { "inkFeathers": -5, "lightSparks": 2 },
                  "standardArtTierDeltas": { "pressure": 1 },
                  "summary": "Хранитель потратил Перья на давление."
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse(await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!.AsObject();
        Assert.False(root.ContainsKey("afterlifeEntityProgressionOverrides"));

        var profile = Assert.Single(root["profiles"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(1, profile["standardArts"]?["pressure"]?.GetValue<int>());
        Assert.Equal(5, profile["currencies"]?["inkFeathers"]?.GetValue<int>());
        Assert.Equal(3, profile["currencies"]?["lightSparks"]?.GetValue<int>());
        Assert.Equal("chaos:6", profile["progressionStrategy"]?["lastAutoProgressionCycleKey"]?.GetValue<string>());

        var ledger = Assert.IsType<JsonArray>(profile["progressionLedger"]);
        var entry = Assert.Single(ledger.OfType<JsonObject>());
        Assert.Equal("gm_override", entry["source"]?.GetValue<string>());
        Assert.Equal("chaos:6", entry["cycleKey"]?.GetValue<string>());
        Assert.Equal("Хранитель потратил Перья на давление.", entry["summary"]?.GetValue<string>());
        Assert.Equal(0, entry["income"]?["inkFeathers"]?.GetValue<int>());
        Assert.Equal(2, entry["income"]?["lightSparks"]?.GetValue<int>());
        Assert.Equal(5, entry["spending"]?["inkFeathers"]?.GetValue<int>());
        Assert.Equal(0, entry["spending"]?["lightSparks"]?.GetValue<int>());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_AppliesSpecialArtAndSoulDissipationProgressionOverride()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            AfterlifeEntityProfileState.StatePath,
            """
            {
              "schemaVersion": 1,
              "profiles": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  "displayName": "Хранитель Зеркал",
                  "realm": "Chaos Sea",
                  "currencies": { "inkFeathers": 100, "lightSparks": 0 },
                  "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
                  "standardArts": { "pressure": 0 },
                  "specialArts": [
                    {
                      "artId": "mirror_guard",
                      "displayName": "Зеркальная Защита",
                      "ownerActorType": "guardian",
                      "ownerActorId": "guardian_mirror",
                      "baseOperation": "guard",
                      "tier": 1,
                      "costMultiplierPercent": 150,
                      "upgradeCost": { "inkFeathers": 30, "lightSparks": 0 },
                      "canTeachPlayer": true,
                      "trainingConditions": ["Провести сцену обучения с Хранителем Зеркал."],
                      "effectSummary": "При успехе отражает часть давления."
                    }
                  ],
                  "customStates": [],
                  "soulDissipationTier": 0,
                  "progressionStrategy": { "strategyId": "strategy_guardian_mirror", "summary": "Качать особую защиту.", "priorityOrder": ["mirror_guard"] },
                  "ledger": []
                }
              ],
              "afterlifeEntityProgressionOverrides": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  "cycleKey": "chaos:10",
                  "reason": "Хранитель изменил приоритет после дуэли.",
                  "summary": "Хранитель принудительно усилил особое искусство и развеивание души.",
                  "currencyDeltas": { "inkFeathers": -80 },
                  "specialArtTierDeltas": { "mirror_guard": 1 },
                  "soulDissipationTierDelta": 1
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse(await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!.AsObject();
        Assert.False(root.ContainsKey(AfterlifeEntityProfileState.ProgressionOverridesProperty));
        var profile = Assert.Single(root["profiles"]!.AsArray().OfType<JsonObject>());
        var art = Assert.Single(profile["specialArts"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(2, art["tier"]?.GetValue<int>());
        Assert.Equal(1, profile["soulDissipationTier"]?.GetValue<int>());
        Assert.Equal(20, profile["currencies"]?["inkFeathers"]?.GetValue<int>());

        var ledger = Assert.IsType<JsonArray>(profile["progressionLedger"]);
        var entry = Assert.Single(ledger.OfType<JsonObject>());
        Assert.Equal("gm_override", entry["source"]?.GetValue<string>());
        Assert.Equal("chaos:10", entry["cycleKey"]?.GetValue<string>());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_PreservesInvalidOverrideMarkerForUnknownSpecialArtDelta()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var backupPath = "game_state/control/test_backups/afterlife_entity_profiles.previous.json";
        await _fs.WriteFileAtomicAsync(
            backupPath,
            """
            {
              "schemaVersion": 1,
              "profiles": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  "displayName": "Хранитель Зеркал",
                  "realm": "Chaos Sea",
                  "currencies": { "inkFeathers": 100, "lightSparks": 0 },
                  "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
                  "standardArts": { "guard": 0 },
                  "specialArts": [],
                  "customStates": [],
                  "soulDissipationTier": 0,
                  "progressionStrategy": { "strategyId": "strategy_guardian_mirror", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
                  "ledger": []
                }
              ]
            }
            """);
        await _fs.WriteFileAtomicAsync(
            AfterlifeEntityProfileState.StatePath,
            """
            {
              "schemaVersion": 1,
              "afterlifeEntityProgressionOverrides": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  "cycleKey": "chaos:9",
                  "reason": "GM override.",
                  "summary": "Опечатка в artId.",
                  "specialArtTierDeltas": { "miror_guard": 1 }
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>
        {
            [AfterlifeEntityProfileState.StatePath] = backupPath
        });

        var root = JsonNode.Parse(await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!.AsObject();
        Assert.True(root.ContainsKey("lastInvalidProgressionOverride"));
        Assert.Equal("unknown_special_art", root["lastInvalidProgressionOverrideReason"]?.GetValue<string>());
        var profile = Assert.Single(root["profiles"]!.AsArray().OfType<JsonObject>());
        Assert.Empty(profile["specialArts"]!.AsArray());
        Assert.False(root.ContainsKey(AfterlifeEntityProfileState.ProgressionOverridesProperty));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_AppliesSpecialArtLearningReceiptToPlayerProfile()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            AfterlifeEntityProfileState.StatePath,
            """
            {
              "schemaVersion": 1,
              "profiles": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  "displayName": "Хранитель Зеркал",
                  "realm": "Chaos Sea",
                  "currencies": { "inkFeathers": 120, "lightSparks": 0 },
                  "progression": { "enlightenment": { "experience": 48, "tier": 4 }, "radiance": { "experience": 0, "tier": 0 } },
                  "standardArts": { "guard": 1 },
                  "specialArts": [
                    {
                      "artId": "mirror_guard",
                      "displayName": "Зеркальная Защита",
                      "ownerActorType": "guardian",
                      "ownerActorId": "guardian_mirror",
                      "baseOperation": "guard",
                      "tier": 2,
                      "costMultiplierPercent": 150,
                      "upgradeCost": { "inkFeathers": 30, "lightSparks": 0 },
                      "canTeachPlayer": true,
                      "trainingConditions": ["Провести сцену обучения с Хранителем Зеркал."],
                      "effectSummary": "При успехе отражает часть давления в сторону противника."
                    }
                  ],
                  "customStates": [],
                  "soulDissipationTier": 0,
                  "progressionStrategy": { "strategyId": "strategy_guardian_mirror", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
                  "ledger": []
                },
                {
                  "actorType": "player_soul",
                  "actorId": "player_soul",
                  "displayName": "Асуран",
                  "realm": "Chaos Sea",
                  "currencies": { "inkFeathers": 0, "lightSparks": 0 },
                  "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
                  "standardArts": { "guard": 0 },
                  "specialArts": [],
                  "customStates": [],
                  "soulDissipationTier": 0,
                  "progressionStrategy": { "strategyId": "strategy_player", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
                  "ledger": []
                }
              ],
              "afterlifeSpecialArtLearningReceipts": [
                {
                  "receiptId": "learn_mirror_guard_001",
                  "teacherActorType": "guardian",
                  "teacherActorId": "guardian_mirror",
                  "playerActorId": "player_soul",
                  "artId": "mirror_guard",
                  "learnedAtTurn": 31,
                  "trainingConditionSatisfied": true,
                  "roleplayEvidence": "Игрок прошёл сцену отражения клятв и Хранитель признал обучение.",
                  "summary": "Игрок изучил Зеркальную Защиту."
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse(await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!.AsObject();
        Assert.False(root.ContainsKey(AfterlifeEntityProfileState.SpecialArtLearningReceiptsProperty));

        var player = root["profiles"]!.AsArray()
            .OfType<JsonObject>()
            .Single(profile => profile["actorType"]?.GetValue<string>() == "player_soul");
        var learnedArt = Assert.Single(player["specialArts"]!.AsArray().OfType<JsonObject>());
        Assert.Equal("mirror_guard", learnedArt["artId"]?.GetValue<string>());
        Assert.Equal("player_soul", learnedArt["ownerActorType"]?.GetValue<string>());
        Assert.Equal("player_soul", learnedArt["ownerActorId"]?.GetValue<string>());
        Assert.Equal("guardian_mirror", learnedArt["learnedFromActorId"]?.GetValue<string>());
        Assert.Equal(31, learnedArt["learnedAtTurn"]?.GetValue<int>());

        var ledger = Assert.IsType<JsonArray>(player["ledger"]);
        var entry = Assert.Single(ledger.OfType<JsonObject>());
        Assert.Equal("learn_special_art", entry["reason"]?.GetValue<string>());
    }
}
