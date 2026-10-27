namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    [ThreadStatic]
    private static OperationTimingContext? _operationTimingContext;

    /// <summary>
    /// Selects this manager's passive receiver for nested synchronous computations on the current thread.
    /// </summary>
    /// <returns>
    /// A restoring scope when observation context changes; otherwise, <see langword="null"/>.
    /// The scope must remain within synchronous work and must not span an await.
    /// </returns>
    internal IDisposable? EnterOperationTimingContext() =>
        EnterOperationTimingContext(_hooks?.OperationTimingObserver, _hooks?.PassiveOperationTimings);

    /// <summary>
    /// Selects a passive receiver for synchronous computations without constructing a physical filesystem host.
    /// </summary>
    /// <param name="observer">
    /// The receiver to select; <see langword="null"/> temporarily suppresses an enclosing receiver.
    /// </param>
    /// <returns>
    /// A restoring scope when the receiver context changes; otherwise <see langword="null"/>.
    /// The scope must remain on its entering thread and must not span an await.
    /// </returns>
    internal static IDisposable? EnterOperationTimingContext(Action<string, string, TimeSpan>? observer)
        => EnterOperationTimingContext(observer, null);

    /// <summary>
    /// Selects synchronous diagnostic destinations without changing observation eligibility.
    /// </summary>
    /// <param name="observer">
    /// The existing arbitrary callback, or <see langword="null"/> when absent.
    /// </param>
    /// <param name="recorder">
    /// The sealed passive recorder, or <see langword="null"/> to suppress enclosing accumulation.
    /// </param>
    /// <returns>
    /// A restoring scope confined to the entering synchronous thread, or <see langword="null"/> when unnecessary.
    /// </returns>
    internal static IDisposable? EnterOperationTimingContext(Action<string, string, TimeSpan>? observer,
        OperationTimingAccumulator? recorder)
    {
        if (observer is null && recorder is null && _operationTimingContext is null) return null;
        var context = new OperationTimingContext(_operationTimingContext, observer, recorder);
        _operationTimingContext = context;
        return context;
    }

    /// <summary>
    /// Observes synchronous work through the selected callback or active sealed accumulator.
    /// </summary>
    /// <param name="name">
    /// The literal operation name emitted without changing the computation's authority.
    /// </param>
    /// <returns>
    /// An idempotent timer for a callback or active passive window; otherwise, <see langword="null"/>.
    /// </returns>
    internal static IDisposable? MeasureCurrentOperation(string name) =>
        _operationTimingContext?.Observer is { } observer ? new OperationTimer(observer, name) :
        _operationTimingContext?.Recorder?.Measure(name);

    /// <summary>Measures only through the existing passive recorder, never invoking an arbitrary observer.</summary>
    /// <param name="name">The literal passive operation name.</param>
    /// <returns>The existing passive scope, or null when an observer takes precedence or no recorder is active.</returns>
    internal static IDisposable? MeasureCurrentPassiveOperation(string name) =>
        _operationTimingContext?.Observer is not null ? null : _operationTimingContext?.Recorder?.Measure(name);

    /// <summary>
    /// Restores the preceding synchronous receiver without flowing observation to another thread.
    /// </summary>
    private sealed class OperationTimingContext : IDisposable
    {
        private readonly OperationTimingContext? _previous;
        private int _closed;

        /// <summary>
        /// Gets the selected receiver, or <see langword="null"/> to suppress enclosing observation.
        /// </summary>
        internal Action<string, string, TimeSpan>? Observer { get; }

        /// <summary>
        /// Gets the selected sealed recorder without treating it as an arbitrary callback.
        /// </summary>
        internal OperationTimingAccumulator? Recorder { get; }

        /// <summary>
        /// Retains the preceding scope and this manager's passive receiver.
        /// </summary>
        /// <param name="previous">
        /// The preceding scope on this thread, or <see langword="null"/> when none is active.
        /// </param>
        /// <param name="observer">
        /// The selected passive receiver, or <see langword="null"/> to suppress observation.
        /// </param>
        /// <param name="recorder">
        /// The selected sealed recorder, or <see langword="null"/> to suppress enclosing accumulation.
        /// </param>
        internal OperationTimingContext(OperationTimingContext? previous, Action<string, string, TimeSpan>? observer,
            OperationTimingAccumulator? recorder)
        {
            _previous = previous;
            Observer = observer;
            Recorder = recorder;
        }

        /// <summary>
        /// Closes this scope once and restores the nearest enclosing scope that is still open.
        /// The scope must be disposed on the synchronous thread on which it was entered.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0 || !ReferenceEquals(_operationTimingContext, this)) return;
            var previous = _previous;
            while (previous is not null && previous._closed != 0) previous = previous._previous;
            _operationTimingContext = previous;
        }
    }
}
