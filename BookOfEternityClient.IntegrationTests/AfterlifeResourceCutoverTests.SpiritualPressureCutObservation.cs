using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Composes cut-only currentness/stale-read evidence with separate complete materialization cold/warm queries.
    /// No ordinary query occurs between the real cut and wound insertion; the legacy isolated oracle remains distinct.
    /// </summary>
    [Fact]
    public Task SpiritualPressureGroup_MaterializationCutObservationComposesInvalidationAndLifecycle() =>
        WithPressureTransitionAsync(SpiritualPressureThirdGuardTrigger.OwnApSpent, async window =>
        {
            var baseline = await CapturePressureTransitionBaselineAsync(window, "composed_before_request");
            var beforeCounts = window.Capture.ReadExistingStrainReuseCounts();
            var beforeWork = window.Capture.ReadExistingPressureGroupComputationCounts();
            Assert.Null(window.Capture.ReadPressureCutObservation());
            await window.Capture.RequestPressureCutObservationAsync(window.Lease, window.Admission);
            Assert.Null(window.Capture.ReadPressureCutObservation());
            Assert.Equal(beforeCounts, window.Capture.ReadExistingStrainReuseCounts());
            Assert.Equal(beforeWork, window.Capture.ReadExistingPressureGroupComputationCounts());
            await AssertPressureTransitionInertAsync(window, baseline);

            var epoch = window.Selection.Routing!.RoutingEpoch;
            var materialized = await MeasurePressureOwnerPhaseAsync("composed_cut_materialization", window.Name,
                () => window.Capture.MaterializeWoundAsync(window.Lease, window.Admission,
                    window.Decision, PressureTransitionScene));
            AssertNoConflictFrameErrors(materialized.Issues);
            var wound = Assert.IsType<WoundMaterializationEnvelope>(materialized.Wound);
            Assert.Equal((PressureTransitionWoundId, 2, "II"), (wound.WoundId, wound.Severity.Rank, wound.Severity.Value));
            Assert.Equal(2, wound.Consequences.SlotsUsed);
            Assert.Equal(2, wound.Consequences.OwnedEffectSources.RootBindings.Count);

            // RED must reach this assertion after lawful real materialization, not fail in fixture preparation.
            var result = Assert.IsType<ValidationService.SpiritualPressureCutObservationResult>(
                window.Capture.ReadPressureCutObservation());
            Assert.Equal(ValidationService.SpiritualPressureCutObservationStatus.Success, result.Status);
            Assert.Null(result.Reason);
            var observation = Assert.IsType<ValidationService.SpiritualPressureCutObservation>(result.Observation);
            AssertPressureCutWarmEvidence(window, baseline, observation);
            Assert.Equal((0L, 0L, true),
                (observation.Before.WoundVersion, observation.Before.DependencyCutVersion, observation.Before.MechanicsCurrent));
            Assert.Equal((0L, 1L, false),
                (observation.After.WoundVersion, observation.After.DependencyCutVersion, observation.After.MechanicsCurrent));
            Assert.Equal((1, window.Admission.SourceRevision),
                (observation.Before.NextOrdinal, observation.Before.SourceRevision));
            Assert.Equal((observation.Before.NextOrdinal, observation.Before.SourceRevision),
                (observation.After.NextOrdinal, observation.After.SourceRevision));
            Assert.True(observation.Before.ExactOwnersCurrent);
            Assert.True(observation.After.ExactOwnersCurrent);
            Assert.True(observation.SameOrdinaryContext);
            Assert.True(observation.SameRoutingIdentity);
            Assert.True(observation.StaleReadReturnedNoData);
            Assert.Equal("spiritual_wound_read_stale", observation.StaleReadCode);
            Assert.Equal(1, observation.StaleReadIssueCount);
            Assert.Equal(observation.WarmCounts, observation.AfterCutCounts);
            Assert.Equal(observation.WarmWork, observation.AfterCutWork);

            Assert.Equal((1L, 1L), (window.Admission.Draft.WoundReadVersion, window.Admission.Draft.DependencyCutVersion));
            Assert.False(window.Admission.Draft.HasPendingWoundIntegration);
            Assert.NotSame(epoch, window.Selection.Routing.RoutingEpoch);
            Assert.True(window.Selection.Routing.TryReadCurrentWoundReactionView(out _, out var roots, out _));
            Assert.DoesNotContain(roots, root => root.EffectId == PressureTransitionEffectId);
            foreach (var root in wound.Consequences.OwnedEffectSources.RootBindings)
                Assert.Single(roots, installed => installed.EffectId == root.EffectId);
            var after = await CapturePressureTransitionBaselineAsync(window, "composed_after_materialization");
            Assert.Equal(baseline.State.SourceRevision, after.State.SourceRevision);
            Assert.Equal(baseline.State.Frontier.NextOrdinal, after.State.Frontier.NextOrdinal);
            Assert.Equal(baseline.State.Frontier.ResourceStateJson, after.State.Frontier.ResourceStateJson);
            Assert.Equal(baseline.State.Frontier.ResourceHistoryJson, after.State.Frontier.ResourceHistoryJson);
            Assert.NotEqual(baseline.State.RoutingStateFingerprint, after.State.RoutingStateFingerprint);
            Assert.True(after.State.Frontier.AllocationCursor > baseline.State.Frontier.AllocationCursor);
            AssertExistingPressureInputInert(window.Context, window.Lease, baseline.Files, baseline.Generation);
            AssertPressureCutRetainedBefore(window, after.State.DraftStateJson);

            // This is after the complete transaction, deliberately not an isolated post-cut cold query.
            await AssertPressureTransitionColdWarmAsync(window, after);
            var retryCounts = window.Capture.ReadExistingStrainReuseCounts();
            var retryWork = window.Capture.ReadExistingPressureGroupComputationCounts();
            var retry = await window.Capture.MaterializeWoundAsync(window.Lease, window.Admission,
                window.Decision, PressureTransitionScene);
            Assert.Same(materialized, retry);
            Assert.Same(result, window.Capture.ReadPressureCutObservation());
            var retried = window.Capture.ReadExistingStrainReuseCounts();
            Assert.Equal(retryCounts.ActualEvaluations + 1, retried.ActualEvaluations);
            Assert.Equal((retryCounts.Queries, retryCounts.OrdinaryEvaluations, retryCounts.SignedParentReads),
                (retried.Queries, retried.OrdinaryEvaluations, retried.SignedParentReads));
            Assert.Equal(retryWork, window.Capture.ReadExistingPressureGroupComputationCounts());
            await AssertPressureTransitionInertAsync(window, after);
            var mutableCopy = observation.Cold.ReadOrdinaryExchange();
            mutableCopy["summary"] = "caller-owned copy";
            Assert.True(JsonNode.DeepEquals(window.Ordinary, observation.Cold.ReadOrdinaryExchange()));
            window.Capture.Dispose();
            Assert.Same(result, window.Capture.ReadPressureCutObservation());
            Assert.Equal("spiritual_wound_read_stale", result.Observation!.StaleReadCode);
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await window.Capture.RequestPressureCutObservationAsync(window.Lease, window.Admission));
            AssertExistingPressureInputInert(window.Context, window.Lease, baseline.Files, baseline.Generation);
        }, expectedActualEvaluations: 2, revokesCapture: true);

    /// <summary>Checks actual shared cold computation, zero-work warm reuse and unchanged fresh authority fences.</summary>
    /// <param name="window">Fresh actual owner fixture.</param>
    /// <param name="baseline">Pre-materialization physical and closed-frontier evidence.</param>
    /// <param name="observation">Detached historical report from the actual materialization.</param>
    private static void AssertPressureCutWarmEvidence(PressureTransitionWindow window, PressureTransitionBaseline baseline,
        ValidationService.SpiritualPressureCutObservation observation)
    {
        AssertPressureTransitionComparison(window, baseline, new(observation.Cold, []));
        AssertPressureTransitionComparison(window, baseline, new(observation.Warm, []));
        var before = observation.InitialCounts;
        var cold = observation.ColdCounts;
        var warm = observation.WarmCounts;
        Assert.Equal(before.Queries + 1, cold.Queries);
        Assert.Equal(before.OrdinaryEvaluations + 1, cold.OrdinaryEvaluations);
        Assert.Equal(before.SignedParentReads + 2, cold.SignedParentReads);
        Assert.Equal(before.ActualEvaluations, cold.ActualEvaluations);
        Assert.True(cold.FreshInputChecks > before.FreshInputChecks);
        Assert.True(cold.FreshDependencyChecks > before.FreshDependencyChecks);
        Assert.Equal(PressureTransitionNextBatch(observation.InitialWork.CurrentBatch), observation.ColdWork.CurrentBatch);
        Assert.Equal(PressureTransitionNextBatch(observation.InitialWork.SignedBatch), observation.ColdWork.SignedBatch);
        Assert.Equal(observation.InitialWork.ResourceBatchComputations + 1, observation.ColdWork.ResourceBatchComputations);
        Assert.Equal(cold.Queries + 1, warm.Queries);
        Assert.Equal(cold.OrdinaryEvaluations, warm.OrdinaryEvaluations);
        Assert.Equal(cold.SignedParentReads, warm.SignedParentReads);
        Assert.Equal(cold.ActualEvaluations, warm.ActualEvaluations);
        Assert.True(warm.FreshInputChecks > cold.FreshInputChecks);
        Assert.True(warm.FreshDependencyChecks > cold.FreshDependencyChecks);
        Assert.Equal(observation.ColdWork, observation.WarmWork);
    }

    /// <summary>Reads the existing post-commit detached diagnostic image, never a live before authority or an export inside speculation.</summary>
    /// <param name="window">Fresh actual owner fixture.</param>
    /// <param name="draftJson">The existing closed diagnostic export collected only after commit.</param>
    private static void AssertPressureCutRetainedBefore(PressureTransitionWindow window, string draftJson)
    {
        var draft = JsonNode.Parse(draftJson)!;
        var before = Assert.Single(draft["BeforeStates"]!.AsArray())!;
        Assert.Equal((0L, 1L, true), (before["Version"]!.GetValue<long>(), before["CutVersion"]!.GetValue<long>(),
            before["PreparationAttempted"]!.GetValue<bool>()));
        Assert.False(before["Preparing"]!.GetValue<bool>());
        var data = before["Data"]!;
        var player = Assert.Single(data["WoundCarriers"]!["AfterlifeProfiles"]!["profiles"]!.AsArray(),
            row => row!["actorType"]!.GetValue<string>() == "player_soul")!;
        Assert.Equal(3, player["activeWounds"]!.AsArray().Count);
        foreach (var parent in window.Data.Parents)
            Assert.True(JsonNode.DeepEquals(parent, Assert.Single(player["activeWounds"]!.AsArray(),
                row => row!["woundId"]!.GetValue<string>() == parent["woundId"]!.GetValue<string>())));
        Assert.True(JsonNode.DeepEquals(window.Data.WoundIdentities, data["WoundIdentity"]));
        Assert.True(JsonNode.DeepEquals(window.Data.History, data["WoundHistory"]));
        var identity = Assert.Single(data["EffectIdentity"]!["entries"]!.AsArray(),
            row => row!["effectId"]!.GetValue<string>() == PressureTransitionEffectId)!;
        var transitions = identity["transitions"]!.AsArray();
        Assert.Equal(new[] { "create", "trigger" }, transitions.Select(row => row!["kind"]!.GetValue<string>()));
        Assert.Equal(new[] { 43, 46 }, transitions.Select(row => row!["turn"]!.GetValue<int>()));
        var activation = Assert.Single(window.Interval.EffectAfter.AcceptedActivations,
            row => row.Activation.Stamp.Identity.EffectId == PressureTransitionEffectId);
        Assert.Equal(("own_ap_spent", "resource_spent", false),
            (activation.Activation.Stamp.Identity.TriggerId, activation.Activation.Stamp.Identity.EventKind,
                activation.Activation.Stamp.ConsumesUse));
        Assert.Equal(activation.Activation.Stamp.Identity.EventRef, transitions[1]!["eventRef"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(window.Data.EffectIdentities["entries"]![2]!["transitions"]![0], transitions[0]));
        var insertion = Assert.Single(draft["Insertions"]!.AsArray())!;
        Assert.Equal(1L, insertion["VersionAfter"]!.GetValue<long>());
        var current = draft["CurrentWoundState"]!;
        var history = WoundHistoryState.Parse(current["WoundHistory"]!.ToJsonString(), WoundHistoryState.HistoryPath);
        var identities = WoundIdentityState.Parse(current["WoundIdentity"]!.ToJsonString(), WoundIdentityState.StatePath);
        AssertNoConflictFrameErrors(history.Issues);
        AssertNoConflictFrameErrors(identities.Issues);
        var targetHistory = history.State!.ResolveWoundTransitions(PressureTransitionWoundId);
        Assert.Equal(new[] { "create", "worsen" }, targetHistory.Select(row => row.Kind));
        Assert.Equal(new[] { 1, 2 }, targetHistory.Select(row => row.WoundTransitionOrdinal));
        Assert.Equal(46, targetHistory[^1].Turn);
        Assert.True(identities.State!.TryGetEntry(PressureTransitionWoundId, out var targetIdentity));
        Assert.Equal(targetHistory[^1].AfterFingerprint, targetIdentity.SemanticFingerprint);
        Assert.Equal(2, targetIdentity.LastTransitionOrdinal);
        Assert.NotEmpty(draft["TriggerJournal"]!.AsArray());
    }
}
