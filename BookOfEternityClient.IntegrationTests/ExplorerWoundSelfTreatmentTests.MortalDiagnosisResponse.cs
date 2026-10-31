using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    private static async Task<WoundMaterializationEnvelope> SeedMortalDiagnosisJourneyAsync(FileSystemManager files)
    {
        files.EnsureDirectoryStructure();
        // The ordinary complete Engine template is copied once. Component DATA
        // then supplies the real wound/item/medic graph before the sole bootstrap.
        foreach (var source in Directory.EnumerateFiles(TestRepoPaths.BaseSessionRoot, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(files.GameSessionPath, Path.GetRelativePath(TestRepoPaths.BaseSessionRoot, source));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(source, destination, true);
        }
        var fullSoul = JsonNode.Parse((await files.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        fullSoul["currentRealm"] = "Mortal World"; fullSoul["currentIncarnation"] = 1;
        var fullClock = JsonNode.Parse((await files.ReadFileAsync(EffectAcceptedTurnInputComposer.WorldTimePath))!)!.AsObject();
        fullClock["currentTimeInMinutes"] = 1260;
        var original = MortalWoundTreatmentResolverTests.SeedGuidedDiagnosisInitialWorld(files, beforeResourceBootstrap: current =>
        {
            void Write(string path, JsonNode value) => File.WriteAllText(current.ResolvePath(path), value.ToJsonString());
            var npc = JsonNode.Parse(File.ReadAllText(current.ResolvePath("game_state/npcs/npc_core.json")))!.AsObject();
            var medic = Assert.Single(npc["NPCsInScene"]!.AsArray());
            Assert.Equal("field_medic_01", medic!["NPCId"]!.GetValue<string>());
            medic["name"] = "Field medic";
            NpcTradeRequestState.NormalizeNpcTradeReceiptsShape(medic.AsObject());
            Write("game_state/npcs/npc_core.json", npc);
            Write("game_state/meta/soul_state.json", fullSoul);
            Write(EffectAcceptedTurnInputComposer.WorldTimePath, fullClock);
            Write("game_state/misc/characteristics.json", new JsonObject(Characteristics.All.Select(name =>
                new KeyValuePair<string, JsonNode?>(name, JsonValue.Create(10)))));
            Write("game_state/player/experience.json", new JsonObject { ["level"] = 1, ["experience"] = 0, ["experienceForNextLevel"] = 100 });
            // The component scene replaces the template inventory. Its unrelated
            // sword journal must not survive into this scene's sole bootstrap.
            const string itemJournalPath = "game_state/npcs/item_journals.json";
            var itemJournal = new JsonObject { ["entries"] = new JsonArray() };
            Write(itemJournalPath, itemJournal);
            JsonObject? ReadInitialRoot(string path)
            {
                var fullPath = current.ResolvePath(path);
                return File.Exists(fullPath) ? JsonNode.Parse(File.ReadAllText(fullPath))!.AsObject() : null;
            }
            var companions = new Dictionary<string, JsonObject>(StringComparer.Ordinal) { [itemJournalPath] = itemJournal };
            foreach (var path in new[] { "game_state/inventory/item_bonds.json", "game_state/inventory/item_text_updates.json",
                         "game_state/inventory/recipes.json", "game_state/quests/quest_history.json" })
                if (ReadInitialRoot(path) is { } root) companions.Add(path, root);
            var items = MortalItemCarrierCatalog.Build(new(ReadInitialRoot(InventoryEquipmentService.ItemsPath), npc,
                ReadInitialRoot("game_state/npcs/npc_inventory.json"), ReadInitialRoot(StorageTransportMoveService.CurrentLocationPath),
                ReadInitialRoot(StorageTransportMoveService.VehiclesPath), companions,
                ReadInitialRoot(MortalLocationStorageContentsState.StatePath)));
            Assert.Empty(items.Issues);
            // The existing catalog excludes explicitly HistoricalOnly quest
            // rewards; preserve that history and resolve every remaining live ref.
            foreach (var reference in items.ByCompanionReference)
            {
                Assert.True(items.ByItemId.TryGetValue(reference.Key, out var matches),
                    "Initial diagnosis companion has no exact item carrier: " + reference.Key);
                Assert.Single(matches!);
            }
        });
        await files.WriteFileAtomicAsync("game_state/core/game_settings.json", "{\"qteEventsEnabled\":false}");
        await files.WriteFileAtomicAsync("game_state/npcs/npc_journals.json", "{\"NPCJournals\":[]}");
        foreach (var pair in new Dictionary<string, JsonObject>
        {
            [AfterlifeEntityProfileState.StatePath] = AfterlifeEntityProfileState.CreateDefaultRoot(),
            [AfterlifeSpiritualConflictState.StatePath] = AfterlifeSpiritualConflictState.CreateDefaultRoot(),
            [AfterlifeChronicleState.StatePath] = AfterlifeChronicleState.CreateDefaultRoot(),
            [AfterlifeGlobalFlagState.StatePath] = AfterlifeGlobalFlagState.CreateDefaultRoot(),
            [WoundCarrierCatalog.NpcPath] = new() { ["schemaVersion"] = 1, ["entries"] = new JsonArray() },
            [WoundCarrierCatalog.EnemiesPath] = new() { ["enemiesData"] = new JsonArray() },
            [WoundCarrierCatalog.AlliesPath] = new() { ["alliesData"] = new JsonArray() }
        })
            if (await files.ReadFileBytesAsync(pair.Key) is null) await files.WriteFileAtomicAsync(pair.Key, pair.Value.ToJsonString());
        // Only initial template/fixture commands are removed. The Engine creates
        // its own actual request, dice and immutable original after confirmation.
        foreach (var path in new[] { LiveTurnPreparationService.TurnRequestPath, AcceptedMechanicsPlan.WoundCommandPath,
            WoundAcceptedTurnSnapshotContract.PendingResolutionPath, ProgressionScheduleService.ReportPath })
            if (await files.ReadFileBytesAsync(path) is not null) files.DeleteFile(path);
        await new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance).EnsureInitializedAsync();
        var control = await new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance).BuildControlForNextTurnAsync("Mortal World");
        Assert.Equal(1260, control.CurrentWorldTimeInMinutes);
        Assert.False(control.MustEvaluateWorldProgression); Assert.False(control.MustEvaluateFactionProgression);
        return original;
    }

    private static async Task WriteMortalDiagnosisJourneyResponseAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, TurnRequest request, byte[] journalsBefore)
    {
        var timestamp = DateTime.UtcNow.ToString("O");
        async Task Write(string path, object value) => await files.WriteFileAtomicAsync(lease, path,
            JsonSerializer.Serialize(value, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var journals = JsonNode.Parse(FileSystemManager.DecodeFileBytes(journalsBefore))!.AsObject();
        var entries = journals["NPCJournals"]!.AsArray(); Assert.Empty(entries);
        entries.Add(new JsonObject
        {
            ["npcId"] = "field_medic_01", ["npcName"] = "Field medic",
            ["journalEntries"] = new JsonArray(new JsonObject
            {
                ["entryId"] = "diagnosis_medic_" + request.TurnNumber,
                ["timestamp"] = timestamp,
                ["description"] = "Я осмотрел рану путника выбранным способом. Я не обещаю лечения: отмечаю только то, что удалось установить, и сохраняю профессиональную осторожность."
            })
        });
        await files.WriteFileAtomicAsync(lease, "game_state/npcs/npc_journals.json", journals.ToJsonString());
        await Write("output/narrative_response.json", new
        {
            response = "Я даю полевому медику спокойно осмотреть рану. Он осторожно раздвигает её края стерильной нитью, всматривается и объясняет, что смог заметить. Осмотр занимает полчаса; медик не обещает, что от него рана заживёт быстрее.", timestamp
        });
        await Write("output/interface_updates.json", new { dialogueOptions = Array.Empty<object>(), timestamp });
        await Write("output/debug_logs.json", new
        {
            timestamp,
            gm_thoughts_markdown = """
            ## NPC Scope
            - Mode: Scene-local
            - Relevant actors: Field medic
            - Why relevant: field_medic_01 непосредственно выполняет отдельно подтверждённый осмотр игрока и объясняет его границы.
            - Actors outside scope: нет
            - Why outside scope: Другие акторы в процедуре не участвуют.

            ## Actor Brain 2.0
            ### Field medic
            - Current location: T061 field clinic; loc_field_clinic_001, остаётся на месте.
            - Situation: Путник просит осмотреть существующую рану явно заданным способом.
            - Profile inputs: Полевой медик, текущая локация, принятые навыки field_medicine, собственные материалы и согласие на осмотр; скрытых знаний игрока не предполагает.
            - Motivation: Дать точное ограниченное наблюдение, не обещая лечения.
            - Constraints: Нет права менять wound/History/clock; результат проверки, расходы и время принадлежат клиенту.
            - Thoughts: Я выбираю осторожный осмотр и объясняю пределы наблюдения.
            - Strategy options:
              1. Осмотреть осторожно. Benefit: путник получает обоснованное наблюдение. Risk: результата может быть недостаточно.
              2. Отказаться от осмотра. Benefit: не придётся отвечать за вывод. Risk: путник останется без доступной помощи.
            - Chosen strategy: Выполнить согласованный ограниченный осмотр, не предпринимая лечение.
            - Rejected alternatives: Отказ без препятствия не отвечает просьбе; обещание исцеления не следует из осмотра.
            - Actions: Медик осматривает рану и осторожно описывает наблюдение; отношений, навыков, личности или локации не меняет.
            - State changes: NPCJournals[].journalEntries[] добавляет его собственную first-person запись в game_state/npcs/npc_journals.json; npc_core неизменен.

            ## Reasoning
            Клиент удерживает единственную исходную проверку, расход и явно заданные 30 минут. GM не перебрасывает, не лечит и не пишет будущий clock. Исходный control не требует мирового/фракционного отчёта; вновь прошедшее время остаётся обычной scheduler обязанностью следующего хода.
            """
        });
        Assert.False(request.ProgressionControl!.MustEvaluateWorldProgression);
        Assert.False(request.ProgressionControl.MustEvaluateFactionProgression);
        Assert.Null(await files.ReadFileBytesAsync(lease, ProgressionScheduleService.ReportPath));
        await Write("ready/turn_complete.json", new
        {
            sessionId = request.SessionId, requestId = request.RequestId, turnNumber = request.TurnNumber,
            status = "success", timestamp, filesModified = new[]
            { "output/narrative_response.json", "output/interface_updates.json", "output/debug_logs.json", "game_state/npcs/npc_journals.json" }
        });
    }
}
