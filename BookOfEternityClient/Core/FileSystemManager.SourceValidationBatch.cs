using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    private long _sourceBatchInvocations, _sourceBatchRequests, _sourceBatchQueries, _sourceBatchHits;

    /// <summary>
    /// Captures completed bounded batches without invoking diagnostic callbacks.
    /// </summary>
    /// <returns>
    /// Cumulative directory requests, original classifier attempts and positive DATA hits.
    /// </returns>
    internal AdmissionDirectoryStatistics CaptureSourceValidationBatchStatistics() => new(
        Interlocked.Read(ref _sourceBatchInvocations), Interlocked.Read(ref _sourceBatchRequests),
        Interlocked.Read(ref _sourceBatchQueries), Interlocked.Read(ref _sourceBatchHits));

    /// <summary>
    /// Excludes arbitrary callbacks that could change directory DATA without a supported mutation epoch.
    /// </summary>
    private bool CanReuseSourceValidationBatch => SourceValidationBatchReuseEnabled && CanReuseSourceData &&
        ValidationReuseSuppression.Value == 0 && _hooks?.CanonicalReadActivityObserver == null &&
        _hooks?.ExactPhysicalReadActivityObserver == null && _hooks?.OperationTimingObserver == null &&
        _hooks?.BrowserOriginalAdmissionTimingObserver == null && _operationTimingContext?.Observer == null;

    /// <summary>
    /// Creates directory DATA only under an already admitted exact serial source owner.
    /// </summary>
    /// <param name="lease">
    /// Original caller-owned physical lease.
    /// </param>
    /// <param name="owner">
    /// Existing source or reserved owner retaining its original work slot.
    /// </param>
    /// <param name="work">
    /// Never-reused identity of this bounded batch within that work slot.
    /// </param>
    /// <returns>
    /// A new empty batch, or <see langword="null"/> for the original fresh route.
    /// </returns>
    private SourceValidationBatch? BeginSourceValidationBatch(CanonicalWriteLease lease,
        ISourceValidationBatchOwner owner, object work)
    {
        if (!CanReuseSourceValidationBatch || !owner.TryCaptureBatchVersion(work, out var version) || version == null ||
            MainAdmissions.Value is not { } entry || lease.AmbientRegistration is not { } registration) return null;
        return new(this, lease, owner, work, version, entry, registration);
    }

    /// <summary>
    /// Supplies an in-memory proof of exact serial ownership without filesystem authority or callbacks.
    /// </summary>
    internal interface ISourceValidationBatchOwner
    {
        /// <summary>
        /// Captures the current root token only for the original healthy serial work identity.
        /// </summary>
        /// <param name="work">
        /// Exact identity installed by the owner for this bounded batch.
        /// </param>
        /// <param name="version">
        /// Current quiescent root token, or <see langword="null"/> for fresh fallback.
        /// </param>
        /// <returns>
        /// <see langword="true"/> while original ownership is current; otherwise, <see langword="false"/>.
        /// </returns>
        bool TryCaptureBatchVersion(object work, out object? version);
    }

    /// <summary>
    /// Retains positive directory DATA for one explicit serial source read or fresh-source loop.
    /// </summary>
    internal sealed class SourceValidationBatch : IReadPathObservations, IDisposable
    {
        private readonly FileSystemManager _files;
        private readonly CanonicalWriteLease _lease;
        private readonly ISourceValidationBatchOwner _owner;
        private readonly object _work, _version;
        private readonly MainAdmission _entry;
        private readonly AmbientCanonicalLeaseRegistration _registration;
        private readonly object _gate = new();
        private readonly string _root;
        private readonly ValidationOperation _data = new DefaultOperationValidationService().BeginOperation();
        private readonly Dictionary<string, object> _trustedKeys = new(StringComparer.Ordinal);
        private readonly Dictionary<(string Path, PathMetadataRole Role), object> _windowsKeys = new();
        private readonly IWindowsPathMetadataProvider _windows = new WindowsPathMetadataProvider(null);
        private readonly IDisposable? _timing;
        private static readonly OperationComputation<TrustedLocalFileScope.EntryKind> TrustedDirectory =
            new("SourceValidation.PositiveTrustedDirectory", 1, value => value);
        private static readonly OperationComputation<PathMetadataObservation> WindowsDirectory =
            new("SourceValidation.PositiveWindowsDirectory", 1, value => value);
        private bool _closed, _observing;
        private long _requests, _queries, _hits;

        /// <summary>
        /// Binds an empty directory epoch to original in-memory ownership captured by the manager.
        /// </summary>
        /// <param name="files">
        /// Exact manager owning this batch and receiving passive counters.
        /// </param>
        /// <param name="lease">
        /// Original active physical lease, never owned or replaced by this batch.
        /// </param>
        /// <param name="owner">
        /// Existing serial source owner.
        /// </param>
        /// <param name="work">
        /// Exact never-reused serial work identity.
        /// </param>
        /// <param name="version">
        /// Original quiescent root token.
        /// </param>
        /// <param name="entry">
        /// Exact current main-admission frame.
        /// </param>
        /// <param name="registration">
        /// Original ambient physical lease registration.
        /// </param>
        internal SourceValidationBatch(FileSystemManager files, CanonicalWriteLease lease,
            ISourceValidationBatchOwner owner, object work, object version, MainAdmission entry,
            AmbientCanonicalLeaseRegistration registration)
        {
            _files = files; _lease = lease; _owner = owner; _work = work; _version = version;
            _entry = entry; _registration = registration;
            _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(files.GameSessionPath));
            _timing = files.MeasureOperation("SourceValidationBatch.Lifetime");
        }

        /// <inheritdoc />
        public IReadPathObservations? Active { get { lock (_gate) return CanReuse() ? this : null; } }

        /// <inheritdoc />
        public bool CanShareAdmission(FileSystemManager owner) => ReferenceEquals(owner, _files) &&
            owner.AdmissionDirectoryReuseEnabled && Active != null;

        /// <inheritdoc />
        public TrustedLocalFileScope.EntryKind ObserveTrusted(string path) => ObserveForAdmission(path, out _);

        /// <inheritdoc />
        public TrustedLocalFileScope.EntryKind ObserveForAdmission(string path, out bool sourceQueried)
        {
            lock (_gate)
            {
                sourceQueried = false;
                if (!CanReuse()) { sourceQueried = true; return TrustedLocalFileScope.Probe(path); }
                Enter(); _requests++;
                try
                {
                    if (_trustedKeys.TryGetValue(path, out var key))
                    {
                        var retained = _data.GetOrCompute(TrustedDirectory, key, [], Missing<TrustedLocalFileScope.EntryKind>);
                        if (CanReuse()) { _hits++; return retained; }
                        sourceQueried = true; return TrustedLocalFileScope.Probe(path);
                    }
                    _queries++; sourceQueried = true;
                    var kind = TrustedLocalFileScope.Probe(path);
                    if (CanReuse() && kind == TrustedLocalFileScope.EntryKind.Directory && IsRelated(path))
                    {
                        key = new object();
                        _data.GetOrCompute(TrustedDirectory, key, [], () => kind);
                        _trustedKeys.Add(path, key);
                    }
                    return kind;
                }
                catch { Dispose(); throw; }
                finally { _observing = false; }
            }
        }

        /// <inheritdoc />
        public PathMetadataObservation ObserveDirectory(string path, PathMetadataRole role)
        {
            lock (_gate)
            {
                if (!CanReuse()) return _windows.ObserveDirectory(path, role);
                Enter(); _requests++;
                try
                {
                    var identity = (path, role);
                    if (_windowsKeys.TryGetValue(identity, out var key))
                    {
                        var retained = _data.GetOrCompute(WindowsDirectory, key, [], Missing<PathMetadataObservation>);
                        if (CanReuse()) { _hits++; return retained; }
                        return _windows.ObserveDirectory(path, role);
                    }
                    _queries++;
                    var value = _windows.ObserveDirectory(path, role);
                    if (CanReuse() && value.Exists && value.CachedAttributes is { } attrs &&
                        (attrs & FileAttributes.Directory) != 0 && (attrs & FileAttributes.ReparsePoint) == 0 && IsRelated(path))
                    {
                        key = new object();
                        _data.GetOrCompute(WindowsDirectory, key, [], () => value);
                        _windowsKeys.Add(identity, key);
                    }
                    return value;
                }
                catch { Dispose(); throw; }
                finally { _observing = false; }
            }
        }

        /// <inheritdoc />
        public PathMetadataObservation ObserveFile(string path, PathMetadataRole role) => _windows.ObserveFile(path, role);

        /// <inheritdoc />
        public bool LegacyDirectoryExists(string path, PathMetadataRole role) => _windows.LegacyDirectoryExists(path, role);

        /// <inheritdoc />
        public bool LegacyFileExists(string path, PathMetadataRole role) => _windows.LegacyFileExists(path, role);

        /// <inheritdoc />
        public bool IsReparsePoint(string path, PathMetadataRole role) => _windows.IsReparsePoint(path, role);

        /// <summary>
        /// Refuses a missing retained entry rather than silently adopting a new baseline.
        /// </summary>
        /// <typeparam name="T">
        /// Expected immutable directory DATA type.
        /// </typeparam>
        /// <returns>
        /// No value; this invariant failure always throws.
        /// </returns>
        private static T Missing<T>() => throw new InvalidOperationException("Source directory DATA was retired.");

        /// <summary>
        /// Permanently retires the batch on exact ownership or version loss.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> only for the original eligible serial work and root token.
        /// </returns>
        private bool CanReuse()
        {
            if (_closed) return false;
            if (_files.CanReuseSourceValidationBatch && ReferenceEquals(MainAdmissions.Value, _entry) &&
                ReferenceEquals(_lease.AmbientRegistration, _registration) &&
                _files.IsBorrowedValidationReadFlowCurrent(_lease, _entry) &&
                _owner.TryCaptureBatchVersion(_work, out var current) && ReferenceEquals(current, _version)) return true;
            Dispose(); return false;
        }

        /// <summary>
        /// Rejects overlapping or reentrant observations before partial DATA can be reused.
        /// </summary>
        private void Enter()
        {
            if (_observing) { Dispose(); throw new InvalidOperationException("Source batch observation was reentered."); }
            _observing = true;
        }

        /// <summary>
        /// Restricts DATA to exact components related to the session without changing grant decisions.
        /// </summary>
        /// <param name="path">
        /// Exact component selected by the caller after its grant checks.
        /// </param>
        /// <returns>
        /// <see langword="true"/> for the root, ancestors or descendants; otherwise, <see langword="false"/>.
        /// </returns>
        private bool IsRelated(string path) => string.Equals(path, _root, StringComparison.Ordinal) ||
            path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            _root.StartsWith(path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar, StringComparison.Ordinal);

        /// <summary>
        /// Clears directory DATA and records this completed batch once; later observations are fresh.
        /// </summary>
        public void Dispose()
        {
            lock (_gate)
            {
                if (_closed) return;
                _closed = true;
                _trustedKeys.Clear(); _windowsKeys.Clear(); _data.Dispose(); _timing?.Dispose();
                Interlocked.Increment(ref _files._sourceBatchInvocations);
                Interlocked.Add(ref _files._sourceBatchRequests, _requests);
                Interlocked.Add(ref _files._sourceBatchQueries, _queries);
                Interlocked.Add(ref _files._sourceBatchHits, _hits);
            }
        }
    }
}
