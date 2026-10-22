using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    /// <summary>
    /// Compares repeated genuine issued mechanical and continuation checks while retaining all live authority guards.
    /// </summary>
    /// <param name="reuse">
    /// Whether the same production source validators may reuse successful DATA facts.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecoverySourceCache_PreservesGenuineProofsLiveSlotsAndChangedByteRefusal(bool reuse)
    {
        await using var fixture = new Fixture(_output, nameof(RecoverySourceCache_PreservesGenuineProofsLiveSlotsAndChangedByteRefusal));
        await fixture.InitializeAsync();
        var bound = await fixture.PrepareBoundJointAsync(async () =>
        {
            using var scope = fixture.Files.BeginRecoveryValidation(fixture.Lease, reuse);
            var start = fixture.ResetCandidateCapture("RecoveryDataEightRequests");
            for (var i = 0; i < 8; i++) await fixture.ValidateRetainedRecoveryBaselineAsync();
            var counts = fixture.CompleteCandidateCapture("RecoveryDataEightRequests", start);
            foreach (var name in new[] { "Mechanical", "Continuation" })
            {
                Assert.Equal(8, counts.GetValueOrDefault("RecoveryValidation.Request." + name));
                Assert.Equal(reuse ? 1 : 8, counts.GetValueOrDefault("RecoveryValidation.Source." + name));
                Assert.Equal(reuse ? 7 : 0, counts.GetValueOrDefault("RecoveryValidation.Reuse." + name));
            }
            Assert.Equal(8, counts.GetValueOrDefault("JointMechanical.ValidateOwnerAndFingerprint"));
            Assert.Equal(8, counts.GetValueOrDefault("Continuation.InspectConfirmedHeldClaim"));
            Assert.Equal(8, counts.GetValueOrDefault("JointBaseline.ConfirmItemBaseline"));
            Assert.Equal(reuse ? 2 : 0, scope.CaptureState().Entries);
            fixture.Output.WriteLine(JsonSerializer.Serialize(new { Kind = "RecoveryRealProofCounts", Reuse = reuse,
                RequestsPerValidator = 8, Counts = counts, State = scope.CaptureState() }));

            // Supported physical writes must invalidate an already warm continuation fact.
            await fixture.AssertJointCurrentBytesRefusedAndRestoredAsync();
            scope.Dispose();
            Assert.True(scope.CaptureState().Closed);
            using var nextOperation = fixture.Files.BeginRecoveryValidation(fixture.Lease, reuse);
            await fixture.ValidateRetainedRecoveryBaselineAsync();
            var itemStart = fixture.ResetCandidateCapture("RecoveryLiveItemSlotRefusal");
            fixture.AbortRetainedRecoveryItemSlot();
            var refusal = await Assert.ThrowsAsync<InvalidDataException>(fixture.ValidateRetainedRecoveryBaselineAsync);
            Assert.Contains("item slot", refusal.Message, StringComparison.Ordinal);
            var itemCounts = fixture.CompleteCandidateCapture("RecoveryLiveItemSlotRefusal", itemStart);
            Assert.Equal(1, itemCounts.GetValueOrDefault("Continuation.InspectConfirmedHeldClaim"));
            Assert.Equal(1, itemCounts.GetValueOrDefault("JointMechanical.ValidateOwnerAndFingerprint"));
            Assert.Equal(1, itemCounts.GetValueOrDefault("JointBaseline.ConfirmItemBaseline"));
            Assert.Equal(reuse ? 0 : 1, itemCounts.GetValueOrDefault("RecoveryValidation.Source.Mechanical"));
            Assert.Equal(reuse ? 0 : 1, itemCounts.GetValueOrDefault("RecoveryValidation.Source.Continuation"));
            Assert.Equal(new ValidationOperationState(0, 0, true), nextOperation.CaptureState());
        });
        Assert.True(bound.Success, JsonSerializer.Serialize(bound.Issues));
    }

    private sealed partial class Fixture
    {
        /// <summary>
        /// Invokes both unchanged production live wrappers and the final real item-slot guard.
        /// </summary>
        /// <returns>
        /// Completion after the genuine retained common baseline validates under the original lease.
        /// </returns>
        internal Task ValidateRetainedRecoveryBaselineAsync() =>
            WoundAcceptedTurnPlanner.ValidateJointRepairCommonBaselineAsync(Files, Lease,
                _jointBaseline ?? throw new InvalidOperationException("A genuine issued common baseline is required."));

        /// <summary>
        /// Revokes this fixture's actual unpublished in-memory slot through the normal registry abort.
        /// </summary>
        internal void AbortRetainedRecoveryItemSlot() =>
            AcceptedTurnAuthorityRegistry.AbortMortalJointRepairPublication(Files, Lease,
                _jointContinuation ?? throw new InvalidOperationException("A genuine continuation is required."),
                _jointReservation ?? throw new InvalidOperationException("A genuine reservation is required."));
    }
}
