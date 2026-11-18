using Xunit;
namespace BookOfEternityClient.Tests;

public sealed class GmHostExitOriginalSoakTests
{
    [Fact]
    public async Task OriginalFifoOwner_ObservedFor120SecondsThenScopedStop()
        => await GmOwnedTerminalLinuxTests.RunAsync("terminal-main-consumer-pidfd-soak");
}
