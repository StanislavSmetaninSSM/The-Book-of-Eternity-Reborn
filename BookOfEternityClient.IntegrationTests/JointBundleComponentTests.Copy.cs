using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    private sealed partial class Fixture
    {
        /// <summary>
        /// Copies the genuinely bound joint bundle in its own complete observation slice.
        /// Requires preparation, effect and final admission while refusing redundant final reduction.
        /// </summary>
        internal void AssertDirectJointCopyRetainsAdmissionWithoutFinalReduction()
        {
            var bundle = _jointBundle ?? throw new InvalidOperationException("A genuine joint binding is required.");
            var offset = ResetCandidateCapture("DirectJointCopy");
            AcceptedMechanicsWoundStageBundle copy;
            using (Files.EnterOperationTimingContext())
            using (Files.MeasureOperation("JointBundleCandidate.DirectJointCopy"))
                copy = bundle.DetachedCopy();
            var counts = CompleteCandidateCapture("DirectJointCopy", offset);
            Assert.True(counts.GetValueOrDefault("WoundPlanCache.ValidatePreparedResult") > 0);
            Assert.True(counts.GetValueOrDefault("WoundEffect.AcceptEffectResult") > 0);
            Assert.Equal(0, counts.GetValueOrDefault("WoundPlanner.Finalize"));
            Assert.Equal(0, counts.GetValueOrDefault("WoundPlanCache.FinalizationReplay"));
            Assert.True(counts.GetValueOrDefault("WoundPlanner.FinalizationHandoff") > 0);
            Assert.True(counts.GetValueOrDefault("WoundPlanner.JointFinalAdmission") > 0);
            Assert.NotSame(bundle, copy);
            Assert.Equal(bundle.BundleFingerprint, copy.BundleFingerprint);
            Assert.Equal(bundle.WoundFinalPlanFingerprint, copy.WoundFinalPlanFingerprint);
        }
    }
}
