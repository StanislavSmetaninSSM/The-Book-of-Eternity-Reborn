using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GmInheritedTerminalLinuxTests
{
    [Fact]
    public async Task ActualOwnedTerminal_UsesInheritedBootstrapAndPreservesIoCleanup()
    {
        string? folder = null;
        await GmOwnedTerminalLinuxTests.RunAsync("terminal-inherited", value => folder = value);
        using var result = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(folder!, "scenario.json")));
        Assert.True(result.RootElement.GetProperty("InheritedBootstrap").GetBoolean());
    }
    [Theory]
    [InlineData("terminal-inherited-control-launch-cancel")]
    [InlineData("terminal-inherited-control-launch-expiry")]
    [InlineData("terminal-inherited-control-release-cancel")]
    [InlineData("terminal-inherited-control-release-expiry")]
    public async Task BlockedOriginalControl_CancelsWithoutCommandOrPhaseAdvance(string mode)
        => await GmOwnedTerminalLinuxTests.RunAsync(mode);
    [Fact]
    public async Task ActualPartialControlInitialization_RetainsOriginalFailureAndOwner()
        => await GmOwnedTerminalLinuxTests.RunAsync("terminal-inherited-control-init-failure");
}
