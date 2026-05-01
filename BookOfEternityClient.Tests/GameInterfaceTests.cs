using BookOfEternityClient.Models.GameState;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GameInterfaceTests
{
    [Fact]
    public void SafeMarkup_DoesNotThrow_OnBrokenMarkup()
    {
        var ex = Record.Exception(() => GameInterface.SafeMarkup("[dim]→ До ранга [white]Дружелюбный[/]: 50 репутации"));

        Assert.Null(ex);
    }

    [Fact]
    public void SafeMarkup_DoesNotThrow_OnUnknownStyleArrayText()
    {
        var ex = Record.Exception(() => GameInterface.SafeMarkup("selectedCardIds=[card_alpha, card_beta]"));

        Assert.Null(ex);
    }

    [Fact]
    public void SafeMarkup_DoesNotThrow_OnValidMarkup()
    {
        var ex = Record.Exception(() => GameInterface.SafeMarkup("[dim]Текст[/] [white]в порядке[/]"));

        Assert.Null(ex);
    }

    [Fact]
    public void ShouldRenderAfterlifeStatus_ShiningAbode_ReturnsTrue()
    {
        var state = new AggregatedGameState
        {
            CurrentRealm = "Shining Abode",
        };

        Assert.True(GameInterface.ShouldRenderAfterlifeStatus(state));
    }

    [Fact]
    public void ShouldRenderAfterlifeStatus_ChaosSea_ReturnsTrue()
    {
        var state = new AggregatedGameState
        {
            CurrentRealm = "Chaos Sea",
        };

        Assert.True(GameInterface.ShouldRenderAfterlifeStatus(state));
    }

    [Fact]
    public void ShouldRenderAfterlifeStatus_MortalRealm_ReturnsFalse()
    {
        var state = new AggregatedGameState
        {
            CurrentRealm = "Неон-Сити",
        };

        Assert.False(GameInterface.ShouldRenderAfterlifeStatus(state));
    }

    [Fact]
    public void Localization_SoulInventoryInfo_UsesAfterlifeWording()
    {
        var ru = new LocalizationManager { CurrentLanguage = "ru" };
        var en = new LocalizationManager { CurrentLanguage = "en" };

        var ruValue = ru.T("soul_inventory_info");
        var enValue = en.T("soul_inventory_info");

        Assert.Contains("загроб", ruValue, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Море Хаоса", ruValue, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("afterlife", enValue, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Chaos Sea", enValue, StringComparison.OrdinalIgnoreCase);
    }
}
