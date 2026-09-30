using System.Text;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Reports a private first-pending repair attempt without granting decision or publication authority.
    /// </summary>
    /// <param name="Disposition">
    /// Match, repaired, no_checkpoint, not_committed, or blocked.
    /// </param>
    /// <param name="Capture">
    /// Fresh original owner on match or repaired; the caller must dispose it.
    /// </param>
    /// <param name="Pending">
    /// Owner-derived first packet on match or repaired.
    /// </param>
    /// <param name="Issues">
    /// Failures on not_committed or blocked; empty for successful dispositions.
    /// </param>
    internal sealed record SpiritualC2PendingRepairResult(
        string Disposition, SpiritualOriginalTurnCapture? Capture,
        SpiritualWoundDecisionPendingState? Pending, IReadOnlyList<ValidationIssue> Issues);

    /// <summary>
    /// Restores only a stale or absent first pending projection from the authoritative checkpoint replay.
    /// </summary>
    /// <param name="lease">
    /// Active canonical lease retained through classification, write and exact read-back.
    /// </param>
    /// <param name="writeOverrideAsync">
    /// Optional test transport replacing only the atomic pending write; receives the replay owner
    /// so tests can reproduce revocation during the write await.
    /// </param>
    /// <returns>
    /// A retained matching or repaired owner, or a fail-closed transport disposition.
    /// </returns>
    internal async Task<SpiritualC2PendingRepairResult> RepairInitialSpiritualPendingAsync(
        FileSystemManager.CanonicalWriteLease lease,
        Func<FileSystemManager.CanonicalWriteLease, SpiritualOriginalTurnCapture,
            string, byte[], Task>? writeOverrideAsync = null) =>
        await RepairSpiritualPendingCoreAsync(lease, includeSavedAdvances: false, writeOverrideAsync);

    /// <summary>
    /// Restores an initial or saved pending projection from the latest strictly replayed checkpoint.
    /// </summary>
    /// <param name="lease">
    /// Active canonical lease retained through classification, write and exact read-back.
    /// </param>
    /// <param name="writeOverrideAsync">
    /// Optional test transport replacing only the atomic pending write and receiving the replay owner.
    /// </param>
    /// <returns>
    /// A retained matching or repaired owner, or a fail-closed transport disposition.
    /// </returns>
    internal async Task<SpiritualC2PendingRepairResult> RepairSpiritualPendingAsync(
        FileSystemManager.CanonicalWriteLease lease,
        Func<FileSystemManager.CanonicalWriteLease, SpiritualOriginalTurnCapture,
            string, byte[], Task>? writeOverrideAsync = null) =>
        await RepairSpiritualPendingCoreAsync(lease, includeSavedAdvances: true, writeOverrideAsync);

    /// <summary>
    /// Repairs a derived pending packet using the selected strict replay policy.
    /// </summary>
    /// <param name="lease">
    /// Active canonical lease for all physical comparisons and transport.
    /// </param>
    /// <param name="includeSavedAdvances">
    /// Replays committed decision rows when <see langword="true"/>.
    /// </param>
    /// <param name="writeOverrideAsync">
    /// Optional test transport for the atomic pending write.
    /// </param>
    /// <returns>
    /// A retained owner only after the repaired pair is classified again.
    /// </returns>
    private async Task<SpiritualC2PendingRepairResult> RepairSpiritualPendingCoreAsync(
        FileSystemManager.CanonicalWriteLease lease, bool includeSavedAdvances,
        Func<FileSystemManager.CanonicalWriteLease, SpiritualOriginalTurnCapture,
            string, byte[], Task>? writeOverrideAsync)
    {
        _fs.EnsureCanonicalWriteLeaseActive(lease);
        var classified = includeSavedAdvances
            ? await ClassifySpiritualPendingAsync(lease)
            : await ClassifyInitialSpiritualPendingAsync(lease);
        if (classified.Disposition is "blocked" or "no_checkpoint")
            return new(classified.Disposition, null, null, classified.Issues);
        if (classified.Capture is not { } capture || classified.Pending is not { } pending ||
            classified.CheckpointImage is not { } checkpointBefore ||
            classified.PendingImage is not { } pendingBefore)
        {
            classified.Capture?.Dispose();
            return PendingRepairFailure("blocked", "spiritual_pending_repair_classifier_invalid");
        }

        var retained = false;
        try
        {
            if (!capture.IsCurrentOwner)
                return PendingRepairFailure("blocked", "spiritual_pending_repair_owner_revoked");
            if (classified.Disposition == "match")
            {
                retained = true;
                return new("match", capture, pending, []);
            }
            if (classified.Disposition != "repair_required")
                return PendingRepairFailure("blocked", "spiritual_pending_repair_classifier_invalid");

            var checkpointPath = SpiritualWoundCaptureCheckpointState.StatePath;
            var pendingPath = SpiritualWoundDecisionPendingState.StatePath;
            var currentCheckpoint = await _fs.ReadFileBytesAsync(lease, checkpointPath);
            var currentPending = await _fs.ReadFileBytesAsync(lease, pendingPath);
            if (!SamePhysicalBytes(checkpointBefore.Bytes, currentCheckpoint) ||
                !SamePhysicalBytes(pendingBefore.Bytes, currentPending) || !capture.IsCurrentOwner)
                return PendingRepairFailure("blocked", "spiritual_pending_repair_baseline_changed");

            var pendingBytes = Encoding.UTF8.GetBytes(
                SpiritualWoundDecisionPendingState.SerializeCanonical(pending));
            var writer = writeOverrideAsync ??
                ((FileSystemManager.CanonicalWriteLease heldLease,
                    SpiritualOriginalTurnCapture _, string path, byte[] bytes) =>
                    _fs.WriteFileAtomicBytesAsync(heldLease, path, bytes));
            try { await writer(lease, capture, pendingPath, pendingBytes); }
            catch (Exception) { /* The exact read-back determines an uncertain replacement. */ }

            byte[]? confirmedCheckpoint;
            byte[]? confirmedPending;
            try
            {
                confirmedCheckpoint = await _fs.ReadFileBytesAsync(lease, checkpointPath);
                confirmedPending = await _fs.ReadFileBytesAsync(lease, pendingPath);
                var checkpointAfterPending = await _fs.ReadFileBytesAsync(lease, checkpointPath);
                if (!SamePhysicalBytes(checkpointBefore.Bytes, checkpointAfterPending))
                    return PendingRepairFailure("blocked", "spiritual_pending_repair_authority_changed");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return PendingRepairFailure("blocked", "spiritual_pending_repair_readback_failed");
            }
            if (!SamePhysicalBytes(checkpointBefore.Bytes, confirmedCheckpoint) || !capture.IsCurrentOwner)
                return PendingRepairFailure("blocked", "spiritual_pending_repair_authority_changed");
            if (!SamePhysicalBytes(pendingBytes, confirmedPending))
                return SamePhysicalBytes(pendingBefore.Bytes, confirmedPending)
                    ? PendingRepairFailure("not_committed", "spiritual_pending_repair_not_committed")
                    : PendingRepairFailure("blocked", "spiritual_pending_repair_ambiguous");

            // The old capture witnessed the pre-repair pending bytes. Reopen a new owner
            // against the now-confirmed pair before permitting further use of its tuple.
            capture.Dispose();
            var confirmed = includeSavedAdvances
                ? await ClassifySpiritualPendingAsync(lease)
                : await ClassifyInitialSpiritualPendingAsync(lease);
            if (confirmed.Disposition != "match" || confirmed.Capture is not { } currentOwner ||
                confirmed.Pending is not { } currentPacket ||
                confirmed.CheckpointImage is not { } reopenedCheckpoint ||
                confirmed.PendingImage is not { } reopenedPending ||
                !SamePhysicalBytes(checkpointBefore.Bytes, reopenedCheckpoint.Bytes) ||
                !SamePhysicalBytes(pendingBytes, reopenedPending.Bytes))
            {
                confirmed.Capture?.Dispose();
                return confirmed.Issues.Count != 0
                    ? new("blocked", null, null, confirmed.Issues)
                    : PendingRepairFailure("blocked", "spiritual_pending_repair_replay_mismatch");
            }
            return new("repaired", currentOwner, currentPacket, []);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            InvalidOperationException)
        {
            return PendingRepairFailure("blocked", "spiritual_pending_repair_failed");
        }
        finally
        {
            if (!retained)
                capture.Dispose();
        }
    }

    private static SpiritualC2PendingRepairResult PendingRepairFailure(string disposition, string code) =>
        new(disposition, null, null,
        [
            new ValidationIssue(SpiritualWoundDecisionPendingState.StatePath, IssueSeverity.Error,
                "The spiritual pending projection could not be confirmed from its checkpoint.",
                code: code, section: "AcceptedTurnWoundMaterialization")
        ]);
}
