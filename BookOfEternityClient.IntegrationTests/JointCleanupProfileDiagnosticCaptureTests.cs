using System.Text;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Injects diagnostic storage failures without starting a game or the genuine DATA profile.
/// </summary>
public sealed class JointCleanupProfileDiagnosticCaptureTests
{
    /// <summary>
    /// Requires every observer storage failure to survive the production hook's exception isolation.
    /// </summary>
    /// <param name="failAt">
    /// The begin or end write to fail once; zero keeps every write successful.
    /// </param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ObserverFailureRemainsExplicitAndLaterRecordsContinue(int failAt)
    {
        var clock = new ManualDiagnosticClock();
        using var writer = new FailOnceWriter(failAt, () => clock.Advance(TimeSpan.FromMilliseconds(6)));
        var capture = new JointCleanupProfileDiagnosticCapture(writer, clock);
        var files = new FileSystemManager(Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "unused-profile-observer-" + Guid.NewGuid().ToString("N")),
            NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks { OperationTimingObserver = capture.Observe });
        var body = 0;
        using (files.MeasureOperation("control.first")) body++;
        using (files.MeasureOperation("control.second")) body++;
        Assert.Equal(2, body);
        Assert.Equal(4, capture.Attempted);
        Assert.Equal(failAt == 0 ? 4 : 3, capture.Stored);
        Assert.Equal(failAt == 0, capture.Complete);
        if (failAt == 0) Assert.Empty(capture.GetFailures());
        else
        {
            var failure = Assert.Single(capture.GetFailures());
            Assert.Same(writer.Failure, failure.Error);
            Assert.Contains(failAt == 1 ? "begin" : "end", failure.Stage);
        }
        Assert.Contains("control.second", writer.Text);
        var costs = capture.GetWriteCosts();
        Assert.Equal(4, costs.Length);
        Assert.Equal(4, costs.Sum(row => row.Attempted));
        Assert.Equal(capture.Stored, costs.Sum(row => row.Stored));
        Assert.All(costs, row => { Assert.Equal(1, row.Attempted); Assert.Equal(1, row.Measured);
            Assert.Equal(TimeSpan.FromMilliseconds(6), row.SerializationAndWriteElapsed); });
        Assert.Equal(failAt == 1 ? 0 : 1, Assert.Single(costs, row => row.Operation == "control.first" && row.State == "begin").Stored);
        Assert.Equal(failAt == 2 ? 0 : 1, Assert.Single(costs, row => row.Operation == "control.first" && row.State == "end").Stored);
        Assert.Equal(2, costs.Count(row => row.Operation == "control.second" && row.Stored == 1));
        costs[0] = default;
        Assert.All(capture.GetWriteCosts(), row => Assert.NotNull(row.Operation));
    }

    /// <summary>
    /// Keeps an injected clock failure secondary to storage failure and continues later timing writes.
    /// </summary>
    /// <param name="timestampFailureAt">
    /// One-based timestamp call to fail at the first write's start or end.
    /// </param>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ClockFailureDoesNotReplaceStorageFailureOrPreventLaterRecords(int timestampFailureAt)
    {
        var clock = new ManualDiagnosticClock(timestampFailureAt);
        using var writer = new FailOnceWriter(1, () => clock.Advance(TimeSpan.FromMilliseconds(6)));
        var capture = new JointCleanupProfileDiagnosticCapture(writer, clock);
        capture.Observe("first", "begin", TimeSpan.Zero);
        capture.Observe("later", "end", TimeSpan.FromMilliseconds(10));
        Assert.Equal(2, capture.Attempted);
        Assert.Equal(1, capture.Stored);
        Assert.False(capture.Complete);
        Assert.Contains(capture.GetFailures(), row => ReferenceEquals(row.Error, writer.Failure));
        Assert.Contains(capture.GetFailures(), row => ReferenceEquals(row.Error, clock.Failure));
        var costs = capture.GetWriteCosts();
        var first = Assert.Single(costs, row => row.Operation == "first");
        Assert.Equal(1, first.Attempted); Assert.Equal(0, first.Stored); Assert.Equal(0, first.Measured);
        Assert.Equal(TimeSpan.Zero, first.SerializationAndWriteElapsed);
        var later = Assert.Single(costs, row => row.Operation == "later");
        Assert.Equal(1, later.Attempted); Assert.Equal(1, later.Stored); Assert.Equal(1, later.Measured);
        Assert.Equal(TimeSpan.FromMilliseconds(6), later.SerializationAndWriteElapsed);
        Assert.Contains("later", writer.Text);
    }

    /// <summary>
    /// Supplies exact elapsed write costs without sleeping or constructing a game world.
    /// </summary>
    private sealed class ManualDiagnosticClock : TimeProvider
    {
        private long _ticks;
        private int _calls;
        private readonly int _failAt;

        /// <summary>
        /// Selects an optional timestamp failure.
        /// </summary>
        /// <param name="failAt">
        /// One-based timestamp call to fail, or zero to keep all calls successful.
        /// </param>
        internal ManualDiagnosticClock(int failAt = 0) => _failAt = failAt;

        /// <summary>
        /// Gets the exact clock error used by the fixture.
        /// </summary>
        internal InvalidOperationException Failure { get; } = new("injected diagnostic clock failure");

        /// <inheritdoc />
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        /// <inheritdoc />
        public override long GetTimestamp()
        {
            if (++_calls == _failAt) throw Failure;
            return _ticks;
        }

        /// <summary>
        /// Advances only the fixture's monotonic counter.
        /// </summary>
        /// <param name="elapsed">
        /// Exact nonnegative simulated write duration.
        /// </param>
        internal void Advance(TimeSpan elapsed) => _ticks += elapsed.Ticks;
    }

    /// <summary>
    /// Preserves the validator exception and the full durable reporting secondary together.
    /// </summary>
    /// <returns>
    /// Completion after an injected cost-report failure remains in both deferred evidence and exception data.
    /// </returns>
    [Fact]
    public async Task ComponentCostReportFailurePreservesPrimaryAndDurableSecondary()
    {
        using var timingWriter = new StringWriter();
        using var costWriter = new FailOnceWriter(1);
        var capture = new JointCleanupProfileDiagnosticCapture(timingWriter);
        var files = new FileSystemManager(Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "unused-profile-cost-" + Guid.NewGuid().ToString("N")),
            NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks { OperationTimingObserver = capture.Observe });
        var primary = new InvalidDataException("original validator refusal");
        var observed = await Record.ExceptionAsync(() => JointCleanupValidatorProfileTests.MeasureAsync<bool>(files,
            costWriter, capture, "control.refusal", () => Task.FromException<bool>(primary)));
        Assert.Same(primary, observed);
        Assert.Contains(primary.Data.Values.Cast<object>(), value => ReferenceEquals(value, costWriter.Failure));
        var secondary = Assert.Single(capture.GetFailures());
        Assert.Same(costWriter.Failure, secondary.Error);
        Assert.Equal("component-cost-report", secondary.Stage);
        Assert.False(capture.Complete);
    }

    /// <summary>
    /// Fails one complete diagnostic write while allowing all later writes to continue.
    /// </summary>
    private sealed class FailOnceWriter : TextWriter
    {
        private readonly StringBuilder _text = new();
        private readonly int _failAt;
        private int _writes;
        private readonly Action? _beforeWrite;

        /// <summary>
        /// Selects the one write to fail.
        /// </summary>
        /// <param name="failAt">
        /// One-based write index, or zero for no injected error.
        /// </param>
        /// <param name="beforeWrite">
        /// Optional fixture action invoked before each successful or failed write attempt.
        /// </param>
        internal FailOnceWriter(int failAt, Action? beforeWrite = null)
        {
            _failAt = failAt;
            _beforeWrite = beforeWrite;
        }

        /// <summary>
        /// Gets the exact injected storage error retained by the capture.
        /// </summary>
        internal IOException Failure { get; } = new("injected diagnostic write or flush failure");

        /// <summary>
        /// Gets all successfully written diagnostic text without a record quota.
        /// </summary>
        internal string Text => _text.ToString();

        /// <inheritdoc />
        public override Encoding Encoding => Encoding.UTF8;

        /// <summary>
        /// Injects the selected error or retains this complete line.
        /// </summary>
        /// <param name="value">
        /// Complete diagnostic text; null contributes only the line ending.
        /// </param>
        public override void WriteLine(string? value)
        {
            _beforeWrite?.Invoke();
            if (++_writes == _failAt) throw Failure;
            _text.AppendLine(value);
        }
    }
}
