using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task QtePhysicalInjuryConsumer_ActualArchiveLoadsIntoFreshProcessAndStagesNewDecision(bool materialize, bool consumed)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        AgentConsoleLiveInputSource? qteInput = null;
        QteStoryRun source;
        try { source = await RunActualQteStorySourceAsync(physicalDeclaration: true, physicalMaximum: 2, retainLiveInput: input => qteInput = input); }
        finally { qteInput?.Dispose(); }
        Assert.Null(source.Failure);
        await AssertActualQteStoryFinalizedAsync(_fs, source.Source!, source.StoryAtHandoff!, 155m);
        var occurrence = await AssertActualPhysicalOccurrenceAsync(source, 2);
        var expected = MortalArchiveSource(source, occurrence.OccurrenceId, occurrence.OpportunityRef);
        var completedTurn = consumed ? 43 : 42;
        if (consumed)
        {
            using var run = CreateMortalArchiveOrdinaryTurn(_fs, materialize, resourceWork: false, pauseAfterPublication: false);
            GetPrivateField<GameLoop>(run.Engine, "_gameLoop").SetSession(source.Source!.SessionId, 42);
            await GetPrivateField<StateManager>(run.Engine, "_stateManager").RefreshGameStateAsync();
            run.Failure = await Record.ExceptionAsync(() => InvokePrivateTaskAsync(run.Engine, "ProcessPlayerTurn", "Осмотреть порез после обвала.", null));
            WritePhysicalInjuryDiagnostic("qte_archive_parent_completed_decision", MortalArchiveRunObservation(run));
            Assert.Null(run.Failure);
            Assert.True(run.Accepted);
            Assert.Equal(43, GetPrivateField<GameLoop>(run.Engine, "_gameLoop").TurnNumber);
            Assert.Equal(new[] { 42, 43 }, await MortalArchiveStoryTurnsAsync(_fs));
            await AssertMortalArchiveDecisionAsync(_fs, expected, materialize, run.Snapshot!);
        }
        else Assert.Equal(new[] { 42 }, await MortalArchiveStoryTurnsAsync(_fs));

        string generation;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync()) generation = _fs.GetOrCreateSessionGeneration(lease);
        var save = GetPrivateField<SaveLoadService>(source.Engine!, "_saveLoad");
        var saveDirectory = _fs.ResolvePath("saves/manual_saves");
        var saveBefore = Directory.GetFiles(saveDirectory, "*.zip").ToDictionary(path => path,
            path => MortalArchiveHash(File.ReadAllBytes(path)), StringComparer.Ordinal);
        var saveSucceeded = await save.SaveGameAsync("qte_archive_qualification", "Actual pending or consumed Mortal QTE decision.", turnNumber: completedTurn);
        var saveAfter = Directory.GetFiles(saveDirectory, "*.zip").ToDictionary(path => path,
            path => MortalArchiveHash(File.ReadAllBytes(path)), StringComparer.Ordinal);
        var createdArchives = saveAfter.Keys.Except(saveBefore.Keys, StringComparer.Ordinal).ToArray();
        WritePhysicalInjuryDiagnostic("qte_archive_actual_save_catalog", new
        {
            materialize, consumed, completedTurn, saveSucceeded, SaveBefore = saveBefore, SaveAfter = saveAfter, CreatedArchives = createdArchives
        });
        Assert.True(saveSucceeded);
        foreach (var pair in saveBefore)
        {
            Assert.True(saveAfter.TryGetValue(pair.Key, out var savedHash));
            Assert.Equal(pair.Value, savedHash);
        }
        var archivePath = Assert.Single(createdArchives);
        string[] archiveEntries;
        using (var archive = ZipFile.OpenRead(archivePath))
        {
            AssertMortalArchiveExcludedEntries(archive);
            archiveEntries = archive.Entries.Select(entry => entry.FullName).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        }
        var immutable = await CaptureMortalArchiveImagesAsync(_fs, MortalArchiveImmutablePaths);
        var loadedRoot = Path.Combine(_rootPath, "loaded-session-root");
        var output = Path.Combine(_rootPath, "archive-child.json");
        var context = new JsonObject
        {
            ["source"] = expected, ["archivePath"] = archivePath, ["archiveSha256"] = MortalArchiveHash(File.ReadAllBytes(archivePath)),
            ["saveBefore"] = JsonSerializer.SerializeToNode(saveBefore), ["saveAfter"] = JsonSerializer.SerializeToNode(saveAfter),
            ["archiveEntries"] = JsonSerializer.SerializeToNode(archiveEntries), ["binaryPins"] = JsonSerializer.SerializeToNode(MortalArchiveBinaryPins()),
            ["dependencyContext"] = MortalArchiveDependencyContext(),
            ["parentGeneration"] = generation, ["parentProcessId"] = Environment.ProcessId,
            ["completedTurn"] = completedTurn, ["immutableImages"] = JsonSerializer.SerializeToNode(immutable),
            ["receiptBefore"] = await _fs.ReadFileAsync(MortalWoundOpportunityReceiptState.StatePath),
            ["historyBefore"] = await _fs.ReadFileAsync(WoundHistoryState.HistoryPath)
        };
        var contextPath = Path.Combine(_rootPath, "archive-child-context.json");
        await File.WriteAllTextAsync(contextPath, context.ToJsonString());
        var failure = await Record.ExceptionAsync(() => RunMortalArchiveChildAsync("engine-qte-wound-archive", loadedRoot, output,
            (materialize ? 1 : 0) + (consumed ? 2 : 0), contextPath));
        var observed = File.Exists(output) ? JsonNode.Parse(File.ReadAllText(output)) : null;
        var cleanup = MortalArchiveChildCleanup(output + ".log");
        WritePhysicalInjuryDiagnostic("qte_archive_actual_child", new
        {
            materialize, consumed, ParentProcessId = Environment.ProcessId, Archive = context, Child = observed,
            ChildFailure = failure?.ToString(), ChildCleanup = cleanup, LoadedRootAbsent = !Directory.Exists(loadedRoot)
        });
        Assert.Null(failure);
        Assert.NotNull(observed);
        Assert.NotEqual(Environment.ProcessId, observed!["processId"]!.GetValue<int>());
        Assert.Null(observed["failure"]);
        Assert.True(observed["actualLoadAndRebind"]!.GetValue<bool>());
        Assert.NotEqual(generation, observed["generation"]!.GetValue<string>());
        Assert.Equal(completedTurn + 1, observed["actualTurn"]!.GetValue<int>());
        Assert.False(Directory.Exists(loadedRoot));
        Assert.Equal(2, cleanup.Length); // Borrowed loaded root and auxiliary factory root are separately owned.
        foreach (var receipt in cleanup) { Assert.True(receipt["OwnedFixtureRemoved"]!.GetValue<bool>()); Assert.Null(receipt["CleanupFailure"]); }
        foreach (var pair in immutable) Assert.Equal(pair.Value, await _fs.ReadFileBytesAsync(pair.Key));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task QtePhysicalInjuryConsumer_ActualDurableSessionFreshProcessReplaysOrRefusesWithoutUndoingPublication(bool materialize, bool changed)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        AgentConsoleLiveInputSource? qteInput = null;
        QteStoryRun source;
        try { source = await RunActualQteStorySourceAsync(physicalDeclaration: true, physicalMaximum: 2, retainLiveInput: input => qteInput = input); }
        finally { qteInput?.Dispose(); }
        Assert.Null(source.Failure);
        await AssertActualQteStoryFinalizedAsync(_fs, source.Source!, source.StoryAtHandoff!, 155m);
        var occurrence = await AssertActualPhysicalOccurrenceAsync(source, 2);
        var expected = MortalArchiveSource(source, occurrence.OccurrenceId, occurrence.OpportunityRef);
        MortalArchiveTurn run;
        using (run = CreateMortalArchiveOrdinaryTurn(_fs, materialize, resourceWork: true, pauseAfterPublication: true))
        {
            GetPrivateField<GameLoop>(run.Engine, "_gameLoop").SetSession(source.Source!.SessionId, 42);
            await GetPrivateField<StateManager>(run.Engine, "_stateManager").RefreshGameStateAsync();
            run.Failure = await Record.ExceptionAsync(() => InvokePrivateTaskAsync(run.Engine, "ProcessPlayerTurn", "Осмотреть порез после обвала.", null));
        } // Release the parent's live input before the child; keep this physical fixture alive.
        WritePhysicalInjuryDiagnostic("qte_durable_parent_published_pause", MortalArchiveRunObservation(run));
        var pause = Assert.IsType<OperationCanceledException>(run.Failure);
        Assert.Equal(MortalArchivePauseMessage, pause.Message);
        Assert.True(run.Accepted);
        Assert.Equal(42, GetPrivateField<GameLoop>(run.Engine, "_gameLoop").TurnNumber);
        Assert.Equal(new[] { 42 }, await MortalArchiveStoryTurnsAsync(_fs));
        await AssertMortalArchiveDecisionAsync(_fs, expected, materialize, run.Snapshot!);
        await AssertMortalArchiveResourceWorkAsync(_fs, materialize);
        Assert.Equal(run.ReadyBytes, await _fs.ReadFileBytesAsync("ready/turn_complete.json"));
        Assert.NotNull(run.ReadyBytes);
        var original = await CaptureQteStoryOriginalArtifactsAsync();
        var mechanics = await CaptureMortalArchiveImagesAsync(_fs, MortalArchiveMechanicalPaths);
        Assert.NotNull(mechanics[MortalQteDecisionPublication.StatePath]);
        string generation;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync()) generation = _fs.GetOrCreateSessionGeneration(lease);
        var output = Path.Combine(_rootPath, "durable-child.json");
        var contextPath = Path.Combine(_rootPath, "durable-child-context.json");
        await File.WriteAllTextAsync(contextPath, new JsonObject
        {
            ["source"] = expected, ["parentGeneration"] = generation, ["parentProcessId"] = Environment.ProcessId,
            ["binaryPins"] = JsonSerializer.SerializeToNode(MortalArchiveBinaryPins()),
            ["dependencyContext"] = MortalArchiveDependencyContext(),
            ["requestId"] = run.Snapshot!.RequestId, ["snapshotToken"] = run.Snapshot.SnapshotToken,
            ["originalImages"] = JsonSerializer.SerializeToNode(original), ["mechanicalImages"] = JsonSerializer.SerializeToNode(mechanics),
            ["readyBytes"] = JsonSerializer.SerializeToNode(run.ReadyBytes), ["decision"] = run.Decision!.DeepClone()
        }.ToJsonString());
        var failure = await Record.ExceptionAsync(() => RunMortalArchiveChildAsync("engine-qte-wound-durable", _fs.BasePath, output,
            (materialize ? 1 : 0) + (changed ? 2 : 0), contextPath));
        var childResultRaw = File.Exists(output) ? File.ReadAllText(output) : null;
        var childLog = File.Exists(output + ".log") ? File.ReadAllText(output + ".log") : null;
        var availableChildArtifacts = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var path in new[] { "input/turn_request.json", "ready/turn_complete.json", "ready/turn_error.json",
                     "game_state/control/validation_repair_request.json", "game_state/control/validation_repair_ready.json",
                     "game_state/control/gm_validation_repair_artifact_stall_report.json", MortalWoundOpportunityAdapter.DecisionPath })
            availableChildArtifacts[path] = await _fs.ReadFileAsync(path);
        var observed = childResultRaw is null ? null : JsonNode.Parse(childResultRaw);
        var cleanup = MortalArchiveChildCleanup(output + ".log");
        WritePhysicalInjuryDiagnostic("qte_durable_actual_child", new
        {
            materialize, changed, ParentProcessId = Environment.ProcessId, Child = observed, ChildFailure = failure?.ToString(),
            ChildResultRaw = childResultRaw, ChildLog = childLog, ChildContext = JsonNode.Parse(File.ReadAllText(contextPath)),
            AvailableChildArtifacts = availableChildArtifacts, ChildCleanup = cleanup, OriginalHashes = MortalArchiveDigests(original.ToDictionary(p => p.Key, p => (byte[]?)p.Value)),
            MechanicalBefore = MortalArchiveDigests(mechanics), MechanicalAfter = MortalArchiveDigests(await CaptureMortalArchiveImagesAsync(_fs, MortalArchiveMechanicalPaths))
        });
        Assert.Null(failure);
        Assert.NotNull(observed);
        Assert.NotEqual(Environment.ProcessId, observed!["processId"]!.GetValue<int>());
        Assert.Null(observed["failure"]);
        Assert.Equal(generation, observed["generation"]!.GetValue<string>());
        foreach (var pair in mechanics) Assert.Equal(pair.Value, await _fs.ReadFileBytesAsync(pair.Key));
        Assert.Equal(changed ? new[] { 42 } : new[] { 42, 43 }, await MortalArchiveStoryTurnsAsync(_fs));
        Assert.Equal(changed ? 42 : 43, observed["actualTurn"]!.GetValue<int>());
        Assert.Single(cleanup); // Child cleans only its factory root, never this borrowed durable root.
        Assert.True(cleanup[0]["OwnedFixtureRemoved"]!.GetValue<bool>());
        Assert.Null(cleanup[0]["CleanupFailure"]);
        if (!changed) AssertQteStoryOriginalArtifactsRemoved(original);
    }

    // TestSupport reflection transport: actual LoadGame/rebind, then a genuine new ordinary request.
    public static async Task WriteMortalQteArchiveProbeAsync(string root, string output, int scenario, string contextPath)
    {
        Assert.InRange(scenario, 0, 3);
        var materialize = (scenario & 1) != 0;
        var consumed = (scenario & 2) != 0;
        var context = JsonNode.Parse(File.ReadAllText(contextPath))!.AsObject();
        using var factory = new GameEngineTurnLifecycleTests();
        using var factoryOwned = new CleanupOwnedFixture(factory._rootPath, Console.WriteLine);
        using var loadedOwned = new CleanupOwnedFixture(root, Console.WriteLine);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        files.EnsureDirectoryStructure();
        var targetInitialization = new
        {
            GameSessionPath = files.GameSessionPath, GameSessionExists = Directory.Exists(files.GameSessionPath),
            Directories = Directory.GetDirectories(files.GameSessionPath, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(files.GameSessionPath, path)).OrderBy(path => path, StringComparer.Ordinal).ToArray(),
            Files = Directory.GetFiles(files.GameSessionPath, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(files.GameSessionPath, path)).OrderBy(path => path, StringComparer.Ordinal).ToArray()
        };
        Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "qte_archive_child_target_initialized", Observation = targetInitialization }));
        Assert.True(targetInitialization.GameSessionExists);
        Assert.Empty(targetInitialization.Files); // Ordinary directories only: no copied source, snapshot or canonical state.
        using var run = factory.CreateMortalArchiveOrdinaryTurn(files, consumed ? null : materialize, false, false);
        Exception? failure = null;
        LoadReplacementResult? load = null;
        string? generation = null;
        Dictionary<string, byte[]?>? loaded = null;
        Dictionary<string, byte[]?>? after = null;
        object? profileProjection = null;
        object? loadedDependencyBindings = null;
        object? targetBootstrap = null;
        string? bootstrapGeneration = null;
        try
        {
            Assert.NotEqual(Environment.ProcessId, context["parentProcessId"]!.GetValue<int>());
            AssertMortalArchiveChildBinaries(context);
            bootstrapGeneration = await GetPrivateField<StateManager>(run.Engine, "_stateManager").BootstrapLocalStorageAsync();
            var bootstrapFiles = Directory.GetFiles(files.GameSessionPath, "*", SearchOption.AllDirectories)
                .ToDictionary(path => Path.GetRelativePath(files.GameSessionPath, path).Replace('\\', '/'),
                    path => MortalArchiveHash(File.ReadAllBytes(path)), StringComparer.Ordinal);
            var localGenerationBytes = File.ReadAllBytes(files.SessionGenerationPath);
            targetBootstrap = new
            {
                BootstrapGeneration = bootstrapGeneration, CanonicalFiles = bootstrapFiles,
                ConfigPath = files.ResolvePath("config.json"), LocalGenerationPath = files.SessionGenerationPath,
                LocalGenerationLength = localGenerationBytes.LongLength, LocalGenerationSha256 = MortalArchiveHash(localGenerationBytes)
            };
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "qte_archive_child_ordinary_bootstrap", Observation = targetBootstrap }));
            Assert.Equal(new[] { "config.json" }, bootstrapFiles.Keys.OrderBy(path => path, StringComparer.Ordinal));
            Assert.Equal(bootstrapGeneration, JsonNode.Parse(MortalArchiveText(localGenerationBytes))!["GenerationId"]!.GetValue<string>());
            Assert.NotEqual(context["parentGeneration"]!.GetValue<string>(), bootstrapGeneration);
            var archivePath = context["archivePath"]!.GetValue<string>();
            Assert.Equal(context["archiveSha256"]!.GetValue<string>(), MortalArchiveHash(File.ReadAllBytes(archivePath)));
            load = await InvokePrivateAsync<LoadReplacementResult>(run.Engine, "LoadSelectedSaveAndRebindRuntimeAsync", archivePath);
            Assert.Equal(LoadReplacementDisposition.Committed, load.Disposition);
            Assert.False(load.ContinuationBlocked);
            Assert.Null(load.Failure);
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync()) generation = files.GetOrCreateSessionGeneration(lease);
            Assert.Equal(load.EstablishedGeneration, generation);
            Assert.NotEqual(bootstrapGeneration, generation);
            Assert.NotEqual(context["parentGeneration"]!.GetValue<string>(), generation);
            var loop = GetPrivateField<GameLoop>(run.Engine, "_gameLoop");
            Assert.Equal(context["source"]!["sessionId"]!.GetValue<string>(), loop.SessionId);
            Assert.Equal(consumed ? 43 : 42, loop.TurnNumber); // Derived by actual rebind from the loaded stories.
            Assert.Equal(consumed ? new[] { 42, 43 } : new[] { 42 }, await MortalArchiveStoryTurnsAsync(files));
            foreach (var path in new[] { "input/turn_request.json", "ready/turn_complete.json", "game_state/control/pending_turn_snapshot.json",
                         PendingTurnSnapshotAuthority.AuthorityPath, MortalWoundOpportunityAdapter.DecisionPath,
                         ResourceMaterializationContract.CommandPath, AcceptedMechanicsPlan.WoundCommandPath }) Assert.False(files.FileExists(path));
            Assert.Empty(Directory.GetFiles(files.ResolvePath("game_state/control"), "*", SearchOption.AllDirectories)
                .Where(path => Path.GetRelativePath(files.GameSessionPath, path).Replace('\\', '/').StartsWith("game_state/control/pending_turn_snapshot/", StringComparison.Ordinal)));
            loaded = await CaptureMortalArchiveImagesAsync(files, MortalArchiveImmutablePaths);
            loadedDependencyBindings = MortalArchiveLoadedDependencyBindings();
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "qte_archive_child_loaded_dependencies", Observation = loadedDependencyBindings }));
            using (var archive = ZipFile.OpenRead(archivePath))
            {
                AssertMortalArchiveExcludedEntries(archive);
                Assert.Equal(context["archiveEntries"]!.AsArray().Select(node => node!.GetValue<string>()),
                    archive.Entries.Select(entry => entry.FullName).OrderBy(name => name, StringComparer.Ordinal));
                foreach (var path in MortalArchiveImmutablePaths) Assert.Equal(MortalArchiveEntryBytes(archive, path), loaded[path]);
                var originalProfile = AfterlifeEntityProfileState.DecodeMirrorObject(MortalArchiveEntryBytes(archive, AfterlifeEntityProfileState.StatePath));
                var expectedProfile = originalProfile?.DeepClone().AsObject();
                if (expectedProfile is not null)
                    AfterlifeEntityProfileState.ApplyPlayerSoulProfileClientAuthority(expectedProfile,
                        AfterlifeEntityProfileState.DecodeMirrorObject(MortalArchiveEntryBytes(archive, "game_state/meta/soul_state.json")),
                        AfterlifeEntityProfileState.DecodeMirrorObject(MortalArchiveEntryBytes(archive, ShiningAbodeState.StatePath)));
                var actualProfile = AfterlifeEntityProfileState.DecodeMirrorObject(await files.ReadFileBytesAsync(AfterlifeEntityProfileState.StatePath));
                profileProjection = new { Archived = originalProfile, ExpectedClientProjection = expectedProfile, ActualLoaded = actualProfile };
                Assert.True(JsonNode.DeepEquals(expectedProfile, actualProfile));
            }
            var inheritedMarker = loaded[MortalQteDecisionPublication.StatePath];
            if (inheritedMarker is not null)
                Assert.NotEqual(generation, JsonNode.Parse(MortalArchiveText(inheritedMarker))!["generation"]!.GetValue<string>());
            await AssertMortalArchiveResourceAgreementAsync(files);
            var previousRequest = context["receiptBefore"]?.GetValue<string>() is { } priorJson
                ? Assert.Single(MortalWoundOpportunityReceiptState.Parse(priorJson, MortalWoundOpportunityReceiptState.StatePath).State!.Receipts).RequestId : null;
            run.Failure = await Record.ExceptionAsync(() => InvokePrivateTaskAsync(run.Engine, "ProcessPlayerTurn", "Осмотреть состояние после загрузки.", null));
            Assert.Null(run.Failure);
            Assert.True(run.Accepted);
            Assert.NotNull(run.Snapshot);
            Assert.Equal(consumed ? 44 : 43, run.Snapshot!.TurnNumber);
            Assert.NotEqual(context["source"]!["requestId"]!.GetValue<string>(), run.Snapshot.RequestId);
            Assert.NotEqual(context["source"]!["snapshotToken"]!.GetValue<string>(), run.Snapshot.SnapshotToken);
            Assert.NotEqual(previousRequest, run.Snapshot.RequestId);
            Assert.Equal(consumed ? new[] { 42, 43, 44 } : new[] { 42, 43 }, await MortalArchiveStoryTurnsAsync(files));
            await AssertMortalArchiveDecisionAsync(files, context["source"]!.AsObject(), materialize, consumed ? null : run.Snapshot, laterOrdinaryTurn: consumed);
            after = await CaptureMortalArchiveImagesAsync(files, MortalArchiveImmutablePaths);
            if (consumed)
            {
                Assert.Null(run.Decision);
                Assert.Equal(loaded[MortalWoundOpportunityReceiptState.StatePath], after[MortalWoundOpportunityReceiptState.StatePath]);
                Assert.Equal(loaded[WoundIdentityState.StatePath], after[WoundIdentityState.StatePath]);
                if (materialize)
                {
                    var history = await MortalArchiveResourceHistoryAsync(files);
                    var tick = Assert.Single(history.Transitions, row => row.Turn == 44 && row.SourceEvidence.SourceKind == "effect_component");
                    Assert.Equal(ResourceTransitionOperation.Damage, tick.Operation);
                    Assert.Equal(ResourceTransitionOutcome.Applied, tick.Outcome);
                    Assert.Equal(3m, tick.AppliedAmount);
                    Assert.Equal(tick.BeforeState!.Current - 3m, tick.AfterState!.Current);
                }
            }
            Assert.Equal(consumed ? 44 : 43, loop.TurnNumber);
            Assert.False(files.FileExists(MortalWoundOpportunityAdapter.DecisionPath));
            Assert.False(files.FileExists(AcceptedMechanicsPlan.WoundCommandPath));
            Assert.False(files.FileExists("input/turn_request.json"));
            Assert.False(files.FileExists("game_state/control/pending_turn_snapshot.json"));
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new
            {
                processId = Environment.ProcessId, scenario, actualLoadAndRebind = load?.Disposition == LoadReplacementDisposition.Committed && !load.ContinuationBlocked,
                Load = load is null ? null : new { load.Disposition, load.EstablishedGeneration, load.ContinuationBlocked, Failure = load.Failure?.ToString() },
                BinaryPins = MortalArchiveBinaryPins(), DependencyContext = MortalArchiveDependencyContext(),
                ProcessArguments = Environment.GetCommandLineArgs(), LoadedDependencyBindings = loadedDependencyBindings,
                FreshFactory = true, targetInitialization, targetBootstrap, bootstrapGeneration, profileProjection, generation,
                actualTurn = GetPrivateField<GameLoop>(run.Engine, "_gameLoop").TurnNumber,
                failure = failure?.ToString(), Run = MortalArchiveRunObservation(run), Loaded = loaded is null ? null : MortalArchiveDigests(loaded),
                After = after is null ? null : MortalArchiveDigests(after), Receipt = await files.ReadFileAsync(MortalWoundOpportunityReceiptState.StatePath),
                History = await files.ReadFileAsync(WoundHistoryState.HistoryPath), ResourceHistory = await files.ReadFileAsync(ResourceMaterializationContract.HistoryPath)
            }));
        }
    }

    // Same physical durable session: no snapshot copy, archive load, re-signing or carried cache.
    public static async Task WriteMortalQteDurableProbeAsync(string root, string output, int scenario, string contextPath)
    {
        Assert.InRange(scenario, 0, 3);
        var materialize = (scenario & 1) != 0;
        var changed = (scenario & 2) != 0;
        var context = JsonNode.Parse(File.ReadAllText(contextPath))!.AsObject();
        using var factory = new GameEngineTurnLifecycleTests();
        using var factoryOwned = new CleanupOwnedFixture(factory._rootPath, Console.WriteLine);
        Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "qte_durable_child_owned_entry", Observation = new
        { ProcessId = Environment.ProcessId, scenario, BorrowedDurableRoot = root, AuxiliaryFactoryRoot = factory._rootPath,
            ParentProcessId = context["parentProcessId"]!.GetValue<int>() } }));
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        using var run = factory.CreateMortalArchiveOrdinaryTurn(files, null, false, false, cancelRepair: changed);
        Exception? failure = null;
        string? generation = null;
        PendingTurnSnapshotReadAuthority? original = null;
        bool? handled = null, secondIdle = null;
        var before = await CaptureMortalArchiveImagesAsync(files, MortalArchiveMechanicalPaths);
        var loadedDependencyBindings = MortalArchiveLoadedDependencyBindings();
        Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "qte_durable_child_loaded_dependencies", Observation = loadedDependencyBindings }));
        Dictionary<string, byte[]?>? after = null;
        try
        {
            Assert.NotEqual(Environment.ProcessId, context["parentProcessId"]!.GetValue<int>());
            AssertMortalArchiveChildBinaries(context);
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                generation = files.GetOrCreateSessionGeneration(lease);
                original = MortalQteWoundOpportunityProjection.ReadSnapshot(files, lease);
            }
            Assert.Equal(context["parentGeneration"]!.GetValue<string>(), generation);
            Assert.Equal(context["requestId"]!.GetValue<string>(), original.RequestId);
            Assert.Equal(context["snapshotToken"]!.GetValue<string>(), original.SnapshotToken);
            Assert.Equal(43, original.TurnNumber);
            foreach (var pair in context["originalImages"]!.AsObject()) Assert.Equal(MortalArchiveContextBytes(pair.Value!), await files.ReadFileBytesAsync(pair.Key));
            foreach (var pair in context["mechanicalImages"]!.AsObject())
                Assert.Equal(pair.Value is null ? null : MortalArchiveContextBytes(pair.Value), before[pair.Key]);
            Assert.Equal(MortalArchiveContextBytes(context["readyBytes"]!), await files.ReadFileBytesAsync("ready/turn_complete.json"));
            await AssertMortalArchiveResourceWorkAsync(files, materialize);
            var draft = context["decision"]!.DeepClone().AsObject();
            if (changed)
            {
                if (materialize) draft["proposal"]!["display"]!["description"] = "Изменённое законное описание уже принятого пореза.";
                else { draft["decision"] = "materialize"; draft["woundRef"] = "changed_replay_cut"; draft["proposal"] = CreateMortalComponentProposal(); }
            }
            await files.WriteFileAtomicAsync(MortalWoundOpportunityAdapter.DecisionPath, draft.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            Assert.False(files.FileExists(ResourceMaterializationContract.CommandPath));
            Assert.False(files.FileExists(EffectAcceptedTurnPlan.CommandPath));
            Assert.False(files.FileExists(AcceptedMechanicsPlan.WoundCommandPath));
            await GetPrivateField<StateManager>(run.Engine, "_stateManager").RefreshGameStateAsync();
            GetPrivateField<GameLoop>(run.Engine, "_gameLoop").SetSession(original.SessionId, 42);
            var idleInput = GetPrivateField<IConsoleInputSource>(run.Engine, "_inputSource");
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "qte_durable_child_before_actual_idle", Observation = new
            { ProcessId = Environment.ProcessId, scenario, BorrowedDurableRoot = root, AuxiliaryFactoryRoot = factory._rootPath,
                generation, original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber, original.Realm,
                InputType = idleInput.GetType().FullName, idleInput.KeyAvailable, PlannedRepairCancellation = changed,
                MechanicalBefore = MortalArchiveDigests(before) } }));
            handled = await InvokePrivateAsync<bool>(run.Engine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");
            after = await CaptureMortalArchiveImagesAsync(files, MortalArchiveMechanicalPaths);
            foreach (var pair in before) Assert.Equal(pair.Value, after[pair.Key]); // Changed refusal must not undo an already committed publication.
            Assert.Equal(changed ? new[] { 42 } : new[] { 42, 43 }, await MortalArchiveStoryTurnsAsync(files));
            Assert.Equal(changed ? 42 : 43, GetPrivateField<GameLoop>(run.Engine, "_gameLoop").TurnNumber);
            await AssertMortalArchiveDecisionAsync(files, context["source"]!.AsObject(), materialize, original);
            // Ordinary accepted completion or settled cancellation reaches false; it is not an acceptance proof.
            // Check this only after the strict canonical/story oracles so it cannot mask a destructive rejection.
            Assert.False(handled);
            if (!changed)
            {
                foreach (var pair in context["originalImages"]!.AsObject()) Assert.False(files.FileExists(pair.Key));
                Assert.False(files.FileExists(MortalWoundOpportunityAdapter.DecisionPath));
                Assert.False(files.FileExists(AcceptedMechanicsPlan.WoundCommandPath));
                Assert.False(files.FileExists("ready/turn_complete.json"));
                secondIdle = await InvokePrivateAsync<bool>(run.Engine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");
                foreach (var pair in before) Assert.Equal(pair.Value, await files.ReadFileBytesAsync(pair.Key));
                Assert.Equal(new[] { 42, 43 }, await MortalArchiveStoryTurnsAsync(files));
            }
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            // Capture actual after bytes even when the strict changed-refusal oracle exposes the known outer-rollback risk.
            after ??= await CaptureMortalArchiveImagesAsync(files, MortalArchiveMechanicalPaths);
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new
            {
                processId = Environment.ProcessId, scenario, generation, handled, secondIdle,
                AuxiliaryFactoryRoot = factory._rootPath, InputType = GetPrivateField<IConsoleInputSource>(run.Engine, "_inputSource").GetType().FullName,
                InputKeyAvailableAfter = GetPrivateField<IConsoleInputSource>(run.Engine, "_inputSource").KeyAvailable,
                BinaryPins = MortalArchiveBinaryPins(), DependencyContext = MortalArchiveDependencyContext(),
                ProcessArguments = Environment.GetCommandLineArgs(), LoadedDependencyBindings = loadedDependencyBindings, FreshFactory = true,
                actualTurn = GetPrivateField<GameLoop>(run.Engine, "_gameLoop").TurnNumber, failure = failure?.ToString(),
                Original = original is null ? null : new { original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber },
                Before = MortalArchiveDigests(before), After = MortalArchiveDigests(after),
                Receipt = await files.ReadFileAsync(MortalWoundOpportunityReceiptState.StatePath),
                Marker = await files.ReadFileAsync(MortalQteDecisionPublication.StatePath), Stories = await MortalArchiveStoryTurnsAsync(files)
            }));
        }
    }

    private const string MortalArchivePauseMessage = "Controlled pause after actual N43 common publication before story append.";
    private static readonly string[] MortalArchiveImmutablePaths =
    [
        MortalWoundOccurrenceState.StatePath, MortalWoundOpportunityReceiptState.StatePath, WoundIdentityState.StatePath, WoundHistoryState.HistoryPath,
        WoundCarrierCatalog.PlayerPath, WoundCarrierCatalog.NpcPath, WoundCarrierCatalog.EnemiesPath, WoundCarrierCatalog.AlliesPath,
        EffectAcceptedTurnPlan.IdentityIndexPath, EffectCarrierCatalog.PlayerPath, MortalQteDecisionPublication.StatePath
    ];
    private static readonly string[] MortalArchiveMechanicalPaths = MortalArchiveImmutablePaths.Concat(MortalQteDecisionPublication.QuartetPaths)
        .Concat(new[] { EffectCarrierCatalog.NpcPath, EffectCarrierCatalog.EnemiesPath, EffectCarrierCatalog.AlliesPath,
            EffectCarrierCatalog.AfterlifeProfilesPath, EffectCarrierCatalog.SpiritualConflictPath, ResourcePendingResolutionState.PendingPath })
        .Concat(EffectAcceptedTurnInputComposer.SourceAuthorityPaths).Distinct(StringComparer.Ordinal).ToArray();

    private sealed class MortalArchiveTurn : IDisposable
    {
        internal required GameEngine Engine;
        internal required AgentConsoleLiveInputSource Input;
        internal PendingTurnSnapshotReadAuthority? Snapshot;
        internal JsonObject? Decision;
        internal byte[]? RequestBytes, ReadyBytes;
        internal Exception? Failure;
        internal bool Accepted;
        public void Dispose() => Input.Dispose();
    }

    private MortalArchiveTurn CreateMortalArchiveOrdinaryTurn(FileSystemManager files, bool? materialize, bool resourceWork, bool pauseAfterPublication,
        bool cancelRepair = false)
    {
        var console = new AgentConsoleStateStore();
        AgentConsoleLiveInputSource? live = null;
        var input = live = new AgentConsoleLiveInputSource(console, TimeSpan.FromSeconds(10), AgentConsoleLiveInputSource.DefaultMaxQueueLength,
            new AgentConsoleLiveInputSourceHooks { ActiveReadRegistered = kind =>
            {
                Assert.Equal(AgentConsoleInputKind.Key, kind);
                Assert.True(live!.EnqueueKey(Key(ConsoleKey.Enter)).Accepted);
            }});
        MortalArchiveTurn? run = null;
        IConsoleInputSource engineInput = cancelRepair ? new QueuedConsoleInputSource([Key(ConsoleKey.Escape)]) : input;
        var engine = CreateGameEngine(engineInput, settings => { InertRealmSettings(settings); settings.AutosaveIntervalTurns = 0; },
            fileSystem: files, finalizationHooks: new GameEngineSessionFinalizationHooks { AtCheckpointAsync = Author });
        run = new MortalArchiveTurn { Engine = engine, Input = input };
        return run;

        async Task Author(SessionFinalizationCheckpoint checkpoint)
        {
            if (checkpoint == SessionFinalizationCheckpoint.AcceptedOutcomeValidatedBeforeMaterialization)
            {
                run!.Accepted = true;
                if (pauseAfterPublication) throw new OperationCanceledException(MortalArchivePauseMessage);
                return;
            }
            if (checkpoint != SessionFinalizationCheckpoint.TerminalWaitStarted) return;
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            run!.Snapshot = MortalQteWoundOpportunityProjection.ReadSnapshot(files, lease);
            run.RequestBytes = await files.ReadFileBytesAsync(lease, "input/turn_request.json");
            using var request = JsonDocument.Parse(MortalArchiveText(run.RequestBytes!));
            Assert.Equal(run.Snapshot.RequestId, request.RootElement.GetProperty("requestId").GetString());
            var offered = request.RootElement.GetProperty("woundOpportunities").EnumerateArray().ToArray();
            if (materialize is null) Assert.Empty(offered);
            else
            {
                var row = Assert.Single(offered);
                Assert.True(new HashSet<string>(row.EnumerateObject().Select(p => p.Name)).SetEquals(["opportunityRef", "domain", "maximumSeverity", "safeContext"]));
                Assert.Equal("physical", row.GetProperty("domain").GetString());
                Assert.Equal("II", row.GetProperty("maximumSeverity").GetString());
                Assert.True(new HashSet<string>(row.GetProperty("safeContext").EnumerateObject().Select(p => p.Name)).SetEquals(["target", "cause", "allowedLocationKinds"]));
                run.Decision = new JsonObject { ["opportunityRef"] = row.GetProperty("opportunityRef").GetString(), ["decision"] = materialize.Value ? "materialize" : "none" };
                if (materialize.Value) { run.Decision["woundRef"] = "archive_cut"; run.Decision["proposal"] = CreateMortalComponentProposal(); }
                await files.WriteFileAtomicAsync(lease, MortalWoundOpportunityAdapter.DecisionPath, run.Decision.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            }
            var timestamp = DateTime.UtcNow.ToString("O");
            await files.WriteFileAtomicAsync(lease, "output/narrative_response.json", JsonSerializer.Serialize(new
                { response = materialize == true ? MortalComponentAcquisition : "Герой внимательно осматривает состояние после обвала.", timestamp }));
            await files.WriteFileAtomicAsync(lease, "output/interface_updates.json", JsonSerializer.Serialize(new { dialogueOptions = Array.Empty<object>(), timestamp }));
            await files.WriteFileAtomicAsync(lease, "output/debug_logs.json", JsonSerializer.Serialize(new { timestamp,
                gm_thoughts_markdown = "## NPC Scope\n- Mode: Scene-local\n- Relevant actors: нет\n- Why relevant: Осмотр состояния без участия NPC.\n- Actors outside scope: нет\n- Why outside scope: В сцене нет других акторов.\n\n## Reasoning\n- Only the offered public decision and declared ordinary resource work are authored." }));
            var modified = new List<string> { "output/narrative_response.json", "output/interface_updates.json", "output/debug_logs.json" };
            if (materialize is not null) modified.Add(MortalWoundOpportunityAdapter.DecisionPath);
            if (resourceWork)
            {
                Assert.Equal(43, run.Snapshot.TurnNumber);
                string[] resourcePaths = [ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath,
                    ResourceMaterializationContract.HistoryPath];
                var resourceRead = PendingTurnSnapshotReader.ReadCurrent(files, lease, resourcePaths);
                WritePhysicalInjuryDiagnostic("qte_durable_parent_resource_snapshot", new
                {
                    resourceRead.Success, resourceRead.Issues,
                    Source = resourceRead.Snapshot is null ? null : new
                    { resourceRead.Snapshot.SessionId, resourceRead.Snapshot.RequestId, resourceRead.Snapshot.SnapshotToken,
                        resourceRead.Snapshot.TurnNumber, resourceRead.Snapshot.Realm },
                    SelectedResourceSha256 = resourceRead.Snapshot is null ? null : resourcePaths.ToDictionary(path => path,
                        path => MortalArchiveHash(resourceRead.Snapshot.ReadRequiredBytes(path)), StringComparer.Ordinal)
                });
                Assert.True(resourceRead.Success, JsonSerializer.Serialize(resourceRead.Issues));
                Assert.NotNull(resourceRead.Snapshot);
                var resourceSnapshot = resourceRead.Snapshot!;
                Assert.Equal(run.Snapshot.SessionId, resourceSnapshot.SessionId);
                Assert.Equal(run.Snapshot.RequestId, resourceSnapshot.RequestId);
                Assert.Equal(run.Snapshot.SnapshotToken, resourceSnapshot.SnapshotToken);
                Assert.Equal(run.Snapshot.TurnNumber, resourceSnapshot.TurnNumber);
                Assert.Equal(run.Snapshot.Realm, resourceSnapshot.Realm);
                var originalDefinitions = ResourceDefinitionCatalog.ParseCanonical(MortalArchiveText(resourceSnapshot.ReadRequiredBytes(ResourceMaterializationContract.DefinitionsPath)), false);
                Assert.True(originalDefinitions.IsValid);
                var originalState = ResourceStateContract.ParseCanonical(MortalArchiveText(resourceSnapshot.ReadRequiredBytes(ResourceMaterializationContract.StatePath)), originalDefinitions.Catalog!, false);
                Assert.True(originalState.IsValid);
                Assert.True(originalState.Ledger!.TryResolveExact(new ResourceCoordinate("mortal_world", ResourceOwnerKind.Player, "player_current", "health"), out var originalHealth));
                Assert.Equal(155m, originalHealth!.Current);
                await files.WriteFileAtomicAsync(lease, ResourceMaterializationContract.CommandPath, new JsonObject
                {
                    ["resourceChanges"] = new JsonArray(new JsonObject { ["operation"] = "damage",
                        ["target"] = new JsonObject { ["kind"] = "player", ["targetId"] = "player_current" }, ["resourceKey"] = "health", ["amount"] = 1,
                        ["source"] = new JsonObject { ["kind"] = "narrative_outcome" }, ["eventRef"] = "turn_43:resource:1", ["reason"] = "Осмотр ушиба причиняет небольшую боль." })
                }.ToJsonString());
                modified.Add(ResourceMaterializationContract.CommandPath);
            }
            await files.WriteFileAtomicAsync(lease, "ready/turn_complete.json", JsonSerializer.Serialize(new
                { sessionId = run.Snapshot.SessionId, requestId = run.Snapshot.RequestId, turnNumber = run.Snapshot.TurnNumber,
                    status = "success", timestamp, filesModified = modified }));
            run.ReadyBytes = await files.ReadFileBytesAsync(lease, "ready/turn_complete.json");
        }
    }

    private Task RunMortalArchiveChildAsync(string mode, string root, string output, int scenario, string contextPath)
    {
        var paths = MortalArchiveDependencyPaths();
        var executable = Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet");
        string[] arguments = ["exec", "--runtimeconfig", paths["runtimeconfig"], "--depsfile", paths["depsfile"],
            paths["support"], mode, paths["integration"], root, output, scenario.ToString(), contextPath];
        WritePhysicalInjuryDiagnostic("qte_mortal_child_dependency_launch", new
        {
            mode, Executable = executable, Arguments = arguments, DependencyContext = MortalArchiveDependencyContext()
        });
        return RunBrowserColdChildAsync(executable, arguments, output + ".log", 45);
    }

    private static JsonObject MortalArchiveSource(QteStoryRun source, string occurrenceId, string opportunityRef) => new()
    {
        ["sessionId"] = source.Source!.SessionId, ["requestId"] = source.Source.RequestId, ["snapshotToken"] = source.SnapshotToken,
        ["turn"] = 42, ["occurrenceId"] = occurrenceId, ["opportunityRef"] = opportunityRef
    };
    private static object MortalArchiveRunObservation(MortalArchiveTurn run) => new
    {
        run.Accepted, Failure = run.Failure?.ToString(), Source = run.Snapshot is null ? null : new
        { run.Snapshot.SessionId, run.Snapshot.RequestId, run.Snapshot.SnapshotToken, run.Snapshot.TurnNumber },
        ActualRequest = run.RequestBytes is null ? null : MortalArchiveText(run.RequestBytes),
        ActualReady = run.ReadyBytes is null ? null : MortalArchiveText(run.ReadyBytes), run.Decision
    };
    private static string MortalArchiveText(byte[] bytes) => Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
    private static string MortalArchiveHash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static object MortalArchiveDigests(IReadOnlyDictionary<string, byte[]?> images) => images.ToDictionary(p => p.Key,
        p => new { Exists = p.Value is not null, Length = p.Value?.LongLength, Sha256 = p.Value is null ? null : MortalArchiveHash(p.Value) });
    private static async Task<Dictionary<string, byte[]?>> CaptureMortalArchiveImagesAsync(FileSystemManager files, IEnumerable<string> paths)
    {
        var result = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in paths) result.Add(path, await files.ReadFileBytesAsync(path));
        return result;
    }
    private static JsonNode[] MortalArchiveChildCleanup(string log) => (File.Exists(log) ? File.ReadAllLines(log) : [])
        .Select(line => (Line: line, Start: line.IndexOf("{\"CleanupOwnedRoot\"", StringComparison.Ordinal)))
        .Where(item => item.Start >= 0).Select(item => JsonNode.Parse(item.Line[item.Start..])!).ToArray();
    private static async Task<int[]> MortalArchiveStoryTurnsAsync(FileSystemManager files) => MortalArchiveText(
        (await files.ReadFileBytesAsync(QteStoryPath)) ?? []).Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(row => JsonNode.Parse(row)!["turn"]!.GetValue<int>()).ToArray();
    private static byte[]? MortalArchiveEntryBytes(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path);
        if (entry is null) return null;
        using var stream = entry.Open(); using var bytes = new MemoryStream(); stream.CopyTo(bytes); return bytes.ToArray();
    }
    private static void AssertMortalArchiveExcludedEntries(ZipArchive archive)
    {
        var names = archive.Entries.Select(entry => entry.FullName).ToArray();
        Assert.DoesNotContain(names, path => path.StartsWith("input/", StringComparison.Ordinal) || path.StartsWith("ready/", StringComparison.Ordinal) ||
            path == "game_state/control/pending_turn_snapshot.json" || path.StartsWith("game_state/control/pending_turn_snapshot/", StringComparison.Ordinal) ||
            path == PendingTurnSnapshotAuthority.AuthorityPath || path == MortalWoundOpportunityAdapter.DecisionPath ||
            path == ResourceMaterializationContract.CommandPath || path == EffectAcceptedTurnPlan.CommandPath || path == AcceptedMechanicsPlan.WoundCommandPath);
    }

    private static byte[] MortalArchiveContextBytes(JsonNode node) => Convert.FromBase64String(node.GetValue<string>());
    private static Dictionary<string, string> MortalArchiveDependencyPaths()
    {
        var integration = typeof(GameEngineTurnLifecycleTests).Assembly.Location;
        var directory = Path.GetDirectoryName(integration)!;
        return new(StringComparer.Ordinal)
        {
            ["integration"] = integration, ["support"] = Path.Combine(directory, "BookOfEternityClient.TestSupport.dll"),
            ["runtimeconfig"] = Path.ChangeExtension(integration, ".runtimeconfig.json"),
            ["depsfile"] = Path.ChangeExtension(integration, ".deps.json"),
            ["immutable"] = Path.Combine(directory, "System.Collections.Immutable.dll")
        };
    }
    private static Dictionary<string, string> MortalArchiveBinaryPins() => MortalArchiveDependencyPaths()
        .ToDictionary(pair => pair.Key, pair => MortalArchiveHash(File.ReadAllBytes(pair.Value)), StringComparer.Ordinal);
    private static JsonObject MortalArchiveDependencyContext() => new()
    {
        ["paths"] = JsonSerializer.SerializeToNode(MortalArchiveDependencyPaths()),
        ["sha256"] = JsonSerializer.SerializeToNode(MortalArchiveBinaryPins()),
        ["immutableAssemblyIdentity"] = System.Reflection.AssemblyName.GetAssemblyName(MortalArchiveDependencyPaths()["immutable"]).FullName
    };
    private static object MortalArchiveLoadedDependencyBindings() => AppDomain.CurrentDomain.GetAssemblies()
        .Where(assembly => assembly.GetName().Name is "System.Collections.Immutable" or "BookOfEternityClient.IntegrationTests" or "BookOfEternityClient.TestSupport")
        .OrderBy(assembly => assembly.FullName, StringComparer.Ordinal)
        .Select(assembly => new
        {
            Identity = assembly.FullName, Path = assembly.Location,
            Sha256 = MortalArchiveHash(File.ReadAllBytes(assembly.Location)),
            LoadContext = System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(assembly)?.Name,
            IsDefaultContext = ReferenceEquals(System.Runtime.Loader.AssemblyLoadContext.Default,
                System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(assembly))
        }).ToArray();
    private static void AssertMortalArchiveChildBinaries(JsonObject context)
    {
        foreach (var pair in MortalArchiveBinaryPins()) Assert.Equal(context["binaryPins"]![pair.Key]!.GetValue<string>(), pair.Value);
    }

    private static async Task<ResourceHistoryState> MortalArchiveResourceHistoryAsync(FileSystemManager files)
    {
        var definitions = ResourceDefinitionCatalog.ParseCanonical(await files.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath), false);
        Assert.True(definitions.IsValid);
        var history = ResourceHistoryState.ParseCanonical(await files.ReadFileAsync(ResourceMaterializationContract.HistoryPath), definitions.Catalog!, false);
        Assert.True(history.IsValid);
        return history.History!;
    }
    private static async Task AssertMortalArchiveResourceAgreementAsync(FileSystemManager files)
    {
        var definitions = ResourceDefinitionCatalog.ParseCanonical(await files.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath), false);
        Assert.True(definitions.IsValid);
        var state = ResourceStateContract.ParseCanonical(await files.ReadFileAsync(ResourceMaterializationContract.StatePath), definitions.Catalog!, false);
        Assert.True(state.IsValid);
        var history = await MortalArchiveResourceHistoryAsync(files);
        Assert.Empty(history.ValidateStateAgreement(state.Ledger!));
        var authority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(definitions.Catalog!, path => files.ReadFileAsync(path), state.Ledger!, history,
            CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation);
        Assert.True(authority.IsValid, JsonSerializer.Serialize(authority.Issues));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(authority.CanonicalAuthorityJson!), JsonNode.Parse((await files.ReadFileAsync(CanonicalResourceOwnerAuthorityComposer.AuthorityPath))!)));
    }
    private static async Task AssertMortalArchiveResourceWorkAsync(FileSystemManager files, bool materialize)
    {
        var history = await MortalArchiveResourceHistoryAsync(files);
        var damage = Assert.Single(history.Transitions, row => row.EventRef == "turn_43:resource:1");
        Assert.Equal(ResourceTransitionOutcome.Applied, damage.Outcome);
        Assert.Equal(ResourceTransitionOperation.Damage, damage.Operation);
        Assert.Equal("narrative_outcome", damage.SourceEvidence.SourceKind);
        Assert.Equal(1m, damage.AppliedAmount);
        Assert.Equal(materialize ? 152m : 155m, damage.BeforeState!.Current);
        Assert.Equal(materialize ? 151m : 154m, damage.AfterState!.Current);
        if (materialize)
        {
            var tick = Assert.Single(history.Transitions, row => row.Turn == 43 && row.SourceEvidence.SourceKind == "effect_component");
            Assert.Equal(ResourceTransitionOperation.Damage, tick.Operation);
            Assert.Equal(ResourceTransitionOutcome.Applied, tick.Outcome);
            Assert.Equal(3m, tick.AppliedAmount);
            Assert.Equal(155m, tick.BeforeState!.Current);
            Assert.Equal(152m, tick.AfterState!.Current);
            Assert.True(tick.ExecutionSequence < damage.ExecutionSequence);
        }
        await AssertMortalArchiveResourceAgreementAsync(files);
    }
    private static async Task AssertMortalArchiveDecisionAsync(FileSystemManager files, JsonObject source, bool materialize,
        PendingTurnSnapshotReadAuthority? decisionSource, bool laterOrdinaryTurn = false)
    {
        var pending = MortalWoundOccurrenceState.Parse(await files.ReadFileAsync(MortalWoundOccurrenceState.StatePath), MortalWoundOccurrenceState.StatePath);
        Assert.True(pending.IsValid); Assert.Empty(pending.State!.Occurrences);
        var receipts = MortalWoundOpportunityReceiptState.Parse(await files.ReadFileAsync(MortalWoundOpportunityReceiptState.StatePath), MortalWoundOpportunityReceiptState.StatePath);
        Assert.True(receipts.IsValid); var receipt = Assert.Single(receipts.State!.Receipts);
        Assert.Equal(source["occurrenceId"]!.GetValue<string>(), receipt.OpportunityId);
        Assert.Equal(source["sessionId"]!.GetValue<string>(), receipt.SourceSessionId);
        Assert.Equal(source["requestId"]!.GetValue<string>(), receipt.SourceRequestId);
        Assert.Equal(source["snapshotToken"]!.GetValue<string>(), receipt.SourceSnapshotToken);
        Assert.Equal(42, receipt.SourceTurn);
        Assert.Equal(43, receipt.Turn);
        Assert.Equal(materialize ? "materialize" : "none", receipt.Decision);
        if (decisionSource is not null)
        {
            Assert.Equal(decisionSource.RequestId, receipt.RequestId);
            Assert.Equal(decisionSource.SnapshotToken, receipt.SnapshotToken);
        }
        if (!materialize)
        {
            Assert.Null(receipt.WoundId); Assert.Null(receipt.TransitionId);
            Assert.False(files.FileExists(WoundCarrierCatalog.PlayerPath));
            Assert.False(files.FileExists(WoundIdentityState.StatePath));
            Assert.False(files.FileExists(WoundHistoryState.HistoryPath));
        }
        else
        {
            var player = JsonNode.Parse((await files.ReadFileAsync(WoundCarrierCatalog.PlayerPath))!)!.AsObject();
            var wound = Assert.IsType<JsonObject>(Assert.Single(player["activeWounds"]!.AsArray()));
            var parsed = WoundMaterializationContract.Parse(wound.ToJsonString(), WoundCarrierCatalog.PlayerPath + ".activeWounds[0]");
            Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
            Assert.Equal("I", parsed.Wound!.Severity.Value); Assert.Equal("physical", parsed.Wound.Classification.Domain);
            Assert.Equal("II", parsed.Wound.Severity.MaximumAtCreation);
            Assert.Equal(43, parsed.Wound.Origin.CreatedAtTurn);
            Assert.Equal(source["occurrenceId"]!.GetValue<string>(), parsed.Wound.Origin.OpportunityId);
            Assert.Equal(receipt.WoundId, parsed.Wound.WoundId);
            var identity = WoundIdentityState.Parse(await files.ReadFileAsync(WoundIdentityState.StatePath), WoundIdentityState.StatePath);
            var history = WoundHistoryState.Parse(await files.ReadFileAsync(WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
            Assert.True(identity.IsValid); Assert.True(history.IsValid);
            Assert.Equal(receipt.WoundId, Assert.Single(identity.State!.Entries).WoundId);
            var transition = laterOrdinaryTurn
                ? Assert.Single(history.State!.Transitions, row => row.Kind == "create")
                : Assert.Single(history.State!.Transitions);
            Assert.Equal("create", transition.Kind); Assert.Equal(receipt.WoundId, transition.WoundId); Assert.Equal(receipt.TransitionId, transition.TransitionId);
            var carriers = WoundCarrierCatalog.Build(new(player,
                await MortalArchiveObjectAsync(files, WoundCarrierCatalog.NpcPath), await MortalArchiveObjectAsync(files, WoundCarrierCatalog.EnemiesPath),
                await MortalArchiveObjectAsync(files, WoundCarrierCatalog.AlliesPath), await MortalArchiveObjectAsync(files, WoundCarrierCatalog.AfterlifeProfilesPath)));
            Assert.Empty(carriers.Issues);
            Assert.Empty(history.State.ValidateAgreement(identity.State, carriers));
            Assert.Equal(transition.TransitionId, wound["recovery"]!["recoveryAnchor"]!["anchorTransitionId"]!.GetValue<string>());
            Assert.Equal(transition.TransitionId, wound["recovery"]!["deteriorationAnchor"]!["anchorTransitionId"]!.GetValue<string>());
            var binding = Assert.IsType<JsonObject>(Assert.Single(wound["consequences"]!["ownedEffectSources"]!["rootBindings"]!.AsArray()));
            var effects = JsonNode.Parse((await files.ReadFileAsync(EffectCarrierCatalog.PlayerPath))!)!.AsObject();
            var effect = Assert.Single(effects["activeEffects"]!.AsArray().OfType<JsonObject>(), row => row["effectId"]?.GetValue<string>() == binding["effectId"]!.GetValue<string>());
            Assert.Equal(parsed.Wound.WoundId, effect["source"]!["sourceId"]!.GetValue<string>());
            Assert.Equal("wound", effect["source"]!["kind"]!.GetValue<string>());
            var index = JsonNode.Parse((await files.ReadFileAsync(EffectAcceptedTurnPlan.IdentityIndexPath))!)!.AsObject();
            Assert.Single(index["entries"]!.AsArray().OfType<JsonObject>(), row => row["effectId"]?.GetValue<string>() == binding["effectId"]!.GetValue<string>());
        }
        await AssertMortalArchiveResourceAgreementAsync(files);
    }

    private static async Task<JsonObject?> MortalArchiveObjectAsync(FileSystemManager files, string path)
    {
        var raw = await files.ReadFileAsync(path);
        return raw is null ? null : JsonNode.Parse(raw)!.AsObject();
    }
}
