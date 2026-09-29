using System.Collections.ObjectModel;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Carries a detached registered C1 path set and actual publication rollback images.
    /// </summary>
    /// <param name="RegisteredPaths">
    /// Exact frozen relative paths available to the pending packet parser.
    /// </param>
    /// <param name="BeforeImages">
    /// Complete owner and frozen-physical rollback images plus signed C1 service roots;
    /// the first-offer producer independently checks exact registered coverage.
    /// </param>
    internal sealed record SpiritualC1ImageInventory(
        IReadOnlyList<string> RegisteredPaths,
        IReadOnlyDictionary<string, CanonicalBeforeImage> BeforeImages);

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Reads the frozen registered paths and rollback images of this exact capture.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease authenticating the current capture.
        /// </param>
        /// <returns>
        /// Detached C1 path and image evidence, without packet or publication authority.
        /// </returns>
        internal SpiritualC1ImageInventory ReadC1ImageInventory(
            FileSystemManager.CanonicalWriteLease lease)
        {
            if (!_gate.Wait(0))
                throw new InvalidOperationException("The original capture is continuing.");
            try
            {
                EnsureCurrent(lease);
                return ReadC1ImageInventoryCore();
            }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// Reads the exact C1 image inventory while the caller already owns the capture gate.
        /// </summary>
        /// <returns>
        /// Detached registered paths and owner rollback images.
        /// </returns>
        private SpiritualC1ImageInventory ReadC1ImageInventoryCore()
        {
                var registered = _draftInputs.PathInventory
                    .Concat(WoundAcceptedTurnSnapshotContract.RequiredPaths)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static path => path, StringComparer.Ordinal)
                    .ToArray();
                var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (registered.Any(path => !PendingTurnSnapshotAuthority.IsSafeRelativePath(path) ||
                    !aliases.Add(path)) || _input.BeforeImages.Keys.Any(path =>
                    SpiritualOriginalDraftInputs.IsDraftPath(path) &&
                    !_draftInputs.PathInventory.Contains(path, StringComparer.Ordinal)))
                    throw new InvalidOperationException("The C1 path inventory contains an unsafe or aliased path.");
                var pathSet = registered.ToHashSet(StringComparer.Ordinal);
                var images = _input.BeforeImages
                    .Where(pair => pathSet.Contains(pair.Key))
                    .ToDictionary(pair => pair.Key,
                        pair => new CanonicalBeforeImage(pair.Value.Existed, pair.Value.Bytes),
                        StringComparer.Ordinal);
                foreach (var path in _draftInputs.PathInventory)
                {
                    var original = _draftInputs.ReadImage(path);
                    if (images.TryGetValue(path, out var ownerImage))
                    {
                        if (ownerImage.Fingerprint != original.Fingerprint)
                            throw new InvalidOperationException(
                                "The C1 rollback image disagrees with its frozen physical draft.");
                        continue;
                    }
                    images.Add(path, new CanonicalBeforeImage(original.Existed, original.Bytes));
                }
                foreach (var path in registered.Where(path =>
                    !SpiritualOriginalDraftInputs.IsDraftPath(path) &&
                    path != SpiritualWoundCaptureCheckpointState.StatePath &&
                    path != SpiritualWoundDecisionPendingState.StatePath))
                {
                    if (!_physicalWitnesses.TryGetValue(path, out var physical))
                        throw new InvalidOperationException(
                            $"The non-draft C1 rollback path '{path}' has no physical witness.");
                    if (images.TryGetValue(path, out var ownerImage) &&
                        ownerImage.Fingerprint != physical.Fingerprint)
                        throw new InvalidOperationException(
                            $"The non-draft C1 rollback path '{path}' disagrees with its physical witness: " +
                            $"owner={ownerImage.Fingerprint}, physical={physical.Fingerprint}.");
                    images[path] = new CanonicalBeforeImage(physical.Existed, physical.Bytes);
                }
                foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                    SpiritualWoundDecisionPendingState.StatePath })
                {
                    if (!_signedServiceRollbackImages.TryGetValue(path, out var signed) ||
                        images.TryGetValue(path, out var ownerImage) &&
                        ownerImage.Fingerprint != signed.Fingerprint)
                        throw new InvalidOperationException(
                            "The private C1 rollback image lacks its signed original owner.");
                    images[path] = new CanonicalBeforeImage(signed.Existed, signed.Bytes);
                }
                return new(Array.AsReadOnly(registered),
                    new ReadOnlyDictionary<string, CanonicalBeforeImage>(images));
        }
    }
}
