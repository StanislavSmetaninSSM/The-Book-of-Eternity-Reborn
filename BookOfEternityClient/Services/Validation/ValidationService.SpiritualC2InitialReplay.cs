using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Retains the freshly reconstructed original owners and their verified first C1 packet.
    /// No result grants pending repair, persistence or accepted publication authority.
    /// </summary>
    /// <param name="Capture">
    /// Fresh exact owner capture, or <see langword="null"/> on rejection.
    /// </param>
    /// <param name="Pending">
    /// Strictly rederived first packet, or <see langword="null"/> on rejection.
    /// </param>
    /// <param name="ProposedInstanceRow">
    /// Detached unpublished instance row, or <see langword="null"/> for an existing instance.
    /// </param>
    /// <param name="Issues">
    /// Validation failures; empty only for a complete first packet replay.
    /// </param>
    internal sealed record SpiritualC2InitialReplayResult(
        SpiritualOriginalTurnCapture? Capture,
        SpiritualWoundDecisionPendingState? Pending,
        JsonObject? ProposedInstanceRow,
        IReadOnlyList<ValidationIssue> Issues);

    /// <summary>
    /// Replays a parsed initial checkpoint through fresh original owners without a file write.
    /// </summary>
    /// <param name="lease">
    /// Active canonical lease for signed origin and immutable physical witness checks.
    /// </param>
    /// <param name="checkpoint">
    /// Strictly parsed, nonempty initial comparison record.
    /// </param>
    /// <returns>
    /// Current reconstructed owners and verified first packet, or issues without retained owners.
    /// </returns>
    internal Task<SpiritualC2InitialReplayResult> ReplayInitialSpiritualCheckpointAsync(
        FileSystemManager.CanonicalWriteLease lease,
        SpiritualWoundCaptureCheckpointState checkpoint) =>
        SpiritualOriginalTurnCapture.ReplayInitialCheckpointAsync(this, lease, checkpoint);

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Derives the first packet from the original A view and exact replay allocation stream.
        /// </summary>
        /// <param name="validator">
        /// Fresh validator receiving the reconstructed capture.
        /// </param>
        /// <param name="lease">
        /// Active canonical lease authenticating the original physical authority files.
        /// </param>
        /// <param name="checkpoint">
        /// Parsed original checkpoint whose first markers must match the real owner replay.
        /// </param>
        /// <returns>
        /// Retained replay owners and strict packet on exact agreement, or validation issues.
        /// </returns>
        internal static Task<SpiritualC2InitialReplayResult> ReplayInitialCheckpointAsync(
            ValidationService validator, FileSystemManager.CanonicalWriteLease lease,
            SpiritualWoundCaptureCheckpointState checkpoint) =>
            ReplayFirstCheckpointCoreAsync(validator, lease, checkpoint, allowSavedAdvances: false);

        /// <summary>
        /// Replays the initial owner boundary of a checkpoint containing later saved advances.
        /// Its remaining allocation rows stay replay-only until every saved step is proved.
        /// </summary>
        /// <param name="validator">
        /// Fresh validator receiving the original owner tuple.
        /// </param>
        /// <param name="lease">
        /// Active canonical lease for original signed and physical witnesses.
        /// </param>
        /// <param name="checkpoint">
        /// Strict checkpoint whose first packet marker is checked before saved steps.
        /// </param>
        /// <returns>
        /// Reconstructed first packet and owners, or validation issues.
        /// </returns>
        internal static Task<SpiritualC2InitialReplayResult> ReplaySavedFirstCheckpointAsync(
            ValidationService validator, FileSystemManager.CanonicalWriteLease lease,
            SpiritualWoundCaptureCheckpointState checkpoint) =>
            ReplayFirstCheckpointCoreAsync(validator, lease, checkpoint, allowSavedAdvances: true);

        /// <summary>
        /// Replays only the first owner-derived packet, optionally retaining later journal rows.
        /// </summary>
        /// <param name="validator">
        /// Fresh validator receiving the owner tuple.
        /// </param>
        /// <param name="lease">
        /// Active canonical lease for signed origin checks.
        /// </param>
        /// <param name="checkpoint">
        /// Strict checkpoint with an authentic first marker.
        /// </param>
        /// <param name="allowSavedAdvances">
        /// Keeps retained future allocations unread when <see langword="true"/>.
        /// </param>
        /// <returns>
        /// First owner packet or validation issues.
        /// </returns>
        private static async Task<SpiritualC2InitialReplayResult> ReplayFirstCheckpointCoreAsync(
            ValidationService validator, FileSystemManager.CanonicalWriteLease lease,
            SpiritualWoundCaptureCheckpointState checkpoint, bool allowSavedAdvances)
        {
            ArgumentNullException.ThrowIfNull(validator);
            ArgumentNullException.ThrowIfNull(checkpoint);
            validator._fs.EnsureCanonicalWriteLeaseActive(lease);
            if (!checkpoint.HasCheckpoint ||
                (!allowSavedAdvances && (checkpoint.CommittedAdvance != 0 || checkpoint.HasPendingSubmission)) ||
                (allowSavedAdvances && checkpoint.CommittedAdvance == 0 && !checkpoint.HasPendingSubmission) ||
                validator._spiritualOriginalTurnCapture is not null)
                return InitialReplayFailure("spiritual_checkpoint_first_replay_invalid");

            SpiritualOriginalTurnCaptureResult recorded;
            try
            {
                recorded = await CaptureFromCheckpointOriginAsync(validator, lease, checkpoint);
            }
            catch (Exception error) when (error is InvalidOperationException or FormatException or
                OverflowException or System.Text.Json.JsonException)
            {
                validator._spiritualOriginalTurnCapture?.Dispose();
                return InitialReplayFailure("spiritual_checkpoint_owner_replay_mismatch");
            }
            if (recorded.Capture is not { } capture)
                return recorded.Issues.Count != 0
                    ? new(null, null, null, recorded.Issues)
                    : InitialReplayFailure("spiritual_checkpoint_owner_replay_mismatch");
            if (recorded.Issues.Count != 0)
            {
                capture.Dispose();
                return new(null, null, null, recorded.Issues);
            }
            var retained = false;
            try
            {
                var beginIssues = await capture.BeginResourceExecutionAsync(lease);
                if (beginIssues.Count != 0)
                    return new(null, null, null, beginIssues);
                var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
                if (advanced.Issues.Count != 0)
                    return new(null, null, null, advanced.Issues);
                if (!capture._source.TryReadInitialActiveExchangeInventory(out _,
                        out var originalExchangeIds) || originalExchangeIds.Length == 0)
                    return InitialReplayFailure("spiritual_checkpoint_original_inventory_invalid");
                for (var ordinal = 0; ordinal < originalExchangeIds.Length; ordinal++)
                {
                    if (advanced.Step?.Interval is not { } interval ||
                        interval.Ordinal != ordinal ||
                        interval.ExchangeId != originalExchangeIds[ordinal])
                        return InitialReplayFailure("spiritual_checkpoint_original_inventory_invalid");
                    var offered = await capture.ComposeC1FirstOfferAsync(lease, interval);
                    if (offered.Issues.Count != 0)
                        return new(null, null, null, offered.Issues);
                    if (offered.Pending is null)
                    {
                        if (ordinal + 1 == originalExchangeIds.Length)
                            break;
                        advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
                        if (advanced.Issues.Count != 0)
                            return new(null, null, null, advanced.Issues);
                        continue;
                    }
                    if (offered.Pending.PacketFingerprint !=
                            checkpoint.InitialPendingPacketFingerprint ||
                        capture.ReadAllocationCursor(lease) != checkpoint.InitialAllocationCount ||
                        !allowSavedAdvances &&
                        capture.ReadAllocationJournal(lease).Count != checkpoint.InitialAllocationCount)
                        return InitialReplayFailure("spiritual_checkpoint_first_packet_mismatch");
                    retained = true;
                    return new(capture, offered.Pending,
                        offered.ProposedInstanceRow?.DeepClone().AsObject(), []);
                }
                return InitialReplayFailure("spiritual_checkpoint_first_offer_missing");
            }
            catch (Exception error) when (error is InvalidOperationException or FormatException or
                OverflowException or System.Text.Json.JsonException)
            {
                return InitialReplayFailure("spiritual_checkpoint_owner_replay_mismatch");
            }
            finally
            {
                if (!retained)
                    capture.Dispose();
            }
        }

        private static SpiritualC2InitialReplayResult InitialReplayFailure(string code) =>
            new(null, null, null,
            [
                new ValidationIssue(SpiritualWoundCaptureCheckpointState.StatePath,
                    IssueSeverity.Error, "The first spiritual checkpoint cannot be replayed by its original owners.",
                    code: code, section: "AcceptedTurnWoundMaterialization")
            ]);
    }
}
