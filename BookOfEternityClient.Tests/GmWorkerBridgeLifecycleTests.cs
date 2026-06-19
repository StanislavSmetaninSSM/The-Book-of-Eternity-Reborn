using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerBridgeLifecycleTests
{
    [Fact]
    public void BuildInitialStatuses_ReportsDisabledAndStoppedWorkersWithoutLaunchingVisibleWindows()
    {
        var enabled = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        var disabled = GmWorkerBridgeTestFixtures.NarrativeDraftGeminiProfile() with { Enabled = false };

        var statuses = GmWorkerBridgePool.BuildInitialStatuses([enabled, disabled]);

        var enabledStatus = Assert.Single(statuses, status => status.WorkerId == enabled.WorkerId);
        var disabledStatus = Assert.Single(statuses, status => status.WorkerId == disabled.WorkerId);
        Assert.Equal(WorkerBridgeState.Stopped, enabledStatus.State);
        Assert.False(enabledStatus.Ready);
        Assert.Equal(WorkerBridgeState.Disabled, disabledStatus.State);
        Assert.False(disabledStatus.Ready);
    }
}
