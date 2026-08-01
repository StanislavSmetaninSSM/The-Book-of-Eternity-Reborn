using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests
{
    [Fact]
    public void NormalizerBackupInputFiles_IncludesSarefMainStoryState()
    {
        Assert.Contains(
            SarefMainStoryState.StatePath,
            CanonicalStateNormalizer.NormalizerBackupInputFiles,
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_SarefMainStoryUpdateWrapper_ProjectsAgainstBackupBaseline()
    {
        var backupPath = "test_backups/pre_saref_story.json";
        await _fs.WriteFileAtomicAsync(backupPath, BuildSarefRouteBaseline());
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, """
        {
          "sarefMainStoryUpdate": {
            "mode": "reveal_wings",
            "requestId": "saref_wings_infiltration:42",
            "resolvedAtTurn": 43,
            "routeSafety": "safe",
            "entryMode": "safe_infiltration",
            "summary": "Игрок нашел внешний круг Крыльев Ангелов.",
            "factionLinks": {
              "wingsFactionId": "wings_of_angels"
            }
          }
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [SarefMainStoryState.StatePath] = backupPath
        });

        var raw = await _fs.ReadFileAsync(SarefMainStoryState.StatePath);
        var root = JsonNode.Parse(raw!)!.AsObject();

        Assert.False(root.ContainsKey(SarefMainStoryState.ResponseField));
        Assert.Equal("wings_revealed", root["revealStage"]!.GetValue<string>());
        Assert.Equal("revealed", root["wingsInfiltration"]!["status"]!.GetValue<string>());
        Assert.Equal("saref_wings_infiltration:42", root["wingsInfiltration"]!["requestId"]!.GetValue<string>());
        Assert.Equal(43, root["wingsInfiltration"]!["resolvedAtTurn"]!.GetValue<int>());
        Assert.Equal("revealed", root["factionLinks"]!["visibility"]!.GetValue<string>());
        Assert.Equal("wings_of_angels", root["factionLinks"]!["wingsFactionId"]!.GetValue<string>());
        Assert.Equal(4, root["sarefRevelations"]!.AsArray().Count);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_SarefMemorySceneUpdateWrapper_ProjectsQuestClosureAgainstBackupBaseline()
    {
        var backupPath = "test_backups/pre_saref_memory_scene.json";
        await _fs.WriteFileAtomicAsync(backupPath, BuildSarefRouteBaseline());
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Chaos Sea",
          "pendingMemoryLegacy": null
        }
        """);
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, """
        {
          "sarefMainStoryUpdate": {
            "mode": "record_memory_scene",
            "memoryScene": {
              "sceneId": "memory_scene_azalia_q4",
              "status": "completed",
              "layer": "Воспоминание",
              "guardianId": "azalia",
              "questId": "azalia_saref_q4",
              "questOrdinal": 4,
              "role": {
                "roleId": "azalia_white_lodge_witness",
                "displayName": "Свидетель ложи",
                "summary": "Игрок действует через роль свидетеля старого предательства Азалии."
              },
              "boundaries": [
                { "boundaryId": "past_is_fixed", "summary": "Сареф уже вошел в ложу; это нельзя отменить." }
              ],
              "abilities": [
                { "abilityId": "read_oath", "name": "Прочитать клятву", "summary": "Увидеть скрытую цену белых перьев." }
              ],
              "requiredStoryNodes": [
                { "nodeId": "enter_lodge", "status": "completed", "summary": "Игрок вошел в ложу белых перьев." }
              ],
              "successCondition": {
                "conditionId": "truth_recognized",
                "summary": "Игрок распознал связь ложи с Крыльями Ангелов.",
                "satisfied": true
              },
              "closureTarget": {
                "guardianId": "azalia",
                "questId": "azalia_saref_q4",
                "questOrdinal": 4,
                "revelationId": "rev_azalia_faction",
                "advantageId": "adv_azalia_false_loyalty"
              },
              "resolvedAtTurn": 44,
              "resolutionSummary": "Воспоминание завершено, Азалия получила правду о ложе белых перьев."
            },
            "guardianQuestline": {
              "guardianId": "azalia",
              "questStates": [
                {
                  "questOrdinal": 4,
                  "status": "completed",
                  "questId": "azalia_saref_q4",
                  "completedAtTurn": 44,
                  "memorySceneProof": {
                    "sceneId": "memory_scene_azalia_q4",
                    "layer": "Воспоминание",
                    "roleId": "azalia_white_lodge_witness",
                    "guardianId": "azalia",
                    "questId": "azalia_saref_q4",
                    "questOrdinal": 4,
                    "completedAtTurn": 44,
                    "successConditionSatisfied": true,
                    "summary": "Игрок прошел роль свидетеля и восстановил правду о ложе белых перьев."
                  }
                }
              ]
            },
            "sarefRevelation": {
              "revelationId": "rev_azalia_faction",
              "category": "faction",
              "sourceGuardianId": "azalia",
              "sourceQuestId": "azalia_saref_q4",
              "sourceQuestOrdinal": 4,
              "revealedAtTurn": 44
            },
            "sarefAdvantage": {
              "advantageId": "adv_azalia_false_loyalty",
              "sourceGuardianId": "azalia",
              "sourceQuestId": "azalia_saref_q4",
              "sourceQuestOrdinal": 4,
              "state": "available",
              "applicableScenes": [ "wings_infiltration" ],
              "summary": "Можно выдать себя за полезного перебежчика.",
              "unlockedAtTurn": 44
            }
          }
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [SarefMainStoryState.StatePath] = backupPath
        });

        var raw = await _fs.ReadFileAsync(SarefMainStoryState.StatePath);
        var root = JsonNode.Parse(raw!)!.AsObject();

        Assert.False(root.ContainsKey(SarefMainStoryState.ResponseField));
        Assert.Equal("completed", root["memoryScene"]!["status"]!.GetValue<string>());
        Assert.Equal("Воспоминание", root["memoryScene"]!["layer"]!.GetValue<string>());
        var questline = Assert.IsType<JsonObject>(Assert.Single(root["guardianQuestlines"]!.AsArray()));
        var questFour = Assert.IsType<JsonObject>(questline["questStates"]!.AsArray().Single(node =>
            node is JsonObject quest && quest["questOrdinal"]?.GetValue<int>() == 4));
        Assert.Equal("memory_scene_azalia_q4", questFour["memorySceneProof"]!["sceneId"]!.GetValue<string>());
        Assert.Contains(root["sarefRevelations"]!.AsArray(), node =>
            node is JsonObject revelation &&
            revelation["revelationId"]?.GetValue<string>() == "rev_azalia_faction");
        Assert.Contains(root["sarefAdvantages"]!.AsArray(), node =>
            node is JsonObject advantage &&
            advantage["advantageId"]?.GetValue<string>() == "adv_azalia_false_loyalty");
        var soulRaw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        var soulRoot = JsonNode.Parse(soulRaw!)!.AsObject();
        Assert.Null(soulRoot["pendingMemoryLegacy"]);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_SarefDefeatOutcomeUpdateWrapper_ProjectsAgainstBackupBaseline()
    {
        var backupPath = "test_backups/pre_saref_story_defeat.json";
        await _fs.WriteFileAtomicAsync(backupPath, BuildSarefRouteBaseline());
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, """
        {
          "sarefMainStoryUpdate": {
            "mode": "record_defeat_outcome",
            "resolvedAtTurn": 91,
            "defeatOutcome": {
              "outcomeId": "saref_defeat_forced_oath_001",
              "outcomeType": "forced_oath",
              "sceneType": "saref_confrontation",
              "oathId": "saref_oath_001",
              "summary": "Сареф принудил душу к клятве после поражения.",
              "gmMotivation": "Сареф хочет использовать игрока как связанную фигуру."
            },
            "playerOathState": {
              "state": "oathbound",
              "oathId": "saref_oath_001",
              "summary": "Игрок связан клятвой Сарефа."
            }
          }
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [SarefMainStoryState.StatePath] = backupPath
        });

        var raw = await _fs.ReadFileAsync(SarefMainStoryState.StatePath);
        var root = JsonNode.Parse(raw!)!.AsObject();

        Assert.False(root.ContainsKey(SarefMainStoryState.ResponseField));
        var outcome = Assert.IsType<JsonObject>(Assert.Single(root["defeatOutcomes"]!.AsArray()));
        Assert.Equal("saref_defeat_forced_oath_001", outcome["outcomeId"]!.GetValue<string>());
        Assert.Equal("forced_oath", outcome["outcomeType"]!.GetValue<string>());
        Assert.Equal(91, outcome["resolvedAtTurn"]!.GetValue<int>());
        Assert.Equal("oathbound", root["playerOathState"]!["state"]!.GetValue<string>());
    }

    private static string BuildSarefRouteBaseline() => """
    {
      "schemaVersion": 1,
      "revealStage": "name_revealed",
      "guardianQuestlines": [
        {
          "guardianId": "azalia",
          "questStates": [
            { "questOrdinal": 1, "status": "completed", "questId": "azalia_saref_q1" },
            { "questOrdinal": 2, "status": "completed", "questId": "azalia_saref_q2" },
            { "questOrdinal": 3, "status": "completed", "questId": "azalia_saref_q3" },
            { "questOrdinal": 4, "status": "completed", "questId": "azalia_saref_q4" }
          ]
        }
      ],
      "latentTraces": [],
      "sarefRevelations": [
        { "revelationId": "rev_identity", "category": "identity", "sourceGuardianId": "azalia", "sourceQuestId": "azalia_saref_q4", "sourceQuestOrdinal": 4, "revealedAtTurn": 50 },
        { "revelationId": "rev_method", "category": "method", "sourceGuardianId": "azalia", "sourceQuestId": "azalia_saref_q4", "sourceQuestOrdinal": 4, "revealedAtTurn": 51 },
        { "revelationId": "rev_faction", "category": "faction", "sourceGuardianId": "azalia", "sourceQuestId": "azalia_saref_q4", "sourceQuestOrdinal": 4, "revealedAtTurn": 52 },
        { "revelationId": "rev_path", "category": "path", "sourceGuardianId": "azalia", "sourceQuestId": "azalia_saref_q4", "sourceQuestOrdinal": 4, "revealedAtTurn": 53 }
      ],
      "sarefAdvantages": [],
      "sarefAdvantageUses": [],
      "wingsInfiltration": null,
      "factionLinks": { "visibility": "hidden", "wingsFactionId": null },
      "finalConfrontation": null,
      "defeatOutcomes": [],
      "endings": [],
      "playerOathState": null,
      "sarefPersonalBond": null
    }
    """;
}
