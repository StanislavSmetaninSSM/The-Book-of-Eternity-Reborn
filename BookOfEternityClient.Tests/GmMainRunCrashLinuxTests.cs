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
        await RunBoundedAsync("terminal-main-crash-launch-"+cut);

    [Theory]
    [InlineData("before")]
    [InlineData("receipt")]
    [InlineData("reply")]
    public async Task ClientDeath_OriginalReceiptBoundaryControlsColdContinuation(string cut)=>
        await RunBoundedAsync("terminal-main-crash-pin-"+cut);

    [Theory]
    [InlineData("stopping")]
    [InlineData("io")]
    [InlineData("staged")]
    [InlineData("readback")]
    [InlineData("ack")]
    [InlineData("fault")]
    public async Task StopDeath_ActualSettlementAndDurableAckControlNewEpoch(string cut)=>
        await RunBoundedAsync("terminal-main-crash-stop-"+cut);

    private static async Task RunBoundedAsync(string mode)
    {
        string? folder=null;
        try{await GmOwnedTerminalLinuxTests.RunAsync(mode,value=>folder=value);}
        finally{if(folder!=null)MainRunCrashScenarioDriver.CleanupAfterGuardian(folder);}
    }
}
