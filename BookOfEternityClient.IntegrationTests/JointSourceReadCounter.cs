namespace BookOfEternityClient.Tests;

/// <summary>
/// Counts existing source-probe callbacks without reading files or retaining unbounded paths.
/// </summary>
internal sealed class JointSourceReadCounter
{
    /// <summary>
    /// Creates a counter whose bounded snapshots go to the existing stage writer.
    /// </summary>
    /// <param name="emit">
    /// The scalar snapshot sink; the caller supplies an ancillary, nonthrowing reporter.
    /// </param>
    internal JointSourceReadCounter(Action<JointSourceReadSnapshot> emit) { }

    /// <summary>
    /// Counts an existing initial read hook without opening or validating the path.
    /// </summary>
    /// <param name="path">
    /// The observed relative path; only bounded path identities are retained internally.
    /// </param>
    internal void InitialRead(string path) { }

    /// <summary>
    /// Counts the existing after-attempt callback without asserting successful I/O.
    /// </summary>
    internal void ReturnedRead() { }

    /// <summary>
    /// Counts an existing before-mutation callback without changing state.
    /// </summary>
    internal void Mutation() { }

    /// <summary>
    /// Flushes the previous interval and starts a fresh bounded interval.
    /// </summary>
    /// <param name="phase">
    /// The fixed capture-to-validated or post-validated interval name.
    /// </param>
    internal void Advance(string phase) { }

    /// <summary>
    /// Emits the current observed totals at a normal completion boundary.
    /// </summary>
    internal void Complete() { }
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
