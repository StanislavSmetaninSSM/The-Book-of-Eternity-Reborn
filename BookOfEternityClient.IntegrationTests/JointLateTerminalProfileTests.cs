using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Measures real fresh-engine late-terminal completion on independently copied observed cleanup DATA.
/// This replay does not reproduce source42 preparation, process-cold recovery or native retirement.
/// </summary>
public sealed class JointLateTerminalProfileTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Selects the receiver of compact references to this profile's complete external artifacts.
    /// </summary>
    /// <param name="output">
    /// The current test's output receiver; raw diagnostics are stored separately without quotas.
    /// </param>
    public JointLateTerminalProfileTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Executes the actual late owner with fresh real services and verifies its exact cleanup and installed response.
    /// </summary>
    /// <returns>
    /// Completion after real completion, inert repeat, confirmed local closing and owned cleanup.
    /// Refusal or timeout remains an actual failure with external evidence and retained owned DATA as applicable.
    /// </returns>
    [Fact]
    public async Task ActualCleanupData_ProfilesFreshEngineLateOwnerWithoutWorldPreparation()
    {
        var id = Guid.NewGuid().ToString("N");
        var directory = Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "joint-late-profile");
        Directory.CreateDirectory(directory);
        var diagnosticPath = Path.Combine(directory, id + ".jsonl");
        var reportPath = diagnosticPath + ".report.json";
        var owned = Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "joint-late-owned", id);
        var root = Path.Combine(owned, "copy");
        var report = new Dictionary<string, object?>
        {
            ["Kind"] = "joint_late_terminal_profile_v1", ["Status"] = "in_progress",
            ["Path"] = diagnosticPath, ["OwnedRoot"] = owned, ["FixtureRoot"] = root,
            ["ArchiveSHA256"] = JointCleanupValidatorTestData.ArchiveSha256,
            ["HistoricalOwnerRecreated"] = false, ["Source42OrStage43Prepared"] = false,
            ["StartedUtc"] = DateTime.UtcNow.ToString("O")
        };
        // A timeout can occur before any finally block; register the full artifact/root identity first.
        WriteReport(reportPath, report);
        var failures = new List<string>();
        Exception? failure = null;
        FileSystemManager? files = null;
        TextWriter? raw = null;
        JointCleanupProfileDiagnosticCapture? capture = null;
        Dictionary<string, string>? before = null;
        byte[]? generationBefore = null;
        string? generation = null;
        var outerAttempted = false;
        var outerAwaitSettled = false;
        var outerCompleted = false;
        var afterconditionClosed = false;
        var verified = false;
        var writerClosed = false;
        object? remoteClose = null;
        var remoteCallbackInvoked = false;
        var remoteCloseObserved = false;
        var wasRemote = false;
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
        try
        {
            raw = TextWriter.Synchronized(new StreamWriter(
                new FileStream(diagnosticPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read),
                new UTF8Encoding(false)) { AutoFlush = true });
            capture = new JointCleanupProfileDiagnosticCapture(raw);
            var setup = Stopwatch.StartNew();
            JointLateTerminalFixture.MaterializeObservedCleanupData(owned);
            before = JointCleanupValidatorTestData.CaptureImages(root);
            var directoriesBefore = JointCleanupValidatorTestData.CaptureDirectories(root);
            report["BeforeGameImages"] = before;
            report["BeforeGameDirectories"] = directoriesBefore;
            files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks { OperationTimingObserver = capture.Observe });
            generationBefore = File.ReadAllBytes(files.SessionGenerationPath);
            GameEngine.PendingTurnSnapshotManifest manifest;
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                generation = files.ReadExistingSessionGeneration(lease)
                    ?? throw new InvalidDataException("Observed cleanup DATA has no existing generation.");
                manifest = await SessionOperationContext.RunBoundAsync(files, generation, lease,
                    () => JointCleanupValidatorTestData.ReadManifestAsync(files, lease));
            }
            var cohort = ExpectedCleanupCohort(manifest);
            report["ExpectedDeletedCohort"] = cohort.OrderBy(path => path, StringComparer.Ordinal).ToArray();
            report["ExpectedSession"] = manifest.SessionId;
            report["ExpectedTurn"] = manifest.TurnNumber;
            report["SetupMilliseconds"] = setup.Elapsed.TotalMilliseconds;
            var runtime = await JointCleanupValidatorProfileTests.MeasureAsync(files, raw, capture, "LateProfile.ConstructRealServices", () =>
                Task.FromResult(GameEngineTurnLifecycleTests.CreateGameEngineRuntime(files,
                    logger: JointLateTerminalFixture.CreateLogger(raw, capture))));
            AssertImagesEqual(before, JointCleanupValidatorTestData.CaptureImages(root));
            Assert.Equal(directoriesBefore, JointCleanupValidatorTestData.CaptureDirectories(root));
            Assert.Equal(generationBefore, File.ReadAllBytes(files.SessionGenerationPath));
            report["ConstructionPreservedGameAndGeneration"] = true;
            WriteReport(reportPath, report);
            outerAttempted = true;
            await JointCleanupValidatorProfileTests.MeasureAsync(files, raw, capture, "LateProfile.OuterExpectedSession", async () =>
            {
                try
                {
                    var completed = await SessionOperationContext.RunParticipatingExpectedSessionAsync(files, generation, async () =>
                    {
                        await JointLateTerminalFixture.RefreshAndAdoptAsync(runtime, files, generation);
                        Assert.Equal(manifest.SessionId, runtime.GameLoop.SessionId);
                        Assert.Equal(manifest.TurnNumber, runtime.GameLoop.TurnNumber);
                        var handled = await JointCleanupValidatorProfileTests.MeasureAsync(files, raw, capture, "LateProfile.ActualLateOwner",
                            runtime.Engine.ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync);
                        report["LateOwnerReturnedHandled"] = handled;
                        report["InstalledResponse"] = runtime.Engine.CaptureCurrentResponseForTesting();
                        Assert.False(handled, "The real late owner held or diverted completion; inspect complete engine diagnostics.");
                        var response = runtime.Engine.CaptureCurrentResponseForTesting();
                        Assert.True(response.HasValue, "The actual owner did not install its completion response.");
                        Assert.Equal(manifest.SessionId, runtime.GameLoop.SessionId);
                        Assert.Equal(manifest.TurnNumber, runtime.GameLoop.TurnNumber);
                        using (files.MeasureOperation("LateProfile.CompletionOracles"))
                        {
                            var after = JointCleanupValidatorTestData.CaptureImages(root);
                            report["AfterGameImages"] = after;
                            AssertImagesEqual(before.Where(pair => !cohort.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal), after);
                            Assert.Equal(directoriesBefore, JointCleanupValidatorTestData.CaptureDirectories(root));
                            Assert.Equal(generationBefore, File.ReadAllBytes(files.SessionGenerationPath));
                            VerifyInstalledResponse(root, response!.Value, runtime);
                        }
                        JointLateTerminalObservation repeatBefore;
                        using (files.MeasureOperation("LateProfile.CaptureRepeatBaseline"))
                            repeatBefore = Observe(root, runtime);
                        var repeat = await JointCleanupValidatorProfileTests.MeasureAsync(files, raw, capture, "LateProfile.InertRepeat",
                            runtime.Engine.ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync);
                        Assert.False(repeat);
                        using (files.MeasureOperation("LateProfile.RepeatOracles"))
                            JointLateTerminalAssertions.VerifyInertRepeat(repeatBefore, Observe(root, runtime));
                        report["InertRepeatVerified"] = true;
                        return true;
                    }, observeOriginalClose: (close, observed, remote) =>
                    {
                        remoteCallbackInvoked = true;
                        remoteClose = close;
                        remoteCloseObserved = observed;
                        wasRemote = remote;
                    });
                    outerCompleted = true;
                    return completed;
                }
                finally { outerAwaitSettled = true; }
            });
            verified = true;
        }
        catch (Exception error) { RecordFailure(error, "setup-or-real-owner"); }
        finally
        {
            report["OuterAttempted"] = outerAttempted;
            report["OuterAwaitSettledIncludingDisposal"] = outerAwaitSettled;
            report["OuterCompletedSuccessfully"] = outerCompleted;
            report["RemoteCallbackInvoked"] = remoteCallbackInvoked;
            report["RemoteTerminalClose"] = remoteClose;
            report["RemoteCloseObserved"] = remoteCloseObserved;
            report["WasRemote"] = wasRemote;
            if (files is not null && raw is not null && capture is not null && outerAwaitSettled)
            {
                try
                {
                    await JointCleanupValidatorProfileTests.MeasureAsync(files, raw, capture, "LateProfile.LocalClosingAftercondition", async () =>
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
            // Preserve actual post-failure DATA observations before any owned cleanup.
            if (Directory.Exists(root))
                Attempt(() => { report["FinalGameImages"] = JointCleanupValidatorTestData.CaptureImages(root);
                    report["FinalGameDirectories"] = JointCleanupValidatorTestData.CaptureDirectories(root); }, "final-game-observation");
            if (generationBefore is not null && files is not null)
                Attempt(() => { report["GenerationBytesUnchanged"] = generationBefore.SequenceEqual(File.ReadAllBytes(files.SessionGenerationPath));
                    Assert.Equal(generationBefore, File.ReadAllBytes(files.SessionGenerationPath)); }, "generation-oracle");
            Attempt(() => { using var input = File.OpenRead(JointCleanupValidatorTestData.ArchivePath);
                Assert.Equal(JointCleanupValidatorTestData.ArchiveSha256, Convert.ToHexString(SHA256.HashData(input))); }, "immutable-source-oracle");
            if (capture is not null)
                foreach (var secondary in capture.GetFailures()) RecordFailure(secondary.Error, secondary.Stage);
            var cleanupClock = Stopwatch.StartNew();
            if (verified && afterconditionClosed && failure is null)
                Attempt(() => Directory.Delete(owned, recursive: true), "owned-cleanup");
            report["OwnedCleanupMilliseconds"] = cleanupClock.Elapsed.TotalMilliseconds;
            report["LocalAfterconditionClosed"] = afterconditionClosed;
            report["OwnedRootRemoved"] = !Directory.Exists(owned);
            report["RetainedForFailureOrUnconfirmedClosing"] = Directory.Exists(owned);
            Attempt(() => { raw?.Dispose(); writerClosed = raw is not null; }, "writer-close");
            report["WriterClosed"] = writerClosed;
            report["TimingCaptureComplete"] = capture?.Complete;
            report["TimingBoundariesAttempted"] = capture?.Attempted;
            report["TimingBoundariesStored"] = capture?.Stored;
            if (writerClosed)
                Attempt(() => { using var input = File.OpenRead(diagnosticPath); report["StoredBytes"] = input.Length;
                    report["Sha256"] = Convert.ToHexString(SHA256.HashData(input)); }, "diagnostic-hash");
            Attempt(() => _output.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_late_profile_reference",
                Path = diagnosticPath, ReportPath = reportPath, OwnedRoot = owned, WriterClosed = writerClosed,
                LocalAfterconditionClosed = afterconditionClosed, OwnedRootRemoved = !Directory.Exists(owned) })), "compact-reference");
            report["Verified"] = verified;
            report["Status"] = failure is null && verified && afterconditionClosed && writerClosed && capture?.Complete == true ? "passed" : "failed";
            report["Failures"] = failures.ToArray();
            Attempt(() => WriteReport(reportPath, report), "report-file");
        }
        if (failure is not null)
        {
            failure.Data["LateProfileDiagnostics"] = diagnosticPath;
            failure.Data["LateProfileReport"] = reportPath;
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        Assert.True(verified && outerCompleted && afterconditionClosed && writerClosed && capture?.Complete == true);
        Assert.False(Directory.Exists(owned));
    }

    /// <summary>
    /// Derives the expected deletion inventory as ordinary test DATA without asking production to authorize it.
    /// </summary>
    /// <param name="manifest">
    /// The copied original DTO; its values are expected paths, never live authority.
    /// </param>
    /// <returns>
    /// Exact game-root-relative cohort names used only for independent before/after comparison.
    /// </returns>
    private static HashSet<string> ExpectedCleanupCohort(GameEngine.PendingTurnSnapshotManifest manifest) =>
        manifest.Files.Values.Concat(manifest.RollbackBackups.Values)
            .Concat(manifest.Files.Keys.Where(ConsoleLocalTurnRollbackArtifacts.IsMarker))
            .Concat(new[] { LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath,
                "input/turn_request.json", "ready/turn_complete.json", MortalWoundOpportunityAdapter.DecisionPath,
                "game_state/control/validation_repair_request.json", "game_state/control/validation_repair_ready.json" })
            .Select(path => Path.Combine("game_session", path.Replace('/', Path.DirectorySeparatorChar)))
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Requires exact ordinal file membership, byte lengths and hashes without accepting unrelated additions or changes.
    /// </summary>
    /// <param name="expected">
    /// Expected current game images, excluding only the explicitly authorized cleanup cohort after completion.
    /// </param>
    /// <param name="actual">
    /// Independently observed current game images.
    /// </param>
    private static void AssertImagesEqual(Dictionary<string, string> expected, Dictionary<string, string> actual) =>
        Assert.Equal(expected.OrderBy(pair => pair.Key, StringComparer.Ordinal), actual.OrderBy(pair => pair.Key, StringComparer.Ordinal));

    /// <summary>
    /// Checks installed response content against retained accepted display DATA and refreshed real state.
    /// </summary>
    /// <param name="root">
    /// The independent fixture root after actual cleanup.
    /// </param>
    /// <param name="response">
    /// An independent snapshot of the actual installed runtime response.
    /// </param>
    /// <param name="runtime">
    /// The real service bundle whose read-only state was refreshed by actual completion.
    /// </param>
    private static void VerifyInstalledResponse(string root, JsonElement response, GameEngineTestRuntime runtime)
    {
        using var narrative = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "game_session", "output", "narrative_response.json")));
        Assert.Equal(PlayerFacingTextNormalizer.NormalizeEscapedLineBreakArtifacts(narrative.RootElement.GetProperty("response").GetString()),
            response.GetProperty("response").GetString());
        using var ui = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "game_session", "output", "interface_updates.json")));
        JointLateTerminalAssertions.VerifyInterfaceResponse(response, ui.RootElement);
        Assert.Equal(runtime.StateManager.CurrentState.PlayerStatus.CurrentCondition,
            response.GetProperty("playerStatus").GetProperty("currentCondition").GetString());
    }

    /// <summary>
    /// Captures independent ordinary game and runtime DATA without issuing validation or cleanup authority.
    /// </summary>
    /// <param name="root">
    /// The independent fixture root observed without modifying it.
    /// </param>
    /// <param name="runtime">
    /// The actual runtime whose installed response and session tuple are observed.
    /// </param>
    /// <returns>
    /// The complete observation used for the inert-repeat comparison; an absent installed response refuses.
    /// </returns>
    private static JointLateTerminalObservation Observe(string root, GameEngineTestRuntime runtime) =>
        new(JointCleanupValidatorTestData.CaptureImages(root), JointCleanupValidatorTestData.CaptureDirectories(root),
            runtime.GameLoop.SessionId, runtime.GameLoop.TurnNumber,
            runtime.Engine.CaptureCurrentResponseForTesting()?.GetRawText()
                ?? throw new InvalidDataException("The actual runtime lost its installed response."));

    /// <summary>
    /// Saves the complete profile observation without embedding raw diagnostic streams in a JSON scalar.
    /// </summary>
    /// <param name="path">
    /// The external report path outside the owned fixture root.
    /// </param>
    /// <param name="report">
    /// Current complete observations, including explicit unfinished or failed status.
    /// </param>
    private static void WriteReport(string path, Dictionary<string, object?> report) =>
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
}
