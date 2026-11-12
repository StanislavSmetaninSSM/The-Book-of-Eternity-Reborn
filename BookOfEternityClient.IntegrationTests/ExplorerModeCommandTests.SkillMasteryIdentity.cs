using Xunit;
using BookOfEternityClient.UI;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerModeCommandTests
{
    [Theory]
    [InlineData("Fireball", 3, 7)]
    [InlineData("fireball", 7, 3)]
    public async Task TryProcessCommand_Skills_KeepsExactCaseSiblingMastery(
        string selectedName, int expectedLevel, int otherLevel)
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/player/skills_active.json", new
        {
            activeSkillChanges = new[]
            {
                new { skillName = "Fireball", skillDescription = "First exact identity." },
                new { skillName = "fireball", skillDescription = "Second exact identity." }
            }
        });
        await WriteJsonAsync("game_state/player/skills_passive.json", new { passiveSkillChanges = new object[0] });
        await WriteJsonAsync("game_state/player/skill_mastery.json", new
        {
            skillMasteryChanges = new[]
            {
                new { skillName = "Fireball", newMasteryLevel = 3 },
                new { skillName = "fireball", newMasteryLevel = 7 }
            }
        });
        await _stateManager.RefreshGameStateAsync();
        _console.QueueSelection("Навыки", ConsoleLayout.PlainChoiceLabel("⚡ " + selectedName), "← Назад");

        await _explorer.TryProcessCommand("/навыки");

        AssertNoHiddenExplorerErrors("exact_skill_mastery");
        var rendered = ExtractRenderedText();
        Assert.Contains($"Мастерство: {expectedLevel}", rendered);
        Assert.DoesNotContain($"Мастерство: {otherLevel}", rendered);
    }
}
