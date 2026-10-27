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
}
