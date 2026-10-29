using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.WebUi;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Known History text stays literal through the existing browser normalization boundary.</summary>
public sealed class WoundHistoryBrowserTextTests
{
    /// <summary>A closing HTML tag is text, not an English slash-command hint.</summary>
    [Theory]
    [InlineData("[red]Зажившая[/] <b>рана</b>")]
    [InlineData("Известная <em>рана</em>")]
    public void BrowserTextKeepsLiteralClosingTag(string text) => AssertText(text, text);

    /// <summary>Ordinary command hints still disappear, including beside literal tagged text.</summary>
    [Theory]
    [InlineData("Подсказка /help <topic>", "Подсказка")]
    [InlineData("Подсказка /npc_talk", "Подсказка")]
    [InlineData("Метка <b>раны</b> /help <topic>", "Метка <b>раны</b>")]
    public void BrowserCommandHintsRemainFilteredAroundLiteralText(string text, string expected) => AssertText(text, expected);

    private static void AssertText(string text, string expected)
    {
        var input = new ExplorerCommandResult { Command = "/раны история", Blocks = [new UiTextBlock { Text = text }] };
        var result = BrowserEntityDossierPrototypeNormalizer.Normalize(input);
        Assert.Equal(text, Assert.IsType<UiTextBlock>(Assert.Single(input.Blocks)).Text);
        Assert.Equal(expected, Assert.IsType<UiTextBlock>(Assert.Single(result.Blocks)).Text);
    }
}
