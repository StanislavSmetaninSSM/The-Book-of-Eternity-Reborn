using System.Diagnostics;

namespace BookOfEternityClient.Core;

/// <summary>
/// Accumulates named durations without callbacks, filesystem access or authority decisions.
/// </summary>
internal sealed class OperationTimingAccumulator
{
    private Window? _active;

    /// <summary>
    /// Opens an exclusive measurement window without changing any game operation.
    /// </summary>
    /// <returns>
    /// The window to close and inspect; overlapping windows on this recorder are refused.
    /// </returns>
    internal Window StartWindow() => StartWindow(captureEvents: false);

    /// <summary>
    /// Opens an exclusive window with optional original scalar event capture.
    /// </summary>
    /// <param name="captureEvents">
    /// Whether original begin and end boundaries are requested in addition to aggregates.
    /// </param>
    /// <returns>
    /// The window to close, drain and inspect.
    /// </returns>
    internal Window StartWindow(bool captureEvents)
    {
        var window = new Window(this, captureEvents);
        if (Interlocked.CompareExchange(ref _active, window, null) is not null)
            throw new InvalidOperationException("This recorder already has an active window.");
        return window;
    }

    /// <summary>
    /// Starts one named scope in the currently active window.
    /// </summary>
    /// <param name="name">
    /// The nonempty literal section name; no limit is imposed on distinct names or invocations.
    /// </param>
    /// <returns>
    /// An idempotent scope, or <see langword="null"/> when no window is active.
    /// </returns>
    internal IDisposable? Measure(string name) => Volatile.Read(ref _active)?.Begin(name);

    /// <summary>
    /// Owns independent counters and explicitly records scopes completing after its close.
    /// </summary>
    internal sealed class Window : IDisposable
    {
        private readonly OperationTimingAccumulator _owner;
        private readonly object _gate = new();
        private readonly Dictionary<string, Counter> _rows = new(StringComparer.Ordinal);
        private readonly long _started = Stopwatch.GetTimestamp();
        private readonly bool _captureEvents;
        private Queue<NamedEvent>? _events;
        private long _nextScopeId, _eventAttempted, _eventEnqueued, _eventDrained, _eventFailed;
        private long _stopped, _open, _late;
        private bool _closed;

        /// <summary>
        /// Creates a window whose counters never contain game data or caller delegates.
        /// </summary>
        /// <param name="owner">
        /// The recorder whose active slot this window releases on close.
        /// </param>
        internal Window(OperationTimingAccumulator owner) : this(owner, captureEvents: false) { }

        /// <summary>
        /// Creates a window with optional original scalar events and no eagerly allocated event buffer.
        /// </summary>
        /// <param name="owner">The recorder whose active slot this window releases on close.</param>
        /// <param name="captureEvents">Whether to retain each original named boundary.</param>
        internal Window(OperationTimingAccumulator owner, bool captureEvents)
        {
            _owner = owner;
            _captureEvents = captureEvents;
        }

        /// <summary>
        /// Registers a scope only while this window remains open.
        /// </summary>
        /// <param name="name">
        /// The nonempty literal section name.
        /// </param>
        /// <returns>
        /// The registered scope, or <see langword="null"/> after window close.
        /// </returns>
        internal IDisposable? Begin(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            lock (_gate)
            {
                if (_closed) return null;
                if (!_rows.TryGetValue(name, out var counter)) _rows.Add(name, counter = new());
                counter.Started++;
                _open++;
                return new Scope(this, counter, name, _captureEvents ? ++_nextScopeId : 0);
            }
        }

        /// <summary>
        /// Stops new registrations once; already registered scopes retain their own window.
        /// </summary>
        public void Dispose()
        {
            lock (_gate)
            {
                if (_closed) return;
                _stopped = Stopwatch.GetTimestamp();
                _closed = true;
                Interlocked.CompareExchange(ref _owner._active, null, this);
            }
        }

