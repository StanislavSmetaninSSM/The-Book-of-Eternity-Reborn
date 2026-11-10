using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises the real history consumer on independent observed DATA without constructing a game engine.
/// </summary>
public sealed class JointHistoryConsumerComponentTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Selects the receiver of references to the complete external component evidence.
    /// </summary>
    /// <param name="output">
    /// The current case's output receiver; full diagnostics remain in the external artifacts.
    /// </param>
    public JointHistoryConsumerComponentTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Validates actual story history or refuses a pending wave introduced only inside its continuation binder.
    /// </summary>
    /// <param name="transientPendingWave">
    /// Whether to insert an absent pending wave at the first consumer binder and restore it at that binder's end.
    /// </param>
    /// <returns>
    /// Completion after the exact expected consumer outcome, unchanged DATA and confirmed local closing.
    /// Unexpected failures retain the owned copy and complete available diagnostics.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualCleanupData_ValidatesHistoryOrRefusesTransientPendingWave(bool transientPendingWave)
    {
        var id = Guid.NewGuid().ToString("N");
        var directory = Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "joint-history-consumer");
        Directory.CreateDirectory(directory);
        var diagnosticPath = Path.Combine(directory, id + ".jsonl");
        var reportPath = diagnosticPath + ".report.json";
        var owned = Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "joint-history-owned", id);
        var root = Path.Combine(owned, "copy");
        var report = new Dictionary<string, object?>
        {
            ["Kind"] = "joint_history_consumer_component_v1", ["Status"] = "in_progress",
            ["Path"] = diagnosticPath, ["OwnedRoot"] = owned, ["FixtureRoot"] = root,
            ["TransientPendingWave"] = transientPendingWave,
            ["ArchiveSHA256"] = JointCleanupValidatorTestData.ArchiveSha256,
            ["GameEngineConstructed"] = false, ["Source42OrStage43Prepared"] = false,
            ["HistoricalOwnerRecreated"] = false, ["StartedUtc"] = DateTime.UtcNow.ToString("O")
        };
        // Register root and artifact identity before any operation that the external runner can interrupt.
        WriteReport(reportPath, report);
        var failures = new List<string>();
        Exception? failure = null;
        Exception? consumerFailure = null;
        List<ValidationIssue>? issues = null;
        FileSystemManager? files = null;
        TextWriter? raw = null;
        JointCleanupProfileDiagnosticCapture? capture = null;
        ConsumerObservation? observation = null;
        Dictionary<string, string>? before = null;
        string[]? directoriesBefore = null;
        byte[]? generationBefore = null;
        string? generation = null;
        var outerAttempted = false;
        var outerAwaitSettled = false;
        var outerCompleted = false;
        var afterconditionClosed = false;
        var outcomeVerified = false;
        var dataUnchanged = false;
        var writerClosed = false;
        object? remoteClose = null;
        var remoteCallbackInvoked = false;
        var remoteCloseObserved = false;
        var wasRemote = false;
        long? outerClosingStarted = null;
        TimeSpan? outerClosingCpu = null;
        var mergedDiagnostics = 0;
        void RecordFailure(Exception error, string stage)
        {
            failures.Add(stage + ": " + error);
            failure = JointInterfaceCrashProcessSupport.PreserveFailure(failure, error, stage);
        }
        void Attempt(Action operation, string stage)
        {
            try { operation(); }
            catch (Exception error) { RecordFailure(error, stage); }
        }
        void MergeDiagnostics()
        {
            if (capture is null) return;
            var deferred = capture.GetFailures();
            for (; mergedDiagnostics < deferred.Length; mergedDiagnostics++)
                RecordFailure(deferred[mergedDiagnostics].Error, deferred[mergedDiagnostics].Stage);
        }
        using var process = Process.GetCurrentProcess();
        try
        {
            raw = TextWriter.Synchronized(new StreamWriter(
                new FileStream(diagnosticPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read),
                new UTF8Encoding(false)) { AutoFlush = true });
            raw.WriteLine(JsonSerializer.Serialize(report));
            capture = new JointCleanupProfileDiagnosticCapture(raw);
            var materializeClock = Stopwatch.StartNew();
            var materializeCpu = process.TotalProcessorTime;
            raw.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_history_consumer_stage",
                Operation = "HistoryConsumer.MaterializeObservedData", Phase = "begin" }));
            try { JointLateTerminalFixture.MaterializeObservedCleanupData(owned); }
            finally
            {
                try { WriteCost(raw, "HistoryConsumer.MaterializeObservedData", materializeClock.Elapsed,
                    process.TotalProcessorTime - materializeCpu); }
                catch (Exception error) { capture.RecordDiagnosticFailure(error, "materialize-cost-report"); }
            }
            before = JointCleanupValidatorTestData.CaptureImages(root);
            directoriesBefore = JointCleanupValidatorTestData.CaptureDirectories(root);
            report["BeforeGameImages"] = before;
            report["BeforeGameDirectories"] = directoriesBefore;
            observation = new ConsumerObservation(root, raw, capture, transientPendingWave);
            files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance,
                new FileSystemManagerHooks { OperationTimingObserver = observation.Observe });
            generationBefore = File.ReadAllBytes(files.SessionGenerationPath);
            report["GenerationBeforeBase64"] = Convert.ToBase64String(generationBefore);
            await using (var generationLease = await files.AcquireCanonicalWriteLeaseAsync())
                generation = files.ReadExistingSessionGeneration(generationLease)
                    ?? throw new InvalidDataException("Observed cleanup DATA has no existing generation.");
            report["ExpectedGeneration"] = generation;
            WriteReport(reportPath, report);
            outerAttempted = true;
            await JointCleanupValidatorProfileTests.MeasureAsync(files, raw, capture, "HistoryConsumer.OuterExpectedSession", async () =>
            {
                try
                {
                    var result = await SessionOperationContext.RunParticipatingExpectedSessionAsync(files, generation, async () =>
                    {
                        try
                        {
                            var story = new StoryService(files,
                                new JointComponentDiagnosticLogger<StoryService>(raw, capture, "joint_history_consumer_log"));
                            var validator = new ValidationService(files,
                                new JointComponentDiagnosticLogger<ValidationService>(raw, capture, "joint_history_consumer_log"));
                            var issued = await JointCleanupValidatorProfileTests.MeasureAsync(files, raw, capture,
                                "HistoryConsumer.RealIssuance", async () =>
                                {
                                    await using var initialLease = await files.AcquireCanonicalWriteLeaseAsync();
                                    Assert.Equal(generation, files.ReadExistingSessionGeneration(initialLease));
                                    var original = MortalQteWoundOpportunityProjection.ReadSnapshot(files, initialLease);
                                    var manifest = await JointCleanupValidatorTestData.ReadManifestAsync(files, initialLease);
                                    Assert.Equal(original.SessionId, manifest.SessionId);
                                    Assert.Equal(original.RequestId, manifest.RequestId);
                                    Assert.Equal(original.TurnNumber, manifest.TurnNumber);
                                    Assert.Equal(original.SnapshotToken, manifest.ManifestPayloadHash);
                                    Assert.Equal("mortal_world", original.Realm);
                                    var applied = await MortalQteDecisionPublication.ReadJointProgressionPhaseAsync(files,
                                        initialLease, original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber);
                                    Assert.Equal("applied", applied.Phase);
                                    var read = await story.ReadMortalJointStoryAsync(initialLease, applied);
                                    Assert.True(read.Disposition == StoryService.MortalJointStoryDisposition.AlreadyAppended,
                                        "history_fixture_story_refused: " + read.Disposition + "; " + read.Failure);
                                    var proof = Assert.IsType<StoryService.MortalJointStoryProof>(read.Proof);
                                    report["OriginalTuple"] = new { original.SessionId, original.RequestId,
                                        original.SnapshotToken, original.TurnNumber, original.Realm };
                                    return (Manifest: manifest, Proof: proof);
                                });
                            // The real history proof acquires fresh leases. The issuance lease is already disposed here.
                            report["TimingWriteCostsBeforeConsumer"] = capture.GetWriteCosts();
                            WriteReport(reportPath, report);
                            using (validator.UsePrevalidatedPendingTurnSnapshotScope(issued.Manifest))
                            {
                                observation.BeginConsumer();
                                try
                                {
                                    issues = await JointCleanupValidatorProfileTests.MeasureAsync(files, raw, capture,
                                        "HistoryConsumer.ActualGameStateValidation",
                                        () => validator.ValidateMortalJointStoryGameStateAsync(issued.Proof));
                                }
                                catch (Exception error) { consumerFailure = error; }
                                finally
                                {
                                    observation.EndConsumer();
                                    report["TimingWriteCostsAfterConsumer"] = capture.GetWriteCosts();
                                }
                            }
                            report["ReturnedIssues"] = issues;
                            report["IssuesWereReturned"] = issues is not null;
                            report["ConsumerException"] = consumerFailure?.ToString();
                            report["ConsumerSecondaryFailures"] = consumerFailure?.Data.Cast<System.Collections.DictionaryEntry>()
                                .Select(entry => new { Stage = entry.Key.ToString(), Error = entry.Value?.ToString() }).ToArray();
                            try
                            {
                                raw.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_history_consumer_outcome",
                                    ReturnedIssues = issues, IssuesWereReturned = issues is not null,
                                    Exception = consumerFailure?.ToString(), SecondaryFailures = report["ConsumerSecondaryFailures"],
                                    Observation = observation.Describe() }));
                            }
                            catch (Exception error) { capture.RecordDiagnosticFailure(error, "consumer-outcome-report"); }
                            if (consumerFailure is not null && (!transientPendingWave ||
                                consumerFailure.GetType() != typeof(InvalidDataException) ||
                                consumerFailure.Message != "mortal_qte_completed_publication_invalid: joint_published_footprint: " +
                                    WoundAcceptedTurnSnapshotContract.PendingResolutionPath))
                                ExceptionDispatchInfo.Capture(consumerFailure).Throw();
                            observation.VerifyExpectedOutcome(consumerFailure, issues);
                            outcomeVerified = true;
                            return true;
                        }
                        finally
                        {
                            outerClosingStarted = Stopwatch.GetTimestamp();
                            try { outerClosingCpu = process.TotalProcessorTime; }
                            catch (Exception error) { capture.RecordDiagnosticFailure(error, "outer-closing-cpu-start"); }
                        }
                    }, observeOriginalClose: (close, observed, remote) =>
                    {
                        remoteCallbackInvoked = true;
                        remoteClose = close;
                        remoteCloseObserved = observed;
                        wasRemote = remote;
                    });
                    outerCompleted = true;
                    return result;
                }
                finally
                {
                    outerAwaitSettled = true;
                    if (outerClosingStarted is { } started)
                    {
                        var elapsed = Stopwatch.GetElapsedTime(started);
                        report["AwaitedOuterClosingMilliseconds"] = elapsed.TotalMilliseconds;
                        try
                        {
                            var cpu = outerClosingCpu is { } cpuStart ? process.TotalProcessorTime - cpuStart : (TimeSpan?)null;
                            report["AwaitedOuterClosingCpuMilliseconds"] = cpu?.TotalMilliseconds;
                            raw.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_history_consumer_process_cost",
                                Operation = "HistoryConsumer.AwaitedOuterClosing", WallMilliseconds = elapsed.TotalMilliseconds,
                                CpuMilliseconds = cpu?.TotalMilliseconds, AwaitSettledIncludingDisposal = true,
                                OuterCompletedSuccessfully = outerCompleted }));
                        }
                        catch (Exception error) { capture.RecordDiagnosticFailure(error, "outer-closing-cost-report"); }
                    }
                }
            });
        }
        catch (Exception error) { RecordFailure(error, "setup-or-actual-consumer"); }
        finally
        {
            report["OuterAttempted"] = outerAttempted;
            report["OuterAwaitSettledIncludingDisposal"] = outerAwaitSettled;
            report["OuterCompletedSuccessfully"] = outerCompleted;
            report["RemoteCallbackInvoked"] = remoteCallbackInvoked;
            report["RemoteTerminalClose"] = remoteClose;
            report["RemoteCloseObserved"] = remoteCloseObserved;
            report["WasRemote"] = wasRemote;
            report["ReturnedIssues"] = issues;
            report["IssuesWereReturned"] = issues is not null;
            report["ConsumerException"] = consumerFailure?.ToString();
            report["ConsumerSecondaryFailures"] = consumerFailure?.Data.Cast<System.Collections.DictionaryEntry>()
                .Select(entry => new { Stage = entry.Key.ToString(), Error = entry.Value?.ToString() }).ToArray();
            report["Observation"] = observation?.Describe();
            if (files is not null && raw is not null && capture is not null && outerAwaitSettled)
            {
                try
                {
                    await JointCleanupValidatorProfileTests.MeasureAsync(files, raw, capture,
                        "HistoryConsumer.LocalClosingAftercondition", async () =>
                        {
                            var independent = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
                            await using (var admission = independent.BeginMainAdmission())
                            {
                                await admission.AcquireAsync();
                                await using (var lease = await independent.AcquireCanonicalWriteLeaseAsync())
                                    Assert.Equal(generation, independent.ReadExistingSessionGeneration(lease));
                                await admission.CompleteAsync(MainOperationOutcome.Completed, false);
                            }
                            return true;
                        });
                    afterconditionClosed = true;
                }
                catch (Exception error) { RecordFailure(error, "local-closing-aftercondition"); }
            }
            if (files is not null && raw is not null && capture is not null && before is not null && generationBefore is not null)
            {
                try
                {
                    await JointCleanupValidatorProfileTests.MeasureAsync(files, raw, capture, "HistoryConsumer.DataOracles", () =>
                    {
                        var after = JointCleanupValidatorTestData.CaptureImages(root);
                        var directoriesAfter = JointCleanupValidatorTestData.CaptureDirectories(root);
                        var generationAfter = File.ReadAllBytes(files.SessionGenerationPath);
                        report["FinalGameImages"] = after;
                        report["FinalGameDirectories"] = directoriesAfter;
                        report["GenerationAfterBase64"] = Convert.ToBase64String(generationAfter);
                        Assert.Equal(before.OrderBy(pair => pair.Key, StringComparer.Ordinal),
                            after.OrderBy(pair => pair.Key, StringComparer.Ordinal));
                        Assert.Equal(directoriesBefore, directoriesAfter);
                        Assert.Equal(generationBefore, generationAfter);
                        using var archive = File.OpenRead(JointCleanupValidatorTestData.ArchivePath);
                        Assert.Equal(JointCleanupValidatorTestData.ArchiveSha256, Convert.ToHexString(SHA256.HashData(archive)));
                        return Task.FromResult(true);
                    });
                    dataUnchanged = true;
                }
                catch (Exception error) { RecordFailure(error, "data-oracles"); }
            }
            // Merge diagnostic failures only after actual consumer, closing and DATA outcomes are established.
            MergeDiagnostics();
            report["OutcomeVerified"] = outcomeVerified;
            report["GameDirectoriesAndGenerationUnchanged"] = dataUnchanged;
            report["LocalAfterconditionClosed"] = afterconditionClosed;
            Attempt(() => WriteReport(reportPath, report), "pre-cleanup-report");
            var cleanupClock = Stopwatch.StartNew();
            TimeSpan? cleanupCpu = null;
            Attempt(() => cleanupCpu = process.TotalProcessorTime, "cleanup-cpu-start");
            if (outcomeVerified && outerCompleted && afterconditionClosed && dataUnchanged && failure is null && capture?.Complete == true)
                Attempt(() => Directory.Delete(owned, recursive: true), "owned-cleanup");
            Attempt(() => { if (raw is not null) WriteCost(raw, "HistoryConsumer.OwnedCleanup", cleanupClock.Elapsed,
                cleanupCpu is { } cpuStart ? process.TotalProcessorTime - cpuStart : null); }, "cleanup-cost-report");
            report["OwnedRootRemoved"] = !Directory.Exists(owned);
            report["RetainedForFailureOrUnconfirmedClosing"] = Directory.Exists(owned);
            Attempt(() => { raw?.Dispose(); writerClosed = raw is not null; }, "writer-close");
            MergeDiagnostics();
            report["WriterClosed"] = writerClosed;
            report["TimingCaptureComplete"] = capture?.Complete;
            report["TimingBoundariesAttempted"] = capture?.Attempted;
            report["TimingBoundariesStored"] = capture?.Stored;
            Attempt(() =>
            {
                var costs = capture?.GetWriteCosts();
                report["TimingWriteCosts"] = costs;
                report["TimingWriteCostTotals"] = costs is null ? null : new
                {
                    Attempted = costs.Sum(row => row.Attempted), Stored = costs.Sum(row => row.Stored),
                    Measured = costs.Sum(row => row.Measured),
                    SerializationAndWriteMilliseconds = costs.Sum(row => row.SerializationAndWriteElapsed.TotalMilliseconds)
                };
                report["TimingWriteCostContract"] = "Monotonic serializer + inner timing lock + synchronous writer/AutoFlush; " +
                    "excludes outer capture-gate wait, clock/accounting overhead and non-timing logger/cost/report writes. " +
                    "All named attempts retained without quotas; measured includes failed writes.";
            }, "timing-write-cost-report");
            if (writerClosed)
                Attempt(() => { using var input = File.OpenRead(diagnosticPath); report["StoredBytes"] = input.Length;
                    report["Sha256"] = Convert.ToHexString(SHA256.HashData(input)); }, "diagnostic-hash");
            Attempt(() => _output.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_history_consumer_reference",
                Path = diagnosticPath, ReportPath = reportPath, OwnedRoot = owned, WriterClosed = writerClosed,
                LocalAfterconditionClosed = afterconditionClosed, OwnedRootRemoved = !Directory.Exists(owned) })), "compact-reference");
            report["Status"] = failure is null && outcomeVerified && outerCompleted && afterconditionClosed &&
                dataUnchanged && writerClosed && capture?.Complete == true && !Directory.Exists(owned) ? "passed" : "failed";
            report["Failures"] = failures.ToArray();
            report["Failure"] = failure?.ToString();
            report["SecondaryFailures"] = failure?.Data.Cast<System.Collections.DictionaryEntry>()
                .Select(entry => new { Stage = entry.Key.ToString(), Error = entry.Value?.ToString() }).ToArray();
            Attempt(() => WriteReport(reportPath, report), "report-file");
        }
        if (failure is not null)
        {
            failure.Data["HistoryConsumerDiagnostics"] = diagnosticPath;
            failure.Data["HistoryConsumerReport"] = reportPath;
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        Assert.True(outcomeVerified && outerCompleted && afterconditionClosed && dataUnchanged && writerClosed && capture?.Complete == true);
        Assert.False(Directory.Exists(owned));
    }

    /// <summary>
    /// Writes the complete current evidence report outside the owned game copy.
    /// </summary>
    /// <param name="path">
    /// The external report destination registered before fixture materialization.
    /// </param>
    /// <param name="report">
    /// Complete observations, returned issues and exception text without a diagnostic quota.
    /// </param>
    private static void WriteReport(string path, Dictionary<string, object?> report) =>
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

    /// <summary>
    /// Records wall and process CPU costs for setup or cleanup outside an available manager timing scope.
    /// </summary>
    /// <param name="writer">
    /// The caller-owned external JSONL writer.
    /// </param>
    /// <param name="name">
    /// The literal operation whose complete interval was measured.
    /// </param>
    /// <param name="wall">
    /// Elapsed wall time, including any observer work within the interval.
    /// </param>
    /// <param name="cpu">
    /// Observed process CPU delta, or <see langword="null"/> when its initial observation failed.
    /// This does not attribute CPU usage to native I/O or another process.
    /// </param>
    private static void WriteCost(TextWriter writer, string name, TimeSpan wall, TimeSpan? cpu) =>
        writer.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_history_consumer_process_cost", Operation = name,
            WallMilliseconds = wall.TotalMilliseconds, CpuMilliseconds = cpu?.TotalMilliseconds }));

    /// <summary>
    /// Observes actual consumer proof work and owns the one transient mutation within the negative binder.
    /// </summary>
    private sealed class ConsumerObservation
    {
        private const string Binder = "JointStorySource.BindContinuationAsync";
        private const string Footprint = "JointProof.ValidateFootprintAsync";
        private const string SourceAudit = "JointStorySource.VerifyCurrentSessionOperation";
        private readonly string _pendingPath;
        private readonly TextWriter _writer;
        private readonly JointCleanupProfileDiagnosticCapture _capture;
        private readonly bool _negative;
        private readonly Dictionary<string, int> _begins = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _ends = new(StringComparer.Ordinal);
        private bool _armed;
        private bool _consumerEntered;
        private bool _injectionAttempted;
        private bool _injected;
        private bool _ownsPendingWave;
        private bool _targetBinderOpen;
        private bool _presentAtBinderEnd;
        private bool _restored;

        /// <summary>
        /// Selects the independent root, full diagnostics and whether this case inserts a transient pending wave.
        /// </summary>
        /// <param name="root">
        /// The independent immutable-DATA copy owned by the current case.
        /// </param>
        /// <param name="writer">
        /// The synchronized external JSONL writer, kept open through all observation callbacks.
        /// </param>
        /// <param name="capture">
        /// The existing deferred ledger for every observer or storage failure.
        /// </param>
        /// <param name="negative">
        /// Whether the first real consumer binder receives the temporary otherwise absent pending-wave file.
        /// </param>
        internal ConsumerObservation(string root, TextWriter writer, JointCleanupProfileDiagnosticCapture capture, bool negative)
        {
            _pendingPath = Path.Combine(root, "game_session", WoundAcceptedTurnSnapshotContract.PendingResolutionPath.Replace('/', Path.DirectorySeparatorChar));
            _writer = writer;
            _capture = capture;
            _negative = negative;
        }

        /// <summary>
        /// Arms consumer-only counting and injection after real proof issuance and initial-lease disposal.
        /// </summary>
        internal void BeginConsumer()
        {
            Assert.False(File.Exists(_pendingPath));
            _armed = true;
        }

        /// <summary>
        /// Stops consumer counting after its actual return or exception and binder unwinding.
        /// </summary>
        internal void EndConsumer() => _armed = false;

        /// <summary>
        /// Retains every timing boundary and performs the negative mutation only around the first inner binder.
        /// </summary>
        /// <param name="name">
        /// The literal production operation name.
        /// </param>
        /// <param name="boundary">
        /// The production begin or end boundary; the matching end restores the inserted file even on failure.
        /// </param>
        /// <param name="elapsed">
        /// The actual interval supplied by the existing operation timing hook.
        /// </param>
        internal void Observe(string name, string boundary, TimeSpan elapsed)
        {
            _capture.Observe(name, boundary, elapsed);
            if (!_armed) return;
            try
            {
                var counts = boundary == "begin" ? _begins : _ends;
                counts[name] = counts.GetValueOrDefault(name) + 1;
                if (name == "GameState.ValidateCoreAsync" && boundary == "begin") _consumerEntered = true;
                if (!_negative || name != Binder) return;
                if (boundary == "begin" && !_injectionAttempted)
                {
                    _injectionAttempted = true;
                    _targetBinderOpen = true;
                    using (var inserted = new FileStream(_pendingPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                    {
                        _ownsPendingWave = true;
                        inserted.Write("{}"u8);
                    }
                    _injected = true;
                    _writer.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_history_pending_wave_injected", Path = _pendingPath }));
                }
                else if (boundary == "end" && _targetBinderOpen)
                {
                    _presentAtBinderEnd = _ownsPendingWave && File.Exists(_pendingPath);
                    if (!_presentAtBinderEnd || !File.ReadAllBytes(_pendingPath).AsSpan().SequenceEqual("{}"u8))
                        throw new InvalidDataException("Transient pending wave was not retained unchanged through the real binder.");
                    File.Delete(_pendingPath);
                    _restored = !File.Exists(_pendingPath);
                    _ownsPendingWave = false;
                    _targetBinderOpen = false;
                    _writer.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_history_pending_wave_restored", Path = _pendingPath }));
                }
            }
            catch (Exception error) { _capture.RecordDiagnosticFailure(error, "history-consumer-observer:" + name + ":" + boundary); }
        }

        /// <summary>
        /// Requires the exact real consumer outcome and its actual history or inner-binder observations.
        /// </summary>
        /// <param name="error">
        /// The exact consumer exception, or <see langword="null"/> when it returned normally.
        /// </param>
        /// <param name="issues">
        /// All returned issues, or <see langword="null"/> when the consumer threw before returning a list.
        /// </param>
        internal void VerifyExpectedOutcome(Exception? error, List<ValidationIssue>? issues)
        {
            Assert.True(_consumerEntered);
            Assert.False(_armed);
            if (_negative)
            {
                var refusal = Assert.IsType<InvalidDataException>(error);
                Assert.Equal("mortal_qte_completed_publication_invalid: joint_published_footprint: " +
                    WoundAcceptedTurnSnapshotContract.PendingResolutionPath, refusal.Message);
                Assert.Null(issues);
                Assert.True(_injected && _presentAtBinderEnd && _restored);
                Assert.False(_ownsPendingWave || _targetBinderOpen);
                Assert.Equal(1, _begins.GetValueOrDefault(Binder));
                Assert.Equal(1, _ends.GetValueOrDefault(Binder));
            }
            else
            {
                Assert.Null(error);
                Assert.NotNull(issues);
                Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
                Assert.Equal(2, _begins.GetValueOrDefault(SourceAudit));
                Assert.Equal(2, _ends.GetValueOrDefault(SourceAudit));
                Assert.Equal(10, _begins.GetValueOrDefault(Footprint));
                Assert.Equal(10, _ends.GetValueOrDefault(Footprint));
                Assert.False(_injectionAttempted || _injected || _ownsPendingWave);
            }
            Assert.False(File.Exists(_pendingPath));
        }

        /// <summary>
        /// Returns complete consumer operation counts and transient-mutation observations as ordinary report DATA.
        /// </summary>
        /// <returns>
        /// Independent count maps and the actual entry, insertion, retention and restoration flags.
        /// </returns>
        internal object Describe() => new
        {
            ConsumerEntered = _consumerEntered, ConsumerArmed = _armed,
            BegunOperations = new Dictionary<string, int>(_begins, StringComparer.Ordinal),
            EndedOperations = new Dictionary<string, int>(_ends, StringComparer.Ordinal),
            InjectionAttempted = _injectionAttempted, PendingWaveInjected = _injected,
            PendingWavePresentAtBinderEnd = _presentAtBinderEnd, PendingWaveRestored = _restored,
            OwnsPendingWave = _ownsPendingWave, TargetBinderStillOpen = _targetBinderOpen
        };
    }
}
