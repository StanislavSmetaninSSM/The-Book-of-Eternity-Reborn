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
