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
    private async Task<AcceptedTurnValidationDisposition>
        ValidateAcceptedTurnOutcomeWithRepairLoopAsync(
        string source,
        ValidatedPendingTurnSnapshotContext? activeSnapshotContext,
        RollbackSnapshot? rollbackSnapshot,
        int expectedTurn,
        ProgressionControl? progressionControl)
    {
        _acceptedTurnWoundNotifications = Array.Empty<WoundPlayerNotification>();
        _acceptedTurnSpiritualOutput = null;
        _acceptedTurnSpiritualConflictValidation = null;
        var finalizedTreatmentReplayQuarantineFailure =
            await TryQuarantineFinalizedTreatmentReplayAsync(expectedTurn);
        if (finalizedTreatmentReplayQuarantineFailure.HasValue)
            return finalizedTreatmentReplayQuarantineFailure.Value;
        var criticalRepairAttempt = 0;
        List<ValidationIssue>? lastCriticalRepairErrors = null;
        var lastCriticalRepairAttempt = 0;
        DateTime? lastCriticalRepairBoundaryUtc = null;
        DateTime? lastCriticalRepairStartedAtUtc = null;
        string? lastCriticalRepairSessionGeneration = null;
        IReadOnlyList<MortalLocationRepairRetryObligation>? mortalLocationRetryObligations = null;
        List<ValidationIssue>? mortalLocationRetryErrors = null;
        IReadOnlyList<RepairResubmissionPathObligation>? mortalLocationRequiredResubmissionPaths = null;
        IReadOnlyList<EffectRepairRetryObligation>? effectRetryObligations = null;
        List<ValidationIssue>? effectRetryErrors = null;
        IReadOnlyList<RepairResubmissionPathObligation>? effectRequiredResubmissionPaths = null;
        IReadOnlyList<ResourceRepairRetryObligation>? resourceRetryObligations = null;
        List<ValidationIssue>? resourceRetryErrors = null;
        IReadOnlyList<RepairResubmissionPathObligation>? resourceRequiredResubmissionPaths = null;
        IReadOnlyList<WoundRepairRetryObligation>? woundRepairRetryObligations = null;
        List<ValidationIssue>? woundRepairRetryErrors = null;
        IReadOnlyList<RepairResubmissionPathObligation>? woundRequiredResubmissionPaths = null;
        byte[]? pendingResolutionRepairCheckpoint = null;

        while (true)
        {
            await EnsureClientOwnedSystemFilesHealthyAsync();
            var spiritualAdmission = SpiritualContinuationAdmission.Admitted;
            UpstreamAcceptedTurnValidation upstreamValidation;
            using (var upstreamSnapshotScope = _validator.UsePrevalidatedPendingTurnSnapshotScope(activeSnapshotContext?.Manifest))
                upstreamValidation = await CollectAcceptedTurnUpstreamRawStateIssuesAsync();
            if (upstreamValidation.RejectSpiritualBoundary)
            {
                LogSpiritualContinuationIssues(upstreamValidation.Issues);
                return AcceptedTurnValidationDisposition.TerminalRejected;
            }
            var rawIssues = upstreamValidation.Issues.ToList();
            if (!rawIssues.Any(issue => issue.Severity == IssueSeverity.Error))
            {
                spiritualAdmission = await ContinueAcceptedSpiritualTurnAsync(source, expectedTurn, activeSnapshotContext);
                if (spiritualAdmission == SpiritualContinuationAdmission.Rejected)
                    return AcceptedTurnValidationDisposition.TerminalRejected;
                if (spiritualAdmission == SpiritualContinuationAdmission.RetryableHeld)
                    return AcceptedTurnValidationDisposition.SpiritualContinuationHeldBlocked;
                var freshSnapshot = await ResolveActivePendingTurnSnapshotContextAsync();
                activeSnapshotContext = freshSnapshot.Context;
            }
            using var pendingSnapshotScope = _validator.UsePrevalidatedPendingTurnSnapshotScope(activeSnapshotContext?.Manifest);
            rawIssues.AddRange(await _validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
            if (spiritualAdmission == SpiritualContinuationAdmission.RequiresInitialResourceValidation &&
                !rawIssues.Any(issue => issue.Severity == IssueSeverity.Error))
            {
                _logger.LogWarning("Original spiritual capture failed without ordinary resource errors; publication remains rejected.");
                return AcceptedTurnValidationDisposition.TerminalRejected;
            }
            if (mortalLocationRetryObligations is { Count: > 0 } &&
                (!await HasExactMortalLocationRepairResubmissionAsync(mortalLocationRetryObligations) ||
                 rollbackSnapshot == null ||
                 mortalLocationRequiredResubmissionPaths == null ||
                 !await AreRollbackTrackedPathsResubmittedForRepairSessionAsync(
                     rollbackSnapshot,
                     mortalLocationRequiredResubmissionPaths,
                     lastCriticalRepairSessionGeneration!)))
            {
                foreach (var retryError in mortalLocationRetryErrors!)
                {
                    if (!rawIssues.Any(issue =>
                            string.Equals(issue.Code, retryError.Code, StringComparison.Ordinal) &&
                            string.Equals(issue.FilePath, retryError.FilePath, StringComparison.Ordinal) &&
                            string.Equals(issue.Actor, retryError.Actor, StringComparison.Ordinal)))
                    {
                        rawIssues.Add(retryError);
                    }
                }
            }
            if (effectRetryObligations is { Count: > 0 } &&
                (!await HasExactEffectRepairResubmissionAsync(effectRetryObligations) ||
                 rollbackSnapshot == null ||
                 effectRequiredResubmissionPaths == null ||
                 !await AreRollbackTrackedPathsResubmittedForRepairSessionAsync(
                     rollbackSnapshot,
                     effectRequiredResubmissionPaths,
                     lastCriticalRepairSessionGeneration!)))
            {
                foreach (var retryError in effectRetryErrors!)
                {
                    if (!rawIssues.Any(issue =>
                            string.Equals(issue.Code, retryError.Code, StringComparison.Ordinal) &&
                            string.Equals(issue.FilePath, retryError.FilePath, StringComparison.Ordinal) &&
                            string.Equals(issue.Actor, retryError.Actor, StringComparison.Ordinal)))
                    {
                        rawIssues.Add(retryError);
                    }
                }
            }
            if (resourceRetryObligations is { Count: > 0 } &&
                (!await HasExactResourceRepairResubmissionAsync(resourceRetryObligations) ||
                 rollbackSnapshot == null ||
                 resourceRequiredResubmissionPaths == null ||
                 !await AreRollbackTrackedPathsResubmittedForRepairSessionAsync(
                     rollbackSnapshot,
                     resourceRequiredResubmissionPaths,
                     lastCriticalRepairSessionGeneration!)))
            {
                foreach (var retryError in resourceRetryErrors!)
                {
                    if (!rawIssues.Any(issue =>
                            string.Equals(issue.Code, retryError.Code, StringComparison.Ordinal) &&
                            string.Equals(issue.FilePath, retryError.FilePath, StringComparison.Ordinal) &&
                            string.Equals(issue.Actor, retryError.Actor, StringComparison.Ordinal)))
                    {
                        rawIssues.Add(retryError);
                    }
                }
            }
            if (woundRepairRetryObligations is { Count: > 0 } &&
                (!await HasExactWoundRepairResubmissionAsync(
                     woundRepairRetryObligations) ||
                 rollbackSnapshot == null ||
                 woundRequiredResubmissionPaths == null ||
                 !await AreRollbackTrackedPathsResubmittedForRepairSessionAsync(
                     rollbackSnapshot,
                     woundRequiredResubmissionPaths,
                     lastCriticalRepairSessionGeneration!)))
            {
                foreach (var retryError in woundRepairRetryErrors!)
                {
                    if (!rawIssues.Any(issue =>
                            string.Equals(issue.Code, retryError.Code, StringComparison.Ordinal) &&
                            string.Equals(issue.FilePath, retryError.FilePath, StringComparison.Ordinal) &&
                            string.Equals(issue.Actor, retryError.Actor, StringComparison.Ordinal)))
                    {
                        rawIssues.Add(WoundAcceptedTurnData.CloneIssue(retryError));
                    }
                }
            }
            var rawErrors = PrioritizeValidationErrors(rawIssues.Where(i => i.Severity == IssueSeverity.Error)).ToList();
            if (rawErrors.Count > 0)
            {
                criticalRepairAttempt++;
                _logger.LogError(
                    "Critical accepted-turn raw state corruption after {Source}: {Count} errors",
                    source,
                    rawErrors.Count);

                lastCriticalRepairErrors = rawErrors;
                lastCriticalRepairAttempt = criticalRepairAttempt;
                lastCriticalRepairSessionGeneration = await CaptureCurrentSessionGenerationAsync();
                var rawRepairStartedAtUtc = DateTime.UtcNow;
                var actionableLocationPackets = MortalLocationRepairPacketBuilder.Build(rawErrors);
                if (actionableLocationPackets.Count > 0 && mortalLocationRetryObligations == null)
                {
                    mortalLocationRetryObligations = actionableLocationPackets
                        .Select(packet => new MortalLocationRepairRetryObligation(
                            packet.CanonicalActorNames.Single(),
                            packet.Route,
                            packet.ExpectedMaterializationId))
                        .Distinct()
                        .ToArray();
                    mortalLocationRetryErrors = rawErrors.ToList();
                    if (HasRollbackCapability(rollbackSnapshot))
                    {
                        mortalLocationRequiredResubmissionPaths =
                            await CaptureChangedRollbackTrackedPathsForRepairSessionAsync(
                                rollbackSnapshot!,
                                lastCriticalRepairSessionGeneration);
                    }
                }
                var actionableEffectPackets = EffectRepairPacketBuilder.Build(
                    rawErrors,
                    HasRollbackCapability(rollbackSnapshot));
                if (actionableEffectPackets.Count > 0 && effectRetryObligations == null)
                {
                    effectRetryObligations = actionableEffectPackets
                        .Select(packet =>
                        {
                            var correction = packet.ExactFieldCorrections.Single();
                            return new EffectRepairRetryObligation(
                                packet.Actor,
                                packet.RawCoordinate,
                                packet.ExpectedSource.DeepClone().AsObject(),
                                packet.ExpectedTarget.DeepClone().AsObject(),
                                packet.ExpectedDefinitionKey,
                                packet.ExpectedEventRef.DeepClone().AsObject(),
                                correction.ExpectedValueJson);
                        })
                        .ToArray();
                    effectRetryErrors = rawErrors.ToList();
                    effectRequiredResubmissionPaths =
                        await CaptureChangedRollbackTrackedPathsForRepairSessionAsync(
                            rollbackSnapshot!,
                        lastCriticalRepairSessionGeneration);
                }
                var actionableResourcePackets = ResourceRepairPacketBuilder.Build(rawErrors);
                if (actionableResourcePackets.Count > 0 && resourceRetryObligations == null)
                {
                    var capturedResourceObligations =
                        await CaptureResourceRepairRetryObligationsAsync(
                            actionableResourcePackets);
                    if (capturedResourceObligations.Count != actionableResourcePackets.Count)
                    {
                        rawErrors.Add(new ValidationIssue(
                            ResourceMaterializationContract.CommandPath,
                            IssueSeverity.Error,
                            "Resource repair could not bind the rejected command to one exact retry authority.",
                            code: "resource_repair_retry_authority_unavailable",
                            actor: "Client",
                            section: "UnifiedResourceAuthority",
                            expected: "one strict duplicate-free rejected resource command root",
                            actual: "missing, malformed, ambiguous, or changed before repair capture",
                            category: IssueCategory.StateConsistency));
                    }
                    else
                    {
                        resourceRetryObligations = capturedResourceObligations;
                        resourceRetryErrors = rawErrors.ToList();
                        if (HasRollbackCapability(rollbackSnapshot))
                        {
                            resourceRequiredResubmissionPaths =
                                await CaptureChangedRollbackTrackedPathsForRepairSessionAsync(
                                    rollbackSnapshot!,
                                    lastCriticalRepairSessionGeneration);
                        }
                    }
                }
                var actionableWoundPackets = WoundRepairPacketBuilder.Build(rawErrors);
                if (actionableWoundPackets.Count > 0 &&
                    woundRepairRetryObligations == null)
                {
                    var capturedWoundObligations =
                        await CaptureWoundRepairRetryObligationsAsync(
                            actionableWoundPackets);
                    if (capturedWoundObligations.Count !=
                        actionableWoundPackets.Count)
                    {
                        rawErrors.Add(BuildWoundRepairRetryAuthorityIssue());
                    }
                    else
                    {
                        woundRepairRetryObligations = capturedWoundObligations;
                        woundRepairRetryErrors = rawErrors
                            .Select(WoundAcceptedTurnData.CloneIssue)
                            .ToList();
                        if (HasRollbackCapability(rollbackSnapshot))
                        {
                            woundRequiredResubmissionPaths =
                                await CaptureChangedRollbackTrackedPathsForRepairSessionAsync(
                                    rollbackSnapshot!,
                                    lastCriticalRepairSessionGeneration);
                        }
                    }
                }
                if (!await WaitForContractRepairAsync(
                        source,
                        rawErrors,
                        criticalRepairAttempt,
                        rollbackSnapshot,
                        lastCriticalRepairSessionGeneration,
                        mortalLocationRetryObligations,
                        mortalLocationRequiredResubmissionPaths,
                        effectRetryObligations,
                        effectRequiredResubmissionPaths,
                        resourceRetryObligations,
                        resourceRequiredResubmissionPaths,
                        woundRepairRetryObligations,
                        woundRequiredResubmissionPaths,
                        pendingResolutionRepairCheckpoint: pendingResolutionRepairCheckpoint))
                {
                    return await SettlePreCanonicalTreatmentPublicationBeforeTerminalRollbackAsync(
                        rollbackSnapshot,
                        "validation_failed");
                }
                lastCriticalRepairStartedAtUtc = rawRepairStartedAtUtc;
                lastCriticalRepairBoundaryUtc = ResolveCanonicalRepairOutputFreshnessBoundaryUtc(
                    rawErrors,
                    rawRepairStartedAtUtc);

                continue;
            }

            AcceptedTurnCanonicalRefreshResult canonicalRefresh;
            try
            {
                canonicalRefresh = await RefreshAcceptedTurnCanonicalStateForValidationAsync(
                    expectedTurn,
                    activeSnapshotContext);
            }
            catch (CompensatedTreatmentPublicationException exception)
            {
                _logger.LogWarning(
                    exception.InnerException ?? exception,
                    "Held treatment publication failed after canonical publication and was compensated with {Outcome}.",
                    exception.Outcome);
                return MapTreatmentPublicationSettlementToDisposition(
                    exception.Outcome);
            }
            catch (UnsafeTreatmentPublicationSettlementException)
            {
                throw;
            }
            catch (SessionReplacedException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                await FailClosedAcceptedTurnCanonicalRefreshAsync(
                    source,
                    exception,
                    rollbackSnapshot);
                return AcceptedTurnValidationDisposition.TerminalRejected;
            }
            await using var treatmentResourcePublicationTransaction =
                canonicalRefresh.TreatmentResourcePublicationTransaction;
            if (!canonicalRefresh.BaselineUsable)
            {
                criticalRepairAttempt++;
                var baselineErrors = new List<ValidationIssue>
                {
                    new(
                        "game_state/control/pending_turn_snapshot.json",
                        IssueSeverity.Error,
                        "Accepted-turn canonical materialization requires a readable validated pending-turn snapshot baseline.",
                        code: "accepted_turn_invalid_snapshot_baseline",
                        section: "AcceptedTurnCanonicalState",
                        expected: "usable current pending turn snapshot manifest with detached authority and hash-validated canonical baseline files",
                        actual: "validated snapshot baseline is missing, detached-authority-invalid, modified, structurally invalid, or mismatched to the active request context",
                        repairHint: "Восстанови current pending_turn_snapshot.json, detached snapshot authority и canonical snapshot files без tampering; accepted-turn canonical validation должна читать pre-turn baseline только из validated snapshot authority.")
                };

                _logger.LogError(
                    "Critical accepted-turn canonical baseline authority failure after {Source}: {Count} errors",
                    source,
                    baselineErrors.Count);

                lastCriticalRepairErrors = baselineErrors;
                lastCriticalRepairAttempt = criticalRepairAttempt;
                lastCriticalRepairSessionGeneration = await CaptureCurrentSessionGenerationAsync();
                var baselineRepairStartedAtUtc = DateTime.UtcNow;
                await CompensateTreatmentResourcePublicationBeforeRepairAsync(
                    treatmentResourcePublicationTransaction);
                if (!await WaitForContractRepairAsync(
                        source,
                        baselineErrors,
                        criticalRepairAttempt,
                        rollbackSnapshot,
                        lastCriticalRepairSessionGeneration,
                        pendingResolutionRepairCheckpoint: pendingResolutionRepairCheckpoint))
                {
                    await ReleaseTreatmentResourcePublicationBeforeTerminalRollbackAsync(
                        treatmentResourcePublicationTransaction,
                        rollbackSnapshot,
                        "validation_failed");
                    return AcceptedTurnValidationDisposition.TerminalRejected;
                }
                lastCriticalRepairStartedAtUtc = baselineRepairStartedAtUtc;
                lastCriticalRepairBoundaryUtc = ResolveCanonicalRepairOutputFreshnessBoundaryUtc(
                    baselineErrors,
                    baselineRepairStartedAtUtc);

                continue;
            }

            var postSealErrors = PrioritizeValidationErrors(
                    canonicalRefresh.PostSealIssues.Where(issue =>
                        issue.Severity == IssueSeverity.Error))
                .ToList();
            if (postSealErrors.Count > 0)
            {
                criticalRepairAttempt++;
                _logger.LogError(
                    "Critical accepted-turn post-seal validation failure after {Source}: {Count} errors",
                    source,
                    postSealErrors.Count);

                lastCriticalRepairErrors = postSealErrors;
                lastCriticalRepairAttempt = criticalRepairAttempt;
                lastCriticalRepairSessionGeneration = await CaptureCurrentSessionGenerationAsync();
                var postSealRepairStartedAtUtc = DateTime.UtcNow;
                var actionableWoundPackets = WoundRepairPacketBuilder.Build(postSealErrors);
                if (actionableWoundPackets.Count > 0 &&
                    woundRepairRetryObligations == null)
                {
                    var capturedWoundObligations =
                        await CaptureWoundRepairRetryObligationsAsync(
                            actionableWoundPackets);
                    if (capturedWoundObligations.Count !=
                        actionableWoundPackets.Count)
                    {
                        postSealErrors.Add(BuildWoundRepairRetryAuthorityIssue());
                    }
                    else
                    {
                        woundRepairRetryObligations = capturedWoundObligations;
                        woundRepairRetryErrors = postSealErrors
                            .Select(WoundAcceptedTurnData.CloneIssue)
                            .ToList();
                        if (HasRollbackCapability(rollbackSnapshot))
                        {
                            woundRequiredResubmissionPaths =
                                await CaptureChangedRollbackTrackedPathsForRepairSessionAsync(
                                    rollbackSnapshot!,
                                    lastCriticalRepairSessionGeneration);
                        }
                    }
                }
                await CompensateTreatmentResourcePublicationBeforeRepairAsync(
                    treatmentResourcePublicationTransaction);
                if (!await WaitForContractRepairAsync(
                        source,
                        postSealErrors,
                        criticalRepairAttempt,
                        rollbackSnapshot,
                        lastCriticalRepairSessionGeneration,
                        woundRepairRetryObligations: woundRepairRetryObligations,
                        woundRequiredResubmissionPaths: woundRequiredResubmissionPaths,
                        pendingResolutionRepairCheckpoint: pendingResolutionRepairCheckpoint))
                {
                    await ReleaseTreatmentResourcePublicationBeforeTerminalRollbackAsync(
                        treatmentResourcePublicationTransaction,
                        rollbackSnapshot,
                        "validation_failed");
                    return AcceptedTurnValidationDisposition.TerminalRejected;
                }

                lastCriticalRepairStartedAtUtc = postSealRepairStartedAtUtc;
                lastCriticalRepairBoundaryUtc = ResolveCanonicalRepairOutputFreshnessBoundaryUtc(
                    postSealErrors,
                    postSealRepairStartedAtUtc);
                continue;
            }

            if (canonicalRefresh.MechanicsPlan is { AwaitsPendingResolution: true } pendingPlan)
            {
                if (!pendingPlan.PendingAfterImages.TryGetValue(
                        ResourcePendingResolutionState.PendingPath,
                        out var pendingAfterImage) ||
                    pendingAfterImage == null)
                {
                    throw new InvalidDataException(
                        "Pending accepted mechanics plan has no technical pending checkpoint.");
                }
                pendingResolutionRepairCheckpoint =
                    await _fs.ReadFileBytesAsync(ResourcePendingResolutionState.PendingPath) ??
                    throw new InvalidDataException(
                        "Published pending accepted mechanics checkpoint is missing.");
                criticalRepairAttempt++;
                var pendingErrors = new List<ValidationIssue>
                {
                    BuildBoundedResourceResolutionResubmissionIssue(pendingPlan)
                };
                lastCriticalRepairErrors = pendingErrors;
                lastCriticalRepairAttempt = criticalRepairAttempt;
                lastCriticalRepairSessionGeneration = await CaptureCurrentSessionGenerationAsync();
                var pendingStartedAtUtc = DateTime.UtcNow;
                await CompensateTreatmentResourcePublicationBeforeRepairAsync(
                    treatmentResourcePublicationTransaction);
                if (!await WaitForBoundedPendingResolutionResubmissionAsync(
                        source,
                        pendingErrors,
                        criticalRepairAttempt,
                        rollbackSnapshot,
                        lastCriticalRepairSessionGeneration,
                        pendingResolutionRepairCheckpoint))
                {
                    await ReleaseTreatmentResourcePublicationBeforeTerminalRollbackAsync(
                        treatmentResourcePublicationTransaction,
                        rollbackSnapshot,
                        "validation_failed");
                    return AcceptedTurnValidationDisposition.TerminalRejected;
                }
                lastCriticalRepairStartedAtUtc = pendingStartedAtUtc;
                lastCriticalRepairBoundaryUtc = ResolveCanonicalRepairOutputFreshnessBoundaryUtc(
                    pendingErrors,
                    pendingStartedAtUtc);
                continue;
            }

            if (canonicalRefresh.MechanicsPlan is { } acceptedPlan &&
                acceptedPlan.PendingAfterImages.TryGetValue(
                    ResourcePendingResolutionState.PendingPath,
                    out var acceptedPendingAfterImage))
            {
                pendingResolutionRepairCheckpoint = acceptedPendingAfterImage == null
                    ? null
                    : await _fs.ReadFileBytesAsync(
                        ResourcePendingResolutionState.PendingPath) ??
                      throw new InvalidDataException(
                          "Published accepted mechanics pending checkpoint is missing.");
            }

            await EnsureClientOwnedSystemFilesHealthyAsync();
            var canonicalIssues = await _criticalStateHealth.ValidateCriticalCanonicalStateAsync();
            var canonicalErrors = PrioritizeValidationErrors(canonicalIssues.Where(i => i.Severity == IssueSeverity.Error)).ToList();
            if (canonicalErrors.Count > 0)
            {
                criticalRepairAttempt++;
                _logger.LogError(
                    "Critical accepted-turn canonical state corruption after {Source}: {Count} errors",
                    source,
                    canonicalErrors.Count);

                lastCriticalRepairErrors = canonicalErrors;
                lastCriticalRepairAttempt = criticalRepairAttempt;
                lastCriticalRepairSessionGeneration = await CaptureCurrentSessionGenerationAsync();
                var canonicalRepairStartedAtUtc = DateTime.UtcNow;
                await CompensateTreatmentResourcePublicationBeforeRepairAsync(
                    treatmentResourcePublicationTransaction);
                if (!await WaitForContractRepairAsync(
                        source,
                        canonicalErrors,
                        criticalRepairAttempt,
                        rollbackSnapshot,
                        lastCriticalRepairSessionGeneration,
                        pendingResolutionRepairCheckpoint: pendingResolutionRepairCheckpoint))
                {
                    await ReleaseTreatmentResourcePublicationBeforeTerminalRollbackAsync(
                        treatmentResourcePublicationTransaction,
                        rollbackSnapshot,
                        "validation_failed");
                    return AcceptedTurnValidationDisposition.TerminalRejected;
                }
                lastCriticalRepairStartedAtUtc = canonicalRepairStartedAtUtc;
                lastCriticalRepairBoundaryUtc = ResolveCanonicalRepairOutputFreshnessBoundaryUtc(
                    canonicalErrors,
                    canonicalRepairStartedAtUtc);

                continue;
            }

            if (lastCriticalRepairErrors is { Count: > 0 } &&
                lastCriticalRepairStartedAtUtc.HasValue)
            {
                lastCriticalRepairBoundaryUtc = ResolveCanonicalRepairOutputFreshnessBoundaryUtc(
                    lastCriticalRepairErrors,
                    lastCriticalRepairStartedAtUtc.Value);
            }

            var treatmentPublicationCompensatedForRepair = false;
            using var completedConflictValidationScope = _validator.UseCompletedSpiritualConflictValidationScope(
                canonicalRefresh.SpiritualConflictValidation);
            var currentStateValid = await ValidateCurrentGameStateOrShowErrorsAsync(
                    source,
                    rollbackSnapshot,
                    progressionControl,
                    allowRepairLoop: true,
                    initialCanonicalRepairBoundaryUtc: lastCriticalRepairBoundaryUtc,
                    initialCanonicalRepairErrors: lastCriticalRepairErrors,
                    initialCanonicalRepairStartedAtUtc: lastCriticalRepairStartedAtUtc,
                    pendingResolutionRepairCheckpoint: pendingResolutionRepairCheckpoint,
                    beforeRepairMutation: async () =>
                    {
                        treatmentPublicationCompensatedForRepair =
                            await CompensateTreatmentResourcePublicationBeforeRepairAsync(
                                treatmentResourcePublicationTransaction);
                    },
                    beforeTerminalRepairReturn: () =>
                        ReleaseTreatmentResourcePublicationBeforeTerminalRollbackAsync(
                            treatmentResourcePublicationTransaction,
                            rollbackSnapshot,
                            "validation_failed"),
                    applyProgressionOnSuccess:
                        treatmentResourcePublicationTransaction is null,
                    treatmentResourcePublicationTransaction:
                        treatmentResourcePublicationTransaction);
            if (!currentStateValid)
            {
                return AcceptedTurnValidationDisposition.TerminalRejected;
            }
            if (treatmentPublicationCompensatedForRepair)
                continue;

            if (treatmentResourcePublicationTransaction is not null &&
                progressionControl is not null)
            {
                var progressionAdvance = await treatmentResourcePublicationTransaction
                    .AdvancePublishedAgreementWithProgressionAsync(
                        _fs,
                        _progressionSchedule,
                        progressionControl);
                if (!progressionAdvance.IsValid ||
                    progressionAdvance.Issues.Count != 0 ||
                    progressionAdvance.ChangedCount != 1 ||
                    progressionAdvance.Outcome !=
                        MortalWoundTreatmentPublicationTransactionOutcome
                            .PublishedAgreementAdvanced)
                {
                    var settlement =
                        await SettleTreatmentResourcePublicationCompletionFailureAsync(
                            treatmentResourcePublicationTransaction);
                    return MapTreatmentPublicationSettlementToDisposition(
                        settlement);
                }
            }

            if (lastCriticalRepairErrors is { Count: > 0 })
            {
                await AppendClearedValidationRepairTrajectoryAsync(
                    source,
                    lastCriticalRepairErrors,
                    lastCriticalRepairAttempt,
                    lastCriticalRepairSessionGeneration!);
                await CleanupAcceptedTurnCommandSurfacesAsync(lastCriticalRepairSessionGeneration);
