using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GuardianSystemRegressionTests
{    [Fact]
    public async Task ValidateGameState_MortalGuardianQuestProgressReadyToTurnInUsesNonPhysicalEvidence()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 2
        }
        """);
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_mortal_guardian_quest_progress.json",
            """
            {
              "soulName": "Тестовая Душа",
              "currentRealm": "Mortal World",
              "currentIncarnation": 2
            }
            """);
        await SnapshotCurrentChaosSeaLoreForMortalTurnAsync("ready_to_turn_in");

        const string preTurnGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия",
              "abodePower": { "currentPower": 10, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "questManagement": {
                "availableQuests": [],
                "activeQuests": [
                  {
                    "questId": "quest_azalia_rare_ore_echo",
                    "questName": "Серебряная руда сна",
                    "status": "active",
                    "difficulty": "normal"
                  },
                  {
                    "questId": "quest_azalia_legacy_statusless",
                    "questName": "Legacy statusless quest",
                    "difficulty": "easy"
                  }
                ],
                "completedQuests": []
              }
            }
          ]
        }
        """;
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_mortal_guardian_quest_progress.json",
            NormalizeGuardianStateJson(preTurnGuardiansJson));
        await EnsureEmptyCurrentGuardianProjectTrackerAndPowerJournalAsync();
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson("""
        {
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия",
              "abodePower": { "currentPower": 10, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "questManagement": {
                "availableQuests": [],
                "activeQuests": [
                  {
                    "questId": "quest_azalia_rare_ore_echo",
                    "questName": "Серебряная руда сна",
                    "status": "active",
                    "difficulty": "normal"
                  },
                  {
                    "questId": "quest_azalia_legacy_statusless",
                    "questName": "Legacy statusless quest",
                    "difficulty": "easy"
                  }
                ],
                "completedQuests": []
              }
            }
          ],
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
                },
                "lifeEventEvidence": "Событие поиска подтверждено памятью текущей жизни."
              },
              "turnInRequirement": "Вернуться к Хранителю с духовным слепком."
            }
          ],
          "_lastUpdated": "2026-05-12T00:00:00Z"
        }
        """));

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["game_state/meta/guardians.json"] = "test_backups/preturn_guardians_mortal_guardian_quest_progress.json",
            [GuardianProjectState.TrackerPath] = "test_backups/preturn_guardian_tracker_auto_baseline.json"
        });

        var normalizedGuardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        var normalizedGuardiansRoot = JsonNode.Parse(normalizedGuardiansJson!)!.AsObject();
        Assert.True(normalizedGuardiansRoot.ContainsKey(GuardianProjectState.QuestProgressUpdatesProperty));
        Assert.Equal(
            "ready_to_turn_in",
            normalizedGuardiansRoot["guardians"]![0]!["questManagement"]!["activeQuests"]![0]!["status"]!.GetValue<string>());
        Assert.False(normalizedGuardiansRoot["guardians"]![0]!["questManagement"]!["activeQuests"]![1]!.AsObject().ContainsKey("status"));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "realm_segregation_violation", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_ready_to_turn_in_missing_evidence", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_ready_to_turn_in_physical_item_transfer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_MortalGuardianQuestProgressPartialCommandPreservesUnchangedFields()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 2
        }
        """);
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_mortal_guardian_quest_progress_partial.json",
            """
            {
              "soulName": "Тестовая Душа",
              "currentRealm": "Mortal World",
              "currentIncarnation": 2
            }
            """);
        await SnapshotCurrentChaosSeaLoreForMortalTurnAsync("partial_command");

        const string preTurnGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия",
              "abodePower": { "currentPower": 10, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "questManagement": {
                "availableQuests": [],
                "activeQuests": [
                  {
                    "questId": "quest_azalia_rare_ore_echo",
                    "questName": "Серебряная руда сна",
                    "status": "active",
                    "difficulty": "normal",
                    "progressSummary": "Игрок уже нашёл первый след руды.",
                    "objectiveState": {
                      "knownVein": "northern_mine"
                    }
                  }
                ],
                "completedQuests": []
              }
            }
          ]
        }
        """;
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_mortal_guardian_quest_progress_partial.json",
            NormalizeGuardianStateJson(preTurnGuardiansJson));
        await EnsureEmptyCurrentGuardianProjectTrackerAndPowerJournalAsync();
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson("""
        {
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия",
              "abodePower": { "currentPower": 10, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "questManagement": {
                "availableQuests": [],
                "activeQuests": [
                  {
                    "questId": "quest_azalia_rare_ore_echo",
                    "questName": "Серебряная руда сна",
                    "status": "active",
                    "difficulty": "normal",
                    "progressSummary": "Игрок уже нашёл первый след руды.",
                    "objectiveState": {
                      "knownVein": "northern_mine"
                    }
                  }
                ],
                "completedQuests": []
              }
            }
          ],
          "guardianQuestProgressUpdates": [
            {
              "guardianId": "guardian_azalia",
              "questId": "quest_azalia_rare_ore_echo",
              "status": "ready_to_turn_in",
              "readyToTurnInEvidence": {
                "itemEcho": {
                  "mortalItemName": "Серебряная руда сна",
                  "proofKind": "memory_imprint"
                }
              }
            }
          ],
          "_lastUpdated": "2026-05-12T00:00:00Z"
        }
        """));

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["game_state/meta/guardians.json"] = "test_backups/preturn_guardians_mortal_guardian_quest_progress_partial.json",
            [GuardianProjectState.TrackerPath] = "test_backups/preturn_guardian_tracker_auto_baseline.json"
        });

        var normalizedGuardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        var normalizedQuest = JsonNode.Parse(normalizedGuardiansJson!)!
            ["guardians"]![0]!["questManagement"]!["activeQuests"]![0]!.AsObject();
        Assert.Equal("ready_to_turn_in", normalizedQuest["status"]!.GetValue<string>());
        Assert.Equal("Игрок уже нашёл первый след руды.", normalizedQuest["progressSummary"]!.GetValue<string>());
        Assert.Equal("northern_mine", normalizedQuest["objectiveState"]!["knownVein"]!.GetValue<string>());
        Assert.True(normalizedQuest.ContainsKey("updatedAtTurn"));
        Assert.True(normalizedQuest.ContainsKey("updatedAtUtc"));
        Assert.True(normalizedQuest.ContainsKey("readyToTurnInAtTurn"));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "realm_segregation_violation", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_MortalGuardianQuestProgressDirectMaterializedDeltaRequiresCommand()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 2
        }
        """);
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_mortal_guardian_quest_progress_direct_delta.json",
            """
            {
              "soulName": "Тестовая Душа",
              "currentRealm": "Mortal World",
              "currentIncarnation": 2
            }
            """);

        const string preTurnGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия",
              "abodePower": { "currentPower": 10, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
              }
            }
          ]
        }
        """;
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_mortal_guardian_quest_progress_direct_delta.json",
            NormalizeGuardianStateJson(preTurnGuardiansJson));

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson("""
        {
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия",
              "abodePower": { "currentPower": 10, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "questManagement": {
                "availableQuests": [],
                "activeQuests": [
                  {
                    "questId": "quest_azalia_rare_ore_echo",
                    "questName": "Серебряная руда сна",
                    "status": "ready_to_turn_in",
                    "difficulty": "normal",
                    "progressSummary": "Direct materialized delta without guardianQuestProgressUpdates.",
                    "readyToTurnInEvidence": {
                      "itemEcho": {
                        "mortalItemName": "Серебряная руда сна",
                        "proofKind": "memory_imprint"
                      }
                    }
                  }
                ],
                "completedQuests": []
              }
            }
          ],
          "_lastUpdated": "2026-05-12T00:00:00Z"
        }
        """));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "realm_segregation_violation", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "guardian_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_MortalGuardianQuestProgressMaterializedDeltaMustMatchCommand()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 2
        }
        """);
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_mortal_guardian_quest_progress_command_mismatch.json",
            """
            {
              "soulName": "Тестовая Душа",
              "currentRealm": "Mortal World",
              "currentIncarnation": 2
            }
            """);

        const string preTurnGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия",
              "abodePower": { "currentPower": 10, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
              }
            }
          ]
        }
        """;
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_mortal_guardian_quest_progress_command_mismatch.json",
            NormalizeGuardianStateJson(preTurnGuardiansJson));

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson("""
        {
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия",
              "abodePower": { "currentPower": 10, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "questManagement": {
                "availableQuests": [],
                "activeQuests": [
                  {
                    "questId": "quest_azalia_rare_ore_echo",
                    "questName": "Серебряная руда сна",
                    "status": "failed",
                    "difficulty": "normal",
                    "progressSummary": "Materialized state says the quest failed."
                  }
                ],
                "completedQuests": []
              }
            }
          ],
          "guardianQuestProgressUpdates": [
            {
              "guardianId": "guardian_azalia",
              "questId": "quest_azalia_rare_ore_echo",
              "status": "ready_to_turn_in",
              "progressSummary": "Command says the quest is ready to turn in.",
              "readyToTurnInEvidence": {
                "itemEcho": {
                  "mortalItemName": "Серебряная руда сна",
                  "proofKind": "memory_imprint"
                }
              }
            }
          ],
          "_lastUpdated": "2026-05-12T00:00:00Z"
        }
        """));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "realm_segregation_violation", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "guardian_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_MortalGuardianQuestProgressWithoutStatusFailsAfterNormalization()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 2
        }
        """);
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_mortal_guardian_quest_progress_missing_status.json",
            """
            {
              "soulName": "Тестовая Душа",
              "currentRealm": "Mortal World",
              "currentIncarnation": 2
            }
            """);

        const string preTurnGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия",
              "abodePower": { "currentPower": 10, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
              }
            }
          ]
        }
        """;
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_mortal_guardian_quest_progress_missing_status.json",
            NormalizeGuardianStateJson(preTurnGuardiansJson));

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson("""
        {
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия",
              "abodePower": { "currentPower": 10, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
              }
            }
          ],
          "guardianQuestProgressUpdates": [
            {
              "guardianId": "guardian_azalia",
              "questId": "quest_azalia_rare_ore_echo",
              "progressSummary": "Игрок нашёл редкую руду; malformed update не указал обязательный status."
            }
          ],
          "_lastUpdated": "2026-05-12T00:00:00Z"
        }
        """));

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["game_state/meta/guardians.json"] = "test_backups/preturn_guardians_mortal_guardian_quest_progress_missing_status.json"
        });

        var normalizedGuardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        var normalizedGuardiansRoot = JsonNode.Parse(normalizedGuardiansJson!)!.AsObject();
        var normalizedQuest = normalizedGuardiansRoot["guardians"]![0]!["questManagement"]!["activeQuests"]![0]!.AsObject();
        Assert.True(normalizedGuardiansRoot.ContainsKey(GuardianProjectState.QuestProgressUpdatesProperty));
        Assert.Equal("active", normalizedQuest["status"]!.GetValue<string>());
        Assert.Null(normalizedQuest["progressSummary"]);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "missing_required_string", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".guardianQuestProgressUpdates[0].status", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_AfterlifeGuardianQuestProgressStateDeltaRequiresExplicitAuthority()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 2
        }
        """);
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_afterlife_guardian_quest_progress.json",
            """
            {
              "soulName": "Тестовая Душа",
              "currentRealm": "Chaos Sea",
              "currentIncarnation": 2
            }
            """);

        const string preTurnGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия",
              "abodePower": { "currentPower": 10, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
              }
            }
          ]
        }
        """;
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_afterlife_guardian_quest_progress.json",
            NormalizeGuardianStateJson(preTurnGuardiansJson));
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson("""
        {
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия",
              "abodePower": { "currentPower": 10, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "questManagement": {
                "availableQuests": [],
                "activeQuests": [
                  {
                    "questId": "quest_azalia_rare_ore_echo",
                    "questName": "Серебряная руда сна",
                    "status": "ready_to_turn_in",
                    "difficulty": "normal",
                    "progressSummary": "Afterlife raw state tried to progress a Mortal quest.",
                    "readyToTurnInEvidence": {
                      "itemEcho": {
                        "mortalItemName": "Серебряная руда сна",
                        "proofKind": "memory_imprint"
                      }
                    }
                  }
                ],
                "completedQuests": []
              }
            }
          ]
        }
        """));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_GuardianQuestReadyToTurnInRejectsPhysicalMortalItem()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
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
                    "status": "ready_to_turn_in",
                    "difficulty": "normal",
                    "readyToTurnInEvidence": {
                      "physicalItem": { "itemId": "ore_001" },
                      "itemEcho": "Слепок руды"
                    }
                  }
                ],
                "completedQuests": []
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_ready_to_turn_in_physical_item_transfer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_GuardianQuestProgressActiveRejectsPhysicalEvidence()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
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
              }
            }
          ],
          "guardianQuestProgressUpdates": [
            {
              "guardianId": "guardian_azalia",
              "questId": "quest_azalia_rare_ore_echo",
              "status": "active",
              "progressSummary": "Игрок нашёл зацепку, но предмет ещё не является turn-in proof.",
              "readyToTurnInEvidence": {
                "physicalItem": { "itemId": "ore_001" }
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_ready_to_turn_in_physical_item_transfer", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".guardianQuestProgressUpdates[0].readyToTurnInEvidence.physicalItem", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_GuardianQuestReadyToTurnInRejectsNestedTransferredItemId()
    {
        await AssertReadyToTurnInEvidenceRejectedAsync(
            """
            {
              "itemEcho": {
                "mortalItemName": "Серебряная руда сна",
                "proofKind": "memory_imprint",
                "transferredItemId": "ore_001"
              }
            }
            """,
            ".readyToTurnInEvidence.itemEcho.transferredItemId");
    }

    [Fact]
    public async Task ValidateGameState_GuardianQuestFailedRejectsDeepNestedInventoryItem()
    {
        await AssertReadyToTurnInEvidenceRejectedAsync(
            """
            {
              "itemEcho": {
                "mortalItemName": "Серебряная руда сна",
                "details": {
                  "inventoryItem": {
                    "itemId": "ore_001"
                  }
                }
              }
            }
            """,
            ".readyToTurnInEvidence.itemEcho.details.inventoryItem",
            "failed");
    }

    [Fact]
    public async Task ValidateGameState_GuardianQuestActiveAllowsSafeOptionalEvidence()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
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
                    "difficulty": "normal",
                    "readyToTurnInEvidence": {
                      "itemEcho": {
                        "mortalItemName": "Серебряная руда сна",
                        "proofKind": "memory_imprint"
                      }
                    }
                  }
                ],
                "completedQuests": []
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_ready_to_turn_in_physical_item_transfer", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_ready_to_turn_in_missing_evidence", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_ready_to_turn_in_evidence_missing_proof", StringComparison.OrdinalIgnoreCase));
    }

    private async Task AssertReadyToTurnInEvidenceRejectedAsync(
        string evidenceJson,
        string expectedPathSuffix,
        string questStatus = "ready_to_turn_in")
    {
        await WriteRawAsync("game_state/meta/guardians.json", $$"""
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
                    "status": "{{questStatus}}",
                    "difficulty": "normal",
                    "readyToTurnInEvidence": {{evidenceJson}}
                  }
                ],
                "completedQuests": []
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_ready_to_turn_in_physical_item_transfer", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(expectedPathSuffix, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_CanonicalReadyToTurnInQuestRequiresEvidence()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
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
                    "status": "ready_to_turn_in",
                    "difficulty": "normal"
                  }
                ],
                "completedQuests": []
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_ready_to_turn_in_missing_evidence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_CompleteQuestRejectsExplicitActiveQuestBeforeTurnIn()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "UpdateGuardians": [
            {
              "command": "completeQuest",
              "guardianId": "guardian_azalia",
              "questId": "quest_azalia_rare_ore_echo",
              "outcome": "success"
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_complete_active_not_ready.json",
            NormalizeGuardianStateJson("""
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
                  }
                }
              ]
            }
            """));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_complete_quest_not_ready_to_turn_in", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("success")]
    [InlineData("failure")]
    [InlineData("partial")]
    public async Task ValidateGameState_CompleteQuestRejectsAvailableOnlyQuest(string outcome)
    {
        await WriteRawAsync("game_state/meta/guardians.json", $$"""
        {
          "UpdateGuardians": [
            {
              "command": "completeQuest",
              "guardianId": "guardian_azalia",
              "questId": "quest_azalia_offered_only",
              "outcome": "{{outcome}}"
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            $"test_backups/preturn_guardians_complete_available_only_{outcome}.json",
            NormalizeGuardianStateJson("""
            {
              "guardians": [
                {
                  "guardianId": "guardian_azalia",
                  "canonicalName": "Азалия",
                  "questManagement": {
                    "availableQuests": [
                      {
                        "questId": "quest_azalia_offered_only",
                        "questName": "Предложение на будущую жизнь",
                        "origin": "guardian_baseline_mortal_life_hook",
                        "difficulty": "easy"
                      }
                    ],
                    "activeQuests": [],
                    "completedQuests": []
                  }
                }
              ]
            }
            """));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_complete_quest_not_active", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_complete_quest_unknown_quest_id", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("ready_to_turn_in", "success", false)]
    [InlineData("failed", "failure", false)]
    [InlineData("failed", "partial", false)]
    [InlineData("expired", "failure", false)]
    [InlineData("expired", "partial", false)]
    [InlineData("failed", "success", true)]
    [InlineData("expired", "success", true)]
    [InlineData("active", "failure", true)]
    public async Task ValidateGameState_CompleteQuestStatusMustMatchOutcome(
        string questStatus,
        string outcome,
        bool shouldRejectStatusOutcome)
    {
        await WriteRawAsync("game_state/meta/guardians.json", $$"""
        {
          "UpdateGuardians": [
            {
              "command": "completeQuest",
              "guardianId": "guardian_azalia",
              "questId": "quest_azalia_rare_ore_echo",
              "outcome": "{{outcome}}"
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            $"test_backups/preturn_guardians_complete_{questStatus}_{outcome}.json",
            NormalizeGuardianStateJson($$"""
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
                        "status": "{{questStatus}}",
                        "difficulty": "normal",
                        "readyToTurnInEvidence": {
                          "itemEcho": "Слепок руды"
                        }
                      }
                    ],
                    "completedQuests": []
                  }
                }
              ]
            }
            """));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();
        var hasStatusOutcomeIssue = issues.Any(issue =>
            string.Equals(issue.Code, "guardian_complete_quest_not_ready_to_turn_in", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(shouldRejectStatusOutcome, hasStatusOutcomeIssue);
    }

    private async Task SnapshotCurrentChaosSeaLoreForMortalTurnAsync(string suffix)
    {
        foreach (var path in new[]
        {
            "lore/chaos_sea/soul_system_lore.json",
            "lore/chaos_sea/cosmology.json",
            "lore/chaos_sea/guardians_lore.json",
            "lore/chaos_sea/player_chronicle.json"
        })
        {
            var content = await _fs.ReadFileAsync(path);
            if (string.IsNullOrWhiteSpace(content))
                continue;

            await WritePreTurnTrackedFileAsync(
                path,
                $"test_backups/preturn_{suffix}_{path.Replace('/', '_')}",
                content);
        }
    }

}

