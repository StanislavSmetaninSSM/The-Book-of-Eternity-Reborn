using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class MortalQteDecisionPublication
{
    // These outcomes settle ONLY the three-file bookkeeping phase. Mechanics
    // remain committed; none is an old treatment rollback/hold-release result.
    internal enum JointProgressionPublicationDisposition
    {
        Applied, AlreadyApplied, RetryableUnapplied, PublicationUncertain
    }
    internal sealed record JointProgressionPublicationResult(JointProgressionPublicationDisposition Disposition,
        JointProgressionPhaseCapability? AppliedCapability, JointCompletedWitness? CompletedWitness,
        IReadOnlyList<ValidationIssue> Issues);

    internal static async Task<JointProgressionPublicationResult> CommitJointProgressionPhaseAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, JointProgressionPhaseCapability capability)
    {
        byte[]? appliedMarker = null;
        var knownCommitted = false;
        try
        {
            await capability.ValidateCurrentAsync(files, lease);
            if (capability.Phase == "applied")
                return await ReadFreshAppliedResultAsync(files, lease, capability, capability.MarkerBefore,
                    JointProgressionPublicationDisposition.AlreadyApplied);
            appliedMarker = capability.CreateAppliedMarker(JointProgressionPhaseIssuer);
            var changes = CreateJointProgressionPhaseChanges(capability, appliedMarker, restore: false);
            if (!capability.TryConsume(JointProgressionPhaseIssuer)) return JointPhaseHeld("The exact phase capability is already consumed.");
            // One existing trusted publication. The unchanged report is guard-
            // only: the full exact source/phase proof in the publisher callback
            // checks it after boundary awaits and before every commit attempt.
            var outcome = await files.PublishLocalFilesAsync(lease, changes,
                validatePreparedNamespace: () => capability.ValidateForPublicationAsync(JointProgressionPhaseIssuer, files, lease));
            if (outcome.Disposition == TrustedLocalPublicationDisposition.Uncertain)
                return JointPhaseHeld("The phase publication outcome is uncertain: " + outcome.Failure?.Message);
            if (outcome.Disposition == TrustedLocalPublicationDisposition.RolledBack)
                return await ReadKnownUnappliedResultAsync(files, lease, capability,
                    "The trusted publisher rolled back the phase: " + outcome.Failure?.Message);
            knownCommitted = true;
            files.RequireCommittedLocalPublication(outcome);
            await RequireJointPhaseImagesAsync(files, lease, capability, appliedMarker, applied: true);
            return await ReadFreshAppliedResultAsync(files, lease, capability, appliedMarker,
                JointProgressionPublicationDisposition.Applied);
        }
        catch (CoordinatedStatePublicationUncertainException error)
        {
            // Retained publisher debt is resolved by its existing owner before
            // any later classification. It never authorizes a restore here.
            return JointPhaseHeld(error.Message);
        }
        catch (Exception error) when (error is not SessionReplacedException &&
            error is InvalidDataException or IOException or InvalidOperationException or ArgumentException or System.Text.Json.JsonException or
                FormatException or KeyNotFoundException or System.Text.DecoderFallbackException or OperationCanceledException)
        {
            if (!knownCommitted || appliedMarker is null) return JointPhaseHeld(error.Message);
            return await CompensateKnownJointProgressionAsync(files, lease, capability, appliedMarker, error.Message);
        }
    }

    private static CanonicalLocalFileChange[] CreateJointProgressionPhaseChanges(JointProgressionPhaseCapability capability,
        byte[] appliedMarker, bool restore)
    {
        var changes = new List<CanonicalLocalFileChange>
        {
            new(ProgressionScheduleService.SchedulePath,
                restore ? capability.ScheduleAfter.Bytes : capability.ScheduleBefore.Bytes,
                restore ? capability.ScheduleBefore.Bytes : capability.ScheduleAfter.Bytes),
            new(StatePath, restore ? appliedMarker.ToArray() : capability.MarkerBefore,
                restore ? capability.MarkerBefore : appliedMarker.ToArray())
        };
        if (capability.ReportBefore.Fingerprint != capability.ReportAfter.Fingerprint)
            changes.Insert(1, new(ProgressionScheduleService.ReportPath,
                restore ? capability.ReportAfter.Bytes : capability.ReportBefore.Bytes,
                restore ? capability.ReportBefore.Bytes : capability.ReportAfter.Bytes));
        return changes.ToArray();
    }

    private static async Task RequireJointPhaseImagesAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        JointProgressionPhaseCapability capability, byte[] appliedMarker, bool applied)
    {
        // Complete fixed inventory, including unchanged or absent report.
        var expected = new[]
        {
            (ProgressionScheduleService.SchedulePath, applied ? capability.ScheduleAfter.Bytes : capability.ScheduleBefore.Bytes),
            (ProgressionScheduleService.ReportPath, applied ? capability.ReportAfter.Bytes : capability.ReportBefore.Bytes),
            (StatePath, applied ? appliedMarker : capability.MarkerBefore)
        };
        foreach (var (path, bytes) in expected)
            if (!Same(bytes, await files.ReadFileBytesAsync(lease, path))) throw Invalid("joint_progression_publication_readback: " + path);
    }

    private static async Task<JointProgressionPublicationResult> ReadFreshAppliedResultAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, JointProgressionPhaseCapability original, byte[] marker,
        JointProgressionPublicationDisposition disposition)
    {
        // Fresh reader first resolves journal debt, then rereads the COMPLETE
        // mechanical union, original/control/G, frozen calculation and phase.
        var fresh = await original.ReadFreshAsync(files, lease);
        if (fresh.Phase != "applied" || !original.SameImmutableBasis(fresh) || !Same(marker, fresh.MarkerBefore))
            throw Invalid("joint_progression_applied_owner_changed");
        await RequireJointPhaseImagesAsync(files, lease, fresh, marker, applied: true);
        var witness = await ReadJointCompletedWitnessAsync(files, lease,
            fresh.SessionIdForOwner, fresh.RequestIdForOwner, fresh.SnapshotTokenForOwner, fresh.TurnForOwner);
        await fresh.ValidateCurrentAsync(files, lease);
        return new(disposition, fresh, witness, Array.Empty<ValidationIssue>());
    }

    private static async Task<JointProgressionPublicationResult> ReadKnownUnappliedResultAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, JointProgressionPhaseCapability original, string reason)
    {
        var fresh = await original.ReadFreshAsync(files, lease);
        if (fresh.Phase != "unapplied" || !original.SameImmutableBasis(fresh) || !Same(original.MarkerBefore, fresh.MarkerBefore))
            return JointPhaseHeld("Known rollback did not preserve the exact unapplied basis: " + reason);
        await RequireJointPhaseImagesAsync(files, lease, fresh, fresh.MarkerBefore, applied: false);
        await fresh.ValidateCurrentAsync(files, lease);
        return new(JointProgressionPublicationDisposition.RetryableUnapplied, null, null, JointPhaseIssues(reason));
    }

    private static async Task<JointProgressionPublicationResult> CompensateKnownJointProgressionAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, JointProgressionPhaseCapability original, byte[] appliedMarker, string failure)
    {
        try
        {
            // Known commitment alone is insufficient. BEFORE any restore, prove
            // the exact current applied owner/basis and all three intended bytes,
            // with the entire committed mechanical union still authenticated.
            var applied = await original.ReadFreshAsync(files, lease);
            if (applied.Phase != "applied" || !original.SameImmutableBasis(applied) || !Same(appliedMarker, applied.MarkerBefore))
                return JointPhaseHeld("Compensation refuses changed phase ownership: " + failure);
            await RequireJointPhaseImagesAsync(files, lease, applied, appliedMarker, applied: true);
            if (!original.TryBeginCompensation(JointProgressionPhaseIssuer))
                return JointPhaseHeld("Compensation requires the same known committed phase attempt.");
            await applied.ValidateCurrentAsync(files, lease);
            var restore = CreateJointProgressionPhaseChanges(original, appliedMarker, restore: true);
            var outcome = await files.PublishLocalFilesAsync(lease, restore,
                validatePreparedNamespace: () => applied.ValidateCurrentAsync(files, lease));
            if (outcome.Disposition != TrustedLocalPublicationDisposition.Committed)
                return JointPhaseHeld("Phase compensation was not committed: " + outcome.Failure?.Message);
            files.RequireCommittedLocalPublication(outcome);
            await RequireJointPhaseImagesAsync(files, lease, original, appliedMarker, applied: false);
            return await ReadKnownUnappliedResultAsync(files, lease, original, failure);
        }
        catch (Exception error) when (error is not SessionReplacedException &&
            error is CoordinatedStatePublicationUncertainException or InvalidDataException or IOException or
                InvalidOperationException or ArgumentException or System.Text.Json.JsonException or FormatException or
                KeyNotFoundException or System.Text.DecoderFallbackException or OperationCanceledException)
        {
            // No sequential restore tail and no further compensation attempt.
            // Unproved/mixed/foreign bytes and current generation conflicts stay.
            return JointPhaseHeld("Phase compensation blocked: " + error.Message);
        }
    }

    private static JointProgressionPublicationResult JointPhaseHeld(string reason) =>
        new(JointProgressionPublicationDisposition.PublicationUncertain, null, null, JointPhaseIssues(reason));
    private static IReadOnlyList<ValidationIssue> JointPhaseIssues(string reason) =>
        [MortalWoundOpportunityAdapter.PrivateSourceIssue(WoundAcceptedTurnPlannerCore.NewIssue(
            "mortal_joint_progression_phase_held", reason, "the exact original-bound three-member progression phase", "blocked"))];
}
