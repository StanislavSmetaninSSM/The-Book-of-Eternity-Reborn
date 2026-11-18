using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class FactionCoreChangesTests
{
    [Fact]
    public async Task Normalize_CompleteProgressionWithNullPriorities_PreservesNullThroughFinalValidation()
    {
        var fixture = await WriteExistingFixtureAsync(current =>
            current[FactionCoreChangesContract.PropertyName] = new JsonArray(BuildCommand(
                "faction_watch", "progressionAndPower", BuildCompleteGroup("progressionAndPower"))),
            mutatePreTurn: original => FindFaction(original, "faction_watch").Remove("customArchetypePriorities"));
        var baselineBytes = await _fs.ReadFileBytesAsync(fixture.Backups[CorePath]);
        var preIssues = await _validator.ValidateFactionCoreChangesBeforeNormalizationAsync();
        Assert.Empty(preIssues);
        await CreateNormalizer().NormalizeAccumulatedStateAsync(fixture.Backups);
        var core = await ReadObjectAsync(CorePath);
        Assert.False(core.ContainsKey(FactionCoreChangesContract.PropertyName));
        var target = FindFaction(core, "faction_watch");
        Assert.True(target.ContainsKey("customArchetypePriorities"));
        Assert.Null(target["customArchetypePriorities"]);
        foreach (var field in BuildCompleteGroup("progressionAndPower"))
            Assert.True(JsonNode.DeepEquals(field.Value, target[field.Key]), field.Key);
        Assert.True(JsonNode.DeepEquals(FindFaction(fixture.PreTurnCore, "faction_bridge_compact"),
            FindFaction(core, "faction_bridge_compact")));
        Assert.Equal(baselineBytes, await _fs.ReadFileBytesAsync(fixture.Backups[CorePath]));
        var after = await _fs.ReadFileBytesAsync(CorePath);
        var issues = await _validator.ValidateGameStateAsync(new GameStateValidationSelection(
            GameStateValidationPhase.WorldQuestCombatFactionStateFiles, [CorePath]));
        Assert.Equal(after, await _fs.ReadFileBytesAsync(CorePath));
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error &&
            issue.FilePath.Contains(".customArchetypePriorities", StringComparison.Ordinal));
    }
}
