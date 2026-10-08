using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmMainFinalizationCapabilityTests
{
    [Fact]
    public Task OriginalRunningOwner_PurposeAloneCannotRecoverOrPublish()=>
        GmOwnedTerminalLinuxTests.RunAsync("terminal-main-finalization-purpose");
    [Fact]
    public Task OriginalClosingFrame_FinalizationLeaseCanReadButCannotPublish()=>
        GmOwnedTerminalLinuxTests.RunAsync("terminal-main-finalization-readonly");
    [Fact]
    public Task OriginalRunningOwner_ActualBoundClosingKeepsGenerationVerification()=>
        GmOwnedTerminalLinuxTests.RunAsync("terminal-main-finalization-bound-close");
}
