using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Requires one cold pair computation, fresh warm hits and detached mutable ordinary DATA.</summary>
    [Fact]
    public Task SpiritualPressureGroup_PairColdWarmDetachedComparison() =>
        WithPressureGroupCaseAsync(new("group_pair_reuse"), async window =>
        {
            var owner = window.Capture;
            var before = owner.ReadExistingStrainReuseCounts();
            var sourceBefore = owner.ReadExistingPressureGroupComputationCounts();
            var cold = await MeasurePressureOwnerPhaseAsync("group_cold", window.Spec.Name,
                () => ReadPressureGroupComparisonAsync(window));
            AssertPressureGroupComparison(window, cold);
            var afterCold = owner.ReadExistingStrainReuseCounts();
            Assert.Equal(before.Queries + 1, afterCold.Queries);
            Assert.Equal(before.OrdinaryEvaluations + 1, afterCold.OrdinaryEvaluations);
            Assert.Equal(before.SignedParentReads + 2, afterCold.SignedParentReads);
            var sourceCold = owner.ReadExistingPressureGroupComputationCounts();
            Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(
                sourceBefore.CurrentBatch.CatalogBuildAttempts + 1, sourceBefore.CurrentBatch.IdentityParseAttempts + 1,
                sourceBefore.CurrentBatch.HistoryParseAttempts + 1, sourceBefore.CurrentBatch.HistoryAgreementAttempts + 1,
                sourceBefore.CurrentBatch.ParentResolutionAttempts + 2), sourceCold.CurrentBatch);
            Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(
                sourceBefore.SignedBatch.CatalogBuildAttempts + 1, sourceBefore.SignedBatch.IdentityParseAttempts + 1,
                sourceBefore.SignedBatch.HistoryParseAttempts + 1, sourceBefore.SignedBatch.HistoryAgreementAttempts + 1,
                sourceBefore.SignedBatch.ParentResolutionAttempts + 2), sourceCold.SignedBatch);
            Assert.Equal(sourceBefore.ResourceBatchComputations + 1, sourceCold.ResourceBatchComputations);
            Assert.Equal(before.ActualEvaluations, afterCold.ActualEvaluations);
            Assert.True(afterCold.FreshInputChecks > before.FreshInputChecks);
            Assert.True(afterCold.FreshDependencyChecks > before.FreshDependencyChecks);
            await AssertPressureGroupWindowInertAsync(window);
            var warm = await MeasurePressureOwnerPhaseAsync("group_warm", window.Spec.Name,
                () => ReadPressureGroupComparisonAsync(window));
            AssertPressureGroupComparison(window, warm);
            var afterWarm = owner.ReadExistingStrainReuseCounts();
            Assert.Equal(sourceCold, owner.ReadExistingPressureGroupComputationCounts());
            Assert.Equal(afterCold.Queries + 1, afterWarm.Queries);
            Assert.Equal(afterCold.OrdinaryEvaluations, afterWarm.OrdinaryEvaluations);
            Assert.Equal(afterCold.SignedParentReads, afterWarm.SignedParentReads);
            Assert.Equal(afterCold.ActualEvaluations, afterWarm.ActualEvaluations);
            Assert.True(afterWarm.FreshInputChecks > afterCold.FreshInputChecks);
            Assert.True(afterWarm.FreshDependencyChecks > afterCold.FreshDependencyChecks);
            cold.Comparison!.ReadOrdinaryExchange()["after"]!["playerSideStrain"] = "broken";
            var detached = await ReadPressureGroupComparisonAsync(window);
            AssertPressureGroupComparison(window, detached);
            var afterDetached = owner.ReadExistingStrainReuseCounts();
            Assert.Equal(sourceCold, owner.ReadExistingPressureGroupComputationCounts());
            Assert.Equal(afterWarm.Queries + 1, afterDetached.Queries);
            Assert.Equal(afterWarm.OrdinaryEvaluations, afterDetached.OrdinaryEvaluations);
            Assert.Equal(afterWarm.SignedParentReads, afterDetached.SignedParentReads);
            Assert.Equal(afterWarm.ActualEvaluations, afterDetached.ActualEvaluations);
            Assert.True(afterDetached.FreshInputChecks > afterWarm.FreshInputChecks);
            Assert.True(afterDetached.FreshDependencyChecks > afterWarm.FreshDependencyChecks);
            await AssertPressureGroupWindowInertAsync(window);
            owner.Dispose();
            AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        });

    /// <summary>Requires ordinary III DATA without granting hypothetical IV actual projection.</summary>
    [Fact]
    public Task SpiritualPressureGroup_PairOrdinaryThreeComparison() =>
        WithPressureGroupCaseAsync(new("group_pair_ordinary_three", After: "overwhelmed"), async window =>
        {
            var result = await ReadPressureGroupComparisonAsync(window);
            AssertPressureGroupComparison(window, result);
            Assert.Equal((18L, 3), (result.Comparison!.OrdinaryCalculation.TraumaPressure,
                result.Comparison.OrdinaryCalculation.MaximumSeverityRank));
            Assert.Equal(0, window.Capture.ReadExistingStrainReuseCounts().ActualEvaluations);
        });

    /// <summary>Preserves selected-parent and unrelated cost-only pressure burdens while isolating only strain rows.</summary>
    [Fact]
    public Task SpiritualPressureGroup_ApplicableCostsIncluded() =>
        WithPressureGroupCaseAsync(new("group_applicable_costs", PlayerBefore: 6, PlayerCost: 6,
            SelectedCost: true, OtherCost: true), async window =>
        {
            var result = await ReadPressureGroupComparisonAsync(window);
            AssertPressureGroupComparison(window, result);
            Assert.Equal((13L, 2), (result.Comparison!.OrdinaryCalculation.TraumaPressure,
                result.Comparison.OrdinaryCalculation.MaximumSeverityRank));
            var player = Assert.Single(result.Comparison.ExpectedTransitions,
                row => row.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor);
            Assert.Equal((6m, 0m, 6m), (player.Before, player.After, player.Amount));
        });

    /// <summary>A shape-valid base3 audit reaches the real cost consumer and cannot suppress the extra pressure costs.</summary>
    [Fact]
    public Task SpiritualPressureGroup_WrongApplicableCostsRefused() =>
        WithPressureGroupCaseAsync(new("group_wrong_costs", PlayerBefore: 6, PlayerCost: 3,
            SelectedCost: true, OtherCost: true), async window =>
        {
            var before = window.Capture.ReadExistingStrainReuseCounts();
            var result = await ReadPressureGroupComparisonAsync(window);
            Assert.Null(result.Comparison); Assert.False(result.PrefixBlocked);
            var issue = Assert.Single(result.Issues);
            Assert.Equal("afterlife_conflict_action_cost_mismatch", issue.Code);
            Assert.Equal("activeConflict.exchangeLog[0].actionCostAudit.player.effectiveCost", issue.FilePath);
            Assert.Equal("baseCost=3, minCost=1, effectiveCost=6", issue.Expected);
            Assert.Equal("baseCost=3, minCost=1, effectiveCost=3", issue.Actual);
            Assert.Equal(IssueSeverity.Error, issue.Severity);
            var after = window.Capture.ReadExistingStrainReuseCounts();
            Assert.Equal(before.Queries + 1, after.Queries);
            Assert.Equal(before.OrdinaryEvaluations + 1, after.OrdinaryEvaluations);
            Assert.Equal(before.ActualEvaluations, after.ActualEvaluations);
            Assert.True(window.Capture.IsCurrentOwner);
        });

    /// <summary>Missing applicable roll evidence fails twice in the real evaluator, never a cached failure.</summary>
    [Fact]
    public Task SpiritualPressureGroup_MissingPressureRollAuditReevaluates() =>
        AssertPressureGroupMissingRollReevaluatesAsync(2);

    /// <summary>Checks failure nonreuse for one complete independently prepared parent list.</summary>
    /// <param name="parents">Retained applicable parent count, not a caller-authored production scalar.</param>
    private Task AssertPressureGroupMissingRollReevaluatesAsync(int parents) =>
        WithPressureGroupCaseAsync(new("group_missing_pressure_roll_" + parents, Parents: parents, PressureRoll: true), async window =>
        {
            Assert.Null(window.Copy.Ordinary["diceAudit"]?["rollMode"]?["player"]);
            var before = window.Capture.ReadExistingStrainReuseCounts();
            string? firstIssue = null;
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                var result = await ReadPressureGroupComparisonAsync(window);
                Assert.Null(result.Comparison); Assert.False(result.PrefixBlocked);
                var issue = Assert.Single(result.Issues);
                Assert.Equal("spiritual_wound_roll_hindrance_missing", issue.Code);
                Assert.Equal("activeConflict.exchangeLog[0].diceAudit.rollMode.player", issue.FilePath);
                Assert.Equal(IssueSeverity.Error, issue.Severity);
                var fingerprint = string.Join("|", issue.Code, issue.FilePath, issue.Severity, issue.Expected, issue.Actual);
                if (firstIssue is null) firstIssue = fingerprint; else Assert.Equal(firstIssue, fingerprint);
                var after = window.Capture.ReadExistingStrainReuseCounts();
                Assert.Equal(before.Queries + attempt, after.Queries);
                Assert.Equal(before.OrdinaryEvaluations + attempt, after.OrdinaryEvaluations);
                Assert.Equal(before.ActualEvaluations, after.ActualEvaluations);
                Assert.True(after.FreshInputChecks > before.FreshInputChecks);
                Assert.True(after.FreshDependencyChecks > before.FreshDependencyChecks);
                Assert.True(window.Capture.IsCurrentOwner);
                await AssertPressureGroupWindowInertAsync(window);
            }
        });

    /// <summary>Valid ordinary maximum0 DATA still permits the existing positive singleton actual lane.</summary>
    [Fact]
    public Task SpiritualPressureGroup_SingletonOrdinaryMaximumZeroKeepsActual() =>
        WithPressureGroupCaseAsync(new("group_singleton_max_zero", Parents: 1, After: "strained", PlayerDie: 6,
            AdvanceExpected: true), async window =>
        {
            var comparison = await ReadPressureGroupComparisonAsync(window);
            AssertPressureGroupComparison(window, comparison);
            Assert.Equal((6L, 0), (comparison.Comparison!.OrdinaryCalculation.TraumaPressure,
                comparison.Comparison.OrdinaryCalculation.MaximumSeverityRank));
            var hypothetical = SpiritualWoundOpportunityMath.Calculate(new(6, 0, 0, "clear", "fractured", "hostile", 4))!;
            Assert.Equal((11L, 1), (hypothetical.TraumaPressure, hypothetical.MaximumSeverityRank));
            await AssertPressureGroupWindowInertAsync(window);
            var counts = window.Capture.ReadExistingStrainReuseCounts();
            Assert.Null(window.Capture.ReadExistingPressureActualRefusalObservation());
            var advanced = await window.Capture.AdvanceNextResourceExchangeAsync(window.Lease);
            Assert.Null(window.Capture.ReadExistingPressureActualRefusalObservation());
            AssertNoConflictFrameErrors(advanced.Issues);
            var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step?.Interval);
            Assert.Equal(2, interval.AppliedTransitions.Count);
            Assert.Empty(interval.ReplayTransitions);
            foreach (var kind in new[] { ResourceOwnerKind.AfterlifeActor, ResourceOwnerKind.AfterlifeConflictSide })
            {
                var spend = Assert.Single(interval.AppliedTransitions, row => row.Coordinate.OwnerKind == kind);
                Assert.Equal(ResourceTransitionOperation.Spend, spend.Operation);
                Assert.Equal((3m, 3m, 3m, 0m),
                    (spend.RequestedAmount, spend.AppliedAmount, spend.BeforeState!.Current, spend.AfterState!.Current));
            }
            var evidence = Assert.Single(window.Capture.ReadClosedExchangeEvidence(window.Lease));
            var actual = Assert.Single(evidence.Sources);
            Assert.Equal(hypothetical, actual.Calculation);
            Assert.Equal((11L, 1), (actual.Calculation.TraumaPressure, actual.Calculation.MaximumSeverityRank));
            var expected = window.Copy.Ordinary.DeepClone().AsObject();
            expected["after"]!["playerSideStrain"] = "fractured";
            Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(actual.ExchangeJson)));
            Assert.Equal(new[] { 6, 12 }, evidence.DiceClaims.Select(row => row.Value));
            var after = window.Capture.ReadExistingStrainReuseCounts();
            Assert.Equal(counts.OrdinaryEvaluations, after.OrdinaryEvaluations);
            Assert.Equal(counts.ActualEvaluations + 1, after.ActualEvaluations);
            AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
            window.Capture.Dispose();
            AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        });

    /// <summary>Requires legitimate pair ordinary maximum0 DATA without any group actual advance.</summary>
    [Fact]
    public Task SpiritualPressureGroup_PairOrdinaryMaximumZeroComparison() =>
        WithPressureGroupCaseAsync(new("group_pair_max_zero", After: "strained", PlayerDie: 6), async window =>
        {
            var result = await ReadPressureGroupComparisonAsync(window);
            AssertPressureGroupComparison(window, result);
            Assert.Equal((6L, 0), (result.Comparison!.OrdinaryCalculation.TraumaPressure,
                result.Comparison.OrdinaryCalculation.MaximumSeverityRank));
            Assert.Equal(0, window.Capture.ReadExistingStrainReuseCounts().ActualEvaluations);
        });

    /// <summary>A lawful III+IV pair reaches the real whole-group ordinary owner and retains the literal boundary result.</summary>
    [Fact]
    public Task SpiritualPressureGroup_ThreeFourBoundaryOrdinaryComparison() =>
        WithPressureGroupCaseAsync(new("group_three_four_boundary", Ranks: new(3, 4)), async window =>
        {
            var before = window.Capture.ReadExistingStrainReuseCounts();
            var result = await ReadPressureGroupComparisonAsync(window);
            if (result.Comparison is null)
            {
                Assert.False(result.PrefixBlocked);
                var issue = Assert.Single(result.Issues);
                Assert.Equal(IssueSeverity.Error, issue.Severity);
                Assert.Equal(("spiritual_pressure_ordinary_contour_unsupported", AfterlifeSpiritualConflictState.StatePath,
                    "one complete signed current rank-III/IV parent or a complete rank-III/IV pressure strain group"),
                    (issue.Code, issue.FilePath, issue.Actual));
                var refused = window.Capture.ReadExistingStrainReuseCounts();
                Assert.Equal((before.OrdinaryEvaluations, before.ActualEvaluations),
                    (refused.OrdinaryEvaluations, refused.ActualEvaluations));
                Assert.Equal(before.Queries + 1, refused.Queries);
                Assert.True(window.Capture.IsCurrentOwner);
            }
            // Unconditional desired success: the known current rank guard is evidence, never the expected result.
            Assert.NotNull(result.Comparison);
            AssertPressureGroupComparison(window, result);
            Assert.Equal((13L, 2), (result.Comparison!.OrdinaryCalculation.TraumaPressure,
                result.Comparison.OrdinaryCalculation.MaximumSeverityRank));
            Assert.Equal(new[] { 3, 4 }, window.ExpectedParentJson.Select(parent =>
                JsonNode.Parse(parent)!["severity"]!["rank"]!.GetValue<int>()));
            Assert.Equal((1, 0), (window.Capture.ReadExistingStrainReuseCounts().OrdinaryEvaluations,
                window.Capture.ReadExistingStrainReuseCounts().ActualEvaluations));
        });

    /// <summary>Three complete III parents now advance the existing ordinary-II contour as one v6 group, without selecting a subset.</summary>
    [Fact]
    public Task SpiritualPressureGroup_ThreeStrainParentsAdvanceCompleteSet() =>
        WithPressureGroupCaseAsync(new("group_third_parent", Parents: 3, AdvanceExpected: true), window =>
            AssertPressureOwnedGroupActualAdmissionAsync(window, ExpectedPressureGroupOrdinary(window.Spec),
                "The formerly excluded complete N3 group must retain all parents through actual/C1.", multiParent: true));

    /// <summary>Requires a supported-set refusal before any ordinary or actual evaluator entry.</summary>
    private async Task AssertPressureGroupSelectionRefusalAsync(PressureGroupWindow window)
    {
        var before = window.Capture.ReadExistingStrainReuseCounts();
        var result = await ReadPressureGroupComparisonAsync(window);
        Assert.Null(result.Comparison); Assert.False(result.PrefixBlocked);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("spiritual_pressure_ordinary_contour_unsupported", issue.Code);
        Assert.Equal(AfterlifeSpiritualConflictState.StatePath, issue.FilePath);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        var after = window.Capture.ReadExistingStrainReuseCounts();
        Assert.Equal(before.Queries + 1, after.Queries);
        Assert.Equal(before.OrdinaryEvaluations, after.OrdinaryEvaluations);
        Assert.Equal(before.ActualEvaluations, after.ActualEvaluations);
        Assert.True(window.Capture.IsCurrentOwner);
        await AssertPressureGroupWindowInertAsync(window);
    }
}
