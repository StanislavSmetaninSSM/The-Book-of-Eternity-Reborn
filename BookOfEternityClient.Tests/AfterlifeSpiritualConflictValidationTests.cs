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

    private static string BuildActiveConflictRootJson(string conflictId = "afterlife_conflict_test_001")
    {
        var root = BuildRootWithActiveConflictAndInvalidMarkers();
        root.Remove("lastInvalidUpdate");
        root.Remove("lastInvalidUpdateReason");
        root.Remove("lastInvalidUpdateAtUtc");
        if (root["activeConflict"] is JsonObject activeConflict)
            activeConflict["conflictId"] = conflictId;
        return root.ToJsonString();
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
        await WriteValidatedSnapshotManifestAsync(
            "обработки хода",
            "Я продолжаю активный afterlife spiritual conflict.",
            ("game_state/meta/soul_state.json", soul),
            (AfterlifeSpiritualConflictState.StatePath, preTurnConflict));
    }

    private async Task WriteValidatedConflictSnapshotFromCurrentAsync(string playerAction)
    {
        var soul = await _fs.ReadFileAsync("game_state/meta/soul_state.json")
            ?? throw new InvalidOperationException("Expected current soul_state.json before snapshot capture.");
        var conflict = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath)
            ?? throw new InvalidOperationException("Expected current afterlife_spiritual_conflict_state.json before snapshot capture.");

        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", soul);
        await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.StatePath, conflict);
        await WriteValidatedSnapshotManifestAsync(
            "обработки хода",
            playerAction,
            ("game_state/meta/soul_state.json", soul),
            (AfterlifeSpiritualConflictState.StatePath, conflict));
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

    private Task WriteConflictStateWithRawExchangeAsync(string exchangeJson)
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
              {{exchangeJson}}
            ]
          },
          "recentConflicts": []
        }
        """);
    }

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
