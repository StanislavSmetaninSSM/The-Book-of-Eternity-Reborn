using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class BrowserOriginalOwnerLinuxTests
{
    [Fact]
    public Task ActualIdleRelay_PublicHttpReadsCloseOriginalPins() =>
        ProductionMainLinuxFixture.RunAsync("production-main-relay-http-reads");
}
