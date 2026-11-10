using BookOfEternityClient.Core;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Accounts for every attempted profile timing write separately from validator outcomes.
/// Deferred diagnostic failures are merged only after the validator's primary outcome is known.
/// </summary>
internal sealed class JointCleanupProfileDiagnosticCapture
{
    private readonly GameEngineTurnLifecycleTests.JointOperationTimings _timings;
    private readonly object _gate = new();
    private readonly TimeProvider _clock;
    private readonly TextWriter _writer;
    private long _flushAttempted, _flushStored;
    private TimeSpan _flushElapsed, _flushGateWait;
    private readonly Dictionary<(string Name, string State), WriteCost> _writeCosts = new();
    private readonly List<(Exception Error, string Stage)> _failures = new();
    private long _attempted;
    private long _stored;

    /// <summary>
    /// Selects the uncapped JSONL destination without taking ownership of its lifetime.
    /// </summary>
    /// <param name="writer">
    /// The profile's external timing writer, kept open through owner close and cleanup.
    /// </param>
    /// <param name="timeProvider">
    /// Monotonic diagnostic clock, or <see langword="null"/> for <see cref="TimeProvider.System"/>.
    /// </param>
    internal JointCleanupProfileDiagnosticCapture(TextWriter writer, TimeProvider? timeProvider = null)
    {
        _writer = writer;
        _timings = new(writer);
        _clock = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Returns a detached ledger of every named timing-write attempt.
    /// </summary>
    /// <returns>
    /// Per-operation and boundary write costs, including failed and unmeasured attempts.
    /// </returns>
    internal WriteCost[] GetWriteCosts()
    {
        lock (_gate) return _writeCosts.Values.OrderBy(row => row.Operation, StringComparer.Ordinal)
            .ThenBy(row => row.State, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Describes the measured serializer and synchronous writer span for a literal timer boundary.
    /// </summary>
    /// <param name="Operation">
    /// Literal operation name supplied by the production hook.
    /// </param>
    /// <param name="State">
    /// Literal begin or end boundary supplied by that hook.
    /// </param>
    /// <param name="Attempted">
    /// Every attempted timing write for this name and boundary.
    /// </param>
    /// <param name="Stored">
    /// Writes whose serializer and writer returned successfully.
    /// </param>
    /// <param name="Measured">
    /// Attempts with both monotonic timestamps and a computed duration, including failed writes.
    /// </param>
    /// <param name="SerializationAndWriteElapsed">
    /// Summed elapsed serializer, inner lock and synchronous writer time; excludes outer gate wait and accounting.
    /// </param>
    internal readonly record struct WriteCost(string Operation, string State, long Attempted, long Stored,
        long Measured, TimeSpan SerializationAndWriteElapsed);

    /// <summary>
    /// Gets the number of timing boundaries offered to the observer, without a quota.
    /// </summary>
    internal long Attempted { get { lock (_gate) return _attempted; } }

    /// <summary>
    /// Gets the number of timing writes that returned successfully.
    /// </summary>
    internal long Stored { get { lock (_gate) return _stored; } }

    /// <summary>
    /// Gets whether every offered timing boundary was written and no deferred diagnostic error exists.
    /// Successful writer closing and exact artifact hashing are checked separately by the profile.
    /// </summary>
    internal bool Complete { get { lock (_gate) return _attempted == _stored && _failures.Count == 0; } }

    /// <summary>
    /// Observes a real timer boundary using the existing serializer and sequence numbering.
    /// Measures each serializer/write attempt while deferring storage and clock failures.
    /// </summary>
    /// <param name="name">
    /// The literal operation name received from the production hook.
    /// </param>
    /// <param name="boundary">
    /// The actual begin or end boundary received from that hook.
    /// </param>
    /// <param name="elapsed">
    /// The production timer's elapsed interval, or zero at begin.
    /// </param>
    internal void Observe(string name, string boundary, TimeSpan elapsed)
        => ObserveCore(name, boundary, elapsed, null);

    /// <summary>
    /// Accounts for full original passive event transport without reconstructing events from aggregates.
    /// </summary>
    /// <param name="recorded">One detached source-clock event.</param>
    internal void ObserveRecorded(OperationTimingAccumulator.NamedEvent recorded)
        => ObserveCore(recorded.Name, recorded.State, TimeSpan.Zero, recorded);

    /// <summary>Flushes the same synchronized writer without a callback or separate transport.</summary>
    internal void Flush()
    {
        var requested = System.Diagnostics.Stopwatch.GetTimestamp();
        lock (_gate)
        {
            _flushGateWait += System.Diagnostics.Stopwatch.GetElapsedTime(requested);
            _flushAttempted++;
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            try { _writer.Flush(); _flushStored++; }
            catch (Exception error) { _failures.Add((error, "timing-flush")); }
            finally { _flushElapsed += System.Diagnostics.Stopwatch.GetElapsedTime(started); }
        }
    }

    /// <summary>Copies explicit flush attempts, successful returns, actual flush cost and separate gate wait.</summary>
    internal (long Attempted, long Stored, TimeSpan Elapsed, TimeSpan GateWait) GetFlushCosts()
    {
        lock (_gate) return (_flushAttempted, _flushStored, _flushElapsed, _flushGateWait);
    }

    private void ObserveCore(string name, string boundary, TimeSpan elapsed,
        OperationTimingAccumulator.NamedEvent? recorded)
    {
        lock (_gate)
        {
            _attempted++;
            long? started = null, ended = null;
            Exception? startFailure = null, endFailure = null;
            var stored = false;
            try { started = _clock.GetTimestamp(); }
            catch (Exception error) { startFailure = error; }
            try
            {
                try
                {
                    if (recorded is { } original) _timings.ObserveRecorded(original);
                    else _timings.Observe(name, boundary, elapsed);
                }
                finally
                {
                    try { ended = _clock.GetTimestamp(); }
                    catch (Exception error) { endFailure = error; }
                }
                _stored++;
                stored = true;
            }
            catch (Exception error)
            {
                // Retain the original writer failure before secondary measurement errors.
                _failures.Add((error, "timing-observer:" + name + ":" + boundary));
            }
            if (startFailure is not null)
                _failures.Add((startFailure, "timing-write-clock-start:" + name + ":" + boundary));
            if (endFailure is not null)
                _failures.Add((endFailure, "timing-write-clock-end:" + name + ":" + boundary));
            var measured = false;
            var writeElapsed = TimeSpan.Zero;
            if (started is { } start && ended is { } end)
            {
                try { writeElapsed = _clock.GetElapsedTime(start, end); measured = true; }
                catch (Exception error) { _failures.Add((error, "timing-write-clock-elapsed:" + name + ":" + boundary)); }
            }
            try
            {
                _writeCosts.TryGetValue((name, boundary), out var previous);
                _writeCosts[(name, boundary)] = new WriteCost(name, boundary, previous.Attempted + 1,
                    previous.Stored + (stored ? 1 : 0), previous.Measured + (measured ? 1 : 0),
                    previous.SerializationAndWriteElapsed + writeElapsed);
            }
            catch (Exception error) { _failures.Add((error, "timing-write-accounting:" + name + ":" + boundary)); }
        }
    }

    /// <summary>
    /// Records a diagnostic error for later durable reporting without replacing a validator failure.
    /// </summary>
    /// <param name="error">
    /// The original storage or component-report exception.
    /// </param>
    /// <param name="stage">
    /// The exact diagnostic stage retained with this error.
    /// </param>
    internal void RecordDiagnosticFailure(Exception error, string stage)
    {
        lock (_gate) _failures.Add((error, stage));
    }

    /// <summary>
    /// Returns every deferred diagnostic failure without removing or limiting observations.
    /// </summary>
    /// <returns>
    /// A snapshot of all original exception objects and their diagnostic stage labels.
    /// </returns>
    internal (Exception Error, string Stage)[] GetFailures()
    {
        lock (_gate) return _failures.ToArray();
    }
}
