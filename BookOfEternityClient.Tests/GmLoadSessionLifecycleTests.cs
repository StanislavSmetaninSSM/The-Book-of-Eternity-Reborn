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
}
