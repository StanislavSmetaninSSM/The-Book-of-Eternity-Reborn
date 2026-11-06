using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Normal I→II insertion proves lifecycle nonreuse, not an isolated wound-version predicate.</summary>
    [Fact]
    public Task SpiritualPressureGroup_WarmRoutingWoundTransitionReevaluates() =>
        WithPressureTransitionAsync(SpiritualPressureThirdGuardTrigger.TurnEnd, async window =>
        {
            var baseline = await CapturePressureTransitionBaselineAsync(window, "before_warm");
            Assert.Equal((0L, 0L), (baseline.State.WoundVersion, baseline.State.DependencyCutVersion));
            var epoch = window.Selection.Routing!.RoutingEpoch;
            await AssertPressureTransitionColdWarmAsync(window, baseline);

            // Normal materialization revalidates continuation inputs and invalidates ordinary DATA
            // before insertion. This is deliberately a whole lifecycle control, not a predicate mutation test.
            var changed = await MeasurePressureOwnerPhaseAsync("transition_materialize", window.Name,
                () => window.Capture.MaterializeWoundAsync(window.Lease, window.Admission,
                    window.Decision, PressureTransitionScene));
            AssertNoConflictFrameErrors(changed.Issues);
            var wound = Assert.IsType<WoundMaterializationEnvelope>(changed.Wound);
            Assert.Equal((PressureTransitionWoundId, 2, "II"), (wound.WoundId, wound.Severity.Rank, wound.Severity.Value));
            Assert.Equal(2, wound.Consequences.SlotsUsed);
            Assert.Equal(2, wound.Consequences.OwnedEffectSources.RootBindings.Count);
            Assert.Equal((1L, 0L), (window.Admission.Draft.WoundReadVersion, window.Admission.Draft.DependencyCutVersion));
            Assert.False(window.Admission.Draft.HasPendingWoundIntegration);
            Assert.NotSame(epoch, window.Selection.Routing.RoutingEpoch);
            Assert.True(window.Selection.Routing.TryReadCurrentWoundReactionView(out _, out var installedRoots, out _));
            Assert.DoesNotContain(installedRoots, row => row.EffectId == PressureTransitionEffectId);
            foreach (var root in wound.Consequences.OwnedEffectSources.RootBindings)
                Assert.Single(installedRoots, row => row.EffectId == root.EffectId);
            var after = await CapturePressureTransitionBaselineAsync(window, "after_materialize");
            Assert.Equal((1L, 0L), (after.State.WoundVersion, after.State.DependencyCutVersion));
            Assert.NotEqual(baseline.State.RoutingStateFingerprint, after.State.RoutingStateFingerprint);
            Assert.Equal(baseline.State.Frontier.NextOrdinal, after.State.Frontier.NextOrdinal);
            Assert.Equal(baseline.State.SourceRevision, after.State.SourceRevision);
            AssertPressureTransitionClosedFrontier(window, after.State);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await window.Capture.ReadExistingPressureOrdinaryProbeStateAsync(window.Lease));
            await AssertPressureTransitionInertAsync(window, after);
            await AssertPressureTransitionColdWarmAsync(window, after);
        });

    /// <summary>A preselected real player-AP activation cut changes only cut identity before the next query.</summary>
    [Fact]
    public Task SpiritualPressureGroup_WarmDependencyCutTransitionReevaluates() =>
        WithPressureTransitionAsync(SpiritualPressureThirdGuardTrigger.OwnApSpent, async window =>
        {
            var baseline = await CapturePressureTransitionBaselineAsync(window, "before_warm");
            Assert.Equal((0L, 0L), (baseline.State.WoundVersion, baseline.State.DependencyCutVersion));
            var epoch = window.Selection.Routing!.RoutingEpoch;
            var phases = window.Admission.Draft.Phases.ToArray();
            var activation = Assert.Single(window.Interval.EffectAfter.AcceptedActivations,
                row => row.Activation.Stamp.Identity.EffectId == PressureTransitionEffectId);
            Assert.Equal(("own_ap_spent", "resource_spent"),
                (activation.Activation.Stamp.Identity.TriggerId, activation.Activation.Stamp.Identity.EventKind));
            Assert.False(activation.Activation.Stamp.ConsumesUse);
            Assert.Null(activation.Activation.Stamp.UsesBefore);
            Assert.Null(activation.Activation.UsesAfter);
            await AssertPressureTransitionColdWarmAsync(window, baseline);

            // Nothing that validates a source, reads an opportunity or prepares materialization
            // may intervene between the warm comparison above and this actual draft-owned cut.
            var started = Stopwatch.GetTimestamp();
            var issues = new List<ValidationIssue>();
            var before = window.Admission.Draft.AdvanceForWoundInsertion(window.Selection, issues);
            WritePressureOwnerTiming("transition_dependency_cut", window.Name, started);
            AssertNoConflictFrameErrors(issues);
            Assert.NotNull(before);
            Assert.True(before.IsCurrent);
            Assert.Equal((0L, 1L), (window.Admission.Draft.WoundReadVersion, window.Admission.Draft.DependencyCutVersion));
            Assert.False(window.Admission.Draft.HasPendingWoundIntegration);
            Assert.Same(epoch, window.Selection.Routing.RoutingEpoch);
            Assert.Equal(phases, window.Admission.Draft.Phases);
            var data = before.Data;
            var profiles = Assert.IsType<JsonObject>(data.WoundCarriers!.AfterlifeProfiles);
            var player = Assert.Single(profiles["profiles"]!.AsArray(), row => row!["actorType"]!.GetValue<string>() == "player_soul")!;
            Assert.Equal(3, player["activeWounds"]!.AsArray().Count);
            foreach (var parent in window.Data.Parents)
                Assert.True(JsonNode.DeepEquals(parent, Assert.Single(player["activeWounds"]!.AsArray(), row =>
                    row!["woundId"]!.GetValue<string>() == parent["woundId"]!.GetValue<string>())));
            var identity = Assert.Single(data.EffectIdentity!["entries"]!.AsArray(), row =>
                row!["effectId"]!.GetValue<string>() == PressureTransitionEffectId)!;
            var transitions = identity["transitions"]!.AsArray();
            Assert.Equal(new[] { "create", "trigger" }, transitions.Select(row => row!["kind"]!.GetValue<string>()));
            Assert.Equal(new[] { 43, 46 }, transitions.Select(row => row!["turn"]!.GetValue<int>()));
            Assert.Equal(activation.Activation.Stamp.Identity.EventRef, transitions[1]!["eventRef"]!.GetValue<string>());
            Assert.Equal(window.Data.EffectIdentities["entries"]![2]!["transitions"]![0]!.ToJsonString(), transitions[0]!.ToJsonString());
            var effects = EffectCarrierCatalog.Build(data.EffectCarriers!);
            Assert.Empty(effects.Issues);
            Assert.True(effects.TryResolveOne(PressureTransitionEffectId, out var effect));
            Assert.Equal(transitions[1]!["transitionId"]!.GetValue<string>(),
                effect.Effect["chronology"]!["lastTransitionId"]!.GetValue<string>());
            Assert.Equal(46, effect.Effect["chronology"]!["lastTransitionTurn"]!.GetValue<int>());
            var after = await CapturePressureTransitionBaselineAsync(window, "after_cut");
            Assert.Equal((0L, 1L), (after.State.WoundVersion, after.State.DependencyCutVersion));
            Assert.Equal(baseline.State.SourceRevision, after.State.SourceRevision);
            Assert.Equal(baseline.State.Frontier.NextOrdinal, after.State.Frontier.NextOrdinal);
            Assert.Equal(baseline.State.RoutingStateFingerprint, after.State.RoutingStateFingerprint);
            Assert.Equal(baseline.State.Frontier.ResourceStateJson, after.State.Frontier.ResourceStateJson);
            Assert.Equal(baseline.State.Frontier.ResourceHistoryJson, after.State.Frontier.ResourceHistoryJson);
            var expectedSource = JsonNode.Parse(baseline.State.Frontier.SourceStateJson)!;
            expectedSource["dependencyCutVersion"] = 1L;
            Assert.True(JsonNode.DeepEquals(expectedSource, JsonNode.Parse(after.State.Frontier.SourceStateJson)));

            // The exact already registered before-authority is reused with no second cut/allocation.
            var retry = window.Admission.Draft.AdvanceForWoundInsertion(window.Selection, issues);
            AssertNoConflictFrameErrors(issues);
            Assert.Same(before, retry);
            Assert.True(retry!.IsCurrent);
            Assert.Equal(1L, window.Admission.Draft.DependencyCutVersion);
            Assert.Same(epoch, window.Selection.Routing.RoutingEpoch);
            await AssertPressureTransitionInertAsync(window, after);
            await AssertPressureTransitionColdWarmAsync(window, after);
        });

    /// <summary>Requires complete ordinary E1 DATA and genuine cold work, then a fresh-fenced zero-work warm hit.</summary>
    private async Task AssertPressureTransitionColdWarmAsync(PressureTransitionWindow window, PressureTransitionBaseline baseline)
    {
        var before = window.Capture.ReadExistingStrainReuseCounts();
        var sourceBefore = window.Capture.ReadExistingPressureGroupComputationCounts();
        try
        {
            var cold = await MeasurePressureOwnerPhaseAsync("transition_cold_query", window.Name,
                () => window.Capture.ReadExistingPressureOrdinaryComparisonAsync(window.Lease));
            AssertPressureTransitionComparison(window, baseline, cold);
        }
        finally { await AssertPressureTransitionInertAsync(window, baseline); }
        var coldCounts = window.Capture.ReadExistingStrainReuseCounts();
        var sourceCold = window.Capture.ReadExistingPressureGroupComputationCounts();
        Assert.Equal(before.Queries + 1, coldCounts.Queries);
        Assert.Equal(before.OrdinaryEvaluations + 1, coldCounts.OrdinaryEvaluations);
        Assert.Equal(before.SignedParentReads + 2, coldCounts.SignedParentReads);
        Assert.Equal(before.ActualEvaluations, coldCounts.ActualEvaluations);
        Assert.True(coldCounts.FreshInputChecks > before.FreshInputChecks);
        Assert.True(coldCounts.FreshDependencyChecks > before.FreshDependencyChecks);
        Assert.Equal(PressureTransitionNextBatch(sourceBefore.CurrentBatch), sourceCold.CurrentBatch);
        Assert.Equal(PressureTransitionNextBatch(sourceBefore.SignedBatch), sourceCold.SignedBatch);
        Assert.Equal(sourceBefore.ResourceBatchComputations + 1, sourceCold.ResourceBatchComputations);
        try
        {
            var warm = await MeasurePressureOwnerPhaseAsync("transition_warm_query", window.Name,
                () => window.Capture.ReadExistingPressureOrdinaryComparisonAsync(window.Lease));
            AssertPressureTransitionComparison(window, baseline, warm);
        }
        finally { await AssertPressureTransitionInertAsync(window, baseline); }
        var warmCounts = window.Capture.ReadExistingStrainReuseCounts();
        Assert.Equal(coldCounts.Queries + 1, warmCounts.Queries);
        Assert.Equal(coldCounts.OrdinaryEvaluations, warmCounts.OrdinaryEvaluations);
        Assert.Equal(coldCounts.SignedParentReads, warmCounts.SignedParentReads);
        Assert.Equal(coldCounts.ActualEvaluations, warmCounts.ActualEvaluations);
        Assert.True(warmCounts.FreshInputChecks > coldCounts.FreshInputChecks);
        Assert.True(warmCounts.FreshDependencyChecks > coldCounts.FreshDependencyChecks);
        Assert.Equal(sourceCold, window.Capture.ReadExistingPressureGroupComputationCounts());
    }

    private static ValidationService.SpiritualPressureBatchReaderCounts PressureTransitionNextBatch(
        ValidationService.SpiritualPressureBatchReaderCounts before) => new(before.CatalogBuildAttempts + 1,
        before.IdentityParseAttempts + 1, before.HistoryParseAttempts + 1, before.HistoryAgreementAttempts + 1,
        before.ParentResolutionAttempts + 2);

    /// <summary>Literal prospective harm and two unpaid AP3→0 rows; E1 is never advanced or admitted.</summary>
    private static void AssertPressureTransitionComparison(PressureTransitionWindow window, PressureTransitionBaseline baseline,
        ValidationService.SpiritualOrdinaryPressureComparisonResult result)
    {
        AssertNoConflictFrameErrors(result.Issues);
        Assert.False(result.PrefixBlocked);
        var comparison = Assert.IsType<ValidationService.SpiritualOrdinaryPressureComparisonData>(result.Comparison);
        Assert.Equal("/activeConflict/exchangeLog/1", comparison.ExchangePointer);
        Assert.True(JsonNode.DeepEquals(window.Ordinary, comparison.ReadOrdinaryExchange()));
        Assert.True(JsonNode.DeepEquals(window.Ordinary, JsonNode.Parse(comparison.OrdinaryExchangeJson)));
        var raw = baseline.Files.Single(row => row.Key.Replace('\\', '/') == AfterlifeSpiritualConflictState.StatePath).Value;
        Assert.Equal(new CanonicalBeforeImage(true, raw).Fingerprint, comparison.RawInputFingerprint);
        var expected = SpiritualWoundOpportunityMath.Calculate(new(12, 0, 0, "fractured", "overwhelmed", "hostile", 4));
        Assert.NotNull(expected);
        Assert.Equal((2, 3, 0, 16L, 2), (expected.PreviousStrainRank, expected.NewStrainRank,
            expected.ExtraJumpSteps, expected.TraumaPressure, expected.MaximumSeverityRank));
        Assert.Equal(expected, comparison.OrdinaryCalculation);
        Assert.Equal(2, comparison.ExpectedTransitions.Count);
        foreach (var side in new[] { ResourceOwnerKind.AfterlifeActor, ResourceOwnerKind.AfterlifeConflictSide })
        {
            var row = Assert.Single(comparison.ExpectedTransitions, value => value.Coordinate.OwnerKind == side);
            var id = side == ResourceOwnerKind.AfterlifeActor ? "player_soul" : window.Conflict["activeConflict"]!
                [AfterlifeEntityProfileState.ResourceOwnerBindingsProperty]!["opposition"]!["resourceOwnerId"]!.GetValue<string>();
            Assert.Equal(new ResourceCoordinate("chaos_sea", side, id, "spiritual_action_points"), row.Coordinate);
            Assert.Equal(ResourceTransitionOperation.Spend, row.Operation);
            Assert.Equal((3m, 0m, 3m, 0m), (row.Before, row.After, row.Amount, row.RecoveryPayment));
            Assert.Equal(PressureTransitionOrdinaryId, row.SourceId);
        }
    }
}
