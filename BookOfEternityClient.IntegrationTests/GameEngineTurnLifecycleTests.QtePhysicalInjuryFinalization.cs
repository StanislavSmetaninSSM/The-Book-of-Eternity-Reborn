using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>Requires a real ordinary engine QTE health outcome to publish its pending wound source.</summary>
    /// <returns>A task reaching the missing-occurrence assertion only after genuine signed admission and finalized health damage.</returns>
    [Fact]
    public async Task QtePhysicalInjury_ActualOrdinaryTurnFinalizedHealthDamagePublishesOccurrence()
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        await _fs.WriteFileAtomicAsync("game_state/core/game_settings.json", "{\"qteEventsEnabled\":true}");
        await _fs.WriteFileAtomicAsync("game_state/misc/characteristics.json", JsonSerializer.Serialize(
            Characteristics.All.ToDictionary(name => name, _ => 20), SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync("game_state/player/experience.json", "{\"level\":1,\"experience\":0,\"experienceForNextLevel\":100}");

        var resources = ResourceBootstrapStateBuilder.BuildMortalPlayer(
            incarnationNumber: 2, turn: PhysicalInjurySourceTurn - 1,
            permanentStrength: 20, permanentConstitution: 20,
            permanentIntelligence: 20, permanentWisdom: 20, permanentFaith: 20);
        Assert.True(resources.IsValid, JsonSerializer.Serialize(resources.Issues));
        await _fs.WriteFileAtomicAsync(ResourceMaterializationContract.DefinitionsPath, resources.Definitions!.ToCanonicalJson());
        await _fs.WriteFileAtomicAsync(ResourceMaterializationContract.StatePath, resources.State!.ToCanonicalJson());
        await _fs.WriteFileAtomicAsync(ResourceMaterializationContract.HistoryPath, resources.History!.ToCanonicalJson());
        var bootstrapAuthority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(resources.Definitions,
            path => _fs.ReadFileAsync(path), resources.State, resources.History,
            CanonicalResourceOwnerAuthorityPurpose.FinalAfterImage);
        Assert.True(bootstrapAuthority.IsValid, JsonSerializer.Serialize(bootstrapAuthority.Issues));
        await _fs.WriteFileAtomicAsync(CanonicalResourceOwnerAuthorityComposer.AuthorityPath, bootstrapAuthority.CanonicalAuthorityJson!);
        var healthBefore = Assert.Single(resources.State.Entries, entry => entry.Coordinate ==
            new ResourceCoordinate("mortal_world", ResourceOwnerKind.Player, "player_current", "health"));
        Assert.Equal(160m, healthBefore.Current);
        var healthBytesBefore = (await _fs.ReadFileBytesAsync(ResourceMaterializationContract.StatePath))!;
        var protectedWounds = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in new[] { WoundIdentityState.StatePath, WoundHistoryState.HistoryPath,
                     MortalWoundOpportunityReceiptState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
            protectedWounds[path] = await _fs.ReadFileBytesAsync(path);
        Assert.False(_fs.FileExists(MortalWoundOccurrenceState.StatePath));

        // Drive existing real Agent Console menus; no test calls BeginAcceptedScene or manufactures a terminal result.
        var console = new AgentConsoleStateStore();
        AgentConsoleLiveInputSource? live = null;
        JsonObject? submittedOffer = null;
        var screens = new List<string>();
        var frozenObserved = false;
        using var input = live = new AgentConsoleLiveInputSource(console, TimeSpan.FromSeconds(10),
            AgentConsoleLiveInputSource.DefaultMaxQueueLength, new AgentConsoleLiveInputSourceHooks
            {
                ActiveReadRegistered = kind =>
                {
                    var screen = console.GetSnapshot();
                    Assert.Equal(AgentConsoleInputKind.Key, kind);
                    Assert.NotNull(screen);
                    Assert.StartsWith("qte-", screen.ScreenId, StringComparison.Ordinal);
                    screens.Add(screen.ScreenId);
                    if (screen.ScreenId.StartsWith("qte-action-", StringComparison.Ordinal))
                    {
                        var runtime = JsonNode.Parse(File.ReadAllText(_fs.ResolvePath(QteSceneService.QteRuntimePath)))!.AsObject();
                        var frozen = Assert.IsType<JsonObject>(runtime["activeScene"]!["offer"]);
                        AssertPhysicalInjuryBranches(submittedOffer!, frozen, declared: true);
                        Assert.Equal(healthBytesBefore, File.ReadAllBytes(_fs.ResolvePath(ResourceMaterializationContract.StatePath)));
                        frozenObserved = true;
                        WritePhysicalInjuryDiagnostic("actual_engine_frozen_before_harm", new
                        {
                            Screen = screen.ScreenId, FrozenBranches = frozen["terminalOutcomes"], HealthCurrent = healthBefore.Current
                        });
                    }
                    Assert.True(live!.EnqueueKey(Key(ConsoleKey.Enter)).Accepted);
                }
            });
        var engine = CreateGameEngine(input, settings =>
        {
            InertRealmSettings(settings);
            settings.AutosaveIntervalTurns = 0;
        });
        var qte = GetPrivateField<QteSceneService>(engine, "_qteSceneService");
        SetPrivateField(qte, "_inputSource", input);
        var gameLoop = GetPrivateField<GameLoop>(engine, "_gameLoop");
        gameLoop.SetSession("session_qte_physical_finalization", PhysicalInjurySourceTurn - 1);
        await _fs.WriteFileAtomicAsync("game_state/history/chat_log.json", new JsonObject
        {
            ["sessionId"] = gameLoop.SessionId, ["entries"] = new JsonArray()
        }.ToJsonString());
        await GetPrivateField<StateManager>(engine, "_stateManager").RefreshGameStateAsync();
        TurnRequest? sourceRequest = null;
        string? sourceSnapshotToken = null;

        var gm = Task.Run(async () =>
        {
            try
            {
                sourceRequest = await WaitForTurnRequestAsync();
                Assert.Equal(PhysicalInjurySourceTurn, sourceRequest.TurnNumber);
                await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
                {
                    var snapshot = PendingTurnSnapshotReader.ReadCurrent(_fs, lease,
                        [ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath,
                         ResourceMaterializationContract.HistoryPath, CanonicalResourceOwnerAuthorityComposer.AuthorityPath]);
                    Assert.True(snapshot.Success, JsonSerializer.Serialize(snapshot.Issues));
                    Assert.Equal(sourceRequest.SessionId, snapshot.Snapshot!.SessionId);
                    Assert.Equal(sourceRequest.RequestId, snapshot.Snapshot.RequestId);
                    Assert.Equal(sourceRequest.TurnNumber, snapshot.Snapshot.TurnNumber);
                    sourceSnapshotToken = snapshot.Snapshot.SnapshotToken;
                    Assert.False(string.IsNullOrWhiteSpace(sourceSnapshotToken));
                    var manifest = JsonNode.Parse((await _fs.ReadFileAsync(lease, "game_state/control/pending_turn_snapshot.json"))!)!.AsObject();
                    Assert.Equal(QteSceneService.OrdinaryPlayerTurnSourceLabel, manifest["sourceLabel"]!.GetValue<string>());
                    WritePhysicalInjuryDiagnostic("actual_engine_source_snapshot", new
                    {
                        snapshot.Snapshot.SessionId, snapshot.Snapshot.RequestId, snapshot.Snapshot.SnapshotToken,
                        snapshot.Snapshot.TurnNumber, SourceLabel = manifest["sourceLabel"], Health = healthBefore.Coordinate,
                        healthBefore.Current
                    });
                }

                submittedOffer = BuildPhysicalInjuryOffer(declared: true, maximum: 2);
                await _fs.WriteFileAtomicAsync(QteSceneService.QteOfferPath, submittedOffer.ToJsonString());
                var timestamp = DateTime.UtcNow.ToString("o");
                await WriteJsonAsync("output/narrative_response.json", new { response = "Камни падают. Укрыться от обвала?", timestamp });
                await WriteJsonAsync("output/interface_updates.json", new { dialogueOptions = Array.Empty<object>(), timestamp });
                await WriteJsonAsync("output/debug_logs.json", new
                {
                    timestamp,
                    gm_thoughts_markdown = "## NPC Scope\n- Mode: Scene-local\n- Relevant actors: нет\n- Why relevant: Обвал без участия NPC.\n- Actors outside scope: нет\n- Why outside scope: В сцене нет других акторов.\n\n## Reasoning\n- Only the QTE offer is authored; no ordinary state changes."
                });
                var admission = await GetPrivateField<ValidationService>(engine, "_validator").ValidateAcceptedTurnQteOfferAsync();
                WritePhysicalInjuryDiagnostic("actual_engine_raw_offer_admission", new { Issues = admission });
                Assert.DoesNotContain(admission, issue => issue.Severity == IssueSeverity.Error);
                Assert.Equal(healthBytesBefore, await _fs.ReadFileBytesAsync(ResourceMaterializationContract.StatePath));
                await WriteJsonAsync("ready/turn_complete.json", new
                {
                    sessionId = sourceRequest.SessionId, requestId = sourceRequest.RequestId, turnNumber = sourceRequest.TurnNumber,
                    status = "success", timestamp = DateTime.UtcNow.ToString("o"),
                    filesModified = new[] { QteSceneService.QteOfferPath, "output/narrative_response.json", "output/interface_updates.json", "output/debug_logs.json" }
                });
            }
            catch (Exception failure)
            {
                WritePhysicalInjuryDiagnostic("actual_engine_gm_fixture_blocker", new { Failure = failure.ToString() });
                if (sourceRequest != null)
                    await WriteJsonAsync("ready/turn_error.json", new
                    {
                        sessionId = sourceRequest.SessionId, requestId = sourceRequest.RequestId,
                        turnNumber = sourceRequest.TurnNumber, status = "error", timestamp = DateTime.UtcNow.ToString("o"),
                        error = "Offline QTE fixture failed before producing its correlated completion."
                    });
                throw;
            }
        });
        var engineFailure = await Record.ExceptionAsync(() =>
            InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", "Укрыться от падающих камней.", null));
        var gmFailure = await Record.ExceptionAsync(async () => await gm);
        WritePhysicalInjuryDiagnostic("actual_engine_execution", new
        {
            EngineFailure = engineFailure?.ToString(), GmFailure = gmFailure?.ToString()
        });
        Assert.Null(gmFailure);
        Assert.Null(engineFailure);

        // These are prerequisite assertions. A failure here is a setup/finalization blocker, never missing-occurrence RED.
        Assert.NotNull(sourceRequest);
        Assert.True(frozenObserved, "The actual engine did not reach the accepted frozen QTE action.");
        Assert.Contains("qte-offer-decision", screens);
        Assert.Equal(PhysicalInjurySourceTurn, gameLoop.TurnNumber);
        var definitions = ResourceDefinitionCatalog.ParseCanonical(await _fs.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath), false);
        Assert.True(definitions.IsValid, JsonSerializer.Serialize(definitions.Issues));
        var state = ResourceStateContract.ParseCanonical(await _fs.ReadFileAsync(ResourceMaterializationContract.StatePath), definitions.Catalog!, false);
        var history = ResourceHistoryState.ParseCanonical(await _fs.ReadFileAsync(ResourceMaterializationContract.HistoryPath), definitions.Catalog!, false);
        Assert.True(state.IsValid, JsonSerializer.Serialize(state.Issues));
        Assert.True(history.IsValid, JsonSerializer.Serialize(history.Issues));
        Assert.Empty(history.History!.ValidateStateAgreement(state.Ledger!));
        var expectedEvent = $"turn_{sourceRequest.TurnNumber}:qte_terminal:1:resource:1";
        var damage = Assert.Single(history.History.Transitions, entry => entry.EventRef == expectedEvent);
        Assert.Equal(ResourceTransitionOperation.Damage, damage.Operation);
        Assert.Equal(healthBefore.Coordinate, damage.Coordinate);
        Assert.Equal(5m, damage.RequestedAmount);
        Assert.Equal(5m, damage.AppliedAmount);
        Assert.Equal(ResourceTransitionOutcome.Applied, damage.Outcome);
        Assert.Equal(160m, damage.BeforeState!.Current);
        Assert.Equal(155m, damage.AfterState!.Current);
        Assert.Equal(sourceRequest.TurnNumber, damage.Turn);
        Assert.Equal(resources.History!.Transitions.Count + 1, history.History.Transitions.Count);
        var healthAfter = Assert.Single(state.Ledger!.Entries, entry => entry.Coordinate == healthBefore.Coordinate);
        Assert.Equal(155m, healthAfter.Current);
        Assert.Equal(damage.TransitionId, healthAfter.Chronology.LastTransitionId);
        foreach (var entry in resources.State.Entries.Where(entry => entry.Coordinate != healthBefore.Coordinate))
            Assert.Equal(entry, Assert.Single(state.Ledger.Entries, current => current.Coordinate == entry.Coordinate));
        var authority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(definitions.Catalog!,
            path => _fs.ReadFileAsync(path), state.Ledger, history.History,
            CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation);
        Assert.True(authority.IsValid, JsonSerializer.Serialize(authority.Issues));
        var continuation = JsonNode.Parse((await _fs.ReadFileAsync(QteDeferredEffectContinuation.StatePath))!)!.AsObject();
        Assert.Equal("terminal", continuation["state"]!.GetValue<string>());
        Assert.Equal("outcome_1", continuation["selectedTerminalBinding"]!["outcomeId"]!.GetValue<string>());
        Assert.Equal(sourceRequest.TurnNumber, continuation["acceptedSourceTurn"]!.GetValue<int>());
        Assert.False(string.IsNullOrWhiteSpace(continuation["terminalFingerprint"]!.GetValue<string>()));
        var qteHistory = JsonNode.Parse((await _fs.ReadFileAsync(QteSceneService.QteHistoryPath))!)!.AsArray();
        var terminal = Assert.Single(qteHistory)!;
        Assert.Equal("outcome_1", terminal["outcomeId"]!.GetValue<string>());
        Assert.Equal("success", terminal["grade"]!.GetValue<string>());
        Assert.Equal(sourceRequest.TurnNumber, terminal["acceptedAtTurn"]!.GetValue<int>());
        Assert.Equal(sourceRequest.TurnNumber, terminal["finishedAtTurn"]!.GetValue<int>());
        Assert.Null((await qte.ReadRuntimeStateAsync()).ActiveScene);
        Assert.Equal("Камень задевает героя.", GetPrivateField<GameResponse>(engine, "_lastResponse").Response);
        Assert.False(_fs.FileExists("input/turn_request.json"));
        Assert.False(_fs.FileExists("game_state/control/pending_turn_snapshot.json"));
        Assert.False(_fs.FileExists(PendingTurnSnapshotAuthority.AuthorityPath));
        Assert.False(_fs.FileExists(QteSceneService.QteOfferPath));
        await AssertPhysicalInjuryProtectedBytesAsync(protectedWounds);
        WritePhysicalInjuryDiagnostic("actual_engine_finalized_health_damage", new
        {
            sourceRequest.SessionId, sourceRequest.RequestId, SourceSnapshotToken = sourceSnapshotToken,
            SourceTurn = sourceRequest.TurnNumber, HealthBefore = healthBefore.Current, HealthAfter = healthAfter.Current,
            Damage = damage, SelectedTerminal = continuation["selectedTerminalBinding"],
            ContinuationState = continuation["state"], TerminalFingerprint = continuation["terminalFingerprint"], Screens = screens
        });

        // First causal producer gap is asserted only after actual finalized canonical health damage above.
        var occurrenceJson = await _fs.ReadFileAsync(MortalWoundOccurrenceState.StatePath);
        WritePhysicalInjuryDiagnostic("actual_engine_pending_occurrence", new { Present = occurrenceJson != null, Raw = occurrenceJson });
        Assert.True(occurrenceJson != null,
            "Finalized canonical player health Damage from the declared ordinary QTE has no pending Mortal wound occurrence.");
        var occurrences = MortalWoundOccurrenceState.Parse(occurrenceJson, MortalWoundOccurrenceState.StatePath);
        Assert.True(occurrences.IsValid, JsonSerializer.Serialize(occurrences.Issues));
        var occurrence = Assert.Single(occurrences.State!.Occurrences);
        Assert.Equal(sourceRequest.SessionId, occurrence.SourceSessionId);
        Assert.Equal(sourceRequest.RequestId, occurrence.SourceRequestId);
        Assert.Equal(sourceSnapshotToken, occurrence.SourceSnapshotToken);
        Assert.Equal(sourceRequest.TurnNumber, occurrence.SourceTurn);
        Assert.Equal("qte", occurrence.AdapterKind);
        Assert.Equal("physical", occurrence.Domain);
        Assert.Equal(2, occurrence.Outcome.MaximumSeverityRank);
    }
}
