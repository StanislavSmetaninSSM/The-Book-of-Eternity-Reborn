using System.Security.Cryptography;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    // Foundation for the joint repair owner. This method is deliberately not a
    // dispatch/publication entry: its caller must hold the admitted joint family.
    // The durable value selects provenance to reconstruct; it grants no authority.
    private async Task RestoreMortalJointRepairBaselineAsync(
        RollbackSnapshot snapshot, MortalJointRepairCheckpoint expectedCheckpoint)
    {
        var expected = expectedCheckpoint.ToJsonObject();
        var generation = expected["sessionGeneration"]!.GetValue<string>();
        CanonicalLocalFileChange[]? installed = null;
        var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        Exception? operationFailure = null;
        try
        {
            ThrowIfRepairSessionReplaced(writeLease, generation);
            RequireOrdinaryJointRepairLease(writeLease);
            var reconstructed = await ReadCurrentJointRejectedCaptureAsync(writeLease, expected);
            reconstructed.EnsureCurrent(_fs, writeLease);
            var baseline = await ReadValidatedRollbackBeforeImagesAsync(writeLease, snapshot);
            if (!snapshot.BaselineFiles.SetEquals(baseline.Keys))
                throw new InvalidDataException("The joint restore requires the complete original rollback inventory.");

            var desired = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (path, bytes) in baseline) desired.Add(path, bytes);
            foreach (var path in EnumerateRollbackTrackedFiles(writeLease))
                if (!snapshot.BaselineFiles.Contains(path)) desired.Add(path, null);
            // The homogeneous command and full private wave are the only baseline
            // overrides. Neither a public mixed command nor an accepted QTE command
            // is fabricated or installed here.
            desired[AcceptedMechanicsPlan.WoundCommandPath] = reconstructed.CommandBytes;
            desired[WoundAcceptedTurnSnapshotContract.PendingResolutionPath] =
                System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(reconstructed.Pending);

            var changes = new List<CanonicalLocalFileChange>();
            foreach (var (path, after) in desired.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!_fs.UsesTrustedLocalWriter(writeLease, path))
                    throw new InvalidDataException("The joint restore requires one ordinary local batch: " + path);
                changes.Add(new(path, await _fs.ReadFileBytesAsync(writeLease, path), after));
            }
            var cohort = changes.ToArray();
            var changed = cohort.Where(change => !JointRestoreBytesEqual(change.Before, change.After)).ToArray();
            if (changed.Length != 0)
            {
                var outcome = await _fs.PublishLocalFilesAsync(writeLease, changed,
                    validatePreparedNamespace: async () =>
                    {
                        ThrowIfRepairSessionReplaced(writeLease, generation);
                        reconstructed.EnsureCurrent(_fs, writeLease);
                        await RequireJointRestoreImagesAsync(writeLease, cohort, after: false);
                        await RequireJointOriginalBytesAsync(writeLease, expected);
                    });
                _fs.RequireCommittedLocalPublication(outcome);
            }
            installed = cohort;
            try
            {
                ThrowIfRepairSessionReplaced(writeLease, generation);
                await RequireJointRestoreImagesAsync(writeLease, cohort, after: true);
                await RequireJointOriginalBytesAsync(writeLease, expected);
                _ = await ReadCurrentJointRejectedCaptureAsync(writeLease, expected);
            }
            catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException)
            {
                await CompensateJointRestoreAsync(writeLease, cohort, generation, failure);
                installed = null;
                throw;
            }
        }
        catch (Exception failure) { operationFailure = failure; throw; }
        finally { await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(_fs, writeLease, installed is not null, operationFailure); }

        // No repair dispatch can proceed until both physical readback and ordinary
        // runtime projection refresh succeeded. A known refresh failure compensates
        // the entire installed cohort under a freshly participating same-G lease.
        try { await RefreshRuntimeStateAfterExactRollbackAsync(); }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException)
        {
            if (installed is null) throw;
            FileSystemManager.CanonicalWriteLease compensationLease;
            try { compensationLease = await _fs.AcquireCanonicalWriteLeaseAsync(); }
            catch (Exception error)
            {
                var uncertainty = error as CoordinatedStatePublicationUncertainException ?? new CoordinatedStatePublicationUncertainException(error);
                uncertainty.Data["JointRepairOwningFailure"] = failure;
                throw uncertainty;
            }
            Exception? compensationFailure = null;
            try { await CompensateJointRestoreAsync(compensationLease, installed, generation, failure); }
            catch (Exception error) { compensationFailure = error; throw; }
            finally { await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(_fs, compensationLease, false, compensationFailure); }
            throw;
        }
    }

    private async Task<MortalJointRepairCheckpoint.RejectedCapture> ReadCurrentJointRejectedCaptureAsync(
        FileSystemManager.CanonicalWriteLease lease, JsonObject expected)
    {
        var pending = await _fs.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
        var result = pending is null
            ? await MortalJointRepairCheckpoint.CaptureAsync(_fs, lease, expected["sessionId"]!.GetValue<string>(),
                expected["requestId"]!.GetValue<string>(), expected["snapshotToken"]!.GetValue<string>(), expected["turnNumber"]!.GetValue<int>())
            : await MortalJointRepairCheckpoint.ReconstructAsync(_fs, lease, expected["sessionId"]!.GetValue<string>(),
                expected["requestId"]!.GetValue<string>(), expected["snapshotToken"]!.GetValue<string>(), expected["turnNumber"]!.GetValue<int>());
        if (!result.IsValid || result.Capture is null || !JsonNode.DeepEquals(result.Checkpoint!.ToJsonObject(), expected))
            throw new InvalidDataException("The actual joint original/request/source graph did not reconstruct the retained checkpoint: " +
                string.Join("; ", result.Issues.Select(issue => issue.Code)));
        return result.Capture;
    }

    private static void RequireOrdinaryJointRepairLease(FileSystemManager.CanonicalWriteLease lease)
    {
        lease.EnsureNoPendingLocalDecision();
        if (lease.IsLegacyStorageRecovery || lease.MutationIntentRecorder is not null || lease.BrowserLocalAccess is not null)
            throw new InvalidDataException("The joint repair restore requires its ordinary participating canonical lease.");
    }

    private async Task RequireJointOriginalBytesAsync(FileSystemManager.CanonicalWriteLease lease, JsonObject checkpoint)
    {
        foreach (var (path, field) in new[]
        {
            (LiveTurnPreparationService.PendingTurnSnapshotManifestPath, "originalManifestHash"),
            (PendingTurnSnapshotAuthority.AuthorityPath, "originalAuthorityHash")
        })
        {
            var bytes = await _fs.ReadFileBytesAsync(lease, path);
            if (bytes is null || Convert.ToHexString(SHA256.HashData(bytes)) != checkpoint[field]!.GetValue<string>())
                throw new InvalidDataException("The joint repair original bytes changed: " + path);
        }
    }

    private async Task RequireJointRestoreImagesAsync(FileSystemManager.CanonicalWriteLease lease,
        IReadOnlyList<CanonicalLocalFileChange> cohort, bool after)
    {
        foreach (var member in cohort)
            if (!JointRestoreBytesEqual(await _fs.ReadFileBytesAsync(lease, member.RelativePath), after ? member.After : member.Before))
                throw new InvalidDataException("The joint repair batch readback changed: " + member.RelativePath);
        var expected = cohort.Where(member => (after ? member.After : member.Before) is not null)
            .Select(member => member.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!expected.SetEquals(EnumerateRollbackTrackedFiles(lease)))
            throw new InvalidDataException("The joint repair rollback-tracked inventory changed outside its declared cohort.");
    }

    private async Task CompensateJointRestoreAsync(FileSystemManager.CanonicalWriteLease lease,
        IReadOnlyList<CanonicalLocalFileChange> cohort, string generation, Exception owningFailure)
    {
        try
        {
            ThrowIfRepairSessionReplaced(lease, generation);
            RequireOrdinaryJointRepairLease(lease);
            var paths = cohort.Select(member => member.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (EnumerateRollbackTrackedFiles(lease).Any(path => !paths.Contains(path)))
                throw new InvalidDataException("Unknown joint repair member prevents compensation.");
            var compensation = new List<CanonicalLocalFileChange>();
            foreach (var member in cohort)
            {
                var current = await _fs.ReadFileBytesAsync(lease, member.RelativePath);
                if (!JointRestoreBytesEqual(current, member.Before) && !JointRestoreBytesEqual(current, member.After))
                    throw new InvalidDataException("Unknown joint repair bytes prevent compensation: " + member.RelativePath);
                if (!JointRestoreBytesEqual(current, member.Before)) compensation.Add(new(member.RelativePath, current, member.Before));
            }
            if (compensation.Count != 0)
                _fs.RequireCommittedLocalPublication(await _fs.PublishLocalFilesAsync(lease, compensation));
            await RequireJointRestoreImagesAsync(lease, cohort, after: false);
        }
        catch (Exception failure)
        {
            var uncertainty = failure as CoordinatedStatePublicationUncertainException ?? new CoordinatedStatePublicationUncertainException(failure);
            uncertainty.Data["JointRepairOwningFailure"] = owningFailure;
            throw uncertainty;
        }
    }

    private static bool JointRestoreBytesEqual(byte[]? left, byte[]? right) =>
        left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);
}
