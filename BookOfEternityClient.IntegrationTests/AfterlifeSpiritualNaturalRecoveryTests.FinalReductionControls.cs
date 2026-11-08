using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeSpiritualNaturalRecoveryTests
{
    [Fact]
    public Task NaturalFinalReductionRefusalsRetireWarmDataAndClosedOwnersComputeFresh() =>
        WithNaturalFinalReductionControlAsync("refusals", null, (fixture, owner, bundle, window, unchanged) =>
        {
            var prepared = bundle.PreparedPlan;
            var effects = bundle.EffectBatchPlan;
            var expected = bundle.FinalPlan;
            var originalEffects = new WoundEffectBatchPlanningResult(effects, Array.Empty<ValidationIssue>());
            long Reductions() => window.CaptureNamedCounts("SpiritualFinal.Reduce").Completed;
            WoundAcceptedTurnPlanningResult Replay() => WoundAcceptedTurnPlanner.Finalize(prepared, originalEffects);
            void AssertUnchanged()
            {
                fixture.AssertMutationRootsUnchanged(unchanged);
                fixture.AssertResourcesUnchanged();
            }
            void AssertFreshThenHit()
            {
                var before = Reductions();
                AssertNaturalFinalReductionData(expected, Replay());
                Assert.Equal(before + 1, Reductions());
                AssertNaturalFinalReductionData(expected, Replay());
                Assert.Equal(before + 1, Reductions());
                owner.RequireExecutionCurrent();
                AssertUnchanged();
            }
            void AssertRefused(WoundPreparedAcceptedTurnPlan candidate, WoundEffectBatchPlanningResult accepted,
                string code, long reductionDelta = 0)
            {
                var before = Reductions();
                var refused = WoundAcceptedTurnPlanner.Finalize(candidate, accepted);
                Assert.False(refused.Success);
                Assert.Null(refused.Plan);
                Assert.Contains(refused.Issues, issue => issue.Code == code);
                Assert.Equal(before + reductionDelta, Reductions());
                AssertUnchanged();
                AssertFreshThenHit();
            }

            Assert.True(owner.IsNatural);
            Assert.False(owner.IsClosed);
            owner.RequireExecutionCurrent();
            Assert.True(effects.TryReadSpiritualHealingExecution(prepared, out var executed));
            Assert.Single(executed);
            Assert.Equal(1, Reductions());

            Assert.Empty(effects.EffectPlan.DeferredReactions);
            // Count only one real synchronous handoff, excluding fixture and bundle construction.
            // All authority, input/plan hashes and nested shape checks still run on each invocation.
            using (owner.EnterPassiveTimingContext())
            {
                long Count(string name) => window.CaptureNamedCounts(name).Completed;
                var planCopies = Count("WoundData.CloneEffectPlan");
                var inputCopies = Count("WoundData.CloneEffectInput");
                var payloads = Count("WoundFingerprint.AcceptedEffectPayload");
                Assert.Null(WoundAcceptedTurnPlannerCore.ValidateFinalizationHandoff(prepared, originalEffects,
                    out var admitted, out var applications, out var recovery));
                var copies = (Plans: Count("WoundData.CloneEffectPlan") - planCopies,
                    Inputs: Count("WoundData.CloneEffectInput") - inputCopies,
                    Payloads: Count("WoundFingerprint.AcceptedEffectPayload") - payloads);
                Assert.True(recovery);
                Assert.Null(applications);
                Assert.NotNull(admitted);
                Assert.Equal(effects.WoundPreparationFingerprint, admitted.WoundPreparationFingerprint);
                Assert.Equal(effects.EffectInputFingerprint, admitted.EffectInputFingerprint);
                Assert.Equal(effects.EffectAcceptedTurnPlanFingerprint, admitted.EffectAcceptedTurnPlanFingerprint);
                Assert.True(copies.Plans == 2 && copies.Inputs == 2,
                    $"One natural handoff must detach the envelope and its members once: " +
                    $"plans={copies.Plans}, inputs={copies.Inputs}, payloads={copies.Payloads}.");
            }
            using (owner.EnterPassiveTimingContext())
                AssertNaturalHandoffDataBoundaries(prepared, effects, window);
            var observerTrace = CaptureNaturalHandoffObserverTrace(prepared, originalEffects);
            AssertNaturalFinalReductionData(expected, Replay());
            AssertNaturalFinalReductionData(expected, Replay());
            Assert.Equal(1, Reductions());

            // A handoff refusal must discard the already warm pure result.
            var brokenPreparation = CopyNaturalFinalPreparation(prepared,
                preparationFingerprint: prepared.WoundPreparationFingerprint + "_changed");
            AssertRefused(brokenPreparation, originalEffects, "wound_plan_prepared_seal_mismatch");

            // Baseline is separately sealed: keep the real preparation and private execution proof,
            // but admit different DATA far enough to reach the original strict reducer refusal.
            var baseline = prepared.BaselineAuthority;
            var history = baseline.PreTurnHistory;
            history["unsupported_final_reduction_control"] = true;
            var changedBaseline = new WoundPreparedBaselineAuthority(baseline.PreparedInputFingerprint,
                baseline.PreTurnCarriers, baseline.PreTurnIdentityIndex, history,
                WoundAcceptedTurnFingerprints.ComputeBaselineAuthority(baseline.PreparedInputFingerprint,
                    baseline.PreTurnCarriers, baseline.PreTurnIdentityIndex, history));
            Assert.NotEqual(baseline.AuthoritySeal, changedBaseline.AuthoritySeal);
            var changedPreparation = CopyNaturalFinalPreparation(prepared, baseline: changedBaseline);
            Assert.Equal(prepared.WoundPreparationFingerprint,
                WoundAcceptedTurnFingerprints.ComputePreparation(changedPreparation));
            Assert.Null(WoundAcceptedTurnPlannerCore.ValidateFinalizationHandoff(changedPreparation, originalEffects,
                out _, out _, out _));
            AssertRefused(changedPreparation, originalEffects, "spiritual_healing_finalization_invalid", reductionDelta: 1);

            var effectPlan = effects.EffectPlan;
            var copiedEffect = CopyNaturalFinalEffect(effectPlan, effectPlan.InputFingerprint,
                effectPlan.SpiritualHealingExecutionAuthority);
            Assert.Equal(WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(effectPlan),
                WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(copiedEffect));
            Assert.True(EffectAcceptedTurnPlanner.TryReadSpiritualHealingExecution(prepared, copiedEffect, out _));

            // Re-sealing the batch cannot turn a changed effect input seal into a valid handoff.
            var changedEffectSeal = CopyNaturalFinalEffect(effectPlan, effectPlan.InputFingerprint + "_changed",
                effectPlan.SpiritualHealingExecutionAuthority);
            var changedEffects = WoundEffectBatchAcceptedPlan.Create(prepared, effects.EffectInput, changedEffectSeal,
                effects.ApplicationResults, effects.TerminationResults);
            AssertRefused(prepared, new(changedEffects, Array.Empty<ValidationIssue>()),
                "wound_plan_effect_handoff_invalid");

            // Re-sealed handoff DATA cannot make an unissued object the actual private proof.
            var substitutedProof = CopyNaturalFinalEffect(effectPlan, effectPlan.InputFingerprint, new object());
            Assert.False(EffectAcceptedTurnPlanner.TryReadSpiritualHealingExecution(prepared, substitutedProof, out _));
            var substitutedEffects = new WoundEffectBatchPlanningResult(
                WoundEffectBatchAcceptedPlan.Create(prepared, effects.EffectInput, substitutedProof,
                    effects.ApplicationResults, effects.TerminationResults), Array.Empty<ValidationIssue>());
            Assert.Null(WoundAcceptedTurnPlannerCore.ValidateFinalizationHandoff(prepared, substitutedEffects,
                out _, out _, out _));
            AssertRefused(prepared, substitutedEffects, "spiritual_healing_execution_invalid");

            owner.Close();
            Assert.True(owner.IsClosed);
            Assert.Throws<InvalidDataException>(() => owner.RequireExecutionCurrent());
            // A closed owner always takes the original read path. The current-observer trace must match it exactly.
            Assert.Equal(observerTrace, CaptureNaturalHandoffObserverTrace(prepared, originalEffects));
            var beforeClosedReplay = Reductions();
            AssertNaturalFinalReductionData(expected, Replay());
            Assert.Equal(beforeClosedReplay + 1, Reductions());
            AssertNaturalFinalReductionData(expected, Replay());
            Assert.Equal(beforeClosedReplay + 2, Reductions());
            Assert.Equal(8, Reductions());
            AssertUnchanged();
        });

    [Fact]
    public Task NaturalFinalReductionWithActualObserverPreservesEveryOriginalCallback()
    {
        long begins = 0, ends = 0;
        void Observe(string name, string boundary, TimeSpan _)
        {
            if (name != "WoundCanonical.Serialize") return;
            if (boundary == "begin") Interlocked.Increment(ref begins);
            if (boundary == "end") Interlocked.Increment(ref ends);
        }
        return WithNaturalFinalReductionControlAsync("observer", Observe, (fixture, owner, bundle, window, unchanged) =>
        {
            var prepared = bundle.PreparedPlan;
            var effects = new WoundEffectBatchPlanningResult(bundle.EffectBatchPlan, Array.Empty<ValidationIssue>());
            var expected = bundle.FinalPlan;
            owner.RequireExecutionCurrent();
            // A configured manager observer must prohibit reuse even when this direct call has no ambient callback.
            var directTimings = new OperationTimingAccumulator();
            using (var directWindow = directTimings.StartWindow())
            {
                var callbacksBefore = (begins, ends);
                using (FileSystemManager.EnterOperationTimingContext(null, directTimings))
                    Assert.Null(WoundAcceptedTurnPlannerCore.ValidateFinalizationHandoff(prepared, effects,
                        out _, out _, out _));
                Assert.Equal(7, directWindow.CaptureNamedCounts("WoundData.CloneEffectPlan").Completed);
                Assert.Equal(7, directWindow.CaptureNamedCounts("WoundData.CloneEffectInput").Completed);
                Assert.Equal(callbacksBefore, (begins, ends));
                directWindow.Dispose();
                Assert.True(directWindow.Capture().Complete);
            }
            var beforeFirst = (Begin: begins, End: ends);
            var first = WoundAcceptedTurnPlanner.Finalize(prepared, effects);
            var firstCallbacks = (Begin: begins - beforeFirst.Begin, End: ends - beforeFirst.End);
            Assert.True(firstCallbacks.Begin > 1, "The actual reducer must preserve its multiple original leaf callbacks.");
            Assert.Equal(firstCallbacks.Begin, firstCallbacks.End);
            AssertNaturalFinalReductionData(expected, first);
            var beforeSecond = (Begin: begins, End: ends);
            var second = WoundAcceptedTurnPlanner.Finalize(prepared, effects);
            var secondCallbacks = (Begin: begins - beforeSecond.Begin, End: ends - beforeSecond.End);
            Assert.Equal(firstCallbacks, secondCallbacks);
            AssertNaturalFinalReductionData(expected, second);
            Assert.Equal(0, window.CaptureNamedCounts("SpiritualFinal.Request").Completed);
            Assert.Equal(0, window.CaptureNamedCounts("SpiritualFinal.Reduce").Completed);
            owner.RequireExecutionCurrent();
            fixture.AssertMutationRootsUnchanged(unchanged);
            fixture.AssertResourcesUnchanged();
        });
    }

    /// <summary>Records the current-observer handoff trace and preserves the ignored-exception policy of its late callback.</summary>
    /// <param name="prepared">The real signed natural preparation, open or closed without changing its DATA.</param>
    /// <param name="effects">The detached sealed execution envelope.</param>
    /// <returns>The ordered original callback names and boundaries for comparison with the closed-owner fresh path.</returns>
    private static string[] CaptureNaturalHandoffObserverTrace(WoundPreparedAcceptedTurnPlan prepared,
        WoundEffectBatchPlanningResult effects)
    {
        var trace = new List<string>();
        using (FileSystemManager.EnterOperationTimingContext((name, boundary, _) => trace.Add(name + ":" + boundary)))
            Assert.Null(WoundAcceptedTurnPlannerCore.ValidateFinalizationHandoff(prepared, effects,
                out _, out _, out _));
        var computations = trace.Count(value => value == "FingerprintWriter.Compute:begin");
        Assert.True(computations > 1);
        var remaining = computations;
        WoundAcceptedTurnPlanningResult? observed;
        using (FileSystemManager.EnterOperationTimingContext((name, boundary, _) =>
        {
            if (name == "FingerprintWriter.Compute" && boundary == "begin" && --remaining == 0)
                throw new InvalidOperationException("natural handoff late observer control");
        }))
            observed = WoundAcceptedTurnPlannerCore.ValidateFinalizationHandoff(prepared, effects,
                out _, out _, out _);
        Assert.Equal(0, remaining);
        Assert.Null(observed);
        return trace.ToArray();
    }

    /// <summary>Copies the natural preparation DATA while retaining its actual continuation for one targeted refusal.</summary>
    /// <param name="source">The normal signed preparation whose fields are otherwise unchanged.</param>
    /// <param name="preparationFingerprint">An altered preparation seal, or null to preserve the original seal.</param>
    /// <param name="baseline">Separately re-sealed baseline DATA, or null to preserve the original baseline.</param>
    /// <returns>A detached typed candidate which does not gain authority through construction.</returns>
    private static WoundPreparedAcceptedTurnPlan CopyNaturalFinalPreparation(WoundPreparedAcceptedTurnPlan source,
        string? preparationFingerprint = null, WoundPreparedBaselineAuthority? baseline = null) =>
        new(source.Binding, source.BindingFingerprint, source.InputFingerprint,
            preparationFingerprint ?? source.WoundPreparationFingerprint, source.AllocatedWoundIds,
            source.AllocatedTransitionIds, source.PreparedWounds, source.EffectOperationBatches,
            baseline ?? source.BaselineAuthority, spiritualHealingContinuationAuthority: source.SpiritualHealingContinuationAuthority);

    /// <summary>Copies the original effect payload with a selected input seal and execution-proof companion.</summary>
    /// <param name="source">The actual accepted execution supplying every other payload field.</param>
    /// <param name="inputFingerprint">The original input seal or the targeted altered seal.</param>
    /// <param name="executionProof">The original private proof or an unissued substitute.</param>
    /// <returns>A typed candidate preserving a bound lifecycle ledger only for unchanged payload.</returns>
    private static EffectAcceptedTurnPlan CopyNaturalFinalEffect(EffectAcceptedTurnPlan source,
        string inputFingerprint, object? executionProof) =>
        new(inputFingerprint, source.CarrierAuthorityFingerprint, source.SourceAuthorityFingerprint,
            source.TargetAuthorityFingerprint, source.AllocatedCombatantIds, source.AllocatedEffectIds,
            source.AllocatedTransitionIds, source.Sources, source.Targets, source.SourceBindings,
            source.DeferredReactions, source.ReactionExpansionCount, source.ReactionExpansionUsage,
            source.ActiveEffects, source.ResourceTriggerCarriers, source.SourceAuthority, source.TargetAuthority,
            source.EventInput, source.CarrierBeforeImages, source.CarrierAfterImages, source.IdentityIndexBeforeImage,
            source.IdentityIndexAfterImage, source.TouchedPaths, source.DeletedPaths, source.AcceptedCarrierBaselines,
            woundApplicationRootEffectBindings: source.WoundApplicationRootEffectBindings,
            skillScopeAuthority: source.SkillScopeAuthority,
            mortalRecoveryExecutionAuthority: source.MortalRecoveryExecutionAuthority,
            // A lifecycle ledger is separately bound to its exact payload. Do not transplant it to changed DATA.
            lifecycleExecutionLedger: inputFingerprint == source.InputFingerprint &&
                ReferenceEquals(executionProof, source.SpiritualHealingExecutionAuthority) ? source.LifecycleExecutionLedger : null,
            spiritualHealingExecutionAuthority: executionProof,
            mortalDiagnosisExecutionAuthority: source.MortalDiagnosisExecutionAuthority,
            mortalTreatmentExecutionAuthority: source.MortalTreatmentExecutionAuthority);

    /// <summary>Compares detached final DATA and the real IV0-to-IV1 natural history and carrier oracles.</summary>
    /// <param name="expected">The original common plan's admitted final wound DATA.</param>
    /// <param name="result">A replay which must preserve that complete result.</param>
    private static void AssertNaturalFinalReductionData(WoundAcceptedTurnPlan expected, WoundAcceptedTurnPlanningResult result)
    {
        Assert.True(result.Success, string.Join(";", result.Issues.Select(issue => issue.Code + ":" + issue.Actual)));
        var actual = Assert.IsType<WoundAcceptedTurnPlan>(result.Plan);
        Assert.Equal(expected.WoundFinalPlanFingerprint, actual.WoundFinalPlanFingerprint);
        Assert.Equal(expected.AllocatedWoundIds, actual.AllocatedWoundIds);
        Assert.Equal(expected.AllocatedTransitionIds, actual.AllocatedTransitionIds);
        Assert.Equal(expected.IdentityIndexAfterImage.ToJsonString(), actual.IdentityIndexAfterImage.ToJsonString());
        Assert.Equal(expected.HistoryAfterImage.ToJsonString(), actual.HistoryAfterImage.ToJsonString());
        Assert.Equal(JsonSerializer.Serialize(expected.TransitionIntents), JsonSerializer.Serialize(actual.TransitionIntents));
        var expectedCarrier = Assert.Single(expected.CarrierContributions);
        var actualCarrier = Assert.Single(actual.CarrierContributions);
        Assert.Equal(expectedCarrier.Owner, actualCarrier.Owner);
        Assert.Equal(expectedCarrier.ExpectedWoundCollectionFingerprint, actualCarrier.ExpectedWoundCollectionFingerprint);
        var expectedMutation = Assert.Single(expectedCarrier.Mutations);
        var actualMutation = Assert.Single(actualCarrier.Mutations);
        Assert.Equal((expectedMutation.Operation, expectedMutation.WoundId), (actualMutation.Operation, actualMutation.WoundId));
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(expectedMutation.BeforeWound!),
            WoundMaterializationContract.SerializeCanonical(actualMutation.BeforeWound!));
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(expectedMutation.AfterWound!),
            WoundMaterializationContract.SerializeCanonical(actualMutation.AfterWound!));
        Assert.Equal((4, 1L), (actualMutation.AfterWound!.Severity.Rank, actualMutation.AfterWound.Recovery.CurrentStepProgress));
        var history = WoundHistoryState.Parse(actual.HistoryAfterImage.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(history.IsValid);
        Assert.Equal(2, history.State!.Transitions.Count);
        Assert.True(Assert.IsType<SpiritualHealingPersistedResult>(history.State.Transitions[^1].TransitionResult).IsNatural);
    }

    /// <summary>Owns one independent signed IV0/H0 common preparation, synchronous controls and complete cleanup.</summary>
    /// <param name="control">The control's diagnostic directory and request suffix.</param>
    /// <param name="observer">An actual filesystem observer, or null to keep passive DATA reuse eligible.</param>
    /// <param name="assertions">Synchronous controls which must not hold a thread-local timing scope across an await.</param>
    /// <returns>The completed fixture lifetime, including local-only timing and cleanup evidence.</returns>
    private static async Task WithNaturalFinalReductionControlAsync(string control,
        Action<string, string, TimeSpan>? observer,
        Action<HealingFixture, AfterlifeWoundHealingPlanner.Continuation, AcceptedMechanicsWoundStageBundle,
            OperationTimingAccumulator.Window, Dictionary<string, byte[]?>> assertions)
    {
        var evidence = Path.Combine(TestDiagnosticPaths.Root, "natural-final-reduction-controls", control, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(evidence);
        var timings = new OperationTimingAccumulator();
        using var window = timings.StartWindow();
        HealingFixture? fixture = null;
        string? ownedRoot = null;
        Exception? scenarioFailure = null;
        var passed = false;
        try
        {
            fixture = await HealingFixture.CreateAsync(4, 0, 20, stageRequest: false,
                hooks: new FileSystemManagerHooks { PassiveOperationTimings = timings, OperationTimingObserver = observer },
                allocatedRootForDiagnostics: root => ownedRoot = root,
                beforeResourceBootstrap: (files, _) => files.WriteFileAtomicAsync(
                    AfterlifeSpiritualConflictState.StatePath, AfterlifeSpiritualConflictState.CreateDefaultRoot().ToJsonString()));
            var before = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
            Assert.Equal((4, 0L), (before.Severity.Rank, before.Recovery.CurrentStepProgress));
            var (_, projection) = await PrepareLaterNaturalCycleAsync(fixture, 43, "request_natural_final_" + control);
            Assert.NotNull(projection);
            await fixture.Files.WriteFileAtomicAsync("output/interface_updates.json", new JsonObject
            {
                ["spiritualHealingGraphs"] = AuthorNaturalGraphs(before, projection!["destinationGraphs"]!.AsArray()),
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            }.ToJsonString());
            await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
            AssertNoCleanStartErrors(await new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance)
                .PrepareSpiritualSelfTreatmentPublicationAsync(lease));
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(fixture.Files, lease, out _, out var ready));
            var plan = Assert.IsType<AcceptedMechanicsPlan>(ready.Plan);
            var bundle = Assert.IsType<AcceptedMechanicsWoundStageBundle>(plan.WoundStageBundle);
            var owner = Assert.IsType<AfterlifeWoundHealingPlanner.Continuation>(bundle.SpiritualHealingContinuation);
            try
            {
                var unchanged = fixture.CaptureMutationRoots(plan.BeforeImages.Keys.Concat(plan.TouchedPaths)
                    .Concat(plan.ConsumedPaths).Append("output/interface_updates.json"));
                assertions(fixture, owner, bundle, window, unchanged);
                fixture.AssertMutationRootsUnchanged(unchanged);
                passed = true;
            }
            finally { owner.Close(); }
        }
        catch (Exception failure) { scenarioFailure = failure; throw; }
        finally
        {
            Exception? cleanupFailure = null;
            try { if (fixture is not null) await fixture.DisposeAsync(); }
            catch (Exception failure) { cleanupFailure = failure; }
            finally { window.Dispose(); }
            var capture = window.Capture();
            var removed = ownedRoot is not null && !Directory.Exists(ownedRoot);
            await File.WriteAllTextAsync(Path.Combine(evidence, "outcome.json"), JsonSerializer.Serialize(new
            {
                passed, ownedRoot, removed, scenarioFailure = scenarioFailure?.ToString(),
                cleanupFailure = cleanupFailure?.ToString(), timings = capture
            }));
            if (cleanupFailure is not null)
                throw new AggregateException("Natural reduction control fixture cleanup failed.",
                    scenarioFailure is null ? [cleanupFailure] : [scenarioFailure, cleanupFailure]);
            Assert.True(capture.Complete);
            if (ownedRoot is not null) Assert.True(removed);
        }
    }
}
