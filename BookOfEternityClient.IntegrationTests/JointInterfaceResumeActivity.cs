using BookOfEternityClient.Core;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Identifies the distinct existing hooks observed by a Windows resumer.
/// </summary>
internal enum JointResumeObservation { CanonicalInitial, ExactInitial, CanonicalOpened, CanonicalContended, MainContended }

/// <summary>
/// Observes bounded scalar activity without opening files or changing game results.
/// </summary>
internal sealed class JointInterfaceResumeActivity
{
    /// <summary>
    /// Creates a child-local observer with a monotonic clock seam for finite tests.
    /// </summary>
    /// <param name="report">
    /// The existing nonthrowing stage sink, retaining the child's independent identity.
    /// </param>
    /// <param name="elapsed">
    /// A monotonic elapsed clock; null uses Stopwatch without creating a timer.
    /// </param>
    internal JointInterfaceResumeActivity(JointInterfaceCrashStageReport report, Func<TimeSpan>? elapsed = null) { }

    /// <summary>
    /// Counts one existing hook invocation without asserting successful I/O.
    /// </summary>
    /// <param name="observation">
    /// The fixed hook population to increment.
    /// </param>
    internal void Observe(JointResumeObservation observation) { }

    /// <summary>
    /// Counts a publication or recovery observation without reading its journal.
    /// </summary>
    /// <param name="phase">
    /// The existing publication phase enum.
    /// </param>
    /// <param name="recovery">
    /// True selects recovery; false selects ordinary publication.
    /// </param>
    internal void Publication(TrustedLocalPublicationPhase phase, bool recovery) { }

    /// <summary>
    /// Names a real callback boundary and partitions activity at callback entry.
    /// </summary>
    /// <param name="checkpoint">
    /// The actual engine checkpoint enum, never inferred from callback order.
    /// </param>
    /// <param name="before">
    /// True records entry and starts its interval; false records callback return.
    /// </param>
    internal void Checkpoint(SessionFinalizationCheckpoint checkpoint, bool before) { }

    /// <summary>
    /// Emits remaining observed counts at a normal completion boundary when capacity remains.
    /// </summary>
    internal void Complete() { }
}
