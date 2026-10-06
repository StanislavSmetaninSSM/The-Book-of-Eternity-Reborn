using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmMainRunCrashLinuxTests
{
    [Theory]
    [InlineData("namespace")]
    [InlineData("prepared")]
    [InlineData("held")]
    [InlineData("running")]
    [InlineData("released")]
    public async Task LaunchDeath_FreshProcessRefusesWithoutReplayOrCanonicalEffects(string cut)=>
        await GmOwnedTerminalLinuxTests.RunAsync("terminal-main-crash-launch-"+cut);
}
