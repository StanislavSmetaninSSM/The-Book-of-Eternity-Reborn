using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmBridgeContenderRestartTests
{
    [Fact]
    public Task SameRootContenderPreservesOriginalAndSameConfigRestart() =>
        ProductionMainLinuxFixture.RunAsync("production-main-contender-restart");
}
