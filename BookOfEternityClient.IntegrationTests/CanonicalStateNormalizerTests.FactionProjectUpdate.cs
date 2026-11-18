using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NormalizeAccumulatedStateAsync_FactionProjectPartialUpdatePreservesOmittedFields(bool explicitRename)
    {
        var target = CreateFactionProjectCompletionActiveProject("project_completion_bridge", "Repair the crossing");
        target["timeSpentMinutes"] = 0;
        target["currentStep"] = 0;
        var sibling = CreateFactionProjectCompletionActiveProject("project_completion_supplies", "Restock the watch post");
        var otherOwner = target.DeepClone().AsObject();
        otherOwner["factionId"] = "faction_other_council";
        otherOwner["factionName"] = "Other Council";
        otherOwner["projectName"] = "Other owner's same project ID";
        var historical = CreateFactionProjectCompletionActiveProject("project_completion_survey", "Survey the river bank");
        historical.Remove("activeState");
        historical["finalState"] = "Completed";
        historical["completionTurn"] = 12;
        var original = new JsonObject
        {
            ["activeProjects"] = new JsonArray(target, sibling, otherOwner),
            ["completedProjects"] = new JsonArray(historical)
        };
        var patch = new JsonObject { ["projectId"] = "project_completion_bridge", ["timeSpentMinutes"] = 240, ["currentStep"] = 1 };
        if (explicitRename) patch["projectName"] = "Repair the northern crossing";
        var command = new JsonObject { ["factionId"] = "faction_completion_council",
            ["factionName"] = "Crossing Council", ["projectUpdate"] = patch };
        var submitted = original.DeepClone().AsObject();
        submitted["factionProjectUpdates"] = new JsonArray(command);
        var backups = new Dictionary<string, string>(StringComparer.Ordinal);
        await AddFactionBackupAsync(backups, MortalFactionProjectsPath, original);
        await _fs.WriteFileAtomicAsync(MortalFactionProjectsPath, submitted.ToJsonString());
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """{"currentRealm":"Mortal World","currentIncarnation":1}""");
        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        using (var response = JsonDocument.Parse(new JsonObject {
            ["factionProjectUpdates"] = new JsonArray(command.DeepClone()) }.ToJsonString()))
            Assert.DoesNotContain(validator.ValidateResponse(response.RootElement), issue => issue.Severity == IssueSeverity.Error);
        var originalBytes = await _fs.ReadFileBytesAsync(backups[MortalFactionProjectsPath]);
        var expected = original.DeepClone();
        expected["activeProjects"]![0]!["timeSpentMinutes"] = 240;
        expected["activeProjects"]![0]!["currentStep"] = 1;
        if (explicitRename) expected["activeProjects"]![0]!["projectName"] = "Repair the northern crossing";

        await CreateFactionNormalizer().NormalizeAccumulatedStateAsync(backups);

        var actual = await ReadFactionObjectAsync(MortalFactionProjectsPath);
        Assert.False(actual.ContainsKey("factionProjectUpdates"));
        Assert.True(JsonNode.DeepEquals(expected, actual), actual.ToJsonString());
        Assert.Equal(originalBytes, await _fs.ReadFileBytesAsync(backups[MortalFactionProjectsPath]));
    }
}
