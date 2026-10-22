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
/// Measures real phase/story owners using independent copies of actual observed DATA.
/// </summary>
public sealed class JointAppliedStoryComponentTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Selects the uncapped per-case diagnostic output.
    /// </summary>
    /// <param name="output">
    /// The current case's output writer; all timing records are retained even when assertions fail.
    /// </param>
    public JointAppliedStoryComponentTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Appends through the real story owner, verifies exact repeat, then rejects changed story bytes.
    /// </summary>
    /// <returns>
    /// Completion after current original/phase/story proofs and refusal, without full game setup.
    /// </returns>
    [Fact]
    public Task ActualAppliedData_AppendsOnceThenRefusesStoryDrift() => RunAsync(false);

    /// <summary>
    /// Refuses changed accepted narrative without appending or mutating other game data.
    /// </summary>
    /// <returns>
    /// Completion after genuine initial phase proof and changed-source refusal on an independent root.
    /// </returns>
    [Fact]
    public Task ActualAppliedData_RefusesChangedNarrativeWithoutAppend() => RunAsync(true);

    /// <summary>
    /// Refuses a changed source during the retained post-append readback without returning a proof.
    /// </summary>
    /// <returns>
    /// Completion after publication uncertainty and preservation of the deliberately changed game images.
    /// </returns>
    [Fact]
    public Task ActualAppliedData_RefusesDriftDuringRetainedReadback() => RunAsync(false, true);

    /// <summary>
    /// Reuses only progression DATA between the real completed proof and phase reader on an independent file copy.
    /// </summary>
    /// <returns>
    /// Completion after one source body, one reuse, unchanged files and owned scope closure.
    /// </returns>
    [Fact]
    public Task ActualAppliedData_PhaseReaderReusesProgressionBody() => RunAsync(false, phaseOnly: true);

    /// <summary>
    /// Runs one actual service case on a new physical root and a freshly issued participating lease.
    /// </summary>
    /// <param name="changeNarrativeBeforeStory">
    /// <see langword="true"/> tests narrative refusal; <see langword="false"/> tests append/repeat/story drift.
    /// </param>
    /// <param name="changeDuringReadback">
    /// Whether the existing canonical read hook changes narrative at the first post-append readback read.
    /// The default <see langword="false"/> leaves that readback unchanged.
    /// </param>
    /// <param name="phaseOnly">
    /// Whether to qualify the real phase DATA pair without installing an arbitrary read hook or running story setup.
    /// </param>
    /// <returns>
    /// Completion after real assertions, complete timing output and disposal of only this case's root.
    /// </returns>
    private async Task RunAsync(bool changeNarrativeBeforeStory, bool changeDuringReadback = false, bool phaseOnly = false)
    {
        var diagnostics = Path.Combine(Path.GetDirectoryName(JointChildOutputCapture.ResolveDefaultArtifactParent())!, "joint-story-components");
        Directory.CreateDirectory(diagnostics);
        var diagnosticsPath = Path.Combine(diagnostics, Guid.NewGuid().ToString("N") + ".jsonl");
        using var raw = new StreamWriter(new FileStream(diagnosticsPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
        var timings = new GameEngineTurnLifecycleTests.JointOperationTimings(raw);
        var owned = Path.Combine(Path.GetTempPath(), "boe-applied-story-component-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(owned);
        using var cleanup = new CleanupOwnedFixture(owned, _output.WriteLine);
        var template = Path.Combine(TestRepoPaths.RepoRoot, "specs", "1536-complete-wound-materialization", "fixtures", "joint-applied-data");
        var sourceRecord = JsonNode.Parse(File.ReadAllText(Path.Combine(template, "source-record.json")))!.AsObject();
        var archive = Path.Combine(template, sourceRecord["Archive"]!.GetValue<string>());
        var archiveBefore = File.ReadAllBytes(archive);
        try
        {
            var setup = Stopwatch.StartNew();
            var root = Path.Combine(owned, "copy");
            var manifest = JointAppliedDataTemplate.Materialize(archive, sourceRecord["ArchiveSHA256"]!.GetValue<string>(), root);
            var sourceReads = 0;
            var readbackStarted = false;
            var injectedReadbackDrift = false;
            var progressionBodies = 0;
            var validationMetrics = new List<RecoveryValidationMetrics>();
            Dictionary<string, string>? readbackDamagedImages = null;
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    OperationTimingObserver = (name, boundary, elapsed) =>
                    {
                        if (name == "JointStorySource.VerifyCurrentSessionOperation" && boundary == "begin")
                            sourceReads++;
                        if (name == "JointStoryComplete.ReadBackSourceAsync" && boundary == "begin")
                            readbackStarted = true;
                        if (name == "JointProgression.ValidateDataBodyAsync" && boundary == "begin")
                            Interlocked.Increment(ref progressionBodies);
                        timings.Observe(name, boundary, elapsed);
                    },
                    RecoveryValidationObserver = metrics => validationMetrics.Add(metrics),
                    BeforeCanonicalReadOpenAsync = !changeDuringReadback ? null : _ =>
                    {
                        if (changeDuringReadback && readbackStarted && !injectedReadbackDrift)
                        {
                            injectedReadbackDrift = true;
                            var path = Path.Combine(root, "game_session", "output", "narrative_response.json");
                            var narrative = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
                            narrative["response"] = "Deliberate source drift during the retained post-append readback.";
                            File.WriteAllText(path, narrative.ToJsonString());
                            readbackDamagedImages = CaptureImages(root);
                            raw.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_component_readback_drift", Injected = true }));
                        }
                        return Task.CompletedTask;
                    }
                });
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            var generation = files.GetOrCreateSessionGeneration(lease);
            raw.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_component_setup", Operation = "Component.MaterializeAndAcquireLease",
                Milliseconds = setup.Elapsed.TotalMilliseconds, Root = root, SourceArchiveSha256 = sourceRecord["ArchiveSHA256"]!.GetValue<string>(),
                CopiedFiles = manifest["Files"]!.AsArray().Count, GameEngineConstructed = false }));
            await SessionOperationContext.RunBoundAsync(files, generation, lease, async () =>
            {
                using var originalTimer = files.MeasureOperation("Component.ReadCurrentOriginal");
                var original = MortalQteWoundOpportunityProjection.ReadSnapshot(files, lease);
                originalTimer?.Dispose();
                var tuple = manifest["Provenance"]!["Original"]!.AsObject();
                Assert.Equal(tuple["SessionId"]!.GetValue<string>(), original.SessionId);
                Assert.Equal(tuple["RequestId"]!.GetValue<string>(), original.RequestId);
                Assert.Equal(tuple["SnapshotToken"]!.GetValue<string>(), original.SnapshotToken);
                Assert.Equal(43, original.TurnNumber);
                var phaseBefore = phaseOnly ? CaptureImages(root) : null;
                using var phaseTimer = files.MeasureOperation("Component.ReadAppliedPhaseAsync");
                var applied = await MortalQteDecisionPublication.ReadJointProgressionPhaseAsync(files, lease,
                    original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber);
                phaseTimer?.Dispose();
                Assert.Equal("applied", applied.Phase);
                if (phaseOnly)
                {
                    raw.WriteLine(JsonSerializer.Serialize(new { Boundary = "progression_data_pair", Bodies = progressionBodies,
                        Metrics = validationMetrics }));
                    Assert.Equal(phaseBefore!.OrderBy(pair => pair.Key), CaptureImages(root).OrderBy(pair => pair.Key));
                    Assert.Equal(1, progressionBodies);
                    var metrics = Assert.Single(validationMetrics);
                    Assert.Equal(2, metrics.Requests);
                    Assert.Equal(1, metrics.Sources);
                    Assert.Equal(1, metrics.Reuses);
                    Assert.True(metrics.Closed);
                    Assert.Equal(new ValidationOperationState(0, 0, true), metrics.State);
                    return true;
                }
                var story = new StoryService(files, NullLogger<StoryService>.Instance);
                if (changeNarrativeBeforeStory)
                {
                    var path = files.ResolvePath("output/narrative_response.json");
                    var narrative = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
                    narrative["response"] = "Deliberately changed accepted narrative for the independent refusal control.";
                    File.WriteAllText(path, narrative.ToJsonString());
                    var before = CaptureImages(root);
                    using var refusalTimer = files.MeasureOperation("Component.ReadStoryAfterNarrativeDriftAsync");
                    var refused = await story.ReadMortalJointStoryAsync(lease, applied);
                    refusalTimer?.Dispose();
                    Assert.Equal(StoryService.MortalJointStoryDisposition.Refused, refused.Disposition);
                    Assert.Null(refused.Proof); Assert.False(string.IsNullOrWhiteSpace(refused.Failure));
                    Assert.Equal(before.OrderBy(pair => pair.Key), CaptureImages(root).OrderBy(pair => pair.Key));
                    return true;
                }
                using var initialTimer = files.MeasureOperation("Component.ReadUnappendedStoryAsync");
                var initial = await story.ReadMortalJointStoryAsync(lease, applied);
                initialTimer?.Dispose();
                Assert.Equal(StoryService.MortalJointStoryDisposition.OriginalUnappended, initial.Disposition);
                Assert.Null(initial.Proof);
                var beforeAppendReads = sourceReads;
                using var appendTimer = files.MeasureOperation("Component.AppendStoryAsync");
                var appended = await story.AppendMortalJointStoryAsync(lease, applied);
                appendTimer?.Dispose();
                if (changeDuringReadback)
                {
                    Assert.True(injectedReadbackDrift);
                    Assert.Equal(StoryService.MortalJointStoryDisposition.PublicationUncertain, appended.Disposition);
                    Assert.Null(appended.Proof);
                    Assert.False(string.IsNullOrWhiteSpace(appended.Failure));
                    Assert.NotNull(readbackDamagedImages);
                    Assert.Equal(readbackDamagedImages.OrderBy(pair => pair.Key), CaptureImages(root).OrderBy(pair => pair.Key));
                    return true;
                }
                Assert.Equal(StoryService.MortalJointStoryDisposition.AppendedAndVerified, appended.Disposition);
                var proof = Assert.IsType<StoryService.MortalJointStoryProof>(appended.Proof);
                Assert.Equal(proof.AfterBytes, File.ReadAllBytes(files.ResolvePath(proof.StoryPath)));
                raw.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_component_source_count", Operation = "Append", Reads = sourceReads - beforeAppendReads }));
                Assert.Equal(3, sourceReads - beforeAppendReads);
                using var proofTimer = files.MeasureOperation("Component.ValidateCurrentStoryProofAsync");
                await proof.ValidateCurrentAsync(files, lease);
                proofTimer?.Dispose();
                var foreignFiles = new FileSystemManager(Path.Combine(owned, "foreign"), NullLogger<FileSystemManager>.Instance);
                await Assert.ThrowsAsync<InvalidDataException>(() => proof.ValidateCurrentAsync(foreignFiles, lease));
                var beforeRepeatReads = sourceReads;
                using var repeatTimer = files.MeasureOperation("Component.RepeatAppendStoryAsync");
                var repeat = await story.AppendMortalJointStoryAsync(lease, applied);
                repeatTimer?.Dispose();
                Assert.Equal(StoryService.MortalJointStoryDisposition.AlreadyAppended, repeat.Disposition);
                Assert.Equal(proof.RowJson, Assert.IsType<StoryService.MortalJointStoryProof>(repeat.Proof).RowJson);
                Assert.Equal(proof.AfterBytes, File.ReadAllBytes(files.ResolvePath(proof.StoryPath)));
                raw.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_component_source_count", Operation = "Repeat", Reads = sourceReads - beforeRepeatReads }));
                Assert.Equal(2, sourceReads - beforeRepeatReads);
                File.AppendAllText(files.ResolvePath(proof.StoryPath), "deliberately changed suffix");
                var damaged = CaptureImages(root);
                using var driftTimer = files.MeasureOperation("Component.ValidateStoryProofAfterDriftAsync");
                await Assert.ThrowsAsync<InvalidDataException>(() => proof.ValidateCurrentAsync(files, lease));
                driftTimer?.Dispose();
                Assert.Equal(damaged.OrderBy(pair => pair.Key), CaptureImages(root).OrderBy(pair => pair.Key));
                return true;
            });
        }
        finally
        {
            raw.Dispose();
            using var diagnosticInput = File.OpenRead(diagnosticsPath);
            _output.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_component_diagnostics", Path = diagnosticsPath,
                Bytes = diagnosticInput.Length, Sha256 = Convert.ToHexString(SHA256.HashData(diagnosticInput)),
                Complete = true, Representation = "complete-utf8-jsonl-file" }));
            Assert.Equal(archiveBefore, File.ReadAllBytes(archive));
        }
    }

    /// <summary>
    /// Captures exact game-file hashes for a read/refusal oracle, excluding runtime coordination data.
    /// </summary>
    /// <param name="root">
    /// The current case's ordinary private root.
    /// </param>
    /// <returns>
    /// Every game-session relative file name and its current exact SHA-256.
    /// </returns>
    private static Dictionary<string, string> CaptureImages(string root) =>
        Directory.EnumerateFiles(Path.Combine(root, "game_session"), "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(root, path),
                path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.Ordinal);
}
