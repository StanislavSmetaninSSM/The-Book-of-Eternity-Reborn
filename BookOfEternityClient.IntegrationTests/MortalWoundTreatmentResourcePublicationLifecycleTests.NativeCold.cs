using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    private static readonly string[] GuaranteedNativeSnapshotPaths =
    [
        "game_state/meta/soul_state.json",
        ResourceMaterializationContract.DefinitionsPath,
        ResourceMaterializationContract.StatePath,
        ResourceMaterializationContract.HistoryPath,
        CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
        WoundCarrierCatalog.PlayerPath,
        WoundIdentityState.StatePath,
        WoundHistoryState.HistoryPath
    ];

    private static readonly string[] GuaranteedNativeMechanicalPaths =
    [
        .. GuaranteedNativeSnapshotPaths,
        EffectCarrierCatalog.PlayerPath,
        EffectAcceptedTurnPlan.IdentityIndexPath,
        WoundAcceptedTurnSnapshotContract.PendingResolutionPath
    ];

    [Fact]
    public async Task GuaranteedTreatment_NativeSameRootOrdinaryCompletionSpendsConfirmedEnergyOnce()
    {
        // Existing canonical bootstrap is a precondition, not a new QTE creation proof.
        await using var held = await CreateHeldTreatmentPipelineContextAsync(
            fault: null, withRollbackAuthority: true, composePublication: false);
        using var owned = new CleanupOwnedFixture(held.Root, line => _directGachaOutput?.WriteLine(line));
        var files = held.FileSystem;
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(files, held.Lease));
        Assert.Equal("guaranteed", held.Request.Mode);
        Assert.Equal("held", held.Request.ResourceAuthority.ReservationDisposition);
        AssertHeldTreatmentClaims(held.Request, itemScenario: null);
        Assert.Equal(2, await ReadCurrentTreatmentEnergyAsync(held));
        var originalRead = PendingTurnSnapshotReader.ReadCurrent(files, held.Lease, GuaranteedNativeSnapshotPaths);
        Assert.True(originalRead.Success, DescribeValidationIssues(originalRead.Issues));
        var original = Assert.IsType<PendingTurnSnapshotReadAuthority>(originalRead.Snapshot);
        Assert.Equal(42, original.TurnNumber);
        Assert.Equal(held.Request.Coordinates.SessionId, original.SessionId);
        Assert.Equal(held.Request.Coordinates.RequestId, original.RequestId);
        Assert.Equal(held.Request.Coordinates.SnapshotToken, original.SnapshotToken);
        Assert.Equal("mortal_world", original.Realm);
        var generation = files.GetOrCreateSessionGeneration(held.Lease);
        Assert.Equal(held.Request.Coordinates.SessionGeneration, generation);
        var beforeHistory = ReadCurrentTreatmentHistory(files);
        Assert.Single(beforeHistory.State!.Transitions);
        Assert.Equal(0, CountHeldTreatmentEnergySpends(await ReadTreatmentResourceHistoryAsync(files)));
        var storyBefore = await CaptureGuaranteedNativeStoriesAsync(files);
        Assert.DoesNotContain(storyBefore.Values.SelectMany(bytes => MortalArchiveText(bytes!).Split('\n', StringSplitOptions.RemoveEmptyEntries))
            .Select(line => JsonNode.Parse(line)!.AsObject()), story => story["turn"]!.GetValue<int>() == 42);

        await held.ReleaseLeaseAsync();
        await WriteGuaranteedNativeOrdinaryOutputsAsync(files, original);
        var originalImages = await CaptureGuaranteedNativeOriginalAsync(files);
        var command = Assert.IsType<byte[]>(await files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
        var ready = Assert.IsType<byte[]>(await files.ReadFileBytesAsync("ready/turn_complete.json"));
        var context = new JsonObject
        {
            ["parentProcessId"] = Environment.ProcessId, ["generation"] = generation,
            ["sessionId"] = original.SessionId, ["requestId"] = original.RequestId,
            ["snapshotToken"] = original.SnapshotToken, ["turn"] = original.TurnNumber,
            ["realm"] = original.Realm, ["attemptId"] = held.Request.Coordinates.AttemptId,
            ["requestFingerprint"] = held.Request.RequestFingerprint,
            ["commandSha256"] = MortalArchiveHash(command), ["readySha256"] = MortalArchiveHash(ready),
            ["originalDigests"] = JsonSerializer.SerializeToNode(MortalArchiveDigests(originalImages)),
            ["binaryPins"] = JsonSerializer.SerializeToNode(MortalArchiveBinaryPins()),
            ["dependencyContext"] = MortalArchiveDependencyContext()
        };
        var contextPath = Path.Combine(held.Root, "guaranteed-native-context.json");
        var output = Path.Combine(held.Root, "guaranteed-native-result.json");
        await File.WriteAllTextAsync(contextPath, context.ToJsonString());
        WritePhysicalInjuryDiagnostic("fr063_guaranteed_native_parent_held", new
        {
            CanonicalBootstrapPrecondition = true, ParentProcessId = Environment.ProcessId, generation,
            original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber, original.Realm,
            held.Request.Mode, held.Request.RequestFingerprint, held.Request.Coordinates.AttemptId,
            held.Request.ResourceAuthority.ReservationDisposition, held.Request.ResourceAuthority.Claims,
            EnergyBefore = 2, NoPrecomposedPlan = true, LeaseReleased = true,
            RawCommand = MortalArchiveText(command), RawReady = MortalArchiveText(ready),
            Original = MortalArchiveDigests(originalImages), StoryBefore = MortalArchiveDigests(storyBefore), Context = context
        });
        var failure = await Record.ExceptionAsync(() => RunMortalArchiveChildAsync(
            "engine-mortal-guaranteed-treatment-cold", held.Root, output, 0, contextPath));
        // Preserve all child and current-session evidence before any causal assertion or owned cleanup.
        var log = File.Exists(output + ".log") ? await File.ReadAllTextAsync(output + ".log") : null;
        var resultText = File.Exists(output) ? await File.ReadAllTextAsync(output) : null;
        var artifacts = await CaptureGuaranteedNativeArtifactsAsync(files);
        WritePhysicalInjuryDiagnostic("fr063_guaranteed_native_parent_after_child", new
        {
            Failure = failure?.ToString(), FullChildLog = log, ManagedResult = resultText,
            Context = context, Artifacts = artifacts,
            ChildCleanup = MortalArchiveChildCleanup(output + ".log")
        });
        Assert.NotNull(resultText);
        var result = JsonNode.Parse(resultText!)!.AsObject();
        Assert.True(result["prerequisitesValidated"]!.GetValue<bool>(), resultText);
        Assert.True(result["actualCallerEntered"]!.GetValue<bool>(), resultText);
        Assert.Null(failure);
        Assert.Null(result["failure"]);
        Assert.Equal(42, result["actualTurn"]!.GetValue<int>());
        Assert.True(result["accepted"]!.GetValue<bool>());
        await AssertGuaranteedNativePublicationAsync(files, context);
        Assert.False(result["handled"]!.GetValue<bool>());
        Assert.False(result["secondIdle"]!.GetValue<bool>());
        var childCleanup = Assert.Single(MortalArchiveChildCleanup(output + ".log"));
        Assert.True(childCleanup["OwnedFixtureRemoved"]!.GetValue<bool>());
        Assert.Null(childCleanup["CleanupFailure"]);
        Assert.False(Directory.Exists(childCleanup["CleanupOwnedRoot"]!.GetValue<string>()));
    }

    public static async Task WriteMortalGuaranteedTreatmentColdProbeAsync(
        string root, string output, int scenario, string contextPath)
    {
        Assert.Equal(0, scenario);
        var context = JsonNode.Parse(await File.ReadAllTextAsync(contextPath))!.AsObject();
        using var factory = new GameEngineTurnLifecycleTests();
        using var factoryOwned = new CleanupOwnedFixture(factory._rootPath, Console.WriteLine);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var input = new QueuedConsoleInputSource([Key(ConsoleKey.Escape)]);
        var accepted = false;
        var engine = factory.CreateGameEngine(input,
            settings => { InertRealmSettings(settings); settings.AutosaveIntervalTurns = 0; },
            fileSystem: files,
            finalizationHooks: new GameEngineSessionFinalizationHooks
            {
                AtCheckpointAsync = checkpoint =>
                {
                    if (checkpoint == SessionFinalizationCheckpoint.AcceptedOutcomeValidatedBeforeMaterialization)
                        accepted = true;
                    return Task.CompletedTask;
                }
            });
        Exception? failure = null;
        bool prerequisitesValidated = false, actualCallerEntered = false;
        bool? handled = null, secondIdle = null;
        string? generation = null;
        PendingTurnSnapshotReadAuthority? original = null;
        Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "fr063_guaranteed_native_child_entry", Observation = new
        {
            ProcessId = Environment.ProcessId, ParentProcessId = context["parentProcessId"]!.GetValue<int>(),
            BorrowedRoot = root, AuxiliaryFactoryRoot = factory._rootPath, scenario,
            ProcessArguments = Environment.GetCommandLineArgs(), DependencyContext = MortalArchiveDependencyContext(),
            LoadedDependencies = MortalArchiveLoadedDependencyBindings(), InputType = input.GetType().FullName
        } }));
        try
        {
            Assert.NotEqual(Environment.ProcessId, context["parentProcessId"]!.GetValue<int>());
            AssertMortalArchiveChildBinaries(context);
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                generation = files.GetOrCreateSessionGeneration(lease);
                Assert.Equal(context["generation"]!.GetValue<string>(), generation);
                var read = PendingTurnSnapshotReader.ReadCurrent(files, lease, GuaranteedNativeSnapshotPaths);
                Assert.True(read.Success, DescribeValidationIssues(read.Issues));
                original = Assert.IsType<PendingTurnSnapshotReadAuthority>(read.Snapshot);
                Assert.Equal(context["sessionId"]!.GetValue<string>(), original.SessionId);
                Assert.Equal(context["requestId"]!.GetValue<string>(), original.RequestId);
                Assert.Equal(context["snapshotToken"]!.GetValue<string>(), original.SnapshotToken);
                Assert.Equal(42, original.TurnNumber);
                Assert.Equal(context["realm"]!.GetValue<string>(), original.Realm);
                Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(files, lease));
            }
            foreach (var pair in context["originalDigests"]!.AsObject())
            {
                var bytes = await files.ReadFileBytesAsync(pair.Key);
                Assert.Equal(pair.Value!["Exists"]!.GetValue<bool>(), bytes is not null);
                Assert.Equal(pair.Value["Sha256"]?.GetValue<string>(), bytes is null ? null : MortalArchiveHash(bytes));
            }
            Assert.Equal(context["commandSha256"]!.GetValue<string>(), MortalArchiveHash(
                Assert.IsType<byte[]>(await files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath))));
            Assert.Equal(context["readySha256"]!.GetValue<string>(), MortalArchiveHash(
                Assert.IsType<byte[]>(await files.ReadFileBytesAsync("ready/turn_complete.json"))));
            Assert.Equal(2m, await ReadGuaranteedNativeEnergyAsync(files));
            await GetPrivateField<StateManager>(engine, "_stateManager").RefreshGameStateAsync();
            GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(original!.SessionId, 41);
            prerequisitesValidated = true;
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "fr063_guaranteed_native_before_actual_caller", Observation = new
            {
                ProcessId = Environment.ProcessId, generation, original.SessionId, original.RequestId,
                original.SnapshotToken, original.TurnNumber, original.Realm, PreviousTurn = 41,
                FreshCacheEmpty = true, input.KeyAvailable, Artifacts = await CaptureGuaranteedNativeArtifactsAsync(files)
            } }));
            // No ExportCurrent, RestorePersistedTreatmentRequests, reducer or Compose call occurs before entry.
            actualCallerEntered = true;
            handled = await InvokePrivateAsync<bool>(engine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "fr063_guaranteed_native_after_actual_caller", Observation = new
            {
                accepted, handled, ActualTurn = GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber,
                Artifacts = await CaptureGuaranteedNativeArtifactsAsync(files)
            } }));
            Assert.True(GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber == 42,
                "The actual native ordinary caller did not complete the confirmed held guaranteed treatment turn42.");
            Assert.True(accepted);
            await AssertGuaranteedNativePublicationAsync(files, context);
            Assert.False(handled);
            var committed = await CaptureMortalArchiveImagesAsync(files, GuaranteedNativeMechanicalPaths);
            var stories = await CaptureGuaranteedNativeStoriesAsync(files);
            secondIdle = await InvokePrivateAsync<bool>(engine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");
            Assert.False(secondIdle);
            foreach (var pair in committed) Assert.Equal(pair.Value, await files.ReadFileBytesAsync(pair.Key));
            foreach (var pair in stories) Assert.Equal(pair.Value, await files.ReadFileBytesAsync(pair.Key));
            Assert.Equal(42, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new
            {
                processId = Environment.ProcessId, generation, scenario, prerequisitesValidated, actualCallerEntered,
                accepted, handled, secondIdle, actualTurn = GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber,
                failure = failure?.ToString(), AuxiliaryFactoryRoot = factory._rootPath,
                Source = original is null ? null : new { original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber, original.Realm },
                BinaryPins = MortalArchiveBinaryPins(), LoadedDependencies = MortalArchiveLoadedDependencyBindings(),
                Artifacts = await CaptureGuaranteedNativeArtifactsAsync(files)
            }));
        }
    }

    private static async Task WriteGuaranteedNativeOrdinaryOutputsAsync(
        FileSystemManager files, PendingTurnSnapshotReadAuthority original)
    {
        var timestamp = DateTime.UtcNow.ToString("O");
        await files.WriteFileAtomicAsync("output/narrative_response.json", JsonSerializer.Serialize(new
            { response = HeldTreatmentPipelineContext.FinalSceneText, timestamp }));
        await files.WriteFileAtomicAsync("output/interface_updates.json", JsonSerializer.Serialize(new
            { dialogueOptions = Array.Empty<object>(), timestamp }));
        await files.WriteFileAtomicAsync("output/debug_logs.json", JsonSerializer.Serialize(new
        {
            timestamp, gm_thoughts_markdown = "## NPC Scope\n- Mode: Scene-local\n- Relevant actors: нет\n" +
                "- Why relevant: Завершение уже запечатанного клиентом лечения без нового решения NPC.\n" +
                "- Actors outside scope: Field medic\n- Why outside scope: Нет нового взаимодействия, реплик или изменений NPC.\n\n" +
                "## Reasoning\n- Only the existing client-sealed guaranteed treatment is completed; no GM mechanical delta is authored."
        }));
        string[] modified = ["output/narrative_response.json", "output/interface_updates.json", "output/debug_logs.json"];
        await files.WriteFileAtomicAsync("ready/turn_complete.json", JsonSerializer.Serialize(new
        {
            sessionId = original.SessionId, requestId = original.RequestId, turnNumber = original.TurnNumber,
            status = "success", timestamp, filesModified = modified
        }));
        using var ready = JsonDocument.Parse(MortalArchiveText(Assert.IsType<byte[]>(
            await files.ReadFileBytesAsync("ready/turn_complete.json"))));
        Assert.Equal(original.RequestId, ready.RootElement.GetProperty("requestId").GetString());
        Assert.Equal(original.SessionId, ready.RootElement.GetProperty("sessionId").GetString());
        Assert.Equal(42, ready.RootElement.GetProperty("turnNumber").GetInt32());
        Assert.Equal("success", ready.RootElement.GetProperty("status").GetString());
        Assert.Equal(modified, ready.RootElement.GetProperty("filesModified").EnumerateArray().Select(value => value.GetString()).ToArray());
        foreach (var path in modified) Assert.NotNull(await files.ReadFileBytesAsync(path));
    }

    private static async Task AssertGuaranteedNativePublicationAsync(FileSystemManager files, JsonObject expected)
    {
        Assert.Equal(0m, await ReadGuaranteedNativeEnergyAsync(files));
        var resources = await ReadTreatmentResourceHistoryAsync(files);
        Assert.Equal(1, CountHeldTreatmentEnergySpends(resources));
        var spend = Assert.Single(resources.Transitions, row => row.Operation == ResourceTransitionOperation.Spend &&
            row.Coordinate.ResourceKey == "energy" && row.Coordinate.ResourceOwnerId == "player_current");
        Assert.Equal(2m, spend.AppliedAmount); Assert.Equal(2m, spend.BeforeState!.Current); Assert.Equal(0m, spend.AfterState!.Current);
        Assert.Equal(42, spend.Turn); Assert.Equal(ResourceTransitionOutcome.Applied, spend.Outcome);
        var history = ReadCurrentTreatmentHistory(files);
        Assert.Equal(2, history.State!.Transitions.Count);
        var treatment = Assert.Single(history.State.Transitions, row => row.Kind == "treat");
        Assert.Equal(HeldTreatmentPipelineContext.OperationKey, treatment.OperationKey);
        Assert.Equal(42, treatment.Turn); Assert.Equal(HeldTreatmentPipelineContext.WoundId, treatment.WoundId);
        var result = Assert.IsType<MortalWoundTreatmentPersistedResult>(treatment.TreatmentResult);
        Assert.Equal("guaranteed", result.Request.Mode);
        Assert.Equal(expected["requestFingerprint"]!.GetValue<string>(), result.Request.RequestFingerprint);
        Assert.Equal(expected["attemptId"]!.GetValue<string>(), result.Request.Coordinates.AttemptId);
        Assert.Equal(expected["sessionId"]!.GetValue<string>(), result.Receipt.Coordinates.SessionId);
        Assert.Equal(expected["requestId"]!.GetValue<string>(), result.Receipt.Coordinates.RequestId);
        Assert.Equal(expected["snapshotToken"]!.GetValue<string>(), result.Receipt.Coordinates.SnapshotToken);
        Assert.Equal("success", result.Receipt.ResultCategory);
        Assert.Contains(result.Receipt.DeclaredResult, operation => operation.Kind == "stabilize");
        JsonElement? pending = files.FileExists(WoundAcceptedTurnSnapshotContract.PendingResolutionPath)
            ? JsonSerializer.SerializeToElement(JsonNode.Parse((await files.ReadFileAsync(WoundAcceptedTurnSnapshotContract.PendingResolutionPath))!)) : null;
        var catalog = MortalWoundTreatmentPersistedRequestCatalog.Parse(null, pending, history);
        Assert.True(catalog.IsValid, DescribeValidationIssues(catalog.Issues));
        Assert.Empty(catalog.HeldRequests);
        Assert.Equal(result.Request.RequestFingerprint, Assert.Single(catalog.FinalizedRequests).RequestFingerprint);
        var wound = ReadCurrentTreatmentWound(files);
        Assert.Equal(HeldTreatmentPipelineContext.WoundId, wound.WoundId);
        Assert.Equal("stabilized", wound.Care.State);
        Assert.Equal(42, wound.Care.StabilizedAtTurn);
        Assert.Equal(result.Request.Coordinates.AttemptId, wound.Care.LastAttemptId);
        var stories = await CaptureGuaranteedNativeStoriesAsync(files);
        var row = Assert.Single(stories.Values.SelectMany(bytes => MortalArchiveText(bytes!).Split('\n', StringSplitOptions.RemoveEmptyEntries))
            .Select(line => JsonNode.Parse(line)!.AsObject()), story => story["turn"]!.GetValue<int>() == 42);
        Assert.Equal(HeldTreatmentPipelineContext.FinalSceneText, row["narrative"]!.GetValue<string>());
        Assert.False(files.FileExists(AcceptedMechanicsPlan.WoundCommandPath));
        Assert.False(files.FileExists("ready/turn_complete.json"));
        Assert.False(files.FileExists("input/turn_request.json"));
        Assert.False(files.FileExists(LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
    }

    private static async Task<decimal> ReadGuaranteedNativeEnergyAsync(FileSystemManager files)
    {
        var definitions = ResourceDefinitionCatalog.ParseCanonical(await files.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath), false);
        Assert.True(definitions.IsValid, DescribeValidationIssues(definitions.Issues));
        var state = ResourceStateContract.ParseCanonical(await files.ReadFileAsync(ResourceMaterializationContract.StatePath), definitions.Catalog!, false);
        Assert.True(state.IsValid, DescribeValidationIssues(state.Issues));
        Assert.True(state.Ledger!.TryResolveExact(new ResourceCoordinate("mortal_world", ResourceOwnerKind.Player, "player_current", "energy"), out var energy));
        return Assert.IsType<ResourceStateEntry>(energy).Current;
    }

    private static Task<Dictionary<string, byte[]?>> CaptureGuaranteedNativeStoriesAsync(FileSystemManager files) =>
        CaptureMortalArchiveImagesAsync(files, Directory.GetFiles(files.ResolvePath("stories"), "*.jsonl")
            .Select(path => Path.GetRelativePath(files.GameSessionPath, path).Replace('\\', '/')));

    private static Task<Dictionary<string, byte[]?>> CaptureGuaranteedNativeOriginalAsync(FileSystemManager files) =>
        CaptureMortalArchiveImagesAsync(files, Directory.GetFiles(files.GameSessionPath, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(files.GameSessionPath, path).Replace('\\', '/'))
            .Where(path => path.StartsWith("input/", StringComparison.Ordinal) || path.Contains(".rollback.", StringComparison.Ordinal) ||
                path.StartsWith("game_state/control/pending_turn_snapshot", StringComparison.Ordinal)));

    private static async Task<Dictionary<string, string?>> CaptureGuaranteedNativeArtifactsAsync(FileSystemManager files)
    {
        var paths = GuaranteedNativeMechanicalPaths.Concat(new[]
        {
            AcceptedMechanicsPlan.WoundCommandPath, ResourceMaterializationContract.CommandPath, EffectAcceptedTurnPlan.CommandPath,
            "input/turn_request.json", "ready/turn_complete.json", "ready/turn_error.json", "output/narrative_response.json",
            "output/interface_updates.json", "output/debug_logs.json", "game_state/control/validation_repair_request.json",
            LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath
        }).Concat((await CaptureGuaranteedNativeOriginalAsync(files)).Keys).Concat((await CaptureGuaranteedNativeStoriesAsync(files)).Keys)
            .Distinct(StringComparer.Ordinal);
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var bytes = await files.ReadFileBytesAsync(path);
            result.Add(path, bytes is null ? null : Convert.ToBase64String(bytes));
        }
        return result;
    }
}
