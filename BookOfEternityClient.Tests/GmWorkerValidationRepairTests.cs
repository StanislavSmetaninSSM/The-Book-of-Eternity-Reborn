using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerValidationRepairTests
{
    [Fact]
    public void BuildValidationRepairTask_PackagesValidationIssuesIntoScopedWorkerPacket()
    {
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        var sourceTurn = new WorkerTurnReference
        {
            SessionId = "test-session",
            RequestId = "test-request",
            TurnNumber = 12
        };
        var issues = new[]
        {
            new ValidationIssue(
                "game_state/world/weather.json",
                IssueSeverity.Error,
                "normalizedWeatherState.description is required.",
                code: "normalized_weather_missing_description",
                repairHint: "Add a player-facing weather description.")
        };
        var contextHashes = new Dictionary<string, string>
        {
            ["game_state/world/weather.json"] = "sha256-weather"
        };

        var task = GmWorkerTaskPacketBuilder.BuildValidationRepairTask(
            profile,
            "worker_task_test",
            sourceTurn,
            issues,
            contextHashes,
            "2026-06-20T00:00:00Z");

        Assert.Equal("worker_task_test", task.TaskId);
        Assert.Equal(profile.WorkerId, task.WorkerId);
        Assert.Equal(WorkerTaskType.ValidationRepair, task.TaskType);
        Assert.Contains("game_state/world/weather.json", task.AllowedProposalPaths);
        Assert.Contains(task.ValidationIssues, issue =>
            issue.Code == "normalized_weather_missing_description" &&
            issue.Path == "game_state/world/weather.json");
        Assert.Contains(task.ContextFiles, file =>
            file.Path == "game_state/world/weather.json" &&
            file.Sha256 == "sha256-weather");

        var result = GmWorkerContractValidator.ValidateTaskPacket(task, profile);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }
}
