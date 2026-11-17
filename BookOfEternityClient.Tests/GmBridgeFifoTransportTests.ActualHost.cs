using Xunit;
namespace BookOfEternityClient.Tests;
public sealed partial class GmBridgeFifoTransportTests
{
    [Fact]
    public async Task ActualHost_MainOriginalCloseAndShippedLauncherDiagnosticsShutdown()
        => await GmOwnedTerminalLinuxTests.RunAsync("terminal-main-consumer-fifo");

    [Fact]
    public async Task ActualHost_LoadStopAndNoReplacementFinishRetainOneOriginalStream()
        => await GmOwnedTerminalLinuxTests.RunAsync("terminal-main-consumer-load-fifo");

}
