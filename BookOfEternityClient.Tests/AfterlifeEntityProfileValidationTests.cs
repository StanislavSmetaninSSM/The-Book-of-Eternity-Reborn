using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeEntityProfileValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public AfterlifeEntityProfileValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-afterlife-entity-profile-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateGameStateAsync_ValidAfterlifeEntityProfile_PassesProfileValidation()
    {
        await WriteProfileStateAsync(BuildValidProfileJson());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("afterlife_entity_profile_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_InvalidAfterlifeEntityProfile_ReportsContractIssues()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": -1, "lightSparks": 0 },
              "progression": { "enlightenment": { "experience": 10, "tier": 1 }, "radiance": { "experience": 0, "tier": 0 } },
              "standardArts": { "pressure": 6 },
              "specialArts": [
                {
                  "artId": "mirror_guard",
                  "displayName": "Зеркальная Защита",
                  "ownerActorType": "guardian",
                  "ownerActorId": "guardian_mirror",
                  "baseOperation": "unknown_art",
                  "tier": 1,
                  "costMultiplierPercent": 100,
                  "upgradeCost": { "inkFeathers": 30, "lightSparks": 0 },
                  "canTeachPlayer": true,
                  "trainingConditions": [],
                  "effectSummary": "Отражает чужую защиту."
                }
              ],
              "soulDissipationTier": 0,
              "progressionStrategy": { "strategyId": "strategy_1", "summary": "Качать давление.", "priorityOrder": ["pressure"] },
              "ledger": []
            },
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Дубликат",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 0, "lightSparks": 0 },
              "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
              "standardArts": {},
              "specialArts": [],
              "soulDissipationTier": 0,
              "progressionStrategy": { "strategyId": "strategy_2", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
              "ledger": []
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_negative_currency", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_invalid_standard_art_tier", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_invalid_special_art_base_operation", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_invalid_special_art_cost_multiplier", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_special_art_missing_training_conditions", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_duplicate_actor", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MalformedSpecialArtLearningReceipt_ReportsContractIssues()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 0, "lightSparks": 0 },
              "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
              "standardArts": {},
              "specialArts": [],
              "customStates": [],
              "soulDissipationTier": 0,
              "progressionStrategy": { "strategyId": "strategy_1", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
              "ledger": []
            }
          ],
          "afterlifeSpecialArtLearningReceipts": [
            {
              "receiptId": "",
              "teacherActorType": "guardian",
              "teacherActorId": "guardian_mirror",
              "artId": "",
              "playerActorId": "",
              "trainingConditionSatisfied": false,
              "roleplayEvidence": "",
              "summary": ""
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_special_art_learning_missing_receipt_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_special_art_learning_missing_art_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_special_art_learning_missing_player_actor_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_special_art_learning_condition_not_satisfied", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_special_art_learning_missing_roleplay_evidence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MalformedAfterlifeEntityCustomStates_ReportContractIssues()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 0, "lightSparks": 0 },
              "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
              "standardArts": {},
              "specialArts": [],
              "customStates": [
                {
                  "stateName": "Без идентификатора",
                  "currentValue": 1,
                  "minValue": 0,
                  "maxValue": 5,
                  "description": "Это состояние нельзя удалить без stateId.",
                  "progressionRule": { "changePerTurn": 0, "description": "Не меняется." },
                  "thresholds": []
                }
              ],
              "soulDissipationTier": 0,
              "progressionStrategy": { "strategyId": "strategy_1", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
              "ledger": []
            }
          ],
          "afterlifeEntityCustomStateChanges": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "statesToAddOrUpdate": [
                {
                  "stateId": "bad_state",
                  "stateName": "Неполное состояние",
                  "currentValue": 1,
                  "minValue": 0,
                  "maxValue": 5,
                  "description": "Нет progressionRule и thresholds."
                }
              ],
              "statesToRemove": [""]
            },
            {
              "actorType": "resident",
              "actorId": "resident_echo"
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_custom_state_missing_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_custom_state_missing_required_fields", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_custom_state_remove_invalid_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_custom_state_change_empty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MalformedEntityProgressionLedgerAndOverride_ReportContractIssues()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 0, "lightSparks": 0 },
              "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
              "standardArts": {},
              "specialArts": [],
              "customStates": [],
              "soulDissipationTier": 0,
              "progressionStrategy": { "strategyId": "strategy_1", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
              "progressionLedger": [
                {
                  "entryId": "",
                  "source": "client_auto_strategy",
                  "income": { "inkFeathers": -1 },
                  "spending": { "inkFeathers": 0 }
                }
              ],
              "ledger": []
            }
          ],
          "afterlifeEntityProgressionOverrides": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "cycleKey": "",
              "currencyDeltas": { "inkFeathers": -5 }
            },
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "cycleKey": "chaos:7",
              "reason": "Без дельт.",
              "summary": "Ничего не меняет."
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_progression_ledger_missing_entry_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_progression_ledger_missing_cycle_key", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_progression_ledger_negative_amount", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_progression_override_missing_cycle_key", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_progression_override_missing_reason", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_progression_override_empty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SplitProgressionOverrideLedger_PassesProfileValidation()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 5, "lightSparks": 3 },
              "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
              "standardArts": { "pressure": 1 },
              "specialArts": [],
              "customStates": [],
              "soulDissipationTier": 0,
              "progressionStrategy": { "strategyId": "strategy_1", "summary": "Качать давление.", "priorityOrder": ["pressure"], "lastAutoProgressionCycleKey": "chaos:6" },
              "progressionLedger": [
                {
                  "entryId": "guardian_mirror_chaos_6_gm_override",
                  "cycleKey": "chaos:6",
                  "source": "gm_override",
                  "summary": "Хранитель потратил Перья и получил Искры.",
                  "income": { "inkFeathers": 0, "lightSparks": 2 },
                  "spending": { "inkFeathers": 5, "lightSparks": 0 }
                }
              ],
              "ledger": []
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_progression_ledger_negative_amount", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("afterlife_entity_profile_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_UnknownProgressionStrategyPriority_ReportsContractIssue()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 0, "lightSparks": 0 },
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
              "progressionStrategy": { "strategyId": "strategy_1", "summary": "Качать непонятное.", "priorityOrder": ["unknown_art"] },
              "ledger": []
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_strategy_unknown_priority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RejectsProgressionOverrideForUnknownSpecialArt()
    {
        await WriteProfileStateAsync("""
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
              "progressionStrategy": { "strategyId": "strategy_1", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
              "ledger": []
            }
          ],
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

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_progression_override_unknown_special_art", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RejectsInvalidProgressionOverrideMarker()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [],
          "lastInvalidProgressionOverride": {
            "actorType": "guardian",
            "actorId": "guardian_mirror",
            "specialArtTierDeltas": { "miror_guard": 1 }
          },
          "lastInvalidProgressionOverrideReason": "unknown_special_art"
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_progression_override_invalid_authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MalformedSpecialArtAndSoulDissipationOverrides_ReportContractIssues()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 0, "lightSparks": 0 },
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
              "progressionStrategy": { "strategyId": "strategy_1", "summary": "Качать защиту.", "priorityOrder": ["mirror_guard", "soul_dissipation"] },
              "ledger": []
            }
          ],
          "afterlifeEntityProgressionOverrides": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "cycleKey": "chaos:7",
              "reason": "Некорректное изменение.",
              "summary": "Неверные поля.",
              "specialArtTierDeltas": [],
              "soulDissipationTierDelta": "up"
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_progression_override_invalid_special_art_delta", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_progression_override_invalid_soul_dissipation_delta", StringComparison.OrdinalIgnoreCase));
    }

    private Task WriteProfileStateAsync(string json) =>
        _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, json);

    private static string BuildValidProfileJson() =>
        """
        {
          "schemaVersion": 1,
          "profiles": [
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
        """;

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // Ignore temp cleanup failures.
        }
    }
}
