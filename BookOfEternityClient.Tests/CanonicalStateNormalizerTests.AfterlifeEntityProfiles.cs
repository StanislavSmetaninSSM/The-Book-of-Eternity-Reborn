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
                      "baseOperation": "guard",
                      "tier": 1,
                      "costMultiplierPercent": 150,
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
}
