using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    private async Task<(string? Action, PendingPlayerActionService.Binding? Binding)> SelectPhysicalJourneyActionAsync(
        FileSystemManager files, GameEngineTestRuntime runtime, TestExplorerConsole console, string mode, bool browser,
        Dictionary<string, byte[]?> before)
    {
        var opened = await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true);
        var menu = Assert.IsType<WoundCommandState>(opened.WoundCommandState);
        var selected = menu.IsTargetSelection ? menu.ForTarget(Assert.Single(menu.Targets, target => target.Owner.OwnerKind == "player")) : menu;
        var choice = Assert.Single(selected.Choices, candidate => candidate.Available && candidate.Selection.MortalTreatment?.Mode == mode &&
            candidate.Selection.MortalTreatment.Provider.OwnerId == "field_medic_01");
        Assert.Contains("Условия", choice.Reason); Assert.Contains("расход", choice.Reason, StringComparison.OrdinalIgnoreCase);
        if (mode == "course") { Assert.Contains("Условия этого этапа", choice.Reason); Assert.Contains("окно включительно", choice.Reason); }
        AssertMortalDiagnosisImages(before, files);
        if (browser)
        {
            var service = CreateCommands(files);
            var cancelled = await service.ExecuteAsync(new("/лечить", Owner));
            await service.CancelPromptSessionAsync(new(cancelled.InteractiveSession!.SessionId, Owner));
            AssertMortalDiagnosisImages(before, files);
            var promptResult = await service.ExecuteAsync(new("/лечить", Owner));
            if (promptResult.Prompts.OfType<UiSelectionPrompt>().SingleOrDefault(prompt => prompt.Id == "wound_target") is { } targets)
            {
                var self = Assert.Single(targets.Options, option => !option.Disabled && option.Label == "Себя");
                promptResult = await service.SubmitPromptSessionAsync(new(promptResult.InteractiveSession!.SessionId,
                    new() { ["wound_target"] = JsonValue.Create(self.Value) }, Owner));
            }
            var prompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(promptResult.Prompts, value => value.Id == "wound_selection"));
            var option = Assert.Single(prompt.Options, value => !value.Disabled && value.Label.Contains(choice.Name, StringComparison.Ordinal));
            var submitted = await service.SubmitPromptSessionAsync(new(promptResult.InteractiveSession!.SessionId,
                new() { ["wound_selection"] = JsonValue.Create(option.Value), ["confirm_wound_treatment"] = JsonValue.Create(true) }, Owner));
            Assert.DoesNotContain(submitted.Notifications, notification => notification.Severity == UiNotificationSeverity.Error);
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            var queued = Assert.IsType<PendingPlayerActionService.State>(await PendingPlayerActionService.ReadAsync(files, lease));
            Assert.Equal(choice.Selection, queued.Binding.SpiritualSelfTreatment);
            AssertMortalDiagnosisImages(before, files);
            return (null, queued.Binding);
        }
        var confirmsBefore = console.ConfirmPrompts.Count;
        console.QueueAnySelection("Отмена"); Assert.Equal(string.Empty, await runtime.Explorer.TryProcessCommand("/лечить"));
        AssertMortalDiagnosisImages(before, files); Assert.Equal(confirmsBefore, console.ConfirmPrompts.Count);
        var sequence = new List<string>();
        if (menu.IsTargetSelection)
        {
            var targets = menu.Targets.Where(target => target.Available).ToArray();
            var self = Assert.Single(targets, target => target.Owner.OwnerKind == "player");
            sequence.Add((Array.IndexOf(targets, self) + 1) + ". " + self.Label);
        }
        var choices = selected.Choices.Where(candidate => candidate.Available).ToArray();
        sequence.Add((Array.IndexOf(choices, choice) + 1) + ". " + choice.Name + " — " + choice.Severity);
        console.QueueAnySelection(sequence.ToArray()); console.QueueAnyConfirmResponse(true);
        var action = await runtime.Explorer.TryProcessCommand("/лечить");
        Assert.Equal(WoundApplicationService.MortalTreatmentActionText, action);
        Assert.Equal(confirmsBefore + 1, console.ConfirmPrompts.Count); AssertMortalDiagnosisImages(before, files);
        return (action, null);
    }

    private sealed record PhysicalItemQuantity(string Kind, string OwnerId, int Count);

    private static async Task<Dictionary<string, PhysicalItemQuantity>> CapturePhysicalItemCountsAsync(FileSystemManager files)
    {
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        async Task<JsonObject?> Root(string path) => await files.ReadFileAsync(lease, path) is { } text ? JsonNode.Parse(text)!.AsObject() : null;
        var catalog = MortalItemCarrierCatalog.Build(new(await Root(InventoryEquipmentService.ItemsPath), await Root(NpcCoreChangesContract.NpcCorePath),
            null, null, null, new Dictionary<string, JsonObject>(StringComparer.Ordinal)));
        Assert.Empty(catalog.Issues);
        return catalog.ByItemId.ToDictionary(pair => pair.Key, pair =>
        {
            var carrier = Assert.Single(pair.Value);
            return new PhysicalItemQuantity(carrier.Carrier.Kind, carrier.Carrier.OwnerId, carrier.Item["count"]!.GetValue<int>());
        }, StringComparer.Ordinal);
    }

    private static async Task AssertPhysicalDeclaredItemsExactlyOnceAsync(FileSystemManager files,
        Dictionary<string, PhysicalItemQuantity> before, MortalWoundTreatmentPersistedResult result,
        MortalWoundTreatmentResourceFinalization finalization)
    {
        var expected = before.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var index = MortalItemIdentityState.Parse(JsonNode.Parse(Assert.IsType<string>(await files.ReadFileAsync(MortalItemIdentityState.StatePath))));
        Assert.Empty(index.Issues);
        var transitionIds = new List<string>();
        for (var ordinal = 0; ordinal < finalization.Consumptions.Count; ordinal++)
        {
            var consumption = finalization.Consumptions[ordinal]; Assert.Equal("item_quantity", consumption.Kind);
            var command = WoundAcceptedTurnPlanner.CreateTreatmentItemConsumptionCommand(result.Request.Coordinates.AcceptedStateFingerprint,
                result.Request.RequestFingerprint, result.Receipt.ResultFingerprint, finalization.FinalizationFingerprint, ordinal, consumption);
            Assert.True(expected.TryGetValue(command.ItemId, out var item)); Assert.True(item!.Count >= command.Quantity);
            var remaining = item.Count - command.Quantity;
            if (remaining == 0) expected.Remove(command.ItemId); else expected[command.ItemId] = item with { Count = remaining };
            var identity = index.EntriesByItemId[command.ItemId];
            var transition = Assert.Single(identity["transitions"]!.AsArray(), value => value!["transitionId"]!.GetValue<string>() == command.TransitionId);
            Assert.Equal(item.Count, transition!["quantityBefore"]!.GetValue<int>());
            Assert.Equal(remaining, transition["quantityAfter"]!.GetValue<int>());
            transitionIds.Add(command.TransitionId);
        }
        var actual = await CapturePhysicalItemCountsAsync(files);
        Assert.Equal(expected.OrderBy(pair => pair.Key, StringComparer.Ordinal), actual.OrderBy(pair => pair.Key, StringComparer.Ordinal));
        var actualIds = index.EntriesByItemId.Values.SelectMany(value => value["transitions"]!.AsArray().OfType<JsonObject>())
            .Where(value => value["turn"]!.GetValue<int>() == result.Request.Coordinates.Turn && value["authorityKind"]?.GetValue<string>() == "mortal_wound_treatment")
            .Select(value => value["transitionId"]!.GetValue<string>()).ToArray();
        Assert.Equal(transitionIds.Order(StringComparer.Ordinal), actualIds.Order(StringComparer.Ordinal));
    }

    private static async Task WriteMortalTreatmentJourneyResponseAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, TurnRequest request, int? waitMinutes)
    {
        var timestamp = DateTime.UtcNow.ToString("O");
        async Task Write(string path, object value) => await files.WriteFileAtomicAsync(lease, path,
            JsonSerializer.Serialize(value, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var modified = new List<string> { "output/narrative_response.json", "output/interface_updates.json", "output/debug_logs.json" };
        if (waitMinutes is null)
        {
            var journals = JsonNode.Parse(Assert.IsType<string>(await files.ReadFileAsync(lease, "game_state/npcs/npc_journals.json")))!.AsObject();
            var rows = journals["NPCJournals"]!.AsArray();
            var medic = rows.OfType<JsonObject>().SingleOrDefault(value => value["npcId"]?.GetValue<string>() == "field_medic_01");
            if (medic is null) { medic = new JsonObject { ["npcId"] = "field_medic_01", ["npcName"] = "Field medic", ["journalEntries"] = new JsonArray() }; rows.Add(medic); }
            medic["journalEntries"]!.AsArray().Add(new JsonObject
            {
                ["entryId"] = "treatment_medic_" + request.RequestId, ["timestamp"] = timestamp,
                ["description"] = "Я выполнил согласованный этап лечения раны путника. Я соблюдал выбранный способ, не обещая ничего сверх его возможностей."
            });
            await files.WriteFileAtomicAsync(lease, "game_state/npcs/npc_journals.json", journals.ToJsonString());
            modified.Add("game_state/npcs/npc_journals.json");
        }
        await Write("output/narrative_response.json", new { response = waitMinutes is null
            ? "Я позволяю полевому медику выполнить согласованный этап лечения. Он работает осторожно, использует необходимые материалы и заканчивает, объяснив пределы сделанного."
            : "Я спокойно отдыхаю в клинике и жду следующего согласованного этапа лечения.", timestamp });
        var updates = new JsonObject { ["dialogueOptions"] = new JsonArray(), ["timestamp"] = timestamp };
        if (waitMinutes is { } elapsed) updates["timeChange"] = elapsed;
        await files.WriteFileAtomicAsync(lease, "output/interface_updates.json", updates.ToJsonString());
        var reasoning = waitMinutes is null ? """
            ## NPC Scope
            - Mode: Scene-local
            - Relevant actors: Field medic
            - Why relevant: field_medic_01 выполняет согласованный этап лечения игрока.
            - Actors outside scope: нет
            - Why outside scope: Другие акторы в лечении не участвуют.

            ## Actor Brain 2.0
            ### Field medic
            - Current location: T061 field clinic; loc_field_clinic_001, остаётся на месте.
            - Situation: Путник отдельно подтвердил известный способ лечения существующей раны.
            - Profile inputs: Собственные принятые навыки field_medicine, материалы, локация и согласие; чужих скрытых сведений не предполагает.
            - Motivation: Выполнить ровно согласованный этап безопасно.
            - Constraints: Не менять клиентские wound/History/resources/clock и не придумывать новую проверку или стоимость.
            - Thoughts: Я выполняю этот этап осторожно и не обещаю полного исцеления.
            - Strategy options:
              1. Выполнить согласованный этап. Benefit: путник получает предусмотренную помощь. Risk: способ имеет свои ограничения.
              2. Отказаться без препятствия. Benefit: не придётся работать. Risk: путник останется без согласованной помощи.
            - Chosen strategy: Выполнить отдельно подтверждённый этап выбранного способа.
            - Rejected alternatives: Беспричинный отказ не соответствует просьбе.
            - Actions: Медик проводит только выбранный этап, не меняет личность, отношения, умения или местоположение.
            - State changes: NPCJournals[].journalEntries[] получает собственную first-person запись; npc_core не меняется GM.

            ## Reasoning
            Клиент удерживает точный исходный mode/request, проверку/Fate и расходы. GM пишет действительную сцену и обычный NPC journal, не авторит wound_commands, результат, расход или repair wave.
            """ : """
            ## NPC Scope
            - Mode: Scene-local
            - Relevant actors: нет
            - Why relevant: Игрок только спокойно ждёт, не обращается к NPC.
            - Actors outside scope: Field medic
            - Why outside scope: field_medic_01 не участвует в действии, остаётся на месте; его мысли, состояние и journal не меняются.

            ## Reasoning
            Отдельное обычное действие ожидания. timeChange соответствует явно выбранному интервалу; отчёт выполняет только обязанности действительного исходного control. Это не автоматическое продолжение лечения.
            """;
        await Write("output/debug_logs.json", new { timestamp, gm_thoughts_markdown = reasoning });
        var control = request.ProgressionControl!;
        if (control.WorldCyclesAlreadyPendingBeforeTurn > 0 || control.FactionCyclesAlreadyPendingBeforeTurn > 0)
        {
            await Write(ProgressionScheduleService.ReportPath, new
            {
                progressionProcessingReport = new { sessionId = request.SessionId, requestId = request.RequestId, turnNumber = request.TurnNumber,
                    worldCyclesProcessed = control.WorldCyclesAlreadyPendingBeforeTurn, factionCyclesProcessed = control.FactionCyclesAlreadyPendingBeforeTurn,
                    newLastWorldSimulationTimeInMinutes = control.WorldCyclesAlreadyPendingBeforeTurn > 0
                        ? checked(control.LastWorldSimulationTimeInMinutes + control.WorldCyclesAlreadyPendingBeforeTurn * control.WorldCycleMinutes) : (int?)null,
                    newLastFactionSimulationTimeInMinutes = control.FactionCyclesAlreadyPendingBeforeTurn > 0
                        ? checked(control.LastFactionSimulationTimeInMinutes + control.FactionCyclesAlreadyPendingBeforeTurn * control.FactionCycleMinutes) : (int?)null }
            });
            modified.Add(ProgressionScheduleService.ReportPath);
        }
        await Write("ready/turn_complete.json", new { sessionId = request.SessionId, requestId = request.RequestId,
            turnNumber = request.TurnNumber, status = "success", timestamp, filesModified = modified });
    }
}
