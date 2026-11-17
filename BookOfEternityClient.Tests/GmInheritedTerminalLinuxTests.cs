using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmInheritedTerminalLinuxTests
{
    [Fact]
    public async Task ActualOwnedTerminal_UsesInheritedBootstrapAndPreservesIoCleanup()
    {
        string? folder = null;
        await GmOwnedTerminalLinuxTests.RunAsync("terminal-inherited", value => folder = value);
        using var result = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(folder!, "scenario.json")));
        Assert.True(result.RootElement.GetProperty("InheritedBootstrap").GetBoolean());
    }
}
