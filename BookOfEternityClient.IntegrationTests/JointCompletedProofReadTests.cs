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
/// Compares the actual completed-proof reader on independent ordinary applied-DATA copies.
/// </summary>
/// <param name="output">
/// Receiver for compact counts and full uncapped timing-file references.
/// </param>
public sealed class JointCompletedProofReadTests(ITestOutputHelper output)
{
    /// <summary>
    /// Requires fewer physical attribute probes with identical real proof checks and unchanged game bytes.
    /// </summary>
    /// <returns>
    /// Completion after eligible and forced-fresh copies both validate through the original entry point.
    /// </returns>
    [Fact]
    public async Task ActualCompletedProofReducesAttributesAgainstFreshHookFallback()
    {
        var fresh = await RunAsync(freshHook: true, drift: false);
        var cached = await RunAsync(freshHook: false, drift: false);
        output.WriteLine(JsonSerializer.Serialize(new { Fresh = fresh, Cached = cached }));
        Assert.Equal(fresh.GameImages.OrderBy(x => x.Key), cached.GameImages.OrderBy(x => x.Key));
        Assert.Equal(0, fresh.ReadSetOpens);
        Assert.Equal(0, cached.ReadSetOpens);
        Assert.Equal(0, fresh.SourceFences);
        Assert.Equal(1, cached.SourceFences);
        Assert.True(cached.Reuses > 0);
        Assert.True(cached.SourceReads.Values.Sum() < fresh.SourceReads.Values.Sum());
        Assert.True(cached.Attributes < fresh.Attributes,
            $"Actual proof attributes {cached.Attributes} must be below original fallback {fresh.Attributes}.");
    }

    /// <summary>
    /// Refuses raw history drift after its first observation before returning a completed proof.
    /// </summary>
    /// <returns>
    /// Completion after deliberate drift remains the only game change and no proof result escapes.
    /// </returns>
    [Fact]
    public async Task ActualCompletedProofRefusesRawHistoryDriftBeforeReturn() =>
        _ = await RunAsync(freshHook: false, drift: true);

    /// <summary>
    /// Refuses late raw byte or absence drift after all proof checks but before the owning source fence returns.
    /// </summary>
    /// <param name="change">
    /// Existing history bytes or an absent continuation command changed at fresh-fence entry.
    /// </param>
    /// <returns>
    /// Completion after the exact source refusal and confirmation that no successful validation escaped.
    /// </returns>
    [Theory]
    [InlineData("history")]
    [InlineData("continuation")]
    public async Task ActualCompletionRefusesLateSourceDriftBeforeOwningReturn(string change) =>
        _ = await RunAsync(freshHook: false, drift: true, fenceChange: change);

    /// <summary>
    /// Preserves the existing outer reservation's writer and closing exclusions through the completion entry.
    /// </summary>
    /// <returns>
    /// Completion after genuine proof validation, two refused effects and the outer owner's fresh closing.
    /// </returns>
    [Fact]
    public async Task ActualCompletionKeepsOuterReservedPlanExclusions()
    {
        var observed = await RunAsync(freshHook: false, drift: false, outerReserved: true);
        Assert.Equal(1, observed.ReadSetOpens);
        Assert.Equal(0, observed.SourceFences);
    }

    /// <summary>
    /// Shares already-read file DATA between independently parsed complete proofs in one owning operation.
    /// </summary>
    /// <returns>
    /// Completion after at least a quarter of actual source opens are removed with fewer attribute probes and identical game bytes.
    /// </returns>
    [Fact]
    public async Task ActualOwningOperationReusesFilesBetweenIndependentProofs()
    {
        var fresh = await RunAsync(freshHook: true, drift: false, repetitions: 2, owningOperation: true);
        var cached = await RunAsync(freshHook: false, drift: false, repetitions: 2, owningOperation: true);
        output.WriteLine(JsonSerializer.Serialize(new { Fresh = fresh, Cached = cached }));
        Assert.Equal(fresh.GameImages.OrderBy(x => x.Key), cached.GameImages.OrderBy(x => x.Key));
        Assert.Equal(0, cached.ReadSetOpens);
        Assert.True(cached.SourceReads.Values.Sum() * 4 < fresh.SourceReads.Values.Sum() * 3,
            $"Owning sources {cached.SourceReads.Values.Sum()} must be at least25% below fresh {fresh.SourceReads.Values.Sum()}.");
        Assert.True(cached.Attributes < fresh.Attributes);
        Assert.True(cached.Reuses > 0);
    }

