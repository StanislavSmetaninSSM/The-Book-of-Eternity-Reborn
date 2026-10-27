using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

/// <summary>The confirmed public command reaches the actual ordinary GameEngine caller.</summary>
public sealed class ExplorerWoundSelfTreatmentTests(ITestOutputHelper output)
{
    private const string Owner = "wound-guided-client";

    [Fact]
    public async Task HandledGraphRepairPacketNeedsNoPhysicalLeaseWhileMainHoldsIt()
    {
        const string path = "game_state/control/validation_repair_request.json";
        var root = Path.Combine(Path.GetTempPath(), "boe-graph-repair-observer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var armed = false;
        var unexpectedOpens = 0;
        try
        {
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    BeforeCanonicalWriteLockOpenAsync = () =>
                    {
                        if (armed)
                        {
                            Interlocked.Increment(ref unexpectedOpens);
                            throw new InvalidOperationException("An unchanged or refused repair observation must not open another physical lease.");
                        }
                        return Task.CompletedTask;
                    }
                });
            files.EnsureDirectoryStructure();
            await SessionOperationContext.RunParticipatingCurrentSessionAsync(files, async () =>
            {
                await files.WriteFileAtomicAsync(path, "{\"revalidationAttempt\":1}");
                var firstCalls = 0;
                var handled = await ObserveGraphRepairPacketAsync(files, path, null, (lease, raw) =>
                {
                    files.EnsureCanonicalWriteLeaseActive(lease);
                    Assert.Equal("{\"revalidationAttempt\":1}", CanonicalJsonUtf8.DecodeOneOptionalBom(raw));
                    firstCalls++;
                    return Task.CompletedTask;
                }, CancellationToken.None);
                Assert.NotNull(handled);
                Assert.Equal(1, firstCalls);
                var begin = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                // Capture actual main participation before the held physical lease exists.
                var observer = Task.Run(async () =>
                {
                    await begin.Task;
                    return await ObserveGraphRepairPacketAsync(files, path, handled,
                        (_, _) => throw new InvalidOperationException("An unchanged packet was processed again."), CancellationToken.None);
                });
                try
                {
                    await using var heldMainLease = await files.AcquireCanonicalWriteLeaseAsync();
                    armed = true;
                    begin.SetResult(true);
                    Assert.Equal(handled, await observer);
                    Assert.Equal(0, unexpectedOpens);
                }
                finally
                {
                    armed = false;
                    begin.TrySetCanceled();
                    if (!observer.IsCompleted)
                    {
                        try { await observer; }
                        catch (OperationCanceledException) { }
                    }
                }
                files.DeleteFile(path);
                await files.WriteFileAtomicAsync(path, "{\"revalidationAttempt\":2,\"actual\":\"second_issue_body\"}");
                armed = true;
                try
                {
                    var failure = await Assert.ThrowsAsync<InvalidDataException>(() => ObserveGraphRepairPacketAsync(files,
                        path, handled, (_, _) => throw new InvalidOperationException("A second packet was processed."), CancellationToken.None));
                    Assert.Contains("second_issue_body", failure.Message);
                    Assert.Equal(0, unexpectedOpens);
                }
                finally { armed = false; }
            });
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ActualGraphBearingSelfTreatmentRepairsGraphsWithoutChangingItsAttempt()
    {
        const string repairPath = "game_state/control/validation_repair_request.json";
        const string readyPath = "game_state/control/validation_repair_ready.json";
        var diagnostics = new GameEngineTurnLifecycleTests.SpiritualLifecycleTestLogger();
        var input = new RepairPlayerInput(diagnostics);
        var repairCount = 0;
        await using var fixture = await HealingFixture.CreateAsync(3, 5, 20, stageRequest: false);
        var original = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        var console = new TestExplorerConsole();
        console.QueueAnySelection("1. " + WoundPlayerProjection.Text(original.Display.Name) + " — III");
        console.QueueAnyConfirmResponse(true);
        TurnRequest? turn = null;
        JsonObject? retainedRequest = null;
        Dictionary<string, byte[]>? retainedSources = null;
        Dictionary<string, byte[]?>? unpublished = null;
        JsonArray? authoredGraphs = null;
        var responseWritten = false;
        var repaired = false;
        using var stop = new CancellationTokenSource();
        Task? gmRepair = null;
        var runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(fixture.Files,
            inputSource: input, logger: diagnostics, explorerConsole: console,
            finalizationHooks: new GameEngineSessionFinalizationHooks
            {
                AtCheckpointAsync = async checkpoint =>
                {
                    if (checkpoint == SessionFinalizationCheckpoint.AcceptedOutcomeValidatedBeforeMaterialization)
                    {
                        // Join while the same actual console main participation is still alive.
                        stop.Cancel();
                        if (gmRepair is not null) await gmRepair;
                        return;
                    }
                    if (checkpoint != SessionFinalizationCheckpoint.TerminalWaitStarted || responseWritten) return;
                    await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
                    {
                        turn = JsonSerializer.Deserialize<TurnRequest>((await fixture.Files.ReadFileAsync(lease, "input/turn_request.json"))!,
                            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                        var healing = Assert.IsType<JsonObject>(turn.SpiritualHealing);
                        Assert.Equal(5, healing["healingTier"]!.GetValue<int>());
                        Assert.NotEmpty(healing["destinationGraphs"]!.AsArray());
                        retainedSources = new(StringComparer.Ordinal);
                        foreach (var path in new[] { "input/turn_request.json", LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                                     PendingTurnSnapshotAuthority.AuthorityPath, AcceptedMechanicsPlan.WoundCommandPath })
                            retainedSources.Add(path, (await fixture.Files.ReadFileBytesAsync(lease, path))!);
                        retainedRequest = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(
                            retainedSources[AcceptedMechanicsPlan.WoundCommandPath]))!.AsObject();
                        Assert.Null(retainedRequest["snapshotToken"]);
                        Assert.Single(retainedRequest["attempt"]!["roll"]!["values"]!.AsArray());
                        Assert.True(JsonNode.DeepEquals(retainedRequest["attempt"]!["roll"], healing["roll"]));
                        // The controlled GM first omits required authored graphs; it never changes the client draw.
                        await WriteNativeCompletionAsync(fixture.Files, lease, turn);
                        unpublished = fixture.CaptureMutationRoots();
                        responseWritten = true;
                    }
                    gmRepair = Task.Run(RunGmRepairAsync);
                }
            });
        async Task RunGmRepairAsync()
        {
            try
            {
                byte[]? handled = null;
                while (!stop.IsCancellationRequested)
                {
                    handled = await ObserveGraphRepairPacketAsync(fixture.Files, repairPath, handled, async (lease, raw) =>
                    {
                        Assert.Equal(1, Interlocked.Increment(ref repairCount));
                        var repair = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(raw))!.AsObject();
                        Assert.NotNull(turn);
                        Assert.Equal(turn!.SessionId, repair["sessionId"]!.GetValue<string>());
                        Assert.Equal(turn.RequestId, repair["requestId"]!.GetValue<string>());
                        Assert.Equal(turn.TurnNumber, repair["turnNumber"]!.GetValue<int>());
                        var issue = Assert.Single(repair["errors"]!.AsArray());
                        Assert.Equal("spiritual_self_treatment_admission_invalid", issue!["code"]!.GetValue<string>());
                        Assert.Contains("GM-authored spiritual destination graphs are required", issue["actual"]!.GetValue<string>());
                        Assert.False(repair["fullTurnResubmissionRequired"]!.GetValue<bool>());
                        foreach (var pair in retainedSources!)
                            Assert.Equal(pair.Value, await fixture.Files.ReadFileBytesAsync(lease, pair.Key));
                        fixture.AssertMutationRootsUnchanged(unpublished!);
                        authoredGraphs = AuthorRequestedGraphs(original, turn.SpiritualHealing!["destinationGraphs"]!.AsArray());
                        await fixture.Files.WriteFileAtomicAsync(lease, "output/interface_updates.json", new JsonObject
                        {
                            ["dialogueOptions"] = new JsonArray(), ["spiritualHealingGraphs"] = authoredGraphs.DeepClone(),
                            ["timestamp"] = DateTime.UtcNow.ToString("o")
                        }.ToJsonString());
                        await fixture.Files.WriteFileAtomicAsync(lease, readyPath, JsonSerializer.Serialize(new
                        {
                            sessionId = turn.SessionId, requestId = turn.RequestId, turnNumber = turn.TurnNumber,
                            updatedAtUtc = DateTime.UtcNow.ToString("o"), note = "Добавлены только запрошенные ослабленные последствия."
                        }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                        repaired = true;
                    }, stop.Token);
                    await Task.Delay(500, stop.Token);
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            catch
            {
                input.RequestEscape(); // Let the actual repair wait finish its owned cleanup before surfacing the fixture fault.
                throw;
            }
        }
        try
        {
            runtime.GameLoop.SetSession("session_graph_repair_spiritual_healing", 42);
            await runtime.StateManager.RefreshGameStateAsync();
            var action = await runtime.Explorer.TryProcessCommand("/лечить");
            Assert.Equal(WoundApplicationService.ActionText, action);
            await runtime.Engine.ProcessPlayerTurn(action!);
            Assert.True(responseWritten, "The graph-bearing caller did not reach its controlled GM response.");
            Assert.NotNull(gmRepair);
            stop.Cancel();
            await gmRepair;
            Assert.True(repaired);
            Assert.Equal(1, repairCount);
            var history = await fixture.ReadHistoryAsync();
            var attempt = Assert.Single(history.Transitions, t => t.AttemptId is not null);
            var result = Assert.IsType<SpiritualHealingPersistedResult>(attempt.TransitionResult);
            Assert.True(JsonNode.DeepEquals(retainedRequest, result.ToCanonicalJson()["request"]));
            var last = result.Stages[^1].After;
            var wounds = (await fixture.ReadWoundsAsync()).Occurrences;
            if (last.Lifecycle == "healed")
            {
                Assert.Empty(wounds);
                Assert.Single(history.Transitions, t => t.Terminal);
            }
            else
            {
                Assert.Equal(WoundMaterializationContract.SerializeCanonical(last),
                    WoundMaterializationContract.SerializeCanonical(Assert.Single(wounds).Wound));
                Assert.DoesNotContain(history.Transitions, t => t.Terminal);
            }
            foreach (var graph in authoredGraphs!)
            {
                var rank = turn!.SpiritualHealing!["destinationGraphs"]!.AsArray().Single(g =>
                    g!["stageRef"]!.GetValue<string>() == graph!["stageRef"]!.GetValue<string>())!["severityRank"]!.GetValue<int>();
                var stage = result.Stages.First(s => s.After.Severity.Rank == rank);
                Assert.All(stage.After.Consequences.Entries, e => Assert.Equal("Последствие ослаблено Мастером для этой попытки.", e.ReadableSummary));
                if (rank == 2)
                {
                    Assert.Contains(stage.After.Consequences.Entries, e => e.ProfileKey == "spiritual_action_cost_burden");
                    var cost = stage.After.Consequences.OwnedEffectSources.Definitions
                        .SelectMany(d => d.GetProperty("components").EnumerateArray())
                        .Single(c => c.GetProperty("profile").GetString() == "spiritual_action_cost_burden");
                    Assert.Equal(1m, cost.GetProperty("payload").GetProperty("magnitude").GetDecimal());
                }
            }
            var schedule = JsonNode.Parse((await fixture.Files.ReadFileAsync(ProgressionScheduleService.SchedulePath))!)!;
            Assert.Equal(1, turn!.ProgressionControl!.ChaosSeaCyclesExpectedThisTurn);
            Assert.Equal(turn.ProgressionControl.NextChaosSeaTurnOrdinal, schedule["currentChaosSeaTurnOrdinal"]!.GetValue<int>());
            foreach (var path in new[] { repairPath, readyPath, AcceptedMechanicsPlan.WoundCommandPath, ProgressionScheduleService.ReportPath })
                Assert.Null(await fixture.Files.ReadFileBytesAsync(path));
            Assert.Null(await fixture.Files.ReadFileBytesAsync(PendingPlayerActionService.PendingPath));
            fixture.AssertResourcesUnchanged();
        }
        finally
        {
            stop.Cancel();
            try { if (gmRepair is not null) await gmRepair; }
            finally
            {
                output.WriteLine(diagnostics.Describe());
                await runtime.AudioService.DisposeAsync();
            }
        }
    }

    private async Task<byte[]?> ObserveGraphRepairPacketAsync(FileSystemManager files, string path, byte[]? handled,
        Func<FileSystemManager.CanonicalWriteLease, byte[], Task> processFirst, CancellationToken stop)
    {
        // Observation-only file-GM signal: these bytes can skip work or refuse,
        // but only the fresh leased reread below permits first-response authoring.
        byte[] observed;
        try
        {
            await using var stream = new FileStream(files.ResolvePath(path), FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, stop);
            observed = buffer.ToArray();
        }
        catch (FileNotFoundException) { return handled; }
        if (handled is not null && observed.AsSpan().SequenceEqual(handled)) return handled;
        output.WriteLine("Observed graph repair packet:\n" + CanonicalJsonUtf8.DecodeOneOptionalBom(observed));
        if (handled is not null)
            throw new InvalidDataException("Unexpected further graph repair packet:\n" + CanonicalJsonUtf8.DecodeOneOptionalBom(observed));
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync(cancellationToken: stop);
        var raw = await files.ReadFileBytesAsync(lease, path);
        if (raw is null || !raw.AsSpan().SequenceEqual(observed))
            throw new InvalidDataException("The first graph repair packet changed before its actual leased admission.");
        await processFirst(lease, raw);
        return raw;
    }

    private static JsonArray AuthorRequestedGraphs(WoundMaterializationEnvelope original, JsonArray requests)
    {
        var graphs = new JsonArray();
        foreach (var request in requests)
        {
            var rank = request!["severityRank"]!.GetValue<int>();
            Assert.InRange(rank, 1, 2);
            var definitions = new JsonArray();
            var profiles = rank == 2
                ? new[] { "spiritual_roll_hindrance", "spiritual_action_cost_burden" }
                : new[] { "spiritual_roll_hindrance" };
            foreach (var profile in profiles)
            {
                var rootId = original.Consequences.Entries.First(e => e.ProfileKey == profile).EffectId;
                var binding = original.Consequences.OwnedEffectSources.RootBindings.Single(b => b.EffectId == rootId);
                var definition = JsonNode.Parse(original.Consequences.OwnedEffectSources.Definitions.Single(d =>
                    d.GetProperty("definitionKey").GetString() == binding.DefinitionKey).GetRawText())!.AsObject();
                definition["links"] = new JsonArray();
                if (profile == "spiritual_action_cost_burden") definition["components"]![0]!["payload"]!["magnitude"] = 1;
                definitions.Add(new JsonObject
                {
                    ["definitionRef"] = binding.DefinitionKey, ["definition"] = definition,
                    ["root"] = new JsonObject
                    {
                        ["ownership"] = new JsonObject { ["kind"] = "base_wound", ["complicationRef"] = null },
                        ["slots"] = new JsonArray(new JsonObject { ["profileKey"] = profile,
                            ["readableSummary"] = "Последствие ослаблено Мастером для этой попытки." })
                    }
                });
            }
            graphs.Add(new JsonObject { ["stageRef"] = request["stageRef"]!.DeepClone(), ["consequenceDefinitions"] = definitions });
        }
        return graphs;
    }

    [Fact]
    public async Task ConsoleCancellationAndConfirmationUseTheSameExplorerAndActualGameEngine()
    {
        await using var fixture = await HealingFixture.CreateAsync(1, 1, 20, stageRequest: false);
        var wound = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        var console = new TestExplorerConsole();
        console.QueueAnySelection("Отмена", "1. " + WoundPlayerProjection.Text(wound.Display.Name) + " — I");
        console.QueueAnyConfirmResponse(true);
        JsonObject? retainedRequest = null;
        TurnRequest? turn = null;
        var responseWritten = false;
        var diagnostics = new GameEngineTurnLifecycleTests.SpiritualLifecycleTestLogger();
        var runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(fixture.Files,
            inputSource: new NoPlayerInput(diagnostics), logger: diagnostics, explorerConsole: console,
            finalizationHooks: new GameEngineSessionFinalizationHooks
            {
                AtCheckpointAsync = async checkpoint =>
                {
                    if (checkpoint != SessionFinalizationCheckpoint.TerminalWaitStarted || responseWritten) return;
                    await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
                    Assert.Null(await PendingPlayerActionService.ReadAsync(fixture.Files, lease));
                    turn = JsonSerializer.Deserialize<TurnRequest>((await fixture.Files.ReadFileAsync(lease, "input/turn_request.json"))!,
                        SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                    var projected = JsonSerializer.SerializeToNode(turn, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                    Assert.Empty(projected["spiritualHealing"]!["destinationGraphs"]!.AsArray());
                    var bytes = (await fixture.Files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!;
                    retainedRequest = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes))!.AsObject();
                    Assert.Equal("spiritual_self_treatment_request", retainedRequest["kind"]!.GetValue<string>());
                    Assert.Null(retainedRequest["snapshotToken"]);
                    Assert.Single(retainedRequest["attempt"]!["roll"]!["values"]!.AsArray());
                    await WriteNativeCompletionAsync(fixture.Files, lease, turn);
                    responseWritten = true;
                }
            });
        try
        {
            runtime.GameLoop.SetSession("session_console_spiritual_healing", 42);
            await runtime.StateManager.RefreshGameStateAsync();
            var before = fixture.CaptureMutationRoots();
            Assert.Equal(string.Empty, await runtime.Explorer.TryProcessCommand("/лечить"));
            fixture.AssertMutationRootsUnchanged(before);
            Assert.Empty(console.ConfirmPrompts);
            Assert.Null(await fixture.Files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
            Assert.Null(await fixture.Files.ReadFileBytesAsync(PendingPlayerActionService.PendingPath));
            await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
                Assert.Null(runtime.Explorer.ConsumePendingSpiritualSelfTreatment(WoundApplicationService.ActionText,
                    fixture.Files.GetOrCreateSessionGeneration(lease)));

            var action = await runtime.Explorer.TryProcessCommand("/лечить");
            Assert.Equal(WoundApplicationService.ActionText, action);
            Assert.Single(console.ConfirmPrompts);
            fixture.AssertMutationRootsUnchanged(before);
            Assert.Null(await fixture.Files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
            Assert.Null(await fixture.Files.ReadFileBytesAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
            Assert.Null(await fixture.Files.ReadFileBytesAsync(PendingPlayerActionService.PendingPath));
            await runtime.Engine.ProcessPlayerTurn(action!);
            Assert.True(responseWritten, "The console action did not reach the native-shaped GM fixture.");
            Assert.Empty((await fixture.ReadWoundsAsync()).Occurrences);
            var history = await fixture.ReadHistoryAsync();
            Assert.Single(history.Transitions, t => t.Terminal);
            var result = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single(history.Transitions, t => t.AttemptId is not null).TransitionResult);
            Assert.True(JsonNode.DeepEquals(retainedRequest, result.ToCanonicalJson()["request"]));
            var schedule = JsonNode.Parse((await fixture.Files.ReadFileAsync(ProgressionScheduleService.SchedulePath))!)!;
            Assert.Equal(turn!.ProgressionControl!.NextChaosSeaTurnOrdinal, schedule["currentChaosSeaTurnOrdinal"]!.GetValue<int>());
            Assert.Null(await fixture.Files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
            Assert.Null(await fixture.Files.ReadFileBytesAsync(ProgressionScheduleService.ReportPath));
            Assert.Null(await fixture.Files.ReadFileBytesAsync(PendingPlayerActionService.PendingPath));
            fixture.AssertResourcesUnchanged();
            await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
                Assert.Null(runtime.Explorer.ConsumePendingSpiritualSelfTreatment(action!, fixture.Files.GetOrCreateSessionGeneration(lease)));
            var settled = fixture.CaptureMutationRoots();
            Assert.Equal(string.Empty, await runtime.Explorer.TryProcessCommand("/лечить"));
            fixture.AssertMutationRootsUnchanged(settled);
            Assert.Single(console.ConfirmPrompts);
        }
        finally
        {
            output.WriteLine(diagnostics.Describe());
            await runtime.AudioService.DisposeAsync();
        }
    }

    [Fact]
    public async Task ConfirmedBrowserSelfTreatmentStagesAndCompletesThroughActualGameEngine()
    {
        await using var fixture = await HealingFixture.CreateAsync(1, 1, 20, stageRequest: false);
        var wound = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        var service = CreateCommands(fixture.Files);
        var before = fixture.CaptureMutationRoots();
        var opened = await service.ExecuteAsync(new("/лечить", Owner));
        Assert.Equal(CommandExecutionState.RequiresInput, opened.State);
        var selection = Assert.IsType<UiSelectionPrompt>(Assert.Single(opened.Prompts, p => p.Id == "wound_selection"));
        var option = Assert.Single(selection.Options, o => !o.Disabled);
        Assert.NotNull(opened.InteractiveSession);
        Assert.True(opened.InteractiveSession!.RequiresLocalUiLock);
        Assert.DoesNotContain(wound.WoundId, JsonSerializer.Serialize(opened));
        fixture.AssertMutationRootsUnchanged(before);
        var submitted = await service.SubmitPromptSessionAsync(new(opened.InteractiveSession.SessionId,
            new() { ["wound_selection"] = JsonValue.Create(option.Value), ["confirm_wound_treatment"] = JsonValue.Create(true) }, Owner));
        Assert.DoesNotContain(submitted.Notifications, n => n.Severity == UiNotificationSeverity.Error);
        fixture.AssertMutationRootsUnchanged(before);

        PendingPlayerActionService.Binding binding;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            var queued = Assert.IsType<PendingPlayerActionService.State>(await PendingPlayerActionService.ReadAsync(fixture.Files, lease));
            Assert.Equal("queued", queued.Phase);
            binding = queued.Binding;
            var queuedRoot = JsonNode.Parse(queued.Json)!;
            Assert.Equal("browser-wound-action", queuedRoot["source"]!.GetValue<string>());
            Assert.Equal(wound.WoundId, queuedRoot["spiritualSelfTreatment"]!["woundId"]!.GetValue<string>());
            Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(wound),
                queuedRoot["spiritualSelfTreatment"]!["expectedWoundFingerprint"]!.GetValue<string>());
            Assert.Null(await fixture.Files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
            Assert.Null(await fixture.Files.ReadFileBytesAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
            Assert.Null(queuedRoot["attempt"]);
        }

        PendingPlayerActionService.Staged? original = null;
        JsonObject? retainedRequest = null;
        TurnRequest? turn = null;
        var responseWritten = false;
        var diagnostics = new GameEngineTurnLifecycleTests.SpiritualLifecycleTestLogger();
        var runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(fixture.Files,
            inputSource: new NoPlayerInput(diagnostics), logger: diagnostics,
            finalizationHooks: new GameEngineSessionFinalizationHooks
            {
                AtCheckpointAsync = async checkpoint =>
                {
                    if (checkpoint != SessionFinalizationCheckpoint.TerminalWaitStarted || responseWritten) return;
                    await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
                    turn = JsonSerializer.Deserialize<TurnRequest>((await fixture.Files.ReadFileAsync(lease, "input/turn_request.json"))!,
                        SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                    Assert.Equal(binding.ActionId, turn.RequestId);
                    var projected = JsonSerializer.SerializeToNode(turn, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                    Assert.NotNull(projected["spiritualHealing"]);
                    Assert.Empty(projected["spiritualHealing"]!["destinationGraphs"]!.AsArray());
                    var bytes = (await fixture.Files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!;
                    retainedRequest = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes))!.AsObject();
                    Assert.Equal("spiritual_self_treatment_request", retainedRequest["kind"]!.GetValue<string>());
                    Assert.Null(retainedRequest["snapshotToken"]);
                    Assert.Single(retainedRequest["attempt"]!["roll"]!["values"]!.AsArray());
                    var state = Assert.IsType<PendingPlayerActionService.State>(await PendingPlayerActionService.ReadAsync(fixture.Files, lease));
                    original = PendingPlayerActionService.ReadStaged(state);
                    var proof = JsonNode.Parse(state.ProofJson!)!;
                    Assert.Equal(bytes, Convert.FromBase64String(proof["spiritualWoundRequestBytesBase64"]!.GetValue<string>()));
                    await WriteNativeCompletionAsync(fixture.Files, lease, turn);
                    responseWritten = true;
                }
            });
        try
        {
            runtime.GameLoop.SetSession("session_guided_spiritual_healing", 42);
            await runtime.StateManager.RefreshGameStateAsync();
            await runtime.Engine.ProcessPlayerTurn(binding.Action, browserAction: binding);
            Assert.True(responseWritten, "The actual caller did not reach its native-shaped GM fixture.");
            Assert.NotNull(original);
            var manifest = GameEngine.ValidateDetachedBrowserBinding(original!);
            Assert.Equal(turn!.RequestId, manifest.RequestId);
            foreach (var artifact in manifest.Files.Values)
                Assert.Null(await fixture.Files.ReadFileBytesAsync(artifact));
            // Byte identity survives artifact cleanup; semantic JSON equivalence is insufficient.
            var respelled = System.Text.Encoding.UTF8.GetBytes(" " + CanonicalJsonUtf8.DecodeOneOptionalBom(
                Convert.FromBase64String(original!.SpiritualWoundRequestBytesBase64!)));
            Assert.Throws<InvalidDataException>(() => GameEngine.ValidateDetachedBrowserBinding(original with
                { SpiritualWoundRequestBytesBase64 = Convert.ToBase64String(respelled) }));
            Assert.Empty((await fixture.ReadWoundsAsync()).Occurrences);
            var history = await fixture.ReadHistoryAsync();
            Assert.Single(history.Transitions, t => t.Terminal);
            var result = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single(history.Transitions, t => t.AttemptId is not null).TransitionResult);
            Assert.True(JsonNode.DeepEquals(retainedRequest, result.ToCanonicalJson()["request"]));
            var schedule = JsonNode.Parse((await fixture.Files.ReadFileAsync(ProgressionScheduleService.SchedulePath))!)!;
            Assert.Equal(turn.ProgressionControl!.NextChaosSeaTurnOrdinal, schedule["currentChaosSeaTurnOrdinal"]!.GetValue<int>());
            Assert.Null(await fixture.Files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
            Assert.Null(await fixture.Files.ReadFileBytesAsync(ProgressionScheduleService.ReportPath));
            fixture.AssertResourcesUnchanged();
        }
        finally
        {
            output.WriteLine(diagnostics.Describe());
            await runtime.AudioService.DisposeAsync();
        }
    }

    [Fact]
    public async Task CancellingBrowserTreatmentLeavesNoIntentDrawOrGameplayChange()
    {
        await using var fixture = await HealingFixture.CreateAsync(1, 1, 20, stageRequest: false);
        var service = CreateCommands(fixture.Files);
        var before = fixture.CaptureMutationRoots();
        var opened = await service.ExecuteAsync(new("/лечить", Owner));
        Assert.Equal(CommandExecutionState.RequiresInput, opened.State);
        Assert.NotNull(opened.InteractiveSession);
        await service.CancelPromptSessionAsync(new(opened.InteractiveSession!.SessionId, Owner));
        Assert.Null(await fixture.Files.ReadFileBytesAsync(PendingPlayerActionService.PendingPath));
        Assert.Null(await fixture.Files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
        fixture.AssertMutationRootsUnchanged(before);
    }

    private static ExplorerWebCommandService CreateCommands(FileSystemManager files)
    {
        var state = new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance);
        return new(files, state, new LocalizationManager(), new ValidationService(files, NullLogger<ValidationService>.Instance));
    }

    private static async Task WriteNativeCompletionAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease, TurnRequest request)
    {
        var timestamp = DateTime.UtcNow.ToString("o");
        async Task Write(string path, object value) => await files.WriteFileAtomicAsync(lease, path,
            JsonSerializer.Serialize(value, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await Write("output/narrative_response.json", new { response = "Душа собирает свою целостность, пока жизнь Моря Хаоса продолжается.", timestamp });
        await Write("output/interface_updates.json", new { dialogueOptions = Array.Empty<object>(), timestamp });
        await Write("output/debug_logs.json", new { gm_thoughts_markdown = string.Join("\n",
            "## NPC Scope", "- Mode: Scene-local", "- Relevant actors: нет",
            "- Why relevant: Душа восстанавливается самостоятельно; NPC не участвуют.",
            "- Actors outside scope: нет", "- Why outside scope: Структурные акторы не меняются.",
            "", "## Reasoning", "Душа сосредоточилась на восстановлении; мир продолжил свой ход."), timestamp });
        var c = request.ProgressionControl!;
        await Write(ProgressionScheduleService.ReportPath, new { progressionProcessingReport = new
        {
            sessionId = request.SessionId, requestId = request.RequestId, turnNumber = request.TurnNumber,
            worldCyclesProcessed = 0, factionCyclesProcessed = 0,
            newLastWorldSimulationTimeInMinutes = c.LastWorldSimulationTimeInMinutes,
            newLastFactionSimulationTimeInMinutes = c.LastFactionSimulationTimeInMinutes,
            chaosSeaCyclesProcessed = c.ChaosSeaCyclesExpectedThisTurn, guardianProjectCyclesProcessed = c.GuardianProjectCyclesExpectedThisTurn,
            residentAgencyCyclesProcessed = c.ResidentAgencyCyclesExpectedThisTurn, shiningAbodeCyclesProcessed = c.ShiningAbodeCyclesExpectedThisTurn,
            shiningFactionCyclesProcessed = c.ShiningFactionCyclesExpectedThisTurn, shiningTradeCyclesProcessed = c.ShiningTradeCyclesExpectedThisTurn,
            newLastChaosSeaSimulationOrdinal = c.NextChaosSeaTurnOrdinal, newLastGuardianProjectCycleOrdinal = c.NextGuardianProjectCycleOrdinal,
            newLastResidentAgencyCycleOrdinal = c.NextResidentAgencyCycleOrdinal, newLastShiningAbodeCycleOrdinal = c.NextShiningAbodeCycleOrdinal,
            newLastShiningFactionCycleOrdinal = c.NextShiningFactionCycleOrdinal, newLastShiningTradeCycleOrdinal = c.NextShiningTradeCycleOrdinal,
            afterlifeCatchupProcessed = false, afterlifeCatchupSummaryEventsProcessed = 0
        }});
        await Write("ready/turn_complete.json", new { sessionId = request.SessionId, requestId = request.RequestId,
            turnNumber = request.TurnNumber, timestamp, status = "success", filesModified = new[]
            { "output/narrative_response.json", "output/interface_updates.json", "output/debug_logs.json", ProgressionScheduleService.ReportPath } });
    }

    private sealed class NoPlayerInput(GameEngineTurnLifecycleTests.SpiritualLifecycleTestLogger diagnostics) : IConsoleInputSource
    {
        public bool IsScripted => true;
        public bool KeyAvailable => false;
        public ConsoleKeyInfo ReadKey(bool intercept = true) => throw new InvalidOperationException("Unexpected interactive input in browser self-treatment.\n" + diagnostics.Describe());
        public string? ReadLine() => throw new InvalidOperationException("Unexpected interactive input in browser self-treatment.\n" + diagnostics.Describe());
        public void AssertCompleted() { }
    }

    private sealed class RepairPlayerInput(GameEngineTurnLifecycleTests.SpiritualLifecycleTestLogger diagnostics) : IConsoleInputSource
    {
        private readonly NoPlayerInput _otherwise = new(diagnostics);
        private int _escape;
        internal void RequestEscape() => Interlocked.Exchange(ref _escape, 1);
        public bool IsScripted => true;
        public bool KeyAvailable => Volatile.Read(ref _escape) != 0;
        public ConsoleKeyInfo ReadKey(bool intercept = true) => Interlocked.Exchange(ref _escape, 0) != 0
            ? new ConsoleKeyInfo('\0', ConsoleKey.Escape, false, false, false)
            : _otherwise.ReadKey(intercept);
        public string? ReadLine() => _otherwise.ReadLine();
        public void AssertCompleted() => _otherwise.AssertCompleted();
    }
}
