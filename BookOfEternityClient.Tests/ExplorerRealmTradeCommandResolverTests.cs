using BookOfEternityClient.CommandProtocol;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ExplorerRealmTradeCommandResolverTests
{
    [Theory]
    [InlineData("Mortal World", "", "/npc_trade")]
    [InlineData("Этерния", "Марек", "/npc_trade Марек")]
    [InlineData("Chaos Sea", "", "/guardian_trade")]
    [InlineData("Море Хаоса", "guardian_alpha", "/guardian_trade guardian_alpha")]
    [InlineData("Shining Abode", "", "/shining_trade")]
    [InlineData("Сияющая Обитель", "faction_dawn", "/shining_trade faction_dawn")]
    public void Resolve_MapsCurrentRealmToExistingTradeCommand(
        string currentRealm,
        string arguments,
        string expectedCommand)
    {
        var result = ExplorerRealmTradeCommandResolver.Resolve(currentRealm, arguments);

        Assert.True(result.Success);
        Assert.Equal(expectedCommand, result.Command);
        Assert.Equal(string.Empty, result.ErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_UnresolvedRealmReturnsLocalizedFailure(string? currentRealm)
    {
        var result = ExplorerRealmTradeCommandResolver.Resolve(currentRealm, "ignored");

        Assert.False(result.Success);
        Assert.Equal(string.Empty, result.Command);
        Assert.Contains("реальност", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("currentRealm", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
