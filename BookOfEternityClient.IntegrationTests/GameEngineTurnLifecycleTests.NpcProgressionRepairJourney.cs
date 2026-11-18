using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public Task NpcProgressionJourney_RejectedResultRepairsBeforeSingleAcceptedGrant() =>
        RunProgressionJourneyAsync(withConsumers: false, repairInvalidResult: true);

    private async Task ObserveAndRepairProgressionJourneyAsync(
        GameLoop loop, ProgressionJourneyInput input, string beforeActor, CancellationToken cancellation)
    {
        const string repairPath = "game_state/control/validation_repair_request.json";
        // This observer represents the external GM file boundary, not a second client
        // canonical owner. Client FileSystemManager reads would attempt owner admission.
        var requestPath = _fs.ResolvePath(repairPath);
        var corePath = _fs.ResolvePath(NpcCoreChangesContract.NpcCorePath);
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (!File.Exists(requestPath))
            {
                if (DateTime.UtcNow >= deadline) throw new TimeoutException("No actual progression repair request appeared.");
                await Task.Delay(50, cancellation);
            }
            var repairText = await File.ReadAllTextAsync(requestPath, cancellation);
            var request = JsonNode.Parse(repairText)!.AsObject();
            var diagnosticRoot = Environment.GetEnvironmentVariable("BOE_TEST_DIAGNOSTIC_ROOT");
            if (!string.IsNullOrWhiteSpace(diagnosticRoot))
                await File.WriteAllTextAsync(Path.Combine(diagnosticRoot, "1538-repair-request-" + Guid.NewGuid().ToString("N") + ".json"), repairText, cancellation);
            Assert.Contains(request["errors"]!.AsArray(), error =>
                error?["code"]?.GetValue<string>() == "npc_core_changes_progression_invalid" &&
                error?["filePath"]?.GetValue<string>().EndsWith(".NPCCoreChanges[0].progression.experience", StringComparison.Ordinal) == true);
            Assert.False(request["metadataDiagnosticOnly"]!.GetValue<bool>());
            Assert.False(request["fullTurnResubmissionRequired"]!.GetValue<bool>());
            var turn = JsonNode.Parse(await File.ReadAllTextAsync(_fs.ResolvePath("input/turn_request.json"), cancellation))!;
            foreach (var key in new[] { "sessionId", "requestId", "turnNumber" })
                Assert.Equal(turn[key]!.ToJsonString(), request[key]!.ToJsonString());
            Assert.Equal(41, loop.TurnNumber);
            var core = JsonNode.Parse(await File.ReadAllTextAsync(corePath, cancellation))!.AsObject();
            Assert.Equal(beforeActor, Assert.Single(core["NPCsInScene"]!.AsArray())!.ToJsonString());
            var storyPath = _fs.ResolvePath("stories/mortal_life_2.jsonl");
            var story = File.Exists(storyPath) ? await File.ReadAllTextAsync(storyPath, cancellation) : null;
            Assert.True(string.IsNullOrWhiteSpace(story), "Rejected progression must not append accepted story.");
            var command = Assert.Single(core["NPCCoreChanges"]!.AsArray())!.AsObject();
            Assert.Equal(ProgressionJourneyActor, command["NPCId"]!.GetValue<string>());
            Assert.Equal(2, command["progression"]!["experience"]!.GetValue<int>());
            command["progression"]!["experience"] = 1;
            await WriteExternalRepairPacketAsync(corePath, core.ToJsonString(), cancellation);
            await WriteRepairReadyAsync(request, cancellation);

            // Canonical correction requires genuinely fresh player-facing artifacts.
            // Do not hide that second repair or refresh outputs before normalization.
            var outputRequestText = await WaitForChangedRepairRequestAsync(requestPath, repairText, deadline, cancellation);
            var outputRequest = JsonNode.Parse(outputRequestText)!.AsObject();
            var outputErrors = outputRequest["errors"]!.AsArray();
            Assert.NotEmpty(outputErrors);
            Assert.All(outputErrors, error =>
            {
                Assert.Equal("accepted_turn_stale_player_facing_output_after_canonical_repair", error!["code"]!.GetValue<string>());
                Assert.Contains(error["filePath"]!.GetValue<string>(), new[] { "output/narrative_response.json", "output/interface_updates.json" });
            });
            foreach (var key in new[] { "sessionId", "requestId", "turnNumber" })
                Assert.Equal(request[key]!.ToJsonString(), outputRequest[key]!.ToJsonString());
            Assert.False(outputRequest["metadataDiagnosticOnly"]!.GetValue<bool>());
            Assert.False(outputRequest["fullTurnResubmissionRequired"]!.GetValue<bool>());
            Assert.Equal(41, loop.TurnNumber);
            var correctedCore = await File.ReadAllTextAsync(corePath, cancellation);
            var corrected = JsonNode.Parse(correctedCore)!.AsObject();
            var correctedActor = Assert.Single(corrected["NPCsInScene"]!.AsArray())!;
            Assert.Equal(2, correctedActor["level"]!.GetValue<int>());
            Assert.Equal(1, correctedActor["experience"]!.GetValue<int>());
            Assert.False(corrected.ContainsKey("NPCCoreChanges"));
            Assert.True(!File.Exists(storyPath) || string.IsNullOrWhiteSpace(await File.ReadAllTextAsync(storyPath, cancellation)));
            var timestamp = DateTime.UtcNow.ToString("O");
            await WriteExternalRepairPacketAsync(_fs.ResolvePath("output/narrative_response.json"), JsonSerializer.Serialize(new
            {
                response = "Лев завершил упражнение и стал опытнее. Его уровень повысился до 2.", timestamp
            }), cancellation);
            await WriteExternalRepairPacketAsync(_fs.ResolvePath("output/interface_updates.json"), JsonSerializer.Serialize(new
            {
                dialogueOptions = Array.Empty<object>(), timestamp
            }), cancellation);
            Assert.Equal(correctedCore, await File.ReadAllTextAsync(corePath, cancellation));
            await WriteRepairReadyAsync(outputRequest, cancellation);
            repairText = outputRequestText;
            input.RepairCompleted = true;
            // Keep an owned watchdog after submission: a second refusal must not wait
            // forever or be mistaken for the intended single repair.
            while (!cancellation.IsCancellationRequested)
            {
                if (DateTime.UtcNow >= deadline)
                    throw new TimeoutException("Corrected progression did not finish within the protected repair window.");
                if (File.Exists(requestPath))
                {
                    string? current;
                    try { current = await File.ReadAllTextAsync(requestPath, cancellation); }
                    catch (FileNotFoundException) { current = null; } // Successful validation removes this request.
                    if (current != null && !string.Equals(current, repairText, StringComparison.Ordinal))
                        throw new InvalidOperationException("Unexpected subsequent repair request: " + current);
                }
                await Task.Delay(50, cancellation);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error)
        {
            input.RepairFailure = error;
            throw;
        }
    }

    private async Task WriteRepairReadyAsync(JsonObject request, CancellationToken cancellation) =>
        await WriteExternalRepairPacketAsync(_fs.ResolvePath("game_state/control/validation_repair_ready.json"), JsonSerializer.Serialize(new
        {
            sessionId = request["sessionId"]!.GetValue<string>(),
            requestId = request["requestId"]!.GetValue<string>(),
            turnNumber = request["turnNumber"]!.GetValue<int>(),
            status = "success", timestamp = DateTime.UtcNow.ToString("O")
        }), cancellation);

    private static async Task<string> WaitForChangedRepairRequestAsync(
        string path, string previous, DateTime deadline, CancellationToken cancellation)
    {
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(path))
            {
                string? current;
                try { current = await File.ReadAllTextAsync(path, cancellation); }
                catch (FileNotFoundException) { current = null; }
                if (current != null && !string.Equals(current, previous, StringComparison.Ordinal)) return current;
            }
            await Task.Delay(50, cancellation);
        }
        throw new TimeoutException("Expected fresh-output repair request did not appear.");
    }

    private static async Task WriteExternalRepairPacketAsync(string path, string content, CancellationToken cancellation)
    {
        var temporary = path + ".fixture-" + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllTextAsync(temporary, content, cancellation);
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
