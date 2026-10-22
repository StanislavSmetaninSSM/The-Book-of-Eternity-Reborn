using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    private readonly AsyncLocal<SourceDataOperation?> _sourceDataOperation = new();

    /// <summary>
    /// Registers an owning source DATA lifetime or borrows the operation already present in this flow.
    /// </summary>
    /// <returns>
    /// A new owner to complete and dispose, or <see langword="null"/> when the existing owner remains responsible.
    /// </returns>
    internal SourceDataOperation? BeginSourceDataOperationIfNeeded()
    {
        if (_sourceDataOperation.Value is { } current)
        {
            current.RequireOpen();
            return null;
        }
        var operation = new SourceDataOperation(this);
        _sourceDataOperation.Value = operation;
        ValidationDiagnosticScopes.Value++;
        return operation;
    }

    /// <summary>
    /// Owns the existing general service storage for one selected source DATA operation.
    /// </summary>
    internal sealed class SourceDataOperation : IDisposable
    {
        private readonly FileSystemManager _files;
        private readonly ValidationOperation _operation = new DefaultOperationValidationService().BeginOperation();
        private bool _closed;

        /// <summary>
        /// Creates an independent operation through the existing service backend.
        /// </summary>
        /// <param name="files">
        /// Exact manager registering this owner in its calling flow.
        /// </param>
        internal SourceDataOperation(FileSystemManager files) => _files = files;

        /// <summary>
        /// Refuses use after the owning operation has closed.
        /// </summary>
        internal void RequireOpen() => ObjectDisposedException.ThrowIf(_closed, this);

        /// <summary>
        /// Verifies pending source dependencies before a caller consumes them under its original lease.
        /// </summary>
        /// <param name="lease">
        /// Active exact current lease; a foreign or retired lease retains its original refusal.
        /// </param>
        /// <returns>
        /// Completion after the source fence succeeds.
        /// </returns>
        internal Task VerifyAsync(CanonicalWriteLease lease)
        {
            RequireOpen();
            _files.EnsureCanonicalWriteLeaseActive(lease);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Completes the owning operation's required successful-return source fence.
        /// </summary>
        /// <returns>
        /// Completion after all retained source dependencies have been verified.
        /// </returns>
        internal Task CompleteAsync()
        {
            RequireOpen();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Captures storage ownership without exposing retained file payloads.
        /// </summary>
        /// <returns>
        /// The existing general operation's entry, dependency and closed counts.
        /// </returns>
        internal ValidationOperationState CaptureState() => _operation.CaptureState();

        /// <summary>
        /// Drops all operation-owned references and removes only this flow's original registration.
        /// </summary>
        public void Dispose()
        {
            if (_closed) return;
            _closed = true;
            try { _operation.Dispose(); }
            finally
            {
                if (ReferenceEquals(_files._sourceDataOperation.Value, this))
                {
                    _files._sourceDataOperation.Value = null;
                    ValidationDiagnosticScopes.Value--;
                }
            }
        }
    }
}
