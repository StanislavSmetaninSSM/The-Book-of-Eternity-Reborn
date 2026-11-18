using Xunit;
namespace BookOfEternityClient.Tests;

public sealed class MortalMenuFrameTests
{
    [Fact]
    public Task CapturedPipeFixedMenuFrameContracts() => MortalConsoleRouteTests.RunAsync("test-mortal-menu-frame.py", 16);
}
