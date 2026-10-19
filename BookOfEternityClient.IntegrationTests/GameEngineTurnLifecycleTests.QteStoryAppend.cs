using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    private const string QteStoryPath = "stories/mortal_life_2.jsonl";
    private const string QteStoryAction = "Укрыться от падающих камней.";
    private const string QteStoryNarrative = "Камни падают. Укрыться от обвала?";

    [Theory]
    [InlineData("create")]
    [InlineData("change")]
    public async Task QteStoryAppend_OriginalGmHistoryTamperIsRefused(string mutation)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var run = await RunActualQteStorySourceAsync(preacceptMutation: mutation);
        Assert.IsType<OperationCanceledException>(run.Failure);
        Assert.NotNull(run.Source);
        var expected = mutation == "create" ? "client_owned_story_continuity_created_by_gm" : "client_owned_history_surface_modified";
        Assert.Contains(run.PreacceptIssues!, issue => issue.FilePath == QteStoryPath && issue.Code == expected);
        Assert.False(run.ActionObserved);
        await AssertPhysicalInjuryProtectedBytesAsync(run.ProtectedBefore);
        WritePhysicalInjuryDiagnostic("qte_story_original_gm_refused", new { mutation, Issues = run.PreacceptIssues });
    }

    [Theory]
    [InlineData("story_altered", "client_owned_story_append_afterimage_mismatch")]
    [InlineData("unrelated_story", "client_owned_story_continuity_created_by_gm")]
    [InlineData("crosslink", "qte_deferred_continuation_runtime_mismatch")]
    [InlineData("stale_source", "qte_client_story_append_source_mismatch")]
    [InlineData("replay", "client_owned_story_append_afterimage_mismatch")]
    public async Task QteStoryAppend_PostHandoffHistoryTamperIsRefused(string mutation, string diagnostic)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var run = await RunActualQteStorySourceAsync(postHandoffMutation: mutation);
        Assert.True(run.ActionObserved, "Actual ordinary admission and client story append must precede the directed tamper.");
        Assert.NotNull(run.StoryAtHandoff);
        Assert.NotNull(run.Failure);
        Assert.Contains(diagnostic, run.Failure.ToString(), StringComparison.Ordinal);
        if (mutation == "unrelated_story") Assert.Contains("stories/mortal_life_999.jsonl", run.Failure.ToString(), StringComparison.Ordinal);
        await AssertPhysicalInjuryProtectedBytesAsync(run.ProtectedBefore);
        Assert.False(_fs.FileExists(QteSceneService.QteHistoryPath));
        WritePhysicalInjuryDiagnostic("qte_story_post_handoff_refused", new { mutation, diagnostic, Failure = run.Failure.ToString() });
    }

    [Theory]
    [InlineData("recovered_ready")]
    [InlineData("late_ready")]
    public async Task QteStoryAppend_RecoveredOrdinarySourceRetainsExactDelta(string mode)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var run = await RunActualQteStorySourceAsync(mode: mode);
        Assert.Null(run.Failure);
        await AssertActualQteStoryFinalizedAsync(_fs, run.Source!, run.StoryAtHandoff!, expectedHealth: 155m);
        Assert.False(_fs.FileExists("game_state/control/pending_turn_snapshot.json"));
    }

    [Fact]
    public async Task QteStoryAppend_DeferredReceiptResumeRetainsExactDelta()
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var run = await RunActualQteStorySourceAsync(bounded: true);
        Assert.Null(run.Failure);
        Assert.True(run.ActionObserved);
        var active = (await GetPrivateField<QteSceneService>(run.Engine!, "_qteSceneService").ReadRuntimeStateAsync()).ActiveScene;
        Assert.Equal("awaiting_receipt", active!.EffectResolutionState);
        Assert.False(_fs.FileExists("ready/turn_complete.json"));
        Assert.False(_fs.FileExists("ready/turn_error.json"));
        var retainedOriginal = await CaptureQteStoryOriginalArtifactsAsync();
        await AssertPhysicalInjuryProtectedBytesAsync(run.ProtectedBefore);
        // Without a QTE receipt, the actual idle caller must remain awaiting and must not
        // reaccept the already consumed ordinary ready or append that ordinary story twice.
        Assert.True(await InvokePrivateAsync<bool>(run.Engine!, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync"));
        Assert.Equal(PhysicalInjurySourceTurn, GetPrivateField<GameLoop>(run.Engine!, "_gameLoop").TurnNumber);
        Assert.Equal("awaiting_receipt", (await GetPrivateField<QteSceneService>(run.Engine!, "_qteSceneService").ReadRuntimeStateAsync()).ActiveScene!.EffectResolutionState);
        Assert.Equal(run.StoryAtHandoff, await _fs.ReadFileBytesAsync(QteStoryPath));
        await AssertPhysicalInjuryProtectedBytesAsync(run.ProtectedBefore);
        foreach (var pair in retainedOriginal) Assert.Equal(pair.Value, await _fs.ReadFileBytesAsync(pair.Key));
        Assert.False(_fs.FileExists("ready/turn_complete.json"));
        Assert.False(_fs.FileExists("ready/turn_error.json"));
        WritePhysicalInjuryDiagnostic("qte_story_actual_idle_without_receipt", new { run.Source!.RequestId, Turn = GetPrivateField<GameLoop>(run.Engine!, "_gameLoop").TurnNumber, State = "awaiting_receipt", OriginalArtifacts = retainedOriginal.Keys });
        var definitions = ResourceDefinitionCatalog.ParseCanonical(await _fs.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath), false).Catalog!;
        var pending = ResourcePendingResolutionState.ParseCanonical(await _fs.ReadFileAsync(ResourcePendingResolutionState.PendingPath), definitions, false);
        Assert.True(pending.IsValid, JsonSerializer.Serialize(pending.Issues));
        var pendingRequest = Assert.Single(pending.State!.Requests);
        var request = JsonNode.Parse((await _fs.ReadFileAsync(QteDeferredEffectContinuation.RequestPath))!)!.AsObject();
        // Actual pending request and correlation produce only offline GM receipt data; no accepted receipt/authority is fabricated.
        var receipts = new JsonObject { ["schemaVersion"] = 1, ["effectResolutionReceipts"] = new JsonArray(new JsonObject
        { ["requestId"] = pendingRequest.RequestId, ["resultKind"] = "resource_delta", ["amount"] = 2, ["reason"] = "Каменная крошка усиливает рану." }) };
        foreach (var field in new[] { "requestKind", "sessionId", "sessionGeneration", "continuationId", "requestId", "waveId", "waveOrdinal", "acceptedSourceTurn", "qteId", "selectedTerminalFingerprint", "pendingStateFingerprint", "fullTurnFingerprint", "semanticTurnFingerprint" })
            receipts[field] = request[field]!.DeepClone();
        await _fs.WriteFileAtomicAsync(QteDeferredEffectContinuation.ReceiptPath, receipts.ToJsonString());
        var receiptBytes = (await _fs.ReadFileBytesAsync(QteDeferredEffectContinuation.ReceiptPath))!;
        var ready = new JsonObject { ["schemaVersion"] = 1, ["receiptsFingerprint"] = "sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(receiptBytes)).ToLowerInvariant(), ["timestamp"] = DateTime.UtcNow.ToString("o"), ["status"] = "success" };
        foreach (var field in new[] { "requestKind", "sessionId", "sessionGeneration", "continuationId", "requestId", "waveId", "waveOrdinal", "acceptedSourceTurn", "qteId", "pendingStateFingerprint" }) ready[field] = request[field]!.DeepClone();
        await _fs.WriteFileAtomicAsync(QteDeferredEffectContinuation.ReadyPath, ready.ToJsonString());
        // Recreate the real engine/service on the same root, retaining generation and canonical continuation.
        var resumed = CreateGameEngine(new QueuedConsoleInputSource([]), settings => { InertRealmSettings(settings); settings.AutosaveIntervalTurns = 0; });
        await GetPrivateField<StateManager>(resumed, "_stateManager").RefreshGameStateAsync();
        GetPrivateField<GameLoop>(resumed, "_gameLoop").SetSession(run.Source!.SessionId, run.Source.TurnNumber);
        Assert.True(await InvokePrivateAsync<bool>(resumed, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync"));
        await AssertActualQteStoryFinalizedAsync(_fs, run.Source, run.StoryAtHandoff!, expectedHealth: 153m);
        AssertQteStoryOriginalArtifactsRemoved(retainedOriginal);
        WritePhysicalInjuryDiagnostic("qte_story_deferred_actual_idle_resume", new { run.Source.RequestId, OriginalArtifacts = retainedOriginal.Keys });
    }

    [Fact]
    public async Task QteStoryAppend_FreshProcessPersistedDeltaKeepsAuthority()
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var run = await RunActualQteStorySourceAsync(pauseAtAction: true);
        Assert.IsType<OperationCanceledException>(run.Failure);
        Assert.True(run.ActionObserved);
        await AssertPhysicalInjuryProtectedBytesAsync(run.ProtectedBefore);
        Assert.False(_fs.FileExists("ready/turn_complete.json"));
        Assert.False(_fs.FileExists("ready/turn_error.json"));
        var retainedOriginal = await CaptureQteStoryOriginalArtifactsAsync();
        var output = Path.Combine(_rootPath, "qte-story-cold.json");
        var support = Path.Combine(Path.GetDirectoryName(typeof(GameEngineTurnLifecycleTests).Assembly.Location)!, "BookOfEternityClient.TestSupport.dll");
        await RunBrowserColdChildAsync(Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet"),
            [support, "engine-qte-story-resume", typeof(GameEngineTurnLifecycleTests).Assembly.Location, _fs.BasePath, output], output + ".log", 45);
        var observed = JsonNode.Parse(File.ReadAllText(output))!.AsObject();
        Assert.NotEqual(Environment.ProcessId, observed["processId"]!.GetValue<int>());
        Assert.True(observed["handledByActualIdleCaller"]!.GetValue<bool>());
        Assert.Equal("terminal", observed["continuationState"]!.GetValue<string>());
        Assert.Equal(run.Source!.RequestId, observed["requestId"]!.GetValue<string>());
        Assert.Equal(run.SnapshotToken, observed["snapshotToken"]!.GetValue<string>());
        await AssertActualQteStoryFinalizedAsync(_fs, run.Source, run.StoryAtHandoff!, expectedHealth: 155m);
        AssertQteStoryOriginalArtifactsRemoved(retainedOriginal);
        var childCleanup = File.ReadAllLines(output + ".log")
            .Select(line => (Line: line, Start: line.IndexOf("{\"CleanupOwnedRoot\"", StringComparison.Ordinal)))
            .Where(item => item.Start >= 0)
            .Select(item => JsonNode.Parse(item.Line[item.Start..])!).ToArray();
        var receipt = Assert.Single(childCleanup);
        Assert.True(receipt["OwnedFixtureRemoved"]!.GetValue<bool>());
        Assert.Null(receipt["CleanupFailure"]);
        WritePhysicalInjuryDiagnostic("qte_story_actual_child_idle_completed", new
        {
            ParentProcessId = Environment.ProcessId, Child = observed, ChildCleanup = childCleanup,
            OriginalArtifacts = retainedOriginal.Keys, OriginalArtifactsRemoved = retainedOriginal.Keys.All(path => !_fs.FileExists(path)),
            StoryUnchanged = run.StoryAtHandoff!.AsSpan().SequenceEqual(File.ReadAllBytes(_fs.ResolvePath(QteStoryPath)))
        });
    }

    // Same existing TestSupport reflection transport as browser cold probes; no independent harness or manufactured QTE start.
    public static async Task WriteQteStoryResumeProbeAsync(string root, string output)
    {
        using var factory = new GameEngineTurnLifecycleTests();
        using var ownedFactory = new CleanupOwnedFixture(factory._rootPath, Console.WriteLine);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var console = new AgentConsoleStateStore();
        var screens = new List<string>();
        AgentConsoleLiveInputSource? live = null;
        using var input = live = new AgentConsoleLiveInputSource(console, TimeSpan.FromSeconds(10),
            AgentConsoleLiveInputSource.DefaultMaxQueueLength, new AgentConsoleLiveInputSourceHooks
            {
                ActiveReadRegistered = kind =>
                {
                    Assert.Equal(AgentConsoleInputKind.Key, kind);
                    var screen = console.GetSnapshot();
                    Assert.NotNull(screen);
                    Assert.StartsWith("qte-", screen!.ScreenId, StringComparison.Ordinal);
                    screens.Add(screen.ScreenId);
                    Assert.True(live!.EnqueueKey(Key(ConsoleKey.Enter)).Accepted);
                }
            });
        var engine = factory.CreateGameEngine(input, settings => { InertRealmSettings(settings); settings.AutosaveIntervalTurns = 0; }, fileSystem: files);
        await GetPrivateField<StateManager>(engine, "_stateManager").RefreshGameStateAsync();
        var qte = GetPrivateField<QteSceneService>(engine, "_qteSceneService");
        var original = JsonNode.Parse((await files.ReadFileAsync("input/turn_request.json"))!)!.AsObject();
        string token;
        string generation;
        string session;
        int turn;
        SetPrivateField(qte, "_inputSource", input);
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            var source = PendingTurnSnapshotReader.ReadCurrent(files, lease, [ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath]);
            Assert.True(source.Success, JsonSerializer.Serialize(source.Issues));
            token = source.Snapshot!.SnapshotToken;
            generation = files.GetOrCreateSessionGeneration(lease);
            session = source.Snapshot.SessionId;
            turn = source.Snapshot.TurnNumber;
        }
        GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(session, turn);
        var handled = await InvokePrivateAsync<bool>(engine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");
        var completed = JsonNode.Parse((await files.ReadFileAsync(QteDeferredEffectContinuation.StatePath))!)!.AsObject();
        Assert.NotEmpty(screens);
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { processId = Environment.ProcessId,
            handledByActualIdleCaller = handled, continuationState = completed["state"]!.GetValue<string>(),
            requestId = original["requestId"]!.GetValue<string>(), snapshotToken = token, generation, screens }));
    }

    private async Task<Dictionary<string, byte[]>> CaptureQteStoryOriginalArtifactsAsync()
    {
        var manifestJson = (await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json"))!;
        var manifest = JsonNode.Parse(manifestJson)!.AsObject();
        var paths = new[] { "input/turn_request.json", "game_state/control/pending_turn_snapshot.json", PendingTurnSnapshotAuthority.AuthorityPath }
            .Concat(manifest["files"]!.AsObject().Select(pair => pair.Value!.GetValue<string>()))
            .Concat(manifest["rollbackBackups"]!.AsObject().Select(pair => pair.Value!.GetValue<string>()))
            .Distinct(StringComparer.Ordinal).ToArray();
        var images = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var bytes = await _fs.ReadFileBytesAsync(path);
            Assert.NotNull(bytes);
            images.Add(path, bytes!);
        }
        Assert.NotEmpty(images);
        WritePhysicalInjuryDiagnostic("qte_story_retained_original_artifacts", new { Paths = images.Keys });
        return images;
    }

    private void AssertQteStoryOriginalArtifactsRemoved(IReadOnlyDictionary<string, byte[]> originals)
    {
        foreach (var path in originals.Keys) Assert.False(_fs.FileExists(path), "Completed QTE retained original artifact: " + path);
        Assert.False(_fs.FileExists("ready/turn_complete.json"));
        Assert.False(_fs.FileExists("ready/turn_error.json"));
    }

    private sealed class QteStoryRun
    {
        internal GameEngine? Engine;
        internal TurnRequest? Source;
        internal string? SnapshotToken;
        internal Exception? Failure;
        internal IReadOnlyList<ValidationIssue>? PreacceptIssues;
        internal bool ActionObserved;
        internal byte[]? StoryAtHandoff;
        internal Dictionary<string, byte[]?> ProtectedBefore = new(StringComparer.Ordinal);
    }

    // Fixture changes that would break these tests: accepting a GM history edit, recapturing arbitrary history,
    // losing exact client append at terminal/restart, or applying selected health Damage more than once.
    /// <summary>
    /// Runs the actual source turn and QTE story handoff using an isolated authored response.
    /// </summary>
    /// <param name="preacceptMutation">
    /// Optional controlled history mutation before acceptance; null preserves the ordinary response.
    /// </param>
    /// <param name="postHandoffMutation">
    /// Optional controlled mutation after the actual story handoff; null leaves it unchanged.
    /// </param>
    /// <param name="mode">
    /// Ordinary execution by default, or the existing recovery fixture mode.
    /// </param>
    /// <param name="pauseAtAction">
    /// Whether to stop with a controlled cancellation after observing the actual QTE action.
    /// </param>
    /// <param name="bounded">
    /// Whether to seed the existing bounded-reaction fixture and report its early refusal.
    /// </param>
    /// <param name="physicalDeclaration">
    /// Whether the authored offer declares a physical injury.
    /// </param>
    /// <param name="physicalMaximum">
    /// The declared injury maximum, defaulting to two.
    /// </param>
    /// <param name="physicalOnlyUnselected">
    /// Whether to remove physical injury from the selected terminal outcome.
    /// </param>
    /// <param name="observeQteAction">
    /// Optional callback after the real action handoff has passed its existing assertions.
    /// </param>
    /// <param name="retainLiveInput">
    /// Optional transfer of input disposal to the caller; null keeps disposal inside this method.
    /// </param>
    /// <param name="physicalSiblingDamage">
    /// Whether to add the existing sibling damage response fragment.
    /// </param>
    /// <param name="configureBeforePreparation">
    /// Optional world preparation before source capture; null uses the base world.
    /// </param>
    /// <param name="sourceStages">
    /// Optional bounded scalar reporter for the isolated source probe; null preserves other consumers.
    /// </param>
    /// <returns>
    /// The actual source, handoff observations and captured engine failure, if any.
    /// </returns>
    private async Task<QteStoryRun> RunActualQteStorySourceAsync(string? preacceptMutation = null,
        string? postHandoffMutation = null, string mode = "ordinary", bool pauseAtAction = false, bool bounded = false,
        bool physicalDeclaration = false, int physicalMaximum = 2, bool physicalOnlyUnselected = false,
        Action? observeQteAction = null, Action<AgentConsoleLiveInputSource>? retainLiveInput = null,
        bool physicalSiblingDamage = false, Func<FileSystemManager, Task>? configureBeforePreparation = null,
        JointInterfaceCrashStageReport? sourceStages = null)
    {
        var run = new QteStoryRun();
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        await _fs.WriteFileAtomicAsync("game_state/core/game_settings.json", "{\"qteEventsEnabled\":true}");
        await _fs.WriteFileAtomicAsync("game_state/misc/characteristics.json", JsonSerializer.Serialize(Characteristics.All.ToDictionary(name => name, _ => 20), SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync("game_state/player/experience.json", "{\"level\":1,\"experience\":0,\"experienceForNextLevel\":100}");
        var resources = ResourceBootstrapStateBuilder.BuildMortalPlayer(2, PhysicalInjurySourceTurn - 1, 20, 20, 20, 20, 20);
        Assert.True(resources.IsValid, JsonSerializer.Serialize(resources.Issues));
        await _fs.WriteFileAtomicAsync(ResourceMaterializationContract.DefinitionsPath, resources.Definitions!.ToCanonicalJson());
        await _fs.WriteFileAtomicAsync(ResourceMaterializationContract.StatePath, resources.State!.ToCanonicalJson());
        await _fs.WriteFileAtomicAsync(ResourceMaterializationContract.HistoryPath, resources.History!.ToCanonicalJson());
        var authority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(resources.Definitions, path => _fs.ReadFileAsync(path), resources.State, resources.History, CanonicalResourceOwnerAuthorityPurpose.FinalAfterImage);
        Assert.True(authority.IsValid, JsonSerializer.Serialize(authority.Issues));
        await _fs.WriteFileAtomicAsync(CanonicalResourceOwnerAuthorityComposer.AuthorityPath, authority.CanonicalAuthorityJson!);
        if (preacceptMutation == "change") await new StoryService(_fs, NullLogger<StoryService>.Instance).AppendTurnAsync(41, "Mortal World", 2, "Предыдущий ход.", "До обвала.");
        if (bounded) await SeedQteStoryBoundedReactionAsync();
        if (configureBeforePreparation is not null) await configureBeforePreparation(_fs);
        foreach (var path in new[] { ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
                     WoundIdentityState.StatePath, WoundHistoryState.HistoryPath, MortalWoundOpportunityReceiptState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
            run.ProtectedBefore[path] = await _fs.ReadFileBytesAsync(path);
        var console = new AgentConsoleStateStore();
        AgentConsoleLiveInputSource? live = null;
        var input = live = new AgentConsoleLiveInputSource(console, TimeSpan.FromSeconds(10), AgentConsoleLiveInputSource.DefaultMaxQueueLength,
            new AgentConsoleLiveInputSourceHooks { ActiveReadRegistered = kind =>
            {
                Assert.Equal(AgentConsoleInputKind.Key, kind);
                var screen = console.GetSnapshot()!;
                if (screen.ScreenId.StartsWith("qte-action-", StringComparison.Ordinal) && !run.ActionObserved)
                {
                    sourceStages?.Observe("source-qte", "enter");
                    run.StoryAtHandoff = File.ReadAllBytes(_fs.ResolvePath(QteStoryPath));
                    var rows = Encoding.UTF8.GetString(run.StoryAtHandoff).TrimStart('\uFEFF').Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    var row = Assert.Single(rows);
                    var entry = StrictJsonAuthority.Deserialize<StoryEntry>(row, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "actual client story row")!;
                    Assert.Equal(PhysicalInjurySourceTurn, entry.Turn);
                    Assert.Equal(QteStoryAction, entry.Player);
                    Assert.Equal(QteStoryNarrative, entry.Narrative);
                    Assert.Equal(run.ProtectedBefore[ResourceMaterializationContract.StatePath], File.ReadAllBytes(_fs.ResolvePath(ResourceMaterializationContract.StatePath)));
                    run.ActionObserved = true;
                    observeQteAction?.Invoke();
                    WritePhysicalInjuryDiagnostic("qte_story_actual_client_append", new { Mode = mode, RequestId = run.Source!.RequestId, SnapshotToken = run.SnapshotToken, Story = row });
                    switch (postHandoffMutation)
                    {
                        case "story_altered": File.AppendAllText(_fs.ResolvePath(QteStoryPath), " "); break;
                        case "replay": File.AppendAllText(_fs.ResolvePath(QteStoryPath), row + "\n"); break;
                        case "unrelated_story": File.WriteAllText(_fs.ResolvePath("stories/mortal_life_999.jsonl"), row + "\n"); break;
                        case "crosslink":
                            var runtime = JsonNode.Parse(File.ReadAllText(_fs.ResolvePath(QteSceneService.QteRuntimePath)))!.AsObject();
                            runtime["activeScene"]!["deferredEffectContinuationFingerprint"] = "sha256:" + new string('0', 64);
                            File.WriteAllText(_fs.ResolvePath(QteSceneService.QteRuntimePath), runtime.ToJsonString()); break;
                        case "stale_source":
                            var request = JsonNode.Parse(File.ReadAllText(_fs.ResolvePath("input/turn_request.json")))!.AsObject();
                            request["requestId"] = "stale_original_request";
                            File.WriteAllText(_fs.ResolvePath("input/turn_request.json"), request.ToJsonString()); break;
                    }
                    if (pauseAtAction) throw new OperationCanceledException("Controlled pause after actual accepted QTE/story handoff.");
                }
                Assert.True(live!.EnqueueKey(Key(ConsoleKey.Enter)).Accepted);
            }});
        using var ownedInput = retainLiveInput is null ? input : null;
        retainLiveInput?.Invoke(input);
        var stagedReady = false;
        async Task Author(SessionFinalizationCheckpoint checkpoint)
        {
            sourceStages?.ObserveSourceCheckpoint(checkpoint);
            if (checkpoint != SessionFinalizationCheckpoint.TerminalWaitStarted || stagedReady) return;
            sourceStages?.Observe("source-author", "enter");
            await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            {
                run.Source = JsonSerializer.Deserialize<TurnRequest>((await _fs.ReadFileAsync(lease, "input/turn_request.json"))!, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                var source = PendingTurnSnapshotReader.ReadCurrent(_fs, lease, [ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath]);
                Assert.True(source.Success, JsonSerializer.Serialize(source.Issues));
                Assert.Equal(run.Source.RequestId, source.Snapshot!.RequestId);
                Assert.Equal(PhysicalInjurySourceTurn, source.Snapshot.TurnNumber);
                run.SnapshotToken = source.Snapshot.SnapshotToken;
            }
            if (preacceptMutation != null)
            {
                await _fs.WriteFileAtomicAsync(QteStoryPath, "{\"turn\":42,\"player\":\"GM wrote history\",\"narrative\":\"forbidden\"}\n");
                run.PreacceptIssues = await GetPrivateField<ValidationService>(run.Engine!, "_validator").ValidateGameStateAsync(GameStateValidationPhase.ClientOwnedControlFiles);
                throw new OperationCanceledException("Stop after real original GM history admission check.");
            }
            var authoredOffer = BuildPhysicalInjuryOffer(declared: physicalDeclaration, maximum: physicalMaximum);
            if (physicalOnlyUnselected) authoredOffer["terminalOutcomes"]![0]!.AsObject().Remove("physicalInjury");
            if (physicalSiblingDamage)
            {
                var changes = authoredOffer["terminalOutcomes"]![0]!["responseFragment"]!["resourceChanges"]!.AsArray();
                var sibling = changes[0]!.DeepClone().AsObject();
                sibling["amount"] = 1;
                sibling["eventRef"] = $"turn_{PhysicalInjurySourceTurn}:qte_terminal:1:resource:2";
                sibling["reason"] = "Каменная крошка дополнительно задевает героя.";
                changes.Add(sibling);
            }
            await _fs.WriteFileAtomicAsync(QteSceneService.QteOfferPath, authoredOffer.ToJsonString());
            var timestamp = DateTime.UtcNow.ToString("o");
            await WriteJsonAsync("output/narrative_response.json", new { response = QteStoryNarrative, timestamp });
            await WriteJsonAsync("output/interface_updates.json", new { dialogueOptions = Array.Empty<object>(), timestamp });
            await WriteJsonAsync("output/debug_logs.json", new { timestamp, gm_thoughts_markdown = "## NPC Scope\n- Mode: Scene-local\n- Relevant actors: нет\n- Why relevant: Обвал без участия NPC.\n- Actors outside scope: нет\n- Why outside scope: В сцене нет других акторов.\n\n## Reasoning\n- Only the QTE offer is authored; no ordinary state changes." });
            await WriteJsonAsync("ready/turn_complete.json", new { sessionId = run.Source.SessionId, requestId = run.Source.RequestId,
                turnNumber = run.Source.TurnNumber, status = "success", timestamp, filesModified = new[] { QteSceneService.QteOfferPath, "output/narrative_response.json", "output/interface_updates.json", "output/debug_logs.json" } });
            stagedReady = true;
            sourceStages?.Observe("source-author", "return");
            if (mode != "ordinary") throw new OperationCanceledException("Original real request/output staged before separate ready recovery.");
        }
        GameEngine NewEngine(bool hooks)
        {
            var engine = CreateGameEngine(input, settings => { InertRealmSettings(settings); settings.AutosaveIntervalTurns = 0; },
                finalizationHooks: hooks ? new GameEngineSessionFinalizationHooks { AtCheckpointAsync = Author } : null);
            SetPrivateField(GetPrivateField<QteSceneService>(engine, "_qteSceneService"), "_inputSource", input);
            GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession("session_qte_story_append", 41);
            return engine;
        }
        run.Engine = NewEngine(true);
        await _fs.WriteFileAtomicAsync("game_state/history/chat_log.json", new JsonObject { ["sessionId"] = "session_qte_story_append", ["entries"] = new JsonArray() }.ToJsonString());
        await GetPrivateField<StateManager>(run.Engine, "_stateManager").RefreshGameStateAsync();
        sourceStages?.Observe("source-turn", "enter");
        run.Failure = await Record.ExceptionAsync(() => InvokePrivateTaskAsync(run.Engine, "ProcessPlayerTurn", QteStoryAction, null));
        sourceStages?.Observe("source-turn", run.Failure is null ? "return" : "error", run.Failure?.GetType().Name);
        if (mode != "ordinary")
        {
            Assert.IsType<OperationCanceledException>(run.Failure);
            Assert.True(stagedReady);
            Assert.False(_fs.FileExists(QteStoryPath));
            run.Engine = NewEngine(false);
            await GetPrivateField<StateManager>(run.Engine, "_stateManager").RefreshGameStateAsync();
            run.Failure = await Record.ExceptionAsync(() => InvokePrivateTaskAsync(run.Engine,
                mode == "recovered_ready" ? "WaitForGmResponse" : "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync"));
        }
        if (bounded && !run.ActionObserved)
        {
            // Preserve the actual engine outcome; expose full current-state refusal codes only
            // for this bounded fixture's unreached source/action boundary.
            try
            {
                var issues = await GetPrivateField<ValidationService>(run.Engine!, "_validator").ValidateGameStateAsync();
                WritePhysicalInjuryDiagnostic("qte_story_bounded_early_refusal", new
                {
                    SourceObserved = run.Source != null, run.ActionObserved,
                    EngineFailure = run.Failure?.ToString(), Issues = issues
                });
            }
            catch (Exception diagnosticFailure)
            {
                WritePhysicalInjuryDiagnostic("qte_story_bounded_early_refusal_diagnostic_failure", new
                {
                    SourceObserved = run.Source != null, run.ActionObserved,
                    EngineFailure = run.Failure?.ToString(), DiagnosticFailure = diagnosticFailure.ToString()
                });
            }
        }
        WritePhysicalInjuryDiagnostic("qte_story_actual_engine_result", new { Mode = mode, preacceptMutation, postHandoffMutation, run.ActionObserved, Failure = run.Failure?.ToString() });
        return run;
    }

    private static async Task AssertActualQteStoryFinalizedAsync(FileSystemManager files, TurnRequest source, byte[] story, decimal expectedHealth)
    {
        Assert.Equal(story, await files.ReadFileBytesAsync(QteStoryPath));
        var definitions = ResourceDefinitionCatalog.ParseCanonical(await files.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath), false);
        Assert.True(definitions.IsValid, JsonSerializer.Serialize(definitions.Issues));
        var state = ResourceStateContract.ParseCanonical(await files.ReadFileAsync(ResourceMaterializationContract.StatePath), definitions.Catalog!, false);
        var history = ResourceHistoryState.ParseCanonical(await files.ReadFileAsync(ResourceMaterializationContract.HistoryPath), definitions.Catalog!, false);
        Assert.True(state.IsValid, JsonSerializer.Serialize(state.Issues));
        Assert.True(history.IsValid, JsonSerializer.Serialize(history.Issues));
        Assert.Empty(history.History!.ValidateStateAgreement(state.Ledger!));
        var selected = Assert.Single(history.History.Transitions, transition => transition.EventRef == $"turn_{source.TurnNumber}:qte_terminal:1:resource:1");
        Assert.Equal(ResourceTransitionOperation.Damage, selected.Operation);
        Assert.Equal(ResourceTransitionOutcome.Applied, selected.Outcome);
        Assert.Equal(5m, selected.AppliedAmount);
        Assert.Equal(160m, selected.BeforeState!.Current);
        Assert.Equal(155m, selected.AfterState!.Current);
        Assert.Equal(expectedHealth, Assert.Single(state.Ledger!.Entries, entry => entry.Coordinate.ResourceKey == "health").Current);
        var terminal = Assert.Single(JsonNode.Parse((await files.ReadFileAsync(QteSceneService.QteHistoryPath))!)!.AsArray())!;
        Assert.Equal(source.TurnNumber, terminal["acceptedAtTurn"]!.GetValue<int>());
        var continuation = JsonNode.Parse((await files.ReadFileAsync(QteDeferredEffectContinuation.StatePath))!)!.AsObject();
        Assert.Equal("terminal", continuation["state"]!.GetValue<string>());
        Assert.Null(JsonNode.Parse((await files.ReadFileAsync(QteSceneService.QteRuntimePath))!)["activeScene"]);
    }

    private async Task SeedQteStoryBoundedReactionAsync()
    {
        // Reuse the existing exact source/carrier fixture; copy its real canonical definitions, not pending authority.
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        using var ownedSource = new CleanupOwnedFixture(context.RootPath, line => _directGachaOutput?.WriteLine(line));
        var trigger = new JsonObject { ["triggerId"] = "on_resource_damaged", ["eventType"] = "resource_damaged", ["priority"] = 100,
            ["componentIds"] = new JsonArray("component_001"), ["consumeUses"] = true, ["resolutionMode"] = "bounded_receipt" };
        var definition = EffectMaterializationTestFixture.CreateDefinition("periodic_damage");
        definition["lifetime"] = new JsonObject { ["mode"] = "uses", ["initialUses"] = 1, ["consumingEventTypes"] = new JsonArray("resource_damaged") };
        definition["triggers"] = new JsonArray(trigger.DeepClone());
        await context.SeedPlayerSkillSourceAsync(definition);
        var skills = (await context.ReadJsonAsync(EffectMaterializationTestContext.MaterializableSkillPath))!.AsObject();
        Assert.Empty(Assert.IsType<JsonArray>(skills["removeActiveSkills"]));
        skills.Remove("removeActiveSkills");
        await _fs.WriteFileAtomicAsync(EffectMaterializationTestContext.MaterializableSkillPath, skills.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await WriteJsonAsync("game_state/player/skill_mastery.json", new
        {
            skillMasteryChanges = new[]
            {
                new
                {
                    skillId = EffectMaterializationTestContext.MaterializableSkillId,
                    skillName = "Кровавый след", targetKind = "active_skill_mastery",
                    newMasteryLevel = 1, newCurrentMasteryProgress = 0,
                    newMasteryProgressNeeded = 100, masteryLeveledUp = false
                }
            }
        });
        var effect = EffectMaterializationTestContext.CreateSkillCanonicalEffect(profile: "periodic_damage");
        effect["lifetime"] = new JsonObject { ["mode"] = "uses", ["remainingUses"] = 1, ["consumingTriggerIds"] = new JsonArray("on_resource_damaged") };
        effect["triggers"] = new JsonArray(trigger.DeepClone());
        await _fs.WriteFileAtomicAsync(EffectMaterializationTestContext.PlayerEffectsPath, new JsonObject { ["schemaVersion"] = 1, ["activeEffects"] = new JsonArray(effect.DeepClone()) }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync(EffectMaterializationTestContext.IdentityIndexPath, EffectMaterializationTestFixture.CreateIdentityIndex(effect).ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        // Match the existing publisher before original source/protected-byte capture; no runtime afterimages.
        foreach (var path in new[] { ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath,
                     ResourceMaterializationContract.HistoryPath, CanonicalResourceOwnerAuthorityComposer.AuthorityPath })
        {
            var root = JsonNode.Parse((await _fs.ReadFileAsync(path))!)!;
            await _fs.WriteFileAtomicAsync(path, root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        }
    }
}
