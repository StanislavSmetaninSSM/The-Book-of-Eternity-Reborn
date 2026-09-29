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
    /// Proves a real ordinary source permits rank IV while the GM lawfully chooses a new rank-II wound.
    /// </summary>
    /// <returns>
    /// A task completing after actual file decisions, one publication and final caller cleanup.
    /// </returns>
    [Fact]
    public Task SpiritualSourceCeilings_OrdinaryAllowsIVAndPublishesChosenII() =>
        RunSpiritualSourceCeilingLifecycleAsync(special: false, capped: false);

    /// <summary>
    /// Proves an original special art without an envelope adds no ceiling below the ordinary rank-IV calculation.
    /// </summary>
    /// <returns>
    /// A task completing after the real special cost and chosen rank-II wound are published once.
    /// </returns>
    [Fact]
    public Task SpiritualSourceCeilings_UnrestrictedSpecialAllowsIVAndPublishesChosenII() =>
        RunSpiritualSourceCeilingLifecycleAsync(special: true, capped: false);

    /// <summary>
    /// Proves an originally signed special-art cap limits the rank-IV calculation to an actual rank-II offer.
    /// </summary>
    /// <returns>
    /// A task completing after a capped rank-II creation and the actual final response are verified.
    /// </returns>
    [Fact]
    public Task SpiritualSourceCeilings_OriginalSpecialCapIIConstrainsIVAndPublishesII() =>
        RunSpiritualSourceCeilingLifecycleAsync(special: true, capped: true);

    /// <summary>
    /// Runs a genuine signed two-exchange turn, declines its first source and submits a rank-II creation to the second offer.
    /// </summary>
    /// <param name="special">
    /// Whether the second source uses a special art materialized before the original snapshot.
    /// </param>
    /// <param name="capped">
    /// Whether that original art limits severity to II; only valid with <paramref name="special"/>.
    /// </param>
    /// <returns>
    /// A task completing after exact offers, unchanged original inputs and the full successful caller are checked.
    /// </returns>
    private async Task RunSpiritualSourceCeilingLifecycleAsync(bool special, bool capped)
    {
        const string repairPath = "game_state/control/validation_repair_request.json";
        const string readyPath = "game_state/control/validation_repair_ready.json";
        const string firstNarration = "Первое давление не оставило духовной раны.";
        const string laterNarration = AfterlifeResourceCutoverTests.SourceCeilingAcquisitionNarration;
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
            await AfterlifeResourceCutoverTests.SeedSpiritualSourceCeilingAuthorityAsync(original, special, capped);
            var chat = Assert.IsType<JsonObject>(await original.ReadJsonAsync("game_state/history/chat_log.json"));
            chat["sessionId"] = request.SessionId;
            await original.WriteExactJsonAsync("game_state/history/chat_log.json", chat.ToJsonString());
            engine = CreateGameEngine(input, finalizationHooks: finalization, fileSystem: original.FileSystem, logger: logger);
            await InvokePrivateTaskAsync(engine, "RefreshRuntimeStateAsync");
            GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(request.SessionId, 41);
            await InvokePrivateTaskAsync(engine, "EnsureClientOwnedSystemFilesHealthyAsync");
            request.ProgressionControl = await new ProgressionScheduleService(original.FileSystem,
                NullLogger<ProgressionScheduleService>.Instance).BuildControlForNextTurnAsync("Chaos Sea");
            var rollback = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "source-ceiling-spiritual-original");
            await original.WriteExactJsonAsync("input/turn_request.json", JsonSerializer.Serialize(request, SnapshotHashJsonOpts));
            await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, rollback, "source-ceiling-spiritual-original");
        }, hooks: hooks);
        await AfterlifeResourceCutoverTests.WriteSpiritualSourceCeilingExchangesAsync(context, special);
        await WriteSpiritualSourceCeilingOutputsAsync(context, request, special, capped);
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
                            var second = offers.Count == 2;
                            Assert.Equal(1, continuation.Offer!.MinimumSeverityRank);
                            Assert.Equal(second ? (capped ? 2 : 4) : 1, continuation.Offer.MaximumSeverityRank);
                            Assert.Null(continuation.Offer.RequiredSeverityRank);
                            Assert.Equal(new[] { "none", "materialize" }, continuation.Offer.AllowedDecisions);
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
                                Assert.Equal(second ? 1 : 0, checkpoint["committedAdvance"]!.GetValue<int>());
                            }
                            await context.WriteExactJsonAsync("output/narrative_response.json", JsonSerializer.Serialize(new
                            {
                                response = second ? firstNarration + " " + laterNarration : firstNarration,
                                timestamp = DateTime.UtcNow.ToString("O")
                            }));
                            var decision = second
                                ? AfterlifeResourceCutoverTests.CreateSpiritualSourceCeilingDecision(continuation.Offer!.OpportunityRef)
                                : JsonSerializer.SerializeToElement(new
                                {
                                    opportunityRef = continuation.Offer!.OpportunityRef, decision = "none"
                                });
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
        await AssertSpiritualSourceCeilingPublicationAsync(context, engine!, offers.ToArray(),
            originalImages[AfterlifeSpiritualConflictState.StatePath], special, capped, firstNarration, laterNarration);
    }

    /// <summary>
    /// Checks both exact source witnesses, the single rank-II effect group, causal costs and final response cleanup.
    /// </summary>
    /// <param name="context">
    /// Full physical session after the actual successful caller completes.
    /// </param>
    /// <param name="engine">
    /// Engine holding the real final response and advanced turn.
    /// </param>
    /// <param name="offers">
    /// The real none-then-materialize file offers in delivery order.
    /// </param>
    /// <param name="originalDraft">
    /// Original raw two-exchange draft whose actions, dice and costs must survive publication.
    /// </param>
    /// <param name="special">
    /// Whether the second source and cost must be bound to the original special art.
    /// </param>
    /// <param name="capped">
    /// Whether its original source envelope limits the otherwise rank-IV result to II.
    /// </param>
    /// <param name="firstNarration">
    /// Exact no-wound narration submitted with the first decline.
    /// </param>
    /// <param name="laterNarration">
    /// Exact rank-II acquisition narration submitted with the second decision.
    /// </param>
    /// <returns>
    /// A task completing after one creation, its two effect roots, exact source ceilings and one-shot presentation are proved.
    /// </returns>
    private static async Task AssertSpiritualSourceCeilingPublicationAsync(ResourceMaterializationTestContext context,
        GameEngine engine, SpiritualWoundContinuationRequest[] offers, byte[] originalDraft,
        bool special, bool capped, string firstNarration, string laterNarration)
    {
        var receipt = Assert.IsType<JsonObject>(await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath));
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(receipt.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
        Assert.Single(receipt["instances"]!.AsArray());
        Assert.Empty(receipt["closures"]!.AsArray());
        var original = JsonNode.Parse(originalDraft)!;
        Assert.Equal(2, receipt["sources"]!.AsArray().Count);
        var decisions = receipt["decisions"]!.AsArray();
        Assert.Equal(2, decisions.Count);
        Assert.Equal("none", decisions[0]!["decision"]!.GetValue<string>());
        Assert.Null(decisions[0]!["woundId"]);
        Assert.Null(decisions[0]!["transitionId"]);
        Assert.Null(decisions[0]!["selectedSeverityRank"]);
        Assert.Equal("materialize", decisions[1]!["decision"]!.GetValue<string>());
        Assert.Equal(2, decisions[1]!["selectedSeverityRank"]!.GetValue<int>());
        var woundId = decisions[1]!["woundId"]!.GetValue<string>();
        for (var index = 0; index < 2; index++)
        {
            Assert.Equal(offers[index].Offer!.OpportunityRef, decisions[index]!["opportunityRef"]!.GetValue<string>());
            var witness = receipt["sources"]![index]!["witness"]!;
            Assert.True(JsonNode.DeepEquals(witness, decisions[index]!["sourceWitness"]));
            Assert.Equal(original["activeConflict"]!["exchangeLog"]![index]!["exchangeId"]!.GetValue<string>(),
                witness["exchangeId"]!.GetValue<string>());
            Assert.Equal(index, witness["exchangeOrdinal"]!.GetValue<int>());
            Assert.Equal("opposition", witness["affectedSide"]!.GetValue<string>());
            Assert.Equal("session_engine_spiritual", decisions[index]!["binding"]!["sessionId"]!.GetValue<string>());
            Assert.Equal("request_engine_spiritual_42", decisions[index]!["binding"]!["requestId"]!.GetValue<string>());
            Assert.Equal(42, decisions[index]!["binding"]!["turn"]!.GetValue<int>());
        }
        var source = receipt["sources"]![1]!["witness"]!.AsObject();
        var calculation = SpiritualWoundSourceWitness.Validate(source);
        Assert.Equal(15L, calculation.Input.HarmfulMargin);
        Assert.Equal(2, calculation.Input.AppliedArtTier);
        Assert.Equal(0, calculation.Input.TargetResilienceTier);
        Assert.Equal(31L, calculation.TraumaPressure);
        Assert.Equal(4, calculation.FormulaSeverityRank);
        Assert.Equal(4, calculation.DestinationSeverityCap);
        Assert.Equal(4, calculation.DangerModeSeverityCap);
        Assert.Equal(capped ? 2 : 4, calculation.Input.SourceSeverityCap);
        Assert.Equal(capped ? 2 : 4, calculation.MaximumSeverityRank);
        Assert.Equal(special ? "special" : "standard", source["appliedArtKind"]!.GetValue<string>());
        Assert.Null(source["guaranteedSeverityRank"]);
        if (special)
        {
            var originalArt = JsonNode.Parse(Convert.FromBase64String(source["specialArtJsonBase64"]!.GetValue<string>()))!;
            Assert.Equal("art_source_owner", source["appliedArtId"]!.GetValue<string>());
            Assert.Equal(2, originalArt["tier"]!.GetValue<int>());
            Assert.Equal(200, originalArt["costMultiplierPercent"]!.GetValue<int>());
            if (capped) Assert.Equal(2, originalArt["spiritualWoundEnvelope"]!["maximumSeverityRank"]!.GetValue<int>());
            else Assert.Null(originalArt["spiritualWoundEnvelope"]);
        }
        else Assert.Null(source["specialArtJsonBase64"]);
        var identity = WoundIdentityState.Parse(await context.FileSystem.ReadFileAsync(WoundIdentityState.StatePath), WoundIdentityState.StatePath);
        Assert.True(identity.IsValid, string.Join("\n", identity.Issues));
        Assert.Equal(woundId, Assert.Single(identity.State!.Entries).WoundId);
        var history = WoundHistoryState.Parse(await context.FileSystem.ReadFileAsync(WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
        Assert.True(history.IsValid, string.Join("\n", history.Issues));
        var transition = Assert.Single(history.State!.Transitions);
        Assert.Equal("create", transition.Kind);
        Assert.Equal(decisions[1]!["transitionId"]!.GetValue<string>(), transition.TransitionId);
        Assert.Equal(woundId, transition.WoundId);
        Assert.Equal(42, transition.Turn);
        var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
        var carriers = WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(null, null, null, null, profiles));
        Assert.Empty(carriers.Issues);
        Assert.True(carriers.TryResolveOne(woundId, out var occurrence));
        Assert.Equal(2, occurrence.Wound.Severity.Rank);
        Assert.Equal("create", occurrence.Wound.LastTransition.Kind);
        var roots = occurrence.Wound.Consequences.OwnedEffectSources.RootBindings;
        Assert.Equal(2, roots.Count);
        Assert.Equal(2, occurrence.Wound.Consequences.SlotBudget);
        Assert.Equal(2, occurrence.Wound.Consequences.SlotsUsed);
        Assert.Equal(2, occurrence.Wound.Consequences.Entries.Count);
        Assert.All(occurrence.Wound.Consequences.Entries,
            entry => Assert.Equal("spiritual_action_cost_burden", entry.ProfileKey));
        var woundDefinitions = occurrence.Wound.Consequences.OwnedEffectSources.Definitions;
        Assert.Equal(2, woundDefinitions.Count);
        Assert.Equal(new[] { "guard", "maneuver" }, woundDefinitions.Select(definition =>
            definition.GetProperty("components")[0].GetProperty("payload").GetProperty("operation").GetString())
            .Order(StringComparer.Ordinal));
        Assert.All(woundDefinitions, definition =>
        {
            var component = Assert.Single(definition.GetProperty("components").EnumerateArray());
            Assert.Equal(1, component.GetProperty("payload").GetProperty("magnitude").GetInt32());
        });
        Assert.Equal(roots.Select(root => root.DefinitionKey).Order(StringComparer.Ordinal),
            woundDefinitions.Select(definition => definition.GetProperty("definitionKey").GetString()).Order(StringComparer.Ordinal));
        var effectCarriers = EffectCarrierCatalog.Build(new EffectCarrierCatalogInput(null, null, null, null, profiles, null));
        Assert.Empty(effectCarriers.Issues);
        Assert.Equal(2, effectCarriers.Occurrences.Count);
        foreach (var root in roots)
        {
            Assert.True(effectCarriers.TryResolveOne(root.EffectId, out var effect));
            Assert.Equal("guardian_frame", effect.Coordinate.OwnerId);
            Assert.Equal(woundId, effect.Effect["source"]!["sourceId"]!.GetValue<string>());
            Assert.Equal(root.DefinitionKey, effect.Effect["source"]!["definitionKey"]!.GetValue<string>());
        }
        var effects = EffectIdentityState.Parse(JsonSerializer.SerializeToElement(
            await context.ReadJsonAsync(EffectIdentityState.StatePath)), EffectIdentityState.StatePath);
        Assert.Empty(effects.Issues);
        Assert.Equal(roots.Select(root => root.EffectId).Order(StringComparer.Ordinal),
            effects.State!.Entries.Where(entry => entry.State == "active").Select(entry => entry.EffectId).Order(StringComparer.Ordinal));
        Assert.Equal(2, effects.State.Entries.Count);
        Assert.All(effects.State.Entries, entry =>
        {
            Assert.Equal(woundId, entry.Source["sourceId"]!.GetValue<string>());
            Assert.Equal(42, entry.CreatedAtTurn);
            Assert.Equal("create", Assert.Single(entry.Transitions).Kind);
        });
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath), allowMissingPristine: false);
        Assert.True(definitions.IsValid, string.Join("\n", definitions.Issues));
        var resources = ResourceStateContract.ParseCanonical(await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath),
            definitions.Catalog!, allowMissingPristine: false);
        Assert.True(resources.IsValid, string.Join("\n", resources.Issues));
        var balances = resources.Ledger!.Entries.Where(entry => entry.Coordinate.ResourceKey == "spiritual_action_points").ToArray();
        Assert.Equal(2, balances.Length);
        var oppositionOwnerId = original["activeConflict"]![AfterlifeEntityProfileState.ResourceOwnerBindingsProperty]!
            ["opposition"]!["resourceOwnerId"]!.GetValue<string>();
        Assert.Equal(new[] { oppositionOwnerId, "player_soul" }.Order(StringComparer.Ordinal),
            balances.Select(entry => entry.Coordinate.ResourceOwnerId).Order(StringComparer.Ordinal));
        foreach (var entry in balances)
            Assert.Equal(special && entry.Coordinate.ResourceOwnerId == "player_soul" ? 3m : 4m, entry.Current);
        var resourceHistory = ResourceHistoryState.ParseCanonical(await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.HistoryPath),
            definitions.Catalog!, allowMissingPristine: false);
        Assert.True(resourceHistory.IsValid, string.Join("\n", resourceHistory.Issues));
        var spends = resourceHistory.History!.Transitions.Where(entry => entry.Turn == 42 &&
            entry.Operation == ResourceTransitionOperation.Spend && entry.Coordinate.ResourceKey == "spiritual_action_points").ToArray();
        Assert.Equal(4, spends.Length);
        foreach (var balance in balances)
        {
            var ordered = spends.Where(entry => entry.Coordinate == balance.Coordinate).ToArray();
            Assert.Equal(original["activeConflict"]!["exchangeLog"]!.AsArray().Select(exchange => exchange!["exchangeId"]!.GetValue<string>()),
                ordered.Select(entry => entry.OriginId));
            Assert.Equal(new[] { 6m, 5m }, ordered.Select(entry => entry.BeforeState!.Current));
            var secondCost = special && balance.Coordinate.ResourceOwnerId == "player_soul" ? 2m : 1m;
            Assert.Equal(new[] { 5m, 5m - secondCost }, ordered.Select(entry => entry.AfterState!.Current));
            Assert.Equal(new[] { 1m, secondCost }, ordered.Select(entry => entry.AppliedAmount));
        }
        var conflict = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        Assert.True(JsonNode.DeepEquals(original["activeConflict"]!["exchangeLog"], conflict["activeConflict"]!["exchangeLog"]));
        var response = GetPrivateField<GameResponse>(engine, "_lastResponse");
        Assert.Contains(firstNarration, response.Response, StringComparison.Ordinal);
        Assert.Contains(laterNarration, response.Response, StringComparison.Ordinal);
        var notifications = Assert.IsType<string[]>(response.WoundNotifications);
        var notification = Assert.Single(notifications);
        Assert.Contains("Получена духовная рана", notification, StringComparison.Ordinal);
        Assert.Contains("(II)", notification, StringComparison.Ordinal);
        Assert.Contains("/раны", notification, StringComparison.Ordinal);
        Assert.DoesNotContain("ухудшилась", notification, StringComparison.Ordinal);
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

    /// <summary>
    /// Authors complete two-exchange reasoning without describing a declined first source as an existing wound.
    /// </summary>
    /// <param name="context">
    /// Signed full engine fixture whose raw exchanges are already authored.
    /// </param>
    /// <param name="request">
    /// Original request identity and progression control for the terminal outputs.
    /// </param>
    /// <param name="special">
    /// Whether the player uses the original tier-two special art in the second exchange.
    /// </param>
    /// <param name="capped">
    /// Whether that original art has a rank-II source ceiling.
    /// </param>
    /// <returns>
    /// A task completing after ordinary outputs and accurate actor reasoning are ready for the actual caller.
    /// </returns>
    private static async Task WriteSpiritualSourceCeilingOutputsAsync(ResourceMaterializationTestContext context,
        TurnRequest request, bool special, bool capped)
    {
        await WriteSpiritualWorseningLifecycleOutputsAsync(context, request);
        var debug = Assert.IsType<JsonObject>(await context.ReadJsonAsync("output/debug_logs.json"));
        var reasoning = debug["gm_thoughts_markdown"]!.GetValue<string>();
        reasoning = reasoning.Replace("особые искусства отсутствуют.", special
            ? "у игрока исходная Нить надлома ранга 2 с множителем цены 200%; " +
              (capped ? "исходное ограничение раны — II, гарантии нет." : "ограничения раны и гарантии нет.")
            : "особые искусства отсутствуют.", StringComparison.Ordinal);
        reasoning = reasoning.Replace("запас каждого участника меняется 6→5→4.", special
            ? "запас игрока меняется 6→5→3 (обычное pressure стоит 1, особое — 2), запас хранителя — 6→5→4."
            : "запас каждого участника меняется 6→5→4.", StringComparison.Ordinal);
        reasoning = reasoning.Replace("второй источник может ухудшить ту же рану.",
            "первый источник отклонён без раны, второй может создать новую рану ранга II с последствиями для guard и maneuver.",
            StringComparison.Ordinal);
        debug["gm_thoughts_markdown"] = reasoning;
        await context.WriteExactJsonAsync("output/debug_logs.json", debug.ToJsonString());
    }
}
