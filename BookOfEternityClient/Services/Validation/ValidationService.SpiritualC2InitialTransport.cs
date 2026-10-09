using System.Text;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Reports the exact outcome of the first two-root private transport attempt.
    /// The result does not grant accepted publication or pending repair authority.
    /// </summary>
    /// <param name="Disposition">
    /// Committed, no_offer, not_committed, repair_required or blocked.
    /// </param>
    /// <param name="Checkpoint">
    /// Detached owner-derived checkpoint when available, including repair-required outcomes.
    /// </param>
    /// <param name="Pending">
    /// Detached owner-derived pending packet when available, including repair-required outcomes.
    /// </param>
    /// <param name="Issues">
    /// Validation or transport failures; empty only for committed or no_offer.
    /// </param>
    internal sealed record SpiritualC2InitialTransportResult(
        string Disposition, SpiritualWoundCaptureCheckpointState? Checkpoint,
        SpiritualWoundDecisionPendingState? Pending, IReadOnlyList<ValidationIssue> Issues);

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Writes the first private checkpoint, confirms it, then writes its derived C1 projection.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease held across both writes and every exact read-back.
        /// </param>
        /// <param name="interval">
        /// Exact latest closed interval eligible for the first packet.
        /// </param>
        /// <param name="writeOverrideAsync">
        /// Optional test transport replacing only the atomic write operation.
        /// </param>
        /// <returns>
        /// Committed pair or a disposition requiring a fresh attempt or strict replay repair.
        /// </returns>
        internal async Task<SpiritualC2InitialTransportResult> CommitC2FirstTransportAsync(
            FileSystemManager.CanonicalWriteLease lease,
            AcceptedMechanicsPlanner.SpiritualExchangeInterval interval,
            Func<FileSystemManager.CanonicalWriteLease, string, byte[], Task>? writeOverrideAsync = null)
        {
            ArgumentNullException.ThrowIfNull(interval);
            await _gate.WaitAsync();
            try
            {
                var draft = await ComposeC2FirstCheckpointDraftCoreAsync(lease, interval);
                if (draft.Issues.Count != 0)
                    return new("blocked", null, null, draft.Issues);
                if (draft.Checkpoint is not { } checkpoint || draft.Pending is not { } pending)
                    return new("no_offer", null, null, []);

                var inventory = ReadC1ImageInventoryCore();
                if (!inventory.BeforeImages.TryGetValue(SpiritualWoundCaptureCheckpointState.StatePath,
                        out var checkpointBefore) ||
                    !inventory.BeforeImages.TryGetValue(SpiritualWoundDecisionPendingState.StatePath,
                        out var pendingBefore) ||
                    !IsEmptyCheckpointBefore(checkpointBefore) ||
                    !IsEmptyPendingBefore(pendingBefore, inventory.RegisteredPaths))
                    return TransportFailure("blocked", "spiritual_first_transport_active_baseline");
                var checkpointPath = SpiritualWoundCaptureCheckpointState.StatePath;
                var pendingPath = SpiritualWoundDecisionPendingState.StatePath;
                var currentCheckpoint = await _validator._fs.ReadFileBytesAsync(lease, checkpointPath);
                var currentPending = await _validator._fs.ReadFileBytesAsync(lease, pendingPath);
                if (!ExactBytes(currentCheckpoint, checkpointBefore.Bytes) ||
                    !ExactBytes(currentPending, pendingBefore.Bytes))
                {
                    RevokeUnderLease(lease);
                    return TransportFailure("blocked", "spiritual_first_transport_baseline_changed");
                }
                if (!IsCurrentOwner)
                    return TransportFailure("blocked", "spiritual_first_transport_owner_revoked");

                var writer = writeOverrideAsync ??
                    ((FileSystemManager.CanonicalWriteLease heldLease, string path, byte[] bytes) =>
                        _validator._fs.WriteFileAtomicBytesAsync(heldLease, path, bytes));
                var checkpointBytes = Encoding.UTF8.GetBytes(
                    SpiritualWoundCaptureCheckpointState.SerializeCanonical(checkpoint));
                try { await writer(lease, checkpointPath, checkpointBytes); }
                catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException)
                { /* Exact read-back is permitted only for a known ordinary transport failure. */ }

                byte[]? confirmedCheckpoint;
                try { confirmedCheckpoint = await _validator._fs.ReadFileBytesAsync(lease, checkpointPath); }
                catch (Exception)
                {
                    RevokeUnderLease(lease);
                    return TransportFailure("blocked", "spiritual_first_transport_checkpoint_read_failed");
                }
                if (!ExactBytes(confirmedCheckpoint, checkpointBytes))
                {
                    RevokeUnderLease(lease);
                    return ExactBytes(confirmedCheckpoint, checkpointBefore.Bytes)
                        ? TransportFailure("not_committed", "spiritual_first_transport_not_committed")
                        : TransportFailure("blocked", "spiritual_first_transport_checkpoint_ambiguous");
                }
                if (!IsCurrentOwner)
                    return TransportFailure("repair_required", "spiritual_first_transport_owner_revoked",
                        checkpoint, pending);

                var pendingBytes = Encoding.UTF8.GetBytes(
                    SpiritualWoundDecisionPendingState.SerializeCanonical(pending));
                try { await writer(lease, pendingPath, pendingBytes); }
                catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException)
                { /* Exact read-back is permitted only for a known ordinary transport failure. */ }

                byte[]? confirmedPending;
                try { confirmedPending = await _validator._fs.ReadFileBytesAsync(lease, pendingPath); }
                catch (Exception)
                {
                    RevokeUnderLease(lease);
                    return TransportFailure("repair_required", "spiritual_first_transport_pending_read_failed",
                        checkpoint, pending);
                }
                if (!ExactBytes(confirmedPending, pendingBytes))
                {
                    RevokeUnderLease(lease);
                    return TransportFailure("repair_required", "spiritual_first_transport_pending_repair_required",
                        checkpoint, pending);
                }
                if (!IsCurrentOwner)
                    return TransportFailure("repair_required", "spiritual_first_transport_owner_revoked",
                        checkpoint, pending);
                return new("committed", checkpoint, pending, []);
            }
            finally { _gate.Release(); }
        }

        private static bool ExactBytes(byte[]? left, byte[]? right) =>
            left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);

        private bool IsEmptyCheckpointBefore(CanonicalBeforeImage before)
        {
            if (!before.Existed)
                return true;
            var parsed = SpiritualWoundCaptureCheckpointState.Parse(
                DecodeOriginalRoot(before.Bytes!),
                SpiritualWoundCaptureCheckpointState.StatePath, _draftInputs.PathInventory);
            return parsed.IsValid && parsed.State is { HasCheckpoint: false };
        }

        private static bool IsEmptyPendingBefore(CanonicalBeforeImage before,
            IReadOnlyList<string> registeredPaths)
        {
            if (!before.Existed)
                return true;
            var parsed = SpiritualWoundDecisionPendingState.Parse(
                DecodeOriginalRoot(before.Bytes!),
                SpiritualWoundDecisionPendingState.StatePath, registeredPaths);
            return parsed.IsValid && parsed.State is { HasPending: false };
        }

        private static string DecodeOriginalRoot(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var reader = new StreamReader(stream, Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }

        private static SpiritualC2InitialTransportResult TransportFailure(string disposition,
            string code, SpiritualWoundCaptureCheckpointState? checkpoint = null,
            SpiritualWoundDecisionPendingState? pending = null) =>
            new(disposition, checkpoint, pending,
            [
                new ValidationIssue(SpiritualWoundCaptureCheckpointState.StatePath,
                    IssueSeverity.Error, "The first spiritual private transport was not fully confirmed.",
                    code: code, section: "AcceptedTurnWoundMaterialization")
            ]);
    }
}
