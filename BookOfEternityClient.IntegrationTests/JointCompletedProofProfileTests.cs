using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Separates real completed-publication validation costs on an independent copy of observed data.
/// </summary>
public sealed class JointCompletedProofProfileTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Selects the diagnostic output receiving every record from this case.
    /// </summary>
    /// <param name="output">
    /// The current test's output receiver; records are retained without a count or byte quota.
    /// </param>
    public JointCompletedProofProfileTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Measures one genuine applied-phase read and ordinary continuation binding without game setup.
    /// </summary>
    /// <returns>
    /// Completion after successful real validations, unchanged game images and generation, and owned cleanup.
    /// </returns>
    [Fact]
    public async Task ActualAppliedData_ProfilesPhaseAndOrdinaryContinuationWithoutWrites()
    {
        var diagnostics = Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "joint-proof-profile");
        Directory.CreateDirectory(diagnostics);
        var diagnosticsPath = Path.Combine(diagnostics, Guid.NewGuid().ToString("N") + ".jsonl");
        using var raw = new StreamWriter(new FileStream(diagnosticsPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
        var timings = new GameEngineTurnLifecycleTests.JointOperationTimings(raw);
        var owned = Path.Combine(Path.GetTempPath(), "boe-joint-proof-profile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(owned);
        var template = Path.Combine(TestRepoPaths.RepoRoot, "specs", "1536-complete-wound-materialization", "fixtures", "joint-applied-data");
        var recordPath = Path.Combine(template, "source-record.json");
        var recordBefore = File.ReadAllBytes(recordPath);
        var sourceRecord = JsonNode.Parse(recordBefore)!.AsObject();
        var archive = Path.Combine(template, sourceRecord["Archive"]!.GetValue<string>());
        var archiveBefore = File.ReadAllBytes(archive);
        try
        {
            using var cleanup = new CleanupOwnedFixture(owned, line => { raw.WriteLine(line); _output.WriteLine(line); });
            var setup = Stopwatch.StartNew();
            var root = Path.Combine(owned, "copy");
            var manifest = JointAppliedDataTemplate.Materialize(archive, sourceRecord["ArchiveSHA256"]!.GetValue<string>(), root);
            var completedProofs = 0;
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    OperationTimingObserver = (name, boundary, elapsed) =>
                    {
                        if (name == "JointProof.RecoverStableMarkerAsync" && boundary == "begin") completedProofs++;
                        timings.Observe(name, boundary, elapsed);
                    }
                });
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            var generation = files.GetOrCreateSessionGeneration(lease);
            var generationBefore = File.ReadAllBytes(files.SessionGenerationPath);
            var imagesBefore = CaptureImages(root);
            var directoriesBefore = CaptureDirectories(root);
            raw.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_profile_setup", Root = root,
                Milliseconds = setup.Elapsed.TotalMilliseconds, CopiedFiles = manifest["Files"]!.AsArray().Count,
                SourceArchiveSha256 = sourceRecord["ArchiveSHA256"]!.GetValue<string>(), GameEngineConstructed = false }));
            await SessionOperationContext.RunBoundAsync(files, generation, lease, async () =>
            {
                using var originalTimer = files.MeasureOperation("Profile.ReadCurrentOriginal");
                var original = MortalQteWoundOpportunityProjection.ReadSnapshot(files, lease);
                originalTimer?.Dispose();
                var tuple = manifest["Provenance"]!["Original"]!.AsObject();
                Assert.Equal(tuple["SessionId"]!.GetValue<string>(), original.SessionId);
                Assert.Equal(tuple["RequestId"]!.GetValue<string>(), original.RequestId);
                Assert.Equal(tuple["SnapshotToken"]!.GetValue<string>(), original.SnapshotToken);
                Assert.Equal(43, original.TurnNumber);
                using var process = Process.GetCurrentProcess();
                var phaseCpu = process.TotalProcessorTime;
                var phaseWall = Stopwatch.StartNew();
                using var phaseTimer = files.MeasureOperation("Profile.ReadAppliedPhaseAsync");
                var phase = await MortalQteDecisionPublication.ReadJointProgressionPhaseAsync(files, lease,
                    original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber);
                phaseTimer?.Dispose();
                phaseWall.Stop();
                raw.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_profile_process_cost", Operation = "AppliedPhase",
                    WallMilliseconds = phaseWall.Elapsed.TotalMilliseconds,
                    CpuMilliseconds = (process.TotalProcessorTime - phaseCpu).TotalMilliseconds }));
                Assert.Equal("applied", phase.Phase);
                Assert.Equal(1, completedProofs);
                var bindingCpu = process.TotalProcessorTime;
                var bindingWall = Stopwatch.StartNew();
                using var bindingTimer = files.MeasureOperation("Profile.BindOrdinaryContinuationAsync");
                var binding = await MortalQteDecisionPublication.TryBindExactJointContinuationAsync(files, lease,
                    original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber);
                bindingTimer?.Dispose();
                bindingWall.Stop();
                raw.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_profile_process_cost", Operation = "OrdinaryContinuation",
                    WallMilliseconds = bindingWall.Elapsed.TotalMilliseconds,
                    CpuMilliseconds = (process.TotalProcessorTime - bindingCpu).TotalMilliseconds }));
                Assert.NotNull(binding);
                Assert.True(binding.Success);
                Assert.Single(binding.Notifications);
                Assert.Equal(3, completedProofs);
                Assert.Equal(imagesBefore.OrderBy(pair => pair.Key), CaptureImages(root).OrderBy(pair => pair.Key));
                Assert.Equal(directoriesBefore, CaptureDirectories(root));
                Assert.Equal(generationBefore, File.ReadAllBytes(files.SessionGenerationPath));
                Assert.Equal(generation, files.ReadExistingSessionGeneration(lease));
                raw.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_profile_readonly_oracle", AppliedPhase = phase.Phase,
                    OrdinaryContinuationSuccess = binding.Success, CompletedProofReads = completedProofs,
                    GameFiles = imagesBefore.Count, GameDirectories = directoriesBefore.Length,
                    GameImagesAndDirectoriesUnchanged = true, GenerationBytesUnchanged = true }));
                return true;
            });
        }
        finally
        {
            raw.Flush();
            _output.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_profile_diagnostics", Path = diagnosticsPath }));
            _output.WriteLine(File.ReadAllText(diagnosticsPath));
            Assert.Equal(archiveBefore, File.ReadAllBytes(archive));
            Assert.Equal(recordBefore, File.ReadAllBytes(recordPath));
        }
    }

    /// <summary>
    /// Captures every exact game-file name and byte hash without including runtime coordination.
    /// </summary>
    /// <param name="root">
    /// The case's independently imported root containing the game session.
    /// </param>
    /// <returns>
    /// An ordinal map of relative game-file names to exact SHA-256 values.
    /// </returns>
    private static Dictionary<string, string> CaptureImages(string root) =>
        Directory.EnumerateFiles(Path.Combine(root, "game_session"), "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(root, path),
                path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.Ordinal);

    /// <summary>
    /// Captures every game-directory name, including empty directories, in ordinal order.
    /// </summary>
    /// <param name="root">
    /// The case's independently imported root containing the game session.
    /// </param>
    /// <returns>
    /// The sorted relative directory names for the complete game-session subtree.
    /// </returns>
    private static string[] CaptureDirectories(string root) =>
        Directory.EnumerateDirectories(Path.Combine(root, "game_session"), "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path)).Order(StringComparer.Ordinal).ToArray();
}
