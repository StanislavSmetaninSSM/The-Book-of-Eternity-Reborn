using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public Task GuaranteedTreatment_ActualLivePlayerTurnCompletesNonzeroJointRepairExactlyOnce() =>
        RunJointCompletionControlAsync(nonzeroProgression: true, useLiveCaller: true);

    [Fact]
    public Task GuaranteedTreatment_RealAppendedRowAfterReadbackFailureFreshProcessCompletesWithoutAppend() =>
        RunJointCompletionControlAsync(nonzeroProgression: true, pauseAppliedForCold: true,
            refusalScenario: "story_readback_failure", recoverRetainedStoryRow: true);
}
