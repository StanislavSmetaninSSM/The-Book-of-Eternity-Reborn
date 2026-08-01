using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests
{
    [Fact]
    public async Task NormalizeAccumulatedStateAsync_GuardianQuestProgressUpdateMarksReadyToTurnIn()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 37 }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия",
              "questManagement": {
                "availableQuests": [],
                "activeQuests": [
                  {
                    "questId": "quest_azalia_rare_ore_echo",
                    "questName": "Серебряная руда сна",
                    "status": "active",
                    "difficulty": "normal"
                  }
                ],
                "completedQuests": []
              },
              "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_azalia",
            "canonicalName": "Азалия",
            "questManagement": {
              "availableQuests": [],
              "activeQuests": [
                {
                  "questId": "quest_azalia_rare_ore_echo",
                  "questName": "Серебряная руда сна",
                  "status": "active",
                  "difficulty": "normal"
                }
              ],
              "completedQuests": []
            },
            "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          },
          "guardianQuestProgressUpdates": [
            {
              "guardianId": "guardian_azalia",
              "questId": "quest_azalia_rare_ore_echo",
              "status": "ready_to_turn_in",
              "progressSummary": "Игрок нашёл редкую руду; предмет остался в смертном мире.",
              "readyToTurnInEvidence": {
                "itemEcho": {
                  "mortalItemName": "Серебряная руда сна",
                  "proofKind": "memory_imprint"
                }
              },
              "turnInRequirement": "Вернуться к Хранителю с духовным слепком."
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        var root = JsonNode.Parse(guardiansJson!)!.AsObject();
        var quest = root["guardians"]![0]!["questManagement"]!["activeQuests"]![0]!.AsObject();

        Assert.True(root.ContainsKey("guardianQuestProgressUpdates"));
        Assert.Equal("ready_to_turn_in", quest["status"]!.GetValue<string>());
        Assert.Equal(37, quest["readyToTurnInAtTurn"]!.GetValue<int>());
        Assert.Contains("редкую руду", quest["progressSummary"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(
            "Серебряная руда сна",
            quest["readyToTurnInEvidence"]!["itemEcho"]!["mortalItemName"]!.GetValue<string>());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_GuardianQuestProgressUpdateWithoutStatusKeepsRawSurface()
    {
        await WriteGuardianQuestProgressUpdateScenarioAsync("""
              "guardianId": "guardian_azalia",
              "questId": "quest_azalia_rare_ore_echo",
              "progressSummary": "Игрок нашёл редкую руду, но статус прогресса не указан."
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        var root = JsonNode.Parse(guardiansJson!)!.AsObject();
        var quest = root["guardians"]![0]!["questManagement"]!["activeQuests"]![0]!.AsObject();

        Assert.True(root.ContainsKey("guardianQuestProgressUpdates"));
        Assert.Equal("active", quest["status"]!.GetValue<string>());
        Assert.Null(quest["progressSummary"]);
        Assert.Null(quest["updatedAtTurn"]);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_GuardianQuestProgressUpdateWithInvalidStatusKeepsRawSurface()
    {
        await WriteGuardianQuestProgressUpdateScenarioAsync("""
              "guardianId": "guardian_azalia",
              "questId": "quest_azalia_rare_ore_echo",
              "status": "completed",
              "progressSummary": "Игрок нашёл редкую руду, но статус не поддерживается progress update."
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        var root = JsonNode.Parse(guardiansJson!)!.AsObject();
        var quest = root["guardians"]![0]!["questManagement"]!["activeQuests"]![0]!.AsObject();

        Assert.True(root.ContainsKey("guardianQuestProgressUpdates"));
        Assert.Equal("active", quest["status"]!.GetValue<string>());
        Assert.Null(quest["progressSummary"]);
        Assert.Null(quest["updatedAtTurn"]);
    }

    private async Task WriteGuardianQuestProgressUpdateScenarioAsync(string updateBody)
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 37 }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", $$"""
        {
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия",
              "questManagement": {
                "availableQuests": [],
                "activeQuests": [
                  {
                    "questId": "quest_azalia_rare_ore_echo",
                    "questName": "Серебряная руда сна",
                    "status": "active",
                    "difficulty": "normal"
                  }
                ],
                "completedQuests": []
              },
              "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianQuestProgressUpdates": [
            {
        {{updateBody}}
            }
          ]
        }
        """);
    }
}
