using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    internal sealed partial class ReservedValidationReadSet
    {
        private readonly CleanupProofReadWindow? _cleanupWindow;
        private readonly ValidationOperation? _cleanupData;
        private readonly Dictionary<(string Path, ValidationSnapshotFamily Family, bool Explicit), object> _cleanupKeys = new();
        private static readonly OperationComputation<CanonicalFileReadSnapshot?> CleanupSnapshot =
            new("CleanupCompletedProof.FileSnapshot", 1, Copy);

        /// <summary>
        /// Interns only an opaque reader identity; payload bytes belong exclusively to the existing service.
        /// </summary>
        /// <param name="key">
        /// Exact raw path, reader family and explicit-lease policy.
        /// </param>
        /// <returns>
        /// Stable operation-local identity, never shared across completed-proof windows.
        /// </returns>
        private object CleanupKey((string Path, ValidationSnapshotFamily Family, bool Explicit) key)
        {
            if (_cleanupKeys.TryGetValue(key, out var identity)) return identity;
            identity = new object();
            _cleanupKeys.Add(key, identity); _order.Add(key);
            return identity;
        }

        /// <summary>
        /// Reads synchronous DATA through the existing temporary computation service.
        /// </summary>
        /// <param name="key">
        /// Original reader identity.
        /// </param>
        /// <param name="declaredLease">
        /// Exact original explicit lease, or <see langword="null"/> for original ambient semantics.
        /// </param>
        /// <returns>
        /// Detached source DATA; original source and dependency failures propagate.
        /// </returns>
        private CanonicalFileReadSnapshot? ReadCleanupData(
            (string Path, ValidationSnapshotFamily Family, bool Explicit) key, CanonicalWriteLease? declaredLease)
        {
            var computed = false;
            var task = _cleanupData!.GetOrComputeAsync(CleanupSnapshot, CleanupKey(key), [_cleanupWindow!], _ =>
            {
                computed = true;
                using var source = _files.MeasureOperation("ValidationReadSet.SourceSync");
                return Task.FromResult(_files.ReadValidationSource(key.Path, key.Family, declaredLease));
            });
            if (!task.IsCompleted) throw new InvalidOperationException("Synchronous cleanup DATA did not settle synchronously.");
            var snapshot = task.GetAwaiter().GetResult();
            if (!computed) { using var reused = _files.MeasureOperation("ValidationReadSet.ReuseSync"); }
            _cleanupWindow!.RequireCurrent();
            return snapshot;
        }

        /// <summary>
        /// Reads asynchronous DATA through the same service while preserving original cancellation.
        /// </summary>
        /// <param name="key">
        /// Original reader identity, including its asynchronous family.
        /// </param>
        /// <param name="declaredLease">
        /// Exact original explicit lease, or <see langword="null"/> for ambient semantics.
        /// </param>
        /// <param name="token">
        /// Original token, checked on hits as well as physical reads.
        /// </param>
        /// <returns>
        /// Detached source DATA or original absence, after live dependency checks.
        /// </returns>
        private async Task<CanonicalFileReadSnapshot?> ReadCleanupDataAsync(
            (string Path, ValidationSnapshotFamily Family, bool Explicit) key, CanonicalWriteLease? declaredLease,
            CancellationToken token)
        {
            var computed = false;
            var snapshot = await _cleanupData!.GetOrComputeAsync(CleanupSnapshot, CleanupKey(key), [_cleanupWindow!], async ct =>
            {
                computed = true;
                using var source = _files.MeasureOperation("ValidationReadSet.SourceAsync");
                return await _files.ReadValidationSourceAsync(key.Path, key.Family, declaredLease, ct);
            }, token);
            if (!computed) { using var reused = _files.MeasureOperation("ValidationReadSet.ReuseAsync"); }
            _cleanupWindow!.RequireCurrent();
            return snapshot;
        }

        /// <summary>
        /// Retrieves the retained original without ever recomputing a missing completion baseline.
        /// </summary>
        /// <param name="key">
        /// Successfully observed original identity whose fresh source will be compared.
        /// </param>
        /// <returns>
        /// Detached retained DATA, including cached absence; a missing or retired baseline is refused.
        /// </returns>
        private CanonicalFileReadSnapshot? RetainedSnapshot(
            (string Path, ValidationSnapshotFamily Family, bool Explicit) key)
        {
            if (_cleanupWindow == null) return _snapshots[key];
            var task = _cleanupData!.TryGetAsync(CleanupSnapshot, _cleanupKeys[key], [_cleanupWindow]);
            if (!task.IsCompleted) throw new InvalidOperationException("Cleanup baseline lookup did not settle synchronously.");
            var lookup = task.GetAwaiter().GetResult();
            _cleanupWindow.RequireCurrent();
            if (!lookup.Found) throw new InvalidOperationException("Cleanup source completion baseline is unavailable.");
            return lookup.Value;
        }
    }
}
