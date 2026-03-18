using BookOfEternityClient.UI;
using Spectre.Console;
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
    public void SafeMarkup_DoesNotThrow_OnValidMarkup()
    {
        var ex = Record.Exception(() => GameInterface.SafeMarkup("[dim]Текст[/] [white]в порядке[/]"));

        Assert.Null(ex);
    }
}
