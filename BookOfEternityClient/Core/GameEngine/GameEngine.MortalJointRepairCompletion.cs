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

        await ValidateCurrentMortalJointCompletedProofAsync(frame);
        var issues = publication.Issues.ToList();
        WoundAcceptedTurnOutputBindingResult output;
        if (publication.Plan is { } plan)
        {
            issues.AddRange(await ValidateAcceptedWoundPostPublicationAuthorityAsync(plan, frame));
            output = await BindPublishedAcceptedWoundOutputAsync(plan);
        }
        else
            output = continuationOutput ?? throw new InvalidDataException("Cold joint continuation requires its actual physically bound output.");
        issues.AddRange(output.Issues);

        // Use the real ordinary READ APIs, not the repair helper: that helper
        // ensures files, deletes repair state and can apply progression. No
        // repair, reduction, scheduler, writer or old transaction runs here.
        using (var snapshotScope = _validator.UsePrevalidatedPendingTurnSnapshotScope(frame.Manifest))
        {
            issues.AddRange(await _validator.ValidateGameStateAsync());
            issues.AddRange(await _validator.ValidateAcceptedTurnNarrativePayloadAsync());
            issues.AddRange(await _validator.ValidateAcceptedTurnInterfacePayloadAsync());
            issues.AddRange(await _validator.ValidateAcceptedTurnReasoningAsync());
            issues.AddRange(await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync());
            issues.AddRange(await _validator.ValidateAcceptedTurnQteOfferAsync());
            issues.AddRange(await _validator.ValidateAcceptedTurnMortalCombatMaterializationAsync());
            issues.AddRange(await _validator.ValidateAcceptedTurnMortalLevelUpMaterializationAsync());
            issues.AddRange(await _validator.ValidatePendingMemoryLegacyApplicationAsync());
            if (frame.ProgressionControl is not null)
                issues.AddRange(await _progressionSchedule.ValidateAcceptedTurnOutcomeAsync(frame.ProgressionControl));
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
        // Actual StagePlayerTurnAsync always supplies progression. Its existing
        // two-file writer has no durable once-only acknowledgement, so neither
        // fresh completion nor replay may silently omit or reapply it. The named
        // owning amendment is OPEN; no null-control fixture substitute accepts.
        _logger.LogWarning("Joint canonical/narrative validation passed, but durable once-owned progression completion remains blocked.");
        return AcceptedTurnValidationDisposition.MortalJointRepairPublicationHeldBlocked;
    }
}
