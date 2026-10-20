using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Serializes every observed operation boundary to diagnostic output in sequence order.
    /// An end boundary records elapsed time without implying successful completion.
    /// </summary>
    private sealed class JointOperationTimings
    {
        private readonly object _gate = new();
        private readonly long _started = Stopwatch.GetTimestamp();
        private readonly TextWriter _output;
        private long _sequence;

        /// <summary>
        /// Selects the writer receiving serialized operation timing records.
        /// </summary>
        /// <param name="output">
        /// Diagnostic output writer, or <see langword="null"/> to use <see cref="Console.Out"/>.
        /// </param>
        public JointOperationTimings(TextWriter? output = null)
        {
            _output = output ?? Console.Out;
        }

        /// <summary>
        /// Writes the observed boundary with its duration and the next sequence number.
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
                _output.WriteLine(JsonSerializer.Serialize(new
                {
                    Boundary = "joint_operation_timing",
                    Operation = name,
                    State = boundary,
                    Milliseconds = elapsed.TotalMilliseconds,
                    Sequence = ++_sequence,
                    PID = Environment.ProcessId,
                    TotalElapsedMilliseconds = Stopwatch.GetElapsedTime(_started).TotalMilliseconds
                }));
            }
        }
    }
}
