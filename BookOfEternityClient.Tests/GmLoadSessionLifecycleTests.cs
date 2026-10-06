using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmLoadSessionLifecycleTests
{
    [Fact]
    public Task BrowserLoad_OriginalLiveOwnerStopsBeforeRealReplacement()=>ProductionMainLinuxFixture.RunAsync("production-main-load-browser");
    [Fact]
    public Task ConsoleLoad_OriginalLiveOwnerStopsBeforeRealReplacement()=>ProductionMainLinuxFixture.RunAsync("production-main-load-console");
    [Fact]
    public Task BrowserHttp_LoadFullBundleCurrentAckFreshEpoch()=>ProductionMainLinuxFixture.RunAsync("production-main-load-browser-http");
    [Fact]
    public Task BrowserHttp_PrematureAckCannotAuthorizeFreshLaunch()=>ProductionMainLinuxFixture.RunAsync("production-main-load-http-early-ack");
    [Fact]
    public Task ConsoleLoad_CommittedCleanupDebtCannotLaunchFresh()=>ProductionMainLinuxFixture.RunAsync("production-main-load-console-debt");
    [Theory]
    [InlineData("stop-reply-loss")]
    [InlineData("stop-uncertain")]
    [InlineData("refresh-refused")]
    [InlineData("manual-cancel")]
    public Task BrowserHttp_OriginalStopAndRefreshFaults(string fault)=>ProductionMainLinuxFixture.RunAsync("production-main-load-http-fault-"+fault);
    [Theory]
    [InlineData("restart-reply-loss")]
    [InlineData("generation-race")]
    [InlineData("load-reply-loss")]
    [InlineData("cancel-after-decision")]
    public Task BrowserHttp_OriginalRestartFaults(string fault)=>ProductionMainLinuxFixture.RunAsync("production-main-load-http-fault-"+fault);
    [Theory]
    [InlineData("rollback")]
    [InlineData("uncertain")]
    [InlineData("no-active")]
    [InlineData("worker-debt")]
    public Task BrowserHttp_TypedStorageAndIndependentAdmission(string fault)=>ProductionMainLinuxFixture.RunAsync("production-main-load-http-fault-"+fault);
}
