using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmMainRunOwnerLinuxTests
{
    [Fact]
    public async Task ActualBridge_DurableOriginalOwnerBeforeInteractiveReleaseAndScopedRetirement()=>
        await GmOwnedTerminalLinuxTests.RunAsync("terminal-fence");
    [Theory]
    [InlineData("terminal-main-running-debt")]
    [InlineData("terminal-main-replacement")]
    [InlineData("terminal-main-forged-stop")]
    [InlineData("terminal-main-closing")]
    [InlineData("terminal-main-pin-refusal")]
    [InlineData("terminal-main-worker")]
    public async Task OriginalOwner_ReviewBoundary(string mode)=>await GmOwnedTerminalLinuxTests.RunAsync(mode);
}

