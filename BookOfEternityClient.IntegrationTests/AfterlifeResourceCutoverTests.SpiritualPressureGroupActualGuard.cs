using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Valid singleton ordinaryIII remains DATA, while its old hypothetical IV actual lane stays closed.</summary>
    [Fact]
    public Task SpiritualPressureGroup_SingletonOrdinaryThreeCannotAdvanceActual() =>
        WithPressureGroupCaseAsync(new("group_actual_ordinary_three", Parents: 1, After: "overwhelmed"), async window =>
            await AssertPressureGroupActualGuardAsync(window,
                "actual existing pressure requires ordinary + 1 below broken"));

    /// <summary>Another applicable cost-only wound remains evaluated for comparison and excluded by the legacy actual adapter.</summary>
    [Fact]
    public Task SpiritualPressureGroup_OtherWoundCostCannotAdvanceActual() =>
        WithPressureGroupCaseAsync(new("group_actual_other_wound", Parents: 1, PlayerBefore: 4, PlayerCost: 4,
            OtherCost: true), async window =>
            await AssertPressureGroupActualGuardAsync(window,
                "actual existing pressure excludes another pressure wound source"));

    /// <summary>Normal count/rank/caps and signed resilience3 discriminate the moved positive-actual-ceiling refusal.</summary>
    [Fact]
    public Task SpiritualPressureGroup_NonpositiveActualMaximumCannotAdvance() =>
        WithPressureGroupCaseAsync(new("group_actual_nonpositive", Parents: 1, After: "strained", Resilience: 3), async window =>
        {
            var expectedOrdinary = SpiritualWoundOpportunityMath.Calculate(new(8, 0, 3, "clear", "strained", "hostile", 4))!;
            var expectedActual = SpiritualWoundOpportunityMath.Calculate(new(8, 0, 3, "clear", "fractured", "hostile", 4))!;
            Assert.Equal((2L, 0), (expectedOrdinary.TraumaPressure, expectedOrdinary.MaximumSeverityRank));
            Assert.Equal((7L, 0), (expectedActual.TraumaPressure, expectedActual.MaximumSeverityRank));
            Assert.Equal((2, 4, 4), (expectedActual.NewStrainRank, expectedActual.DangerModeSeverityCap,
                expectedActual.Input.SourceSeverityCap));
            await AssertPressureGroupActualGuardAsync(window,
                "actual existing pressure requires a positive hypothetical actual maximum");
        });

    /// <summary>
    /// Requires an independently successful real comparison, exact adapter diagnosis, no actual evaluator
    /// entry, terminal revocation and complete physical inertness. Disposed owners are not probed as live.
    /// </summary>
    private async Task AssertPressureGroupActualGuardAsync(PressureGroupWindow window, string expectedDetail)
    {
        var result = await ReadPressureGroupComparisonAsync(window);
        AssertPressureGroupComparison(window, result);
        await AssertPressureGroupWindowInertAsync(window);
        var counts = window.Capture.ReadExistingStrainReuseCounts();
        Assert.Equal(1, counts.OrdinaryEvaluations);
        Assert.Equal(0, counts.ActualEvaluations);
        var sourceCounts = window.Capture.ReadExistingPressureGroupComputationCounts();
        Assert.Null(window.Capture.ReadExistingPressureActualRefusalObservation());
        await window.Capture.RequestExistingPressureActualRefusalObservationAsync(window.Lease);
        Assert.Null(window.Capture.ReadExistingPressureActualRefusalObservation());
        Assert.Equal(counts, window.Capture.ReadExistingStrainReuseCounts());
        Assert.Equal(sourceCounts, window.Capture.ReadExistingPressureGroupComputationCounts());
        AssertPressureOrdinaryFrontiersEqual(window.Frontier,
            await window.Capture.ReadExistingPressureOrdinaryProbeStateAsync(window.Lease));
        var refused = await MeasurePressureOwnerPhaseAsync("group_actual_guard", window.Spec.Name,
            () => window.Capture.AdvanceNextResourceExchangeAsync(window.Lease));
        Assert.Null(refused.Step);
        var issue = Assert.Single(refused.Issues);
        Assert.Equal("spiritual_pressure_ordinary_contour_unsupported", issue.Code);
        Assert.Equal(AfterlifeSpiritualConflictState.StatePath, issue.FilePath);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Equal(expectedDetail, issue.Actual);
        var retained = Assert.IsType<ValidationService.SpiritualPressureActualRefusalObservationResult>(
            window.Capture.ReadExistingPressureActualRefusalObservation());
        Assert.Null(retained.UnavailableReason);
        var observed = Assert.IsType<ValidationService.SpiritualPressureActualRefusalObservation>(retained.Observation);
        AssertPressureOrdinaryFrontiersEqual(window.Frontier, observed.Frontier);
        Assert.Equal(new ValidationService.SpiritualPressureProjectionInventory(0, 0, 0), observed.Projections);
        Assert.NotNull(retained.Issues);
        var frozen = Assert.Single(retained.Issues);
        Assert.Equal((issue.FilePath, issue.Severity, issue.Message, issue.Category, issue.Code,
            issue.Actor, issue.Section, issue.Expected, issue.Actual, issue.RepairHint),
            (frozen.FilePath, frozen.Severity, frozen.Message, frozen.Category, frozen.Code,
                frozen.Actor, frozen.Section, frozen.Expected, frozen.Actual, frozen.RepairHint));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(issue.RepairTargetFiles), frozen.RepairTargetFilesJson);
        var after = window.Capture.ReadExistingStrainReuseCounts();
        Assert.Equal(counts.OrdinaryEvaluations, after.OrdinaryEvaluations);
        Assert.Equal(counts.ActualEvaluations, after.ActualEvaluations);
        Assert.Equal(sourceCounts, window.Capture.ReadExistingPressureGroupComputationCounts());
        Assert.False(window.Capture.IsCurrentOwner);
        AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        AssertPressureNoPrivatePair(window.Copy.Context, window.Lease);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await window.Capture.ReadExistingPressureOrdinaryProbeStateAsync(window.Lease));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await window.Capture.ReadExistingPressureOrdinaryComparisonAsync(window.Lease));
        AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        window.Capture.Dispose();
        Assert.Same(retained, window.Capture.ReadExistingPressureActualRefusalObservation());
    }
}
