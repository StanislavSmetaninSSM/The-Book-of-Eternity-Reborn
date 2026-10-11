using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class BrowserAdmissionDiagnosticsTests(ITestOutputHelper output)
{
    private static Type Collector => typeof(FileSystemManager).Assembly.GetType(
        "BookOfEternityClient.Core.BrowserAdmissionDiagnosticCollector")
        ?? throw new Xunit.Sdk.XunitException("Missing bounded original-browser diagnostic collector.");
    private static object Invoke(object collector, string method, params object[] args) =>
        Collector.GetMethod(method, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(collector, args)!;
    private static object Create(Func<long> clock, int capacity = 64) =>
        Collector.GetMethod("CreateForTests", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [clock, capacity])!;
    private static IDisposable Context(object collector, string origin, string request = "unbound") =>
        (IDisposable)Invoke(collector, "Context", origin, request, "staged", "SessionMutation", 7L, true);
    private static IDisposable Measure(object collector, string stage) =>
        (IDisposable)Invoke(collector, "Measure", stage);
    private static JsonNode Snapshot(object collector) => JsonNode.Parse((string)Invoke(collector, "SnapshotJson"))!;

    [Fact]
    public void NestedSynchronousFramesConserveSelfTimeAndClassifyBytes()
    {
        long ticks = 0;
        var collector = Create(() => ticks);
        using (Context(collector, "acquired-lock-before-recovery", "0123456789abcdef0123456789abcdef"))
        using (Measure(collector, "inclusive-preflight"))
        {
            Assert.False(Snapshot(collector)["Complete"]!.GetValue<bool>());
            ticks = 10;
            using (Measure(collector, "physical-verification"))
            {
                Invoke(collector, "Read", "tuple", 23L);
                ticks = 20;
                using (Measure(collector, "snapshot-hash-verification"))
                {
                    Invoke(collector, "Read", "snapshot-hash", 31L);
                    ticks = 40;
                }
                ticks = 50;
            }
            ticks = 70;
        }
        var result = Snapshot(collector);
        var rows = result["Rows"]!.AsArray();
        Assert.Equal(3, rows.Count);
        var parent = rows.Single(row => row!["Stage"]!.GetValue<string>() == "inclusive-preflight")!;
        Assert.Equal(70, parent["InclusiveTicks"]!.GetValue<long>());
        Assert.Equal(30, parent["SelfTicks"]!.GetValue<long>());
        Assert.Equal(70, rows.Sum(row => row!["SelfTicks"]!.GetValue<long>()));
        Assert.Equal(23, rows.Sum(row => row!["TupleBytes"]!.GetValue<long>()));
        Assert.Equal(31, rows.Sum(row => row!["SnapshotHashBytes"]!.GetValue<long>()));
        Assert.All(rows, row => Assert.Equal("acquired-lock-before-recovery", row!["Origin"]!.GetValue<string>()));
        Assert.Equal(0, result["AccountingErrors"]!.GetValue<int>());
    }

    [Fact]
    public async Task ConcurrentBranchesKeepSynchronousFrameStacksSeparate()
    {
        var collector = Create(System.Diagnostics.Stopwatch.GetTimestamp);
        using var barrier = new Barrier(2);
        await Task.WhenAll(new[] { "main-admission-acquire", "worker-cleanup-audit-append" }.Select(origin =>
            Task.Factory.StartNew(() =>
            {
                using (Context(collector, origin))
                using (Measure(collector, "inclusive-preflight"))
                {
                    Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(5)));
                    using (Measure(collector, "physical-verification")) Invoke(collector, "Read", "rollback", 17L);
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)));
        var result = Snapshot(collector);
        Assert.Equal(4, result["Rows"]!.AsArray().Count);
        Assert.Equal(34, result["Rows"]!.AsArray().Sum(row => row!["RollbackBytes"]!.GetValue<long>()));
        Assert.Equal(0, result["AccountingErrors"]!.GetValue<int>());
    }

    [Fact]
    public void SnapshotDuringClockSetupCannotClaimACompletedRow()
    {
        using var starting = new ManualResetEventSlim();
        using var releaseClock = new ManualResetEventSlim();
        using var releaseScope = new ManualResetEventSlim();
        var calls = 0;
        var collector = Create(() =>
        {
            if (Interlocked.Increment(ref calls) == 2)
            {
                starting.Set();
                Assert.True(releaseClock.Wait(TimeSpan.FromSeconds(5)));
            }
            return 1;
        });
        var measuring = Task.Factory.StartNew(() =>
        {
            using (Measure(collector, "setup-in-progress"))
                Assert.True(releaseScope.Wait(TimeSpan.FromSeconds(5)));
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            Assert.True(starting.Wait(TimeSpan.FromSeconds(5)));
            var snapshot = Snapshot(collector);
            Assert.False(snapshot["Complete"]!.GetValue<bool>());
        }
        finally
        {
            releaseClock.Set(); releaseScope.Set();
            measuring.GetAwaiter().GetResult();
        }
        Assert.True(Snapshot(collector)["Complete"]!.GetValue<bool>());
    }

    [Fact]
    public void CapacityBoundsAggregationAndMarksDroppedRows()
    {
        var collector = Create(() => 1, 2);
        for (var i = 0; i < 20; i++)
            using (Context(collector, "other"))
            using (Measure(collector, "stage-" + i)) { }
        var result = Snapshot(collector);
        Assert.Equal(2, result["Rows"]!.AsArray().Count);
        Assert.Equal(18, result["DroppedRows"]!.GetValue<int>());
        Assert.False(result["Complete"]!.GetValue<bool>());
    }

    [Fact]
    public void DisabledOrInvalidOwnedOptionsCreateNoCollectorOrExport()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-diag-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var create = Collector.GetMethod("CreateOwned", BindingFlags.Static | BindingFlags.NonPublic)!;
            Assert.Null(create.Invoke(null, [null, null, root]));
            Assert.Null(create.Invoke(null, [root, "invalid", root]));
            Assert.Null(create.Invoke(null, [root, Guid.NewGuid().ToString("N"), root]));
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
            var nested = Path.Combine(root, "nested");
            Directory.CreateDirectory(nested);
            var nonce = Guid.NewGuid().ToString("N");
            File.WriteAllText(Path.Combine(nested, "owner-nonce"), nonce);
            Assert.Null(create.Invoke(null, [nested, nonce, root]));
            Directory.Delete(nested, true);
        }
        finally { Directory.Delete(root); }
    }

    [Fact]
    public void OwnedExportIsCreateOnlyAndFailuresCannotEscape()
    {
        var own = Path.Combine(Path.GetTempPath(), "boe-diag-" + Guid.NewGuid().ToString("N"));
        var canonical = Path.Combine(own, "canonical");
        var evidence = Path.Combine(own, "evidence");
        Directory.CreateDirectory(canonical);
        Directory.CreateDirectory(evidence);
        var nonce = Guid.NewGuid().ToString("N");
        File.WriteAllText(Path.Combine(evidence, "owner-nonce"), nonce);
        try
        {
            var collector = Collector.GetMethod("CreateOwned", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [evidence, nonce, canonical]);
            Assert.NotNull(collector);
            using (Context(collector, "other")) using (Measure(collector, "inclusive-preflight")) { }
            Invoke(collector, "Export");
            var exported = Directory.EnumerateFiles(evidence, "*.json").Single();
            var original = File.ReadAllBytes(exported);
            Invoke(collector, "Export");
            Assert.Equal(original, File.ReadAllBytes(exported));
            var refused = Collector.GetMethod("CreateOwned", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [evidence, nonce, canonical]);
            Assert.NotNull(refused);
            File.WriteAllText(Path.Combine(evidence, "owner-nonce"), "changed");
            Invoke(refused, "Export");
            Assert.Equal(1, Snapshot(refused)["ExportFailures"]!.GetValue<int>());
            Assert.False(Snapshot(refused)["Complete"]!.GetValue<bool>());
            Assert.Equal(original, File.ReadAllBytes(exported));
            Assert.Empty(Directory.EnumerateFileSystemEntries(canonical));
        }
        finally { Directory.Delete(own, true); }
    }

    [Fact]
    public void FiniteCollectorCalibrationReportsOwnElapsedWithoutLatencyClaim()
    {
        var collector = Create(System.Diagnostics.Stopwatch.GetTimestamp);
        const int repetitions = 10000;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < repetitions; i++) { }
        var emptySeconds = watch.Elapsed.TotalSeconds;
        watch.Restart();
        using (Context(collector, "other"))
            for (var i = 0; i < repetitions; i++) using (Measure(collector, "calibration")) { }
        var diagnosticSeconds = watch.Elapsed.TotalSeconds;
        var result = Snapshot(collector);
        Assert.Equal(repetitions, result["Rows"]!.AsArray().Single()!["Count"]!.GetValue<long>());
        output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { Repetitions = repetitions, EmptySeconds = emptySeconds,
            DiagnosticSeconds = diagnosticSeconds, Scope = "Finite in-memory collector calibration; not end-to-end overhead or acceptance latency." }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalHookFailurePropagatesWithDiagnosticsDisabledAndEnabled(bool enabled)
    {
        var own = Path.Combine(Path.GetTempPath(), "boe-diag-hook-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(own);
        try
        {
            var original = new InvalidOperationException("Original hook failure remains visible.");
            var files = new FileSystemManager(own, Microsoft.Extensions.Logging.Abstractions.NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                { BrowserOriginalAdmissionTimingObserver = (_, _) => throw original });
            var collector = enabled ? Create(System.Diagnostics.Stopwatch.GetTimestamp) : null;
            typeof(FileSystemManager).GetField("_browserAdmissionDiagnostic", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(files, collector);
            var measured = (IDisposable)typeof(FileSystemManager).GetMethod("MeasureOriginalBrowserAdmission",
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(files, ["inclusive-preflight"])!;
            Assert.Same(original, Assert.Throws<InvalidOperationException>(measured.Dispose));
            if (collector != null)
            {
                var snapshot = Snapshot(collector);
                Assert.Equal(1, snapshot["Rows"]!.AsArray().Single()!["Count"]!.GetValue<long>());
                Assert.Equal(0, snapshot["DiagnosticFailures"]!.GetValue<int>());
            }
            Assert.Empty(Directory.EnumerateFileSystemEntries(own));
        }
        finally { Directory.Delete(own); }
    }
}
