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
    /// Delivers two real file decisions through the full accepted caller and presents creation then worsening of one wound exactly once.
    /// </summary>
    /// <returns>
    /// A task completing after exact original inputs, two ordered real notifications, one publication and terminal cleanup are verified.
    /// </returns>
    [Fact]
    public async Task AcceptedTurnSpiritualContinuation_FileCreateThenWorsenPresentsOrderedNotificationsOnce()
    {
        const string repairPath = "game_state/control/validation_repair_request.json";
        const string readyPath = "game_state/control/validation_repair_ready.json";
        const string firstNarration = "Чужое давление надломило волю хранителя.";
        const string laterNarration = "Новый натиск углубил надлом воли хранителя.";
        var input = new QueuedConsoleInputSource([]);
        var logger = new SpiritualLifecycleTestLogger();
        var checkpoints = new ConcurrentQueue<string>();
        var armed = false;
        var publicationWrites = 0;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = path =>
            {
                if (armed && path == SpiritualWoundOpportunityReceiptState.StatePath)
                    Interlocked.Increment(ref publicationWrites);
                return Task.CompletedTask;
            }
        };
        var finalization = new GameEngineSessionFinalizationHooks
        {
            AtCheckpointAsync = checkpoint =>
            {
                checkpoints.Enqueue($"{DateTime.UtcNow:O} {checkpoint}");
                while (checkpoints.Count > 24) checkpoints.TryDequeue(out _);
                return Task.CompletedTask;
            }
        };
        GameEngine? engine = null;
        var request = new TurnRequest
        {
            SessionId = "session_engine_spiritual", RequestId = "request_engine_spiritual_42", TurnNumber = 42,
            PlayerAction = "Удержать встречное духовное давление.", Timestamp = DateTime.UtcNow.ToString("O"),
            PreGeneratedDices1d20 = [15, 5, 18, 3]
        };
        await using var context = await AfterlifeResourceCutoverTests.CreateSpiritualGameEngineOriginalAsync(async original =>
        {
            await AfterlifeResourceCutoverTests.SeedSpiritualWorseningTierAuthorityAsync(original);
            var chat = Assert.IsType<JsonObject>(await original.ReadJsonAsync("game_state/history/chat_log.json"));
            chat["sessionId"] = request.SessionId;
            await original.WriteExactJsonAsync("game_state/history/chat_log.json", chat.ToJsonString());
            engine = CreateGameEngine(input, finalizationHooks: finalization, fileSystem: original.FileSystem, logger: logger);
            await InvokePrivateTaskAsync(engine, "RefreshRuntimeStateAsync");
            GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(request.SessionId, 41);
            await InvokePrivateTaskAsync(engine, "EnsureClientOwnedSystemFilesHealthyAsync");
            request.ProgressionControl = await new ProgressionScheduleService(original.FileSystem,
                NullLogger<ProgressionScheduleService>.Instance).BuildControlForNextTurnAsync("Chaos Sea");
            var rollback = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "worsening-spiritual-original");
            await original.WriteExactJsonAsync("input/turn_request.json", JsonSerializer.Serialize(request, SnapshotHashJsonOpts));
            await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, rollback, "worsening-spiritual-original");
        }, hooks: hooks);
        await AfterlifeResourceCutoverTests.WriteSpiritualWorseningExchangesAsync(context);
        await WriteSpiritualWorseningLifecycleOutputsAsync(context, request);
        var originalImages = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var path in new[] { "input/turn_request.json", "game_state/control/pending_turn_snapshot.json",
                     "game_state/control/pending_turn_snapshot.authority.json", ResourceMaterializationContract.StatePath,
                     ResourceMaterializationContract.HistoryPath, AfterlifeSpiritualConflictState.StatePath,
                     AfterlifeEntityProfileState.StatePath, WoundIdentityState.StatePath, WoundHistoryState.HistoryPath })
            originalImages.Add(path, Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync(path)));
        armed = true;

        var offers = new ConcurrentQueue<SpiritualWoundContinuationRequest>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        Exception? callbackFailure = null;
        string? lastRepair = null;
        using var stop = new CancellationTokenSource();
        var callback = Task.Run(async () =>
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    string? raw = null;
                    try
                    {
                        raw = await ReadSequentialSpiritualPhysicalDiagnosticAsync(
                            context.FileSystem.ResolvePath(repairPath), tail: false, stop.Token);
                    }
                    catch (FileNotFoundException) { }
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        lastRepair = raw;
                        using var document = JsonDocument.Parse(raw);
                        var repair = document.RootElement;
                        Assert.True(repair.TryGetProperty("spiritualWoundContinuation", out var envelope), raw);
                        var continuation = SpiritualWoundContinuationProtocol.ReadRequest(envelope);
                        if (seen.Add(continuation.ContinuationId))
                        {
                            offers.Enqueue(continuation);
                            Assert.InRange(offers.Count, 1, 2);
                            Assert.Equal("decision", continuation.Phase);
                            Assert.Empty(continuation.DependentDraftFields);
                            Assert.Empty(SpiritualWoundContinuationProtocol.ValidateRequest(continuation));
                            Assert.Equal(request.SessionId, repair.GetProperty("sessionId").GetString());
                            Assert.Equal(request.RequestId, repair.GetProperty("requestId").GetString());
                            Assert.Equal(42, repair.GetProperty("turnNumber").GetInt32());
                            Assert.Equal(0, repair.GetProperty("errors").GetArrayLength());
                            Assert.False(repair.GetProperty("fullTurnResubmissionRequired").GetBoolean());
                            var worsening = offers.Count == 2;
                            if (worsening) Assert.Equal(2, continuation.Offer!.MinimumSeverityRank);
                            using var leaseDeadline = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                            leaseDeadline.CancelAfter(TimeSpan.FromSeconds(10));
                            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync(
                                             cancellationToken: leaseDeadline.Token))
                            {
                                foreach (var pair in originalImages)
                                    Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
                                Assert.False(context.FileSystem.FileExists(lease, SpiritualWoundOpportunityReceiptState.StatePath));
                                var checkpoint = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease,
                                    SpiritualWoundCaptureCheckpointState.StatePath))!)!["checkpoint"]!;
                                Assert.Equal(worsening ? 1 : 0, checkpoint["committedAdvance"]!.GetValue<int>());
                            }
                            await context.WriteExactJsonAsync("output/narrative_response.json", JsonSerializer.Serialize(new
                            {
                                response = worsening ? firstNarration + " " + laterNarration : firstNarration,
                                timestamp = DateTime.UtcNow.ToString("O")
                            }));
                            var decision = AfterlifeResourceCutoverTests.CreateSpiritualWorseningLifecycleDecision(
                                continuation.Offer!.OpportunityRef, worsening);
                            await context.WriteExactJsonAsync(readyPath, JsonSerializer.Serialize(new
                            {
                                sessionId = request.SessionId, requestId = request.RequestId, turnNumber = 42,
                                timestamp = DateTime.UtcNow.ToString("O"), status = "success",
                                spiritualWoundContinuation = new
                                {
                                    schemaVersion = 1, continuationId = continuation.ContinuationId,
                                    woundDecisions = new[] { decision }
                                }
                            }));
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
        var operation = InvokePrivateTaskResultAsync(engine!, "WaitForGmResponse");
        object? result = null;
        Exception? failure = null;
        var cleanup = new List<Exception>();
        try { result = await operation.WaitAsync(TimeSpan.FromSeconds(540)); }
        catch (Exception error) { failure = error; }
        finally
        {
            stop.Cancel();
            input.Enqueue(Key(ConsoleKey.Escape));
            try { await callback.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (Exception error) { cleanup.Add(error); }
            if (!operation.IsCompleted)
            {
                try { await operation.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (Exception error) { cleanup.Add(error); }
            }
        }
        var diagnostics = failure is not null || callbackFailure is not null || cleanup.Count != 0 || result is not true
            ? await ReadSequentialSpiritualFailureDiagnosticsAsync(context.FileSystem, checkpoints) : string.Empty;
        Assert.True(failure is null && callbackFailure is null && cleanup.Count == 0 && result is true,
            $"Caller: {failure}; callback: {callbackFailure}; cleanup: {string.Join("\n", cleanup)}; result: {result}; " +
            $"offers: {offers.Count}; last repair: {lastRepair}. {logger.Describe()}\n{diagnostics}");
        Assert.Equal(2, offers.Count);
        Assert.Equal(2, offers.Select(offer => offer.ContinuationId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(2, offers.Select(offer => offer.Offer!.OpportunityRef).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(1, publicationWrites);
        Assert.Equal(42, GetPrivateField<GameLoop>(engine!, "_gameLoop").TurnNumber);
        await AssertSpiritualWorseningLifecyclePublicationAsync(context, engine!, offers.ToArray(),
            originalImages[AfterlifeSpiritualConflictState.StatePath], firstNarration, laterNarration);
    }

    /// <summary>
    /// Checks actual published identities, causal resource history, ordered response notifications and terminal cleanup.
    /// </summary>
    /// <param name="context">
    /// Full physical session after the actual successful caller completes.
    /// </param>
    /// <param name="engine">
    /// Engine holding the real final response and advanced turn.
    /// </param>
    /// <param name="offers">
    /// Both actual file offers in delivery order.
    /// </param>
    /// <param name="originalDraft">
    /// Original raw two-exchange draft whose actions, dice and costs must survive publication.
    /// </param>
    /// <param name="firstNarration">
    /// Exact rank-I acquisition scene committed with the first decision.
    /// </param>
    /// <param name="laterNarration">
    /// Exact rank-II worsening scene committed with the second decision.
    /// </param>
    /// <returns>
    /// A task completing after one create/worsen chain, one story and one-shot player presentation are proved.
    /// </returns>
    private static async Task AssertSpiritualWorseningLifecyclePublicationAsync(ResourceMaterializationTestContext context,
        GameEngine engine, SpiritualWoundContinuationRequest[] offers, byte[] originalDraft,
        string firstNarration, string laterNarration)
    {
        var receipt = Assert.IsType<JsonObject>(await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath));
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(receipt.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
        Assert.Single(receipt["instances"]!.AsArray());
        Assert.Equal(2, receipt["sources"]!.AsArray().Count);
        var decisions = receipt["decisions"]!.AsArray();
        Assert.Equal(2, decisions.Count);
        var woundId = decisions[0]!["woundId"]!.GetValue<string>();
        for (var index = 0; index < 2; index++)
        {
            Assert.Equal("materialize", decisions[index]!["decision"]!.GetValue<string>());
            Assert.Equal(offers[index].Offer!.OpportunityRef, decisions[index]!["opportunityRef"]!.GetValue<string>());
            Assert.Equal(woundId, decisions[index]!["woundId"]!.GetValue<string>());
            Assert.Equal(index + 1, decisions[index]!["selectedSeverityRank"]!.GetValue<int>());
        }
        Assert.NotEqual(decisions[0]!["transitionId"]!.GetValue<string>(), decisions[1]!["transitionId"]!.GetValue<string>());
        var identity = WoundIdentityState.Parse(await context.FileSystem.ReadFileAsync(WoundIdentityState.StatePath), WoundIdentityState.StatePath);
        Assert.True(identity.IsValid, string.Join("\n", identity.Issues));
        Assert.Equal(woundId, Assert.Single(identity.State!.Entries).WoundId);
        var history = WoundHistoryState.Parse(await context.FileSystem.ReadFileAsync(WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
        Assert.True(history.IsValid, string.Join("\n", history.Issues));
        Assert.Equal(new[] { "create", "worsen" }, history.State!.Transitions.Select(transition => transition.Kind));
        Assert.Equal(decisions.Select(decision => decision!["transitionId"]!.GetValue<string>()),
            history.State.Transitions.Select(transition => transition.TransitionId));
        Assert.All(history.State.Transitions, transition =>
        {
            Assert.Equal(woundId, transition.WoundId);
            Assert.Equal(42, transition.Turn);
        });
        var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
        var carriers = WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(null, null, null, null, profiles));
        Assert.Empty(carriers.Issues);
        Assert.True(carriers.TryResolveOne(woundId, out var occurrence));
        Assert.Equal(2, occurrence.Wound.Severity.Rank);
        Assert.Equal("worsen", occurrence.Wound.LastTransition.Kind);
        var roots = occurrence.Wound.Consequences.OwnedEffectSources.RootBindings;
        Assert.Equal(2, roots.Count);
        var effects = EffectIdentityState.Parse(JsonSerializer.SerializeToElement(
            await context.ReadJsonAsync(EffectIdentityState.StatePath)), EffectIdentityState.StatePath);
        Assert.Empty(effects.Issues);
        Assert.Equal(roots.Select(root => root.EffectId).Order(StringComparer.Ordinal),
            effects.State!.Entries.Where(entry => entry.State == "active").Select(entry => entry.EffectId).Order(StringComparer.Ordinal));
        Assert.All(effects.State.Entries, entry =>
        {
            Assert.Equal(woundId, entry.Source["sourceId"]!.GetValue<string>());
            Assert.Equal(42, entry.CreatedAtTurn);
        });
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath), allowMissingPristine: false);
        Assert.True(definitions.IsValid, string.Join("\n", definitions.Issues));
        var resources = ResourceStateContract.ParseCanonical(await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath),
            definitions.Catalog!, allowMissingPristine: false);
        Assert.True(resources.IsValid, string.Join("\n", resources.Issues));
        var balances = resources.Ledger!.Entries.Where(entry => entry.Coordinate.ResourceKey == "spiritual_action_points").ToArray();
        Assert.Equal(2, balances.Length);
        Assert.All(balances, entry => Assert.Equal(4m, entry.Current));
        var resourceHistory = ResourceHistoryState.ParseCanonical(await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.HistoryPath),
            definitions.Catalog!, allowMissingPristine: false);
        Assert.True(resourceHistory.IsValid, string.Join("\n", resourceHistory.Issues));
        var spends = resourceHistory.History!.Transitions.Where(entry => entry.Turn == 42 &&
            entry.Operation == ResourceTransitionOperation.Spend && entry.Coordinate.ResourceKey == "spiritual_action_points").ToArray();
        Assert.Equal(4, spends.Length);
        foreach (var balance in balances)
        {
            var ordered = spends.Where(entry => entry.Coordinate == balance.Coordinate).ToArray();
            Assert.Equal(new[] { 6m, 5m }, ordered.Select(entry => entry.BeforeState!.Current));
            Assert.Equal(new[] { 5m, 4m }, ordered.Select(entry => entry.AfterState!.Current));
            Assert.All(ordered, entry => Assert.Equal(1m, entry.AppliedAmount));
        }
        var conflict = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var original = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(originalDraft))!;
        Assert.True(JsonNode.DeepEquals(original["activeConflict"]!["exchangeLog"], conflict["activeConflict"]!["exchangeLog"]));
        var response = GetPrivateField<GameResponse>(engine, "_lastResponse");
        Assert.Contains(firstNarration, response.Response, StringComparison.Ordinal);
        Assert.Contains(laterNarration, response.Response, StringComparison.Ordinal);
        var notifications = Assert.IsType<string[]>(response.WoundNotifications);
        Assert.Equal(2, notifications.Length);
        Assert.Contains("Получена духовная рана", notifications[0], StringComparison.Ordinal);
        Assert.Contains("(I)", notifications[0], StringComparison.Ordinal);
        Assert.Contains("Духовная рана ухудшилась", notifications[1], StringComparison.Ordinal);
        Assert.Contains("(II)", notifications[1], StringComparison.Ordinal);
        Assert.All(notifications, notification => Assert.Contains("/раны", notification, StringComparison.Ordinal));
        var refreshed = Assert.IsType<GameResponse>(await InvokePrivateTaskResultAsync(engine, "BuildGameResponseFromFiles"));
        Assert.True(refreshed.WoundNotifications is null or { Length: 0 });
        var stories = new StoryService(context.FileSystem, NullLogger<StoryService>.Instance);
        var story = Assert.Single(await stories.ReadStoryAsync("stories/chaos_sea.jsonl"), entry => entry.Turn == 42);
        Assert.Equal("Удержать встречное духовное давление.", story.Player);
        Assert.Equal(response.Response, story.Narrative);
        foreach (var path in new[] { "input/turn_request.json", "ready/turn_complete.json", "ready/turn_error.json",
                     "game_state/control/pending_turn_snapshot.json", "game_state/control/pending_turn_snapshot.authority.json",
                     "game_state/control/validation_repair_request.json", "game_state/control/validation_repair_ready.json",
                     SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath,
                     AcceptedMechanicsPlan.WoundCommandPath })
            Assert.False(context.FileSystem.FileExists(path), path);
    }
}
