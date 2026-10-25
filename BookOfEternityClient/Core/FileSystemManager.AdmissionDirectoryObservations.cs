namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    private long _admissionDirectoryInvocations, _admissionDirectoryRequests, _admissionDirectoryQueries, _admissionDirectoryHits;
    private long _sharedReadAdmissionHits;

    /// <summary>
    /// Captures completed main admissions' directory hits borrowed from explicit bounded read contexts.
    /// </summary>
    /// <returns>
    /// Cumulative borrowed DATA hits, excluding invocation-local cache hits and native authority queries.
    /// </returns>
    internal long CaptureSharedReadAdmissionHits() => Interlocked.Read(ref _sharedReadAdmissionHits);

    /// <summary>
    /// Selects the original uncached classifier route for controlled fixture comparisons when false.
    /// </summary>
    internal bool AdmissionDirectoryReuseEnabled { get; set; } = true;

    /// <summary>
    /// Captures cumulative classifier work without invoking diagnostic callbacks.
    /// </summary>
    /// <returns>
    /// Counts from completed or failed admission contexts; native handle queries are not included.
    /// </returns>
    internal AdmissionDirectoryStatistics CaptureAdmissionDirectoryStatistics() => new(
        Interlocked.Read(ref _admissionDirectoryInvocations), Interlocked.Read(ref _admissionDirectoryRequests),
        Interlocked.Read(ref _admissionDirectoryQueries), Interlocked.Read(ref _admissionDirectoryHits));

    /// <summary>
    /// Creates one explicit observation lifetime only on the ordinary callback-free route.
    /// </summary>
    /// <param name="readPathPhase">
    /// Optional bounded read observations from this exact manager; foreign, closed or disabled
    /// phases retain the original independent admission context.
    /// </param>
    /// <returns>
    /// An invocation-owned context, or null when the original unshared route is required.
    /// </returns>
    internal AdmissionDirectoryObservations? BeginAdmissionDirectoryObservations(IReadPathObservations? readPathPhase = null)
    {
        if (!CanReuseSourceData || _hooks?.OperationTimingObserver != null ||
            _operationTimingContext?.Observer != null || ValidationReuseSuppression.Value != 0)
            return null;
        var root = CanonicalRootAuthorityIdentity;
        lock (root.WorkerContextGate)
            if (root.WorkerContext != null) return null;
        return new(BasePath, enableReuse: AdmissionDirectoryReuseEnabled,
            sharedReadPhase: readPathPhase?.CanShareAdmission(this) == true ? readPathPhase : null);
    }

    /// <summary>
    /// Closes the observation lifetime before adding its passive work totals.
    /// </summary>
    /// <param name="observations">
    /// This invocation's context; null means the original unshared route produced no context totals.
    /// </param>
    private void EndAdmissionDirectoryObservations(AdmissionDirectoryObservations? observations)
    {
        if (observations == null) return;
        observations.Dispose();
        var counts = observations.CaptureStatistics();
        Interlocked.Add(ref _admissionDirectoryInvocations, counts.Invocations);
        Interlocked.Add(ref _admissionDirectoryRequests, counts.Requests);
        Interlocked.Add(ref _admissionDirectoryQueries, counts.SourceQueries);
        Interlocked.Add(ref _admissionDirectoryHits, counts.Hits);
        Interlocked.Add(ref _sharedReadAdmissionHits, observations.SharedHits);
    }
}
