using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Measures real read-only cleanup validators on observed DATA with a fresh participating owner.
/// Historical coordinator, worker and game-loop costs are outside this component measurement.
/// </summary>
public sealed class JointCleanupValidatorProfileTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Selects the output receiving compact references to the complete external diagnostic files.
    /// </summary>
    /// <param name="output">
    /// The current case's output receiver; raw timing records remain in uncapped UTF-8 JSONL.
    /// </param>
    public JointCleanupValidatorProfileTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Measures fresh original, applied, story, projection, terminal and destructive-original checks.
    /// Read-only state aggregation is measured separately; no engine or full cleanup is executed.
    /// </summary>
    /// <returns>
    /// Completion after genuine component acceptance, unchanged game and generation images and confirmed owned cleanup.
    /// A real validator refusal remains a test failure with its diagnostics retained.
    /// </returns>
    [Fact]
    public async Task ActualCleanupData_ProfilesReadOnlyValidatorsWithoutGameEngine()
    {
        var diagnostics = Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "joint-cleanup-profile");
        Directory.CreateDirectory(diagnostics);
        var diagnosticPath = Path.Combine(diagnostics, Guid.NewGuid().ToString("N") + ".jsonl");
        var reportPath = diagnosticPath + ".report.json";
        var owned = Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "joint-cleanup-profile-owned", Guid.NewGuid().ToString("N"));
        var root = Path.Combine(owned, "copy");
        var failures = new List<string>();
        Exception? failure = null;
        StreamWriter? raw = null;
        FileSystemManager.CanonicalWriteLease? lease = null;
        FileSystemManager? files = null;
        Dictionary<string, string>? before = null;
        string[]? directoriesBefore = null;
        byte[]? generationBefore = null;
        string? generation = null;
        var acquireStarted = false;
        var ownerClosed = false;
        var writerClosed = false;
        var imagesUnchanged = false;
        var generationUnchanged = false;
        var validatorsAccepted = false;
        long? bytes = null;
        string? sha256 = null;
        void RecordFailure(Exception error, string stage)
        {
            failures.Add(stage + ": " + error);
            failure = JointInterfaceCrashProcessSupport.PreserveFailure(failure, error, stage);
        }
        void Attempt(Action action, string stage)
        {
            try { action(); }
            catch (Exception error) { RecordFailure(error, stage); }
        }
        var clock = Stopwatch.StartNew();
        try
        {
            raw = new StreamWriter(new FileStream(diagnosticPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read),
                new UTF8Encoding(false)) { AutoFlush = true };
            var timings = new GameEngineTurnLifecycleTests.JointOperationTimings(raw);
            Directory.CreateDirectory(owned);
            var manifest = JointCleanupDataTemplate.Materialize(JointCleanupValidatorTestData.ArchivePath,
                JointCleanupValidatorTestData.ArchiveSha256, root);
            files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                { OperationTimingObserver = timings.Observe });
            acquireStarted = true;
            lease = await files.AcquireCanonicalWriteLeaseAsync();
            generation = files.ReadExistingSessionGeneration(lease); // Never manufacture a generation for this DATA.
            generationBefore = File.ReadAllBytes(files.SessionGenerationPath);
            before = JointCleanupValidatorTestData.CaptureImages(root);
            directoriesBefore = JointCleanupValidatorTestData.CaptureDirectories(root);
            raw.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_cleanup_profile_setup", Root = root,
                OwnedRoot = owned, Milliseconds = clock.Elapsed.TotalMilliseconds, GameEngineConstructed = false,
                ArchiveSHA256 = JointCleanupValidatorTestData.ArchiveSha256, DeclaredFiles = manifest["Files"]!.AsArray().Count,
                HistoricalOwnerRecreated = false, ActualReturnedReadBytes = (long?)null }));
            await SessionOperationContext.RunBoundAsync(files, generation, lease, async () =>
            {
                var original = await MeasureAsync(files, raw, "Profile.ReadCurrentOriginal",
                    () => Task.FromResult(MortalQteWoundOpportunityProjection.ReadSnapshot(files, lease)));
                var parsed = await MeasureAsync(files, raw, "Profile.ParseCurrentManifestData",
                    () => JointCleanupValidatorTestData.ReadManifestAsync(files, lease));
                Assert.Equal(original.SessionId, parsed.SessionId);
                Assert.Equal(original.RequestId, parsed.RequestId);
                Assert.Equal(original.TurnNumber, parsed.TurnNumber);
                Assert.Equal(original.SnapshotToken, parsed.ManifestPayloadHash);
                var applied = await MeasureAsync(files, raw, "JointCleanup.ReadAppliedPhaseAsync", () =>
                    MortalQteDecisionPublication.ReadJointProgressionPhaseAsync(files, lease,
                        original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber));
                Assert.Equal("applied", applied.Phase);
                var story = new StoryService(files, NullLogger<StoryService>.Instance);
                var storyResult = await MeasureAsync(files, raw, "JointCleanup.ReadStoryAsync",
                    () => story.ReadMortalJointStoryAsync(lease, applied));
                Assert.True(storyResult.Disposition == StoryService.MortalJointStoryDisposition.AlreadyAppended,
                    "joint_cleanup_owned_story_missing: " + storyResult.Disposition + "; " + storyResult.Failure);
                Assert.IsType<StoryService.MortalJointStoryProof>(storyResult.Proof);
                var state = new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance);
                var characteristics = new CharacteristicsService(files, state, NullLogger<CharacteristicsService>.Instance);
                var projection = await MeasureAsync(files, raw, "JointCleanup.VerifyProjectionAsync",
                    () => characteristics.VerifyMortalJointProjectionAsync(lease, applied));
                Assert.True(projection.Disposition == CharacteristicsService.JointDerivedProjectionDisposition.ExistingVerified,
                    "joint_cleanup_projection_refused: " + projection.Failure);
                await MeasureAsync(files, raw, "JointCleanup.VerifyTerminalSignalAsync", async () =>
                {
                    await GameEngine.VerifyMortalJointTerminalSignalAsync(files, lease, original.SessionId,
                        original.RequestId, original.TurnNumber, NullLogger<GameEngine>.Instance);
                    return true;
                });
                var payload = await MeasureAsync(files, raw, "JointCleanup.VerifyDestructiveOriginal",
                    () => GameEngine.VerifyMortalJointDestructiveOriginalAsync(files, lease, parsed, original.SnapshotToken));
                Assert.Equal(original.SnapshotToken, payload.ManifestPayloadHash);
                await MeasureAsync(files, raw, "JointCleanup.RefreshReadOnlyStateAsync", async () =>
                {
                    await state.RefreshGameStateReadOnlyAsync(lease);
                    return true;
                });
                validatorsAccepted = true;
                return true;
            });
        }
        catch (Exception error) { RecordFailure(error, "validators-or-setup"); }
        finally
        {
            Attempt(() =>
            {
                using var source = File.OpenRead(JointCleanupValidatorTestData.ArchivePath);
                Assert.Equal(JointCleanupValidatorTestData.ArchiveSha256, Convert.ToHexString(SHA256.HashData(source)));
            }, "immutable-source-oracle");
            // Oracles are attempted on refusal as well, before the real owner is closed.
            if (before is not null && files is not null && generationBefore is not null)
            {
                Attempt(() =>
                {
                    Assert.Equal(before.OrderBy(p => p.Key), JointCleanupValidatorTestData.CaptureImages(root).OrderBy(p => p.Key));
                    Assert.Equal(directoriesBefore, JointCleanupValidatorTestData.CaptureDirectories(root));
                    imagesUnchanged = true;
                    Assert.Equal(generationBefore, File.ReadAllBytes(files.SessionGenerationPath));
                    Assert.Equal(generation, files.ReadExistingSessionGeneration(lease!));
                    generationUnchanged = true;
                }, "readonly-oracle");
            }
            clock.Restart();
            if (lease is not null)
            {
                try { await lease.DisposeAsync(); ownerClosed = true; }
                catch (Exception error) { RecordFailure(error, "owner-close"); }
            }
            Attempt(() => raw?.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_cleanup_profile_owner_close",
                Milliseconds = clock.Elapsed.TotalMilliseconds, AcquireStarted = acquireStarted, OwnerClosed = ownerClosed })), "owner-close-report");
            clock.Restart();
            if (!acquireStarted || ownerClosed)
                Attempt(() => { if (Directory.Exists(owned)) Directory.Delete(owned, recursive: true); }, "owned-cleanup");
            Attempt(() => raw?.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_cleanup_profile_cleanup",
                Milliseconds = clock.Elapsed.TotalMilliseconds, OwnedRoot = owned, Removed = !Directory.Exists(owned),
                RetainedForUnconfirmedOwner = acquireStarted && !ownerClosed })), "cleanup-report");
            Attempt(() => { raw?.Dispose(); writerClosed = raw is not null; }, "writer-close");
            // This is direct writer completion, never a claim about a process pipe's EOF.
            if (writerClosed)
                Attempt(() => { using var input = File.OpenRead(diagnosticPath); bytes = input.Length;
                    sha256 = Convert.ToHexString(SHA256.HashData(input)); }, "diagnostic-hash");
            Attempt(() => _output.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_cleanup_profile_reference",
                Path = diagnosticPath, ReportPath = reportPath, StoredBytes = bytes, Sha256 = sha256, WriterClosed = writerClosed,
                OwnedRoot = owned, OwnedRootRemoved = !Directory.Exists(owned) })), "compact-report");
            Attempt(() => File.WriteAllText(reportPath, JsonSerializer.Serialize(new { Kind = "joint_cleanup_validator_profile_v1",
                Path = diagnosticPath, StoredBytes = bytes, Sha256 = sha256, WriterClosed = writerClosed,
                ValidatorsAccepted = validatorsAccepted, GameImagesAndDirectoriesUnchanged = imagesUnchanged,
                GenerationBytesUnchanged = generationUnchanged, OwnerClosed = ownerClosed, OwnedRoot = owned,
                OwnedRootRemoved = !Directory.Exists(owned), Failures = failures.ToArray(),
                ActualReturnedReadBytes = (long?)null, HistoricalOwnerRecreated = false }, new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(false)), "report-file");
        }
        if (failure is not null)
        {
            failure.Data["CleanupProfileDiagnostics"] = diagnosticPath;
            failure.Data["CleanupProfileReport"] = reportPath;
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        Assert.True(validatorsAccepted && imagesUnchanged && generationUnchanged && ownerClosed && writerClosed);
        Assert.False(Directory.Exists(owned));
    }

    /// <summary>
    /// Measures one real component call with named wall, process CPU and nested operation timings.
    /// </summary>
    /// <typeparam name="T">
    /// The actual component result type.
    /// </typeparam>
    /// <param name="files">
    /// The participating manager whose existing observer emits nested timing records.
    /// </param>
    /// <param name="raw">
    /// The uncapped external JSONL writer, kept open until owner close and cleanup finish.
    /// </param>
    /// <param name="name">
    /// Literal component name without paths or payloads.
    /// </param>
    /// <param name="operation">
    /// The unchanged production validator or separately named read-only aggregation operation.
    /// </param>
    /// <returns>
    /// The actual result; original failures propagate and diagnostic failures are attached separately.
    /// </returns>
    private static async Task<T> MeasureAsync<T>(FileSystemManager files, TextWriter raw, string name, Func<Task<T>> operation)
    {
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        var wall = Stopwatch.StartNew();
        Exception? primary = null;
        IDisposable? timing = null;
        try { timing = files.MeasureOperation(name); return await operation(); }
        catch (Exception error) { primary = error; throw; }
        finally
        {
            try
            {
                timing?.Dispose();
                raw.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_cleanup_profile_process_cost", Operation = name,
                    WallMilliseconds = wall.Elapsed.TotalMilliseconds,
                    CpuMilliseconds = (process.TotalProcessorTime - cpu).TotalMilliseconds, Returned = primary is null }));
            }
            catch (Exception report) when (primary is not null)
            { JointInterfaceCrashProcessSupport.PreserveFailure(primary, report, "component-cost-report"); }
        }
    }
}
