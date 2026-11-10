namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    internal sealed partial class SourceDataOperation
    {
        private object? _directoryBatchWork;

        /// <summary>
        /// Opens a directory epoch confined to the currently admitted source computation or fence.
        /// </summary>
        /// <returns>
        /// Empty bounded DATA, or <see langword="null"/> for original fresh classification.
        /// </returns>
        private SourceValidationBatch? BeginDirectoryBatch()
        {
            var work = new object();
            lock (_gate) _directoryBatchWork = work;
            return _lease is { } lease ? _files.BeginSourceValidationBatch(lease, this, work) : null;
        }

        /// <inheritdoc />
        bool ISourceValidationBatchOwner.TryCaptureBatchVersion(object work, out object? version)
        {
            lock (_gate)
            {
                version = null;
                if (!_busy || !ReferenceEquals(work, _directoryBatchWork) || _closed || _failed || _completed || _suspended != 0)
                    return false;
            }
            return TryRootVersion(out version) && ReferenceEquals(version, _rootVersion);
        }
    }

    internal sealed partial class ReservedValidationReadSet
    {
        private object? _directoryBatchWork;

        /// <summary>
        /// Opens a directory epoch under the existing reservation, excluding prepared active-mutation windows.
        /// </summary>
        /// <returns>
        /// Empty bounded DATA, or <see langword="null"/> for the original fresh route.
        /// </returns>
        private SourceValidationBatch? BeginDirectoryBatch()
        {
            var work = new object();
            lock (_state) _directoryBatchWork = work;
            return _files.BeginSourceValidationBatch(_lease, this, work);
        }

        /// <inheritdoc />
        bool ISourceValidationBatchOwner.TryCaptureBatchVersion(object work, out object? version)
        {
            lock (_state)
            {
                version = null;
                if (!_busy || !ReferenceEquals(work, _directoryBatchWork) || !_opened || _closed || _failed ||
                    _cleanupWindow != null || _reservation.Closed ||
                    !ReferenceEquals(_lease.AmbientRegistration, _registration) ||
                    !_files.IsBorrowedValidationReadFlowCurrent(_lease, _entryAdmission)) return false;
            }
            var root = _files.CanonicalRootAuthorityIdentity;
            lock (root.WorkerContextGate)
                return root.WorkerContext == null && _owner.TryCaptureRecoveryValidationVersion(_lease, _reservation, out version);
        }
    }
}
