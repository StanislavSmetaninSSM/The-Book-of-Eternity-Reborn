using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;

namespace BookOfEternityClient.Core;

internal enum AcceptedTurnValidationDisposition
{
    Accepted,
    TerminalRejected,
    RetryablePublicationRearmed,
    RetryablePublicationHeldBlocked,

    /// <summary>
    /// Preserves the original turn and committed spiritual decision while its pending projection awaits recovery.
    /// </summary>
    SpiritualContinuationHeldBlocked
}

public partial class GameEngine
{
    private const string WoundNarrativeOutputPath =
        "output/narrative_response.json";
    private const string GmBridgeIdleWithoutTerminalSignalHarnessSource = "gm_bridge_idle_without_terminal_signal";
    private const string GmOutputWithoutTerminalSignalHarnessSource = "gm_output_without_terminal_signal";
    private const string ClientRecoveredMissingTerminalSignalHarnessSource = "client_recovered_gm_output_without_terminal_signal";
    private static readonly string[] RecoverableGmOutputRequiredFiles =
    [
        "output/narrative_response.json",
        "output/debug_logs.json"
    ];
    private WoundPlayerNotification[] _acceptedTurnWoundNotifications =
        Array.Empty<WoundPlayerNotification>();
    private SpiritualWoundPublishedOutput? _acceptedTurnSpiritualOutput;
    private ValidationService.SpiritualOriginalTurnCapture.SpiritualCompletedConflictValidation?
        _acceptedTurnSpiritualConflictValidation;


    private sealed class CompensatedTreatmentPublicationException : Exception
    {
        internal CompensatedTreatmentPublicationException(
            Exception originalException,
            MortalWoundTreatmentPublicationTransactionOutcome outcome)
            : base(
                "The held treatment publication failed after publication and was safely compensated.",
                originalException)
        {
            Outcome = outcome;
        }

        internal MortalWoundTreatmentPublicationTransactionOutcome Outcome
        {
            get;
        }
    }

    private sealed class UnsafeTreatmentPublicationSettlementException
        : AggregateException
    {
        internal UnsafeTreatmentPublicationSettlementException(
            Exception originalException,
            Exception settlementException)
            : base(
                "Held treatment publication failed and its compensation did not reach one exact safe settlement.",
                originalException,
                settlementException)
        {
        }
    }

    internal static WoundAcceptedTurnOutputBindingResult BindAcceptedWoundOutput(
        AcceptedMechanicsPlan plan,
        string finalSceneText)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var bundle = plan.WoundStageBundle;
        if (bundle is null)
        {
            return new WoundAcceptedTurnOutputBindingResult(
                Array.Empty<WoundPlayerNotification>(),
                Array.Empty<ValidationIssue>());
        }

        var bound = WoundPlayerNotification.ComposeAcceptedTurn(
            bundle,
            finalSceneText);
        if (bound.Success)
            return bound;

