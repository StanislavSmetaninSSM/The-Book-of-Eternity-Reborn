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
        ValidationDiagnosticScopes.Value++;
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
        CanonicalWriteLease lease, Func<Task> validate) =>
        ValidationReuseSuppression.Value != 0 || _recoveryValidation.Value is not { } scope
            ? validate() : scope.ValidateAsync(rule, inputIdentity, lease, validate);

    /// <summary>
    /// Clears reuse around an authority-bearing source fence or arbitrary callback.
    /// </summary>
    /// <returns>
    /// A restoring fresh scope, or null outside recovery validation.
    /// </returns>
    internal IDisposable? ForceFreshRecoveryValidation() => _recoveryValidation.Value?.ForceFresh();

    /// <summary>
    /// Runs an arbitrary callback inside a fresh boundary for its complete asynchronous lifetime.
    /// </summary>
    /// <param name="callback">
    /// Existing callback whose result and failure must remain unchanged.
    /// </param>
    /// <returns>
    /// Completion after the callback settles and its fresh boundary is closed.
    /// </returns>
    private async Task InvokeRecoveryValidationCallbackAsync(Func<Task> callback)
    {
        using var fresh = ForceFreshRecoveryValidation();
        await callback();
    }

    /// <summary>
    /// Revokes source reuse after a live authority or source failure without masking it.
    /// </summary>
    internal void InvalidateRecoveryValidationAfterFailure() => _recoveryValidation.Value?.CloseCache();

    /// <summary>
    /// Owns the general service context for one original physical publication lease.
    /// </summary>
    internal sealed class RecoveryValidationScope : IDisposable, IOperationDependency
    {
        private readonly FileSystemManager _files;
        private readonly ValidationOperation _operation = new DefaultOperationValidationService().BeginOperation();
        private readonly CanonicalWriteLease _lease;
        private readonly AmbientCanonicalLeaseRegistration? _registration;
        private readonly long _generationRevision;
        private readonly bool _reuseEnabled;
        private readonly IOperationDependency[] _dependencies;
        private int _closed;
        private volatile bool _failed;

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
            _lease = lease;
            _registration = lease.AmbientRegistration;
            _generationRevision = files.CanonicalRootAuthorityIdentity.SessionGenerationRevision;
            _reuseEnabled = reuseEnabled && files.SupportsRecoveryDataReuse();
            _dependencies = [this];
        }

        /// <summary>
        /// Performs the original body or obtains its fact from this exact operation's general service.
        /// </summary>
        /// <param name="rule">
        /// Stable identity of the source validation algorithm.
        /// </param>
        /// <param name="inputIdentity">
        /// Exact private immutable input identity, never a caller assertion of authority.
        /// </param>
        /// <param name="lease">
        /// Original requested lease; another lease cannot inherit this operation's facts.
        /// </param>
        /// <param name="validate">
        /// Actual source body, invoked on a miss or fresh unsupported route.
        /// </param>
        /// <returns>
        /// Completion after the original source contract succeeds; failures retire reuse.
        /// </returns>
        internal async Task ValidateAsync(OperationValidationRule rule, object inputIdentity,
            CanonicalWriteLease lease, Func<Task> validate)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _closed) != 0, this);
            using var request = _files.MeasureOperation("RecoveryValidation.Request." + rule.Computation.Name);
            var sourceExecuted = false;
            async Task Source()
            {
                sourceExecuted = true;
                using var timing = _files.MeasureOperation("RecoveryValidation.Source." + rule.Computation.Name);
                await validate();
            }
            try
            {
                if (!ReferenceEquals(lease, _lease) || !TryCaptureVersion(out _))
                {
                    using var fresh = _files.MeasureOperation("RecoveryValidation.Fresh." + rule.Computation.Name);
                    await Source();
                }
                else
                    await _operation.ValidateAsync(rule, inputIdentity, _dependencies, _ => Source());
                if (!sourceExecuted)
                {
                    using var reuse = _files.MeasureOperation("RecoveryValidation.Reuse." + rule.Computation.Name);
                }
            }
            catch { CloseCache(); throw; }
        }

        /// <summary>
        /// Captures the exact live original segment and a quiescent root version without filesystem work.
        /// </summary>
        /// <param name="version">
        /// Receives a never-reused root token on success, otherwise null.
        /// </param>
        /// <returns>
        /// True only while this operation, registration, generation and physical lease remain eligible.
        /// </returns>
        public bool TryCaptureVersion(out object? version)
        {
            version = null;
            if (!_reuseEnabled || _failed || Volatile.Read(ref _closed) != 0 ||
                ValidationReuseSuppression.Value != 0 || !_lease.IsActive ||
                _registration is null || _registration.Inactive || !ReferenceEquals(_lease.AmbientRegistration, _registration) ||
                _files.CanonicalRootAuthorityIdentity.SessionGenerationRevision != _generationRevision)
                return false;
            ValidationReadReservation? compatibleRead = null;
            if (_files._validationReadSet.Value is { } readSet &&
                !readSet.TryGetActiveReservation(_files, _lease, out compatibleRead)) return false;
            var root = _files.CanonicalRootAuthorityIdentity;
            lock (root.WorkerContextGate)
            {
                if (root.WorkerContext is not null || _lease.MainAdmission is not { } admission) return false;
                return admission.TryCaptureRecoveryValidationVersion(_lease, compatibleRead, out version);
            }
        }

        /// <summary>
        /// Clears prior successes and forces all nested DATA bodies to run during a source boundary.
        /// </summary>
        /// <returns>
        /// A restoring scope, or null after a prior failure has permanently disabled reuse.
        /// </returns>
        internal IDisposable? ForceFresh()
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _closed) != 0, this);
            return _failed ? null : _operation.ForceFresh();
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
            _failed = true;
            try { _operation.Dispose(); }
            catch { /* Cleanup cannot replace the original validation refusal. */ }
        }

        /// <summary>
        /// Clears all operation-owned results and removes only this caller-flow registration.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0) return;
            try { _operation.Dispose(); }
            finally
            {
                if (ReferenceEquals(_files._recoveryValidation.Value, this))
                {
                    _files._recoveryValidation.Value = null;
                    ValidationDiagnosticScopes.Value--;
                }
            }
        }
    }

    /// <summary>
    /// Excludes callback routes whose arbitrary effects cannot be represented by the root version protocol.
    /// </summary>
    /// <returns>
    /// True for the physical original route with supported passive diagnostics and bracketed callbacks.
    /// </returns>
    private bool SupportsRecoveryDataReuse() =>
        ReferenceEquals(_loadTransactionOperations, PhysicalLoadTransactionOperations.Instance) &&
        _hooks?.PathMetadataQueryObserver is null &&
        _hooks?.BeforeCanonicalReadOpenAsync is null && _hooks?.AfterCanonicalReadInitialValidationAsync is null &&
        _hooks?.BeforeRuntimeFileReadOpenAsync is null && _hooks?.AfterRuntimeFileReadOpenedAsync is null &&
        _hooks?.AfterRuntimeFileReadInitialValidationAsync is null && _hooks?.AfterExactPhysicalReadInitialValidationAsync is null &&
        _hooks?.AfterPhysicalFileAuthorityValidatedAsync is null && _hooks?.AfterCanonicalReadAttemptAsync is null &&
        _hooks?.BeforeCanonicalExistenceFollowUpProbeAsync is null && _hooks?.BeforeMainBorrowRetainAsync is null &&
        _hooks?.BeforeCanonicalWriteLockOpenAsync is null && _hooks?.BeforeSessionLifecycleLockOpenAsync is null &&
        _hooks?.AfterSessionLifecycleLockOpenedAsync is null && _hooks?.SessionLifecycleLockContendedAsync is null &&
        _hooks?.AfterBrowserOriginalPreflightAsync is null && _hooks?.BrowserRecoveryJournalSelectedObserver is null &&
        _hooks?.BeforeRuntimeFileCreateAsync is null && _hooks?.AfterRuntimeMutationBoundaryValidatedAsync is null &&
        _hooks?.BeforeLoadDirectoryMoveAsync is null && _hooks?.AfterLoadStagingFinalValidationAsync is null &&
        _hooks?.AfterLoadDirectoryMoveAsync is null && _hooks?.BeforeLoadPublicationGuardRootDeleteAsync is null &&
        _hooks?.SessionOperationClosingAsync is null;
}
