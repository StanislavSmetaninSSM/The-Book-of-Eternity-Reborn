using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmMainProductionAdmissionTests
{
    [Fact]
    public Task OrdinaryBridge_ConfiguredCliUsesDurableOriginalProductionRoute()=>ProductionMainLinuxFixture.RunAsync("bridge");
    [Theory]
    [InlineData("refuse-auto")]
    [InlineData("refuse-systemd")]
    [InlineData("refuse-command")]
    [InlineData("refuse-cwd")]
    [InlineData("refuse-package")]
    [InlineData("refuse-worker")]
    [InlineData("refuse-storage")]
    public Task AdmissionRefusal_RealOrdinaryEntrypointBeforeCreation(string mode)=>ProductionMainLinuxFixture.RunAsync(mode);
    [Fact]
    public Task OriginalProduction_PipeDraftTakeoverCancelScopedStop()=>ProductionMainLinuxFixture.RunAsync("production-main-controls");
    [Fact]
    public Task RealConsoleHealthAndInertDaemonConsumers_BorrowOriginalLiveProductionPins()=>ProductionMainLinuxFixture.RunAsync("production-main-consumers");
    [Fact]
    public Task OriginalProduction_UncertainRetainsOwnerAndWorkerInventory()=>ProductionMainLinuxFixture.RunAsync("production-main-uncertain");
}
