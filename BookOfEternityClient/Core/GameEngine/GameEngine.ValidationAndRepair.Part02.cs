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

public partial class GameEngine
{

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
            }
            else
            {
                await CleanupAcceptedTurnCommandSurfacesAsync();
            }
            await RefreshRuntimeStateAsync();
            if (treatmentResourcePublicationTransaction is null)
            {
                _acceptedTurnSpiritualOutput = canonicalRefresh.SpiritualWoundOutput;
                _acceptedTurnSpiritualConflictValidation = canonicalRefresh.SpiritualConflictValidation;
                _acceptedTurnWoundNotifications = canonicalRefresh.WoundNotifications
                    .Select(static value => value with
                    {
                        Text = value.Text with { }
                    }).ToArray();
                return CompleteAcceptedTurnWithoutTreatmentResourcePublication();
            }

            var treatmentResourcePublicationCompletion =
                await treatmentResourcePublicationTransaction.CompleteAsync(_fs);
            if (!treatmentResourcePublicationCompletion.IsValid ||
                treatmentResourcePublicationCompletion.Issues.Count != 0 ||
                treatmentResourcePublicationCompletion.ChangedCount != 1 ||
                treatmentResourcePublicationCompletion.Outcome !=
                    MortalWoundTreatmentPublicationTransactionOutcome.Finalized)
            {
                var settlement =
                    await SettleTreatmentResourcePublicationCompletionFailureAsync(
                        treatmentResourcePublicationTransaction);
                return MapTreatmentPublicationSettlementToDisposition(
                    settlement);
            }
            _acceptedTurnWoundNotifications = canonicalRefresh.WoundNotifications
                .Select(static value => value with
                {
                    Text = value.Text with { }
                }).ToArray();
            return AcceptedTurnValidationDisposition.Accepted;
        }
    }
}
