using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmPreparedGrantStopTests
{
    [Fact]
    public async Task PreparedGrant_StopBeforeActivationRefusesEffectsAndRetiresOriginal()
        => await GmOwnedTerminalLinuxTests.RunAsync("terminal-main-prepared-stop-race");
}
