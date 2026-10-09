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
    /// Interrupts the actual caller after common publication and requires cold recovery to restore or commit the entire turn.
    /// </summary>
    /// <returns>
    /// A task completing after the preserved pre-story cut and the all-restored-or-all-committed recovery oracle are checked.
    /// </returns>
    [Fact]
    public Task SpiritualInterruption_PostPublicationBeforeStoryRecoversAtomically() =>
        AssertSpiritualInterruptionRecoveryAsync(afterStory: false);

    /// <summary>
    /// Interrupts the actual caller before terminal deletion after story append and checks the same atomic recovery oracle.
    /// </summary>
    /// <returns>
    /// A task completing after the preserved post-story cut and recovery without duplicate publication or story are checked.
    /// </returns>
    [Fact]
    public Task SpiritualInterruption_PostStoryBeforeTerminalCleanupRecoversAtomically() =>
        AssertSpiritualInterruptionRecoveryAsync(afterStory: true);

    /// <summary>
    /// Uses a genuine signed original, one file decision and deterministic interruption, without simulating an operating-system kill.
    /// </summary>
    /// <param name="afterStory">
    /// Whether to interrupt before Ready deletion after story append; otherwise interrupts at the post-publication checkpoint.
    /// </param>
    /// <returns>
    /// A task completing after a new engine processes the untouched durable cut with no GM response during recovery.
    /// </returns>
    private async Task AssertSpiritualInterruptionRecoveryAsync(bool afterStory)
    {
        var input = new QueuedConsoleInputSource([]);
        var logger = new SpiritualLifecycleTestLogger();
        var checkpoints = new ConcurrentQueue<string>();
        GameEngine? engine = null;
        ResourceMaterializationTestContext? physical = null;
        Dictionary<string, byte[]>? cut = null;
        byte[]? originalStory = null;
        var published = false;
        var interruptions = 0;
        var publicationWrites = 0;
        var request = new TurnRequest
        {
            SessionId = "session_engine_spiritual", RequestId = "request_engine_spiritual_42", TurnNumber = 42,
            PlayerAction = "Удержать встречное духовное давление.", Timestamp = DateTime.UtcNow.ToString("O"),
            PreGeneratedDices1d20 = [15, 5, 12, 8]
        };
        var fileHooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = async path =>
            {
                if (physical is null) return;
                if (path == SpiritualWoundOpportunityReceiptState.StatePath)
                    Interlocked.Increment(ref publicationWrites);
                if (!afterStory || !published || path != "ready/turn_complete.json" || interruptions != 0)
                    return;
                // This hook already runs under the deletion's canonical lease: read physical bytes without nesting a lease.
                var story = await File.ReadAllTextAsync(physical.FileSystem.ResolvePath("stories/chaos_sea.jsonl"));
                Assert.Contains(story.Split('\n', StringSplitOptions.RemoveEmptyEntries),
                    line => JsonSerializer.Deserialize<StoryEntry>(line.TrimStart('\uFEFF'))?.Turn == 42);
                cut = await ReadSpiritualEntryGuardFilesAsync(physical);
                Interlocked.Increment(ref interruptions);
                throw new OperationCanceledException("Probe interrupted before accepted terminal deletion after story append.");
            }
        };
        var finalization = new GameEngineSessionFinalizationHooks
        {
            AtCheckpointAsync = async checkpoint =>
            {
                checkpoints.Enqueue($"{DateTime.UtcNow:O} {checkpoint}");
                while (checkpoints.Count > 24) checkpoints.TryDequeue(out _);
                if (checkpoint != SessionFinalizationCheckpoint.AcceptedOutcomeValidatedBeforeMaterialization) return;
                Assert.NotNull(physical);
                await AssertSpiritualInterruptionPublicationAsync(physical!);
                await AssertSpiritualInterruptionOwnersSettledAsync(physical!.FileSystem);
                published = true;
                if (afterStory) return;
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await using (var lease = await physical.FileSystem.AcquireCanonicalWriteLeaseAsync(cancellationToken: deadline.Token))
                    cut = await ReadSpiritualEntryGuardFilesAsync(physical);
                Interlocked.Increment(ref interruptions);
                throw new OperationCanceledException("Probe interrupted after common publication before story append.");
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
            var stories = new StoryService(baseline.FileSystem, NullLogger<StoryService>.Instance);
            await stories.AppendTurnAsync(41, "Chaos Sea", 0, "Предыдущий завершённый ход.", "Душа готовится к духовному обмену.");
            Assert.Equal(41, Assert.IsType<int>(await InvokePrivateTaskResultAsync(engine, "DetectCurrentSessionTurnNumberAsync")));
            originalStory = Assert.IsType<byte[]>(await baseline.FileSystem.ReadFileBytesAsync("stories/chaos_sea.jsonl"));
            request.ProgressionControl = await new ProgressionScheduleService(baseline.FileSystem,
                NullLogger<ProgressionScheduleService>.Instance).BuildControlForNextTurnAsync("Chaos Sea");
            var rollback = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "interrupted-spiritual-original");
            await baseline.WriteExactJsonAsync("input/turn_request.json", JsonSerializer.Serialize(request, SnapshotHashJsonOpts));
            await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, rollback, "interrupted-spiritual-original");
        }, hooks: fileHooks);
        using var owned = new OriginalFixtureCompletion(context.RootPath,
            () => context.DisposeAsync().GetAwaiter().GetResult(), text => _directGachaOutput?.WriteLine(text));
        var storageWitness = new SpiritualLifecyclePublicationWitness(context.FileSystem,
            text => _directGachaOutput?.WriteLine(text));
        physical = context;
        var manifest = Assert.IsType<JsonObject>(await context.ReadJsonAsync("game_state/control/pending_turn_snapshot.json"));
        var original = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        // Signing can initialize canonical afterlife roots and register their exact bytes as additional rollback evidence.
        foreach (var path in manifest["rollbackBaselineFiles"]!.AsArray().Select(value => value!.GetValue<string>()))
        {
            var backupPath = manifest["rollbackBackups"]![path]!.GetValue<string>();
            original.Add(path, Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync(backupPath)));
        }
        Assert.NotEmpty(original);
        await AfterlifeResourceCutoverTests.WriteSpiritualGameEngineExchangeAsync(context);
        await WriteSpiritualLifecycleOutputsAsync(context, request);
        var originalReady = Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync("ready/turn_complete.json"));
        var signedOriginal = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var path in new[] { "input/turn_request.json", "game_state/control/pending_turn_snapshot.json",
                     "game_state/control/pending_turn_snapshot.authority.json" })
            signedOriginal.Add(path, Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync(path)));
        var observation = new SpiritualInterruptionObservation();
        using (var stop = new CancellationTokenSource())
        {
            var responder = ObserveSpiritualInterruptionRequestAsync(context, request, observation, respond: true, stop.Token);
            var warm = await AwaitSpiritualInterruptionPhaseAsync(InvokePrivateTaskResultAsync(engine!, "WaitForGmResponse"),
                responder, observation, stop, input, TimeSpan.FromSeconds(420));
            var diagnostics = await ReadSequentialSpiritualFailureDiagnosticsAsync(context.FileSystem, checkpoints);
            Assert.True(warm.Error is OperationCanceledException && warm.Cleanup is null,
                $"Warm result={warm.Result}; error={warm.Error}; cleanup={warm.Cleanup}; requests={observation.Responses}; " +
                $"last repair={observation.LastRepair}. {logger.Describe()}\n{diagnostics}");
        }
        Assert.True(published);
        Assert.Equal(1, interruptions);
        Assert.Equal(1, publicationWrites);
        Assert.Equal(1, observation.Responses);
        Assert.NotNull(cut);
        await AssertSpiritualEntryGuardFilesAsync(context, cut!);
        Assert.Equal(originalReady, await context.FileSystem.ReadFileBytesAsync("ready/turn_complete.json"));
        foreach (var pair in signedOriginal)
            Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(pair.Key));
        await AssertSpiritualInterruptionOwnersSettledAsync(context.FileSystem);
        Assert.Equal(afterStory ? 42 : 41, GetPrivateField<GameLoop>(engine!, "_gameLoop").TurnNumber);

        var coldFs = new FileSystemManager(context.RootPath, NullLogger<FileSystemManager>.Instance);
        var coldInput = new QueuedConsoleInputSource([]);
        var coldEngine = CreateGameEngine(coldInput, fileSystem: coldFs, logger: logger);
        await InvokePrivateTaskAsync(coldEngine, "RefreshRuntimeStateAsync");
        var persistedTurn = Assert.IsType<int>(await InvokePrivateTaskResultAsync(coldEngine, "DetectCurrentSessionTurnNumberAsync"));
        Assert.Equal(afterStory ? 42 : 41, persistedTurn);
        GetPrivateField<GameLoop>(coldEngine, "_gameLoop").SetSession(request.SessionId, persistedTurn);
        var coldObservation = new SpiritualInterruptionObservation();
        using (var stop = new CancellationTokenSource())
        {
            var observer = ObserveSpiritualInterruptionRequestAsync(context, request, coldObservation, respond: false, stop.Token);
            var operation = RunSpiritualInterruptionColdStartupAsync(coldEngine);
            var cold = await AwaitSpiritualInterruptionPhaseAsync(operation, observer, coldObservation, stop, coldInput,
                TimeSpan.FromSeconds(180));
            var diagnostics = await ReadSequentialSpiritualFailureDiagnosticsAsync(coldFs, checkpoints);
            Assert.True(cold.Error is null && cold.Cleanup is null && coldObservation.LastRepair is null,
                $"Cold result={cold.Result}; error={cold.Error}; cleanup={cold.Cleanup}; repair={coldObservation.LastRepair}; " +
                $"turn={GetPrivateField<GameLoop>(coldEngine, "_gameLoop").TurnNumber}. {logger.Describe()}\n{diagnostics}");
        }
        await AssertSpiritualInterruptionAtomicOutcomeAsync(context, coldEngine, original, originalStory!, cut!, request,
            logger.Describe());
        storageWitness.AssertSettled("additional-spiritual-original-actors-settled");
    }
}
