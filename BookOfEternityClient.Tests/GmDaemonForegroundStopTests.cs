using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmDaemonForegroundStopTests
{
    [Fact]
    public Task ActualDaemon_OriginalFenceStopsBeforeForegroundInterrupt()=>
        ProductionMainLinuxFixture.RunAsync("production-main-daemon-coordinated-stop");
    [Fact]
    public Task ActualIdleDaemon_CtrlCClosesOriginalActivePinBeforeScopedStop()=>
        ProductionMainLinuxFixture.RunAsync("production-main-daemon-stop");
}
