using Xunit;
using BookOfEternityClient.Services.GmWorkers;

namespace BookOfEternityClient.Tests;

public sealed partial class GmWorkerNativePoolTests
{
    [Fact]
    public async Task ActualPool_LostOriginalHelperCannotReleaseWorker()
    {
        var result = await RunScenario("pool-helper-loss-before-release", allowGuardianEmergency: true);
        Assert.True(result.GetProperty("helperWasLost").GetBoolean());
        Assert.False(result.GetProperty("success").GetBoolean());
        Assert.False(result.GetProperty("validatedExecution").GetBoolean());
        Assert.True(result.GetProperty("failureCopyRejected").GetBoolean());
        Assert.True(result.GetProperty("noCanonicalProposalOrInbox").GetBoolean());
        Assert.Equal(0, result.GetProperty("publicationCalls").GetInt32());
        Assert.Equal(1, result.GetProperty("retainedWorkspaces").GetInt32());
        Assert.Equal(1, result.GetProperty("reaperEntries").GetInt32());
        Assert.Equal(1, result.GetProperty("reaperCapacity").GetInt32());
        Assert.Equal(0, result.GetProperty("workerStarts").GetInt32());
    }

    [Theory]
    [InlineData("wrong-run")]
    [InlineData("wrong-scope")]
    [InlineData("malformed")]
    [InlineData("uncertain")]
    [InlineData("late-terminal")]
    public async Task ActualPool_NativeObservationUncertaintySurvivesLateCleanup(string mode)
    {
        var result = await RunScenario("pool-fault-" + mode);
        AssertRetainedUncertainty(result);
        Assert.True(result.GetProperty("observationFaultReached").GetBoolean());
        Assert.Equal(0, result.GetProperty("result").GetProperty("ExitCode").GetInt32());
        Assert.True(result.GetProperty("actualOutputTasksSettled").GetBoolean());
        if (mode.StartsWith("late-", StringComparison.Ordinal))
            Assert.InRange(result.GetProperty("elapsedMilliseconds").GetInt64(), 4900, 20000);
    }

    [Fact]
    public async Task ActualPool_LateOriginalOutputObservationCannotClearUncertainty()
    {
        var result = await RunScenario("pool-fault-late-output");
        AssertRetainedUncertainty(result);
        Assert.True(result.GetProperty("observationFaultReached").GetBoolean());
        Assert.True(result.GetProperty("lateOutputObservationSettled").GetBoolean());
        Assert.True(result.GetProperty("retainedSlotProbePending").GetBoolean());
        Assert.True(result.GetProperty("retainedSlotProbeCanceled").GetBoolean());
        Assert.Equal(0, result.GetProperty("result").GetProperty("ExitCode").GetInt32());
        Assert.True(result.GetProperty("actualOutputTasksSettled").GetBoolean());
        Assert.InRange(result.GetProperty("elapsedMilliseconds").GetInt64(), 4900, 20000);
    }

    [Theory]
    [InlineData("owner-eof")]
    [InlineData("status-loss")]
    [InlineData("helper-loss")]
    public async Task ActualPool_AuthorityLossAfterCompletedRetainsOriginalExecution(string mode)
    {
        var result = await RunScenario("pool-fault-" + mode, allowGuardianEmergency: mode == "helper-loss");
        AssertRetainedUncertainty(result);
        Assert.Equal(0, result.GetProperty("observedCompletion").GetInt32());
        Assert.Equal((int)GmWorkerProcessCompletionOutcomeKind.Completed, result.GetProperty("arbiterOutcome").GetInt32());
    }

    private static void AssertRetainedUncertainty(System.Text.Json.JsonElement result)
    {
        Assert.False(result.GetProperty("success").GetBoolean());
        Assert.False(result.GetProperty("validatedExecution").GetBoolean());
        Assert.True(result.GetProperty("failureCopyRejected").GetBoolean());
        Assert.True(result.GetProperty("noCanonicalProposalOrInbox").GetBoolean());
        Assert.True(result.GetProperty("canonicalContextUnchanged").GetBoolean());
        Assert.Equal(1, result.GetProperty("workerStarts").GetInt32());
        Assert.Equal(0, result.GetProperty("publicationCalls").GetInt32());
        Assert.Equal(1, result.GetProperty("beforeLateEntries").GetInt32());
        Assert.Equal(1, result.GetProperty("beforeLateCapacity").GetInt32());
        Assert.Equal(1, result.GetProperty("retainedWorkspaces").GetInt32());
        Assert.Equal(1, result.GetProperty("reaperEntries").GetInt32());
        Assert.Equal(1, result.GetProperty("reaperCapacity").GetInt32());
        Assert.Equal(0, result.GetProperty("cleanupConfirmedAuditCount").GetInt32());
        Assert.Equal(0, result.GetProperty("quarantineReceipts").GetInt32());
        Assert.Equal((int)GmWorkerStopState.Uncertain, result.GetProperty("result").GetProperty("StopEvidence").GetProperty("State").GetInt32());
        Assert.Equal((int)GmWorkerStopState.Uncertain, result.GetProperty("lateStop").GetProperty("State").GetInt32());
    }
}
