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

    /// <summary>Checks the complete effect input against its exact private procedure-family preparation.</summary>
    /// <param name="authority">The actual diagnosis or ordinary treatment continuation, never a serialized marker.</param>
    /// <param name="prepared">The prepared stage owned by that continuation.</param>
    /// <param name="input">The complete proposed effect input, including canonical lifecycle and typed Fate events.</param>
    /// <returns>Whether the immutable input agrees; actual reservation/currentness checks remain with the existing owners.</returns>
    internal static bool MortalProcedureEffectInputAgrees(object authority, WoundPreparedAcceptedTurnPlan prepared,
        EffectAcceptedTurnInput input)
    {
        if (authority is not MortalDiagnosisContinuation diagnosis)
            return TreatmentContinuationEffectInputAgrees(authority, input);
        try
        {
            if (!ReferenceEquals(prepared.MortalDiagnosisAuthority, diagnosis) || !diagnosis.PreparedAgrees(prepared))
                return false;
            var expected = ComposeMortalDiagnosisEffectInput(prepared);
            return WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, input) ==
                WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, expected);
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException &&
            failure is (InvalidDataException or InvalidOperationException or ArgumentException or System.Text.Json.JsonException or
                OverflowException or NullReferenceException))
        {
            return false;
        }
    }

    internal static bool MortalProcedureFinalPlanAgrees(object authority, AcceptedMechanicsWoundStageBundle bundle) =>
        authority is MortalDiagnosisContinuation diagnosis
            ? MortalProcedureFinalPlanAgrees(diagnosis, bundle.PreparedPlan, bundle.EffectBatchPlan, bundle.FinalPlan)
            : TreatmentContinuationFinalPlanAgrees(authority, bundle);

    /// <summary>Checks the same immutable diagnosis handoff using one caller-owned export of each stage; this DATA comparison grants no current authority.</summary>
    internal static bool MortalProcedureFinalPlanAgrees(MortalDiagnosisContinuation diagnosis,
        WoundPreparedAcceptedTurnPlan prepared, WoundEffectBatchAcceptedPlan effects, WoundAcceptedTurnPlan final) =>
        ReferenceEquals(prepared.MortalDiagnosisAuthority, diagnosis) && diagnosis.PreparedAgrees(prepared) &&
        WoundAcceptedTurnPlanCache.ValidateFinalResult(prepared, effects,
            new(final, Array.Empty<ValidationIssue>())).Success;

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
