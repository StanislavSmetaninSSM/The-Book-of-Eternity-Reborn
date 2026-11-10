namespace BookOfEternityClient.Tests;

/// <summary>
/// Counts existing source-probe callbacks without reading files or retaining unbounded paths.
/// </summary>
internal sealed class JointSourceReadCounter
{
    private readonly Action<JointSourceReadSnapshot> _emit;
    private readonly object _gate = new();
    private readonly HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);
    private string _phase = "before-capture";
    private long _initial, _returned, _rollback, _repeated, _mutations, _nextReport = 1;
    private bool _pathLimit;
    /// <summary>
    /// Creates a counter whose bounded snapshots go to the existing stage writer.
    /// </summary>
    /// <param name="emit">
    /// The scalar snapshot sink; the caller supplies an ancillary, nonthrowing reporter.
    /// </param>
    internal JointSourceReadCounter(Action<JointSourceReadSnapshot> emit) => _emit = emit;

    /// <summary>
    /// Counts an existing initial read hook without opening or validating the path.
    /// </summary>
    /// <param name="path">
    /// The observed relative path; only bounded path identities are retained internally.
    /// </param>
    internal void InitialRead(string path)
    {
        lock (_gate)
        {
            _initial++;
            if (path.Contains(".rollback.", StringComparison.OrdinalIgnoreCase))
            {
                _rollback++;
                if (path.Length > 512) _pathLimit = true;
                else if (_paths.Contains(path)) _repeated++;
                else if (_paths.Count < 1024) _paths.Add(path);
                else _pathLimit = true;
            }
            EmitPeriodic();
        }
    }

    /// <summary>
    /// Counts the existing after-attempt callback without asserting successful I/O.
    /// </summary>
    internal void ReturnedRead()
    {
        lock (_gate) { _returned++; EmitPeriodic(); }
    }

    /// <summary>
    /// Counts an existing before-mutation callback without changing state.
    /// </summary>
    internal void Mutation()
    {
        lock (_gate) _mutations++;
    }

    /// <summary>
    /// Flushes the previous interval and starts a fresh bounded interval.
    /// </summary>
    /// <param name="phase">
    /// The fixed capture-to-validated or post-validated interval name.
    /// </param>
    internal void Advance(string phase)
    {
        if (phase is not ("capture-to-validated" or "post-validated"))
            throw new ArgumentOutOfRangeException(nameof(phase));
        lock (_gate)
        {
            Emit();
            _phase = phase;
            _initial = _returned = _rollback = _repeated = _mutations = 0;
            _paths.Clear(); _pathLimit = false; _nextReport = 1;
        }
    }

    /// <summary>
    /// Emits the current observed totals at a normal completion boundary.
    /// </summary>
    internal void Complete()
    {
        lock (_gate) Emit();
    }

    /// <summary>
    /// Emits at eight fixed read-count thresholds while the caller holds the counter gate.
    /// </summary>
    private void EmitPeriodic()
    {
        if (_nextReport <= 16384 && _initial + _returned >= _nextReport)
        {
            Emit(); _nextReport *= 4;
        }
    }

    /// <summary>
    /// Copies scalar totals to the ancillary sink while the caller holds the counter gate.
    /// </summary>
    private void Emit() => _emit(new(_phase, _initial, _returned, _rollback, _paths.Count, _repeated, _mutations, _pathLimit));
}

/// <summary>
/// Carries scalar callback counts; truncated observations remain lower bounds.
/// </summary>
/// <param name="Phase">
/// The fixed source interval name.
/// </param>
/// <param name="InitialReads">
/// Initial read hooks observed, not completed reads.
/// </param>
/// <param name="ReturnedReads">
/// After-attempt hooks observed, not a success count.
/// </param>
/// <param name="RollbackReads">
/// Initial hooks whose relative path contains the rollback marker.
/// </param>
/// <param name="UniqueRollbackPaths">
/// Distinct retained rollback paths, bounded to1024.
/// </param>
/// <param name="RepeatedRollbackReads">
/// Repeated initial hooks for a retained rollback path.
/// </param>
/// <param name="Mutations">
/// Before-mutation callbacks observed in this interval.
/// </param>
/// <param name="PathLimitReached">
/// Whether any path could not be tracked because of the count or length bound.
/// </param>
internal sealed record JointSourceReadSnapshot(string Phase, long InitialReads, long ReturnedReads,
    long RollbackReads, int UniqueRollbackPaths, long RepeatedRollbackReads, long Mutations, bool PathLimitReached);
