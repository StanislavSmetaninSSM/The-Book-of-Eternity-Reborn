namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    private readonly AsyncLocal<ReservedValidationReadSet?> _validationReadSet = new();
    private static readonly AsyncLocal<int> ValidationReuseSuppression = new();
    private static readonly AsyncLocal<int> ValidationDiagnosticScopes = new();

    /// <summary>
    /// Prevents diagnostic receiver reads and their asynchronous descendants from joining a functional read set.
    /// </summary>
    /// <returns>
    /// A scope restoring the previous diagnostic routing value, or <see langword="null"/> outside validation routing.
    /// Diagnostics themselves remain enabled.
    /// </returns>
    private static IDisposable? SuppressValidationReuse() =>
        ValidationDiagnosticScopes.Value == 0 && ValidationReuseSuppression.Value == 0
            ? null : new ValidationDiagnosticSuppression();

    /// <summary>
    /// Restores flowing diagnostic suppression without providing filesystem authority.
    /// </summary>
    private sealed class ValidationDiagnosticSuppression : IDisposable
    {
        private readonly int _previous = ValidationReuseSuppression.Value;
        private bool _closed;

        /// <summary>
        /// Suppresses reuse in the current execution context and its captured descendants.
        /// </summary>
        internal ValidationDiagnosticSuppression() => ValidationReuseSuppression.Value = _previous + 1;

        /// <summary>
        /// Restores the caller's routing value once.
        /// </summary>
        public void Dispose()
        {
            if (_closed) return;
            _closed = true;
            ValidationReuseSuppression.Value = _previous;
        }
    }

    /// <summary>
    /// Issues one optional read set for an ordinary, exclusively reserved validation phase.
    /// </summary>
    /// <param name="lease">
    /// Exact caller-owned canonical lease, retained without acquisition or ownership transfer.
    /// </param>
    /// <returns>
    /// An unopened scope requiring completion and disposal, or <see langword="null"/> for fresh unsupported execution.
    /// </returns>
    internal ReservedValidationReadSet? TryBeginReservedValidationReadSet(CanonicalWriteLease lease) =>
        TryBeginReservedValidationReadSetCore(lease, preserveOriginalAdmission: false);

    /// <summary>
    /// Issues the existing reservation with a privately selected per-request admission policy.
    /// </summary>
    /// <param name="lease">
    /// Exact caller-owned canonical lease retained without ownership transfer.
    /// </param>
    /// <param name="preserveOriginalAdmission">
    /// Whether the stable post-recovery helper retains original reader admission without an added full lease check.
    /// Both values freshly validate opening, every original physical source and completion.
    /// </param>
    /// <returns>
    /// An unopened scope, or <see langword="null"/> for the original fresh fallback.
    /// </returns>
    private ReservedValidationReadSet? TryBeginReservedValidationReadSetCore(CanonicalWriteLease lease,
        bool preserveOriginalAdmission)
    {
        // Arbitrary read callbacks can change sources between observations. Decide before activation.
        if (_validationReadSet.Value != null || ValidationReuseSuppression.Value != 0 || !CanReuseSourceData) return null;
        var reservation = TryBeginBorrowedValidationReservation(lease);
        if (reservation == null) return null;
        var scope = new ReservedValidationReadSet(this, lease, reservation, preserveOriginalAdmission);
        _validationReadSet.Value = scope;
        ValidationDiagnosticScopes.Value++;
        return scope;
    }

    /// <summary>
    /// Selects the original reader contract independently of path spelling and explicit lease mode.
    /// </summary>
    internal enum ValidationSnapshotFamily { OrdinarySync, OrdinaryAsync, ProtectedSync, ProtectedAsync }

    /// <summary>
    /// Determines whether arbitrary source callbacks require the unchanged uncached reader route.
    /// </summary>
    private bool CanReuseSourceData =>
        _hooks?.PathMetadataQueryObserver == null &&
        _hooks?.BeforeCanonicalReadOpenAsync == null && _hooks?.AfterCanonicalReadInitialValidationAsync == null &&
        _hooks?.BeforeRuntimeFileReadOpenAsync == null && _hooks?.AfterRuntimeFileReadOpenedAsync == null &&
        _hooks?.AfterRuntimeFileReadInitialValidationAsync == null && _hooks?.AfterExactPhysicalReadInitialValidationAsync == null &&
        _hooks?.AfterPhysicalFileAuthorityValidatedAsync == null && _hooks?.AfterCanonicalReadAttemptAsync == null &&
        _hooks?.BeforeCanonicalExistenceFollowUpProbeAsync == null;

    /// <summary>
    /// Dispatches protected synchronous snapshot DATA while preserving the original declared lease identity.
    /// </summary>
    /// <param name="relativePath">
    /// Raw relative path spelling used by the source reader.
    /// </param>
    /// <param name="lease">
    /// Original declared lease, or <see langword="null"/> for lease-free semantics.
    /// </param>
    /// <returns>
    /// Detached content/timestamp or observed absence.
    /// </returns>
    private CanonicalFileReadSnapshot? ReadFileSnapshotCore(string relativePath, CanonicalWriteLease? lease = null) =>
        _validationReadSet.Value is { } phase && ValidationReuseSuppression.Value == 0
            ? phase.Read(relativePath, ValidationSnapshotFamily.ProtectedSync, lease)
            : _sourceDataOperation.Value is { } operation && ValidationReuseSuppression.Value == 0
                ? operation.Read(relativePath, ValidationSnapshotFamily.ProtectedSync, lease)
                : ReadFileSnapshotUncached(relativePath);

    /// <summary>
    /// Dispatches protected asynchronous snapshot DATA while retaining original cancellation and lease identity.
    /// </summary>
    /// <param name="relativePath">
    /// Raw relative spelling used by the unchanged source reader.
    /// </param>
    /// <param name="cancellationToken">
    /// Original cancellation token checked for both fresh and reused reads.
    /// </param>
    /// <param name="lease">
    /// Declared lease, or <see langword="null"/> for the original lease-free contract.
    /// </param>
    /// <returns>
    /// Detached snapshot or observed absence.
    /// </returns>
    private Task<CanonicalFileReadSnapshot?> ReadFileSnapshotCoreAsync(string relativePath,
        CancellationToken cancellationToken, CanonicalWriteLease? lease = null) =>
        _validationReadSet.Value is { } phase && ValidationReuseSuppression.Value == 0
            ? phase.ReadAsync(relativePath, ValidationSnapshotFamily.ProtectedAsync, lease, cancellationToken)
            : _sourceDataOperation.Value is { } operation && ValidationReuseSuppression.Value == 0
                ? operation.ReadAsync(relativePath, ValidationSnapshotFamily.ProtectedAsync, lease, cancellationToken)
                : ReadFileSnapshotUncachedAsync(relativePath, cancellationToken);

    /// <summary>
    /// Invokes a passive physical-read activity receiver without allowing its reads to join a functional read set.
    /// </summary>
    /// <param name="observer">
    /// Receiver whose errors are isolated, or <see langword="null"/> to disable this observation.
    /// </param>
    /// <param name="path">
    /// Original relative or exact physical spelling supplied by the actual read.
    /// </param>
    private static void ObservePassiveRead(Action<string>? observer, string path)
    {
        if (observer == null) return;
        using var diagnostic = SuppressValidationReuse();
        try { observer(path); }
        catch (Exception) { /* Passive observation must not change the read result. */ }
    }

    /// <summary>
    /// Invokes the original synchronous source reader without entering the read-set dispatch again.
    /// </summary>
    /// <param name="path">
    /// Original, unnormalized relative spelling.
    /// </param>
    /// <param name="family">
    /// The synchronous family whose original semantics must be retained.
    /// </param>
    /// <param name="lease">
    /// Original declared lease, or <see langword="null"/> for a lease-free read.
    /// </param>
    /// <returns>
    /// Fresh content and timestamp, or <see langword="null"/> for original observed absence.
    /// </returns>
    private CanonicalFileReadSnapshot? ReadValidationSource(string path, ValidationSnapshotFamily family, CanonicalWriteLease? lease) =>
        family switch
        {
            ValidationSnapshotFamily.OrdinarySync => ReadOrdinaryFileSnapshotCore(path, lease, out _, nullableDataFailure: false),
            ValidationSnapshotFamily.ProtectedSync => ReadFileSnapshotUncached(path),
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };

    /// <summary>
    /// Invokes the original asynchronous source reader without entering read-set dispatch again.
    /// </summary>
    /// <param name="path">
    /// Original, unnormalized relative spelling.
    /// </param>
    /// <param name="family">
    /// Asynchronous reader family selected by the original caller.
    /// </param>
    /// <param name="lease">
    /// Original declared lease, or <see langword="null"/> for a lease-free read.
    /// </param>
    /// <param name="token">
    /// Original cancellation token, also observed before any reused result.
    /// </param>
    /// <returns>
    /// Fresh content and timestamp, or <see langword="null"/> for original observed absence.
    /// </returns>
    private Task<CanonicalFileReadSnapshot?> ReadValidationSourceAsync(string path, ValidationSnapshotFamily family,
        CanonicalWriteLease? lease, CancellationToken token) =>
        family switch
        {
            ValidationSnapshotFamily.OrdinaryAsync => ReadOrdinaryFileSnapshotUncachedAsync(path, token, lease),
            ValidationSnapshotFamily.ProtectedAsync => ReadFileSnapshotUncachedAsync(path, token),
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };

    /// <summary>
    /// Retains private source DATA for one exact reserved owner and proves every dependency freshly before success.
    /// </summary>
    internal sealed partial class ReservedValidationReadSet : IAsyncDisposable
    {
        private readonly FileSystemManager _files;
        private readonly CanonicalWriteLease _lease;
        private readonly MainAdmission _owner;
        private readonly MainAdmission _entryAdmission;
        private readonly AmbientCanonicalLeaseRegistration _registration;
        private readonly PendingSnapshotValidationReadScope _reservation;
        private readonly bool _preserveOriginalAdmission;
        private readonly Dictionary<(string Path, ValidationSnapshotFamily Family, bool Explicit), CanonicalFileReadSnapshot?> _snapshots = new();
        private readonly List<(string Path, ValidationSnapshotFamily Family, bool Explicit)> _order = [];
        private readonly object _state = new();
        private LocalSessionGenerationSnapshot? _generation;
        private bool _opened, _busy, _failed, _completing, _closed;
        private TaskCompletionSource? _drained;
        private Task? _closeTask;

        /// <summary>
        /// Binds the exact lease and registration already retained by the successfully issued reservation.
        /// </summary>
        /// <param name="files">
        /// Manager owning every observation and this execution-flow marker.
        /// </param>
        /// <param name="lease">
        /// Original active ordinary lease, never owned by this read set.
        /// </param>
        /// <param name="reservation">
        /// Exact successfully installed borrowed exclusion reservation.
        /// </param>
        /// <param name="preserveOriginalAdmission">
        /// Whether requests retain original reader admission without an added full check; opening and completion remain fresh.
        /// </param>
        /// <param name="cleanupWindow">
        /// Exact publisher-issued DATA version, or null for the original reserved-reader storage policy.
        /// </param>
        internal ReservedValidationReadSet(FileSystemManager files, CanonicalWriteLease lease,
            PendingSnapshotValidationReadScope reservation, bool preserveOriginalAdmission,
            CleanupProofReadWindow? cleanupWindow = null)
        {
            _files = files; _lease = lease; _reservation = reservation;
            _owner = lease.MainAdmission!; _registration = lease.AmbientRegistration!;
            _entryAdmission = reservation.EntryAdmission;
            _preserveOriginalAdmission = preserveOriginalAdmission;
            _cleanupWindow = cleanupWindow;
            if (cleanupWindow != null) _cleanupData = new Services.DefaultOperationValidationService().BeginOperation();
        }

        /// <summary>
        /// Exports only a healthy ready reservation identity for no-IO outer DATA eligibility.
        /// </summary>
        /// <param name="files">
        /// Exact manager required to match this read set.
        /// </param>
        /// <param name="lease">
        /// Exact retained physical lease, never transferred to the caller.
        /// </param>
        /// <param name="reservation">
        /// Receives the ready original reservation on success, otherwise null.
        /// </param>
        /// <returns>
        /// True only after successful opening and before failure, completion, overlap or close.
        /// </returns>
        internal bool TryGetActiveReservation(FileSystemManager files, CanonicalWriteLease lease,
            out ValidationReadReservation? reservation)
        {
            lock (_state)
            {
                reservation = null;
                if (!ReferenceEquals(files, _files) || !ReferenceEquals(lease, _lease) ||
                    !_opened || _failed || _closed || _completing || _busy) return false;
                reservation = _reservation;
                return true;
            }
        }

        /// <summary>
        /// Opens the borrowed reservation and captures the exact existing generation bytes without creating a generation.
        /// </summary>
        /// <returns>
        /// Completion after full original admission and generation validation.
        /// </returns>
        internal async Task OpenAsync()
        {
            Enter(opening: true);
            try
            {
                using var timing = _files.MeasureOperation("ValidationReadSet.Open");
                await _reservation.OpenAsync();
                _generation = _files.ReadLocalGenerationSnapshot(_lease);
                if (_generation.Bytes == null) throw new InvalidDataException("Validation requires an existing generation.");
                _cleanupWindow?.RequireGeneration(_generation);
                RequireAuthority();
                lock (_state) { RequireHealthy(); _opened = true; }
            }
            catch { Fail(); throw; }
            finally { Leave(); }
        }

        /// <summary>
        /// Acquires the single functional operation slot, latching any overlap or retired use as failure.
        /// </summary>
        /// <param name="opening">
        /// Whether this is initial opening rather than a source consumer or completion.
        /// </param>
        private void Enter(bool opening = false)
        {
            lock (_state)
            {
                if (_failed || _closed || _busy || (opening ? _opened : !_opened) || _completing)
                { _failed = true; throw new InvalidOperationException("Reserved validation read set is unavailable or overlapping."); }
                _busy = true;
            }
        }

        /// <summary>
        /// Releases the operation slot after all source work and result validation have ended.
        /// </summary>
        private void Leave()
        {
            lock (_state)
            {
                _busy = false;
                _drained?.TrySetResult();
            }
        }

        /// <summary>
        /// Invalidates reuse after any admitted operation fails.
        /// </summary>
        private void Fail() { lock (_state) _failed = true; }

        /// <summary>
        /// Refuses a failure recorded by this operation, an overlapping caller or disposal.
        /// </summary>
        private void RequireHealthy()
        {
            if (_failed || _closed) throw new InvalidOperationException("Reserved validation read set lost its original lifetime.");
        }

        /// <summary>
        /// Checks the exact registration and full physical/main/worker admission without reusing admission observations.
        /// </summary>
        private void RequireAuthority()
        {
            using var bypass = SuppressValidationReuse();
            RequireRetainedFlow();
            _lease.EnsureNoPendingLocalDecision();
            _files.EnsureValidCanonicalWriteLease(_lease);
        }

        /// <summary>
        /// Checks every request's retained flow and pending decision while preserving its selected reader admission.
        /// </summary>
        private void RequireReadAuthority()
        {
            if (_preserveOriginalAdmission)
            {
                RequireRetainedFlow();
                _lease.EnsureNoPendingLocalDecision();
            }
            else RequireAuthority();
        }

        /// <summary>
        /// Checks the captured entry frame, its live physical-owner ancestry and exact registration after source work.
        /// </summary>
        private void RequireRetainedFlow()
        {
            _files.RequirePendingSnapshotLeaseFlow(_lease, _owner, _registration);
            if (!_files.IsBorrowedValidationReadFlowCurrent(_lease, _entryAdmission))
                throw new InvalidOperationException("Validation read left its exact retained registration.");
            _cleanupWindow?.RequireCurrent();
        }

        /// <summary>
        /// Produces a detached result so consumers cannot mutate the retained source observation.
        /// </summary>
        /// <param name="snapshot">
        /// Private snapshot DATA, or <see langword="null"/> for absence.
        /// </param>
        /// <returns>
        /// A fresh byte buffer and the observed timestamp, or <see langword="null"/> for absence.
        /// </returns>
        private static CanonicalFileReadSnapshot? Copy(CanonicalFileReadSnapshot? snapshot) =>
            snapshot == null ? null : new((byte[])snapshot.Content.Clone(), snapshot.LastWriteTimeUtc);

        /// <summary>
        /// Reads or reuses one exact synchronous identity while retaining fresh admission checks.
        /// </summary>
        /// <param name="path">
        /// Raw ordinal relative spelling, never normalized for identity.
        /// </param>
        /// <param name="family">
        /// Original synchronous reader family.
        /// </param>
        /// <param name="declaredLease">
        /// Exact retained lease, or <see langword="null"/> for a lease-free caller in its exact flow.
        /// </param>
        /// <returns>
        /// Detached content/timestamp or observed absence; errors invalidate the phase and propagate.
        /// </returns>
        internal CanonicalFileReadSnapshot? Read(string path, ValidationSnapshotFamily family, CanonicalWriteLease? declaredLease)
        {
            Enter();
            try
            {
                using var request = _files.MeasureOperation("ValidationReadSet.RequestSync");
                RequireDeclaredLease(declaredLease); RequireReadAuthority();
                var key = (path, family, declaredLease != null);
                CanonicalFileReadSnapshot? snapshot;
                if (_cleanupWindow != null)
                    snapshot = ReadCleanupData(key, declaredLease);
                else if (!_snapshots.TryGetValue(key, out snapshot))
                {
                    using var source = _files.MeasureOperation("ValidationReadSet.SourceSync");
                    snapshot = _files.ReadValidationSource(path, family, declaredLease);
                    _snapshots.Add(key, snapshot); _order.Add(key);
                }
                else { using var reuse = _files.MeasureOperation("ValidationReadSet.ReuseSync"); }
                RequireRetainedFlow();
                lock (_state) { RequireHealthy(); return Copy(snapshot); }
            }
            catch { Fail(); throw; }
            finally { Leave(); }
        }

        /// <summary>
        /// Reads or reuses one exact asynchronous identity, preserving cancellation and fresh admission.
        /// </summary>
        /// <param name="path">
        /// Raw ordinal relative spelling.
        /// </param>
        /// <param name="family">
        /// Original asynchronous reader family.
        /// </param>
        /// <param name="declaredLease">
        /// Exact retained lease, or <see langword="null"/> for the exact lease-free execution flow.
        /// </param>
        /// <param name="token">
        /// Cancellation checked before both fresh and reused results.
        /// </param>
        /// <returns>
        /// Detached content/timestamp or absence; cancellation and original errors propagate and invalidate reuse.
        /// </returns>
        internal async Task<CanonicalFileReadSnapshot?> ReadAsync(string path, ValidationSnapshotFamily family,
            CanonicalWriteLease? declaredLease, CancellationToken token)
        {
            Enter();
            try
            {
                using var request = _files.MeasureOperation("ValidationReadSet.RequestAsync");
                token.ThrowIfCancellationRequested(); RequireDeclaredLease(declaredLease); RequireReadAuthority();
                var key = (path, family, declaredLease != null);
                CanonicalFileReadSnapshot? snapshot;
                if (_cleanupWindow != null)
                    snapshot = await ReadCleanupDataAsync(key, declaredLease, token);
                else if (!_snapshots.TryGetValue(key, out snapshot))
                {
                    using var source = _files.MeasureOperation("ValidationReadSet.SourceAsync");
                    snapshot = await _files.ReadValidationSourceAsync(path, family, declaredLease, token);
                    _snapshots.Add(key, snapshot); _order.Add(key);
                }
                else { using var reuse = _files.MeasureOperation("ValidationReadSet.ReuseAsync"); }
                token.ThrowIfCancellationRequested(); RequireRetainedFlow();
                lock (_state) { RequireHealthy(); return Copy(snapshot); }
            }
            catch { Fail(); throw; }
            finally { Leave(); }
        }

        /// <summary>
        /// Rejects a foreign explicit lease instead of treating it as an equivalent read identity.
        /// </summary>
        /// <param name="declaredLease">
        /// Lease supplied by the original reader, or <see langword="null"/> for lease-free semantics.
        /// </param>
        private void RequireDeclaredLease(CanonicalWriteLease? declaredLease)
        {
            if (declaredLease != null && !ReferenceEquals(declaredLease, _lease))
                throw new InvalidOperationException("Validation read supplied a foreign lease.");
        }

        /// <summary>
        /// Disables reuse and freshly proves every touched source and exact generation before allowing success.
        /// </summary>
        /// <returns>
        /// Completion only when all original bytes, absence, timestamps and authority still match.
        /// </returns>
        internal async Task CompleteAsync()
        {
            Enter();
            try
            {
                using var timing = _files.MeasureOperation("ValidationReadSet.ExitProof");
                lock (_state) _completing = true;
                RequireAuthority();
                foreach (var key in _order)
                {
                    using var source = _files.MeasureOperation("ValidationReadSet.ExitSource");
                    var declared = key.Explicit ? _lease : null;
                    var fresh = key.Family is ValidationSnapshotFamily.OrdinaryAsync or ValidationSnapshotFamily.ProtectedAsync
                        ? await _files.ReadValidationSourceAsync(key.Path, key.Family, declared, CancellationToken.None)
                        : _files.ReadValidationSource(key.Path, key.Family, declared);
                    var original = RetainedSnapshot(key);
                    if ((fresh == null) != (original == null) || fresh != null &&
                        (fresh.LastWriteTimeUtc != original!.LastWriteTimeUtc || !fresh.Content.AsSpan().SequenceEqual(original.Content)))
                        throw new InvalidDataException("Reserved validation source changed: " + key.Path);
                }
                foreach (var key in _presenceOrder)
                {
                    using var source = _files.MeasureOperation("ValidationReadSet.ExitPresence");
                    if (ProbePresence(key.Path, key.Explicit) != _presence[key])
                        throw new InvalidDataException("Reserved validation presence changed: " + key.Path);
                }
                var generation = _files.ReadLocalGenerationSnapshot(_lease);
                if (generation.Binding != _generation!.Binding || generation.Bytes == null ||
                    !generation.Bytes.AsSpan().SequenceEqual(_generation.Bytes))
                    throw new InvalidDataException("Reserved validation generation bytes changed.");
                RequireAuthority();
                lock (_state) RequireHealthy();
            }
            catch { Fail(); throw; }
            finally { Leave(); }
        }

        /// <summary>
        /// Retires routing and source buffers and releases only this phase's borrowed reservation.
        /// </summary>
        /// <returns>
        /// Completion after any admitted operation settles and the reservation releases; the caller-owned lease remains active.
        /// </returns>
        public ValueTask DisposeAsync()
        {
            if (ReferenceEquals(_files._validationReadSet.Value, this))
            {
                _files._validationReadSet.Value = null;
                ValidationDiagnosticScopes.Value--;
            }
            lock (_state)
            {
                if (_closeTask != null) return new ValueTask(_closeTask);
                _closed = true;
                Task drain = Task.CompletedTask;
                if (_busy)
                {
                    _failed = true;
                    _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    drain = _drained.Task;
                }
                _closeTask = CloseAfterReadAsync(drain);
                return new ValueTask(_closeTask);
            }
        }

        /// <summary>
        /// Waits for the admitted reader before clearing its private buffers and releasing its borrowed reservation.
        /// </summary>
        /// <param name="drain">
        /// Completion of the already admitted operation, or a completed task when no operation is active.
        /// </param>
        /// <returns>
        /// Completion after release, including propagation of any actual reservation-close failure.
        /// </returns>
        private async Task CloseAfterReadAsync(Task drain)
        {
            await drain;
            lock (_state)
            {
                _snapshots.Clear(); _order.Clear(); _presence.Clear(); _presenceOrder.Clear(); _generation = null;
                _cleanupData?.Dispose(); _cleanupKeys.Clear();
            }
            await _reservation.DisposeAsync();
        }
    }
}
