using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

/// <summary>Sealed local spiritual-purpose DATA; the actual continuation owns fresh execution authority.</summary>
internal sealed class SpiritualDiagnosisTransitionProof
{
    private readonly JsonObject _request;
    private readonly SpiritualSelfTreatmentStageContext _context;
    private readonly SpiritualDiagnosisAdmission _admission;

    private SpiritualDiagnosisTransitionProof(SpiritualSelfTreatmentStageContext context, JsonObject request,
        SpiritualDiagnosisAdmission admission)
    {
        _context = context with { Progression = JsonSerializer.Deserialize<ProgressionControl>(JsonSerializer.Serialize(context.Progression))! };
        _request = request; // Private factory already detached and validated this image once.
        _admission = admission with { Wound = WoundAcceptedTurnData.CloneWound(admission.Wound)! };
        RequestFingerprint = _request["requestFingerprint"]!.GetValue<string>();
        GuaranteeFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[]
        { "spiritual_diagnosis.guaranteed_knowledge", "1", RequestFingerprint, admission.PathFingerprint });
        Fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[]
        { "spiritual_diagnosis.local_proof", "1", RequestFingerprint, admission.SourceFingerprint,
            admission.RequirementFingerprint, admission.PathFingerprint, GuaranteeFingerprint });
    }

    internal string RequestFingerprint { get; }
    internal string GuaranteeFingerprint { get; }
    internal string Fingerprint { get; }
    internal string AttemptId => _request["diagnosis"]!["attemptId"]!.GetValue<string>();
    internal JsonObject Request => _request.DeepClone().AsObject();

    internal static SpiritualDiagnosisTransitionProof Create(SpiritualSelfTreatmentStageContext context,
        JsonObject request, SpiritualDiagnosisAdmission admission)
    {
        var detached = request.DeepClone().AsObject();
        if (!SpiritualDiagnosisRequestCodec.IsExact(detached, context, admission.Selection, admission))
            throw new InvalidDataException("Spiritual diagnosis requires its exact retained context and admission.");
        var path = admission.Wound.Treatment.DiagnosisPaths.Single(p =>
            p.DiagnosisPathId == admission.Selection.Diagnosis!.DiagnosisPathId);
        if (
            admission.Wound.Classification.Domain != "spiritual" ||
            admission.Wound.Owner.Realm is not ("chaos_sea" or "shining_abode") ||
            path.Check.ValueKind != JsonValueKind.Object || path.Check.EnumerateObject().Any())
            throw new InvalidDataException("Spiritual diagnosis requires an exact request and supported guaranteed check.");
        return new(context, detached, admission);
    }

    internal bool Matches(WoundTransitionRequest request, WoundDiagnosisEvidence evidence) =>
        request.Before is not null && request.ProposedAfter is not null &&
        evidence.ExpectedBeforeFingerprint == _admission.Selection.ExpectedWoundFingerprint &&
        evidence.AuthorityRef == _context.RequestId && request.Turn == _context.Turn &&
        evidence.DiagnosisPathId == _admission.Selection.Diagnosis!.DiagnosisPathId && evidence.ResultKind == "success" &&
        evidence.AttemptId == AttemptId &&
        evidence.RequirementAuthorityFingerprint == _admission.RequirementFingerprint &&
        evidence.CheckResultFingerprint == GuaranteeFingerprint && request.ProposedAfter.Owner == _admission.Wound.Owner;
}

internal sealed partial record WoundDiagnosisEvidence
{
    internal SpiritualDiagnosisTransitionProof? SpiritualProof { get; private init; }

    internal static WoundTransitionRequest CreateSpiritualRequest(SpiritualSelfTreatmentStageContext context,
        JsonObject request, SpiritualDiagnosisAdmission admission, WoundMaterializationEnvelope after,
        string transitionId, string operationKey, string eventRef)
    {
        var proof = SpiritualDiagnosisTransitionProof.Create(context, request, admission);
        var local = CreateRequest(transitionId, context.RequestId, operationKey,
            proof.AttemptId, eventRef, context.Turn,
            admission.Wound, after, admission.Selection.Diagnosis!.DiagnosisPathId, "success",
            admission.RequirementFingerprint, proof.GuaranteeFingerprint);
        var evidence = ((WoundDiagnosisEvidence)local.Evidence) with { SpiritualProof = proof };
        local = local with { Evidence = evidence };
        return local with { Evidence = evidence with { RequestFingerprint = evidence.ComputeRequestFingerprint(local) } };
    }

    private string ComputeRequestFingerprint(WoundTransitionRequest request)
    {
        var mortal = ComputeMortalRequestFingerprint(request);
        return SpiritualProof is null ? mortal : WoundAcceptedTurnFingerprintWriter.Compute(new[]
        { "spiritual_diagnosis.reducer_request", "1", mortal, SpiritualProof.Fingerprint });
    }
}
