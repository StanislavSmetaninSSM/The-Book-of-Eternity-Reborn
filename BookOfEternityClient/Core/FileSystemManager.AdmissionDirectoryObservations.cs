namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    private long _admissionDirectoryInvocations, _admissionDirectoryRequests, _admissionDirectoryQueries, _admissionDirectoryHits;

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
    /// <returns>
    /// An invocation-owned context, or null when the original unshared route is required.
    /// </returns>
    private AdmissionDirectoryObservations? BeginAdmissionDirectoryObservations()
    {
        if (!CanReuseSourceData || _hooks?.OperationTimingObserver != null ||
            _operationTimingContext?.Observer != null || ValidationReuseSuppression.Value != 0)
            return null;
        var root = CanonicalRootAuthorityIdentity;
        lock (root.WorkerContextGate)
            if (root.WorkerContext != null) return null;
        return new(BasePath, enableReuse: AdmissionDirectoryReuseEnabled);
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
    }
}
