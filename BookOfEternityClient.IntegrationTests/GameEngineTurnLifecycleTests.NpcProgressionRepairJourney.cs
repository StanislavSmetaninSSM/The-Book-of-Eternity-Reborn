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
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!_fs.FileExists(repairPath))
            {
                if (DateTime.UtcNow >= deadline) throw new TimeoutException("No actual progression repair request appeared.");
                await Task.Delay(50, cancellation);
            }
            var repairText = (await _fs.ReadFileAsync(repairPath))!;
            var request = JsonNode.Parse(repairText)!.AsObject();
            Assert.Contains("progression", repairText, StringComparison.OrdinalIgnoreCase);
            Assert.False(request["metadataDiagnosticOnly"]!.GetValue<bool>());
            Assert.False(request["fullTurnResubmissionRequired"]!.GetValue<bool>());
            var turn = JsonNode.Parse((await _fs.ReadFileAsync("input/turn_request.json"))!)!;
            foreach (var key in new[] { "sessionId", "requestId", "turnNumber" })
                Assert.Equal(turn[key]!.ToJsonString(), request[key]!.ToJsonString());
            Assert.Equal(41, loop.TurnNumber);
            var core = JsonNode.Parse((await _fs.ReadFileAsync(NpcCoreChangesContract.NpcCorePath))!)!.AsObject();
            Assert.Equal(beforeActor, Assert.Single(core["NPCsInScene"]!.AsArray())!.ToJsonString());
            var story = await _fs.ReadFileAsync("stories/mortal_life_2.jsonl");
            Assert.True(string.IsNullOrWhiteSpace(story), "Rejected progression must not append accepted story.");
            var command = Assert.Single(core["NPCCoreChanges"]!.AsArray())!.AsObject();
            Assert.Equal(ProgressionJourneyActor, command["NPCId"]!.GetValue<string>());
            Assert.Equal(2, command["progression"]!["experience"]!.GetValue<int>());
            command["progression"]!["experience"] = 1;
            await _fs.WriteFileAtomicAsync(NpcCoreChangesContract.NpcCorePath, core.ToJsonString());
            await WriteJsonAsync("game_state/control/validation_repair_ready.json", new
            {
                sessionId = request["sessionId"]!.GetValue<string>(),
                requestId = request["requestId"]!.GetValue<string>(),
                turnNumber = request["turnNumber"]!.GetValue<int>(),
                timestamp = DateTime.UtcNow.ToString("O")
            });
            input.RepairCompleted = true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error)
        {
            input.RepairFailure = error;
            throw;
        }
    }
}
