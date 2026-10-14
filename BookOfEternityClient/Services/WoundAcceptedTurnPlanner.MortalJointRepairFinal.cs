using System.Text.Json;

namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnPlannerCore
{
    private static WoundAcceptedTurnPlanningResult ComposeJointRepairFinalPlan(
        WoundPreparedAcceptedTurnPlan prepared, WoundEffectBatchAcceptedPlan accepted,
        IReadOnlyList<EffectAcceptedApplicationResult> applications)
    {
        try
        {
            if (!WoundAcceptedTurnPlanner.TryReadJointRepairFinal(prepared, out var joint) ||
                !WoundAcceptedTurnPlanner.JointRepairEffectInputAgrees(prepared, accepted.EffectInput))
                return FailedFinal("wound_plan_joint_final_invalid", "Joint finalization requires its whole private preparation and one effect stage.",
                    "current private joint preparation and exact effect input", "changed authority or effect input");
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

            // The shared effect stage has already authenticated the complete ordered
            // result inventory. Each pure reducer receives only its own applications.
            var acquisitionRefs = prepared.EffectOperationBatches[0].RootApplications
                .Select(row => row.ApplicationRef).ToHashSet(StringComparer.Ordinal);
            var acquisition = ReduceAcquisitionContributions(prepared, catalog, identity.State!, history.State!,
                applications.Where(row => acquisitionRefs.Contains(row.ApplicationRef)).ToArray(), acquisitionCount: 1);
            if (acquisition.Value is not { } acquired) return new(null, acquisition.Issues);
            var treatmentRefs = (joint.TreatmentBatch?.RootApplications ?? Array.Empty<WoundRootEffectApplication>())
                .Select(row => row.ApplicationRef).ToHashSet(StringComparer.Ordinal);
            var treatment = ReduceTreatmentContribution(joint.Outcome, joint.Resolution, joint.Outcome.TransitionId,
                joint.PersistedResult, joint.TreatmentBatch,
                applications.Where(row => treatmentRefs.Contains(row.ApplicationRef)).ToArray(),
                beforeMatches[0].Wound, checked(history.State!.NextOrdinal + acquired.HistoryRows.Count));
            if (treatment.Value is not { } treated) return new(null, treatment.Issues);
            var union = CombineJointRepairContributions(acquired, treated, history.State.NextOrdinal);
            if (union.Value is not { } contributions) return new(null, union.Issues);
            // Build each canonical root once from typed contributions, never merge
            // independently finalized JSON/identity/history afterimages.
            var assembled = AssembleWoundContributions(new WoundOperationBeforeData(baseline.PreTurnCarriers,
                baseline.PreTurnIdentityIndex, baseline.PreTurnHistory, null, null), history.State, contributions);
            if (assembled.State is not { } state) return new(null, assembled.Issues);
            var fingerprint = WoundAcceptedTurnFingerprints.ComputeFinal(prepared, accepted,
                state.Contributions, state.Identity, state.History, state.Intents);
            return new(new WoundAcceptedTurnPlan(prepared.Binding, prepared.BindingFingerprint, prepared.InputFingerprint,
                accepted.WoundPreparationFingerprint, accepted.EffectInputFingerprint, accepted.EffectAcceptedTurnPlanFingerprint,
                fingerprint, prepared.AllocatedWoundIds, prepared.AllocatedTransitionIds,
                state.Contributions, state.Identity, state.History, state.Intents), Array.Empty<ValidationIssue>());
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or JsonException or
            NullReferenceException or OverflowException or InvalidDataException)
        {
            return FailedFinal("wound_plan_joint_final_invalid", "The whole joint wound contribution could not finalize atomically.",
                "one sealed acquisition-then-treatment contribution graph", error.GetType().Name + ":" + error.Message);
        }
    }
}
