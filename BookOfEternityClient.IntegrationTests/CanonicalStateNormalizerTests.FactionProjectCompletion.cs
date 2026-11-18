using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests
{
    [Fact]
    public async Task NormalizeAccumulatedStateAsync_FactionProjectCompletion_PreservesIntegerTurnSiblingsAndHistory()
    {
        var command = CreateFactionProjectCompletionCommand();
        command["completionTurn"] = 44;
        var (original, backups) = await ArrangeFactionProjectCompletionAsync(command);
        var expected = original.DeepClone().AsObject();
        var expectedCompleted = expected["activeProjects"]![0]!.DeepClone().AsObject();
        expectedCompleted.Remove("activeState");
        expectedCompleted["finalState"] = "Completed";
        expectedCompleted["completionTurn"] = 44;
        expected["activeProjects"]!.AsArray().RemoveAt(0);
        expected["completedProjects"]!.AsArray().Add(expectedCompleted);
        var originalBytes = await _fs.ReadFileBytesAsync(backups[MortalFactionProjectsPath]);

        await CreateFactionNormalizer().NormalizeAccumulatedStateAsync(backups);

        var actual = await ReadFactionObjectAsync(MortalFactionProjectsPath);
        var completed = Assert.Single(actual["completedProjects"]!.AsArray().OfType<JsonObject>(),
            project => project["projectId"]!.GetValue<string>() == "project_completion_bridge");
        var turn = Assert.IsAssignableFrom<JsonValue>(completed["completionTurn"]);
        Assert.True(turn.TryGetValue<int>(out var completionTurn),
            "Canonical completionTurn must remain a JSON Int32, not a string or inferred default.");
        Assert.Equal(44, completionTurn);
        Assert.False(actual.ContainsKey("completeFactionProjects"));
        // Whole-root equality protects every remaining field of the completed
        // project, the untouched active sibling and immutable completed history.
        Assert.True(JsonNode.DeepEquals(expected, actual), actual.ToJsonString());
        Assert.Equal(originalBytes, await _fs.ReadFileBytesAsync(backups[MortalFactionProjectsPath]));

        var validator = new ValidationService(_fs,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(new GameStateValidationSelection(
            GameStateValidationPhase.WorldQuestCombatFactionStateFiles, [MortalFactionProjectsPath]));
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("numeric-string")]
    [InlineData("negative")]
    [InlineData("fraction")]
    [InlineData("overflow")]
    [InlineData("boolean")]
    [InlineData("object")]
    public async Task NormalizeAccumulatedStateAsync_FactionProjectCompletion_InvalidTurnFailsWithoutMutation(
        string defect)
    {
        var command = CreateFactionProjectCompletionCommand();
        switch (defect)
        {
            case "missing":
                break;
            case "null":
                command["completionTurn"] = null;
                break;
            case "numeric-string":
                command["completionTurn"] = "44";
                break;
            case "negative":
                command["completionTurn"] = -1;
                break;
            case "fraction":
                command["completionTurn"] = 44.5;
                break;
            case "overflow":
                command["completionTurn"] = 2147483648L;
                break;
            case "boolean":
                command["completionTurn"] = true;
                break;
            case "object":
                command["completionTurn"] = new JsonObject { ["turn"] = 44 };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(defect), defect, null);
        }
        var (_, backups) = await ArrangeFactionProjectCompletionAsync(command);
        var currentBytes = await _fs.ReadFileBytesAsync(MortalFactionProjectsPath);
        var originalBytes = await _fs.ReadFileBytesAsync(backups[MortalFactionProjectsPath]);
        Assert.NotNull(currentBytes);
        Assert.NotNull(originalBytes);

        // The raw completion shape validator does not currently require a turn.
        // The actual reducer must refuse an invalid value before publishing or
        // consuming this tracker, rather than parse it or guess a valid integer.
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateFactionNormalizer().NormalizeAccumulatedStateAsync(backups));

        Assert.Equal(currentBytes, await _fs.ReadFileBytesAsync(MortalFactionProjectsPath));
        Assert.Equal(originalBytes, await _fs.ReadFileBytesAsync(backups[MortalFactionProjectsPath]));
    }

    private static JsonObject CreateFactionProjectCompletionCommand() => new()
    {
        ["factionId"] = "faction_completion_council",
        ["factionName"] = "Crossing Council",
        ["projectId"] = "project_completion_bridge",
        ["projectName"] = "Repair the crossing",
        ["finalState"] = "Completed"
    };

    private async Task<(JsonObject Original, Dictionary<string, string> Backups)>
        ArrangeFactionProjectCompletionAsync(JsonObject command)
    {
        var target = CreateFactionProjectCompletionActiveProject(
            "project_completion_bridge", "Repair the crossing");
        var sibling = CreateFactionProjectCompletionActiveProject(
            "project_completion_supplies", "Restock the watch post");
        sibling["timeSpentMinutes"] = 120;
        sibling["currentStep"] = 1;
        var historical = CreateFactionProjectCompletionActiveProject(
            "project_completion_survey", "Survey the river bank");
        historical.Remove("activeState");
        historical["finalState"] = "Completed";
        historical["completionTurn"] = 12;
        var original = new JsonObject
        {
            ["activeProjects"] = new JsonArray(target, sibling),
            ["completedProjects"] = new JsonArray(historical)
        };
        var backups = new Dictionary<string, string>(StringComparer.Ordinal);
        await AddFactionBackupAsync(backups, MortalFactionProjectsPath, original);
        var current = original.DeepClone().AsObject();
        current["completeFactionProjects"] = new JsonArray(command);
        await _fs.WriteFileAtomicAsync(MortalFactionProjectsPath,
            current.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json",
            """{"currentRealm":"Mortal World","currentIncarnation":1}""");
        return (original, backups);
    }

    private static JsonObject CreateFactionProjectCompletionActiveProject(string id, string name) => new()
    {
        ["factionId"] = "faction_completion_council",
        ["factionName"] = "Crossing Council",
        ["projectId"] = id,
        ["projectName"] = name,
        ["description"] = "A documented council project with recorded resources and progress.",
        ["activeState"] = "Active",
        ["totalResourceCost"] = new JsonArray(),
        ["resourcesSpent"] = new JsonArray(),
        ["totalTimeCostMinutes"] = 480,
        ["timeSpentMinutes"] = 480,
        ["totalSteps"] = 2,
        ["currentStep"] = 2
    };
}
