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
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        await MortalQteDecisionPublication.ValidateCurrentJointCompletionAsync(_fs, lease,
            frame.SessionId, frame.RequestId, frame.Payload.ManifestPayloadHash, frame.TurnNumber);
    }

    // This entry runs before old finalized-treatment quarantine, raw admission,
    // staging or any scheduler. A fresh process reconstructs no accepted plan or
    // live claim from a marker: the entire current physical v2 proof is reread.
    private async Task<AcceptedTurnValidationDisposition?> TryReadMortalJointCompletedContinuationAsync(
        string source, ValidatedPendingTurnSnapshotContext? frame, int expectedTurn)
    {
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
            observedPhase = await MortalQteDecisionPublication.ReadJointProgressionPhaseAsync(_fs, lease,
                frame.SessionId, frame.RequestId, frame.Payload.ManifestPayloadHash, frame.TurnNumber);
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
            issues.AddRange(await ValidateAcceptedWoundPostPublicationAuthorityAsync(plan, frame));
            output = await BindPublishedAcceptedWoundOutputAsync(plan);
        }
        else
            output = appliedOutput ?? continuationOutput ?? throw new InvalidDataException("Cold joint continuation requires its actual physically bound output.");
        issues.AddRange(output.Issues);

        // Use the real ordinary READ APIs, not the repair helper: that helper
        // ensures files, deletes repair state and can apply progression. No
        // repair, reduction, scheduler, writer or old transaction runs here.
        using (var snapshotScope = _validator.UsePrevalidatedPendingTurnSnapshotScope(frame.Manifest))
        {
            issues.AddRange(existingStory is null ? await _validator.ValidateGameStateAsync() :
                await _validator.ValidateMortalJointStoryGameStateAsync(existingStory));
            issues.AddRange(await _validator.ValidateAcceptedTurnNarrativePayloadAsync());
            issues.AddRange(await _validator.ValidateAcceptedTurnInterfacePayloadAsync());
            issues.AddRange(await _validator.ValidateAcceptedTurnReasoningAsync());
            issues.AddRange(await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync());
            issues.AddRange(await _validator.ValidateAcceptedTurnQteOfferAsync());
            issues.AddRange(await _validator.ValidateAcceptedTurnMortalCombatMaterializationAsync());
            issues.AddRange(await _validator.ValidateAcceptedTurnMortalLevelUpMaterializationAsync());
            issues.AddRange(await _validator.ValidatePendingMemoryLegacyApplicationAsync());
            if (observedPhase.Phase == "unapplied")
            {
                if (frame.ProgressionControl is null) throw new InvalidDataException("joint_postcommon_original_control_missing");
                issues.AddRange(await _progressionSchedule.ValidateAcceptedTurnOutcomeAsync(frame.ProgressionControl));
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
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            // Old lease-local values do not authorize this lease. The actual
            // complete physical owner is reconstructed, then constrained to the
            // exact phase observed before the read-only canonical/narrative pass.
            var current = await MortalQteDecisionPublication.ReadJointProgressionPhaseAsync(_fs, lease,
                frame.SessionId, frame.RequestId, frame.Payload.ManifestPayloadHash, frame.TurnNumber);
            if (current.Phase != observedPhase.Phase || !current.MarkerBefore.AsSpan().SequenceEqual(observedPhase.MarkerBefore))
                throw new InvalidDataException("joint_postcommon_phase_changed_during_validation");
            var phaseResult = await MortalQteDecisionPublication.CommitJointProgressionPhaseAsync(_fs, lease, current);
            if (phaseResult.Disposition is not (MortalQteDecisionPublication.JointProgressionPublicationDisposition.Applied or
                MortalQteDecisionPublication.JointProgressionPublicationDisposition.AlreadyApplied) || phaseResult.AppliedCapability is not { } applied)
            {
                _mortalJointRepairCanonicalRefresh = publication with { Issues = issues.Concat(phaseResult.Issues).ToArray() };
                _logger.LogWarning("Joint progression phase remained blocked after {Source}: {Disposition}; committed union retained.", source, phaseResult.Disposition);
                return AcceptedTurnValidationDisposition.MortalJointRepairPublicationHeldBlocked;
            }
            var projection = existingStory is null ? await _charService.WriteMortalJointProjectionAsync(lease, applied) :
                await _charService.VerifyMortalJointProjectionAsync(lease, applied);
            if (projection.Disposition is not (CharacteristicsService.JointDerivedProjectionDisposition.WrittenAndVerified or
                CharacteristicsService.JointDerivedProjectionDisposition.ExistingVerified))
            {
                _logger.LogWarning("Joint derived projection refused after {Source}: {Disposition}: {Failure}; evidence retained.",
                    source, projection.Disposition, projection.Failure);
                return AcceptedTurnValidationDisposition.MortalJointRepairPublicationHeldBlocked;
            }
            await applied.ValidateCurrentAsync(_fs, lease);
        }
        // This is a dedicated prerequisite disposition, never generic Accepted.
        // The named actual caller must freshly prove story/projection/turn before
        // any advancement; terminal cleanup remains an independent owned step.
        return AcceptedTurnValidationDisposition.MortalJointRepairCompletionReady;
    }
}
