using System.Diagnostics;
using System.IO;
using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Serializes every observed operation boundary to diagnostic output in sequence order.
    /// An end boundary records elapsed time without implying successful completion.
    /// </summary>
    internal sealed class JointOperationTimings
    {
        private readonly object _gate = new();
        private readonly long _started = Stopwatch.GetTimestamp();
        private readonly TextWriter _output;
        private long _sequence;
        private long _completedObservations;
        private long _gateWaitTicks;
        private long _buildAndSerializeTicks;
        private long _writeTicks;

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
        /// Includes cumulative costs of completed prior observations so interrupted output retains observer accounting.
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
            => ObserveCore(name, boundary, elapsed.TotalMilliseconds, null);

        /// <summary>
        /// Persists an original detached passive event with its source clock rather than replay time.
        /// </summary>
        /// <param name="recorded">The scalar event produced by the sealed recorder.</param>
        internal void ObserveRecorded(OperationTimingAccumulator.NamedEvent recorded)
        {
            if (recorded.Sequence <= 0 || recorded.ScopeId <= 0 || recorded.TimestampTicks < _started ||
                recorded.ElapsedTicks < 0 || recorded.State is not ("begin" or "end") ||
                recorded.State == "begin" && recorded.ElapsedTicks != 0)
                throw new InvalidDataException("Invalid original passive timing event.");
            ArgumentException.ThrowIfNullOrEmpty(recorded.Name);
            ObserveCore(recorded.Name, recorded.State, recorded.ElapsedTicks * 1000d / Stopwatch.Frequency, recorded);
        }

        /// <summary>
        /// Serializes one real boundary; deferred transport costs remain distinct from its original clock.
        /// </summary>
        private void ObserveCore(string name, string boundary, double milliseconds,
            OperationTimingAccumulator.NamedEvent? recorded)
        {
            var requested = Stopwatch.GetTimestamp();
            lock (_gate)
            {
                var entered = Stopwatch.GetTimestamp();
                var buildStarted = Stopwatch.GetTimestamp();
                object payload = recorded is { } original ? new
                {
                    Boundary = "joint_operation_timing", Operation = name, State = boundary,
                    Milliseconds = milliseconds, Sequence = ++_sequence, PID = Environment.ProcessId,
                    TotalElapsedMilliseconds = Stopwatch.GetElapsedTime(_started, original.TimestampTicks).TotalMilliseconds,
                    Transport = "sealed-passive-events-v1", PassiveSequence = original.Sequence,
                    original.ScopeId, original.TimestampTicks, original.ElapsedTicks, original.Late,
                    ObserverCompletedBefore = _completedObservations,
                    ObserverGateMillisecondsBefore = _gateWaitTicks * 1000d / Stopwatch.Frequency,
                    ObserverBuildSerializeMillisecondsBefore = _buildAndSerializeTicks * 1000d / Stopwatch.Frequency,
                    ObserverWriteMillisecondsBefore = _writeTicks * 1000d / Stopwatch.Frequency
                } : new
                {
                    Boundary = "joint_operation_timing",
                    Operation = name,
                    State = boundary,
                    Milliseconds = milliseconds,
                    Sequence = ++_sequence,
                    PID = Environment.ProcessId,
                    TotalElapsedMilliseconds = Stopwatch.GetElapsedTime(_started).TotalMilliseconds,
                    ObserverCompletedBefore = _completedObservations,
                    ObserverGateMillisecondsBefore = _gateWaitTicks * 1000d / Stopwatch.Frequency,
                    ObserverBuildSerializeMillisecondsBefore = _buildAndSerializeTicks * 1000d / Stopwatch.Frequency,
                    ObserverWriteMillisecondsBefore = _writeTicks * 1000d / Stopwatch.Frequency
                };
                var record = JsonSerializer.Serialize(payload);
                var writeStarted = Stopwatch.GetTimestamp();
                _output.WriteLine(record);
                var written = Stopwatch.GetTimestamp();
                _gateWaitTicks += entered - requested;
                _buildAndSerializeTicks += writeStarted - buildStarted;
                _writeTicks += written - writeStarted;
                _completedObservations++;
            }
        }
    }
}