        return new WoundAcceptedTurnOutputBindingResult(
            Array.Empty<WoundPlayerNotification>(),
            bound.Issues.Select(static issue =>
            {
                var narrativeRepairable = IsWoundNarrationRepairIssue(issue);
                return new ValidationIssue(
                    narrativeRepairable
                        ? WoundNarrativeOutputPath + ".response"
                        : AcceptedMechanicsPlan.WoundCommandPath,
                    issue.Severity,
                    issue.Message,
                    code: issue.Code,
                    actor: narrativeRepairable ? issue.Actor : "Client",
                    section: issue.Section,
                    expected: issue.Expected,
                    actual: issue.Actual,
                    repairHint: narrativeRepairable
                        ? issue.RepairHint
                        : "Reject the accepted turn because its finalized wound stages no longer agree.",
                    category: issue.Category,
                    repairTargetFiles: narrativeRepairable
                        ? new[] { WoundNarrativeOutputPath }
                        : Array.Empty<string>());
            })
                .ToArray());
    }

    private static bool IsWoundNarrationRepairIssue(ValidationIssue issue) =>
        issue.Code is
            "wound_acquisition_narration_missing" or
            "wound_acquisition_narration_contradiction";

    private Task EnsureClientOwnedSystemFilesHealthyAsync(bool ordinaryEntry = false)=>
        SessionOperationContext.RunParticipatingCurrentSessionAsync(_fs,()=>EnsureClientOwnedSystemFilesHealthyCoreAsync(ordinaryEntry));
    private async Task EnsureClientOwnedSystemFilesHealthyCoreAsync(bool ordinaryEntry)
    {
        await CleanupOrphanedTurnRequestBeforeValidationAsync();
        await _stateManager.RefreshGameStateAsync();
        var preserveControlFilesForTerminalValidation =
            await ShouldPreserveClientOwnedControlFilesForTerminalValidationAsync();

        if (ordinaryEntry)
        {
            if (!await WriteGameSettingsForGm())
                throw new InvalidDataException("Синхронизация настроек не подтверждена. Требуется завершить активную запись или восстановление перед продолжением.");
        }
        else
        {
            // B3: accepted-turn/repair receipt workflows retain their existing
            // route until that whole publication boundary is migrated.
            if (await _systemModService.WriteManifestForGmAsync())
                await _stateManager.SaveSettingsAsync();
        }

        await AfterlifeNotificationState.EnsureHealthyAsync(_fs);
        if (!preserveControlFilesForTerminalValidation)
        {
            await _afterlifeReturnGuardService.EnsureHealthyAsync(_stateManager.CurrentState.CurrentRealm);
            await _systemGuardianLibraryService.EnsureAttractionRequestHealthyAsync(_stateManager.CurrentState.CurrentRealm);
        }

        await _qteSceneService.EnsureRuntimeStateHealthyAsync();
        await _progressionSchedule.EnsureInitializedAsync();
    }

    private async Task CleanupOrphanedTurnRequestBeforeValidationAsync()
    {
        if (!_fs.FileExists("input/turn_request.json"))
            return;

        if (HasTerminalReadySignal() ||
            _fs.FileExists(ValidationRepairRequestPath) ||
            _fs.FileExists(ValidationRepairReadyPath))
            return;

        var pendingSnapshot = await ResolveActivePendingTurnSnapshotContextAsync();
        if (pendingSnapshot.Status == PendingTurnSnapshotResolutionStatus.Usable)
            return;

        _logger.LogWarning(
            pendingSnapshot.Status == PendingTurnSnapshotResolutionStatus.Missing
                ? "Найден orphaned input/turn_request.json без pending snapshot manifest перед validation. Удаление как stale runtime artifact."
                : "Найден input/turn_request.json с unreadable/invalid validated pending snapshot authority перед validation. Удаление как stale runtime artifact.");
        _fs.DeleteFile("input/turn_request.json");
    }

    private async Task<bool> ValidateCurrentGameStateOrShowErrorsAsync(string source,
        RollbackSnapshot? rollbackSnapshot = null,
        ProgressionControl? progressionControl = null,
        bool allowRepairLoop = false,
        DateTime? initialCanonicalRepairBoundaryUtc = null,
        IReadOnlyCollection<ValidationIssue>? initialCanonicalRepairErrors = null,
        DateTime? initialCanonicalRepairStartedAtUtc = null,
        byte[]? pendingResolutionRepairCheckpoint = null,
        Func<Task>? beforeRepairMutation = null,
        Func<Task>? beforeTerminalRepairReturn = null,
        bool applyProgressionOnSuccess = true,
        MortalWoundTreatmentResourcePublicationTransaction?
            treatmentResourcePublicationTransaction = null)
    {
        var repairAttempt = 0;
        List<ValidationIssue>? lastRepairErrors = null;
        IReadOnlyCollection<ValidationIssue>? outputFreshnessRepairErrors = initialCanonicalRepairErrors;
        var lastRepairAttempt = 0;
        var lastCanonicalRepairBoundaryUtc = initialCanonicalRepairBoundaryUtc;
        DateTime? lastCanonicalRepairStartedAtUtc = initialCanonicalRepairStartedAtUtc;
        string? lastRepairSessionGeneration = null;
        var repairMutationPrepared = false;

        while (true)
        {
            await EnsureClientOwnedSystemFilesHealthyAsync();
            var issues = await _validator.ValidateGameStateAsync();
            if (RequiresAcceptedTurnPayloadValidation(source))
            {
                if (RequiresFreshNarrativePayload(source))
                    issues.AddRange(await _validator.ValidateAcceptedTurnNarrativePayloadAsync());
                issues.AddRange(await _validator.ValidateAcceptedTurnInterfacePayloadAsync());
                issues.AddRange(await _validator.ValidateAcceptedTurnReasoningAsync());
                issues.AddRange(await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync());
                issues.AddRange(await _validator.ValidateAcceptedTurnQteOfferAsync());
                issues.AddRange(await _validator.ValidateAcceptedTurnMortalCombatMaterializationAsync());
                issues.AddRange(await _validator.ValidateAcceptedTurnMortalLevelUpMaterializationAsync());
            }
            if (allowRepairLoop &&
                outputFreshnessRepairErrors is { Count: > 0 } &&
                lastCanonicalRepairStartedAtUtc.HasValue)
            {
                lastCanonicalRepairBoundaryUtc = ResolveCanonicalRepairOutputFreshnessBoundaryUtc(
                    outputFreshnessRepairErrors,
                    lastCanonicalRepairStartedAtUtc.Value,
                    lastCanonicalRepairBoundaryUtc);
            }
            if (allowRepairLoop &&
                outputFreshnessRepairErrors is { Count: > 0 } &&
                lastCanonicalRepairBoundaryUtc.HasValue)
            {
                issues.AddRange(CollectPlayerFacingOutputStaleAfterCanonicalRepairIssues(
                    outputFreshnessRepairErrors,
                    lastCanonicalRepairBoundaryUtc.Value));
            }
            issues.AddRange(await _validator.ValidatePendingMemoryLegacyApplicationAsync());
            if (progressionControl != null)
                issues.AddRange(await _progressionSchedule.ValidateAcceptedTurnOutcomeAsync(progressionControl));
            if (treatmentResourcePublicationTransaction is not null)
            {
                issues = (await treatmentResourcePublicationTransaction
                    .FilterExactPublishedItemValidationIssuesAsync(_fs, issues))
                    .ToList();
            }
            var errors = PrioritizeValidationErrors(issues.Where(i => i.Severity == IssueSeverity.Error)).ToList();

            if (allowRepairLoop)
                errors = await FilterRestoredForbiddenRealmBaselineErrorsAsync(source, errors);

            if (errors.Any(static issue => issue.Code == "spiritual_completed_conflict_validation_mismatch"))
            {
                _logger.LogError("Completed spiritual publication lost its exact validation binding after {Source}.", source);
                if (beforeTerminalRepairReturn is not null)
                    await beforeTerminalRepairReturn();
                return false;
            }

            if (errors.Count == 0)
            {
                if (allowRepairLoop && lastRepairErrors is { Count: > 0 })
                {
                    await AppendClearedValidationRepairTrajectoryAsync(
                        source,
                        lastRepairErrors,
                        lastRepairAttempt,
                        lastRepairSessionGeneration!);
                    await DeleteValidationRepairFilesForSessionAsync(lastRepairSessionGeneration!);
                }
                else
                {
                    await DeleteValidationRepairFilesAsync();
                }
                if (applyProgressionOnSuccess && progressionControl != null)
                    await _progressionSchedule.ApplyAcceptedTurnOutcomeAsync(progressionControl);
                return true;
            }

            if (allowRepairLoop &&
                !repairMutationPrepared &&
                beforeRepairMutation is not null)
            {
                await beforeRepairMutation();
                repairMutationPrepared = true;
            }

            if (allowRepairLoop && await TryAutoRollbackRealmSegregationViolationsAsync(source, errors))
            {
                await RefreshRuntimeStateAsync();
                continue;
            }

            if (allowRepairLoop && await TryAutoRepairStartupGuardianDirectMaterializationAsync(source, errors))
            {
                await RefreshRuntimeStateAsync();
                continue;
            }

            _logger.LogError("Нарушение контракта состояния после {Source}: {Count} ошибок", source, errors.Count);

            if (!allowRepairLoop)
            {
                if (HasRollbackCapability(rollbackSnapshot))
                {
                    await RestorePreTurnBackup(rollbackSnapshot!);
                    CleanupBackup(rollbackSnapshot!);
                }

                await _progressionSchedule.DeleteTransientReportAsync();
                ShowContractValidationErrors(source, errors);
                return false;
            }

            repairAttempt++;
            lastRepairErrors = errors;
            lastRepairAttempt = repairAttempt;
            lastRepairSessionGeneration = await CaptureCurrentSessionGenerationAsync();
            var repairStartedAtUtc = DateTime.UtcNow;
            if (!await WaitForContractRepairAsync(
                    source,
                    errors,
                    repairAttempt,
                    rollbackSnapshot,
                    lastRepairSessionGeneration,
                    pendingResolutionRepairCheckpoint: pendingResolutionRepairCheckpoint))
            {
                if (beforeTerminalRepairReturn is not null)
                    await beforeTerminalRepairReturn();
                return false;
            }
            var canonicalRepairErrors = errors
                .Where(issue => IsCanonicalStateRepairIssue(issue) &&
                                !IsPlayerFacingNeutralActorMemoryRepairIssue(issue))
                .ToArray();
            if (canonicalRepairErrors.Length > 0)
            {
                outputFreshnessRepairErrors = MergeCanonicalRepairErrors(
                    outputFreshnessRepairErrors,
                    canonicalRepairErrors);
                lastCanonicalRepairStartedAtUtc = repairStartedAtUtc;
            }

            if (outputFreshnessRepairErrors is { Count: > 0 } &&
                lastCanonicalRepairStartedAtUtc.HasValue)
            {
                var unobservableFallbackUtc = canonicalRepairErrors.Length > 0
                    ? DateTime.UtcNow
                    : lastCanonicalRepairBoundaryUtc;
                lastCanonicalRepairBoundaryUtc = ResolveCanonicalRepairOutputFreshnessBoundaryUtc(
                    outputFreshnessRepairErrors,
                    lastCanonicalRepairStartedAtUtc.Value,
                    unobservableFallbackUtc);
            }
        }
    }
}
