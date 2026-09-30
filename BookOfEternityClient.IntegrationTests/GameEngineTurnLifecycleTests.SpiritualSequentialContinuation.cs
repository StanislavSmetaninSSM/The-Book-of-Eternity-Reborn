using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Authors the existing two-source C2 recipe in a full signed GameEngine original without creating private authority.
    /// </summary>
    /// <param name="context">
    /// Signed original whose single pressure exchange harms both sides using original dice coordinates two and three.
    /// </param>
    /// <returns>
    /// A task completing after the raw draft contains both harmed sides and unchanged per-side action costs.
    /// </returns>
    internal static async Task WriteSpiritualGameEngineTwoSourceExchangeAsync(ResourceMaterializationTestContext context)
    {
        await WriteCompleteConflictFrameExchangeAsync(context);
        var conflict = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = conflict["activeConflict"]!.AsObject();
        var exchange = active["exchangeLog"]![0]!.AsObject();
        exchange["after"]!["playerSideStrain"] = "broken";
        exchange["after"]!["oppositionSideStrain"] = "broken";
        active["playerSideStrain"] = "broken";
        active["oppositionSideStrain"] = "broken";
        var dice = exchange["diceAudit"]!;
        dice["diceUsed"]![0]!["sourceIndex"] = 2;
        dice["diceUsed"]![0]!["value"] = 12;
        dice["diceUsed"]![1]!["sourceIndex"] = 3;
        dice["diceUsed"]![1]!["value"] = 8;
        dice["playerTotal"] = 12;
        dice["oppositionTotal"] = 8;
        dice["margin"] = 4;
        dice["outcomeBand"] = "player_success";
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, conflict.ToJsonString());
    }
}

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Consumes two successive file responses through the real engine and preserves the completed turn after cold startup.
    /// </summary>
    /// <returns>
    /// A task completing after one publication, ordered receipts and cold no-op behavior are verified.
    /// </returns>
    [Fact]
    public async Task AcceptedTurnSpiritualContinuation_TwoFileOffersPublishOnceAndColdStartupDoesNotReplay()
    {
        await RunSequentialSpiritualContinuationAsync(worker: false);
    }

    /// <summary>
    /// Consumes two successive actual worker responses and preserves the completed turn after cold startup.
    /// </summary>
    /// <returns>
    /// A task completing after separate worker dispatches publish one turn and cold startup preserves every file.
    /// </returns>
    [Fact]
    [Trait("Category", "ProcessIntegration")]
    public async Task AcceptedTurnSpiritualContinuation_TwoWorkerOffersPublishOnceAndColdStartupDoesNotReplay()
    {
        await RunSequentialSpiritualContinuationAsync(worker: true);
    }

    /// <summary>
    /// Rejects a replayed first response at the second real offer before accepting the current response once.
    /// </summary>
    /// <returns>
    /// A task completing after rejected replay preserves the private frontier and final publication remains singular.
    /// </returns>
    [Fact]
    public async Task AcceptedTurnSpiritualContinuation_StaleFirstReadyPreservesSecondOffer()
    {
        await RunSequentialSpiritualContinuationAsync(worker: false, replayFirstReady: true);
    }

    /// <summary>
    /// Consumes two successive real offers through file or worker transport and preserves one publication after cold startup.
    /// </summary>
    /// <param name="worker">
    /// Whether both decisions are authored by actual worker processes and applied through their normal Ready publisher.
    /// </param>
    /// <param name="replayFirstReady">
    /// Whether file delivery first replays the previous response at the second offer and verifies rejection before continuing.
    /// </param>
    /// <returns>
    /// A task completing after distinct offer identities, exact original inputs, single costs and receipts, and cold no-op behavior are verified.
    /// </returns>
    private async Task RunSequentialSpiritualContinuationAsync(bool worker, bool replayFirstReady = false)
    {
        Assert.False(worker && replayFirstReady);
        const string repairPath = "game_state/control/validation_repair_request.json";
        const string readyPath = "game_state/control/validation_repair_ready.json";
        var input = new QueuedConsoleInputSource([]);
        var logger = new SpiritualLifecycleTestLogger();
        var lifecycleCheckpoints = new ConcurrentQueue<string>();
        var finalizationHooks = new GameEngineSessionFinalizationHooks
        {
            AtCheckpointAsync = checkpoint =>
            {
                lifecycleCheckpoints.Enqueue($"{DateTime.UtcNow:O} {checkpoint}");
                while (lifecycleCheckpoints.Count > 24)
                    lifecycleCheckpoints.TryDequeue(out _);
                return Task.CompletedTask;
            }
        };
        GameEngine? engine = null;
        object? rollback = null;
        var request = new TurnRequest
        {
            SessionId = "session_engine_spiritual", RequestId = "request_engine_spiritual_42", TurnNumber = 42,
            PlayerAction = "Удержать встречное духовное давление.", Timestamp = DateTime.UtcNow.ToString("O"),
            PreGeneratedDices1d20 = [15, 5, 12, 8]
        };
        await using var context = await AfterlifeResourceCutoverTests.CreateSpiritualGameEngineOriginalAsync(async original =>
        {
            var chat = Assert.IsType<JsonObject>(await original.ReadJsonAsync("game_state/history/chat_log.json"));
            chat["sessionId"] = request.SessionId;
            await original.WriteExactJsonAsync("game_state/history/chat_log.json", chat.ToJsonString());
            engine = CreateGameEngine(input, configureSettings: settings =>
            {
                if (worker)
                    settings.GmWorkerBridgeProfiles.Add(CreateSequentialSpiritualWorkerProfile(original));
            }, finalizationHooks: finalizationHooks, fileSystem: original.FileSystem, logger: logger);
            await InvokePrivateTaskAsync(engine, "RefreshRuntimeStateAsync");
            GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(request.SessionId, 41);
            await InvokePrivateTaskAsync(engine, "EnsureClientOwnedSystemFilesHealthyAsync");
            request.ProgressionControl = await new ProgressionScheduleService(original.FileSystem,
                NullLogger<ProgressionScheduleService>.Instance).BuildControlForNextTurnAsync("Chaos Sea");
            rollback = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "sequential-spiritual-original");
            await original.WriteExactJsonAsync("input/turn_request.json", JsonSerializer.Serialize(request, SnapshotHashJsonOpts));
            await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync",
                request, rollback, "sequential-spiritual-original");
        });
        await AfterlifeResourceCutoverTests.WriteSpiritualGameEngineTwoSourceExchangeAsync(context);
        await WriteSpiritualLifecycleOutputsAsync(context, request);
        var debug = Assert.IsType<JsonObject>(await context.ReadJsonAsync("output/debug_logs.json"));
        debug["gm_thoughts_markdown"] = debug["gm_thoughts_markdown"]!.GetValue<string>()
            .Replace("исходные значения кубиков 15 и 5", "исходные значения кубиков 12 и 8", StringComparison.Ordinal)
            + "\n- Exchange result: player_soul и guardian_frame завершают один обмен с playerSideStrain=broken и oppositionSideStrain=broken; каждый платит 3 spiritual_action_points один раз. Новые раны не объявлены до отдельных решений клиента.";
        await context.WriteExactJsonAsync("output/debug_logs.json", debug.ToJsonString());
        var resolution = await InvokePrivateTaskResultAsync(engine!, "ResolveActivePendingTurnSnapshotContextAsync");
        Assert.Equal("Usable", resolution.GetType().GetProperty("Status")!.GetValue(resolution)!.ToString());
        var snapshot = resolution.GetType().GetProperty("Context")!.GetValue(resolution);
        Assert.NotNull(snapshot);
        var originalImages = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var path in new[] { "input/turn_request.json", "game_state/control/pending_turn_snapshot.json",
                     "game_state/control/pending_turn_snapshot.authority.json", ResourceMaterializationContract.StatePath,
                     AfterlifeSpiritualConflictState.StatePath, WoundIdentityState.StatePath, WoundHistoryState.HistoryPath })
            originalImages.Add(path, Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync(path)));

        var offers = new ConcurrentDictionary<string, SpiritualWoundContinuationRequest>(StringComparer.Ordinal);
        var offerOrder = new ConcurrentQueue<string>();
        Exception? callbackFailure = null;
        string? lastRepair = null;
        var fileResponses = 0;
        string? firstReady = null;
        var rejectedReplay = false;
        using var stop = new CancellationTokenSource();
        var callback = Task.Run(async () =>
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    var physical = context.FileSystem.ResolvePath(repairPath);
                    string? raw = null;
                    try
                    {
                        if (File.Exists(physical))
                        {
                            using var stream = new FileStream(physical, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                            using var reader = new StreamReader(stream);
                            raw = await reader.ReadToEndAsync(stop.Token);
                        }
                    }
                    catch (FileNotFoundException) { }
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        lastRepair = raw;
                        using var document = JsonDocument.Parse(raw);
                        var repair = document.RootElement;
                        Assert.True(repair.TryGetProperty("spiritualWoundContinuation", out var envelope), raw);
                        var continuation = SpiritualWoundContinuationProtocol.ReadRequest(envelope);
                        if (offers.TryAdd(continuation.ContinuationId, continuation))
                        {
                            offerOrder.Enqueue(continuation.Offer!.OpportunityRef);
                            Assert.InRange(offers.Count, 1, 2);
                            Assert.Equal("decision", continuation.Phase);
                            Assert.Empty(continuation.DependentDraftFields);
                            Assert.Empty(SpiritualWoundContinuationProtocol.ValidateRequest(continuation));
                            Assert.Equal(0, repair.GetProperty("errors").GetArrayLength());
                            Assert.False(repair.GetProperty("fullTurnResubmissionRequired").GetBoolean());
                            Assert.Equal(request.SessionId, repair.GetProperty("sessionId").GetString());
                            Assert.Equal(request.RequestId, repair.GetProperty("requestId").GetString());
                            Assert.Equal(42, repair.GetProperty("turnNumber").GetInt32());
                            if (!worker)
                            {
                                using var leaseDeadline = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                                leaseDeadline.CancelAfter(TimeSpan.FromSeconds(10));
                                await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync(
                                                 cancellationToken: leaseDeadline.Token))
                                {
                                    foreach (var pair in originalImages)
                                        Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
                                    Assert.False(context.FileSystem.FileExists(lease, SpiritualWoundOpportunityReceiptState.StatePath));
                                    Assert.True(context.FileSystem.FileExists(lease, SpiritualWoundCaptureCheckpointState.StatePath));
                                }
                                if (replayFirstReady && offers.Count == 2)
                                {
                                    Assert.NotNull(firstReady);
                                    await AssertStaleSpiritualReadyRejectedAsync(context, continuation, firstReady,
                                        originalImages, stop.Token);
                                    rejectedReplay = true;
                                }
                                var currentReady = JsonSerializer.Serialize(new
                                {
                                    sessionId = request.SessionId, requestId = request.RequestId, turnNumber = 42,
                                    timestamp = DateTime.UtcNow.ToString("O"), status = "success",
                                    spiritualWoundContinuation = new
                                    {
                                        schemaVersion = 1, continuationId = continuation.ContinuationId,
                                        woundDecisions = new[] { new { opportunityRef = continuation.Offer!.OpportunityRef, decision = "none" } }
                                    }
                                });
                                firstReady ??= currentReady;
                                await context.WriteExactJsonAsync(readyPath, currentReady);
                                fileResponses++;
                            }
                        }
                    }
                    await Task.Delay(25, stop.Token);
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            catch (Exception error)
            {
                callbackFailure = error;
                input.Enqueue(Key(ConsoleKey.Escape));
            }
        });
        var validation = InvokePrivateTaskResultAsync(engine!, "WaitForGmResponse");
        object? disposition = null;
        Exception? failure = null;
        var cleanupFailures = new List<Exception>();
        try { disposition = await validation.WaitAsync(TimeSpan.FromSeconds(worker ? 540 : replayFirstReady ? 480 : 420)); }
        catch (Exception error) { failure = error; }
        finally
        {
            stop.Cancel();
            input.Enqueue(Key(ConsoleKey.Escape));
            try { await callback.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (Exception error) { cleanupFailures.Add(error); }
            if (!validation.IsCompleted)
            {
                try { await validation.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (Exception error) { cleanupFailures.Add(error); }
            }
        }
        var failureDiagnostics = failure is not null || callbackFailure is not null || cleanupFailures.Count != 0 ||
                                 disposition is not true
            ? await ReadSequentialSpiritualFailureDiagnosticsAsync(context.FileSystem, lifecycleCheckpoints)
            : string.Empty;
        Assert.True(failure is null && callbackFailure is null && cleanupFailures.Count == 0,
            $"Validation: {failure}; callback: {callbackFailure}; cleanup: {string.Join("\n", cleanupFailures)}; " +
            $"disposition: {disposition}; offers: {offers.Count}; file responses: {fileResponses}; last repair: {lastRepair}. {logger.Describe()}\n{failureDiagnostics}");
        Assert.True(Assert.IsType<bool>(disposition),
            $"Disposition: {disposition}; last repair: {lastRepair}. {logger.Describe()}\n{failureDiagnostics}");
        Assert.Equal(42, GetPrivateField<GameLoop>(engine!, "_gameLoop").TurnNumber);
        Assert.Contains("встречное давление", GetPrivateField<GameResponse>(engine!, "_lastResponse").Response, StringComparison.Ordinal);
        Assert.Equal(2, offers.Count);
        Assert.Equal(2, offers.Values.Select(offer => offer.Offer!.OpportunityRef).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(worker ? 0 : 2, fileResponses);
        Assert.Equal(replayFirstReady, rejectedReplay);
        if (worker)
            await AssertSequentialSpiritualWorkersAsync(context, offers);
        foreach (var path in new[] { repairPath, readyPath, SpiritualWoundCaptureCheckpointState.StatePath,
                     SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath,
                     "input/turn_request.json", "ready/turn_complete.json", "ready/turn_error.json",
                     "game_state/control/pending_turn_snapshot.json", "game_state/control/pending_turn_snapshot.authority.json" })
            Assert.False(context.FileSystem.FileExists(path), path);
        var receipts = Assert.IsType<JsonObject>(await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath));
        Assert.Single(receipts["instances"]!.AsArray());
        Assert.Equal(2, receipts["sources"]!.AsArray().Count);
        Assert.Equal(2, receipts["decisions"]!.AsArray().Count);
        Assert.All(receipts["decisions"]!.AsArray(), decision => Assert.Equal("none", decision!["decision"]!.GetValue<string>()));
        Assert.Equal(offerOrder.ToArray(),
            receipts["decisions"]!.AsArray().Select(decision => decision!["opportunityRef"]!.GetValue<string>()));
        for (var index = 0; index < 2; index++)
        {
            var source = receipts["sources"]![index]!;
            var decision = receipts["decisions"]![index]!;
            Assert.Equal(index + 1, source["ordinal"]!.GetValue<int>());
            Assert.Equal(index + 1, decision["ordinal"]!.GetValue<int>());
            Assert.True(JsonNode.DeepEquals(source["witness"], decision["sourceWitness"]));
        }
        foreach (var path in new[] { WoundIdentityState.StatePath, WoundHistoryState.HistoryPath })
            Assert.Equal(originalImages[path], await context.FileSystem.ReadFileBytesAsync(path));
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath), allowMissingPristine: false);
        Assert.True(definitions.IsValid, string.Join("\n", definitions.Issues));
        var resources = ResourceStateContract.ParseCanonical(await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath),
            definitions.Catalog!, allowMissingPristine: false);
        Assert.True(resources.IsValid, string.Join("\n", resources.Issues));
        var balances = resources.Ledger!.Entries.Where(entry => entry.Coordinate.ResourceKey == "spiritual_action_points").ToArray();
        Assert.Equal(2, balances.Length);
        Assert.All(balances, entry => Assert.Equal(3m, entry.Current));
        var history = ResourceHistoryState.ParseCanonical(await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.HistoryPath),
            definitions.Catalog!, allowMissingPristine: false);
        Assert.True(history.IsValid, string.Join("\n", history.Issues));
        var spends = history.History!.Transitions.Where(entry => entry.Turn == 42 &&
            entry.OriginId == "exchange_conflict_frame_42" && entry.Operation == ResourceTransitionOperation.Spend).ToArray();
        Assert.Equal(2, spends.Length);
        Assert.All(spends, entry =>
        {
            Assert.Equal(3m, entry.AppliedAmount);
            Assert.Equal(6m, entry.BeforeState!.Current);
            Assert.Equal(3m, entry.AfterState!.Current);
        });
        var conflict = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        Assert.False(conflict.ContainsKey(AfterlifeSpiritualConflictState.ResponseField));
        var exchange = Assert.Single(conflict["activeConflict"]!["exchangeLog"]!.AsArray())!;
        Assert.Equal("exchange_conflict_frame_42", exchange["exchangeId"]!.GetValue<string>());
        Assert.Equal(new[] { 2, 3 }, exchange["diceAudit"]!["diceUsed"]!.AsArray().Select(die => die!["sourceIndex"]!.GetValue<int>()));
        Assert.Equal(new[] { 12, 8 }, exchange["diceAudit"]!["diceUsed"]!.AsArray().Select(die => die!["value"]!.GetValue<int>()));
        Assert.Equal("broken", exchange["after"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("broken", exchange["after"]!["oppositionSideStrain"]!.GetValue<string>());

        var stories = new StoryService(context.FileSystem, NullLogger<StoryService>.Instance);
        var story = await stories.ReadStoryAsync("stories/chaos_sea.jsonl");
        var acceptedStory = Assert.Single(story, entry => entry.Turn == 42);
        Assert.Equal(request.PlayerAction, acceptedStory.Player);
        Assert.Contains("встречное давление", acceptedStory.Narrative, StringComparison.Ordinal);
        var beforeCold = await ReadSpiritualEntryGuardFilesAsync(context);
        var coldFileSystem = new FileSystemManager(context.RootPath, NullLogger<FileSystemManager>.Instance);
        var coldInput = new QueuedConsoleInputSource([]);
        var coldEngine = CreateGameEngine(coldInput, fileSystem: coldFileSystem, logger: logger);
        var coldLoop = GetPrivateField<GameLoop>(coldEngine, "_gameLoop");
        await InvokePrivateTaskAsync(coldEngine, "RefreshRuntimeStateAsync");
        var persistedTurn = Assert.IsType<int>(await InvokePrivateTaskResultAsync(coldEngine, "DetectCurrentSessionTurnNumberAsync"));
        Assert.Equal(42, persistedTurn);
        var persistedSession = GetPrivateField<StateManager>(coldEngine, "_stateManager").CurrentState.SessionId;
        Assert.Equal(request.SessionId, persistedSession);
        // ContinueCurrentSessionFlow restores its runtime cursor from this persisted story and chat identity.
        coldLoop.SetSession(persistedSession, persistedTurn);
        await InvokePrivateTaskAsync(coldEngine, "NormalizePendingRepairArtifactsAsync");
        var cold = InvokePrivateTaskResultAsync(coldEngine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");
        object? coldResult = null;
        Exception? coldFailure = null;
        try { coldResult = await cold.WaitAsync(TimeSpan.FromSeconds(60)); }
        catch (Exception error) { coldFailure = error; }
        finally
        {
            if (!cold.IsCompleted)
            {
                coldInput.Enqueue(Key(ConsoleKey.Escape));
                try { await cold.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (Exception error) { cleanupFailures.Add(error); }
            }
        }
        Assert.True(coldFailure is null && cleanupFailures.Count == 0,
            $"Cold lifecycle: {coldFailure}; cleanup: {string.Join("\n", cleanupFailures)}. {logger.Describe()}");
        Assert.False(Assert.IsType<bool>(coldResult), logger.Describe());
        Assert.Equal(42, coldLoop.TurnNumber);
        await AssertSpiritualEntryGuardFilesAsync(context, beforeCold);
    }

    /// <summary>
    /// Collects bounded physical worker and continuation diagnostics before a failed fixture is disposed, without acquiring a canonical lease.
    /// </summary>
    /// <param name="fs">
    /// Filesystem used only to resolve physical paths; no canonical read or mutation APIs are called.
    /// </param>
    /// <param name="checkpoints">
    /// At most twenty-four timestamped actual lifecycle checkpoints retained by the hook.
    /// </param>
    /// <returns>
    /// Bounded audit, Ready, task and C2 summaries; read or parse failures are diagnostic text and never replace the primary failure.
    /// </returns>
    private static async Task<string> ReadSequentialSpiritualFailureDiagnosticsAsync(FileSystemManager fs,
        ConcurrentQueue<string> checkpoints)
    {
        var lines = new List<string> { "Physical diagnostics at " + DateTime.UtcNow.ToString("O"),
            "Lifecycle checkpoints:\n" + string.Join("\n", checkpoints.ToArray()) };
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        foreach (var path in new[] { GmWorkerAuditLog.AuditLogPath,
                     "game_state/control/validation_repair_ready.json", SpiritualWoundCaptureCheckpointState.StatePath,
                     SpiritualWoundDecisionPendingState.StatePath, GmWorkerValidationRepairDelegator.LatestValidationRepairTaskPath })
        {
            try
            {
                var text = await ReadSequentialSpiritualPhysicalDiagnosticAsync(fs.ResolvePath(path),
                    tail: path == GmWorkerAuditLog.AuditLogPath, deadline.Token);
                if (text is null)
                {
                    lines.Add(path + ": <absent>");
                    continue;
                }
                string summary;
                if (path == GmWorkerAuditLog.AuditLogPath)
                {
                    summary = string.Join("\n", text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .TakeLast(12).Select(row => row.Length > 1500 ? row[..1500] + " [truncated]" : row));
                }
                else
                {
                    var root = JsonNode.Parse(text)!.AsObject();
                    if (path == SpiritualWoundCaptureCheckpointState.StatePath)
                    {
                        var body = root["checkpoint"];
                        summary = new JsonObject
                        {
                            ["checkpointPresent"] = body is not null,
                            ["committedAdvance"] = body?["committedAdvance"]?.DeepClone(),
                            ["advancesCount"] = (body?["advances"] as JsonArray)?.Count,
                            ["pendingSubmissionPresent"] = body?["pendingSubmission"] is not null,
                            ["checkpointFingerprint"] = body?["checkpointFingerprint"]?.DeepClone()
                        }.ToJsonString();
                    }
                    else if (path == SpiritualWoundDecisionPendingState.StatePath)
                    {
                        var pending = root["pending"];
                        summary = new JsonObject
                        {
                            ["pendingPresent"] = pending is not null,
                            ["packetFingerprint"] = pending?["packetFingerprint"]?.DeepClone(),
                            ["sourcesCount"] = (pending?["sources"] as JsonArray)?.Count,
                            ["decisionsCount"] = (pending?["decisions"] as JsonArray)?.Count
                        }.ToJsonString();
                    }
                    else if (path == GmWorkerValidationRepairDelegator.LatestValidationRepairTaskPath)
                    {
                        summary = new JsonObject
                        {
                            ["taskId"] = root["taskId"]?.DeepClone(),
                            ["workerId"] = root["workerId"]?.DeepClone(),
                            ["sourceTurn"] = root["sourceTurn"]?.DeepClone(),
                            ["phase"] = root["spiritualWoundContinuation"]?["phase"]?.DeepClone(),
                            ["continuationId"] = root["spiritualWoundContinuation"]?["continuationId"]?.DeepClone()
                        }.ToJsonString();
                    }
                    else
                    {
                        summary = new JsonObject
                        {
                            ["sessionId"] = root["sessionId"]?.DeepClone(), ["requestId"] = root["requestId"]?.DeepClone(),
                            ["turnNumber"] = root["turnNumber"]?.DeepClone(), ["status"] = root["status"]?.DeepClone(),
                            ["spiritualWoundContinuation"] = root["spiritualWoundContinuation"]?.DeepClone()
                        }.ToJsonString();
                    }
                    if (summary.Length > 6000)
                        summary = summary[..6000] + " [truncated]";
                }
                lines.Add(path + ": " + summary);
            }
            catch (Exception error)
            {
                lines.Add(path + ": diagnostic unavailable: " + error.GetType().Name + ": " + error.Message);
            }
        }
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Reads a bounded shared physical image while the engine may still be working or retaining its canonical lease.
    /// </summary>
    /// <param name="path">
    /// Resolved physical diagnostic file; an absent path returns <see langword="null"/>.
    /// </param>
    /// <param name="tail">
    /// Whether to read the final 64 KiB of an audit log instead of the first 1 MiB of a JSON image.
    /// </param>
    /// <param name="cancellationToken">
    /// Shared five-second deadline for all failure diagnostics.
    /// </param>
    /// <returns>
    /// Decoded bounded content without a UTF-8 preamble, or <see langword="null"/> for an absent file.
    /// </returns>
    private static async Task<string?> ReadSequentialSpiritualPhysicalDiagnosticAsync(string path, bool tail,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return null;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
        var limit = tail ? 65536 : 1048576;
        var offset = tail ? Math.Max(0, stream.Length - limit) : 0;
        stream.Position = offset;
        var bytes = new byte[(int)Math.Min(stream.Length - offset, limit)];
        var read = 0;
        while (read < bytes.Length)
        {
            var count = await stream.ReadAsync(bytes.AsMemory(read), cancellationToken);
            if (count == 0)
                break;
            read += count;
        }
        var text = Encoding.UTF8.GetString(bytes, 0, read).TrimStart('\uFEFF');
        if (offset > 0 && text.IndexOf('\n') is var newline && newline >= 0)
            text = text[(newline + 1)..];
        return text;
    }

    /// <summary>
    /// Creates a real worker response producer whose proposal identity belongs to each distinct dispatched task.
    /// </summary>
    /// <param name="context">
    /// Fixture root receiving the process script outside canonical session state.
    /// </param>
    /// <returns>
    /// A validation-repair worker profile that authors only explicit none decisions in its reserved proposal workspace.
    /// </returns>
    private static WorkerBridgeProfile CreateSequentialSpiritualWorkerProfile(ResourceMaterializationTestContext context)
    {
        var scriptPath = Path.Combine(context.RootPath, "spiritual-sequential-worker.ps1");
        File.WriteAllText(scriptPath, """
            $ErrorActionPreference = 'Stop'
            $task = Get-Content -Raw -LiteralPath $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
            $continuation = $task.spiritualWoundContinuation
            if ($null -eq $continuation -or $continuation.phase -ne 'decision') { throw 'Expected an actual decision task.' }
            if ($task.sourceTurn.sessionId -ne 'session_engine_spiritual' -or $task.sourceTurn.requestId -ne 'request_engine_spiritual_42' -or $task.sourceTurn.turnNumber -ne 42) { throw 'Original turn identity changed.' }
            $proposal = [ordered]@{
                schemaVersion = 1; proposalId = ('proposal_' + $task.taskId); taskId = $task.taskId; workerId = $task.workerId
                status = 'completed'; summary = 'Explicitly declined the current sequential spiritual offer.'
                changedFiles = @(); findings = @()
                spiritualWoundContinuation = [ordered]@{
                    schemaVersion = 1; continuationId = $continuation.continuationId
                    woundDecisions = @([ordered]@{ opportunityRef = $continuation.offer.opportunityRef; decision = 'none' })
                }
                selfCheck = [ordered]@{ scopeReviewed = $true; validationExpectedToPass = $true; notes = @() }
                createdAtUtc = [DateTime]::UtcNow.ToString('O')
            }
            $proposal | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
            """);
        return GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile() with
        {
            LaunchCommand = $"pwsh.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            TimeoutSeconds = 10
        };
    }

    /// <summary>
    /// Verifies two actual worker dispatches, applied proposals and Ready publications against the observed owner-issued offers.
    /// </summary>
    /// <param name="context">
    /// Accepted session retaining real worker task, proposal and audit files.
    /// </param>
    /// <param name="offers">
    /// Distinct public continuation requests observed during the engine's waits.
    /// </param>
    /// <returns>
    /// A task completing after each offer has exactly one separately correlated worker response and apply/Ready event.
    /// </returns>
    private static async Task AssertSequentialSpiritualWorkersAsync(ResourceMaterializationTestContext context,
        IReadOnlyDictionary<string, SpiritualWoundContinuationRequest> offers)
    {
        var events = await new GmWorkerAuditLog(context.FileSystem).ReadEventsAsync();
        var dispatched = events.Where(entry => entry.EventType == "task-dispatched").ToArray();
        Assert.Equal(2, dispatched.Length);
        foreach (var eventType in new[] { "proposal-applied", "validation-repair-ready-created" })
            Assert.Equal(2, events.Count(entry => entry.EventType == eventType));
        Assert.Equal(2, dispatched.Select(entry => entry.TaskId).Distinct(StringComparer.Ordinal).Count());
        var responded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dispatch in dispatched)
        {
            var task = GmWorkerJson.Deserialize<WorkerTaskPacket>((await context.FileSystem.ReadFileAsync(
                GmWorkerBridgePool.GetTaskPacketPath(dispatch.TaskId!)))!);
            Assert.NotNull(task);
            Assert.Equal("session_engine_spiritual", task.SourceTurn.SessionId);
            Assert.Equal("request_engine_spiritual_42", task.SourceTurn.RequestId);
            Assert.Equal(42, task.SourceTurn.TurnNumber);
            Assert.Empty(task.ValidationIssues);
            var request = Assert.IsType<SpiritualWoundContinuationRequest>(task.SpiritualWoundContinuation);
            Assert.Equal(JsonSerializer.Serialize(offers[request.ContinuationId]), JsonSerializer.Serialize(request));
            Assert.True(responded.Add(request.ContinuationId));
            var proposal = GmWorkerJson.Deserialize<WorkerProposal>((await context.FileSystem.ReadFileAsync(
                GmWorkerBridgePool.GetProposalInboxPath(task.TaskId)))!);
            Assert.NotNull(proposal);
            Assert.Equal("proposal_" + task.TaskId, proposal.ProposalId);
            Assert.Equal(task.TaskId, proposal.TaskId);
            Assert.Empty(proposal.ChangedFiles);
            var response = Assert.IsType<SpiritualWoundContinuationResponse>(proposal.SpiritualWoundContinuation);
            Assert.Empty(SpiritualWoundContinuationProtocol.ValidateResponse(request, response));
            Assert.Equal("none", Assert.Single(response.WoundDecisions).GetProperty("decision").GetString());
            foreach (var eventType in new[] { "proposal-applied", "validation-repair-ready-created" })
                Assert.Single(events, entry => entry.EventType == eventType && entry.TaskId == task.TaskId);
        }
        Assert.Equal(offers.Keys.Order(StringComparer.Ordinal), responded.Order(StringComparer.Ordinal));
    }
}
