using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Recovers a file-selected wound after deterministic interruption at its durable dependent boundary.
    /// </summary>
    /// <returns>
    /// A task completing after fresh-engine correction preserves the saved choice and publishes its effects once.
    /// </returns>
    [Fact]
    public async Task AcceptedTurnSpiritualContinuation_FileDependentDraftResumesSavedChoiceAfterInterruption()
    {
        await RunDependentSpiritualColdContinuationAsync(worker: false);
    }

    /// <summary>
    /// Interrupts the real caller after durable wound selection, then resumes only the allowed arithmetic correction in a fresh engine.
    /// </summary>
    /// <param name="worker">
    /// Whether actual worker processes author both responses; otherwise the file callback writes each response.
    /// </param>
    /// <param name="position">
    /// Whether the file responder corrects the saved guardian position burden; defaults to the existing guard-cost scenario.
    /// </param>
    /// <returns>
    /// A task completing after saved command and allocation replay, canonical publication, notifications and terminal cleanup agree.
    /// </returns>
    private async Task RunDependentSpiritualColdContinuationAsync(bool worker, bool position = false)
    {
        Assert.False(worker && position, "Position coverage uses the file responder; worker defaults remain unchanged.");
        const string repairPath = "game_state/control/validation_repair_request.json";
        var input = new QueuedConsoleInputSource([]);
        var logger = new SpiritualLifecycleTestLogger();
        ResourceMaterializationTestContext? physicalContext = null;
        byte[]? interruptedCheckpoint = null;
        byte[]? interruptedCommand = null;
        byte[]? interruptedPending = null;
        var interruptions = 0;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = async path =>
            {
                if (path != repairPath || physicalContext is null || interruptions != 0)
                    return;
                // The same hook covers deletion: let the old decision transport finish cleanup first.
                if (File.Exists(physicalContext.FileSystem.ResolvePath(repairPath)))
                    return;
                var physical = physicalContext.FileSystem.ResolvePath(SpiritualWoundCaptureCheckpointState.StatePath);
                if (!File.Exists(physical))
                    return;
                var checkpoint = await File.ReadAllBytesAsync(physical);
                if (ParseDependentSpiritualBytes(checkpoint)["checkpoint"]?["pendingSubmission"] is not JsonObject)
                    return;
                interruptedCheckpoint = checkpoint;
                interruptedCommand = await File.ReadAllBytesAsync(physicalContext.FileSystem.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath));
                interruptedPending = await File.ReadAllBytesAsync(physicalContext.FileSystem.ResolvePath(SpiritualWoundDecisionPendingState.StatePath));
                Interlocked.Increment(ref interruptions);
                // Deterministic process-interruption seam: cancellation propagates before caller rollback.
                throw new OperationCanceledException("Interrupted after durable selection before dependent request publication.");
            }
        };
        GameEngine? engine = null;
        var request = new TurnRequest
        {
            SessionId = "session_engine_spiritual", RequestId = "request_engine_spiritual_42", TurnNumber = 42,
            PlayerAction = "Удержать встречное духовное давление.", Timestamp = DateTime.UtcNow.ToString("O"),
            PreGeneratedDices1d20 = position ? [15, 5, 8, 9] : [15, 5, 12, 8]
        };
        await using var context = await AfterlifeResourceCutoverTests.CreateSpiritualGameEngineOriginalAsync(async original =>
        {
            var chat = Assert.IsType<JsonObject>(await original.ReadJsonAsync("game_state/history/chat_log.json"));
            chat["sessionId"] = request.SessionId;
            await original.WriteExactJsonAsync("game_state/history/chat_log.json", chat.ToJsonString());
            engine = CreateGameEngine(input, configureSettings: settings =>
            {
                if (worker)
                    settings.GmWorkerBridgeProfiles.Add(CreateDependentSpiritualWorkerProfile(original));
            }, fileSystem: original.FileSystem, logger: logger);
            await InvokePrivateTaskAsync(engine, "RefreshRuntimeStateAsync");
            GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(request.SessionId, 41);
            await InvokePrivateTaskAsync(engine, "EnsureClientOwnedSystemFilesHealthyAsync");
            request.ProgressionControl = await new ProgressionScheduleService(original.FileSystem,
                NullLogger<ProgressionScheduleService>.Instance).BuildControlForNextTurnAsync("Chaos Sea");
            var rollback = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "dependent-spiritual-original");
            await original.WriteExactJsonAsync("input/turn_request.json", JsonSerializer.Serialize(request, SnapshotHashJsonOpts));
            await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync",
                request, rollback, "dependent-spiritual-original");
        }, hooks: hooks);
        physicalContext = context;
        if (position)
        {
            await AfterlifeResourceCutoverTests.WriteSpiritualGameEnginePositionDependentExchangeAsync(context);
            await WritePositionDependentSpiritualLifecycleOutputsAsync(context, request);
        }
        else
        {
            await AfterlifeResourceCutoverTests.WriteSpiritualGameEngineDependentExchangeAsync(context);
            await WriteDependentSpiritualLifecycleOutputsAsync(context, request);
        }
        var originals = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in new[] { "input/turn_request.json", "ready/turn_complete.json",
                     "game_state/control/pending_turn_snapshot.json", "game_state/control/pending_turn_snapshot.authority.json",
                     ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
                     WoundIdentityState.StatePath, WoundHistoryState.HistoryPath, EffectIdentityState.StatePath,
                     AfterlifeEntityProfileState.StatePath })
            originals.Add(path, await context.FileSystem.ReadFileBytesAsync(path));
        var originalDraft = Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync(AfterlifeSpiritualConflictState.StatePath));
        var warmObservation = new DependentSpiritualGmObservation();
        using (var stop = new CancellationTokenSource())
        {
            var callback = ObserveDependentSpiritualGmAsync(context.FileSystem, input, request, worker,
                "decision", originals, savedCommand: null, warmObservation, stop.Token, position);
            var warm = await AwaitDependentSpiritualPhaseAsync(
                InvokePrivateTaskResultAsync(engine!, "WaitForGmResponse"), callback, stop, input,
                warmObservation, logger, TimeSpan.FromSeconds(360));
            Assert.True(warm.Error is OperationCanceledException,
                $"Expected deterministic durable-boundary cancellation; result={warm.Result}; error={warm.Error}. {logger.Describe()}");
        }
        Assert.Equal(1, interruptions);
        Assert.False(context.FileSystem.FileExists(repairPath));
        Assert.False(context.FileSystem.FileExists("game_state/control/validation_repair_ready.json"));
        Assert.Single(warmObservation.Requests);
        Assert.Equal(worker ? 0 : 1, warmObservation.FileResponses);
        Assert.Equal(41, GetPrivateField<GameLoop>(engine!, "_gameLoop").TurnNumber);
        foreach (var pair in originals)
            Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(pair.Key));
        Assert.Equal(originalDraft, await context.FileSystem.ReadFileBytesAsync(AfterlifeSpiritualConflictState.StatePath));
        Assert.False(context.FileSystem.FileExists(SpiritualWoundOpportunityReceiptState.StatePath));
        Assert.Equal(interruptedCheckpoint, await context.FileSystem.ReadFileBytesAsync(SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.Equal(interruptedPending, await context.FileSystem.ReadFileBytesAsync(SpiritualWoundDecisionPendingState.StatePath));
        Assert.Equal(interruptedCommand, await context.FileSystem.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
        var checkpointBefore = ParseDependentSpiritualBytes(Assert.IsType<byte[]>(interruptedCheckpoint))["checkpoint"]!;
        var submission = checkpointBefore["pendingSubmission"]!.AsObject();
        Assert.Equal(0, checkpointBefore["committedAdvance"]!.GetValue<int>());
        Assert.Equal(0, submission["priorCommittedAdvance"]!.GetValue<int>());
        Assert.Equal("materialize", submission["stagedDecision"]!["decision"]!.GetValue<string>());
        Assert.Equal(interruptedCommand, Convert.FromBase64String(submission["command"]!["contentBase64"]!.GetValue<string>()));
        var decisionFingerprint = submission["stagedDecision"]!["decisionFingerprint"]!.GetValue<string>();
        var retainedAllocations = checkpointBefore["allocations"]!.AsArray().Concat(submission["allocations"]!.AsArray())
            .Select(row => row!.DeepClone()).ToArray();
        Assert.NotEmpty(submission["allocations"]!.AsArray());
        Assert.Equal(request.SessionId, checkpointBefore["sessionId"]!.GetValue<string>());
        Assert.Equal(request.RequestId, checkpointBefore["requestId"]!.GetValue<string>());
        Assert.Equal(42, checkpointBefore["turn"]!.GetValue<int>());
        var manifest = ParseDependentSpiritualBytes(Assert.IsType<byte[]>(originals["game_state/control/pending_turn_snapshot.json"]));
        Assert.Equal(manifest["manifestPayloadHash"]!.GetValue<string>(), checkpointBefore["snapshotToken"]!.GetValue<string>());

        byte[]? completedCheckpoint = null;
        byte[]? completedCommand = null;
        var coldPendingBoundaries = new List<string>();
        var coldHooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = async path =>
            {
                if (path != SpiritualWoundDecisionPendingState.StatePath || completedCheckpoint is not null)
                    return;
                var checkpoint = await File.ReadAllBytesAsync(context.FileSystem.ResolvePath(SpiritualWoundCaptureCheckpointState.StatePath));
                var body = ParseDependentSpiritualBytes(checkpoint)["checkpoint"]!;
                if (coldPendingBoundaries.Count < 8)
                    coldPendingBoundaries.Add($"committedAdvance={body["committedAdvance"]}; pendingSubmission={body["pendingSubmission"] is not null}");
                if (body["committedAdvance"]!.GetValue<int>() == 1 && body["pendingSubmission"] is null)
                {
                    completedCheckpoint = checkpoint;
                    completedCommand = await File.ReadAllBytesAsync(context.FileSystem.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath));
                }
            }
        };
        var coldFs = new FileSystemManager(context.RootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, coldHooks);
        var coldInput = new QueuedConsoleInputSource([]);
        var coldEngine = CreateGameEngine(coldInput, configureSettings: settings =>
        {
            if (worker)
                settings.GmWorkerBridgeProfiles.Add(CreateDependentSpiritualWorkerProfile(context));
        }, fileSystem: coldFs, logger: logger);
        var coldLoop = GetPrivateField<GameLoop>(coldEngine, "_gameLoop");
        coldLoop.SetSession(request.SessionId, 41);
        await InvokePrivateTaskAsync(coldEngine, "RefreshRuntimeStateAsync");
        await InvokePrivateTaskAsync(coldEngine, "NormalizePendingRepairArtifactsAsync");
        foreach (var pair in originals)
            Assert.Equal(pair.Value, await coldFs.ReadFileBytesAsync(pair.Key));
        Assert.Equal(interruptedCheckpoint, await coldFs.ReadFileBytesAsync(SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.Equal(interruptedCommand, await coldFs.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
        var coldObservation = new DependentSpiritualGmObservation();
        using (var stop = new CancellationTokenSource())
        {
            var callback = ObserveDependentSpiritualGmAsync(coldFs, coldInput, request, worker,
                "dependent_draft", originals, interruptedCommand, coldObservation, stop.Token, position);
            var cold = await AwaitDependentSpiritualPhaseAsync(
                InvokePrivateTaskResultAsync(coldEngine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync"),
                callback, stop, coldInput, coldObservation, logger, TimeSpan.FromSeconds(420));
            Assert.True(cold.Error is null, $"Cold recovery failed: {cold.Error}. {logger.Describe()}");
            Assert.False(Assert.IsType<bool>(cold.Result), logger.Describe());
        }
        var coldRequest = Assert.Single(coldObservation.Requests).Value;
        var warmRequest = Assert.Single(warmObservation.Requests).Value;
        Assert.NotEqual(warmRequest.ContinuationId, coldRequest.ContinuationId);
        Assert.Equal(worker ? 0 : 1, coldObservation.FileResponses);
        Assert.True(completedCheckpoint is not null && completedCommand is not null,
            $"Completed C2 boundary was not observed; turn={coldLoop.TurnNumber}; " +
            $"pending boundaries=[{string.Join("; ", coldPendingBoundaries)}]; " +
            $"requests={coldObservation.Requests.Count}; file responses={coldObservation.FileResponses}; " +
            $"last repair={coldObservation.LastRepair}. {logger.Describe()}");
        Assert.Equal(interruptedCommand, completedCommand);
        var finalCheckpoint = ParseDependentSpiritualBytes(Assert.IsType<byte[]>(completedCheckpoint))["checkpoint"]!;
        Assert.Null(finalCheckpoint["pendingSubmission"]);
        Assert.Equal(1, finalCheckpoint["committedAdvance"]!.GetValue<int>());
        Assert.Equal(decisionFingerprint, finalCheckpoint["advances"]!.AsArray().Last()!["newDecisionFingerprints"]![0]!.GetValue<string>());
        var finalAllocations = finalCheckpoint["allocations"]!.AsArray();
        Assert.True(finalAllocations.Count >= retainedAllocations.Length);
        for (var index = 0; index < retainedAllocations.Length; index++)
            Assert.True(JsonNode.DeepEquals(retainedAllocations[index], finalAllocations[index]), $"Retained allocation {index} changed.");
        foreach (var field in new[] { "sessionId", "requestId", "turn", "snapshotToken" })
            Assert.True(JsonNode.DeepEquals(checkpointBefore[field], finalCheckpoint[field]), field);
        if (worker)
            await AssertDependentSpiritualWorkersAsync(context, warmRequest, coldRequest);
        await AssertDependentSpiritualPublicationAsync(context, coldEngine, originalDraft, warmRequest, decisionFingerprint, position);
    }

    /// <summary>
    /// Retains bounded callback outcomes and actual observed requests for one engine phase.
    /// </summary>
    private sealed class DependentSpiritualGmObservation
    {
        /// <summary>
        /// Gets each observed request keyed by its actual continuation identity.
        /// </summary>
        internal ConcurrentDictionary<string, SpiritualWoundContinuationRequest> Requests { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets or sets the number of explicit file responses written; workers never increment this count.
        /// </summary>
        internal int FileResponses { get; set; }

        /// <summary>
        /// Gets or sets the callback failure, preserving its diagnostic before engine cleanup.
        /// </summary>
        internal Exception? Failure { get; set; }

        /// <summary>
        /// Gets or sets the last public request for bounded failure diagnostics.
        /// </summary>
        internal string? LastRepair { get; set; }
    }

    /// <summary>
    /// Observes actual requests and supplies only file-mode decisions or allowlisted corrections without acquiring private authority.
    /// </summary>
    /// <param name="fs">
    /// The phase's real filesystem, fresh for cold recovery.
    /// </param>
    /// <param name="input">
    /// Console input used only to release a failed callback's engine wait.
    /// </param>
    /// <param name="original">
    /// Immutable original turn identity.
    /// </param>
    /// <param name="worker">
    /// Whether the callback observes only and leaves all proposal and Ready writes to the actual worker bridge.
    /// </param>
    /// <param name="phase">
    /// The only allowed request phase for this run: initial decision or cold dependent draft.
    /// </param>
    /// <param name="originals">
    /// Exact signed inputs and unpublished canonical images that must remain unchanged while awaiting the GM;
    /// a <see langword="null"/> image requires the corresponding file to remain absent.
    /// </param>
    /// <param name="savedCommand">
    /// Exact durable selected command for cold recovery, or <see langword="null"/> before initial selection.
    /// </param>
    /// <param name="observation">
    /// Shared result holder read after the callback has completed.
    /// </param>
    /// <param name="cancellationToken">
    /// Stops polling when the bounded engine phase ends.
    /// </param>
    /// <param name="position">
    /// Whether to author the exact position correction; defaults to the existing guard-cost response.
    /// </param>
    /// <returns>
    /// A task completing when polling stops; callback failures are retained and release the engine wait.
    /// </returns>
    private static Task ObserveDependentSpiritualGmAsync(FileSystemManager fs, QueuedConsoleInputSource input,
        TurnRequest original, bool worker, string phase, IReadOnlyDictionary<string, byte[]?> originals,
        byte[]? savedCommand, DependentSpiritualGmObservation observation, CancellationToken cancellationToken, bool position = false) => Task.Run(async () =>
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var path = fs.ResolvePath("game_state/control/validation_repair_request.json");
                string? raw = null;
                try
                {
                    if (File.Exists(path))
                    {
                        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                        using var reader = new StreamReader(stream);
                        raw = await reader.ReadToEndAsync(cancellationToken);
                    }
                }
                catch (FileNotFoundException) { }
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    observation.LastRepair = raw;
                    using var document = JsonDocument.Parse(raw);
                    var repair = document.RootElement;
                    Assert.True(repair.TryGetProperty("spiritualWoundContinuation", out var envelope), raw);
                    var continuation = SpiritualWoundContinuationProtocol.ReadRequest(envelope);
                    if (observation.Requests.TryAdd(continuation.ContinuationId, continuation))
                    {
                        Assert.Single(observation.Requests);
                        Assert.Equal(phase, continuation.Phase);
                        Assert.Equal(original.SessionId, repair.GetProperty("sessionId").GetString());
                        Assert.Equal(original.RequestId, repair.GetProperty("requestId").GetString());
                        Assert.Equal(42, repair.GetProperty("turnNumber").GetInt32());
                        Assert.False(repair.GetProperty("fullTurnResubmissionRequired").GetBoolean());
                        Assert.Empty(SpiritualWoundContinuationProtocol.ValidateRequest(continuation));
                        if (phase == "dependent_draft")
                        {
                            Assert.Null(continuation.Offer);
                            Assert.True(repair.GetProperty("errors").GetArrayLength() > 0, raw);
                            Assert.Equal(position
                                ? new[] { "/activeConflict/exchangeLog/1/diceAudit/margin",
                                    "/activeConflict/exchangeLog/1/diceAudit/modifierBreakdown",
                                    "/activeConflict/exchangeLog/1/diceAudit/playerTotal" }
                                : new[] { "/activeConflict/exchangeLog/1/actionCostAudit/opposition/after",
                                    "/activeConflict/exchangeLog/1/actionCostAudit/opposition/effectiveCost" },
                                continuation.DependentDraftFields.Select(field => field.JsonPointer));
                            Assert.All(continuation.DependentDraftFields, field => Assert.Equal(AfterlifeSpiritualConflictState.StatePath, field.Path));
                        }
                        else
                        {
                            Assert.NotNull(continuation.Offer);
                            Assert.Empty(continuation.DependentDraftFields);
                            Assert.Equal(0, repair.GetProperty("errors").GetArrayLength());
                        }
                        if (!worker)
                        {
                            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                            deadline.CancelAfter(TimeSpan.FromSeconds(10));
                            await using (var lease = await fs.AcquireCanonicalWriteLeaseAsync(cancellationToken: deadline.Token))
                            {
                                foreach (var pair in originals)
                                    Assert.Equal(pair.Value, await fs.ReadFileBytesAsync(lease, pair.Key));
                                Assert.False(fs.FileExists(lease, SpiritualWoundOpportunityReceiptState.StatePath));
                                if (savedCommand is not null)
                                    Assert.Equal(savedCommand, await fs.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
                            }
                            JsonElement[] decisions;
                            if (phase == "decision")
                            {
                                var narrative = ParseDependentSpiritualBytes(Assert.IsType<byte[]>(await fs.ReadFileBytesAsync("output/narrative_response.json")));
                                narrative["response"] = position
                                    ? "Душа удерживает встречное давление. Чужое давление надломило волю хранителя. Во втором обмене оба сохраняют pressure; новое напряжение не возникает."
                                    : "Душа удерживает встречное давление. Чужое давление надломило волю хранителя. Хранитель сохраняет исходное guard во втором обмене.";
                                narrative["timestamp"] = DateTime.UtcNow.ToString("O");
                                await fs.WriteFileAtomicAsync("output/narrative_response.json", narrative.ToJsonString());
                                decisions = [position
                                    ? AfterlifeResourceCutoverTests.CreateSpiritualDependentPositionDecision(continuation.Offer!.OpportunityRef)
                                    : AfterlifeResourceCutoverTests.CreateSpiritualDependentGuardDecision(continuation.Offer!.OpportunityRef)];
                            }
                            else
                            {
                                var conflict = ParseDependentSpiritualBytes(Assert.IsType<byte[]>(await fs.ReadFileBytesAsync(AfterlifeSpiritualConflictState.StatePath)));
                                if (position)
                                    ApplySpiritualPositionDependentCorrection(conflict);
                                else
                                {
                                    conflict["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["opposition"]!["effectiveCost"] = 3;
                                    conflict["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["opposition"]!["after"] = 0;
                                }
                                await fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, conflict.ToJsonString());
                                decisions = [];
                            }
                            await fs.WriteFileAtomicAsync("game_state/control/validation_repair_ready.json", JsonSerializer.Serialize(new
                            {
                                sessionId = original.SessionId, requestId = original.RequestId, turnNumber = 42,
                                timestamp = DateTime.UtcNow.ToString("O"), status = "success",
                                spiritualWoundContinuation = new
                                {
                                    schemaVersion = 1, continuationId = continuation.ContinuationId, woundDecisions = decisions
                                }
                            }));
                            observation.FileResponses++;
                        }
                    }
                }
                await Task.Delay(25, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception error)
        {
            observation.Failure = error;
            input.Enqueue(Key(ConsoleKey.Escape));
        }
    });

    /// <summary>
    /// Bounds one complete engine phase and callback while preserving its primary failure and all cleanup diagnostics.
    /// </summary>
    /// <param name="operation">
    /// Actual warm or cold lifecycle operation to await.
    /// </param>
    /// <param name="callback">
    /// Public-request observer and optional file responder for the phase.
    /// </param>
    /// <param name="stop">
    /// Cancellation source owned by the caller and used to end callback polling.
    /// </param>
    /// <param name="input">
    /// Console input used to release an incomplete lifecycle after its deadline.
    /// </param>
    /// <param name="observation">
    /// Callback evidence included in failure diagnostics.
    /// </param>
    /// <param name="logger">
    /// Bounded actual engine diagnostics.
    /// </param>
    /// <param name="timeout">
    /// Maximum duration of the complete lifecycle phase before bounded cleanup.
    /// </param>
    /// <param name="describeTimeoutBoundary">
    /// Optional read-only physical boundary description captured before timeout cleanup.
    /// </param>
    /// <returns>
    /// Actual lifecycle result or primary exception after successful callback and cleanup checks.
    /// </returns>
    private static async Task<(object? Result, Exception? Error)> AwaitDependentSpiritualPhaseAsync(Task<object> operation,
        Task callback, CancellationTokenSource stop, QueuedConsoleInputSource input,
        DependentSpiritualGmObservation observation, SpiritualLifecycleTestLogger logger, TimeSpan timeout,
        Func<string>? describeTimeoutBoundary = null)
    {
        object? result = null;
        Exception? failure = null;
        string boundary = "";
        var cleanup = new List<Exception>();
        try { result = await operation.WaitAsync(timeout); }
        catch (Exception error)
        {
            failure = error;
            if (error is TimeoutException && describeTimeoutBoundary is not null)
            {
                try { boundary = describeTimeoutBoundary(); }
                catch (Exception diagnosticError) { boundary = "snapshot failed: " + diagnosticError.Message; }
                failure = new TimeoutException($"{error.Message} Boundary: {boundary}", error);
            }
        }
        finally
        {
            try { stop.Cancel(); }
            catch (Exception error) { cleanup.Add(error); }
            if (!operation.IsCompleted) input.Enqueue(Key(ConsoleKey.Escape));
            // Drain both originals independently: a timed-out proxy cannot authorize root disposal.
            try { await callback; }
            catch (Exception error) { cleanup.Add(error); }
            try { await operation; }
            catch (Exception error)
            {
                if (!ReferenceEquals(error, failure)) cleanup.Add(error);
            }
            Assert.True(operation.IsCompleted && callback.IsCompleted);
        }
        Assert.True(observation.Failure is null && cleanup.Count == 0,
            $"Primary: {failure}; callback: {observation.Failure}; cleanup: {string.Join("\n", cleanup)}; " +
            $"result: {result}; requests: {observation.Requests.Count}; file responses: {observation.FileResponses}; " +
            $"last repair: {observation.LastRepair}; boundary: {boundary}. {logger.Describe()}");
        return (result, failure);
    }

    /// <summary>
    /// Parses physical JSON bytes while accepting the filesystem writer's optional UTF-8 preamble.
    /// </summary>
    /// <param name="bytes">
    /// Exact physical image retained for separate byte-preservation assertions.
    /// </param>
    /// <returns>
    /// The parsed object used only for test diagnostics and structural assertions.
    /// </returns>
    private static JsonObject ParseDependentSpiritualBytes(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = new StreamReader(stream);
        return Assert.IsType<JsonObject>(JsonNode.Parse(reader.ReadToEnd()));
    }
}
