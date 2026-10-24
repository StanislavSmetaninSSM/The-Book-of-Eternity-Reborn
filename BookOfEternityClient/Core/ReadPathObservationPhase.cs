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
    private readonly ValidationOperation _operation = new DefaultOperationValidationService().BeginOperation();
    private long _requests, _queries, _hits;
    private bool _closed;

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
    internal ReadPathObservationPhase(string root, Func<string, TrustedLocalFileScope.EntryKind>? probe = null,
        IWindowsPathMetadataProvider? windows = null, FileSystemManager? owner = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        if (!Path.IsPathFullyQualified(root)) throw new ArgumentException("The root must be absolute.", nameof(root));
        _probe = probe ?? TrustedLocalFileScope.Probe;
        _windows = windows ?? new WindowsPathMetadataProvider(null);
        _owner = owner;
    }

    /// <summary>
    /// Observes the original TrustedScope kind without granting access or file authority.
    /// </summary>
    /// <param name="path">
    /// Exact component spelling selected after the caller's lexical and grant checks.
    /// </param>
    /// <returns>
    /// The original classification; closed phases use fresh source work.
    /// </returns>
    internal TrustedLocalFileScope.EntryKind ObserveTrusted(string path)
    {
        if (!_closed) { _requests++; _queries++; }
        try { return _probe(path); }
        catch { Dispose(); throw; }
    }

    /// <inheritdoc />
    public PathMetadataObservation ObserveDirectory(string path, PathMetadataRole role)
    {
        if (!_closed) { _requests++; _queries++; }
        try { return _windows.ObserveDirectory(path, role); }
        catch { Dispose(); throw; }
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
    /// Clears service DATA and records this phase once; later calls observe fresh sources.
    /// </summary>
    public void Dispose()
    {
        if (_closed) return;
        _closed = true;
        _operation.Dispose();
        _owner?.RecordOrdinaryReadPathStatistics(CaptureStatistics());
    }
}
