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
public sealed partial class ExplorerWoundSelfTreatmentTests(ITestOutputHelper output)
{
    private const string Owner = "wound-guided-client";

    [Fact]
    public async Task OrdinaryShiningTurnUsesSharedClockWithoutAdvancingInactiveChaosContour()
    {
        await using var fixture = await HealingFixture.CreateAsync(2, 3, 20, stageRequest: false, realm: "Shining Abode");
        var original = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        var scheduler = new ProgressionScheduleService(fixture.Files, NullLogger<ProgressionScheduleService>.Instance);
        await scheduler.EnsureInitializedAsync();
        var seeded = JsonNode.Parse((await fixture.Files.ReadFileAsync(ProgressionScheduleService.SchedulePath))!)!.AsObject();
        seeded["currentChaosSeaTurnOrdinal"] = 9; // Existing shared afterlife clock, not a new realm-specific counter.
        foreach (var key in new[] { "lastGuardianProjectCycleOrdinal", "lastResidentAgencyCycleOrdinal",
                     "lastShiningAbodeCycleOrdinal", "lastShiningFactionCycleOrdinal", "lastShiningTradeCycleOrdinal" }) seeded[key] = 9;
        seeded["lastChaosSeaSimulationOrdinal"] = 2;
        await fixture.Files.WriteFileAtomicAsync(ProgressionScheduleService.SchedulePath, seeded.ToJsonString());
        var diagnostics = new GameEngineTurnLifecycleTests.SpiritualLifecycleTestLogger();
        TurnRequest? turn = null;
        var responseWritten = false;
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
                    Assert.Equal("Shining Abode", turn.ProgressionControl!.CurrentRealm);
                    Assert.Equal(9, turn.ProgressionControl.CurrentChaosSeaTurnOrdinal);
                    Assert.Equal(10, turn.ProgressionControl.NextChaosSeaTurnOrdinal);
                    Assert.Equal(10, turn.ProgressionControl.NextShiningAbodeCycleOrdinal);
                    Assert.Equal(0, turn.ProgressionControl.ChaosSeaCyclesExpectedThisTurn);
                    Assert.Equal(1, turn.ProgressionControl.ShiningAbodeCyclesExpectedThisTurn);
                    Assert.Equal("natural", turn.SpiritualHealing!["mode"]!.GetValue<string>());
                    Assert.Null(turn.SpiritualHealing["attemptId"]);
                    Assert.Null(turn.SpiritualHealing["roll"]);
                    Assert.Null(await fixture.Files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
                    Assert.Equal(1, Assert.Single(turn.SpiritualHealing["destinationGraphs"]!.AsArray())!["severityRank"]!.GetValue<int>());
                    await WriteNativeCompletionAsync(fixture.Files, lease, turn,
                        AfterlifeSpiritualNaturalRecoveryTests.AuthorNaturalGraphs(original, turn.SpiritualHealing["destinationGraphs"]!.AsArray()),
                        "Тихий свет Сияющей Обители помогает душе постепенно обрести целостность.");
                    responseWritten = true;
                }
            });
        try
        {
            runtime.GameLoop.SetSession("session_ordinary_shining_recovery", 42);
            await runtime.StateManager.RefreshGameStateAsync();
            await runtime.Engine.ProcessPlayerTurn("Спокойно наблюдать за безопасной жизнью Сияющей Обители.");
            Assert.True(responseWritten);
            Assert.Equal(43, runtime.GameLoop.TurnNumber);
            var after = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
            Assert.Equal("shining_abode", after.Owner.Realm);
            Assert.Equal(1, after.Severity.Rank);
            Assert.Equal(0, after.Recovery.CurrentStepProgress); //SignedH3 adds4, completing II exactly.
            var history = await fixture.ReadHistoryAsync();
            Assert.DoesNotContain(history.Transitions, t => t.AttemptId is not null || t.Kind == "treat");
            var result = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single(history.Transitions,
                t => t.TransitionResult is SpiritualHealingPersistedResult).TransitionResult);
            Assert.True(result.IsNatural);
            Assert.Equal("shining_abode", result.ToCanonicalJson()["binding"]!["realm"]!.GetValue<string>());
            Assert.Equal(10, result.ToCanonicalJson()["source"]!["projection"]!["cycleOrdinal"]!.GetValue<int>());
            var schedule = JsonNode.Parse((await fixture.Files.ReadFileAsync(ProgressionScheduleService.SchedulePath))!)!;
            Assert.Equal(10, schedule["currentChaosSeaTurnOrdinal"]!.GetValue<int>());
            Assert.Equal(2, schedule["lastChaosSeaSimulationOrdinal"]!.GetValue<int>());
            foreach (var key in new[] { "lastGuardianProjectCycleOrdinal", "lastResidentAgencyCycleOrdinal",
                         "lastShiningAbodeCycleOrdinal", "lastShiningFactionCycleOrdinal", "lastShiningTradeCycleOrdinal" })
                Assert.Equal(10, schedule[key]!.GetValue<int>());
            Assert.Null(await fixture.Files.ReadFileBytesAsync(ProgressionScheduleService.ReportPath));
            Assert.Null(await fixture.Files.ReadFileBytesAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
            fixture.AssertResourcesUnchanged();
            output.WriteLine("Shining natural: II→I0, shared9→10, inactiveChaos2→2, healingDraw=none, cycle=1.");
        }
        finally { output.WriteLine(diagnostics.Describe()); await runtime.AudioService.DisposeAsync(); }
    }

    [Fact]
    public async Task OrdinaryRankThreeTurnUsesRequestedGraphWithoutTreatmentEntropy()
    {
        await using var fixture = await HealingFixture.CreateAsync(3, 5, 20, stageRequest: false);
        var original = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        var diagnostics = new GameEngineTurnLifecycleTests.SpiritualLifecycleTestLogger();
        TurnRequest? turn = null;
        JsonArray? graphs = null;
        var responseWritten = false;
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
                    Assert.Equal("natural", turn.SpiritualHealing!["mode"]!.GetValue<string>());
                    Assert.Null(turn.SpiritualHealing["attemptId"]);
                    Assert.Null(turn.SpiritualHealing["roll"]);
                    Assert.Null(await fixture.Files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
                    Assert.Equal(2, Assert.Single(turn.SpiritualHealing["destinationGraphs"]!.AsArray())!["severityRank"]!.GetValue<int>());
                    graphs = AfterlifeSpiritualNaturalRecoveryTests.AuthorNaturalGraphs(original,
                        turn.SpiritualHealing["destinationGraphs"]!.AsArray());
                    await WriteNativeCompletionAsync(fixture.Files, lease, turn, graphs);
                    responseWritten = true;
                }
            });
        try
        {
            runtime.GameLoop.SetSession("session_ordinary_spiritual_graph", 42);
            await runtime.StateManager.RefreshGameStateAsync();
            await runtime.Engine.ProcessPlayerTurn("Спокойно наблюдать за безопасным течением Моря Хаоса.");
            Assert.True(responseWritten);
            Assert.Equal(43, runtime.GameLoop.TurnNumber);
            var after = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
            Assert.Equal(2, after.Severity.Rank);
            Assert.Equal(0, after.Recovery.CurrentStepProgress); // One natural6 crosses III threshold6.
            Assert.Null(after.Care.LastAttemptId);
            var history = await fixture.ReadHistoryAsync();
            Assert.DoesNotContain(history.Transitions, t => t.AttemptId is not null || t.Kind == "treat");
            var result = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single(history.Transitions,
                t => t.TransitionResult is SpiritualHealingPersistedResult).TransitionResult);
            Assert.True(result.IsNatural);
            Assert.Equal("recover", Assert.Single(result.Stages).Kind);
            Assert.Null(result.ToCanonicalJson()["request"]);
            Assert.True(JsonNode.DeepEquals(graphs, JsonNode.Parse((await fixture.Files.ReadFileAsync("output/interface_updates.json"))!)!["spiritualHealingGraphs"]));
            var schedule = JsonNode.Parse((await fixture.Files.ReadFileAsync(ProgressionScheduleService.SchedulePath))!)!;
            Assert.Equal(turn!.ProgressionControl!.NextChaosSeaTurnOrdinal, schedule["currentChaosSeaTurnOrdinal"]!.GetValue<int>());
            foreach (var path in new[] { AcceptedMechanicsPlan.WoundCommandPath, ProgressionScheduleService.ReportPath,
                         PendingPlayerActionService.PendingPath, LiveTurnPreparationService.PendingTurnSnapshotManifestPath })
                Assert.Null(await fixture.Files.ReadFileBytesAsync(path));
            fixture.AssertResourcesUnchanged();
            output.WriteLine("Ordinary natural graph: III→II, progress0, tierV, healingDraw=none, attempt=none, cycle=1.");
        }
        finally
        {
            output.WriteLine(diagnostics.Describe());
            await runtime.AudioService.DisposeAsync();
        }
    }

    [Fact]
    public async Task OrdinarySafeTurnRecoversWithoutTreatmentAttemptOrDraw()
    {
        await using var fixture = await HealingFixture.CreateAsync(1, 0, 20, stageRequest: false);
        var original = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        Assert.Equal(0, original.Recovery.CurrentStepProgress);
        var diagnostics = new GameEngineTurnLifecycleTests.SpiritualLifecycleTestLogger();
        TurnRequest? turn = null;
        var responseWritten = false;
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
                    Assert.Null(await fixture.Files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
                    Assert.Null(await PendingPlayerActionService.ReadAsync(fixture.Files, lease));
                    // Ordinary turn dice are unrelated to recovery; this fixture never
                    // creates a separate healing request, attempt or retained draw.
                    await WriteNativeCompletionAsync(fixture.Files, lease, turn);
                    responseWritten = true;
                }
            });
        try
        {
            runtime.GameLoop.SetSession("session_ordinary_spiritual_recovery", 42);
            await runtime.StateManager.RefreshGameStateAsync();
            await runtime.Engine.ProcessPlayerTurn("Остаться в безопасном месте и спокойно наблюдать за Морем Хаоса.");
            Assert.True(responseWritten, "The ordinary action did not reach its native-shaped GM response.");
            Assert.Equal(43, runtime.GameLoop.TurnNumber); // A rejected/rolled-back turn is not natural-recovery RED.
            var wound = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
            Assert.Equal(original.WoundId, wound.WoundId);
            Assert.Equal(1, wound.Severity.Rank);
            Assert.Equal(1, wound.Recovery.CurrentStepProgress);
            Assert.Equal(original.Care.LastAttemptId, wound.Care.LastAttemptId);
            var history = await fixture.ReadHistoryAsync();
            Assert.DoesNotContain(history.Transitions, t => t.AttemptId is not null || t.Kind == "treat");
            var recovered = Assert.Single(history.Transitions, t => t.Kind == "recover");
            var result = Assert.IsType<SpiritualHealingPersistedResult>(recovered.TransitionResult).ToCanonicalJson();
            Assert.Equal("natural", result["mode"]!.GetValue<string>());
            Assert.Null(result["request"]);
            Assert.NotNull(turn);
            var projection = Assert.IsType<JsonObject>(turn!.SpiritualHealing);
            Assert.Equal("natural", projection["mode"]!.GetValue<string>());
            Assert.Null(projection["attemptId"]);
            Assert.Null(projection["roll"]);
            var schedule = JsonNode.Parse((await fixture.Files.ReadFileAsync(ProgressionScheduleService.SchedulePath))!)!;
            Assert.Equal(1, turn.ProgressionControl!.ChaosSeaCyclesExpectedThisTurn);
            Assert.Equal(turn.ProgressionControl.NextChaosSeaTurnOrdinal, schedule["currentChaosSeaTurnOrdinal"]!.GetValue<int>());
            foreach (var path in new[] { AcceptedMechanicsPlan.WoundCommandPath, ProgressionScheduleService.ReportPath,
                         PendingPlayerActionService.PendingPath, LiveTurnPreparationService.PendingTurnSnapshotManifestPath })
                Assert.Null(await fixture.Files.ReadFileBytesAsync(path));
            fixture.AssertResourcesUnchanged();
        }
        finally
        {
            output.WriteLine(diagnostics.Describe());
            await runtime.AudioService.DisposeAsync();
        }
    }

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
        var graphsRepaired = false;
        var outputRepaired = false;
        Dictionary<string, byte[]?>? publishedMechanical = null;
        byte[]? preservedDebug = null;
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
                        var phase = Interlocked.Increment(ref repairCount);
                        Assert.InRange(phase, 1, 2);
                        var repair = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(raw))!.AsObject();
                        Assert.NotNull(turn);
                        Assert.Equal(turn!.SessionId, repair["sessionId"]!.GetValue<string>());
                        Assert.Equal(turn.RequestId, repair["requestId"]!.GetValue<string>());
                        Assert.Equal(turn.TurnNumber, repair["turnNumber"]!.GetValue<int>());
                        if (phase == 1)
                        {
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
                            graphsRepaired = true;
                        }
                        else
                        {
                            Assert.True(IsExpectedOutputRepairPacket(raw, turn));
                            foreach (var pair in retainedSources!.Where(p => p.Key != AcceptedMechanicsPlan.WoundCommandPath))
                                Assert.Equal(pair.Value, await fixture.Files.ReadFileBytesAsync(lease, pair.Key));
                            Assert.Null(await fixture.Files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
                            var observed = PendingTurnSnapshotReader.ReadObservedCurrent(fixture.Files, lease,
                                new[] { AcceptedMechanicsPlan.WoundCommandPath });
                            Assert.True(observed.Success, string.Join("\n", observed.Issues));
                            Assert.NotNull(observed.Snapshot);
                            Assert.NotNull(observed.Evidence);
                            var signed = observed.Snapshot!;
                            observed.Evidence!.RequireCurrent(fixture.Files, lease, signed);
                            Assert.Equal(retainedSources[AcceptedMechanicsPlan.WoundCommandPath],
                                signed.ReadRequiredBytes(AcceptedMechanicsPlan.WoundCommandPath));
                            var scheduler = new ProgressionScheduleService(fixture.Files, NullLogger<ProgressionScheduleService>.Instance);
                            Assert.NotNull(retainedRequest);
                            Assert.True(await scheduler.IsPublishedSpiritualCycleAsync(turn.ProgressionControl!, lease,
                                signed.SnapshotToken, retainedRequest));
                            publishedMechanical = fixture.CaptureMutationRoots(
                                WoundAcceptedTurnSnapshotContract.PublicationAgreementPaths.Where(p => !p.StartsWith("output/", StringComparison.Ordinal))
                                    .Concat(AfterlifeWoundTreatmentAuthority.SourcePaths));
                            preservedDebug = await fixture.Files.ReadFileBytesAsync(lease, "output/debug_logs.json");
                            var narrative = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, "output/narrative_response.json"))!)!.AsObject();
                            var currentInterface = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, "output/interface_updates.json"))!)!.AsObject();
                            Assert.True(JsonNode.DeepEquals(authoredGraphs, currentInterface["spiritualHealingGraphs"]));
                            narrative["timestamp"] = DateTime.UtcNow.ToString("o");
                            currentInterface["timestamp"] = DateTime.UtcNow.ToString("o");
                            await fixture.Files.WriteFileAtomicAsync(lease, "output/narrative_response.json", narrative.ToJsonString());
                            await fixture.Files.WriteFileAtomicAsync(lease, "output/interface_updates.json", currentInterface.ToJsonString());
                            Assert.Equal(preservedDebug, await fixture.Files.ReadFileBytesAsync(lease, "output/debug_logs.json"));
                            fixture.AssertMutationRootsUnchanged(publishedMechanical);
                            observed.Evidence.RequireCurrent(fixture.Files, lease, signed);
                            outputRepaired = true;
                        }
                        await fixture.Files.WriteFileAtomicAsync(lease, readyPath, JsonSerializer.Serialize(new
                        {
                            sessionId = turn.SessionId, requestId = turn.RequestId, turnNumber = turn.TurnNumber,
                            updatedAtUtc = DateTime.UtcNow.ToString("o"), note = "Исправлены только перечисленные в текущем запросе файлы."
                        }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                    }, stop.Token, candidate => repairCount == 1 && IsExpectedOutputRepairPacket(candidate, turn));
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
            Assert.True(graphsRepaired && outputRepaired);
            Assert.Equal(2, repairCount);
            Assert.NotNull(publishedMechanical);
            fixture.AssertMutationRootsUnchanged(publishedMechanical!);
            Assert.Equal(preservedDebug, await fixture.Files.ReadFileBytesAsync("output/debug_logs.json"));
            var finalInterface = JsonNode.Parse((await fixture.Files.ReadFileAsync("output/interface_updates.json"))!)!;
            Assert.True(JsonNode.DeepEquals(authoredGraphs, finalInterface["spiritualHealingGraphs"]));
            var history = await fixture.ReadHistoryAsync();
            var attempt = Assert.Single(history.Transitions, t => t.AttemptId is not null);
            var result = Assert.IsType<SpiritualHealingPersistedResult>(attempt.TransitionResult);
            Assert.True(JsonNode.DeepEquals(retainedRequest, result.ToCanonicalJson()["request"]));
            var stages = result.Stages;
            var last = stages[^1].After;
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
                var stage = stages.First(s => s.After.Severity.Rank == rank);
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
            output.WriteLine("Actual graph self-treatment: d20=" + string.Join(",", retainedRequest!["attempt"]!["roll"]!["values"]!.AsArray().Select(v => v!.GetValue<int>())) +
                "; final=" + last.Lifecycle + "/" + last.Severity.Rank + "/" + last.Recovery.CurrentStepProgress + "; repair phases=2; world cycles=1.");
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

    private static bool IsExpectedOutputRepairPacket(byte[] raw, TurnRequest? turn)
    {
        if (turn is null) return false;
        try
        {
            var packet = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(raw))!.AsObject();
            if (packet["sessionId"]!.GetValue<string>() != turn.SessionId || packet["requestId"]!.GetValue<string>() != turn.RequestId ||
                packet["turnNumber"]!.GetValue<int>() != turn.TurnNumber || packet["fullTurnResubmissionRequired"]!.GetValue<bool>() ||
                packet["metadataDiagnosticOnly"]!.GetValue<bool>()) return false;
            var errors = packet["errors"]!.AsArray();
            var targets = new[] { "output/interface_updates.json", "output/narrative_response.json" };
            if (errors.Count != 2 || errors.Any(e => e?["code"]?.GetValue<string>() != "accepted_turn_stale_player_facing_output_after_canonical_repair") ||
                !new HashSet<string>(errors.Select(e => e!["filePath"]!.GetValue<string>()), StringComparer.Ordinal).SetEquals(targets)) return false;
            var packets = packet["harnessRepairPackets"]!.AsArray();
            return packets.Count == 1 && packets[0]!["kind"]!.GetValue<string>() == "accepted_turn_output_artifact_repair" &&
                packets[0]!["targetFiles"]!.AsArray().Count == 2 &&
                new HashSet<string>(packets[0]!["targetFiles"]!.AsArray().Select(p => p!.GetValue<string>()), StringComparer.Ordinal).SetEquals(targets);
        }
        catch (Exception failure) when (failure is JsonException or InvalidOperationException or ArgumentException or FormatException or NullReferenceException)
        { return false; } // Diagnostic observation can only nominate a fresh read, never authorize a write.
    }

    private async Task<byte[]?> ObserveGraphRepairPacketAsync(FileSystemManager files, string path, byte[]? handled,
        Func<FileSystemManager.CanonicalWriteLease, byte[], Task> processPacket, CancellationToken stop,
        Func<byte[], bool>? isExpectedFurtherSignal = null)
    {
        // Observation-only file-GM signal: these bytes can skip work or refuse,
        // but only a fresh leased reread can reach the response handler.
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
        if (handled is not null && isExpectedFurtherSignal?.Invoke(observed) != true)
            throw new InvalidDataException("Unexpected further graph repair packet:\n" + CanonicalJsonUtf8.DecodeOneOptionalBom(observed));
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync(cancellationToken: stop);
        var raw = await files.ReadFileBytesAsync(lease, path);
        if (raw is null || !raw.AsSpan().SequenceEqual(observed))
            throw new InvalidDataException("The graph repair packet changed before its actual leased admission.");
        await processPacket(lease, raw);
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

    private static ExplorerWebCommandService CreateCommands(FileSystemManager files, ExplorerWebPromptSessionService? prompts = null)
    {
        var state = new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance);
        return new(files, state, new LocalizationManager(), new ValidationService(files, NullLogger<ValidationService>.Instance), prompts);
    }

    internal static async Task WriteNativeCompletionAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        TurnRequest request, JsonArray? healingGraphs = null, string? narrative = null)
    {
        var timestamp = DateTime.UtcNow.ToString("o");
        async Task Write(string path, object value) => await files.WriteFileAtomicAsync(lease, path,
            JsonSerializer.Serialize(value, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await Write("output/narrative_response.json", new { response = narrative ?? "Душа собирает свою целостность, пока жизнь Моря Хаоса продолжается.", timestamp });
        if (healingGraphs is null)
            await Write("output/interface_updates.json", new { dialogueOptions = Array.Empty<object>(), timestamp });
        else
            await Write("output/interface_updates.json", new { dialogueOptions = Array.Empty<object>(),
                spiritualHealingGraphs = healingGraphs.DeepClone(), timestamp });
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
