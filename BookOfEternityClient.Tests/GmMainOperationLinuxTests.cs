using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmMainOperationLinuxTests
{
    [Theory]
    [InlineData("terminal-main-operation-explicit-failed-finalization")]
    [InlineData("terminal-main-operation-explicit-cancelled-finalization")]
    public async Task ActualAdapter_ExplicitOutcomeSurvivesFinalization(string mode)=>await GmOwnedTerminalLinuxTests.RunAsync(mode);
    [Fact]
    public async Task OriginalOwner_ActualPipeRetainsParticipatingOperation()=>await GmOwnedTerminalLinuxTests.RunAsync("terminal-main-operation-positive");
    [Theory]
    [InlineData("terminal-main-operation-retain-race")]
    [InlineData("terminal-main-operation-cancel-closing")]
    [InlineData("terminal-main-operation-failed-clean-closing")]
    public async Task OriginalClient_FinalizationBoundary(string mode)=>await GmOwnedTerminalLinuxTests.RunAsync(mode);
    [Theory]
    [InlineData("terminal-main-operation-close-reply-loss")]
    [InlineData("terminal-main-operation-escaped-borrow")]
    public async Task OriginalClient_ContinuationBoundary(string mode)=>await GmOwnedTerminalLinuxTests.RunAsync(mode);
    [Theory]
    [InlineData("terminal-main-operation-successful-finalization")]
    [InlineData("terminal-main-operation-shutdown-identity")]
    public async Task OriginalClient_ReviewClosingBoundary(string mode)=>await GmOwnedTerminalLinuxTests.RunAsync(mode);
    [Theory]
    [InlineData("terminal-main-operation-helper-caught-loss")]
    [InlineData("terminal-main-operation-helper-timeout")]
    [InlineData("terminal-main-operation-helper-timeout-reply-loss")]
    [InlineData("terminal-main-operation-helper-stall")]
    [InlineData("terminal-main-operation-helper-stopping")]
    public async Task ActualHelper_ControlledLiveBoundary(string mode)=>await GmOwnedTerminalLinuxTests.RunAsync(mode);
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
    public async Task OriginalOwner_ActualParticipatingBoundary(string mode)=>await GmOwnedTerminalLinuxTests.RunAsync(mode);
    [Fact]
    public Task OriginalOwner_StatusAdmission()=>GmOwnedTerminalLinuxTests.RunAsync("terminal-main-operation-status-admission");
}
