using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmMainOperationLinuxTests
{
    [Fact]
    public async Task OriginalOwner_ActualPipeRetainsParticipatingOperation()=>await GmOwnedTerminalLinuxTests.RunAsync("terminal-main-operation-positive");
    [Fact]
    public async Task OriginalHelper_UnsentOversizedCommandCanClose()=>await GmOwnedTerminalLinuxTests.RunAsync("terminal-main-operation-helper-oversized");
    [Theory]
    [InlineData("terminal-main-operation-status-fault")]
    [InlineData("terminal-main-operation-status-stall")]
    public async Task OriginalOwner_StatusRetirementFaultBoundary(string mode)=>await GmOwnedTerminalLinuxTests.RunAsync(mode);
    [Theory]
    [InlineData("terminal-main-operation-omitted-close")]
    [InlineData("terminal-main-operation-activation-exit")]
    [InlineData("terminal-main-operation-client-positive")]
    [InlineData("terminal-main-operation-query")]
    [InlineData("terminal-main-operation-shutdown")]
    [InlineData("terminal-main-operation-status-admission")]
    public async Task OriginalOwner_ActualParticipatingBoundary(string mode)=>await GmOwnedTerminalLinuxTests.RunAsync(mode);
}
