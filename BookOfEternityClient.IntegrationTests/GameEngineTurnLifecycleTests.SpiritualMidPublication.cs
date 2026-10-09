using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Reopens an exact independently copied mixed publication cut and requires complete restoration or complete acceptance.
    /// The warm caller finishes independently; this deterministic durable-image probe does not simulate an operating-system kill.
    /// </summary>
    /// <returns>
    /// A task completing after genuine publication, mixed-cut evidence, fresh-root recovery and terminal cleanup are verified.
    /// </returns>
    [Fact]
    public async Task SpiritualMidPublication_ColdCopiedCutRecoversAtomically()
    {
        var input = new QueuedConsoleInputSource([]);
        var logger = new SpiritualLifecycleTestLogger();
        var checkpoints = new ConcurrentQueue<string>();
        GameEngine? engine = null;
        ResourceMaterializationTestContext? physical = null;
        SpiritualLifecyclePublicationWitness? witness = null;
        Dictionary<string, byte[]>? cut = null;
        Dictionary<string, byte[]>? committed = null;
        byte[]? originalStory = null;
        byte[]? generationBytes = null;
        var captureCount = 0;
        var request = new TurnRequest
        {
            SessionId = "session_engine_spiritual", RequestId = "request_engine_spiritual_42", TurnNumber = 42,
            PlayerAction = "Удержать встречное духовное давление.", Timestamp = DateTime.UtcNow.ToString("O"),
            PreGeneratedDices1d20 = [15, 5, 12, 8]
        };
        var copied = await ResourceMaterializationTestContext.CreateAsync();
        using var ownedCopy = new OriginalFixtureCompletion(copied.RootPath,
            () => copied.DisposeAsync().GetAwaiter().GetResult(), text => _directGachaOutput?.WriteLine(text));
        Assert.Empty(await ReadSpiritualEntryGuardFilesAsync(copied));
        var hooks = new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, index) => { witness?.Observe(phase, index); },
            BeforeCanonicalMutationAsync = async path =>
            {
                if (physical is null || cut is not null || path != WoundIdentityState.StatePath)
                    return;
                // The hook runs before the next file's temporary or reversible-publication journal is created.
                // Read physical files without trying to reacquire the publisher's canonical lease.
                var definitions = ResourceDefinitionCatalog.ParseCanonical(await File.ReadAllTextAsync(
                    physical.FileSystem.ResolvePath(ResourceMaterializationContract.DefinitionsPath)), allowMissingPristine: false);
                Assert.True(definitions.IsValid);
                var state = ResourceStateContract.ParseCanonical(await File.ReadAllTextAsync(
                    physical.FileSystem.ResolvePath(ResourceMaterializationContract.StatePath)),
                    definitions.Catalog!, allowMissingPristine: false);
                Assert.True(state.IsValid);
                var points = state.Ledger!.Entries.Where(row => row.Coordinate.ResourceKey == "spiritual_action_points").ToArray();
                if (points.Length != 2 || points.Any(row => row.Current != 3m))
                    return;
                witness!.RequireLatestCurrent(ResourceMaterializationContract.StatePath);
                witness.AssertSettled("mid-original-between-committed-publications");
                cut = await ReadSpiritualEntryGuardFilesAsync(physical);
                generationBytes = await File.ReadAllBytesAsync(physical.FileSystem.SessionGenerationPath);
                Assert.True(!Directory.Exists(physical.FileSystem.PhysicalPublicationTransactionsRootPath) ||
                    !Directory.EnumerateFiles(physical.FileSystem.PhysicalPublicationTransactionsRootPath, "*", SearchOption.AllDirectories).Any(),
                    "The cut must be between completed per-file publications, not omit an active per-file recovery journal.");
                await CopySpiritualMidPublicationImagesAsync(copied, cut, generationBytes);
                await AssertSpiritualEntryGuardFilesAsync(copied, cut);
                Assert.Equal(generationBytes, File.ReadAllBytes(copied.FileSystem.SessionGenerationPath));
                Assert.False(File.Exists(Path.Combine(copied.FileSystem.RuntimeRootPath,
                    "trusted-local-publication-v1", "active.json")));
                captureCount++;
            }
        };
        var finalization = new GameEngineSessionFinalizationHooks
        {
            AtCheckpointAsync = async checkpoint =>
            {
                checkpoints.Enqueue($"{DateTime.UtcNow:O} {checkpoint}");
                while (checkpoints.Count > 24) checkpoints.TryDequeue(out _);
                if (checkpoint != SessionFinalizationCheckpoint.AcceptedOutcomeValidatedBeforeMaterialization)
                    return;
                Assert.NotNull(physical);
                Assert.NotNull(cut);
                await AssertSpiritualInterruptionPublicationAsync(physical!);
                await AssertSpiritualInterruptionOwnersSettledAsync(physical!.FileSystem);
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await using var lease = await physical.FileSystem.AcquireCanonicalWriteLeaseAsync(cancellationToken: deadline.Token);
                committed = await ReadSpiritualEntryGuardFilesAsync(physical);
            }
        };
        var context = await AfterlifeResourceCutoverTests.CreateSpiritualGameEngineOriginalAsync(async baseline =>
        {
            var chat = Assert.IsType<JsonObject>(await baseline.ReadJsonAsync("game_state/history/chat_log.json"));
            chat["sessionId"] = request.SessionId;
            await baseline.WriteExactJsonAsync("game_state/history/chat_log.json", chat.ToJsonString());
            engine = CreateGameEngine(input, finalizationHooks: finalization, fileSystem: baseline.FileSystem, logger: logger);
            await InvokePrivateTaskAsync(engine, "RefreshRuntimeStateAsync");
            await InvokePrivateTaskAsync(engine, "EnsureClientOwnedSystemFilesHealthyAsync");
            GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(request.SessionId, 41);
            await new StoryService(baseline.FileSystem, NullLogger<StoryService>.Instance).AppendTurnAsync(
                41, "Chaos Sea", 0, "Предыдущий завершённый ход.", "Душа готовится к духовному обмену.");
            Assert.Equal(41, Assert.IsType<int>(await InvokePrivateTaskResultAsync(engine, "DetectCurrentSessionTurnNumberAsync")));
            originalStory = Assert.IsType<byte[]>(await baseline.FileSystem.ReadFileBytesAsync("stories/chaos_sea.jsonl"));
            request.ProgressionControl = await new ProgressionScheduleService(baseline.FileSystem,
                NullLogger<ProgressionScheduleService>.Instance).BuildControlForNextTurnAsync("Chaos Sea");
            var rollback = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "mid-publication-spiritual-original");
            await baseline.WriteExactJsonAsync("input/turn_request.json", JsonSerializer.Serialize(request, SnapshotHashJsonOpts));
            await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, rollback, "mid-publication-spiritual-original");
        }, hooks: hooks);
        using var owned = new OriginalFixtureCompletion(context.RootPath,
            () => context.DisposeAsync().GetAwaiter().GetResult(), text => _directGachaOutput?.WriteLine(text));
        physical = context;
        witness = new(context.FileSystem, text => _directGachaOutput?.WriteLine(text),
            ResourceMaterializationContract.StatePath);
        var manifest = Assert.IsType<JsonObject>(await context.ReadJsonAsync("game_state/control/pending_turn_snapshot.json"));
        var original = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var path in manifest["rollbackBaselineFiles"]!.AsArray().Select(value => value!.GetValue<string>()))
            original.Add(path, Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync(
                manifest["rollbackBackups"]![path]!.GetValue<string>())));
        Assert.NotEmpty(original);
        await AfterlifeResourceCutoverTests.WriteSpiritualGameEngineExchangeAsync(context);
        await WriteSpiritualLifecycleOutputsAsync(context, request);
        var originalPacket = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var path in new[] { "input/turn_request.json", "ready/turn_complete.json",
                     "game_state/control/pending_turn_snapshot.json", PendingTurnSnapshotAuthority.AuthorityPath })
            originalPacket.Add(path, Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync(path)));

        var warmObservation = new SpiritualInterruptionObservation();
        using (var stop = new CancellationTokenSource())
        {
            var responder = ObserveSpiritualInterruptionRequestAsync(context, request, warmObservation, respond: true, stop.Token);
            var warm = await AwaitSpiritualInterruptionPhaseAsync(InvokePrivateTaskResultAsync(engine!, "WaitForGmResponse"),
                responder, warmObservation, stop, input, TimeSpan.FromSeconds(420));
            var diagnostics = await ReadSequentialSpiritualFailureDiagnosticsAsync(context.FileSystem, checkpoints);
            Assert.True(warm.Error is null && warm.Cleanup is null && warm.Result is true,
                $"Warm result={warm.Result}; error={warm.Error}; cleanup={warm.Cleanup}; captures={captureCount}; " +
                $"requests={warmObservation.Responses}; repair={warmObservation.LastRepair}. {logger.Describe()}\n{diagnostics}");
        }
        witness.AssertSettled("mid-warm-actors-settled");
        Assert.Equal(1, captureCount);
        Assert.Equal(1, warmObservation.Responses);
        Assert.NotNull(cut);
        Assert.NotNull(committed);
        Assert.NotNull(generationBytes);
        Assert.Equal(42, GetPrivateField<GameLoop>(engine!, "_gameLoop").TurnNumber);
        AssertSpiritualMidPublicationMixedCut(original, cut!, committed!, originalPacket);
        // The completed warm caller cannot supply cold state: the separate copied tree must still be the exact earlier cut.
        await AssertSpiritualEntryGuardFilesAsync(copied, cut!);
        Assert.Equal(generationBytes, await File.ReadAllBytesAsync(copied.FileSystem.SessionGenerationPath));
        Assert.NotEqual(context.RootPath, copied.RootPath);

        Assert.False(File.Exists(Path.Combine(copied.FileSystem.RuntimeRootPath,
            "trusted-local-publication-v1", "active.json")));
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { kind = "mid-copied-cut-before-cold-admission",
            root = copied.RootPath, generationBytes, copiedGeneration = File.ReadAllBytes(copied.FileSystem.SessionGenerationPath),
            images = cut, journalAbsent = true }));
        var coldFs = new FileSystemManager(copied.RootPath, NullLogger<FileSystemManager>.Instance);
        var coldInput = new QueuedConsoleInputSource([]);
        var coldEngine = CreateGameEngine(coldInput, fileSystem: coldFs, logger: logger);
        await AssertSpiritualInterruptionOwnersSettledAsync(coldFs);
        await InvokePrivateTaskAsync(coldEngine, "RefreshRuntimeStateAsync");
        var persistedTurn = Assert.IsType<int>(await InvokePrivateTaskResultAsync(coldEngine, "DetectCurrentSessionTurnNumberAsync"));
        Assert.Equal(41, persistedTurn);
        var persistedSession = GetPrivateField<StateManager>(coldEngine, "_stateManager").CurrentState.SessionId;
        Assert.Equal(request.SessionId, persistedSession);
        GetPrivateField<GameLoop>(coldEngine, "_gameLoop").SetSession(persistedSession, persistedTurn);
        var coldObservation = new SpiritualInterruptionObservation();
        using (var stop = new CancellationTokenSource())
        {
            var observer = ObserveSpiritualInterruptionRequestAsync(copied, request, coldObservation, respond: false, stop.Token);
            var cold = await AwaitSpiritualInterruptionPhaseAsync(RunSpiritualInterruptionColdStartupAsync(coldEngine),
                observer, coldObservation, stop, coldInput, TimeSpan.FromSeconds(180));
            var diagnostics = await ReadSequentialSpiritualFailureDiagnosticsAsync(coldFs, checkpoints);
            Assert.True(cold.Error is null && cold.Cleanup is null && coldObservation.LastRepair is null,
                $"Cold result={cold.Result}; error={cold.Error}; cleanup={cold.Cleanup}; repair={coldObservation.LastRepair}; " +
                $"turn={GetPrivateField<GameLoop>(coldEngine, "_gameLoop").TurnNumber}. {logger.Describe()}\n{diagnostics}");
        }
        Assert.Equal(0, coldObservation.Responses);
        await AssertSpiritualInterruptionAtomicOutcomeAsync(copied, coldEngine, original, originalStory!, committed!, request,
            logger.Describe());
    }

    /// <summary>
    /// Copies detached cut bytes into an independently owned empty root, retaining the actual generation without runtime owners.
    /// </summary>
    /// <param name="destination">
    /// Fresh isolated test context whose session tree is empty.
    /// </param>
    /// <param name="images">
    /// Complete physically enumerated session images captured while the warm publisher holds its lease.
    /// </param>
    /// <param name="generation">
    /// Exact existing session-generation metadata; lock files and process-local claims are not copied.
    /// </param>
    /// <returns>
    /// A task completing after every detached session image and the generation metadata have been persisted.
    /// </returns>
    private static async Task CopySpiritualMidPublicationImagesAsync(ResourceMaterializationTestContext destination,
        Dictionary<string, byte[]> images, byte[] generation)
    {
        foreach (var pair in images)
            await destination.WriteExactBytesAsync(pair.Key, pair.Value.ToArray());
        var path = destination.FileSystem.SessionGenerationPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, generation.ToArray());
    }

    /// <summary>
    /// Proves exact published resource bytes coexist with an unpublished wound index and unconsumed private roots.
    /// </summary>
    /// <param name="original">
    /// Genuine signed rollback baseline, including original path absence through missing entries.
    /// </param>
    /// <param name="cut">
    /// Physical images copied before the next common-writer mutation.
    /// </param>
    /// <param name="committed">
    /// Independently captured genuine completed publication, before story append.
    /// </param>
    /// <param name="originalPacket">
    /// Exact request, terminal and signed identity metadata from before the GM response.
    /// </param>
    private static void AssertSpiritualMidPublicationMixedCut(Dictionary<string, byte[]> original,
        Dictionary<string, byte[]> cut, Dictionary<string, byte[]> committed, Dictionary<string, byte[]> originalPacket)
    {
        var mixed = cut.ToDictionary(pair => pair.Key.Replace('\\', '/'), pair => pair.Value, StringComparer.Ordinal);
        var final = committed.ToDictionary(pair => pair.Key.Replace('\\', '/'), pair => pair.Value, StringComparer.Ordinal);
        var resourcePath = ResourceMaterializationContract.StatePath;
        Assert.False(original[resourcePath].AsSpan().SequenceEqual(mixed[resourcePath]));
        Assert.Equal(final[resourcePath], mixed[resourcePath]);
        var woundPath = WoundIdentityState.StatePath;
        Assert.True(final.ContainsKey(woundPath));
        original.TryGetValue(woundPath, out var beforeWounds);
        mixed.TryGetValue(woundPath, out var cutWounds);
        Assert.Equal(beforeWounds, cutWounds);
        Assert.True(cutWounds is null || !cutWounds.AsSpan().SequenceEqual(final[woundPath]));
        foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                     SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
        {
            Assert.True(mixed.ContainsKey(path), path);
            Assert.False(final.ContainsKey(path), path);
        }
        foreach (var pair in originalPacket)
            Assert.Equal(pair.Value, mixed[pair.Key]);
    }
}
