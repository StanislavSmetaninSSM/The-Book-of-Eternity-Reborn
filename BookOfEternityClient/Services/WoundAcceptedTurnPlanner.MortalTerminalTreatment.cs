namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnPlanner
{
    /// <summary>
    /// Projects the actual standalone or joint private treatment for the shared
    /// terminal executor. These retained DATA and references grant no new admission.
    /// </summary>
    internal sealed record TerminalTreatmentView(object Continuation, string ContinuationFingerprint,
        MortalWoundTreatmentAttemptRequest Request, MortalWoundTreatmentResolution Resolution,
        MortalWoundTreatmentOutcomePreparation Outcome, MortalWoundTreatmentPersistedResult PersistedResult,
        WoundAcceptedTurnBinding TreatmentBinding,
        WoundEffectOperationBatch? TreatmentBatch, MortalWoundHealLegacyPreparation Legacies);

    /// <summary>
    /// Reads one complete terminal treatment only after its existing private
    /// family and whole prepared batch agree, preserving both original bindings.
    /// </summary>
    /// <param name="prepared">
    /// The actual prepared stage with its standalone or real joint companion.
    /// </param>
    /// <param name="view">
    /// Receives copied comparison DATA and the existing private owner reference.
    /// </param>
    /// <returns>
    /// Whether the actual family has a complete terminal preparation.
    /// </returns>
    internal static bool TryReadTerminalTreatment(WoundPreparedAcceptedTurnPlan prepared, out TerminalTreatmentView view)
    {
        view = null!;
        if (prepared.JointRepairContinuationAuthority is not null)
        {
            if (!JointRepairPreparedAgrees(prepared) ||
                !TryReadJointRepair(prepared.JointRepairContinuationAuthority, out var joint) ||
                !joint.Outcome.PreparesTerminalHeal || joint.LegacyPreparation is null) return false;
            view = new(joint, joint.Fingerprint, joint.Request, joint.Resolution, joint.Outcome,
                MortalWoundTreatmentPersistedResult.Create(joint.Resolution), joint.TreatmentInput.Binding, joint.TreatmentBatch, joint.LegacyPreparation);
            return true;
        }
        if (!TryReadTreatmentContinuation(prepared.TreatmentContinuationAuthority, out var treatment) ||
            !TreatmentContinuationPreparedAgrees(prepared) || !treatment.OutcomePreparation.PreparesTerminalHeal ||
            treatment.LegacyPreparation is null) return false;
        view = new(prepared.TreatmentContinuationAuthority!, treatment.Fingerprint, treatment.RequestAuthority,
            treatment.Resolution, treatment.OutcomePreparation, treatment.PersistedResult, prepared.Binding,
            MortalWoundTreatmentOutcomePublicationPlanner.RequiresEffectBatch(treatment.OutcomePreparation)
                ? prepared.EffectOperationBatches[0] : null, treatment.LegacyPreparation);
        return true;
    }
}
