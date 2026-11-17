using Xunit;
namespace BookOfEternityClient.Tests;
public sealed partial class GmBridgeFifoTransportTests
{
    [Fact]
    public async Task ActualHost_MainOriginalCloseAndShippedLauncherDiagnosticsShutdown()
        => await GmOwnedTerminalLinuxTests.RunAsync("terminal-main-consumer-fifo");
}
