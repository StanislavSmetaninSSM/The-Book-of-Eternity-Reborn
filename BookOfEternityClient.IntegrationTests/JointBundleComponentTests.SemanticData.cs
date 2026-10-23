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
        var bound = await fixture.PrepareBoundJointAsync();
        Assert.True(bound.Success, JsonSerializer.Serialize(bound.Issues));
        await fixture.AssertSemanticStageReuseAsync(bound.Input!, bound.Authority!);
    }

    private sealed partial class Fixture
    {
        /// <summary>
        /// Executes the real preparation and final stages eight times in one selected whole-operation cache lifetime.
        /// </summary>
        /// <param name="input">
        /// The genuinely bound common input produced by this fixture's real issuer.
        /// </param>
        /// <param name="authority">
        /// The matching private common authority retained for the existing refusal and abort controls.
        /// </param>
        /// <returns>
        /// Completion after unchanged fresh admission, warm-data refusal controls and the successful-return source fence.
        /// </returns>
        internal async Task AssertSemanticStageReuseAsync(AcceptedMechanicsInput input,
            WoundAcceptedTurnPlanner.MortalJointRepairCommonAuthority authority)
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
                // Reuse the original constructor/copy/mutable-return/private-history
                // corruption/restoration and aborted reservation parity controls.
                AssertJointCopyContracts(input, authority);
            }
            await source.CompleteAsync();
        }
    }
}
