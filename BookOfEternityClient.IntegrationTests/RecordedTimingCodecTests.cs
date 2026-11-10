using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Independent accepted-format oracle over real sealed recorder DATA, without a game or authority.</summary>
public sealed class RecordedTimingCodecTests
{
    /// <summary>Compares whole strings/UTF8, including member order/default escaping, to the frozen anonymous-object format.</summary>
    [Fact]
    public void ActualNestedAndLateEventsKeepExactAcceptedFormat()
    {
        using var writer = new StringWriter();
        var beforeOrigin = Stopwatch.GetTimestamp();
        var timings = new GameEngineTurnLifecycleTests.JointOperationTimings(writer);
        var afterOrigin = Stopwatch.GetTimestamp();
        var events = CaptureActualEvents();
        Assert.Equal(6, events.Length);
        foreach (var item in events) timings.ObserveRecorded(item);
        var rows = writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(events.Length, rows.Length);
        double previousGate = 0, previousBuild = 0, previousWrite = 0;
        for (var index = 0; index < rows.Length; index++)
        {
            var source = events[index];
            using var document = JsonDocument.Parse(rows[index]);
            var row = document.RootElement;
            Assert.Equal(index + 1, row.GetProperty("Sequence").GetInt64());
            Assert.Equal(Environment.ProcessId, row.GetProperty("PID").GetInt32());
            Assert.Equal(source.Name, row.GetProperty("Operation").GetString());
            Assert.Equal(source.State, row.GetProperty("State").GetString());
            Assert.Equal(source.Sequence, row.GetProperty("PassiveSequence").GetInt64());
            Assert.Equal(source.ScopeId, row.GetProperty("ScopeId").GetInt64());
            Assert.Equal(source.TimestampTicks, row.GetProperty("TimestampTicks").GetInt64());
            Assert.Equal(source.ElapsedTicks, row.GetProperty("ElapsedTicks").GetInt64());
            Assert.Equal(source.Late, row.GetProperty("Late").GetBoolean());
            Assert.Equal(source.ElapsedTicks * 1000d / Stopwatch.Frequency, row.GetProperty("Milliseconds").GetDouble());
            var observer = ReadObserverScalars(row);
            Assert.Equal(index, observer.Completed);
            Assert.InRange(observer.TotalMilliseconds,
                Stopwatch.GetElapsedTime(afterOrigin, source.TimestampTicks).TotalMilliseconds,
                Stopwatch.GetElapsedTime(beforeOrigin, source.TimestampTicks).TotalMilliseconds);
            Assert.True(observer.GateMilliseconds >= previousGate);
            Assert.True(observer.BuildMilliseconds >= previousBuild);
            Assert.True(observer.WriteMilliseconds >= previousWrite);
            if (index == 0)
            {
                Assert.Equal(0, observer.GateMilliseconds); Assert.Equal(0, observer.BuildMilliseconds);
                Assert.Equal(0, observer.WriteMilliseconds);
            }
            var expected = AcceptedOldFormat(source, index + 1, observer);
            Assert.Equal(expected, rows[index]);
            Assert.Equal(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(rows[index]));
            previousGate = observer.GateMilliseconds;
            previousBuild = observer.BuildMilliseconds;
            previousWrite = observer.WriteMilliseconds;
        }
        Assert.Contains(events, row => row.State == "begin" && row.ElapsedTicks == 0 && !row.Late);
        Assert.Contains(events, row => row.State == "end" && row.ElapsedTicks > 0);
        Assert.True(events[^1].Late);
        Assert.Contains("\\u", rows[0]); // default escaping is part of the full-string contract
    }

