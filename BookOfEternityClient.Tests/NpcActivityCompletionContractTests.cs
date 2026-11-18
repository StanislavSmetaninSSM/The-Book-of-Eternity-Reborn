using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Terminal command DATA validation only, never a publication grant.</summary>
public sealed class NpcActivityCompletionContractTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-npc-terminal-contract-" + Guid.NewGuid().ToString("N"));
    private readonly ValidationService _validator;
    public NpcActivityCompletionContractTests()
    {
        Directory.CreateDirectory(_root);
        var files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        files.EnsureDirectoryStructure();
        _validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
    }

    [Theory]
    [InlineData("Completed", "Success", null)]
    [InlineData("Completed", "SuccessWithComplication", null)]
    [InlineData("Completed", "Failure", null)]
    [InlineData("Abandoned", "Failure", null)]
    [InlineData("Abandoned", "Success", "npc_complete_activity_outcome_state_conflict")]
    [InlineData("Abandoned", "SuccessWithComplication", "npc_complete_activity_outcome_state_conflict")]
    [InlineData("Completed", null, "npc_complete_activity_outcome_required")]
    [InlineData("Completed", "Unknown", "npc_complete_activity_outcome_invalid")]
    public void ExplicitOutcomeTable(string state, string? outcome, string? expectedCode)
    {
        var command = Command(state, outcome);
        using var document = JsonDocument.Parse(new JsonObject { ["completeNPCActivities"] = new JsonArray(command) }.ToJsonString());
        var issues = _validator.ValidateResponse(document.RootElement);
        var terminal = issues.Where(issue => issue.Code?.StartsWith("npc_complete_activity_", StringComparison.Ordinal) == true).ToArray();
        if (expectedCode is null) Assert.Empty(terminal);
        else Assert.Contains(terminal, issue => issue.Code == expectedCode && issue.Severity == IssueSeverity.Error && !string.IsNullOrWhiteSpace(issue.RepairHint));
    }

    [Fact]
    public void SuppliedCompletionTurnIsNotClientAuthority()
    {
        var command = Command("Completed", "Success");command["completionTurn"] = 42;
        using var document = JsonDocument.Parse(new JsonObject { ["completeNPCActivities"] = new JsonArray(command) }.ToJsonString());
        Assert.Contains(_validator.ValidateResponse(document.RootElement), issue => issue.Code == "npc_complete_activity_turn_client_owned");
    }

    [Theory]
    [InlineData("broken")]
    [InlineData("fixed")]
    public void ExistingActivityNameFixturesRetainExplicitOutcomeContract(string variant)
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "FileSystemExample", "validator_fixtures",
            "npc_complete_activity_name", variant, "npc_activities.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("Success", document.RootElement.GetProperty("completeNPCActivities")[0]
            .GetProperty("finalOutcome").GetString());
        Assert.DoesNotContain(_validator.ValidateResponse(document.RootElement), issue =>
            issue.Code?.StartsWith("npc_complete_activity_outcome_", StringComparison.Ordinal) == true ||
            issue.Code == "npc_complete_activity_turn_client_owned");
        // Name cross-reference validity is intentionally not established by response-only validation.
    }

    private static JsonObject Command(string state, string? outcome)
    {
        var command = new JsonObject { ["NPCId"] = "scout", ["NPCName"] = "Mira", ["activityName"] = "Survey", ["finalState"] = state, ["narrativeSummary"] = "The survey ended." };
        if (outcome is not null) command["finalOutcome"] = outcome;
        return command;
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
