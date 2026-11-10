using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeSpiritualNaturalRecoveryTests
{
    /// <summary>Checks same-call handoff DATA isolation and original refusal/fallback paths without another game setup.</summary>
    /// <param name="prepared">The existing real natural owner's signed preparation.</param>
    /// <param name="effects">Its already executed, sealed effect batch.</param>
    /// <param name="window">The caller's accumulator, under its synchronous passive timing context.</param>
    private static void AssertNaturalHandoffDataBoundaries(WoundPreparedAcceptedTurnPlan prepared,
        WoundEffectBatchAcceptedPlan effects, OperationTimingAccumulator.Window window)
    {
        Assert.Empty(effects.EffectPlan.DeferredReactions);
        var expectedData = CaptureNaturalHandoffData(effects);
        var envelope = new WoundEffectBatchPlanningResult(effects, Array.Empty<ValidationIssue>());
        WoundEffectBatchAcceptedPlan? previous = null;
        for (var call = 0; call < 2; call++)
        {
            var measured = MeasureNaturalHandoffData(prepared, envelope, window);
            Assert.Equal((2L, 2L, 2L), measured.Copies);
            Assert.Null(measured.Refusal);
            Assert.True(measured.Recovery);
            Assert.Null(measured.Applications);
            var admitted = Assert.IsType<WoundEffectBatchAcceptedPlan>(measured.Accepted);
            Assert.NotSame(effects, admitted);
            Assert.NotSame(previous, admitted);
            Assert.Equal(expectedData, CaptureNaturalHandoffData(admitted));
            AssertNaturalHandoffSealsAndCompanions(prepared, effects, admitted);
            if (call == 0)
                MutateNaturalHandoffExports(admitted);
            previous = admitted;
        }
        Assert.Equal(expectedData, CaptureNaturalHandoffData(effects));

        // Preserve short-circuit acquisition: null issues/null issue/empty result never clone a plan.
        // A contradictory plan-plus-issue envelope obtains the original one detached plan before refusal.
        var upstreamIssue = new ValidationIssue(EffectAcceptedTurnPlan.CommandPath, IssueSeverity.Error,
            "The independent handoff control supplies an upstream failure.", "natural_handoff_control_failed");
        var malformed = new (WoundEffectBatchPlanningResult Result, long Copies)[]
        {
            (new(effects, null!), 0),
            (new(effects, new ValidationIssue[] { null! }), 0),
            (new(null, Array.Empty<ValidationIssue>()), 0),
            (new(effects, new[] { upstreamIssue }), 1)
        };
        foreach (var candidate in malformed)
        {
            var measured = MeasureNaturalHandoffData(prepared, candidate.Result, window);
            Assert.Equal((candidate.Copies, candidate.Copies, candidate.Copies), measured.Copies);
            var refused = Assert.IsType<WoundAcceptedTurnPlanningResult>(measured.Refusal);
            Assert.False(refused.Success);
            Assert.Null(refused.Plan);
            Assert.Equal("wound_plan_effect_handoff_invalid", Assert.Single(refused.Issues).Code);
            Assert.Null(measured.Accepted);
            Assert.Null(measured.Applications);
            Assert.False(measured.Recovery);
        }

        var failedEnvelope = new WoundEffectBatchPlanningResult(null, new[] { upstreamIssue });
        var failed = MeasureNaturalHandoffData(prepared, failedEnvelope, window);
        Assert.Equal((0L, 0L, 0L), failed.Copies);
        var failedResult = Assert.IsType<WoundAcceptedTurnPlanningResult>(failed.Refusal);
        Assert.False(failedResult.Success);
        Assert.Null(failedResult.Plan);
        Assert.Equal(new[] { upstreamIssue.Code, "wound_plan_effect_stage_failed" },
            failedResult.Issues.Select(issue => issue.Code));
        Assert.Null(failed.Accepted);
        Assert.Null(failed.Applications);
        Assert.False(failed.Recovery);

        AssertNaturalHandoffDeferredFallback(prepared, effects, window);
    }

    /// <summary>Measures only one real synchronous handoff, excluding all fixture, export and assertion work.</summary>
    /// <param name="prepared">The existing signed natural preparation.</param>
    /// <param name="effects">The independently constructed effect-result envelope.</param>
    /// <param name="window">The caller's active passive accumulator.</param>
    /// <returns>The original production result, out parameters and exact copy/hash deltas.</returns>
    private static (WoundAcceptedTurnPlanningResult? Refusal, WoundEffectBatchAcceptedPlan? Accepted,
        IReadOnlyList<EffectAcceptedApplicationResult>? Applications, bool Recovery,
        (long Plans, long Inputs, long Payloads) Copies) MeasureNaturalHandoffData(
        WoundPreparedAcceptedTurnPlan prepared, WoundEffectBatchPlanningResult effects,
        OperationTimingAccumulator.Window window)
    {
        var before = CaptureNaturalHandoffCounts(window);
        var refusal = WoundAcceptedTurnPlannerCore.ValidateFinalizationHandoff(prepared, effects,
            out var accepted, out var applications, out var recovery);
        var after = CaptureNaturalHandoffCounts(window);
        return (refusal, accepted, applications, recovery,
            (after.Plans - before.Plans, after.Inputs - before.Inputs, after.Payloads - before.Payloads));
    }

    /// <summary>Reads completed counts without exporting or hashing any effect DATA.</summary>
    /// <param name="window">The existing fixture's named timing window.</param>
    /// <returns>Completed plan clones, input clones and accepted-payload hashes.</returns>
    private static (long Plans, long Inputs, long Payloads) CaptureNaturalHandoffCounts(
        OperationTimingAccumulator.Window window) =>
        (window.CaptureNamedCounts("WoundData.CloneEffectPlan").Completed,
            window.CaptureNamedCounts("WoundData.CloneEffectInput").Completed,
            window.CaptureNamedCounts("WoundFingerprint.AcceptedEffectPayload").Completed);

    /// <summary>Checks complete seals and unchanged immutable companions after the measured handoff has returned.</summary>
    /// <param name="prepared">The real signed preparation used for fresh seal recomputation.</param>
    /// <param name="expected">The independent expected batch.</param>
    /// <param name="actual">The returned detached batch.</param>
    private static void AssertNaturalHandoffSealsAndCompanions(WoundPreparedAcceptedTurnPlan prepared,
        WoundEffectBatchAcceptedPlan expected, WoundEffectBatchAcceptedPlan actual)
    {
        var expectedInput = expected.EffectInput;
        var actualInput = actual.EffectInput;
        var expectedPlan = expected.EffectPlan;
        var actualPlan = actual.EffectPlan;
        Assert.NotSame(expectedInput, actualInput);
        Assert.NotSame(expectedPlan, actualPlan);
        Assert.Equal(expected.WoundPreparationFingerprint, actual.WoundPreparationFingerprint);
        Assert.Equal(expected.EffectInputFingerprint, actual.EffectInputFingerprint);
        Assert.Equal(expected.EffectAcceptedTurnPlanFingerprint, actual.EffectAcceptedTurnPlanFingerprint);
        Assert.Equal(actual.WoundPreparationFingerprint, WoundAcceptedTurnFingerprints.ComputePreparation(prepared));
        Assert.Equal(actual.EffectInputFingerprint, WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, actualInput));
        Assert.Equal(actual.EffectInputFingerprint, actualPlan.InputFingerprint);
        Assert.Equal(actual.EffectAcceptedTurnPlanFingerprint, WoundAcceptedTurnFingerprints.ComputeEffectPlan(
            prepared, actualInput, actualPlan, actual.ApplicationResults, actual.TerminationResults));
        Assert.Equal(WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(expectedPlan),
            WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(actualPlan));
        Assert.Same(expectedInput.SourceAuthority, actualInput.SourceAuthority);
        Assert.Same(expectedInput.TargetAuthority, actualInput.TargetAuthority);
        Assert.Same(expectedInput.PreallocatedCombatantIdentities, actualInput.PreallocatedCombatantIdentities);
        Assert.Same(expectedInput.TargetAuthorityInput?.CombatantIdentities, actualInput.TargetAuthorityInput?.CombatantIdentities);
        Assert.Same(expectedInput.SkillScopeAuthority, actualInput.SkillScopeAuthority);
        Assert.Same(expectedInput.WoundApplicationLocations, actualInput.WoundApplicationLocations);
        Assert.Same(expectedPlan.SourceAuthority, actualPlan.SourceAuthority);
        Assert.Same(expectedPlan.TargetAuthority, actualPlan.TargetAuthority);
        Assert.Same(expectedPlan.SkillScopeAuthority, actualPlan.SkillScopeAuthority);
        Assert.Same(expectedPlan.LifecycleExecutionLedger, actualPlan.LifecycleExecutionLedger);
        Assert.Same(expectedPlan.MortalRecoveryExecutionAuthority, actualPlan.MortalRecoveryExecutionAuthority);
        Assert.Same(expectedPlan.SpiritualHealingExecutionAuthority, actualPlan.SpiritualHealingExecutionAuthority);
        Assert.Same(expectedPlan.MortalDiagnosisExecutionAuthority, actualPlan.MortalDiagnosisExecutionAuthority);
        Assert.Same(expectedPlan.MortalTreatmentExecutionAuthority, actualPlan.MortalTreatmentExecutionAuthority);
    }

    /// <summary>Snapshots every exported payload field explicitly, independently of the production fingerprint traversal.</summary>
    /// <param name="batch">A sealed expected or returned batch, read outside counter deltas.</param>
    /// <returns>Complete deterministic DATA for direct equality, including nested application materialization.</returns>
    private static string CaptureNaturalHandoffData(WoundEffectBatchAcceptedPlan batch)
    {
        var input = batch.EffectInput;
        var plan = batch.EffectPlan;
        return JsonSerializer.Serialize(new
        {
            batch.WoundPreparationFingerprint, batch.EffectInputFingerprint, batch.EffectAcceptedTurnPlanFingerprint,
            Input = new
            {
                input.SessionId, input.SnapshotToken, input.RawCommands, input.EventInput, input.Realm,
                SourceAuthority = input.SourceAuthority.Fingerprint, TargetAuthority = input.TargetAuthority.Fingerprint,
                input.PreTurnCarriers, input.PreTurnIdentityIndex,
                TargetAuthorityInput = input.TargetAuthorityInput is not { } targets ? null : new
                {
                    targets.PreTurnTargets, targets.SameTurnTargets,
                    HistoricalTargetIds = targets.HistoricalTargetIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    CombatantIdentities = targets.CombatantIdentities?.Fingerprint
                },
                input.PublicationCarrierBaselines,
                PreallocatedCombatantIdentities = input.PreallocatedCombatantIdentities?.Fingerprint,
                input.AcceptedCarrierBaselines, SkillScopeAuthority = input.SkillScopeAuthority?.Fingerprint,
                WoundApplicationLocations = input.WoundApplicationLocations?.Fingerprint
            },
            Plan = new
            {
                plan.InputFingerprint, plan.CarrierAuthorityFingerprint, plan.SourceAuthorityFingerprint,
                plan.TargetAuthorityFingerprint, plan.AllocatedCombatantIds, plan.AllocatedEffectIds,
                plan.AllocatedTransitionIds, plan.Sources, plan.Targets,
                SourceBindings = plan.SourceBindings.Select(source => new
                {
                    source.Key, source.Definition, source.Materializable, source.Active, source.SameTurn,
                    source.SourceRef, source.RequiredApplicationAuthority,
                    SatisfiedPredicates = source.SatisfiedPredicates.OrderBy(value => value, StringComparer.Ordinal).ToArray()
                }).ToArray(),
                plan.DeferredReactions, plan.ReactionExpansionCount,
                ReactionExpansionUsage = plan.ReactionExpansionUsage
                    .OrderBy(pair => pair.Key.EffectId, StringComparer.Ordinal)
                    .ThenBy(pair => pair.Key.ComponentId, StringComparer.Ordinal)
                    .Select(pair => new { pair.Key, pair.Value }).ToArray(),
                plan.ActiveEffects, plan.ResourceTriggerCarriers,
                SourceAuthority = plan.SourceAuthority.Fingerprint, TargetAuthority = plan.TargetAuthority.Fingerprint,
                plan.EventInput,
                CarrierBeforeImages = plan.CarrierBeforeImages.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray(),
                CarrierAfterImages = plan.CarrierAfterImages.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray(),
                plan.IdentityIndexBeforeImage, plan.IdentityIndexAfterImage, plan.TouchedPaths, plan.DeletedPaths,
                plan.AcceptedCarrierBaselines, plan.WoundApplicationRootEffectBindings,
                SkillScopeAuthority = plan.SkillScopeAuthority?.Fingerprint,
                LifecycleExecutionLedger = plan.LifecycleExecutionLedger?.ToJsonObject(),
                plan.IsAcceptedBoundaryComplete, plan.AcceptedBoundaryBasePlanFingerprint, plan.AcceptedBoundaryFinalPlanFingerprint
            },
            Applications = batch.ApplicationResults.Select(application => new
            {
                application.ApplicationRef, application.Disposition, application.EffectId, application.CreateTransitionId,
                application.CreatedEventRef, application.CausalEventRef, application.SourceKey, application.TargetKey,
                application.CarrierCoordinate,
                Materialization = new
                {
                    application.Materialization.ComponentCount, application.Materialization.MaterializationFingerprint,
                    Slots = application.Materialization.SlotBindings.Select(slot => new
                    {
                        slot.Slot, slot.ProfileKey, slot.ReadableSummary
                    }).ToArray()
                }
            }).ToArray(),
            batch.TerminationResults
        });
    }

    /// <summary>Mutates exported input and plan JSON without touching the caller's retained batch or real game state.</summary>
    /// <param name="batch">The first detached handoff result whose exports are deliberately modified.</param>
    private static void MutateNaturalHandoffExports(WoundEffectBatchAcceptedPlan batch)
    {
        const string probe = "natural_handoff_export_mutation";
        var input = batch.EffectInput;
        input.RawCommands[probe] = true;
        input.EventInput[probe] = true;
        if (input.PreTurnIdentityIndex is { } inputIdentity)
            inputIdentity[probe] = true;
        var plan = batch.EffectPlan;
        var events = plan.EventInput;
        var identity = plan.IdentityIndexAfterImage;
        events[probe] = true;
        identity[probe] = true;
        foreach (var carrier in plan.CarrierAfterImages.Values)
            carrier[probe] = true;
        foreach (var binding in plan.SourceBindings)
            binding.Definition[probe] = true;
        Assert.True(input.RawCommands[probe]!.GetValue<bool>());
        Assert.True(events[probe]!.GetValue<bool>());
        Assert.True(identity[probe]!.GetValue<bool>());
    }

    /// <summary>Preserves the original deferred-reaction path and rejects changed aliased predicates against fresh seals.</summary>
    /// <param name="prepared">The already signed natural preparation; no execution authority is manufactured.</param>
    /// <param name="effects">The real effect DATA supplying every unchanged field.</param>
    /// <param name="window">The caller's existing passive timing window.</param>
    private static void AssertNaturalHandoffDeferredFallback(WoundPreparedAcceptedTurnPlan prepared,
        WoundEffectBatchAcceptedPlan effects, OperationTimingAccumulator.Window window)
    {
        var predicates = new HashSet<string>(StringComparer.Ordinal) { "active" };
        var source = new EffectSourceAuthorityEntry(
            new EffectSourceKey(prepared.Binding.Realm, "wound", "wound_handoff_control", "handoff_control"),
            new JsonObject { ["definitionKey"] = "handoff_control" }, true, true, false, null, predicates);
        var reaction = new EffectReactionExecution("handoff_control_event", "handoff_control_trigger_event",
            "handoff_control_causal_event", prepared.Binding.Turn, "resource_damaged",
            new EffectTargetKey(prepared.Binding.Realm, "player", "player_soul"), "effect_handoff_control",
            "trigger_handoff_control", "component_handoff_control", "event_outcome", "before_current_event",
            null, 1, source, new JsonObject());
        var changedPlan = CopyNaturalHandoffWithDeferredReaction(effects.EffectPlan, reaction);
        Assert.Null(changedPlan.LifecycleExecutionLedger);
        Assert.Same(predicates, Assert.Single(changedPlan.DeferredReactions).DownstreamSource!.SatisfiedPredicates);
        var changedBatch = WoundEffectBatchAcceptedPlan.Create(prepared, effects.EffectInput, changedPlan,
            effects.ApplicationResults, effects.TerminationResults);
        Assert.Equal(effects.EffectInputFingerprint, changedBatch.EffectInputFingerprint);
        Assert.NotEqual(effects.EffectAcceptedTurnPlanFingerprint, changedBatch.EffectAcceptedTurnPlanFingerprint);
        var envelope = new WoundEffectBatchPlanningResult(changedBatch, Array.Empty<ValidationIssue>());
        var expected = CaptureNaturalHandoffData(changedBatch);
        var admitted = MeasureNaturalHandoffData(prepared, envelope, window);
        // No bound lifecycle ledger is transplanted, so these original seven clones do not hash that ledger's payload.
        Assert.Equal((7L, 7L, 0L), admitted.Copies);
        Assert.Null(admitted.Refusal);
        Assert.True(admitted.Recovery);
        Assert.Null(admitted.Applications);
        var accepted = Assert.IsType<WoundEffectBatchAcceptedPlan>(admitted.Accepted);
        Assert.Equal(expected, CaptureNaturalHandoffData(accepted));
        AssertNaturalHandoffSealsAndCompanions(prepared, changedBatch, accepted);
        Assert.Same(predicates, Assert.Single(accepted.EffectPlan.DeferredReactions).DownstreamSource!.SatisfiedPredicates);

        // This is sealed handoff DATA admission only. It does not authenticate the modified plan for execution.
        Assert.True(predicates.Add("changed_after_sealing"));
        var refused = MeasureNaturalHandoffData(prepared, envelope, window);
        Assert.Equal((6L, 6L, 0L), refused.Copies);
        var refusal = Assert.IsType<WoundAcceptedTurnPlanningResult>(refused.Refusal);
        Assert.False(refusal.Success);
        Assert.Null(refusal.Plan);
        Assert.Equal("wound_plan_effect_handoff_invalid", Assert.Single(refusal.Issues).Code);
        Assert.Null(refused.Applications);
        Assert.False(refused.Recovery);
    }

    /// <summary>Copies independent effect DATA with one unsupported deferred reaction and no transplanted lifecycle ledger.</summary>
    /// <param name="source">The real plan providing every other DATA field and existing proof companion.</param>
    /// <param name="reaction">The deliberately aliased predicate-set control.</param>
    /// <returns>A changed typed payload, not an authenticated execution or boundary-completion proof.</returns>
    private static EffectAcceptedTurnPlan CopyNaturalHandoffWithDeferredReaction(EffectAcceptedTurnPlan source,
        EffectReactionExecution reaction) =>
        new(source.InputFingerprint, source.CarrierAuthorityFingerprint, source.SourceAuthorityFingerprint,
            source.TargetAuthorityFingerprint, source.AllocatedCombatantIds, source.AllocatedEffectIds,
            source.AllocatedTransitionIds, source.Sources, source.Targets, source.SourceBindings,
            new[] { reaction }, source.ReactionExpansionCount, source.ReactionExpansionUsage,
            source.ActiveEffects, source.ResourceTriggerCarriers, source.SourceAuthority, source.TargetAuthority,
            source.EventInput, source.CarrierBeforeImages, source.CarrierAfterImages, source.IdentityIndexBeforeImage,
            source.IdentityIndexAfterImage, source.TouchedPaths, source.DeletedPaths, source.AcceptedCarrierBaselines,
            woundApplicationRootEffectBindings: source.WoundApplicationRootEffectBindings,
            skillScopeAuthority: source.SkillScopeAuthority,
            mortalRecoveryExecutionAuthority: source.MortalRecoveryExecutionAuthority,
            lifecycleExecutionLedger: null,
            spiritualHealingExecutionAuthority: source.SpiritualHealingExecutionAuthority,
            mortalDiagnosisExecutionAuthority: source.MortalDiagnosisExecutionAuthority,
            mortalTreatmentExecutionAuthority: source.MortalTreatmentExecutionAuthority);
}