    /// <summary>
    /// Retains only measured counters and immutable game-image hashes from one independently closed copy.
    /// </summary>
    /// <param name="Attributes">
    /// Actual completed File.GetAttributes probes during the real proof entry.
    /// </param>
    /// <param name="ReadSetOpens">
    /// Number of actual reserved read-set openings during the proof.
    /// </param>
    /// <param name="Reuses">
    /// Actual synchronous and asynchronous source reuse events.
    /// </param>
    /// <param name="GameImages">
    /// Exact game-session path-to-hash mapping after the read or deliberate refusal.
    /// </param>
    /// <param name="SourceReads">
    /// Actual canonical physical source-read events for every observed relative path.
    /// </param>
    /// <param name="SourceFences">
    /// Existing whole-source fresh fences completed or refused during the measured validation.
    /// </param>
    private sealed record Observation(int Attributes, int ReadSetOpens, int Reuses,
        Dictionary<string, string> GameImages, Dictionary<string, int> SourceReads, int SourceFences);

    /// <summary>
    /// Runs the production proof on a verified DATA template without building a game world.
    /// </summary>
    /// <param name="freshHook">
    /// Installs an effect-free functional read hook to select the original unsupported/fresh route.
    /// </param>
    /// <param name="drift">
    /// Changes current wound history after its initial proof read to exercise the physical exit fence.
    /// </param>
    /// <param name="repetitions">
    /// Number of independent original entry calls; each parses its own private proof root.
    /// </param>
    /// <param name="owningOperation">
    /// Whether the complete sequence belongs to one explicitly completed source DATA operation.
    /// </param>
    /// <param name="fenceChange">
    /// Optional late source change; null retains the original mid-proof history injection.
    /// </param>
    /// <param name="outerReserved">
    /// Whether a real outer reserved reader retains writer and entry-closing exclusions.
    /// </param>
    /// <returns>
    /// Exact observed counters and game-image hashes after native-independent source validation.
    /// </returns>
    private async Task<Observation> RunAsync(bool freshHook, bool drift, int repetitions = 1, bool owningOperation = false,
        string? fenceChange = null, bool outerReserved = false)
    {
        var diagnosticParent = Path.Combine(Path.GetDirectoryName(JointChildOutputCapture.ResolveDefaultArtifactParent())!, "joint-proof-reads");
        Directory.CreateDirectory(diagnosticParent);
        var diagnosticPath = Path.Combine(diagnosticParent, Guid.NewGuid().ToString("N") + ".jsonl");
        using var raw = new StreamWriter(new FileStream(diagnosticPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
        var timings = new GameEngineTurnLifecycleTests.JointOperationTimings(raw);
        var own = Path.Combine(Path.GetTempPath(), "boe-proof-component-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(own);
        using var cleanup = new CleanupOwnedFixture(own, output.WriteLine);
        var appliedSource = JointAppliedDataTemplate.ResolveCurrent();
        var archive = appliedSource.ArchivePath;
        var archiveHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archive)));
        try
        {
            var root = Path.Combine(own, "copy");
            var manifest = JointAppliedDataTemplate.Materialize(appliedSource, root);
            var measuring = false;
            var attributes = 0; var opens = 0; var reuses = 0; var fences = 0; var injected = false;
            var sourceReads = new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, string>? damaged = null;
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    BeforeCanonicalReadOpenAsync = freshHook ? _ => Task.CompletedTask : null,
                    CanonicalReadActivityObserver = path =>
                    {
                        if (measuring) sourceReads[path] = sourceReads.GetValueOrDefault(path) + 1;
                    },
                    OperationTimingObserver = (name, boundary, elapsed) =>
                    {
                        timings.Observe(name, boundary, elapsed);
                        if (!measuring) return;
                        if (name == "TrustedScope.Probe.GetAttributes" && boundary == "end") attributes++;
                        if (name == "ValidationReadSet.Open" && boundary == "begin") opens++;
                        if (name == "OperationFileData.FreshFence" && boundary == "begin") fences++;
                        if (name is "ValidationReadSet.ReuseSync" or "ValidationReadSet.ReuseAsync" && boundary == "begin") reuses++;
                        if (name is "OperationFileData.ReuseSync" or "OperationFileData.ReuseAsync" && boundary == "begin") reuses++;
                        if (drift && !injected && boundary == "begin" &&
                            name == (fenceChange is null ? "JointProof.ValidateResourcesAsync" : "OperationFileData.FreshFence"))
                        {
                            injected = true;
                            var path = Path.Combine(root, "game_session", (fenceChange == "continuation"
                                ? AcceptedMechanicsPlan.WoundCommandPath : WoundHistoryState.HistoryPath).Replace('/', Path.DirectorySeparatorChar));
                            if (fenceChange == "continuation") { Assert.False(File.Exists(path)); File.WriteAllText(path, "{}"); }
                            else File.AppendAllText(path, "\n ");
                            damaged = CaptureImages(root);
                        }
                    }
                });
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            var generation = files.GetOrCreateSessionGeneration(lease);
            var before = CaptureImages(root);
            var tuple = manifest["Provenance"]!["Original"]!.AsObject();
            await SessionOperationContext.RunBoundAsync(files, generation, lease, async () =>
            {
                await using var entry = outerReserved ? files.BeginParticipatingMainAdmission() : null;
                if (entry is not null) await entry.AcquireAsync();
                await using var reserved = outerReserved ? files.TryBeginReservedValidationReadSet(lease) : null;
                measuring = true;
                try
                {
                    if (outerReserved) { Assert.NotNull(reserved); await reserved.OpenAsync(); }
                    using var operation = owningOperation ? files.BeginSourceDataOperationIfNeeded() : null;
                    Task Read() => MortalQteDecisionPublication.ValidateCurrentJointCompletionAsync(files, lease,
                        tuple["SessionId"]!.GetValue<string>(), tuple["RequestId"]!.GetValue<string>(),
                        tuple["SnapshotToken"]!.GetValue<string>(), tuple["TurnNumber"]!.GetValue<int>());
                    for (var repetition = 0; repetition < repetitions; repetition++)
                    {
                        if (drift)
                        {
                            var escaped = false;
                            var refusal = await Assert.ThrowsAsync<InvalidDataException>(async () => { await Read(); escaped = true; });
                            Assert.False(escaped);
                            if (fenceChange is not null)
                            {
                                Assert.Contains(fenceChange == "continuation" ? AcceptedMechanicsPlan.WoundCommandPath : WoundHistoryState.HistoryPath,
                                    refusal.Message, StringComparison.Ordinal);
                                Assert.Equal(1, fences);
                            }
                        }
                        else await Read();
                    }
                    if (operation is not null) await operation.CompleteAsync();
                    if (reserved is not null)
                    {
                        var other = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
                        var writer = await Assert.ThrowsAsync<InvalidOperationException>(() => other.WriteFileAtomicAsync("other.txt", "forbidden"));
                        Assert.Contains("reserv", writer.Message, StringComparison.OrdinalIgnoreCase);
                        Assert.NotNull(entry);
                        var closing = Assert.Throws<InvalidOperationException>(entry.BeginClosing);
                        Assert.Contains("validation", closing.Message, StringComparison.OrdinalIgnoreCase);
                        Assert.False(entry.BoundClosing);
                        Assert.False(entry.Closed);
                        await reserved.CompleteAsync();
                        await reserved.DisposeAsync();
                        entry.BeginClosing();
                        Assert.True(entry.BoundClosing);
                    }
                }
                finally { measuring = false; }
                return true;
            });
            var after = CaptureImages(root);
            Assert.Equal((drift ? Assert.IsType<Dictionary<string, string>>(damaged) : before).OrderBy(x => x.Key), after.OrderBy(x => x.Key));
            if (drift) Assert.True(injected);
            return new(attributes, opens, reuses, after, sourceReads, fences);
        }
        finally
        {
            raw.Dispose();
            using var input = File.OpenRead(diagnosticPath);
            output.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_proof_diagnostics", Path = diagnosticPath,
                Bytes = input.Length, SHA256 = Convert.ToHexString(SHA256.HashData(input)), Complete = true }));
            Assert.Equal(archiveHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archive))));
        }
    }

    /// <summary>
    /// Captures exact game-file identities without including runtime lock metadata.
    /// </summary>
    /// <param name="root">
    /// The verified per-case copy's physical root.
    /// </param>
    /// <returns>
    /// Every game-session path and current content hash.
    /// </returns>
    private static Dictionary<string, string> CaptureImages(string root) =>
        Directory.EnumerateFiles(Path.Combine(root, "game_session"), "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(root, path), path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.Ordinal);
}
