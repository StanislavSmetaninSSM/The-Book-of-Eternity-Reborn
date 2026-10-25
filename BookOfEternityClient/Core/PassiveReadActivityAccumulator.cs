using System.Diagnostics;

namespace BookOfEternityClient.Core;

/// <summary>
/// Separates the original canonical and exact-physical initial-validation observations.
/// </summary>
internal enum PassiveReadActivityKind
{
    /// <summary>
    /// Records the canonical reader's original observation boundary.
    /// </summary>
    CanonicalInitial,
    /// <summary>
    /// Records the exact-physical reader's original observation boundary.
    /// </summary>
    ExactInitial
}

/// <summary>
/// Carries one observed read population and its original monotonic recording time.
/// </summary>
/// <param name="Kind">
/// Original read population; this event does not assert successful completion.
/// </param>
/// <param name="Timestamp">
/// Monotonic <see cref="Stopwatch.GetTimestamp"/> value recorded under the accumulator gate.
/// </param>
internal readonly record struct PassiveReadActivityEvent(PassiveReadActivityKind Kind, long Timestamp);

/// <summary>
/// Buffers every scalar read event until a detached drain, without storage authority, callbacks or quotas.
/// </summary>
internal sealed class PassiveReadActivityAccumulator
{
    private readonly object _gate = new();
    private readonly List<PassiveReadActivityEvent> _events = [];
    private long _lastTimestamp = long.MinValue;

    /// <summary>
    /// Records every event without invoking delegates, performing I/O or applying a count quota.
    /// </summary>
    /// <param name="kind">
    /// Canonical or exact-physical initial validation; undefined values are rejected.
    /// </param>
    internal void Record(PassiveReadActivityKind kind)
    {
        lock (_gate) Append(kind, Stopwatch.GetTimestamp());
    }

    /// <summary>
    /// Supplies deterministic timestamp DATA to the same append path for finite diagnostic fixtures.
    /// </summary>
    /// <param name="kind">
    /// Defined original read population.
    /// </param>
    /// <param name="timestamp">
    /// Monotonic stopwatch ticks, at least the preceding event timestamp; backwards values are rejected.
    /// </param>
    internal void RecordForTests(PassiveReadActivityKind kind, long timestamp)
    {
        lock (_gate) Append(kind, timestamp);
    }

    /// <summary>
    /// Appends one validated scalar event while its caller retains the recording gate.
    /// </summary>
    /// <param name="kind">
    /// Defined original read population.
    /// </param>
    /// <param name="timestamp">
    /// Nondecreasing original observation time.
    /// </param>
    private void Append(PassiveReadActivityKind kind, long timestamp)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (timestamp < _lastTimestamp) throw new ArgumentOutOfRangeException(nameof(timestamp));
        _events.Add(new(kind, timestamp));
        _lastTimestamp = timestamp;
    }

    /// <summary>
    /// Atomically detaches all pending events so replay and output run outside the recording gate.
    /// </summary>
    /// <returns>
    /// Every event recorded before this drain's ordering point, in recording order, exactly once.
    /// Later events remain pending; abrupt termination before a drain can lose the pending tail.
    /// </returns>
    internal PassiveReadActivityEvent[] Drain()
    {
        lock (_gate)
        {
            var result = _events.ToArray();
            _events.Clear();
            return result;
        }
    }
}
