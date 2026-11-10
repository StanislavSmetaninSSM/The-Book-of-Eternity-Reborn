using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnPlannerCore
{
    /// <summary>
    /// Assembles actual sealed treatment/heal execution. Global History order and
    /// per-wound ordinals stay distinct; no executed snapshot is rebased or edited.
    /// </summary>
    private static WoundAcceptedTurnPlanningResult ComposeTerminalTreatmentFinalPlan(
        WoundPreparedAcceptedTurnPlan prepared, WoundEffectBatchAcceptedPlan accepted)
    {
        var boundary = "private_execution";
        try
        {
            if (!WoundAcceptedTurnPlanner.TryReadTerminalTreatment(prepared, out var continuation))
                return TerminalTreatmentFailure(boundary, "missing or changed private preparation/execution");
            boundary = "original_baselines";
            var baseline = prepared.BaselineAuthority;
            var catalog = WoundCarrierCatalog.Build(baseline.PreTurnCarriers);
            var identities = WoundIdentityState.Parse(baseline.PreTurnIdentityIndex.ToJsonString(), WoundIdentityState.StatePath);
            var history = WoundHistoryState.Parse(baseline.PreTurnHistory.ToJsonString(), WoundHistoryState.HistoryPath);
            var originals = catalog.Occurrences.Where(row => row.WoundId == continuation.Outcome.Before.WoundId).ToArray();
            if (catalog.Issues.Count != 0 || !identities.IsValid || !history.IsValid || originals.Length != 1 ||
                history.State!.ValidateAgreement(identities.State!, catalog).Count != 0 ||
                WoundMaterializationContract.SerializeCanonical(originals[0].Wound) != WoundMaterializationContract.SerializeCanonical(continuation.Outcome.Before))
                return TerminalTreatmentFailure(boundary, "original wound/carrier/identity/History differs");
            var reduced = ReduceTerminalTreatmentContribution(prepared, accepted, continuation, history.State!.NextOrdinal);
            if (reduced.Value is not { } contribution) return new(null, reduced.Issues);
            var assembled = AssembleWoundContributions(new WoundOperationBeforeData(baseline.PreTurnCarriers,
                baseline.PreTurnIdentityIndex, baseline.PreTurnHistory, null, null), history.State, contribution);
            if (assembled.State is not { } state) return new(null, assembled.Issues);
            var fingerprint = WoundAcceptedTurnFingerprints.ComputeFinal(prepared, accepted,
                state.Contributions, state.Identity, state.History, state.Intents);
            return new(new WoundAcceptedTurnPlan(prepared.Binding, prepared.BindingFingerprint, prepared.InputFingerprint,
                accepted.WoundPreparationFingerprint, accepted.EffectInputFingerprint, accepted.EffectAcceptedTurnPlanFingerprint,
                fingerprint, prepared.AllocatedWoundIds, prepared.AllocatedTransitionIds,
                state.Contributions, state.Identity, state.History, state.Intents), Array.Empty<ValidationIssue>());
        }
        catch (Exception failure) when (failure is ArgumentException or InvalidOperationException or InvalidDataException or JsonException or OverflowException or NullReferenceException)
        { return TerminalTreatmentFailure(boundary, failure.Message); }
    }

    /// <summary>
    /// Checks the complete terminal contour against the exact private executed
    /// snapshots before output/publication accepts an otherwise well-formed bundle.
    /// </summary>
    /// <param name="bundle">The immutable actual prepared, effect and final stages.</param>
    /// <returns>Whether the complete original-to-terminal contour and final seal agree.</returns>
    internal static bool TerminalTreatmentFinalPlanAgrees(AcceptedMechanicsWoundStageBundle bundle)
    {
        var rebuilt = ComposeTerminalTreatmentFinalPlan(bundle.PreparedPlan, bundle.EffectBatchPlan);
        if (!rebuilt.Success || rebuilt.Plan is null) return false;
        var actual = bundle.FinalPlan;
        var fingerprint = WoundAcceptedTurnFingerprints.ComputeFinal(bundle.PreparedPlan, bundle.EffectBatchPlan,
            actual.CarrierContributions, actual.IdentityIndexAfterImage, actual.HistoryAfterImage, actual.TransitionIntents);
        return fingerprint == actual.WoundFinalPlanFingerprint && fingerprint == rebuilt.Plan.WoundFinalPlanFingerprint;
    }

    private static WoundAcceptedTurnPlanningResult TerminalTreatmentFailure(string boundary, string actual) =>
        FailedFinal("wound_plan_terminal_treatment_invalid", "Terminal treatment must preserve its original and actual executed chain.",
            "exact treat/heal/legacy History, terminal identity and carrier removal", boundary + ":" + actual);
}
