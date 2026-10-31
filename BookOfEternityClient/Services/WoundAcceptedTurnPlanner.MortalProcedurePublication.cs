namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnPlanner
{
    // Shared publication DATA, not a treatment outcome or an additional owner.
    // Only the two closed private continuation families can supply this view.
    internal sealed record MortalProcedurePublicationView(
        MortalWoundTreatmentAcceptedStateAuthority AcceptedStateAuthority,
        MortalWoundTreatmentAttemptRequest RequestAuthority,
        MortalWoundTreatmentResolution Resolution,
        MortalWoundTreatmentResourceFinalization ResourceFinalization,
        string AcceptedStateFingerprint, string SemanticFingerprint, object ReservationAuthority,
        MortalTreatmentItemCommandEnvelope ItemCommandEnvelope, object SkillProjectionAuthority, string Fingerprint);

    internal sealed record MortalDiagnosisPublicationBinding(object ReservationAuthority, string SemanticFingerprint,
        MortalWoundTreatmentResourceFinalization Finalization, MortalTreatmentItemCommandEnvelope ItemCommands,
        object SkillProjectionAuthority, MortalWoundCriticalReactionPublicationResult CriticalReaction);

    internal static bool TryReadMortalProcedurePublication(object? authority, out MortalProcedurePublicationView value)
    {
        if (authority is MortalDiagnosisContinuation diagnosis && diagnosis.Publication is { } binding)
        {
            value = new(diagnosis.Owner, diagnosis.Outcome.Request, diagnosis.Outcome.Resolution, binding.Finalization,
                diagnosis.Owner.AcceptedStateFingerprint, binding.SemanticFingerprint, binding.ReservationAuthority,
                binding.ItemCommands, binding.SkillProjectionAuthority, diagnosis.Fingerprint);
            return true;
        }
        if (TryReadTreatmentContinuation(authority, out var treatment))
        {
            value = new(treatment.AcceptedStateAuthority, treatment.RequestAuthority, treatment.Resolution,
                treatment.ResourceFinalization, treatment.AcceptedStateFingerprint, treatment.SemanticFingerprint,
                treatment.ReservationAuthority, treatment.ItemCommandEnvelope, treatment.SkillProjectionAuthority, treatment.Fingerprint);
            return true;
        }
        value = null!; return false;
    }

    internal static string GetMortalProcedureContinuationFingerprint(object authority) =>
        TryReadMortalProcedurePublication(authority, out var view) ? view.Fingerprint :
            throw new InvalidDataException("Procedure publication requires its exact private purpose-bound continuation.");

    internal static bool MortalProcedureReservationAgrees(object authority, object reservation, string? semantic = null) =>
        TryReadMortalProcedurePublication(authority, out var view) && ReferenceEquals(view.ReservationAuthority, reservation) &&
        (semantic is null || view.SemanticFingerprint == semantic);

    internal static bool MortalProcedurePreparedAgrees(WoundPreparedAcceptedTurnPlan prepared) =>
        prepared.MortalDiagnosisAuthority is { } diagnosis ? diagnosis.PreparedAgrees(prepared) : TreatmentContinuationPreparedAgrees(prepared);

    internal static bool MortalProcedureFinalPlanAgrees(object authority, AcceptedMechanicsWoundStageBundle bundle) =>
        authority is MortalDiagnosisContinuation diagnosis
            ? ReferenceEquals(bundle.PreparedPlan.MortalDiagnosisAuthority, diagnosis) && diagnosis.PreparedAgrees(bundle.PreparedPlan) &&
              WoundAcceptedTurnPlanCache.ValidateFinalResult(bundle.PreparedPlan, bundle.EffectBatchPlan,
                  new(bundle.FinalPlan, Array.Empty<ValidationIssue>())).Success
            : TreatmentContinuationFinalPlanAgrees(authority, bundle);

    internal static bool MortalDiagnosisPublicationAgrees(object authority, AcceptedMechanicsWoundStageBundle bundle,
        MortalWoundTreatmentAcceptedStateAuthority accepted, MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResolution resolution, string semantic)
    {
        if (authority is not MortalDiagnosisContinuation diagnosis || diagnosis.Publication is not { } publication ||
            !ReferenceEquals(diagnosis.Owner, accepted) || !ReferenceEquals(diagnosis.Outcome.Request, request) ||
            !ReferenceEquals(diagnosis.Outcome.Resolution, resolution) || publication.SemanticFingerprint != semantic ||
            request.Mode != "diagnosis" || request.Coordinates.Purpose != "diagnosis" || !MortalProcedureFinalPlanAgrees(authority, bundle)) return false;
        diagnosis.RequireCurrent();
        return true;
    }
}
