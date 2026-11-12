using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.WebUi;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWebCommandServiceTests
{
    [Theory]
    [InlineData("skill_upper", "Fireball", "3")]
    [InlineData("skill_lower", "fireball", "7")]
    public async Task ExecuteAsync_SkillIdentity_UsesExactCanonicalMastery(
        string selector, string name, string expectedLevel)
    {
        await WriteExactSkillSiblingsAsync(includeIds: true);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest($"/skills skill {selector}"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var dossier = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers),
            block => block.EntityType == "skill");
        Assert.Equal($"Навык: {name}", dossier.Title);
        var mastery = Assert.Single(EnumerateKeyValueGrids(dossier).SelectMany(grid => grid.Items),
            item => item.Key == "Мастерство");
        Assert.Equal(expectedLevel, mastery.Value);
    }

    [Fact]
    public async Task ExecuteAsync_SkillIdentity_ExactNameSelectsLowerCaseSibling()
    {
        await WriteExactSkillSiblingsAsync(includeIds: true);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/skills skill fireball"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var dossier = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers),
            block => block.EntityType == "skill");
        Assert.Equal("Навык: fireball", dossier.Title);
        Assert.Contains("Lower sibling description", CollectBlockText(result.Blocks), StringComparison.Ordinal);
        Assert.DoesNotContain("Upper sibling description", CollectBlockText(result.Blocks), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_SkillIdentity_Overview_PreservesExactNameSiblingsWithoutIds()
    {
        await WriteExactSkillSiblingsAsync(includeIds: false);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/skills"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var upper = Assert.Single(result.Actions, action => action.Label == "Открыть: Fireball");
        var lower = Assert.Single(result.Actions, action => action.Label == "Открыть: fireball");
        Assert.NotEqual(upper.Id, lower.Id);
        Assert.NotEqual(upper.Command, lower.Command);
        foreach (var action in new[] { upper, lower })
        {
            var detail = await _service.ExecuteAsync(new ExplorerWebCommandRequest(action.Command));
            var dossier = Assert.Single(detail.Blocks.SelectMany(EnumerateEntityDossiers),
                block => block.EntityType == "skill");
            Assert.Equal("Навык: " + action.Label["Открыть: ".Length..], dossier.Title);
        }
    }

    private async Task WriteExactSkillSiblingsAsync(bool includeIds)
    {
        var upper = new JsonObject
        {
            ["skillName"] = "Fireball",
            ["skillDescription"] = "Upper sibling description"
        };
        var lower = new JsonObject
        {
            ["skillName"] = "fireball",
            ["skillDescription"] = "Lower sibling description"
        };
        if (includeIds)
        {
            upper["skillId"] = "skill_upper";
            lower["skillId"] = "skill_lower";
        }
        await _fs.WriteFileAtomicAsync("game_state/player/skills_active.json",
            new JsonObject { ["activeSkillChanges"] = new JsonArray(upper, lower) }.ToJsonString());
        await _fs.WriteFileAtomicAsync("game_state/player/skills_passive.json", "{\"passiveSkillChanges\":[]}");
        await _fs.WriteFileAtomicAsync("game_state/player/skill_mastery.json", """
        {
          "skillMasteryChanges": [
            { "skillName": "Fireball", "newMasteryLevel": 3, "newCurrentMasteryProgress": 2, "newMasteryProgressNeeded": 8 },
            { "skillName": "fireball", "newMasteryLevel": 7, "newCurrentMasteryProgress": 4, "newMasteryProgressNeeded": 16 }
          ]
        }
        """);
    }
}
