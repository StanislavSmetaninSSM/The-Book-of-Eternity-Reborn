using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GmWorkerNativePoolTests
{
    [Theory]
    [InlineData("cancel-release", true, 0)]
    [InlineData("timeout-release", false, 0)]
    [InlineData("cancel-publication", true, 1)]
    [InlineData("timeout-publication", false, 1)]
    public async Task ActualPool_CancellationAndTimeoutNeverPublishDespiteConfirmedCleanup(string mode, bool canceled, int starts)
    {
        var result = await RunScenario("pool-terminal-" + mode);
        AssertRejectedAndCleaned(result);
        Assert.Equal(canceled, result.GetProperty("canceled").GetBoolean());
        Assert.Equal(!canceled, result.GetProperty("resultReturned").GetBoolean());
        Assert.Equal(starts, result.GetProperty("workerStarts").GetInt32());
        Assert.Equal(starts, result.GetProperty("publicationCalls").GetInt32());
        if (!canceled)
        {
            Assert.True(result.GetProperty("result").GetProperty("TimedOut").GetBoolean());
            Assert.True(result.GetProperty("failureCopyRejected").GetBoolean());
        }
    }

    [Theory]
    [InlineData("generation", true)]
    [InlineData("task-bytes", false)]
    public async Task ActualPool_StaleGenerationOrTaskRejectsAfterRealWorkerStop(string mode, bool replaced)
    {
        var result = await RunScenario("pool-terminal-" + mode);
        AssertRejectedAndCleaned(result);
        Assert.Equal(1, result.GetProperty("workerStarts").GetInt32());
        Assert.Equal(1, result.GetProperty("publicationCalls").GetInt32());
        Assert.Equal(replaced, result.GetProperty("result").GetProperty("SessionReplaced").GetBoolean());
        Assert.Equal(0, result.GetProperty("result").GetProperty("ExitCode").GetInt32());
        Assert.True(result.GetProperty("result").GetProperty("OutputsSettled").GetBoolean());
        Assert.False(result.GetProperty("result").GetProperty("TimedOut").GetBoolean());
        Assert.True(result.GetProperty("tamperedTaskRetained").GetBoolean());
        Assert.True(result.GetProperty("failureCopyRejected").GetBoolean());
        Assert.True(result.GetProperty("result").GetProperty("StopEvidence").GetProperty("CleanupComplete").GetBoolean());
    }

    [Theory]
    [InlineData("nonzero")]
    [InlineData("missing-proposal")]
    public async Task ActualPool_NonzeroOrMissingProposalCannotBecomeSuccessfulStoppedResult(string mode)
    {
        var result = await RunScenario("pool-terminal-" + mode);
        AssertRejectedAndCleaned(result);
        Assert.Equal(1, result.GetProperty("workerStarts").GetInt32());
        Assert.Equal(0, result.GetProperty("publicationCalls").GetInt32());
        var run = result.GetProperty("result");
        Assert.Equal(mode == "nonzero" ? 23 : 0, run.GetProperty("ExitCode").GetInt32());
        Assert.False(run.GetProperty("TimedOut").GetBoolean());
        Assert.False(run.GetProperty("SessionReplaced").GetBoolean());
        Assert.Equal((int)BookOfEternityClient.Services.GmWorkers.WorkerBridgeState.Failed, run.GetProperty("Status").GetProperty("State").GetInt32());
        Assert.True(result.GetProperty("failureCopyRejected").GetBoolean());
        Assert.True(result.GetProperty("result").GetProperty("OutputsSettled").GetBoolean());
        Assert.True(result.GetProperty("result").GetProperty("StopEvidence").GetProperty("CleanupComplete").GetBoolean());
    }

    private static void AssertRejectedAndCleaned(JsonElement result)
    {
        Assert.False(result.GetProperty("success").GetBoolean());
        Assert.False(result.GetProperty("validatedExecution").GetBoolean());
        Assert.True(result.GetProperty("noCanonicalProposalOrInbox").GetBoolean());
        Assert.True(result.GetProperty("canonicalContextUnchanged").GetBoolean());
        Assert.True(result.GetProperty("workspaceCleaned").GetBoolean(), result.GetProperty("failure").ToString());
        Assert.True(result.GetProperty("stagingCleaned").GetBoolean());
        Assert.Equal(0, result.GetProperty("reaperEntries").GetInt32());
        Assert.Equal(0, result.GetProperty("reaperCapacity").GetInt32());
    }
}
