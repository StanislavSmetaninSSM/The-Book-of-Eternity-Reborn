using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmMainProductionAdmissionTests
{
    [Fact]
    public Task OrdinaryBridge_ConfiguredCliUsesDurableOriginalProductionRoute()=>ProductionMainLinuxFixture.RunAsync("bridge");
}
