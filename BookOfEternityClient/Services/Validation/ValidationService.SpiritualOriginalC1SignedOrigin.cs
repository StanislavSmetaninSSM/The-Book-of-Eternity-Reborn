using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Carries exact signed receipt and conflict images from the original pending snapshot.
    /// These are separate from the physical candidate and publication rollback images.
    /// </summary>
    /// <param name="Receipt">
    /// Signed original spiritual receipt bytes or signed absence.
    /// </param>
    /// <param name="Conflict">
    /// Signed original spiritual conflict bytes or signed absence.
    /// </param>
    /// <param name="SnapshotFingerprint">
    /// Validated original manifest payload fingerprint.
    /// </param>
    internal sealed record SpiritualSignedC1Origin(
        CanonicalBeforeImage Receipt, CanonicalBeforeImage Conflict,
        string SnapshotFingerprint);

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        private readonly PendingTurnSnapshotReadAuthority? _signedC1OriginSnapshot;

        /// <summary>
        /// Rechecks the selected signed original snapshot and returns detached receipt and conflict images.
        /// This does not replace the separate physical draft freshness check before an offer.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease authenticating the current capture.
        /// </param>
        /// <returns>
        /// Exact signed A evidence, never candidate or rollback B evidence.
        /// </returns>
        internal SpiritualSignedC1Origin ReadVerifiedSignedC1Origin(
            FileSystemManager.CanonicalWriteLease lease)
        {
            if (!_gate.Wait(0))
                throw new InvalidOperationException("The original capture is continuing.");
            try
            {
                EnsureCurrent(lease);
                return ReadVerifiedSignedC1OriginCore(lease);
            }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// Rechecks signed A while the caller already holds the capture gate.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease authenticating this capture.
        /// </param>
        /// <returns>
        /// Detached signed original receipt and conflict images.
        /// </returns>
        private SpiritualSignedC1Origin ReadVerifiedSignedC1OriginCore(
            FileSystemManager.CanonicalWriteLease lease)
        {
            EnsureCurrent(lease);
            var retained = _signedC1OriginSnapshot ??
                throw new InvalidOperationException("A named signed C1 origin is required.");
            var read = PendingTurnSnapshotReader.ReadCurrent(_validator._fs, lease,
                PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(
                    [SpiritualWoundSourceSession.SoulPath],
                    [SpiritualWoundCaptureCheckpointState.StatePath,
                     SpiritualWoundDecisionPendingState.StatePath,
                     SpiritualWoundOpportunityReceiptState.StatePath,
                     AfterlifeSpiritualConflictState.StatePath]));
            if (!read.Success || read.Snapshot is not { } current ||
                retained.SessionId != current.SessionId ||
                retained.RequestId != current.RequestId ||
                retained.SnapshotToken != current.SnapshotToken ||
                retained.TurnNumber != current.TurnNumber ||
                retained.Realm != current.Realm ||
                !_source.IsCurrentOwner)
                throw new InvalidOperationException("The signed C1 origin changed or cannot be authenticated.");
            foreach (var path in new[]
                {
                    SpiritualWoundSourceSession.SoulPath,
                    SpiritualWoundCaptureCheckpointState.StatePath,
                    SpiritualWoundDecisionPendingState.StatePath,
                    SpiritualWoundOpportunityReceiptState.StatePath,
                    AfterlifeSpiritualConflictState.StatePath
                })
            {
                var before = ReadSelectedImage(retained, path);
                var now = ReadSelectedImage(current, path);
                if (before.Existed != now.Existed ||
                    before.Bytes is not null && !before.Bytes.SequenceEqual(now.Bytes ?? []) ||
                    before.Bytes is null && now.Bytes is not null)
                    throw new InvalidOperationException("The signed C1 origin image changed.");
            }
            return new(ReadSelectedImage(retained, SpiritualWoundOpportunityReceiptState.StatePath),
                ReadSelectedImage(retained, AfterlifeSpiritualConflictState.StatePath),
                "sha256:" + retained.SnapshotToken.ToLowerInvariant());
        }

        /// <summary>
        /// Copies one selected signed image, including an explicitly observed absent image.
        /// </summary>
        /// <param name="snapshot">
        /// Authenticated selected pending snapshot.
        /// </param>
        /// <param name="path">
        /// Exact observed logical path to copy.
        /// </param>
        /// <returns>
        /// Detached signed image or an error when the path was not selected.
        /// </returns>
        private static CanonicalBeforeImage ReadSelectedImage(
            PendingTurnSnapshotReadAuthority snapshot, string path)
        {
            if (snapshot.CoveredLogicalPaths.Contains(path, StringComparer.Ordinal))
                return new CanonicalBeforeImage(true, snapshot.ReadRequiredBytes(path));
            if (snapshot.AbsentLogicalPaths.Contains(path, StringComparer.Ordinal))
                return new CanonicalBeforeImage(false, null);
            throw new InvalidOperationException("The signed C1 origin does not cover a required observed path.");
        }
    }
}
