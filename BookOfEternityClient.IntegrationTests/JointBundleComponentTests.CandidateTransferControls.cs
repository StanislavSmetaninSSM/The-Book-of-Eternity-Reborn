using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    /// <summary>Preserves exact phase snapshots and all source rows while one continuing transfer interleaves with repeated boundaries.</summary>
    /// <returns>Completion after two independent actual fixtures and both transfer workers have closed.</returns>
    [Fact]
    public async Task ComponentTransfer_BoundariesKeepOneOrderedQueueAndMarkerSnapshot()
    {
        var first = new Fixture(_output, nameof(ComponentTransfer_BoundariesKeepOneOrderedQueueAndMarkerSnapshot) + "-first",
            passiveTimingEvents: true, gzipRecords: true, continuingPassiveTimings: true);
        Fixture? second = null;
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? body = null;
        var firstStart = -1;
        await using (first)
        {
            second = new Fixture(_output, nameof(ComponentTransfer_BoundariesKeepOneOrderedQueueAndMarkerSnapshot) + "-second",
                passiveTimingEvents: true, gzipRecords: true, continuingPassiveTimings: true);
            await using (second)
            {
                first.EmitTransferControlScope("T608.Transfer.FirstBefore");
                second.EmitTransferControlScope("T608.Transfer.SecondBefore");
                firstStart = first.ResetCandidateCapture("T608.Transfer.First");
                var secondStart = second.ResetCandidateCapture("T608.Transfer.Second");
                try
                {
                    body = Task.Run(() =>
                    {
                        using (first.Files.EnterOperationTimingContext())
                        using (var scope = first.Files.MeasureOperation("T608.Transfer.FirstSubject"))
                        {
                            Assert.NotNull(scope); entered.SetResult(); release.Wait();
                        }
                    });
                    var signaled = await Task.WhenAny(entered.Task, body); await signaled;
                    Assert.True(entered.Task.IsCompletedSuccessfully); Assert.False(body.IsCompleted);
                    var began = Stopwatch.GetTimestamp(); var visible = false;
                    do
                    {
                        var (_, rows, _) = ReadCandidateVisibilityPrefix(first.Output.RecordsPath);
                        visible = rows.Any(row => row.TryGetProperty("Operation", out var name) &&
                            name.GetString() == "T608.Transfer.FirstSubject" && row.GetProperty("State").GetString() == "begin");
                        if (!visible) await Task.Delay(20);
                    } while (!visible && Stopwatch.GetElapsedTime(began) < TimeSpan.FromSeconds(2));
                    Assert.True(visible); Assert.False(body.IsCompleted);
                    // The consumer already published the actual begin; repeated barriers must neither borrow nor duplicate it.
                    Assert.Equal(3, first.CaptureObservationOrdinal()); Assert.Equal(3, first.CaptureObservationOrdinal());
                    second.EmitTransferControlScope("T608.Transfer.SecondSubject");
                    var secondCounts = second.CompleteCandidateCapture("T608.Transfer.Second", secondStart);
                    Assert.Single(secondCounts); Assert.Equal(1, secondCounts["T608.Transfer.SecondSubject"]);
                    Assert.Equal(4, second.CaptureObservationOrdinal()); Assert.Equal(4, second.CaptureObservationOrdinal());
                    Assert.Equal(3, first.CaptureObservationOrdinal());
                }
                finally
                {
                    release.Set();
                    if (body is not null) await body;
                }
                var firstCounts = first.CompleteCandidateCapture("T608.Transfer.First", firstStart);
                Assert.Single(firstCounts); Assert.Equal(1, firstCounts["T608.Transfer.FirstSubject"]);
                Assert.Equal(4, first.CaptureObservationOrdinal()); Assert.Equal(4, first.CaptureObservationOrdinal());
                first.EmitTransferControlScope("T608.Transfer.FirstTail");
                second.EmitTransferControlScope("T608.Transfer.SecondTail");
                first.RequireCandidateVisibilityHasNoAuthority(); second.RequireCandidateVisibilityHasNoAuthority();
            }
        }
        Assert.NotNull(second); Assert.Equal(2, firstStart);
        var (firstReport, firstEvents) = ReadTransferControlCapture(first,
            ["FirstBefore", "FirstSubject", "FirstTail"], diagnosticComplete: true);
        var (secondReport, secondEvents) = ReadTransferControlCapture(second,
            ["SecondBefore", "SecondSubject", "SecondTail"], diagnosticComplete: true);
        AssertTransferPhase(first, firstEvents, "T608.Transfer.First", "T608.Transfer.FirstSubject");
        AssertTransferPhase(second, secondEvents, "T608.Transfer.Second", "T608.Transfer.SecondSubject");
        Assert.NotEqual(first.Output.ExpectedOwnedRoot, second.Output.ExpectedOwnedRoot);
        Assert.DoesNotContain(firstEvents, row => row.GetProperty("Operation").GetString()!.Contains("Second", StringComparison.Ordinal));
        Assert.DoesNotContain(secondEvents, row => row.GetProperty("Operation").GetString()!.Contains("First", StringComparison.Ordinal));
        var firstJoin = first.SettleClosedTransferForControl(); var firstJoinAgain = first.SettleClosedTransferForControl();
        Assert.Same(firstJoin, firstJoinAgain); var receipt = await firstJoin; Assert.NotNull(receipt);
        Assert.False(receipt.ContextSentinelPresent); Assert.True(receipt.Batches > 0);
        var secondJoin = second.SettleClosedTransferForControl(); Assert.Same(secondJoin, second.SettleClosedTransferForControl());
        Assert.NotNull(await secondJoin);
        WriteTransferControlOracle(first, nameof(ComponentTransfer_BoundariesKeepOneOrderedQueueAndMarkerSnapshot), new
        {
            IndependentSecondReport = second.Output.ReportPath, FirstRows = firstEvents, SecondRows = secondEvents,
            RepeatedBoundariesExact = true, IndependentQueues = true, SharedTransferCompletion = true,
            FirstEvents = firstReport.GetProperty("TimingStored").GetInt64(), SecondEvents = secondReport.GetProperty("TimingStored").GetInt64()
        });
    }

    /// <summary>Defers the existing lost-detached-batch fault while retaining actual final tail and the component's original closing exception policy.</summary>
    /// <returns>Completion after actual transfer settlement, incomplete diagnostic close and honest retained-root receipt.</returns>
    [Fact]
    public async Task ComponentTransfer_FaultAndFinalTailKeepExistingClosePolicy()
    {
        var fixture = new Fixture(_output, nameof(ComponentTransfer_FaultAndFinalTailKeepExistingClosePolicy),
            passiveTimingEvents: true, gzipRecords: true, continuingPassiveTimings: true,
            passiveTransferFaultForControl: LifecycleTimingFaultForControl.DrainAfterDetach);
        var sentinel = new InvalidOperationException("T608 component body sentinel; existing diagnostic close may override it.");
        Exception? diagnosticFault = null; Exception? caught = null; var bodyReachedSentinel = false;
        try
        {
            await using (fixture)
            {
                fixture.EmitTransferFaultBatchForControl();
                var began = Stopwatch.GetTimestamp();
                do
                {
                    diagnosticFault = fixture.Output.Timings.GetFailures().SingleOrDefault(row =>
                        row.Stage == "lifecycle-continuing-drain").Error;
                    if (diagnosticFault is null) await Task.Delay(20);
                } while (diagnosticFault is null && Stopwatch.GetElapsedTime(began) < TimeSpan.FromSeconds(2));
                Assert.IsType<IOException>(diagnosticFault);
                Assert.Equal("Injected lifecycle drain failure after actual event detach.", diagnosticFault!.Message);
                fixture.RequireCandidateVisibilityHasNoAuthority();
                fixture.EmitTransferControlScope("T608.Transfer.FinalTailAfterFault");
                // This file-only contour has no functional resources; close the real window before joining its actual worker.
                fixture.CloseControlWindowForControl();
                var requests = new[]
                {
                    Task.Factory.StartNew(fixture.SettleClosedTransferForControl, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default),
                    Task.Factory.StartNew(fixture.SettleClosedTransferForControl, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default)
                };
                var joins = await Task.WhenAll(requests); Assert.Same(joins[0], joins[1]);
                var receipts = await Task.WhenAll(joins);
                Assert.NotNull(receipts[0]); Assert.Same(receipts[0], receipts[1]);
                Assert.Equal(2, receipts[0]!.Batches); Assert.Equal(2, receipts[0]!.LargestBatch);
                bodyReachedSentinel = true;
                throw sentinel;
            }
        }
        catch (Exception error) { caught = error; }
        Assert.True(bodyReachedSentinel); Assert.NotNull(diagnosticFault);
        var closingFailure = Assert.IsType<AggregateException>(caught);
        Assert.StartsWith("Component closing failed; inspect the actual root and diagnostic receipts.", closingFailure.Message);
        Assert.NotSame(sentinel, closingFailure);
        Assert.Contains(closingFailure.Flatten().InnerExceptions, error => ReferenceEquals(error, diagnosticFault));
        var (report, events) = ReadTransferControlCapture(fixture, ["FinalTailAfterFault"], diagnosticComplete: false);
        Assert.Equal(2, report.GetProperty("TimingStored").GetInt64());
        Assert.False(report.GetProperty("PassiveTimingCompletion").GetProperty("Complete").GetBoolean());
        var completion = report.GetProperty("PassiveTimingCompletion"); var status = completion.GetProperty("Events");
        foreach (var name in new[] { "Attempted", "Enqueued", "Drained" }) Assert.Equal(4, status.GetProperty(name).GetInt64());
        Assert.Equal(0, status.GetProperty("Pending").GetInt64()); Assert.Equal(0, status.GetProperty("Failed").GetInt64());
        Assert.Equal(4, completion.GetProperty("ExpectedRecords").GetInt64());
        Assert.Equal(2, completion.GetProperty("TransportStored").GetInt64());
        Assert.True(completion.GetProperty("Snapshot").GetProperty("Complete").GetBoolean());
        Assert.Equal(new long[] { 2, 2 }, events.Select(row => row.GetProperty("ScopeId").GetInt64()));
        Assert.Contains(report.GetProperty("Failures").EnumerateArray(), row => row.GetProperty("Stage").GetString() == "lifecycle-continuing-drain");
        Assert.Same(closingFailure, await Record.ExceptionAsync(() => fixture.DisposeAsync().AsTask()));
        WriteTransferControlOracle(fixture, nameof(ComponentTransfer_FaultAndFinalTailKeepExistingClosePolicy), new
        {
            ActualLostBatch = 2, ActualFinalTail = events, bodyReachedSentinel, ExistingComponentClosingAggregate = true,
            OriginalBodyNotClaimedPrimary = true, SharedTransferCompletion = true, RootIntentionallyRetained = true
        });
    }

    /// <summary>Checks the actual small closed sink, observation queue and root/error policy for these two finite controls.</summary>
    /// <param name="fixture">The actual already closed or intentionally retained component fixture.</param>
    /// <param name="operationSuffixes">Independent literal expected actual stored operation names.</param>
    /// <param name="diagnosticComplete">Whether the original source-to-writer ledger should be complete.</param>
    /// <returns>Detached original report/event DATA, never authority.</returns>
    private static (JsonElement Report, JsonElement[] Events) ReadTransferControlCapture(Fixture fixture,
        string[] operationSuffixes, bool diagnosticComplete)
    {
        fixture.RequireCandidateVisibilityHasNoAuthority(); var output = fixture.Output;
        using var document = JsonDocument.Parse(File.ReadAllText(output.ReportPath)); var report = document.RootElement.Clone();
        Assert.Equal(diagnosticComplete, report.GetProperty("DiagnosticComplete").GetBoolean());
        Assert.True(report.GetProperty("WriterClosed").GetBoolean()); Assert.True(report.GetProperty("GzipFooterClosed").GetBoolean());
        var closing = report.GetProperty("FixtureClosing"); Assert.Equal(output.ExpectedOwnedRoot, closing.GetProperty("Owned").GetString());
        Assert.Equal(diagnosticComplete, closing.GetProperty("Closing").GetBoolean());
        Assert.Equal(!diagnosticComplete, closing.GetProperty("Retained").GetBoolean());
        if (diagnosticComplete)
        {
            Assert.Empty(report.GetProperty("Failures").EnumerateArray()); Assert.Empty(closing.GetProperty("Failures").EnumerateArray());
            Assert.Throws<FileNotFoundException>(() => File.GetAttributes(output.ExpectedOwnedRoot));
        }
        else
        {
            Assert.NotEmpty(report.GetProperty("Failures").EnumerateArray()); Assert.NotEmpty(closing.GetProperty("Failures").EnumerateArray());
            Assert.True((File.GetAttributes(output.ExpectedOwnedRoot) & FileAttributes.Directory) != 0);
            Assert.Equal(output.FixtureNonce, File.ReadAllText(Path.Combine(output.ExpectedOwnedRoot, ".fixture-owner")));
        }
        var stored = File.ReadAllBytes(output.RecordsPath); var decoded = ReadClosedCopyRangeRecords(output.RecordsPath, gzip: true);
        var (rows, trailing) = ReadCopyTailRows(decoded); Assert.Equal(0, trailing);
        var storedHash = Convert.ToHexString(SHA256.HashData(stored)); var decodedHash = Convert.ToHexString(SHA256.HashData(decoded));
        Assert.Equal(stored.LongLength, report.GetProperty("StoredBytes").GetInt64()); Assert.Equal(storedHash, report.GetProperty("Sha256").GetString());
        Assert.Equal(decoded.LongLength, report.GetProperty("DecodedBytes").GetInt64()); Assert.Equal(decodedHash, report.GetProperty("DecodedSha256").GetString());
        Assert.Equal(rows.LongLength, report.GetProperty("DecodedRows").GetInt64());
        JointBundleDiagnosticOutput.ValidateClosedGzipIdentity(output.RecordsPath, stored.LongLength, storedHash, decoded.LongLength, decodedHash);
        var events = rows.Where(row => row.TryGetProperty("Boundary", out var boundary) && boundary.GetString() == "joint_operation_timing").ToArray();
        var names = operationSuffixes.SelectMany(name => new[] { "T608.Transfer." + name + "|begin", "T608.Transfer." + name + "|end" }).ToArray();
        Assert.Equal(names, events.Select(CopyTailEventName));
        var observations = fixture.CaptureOwnedOrdinaryPhaseRows(); Assert.Equal(names, observations.Select(row => row.Name + "|" + row.Boundary));
        Assert.Equal(observations.Select(row => row.SourceScopeId), events.Select(row => row.GetProperty("ScopeId").GetInt64()));
        Assert.Equal(observations.Select(row => row.Milliseconds), events.Select(row => row.GetProperty("Milliseconds").GetDouble()));
        Assert.Equal(events.LongLength, report.GetProperty("TimingAttempted").GetInt64()); Assert.Equal(events.LongLength, report.GetProperty("TimingStored").GetInt64());
        Assert.Equal(rows.Length - events.Length, report.GetProperty("OutputStored").GetInt64());
        Assert.Equal(report.GetProperty("OutputAttempted").GetInt64(), report.GetProperty("OutputStored").GetInt64());
        var fields = new[] { "Boundary", "Operation", "State", "Milliseconds", "Sequence", "PID", "TotalElapsedMilliseconds",
            "Transport", "PassiveSequence", "ScopeId", "TimestampTicks", "ElapsedTicks", "Late", "ObserverCompletedBefore",
            "ObserverGateMillisecondsBefore", "ObserverBuildSerializeMillisecondsBefore", "ObserverWriteMillisecondsBefore" };
        double[] previousCosts = [0, 0, 0];
        for (var i = 0; i < events.Length; i++)
        {
            var row = events[i]; Assert.Equal(fields, row.EnumerateObject().Select(field => field.Name)); Assert.Equal(i + 1, row.GetProperty("Sequence").GetInt64());
            Assert.Equal(i + 1 + (diagnosticComplete ? 0 : 2), row.GetProperty("PassiveSequence").GetInt64()); Assert.Equal(i, row.GetProperty("ObserverCompletedBefore").GetInt64());
            Assert.Equal(Environment.ProcessId, row.GetProperty("PID").GetInt32()); Assert.Equal("sealed-passive-events-v1", row.GetProperty("Transport").GetString());
            Assert.False(row.GetProperty("Late").GetBoolean()); var ticks = row.GetProperty("ElapsedTicks").GetInt64();
            Assert.True(ticks >= 0); Assert.True(row.GetProperty("TimestampTicks").GetInt64() > 0);
            Assert.Equal(ticks * 1000d / Stopwatch.Frequency, row.GetProperty("Milliseconds").GetDouble());
            if (i % 2 == 0) Assert.Equal(0, ticks);
            else
            {
                Assert.Equal(events[i - 1].GetProperty("ScopeId").GetInt64(), row.GetProperty("ScopeId").GetInt64());
                Assert.Equal(row.GetProperty("TimestampTicks").GetInt64() - events[i - 1].GetProperty("TimestampTicks").GetInt64(), ticks);
            }
            for (var j = 0; j < previousCosts.Length; j++)
            {
                var cost = row.GetProperty(fields[14 + j]).GetDouble(); Assert.True(double.IsFinite(cost) && cost >= previousCosts[j]); previousCosts[j] = cost;
            }
        }
        var completion = report.GetProperty("PassiveTimingCompletion"); var status = completion.GetProperty("Events");
        Assert.Equal(diagnosticComplete, completion.GetProperty("Complete").GetBoolean());
        Assert.True(status.GetProperty("Enabled").GetBoolean()); Assert.Equal(0, status.GetProperty("Pending").GetInt64());
        Assert.Equal(0, status.GetProperty("Failed").GetInt64());
        var snapshot = completion.GetProperty("Snapshot"); Assert.True(snapshot.GetProperty("Closed").GetBoolean());
        Assert.True(snapshot.GetProperty("Complete").GetBoolean()); Assert.Equal(0, snapshot.GetProperty("OpenScopes").GetInt64());
        Assert.Equal(0, snapshot.GetProperty("LateCompletions").GetInt64());
        if (diagnosticComplete)
        {
            foreach (var name in new[] { "Attempted", "Enqueued", "Drained" }) Assert.Equal(events.LongLength, status.GetProperty(name).GetInt64());
            foreach (var name in new[] { "ExpectedRecords", "TransportAttempted", "TransportStored" }) Assert.Equal(events.LongLength, completion.GetProperty(name).GetInt64());
        }
        var counters = snapshot.GetProperty("Rows").EnumerateArray().ToArray();
        foreach (var group in events.GroupBy(row => row.GetProperty("Operation").GetString()))
        {
            var counter = Assert.Single(counters, item => item.GetProperty("Name").GetString() == group.Key);
            var ended = Assert.Single(group, item => item.GetProperty("State").GetString() == "end");
            Assert.Equal(1, counter.GetProperty("Started").GetInt64()); Assert.Equal(1, counter.GetProperty("Completed").GetInt64());
            Assert.Equal(ended.GetProperty("Milliseconds").GetDouble(), counter.GetProperty("InclusiveMilliseconds").GetDouble());
            Assert.Equal(ended.GetProperty("Milliseconds").GetDouble(), counter.GetProperty("MaxMilliseconds").GetDouble());
        }
        var costs = report.GetProperty("TimingWriteCosts").EnumerateArray().ToArray(); Assert.Equal(events.Length, costs.Length);
        foreach (var cost in costs)
        {
            Assert.Single(events, row => row.GetProperty("Operation").GetString() == cost.GetProperty("Operation").GetString() && row.GetProperty("State").GetString() == cost.GetProperty("State").GetString());
            foreach (var name in new[] { "Attempted", "Stored", "Measured" }) Assert.Equal(1, cost.GetProperty(name).GetInt64());
        }
        var flush = report.GetProperty("FlushCosts"); Assert.True(flush.GetProperty("Attempted").GetInt64() > 0);
        Assert.Equal(flush.GetProperty("Attempted").GetInt64(), flush.GetProperty("Stored").GetInt64());
        return (report, events);
    }

    /// <summary>Checks exact original reset/completion markers against one actual retained subject pair and duration.</summary>
    /// <param name="fixture">The actual successful already closed component.</param>
    /// <param name="events">Every original stored timing event from that component.</param>
    /// <param name="phase">The literal declared phase.</param>
    /// <param name="operation">The actual subject operation inside it.</param>
    private static void AssertTransferPhase(Fixture fixture, JsonElement[] events, string phase, string operation)
    {
        var (rows, trailing) = ReadCopyTailRows(ReadClosedCopyRangeRecords(fixture.Output.RecordsPath, gzip: true)); Assert.Equal(0, trailing);
        var reset = Assert.Single(rows, row => row.TryGetProperty("Kind", out var kind) && kind.GetString() == "CandidateCaptureReset");
        var completed = Assert.Single(rows, row => row.TryGetProperty("Kind", out var kind) && kind.GetString() == "CandidateCaptureCompleted");
        Assert.Equal(phase, reset.GetProperty("Phase").GetString()); Assert.Equal(2, reset.GetProperty("Offset").GetInt32());
        Assert.Equal(phase, completed.GetProperty("Phase").GetString()); Assert.Equal(2, completed.GetProperty("Offset").GetInt32());
        Assert.Equal(4, completed.GetProperty("EndOffset").GetInt32());
        var actual = Assert.Single(completed.GetProperty("Operations").EnumerateArray()); Assert.Equal(operation, actual.GetProperty("Name").GetString());
        Assert.Equal(1, actual.GetProperty("Count").GetInt32()); Assert.Equal(events[3].GetProperty("Milliseconds").GetDouble(), actual.GetProperty("InclusiveMilliseconds").GetDouble());
        Assert.Equal(operation, events[2].GetProperty("Operation").GetString()); Assert.Equal(operation, events[3].GetProperty("Operation").GetString());
    }

    /// <summary>Writes a compact detached comparison receipt beside original raw after the existing fixture close policy.</summary>
    /// <param name="fixture">The already closed actual fixture.</param>
    /// <param name="control">The actual test method.</param>
    /// <param name="data">Only comparison DATA, never a callback or authority.</param>
    private void WriteTransferControlOracle(Fixture fixture, string control, object data)
    {
        var path = Path.Combine(fixture.Output.DiagnosticDirectory, "component-transfer-control-oracle.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new { Kind = "T608ComponentTransferControl", Control = control,
            ReportPath = fixture.Output.ReportPath, RecordsPath = fixture.Output.RecordsPath, Data = data,
            ActualGenuineAuthorityClaim = false }, new JsonSerializerOptions { WriteIndented = true }));
        _output.WriteLine(JsonSerializer.Serialize(new { Kind = "T608ComponentTransferControlReference", Control = control, Path = path }));
    }

    private sealed partial class Fixture
    {
        /// <summary>Emits one actual explicitly test-labelled synchronous scope without adding a product timing name.</summary>
        /// <param name="name">A literal T608.Transfer control name.</param>
        internal void EmitTransferControlScope(string name)
        {
            RequireCandidateVisibilityHasNoAuthority(); Assert.StartsWith("T608.Transfer.", name);
            using (Files.EnterOperationTimingContext())
            using (var scope = Files.MeasureOperation(name)) Assert.NotNull(scope);
        }

        /// <summary>Queues one complete real source pair under the diagnostic gate, so the existing consumer fault loses exactly that batch.</summary>
        internal void EmitTransferFaultBatchForControl()
        {
            Assert.NotNull(_passiveTransfer);
            lock (_passiveTransfer.BoundaryGate) EmitTransferControlScope("T608.Transfer.LostSubject");
        }

        /// <summary>Closes this no-resource actual control window before settlement; never closes a borrowed functional capability.</summary>
        internal void CloseControlWindowForControl()
        {
            RequireCandidateVisibilityHasNoAuthority(); Assert.NotNull(_passiveWindow);
            _passiveWindow.Dispose();
        }

        /// <summary>Accesses only the existing completion Task after the actual window has closed.</summary>
        /// <returns>One shared worker-join result; no whole-fixture concurrent-disposal guarantee.</returns>
        internal Task<LifecyclePassiveTimingDiagnostics.DrainReceipt?> SettleClosedTransferForControl()
        {
            Assert.NotNull(_passiveTransfer); Assert.NotNull(_passiveWindow); Assert.True(_passiveWindow.Capture().Closed);
            return _passiveTransfer.StopAndJoinAsync();
        }
    }
}
