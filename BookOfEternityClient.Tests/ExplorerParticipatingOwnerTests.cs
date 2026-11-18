using Xunit;
namespace BookOfEternityClient.Tests;

/// <summary>Actual neutral owner/transport qualification, not a live gameplay or storage mutation test.</summary>
public sealed class ExplorerParticipatingOwnerTests
{
    [Fact]
    public Task SharedDispatchBorrowsOriginalRunningOwnerAndClosesBeforeReturn() =>
        GmOwnedTerminalLinuxTests.RunAsync("terminal-main-explorer-dispatch");
    [Fact]
    public Task StoppingOriginalOwnerRefusesMenuBeforeAnyGameplayChange() =>
        GmOwnedTerminalLinuxTests.RunAsync("terminal-main-explorer-stopping");
}
