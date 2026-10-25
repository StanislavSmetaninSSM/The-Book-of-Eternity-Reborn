using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Tests;

internal static partial class OwnedTerminalScenarioDriver
{
    /// <summary>
    /// Persists existing named-timer boundaries and primary fixture failures before bounded cleanup can end the driver.
    /// </summary>
    private sealed class NamedTerminalFixtureJournal : IDisposable
    {
        private readonly object _gate = new();
        private readonly StreamWriter _writer;
        private Exception? _failure;
        private long _open, _attempted, _stored;

        /// <summary>
        /// Opens the uncapped journal in the caller's independently owned external diagnostic folder.
        /// </summary>
        /// <param name="folder">The fresh native fixture's diagnostic folder.</param>
        /// <param name="mode">The exact existing bridge scenario being observed.</param>
        internal NamedTerminalFixtureJournal(string folder, string mode)
        {
            _writer = new StreamWriter(new FileStream(Path.Combine(folder, "named-timings.jsonl"),
                FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
            Record("fixture_initial", new { Mode = mode, Folder = folder, PID = Environment.ProcessId });
        }

        /// <summary>
        /// Creates the existing timer while restoring its thread-static receiver before any caller awaits.
        /// </summary>
        /// <param name="name">The literal operation or RPC phase name.</param>
        /// <returns>The original timing scope, independently disposable after the measured await.</returns>
        internal IDisposable? Measure(string name)
        {
            using var receiver = FileSystemManager.EnterOperationTimingContext(Observe);
            return FileSystemManager.MeasureCurrentOperation(name);
        }

        /// <summary>
        /// Writes original timer boundaries and counts outstanding scopes without implementing another clock.
        /// </summary>
        private void Observe(string name, string state, TimeSpan elapsed)
        {
            lock (_gate)
            {
                if (state == "begin") _open++;
                else if (state == "end") _open--;
                Write("named_timing", new { Boundary = "joint_operation_timing", Operation = name,
                    State = state, Milliseconds = elapsed.TotalMilliseconds });
            }
        }

        /// <summary>
        /// Flushes each observation immediately without replacing the observed operation's primary failure.
        /// </summary>
        /// <param name="kind">The diagnostic record kind.</param>
        /// <param name="value">The current observation, serialized before returning to the caller.</param>
        internal void Record(string kind, object value)
        {
            lock (_gate) Write(kind, value);
        }

        /// <summary>
        /// Records write failures while leaving the primary scenario and its cleanup responsible for their original outcome.
        /// </summary>
        private void Write(string kind, object value)
        {
            _attempted++;
            try { _writer.WriteLine(JsonSerializer.Serialize(new { Kind = kind, Value = value })); _stored++; }
            catch (Exception error) { _failure ??= error; }
        }

        /// <summary>
        /// Marks only a normally finished journal complete; lost diagnostic writes cannot qualify as complete.
        /// </summary>
        internal void Complete()
        {
            lock (_gate)
            {
                Write("fixture_journal_complete", new { Complete = _failure is null && _open == 0 && _attempted == _stored,
                    OpenScopes = _open, AttemptedBefore = _attempted, StoredBefore = _stored });
                if (_failure is not null || _open != 0 || _attempted != _stored)
                    throw new IOException("Terminal fixture diagnostic journal is incomplete.", _failure);
            }
        }

        /// <summary>
        /// Closes the physical writer after all normal cleanup and completion checks.
        /// </summary>
        public void Dispose() { lock (_gate) _writer.Dispose(); }
    }
}
