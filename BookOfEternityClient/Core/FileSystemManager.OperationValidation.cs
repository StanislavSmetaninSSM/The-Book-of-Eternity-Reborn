using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    private readonly AsyncLocal<RecoveryValidationScope?> _recoveryValidation = new();

    /// <summary>
    /// Registers temporary source-validation results for one exact publication lease.
    /// </summary>
    /// <param name="lease">
    /// The original active physical lease, retained without ownership transfer.
    /// </param>
    /// <param name="reuseEnabled">
    /// Whether eligible DATA facts may be reused; false selects the same original source work.
    /// </param>
    /// <returns>
    /// An operation scope disposed before the caller closes the lease.
    /// </returns>
    internal RecoveryValidationScope BeginRecoveryValidation(CanonicalWriteLease lease, bool reuseEnabled = true)
    {
        EnsureCanonicalWriteLeaseActive(lease);
        if (_recoveryValidation.Value is not null)
            throw new InvalidOperationException("A recovery validation operation is already registered.");
        var scope = new RecoveryValidationScope(this, lease, reuseEnabled);
        _recoveryValidation.Value = scope;
        return scope;
    }

    /// <summary>
    /// Dispatches a source-validation fact through the current operation when supported.
    /// </summary>
    /// <param name="rule">
    /// Stable identity of the actual DATA algorithm.
    /// </param>
    /// <param name="inputIdentity">
    /// Exact private proof identity covering the immutable algorithm inputs.
    /// </param>
    /// <param name="lease">
    /// The caller's original physical lease.
    /// </param>
    /// <param name="validate">
    /// Original source work that throws on refusal and grants no new authority.
    /// </param>
    /// <returns>
    /// Completion after a successful current DATA validation.
    /// </returns>
    internal Task ValidateRecoveryDataAsync(OperationValidationRule rule, object inputIdentity,
        CanonicalWriteLease lease, Func<Task> validate) => validate();

    /// <summary>
    /// Clears reuse around an authority-bearing source fence or arbitrary callback.
    /// </summary>
    /// <returns>
    /// A restoring fresh scope, or null outside recovery validation.
    /// </returns>
    internal IDisposable? ForceFreshRecoveryValidation() => null;

    /// <summary>
    /// Revokes source reuse after a live authority or source failure without masking it.
    /// </summary>
    internal void InvalidateRecoveryValidationAfterFailure() => _recoveryValidation.Value?.CloseCache();

    /// <summary>
    /// Owns the general service context for one original physical publication lease.
    /// </summary>
    internal sealed class RecoveryValidationScope : IDisposable
    {
        private readonly FileSystemManager _files;
        private readonly ValidationOperation _operation = new DefaultOperationValidationService().BeginOperation();

        /// <summary>
        /// Retains the exact manager and lease for the caller-owned operation.
        /// </summary>
        /// <param name="files">
        /// Manager registering this scope synchronously in the caller flow.
        /// </param>
        /// <param name="lease">
        /// Original physical lease; this scope never disposes it.
        /// </param>
        /// <param name="reuseEnabled">
        /// Whether eligible source facts may be retained.
        /// </param>
        internal RecoveryValidationScope(FileSystemManager files, CanonicalWriteLease lease, bool reuseEnabled)
        {
            _files = files;
        }

        /// <summary>
        /// Captures actual payload counts without exposing validation data.
        /// </summary>
        /// <returns>
        /// Entry/dependency counts and the context's closed state.
        /// </returns>
        internal ValidationOperationState CaptureState() => _operation.CaptureState();

        /// <summary>
        /// Closes reusable storage after failure while preserving the primary exception.
        /// </summary>
        internal void CloseCache()
        {
            try { _operation.Dispose(); }
            catch { /* Cleanup cannot replace the original validation refusal. */ }
        }

        /// <summary>
        /// Clears all operation-owned results and removes only this caller-flow registration.
        /// </summary>
        public void Dispose()
        {
            _operation.Dispose();
            if (ReferenceEquals(_files._recoveryValidation.Value, this))
                _files._recoveryValidation.Value = null;
        }
    }
}
