using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task MortalLivingWorldRepair_ConflictingActivityCommandsRefuseThenRepairSameRequest()
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        const string activityPath = "game_state/npcs/npc_activities.json";
        const string repairPath = "game_state/control/validation_repair_request.json";
        TurnRequest? dispatched = null;
        JsonObject? declaredReport = null;
        byte[]? originalRequest = null, originalManifest = null;
        var repairCount = 0;
        string? handledRepair = null;
        var input = new LivingWorldRepairInput(() => _fs.FileExists(repairPath), () =>
        {
            var repairText = File.ReadAllText(_fs.ResolvePath(repairPath));
            if (handledRepair == repairText) return; // Only the identical still-visible request.
            Assert.True(repairCount < 2, "Unexpected additional repair packet: " + repairText);
            handledRepair = repairText;
            var repair = JsonNode.Parse(repairText)!;
            Assert.Equal(1, repair["revalidationAttempt"]!.GetValue<int>());
            repairCount++;
            Assert.NotNull(dispatched);
            Assert.Equal(dispatched.SessionId, repair["sessionId"]!.GetValue<string>());
            Assert.Equal(dispatched.RequestId, repair["requestId"]!.GetValue<string>());
            Assert.Equal(dispatched.TurnNumber, repair["turnNumber"]!.GetValue<int>());
            var codes = repair["errors"]!.AsArray().Select(issue => issue!["code"]!.GetValue<string>()).ToArray();
            if (repairCount == 1)
            {
                Assert.Equal(new[] { "npc_activity_update_completion_composition_forbidden" }, codes);
            }
            else
            {
                Assert.Equal(2, codes.Length);
                Assert.All(codes, code => Assert.Equal("accepted_turn_stale_player_facing_output_after_canonical_repair", code));
            }
            Assert.False(repair["fullTurnResubmissionRequired"]!.GetValue<bool>());
            Assert.Empty(repair["requiredResubmissionPaths"]!.AsArray());
            Assert.Equal(originalRequest, File.ReadAllBytes(_fs.ResolvePath("input/turn_request.json")));
            Assert.Equal(originalManifest, File.ReadAllBytes(_fs.ResolvePath(LiveTurnPreparationService.PendingTurnSnapshotManifestPath)));
            var coreBytes = File.ReadAllBytes(_fs.ResolvePath("game_state/npcs/npc_core.json"));
            var core = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(coreBytes).TrimStart('\uFEFF'))!;
            Assert.Equal(repairCount == 1 ? 0 : 240, core["NPCsInScene"]![0]!["currentActivity"]!["timeSpentMinutes"]!.GetValue<int>());
            var storyPath = _fs.ResolvePath("stories/mortal_life_2.jsonl");
            Assert.True(!File.Exists(storyPath) || string.IsNullOrWhiteSpace(File.ReadAllText(storyPath)));
            var historyPath = _fs.ResolvePath(MortalLivingWorldSource.HistoryPath);
            if (repairCount == 1 && File.Exists(historyPath)) Assert.Empty(JsonNode.Parse(File.ReadAllText(historyPath))!["entries"]!.AsArray());
            var historyBytes = File.Exists(historyPath) ? File.ReadAllBytes(historyPath) : null;
            var reportBytes = File.Exists(_fs.ResolvePath(MortalLivingWorldSource.ReportPath))
                ? File.ReadAllBytes(_fs.ResolvePath(MortalLivingWorldSource.ReportPath)) : null;

            // The correction removes the unsupported terminal command, retaining legal progress. No
            // canonical actor, original snapshot, owner or receipt is rewritten.
            if (repairCount == 1)
                File.WriteAllText(_fs.ResolvePath(activityPath), ActivityCommand().ToJsonString());
            // Real repair freshness requires re-emitting the unchanged report and
            // player-facing outputs after the repaired canonical-input file.
            var rewritePaths = repairCount == 1
                ? new[] { MortalLivingWorldSource.ReportPath, "output/narrative_response.json", "output/interface_updates.json" }
                : new[] { "output/narrative_response.json", "output/interface_updates.json" };
            foreach (var path in rewritePaths)
            {
                var physical = _fs.ResolvePath(path);
                var unchanged = File.ReadAllBytes(physical);
                File.WriteAllBytes(physical, unchanged);
            }
            if (repairCount == 2)
            {
                Assert.Equal(coreBytes, File.ReadAllBytes(_fs.ResolvePath("game_state/npcs/npc_core.json")));
                Assert.Equal(historyBytes, File.Exists(historyPath) ? File.ReadAllBytes(historyPath) : null);
                Assert.Equal(reportBytes, File.Exists(_fs.ResolvePath(MortalLivingWorldSource.ReportPath))
                    ? File.ReadAllBytes(_fs.ResolvePath(MortalLivingWorldSource.ReportPath)) : null);
            }
            File.WriteAllText(_fs.ResolvePath("game_state/control/validation_repair_ready.json"), JsonSerializer.Serialize(new
            {
                sessionId = dispatched.SessionId, requestId = dispatched.RequestId, turnNumber = dispatched.TurnNumber,
                updatedAtUtc = DateTime.UtcNow.ToString("O"), status = "success"
            }));
        });
        var runtime = await CreateMortalLivingWorldJourneyAsync(async request =>
        {
            Assert.Null(dispatched);
            dispatched = request;
            originalRequest = await _fs.ReadFileBytesAsync("input/turn_request.json");
            originalManifest = await _fs.ReadFileBytesAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath);
            var core = JsonNode.Parse((await _fs.ReadFileAsync("game_state/npcs/npc_core.json"))!)!;
            var before = core["NPCsInScene"]![0]!["currentActivity"]!.DeepClone();
            var claimed = before.DeepClone(); claimed["timeSpentMinutes"] = 240; claimed["currentStepNumber"] = 1;
            var time = JsonNode.Parse((await _fs.ReadFileAsync(MortalLivingWorldSource.WorldTimePath))!)!.AsObject();
            var from = time["currentTimeInMinutes"]!.GetValue<int>();
            time["currentTimeInMinutes"] = from + 240; time["timeOfDay"] = "12:00";
            await _fs.WriteFileAtomicAsync(MortalLivingWorldSource.WorldTimePath, time.ToJsonString());
            var audit = new JsonArray();
            foreach (var scope in new[] { "npcs", "factions", "locations", "projects", "world" })
                audit.Add(new JsonObject { ["scope"] = scope, ["reason"] = "Reviewed the four-hour interval; only the declared scout progress is claimed." });
            var target = JourneyReference("npcActivity", JourneyScoutId);
            var report = new JsonObject
            {
                ["schemaVersion"] = 1, ["sessionId"] = request.SessionId, ["requestId"] = request.RequestId,
                ["turn"] = request.TurnNumber, ["fromMinute"] = from, ["toMinute"] = from + 240,
                ["resolution"] = "changes", ["audit"] = audit, ["events"] = new JsonArray(new JsonObject
                {
                    ["eventId"] = "scout_missing_command", ["fromMinute"] = from, ["toMinute"] = from + 240,
                    ["actors"] = new JsonArray(JourneyReference("npc", JourneyScoutId)), ["targets"] = new JsonArray(target),
                    ["cause"] = new JsonObject { ["summary"] = "The scout continued the established crossing survey.", ["references"] = new JsonArray(JourneyReference("location", JourneyLocationId)) },
                    ["effects"] = new JsonArray(JourneyEffect("scout_progress", JourneyReference("npcActivity", JourneyScoutId), "currentActivity", before, claimed)),
                    ["consequences"] = new JsonArray(new JsonObject { ["summary"] = "The first survey step is claimed complete.", ["effectIds"] = new JsonArray("scout_progress") }),
                    ["knowledge"] = new JsonObject { ["player"] = "known", ["newsReferences"] = new JsonArray("scout_missing_command") }
                })
            };
            declaredReport = (JsonObject)report.DeepClone();
            await _fs.WriteFileAtomicAsync(MortalLivingWorldSource.ReportPath, report.ToJsonString());
            await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", new JsonObject
            {
                ["worldEventsLog"] = new JsonArray(new JsonObject
                {
                    ["eventId"] = "scout_missing_command", ["title"] = "Доклад разведчицы",
                    ["description"] = "Мира завершила первый этап обследования переправы.",
                    ["visibility"] = "public", ["locationId"] = JourneyLocationId
                })
            }.ToJsonString());
            await WriteJourneyScoutJournalAsync(request.TurnNumber, 0);
            // Deliberately conflict with the supported non-terminal progress command.
            // Repair must remove completion; this test does not qualify terminal completion.
            var conflicting = ActivityCommand();
            conflicting["completeNPCActivities"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = JourneyScoutId, ["NPCName"] = "Mira the scout",
                ["activityName"] = before["activityName"]!.DeepClone(),
                ["finalState"] = "Completed", ["narrativeSummary"] = "The survey is complete."
            });
            await _fs.WriteFileAtomicAsync(activityPath, conflicting.ToJsonString());
            await WriteMortalLivingWorldJourneyOutputAsync(request, "Разведчица Мира завершила первый этап обследования переправы за четыре часа.",
                [MortalLivingWorldSource.WorldTimePath, MortalLivingWorldSource.ReportPath, "game_state/npcs/npc_journals.json", "game_state/world/world_events.json", activityPath], JourneyActorReasoning(0));
        }, SeedMortalLivingWorldJourneyActorsAsync, input);
        await runtime.Engine.ProcessPlayerTurn("Ждать доклада разведчицы четыре часа.");
        Assert.InRange(repairCount, 1, 2); // A second challenge, if issued, must be exact output freshness (asserted above).
        Assert.Equal(42, runtime.GameLoop.TurnNumber);
        var finalCore = JsonNode.Parse((await _fs.ReadFileAsync("game_state/npcs/npc_core.json"))!)!;
        Assert.Equal(240, finalCore["NPCsInScene"]![0]!["currentActivity"]!["timeSpentMinutes"]!.GetValue<int>());
        var history = JsonNode.Parse((await _fs.ReadFileAsync(MortalLivingWorldSource.HistoryPath))!)!;
        var receipt = Assert.Single(history["entries"]!.AsArray())!;
        Assert.Equal(dispatched!.RequestId, receipt["requestId"]!.GetValue<string>());
        Assert.Equal(dispatched.SessionId, receipt["sessionId"]!.GetValue<string>());
        Assert.Equal(JsonNode.Parse(System.Text.Encoding.UTF8.GetString(originalManifest!).TrimStart('\uFEFF'))!["manifestPayloadHash"]!.GetValue<string>(),
            receipt["snapshotToken"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(declaredReport, receipt["report"]));
        Assert.Equal(42, receipt["turn"]!.GetValue<int>());
        var story = (await _fs.ReadFileAsync("stories/mortal_life_2.jsonl"))!;
        Assert.Single(story.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.False(_fs.FileExists(repairPath));
        Assert.False(_fs.FileExists(MortalLivingWorldSource.ReportPath));
        static JsonObject ActivityCommand() => new() { ["NPCActivityUpdates"] = new JsonArray(new JsonObject
        { ["NPCId"] = JourneyScoutId, ["NPCName"] = "Mira the scout", ["activityUpdate"] = new JsonObject { ["timeSpentMinutes"] = 240, ["currentStepNumber"] = 1 } }) };
    }

}