    /// <summary>Requires the same string writer exception, consumed sequence and unchanged successful-cost counters.</summary>
    [Fact]
    public void ActualWriterFailureKeepsStringDispatchAndAcceptedCounterOrdering()
    {
        using var writer = new FailFirstStringWriter();
        var timings = new GameEngineTurnLifecycleTests.JointOperationTimings(writer);
        var recorder = new OperationTimingAccumulator();
        using var window = recorder.StartWindow(captureEvents: true);
        using (recorder.Measure("real failed writer 日本語 \"\\\n")) { }
        window.Dispose();
        var events = window.DrainEvents();
        Assert.Equal(2, events.Length);
        Assert.Same(writer.Failure, Record.Exception(() => timings.ObserveRecorded(events[0])));
        timings.ObserveRecorded(events[1]);
        Assert.Equal(2, writer.Attempts);
        var actual = Assert.Single(writer.Rows);
        using var document = JsonDocument.Parse(actual);
        var row = document.RootElement;
        var observer = ReadObserverScalars(row);
        Assert.Equal(2, row.GetProperty("Sequence").GetInt64());
        Assert.Equal(0, observer.Completed);
        Assert.Equal(0, observer.GateMilliseconds); Assert.Equal(0, observer.BuildMilliseconds);
        Assert.Equal(0, observer.WriteMilliseconds);
        Assert.Equal(events[1].TimestampTicks, row.GetProperty("TimestampTicks").GetInt64());
        Assert.Equal(events[1].ElapsedTicks, row.GetProperty("ElapsedTicks").GetInt64());
        var expected = AcceptedOldFormat(events[1], 2, observer);
        Assert.Equal(expected, actual);
        Assert.Equal(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
    }

    private static OperationTimingAccumulator.NamedEvent[] CaptureActualEvents()
    {
        var recorder = new OperationTimingAccumulator();
        using var window = recorder.StartWindow(captureEvents: true);
        const string name = "actual 日本語 рана 🩹 \"quote\" \\ <>&'\n\r\t\0\u0001";
        using (recorder.Measure(name)) using (recorder.Measure(name)) { }
        var late = recorder.Measure("actual late \"\\\n");
        window.Dispose();
        late!.Dispose(); // actual sealed source records this end as late
        return window.DrainEvents();
    }

    private readonly record struct ObserverScalars(double TotalMilliseconds, long Completed,
        double GateMilliseconds, double BuildMilliseconds, double WriteMilliseconds);

    // Only observer-origin/cost values are read from the result. Source identity,
    // field list/order, duration, transport sequence and PID stay independent.
    private static ObserverScalars ReadObserverScalars(JsonElement row) => new(
        row.GetProperty("TotalElapsedMilliseconds").GetDouble(), row.GetProperty("ObserverCompletedBefore").GetInt64(),
        row.GetProperty("ObserverGateMillisecondsBefore").GetDouble(), row.GetProperty("ObserverBuildSerializeMillisecondsBefore").GetDouble(),
        row.GetProperty("ObserverWriteMillisecondsBefore").GetDouble());

    // Frozen accepted passive shape from 92aaa8, authored before the typed
    // implementation. No candidate DTO, generated metadata or codec is used.
    private static string AcceptedOldFormat(OperationTimingAccumulator.NamedEvent source, long transportSequence, ObserverScalars observer)
    {
        object payload = new
        {
            Boundary = "joint_operation_timing", Operation = source.Name, State = source.State,
            Milliseconds = source.ElapsedTicks * 1000d / Stopwatch.Frequency, Sequence = transportSequence, PID = Environment.ProcessId,
            TotalElapsedMilliseconds = observer.TotalMilliseconds,
            Transport = "sealed-passive-events-v1", PassiveSequence = source.Sequence,
            source.ScopeId, source.TimestampTicks, source.ElapsedTicks, source.Late,
            ObserverCompletedBefore = observer.Completed,
            ObserverGateMillisecondsBefore = observer.GateMilliseconds,
            ObserverBuildSerializeMillisecondsBefore = observer.BuildMilliseconds,
            ObserverWriteMillisecondsBefore = observer.WriteMilliseconds
        };
        return JsonSerializer.Serialize(payload);
    }

    private sealed class FailFirstStringWriter : TextWriter
    {
        internal IOException Failure { get; } = new("Actual recorded string writer failure.");
        internal int Attempts { get; private set; }
        internal List<string> Rows { get; } = [];
        /// <inheritdoc />
        public override Encoding Encoding => Encoding.UTF8;
        /// <inheritdoc />
        public override void WriteLine(string? value)
        {
            if (++Attempts == 1) throw Failure;
            Rows.Add(value ?? string.Empty);
        }
    }
}
