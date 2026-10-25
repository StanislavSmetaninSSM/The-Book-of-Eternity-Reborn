using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

/// <summary>
/// Owns positive-directory DATA for one explicit synchronous ordinary-read preparation.
/// </summary>
internal sealed class ReadPathObservationPhase : IWindowsPathMetadataProvider, IDisposable
{
    private readonly Func<string, TrustedLocalFileScope.EntryKind> _probe;
    private readonly IWindowsPathMetadataProvider _windows;
    private readonly FileSystemManager? _owner;
    private readonly IDisposable? _timing;
    private readonly object? _rootVersion;
    private readonly string _root;
    private readonly int _thread = Environment.CurrentManagedThreadId;
    private readonly ValidationOperation _operation = new DefaultOperationValidationService().BeginOperation();
    private readonly Dictionary<string, object> _trustedKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Path, PathMetadataRole Role), object> _windowsKeys = new();
    private static readonly OperationComputation<TrustedLocalFileScope.EntryKind> TrustedDirectory =
        new("OrdinaryRead.PositiveTrustedDirectory", 1, value => value);
    private static readonly OperationComputation<PathMetadataObservation> WindowsDirectory =
        new("OrdinaryRead.PositiveWindowsDirectory", 1, value => value);
    private long _requests, _queries, _hits;
    private bool _closed, _observing;

    /// <summary>
    /// Binds original classifiers and an optional fixed manager counter destination.
    /// </summary>
    /// <param name="root">
    /// The absolute session directory whose related directory components may supply DATA.
    /// </param>
    /// <param name="probe">
    /// The original classifier; <see langword="null"/> selects the physical classifier.
    /// </param>
    /// <param name="windows">
    /// The original Windows provider; <see langword="null"/> selects its callback-free physical implementation.
    /// </param>
    /// <param name="owner">
    /// Optional manager receiving counters once on close, without a caller callback.
    /// </param>
    /// <param name="rootVersion">
    /// The manager's captured quiescent version. With an owner, <see langword="null"/> selects fresh fallback;
    /// isolated classifier fixtures do not require a token.
    /// </param>
    internal ReadPathObservationPhase(string root, Func<string, TrustedLocalFileScope.EntryKind>? probe = null,
        IWindowsPathMetadataProvider? windows = null, FileSystemManager? owner = null, object? rootVersion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        if (!Path.IsPathFullyQualified(root)) throw new ArgumentException("The root must be absolute.", nameof(root));
        _probe = probe ?? TrustedLocalFileScope.Probe;
        _windows = windows ?? new WindowsPathMetadataProvider(null);
        _owner = owner;
        _rootVersion = rootVersion;
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        _timing = owner?.MeasureOperation("OrdinaryReadPath.Phase");
    }

    /// <summary>
    /// Selects this phase only on its original synchronous flow and unchanged manager version.
    /// </summary>
    internal ReadPathObservationPhase? Active => CanReuse() ? this : null;

    /// <summary>
    /// Observes the original TrustedScope kind without granting access or file authority.
    /// </summary>
    /// <param name="path">
    /// Exact component spelling selected after the caller's lexical and grant checks.
    /// </param>
    /// <returns>
    /// The original classification; closed phases use fresh source work.
    /// </returns>
    internal TrustedLocalFileScope.EntryKind ObserveTrusted(string path) => ObserveTrustedCore(path, out _);

    /// <summary>
    /// Selects this preparation's directory DATA only for its original eligible manager admission.
    /// </summary>
    /// <param name="owner">
    /// Exact manager requesting the initial admission; another manager is never eligible.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for this live synchronous phase with both sharing policies enabled;
    /// otherwise, <see langword="false"/> keeps the original classifier route.
    /// </returns>
    internal bool CanShareAdmission(FileSystemManager owner) => _owner != null && ReferenceEquals(_owner, owner) &&
        owner.OrdinaryReadAdmissionReuseEnabled && owner.AdmissionDirectoryReuseEnabled && CanReuse();

    /// <summary>
    /// Classifies one initial-admission component and reports whether the original source was queried.
    /// </summary>
    /// <param name="path">
    /// Exact normalized component selected after the admission's original grant checks.
    /// </param>
    /// <param name="sourceQueried">
    /// Receives <see langword="true"/> when the original classifier was attempted, including failure;
    /// <see langword="false"/> denotes retained DATA or a failure before source access.
    /// </param>
    /// <returns>
    /// Original entry DATA; disabled sharing, closed phases and foreign threads use fresh classification.
    /// </returns>
    internal TrustedLocalFileScope.EntryKind ObserveForAdmission(string path, out bool sourceQueried)
    {
        if (_owner is { } owner && (!owner.OrdinaryReadAdmissionReuseEnabled || !owner.AdmissionDirectoryReuseEnabled))
        {
            sourceQueried = true;
            return _probe(path);
        }
        return ObserveTrustedCore(path, out sourceQueried);
    }

    /// <summary>
    /// Shares eligible positive directory DATA while distinguishing a source attempt from a retained observation.
    /// </summary>
    /// <param name="path">
    /// Exact original classifier spelling.
    /// </param>
    /// <param name="sourceQueried">
    /// Receives whether the original classifier was attempted before completion or failure.
    /// </param>
    /// <returns>
    /// Original entry DATA without file or access authority.
    /// </returns>
    private TrustedLocalFileScope.EntryKind ObserveTrustedCore(string path, out bool sourceQueried)
    {
        sourceQueried = false;
        if (!CanReuse())
        {
            sourceQueried = true;
            return _probe(path);
        }
        Enter();
        _requests++;
        try
        {
            if (_trustedKeys.TryGetValue(path, out var key))
            {
                var retained = _operation.GetOrCompute(TrustedDirectory, key, [],
                    static () => throw new InvalidOperationException("Retained directory DATA is unavailable."));
                if (!CanReuse())
                {
                    sourceQueried = true;
                    return _probe(path);
                }
                _hits++;
                return retained;
            }
            _queries++;
            sourceQueried = true;
            var kind = _probe(path);
            if (CanReuse() && kind == TrustedLocalFileScope.EntryKind.Directory && IsRelated(path))
            {
                key = new object();
                _operation.GetOrCompute(TrustedDirectory, key, [], () => kind);
                _trustedKeys.Add(path, key);
            }
            return kind;
        }
        catch { Dispose(); throw; }
        finally { _observing = false; }
    }

    /// <inheritdoc />
    public PathMetadataObservation ObserveDirectory(string path, PathMetadataRole role)
    {
        if (!CanReuse()) return _windows.ObserveDirectory(path, role);
        Enter();
        _requests++;
        try
        {
            var identity = (path, role);
            if (_windowsKeys.TryGetValue(identity, out var key))
            {
                var retained = _operation.GetOrCompute(WindowsDirectory, key, [],
                    static () => throw new InvalidOperationException("Retained Windows directory DATA is unavailable."));
                if (!CanReuse()) return _windows.ObserveDirectory(path, role);
                _hits++;
                return retained;
            }
            _queries++;
            var observation = _windows.ObserveDirectory(path, role);
            if (CanReuse() && observation.Exists && observation.CachedAttributes is { } attributes &&
                (attributes & FileAttributes.Directory) != 0 && (attributes & FileAttributes.ReparsePoint) == 0 && IsRelated(path))
            {
                key = new object();
                _operation.GetOrCompute(WindowsDirectory, key, [], () => observation);
                _windowsKeys.Add(identity, key);
            }
            return observation;
        }
        catch { Dispose(); throw; }
        finally { _observing = false; }
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
    /// Captures this lifetime's classification requests, original attempts and directory reuses.
    /// </summary>
    /// <returns>
    /// Passive counts without invoking a classifier or retaining storage authority.
    /// </returns>
    internal AdmissionDirectoryStatistics CaptureStatistics() => new(1, _requests, _queries, _hits);

    /// <summary>
    /// Inspects the service's retained entry counts without exposing cached values.
    /// </summary>
    /// <returns>
    /// The existing operation service's current diagnostic state.
    /// </returns>
    internal ValidationOperationState CaptureState() => _operation.CaptureState();

    /// <summary>
    /// Rejects reentrant classifier execution before it can reuse partially observed DATA.
    /// </summary>
    private void Enter()
    {
        if (_observing)
        {
            Dispose();
            throw new InvalidOperationException("Ordinary preparation observations cannot be reentered.");
        }
        _observing = true;
    }

    /// <summary>
    /// Retires DATA after a manager version change; foreign threads use fresh sources.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> only for this open phase's unchanged synchronous flow.
    /// </returns>
    private bool CanReuse()
    {
        if (_closed || Environment.CurrentManagedThreadId != _thread) return false;
        if (_owner is null || _rootVersion is { } version && _owner.IsOrdinaryReadPathVersionCurrent(version)) return true;
        Dispose();
        return false;
    }

    /// <summary>
    /// Restricts directory DATA to exact components related to the selected session root.
    /// </summary>
    /// <param name="path">
    /// Original component spelling, after the caller's lexical and grant validation.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for the root, descendants or ancestors; otherwise, <see langword="false"/>.
    /// </returns>
    private bool IsRelated(string path) => !string.IsNullOrEmpty(path) &&
        (string.Equals(path, _root, StringComparison.Ordinal) ||
         path.StartsWith(_root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
         _root.StartsWith(path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar, StringComparison.Ordinal));

    /// <summary>
    /// Clears service DATA and records this phase once; later calls observe fresh sources.
    /// </summary>
    public void Dispose()
    {
        if (_closed) return;
        _closed = true;
        _trustedKeys.Clear();
        _windowsKeys.Clear();
        _operation.Dispose();
        _timing?.Dispose();
        _owner?.RecordOrdinaryReadPathStatistics(CaptureStatistics());
    }
}
