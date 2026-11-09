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
    SpiritualContinuationHeldBlocked,

    /// <summary>Retains a completed Mortal publication and its actual original turn without accepting conflicting resubmission.</summary>
    MortalCompletedPublicationHeldBlocked,

    /// <summary>Retains the exact joint wave/original and blocks destructive outer rollback.</summary>
    MortalJointRepairPublicationHeldBlocked,
    /// <summary>Verified joint phase/projection prerequisites; only the named actual caller can finish its story/turn.</summary>
    MortalJointRepairCompletionReady,
    /// <summary>The joint owner already performed terminal rollback/removal and released the claim.</summary>
    MortalJointRepairTerminalReleased
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
    private ValidationService.GuardianMusingsPublicationCapture.GuardianMusingsCompletedValidation?
        _acceptedTurnGuardianMusingsValidation;

    private MortalWoundTreatmentResourcePublicationTransaction.CompletedItemValidation?
        _acceptedTurnCompletedTreatmentItems;

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

    /// <summary>Checks client-owned system files within the existing participating session operation.</summary>
    /// <param name="ordinaryEntry">Whether this is an ordinary session-entry check.</param>
    /// <returns>Completion of the unchanged health and repair path.</returns>
    internal Task EnsureClientOwnedSystemFilesHealthyAsync(bool ordinaryEntry = false)=>
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

    /// <summary>
    /// Validates current state and optionally coordinates repair, closing each source read proof before later checks or mutations.
    /// </summary>
    /// <param name="source">
    /// Validation origin; the console post-materialization origin uses a fresh pending-snapshot invocation.
    /// </param>
    /// <param name="rollbackSnapshot">
    /// Optional original rollback capability used by the existing rejection and repair paths.
    /// </param>
    /// <param name="progressionControl">
    /// Optional accepted-turn progression contract to validate and apply on success.
    /// </param>
    /// <param name="allowRepairLoop">
    /// Whether validation errors may enter the existing repair loop.
    /// </param>
    /// <param name="initialCanonicalRepairBoundaryUtc">
    /// Optional prior repair freshness boundary.
    /// </param>
    /// <param name="initialCanonicalRepairErrors">
    /// Optional prior errors whose player-facing outputs require freshness checks.
    /// </param>
    /// <param name="initialCanonicalRepairStartedAtUtc">
    /// Optional start time associated with the supplied prior repair errors.
    /// </param>
    /// <param name="pendingResolutionRepairCheckpoint">
    /// Optional original pending-resolution checkpoint passed to the repair route.
    /// </param>
    /// <param name="beforeRepairMutation">
    /// Optional callback before the first repair mutation, after game-state validation has closed.
    /// </param>
    /// <param name="beforeTerminalRepairReturn">
    /// Optional callback on the existing terminal rejection paths.
    /// </param>
    /// <param name="applyProgressionOnSuccess">
    /// Whether to apply the supplied progression contract after validation succeeds.
    /// </param>
    /// <param name="treatmentResourcePublicationTransaction">
    /// Optional transaction used to filter its exact already-published item issues.
    /// </param>
    /// <param name="guardianMusingsForRepair">
    /// Optional completed guardian-musings proof to recheck after genuine repair completion.
    /// </param>
    /// <returns>
    /// <see langword="true"/> after validation succeeds; <see langword="false"/> on terminal rejection or declined repair.
    /// </returns>
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
            treatmentResourcePublicationTransaction = null,
        ValidationService.GuardianMusingsPublicationCapture.GuardianMusingsCompletedValidation?
            guardianMusingsForRepair = null)
    {
        using var stateTiming = _fs.MeasureOperation("PlayerTurn.ValidateCurrentState.Total");
        var repairAttempt = 0;
        List<ValidationIssue>? lastRepairErrors = null;
        IReadOnlyCollection<ValidationIssue>? outputFreshnessRepairErrors = initialCanonicalRepairErrors;
        var lastRepairAttempt = 0;
        var lastCanonicalRepairBoundaryUtc = initialCanonicalRepairBoundaryUtc;
        DateTime? lastCanonicalRepairStartedAtUtc = initialCanonicalRepairStartedAtUtc;
        string? lastRepairSessionGeneration = null;
        var repairMutationPrepared = false;
        var repairResubmissionCompleted = false;

        while (true)
        {
            using (_fs.MeasureOperation("PlayerTurn.ValidateCurrentState.SystemHealth"))
                await EnsureClientOwnedSystemFilesHealthyAsync();
            // Install in this flow after the fresh read-only lease has closed.
            // Each genuine repair completion permits exactly one validation iteration.
            using var repairedMusingsScope = repairResubmissionCompleted && guardianMusingsForRepair is not null
                ? _validator.UseCompletedGuardianMusingsValidationScope(
                    await _validator.RecheckOriginalGuardianMusingsAfterRepairAsync(guardianMusingsForRepair))
                : null;
            repairResubmissionCompleted = false;
            List<ValidationIssue> issues;
#if DEBUG
            using (_fs.BrowserDiagnosticLogical(null, source))
#endif
            {
                using var rawStateTiming = _fs.MeasureOperation("PlayerTurn.ValidateCurrentState.GameState");
                issues = source == PostAcceptedMaterializedStateValidationSource && !_fs.HasBrowserOriginalOperation
                    ? await _validator.ValidatePostMaterializedGameStateAsync()
                    : await _validator.ValidateGameStateAsync();
            }
            if (RequiresAcceptedTurnPayloadValidation(source))
            {
                if (RequiresFreshNarrativePayload(source))
                    issues.AddRange(await _validator.ValidateAcceptedTurnNarrativePayloadAsync());
                issues.AddRange(await ReadMortalJointInterfaceAtGenericSlotAsync());
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

            if (errors.Count != 0)
                repairedMusingsScope?.Invalidate();

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
            repairResubmissionCompleted = true;
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

    /// <summary>
    /// Validates and publishes one accepted turn, preserving original authority through bounded repair and continuation.
    /// </summary>
    /// <param name="source">
    /// Caller label used for validation diagnostics.
    /// </param>
    /// <param name="activeSnapshotContext">
    /// Validated original snapshot, or <see langword="null"/> when it must be resolved from disk.
    /// </param>
    /// <param name="rollbackSnapshot">
    /// Original rollback capability, or <see langword="null"/> when rollback is unavailable.
    /// </param>
    /// <param name="expectedTurn">
    /// Exact original turn number to validate.
    /// </param>
    /// <param name="progressionControl">
    /// Original progression obligations, or <see langword="null"/> when no progression is expected.
    /// </param>
    /// <returns>
    /// Accepted after common publication and validation, or a terminal disposition requiring caller settlement.
    /// </returns>
    private sealed class MortalCompletedPublicationHoldException : Exception { }
    private sealed class MortalJointRepairPublicationHoldException : Exception { }
}
