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

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
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

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        Assert.False(root.ContainsKey("afterlifeEntityCustomStateChanges"));

        var profile = Assert.Single(root["profiles"]!.AsArray().OfType<JsonObject>());
        var states = Assert.IsType<JsonArray>(profile["customStates"]);
        var state = Assert.Single(states.OfType<JsonObject>());
        Assert.Equal("echo_hunger", state["stateId"]?.GetValue<string>());
        Assert.Equal("Голод эха", state["stateName"]?.GetValue<string>());
        Assert.Equal(3, state["currentValue"]?.GetValue<int>());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_AppliesAfterlifeActorAgencyCommands()
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
                  "customStates": [],
                  "soulDissipationTier": 1,
                  "progressionStrategy": {
                    "strategyId": "strategy_guardian_mirror",
                    "summary": "Сначала укрепляет защиту, затем давление.",
                    "priorityOrder": ["guard", "pressure"]
                  },
                  "ledger": []
                }
              ],
              "afterlifeActorGoalUpdates": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  "goalId": "goal_mirror_oath",
                  "shortTermGoal": "Проверить, понимает ли Душа цену клятв.",
                  "longTermGoal": "Не дать Сарефу снова использовать забытые обеты.",
                  "plan": "Подтолкнуть Душу к сцене зеркального суда.",
                  "gmThoughtsSummary": "Хранитель действует из страха повторить старую ошибку.",
                  "updatedAtTurn": 31
                }
              ],
              "afterlifeActorQuestUpdates": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  "questId": "quest_mirror_oath_trial",
                  "goalId": "goal_mirror_oath",
                  "title": "Суд зеркальной клятвы",
                  "status": "active",
                  "planSummary": "Подготовить испытание и не раскрывать истинную причину заранее.",
                  "successCondition": "Душа осознанно откажется от удобной лжи.",
                  "createdAtTurn": 31
                }
              ],
              "afterlifeActorActivityUpdates": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  "activityId": "activity_prepare_mirror_trial",
                  "goalId": "goal_mirror_oath",
                  "linkedQuestId": "quest_mirror_oath_trial",
                  "activityType": "offscreen_preparation",
                  "summary": "Собирает осколки свидетельств для сцены суда.",
                  "status": "active",
                  "gmThoughtsSummary": "Он готовит сцену, но не принуждает игрока идти туда.",
                  "startedAtTurn": 31
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        Assert.False(root.ContainsKey(AfterlifeEntityProfileState.GoalUpdatesProperty));
        Assert.False(root.ContainsKey(AfterlifeEntityProfileState.QuestUpdatesProperty));
        Assert.False(root.ContainsKey(AfterlifeEntityProfileState.ActivityUpdatesProperty));

        var profile = Assert.Single(root["profiles"]!.AsArray().OfType<JsonObject>());
        Assert.Equal("goal_mirror_oath", profile["goals"]?["goalId"]?.GetValue<string>());
        Assert.Equal("Суд зеркальной клятвы", Assert.Single(profile["personalQuests"]!.AsArray().OfType<JsonObject>())["title"]?.GetValue<string>());
        Assert.Equal("activity_prepare_mirror_trial", profile["currentActivity"]?["activityId"]?.GetValue<string>());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_CompletesAfterlifeActorActivityByCommand()
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
                  "customStates": [],
                  "soulDissipationTier": 1,
                  "progressionStrategy": {
                    "strategyId": "strategy_guardian_mirror",
                    "summary": "Сначала укрепляет защиту, затем давление.",
                    "priorityOrder": ["guard", "pressure"]
                  },
                  "goals": {
                    "goalId": "goal_mirror_oath",
                    "shortTermGoal": "Проверить Душу.",
                    "longTermGoal": "Закрыть старую клятву.",
                    "plan": "Завершить подготовку суда.",
                    "gmThoughtsSummary": "Хранитель не хочет повторить ошибку.",
                    "updatedAtTurn": 31
                  },
                  "personalQuests": [
                    {
                      "questId": "quest_mirror_oath_trial",
                      "goalId": "goal_mirror_oath",
                      "title": "Суд зеркальной клятвы",
                      "status": "active",
                      "planSummary": "Подготовить испытание.",
                      "successCondition": "Душа выберет правду.",
                      "createdAtTurn": 31
                    }
                  ],
                  "currentActivity": {
                    "activityId": "activity_prepare_mirror_trial",
                    "goalId": "goal_mirror_oath",
                    "linkedQuestId": "quest_mirror_oath_trial",
                    "activityType": "offscreen_preparation",
                    "summary": "Собирает осколки свидетельств.",
                    "status": "active",
                    "gmThoughtsSummary": "Подготовка следует его цели.",
                    "startedAtTurn": 31
                  },
                  "ledger": []
                }
              ],
              "completeAfterlifeActorActivities": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  "activityId": "activity_prepare_mirror_trial",
                  "outcome": "completed",
                  "resultingQuestStatus": "completed",
                  "summary": "Осколки собраны, сцена готова.",
                  "completedAtTurn": 32
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        Assert.False(root.ContainsKey(AfterlifeEntityProfileState.CompleteActivitiesProperty));

        var profile = Assert.Single(root["profiles"]!.AsArray().OfType<JsonObject>());
        Assert.False(profile.ContainsKey("currentActivity"));
        var quest = Assert.Single(profile["personalQuests"]!.AsArray().OfType<JsonObject>());
        Assert.Equal("completed", quest["status"]?.GetValue<string>());
        var completed = Assert.Single(profile["completedActivities"]!.AsArray().OfType<JsonObject>());
        Assert.Equal("activity_prepare_mirror_trial", completed["activityId"]?.GetValue<string>());
        Assert.Equal("completed", completed["outcome"]?.GetValue<string>());
    }

    [Theory]
    [InlineData("resident", "resident_oath_001", "Резидент Клятв", "Эхо клятвы", "oath_echo", "oath_released")]
    [InlineData("shining_faction_head", "head_ember_001", "Глава Пепельной Хартии", "Брожение хартии", "charter_unrest", "charter_quieted")]
    public async Task NormalizeAccumulatedStateAsync_AppliesCustomStateLifecycleForResidentAndFactionLeader(
        string actorType,
        string actorId,
        string displayName,
        string oldStateName,
        string oldStateId,
        string newStateId)
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            AfterlifeEntityProfileState.StatePath,
            $$"""
            {
              "schemaVersion": 1,
              "profiles": [
                {
                  "actorType": "{{actorType}}",
                  "actorId": "{{actorId}}",
                  "displayName": "{{displayName}}",
                  "realm": "Shining Abode",
                  "currencies": { "inkFeathers": 60, "lightSparks": 8 },
                  "progression": {
                    "enlightenment": { "experience": 30, "tier": 2 },
                    "radiance": { "experience": 110, "tier": 3 }
                  },
                  "standardArts": { "pressure": 1, "guard": 1 },
                  "specialArts": [],
                  "customStates": [
                    {
                      "stateId": "{{oldStateId}}",
                      "stateName": "{{oldStateName}}",
                      "currentValue": 4,
                      "minValue": 0,
                      "maxValue": 10,
                      "description": "Состояние должно быть удалено targeted lifecycle-командой.",
                      "progressionRule": { "changePerTurn": 0, "description": "Не меняется автоматически." },
                      "thresholds": []
                    }
                  ],
                  "soulDissipationTier": 0,
                  "progressionStrategy": {
                    "strategyId": "strategy_{{actorId}}",
                    "summary": "Поддерживает текущее состояние.",
                    "priorityOrder": ["guard"]
                  },
                  "ledger": []
                }
              ],
              "afterlifeEntityCustomStateChanges": [
                {
                  "actorType": "{{actorType}}",
                  "actorId": "{{actorId}}",
                  "statesToAddOrUpdate": [
                    {
                      "stateId": "{{newStateId}}",
                      "stateName": "Новое состояние",
                      "currentValue": 1,
                      "minValue": 0,
                      "maxValue": 5,
                      "description": "Новое состояние остаётся видимым в профиле после нормализации.",
                      "progressionRule": { "changePerTurn": 1, "description": "Растёт после тематических сцен." },
                      "thresholds": []
                    }
                  ],
                  "statesToRemove": ["{{oldStateId}}"]
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        Assert.False(root.ContainsKey(AfterlifeEntityProfileState.CustomStateChangesProperty));
        var profile = Assert.Single(root["profiles"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(actorType, profile["actorType"]?.GetValue<string>());
        var states = Assert.IsType<JsonArray>(profile["customStates"]);
        var state = Assert.Single(states.OfType<JsonObject>());
        Assert.Equal(newStateId, state["stateId"]?.GetValue<string>());
        Assert.Equal("Новое состояние", state["stateName"]?.GetValue<string>());
        Assert.DoesNotContain(states.OfType<JsonObject>(), item =>
            string.Equals(item["stateId"]?.GetValue<string>(), oldStateId, StringComparison.OrdinalIgnoreCase));
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
        await CorrelateAfterlifeProgressionReportWithTurnRequestAsync();
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

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
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
    public async Task NormalizeAccumulatedStateAsync_DoesNotAutoProgressProfilesFromStaleAfterlifeReport()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            "input/turn_request.json",
            """
            {
              "sessionId": "session_current",
              "requestId": "request_current",
              "turnNumber": 8
            }
            """);
        await _fs.WriteFileAtomicAsync(
            ProgressionScheduleService.ReportPath,
            """
            {
              "progressionProcessingReport": {
                "sessionId": "session_stale",
                "requestId": "request_stale",
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

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        var profile = Assert.Single(root["profiles"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(0, profile["standardArts"]?["guard"]?.GetValue<int>());
        Assert.Equal(0, profile["currencies"]?["inkFeathers"]?.GetValue<int>());
        Assert.Null(profile["progressionStrategy"]?["lastAutoProgressionCycleKey"]);
        Assert.Null(profile["progressionLedger"]);
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
        await CorrelateAfterlifeProgressionReportWithTurnRequestAsync();
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

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
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
    public async Task NormalizeAccumulatedStateAsync_RespectsEntityProgressionResourceReserve()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            ProgressionScheduleService.ReportPath,
            """
            {
              "progressionProcessingReport": {
                "chaosSeaCyclesProcessed": 1,
                "newLastChaosSeaSimulationOrdinal": 31
              }
            }
            """);
        await CorrelateAfterlifeProgressionReportWithTurnRequestAsync();
        await _fs.WriteFileAtomicAsync(
            AfterlifeEntityProfileState.StatePath,
            """
            {
              "schemaVersion": 1,
              "profiles": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_with_reserve_room",
                  "displayName": "Хранитель с запасом",
                  "realm": "Chaos Sea",
                  "currencies": { "inkFeathers": 20, "lightSparks": 0 },
                  "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
                  "standardArts": { "guard": 0 },
                  "specialArts": [],
                  "customStates": [],
                  "soulDissipationTier": 0,
                  "progressionStrategy": {
                    "strategyId": "reserve_room",
                    "summary": "Качать защиту, не трогая резерв.",
                    "priorityOrder": ["guard"],
                    "resourceReserve": { "inkFeathers": 15, "lightSparks": 0 }
                  },
                  "ledger": []
                },
                {
                  "actorType": "guardian",
                  "actorId": "guardian_without_reserve_room",
                  "displayName": "Хранитель без запаса",
                  "realm": "Chaos Sea",
                  "currencies": { "inkFeathers": 2, "lightSparks": 0 },
                  "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
                  "standardArts": { "guard": 0 },
                  "specialArts": [],
                  "customStates": [],
                  "soulDissipationTier": 0,
                  "progressionStrategy": {
                    "strategyId": "reserve_block",
                    "summary": "Качать защиту, не трогая резерв.",
                    "priorityOrder": ["guard"],
                    "resourceReserve": { "inkFeathers": 15, "lightSparks": 0 }
                  },
                  "ledger": []
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        var profiles = root["profiles"]!.AsArray().OfType<JsonObject>().ToDictionary(
            profile => profile["actorId"]!.GetValue<string>(),
            StringComparer.OrdinalIgnoreCase);

        var upgraded = profiles["guardian_with_reserve_room"];
        Assert.Equal(1, upgraded["standardArts"]?["guard"]?.GetValue<int>());
        Assert.Equal(22, upgraded["currencies"]?["inkFeathers"]?.GetValue<int>());
        var upgradedLedger = Assert.Single(upgraded["progressionLedger"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(10, upgradedLedger["spending"]?["inkFeathers"]?.GetValue<int>());

        var blocked = profiles["guardian_without_reserve_room"];
        Assert.Equal(0, blocked["standardArts"]?["guard"]?.GetValue<int>());
        Assert.Equal(14, blocked["currencies"]?["inkFeathers"]?.GetValue<int>());
        var blockedLedger = Assert.Single(blocked["progressionLedger"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(0, blockedLedger["spending"]?["inkFeathers"]?.GetValue<int>());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_SkipsForbiddenEntityProgressionSpend()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            ProgressionScheduleService.ReportPath,
            """
            {
              "progressionProcessingReport": {
                "chaosSeaCyclesProcessed": 1,
                "newLastChaosSeaSimulationOrdinal": 32
              }
            }
            """);
        await CorrelateAfterlifeProgressionReportWithTurnRequestAsync();
        await _fs.WriteFileAtomicAsync(
            AfterlifeEntityProfileState.StatePath,
            """
            {
              "schemaVersion": 1,
              "profiles": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_forbid_dissipation",
                  "displayName": "Хранитель с запретом",
                  "realm": "Chaos Sea",
                  "currencies": { "inkFeathers": 48, "lightSparks": 0 },
                  "progression": { "enlightenment": { "experience": 60, "tier": 3 }, "radiance": { "experience": 0, "tier": 0 } },
                  "standardArts": { "guard": 0 },
                  "specialArts": [],
                  "customStates": [],
                  "soulDissipationTier": 0,
                  "progressionStrategy": {
                    "strategyId": "forbid_dissipation",
                    "summary": "Не качать развеивание души.",
                    "priorityOrder": ["soul_dissipation", "guard"],
                    "forbiddenSpends": ["soulDissipationTier"]
                  },
                  "ledger": []
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        var profile = Assert.Single(root["profiles"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(0, profile["soulDissipationTier"]?.GetValue<int>());
        Assert.Equal(1, profile["standardArts"]?["guard"]?.GetValue<int>());
        Assert.Equal(50, profile["currencies"]?["inkFeathers"]?.GetValue<int>());
        var ledger = Assert.Single(profile["progressionLedger"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(10, ledger["spending"]?["inkFeathers"]?.GetValue<int>());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_UsesAllowedEntityProgressionSpendList()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            ProgressionScheduleService.ReportPath,
            """
            {
              "progressionProcessingReport": {
                "chaosSeaCyclesProcessed": 1,
                "newLastChaosSeaSimulationOrdinal": 33
              }
            }
            """);
        await CorrelateAfterlifeProgressionReportWithTurnRequestAsync();
        await _fs.WriteFileAtomicAsync(
            AfterlifeEntityProfileState.StatePath,
            """
            {
              "schemaVersion": 1,
              "profiles": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_allow_enlightenment",
                  "displayName": "Хранитель просветления",
                  "realm": "Chaos Sea",
                  "currencies": { "inkFeathers": 20, "lightSparks": 0 },
                  "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
                  "standardArts": { "guard": 0 },
                  "specialArts": [],
                  "customStates": [],
                  "soulDissipationTier": 0,
                  "progressionStrategy": {
                    "strategyId": "allow_enlightenment",
                    "summary": "Качать только просветление.",
                    "priorityOrder": ["guard", "enlightenment"],
                    "allowedSpends": ["enlightenment"]
                  },
                  "ledger": []
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        var profile = Assert.Single(root["profiles"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(0, profile["standardArts"]?["guard"]?.GetValue<int>());
        Assert.Equal(20, profile["progression"]?["enlightenment"]?["experience"]?.GetValue<int>());
        Assert.Equal(1, profile["progression"]?["enlightenment"]?["tier"]?.GetValue<int>());
        Assert.Equal(22, profile["currencies"]?["inkFeathers"]?.GetValue<int>());
        var ledger = Assert.Single(profile["progressionLedger"]!.AsArray().OfType<JsonObject>());
        var upgrades = Assert.IsType<JsonArray>(ledger["upgrades"]);
        Assert.Contains(upgrades.OfType<JsonValue>(), item => item.GetValue<string>() == "enlightenment:experience+20");
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_DoesNotReapplySettledCycleAfterStrategyRefresh()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            ProgressionScheduleService.ReportPath,
            """
            {
              "progressionProcessingReport": {
                "sessionId": "session_refresh",
                "requestId": "request_refresh",
                "turnNumber": 40,
                "chaosSeaCyclesProcessed": 1,
                "newLastChaosSeaSimulationOrdinal": 40
              }
            }
            """);
        await CorrelateAfterlifeProgressionReportWithTurnRequestAsync();

        var backupPath = "game_state/control/test_backups/afterlife_entity_profiles.refresh.previous.json";
        await _fs.WriteFileAtomicAsync(
            backupPath,
            """
            {
              "schemaVersion": 1,
              "profiles": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_refresh",
                  "displayName": "Хранитель Обновления",
                  "realm": "Chaos Sea",
                  "currencies": { "inkFeathers": 2, "lightSparks": 0 },
                  "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
                  "standardArts": { "guard": 1, "pressure": 0 },
                  "specialArts": [],
                  "customStates": [],
                  "soulDissipationTier": 0,
                  "progressionStrategy": {
                    "strategyId": "strategy_refresh",
                    "summary": "Сначала защита.",
                    "priorityOrder": ["guard"],
                    "lastAutoProgressionCycleKey": "chaos:40"
                  },
                  "progressionLedger": [
                    {
                      "entryId": "entity_progression_auto_guardian_guardian_refresh_chaos_40",
                      "cycleKey": "chaos:40",
                      "source": "client_auto_strategy",
                      "summary": "Автопрокачка по стратегии применила доход и один приоритетный апгрейд.",
                      "income": { "inkFeathers": 12, "lightSparks": 0 },
                      "spending": { "inkFeathers": 10, "lightSparks": 0 },
                      "upgrades": ["guard:0->1"]
                    }
                  ],
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
              "afterlifeEntityProfileUpdates": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_refresh",
                  "displayName": "Хранитель Обновления",
                  "realm": "Chaos Sea",
                  "currencies": { "inkFeathers": 2, "lightSparks": 0 },
                  "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
                  "standardArts": { "guard": 1, "pressure": 0 },
                  "specialArts": [],
                  "customStates": [],
                  "soulDissipationTier": 0,
                  "progressionStrategy": {
                    "strategyId": "strategy_refresh",
                    "summary": "Теперь сначала давление.",
                    "priorityOrder": ["pressure"]
                  },
                  "progressionLedger": [
                    {
                      "entryId": "entity_progression_auto_guardian_guardian_refresh_chaos_40",
                      "cycleKey": "chaos:40",
                      "source": "client_auto_strategy",
                      "summary": "Автопрокачка по стратегии применила доход и один приоритетный апгрейд.",
                      "income": { "inkFeathers": 12, "lightSparks": 0 },
                      "spending": { "inkFeathers": 10, "lightSparks": 0 },
                      "upgrades": ["guard:0->1"]
                    }
                  ],
                  "ledger": []
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>
        {
            [AfterlifeEntityProfileState.StatePath] = backupPath
        });

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        var profile = Assert.Single(root["profiles"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(2, profile["currencies"]?["inkFeathers"]?.GetValue<int>());
        Assert.Equal(1, profile["standardArts"]?["guard"]?.GetValue<int>());
        Assert.Equal(0, profile["standardArts"]?["pressure"]?.GetValue<int>());
        Assert.Equal("Теперь сначала давление.", profile["progressionStrategy"]?["summary"]?.GetValue<string>());
        Assert.Equal("chaos:40", profile["progressionStrategy"]?["lastAutoProgressionCycleKey"]?.GetValue<string>());
        Assert.Single(profile["progressionLedger"]!.AsArray().OfType<JsonObject>());
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
        await CorrelateAfterlifeProgressionReportWithTurnRequestAsync();
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

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
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
        await CorrelateAfterlifeProgressionReportWithTurnRequestAsync();
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

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
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
                  "realm": "Shining Abode",
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
                  "cycleKey": "shining:6",
                  "reason": "GM решил, что хранитель сделал рывок после сцены.",
                  "currencyDeltas": { "inkFeathers": -5, "lightSparks": 2 },
                  "standardArtTierDeltas": { "pressure": 1 },
                  "summary": "Хранитель потратил Перья на давление."
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        Assert.False(root.ContainsKey("afterlifeEntityProgressionOverrides"));

        var profile = Assert.Single(root["profiles"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(1, profile["standardArts"]?["pressure"]?.GetValue<int>());
        Assert.Equal(5, profile["currencies"]?["inkFeathers"]?.GetValue<int>());
        Assert.Equal(3, profile["currencies"]?["lightSparks"]?.GetValue<int>());
        Assert.Equal("shining:6", profile["progressionStrategy"]?["lastAutoProgressionCycleKey"]?.GetValue<string>());

        var ledger = Assert.IsType<JsonArray>(profile["progressionLedger"]);
        var entry = Assert.Single(ledger.OfType<JsonObject>());
        Assert.Equal("gm_override", entry["source"]?.GetValue<string>());
        Assert.Equal("shining:6", entry["cycleKey"]?.GetValue<string>());
        Assert.Equal("Хранитель потратил Перья на давление.", entry["summary"]?.GetValue<string>());
        Assert.Equal(0, entry["income"]?["inkFeathers"]?.GetValue<int>());
        Assert.Equal(2, entry["income"]?["lightSparks"]?.GetValue<int>());
        Assert.Equal(5, entry["spending"]?["inkFeathers"]?.GetValue<int>());
        Assert.Equal(0, entry["spending"]?["lightSparks"]?.GetValue<int>());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_SaturatesEntityProgressionOverrideArithmetic()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            AfterlifeEntityProfileState.StatePath,
            $$"""
            {
              "schemaVersion": 1,
              "profiles": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  "displayName": "Хранитель Зеркал",
                  "realm": "Chaos Sea",
                  "currencies": { "inkFeathers": {{int.MaxValue - 5}}, "lightSparks": 0 },
                  "progression": {
                    "enlightenment": { "experience": {{int.MaxValue - 2}}, "tier": 4 },
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
                  "ledger": []
                }
              ],
              "afterlifeEntityProgressionOverrides": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  "cycleKey": "chaos:overflow",
                  "reason": "GM проверяет верхнюю границу прогрессии.",
                  "currencyDeltas": { "inkFeathers": 10 },
                  "progressionExperienceDeltas": { "enlightenment": 10 },
                  "summary": "Проверка насыщения вместо int overflow."
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        var profile = Assert.Single(root["profiles"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(int.MaxValue, profile["currencies"]?["inkFeathers"]?.GetValue<int>());
        Assert.Equal(int.MaxValue, profile["progression"]?["enlightenment"]?["experience"]?.GetValue<int>());
        Assert.Equal(AfterlifeEntityProfileState.MaxProfileTier, profile["progression"]?["enlightenment"]?["tier"]?.GetValue<int>());
        Assert.False(root.ContainsKey(AfterlifeEntityProfileState.LastInvalidProgressionOverrideProperty));
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

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
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

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        Assert.True(root.ContainsKey("lastInvalidProgressionOverride"));
        Assert.Equal("unknown_special_art", root["lastInvalidProgressionOverrideReason"]?.GetValue<string>());
        var profile = Assert.Single(root["profiles"]!.AsArray().OfType<JsonObject>());
        Assert.Empty(profile["specialArts"]!.AsArray());
        Assert.False(root.ContainsKey(AfterlifeEntityProfileState.ProgressionOverridesProperty));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_PreservesInvalidOverrideMarkerForMissingTargetIdentity()
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
                  "standardArts": { "guard": 0 },
                  "specialArts": [],
                  "customStates": [],
                  "soulDissipationTier": 0,
                  "progressionStrategy": { "strategyId": "strategy_guardian_mirror", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
                  "progressionLedger": [],
                  "ledger": []
                }
              ],
              "afterlifeEntityProgressionOverrides": [
                {
                  "cycleKey": "chaos:9",
                  "reason": "GM override без target.",
                  "summary": "Не должен исчезать no-op.",
                  "currencyDeltas": { "inkFeathers": 10 }
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        Assert.True(root.ContainsKey(AfterlifeEntityProfileState.LastInvalidProgressionOverrideProperty));
        Assert.Equal("missing_target_profile", root[AfterlifeEntityProfileState.LastInvalidProgressionOverrideReasonProperty]?.GetValue<string>());
        Assert.False(root.ContainsKey(AfterlifeEntityProfileState.ProgressionOverridesProperty));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_PreservesInvalidOverrideMarkerForNonObjectOverride()
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
                  "standardArts": { "guard": 0 },
                  "specialArts": [],
                  "customStates": [],
                  "soulDissipationTier": 0,
                  "progressionStrategy": { "strategyId": "strategy_guardian_mirror", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
                  "progressionLedger": [],
                  "ledger": []
                }
              ],
              "afterlifeEntityProgressionOverrides": [
                "not_an_override_object"
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        Assert.True(root.ContainsKey(AfterlifeEntityProfileState.LastInvalidProgressionOverrideProperty));
        Assert.Equal("progression_override_not_object", root[AfterlifeEntityProfileState.LastInvalidProgressionOverrideReasonProperty]?.GetValue<string>());
        Assert.False(root.ContainsKey(AfterlifeEntityProfileState.ProgressionOverridesProperty));
    }

    [Theory]
    [InlineData("\"currencyDeltas\": { }", "invalid_currency_delta")]
    [InlineData("\"currencyDeltas\": { \"inkFeathers\": \"many\" }", "invalid_currency_delta")]
    [InlineData("\"currencyDeltas\": { \"moonCoins\": 5 }", "invalid_currency_delta")]
    [InlineData("\"standardArtTierDeltas\": { }", "invalid_standard_art_delta")]
    [InlineData("\"standardArtTierDeltas\": { \"manuever\": 1 }", "invalid_standard_art_delta")]
    [InlineData("\"specialArtTierDeltas\": { }", "invalid_special_art_delta")]
    [InlineData("\"specialArtTierDeltas\": { \"mirror_guard\": 99 }", "invalid_special_art_delta")]
    [InlineData("\"soulDissipationTierDelta\": 99", "invalid_soul_dissipation_delta")]
    [InlineData("\"progressionExperienceDeltas\": { }", "invalid_progression_delta")]
    [InlineData("\"progressionExperienceDeltas\": { \"enlightenment\": \"much\" }", "invalid_progression_delta")]
    [InlineData("\"progressionExperienceDeltas\": { \"karma\": 5 }", "invalid_progression_delta")]
    public async Task NormalizeAccumulatedStateAsync_PreservesInvalidOverrideMarkerForMalformedDeltas(
        string deltaJson,
        string expectedReason)
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            AfterlifeEntityProfileState.StatePath,
            $$"""
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
                  "specialArts": [
                    {
                      "artId": "mirror_guard",
                      "displayName": "Зеркальный Щит",
                      "ownerActorType": "guardian",
                      "ownerActorId": "guardian_mirror",
                      "baseOperation": "guard",
                      "tier": 1,
                      "costMultiplierPercent": 150,
                      "upgradeCost": { "inkFeathers": 30, "lightSparks": 0 },
                      "canTeachPlayer": true,
                      "effectSummary": "Усиленная защита."
                    }
                  ],
                  "customStates": [],
                  "soulDissipationTier": 0,
                  "progressionStrategy": { "strategyId": "strategy_guardian_mirror", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
                  "ledger": []
                }
              ],
              "afterlifeEntityProgressionOverrides": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  "cycleKey": "chaos:9",
                  "reason": "GM override с malformed delta.",
                  "summary": "Не должен исчезать no-op.",
                  {{deltaJson}}
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        Assert.True(root.ContainsKey(AfterlifeEntityProfileState.LastInvalidProgressionOverrideProperty));
        Assert.Equal(expectedReason, root[AfterlifeEntityProfileState.LastInvalidProgressionOverrideReasonProperty]?.GetValue<string>());
        Assert.False(root.ContainsKey(AfterlifeEntityProfileState.ProgressionOverridesProperty));
    }

    [Theory]
    [InlineData("""
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  "reason": "GM override without cycle.",
                  "summary": "Не должен примениться.",
                  "currencyDeltas": { "inkFeathers": 10 }
                }
                """)]
    [InlineData("""
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  "cycleKey": "chaos:9",
                  "summary": "Не должен примениться.",
                  "currencyDeltas": { "inkFeathers": 10 }
                }
                """)]
    [InlineData("""
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  "cycleKey": "chaos:9",
                  "reason": "GM override without summary.",
                  "currencyDeltas": { "inkFeathers": 10 }
                }
                """)]
    public async Task NormalizeAccumulatedStateAsync_PreservesInvalidOverrideMarkerForMalformedMetadata(
        string overrideJson)
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            AfterlifeEntityProfileState.StatePath,
            $$"""
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
                  "progressionLedger": [],
                  "ledger": []
                }
              ],
              "afterlifeEntityProgressionOverrides": [
                {{overrideJson}}
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        Assert.True(root.ContainsKey(AfterlifeEntityProfileState.LastInvalidProgressionOverrideProperty));
        Assert.Equal("incomplete_progression_override", root[AfterlifeEntityProfileState.LastInvalidProgressionOverrideReasonProperty]?.GetValue<string>());
        Assert.False(root.ContainsKey(AfterlifeEntityProfileState.ProgressionOverridesProperty));

        var profile = Assert.Single(root["profiles"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(100, profile["currencies"]?["inkFeathers"]?.GetValue<int>());
        Assert.Empty(profile["progressionLedger"]!.AsArray());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_PreservesInvalidProfileCommandMarkerForUnknownCustomStateTarget()
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
                  "standardArts": { "guard": 0 },
                  "specialArts": [],
                  "customStates": [],
                  "soulDissipationTier": 0,
                  "progressionStrategy": { "strategyId": "strategy_guardian_mirror", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
                  "ledger": []
                }
              ],
              "afterlifeEntityCustomStateChanges": [
                {
                  "actorType": "resident",
                  "actorId": "resident_missing",
                  "statesToRemove": ["old_state"]
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        Assert.True(root.ContainsKey("lastInvalidProfileCommand"));
        Assert.Equal("unknown_custom_state_target", root["lastInvalidProfileCommandReason"]?.GetValue<string>());
        Assert.False(root.ContainsKey(AfterlifeEntityProfileState.CustomStateChangesProperty));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_PreservesInvalidProfileCommandMarkerForMalformedCustomStateUpsert()
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
                  "standardArts": { "guard": 0 },
                  "specialArts": [],
                  "customStates": [],
                  "soulDissipationTier": 0,
                  "progressionStrategy": { "strategyId": "strategy_guardian_mirror", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
                  "ledger": []
                }
              ],
              "afterlifeEntityCustomStateChanges": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  "statesToAddOrUpdate": ["not_a_state_object"]
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        Assert.True(root.ContainsKey(AfterlifeEntityProfileState.LastInvalidCommandProperty));
        Assert.Equal("custom_state_upsert_not_object", root[AfterlifeEntityProfileState.LastInvalidCommandReasonProperty]?.GetValue<string>());
        Assert.False(root.ContainsKey(AfterlifeEntityProfileState.CustomStateChangesProperty));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_PreservesInvalidProfileCommandMarkerForMalformedCustomStateRemoval()
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
                  "standardArts": { "guard": 0 },
                  "specialArts": [],
                  "customStates": [
                    { "stateId": "oath", "displayName": "Клятва", "description": "Активная клятва.", "intensity": 1 }
                  ],
                  "soulDissipationTier": 0,
                  "progressionStrategy": { "strategyId": "strategy_guardian_mirror", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
                  "ledger": []
                }
              ],
              "afterlifeEntityCustomStateChanges": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  "statesToRemove": [""]
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        Assert.True(root.ContainsKey(AfterlifeEntityProfileState.LastInvalidCommandProperty));
        Assert.Equal("custom_state_remove_invalid_id", root[AfterlifeEntityProfileState.LastInvalidCommandReasonProperty]?.GetValue<string>());
        Assert.False(root.ContainsKey(AfterlifeEntityProfileState.CustomStateChangesProperty));
        var profile = Assert.Single(root["profiles"]!.AsArray().OfType<JsonObject>());
        Assert.Single(profile["customStates"]!.AsArray());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_PreservesInvalidProfileCommandMarkerForNonArrayCustomStateChild()
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
                  "standardArts": { "guard": 0 },
                  "specialArts": [],
                  "customStates": [],
                  "soulDissipationTier": 0,
                  "progressionStrategy": { "strategyId": "strategy_guardian_mirror", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
                  "ledger": []
                }
              ],
              "afterlifeEntityCustomStateChanges": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  "statesToAddOrUpdate": [
                    { "stateId": "oath", "displayName": "Клятва", "description": "Активная клятва.", "intensity": 1 }
                  ],
                  "statesToRemove": { "not": "array" }
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        Assert.True(root.ContainsKey(AfterlifeEntityProfileState.LastInvalidCommandProperty));
        Assert.Equal("custom_state_removals_not_array", root[AfterlifeEntityProfileState.LastInvalidCommandReasonProperty]?.GetValue<string>());
        Assert.False(root.ContainsKey(AfterlifeEntityProfileState.CustomStateChangesProperty));
        var profile = Assert.Single(root["profiles"]!.AsArray().OfType<JsonObject>());
        Assert.Empty(profile["customStates"]!.AsArray());
    }

    [Theory]
    [InlineData("\"statesToRemove\": []")]
    [InlineData("\"statesToAddOrUpdate\": []")]
    [InlineData("\"statesToAddOrUpdate\": [], \"statesToRemove\": []")]
    public async Task NormalizeAccumulatedStateAsync_PreservesInvalidProfileCommandMarkerForEmptyCustomStateChange(
        string customStateCommandChildren)
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            AfterlifeEntityProfileState.StatePath,
            $$"""
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
              ],
              "afterlifeEntityCustomStateChanges": [
                {
                  "actorType": "guardian",
                  "actorId": "guardian_mirror",
                  {{customStateCommandChildren}}
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        Assert.True(root.ContainsKey(AfterlifeEntityProfileState.LastInvalidCommandProperty));
        Assert.Equal("empty_custom_state_change", root[AfterlifeEntityProfileState.LastInvalidCommandReasonProperty]?.GetValue<string>());
        Assert.False(root.ContainsKey(AfterlifeEntityProfileState.CustomStateChangesProperty));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_PreservesInvalidProfileCommandMarkerForNonObjectProfileUpdate()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            AfterlifeEntityProfileState.StatePath,
            """
            {
              "schemaVersion": 1,
              "profiles": [],
              "afterlifeEntityProfileUpdates": [
                "not_a_profile_object"
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        Assert.True(root.ContainsKey(AfterlifeEntityProfileState.LastInvalidCommandProperty));
        Assert.Equal("profile_update_not_object", root[AfterlifeEntityProfileState.LastInvalidCommandReasonProperty]?.GetValue<string>());
        Assert.False(root.ContainsKey(AfterlifeEntityProfileState.UpdateProperty));
    }

    [Theory]
    [InlineData("afterlifeEntityCustomStateChanges", "lastInvalidProfileCommand", "lastInvalidProfileCommandReason", "custom_state_changes_not_array")]
    [InlineData("afterlifeSpecialArtLearningReceipts", "lastInvalidProfileCommand", "lastInvalidProfileCommandReason", "special_art_learning_receipts_not_array")]
    [InlineData("afterlifeEntityProgressionOverrides", "lastInvalidProgressionOverride", "lastInvalidProgressionOverrideReason", "progression_overrides_not_array")]
    public async Task NormalizeAccumulatedStateAsync_PreservesInvalidMarkerForNonArrayProfileCommands(
        string commandProperty,
        string markerProperty,
        string reasonProperty,
        string expectedReason)
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            AfterlifeEntityProfileState.StatePath,
            $$"""
            {
              "schemaVersion": 1,
              "profiles": [],
              "{{commandProperty}}": { "not": "array" }
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        Assert.True(root.ContainsKey(markerProperty));
        Assert.Equal(expectedReason, root[reasonProperty]?.GetValue<string>());
        Assert.False(root.ContainsKey(commandProperty));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_PreservesInvalidProfileCommandMarkerForMalformedLearningReceipt()
    {
        var cases = new (string ReceiptJson, string ExpectedReason)[]
        {
            (
                """
                {
                  "teacherActorType": "guardian",
                  "teacherActorId": "guardian_mirror",
                  "playerActorId": "player_soul",
                  "artId": "mirror_guard",
                  "learnedAtTurn": 31,
                  "trainingConditionSatisfied": true,
                  "roleplayEvidence": "Игрок прошёл сцену обучения.",
                  "summary": "GM признал обучение."
                }
                """,
                "incomplete_special_art_learning_receipt"),
            (
                """
                {
                  "receiptId": "learn_mirror_guard_001",
                  "teacherActorType": "guardian",
                  "teacherActorId": "guardian_mirror",
                  "playerActorId": "player_soul",
                  "artId": "mirror_guard",
                  "learnedAtTurn": 31,
                  "trainingConditionSatisfied": false,
                  "roleplayEvidence": "Игрок прошёл сцену обучения.",
                  "summary": "GM признал обучение."
                }
                """,
                "special_art_learning_condition_not_satisfied"),
            (
                """
                {
                  "receiptId": "learn_mirror_guard_001",
                  "teacherActorType": "guardian",
                  "teacherActorId": "guardian_mirror",
                  "playerActorId": "player_soul",
                  "artId": "mirror_guard",
                  "learnedAtTurn": "now",
                  "trainingConditionSatisfied": true,
                  "roleplayEvidence": "Игрок прошёл сцену обучения.",
                  "summary": "GM признал обучение."
                }
                """,
                "invalid_special_art_learning_turn"),
            (
                """
                {
                  "receiptId": "learn_mirror_guard_001",
                  "teacherActorType": "guardian",
                  "teacherActorId": "guardian_mirror",
                  "playerActorId": "player_soul",
                  "artId": "mirror_guard",
                  "learnedAtTurn": 31,
                  "trainingConditionSatisfied": true,
                  "summary": "GM признал обучение."
                }
                """,
                "incomplete_special_art_learning_receipt")
        };

        foreach (var (receiptJson, expectedReason) in cases)
        {
            var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
            await _fs.WriteFileAtomicAsync(
                AfterlifeEntityProfileState.StatePath,
                $$"""
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
                    {{receiptJson}}
                  ]
                }
                """);

            await normalizer.NormalizeAccumulatedStateAsync();

            var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
            Assert.True(root.ContainsKey(AfterlifeEntityProfileState.LastInvalidCommandProperty));
            Assert.Equal(expectedReason, root[AfterlifeEntityProfileState.LastInvalidCommandReasonProperty]?.GetValue<string>());
            Assert.False(root.ContainsKey(AfterlifeEntityProfileState.SpecialArtLearningReceiptsProperty));

            var player = root["profiles"]!.AsArray()
                .OfType<JsonObject>()
                .Single(profile => profile["actorType"]?.GetValue<string>() == "player_soul");
            Assert.Empty(player["specialArts"]!.AsArray());
        }
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_PreservesInvalidProfileCommandMarkerForUnknownLearningAuthority()
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
                  "specialArts": [],
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
                  "receiptId": "learn_missing_art",
                  "teacherActorType": "guardian",
                  "teacherActorId": "guardian_mirror",
                  "playerActorId": "player_soul",
                  "artId": "missing_art",
                  "learnedAtTurn": 31,
                  "trainingConditionSatisfied": true,
                  "roleplayEvidence": "Игрок прошёл сцену обучения.",
                  "summary": "GM признал обучение."
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        Assert.True(root.ContainsKey("lastInvalidProfileCommand"));
        Assert.Equal("unknown_special_art_learning_art", root["lastInvalidProfileCommandReason"]?.GetValue<string>());
        Assert.False(root.ContainsKey(AfterlifeEntityProfileState.SpecialArtLearningReceiptsProperty));
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

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
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
        Assert.Equal(0, learnedArt["tier"]?.GetValue<int>());

        var ledger = Assert.IsType<JsonArray>(player["ledger"]);
        var entry = Assert.Single(ledger.OfType<JsonObject>());
        Assert.Equal("learn_special_art", entry["reason"]?.GetValue<string>());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_RejectsSpecialArtLearningReceiptWithNonZeroInitialTier()
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
                  "summary": "Игрок изучил Зеркальную Защиту.",
                  "initialTier": 5
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        Assert.True(root.ContainsKey("lastInvalidProfileCommand"));
        Assert.Equal("invalid_special_art_learning_initial_tier", root["lastInvalidProfileCommandReason"]?.GetValue<string>());
        Assert.False(root.ContainsKey(AfterlifeEntityProfileState.SpecialArtLearningReceiptsProperty));

        var player = root["profiles"]!.AsArray()
            .OfType<JsonObject>()
            .Single(profile => profile["actorType"]?.GetValue<string>() == "player_soul");
        Assert.Empty(player["specialArts"]!.AsArray());
    }

    private async Task CorrelateAfterlifeProgressionReportWithTurnRequestAsync()
    {
        var reportRoot = JsonNode.Parse(
            await _fs.ReadFileAsync(ProgressionScheduleService.ReportPath) ?? "{}")!.AsObject();
        var report = reportRoot["progressionProcessingReport"] as JsonObject ?? reportRoot;
        var sessionId = GetTestString(report["sessionId"]) ?? "session_entity_progression";
        var requestId = GetTestString(report["requestId"]) ?? "request_entity_progression";
        var turnNumber = GetTestInt(report["turnNumber"], 77);

        report["sessionId"] = sessionId;
        report["requestId"] = requestId;
        report["turnNumber"] = turnNumber;

        await _fs.WriteFileAtomicAsync(
            "input/turn_request.json",
            $$"""
            {
              "sessionId": "{{sessionId}}",
              "requestId": "{{requestId}}",
              "turnNumber": {{turnNumber}}
            }
            """);
        await _fs.WriteFileAtomicAsync(ProgressionScheduleService.ReportPath, reportRoot.ToJsonString());
    }

    private static string? GetTestString(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var stringValue))
                return stringValue;
        }

        return null;
    }

    private static int GetTestInt(JsonNode? node, int defaultValue)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var intValue))
                return intValue;
            if (value.TryGetValue<long>(out var longValue) &&
                longValue >= int.MinValue &&
                longValue <= int.MaxValue)
            {
                return (int)longValue;
            }
        }

        return defaultValue;
    }
}
