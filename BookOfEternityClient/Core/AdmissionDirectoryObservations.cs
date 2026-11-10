using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

/// <summary>
/// Describes source classification work without granting directory or file authority.
/// </summary>
/// <param name="Invocations">
/// Number of completed or failed observation lifetimes, including serial asynchronous batches.
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
    private readonly string _root;
    private readonly bool _enableReuse;
    private readonly IReadPathObservations? _sharedReadPhase;
    private readonly ValidationOperation _operation = new DefaultOperationValidationService().BeginOperation();
    private readonly Dictionary<string, object> _keys = new(StringComparer.Ordinal);
    private static readonly OperationComputation<TrustedLocalFileScope.EntryKind> DirectoryData =
        new("MainAdmission.PositiveDirectoryData", 1, value => value);
    private bool _closed;
    private bool _observing;
    private long _requests, _queries, _hits;

    /// <summary>
    /// Gets observations borrowed from an outer bounded read context rather than this invocation's cache.
    /// </summary>
    internal long SharedHits { get; private set; }

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
    /// <param name="sharedReadPhase">
    /// Optional caller-admitted bounded read context. It is consulted only with the default physical
    /// classifier and enabled reuse; borrowed DATA is never retained in this invocation's cache.
    /// </param>
    internal AdmissionDirectoryObservations(string root,
        Func<string, TrustedLocalFileScope.EntryKind>? probe = null, bool enableReuse = true,
        IReadPathObservations? sharedReadPhase = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        if (!Path.IsPathFullyQualified(root)) throw new ArgumentException("The root must be absolute.", nameof(root));
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        _enableReuse = enableReuse;
        _probe = probe ?? TrustedLocalFileScope.Probe;
        _sharedReadPhase = enableReuse && probe == null ? sharedReadPhase : null;
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
        if (_observing)
        {
            Dispose();
            throw new InvalidOperationException("Admission observations cannot be reentered.");
        }
        _observing = true;
        _requests++;
        try
        {
            if (_keys.TryGetValue(path, out var key))
            {
                var cached = _operation.GetOrCompute(DirectoryData, key, [],
                    () => throw new InvalidOperationException("Retained directory DATA is unavailable."));
                if (cached != TrustedLocalFileScope.EntryKind.Directory)
                    throw new InvalidOperationException("Retained DATA is not a positive directory observation.");
                _hits++;
                return cached;
            }
            var sourceQueried = false;
            TrustedLocalFileScope.EntryKind kind;
            try
            {
                if (_sharedReadPhase is { } phase)
                    kind = phase.ObserveForAdmission(path, out sourceQueried);
                else
                {
                    sourceQueried = true;
                    kind = _probe(path);
                }
            }
            finally
            {
                if (sourceQueried) _queries++;
            }
            ObjectDisposedException.ThrowIf(_closed, this);
            if (!sourceQueried)
            {
                _hits++;
                SharedHits++;
            }
            if (sourceQueried && _enableReuse && kind == TrustedLocalFileScope.EntryKind.Directory && IsRelated(path))
            {
                key = new object();
                _operation.GetOrCompute(DirectoryData, key, [], () => kind);
                _keys.Add(path, key);
            }
            return kind;
        }
        catch { Dispose(); throw; }
        finally { _observing = false; }
    }

    /// <summary>
    /// Captures lifetime work without invoking a callback or filesystem query.
    /// </summary>
    /// <returns>
    /// Counts for this invocation only.
    /// </returns>
    internal AdmissionDirectoryStatistics CaptureStatistics() => new(1, _requests, _queries, _hits);

    /// <summary>
    /// Reports retained service state without exposing cached paths or permissions.
    /// </summary>
    /// <returns>
    /// Entry/dependency counts and whether the lifetime has closed.
    /// </returns>
    internal ValidationOperationState CaptureState() => _operation.CaptureState();

    /// <summary>
    /// Restricts identities to exact ordinal components under the selected root or its ancestors.
    /// </summary>
    /// <param name="path">
    /// Normalized component supplied after the caller's spelling and grant checks.
    /// </param>
    /// <returns>
    /// True for the root or a component-related name; false for unrelated names.
    /// </returns>
    private bool IsRelated(string path) => string.Equals(path, _root, StringComparison.Ordinal) ||
        path.StartsWith(_root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
        _root.StartsWith(path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    /// <summary>
    /// Ends this invocation before any later admission can use its observations.
    /// </summary>
    public void Dispose()
    {
        if (_closed) return;
        _closed = true;
        _keys.Clear();
        _operation.Dispose();
    }
}
