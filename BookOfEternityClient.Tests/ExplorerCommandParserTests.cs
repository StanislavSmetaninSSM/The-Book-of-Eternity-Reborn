using BookOfEternityClient.CommandProtocol;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ExplorerCommandParserTests
{
    [Fact]
    public void Parse_BaseCommand_SeparatesRawInputAndCanonicalIdentity()
    {
        var parsed = ExplorerCommandParser.Parse("/status");

        Assert.True(parsed.Success);
        Assert.Equal("/status", parsed.RawInput);
        Assert.Equal("status", parsed.Descriptor?.Id);
        Assert.Equal("/status", parsed.CommandToken);
        Assert.Equal("/status", parsed.CanonicalCommand);
        Assert.Equal(string.Empty, parsed.Arguments);
        Assert.Null(parsed.Subcommand);
    }

    [Fact]
    public void Parse_RussianAliasWithArguments_PreservesArguments()
    {
        var parsed = ExplorerCommandParser.Parse("/математик 2 + 3 * 5");

        Assert.True(parsed.Success);
        Assert.Equal("math", parsed.Descriptor?.Id);
        Assert.Equal("/математик", parsed.CommandToken);
        Assert.Equal("/math", parsed.CanonicalCommand);
        Assert.Equal("2 + 3 * 5", parsed.Arguments);
    }

    [Theory]
    [InlineData("/торговля Мирвен", "/торговля", "Мирвен")]
    [InlineData("/trade faction_dawn", "/trade", "faction_dawn")]
    public void Parse_UniversalTradeAlias_IsRegisteredAndPreservesArguments(
        string command,
        string expectedToken,
        string expectedArguments)
    {
        var parsed = ExplorerCommandParser.Parse(command);

        Assert.True(parsed.Success);
        Assert.Equal("trade", parsed.Descriptor?.Id);
        Assert.Equal(expectedToken, parsed.CommandToken);
        Assert.Equal("/trade", parsed.CanonicalCommand);
        Assert.Equal(expectedArguments, parsed.Arguments);
        Assert.True(parsed.Descriptor?.AcceptsArguments);
    }

    [Fact]
    public void Parse_MemorySceneSubcommand_UsesCanonicalSubcommandRoute()
    {
        var parsed = ExplorerCommandParser.Parse("/воспоминание начать");

        Assert.True(parsed.Success);
        Assert.Equal("saref_memory_scene", parsed.Descriptor?.Id);
        Assert.Equal("/воспоминание", parsed.CommandToken);
        Assert.Equal("start", parsed.Subcommand?.Id);
        Assert.Equal("/воспоминание_начать", parsed.CanonicalCommand);
        Assert.Equal(string.Empty, parsed.Arguments);
    }

    [Fact]
    public void Parse_UnknownCommand_ReturnsRussianError()
    {
        var parsed = ExplorerCommandParser.Parse("/нет_такой_команды");

        Assert.False(parsed.Success);
        Assert.Contains("Команда не найдена", parsed.ErrorTitle, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/нет_такой_команды", parsed.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_UnknownSubcommand_ReturnsRussianError()
    {
        var parsed = ExplorerCommandParser.Parse("/сареф неизвестная_ветка");

        Assert.False(parsed.Success);
        Assert.Contains("Неизвестная подкоманда", parsed.ErrorTitle, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("неизвестная_ветка", parsed.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_UnbalancedQuotes_ReturnsMalformedArgumentError()
    {
        var parsed = ExplorerCommandParser.Parse("/math \"2 + 3");

        Assert.False(parsed.Success);
        Assert.Contains("Некорректные аргументы", parsed.ErrorTitle, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("кавыч", parsed.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
