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
    private async Task<AcceptedTurnValidationDisposition>
        ValidateAcceptedTurnOutcomeWithRepairLoopAsync(
        string source,
        ValidatedPendingTurnSnapshotContext? activeSnapshotContext,
        RollbackSnapshot? rollbackSnapshot,
        int expectedTurn,
        ProgressionControl? progressionControl)
    {
        using var acceptedTiming = _fs.MeasureOperation("PlayerTurn.AcceptedValidation.Total");
        if (_mortalJointRepairCanonicalRefresh?.Frame is { } detachedFrame &&
            !SameMortalJointOriginalFrame(detachedFrame, activeSnapshotContext, expectedTurn))
            _mortalJointRepairCanonicalRefresh = null; // bookkeeping only; no physical signal/source cleanup
        MortalQteDecisionPublication.CurrentPublicationInspection entry;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            entry = activeSnapshotContext is null && !_fs.FileExists(lease, MortalQteDecisionPublication.StatePath)
                ? new(MortalQteDecisionPublication.CurrentPublicationState.NotCommitted, false, "no_active_completion")
                : await MortalQteDecisionPublication.InspectCurrentAsync(_fs, lease,
                    activeSnapshotContext?.SessionId ?? string.Empty, activeSnapshotContext?.RequestId ?? string.Empty,
                    activeSnapshotContext?.Payload.ManifestPayloadHash ?? string.Empty, expectedTurn);
        }
        if (entry.State == MortalQteDecisionPublication.CurrentPublicationState.IntegrityConflict ||
            entry.State == MortalQteDecisionPublication.CurrentPublicationState.VerifiedCompleted && !entry.MayResubmit)
        {
            _logger.LogWarning("Mortal completed publication retained before validation: {Reason}", entry.Reason);
            return AcceptedTurnValidationDisposition.MortalCompletedPublicationHeldBlocked;
        }
        async Task BeforeDestructiveRefusalAsync()
        {
            await using (var interfaceLease = await _fs.AcquireCanonicalWriteLeaseAsync())
                await RequireNoMortalJointInterfaceRestoreAsync(interfaceLease, blockSelectedMissing: true);
            if (_mortalJointRepairCanonicalRefresh is { } joint &&
                (MatchesMortalJointRepairOutcomeFrame(activeSnapshotContext, expectedTurn) || MatchesMortalJointRepairFrame(activeSnapshotContext, expectedTurn)))
            {
                // A current owning outcome blocks destructive fallback even when
                // evidence is uncertain. A diagnostic witness grants no authority:
                // re-read the complete v2 physical proof before reporting continuity.
                if (joint.Completed is not null)
                {
                    await using var jointCompletionLease = await _fs.AcquireCanonicalWriteLeaseAsync();
                    try { await joint.Completed.ValidateCurrentAsync(_fs, jointCompletionLease); }
                    catch (Exception error) when (error is InvalidDataException or IOException or InvalidOperationException or
                        System.Text.Json.JsonException or ArgumentException)
                    { _logger.LogError(error, "Joint post-publication evidence changed; destructive fallback remains blocked."); }
                }
                throw new MortalJointRepairPublicationHoldException();
            }
            if (entry.State != MortalQteDecisionPublication.CurrentPublicationState.VerifiedCompleted) return;
            // A remembered classification grants no authority: reread the original binding and
            // complete current footprint under the participating lease before any repair/rollback.
            await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
            var current = await MortalQteDecisionPublication.InspectCurrentAsync(_fs, lease,
                activeSnapshotContext!.SessionId, activeSnapshotContext.RequestId,
                activeSnapshotContext.Payload.ManifestPayloadHash, expectedTurn);
            _logger.LogWarning("Completed Mortal retry cannot enter destructive repair: {State}, {Reason}", current.State, current.Reason);
            throw new MortalCompletedPublicationHoldException();
        }
        try
        {
            var uncertainJointDiagnostic = MatchesMortalJointRepairOutcomeFrame(activeSnapshotContext, expectedTurn) &&
                _mortalJointRepairCanonicalRefresh?.Disposition == MortalJointRepairPublicationDisposition.PublicationUncertain;
            var mayInspectCompletedJoint = !uncertainJointDiagnostic;
            if (uncertainJointDiagnostic && activeSnapshotContext is { } currentFrame &&
                entry.State == MortalQteDecisionPublication.CurrentPublicationState.VerifiedCompleted && entry.MayResubmit)
            {
                await using var currentProofLease = await _fs.AcquireCanonicalWriteLeaseAsync();
                // Only an independently validated CURRENT child3 proof may
                // supersede this selected protocol's old diagnostic. Child1/v1
                // retains its existing uncertain-publication hold policy.
                mayInspectCompletedJoint = await MortalQteDecisionPublication.ReadCurrentJointInterfaceDataAsync(_fs,
                    currentProofLease, currentFrame.SessionId, currentFrame.RequestId,
                    currentFrame.Payload.ManifestPayloadHash, expectedTurn) is not null;
            }
            if (mayInspectCompletedJoint && entry.State == MortalQteDecisionPublication.CurrentPublicationState.VerifiedCompleted && entry.MayResubmit)
            {
                var jointContinuation = await TryReadMortalJointCompletedContinuationAsync(source, activeSnapshotContext, expectedTurn);
                if (jointContinuation.HasValue) return jointContinuation.Value;
            }
            var resumedInterface = await TryResumeMortalJointInterfaceBeforeCoreAsync(source, activeSnapshotContext, expectedTurn);
            if (resumedInterface.HasValue) return resumedInterface.Value;
            if (uncertainJointDiagnostic)
                return AcceptedTurnValidationDisposition.MortalJointRepairPublicationHeldBlocked;
            var result = await ValidateAcceptedTurnOutcomeWithRepairLoopCoreAsync(source, activeSnapshotContext, rollbackSnapshot,
                expectedTurn, progressionControl, BeforeDestructiveRefusalAsync);
            if (result is not (AcceptedTurnValidationDisposition.Accepted or AcceptedTurnValidationDisposition.MortalJointRepairCompletionReady))
                await BeforeDestructiveRefusalAsync();
            return result;
        }
        catch (MortalCompletedPublicationHoldException)
        {
            return AcceptedTurnValidationDisposition.MortalCompletedPublicationHeldBlocked;
        }
        catch (MortalJointRepairPublicationHoldException)
        {
            return AcceptedTurnValidationDisposition.MortalJointRepairPublicationHeldBlocked;
        }
        catch (Exception error) when ((MatchesMortalJointRepairOutcomeFrame(activeSnapshotContext, expectedTurn) ||
            MatchesMortalJointRepairFrame(activeSnapshotContext, expectedTurn)) &&
            error is CoordinatedStatePublicationUncertainException or InvalidDataException or IOException or
                InvalidOperationException or ArgumentException or System.Text.Json.JsonException or AggregateException)
        {
            // This checkpoint was minted from genuine source reconstruction before
            // restore. Failure grants no outer rollback or claim-loss repair. The
            // owning source/restore/publication boundary already settled known work
            // or retained uncertainty; preserve its current evidence for recovery.
            _logger.LogError(error, "The source-bound joint repair owner is blocked; outer rollback is suppressed.");
            return AcceptedTurnValidationDisposition.MortalJointRepairPublicationHeldBlocked;
        }
    }

    private async Task<AcceptedTurnValidationDisposition> ValidateAcceptedTurnOutcomeWithRepairLoopCoreAsync(
        string source, ValidatedPendingTurnSnapshotContext? activeSnapshotContext, RollbackSnapshot? rollbackSnapshot,
        int expectedTurn, ProgressionControl? progressionControl, Func<Task> beforeDestructiveRefusal)
    {
        using var coreTiming = _fs.MeasureOperation("PlayerTurn.AcceptedValidation.Core");
        _acceptedTurnWoundNotifications = Array.Empty<WoundPlayerNotification>();
        _acceptedTurnSpiritualOutput = null;
        _acceptedTurnSpiritualConflictValidation = null;
        _acceptedTurnGuardianMusingsValidation = null;
        _acceptedTurnCompletedTreatmentItems = null;
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
            using var iterationTiming = _fs.MeasureOperation("PlayerTurn.AcceptedValidation.Iteration");
            ExpireCurrentMortalJointNarrativeInvocation();
            _mortalJointInterfaceValidationIteration++;
            using (_fs.MeasureOperation("PlayerTurn.AcceptedValidation.SystemHealth"))
                await EnsureClientOwnedSystemFilesHealthyAsync();
            var spiritualAdmission = SpiritualContinuationAdmission.Admitted;
            UpstreamAcceptedTurnValidation upstreamValidation;
#if DEBUG
            using (_fs.BrowserDiagnosticLogical(null, "accepted-upstream-raw"))
#endif
            using (_fs.MeasureOperation("PlayerTurn.AcceptedValidation.UpstreamRawState"))
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
                using (_fs.MeasureOperation("PlayerTurn.AcceptedValidation.SpiritualContinuation"))
                    spiritualAdmission = await ContinueAcceptedSpiritualTurnAsync(source, expectedTurn, activeSnapshotContext);
                if (spiritualAdmission == SpiritualContinuationAdmission.Rejected)
                    return AcceptedTurnValidationDisposition.TerminalRejected;
                if (spiritualAdmission == SpiritualContinuationAdmission.RetryableHeld)
                    return AcceptedTurnValidationDisposition.SpiritualContinuationHeldBlocked;
                var freshSnapshot = await ResolveActivePendingTurnSnapshotContextAsync();
                activeSnapshotContext = freshSnapshot.Context;
            }
            using var pendingSnapshotScope = _validator.UsePrevalidatedPendingTurnSnapshotScope(activeSnapshotContext?.Manifest);
