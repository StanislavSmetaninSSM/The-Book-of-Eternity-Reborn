using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmMainRunOwnerLinuxTests
{
    [Fact]
    public async Task ActualBridge_DurableOriginalOwnerBeforeInteractiveReleaseAndScopedRetirement()=>
        await GmOwnedTerminalLinuxTests.RunAsync("terminal-fence");
    [Theory]
    [InlineData("terminal-main-running-debt")]
    [InlineData("terminal-main-launch-generation")]
    [InlineData("terminal-main-namespace-validate")]
    [InlineData("terminal-main-namespace-publish")]
    [InlineData("terminal-main-stopped-debt-epoch")]
    [InlineData("terminal-main-stop-late-authority")]
    [InlineData("terminal-main-held-expiry")]
    [InlineData("terminal-main-prepared-debt")]
    [InlineData("terminal-main-replacement")]
    [InlineData("terminal-main-forged-stop")]
    [InlineData("terminal-main-closing")]
    [InlineData("terminal-main-pin-refusal")]
    [InlineData("terminal-main-worker")]
    public async Task OriginalOwner_ReviewBoundary(string mode)=>await GmOwnedTerminalLinuxTests.RunAsync(mode);
    [Fact]
    public async Task StagedIntent_SettlesOriginalBeforeDecision()=>await GmOwnedTerminalLinuxTests.RunAsync("terminal-main-staged-rollback");
    [Theory]
    [InlineData("terminal-main-unbound")]
    [InlineData("terminal-main-held-rollback")]
    [InlineData("terminal-main-held-commit")]
    [InlineData("terminal-main-output-drain")]
    [InlineData("terminal-main-output-fault")]
    [InlineData("terminal-main-pin-timeout")]
    [InlineData("terminal-main-single-release")]
    [InlineData("terminal-main-recovery-generation")]
    [InlineData("terminal-main-recovery-same")]
    [InlineData("terminal-main-save-load")]
    public async Task OriginalOwner_ConnectedSettlement(string mode)=>await GmOwnedTerminalLinuxTests.RunAsync(mode);
}
