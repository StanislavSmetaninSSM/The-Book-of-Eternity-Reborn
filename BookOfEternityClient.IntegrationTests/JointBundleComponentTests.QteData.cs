using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    /// <summary>
    /// Reuses actual QTE DATA under genuine owners, retains fresh proofs and refuses an appearing lifecycle context.
    /// </summary>
    /// <returns>
    /// Completion after real source counts, warm source refusal and ordinary owned cleanup.
    /// </returns>
    [Fact]
    public async Task JointQteData_ReusesActualReconstructionAndRetainsFreshProofs()
    {
        await using var fixture = new Fixture(_output, nameof(JointQteData_ReusesActualReconstructionAndRetainsFreshProofs), passiveTimingEvents: true);
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
        /// Completion after one reconstruction, three entry/exit proof pairs and an expected warm-context refusal.
        /// </returns>
        internal async Task AssertQteDataReuseAsync()
        {
            var resource = _jointResourceAuthority ?? throw new InvalidOperationException("A genuine resource owner is required.");
            using var source = Files.BeginSourceDataOperationIfNeeded();
            Assert.NotNull(source);
            using var validation = Files.BeginRecoveryValidation(Lease);
            // The real async validators own their synchronous timing contexts;
            // a thread-local context must never span these awaits.
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
            await Files.VerifySourceDataAsync(Lease);
            var context = Files.ResolvePath("game_state/control/validation_repair_request.json");
            Assert.False(File.Exists(context));
            Directory.CreateDirectory(Path.GetDirectoryName(context)!);
            try
            {
                File.WriteAllText(context, "{}");
                await Assert.ThrowsAsync<InvalidDataException>(() =>
                    WoundAcceptedTurnPlanner.ValidateJointRepairResourcePublicationAsync(Files, Lease, resource));
                Assert.Equal(0, validation.CaptureState().Entries);
            }
            finally { File.Delete(context); }
            // This operation deliberately ends in the original refusal. Its failed
            // owner is disposed rather than completed as a successful operation.
        }
    }
}
