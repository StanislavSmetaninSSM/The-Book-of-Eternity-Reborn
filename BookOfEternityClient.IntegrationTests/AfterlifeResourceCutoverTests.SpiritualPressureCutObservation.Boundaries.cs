using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Fresh independent owners reject foreign/duplicate requests and preserve the pending request until explicit disposal.</summary>
    [Fact]
    public Task SpiritualPressureCutObservation_RequestsRejectForeignDuplicateAndDisposedOwners() =>
        WithPressureTransitionAsync(SpiritualPressureThirdGuardTrigger.OwnApSpent, async a =>
            await WithPressureTransitionAsync(SpiritualPressureThirdGuardTrigger.OwnApSpent, async b =>
            {
                Assert.NotEqual(a.Context.RootPath, b.Context.RootPath);
                var baselineA = await CapturePressureTransitionBaselineAsync(a, "request_owner_a");
                var baselineB = await CapturePressureTransitionBaselineAsync(b, "request_owner_b");
                var countsA = a.Capture.ReadExistingStrainReuseCounts();
                var countsB = b.Capture.ReadExistingStrainReuseCounts();
                var workA = a.Capture.ReadExistingPressureGroupComputationCounts();
                var workB = b.Capture.ReadExistingPressureGroupComputationCounts();
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await a.Capture.RequestPressureCutObservationAsync(b.Lease, a.Admission));
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await a.Capture.RequestPressureCutObservationAsync(a.Lease, b.Admission));
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await b.Capture.RequestPressureCutObservationAsync(b.Lease, a.Admission));
                Assert.Null(a.Capture.ReadPressureCutObservation());
                Assert.Null(b.Capture.ReadPressureCutObservation());
                await a.Capture.RequestPressureCutObservationAsync(a.Lease, a.Admission);
                var duplicate = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await a.Capture.RequestPressureCutObservationAsync(a.Lease, a.Admission));
                Assert.Equal("A one-shot cut observation cannot be requested again.", duplicate.Message);
                var foreignAttempt = await a.Capture.MaterializeWoundAsync(a.Lease, b.Admission,
                    b.Decision, PressureTransitionScene);
                Assert.Null(foreignAttempt.Wound);
                Assert.Equal("spiritual_wound_frontier_mismatch", Assert.Single(foreignAttempt.Issues).Code);
                Assert.Null(a.Capture.ReadPressureCutObservation());
                Assert.Equal(countsA, a.Capture.ReadExistingStrainReuseCounts());
                Assert.Equal(countsB, b.Capture.ReadExistingStrainReuseCounts());
                Assert.Equal(workA, a.Capture.ReadExistingPressureGroupComputationCounts());
                Assert.Equal(workB, b.Capture.ReadExistingPressureGroupComputationCounts());
                await AssertPressureTransitionInertAsync(a, baselineA);
                await AssertPressureTransitionInertAsync(b, baselineB);
                a.Capture.Dispose();
                var cancelled = Assert.IsType<ValidationService.SpiritualPressureCutObservationResult>(a.Capture.ReadPressureCutObservation());
                Assert.Equal(ValidationService.SpiritualPressureCutObservationStatus.Unavailable, cancelled.Status);
                Assert.Null(cancelled.Observation);
                Assert.Equal("The capture was disposed before the requested materialization.", cancelled.Reason);
                await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                    await a.Capture.RequestPressureCutObservationAsync(a.Lease, a.Admission));
                Assert.Same(cancelled, a.Capture.ReadPressureCutObservation());
                Assert.Null(b.Capture.ReadPressureCutObservation());
                Assert.True(b.Capture.IsCurrentOwner);
                AssertExistingPressureInputInert(a.Context, a.Lease, baselineA.Files, baselineA.Generation);
                await AssertPressureTransitionInertAsync(b, baselineB);
            }, expectedActualEvaluations: 0), expectedActualEvaluations: 0, revokesCapture: true);

    /// <summary>Actual source replacement makes the old admission stale; its owner exception remains visible and is recorded as failure.</summary>
    [Fact]
    public Task SpiritualPressureCutObservation_RevokedSourcePreservesOwnerFailure() =>
        WithPressureTransitionAsync(SpiritualPressureThirdGuardTrigger.OwnApSpent, async window =>
        {
            var baseline = await CapturePressureTransitionBaselineAsync(window, "before_source_revocation");
            var counts = window.Capture.ReadExistingStrainReuseCounts();
            var work = window.Capture.ReadExistingPressureGroupComputationCounts();
            await window.Capture.RequestPressureCutObservationAsync(window.Lease, window.Admission);
            var replacement = await window.Context.Validator.BeginSpiritualWoundSourceSessionAsync(window.Lease);
            try
            {
                Assert.False(window.Capture.IsCurrentOwner);
                Assert.False(window.Capture.OwnsWoundAdmission(window.Admission));
                var refused = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await window.Capture.RequestPressureCutObservationAsync(window.Lease, window.Admission));
                Assert.Equal("The original turn capture was revoked.", refused.Message);
                var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await window.Capture.MaterializeWoundAsync(window.Lease, window.Admission,
                        window.Decision, PressureTransitionScene));
                Assert.Equal(refused.Message, error.Message);
                var failed = Assert.IsType<ValidationService.SpiritualPressureCutObservationResult>(window.Capture.ReadPressureCutObservation());
                Assert.Equal(ValidationService.SpiritualPressureCutObservationStatus.Failure, failed.Status);
                Assert.Null(failed.Observation);
                Assert.Equal(error.GetType().Name + ": " + error.Message, failed.Reason);
                Assert.Equal(counts, window.Capture.ReadExistingStrainReuseCounts());
                Assert.Equal(work, window.Capture.ReadExistingPressureGroupComputationCounts());
                AssertExistingPressureInputInert(window.Context, window.Lease, baseline.Files, baseline.Generation);
                window.Capture.Dispose();
                Assert.Same(failed, window.Capture.ReadPressureCutObservation());
            }
            finally { replacement.Session?.Revoke(); }
        }, expectedActualEvaluations: 0, revokesCapture: true);

    /// <summary>A normal selected-decision conflict consumes the observation as failure without a cut, allocation or diagnostic query.</summary>
    [Fact]
    public Task SpiritualPressureCutObservation_RejectedDecisionRecordsFailureWithoutCut() =>
        WithPressureTransitionAsync(SpiritualPressureThirdGuardTrigger.OwnApSpent, async window =>
        {
            var baseline = await CapturePressureTransitionBaselineAsync(window, "before_rejected_decision");
            var counts = window.Capture.ReadExistingStrainReuseCounts();
            var work = window.Capture.ReadExistingPressureGroupComputationCounts();
            await window.Capture.RequestPressureCutObservationAsync(window.Lease, window.Admission);
            var refused = await window.Capture.MaterializeWoundAsync(window.Lease, window.Admission,
                window.Decision, "A different scene conflicts with the retained selection.");
            Assert.Null(refused.Wound);
            Assert.Equal("spiritual_wound_selection_conflict", Assert.Single(refused.Issues).Code);
            var failed = Assert.IsType<ValidationService.SpiritualPressureCutObservationResult>(window.Capture.ReadPressureCutObservation());
            Assert.Equal(ValidationService.SpiritualPressureCutObservationStatus.Failure, failed.Status);
            Assert.Null(failed.Observation);
            Assert.Equal("spiritual_wound_selection_conflict", failed.Reason);
            var after = window.Capture.ReadExistingStrainReuseCounts();
            Assert.Equal(counts.ActualEvaluations + 1, after.ActualEvaluations);
            Assert.Equal((counts.Queries, counts.OrdinaryEvaluations, counts.SignedParentReads),
                (after.Queries, after.OrdinaryEvaluations, after.SignedParentReads));
            Assert.Equal(work, window.Capture.ReadExistingPressureGroupComputationCounts());
            await AssertPressureTransitionInertAsync(window, baseline);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await window.Capture.RequestPressureCutObservationAsync(window.Lease, window.Admission));
            Assert.Same(failed, window.Capture.ReadPressureCutObservation());
        }, expectedActualEvaluations: 1);
}
