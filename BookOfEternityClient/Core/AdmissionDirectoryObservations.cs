using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

/// <summary>
/// Describes source classification work without granting directory or file authority.
/// </summary>
/// <param name="Invocations">
/// Number of completed or failed synchronous observation lifetimes.
/// </param>
/// <param name="Requests">
/// Number of classifier requests received by those lifetimes.
/// </param>
/// <param name="SourceQueries">
/// Number of original classifier attempts, including failed attempts.
/// </param>
/// <param name="Hits">
/// Number of successful directory DATA reuses.
/// </param>
internal readonly record struct AdmissionDirectoryStatistics(long Invocations, long Requests, long SourceQueries, long Hits)
{
    /// <summary>
    /// Calculates the work recorded between two captures from the same owner.
    /// </summary>
    /// <param name="after">
    /// The later cumulative capture.
    /// </param>
    /// <param name="before">
    /// The earlier cumulative capture.
    /// </param>
    /// <returns>
    /// Component-wise differences without clamping or changing signed values.
    /// </returns>
    public static AdmissionDirectoryStatistics operator -(AdmissionDirectoryStatistics after, AdmissionDirectoryStatistics before) =>
        new(after.Invocations - before.Invocations, after.Requests - before.Requests,
            after.SourceQueries - before.SourceQueries, after.Hits - before.Hits);
}

/// <summary>
/// Owns directory observation DATA for exactly one synchronous composed admission.
/// </summary>
internal sealed class AdmissionDirectoryObservations : IDisposable
{
    private readonly Func<string, TrustedLocalFileScope.EntryKind> _probe;
    private bool _closed;
    private long _requests, _queries;

    /// <summary>
    /// Selects the exact root, original classifier and per-invocation reuse policy.
    /// </summary>
    /// <param name="root">
    /// The fully qualified root whose ordinary admission is being validated.
    /// </param>
    /// <param name="probe">
    /// Original classifier or an isolated fixture source; null selects the physical classifier.
    /// </param>
    /// <param name="enableReuse">
    /// Whether positive directory observations may be shared in this invocation.
    /// </param>
    internal AdmissionDirectoryObservations(string root,
        Func<string, TrustedLocalFileScope.EntryKind>? probe = null, bool enableReuse = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _probe = probe ?? TrustedLocalFileScope.Probe;
    }

    /// <summary>
    /// Observes one normalized component while preserving original classifier errors.
    /// </summary>
    /// <param name="path">
    /// Exact normalized component already selected by the caller's grant checks.
    /// </param>
    /// <returns>
    /// The original kind; this value does not grant file access or mutation rights.
    /// </returns>
    internal TrustedLocalFileScope.EntryKind Observe(string path)
    {
        ObjectDisposedException.ThrowIf(_closed, this);
        _requests++;
        _queries++;
        try { return _probe(path); }
        catch { Dispose(); throw; }
    }

    /// <summary>
    /// Captures lifetime work without invoking a callback or filesystem query.
    /// </summary>
    /// <returns>
    /// Counts for this invocation only.
    /// </returns>
    internal AdmissionDirectoryStatistics CaptureStatistics() => new(1, _requests, _queries, 0);

    /// <summary>
    /// Reports retained service state without exposing cached paths or permissions.
    /// </summary>
    /// <returns>
    /// Entry/dependency counts and whether the lifetime has closed.
    /// </returns>
    internal ValidationOperationState CaptureState() => new(0, 0, _closed);

    /// <summary>
    /// Ends this invocation before any later admission can use its observations.
    /// </summary>
    public void Dispose() => _closed = true;
}
