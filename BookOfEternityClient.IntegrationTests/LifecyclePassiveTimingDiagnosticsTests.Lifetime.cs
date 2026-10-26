using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Core;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed partial class LifecyclePassiveTimingDiagnosticsTests
{
    /// <summary>Preserves all real original event pairs and the resource-close tail across concurrent completion calls.</summary>
    /// <param name="gzip">Whether the same existing writer uses opt-in lossless compression.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalEventsAndResourceCloseTailHaveOneCompletion(bool gzip)
    {
        var root = CreateRoot();
        var capture = new LifecyclePassiveTimingDiagnostics(nameof(OriginalEventsAndResourceCloseTailHaveOneCompletion),
            _transportTestOutput, gzipRecords: gzip);
        capture.BindFixtureRoot(root);
        var recorder = new OperationTimingAccumulator();
        var window = recorder.StartWindow(captureEvents: true);
        capture.Arm(window);
        var before = Stopwatch.GetTimestamp();
        for (var index = 0; index < 1100; index++) using (recorder.Measure("actual." + index)) { }
        var resource = recorder.Measure("resource.close.tail");
        await Task.Yield();
        resource!.Dispose(); // actual last scope ends during caller resource closing
        var after = Stopwatch.GetTimestamp();
        Directory.Delete(root);
        window.Dispose();
        var first = capture.CompleteAsync();
        var second = capture.CompleteAsync();
        var disposal = capture.DisposeAsync().AsTask();
        Assert.Same(first, second); Assert.Same(first, disposal);
        await Task.WhenAll(first, second, disposal);
        var rows = ReadRows(capture.RecordsPath);
        Assert.Equal(2202, rows.Length);
        for (var index = 0; index < rows.Length; index += 2)
        {
            var begin = rows[index]; var end = rows[index + 1];
            Assert.Equal(index + 1, begin.GetProperty("PassiveSequence").GetInt64());
            Assert.Equal(index + 2, end.GetProperty("PassiveSequence").GetInt64());
            Assert.Equal(begin.GetProperty("ScopeId").GetInt64(), end.GetProperty("ScopeId").GetInt64());
            Assert.Equal("begin", begin.GetProperty("State").GetString());
            Assert.Equal("end", end.GetProperty("State").GetString());
            var beginTick = begin.GetProperty("TimestampTicks").GetInt64();
            var endTick = end.GetProperty("TimestampTicks").GetInt64();
            Assert.InRange(beginTick, before, after); Assert.InRange(endTick, beginTick, after);
            Assert.Equal(endTick - beginTick, end.GetProperty("ElapsedTicks").GetInt64());
        }
        Assert.Equal("resource.close.tail", rows[^1].GetProperty("Operation").GetString());
        Assert.Equal(2202, window.CaptureEventStatus().Drained);
        Assert.True(window.Capture().Complete);
        Assert.True(capture.ConsumerBatches > 0);
        AssertComplete(capture);
    }

    /// <summary>Proves caller context is unchanged and absent from the detached consumer.</summary>
    /// <param name="alreadySuppressed">Whether the caller already suppressed context flow at synchronous startup.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConsumerDoesNotInheritCallerContext(bool alreadySuppressed)
    {
        var root = CreateRoot();
        var capture = new LifecyclePassiveTimingDiagnostics(nameof(ConsumerDoesNotInheritCallerContext), _transportTestOutput);
        capture.BindFixtureRoot(root);
        var recorder = new OperationTimingAccumulator();
        var window = recorder.StartWindow(captureEvents: true);
        var previous = LifecyclePassiveTimingDiagnostics.ContextSentinelForControl.Value;
        Exception? primary = null;
        string? callerAfterClose = null;
        try
        {
            LifecyclePassiveTimingDiagnostics.ContextSentinelForControl.Value = "caller-only";
            if (alreadySuppressed)
            {
                using var suppression = ExecutionContext.SuppressFlow();
                capture.Arm(window);
                Assert.True(ExecutionContext.IsFlowSuppressed());
            }
            else capture.Arm(window);
            Assert.False(ExecutionContext.IsFlowSuppressed());
            Assert.Equal("caller-only", LifecyclePassiveTimingDiagnostics.ContextSentinelForControl.Value);
            using (recorder.Measure("context.actual")) { }
        }
        catch (Exception error) { primary = error; }
        finally
        {
            try
            {
                try { Directory.Delete(root); }
                catch (Exception error) { primary ??= error; capture.RecordClosingFailure(error, "context-control-root-close"); }
                await capture.CompleteAsync(primary);
                callerAfterClose = LifecyclePassiveTimingDiagnostics.ContextSentinelForControl.Value;
            }
            finally { LifecyclePassiveTimingDiagnostics.ContextSentinelForControl.Value = previous; }
        }
        Assert.False(Assert.IsType<bool>(capture.ConsumerSawContextSentinel));
        Assert.Equal("caller-only", callerAfterClose);
        AssertComplete(capture);
    }

    /// <summary>Preserves pre-bind/full-message forwarding once while new persistent faults remain deferred.</summary>
    /// <param name="failWriter">Whether the actual existing records writer is disposed before functional work.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrebindMessagesForwardOnceAndPersistentFailureDefers(bool failWriter)
    {
        var root = CreateRoot();
        var original = new OriginalOutput();
        var capture = new LifecyclePassiveTimingDiagnostics(nameof(PrebindMessagesForwardOnceAndPersistentFailureDefers), original,
            failWriter ? LifecycleTimingFaultForControl.ClosedWriter : LifecycleTimingFaultForControl.None, gzipRecords: true);
        capture.WriteLine("before-binding 日本語");
        capture.BindFixtureRoot(root);
        var recorder = new OperationTimingAccumulator();
        var window = recorder.StartWindow(captureEvents: true);
        capture.Arm(window);
        capture.WriteLine("after-binding {0}", "рана");
        var bodyReached = false;
        using (recorder.Measure("body.after.message")) bodyReached = true;
        Assert.True(bodyReached);
        Assert.Equal(1, original.Messages.Count(row => row == "before-binding 日本語"));
        Assert.Equal(1, original.Messages.Count(row => row == "after-binding рана"));
        Directory.Delete(root);
        var failure = await Record.ExceptionAsync(() => capture.CompleteAsync());
        if (failWriter) Assert.IsType<AggregateException>(failure);
        else
        {
            Assert.Null(failure); AssertComplete(capture);
            var messages = ReadRows(capture.RecordsPath).Where(row => row.TryGetProperty("Message", out _)).ToArray();
            Assert.Equal(new[] { "before-binding 日本語", "after-binding рана" },
                messages.Select(row => row.GetProperty("Message").GetString()).ToArray());
        }
        using var report = JsonDocument.Parse(File.ReadAllText(capture.ReportPath));
        Assert.True(report.RootElement.GetProperty("FixtureClosing").GetProperty("Closing").GetBoolean());
        Assert.Equal(!failWriter, report.RootElement.GetProperty("DiagnosticComplete").GetBoolean());
        if (failWriter)
        {
            Assert.Contains(report.RootElement.GetProperty("Failures").EnumerateArray(),
                row => row.GetProperty("Stage").GetString() == "timing-flush");
            Assert.True(report.RootElement.GetProperty("FlushCosts").GetProperty("Attempted").GetInt64() > 0);
            Assert.Equal(0, report.RootElement.GetProperty("FlushCosts").GetProperty("Stored").GetInt64());
        }
    }

    /// <summary>Preserves the actual functional primary object/stack and cleanup receipt after a diagnostic fault.</summary>
    /// <param name="failureStage">The closed drain or actual completion-reference failure case.</param>
    [Theory]
    [InlineData("drain")]
    [InlineData("finish")]
    public async Task ActualDiagnosticFailureKeepsPrimaryObjectAndRootReceipt(string failureStage)
    {
        var root = CreateRoot();
        var original = new OriginalOutput(failureStage == "finish");
        var capture = new LifecyclePassiveTimingDiagnostics(nameof(ActualDiagnosticFailureKeepsPrimaryObjectAndRootReceipt), original,
            failureStage == "drain" ? LifecycleTimingFaultForControl.DrainAfterDetach : LifecycleTimingFaultForControl.None);
        capture.BindFixtureRoot(root);
        var recorder = new OperationTimingAccumulator();
        var window = recorder.StartWindow(captureEvents: true);
        capture.Arm(window);
        var scope = recorder.Measure("fault.actual");
        Exception? setupFailure = null;
        try { if (failureStage == "drain") await WaitUntilAsync(() => window.CaptureEventStatus().Drained > 0); }
        catch (Exception error) { setupFailure = error; }
        finally { scope!.Dispose(); }
        var primary = setupFailure ?? Record.Exception(ThrowPrimary)!;
        Directory.Delete(root);
        var failure = await Record.ExceptionAsync(() => capture.CompleteAsync(primary));
        if (setupFailure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(setupFailure).Throw();
        Assert.Same(primary, failure); Assert.Contains(nameof(ThrowPrimary), failure!.StackTrace ?? string.Empty);
        Assert.Same(failure, await Record.ExceptionAsync(() => capture.DisposeAsync().AsTask()));
        using var report = JsonDocument.Parse(File.ReadAllText(capture.ReportPath));
        Assert.False(report.RootElement.GetProperty("DiagnosticComplete").GetBoolean());
        Assert.Equal(root, report.RootElement.GetProperty("FixtureClosing").GetProperty("Owned").GetString());
        Assert.True(report.RootElement.GetProperty("FixtureClosing").GetProperty("Closing").GetBoolean());
        Assert.NotEmpty(report.RootElement.GetProperty("Failures").EnumerateArray());
        if (failureStage == "drain")
        {
            Assert.Equal(2, window.CaptureEventStatus().Drained);
            Assert.Equal(1, report.RootElement.GetProperty("TimingStored").GetInt64());
        }
        else Assert.Contains(report.RootElement.GetProperty("Failures").EnumerateArray(),
            row => row.GetProperty("Stage").GetString() == "completion-reference");
    }

    /// <summary>Refuses cleanup qualification for a retained directory, replacement file or broken link.</summary>
    /// <param name="replacement">The actual entry left at the registered root after the caller cleanup boundary.</param>
    [Theory]
    [InlineData("directory")]
    [InlineData("file")]
    [InlineData("broken-link")]
    public async Task RemainingOrReplacedRootCannotBecomeSuccessfulCleanup(string replacement)
    {
        var root = CreateRoot();
        var capture = new LifecyclePassiveTimingDiagnostics(nameof(RemainingOrReplacedRootCannotBecomeSuccessfulCleanup), _transportTestOutput);
        capture.BindFixtureRoot(root);
        var recorder = new OperationTimingAccumulator();
        capture.Arm(recorder.StartWindow(captureEvents: true));
        using (recorder.Measure("root.actual")) { }
        if (replacement != "directory")
        {
            Directory.Delete(root);
            if (replacement == "file") File.WriteAllText(root, "replacement");
            else Directory.CreateSymbolicLink(root, root + "-missing-target");
        }
        try
        {
            Assert.IsType<AggregateException>(await Record.ExceptionAsync(() => capture.CompleteAsync()));
            using var report = JsonDocument.Parse(File.ReadAllText(capture.ReportPath));
            var closing = report.RootElement.GetProperty("FixtureClosing");
            Assert.True(closing.GetProperty("Retained").GetBoolean()); Assert.False(closing.GetProperty("Closing").GetBoolean());
            Assert.False(report.RootElement.GetProperty("DiagnosticComplete").GetBoolean());
            Assert.NotEqual(TrustedLocalFileScope.EntryKind.Missing, TrustedLocalFileScope.Probe(root));
        }
        finally
        {
            // Only the cheap control cleans its own deliberate replacement AFTER
            // the refused receipt; the adapter never retries or requalifies it.
            if (replacement == "file") File.Delete(root); else Directory.Delete(root);
        }
    }

    /// <summary>Refuses an open scope or a late resource-end boundary rather than fabricating a closed trace.</summary>
    /// <param name="late">Whether the real scope ends after its window closed.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenOrLateScopeRefusesCompleteTrace(bool late)
    {
        var root = CreateRoot();
        var capture = new LifecyclePassiveTimingDiagnostics(nameof(OpenOrLateScopeRefusesCompleteTrace), _transportTestOutput);
        capture.BindFixtureRoot(root);
        var recorder = new OperationTimingAccumulator();
        var window = recorder.StartWindow(captureEvents: true);
        capture.Arm(window);
        var scope = recorder.Measure("unfinished.actual");
        if (late) { window.Dispose(); scope!.Dispose(); }
        Directory.Delete(root);
        try
        {
            Assert.IsType<AggregateException>(await Record.ExceptionAsync(() => capture.CompleteAsync()));
            using var report = JsonDocument.Parse(File.ReadAllText(capture.ReportPath));
            Assert.False(report.RootElement.GetProperty("PassiveTimingCompletion").GetProperty("Complete").GetBoolean());
        }
        finally { scope?.Dispose(); }
    }

    /// <summary>Retries only the unfinished physical final row and rejects completed malformed JSON.</summary>
    [Fact]
    public void LiveReaderRetriesOnlyUnfinishedFinalFragment()
    {
        var root = CreateRoot(); var path = Path.Combine(root, "partial.jsonl");
        try
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(stream) { AutoFlush = true };
            writer.Write("{\"Kind\":\"first\"}\n{\"Kind\":");
            Assert.Single(ReadRows(path));
            writer.Write("\"second\"}\n");
            Assert.Equal(2, ReadRows(path).Length);
            writer.Write("malformed-complete-row\n");
            Assert.Throws<JsonException>(() => ReadRows(path));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-lifecycle-timing-control-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); return root;
    }
    private static void AssertComplete(LifecyclePassiveTimingDiagnostics capture)
    {
        using var report = JsonDocument.Parse(File.ReadAllText(capture.ReportPath));
        Assert.True(report.RootElement.GetProperty("DiagnosticComplete").GetBoolean());
        Assert.True(report.RootElement.GetProperty("PassiveTimingCompletion").GetProperty("Complete").GetBoolean());
        Assert.Equal(capture.RegisteredRoot, report.RootElement.GetProperty("FixtureClosing").GetProperty("Owned").GetString());
        Assert.True(report.RootElement.GetProperty("FixtureClosing").GetProperty("Closing").GetBoolean());
    }
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var start = Stopwatch.GetTimestamp();
        while (!condition() && Stopwatch.GetElapsedTime(start) < TimeSpan.FromSeconds(2)) await Task.Delay(10);
        Assert.True(condition());
    }
    private static void ThrowPrimary() => throw new InvalidOperationException("Original functional failure.");

    private sealed class OriginalOutput(bool failFinish = false) : ITestOutputHelper
    {
        internal List<string> Messages { get; } = [];
        /// <inheritdoc />
        public void WriteLine(string message)
        {
            Messages.Add(message);
            if (failFinish && message.Contains("\"Kind\":\"JointBundleDiagnosticFinished\"", StringComparison.Ordinal))
                throw new IOException("Actual completion reference writer failure.");
        }
        /// <inheritdoc />
        public void WriteLine(string format, params object[] args) => WriteLine(string.Format(format, args));
    }
}
