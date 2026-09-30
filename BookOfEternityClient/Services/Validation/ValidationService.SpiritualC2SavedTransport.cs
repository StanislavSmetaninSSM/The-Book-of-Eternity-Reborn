using System.Text;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Reports the exact outcome of one saved-decision private transport attempt.
    /// </summary>
    /// <param name="Disposition">
    /// Committed, not_committed, repair_required or blocked.
    /// </param>
    /// <param name="Checkpoint">
    /// Detached owner-derived staged or advanced checkpoint when available, including repair-required outcomes.
    /// </param>
    /// <param name="Pending">
    /// Detached owner-derived projection when available, including repair-required outcomes.
    /// </param>
    /// <param name="Issues">
    /// Validation or transport failures; empty only for a fully confirmed pair.
    /// </param>
    /// <param name="RequiresDependentContinuation">
    /// Whether the submitted decision needs a corrected later original exchange before retry.
    /// </param>
    internal sealed record SpiritualC2SavedTransportResult(
        string Disposition, SpiritualWoundCaptureCheckpointState? Checkpoint,
        SpiritualWoundDecisionPendingState? Pending, IReadOnlyList<ValidationIssue> Issues,
        bool RequiresDependentContinuation = false);

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Saves one owner-derived decision checkpoint before replacing its pending projection.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease held through derivation, both writes and exact read-backs.
        /// </param>
        /// <param name="writeOverrideAsync">
        /// Optional test transport replacing only atomic writes and receiving this capture.
        /// </param>
        /// <param name="expectedCommandBytes">
        /// Optional exact submitted GM command bytes that must match the saved decision input.
        /// </param>
        /// <param name="dependentPolicy">
        /// Additional exact comparison derived by the live private-session adapter; null preserves direct internal transport.
        /// This restriction never replaces source ownership or actual corrected-exchange validation.
        /// </param>
        /// <returns>
        /// A confirmed pair or a disposition requiring strict replay repair or a fresh attempt.
        /// </returns>
        internal async Task<SpiritualC2SavedTransportResult> CommitC2SavedTransportAsync(
            FileSystemManager.CanonicalWriteLease lease,
            Func<FileSystemManager.CanonicalWriteLease, SpiritualOriginalTurnCapture,
                string, byte[], Task>? writeOverrideAsync = null,
            byte[]? expectedCommandBytes = null,
            SpiritualWoundDependentDraftPolicy? dependentPolicy = null)
        {
            await _gate.WaitAsync();
            try
            {
                var pair = _matchedC2Pair;
                if (pair is null || _c2DecisionDrafted)
                    return SavedTransportFailure("blocked", "spiritual_saved_transport_pair_required");
                var writer = writeOverrideAsync ??
                    ((FileSystemManager.CanonicalWriteLease heldLease,
                        SpiritualOriginalTurnCapture _, string path, byte[] bytes) =>
                        _validator._fs.WriteFileAtomicBytesAsync(heldLease, path, bytes));
                if (!_c2PendingSubmission)
                    return await StageAndResumeC2SubmissionCoreAsync(lease, writer, expectedCommandBytes);
                if (expectedCommandBytes is not null && !ExactBytes(
                        await _validator._fs.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath),
                        expectedCommandBytes))
                {
                    RevokeUnderLease(lease);
                    return SavedTransportFailure("blocked", "spiritual_c2_submitted_command_changed");
                }
                var draft = await ResumeC2DecisionDraftCoreAsync(lease, dependentPolicy);
                if (draft.Checkpoint is not { } checkpoint || draft.Pending is not { } pending ||
                    draft.Issues.Count != 0)
                    return new("blocked", null, null, draft.Issues,
                        draft.RequiresDependentContinuation);
                if (!IsCurrentOwner)
                    return SavedTransportFailure("blocked", "spiritual_saved_transport_owner_revoked");

                var checkpointPath = SpiritualWoundCaptureCheckpointState.StatePath;
                var pendingPath = SpiritualWoundDecisionPendingState.StatePath;
                var checkpointBytes = Encoding.UTF8.GetBytes(
                    SpiritualWoundCaptureCheckpointState.SerializeCanonical(checkpoint));
                try { await writer(lease, this, checkpointPath, checkpointBytes); }
                catch (Exception) { /* Exact read-back resolves uncertain atomic replacement. */ }

                byte[]? confirmedCheckpoint;
                try { confirmedCheckpoint = await _validator._fs.ReadFileBytesAsync(lease, checkpointPath); }
                catch (Exception)
                {
                    RevokeUnderLease(lease);
                    return SavedTransportFailure("blocked", "spiritual_saved_transport_checkpoint_read_failed");
                }
                if (!ExactBytes(confirmedCheckpoint, checkpointBytes))
                {
                    RevokeUnderLease(lease);
                    return ExactBytes(confirmedCheckpoint, pair.CheckpointBytes)
                        ? SavedTransportFailure("not_committed", "spiritual_saved_transport_not_committed")
                        : SavedTransportFailure("blocked", "spiritual_saved_transport_checkpoint_ambiguous");
                }
                if (!IsCurrentOwner)
                    return SavedTransportFailure("repair_required", "spiritual_saved_transport_owner_revoked",
                        checkpoint, pending);

                byte[]? pendingBeforeProjection;
                try { pendingBeforeProjection = await _validator._fs.ReadFileBytesAsync(lease, pendingPath); }
                catch (Exception)
                {
                    RevokeUnderLease(lease);
                    return SavedTransportFailure("repair_required", "spiritual_saved_transport_pending_read_failed",
                        checkpoint, pending);
                }
                if (!ExactBytes(pendingBeforeProjection, pair.PendingBytes) || !IsCurrentOwner)
                {
                    if (IsCurrentOwner)
                        RevokeUnderLease(lease);
                    return SavedTransportFailure("repair_required", "spiritual_saved_transport_pending_changed",
                        checkpoint, pending);
                }

                var pendingBytes = Encoding.UTF8.GetBytes(
                    SpiritualWoundDecisionPendingState.SerializeCanonical(pending));
                try { await writer(lease, this, pendingPath, pendingBytes); }
                catch (Exception) { /* Checkpoint remains the committed recovery boundary. */ }

                byte[]? confirmedPending;
                byte[]? checkpointAfterPending;
                try
                {
                    confirmedPending = await _validator._fs.ReadFileBytesAsync(lease, pendingPath);
                    checkpointAfterPending = await _validator._fs.ReadFileBytesAsync(lease, checkpointPath);
                }
                catch (Exception)
                {
                    RevokeUnderLease(lease);
                    return SavedTransportFailure("repair_required", "spiritual_saved_transport_pending_read_failed",
                        checkpoint, pending);
                }
                if (!ExactBytes(checkpointAfterPending, checkpointBytes))
                {
                    RevokeUnderLease(lease);
                    return SavedTransportFailure("blocked", "spiritual_saved_transport_checkpoint_changed");
                }
                if (!ExactBytes(confirmedPending, pendingBytes) || !IsCurrentOwner)
                {
                    if (IsCurrentOwner)
                        RevokeUnderLease(lease);
                    return SavedTransportFailure("repair_required", "spiritual_saved_transport_pending_repair_required",
                        checkpoint, pending);
                }
                try
                {
                    var originIssues = await CheckRetainedInputsAfterC2TransportAsync(
                        lease, checkpointBytes, pendingBytes);
                    if (originIssues.Count != 0)
                        return new("blocked", checkpoint, pending, originIssues);
                    var sourceIssues = await _source.CheckContinuationInputsAsync(lease);
                    if (sourceIssues.Count != 0)
                    {
                        RevokeUnderLease(lease);
                        return new("blocked", checkpoint, pending, sourceIssues);
                    }
                    foreach (var input in ReadC2CommittedInputLayer(checkpoint))
                    {
                        var bytes = await _validator._fs.ReadFileBytesAsync(lease, input.Key);
                        if (!SameExactImage(input.Value, new CanonicalBeforeImage(bytes is not null, bytes)))
                        {
                            RevokeUnderLease(lease);
                            return SavedTransportFailure("blocked", "spiritual_saved_transport_input_changed",
                                checkpoint, pending);
                        }
                    }
                    var finalCheckpoint = await _validator._fs.ReadFileBytesAsync(lease, checkpointPath);
                    var finalPending = await _validator._fs.ReadFileBytesAsync(lease, pendingPath);
                    var finalCheckpointAfterPending = await _validator._fs.ReadFileBytesAsync(
                        lease, checkpointPath);
                    if (!IsCurrentOwner || !ExactBytes(finalCheckpoint, checkpointBytes) ||
                        !ExactBytes(finalPending, pendingBytes) ||
                        !ExactBytes(finalCheckpointAfterPending, checkpointBytes))
                    {
                        if (IsCurrentOwner)
                            RevokeUnderLease(lease);
                        return SavedTransportFailure("blocked", "spiritual_saved_transport_postflight_changed");
                    }
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                    InvalidOperationException or FormatException or System.Text.Json.JsonException)
                {
                    if (IsCurrentOwner)
                        RevokeUnderLease(lease);
                    return SavedTransportFailure("blocked", "spiritual_saved_transport_postflight_failed");
                }
                return new("committed", checkpoint, pending, []);
            }
            finally { _gate.Release(); }
        }

        private static SpiritualC2SavedTransportResult SavedTransportFailure(string disposition,
            string code, SpiritualWoundCaptureCheckpointState? checkpoint = null,
            SpiritualWoundDecisionPendingState? pending = null) =>
            new(disposition, checkpoint, pending,
            [
                new ValidationIssue(SpiritualWoundCaptureCheckpointState.StatePath,
                    IssueSeverity.Error, "The spiritual saved decision transport was not fully confirmed.",
                    code: code, section: "AcceptedTurnWoundMaterialization")
            ]);
    }
}