        /// <summary>
        /// Copies every recorded name, including incomplete and late completions.
        /// </summary>
        /// <returns>
        /// Detached counters; Complete requires a closed window with no open or late scopes.
        /// Inclusive scopes may overlap and must not be added as independent costs.
        /// </returns>
        internal Snapshot Capture()
        {
            lock (_gate)
                return new(_closed, _open, _late, _closed && _open == 0 && _late == 0,
                    ((_closed ? _stopped : Stopwatch.GetTimestamp()) - _started) * 1000d / Stopwatch.Frequency,
                    _rows.OrderBy(row => row.Key, StringComparer.Ordinal).Select(row => new Row(row.Key,
                        row.Value.Started, row.Value.Completed, row.Value.Ticks * 1000d / Stopwatch.Frequency,
                        row.Value.MaxTicks * 1000d / Stopwatch.Frequency)).ToArray());
        }

        /// <summary>
        /// Atomically removes a detached FIFO batch of original named events.
        /// </summary>
        /// <returns>
        /// Every event pending at the drain boundary, or an empty array when capture is disabled.
        /// </returns>
        /// <exception cref="OutOfMemoryException">
        /// The detached batch could not be allocated; all pending events and accounting remain intact.
        /// </exception>
        internal NamedEvent[] DrainEvents()
        {
            lock (_gate)
            {
                if (_events is null || _events.Count == 0) return Array.Empty<NamedEvent>();
                var batch = _events.ToArray();
                _events.Clear();
                _eventDrained += batch.LongLength;
                return batch;
            }
        }

        /// <summary>
        /// Copies event capture accounting independently of aggregate completeness.
        /// </summary>
        /// <returns>
        /// Capture selection and counts; failed capture cannot qualify a complete diagnostic trace.
        /// </returns>
        internal EventStatus CaptureEventStatus()
        {
            lock (_gate)
                return new(_captureEvents, _eventAttempted, _eventEnqueued, _eventDrained,
                    _eventEnqueued - _eventDrained, _eventFailed);
        }

        /// <summary>
        /// Retains an original scalar boundary under the existing window gate without external code.
        /// Buffer allocation failure remains explicit while leaving the measured operation unchanged.
        /// </summary>
        /// <param name="scopeId">The identity pairing this scope's two original boundaries.</param>
        /// <param name="name">The unchanged section name.</param>
        /// <param name="state">The original begin or end boundary.</param>
        /// <param name="timestamp">The original absolute Stopwatch timestamp.</param>
        /// <param name="ticks">Zero at begin, or the original inclusive duration at end.</param>
        /// <param name="late">Whether the boundary registered after window close.</param>
        private void RecordEvent(long scopeId, string name, string state, long timestamp, long ticks, bool late)
        {
            if (!_captureEvents) return;
            var sequence = ++_eventAttempted;
            try
            {
                _events ??= new Queue<NamedEvent>();
                _events.Enqueue(new(sequence, scopeId, name, state, timestamp, ticks, late));
                _eventEnqueued++;
            }
            catch (OutOfMemoryException)
            {
                _eventFailed++;
            }
        }

        /// <summary>
        /// Completes an already registered scope without calling external code.
        /// </summary>
        /// <param name="counter">
        /// The exact counter selected at registration.
        /// </param>
        /// <param name="name">The unchanged original section name.</param>
        /// <param name="scopeId">The identity pairing this scope's original boundaries.</param>
        /// <param name="timestamp">The absolute end timestamp captured before acquiring the gate.</param>
        /// <param name="ticks">
        /// Monotonic inclusive duration captured before updating aggregate state.
        /// </param>
        private void End(Counter counter, string name, long scopeId, long timestamp, long ticks)
        {
            lock (_gate)
            {
                counter.Completed++;
                counter.Ticks += ticks;
                counter.MaxTicks = Math.Max(counter.MaxTicks, ticks);
                _open--;
                if (_closed) _late++;
                RecordEvent(scopeId, name, "end", timestamp, ticks, _closed);
            }
        }

        /// <summary>
        /// Holds one name's values under its window's monitor.
        /// </summary>
        private sealed class Counter
        {
            internal long Started, Completed, Ticks, MaxTicks;
        }

        /// <summary>
        /// Completes its original window's counter once, even after that window closes.
        /// </summary>
        private sealed class Scope : IDisposable
        {
            private readonly Window _window;
            private readonly Counter _counter;
            private readonly string _name;
            private readonly long _scopeId, _started;
            private int _closed;

