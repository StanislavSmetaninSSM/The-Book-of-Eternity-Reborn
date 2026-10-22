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
        if (ValidationReuseSuppression.Value != 0) return null;
        if (_sourceDataOperation.Value is { } current)
        {
            if (current.IsSuspended) return null;
            current.RequireOpen();
            return null;
        }
        var operation = new SourceDataOperation(this);
        _sourceDataOperation.Value = operation;
        ValidationDiagnosticScopes.Value++;
        return operation;
    }

    /// <summary>
    /// Freshly verifies provisional DATA at a selected original effect boundary.
    /// </summary>
    /// <param name="lease">
    /// Original caller-owned lease under which the effect will be admitted.
    /// </param>
    /// <returns>
    /// Completion after verification, or immediate completion outside a source operation.
    /// </returns>
    internal Task VerifySourceDataAsync(CanonicalWriteLease lease) =>
        ValidationReuseSuppression.Value == 0 && _sourceDataOperation.Value is { } operation
            ? operation.VerifyAsync(lease) : Task.CompletedTask;

    /// <summary>
    /// Consumes the verified source transition before the selected synchronous runtime effect.
    /// </summary>
    /// <param name="lease">
    /// Exact original physical lease retained throughout the synchronous effect.
    /// </param>
    /// <returns>
    /// Original mutation token that must remain live until the effect has finished.
    /// </returns>
    internal IDisposable BeginVerifiedSourceEffect(CanonicalWriteLease lease) => BeginValidationMutation(lease);

    /// <summary>
    /// Owns the existing general service storage for one selected source DATA operation.
    /// </summary>
    internal sealed class SourceDataOperation : IDisposable, IOperationDependency
    {
        private readonly FileSystemManager _files;
        private readonly ValidationOperation _operation = new DefaultOperationValidationService().BeginOperation();
        private static readonly OperationComputation<CanonicalFileReadSnapshot?> Snapshot = new(
            "Operation.FileSnapshot", 1, value => value == null ? null : new((byte[])value.Content.Clone(), value.LastWriteTimeUtc));
        private readonly object _gate = new();
        private readonly Dictionary<(string Path, ValidationSnapshotFamily Family, bool Explicit), SourceKey> _keys = new();
        private readonly List<SourceKey> _touched = new();
        private CanonicalWriteLease? _lease;
        private LocalSessionGenerationSnapshot? _generation;
        private object _segment = new();
        private object? _rootVersion;
        private object? _verifiedVersion;
        private CanonicalWriteLease? _verifiedLease;
        private MainAdmission? _verifiedEntry;
        private AmbientCanonicalLeaseRegistration? _verifiedRegistration;
        private CanonicalWriteLease? _generationLease;
        private bool _busy;
        private volatile bool _failed;
        private volatile bool _completed;
        private volatile int _suspended;
        private volatile bool _closed;

        /// <summary>
        /// Identifies source DATA without retaining a second copy of its payload.
        /// </summary>
        /// <param name="Path">
        /// Raw ordinal path spelling supplied to the original reader.
        /// </param>
        /// <param name="Family">
        /// Original reader contract whose source observations remain independent.
        /// </param>
        /// <param name="Explicit">
        /// Whether the original reader declared its lease explicitly.
        /// </param>
        private sealed record SourceKey(string Path, ValidationSnapshotFamily Family, bool Explicit);

        /// <summary>
        /// Creates an independent operation through the existing service backend.
        /// </summary>
        /// <param name="files">
        /// Exact manager registering this owner in its calling flow.
        /// </param>
        internal SourceDataOperation(FileSystemManager files) => _files = files;

        /// <summary>
        /// Gets whether an independently authorized fresh protocol currently bypasses provisional DATA.
        /// </summary>
        internal bool IsSuspended
        {
            get { ObjectDisposedException.ThrowIf(_closed, this); return _suspended != 0; }
        }

        /// <summary>
        /// Gets or sets an optional fixture barrier before the original asynchronous source computation.
        /// A null value leaves source work unchanged; the barrier neither supplies DATA nor grants authority.
        /// </summary>
        internal Task? SourceReadBarrierForTests { get; set; }

        /// <summary>
        /// Refuses use after the owning operation has closed.
        /// </summary>
        internal void RequireOpen()
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            if (_failed || _completed) throw new InvalidOperationException("The source operation is no longer available.");
        }

        /// <summary>
        /// Observes the actual lease without admitting or retaining any new authority.
        /// </summary>
        /// <param name="lease">
        /// Lease supplied by original acquisition or an explicitly admitted reader.
        /// </param>
        internal void Observe(CanonicalWriteLease lease)
        {
            lock (_gate)
            {
                if (_closed || _failed || _completed || _suspended != 0 || _busy) return;
                if (_lease == null || !_lease.IsActive)
                {
                    _lease = lease;
                    ClearReceipt();
                }
            }
        }

        /// <summary>
        /// Captures only a live original ordinary lease's in-memory root version.
        /// </summary>
        /// <param name="version">
        /// Exact root version, or no version for unsupported original access.
        /// </param>
        /// <returns>
        /// Whether the current registration and admission can serve operation DATA.
        /// </returns>
        private bool TryRootVersion(out object? version)
        {
            version = null;
            if (_lease is not { } lease || MainAdmissions.Value is not { } entry ||
                !_files.IsBorrowedValidationReadFlowCurrent(lease, entry)) return false;
            var root = _files.CanonicalRootAuthorityIdentity;
            lock (root.WorkerContextGate)
                return root.WorkerContext == null && lease.MainAdmission != null &&
                    lease.MainAdmission.TryCaptureSourceDataVersion(lease, out version);
        }

        /// <summary>
        /// Supplies the existing service with this segment's identity after pure live checks.
        /// </summary>
        /// <param name="version">
        /// Segment identity when the exact root remains unchanged, otherwise no reusable version.
        /// </param>
        /// <returns>
        /// Whether a DATA result can be retained or reused in this operation.
        /// </returns>
        public bool TryCaptureVersion(out object? version)
        {
            version = null;
            if (_closed || _failed || _suspended != 0 || !TryRootVersion(out var root) ||
                !ReferenceEquals(root, _rootVersion)) return false;
            version = _segment;
            return true;
        }

        /// <summary>
        /// Enters one serial functional read, preserving unsupported fresh reader routes.
        /// </summary>
        /// <param name="declared">
        /// Original explicitly supplied lease, or no declared lease for the original implicit reader.
        /// </param>
        /// <returns>
        /// Whether the original source can participate in DATA reuse.
        /// </returns>
        private bool EnterRead(CanonicalWriteLease? declared)
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_closed, this);
                if (_suspended != 0) return false;
                RequireOpen();
                if (_busy) { _failed = true; throw new InvalidOperationException("Overlapping source operation."); }
                ClearReceipt();
                if (declared != null)
                {
                    if (_lease != null && _lease.IsActive && !ReferenceEquals(_lease, declared))
                        throw new InvalidOperationException("Source operation supplied a foreign lease.");
                    _lease = declared;
                }
                _busy = true;
            }
            try
            {
                object? root = null;
                var eligible = _files.CanReuseSourceData && TryRootVersion(out root);
                RequireOpen();
                if (!eligible)
                {
                    lock (_gate) _busy = false;
                    return false;
                }
                if (_rootVersion != null && !ReferenceEquals(_rootVersion, root))
                {
                    _failed = true;
                    throw new InvalidOperationException("Canonical data changed before the source operation settled.");
                }
                _rootVersion = root;
                return true;
            }
            catch
            {
                _failed = true;
                lock (_gate) _busy = false;
                throw;
            }
        }

        /// <summary>
        /// Admits each new physical adapter freshly before selecting provisional DATA.
        /// </summary>
        private void BindGeneration()
        {
            if (ReferenceEquals(_generationLease, _lease)) return;
            var generation = _files.ReadLocalGenerationSnapshot(_lease!);
            if (generation.Bytes == null || _generation != null &&
                (generation.Binding != _generation.Binding || _generation.Bytes == null ||
                    !generation.Bytes.AsSpan().SequenceEqual(_generation.Bytes)))
                throw new InvalidDataException("Source operation generation changed between admitted leases.");
            _generation ??= generation;
            _generationLease = _lease;
        }

        /// <summary>
        /// Retires the one-use verification receipt without granting approval to another adapter.
        /// </summary>
        private void ClearReceipt()
        {
            _verifiedVersion = null;
            _verifiedLease = null;
            _verifiedEntry = null;
            _verifiedRegistration = null;
        }

        /// <summary>
        /// Creates a stable operation-local key and records its first touch without duplicating DATA.
        /// </summary>
        /// <param name="path">
        /// Raw ordinal spelling passed unchanged to the original reader.
        /// </param>
        /// <param name="family">
        /// Original synchronous or asynchronous reader contract.
        /// </param>
        /// <param name="declared">
        /// Original declared lease, or no declaration for implicit reads.
        /// </param>
        /// <returns>
        /// The stable identity used by the existing general cache service.
        /// </returns>
        private SourceKey Key(string path, ValidationSnapshotFamily family, CanonicalWriteLease? declared)
        {
            var identity = (path, family, declared != null);
            if (!_keys.TryGetValue(identity, out var key))
            {
                key = new(path, family, declared != null);
                _keys.Add(identity, key);
                _touched.Add(key);
            }
            return key;
        }

        /// <summary>
        /// Reuses synchronous source DATA without blocking on unfinished asynchronous work.
        /// </summary>
        /// <param name="path">
        /// Original raw source path.
        /// </param>
        /// <param name="family">
        /// Original synchronous reader family.
        /// </param>
        /// <param name="declared">
        /// Original declared lease or no declaration.
        /// </param>
        /// <returns>
        /// Detached source bytes and timestamp, or the original observed absence.
        /// </returns>
        internal CanonicalFileReadSnapshot? Read(string path, ValidationSnapshotFamily family, CanonicalWriteLease? declared)
        {
            if (!EnterRead(declared)) return _files.ReadValidationSource(path, family, declared);
            try
            {
                using var request = _files.MeasureOperation("OperationFileData.RequestSync");
                BindGeneration();
                var computed = false;
                var task = _operation.GetOrComputeAsync(Snapshot, Key(path, family, declared), [this], _ =>
                {
                    computed = true;
                    using var source = _files.MeasureOperation("OperationFileData.SourceSync");
                    return Task.FromResult(_files.ReadValidationSource(path, family, declared));
                });
                if (!task.IsCompleted) throw new InvalidOperationException("A synchronous source computation did not settle synchronously.");
                var value = task.GetAwaiter().GetResult();
                if (!computed) { using var reuse = _files.MeasureOperation("OperationFileData.ReuseSync"); }
                return value;
            }
            catch { _failed = true; throw; }
            finally { lock (_gate) _busy = false; }
        }

        /// <summary>
        /// Reuses asynchronous source DATA through the existing general service.
        /// </summary>
        /// <param name="path">
        /// Original raw source path.
        /// </param>
        /// <param name="family">
        /// Original asynchronous reader family.
        /// </param>
        /// <param name="declared">
        /// Original declared lease or no declaration.
        /// </param>
        /// <param name="token">
        /// Cancellation observed before both fresh reads and cache hits.
        /// </param>
        /// <returns>
        /// Detached source bytes and timestamp, or observed absence.
        /// </returns>
        internal async Task<CanonicalFileReadSnapshot?> ReadAsync(string path, ValidationSnapshotFamily family,
            CanonicalWriteLease? declared, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!EnterRead(declared)) return await _files.ReadValidationSourceAsync(path, family, declared, token);
            try
            {
                using var request = _files.MeasureOperation("OperationFileData.RequestAsync");
                BindGeneration();
                var computed = false;
                var value = await _operation.GetOrComputeAsync(Snapshot, Key(path, family, declared), [this], async ct =>
                {
                    computed = true;
                    using var source = _files.MeasureOperation("OperationFileData.SourceAsync");
                    if (SourceReadBarrierForTests is { } barrier) await barrier.WaitAsync(ct);
                    return await _files.ReadValidationSourceAsync(path, family, declared, ct);
                }, token);
                if (!computed) { using var reuse = _files.MeasureOperation("OperationFileData.ReuseAsync"); }
                return value;
            }
            catch { _failed = true; throw; }
            finally { lock (_gate) _busy = false; }
        }

        /// <summary>
        /// Verifies pending source dependencies before a caller consumes them under its original lease.
        /// </summary>
        /// <param name="lease">
        /// Active exact current lease; a foreign or retired lease retains its original refusal.
        /// </param>
        /// <returns>
        /// Completion after the source fence succeeds.
        /// </returns>
        internal async Task VerifyAsync(CanonicalWriteLease lease)
        {
            bool independent;
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_closed, this);
                independent = _suspended != 0;
                if (!independent)
                {
                    RequireOpen();
                    if (_busy) throw new InvalidOperationException("A source fence requires serial original access.");
                    if (_lease != null && !ReferenceEquals(lease, _lease)) throw new InvalidOperationException("Source fence supplied a foreign lease.");
                    _busy = true;
                }
            }
            if (independent)
            {
                // The fresh protocol owns its original admission and checks; this issues no receipt.
                _files.EnsureCanonicalWriteLeaseActive(lease);
                return;
            }
            try
            {
                using var fence = _files.MeasureOperation("OperationFileData.FreshFence");
                _files.EnsureCanonicalWriteLeaseActive(lease);
                if (_touched.Count == 0) return;
                if (!TryRootVersion(out var root) || !ReferenceEquals(root, _rootVersion))
                    throw new InvalidOperationException("Source fence lost its original version.");
                foreach (var key in _touched)
                {
                    // A missing expected cache entry is a refusal, never a newly observed baseline.
                    var expected = await _operation.GetOrComputeAsync(Snapshot, key, [this], _ =>
                        throw new InvalidOperationException("Expected source DATA was retired before its fence."));
                    using var source = _files.MeasureOperation("OperationFileData.FreshSource");
                    var declared = key.Explicit ? lease : null;
                    var fresh = key.Family is ValidationSnapshotFamily.OrdinaryAsync or ValidationSnapshotFamily.ProtectedAsync
                        ? await _files.ReadValidationSourceAsync(key.Path, key.Family, declared, CancellationToken.None)
                        : _files.ReadValidationSource(key.Path, key.Family, declared);
                    if ((fresh == null) != (expected == null) || fresh != null &&
                        (fresh.LastWriteTimeUtc != expected!.LastWriteTimeUtc || !fresh.Content.AsSpan().SequenceEqual(expected.Content)))
                        throw new InvalidDataException("Operation source changed: " + key.Path);
                }
                var generation = _files.ReadLocalGenerationSnapshot(lease);
                if (generation.Binding != _generation!.Binding || generation.Bytes == null || _generation.Bytes == null ||
                    !generation.Bytes.AsSpan().SequenceEqual(_generation.Bytes))
                    throw new InvalidDataException("Operation generation bytes changed.");
                _files.EnsureCanonicalWriteLeaseActive(lease);
                if (!TryRootVersion(out root) || !ReferenceEquals(root, _rootVersion))
                    throw new InvalidOperationException("Source fence version changed during verification.");
                _verifiedVersion = root;
                _verifiedLease = lease;
                _verifiedEntry = MainAdmissions.Value;
                _verifiedRegistration = lease.AmbientRegistration;
            }
            catch { _failed = true; throw; }
            finally { lock (_gate) _busy = false; }
        }

        /// <summary>
        /// Completes the owning operation's required successful-return source fence.
        /// </summary>
        /// <returns>
        /// Completion after all retained source dependencies have been verified.
        /// </returns>
        internal async Task CompleteAsync()
        {
            RequireOpen();
            if (_touched.Count != 0) await VerifyAsync(_lease ?? throw new InvalidOperationException("Source fence lost its lease."));
            _completed = true;
        }

        /// <summary>
        /// Registers an original mutation atomically against a one-use verified root version.
        /// </summary>
        /// <param name="admission">
        /// Exact admission still responsible for the original writer.
        /// </param>
        /// <returns>
        /// Original mutation reservation after provisional DATA has been retired.
        /// </returns>
        internal MainMutationReservation BeginMutation(MainAdmission admission)
        {
            object? expected;
            MainAdmission? entry;
            bool independent;
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_closed, this);
                if (_busy) throw new InvalidOperationException("Mutation overlaps source work.");
                independent = _suspended != 0;
                expected = null;
                entry = null;
                if (!independent)
                {
                    RequireOpen();
                    expected = _touched.Count == 0 ? null : _verifiedVersion;
                    entry = expected == null ? null : _verifiedEntry;
                    if (_touched.Count != 0 && (expected == null || !ReferenceEquals(_verifiedLease, _lease) ||
                        !ReferenceEquals(entry, MainAdmissions.Value) ||
                        !ReferenceEquals(_verifiedRegistration, _lease?.AmbientRegistration)))
                        throw new InvalidOperationException("Mutation requires a freshly verified source operation.");
                    _busy = true;
                }
            }
            if (independent) return admission.BeginMutation();
            try
            {
                if (expected != null && (!TryRootVersion(out var root) || !ReferenceEquals(root, expected)))
                    throw new InvalidOperationException("The verified source root changed before mutation admission.");
                var token = admission.BeginMutation(expected, entry);
                try { RequireOpen(); Retire(); return token; }
                catch { token.Dispose(); throw; }
            }
            finally { lock (_gate) _busy = false; }
        }

        /// <summary>
        /// Clears provisional DATA before an independently authorized fresh protocol runs.
        /// </summary>
        /// <returns>
        /// A serial restoring scope that bypasses DATA for the complete fresh protocol.
        /// </returns>
        internal IDisposable Suspend()
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_closed, this);
                if (_busy) throw new InvalidOperationException("Fresh protocol overlaps source work.");
                Retire();
                _suspended++;
            }
            return new SourceFreshScope(this);
        }

        /// <summary>
        /// Verifies provisional dependencies before their physical lease closes.
        /// </summary>
        /// <param name="lease">
        /// Original physical lease about to release its resources.
        /// </param>
        /// <returns>
        /// Completion after pending DATA has settled, without owning lease disposal.
        /// </returns>
        internal async Task BeforeLeaseCloseAsync(CanonicalWriteLease lease)
        {
            if (_closed || !ReferenceEquals(_lease, lease)) return;
            try
            {
                if (!_failed && !_completed && _suspended == 0 && _touched.Count != 0) await VerifyAsync(lease);
            }
            finally
            {
                // A new lease must establish its own fresh adapter. No admission survives this close.
                _lease = null;
                ClearReceipt();
            }
        }

        /// <summary>
        /// Invalidates functional reuse after original source or authority failure.
        /// </summary>
        internal void Fail() => _failed = true;

        /// <summary>
        /// Removes payloads from the existing cache and rotates operation-local identities.
        /// </summary>
        private void Retire()
        {
            using var fresh = _operation.ForceFresh();
            _keys.Clear();
            _touched.Clear();
            _generation = null;
            _generationLease = null;
            _rootVersion = null;
            ClearReceipt();
            _segment = new();
        }

        /// <summary>
        /// Restores source routing after an independently authorized fresh protocol settles.
        /// </summary>
        /// <param name="owner">
        /// Exact operation whose nested suspension is being closed.
        /// </param>
        private sealed class SourceFreshScope(SourceDataOperation owner) : IDisposable
        {
            private bool _closed;

            /// <summary>
            /// Releases this nested bypass once without restoring retired DATA.
            /// </summary>
            public void Dispose()
            {
                if (_closed) return;
                _closed = true;
                lock (owner._gate) owner._suspended--;
            }
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
            try
            {
                _operation.Dispose(); _keys.Clear(); _touched.Clear(); _lease = null; _generation = null;
                _generationLease = null; SourceReadBarrierForTests = null; ClearReceipt();
            }
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
