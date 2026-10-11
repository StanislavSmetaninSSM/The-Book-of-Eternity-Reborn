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

    // New attribution contracts are deliberately test-first. These are collector
    // observations, never assertions that a storage admission or turn succeeded.
    private static IDisposable Logical(object collector, string phase, string caller) =>
        (IDisposable)Invoke(collector, "Logical", phase, caller);
    private static IDisposable Window(object collector) => (IDisposable)Invoke(collector, "BeginWindow",
        "0123456789abcdef0123456789abcdef", "fedcba9876543210fedcba9876543210", "11111111111111111111111111111111");

    [Fact]
    public async Task LogicalLabelsFlowAcrossAwaitAndRestoreWithoutSiblingLeakage()
    {
        var collector = Create(System.Diagnostics.Stopwatch.GetTimestamp);
        using (Logical(collector, "outer", "engine-test"))
        {
            await Task.WhenAll(new[] { "MetaMiscStateFiles", "ClientOwnedControlFiles" }.Select(async phase =>
            {
                using (Logical(collector, phase, "engine-test"))
                {
                    await Task.Yield();
                    using (Measure(collector, "physical-verification")) { }
                }
            }));
            var original = new InvalidOperationException("logical branch original");
            var caught = await Record.ExceptionAsync(async () =>
            {
                using (Logical(collector, "failed-branch", "engine-test"))
                { await Task.Yield(); throw original; }
            });
            Assert.Same(original, caught);
            using (Measure(collector, "after-branches")) { }
        }
        using (Measure(collector, "after-restore")) { }
        var rows = Snapshot(collector)["Rows"]!.AsArray();
        Assert.Equal(new[] { "ClientOwnedControlFiles", "MetaMiscStateFiles" },
            rows.Where(row => row!["Stage"]!.GetValue<string>() == "physical-verification")
                .Select(row => row!["ValidationPhase"]!.GetValue<string>()).OrderBy(value => value));
        Assert.Equal("outer", rows.Single(row => row!["Stage"]!.GetValue<string>() == "after-branches")!["ValidationPhase"]!.GetValue<string>());
        Assert.Equal("engine-test", rows.Single(row => row!["Stage"]!.GetValue<string>() == "after-branches")!["EngineCaller"]!.GetValue<string>());
        Assert.Equal("unbound", rows.Single(row => row!["Stage"]!.GetValue<string>() == "after-restore")!["ValidationPhase"]!.GetValue<string>());
        Assert.Equal(0, Snapshot(collector)["AccountingErrors"]!.GetValue<int>());
    }

    [Fact]
    public void WindowSpansSeparateContainedCrossingAndLateWork()
    {
        long ticks = 0;
        var collector = Create(() => ticks);
        using (Logical(collector, "MetaMiscStateFiles", "postaccepted-validation"))
        using (Window(collector))
        {
            ticks = 1;
            using (Measure(collector, "contained")) ticks = 3;
            ticks = 4;
            using (Measure(collector, "crossing"))
            {
                ticks = 6;
                Invoke(collector, "Mark", "accepted-receipt-published");
                ticks = 8;
            }
            ticks = 9;
            using (Measure(collector, "late")) ticks = 11;
            ticks = 12;
        }
        var result = Snapshot(collector);
        var rows = result["Rows"]!.AsArray();
        var contained = rows.Single(row => row!["Stage"]!.GetValue<string>() == "contained")!;
        Assert.Equal(1, contained["ContainedCount"]!.GetValue<long>());
        Assert.Equal(2, contained["ContainedInclusiveTicks"]!.GetValue<long>());
        Assert.Equal(2, contained["ContainedSelfTicks"]!.GetValue<long>());
        Assert.Equal("postaccepted-validation", contained["EngineCaller"]!.GetValue<string>());
        Assert.Equal("0123456789abcdef0123456789abcdef", contained["RequestId"]!.GetValue<string>());
        Assert.Equal("fedcba9876543210fedcba9876543210", contained["ActionId"]!.GetValue<string>());
        Assert.Equal("11111111111111111111111111111111", contained["Generation"]!.GetValue<string>());
        Assert.NotEqual("unbound", contained["WindowId"]!.GetValue<string>());
        Assert.Equal(contained["WindowId"]!.GetValue<string>(), contained["ProcessingAttemptId"]!.GetValue<string>());
        Assert.Null(contained["OwnerIdentity"]); // No actual original owner installed in this pure clock test.
        var crossing = rows.Single(row => row!["Stage"]!.GetValue<string>() == "crossing")!;
        Assert.Equal(1, crossing["CrossingCount"]!.GetValue<long>());
        Assert.Equal(4, crossing["CrossingInclusiveTicks"]!.GetValue<long>());
        Assert.Equal(0, crossing["ContainedInclusiveTicks"]!.GetValue<long>());
        Assert.Equal(1, rows.Single(row => row!["Stage"]!.GetValue<string>() == "late")!["LateCount"]!.GetValue<long>());
        var window = Assert.Single(result["Windows"]!.AsArray())!;
        Assert.Equal(0, window["StartedTicks"]!.GetValue<long>());
        Assert.Equal(6, window["AcceptedTicks"]!.GetValue<long>());
        Assert.Equal(12, window["ClosedTicks"]!.GetValue<long>());
        Assert.Equal("accepted", window["Outcome"]!.GetValue<string>());
        Assert.True(result["Complete"]!.GetValue<bool>());

        // Force an inherited branch to contend while the boundary clock is read.
        // It must not classify later work before that earlier boundary is published.
        VerifyBoundarySerialization(accepted: true);
        VerifyBoundarySerialization(accepted: false);

        void VerifyBoundarySerialization(bool accepted)
        {
            long concurrentTicks = 0;
            var readingBoundary = false;
            var boundaryThread = Environment.CurrentManagedThreadId;
            object? gate = null;
            Task? branch = null;
            bool? gateWasAvailable = null;
            using var examined = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            object? concurrentCollector = null;
            var concurrent = Create(() =>
            {
                if (readingBoundary && Environment.CurrentManagedThreadId == boundaryThread)
                {
                    branch = Task.Run(() =>
                    {
                        var entered = Monitor.TryEnter(gate!);
                        gateWasAvailable = entered;
                        if (entered) Monitor.Exit(gate!);
                        examined.Set();
                        if (!release.Wait(TimeSpan.FromSeconds(5)))
                            throw new TimeoutException("Boundary interleaving release missing.");
                        using (Measure(concurrentCollector!, "inherited-late")) { }
                        Invoke(concurrentCollector!, "Content", "late.json", "test", "physical-result", "missing", 0L);
                    });
                    if (!examined.Wait(TimeSpan.FromSeconds(5)))
                        throw new TimeoutException("Boundary interleaving branch missing.");
                    return 6;
                }
                return Volatile.Read(ref concurrentTicks);
            });
            concurrentCollector = concurrent;
            gate = Collector.GetField("_gate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(concurrent)!;
            using (Logical(concurrent, "MetaMiscStateFiles", "boundary-interleaving"))
            {
                var windowScope = Window(concurrent);
                try
                {
                    readingBoundary = true;
                    if (accepted) Invoke(concurrent, "Mark", "accepted-receipt-published");
                    else windowScope.Dispose();
                }
                finally
                {
                    readingBoundary = false;
                    Volatile.Write(ref concurrentTicks, 8);
                    release.Set();
                }
                branch!.GetAwaiter().GetResult();
                Volatile.Write(ref concurrentTicks, 10);
                windowScope.Dispose();
            }
            Assert.False(gateWasAvailable);
            var concurrentResult = Snapshot(concurrent);
            var lateRow = Assert.Single(concurrentResult["Rows"]!.AsArray())!;
            Assert.Equal(1, lateRow["LateCount"]!.GetValue<long>());
            Assert.Equal(0, lateRow["ContainedCount"]!.GetValue<long>());
            var content = Assert.Single(concurrentResult["ContentRows"]!.AsArray())!;
            Assert.Equal(1, content["AfterWindow"]!["PhysicalMissing"]!.GetValue<long>());
            Assert.Equal(0, content["InsideWindow"]!["PhysicalMissing"]?.GetValue<long>() ?? 0);
            Assert.Equal(0, concurrentResult["DiagnosticFailures"]!.GetValue<int>());
            Assert.Equal(accepted ? "accepted" : "incomplete",
                Assert.Single(concurrentResult["Windows"]!.AsArray())!["Outcome"]!.GetValue<string>());
            Assert.True(concurrentResult["Complete"]!.GetValue<bool>());
        }
    }

    [Fact]
    public async Task ContentCountersKeepMissingFollowupAndNullableDataFaultDistinct()
    {
        var collector = Create(() => 1);
        const string path = "game_state/control/life_transitions.json";
        using (Logical(collector, "MetaMiscStateFiles", "postaccepted-validation"))
        {
            Invoke(collector, "Content", path, "public-text-async", "semantic", "started", 0L);
            Invoke(collector, "Content", path, "public-text-async", "initial", "missing", 0L);
            Invoke(collector, "Content", path, "public-text-async", "followup-reason", "missing", 0L);
            Invoke(collector, "Content", path, "public-text-async", "followup", "present", 17L);
            Invoke(collector, "Content", path, "public-text-async", "semantic", "returned-present", 17L);
            Invoke(collector, "Content", path, "world-nullable-sync", "leaf", "data-fault", 0L);
        }
        var rows = Snapshot(collector)["ContentRows"]!.AsArray();
        Assert.Equal(2, rows.Count);
        var ordinary = rows.Single(row => row!["ReaderFamily"]!.GetValue<string>() == "public-text-async")!["OutsideWindow"]!;
        Assert.Equal(1, ordinary["SemanticStarts"]!.GetValue<long>());
        Assert.Equal(1, ordinary["InitialMissing"]!.GetValue<long>());
        Assert.Equal(1, ordinary["FollowupPresent"]!.GetValue<long>());
        var fault = rows.Single(row => row!["ReaderFamily"]!.GetValue<string>() == "world-nullable-sync")!;
        Assert.Equal(1, fault["OutsideWindow"]!["LeafDataFaults"]!.GetValue<long>());
        Assert.Equal(0, (fault["OutsideWindow"]!["LeafMissing"]?.GetValue<long>() ?? 0));
        Assert.All(rows, row => Assert.Equal("MetaMiscStateFiles", row!["ValidationPhase"]!.GetValue<string>()));
        Assert.Equal(0, Snapshot(collector)["Acquisitions"]!.GetValue<long>());

        // Actual public flow: the ORIGINAL after-attempt hook creates the absent
        // file, so the quiescence followup reads it. No fabricated authority/lease.
        var own = Path.Combine(Path.GetTempPath(), "boe-diag-content-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(own);
        try
        {
            var hookCalls = 0;
            FileSystemManager? files = null;
            files = new FileSystemManager(own, Microsoft.Extensions.Logging.Abstractions.NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                { AfterCanonicalReadAttemptAsync = relative =>
                    { hookCalls++; File.WriteAllText(files!.ResolvePath(relative), "actual followup"); return Task.CompletedTask; } });
            Directory.CreateDirectory(files.GameSessionPath);
            var wired = Create(System.Diagnostics.Stopwatch.GetTimestamp);
            typeof(FileSystemManager).GetField("_browserAdmissionDiagnostic", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(files, wired);
            Assert.Equal("actual followup", await files.ReadFileAsync("followup.txt"));
            Assert.Equal(1, hookCalls);
            var actual = Snapshot(wired)["ContentRows"]!.AsArray().Single(row => row!["ReaderFamily"]!.GetValue<string>() == "public-text-async")!["OutsideWindow"]!;
            Assert.Equal(1, actual["SemanticStarts"]!.GetValue<long>());
            Assert.Equal(1, actual["InitialMissing"]!.GetValue<long>());
            Assert.Equal(1, actual["FollowupReasonMissing"]!.GetValue<long>());
            Assert.Equal(1, actual["FollowupPresent"]!.GetValue<long>());
            Assert.Equal(1, actual["SemanticReturnedPresent"]!.GetValue<long>());
            Assert.Equal(0, (actual["SemanticFaults"]?.GetValue<long>() ?? 0));
            Assert.True(Snapshot(wired)["Complete"]!.GetValue<bool>());

            var original = new IOException("actual nullable data read failure");
            var nullableFiles = new FileSystemManager(own, Microsoft.Extensions.Logging.Abstractions.NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                { AfterCanonicalReadInitialValidationAsync = _ => Task.FromException(original) });
            var nullableCollector = Create(System.Diagnostics.Stopwatch.GetTimestamp);
            typeof(FileSystemManager).GetField("_browserAdmissionDiagnostic", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(nullableFiles, nullableCollector);
            var reader = typeof(FileSystemManager).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(method => method.Name == "ReadOrdinaryFileSnapshotCore" && method.GetParameters().Length == 4);
            object?[] arguments = ["followup.txt", null, null, true];
            Assert.Null(reader.Invoke(nullableFiles, arguments));
            Assert.Same(original, Assert.IsType<System.Runtime.ExceptionServices.ExceptionDispatchInfo>(arguments[2]).SourceException);
            var data = Snapshot(nullableCollector)["ContentRows"]!.AsArray().Single()!["OutsideWindow"]!;
            Assert.Equal(1, data["PhysicalDataFaults"]!.GetValue<long>());
            Assert.Equal(0, (data["PhysicalMissing"]?.GetValue<long>() ?? 0));
        }
        finally { Directory.Delete(own, true); }
    }

    [Fact]
    public void FailedAndWindowLimitedCaptureCannotClaimCompleteProcessing()
    {
        var collector = Create(() => 1);
        using (Window(collector))
        {
            Assert.False(Snapshot(collector)["Complete"]!.GetValue<bool>());
            Invoke(collector, "Mark", "finalizer-failed");
        }
        var first = Assert.Single(Snapshot(collector)["Windows"]!.AsArray())!;
        Assert.Equal("failed", first["Outcome"]!.GetValue<string>());
        Assert.Null(first["AcceptedTicks"]);
        for (var i = 0; i < 8; i++) using (Window(collector)) { }
        var result = Snapshot(collector);
        Assert.Equal(8, result["Windows"]!.AsArray().Count);
        Assert.Equal(1, result["DroppedWindows"]!.GetValue<int>());
        Assert.False(result["Complete"]!.GetValue<bool>());

        var limited = Create(() => 1, 1);
        using (Measure(limited, "occupies-shared-row")) { }
        Invoke(limited, "Content", "missing.json", "ordinary-sync", "physical-result", "missing", 0L);
        Assert.Equal(1, Snapshot(limited)["DroppedContentRows"]!.GetValue<int>());
        Assert.False(Snapshot(limited)["Complete"]!.GetValue<bool>());

        var open = Create(() => 1);
        Invoke(open, "Content", "read.json", "public-text-async", "semantic", "started", 0L);
        Assert.False(Snapshot(open)["Complete"]!.GetValue<bool>());
        Invoke(open, "Content", "read.json", "public-text-async", "semantic", "fault", 0L);
        using (Window(open)) { }
        Assert.Equal("incomplete", Assert.Single(Snapshot(open)["Windows"]!.AsArray())!["Outcome"]!.GetValue<string>());

        var throwClock = false;
        var closing = Create(() => throwClock ? throw new IOException("diagnostic clock") : 1);
        var original = new InvalidOperationException("original failure during close");
        Assert.Same(original, Record.Exception((Action)(() =>
        { using (Window(closing)) { throwClock = true; throw original; } })));
        throwClock = false;
        using (Measure(closing, "after-failed-close")) { }
        Assert.Equal("unbound", Assert.Single(Snapshot(closing)["Rows"]!.AsArray())!["WindowId"]!.GetValue<string>());
        Assert.Equal(1, Snapshot(closing)["DiagnosticFailures"]!.GetValue<int>());
    }

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
            var files = new FileSystemManager(root, Microsoft.Extensions.Logging.Abstractions.NullLogger<FileSystemManager>.Instance);
            typeof(FileSystemManager).GetField("_browserAdmissionDiagnostic", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(files, null);
            var logical = (Func<string?, string?, IDisposable?>)typeof(FileSystemManager)
                .GetMethod("BrowserDiagnosticLogical", BindingFlags.Instance | BindingFlags.NonPublic)!
                .CreateDelegate(typeof(Func<string?, string?, IDisposable?>), files);
            var window = (Func<string, string, string, IDisposable?>)typeof(FileSystemManager)
                .GetMethod("BrowserDiagnosticWindow", BindingFlags.Instance | BindingFlags.NonPublic)!
                .CreateDelegate(typeof(Func<string, string, string, IDisposable?>), files);
            var mark = (Action<string>)typeof(FileSystemManager)
                .GetMethod("BrowserDiagnosticMark", BindingFlags.Instance | BindingFlags.NonPublic)!
                .CreateDelegate(typeof(Action<string>), files);
            // Warm the actual null-conditional filesystem surface before the finite
            // allocation observation; reflection itself is outside this observation.
            Assert.Null(logical("MetaMiscStateFiles", "disabled"));
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++)
            {
                using var labelScope = logical("MetaMiscStateFiles", "disabled");
                using var windowScope = window("request", "action", "generation");
                mark("processing-owner");
            }
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
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
            using (Measure(collector, "after-export-excluded")) { }
            var sealedSnapshot = Snapshot(collector);
            Assert.True(sealedSnapshot["CaptureSealed"]!.GetValue<bool>());
            Assert.Single(sealedSnapshot["Rows"]!.AsArray());
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
        var beforeUnlabeledBytes = GC.GetAllocatedBytesForCurrentThread();
        using (Context(collector, "other"))
            for (var i = 0; i < repetitions; i++) using (Measure(collector, "calibration")) { }
        var diagnosticSeconds = watch.Elapsed.TotalSeconds;
        var unlabeledBytes = GC.GetAllocatedBytesForCurrentThread() - beforeUnlabeledBytes;
        watch.Restart();
        var beforeLabeledBytes = GC.GetAllocatedBytesForCurrentThread();
        using (Context(collector, "other"))
            for (var i = 0; i < repetitions; i++)
                using (Logical(collector, "calibration", "collector-control"))
                using (Measure(collector, "calibration-labeled"))
                    Invoke(collector, "Content", "calibration.json", "ordinary-sync", "physical-result", "missing", 0L);
        var labeledSeconds = watch.Elapsed.TotalSeconds;
        var labeledBytes = GC.GetAllocatedBytesForCurrentThread() - beforeLabeledBytes;
        var result = Snapshot(collector);
        Assert.Equal(2, result["Rows"]!.AsArray().Count);
        Assert.All(result["Rows"]!.AsArray(), row => Assert.Equal(repetitions, row!["Count"]!.GetValue<long>()));
        Assert.Equal(repetitions, Assert.Single(result["ContentRows"]!.AsArray())!["OutsideWindow"]!["PhysicalMissing"]!.GetValue<long>());
        output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { Repetitions = repetitions, EmptySeconds = emptySeconds,
            UnlabeledSeconds = diagnosticSeconds, LabeledSeconds = labeledSeconds, UnlabeledManagedBytes = unlabeledBytes,
            LabeledManagedBytes = labeledBytes,
            Scope = "Finite in-memory control including test reflection harness overhead; label/content/window-free loop observations, not isolated collector cost or end-to-end IO/acceptance overhead. Do not subtract as exact calibrated production overhead." }));
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
