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
            var deadline = DateTime.UtcNow.AddSeconds(30);
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
            await WriteExternalRepairPacketAsync(_fs.ResolvePath("game_state/control/validation_repair_ready.json"), JsonSerializer.Serialize(new
            {
                sessionId = request["sessionId"]!.GetValue<string>(),
                requestId = request["requestId"]!.GetValue<string>(),
                turnNumber = request["turnNumber"]!.GetValue<int>(),
                status = "success",
                timestamp = DateTime.UtcNow.ToString("O")
            }), cancellation);
            input.RepairCompleted = true;
            // Keep an owned watchdog after submission: a second refusal must not wait
            // forever or be mistaken for the intended single repair.
            while (!cancellation.IsCancellationRequested)
            {
                if (DateTime.UtcNow >= deadline)
                    throw new TimeoutException("Corrected progression did not finish within the protected repair window.");
                if (File.Exists(requestPath))
                {
                    var current = await File.ReadAllTextAsync(requestPath, cancellation);
                    if (!string.Equals(current, repairText, StringComparison.Ordinal))
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
