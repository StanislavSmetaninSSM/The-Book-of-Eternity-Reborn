using System.Text.Json;

namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnPlannerCore
{
    /// <summary>
    /// Admits the exact joint handoff, then builds its acquisition and treatment final graphs normally.
    /// </summary>
    /// <param name="prepared">
    /// The prepared whole joint stage whose private continuation and original baseline are checked.
    /// </param>
    /// <param name="accepted">
    /// The admitted shared effect stage.
    /// </param>
    /// <param name="applications">
    /// The complete ordered application results already checked by the finalization handoff.
    /// </param>
    /// <returns>
    /// The normally reduced joint plan or the original joint admission or reduction refusal.
    /// </returns>
    private static WoundAcceptedTurnPlanningResult ComposeJointRepairFinalPlan(
        WoundPreparedAcceptedTurnPlan prepared, WoundEffectBatchAcceptedPlan accepted,
        IReadOnlyList<EffectAcceptedApplicationResult> applications)
    {
        try
        {
            var refused = ReadJointFinalAdmission(prepared, accepted, out var admission);
            if (refused is not null) return refused;
            var joint = admission!.Joint;
            var effectInput = accepted.EffectInput;
            // FinalizationHandoff freshly verified these seals and actual payloads
            // before this private family dispatch. Preparation does not include the
            // baseline seal, so the baseline must remain a separate input value.
            var admittedInput = new OperationStructuralSnapshot(
            [
                prepared.WoundPreparationFingerprint, admission.Baseline.AuthoritySeal,
                accepted.EffectInputFingerprint, accepted.EffectAcceptedTurnPlanFingerprint,
                prepared.BindingFingerprint, prepared.InputFingerprint
            ],
            [
                prepared.JointRepairContinuationAuthority, effectInput.SourceAuthority,
                effectInput.TargetAuthority, effectInput.SkillScopeAuthority,
                effectInput.WoundApplicationLocations, effectInput.PreallocatedCombatantIdentities,
                effectInput.TargetAuthorityInput?.CombatantIdentities
            ]);
            return joint.SemanticData.ReadFinalReduction(joint.Operation, admittedInput,
                () => ReduceJointFinalPlan(prepared, accepted, applications, admission));
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or JsonException or
            NullReferenceException or OverflowException or InvalidDataException)
        {
            return JointFinalFailure(error);
        }
    }

    /// <summary>
    /// Reduces freshly admitted detached joint DATA into independent final after-images.
    /// </summary>
    /// <param name="prepared">
    /// The exact preparation already checked by the finalization handoff and joint admission.
    /// </param>
    /// <param name="accepted">
    /// The same accepted effect result whose actual seals and ordered inventory were freshly verified.
    /// </param>
    /// <param name="applications">
    /// The complete ordered detached application results admitted for this invocation.
    /// </param>
    /// <param name="admission">
    /// The original agreeing baseline, catalog, identity, history and retained joint contribution DATA.
    /// </param>
    /// <returns>
    /// The independently assembled final plan or the unchanged pure reduction refusal.
    /// </returns>
    private static WoundAcceptedTurnPlanningResult ReduceJointFinalPlan(WoundPreparedAcceptedTurnPlan prepared,
        WoundEffectBatchAcceptedPlan accepted, IReadOnlyList<EffectAcceptedApplicationResult> applications,
        JointFinalAdmission admission)
    {
        using var reductionTiming = BookOfEternityClient.Core.FileSystemManager.MeasureCurrentOperation("JointFinal.Reduce");
        var joint = admission.Joint;
        var baseline = admission.Baseline;
        var catalog = admission.Catalog;
        var identity = admission.Identity;
        var history = admission.History;
        var before = admission.Before;

        // The shared effect stage has already authenticated the complete ordered
        // result inventory. Each pure reducer receives only its own applications.
        var acquisitionRefs = prepared.EffectOperationBatches[0].RootApplications
            .Select(row => row.ApplicationRef).ToHashSet(StringComparer.Ordinal);
        var acquisition = ReduceAcquisitionContributions(prepared, catalog, identity, history,
            applications.Where(row => acquisitionRefs.Contains(row.ApplicationRef)).ToArray(), acquisitionCount: 1);
        if (acquisition.Value is not { } acquired) return new(null, acquisition.Issues);
        var treatmentRefs = (joint.TreatmentBatch?.RootApplications ?? Array.Empty<WoundRootEffectApplication>())
            .Select(row => row.ApplicationRef).ToHashSet(StringComparer.Ordinal);
        var treatment = ReduceTreatmentContribution(joint.Outcome, joint.Resolution, joint.Outcome.TransitionId,
            joint.PersistedResult, joint.TreatmentBatch,
            applications.Where(row => treatmentRefs.Contains(row.ApplicationRef)).ToArray(),
            before, checked(history.NextOrdinal + acquired.HistoryRows.Count));
        if (treatment.Value is not { } treated) return new(null, treatment.Issues);
        var union = CombineJointRepairContributions(acquired, treated, history.NextOrdinal);
        if (union.Value is not { } contributions) return new(null, union.Issues);
        // Build each canonical root once from typed contributions, never merge
        // independently finalized JSON/identity/history afterimages.
        var assembled = AssembleWoundContributions(new WoundOperationBeforeData(baseline.PreTurnCarriers,
            baseline.PreTurnIdentityIndex, baseline.PreTurnHistory, null, null), history, contributions);
        if (assembled.State is not { } state) return new(null, assembled.Issues);
        var fingerprint = WoundAcceptedTurnFingerprints.ComputeFinal(prepared, accepted,
            state.Contributions, state.Identity, state.History, state.Intents);
        return new(new WoundAcceptedTurnPlan(prepared.Binding, prepared.BindingFingerprint, prepared.InputFingerprint,
            accepted.WoundPreparationFingerprint, accepted.EffectInputFingerprint, accepted.EffectAcceptedTurnPlanFingerprint,
            fingerprint, prepared.AllocatedWoundIds, prepared.AllocatedTransitionIds,
            state.Contributions, state.Identity, state.History, state.Intents), Array.Empty<ValidationIssue>());
    }

    /// <summary>
    /// Repeats joint-final admission without rebuilding the previously retained final graphs.
    /// </summary>
    /// <param name="prepared">
    /// The prepared stage whose actual private joint companion must still agree.
    /// </param>
    /// <param name="accepted">
    /// The accepted effect stage whose input must match the whole joint preparation.
    /// </param>
    /// <returns>
    /// The original joint refusal, or <see langword="null"/> when the complete joint admission succeeds.
    /// </returns>
    internal static WoundAcceptedTurnPlanningResult? ValidateJointFinalAdmission(
        WoundPreparedAcceptedTurnPlan prepared, WoundEffectBatchAcceptedPlan accepted)
    {
        try { return ReadJointFinalAdmission(prepared, accepted, out _); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or JsonException or
            NullReferenceException or OverflowException or InvalidDataException)
        {
            return JointFinalFailure(error);
        }
    }

    /// <summary>
    /// Checks the original joint owner, effect input and baseline in their existing short-circuit order.
    /// </summary>
    /// <param name="prepared">
    /// The whole joint preparation to authenticate.
    /// </param>
    /// <param name="accepted">
    /// The effect input to compare with that preparation.
    /// </param>
    /// <param name="admission">
    /// Receives private reduction data only on success; <see langword="null"/> on an ordinary refusal.
    /// </param>
    /// <returns>
    /// The original refusal result, or <see langword="null"/> when reduction data has been admitted.
    /// </returns>
    private static WoundAcceptedTurnPlanningResult? ReadJointFinalAdmission(
        WoundPreparedAcceptedTurnPlan prepared, WoundEffectBatchAcceptedPlan accepted,
        out JointFinalAdmission? admission)
    {
        using var operationTiming = BookOfEternityClient.Core.FileSystemManager.MeasureCurrentOperation(
            "WoundPlanner.JointFinalAdmission");
        admission = null;
        if (!WoundAcceptedTurnPlanner.TryReadJointRepairFinal(prepared, out var joint) ||
            !WoundAcceptedTurnPlanner.JointRepairEffectInputAgrees(prepared, accepted.EffectInput))
            return FailedFinal("wound_plan_joint_final_invalid", "Joint finalization requires its whole private preparation and one effect stage.",
                "current private joint preparation and exact effect input", "changed authority or effect input");
        using var baselineTiming = BookOfEternityClient.Core.FileSystemManager.MeasureCurrentOperation(
            "JointFinalAdmission.Baseline");
        var baseline = prepared.BaselineAuthority;
        var catalog = WoundCarrierCatalog.Build(baseline.PreTurnCarriers);
        var identity = WoundIdentityState.Parse(baseline.PreTurnIdentityIndex.ToJsonString(), WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(baseline.PreTurnHistory.ToJsonString(), WoundHistoryState.HistoryPath);
        var beforeMatches = catalog.Occurrences.Where(row => row.WoundId == joint.Outcome.Before.WoundId).ToArray();
        if (catalog.Issues.Count != 0 || !identity.IsValid || !history.IsValid ||
            history.State!.ValidateAgreement(identity.State!, catalog).Count != 0 || beforeMatches.Length != 1 ||
            WoundIdentityState.ComputeSemanticFingerprint(beforeMatches[0].Wound) !=
                WoundIdentityState.ComputeSemanticFingerprint(joint.Outcome.Before))
            return FailedFinal("wound_plan_joint_baseline_invalid", "Both contributions require one exact agreeing original wound baseline.",
                joint.Outcome.Before.WoundId, "changed carrier, identity or history");
        admission = new(joint, baseline, catalog, identity.State!, history.State!, beforeMatches[0].Wound);
        return null;
    }

    /// <summary>
    /// Maps a joint admission or reducer exception using the unchanged finalization refusal.
    /// </summary>
    /// <param name="error">
    /// The actual exception caught by the existing joint finalization filter.
    /// </param>
    /// <returns>
    /// The original joint-final failure code, message and exception detail.
    /// </returns>
    private static WoundAcceptedTurnPlanningResult JointFinalFailure(Exception error) =>
        FailedFinal("wound_plan_joint_final_invalid", "The whole joint wound contribution could not finalize atomically.",
            "one sealed acquisition-then-treatment contribution graph", error.GetType().Name + ":" + error.Message);

    /// <summary>
    /// Holds only the private data admitted for this synchronous final reduction call.
    /// </summary>
    /// <param name="Joint">
    /// The existing authenticated joint final view.
    /// </param>
    /// <param name="Baseline">
    /// The detached original baseline used by assembly.
    /// </param>
    /// <param name="Catalog">
    /// The agreeing original carrier catalog.
    /// </param>
    /// <param name="Identity">
    /// The agreeing parsed original identity state.
    /// </param>
    /// <param name="History">
    /// The agreeing parsed original history state.
    /// </param>
    /// <param name="Before">
    /// The unique original wound that agrees with the treatment outcome.
    /// </param>
    private sealed record JointFinalAdmission(
        WoundAcceptedTurnPlanner.JointRepairFinalView Joint,
        WoundPreparedBaselineAuthority Baseline,
        WoundCarrierCatalog Catalog,
        WoundIdentityState Identity,
        WoundHistoryState History,
        WoundMaterializationEnvelope Before);

}
