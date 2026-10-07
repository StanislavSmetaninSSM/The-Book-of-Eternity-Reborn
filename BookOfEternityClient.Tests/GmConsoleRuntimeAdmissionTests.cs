using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmConsoleRuntimeAdmissionTests
{
    [Fact]
    public Task ActualConsoleNormalizationAndRefresh_CloseOriginalPinsAndRefuseStopping()=>
        ProductionMainLinuxFixture.RunAsync("production-main-console-runtime");
}
