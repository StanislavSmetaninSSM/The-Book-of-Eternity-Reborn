using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GmWorkerNativePoolTests
{
    [Theory]
    [InlineData("dispose")]
    [InlineData("workspace")]
    [InlineData("audit-failure")]
    [InlineData("audit-unavailable")]
    [InlineData("receipt-temp")]
    [InlineData("receipt-ack")]
    [InlineData("receipt-replaced")]
    [InlineData("receipt-conflict")]
    public async Task ActualPool_ValidatedStopCleanupRetriesRetainOriginalPhasesAndCapacity(string mode)
    {
        var result = await RunScenario("pool-cleanup-" + mode);
        var report = result.GetProperty("cleanupReport");
        Assert.True(report.GetProperty("acceptedBefore").GetBoolean(), result.GetProperty("failure").ToString());
        Assert.True(result.GetProperty("result").GetProperty("Status").GetProperty("CleanupDeferred").GetBoolean());
        Assert.True(result.GetProperty("result").GetProperty("StopEvidence").GetProperty("CleanupComplete").GetBoolean());
        Assert.True(result.GetProperty("result").GetProperty("OutputsSettled").GetBoolean());
        Assert.Equal(1, result.GetProperty("workerStarts").GetInt32());
        Assert.Equal(1, result.GetProperty("releases").GetInt32());
        Assert.Equal(1, result.GetProperty("publicationCalls").GetInt32());
        Assert.True(result.GetProperty("canonicalContextUnchanged").GetBoolean());
        var initial = report.GetProperty("initial");
        Assert.Equal(1, initial.GetProperty("entries").GetInt32());
        Assert.Equal(1, initial.GetProperty("capacity").GetInt32());
        Assert.True(initial.GetProperty("workspaceExists").GetBoolean());
        Assert.True(initial.GetProperty("runtimeAuthorityRetained").GetBoolean());
        Assert.Equal(mode != "dispose", initial.GetProperty("ownerDisposed").GetBoolean());
        Assert.Equal(mode == "dispose" ? 0 : 1, initial.GetProperty("workspaceHookCalls").GetInt32());
        var first = report.GetProperty("first");
        var needsRetry = mode is "audit-failure" or "receipt-temp" or "receipt-ack" or "receipt-conflict";
        Assert.Equal(needsRetry ? 1 : 0, first.GetProperty("capacity").GetInt32());
        Assert.False(first.GetProperty("workspaceExists").GetBoolean());
        Assert.Equal(needsRetry, first.GetProperty("runtimeAuthorityRetained").GetBoolean());
        var final = report.GetProperty("final");
        var conflicted = mode == "receipt-conflict";
        Assert.Equal(conflicted ? 1 : 0, final.GetProperty("entries").GetInt32());
        Assert.Equal(conflicted ? 1 : 0, final.GetProperty("capacity").GetInt32());
        Assert.Equal(conflicted ? 1 : 0, report.GetProperty("capacityAfterProbe").GetInt32());
        Assert.True(final.GetProperty("ownerDisposed").GetBoolean());
        Assert.False(final.GetProperty("workspaceExists").GetBoolean());
        Assert.False(final.GetProperty("workspaceRootExists").GetBoolean());
        Assert.Equal(conflicted, final.GetProperty("runtimeAuthorityRetained").GetBoolean());
        Assert.Equal(mode == "dispose" ? 1 : 2, final.GetProperty("workspaceHookCalls").GetInt32());
        if (mode == "dispose") Assert.Equal(2, final.GetProperty("disposeAttempts").GetInt32());
        if (mode == "audit-failure") Assert.True(report.GetProperty("auditFailures").GetInt32() > 0);
        Assert.True(report.GetProperty("receiptStable").GetBoolean());
        Assert.True(report.GetProperty("proposalUnchanged").GetBoolean());
        Assert.True(report.GetProperty("conflictingReceiptRetained").GetBoolean());
        Assert.Equal(!conflicted, report.GetProperty("capacityReusable").GetBoolean());
        Assert.Equal(!conflicted, report.GetProperty("probeStoppedBeforeReservation").GetBoolean());
        var fallback = mode.StartsWith("receipt-", StringComparison.Ordinal) || mode == "audit-unavailable";
        Assert.Equal(fallback ? 1 : 0, result.GetProperty("quarantineReceipts").GetInt32());
        Assert.Equal(fallback ? 0 : 1, report.GetProperty("cleanupConfirmedEvents").GetInt32());
        Assert.Equal(fallback, report.GetProperty("receiptPublishedCalls").GetInt32() == 1);
        if (fallback && !conflicted) Assert.True(report.GetProperty("receiptBound").GetBoolean());
        Assert.Equal(mode is "audit-unavailable" or "receipt-temp" or "receipt-ack" or "receipt-conflict" ? 0 : 1,
            report.GetProperty("proposalReceivedEvents").GetInt32());
    }
}
