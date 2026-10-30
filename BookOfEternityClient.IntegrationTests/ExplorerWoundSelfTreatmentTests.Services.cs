using System.Runtime.ExceptionServices;
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

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    [Fact]
    public Task ConsoleElyaraServiceConfirmsExactPriceAndPublishesOnePaidTreatment() => RunServiceJourneyAsync(browser: false);
    [Fact]
    public Task BrowserElyaraServiceQueuesExactQuoteAndPublishesOnePaidTreatment() => RunServiceJourneyAsync(browser: true);
    [Fact]
    public Task ElyaraServicePublicationFailureRestoresUnpaidWholeConsoleTurn() => RunServiceJourneyAsync(browser: false, failPublication: true);

    private async Task RunServiceJourneyAsync(bool browser, bool failPublication = false, string realm = "Chaos Sea", bool faction = false)
    {
        if (!faction)
        {
            await RunServiceJourneyCoreAsync(browser, failPublication, realm, faction);
            return;
        }
        var diagnosticRoot = Path.GetFullPath(TestDiagnosticPaths.Root);
        var repositoryRoot = Path.GetFullPath(TestRepoPaths.RepoRoot).TrimEnd(Path.DirectorySeparatorChar);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (diagnosticRoot.Equals(repositoryRoot, comparison) || diagnosticRoot.StartsWith(repositoryRoot + Path.DirectorySeparatorChar, comparison))
            throw new InvalidDataException("Faction service capture requires BOE_TEST_DIAGNOSTIC_ROOT outside the checkout.");
        var capture = new LifecyclePassiveTimingDiagnostics("FactionService-" + (browser ? "browser" : failPublication ? "rollback" : "console"),
            output, gzipRecords: true);
        // Caller macro sections only. No filesystem recorder/observer is attached,
        // so its DATA reuse and fresh authority behavior remain unchanged.
        var timings = new OperationTimingAccumulator();
        var window = timings.StartWindow(captureEvents: true);
        Exception? primary = null;
        IDisposable? closing = null;
        try
        {
            using var journey = timings.Measure("FactionService.JourneyIncludingClosing");
            await RunServiceJourneyCoreAsync(browser, failPublication, realm, faction, capture, timings,
                actualRoot => { capture.BindFixtureRoot(actualRoot); capture.Arm(window); },
                () => closing = timings.Measure("FactionService.Closing"));
        }
        catch (Exception error) { primary = error; }
        finally
        {
            // The inner await-using fixture and audio close have already unwound.
            closing?.Dispose();
            await capture.CompleteAsync(primary);
        }
    }

    private async Task RunServiceJourneyCoreAsync(bool browser, bool failPublication, string realm, bool faction,
        LifecyclePassiveTimingDiagnostics? persistent = null, OperationTimingAccumulator? timings = null,
        Action<string>? allocatedRootForDiagnostics = null, Action? closingForDiagnostics = null)
    {
        var price = faction ? 51 : 25; var rank = faction ? 2 : 1; var tier = faction ? 3 : 5;
        var providerName = faction ? AfterlifeHealingPaymentLifecycleTests.FactionHealerName : "Элиара";
        FileSystemManager? ownedFiles = null; var armed = false; var cuts = 0;
        var hooks = !failPublication ? null : new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, _) =>
                {
                    if (!armed || phase != TrustedLocalPublicationPhase.MemberPublished) return;
                    var path = ownedFiles!.ResolvePath(AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath);
                    if (JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(File.ReadAllBytes(path)))!["inkFeathers"]!["current"]!.GetValue<int>() != 500 - price) return;
                    armed = false; cuts++; throw new InvalidOperationException("Paid service Engine C4 cut after wallet publication.");
                }
            };
        var fixture = await CreateFixtureAsync();
        Exception? primaryFailure = null;
        try { await RunWithFixtureAsync(); }
        catch (Exception error) { primaryFailure ??= error; }
        finally
        {
            try { await fixture.DisposeAsync(); }
            catch (Exception error)
            {
                primaryFailure ??= error;
                persistent?.RecordClosingFailure(error, "faction-service-fixture-close");
            }
        }
        if (primaryFailure is not null) ExceptionDispatchInfo.Capture(primaryFailure).Throw();
        async Task<AfterlifeWoundHealingLifecycleTests.Fixture> CreateFixtureAsync()
        {
            using var preparation = timings?.Measure("FactionService.FixturePreparation");
            return faction
                ? await AfterlifeHealingPaymentLifecycleTests.CreateFactionServiceFixtureAsync(rank, 20, hooks, engine: true,
                    allocatedRootForDiagnostics: allocatedRootForDiagnostics)
                : await AfterlifeHealingPaymentLifecycleTests.CreateServiceFixtureAsync(rank, 20, hooks, engine: true, realm: realm);
        }
        async Task RunWithFixtureAsync()
        {
            var files = fixture.Files; ownedFiles = files;
            var beforeWound = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
            var console = new TestExplorerConsole();
            var diagnostics = new GameEngineTurnLifecycleTests.SpiritualLifecycleTestLogger(persistent);
            JsonObject? retained = null; TurnRequest? turn = null; var wroteResponse = false;
            using var construction = timings?.Measure("FactionService.RuntimeConstruction");
            var runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(files, inputSource: new NoPlayerInput(diagnostics),
                logger: diagnostics, explorerConsole: console, finalizationHooks: new GameEngineSessionFinalizationHooks
                {
                    AtCheckpointAsync = async checkpoint =>
                    {
                        WritePhase("Faction service finalization checkpoint: " + checkpoint);
                        if (checkpoint != SessionFinalizationCheckpoint.TerminalWaitStarted || wroteResponse) return;
                        using var response = timings?.Measure("FactionService.GmResponse");
                        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                        turn = JsonSerializer.Deserialize<TurnRequest>((await files.ReadFileAsync(lease, LiveTurnPreparationService.TurnRequestPath))!,
                            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                        Assert.Equal("service", turn.SpiritualHealing!["mode"]!.GetValue<string>());
                        Assert.Equal(price, turn.SpiritualHealing["quote"]!["amount"]!.GetValue<int>());
                        retained = JsonNode.Parse((await files.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!)!.AsObject();
                        Assert.Equal(7, retained["schemaVersion"]!.GetValue<int>());
                        Assert.Equal("player_service_confirmed", retained["playerConfirmation"]!.GetValue<string>());
                        Assert.Equal(500, JsonNode.Parse((await files.ReadFileAsync(lease, AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath))!)!["inkFeathers"]!["current"]!.GetValue<int>());
                        await WriteNativeCompletionAsync(files, lease, turn,
                            AfterlifeSpiritualNaturalRecoveryTests.AuthorNaturalGraphs(beforeWound, turn.SpiritualHealing["destinationGraphs"]!.AsArray()),
                            providerName + " принимает подтверждённую платную попытку лечения; клиент рассчитывает её результат и один обычный цикл.");
                        await WriteElyaraServiceReasoningAsync(files, lease, turn, faction ? ElyaraFixtureReasoning.FactionWitness : ElyaraFixtureReasoning.Paid);
                        if (faction) await WriteFactionServiceReasoningAsync(files, lease, turn);
                        wroteResponse = true; armed = failPublication;
                    }
                });
            construction?.Dispose();
            try
            {
                runtime.GameLoop.SetSession("session_elyara_service_" + (browser ? "browser" : "console"), 42);
                using (timings?.Measure("FactionService.RefreshGameState"))
                    await runtime.StateManager.RefreshGameStateAsync();
                if (failPublication)
                {
                    // Ordinary runtime health initializes this ledger before the
                    // pre-turn backup. Match that committed zero-cycle baseline;
                    // do not mistake first initialization for a retained paid turn.
                    Assert.False(files.FileExists(ProgressionScheduleService.SchedulePath));
                    var initialized = await new ProgressionScheduleService(files,
                        NullLogger<ProgressionScheduleService>.Instance).EnsureInitializedAsync();
                    Assert.Equal(0, initialized.CurrentChaosSeaTurnOrdinal);
                    Assert.Equal(0, initialized.PendingChaosSeaCycles);
                }
                var before = fixture.CaptureMutationRoots(new[] { AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath,
                    GuardianThoughtJournalState.StatePath, EffectAcceptedTurnInputComposer.WorldTimePath,
                    PendingTurnStateService.PendingDiceStatePath }.Concat(AfterlifeWoundTreatmentAuthority.ServiceLocalityPaths)
                    .Concat(realm == "Shining Abode" ? new[] { ShiningAbodeState.StatePath } : Array.Empty<string>())
                    .Concat(faction ? new[] { GuardianAbodeResidentState.StatePath } : Array.Empty<string>()));
                using var handler = timings?.Measure("FactionService.HandlerAndTurn");
                if (browser)
                {
                    var service = CreateCommands(files);
                    var cancelled = await service.ExecuteAsync(new("/лечить", Owner));
                    await service.CancelPromptSessionAsync(new(cancelled.InteractiveSession!.SessionId, Owner));
                    fixture.AssertMutationRootsUnchanged(before);
                    var opened = await service.ExecuteAsync(new(realm == "Shining Abode" && !faction ? "/элиара" : "/лечить", Owner));
                    Assert.Equal(CommandExecutionState.RequiresInput, opened.State);
                    var targets = opened.Prompts.OfType<UiSelectionPrompt>().SingleOrDefault(p => p.Id == "wound_target");
                    Assert.Equal(realm == "Chaos Sea", targets is not null);
                    if (targets is not null)
                    {
                        var self = Assert.Single(targets.Options, o => !o.Disabled && o.Label == "Себя");
                        opened = await service.SubmitPromptSessionAsync(new(opened.InteractiveSession!.SessionId,
                            new() { ["wound_target"] = JsonValue.Create(self.Value) }, Owner));
                    }
                    var prompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(opened.Prompts, p => p.Id == "wound_selection"));
                    var paid = Assert.Single(prompt.Options, o => !o.Disabled && o.Label.Contains(providerName, StringComparison.Ordinal));
                    Assert.Contains(price.ToString(), paid.Label);
                    var submitted = await service.SubmitPromptSessionAsync(new(opened.InteractiveSession!.SessionId,
                        new() { ["wound_selection"] = JsonValue.Create(paid.Value), ["confirm_wound_treatment"] = JsonValue.Create(true) }, Owner));
                    Assert.DoesNotContain(submitted.Notifications, n => n.Severity == UiNotificationSeverity.Error);
                    PendingPlayerActionService.Binding binding;
                    await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                    {
                        var queued = Assert.IsType<PendingPlayerActionService.State>(await PendingPlayerActionService.ReadAsync(files, lease));
                        Assert.Equal("queued", queued.Phase); binding = queued.Binding;
                        Assert.True(binding.SpiritualSelfTreatment!.Service!.PlayerConfirmed);
                        Assert.Equal(price, binding.SpiritualSelfTreatment.Service.Quote.Amount);
                    }
                    fixture.AssertMutationRootsUnchanged(before);
                    using (timings?.Measure("FactionService.ProcessPlayerTurn"))
                        await runtime.Engine.ProcessPlayerTurn(binding.Action, browserAction: binding);
                }
                else
                {
                    console.QueueAnySelection("Отмена");
                    Assert.Equal(string.Empty, await runtime.Explorer.TryProcessCommand("/лечить"));
                    fixture.AssertMutationRootsUnchanged(before); Assert.Empty(console.ConfirmPrompts);
                    var offered = (await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true)).WoundCommandState!;
                    var choice = Assert.Single(offered.Choices, c => c.Available && c.Selection.Service is not null &&
                        (c.Selection.Service.Quote.Provider.OwnerKind == "resident") == faction);
                    Assert.Equal(realm == "Chaos Sea", offered.IsTargetSelection);
                    var selections = new List<string>();
                    if (offered.IsTargetSelection)
                    {
                        var targets = offered.Targets.Where(t => t.Available).ToArray();
                        var self = Assert.Single(targets, t => t.Owner.OwnerKind == "player_soul");
                        selections.Add((Array.IndexOf(targets, self) + 1) + ". " + self.Label);
                    }
                    var choices = offered.Choices.Where(c => c.Available).ToArray();
                    selections.Add((Array.IndexOf(choices, choice) + 1) + ". " + choice.Name + " — " + choice.Severity);
                    console.QueueAnySelection(selections.ToArray());
                    console.QueueAnyConfirmResponse(true);
                    var action = await runtime.Explorer.TryProcessCommand(realm == "Shining Abode" && !faction ? "/элиара" : "/лечить");
                    Assert.Equal(faction ? WoundApplicationService.FactionServiceActionText : WoundApplicationService.ServiceActionText, action);
                    Assert.Contains(price.ToString(), Assert.Single(console.ConfirmPrompts));
                    fixture.AssertMutationRootsUnchanged(before);
                    using (timings?.Measure("FactionService.ProcessPlayerTurn"))
                        await runtime.Engine.ProcessPlayerTurn(action!);
                }
                using var assertions = timings?.Measure("FactionService.Assertions");
                Assert.True(wroteResponse);
                if (failPublication)
                {
                    Assert.Equal(1, cuts); Assert.Equal(42, runtime.GameLoop.TurnNumber);
                    fixture.AssertMutationRootsUnchanged(before);
                    Assert.DoesNotContain((await fixture.ReadHistoryAsync()).Transitions, t => t.AttemptId is not null);
                    Assert.Null(await files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
                    Assert.Null(await files.ReadFileBytesAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
                    output.WriteLine("Console paid service C4 cut: wallet and complete pre-turn wound/effects/History/cycle restored; no accepted payment.");
                    return;
                }
                Assert.Equal(realm, turn!.ProgressionControl!.CurrentRealm);
                Assert.Equal(realm == "Shining Abode" ? 1 : 0, turn.ProgressionControl.ShiningAbodeCyclesExpectedThisTurn);
                Assert.Equal(realm == "Chaos Sea" ? 1 : 0, turn.ProgressionControl.ChaosSeaCyclesExpectedThisTurn);
                Assert.Equal(43, runtime.GameLoop.TurnNumber);
                var wallet = JsonNode.Parse((await files.ReadFileAsync(AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath))!)!["inkFeathers"]!;
                Assert.Equal(500 - price, wallet["current"]!.GetValue<int>()); Assert.Equal(900, wallet["total"]!.GetValue<int>());
                var history = await fixture.ReadHistoryAsync();
                var paidResult = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single(history.Transitions, t => t.AttemptId is not null).TransitionResult);
                Assert.Equal("service", paidResult.Mode); Assert.True(JsonNode.DeepEquals(retained, paidResult.ToCanonicalJson()["request"]));
                var die = retained!["attempt"]!["roll"]!["values"]![0]!.GetValue<int>();
                var calculation = SpiritualHealingOutcomeMath.Calculate(new(tier, rank, die, 0, 0))!;
                var expected = SpiritualWoundRecoveryPlanner.Build(beforeWound, 0, retained["attempt"]!["attemptId"]!.GetValue<string>(),
                    retained["attempt"]!["cycleKey"]!.GetValue<string>(), calculation);
                Assert.Equal(expected.Select(s => (s.Kind, s.SeverityRank, s.Progress)), paidResult.Stages.Select(s => (s.Kind, s.After.Severity.Rank, s.After.Recovery.CurrentStepProgress)));
                var schedule = JsonNode.Parse((await files.ReadFileAsync(ProgressionScheduleService.SchedulePath))!)!;
                Assert.Equal(turn!.ProgressionControl!.NextChaosSeaTurnOrdinal, schedule["currentChaosSeaTurnOrdinal"]!.GetValue<int>());
                if (realm == "Shining Abode")
                {
                    Assert.Equal(1, schedule["lastShiningAbodeCycleOrdinal"]!.GetValue<int>());
                    Assert.Equal(0, schedule["lastChaosSeaSimulationOrdinal"]!.GetValue<int>());
                }
                foreach (var path in new[] { AcceptedMechanicsPlan.WoundCommandPath, ProgressionScheduleService.ReportPath,
                    LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingPlayerActionService.PendingPath })
                    Assert.Null(await files.ReadFileBytesAsync(path));
                if (faction)
                {
                    var roster = JsonNode.Parse((await files.ReadFileAsync(GuardianAbodeResidentState.StatePath))!)!;
                    var thought = Assert.Single(roster[GuardianAbodeResidentState.ThoughtJournalProperty]!.AsArray().OfType<JsonObject>());
                    Assert.Equal(AfterlifeHealingPaymentLifecycleTests.FactionHealerId, thought["residentId"]!.GetValue<string>());
                    Assert.Equal(43, thought["turn"]!.GetValue<int>());
                    Assert.Contains("Я", thought["summary"]!.GetValue<string>());
                    Assert.Null(roster[GuardianAbodeResidentState.UpdateThoughtJournalProperty]);
                }
                fixture.AssertResourcesUnchanged();
                output.WriteLine($"{(browser ? "Browser" : "Console")} public {providerName}: cancel=0, confirmed price{price}, wallet500→{500-price},total900, actual d{die}/{calculation.ResultBand}; one accepted attempt/cycle/result7, complete transient cleanup.");
            }
            catch (Exception error) { primaryFailure = error; }
            finally
            {
                closingForDiagnostics?.Invoke();
                armed = false;
                WritePhase("Faction service closing begin; actual audio and fixture disposal follow.");
                try { output.WriteLine(diagnostics.Describe()); }
                catch (Exception error)
                {
                    primaryFailure ??= error;
                    persistent?.RecordClosingFailure(error, "faction-service-describe-forward");
                }
                try
                {
                    using (timings?.Measure("FactionService.AudioDisposal"))
                        await runtime.AudioService.DisposeAsync();
                }
                catch (Exception error)
                {
                    primaryFailure ??= error;
                    persistent?.RecordClosingFailure(error, "faction-service-audio-close");
                }
            }
        }

        void WritePhase(string message)
        {
            if (persistent is null) return;
            try { persistent.WriteLine(message); }
            catch (Exception error) { persistent.RecordClosingFailure(error, "faction-service-phase-forward"); }
        }
    }

    private enum ElyaraFixtureReasoning { Paid, Arrival, OrdinaryWait, FactionWitness }

    private static async Task WriteElyaraServiceReasoningAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease, TurnRequest turn,
        ElyaraFixtureReasoning kind = ElyaraFixtureReasoning.Paid)
    {
        const string name = "Элиара Последней Раны"; const string sourceName = "Тихий Хранитель";
        var timestamp = DateTime.UtcNow.ToString("o"); var remote = kind is ElyaraFixtureReasoning.Arrival or ElyaraFixtureReasoning.OrdinaryWait;
        var place = turn.ProgressionControl!.CurrentRealm == "Shining Abode" ? "Зал сопровождения" : "Лазарет Незаживающего Света";
        var summary = kind switch
        {
            ElyaraFixtureReasoning.Arrival => "Я встречаю прибывшую душу; лечение ещё не подтверждено.",
            ElyaraFixtureReasoning.OrdinaryWait => "Я продолжаю обычную стратегию в своей Обители, пока мир совершает безопасный цикл.",
            ElyaraFixtureReasoning.FactionWitness => "Я продолжаю обычную стратегию; душа отдельно подтвердила лечение у местного фракционного целителя.",
            _ => "Душа подтвердила цену принятой попытки; я применяю своё ИсцелениеV к выбранной ране."
        };
        var consequence = kind == ElyaraFixtureReasoning.FactionWitness ? "Я не лечу душу и не получаю оплату; клиент рассчитывает мой обычный цикл." : remote ? "Я сохраняю своё место; клиент рассчитывает обычное развитие, активного лечения и оплаты нет." : "Клиент публикует одну оплату и реальный исход; нового долга нет.";
        var intent = kind == ElyaraFixtureReasoning.Paid ? "Лечить в рамках подтверждённой услуги." : "Продолжить обычную стратегию без навязанного лечения.";
        JsonObject Memory(string id, string thought, string goal) => new()
        {
            ["entryId"] = "elyara_route_" + id + "_" + turn.RequestId, ["guardianId"] = id,
            ["turn"] = turn.TurnNumber, ["timestamp"] = timestamp, ["title"] = remote ? "Обычный безопасный цикл" : "Подтверждённая публичная услуга",
            ["summary"] = thought, ["eventType"] = "soul_assessment", ["consequence"] = consequence,
            ["attitude"] = "watchful", ["intent"] = goal
        };
        var journal = JsonNode.Parse((await files.ReadFileAsync(lease, GuardianThoughtJournalState.StatePath)) ?? "{\"entries\":[]}")!.AsObject();
        var updates = new JsonArray(Memory(AfterlifeWoundTreatmentAuthority.ElyaraId, summary, intent));
        if (remote) updates.Add(Memory("guardian_visit_source", "Я остаюсь в своей Обители и поддерживаю обычный безопасный цикл, не решая за душу.", "Сохранять обычный порядок без вмешательства в лечение."));
        journal[GuardianThoughtJournalState.UpdateProperty] = updates;
        await files.WriteFileAtomicAsync(lease, GuardianThoughtJournalState.StatePath, journal.ToJsonString());
        const string clientChanges = "guardianThoughtJournalUpdates в game_state/meta/guardian_thought_journal.json добавляет мою first-person память. Клиент сам формирует currencies, progressionStrategy и client_auto_strategy progressionLedger; ГМ не задаёт доход, ключ цикла или развитие. Рану, оплату, эффекты, History и время публикует клиент.";
        string Brain(string actor, string location, string thought, string goal, bool provider) => string.Join("\n",
            "", "### " + actor, "- Current location: " + location + "; Хранитель остаётся здесь.",
            "- Situation: " + (kind == ElyaraFixtureReasoning.FactionWitness ? "Душа отдельно подтвердила услугу местного фракционного целителя в текущем зале." : kind == ElyaraFixtureReasoning.Paid ? "Душа подтвердила цену25 за попытку." : kind == ElyaraFixtureReasoning.Arrival ? "Душа совершает обычный переход к Элиаре; платное лечение ещё не выбрано." : "Душа спокойно ждёт; мир продолжает обычный безопасный цикл."),
            "- Profile inputs: " + (kind == ElyaraFixtureReasoning.FactionWitness ? "Живая Элиара в текущем Зале сопровождения; действующая обычная стратегия, в этой попытке не является целителем или получателем оплаты." : provider ? "Живая текущая Элиара, ИсцелениеV, публичный множитель100%." : "Текущий Хранитель исходной Обители с действующей стратегией развития; не лечит и не принимает лечение."),
            "- Thoughts: " + thought, "- Motivation: " + goal,
            "- Constraints: Клиент считает развитие и цикл; лечение и оплата требуют отдельного подтверждения, чужой выбор не подменяется.",
            "- Strategy options:", kind == ElyaraFixtureReasoning.Paid ? "1. Провести услугу. Benefit: помочь душе. Risk: неудачный бросок." : "1. Продолжить обычную стратегию. Benefit: устойчивый порядок. Risk: лечение отложено.",
            "2. Навязать другое решение. Benefit: больше контроля. Risk: необоснованное вмешательство в выбор души.",
            "- Chosen strategy: " + goal, "- Rejected alternatives: Нет основания навязывать душе другую услугу или перемещение.",
            "- Actions: " + consequence, "- State changes: " + clientChanges);
        var reasoning = string.Join("\n", "## NPC Scope", "- Mode: " + (remote ? "Mixed" : "Scene-local"),
            "- Relevant actors: " + name + (remote ? ", " + sourceName : ""),
            "- Why relevant: Обычный цикл затрагивает развитие объявленных Хранителей; Элиара участвует в доступном маршруте помощи.",
            "- Actors outside scope: нет", "- Why outside scope: Другие акторы не меняются.", "", "## Reasoning",
            Brain(name, place, summary, intent, provider: kind != ElyaraFixtureReasoning.FactionWitness));
        if (remote) reasoning += "\n" + Brain(sourceName, sourceName + " — Обитель",
            "Я поддерживаю обычную жизнь своей Обители и не навязываю душе лечение.", "Продолжить обычную стратегию без вмешательства в лечение.", provider: false);
        await files.WriteFileAtomicAsync(lease, "output/debug_logs.json", new JsonObject
        { ["timestamp"] = timestamp, ["gm_thoughts_markdown"] = reasoning }.ToJsonString());
    }

    private static async Task AssertRemoteGuardianProgressionAsync(FileSystemManager files)
    {
        var profiles = JsonNode.Parse((await files.ReadFileAsync(WoundCarrierCatalog.AfterlifeProfilesPath))!)!["profiles"]!.AsArray();
        foreach (var id in new[] { AfterlifeWoundTreatmentAuthority.ElyaraId, "guardian_visit_source" })
        {
            var actor = Assert.Single(profiles.OfType<JsonObject>(), p => p["actorId"]!.GetValue<string>() == id);
            Assert.Equal(12, actor["currencies"]!["inkFeathers"]!.GetValue<int>());
            Assert.Equal("chaos:1", actor["progressionStrategy"]!["lastAutoProgressionCycleKey"]!.GetValue<string>());
            Assert.Single(actor["progressionLedger"]!.AsArray(), e => e!["source"]!.GetValue<string>() == "client_auto_strategy");
        }
    }
}
