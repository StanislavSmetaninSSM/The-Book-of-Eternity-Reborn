using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmDaemonForegroundStopTests
{
    [Fact]
    public Task ActualIdleDaemon_CtrlCClosesOriginalActivePinBeforeScopedStop()=>
        ProductionMainLinuxFixture.RunAsync("production-main-daemon-stop");
}