#if DEBUG
            using var diagnosticAcceptedCaller = _fs.BrowserDiagnosticLogical(null, "accepted-validation-loop");
#endif
            using (_fs.MeasureOperation("PlayerTurn.AcceptedValidation.ResourceAndTreatment"))
                rawIssues.AddRange(await ValidateResourceAndMortalGuaranteedTreatmentAsync(
                    !rawIssues.Any(static issue => issue.Severity == IssueSeverity.Error), activeSnapshotContext, expectedTurn));
            using var retryTiming = _fs.MeasureOperation("PlayerTurn.AcceptedValidation.CheckRetryObligations");
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
            retryTiming?.Dispose();
            if (rawErrors.Count > 0)
            {
                using var repairTiming = _fs.MeasureOperation("PlayerTurn.AcceptedValidation.PrepareAndWaitRepair");
                await beforeDestructiveRefusal();
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
                using var repairWaitTiming = _fs.MeasureOperation("PlayerTurn.AcceptedValidation.WaitForContractRepair");
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
                repairWaitTiming?.Dispose();
                lastCriticalRepairStartedAtUtc = rawRepairStartedAtUtc;
                lastCriticalRepairBoundaryUtc = ResolveCanonicalRepairOutputFreshnessBoundaryUtc(
                    rawErrors,
                    rawRepairStartedAtUtc);

                continue;
            }

            AcceptedTurnCanonicalRefreshResult canonicalRefresh;
            try
            {
                using var refreshTiming = _fs.MeasureOperation("PlayerTurn.AcceptedValidation.RefreshCanonicalState");
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
            catch (CoordinatedStatePublicationUncertainException)
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
                try
                {
                    await beforeDestructiveRefusal();
                }
                catch (MortalJointRepairPublicationHoldException)
                {
                    _logger.LogError(
                        exception,
                        "The source-bound joint repair owner is blocked; outer rollback is suppressed.");
                    throw;
                }
                await FailClosedAcceptedTurnCanonicalRefreshAsync(
                    source,
                    exception,
                    rollbackSnapshot);
                return AcceptedTurnValidationDisposition.TerminalRejected;
            }
            await using var treatmentResourcePublicationTransaction =
                canonicalRefresh.TreatmentResourcePublicationTransaction;
            if (canonicalRefresh.JointPublication is { } jointPublication)
            {
                if (activeSnapshotContext is null)
                    return AcceptedTurnValidationDisposition.MortalJointRepairPublicationHeldBlocked;
                // The known common batch/readback and original claim settlement
                // have returned with the issuing lease closed. A configured test
                // pause cannot throw through the publisher or apply progression.
                if (jointPublication.Disposition == MortalJointRepairPublicationDisposition.Committed)
                    await InvokeSessionFinalizationCheckpointAsync(SessionFinalizationCheckpoint.MortalJointCommonReadbackSettledBeforeProgression);
                return await ValidateMortalJointPostPublicationAsync(source, activeSnapshotContext, jointPublication);
            }
            try
            {
                if (!canonicalRefresh.BaselineUsable)
                {
                    await beforeDestructiveRefusal();
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
                    await beforeDestructiveRefusal();
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
                    await beforeDestructiveRefusal();
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
                    await beforeDestructiveRefusal();
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
                using var guardianMusingsValidationScope = _validator.UseCompletedGuardianMusingsValidationScope(
                    canonicalRefresh.GuardianMusingsValidation);
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
                            await beforeDestructiveRefusal();
                            guardianMusingsValidationScope.Invalidate();
                            treatmentPublicationCompensatedForRepair =
                                await CompensateTreatmentResourcePublicationBeforeRepairAsync(
                                    treatmentResourcePublicationTransaction);
                        },
                        beforeTerminalRepairReturn: async () =>
                        {
                            await beforeDestructiveRefusal();
                            await ReleaseTreatmentResourcePublicationBeforeTerminalRollbackAsync(
                                treatmentResourcePublicationTransaction, rollbackSnapshot, "validation_failed");
                        },
                        applyProgressionOnSuccess:
                            treatmentResourcePublicationTransaction is null,
                        treatmentResourcePublicationTransaction:
                            treatmentResourcePublicationTransaction,
                        guardianMusingsForRepair: canonicalRefresh.GuardianMusingsValidation);
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
                    canonicalRefresh = canonicalRefresh with
                    {
                        GuardianMusingsValidation = await CleanupAcceptedTurnCommandSurfacesAsync(
                            lastCriticalRepairSessionGeneration, canonicalRefresh.GuardianMusingsValidation)
                    };
                }
                else
                {
                    canonicalRefresh = canonicalRefresh with
                    {
                        GuardianMusingsValidation = await CleanupAcceptedTurnCommandSurfacesAsync(
                            guardianMusings: canonicalRefresh.GuardianMusingsValidation)
                    };
                }
                using var cleanedGuardianMusingsValidationScope = _validator.UseCompletedGuardianMusingsValidationScope(
                    canonicalRefresh.GuardianMusingsValidation);
                await RefreshRuntimeStateAsync();
                if (treatmentResourcePublicationTransaction is null)
                {
                    _acceptedTurnSpiritualOutput = canonicalRefresh.SpiritualWoundOutput;
                    _acceptedTurnSpiritualConflictValidation = canonicalRefresh.SpiritualConflictValidation;
                    _acceptedTurnGuardianMusingsValidation = canonicalRefresh.GuardianMusingsValidation;
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
                _acceptedTurnCompletedTreatmentItems = treatmentResourcePublicationTransaction.ReadCompletedItemValidation();
                _acceptedTurnWoundNotifications = canonicalRefresh.WoundNotifications
                    .Select(static value => value with
                    {
                        Text = value.Text with { }
                    }).ToArray();
                return AcceptedTurnValidationDisposition.Accepted;
            }
            catch (CoordinatedStatePublicationUncertainException uncertain)
            {
                treatmentResourcePublicationTransaction?.ObservePublicationUncertainty(uncertain);
                throw;
            }
        }
    }
}
