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
    /// Completes actual file-GM A and B responses and publishes their one original turn once.
    /// </summary>
    /// <returns>
    /// A task completing after exact staged transport, resources, wound, narrative and cleanup checks.
    /// </returns>
    [Fact]
    public Task SpiritualStagedContinuation_FileAThenBPublishesOnce() => RunSpiritualStagedLifecycleAsync(false, "none");

    /// <summary>
    /// Recovers an actual worker-selected wound in a fresh engine and completes separate worker A and B proposals before one publication.
    /// </summary>
    /// <returns>
    /// A task completing after actual worker apply/Ready evidence and canonical publication checks.
    /// </returns>
    [Fact]
    [Trait("Category", "ProcessIntegration")]
    public Task SpiritualStagedContinuation_WorkerAThenBPublishesOnce() => RunSpiritualStagedLifecycleAsync(true, "decision");

    /// <summary>
    /// Recovers the exact file A correction before Ready and requires its own response before B.
    /// </summary>
    /// <returns>
    /// A task completing after genuine warm interruption and fresh-engine publication.
    /// </returns>
    [Fact]
    public Task SpiritualStagedContinuation_ColdAfterAApplyRequiresAReadyThenB() => RunSpiritualStagedLifecycleAsync(false, "a_apply");

    /// <summary>
    /// Recovers an existing A Ready before its engine read and continues through a separate B response.
    /// </summary>
    /// <returns>
    /// A task completing after the original transport is authenticated during cold recovery.
    /// </returns>
    [Fact]
    public Task SpiritualStagedContinuation_ColdAfterAReadyAuthenticatesBeforeB() => RunSpiritualStagedLifecycleAsync(false, "a_ready");

    /// <summary>
    /// Recovers confirmed A with an already-correct B draft and requires a separate B Ready before any ordinary advancement.
    /// </summary>
    /// <returns>
    /// A task completing after cold replay waits on the issued B, preserves unpublished state and publishes once after its response.
    /// </returns>
    [Fact]
    public Task SpiritualStagedContinuation_ColdAfterACommitBeforeCleanupIssuesB() => RunSpiritualStagedLifecycleAsync(false, "a_commit");

    /// <summary>
    /// Recovers after B request publication returns and its lease closes, preserving completed A and the original saved choice.
    /// </summary>
    /// <returns>
    /// A task completing after a separate B Ready and one accepted turn.
    /// </returns>
    [Fact]
    public Task SpiritualStagedContinuation_ColdAfterBPublicationRetainsA() => RunSpiritualStagedLifecycleAsync(false, "b_publish");

    /// <summary>
    /// Rejects a stale A Ready at the issued B boundary before accepting the current file response.
    /// </summary>
    /// <returns>
    /// A task completing after the stale response leaves private state intact and final publication remains singular.
    /// </returns>
    [Fact]
    public Task SpiritualStagedContinuation_StaleAReadyDoesNotCompleteB() => RunSpiritualStagedLifecycleAsync(false, "none", staleA: true);

    /// <summary>
    /// Preserves a Ready inserted after accepted A cleanup and blocks B publication until the orphan transport is resolved.
    /// </summary>
    /// <returns>
    /// A task completing after the real warm caller rejects without changing private, canonical or orphan Ready bytes.
    /// </returns>
    [Fact]
    public Task SpiritualStagedContinuation_WarmOrphanReadyAfterACommitBlocksB() =>
        RunSpiritualStagedLifecycleAsync(false, "none", orphanAfterA: true);

    /// <summary>
    /// Runs the shared full signed three-exchange fixture through real GM waits and optional durable cold recovery.
    /// </summary>
    /// <param name="worker">
    /// Whether real worker processes author the three proposals, including fresh-engine recovery at the decision cut.
    /// </param>
    /// <param name="cut">
    /// The interruption boundary: none, decision, a_apply, a_ready, a_commit or b_publish.
    /// </param>
    /// <param name="staleA">
    /// Whether file delivery deliberately replays A Ready at B before its valid response; defaults to no replay.
    /// </param>
    /// <param name="orphanAfterA">
    /// Whether to insert one exact A Ready after A cleanup and require blocked B publication; defaults to normal continuation.
    /// </param>
    /// <returns>
    /// A task completing after actual caller publication or the selected orphan rejection preserves its original inputs.
    /// </returns>
    private async Task RunSpiritualStagedLifecycleAsync(bool worker, string cut, bool staleA = false, bool orphanAfterA = false)
    {
        Assert.False(worker && (cut != "decision" || staleA || orphanAfterA));
        Assert.False(orphanAfterA && (cut != "none" || staleA));
        const string repairPath = "game_state/control/validation_repair_request.json";
        const string readyPath = "game_state/control/validation_repair_ready.json";
        var input = new QueuedConsoleInputSource([]);
        var logger = new SpiritualLifecycleTestLogger();
        var shared = new SpiritualStagedObservation();
        ResourceMaterializationTestContext? physical = null;
        Dictionary<string, byte[]?>? interrupted = null;
        byte[]? recoveredCheckpoint = null;
        byte[]? recoveredCommand = null;
        var interruptions = 0;
        var orphanInjections = 0;
        var orphanBPublications = 0;
        var observedBPublications = 0;
        byte[]? observedBRequest = null;
        byte[]? orphanReady = null;
        Dictionary<string, byte[]?>? orphanImages = null;
        long confirmedProgressPublicationTicks = 0;
        async Task InterruptAsync()
        {
            if (Interlocked.CompareExchange(ref interruptions, 1, 0) != 0) return;
            interrupted = await ReadSpiritualStagedPhysicalImagesAsync(physical!);
            throw new OperationCanceledException("Staged continuation interrupted at durable " + cut + ".");
        }
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = async path =>
            {
                if (cut == "decision" && path == SpiritualWoundDecisionPendingState.StatePath &&
                    physical is not null && recoveredCheckpoint is null && interruptions != 0)
                {
                    var recoveredPath = physical.FileSystem.ResolvePath(SpiritualWoundCaptureCheckpointState.StatePath);
                    if (File.Exists(recoveredPath))
                    {
                        var bytes = await File.ReadAllBytesAsync(recoveredPath);
                        var body = ParseDependentSpiritualBytes(bytes)["checkpoint"];
                        if (body?["committedAdvance"]?.GetValue<int>() == 1 && body["pendingSubmission"] is null)
                        {
                            recoveredCheckpoint = bytes;
                            recoveredCommand = await File.ReadAllBytesAsync(physical.FileSystem.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath));
                        }
                    }
                }
                if (cut == "decision" && path == repairPath && physical is not null && interruptions == 0 &&
                    !File.Exists(physical.FileSystem.ResolvePath(repairPath)))
                {
                    var decisionCheckpointPath = physical.FileSystem.ResolvePath(SpiritualWoundCaptureCheckpointState.StatePath);
                    if (File.Exists(decisionCheckpointPath) &&
                        ParseDependentSpiritualBytes(await File.ReadAllBytesAsync(decisionCheckpointPath))
                            ["checkpoint"]?["pendingSubmission"] is JsonObject)
                    {
                        // The initial transport has closed; cancel before dependent A is published.
                        await InterruptAsync();
                    }
                }
                if (cut != "a_commit" || path != readyPath || physical is null || shared.A is null || interruptions != 0) return;
                var checkpointPath = physical.FileSystem.ResolvePath(SpiritualWoundCaptureCheckpointState.StatePath);
                var requestFile = physical.FileSystem.ResolvePath(repairPath);
                var readyFile = physical.FileSystem.ResolvePath(readyPath);
                if (!File.Exists(checkpointPath) || !File.Exists(requestFile) || !File.Exists(readyFile) ||
                    !File.Exists(physical.FileSystem.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath))) return;
                var checkpoint = ParseDependentSpiritualBytes(await File.ReadAllBytesAsync(checkpointPath));
                if (checkpoint["checkpoint"]?["pendingSubmission"]?["dependentDraftProgress"] is not JsonArray { Count: 1 }) return;
                var priorRequest = ParseDependentSpiritualBytes(await File.ReadAllBytesAsync(requestFile));
                var priorReady = ParseDependentSpiritualBytes(await File.ReadAllBytesAsync(readyFile));
                if (priorRequest["spiritualWoundContinuation"]?["continuationId"]?.GetValue<string>() != shared.A.ContinuationId ||
                    priorReady["spiritualWoundContinuation"]?["continuationId"]?.GetValue<string>() != shared.A.ContinuationId) return;
                // The engine reaches exact A Ready deletion only after confirmed progress readback and owner reconstruction.
                await InterruptAsync();
            },
            BeforeCanonicalWriteLockOpenAsync = async () =>
            {
                // Only the engine filesystem has hooks. The responder uses a separate filesystem instance.
                if (cut == "a_apply" && Volatile.Read(ref shared.AppliedAWithoutReady) != 0)
                    await InterruptAsync();
                if (cut == "b_publish" && Volatile.Read(ref observedBPublications) != 0 && physical is not null && interruptions == 0)
                {
                    Assert.NotNull(observedBRequest);
                    Assert.Equal(observedBRequest, await File.ReadAllBytesAsync(physical.FileSystem.ResolvePath(repairPath)));
                    // Publication has returned and its caller lease has closed before this new lock acquisition.
                    await InterruptAsync();
                }
                if (!orphanAfterA || physical is null || shared.A is null || orphanInjections != 0 ||
                    File.Exists(physical.FileSystem.ResolvePath(repairPath)) || File.Exists(physical.FileSystem.ResolvePath(readyPath))) return;
                var checkpointPath = physical.FileSystem.ResolvePath(SpiritualWoundCaptureCheckpointState.StatePath);
                if (!File.Exists(checkpointPath) || !File.Exists(physical.FileSystem.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath))) return;
                var checkpoint = ParseDependentSpiritualBytes(await File.ReadAllBytesAsync(checkpointPath));
                if (checkpoint["checkpoint"]?["pendingSubmission"]?["dependentDraftProgress"] is not JsonArray { Count: 1 }) return;
                if (Interlocked.CompareExchange(ref orphanInjections, 1, 0) != 0) return;
                var originalTurn = ParseDependentSpiritualBytes(await File.ReadAllBytesAsync(physical.FileSystem.ResolvePath("input/turn_request.json")));
                orphanReady = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                {
                    sessionId = originalTurn["sessionId"]!.GetValue<string>(),
                    requestId = originalTurn["requestId"]!.GetValue<string>(),
                    turnNumber = originalTurn["turnNumber"]!.GetValue<int>(),
                    timestamp = DateTime.UtcNow.ToString("O"), status = "success",
                    spiritualWoundContinuation = new
                    {
                        schemaVersion = 1, continuationId = shared.A.ContinuationId, woundDecisions = Array.Empty<JsonElement>()
                    }
                }));
                // This hook precedes lock acquisition. Raw I/O avoids acquiring a nested canonical lease.
                await File.WriteAllBytesAsync(physical.FileSystem.ResolvePath(readyPath), orphanReady);
                orphanImages = await ReadSpiritualStagedPhysicalImagesAsync(physical);
            },
            BeforeCanonicalReadOpenAsync = async path =>
            {
                if (cut != "a_ready" || path != readyPath || physical is null || shared.A is null || interruptions != 0) return;
                var file = physical.FileSystem.ResolvePath(readyPath);
                if (!File.Exists(file)) return;
                var ready = ParseDependentSpiritualBytes(await File.ReadAllBytesAsync(file));
                if (ready["spiritualWoundContinuation"]?["continuationId"]?.GetValue<string>() == shared.A.ContinuationId)
                    await InterruptAsync();
            },
            AfterPhysicalFilePublishedAsync = async path =>
            {
                if (physical is not null && string.Equals(path,
                    physical.FileSystem.ResolvePath(SpiritualWoundCaptureCheckpointState.StatePath), StringComparison.OrdinalIgnoreCase))
                {
                    var checkpoint = ParseDependentSpiritualBytes(await ReadSpiritualStagedPublishedBytesAsync(path));
                    if (checkpoint["checkpoint"]?["pendingSubmission"]?["dependentDraftProgress"] is JsonArray { Count: 1 })
                        Interlocked.CompareExchange(ref confirmedProgressPublicationTicks, DateTime.UtcNow.Ticks, 0);
                }
                if ((cut != "b_publish" && !orphanAfterA) || physical is null || interruptions != 0 ||
                    !string.Equals(path, physical.FileSystem.ResolvePath(repairPath), StringComparison.OrdinalIgnoreCase)) return;
                var publishedBytes = await ReadSpiritualStagedPublishedBytesAsync(path);
                var raw = ParseDependentSpiritualBytes(publishedBytes);
                var fields = raw["spiritualWoundContinuation"]?["dependentDraftFields"] as JsonArray;
                if (fields is not null && fields.Any(row => row?["jsonPointer"]?.GetValue<string>().Contains("/exchangeLog/2/", StringComparison.Ordinal) == true))
                {
                    if (orphanAfterA)
                    {
                        Interlocked.Increment(ref orphanBPublications);
                        input.Enqueue(Key(ConsoleKey.Escape));
                        return;
                    }
                    // This callback precedes publication confirmation; throwing here would roll B back.
                    observedBRequest = publishedBytes;
                    Volatile.Write(ref observedBPublications, 1);
                }
            }
        };
        GameEngine? engine = null;
        var request = new TurnRequest
        {
            SessionId = "session_engine_spiritual", RequestId = "request_engine_spiritual_42", TurnNumber = 42,
            PlayerAction = "Удержать встречное духовное давление.", Timestamp = DateTime.UtcNow.ToString("O"),
            PreGeneratedDices1d20 = [5, 15, 20, 18, 9, 8]
        };
        await using var context = await AfterlifeResourceCutoverTests.CreateSpiritualGameEngineOriginalAsync(async original =>
        {
            await AfterlifeResourceCutoverTests.SeedSpiritualWorseningTierAuthorityAsync(original);
            var chat = Assert.IsType<JsonObject>(await original.ReadJsonAsync("game_state/history/chat_log.json"));
            chat["sessionId"] = request.SessionId;
            await original.WriteExactJsonAsync("game_state/history/chat_log.json", chat.ToJsonString());
            engine = CreateGameEngine(input, configureSettings: settings =>
            {
                if (worker) settings.GmWorkerBridgeProfiles.Add(CreateSpiritualStagedWorkerProfile(original));
            }, fileSystem: original.FileSystem, logger: logger);
            await InvokePrivateTaskAsync(engine, "RefreshRuntimeStateAsync");
            await InvokePrivateTaskAsync(engine, "EnsureClientOwnedSystemFilesHealthyAsync");
            await new StoryService(original.FileSystem, NullLogger<StoryService>.Instance).AppendTurnAsync(
                41, "Chaos Sea", 0, "Предыдущий завершённый ход.", "Душа готовится к духовному обмену.");
            var persistedTurn = Assert.IsType<int>(await InvokePrivateTaskResultAsync(engine, "DetectCurrentSessionTurnNumberAsync"));
            Assert.Equal(41, persistedTurn);
            GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(request.SessionId, persistedTurn);
            request.ProgressionControl = await new ProgressionScheduleService(original.FileSystem,
                NullLogger<ProgressionScheduleService>.Instance).BuildControlForNextTurnAsync("Chaos Sea");
            var backup = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "staged-spiritual-original");
            await original.WriteExactJsonAsync("input/turn_request.json", JsonSerializer.Serialize(request, SnapshotHashJsonOpts));
            await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, backup, "staged-spiritual-original");
        }, hooks: hooks);
        physical = context;
        var originalDraft = await AfterlifeResourceCutoverTests.WriteSpiritualStagedOriginalExchangesAsync(context);
        await WriteSpiritualStagedLifecycleOutputsAsync(context, request);
        var originalImages = await ReadSpiritualStagedPhysicalImagesAsync(context);
        var responderFs = new FileSystemManager(context.RootPath, NullLogger<FileSystemManager>.Instance);
        string DescribeStagedTimeoutBoundary()
        {
            var checkpointFile = context.FileSystem.ResolvePath(SpiritualWoundCaptureCheckpointState.StatePath);
            var checkpoint = ParseDependentSpiritualBytes(File.ReadAllBytes(checkpointFile));
            var rows = checkpoint["checkpoint"]?["pendingSubmission"]?["dependentDraftProgress"] as JsonArray;
            string TransportId(string path)
            {
                var physicalPath = context.FileSystem.ResolvePath(path);
                if (!File.Exists(physicalPath)) return "absent";
                return ParseDependentSpiritualBytes(File.ReadAllBytes(physicalPath))
                    ["spiritualWoundContinuation"]?["continuationId"]?.GetValue<string>() ?? "other";
            }
            return $"interruptions={interruptions}; orphanInjections={orphanInjections}; " +
                $"orphanBPublications={orphanBPublications}; acceptedRows={rows?.Count ?? 0}; " +
                $"observedBPublications={Volatile.Read(ref observedBPublications)}; " +
                $"progressPublishedAt={Interlocked.Read(ref confirmedProgressPublicationTicks)}; " +
                $"request={TransportId(repairPath)}; ready={TransportId(readyPath)}; " +
                $"responses={shared.Phase.FileResponses}";
        }
        using (var stop = new CancellationTokenSource())
        {
            var responder = ObserveSpiritualStagedRequestsAsync(responderFs, input, request, originalDraft,
                shared, worker, orphanAfterA ? "b_publish" : cut, staleA, stop.Token);
            var warm = await AwaitDependentSpiritualPhaseAsync(InvokePrivateTaskResultAsync(engine!, "WaitForGmResponse"),
                responder, stop, input, shared.Phase, logger,
                TimeSpan.FromSeconds(cut is "a_commit" or "b_publish" ? 540 : cut != "none" ? 360 : worker ? 1500 : 1200),
                describeTimeoutBoundary: DescribeStagedTimeoutBoundary);
            if (orphanAfterA)
                Assert.True(warm.Error is null && warm.Result is false, $"{warm.Error}; {logger.Describe()}");
            else if (cut == "none")
                Assert.True(warm.Error is null && warm.Result is true, $"{warm.Error}; {logger.Describe()}");
            else Assert.IsAssignableFrom<OperationCanceledException>(warm.Error);
        }
        if (orphanAfterA)
        {
            Assert.Equal(1, orphanInjections);
            Assert.Equal(0, orphanBPublications);
            Assert.NotNull(orphanImages);
            foreach (var pair in orphanImages!) Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(pair.Key));
            Assert.Equal(orphanReady, await context.FileSystem.ReadFileBytesAsync(readyPath));
            Assert.Equal(41, GetPrivateField<GameLoop>(engine!, "_gameLoop").TurnNumber);
            Assert.Equal(2, shared.Phase.FileResponses);
            Assert.Equal(2, shared.Phase.Requests.Count);
            Assert.Null(shared.B);
            return;
        }
        var acceptedEngine = engine!;
        if (cut != "none")
        {
            Assert.Equal(1, interruptions);
            Assert.NotNull(interrupted);
            foreach (var pair in interrupted!) Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(pair.Key));
            foreach (var pair in originalImages.Where(pair => pair.Key is not AfterlifeSpiritualConflictState.StatePath &&
                !pair.Key.Contains("validation_repair", StringComparison.Ordinal) && pair.Key != SpiritualWoundCaptureCheckpointState.StatePath &&
                pair.Key != SpiritualWoundDecisionPendingState.StatePath && pair.Key != AcceptedMechanicsPlan.WoundCommandPath))
                Assert.Equal(pair.Value, interrupted[pair.Key]);
            Assert.NotNull(interrupted[SpiritualWoundCaptureCheckpointState.StatePath]);
            Assert.NotNull(interrupted[SpiritualWoundDecisionPendingState.StatePath]);
            Assert.NotNull(interrupted[AcceptedMechanicsPlan.WoundCommandPath]);
            Assert.Equal(41, GetPrivateField<GameLoop>(engine!, "_gameLoop").TurnNumber);
            if (cut == "decision")
            {
                Assert.Single(shared.Phase.Requests);
                Assert.Equal(0, shared.Phase.FileResponses);
                Assert.Null(shared.A);
                Assert.Null(interrupted[repairPath]);
                Assert.Null(interrupted[readyPath]);
            }
            if (cut == "a_commit")
            {
                var alreadyCorrected = AfterlifeResourceCutoverTests.CreateSpiritualStagedCorrectionB(
                    AfterlifeResourceCutoverTests.CreateSpiritualStagedCorrectionA(originalDraft));
                await responderFs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, alreadyCorrected.ToJsonString());
                Assert.Equal(shared.A!.ContinuationId, ParseDependentSpiritualBytes(
                    (await responderFs.ReadFileBytesAsync(readyPath))!)["spiritualWoundContinuation"]!["continuationId"]!.GetValue<string>());
            }
            var beforeBReady = cut == "a_commit" ? await ReadSpiritualStagedPhysicalImagesAsync(context) : null;
            var unansweredB = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var unansweredBLocks = 0;
            var coldHooks = cut != "a_commit" ? null : new FileSystemManagerHooks
            {
                BeforeCanonicalWriteLockOpenAsync = async () =>
                {
                    var publishedRequest = context.FileSystem.ResolvePath(repairPath);
                    if (!File.Exists(publishedRequest) || File.Exists(context.FileSystem.ResolvePath(readyPath))) return;
                    var report = ParseDependentSpiritualBytes(await File.ReadAllBytesAsync(publishedRequest));
                    var fields = report["spiritualWoundContinuation"]?["dependentDraftFields"] as JsonArray;
                    if (fields is null || !fields.Any(field => field?["jsonPointer"]?.GetValue<string>()
                            .StartsWith("/activeConflict/exchangeLog/2/", StringComparison.Ordinal) == true)) return;
                    // Two engine entries with B outstanding prove that the responder did not answer immediately at publication.
                    if (Interlocked.Increment(ref unansweredBLocks) == 2) unansweredB.TrySetResult(true);
                }
            };
            if (cut == "decision") coldHooks = hooks;
            var coldFs = new FileSystemManager(context.RootPath, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, hooks: coldHooks);
            var coldInput = new QueuedConsoleInputSource([]);
            var coldEngine = CreateGameEngine(coldInput, configureSettings: settings =>
            {
                if (worker) settings.GmWorkerBridgeProfiles.Add(CreateSpiritualStagedWorkerProfile(context));
            }, fileSystem: coldFs, logger: logger);
            await InvokePrivateTaskAsync(coldEngine, "RefreshRuntimeStateAsync");
            var persistedTurn = Assert.IsType<int>(await InvokePrivateTaskResultAsync(coldEngine, "DetectCurrentSessionTurnNumberAsync"));
            Assert.Equal(41, persistedTurn);
            GetPrivateField<GameLoop>(coldEngine, "_gameLoop").SetSession(request.SessionId, persistedTurn);
            using var stop = new CancellationTokenSource();
            var coldOperation = RunSpiritualInterruptionColdStartupAsync(coldEngine);
            var responder = Task.Run(async () =>
            {
                try
                {
                    if (cut == "a_commit")
                    {
                        await Task.WhenAny(coldOperation, unansweredB.Task).WaitAsync(stop.Token);
                        Assert.True(unansweredB.Task.IsCompletedSuccessfully,
                            "Cold engine completed without issuing and waiting for separate B Ready despite its fully corrected draft.");
                        Assert.False(coldOperation.IsCompleted, "Cold engine advanced before the withheld B response.");
                        Assert.False(File.Exists(context.FileSystem.ResolvePath(readyPath)));
                        Assert.Equal(41, GetPrivateField<GameLoop>(coldEngine, "_gameLoop").TurnNumber);
                        var waiting = await ReadSpiritualStagedPhysicalImagesAsync(context);
                        foreach (var pair in beforeBReady!.Where(pair => !pair.Key.Contains("validation_repair", StringComparison.Ordinal)))
                            Assert.Equal(pair.Value, waiting[pair.Key]);
                        Assert.Equal(2, shared.Phase.FileResponses);
                    }
                    await ObserveSpiritualStagedRequestsAsync(responderFs, coldInput, request, originalDraft,
                        shared, worker, "none", false, stop.Token);
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
                catch (Exception error)
                {
                    shared.Phase.Failure = error;
                    coldInput.Enqueue(Key(ConsoleKey.Escape));
                }
            });
            var cold = await AwaitDependentSpiritualPhaseAsync(coldOperation,
                responder, stop, coldInput, shared.Phase, logger,
                TimeSpan.FromSeconds(worker ? 1200 : cut is "a_apply" or "a_ready" ? 780 : 600),
                describeTimeoutBoundary: DescribeStagedTimeoutBoundary);
            Assert.True(cold.Error is null && cold.Result is false, $"{cold.Error}; {logger.Describe()}");
            if (cut == "decision")
            {
                Assert.Equal(interrupted[AcceptedMechanicsPlan.WoundCommandPath], shared.Command);
                Assert.Equal(interrupted[AcceptedMechanicsPlan.WoundCommandPath], recoveredCommand);
                Assert.NotNull(recoveredCheckpoint);
                var saved = ParseDependentSpiritualBytes(interrupted[SpiritualWoundCaptureCheckpointState.StatePath]!)["checkpoint"]!;
                var submission = saved["pendingSubmission"]!;
                var recovered = ParseDependentSpiritualBytes(recoveredCheckpoint!)["checkpoint"]!;
                Assert.Equal(0, saved["committedAdvance"]!.GetValue<int>());
                Assert.Equal(0, submission["priorCommittedAdvance"]!.GetValue<int>());
                Assert.Equal("materialize", submission["stagedDecision"]!["decision"]!.GetValue<string>());
                Assert.Equal(shared.DecisionFingerprint, submission["stagedDecision"]!["decisionFingerprint"]!.GetValue<string>());
                Assert.Equal(interrupted[AcceptedMechanicsPlan.WoundCommandPath],
                    Convert.FromBase64String(submission["command"]!["contentBase64"]!.GetValue<string>()));
                Assert.NotEmpty(submission["allocations"]!.AsArray());
                var allocations = new JsonArray(saved["allocations"]!.AsArray()
                    .Concat(submission["allocations"]!.AsArray()).Select(row => row!.DeepClone()).ToArray());
                var recoveredAllocations = recovered["allocations"]!.AsArray();
                Assert.True(recoveredAllocations.Count >= allocations.Count,
                    $"Recovered allocation count {recoveredAllocations.Count} is below saved count {allocations.Count}.");
                for (var index = 0; index < allocations.Count; index++)
                    Assert.True(JsonNode.DeepEquals(allocations[index], recoveredAllocations[index]),
                        $"Retained allocation {index} changed during cold recovery.");
                Assert.Equal(1, recovered["committedAdvance"]!.GetValue<int>());
                Assert.Null(recovered["pendingSubmission"]);
                Assert.Equal(shared.DecisionFingerprint,
                    recovered["advances"]!.AsArray().Last()!["newDecisionFingerprints"]![0]!.GetValue<string>());
                foreach (var field in new[] { "sessionId", "requestId", "turn", "snapshotToken" })
                    Assert.True(JsonNode.DeepEquals(saved[field], recovered[field]), field);
                Assert.Equal(originalImages[AfterlifeSpiritualConflictState.StatePath], interrupted[AfterlifeSpiritualConflictState.StatePath]);
            }
            acceptedEngine = coldEngine;
        }
        Assert.Equal(3, shared.Phase.Requests.Count);
        Assert.NotNull(shared.A);
        Assert.NotNull(shared.B);
        Assert.NotEqual(shared.A!.ContinuationId, shared.B!.ContinuationId);
        Assert.Equal(worker ? 0 : 3, shared.Phase.FileResponses);
        Assert.Equal(staleA ? 1 : 0, shared.StaleReadyRejections);
        await AssertSpiritualStagedPublicationAsync(context, acceptedEngine, originalDraft, shared);
        if (worker) await AssertSpiritualStagedWorkersAsync(context, shared);
    }

    /// <summary>
    /// Reads a publication callback's destination while the publisher retains its source handle through confirmation.
    /// </summary>
    /// <param name="absolutePath">
    /// The normalized physical destination supplied by the publication callback.
    /// </param>
    /// <returns>
    /// Exact observed bytes read with cooperative sharing, without retries or canonical lease acquisition.
    /// </returns>
    private static async Task<byte[]> ReadSpiritualStagedPublishedBytesAsync(string absolutePath)
    {
        await using var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        return memory.ToArray();
    }
}
