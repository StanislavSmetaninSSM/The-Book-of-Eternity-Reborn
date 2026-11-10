using System.Diagnostics;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>A lawful single-exchange owner materializes without a supported ordinary comparison, then rejects its genuinely expired lease.</summary>
    [Fact]
    public async Task SpiritualPressureCutObservation_UnavailableComparisonAndExpiredLeasePreserveOwnerOutcomes()
    {
        ResourceMaterializationTestContext? context = null;
        FileSystemManager.CanonicalWriteLease? openLease = null;
        ValidationService.SpiritualOriginalTurnCapture? capture = null;
        var started = Stopwatch.GetTimestamp();
        try
        {
            context = await CreateCompleteConflictFrameContextAsync();
            await WriteCompleteConflictFrameExchangeAsync(context);
            openLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            var lease = openLease;
            var acquired = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
            AssertNoConflictFrameErrors(acquired.Issues);
            capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(acquired.Capture);
            AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
            var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
            AssertNoConflictFrameErrors(advanced.Issues);
            Assert.NotNull(advanced.Step!.Interval);
            var admitted = await capture.AdmitLatestClosedWoundSourceAsync(lease, "opposition");
            AssertNoConflictFrameErrors(admitted.Issues);
            var admission = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture.WoundSourceAdmission>(admitted.Admission);
            var opportunity = await capture.ReadWoundOpportunityAsync(lease, admission);
            AssertNoConflictFrameErrors(opportunity.Issues);
            var decision = OriginalSpiritualWoundDecision(opportunity.Opportunity!.PublicRef);
            var files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
            var generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
            var counts = capture.ReadExistingStrainReuseCounts();
            var work = capture.ReadExistingPressureGroupComputationCounts();
            await capture.RequestPressureCutObservationAsync(lease, admission);
            var materialized = await capture.MaterializeWoundAsync(lease, admission, decision,
                "Чужое давление надломило волю хранителя.");
            AssertNoConflictFrameErrors(materialized.Issues);
            var wound = Assert.IsType<WoundMaterializationEnvelope>(materialized.Wound);
            Assert.Equal((1, "I"), (wound.Severity.Rank, wound.Severity.Value));
            Assert.Single(wound.Consequences.OwnedEffectSources.RootBindings);
            Assert.False(admission.Draft.HasPendingWoundIntegration);
            var report = Assert.IsType<ValidationService.SpiritualPressureCutObservationResult>(capture.ReadPressureCutObservation());
            Assert.Equal(ValidationService.SpiritualPressureCutObservationStatus.Unavailable, report.Status);
            Assert.Null(report.Observation);
            Assert.StartsWith("The cold ordinary comparison is unavailable:", report.Reason);
            var after = capture.ReadExistingStrainReuseCounts();
            Assert.Equal(counts.ActualEvaluations + 1, after.ActualEvaluations);
            Assert.Equal(counts.OrdinaryEvaluations, after.OrdinaryEvaluations);
            Assert.Equal(counts.SignedParentReads, after.SignedParentReads);
            Assert.Equal(work, capture.ReadExistingPressureGroupComputationCounts());
            AssertExistingPressureInputInert(context, lease, files, generation);
            var cursor = capture.ReadAllocationCursor(lease);
            var journal = capture.ReadAllocationJournal(lease).ToJsonString();
            Assert.True(cursor > 0);

            await lease.DisposeAsync();
            openLease = null;
            var expired = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await capture.RequestPressureCutObservationAsync(lease, admission));
            var ownerError = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await capture.MaterializeWoundAsync(lease, admission, decision,
                    "Чужое давление надломило волю хранителя."));
            Assert.Equal(expired.Message, ownerError.Message);
            Assert.Same(report, capture.ReadPressureCutObservation());
            Assert.Equal(after, capture.ReadExistingStrainReuseCounts());
            Assert.Equal(work, capture.ReadExistingPressureGroupComputationCounts());
            openLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            Assert.Equal(cursor, capture.ReadAllocationCursor(openLease));
            Assert.Equal(journal, capture.ReadAllocationJournal(openLease).ToJsonString());
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await capture.RequestPressureCutObservationAsync(openLease, admission));
            capture.Dispose();
            Assert.Same(report, capture.ReadPressureCutObservation());
            AssertExistingPressureInputInert(context, openLease, files, generation);
            AssertPressureNoPrivatePair(context, openLease);
        }
        finally
        {
            capture?.Dispose();
            if (openLease != null) await openLease.DisposeAsync();
            if (context != null)
            {
                await context.DisposeAsync();
                Assert.False(Directory.Exists(context.RootPath));
            }
            WritePressureOwnerTiming("cut_unavailable_total", "single_exchange_unavailable", started);
        }
    }
}
