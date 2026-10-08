using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmBridgeOriginalConsumerTests
{
    [Theory]
    [InlineData("rollback")]
    [InlineData("unknown")]
    public Task ProductionStartupConfigurationUsesSettledCanonicalSnapshot(string boundary) =>
        ProductionMainLinuxFixture.RunAsync("production-main-config-recovery-"+boundary);

    [Theory]
    [InlineData("slot")]
    [InlineData("lease")]
    public Task PrivateWorkerDispatch_BorrowsOriginalPinAndCapturedInputCancellation(string boundary) =>
        GmOwnedTerminalLinuxTests.RunAsync("terminal-main-worker-dispatch-"+boundary);

    [Theory]
    [InlineData("exact")]
    [InlineData("generation")]
    [InlineData("run")]
    [InlineData("terminal")]
    [InlineData("null")]
    [InlineData("root")]
    [InlineData("backend")]
    [InlineData("invalid")]
    [InlineData("foreign-host")]
    public Task ConsoleLoad_StartedNotReadyReceiptRequiresActualFreshOriginalIdentity(string boundary) =>
        ProductionMainLinuxFixture.RunAsync("production-main-load-console-receipt-"+boundary);
}
