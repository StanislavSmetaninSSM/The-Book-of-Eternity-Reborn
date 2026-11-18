using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmMainWorkerCleanupTests
{
    [Theory]
    [InlineData("expired")]
    [InlineData("retired")]
    [InlineData("receipt-retry")]
    [InlineData("metadata")]
    public Task OriginalMain_DeferredNoLaunchCleanupPreservesExactAuthority(string boundary) =>
        GmOwnedTerminalLinuxTests.RunAsync("terminal-main-worker-cleanup-" + boundary);
    [Theory]
    [InlineData("publication")]
    [InlineData("publication-expired")]
    [InlineData("publication-retired")]
    public Task OriginalMain_CanonicalAuditUncertaintyRemainsSticky(string boundary) =>
        GmOwnedTerminalLinuxTests.RunAsync("terminal-main-worker-cleanup-" + boundary);
}
