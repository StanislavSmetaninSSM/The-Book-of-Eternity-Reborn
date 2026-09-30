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
    /// Publishes one original binding turn only after the selected wound and separately correlated A/B Ready responses.
    /// </summary>
    /// <returns>
    /// A task completing after actual engine publication and idle replay preservation.
    /// </returns>
    [Fact]
    public Task SpiritualBindingContinuation_FileRepliesPublishOnce() => RunBindingLifecycleAsync(false);

    /// <summary>
    /// Recovers accepted force-binding A in a fresh engine and requires its separate terminal B response before publication.
    /// </summary>
    /// <returns>
    /// A task completing after cold original-owner replay, B Ready and once-only publication.
    /// </returns>
    [Fact]
    public Task SpiritualBindingContinuation_ColdAfterARequiresFinalReady() => RunBindingLifecycleAsync(true);

    /// <summary>
    /// Runs the two-exchange binding scenario through real public requests and an optional cold terminal boundary.
    /// </summary>
    /// <param name="strong">
    /// Uses force binding and interrupts after B publication; ordinary binding completes warm.
    /// </param>
    /// <returns>
    /// A task completing after saved-choice, resource, canonical-control and duplicate-publication assertions.
    /// </returns>
    private async Task RunBindingLifecycleAsync(bool strong)
    {
        var input = new QueuedConsoleInputSource([]);
        var logger = new SpiritualLifecycleTestLogger();
        var observed = new SpiritualStagedObservation();
        ResourceMaterializationTestContext? physical = null;
        GameEngine? engine = null;
        var bPublished = 0;
        var interrupted = 0;
        var milestones = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var hooks = new FileSystemManagerHooks
        {
            AfterPhysicalFilePublishedAsync = async path =>
            {
                if (physical is null) return;
                foreach (var relative in new[] { SpiritualWoundOpportunityReceiptState.StatePath,
                    ResourceMaterializationContract.StatePath, WoundIdentityState.StatePath, "stories/chaos_sea.jsonl" })
                    if (path == physical.FileSystem.ResolvePath(relative)) milestones.Enqueue(DateTime.UtcNow.ToString("O") + " " + relative);
                if (path != physical.FileSystem.ResolvePath("game_state/control/validation_repair_request.json")) return;
                var root = ParseDependentSpiritualBytes(await ReadSpiritualStagedPublishedBytesAsync(path));
                if (root["spiritualWoundContinuation"]?["dependentDraftFields"] is JsonArray fields &&
                    fields.Count == 1 && fields[0]?["jsonPointer"]?.GetValue<string>() == "/activeConflict/controlState")
                {
                    milestones.Enqueue(DateTime.UtcNow.ToString("O") + " terminal B published");
                    Volatile.Write(ref bPublished, 1);
                }
            },
            BeforeCanonicalWriteLockOpenAsync = () =>
            {
                if (strong && Volatile.Read(ref bPublished) == 1 && Interlocked.CompareExchange(ref interrupted, 1, 0) == 0)
                    throw new OperationCanceledException("Binding A is durable; separate B is unanswered.");
                return Task.CompletedTask;
            }
        };
        var request = new TurnRequest
        {
            SessionId = "session_engine_spiritual", RequestId = "request_engine_spiritual_42", TurnNumber = 42,
            PlayerAction = "Удержать встречное духовное давление.", Timestamp = DateTime.UtcNow.ToString("O"),
            PreGeneratedDices1d20 = [5, 15, 13, strong ? 11 : 10]
        };
        await using var context = await AfterlifeResourceCutoverTests.CreateSpiritualGameEngineOriginalAsync(async original =>
        {
            await AfterlifeResourceCutoverTests.SeedOriginalLastBindingInputsAsync(original, strong, mixed: false, seedIntake: false);
            var chat = Assert.IsType<JsonObject>(await original.ReadJsonAsync("game_state/history/chat_log.json"));
            chat["sessionId"] = request.SessionId;
            await original.WriteExactJsonAsync("game_state/history/chat_log.json", chat.ToJsonString());
            engine = CreateGameEngine(input, fileSystem: original.FileSystem, logger: logger);
            await InvokePrivateTaskAsync(engine, "RefreshRuntimeStateAsync");
            await InvokePrivateTaskAsync(engine, "EnsureClientOwnedSystemFilesHealthyAsync");
            await new StoryService(original.FileSystem, NullLogger<StoryService>.Instance).AppendTurnAsync(
                41, "Chaos Sea", 0, "Предыдущий завершённый ход.", "Душа готовится к духовному обмену.");
            GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(request.SessionId, 41);
            request.ProgressionControl = await new ProgressionScheduleService(original.FileSystem,
                NullLogger<ProgressionScheduleService>.Instance).BuildControlForNextTurnAsync("Chaos Sea");
            var backup = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "binding-original");
            await original.WriteExactJsonAsync("input/turn_request.json", JsonSerializer.Serialize(request, SnapshotHashJsonOpts));
            await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, backup, "binding-original");
        }, hooks: hooks);
        physical = context;
        var originalDraft = await AfterlifeResourceCutoverTests.WriteOriginalLastBindingDraftAsync(context, strong, "lost");
        await WriteBindingLifecycleOutputsAsync(context, request, strong);
        var originalImages = await ReadSpiritualStagedPhysicalImagesAsync(context);
        var responderFs = new FileSystemManager(context.RootPath, NullLogger<FileSystemManager>.Instance);
        using (var stop = new CancellationTokenSource())
        {
            var responder = ObserveBindingLifecycleAsync(responderFs, input, request, originalDraft, observed, strong,
                withholdFinal: strong, stop.Token);
            var warm = await AwaitDependentSpiritualPhaseAsync(InvokePrivateTaskResultAsync(engine!, "WaitForGmResponse"),
                responder, stop, input, observed.Phase, logger, TimeSpan.FromMinutes(strong ? 8 : 11),
                describeTimeoutBoundary: () => string.Join("; ", milestones));
            if (strong) Assert.IsAssignableFrom<OperationCanceledException>(warm.Error);
            else Assert.True(warm.Error is null && warm.Result is true, $"{warm.Error}; {logger.Describe()}");
        }
        var acceptedEngine = engine!;
        if (strong)
        {
            Assert.Equal(1, interrupted);
            Assert.Equal(41, GetPrivateField<GameLoop>(engine!, "_gameLoop").TurnNumber);
            Assert.Equal(2, observed.Phase.FileResponses);
            var held = await ReadSpiritualStagedPhysicalImagesAsync(context);
            foreach (var path in new[] { "input/turn_request.json", ResourceMaterializationContract.StatePath,
                ResourceMaterializationContract.HistoryPath, WoundIdentityState.StatePath, WoundHistoryState.HistoryPath,
                EffectIdentityState.StatePath, SpiritualWoundOpportunityReceiptState.StatePath, "stories/chaos_sea.jsonl" })
                Assert.Equal(originalImages[path], held[path]);
            var checkpoint = ParseDependentSpiritualBytes(held[SpiritualWoundCaptureCheckpointState.StatePath]!);
            var progress = Assert.Single(checkpoint["checkpoint"]!["pendingSubmission"]!["dependentDraftProgress"]!.AsArray())!;
            Assert.Equal(observed.A!.ContinuationId, progress["acceptedContinuationId"]!.GetValue<string>());
            Assert.Equal(observed.Command, held[AcceptedMechanicsPlan.WoundCommandPath]);
            Assert.Null(held["game_state/control/validation_repair_ready.json"]);
            var waiting = ParseDependentSpiritualBytes(held["game_state/control/validation_repair_request.json"]!);
            Assert.Equal("/activeConflict/controlState", Assert.Single(waiting["spiritualWoundContinuation"]!["dependentDraftFields"]!.AsArray())!["jsonPointer"]!.GetValue<string>());
            var coldInput = new QueuedConsoleInputSource([]);
            var coldFs = new FileSystemManager(context.RootPath, NullLogger<FileSystemManager>.Instance);
            acceptedEngine = CreateGameEngine(coldInput, fileSystem: coldFs, logger: logger);
            await InvokePrivateTaskAsync(acceptedEngine, "RefreshRuntimeStateAsync");
            var persisted = Assert.IsType<int>(await InvokePrivateTaskResultAsync(acceptedEngine, "DetectCurrentSessionTurnNumberAsync"));
            Assert.Equal(41, persisted);
            GetPrivateField<GameLoop>(acceptedEngine, "_gameLoop").SetSession(request.SessionId, persisted);
            using var stop = new CancellationTokenSource();
            var responder = ObserveBindingLifecycleAsync(responderFs, coldInput, request, originalDraft, observed, strong,
                withholdFinal: false, stop.Token);
            var cold = await AwaitDependentSpiritualPhaseAsync(RunSpiritualInterruptionColdStartupAsync(acceptedEngine),
                responder, stop, coldInput, observed.Phase, logger, TimeSpan.FromMinutes(8));
            Assert.True(cold.Error is null && cold.Result is false, $"{cold.Error}; {logger.Describe()}");
        }
        Assert.Equal(3, observed.Phase.Requests.Count);
        Assert.Equal(3, observed.Phase.FileResponses);
        Assert.NotEqual(observed.A!.ContinuationId, observed.B!.ContinuationId);
        await AssertBindingLifecyclePublicationAsync(context, acceptedEngine, originalDraft, observed, strong);
    }
}
