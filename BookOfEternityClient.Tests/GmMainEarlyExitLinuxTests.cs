using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmMainEarlyExitLinuxTests
{
    [Theory]
    [InlineData("production-main-early-exit")]
    [InlineData("production-main-early-exit-metadata-fault")]
    public Task OriginalProduction_EarlyExitStatusSettlement(string mode)=>ProductionMainLinuxFixture.RunAsync(mode);
}
