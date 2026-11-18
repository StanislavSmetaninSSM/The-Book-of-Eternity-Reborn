using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmPreparedGrantStopTests
{
    [Fact]
    public async Task PreparedGrant_StopBeforeActivationRefusesEffectsAndRetiresOriginal()
        => await GmOwnedTerminalLinuxTests.RunAsync("terminal-main-prepared-stop-race");
    [Theory]
    [InlineData("terminal-main-loss-prepared")]
    [InlineData("terminal-main-loss-active")]
    public async Task LostBeforeStop_RemainsUnresolvedWithoutFabricatedLogicalStop(string mode)
        => await GmOwnedTerminalLinuxTests.RunAsync(mode);
}