            /// <summary>
            /// Captures the original start clock while registering its optional begin under the window gate.
            /// </summary>
            /// <param name="window">The window which registered this scope.</param>
            /// <param name="counter">The registered name's aggregate counter.</param>
            /// <param name="name">The unchanged section name retained for its original end.</param>
            /// <param name="scopeId">The window-local identity pairing its original boundaries.</param>
            internal Scope(Window window, Counter counter, string name, long scopeId)
            {
                _window = window;
                _counter = counter;
                _name = name;
                _scopeId = scopeId;
                _started = Stopwatch.GetTimestamp();
                window.RecordEvent(scopeId, name, "begin", _started, 0, late: false);
            }

            /// <summary>
            /// Captures duration once and retains any late completion in the original window.
            /// </summary>
            public void Dispose()
            {
                if (Interlocked.Exchange(ref _closed, 1) != 0) return;
                var timestamp = Stopwatch.GetTimestamp();
                _window.End(_counter, _name, _scopeId, timestamp, timestamp - _started);
            }
        }
    }

    /// <summary>
    /// Holds one original named boundary without callbacks or authority-bearing objects.
    /// </summary>
    /// <param name="Sequence">Window-local registration order, independent of clock ordering.</param>
    /// <param name="ScopeId">Window-local identity pairing a scope's begin and end.</param>
    /// <param name="Name">The unchanged original section name.</param>
    /// <param name="State">The original boundary, either begin or end.</param>
    /// <param name="TimestampTicks">Absolute Stopwatch timestamp captured at the boundary.</param>
    /// <param name="ElapsedTicks">Zero at begin; original inclusive Stopwatch ticks at end.</param>
    /// <param name="Late">Whether this boundary registered after its original window closed.</param>
    internal readonly record struct NamedEvent(long Sequence, long ScopeId, string Name, string State,
        long TimestampTicks, long ElapsedTicks, bool Late);

    /// <summary>
    /// Describes original event capture without exposing its mutable buffer.
    /// </summary>
    /// <param name="Enabled">Whether original event capture was requested.</param>
    /// <param name="Attempted">Number of original event enqueue attempts.</param>
    /// <param name="Enqueued">Number of successfully retained original events.</param>
    /// <param name="Drained">Number of original events transferred to detached batches.</param>
    /// <param name="Pending">Successfully retained original events not yet drained.</param>
    /// <param name="Failed">Original event enqueue attempts that could not be retained.</param>
    internal readonly record struct EventStatus(bool Enabled, long Attempted, long Enqueued,
        long Drained, long Pending, long Failed);

    /// <summary>
    /// Contains detached aggregate data for one literal section name.
    /// </summary>
    /// <param name="Name">
    /// Exact section name.
    /// </param>
    /// <param name="Started">
    /// Number of registered scopes.
    /// </param>
    /// <param name="Completed">
    /// Number of completed scopes, including late completions.
    /// </param>
    /// <param name="InclusiveMilliseconds">
    /// Sum of completed inclusive durations; nested scopes overlap.
    /// </param>
    /// <param name="MaxMilliseconds">
    /// Largest completed duration, or zero when none completed.
    /// </param>
    internal sealed record Row(string Name, long Started, long Completed,
        double InclusiveMilliseconds, double MaxMilliseconds);

    /// <summary>
    /// Describes one window without retaining its mutable counters.
    /// </summary>
    /// <param name="Closed">
    /// Whether registrations have stopped.
    /// </param>
    /// <param name="OpenScopes">
    /// Registered scopes that have not completed.
    /// </param>
    /// <param name="LateCompletions">
    /// Completions after window close, which make an exact window interpretation invalid.
    /// </param>
    /// <param name="Complete">
    /// Whether the window closed without open or late scopes.
    /// </param>
    /// <param name="ElapsedMilliseconds">
    /// Monotonic window wall time, including profiler overhead.
    /// </param>
    /// <param name="Rows">
    /// Every named aggregate, with no name, invocation or byte quota.
    /// </param>
    internal sealed record Snapshot(bool Closed, long OpenScopes, long LateCompletions,
        bool Complete, double ElapsedMilliseconds, Row[] Rows);
}
