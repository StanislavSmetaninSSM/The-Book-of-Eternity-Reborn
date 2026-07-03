using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeSpiritualConflictValidationTests : IDisposable
{
    private static readonly int[] AuthoritativeConflictDice = { 5, 18, 14, 9, 11, 7, 20, 1, 13, 6, 16, 8, 12, 4, 10, 15, 3, 17, 2, 19 };

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public AfterlifeSpiritualConflictValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-afterlife-spiritual-conflict-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateGameStateAsync_NoEffectExchange_AllowsIdenticalBeforeAfter()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateAsync("no_effect");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_exchange_no_state_delta", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_no_effect_has_state_delta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SuccessExchange_RejectsIdenticalBeforeAfter()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateAsync("success");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_exchange_no_state_delta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ContestedExchangeWithAuthoritativeDice_RequiresDiceAudit()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync("""
        {
          "exchangeId": "exchange_missing_dice_001",
          "operationType": "guard",
          "outcome": "success",
          "before": { "conflictPosition": "contested" },
          "after": { "conflictPosition": "player_advantaged" }
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_exchange_missing_dice_audit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ContestedExchange_RejectsDiceNotFromAuthoritativePool()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_wrong_dice_001",
          "operationType": "guard",
          "outcome": "success",
          "before": { "conflictPosition": "contested" },
          "after": { "conflictPosition": "player_advantaged" },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson(playerValueOverride: 15)}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_dice_value_not_authorized", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_AdvantageDiceAudit_SelectingLowerDie_ReportsIssue()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_advantage_wrong_selection_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": { "playerSideStrain": "clear", "oppositionSideStrain": "clear", "conflictPosition": "contested" },
          "after": { "playerSideStrain": "clear", "oppositionSideStrain": "strained", "conflictPosition": "contested" },
          "diceAudit": {{BuildPlayerAdvantageDiceAudit(selectBest: false).ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_dice_advantage_selected_die_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_AdvantageDiceAudit_SelectingBestDie_Allows()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_advantage_valid_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": { "playerSideStrain": "clear", "oppositionSideStrain": "clear", "conflictPosition": "contested" },
          "after": { "playerSideStrain": "clear", "oppositionSideStrain": "strained", "conflictPosition": "contested" },
          "diceAudit": {{BuildPlayerAdvantageDiceAudit(selectBest: true).ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code is not null &&
            issue.Code.StartsWith("afterlife_conflict_dice_advantage", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_GreatAdvantageDiceAudit_SelectingHighestDie_Allows()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_great_advantage_valid_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": { "playerSideStrain": "clear", "oppositionSideStrain": "clear", "conflictPosition": "contested" },
          "after": { "playerSideStrain": "clear", "oppositionSideStrain": "strained", "conflictPosition": "contested" },
          "diceAudit": {{BuildPlayerTieredRollDiceAudit(
              "great_advantage",
              new JsonNode?[] { BuildRollModeSource("great_advantage", "решающее темповое окно после защиты") },
              Array.Empty<JsonNode?>(),
              new (int SourceIndex, int Value, bool Selected)[] { (0, 5, false), (1, 18, true), (2, 14, false) }).ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code is not null &&
            issue.Code.StartsWith("afterlife_conflict_dice_", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_AbsentCombatConditions_RemainsBackwardCompatible()
    {
        await WriteSoulStateAsync();
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        root.Remove("lastInvalidUpdate");
        root.Remove("lastInvalidUpdateReason");
        root.Remove("lastInvalidUpdateAtUtc");
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code is not null &&
            issue.Code.StartsWith("afterlife_combat_condition_", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ValidActiveCombatCondition_AllowsKnownContractShape()
    {
        await WriteSoulStateAsync();
        await WriteActiveConflictWithCombatConditionsAsync(BuildValidCombatCondition().ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code is not null &&
            issue.Code.StartsWith("afterlife_combat_condition_", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ValidCombatConditionCanonicalAliases_AllowsDocumentedShape()
    {
        await WriteSoulStateAsync();
        await WriteActiveConflictWithCombatConditionsAsync(BuildCanonicalCombatCondition().ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code is not null &&
            issue.Code.StartsWith("afterlife_combat_condition_", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ActiveCombatConditionMissingRequiredFields_IsRejected()
    {
        await WriteSoulStateAsync();
        await WriteActiveConflictWithCombatConditionsAsync("""
        {
          "conditionId": "condition_missing_fields",
          "status": "active"
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_combat_condition_missing_required_field", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".combatConditions[0].displayName", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_combat_condition_missing_required_field", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".combatConditions[0].counterplay", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CombatConditionRejectsUnsupportedKindAndAxis()
    {
        await WriteSoulStateAsync();
        var condition = BuildValidCombatCondition();
        condition["kind"] = "rage";
        condition["mechanicalAxis"] = "strength";
        await WriteActiveConflictWithCombatConditionsAsync(condition.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_combat_condition_invalid_kind", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_combat_condition_invalid_mechanical_axis", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ActiveCombatConditionRequiresCounterplayAndLiveDuration()
    {
        await WriteSoulStateAsync();
        var condition = BuildValidCombatCondition();
        condition["counterplay"] = new JsonArray();
        condition["duration"] = new JsonObject
        {
            ["type"] = "next_matching_operation",
            ["remainingUses"] = 0
        };
        await WriteActiveConflictWithCombatConditionsAsync(condition.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_combat_condition_missing_counterplay", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_combat_condition_active_duration_spent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CombatConditionRejectsControlPayoffOutsideAntiControlOperations()
    {
        await WriteSoulStateAsync();
        var condition = BuildValidCombatCondition();
        condition["conditionId"] = "illegal_control_condition";
        condition["mechanicalAxis"] = "controlState";
        condition["affectedOperations"] = new JsonArray(JsonValue.Create("pressure"));
        condition["payoff"] = new JsonObject
        {
            ["effect"] = "create_control",
            ["sourceType"] = "combat_condition"
        };
        await WriteActiveConflictWithCombatConditionsAsync(condition.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_combat_condition_illegal_control_payoff", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RollModeCombatConditionSourceMustReferenceActiveCondition()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_missing_condition_source_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": { "playerSideStrain": "clear", "oppositionSideStrain": "clear", "conflictPosition": "contested" },
          "after": { "playerSideStrain": "clear", "oppositionSideStrain": "strained", "conflictPosition": "contested" },
          "diceAudit": {{BuildPlayerTieredRollDiceAudit(
              "advantage",
              new JsonNode?[] { BuildCombatConditionRollModeSource("missing_condition_id") },
              Array.Empty<JsonNode?>(),
              new (int SourceIndex, int Value, bool Selected)[] { (0, 5, false), (1, 18, true) }).ToJsonString()}}
        }
        """);
        await SetActiveCombatConditionsAsync(BuildValidCombatCondition().ToJsonString());
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_combat_condition_roll_source_missing_active_condition", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RollModeCombatConditionSourceCannotReferenceConsumedCondition()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_consumed_condition_source_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": { "playerSideStrain": "clear", "oppositionSideStrain": "clear", "conflictPosition": "contested" },
          "after": { "playerSideStrain": "clear", "oppositionSideStrain": "strained", "conflictPosition": "contested" },
          "diceAudit": {{BuildPlayerTieredRollDiceAudit(
              "advantage",
              new JsonNode?[] { BuildCombatConditionRollModeSource("consumed_condition_id") },
              Array.Empty<JsonNode?>(),
              new (int SourceIndex, int Value, bool Selected)[] { (0, 5, false), (1, 18, true) }).ToJsonString()}}
        }
        """);
        var consumed = BuildValidCombatCondition("consumed_condition_id");
        consumed["status"] = "consumed";
        consumed["duration"] = new JsonObject
        {
            ["type"] = "next_matching_operation",
            ["remainingUses"] = 0
        };
        await SetActiveCombatConditionsAsync(consumed.ToJsonString());
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_combat_condition_roll_source_missing_active_condition", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RollModeNonCombatConditionSourceWithSourceId_RemainsValid()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_tempo_window_source_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": { "playerSideStrain": "clear", "oppositionSideStrain": "clear", "conflictPosition": "contested" },
          "after": { "playerSideStrain": "clear", "oppositionSideStrain": "strained", "conflictPosition": "contested" },
          "diceAudit": {{BuildPlayerTieredRollDiceAudit(
              "advantage",
              new JsonNode?[]
              {
                  new JsonObject
                  {
                      ["sourceType"] = "guard_tempo_window",
                      ["sourceId"] = "tempo_guard_valid_001",
                      ["level"] = "advantage",
                      ["summary"] = "Темповое окно после успешной защиты."
                  }
              },
              Array.Empty<JsonNode?>(),
              new (int SourceIndex, int Value, bool Selected)[] { (0, 5, false), (1, 18, true) }).ToJsonString()}}
        }
        """);
        await SetActiveCombatConditionsAsync(BuildValidCombatCondition().ToJsonString());
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_combat_condition_roll_source_missing_active_condition", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(MalformedActiveCombatConditionCases))]
    public async Task ValidateGameStateAsync_MalformedActiveCombatCondition_IsRejected(
        string expectedCode,
        string expectedPathSuffix,
        Action<JsonObject> mutateCondition)
    {
        await WriteSoulStateAsync();
        var condition = BuildValidCombatCondition("malformed_condition_001");
        mutateCondition(condition);
        await WriteActiveConflictWithCombatConditionsAsync(condition.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, expectedCode, StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(expectedPathSuffix, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_GreatAdvantageDiceAudit_WithTwoDice_ReportsCountMismatch()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_great_advantage_short_roll_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": { "playerSideStrain": "clear", "oppositionSideStrain": "clear", "conflictPosition": "contested" },
          "after": { "playerSideStrain": "clear", "oppositionSideStrain": "strained", "conflictPosition": "contested" },
          "diceAudit": {{BuildPlayerTieredRollDiceAudit(
              "great_advantage",
              new JsonNode?[] { BuildRollModeSource("great_advantage", "решающее темповое окно после защиты") },
              Array.Empty<JsonNode?>(),
              new (int SourceIndex, int Value, bool Selected)[] { (0, 5, false), (1, 18, true) }).ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_dice_roll_count_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_GreatAdvantageWithOrdinaryDisadvantage_StepsDownToAdvantage()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_great_advantage_step_down_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": { "playerSideStrain": "clear", "oppositionSideStrain": "clear", "conflictPosition": "contested" },
          "after": { "playerSideStrain": "clear", "oppositionSideStrain": "strained", "conflictPosition": "contested" },
          "diceAudit": {{BuildPlayerTieredRollDiceAudit(
              "advantage",
              new JsonNode?[] { BuildRollModeSource("great_advantage", "решающее темповое окно после защиты") },
              new JsonNode?[] { JsonValue.Create("слабое сопротивление духовной среде") },
              new (int SourceIndex, int Value, bool Selected)[] { (0, 5, false), (1, 18, true) }).ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code is not null &&
            issue.Code.StartsWith("afterlife_conflict_dice_", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_DisadvantageDiceAudit_SelectingHigherDie_ReportsIssue()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_disadvantage_wrong_selection_001",
          "operationType": "pressure",
          "outcome": "setback",
          "before": { "playerSideStrain": "clear", "oppositionSideStrain": "clear", "conflictPosition": "contested" },
          "after": { "playerSideStrain": "strained", "oppositionSideStrain": "clear", "conflictPosition": "contested" },
          "diceAudit": {{BuildPlayerDisadvantageDiceAudit(selectLowest: false).ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_dice_disadvantage_selected_die_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_DireDisadvantageDiceAudit_SelectingLowestDie_Allows()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_dire_disadvantage_valid_001",
          "operationType": "pressure",
          "outcome": "setback",
          "before": { "playerSideStrain": "clear", "oppositionSideStrain": "clear", "conflictPosition": "contested" },
          "after": { "playerSideStrain": "strained", "oppositionSideStrain": "clear", "conflictPosition": "contested" },
          "diceAudit": {{BuildPlayerTieredRollDiceAudit(
              "dire_disadvantage",
              Array.Empty<JsonNode?>(),
              new JsonNode?[] { BuildRollModeSource("dire_disadvantage", "сильные духовные оковы подавляют действие") },
              new (int SourceIndex, int Value, bool Selected)[] { (0, 5, true), (1, 18, false), (2, 14, false) }).ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code is not null &&
            issue.Code.StartsWith("afterlife_conflict_dice_", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_AdvantageWithDireDisadvantage_StepsDownToDisadvantage()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_dire_disadvantage_step_down_001",
          "operationType": "pressure",
          "outcome": "setback",
          "before": { "playerSideStrain": "clear", "oppositionSideStrain": "clear", "conflictPosition": "contested" },
          "after": { "playerSideStrain": "strained", "oppositionSideStrain": "clear", "conflictPosition": "contested" },
          "diceAudit": {{BuildPlayerTieredRollDiceAudit(
              "disadvantage",
              new JsonNode?[] { JsonValue.Create("поддержка союзника") },
              new JsonNode?[] { BuildRollModeSource("dire_disadvantage", "сильные духовные оковы подавляют действие") },
              new (int SourceIndex, int Value, bool Selected)[] { (0, 5, true), (1, 18, false) }).ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code is not null &&
            issue.Code.StartsWith("afterlife_conflict_dice_", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_DuplicateOrdinaryAdvantageSources_DoNotBecomeGreatAdvantage()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_duplicate_advantage_sources_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": { "playerSideStrain": "clear", "oppositionSideStrain": "clear", "conflictPosition": "contested" },
          "after": { "playerSideStrain": "clear", "oppositionSideStrain": "strained", "conflictPosition": "contested" },
          "diceAudit": {{BuildPlayerTieredRollDiceAudit(
              "advantage",
              new JsonNode?[] { JsonValue.Create("поддержка союзника"), JsonValue.Create("благоприятная духовная среда") },
              Array.Empty<JsonNode?>(),
              new (int SourceIndex, int Value, bool Selected)[] { (0, 5, false), (1, 18, true) }).ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code is not null &&
            issue.Code.StartsWith("afterlife_conflict_dice_", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CancelledAdvantageDisadvantage_RejectsExtraDice()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_cancelled_roll_extra_die_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": { "playerSideStrain": "clear", "oppositionSideStrain": "clear", "conflictPosition": "contested" },
          "after": { "playerSideStrain": "clear", "oppositionSideStrain": "strained", "conflictPosition": "contested" },
          "diceAudit": {{BuildCancelledPlayerRollWithExtraDieAudit().ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_dice_cancelled_roll_uses_extra_dice", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_DisadvantageDiceAudit_DiscardedNatural20DoesNotTriggerCritical()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_discarded_critical_001",
          "operationType": "pressure",
          "outcome": "setback",
          "before": { "playerSideStrain": "clear", "oppositionSideStrain": "clear", "conflictPosition": "contested" },
          "after": { "playerSideStrain": "strained", "oppositionSideStrain": "clear", "conflictPosition": "contested" },
          "diceAudit": {{BuildPlayerDisadvantageDiceAuditWithDiscardedNatural20().ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_dice_missing_critical_result", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_dice_critical_result_without_critical_roll", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_dice_disadvantage_selected_die_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SpecialArtExchange_RequiresEffectNote()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync("""
        {
          "exchangeId": "exchange_special_missing_note_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": { "playerSideStrain": "clear", "oppositionSideStrain": "clear", "conflictPosition": "contested" },
          "after": { "playerSideStrain": "clear", "oppositionSideStrain": "strained", "conflictPosition": "contested" },
          "specialArtAudit": {
            "artId": "mirror_pressure",
            "displayName": "Зеркальное Давление",
            "ownerActorType": "guardian",
            "ownerActorId": "guardian_mirror",
            "baseOperation": "pressure",
            "costMultiplierPercent": 150
          },
          "actionCostAudit": {
            "player": {
              "operationType": "pressure",
              "baseCost": 3,
              "minCost": 1,
              "artTier": 0,
              "specialArtId": "mirror_pressure",
              "specialCostMultiplierPercent": 150,
              "standardEffectiveCost": 3,
              "effectiveCost": 5,
              "before": 6,
              "after": 1
            }
          }
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_special_art_missing_effect_note", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SpecialArtExchange_RequiresScaledActionCost()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync("""
        {
          "exchangeId": "exchange_special_cost_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": { "playerSideStrain": "clear", "oppositionSideStrain": "clear", "conflictPosition": "contested" },
          "after": { "playerSideStrain": "clear", "oppositionSideStrain": "strained", "conflictPosition": "contested" },
          "specialArtAudit": {
            "artId": "mirror_pressure",
            "displayName": "Зеркальное Давление",
            "ownerActorType": "player_soul",
            "ownerActorId": "player_soul",
            "baseOperation": "pressure",
            "costMultiplierPercent": 150,
            "effectNote": "Зеркальное давление раздваивает импульс и усиливает нажим на противника."
          },
          "actionCostAudit": {
            "player": {
              "operationType": "pressure",
              "baseCost": 3,
              "minCost": 1,
              "artTier": 0,
              "specialArtId": "mirror_pressure",
              "specialCostMultiplierPercent": 150,
              "standardEffectiveCost": 3,
              "effectiveCost": 3,
              "before": 6,
              "after": 3
            }
          }
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_special_art_cost_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SoulDissipationWithoutEnoughTier_ReportsIssue()
    {
        await WriteSoulStateAsync();
        await WriteSoulDissipationProfileStateAsync(playerDissipationTier: 2, targetEnlightenmentTier: 2);
        await WriteResolvedConflictWithSoulDissipationAsync("""
        {
          "proofId": "soul_dissipation_proof_low_tier_001",
          "actorType": "player_soul",
          "actorId": "player_soul",
          "targetActorType": "guardian",
          "targetActorId": "guardian_liora",
          "dissipationTier": 2,
          "targetStabilityCoefficient": 2,
          "resolvedAtTurn": 7,
          "victoryProof": "player_victory",
          "gmMotivation": "Душа пытается окончательно развеять побеждённого Хранителя.",
          "outcome": "target_dispersed"
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_soul_dissipation_tier_too_low", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SoulDissipationWithoutVictory_ReportsIssue()
    {
        await WriteSoulStateAsync();
        await WriteSoulDissipationProfileStateAsync(playerDissipationTier: 3, targetEnlightenmentTier: 1);
        await WriteResolvedConflictWithSoulDissipationAsync("""
        {
          "proofId": "soul_dissipation_proof_no_victory_001",
          "actorType": "player_soul",
          "actorId": "player_soul",
          "targetActorType": "guardian",
          "targetActorId": "guardian_liora",
          "dissipationTier": 3,
          "targetStabilityCoefficient": 1,
          "resolvedAtTurn": 7,
          "victoryProof": "player_victory",
          "gmMotivation": "Душа пытается развеять цель без доказанной победы.",
          "outcome": "target_dispersed"
        }
        """, playerOutcome: "lost", resolutionKind: "player_loss");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_soul_dissipation_missing_victory_proof", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PlayerSoulDissipationRequiresTerminalGameOver()
    {
        await WriteSoulStateAsync();
        await WriteSoulDissipationProfileStateAsync(playerDissipationTier: 0, targetEnlightenmentTier: 1, oppositionDissipationTier: 3);
        await WriteResolvedConflictWithSoulDissipationAsync("""
        {
          "proofId": "soul_dissipation_proof_player_death_001",
          "actorType": "guardian",
          "actorId": "guardian_liora",
          "targetActorType": "player_soul",
          "targetActorId": "player_soul",
          "dissipationTier": 3,
          "targetStabilityCoefficient": 1,
          "resolvedAtTurn": 7,
          "victoryProof": "opposition_victory",
          "gmMotivation": "Хранитель решает окончательно уничтожить душу после победы.",
          "outcome": "player_soul_dispersed"
        }
        """, playerOutcome: "lost", resolutionKind: "player_loss");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_player_soul_dissipation_missing_game_over", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PlayerSoulDissipationGameOverWrongMessage_ReportsIssue()
    {
        await WriteSoulStateWithTerminalGameOverAsync("Вы почти мертвы.");
        await WriteSoulDissipationProfileStateAsync(playerDissipationTier: 0, targetEnlightenmentTier: 1, oppositionDissipationTier: 3);
        await WriteResolvedConflictWithSoulDissipationAsync("""
        {
          "proofId": "soul_dissipation_proof_player_death_002",
          "actorType": "guardian",
          "actorId": "guardian_liora",
          "targetActorType": "player_soul",
          "targetActorId": "player_soul",
          "dissipationTier": 3,
          "targetStabilityCoefficient": 1,
          "resolvedAtTurn": 7,
          "victoryProof": "opposition_victory",
          "gmMotivation": "Хранитель мотивирован окончательно развеять душу.",
          "outcome": "player_soul_dispersed"
        }
        """, playerOutcome: "lost", resolutionKind: "player_loss");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_player_soul_dissipation_game_over_message_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ValidNpcSoulDissipationProof_DoesNotReportSoulDissipationIssue()
    {
        await WriteSoulStateAsync();
        await WriteSoulDissipationProfileStateAsync(playerDissipationTier: 3, targetEnlightenmentTier: 1);
        await WriteResolvedConflictWithSoulDissipationAsync("""
        {
          "proofId": "soul_dissipation_proof_valid_target_001",
          "actorType": "player_soul",
          "actorId": "player_soul",
          "targetActorType": "guardian",
          "targetActorId": "guardian_liora",
          "dissipationTier": 3,
          "targetStabilityCoefficient": 1,
          "resolvedAtTurn": 7,
          "victoryProof": "player_victory",
          "gmMotivation": "Игрок решает развеять побеждённого противника после доказанной победы.",
          "outcome": "target_dispersed"
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("afterlife_conflict_soul_dissipation", StringComparison.OrdinalIgnoreCase) == true ||
            issue.Code?.StartsWith("afterlife_conflict_player_soul_dissipation", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_ValidPlayerSoulDissipationTerminalGameOver_DoesNotReportPlayerGameOverIssue()
    {
        await WriteSoulStateWithTerminalGameOverAsync(AfterlifeSpiritualConflictState.TerminalSoulDissipationMessage);
        await WriteSoulDissipationProfileStateAsync(playerDissipationTier: 0, targetEnlightenmentTier: 1, oppositionDissipationTier: 3);
        await WriteResolvedConflictWithSoulDissipationAsync("""
        {
          "proofId": "soul_dissipation_proof_player_death_002",
          "actorType": "guardian",
          "actorId": "guardian_liora",
          "targetActorType": "player_soul",
          "targetActorId": "player_soul",
          "dissipationTier": 3,
          "targetStabilityCoefficient": 1,
          "resolvedAtTurn": 7,
          "victoryProof": "opposition_victory",
          "gmMotivation": "Хранитель мотивирован окончательно развеять душу после победы.",
          "outcome": "player_soul_dispersed"
        }
        """, playerOutcome: "lost", resolutionKind: "player_loss");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("afterlife_conflict_soul_dissipation", StringComparison.OrdinalIgnoreCase) == true ||
            issue.Code?.StartsWith("afterlife_conflict_player_soul_dissipation", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_TerminalGameOverWithoutSoulDissipationProof_ReportsIssue()
    {
        await WriteSoulStateWithTerminalGameOverAsync(AfterlifeSpiritualConflictState.TerminalSoulDissipationMessage);
        await WriteSoulDissipationProfileStateAsync(playerDissipationTier: 0, targetEnlightenmentTier: 1, oppositionDissipationTier: 3);
        await WriteResolvedConflictWithoutSoulDissipationAsync(playerOutcome: "lost", resolutionKind: "player_loss");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_player_soul_dissipation_unlinked_game_over", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RejectsSoulDissipationTierForgedBySameTurnProfileEdit()
    {
        await WriteSoulStateWithTerminalGameOverAsync(AfterlifeSpiritualConflictState.TerminalSoulDissipationMessage);
        await WriteSoulDissipationProfileStateAsync(playerDissipationTier: 0, targetEnlightenmentTier: 1, oppositionDissipationTier: 5);
        await WriteResolvedConflictWithSoulDissipationAsync("""
        {
          "proofId": "soul_dissipation_proof_player_death_002",
          "actorType": "guardian",
          "actorId": "guardian_liora",
          "targetActorType": "player_soul",
          "targetActorId": "player_soul",
          "dissipationTier": 5,
          "targetStabilityCoefficient": 1,
          "resolvedAtTurn": 7,
          "victoryProof": "opposition_victory",
          "gmMotivation": "Лиора решила окончательно уничтожить душу после победы.",
          "outcome": "player_soul_dispersed"
        }
        """, playerOutcome: "lost", resolutionKind: "player_loss");

        var preTurnProfiles = BuildSoulDissipationProfileStateJson(
            playerDissipationTier: 0,
            targetEnlightenmentTier: 1,
            oppositionDissipationTier: 0);
        await WriteSoulDissipationAuthoritySnapshotAsync(
            preTurnProfiles,
            "pre-turn soul dissipation authority",
            "Душа проигрывает конфликт.");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_soul_dissipation_tier_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RejectsSoulDissipationTargetCoefficientLoweredBySameTurnProfileEdit()
    {
        await WriteSoulStateWithTerminalGameOverAsync(AfterlifeSpiritualConflictState.TerminalSoulDissipationMessage);
        await WriteSoulDissipationProfileStateAsync(
            playerDissipationTier: 0,
            targetEnlightenmentTier: 1,
            oppositionDissipationTier: 5,
            playerEnlightenmentTier: 0);
        await WriteResolvedConflictWithSoulDissipationAsync("""
        {
          "proofId": "soul_dissipation_proof_player_death_002",
          "actorType": "guardian",
          "actorId": "guardian_liora",
          "targetActorType": "player_soul",
          "targetActorId": "player_soul",
          "dissipationTier": 5,
          "targetStabilityCoefficient": 0,
          "resolvedAtTurn": 7,
          "victoryProof": "opposition_victory",
          "gmMotivation": "Лиора решила окончательно уничтожить душу после победы.",
          "outcome": "player_soul_dispersed"
        }
        """, playerOutcome: "lost", resolutionKind: "player_loss");

        var preTurnProfiles = BuildSoulDissipationProfileStateJson(
            playerDissipationTier: 0,
            targetEnlightenmentTier: 1,
            oppositionDissipationTier: 5,
            playerEnlightenmentTier: 4);
        await WriteSoulDissipationAuthoritySnapshotAsync(
            preTurnProfiles,
            "pre-turn soul stability authority",
            "Душа проигрывает конфликт.");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_soul_dissipation_target_coefficient_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("playerTotal", 19, "afterlife_conflict_dice_player_total_mismatch")]
    [InlineData("margin", 5, "afterlife_conflict_dice_margin_mismatch")]
    [InlineData("outcomeBand", "mixed_or_no_effect", "afterlife_conflict_dice_outcome_band_mismatch")]
    public async Task ValidateGameStateAsync_ContestedExchange_ValidatesDiceMathAndOutcomeBand(
        string mutatedField,
        object mutatedValue,
        string expectedCode)
    {
        await WriteSoulStateAsync();
        var diceAudit = BuildPlayerSuccessDiceAudit();
        diceAudit[mutatedField] = mutatedValue switch
        {
            int intValue => JsonValue.Create(intValue),
            string stringValue => JsonValue.Create(stringValue),
            _ => throw new InvalidOperationException($"Unsupported mutated value type: {mutatedValue.GetType().Name}")
        };
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_bad_math_001",
          "operationType": "guard",
          "outcome": "success",
          "before": { "conflictPosition": "contested" },
          "after": { "conflictPosition": "player_advantaged" },
          "diceAudit": {{diceAudit.ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, expectedCode, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_NonContestedPosition_RequiresDicePositionModifier()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_missing_position_modifier_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "player_advantaged"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "player_advantaged"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_dice_missing_position_modifier", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_DiceAuditMissingBeforePosition_IsRejected()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_missing_before_position_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "player_advantaged"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_exchange_missing_before_position", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ContestedPosition_DoesNotRequireDicePositionModifier()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_contested_no_position_modifier_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_exchange_missing_before_position", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_dice_missing_position_modifier", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SuccessfulPressureMissingBeforeOppositionStrain_IsRejected()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_pressure_missing_before_strain_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_pressure_missing_opposition_strain_delta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PartialSuccessfulPressureMissingAfterOppositionStrain_IsRejected()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_pressure_missing_after_strain_001",
          "operationType": "pressure",
          "outcome": "partial_success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "conflictPosition": "contested",
            "pressureSummary": "Давление описано художественно, но не зафиксировало strain delta."
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_pressure_missing_opposition_strain_delta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SuccessfulPressureWithOppositionStrainDelta_IsAccepted()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_pressure_valid_strain_delta_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_pressure_missing_opposition_strain_delta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_NoEffectPressureWithoutOppositionStrainDelta_IsAccepted()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_pressure_no_effect_no_strain_delta_001",
          "operationType": "pressure",
          "outcome": "no_effect",
          "before": {
            "playerSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildMixedNoEffectDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_pressure_missing_opposition_strain_delta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PressureAllowsEquivalentNoControlEncodings()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_pressure_equivalent_no_control_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested",
            "controlState": {
              "level": "none"
            }
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildMixedNoEffectDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_pressure_adds_binding", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PressureStillRejectsRealControlCreation()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_pressure_adds_real_control_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested",
            "controlState": null
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested",
            "controlState": {
              "level": "hindered",
              "controllerSide": "player",
              "controlId": "control_pressure_should_not_create_001",
              "sourceOperation": "pressure",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Pressure must not create control."
            }
          },
          "diceAudit": {{BuildMixedNoEffectDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_pressure_adds_binding", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ContestedPosition_RejectsPositionModifier()
    {
        await WriteSoulStateAsync();
        var diceAudit = BuildPlayerSuccessDiceAuditWithPositionModifier("contested");
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_contested_with_position_modifier_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{diceAudit.ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_dice_unexpected_position_modifier_for_contested", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_NonContestedPosition_AcceptsCanonicalDicePositionModifier()
    {
        await WriteSoulStateAsync();
        var diceAudit = BuildPlayerSuccessDiceAuditWithPositionModifier("player_advantaged");
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_position_modifier_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "player_advantaged"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "player_advantaged"
          },
          "diceAudit": {{diceAudit.ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_dice_missing_position_modifier", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_dice_player_total_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_dice_margin_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PositionModifierWithoutExactPosition_IsRejected()
    {
        await WriteSoulStateAsync();
        var diceAudit = BuildPlayerSuccessDiceAudit();
        AddConflictPositionModifier(diceAudit, "player", null, 2, includePosition: false);
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_blank_position_modifier_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "player_advantaged"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "player_advantaged"
          },
          "diceAudit": {{diceAudit.ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_dice_unexpected_position_modifier", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_DuplicatePositionModifiers_AreRejected()
    {
        await WriteSoulStateAsync();
        var diceAudit = BuildPlayerSuccessDiceAuditWithPositionModifier("player_advantaged");
        AddConflictPositionModifier(diceAudit, "player", "player_advantaged", 2);
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_duplicate_position_modifier_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "player_advantaged"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "player_advantaged"
          },
          "diceAudit": {{diceAudit.ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_dice_invalid_position_modifier_total", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ExtraDifferentPositionModifier_IsRejected()
    {
        await WriteSoulStateAsync();
        var diceAudit = BuildPlayerSuccessDiceAuditWithPositionModifier("player_advantaged");
        AddConflictPositionModifier(diceAudit, "player", "player_dominant", 2);
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_extra_position_modifier_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "player_advantaged"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "player_advantaged"
          },
          "diceAudit": {{diceAudit.ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_dice_unexpected_position_modifier", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ExtraOppositeSidePositionModifier_IsRejected()
    {
        await WriteSoulStateAsync();
        var diceAudit = BuildPlayerSuccessDiceAuditWithPositionModifier("player_advantaged");
        AddConflictPositionModifier(diceAudit, "opposition", "player_advantaged", 2);
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_extra_opposite_side_position_modifier_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "player_advantaged"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "player_advantaged"
          },
          "diceAudit": {{diceAudit.ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_dice_unexpected_position_modifier_side", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_DominantPositionRequiresSinglePositionModifier()
    {
        await WriteSoulStateAsync();
        var diceAudit = BuildPlayerSuccessDiceAudit();
        AddConflictPositionModifier(diceAudit, "player", "player_dominant", 2);
        AddConflictPositionModifier(diceAudit, "player", "player_dominant", 2);
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_split_dominant_position_modifier_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "player_dominant"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "player_dominant"
          },
          "diceAudit": {{diceAudit.ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_dice_invalid_position_modifier_total", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PositionModifierOnWrongSide_IsRejected()
    {
        await WriteSoulStateAsync();
        var diceAudit = BuildPlayerSuccessDiceAudit();
        AddConflictPositionModifier(diceAudit, "player", "opposition_advantaged", 2);
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_wrong_side_position_modifier_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "opposition_advantaged"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "opposition_advantaged"
          },
          "diceAudit": {{diceAudit.ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_dice_unexpected_position_modifier_side", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_DominantPosition_AcceptsSingleCanonicalModifier()
    {
        await WriteSoulStateAsync();
        var diceAudit = BuildPlayerSuccessDiceAuditWithPositionModifier("player_dominant", value: 4);
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_dominant_position_modifier_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "player_dominant"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "player_dominant"
          },
          "diceAudit": {{diceAudit.ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_dice_missing_position_modifier", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_dice_invalid_position_modifier_total", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_dice_player_total_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_dice_margin_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SuccessfulCounter_RequiresMeasuredPayoff()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_counter_no_payoff_001",
          "operationType": "counter",
          "outcome": "success",
          "incomingAction": {
            "operationType": "pressure",
            "summary": "Лиора давит на трещину души."
          },
          "before": {
            "playerSideStrain": "strained",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_counter_missing_payoff", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SuccessfulCounter_RejectsEmptyCounterPayoff()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_counter_empty_payoff_001",
          "operationType": "counter",
          "outcome": "countered",
          "incomingAction": {
            "operationType": "pressure",
            "summary": "Лиора давит на трещину души."
          },
          "counterPayoff": {},
          "before": {
            "playerSideStrain": "strained",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_counter_missing_payoff", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PartialSuccessCounter_RequiresMeasuredPayoff()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_counter_partial_no_payoff_001",
          "operationType": "counter",
          "outcome": "partial_success",
          "incomingAction": {
            "operationType": "pressure",
            "summary": "Лиора давит на трещину души."
          },
          "before": {
            "playerSideStrain": "strained",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_counter_missing_payoff", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SuccessfulCounter_AllowsMeaningfulCounterPayoff()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_counter_meaningful_payoff_001",
          "operationType": "counter",
          "outcome": "countered",
          "incomingAction": {
            "operationType": "pressure",
            "summary": "Лиора давит на трещину души."
          },
          "counterPayoff": {
            "summary": "Игрок сорвал давление и получил безопасное окно для следующего действия."
          },
          "before": {
            "playerSideStrain": "strained",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_counter_missing_payoff", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SuccessfulCounter_AllowsPositionSwingPayoff()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_counter_position_payoff_001",
          "operationType": "counter",
          "outcome": "countered",
          "incomingAction": {
            "operationType": "pressure",
            "summary": "Лиора давит на трещину души."
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "player_advantaged"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_counter_missing_payoff", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ChaosSeaVictoryReward_AllowsInkFeatherDelta()
    {
        await WriteSoulStateWithInkFeathersAsync(50, "Chaos Sea");
        await WriteResolvedConflictRewardStateAsync(BuildConflictRewardAuditJson(
            "Chaos Sea",
            AfterlifeSpiritualConflictState.RewardCurrencyInkFeathers,
            finalAmount: 30));
        await WriteRewardTurnSnapshotAsync(preTurnSoulJson: BuildSoulStateJson("Chaos Sea", inkFeathers: 20));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_reward_currency_delta_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_reward_wrong_currency", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_reward_not_allowed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ShiningVictoryReward_AllowsLightSparkDelta()
    {
        await WriteSoulStateWithInkFeathersAsync(20, "Shining Abode");
        await WriteShiningStateWithLightSparksAsync(8);
        await WriteResolvedConflictRewardStateAsync(BuildConflictRewardAuditJson(
            "Shining Abode",
            AfterlifeSpiritualConflictState.RewardCurrencyLightSparks,
            finalAmount: 3),
            realm: "Shining Abode");
        await WriteRewardTurnSnapshotAsync(
            preTurnSoulJson: BuildSoulStateJson("Shining Abode", inkFeathers: 20),
            preTurnShiningJson: BuildShiningStateJson(lightSparks: 5),
            preTurnConflictJson: BuildActiveConflictRootJson(realm: "Shining Abode"));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_reward_currency_delta_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_reward_wrong_currency", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_reward_not_allowed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RewardWrongCurrencyByRealm_IsRejected()
    {
        await WriteSoulStateWithInkFeathersAsync(20, "Chaos Sea");
        await WriteResolvedConflictRewardStateAsync(BuildConflictRewardAuditJson(
            "Chaos Sea",
            AfterlifeSpiritualConflictState.RewardCurrencyLightSparks,
            finalAmount: 30));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_reward_wrong_currency", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RewardOverCap_IsRejected()
    {
        await WriteSoulStateWithInkFeathersAsync(20, "Chaos Sea");
        await WriteResolvedConflictRewardStateAsync(BuildConflictRewardAuditJson(
            "Chaos Sea",
            AfterlifeSpiritualConflictState.RewardCurrencyInkFeathers,
            finalAmount: AfterlifeSpiritualConflictState.ChaosSeaConflictRewardMaxAmount + 1,
            opposingLeadStrength: 20,
            challengeTier: 5,
            startingConflictPosition: "opposition_dominant",
            riskMultiplierPercent: 150));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_reward_amount_over_cap", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_NoRewardClosuresRemainValid()
    {
        await WriteSoulStateAsync();
        await WriteResolvedConflictRewardStateAsync(rewardAuditJson: null);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("afterlife_conflict_reward_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Theory]
    [InlineData("repair_cancel", "resolved", "pressure", "won")]
    [InlineData("resolve", "resolved", "negotiate", "won")]
    [InlineData("resolve", "resolved", "withdraw", "won")]
    [InlineData("resolve", "resolved", "pressure", "voluntary_withdrawal")]
    public async Task ValidateGameStateAsync_NoRewardOutcomes_CannotGrantCurrency(
        string mode,
        string resolutionState,
        string operationType,
        string playerOutcome)
    {
        await WriteSoulStateWithInkFeathersAsync(20, "Chaos Sea");
        await WriteResolvedConflictRewardStateAsync(
            BuildConflictRewardAuditJson(
                "Chaos Sea",
                AfterlifeSpiritualConflictState.RewardCurrencyInkFeathers,
                finalAmount: 30),
            mode: mode,
            resolutionState: resolutionState,
            operationType: operationType,
            playerOutcome: playerOutcome,
            voluntary: string.Equals(playerOutcome, "voluntary_withdrawal", StringComparison.OrdinalIgnoreCase));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_reward_not_allowed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RewardAuditMustMatchCurrencyDelta()
    {
        await WriteSoulStateWithInkFeathersAsync(49, "Chaos Sea");
        await WriteResolvedConflictRewardStateAsync(BuildConflictRewardAuditJson(
            "Chaos Sea",
            AfterlifeSpiritualConflictState.RewardCurrencyInkFeathers,
            finalAmount: 30));
        await WriteRewardTurnSnapshotAsync(preTurnSoulJson: BuildSoulStateJson("Chaos Sea", inkFeathers: 20));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_reward_currency_delta_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CurrentRewardAuditWithStaleNestedTurn_IsRejected()
    {
        await WriteSoulStateWithInkFeathersAsync(50, "Chaos Sea");
        await WriteResolvedConflictRewardStateAsync(BuildConflictRewardAuditJson(
            "Chaos Sea",
            AfterlifeSpiritualConflictState.RewardCurrencyInkFeathers,
            finalAmount: 30,
            resolvedAtTurn: 0));
        await WriteRewardTurnSnapshotAsync(preTurnSoulJson: BuildSoulStateJson("Chaos Sea", inkFeathers: 20));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_reward_turn_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CurrentRewardAuditWithStaleNestedTurnStillChecksCurrencyDelta()
    {
        await WriteSoulStateWithInkFeathersAsync(49, "Chaos Sea");
        await WriteResolvedConflictRewardStateAsync(BuildConflictRewardAuditJson(
            "Chaos Sea",
            AfterlifeSpiritualConflictState.RewardCurrencyInkFeathers,
            finalAmount: 30,
            resolvedAtTurn: 1));
        await WriteRewardTurnSnapshotAsync(preTurnSoulJson: BuildSoulStateJson("Chaos Sea", inkFeathers: 20));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_reward_currency_delta_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_HardDifficultyDiceAuditRequiresOppositionModifier()
    {
        await WriteSoulStateAsync();
        await WriteGameSettingsAsync("hard");
        var diceAudit = BuildPlayerSuccessDiceAudit();
        diceAudit["difficultyAudit"] = BuildDifficultyAudit("hard");
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_hard_difficulty_missing_modifier_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{diceAudit.ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_dice_difficulty_modifier_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_HardDifficultyDiceAuditWithCanonicalModifierIsAllowed()
    {
        await WriteSoulStateAsync();
        await WriteGameSettingsAsync("hard");
        var diceAudit = BuildPlayerSuccessDiceAudit();
        AddGameDifficultyModifier(diceAudit, "hard", value: 1);
        diceAudit["oppositionTotal"] = diceAudit["oppositionTotal"]!.GetValue<int>() + 1;
        diceAudit["margin"] = diceAudit["playerTotal"]!.GetValue<int>() - diceAudit["oppositionTotal"]!.GetValue<int>();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_hard_difficulty_valid_modifier_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{diceAudit.ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_dice_difficulty_modifier_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_dice_difficulty_audit_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_HardDifficultyRejectsWrongRewardMultiplier()
    {
        await WriteGameSettingsAsync("hard");
        await WriteSoulStateWithInkFeathersAsync(50, "Chaos Sea");
        await WriteResolvedConflictRewardStateAsync(BuildConflictRewardAuditJson(
            "Chaos Sea",
            AfterlifeSpiritualConflictState.RewardCurrencyInkFeathers,
            finalAmount: 30,
            difficulty: "hard",
            difficultyRewardMultiplierPercent: 100),
            diceAuditJson: BuildHardDifficultyPlayerSuccessDiceAuditJson());
        await WriteRewardTurnSnapshotAsync(preTurnSoulJson: BuildSoulStateJson("Chaos Sea", inkFeathers: 20));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_reward_difficulty_multiplier_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_HardDifficultyRewardRequiresDifficultyAdjustedCurrencyDelta()
    {
        await WriteGameSettingsAsync("hard");
        await WriteSoulStateWithInkFeathersAsync(57, "Chaos Sea");
        await WriteResolvedConflictRewardStateAsync(BuildConflictRewardAuditJson(
            "Chaos Sea",
            AfterlifeSpiritualConflictState.RewardCurrencyInkFeathers,
            finalAmount: 37,
            difficulty: "hard",
            difficultyRewardMultiplierPercent: 125),
            diceAuditJson: BuildHardDifficultyPlayerSuccessDiceAuditJson());
        await WriteRewardTurnSnapshotAsync(preTurnSoulJson: BuildSoulStateJson("Chaos Sea", inkFeathers: 20));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_reward_final_amount_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_reward_currency_delta_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ZeroLightSparkRewardRejectsPositiveCurrencyDelta()
    {
        await WriteSoulStateWithInkFeathersAsync(20, "Shining Abode");
        await WriteShiningStateWithLightSparksAsync(6);
        await WriteResolvedConflictRewardStateAsync(BuildConflictRewardAuditJson(
            "Shining Abode",
            AfterlifeSpiritualConflictState.RewardCurrencyLightSparks,
            finalAmount: 0,
            opposingLeadStrength: 1,
            challengeTier: 1,
            startingConflictPosition: "player_dominant",
            riskMultiplierPercent: 50),
            realm: "Shining Abode");
        await WriteRewardTurnSnapshotAsync(
            preTurnSoulJson: BuildSoulStateJson("Shining Abode", inkFeathers: 20),
            preTurnShiningJson: BuildShiningStateJson(lightSparks: 5),
            preTurnConflictJson: BuildActiveConflictRootJson(realm: "Shining Abode"));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_reward_currency_delta_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ZeroLightSparkRewardAllowsZeroCurrencyDelta()
    {
        await WriteSoulStateWithInkFeathersAsync(20, "Shining Abode");
        await WriteShiningStateWithLightSparksAsync(5);
        await WriteResolvedConflictRewardStateAsync(BuildConflictRewardAuditJson(
            "Shining Abode",
            AfterlifeSpiritualConflictState.RewardCurrencyLightSparks,
            finalAmount: 0,
            opposingLeadStrength: 1,
            challengeTier: 1,
            startingConflictPosition: "player_dominant",
            riskMultiplierPercent: 50),
            realm: "Shining Abode");
        await WriteRewardTurnSnapshotAsync(
            preTurnSoulJson: BuildSoulStateJson("Shining Abode", inkFeathers: 20),
            preTurnShiningJson: BuildShiningStateJson(lightSparks: 5),
            preTurnConflictJson: BuildActiveConflictRootJson(realm: "Shining Abode"));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_reward_currency_delta_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RewardAuditOnlyCurrentTurnStillChecksInkFeatherDelta()
    {
        await WriteSoulStateWithInkFeathersAsync(49, "Chaos Sea");
        await WriteResolvedConflictRewardStateAsync(
            BuildConflictRewardAuditJson(
                "Chaos Sea",
                AfterlifeSpiritualConflictState.RewardCurrencyInkFeathers,
                finalAmount: 30,
                resolvedAtTurn: 7),
            proofResolvedAtTurn: null);
        await WriteRewardTurnSnapshotAsync(preTurnSoulJson: BuildSoulStateJson("Chaos Sea", inkFeathers: 20));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_reward_currency_delta_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RewardAuditOnlyCurrentTurnAllowsMatchingInkFeatherDelta()
    {
        await WriteSoulStateWithInkFeathersAsync(50, "Chaos Sea");
        await WriteResolvedConflictRewardStateAsync(
            BuildConflictRewardAuditJson(
                "Chaos Sea",
                AfterlifeSpiritualConflictState.RewardCurrencyInkFeathers,
                finalAmount: 30,
                resolvedAtTurn: 7),
            proofResolvedAtTurn: null);
        await WriteRewardTurnSnapshotAsync(preTurnSoulJson: BuildSoulStateJson("Chaos Sea", inkFeathers: 20));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_reward_currency_delta_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_reward_turn_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RewardAuditOnlyCurrentTurnZeroLightSparkRewardRejectsPositiveDelta()
    {
        await WriteSoulStateWithInkFeathersAsync(20, "Shining Abode");
        await WriteShiningStateWithLightSparksAsync(6);
        await WriteResolvedConflictRewardStateAsync(
            BuildConflictRewardAuditJson(
                "Shining Abode",
                AfterlifeSpiritualConflictState.RewardCurrencyLightSparks,
                finalAmount: 0,
                opposingLeadStrength: 1,
                challengeTier: 1,
                startingConflictPosition: "player_dominant",
                riskMultiplierPercent: 50,
                resolvedAtTurn: 7),
            realm: "Shining Abode",
            proofResolvedAtTurn: null);
        await WriteRewardTurnSnapshotAsync(
            preTurnSoulJson: BuildSoulStateJson("Shining Abode", inkFeathers: 20),
            preTurnShiningJson: BuildShiningStateJson(lightSparks: 5),
            preTurnConflictJson: BuildActiveConflictRootJson(realm: "Shining Abode"));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_reward_currency_delta_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CurrentRewardAuditInflatingPreTurnInputs_IsRejected()
    {
        await WriteSoulStateWithInkFeathersAsync(95, "Chaos Sea");
        await WriteResolvedConflictRewardStateAsync(
            BuildConflictRewardAuditJson(
                "Chaos Sea",
                AfterlifeSpiritualConflictState.RewardCurrencyInkFeathers,
                finalAmount: 75,
                opposingLeadStrength: 12,
                challengeTier: 5,
                startingConflictPosition: "opposition_dominant",
                riskMultiplierPercent: 150,
                resolvedAtTurn: 7),
            proofResolvedAtTurn: 7);
        await WriteRewardTurnSnapshotAsync(preTurnSoulJson: BuildSoulStateJson("Chaos Sea", inkFeathers: 20));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_reward_starting_position_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_reward_opposing_strength_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CounterRequiresIncomingAction()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_counter_without_incoming_001",
          "operationType": "counter",
          "outcome": "success",
          "before": { "conflictPosition": "contested" },
          "after": { "conflictPosition": "player_advantaged" },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_counter_missing_incoming_action", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CurrentContestedExchange_RequiresMatchupAudit()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_missing_matchup_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """, addDefaultMatchupAudit: false);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_matchup_audit_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnExchangeWithoutMatchupAudit_RemainsCompatible()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_legacy_no_matchup_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """, addDefaultMatchupAudit: false);
        await WriteValidatedConflictSnapshotFromCurrentAsync("Я продолжаю конфликт с уже существующим старым exchangeLog.");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_matchup_audit_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_LegacyExchangeWithoutMatchupAuditAndWithoutTurnBaseline_RemainsCompatible()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_legacy_no_baseline_no_matchup_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """, addDefaultMatchupAudit: false);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_matchup_audit_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CurrentNoEffectExchangeWithDiceAudit_RequiresMatchupAudit()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_no_effect_missing_matchup_001",
          "operationType": "guard",
          "outcome": "no_effect",
          "incomingAction": {
            "operationType": "pressure",
            "summary": "Лиора давит на трещину души."
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildMixedNoEffectDiceAuditJson()}}
        }
        """, addDefaultMatchupAudit: false);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_matchup_audit_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnNoEffectExchangeWithDiceAuditWithoutMatchupAudit_RemainsCompatible()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_legacy_no_effect_no_matchup_001",
          "operationType": "guard",
          "outcome": "no_effect",
          "incomingAction": {
            "operationType": "pressure",
            "summary": "Лиора давит на трещину души."
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildMixedNoEffectDiceAuditJson()}}
        }
        """, addDefaultMatchupAudit: false);
        await WriteValidatedConflictSnapshotFromCurrentAsync("Я продолжаю конфликт с уже существующим no-effect exchangeLog.");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_matchup_audit_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CurrentTerminalExchangeWithDiceAudit_RequiresMatchupAudit()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_terminal_missing_matchup_001",
          "operationType": "surrender",
          "outcome": "success",
          "resolutionSource": "contested_pressure",
          "before": {
            "playerSideStrain": "fractured",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested",
            "resolutionState": "active"
          },
          "after": {
            "playerSideStrain": "overwhelmed",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested",
            "resolutionState": "surrender_pending"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """, addDefaultMatchupAudit: false);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_matchup_audit_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CurrentTerminalExchangeWithDiceAuditAndTerminalChoiceAudit_IsAccepted()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_terminal_valid_matchup_001",
          "operationType": "surrender",
          "outcome": "success",
          "resolutionSource": "contested_pressure",
          "before": {
            "playerSideStrain": "fractured",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested",
            "resolutionState": "active"
          },
          "after": {
            "playerSideStrain": "overwhelmed",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested",
            "resolutionState": "surrender_pending"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_matchup_audit_missing", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_matchup_invalid_risk_profile", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_matchup_primary_lane_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnTerminalExchangeWithDiceAuditWithoutMatchupAudit_RemainsCompatible()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_legacy_terminal_no_matchup_001",
          "operationType": "surrender",
          "outcome": "success",
          "resolutionSource": "contested_pressure",
          "before": {
            "playerSideStrain": "fractured",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested",
            "resolutionState": "active"
          },
          "after": {
            "playerSideStrain": "overwhelmed",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested",
            "resolutionState": "surrender_pending"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """, addDefaultMatchupAudit: false);
        await WriteValidatedConflictSnapshotFromCurrentAsync("Я продолжаю конфликт с уже существующим contested surrender exchangeLog.");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_matchup_audit_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_TerminalExchangeMatchupAuditRequiresTerminalChoiceRiskProfile()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_terminal_wrong_risk_001",
          "operationType": "surrender",
          "outcome": "success",
          "resolutionSource": "contested_pressure",
          "matchupAudit": {
            "playerOperation": "surrender",
            "oppositionOperation": "pressure",
            "primaryResolutionLane": "surrender",
            "matchupRationale": "The surrender is still contested and rolled, so its terminal lane must be audited.",
            "riskProfile": "safe_defense"
          },
          "before": {
            "playerSideStrain": "fractured",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested",
            "resolutionState": "active"
          },
          "after": {
            "playerSideStrain": "overwhelmed",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested",
            "resolutionState": "surrender_pending"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_matchup_invalid_risk_profile", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MatchupAuditOppositionOperationMustMatchIncomingActionOperationType()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_matchup_incoming_mismatch_001",
          "operationType": "guard",
          "outcome": "success",
          "incomingAction": {
            "operationType": "pressure",
            "summary": "Лиора давит на трещину души."
          },
          "matchupAudit": {
            "playerOperation": "guard",
            "oppositionOperation": "maneuver",
            "primaryResolutionLane": "guard",
            "matchupRationale": "This audit contradicts the incomingAction and must be rejected.",
            "riskProfile": "safe_defense"
          },
          "before": {
            "playerSideStrain": "strained",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_matchup_opposition_operation_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MatchupAuditOppositionOperationMustMatchIncomingActionFinalOperationType()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_matchup_incoming_final_mismatch_001",
          "operationType": "guard",
          "outcome": "success",
          "incomingAction": {
            "finalOperationType": "pressure",
            "summary": "Лиора давит на трещину души."
          },
          "matchupAudit": {
            "playerOperation": "guard",
            "oppositionOperation": "none",
            "primaryResolutionLane": "guard",
            "matchupRationale": "This audit contradicts finalOperationType and must be rejected.",
            "riskProfile": "safe_defense"
          },
          "before": {
            "playerSideStrain": "strained",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_matchup_opposition_operation_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MatchupAuditOppositionOperationCanMatchIncomingActionFinalOperationTypeWhenOperationTypeExists()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_matchup_incoming_final_match_001",
          "operationType": "guard",
          "outcome": "success",
          "incomingAction": {
            "operationType": "pressure",
            "finalOperationType": "maneuver",
            "summary": "Лиора начала давить, но финальным действием сменила позицию."
          },
          "matchupAudit": {
            "playerOperation": "guard",
            "oppositionOperation": "maneuver",
            "primaryResolutionLane": "guard",
            "matchupRationale": "The audit matches the final incoming operation.",
            "riskProfile": "safe_defense"
          },
          "before": {
            "playerSideStrain": "strained",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_matchup_opposition_operation_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_OppositionActionCostUsesMatchedFinalOperationType()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_action_cost_incoming_final_match_001",
          "operationType": "guard",
          "outcome": "blocked",
          "incomingAction": {
            "operationType": "pressure",
            "finalOperationType": "force_binding",
            "summary": "Лиора начинает давить, но финально стягивает силовые оковы."
          },
          "matchupAudit": {
            "playerOperation": "guard",
            "oppositionOperation": "force_binding",
            "primaryResolutionLane": "guard",
            "matchupRationale": "Защита игрока сдерживает финальное действие противника.",
            "riskProfile": "safe_defense"
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "actionCostAudit": {
            "player": {
              "operationType": "guard",
              "baseCost": 2,
              "minCost": 1,
              "artTier": 0,
              "effectiveCost": 2,
              "before": 6,
              "after": 4
            },
            "opposition": {
              "operationType": "force_binding",
              "baseCost": 5,
              "minCost": 2,
              "artTier": 0,
              "effectiveCost": 5,
              "before": 6,
              "after": 1
            }
          }
        }
        """);
        await WritePreTurnActiveConflictSnapshotWithAuthorityAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_opposition_action_cost_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_opposition_action_cost_art_tier_authority_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_opposition_action_cost_delta_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RejectsStaleIncomingOperationWhenFinalOperationTypeExists()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_matchup_stale_incoming_operation_001",
          "operationType": "guard",
          "outcome": "blocked",
          "incomingAction": {
            "operationType": "pressure",
            "finalOperationType": "force_binding",
            "summary": "Лиора начинает давить, но финально стягивает силовые оковы."
          },
          "matchupAudit": {
            "playerOperation": "guard",
            "oppositionOperation": "pressure",
            "primaryResolutionLane": "guard",
            "matchupRationale": "This audit uses the stale initial operation instead of finalOperationType.",
            "riskProfile": "safe_defense"
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotWithAuthorityAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_matchup_opposition_operation_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MatchupAuditOppositionOperationMustMatchAnyIncomingActionOperation()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_matchup_incoming_both_mismatch_001",
          "operationType": "guard",
          "outcome": "success",
          "incomingAction": {
            "operationType": "pressure",
            "finalOperationType": "maneuver",
            "summary": "Лиора меняет приём в ходе обмена."
          },
          "matchupAudit": {
            "playerOperation": "guard",
            "oppositionOperation": "binding",
            "primaryResolutionLane": "guard",
            "matchupRationale": "This audit matches neither incoming action field.",
            "riskProfile": "safe_defense"
          },
          "before": {
            "playerSideStrain": "strained",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_matchup_opposition_operation_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MatchupAuditOppositionOperationRequiresIncomingActionOperationField()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_matchup_incoming_missing_operation_001",
          "operationType": "guard",
          "outcome": "success",
          "incomingAction": {
            "summary": "Лиора действует, но GM не указал тип приёма."
          },
          "matchupAudit": {
            "playerOperation": "guard",
            "oppositionOperation": "pressure",
            "primaryResolutionLane": "guard",
            "matchupRationale": "This audit is not backed by an incomingAction operation field.",
            "riskProfile": "safe_defense"
          },
          "before": {
            "playerSideStrain": "strained",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_matchup_opposition_operation_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MatchupAuditOppositionOperationMatchingIncomingAction_IsAccepted()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_matchup_incoming_match_001",
          "operationType": "guard",
          "outcome": "success",
          "incomingAction": {
            "operationType": "pressure",
            "summary": "Лиора давит на трещину души."
          },
          "before": {
            "playerSideStrain": "strained",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_matchup_opposition_operation_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MatchupAuditOppositionOperationWithoutIncomingAction_AllowsSupportedToken()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_matchup_no_incoming_001",
          "operationType": "pressure",
          "outcome": "success",
          "matchupAudit": {
            "playerOperation": "pressure",
            "oppositionOperation": "none",
            "primaryResolutionLane": "pressure",
            "matchupRationale": "No incomingAction exists; the audit can record none as the opposition operation.",
            "riskProfile": "offensive_pressure"
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_matchup_opposition_operation_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MatchupMatrixRejectsSuccessfulManeuverAgainstPressure()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_matchup_matrix_maneuver_pressure_001",
          "operationType": "maneuver",
          "outcome": "success",
          "matchupAudit": {
            "playerOperation": "maneuver",
            "oppositionOperation": "pressure",
            "primaryResolutionLane": "maneuver",
            "matchupRationale": "Pressure contests and stops an exposed maneuver.",
            "riskProfile": "position_play"
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "player_advantaged"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_matchup_matrix_violation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MatchupMatrixAllowsSuccessfulManeuverAgainstGuard()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_matchup_matrix_maneuver_guard_001",
          "operationType": "maneuver",
          "outcome": "success",
          "matchupAudit": {
            "playerOperation": "maneuver",
            "oppositionOperation": "guard",
            "primaryResolutionLane": "maneuver",
            "matchupRationale": "Maneuver is a valid answer to a passive guard lane.",
            "riskProfile": "position_play"
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "player_advantaged"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_matchup_matrix_violation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_DuplicateCurrentExchangeCannotReusePreTurnMatchupExemption()
    {
        await WriteSoulStateAsync();
        var duplicateExchange = $$"""
        {
          "exchangeId": "exchange_duplicate_no_matchup_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """;

        await WriteConflictStateWithRawExchangeAsync(duplicateExchange, addDefaultMatchupAudit: false);
        await WriteValidatedConflictSnapshotFromCurrentAsync("Я продолжаю конфликт с одним старым обменом без matchupAudit.");
        await WriteConflictStateWithRawExchangeLogAsync($$"""
        {{duplicateExchange}},
        {{duplicateExchange}}
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_matchup_audit_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_GuardCannotDealOppositionStrain()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_guard_deals_strain_001",
          "operationType": "guard",
          "outcome": "success",
          "incomingAction": {
            "operationType": "pressure",
            "summary": "Лиора давит на трещину души."
          },
          "before": {
            "playerSideStrain": "strained",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_guard_deals_opposition_strain", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("success")]
    [InlineData("partial_success")]
    public async Task ValidateGameStateAsync_SuccessfulGuardCannotWorsenPlayerStrain(string outcome)
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_guard_worsens_player_strain_001",
          "operationType": "guard",
          "outcome": {{JsonSerializer.Serialize(outcome)}},
          "incomingAction": {
            "operationType": "pressure",
            "summary": "Лиора давит на трещину души."
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "strained",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_guard_worsens_player_strain", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_GuardAgainstPressureCanSafelyReducePlayerStrain()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_guard_safe_floor_001",
          "operationType": "guard",
          "outcome": "success",
          "incomingAction": {
            "operationType": "pressure",
            "summary": "Лиора давит на трещину души."
          },
          "before": {
            "playerSideStrain": "strained",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_guard_deals_opposition_strain", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_guard_improves_position", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_guard_worsens_player_strain", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_matchup_audit_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SuccessfulGuardAgainstPressureRequiresTempoAdvantage()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_guard_missing_tempo_001",
          "operationType": "guard",
          "outcome": "success",
          "incomingAction": {
            "operationType": "pressure",
            "summary": "Лиора давит на трещину души."
          },
          "before": {
            "playerSideStrain": "strained",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_guard_missing_tempo_advantage", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_GuardTempoAdvantageMustBeConsumedByNextEligibleExchange()
    {
        await WriteSoulStateAsync();
        var guardExchange = AddDefaultActionCostAudit(AddDefaultMatchupAudit($$"""
        {
          "exchangeId": "exchange_guard_grants_tempo_001",
          "operationType": "guard",
          "outcome": "success",
          "incomingAction": {
            "operationType": "pressure",
            "summary": "Лиора давит на трещину души."
          },
          "before": {
            "playerSideStrain": "strained",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested",
            "tempoAdvantage": {
              "advantageId": "tempo_guard_001",
              "ownerSide": "player",
              "sourceOperation": "guard",
              "sourceExchangeId": "exchange_guard_grants_tempo_001",
              "level": "advantage",
              "status": "available",
              "summary": "Защита сорвала давление и дала темп для следующего приема."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """));
        var pressureExchange = AddDefaultActionCostAudit(AddDefaultMatchupAudit($$"""
        {
          "exchangeId": "exchange_pressure_ignores_tempo_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested",
            "tempoAdvantage": {
              "advantageId": "tempo_guard_001",
              "ownerSide": "player",
              "sourceOperation": "guard",
              "sourceExchangeId": "exchange_guard_grants_tempo_001",
              "level": "advantage",
              "status": "available",
              "summary": "Защита сорвала давление и дала темп для следующего приема."
            }
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested",
            "tempoAdvantage": {
              "advantageId": "tempo_guard_001",
              "ownerSide": "player",
              "sourceOperation": "guard",
              "sourceExchangeId": "exchange_guard_grants_tempo_001",
              "level": "advantage",
              "status": "available",
              "summary": "Защита сорвала давление и дала темп для следующего приема."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """));
        await WriteConflictStateWithRawExchangeLogAsync($$"""
              {{guardExchange}},
              {{pressureExchange}}
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_tempo_advantage_not_consumed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_GuardTempoAdvantageCanBeConsumedByNextEligibleExchange()
    {
        await WriteSoulStateAsync();
        var guardExchange = AddDefaultActionCostAudit(AddDefaultMatchupAudit($$"""
        {
          "exchangeId": "exchange_guard_grants_tempo_valid_001",
          "operationType": "guard",
          "outcome": "success",
          "incomingAction": {
            "operationType": "pressure",
            "summary": "Лиора давит на трещину души."
          },
          "before": {
            "playerSideStrain": "strained",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested",
            "tempoAdvantage": {
              "advantageId": "tempo_guard_valid_001",
              "ownerSide": "player",
              "sourceOperation": "guard",
              "sourceExchangeId": "exchange_guard_grants_tempo_valid_001",
              "level": "advantage",
              "status": "available",
              "summary": "Защита сорвала давление и дала темп для следующего приема."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """));
        var pressureExchange = AddDefaultActionCostAudit(AddDefaultMatchupAudit($$"""
        {
          "exchangeId": "exchange_pressure_consumes_tempo_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested",
            "tempoAdvantage": {
              "advantageId": "tempo_guard_valid_001",
              "ownerSide": "player",
              "sourceOperation": "guard",
              "sourceExchangeId": "exchange_guard_grants_tempo_valid_001",
              "level": "advantage",
              "status": "available",
              "summary": "Защита сорвала давление и дала темп для следующего приема."
            }
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested",
            "tempoAdvantage": {
              "advantageId": "tempo_guard_valid_001",
              "ownerSide": "player",
              "sourceOperation": "guard",
              "sourceExchangeId": "exchange_guard_grants_tempo_valid_001",
              "level": "advantage",
              "status": "consumed",
              "summary": "Темп защиты потрачен на давление."
            }
          },
          "diceAudit": {{BuildPlayerTieredRollDiceAudit(
              "advantage",
              new JsonNode?[]
              {
                  new JsonObject
                  {
                      ["sourceType"] = "guard_tempo_window",
                      ["sourceId"] = "tempo_guard_valid_001",
                      ["level"] = "advantage",
                      ["summary"] = "Темповое окно после успешной защиты."
                  }
              },
              Array.Empty<JsonNode?>(),
              new (int SourceIndex, int Value, bool Selected)[] { (0, 5, false), (1, 18, true) }).ToJsonString()}}
        }
        """));
        await WriteConflictStateWithRawExchangeLogAsync($$"""
              {{guardExchange}},
              {{pressureExchange}}
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_tempo_advantage_not_consumed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_tempo_advantage_missing_roll_mode_source", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SetbackGuardAgainstPressureMustStillMitigatePlayerStrain()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_guard_setback_no_mitigation_001",
          "operationType": "guard",
          "outcome": "setback",
          "incomingAction": {
            "operationType": "pressure",
            "summary": "Лиора давит на трещину души."
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "fractured",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildMixedNoEffectDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_guard_missing_mitigation_floor", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PressureCannotActAsFreeManeuver()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_pressure_free_maneuver_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "player_advantaged"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_pressure_changes_position", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_OppositionControlRestrictsListedSuccessfulOperation()
    {
        const string activeControl = """
        {
          "level": "hindered",
          "controllerSide": "opposition",
          "controlId": "control_restricts_pressure_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "pressure" ],
          "summary": "Оковы мешают игроку давить напрямую."
        }
        """;

        await WriteSoulStateAsync();
        var preTurnRoot = JsonNode.Parse(BuildActiveConflictRootJson())!.AsObject();
        var preTurnActive = Assert.IsType<JsonObject>(preTurnRoot["activeConflict"]);
        preTurnActive["controlState"] = JsonNode.Parse(activeControl);
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, preTurnRoot.ToJsonString());
        await WriteValidatedConflictSnapshotFromCurrentAsync("Я продолжаю духовный бой под оковами противника.");

        var currentRoot = preTurnRoot.DeepClone().AsObject();
        var currentActive = Assert.IsType<JsonObject>(currentRoot["activeConflict"]);
        var exchangeLog = Assert.IsType<JsonArray>(currentActive["exchangeLog"]);
        exchangeLog.Add(JsonNode.Parse(AddDefaultMatchupAudit($$"""
        {
          "exchangeId": "exchange_restricted_pressure_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested",
            "controlState": {{activeControl}}
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested",
            "controlState": {{activeControl}}
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """)));
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, currentRoot.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_operation_restricted_by_control", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CurrentExchangeRequiresActionCostAudit()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_missing_action_cost_audit_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """, addDefaultActionCostAudit: false);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_action_cost_audit_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CurrentIncomingActionRequiresOppositionActionCostAudit()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_missing_opposition_action_cost_audit_001",
          "operationType": "guard",
          "outcome": "blocked",
          "incomingAction": {
            "operationType": "pressure",
            "actorType": "guardian",
            "actorId": "guardian_liora",
            "summary": "Лиора давит на клятву души."
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}},
          "actionCostAudit": {
            "player": {
              "operationType": "guard",
              "baseCost": 2,
              "minCost": 1,
              "artTier": 0,
              "effectiveCost": 2,
              "before": 6,
              "after": 4
            }
          }
        }
        """);
        await WritePreTurnActiveConflictSnapshotWithAuthorityAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_opposition_action_cost_audit_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_TerminalPlayerActionStillRequiresOppositionActionCostAudit()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_terminal_missing_opposition_action_cost_audit_001",
          "operationType": "negotiate",
          "outcome": "blocked",
          "incomingAction": {
            "operationType": "pressure",
            "actorType": "guardian",
            "actorId": "guardian_liora",
            "summary": "Лиора давит на клятву души, пока игрок пытается договориться."
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "player_dominant"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "player_dominant"
          }
        }
        """);
        await WritePreTurnActiveConflictSnapshotWithAuthorityAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_opposition_action_cost_audit_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_AcceptsOppositionActionCostAuditFromEntityProfile()
    {
        await WriteSoulStateAsync();
        await WriteAfterlifeEntityProfilesWithGuardianLioraStandardArtsAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_valid_opposition_action_cost_audit_001",
          "operationType": "guard",
          "outcome": "blocked",
          "incomingAction": {
            "operationType": "pressure",
            "actorType": "guardian",
            "actorId": "guardian_liora",
            "summary": "Лиора давит на клятву души."
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}},
          "actionCostAudit": {
            "player": {
              "operationType": "guard",
              "baseCost": 2,
              "minCost": 1,
              "artTier": 0,
              "effectiveCost": 2,
              "before": 6,
              "after": 4
            },
            "opposition": {
              "operationType": "pressure",
              "baseCost": 3,
              "minCost": 1,
              "artTier": 2,
              "effectiveCost": 1,
              "before": 6,
              "after": 5
            }
          }
        }
        """);
        await WritePreTurnActiveConflictSnapshotWithAuthorityAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_opposition_action_cost_audit_missing", StringComparison.OrdinalIgnoreCase) ||
            issue.Code?.StartsWith("afterlife_conflict_opposition_action_cost", StringComparison.OrdinalIgnoreCase) == true ||
            string.Equals(issue.Code, "afterlife_conflict_action_economy_opposition_delta_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ActionCostAuditMustMatchTierFormula()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_bad_action_cost_formula_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}},
          "actionCostAudit": {
            "player": {
              "operationType": "pressure",
              "baseCost": 3,
              "minCost": 1,
              "artTier": 0,
              "effectiveCost": 2,
              "before": 6,
              "after": 4
            }
          }
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_action_cost_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RejectsActionCostArtTierAboveAuthorityProfile()
    {
        await WriteSoulStateWithAfterlifeCombatProfileAsync("Chaos Sea", """
        {
          "schemaVersion": 1,
          "enlightenmentRank": 1,
          "radianceRank": 0,
          "retainedRadianceRank": 0,
          "spiritFocusTier": 0,
          "lastRecoveryTurn": 0,
          "artTiers": { "pressure": 0 }
        }
        """);
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_forged_action_tier_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}},
          "actionCostAudit": {
            "player": {
              "operationType": "pressure",
              "baseCost": 3,
              "minCost": 1,
              "artTier": 5,
              "effectiveCost": 1,
              "before": 6,
              "after": 5
            }
          }
        }
        """);
        await WritePreTurnActiveConflictSnapshotWithAuthorityAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_action_cost_art_tier_authority_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RejectsSpecialArtCostWhenPlayerHasNotLearnedArt()
    {
        await WriteSoulStateWithAfterlifeCombatProfileAsync("Chaos Sea", """
        {
          "schemaVersion": 1,
          "enlightenmentRank": 1,
          "radianceRank": 0,
          "retainedRadianceRank": 0,
          "spiritFocusTier": 0,
          "lastRecoveryTurn": 0,
          "artTiers": { "pressure": 2 }
        }
        """);
        await WriteAfterlifeEntityProfilesWithPlayerSpecialArtsAsync("[]");
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_unlearned_special_art_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}},
          "specialArtAudit": {
            "artId": "mirror_pressure",
            "displayName": "Зеркальное Давление",
            "ownerActorType": "player_soul",
            "ownerActorId": "player_soul",
            "baseOperation": "pressure",
            "costMultiplierPercent": 150,
            "effectNote": "Зеркальное давление раздваивает импульс и усиливает нажим на противника."
          },
          "actionCostAudit": {
            "player": {
              "operationType": "pressure",
              "baseCost": 3,
              "minCost": 1,
              "artTier": 2,
              "specialArtId": "mirror_pressure",
              "specialCostMultiplierPercent": 150,
              "standardEffectiveCost": 1,
              "effectiveCost": 2,
              "before": 6,
              "after": 4
            }
          }
        }
        """);
        await WritePreTurnActiveConflictSnapshotWithAuthorityAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_special_art_not_learned", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_AcceptsSpecialArtCostFromLearnedPlayerProfile()
    {
        await WriteSoulStateWithAfterlifeCombatProfileAsync("Chaos Sea", """
        {
          "schemaVersion": 1,
          "enlightenmentRank": 1,
          "radianceRank": 0,
          "retainedRadianceRank": 0,
          "spiritFocusTier": 0,
          "lastRecoveryTurn": 0,
          "artTiers": { "pressure": 2 }
        }
        """);
        await WriteAfterlifeEntityProfilesWithPlayerSpecialArtsAsync("""
        [
          {
            "artId": "mirror_pressure",
            "displayName": "Зеркальное Давление",
            "ownerActorType": "player_soul",
            "ownerActorId": "player_soul",
            "baseOperation": "pressure",
            "tier": 2,
            "costMultiplierPercent": 150,
            "upgradeCost": { "inkFeathers": 40, "lightSparks": 0 },
            "effectSummary": "Давление отражает часть обета."
          }
        ]
        """);
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_learned_special_art_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}},
          "specialArtAudit": {
            "artId": "mirror_pressure",
            "displayName": "Зеркальное Давление",
            "ownerActorType": "player_soul",
            "ownerActorId": "player_soul",
            "baseOperation": "pressure",
            "costMultiplierPercent": 150,
            "effectNote": "Зеркальное давление раздваивает импульс и усиливает нажим на противника."
          },
          "actionCostAudit": {
            "player": {
              "operationType": "pressure",
              "baseCost": 3,
              "minCost": 1,
              "artTier": 2,
              "specialArtId": "mirror_pressure",
              "specialCostMultiplierPercent": 150,
              "standardEffectiveCost": 1,
              "effectiveCost": 2,
              "before": 6,
              "after": 4
            }
          }
        }
        """);
        await WritePreTurnActiveConflictSnapshotWithAuthorityAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_special_art_not_learned", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_special_art_authority_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_action_cost_art_tier_authority_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_special_art_cost_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RejectsSpecialArtCostWithoutSpecialArtAudit()
    {
        await WriteSoulStateWithAfterlifeCombatProfileAsync("Chaos Sea", """
        {
          "schemaVersion": 1,
          "enlightenmentRank": 1,
          "radianceRank": 0,
          "retainedRadianceRank": 0,
          "spiritFocusTier": 0,
          "lastRecoveryTurn": 0,
          "artTiers": { "pressure": 2 }
        }
        """);
        await WriteAfterlifeEntityProfilesWithPlayerSpecialArtsAsync("""
        [
          {
            "artId": "mirror_pressure",
            "displayName": "Зеркальное Давление",
            "ownerActorType": "player_soul",
            "ownerActorId": "player_soul",
            "baseOperation": "pressure",
            "tier": 2,
            "costMultiplierPercent": 150,
            "upgradeCost": { "inkFeathers": 40, "lightSparks": 0 },
            "effectSummary": "Давление отражает часть обета."
          }
        ]
        """);
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_special_art_cost_without_audit_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}},
          "actionCostAudit": {
            "player": {
              "operationType": "pressure",
              "baseCost": 3,
              "minCost": 1,
              "artTier": 2,
              "specialArtId": "mirror_pressure",
              "specialCostMultiplierPercent": 150,
              "standardEffectiveCost": 1,
              "effectiveCost": 2,
              "before": 6,
              "after": 4
            }
          }
        }
        """);
        await WritePreTurnActiveConflictSnapshotWithAuthorityAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_special_art_cost_audit_incomplete", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RejectsNonPlayerSpecialArtWithoutOppositionOperation()
    {
        await WriteSoulStateWithAfterlifeCombatProfileAsync("Chaos Sea", """
        {
          "schemaVersion": 1,
          "enlightenmentRank": 1,
          "radianceRank": 0,
          "retainedRadianceRank": 0,
          "spiritFocusTier": 0,
          "lastRecoveryTurn": 0,
          "artTiers": { "pressure": 2 }
        }
        """);
        await WriteAfterlifeEntityProfilesWithGuardianSpecialArtsAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_non_player_special_art_without_opposition_001",
          "operationType": "pressure",
          "outcome": "success",
          "matchupAudit": {
            "playerOperation": "pressure",
            "oppositionOperation": "none",
            "primaryResolutionLane": "pressure",
            "matchupRationale": "Игрок давит на противника без активного встречного приема.",
            "riskProfile": "offensive_pressure"
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}},
          "specialArtAudit": {
            "artId": "mirror_pressure",
            "displayName": "Зеркальное Давление",
            "ownerActorType": "guardian",
            "ownerActorId": "guardian_mirror",
            "baseOperation": "pressure",
            "costMultiplierPercent": 150,
            "effectNote": "Это особое искусство Хранителя ошибочно привязано к действию игрока."
          },
          "actionCostAudit": {
            "player": {
              "operationType": "pressure",
              "baseCost": 3,
              "minCost": 1,
              "artTier": 2,
              "effectiveCost": 1,
              "before": 6,
              "after": 5
            }
          }
        }
        """);
        await WritePreTurnActiveConflictSnapshotWithAuthorityAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_special_art_base_operation_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RejectsNonPlayerSpecialArtThatDoesNotMatchResolvedOppositionOperation()
    {
        await WriteSoulStateWithAfterlifeCombatProfileAsync("Chaos Sea", """
        {
          "schemaVersion": 1,
          "enlightenmentRank": 1,
          "radianceRank": 0,
          "retainedRadianceRank": 0,
          "spiritFocusTier": 0,
          "lastRecoveryTurn": 0,
          "artTiers": { "guard": 1 }
        }
        """);
        await WriteAfterlifeEntityProfilesWithGuardianSpecialArtsAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_non_player_special_art_wrong_resolved_operation_001",
          "operationType": "guard",
          "outcome": "blocked",
          "incomingAction": {
            "operationType": "pressure",
            "finalOperationType": "force_binding",
            "actorType": "guardian",
            "actorId": "guardian_mirror",
            "summary": "Хранитель начинает давить, но финально стягивает силовые оковы."
          },
          "matchupAudit": {
            "playerOperation": "guard",
            "oppositionOperation": "force_binding",
            "primaryResolutionLane": "guard",
            "matchupRationale": "Защита игрока сдерживает финальное действие противника.",
            "riskProfile": "safe_defense"
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}},
          "specialArtAudit": {
            "artId": "mirror_pressure",
            "displayName": "Зеркальное Давление",
            "ownerActorType": "guardian",
            "ownerActorId": "guardian_mirror",
            "baseOperation": "pressure",
            "costMultiplierPercent": 150,
            "effectNote": "Аудит ошибочно применяет особое давление к финальным силовым оковам."
          },
          "actionCostAudit": {
            "player": {
              "operationType": "guard",
              "baseCost": 2,
              "minCost": 1,
              "artTier": 1,
              "effectiveCost": 1,
              "before": 6,
              "after": 5
            },
            "opposition": {
              "operationType": "force_binding",
              "baseCost": 5,
              "minCost": 2,
              "artTier": 0,
              "effectiveCost": 5,
              "before": 6,
              "after": 1
            }
          }
        }
        """);
        await WritePreTurnActiveConflictSnapshotWithAuthorityAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_special_art_base_operation_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RejectsMixedSingularAndArraySpecialArtAudits()
    {
        await WriteSoulStateWithAfterlifeCombatProfileAsync("Chaos Sea", """
        {
          "schemaVersion": 1,
          "enlightenmentRank": 1,
          "radianceRank": 0,
          "retainedRadianceRank": 0,
          "spiritFocusTier": 0,
          "lastRecoveryTurn": 0,
          "artTiers": { "guard": 1 }
        }
        """);
        await WriteAfterlifeEntityProfilesWithPlayerAndGuardianSpecialArtsAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_mixed_special_art_audit_surfaces_001",
          "operationType": "guard",
          "outcome": "blocked",
          "incomingAction": {
            "operationType": "pressure",
            "actorType": "guardian",
            "actorId": "guardian_mirror",
            "summary": "Хранитель давит через Зеркальное Давление."
          },
          "matchupAudit": {
            "playerOperation": "guard",
            "oppositionOperation": "pressure",
            "primaryResolutionLane": "guard",
            "matchupRationale": "Защита игрока сдерживает особое давление.",
            "riskProfile": "safe_defense"
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}},
          "specialArtAudit": {
            "artId": "mirror_guard",
            "displayName": "Зеркальная Защита",
            "ownerActorType": "player_soul",
            "ownerActorId": "player_soul",
            "baseOperation": "guard",
            "costMultiplierPercent": 150,
            "effectNote": "Одиночный аудит описывает особую защиту игрока."
          },
          "specialArtAudits": [
            {
              "artId": "mirror_pressure",
              "displayName": "Зеркальное Давление",
              "ownerActorType": "guardian",
              "ownerActorId": "guardian_mirror",
              "baseOperation": "pressure",
              "costMultiplierPercent": 150,
              "effectNote": "Массивный аудит дублирует одиночный."
            }
          ],
          "actionCostAudit": {
            "player": {
              "operationType": "guard",
              "baseCost": 2,
              "minCost": 1,
              "artTier": 2,
              "specialArtId": "mirror_guard",
              "specialCostMultiplierPercent": 150,
              "standardEffectiveCost": 1,
              "effectiveCost": 2,
              "before": 6,
              "after": 4
            },
            "opposition": {
              "operationType": "pressure",
              "baseCost": 3,
              "minCost": 1,
              "artTier": 3,
              "specialArtId": "mirror_pressure",
              "specialCostMultiplierPercent": 150,
              "standardEffectiveCost": 1,
              "effectiveCost": 2,
              "before": 6,
              "after": 4
            }
          }
        }
        """);
        await WritePreTurnActiveConflictSnapshotWithAuthorityAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_special_art_audit_ambiguous", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RejectsPlayerSpecialArtCostAuditWithMismatchedSpecialArtId()
    {
        await WriteSoulStateWithAfterlifeCombatProfileAsync("Chaos Sea", """
        {
          "schemaVersion": 1,
          "enlightenmentRank": 1,
          "radianceRank": 0,
          "retainedRadianceRank": 0,
          "spiritFocusTier": 0,
          "lastRecoveryTurn": 0,
          "artTiers": { "pressure": 2 }
        }
        """);
        await WriteAfterlifeEntityProfilesWithPlayerSpecialArtsAsync("""
        [
          {
            "artId": "mirror_pressure",
            "displayName": "Зеркальное Давление",
            "ownerActorType": "player_soul",
            "ownerActorId": "player_soul",
            "baseOperation": "pressure",
            "tier": 2,
            "costMultiplierPercent": 150,
            "upgradeCost": { "inkFeathers": 40, "lightSparks": 0 },
            "effectSummary": "Давление отражает часть обета."
          }
        ]
        """);
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_player_special_art_wrong_cost_id_001",
          "operationType": "pressure",
          "outcome": "success",
          "matchupAudit": {
            "playerOperation": "pressure",
            "oppositionOperation": "none",
            "primaryResolutionLane": "pressure",
            "matchupRationale": "Противник не успевает оформить ответное действие.",
            "riskProfile": "offensive_pressure"
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}},
          "specialArtAudit": {
            "artId": "mirror_pressure",
            "displayName": "Зеркальное Давление",
            "ownerActorType": "player_soul",
            "ownerActorId": "player_soul",
            "baseOperation": "pressure",
            "costMultiplierPercent": 150,
            "effectNote": "Зеркальное давление раздваивает импульс и усиливает нажим на противника."
          },
          "actionCostAudit": {
            "player": {
              "operationType": "pressure",
              "baseCost": 3,
              "minCost": 1,
              "artTier": 2,
              "specialArtId": "wrong_pressure",
              "effectiveCost": 1,
              "before": 6,
              "after": 5
            }
          }
        }
        """);
        await WritePreTurnActiveConflictSnapshotWithAuthorityAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_special_art_cost_audit_incomplete", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_AcceptsNonPlayerIncomingSpecialArtFromOwnerProfile()
    {
        await WriteSoulStateWithAfterlifeCombatProfileAsync("Chaos Sea", """
        {
          "schemaVersion": 1,
          "enlightenmentRank": 1,
          "radianceRank": 0,
          "retainedRadianceRank": 0,
          "spiritFocusTier": 0,
          "lastRecoveryTurn": 0,
          "artTiers": { "guard": 1 }
        }
        """);
        await WriteAfterlifeEntityProfilesWithGuardianSpecialArtsAsync();
        await WriteConflictStateWithRawExchangeAsync("""
        {
          "exchangeId": "exchange_guardian_special_art_001",
          "operationType": "guard",
          "outcome": "blocked",
          "incomingAction": {
            "operationType": "pressure",
            "actorType": "guardian",
            "actorId": "guardian_mirror"
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "specialArtAudit": {
            "artId": "mirror_pressure",
            "displayName": "Зеркальное Давление",
            "ownerActorType": "guardian",
            "ownerActorId": "guardian_mirror",
            "baseOperation": "pressure",
            "costMultiplierPercent": 150,
            "effectNote": "Зеркальное давление Хранителя усилило входящий нажим, но защита игрока его остановила."
          },
          "actionCostAudit": {
            "player": {
              "operationType": "guard",
              "baseCost": 2,
              "minCost": 1,
              "artTier": 1,
              "effectiveCost": 1,
              "before": 6,
              "after": 5
            },
            "opposition": {
              "operationType": "pressure",
              "baseCost": 3,
              "minCost": 1,
              "artTier": 3,
              "specialArtId": "mirror_pressure",
              "specialCostMultiplierPercent": 150,
              "standardEffectiveCost": 1,
              "effectiveCost": 2,
              "before": 6,
              "after": 4
            }
          }
        }
        """);
        await WritePreTurnActiveConflictSnapshotWithAuthorityAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_special_art_base_operation_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_special_art_not_in_owner_profile", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_special_art_authority_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_special_art_cost_audit_incomplete", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_special_art_cost_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_opposition_special_art_cost_audit_incomplete", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_opposition_special_art_cost_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_opposition_action_cost_audit_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RejectsOppositionSpecialArtCostAuditWithMismatchedSpecialArtId()
    {
        await WriteSoulStateWithAfterlifeCombatProfileAsync("Chaos Sea", """
        {
          "schemaVersion": 1,
          "enlightenmentRank": 1,
          "radianceRank": 0,
          "retainedRadianceRank": 0,
          "spiritFocusTier": 0,
          "lastRecoveryTurn": 0,
          "artTiers": { "guard": 1 }
        }
        """);
        await WriteAfterlifeEntityProfilesWithGuardianSpecialArtsAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_opposition_special_art_wrong_cost_id_001",
          "operationType": "guard",
          "outcome": "blocked",
          "incomingAction": {
            "operationType": "pressure",
            "actorType": "guardian",
            "actorId": "guardian_mirror"
          },
          "matchupAudit": {
            "playerOperation": "guard",
            "oppositionOperation": "pressure",
            "primaryResolutionLane": "guard",
            "matchupRationale": "Защита игрока блокирует прямое давление Хранителя.",
            "riskProfile": "safe_defense"
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "specialArtAudit": {
            "artId": "mirror_pressure",
            "displayName": "Зеркальное Давление",
            "ownerActorType": "guardian",
            "ownerActorId": "guardian_mirror",
            "baseOperation": "pressure",
            "costMultiplierPercent": 150,
            "effectNote": "Зеркальное давление Хранителя усилило входящий нажим, но защита игрока его остановила."
          },
          "actionCostAudit": {
            "player": {
              "operationType": "guard",
              "baseCost": 2,
              "minCost": 1,
              "artTier": 1,
              "effectiveCost": 1,
              "before": 6,
              "after": 5
            },
            "opposition": {
              "operationType": "pressure",
              "baseCost": 3,
              "minCost": 1,
              "artTier": 3,
              "specialArtId": "wrong_pressure",
              "effectiveCost": 1,
              "before": 6,
              "after": 5
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotWithAuthorityAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_opposition_special_art_cost_audit_incomplete", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_AcceptsPlayerAndOppositionSpecialArtAuditsInSameExchange()
    {
        await WriteSoulStateWithAfterlifeCombatProfileAsync("Chaos Sea", """
        {
          "schemaVersion": 1,
          "enlightenmentRank": 1,
          "radianceRank": 0,
          "retainedRadianceRank": 0,
          "spiritFocusTier": 0,
          "lastRecoveryTurn": 0,
          "artTiers": { "guard": 1 }
        }
        """);
        await WriteAfterlifeEntityProfilesWithPlayerAndGuardianSpecialArtsAsync();
        await WriteConflictStateWithRawExchangeAsync("""
        {
          "exchangeId": "exchange_dual_special_art_001",
          "operationType": "guard",
          "outcome": "blocked",
          "incomingAction": {
            "operationType": "pressure",
            "actorType": "guardian",
            "actorId": "guardian_mirror"
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "specialArtAudits": [
            {
              "artId": "mirror_guard",
              "displayName": "Зеркальная Защита",
              "ownerActorType": "player_soul",
              "ownerActorId": "player_soul",
              "baseOperation": "guard",
              "costMultiplierPercent": 150,
              "effectNote": "Зеркальная защита игрока собрала удар в отражающую грань."
            },
            {
              "artId": "mirror_pressure",
              "displayName": "Зеркальное Давление",
              "ownerActorType": "guardian",
              "ownerActorId": "guardian_mirror",
              "baseOperation": "pressure",
              "costMultiplierPercent": 150,
              "effectNote": "Зеркальное давление Хранителя раздвоило входящий нажим."
            }
          ],
          "actionCostAudit": {
            "player": {
              "operationType": "guard",
              "baseCost": 2,
              "minCost": 1,
              "artTier": 2,
              "specialArtId": "mirror_guard",
              "specialCostMultiplierPercent": 150,
              "standardEffectiveCost": 1,
              "effectiveCost": 2,
              "before": 6,
              "after": 4
            },
            "opposition": {
              "operationType": "pressure",
              "baseCost": 3,
              "minCost": 1,
              "artTier": 3,
              "specialArtId": "mirror_pressure",
              "specialCostMultiplierPercent": 150,
              "standardEffectiveCost": 1,
              "effectiveCost": 2,
              "before": 6,
              "after": 4
            }
          }
        }
        """);
        await WritePreTurnActiveConflictSnapshotWithAuthorityAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_action_cost_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_action_cost_art_tier_authority_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_special_art_cost_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_special_art_cost_audit_incomplete", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_opposition_action_cost_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_opposition_action_cost_art_tier_authority_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_opposition_special_art_cost_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_opposition_special_art_cost_audit_incomplete", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_special_art_base_operation_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_special_art_not_in_owner_profile", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_special_art_authority_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RejectsMultiplePlayerSpecialArtCostDriversForOneExchange()
    {
        await WriteSoulStateWithAfterlifeCombatProfileAsync("Chaos Sea", """
        {
          "schemaVersion": 1,
          "enlightenmentRank": 1,
          "radianceRank": 0,
          "retainedRadianceRank": 0,
          "spiritFocusTier": 0,
          "lastRecoveryTurn": 0,
          "artTiers": { "guard": 1 }
        }
        """);
        await WriteAfterlifeEntityProfilesWithPlayerAndGuardianSpecialArtsAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_ambiguous_player_special_art_001",
          "operationType": "guard",
          "outcome": "blocked",
          "incomingAction": {
            "operationType": "pressure",
            "actorType": "guardian",
            "actorId": "guardian_mirror"
          },
          "matchupAudit": {
            "playerOperation": "guard",
            "oppositionOperation": "pressure",
            "primaryResolutionLane": "guard",
            "matchupRationale": "Защита игрока блокирует прямое давление Хранителя.",
            "riskProfile": "safe_defense"
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "specialArtAudits": [
            {
              "artId": "mirror_guard",
              "displayName": "Зеркальная Защита",
              "ownerActorType": "player_soul",
              "ownerActorId": "player_soul",
              "baseOperation": "guard",
              "costMultiplierPercent": 150,
              "effectNote": "Зеркальная защита игрока собрала удар в отражающую грань."
            },
            {
              "artId": "echo_guard",
              "displayName": "Эхо-Защита",
              "ownerActorType": "player_soul",
              "ownerActorId": "player_soul",
              "baseOperation": "guard",
              "costMultiplierPercent": 160,
              "effectNote": "Эхо-Защита дополнительно исказила входящий нажим."
            }
          ],
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}},
          "actionCostAudit": {
            "player": {
              "operationType": "guard",
              "baseCost": 2,
              "minCost": 1,
              "artTier": 2,
              "specialArtId": "mirror_guard",
              "specialCostMultiplierPercent": 150,
              "standardEffectiveCost": 1,
              "effectiveCost": 2,
              "before": 6,
              "after": 4
            },
            "opposition": {
              "operationType": "pressure",
              "baseCost": 3,
              "minCost": 1,
              "artTier": 3,
              "effectiveCost": 1,
              "before": 6,
              "after": 5
            }
          }
        }
        """);
        await WritePreTurnActiveConflictSnapshotWithAuthorityAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_special_art_cost_binding_ambiguous", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_UsesOppositionSpecialArtTierForActionCost()
    {
        await WriteSoulStateWithAfterlifeCombatProfileAsync("Chaos Sea", """
        {
          "schemaVersion": 1,
          "enlightenmentRank": 1,
          "radianceRank": 0,
          "retainedRadianceRank": 0,
          "spiritFocusTier": 0,
          "lastRecoveryTurn": 0,
          "artTiers": { "guard": 1 }
        }
        """);
        await WriteAfterlifeEntityProfilesWithGuardianSpecialArtTierAsync(standardPressureTier: 0, specialPressureTier: 3);
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_guardian_special_art_tier_001",
          "operationType": "guard",
          "outcome": "blocked",
          "incomingAction": {
            "operationType": "pressure",
            "actorType": "guardian",
            "actorId": "guardian_mirror"
          },
          "matchupAudit": {
            "playerOperation": "guard",
            "oppositionOperation": "pressure",
            "primaryResolutionLane": "guard",
            "matchupRationale": "Защита игрока блокирует прямое давление Хранителя.",
            "riskProfile": "safe_defense"
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "specialArtAudit": {
            "artId": "mirror_pressure",
            "displayName": "Зеркальное Давление",
            "ownerActorType": "guardian",
            "ownerActorId": "guardian_mirror",
            "baseOperation": "pressure",
            "costMultiplierPercent": 150,
            "effectNote": "Хранитель использует развитое особое давление вместо обычного приема."
          },
          "actionCostAudit": {
            "player": {
              "operationType": "guard",
              "baseCost": 2,
              "minCost": 1,
              "artTier": 1,
              "effectiveCost": 1,
              "before": 6,
              "after": 5
            },
            "opposition": {
              "operationType": "pressure",
              "baseCost": 3,
              "minCost": 1,
              "artTier": 3,
              "specialArtId": "mirror_pressure",
              "specialCostMultiplierPercent": 150,
              "standardEffectiveCost": 1,
              "effectiveCost": 2,
              "before": 6,
              "after": 4
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotWithAuthorityAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_opposition_action_cost_art_tier_authority_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_opposition_special_art_cost_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_opposition_special_art_cost_audit_incomplete", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RejectsOppositionSpecialArtOwnedByDifferentActor()
    {
        await WriteSoulStateWithAfterlifeCombatProfileAsync("Chaos Sea", """
        {
          "schemaVersion": 1,
          "enlightenmentRank": 1,
          "radianceRank": 0,
          "retainedRadianceRank": 0,
          "spiritFocusTier": 0,
          "lastRecoveryTurn": 0,
          "artTiers": { "guard": 1 }
        }
        """);
        await WriteAfterlifeEntityProfilesWithMirrorAndEchoGuardianSpecialArtsAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_wrong_owner_special_art_001",
          "operationType": "guard",
          "outcome": "blocked",
          "incomingAction": {
            "operationType": "pressure",
            "actorType": "guardian",
            "actorId": "guardian_mirror"
          },
          "matchupAudit": {
            "playerOperation": "guard",
            "oppositionOperation": "pressure",
            "primaryResolutionLane": "guard",
            "matchupRationale": "Защита игрока блокирует прямое давление Хранителя.",
            "riskProfile": "safe_defense"
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "specialArtAudit": {
            "artId": "echo_pressure",
            "displayName": "Эхо-Давление",
            "ownerActorType": "guardian",
            "ownerActorId": "guardian_echo",
            "baseOperation": "pressure",
            "costMultiplierPercent": 150,
            "effectNote": "Аудит пытается списать особое искусство другого Хранителя."
          },
          "actionCostAudit": {
            "player": {
              "operationType": "guard",
              "baseCost": 2,
              "minCost": 1,
              "artTier": 1,
              "effectiveCost": 1,
              "before": 6,
              "after": 5
            },
            "opposition": {
              "operationType": "pressure",
              "baseCost": 3,
              "minCost": 1,
              "artTier": 3,
              "specialArtId": "echo_pressure",
              "specialCostMultiplierPercent": 150,
              "standardEffectiveCost": 1,
              "effectiveCost": 2,
              "before": 6,
              "after": 4
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotWithAuthorityAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_opposition_special_art_owner_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_NonPlayerIncomingSpecialArtRequiresScaledOppositionCost()
    {
        await WriteSoulStateWithAfterlifeCombatProfileAsync("Chaos Sea", """
        {
          "schemaVersion": 1,
          "enlightenmentRank": 1,
          "radianceRank": 0,
          "retainedRadianceRank": 0,
          "spiritFocusTier": 0,
          "lastRecoveryTurn": 0,
          "artTiers": { "guard": 1 }
        }
        """);
        await WriteAfterlifeEntityProfilesWithGuardianSpecialArtsAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_non_player_special_art_standard_opposition_cost_001",
          "operationType": "guard",
          "outcome": "blocked",
          "incomingAction": {
            "operationType": "pressure",
            "actorType": "guardian",
            "actorId": "guardian_mirror"
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}},
          "specialArtAudit": {
            "artId": "mirror_pressure",
            "displayName": "Зеркальное Давление",
            "ownerActorType": "guardian",
            "ownerActorId": "guardian_mirror",
            "baseOperation": "pressure",
            "costMultiplierPercent": 150,
            "effectNote": "Зеркальное давление Хранителя усилило входящий нажим, но защита игрока его остановила."
          },
          "actionCostAudit": {
            "player": {
              "operationType": "guard",
              "baseCost": 2,
              "minCost": 1,
              "artTier": 1,
              "effectiveCost": 1,
              "before": 6,
              "after": 5
            },
            "opposition": {
              "operationType": "pressure",
              "baseCost": 3,
              "minCost": 1,
              "artTier": 3,
              "effectiveCost": 1,
              "before": 6,
              "after": 5
            }
          }
        }
        """);
        await WritePreTurnActiveConflictSnapshotWithAuthorityAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_opposition_special_art_cost_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_opposition_special_art_cost_audit_incomplete", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RejectsNonPlayerSpecialArtAuthorityMismatch()
    {
        await WriteSoulStateWithAfterlifeCombatProfileAsync("Chaos Sea", """
        {
          "schemaVersion": 1,
          "enlightenmentRank": 1,
          "radianceRank": 0,
          "retainedRadianceRank": 0,
          "spiritFocusTier": 0,
          "lastRecoveryTurn": 0,
          "artTiers": { "guard": 1 }
        }
        """);
        await WriteAfterlifeEntityProfilesWithGuardianSpecialArtsAsync();
        await WriteConflictStateWithRawExchangeAsync("""
        {
          "exchangeId": "exchange_guardian_special_art_forged_001",
          "operationType": "guard",
          "outcome": "blocked",
          "incomingAction": {
            "operationType": "pressure",
            "actorType": "guardian",
            "actorId": "guardian_mirror"
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "specialArtAudit": {
            "artId": "mirror_pressure",
            "displayName": "Зеркальное Давление",
            "ownerActorType": "guardian",
            "ownerActorId": "guardian_mirror",
            "baseOperation": "pressure",
            "costMultiplierPercent": 250,
            "effectNote": "ГМ завысил множитель особого искусства."
          },
          "actionCostAudit": {
            "player": {
              "operationType": "guard",
              "baseCost": 2,
              "minCost": 1,
              "artTier": 1,
              "effectiveCost": 1,
              "before": 6,
              "after": 5
            }
          }
        }
        """);
        await WritePreTurnActiveConflictSnapshotWithAuthorityAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_special_art_authority_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ActionCostAuditRejectsOverspend()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_action_cost_overspend_001",
          "operationType": "counter",
          "outcome": "success",
          "incomingAction": {
            "operationType": "pressure",
            "summary": "Лиора давит на трещину души."
          },
          "counterPayoff": {
            "summary": "Игрок разворачивает нажим."
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}},
          "actionCostAudit": {
            "player": {
              "operationType": "counter",
              "baseCost": 4,
              "minCost": 2,
              "artTier": 0,
              "effectiveCost": 4,
              "before": 3,
              "after": 0
            }
          }
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_action_points_insufficient", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ActionEconomyMustMatchLastCurrentCostAudit()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_action_economy_mismatch_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}},
          "actionCostAudit": {
            "player": {
              "operationType": "pressure",
              "baseCost": 3,
              "minCost": 1,
              "artTier": 1,
              "effectiveCost": 2,
              "before": 6,
              "after": 4
            }
          }
        }
        """, syncRootActionEconomyToLastAudit: false);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_action_economy_delta_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RejectsUnauditedActionEconomyCurrentDeltas()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync("""
        {
          "exchangeId": "exchange_action_economy_unaudited_delta_001",
          "operationType": "negotiate",
          "outcome": "success",
          "matchupAudit": {
            "playerOperation": "negotiate",
            "oppositionOperation": "none",
            "primaryResolutionLane": "negotiate",
            "matchupRationale": "Переговоры решают терминальный выбор без платного приема.",
            "riskProfile": "terminal_choice"
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "player_advantaged"
          }
        }
        """,
        addDefaultMatchupAudit: false,
        rootPlayerActionCurrentOverride: 1,
        rootOppositionActionCurrentOverride: 2);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_action_economy_unaudited_delta", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_action_economy_opposition_unaudited_delta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RejectsUnexpectedActionCostAuditForTerminalOperation()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_terminal_fake_action_cost_001",
          "operationType": "negotiate",
          "outcome": "success",
          "matchupAudit": {
            "playerOperation": "negotiate",
            "oppositionOperation": "none",
            "primaryResolutionLane": "negotiate",
            "matchupRationale": "Переговоры разыграны как терминальный выбор, но не должны тратить ОД.",
            "riskProfile": "terminal_choice"
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "player_advantaged"
          },
          "actionCostAudit": {
            "player": {
              "operationType": "negotiate",
              "baseCost": 0,
              "minCost": 0,
              "artTier": 0,
              "effectiveCost": 0,
              "before": 6,
              "after": 1
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}}
        }
        """,
        addDefaultMatchupAudit: false,
        rootPlayerActionCurrentOverride: 1);
        await WritePreTurnActiveConflictSnapshotWithAuthorityAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_action_cost_audit_unexpected", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_action_economy_unaudited_delta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RejectsFirstActionCostBeforeMismatchingPreTurnActionEconomy()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync("""
        {
          "exchangeId": "exchange_action_cost_pre_turn_anchor_001",
          "operationType": "pressure",
          "outcome": "no_effect",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "actionCostAudit": {
            "player": {
              "operationType": "pressure",
              "baseCost": 3,
              "minCost": 1,
              "artTier": 0,
              "effectiveCost": 3,
              "before": 4,
              "after": 1
            }
          }
        }
        """, addDefaultMatchupAudit: false);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_action_cost_sequence_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RecoverSpiritualPowerCannotExceedMaxActionPoints()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync("""
        {
          "exchangeId": "exchange_recovery_over_cap_001",
          "operationType": "recover_spiritual_power",
          "outcome": "success",
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested",
            "actionEconomy": {
              "player": { "current": 5, "max": 6, "source": "Средоточие Души tier 0" },
              "opposition": { "current": 6, "max": 6, "source": "opposition spiritual authority" }
            }
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested",
            "actionEconomy": {
              "player": { "current": 9, "max": 6, "source": "Средоточие Души tier 0" },
              "opposition": { "current": 6, "max": 6, "source": "opposition spiritual authority" }
            }
          },
          "matchupAudit": {
            "playerOperation": "recover_spiritual_power",
            "oppositionOperation": "guard",
            "primaryResolutionLane": "recover_spiritual_power",
            "riskProfile": "recovery_timing",
            "matchupRationale": "The player gathers focus while the opposition guards."
          },
          "actionCostAudit": {
            "player": {
              "operationType": "recover_spiritual_power",
              "baseCost": 0,
              "minCost": 0,
              "artTier": 0,
              "effectiveCost": 0,
              "before": 5,
              "after": 9
            }
          }
        }
        """, addDefaultMatchupAudit: false);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_action_recovery_exceeds_max", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CounterCannotTargetManeuver()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_counter_targets_maneuver_001",
          "operationType": "counter",
          "outcome": "countered",
          "incomingAction": {
            "operationType": "maneuver",
            "summary": "Лиора пытается обойти защиту игрока."
          },
          "counterPayoff": {
            "summary": "Игрок пытается наказать движение как прямую атаку."
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_counter_invalid_target_operation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CounterCannotTargetGuard()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_counter_targets_guard_001",
          "operationType": "counter",
          "outcome": "countered",
          "incomingAction": {
            "operationType": "guard",
            "summary": "Лиора защищается от давления игрока."
          },
          "counterPayoff": {
            "summary": "Игрок пытается контратаковать безопасную защитную линию."
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_counter_invalid_target_operation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CounterCanTargetPressureWithPayoff()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_counter_targets_pressure_001",
          "operationType": "counter",
          "outcome": "countered",
          "incomingAction": {
            "operationType": "pressure",
            "summary": "Лиора давит на трещину души."
          },
          "counterPayoff": {
            "summary": "Игрок разворачивает давление обратно."
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_counter_invalid_target_operation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CounterCanTargetBindingWithPayoff()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_counter_targets_binding_001",
          "operationType": "counter",
          "outcome": "countered",
          "incomingAction": {
            "operationType": "binding",
            "summary": "Лиора пытается наложить духовные оковы."
          },
          "counterPayoff": {
            "summary": "Игрок разворачивает нить оков обратно."
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_counter_invalid_target_operation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CounterMissingIncomingOperation_IsRejected()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_counter_missing_incoming_operation_001",
          "operationType": "counter",
          "outcome": "countered",
          "incomingAction": {
            "summary": "Лиора действует, но тип входящего приёма не указан."
          },
          "counterPayoff": {
            "summary": "Игрок пытается контратаковать неописанную цель."
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_counter_invalid_target_operation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CounterUnknownIncomingOperation_IsRejected()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_counter_unknown_incoming_operation_001",
          "operationType": "counter",
          "outcome": "countered",
          "incomingAction": {
            "operationType": "presssure",
            "summary": "Лиора давит, но GM ошибся в токене операции."
          },
          "counterPayoff": {
            "summary": "Игрок пытается разворотить ошибочно указанное действие."
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_counter_invalid_target_operation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CounterUnknownIncomingOperationWithoutTurnBaseline_IsRejected()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_counter_unknown_no_baseline_001",
          "operationType": "counter",
          "outcome": "countered",
          "incomingAction": {
            "summary": "Лиора действует, но текущий legacy-shaped exchange не содержит тип цели."
          },
          "counterPayoff": {
            "summary": "Игрок пытается контратаковать неизвестную цель."
          },
          "before": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """, addDefaultMatchupAudit: false);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_counter_invalid_target_operation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CounterSetbackRequiresDownside()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_counter_setback_no_downside_001",
          "operationType": "counter",
          "outcome": "setback",
          "incomingAction": {
            "operationType": "pressure",
            "summary": "Лиора давит на трещину души."
          },
          "before": {
            "playerSideStrain": "strained",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "after": {
            "playerSideStrain": "strained",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_counter_setback_without_downside", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ManeuverCannotDirectlyChangeStrain()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_maneuver_strain_001",
          "operationType": "maneuver",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear"
          },
          "after": {
            "conflictPosition": "player_advantaged",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_maneuver_changes_strain", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SuccessfulManeuverRequiresPositionShift()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_maneuver_no_shift_001",
          "operationType": "maneuver",
          "outcome": "success",
          "before": { "conflictPosition": "contested" },
          "after": { "conflictPosition": "contested" },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_maneuver_missing_position_shift", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_BindingRequiresLeverage()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_binding_without_leverage_001",
          "operationType": "binding",
          "outcome": "success",
          "before": { "conflictPosition": "contested" },
          "after": {
            "conflictPosition": "player_advantaged",
            "bindingState": "imposed"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_binding_without_leverage", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_BindingWithAdvantageIsAllowed()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_binding_with_leverage_001",
          "operationType": "binding",
          "outcome": "success",
          "before": { "conflictPosition": "player_advantaged" },
          "after": {
            "conflictPosition": "player_dominant",
            "bindingState": "imposed"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_binding_without_leverage", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnLegacyBindingWithoutControlState_RemainsCompatible()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_legacy_binding_pre_turn_without_control_001",
          "operationType": "binding",
          "outcome": "success",
          "before": {
            "conflictPosition": "player_advantaged",
            "bindingState": "none"
          },
          "after": {
            "conflictPosition": "player_advantaged",
            "bindingState": "imposed"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerWithPositionModifier("player_advantaged").ToJsonString()}}
        }
        """);
        await WriteValidatedConflictSnapshotFromCurrentAsync("Legacy binding history remains canonical after loading a save.");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_binding_missing_control_delta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CurrentLegacyBindingWithoutControlState_RequiresCanonicalControlDelta()
    {
        await WriteSoulStateAsync();
        var preTurnRoot = JsonNode.Parse(BuildActiveConflictRootJson())!.AsObject();
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, preTurnRoot.ToJsonString());
        await WriteValidatedConflictSnapshotFromCurrentAsync("Current binding must use canonical controlState.");

        var currentRoot = preTurnRoot.DeepClone().AsObject();
        var activeConflict = Assert.IsType<JsonObject>(currentRoot["activeConflict"]);
        var exchangeLog = Assert.IsType<JsonArray>(activeConflict["exchangeLog"]);
        exchangeLog.Add(JsonNode.Parse(AddDefaultMatchupAudit($$"""
        {
          "exchangeId": "exchange_current_legacy_binding_without_control_001",
          "operationType": "binding",
          "outcome": "success",
          "before": {
            "conflictPosition": "player_advantaged",
            "bindingState": "none"
          },
          "after": {
            "conflictPosition": "player_advantaged",
            "bindingState": "imposed"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerWithPositionModifier("player_advantaged").ToJsonString()}}
        }
        """)));
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, currentRoot.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_binding_missing_control_delta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_BindingWithGenericSetupIsAllowed()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_binding_generic_setup_001",
          "operationType": "binding",
          "outcome": "success",
          "setup": true,
          "before": {
            "conflictPosition": "contested",
            "controlState": { "level": "none" }
          },
          "after": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "hindered",
              "controllerSide": "player",
              "controlId": "control_binding_generic_setup_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Generic setup gives ordinary binding enough leverage."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_binding_without_leverage", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ForceBindingRejectsGenericSetupTrueWithoutStrongLeverage()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_force_binding_generic_setup_001",
          "operationType": "force_binding",
          "outcome": "success",
          "setup": true,
          "before": {
            "conflictPosition": "contested",
            "controlState": { "level": "none" }
          },
          "after": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "hindered",
              "controllerSide": "player",
              "controlId": "control_force_binding_generic_setup_001",
              "sourceOperation": "force_binding",
              "restrictedOperations": [ "maneuver", "binding" ],
              "summary": "Generic setup is not strong enough for force binding."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_binding_without_leverage", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_force_binding_without_strong_leverage", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("setupState")]
    [InlineData("bindingSetup")]
    public async Task ValidateGameStateAsync_ForceBindingWithReadySetupIsAllowed(string setupField)
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_force_binding_ready_{{setupField}}_001",
          "operationType": "force_binding",
          "outcome": "success",
          "{{setupField}}": "ready",
          "before": {
            "conflictPosition": "contested",
            "controlState": { "level": "none" }
          },
          "after": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "hindered",
              "controllerSide": "player",
              "controlId": "control_force_binding_ready_{{setupField}}_001",
              "sourceOperation": "force_binding",
              "restrictedOperations": [ "maneuver", "binding" ],
              "summary": "Ready setup gives force binding strong leverage."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_force_binding_without_strong_leverage", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ForceBindingRequiresBroaderControlPayoff()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_force_binding_narrow_payoff_001",
          "operationType": "force_binding",
          "outcome": "success",
          "bindingSetup": "ready",
          "before": {
            "conflictPosition": "contested",
            "controlState": { "level": "none" }
          },
          "after": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "hindered",
              "controllerSide": "player",
              "controlId": "control_force_binding_narrow_payoff_001",
              "sourceOperation": "force_binding",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Силовые оковы не должны давать тот же узкий эффект, что обычные оковы."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_force_binding_without_broad_control_payoff", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_BindingWithAdvantageRequiresControlDelta()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_binding_no_control_delta_001",
          "operationType": "binding",
          "outcome": "success",
          "before": {
            "conflictPosition": "player_advantaged",
            "controlState": { "level": "none", "controllerSide": null }
          },
          "after": {
            "conflictPosition": "player_advantaged",
            "controlState": { "level": "none", "controllerSide": null }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerWithPositionModifier("player_advantaged").ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_binding_missing_control_delta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ControlStateRejectsUnknownRestrictedOperation()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_binding_unknown_restricted_operation_001",
          "operationType": "binding",
          "outcome": "success",
          "before": {
            "conflictPosition": "player_advantaged",
            "controlState": { "level": "none" }
          },
          "after": {
            "conflictPosition": "player_advantaged",
            "controlState": {
              "level": "hindered",
              "controllerSide": "player",
              "controlId": "control_unknown_restricted_operation_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "manuever" ],
              "summary": "Typo in restrictedOperations must not pass validation."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerWithPositionModifier("player_advantaged").ToJsonString()}}
        }
        """, activeControlStateJson: """
        {
          "level": "hindered",
          "controllerSide": "player",
          "controlId": "control_unknown_restricted_operation_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "manuever" ],
          "summary": "Typo in restrictedOperations must not pass validation."
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_control_state_invalid_restricted_operation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_BindingControlSourceMustMatchOperation()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_binding_wrong_control_source_001",
          "operationType": "binding",
          "outcome": "success",
          "before": {
            "conflictPosition": "player_advantaged",
            "controlState": { "level": "none" }
          },
          "after": {
            "conflictPosition": "player_advantaged",
            "controlState": {
              "level": "hindered",
              "controllerSide": "player",
              "controlId": "control_binding_wrong_source_001",
              "sourceOperation": "pressure",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Binding-created control must not claim pressure as its source."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerWithPositionModifier("player_advantaged").ToJsonString()}}
        }
        """, activeControlStateJson: """
        {
          "level": "hindered",
          "controllerSide": "player",
          "controlId": "control_binding_wrong_source_001",
          "sourceOperation": "pressure",
          "restrictedOperations": [ "maneuver" ],
          "summary": "Binding-created control must not claim pressure as its source."
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_control_source_operation_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ControlStateRejectsNegotiateSourceOperation()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync("""
        {
          "exchangeId": "exchange_negotiate_creates_control_001",
          "operationType": "negotiate",
          "outcome": "success",
          "voluntary": true,
          "before": {
            "conflictPosition": "contested",
            "controlState": { "level": "none" }
          },
          "after": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "hindered",
              "controllerSide": "player",
              "controlId": "control_negotiate_invalid_source_001",
              "sourceOperation": "negotiate",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Negotiation is not a control-source operation."
            }
          }
        }
        """, addDefaultMatchupAudit: false, activeControlStateJson: """
        {
          "level": "hindered",
          "controllerSide": "player",
          "controlId": "control_negotiate_invalid_source_001",
          "sourceOperation": "negotiate",
          "restrictedOperations": [ "maneuver" ],
          "summary": "Negotiation is not a control-source operation."
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_control_state_invalid_source_operation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ControlStateRejectsChampionCoordinationSourceOperation()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_champion_coordination_creates_control_001",
          "operationType": "champion_coordination",
          "outcome": "success",
          "before": {
            "sideModel": "champion_duel",
            "conflictPosition": "contested",
            "controlState": { "level": "none" }
          },
          "after": {
            "sideModel": "champion_duel",
            "conflictPosition": "contested",
            "controlState": {
              "level": "hindered",
              "controllerSide": "player",
              "controlId": "control_champion_coordination_invalid_source_001",
              "sourceOperation": "champion_coordination",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Champion coordination is support, not a control-source operation."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}}
        }
        """, activeControlStateJson: """
        {
          "level": "hindered",
          "controllerSide": "player",
          "controlId": "control_champion_coordination_invalid_source_001",
          "sourceOperation": "champion_coordination",
          "restrictedOperations": [ "maneuver" ],
          "summary": "Champion coordination is support, not a control-source operation."
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_control_state_invalid_source_operation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_UnchangedControlCanKeepOriginalSourceOperation()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_pressure_preserves_existing_control_source_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "oppositionSideStrain": "clear",
            "controlState": {
              "level": "hindered",
              "controllerSide": "player",
              "controlId": "control_preserved_source_binding_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Existing binding control remains unchanged."
            }
          },
          "after": {
            "conflictPosition": "contested",
            "oppositionSideStrain": "strained",
            "controlState": {
              "level": "hindered",
              "controllerSide": "player",
              "controlId": "control_preserved_source_binding_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Existing binding control remains unchanged."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}}
        }
        """, activeControlStateJson: """
        {
          "level": "hindered",
          "controllerSide": "player",
          "controlId": "control_preserved_source_binding_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver" ],
          "summary": "Existing binding control remains unchanged."
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_control_source_operation_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_BlockedBindingCannotCreatePlayerControl()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_blocked_binding_creates_control_001",
          "operationType": "binding",
          "outcome": "blocked",
          "incomingAction": {
            "operationType": "break_binding",
            "summary": "The opposing side cuts the binding before it takes hold."
          },
          "before": {
            "conflictPosition": "player_advantaged",
            "controlState": { "level": "none" }
          },
          "after": {
            "conflictPosition": "player_advantaged",
            "controlState": {
              "level": "hindered",
              "controllerSide": "player",
              "controlId": "control_blocked_binding_illegal_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Blocked binding must not create player control."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithPositionModifier("player_advantaged").ToJsonString()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_binding_control_delta_on_failed_outcome", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SetbackForceBindingCannotCreatePlayerControl()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_setback_force_binding_creates_control_001",
          "operationType": "force_binding",
          "outcome": "setback",
          "before": {
            "conflictPosition": "player_dominant",
            "controlState": { "level": "none" }
          },
          "after": {
            "conflictPosition": "player_dominant",
            "controlState": {
              "level": "hindered",
              "controllerSide": "player",
              "controlId": "control_setback_force_binding_illegal_001",
              "sourceOperation": "force_binding",
              "restrictedOperations": [ "maneuver", "binding" ],
              "summary": "Setback force binding must not create player control."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerWithPositionModifier("player_dominant", value: 4).ToJsonString()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_binding_control_delta_on_failed_outcome", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_BlockedBindingCannotRewriteSameLevelPlayerControl()
    {
        await WriteSoulStateAsync();
        await _fs.WriteFileAtomicAsync(GuardianPowerEventState.JournalPath, """{ "entries": [] }""");
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_blocked_binding_rewrites_same_level_control_001",
          "operationType": "binding",
          "outcome": "blocked",
          "incomingAction": {
            "operationType": "guard",
            "summary": "The opposing side prevents the binding from changing."
          },
          "before": {
            "conflictPosition": "player_advantaged",
            "controlState": {
              "level": "hindered",
              "controllerSide": "player",
              "controlId": "control_blocked_binding_existing_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Existing player control before the failed binding."
            }
          },
          "after": {
            "conflictPosition": "player_advantaged",
            "controlState": {
              "level": "hindered",
              "controllerSide": "player",
              "controlId": "control_blocked_binding_rewritten_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver", "guard" ],
              "summary": "Failed binding must not rewrite same-level player control."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerWithPositionModifier("player_advantaged").ToJsonString()}}
        }
        """, activeControlStateJson: """
        {
          "level": "hindered",
          "controllerSide": "player",
          "controlId": "control_blocked_binding_rewritten_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver", "guard" ],
          "summary": "Failed binding must not rewrite same-level player control."
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_binding_control_delta_on_failed_outcome", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CounteredForceBindingCannotRewriteSameLevelPlayerControl()
    {
        await WriteSoulStateAsync();
        await _fs.WriteFileAtomicAsync(GuardianPowerEventState.JournalPath, """{ "entries": [] }""");
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_countered_force_binding_rewrites_same_level_control_001",
          "operationType": "force_binding",
          "outcome": "countered",
          "incomingAction": {
            "operationType": "counter",
            "summary": "The opposing side counters the force binding before it changes."
          },
          "before": {
            "conflictPosition": "player_dominant",
            "controlState": {
              "level": "bound",
              "controllerSide": "player",
              "controlId": "control_countered_force_binding_existing_001",
              "sourceOperation": "force_binding",
              "restrictedOperations": [ "maneuver", "binding" ],
              "summary": "Existing player control before the countered force binding."
            }
          },
          "after": {
            "conflictPosition": "player_dominant",
            "controlState": {
              "level": "bound",
              "controllerSide": "player",
              "controlId": "control_countered_force_binding_rewritten_001",
              "sourceOperation": "force_binding",
              "restrictedOperations": [ "maneuver", "binding", "guard" ],
              "summary": "Countered force binding must not rewrite same-level player control."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerWithPositionModifier("player_dominant", value: 4).ToJsonString()}}
        }
        """, activeControlStateJson: """
        {
          "level": "bound",
          "controllerSide": "player",
          "controlId": "control_countered_force_binding_rewritten_001",
          "sourceOperation": "force_binding",
          "restrictedOperations": [ "maneuver", "binding", "guard" ],
          "summary": "Countered force binding must not rewrite same-level player control."
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_binding_control_delta_on_failed_outcome", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_BlockedBindingCannotClearOppositionControl()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_blocked_binding_clears_opposition_control_001",
          "operationType": "binding",
          "outcome": "blocked",
          "incomingAction": {
            "operationType": "guard",
            "summary": "The opposition holds the soul inside the binding instead of letting a new seal form."
          },
          "before": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_blocked_binding_opposition_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver", "binding" ],
              "summary": "Opposition control is active before the failed binding."
            }
          },
          "after": {
            "conflictPosition": "contested",
            "controlState": null
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_binding_control_delta_on_failed_outcome", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SetbackForceBindingCannotWeakenOppositionControl()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_setback_force_binding_weakens_opposition_control_001",
          "operationType": "force_binding",
          "outcome": "setback",
          "before": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "locked",
              "controllerSide": "opposition",
              "controlId": "control_setback_force_binding_opposition_001",
              "sourceOperation": "force_binding",
              "restrictedOperations": [ "maneuver", "binding", "pressure" ],
              "summary": "Opposition control is locked before the failed force binding."
            }
          },
          "after": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_setback_force_binding_opposition_001",
              "sourceOperation": "force_binding",
              "restrictedOperations": [ "maneuver", "binding" ],
              "summary": "Failed force binding must not weaken opposition control."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}}
        }
        """, activeControlStateJson: """
        {
          "level": "bound",
          "controllerSide": "opposition",
          "controlId": "control_setback_force_binding_opposition_001",
          "sourceOperation": "force_binding",
          "restrictedOperations": [ "maneuver", "binding" ],
          "summary": "Failed force binding must not weaken opposition control."
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_binding_control_delta_on_failed_outcome", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_GuardCannotCreateCanonicalControlState()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_guard_adds_control_state_001",
          "operationType": "guard",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "controlState": { "level": "none" }
          },
          "after": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "controlState": {
              "level": "hindered",
              "controllerSide": "player",
              "controlId": "control_guard_should_not_create_001",
              "sourceOperation": "guard",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Guard illegally creates player control."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_guard_changes_control", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_BindingCannotJumpFromNoneToLocked()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_binding_none_to_locked_001",
          "operationType": "binding",
          "outcome": "success",
          "before": {
            "conflictPosition": "player_advantaged",
            "controlState": { "level": "none" }
          },
          "after": {
            "conflictPosition": "player_advantaged",
            "controlState": {
              "level": "locked",
              "controllerSide": "player",
              "controlId": "control_binding_jump_locked_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver", "break_binding" ],
              "summary": "Binding illegally jumps straight to locked."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithPositionModifier("player_advantaged").ToJsonString()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_binding_control_step_too_large", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_BindingCannotReverseActiveOppositionControl()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_binding_reverses_opposition_control_001",
          "operationType": "binding",
          "outcome": "success",
          "before": {
            "conflictPosition": "player_advantaged",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_opposition_binding_active_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver", "binding" ],
              "summary": "The opponent's binding is still active."
            }
          },
          "after": {
            "conflictPosition": "player_advantaged",
            "controlState": {
              "level": "hindered",
              "controllerSide": "player",
              "controlId": "control_player_binding_illegal_reversal_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Binding must not reverse active opposition control."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithPositionModifier("player_advantaged").ToJsonString()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_binding_under_opposition_control", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_BindingCannotJumpFromHinderedToLocked()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_binding_hindered_to_locked_001",
          "operationType": "binding",
          "outcome": "success",
          "before": {
            "conflictPosition": "player_advantaged",
            "controlState": {
              "level": "hindered",
              "controllerSide": "player",
              "controlId": "control_binding_hindered_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Player has light control."
            }
          },
          "after": {
            "conflictPosition": "player_advantaged",
            "controlState": {
              "level": "locked",
              "controllerSide": "player",
              "controlId": "control_binding_hindered_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver", "break_binding" ],
              "summary": "Binding illegally skips bound."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithPositionModifier("player_advantaged").ToJsonString()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_binding_control_step_too_large", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("none", "hindered")]
    [InlineData("hindered", "bound")]
    [InlineData("bound", "locked")]
    public async Task ValidateGameStateAsync_BindingCanAdvanceControlByOneStep(string beforeLevel, string afterLevel)
    {
        await WriteSoulStateAsync();
        var beforeControl = string.Equals(beforeLevel, "none", StringComparison.OrdinalIgnoreCase)
            ? """{ "level": "none" }"""
            : $$"""
              {
                "level": "{{beforeLevel}}",
                "controllerSide": "player",
                "controlId": "control_binding_one_step_001",
                "sourceOperation": "binding",
                "restrictedOperations": [ "maneuver" ],
                "summary": "Player control before one-step binding."
              }
              """;
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_binding_one_step_{{beforeLevel}}_to_{{afterLevel}}",
          "operationType": "binding",
          "outcome": "success",
          "before": {
            "conflictPosition": "player_advantaged",
            "controlState": {{beforeControl}}
          },
          "after": {
            "conflictPosition": "player_advantaged",
            "controlState": {
              "level": "{{afterLevel}}",
              "controllerSide": "player",
              "controlId": "control_binding_one_step_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver", "break_binding" ],
              "summary": "Binding advances player control by one step."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithPositionModifier("player_advantaged").ToJsonString()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_binding_missing_control_delta", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_binding_control_step_too_large", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PressureCannotCreateCanonicalControlState()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_pressure_adds_control_state_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "oppositionSideStrain": "clear",
            "controlState": { "level": "none", "controllerSide": null }
          },
          "after": {
            "conflictPosition": "contested",
            "oppositionSideStrain": "strained",
            "controlState": {
              "level": "hindered",
              "controllerSide": "player",
              "controlId": "control_pressure_illegal_001",
              "sourceOperation": "pressure",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Pressure illegally created control."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_pressure_adds_binding", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PressureCannotRemoveCanonicalControlState()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_pressure_removes_control_state_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "oppositionSideStrain": "clear",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_pressure_should_not_remove_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Opponent control is active before pressure."
            }
          },
          "after": {
            "conflictPosition": "contested",
            "oppositionSideStrain": "strained"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_pressure_adds_binding", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_BreakBindingRequiresBindingContext()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_break_binding_without_context_001",
          "operationType": "break_binding",
          "outcome": "success",
          "before": { "conflictPosition": "contested" },
          "after": { "conflictPosition": "player_advantaged" },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_break_binding_without_binding", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_BreakBindingRequiresControlReduction()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_break_binding_no_control_delta_001",
          "operationType": "break_binding",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_opposition_bind_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver", "binding" ],
              "summary": "Opponent binds the soul."
            }
          },
          "after": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_opposition_bind_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver", "binding" ],
              "summary": "Opponent bind did not weaken."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_break_binding_missing_control_delta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_BreakBindingCannotUseLegacyDeltaWhenCanonicalControlUnchanged()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_break_binding_legacy_delta_canonical_unchanged_001",
          "operationType": "break_binding",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "bindingState": "active",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_opposition_bind_legacy_bypass_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver", "binding" ],
              "summary": "Canonical opposition control is active."
            }
          },
          "after": {
            "conflictPosition": "contested",
            "bindingState": "broken",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_opposition_bind_legacy_bypass_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver", "binding" ],
              "summary": "Canonical opposition control is still active and unchanged."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_break_binding_missing_control_delta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_BreakBindingAllowsLegacyOnlyBindingDelta()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_break_binding_legacy_only_delta_001",
          "operationType": "break_binding",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "bindingState": "active"
          },
          "after": {
            "conflictPosition": "contested",
            "bindingState": "broken"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_break_binding_without_binding", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_conflict_break_binding_missing_control_delta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_BreakBindingCanWeakenCanonicalControl()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_break_binding_weakens_control_001",
          "operationType": "break_binding",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_opposition_bind_002",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver", "binding" ],
              "summary": "Opponent binds the soul."
            }
          },
          "after": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "hindered",
              "controllerSide": "opposition",
              "controlId": "control_opposition_bind_002",
              "sourceOperation": "break_binding",
              "restrictedOperations": [ "maneuver" ],
              "summary": "The bind weakens but is not gone."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_break_binding_without_binding", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_break_binding_missing_control_delta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_BreakBindingCanReduceSameLevelControlRestrictions()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_break_binding_reduces_same_level_restrictions_001",
          "operationType": "break_binding",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_opposition_bind_restrictions_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver", "binding" ],
              "summary": "Opponent bind restricts two spiritual lanes."
            }
          },
          "after": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_opposition_bind_restrictions_001",
              "sourceOperation": "break_binding",
              "restrictedOperations": [ "maneuver" ],
              "summary": "The break narrows the bind while level remains bound."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_break_binding_missing_control_delta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_BreakBindingSameLevelReorderedRestrictionsDoNotCountAsReduction()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_break_binding_reorders_same_level_restrictions_001",
          "operationType": "break_binding",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_opposition_bind_reorder_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver", "binding" ],
              "summary": "Opponent bind restricts two lanes."
            }
          },
          "after": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_opposition_bind_reorder_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "binding", "maneuver" ],
              "summary": "Reordered restrictions do not weaken control."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_break_binding_missing_control_delta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_BlockedBreakBindingCannotReduceSameLevelControlRestrictions()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_blocked_break_binding_reduces_same_level_restrictions_001",
          "operationType": "break_binding",
          "outcome": "blocked",
          "incomingAction": {
            "operationType": "binding",
            "summary": "The opposing binding holds."
          },
          "before": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_blocked_restriction_reduction_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver", "binding" ],
              "summary": "Opponent control is active before the failed break."
            }
          },
          "after": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_blocked_restriction_reduction_001",
              "sourceOperation": "break_binding",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Failed break must not narrow restrictions."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_break_binding_control_delta_on_failed_outcome", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_BlockedBreakBindingCannotClearControl()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_blocked_break_binding_clears_control_001",
          "operationType": "break_binding",
          "outcome": "blocked",
          "incomingAction": {
            "operationType": "binding",
            "summary": "The opposing binding holds."
          },
          "before": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_blocked_break_binding_should_not_clear_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver", "binding" ],
              "summary": "Opponent control is active before the failed break."
            }
          },
          "after": {
            "conflictPosition": "contested",
            "controlState": null
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_break_binding_control_delta_on_failed_outcome", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SuccessfulBreakBindingCanClearControl()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_success_break_binding_clears_control_001",
          "operationType": "break_binding",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_success_break_binding_clear_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver", "binding" ],
              "summary": "Opponent control is active before the successful break."
            }
          },
          "after": {
            "conflictPosition": "contested",
            "controlState": null
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_break_binding_control_delta_on_failed_outcome", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_break_binding_missing_control_delta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_IncarnationResistanceAcceptsForceIncarnationControlState()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_incarnation_resistance_control_context_001",
          "operationType": "incarnation_resistance",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_force_incarnation_context_001",
              "sourceOperation": "force_incarnation",
              "restrictedOperations": [ "withdraw", "surrender", "negotiate" ],
              "summary": "The Guardian is forcing the soul toward incarnation."
            }
          },
          "after": {
            "conflictPosition": "contested",
            "controlState": null
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_incarnation_resistance_without_force", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_incarnation_resistance_clears_non_force_control", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_IncarnationResistanceCanWeakenForcedControlAndKeepSource()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_incarnation_resistance_weakens_force_control_001",
          "operationType": "incarnation_resistance",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "locked",
              "controllerSide": "opposition",
              "controlId": "control_force_incarnation_residual_001",
              "sourceOperation": "force_incarnation",
              "restrictedOperations": [ "withdraw", "surrender", "negotiate" ],
              "summary": "The Guardian is forcing the soul toward incarnation."
            }
          },
          "after": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_force_incarnation_residual_001",
              "sourceOperation": "force_incarnation",
              "restrictedOperations": [ "withdraw", "surrender" ],
              "summary": "The forced incarnation control weakens but remains active."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}}
        }
        """, activeControlStateJson: """
        {
          "level": "bound",
          "controllerSide": "opposition",
          "controlId": "control_force_incarnation_residual_001",
          "sourceOperation": "force_incarnation",
          "restrictedOperations": [ "withdraw", "surrender" ],
          "summary": "The forced incarnation control weakens but remains active."
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_control_source_operation_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_incarnation_resistance_without_force", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_NextIncarnationResistanceAcceptsResidualForcedControl()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_incarnation_resistance_residual_force_context_001",
          "operationType": "incarnation_resistance",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_force_incarnation_residual_002",
              "sourceOperation": "force_incarnation",
              "restrictedOperations": [ "withdraw", "surrender" ],
              "summary": "The forced incarnation control remains after a previous resistance."
            }
          },
          "after": {
            "conflictPosition": "contested",
            "controlState": null
          }
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_incarnation_resistance_without_force", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_BlockedIncarnationResistanceCannotClearForcedControl()
    {
        await WriteSoulStateAsync();
        await _fs.WriteFileAtomicAsync(GuardianPowerEventState.JournalPath, """{ "entries": [] }""");
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_blocked_incarnation_resistance_clears_force_control_001",
          "operationType": "incarnation_resistance",
          "outcome": "blocked",
          "incomingAction": {
            "operationType": "force_incarnation",
            "actorId": "guardian_liora",
            "summary": "The Guardian's forced incarnation pressure holds."
          },
          "before": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_blocked_force_incarnation_001",
              "sourceOperation": "force_incarnation",
              "restrictedOperations": [ "withdraw", "surrender", "negotiate" ],
              "summary": "The Guardian is forcing the soul toward incarnation."
            }
          },
          "after": {
            "conflictPosition": "contested",
            "controlState": null
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_incarnation_resistance_control_delta_on_failed_outcome", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SetbackIncarnationResistanceCannotWeakenForcedControl()
    {
        await WriteSoulStateAsync();
        await _fs.WriteFileAtomicAsync(GuardianPowerEventState.JournalPath, """{ "entries": [] }""");
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_setback_incarnation_resistance_weakens_force_control_001",
          "operationType": "incarnation_resistance",
          "outcome": "setback",
          "incomingAction": {
            "operationType": "force_incarnation",
            "actorId": "guardian_liora",
            "summary": "The Guardian's forced incarnation pressure intensifies."
          },
          "before": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "locked",
              "controllerSide": "opposition",
              "controlId": "control_setback_force_incarnation_001",
              "sourceOperation": "force_incarnation",
              "restrictedOperations": [ "withdraw", "surrender", "negotiate" ],
              "summary": "The Guardian is forcing the soul toward incarnation."
            }
          },
          "after": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_setback_force_incarnation_001",
              "sourceOperation": "force_incarnation",
              "restrictedOperations": [ "withdraw", "surrender" ],
              "summary": "Failed resistance must not weaken forced incarnation control."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}}
        }
        """, activeControlStateJson: """
        {
          "level": "bound",
          "controllerSide": "opposition",
          "controlId": "control_setback_force_incarnation_001",
          "sourceOperation": "force_incarnation",
          "restrictedOperations": [ "withdraw", "surrender" ],
          "summary": "Failed resistance must not weaken forced incarnation control."
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_incarnation_resistance_control_delta_on_failed_outcome", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_IncarnationResistanceCannotCreateFreshPlayerControl()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_incarnation_resistance_creates_fresh_control_001",
          "operationType": "incarnation_resistance",
          "outcome": "success",
          "incomingAction": {
            "operationType": "force_incarnation",
            "actorId": "guardian_liora",
            "summary": "The Guardian tries to force incarnation from an uncontrolled state."
          },
          "before": {
            "conflictPosition": "contested",
            "controlState": { "level": "none" }
          },
          "after": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "hindered",
              "controllerSide": "player",
              "controlId": "control_incarnation_resistance_fresh_player_001",
              "sourceOperation": "incarnation_resistance",
              "restrictedOperations": [ "force_incarnation" ],
              "summary": "Incarnation resistance must not create fresh player control."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_incarnation_resistance_creates_fresh_control", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_IncarnationResistanceCannotClearOrdinaryBindingControl()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_incarnation_resistance_clears_binding_control_001",
          "operationType": "incarnation_resistance",
          "outcome": "success",
          "incomingAction": {
            "operationType": "force_incarnation",
            "actorId": "guardian_liora",
            "summary": "The Guardian tries to force incarnation while an ordinary binding is active."
          },
          "before": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_ordinary_binding_incarnation_resistance_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver", "binding" ],
              "summary": "An ordinary binding is active and must be answered by break_binding or a valid counter."
            }
          },
          "after": {
            "conflictPosition": "contested",
            "controlState": null
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_incarnation_resistance_without_force", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_incarnation_resistance_clears_non_force_control", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ManeuverCannotImprovePositionWhileOppositionControlIsActive()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_maneuver_under_control_001",
          "operationType": "maneuver",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_opposition_bind_003",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Opponent controls the soul's movement."
            }
          },
          "after": {
            "conflictPosition": "player_advantaged",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_opposition_bind_003",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Opponent control remains unchanged."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_maneuver_blocked_by_control", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CurrentExchangeUnderActiveControlRequiresControlSnapshots()
    {
        await WriteSoulStateAsync();
        var preTurnRoot = JsonNode.Parse(BuildActiveConflictRootJson())!.AsObject();
        var preTurnActive = Assert.IsType<JsonObject>(preTurnRoot["activeConflict"]);
        preTurnActive["controlState"] = JsonNode.Parse("""
        {
          "level": "bound",
          "controllerSide": "opposition",
          "controlId": "control_active_snapshot_required_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver" ],
          "summary": "Opponent control already restricts maneuver before this exchange."
        }
        """);
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, preTurnRoot.ToJsonString());
        await WriteValidatedConflictSnapshotFromCurrentAsync("Я продолжаю духовный бой под активным контролем противника.");

        var currentRoot = preTurnRoot.DeepClone().AsObject();
        var currentActive = Assert.IsType<JsonObject>(currentRoot["activeConflict"]);
        var exchangeLog = Assert.IsType<JsonArray>(currentActive["exchangeLog"]);
        exchangeLog.Add(JsonNode.Parse(AddDefaultMatchupAudit($$"""
        {
          "exchangeId": "exchange_maneuver_omits_control_snapshots_001",
          "operationType": "maneuver",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear"
          },
          "after": {
            "conflictPosition": "player_advantaged",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """)));
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, currentRoot.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_control_snapshot_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CurrentExchangeBeforeControlMustMatchPreTurnControl()
    {
        await WriteSoulStateAsync();
        var preTurnRoot = JsonNode.Parse(BuildActiveConflictRootJson())!.AsObject();
        var preTurnActive = Assert.IsType<JsonObject>(preTurnRoot["activeConflict"]);
        preTurnActive["controlState"] = JsonNode.Parse("""
        {
          "level": "bound",
          "controllerSide": "opposition",
          "controlId": "control_pre_turn_snapshot_mismatch_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver", "binding" ],
          "summary": "Opponent control is active before this exchange."
        }
        """);
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, preTurnRoot.ToJsonString());
        await WriteValidatedConflictSnapshotFromCurrentAsync("Я пытаюсь стереть активный контроль, подделав before snapshot.");

        var currentRoot = preTurnRoot.DeepClone().AsObject();
        var currentActive = Assert.IsType<JsonObject>(currentRoot["activeConflict"]);
        currentActive["controlState"] = null;
        var exchangeLog = Assert.IsType<JsonArray>(currentActive["exchangeLog"]);
        exchangeLog.Add(JsonNode.Parse(AddDefaultMatchupAudit($$"""
        {
          "exchangeId": "exchange_break_binding_falsifies_pre_turn_control_001",
          "operationType": "break_binding",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "bindingState": "active",
            "controlState": null
          },
          "after": {
            "conflictPosition": "contested",
            "bindingState": "broken",
            "controlState": null
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """)));
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, currentRoot.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_control_snapshot_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CurrentExchangeBeforeControlMustMatchFullPreTurnControl()
    {
        await WriteSoulStateAsync();
        var preTurnRoot = JsonNode.Parse(BuildActiveConflictRootJson())!.AsObject();
        var preTurnActive = Assert.IsType<JsonObject>(preTurnRoot["activeConflict"]);
        var activeControl = JsonNode.Parse("""
        {
          "level": "bound",
          "controllerSide": "opposition",
          "controlId": "control_pre_turn_full_snapshot_mismatch_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver", "binding" ],
          "summary": "Opponent control restricts several spiritual actions before this exchange."
        }
        """)!;
        preTurnActive["controlState"] = activeControl.DeepClone();
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, preTurnRoot.ToJsonString());
        await WriteValidatedConflictSnapshotFromCurrentAsync("Я пытаюсь ослабить оковы, подделав список запретов в before snapshot.");

        var currentRoot = preTurnRoot.DeepClone().AsObject();
        var currentActive = Assert.IsType<JsonObject>(currentRoot["activeConflict"]);
        currentActive["controlState"] = null;
        var exchangeLog = Assert.IsType<JsonArray>(currentActive["exchangeLog"]);
        exchangeLog.Add(JsonNode.Parse(AddDefaultMatchupAudit($$"""
        {
          "exchangeId": "exchange_break_binding_falsifies_full_pre_turn_control_001",
          "operationType": "break_binding",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_pre_turn_full_snapshot_mismatch_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Opponent control restricts several spiritual actions before this exchange."
            }
          },
          "after": {
            "conflictPosition": "contested",
            "controlState": null
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """)));
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, currentRoot.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_control_snapshot_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CurrentExchangeBeforeControlAcceptsFullPreTurnControl()
    {
        await WriteSoulStateAsync();
        var preTurnRoot = JsonNode.Parse(BuildActiveConflictRootJson())!.AsObject();
        var preTurnActive = Assert.IsType<JsonObject>(preTurnRoot["activeConflict"]);
        var activeControl = JsonNode.Parse("""
        {
          "level": "bound",
          "controllerSide": "opposition",
          "controlId": "control_pre_turn_full_snapshot_match_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver", "binding" ],
          "summary": "Opponent control restricts several spiritual actions before this exchange."
        }
        """)!;
        preTurnActive["controlState"] = activeControl.DeepClone();
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, preTurnRoot.ToJsonString());
        await WriteValidatedConflictSnapshotFromCurrentAsync("Я честно фиксирую активный контроль перед снятием оков.");

        var currentRoot = preTurnRoot.DeepClone().AsObject();
        var currentActive = Assert.IsType<JsonObject>(currentRoot["activeConflict"]);
        currentActive["controlState"] = null;
        var exchangeLog = Assert.IsType<JsonArray>(currentActive["exchangeLog"]);
        exchangeLog.Add(JsonNode.Parse(AddDefaultMatchupAudit($$"""
        {
          "exchangeId": "exchange_break_binding_uses_full_pre_turn_control_001",
          "operationType": "break_binding",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "controlState": {{activeControl.ToJsonString()}}
          },
          "after": {
            "conflictPosition": "contested",
            "controlState": null
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """)));
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, currentRoot.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_control_snapshot_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_control_snapshot_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_EarlierCurrentExchangeDoesNotRequireControlSnapshotsFromLaterControl()
    {
        await WriteSoulStateAsync();
        var preTurnRoot = JsonNode.Parse(BuildActiveConflictRootJson())!.AsObject();
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, preTurnRoot.ToJsonString());
        await WriteValidatedConflictSnapshotFromCurrentAsync("Я делаю несколько обменов, и контроль появляется только позже.");

        var currentRoot = preTurnRoot.DeepClone().AsObject();
        var currentActive = Assert.IsType<JsonObject>(currentRoot["activeConflict"]);
        var finalControl = JsonNode.Parse("""
        {
          "level": "hindered",
          "controllerSide": "player",
          "controlId": "control_later_binding_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver" ],
          "summary": "Control appears only after the second exchange."
        }
        """)!;
        currentActive["controlState"] = finalControl.DeepClone();
        var exchangeLog = Assert.IsType<JsonArray>(currentActive["exchangeLog"]);
        exchangeLog.Add(JsonNode.Parse(AddDefaultMatchupAudit($$"""
        {
          "exchangeId": "exchange_pressure_before_later_control_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear"
          },
          "after": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """)));
        exchangeLog.Add(JsonNode.Parse(AddDefaultMatchupAudit($$"""
        {
          "exchangeId": "exchange_binding_creates_later_control_001",
          "operationType": "binding",
          "outcome": "success",
          "setup": true,
          "before": {
            "conflictPosition": "contested",
            "controlState": { "level": "none" }
          },
          "after": {
            "conflictPosition": "contested",
            "controlState": {{finalControl.ToJsonString()}}
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """)));
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, currentRoot.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_control_snapshot_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_FinalControlStateRequiresCurrentExchangeSnapshots()
    {
        await WriteSoulStateAsync();
        var preTurnRoot = JsonNode.Parse(BuildActiveConflictRootJson())!.AsObject();
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, preTurnRoot.ToJsonString());
        await WriteValidatedConflictSnapshotFromCurrentAsync("Я давлю на противника, но итоговый контроль должен быть объяснен обменом.");

        var currentRoot = preTurnRoot.DeepClone().AsObject();
        var currentActive = Assert.IsType<JsonObject>(currentRoot["activeConflict"]);
        currentActive["controlState"] = JsonNode.Parse("""
        {
          "level": "hindered",
          "controllerSide": "player",
          "controlId": "control_root_only_without_exchange_audit_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver" ],
          "summary": "Control appears only on the canonical active conflict root."
        }
        """);
        var exchangeLog = Assert.IsType<JsonArray>(currentActive["exchangeLog"]);
        exchangeLog.Add(JsonNode.Parse(AddDefaultMatchupAudit($$"""
        {
          "exchangeId": "exchange_pressure_root_only_control_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear"
          },
          "after": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """)));
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, currentRoot.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_control_snapshot_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_FinalControlStateCannotChangeWithoutCurrentExchange()
    {
        await WriteSoulStateAsync();
        var preTurnRoot = JsonNode.Parse(BuildActiveConflictRootJson())!.AsObject();
        var preTurnActive = Assert.IsType<JsonObject>(preTurnRoot["activeConflict"]);
        preTurnActive["controlState"] = JsonNode.Parse("""
        {
          "level": "bound",
          "controllerSide": "opposition",
          "controlId": "control_root_direct_clear_without_exchange_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver" ],
          "summary": "Opponent control is active before this turn."
        }
        """);
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, preTurnRoot.ToJsonString());
        await WriteValidatedConflictSnapshotFromCurrentAsync("Я пытаюсь убрать контроль без обмена.");

        var currentRoot = preTurnRoot.DeepClone().AsObject();
        var currentActive = Assert.IsType<JsonObject>(currentRoot["activeConflict"]);
        currentActive["controlState"] = null;
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, currentRoot.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_control_snapshot_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_FinalControlStateCannotChangeWhenExchangeLogIsOmitted()
    {
        await WriteSoulStateAsync();
        var preTurnRoot = JsonNode.Parse(BuildActiveConflictRootJson())!.AsObject();
        var preTurnActive = Assert.IsType<JsonObject>(preTurnRoot["activeConflict"]);
        preTurnActive["controlState"] = JsonNode.Parse("""
        {
          "level": "bound",
          "controllerSide": "opposition",
          "controlId": "control_root_omitted_exchange_log_clear_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver" ],
          "summary": "Opponent control is active before this turn."
        }
        """);
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, preTurnRoot.ToJsonString());
        await WriteValidatedConflictSnapshotFromCurrentAsync("Я пытаюсь убрать контроль без exchangeLog.");

        var currentRoot = preTurnRoot.DeepClone().AsObject();
        var currentActive = Assert.IsType<JsonObject>(currentRoot["activeConflict"]);
        currentActive.Remove("exchangeLog");
        currentActive["controlState"] = null;
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, currentRoot.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_control_snapshot_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_FinalControlStateCannotBeCreatedWhenExchangeLogIsOmitted()
    {
        await WriteSoulStateAsync();
        var preTurnRoot = JsonNode.Parse(BuildActiveConflictRootJson())!.AsObject();
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, preTurnRoot.ToJsonString());
        await WriteValidatedConflictSnapshotFromCurrentAsync("Я пытаюсь создать контроль без exchangeLog.");

        var currentRoot = preTurnRoot.DeepClone().AsObject();
        var currentActive = Assert.IsType<JsonObject>(currentRoot["activeConflict"]);
        currentActive.Remove("exchangeLog");
        currentActive["controlState"] = JsonNode.Parse("""
        {
          "level": "hindered",
          "controllerSide": "player",
          "controlId": "control_root_omitted_exchange_log_create_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver" ],
          "summary": "Player control appears only on the active conflict root."
        }
        """);
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, currentRoot.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_control_snapshot_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnControlDoesNotApplyToReplacementConflictWithTerminalProof()
    {
        await WriteSoulStateAsync();
        var preTurnRoot = JsonNode.Parse(BuildActiveConflictRootJson("afterlife_conflict_old_control_001"))!.AsObject();
        var preTurnActive = Assert.IsType<JsonObject>(preTurnRoot["activeConflict"]);
        preTurnActive["controlState"] = JsonNode.Parse("""
        {
          "level": "bound",
          "controllerSide": "opposition",
          "controlId": "control_old_conflict_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver" ],
          "summary": "This control belongs to the old conflict only."
        }
        """);
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, preTurnRoot.ToJsonString());
        await WriteValidatedConflictSnapshotFromCurrentAsync("Я закрываю старый конфликт и начинаю другой.");

        var currentRoot = JsonNode.Parse(BuildActiveConflictRootJson("afterlife_conflict_replacement_001"))!.AsObject();
        var currentActive = Assert.IsType<JsonObject>(currentRoot["activeConflict"]);
        currentActive["exchangeLog"] = new JsonArray();
        currentRoot["recentConflicts"] = JsonNode.Parse("""
        [
          {
            "mode": "resolve",
            "conflictId": "afterlife_conflict_old_control_001",
            "resolutionState": "resolved",
            "resolvedAtTurn": 7,
            "operationType": "negotiate",
            "playerOutcome": "conceded"
          }
        ]
        """);
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, currentRoot.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_control_snapshot_missing", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_control_snapshot_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_active_removed_without_terminal_proof", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CurrentExchangeBeforeControlMustMatchPreviousCurrentExchangeControl()
    {
        await WriteSoulStateAsync();
        var preTurnRoot = JsonNode.Parse(BuildActiveConflictRootJson())!.AsObject();
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, preTurnRoot.ToJsonString());
        await WriteValidatedConflictSnapshotFromCurrentAsync("Я создаю контроль, а затем пытаюсь стереть его поддельным before snapshot.");

        var currentRoot = preTurnRoot.DeepClone().AsObject();
        var currentActive = Assert.IsType<JsonObject>(currentRoot["activeConflict"]);
        currentActive["controlState"] = null;
        var createdControl = JsonNode.Parse("""
        {
          "level": "hindered",
          "controllerSide": "player",
          "controlId": "control_previous_current_snapshot_mismatch_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver" ],
          "summary": "Player control was created by the previous exchange."
        }
        """)!;
        var exchangeLog = Assert.IsType<JsonArray>(currentActive["exchangeLog"]);
        exchangeLog.Add(JsonNode.Parse(AddDefaultMatchupAudit($$"""
        {
          "exchangeId": "exchange_binding_creates_control_before_mismatch_001",
          "operationType": "binding",
          "outcome": "success",
          "setup": true,
          "before": {
            "conflictPosition": "contested",
            "controlState": { "level": "none" }
          },
          "after": {
            "conflictPosition": "contested",
            "controlState": {{createdControl.ToJsonString()}}
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """)));
        exchangeLog.Add(JsonNode.Parse(AddDefaultMatchupAudit($$"""
        {
          "exchangeId": "exchange_break_binding_falsifies_previous_current_control_001",
          "operationType": "break_binding",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "bindingState": "active",
            "controlState": null
          },
          "after": {
            "conflictPosition": "contested",
            "bindingState": "broken",
            "controlState": null
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """)));
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, currentRoot.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_control_snapshot_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CurrentExchangeBeforeControlClearRequiresControlSnapshots()
    {
        await WriteSoulStateAsync();
        var preTurnRoot = JsonNode.Parse(BuildActiveConflictRootJson())!.AsObject();
        var preTurnActive = Assert.IsType<JsonObject>(preTurnRoot["activeConflict"]);
        var activeControl = JsonNode.Parse("""
        {
          "level": "bound",
          "controllerSide": "opposition",
          "controlId": "control_pre_turn_clear_later_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver" ],
          "summary": "Opponent control is active before the multi-exchange turn."
        }
        """)!;
        preTurnActive["controlState"] = activeControl.DeepClone();
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, preTurnRoot.ToJsonString());
        await WriteValidatedConflictSnapshotFromCurrentAsync("Я сначала пытаюсь маневрировать под контролем, а затем срываю оковы.");

        var currentRoot = preTurnRoot.DeepClone().AsObject();
        var currentActive = Assert.IsType<JsonObject>(currentRoot["activeConflict"]);
        currentActive["controlState"] = null;
        var exchangeLog = Assert.IsType<JsonArray>(currentActive["exchangeLog"]);
        exchangeLog.Add(JsonNode.Parse(AddDefaultMatchupAudit($$"""
        {
          "exchangeId": "exchange_maneuver_before_control_clear_001",
          "operationType": "maneuver",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear"
          },
          "after": {
            "conflictPosition": "player_advantaged",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """)));
        exchangeLog.Add(JsonNode.Parse(AddDefaultMatchupAudit($$"""
        {
          "exchangeId": "exchange_break_binding_clears_control_later_001",
          "operationType": "break_binding",
          "outcome": "success",
          "before": {
            "conflictPosition": "player_advantaged",
            "controlState": {{activeControl.ToJsonString()}}
          },
          "after": {
            "conflictPosition": "player_advantaged",
            "controlState": null
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithPositionModifier("player_advantaged").ToJsonString()}}
        }
        """)));
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, currentRoot.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_control_snapshot_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ManeuverCannotRemoveControlWhileImprovingPosition()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_maneuver_removes_control_001",
          "operationType": "maneuver",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_opposition_bind_005",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Opponent controls the soul's movement."
            }
          },
          "after": {
            "conflictPosition": "player_advantaged",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear"
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_maneuver_blocked_by_control", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ManeuverCannotWeakenControlWhileImprovingPosition()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_maneuver_weakens_control_001",
          "operationType": "maneuver",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_opposition_bind_006",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Opponent control is active."
            }
          },
          "after": {
            "conflictPosition": "player_advantaged",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "controlState": {
              "level": "hindered",
              "controllerSide": "opposition",
              "controlId": "control_opposition_bind_006",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Maneuver improperly weakens the existing control."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_maneuver_blocked_by_control", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_BlockedManeuverCannotRemoveExistingControl()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_blocked_maneuver_removes_control_001",
          "operationType": "maneuver",
          "outcome": "blocked",
          "incomingAction": {
            "operationType": "guard",
            "actorId": "guardian_liora",
            "summary": "The Guardian prevents the attempted repositioning."
          },
          "before": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_maneuver_should_not_remove_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Existing opposition control is active."
            }
          },
          "after": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "controlState": null
          },
          "diceAudit": {{BuildMixedNoEffectDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_maneuver_changes_control", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ManeuverSetbackCannotCreateControl()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_maneuver_setback_creates_control_001",
          "operationType": "maneuver",
          "outcome": "setback",
          "incomingAction": {
            "operationType": "pressure",
            "actorId": "guardian_liora",
            "summary": "The Guardian punishes the failed repositioning with pressure."
          },
          "before": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "controlState": {
              "level": "none"
            }
          },
          "after": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "controlState": {
              "level": "hindered",
              "controllerSide": "opposition",
              "controlId": "control_maneuver_setback_illegal_001",
              "sourceOperation": "maneuver",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Maneuver should not create control even on setback."
            }
          },
          "diceAudit": {{BuildValidForcedIncarnationDiceAudit().ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_maneuver_changes_control", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_BlockedGuardCannotRemoveExistingControl()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_blocked_guard_removes_control_001",
          "operationType": "guard",
          "outcome": "blocked",
          "incomingAction": {
            "operationType": "pressure",
            "actorId": "guardian_liora",
            "summary": "The Guardian keeps pressure while existing binding remains active."
          },
          "before": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_blocked_guard_should_not_remove_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver", "binding" ],
              "summary": "Existing opposition control is active."
            }
          },
          "after": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "controlState": null
          },
          "diceAudit": {{BuildMixedNoEffectDiceAuditJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_guard_changes_control", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_GuardSetbackCanRecordIncomingControl()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_guard_setback_records_incoming_control_001",
          "operationType": "guard",
          "outcome": "setback",
          "incomingAction": {
            "operationType": "binding",
            "actorId": "guardian_liora",
            "summary": "The Guardian pushes through the guard and binds the soul."
          },
          "before": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear"
          },
          "after": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "controlState": {
              "level": "hindered",
              "controllerSide": "opposition",
              "controlId": "control_guard_setback_incoming_binding_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver" ],
              "summary": "The failed guard lets the incoming binding take hold."
            }
          },
          "diceAudit": {{BuildValidForcedIncarnationDiceAudit().ToJsonString()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_guard_changes_control", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_GuardSetbackCanRecordIncomingControlAfterExplicitNoControl()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_guard_setback_records_incoming_control_after_none_001",
          "operationType": "guard",
          "outcome": "setback",
          "incomingAction": {
            "operationType": "binding",
            "actorId": "guardian_liora",
            "summary": "The Guardian pushes through the guard and binds the soul."
          },
          "before": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "controlState": {
              "level": "none"
            }
          },
          "after": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "controlState": {
              "level": "hindered",
              "controllerSide": "opposition",
              "controlId": "control_guard_setback_incoming_binding_after_none_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver" ],
              "summary": "The failed guard lets the incoming binding take hold."
            }
          },
          "diceAudit": {{BuildValidForcedIncarnationDiceAudit().ToJsonString()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_guard_changes_control", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_GuardSetbackWithoutIncomingControlStillCannotChangeControl()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_guard_setback_illegal_control_001",
          "operationType": "guard",
          "outcome": "setback",
          "incomingAction": {
            "operationType": "pressure",
            "actorId": "guardian_liora",
            "summary": "The Guardian pressures the soul without a control action."
          },
          "before": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear"
          },
          "after": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "controlState": {
              "level": "hindered",
              "controllerSide": "opposition",
              "controlId": "control_guard_setback_illegal_pressure_001",
              "sourceOperation": "pressure",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Pressure should not become control through guard."
            }
          },
          "diceAudit": {{BuildValidForcedIncarnationDiceAudit().ToJsonString()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_guard_changes_control", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CounterAgainstControlCanUseControlReversalAsPayoff()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_counter_reverses_control_001",
          "operationType": "counter",
          "outcome": "success",
          "incomingAction": {
            "operationType": "binding",
            "actorId": "guardian_opponent",
            "summary": "The Guardian tries to bind the soul."
          },
          "before": {
            "conflictPosition": "contested",
            "oppositionSideStrain": "clear",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_opposition_bind_004",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver", "binding" ],
              "summary": "Opponent bind is active."
            }
          },
          "after": {
            "conflictPosition": "contested",
            "oppositionSideStrain": "clear",
            "controlState": {
              "level": "hindered",
              "controllerSide": "player",
              "controlId": "control_player_reversal_001",
              "sourceOperation": "counter",
              "restrictedOperations": [ "binding" ],
              "summary": "The soul turns the bind back into a weaker counter-control."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_counter_missing_payoff", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CounterAgainstControlCanUseSameLevelRestrictionReductionAsPayoff()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_counter_reduces_control_restrictions_001",
          "operationType": "counter",
          "outcome": "success",
          "incomingAction": {
            "operationType": "binding",
            "actorId": "guardian_opponent",
            "summary": "The Guardian's bind is the incoming action being countered."
          },
          "before": {
            "conflictPosition": "contested",
            "oppositionSideStrain": "clear",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_counter_restrictions_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver", "binding" ],
              "summary": "Opponent bind restricts two lanes."
            }
          },
          "after": {
            "conflictPosition": "contested",
            "oppositionSideStrain": "clear",
            "controlState": {
              "level": "bound",
              "controllerSide": "opposition",
              "controlId": "control_counter_restrictions_001",
              "sourceOperation": "counter",
              "restrictedOperations": [ "maneuver" ],
              "summary": "The counter narrows the bind without fully breaking it."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePowerJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_counter_missing_payoff", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CounterAgainstPressureCannotCreateFreshControlAsPayoff()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_counter_pressure_fresh_control_001",
          "operationType": "counter",
          "outcome": "success",
          "incomingAction": {
            "operationType": "pressure",
            "actorId": "guardian_opponent",
            "summary": "The Guardian pressures the soul without existing control."
          },
          "before": {
            "conflictPosition": "contested",
            "oppositionSideStrain": "clear",
            "controlState": {
              "level": "none"
            }
          },
          "after": {
            "conflictPosition": "contested",
            "oppositionSideStrain": "clear",
            "controlState": {
              "level": "hindered",
              "controllerSide": "player",
              "controlId": "control_counter_pressure_fresh_001",
              "sourceOperation": "counter",
              "restrictedOperations": [ "pressure" ],
              "summary": "A fresh player-side control state should require the binding lane."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_counter_missing_payoff", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CounterAgainstPressureCannotCreateFreshControlWithSeparatePayoff()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_counter_pressure_fresh_control_with_payoff_001",
          "operationType": "counter",
          "outcome": "success",
          "incomingAction": {
            "operationType": "pressure",
            "actorId": "guardian_opponent",
            "summary": "The Guardian pressures the soul without existing control."
          },
          "counterPayoff": {
            "payoffType": "strain_reversal",
            "summary": "The counter also strains the opponent."
          },
          "before": {
            "conflictPosition": "contested",
            "oppositionSideStrain": "clear",
            "controlState": { "level": "none" }
          },
          "after": {
            "conflictPosition": "contested",
            "oppositionSideStrain": "strained",
            "controlState": {
              "level": "hindered",
              "controllerSide": "player",
              "controlId": "control_counter_pressure_fresh_with_payoff_001",
              "sourceOperation": "counter",
              "restrictedOperations": [ "pressure" ],
              "summary": "A separate payoff must not let counter create fresh control."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_counter_creates_fresh_control", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CounterCannotStrengthenExistingPlayerControl()
    {
        await WriteSoulStateAsync();
        var preTurnRoot = JsonNode.Parse(BuildActiveConflictRootJson())!.AsObject();
        var preTurnActive = Assert.IsType<JsonObject>(preTurnRoot["activeConflict"]);
        preTurnActive["controlState"] = JsonNode.Parse("""
        {
          "level": "hindered",
          "controllerSide": "player",
          "controlId": "control_counter_player_growth_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver" ],
          "summary": "Player control already exists before the counter."
        }
        """);
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, preTurnRoot.ToJsonString());
        await WriteValidatedConflictSnapshotFromCurrentAsync("Я пытаюсь усилить контроль контрприёмом вместо оков.");

        var currentRoot = preTurnRoot.DeepClone().AsObject();
        var currentActive = Assert.IsType<JsonObject>(currentRoot["activeConflict"]);
        currentActive["controlState"] = JsonNode.Parse("""
        {
          "level": "bound",
          "controllerSide": "player",
          "controlId": "control_counter_player_growth_001",
          "sourceOperation": "counter",
          "restrictedOperations": [ "maneuver", "pressure" ],
          "summary": "Counter must not strengthen player control."
        }
        """);
        var exchangeLog = Assert.IsType<JsonArray>(currentActive["exchangeLog"]);
        exchangeLog.Add(JsonNode.Parse(AddDefaultMatchupAudit($$"""
        {
          "exchangeId": "exchange_counter_strengthens_player_control_001",
          "operationType": "counter",
          "outcome": "success",
          "incomingAction": {
            "operationType": "pressure",
            "actorId": "guardian_opponent",
            "summary": "The Guardian pressures the soul while player control already exists."
          },
          "counterPayoff": {
            "payoffType": "strain_reversal",
            "summary": "The counter also strains the opponent."
          },
          "before": {
            "conflictPosition": "contested",
            "oppositionSideStrain": "clear",
            "controlState": {
              "level": "hindered",
              "controllerSide": "player",
              "controlId": "control_counter_player_growth_001",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Player control already exists before the counter."
            }
          },
          "after": {
            "conflictPosition": "contested",
            "oppositionSideStrain": "strained",
            "controlState": {
              "level": "bound",
              "controllerSide": "player",
              "controlId": "control_counter_player_growth_001",
              "sourceOperation": "counter",
              "restrictedOperations": [ "maneuver", "pressure" ],
              "summary": "Counter must not strengthen player control."
            }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """)));
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, currentRoot.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_counter_creates_fresh_control", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_IncarnationResistanceRequiresForceIncarnationContext()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_incarnation_resistance_wrong_context_001",
          "operationType": "incarnation_resistance",
          "outcome": "success",
          "before": { "conflictPosition": "contested" },
          "after": { "conflictPosition": "player_advantaged" },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_incarnation_resistance_without_force", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ChampionCoordinationRequiresChampionDuel()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_champion_coordination_wrong_context_001",
          "operationType": "champion_coordination",
          "outcome": "success",
          "sideModel": "direct_duel",
          "before": { "conflictPosition": "contested" },
          "after": { "conflictPosition": "player_advantaged" },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_champion_coordination_without_champion", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ChampionCoordinationInChampionDuelIsAllowed()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_champion_coordination_valid_001",
          "operationType": "champion_coordination",
          "outcome": "success",
          "sideModel": "champion_duel",
          "before": { "conflictPosition": "contested" },
          "after": { "conflictPosition": "player_advantaged" },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_champion_coordination_without_champion", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_LightIncarnateConflictExchangeRequiresExplicitDiceModifier()
    {
        await WriteSoulStateWithLightIncarnateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_light_incarnate_missing_001",
          "exchangeAtTurn": 7,
          "operationType": "guard",
          "outcome": "success",
          "before": { "conflictPosition": "contested" },
          "after": { "conflictPosition": "player_advantaged" },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_LightIncarnateConflictExchangeWithoutTurnRequiresAuditTurn()
    {
        await WriteSoulStateWithLightIncarnateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_light_incarnate_missing_turn_001",
          "operationType": "guard",
          "outcome": "success",
          "before": { "conflictPosition": "contested" },
          "after": { "conflictPosition": "player_advantaged" },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_mismatch", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("auditTurn=missing", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_LightIncarnateConflictExchangeWithOnlyCreatedAtTurnRequiresAuditTurn()
    {
        await WriteSoulStateWithLightIncarnateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_light_incarnate_created_turn_only_001",
          "createdAtTurn": 1,
          "operationType": "guard",
          "outcome": "success",
          "before": { "conflictPosition": "contested" },
          "after": { "conflictPosition": "player_advantaged" },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_mismatch", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("auditTurn=missing", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_HistoricalConflictBeforeLightIncarnate_DoesNotRequireRetroactiveModifier()
    {
        await WriteSoulStateWithLightIncarnateAsync();
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "conflictId": "afterlife_conflict_historical_001",
              "resolutionState": "resolved",
              "resolvedAtTurn": 6,
              "operationType": "guard",
              "playerOutcome": "won",
              "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_unauthorized", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_HistoricalConflictWithoutTurnMarkerAfterLightIncarnate_DoesNotRequireRetroactiveModifier()
    {
        await WriteSoulStateWithLightIncarnateAsync();
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "conflictId": "afterlife_conflict_historical_no_turn_001",
              "resolutionState": "resolved",
              "operationType": "guard",
              "playerOutcome": "won",
              "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_unauthorized", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PersistedExchangeWithoutTurnMarkerAfterLightIncarnate_DoesNotRequireRetroactiveModifier()
    {
        await WriteSoulStateWithLightIncarnateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_light_incarnate_persisted_no_turn_001",
          "operationType": "guard",
          "outcome": "success",
          "before": { "conflictPosition": "contested" },
          "after": { "conflictPosition": "player_advantaged" },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_unauthorized", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnRecentConflictWithoutTurnMarkerWithCurrentDice_DoesNotRequireRetroactiveModifier()
    {
        await WriteSoulStateWithLightIncarnateAsync();
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "conflictId": "afterlife_conflict_pre_turn_no_turn_001",
              "resolutionState": "resolved",
              "operationType": "guard",
              "playerOutcome": "won",
              "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
            }
          ]
        }
        """);
        await WriteValidatedConflictSnapshotFromCurrentAsync("Я делаю следующий ход после старого conflict log.");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_unauthorized", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnExchangeWithoutTurnMarkerWithCurrentDice_DoesNotRequireRetroactiveModifier()
    {
        await WriteSoulStateWithLightIncarnateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_pre_turn_no_turn_001",
          "operationType": "guard",
          "outcome": "success",
          "before": { "conflictPosition": "contested" },
          "after": { "conflictPosition": "player_advantaged" },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);
        await WriteValidatedConflictSnapshotFromCurrentAsync("Я продолжаю ход с уже существующим exchange log.");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_unauthorized", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnExchangeWithOlderTurnMarker_DoesNotUseCurrentTurnDiceAuthority()
    {
        await WriteSoulStateAsync();
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "afterlife_conflict_test_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "playerSide": {
              "leadContestant": {
                "actorType": "player",
                "actorId": "player_soul",
                "displayName": "Асуран"
              },
              "supporters": []
            },
            "oppositionSide": {
              "leadContestant": {
                "actorType": "guardian",
                "actorId": "guardian_liora",
                "displayName": "Лиора"
              },
              "supporters": []
            },
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested",
            "resolutionState": "active",
            "exchangeLog": [
              {
                "exchangeId": "exchange_prior_turn_006",
                "exchangeAtTurn": 6,
                "operationType": "guard",
                "outcome": "success",
                "before": { "conflictPosition": "contested" },
                "after": { "conflictPosition": "player_advantaged" },
                "diceAudit": {{BuildPriorTurnDiceAuditJson()}}
              }
            ]
          },
          "recentConflicts": []
        }
        """);
        await WriteValidatedConflictSnapshotFromCurrentAsync("Я продолжаю духовный бой после старого обмена.");
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "afterlife_conflict_test_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "playerSide": {
              "leadContestant": {
                "actorType": "player",
                "actorId": "player_soul",
                "displayName": "Асуран"
              },
              "supporters": []
            },
            "oppositionSide": {
              "leadContestant": {
                "actorType": "guardian",
                "actorId": "guardian_liora",
                "displayName": "Лиора"
              },
              "supporters": []
            },
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "conflictPosition": "contested",
            "resolutionState": "active",
            "exchangeLog": [
              {
                "exchangeId": "exchange_prior_turn_006",
                "exchangeAtTurn": 6,
                "operationType": "guard",
                "outcome": "success",
                "summary": "GM drifted a historical summary, but the dice still belong to the prior accepted turn.",
                "before": { "conflictPosition": "contested" },
                "after": { "conflictPosition": "player_advantaged" },
                "diceAudit": {{BuildPriorTurnDiceAuditJson()}}
              },
              {
                "exchangeId": "exchange_current_turn_007",
                "exchangeAtTurn": 7,
                "operationType": "pressure",
                "outcome": "success",
                "before": {
                  "playerSideStrain": "clear",
                  "oppositionSideStrain": "clear",
                  "conflictPosition": "contested"
                },
                "after": {
                  "playerSideStrain": "clear",
                  "oppositionSideStrain": "strained",
                  "conflictPosition": "contested"
                },
                "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
              }
            ]
          },
          "recentConflicts": []
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_dice_value_not_authorized", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("exchangeLog[0]", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ChangedNoTurnConflictWithCurrentDice_StillRequiresTurnMarker()
    {
        await WriteSoulStateWithLightIncarnateAsync();
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "conflictId": "afterlife_conflict_changed_no_turn_001",
              "resolutionState": "resolved",
              "operationType": "guard",
              "playerOutcome": "won",
              "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
            }
          ]
        }
        """);
        await WriteValidatedConflictSnapshotFromCurrentAsync("Я меняю conflict log текущим ходом.");
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "conflictId": "afterlife_conflict_changed_no_turn_001",
              "resolutionState": "resolved",
              "operationType": "pressure",
              "playerOutcome": "won",
              "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_mismatch", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("auditTurn=missing", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_LightIncarnateModifierBeforeUnlock_IsRejected()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_light_incarnate_pre_unlock_001",
          "exchangeAtTurn": 6,
          "operationType": "guard",
          "outcome": "success",
          "before": { "conflictPosition": "contested" },
          "after": { "conflictPosition": "player_advantaged" },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithLightIncarnate().ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_unauthorized", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_LightIncarnateModifierWithoutFullSourceClosure_IsRejected()
    {
        await WriteSoulStateWithStandaloneLightIncarnateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_light_incarnate_incomplete_closure_001",
          "exchangeAtTurn": 7,
          "operationType": "guard",
          "outcome": "success",
          "before": { "conflictPosition": "contested" },
          "after": { "conflictPosition": "player_advantaged" },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithLightIncarnate().ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_unauthorized", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_LightIncarnateModifierWithoutFullRadianceSourceMarker_IsRejected()
    {
        await WriteSoulStateWithLightIncarnateAsync(markerRadianceExperience: 0, markerRadianceTier: 0);
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_light_incarnate_low_radiance_marker_001",
          "exchangeAtTurn": 7,
          "operationType": "guard",
          "outcome": "success",
          "before": { "conflictPosition": "contested" },
          "after": { "conflictPosition": "player_advantaged" },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithLightIncarnate().ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_unauthorized", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_LightIncarnateConflictExchangeAcceptsExplicitLeadModifier()
    {
        await WriteSoulStateWithLightIncarnateAsync();
        await WriteConflictStateWithRawExchangeAsync("""
        {
          "exchangeId": "exchange_light_incarnate_present_001",
          "exchangeAtTurn": 7,
          "operationType": "guard",
          "outcome": "success",
          "before": { "conflictPosition": "contested" },
          "after": { "conflictPosition": "player_advantaged" },
          "diceAudit": {
            "formulaVersion": "afterlife_spiritual_conflict_v1",
            "diceSource": "input/turn_request.json.preGeneratedDices1d20",
            "diceUsed": [
              {
                "side": "player",
                "sourceIndex": 2,
                "sides": 20,
                "value": 14
              },
              {
                "side": "opposition",
                "sourceIndex": 3,
                "sides": 20,
                "value": 9
              }
            ],
            "playerTotal": 26,
            "oppositionTotal": 14,
            "margin": 12,
            "outcomeBand": "decisive_player_success",
            "modifierBreakdown": {
              "player": [
                {
                  "source": "guard art tier",
                  "value": 2
                },
                {
                  "source": "current Enlightenment rank",
                  "value": 2
                },
                {
                  "passiveId": "light_incarnate",
                  "source": "light_incarnate",
                  "value": 8
                }
              ],
              "opposition": [
                {
                  "source": "guardian pressure art tier",
                  "value": 2
                },
                {
                  "source": "active Guardian Abode pressure",
                  "value": 3
                }
              ]
            }
          }
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_LightIncarnateChampionSideSupporterUsesSupportBonus()
    {
        await WriteSoulStateWithLightIncarnateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_light_incarnate_champion_support_001",
          "exchangeAtTurn": 7,
          "sideModel": "champion_duel",
          "operationType": "guard",
          "outcome": "success",
          "playerSide": {
            "leadContestant": {
              "actorType": "guardian",
              "actorId": "guardian_champion_ally"
            },
            "supporters": [
              {
                "actorType": "player",
                "actorId": "player_soul"
              }
            ]
          },
          "before": { "conflictPosition": "contested" },
          "after": { "conflictPosition": "player_advantaged" },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithLightIncarnate(SourceOfLightCapstoneState.SupportDiceBonus).ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_unauthorized", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_LightIncarnateAssistedDuelUsesLeadBonus()
    {
        await WriteSoulStateWithLightIncarnateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_light_incarnate_assisted_duel_lead_001",
          "exchangeAtTurn": 7,
          "sideModel": "assisted_duel",
          "operationType": "guard",
          "outcome": "success",
          "before": { "conflictPosition": "contested" },
          "after": { "conflictPosition": "player_advantaged" },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithLightIncarnate(SourceOfLightCapstoneState.LeadDiceBonus).ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_unauthorized", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_LightIncarnateAssistedDuelRejectsSupportBonus()
    {
        await WriteSoulStateWithLightIncarnateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_light_incarnate_assisted_duel_support_001",
          "exchangeAtTurn": 7,
          "conflictMode": "assisted_duel",
          "operationType": "guard",
          "outcome": "success",
          "before": { "conflictPosition": "contested" },
          "after": { "conflictPosition": "player_advantaged" },
          "diceAudit": {{BuildPlayerSuccessDiceAuditWithLightIncarnate(SourceOfLightCapstoneState.SupportDiceBonus).ToJsonString()}}
        }
        """);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_NoEffectExchange_RejectsChangedBeforeAfter()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync("""
        {
          "exchangeId": "exchange_no_effect_delta_001",
          "operationType": "guard",
          "outcome": "no_effect",
          "before": { "conflictPosition": "contested" },
          "after": { "conflictPosition": "player_advantaged" }
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_no_effect_has_state_delta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_VoluntaryResolution_DoesNotRequireDiceAudit()
    {
        await WriteSoulStateAsync();
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "mode": "resolve",
              "conflictId": "afterlife_conflict_voluntary_001",
              "resolutionState": "resolved",
              "resolvedAtTurn": 7,
              "operationType": "surrender",
              "playerOutcome": "voluntary_surrender",
              "voluntary": true
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_resolution_missing_dice_audit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SuccessExchange_RejectsSemanticallyIdenticalReorderedBeforeAfter()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync("""
        {
          "exchangeId": "exchange_reordered_no_delta_001",
          "operationType": "pressure",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear"
          },
          "after": {
            "playerSideStrain": "clear",
            "conflictPosition": "contested"
          }
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_exchange_no_state_delta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SuccessExchange_RejectsNoControlEncodingOnlyDelta()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync($$"""
        {
          "exchangeId": "exchange_no_control_encoding_no_delta_001",
          "operationType": "guard",
          "outcome": "success",
          "before": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear"
          },
          "after": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "controlState": { "level": "none" }
          },
          "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_exchange_no_state_delta", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_guard_changes_control", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_NoEffectExchange_AllowsNoControlEncodingOnlyDelta()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync("""
        {
          "exchangeId": "exchange_no_effect_no_control_encoding_001",
          "operationType": "guard",
          "outcome": "no_effect",
          "before": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "controlState": null
          },
          "after": {
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "controlState": { "level": "none" }
          }
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_no_effect_has_state_delta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ExchangeMissingBefore_FailsAuditSnapshotValidation()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync("""
        {
          "exchangeId": "exchange_missing_before_001",
          "operationType": "guard",
          "outcome": "no_effect",
          "after": { "conflictPosition": "contested" }
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_exchange_missing_before", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ExchangeMissingAfter_FailsAuditSnapshotValidation()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync("""
        {
          "exchangeId": "exchange_missing_after_001",
          "operationType": "guard",
          "outcome": "no_effect",
          "before": { "conflictPosition": "contested" }
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_exchange_missing_after", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CounteredExchangeMissingIncomingAction_FailsAuditValidation()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync("""
        {
          "exchangeId": "exchange_countered_missing_incoming_001",
          "operationType": "counter",
          "outcome": "countered",
          "before": { "conflictPosition": "contested" },
          "after": { "conflictPosition": "player_advantaged" }
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_countered_missing_incoming_action", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CounteredExchangeWithIncomingAction_DoesNotReportMissingIncomingAction()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawExchangeAsync("""
        {
          "exchangeId": "exchange_countered_with_incoming_001",
          "operationType": "counter",
          "outcome": "countered",
          "incomingAction": {
            "operationType": "pressure",
            "actorId": "guardian_liora"
          },
          "before": { "conflictPosition": "contested" },
          "after": { "conflictPosition": "player_advantaged" }
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_countered_missing_incoming_action", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_NonObjectSupporter_FailsSupporterItemValidation()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawPlayerSupportersAsync("""[ "guardian_x" ]""");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_invalid_supporter_item", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SupporterObjectMissingRequiredFields_ReportsIndexedMissingFields()
    {
        await WriteSoulStateAsync();
        await WriteConflictStateWithRawPlayerSupportersAsync("""[ {} ]""");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_missing_required_string", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.FilePath, $"{AfterlifeSpiritualConflictState.StatePath}.activeConflict.playerSide.supporters[0].actorType", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PlayerSoulLead_DoesNotRequireNonPlayerArtAuthority()
    {
        await WriteSoulStateAsync();
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        root.Remove("lastInvalidUpdate");
        root.Remove("lastInvalidUpdateReason");
        root.Remove("lastInvalidUpdateAtUtc");
        var lead = (JsonObject)root["activeConflict"]!["playerSide"]!["leadContestant"]!;
        lead["actorType"] = "player_soul";
        lead.Remove("actorArtTierSnapshot");
        lead.Remove("artAuthoritySource");
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_missing_actor_art_snapshot", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains(".playerSide.", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_missing_required_string", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".playerSide.leadContestant.artAuthoritySource", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SummaryOnlyExchangeUpdate_FailsResponseContract()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea",
          "afterlifeSpiritualConflictUpdate": {
            "mode": "exchange",
            "summary": "Игрок удерживает позицию, но состояние сторон не изменилось."
          }
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_exchange_missing_payload", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_ResponseWrappedConflictUpdate_ProjectsBeforeValidation()
    {
        await WriteSoulStateAsync();
        var baseline = BuildRootWithActiveConflictAndInvalidMarkers();
        baseline.Remove("lastInvalidUpdate");
        baseline.Remove("lastInvalidUpdateReason");
        baseline.Remove("lastInvalidUpdateAtUtc");
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_conflict_normalizer_projection.json";
        await _fs.WriteFileAtomicAsync(backupPath, baseline.ToJsonString());
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, """
        {
          "afterlifeSpiritualConflictUpdate": {
            "mode": "exchange",
            "exchange": {
              "exchangeId": "exchange_normalizer_projection_001",
              "operationType": "pressure",
              "outcome": "success",
              "before": { "conflictPosition": "contested" },
              "after": { "conflictPosition": "player_advantaged" }
            }
          }
        }
        """);
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);

        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [AfterlifeSpiritualConflictState.StatePath] = backupPath
        });

        var projectedJson = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath);
        var projected = JsonNode.Parse(projectedJson!)!.AsObject();
        Assert.False(projected.ContainsKey(AfterlifeSpiritualConflictState.ResponseField));
        Assert.Equal("player_advantaged", projected["activeConflict"]?["conflictPosition"]?.GetValue<string>());
        var exchangeLog = Assert.IsType<JsonArray>(projected["activeConflict"]?["exchangeLog"]);
        Assert.Single(exchangeLog);
        var issues = await _validator.ValidateGameStateAsync();
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_state_unprojected_update", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_StartConflict_CopiesSupporterRoleIntoSupportRole()
    {
        await WriteSoulStateAsync();
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, """
        {
          "afterlifeSpiritualConflictUpdate": {
            "mode": "start",
            "conflictState": {
              "conflictId": "afterlife_conflict_support_role_projection",
              "realm": "Chaos Sea",
              "sideModel": "direct_duel",
              "status": "active",
              "resolutionState": "active",
              "conflictPosition": "contested",
              "playerSide": {
                "leadContestant": {
                  "actorType": "player",
                  "actorId": "player_soul",
                  "displayName": "Асуран"
                },
                "supporters": []
              },
              "oppositionSide": {
                "leadContestant": {
                  "actorType": "guardian_manifestation",
                  "actorId": "weak_vortex",
                  "displayName": "Слабый вихрь",
                  "actorArtTierSnapshot": { "pressure": 1 },
                  "artAuthoritySource": "guardian_training_manifestation"
                },
                "supporters": [
                  {
                    "actorType": "guardian",
                    "actorId": "guardian_myriel",
                    "displayName": "Мириэль",
                    "role": "safety_anchor"
                  }
                ]
              },
              "playerSideStrain": "clear",
              "oppositionSideStrain": "clear",
              "exchangeLog": []
            }
          }
        }
        """);
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);

        await normalizer.NormalizeAccumulatedStateAsync();

        var projectedJson = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath);
        var projected = JsonNode.Parse(projectedJson!)!.AsObject();
        Assert.True(projected["activeConflict"] is JsonObject, projectedJson);
        var supporter = (JsonObject)projected["activeConflict"]!["oppositionSide"]!["supporters"]![0]!;
        Assert.Equal("safety_anchor", supporter["supportRole"]!.GetValue<string>());

        var issues = await _validator.ValidateGameStateAsync();
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_missing_required_string", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".oppositionSide.supporters[0].supportRole", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_SkeletalWrappedExchange_UsesBackupActiveConflictBaseline()
    {
        await WriteSoulStateAsync();
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_conflict_skeletal_exchange_projection.json";
        await _fs.WriteFileAtomicAsync(backupPath, BuildActiveConflictRootJson());
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [],
          "afterlifeSpiritualConflictUpdate": {
            "mode": "exchange",
            "exchange": {
              "exchangeId": "exchange_skeletal_projection_001",
              "operationType": "pressure",
              "outcome": "success",
              "before": { "conflictPosition": "contested" },
              "after": { "conflictPosition": "player_advantaged" }
            }
          }
        }
        """);
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);

        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [AfterlifeSpiritualConflictState.StatePath] = backupPath
        });

        var projectedJson = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath);
        var projected = JsonNode.Parse(projectedJson!)!.AsObject();
        Assert.False(projected.ContainsKey(AfterlifeSpiritualConflictState.ResponseField));
        Assert.Equal("afterlife_conflict_test_001", projected["activeConflict"]?["conflictId"]?.GetValue<string>());
        Assert.Equal("player_advantaged", projected["activeConflict"]?["conflictPosition"]?.GetValue<string>());
        var exchangeLog = Assert.IsType<JsonArray>(projected["activeConflict"]?["exchangeLog"]);
        Assert.Contains(exchangeLog, entry =>
            string.Equals(entry?["exchangeId"]?.GetValue<string>(), "exchange_skeletal_projection_001", StringComparison.OrdinalIgnoreCase));
        Assert.False(projected.ContainsKey("lastInvalidUpdateReason"));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_SkeletalWrappedResolve_UsesBackupActiveConflictBaseline()
    {
        await WriteSoulStateAsync();
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_conflict_skeletal_resolve_projection.json";
        await _fs.WriteFileAtomicAsync(backupPath, BuildActiveConflictRootJson());
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [],
          "afterlifeSpiritualConflictUpdate": {
            "mode": "resolve",
            "resolution": {
              "conflictId": "afterlife_conflict_test_001",
              "resolvedAtTurn": 7,
              "guardianId": "guardian_liora",
              "operationType": "negotiate",
              "playerOutcome": "conceded"
            }
          }
        }
        """);
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);

        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [AfterlifeSpiritualConflictState.StatePath] = backupPath
        });

        var projectedJson = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath);
        var projected = JsonNode.Parse(projectedJson!)!.AsObject();
        Assert.False(projected.ContainsKey(AfterlifeSpiritualConflictState.ResponseField));
        Assert.Null(projected["activeConflict"]);
        var recentConflicts = Assert.IsType<JsonArray>(projected["recentConflicts"]);
        var proof = Assert.IsType<JsonObject>(Assert.Single(recentConflicts));
        Assert.Equal("afterlife_conflict_test_001", proof["conflictId"]?.GetValue<string>());
        Assert.Equal("resolved", proof["resolutionState"]?.GetValue<string>());
        Assert.False(projected.ContainsKey("lastInvalidUpdateReason"));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_CurrentLiveConflictWithWrapper_PrefersCurrentRoot()
    {
        await WriteSoulStateAsync();
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_conflict_current_live_projection.json";
        await _fs.WriteFileAtomicAsync(backupPath, BuildActiveConflictRootJson("afterlife_conflict_backup_001"));
        var currentRoot = JsonNode.Parse(BuildActiveConflictRootJson("afterlife_conflict_current_001"))!.AsObject();
        currentRoot[AfterlifeSpiritualConflictState.ResponseField] = JsonNode.Parse("""
        {
          "mode": "exchange",
          "exchange": {
            "exchangeId": "exchange_current_live_projection_001",
            "operationType": "pressure",
            "outcome": "success",
            "before": { "conflictPosition": "contested" },
            "after": { "conflictPosition": "player_advantaged" }
          }
        }
        """)!.AsObject();
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, currentRoot.ToJsonString());
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);

        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [AfterlifeSpiritualConflictState.StatePath] = backupPath
        });

        var projectedJson = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath);
        var projected = JsonNode.Parse(projectedJson!)!.AsObject();
        Assert.Equal("afterlife_conflict_current_001", projected["activeConflict"]?["conflictId"]?.GetValue<string>());
        Assert.Equal("player_advantaged", projected["activeConflict"]?["conflictPosition"]?.GetValue<string>());
        Assert.False(projected.ContainsKey(AfterlifeSpiritualConflictState.ResponseField));
    }

    [Fact]
    public async Task StateDistributor_ValidConflictUpdate_StripsStaleResponseWrapper()
    {
        await WriteSoulStateAsync();
        var existingRoot = BuildRootWithActiveConflictAndInvalidMarkers();
        existingRoot[AfterlifeSpiritualConflictState.ResponseField] = new JsonObject
        {
            ["mode"] = "exchange",
            ["summary"] = "stale wrapper from a previous malformed response"
        };
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, existingRoot.ToJsonString());

        using var updateDoc = JsonDocument.Parse("""
        {
          "mode": "exchange",
          "exchange": {
            "exchangeId": "exchange_distributor_projection_001",
            "operationType": "pressure",
            "outcome": "success",
            "before": { "conflictPosition": "contested" },
            "after": { "conflictPosition": "player_advantaged" }
          }
        }
        """);
        var distributor = new StateDistributor(_fs, NullLogger<StateDistributor>.Instance);

        await distributor.DistributeAsync(new GameResponse
        {
            AfterlifeSpiritualConflictUpdate = updateDoc.RootElement.Clone()
        });

        var projectedJson = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath);
        var projected = JsonNode.Parse(projectedJson!)!.AsObject();
        Assert.False(projected.ContainsKey(AfterlifeSpiritualConflictState.ResponseField));
        Assert.Equal("player_advantaged", projected["activeConflict"]?["conflictPosition"]?.GetValue<string>());
        var exchangeLog = Assert.IsType<JsonArray>(projected["activeConflict"]?["exchangeLog"]);
        Assert.Contains(exchangeLog, entry =>
            string.Equals(entry?["exchangeId"]?.GetValue<string>(), "exchange_distributor_projection_001", StringComparison.OrdinalIgnoreCase));

        var issues = await _validator.ValidateGameStateAsync();
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_state_unprojected_update", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task StateDistributor_StartConflict_InitializesPlayerActionEconomyFromSpiritFocus()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea",
          "afterlifeCombatProfile": {
            "schemaVersion": 1,
            "spiritFocusTier": 2,
            "artTiers": {}
          }
        }
        """);
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": []
        }
        """);
        using var updateDoc = JsonDocument.Parse("""
        {
          "mode": "start",
          "conflictSeed": {
            "conflictId": "afterlife_conflict_focus_start_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "playerSide": {
              "leadContestant": {
                "actorType": "player",
                "actorId": "player_soul",
                "displayName": "Асуран"
              },
              "supporters": []
            },
            "oppositionSide": {
              "leadContestant": {
                "actorType": "guardian",
                "actorId": "guardian_liora",
                "displayName": "Лиора",
                "actorArtTierSnapshot": {
                  "pressure": 2
                },
                "artAuthoritySource": "guardian_state"
              },
              "supporters": []
            },
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested"
          }
        }
        """);
        var distributor = new StateDistributor(_fs, NullLogger<StateDistributor>.Instance);

        await distributor.DistributeAsync(new GameResponse
        {
            AfterlifeSpiritualConflictUpdate = updateDoc.RootElement.Clone()
        });

        var projected = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath) ?? "{}")!)!.AsObject();
        var playerPool = Assert.IsType<JsonObject>(projected["activeConflict"]?["actionEconomy"]?["player"]);
        Assert.Equal(8, playerPool["current"]?.GetValue<int>());
        Assert.Equal(8, playerPool["max"]?.GetValue<int>());
        Assert.Equal("Средоточие Души tier 2", playerPool["source"]?.GetValue<string>());
    }

    [Fact]
    public async Task ValidateGameStateAsync_SpiritFocusTierOutsideBounds_FailsCombatProfileValidation()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea",
          "afterlifeCombatProfile": {
            "schemaVersion": 1,
            "spiritFocusTier": 6,
            "artTiers": {}
          }
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_combat_profile_invalid_spirit_focus_tier", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CanonicalAccumulatedFiles_DoesNotRequireAfterlifeSpiritualConflictState()
    {
        Assert.DoesNotContain(
            AfterlifeSpiritualConflictState.StatePath,
            CanonicalStateNormalizer.CanonicalAccumulatedFiles);
    }

    [Fact]
    public void ApplyUpdate_SummaryOnlyExchange_MarksInvalidInsteadOfAppendingMalformedExchange()
    {
        var root = JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "afterlife_conflict_test_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "exchangeLog": []
          },
          "recentConflicts": []
        }
        """)!.AsObject();
        var update = JsonNode.Parse("""
        {
          "mode": "exchange",
          "summary": "Нет измеримого эффекта."
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        Assert.Equal("exchange_missing_exchange_object", projected["lastInvalidUpdateReason"]?.GetValue<string>());
        var exchangeLog = Assert.IsType<JsonArray>(projected["activeConflict"]?["exchangeLog"]);
        Assert.Empty(exchangeLog);
    }

    [Fact]
    public void ApplyUpdate_StartWhileConflictActive_MarksInvalidAndPreservesExistingConflict()
    {
        var root = JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "afterlife_conflict_existing_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "exchangeLog": [
              {
                "exchangeId": "exchange_existing_001",
                "operationType": "guard",
                "outcome": "no_effect",
                "before": { "conflictPosition": "contested" },
                "after": { "conflictPosition": "contested" }
              }
            ]
          },
          "recentConflicts": []
        }
        """)!.AsObject();
        var update = JsonNode.Parse("""
        {
          "mode": "start",
          "conflictState": {
            "conflictId": "afterlife_conflict_new_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "exchangeLog": []
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        Assert.Equal("start_while_conflict_active", projected["lastInvalidUpdateReason"]?.GetValue<string>());
        Assert.Equal("afterlife_conflict_existing_001", projected["activeConflict"]?["conflictId"]?.GetValue<string>());
        var exchangeLog = Assert.IsType<JsonArray>(projected["activeConflict"]?["exchangeLog"]);
        Assert.Single(exchangeLog);
    }

    [Fact]
    public void ApplyUpdate_StartMissingRealm_MarksInvalidAndDoesNotCreateConflict()
    {
        var root = JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": []
        }
        """)!.AsObject();
        var update = JsonNode.Parse("""
        {
          "mode": "start",
          "conflictState": {
            "conflictId": "afterlife_conflict_missing_realm_001",
            "sideModel": "direct_duel",
            "exchangeLog": []
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        Assert.Equal("start_missing_realm", projected["lastInvalidUpdateReason"]?.GetValue<string>());
        Assert.Null(projected["activeConflict"]);
    }

    [Fact]
    public void ApplyUpdate_StartUsesExplicitUpdateRealm_WhenConflictRealmMissing()
    {
        var root = JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": []
        }
        """)!.AsObject();
        var update = JsonNode.Parse("""
        {
          "mode": "start",
          "realm": "Shining Abode",
          "conflictState": {
            "conflictId": "afterlife_conflict_shining_001",
            "sideModel": "direct_duel",
            "exchangeLog": []
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        Assert.Equal("Shining Abode", projected["activeConflict"]?["realm"]?.GetValue<string>());
    }

    [Fact]
    public void ApplyUpdate_StartInvalidRealm_MarksInvalid()
    {
        var root = JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": []
        }
        """)!.AsObject();
        var update = JsonNode.Parse("""
        {
          "mode": "start",
          "realm": "MortalWorldProfile",
          "conflictState": {
            "conflictId": "afterlife_conflict_wrong_realm_001",
            "sideModel": "direct_duel",
            "exchangeLog": []
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        Assert.Equal("start_invalid_realm", projected["lastInvalidUpdateReason"]?.GetValue<string>());
        Assert.Null(projected["activeConflict"]);
    }

    [Fact]
    public void ApplyUpdate_ExchangeAfterInvalidUpdate_ClearsInvalidMarkers()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var update = JsonNode.Parse("""
        {
          "mode": "exchange",
          "exchange": {
            "exchangeId": "exchange_001",
            "operationType": "guard",
            "outcome": "no_effect",
            "before": { "conflictPosition": "contested" },
            "after": { "conflictPosition": "contested" }
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        var exchangeLog = Assert.IsType<JsonArray>(projected["activeConflict"]?["exchangeLog"]);
        Assert.Single(exchangeLog);
    }

    [Fact]
    public void ApplyUpdate_ExchangeAppliesAfterSnapshotToActiveConflict()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var update = JsonNode.Parse("""
        {
          "mode": "exchange",
          "exchange": {
            "exchangeId": "exchange_after_projection_001",
            "operationType": "pressure",
            "outcome": "success",
            "before": {
              "playerSideStrain": "clear",
              "oppositionSideStrain": "clear",
              "conflictPosition": "contested"
            },
            "after": {
              "playerSideStrain": "strained",
              "oppositionSideStrain": "clear",
              "conflictPosition": "player_advantaged",
              "controlState": {
                "level": "hindered",
                "controllerSide": "player",
                "controlId": "control_after_projection_001",
                "sourceOperation": "binding",
                "restrictedOperations": [ "maneuver" ],
                "summary": "Projected control state."
              },
              "resolutionState": "active",
              "status": "active"
            }
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        var active = Assert.IsType<JsonObject>(projected["activeConflict"]);
        Assert.Equal("player_advantaged", active["conflictPosition"]?.GetValue<string>());
        Assert.Equal("strained", active["playerSideStrain"]?.GetValue<string>());
        Assert.Equal("clear", active["oppositionSideStrain"]?.GetValue<string>());
        Assert.Equal("hindered", active["controlState"]?["level"]?.GetValue<string>());
        Assert.Equal("player", active["controlState"]?["controllerSide"]?.GetValue<string>());
        Assert.Equal("active", active["resolutionState"]?.GetValue<string>());
        Assert.Equal("active", active["status"]?.GetValue<string>());
        var exchangeLog = Assert.IsType<JsonArray>(active["exchangeLog"]);
        Assert.Single(exchangeLog);
    }

    [Fact]
    public void ApplyUpdate_ExchangeRootFieldsOverrideAfterSnapshot()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var update = JsonNode.Parse("""
        {
          "mode": "exchange",
          "conflictPosition": "opposition_advantaged",
          "playerSideStrain": "clear",
          "exchange": {
            "exchangeId": "exchange_after_override_001",
            "operationType": "pressure",
            "outcome": "success",
            "before": { "conflictPosition": "contested" },
            "after": {
              "conflictPosition": "player_advantaged",
              "playerSideStrain": "strained"
            }
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        var active = Assert.IsType<JsonObject>(projected["activeConflict"]);
        Assert.Equal("opposition_advantaged", active["conflictPosition"]?.GetValue<string>());
        Assert.Equal("clear", active["playerSideStrain"]?.GetValue<string>());
    }

    [Fact]
    public void ApplyUpdate_ExchangeRootControlStateDoesNotOverrideValidatedAfterSnapshot()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var activeBefore = Assert.IsType<JsonObject>(root["activeConflict"]);
        activeBefore["controlState"] = JsonNode.Parse("""
        {
          "level": "bound",
          "controllerSide": "opposition",
          "controlId": "control_root_override_should_not_clear_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver", "binding" ],
          "summary": "Existing opposition control is active."
        }
        """);
        var update = JsonNode.Parse("""
        {
          "mode": "exchange",
          "controlState": null,
          "exchange": {
            "exchangeId": "exchange_root_control_override_001",
            "operationType": "pressure",
            "outcome": "success",
            "before": {
              "conflictPosition": "contested",
              "oppositionSideStrain": "clear",
              "controlState": {
                "level": "bound",
                "controllerSide": "opposition",
                "controlId": "control_root_override_should_not_clear_001",
                "sourceOperation": "binding",
                "restrictedOperations": [ "maneuver", "binding" ],
                "summary": "Existing opposition control is active."
              }
            },
            "after": {
              "conflictPosition": "contested",
              "oppositionSideStrain": "strained",
              "controlState": {
                "level": "bound",
                "controllerSide": "opposition",
                "controlId": "control_root_override_should_not_clear_001",
                "sourceOperation": "binding",
                "restrictedOperations": [ "maneuver", "binding" ],
                "summary": "Existing opposition control is still active."
              }
            }
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        var active = Assert.IsType<JsonObject>(projected["activeConflict"]);
        var control = Assert.IsType<JsonObject>(active["controlState"]);
        Assert.Equal("bound", control["level"]?.GetValue<string>());
        Assert.Equal("opposition", control["controllerSide"]?.GetValue<string>());
        Assert.Equal("control_root_override_should_not_clear_001", control["controlId"]?.GetValue<string>());
    }

    [Fact]
    public void ApplyUpdate_ExchangeReplacementPreservesControlStateWhenAfterSnapshotKeepsControl()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var activeBefore = Assert.IsType<JsonObject>(root["activeConflict"]);
        activeBefore["controlState"] = JsonNode.Parse("""
        {
          "level": "bound",
          "controllerSide": "opposition",
          "controlId": "control_replacement_should_preserve_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver", "binding" ],
          "summary": "Existing opposition control is active."
        }
        """);
        var update = JsonNode.Parse("""
        {
          "mode": "exchange",
          "activeConflictAfter": {
            "conflictId": "afterlife_conflict_test_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "resolutionState": "active",
            "exchangeLog": []
          },
          "exchange": {
            "exchangeId": "exchange_replacement_preserve_control_001",
            "operationType": "pressure",
            "outcome": "success",
            "before": {
              "conflictPosition": "contested",
              "oppositionSideStrain": "clear",
              "controlState": {
                "level": "bound",
                "controllerSide": "opposition",
                "controlId": "control_replacement_should_preserve_001",
                "sourceOperation": "binding",
                "restrictedOperations": [ "maneuver", "binding" ],
                "summary": "Existing opposition control is active."
              }
            },
            "after": {
              "conflictPosition": "contested",
              "oppositionSideStrain": "strained",
              "controlState": {
                "level": "bound",
                "controllerSide": "opposition",
                "controlId": "control_replacement_should_preserve_001",
                "sourceOperation": "binding",
                "restrictedOperations": [ "maneuver", "binding" ],
                "summary": "Existing opposition control is still active."
              }
            }
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        var active = Assert.IsType<JsonObject>(projected["activeConflict"]);
        var control = Assert.IsType<JsonObject>(active["controlState"]);
        Assert.Equal("bound", control["level"]?.GetValue<string>());
        Assert.Equal("opposition", control["controllerSide"]?.GetValue<string>());
        Assert.Equal("control_replacement_should_preserve_001", control["controlId"]?.GetValue<string>());
    }

    [Fact]
    public void ApplyUpdate_ExchangeReplacementDoesNotClearControlForNonAntiControlOperation()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var activeBefore = Assert.IsType<JsonObject>(root["activeConflict"]);
        activeBefore["controlState"] = JsonNode.Parse("""
        {
          "level": "bound",
          "controllerSide": "opposition",
          "controlId": "control_replacement_non_anti_preserve_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver", "binding" ],
          "summary": "Existing opposition control is active."
        }
        """);
        var update = JsonNode.Parse("""
        {
          "mode": "exchange",
          "activeConflictAfter": {
            "conflictId": "afterlife_conflict_test_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "resolutionState": "active",
            "exchangeLog": []
          },
          "exchange": {
            "exchangeId": "exchange_replacement_non_anti_control_001",
            "operationType": "pressure",
            "outcome": "success",
            "before": {
              "conflictPosition": "contested",
              "oppositionSideStrain": "clear"
            },
            "after": {
              "conflictPosition": "contested",
              "oppositionSideStrain": "strained"
            }
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        var active = Assert.IsType<JsonObject>(projected["activeConflict"]);
        var control = Assert.IsType<JsonObject>(active["controlState"]);
        Assert.Equal("control_replacement_non_anti_preserve_001", control["controlId"]?.GetValue<string>());
    }

    [Fact]
    public void ApplyUpdate_ExchangeReplacementWithOmittedAntiControlSnapshot_PreservesActiveControlState()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var activeBefore = Assert.IsType<JsonObject>(root["activeConflict"]);
        activeBefore["controlState"] = JsonNode.Parse("""
        {
          "level": "bound",
          "controllerSide": "opposition",
          "controlId": "control_replacement_anti_omitted_preserve_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver", "binding" ],
          "summary": "Existing opposition control is active."
        }
        """);
        var update = JsonNode.Parse("""
        {
          "mode": "exchange",
          "activeConflictAfter": {
            "conflictId": "afterlife_conflict_test_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "resolutionState": "active",
            "exchangeLog": []
          },
          "exchange": {
            "exchangeId": "exchange_replacement_break_binding_omits_control_001",
            "operationType": "break_binding",
            "outcome": "success",
            "before": {
              "conflictPosition": "contested",
              "controlState": {
                "level": "bound",
                "controllerSide": "opposition",
                "controlId": "control_replacement_anti_omitted_preserve_001",
                "sourceOperation": "binding",
                "restrictedOperations": [ "maneuver", "binding" ],
                "summary": "Existing opposition control is active."
              }
            },
            "after": {
              "conflictPosition": "contested"
            }
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        var active = Assert.IsType<JsonObject>(projected["activeConflict"]);
        var control = Assert.IsType<JsonObject>(active["controlState"]);
        Assert.Equal("control_replacement_anti_omitted_preserve_001", control["controlId"]?.GetValue<string>());
        Assert.Equal("bound", control["level"]?.GetValue<string>());
        Assert.Equal("opposition", control["controllerSide"]?.GetValue<string>());
    }

    [Fact]
    public void ApplyUpdate_ExchangeReplacementWithExplicitNullControlState_IsRespectedWhenAfterOmitsControlState()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var activeBefore = Assert.IsType<JsonObject>(root["activeConflict"]);
        activeBefore["controlState"] = JsonNode.Parse("""
        {
          "level": "bound",
          "controllerSide": "opposition",
          "controlId": "control_replacement_null_should_win_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver", "binding" ],
          "summary": "Existing opposition control is active."
        }
        """);
        var update = JsonNode.Parse("""
        {
          "mode": "exchange",
          "activeConflictAfter": {
            "conflictId": "afterlife_conflict_test_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "controlState": null,
            "resolutionState": "active",
            "exchangeLog": []
          },
          "exchange": {
            "exchangeId": "exchange_replacement_null_control_omitted_after_001",
            "operationType": "break_binding",
            "outcome": "success",
            "before": {
              "conflictPosition": "contested",
              "controlState": {
                "level": "bound",
                "controllerSide": "opposition",
                "controlId": "control_replacement_null_should_win_001",
                "sourceOperation": "binding",
                "restrictedOperations": [ "maneuver", "binding" ],
                "summary": "Existing opposition control is active."
              }
            },
            "after": {
              "conflictPosition": "contested"
            }
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        var active = Assert.IsType<JsonObject>(projected["activeConflict"]);
        Assert.True(active.ContainsKey("controlState"));
        Assert.Null(active["controlState"]);
    }

    [Fact]
    public void ApplyUpdate_ExchangeReplacementWithExplicitControlState_IsRespectedWhenAfterOmitsControlState()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var activeBefore = Assert.IsType<JsonObject>(root["activeConflict"]);
        activeBefore["controlState"] = JsonNode.Parse("""
        {
          "level": "bound",
          "controllerSide": "opposition",
          "controlId": "control_replacement_object_old_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver", "binding" ],
          "summary": "Existing opposition control is active."
        }
        """);
        var update = JsonNode.Parse("""
        {
          "mode": "exchange",
          "activeConflictAfter": {
            "conflictId": "afterlife_conflict_test_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "conflictPosition": "contested",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "controlState": {
              "level": "hindered",
              "controllerSide": "opposition",
              "controlId": "control_replacement_object_new_001",
              "sourceOperation": "break_binding",
              "restrictedOperations": [ "maneuver" ],
              "summary": "The replacement state weakens the old control."
            },
            "resolutionState": "active",
            "exchangeLog": []
          },
          "exchange": {
            "exchangeId": "exchange_replacement_object_control_omitted_after_001",
            "operationType": "break_binding",
            "outcome": "success",
            "before": {
              "conflictPosition": "contested",
              "controlState": {
                "level": "bound",
                "controllerSide": "opposition",
                "controlId": "control_replacement_object_old_001",
                "sourceOperation": "binding",
                "restrictedOperations": [ "maneuver", "binding" ],
                "summary": "Existing opposition control is active."
              }
            },
            "after": {
              "conflictPosition": "contested"
            }
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        var active = Assert.IsType<JsonObject>(projected["activeConflict"]);
        var control = Assert.IsType<JsonObject>(active["controlState"]);
        Assert.Equal("control_replacement_object_new_001", control["controlId"]?.GetValue<string>());
        Assert.Equal("hindered", control["level"]?.GetValue<string>());
        Assert.Equal("opposition", control["controllerSide"]?.GetValue<string>());
    }

    [Fact]
    public void ApplyUpdate_NoEffectExchange_DoesNotApplyAfterSnapshotOrRootOverrides()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var update = JsonNode.Parse("""
        {
          "mode": "exchange",
          "conflictPosition": "player_advantaged",
          "playerSideStrain": "strained",
          "exchange": {
            "exchangeId": "exchange_no_effect_stale_snapshot_001",
            "operationType": "guard",
            "outcome": "no_effect",
            "before": {
              "conflictPosition": "opposition_advantaged",
              "playerSideStrain": "fractured"
            },
            "after": {
              "conflictPosition": "opposition_advantaged",
              "playerSideStrain": "fractured"
            }
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        var active = Assert.IsType<JsonObject>(projected["activeConflict"]);
        Assert.Equal("contested", active["conflictPosition"]?.GetValue<string>());
        Assert.Equal("clear", active["playerSideStrain"]?.GetValue<string>());
        var exchangeLog = Assert.IsType<JsonArray>(active["exchangeLog"]);
        Assert.Contains(exchangeLog, entry =>
            string.Equals(entry?["exchangeId"]?.GetValue<string>(), "exchange_no_effect_stale_snapshot_001", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ApplyUpdate_NoEffectExchangeWithReplacement_MarksInvalidAndPreservesActiveConflict()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var update = JsonNode.Parse("""
        {
          "mode": "exchange",
          "exchange": {
            "exchangeId": "exchange_no_effect_replacement_001",
            "operationType": "guard",
            "outcome": "no_effect",
            "before": { "conflictPosition": "contested" },
            "after": { "conflictPosition": "contested" }
          },
          "activeConflictAfter": {
            "conflictId": "afterlife_conflict_test_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "conflictPosition": "player_advantaged",
            "exchangeLog": []
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        Assert.Equal("exchange_no_effect_state_replacement", projected["lastInvalidUpdateReason"]?.GetValue<string>());
        var active = Assert.IsType<JsonObject>(projected["activeConflict"]);
        Assert.Equal("contested", active["conflictPosition"]?.GetValue<string>());
        var exchangeLog = Assert.IsType<JsonArray>(active["exchangeLog"]);
        Assert.Empty(exchangeLog);
    }

    [Fact]
    public void ApplyUpdate_ResolveAfterInvalidUpdate_ClearsInvalidMarkers()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var update = JsonNode.Parse("""
        {
          "mode": "resolve",
          "resolution": {
            "conflictId": "afterlife_conflict_test_001",
            "resolvedAtTurn": 7,
            "guardianId": "guardian_liora",
            "operationType": "force_incarnation",
            "playerOutcome": "lost"
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        Assert.Null(projected["activeConflict"]);
        var recentConflicts = Assert.IsType<JsonArray>(projected["recentConflicts"]);
        Assert.Single(recentConflicts);
        Assert.Equal("force_incarnation", recentConflicts[0]?["operationType"]?.GetValue<string>());
        Assert.Equal("lost", recentConflicts[0]?["playerOutcome"]?.GetValue<string>());
        Assert.Equal(7, recentConflicts[0]?["resolvedAtTurn"]?.GetValue<int>());
    }

    [Theory]
    [InlineData("player_loss")]
    [InlineData("player_surrender")]
    [InlineData("player_concession")]
    public void ApplyUpdate_ResolveForcedIncarnation_AcceptsResolutionKindWithoutPlayerOutcome(string resolutionKind)
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var update = JsonNode.Parse($$"""
        {
          "mode": "resolve",
          "resolution": {
            "conflictId": "afterlife_conflict_test_001",
            "resolvedAtTurn": 7,
            "guardianId": "guardian_liora",
            "operationType": "force_incarnation",
            "resolutionKind": {{JsonSerializer.Serialize(resolutionKind)}}
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        Assert.Null(projected["activeConflict"]);
        var recentConflicts = Assert.IsType<JsonArray>(projected["recentConflicts"]);
        Assert.Single(recentConflicts);
        Assert.Equal(resolutionKind, recentConflicts[0]?["resolutionKind"]?.GetValue<string>());
        Assert.Null(recentConflicts[0]?["playerOutcome"]);
    }

    [Fact]
    public void ApplyUpdate_ResolveForcedIncarnationInvalidResolutionKindWithoutPlayerOutcome_MarksInvalid()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var update = JsonNode.Parse("""
        {
          "mode": "resolve",
          "resolution": {
            "conflictId": "afterlife_conflict_test_001",
            "resolvedAtTurn": 7,
            "guardianId": "guardian_liora",
            "operationType": "force_incarnation",
            "resolutionKind": "player_victory"
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        Assert.Equal("resolve_incomplete_resolution", projected["lastInvalidUpdateReason"]?.GetValue<string>());
        Assert.Equal("afterlife_conflict_test_001", projected["activeConflict"]?["conflictId"]?.GetValue<string>());
        var recentConflicts = Assert.IsType<JsonArray>(projected["recentConflicts"]);
        Assert.Empty(recentConflicts);
    }

    [Fact]
    public void ApplyUpdate_ResolveResidentConflict_AcceptsResolvedActorIdWithoutGuardianId()
    {
        var root = BuildRootWithOppositionLead("resident", "resident_mira", "Мира");
        var update = JsonNode.Parse("""
        {
          "mode": "resolve",
          "resolution": {
            "conflictId": "afterlife_conflict_test_001",
            "resolvedAtTurn": 7,
            "resolvedActorId": "resident_mira",
            "operationType": "guard",
            "playerOutcome": "won"
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        Assert.Null(projected["activeConflict"]);
        var recentConflicts = Assert.IsType<JsonArray>(projected["recentConflicts"]);
        Assert.Single(recentConflicts);
        Assert.Equal("resident_mira", recentConflicts[0]?["resolvedActorId"]?.GetValue<string>());
    }

    [Fact]
    public void ApplyUpdate_ResolveRadiantActorConflict_AcceptsOppositionActorIdWithoutGuardianId()
    {
        var root = BuildRootWithOppositionLead("radiant_actor", "radiant_head_elian", "Элиан");
        var update = JsonNode.Parse("""
        {
          "mode": "resolve",
          "resolution": {
            "conflictId": "afterlife_conflict_test_001",
            "resolvedAtTurn": 7,
            "oppositionActorId": "radiant_head_elian",
            "operationType": "counter",
            "playerOutcome": "conceded"
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        Assert.Null(projected["activeConflict"]);
        var recentConflicts = Assert.IsType<JsonArray>(projected["recentConflicts"]);
        Assert.Single(recentConflicts);
        Assert.Equal("radiant_head_elian", recentConflicts[0]?["oppositionActorId"]?.GetValue<string>());
    }

    [Fact]
    public void ApplyUpdate_ResolveWithoutActiveConflict_MarksInvalidAndDoesNotAppendRecentConflict()
    {
        var root = JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": []
        }
        """)!.AsObject();
        var update = JsonNode.Parse("""
        {
          "mode": "resolve",
          "resolution": {
            "conflictId": "afterlife_conflict_fabricated_001",
            "resolvedAtTurn": 7,
            "guardianId": "guardian_liora",
            "operationType": "force_incarnation",
            "playerOutcome": "lost"
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        Assert.Equal("resolve_without_active_conflict", projected["lastInvalidUpdateReason"]?.GetValue<string>());
        Assert.Null(projected["activeConflict"]);
        var recentConflicts = Assert.IsType<JsonArray>(projected["recentConflicts"]);
        Assert.Empty(recentConflicts);
    }

    [Fact]
    public void ApplyUpdate_ResolveNonGuardianConflictMissingActorReference_MarksInvalidAndPreservesActiveConflict()
    {
        var root = BuildRootWithOppositionLead("resident", "resident_mira", "Мира");
        var update = JsonNode.Parse("""
        {
          "mode": "resolve",
          "resolution": {
            "conflictId": "afterlife_conflict_test_001",
            "resolvedAtTurn": 7,
            "operationType": "guard",
            "playerOutcome": "won"
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        Assert.Equal("resolve_incomplete_resolution", projected["lastInvalidUpdateReason"]?.GetValue<string>());
        Assert.Equal("afterlife_conflict_test_001", projected["activeConflict"]?["conflictId"]?.GetValue<string>());
        var recentConflicts = Assert.IsType<JsonArray>(projected["recentConflicts"]);
        Assert.Empty(recentConflicts);
    }

    [Fact]
    public void ApplyUpdate_ResolveGuardianConflictWithMatchingResolvedActorId_AcceptsWithoutGuardianId()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var update = JsonNode.Parse("""
        {
          "mode": "resolve",
          "resolution": {
            "conflictId": "afterlife_conflict_test_001",
            "resolvedAtTurn": 7,
            "resolvedActorId": "guardian_liora",
            "operationType": "guard",
            "playerOutcome": "won"
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        Assert.Null(projected["activeConflict"]);
        var recentConflicts = Assert.IsType<JsonArray>(projected["recentConflicts"]);
        Assert.Single(recentConflicts);
        Assert.Equal("guardian_liora", recentConflicts[0]?["resolvedActorId"]?.GetValue<string>());
    }

    [Fact]
    public void ApplyUpdate_ResolveMissingResolution_MarksInvalidAndPreservesActiveConflict()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var update = JsonNode.Parse("""
        {
          "mode": "resolve"
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        Assert.Equal("resolve_missing_resolution", projected["lastInvalidUpdateReason"]?.GetValue<string>());
        Assert.Equal("afterlife_conflict_test_001", projected["activeConflict"]?["conflictId"]?.GetValue<string>());
        var recentConflicts = Assert.IsType<JsonArray>(projected["recentConflicts"]);
        Assert.Empty(recentConflicts);
    }

    [Fact]
    public void ApplyUpdate_ResolveIncompleteResolution_MarksInvalidAndPreservesActiveConflict()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var update = JsonNode.Parse("""
        {
          "mode": "resolve",
          "resolution": {
            "conflictId": "afterlife_conflict_test_001",
            "guardianId": "guardian_liora",
            "playerOutcome": "lost"
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        Assert.Equal("resolve_incomplete_resolution", projected["lastInvalidUpdateReason"]?.GetValue<string>());
        Assert.Equal("afterlife_conflict_test_001", projected["activeConflict"]?["conflictId"]?.GetValue<string>());
        var recentConflicts = Assert.IsType<JsonArray>(projected["recentConflicts"]);
        Assert.Empty(recentConflicts);
    }

    [Fact]
    public void ApplyUpdate_ResolveConflictIdMismatch_MarksInvalidAndPreservesActiveConflict()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var update = JsonNode.Parse("""
        {
          "mode": "resolve",
          "resolution": {
            "conflictId": "afterlife_conflict_other_001",
            "resolvedAtTurn": 7,
            "guardianId": "guardian_liora",
            "operationType": "force_incarnation",
            "playerOutcome": "lost"
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        Assert.Equal("resolve_conflict_id_mismatch", projected["lastInvalidUpdateReason"]?.GetValue<string>());
        Assert.Equal("afterlife_conflict_test_001", projected["activeConflict"]?["conflictId"]?.GetValue<string>());
        var recentConflicts = Assert.IsType<JsonArray>(projected["recentConflicts"]);
        Assert.Empty(recentConflicts);
    }

    [Fact]
    public void ApplyUpdate_ResolveGuardianIdMismatch_MarksInvalidAndPreservesActiveConflict()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var update = JsonNode.Parse("""
        {
          "mode": "resolve",
          "resolution": {
            "conflictId": "afterlife_conflict_test_001",
            "resolvedAtTurn": 7,
            "guardianId": "guardian_other",
            "operationType": "force_incarnation",
            "playerOutcome": "lost"
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        Assert.Equal("resolve_guardian_id_mismatch", projected["lastInvalidUpdateReason"]?.GetValue<string>());
        Assert.Equal("afterlife_conflict_test_001", projected["activeConflict"]?["conflictId"]?.GetValue<string>());
        var recentConflicts = Assert.IsType<JsonArray>(projected["recentConflicts"]);
        Assert.Empty(recentConflicts);
    }

    [Fact]
    public void ApplyUpdate_RepairCancelWithoutActiveConflict_ClearsInvalidMarkersWithoutRecentProof()
    {
        var root = JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [],
          "lastInvalidUpdate": { "mode": "resolve" },
          "lastInvalidUpdateReason": "resolve_without_active_conflict",
          "lastInvalidUpdateAtUtc": "2026-05-06T00:00:00Z"
        }
        """)!.AsObject();
        var update = JsonNode.Parse("""
        {
          "mode": "repair_cancel",
          "resolution": {
            "summary": "No active conflict remained; repair clears the invalid marker only."
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        Assert.Null(projected["activeConflict"]);
        var recentConflicts = Assert.IsType<JsonArray>(projected["recentConflicts"]);
        Assert.Empty(recentConflicts);
    }

    [Fact]
    public void ApplyUpdate_ExchangeWithReplacementPreservesAppendedExchange()
    {
        var root = JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "afterlife_conflict_test_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "exchangeLog": [
              {
                "exchangeId": "exchange_000",
                "operationType": "maneuver",
                "outcome": "success",
                "before": { "conflictPosition": "opposition_advantaged" },
                "after": { "conflictPosition": "contested" }
              }
            ]
          },
          "recentConflicts": []
        }
        """)!.AsObject();
        var update = JsonNode.Parse("""
        {
          "mode": "exchange",
          "exchange": {
            "exchangeId": "exchange_001",
            "operationType": "guard",
            "outcome": "success",
            "before": { "conflictPosition": "contested" },
            "after": { "conflictPosition": "player_advantaged" }
          },
          "activeConflictAfter": {
            "conflictId": "afterlife_conflict_test_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "conflictPosition": "player_advantaged",
            "exchangeLog": [
              {
                "exchangeId": "exchange_000",
                "operationType": "maneuver",
                "outcome": "success",
                "before": { "conflictPosition": "opposition_advantaged" },
                "after": { "conflictPosition": "contested" }
              }
            ]
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        var exchangeLog = Assert.IsType<JsonArray>(projected["activeConflict"]?["exchangeLog"]);
        Assert.Equal(2, exchangeLog.Count);
        Assert.Equal("exchange_000", exchangeLog[0]?["exchangeId"]?.GetValue<string>());
        Assert.Equal("exchange_001", exchangeLog[1]?["exchangeId"]?.GetValue<string>());
    }

    [Fact]
    public void ApplyUpdate_ExchangeWithReplacementDuplicateId_PrefersExplicitExchangePayload()
    {
        var root = JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "afterlife_conflict_test_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "exchangeLog": []
          },
          "recentConflicts": []
        }
        """)!.AsObject();
        var update = JsonNode.Parse("""
        {
          "mode": "exchange",
          "exchange": {
            "exchangeId": "exchange_001",
            "operationType": "guard",
            "outcome": "success",
            "before": { "conflictPosition": "contested" },
            "after": { "conflictPosition": "player_advantaged" }
          },
          "activeConflictAfter": {
            "conflictId": "afterlife_conflict_test_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "conflictPosition": "player_advantaged",
            "exchangeLog": [
              {
                "exchangeId": "exchange_001",
                "mode": "exchange",
                "summary": "stale summary-only copy"
              }
            ]
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        var exchangeLog = Assert.IsType<JsonArray>(projected["activeConflict"]?["exchangeLog"]);
        var exchange = Assert.IsType<JsonObject>(Assert.Single(exchangeLog));
        Assert.Equal("exchange_001", exchange["exchangeId"]?.GetValue<string>());
        Assert.Equal("guard", exchange["operationType"]?.GetValue<string>());
        Assert.IsType<JsonObject>(exchange["before"]);
        Assert.IsType<JsonObject>(exchange["after"]);
        Assert.False(exchange.ContainsKey("summary"));
    }

    [Fact]
    public void ApplyUpdate_BreakBindingWithNullControlState_ClearsActiveControlState()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var active = Assert.IsType<JsonObject>(root["activeConflict"]);
        active["controlState"] = JsonNode.Parse("""
        {
          "level": "bound",
          "controllerSide": "opposition",
          "controlId": "control_opposition_bind_projection_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver", "binding" ],
          "summary": "The opponent's binding is active."
        }
        """);
        var update = JsonNode.Parse("""
        {
          "mode": "exchange",
          "exchange": {
            "exchangeId": "exchange_break_binding_null_control_001",
            "operationType": "break_binding",
            "outcome": "success",
            "before": {
              "conflictPosition": "contested",
              "controlState": {
                "level": "bound",
                "controllerSide": "opposition",
                "controlId": "control_opposition_bind_projection_001",
                "sourceOperation": "binding",
                "restrictedOperations": [ "maneuver", "binding" ],
                "summary": "The opponent's binding is active."
              }
            },
            "after": {
              "conflictPosition": "contested",
              "controlState": null
            }
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        var projectedActive = Assert.IsType<JsonObject>(projected["activeConflict"]);
        Assert.Null(projectedActive["controlState"]);
    }

    [Fact]
    public void ApplyUpdate_BreakBindingWithOmittedControlState_PreservesActiveControlState()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var active = Assert.IsType<JsonObject>(root["activeConflict"]);
        active["controlState"] = JsonNode.Parse("""
        {
          "level": "bound",
          "controllerSide": "opposition",
          "controlId": "control_opposition_bind_projection_002",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver", "binding" ],
          "summary": "The opponent's binding is active."
        }
        """);
        var update = JsonNode.Parse("""
        {
          "mode": "exchange",
          "exchange": {
            "exchangeId": "exchange_break_binding_omitted_control_001",
            "operationType": "break_binding",
            "outcome": "success",
            "before": {
              "conflictPosition": "contested",
              "controlState": {
                "level": "bound",
                "controllerSide": "opposition",
                "controlId": "control_opposition_bind_projection_002",
                "sourceOperation": "binding",
                "restrictedOperations": [ "maneuver", "binding" ],
                "summary": "The opponent's binding is active."
              }
            },
            "after": {
              "conflictPosition": "contested"
            }
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        var projectedActive = Assert.IsType<JsonObject>(projected["activeConflict"]);
        var projectedControl = Assert.IsType<JsonObject>(projectedActive["controlState"]);
        Assert.Equal("control_opposition_bind_projection_002", projectedControl["controlId"]?.GetValue<string>());
        Assert.Equal("bound", projectedControl["level"]?.GetValue<string>());
        Assert.Equal("opposition", projectedControl["controllerSide"]?.GetValue<string>());
    }

    [Fact]
    public void ApplyUpdate_IncarnationResistanceWithOmittedBindingControl_PreservesActiveControlState()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var active = Assert.IsType<JsonObject>(root["activeConflict"]);
        active["controlState"] = JsonNode.Parse("""
        {
          "level": "bound",
          "controllerSide": "opposition",
          "controlId": "control_opposition_bind_projection_incarnation_resistance_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver", "binding" ],
          "summary": "The opponent's ordinary binding is active."
        }
        """);
        var update = JsonNode.Parse("""
        {
          "mode": "exchange",
          "exchange": {
            "exchangeId": "exchange_incarnation_resistance_omits_binding_control_001",
            "operationType": "incarnation_resistance",
            "outcome": "success",
            "incomingAction": {
              "operationType": "force_incarnation",
              "actorId": "guardian_liora",
              "summary": "The Guardian tries to force a handoff while a separate binding is active."
            },
            "before": {
              "conflictPosition": "contested",
              "controlState": {
                "level": "bound",
                "controllerSide": "opposition",
                "controlId": "control_opposition_bind_projection_incarnation_resistance_001",
                "sourceOperation": "binding",
                "restrictedOperations": [ "maneuver", "binding" ],
                "summary": "The opponent's ordinary binding is active."
              }
            },
            "after": {
              "conflictPosition": "contested"
            }
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        var projectedActive = Assert.IsType<JsonObject>(projected["activeConflict"]);
        var projectedControl = Assert.IsType<JsonObject>(projectedActive["controlState"]);
        Assert.Equal("control_opposition_bind_projection_incarnation_resistance_001", projectedControl["controlId"]?.GetValue<string>());
        Assert.Equal("binding", projectedControl["sourceOperation"]?.GetValue<string>());
    }

    [Fact]
    public void ApplyUpdate_IncarnationResistanceWithOmittedForcedIncarnationControl_PreservesActiveControlState()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var active = Assert.IsType<JsonObject>(root["activeConflict"]);
        active["controlState"] = JsonNode.Parse("""
        {
          "level": "bound",
          "controllerSide": "opposition",
          "controlId": "control_force_incarnation_projection_001",
          "sourceOperation": "force_incarnation",
          "restrictedOperations": [ "withdraw", "surrender", "negotiate" ],
          "summary": "The opponent is forcing an incarnation handoff."
        }
        """);
        var update = JsonNode.Parse("""
        {
          "mode": "exchange",
          "exchange": {
            "exchangeId": "exchange_incarnation_resistance_omits_force_control_001",
            "operationType": "incarnation_resistance",
            "outcome": "success",
            "incomingAction": {
              "operationType": "force_incarnation",
              "actorId": "guardian_liora",
              "summary": "The Guardian tries to force a handoff."
            },
            "before": {
              "conflictPosition": "contested",
              "controlState": {
                "level": "bound",
                "controllerSide": "opposition",
                "controlId": "control_force_incarnation_projection_001",
                "sourceOperation": "force_incarnation",
                "restrictedOperations": [ "withdraw", "surrender", "negotiate" ],
                "summary": "The opponent is forcing an incarnation handoff."
              }
            },
            "after": {
              "conflictPosition": "contested"
            }
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        var projectedActive = Assert.IsType<JsonObject>(projected["activeConflict"]);
        var projectedControl = Assert.IsType<JsonObject>(projectedActive["controlState"]);
        Assert.Equal("control_force_incarnation_projection_001", projectedControl["controlId"]?.GetValue<string>());
        Assert.Equal("bound", projectedControl["level"]?.GetValue<string>());
        Assert.Equal("opposition", projectedControl["controllerSide"]?.GetValue<string>());
    }

    [Fact]
    public void ApplyUpdate_BreakBindingWithOmittedStaleControlState_PreservesActiveControlState()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var active = Assert.IsType<JsonObject>(root["activeConflict"]);
        active["controlState"] = JsonNode.Parse("""
        {
          "level": "bound",
          "controllerSide": "opposition",
          "controlId": "control_current_opposition_bind_projection_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver", "binding" ],
          "summary": "The current opponent binding is active."
        }
        """);
        var update = JsonNode.Parse("""
        {
          "mode": "exchange",
          "exchange": {
            "exchangeId": "exchange_break_binding_stale_omitted_control_001",
            "operationType": "break_binding",
            "outcome": "success",
            "before": {
              "conflictPosition": "contested",
              "controlState": {
                "level": "bound",
                "controllerSide": "opposition",
                "controlId": "control_stale_opposition_bind_projection_001",
                "sourceOperation": "binding",
                "restrictedOperations": [ "maneuver", "binding" ],
                "summary": "A stale opponent binding snapshot is active."
              }
            },
            "after": {
              "conflictPosition": "contested"
            }
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        var projectedActive = Assert.IsType<JsonObject>(projected["activeConflict"]);
        var projectedControl = Assert.IsType<JsonObject>(projectedActive["controlState"]);
        Assert.Equal("control_current_opposition_bind_projection_001", projectedControl["controlId"]?.GetValue<string>());
    }

    [Fact]
    public void ApplyUpdate_BreakBindingWithOmittedSameIdDifferentLevel_PreservesActiveControlState()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var active = Assert.IsType<JsonObject>(root["activeConflict"]);
        active["controlState"] = JsonNode.Parse("""
        {
          "level": "locked",
          "controllerSide": "opposition",
          "controlId": "control_reused_opposition_bind_projection_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver", "binding", "withdraw" ],
          "summary": "The current opponent binding strengthened after the stale snapshot."
        }
        """);
        var update = JsonNode.Parse("""
        {
          "mode": "exchange",
          "exchange": {
            "exchangeId": "exchange_break_binding_stale_same_id_level_001",
            "operationType": "break_binding",
            "outcome": "success",
            "before": {
              "conflictPosition": "contested",
              "controlState": {
                "level": "bound",
                "controllerSide": "opposition",
                "controlId": "control_reused_opposition_bind_projection_001",
                "sourceOperation": "binding",
                "restrictedOperations": [ "maneuver", "binding" ],
                "summary": "A stale lower-level binding snapshot."
              }
            },
            "after": {
              "conflictPosition": "contested"
            }
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        var projectedActive = Assert.IsType<JsonObject>(projected["activeConflict"]);
        var projectedControl = Assert.IsType<JsonObject>(projectedActive["controlState"]);
        Assert.Equal("locked", projectedControl["level"]?.GetValue<string>());
        Assert.Equal("opposition", projectedControl["controllerSide"]?.GetValue<string>());
        Assert.Equal("control_reused_opposition_bind_projection_001", projectedControl["controlId"]?.GetValue<string>());
    }

    [Fact]
    public void ApplyUpdate_BreakBindingWithOmittedSameIdDifferentController_PreservesActiveControlState()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var active = Assert.IsType<JsonObject>(root["activeConflict"]);
        active["controlState"] = JsonNode.Parse("""
        {
          "level": "bound",
          "controllerSide": "player",
          "controlId": "control_reused_reversed_projection_001",
          "sourceOperation": "counter",
          "restrictedOperations": [ "binding" ],
          "summary": "The current control was already reversed to the player."
        }
        """);
        var update = JsonNode.Parse("""
        {
          "mode": "exchange",
          "exchange": {
            "exchangeId": "exchange_break_binding_stale_same_id_controller_001",
            "operationType": "break_binding",
            "outcome": "success",
            "before": {
              "conflictPosition": "contested",
              "controlState": {
                "level": "bound",
                "controllerSide": "opposition",
                "controlId": "control_reused_reversed_projection_001",
                "sourceOperation": "binding",
                "restrictedOperations": [ "maneuver", "binding" ],
                "summary": "A stale opponent binding snapshot."
              }
            },
            "after": {
              "conflictPosition": "contested"
            }
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        var projectedActive = Assert.IsType<JsonObject>(projected["activeConflict"]);
        var projectedControl = Assert.IsType<JsonObject>(projectedActive["controlState"]);
        Assert.Equal("bound", projectedControl["level"]?.GetValue<string>());
        Assert.Equal("player", projectedControl["controllerSide"]?.GetValue<string>());
        Assert.Equal("control_reused_reversed_projection_001", projectedControl["controlId"]?.GetValue<string>());
    }

    [Fact]
    public void ApplyUpdate_CounteredBreakBindingWithOmittedControlState_PreservesActiveControlState()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var active = Assert.IsType<JsonObject>(root["activeConflict"]);
        active["controlState"] = JsonNode.Parse("""
        {
          "level": "bound",
          "controllerSide": "opposition",
          "controlId": "control_opposition_bind_projection_003",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver", "binding" ],
          "summary": "The opponent's binding remains active."
        }
        """);
        var update = JsonNode.Parse("""
        {
          "mode": "exchange",
          "exchange": {
            "exchangeId": "exchange_break_binding_countered_control_001",
            "operationType": "break_binding",
            "outcome": "countered",
            "before": {
              "conflictPosition": "contested",
              "controlState": {
                "level": "bound",
                "controllerSide": "opposition",
                "controlId": "control_opposition_bind_projection_003",
                "sourceOperation": "binding",
                "restrictedOperations": [ "maneuver", "binding" ],
                "summary": "The opponent's binding remains active."
              }
            },
            "after": {
              "conflictPosition": "contested"
            }
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        var projectedActive = Assert.IsType<JsonObject>(projected["activeConflict"]);
        var projectedControl = Assert.IsType<JsonObject>(projectedActive["controlState"]);
        Assert.Equal("bound", projectedControl["level"]?.GetValue<string>());
        Assert.Equal("opposition", projectedControl["controllerSide"]?.GetValue<string>());
        Assert.Equal("control_opposition_bind_projection_003", projectedControl["controlId"]?.GetValue<string>());
    }

    [Fact]
    public void ApplyUpdate_ExchangeWithMismatchedReplacement_MarksInvalidAndPreservesActiveConflict()
    {
        var root = JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "afterlife_conflict_test_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "exchangeLog": [
              {
                "exchangeId": "exchange_000",
                "operationType": "maneuver",
                "outcome": "success",
                "before": { "conflictPosition": "opposition_advantaged" },
                "after": { "conflictPosition": "contested" }
              }
            ]
          },
          "recentConflicts": []
        }
        """)!.AsObject();
        var update = JsonNode.Parse("""
        {
          "mode": "exchange",
          "exchange": {
            "exchangeId": "exchange_001",
            "operationType": "guard",
            "outcome": "success",
            "before": { "conflictPosition": "contested" },
            "after": { "conflictPosition": "player_advantaged" }
          },
          "activeConflictAfter": {
            "conflictId": "afterlife_conflict_other_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "conflictPosition": "player_advantaged",
            "exchangeLog": []
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        Assert.Equal("exchange_conflict_id_mismatch", projected["lastInvalidUpdateReason"]?.GetValue<string>());
        Assert.Equal("afterlife_conflict_test_001", projected["activeConflict"]?["conflictId"]?.GetValue<string>());
        var exchangeLog = Assert.IsType<JsonArray>(projected["activeConflict"]?["exchangeLog"]);
        Assert.Single(exchangeLog);
        Assert.Equal("exchange_000", exchangeLog[0]?["exchangeId"]?.GetValue<string>());
        var recentConflicts = Assert.IsType<JsonArray>(projected["recentConflicts"]);
        Assert.Empty(recentConflicts);
    }

    [Theory]
    [InlineData("conflictId")]
    [InlineData("id")]
    public void ApplyUpdate_ExchangeWithMismatchedExchangeConflictIdentity_MarksInvalidAndPreservesActiveConflict(string identityProperty)
    {
        var root = JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "afterlife_conflict_test_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "exchangeLog": [
              {
                "exchangeId": "exchange_000",
                "operationType": "maneuver",
                "outcome": "success",
                "before": { "conflictPosition": "opposition_advantaged" },
                "after": { "conflictPosition": "contested" }
              }
            ]
          },
          "recentConflicts": []
        }
        """)!.AsObject();
        var update = JsonNode.Parse($$"""
        {
          "mode": "exchange",
          "exchange": {
            "exchangeId": "exchange_001",
            {{JsonSerializer.Serialize(identityProperty)}}: "afterlife_conflict_other_001",
            "operationType": "guard",
            "outcome": "no_effect",
            "before": { "conflictPosition": "contested" },
            "after": { "conflictPosition": "contested" }
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        Assert.Equal("exchange_conflict_id_mismatch", projected["lastInvalidUpdateReason"]?.GetValue<string>());
        Assert.Equal("afterlife_conflict_test_001", projected["activeConflict"]?["conflictId"]?.GetValue<string>());
        var exchangeLog = Assert.IsType<JsonArray>(projected["activeConflict"]?["exchangeLog"]);
        var existingExchange = Assert.IsType<JsonObject>(Assert.Single(exchangeLog));
        Assert.Equal("exchange_000", existingExchange["exchangeId"]?.GetValue<string>());
        var recentConflicts = Assert.IsType<JsonArray>(projected["recentConflicts"]);
        Assert.Empty(recentConflicts);
    }

    [Fact]
    public void ApplyUpdate_ExchangeWithReplacementMissingConflictId_PreservesCurrentConflictId()
    {
        var root = JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "afterlife_conflict_test_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "exchangeLog": []
          },
          "recentConflicts": []
        }
        """)!.AsObject();
        var update = JsonNode.Parse("""
        {
          "mode": "exchange",
          "exchange": {
            "exchangeId": "exchange_001",
            "operationType": "guard",
            "outcome": "success",
            "before": { "conflictPosition": "contested" },
            "after": { "conflictPosition": "player_advantaged" }
          },
          "activeConflictAfter": {
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "conflictPosition": "player_advantaged",
            "exchangeLog": []
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        Assert.Equal("afterlife_conflict_test_001", projected["activeConflict"]?["conflictId"]?.GetValue<string>());
        var exchangeLog = Assert.IsType<JsonArray>(projected["activeConflict"]?["exchangeLog"]);
        Assert.Single(exchangeLog);
        Assert.Equal("exchange_001", exchangeLog[0]?["exchangeId"]?.GetValue<string>());
    }

    [Fact]
    public void ApplyUpdate_ModeIsCaseInsensitive()
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var update = JsonNode.Parse("""
        {
          "mode": "EXCHANGE",
          "exchange": {
            "exchangeId": "exchange_001",
            "operationType": "guard",
            "outcome": "no_effect",
            "before": { "conflictPosition": "contested" },
            "after": { "conflictPosition": "contested" }
          }
        }
        """)!.AsObject();

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        AssertNoInvalidMarkers(projected);
        Assert.NotEqual("unsupported_mode", projected["lastInvalidUpdateReason"]?.GetValue<string>());
        var exchangeLog = Assert.IsType<JsonArray>(projected["activeConflict"]?["exchangeLog"]);
        Assert.Single(exchangeLog);
    }

    [Fact]
    public async Task ValidateGameStateAsync_MortalWorldTurnChangingSpiritualConflictState_FailsRealmSegregation()
    {
        const string preTurnSoul = """
        {
          "soulName": "Асуран",
          "currentRealm": "MortalWorldProfile"
        }
        """;
        const string preTurnConflict = """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": []
        }
        """;
        const string currentConflict = """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "conflictId": "mortal_world_illegal_repair_cancel",
              "resolutionState": "repair_cancelled"
            }
          ]
        }
        """;

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", preTurnSoul);
        await _fs.WriteFileAtomicAsync(
            AfterlifeSpiritualConflictState.StatePath,
            AddValidForcedIncarnationDiceAuditToRecentConflicts(currentConflict));
        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", preTurnSoul);
        await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.StatePath, preTurnConflict);
        await WriteValidatedSnapshotManifestAsync(
            ("game_state/meta/soul_state.json", preTurnSoul),
            (AfterlifeSpiritualConflictState.StatePath, preTurnConflict));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "realm_segregation_violation", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains(AfterlifeSpiritualConflictState.StatePath, StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_ActiveConflictInChaosSea_DoesNotReportShiningBootstrapBlock()
    {
        await WriteSoulStateAsync("Chaos Sea");
        await WriteConflictStateAsync("no_effect");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_active_during_shining_bootstrap", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ActiveConflictRealmDifferentFromSoul_FailsRealmMatch()
    {
        await WriteSoulStateAsync("Chaos Sea");
        await WriteConflictStateAsync("no_effect", "Shining Abode");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_active_realm_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ActiveConflictLocalizedRealmDifferentFromSoul_FailsRealmMatch()
    {
        await WriteSoulStateAsync("Сияющая Обитель");
        await WriteConflictStateAsync("no_effect", "Море Хаоса");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_active_realm_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ActiveConflictLocalizedEquivalentRealm_DoesNotReportRealmMismatch()
    {
        await WriteSoulStateAsync("Море Хаоса");
        await WriteConflictStateAsync("no_effect", "Chaos Sea");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_active_realm_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnChaosSeaWithSameTurnShiningRealmEdit_FailsPreTurnRealmGate()
    {
        const string preTurnSoul = """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """;
        const string preTurnConflict = """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": []
        }
        """;

        await WriteSoulStateAsync("Shining Abode");
        await WriteConflictStateAsync("no_effect", "Shining Abode");
        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", preTurnSoul);
        await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.StatePath, preTurnConflict);
        await WriteValidatedSnapshotManifestAsync(
            "обработки хода",
            "GM не должен same-turn переводить душу в Shining Abode и запускать Shining conflict.",
            ("game_state/meta/soul_state.json", preTurnSoul),
            (AfterlifeSpiritualConflictState.StatePath, preTurnConflict));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_active_realm_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnShiningWithSameTurnChaosRealmEdit_FailsPreTurnRealmGate()
    {
        const string preTurnSoul = """
        {
          "soulName": "Асуран",
          "currentRealm": "Shining Abode"
        }
        """;
        const string preTurnConflict = """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": []
        }
        """;
        const string preTurnShining = """
        {
          "availability": "active",
          "preparedIncarnationPackage": null
        }
        """;

        await WriteSoulStateAsync("Chaos Sea");
        await WriteConflictStateAsync("no_effect", "Chaos Sea");
        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", preTurnSoul);
        await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.StatePath, preTurnConflict);
        await WriteSnapshotFileAsync(ShiningAbodeState.StatePath, preTurnShining);
        await WriteValidatedSnapshotManifestAsync(
            "обработки хода",
            "GM не должен same-turn переводить душу в Chaos Sea и запускать Chaos conflict из Shining authority.",
            ("game_state/meta/soul_state.json", preTurnSoul),
            (AfterlifeSpiritualConflictState.StatePath, preTurnConflict),
            (ShiningAbodeState.StatePath, preTurnShining));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_active_realm_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnSealedShiningWithSameTurnActiveEdit_FailsSnapshotAvailabilityGate()
    {
        const string preTurnSoul = """
        {
          "soulName": "Асуран",
          "currentRealm": "Shining Abode"
        }
        """;
        const string preTurnConflict = """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": []
        }
        """;
        const string preTurnShining = """
        {
          "availability": "sealed_until_next_ascension",
          "preparedIncarnationPackage": null
        }
        """;

        await WriteSoulStateAsync("Shining Abode");
        await WriteConflictStateAsync("no_effect", "Shining Abode");
        await WriteShiningAvailabilityAsync(ShiningAbodeState.AvailabilityActive);
        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", preTurnSoul);
        await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.StatePath, preTurnConflict);
        await WriteSnapshotFileAsync(ShiningAbodeState.StatePath, preTurnShining);
        await WriteValidatedSnapshotManifestAsync(
            "обработки хода",
            "GM не должен same-turn раскрывать sealed Shining и запускать conflict.",
            ("game_state/meta/soul_state.json", preTurnSoul),
            (AfterlifeSpiritualConflictState.StatePath, preTurnConflict),
            (ShiningAbodeState.StatePath, preTurnShining));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_active_during_sealed_shining_abode", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnActiveShiningIgnoresSameTurnCurrentRealmRewrite()
    {
        const string preTurnSoul = """
        {
          "soulName": "Асуран",
          "currentRealm": "Shining Abode"
        }
        """;
        const string preTurnConflict = """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": []
        }
        """;
        const string preTurnShining = """
        {
          "availability": "active",
          "preparedIncarnationPackage": null
        }
        """;

        await WriteSoulStateAsync("Chaos Sea");
        await WriteConflictStateAsync("no_effect", "Shining Abode");
        await WriteShiningAvailabilityAsync(ShiningAbodeState.AvailabilitySealedUntilNextAscension);
        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", preTurnSoul);
        await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.StatePath, preTurnConflict);
        await WriteSnapshotFileAsync(ShiningAbodeState.StatePath, preTurnShining);
        await WriteValidatedSnapshotManifestAsync(
            "обработки хода",
            "Pre-turn ordinary Shining остается authority даже если current realm/state переписан.",
            ("game_state/meta/soul_state.json", preTurnSoul),
            (AfterlifeSpiritualConflictState.StatePath, preTurnConflict),
            (ShiningAbodeState.StatePath, preTurnShining));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_active_realm_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_active_during_sealed_shining_abode", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_active_during_shining_bootstrap", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ActiveConflictInShiningPendingBootstrap_FailsModeGate()
    {
        await WriteSoulStateAsync("Shining Abode");
        await WriteConflictStateAsync("no_effect");
        await WriteValidPreparedIncarnationPackageAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_active_during_shining_bootstrap", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ActiveConflictInSealedShiningAbode_FailsAvailabilityGate()
    {
        await WriteSoulStateAsync("Shining Abode");
        await WriteConflictStateAsync("no_effect", "Shining Abode");
        await WriteShiningAvailabilityAsync(ShiningAbodeState.AvailabilitySealedUntilNextAscension);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_active_during_sealed_shining_abode", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ActiveConflictInActiveShiningAbode_DoesNotReportAvailabilityOrBootstrapBlock()
    {
        await WriteSoulStateAsync("Shining Abode");
        await WriteConflictStateAsync("no_effect", "Shining Abode");
        await WriteShiningAvailabilityAsync(ShiningAbodeState.AvailabilityActive);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_active_during_sealed_shining_abode", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_active_during_shining_bootstrap", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ActiveConflictInShiningPackageFault_FailsModeGate()
    {
        await WriteSoulStateAsync("Shining Abode");
        await WriteConflictStateAsync("no_effect");
        await _fs.WriteFileAtomicAsync("game_state/meta/shining_abode_state.json", """
        {
          "preparedIncarnationPackage": "broken"
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_active_during_shining_bootstrap", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("resolved")]
    [InlineData("repair_cancelled")]
    public async Task ValidateGameStateAsync_TerminalActiveConflictResolutionState_FailsLifecycleValidation(string terminalState)
    {
        await WriteSoulStateAsync();
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        root.Remove("lastInvalidUpdate");
        root.Remove("lastInvalidUpdateReason");
        root.Remove("lastInvalidUpdateAtUtc");
        var active = (JsonObject)root["activeConflict"]!;
        active["resolutionState"] = terminalState;
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_terminal_active_conflict", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ReadyToResolveActiveConflict_DoesNotFailTerminalLifecycleValidation()
    {
        await WriteSoulStateAsync();
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        root.Remove("lastInvalidUpdate");
        root.Remove("lastInvalidUpdateReason");
        root.Remove("lastInvalidUpdateAtUtc");
        var active = (JsonObject)root["activeConflict"]!;
        active["resolutionState"] = "ready_to_resolve";
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_terminal_active_conflict", StringComparison.OrdinalIgnoreCase));
    }

    private static JsonObject BuildRootWithActiveConflictAndInvalidMarkers()
    {
        return JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "afterlife_conflict_test_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "playerSide": {
              "leadContestant": {
                "actorType": "player",
                "actorId": "player_soul",
                "displayName": "Асуран"
              },
              "supporters": []
            },
            "oppositionSide": {
              "leadContestant": {
                "actorType": "guardian",
                "actorId": "guardian_liora",
                "displayName": "Лиора",
                "actorArtTierSnapshot": {
                  "pressure": 2
                },
                "artAuthoritySource": "guardian_state"
              },
              "supporters": []
            },
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested",
            "actionEconomy": {
              "player": { "current": 6, "max": 6, "source": "Средоточие Души tier 0" },
              "opposition": { "current": 6, "max": 6, "source": "opposition spiritual authority" }
            },
            "resolutionState": "active",
            "exchangeLog": []
          },
          "recentConflicts": [],
          "lastInvalidUpdate": { "mode": "exchange", "summary": "broken" },
          "lastInvalidUpdateReason": "exchange_missing_exchange_object",
          "lastInvalidUpdateAtUtc": "2026-05-06T00:00:00Z"
        }
        """)!.AsObject();
    }

    private static JsonObject BuildRootWithOppositionLead(string actorType, string actorId, string displayName)
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        var active = (JsonObject)root["activeConflict"]!;
        var oppositionSide = (JsonObject)active["oppositionSide"]!;
        var lead = (JsonObject)oppositionSide["leadContestant"]!;
        lead["actorType"] = actorType;
        lead["actorId"] = actorId;
        lead["displayName"] = displayName;
        return root;
    }

    private static void AssertNoInvalidMarkers(JsonObject root)
    {
        Assert.False(root.ContainsKey("lastInvalidUpdate"));
        Assert.False(root.ContainsKey("lastInvalidUpdateReason"));
        Assert.False(root.ContainsKey("lastInvalidUpdateAtUtc"));
    }

    [Fact]
    public async Task ValidateGameStateAsync_FirstConflictStartWithDefaultBaseline_DoesNotReportMissingTrackedBaseline()
    {
        const string soul = """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """;
        const string preTurnConflict = """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": []
        }
        """;

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soul);
        await WriteConflictStateAsync("no_effect");
        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", soul);
        await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.StatePath, preTurnConflict);
        await WriteValidatedSnapshotManifestAsync(
            "обработки хода",
            "Лиора начинает духовный конфликт, я сопротивляюсь.",
            ("game_state/meta/soul_state.json", soul),
            (AfterlifeSpiritualConflictState.StatePath, preTurnConflict));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "realm_segregation_missing_validated_tracked_baseline", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.FilePath, AfterlifeSpiritualConflictState.StatePath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnActiveConflictMissingCurrentFile_FailsTerminalProofValidation()
    {
        await WriteSoulStateAsync();
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_active_removed_without_terminal_proof", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnActiveConflictClearedWithoutRecentProof_FailsTerminalProofValidation()
    {
        await WriteSoulStateAsync();
        await WritePreTurnActiveConflictSnapshotAsync();
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": []
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_active_removed_without_terminal_proof", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnActiveConflictReplacedWithoutRecentProof_FailsTerminalProofValidation()
    {
        await WriteSoulStateAsync();
        await WritePreTurnActiveConflictSnapshotAsync();
        await _fs.WriteFileAtomicAsync(
            AfterlifeSpiritualConflictState.StatePath,
            BuildActiveConflictRootJson("afterlife_conflict_other_001"));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_active_removed_without_terminal_proof", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnActiveConflictClearedWithResolveProof_DoesNotFailTerminalProofValidation()
    {
        await WriteSoulStateAsync();
        await WritePreTurnActiveConflictSnapshotAsync();
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "mode": "resolve",
              "conflictId": "afterlife_conflict_test_001",
              "resolutionState": "resolved",
              "resolvedAtTurn": 7,
              "operationType": "negotiate",
              "playerOutcome": "conceded"
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_active_removed_without_terminal_proof", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnActiveConflictClearedWithRepairCancelProof_DoesNotFailTerminalProofValidation()
    {
        await WriteSoulStateAsync();
        await WritePreTurnActiveConflictSnapshotAsync();
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "mode": "repair_cancel",
              "conflictId": "afterlife_conflict_test_001",
              "resolutionState": "repair_cancelled"
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_active_removed_without_terminal_proof", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnActiveConflictClearedWithSkeletalResolveProof_FailsTerminalProofValidation()
    {
        await WriteSoulStateAsync();
        await WritePreTurnActiveConflictSnapshotAsync();
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "mode": "resolve",
              "conflictId": "afterlife_conflict_test_001"
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_active_removed_without_terminal_proof", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnActiveConflictClearedWithSkeletalRepairCancelProof_FailsTerminalProofValidation()
    {
        await WriteSoulStateAsync();
        await WritePreTurnActiveConflictSnapshotAsync();
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "mode": "repair_cancel",
              "conflictId": "afterlife_conflict_test_001"
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_active_removed_without_terminal_proof", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnActiveConflictClearedWithDifferentConflictProof_FailsTerminalProofValidation()
    {
        await WriteSoulStateAsync();
        await WritePreTurnActiveConflictSnapshotAsync();
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "mode": "resolve",
              "conflictId": "afterlife_conflict_other_001",
              "resolutionState": "resolved"
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_active_removed_without_terminal_proof", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_IncarnationTriggerWithActiveConflict_FailsHandoffBlocker()
    {
        const string soul = """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """;
        const string preTurnConflict = """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": []
        }
        """;

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soul);
        await WriteConflictStateAsync("no_effect");
        await _fs.WriteFileAtomicAsync("game_state/control/incarnation_trigger.json", """
        {
          "worldDescription": "Новый мир после незавершенного конфликта.",
          "characterDescription": "Душа просыпается в новом теле.",
          "circumstances": "Невалидная попытка воплощения до закрытия духовного конфликта."
        }
        """);
        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", soul);
        await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.StatePath, preTurnConflict);
        await WriteValidatedSnapshotManifestAsync(
            "обработки хода",
            "Я пытаюсь уйти в новую жизнь, пока конфликт еще активен.",
            ("game_state/meta/soul_state.json", soul),
            (AfterlifeSpiritualConflictState.StatePath, preTurnConflict));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "incarnation_trigger_active_spiritual_conflict", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ForcedIncarnation_AcceptsResolvedSpiritualConflictProofWithoutProvocationTag()
    {
        const string soul = """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """;
        const string guardians = """
        {
          "guardians": [
            {
              "guardianId": "guardian_liora",
              "canonicalName": "Лиора",
              "nameVariants": { "default": "Лиора", "feminine": "Лиора", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Лиора",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Хранительница, выигравшая духовный конфликт."
              },
              "manifestationHistory": [],
              "abode": { "abodeId": "abode_liora", "title": "Обитель Лиоры" },
              "relationshipData": { "currentReputation": -30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-05-06T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_liora",
            "canonicalName": "Лиора",
            "nameVariants": { "default": "Лиора", "feminine": "Лиора", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Лиора",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Хранительница, выигравшая духовный конфликт."
            },
            "manifestationHistory": [],
            "abode": { "abodeId": "abode_liora", "title": "Обитель Лиоры" },
            "relationshipData": { "currentReputation": -30, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-05-06T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_liora"
          }
        }
        """;
        const string preTurnConflict = """
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "playerSide": {
              "leadContestant": { "actorType": "player", "actorId": "player_soul", "displayName": "Асуран" },
              "supporters": []
            },
            "oppositionSide": {
              "leadContestant": {
                "actorType": "guardian",
                "actorId": "guardian_liora",
                "displayName": "Лиора",
                "actorArtTierSnapshot": { "pressure": 3, "force_incarnation": 2 },
                "artAuthoritySource": "guardian_state"
              },
              "supporters": []
            },
            "playerSideStrain": "fractured",
            "oppositionSideStrain": "clear",
            "conflictPosition": "opposition_dominant",
            "resolutionState": "ready_to_resolve",
            "exchangeLog": []
          },
          "recentConflicts": []
        }
        """;
        const string currentConflict = """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "mode": "resolve",
              "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
              "resolutionState": "resolved",
              "resolvedAtTurn": 7,
              "resolvedActorId": "guardian_liora",
              "operationType": "force_incarnation",
              "playerOutcome": "lost",
              "resolutionKind": "player_loss",
              "summary": "Лиора выиграла духовный конфликт о навязанном воплощении."
            }
          ]
        }
        """;

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soul);
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", guardians);
        await _fs.WriteFileAtomicAsync(
            AfterlifeSpiritualConflictState.StatePath,
            AddValidForcedIncarnationDiceAuditToRecentConflicts(currentConflict));
        await _fs.WriteFileAtomicAsync("game_state/control/incarnation_trigger.json", """
        {
          "worldDescription": "Новый мир как принудительное следствие поражения.",
          "characterDescription": "Душа просыпается в новом теле.",
          "circumstances": "Принудительное воплощение после проигранного духовного конфликта.",
          "source": "guardian_forced",
          "guardianId": "guardian_liora",
          "severityBand": "harsh",
          "reason": "Поражение в afterlife spiritual conflict",
          "provocationSummary": "Игрок не давал согласия, но проиграл конфликт о forced incarnation."
        }
        """);

        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", soul);
        await WriteSnapshotFileAsync("game_state/meta/guardians.json", guardians);
        await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.StatePath, preTurnConflict);
        await WriteValidatedSnapshotManifestAsync(
            "обработки хода",
            "Я сопротивляюсь Лиоре, но не использую legacy GUARDIAN_PROVOCATION tag.",
            ("game_state/meta/soul_state.json", soul),
            ("game_state/meta/guardians.json", guardians),
            (AfterlifeSpiritualConflictState.StatePath, preTurnConflict));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "forced_incarnation_missing_player_action_provocation_evidence", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "incarnation_trigger_active_spiritual_conflict", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ForcedIncarnation_AcceptsNestedGuardianProofWhenTopLevelRefsMissing()
    {
        const string currentConflict = """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "mode": "resolve",
              "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
              "resolutionState": "resolved",
              "resolvedAtTurn": 7,
              "operationType": "force_incarnation",
              "playerOutcome": "lost",
              "oppositionSide": {
                "leadContestant": {
                  "actorType": "guardian",
                  "actorId": "guardian_liora",
                  "displayName": "Лиора"
                },
                "supporters": []
              }
            }
          ]
        }
        """;

        var issues = await ValidateForcedIncarnationProofScenarioAsync(currentConflict);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "forced_incarnation_missing_player_action_provocation_evidence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ForcedIncarnation_RequiresDiceBackedConflictProof()
    {
        const string currentConflict = """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "mode": "resolve",
              "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
              "resolutionState": "resolved",
              "resolvedAtTurn": 7,
              "guardianId": "guardian_liora",
              "operationType": "force_incarnation",
              "playerOutcome": "lost"
            }
          ]
        }
        """;

        var issues = await ValidateForcedIncarnationProofScenarioAsync(
            currentConflict,
            addForcedIncarnationDiceAudit: false);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_resolution_missing_dice_audit", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "forced_incarnation_missing_player_action_provocation_evidence", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("resolvedActorId")]
    [InlineData("actorId")]
    public async Task ValidateGameStateAsync_ForcedIncarnation_AcceptsGuardianProofWithExtraGenericActorMetadata(string genericActorField)
    {
        var currentConflict = $$"""
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "mode": "resolve",
              "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
              "resolutionState": "resolved",
              "resolvedAtTurn": 7,
              "guardianId": "guardian_liora",
              {{JsonSerializer.Serialize(genericActorField)}}: "player_soul",
              "operationType": "force_incarnation",
              "playerOutcome": "lost"
            }
          ]
        }
        """;

        var issues = await ValidateForcedIncarnationProofScenarioAsync(currentConflict);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "forced_incarnation_missing_player_action_provocation_evidence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ForcedIncarnation_RejectsGenericActorProofWithoutGuardianReference()
    {
        const string currentConflict = """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "mode": "resolve",
              "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
              "resolutionState": "resolved",
              "resolvedAtTurn": 7,
              "actorId": "player_soul",
              "operationType": "force_incarnation",
              "playerOutcome": "lost"
            }
          ]
        }
        """;

        var issues = await ValidateForcedIncarnationProofScenarioAsync(currentConflict);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "forced_incarnation_missing_player_action_provocation_evidence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ForcedIncarnation_UsesPreTurnGuardianReputationForConflictProof()
    {
        const string currentConflict = """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "mode": "resolve",
              "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
              "resolutionState": "resolved",
              "resolvedAtTurn": 7,
              "guardianId": "guardian_liora",
              "operationType": "force_incarnation",
              "playerOutcome": "lost"
            }
          ]
        }
        """;

        var issues = await ValidateForcedIncarnationProofScenarioAsync(
            currentConflict,
            currentGuardiansOverride: BuildForcedIncarnationGuardiansJson(currentReputation: -30),
            preTurnGuardiansOverride: BuildForcedIncarnationGuardiansJson(currentReputation: -5));

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "forced_incarnation_missing_player_action_provocation_evidence", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "forced_incarnation_reputation_too_high", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ForcedIncarnation_RequiresPreTurnActiveGuardianForConflictProof()
    {
        const string currentConflict = """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "mode": "resolve",
              "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
              "resolutionState": "resolved",
              "resolvedAtTurn": 7,
              "guardianId": "guardian_liora",
              "operationType": "force_incarnation",
              "playerOutcome": "lost"
            }
          ]
        }
        """;

        var issues = await ValidateForcedIncarnationProofScenarioAsync(
            currentConflict,
            currentGuardiansOverride: BuildForcedIncarnationGuardiansJson(currentReputation: -30),
            preTurnGuardiansOverride: BuildForcedIncarnationGuardiansJson(
                guardianId: "guardian_other",
                canonicalName: "Иной Хранитель",
                abodeId: "abode_other",
                abodeTitle: "Чужая Обитель",
                currentReputation: -80));

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "forced_incarnation_missing_player_action_provocation_evidence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ForcedIncarnation_RejectsContradictoryTopLevelGuardianProofDespiteNestedMatch()
    {
        const string currentConflict = """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "mode": "resolve",
              "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
              "resolutionState": "resolved",
              "resolvedAtTurn": 7,
              "guardianId": "guardian_other",
              "operationType": "force_incarnation",
              "playerOutcome": "lost",
              "oppositionSide": {
                "leadContestant": {
                  "actorType": "guardian",
                  "actorId": "guardian_liora",
                  "displayName": "Лиора"
                },
                "supporters": []
              }
            }
          ]
        }
        """;

        var issues = await ValidateForcedIncarnationProofScenarioAsync(currentConflict);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "forced_incarnation_missing_player_action_provocation_evidence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ForcedIncarnation_RejectsGuardianForcedSourceWithoutForceIncarnationOperation()
    {
        const string soul = """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """;
        const string preTurnConflict = """
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "afterlife_conflict_liora_pressure_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "oppositionSide": {
              "leadContestant": {
                "actorType": "guardian",
                "actorId": "guardian_liora",
                "displayName": "Лиора"
              },
              "supporters": []
            },
            "exchangeLog": []
          },
          "recentConflicts": []
        }
        """;
        const string currentConflict = """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "mode": "resolve",
              "conflictId": "afterlife_conflict_liora_pressure_001",
              "resolutionState": "resolved",
              "resolvedAtTurn": 7,
              "guardianId": "guardian_liora",
              "operationType": "pressure",
              "source": "guardian_forced",
              "reason": "guardian_forced",
              "consequence": "guardian_forced",
              "playerOutcome": "lost"
            }
          ]
        }
        """;

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soul);
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, currentConflict);
        await _fs.WriteFileAtomicAsync("game_state/control/incarnation_trigger.json", """
        {
          "worldDescription": "Новый мир как принудительное следствие давления.",
          "characterDescription": "Душа просыпается в новом теле.",
          "circumstances": "Принудительное воплощение после pressure proof.",
          "source": "guardian_forced",
          "guardianId": "guardian_liora",
          "severityBand": "harsh",
          "reason": "Pressure proof не должен авторизовать lifecycle trigger",
          "provocationSummary": "Нет operationType=force_incarnation."
        }
        """);
        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", soul);
        await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.StatePath, preTurnConflict);
        await WriteValidatedSnapshotManifestAsync(
            "обработки хода",
            "Обычное сопротивление без legacy provocation tag.",
            ("game_state/meta/soul_state.json", soul),
            (AfterlifeSpiritualConflictState.StatePath, preTurnConflict));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "forced_incarnation_missing_player_action_provocation_evidence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ForcedIncarnation_RejectsUnboundConflictIdProofWithoutProvocationTag()
    {
        const string soul = """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """;
        const string preTurnConflict = """
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "oppositionSide": {
              "leadContestant": {
                "actorType": "guardian",
                "actorId": "guardian_liora",
                "displayName": "Лиора"
              },
              "supporters": []
            },
            "exchangeLog": []
          },
          "recentConflicts": []
        }
        """;
        const string currentConflict = """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "mode": "resolve",
              "conflictId": "afterlife_conflict_fabricated_001",
              "resolutionState": "resolved",
              "resolvedAtTurn": 7,
              "guardianId": "guardian_liora",
              "operationType": "force_incarnation",
              "playerOutcome": "lost"
            }
          ]
        }
        """;

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soul);
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, currentConflict);
        await _fs.WriteFileAtomicAsync("game_state/control/incarnation_trigger.json", """
        {
          "worldDescription": "Новый мир как принудительное следствие поражения.",
          "characterDescription": "Душа просыпается в новом теле.",
          "circumstances": "Принудительное воплощение после неподтвержденного конфликта.",
          "source": "guardian_forced",
          "guardianId": "guardian_liora",
          "severityBand": "harsh",
          "reason": "Непривязанный proof",
          "provocationSummary": "Нет current-turn proof для pre-turn activeConflict."
        }
        """);
        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", soul);
        await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.StatePath, preTurnConflict);
        await WriteValidatedSnapshotManifestAsync(
            "обработки хода",
            "Обычное сопротивление без legacy provocation tag.",
            ("game_state/meta/soul_state.json", soul),
            (AfterlifeSpiritualConflictState.StatePath, preTurnConflict));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "forced_incarnation_missing_player_action_provocation_evidence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ForcedIncarnation_RejectsWrongGuardianConflictProofWithoutProvocationTag()
    {
        const string soul = """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """;
        const string preTurnConflict = """
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "oppositionSide": {
              "leadContestant": {
                "actorType": "guardian",
                "actorId": "guardian_liora",
                "displayName": "Лиора"
              },
              "supporters": []
            },
            "exchangeLog": []
          },
          "recentConflicts": []
        }
        """;
        const string currentConflict = """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "mode": "resolve",
              "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
              "resolutionState": "resolved",
              "resolvedAtTurn": 7,
              "guardianId": "guardian_other",
              "operationType": "force_incarnation",
              "playerOutcome": "lost"
            }
          ]
        }
        """;

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soul);
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, currentConflict);
        await _fs.WriteFileAtomicAsync("game_state/control/incarnation_trigger.json", """
        {
          "worldDescription": "Новый мир как принудительное следствие поражения.",
          "characterDescription": "Душа просыпается в новом теле.",
          "circumstances": "Принудительное воплощение после чужого proof.",
          "source": "guardian_forced",
          "guardianId": "guardian_other",
          "severityBand": "harsh",
          "reason": "Proof другого Хранителя",
          "provocationSummary": "Нет current-turn proof от pre-turn opposition guardian."
        }
        """);
        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", soul);
        await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.StatePath, preTurnConflict);
        await WriteValidatedSnapshotManifestAsync(
            "обработки хода",
            "Обычное сопротивление без legacy provocation tag.",
            ("game_state/meta/soul_state.json", soul),
            (AfterlifeSpiritualConflictState.StatePath, preTurnConflict));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "forced_incarnation_missing_player_action_provocation_evidence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ForcedIncarnation_RejectsStaleSpiritualConflictProofWithoutProvocationTag()
    {
        const string soul = """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """;
        const string conflict = """
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "mode": "resolve",
              "conflictId": "afterlife_conflict_stale_001",
              "resolutionState": "resolved",
              "resolvedAtTurn": 6,
              "guardianId": "guardian_liora",
              "operationType": "force_incarnation",
              "playerOutcome": "lost"
            }
          ]
        }
        """;

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soul);
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, conflict);
        await _fs.WriteFileAtomicAsync("game_state/control/incarnation_trigger.json", """
        {
          "worldDescription": "Новый мир как принудительное следствие поражения.",
          "characterDescription": "Душа просыпается в новом теле.",
          "circumstances": "Принудительное воплощение после старого конфликта.",
          "source": "guardian_forced",
          "guardianId": "guardian_liora",
          "severityBand": "harsh",
          "reason": "Старый конфликт",
          "provocationSummary": "Нет current-turn proof."
        }
        """);
        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", soul);
        await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.StatePath, conflict);
        await WriteValidatedSnapshotManifestAsync(
            "обработки хода",
            "Обычное сопротивление без legacy provocation tag.",
            ("game_state/meta/soul_state.json", soul),
            (AfterlifeSpiritualConflictState.StatePath, conflict));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "forced_incarnation_missing_player_action_provocation_evidence", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<List<ValidationIssue>> ValidateForcedIncarnationProofScenarioAsync(
        string currentConflict,
        string? currentGuardiansOverride = null,
        string? preTurnGuardiansOverride = null,
        bool addForcedIncarnationDiceAudit = true)
    {
        const string soul = """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """;
        const string guardians = """
        {
          "guardians": [
            {
              "guardianId": "guardian_liora",
              "canonicalName": "Лиора",
              "nameVariants": { "default": "Лиора", "feminine": "Лиора", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Лиора",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Хранительница, выигравшая духовный конфликт."
              },
              "manifestationHistory": [],
              "abode": { "abodeId": "abode_liora", "title": "Обитель Лиоры" },
              "relationshipData": { "currentReputation": -30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-05-06T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_liora",
            "canonicalName": "Лиора",
            "nameVariants": { "default": "Лиора", "feminine": "Лиора", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Лиора",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Хранительница, выигравшая духовный конфликт."
            },
            "manifestationHistory": [],
            "abode": { "abodeId": "abode_liora", "title": "Обитель Лиоры" },
            "relationshipData": { "currentReputation": -30, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-05-06T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_liora"
          }
        }
        """;
        var currentGuardians = currentGuardiansOverride ?? guardians;
        var preTurnGuardians = preTurnGuardiansOverride ?? guardians;
        const string preTurnConflict = """
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "playerSide": {
              "leadContestant": { "actorType": "player", "actorId": "player_soul", "displayName": "Асуран" },
              "supporters": []
            },
            "oppositionSide": {
              "leadContestant": {
                "actorType": "guardian",
                "actorId": "guardian_liora",
                "displayName": "Лиора",
                "actorArtTierSnapshot": { "pressure": 3, "force_incarnation": 2 },
                "artAuthoritySource": "guardian_state"
              },
              "supporters": []
            },
            "playerSideStrain": "fractured",
            "oppositionSideStrain": "clear",
            "conflictPosition": "opposition_dominant",
            "resolutionState": "ready_to_resolve",
            "exchangeLog": []
          },
          "recentConflicts": []
        }
        """;

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soul);
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", currentGuardians);
        if (addForcedIncarnationDiceAudit)
            currentConflict = AddValidForcedIncarnationDiceAuditToRecentConflicts(currentConflict);

        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, currentConflict);
        await _fs.WriteFileAtomicAsync("game_state/control/incarnation_trigger.json", """
        {
          "worldDescription": "Новый мир как принудительное следствие поражения.",
          "characterDescription": "Душа просыпается в новом теле.",
          "circumstances": "Принудительное воплощение после проигранного духовного конфликта.",
          "source": "guardian_forced",
          "guardianId": "guardian_liora",
          "severityBand": "harsh",
          "reason": "Поражение в afterlife spiritual conflict",
          "provocationSummary": "Игрок не давал согласия, но проиграл конфликт о forced incarnation."
        }
        """);

        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", soul);
        await WriteSnapshotFileAsync("game_state/meta/guardians.json", preTurnGuardians);
        await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.StatePath, preTurnConflict);
        await WriteValidatedSnapshotManifestAsync(
            "обработки хода",
            "Я сопротивляюсь Лиоре, но не использую legacy GUARDIAN_PROVOCATION tag.",
            ("game_state/meta/soul_state.json", soul),
            ("game_state/meta/guardians.json", preTurnGuardians),
            (AfterlifeSpiritualConflictState.StatePath, preTurnConflict));

        return await _validator.ValidateGameStateAsync();
    }

    private static string BuildForcedIncarnationGuardiansJson(
        string guardianId = "guardian_liora",
        string canonicalName = "Лиора",
        string abodeId = "abode_liora",
        string abodeTitle = "Обитель Лиоры",
        int currentReputation = -30)
    {
        return $$"""
        {
          "guardians": [
            {
              "guardianId": {{JsonSerializer.Serialize(guardianId)}},
              "canonicalName": {{JsonSerializer.Serialize(canonicalName)}},
              "nameVariants": { "default": {{JsonSerializer.Serialize(canonicalName)}}, "feminine": {{JsonSerializer.Serialize(canonicalName)}}, "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": {{JsonSerializer.Serialize(canonicalName)}},
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Хранитель в проверке forced incarnation."
              },
              "manifestationHistory": [],
              "abode": { "abodeId": {{JsonSerializer.Serialize(abodeId)}}, "title": {{JsonSerializer.Serialize(abodeTitle)}} },
              "relationshipData": { "currentReputation": {{currentReputation}}, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-05-06T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": {{JsonSerializer.Serialize(guardianId)}},
            "canonicalName": {{JsonSerializer.Serialize(canonicalName)}},
            "nameVariants": { "default": {{JsonSerializer.Serialize(canonicalName)}}, "feminine": {{JsonSerializer.Serialize(canonicalName)}}, "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": {{JsonSerializer.Serialize(canonicalName)}},
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Хранитель в проверке forced incarnation."
            },
            "manifestationHistory": [],
            "abode": { "abodeId": {{JsonSerializer.Serialize(abodeId)}}, "title": {{JsonSerializer.Serialize(abodeTitle)}} },
            "relationshipData": { "currentReputation": {{currentReputation}}, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-05-06T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          },
          "chaosSeaNavigation": {
            "currentAbodeId": {{JsonSerializer.Serialize(abodeId)}}
          }
        }
        """;
    }

    private static string AddValidForcedIncarnationDiceAuditToRecentConflicts(string conflictJson)
    {
        var root = JsonNode.Parse(conflictJson)!.AsObject();
        if (root["recentConflicts"] is not JsonArray recentConflicts)
            return conflictJson;

        foreach (var proof in recentConflicts.OfType<JsonObject>())
        {
            var operationType = proof["operationType"]?.GetValue<string>();
            if (!string.Equals(operationType, "force_incarnation", StringComparison.OrdinalIgnoreCase) ||
                proof["diceAudit"] is JsonObject)
            {
                continue;
            }

            proof["diceAudit"] = BuildValidForcedIncarnationDiceAudit();
        }

        return root.ToJsonString();
    }

    private static JsonObject BuildValidForcedIncarnationDiceAudit() => JsonNode.Parse("""
    {
      "formulaVersion": "afterlife_spiritual_conflict_v1",
      "diceSource": "input/turn_request.json.preGeneratedDices1d20",
      "diceUsed": [
        {
          "side": "player",
          "sourceIndex": 0,
          "sides": 20,
          "value": 5
        },
        {
          "side": "opposition",
          "sourceIndex": 1,
          "sides": 20,
          "value": 18
        }
      ],
      "playerTotal": 9,
      "oppositionTotal": 23,
      "margin": -14,
      "outcomeBand": "decisive_opposition_success",
      "modifierBreakdown": {
        "player": [
          {
            "source": "incarnation_resistance art tier",
            "value": 2
          },
          {
            "source": "current Enlightenment rank",
            "value": 2
          }
        ],
        "opposition": [
          {
            "source": "guardian force_incarnation art tier",
            "value": 2
          },
          {
            "source": "active Guardian Abode pressure",
            "value": 3
          }
        ]
      }
    }
    """)!.AsObject();

    private static string BuildPlayerSuccessDiceAuditJson(int? playerValueOverride = null)
    {
        var audit = BuildPlayerSuccessDiceAudit();
        if (playerValueOverride != null &&
            audit["diceUsed"] is JsonArray diceUsed &&
            diceUsed[0] is JsonObject playerDie)
        {
            playerDie["value"] = playerValueOverride.Value;
        }

        return audit.ToJsonString();
    }

    private static string BuildMixedNoEffectDiceAuditJson() => JsonNode.Parse("""
    {
      "formulaVersion": "afterlife_spiritual_conflict_v1",
      "diceSource": "input/turn_request.json.preGeneratedDices1d20",
      "diceUsed": [
        {
          "side": "player",
          "sourceIndex": 4,
          "sides": 20,
          "value": 11
        },
        {
          "side": "opposition",
          "sourceIndex": 5,
          "sides": 20,
          "value": 7
        }
      ],
      "playerTotal": 11,
      "oppositionTotal": 11,
      "margin": 0,
      "outcomeBand": "mixed_or_no_effect",
      "modifierBreakdown": {
        "player": [],
        "opposition": [
          {
            "source": "opposition stabilizing pressure",
            "value": 4
          }
        ]
      }
    }
    """)!.AsObject().ToJsonString();

    private static string BuildPriorTurnDiceAuditJson() => JsonNode.Parse("""
    {
      "formulaVersion": "afterlife_spiritual_conflict_v1",
      "diceSource": "input/turn_request.json.preGeneratedDices1d20",
      "diceUsed": [
        {
          "side": "player",
          "sourceIndex": 0,
          "sides": 20,
          "value": 9
        },
        {
          "side": "opposition",
          "sourceIndex": 1,
          "sides": 20,
          "value": 7
        }
      ],
      "playerTotal": 13,
      "oppositionTotal": 12,
      "margin": 1,
      "outcomeBand": "mixed_or_no_effect",
      "modifierBreakdown": {
        "player": [
          {
            "source": "guard art tier",
            "value": 2
          },
          {
            "source": "current Enlightenment rank",
            "value": 2
          }
        ],
        "opposition": [
          {
            "source": "guardian pressure art tier",
            "value": 5
          }
        ]
      }
    }
    """)!.AsObject().ToJsonString();

    private static JsonObject BuildPlayerSuccessDiceAudit() => JsonNode.Parse("""
    {
      "formulaVersion": "afterlife_spiritual_conflict_v1",
      "diceSource": "input/turn_request.json.preGeneratedDices1d20",
      "diceUsed": [
        {
          "side": "player",
          "sourceIndex": 2,
          "sides": 20,
          "value": 14
        },
        {
          "side": "opposition",
          "sourceIndex": 3,
          "sides": 20,
          "value": 9
        }
      ],
      "playerTotal": 18,
      "oppositionTotal": 14,
      "margin": 4,
      "outcomeBand": "player_success",
      "modifierBreakdown": {
        "player": [
          {
            "source": "guard art tier",
            "value": 2
          },
          {
            "source": "current Enlightenment rank",
            "value": 2
          }
        ],
        "opposition": [
          {
            "source": "guardian pressure art tier",
            "value": 2
          },
          {
            "source": "active Guardian Abode pressure",
            "value": 3
          }
        ]
      }
    }
    """)!.AsObject();

    private static JsonObject BuildPlayerAdvantageDiceAudit(bool selectBest)
    {
        var selectedIndex = selectBest ? 2 : 0;
        var selectedValue = selectBest ? 14 : 5;
        var discardedIndex = selectBest ? 0 : 2;
        var discardedValue = selectBest ? 5 : 14;
        return BuildPlayerMultiRollDiceAudit(
            "advantage",
            new[] { "позиционное преимущество игрока" },
            Array.Empty<string>(),
            selectedIndex,
            selectedValue,
            discardedIndex,
            discardedValue);
    }

    private static JsonObject BuildPlayerDisadvantageDiceAudit(bool selectLowest)
    {
        var selectedIndex = selectLowest ? 0 : 2;
        var selectedValue = selectLowest ? 5 : 14;
        var discardedIndex = selectLowest ? 2 : 0;
        var discardedValue = selectLowest ? 14 : 5;
        return BuildPlayerMultiRollDiceAudit(
            "disadvantage",
            Array.Empty<string>(),
            new[] { "активные оковы мешают действию" },
            selectedIndex,
            selectedValue,
            discardedIndex,
            discardedValue);
    }

    private static JsonObject BuildPlayerDisadvantageDiceAuditWithDiscardedNatural20() =>
        BuildPlayerMultiRollDiceAudit(
            "disadvantage",
            Array.Empty<string>(),
            new[] { "активные оковы мешают действию" },
            selectedIndex: 0,
            selectedValue: 5,
            discardedIndex: 6,
            discardedValue: 20);

    private static JsonObject BuildCancelledPlayerRollWithExtraDieAudit() =>
        BuildPlayerMultiRollDiceAudit(
            "normal",
            new[] { "позиционное преимущество игрока" },
            new[] { "активные оковы мешают действию" },
            selectedIndex: 2,
            selectedValue: 14,
            discardedIndex: 0,
            discardedValue: 5);

    private static JsonObject BuildRollModeSource(string level, string summary) => new()
    {
        ["level"] = level,
        ["summary"] = summary
    };

    private static JsonObject BuildCombatConditionRollModeSource(string conditionId) => new()
    {
        ["level"] = "advantage",
        ["sourceType"] = "combat_condition",
        ["conditionId"] = conditionId,
        ["summary"] = "Метка условия открывает цель для давления."
    };

    public static IEnumerable<object[]> MalformedActiveCombatConditionCases()
    {
        yield return
        [
            "afterlife_combat_condition_missing_source_identity",
            ".combatConditions[0].source",
            new Action<JsonObject>(condition => condition["source"] = new JsonObject())
        ];
        yield return
        [
            "afterlife_combat_condition_missing_finite_duration",
            ".combatConditions[0].duration",
            new Action<JsonObject>(condition => condition["duration"] = new JsonObject())
        ];
        yield return
        [
            "afterlife_combat_condition_missing_finite_duration",
            ".combatConditions[0].duration",
            new Action<JsonObject>(condition =>
                condition["duration"] = new JsonObject { ["type"] = "next_matching_operation" })
        ];
        yield return
        [
            "afterlife_combat_condition_invalid_affected_operation",
            ".combatConditions[0].affectedOperations[0]",
            new Action<JsonObject>(condition =>
                condition["affectedOperations"] = new JsonArray(JsonValue.Create("mortal_attack")))
        ];
        yield return
        [
            "afterlife_combat_condition_missing_payoff_effect",
            ".combatConditions[0].payoff",
            new Action<JsonObject>(condition =>
                condition["payoff"] = new JsonObject { ["sourceType"] = "combat_condition" })
        ];
    }

    private static JsonObject BuildValidCombatCondition(string conditionId = "mark_oath_flare_001") => new()
    {
        ["conditionId"] = conditionId,
        ["displayName"] = "Разогретая клятва",
        ["kind"] = "mark",
        ["polarity"] = "buff",
        ["status"] = "active",
        ["source"] = new JsonObject
        {
            ["type"] = "special_art",
            ["actorType"] = "guardian",
            ["actorId"] = "guardian_azalia",
            ["displayName"] = "Азалия"
        },
        ["targetSide"] = "opposition",
        ["targetActorRef"] = "guardian_liora",
        ["affectedOperations"] = new JsonArray(
            JsonValue.Create("pressure"),
            JsonValue.Create("counter")),
        ["mechanicalAxis"] = "rollMode",
        ["payoff"] = new JsonObject
        {
            ["effect"] = "advantage",
            ["level"] = "advantage",
            ["sourceType"] = "combat_condition"
        },
        ["duration"] = new JsonObject
        {
            ["type"] = "next_matching_operation",
            ["remainingUses"] = 1
        },
        ["counterplay"] = new JsonArray(
            JsonValue.Create("break_binding против контекста клятвы"),
            JsonValue.Create("выбрать действие вне pressure/counter")),
        ["visibility"] = "player_visible",
        ["summary"] = "Клятва подсвечена: давление и контрприём легче направить в противника.",
        ["auditRequirement"] = "При расходовании rollMode должен сослаться на conditionId."
    };

    private static JsonObject BuildCanonicalCombatCondition() => new()
    {
        ["conditionId"] = "ward_choice_mirror_001",
        ["name"] = "Оберег зеркального выбора",
        ["kind"] = "ward",
        ["polarity"] = "buff",
        ["status"] = "active",
        ["source"] = new JsonObject
        {
            ["sourceType"] = "special_art",
            ["actorType"] = "guardian",
            ["actorId"] = "guardian_azalia",
            ["displayName"] = "Азалия"
        },
        ["target"] = new JsonObject
        {
            ["side"] = "player",
            ["actorType"] = "player",
            ["actorId"] = "player_soul",
            ["displayName"] = "Игрок"
        },
        ["affectedOperations"] = new JsonArray(
            JsonValue.Create("guard"),
            JsonValue.Create("incarnation_resistance")),
        ["mechanicalAxes"] = new JsonArray(
            JsonValue.Create("actionCostAudit"),
            JsonValue.Create("specialArtAudit.effectNote")),
        ["payoff"] = new JsonObject
        {
            ["effect"] = "protected_cost",
            ["sourceType"] = "combat_condition"
        },
        ["duration"] = new JsonObject
        {
            ["type"] = "next_matching_operation",
            ["remainingUses"] = 1
        },
        ["counterplay"] = new JsonArray(
            JsonValue.Create("force_binding через другой lead contestant"),
            JsonValue.Create("pressure, чтобы сделать оберег слишком дорогим")),
        ["visibility"] = "visible",
        ["summary"] = "Оберег защищает один ответ от дополнительного ОД штрафа."
    };

    private static JsonObject BuildPlayerTieredRollDiceAudit(
        string effectiveMode,
        IReadOnlyCollection<JsonNode?> advantageSources,
        IReadOnlyCollection<JsonNode?> disadvantageSources,
        IReadOnlyList<(int SourceIndex, int Value, bool Selected)> playerRolls)
    {
        const int playerModifier = 4;
        const int oppositionModifier = 5;
        const int oppositionSourceIndex = 5;
        const int oppositionValue = 7;

        var selectedRoll = playerRolls.Single(roll => roll.Selected);
        var playerTotal = selectedRoll.Value + playerModifier;
        var oppositionTotal = oppositionValue + oppositionModifier;
        var margin = playerTotal - oppositionTotal;

        var diceUsed = new JsonArray();
        foreach (var roll in playerRolls)
        {
            diceUsed.Add(new JsonObject
            {
                ["side"] = "player",
                ["sourceIndex"] = roll.SourceIndex,
                ["sides"] = 20,
                ["value"] = roll.Value,
                ["selection"] = roll.Selected ? "selected" : "discarded"
            });
        }

        diceUsed.Add(new JsonObject
        {
            ["side"] = "opposition",
            ["sourceIndex"] = oppositionSourceIndex,
            ["sides"] = 20,
            ["value"] = oppositionValue,
            ["selection"] = "selected"
        });

        return new JsonObject
        {
            ["formulaVersion"] = "afterlife_spiritual_conflict_v1",
            ["diceSource"] = "input/turn_request.json.preGeneratedDices1d20",
            ["diceUsed"] = diceUsed,
            ["rollMode"] = new JsonObject
            {
                ["player"] = new JsonObject
                {
                    ["effectiveMode"] = effectiveMode,
                    ["advantageSources"] = BuildRollModeSourceArray(advantageSources),
                    ["disadvantageSources"] = BuildRollModeSourceArray(disadvantageSources)
                },
                ["opposition"] = new JsonObject
                {
                    ["effectiveMode"] = "normal",
                    ["advantageSources"] = new JsonArray(),
                    ["disadvantageSources"] = new JsonArray()
                }
            },
            ["playerTotal"] = playerTotal,
            ["oppositionTotal"] = oppositionTotal,
            ["margin"] = margin,
            ["outcomeBand"] = ExpectedOutcomeBandForTest(margin),
            ["modifierBreakdown"] = new JsonObject
            {
                ["player"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["source"] = "pressure art tier",
                        ["value"] = 2
                    },
                    new JsonObject
                    {
                        ["source"] = "current Enlightenment rank",
                        ["value"] = 2
                    }
                },
                ["opposition"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["source"] = "guardian pressure art tier",
                        ["value"] = 2
                    },
                    new JsonObject
                    {
                        ["source"] = "active Guardian Abode pressure",
                        ["value"] = 3
                    }
                }
            }
        };
    }

    private static JsonArray BuildRollModeSourceArray(IReadOnlyCollection<JsonNode?> sources)
    {
        var array = new JsonArray();
        foreach (var source in sources)
        {
            array.Add(source is null ? null : JsonNode.Parse(source.ToJsonString()));
        }

        return array;
    }

    private static JsonObject BuildPlayerMultiRollDiceAudit(
        string effectiveMode,
        IReadOnlyCollection<string> advantageSources,
        IReadOnlyCollection<string> disadvantageSources,
        int selectedIndex,
        int selectedValue,
        int discardedIndex,
        int discardedValue)
    {
        var playerModifier = 4;
        var oppositionModifier = 5;
        var playerTotal = selectedValue + playerModifier;
        var oppositionTotal = 9 + oppositionModifier;
        var margin = playerTotal - oppositionTotal;

        var audit = new JsonObject
        {
            ["formulaVersion"] = "afterlife_spiritual_conflict_v1",
            ["diceSource"] = "input/turn_request.json.preGeneratedDices1d20",
            ["diceUsed"] = new JsonArray
            {
                new JsonObject
                {
                    ["side"] = "player",
                    ["sourceIndex"] = discardedIndex,
                    ["sides"] = 20,
                    ["value"] = discardedValue,
                    ["selection"] = "discarded"
                },
                new JsonObject
                {
                    ["side"] = "player",
                    ["sourceIndex"] = selectedIndex,
                    ["sides"] = 20,
                    ["value"] = selectedValue,
                    ["selection"] = "selected"
                },
                new JsonObject
                {
                    ["side"] = "opposition",
                    ["sourceIndex"] = 3,
                    ["sides"] = 20,
                    ["value"] = 9,
                    ["selection"] = "selected"
                }
            },
            ["rollMode"] = new JsonObject
            {
                ["player"] = new JsonObject
                {
                    ["effectiveMode"] = effectiveMode,
                    ["advantageSources"] = new JsonArray(advantageSources.Select(source => JsonValue.Create(source)).ToArray<JsonNode?>()),
                    ["disadvantageSources"] = new JsonArray(disadvantageSources.Select(source => JsonValue.Create(source)).ToArray<JsonNode?>())
                },
                ["opposition"] = new JsonObject
                {
                    ["effectiveMode"] = "normal",
                    ["advantageSources"] = new JsonArray(),
                    ["disadvantageSources"] = new JsonArray()
                }
            },
            ["playerTotal"] = playerTotal,
            ["oppositionTotal"] = oppositionTotal,
            ["margin"] = margin,
            ["outcomeBand"] = ExpectedOutcomeBandForTest(margin),
            ["modifierBreakdown"] = new JsonObject
            {
                ["player"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["source"] = "pressure art tier",
                        ["value"] = 2
                    },
                    new JsonObject
                    {
                        ["source"] = "current Enlightenment rank",
                        ["value"] = 2
                    }
                },
                ["opposition"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["source"] = "guardian pressure art tier",
                        ["value"] = 2
                    },
                    new JsonObject
                    {
                        ["source"] = "active Guardian Abode pressure",
                        ["value"] = 3
                    }
                }
            }
        };

        return audit;
    }

    private static JsonObject BuildPlayerSuccessDiceAuditWithLightIncarnate(int value = SourceOfLightCapstoneState.LeadDiceBonus)
    {
        var audit = BuildPlayerSuccessDiceAudit();
        if (audit["modifierBreakdown"] is JsonObject modifierBreakdown &&
            modifierBreakdown["player"] is JsonArray player)
        {
            player.Add(new JsonObject
            {
                ["passiveId"] = SourceOfLightCapstoneState.PassiveId,
                ["source"] = SourceOfLightCapstoneState.PassiveId,
                ["value"] = value
            });
        }

        var playerTotal = audit["playerTotal"]!.GetValue<int>() + value;
        var oppositionTotal = audit["oppositionTotal"]!.GetValue<int>();
        var margin = playerTotal - oppositionTotal;
        audit["playerTotal"] = playerTotal;
        audit["margin"] = margin;
        audit["outcomeBand"] = margin >= 8 ? "decisive_player_success" : "player_success";
        return audit;
    }

    private static JsonObject BuildPlayerSuccessDiceAuditWithPositionModifier(string position, int value = 2)
    {
        var audit = BuildPlayerSuccessDiceAudit();
        AddConflictPositionModifier(audit, "player", position, value);
        return audit;
    }

    private static string BuildPlayerSuccessDiceAuditWithoutAbodePowerJson() =>
        BuildPlayerSuccessDiceAuditWithoutAbodePower().ToJsonString();

    private static JsonObject BuildPlayerSuccessDiceAuditWithoutAbodePowerWithPositionModifier(string position, int value = 2)
    {
        var audit = BuildPlayerSuccessDiceAuditWithoutAbodePower();
        AddConflictPositionModifier(audit, "player", position, value);
        return audit;
    }

    private static JsonObject BuildPlayerSuccessDiceAuditWithoutAbodePower()
    {
        var audit = BuildPlayerSuccessDiceAudit();
        var removedValue = 0;
        if (audit["modifierBreakdown"] is JsonObject modifierBreakdown &&
            modifierBreakdown["opposition"] is JsonArray oppositionModifiers)
        {
            for (var i = oppositionModifiers.Count - 1; i >= 0; i--)
            {
                if (oppositionModifiers[i] is not JsonObject modifier ||
                    !string.Equals(modifier["source"]?.GetValue<string>(), "active Guardian Abode pressure", StringComparison.OrdinalIgnoreCase) ||
                    modifier["value"] is not JsonValue valueNode)
                {
                    continue;
                }

                removedValue += valueNode.GetValue<int>();
                oppositionModifiers.RemoveAt(i);
            }
        }

        if (removedValue != 0)
        {
            var playerTotal = audit["playerTotal"]!.GetValue<int>();
            var oppositionTotal = audit["oppositionTotal"]!.GetValue<int>() - removedValue;
            var margin = playerTotal - oppositionTotal;
            audit["oppositionTotal"] = oppositionTotal;
            audit["margin"] = margin;
            audit["outcomeBand"] = ExpectedOutcomeBandForTest(margin);
        }

        return audit;
    }

    private static void AddConflictPositionModifier(
        JsonObject audit,
        string side,
        string? position,
        int value,
        bool includePosition = true)
    {
        if (audit["modifierBreakdown"] is JsonObject modifierBreakdown &&
            modifierBreakdown[side] is JsonArray modifiers)
        {
            var modifier = new JsonObject
            {
                ["modifierType"] = "conflict_position",
                ["source"] = "conflictPosition",
                ["value"] = value
            };
            if (includePosition)
                modifier["position"] = position ?? "";

            modifiers.Add(modifier);
        }

        var playerTotal = audit["playerTotal"]!.GetValue<int>();
        var oppositionTotal = audit["oppositionTotal"]!.GetValue<int>();
        if (string.Equals(side, "player", StringComparison.OrdinalIgnoreCase))
            playerTotal += value;
        else
            oppositionTotal += value;

        var margin = playerTotal - oppositionTotal;
        audit["playerTotal"] = playerTotal;
        audit["oppositionTotal"] = oppositionTotal;
        audit["margin"] = margin;
        audit["outcomeBand"] = ExpectedOutcomeBandForTest(margin);
    }

    private static string ExpectedOutcomeBandForTest(int margin) =>
        margin >= 8 ? "decisive_player_success" :
        margin >= 3 ? "player_success" :
        margin >= -2 ? "mixed_or_no_effect" :
        margin >= -7 ? "opposition_success" :
        "decisive_opposition_success";

    private static string BuildActiveConflictRootJson(string conflictId = "afterlife_conflict_test_001", string realm = "Chaos Sea")
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        root.Remove("lastInvalidUpdate");
        root.Remove("lastInvalidUpdateReason");
        root.Remove("lastInvalidUpdateAtUtc");
        if (root["activeConflict"] is JsonObject activeConflict)
        {
            activeConflict["conflictId"] = conflictId;
            activeConflict["realm"] = realm;
        }
        return root.ToJsonString();
    }

    private static string BuildSoulStateJson(string realm, int inkFeathers = 0) => $$"""
    {
      "soulName": "Асуран",
      "currentRealm": {{JsonSerializer.Serialize(realm)}},
      "inkFeathers": {
        "current": {{inkFeathers}},
        "total": {{inkFeathers}}
      }
    }
    """;

    private static string BuildShiningStateJson(int lightSparks) => $$"""
    {
      "availability": "active",
      "radiance": {
        "experience": 250,
        "tier": 2
      },
      "lightSparks": {{lightSparks}},
      "halls": [],
      "factions": [],
      "shiningPoliticalActors": [],
      "gates": {
        "draftVersion": 0,
        "hasOpenDraft": false,
        "isStale": false,
        "nextCandidateCursor": 0,
        "rerollsRemaining": 0,
        "allCandidateBlessingCards": [],
        "availableBlessingCards": [],
        "shownBlessingCardIds": [],
        "selectedBlessingCardIds": []
      },
      "preparedIncarnationPackage": null,
      "gachaSystem": {
        "chargesPerReturn": 0,
        "chargesUsedThisReturn": 0,
        "currentReturnCycleId": "",
        "gachaHistory": []
      }
    }
    """;

    private Task WriteSoulStateWithInkFeathersAsync(int inkFeathers, string realm) =>
        _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", BuildSoulStateJson(realm, inkFeathers));

    private Task WriteShiningStateWithLightSparksAsync(int lightSparks) =>
        _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, BuildShiningStateJson(lightSparks));

    private Task WriteGameSettingsAsync(string difficulty) =>
        _fs.WriteFileAtomicAsync("game_state/core/game_settings.json", $$"""
        {
          "hardMode": {{JsonSerializer.Serialize(string.Equals(difficulty, "hard", StringComparison.OrdinalIgnoreCase))}},
          "impossibleMode": {{JsonSerializer.Serialize(string.Equals(difficulty, "impossible", StringComparison.OrdinalIgnoreCase))}},
          "difficulty": {{JsonSerializer.Serialize(difficulty)}},
          "qteEventsEnabled": true
        }
        """);

    private static JsonObject BuildDifficultyAudit(string difficulty, int? oppositionModifier = null, int? rewardMultiplierPercent = null) =>
        new()
        {
            ["difficulty"] = difficulty,
            ["source"] = "game_state/core/game_settings.json.difficulty",
            ["oppositionModifier"] = oppositionModifier ?? ResolveTestDifficultyOppositionModifier(difficulty),
            ["rewardMultiplierPercent"] = rewardMultiplierPercent ?? ResolveTestDifficultyRewardMultiplierPercent(difficulty)
        };

    private static void AddGameDifficultyModifier(JsonObject diceAudit, string difficulty, int value)
    {
        diceAudit["difficultyAudit"] = BuildDifficultyAudit(difficulty, oppositionModifier: value);
        if (diceAudit["modifierBreakdown"] is not JsonObject modifierBreakdown)
        {
            modifierBreakdown = new JsonObject();
            diceAudit["modifierBreakdown"] = modifierBreakdown;
        }

        if (modifierBreakdown["opposition"] is not JsonArray oppositionModifiers)
        {
            oppositionModifiers = new JsonArray();
            modifierBreakdown["opposition"] = oppositionModifiers;
        }

        oppositionModifiers.Add(new JsonObject
        {
            ["modifierType"] = "game_difficulty",
            ["source"] = "Сложность игры",
            ["difficulty"] = difficulty,
            ["value"] = value
        });
    }

    private static string BuildHardDifficultyPlayerSuccessDiceAuditJson()
    {
        var diceAudit = BuildPlayerSuccessDiceAudit();
        AddGameDifficultyModifier(diceAudit, "hard", value: 1);
        diceAudit["oppositionTotal"] = diceAudit["oppositionTotal"]!.GetValue<int>() + 1;
        diceAudit["margin"] = diceAudit["playerTotal"]!.GetValue<int>() - diceAudit["oppositionTotal"]!.GetValue<int>();
        return diceAudit.ToJsonString();
    }

    private static int ResolveTestDifficultyOppositionModifier(string difficulty) =>
        difficulty.Trim().ToLowerInvariant() switch
        {
            "hard" => 1,
            "impossible" => 2,
            _ => 0
        };

    private static int ResolveTestDifficultyRewardMultiplierPercent(string difficulty) =>
        difficulty.Trim().ToLowerInvariant() switch
        {
            "hard" => 125,
            "impossible" => 150,
            _ => 100
        };

    private static string BuildConflictRewardAuditJson(
        string realm,
        string currency,
        int finalAmount,
        int opposingLeadStrength = 3,
        int challengeTier = 3,
        string sideModel = "direct_duel",
        string startingConflictPosition = "contested",
        int outcomeMultiplierPercent = 100,
        int riskMultiplierPercent = 100,
        int? baseAmount = null,
        int? resolvedAtTurn = null,
        string? difficulty = null,
        int? difficultyRewardMultiplierPercent = null)
    {
        var resolvedBaseAmount = baseAmount ??
                                 (AfterlifeSpiritualConflictState.NormalizeAfterlifeRealmKey(realm) == "shining_abode"
                                     ? AfterlifeSpiritualConflictState.ShiningConflictRewardBaseAmount
                                     : AfterlifeSpiritualConflictState.ChaosSeaConflictRewardBaseAmount);
        var resolvedAtTurnFragment = resolvedAtTurn == null
            ? string.Empty
            : $",\n          \"resolvedAtTurn\": {resolvedAtTurn.Value}";
        var difficultyAuditFragment = string.IsNullOrWhiteSpace(difficulty)
            ? string.Empty
            : $",\n          \"difficultyAudit\": {BuildDifficultyAudit(difficulty, rewardMultiplierPercent: difficultyRewardMultiplierPercent).ToJsonString()}";
        return $$"""
        {
          "realm": {{JsonSerializer.Serialize(realm)}},
          "currency": {{JsonSerializer.Serialize(currency)}},
          "baseAmount": {{resolvedBaseAmount}},
          "opposingLeadStrength": {{opposingLeadStrength}},
          "sideModel": {{JsonSerializer.Serialize(sideModel)}},
          "startingConflictPosition": {{JsonSerializer.Serialize(startingConflictPosition)}},
          "challengeTier": {{challengeTier}},
          "outcomeMultiplierPercent": {{outcomeMultiplierPercent}},
          "riskMultiplierPercent": {{riskMultiplierPercent}},
          "riskReason": "Started from {{startingConflictPosition}} against a measured opposition lead.",
          "finalAmount": {{finalAmount}},
          "narrativeReason": "Player won a contested afterlife spiritual conflict."{{resolvedAtTurnFragment}}{{difficultyAuditFragment}}
        }
        """;
    }

    private Task WriteResolvedConflictRewardStateAsync(
        string? rewardAuditJson,
        string realm = "Chaos Sea",
        string mode = "resolve",
        string resolutionState = "resolved",
        string operationType = "pressure",
        string playerOutcome = "won",
        bool voluntary = false,
        int? proofResolvedAtTurn = 7,
        string? diceAuditJson = null)
    {
        var rewardAuditFragment = string.IsNullOrWhiteSpace(rewardAuditJson)
            ? string.Empty
            : $",\n        \"{AfterlifeSpiritualConflictState.RewardAuditProperty}\": {rewardAuditJson}";
        var voluntaryFragment = voluntary ? ",\n        \"voluntary\": true" : string.Empty;
        var proofResolvedAtTurnFragment = proofResolvedAtTurn == null
            ? string.Empty
            : $",\n              \"resolvedAtTurn\": {proofResolvedAtTurn.Value}";
        var resolvedDiceAuditJson = diceAuditJson ?? BuildPlayerSuccessDiceAudit().ToJsonString();
        return _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "mode": "{{mode}}",
              "conflictId": "afterlife_conflict_test_001",
              "realm": {{JsonSerializer.Serialize(realm)}},
              "sideModel": "direct_duel",
              "resolutionState": "{{resolutionState}}",
              "operationType": "{{operationType}}",
              "playerOutcome": "{{playerOutcome}}",
              "diceAudit": {{resolvedDiceAuditJson}},
              "summary": "The player side won the spiritual conflict."{{proofResolvedAtTurnFragment}}{{voluntaryFragment}}{{rewardAuditFragment}}
            }
          ]
        }
        """);
    }

    private async Task WriteRewardTurnSnapshotAsync(
        string preTurnSoulJson,
        string? preTurnShiningJson = null,
        string? preTurnConflictJson = null)
    {
        preTurnConflictJson ??= BuildActiveConflictRootJson();
        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", preTurnSoulJson);
        await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.StatePath, preTurnConflictJson);
        var snapshotFiles = new List<(string Path, string Json)>
        {
            ("game_state/meta/soul_state.json", preTurnSoulJson),
            (AfterlifeSpiritualConflictState.StatePath, preTurnConflictJson)
        };
        var gameSettingsJson = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.DifficultySettingsPath);
        if (!string.IsNullOrWhiteSpace(gameSettingsJson))
        {
            await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.DifficultySettingsPath, gameSettingsJson);
            snapshotFiles.Add((AfterlifeSpiritualConflictState.DifficultySettingsPath, gameSettingsJson));
        }

        if (preTurnShiningJson == null)
        {
            await WriteValidatedSnapshotManifestAsync(
                "обработки хода",
                "Я завершаю духовный конфликт посмертия.",
                snapshotFiles.ToArray());
            return;
        }

        await WriteSnapshotFileAsync(ShiningAbodeState.StatePath, preTurnShiningJson);
        snapshotFiles.Add((ShiningAbodeState.StatePath, preTurnShiningJson));
        await WriteValidatedSnapshotManifestAsync(
            "обработки хода",
            "Я завершаю духовный конфликт посмертия.",
            snapshotFiles.ToArray());
    }

    private async Task WritePreTurnActiveConflictSnapshotAsync(string conflictId = "afterlife_conflict_test_001")
    {
        var preTurnConflict = BuildActiveConflictRootJson(conflictId);
        const string soul = """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """;

        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", soul);
        await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.StatePath, preTurnConflict);
        var snapshotFiles = new List<(string Path, string Json)>
        {
            ("game_state/meta/soul_state.json", soul),
            (AfterlifeSpiritualConflictState.StatePath, preTurnConflict)
        };
        var gameSettingsJson = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.DifficultySettingsPath);
        if (!string.IsNullOrWhiteSpace(gameSettingsJson))
        {
            await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.DifficultySettingsPath, gameSettingsJson);
            snapshotFiles.Add((AfterlifeSpiritualConflictState.DifficultySettingsPath, gameSettingsJson));
        }

        await WriteValidatedSnapshotManifestAsync(
            "обработки хода",
            "Я продолжаю активный afterlife spiritual conflict.",
            snapshotFiles.ToArray());
    }

    private async Task WriteValidatedConflictSnapshotFromCurrentAsync(string playerAction)
    {
        var soul = await _fs.ReadFileAsync("game_state/meta/soul_state.json")
            ?? throw new InvalidOperationException("Expected current soul_state.json before snapshot capture.");
        var conflict = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath)
            ?? throw new InvalidOperationException("Expected current afterlife_spiritual_conflict_state.json before snapshot capture.");

        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", soul);
        await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.StatePath, conflict);
        var snapshotFiles = new List<(string Path, string Json)>
        {
            ("game_state/meta/soul_state.json", soul),
            (AfterlifeSpiritualConflictState.StatePath, conflict)
        };
        var gameSettingsJson = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.DifficultySettingsPath);
        if (!string.IsNullOrWhiteSpace(gameSettingsJson))
        {
            await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.DifficultySettingsPath, gameSettingsJson);
            snapshotFiles.Add((AfterlifeSpiritualConflictState.DifficultySettingsPath, gameSettingsJson));
        }

        await WriteValidatedSnapshotManifestAsync(
            "обработки хода",
            playerAction,
            snapshotFiles.ToArray());
    }

    private Task WriteSoulStateAsync() => WriteSoulStateAsync("Chaos Sea");

    private Task WriteSoulStateAsync(string realm)
    {
        return _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", $$"""
        {
          "soulName": "Асуран",
          "currentRealm": {{JsonSerializer.Serialize(realm)}}
        }
        """);
    }

    private Task WriteSoulStateWithAfterlifeCombatProfileAsync(string realm, string afterlifeCombatProfileJson)
    {
        return _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", $$"""
        {
          "soulName": "Асуран",
          "currentRealm": {{JsonSerializer.Serialize(realm)}},
          "afterlifeCombatProfile": {{afterlifeCombatProfileJson}}
        }
        """);
    }

    private async Task WritePreTurnActiveConflictSnapshotWithAuthorityAsync(string conflictId = "afterlife_conflict_test_001")
    {
        await _fs.WriteFileAtomicAsync(GuardianPowerEventState.JournalPath, """{ "entries": [] }""");
        const string guardiansJson = """
        {
          "guardians": []
        }
        """;
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", guardiansJson);
        var preTurnSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(preTurnSoulJson))
            preTurnSoulJson = """
            {
              "soulName": "Асуран",
              "currentRealm": "Chaos Sea"
            }
            """;

        var journalJson = await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath) ?? """{ "entries": [] }""";
        var preTurnConflictJson = BuildActiveConflictRootJson(conflictId);
        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", preTurnSoulJson);
        await WriteSnapshotFileAsync("game_state/meta/guardians.json", guardiansJson);
        await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.StatePath, preTurnConflictJson);
        await WriteSnapshotFileAsync(GuardianPowerEventState.JournalPath, journalJson);

        var snapshotFiles = new List<(string Path, string Json)>
        {
            ("game_state/meta/soul_state.json", preTurnSoulJson),
            ("game_state/meta/guardians.json", guardiansJson),
            (AfterlifeSpiritualConflictState.StatePath, preTurnConflictJson),
            (GuardianPowerEventState.JournalPath, journalJson)
        };

        var profileJson = await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath);
        if (!string.IsNullOrWhiteSpace(profileJson))
        {
            await WriteSnapshotFileAsync(AfterlifeEntityProfileState.StatePath, profileJson);
            snapshotFiles.Add((AfterlifeEntityProfileState.StatePath, profileJson));
        }

        var gameSettingsJson = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.DifficultySettingsPath);
        if (!string.IsNullOrWhiteSpace(gameSettingsJson))
        {
            await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.DifficultySettingsPath, gameSettingsJson);
            snapshotFiles.Add((AfterlifeSpiritualConflictState.DifficultySettingsPath, gameSettingsJson));
        }

        await WriteValidatedSnapshotManifestAsync(
            "обработки хода",
            "Я продолжаю активный afterlife spiritual conflict.",
            snapshotFiles.ToArray());
    }

    private Task WriteAfterlifeEntityProfilesWithPlayerSpecialArtsAsync(string specialArtsJson)
    {
        return _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, $$"""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "player_soul",
              "actorId": "player_soul",
              "displayName": "Асуран",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 0, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "experience": 12, "tier": 1 },
                "radiance": { "experience": 0, "tier": 0 }
              },
              "standardArts": { "pressure": 2, "guard": 1 },
              "specialArts": {{specialArtsJson}},
              "soulDissipationTier": 0,
              "progressionStrategy": {
                "strategyId": "strategy_player_soul",
                "summary": "Игрок сам выбирает развитие.",
                "priorityOrder": ["pressure"],
                "lastUpdatedAtTurn": 7
              },
              "warnings": [],
              "ledger": [
                {
                  "entryId": "profile_player_soul_001",
                  "turnNumber": 7,
                  "reason": "test_profile",
                  "summary": "Профиль души игрока."
                }
              ]
            }
          ]
        }
        """);
    }

    private Task WriteAfterlifeEntityProfilesWithGuardianSpecialArtsAsync()
    {
        return _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, """
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "player_soul",
              "actorId": "player_soul",
              "displayName": "Асуран",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 0, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "experience": 12, "tier": 1 },
                "radiance": { "experience": 0, "tier": 0 }
              },
              "standardArts": { "guard": 1 },
              "specialArts": [],
              "soulDissipationTier": 0,
              "progressionStrategy": {
                "strategyId": "strategy_player_soul",
                "summary": "Игрок сам выбирает развитие.",
                "priorityOrder": ["guard"],
                "lastUpdatedAtTurn": 7
              },
              "warnings": [],
              "ledger": [
                {
                  "entryId": "profile_player_soul_001",
                  "turnNumber": 7,
                  "reason": "test_profile",
                  "summary": "Профиль души игрока."
                }
              ]
            },
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Зеркальный Хранитель",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 80, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "experience": 30, "tier": 2 },
                "radiance": { "experience": 0, "tier": 0 }
              },
              "standardArts": { "pressure": 3, "guard": 1 },
              "specialArts": [
                {
                  "artId": "mirror_pressure",
                  "displayName": "Зеркальное Давление",
                  "ownerActorType": "guardian",
                  "ownerActorId": "guardian_mirror",
                  "baseOperation": "pressure",
                  "tier": 3,
                  "costMultiplierPercent": 150,
                  "upgradeCost": { "inkFeathers": 60, "lightSparks": 0 },
                  "effectSummary": "Давление раздваивает импульс и сложнее читается защитой.",
                  "canTeachPlayer": true,
                  "trainingConditions": ["Заслужить доверие Зеркального Хранителя."]
                }
              ],
              "soulDissipationTier": 0,
              "progressionStrategy": {
                "strategyId": "strategy_guardian_mirror",
                "summary": "Хранитель усиливает особые искусства давления.",
                "priorityOrder": ["mirror_pressure"],
                "lastUpdatedAtTurn": 7
              },
              "warnings": [],
              "ledger": [
                {
                  "entryId": "profile_guardian_mirror_001",
                  "turnNumber": 7,
                  "reason": "test_profile",
                  "summary": "Профиль Зеркального Хранителя."
                }
              ]
            }
          ]
        }
        """);
    }

    private Task WriteAfterlifeEntityProfilesWithPlayerAndGuardianSpecialArtsAsync()
    {
        return _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, """
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "player_soul",
              "actorId": "player_soul",
              "displayName": "Асуран",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 0, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "experience": 12, "tier": 1 },
                "radiance": { "experience": 0, "tier": 0 }
              },
              "standardArts": { "guard": 1 },
              "specialArts": [
                {
                  "artId": "mirror_guard",
                  "displayName": "Зеркальная Защита",
                  "ownerActorType": "player_soul",
                  "ownerActorId": "player_soul",
                  "baseOperation": "guard",
                  "tier": 2,
                  "costMultiplierPercent": 150,
                  "upgradeCost": { "inkFeathers": 60, "lightSparks": 0 },
                  "effectSummary": "Защита собирает входящий удар в отражающую грань."
                },
                {
                  "artId": "echo_guard",
                  "displayName": "Эхо-Защита",
                  "ownerActorType": "player_soul",
                  "ownerActorId": "player_soul",
                  "baseOperation": "guard",
                  "tier": 2,
                  "costMultiplierPercent": 160,
                  "upgradeCost": { "inkFeathers": 70, "lightSparks": 0 },
                  "effectSummary": "Защита оставляет дополнительное эхо, которое тоже влияет на обмен."
                }
              ],
              "soulDissipationTier": 0,
              "progressionStrategy": {
                "strategyId": "strategy_player_soul",
                "summary": "Игрок сам выбирает развитие.",
                "priorityOrder": ["guard"],
                "lastUpdatedAtTurn": 7
              },
              "warnings": [],
              "ledger": [
                {
                  "entryId": "profile_player_soul_001",
                  "turnNumber": 7,
                  "reason": "test_profile",
                  "summary": "Профиль души игрока."
                }
              ]
            },
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Зеркальный Хранитель",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 80, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "experience": 30, "tier": 2 },
                "radiance": { "experience": 0, "tier": 0 }
              },
              "standardArts": { "pressure": 3, "guard": 1 },
              "specialArts": [
                {
                  "artId": "mirror_pressure",
                  "displayName": "Зеркальное Давление",
                  "ownerActorType": "guardian",
                  "ownerActorId": "guardian_mirror",
                  "baseOperation": "pressure",
                  "tier": 3,
                  "costMultiplierPercent": 150,
                  "upgradeCost": { "inkFeathers": 60, "lightSparks": 0 },
                  "effectSummary": "Давление раздваивает импульс и сложнее читается защитой.",
                  "canTeachPlayer": true,
                  "trainingConditions": ["Заслужить доверие Зеркального Хранителя."]
                }
              ],
              "soulDissipationTier": 0,
              "progressionStrategy": {
                "strategyId": "strategy_guardian_mirror",
                "summary": "Хранитель усиливает особые искусства давления.",
                "priorityOrder": ["mirror_pressure"],
                "lastUpdatedAtTurn": 7
              },
              "warnings": [],
              "ledger": [
                {
                  "entryId": "profile_guardian_mirror_001",
                  "turnNumber": 7,
                  "reason": "test_profile",
                  "summary": "Профиль Зеркального Хранителя."
                }
              ]
            }
          ]
        }
        """);
    }

    private Task WriteAfterlifeEntityProfilesWithGuardianSpecialArtTierAsync(int standardPressureTier, int specialPressureTier)
    {
        return _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, $$"""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "player_soul",
              "actorId": "player_soul",
              "displayName": "Асуран",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 0, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "experience": 12, "tier": 1 },
                "radiance": { "experience": 0, "tier": 0 }
              },
              "standardArts": { "guard": 1 },
              "specialArts": [],
              "soulDissipationTier": 0,
              "progressionStrategy": {
                "strategyId": "strategy_player_soul",
                "summary": "Игрок сам выбирает развитие.",
                "priorityOrder": ["guard"],
                "lastUpdatedAtTurn": 7
              },
              "warnings": [],
              "ledger": [
                {
                  "entryId": "profile_player_soul_001",
                  "turnNumber": 7,
                  "reason": "test_profile",
                  "summary": "Профиль души игрока."
                }
              ]
            },
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Зеркальный Хранитель",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 80, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "experience": 30, "tier": 2 },
                "radiance": { "experience": 0, "tier": 0 }
              },
              "standardArts": { "pressure": {{standardPressureTier}}, "guard": 1 },
              "specialArts": [
                {
                  "artId": "mirror_pressure",
                  "displayName": "Зеркальное Давление",
                  "ownerActorType": "guardian",
                  "ownerActorId": "guardian_mirror",
                  "baseOperation": "pressure",
                  "tier": {{specialPressureTier}},
                  "costMultiplierPercent": 150,
                  "upgradeCost": { "inkFeathers": 60, "lightSparks": 0 },
                  "effectSummary": "Давление раздваивает импульс и сложнее читается защитой."
                }
              ],
              "soulDissipationTier": 0,
              "progressionStrategy": {
                "strategyId": "strategy_guardian_mirror",
                "summary": "Хранитель усиливает особые искусства давления.",
                "priorityOrder": ["mirror_pressure"],
                "lastUpdatedAtTurn": 7
              },
              "warnings": [],
              "ledger": [
                {
                  "entryId": "profile_guardian_mirror_001",
                  "turnNumber": 7,
                  "reason": "test_profile",
                  "summary": "Профиль Зеркального Хранителя."
                }
              ]
            }
          ]
        }
        """);
    }

    private Task WriteAfterlifeEntityProfilesWithMirrorAndEchoGuardianSpecialArtsAsync()
    {
        return _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, """
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "player_soul",
              "actorId": "player_soul",
              "displayName": "Асуран",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 0, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "experience": 12, "tier": 1 },
                "radiance": { "experience": 0, "tier": 0 }
              },
              "standardArts": { "guard": 1 },
              "specialArts": [],
              "soulDissipationTier": 0,
              "progressionStrategy": {
                "strategyId": "strategy_player_soul",
                "summary": "Игрок сам выбирает развитие.",
                "priorityOrder": ["guard"],
                "lastUpdatedAtTurn": 7
              },
              "warnings": [],
              "ledger": [
                {
                  "entryId": "profile_player_soul_001",
                  "turnNumber": 7,
                  "reason": "test_profile",
                  "summary": "Профиль души игрока."
                }
              ]
            },
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Зеркальный Хранитель",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 80, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "experience": 30, "tier": 2 },
                "radiance": { "experience": 0, "tier": 0 }
              },
              "standardArts": { "pressure": 3, "guard": 1 },
              "specialArts": [],
              "soulDissipationTier": 0,
              "progressionStrategy": {
                "strategyId": "strategy_guardian_mirror",
                "summary": "Хранитель развивает обычное давление.",
                "priorityOrder": ["pressure"],
                "lastUpdatedAtTurn": 7
              },
              "warnings": [],
              "ledger": [
                {
                  "entryId": "profile_guardian_mirror_001",
                  "turnNumber": 7,
                  "reason": "test_profile",
                  "summary": "Профиль Зеркального Хранителя."
                }
              ]
            },
            {
              "actorType": "guardian",
              "actorId": "guardian_echo",
              "displayName": "Эхо-Хранитель",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 80, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "experience": 30, "tier": 2 },
                "radiance": { "experience": 0, "tier": 0 }
              },
              "standardArts": { "pressure": 3, "guard": 1 },
              "specialArts": [
                {
                  "artId": "echo_pressure",
                  "displayName": "Эхо-Давление",
                  "ownerActorType": "guardian",
                  "ownerActorId": "guardian_echo",
                  "baseOperation": "pressure",
                  "tier": 3,
                  "costMultiplierPercent": 150,
                  "upgradeCost": { "inkFeathers": 60, "lightSparks": 0 },
                  "effectSummary": "Давление расходится повторным эхом."
                }
              ],
              "soulDissipationTier": 0,
              "progressionStrategy": {
                "strategyId": "strategy_guardian_echo",
                "summary": "Хранитель развивает особое давление.",
                "priorityOrder": ["echo_pressure"],
                "lastUpdatedAtTurn": 7
              },
              "warnings": [],
              "ledger": [
                {
                  "entryId": "profile_guardian_echo_001",
                  "turnNumber": 7,
                  "reason": "test_profile",
                  "summary": "Профиль Эхо-Хранителя."
                }
              ]
            }
          ]
        }
        """);
    }

    private Task WriteAfterlifeEntityProfilesWithGuardianLioraStandardArtsAsync()
    {
        return _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, """
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_liora",
              "displayName": "Лиора",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 80, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "experience": 30, "tier": 2 },
                "radiance": { "experience": 0, "tier": 0 }
              },
              "standardArts": { "pressure": 2, "guard": 1 },
              "specialArts": [],
              "customStates": [],
              "soulDissipationTier": 0,
              "progressionStrategy": {
                "strategyId": "strategy_guardian_liora",
                "summary": "Лиора усиливает давление.",
                "priorityOrder": ["pressure"],
                "lastUpdatedAtTurn": 7
              },
              "warnings": [],
              "progressionLedger": [],
              "ledger": [
                {
                  "entryId": "profile_guardian_liora_001",
                  "turnNumber": 7,
                  "reason": "test_profile",
                  "summary": "Профиль Лиоры."
                }
              ]
            }
          ]
        }
        """);
    }

    private Task WriteSoulStateWithTerminalGameOverAsync(string message)
    {
        return _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", $$"""
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea",
          "terminalGameOver": {
            "state": "soul_dispersed",
            "message": {{JsonSerializer.Serialize(message)}},
            "conflictId": "afterlife_conflict_test_001",
            "proofId": "soul_dissipation_proof_player_death_002"
          }
        }
        """);
    }

    private async Task WriteSoulDissipationProfileStateAsync(
        int playerDissipationTier,
        int targetEnlightenmentTier,
        int oppositionDissipationTier = 1,
        int playerEnlightenmentTier = 1)
    {
        await _fs.WriteFileAtomicAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": []
        }
        """);

        await _fs.WriteFileAtomicAsync(
            AfterlifeEntityProfileState.StatePath,
            BuildSoulDissipationProfileStateJson(
                playerDissipationTier,
                targetEnlightenmentTier,
                oppositionDissipationTier,
                playerEnlightenmentTier));
    }

    private static string BuildSoulDissipationProfileStateJson(
        int playerDissipationTier,
        int targetEnlightenmentTier,
        int oppositionDissipationTier = 1,
        int playerEnlightenmentTier = 1) =>
        $$"""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "player_soul",
              "actorId": "player_soul",
              "displayName": "Асуран",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 0, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "experience": 12, "tier": {{playerEnlightenmentTier}} },
                "radiance": { "experience": 0, "tier": 0 }
              },
              "standardArts": { "pressure": 1, "guard": 1 },
              "specialArts": [],
              "soulDissipationTier": {{playerDissipationTier}},
              "progressionStrategy": {
                "strategyId": "strategy_player_soul",
                "summary": "Игрок сам выбирает развитие.",
                "priorityOrder": ["pressure"],
                "lastUpdatedAtTurn": 7
              },
              "warnings": [],
              "ledger": [
                {
                  "entryId": "profile_player_soul_001",
                  "turnNumber": 7,
                  "reason": "test_profile",
                  "summary": "Профиль души игрока."
                }
              ]
            },
            {
              "actorType": "guardian",
              "actorId": "guardian_liora",
              "displayName": "Лиора",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 20, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "experience": 30, "tier": {{targetEnlightenmentTier}} },
                "radiance": { "experience": 0, "tier": 0 }
              },
              "standardArts": { "pressure": 2, "guard": 1 },
              "specialArts": [],
              "soulDissipationTier": {{oppositionDissipationTier}},
              "progressionStrategy": {
                "strategyId": "strategy_guardian_liora",
                "summary": "Лиора усиливает давление.",
                "priorityOrder": ["pressure"],
                "lastUpdatedAtTurn": 7
              },
              "warnings": ["ОПАСНО: может развеять душу после победы, если решит это сделать."],
              "ledger": [
                {
                  "entryId": "profile_guardian_liora_001",
                  "turnNumber": 7,
                  "reason": "test_profile",
                  "summary": "Профиль Лиоры."
                }
              ]
            }
          ]
        }
        """;

    private Task WriteResolvedConflictWithSoulDissipationAsync(
        string soulDissipationProofJson,
        string playerOutcome = "won",
        string resolutionKind = "player_victory")
    {
        return _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "mode": "resolve",
              "conflictId": "afterlife_conflict_test_001",
              "realm": "Chaos Sea",
              "sideModel": "direct_duel",
              "resolutionState": "resolved",
              "operationType": "pressure",
              "playerOutcome": "{{playerOutcome}}",
              "resolutionKind": "{{resolutionKind}}",
              "resolvedAtTurn": 7,
              "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePower().ToJsonString()}},
              "summary": "The spiritual conflict was resolved.",
              "soulDissipationProof": {{soulDissipationProofJson}}
            }
          ]
        }
        """);
    }

    private Task WriteResolvedConflictWithoutSoulDissipationAsync(
        string playerOutcome = "won",
        string resolutionKind = "player_victory")
    {
        return _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "mode": "resolve",
              "conflictId": "afterlife_conflict_test_001",
              "realm": "Chaos Sea",
              "sideModel": "direct_duel",
              "resolutionState": "resolved",
              "operationType": "pressure",
              "playerOutcome": "{{playerOutcome}}",
              "resolutionKind": "{{resolutionKind}}",
              "resolvedAtTurn": 7,
              "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePower().ToJsonString()}},
              "summary": "The spiritual conflict was resolved without final soul dissipation."
            }
          ]
        }
        """);
    }

    private async Task WriteSoulDissipationAuthoritySnapshotAsync(
        string preTurnProfiles,
        string sourceLabel,
        string playerAction)
    {
        const string preTurnSoul = """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """;
        const string guardians = """
        {
          "guardians": []
        }
        """;
        const string journal = """
        {
          "entries": []
        }
        """;
        var preTurnConflict = BuildActiveConflictRootJson();

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", guardians);
        await _fs.WriteFileAtomicAsync(GuardianPowerEventState.JournalPath, journal);
        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", preTurnSoul);
        await WriteSnapshotFileAsync("game_state/meta/guardians.json", guardians);
        await WriteSnapshotFileAsync(GuardianPowerEventState.JournalPath, journal);
        await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.StatePath, preTurnConflict);
        await WriteSnapshotFileAsync(AfterlifeEntityProfileState.StatePath, preTurnProfiles);
        await WriteValidatedSnapshotManifestAsync(
            sourceLabel,
            playerAction,
            ("game_state/meta/soul_state.json", preTurnSoul),
            ("game_state/meta/guardians.json", guardians),
            (GuardianPowerEventState.JournalPath, journal),
            (AfterlifeSpiritualConflictState.StatePath, preTurnConflict),
            (AfterlifeEntityProfileState.StatePath, preTurnProfiles));
    }

    private async Task WriteSoulStateWithLightIncarnateAsync(
        int markerRadianceExperience = SourceOfLightCapstoneState.RequiredRadianceExperience,
        int markerRadianceTier = SourceOfLightCapstoneState.RequiredRadianceTier)
    {
        var request = SourceOfLightCapstoneState.CreateRequest(7, 580, 4);
        var sourceMarker = SourceOfLightCapstoneState.CreateCompletedShiningMarker(request);
        sourceMarker["radianceExperienceAtRequest"] = markerRadianceExperience;
        sourceMarker["radianceTierAtRequest"] = markerRadianceTier;
        var soulRoot = BuildSoulRootWithLightIncarnate(request);
        var shiningRoot = new JsonObject
        {
            ["availability"] = ShiningAbodeState.AvailabilityActive,
            ["radiance"] = new JsonObject
            {
                ["experience"] = 580,
                ["tier"] = 4
            },
            ["preparedIncarnationPackage"] = null,
            [SourceOfLightCapstoneState.ShiningStateProperty] = sourceMarker
        };

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soulRoot.ToJsonString());
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString());
    }

    private async Task WriteSoulStateWithStandaloneLightIncarnateAsync()
    {
        var request = SourceOfLightCapstoneState.CreateRequest(7, 580, 4);
        var soulRoot = BuildSoulRootWithLightIncarnate(request);
        soulRoot.Remove("soulRelics");
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soulRoot.ToJsonString());
    }

    private static JsonObject BuildSoulRootWithLightIncarnate(SourceOfLightCapstoneState.SourceOfLightCapstoneRequest request) =>
        new()
        {
            ["soulName"] = "Асуран",
            ["currentRealm"] = "Chaos Sea",
            [AfterlifeSpiritualConflictState.SoulStateProfileProperty] = new JsonObject
            {
                [SourceOfLightCapstoneState.CapstonesProperty] = new JsonObject
                {
                    [SourceOfLightCapstoneState.LightIncarnateProperty] =
                        SourceOfLightCapstoneState.CreateLightIncarnatePassive(request)
                }
            },
            ["soulRelics"] = new JsonObject
            {
                ["stored"] = new JsonArray(SourceOfLightCapstoneState.CreateIncarnatedLightRelic(request))
            }
        };

    private Task WriteValidPreparedIncarnationPackageAsync()
    {
        return _fs.WriteFileAtomicAsync("game_state/meta/shining_abode_state.json", """
        {
          "availability": "active",
          "preparedIncarnationPackage": {
            "selectedCardIds": [
              "shining_card_memory_001"
            ],
            "selectedCards": [
              {
                "cardId": "shining_card_memory_001",
                "sourceType": "head",
                "effectFamily": "memory",
                "rarity": "rare",
                "effectPayload": {}
              }
            ]
          }
        }
        """);
    }

    private Task WriteShiningAvailabilityAsync(string availability)
    {
        return _fs.WriteFileAtomicAsync("game_state/meta/shining_abode_state.json", $$"""
        {
          "availability": {{JsonSerializer.Serialize(availability)}},
          "radiance": {
            "experience": 250,
            "tier": 2
          },
          "preparedIncarnationPackage": null
        }
        """);
    }

    private Task WriteConflictStateAsync(string outcome) => WriteConflictStateAsync(outcome, "Chaos Sea");

    private Task WriteConflictStateAsync(string outcome, string realm)
    {
        return _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "afterlife_conflict_test_001",
            "realm": {{JsonSerializer.Serialize(realm)}},
            "sideModel": "direct_duel",
            "playerSide": {
              "leadContestant": {
                "actorType": "player",
                "actorId": "player_soul",
                "displayName": "Асуран"
              },
              "supporters": []
            },
            "oppositionSide": {
              "leadContestant": {
                "actorType": "guardian",
                "actorId": "guardian_liora",
                "displayName": "Лиора",
                "actorArtTierSnapshot": {
                  "pressure": 2,
                  "guard": 1
                },
                "artAuthoritySource": "guardian_state"
              },
              "supporters": []
            },
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested",
            "resolutionState": "active",
            "exchangeLog": [
              {
                "exchangeId": "exchange_001",
                "operationType": "guard",
                "outcome": "{{outcome}}",
                "before": {
                  "playerSideStrain": "clear",
                  "oppositionSideStrain": "clear",
                  "conflictPosition": "contested"
                },
                "after": {
                  "playerSideStrain": "clear",
                  "oppositionSideStrain": "clear",
                  "conflictPosition": "contested"
                }
              }
            ]
          },
          "recentConflicts": []
        }
        """);
    }

    private Task WriteConflictStateWithRawExchangeAsync(
        string exchangeJson,
        bool addDefaultMatchupAudit = true,
        string? activeControlStateJson = null,
        bool addDefaultActionCostAudit = true,
        bool syncRootActionEconomyToLastAudit = true,
        int? rootPlayerActionCurrentOverride = null,
        int? rootOppositionActionCurrentOverride = null)
    {
        var projectedExchangeJson = addDefaultMatchupAudit
            ? AddDefaultMatchupAudit(exchangeJson)
            : exchangeJson;
        if (addDefaultActionCostAudit)
            projectedExchangeJson = AddDefaultActionCostAudit(projectedExchangeJson);
        var activeControlStateFragment = string.IsNullOrWhiteSpace(activeControlStateJson)
            ? string.Empty
            : $",\n            \"controlState\": {activeControlStateJson}";
        var playerActionCurrent = ResolveRootPlayerActionCurrentFromExchangeLog(
            projectedExchangeJson,
            syncRootActionEconomyToLastAudit);
        var oppositionActionCurrent = ResolveRootActionCurrentFromExchangeLog(
            projectedExchangeJson,
            "opposition",
            syncRootActionEconomyToLastAudit);
        playerActionCurrent = rootPlayerActionCurrentOverride ?? playerActionCurrent;
        oppositionActionCurrent = rootOppositionActionCurrentOverride ?? oppositionActionCurrent;

        return _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "afterlife_conflict_test_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "playerSide": {
              "leadContestant": {
                "actorType": "player",
                "actorId": "player_soul",
                "displayName": "Асуран"
              },
              "supporters": []
            },
            "oppositionSide": {
              "leadContestant": {
                "actorType": "guardian",
                "actorId": "guardian_liora",
                "displayName": "Лиора",
                "actorArtTierSnapshot": {
                  "pressure": 2,
                  "guard": 1
                },
                "artAuthoritySource": "guardian_state"
              },
              "supporters": []
            },
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested",
            "actionEconomy": {
              "player": { "current": {{playerActionCurrent}}, "max": 6, "source": "Средоточие Души tier 0" },
              "opposition": { "current": {{oppositionActionCurrent}}, "max": 6, "source": "opposition spiritual authority" }
            },
            "resolutionState": "active"{{activeControlStateFragment}},
            "exchangeLog": [
              {{projectedExchangeJson}}
            ]
          },
          "recentConflicts": []
        }
        """);
    }

    private Task WriteConflictStateWithRawExchangeLogAsync(
        string exchangeLogJson,
        bool syncRootActionEconomyToLastAudit = true)
    {
        var playerActionCurrent = ResolveRootPlayerActionCurrentFromExchangeLog(
            exchangeLogJson,
            syncRootActionEconomyToLastAudit);
        var oppositionActionCurrent = ResolveRootActionCurrentFromExchangeLog(
            exchangeLogJson,
            "opposition",
            syncRootActionEconomyToLastAudit);
        return _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "afterlife_conflict_test_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "playerSide": {
              "leadContestant": {
                "actorType": "player",
                "actorId": "player_soul",
                "displayName": "Асуран"
              },
              "supporters": []
            },
            "oppositionSide": {
              "leadContestant": {
                "actorType": "guardian",
                "actorId": "guardian_liora",
                "displayName": "Лиора",
                "actorArtTierSnapshot": {
                  "pressure": 2,
                  "guard": 1
                },
                "artAuthoritySource": "guardian_state"
              },
              "supporters": []
            },
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested",
            "actionEconomy": {
              "player": { "current": {{playerActionCurrent}}, "max": 6, "source": "Средоточие Души tier 0" },
              "opposition": { "current": {{oppositionActionCurrent}}, "max": 6, "source": "opposition spiritual authority" }
            },
            "resolutionState": "active",
            "exchangeLog": [
              {{exchangeLogJson}}
            ]
          },
          "recentConflicts": []
        }
        """);
    }

    private async Task WriteActiveConflictWithCombatConditionsAsync(string combatConditionJson)
    {
        await WriteEmptyGuardianPowerJournalAsync();
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        root.Remove("lastInvalidUpdate");
        root.Remove("lastInvalidUpdateReason");
        root.Remove("lastInvalidUpdateAtUtc");
        var active = (JsonObject)root["activeConflict"]!;
        active["combatConditions"] = new JsonArray(JsonNode.Parse(combatConditionJson));
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
    }

    private async Task SetActiveCombatConditionsAsync(string combatConditionJson)
    {
        await WriteEmptyGuardianPowerJournalAsync();
        var current = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath)
            ?? throw new InvalidOperationException("Expected active conflict fixture before adding combatConditions.");
        var root = JsonNode.Parse(current)!.AsObject();
        var active = (JsonObject)root["activeConflict"]!;
        active["combatConditions"] = new JsonArray(JsonNode.Parse(combatConditionJson));
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
    }

    private Task WriteEmptyGuardianPowerJournalAsync() =>
        _fs.WriteFileAtomicAsync(GuardianPowerEventState.JournalPath, """{ "entries": [] }""");

    private static string AddDefaultMatchupAudit(string exchangeJson)
    {
        if (JsonNode.Parse(exchangeJson) is not JsonObject exchange)
            return exchangeJson;

        if (exchange["matchupAudit"] is JsonObject)
            return exchange.ToJsonString();

        var operationType = AfterlifeSpiritualConflictState.GetNodeString(exchange["operationType"]) ?? "pressure";
        var oppositionOperation = ResolveDefaultOppositionOperation(exchange, operationType);
        exchange["matchupAudit"] = new JsonObject
        {
            ["playerOperation"] = operationType,
            ["oppositionOperation"] = oppositionOperation,
            ["primaryResolutionLane"] = operationType,
            ["matchupRationale"] = "Test fixture supplies the required tactical matchup audit for a current contested exchange.",
            ["riskProfile"] = DefaultRiskProfileForOperation(operationType)
        };

        return exchange.ToJsonString();
    }

    private static string AddDefaultActionCostAudit(string exchangeJson)
    {
        if (JsonNode.Parse(exchangeJson) is not JsonObject exchange)
            return exchangeJson;

        if (exchange["actionCostAudit"] is JsonObject)
            return exchange.ToJsonString();

        var operationType = AfterlifeSpiritualConflictState.GetNodeString(exchange["operationType"]);
        if (string.IsNullOrWhiteSpace(operationType) ||
            IsTerminalActionCostTestOperation(operationType) ||
            !TryGetTestActionCost(operationType, out var baseCost, out var minCost))
        {
            return exchange.ToJsonString();
        }

        const int artTier = 0;
        var effectiveCost = Math.Max(minCost, baseCost - artTier);
        var before = string.Equals(operationType, "recover_spiritual_power", StringComparison.OrdinalIgnoreCase) ? 3 : 6;
        var after = string.Equals(operationType, "recover_spiritual_power", StringComparison.OrdinalIgnoreCase)
            ? 6
            : before - effectiveCost;

        var actionCostAudit = new JsonObject
        {
            ["player"] = new JsonObject
            {
                ["operationType"] = operationType,
                ["baseCost"] = baseCost,
                ["minCost"] = minCost,
                ["artTier"] = artTier,
                ["effectiveCost"] = effectiveCost,
                ["before"] = before,
                ["after"] = after
            }
        };

        var oppositionOperation = ResolveDefaultOppositionOperation(exchange, operationType);
        if (!IsTerminalActionCostTestOperation(oppositionOperation) &&
            !string.Equals(oppositionOperation, "none", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(oppositionOperation, "passive", StringComparison.OrdinalIgnoreCase) &&
            TryGetTestActionCost(oppositionOperation, out var oppositionBaseCost, out var oppositionMinCost))
        {
            var oppositionArtTier = ResolveDefaultOppositionArtTier(oppositionOperation);
            var oppositionEffectiveCost = Math.Max(oppositionMinCost, oppositionBaseCost - oppositionArtTier);
            var oppositionBefore = string.Equals(oppositionOperation, "recover_spiritual_power", StringComparison.OrdinalIgnoreCase) ? 3 : 6;
            var oppositionAfter = string.Equals(oppositionOperation, "recover_spiritual_power", StringComparison.OrdinalIgnoreCase)
                ? 6
                : oppositionBefore - oppositionEffectiveCost;
            actionCostAudit["opposition"] = new JsonObject
            {
                ["operationType"] = oppositionOperation,
                ["baseCost"] = oppositionBaseCost,
                ["minCost"] = oppositionMinCost,
                ["artTier"] = oppositionArtTier,
                ["effectiveCost"] = oppositionEffectiveCost,
                ["before"] = oppositionBefore,
                ["after"] = oppositionAfter
            };
        }

        exchange["actionCostAudit"] = actionCostAudit;
        return exchange.ToJsonString();
    }

    private static int ResolveRootPlayerActionCurrentFromExchangeLog(string exchangeLogJson, bool syncRootActionEconomyToLastAudit)
    {
        return ResolveRootActionCurrentFromExchangeLog(exchangeLogJson, "player", syncRootActionEconomyToLastAudit);
    }

    private static int ResolveRootActionCurrentFromExchangeLog(string exchangeLogJson, string side, bool syncRootActionEconomyToLastAudit)
    {
        if (!syncRootActionEconomyToLastAudit)
            return 6;

        try
        {
            if (JsonNode.Parse($"[{exchangeLogJson}]") is not JsonArray exchangeLog)
                return 6;

            int? current = null;
            foreach (var node in exchangeLog.OfType<JsonObject>())
            {
                if (node["actionCostAudit"] is JsonObject actionCostAudit &&
                    actionCostAudit[side] is JsonObject sideAudit &&
                    sideAudit["after"] is JsonValue afterValue &&
                    afterValue.TryGetValue<int>(out var after))
                {
                    current = after;
                }
            }

            return current ?? 6;
        }
        catch (JsonException)
        {
            return 6;
        }
    }

    private static bool IsTerminalActionCostTestOperation(string operationType) =>
        string.Equals(operationType, "withdraw", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(operationType, "surrender", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(operationType, "negotiate", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetTestActionCost(string operationType, out int baseCost, out int minCost)
    {
        (baseCost, minCost) = NormalizeTestOperation(operationType) switch
        {
            "pressure" => (3, 1),
            "guard" => (2, 1),
            "counter" => (4, 2),
            "maneuver" => (3, 1),
            "binding" => (4, 2),
            "force_binding" => (5, 2),
            "break_binding" => (3, 1),
            "incarnation_resistance" => (3, 1),
            "champion_coordination" => (2, 1),
            "recover_spiritual_power" => (0, 0),
            _ => (-1, -1)
        };
        return baseCost >= 0;
    }

    private static int ResolveDefaultOppositionArtTier(string operationType) =>
        NormalizeTestOperation(operationType) switch
        {
            "pressure" => 2,
            "guard" => 1,
            _ => 0
        };

    private static string ResolveDefaultOppositionOperation(JsonObject exchange, string operationType)
    {
        var incomingOperations = new List<string>();
        if (exchange["incomingAction"] is JsonObject incomingAction)
        {
            AddIncomingTestOperation(incomingOperations, incomingAction["operationType"]);
            AddIncomingTestOperation(incomingOperations, incomingAction["finalOperationType"]);
        }

        if (exchange["matchupAudit"] is JsonObject matchupAudit)
        {
            var matchupOperation = AfterlifeSpiritualConflictState.GetNodeString(matchupAudit["oppositionOperation"]);
            if (!string.IsNullOrWhiteSpace(matchupOperation) &&
                (incomingOperations.Count == 0 ||
                 incomingOperations.Any(incomingOperation => string.Equals(
                     NormalizeTestOperation(incomingOperation),
                     NormalizeTestOperation(matchupOperation),
                     StringComparison.OrdinalIgnoreCase))))
            {
                return matchupOperation;
            }
        }

        if (incomingOperations.Count > 0)
            return incomingOperations[0];

        return NormalizeTestOperation(operationType) switch
        {
            "pressure" => "maneuver",
            "guard" => "pressure",
            "counter" => "pressure",
            "maneuver" => "guard",
            "binding" or "force_binding" => "guard",
            "break_binding" => "binding",
            "incarnation_resistance" => "force_incarnation",
            "champion_coordination" => "pressure",
            "recover_spiritual_power" => "guard",
            _ => "none"
        };
    }

    private static void AddIncomingTestOperation(List<string> operations, JsonNode? node)
    {
        var operation = AfterlifeSpiritualConflictState.GetNodeString(node);
        if (string.IsNullOrWhiteSpace(operation) ||
            operations.Any(existing => string.Equals(
                NormalizeTestOperation(existing),
                NormalizeTestOperation(operation),
                StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        operations.Add(operation);
    }

    private static string DefaultRiskProfileForOperation(string operationType) =>
        NormalizeTestOperation(operationType) switch
        {
            "pressure" => "offensive_pressure",
            "guard" => "safe_defense",
            "counter" => "risky_reversal",
            "maneuver" => "position_play",
            "binding" or "force_binding" => "control_leverage",
            "break_binding" or "incarnation_resistance" => "anti_control",
            "champion_coordination" => "champion_support",
            "recover_spiritual_power" => "recovery_timing",
            _ => "terminal_choice"
        };

    private static string NormalizeTestOperation(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private Task WriteConflictStateWithRawPlayerSupportersAsync(string supportersJson)
    {
        return _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "afterlife_conflict_test_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "playerSide": {
              "leadContestant": {
                "actorType": "player",
                "actorId": "player_soul",
                "displayName": "Асуран"
              },
              "supporters": {{supportersJson}}
            },
            "oppositionSide": {
              "leadContestant": {
                "actorType": "guardian",
                "actorId": "guardian_liora",
                "displayName": "Лиора",
                "actorArtTierSnapshot": {
                  "pressure": 2,
                  "guard": 1
                },
                "artAuthoritySource": "guardian_state"
              },
              "supporters": []
            },
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested",
            "resolutionState": "active",
            "exchangeLog": [
              {
                "exchangeId": "exchange_001",
                "operationType": "guard",
                "outcome": "no_effect",
                "before": { "conflictPosition": "contested" },
                "after": { "conflictPosition": "contested" }
              }
            ]
          },
          "recentConflicts": []
        }
        """);
    }

    private Task WriteSnapshotFileAsync(string logicalPath, string json)
    {
        return _fs.WriteFileAtomicAsync($"game_state/control/pending_turn_snapshot/{logicalPath}", json);
    }

    private Task WriteValidatedSnapshotManifestAsync(params (string Path, string Json)[] snapshotFiles) =>
        WriteValidatedSnapshotManifestAsync("afterlife-spiritual-conflict-tests", "mortal turn", snapshotFiles);

    private async Task WriteValidatedSnapshotManifestAsync(
        string sourceLabel,
        string playerAction,
        params (string Path, string Json)[] snapshotFiles)
    {
        const string sessionId = "session_spiritual_conflict_tests";
        const string requestId = "request_spiritual_conflict_tests";
        const int turnNumber = 7;

        await _fs.WriteFileAtomicAsync("input/turn_request.json", $$"""
        {
          "sessionId": "{{sessionId}}",
          "requestId": "{{requestId}}",
          "turnNumber": {{turnNumber}},
          "playerAction": {{JsonSerializer.Serialize(playerAction)}},
          "preGeneratedDices1d20": {{JsonSerializer.Serialize(AuthoritativeConflictDice)}}
        }
        """);

        var files = new JsonObject();
        var snapshotFileHashes = new JsonObject();
        var rollbackBaselineFiles = new JsonArray();

        foreach (var (path, json) in snapshotFiles)
        {
            files[path] = $"game_state/control/pending_turn_snapshot/{path}";
            snapshotFileHashes[path] = PendingTurnSnapshotAuthority.ComputeSha256(json);
            rollbackBaselineFiles.Add(path);
        }

        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turnNumber,
            ["requestTimestamp"] = "2026-05-06T00:00:00Z",
            ["playerAction"] = playerAction,
            ["preGeneratedDices1d20"] = JsonSerializer.SerializeToNode(AuthoritativeConflictDice),
            ["files"] = files,
            ["snapshotFileHashes"] = snapshotFileHashes,
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = rollbackBaselineFiles,
            ["sourceLabel"] = sourceLabel,
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);

        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // ignored
        }
    }
}
