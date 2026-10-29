using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CurrentElyaraVisitUsesOrdinaryTravelThenFreshUnpaidQuote(bool browser)
    {
        await using var fixture = await AfterlifeHealingPaymentLifecycleTests.CreateServiceFixtureAsync(3, 20,
            engine: true, remoteElyara: true);
        var files = fixture.Files;
        var wound = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        var console = new TestExplorerConsole();
        var diagnostics = new GameEngineTurnLifecycleTests.SpiritualLifecycleTestLogger();
        TurnRequest? turn = null; var completed = false;
        var runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(files, inputSource: new NoPlayerInput(diagnostics),
            logger: diagnostics, explorerConsole: console, finalizationHooks: new GameEngineSessionFinalizationHooks
            {
                AtCheckpointAsync = async checkpoint =>
                {
                    if (checkpoint != SessionFinalizationCheckpoint.TerminalWaitStarted || completed) return;
                    await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                    turn = JsonSerializer.Deserialize<TurnRequest>((await files.ReadFileAsync(lease, LiveTurnPreparationService.TurnRequestPath))!,
                        SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                    var intent = ElyaraVisitService.ParseAction(turn.PlayerAction);
                    Assert.Equal("guardian_visit_source", intent.SourceGuardianId);
                    Assert.Equal(AfterlifeWoundTreatmentAuthority.ElyaraId, intent.TargetGuardianId);
                    Assert.Equal("natural", turn.SpiritualHealing!["mode"]!.GetValue<string>());
                    Assert.Null(turn.SpiritualHealing["attemptId"]);
                    Assert.Null(await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
                    var guardians = JsonNode.Parse((await files.ReadFileAsync(lease, ElyaraVisitService.SourcePaths[2]))!)!;
                    var destination = guardians["guardians"]!.AsArray().OfType<JsonObject>().Single(g =>
                        g["guardianId"]!.GetValue<string>() == intent.TargetGuardianId);
                    guardians["activeGuardian"] = destination.DeepClone();
                    guardians["chaosSeaNavigation"]!["currentAbodeId"] = intent.TargetAbodeId;
                    guardians["chaosSeaNavigation"]!["currentGuardianId"] = intent.TargetGuardianId;
                    await files.WriteFileAtomicAsync(lease, ElyaraVisitService.SourcePaths[2], guardians.ToJsonString());
                    await WriteNativeCompletionAsync(files, lease, turn,
                        AfterlifeSpiritualNaturalRecoveryTests.AuthorNaturalGraphs(wound, turn.SpiritualHealing["destinationGraphs"]!.AsArray()),
                        "Душа прибывает в открытую текущую Обитель Элиары. Активное лечение ещё не начиналось.");
                    await WriteElyaraServiceReasoningAsync(files, lease, turn, visitOnly: true);
                    completed = true;
                }
            });
        try
        {
            runtime.GameLoop.SetSession("session_elyara_visit_" + (browser ? "browser" : "console"), 42);
            await runtime.StateManager.RefreshGameStateAsync();
            var before = fixture.CaptureMutationRoots([ElyaraVisitService.SourcePaths[0], ElyaraVisitService.SourcePaths[2]]);
            if (browser)
            {
                var service = CreateCommands(files);
                var cancel = await service.ExecuteAsync(new("/элиара", Owner));
                Assert.True(cancel.InteractiveSession!.RequiresLocalUiLock);
                await service.CancelPromptSessionAsync(new(cancel.InteractiveSession.SessionId, Owner));
                fixture.AssertMutationRootsUnchanged(before);
                var opened = await service.ExecuteAsync(new("/элиара", Owner));
                Assert.Single(opened.Prompts, p => p.Id == "confirm_elyara_visit");
                var json = JsonSerializer.Serialize(opened);
                Assert.DoesNotContain("guardian_visit_source", json);
                Assert.DoesNotContain("sourceFingerprint", json);
                var submitted = await service.SubmitPromptSessionAsync(new(opened.InteractiveSession!.SessionId,
                    new() { ["confirm_elyara_visit"] = JsonValue.Create(true) }, Owner));
                Assert.DoesNotContain(submitted.Notifications, n => n.Severity == UiNotificationSeverity.Error);
                PendingPlayerActionService.Binding binding;
                await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                {
                    var queued = Assert.IsType<PendingPlayerActionService.State>(await PendingPlayerActionService.ReadAsync(files, lease));
                    Assert.Equal(ElyaraVisitService.BrowserSource, JsonNode.Parse(queued.Json)!["source"]!.GetValue<string>());
                    binding = queued.Binding; Assert.Null(binding.SpiritualSelfTreatment);
                }
                fixture.AssertMutationRootsUnchanged(before);
                await runtime.Engine.ProcessPlayerTurn(binding.Action, browserAction: binding);
            }
            else
            {
                console.QueueAnyConfirmResponse(false);
                Assert.Equal(string.Empty, await runtime.Explorer.TryProcessCommand("/элиара"));
                fixture.AssertMutationRootsUnchanged(before);
                console.QueueAnyConfirmResponse(true);
                var action = await runtime.Explorer.TryProcessCommand("/элиара");
                Assert.True(ElyaraVisitService.HasMarker(action!));
                fixture.AssertMutationRootsUnchanged(before);
                await runtime.Engine.ProcessPlayerTurn(action!);
            }
            Assert.True(completed); Assert.Equal(43, runtime.GameLoop.TurnNumber);
            Assert.Equal(1, turn!.ProgressionControl!.ChaosSeaCyclesExpectedThisTurn);
            Assert.Equal(0, turn.ProgressionControl.ShiningAbodeCyclesExpectedThisTurn);
            var soul = JsonNode.Parse((await files.ReadFileAsync(ElyaraVisitService.SourcePaths[0]))!)!;
            Assert.Equal(500, soul["inkFeathers"]!["current"]!.GetValue<int>());
            Assert.DoesNotContain((await fixture.ReadHistoryAsync()).Transitions, t => t.AttemptId is not null);
            foreach (var path in new[] { PendingPlayerActionService.PendingPath, AcceptedMechanicsPlan.WoundCommandPath,
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath, ProgressionScheduleService.ReportPath })
                Assert.Null(await files.ReadFileBytesAsync(path));
            var fresh = (await ElyaraVisitService.OpenAsync(files)).WoundCommandState!;
            Assert.Equal(100, Assert.Single(fresh.Choices, c => c.Available && c.Selection.Service is not null).Selection.Service!.Quote.Amount);
            fixture.AssertResourcesUnchanged();
            output.WriteLine($"{(browser ? "Browser" : "Console")}: cancel inert; ordinary current-provider travel42→43, natural recovery retained, active treatment/payment=0; fresh local quote100 after arrival.");
        }
        finally { output.WriteLine(diagnostics.Describe()); await runtime.AudioService.DisposeAsync(); }
    }

    [Theory]
    [InlineData("dead")] [InlineData("moved")] [InlineData("unexpected_answer")]
    public async Task BrowserVisitRefusesStaleOrForeignConfirmationWithoutQueue(string change)
    {
        await using var fixture = await AfterlifeHealingPaymentLifecycleTests.CreateServiceFixtureAsync(1, 20, remoteElyara: true);
        var files = fixture.Files; var service = CreateCommands(files);
        var opened = await service.ExecuteAsync(new("/элиара", Owner));
        if (change != "unexpected_answer")
        {
            var profiles = JsonNode.Parse((await files.ReadFileAsync(WoundCarrierCatalog.AfterlifeProfilesPath))!)!;
            var actor = profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(AfterlifeHealingServiceContract.IsElyaraProfile);
            if (change == "dead") actor["lifeState"] = "dead";
            else { actor["locationId"] = "other_place"; actor["healingServiceProfile"]!["locationRef"] = "other_place"; }
            await files.WriteFileAtomicAsync(WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
        }
        var before = fixture.CaptureMutationRoots([ElyaraVisitService.SourcePaths[0], ElyaraVisitService.SourcePaths[2]]);
        var answers = new Dictionary<string, JsonNode?> { ["confirm_elyara_visit"] = JsonValue.Create(true) };
        if (change == "unexpected_answer") answers["targetAbodeId"] = JsonValue.Create("foreign_place");
        var result = await service.SubmitPromptSessionAsync(new(opened.InteractiveSession!.SessionId, answers, Owner));
        Assert.Contains(result.Notifications, n => n.Severity == UiNotificationSeverity.Error);
        Assert.Null(await files.ReadFileBytesAsync(PendingPlayerActionService.PendingPath));
        fixture.AssertMutationRootsUnchanged(before);
        await service.CancelPromptSessionAsync(new(opened.InteractiveSession.SessionId, Owner));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AbandonedConsoleVisitCannotPoisonNewCommandOrSession(bool forgetSession)
    {
        await using var fixture = await AfterlifeHealingPaymentLifecycleTests.CreateServiceFixtureAsync(1, 20, remoteElyara: true);
        var files = fixture.Files; var console = new TestExplorerConsole();
        var state = new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance);
        await state.RefreshGameStateAsync();
        var explorer = new ExplorerMode(state, files, new LocalizationManager(), console: console);
        console.QueueAnyConfirmResponse(true);
        var action = await explorer.TryProcessCommand("/элиара");
        Assert.True(ElyaraVisitService.HasMarker(action!));
        if (forgetSession) explorer.ForgetSessionTransientState();
        else { console.QueueAnySelection("Закрыть"); await explorer.TryProcessCommand("/раны"); }
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        Assert.Null(explorer.ConsumePendingElyaraVisit("Другое действие", files.GetOrCreateSessionGeneration(lease)));
        Assert.Null(await files.ReadFileBytesAsync(lease, PendingPlayerActionService.PendingPath));
        Assert.Null(await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
    }
}
