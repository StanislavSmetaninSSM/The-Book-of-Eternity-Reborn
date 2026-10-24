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
    internal Window StartWindow()
    {
        var window = new Window(this);
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
        private long _stopped, _open, _late;
        private bool _closed;

        /// <summary>
        /// Creates a window whose counters never contain game data or caller delegates.
        /// </summary>
        /// <param name="owner">
        /// The recorder whose active slot this window releases on close.
        /// </param>
        internal Window(OperationTimingAccumulator owner) => _owner = owner;

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
                return new Scope(this, counter);
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
        /// Completes an already registered scope without calling external code.
        /// </summary>
        /// <param name="counter">
        /// The exact counter selected at registration.
        /// </param>
        /// <param name="ticks">
        /// Monotonic inclusive duration captured before updating aggregate state.
        /// </param>
        private void End(Counter counter, long ticks)
        {
            lock (_gate)
            {
                counter.Completed++;
                counter.Ticks += ticks;
                counter.MaxTicks = Math.Max(counter.MaxTicks, ticks);
                _open--;
                if (_closed) _late++;
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
        /// <param name="window">
        /// The window which registered this scope.
        /// </param>
        /// <param name="counter">
        /// The registered name's counter.
        /// </param>
        private sealed class Scope(Window window, Counter counter) : IDisposable
        {
            private readonly long _started = Stopwatch.GetTimestamp();
            private int _closed;

            /// <summary>
            /// Captures duration once and retains any late completion in the original window.
            /// </summary>
            public void Dispose()
            {
                if (Interlocked.Exchange(ref _closed, 1) == 0)
                    window.End(counter, Stopwatch.GetTimestamp() - _started);
            }
        }
    }

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
