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
        Assert.Equal(profile.Role, task.Role);
        Assert.Equal(WorkerTaskType.ValidationRepair, task.TaskType);
        Assert.Equal(profile.TimeoutSeconds, task.TimeoutSeconds);
        Assert.Contains(task.AcceptanceCriteria, criterion =>
            criterion.Contains("worker-proposal-v1", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(task.AcceptanceCriteria, criterion =>
            criterion.Contains("validation", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(task.ForbiddenActions, action =>
            action.Contains("canonical game_session files", StringComparison.OrdinalIgnoreCase));
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

    [Fact]
    public void BuildValidationRepairTask_PreservesActorCoordinatesAndTargetsCanonicalFile()
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
                "game_state/npcs/npc_core.json.NPCsInScene[0].materialization.sections.inventory",
                IssueSeverity.Error,
                "Первичная материализация не объясняет секцию inventory.",
                code: "actor_materialization_section_missing",
                actor: "mortal_npc:npc_repair_target",
                section: "inventory",
                expected: "populated or empty_by_design with reason",
                actual: "missing")
        };
        var contextHashes = new Dictionary<string, string>
        {
            ["game_state/npcs/npc_core.json"] = "sha256-npc-core"
        };

        var task = GmWorkerTaskPacketBuilder.BuildValidationRepairTask(
            profile,
            "worker_task_actor_materialization",
            sourceTurn,
            issues,
            contextHashes,
            "2026-06-20T00:00:00Z");

        Assert.Equal(new[] { "game_state/npcs/npc_core.json" }, task.AllowedProposalPaths);
        var packagedIssue = Assert.Single(task.ValidationIssues);
        Assert.Equal("mortal_npc:npc_repair_target", packagedIssue.Actor);
        Assert.Equal("inventory", packagedIssue.Section);
        Assert.Equal("populated or empty_by_design with reason", packagedIssue.Expected);
        Assert.Equal("missing", packagedIssue.Actual);
        Assert.Equal(
            "game_state/npcs/npc_core.json.NPCsInScene[0].materialization.sections.inventory",
            packagedIssue.Path);
        Assert.Equal(
            "game_state/npcs/npc_core.json",
            Assert.Single(task.ContextFiles).Path);
        Assert.Contains(task.AcceptanceCriteria, criterion =>
            criterion.Contains("protected actor data", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(task.ForbiddenActions, action =>
            action.Contains("untargeted actor", StringComparison.OrdinalIgnoreCase));
    }
}
