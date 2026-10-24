namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    private long _readPathInvocations, _readPathRequests, _readPathQueries, _readPathHits;

    /// <summary>
    /// Selects ordinary preparation directory DATA reuse; false retains the original source route.
    /// </summary>
    internal bool OrdinaryReadPathReuseEnabled { get; set; } = true;

    /// <summary>
    /// Captures completed ordinary preparation phases without invoking diagnostic callbacks.
    /// </summary>
    /// <returns>
    /// Cumulative phase classifications, original attempts and positive-directory hits.
    /// </returns>
    internal AdmissionDirectoryStatistics CaptureOrdinaryReadPathStatistics() => new(
        Interlocked.Read(ref _readPathInvocations), Interlocked.Read(ref _readPathRequests),
        Interlocked.Read(ref _readPathQueries), Interlocked.Read(ref _readPathHits));

    /// <summary>
    /// Creates an explicit callback-free preparation phase without issuing any storage authority.
    /// </summary>
    /// <returns>
    /// A phase for the current quiescent root, or <see langword="null"/> for the original fresh route.
    /// </returns>
    internal ReadPathObservationPhase? BeginOrdinaryReadPathPhase()
    {
        if (!TryCaptureOrdinaryReadPathVersion(out var version)) return null;
        using var timing = MeasureOperation("OrdinaryReadPath.OpenPhase");
        return new(GameSessionPath, owner: this, rootVersion: version);
    }

    /// <summary>
    /// Checks only in-memory eligibility and mutation version; native and main authority remain fresh elsewhere.
    /// </summary>
    /// <param name="version">
    /// The nonreused root token when eligible; otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for an ordinary callback-free quiescent root; otherwise, <see langword="false"/>.
    /// </returns>
    private bool TryCaptureOrdinaryReadPathVersion(out object? version)
    {
        version = null;
        if (!OrdinaryReadPathReuseEnabled || !CanReuseSourceData || ValidationReuseSuppression.Value != 0 ||
            _hooks?.OperationTimingObserver != null || _operationTimingContext?.Observer != null) return false;
        var root = CanonicalRootAuthorityIdentity;
        lock (root.WorkerContextGate)
            return root.WorkerContext == null && root.ValidationEpoch.TryCapture(out version);
    }

    /// <summary>
    /// Requires the original preparation version and current callback/worker eligibility on each reuse.
    /// </summary>
    /// <param name="version">
    /// The opaque root token captured before this preparation began.
    /// </param>
    /// <returns>
    /// <see langword="true"/> only for that same live quiescent version; otherwise, <see langword="false"/>.
    /// </returns>
    internal bool IsOrdinaryReadPathVersionCurrent(object version) =>
        TryCaptureOrdinaryReadPathVersion(out var current) && ReferenceEquals(version, current);

    /// <summary>
    /// Adds one already closed explicit preparation phase's passive work counts.
    /// </summary>
    /// <param name="counts">
    /// Counts owned by that phase; no cached DATA or authority is transferred.
    /// </param>
    internal void RecordOrdinaryReadPathStatistics(AdmissionDirectoryStatistics counts)
    {
        Interlocked.Add(ref _readPathInvocations, counts.Invocations);
        Interlocked.Add(ref _readPathRequests, counts.Requests);
        Interlocked.Add(ref _readPathQueries, counts.SourceQueries);
        Interlocked.Add(ref _readPathHits, counts.Hits);
    }
}
