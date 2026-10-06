using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmMainOperationLinuxTests
{
    [Fact]
    public async Task OriginalOwner_ActualPipeRetainsParticipatingOperation()=>await GmOwnedTerminalLinuxTests.RunAsync("terminal-main-operation-positive");
}
