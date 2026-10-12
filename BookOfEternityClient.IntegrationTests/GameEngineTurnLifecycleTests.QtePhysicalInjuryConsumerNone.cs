using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task QtePhysicalInjuryConsumer_ActualNextOrdinaryNoneConsumesOnceWithoutWound()
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var source = await RunActualQteStorySourceAsync(physicalDeclaration: true, physicalMaximum: 2);
        Assert.Null(source.Failure);
        Assert.True(source.ActionObserved);
        await AssertActualQteStoryFinalizedAsync(_fs, source.Source!, source.StoryAtHandoff!, 155m);
        var occurrence = await AssertActualPhysicalOccurrenceAsync(source, 2);
        Assert.Equal(PhysicalInjurySourceTurn, GetPrivateField<GameLoop>(source.Engine!, "_gameLoop").TurnNumber);
        var originals = new[] { "input/turn_request.json", "game_state/control/pending_turn_snapshot.json",
            PendingTurnSnapshotAuthority.AuthorityPath, QteSceneService.QteOfferPath, "ready/turn_complete.json", "ready/turn_error.json" };
        foreach (var path in originals) Assert.False(_fs.FileExists(path), "Original QTE source cleanup incomplete: " + path);
        const string decisionPath = "output/mortal_wound_decisions.json";
        const string nextAction = "Осмотреть ушиб после обвала.";
        const string nextNarrative = "Герой осматривает ушиб и продолжает путь без новой раны.";
        Assert.False(_fs.FileExists(decisionPath));
        Assert.False(_fs.FileExists(AcceptedMechanicsPlan.WoundCommandPath));
        var pendingBefore = (await _fs.ReadFileBytesAsync(MortalWoundOccurrenceState.StatePath))!;
        var receiptBefore = await _fs.ReadFileBytesAsync(MortalWoundOpportunityReceiptState.StatePath);
        var resourceBefore = (await _fs.ReadFileBytesAsync(ResourceMaterializationContract.StatePath))!;
        var resourceHistoryBefore = (await _fs.ReadFileBytesAsync(ResourceMaterializationContract.HistoryPath))!;
        var unchangedWounds = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in new[] { WoundIdentityState.StatePath, WoundHistoryState.HistoryPath,
                     WoundCarrierCatalog.PlayerPath, WoundCarrierCatalog.NpcPath, WoundCarrierCatalog.EnemiesPath,
                     WoundCarrierCatalog.AlliesPath, WoundCarrierCatalog.AfterlifeProfilesPath })
            unchangedWounds.Add(path, await _fs.ReadFileBytesAsync(path));
        PendingTurnSnapshotReadAuthority? next = null;
        byte[]? requestAtHook = null, decisionAtHook = null, readyAtHook = null;
        var hookReached = false;
        var gmAuthored = false;
        var ordinaryValidated = false;
        using var live = new AgentConsoleLiveInputSource(new AgentConsoleStateStore(), TimeSpan.FromSeconds(10));
        var engine = CreateGameEngine(live, settings =>
        { InertRealmSettings(settings); settings.AutosaveIntervalTurns = 0; },
            finalizationHooks: new GameEngineSessionFinalizationHooks { AtCheckpointAsync = AuthorNextOrdinary });
        GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(source.Source!.SessionId, PhysicalInjurySourceTurn);
        await GetPrivateField<StateManager>(engine, "_stateManager").RefreshGameStateAsync();
        var failure = await Record.ExceptionAsync(() => InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", nextAction, null));
        var receiptAfter = await _fs.ReadFileBytesAsync(MortalWoundOpportunityReceiptState.StatePath);
        var pendingAfter = await _fs.ReadFileBytesAsync(MortalWoundOccurrenceState.StatePath);
        var decisionAfter = await _fs.ReadFileBytesAsync(decisionPath);
        var commandAfter = await _fs.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath);
        var storyAfter = await _fs.ReadFileBytesAsync(QteStoryPath);
        WritePhysicalInjuryDiagnostic("qte_none_actual_completed_ordinary", new
        {
            Source = new { source.Source.SessionId, source.Source.RequestId, source.SnapshotToken, source.Source.TurnNumber },
            Next = next is null ? null : new { next.SessionId, next.RequestId, next.SnapshotToken, next.TurnNumber, next.Realm },
            hookReached, gmAuthored, ordinaryValidated, EngineFailure = failure?.ToString(),
            ActualTurn = GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber,
            Request = Text(requestAtHook), Decision = Text(decisionAtHook), Ready = Text(readyAtHook),
            PendingBefore = Text(pendingBefore), PendingAfter = Text(pendingAfter),
            ReceiptBefore = Text(receiptBefore), ReceiptAfter = Text(receiptAfter),
            ResourceBefore = Text(resourceBefore), ResourceAfter = Text(await _fs.ReadFileBytesAsync(ResourceMaterializationContract.StatePath)),
            ResourceHistoryBefore = Text(resourceHistoryBefore), ResourceHistoryAfter = Text(await _fs.ReadFileBytesAsync(ResourceMaterializationContract.HistoryPath)),
            DecisionAfter = Text(decisionAfter), WoundCommandAfter = Text(commandAfter), StoryAfter = Text(storyAfter),
            WoundBefore = unchangedWounds
        });

        // Ordinary admission and completed client tail are prerequisites, not the receipt oracle.
        Assert.True(hookReached && gmAuthored, "Actual N43 request/GM authoring was not reached; prerequisite blocker.");
        Assert.Null(failure);
        Assert.NotNull(next);
        Assert.True(ordinaryValidated, "The actual ordinary outcome was not accepted; prerequisite blocker.");
        Assert.Equal(PhysicalInjurySourceTurn + 1, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
        foreach (var path in originals) Assert.False(_fs.FileExists(path), "Accepted N43 cleanup incomplete: " + path);
        Assert.NotNull(storyAfter);
        var stories = Text(storyAfter)!.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(row => StrictJsonAuthority.Deserialize<StoryEntry>(row, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "actual ordinary story")!).ToArray();
        Assert.Equal(2, stories.Length);
        var accepted = Assert.Single(stories, row => row.Turn == next!.TurnNumber);
        Assert.Equal(nextAction, accepted.Player);
        Assert.Equal(nextNarrative, accepted.Narrative);
        // Ordinary normalization may republish canonical representation. Require exact typed
        // health/state and the existing immutable history, rather than incidental text formatting.
        var definitions = ResourceDefinitionCatalog.ParseCanonical(await _fs.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath), false);
        Assert.True(definitions.IsValid, JsonSerializer.Serialize(definitions.Issues));
        var originalResources = ResourceStateContract.ParseCanonical(Text(resourceBefore), definitions.Catalog!, false);
        var currentResources = ResourceStateContract.ParseCanonical(await _fs.ReadFileAsync(ResourceMaterializationContract.StatePath), definitions.Catalog!, false);
        var originalHistory = ResourceHistoryState.ParseCanonical(Text(resourceHistoryBefore), definitions.Catalog!, false);
        var currentHistory = ResourceHistoryState.ParseCanonical(await _fs.ReadFileAsync(ResourceMaterializationContract.HistoryPath), definitions.Catalog!, false);
        Assert.True(originalResources.IsValid && currentResources.IsValid && originalHistory.IsValid && currentHistory.IsValid);
        Assert.Equal(originalResources.Ledger!.Entries, currentResources.Ledger!.Entries);
        Assert.Equal(originalHistory.History!.Transitions, currentHistory.History!.Transitions);
        Assert.Equal(155m, Assert.Single(currentResources.Ledger.Entries, row => row.Coordinate.ResourceKey == "health").Current);
        Assert.Empty(currentHistory.History.ValidateStateAgreement(currentResources.Ledger));

        // First causal predicate: durable NONE receipt only after full actual ordinary completion.
        Assert.True(receiptAfter is not null, "Completed actual N43 NONE turn did not publish the durable opportunity receipt.");
        var parsed = MortalWoundOpportunityReceiptState.Parse(Text(receiptAfter), MortalWoundOpportunityReceiptState.StatePath);
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
        var receipt = Assert.Single(parsed.State!.Receipts);
        Assert.Equal(occurrence.OccurrenceId, receipt.OpportunityId);
        Assert.Equal(occurrence.OccurrenceFingerprint, receipt.OccurrenceFingerprint);
        Assert.Equal(source.Source.SessionId, receipt.SourceSessionId);
        Assert.Equal(source.Source.RequestId, receipt.SourceRequestId);
        Assert.Equal(source.SnapshotToken, receipt.SourceSnapshotToken);
        Assert.Equal(PhysicalInjurySourceTurn, receipt.SourceTurn);
        Assert.Equal(next!.SessionId, receipt.SessionId);
        Assert.Equal(next.RequestId, receipt.RequestId);
        Assert.Equal(next.SnapshotToken, receipt.SnapshotToken);
        Assert.Equal(next.TurnNumber, receipt.Turn);
        Assert.Equal("none", receipt.Decision);
        Assert.Null(receipt.WoundId);
        Assert.Null(receipt.TransitionId);
        var remaining = MortalWoundOccurrenceState.Parse(Text(pendingAfter), MortalWoundOccurrenceState.StatePath);
        Assert.True(remaining.IsValid, JsonSerializer.Serialize(remaining.Issues));
        Assert.Empty(remaining.State!.Occurrences);
        Assert.Empty(MortalWoundOpportunityReceiptState.ValidateConsumedOccurrenceAgreement(remaining.State, parsed.State));
        foreach (var image in unchangedWounds) Assert.Equal(image.Value, await _fs.ReadFileBytesAsync(image.Key));
        Assert.Null(decisionAfter);
        Assert.Null(commandAfter);

        async Task AuthorNextOrdinary(SessionFinalizationCheckpoint checkpoint)
        {
            if (checkpoint == SessionFinalizationCheckpoint.AcceptedOutcomeValidatedBeforeMaterialization)
            { ordinaryValidated = true; return; } // This checkpoint is explicitly before publication.
            if (checkpoint != SessionFinalizationCheckpoint.TerminalWaitStarted) return;
            hookReached = true;
            await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
            requestAtHook = await _fs.ReadFileBytesAsync(lease, "input/turn_request.json");
            Assert.NotNull(requestAtHook);
            var read = PendingTurnSnapshotReader.ReadCurrent(_fs, lease,
                PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(
                    [MortalWoundOccurrenceState.StatePath, ResourceMaterializationContract.DefinitionsPath,
                     ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
                     CanonicalResourceOwnerAuthorityComposer.AuthorityPath, "game_state/meta/soul_state.json"],
                    unchangedWounds.Keys.Append(MortalWoundOpportunityReceiptState.StatePath)));
            WritePhysicalInjuryDiagnostic("qte_none_actual_request_reader", new
            { read.Success, read.Issues, ActualRequest = Text(requestAtHook) });
            Assert.True(read.Success, JsonSerializer.Serialize(read.Issues));
            next = read.Snapshot!;
            Assert.Equal(source.Source.SessionId, next.SessionId);
            Assert.Equal(PhysicalInjurySourceTurn + 1, next.TurnNumber);
            Assert.NotEqual(source.Source.RequestId, next.RequestId);
            Assert.NotEqual(source.SnapshotToken, next.SnapshotToken);
            Assert.Equal("mortal_world", next.Realm);
            Assert.Equal(pendingBefore, next.ReadRequiredBytes(MortalWoundOccurrenceState.StatePath));
            Assert.Equal(resourceBefore, next.ReadRequiredBytes(ResourceMaterializationContract.StatePath));
            Assert.Equal(resourceHistoryBefore, next.ReadRequiredBytes(ResourceMaterializationContract.HistoryPath));
            foreach (var image in unchangedWounds.Append(new KeyValuePair<string, byte[]?>(MortalWoundOpportunityReceiptState.StatePath, receiptBefore)))
                if (image.Value is null) Assert.Contains(image.Key, next.AbsentLogicalPaths);
                else Assert.Equal(image.Value, next.ReadRequiredBytes(image.Key));
            using var request = JsonDocument.Parse(Text(requestAtHook)!);
            var raw = request.RootElement;
            Assert.Equal(next.SessionId, raw.GetProperty("sessionId").GetString());
            Assert.Equal(next.RequestId, raw.GetProperty("requestId").GetString());
            Assert.Equal(next.TurnNumber, raw.GetProperty("turnNumber").GetInt32());
            var offer = Assert.Single(raw.GetProperty("woundOpportunities").EnumerateArray());
            Assert.Equal(new[] { "domain", "maximumSeverity", "opportunityRef", "safeContext" },
                offer.EnumerateObject().Select(field => field.Name).Order(StringComparer.Ordinal));
            Assert.Equal(occurrence.OpportunityRef, offer.GetProperty("opportunityRef").GetString());
            Assert.Equal("physical", offer.GetProperty("domain").GetString());
            Assert.Equal("II", offer.GetProperty("maximumSeverity").GetString());
            var context = offer.GetProperty("safeContext");
            Assert.Equal(new[] { "allowedLocationKinds", "cause", "target" }, context.EnumerateObject().Select(field => field.Name).Order(StringComparer.Ordinal));
            Assert.Equal(occurrence.SafeContext.Target, context.GetProperty("target").GetString());
            Assert.Equal(occurrence.SafeContext.Cause, context.GetProperty("cause").GetString());
            Assert.Equal(occurrence.SafeContext.AllowedLocationKinds, context.GetProperty("allowedLocationKinds").EnumerateArray().Select(row => row.GetString()));
            var draft = new JsonObject { ["opportunityRef"] = offer.GetProperty("opportunityRef").GetString(), ["decision"] = "none" };
            await _fs.WriteFileAtomicAsync(lease, decisionPath, draft.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            var timestamp = DateTime.UtcNow.ToString("O");
            await _fs.WriteFileAtomicAsync(lease, "output/narrative_response.json", JsonSerializer.Serialize(new { response = nextNarrative, timestamp }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            await _fs.WriteFileAtomicAsync(lease, "output/interface_updates.json", JsonSerializer.Serialize(new { dialogueOptions = Array.Empty<object>(), timestamp }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            await _fs.WriteFileAtomicAsync(lease, "output/debug_logs.json", JsonSerializer.Serialize(new
            {
                timestamp, gm_thoughts_markdown = "## NPC Scope\n- Mode: Scene-local\n- Relevant actors: нет\n- Why relevant: Осмотр ушиба без участия NPC.\n- Actors outside scope: нет\n- Why outside scope: В сцене нет других акторов.\n\n## Reasoning\n- Ordinary prose only; the exact offered wound opportunity is declined with none. No mechanical state delta."
            }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            await _fs.WriteFileAtomicAsync(lease, "ready/turn_complete.json", JsonSerializer.Serialize(new
            {
                sessionId = next.SessionId, requestId = next.RequestId, turnNumber = next.TurnNumber,
                status = "success", timestamp,
                filesModified = new[] { decisionPath, "output/narrative_response.json", "output/interface_updates.json", "output/debug_logs.json" }
            }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            decisionAtHook = await _fs.ReadFileBytesAsync(lease, decisionPath);
            readyAtHook = await _fs.ReadFileBytesAsync(lease, "ready/turn_complete.json");
            using var actualDecision = JsonDocument.Parse(Text(decisionAtHook)!);
            Assert.Equal(new[] { "decision", "opportunityRef" }, actualDecision.RootElement.EnumerateObject().Select(field => field.Name).Order(StringComparer.Ordinal));
            Assert.Equal(occurrence.OpportunityRef, actualDecision.RootElement.GetProperty("opportunityRef").GetString());
            Assert.Equal("none", actualDecision.RootElement.GetProperty("decision").GetString());
            using var actualReady = JsonDocument.Parse(Text(readyAtHook)!);
            Assert.Equal(next.SessionId, actualReady.RootElement.GetProperty("sessionId").GetString());
            Assert.Equal(next.RequestId, actualReady.RootElement.GetProperty("requestId").GetString());
            Assert.Equal(next.TurnNumber, actualReady.RootElement.GetProperty("turnNumber").GetInt32());
            Assert.Equal("success", actualReady.RootElement.GetProperty("status").GetString());
            gmAuthored = true;
            WritePhysicalInjuryDiagnostic("qte_none_actual_offline_gm", new
            { next.SessionId, next.RequestId, next.SnapshotToken, next.TurnNumber, next.AbsentLogicalPaths, Request = Text(requestAtHook), Decision = Text(decisionAtHook), Ready = Text(readyAtHook) });
        }

        static string? Text(byte[]? bytes) => bytes is null ? null : Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
    }
}
