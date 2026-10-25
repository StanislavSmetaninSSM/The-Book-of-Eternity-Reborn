using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises actual no-write owners on independent applied DATA without constructing a game engine.
/// </summary>
/// <param name="output">
/// Receiver for exact input provenance, complete raw timing references and owned cleanup.
/// </param>
public sealed class JointOperationBoundaryTests(ITestOutputHelper output)
{
    /// <summary>
    /// Refuses raw drift before actual no-write success and preserves success for the unchanged positive control.
    /// </summary>
    /// <param name="owner">
    /// Actual progression, story or derived-projection owner to invoke.
    /// </param>
    /// <param name="drift">
    /// Whether a raw edit with restored timestamp occurs after DATA reuse and before the owning fresh fence.
    /// </param>
    /// <returns>
    /// Completion after exact result, unchanged/refused game images and uncapped diagnostics are verified.
    /// </returns>
    [Theory]
    [InlineData("progression", false)]
    [InlineData("progression", true)]
    [InlineData("story", false)]
    [InlineData("story", true)]
    [InlineData("projection", false)]
    [InlineData("projection", true)]
    public async Task ActualNoWriteOwnerRequiresFreshSourceFence(string owner, bool drift)
    {
        var diagnostics = Path.Combine(Path.GetDirectoryName(JointChildOutputCapture.ResolveDefaultArtifactParent())!, "joint-operation-boundaries");
        Directory.CreateDirectory(diagnostics);
        var rawPath = Path.Combine(diagnostics, Guid.NewGuid().ToString("N") + ".jsonl");
        using var raw = new StreamWriter(new FileStream(rawPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
        var timings = new GameEngineTurnLifecycleTests.JointOperationTimings(raw);
        var owned = Path.Combine(Path.GetTempPath(), "boe-operation-boundary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(owned);
        using var cleanup = new CleanupOwnedFixture(owned, output.WriteLine);
        try
        {
            var appliedSource = JointAppliedDataTemplate.ResolveCurrent();
            var archive = appliedSource.ArchivePath;
            var root = Path.Combine(owned, "copy");
            var manifest = JointAppliedDataTemplate.Materialize(appliedSource, root);
            var armed = false;
            var injected = false;
            var reuses = 0;
            Dictionary<string, string>? damaged = null;
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    OperationTimingObserver = (name, boundary, elapsed) =>
                    {
                        timings.Observe(name, boundary, elapsed);
                        if (armed && boundary == "begin" && name is "OperationFileData.ReuseSync" or "OperationFileData.ReuseAsync") reuses++;
                        if (!armed || !drift || injected || name != "OperationFileData.FreshFence" || boundary != "begin") return;
                        injected = true;
                        var path = Path.Combine(root, "game_session", "output", "narrative_response.json");
                        var bytes = File.ReadAllBytes(path);
                        var stamp = File.GetLastWriteTimeUtc(path);
                        File.WriteAllBytes(path, [..bytes, 0x20, 0x0a]);
                        File.SetLastWriteTimeUtc(path, stamp);
                        damaged = CaptureImages(root);
                    }
                });
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            var generation = files.GetOrCreateSessionGeneration(lease);
            await SessionOperationContext.RunBoundAsync(files, generation, lease, async () =>
            {
                var tuple = manifest["Provenance"]!["Original"]!.AsObject();
                var phase = await MortalQteDecisionPublication.ReadJointProgressionPhaseAsync(files, lease,
                    tuple["SessionId"]!.GetValue<string>(), tuple["RequestId"]!.GetValue<string>(),
                    tuple["SnapshotToken"]!.GetValue<string>(), tuple["TurnNumber"]!.GetValue<int>());
                Assert.Equal("applied", phase.Phase);
                var story = new StoryService(files, NullLogger<StoryService>.Instance);
                var state = new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance);
                var projection = new CharacteristicsService(files, state, NullLogger<CharacteristicsService>.Instance);
                if (owner == "story")
                {
                    var setup = await story.AppendMortalJointStoryAsync(lease, phase);
                    Assert.True(setup.Disposition is StoryService.MortalJointStoryDisposition.AppendedAndVerified or
                        StoryService.MortalJointStoryDisposition.AlreadyAppended, setup.Failure);
                }
                if (owner == "projection")
                {
                    var setup = await projection.WriteMortalJointProjectionAsync(lease, phase);
                    Assert.True(setup.Disposition == CharacteristicsService.JointDerivedProjectionDisposition.WrittenAndVerified, setup.Failure);
                }
                var before = CaptureImages(root);
                armed = true;
                switch (owner)
                {
                    case "progression":
                        var progressionResult = await MortalQteDecisionPublication.CommitJointProgressionPhaseAsync(files, lease, phase);
                        Assert.Equal(!drift, progressionResult.Disposition == MortalQteDecisionPublication.JointProgressionPublicationDisposition.AlreadyApplied);
                        if (drift) Assert.Null(progressionResult.AppliedCapability);
                        else Assert.NotNull(progressionResult.AppliedCapability);
                        break;
                    case "story":
                        var storyResult = await story.ReadMortalJointStoryAsync(lease, phase);
                        Assert.Equal(!drift, storyResult.Disposition == StoryService.MortalJointStoryDisposition.AlreadyAppended);
                        if (drift) Assert.Null(storyResult.Proof);
                        else Assert.NotNull(storyResult.Proof);
                        break;
                    case "projection":
                        var projectionResult = await projection.VerifyMortalJointProjectionAsync(lease, phase);
                        Assert.Equal(!drift, projectionResult.Disposition == CharacteristicsService.JointDerivedProjectionDisposition.ExistingVerified);
                        break;
                    default: throw new ArgumentOutOfRangeException(nameof(owner));
                }
                Assert.Equal(drift, injected);
                Assert.True(reuses > 0, "The actual owning operation must reuse source DATA before this fence.");
                Assert.Equal((damaged ?? before).OrderBy(pair => pair.Key), CaptureImages(root).OrderBy(pair => pair.Key));
                output.WriteLine(JsonSerializer.Serialize(new { owner, drift, injected, reuses, GameEngineConstructed = false,
                    InputSHA256 = appliedSource.ArchiveSha256, AllGameImagesPreserved = true }));
                return true;
            });
        }
        finally
        {
            raw.Flush();
            using var stream = new FileStream(rawPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            output.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_operation_diagnostics", Path = rawPath,
                Bytes = stream.Length, SHA256 = Convert.ToHexString(SHA256.HashData(stream)), Complete = true }));
        }
    }

    /// <summary>
    /// Captures all exact game-file hashes while excluding runtime coordination files.
    /// </summary>
    /// <param name="root">
    /// Unique physical root owned by the current fixture.
    /// </param>
    /// <returns>
    /// Ordinal paths and SHA-256 hashes of every game-session file.
    /// </returns>
    private static Dictionary<string, string> CaptureImages(string root) =>
        Directory.EnumerateFiles(Path.Combine(root, "game_session"), "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(root, path),
                path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.Ordinal);
}
