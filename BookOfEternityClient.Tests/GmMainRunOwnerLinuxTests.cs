using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmMainRunOwnerLinuxTests
{
    [Fact]
    public async Task ActualBridge_DurableOriginalOwnerBeforeInteractiveReleaseAndScopedRetirement()=>
        await GmOwnedTerminalLinuxTests.RunAsync("terminal-fence");
}
