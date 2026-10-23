using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    /// <summary>
    /// Requires actual deterministic stage replay reduction while retaining fresh admission and damaged-history refusal.
    /// </summary>
    /// <returns>
    /// Completion after the genuine issuer integration, semantic source counts and normal owned fixture closing.
    /// </returns>
    [Fact]
    public async Task JointSemanticData_ReusesStageSourcesAndPreservesRefusals()
    {
        await using var fixture = new Fixture(_output, nameof(JointSemanticData_ReusesStageSourcesAndPreservesRefusals));
        await fixture.InitializeAsync();
        _ = await fixture.PrepareJointStagesAsync();
        await fixture.AssertSemanticStageReuseAsync();
    }

    private sealed partial class Fixture
    {
        /// <summary>
        /// Executes the real preparation and final stages eight times in one selected whole-operation cache lifetime.
        /// </summary>
        /// <returns>
        /// Completion after unchanged fresh admission, warm-data refusal controls and the successful-return source fence.
        /// </returns>
        internal async Task AssertSemanticStageReuseAsync()
        {
            var bundle = _jointBundle ?? throw new InvalidOperationException("A genuine joint binding is required.");
            using var source = Files.BeginSourceDataOperationIfNeeded();
            Assert.NotNull(source);
            Dictionary<string, int> counts;
            using (Files.EnterOperationTimingContext())
            {
                var offset = ResetCandidateCapture("SemanticStageSources");
                for (var i = 0; i < 8; i++)
                {
                    Assert.True(WoundAcceptedTurnPlanner.JointRepairPreparedAgrees(bundle.PreparedPlan));
                    var result = WoundAcceptedTurnPlannerCore.Finalize(bundle.PreparedPlan,
                        new WoundEffectBatchPlanningResult(bundle.EffectBatchPlan, Array.Empty<ValidationIssue>()));
                    Assert.True(result.Success, JsonSerializer.Serialize(result.Issues));
                    Assert.Equal(bundle.WoundFinalPlanFingerprint, result.Plan!.WoundFinalPlanFingerprint);
                }
                counts = CompleteCandidateCapture("SemanticStageSources", offset);
                Assert.True(counts.GetValueOrDefault("WoundPlanner.FinalizationHandoff") >= 8);
                Assert.True(counts.GetValueOrDefault("WoundPlanner.JointFinalAdmission") >= 8);
                Assert.Equal(1, counts.GetValueOrDefault("JointPreparation.Replay"));
                Assert.Equal(1, counts.GetValueOrDefault("JointFinal.Reduce"));
                AssertWarmSemanticCopyContracts(bundle);
            }
            await source.CompleteAsync();
        }

        /// <summary>
        /// Rechecks independent returned DATA and damaged private history against warmed expected reduction DATA.
        /// </summary>
        /// <param name="bundle">
        /// The genuinely issued stage bundle whose normal unpublished reservation remains owned by this fixture.
        /// </param>
        private void AssertWarmSemanticCopyContracts(AcceptedMechanicsWoundStageBundle bundle)
        {
            var expected = ReadBundleView(bundle);
            var copy = bundle.DetachedCopy();
            MutateReturnedBundleData(copy);
            Assert.Equal(expected, ReadBundleView(copy));
            Assert.Equal(expected, ReadBundleView(bundle));
            using (bundle.DamageRetainedFinalHistoryForComponent())
            {
                var fullFailure = Assert.Throws<ArgumentException>(() =>
                    new AcceptedMechanicsWoundStageBundle(bundle.Input, bundle.PreparedPlan,
                        bundle.EffectBatchPlan, bundle.FinalPlan));
                var copyFailure = Assert.Throws<ArgumentException>(() => bundle.DetachedCopy());
                Assert.Equal("finalPlan", fullFailure.ParamName);
                Assert.Equal(fullFailure.ParamName, copyFailure.ParamName);
                Assert.Equal(fullFailure.Message, copyFailure.Message);
            }
            Assert.Equal(expected, ReadBundleView(bundle.DetachedCopy()));
            CloseJointReservation();
            AcceptedMechanicsWoundStageBundle? fullAfterAbort = null, copyAfterAbort = null;
            var fullError = Record.Exception(() => fullAfterAbort = new(bundle.Input, bundle.PreparedPlan,
                bundle.EffectBatchPlan, bundle.FinalPlan));
            var copyError = Record.Exception(() => copyAfterAbort = bundle.DetachedCopy());
            Assert.Equal(fullError?.GetType(), copyError?.GetType());
            Assert.Equal(fullError?.Message, copyError?.Message);
            if (fullError is null)
            {
                Assert.Equal(expected, ReadBundleView(fullAfterAbort!));
                Assert.Equal(expected, ReadBundleView(copyAfterAbort!));
            }
        }
    }
}
