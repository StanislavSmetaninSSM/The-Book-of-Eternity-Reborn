using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    /// <summary>
    /// Measures genuine terminal stage preparation and two isolated copies without entering common binding.
    /// </summary>
    /// <returns>Completion after terminal DATA, current ownership, detached copies and unchanged source checks.</returns>
    [Fact]
    public async Task JointTerminalPreparation_MeasuresIndependentCopiesBeforeCommonBinding()
    {
        await using var fixture = new Fixture(_output,
            nameof(JointTerminalPreparation_MeasuresIndependentCopiesBeforeCommonBinding),
            passiveTimingEvents: true, gzipRecords: true, continuingPassiveTimings: true);
        var initialize = fixture.ResetCandidateCapture("A5.Terminal.Initialize");
        await fixture.InitializeAsync(terminalTreatment: true);
        _ = fixture.CompleteCandidateCapture("A5.Terminal.Initialize", initialize);
        var preparation = fixture.ResetCandidateCapture("A5.Terminal.StagePreparation");
        var bundle = await fixture.PrepareJointStagesAsync(measureStageCalls: true);
        _ = fixture.CompleteCandidateCapture("A5.Terminal.StagePreparation", preparation);
        await fixture.AssertTerminalPreparationCopiesAsync(bundle);
    }

    private sealed partial class Fixture
    {
        /// <summary>
        /// Separates two actual copy intervals from terminal inspection, full DATA comparisons and fresh source checks.
        /// </summary>
        /// <param name="bundle">The genuine terminal bundle retained by this fixture's unpublished reservation.</param>
        /// <returns>Completion after the existing source owner closes and fresh currentness/bytes remain unchanged.</returns>
        internal async Task AssertTerminalPreparationCopiesAsync(AcceptedMechanicsWoundStageBundle bundle)
        {
            var original = RequireIssuedCandidateStage(bundle);
            Assert.Null(_jointBaseline); Assert.Null(_jointResourceAuthority); Assert.Null(_jointBound);
            var prepared = bundle.PreparedPlan;
            var continuation = prepared.JointRepairContinuationAuthority!;
            var inspection = ResetCandidateCapture("A5.Terminal.BeforeCopyOracles");
            Assert.True(WoundAcceptedTurnPlanner.TryReadTerminalTreatment(prepared, out var terminal));
            Assert.True(terminal.Outcome.PreparesTerminalHeal);
            Assert.True(EffectAcceptedTurnPlanner.TryReadMortalTreatmentExecution(prepared,
                bundle.EffectBatchPlan.EffectPlan, out var stages));
            Assert.Equal(2, stages.Count);
            Assert.Equal("healed", stages[1].After.Lifecycle);
            await WoundAcceptedTurnPlanner.ValidateJointRepairCurrentAsync(Files, Lease, continuation);
            var beforeImages = await CaptureCandidateInvocationBeforeImagesAsync();
            var expected = ReadBundleView(bundle);
            _ = CompleteCandidateCapture("A5.Terminal.BeforeCopyOracles", inspection);

            var copies = new AcceptedMechanicsWoundStageBundle[2];
            using (var source = Files.BeginSourceDataOperationIfNeeded())
            {
                Assert.NotNull(source);
                var semanticOwner = Files.GetCurrentSemanticDataOperation();
                Assert.NotNull(semanticOwner);
                for (var index = 0; index < copies.Length; index++)
                {
                    var phase = "A5.Terminal.DetachedCopy" + (index + 1);
                    var offset = ResetCandidateCapture(phase);
                    using (Files.EnterOperationTimingContext())
                    using (Files.MeasureOperation(phase))
                        copies[index] = bundle.DetachedCopy();
                    var counts = CompleteCandidateCapture(phase, offset);
                    Assert.Equal(1, counts.GetValueOrDefault("WoundBundle.DetachedCopy"));
                    Assert.True(counts.GetValueOrDefault("WoundPlanCache.ValidatePreparedResult") > 0);
                    Assert.True(counts.GetValueOrDefault("WoundEffect.AcceptEffectResult") > 0);
                    Assert.True(counts.GetValueOrDefault("WoundPlanner.JointFinalAdmission") > 0);
                    Assert.Same(semanticOwner, Files.GetCurrentSemanticDataOperation());
                }
                var closing = ResetCandidateCapture("A5.Terminal.SourceClosing");
                await source.CompleteAsync();
                _ = CompleteCandidateCapture("A5.Terminal.SourceClosing", closing);
            }

            var oracles = ResetCandidateCapture("A5.Terminal.AfterCopyOracles");
            Assert.Null(Files.GetCurrentSemanticDataOperation());
            Assert.NotSame(copies[0], copies[1]);
            foreach (var copy in copies)
            {
                Assert.NotSame(bundle, copy);
                Assert.Equal(expected, ReadBundleView(copy));
                Assert.Same(continuation, copy.PreparedPlan.JointRepairContinuationAuthority);
                MutateReturnedBundleData(copy);
                Assert.Equal(expected, ReadBundleView(copy));
            }
            Assert.Equal(expected, ReadBundleView(bundle));
            Assert.True(Lease.IsActive);
            Assert.True(Files.IsCurrentSessionGeneration(Lease, original.Generation));
            await WoundAcceptedTurnPlanner.ValidateJointRepairCurrentAsync(Files, Lease, continuation);
            foreach (var row in beforeImages)
                Assert.Equal(row.Value, await Files.ReadFileBytesAsync(Lease, row.Key));
            _ = CompleteCandidateCapture("A5.Terminal.AfterCopyOracles", oracles);
            Output.WriteLine(JsonSerializer.Serialize(new
            {
                Kind = "A5TerminalPreparationOracles", IndependentCopies = copies.Length,
                TerminalStages = stages.Count, CurrentGeneration = true, UnchangedPaths = beforeImages.Keys.ToArray(),
                CommonBindingEntered = false, Published = false
            }));
        }
    }
}
