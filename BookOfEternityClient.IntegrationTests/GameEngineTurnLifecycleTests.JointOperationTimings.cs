using System.Diagnostics;
using System.Text.Json;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Serializes a bounded stream of observed operation boundaries to diagnostic output.
    /// An end boundary records elapsed time without implying successful completion.
    /// </summary>
    private sealed class JointOperationTimings
    {
        private const int RecordLimit = 1024;
        private readonly object _gate = new();
        private readonly long _started = Stopwatch.GetTimestamp();
        private int _recordCount;
        private bool _limited;

        /// <summary>
        /// Writes the observed boundary, or a single limit marker after the record budget is exhausted.
        /// </summary>
        /// <param name="name">
        /// Literal name of the operation section being observed.
        /// </param>
        /// <param name="boundary">
        /// Observed begin or end boundary; an interrupted operation can have only a begin record.
        /// </param>
        /// <param name="elapsed">
        /// Inclusive monotonic duration supplied by the operation, or zero at its begin boundary.
        /// </param>
        public void Observe(string name, string boundary, TimeSpan elapsed)
        {
            lock (_gate)
            {
                if (_limited) return;
                if (_recordCount == RecordLimit)
                {
                    _limited = true;
                    Console.WriteLine(JsonSerializer.Serialize(new
                    {
                        Boundary = "joint_operation_timing",
                        Operation = "output-limit",
                        State = "limited",
                        Milliseconds = 0d,
                        Sequence = RecordLimit + 1,
                        PID = Environment.ProcessId,
                        TotalElapsedMilliseconds = Stopwatch.GetElapsedTime(_started).TotalMilliseconds,
                        Limit = RecordLimit
                    }));
                    return;
                }

                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    Boundary = "joint_operation_timing",
                    Operation = name,
                    State = boundary,
                    Milliseconds = elapsed.TotalMilliseconds,
                    Sequence = ++_recordCount,
                    PID = Environment.ProcessId,
                    TotalElapsedMilliseconds = Stopwatch.GetElapsedTime(_started).TotalMilliseconds
                }));
            }
        }
    }
}
