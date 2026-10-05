using Xunit;

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
}
