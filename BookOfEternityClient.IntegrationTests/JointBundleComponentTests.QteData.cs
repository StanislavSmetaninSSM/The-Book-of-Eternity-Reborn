using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    /// <summary>
    /// Reuses actual QTE reconstruction DATA under genuine current owners while retaining all fresh proof boundaries.
    /// </summary>
    /// <returns>
    /// Completion after real source counts, fresh baseline/exit checks and ordinary owned cleanup.
    /// </returns>
    [Fact]
    public async Task JointQteData_ReusesActualReconstructionAndRetainsFreshProofs()
    {
        await using var fixture = new Fixture(_output, nameof(JointQteData_ReusesActualReconstructionAndRetainsFreshProofs));
        await fixture.InitializeAsync();
        var bound = await fixture.PrepareBoundJointAsync(fixture.AssertQteDataReuseAsync, reuseStableData: true);
        Assert.True(bound.Success, JsonSerializer.Serialize(bound.Issues));
    }

    private sealed partial class Fixture
    {
        /// <summary>
        /// Exercises the actual resource/QTE path three times with the real private baseline and confirmed held request.
        /// </summary>
        /// <returns>
        /// Completion after one reconstruction and three independent entry/exit proof pairs.
        /// </returns>
        internal async Task AssertQteDataReuseAsync()
        {
            var resource = _jointResourceAuthority ?? throw new InvalidOperationException("A genuine resource owner is required.");
            using var source = Files.BeginSourceDataOperationIfNeeded();
            Assert.NotNull(source);
            using var validation = Files.BeginRecoveryValidation(Lease);
            using (Files.EnterOperationTimingContext())
            {
                var offset = ResetCandidateCapture("QteCalculationSources");
                for (var i = 0; i < 3; i++)
                    await WoundAcceptedTurnPlanner.ValidateJointRepairResourcePublicationAsync(Files, Lease, resource);
                var counts = CompleteCandidateCapture("QteCalculationSources", offset);
                Assert.Equal(1, counts.GetValueOrDefault("JointQte.PrepareAsync"));
                Assert.Equal(1, counts.GetValueOrDefault("JointQte.ReconstructAsync"));
                Assert.Equal(1, counts.GetValueOrDefault("JointQte.RevalidateCorrectionAsync"));
                Assert.Equal(2, counts.GetValueOrDefault("JointQte.CalculationReused"));
                Assert.Equal(6, counts.GetValueOrDefault("JointBaseline.ValidateCurrentAsync"));
                Assert.Equal(3, counts.GetValueOrDefault("ValidationReadSet.ExitProof"));
            }
            await source.CompleteAsync();
        }
    }
}
