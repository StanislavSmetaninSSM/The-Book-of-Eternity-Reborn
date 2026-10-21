using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    private sealed partial class Fixture
    {
        /// <summary>
        /// Requires the genuine retained continuation to reject changed physical bytes and accept their exact restoration.
        /// Appends JSON whitespace so the control changes bytes without changing the accepted narrative's JSON data.
        /// </summary>
        /// <returns>
        /// Completion after fresh validation, changed-byte refusal, exact restoration and fresh validation again.
        /// A failed control retains the fixture for diagnosis.
        /// </returns>
        internal async Task AssertJointCurrentBytesRefusedAndRestoredAsync()
        {
            using var operationTiming = Files.MeasureOperation("JointBundleCandidate.CurrentByteRefusalAndRestoration");
            var continuation = _jointContinuation ?? throw new InvalidOperationException("A genuine joint continuation is required.");
            const string path = "output/narrative_response.json";
            try
            {
                await WoundAcceptedTurnPlanner.ValidateJointRepairCurrentAsync(Files, Lease, continuation);
                var original = await Files.ReadFileBytesAsync(Lease, path)
                    ?? throw new InvalidDataException("The genuine accepted narrative must be present.");
                var changed = new byte[original.Length + 1];
                original.CopyTo(changed, 0);
                changed[^1] = (byte)' ';
                InvalidDataException refusal;
                try
                {
                    await Files.WriteFileAtomicBytesAsync(Lease, path, changed);
                    Assert.Equal(changed, await Files.ReadFileBytesAsync(Lease, path));
                    refusal = await Assert.ThrowsAsync<InvalidDataException>(() =>
                        WoundAcceptedTurnPlanner.ValidateJointRepairCurrentAsync(Files, Lease, continuation));
                }
                finally
                {
                    await Files.WriteFileAtomicBytesAsync(Lease, path, original);
                }
                Assert.Equal(original, await Files.ReadFileBytesAsync(Lease, path));
                await WoundAcceptedTurnPlanner.ValidateJointRepairCurrentAsync(Files, Lease, continuation);
                _output.WriteLine(JsonSerializer.Serialize(new
                {
                    Kind = "JointCurrentByteContracts", Path = path,
                    InitialCurrentValidationPassed = true, PhysicalBytesRefused = true,
                    Refusal = refusal.Message, ExactBytesRestored = true, RestoredCurrentValidationPassed = true
                }));
            }
            catch
            {
                _retained = true;
                throw;
            }
        }
    }
}
