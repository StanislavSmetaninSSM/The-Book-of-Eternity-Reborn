using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    private readonly AsyncLocal<RecoveryValidationScope?> _recoveryValidation = new();

    /// <summary>
    /// Delivers every passive metadata record outside functional validation participation.
    /// </summary>
    /// <param name="query">
    /// Actual metadata query attempt retained unchanged for the receiver.
    /// </param>
    private void ObservePassivePathMetadata(PathMetadataQuery query)
    {
        using var diagnostic = SuppressValidationReuse();
        try { _hooks?.PathMetadataQueryObserver?.Invoke(query); }
        catch (Exception) { /* Passive observation cannot replace a filesystem result. */ }
    }

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
    /// Computes detached source DATA for the caller's current recovery segment.
    /// </summary>
    /// <typeparam name="TResult">
    /// The DATA result whose mutable values are detached by <paramref name="computation"/>.
    /// </typeparam>
    /// <param name="computation">
    /// Stable algorithm identity and defensive copy policy.
    /// </param>
    /// <param name="inputIdentity">
    /// Exact private identity covering the unchanged algorithm inputs.
    /// </param>
    /// <param name="lease">
    /// The caller's original physical lease, verified by the source and its live guards.
    /// </param>
    /// <param name="compute">
    /// Original source work that returns DATA and grants no authority.
    /// </param>
    /// <returns>
    /// A detached successful source result; failures are not converted to results.
    /// </returns>
    internal async Task<TResult> ComputeRecoveryDataAsync<TResult>(OperationComputation<TResult> computation,
        object inputIdentity, CanonicalWriteLease lease, Func<Task<TResult>> compute)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(computation);
            ArgumentNullException.ThrowIfNull(inputIdentity);
            ArgumentNullException.ThrowIfNull(compute);
            return ValidationReuseSuppression.Value != 0 || _recoveryValidation.Value is not { } scope
                ? computation.Copy(await compute())
                : await scope.ComputeAsync(computation, inputIdentity, lease, compute);
        }
        catch { InvalidateRecoveryValidationAfterFailure(); throw; }
    }

    /// <summary>
    /// Creates an owned recovery segment only when the current flow has none.
    /// </summary>
    /// <param name="lease">
    /// Original physical lease used by a newly created segment.
    /// </param>
    /// <returns>
    /// A new scope for the caller to dispose, or <see langword="null"/> when borrowing an
    /// existing scope or running outside functional validation in passive diagnostics.
    /// A borrowed scope keeps its original ownership and eligibility rules.
    /// </returns>
    internal RecoveryValidationScope? BeginRecoveryValidationIfNeeded(CanonicalWriteLease lease) =>
        ValidationReuseSuppression.Value != 0 || _recoveryValidation.Value is not null
            ? null : BeginRecoveryValidation(lease);

    /// <summary>
    /// Clears reuse around an authority-bearing source fence or arbitrary callback.
    /// </summary>
    /// <returns>
    /// A restoring fresh scope, or null outside functional recovery validation and in passive diagnostic routing.
    /// </returns>
    internal IDisposable? ForceFreshRecoveryValidation()
    {
        if (ValidationReuseSuppression.Value != 0) return null;
        var source = _sourceDataOperation.Value?.Suspend();
        try { return new RecoverySourceFreshBoundary(source, _recoveryValidation.Value?.ForceFresh()); }
        catch { source?.Dispose(); throw; }
    }

    /// <summary>
    /// Retires both independent DATA caches across the same original fresh protocol.
    /// </summary>
    /// <param name="source">
    /// Whole-operation source bypass, or no active source owner.
    /// </param>
    /// <param name="facts">
    /// Original semantic fresh scope, or no active semantic owner.
    /// </param>
    private sealed class RecoverySourceFreshBoundary(IDisposable? source, IDisposable? facts) : IDisposable
    {
        /// <summary>
        /// Restores both routing scopes even when the original scope refuses disposal.
        /// </summary>
        public void Dispose()
        {
            try { facts?.Dispose(); }
            finally { source?.Dispose(); }
        }
    }

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
    internal void InvalidateRecoveryValidationAfterFailure()
    {
        if (ValidationReuseSuppression.Value == 0)
        {
            _recoveryValidation.Value?.CloseCache();
            _sourceDataOperation.Value?.Fail();
        }
    }

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
        private readonly Guid _diagnosticId = Guid.NewGuid();
        private readonly (long Begins, long Ends, int Active) _initialActivity;
        private long _requests;
        private long _sources;
        private long _reuses;
        private long _fallbacks;
        private long _freshSources;
        private long _freshBoundaries;
        private int _freshDepth;
        private int _failureClosures;

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
            _initialActivity = files.CanonicalRootAuthorityIdentity.ValidationEpoch.CaptureActivity();
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
            Interlocked.Increment(ref _requests);
            var sourceExecuted = false;
            var fallback = false;
            async Task Source()
            {
                sourceExecuted = true;
                Interlocked.Increment(ref _sources);
                if (fallback || Volatile.Read(ref _freshDepth) != 0) Interlocked.Increment(ref _freshSources);
                using var timing = _files.MeasureOperation("RecoveryValidation.Source." + rule.Computation.Name);
                await validate();
            }
            try
            {
                if (!ReferenceEquals(lease, _lease) || !TryCaptureVersion(out _))
                {
                    fallback = true;
                    Interlocked.Increment(ref _fallbacks);
                    using var fresh = _files.MeasureOperation("RecoveryValidation.Fresh." + rule.Computation.Name);
                    await Source();
                }
                else
                    await _operation.ValidateAsync(rule, inputIdentity, _dependencies, _ => Source());
                if (!sourceExecuted)
                {
                    Interlocked.Increment(ref _reuses);
                    using var reuse = _files.MeasureOperation("RecoveryValidation.Reuse." + rule.Computation.Name);
                }
            }
            catch { CloseCache(); throw; }
        }

        /// <summary>
        /// Computes detached DATA or reuses it under this segment's exact live root version.
        /// </summary>
        /// <typeparam name="TResult">
        /// The result type detached by the registered computation.
        /// </typeparam>
        /// <param name="computation">
        /// Stable algorithm descriptor and defensive copy policy.
        /// </param>
        /// <param name="inputIdentity">
        /// Exact private identity covering the unchanged DATA inputs.
        /// </param>
        /// <param name="lease">
        /// Original requested physical lease; a different lease selects fresh source work.
        /// </param>
        /// <param name="compute">
        /// Actual source computation, invoked on a miss or unsupported fresh route.
        /// </param>
        /// <returns>
        /// A detached result after current source validation; errors retire reusable state.
        /// </returns>
        internal async Task<TResult> ComputeAsync<TResult>(OperationComputation<TResult> computation,
            object inputIdentity, CanonicalWriteLease lease, Func<Task<TResult>> compute)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _closed) != 0, this);
            using var request = _files.MeasureOperation("RecoveryValidation.Request." + computation.Name);
            Interlocked.Increment(ref _requests);
            var sourceExecuted = false;
            var fallback = false;
            async Task<TResult> Source()
            {
                sourceExecuted = true;
                Interlocked.Increment(ref _sources);
                if (fallback || Volatile.Read(ref _freshDepth) != 0) Interlocked.Increment(ref _freshSources);
                using var timing = _files.MeasureOperation("RecoveryValidation.Source." + computation.Name);
                return await compute();
            }
            try
            {
                TResult result;
                if (!ReferenceEquals(lease, _lease) || !TryCaptureVersion(out _))
                {
                    fallback = true;
                    Interlocked.Increment(ref _fallbacks);
                    using var fresh = _files.MeasureOperation("RecoveryValidation.Fresh." + computation.Name);
                    result = computation.Copy(await Source());
                }
                else
                    result = await _operation.GetOrComputeAsync(computation, inputIdentity, _dependencies, _ => Source());
                if (!sourceExecuted)
                {
                    Interlocked.Increment(ref _reuses);
                    using var reuse = _files.MeasureOperation("RecoveryValidation.Reuse." + computation.Name);
                }
                return result;
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
            Interlocked.Increment(ref _freshBoundaries);
            return _failed ? null : new RecoveryFreshBoundary(this, _operation.ForceFresh());
        }

        /// <summary>
        /// Captures actual payload counts without exposing validation data.
        /// </summary>
        /// <returns>
        /// Entry/dependency counts and the context's closed state.
        /// </returns>
        internal ValidationOperationState CaptureState() => _operation.CaptureState();

        /// <summary>
        /// Captures actual request, execution and invalidation observations without accessing retained DATA.
        /// </summary>
        /// <returns>
        /// Counter observations for this segment; root transitions include other managers of the same root.
        /// </returns>
        internal RecoveryValidationMetrics CaptureMetrics()
        {
            var activity = _files.CanonicalRootAuthorityIdentity.ValidationEpoch.CaptureActivity();
            return new RecoveryValidationMetrics
            {
                OperationId = _diagnosticId,
                Requests = Interlocked.Read(ref _requests), Sources = Interlocked.Read(ref _sources),
                Reuses = Interlocked.Read(ref _reuses), FreshFallbacks = Interlocked.Read(ref _fallbacks),
                FreshSources = Interlocked.Read(ref _freshSources), FreshBoundaries = Interlocked.Read(ref _freshBoundaries),
                FailureClosures = Volatile.Read(ref _failureClosures),
                MutationBegins = unchecked(activity.Begins - _initialActivity.Begins),
                MutationEnds = unchecked(activity.Ends - _initialActivity.Ends), ActiveMutations = activity.Active,
                GenerationChanged = _files.CanonicalRootAuthorityIdentity.SessionGenerationRevision != _generationRevision,
                Closed = Volatile.Read(ref _closed) != 0, State = _operation.CaptureState()
            };
        }

        /// <summary>
        /// Delivers complete operation counters outside every authority gate and isolates passive receiver failures.
        /// </summary>
        private void EmitDiagnostics()
        {
            if (_files._hooks?.RecoveryValidationObserver is not { } observer) return;
            using var diagnostic = SuppressValidationReuse();
            using var timing = _files.MeasureOperation("RecoveryValidation.ExportDiagnostics");
            try { observer(CaptureMetrics()); }
            catch (Exception) { /* Passive diagnostics cannot replace the operation outcome. */ }
        }

        /// <summary>
        /// Observes the full lifetime of an existing fresh boundary without changing its validation contract.
        /// </summary>
        private sealed class RecoveryFreshBoundary : IDisposable
        {
            private readonly RecoveryValidationScope _owner;
            private readonly IDisposable _inner;
            private int _closed;

            /// <summary>
            /// Records an active fresh boundary after its underlying service has entered it.
            /// </summary>
            /// <param name="owner">
            /// Exact recovery segment receiving scalar diagnostic observations.
            /// </param>
            /// <param name="inner">
            /// Existing general-service boundary whose disposal restores fresh routing.
            /// </param>
            internal RecoveryFreshBoundary(RecoveryValidationScope owner, IDisposable inner)
            {
                _owner = owner;
                _inner = inner;
                Interlocked.Increment(ref owner._freshDepth);
            }

            /// <summary>
            /// Restores the underlying boundary once and then retires its diagnostic depth.
            /// </summary>
            public void Dispose()
            {
                if (Interlocked.Exchange(ref _closed, 1) != 0) return;
                try { _inner.Dispose(); }
                finally { Interlocked.Decrement(ref _owner._freshDepth); }
            }
        }

        /// <summary>
        /// Closes reusable storage after failure while preserving the primary exception.
        /// </summary>
        internal void CloseCache()
        {
            _failed = true;
            Interlocked.CompareExchange(ref _failureClosures, 1, 0);
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
                EmitDiagnostics();
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
