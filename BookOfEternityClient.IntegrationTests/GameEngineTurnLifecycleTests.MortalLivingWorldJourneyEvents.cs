using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task MortalLivingWorldJourney_ScoutCouncilProjectNewsAndQuietIntervalPersistAcrossRealTurns()
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var stage = 0;
        var requests = new HashSet<string>(StringComparer.Ordinal);
        string[] actions = ["Ждать доклада разведчицы четыре часа.", "Дать совету четыре часа для организации ремонта.",
            "Дождаться завершения укрепления переправы через четыре часа.", "Провести следующие четыре часа спокойно и проверить новости."];
        var runtime = await CreateMortalLivingWorldJourneyAsync(Author, SeedMortalLivingWorldJourneyActorsAsync);
        foreach (var action in actions)
        {
            await runtime.Engine.ProcessPlayerTurn(action);
            if (runtime.GameLoop.TurnNumber != 42 + stage)
                Assert.Fail("Journey turn failed: " + _journeyLogger.Describe() + "\n" + System.Text.Json.JsonSerializer.Serialize(await runtime.ValidationService.ValidateGameStateAsync()));
            var history = await Read(MortalLivingWorldSource.HistoryPath);
            Assert.Equal(stage + 1, history["entries"]!.AsArray().Count);
            Assert.Equal(42 + stage, runtime.GameLoop.TurnNumber);
            Assert.Equal(720 + stage * 240, (await Read(MortalLivingWorldSource.WorldTimePath))["currentTimeInMinutes"]!.GetValue<int>());
            var story = await _fs.ReadFileAsync("stories/mortal_life_2.jsonl");
            Assert.Equal(stage + 1, story!.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
            Assert.False(_fs.FileExists(MortalLivingWorldSource.ReportPath));
            Assert.False(_fs.FileExists("game_state/control/validation_repair_request.json"));
            await runtime.StateManager.RefreshGameStateAsync();
            stage++;
        }
        Assert.Equal(4, requests.Count);
        var core = await Read("game_state/npcs/npc_core.json");
        Assert.Equal(240, core["NPCsInScene"]![0]!["currentActivity"]!["timeSpentMinutes"]!.GetValue<int>());
        var factions = await Read("game_state/factions/faction_core.json");
        Assert.Equal("Reinforce the crossing before reopening public travel.", factions["factions"]![0]!["currentAgenda"]!.GetValue<string>());
        var projects = await Read("game_state/factions/faction_projects.json");
        Assert.Empty(projects["activeProjects"]!.AsArray());
        var completed = Assert.Single(projects["completedProjects"]!.AsArray())!;
        Assert.Equal(JourneyProjectId, completed["projectId"]!.GetValue<string>());
        Assert.Equal("Completed", completed["finalState"]!.GetValue<string>());
        var map = await Read("game_state/world/world_map.json");
        Assert.Equal(1, map["locations"]![0]!["activeThreats"]![0]!["intensity"]!.GetValue<int>());

        async Task Author(TurnRequest request)
        {
            Assert.True(requests.Add(request.RequestId), "Each accepted interval needs a fresh actual request.");
            var time = await Read(MortalLivingWorldSource.WorldTimePath);
            var from = time["currentTimeInMinutes"]!.GetValue<int>();
            var effects = new JsonArray();
            var paths = new List<string> { MortalLivingWorldSource.WorldTimePath, MortalLivingWorldSource.ReportPath };
            var narrative = actions[stage];
            if (stage == 0)
            {
                var npc = (await Read("game_state/npcs/npc_core.json"))["NPCsInScene"]![0]!;
                var before = npc["currentActivity"]!.DeepClone();
                var after = before.DeepClone(); after["timeSpentMinutes"] = 240; after["currentStepNumber"] = 1;
                await Write("game_state/npcs/npc_activities.json", new JsonObject { ["NPCActivityUpdates"] = new JsonArray(new JsonObject
                    { ["NPCId"] = JourneyScoutId, ["name"] = "Mira the scout", ["activityUpdate"] = new JsonObject { ["timeSpentMinutes"] = 240, ["currentStepNumber"] = 1 } }) });
                paths.Add("game_state/npcs/npc_activities.json");
                effects.Add(JourneyEffect("scout-progress", JourneyReference("npcActivity", JourneyScoutId), "currentActivity", before, after));
                narrative = "Разведчица Мира обследовала опоры переправы и установила, что проход остаётся опасным. Совет получил её доклад.";
            }
            if (stage == 1)
            {
                var factionCore = await Read("game_state/factions/faction_core.json");
                var faction = factionCore["factions"]![0]!;
                var agenda = "Reinforce the crossing before reopening public travel.";
                effects.Add(JourneyEffect("council-agenda", JourneyReference("faction", JourneyFactionId), "currentAgenda", faction["currentAgenda"], JsonValue.Create(agenda)));
                factionCore["factionCoreChanges"] = new JsonArray(new JsonObject
                    { ["factionId"] = JourneyFactionId, ["reason"] = "The scout report prioritizes the existing bridge reinforcement project.", ["purposeAndPrinciples"] = new JsonObject { ["purpose"] = faction["purpose"]?.DeepClone(), ["currentAgenda"] = agenda, ["principles"] = faction["principles"]?.DeepClone() } });
                await Write("game_state/factions/faction_core.json", factionCore);
                paths.Add("game_state/factions/faction_core.json");
                var before = (await Read("game_state/factions/faction_projects.json"))["activeProjects"]![0]!;
                effects.Add(JourneyEffect("project-time", JourneyReference("factionProject", JourneyProjectId, JourneyFactionId), "timeSpentMinutes", before["timeSpentMinutes"], JsonValue.Create(240)));
                effects.Add(JourneyEffect("project-step", JourneyReference("factionProject", JourneyProjectId, JourneyFactionId), "currentStep", before["currentStep"], JsonValue.Create(1)));
                await Write("game_state/factions/faction_projects.json", new JsonObject { ["factionProjectUpdates"] = new JsonArray(new JsonObject
                    { ["factionId"] = JourneyFactionId, ["factionName"] = "Crossing Council", ["projectUpdate"] = new JsonObject
                        { ["projectId"] = JourneyProjectId, ["projectName"] = "Repair the crossing", ["timeSpentMinutes"] = 240, ["currentStep"] = 1 } }) });
                paths.Add("game_state/factions/faction_projects.json");
                narrative = "Совет продвинул уже начатый проект укрепления переправы. Первый этап ремонта завершён; угроза остаётся.";
            }
            if (stage == 2)
            {
                var before = (await Read("game_state/factions/faction_projects.json"))["activeProjects"]![0]!;
                var after = before.DeepClone().AsObject(); after.Remove("activeState"); after["finalState"] = "Completed"; after["completionTurn"] = request.TurnNumber.ToString(); after["timeSpentMinutes"] = 480; after["currentStep"] = 2;
                var completion = JourneyEffect("bridge-completed", JourneyReference("factionProject", JourneyProjectId, JourneyFactionId), "entity", before, after);
                completion["operation"] = "complete"; effects.Add(completion);
                await Write("game_state/factions/faction_projects.json", new JsonObject { ["factionProjectUpdates"] = new JsonArray(new JsonObject { ["factionId"] = JourneyFactionId, ["factionName"] = "Crossing Council", ["projectUpdate"] = new JsonObject { ["projectId"] = JourneyProjectId, ["projectName"] = "Repair the crossing", ["timeSpentMinutes"] = 480, ["currentStep"] = 2 } }), ["completeFactionProjects"] = new JsonArray(new JsonObject
                    { ["factionId"] = JourneyFactionId, ["factionName"] = "Crossing Council", ["projectId"] = JourneyProjectId,
                        ["projectName"] = "Repair the crossing", ["finalState"] = "Completed", ["completionTurn"] = request.TurnNumber }) });
                paths.Add("game_state/factions/faction_projects.json");
                var map = await Read("game_state/world/world_map.json");
                effects.Add(JourneyEffect("crossing-risk", JourneyReference("threat", JourneyThreatId, JourneyLocationId), "intensity", map["locations"]![0]!["activeThreats"]![0]!["intensity"], JsonValue.Create(1)));
                await Write("game_state/world/world_map.json", new JsonObject { ["worldMapUpdates"] = new JsonObject
                    { ["threatsToUpdate"] = new JsonArray(new JsonObject { ["targetLocationId"] = JourneyLocationId,
                        ["threatUpdate"] = new JsonObject { ["threatId"] = JourneyThreatId, ["intensity"] = 1 } }) } });
                paths.Add("game_state/world/world_map.json");
                narrative = "Совет объявил о завершении укрепления переправы. Опасность обрушения заметно снизилась; остаточная угроза сохраняется.";
            }
            time["currentTimeInMinutes"] = from + 240;
            time["timeOfDay"] = $"{((from + 240) % 1440) / 60:00}:{(from + 240) % 60:00}";
            if (from / 1440 != (from + 240) / 1440) time["dayOfMonth"] = time["dayOfMonth"]!.GetValue<int>() + 1;
            await Write(MortalLivingWorldSource.WorldTimePath, time);
            var audit = new JsonArray();
            foreach (var scope in new[] { "npcs", "factions", "locations", "projects", "world" })
                audit.Add(new JsonObject { ["scope"] = scope, ["reason"] = stage == 3
                    ? "За последние четыре часа после завершения ремонта разведчица не сообщила новых угроз, совет не менял планов, новых проектов и значимых последствий нет."
                    : "Проверены участники, переправа и проект за текущие четыре часа; все значимые изменения отражены в событии." });
            var events = new JsonArray();
            if (effects.Count > 0)
            {
                var eventId = "journey-crossing-" + request.TurnNumber;
                var targets = new JsonArray();
                foreach (var effect in effects)
                    if (!targets.Any(t => JsonNode.DeepEquals(t, effect!["target"]))) targets.Add(effect!["target"]!.DeepClone());
                events.Add(new JsonObject { ["eventId"] = eventId, ["fromMinute"] = from, ["toMinute"] = from + 240,
                    ["actors"] = new JsonArray(JourneyReference("npc", JourneyScoutId), JourneyReference("faction", JourneyFactionId)),
                    ["targets"] = targets, ["cause"] = new JsonObject { ["summary"] = narrative,
                        ["references"] = new JsonArray(JourneyReference("location", JourneyLocationId), JourneyReference("threat", JourneyThreatId, JourneyLocationId)) },
                    ["effects"] = effects, ["consequences"] = new JsonArray(new JsonObject { ["summary"] = narrative,
                        ["effectIds"] = new JsonArray(effects.Select(e => e!["effectId"]!.DeepClone()).ToArray()) }),
                    ["knowledge"] = new JsonObject { ["player"] = "known", ["newsReferences"] = new JsonArray(eventId) } });
                var newsPath = "game_state/world/world_events.json";
                var news = _fs.FileExists(newsPath) ? await Read(newsPath) : new JsonObject();
                var rows = news["worldEventsLog"] as JsonArray ?? new JsonArray();
                rows.Add(new JsonObject { ["eventId"] = eventId, ["title"] = "Вести с переправы", ["description"] = narrative, ["visibility"] = "public", ["locationId"] = JourneyLocationId });
                if (news["worldEventsLog"] is null) news["worldEventsLog"] = rows;
                await Write(newsPath, news); paths.Add(newsPath);
            }
            var reportFrom = from;
            var control = request.ProgressionControl!;
            if (control.WorldCyclesAlreadyPendingBeforeTurn > 0) reportFrom = Math.Min(reportFrom, control.LastWorldSimulationTimeInMinutes);
            if (control.FactionCyclesAlreadyPendingBeforeTurn > 0) reportFrom = Math.Min(reportFrom, control.LastFactionSimulationTimeInMinutes);
            await Write(MortalLivingWorldSource.ReportPath, new JsonObject { ["schemaVersion"] = 1, ["sessionId"] = request.SessionId,
                ["requestId"] = request.RequestId, ["turn"] = request.TurnNumber, ["fromMinute"] = reportFrom, ["toMinute"] = from + 240,
                ["resolution"] = stage == 3 ? "noSignificantChange" : "changes", ["audit"] = audit, ["events"] = events });
            await WriteJourneyScoutJournalAsync(request.TurnNumber, stage);
            paths.Add("game_state/npcs/npc_journals.json");
            await WriteMortalLivingWorldJourneyOutputAsync(request, narrative, paths.ToArray(), JourneyActorReasoning(stage));
        }
        async Task<JsonObject> Read(string path) => JsonNode.Parse((await _fs.ReadFileAsync(path))!)!.AsObject();
        async Task Write(string path, JsonObject value) => await _fs.WriteFileAtomicAsync(path, value.ToJsonString());
    }

    private static JsonObject JourneyReference(string kind, string id, string? owner = null)
    { var result = new JsonObject { ["kind"] = kind, ["id"] = id }; if (owner is not null) result["ownerId"] = owner; return result; }
    private static JsonObject JourneyEffect(string id, JsonObject target, string field, JsonNode? before, JsonNode? after) => new()
    { ["effectId"] = id, ["target"] = target, ["operation"] = "update", ["field"] = field,
      ["before"] = new JsonObject { ["present"] = true, ["value"] = before?.DeepClone() },
      ["after"] = new JsonObject { ["present"] = true, ["value"] = after?.DeepClone() } };
}
