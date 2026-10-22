namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    /// <summary>
    /// Observes one named operation without changing its authority or execution flow.
    /// </summary>
    /// <param name="name">
    /// The literal method or section name recorded by this caller.
    /// </param>
    /// <returns>
    /// An idempotent timing scope when observation is enabled; otherwise, <see langword="null"/>.
    /// </returns>
    internal IDisposable? MeasureOperation(string name) =>
        _hooks?.OperationTimingObserver is { } observer ? new OperationTimer(observer, name) : null;

    /// <summary>
    /// Emits diagnostic boundaries without transferring authority or replacing operation failures.
    /// </summary>
    private sealed class OperationTimer : IDisposable
    {
        private readonly Action<string, string, TimeSpan> _observer;
        private readonly string _name;
        private readonly long _started;
        private int _closed;

        /// <summary>
        /// Records begin before starting the measured interval.
        /// </summary>
        /// <param name="observer">
        /// Passive diagnostic receiver; its exceptions are ignored.
        /// </param>
        /// <param name="name">
        /// The literal operation name retained unchanged in both boundaries.
        /// </param>
        internal OperationTimer(Action<string, string, TimeSpan> observer, string name)
        {
            _observer = observer;
            _name = name;
            Notify("begin", TimeSpan.Zero);
            _started = System.Diagnostics.Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Records inclusive elapsed time once, including exception unwinding when used in a using scope.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0) return;
            Notify("end", System.Diagnostics.Stopwatch.GetElapsedTime(_started));
        }

        /// <summary>
        /// Isolates observer failures from the operation being measured.
        /// </summary>
        /// <param name="boundary">
        /// The begin or end boundary name.
        /// </param>
        /// <param name="elapsed">
        /// Zero at begin, or monotonic elapsed time captured before notifying the end.
        /// </param>
        private void Notify(string boundary, TimeSpan elapsed)
        {
            using var diagnostic = SuppressValidationReuse();
            try { _observer(_name, boundary, elapsed); }
            catch (Exception) { /* Optional observation must not change the measured operation. */ }
        }
    }
}
