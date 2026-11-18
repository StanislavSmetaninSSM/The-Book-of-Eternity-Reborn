using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task MortalNpcCompletion_MissingOutcomeRepairsAndSettlesOriginalTurnOnce()
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        const string core = "game_state/npcs/npc_core.json", commands = "game_state/npcs/npc_activities.json";
        const string requestPath = "input/turn_request.json", repairPath = "game_state/control/validation_repair_request.json";
        const string story = "stories/mortal_life_2.jsonl", news = "game_state/world/world_events.json";
        const string action = "Ждать восемь часов, пока Мира закончит осмотр переправы.";
        const string narrative = "Через восемь часов Мира закончила осмотр: повреждённая опора не позволяет подтвердить безопасность переправы.";
        const string summary = "The survey finished, but the damaged support prevents a safe-passage recommendation.";
        const string outcome = "SuccessWithComplication", eventId = "scout_survey_concluded";
        const string manifest = LiveTurnPreparationService.PendingTurnSnapshotManifestPath;
        const string authority = PendingTurnSnapshotAuthority.AuthorityPath;
        string[] outputs = ["output/narrative_response.json", "output/interface_updates.json"];
        TurnRequest? original = null;
        byte[]? requestBytes = null, manifestBytes = null, authorityBytes = null, storyBefore = null;
        JsonObject? declared = null, completed = null;
        string? handled = null;
        var repairs = 0;
        var stages = new List<string> { "fixture-created" };
        Exception? callbackFailure = null;
        ValidationService? observedValidator = null;
        string Physical(string path) => _fs.ResolvePath(path);
        byte[]? Bytes(string path) => File.Exists(Physical(path)) ? File.ReadAllBytes(Physical(path)) : null;
        JsonObject Read(string path) => JsonNode.Parse(File.ReadAllText(Physical(path)))!.AsObject();
        void AssertActors(bool settled)
        {
            var actors = Read(core);
            Assert.Empty(actors["UpdateNPCs"]!.AsArray()); // No full-actor resend of the seeded actor.
            var actor = Assert.Single(actors["NPCsInScene"]!.AsArray())!.AsObject();
            Assert.Equal(JourneyScoutId, actor["NPCId"]!.GetValue<string>());
            Assert.True(actor.ContainsKey("currentActivity"));
            if (settled)
            {
                Assert.Null(actor["currentActivity"]);
                Assert.True(JsonNode.DeepEquals(completed, Assert.Single(actor["completedActivities"]!.AsArray())));
            }
            else
            {
                Assert.Equal(0, actor["currentActivity"]!["timeSpentMinutes"]!.GetValue<int>());
                Assert.Equal("Active", actor["currentActivity"]!["activeState"]!.GetValue<string>());
                Assert.Empty(actor["completedActivities"]!.AsArray());
            }
        }

        try
        {
            var input = new LivingWorldRepairInput(() => _fs.FileExists(repairPath), () =>
            {
                var raw = File.ReadAllText(Physical(repairPath));
                if (raw == handled) return;
                handled = raw;
                stages.Add("repair-packet-" + (repairs + 1));
                try
                {
                    Assert.InRange(++repairs, 1, 2);
                    var repair = JsonNode.Parse(raw)!;
                    Assert.Equal(original!.SessionId, repair["sessionId"]!.GetValue<string>());
                    Assert.Equal(original.RequestId, repair["requestId"]!.GetValue<string>());
                    Assert.Equal(original.TurnNumber, repair["turnNumber"]!.GetValue<int>());
                    Assert.Equal(1, repair["revalidationAttempt"]!.GetValue<int>());
                    Assert.False(repair["fullTurnResubmissionRequired"]!.GetValue<bool>());
                    Assert.Empty(repair["requiredResubmissionPaths"]!.AsArray());
                    Assert.Equal(requestBytes, Bytes(requestPath));
                    Assert.Equal(manifestBytes, Bytes(manifest));
                    Assert.Equal(authorityBytes, Bytes(authority));
                    Assert.Equal(storyBefore, Bytes(story));
                    var codes = repair["errors"]!.AsArray().Select(e => e!["code"]!.GetValue<string>()).ToArray();
                    if (repairs == 1)
                    {
                        Assert.Equal(new[] { "npc_complete_activity_outcome_required" }, codes);
                        AssertActors(false);
                        if (_fs.FileExists(MortalLivingWorldSource.HistoryPath))
                            Assert.Empty(Read(MortalLivingWorldSource.HistoryPath)["entries"]!.AsArray());
                        var sidecar = Read(commands);
                        var command = Assert.Single(sidecar["completeNPCActivities"]!.AsArray())!.AsObject();
                        Assert.False(command.ContainsKey("finalOutcome"));
                        command["finalOutcome"] = outcome; // Repair only this original command; never write actor, clock or history.
                        File.WriteAllText(Physical(commands), sidecar.ToJsonString());
                        File.WriteAllBytes(Physical(MortalLivingWorldSource.ReportPath), Bytes(MortalLivingWorldSource.ReportPath)!);
                    }
                    else
                    {
                        Assert.Equal(2, codes.Length);
                        Assert.All(codes, code => Assert.Equal("accepted_turn_stale_player_facing_output_after_canonical_repair", code));
                        AssertActors(true);
                        Assert.False(Read(commands).ContainsKey("completeNPCActivities"));
                    }
                    foreach (var path in outputs) File.WriteAllBytes(Physical(path), Bytes(path)!);
                    File.WriteAllText(Physical("game_state/control/validation_repair_ready.json"), JsonSerializer.Serialize(new
                    {
                        sessionId = original.SessionId, requestId = original.RequestId, turnNumber = original.TurnNumber,
                        updatedAtUtc = DateTime.UtcNow.ToString("O"), status = "success"
                    }));
                }
                catch (Exception error) { callbackFailure ??= error; throw; }
            });

            stages.Add("construct-engine");
            var runtime = await CreateMortalLivingWorldJourneyAsync(async request =>
            {
                stages.Add("terminal-wait-author-entered");
                try
                {
                    Assert.Null(original); original = request;
                    requestBytes = Assert.IsType<byte[]>(Bytes(requestPath));
                    manifestBytes = Assert.IsType<byte[]>(Bytes(manifest));
                    authorityBytes = Assert.IsType<byte[]>(Bytes(authority));
                    storyBefore = Bytes(story);
                    AssertActors(false);
                    stages.Add("verified-original-actor-and-authority");
                    var actor = Read(core)["NPCsInScene"]![0]!;
                    var activity = actor["currentActivity"]!;
                    var name = activity["activityName"]!.GetValue<string>();
                    completed = new JsonObject { ["activityName"] = name, ["completionTurn"] = request.TurnNumber,
                        ["finalOutcome"] = outcome, ["narrativeSummary"] = summary };
                    var time = Read(MortalLivingWorldSource.WorldTimePath);
                    var from = time["currentTimeInMinutes"]!.GetValue<int>(); Assert.Equal(480, from);
                    time["currentTimeInMinutes"] = from + 480; time["timeOfDay"] = "16:00";
                    await _fs.WriteFileAtomicAsync(MortalLivingWorldSource.WorldTimePath, time.ToJsonString());
                    var audit = new JsonArray();
                    foreach (var scope in new[] { "npcs", "factions", "locations", "projects", "world" })
                        audit.Add(new JsonObject { ["scope"] = scope, ["reason"] = "Only the existing scout's survey concludes in this eight-hour interval." });
                    JsonObject Target() => JourneyReference("npcActivity", JourneyScoutId);
                    declared = new JsonObject {
                        ["schemaVersion"] = 1, ["sessionId"] = request.SessionId, ["requestId"] = request.RequestId,
                        ["turn"] = request.TurnNumber, ["fromMinute"] = from, ["toMinute"] = from + 480, ["resolution"] = "changes",
                        ["audit"] = audit, ["events"] = new JsonArray(new JsonObject {
                            ["eventId"] = eventId, ["fromMinute"] = from, ["toMinute"] = from + 480,
                            ["actors"] = new JsonArray(JourneyReference("npc", JourneyScoutId)), ["targets"] = new JsonArray(Target()),
                            ["cause"] = new JsonObject { ["summary"] = summary, ["references"] = new JsonArray(JourneyReference("location", JourneyLocationId)) },
                            ["effects"] = new JsonArray(JourneyEffect("end", Target(), "currentActivity", activity, null),
                                JourneyEffect("record", Target(), "completedActivities", actor["completedActivities"], new JsonArray(completed.DeepClone()))),
                            ["consequences"] = new JsonArray(new JsonObject { ["summary"] = summary, ["effectIds"] = new JsonArray("end", "record") }),
                            ["knowledge"] = new JsonObject { ["player"] = "known", ["newsReferences"] = new JsonArray(eventId) }
                        }) };
                    await _fs.WriteFileAtomicAsync(MortalLivingWorldSource.ReportPath, declared.ToJsonString());
                    await _fs.WriteFileAtomicAsync(commands, new JsonObject { ["completeNPCActivities"] = new JsonArray(new JsonObject {
                        ["NPCId"] = JourneyScoutId, ["NPCName"] = "Mira the scout", ["activityName"] = name,
                        ["finalState"] = "Completed", ["narrativeSummary"] = summary }) }.ToJsonString());
                    await _fs.WriteFileAtomicAsync(news, new JsonObject { ["worldEventsLog"] = new JsonArray(new JsonObject {
                        ["eventId"] = eventId, ["title"] = "Доклад разведчицы", ["description"] = narrative,
                        ["visibility"] = "public", ["locationId"] = JourneyLocationId }) }.ToJsonString());
                    await WriteJourneyScoutJournalAsync(request.TurnNumber, 0);
                    await WriteMortalLivingWorldJourneyOutputAsync(request, narrative, [MortalLivingWorldSource.WorldTimePath,
                        MortalLivingWorldSource.ReportPath, news, "game_state/npcs/npc_journals.json", commands], JourneyActorReasoning(0));
                    stages.Add("terminal-output-written");
                }
                catch (Exception error) { callbackFailure ??= error; throw; }
            }, SeedMortalLivingWorldJourneyActorsAsync, input);

            observedValidator = runtime.ValidationService;
            stages.Add("process-player-turn-entered");
            await runtime.Engine.ProcessPlayerTurn(action);
            stages.Add("process-player-turn-returned");
            Assert.InRange(repairs, 1, 2);
            Assert.Equal(42, runtime.GameLoop.TurnNumber);
            AssertActors(true);
            Assert.Equal(960, Read(MortalLivingWorldSource.WorldTimePath)["currentTimeInMinutes"]!.GetValue<int>());
            Assert.False(Read(commands).ContainsKey("completeNPCActivities"));
            var historyPath = MortalLivingWorldSource.HistoryPath;
            var history = MortalLivingWorldReceipt.ValidateHistory(Read(historyPath));
            var receipt = Assert.Single(history["entries"]!.AsArray())!;
            Assert.Equal(original!.SessionId, receipt["sessionId"]!.GetValue<string>());
            Assert.Equal(original.RequestId, receipt["requestId"]!.GetValue<string>());
            Assert.Equal(original.TurnNumber, receipt["turn"]!.GetValue<int>());
            Assert.Equal(JsonNode.Parse(System.Text.Encoding.UTF8.GetString(manifestBytes!).TrimStart('\uFEFF'))!["manifestPayloadHash"]!.GetValue<string>(),
                receipt["snapshotToken"]!.GetValue<string>());
            Assert.True(JsonNode.DeepEquals(declared, receipt["report"]));
            var row = JsonNode.Parse(Assert.Single(File.ReadAllLines(Physical(story))))!;
            Assert.Equal(original.TurnNumber, row["turn"]!.GetValue<int>());
            Assert.Equal(action, row["player"]!.GetValue<string>());
            Assert.Equal(narrative, row["narrative"]!.GetValue<string>());
            foreach (var path in new[] { requestPath, manifest, authority, repairPath, MortalLivingWorldSource.ReportPath,
                "ready/turn_complete.json", "ready/turn_error.json", "game_state/control/validation_repair_ready.json" })
                Assert.False(_fs.FileExists(path), path);
            Assert.False(Directory.Exists(Physical(LiveTurnPreparationService.PendingTurnSnapshotDirectory)));
            var settledPaths = new[] { core, commands, historyPath, story };
            var settledBytes = settledPaths.Select(Bytes).ToArray();
            await runtime.StateManager.RefreshGameStateAsync();
            for (int i = 0; i < settledPaths.Length; i++) Assert.Equal(settledBytes[i], Bytes(settledPaths[i]));
        }
        catch (Exception failure)
        {
            // Capture only an actual failure before CleanupOwnedFixture removes its original session.
            // Diagnostic collection neither synthesizes a repair nor changes the original test failure.
            var directory = Path.Combine(TestDiagnosticPaths.Root, "npc-completion-engine", Guid.NewGuid().ToString("N"));
            _directGachaOutput?.WriteLine("Completion engine diagnostics: " + directory);
            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "stage.json"), JsonSerializer.Serialize(new {
                    stages, repairs, sessionId = original?.SessionId, requestId = original?.RequestId, turnNumber = original?.TurnNumber,
                    failure = failure.ToString(), callbackException = callbackFailure?.ToString()
                }));
                File.WriteAllText(Path.Combine(directory, "engine.log"), _journeyLogger.Describe());
                foreach (var path in new[] { core, commands, requestPath, manifest, authority, repairPath, story,
                    MortalLivingWorldSource.ReportPath, MortalLivingWorldSource.HistoryPath,
                    "game_state/control/validation_diagnostic_failure_report.json", "error_log.txt",
                    "ready/turn_complete.json", "ready/turn_error.json" })
                {
                    var actual = Physical(path);
                    if (File.Exists(actual))
                        File.Copy(actual, Path.Combine(directory, path.Replace('/', '_')));
                }
                if (requestBytes is not null) File.WriteAllBytes(Path.Combine(directory, "original-request.json"), requestBytes);
                if (manifestBytes is not null) File.WriteAllBytes(Path.Combine(directory, "original-manifest.json"), manifestBytes);
                if (authorityBytes is not null) File.WriteAllBytes(Path.Combine(directory, "original-authority.json"), authorityBytes);
                // Only when pre-author staging did not reach a request. This is a fresh diagnostic,
                // not an assertion that the earlier stage necessarily saw the same issues.
                if (original is null && observedValidator is not null)
                    File.WriteAllText(Path.Combine(directory, "post-failure-validation.json"),
                        JsonSerializer.Serialize(await observedValidator.ValidateGameStateAsync()));
            }
            catch (Exception diagnosticError) { _directGachaOutput?.WriteLine("Diagnostic capture incomplete: " + diagnosticError); }
            throw;
        }
    }
}
