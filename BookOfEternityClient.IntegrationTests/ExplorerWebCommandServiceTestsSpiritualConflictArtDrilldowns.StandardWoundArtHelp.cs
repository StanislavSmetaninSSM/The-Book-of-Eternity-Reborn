using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWebCommandServiceTestsSpiritualConflictArtDrilldowns
{
    [Fact]
    public async Task StandardWoundArt_SpiritualConflictHelpRendersPassiveAndFutureHealingRulesTruthfully()
    {
        await SeedRichSpiritualConflictArtDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/spiritual_combat_help"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var dossier = Assert.Single(result.Blocks.OfType<UiEntityDossierBlock>(), block => block.EntityType == "spiritual-combat-help");
        var arts = Assert.Single(dossier.Sections, section => section.Id == "spiritual-combat-help-arts");
        Assert.DoesNotContain("Базовые действия духовного боя", arts.Summary, StringComparison.OrdinalIgnoreCase);
        var resilience = Assert.Single(arts.Cards, card => card.Title == "Духовная стойкость");
        Assert.Equal("Пассивное духовное искусство", resilience.Subtitle);
        Assert.Contains("не требует отдельного действия", resilience.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Не является отдельным боевым приёмом", resilience.Summary, StringComparison.OrdinalIgnoreCase);
        var healing = Assert.Single(arts.Cards, card => card.Title == "Духовное исцеление");
        Assert.Equal("Целительное духовное искусство", healing.Subtitle);
        Assert.Contains("пока недоступны", healing.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Правила искусства", healing.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "Искусство диагностики и лечения духовных ран; на нулевой ступени доступна только диагностика.",
            healing.Summary,
            StringComparison.OrdinalIgnoreCase);
    }
}
