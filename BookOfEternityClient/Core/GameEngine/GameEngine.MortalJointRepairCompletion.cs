using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    private static bool SameMortalJointOriginalFrame(ValidatedPendingTurnSnapshotContext? left,
        ValidatedPendingTurnSnapshotContext? right, int turn) =>
        left is not null && right is not null && left.TurnNumber == turn && right.TurnNumber == turn &&
        left.SessionId == right.SessionId && left.RequestId == right.RequestId &&
        left.Payload.ManifestPayloadHash == right.Payload.ManifestPayloadHash;

    private bool MatchesMortalJointRepairOutcomeFrame(ValidatedPendingTurnSnapshotContext? frame, int turn) =>
        SameMortalJointOriginalFrame(_mortalJointRepairCanonicalRefresh?.Frame, frame, turn);

    private async Task ValidateCurrentMortalJointCompletedProofAsync(ValidatedPendingTurnSnapshotContext frame)
    {
        using var namedCompletionTimer1 = _fs.MeasureOperation("JointCompletion.ValidateCurrentCompletedProofAsync");
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        using var namedCompletionTimer4 = _fs.MeasureOperation("JointCompletion.ValidateCurrentJointCompletionAsync");
        await MortalQteDecisionPublication.ValidateCurrentJointCompletionAsync(_fs, lease,
            frame.SessionId, frame.RequestId, frame.Payload.ManifestPayloadHash, frame.TurnNumber);
        namedCompletionTimer4?.Dispose();
    }

    // This entry runs before old finalized-treatment quarantine, raw admission,
    // staging or any scheduler. A fresh process reconstructs no accepted plan or
    // live claim from a marker: the entire current physical v2 proof is reread.
    private async Task<AcceptedTurnValidationDisposition?> TryReadMortalJointCompletedContinuationAsync(
        string source, ValidatedPendingTurnSnapshotContext? frame, int expectedTurn)
    {
        using var namedCompletionTimer2 = _fs.MeasureOperation("JointCompletion.TryReadCompletedContinuationAsync");
        if (frame is null || frame.TurnNumber != expectedTurn) return null;
        WoundAcceptedTurnOutputBindingResult? output;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            if (!await MortalQteDecisionPublication.IsCurrentJointCompletedFamilyAsync(_fs, lease,
                frame.SessionId, frame.RequestId, frame.Payload.ManifestPayloadHash, expectedTurn)) return null;
            // Scope failure preservation only AFTER the actual full physical
            // private-joint proof. Old v1 exception handling stays unchanged.
            _mortalJointRepairCanonicalRefresh = new(MortalJointRepairPublicationDisposition.Committed,
                null, Array.Empty<ValidationIssue>(), Frame: frame);
            output = await MortalQteDecisionPublication.TryBindExactJointContinuationAsync(_fs, lease,
                frame.SessionId, frame.RequestId, frame.Payload.ManifestPayloadHash, expectedTurn);
            if (output is null) throw new InvalidDataException("The private joint completion changed during its participating read.");
            _mortalJointRepairCanonicalRefresh = new(MortalJointRepairPublicationDisposition.Committed,
                null, output.Issues, Frame: frame);
        }
        return await ValidateMortalJointPostPublicationAsync(source, frame, _mortalJointRepairCanonicalRefresh!, output);
    }

    private async Task<AcceptedTurnValidationDisposition> ValidateMortalJointPostPublicationAsync(
        string source, ValidatedPendingTurnSnapshotContext frame, MortalJointRepairCanonicalRefresh publication,
        WoundAcceptedTurnOutputBindingResult? continuationOutput = null)
    {
        using var namedCompletionTimer3 = _fs.MeasureOperation("JointCompletion.ValidatePostPublicationAsync");
        if (publication.Disposition != MortalJointRepairPublicationDisposition.Committed ||
            !SameMortalJointOriginalFrame(publication.Frame, frame, frame.TurnNumber))
            return AcceptedTurnValidationDisposition.MortalJointRepairPublicationHeldBlocked;

        MortalQteDecisionPublication.JointProgressionPhaseCapability observedPhase;
        StoryService.MortalJointStoryProof? existingStory = null;
        WoundAcceptedTurnOutputBindingResult? appliedOutput = null;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            // Select and independently validate the physical phase BEFORE any
            // live-report validator. Applied phase validates its frozen original
            // control/report/recomputed pair and exact afterimages instead.
            using var namedCompletionTimer5 = _fs.MeasureOperation("JointCompletion.ReadObservedProgressionAsync");
            observedPhase = await MortalQteDecisionPublication.ReadJointProgressionPhaseAsync(_fs, lease,
                frame.SessionId, frame.RequestId, frame.Payload.ManifestPayloadHash, frame.TurnNumber);
            namedCompletionTimer5?.Dispose();
            if (observedPhase.Phase == "applied")
            {
                var story = await _storyService.ReadMortalJointStoryAsync(lease, observedPhase);
                if (story.Disposition is not (StoryService.MortalJointStoryDisposition.OriginalUnappended or
                    StoryService.MortalJointStoryDisposition.AlreadyAppended))
                    throw new InvalidDataException("joint_postcommon_story_source_refused: " + story.Failure);
                existingStory = story.Proof;
                appliedOutput = await MortalQteDecisionPublication.TryBindExactJointContinuationAsync(_fs, lease,
                    frame.SessionId, frame.RequestId, frame.Payload.ManifestPayloadHash, frame.TurnNumber);
            }
        }
        var issues = publication.Issues.ToList();
        WoundAcceptedTurnOutputBindingResult output;
        if (observedPhase.Phase == "unapplied" && publication.Plan is { } plan)
        {
            using var namedCompletionTimer6 = _fs.MeasureOperation("JointCompletion.ValidatePostPublicationAuthorityAsync");
            issues.AddRange(await ValidateAcceptedWoundPostPublicationAuthorityAsync(plan, frame));
            namedCompletionTimer6?.Dispose();
            using var namedCompletionTimer7 = _fs.MeasureOperation("JointCompletion.BindPublishedOutputAsync");
            output = await BindPublishedAcceptedWoundOutputAsync(plan);
            namedCompletionTimer7?.Dispose();
        }
        else
            output = appliedOutput ?? continuationOutput ?? throw new InvalidDataException("Cold joint continuation requires its actual physically bound output.");
        issues.AddRange(output.Issues);

        // Use the real ordinary READ APIs, not the repair helper: that helper
        // ensures files, deletes repair state and can apply progression. No
        // repair, reduction, scheduler, writer or old transaction runs here.
        using (var snapshotScope = _validator.UsePrevalidatedPendingTurnSnapshotScope(frame.Manifest))
        {
            using var namedCompletionTimer8 = _fs.MeasureOperation("JointCompletion.ValidateGameStateAsync");
            issues.AddRange(existingStory is null ? await _validator.ValidateGameStateAsync() :
                await _validator.ValidateMortalJointStoryGameStateAsync(existingStory));
            namedCompletionTimer8?.Dispose();
            using var namedCompletionTimer9 = _fs.MeasureOperation("JointCompletion.ValidateNarrativeReadOnlyAsync");
            issues.AddRange(await _validator.ValidateMortalJointAcceptedNarrativeReadOnlyAsync());
            namedCompletionTimer9?.Dispose();
            using var namedCompletionTimer10 = _fs.MeasureOperation("JointCompletion.ReadInterfaceAtLateSlotAsync");
            issues.AddRange(await ReadMortalJointInterfaceAtLateSlotAsync(frame));
            namedCompletionTimer10?.Dispose();
            using var namedCompletionTimer11 = _fs.MeasureOperation("JointCompletion.ValidateReasoningAsync");
            issues.AddRange(await _validator.ValidateAcceptedTurnReasoningAsync());
            namedCompletionTimer11?.Dispose();
            using var namedCompletionTimer12 = _fs.MeasureOperation("JointCompletion.ValidateSpecialActionOutcomesAsync");
            issues.AddRange(await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync());
            namedCompletionTimer12?.Dispose();
            using var namedCompletionTimer13 = _fs.MeasureOperation("JointCompletion.ValidateQteOfferAsync");
            issues.AddRange(await _validator.ValidateAcceptedTurnQteOfferAsync());
            namedCompletionTimer13?.Dispose();
            using var namedCompletionTimer14 = _fs.MeasureOperation("JointCompletion.ValidateCombatMaterializationAsync");
            issues.AddRange(await _validator.ValidateAcceptedTurnMortalCombatMaterializationAsync());
            namedCompletionTimer14?.Dispose();
            using var namedCompletionTimer15 = _fs.MeasureOperation("JointCompletion.ValidateLevelUpMaterializationAsync");
            issues.AddRange(await _validator.ValidateAcceptedTurnMortalLevelUpMaterializationAsync());
            namedCompletionTimer15?.Dispose();
            using var namedCompletionTimer16 = _fs.MeasureOperation("JointCompletion.ValidateMemoryLegacyAsync");
            issues.AddRange(await _validator.ValidatePendingMemoryLegacyApplicationAsync());
            namedCompletionTimer16?.Dispose();
            if (observedPhase.Phase == "unapplied")
            {
                if (frame.ProgressionControl is null) throw new InvalidDataException("joint_postcommon_original_control_missing");
                using var namedCompletionTimer17 = _fs.MeasureOperation("JointCompletion.ValidateProgressionScheduleAsync");
                issues.AddRange(await _progressionSchedule.ValidateAcceptedTurnOutcomeAsync(frame.ProgressionControl));
                namedCompletionTimer17?.Dispose();
            }
        }
        // The fresh physical full proof, never the diagnostic witness, brackets
        // canonical/narrative reads and rejects foreign work or footprint drift.
        await ValidateCurrentMortalJointCompletedProofAsync(frame);
        _mortalJointRepairCanonicalRefresh = publication with { Issues = issues.ToArray() };
        if (issues.Any(row => row.Severity == IssueSeverity.Error) || !output.Success)
        {
            _logger.LogError("Joint post-publication validation refused after {Source}: {Count} errors; committed evidence retained.",
                source, issues.Count(row => row.Severity == IssueSeverity.Error));
            return AcceptedTurnValidationDisposition.MortalJointRepairPublicationHeldBlocked;
        }
        using var namedCompletionFinalLease = _fs.MeasureOperation("JointCompletion.FinalProgressionLeaseAndProjection");
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            // Old lease-local values do not authorize this lease. The actual
            // complete physical owner is reconstructed, then constrained to the
            // exact phase observed before the read-only canonical/narrative pass.
            using var namedCompletionTimer18 = _fs.MeasureOperation("JointCompletion.ReadCurrentProgressionAsync");
            var current = await MortalQteDecisionPublication.ReadJointProgressionPhaseAsync(_fs, lease,
                frame.SessionId, frame.RequestId, frame.Payload.ManifestPayloadHash, frame.TurnNumber);
            namedCompletionTimer18?.Dispose();
            if (current.Phase != observedPhase.Phase || !current.MarkerBefore.AsSpan().SequenceEqual(observedPhase.MarkerBefore))
                throw new InvalidDataException("joint_postcommon_phase_changed_during_validation");
            using var namedCompletionTimer19 = _fs.MeasureOperation("JointCompletion.CommitProgressionPhaseAsync");
            var phaseResult = await MortalQteDecisionPublication.CommitJointProgressionPhaseAsync(_fs, lease, current);
            namedCompletionTimer19?.Dispose();
            if (phaseResult.Disposition is not (MortalQteDecisionPublication.JointProgressionPublicationDisposition.Applied or
                MortalQteDecisionPublication.JointProgressionPublicationDisposition.AlreadyApplied) || phaseResult.AppliedCapability is not { } applied)
            {
                _mortalJointRepairCanonicalRefresh = publication with { Issues = issues.Concat(phaseResult.Issues).ToArray() };
                _logger.LogWarning("Joint progression phase remained blocked after {Source}: {Disposition}; committed union retained.", source, phaseResult.Disposition);
                return AcceptedTurnValidationDisposition.MortalJointRepairPublicationHeldBlocked;
            }
            using var namedCompletionTimer20 = _fs.MeasureOperation("JointCompletion.WriteOrVerifyProjectionAsync");
            var projection = existingStory is null ? await _charService.WriteMortalJointProjectionAsync(lease, applied) :
                await _charService.VerifyMortalJointProjectionAsync(lease, applied);
            namedCompletionTimer20?.Dispose();
            if (projection.Disposition is not (CharacteristicsService.JointDerivedProjectionDisposition.WrittenAndVerified or
                CharacteristicsService.JointDerivedProjectionDisposition.ExistingVerified))
            {
                _logger.LogWarning("Joint derived projection refused after {Source}: {Disposition}: {Failure}; evidence retained.",
                    source, projection.Disposition, projection.Failure);
                return AcceptedTurnValidationDisposition.MortalJointRepairPublicationHeldBlocked;
            }
            using var namedCompletionTimer21 = _fs.MeasureOperation("JointCompletion.ValidateAppliedProgressionAsync");
            await applied.ValidateCurrentAsync(_fs, lease);
            namedCompletionTimer21?.Dispose();
        }
        namedCompletionFinalLease?.Dispose();
        // This is a dedicated prerequisite disposition, never generic Accepted.
        // The named actual caller must freshly prove story/projection/turn before
        // any advancement; terminal cleanup remains an independent owned step.
        return AcceptedTurnValidationDisposition.MortalJointRepairCompletionReady;
    }
}
