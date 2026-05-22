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
    public async Task TriggerLifeEndTurnRewardValidation_ValidRecordLifeCompletionStillRaisesPrematureRewardIssues()
    {
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_trigger_reward_guard.json",
            """
            {
              "soulName": "Тестовая Душа",
              "currentRealm": "Mortal World",
              "currentIncarnation": 1,
              "inkFeathers": {
                "current": 5
              },
              "soulRelics": {
                "equipped": [],
                "stored": []
              }
            }
            """);

        await WriteRawAsync("game_state/control/life_transitions.json", """
        {
          "reason": "Death",
          "summary": "Смертная жизнь завершена."
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1,
          "metaStateUpdates": {
            "lifeTransitions": {
              "recordLifeCompletion": {
                "characterFinalState": { "causeOfDeath": "Test" },
                "majorAchievements": [],
                "relationshipsFormed": [],
                "moralChoices": [],
                "skillsLearned": [],
                "enlightenmentGained": 0
              }
            }
          },
          "inkFeathers": {
            "current": 17
          },
          "soulRelics": {
            "equipped": [],
            "stored": [
              {
                "relicId": "trigger_turn_reward_relic",
                "name": "Реликвия Слишком Ранней Награды",
                "rarity": "Epic",
                "quality": "Pristine"
              }
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "life_trigger_turn_awarded_ink_feathers", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "life_trigger_turn_awarded_soul_relic", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "life_evaluation_reward_delta_unreadable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TriggerLifeEndTurnRewardValidation_UnreadableDeltaRaisesExplicitIssue()
    {
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_trigger_unreadable_guard.json",
            """
            {
              "soulName": "Тестовая Душа",
              "currentRealm": "Mortal World",
              "currentIncarnation": 1,
              "inkFeathers": {
                "current": 5
              },
              "soulRelics": {
                "equipped": [],
                "stored": []
              }
            }
            """);

        await WriteRawAsync("game_state/control/life_transitions.json", """
        {
          "reason": "Death",
          "summary": "Смертная жизнь завершена."
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1,
          "metaStateUpdates": {
            "lifeTransitions": {
              "recordLifeCompletion": {
                "characterFinalState": { "causeOfDeath": "Test" },
                "majorAchievements": [],
                "relationshipsFormed": [],
                "moralChoices": [],
                "skillsLearned": [],
                "enlightenmentGained": 0
              }
            }
          },
          "inkFeathers": {
            "current": "17"
          },
          "soulRelics": {
            "equipped": [],
            "stored": []
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "life_trigger_turn_reward_delta_unreadable", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "life_trigger_turn_awarded_ink_feathers", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "life_trigger_turn_awarded_soul_relic", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RecordLifeCompletionValidation_SeparateCanonicalControlFileSuppressesMissingTriggerIssue()
    {
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_record_life_completion_valid_context.json",
            """
            {
              "soulName": "Тестовая Душа",
              "currentRealm": "Mortal World",
              "currentIncarnation": 1
            }
            """);

        await WriteRawAsync("game_state/control/life_transitions.json", """
        {
          "reason": "Death",
          "summary": "Смертная жизнь завершена."
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1,
          "metaStateUpdates": {
            "lifeTransitions": {
              "recordLifeCompletion": {
                "characterFinalState": { "causeOfDeath": "Test" },
                "majorAchievements": [],
                "relationshipsFormed": [],
                "moralChoices": [],
                "skillsLearned": [],
                "enlightenmentGained": 0
              }
            }
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "life_transition_record_without_trigger_life_end", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RecordLifeCompletionValidation_MalformedControlFileStillRaisesMissingTriggerIssue()
    {
        await WriteRawAsync("game_state/control/life_transitions.json", """
        {
          "reason": "Death"
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1,
          "metaStateUpdates": {
            "lifeTransitions": {
              "recordLifeCompletion": {
                "characterFinalState": { "causeOfDeath": "Test" },
                "majorAchievements": [],
                "relationshipsFormed": [],
                "moralChoices": [],
                "skillsLearned": [],
                "enlightenmentGained": 0
              }
            }
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "life_transition_record_without_trigger_life_end", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RecordLifeCompletionValidation_CanonicalTriggerWithAfterlifePreTurnStillRaisesMissingTriggerIssue()
    {
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_record_life_completion_afterlife.json",
            """
            {
              "soulName": "Тестовая Душа",
              "currentRealm": "Chaos Sea",
              "currentIncarnation": 1
            }
            """);

        await WriteRawAsync("game_state/control/life_transitions.json", """
        {
          "reason": "Death",
          "summary": "Смертная жизнь завершена."
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1,
          "metaStateUpdates": {
            "lifeTransitions": {
              "recordLifeCompletion": {
                "characterFinalState": { "causeOfDeath": "Test" },
                "majorAchievements": [],
                "relationshipsFormed": [],
                "moralChoices": [],
                "skillsLearned": [],
                "enlightenmentGained": 0
              }
            }
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "life_transition_invalid_realm", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "life_transition_record_without_trigger_life_end", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TriggerLifeEndValidation_SameTurnCurrentRealmSwitchRaisesExplicitIssue()
    {
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_trigger_same_turn_realm_switch.json",
            """
            {
              "soulName": "Тестовая Душа",
              "currentRealm": "Mortal World",
              "currentIncarnation": 1
            }
            """);

        await WriteRawAsync("game_state/control/life_transitions.json", """
        {
          "reason": "Death",
          "summary": "Смертная жизнь завершена."
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "metaStateUpdates": {
            "lifeTransitions": {
              "recordLifeCompletion": {
                "characterFinalState": { "causeOfDeath": "Test" },
                "majorAchievements": [],
                "relationshipsFormed": [],
                "moralChoices": [],
                "skillsLearned": [],
                "enlightenmentGained": 0
              }
            }
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "life_transition_current_realm_switched_same_turn", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "life_transition_record_without_trigger_life_end", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "life_trigger_turn_awarded_ink_feathers", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "life_trigger_turn_awarded_soul_relic", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "life_trigger_turn_reward_delta_unreadable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TriggerLifeEndTurnRewardValidation_UnresolvedCurrentRealmRaisesExplicitAuthorityIssue()
    {
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_trigger_missing_current_realm.json",
            """
            {
              "soulName": "Тестовая Душа",
              "currentRealm": "Mortal World",
              "currentIncarnation": 1,
              "inkFeathers": {
                "current": 5
              },
              "soulRelics": {
                "equipped": [],
                "stored": []
              }
            }
            """);

        await WriteRawAsync("game_state/control/life_transitions.json", """
        {
          "reason": "Death",
          "summary": "Смертная жизнь завершена."
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentIncarnation": 1,
          "metaStateUpdates": {
            "lifeTransitions": {
              "recordLifeCompletion": {
                "characterFinalState": { "causeOfDeath": "Test" },
                "majorAchievements": [],
                "relationshipsFormed": [],
                "moralChoices": [],
                "skillsLearned": [],
                "enlightenmentGained": 0
              }
            }
          },
          "inkFeathers": {
            "current": 5
          },
          "soulRelics": {
            "equipped": [],
            "stored": []
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "life_trigger_turn_missing_realm_authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LifeEvaluationRewardValidation_InvalidManifestDoesNotTrustRawLifeEvaluationSourceLabel()
    {
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_life_eval_invalid_manifest.json",
            """
            {
              "soulName": "Тестовая Душа",
              "currentRealm": "Chaos Sea",
              "currentIncarnation": 1,
              "inkFeathers": {
                "current": 5
              },
              "soulRelics": {
                "equipped": [],
                "stored": []
              }
            }
            """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "inkFeathers": {
            "current": 5
          },
          "soulRelics": {
            "equipped": [],
            "stored": []
          }
        }
        """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["sourceLabel"] = LifeEvaluationRewardAnalyzer.AutomaticLifeEvaluationSourceLabel;
        await WriteRawAsync("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "client_owned_pending_snapshot_manifest_modified", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "life_evaluation_missing_ink_feather_reward", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "life_evaluation_missing_soul_relic_reward", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AscensionValidation_MissingValidatedSnapshotDoesNotFallbackToCurrentRealm()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "soulProgression": {
            "progressPercent": 100
          }
        }
        """);

        await WriteRawAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        await WriteRawAsync("game_state/control/ascension.json", """
        {
          "AscensionTrigger": true,
          "playerChoice": "Ascension"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "ascension_invalid_validated_snapshot_realm", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ascension_invalid_realm", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AscensionValidation_EnlightenmentExperienceThresholdAllowsMidgameShiningEntry()
    {
        var ascensionReadySoul = """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "enlightenment": {
            "currentTier": "Закалённый",
            "experience": 60,
            "level": 3,
            "progressPercent": 60
          },
          "soulProgression": {
            "totalExperience": 60,
            "tier": 3,
            "progressPercent": 60
          }
        }
        """;
        await WriteRawAsync("game_state/meta/soul_state.json", ascensionReadySoul);
        await WriteRawAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", ascensionReadySoul);

        await WriteRawAsync("game_state/control/ascension.json", """
        {
          "AscensionTrigger": true,
          "playerChoice": "Ascension"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ascension_requires_max_enlightenment", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ascension_invalid_realm", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RealmSegregationValidation_InvalidManifestDoesNotTrustRawSourceLabel()
    {
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_realm_segregation_source_label.json",
            """
            {
              "soulName": "Тестовая Душа",
              "currentRealm": "Mortal World",
              "currentIncarnation": 1
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["sourceLabel"] = LifeEvaluationRewardAnalyzer.AutomaticLifeEvaluationSourceLabel;
        await WriteRawAsync("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "client_owned_pending_snapshot_manifest_modified", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "realm_segregation_missing_validated_snapshot_context", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ClientOwnedPendingSnapshotValidation_UnreadableManifestRaisesGenericIntegrityIssue()
    {
        await WriteRawAsync("game_state/control/pending_turn_snapshot.json", "{ not valid json");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "client_owned_pending_snapshot_manifest_modified", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.FilePath, "game_state/control/pending_turn_snapshot.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ClientOwnedPendingSnapshotValidation_HashValidManifestWithoutSnapshotHashesStillRaisesGenericIntegrityIssue()
    {
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_structural_manifest_guard.json",
            """
            {
              "soulName": "Тестовая Душа",
              "currentRealm": "Chaos Sea",
              "currentIncarnation": 1
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["snapshotFileHashes"] = new JsonObject();
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "client_owned_pending_snapshot_manifest_modified", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.FilePath, "game_state/control/pending_turn_snapshot.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ClientOwnedPendingSnapshotValidation_TamperedRollbackBackupInvalidatesValidatedSnapshotAuthority()
    {
        const string backupPath = "test_backups/preturn_soul_state_rollback_parity_guard.json";

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            backupPath,
            """
            {
              "soulName": "Тестовая Душа",
              "currentRealm": "Chaos Sea",
              "currentIncarnation": 1
            }
            """);

        await WriteRawAsync(
            backupPath,
            """
            {
              "soulName": "Тестовая Душа",
              "currentRealm": "Mortal World",
              "currentIncarnation": 1
            }
            """,
            syncPendingSnapshotAuthority: false);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "client_owned_pending_snapshot_manifest_modified", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.FilePath, "game_state/control/pending_turn_snapshot.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SyncPreTurnQuestBaseline_InvalidManifestDoesNotTrustRawRollbackBackup()
    {
        await WritePreTurnTrackedFileAsync(
            "game_state/quests/regular_quests.json",
            "test_backups/preturn_regular_quests_sync_invalid_manifest.json",
            """
            {
              "quests": [
                {
                  "questId": "quest_from_raw_backup",
                  "title": "Квест только в raw backup",
                  "status": "active"
                }
              ]
            }
            """);

        await WriteRawAsync("game_state/quests/regular_quests.json", """
        {
          "UpdateQuests": [
            {
              "questId": "quest_from_raw_backup",
              "status": "completed"
            }
          ]
        }
        """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["sourceLabel"] = "tampered sync baseline source";
        await WriteRawAsync("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "client_owned_pending_snapshot_manifest_modified", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "quest_update_unknown_existing_quest", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MortalBootstrapValidation_MissingValidatedWorldLoreBaselineRaisesExplicitIssue()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        await WriteRawAsync("lore/current_world/world_setting.json", """
        {
          "worldName": "Новый мир",
          "summary": "Текущее описание мира."
        }
        """);
        await WriteRawAsync("lore/current_world/geography.json", """{ "regions": [] }""");
        await WriteRawAsync("lore/current_world/history.json", """{ "eras": [] }""");
        await WriteRawAsync("lore/current_world/cultures.json", """{ "cultures": [] }""");
        await WriteRawAsync("lore/current_world/threats.json", """{ "threats": [] }""");

        await WritePreTurnTrackedFileAsync(
            "lore/current_world/world_setting.json",
            "test_backups/preturn_current_world_world_setting_missing_validated_baseline.json",
            """
            {
              "worldName": "Старый мир",
              "summary": "Предыдущее описание мира."
            }
            """);
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_mortal_bootstrap_soul_state_context.json",
            """
            {
              "soulName": "Тестовая Душа",
              "currentRealm": "Mortal World",
              "currentIncarnation": 1
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["sourceLabel"] = "воплощения";
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await RemoveTrackedSnapshotEntryFromCurrentPendingTurnSnapshotAsync("lore/current_world/world_setting.json");
        await WriteRawAsync("ready/turn_complete.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12,
          "status": "success",
          "timestamp": "2026-04-14T00:00:00Z",
          "filesModified": [
            "lore/current_world/world_setting.json"
          ]
        }
        """, syncPendingSnapshotAuthority: false);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "mortal_bootstrap_missing_validated_world_lore_baseline", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.FilePath, "lore/current_world/world_setting.json", StringComparison.OrdinalIgnoreCase));
    }

}

