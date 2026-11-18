using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Public validation DATA boundary; not accepted-turn repair or publication authority.</summary>
public sealed class NpcActivityCommandCompositionTests : IDisposable
{
    private const string PathName = "game_state/npcs/npc_activities.json";
    private const string Code = "npc_activity_update_completion_composition_forbidden";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-npc-composition-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemManager _files;
    private readonly ValidationService _validator;

    public NpcActivityCommandCompositionTests()
    {
        Directory.CreateDirectory(_root);
        _files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        _files.EnsureDirectoryStructure();
        _validator = new ValidationService(_files, NullLogger<ValidationService>.Instance);
    }

    public static IEnumerable<object[]> Cases()
    {
        foreach (var response in new[] { false, true })
        foreach (var scenario in new[] { "both-same", "both-different", "update-only", "complete-only", "update-empty-complete", "complete-empty-update" })
            yield return new object[] { response, scenario };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task PublicValidationRejectsWholeSidecarCompositionWithoutConsumingCommands(bool response, string scenario)
    {
        var commands = new JsonObject();
        if (scenario is "both-same" or "both-different" or "update-only" or "update-empty-complete")
            commands["NPCActivityUpdates"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = "scout", ["NPCName"] = "Mira",
                ["activityUpdate"] = new JsonObject { ["timeSpentMinutes"] = 480, ["currentStepNumber"] = 2 }
            });
        if (scenario is "both-same" or "both-different" or "complete-only" or "complete-empty-update")
            commands["completeNPCActivities"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = scenario == "both-different" ? "other" : "scout", ["NPCName"] = scenario == "both-different" ? "Other" : "Mira",
                ["activityName"] = "Scout crossing", ["finalState"] = "Completed", ["narrativeSummary"] = "The crossing was surveyed."
            });
        if (scenario == "update-empty-complete") commands["completeNPCActivities"] = new JsonArray();
        if (scenario == "complete-empty-update") commands["NPCActivityUpdates"] = new JsonArray();
        var raw = commands.ToJsonString();
        List<ValidationIssue> issues;
        if (response)
        {
            using var document = JsonDocument.Parse(raw);
            issues = _validator.ValidateResponse(document.RootElement);
            Assert.False(File.Exists(_files.ResolvePath(PathName)));
        }
        else
        {
            await _files.WriteFileAtomicAsync(PathName, raw);
            var before = File.ReadAllBytes(_files.ResolvePath(PathName));
            issues = await _validator.ValidateGameStateAsync();
            Assert.Equal(before, File.ReadAllBytes(_files.ResolvePath(PathName)));
        }
        var conflict = issues.Where(issue => issue.Code == Code).ToArray();
        if (scenario.StartsWith("both-", StringComparison.Ordinal))
        {
            var issue = Assert.Single(conflict);
            Assert.Equal(IssueSeverity.Error, issue.Severity);
            Assert.False(string.IsNullOrWhiteSpace(issue.RepairHint));
        }
        else Assert.Empty(conflict);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
