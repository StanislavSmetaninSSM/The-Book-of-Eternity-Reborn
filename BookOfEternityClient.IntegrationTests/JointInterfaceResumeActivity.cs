using BookOfEternityClient.Core;
using System.Diagnostics;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Identifies the distinct existing hooks observed by a Windows resumer.
/// </summary>
internal enum JointResumeObservation
{
    /// <summary>
    /// Observes canonical read initial validation.
    /// </summary>
    CanonicalInitial,
    /// <summary>
    /// Observes exact physical read initial validation.
    /// </summary>
    ExactInitial,
    /// <summary>
    /// Observes an opened canonical write lock.
    /// </summary>
    CanonicalOpened,
    /// <summary>
    /// Observes canonical write-lock contention.
    /// </summary>
    CanonicalContended,
    /// <summary>
    /// Observes main-owner contention.
    /// </summary>
    MainContended,
}

/// <summary>
/// Observes bounded scalar activity without opening files or changing game results.
/// </summary>
internal sealed class JointInterfaceResumeActivity
{
    private readonly JointInterfaceCrashStageReport _report;
    private readonly Func<TimeSpan> _elapsed;
    private readonly object _gate = new();
    private readonly long[] _counts = new long[7];
    private string _phase = "before-finalization";
    private string? _publication, _recovery;
    private TimeSpan _nextReport;
    private int _reports;
    /// <summary>
    /// Creates a child-local observer with a monotonic clock seam for finite tests.
    /// </summary>
    /// <param name="report">
    /// The existing nonthrowing stage sink, retaining the child's independent identity.
    /// </param>
    /// <param name="elapsed">
    /// A monotonic elapsed clock; <see langword="null"/> uses <see cref="Stopwatch"/> without creating a timer.
    /// </param>
    internal JointInterfaceResumeActivity(JointInterfaceCrashStageReport report, Func<TimeSpan>? elapsed = null)
    {
        _report = report;
        var started = Stopwatch.GetTimestamp();
        _elapsed = elapsed ?? (() => Stopwatch.GetElapsedTime(started));
    }

    /// <summary>
    /// Counts one existing hook invocation without asserting successful I/O.
    /// </summary>
    /// <param name="observation">
    /// The fixed hook population to increment.
    /// </param>
    internal void Observe(JointResumeObservation observation)
    {
        if (!Enum.IsDefined(observation)) return;
        lock (_gate)
        {
            _counts[(int)observation]++;
            EmitIfDue();
        }
    }

    /// <summary>
    /// Counts a publication or recovery observation without reading its journal.
    /// </summary>
    /// <param name="phase">
    /// The existing publication phase enum.
    /// </param>
    /// <param name="recovery">
    /// <see langword="true"/> selects recovery; <see langword="false"/> selects ordinary publication.
    /// </param>
    internal void Publication(TrustedLocalPublicationPhase phase, bool recovery)
    {
        var name = Enum.IsDefined(phase) ? phase.ToString() : "Unknown";
        lock (_gate)
        {
            _counts[recovery ? 6 : 5]++;
            if (recovery) _recovery = name; else _publication = name;
            EmitIfDue();
        }
    }

    /// <summary>
    /// Names a real callback boundary and partitions activity at callback entry.
    /// </summary>
    /// <param name="checkpoint">
    /// The actual engine checkpoint enum, never inferred from callback order.
    /// </param>
    /// <param name="before">
    /// <see langword="true"/> records entry and starts its interval; <see langword="false"/> records callback return.
    /// </param>
    internal void Checkpoint(SessionFinalizationCheckpoint checkpoint, bool before)
    {
        if (before)
        {
            lock (_gate)
            {
                Emit();
                Array.Clear(_counts);
                _publication = _recovery = null;
                _phase = Enum.IsDefined(checkpoint) ? checkpoint.ToString() : "Unknown";
                _nextReport = TimeSpan.Zero;
            }
        }
        _report.ObserveFinalization(checkpoint, before);
    }

    /// <summary>
    /// Emits remaining observed counts at a normal completion boundary when capacity remains.
    /// </summary>
    internal void Complete()
    {
        lock (_gate) Emit();
    }

    /// <summary>
    /// Samples actual callbacks at most once per fifteen seconds within the current interval.
    /// </summary>
    private void EmitIfDue()
    {
        if (_reports >= 24) return;
        var elapsed = _elapsed();
        if (elapsed < _nextReport) return;
        Emit();
        _nextReport = elapsed + TimeSpan.FromSeconds(15);
    }

    /// <summary>
    /// Copies scalar lower-bound totals under the counter gate to the nonthrowing stage sink.
    /// </summary>
    private void Emit()
    {
        if (_reports >= 24 || !_counts.Any(value => value != 0)) return;
        _reports++;
        _report.ObserveResumeActivity(new(_phase, _counts[0], _counts[1], _counts[2], _counts[3],
            _counts[4], _counts[5], _counts[6], _publication, _recovery, _reports == 24));
    }
}

/// <summary>
/// Carries only scalar lower-bound callback observations for one named interval.
/// </summary>
/// <param name="Phase">
/// The last named callback entry, or the initial before-finalization interval.
/// </param>
/// <param name="CanonicalInitial">
/// Canonical initial-validation callbacks, not completed reads.
/// </param>
/// <param name="ExactInitial">
/// Exact physical initial-validation callbacks, separate from canonical reads.
/// </param>
/// <param name="CanonicalOpened">
/// Canonical write-lock opened callbacks, not completed transactions.
/// </param>
/// <param name="CanonicalContended">
/// Canonical write-lock contention callbacks.
/// </param>
/// <param name="MainContended">
/// Main-owner contention callbacks.
/// </param>
/// <param name="Publications">
/// Ordinary publication phase callbacks.
/// </param>
/// <param name="Recoveries">
/// Publication recovery phase callbacks.
/// </param>
/// <param name="LastPublication">
/// The last observed publication enum name, or <see langword="null"/> when none was observed.
/// </param>
/// <param name="LastRecovery">
/// The last observed recovery enum name, or <see langword="null"/> when none was observed.
/// </param>
/// <param name="Limited">
/// <see langword="true"/> marks the last available activity sample; subsequent activity is omitted.
/// </param>
internal sealed record JointResumeActivitySnapshot(string Phase, long CanonicalInitial, long ExactInitial, long CanonicalOpened, long CanonicalContended, long MainContended, long Publications, long Recoveries, string? LastPublication, string? LastRecovery, bool Limited);
