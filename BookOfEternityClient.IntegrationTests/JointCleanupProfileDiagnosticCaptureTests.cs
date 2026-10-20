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
        using var writer = new FailOnceWriter(failAt);
        var capture = new JointCleanupProfileDiagnosticCapture(writer);
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

        /// <summary>
        /// Selects the one write to fail.
        /// </summary>
        /// <param name="failAt">
        /// One-based write index, or zero for no injected error.
        /// </param>
        internal FailOnceWriter(int failAt) => _failAt = failAt;

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
            if (++_writes == _failAt) throw Failure;
            _text.AppendLine(value);
        }
    }
}
