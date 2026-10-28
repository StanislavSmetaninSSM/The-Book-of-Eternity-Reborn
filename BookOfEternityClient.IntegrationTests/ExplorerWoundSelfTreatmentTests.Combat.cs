using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    [Fact]
    public Task ConsoleCombatHealingUsesTheGuidedChoiceAndActualC2C4() => RunCombatHealingJourneyAsync(browser: false);

    [Fact]
    public Task BrowserCombatHealingUsesItsConfirmedQueueAndActualC2C4() => RunCombatHealingJourneyAsync(browser: true);

    private async Task RunCombatHealingJourneyAsync(bool browser)
    {
        const string session = "session_guided_combat_healing";
        // Same full unsigned initial/lore/profile/resource scaffold as the
        // existing accepted SpiritualContinuation Engine caller. Seed only before
        // the actual Engine's first snapshot; no second bootstrap or postaccept seed.
        await using var context = await AfterlifeResourceCutoverTests.CreateSpiritualGameEngineOriginalAsync(async original =>
        {
            await AfterlifeResourceCutoverTests.SeedCombatHealingOriginalAsync(original, tier: 3, rank: 2);
            var chat = (await original.ReadJsonAsync("game_state/history/chat_log.json"))!; chat["sessionId"] = session;
            await original.WriteExactJsonAsync("game_state/history/chat_log.json", chat.ToJsonString());
            await new ProgressionScheduleService(original.FileSystem, NullLogger<ProgressionScheduleService>.Instance).EnsureInitializedAsync();
            var pending = await new PendingTurnStateService(original.FileSystem, NullLogger<PendingTurnStateService>.Instance).GetOrCreateAsync();
            pending.PreGeneratedDices1d20 = new[] { 14, 5 }.Concat(Enumerable.Repeat(7, 18)).ToArray();
            await original.WriteExactJsonAsync(PendingTurnStateService.PendingDiceStatePath, JsonSerializer.Serialize(pending, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        });
        var files = context.FileSystem;
        var catalog = WoundCarrierCatalog.Build(new(null, null, null, null, (await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath))!.AsObject()));
        Assert.Empty(catalog.Issues); var originalWound = Assert.Single(catalog.Occurrences).Wound;
        Assert.Equal(2, originalWound.Severity.Rank);
        Assert.Equal(1, originalWound.Recovery.CurrentStepProgress);
        var scheduleBefore = await files.ReadFileBytesAsync(ProgressionScheduleService.SchedulePath);
        var worldBefore = await files.ReadFileBytesAsync(EffectAcceptedTurnInputComposer.WorldTimePath);
        var console = new TestExplorerConsole();
        var diagnostics = new GameEngineTurnLifecycleTests.SpiritualLifecycleTestLogger();
        var wrote = false;
        TurnRequest? turn = null;
        JsonObject? originalIntent = null;
        PendingPlayerActionService.Binding? queued = null;
        var runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(files, inputSource: new NoPlayerInput(diagnostics),
            logger: diagnostics, explorerConsole: console, finalizationHooks: new GameEngineSessionFinalizationHooks
            {
                AtCheckpointAsync = async checkpoint =>
                {
                    if (checkpoint != SessionFinalizationCheckpoint.TerminalWaitStarted || wrote) return;
                    await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                    {
                        turn = JsonSerializer.Deserialize<TurnRequest>((await files.ReadFileAsync(lease, LiveTurnPreparationService.TurnRequestPath))!, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                        Assert.Equal(42, turn.TurnNumber); Assert.Equal("combat_self", turn.SpiritualHealing!["mode"]!.GetValue<string>());
                        Assert.Equal(14, turn.PreGeneratedDices1d20[0]); Assert.Equal(5, turn.PreGeneratedDices1d20[1]);
                        Assert.Null(turn.SpiritualHealing["attemptId"]); Assert.Null(turn.SpiritualHealing["roll"]);
                        Assert.Equal(turn.ProgressionControl!.CurrentChaosSeaTurnOrdinal, turn.ProgressionControl.NextChaosSeaTurnOrdinal);
                        Assert.Equal(0, turn.ProgressionControl.ChaosSeaCyclesExpectedThisTurn);
                        originalIntent = JsonNode.Parse((await files.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!)!.AsObject();
                        Assert.Equal(SpiritualCombatHealingIntent.Kind, originalIntent["kind"]!.GetValue<string>());
                        if (browser)
                        {
                            var pending = Assert.IsType<PendingPlayerActionService.State>(await PendingPlayerActionService.ReadAsync(files, lease));
                            Assert.Equal(queued!.ActionId, turn.RequestId); Assert.NotNull(pending.Binding.SpiritualSelfTreatment!.Combat);
                            var staged = PendingPlayerActionService.ReadStaged(pending);
                            Assert.Equal(await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath), Convert.FromBase64String(staged.SpiritualWoundRequestBytesBase64!));
                            GameEngine.ValidateDetachedBrowserBinding(staged);
                        }
                    }
                    // Normal file-GM authoring happens after the actual original,
                    // using its one admitted pool. No direct accepted result is written.
                    await AfterlifeResourceCutoverTests.WriteCombatHealingExchangeAsync(context, "healed", 14, 5, 3);
                    await using var responseLease = await files.AcquireCanonicalWriteLeaseAsync();
                    await WriteCombatCompletionAsync(files, responseLease, turn!, originalWound);
                    wrote = true;
                }
            });
        try
        {
            runtime.GameLoop.SetSession(session, 41);
            await runtime.StateManager.RefreshGameStateAsync();
            var before = new Dictionary<string, byte[]?>();
            foreach (var path in AfterlifeWoundTreatmentAuthority.CombatSourcePaths.Append(AcceptedMechanicsPlan.WoundCommandPath))
                before[path] = await files.ReadFileBytesAsync(path);
            if (browser)
            {
                var service = CreateCommands(files);
                var cancelled = await service.ExecuteAsync(new("/лечить", Owner));
                await service.CancelPromptSessionAsync(new(cancelled.InteractiveSession!.SessionId, Owner));
                foreach (var pair in before) Assert.Equal(pair.Value, await files.ReadFileBytesAsync(pair.Key));
                var opened = await service.ExecuteAsync(new("/лечить", Owner));
                var prompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(opened.Prompts, p => p.Id == "wound_selection"));
                var option = Assert.Single(prompt.Options, p => !p.Disabled);
                Assert.DoesNotContain(originalWound.WoundId, JsonSerializer.Serialize(opened));
                var submitted = await service.SubmitPromptSessionAsync(new(opened.InteractiveSession!.SessionId,
                    new() { ["wound_selection"] = JsonValue.Create(option.Value), ["confirm_wound_treatment"] = JsonValue.Create(true) }, Owner));
                Assert.DoesNotContain(submitted.Notifications, n => n.Severity == UiNotificationSeverity.Error);
                await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                    queued = Assert.IsType<PendingPlayerActionService.State>(await PendingPlayerActionService.ReadAsync(files, lease)).Binding;
                Assert.NotNull(queued.SpiritualSelfTreatment!.Combat);
                foreach (var pair in before) Assert.Equal(pair.Value, await files.ReadFileBytesAsync(pair.Key));
                await runtime.Engine.ProcessPlayerTurn(queued.Action, browserAction: queued);
            }
            else
            {
                console.QueueAnySelection("Отмена");
                Assert.Equal(string.Empty, await runtime.Explorer.TryProcessCommand("/лечить"));
                foreach (var pair in before) Assert.Equal(pair.Value, await files.ReadFileBytesAsync(pair.Key));
                var state = (await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true)).WoundCommandState!;
                var choice = Assert.Single(state.Choices, c => c.Available); Assert.NotNull(choice.Selection.Combat);
                Assert.Contains("ОД", choice.Reason);
                console.QueueAnySelection("1. " + choice.Name + " — " + choice.Severity); console.QueueAnyConfirmResponse(true);
                var action = await runtime.Explorer.TryProcessCommand("/лечить");
                Assert.Equal(WoundApplicationService.CombatActionText, action);
                foreach (var pair in before) Assert.Equal(pair.Value, await files.ReadFileBytesAsync(pair.Key));
                await runtime.Engine.ProcessPlayerTurn(action);
            }
            Assert.True(wrote); Assert.Equal(42, runtime.GameLoop.TurnNumber); // Real acceptance before healing-specific assertions.
            var history = WoundHistoryState.Parse((await context.ReadJsonAsync(WoundHistoryState.HistoryPath))!.ToJsonString(), WoundHistoryState.HistoryPath).State!;
            var result = Assert.Single(history.Transitions.Select(t => t.TransitionResult).OfType<SpiritualCombatHealingPersistedResult>());
            Assert.Equal(14, SpiritualCombatHealingPersistedResult.ReadSource(result.Source).Calculation.Input.NaturalRoll);
            var after = result.Stages[^1].After; Assert.Equal(1, after.Severity.Rank); Assert.Equal(1, after.Recovery.CurrentStepProgress);
            Assert.Equal(originalWound.Recovery.LastTickKey, after.Recovery.LastTickKey); Assert.DoesNotContain(history.Transitions, t => t.Kind == "recover");
            Assert.Equal(2, result.Spend.AppliedAmount); Assert.Equal(6, result.Spend.BeforeState!.Current); Assert.Equal(4, result.Spend.AfterState!.Current);
            Assert.Equal(scheduleBefore, await files.ReadFileBytesAsync(ProgressionScheduleService.SchedulePath));
            Assert.Equal(worldBefore, await files.ReadFileBytesAsync(EffectAcceptedTurnInputComposer.WorldTimePath));
            var receipt = (await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!;
            Assert.Single(receipt["healingDecisions"]!.AsArray()); Assert.Empty(receipt["decisions"]!.AsArray());
            foreach (var path in new[] { AcceptedMechanicsPlan.WoundCommandPath, ProgressionScheduleService.ReportPath,
                SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath,
                LiveTurnPreparationService.TurnRequestPath, LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                PendingTurnSnapshotAuthority.AuthorityPath, "ready/turn_complete.json", PendingPlayerActionService.PendingPath })
                Assert.Null(await files.ReadFileBytesAsync(path));
            output.WriteLine("Combat accepted: original d20=14; H3/II gives I1, typed OD6→4 once, LastTick/schedule/world exact, no natural cycle; original cleanup complete.");
        }
        finally { output.WriteLine(diagnostics.Describe()); await runtime.AudioService.DisposeAsync(); }
    }

    private static async Task WriteCombatCompletionAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        TurnRequest turn, WoundMaterializationEnvelope wound)
    {
        var timestamp = DateTime.UtcNow.ToString("O");
        var graphs = AfterlifeSpiritualNaturalRecoveryTests.AuthorNaturalGraphs(wound, turn.SpiritualHealing!["possibleDestinationGraphs"]!.AsArray());
        await files.WriteFileAtomicAsync(lease, "output/narrative_response.json", new JsonObject
        { ["response"] = "Душа удерживает сосредоточение в духовной дуэли и ослабляет трещину духовной целостности; Хранитель сохраняет защиту. Безопасный цикл не проходит.", ["timestamp"] = timestamp }.ToJsonString());
        await files.WriteFileAtomicAsync(lease, "output/interface_updates.json", new JsonObject
        { ["dialogueOptions"] = new JsonArray(), ["spiritualHealingGraphs"] = graphs, ["timestamp"] = timestamp }.ToJsonString());
        await files.WriteFileAtomicAsync(lease, "output/debug_logs.json", new JsonObject
        {
            ["timestamp"] = timestamp, ["gm_thoughts_markdown"] = string.Join("\n", "## NPC Scope", "- Mode: Scene-local", "- Relevant actors: guardian_frame",
                "- Why relevant: Хранитель противостоит лечащей себя душе в текущей духовной дуэли.", "- Actors outside scope: нет", "- Why outside scope: Другие акторы не действуют.",
                "", "## Reasoning", "### guardian_frame", "- Current location: Море Хаоса; перемещения нет.", "- Situation: Душа лечит собственную рану, сохраняя риск прерывания.",
                "- Profile inputs: Постоянный профиль guardian_frame; guard0, pressure2, духовные ОД6.", "- Thoughts: Я сохраню защиту и не выдам душе новый ресурс или действие.",
                "- Motivation: Удержать собственную устойчивость.", "- Constraints: Исходный guard стоит2; душе доступно стандартное Исцеление3 по цене2. Дополнительного броска и безопасного цикла нет.",
                "- Strategy options:", "1. Удержать защиту. Benefit: сохранить устойчивость. Risk: душа ослабит рану.", "2. Прервать её давлением. Benefit: сорвать лечение. Risk: ослабить собственную защиту.",
                "- Chosen strategy: Guard без смены действия после исходного броска.", "- Rejected alternatives: Давление не заменяет уже объявленный guard.",
                "- Actions: Выполнен один guard; использованы исходные d20 14 и5 с индексами0 и1. Искусство души даёт её ране II→I; opposed margin остаётся отдельной величиной.",
                "- State changes: afterlife_spiritual_conflict_state фиксирует обмен и завершение; клиент публикует typed OD, wound/effects/History и existing receipt. Память, личные цели и отношения не меняются.")
        }.ToJsonString());
        Assert.Null(await files.ReadFileBytesAsync(lease, ProgressionScheduleService.ReportPath));
        await files.WriteFileAtomicAsync(lease, "ready/turn_complete.json", new JsonObject
        {
            ["sessionId"] = turn.SessionId, ["requestId"] = turn.RequestId, ["turnNumber"] = turn.TurnNumber,
            ["timestamp"] = timestamp, ["status"] = "success", ["filesModified"] = new JsonArray(
                AfterlifeSpiritualConflictState.StatePath, "output/narrative_response.json", "output/interface_updates.json", "output/debug_logs.json")
        }.ToJsonString());
    }
}
