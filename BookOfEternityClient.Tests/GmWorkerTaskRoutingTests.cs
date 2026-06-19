using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerTaskRoutingTests
{
    [Fact]
    public void SelectWorkerForTask_ReturnsEnabledProfileWithRequestedTaskType()
    {
        var profiles = new[]
        {
            GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile() with { Enabled = false },
            GmWorkerBridgeTestFixtures.NarrativeDraftGeminiProfile()
        };

        var result = GmWorkerBridgePool.SelectWorkerForTask(profiles, WorkerTaskType.NarrativeDraft);

        Assert.True(result.Found, result.Reason);
        Assert.Equal("narrative_draft_gemini", result.Profile!.WorkerId);
    }

    [Fact]
    public void SelectWorkerForTask_ReturnsFailureWhenNoSuitableWorkerAvailable()
    {
        var profiles = new[]
        {
            GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile() with { Enabled = false }
        };

        var result = GmWorkerBridgePool.SelectWorkerForTask(profiles, WorkerTaskType.ValidationRepair);

        Assert.False(result.Found);
        Assert.Null(result.Profile);
        Assert.Contains("No enabled worker", result.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
