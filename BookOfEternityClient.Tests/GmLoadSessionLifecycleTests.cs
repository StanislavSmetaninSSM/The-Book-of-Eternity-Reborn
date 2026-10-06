using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmLoadSessionLifecycleTests
{
    [Fact]
    public Task BrowserLoad_OriginalLiveOwnerStopsBeforeRealReplacement()=>ProductionMainLinuxFixture.RunAsync("production-main-load-browser");
}
