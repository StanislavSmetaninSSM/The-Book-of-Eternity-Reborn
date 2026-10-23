using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    private readonly AsyncLocal<SourceDataOperation?> _sourceDataOperation = new();

    /// <summary>
    /// Selects genuine provisional DATA before filesystem admission only for a known declared reader policy.
    /// </summary>
    /// <param name="lease">
    /// Exact current adapter whose first source read already passed full admission.
    /// </param>
    /// <param name="path">
    /// Exact raw ordinal path previously dispatched by the normal declared reader.
    /// </param>
    /// <param name="asynchronous">
    /// Whether the normal reader uses its asynchronous family.
    /// </param>
    /// <param name="snapshot">
    /// Detached provisional DATA on a hit, including null for recorded absence; otherwise null.
    /// </param>
    /// <returns>
    /// True only for a present eligible service entry; false preserves the original full reader route.
    /// </returns>
    private bool TryReadDeclaredSourceData(CanonicalWriteLease lease, string path, bool asynchronous,
        out CanonicalFileReadSnapshot? snapshot)
    {
        snapshot = null;
        return _validationReadSet.Value == null && ValidationReuseSuppression.Value == 0 &&
            _sourceDataOperation.Value is { } operation && operation.TryReadDeclaredSnapshot(lease, path, asynchronous, out snapshot);
    }

    /// <summary>
    /// Records the actual successful normal reader dispatch without retaining DATA or namespace facts twice.
    /// </summary>
    /// <param name="lease">
    /// Original fully admitted physical adapter.
    /// </param>
    /// <param name="path">
    /// Original raw ordinal path spelling.
    /// </param>
    /// <param name="asynchronous">
    /// Whether the normal reader used its asynchronous family.
    /// </param>
    /// <param name="ordinary">
    /// Actual original dispatch decision, made by the unchanged filesystem route.
    /// </param>
    private void RememberDeclaredSourceRoute(CanonicalWriteLease lease, string path, bool asynchronous, bool ordinary)
    {
        if (_validationReadSet.Value != null || ValidationReuseSuppression.Value != 0) return;
        var family = asynchronous
            ? ordinary ? ValidationSnapshotFamily.OrdinaryAsync : ValidationSnapshotFamily.ProtectedAsync
            : ordinary ? ValidationSnapshotFamily.OrdinarySync : ValidationSnapshotFamily.ProtectedSync;
        _sourceDataOperation.Value?.RegisterDeclaredRoute(lease, path, family);
    }

    /// <summary>
    /// Freshly closes issued fast DATA before an arbitrary callback can consume it outside the owning protocol.
    /// </summary>
    /// <returns>
    /// Completion after the original full authority and source fence, or immediately when no fast DATA is pending.
    /// </returns>
    private Task VerifyPendingDeclaredSourceDataAsync() =>
        ValidationReuseSuppression.Value == 0 && _sourceDataOperation.Value is { } operation
            ? operation.VerifyPendingDeclaredDataAsync() : Task.CompletedTask;

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
        private readonly Dictionary<(string Path, bool Async), SourceKey> _declaredRoutes = new();
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
        private CanonicalWriteLease? _closingLease;
        private bool _busy;
        private bool _pendingDeclaredData;
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
        /// Gets or sets an optional fixture barrier between fresh generation observation and local binding.
        /// A null value leaves binding unchanged; this barrier supplies no data or authority.
        /// </summary>
        internal Task? GenerationReadBarrierForTests { get; set; }

        /// <summary>
        /// Gets whether active work or local bookkeeping still retains source dependencies.
        /// This diagnostic predicate grants no authority and never inspects file contents.
        /// </summary>
        internal bool HasRetainedSourceReferences
        {
            get
            {
                lock (_gate)
                    return _busy || _suspended != 0 || _keys.Count != 0 || _declaredRoutes.Count != 0 || _pendingDeclaredData || _touched.Count != 0 || _lease != null || _generation != null ||
                        _generationLease != null || _closingLease != null || SourceReadBarrierForTests != null || GenerationReadBarrierForTests != null ||
                        _verifiedLease != null || _verifiedEntry != null || _verifiedRegistration != null;
            }
        }

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
                if (_closed || _failed || _completed || _suspended != 0 || _busy || _closingLease != null) return;
                if (_lease == null || !_lease.IsActive)
                {
                    if (!lease.TryAttachSourceOwner(this)) return;
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
                if (_closingLease != null) throw new InvalidOperationException("The source adapter is closing.");
                if (_busy) { _failed = true; throw new InvalidOperationException("Overlapping source operation."); }
                ClearReceipt();
                if (declared != null)
                {
                    if (_lease != null && _lease.IsActive && !ReferenceEquals(_lease, declared))
                        throw new InvalidOperationException("Source operation supplied a foreign lease.");
                    _lease = declared;
                }
                if (_lease != null && !_lease.TryAttachSourceOwner(this))
                    throw new InvalidOperationException("The source lease is closing or belongs to another DATA owner.");
                _busy = true;
            }
            try
            {
                object? root = null;
                var eligible = _files.CanReuseSourceData && TryRootVersion(out root);
                RequireOpen();
                if (!eligible)
                {
                    LeaveWork();
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
                LeaveWork();
                throw;
            }
        }

        /// <summary>
        /// Admits each new physical adapter freshly before selecting provisional DATA.
        /// </summary>
        private void BindGeneration()
        {
            if (ReferenceEquals(_generationLease, _lease)) return;
            var generation = ReadGenerationForBinding();
            _generation ??= generation;
            _generationLease = _lease;
        }

        /// <summary>
        /// Performs the same fresh adapter binding with an optional deterministic fixture barrier.
        /// </summary>
        /// <param name="token">
        /// Original read cancellation, including cancellation while the fixture barrier is pending.
        /// </param>
        /// <returns>
        /// Completion after the fresh generation observation is bound to its current adapter.
        /// </returns>
        private async Task BindGenerationAsync(CancellationToken token)
        {
            if (ReferenceEquals(_generationLease, _lease)) return;
            var generation = ReadGenerationForBinding();
            if (GenerationReadBarrierForTests is { } barrier) await barrier.WaitAsync(token);
            _generation ??= generation;
            _generationLease = _lease;
        }

        /// <summary>
        /// Reads uncached generation bytes and compares any prior provisional observation.
        /// </summary>
        /// <returns>
        /// Exact current generation bytes after the original physical admission checks.
        /// </returns>
        private LocalSessionGenerationSnapshot ReadGenerationForBinding()
        {
            var generation = _files.ReadLocalGenerationSnapshot(_lease!);
            if (generation.Bytes == null || _generation != null &&
                (generation.Binding != _generation.Binding || _generation.Bytes == null ||
                    !generation.Bytes.AsSpan().SequenceEqual(_generation.Bytes)))
                throw new InvalidDataException("Source operation generation changed between admitted leases.");
            return generation;
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
        /// Registers only an already stored explicit source identity from the successful original reader route.
        /// </summary>
        /// <param name="lease">
        /// Exact original adapter whose fresh generation has already been bound.
        /// </param>
        /// <param name="path">
        /// Original raw ordinal spelling, without normalization or alias inference.
        /// </param>
        /// <param name="family">
        /// Actual original synchronous or asynchronous, ordinary or protected reader family.
        /// </param>
        internal void RegisterDeclaredRoute(CanonicalWriteLease lease, string path, ValidationSnapshotFamily family)
        {
            object segment;
            lock (_gate)
            {
                if (_closed || _failed || _completed || _suspended != 0 || _busy || _closingLease != null) return;
                segment = _segment;
            }
            // Never hold the source monitor while capturing external root/admission state.
            if (!TryRootVersion(out var root)) return;
            lock (_gate)
            {
                if (_closed || _failed || _completed || _suspended != 0 || _busy || _closingLease != null ||
                    !ReferenceEquals(_generationLease, lease) || !ReferenceEquals(_lease, lease) ||
                    !ReferenceEquals(segment, _segment) || !ReferenceEquals(root, _rootVersion)) return;
                if (_keys.TryGetValue((path, family, true), out var key))
                    _declaredRoutes[(path, family is ValidationSnapshotFamily.OrdinaryAsync or ValidationSnapshotFamily.ProtectedAsync)] = key;
            }
        }

        /// <summary>
        /// Issues detached provisional DATA without source IO or filesystem authority classification.
        /// </summary>
        /// <param name="lease">
        /// Exact live current physical adapter; a first or replacement adapter retains full admission.
        /// </param>
        /// <param name="path">
        /// Exact raw ordinal normal-reader spelling.
        /// </param>
        /// <param name="asynchronous">
        /// Whether to select the previously recorded asynchronous reader policy.
        /// </param>
        /// <param name="snapshot">
        /// Detached stored DATA, including null absence, when the result is a hit; otherwise null.
        /// </param>
        /// <returns>
        /// True for an actual eligible service hit; false never computes, probes or grants permission.
        /// </returns>
        internal bool TryReadDeclaredSnapshot(CanonicalWriteLease lease, string path, bool asynchronous,
            out CanonicalFileReadSnapshot? snapshot)
        {
            snapshot = null;
            SourceKey key;
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_closed, this);
                if (_suspended != 0) return false;
                RequireOpen();
                if (!ReferenceEquals(_generationLease, lease) || !_declaredRoutes.TryGetValue((path, asynchronous), out key!)) return false;
            }
            if (!EnterRead(lease)) return false;
            try
            {
                OperationLookup<CanonicalFileReadSnapshot?> result;
                using (_files.MeasureOperation("LeaseDataHit.Request"))
                {
                    var lookup = _operation.TryGetAsync(Snapshot, key, [this]);
                    if (!lookup.IsCompleted) throw new InvalidOperationException("A cache-only source lookup did not settle synchronously.");
                    result = lookup.GetAwaiter().GetResult();
                    RequireOpen();
                    if (result.Found)
                    {
                        _pendingDeclaredData = true;
                        using var reuse = _files.MeasureOperation("LeaseDataHit.Reuse");
                    }
                }
                // Every passive timing callback has settled before final live admission.
                RequireOpen();
                if (!result.Found) return false;
                if (!TryRootVersion(out var root) || !ReferenceEquals(root, _rootVersion))
                    throw new InvalidOperationException("The cached DATA owner lost its original live root.");
                snapshot = result.Value;
                return true;
            }
            catch { _failed = true; throw; }
            finally { LeaveWork(); }
        }

        /// <summary>
        /// Awaits the original full source fence before fast provisional results reach an arbitrary callback.
        /// </summary>
        /// <returns>
        /// Completion after verification, or immediately during an independently fresh protocol or without pending DATA.
        /// </returns>
        internal Task VerifyPendingDeclaredDataAsync()
        {
            CanonicalWriteLease lease;
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_closed, this);
                if (_suspended != 0 || !_pendingDeclaredData) return Task.CompletedTask;
                RequireOpen();
                lease = _lease ?? throw new InvalidOperationException("Pending DATA lost its original adapter.");
            }
            return VerifyAsync(lease);
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
                RequireOpen();
                return value;
            }
            catch { _failed = true; throw; }
            finally { LeaveWork(); }
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
                await BindGenerationAsync(token);
                var computed = false;
                var value = await _operation.GetOrComputeAsync(Snapshot, Key(path, family, declared), [this], async ct =>
                {
                    computed = true;
                    using var source = _files.MeasureOperation("OperationFileData.SourceAsync");
                    if (SourceReadBarrierForTests is { } barrier) await barrier.WaitAsync(ct);
                    return await _files.ReadValidationSourceAsync(path, family, declared, ct);
                }, token);
                if (!computed) { using var reuse = _files.MeasureOperation("OperationFileData.ReuseAsync"); }
                RequireOpen();
                return value;
            }
            catch { _failed = true; throw; }
            finally { LeaveWork(); }
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
        internal Task VerifyAsync(CanonicalWriteLease lease) => VerifyCoreAsync(lease, closingFence: false);

        /// <summary>
        /// Runs the source fence with exclusive admission for the exact adapter's physical closing path.
        /// </summary>
        /// <param name="lease">
        /// Exact original lease whose dependencies are being verified.
        /// </param>
        /// <param name="closingFence">
        /// Whether the caller is the private closing path for the already declared exact adapter.
        /// </param>
        /// <returns>
        /// Completion after the admitted source fence settles outside the bookkeeping monitor.
        /// </returns>
        private async Task VerifyCoreAsync(CanonicalWriteLease lease, bool closingFence)
        {
            bool independent;
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_closed, this);
                if (_closingLease != null && (!closingFence || !ReferenceEquals(_closingLease, lease)))
                    throw new InvalidOperationException("The source adapter is closing.");
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
                _pendingDeclaredData = false;
            }
            catch { _failed = true; throw; }
            finally { LeaveWork(); }
        }

        /// <summary>
        /// Completes the owning operation's required successful-return source fence.
        /// </summary>
        /// <returns>
        /// Completion after all retained source dependencies have been verified.
        /// </returns>
        internal async Task CompleteAsync()
        {
            CanonicalWriteLease? lease;
            lock (_gate)
            {
                RequireOpen();
                if (_closingLease != null || _busy || _suspended != 0)
                    throw new InvalidOperationException("Source completion requires serial original access.");
                lease = _touched.Count == 0 ? null : _lease ?? throw new InvalidOperationException("Source fence lost its lease.");
                if (lease == null) { _completed = true; return; }
            }
            await VerifyAsync(lease);
            lock (_gate)
            {
                RequireOpen();
                if (_closingLease != null || _busy || _suspended != 0 || _verifiedVersion == null)
                    throw new InvalidOperationException("Source completion lost its verified serial access.");
                _completed = true;
            }
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
                if (_closingLease != null) throw new InvalidOperationException("Mutation conflicts with a closing DATA adapter.");
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
            finally { LeaveWork(); }
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
                if (_closingLease != null) throw new InvalidOperationException("Fresh protocol conflicts with a closing DATA adapter.");
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
            bool verify;
            bool closingFence;
            lock (_gate)
            {
                if (_closed || !ReferenceEquals(_lease, lease)) return;
                verify = !_failed && !_completed && _suspended == 0 && _touched.Count != 0;
                closingFence = ReferenceEquals(_closingLease, lease);
            }
            try
            {
                if (verify) await VerifyCoreAsync(lease, closingFence);
            }
            finally
            {
                lock (_gate) ClearReceipt();
            }
        }

        /// <summary>
        /// Excludes new functional reads before a bound physical lease starts its fresh closing fence.
        /// </summary>
        /// <param name="lease">
        /// Exact lease retaining this source guard, including after ambient operation disposal.
        /// </param>
        internal void BeginLeaseClose(CanonicalWriteLease lease)
        {
            lock (_gate)
            {
                if (!ReferenceEquals(_lease, lease)) return;
                if (_busy || _suspended != 0) throw new InvalidOperationException("Pending source work must settle before its physical lease closes.");
                _closingLease = lease;
            }
        }

        /// <summary>
        /// Restores the adapter after original admission refused teardown before resource detachment.
        /// </summary>
        /// <param name="lease">
        /// Exact lease whose attempted close was refused.
        /// </param>
        internal void CancelLeaseClose(CanonicalWriteLease lease)
        {
            lock (_gate) if (ReferenceEquals(_closingLease, lease)) _closingLease = null;
        }

        /// <summary>
        /// Drops the exact adapter only after the original physical lease has attempted all cleanup.
        /// </summary>
        /// <param name="lease">
        /// Original lease whose physical resources have finished teardown.
        /// </param>
        internal void LeaseClosed(CanonicalWriteLease lease)
        {
            lock (_gate)
            {
                lease.DetachSourceOwner(this);
                if (ReferenceEquals(_lease, lease)) _lease = null;
                if (ReferenceEquals(_generationLease, lease)) _generationLease = null;
                if (ReferenceEquals(_closingLease, lease)) _closingLease = null;
                _declaredRoutes.Clear();
                _pendingDeclaredData = false;
                ClearReceipt();
                if (_closed && !_busy && _suspended == 0) DropLocalReferences();
            }
        }

        /// <summary>
        /// Releases the serial slot and clears any local references published after disposal began.
        /// </summary>
        private void LeaveWork()
        {
            lock (_gate)
            {
                _busy = false;
                if (_closed && _suspended == 0) DropLocalReferences();
            }
        }

        /// <summary>
        /// Releases local DATA bookkeeping after source work drains while retaining an active physical close guard.
        /// </summary>
        private void DropLocalReferences()
        {
            _keys.Clear(); _declaredRoutes.Clear(); _pendingDeclaredData = false; _touched.Clear(); _generation = null; _generationLease = null;
            _rootVersion = null; SourceReadBarrierForTests = null; GenerationReadBarrierForTests = null; ClearReceipt();
            if (_closingLease == null)
            {
                _lease?.DetachSourceOwner(this);
                _lease = null;
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
            _declaredRoutes.Clear();
            _pendingDeclaredData = false;
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
                lock (owner._gate)
                {
                    owner._suspended--;
                    if (owner._closed && !owner._busy && owner._suspended == 0) owner.DropLocalReferences();
                }
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
            lock (_gate)
            {
                if (_closed) return;
                _closed = true;
                if (!_busy && _suspended == 0) DropLocalReferences();
            }
            try
            {
                _operation.Dispose();
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
