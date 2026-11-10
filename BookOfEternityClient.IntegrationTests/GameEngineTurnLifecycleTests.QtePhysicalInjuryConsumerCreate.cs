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
    public async Task QtePhysicalInjuryConsumer_ActualNextOrdinaryCreatePublishesCompleteWound()
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
        const string localWoundRef = "wound_local_stone_cut";
        const string acquisition = "Край камня оставляет на боку неглубокий кровоточащий порез.";
        const string nextNarrative = acquisition + " Герой замечает рану при осмотре ушиба.";
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
        WritePhysicalInjuryDiagnostic("qte_create_actual_completed_ordinary", new
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
            WoundBefore = unchangedWounds,
            PlayerWoundsAfter = Text(await _fs.ReadFileBytesAsync(WoundCarrierCatalog.PlayerPath)),
            WoundIdentityAfter = Text(await _fs.ReadFileBytesAsync(WoundIdentityState.StatePath)),
            WoundHistoryAfter = Text(await _fs.ReadFileBytesAsync(WoundHistoryState.HistoryPath)),
            EffectsAfter = Text(await _fs.ReadFileBytesAsync(EffectCarrierCatalog.PlayerPath)),
            EffectIdentityAfter = Text(await _fs.ReadFileBytesAsync(EffectAcceptedTurnPlan.IdentityIndexPath))
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
        // CREATE may legally add effects/resource transitions; only signed pre-turn health/history
        // are exact prerequisites. The first causal predicate follows complete ordinary acceptance.
        var playerAfter = await _fs.ReadFileBytesAsync(WoundCarrierCatalog.PlayerPath);
        Assert.True(playerAfter is not null, "Completed actual N43 CREATE turn did not publish one complete canonical player wound.");
        var player = StrictJsonAuthority.Deserialize<JsonObject>(Text(playerAfter)!, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "actual created player wound carrier")!;
        var wound = Assert.IsType<JsonObject>(Assert.Single(player["activeWounds"]!.AsArray()));
        var parsedWound = WoundMaterializationContract.Parse(wound.ToJsonString(), WoundCarrierCatalog.PlayerPath + ".activeWounds[0]");
        Assert.True(parsedWound.IsValid, JsonSerializer.Serialize(parsedWound.Issues));
        var canonical = parsedWound.Wound!;
        Assert.StartsWith("wound_", canonical.WoundId, StringComparison.Ordinal);
        Assert.NotEqual(localWoundRef, canonical.WoundId);
        Assert.Equal("active", canonical.Lifecycle);
        Assert.Equal("physical", canonical.Classification.Domain);
        Assert.Equal("other", canonical.Classification.LocationProfile.Kind);
        Assert.Equal("I", canonical.Severity.Value);
        Assert.Equal("II", canonical.Severity.MaximumAtCreation);
        Assert.Equal(occurrence.OccurrenceId, canonical.Origin.OpportunityId);
        Assert.Equal(next!.TurnNumber, canonical.Origin.CreatedAtTurn);
        Assert.Equal(occurrence.SafeContext.Cause, canonical.Origin.ReadableCause);
        Assert.Equal("mortal_world", canonical.Owner.Realm);
        Assert.Equal("player", canonical.Owner.OwnerKind);
        Assert.Equal("player_current", canonical.Owner.OwnerId);
        Assert.Equal(WoundCarrierCatalog.PlayerPath, canonical.Owner.CarrierPath);
        var identities = WoundIdentityState.Parse(await _fs.ReadFileAsync(WoundIdentityState.StatePath), WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(await _fs.ReadFileAsync(WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
        Assert.True(identities.IsValid, JsonSerializer.Serialize(identities.Issues));
        Assert.True(history.IsValid, JsonSerializer.Serialize(history.Issues));
        var identity = Assert.Single(identities.State!.Entries);
        Assert.Equal(canonical.WoundId, identity.WoundId);
        Assert.Equal("active", identity.Status);
        var transition = Assert.Single(history.State!.Transitions);
        Assert.Equal("create", transition.Kind);
        Assert.Equal(canonical.WoundId, transition.WoundId);
        Assert.Equal(next!.TurnNumber, transition.Turn);
        var carriers = WoundCarrierCatalog.Build(new(player,
            await ObjectAfter(WoundCarrierCatalog.NpcPath), await ObjectAfter(WoundCarrierCatalog.EnemiesPath),
            await ObjectAfter(WoundCarrierCatalog.AlliesPath), await ObjectAfter(WoundCarrierCatalog.AfterlifeProfilesPath)));
        Assert.Empty(carriers.Issues);
        Assert.Empty(history.State.ValidateAgreement(identities.State, carriers));
        var recovery = Assert.IsType<JsonObject>(wound["recovery"]);
        var anchor = Assert.IsType<JsonObject>(recovery["recoveryAnchor"]);
        Assert.Equal("creation", anchor["anchorKind"]!.GetValue<string>());
        Assert.Equal(transition.TransitionId, anchor["anchorTransitionId"]!.GetValue<string>());
        var deterioration = Assert.IsType<JsonObject>(recovery["deteriorationAnchor"]);
        Assert.Equal("not_stabilized", deterioration["conditionKey"]!.GetValue<string>());
        Assert.Equal(transition.TransitionId, deterioration["anchorTransitionId"]!.GetValue<string>());
        Assert.Equal(anchor["anchorMinute"]!.GetValue<long>(), deterioration["anchorMinute"]!.GetValue<long>());
        var ownedEffects = Assert.IsType<JsonObject>(wound["consequences"]!["ownedEffectSources"]);
        var rootBinding = Assert.IsType<JsonObject>(Assert.Single(ownedEffects["rootBindings"]!.AsArray()));
        var effectId = rootBinding["effectId"]!.GetValue<string>();
        var effects = (await ObjectAfter(EffectCarrierCatalog.PlayerPath))!;
        var effect = Assert.Single(effects["activeEffects"]!.AsArray().OfType<JsonObject>(), row => row["effectId"]?.GetValue<string>() == effectId);
        Assert.Equal("wound", effect["source"]!["kind"]!.GetValue<string>());
        Assert.Equal(canonical.WoundId, effect["source"]!["sourceId"]!.GetValue<string>());
        Assert.Equal(1, canonical.Consequences.SlotBudget);
        Assert.Equal(1, canonical.Consequences.SlotsUsed);
        Assert.Equal(effectId, Assert.Single(canonical.Consequences.Entries).EffectId);
        Assert.Single(canonical.Consequences.OwnedEffectSources.Definitions);
        var effectIndexJson = (await _fs.ReadFileAsync(EffectAcceptedTurnPlan.IdentityIndexPath))!;
        using var effectIndexDocument = JsonDocument.Parse(effectIndexJson);
        var effectIndex = EffectIdentityState.Parse(effectIndexDocument.RootElement, EffectAcceptedTurnPlan.IdentityIndexPath);
        Assert.Empty(effectIndex.Issues);
        Assert.NotNull(effectIndex.State);
        Assert.True(effectIndex.State!.TryGetEntry(effectId, out _));
        var effectIndexRoot = StrictJsonAuthority.Deserialize<JsonObject>(effectIndexJson, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "actual created effect identity")!;
        var effectEntry = Assert.Single(effectIndexRoot["entries"]!.AsArray().OfType<JsonObject>(), row => row["effectId"]?.GetValue<string>() == effectId);
        Assert.Equal("wound", effectEntry["source"]!["kind"]!.GetValue<string>());
        Assert.Equal(canonical.WoundId, effectEntry["source"]!["sourceId"]!.GetValue<string>());
        Assert.Equal(rootBinding["definitionKey"]!.GetValue<string>(), effectEntry["source"]!["definitionKey"]!.GetValue<string>());
        Assert.True(receiptAfter is not null);
        var receipts = MortalWoundOpportunityReceiptState.Parse(Text(receiptAfter), MortalWoundOpportunityReceiptState.StatePath);
        Assert.True(receipts.IsValid, JsonSerializer.Serialize(receipts.Issues));
        var receipt = Assert.Single(receipts.State!.Receipts);
        Assert.Equal(occurrence.OccurrenceId, receipt.OpportunityId);
        Assert.Equal(occurrence.OccurrenceFingerprint, receipt.OccurrenceFingerprint);
        Assert.Equal(source.Source.SessionId, receipt.SourceSessionId);
        Assert.Equal(source.Source.RequestId, receipt.SourceRequestId);
        Assert.Equal(source.SnapshotToken, receipt.SourceSnapshotToken);
        Assert.Equal(PhysicalInjurySourceTurn, receipt.SourceTurn);
        Assert.Equal(next.SessionId, receipt.SessionId);
        Assert.Equal(next.RequestId, receipt.RequestId);
        Assert.Equal(next.SnapshotToken, receipt.SnapshotToken);
        Assert.Equal(next.TurnNumber, receipt.Turn);
        Assert.Equal("materialize", receipt.Decision);
        Assert.Equal(canonical.WoundId, receipt.WoundId);
        Assert.Equal(transition.TransitionId, receipt.TransitionId);
        var remaining = MortalWoundOccurrenceState.Parse(Text(pendingAfter), MortalWoundOccurrenceState.StatePath);
        Assert.True(remaining.IsValid, JsonSerializer.Serialize(remaining.Issues));
        Assert.Empty(remaining.State!.Occurrences);
        Assert.Empty(MortalWoundOpportunityReceiptState.ValidateConsumedOccurrenceAgreement(remaining.State, receipts.State));
        Assert.Empty(MortalWoundOpportunityReceiptState.ValidateHistoryAgreement(receipts.State, history.State));
        Assert.Null(decisionAfter);
        Assert.Null(commandAfter);

        async Task<JsonObject?> ObjectAfter(string path)
        {
            var json = await _fs.ReadFileAsync(path);
            return json is null ? null : StrictJsonAuthority.Deserialize<JsonObject>(json, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "actual post-create carrier");
        }

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
            WritePhysicalInjuryDiagnostic("qte_create_actual_request_reader", new
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
            var draft = new JsonObject
            {
                ["opportunityRef"] = offer.GetProperty("opportunityRef").GetString(), ["decision"] = "materialize",
                ["woundRef"] = localWoundRef, ["proposal"] = CreateProposal()
            };
            await _fs.WriteFileAtomicAsync(lease, decisionPath, draft.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            var timestamp = DateTime.UtcNow.ToString("O");
            await _fs.WriteFileAtomicAsync(lease, "output/narrative_response.json", JsonSerializer.Serialize(new { response = nextNarrative, timestamp }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            await _fs.WriteFileAtomicAsync(lease, "output/interface_updates.json", JsonSerializer.Serialize(new { dialogueOptions = Array.Empty<object>(), timestamp }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            await _fs.WriteFileAtomicAsync(lease, "output/debug_logs.json", JsonSerializer.Serialize(new
            {
                timestamp, gm_thoughts_markdown = "## NPC Scope\n- Mode: Scene-local\n- Relevant actors: нет\n- Why relevant: Осмотр ушиба без участия NPC.\n- Actors outside scope: нет\n- Why outside scope: В сцене нет других акторов.\n\n## Reasoning\n- The exact offered opportunity is materialized at severity I below maximum II. Acquisition narration is verbatim in ordinary prose. Only the public proposal is authored; no canonical IDs, authority or state writes."
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
            Assert.Equal(new[] { "decision", "opportunityRef", "proposal", "woundRef" }, actualDecision.RootElement.EnumerateObject().Select(field => field.Name).Order(StringComparer.Ordinal));
            Assert.Equal(occurrence.OpportunityRef, actualDecision.RootElement.GetProperty("opportunityRef").GetString());
            Assert.Equal("materialize", actualDecision.RootElement.GetProperty("decision").GetString());
            Assert.Equal(localWoundRef, actualDecision.RootElement.GetProperty("woundRef").GetString());
            Assert.Equal("I", actualDecision.RootElement.GetProperty("proposal").GetProperty("severity").GetString());
            Assert.Contains(actualDecision.RootElement.GetProperty("proposal").GetProperty("display").GetProperty("acquisitionNarration").GetString()!, nextNarrative, StringComparison.Ordinal);
            using var actualReady = JsonDocument.Parse(Text(readyAtHook)!);
            Assert.Equal(next.SessionId, actualReady.RootElement.GetProperty("sessionId").GetString());
            Assert.Equal(next.RequestId, actualReady.RootElement.GetProperty("requestId").GetString());
            Assert.Equal(next.TurnNumber, actualReady.RootElement.GetProperty("turnNumber").GetInt32());
            Assert.Equal("success", actualReady.RootElement.GetProperty("status").GetString());
            gmAuthored = true;
            WritePhysicalInjuryDiagnostic("qte_create_actual_offline_gm", new
            { next.SessionId, next.RequestId, next.SnapshotToken, next.TurnNumber, next.AbsentLogicalPaths, Request = Text(requestAtHook), Decision = Text(decisionAtHook), Ready = Text(readyAtHook) });
        }

        JsonObject CreateProposal()
        {
            // Public GM JSON only, copied from the existing creation grammar. No fixture authority.
            var definition = EffectMaterializationTestFixture.CreateDefinition("periodic_damage");
            definition["definitionKey"] = "wound_bleeding_root";
            definition["allowedRealms"] = new JsonArray("mortal_world");
            definition["allowedTargetKinds"] = new JsonArray("player");
            definition["parameterBounds"] = new JsonObject();
            definition["links"] = new JsonArray();
            definition["stacking"]!["stackKey"] = "stack_wound_bleeding_root";
            definition["stacking"]!["policy"] = "independent";
            definition["stacking"]!["maxStacks"] = 1;
            definition["stacking"]!["atMaximum"] = "no_change";
            definition["stacking"]!["refreshMode"] = null;
            definition["stacking"]!["mergeRule"] = null;
            return new JsonObject
            {
                ["classification"] = new JsonObject
                {
                    ["woundType"] = "superficial_laceration",
                    ["locationProfile"] = new JsonObject { ["kind"] = "other", ["readableLocus"] = "поверхностный порез на боку" }
                },
                ["display"] = new JsonObject
                {
                    ["name"] = "Поверхностный порез", ["description"] = "Край камня оставил неглубокий порез.",
                    ["visibleSymptoms"] = new JsonArray("полоска крови", "жжение"),
                    ["prognosis"] = "Порез требует очистки и стабилизации.", ["visibility"] = "known_to_player",
                    ["acquisitionNarration"] = acquisition
                },
                ["severity"] = "I", ["complications"] = new JsonArray(),
                ["consequenceDefinitions"] = new JsonArray(new JsonObject
                {
                    ["definitionRef"] = "bleeding_root", ["definition"] = definition,
                    ["root"] = new JsonObject
                    {
                        ["ownership"] = new JsonObject { ["kind"] = "base_wound", ["complicationRef"] = null },
                        ["slots"] = new JsonArray(new JsonObject
                        { ["profileKey"] = "periodic_damage", ["readableSummary"] = "Свежий порез продолжает кровоточить." })
                    }
                }),
                ["treatment"] = new JsonObject
                {
                    ["diagnosisPaths"] = new JsonArray(),
                    ["routes"] = new JsonArray(new JsonObject
                    {
                        ["routeId"] = "clean_cut_v1", ["displayName"] = "Очистить и прижать порез",
                        ["visibility"] = "known_to_player", ["mode"] = "procedure",
                        ["requirements"] = new JsonArray(),
                        ["resourcePolicy"] = new JsonObject
                        {
                            ["reserveBeforeResolution"] = true, ["consumeOn"] = new JsonArray("success", "failed_attempt"),
                            ["refundOn"] = new JsonArray("cancelled", "validation_failed", "rolled_back"), ["mutations"] = new JsonArray()
                        },
                        ["resolution"] = new JsonObject
                        {
                            ["formulaKey"] = "mortal_wound_procedure_v1", ["difficulty"] = 10,
                            ["rollSource"] = "accepted_d20", ["criticalPolicy"] = "natural_20_first_natural_1_last",
                            ["modifierSource"] = new JsonObject { ["kind"] = "fixed_zero" }
                        },
                        ["outcomes"] = new JsonArray(
                            new JsonObject { ["bandId"] = "stabilized", ["minimumMargin"] = 0, ["maximumMargin"] = null,
                                ["category"] = "success", ["result"] = new JsonArray(new JsonObject { ["kind"] = "stabilize" }) },
                            new JsonObject { ["bandId"] = "unimproved", ["minimumMargin"] = null, ["maximumMargin"] = -1,
                                ["category"] = "failed_attempt", ["result"] = new JsonArray(new JsonObject { ["kind"] = "no_improvement" }) }),
                        ["interruption"] = null
                    }),
                    ["knownRouteIds"] = new JsonArray("clean_cut_v1"), ["completedRouteIds"] = new JsonArray()
                },
                ["recovery"] = new JsonObject
                {
                    ["mode"] = "requires_stabilization", ["clockKind"] = "mortal_world_time", ["cadence"] = 86400,
                    ["currentStepProgress"] = 0, ["currentStepThreshold"] = 3, ["lastTickKey"] = null,
                    ["blockers"] = new JsonArray("not_stabilized"), ["carryOverflow"] = true,
                    ["deteriorationPolicy"] = new JsonObject
                    {
                        ["policyRef"] = "untreated_cut", ["unmetConditions"] = new JsonArray("not_stabilized"),
                        ["graceMinutes"] = 30L, ["cadenceMinutes"] = 10L,
                        ["result"] = new JsonObject { ["kind"] = "increase_severity" }
                    }
                }
            };
        }

        static string? Text(byte[]? bytes) => bytes is null ? null : Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
    }
}
