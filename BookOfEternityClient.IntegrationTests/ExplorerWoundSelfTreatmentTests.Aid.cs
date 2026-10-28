using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Xunit;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    [Fact]
    public Task ConsoleAidUsesAnActualAcceptedGrantThenTheSameExplorerAndEngine() => RunAidJourneyAsync(browser: false);

    [Fact]
    public Task BrowserAidUsesAnActualAcceptedGrantThenTheOriginalQueuedEngineAction() => RunAidJourneyAsync(browser: true);

    private async Task RunAidJourneyAsync(bool browser)
    {
        await using var fixture = await HealingFixture.CreateAsync(3, 3, 14, stageRequest: false,
            beforeResourceBootstrap: AfterlifeSpiritualNaturalRecoveryTests.SeedAidGuardianAsync);
        var files = fixture.Files;
        var console = new TestExplorerConsole();
        var diagnostics = new GameEngineTurnLifecycleTests.SpiritualLifecycleTestLogger();
        var grantWritten = false;
        var aidWritten = false;
        JsonObject? aidRequest = null;
        TurnRequest? aidTurn = null;
        WoundMaterializationEnvelope? grantWound = null;
        PendingPlayerActionService.Binding? browserBinding = null;
        var runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(files,
            inputSource: new NoPlayerInput(diagnostics), logger: diagnostics, explorerConsole: console,
            finalizationHooks: new GameEngineSessionFinalizationHooks
            {
                AtCheckpointAsync = async checkpoint =>
                {
                    if (checkpoint != SessionFinalizationCheckpoint.TerminalWaitStarted) return;
                    await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                    var turn = JsonSerializer.Deserialize<TurnRequest>((await files.ReadFileAsync(lease, "input/turn_request.json"))!,
                        SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                    if (turn.TurnNumber == 43 && !grantWritten)
                    {
                        Assert.Equal("natural", turn.SpiritualHealing!["mode"]!.GetValue<string>());
                        Assert.Equal(0, Assert.Single(turn.SpiritualHealing["cohort"]!.AsArray())!["healingTier"]!.GetValue<int>());
                        Assert.Null(await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
                        var profiles = JsonNode.Parse((await files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
                        profiles["profiles"]!.AsArray().OfType<JsonObject>()
                            .Single(p => SpiritualTreatmentParticipation.ProfileId(p) == AfterlifeSpiritualNaturalRecoveryTests.AidGuardianId)
                            [SpiritualTreatmentParticipation.ChangeProperty] = AfterlifeSpiritualNaturalRecoveryTests.AidGrantProposal();
                        await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
                        await WriteNativeCompletionAsync(files, lease, turn, narrative:
                            "Хранитель целостности понимает предложение души и разрешает ей вылечить его духовную рану в следующий безопасный цикл.");
                        await WriteAidGuardianReasoningAsync(files, lease, turn, grant: true);
                        grantWritten = true;
                    }
                    else if (turn.TurnNumber == 44 && !aidWritten)
                    {
                        Assert.NotNull(grantWound);
                        Assert.Equal("aid", turn.SpiritualHealing!["mode"]!.GetValue<string>());
                        Assert.Equal(3, turn.SpiritualHealing["healingTier"]!.GetValue<int>());
                        Assert.Equal(0, turn.SpiritualHealing["targetNaturalHealingTier"]!.GetValue<int>());
                        aidRequest = JsonNode.Parse((await files.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!)!.AsObject();
                        Assert.Equal("aid", aidRequest["mode"]!.GetValue<string>());
                        Assert.Equal(3, aidRequest["schemaVersion"]!.GetValue<int>());
                        Assert.Equal(3, aidRequest["activeSource"]!["healingTier"]!.GetValue<int>());
                        Assert.Equal("normal", aidRequest["activeSource"]!["rollMode"]!.GetValue<string>());
                        Assert.Equal(0, aidRequest["activeSource"]!["complicationModifier"]!.GetValue<long>());
                        if (browser)
                        {
                            var queued = Assert.IsType<PendingPlayerActionService.State>(await PendingPlayerActionService.ReadAsync(files, lease));
                            Assert.Equal(browserBinding!.ActionId, turn.RequestId);
                            Assert.Equal(browserBinding.SpiritualSelfTreatment, queued.Binding.SpiritualSelfTreatment);
                            var staged = PendingPlayerActionService.ReadStaged(queued);
                            Assert.Equal((await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!,
                                Convert.FromBase64String(staged.SpiritualWoundRequestBytesBase64!));
                            GameEngine.ValidateDetachedBrowserBinding(staged); // Same original request3, not self downgrade.
                        }
                        else Assert.Null(await PendingPlayerActionService.ReadAsync(files, lease));
                        aidTurn = turn;
                        await WriteNativeCompletionAsync(files, lease, turn,
                            AfterlifeSpiritualNaturalRecoveryTests.AuthorNaturalGraphs(grantWound!, turn.SpiritualHealing["destinationGraphs"]!.AsArray()),
                            "Душа помогает Хранителю целостности восстановить духовную рану; безопасная жизнь Обители продолжается.");
                        await WriteAidGuardianReasoningAsync(files, lease, turn, grant: false);
                        aidWritten = true;
                    }
                }
            });
        try
        {
            runtime.GameLoop.SetSession(browser ? "session_browser_guardian_aid" : "session_console_guardian_aid", 42);
            await runtime.StateManager.RefreshGameStateAsync();
            await runtime.Engine.ProcessPlayerTurn("Предложить Хранителю целостности вылечить его духовную рану и спросить разрешение.");
            Assert.True(grantWritten);
            Assert.Equal(43, runtime.GameLoop.TurnNumber); // Actual acceptance precedes every aid-specific oracle.
            grantWound = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
            Assert.Equal(3, grantWound.Severity.Rank);
            Assert.Equal(1, grantWound.Recovery.CurrentStepProgress);
            Assert.Null(await files.ReadFileBytesAsync(LiveTurnPreparationService.TurnRequestPath));
            Assert.Null(await files.ReadFileBytesAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
            var grant = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single((await fixture.ReadHistoryAsync()).Transitions,
                t => t.TransitionResult is SpiritualHealingPersistedResult).TransitionResult);
            Assert.Equal(3, grant.SchemaVersion);
            Assert.Single(grant.ParticipationOutcomes!);
            var before = fixture.CaptureMutationRoots();
            if (browser)
            {
                var service = CreateCommands(files);
                var cancelled = await service.ExecuteAsync(new("/лечить", Owner));
                Assert.Equal(CommandExecutionState.RequiresInput, cancelled.State);
                await service.CancelPromptSessionAsync(new(cancelled.InteractiveSession!.SessionId, Owner));
                fixture.AssertMutationRootsUnchanged(before);
                Assert.Null(await files.ReadFileBytesAsync(PendingPlayerActionService.PendingPath));
                var opened = await service.ExecuteAsync(new("/лечить", Owner));
                var prompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(opened.Prompts, p => p.Id == "wound_selection"));
                var option = Assert.Single(prompt.Options, p => !p.Disabled);
                Assert.Contains("Хранител", option.Label);
                Assert.DoesNotContain(grantWound.WoundId, JsonSerializer.Serialize(opened));
                Assert.DoesNotContain(AfterlifeSpiritualNaturalRecoveryTests.AidGuardianId, JsonSerializer.Serialize(opened));
                var submitted = await service.SubmitPromptSessionAsync(new(opened.InteractiveSession!.SessionId,
                    new() { ["wound_selection"] = JsonValue.Create(option.Value), ["confirm_wound_treatment"] = JsonValue.Create(true) }, Owner));
                Assert.DoesNotContain(submitted.Notifications, n => n.Severity == UiNotificationSeverity.Error);
                await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                    browserBinding = Assert.IsType<PendingPlayerActionService.State>(await PendingPlayerActionService.ReadAsync(files, lease)).Binding;
                Assert.NotNull(browserBinding.SpiritualSelfTreatment!.Aid);
                fixture.AssertMutationRootsUnchanged(before);
                await runtime.Engine.ProcessPlayerTurn(browserBinding.Action);
            }
            else
            {
                console.QueueAnySelection("Отмена");
                Assert.Equal(string.Empty, await runtime.Explorer.TryProcessCommand("/лечить"));
                Assert.Empty(console.ConfirmPrompts);
                fixture.AssertMutationRootsUnchanged(before);
                var offered = (await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true)).WoundCommandState!;
                var choice = Assert.Single(offered.Choices, c => c.Available);
                Assert.NotNull(choice.Selection.Aid);
                console.QueueAnySelection("1. " + choice.Name + " — " + choice.Severity);
                console.QueueAnyConfirmResponse(true);
                var action = await runtime.Explorer.TryProcessCommand("/лечить");
                Assert.Equal(WoundApplicationService.AidActionText, action);
                Assert.Single(console.ConfirmPrompts);
                fixture.AssertMutationRootsUnchanged(before);
                Assert.Null(await files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
                await runtime.Engine.ProcessPlayerTurn(action!);
                await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                Assert.Null(runtime.Explorer.ConsumePendingSpiritualSelfTreatment(action!, files.GetOrCreateSessionGeneration(lease)));
            }
            Assert.True(aidWritten);
            Assert.Equal(44, runtime.GameLoop.TurnNumber);
            var history = await fixture.ReadHistoryAsync();
            var aid = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single(history.Transitions, t => t.AttemptId is not null).TransitionResult);
            Assert.Equal("aid", aid.Mode);
            Assert.True(JsonNode.DeepEquals(aidRequest, aid.ToCanonicalJson()["request"]));
            Assert.Equal(3, aid.ToCanonicalJson()["source"]!["healingTier"]!.GetValue<int>());
            Assert.Equal(0, Assert.Single(aid.ToCanonicalJson()["source"]!["ownerTiers"]!.AsArray())!["healingTier"]!.GetValue<int>());
            var draw = aidRequest!["attempt"]!["roll"]!["values"]!.AsArray().Select(v => v!.GetValue<int>()).Max();
            var calculation = SpiritualHealingOutcomeMath.Calculate(new(3, 3, draw, 0, 0))!;
            var expected = SpiritualWoundRecoveryPlanner.Build(grantWound, 0, aidRequest["attempt"]!["attemptId"]!.GetValue<string>(),
                aidRequest["attempt"]!["cycleKey"]!.GetValue<string>(), calculation);
            Assert.Equal(expected.Select(s => (s.Kind, s.SeverityRank, s.Progress)), aid.Stages.Select(s => (s.Kind, s.After.Severity.Rank, s.After.Recovery.CurrentStepProgress)));
            var after = aid.Stages[^1].After;
            if (after.Lifecycle == "healed") Assert.Empty((await fixture.ReadWoundsAsync()).Occurrences);
            else Assert.Equal(WoundMaterializationContract.SerializeCanonical(after), WoundMaterializationContract.SerializeCanonical(Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound));
            Assert.Equal(2, history.Transitions.Count(t => t.TransitionResult is SpiritualHealingPersistedResult));
            var schedule = JsonNode.Parse((await files.ReadFileAsync(ProgressionScheduleService.SchedulePath))!)!;
            Assert.Equal(aidTurn!.ProgressionControl!.NextChaosSeaTurnOrdinal, schedule["currentChaosSeaTurnOrdinal"]!.GetValue<int>());
            foreach (var path in new[] { AcceptedMechanicsPlan.WoundCommandPath, ProgressionScheduleService.ReportPath,
                         LiveTurnPreparationService.TurnRequestPath, LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingPlayerActionService.PendingPath })
                Assert.Null(await files.ReadFileBytesAsync(path));
            fixture.AssertResourcesUnchanged();
            output.WriteLine($"Guardian aid ({(browser ? "browser" : "console")}): actual grant43/naturalIII1; d20={draw}; final={after.Lifecycle}/{after.Severity.Rank}/{after.Recovery.CurrentStepProgress}; activeH3/targetH0, one aid cycle, one retained attempt.");
        }
        finally { output.WriteLine(diagnostics.Describe()); await runtime.AudioService.DisposeAsync(); }
    }

    private static async Task WriteAidGuardianReasoningAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease, TurnRequest turn, bool grant)
    {
        const string name = "Хранитель целостности";
        var timestamp = DateTime.UtcNow.ToString("o");
        var journal = JsonNode.Parse((await files.ReadFileAsync(lease, GuardianThoughtJournalState.StatePath)) ?? "{\"entries\":[]}")!.AsObject();
        journal[GuardianThoughtJournalState.UpdateProperty] = new JsonArray(new JsonObject
        {
            ["entryId"] = "guardian_aid_thought_" + turn.RequestId, ["guardianId"] = AfterlifeSpiritualNaturalRecoveryTests.AidGuardianId,
            ["turn"] = turn.TurnNumber, ["timestamp"] = timestamp, ["title"] = grant ? "Разрешение помочь" : "Принятие помощи",
            ["summary"] = grant ? "Я понимаю предложение души и разрешаю ей вылечить мою рану в следующий безопасный цикл." : "Я сохраняю своё согласие и принимаю помощь души без оплаты.",
            ["eventType"] = "soul_assessment", ["consequence"] = "Помощь добровольна; новая привязка или обязательство не возникают.",
            ["attitude"] = "trusting", ["intent"] = "Сохранить осознанное участие."
        });
        await files.WriteFileAtomicAsync(lease, GuardianThoughtJournalState.StatePath, journal.ToJsonString());
        await files.WriteFileAtomicAsync(lease, "output/debug_logs.json", new JsonObject
        {
            ["timestamp"] = timestamp, ["gm_thoughts_markdown"] = string.Join("\n", "## NPC Scope", "- Mode: Scene-local", "- Relevant actors: " + name,
                "- Why relevant: Хранитель осознанно разрешает помощь и принимает её.", "- Actors outside scope: нет", "- Why outside scope: Другие акторы не участвуют.",
                "", "## Reasoning", "### " + name, "- Current location: Обитель целостности; перемещения нет.",
                "- Situation: Душа предлагает безвозмездное лечение существующей духовной раны.", "- Profile inputs: Текущий сознательный Хранитель, Исцеление0; душа владеет Исцелением3.",
                "- Thoughts: Я готов принять помощь и сохраняю право отказаться.", "- Motivation: Восстановить целостность без нового долга.", "- Constraints: Нет конфликта, оплаты или передачи контроля; клиент считает исход и цикл.",
                "- Strategy options:", "1. Принять помощь. Benefit: восстановиться. Risk: неудача лечения.", "2. Отказаться. Benefit: сохранить дистанцию. Risk: рана останется.",
                "- Chosen strategy: Принять помощь добровольно.", "- Rejected alternatives: Нет причины отвергать предложенную безопасную помощь.", "- Actions: Хранитель выражает согласие и остаётся в той же Обители.",
                "- State changes: guardianThoughtJournalUpdates в game_state/meta/guardian_thought_journal.json сохраняет first-person память; профиль содержит только предложенное participation change в grant-ходе. Раны, эффекты, History и цикл публикует клиент.")
        }.ToJsonString());
    }
}
