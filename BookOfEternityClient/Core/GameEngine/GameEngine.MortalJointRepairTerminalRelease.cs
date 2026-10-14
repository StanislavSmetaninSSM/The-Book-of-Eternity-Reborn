using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    private static readonly object MortalJointRepairTerminalBatchIssuer = new();
    internal static bool IsMortalJointRepairTerminalBatchIssuer(object issuer) =>
        ReferenceEquals(issuer, MortalJointRepairTerminalBatchIssuer);

    // The repair-loop caller is connected separately. This boundary reconstructs
    // the owner under its acquired lease; it never carries a prior lease's token.
    // TerminalReleased follows physical readback AND exact prepared claim release.
    private async Task<MortalJointRepairPublicationSettlement> TerminateMortalJointRepairAsync(
        RollbackSnapshot snapshot, MortalJointRepairCheckpoint expectedCheckpoint, string reason)
    {
        var expected = expectedCheckpoint.ToJsonObject();
        var generation = expected["sessionGeneration"]!.GetValue<string>();
        var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        AcceptedTurnAuthorityRegistry.MortalJointRepairReleaseToken? token = null;
        CanonicalLocalFileChange[]? cohort = null;
        var attempted = false;
        var wrote = false;
        Exception? operationFailure = null;
        try
        {
            ThrowIfRepairSessionReplaced(lease, generation);
            RequireOrdinaryJointRepairLease(lease);
            var capture = await ReadCurrentJointRejectedCaptureAsync(lease, expected);
            var prepared = await AcceptedTurnAuthorityRegistry.PrepareMortalJointRepairReleaseAsync(_fs, lease, capture, reason);
            if (prepared.Token is null)
                return new(MortalJointRepairPublicationDisposition.PublicationUncertain, prepared.Issues);
            token = prepared.Token;
            var baseline = await ReadValidatedRollbackBeforeImagesAsync(lease, snapshot);
            if (!snapshot.BaselineFiles.SetEquals(baseline.Keys))
                throw new InvalidDataException("Joint terminal rollback requires the complete validated original baseline.");
            var desired = baseline.ToDictionary(pair => pair.Key, pair => (byte[]?)pair.Value, StringComparer.OrdinalIgnoreCase);
            foreach (var path in EnumerateRollbackTrackedFiles(lease))
                if (!snapshot.BaselineFiles.Contains(path)) desired.Add(path, null);
            // The whole privately proved graph is removed; no request-row-only
            // quarantine, foreign sibling removal or independent cleanup writer.
            desired[AcceptedMechanicsPlan.WoundCommandPath] = null;
            desired[WoundAcceptedTurnSnapshotContract.PendingResolutionPath] = null;
            desired[MortalWoundOpportunityAdapter.DecisionPath] = null;
            var members = new List<CanonicalLocalFileChange>();
            foreach (var (path, after) in desired.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!_fs.UsesTrustedLocalWriter(lease, path))
                    throw new InvalidDataException("Joint terminal removal requires one declared ordinary local batch: " + path);
                members.Add(new(path, await _fs.ReadFileBytesAsync(lease, path), after));
            }
            cohort = members.ToArray();
            await token.ValidateBeforeRemovalAsync(_fs, lease);
            await RequireJointRestoreImagesAsync(lease, cohort, after: false);
            await RequireJointOriginalBytesAsync(lease, expected);
            token.BindMembers(MortalJointRepairTerminalBatchIssuer, cohort);
            var changes = cohort.Where(member => !JointRestoreBytesEqual(member.Before, member.After)).ToArray();
            if (changes.Length == 0)
                throw new InvalidDataException("A genuine owned command/private wave cannot have an empty terminal removal.");
            attempted = true;
            var outcome = await _fs.PublishLocalFilesAsync(lease, changes, validatePreparedNamespace: async () =>
            {
                ThrowIfRepairSessionReplaced(lease, generation);
                await token.ValidateBeforeRemovalAsync(_fs, lease);
                await RequireJointRestoreImagesAsync(lease, cohort, after: false);
                await RequireJointOriginalBytesAsync(lease, expected);
            });
            _fs.RequireCommittedLocalPublication(outcome);
            wrote = true;
            token.ObserveCommitted(MortalJointRepairTerminalBatchIssuer);
            await RequireJointRestoreImagesAsync(lease, cohort, after: true);
            await RequireJointOriginalBytesAsync(lease, expected);
            var readback = await token.ValidateRemovalReadbackAsync(_fs, lease);
            var settlement = AcceptedTurnAuthorityRegistry.CompleteMortalJointRepairRelease(_fs, lease, token, readback);
            if (settlement.Disposition != MortalJointRepairPublicationDisposition.TerminalReleased)
            {
                // This is not a successful release. Compensation may proceed only
                // while the token still proves its exact original confirmed claim.
                await token.CompensateRemovalAsync(_fs, lease);
                await RequireJointRestoreImagesAsync(lease, cohort, after: false);
                return new(MortalJointRepairPublicationDisposition.PublicationUncertain, settlement.Issues);
            }
            return settlement;
        }
        catch (CoordinatedStatePublicationUncertainException error)
        {
            operationFailure = error;
            // Recovery owns an uncertain batch. Do not run a second rollback or
            // reinterpret durable removal as proof that the claim was released.
            return JointTerminalBlocked(error);
        }
        catch (Exception error) when (error is InvalidDataException or IOException or InvalidOperationException or
            ArgumentException or System.Text.Json.JsonException or AggregateException)
        {
            operationFailure = error;
            if (attempted && token is not null && cohort is not null)
            {
                try
                {
                    await token.CompensateRemovalAsync(_fs, lease);
                    await RequireJointRestoreImagesAsync(lease, cohort, after: false);
                    await RequireJointOriginalBytesAsync(lease, expected);
                }
                catch (Exception compensationError) when (compensationError is CoordinatedStatePublicationUncertainException or
                    InvalidDataException or IOException or InvalidOperationException or ArgumentException or System.Text.Json.JsonException)
                {
                    return new(MortalJointRepairPublicationDisposition.PublicationUncertain,
                        JointTerminalBlocked(error).Issues.Concat(JointTerminalBlocked(compensationError).Issues).ToArray());
                }
            }
            return JointTerminalBlocked(error);
        }
        finally
        {
            await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(_fs, lease, wrote, operationFailure);
        }
        // The actual lifecycle caller must refresh ordinary runtime projections
        // only after this lease is released, and preserve typed blocked evidence.
    }

    private static MortalJointRepairPublicationSettlement JointTerminalBlocked(Exception error) =>
        new(MortalJointRepairPublicationDisposition.PublicationUncertain,
            [MortalWoundOpportunityAdapter.PrivateSourceIssue(WoundAcceptedTurnPlannerCore.NewIssue(
                "mortal_joint_repair_terminal_uncertain", error.Message,
                "complete owned graph rollback/removal/readback followed by exact prepared agreement release", "blocked"))]);
}
