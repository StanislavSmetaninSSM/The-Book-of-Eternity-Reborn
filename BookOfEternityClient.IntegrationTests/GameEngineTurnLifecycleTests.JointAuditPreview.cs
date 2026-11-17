using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task GuaranteedTreatment_AuditPreviewClosesAndFreshRebuildPublishesOnce()
    {
        var previews = new List<GameEngine.MortalJointAuditPreview>();
        var checkpointCount = 0;
        await RunJointCompletionControlAsync(false, useLiveCaller: true,
            auditLifecycleObserver: async (engine, _) =>
            {
                checkpointCount++;
                previews.Add(await engine.CaptureCurrentMortalJointAuditPreviewAsync());
                previews.Add(await engine.CaptureCurrentMortalJointAuditPreviewAsync());
                Assert.Equal(previews[0], previews[1]);
            },
            observeSettledCommonPlan: plan =>
            {
                Assert.NotNull(plan);
                Assert.Equal(2, previews.Count);
                Assert.Equal(previews[0].InputFingerprint, plan.InputFingerprint);
                Assert.Equal(previews[0].PreparedPlanFingerprint, plan.PreparedPlanFingerprint);
            });
        Assert.Equal(1, checkpointCount);
        Assert.Equal(2, previews.Count);
    }
}
