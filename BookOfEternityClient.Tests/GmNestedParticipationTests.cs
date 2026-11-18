using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmNestedParticipationTests
{
    [Fact]
    public async Task OriginalNestedScopeClosesBeforeFreshHeartbeat()
        => await GmOwnedTerminalLinuxTests.RunAsync("terminal-main-nested-heartbeat");
}
