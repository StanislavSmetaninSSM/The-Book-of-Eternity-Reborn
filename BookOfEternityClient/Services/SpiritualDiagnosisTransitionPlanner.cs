using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Detached spiritual-purpose input to the existing sealed diagnosis reducer family.</summary>
internal static class SpiritualDiagnosisTransitionPlanner
{
    internal static WoundTransitionRequest CreateKnowledgeRequest(SpiritualSelfTreatmentStageContext context,
        JsonObject retainedRequest, SpiritualDiagnosisAdmission admission, WoundMaterializationEnvelope proposedAfter,
        string transitionId, string operationKey, string eventRef)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(retainedRequest);
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentNullException.ThrowIfNull(proposedAfter);
        var request = retainedRequest.DeepClone().AsObject();
        if (!SpiritualDiagnosisRequestCodec.IsExact(request, context, admission.Selection, admission))
            throw new InvalidDataException("Spiritual diagnosis reduction requires its exact retained request and admitted DATA.");
        var program = request["diagnosis"]!.AsObject();
        // A retained request hash is a local binding, never a die/check receipt,
        // current source witness or permission to publish. Keep the existing
        // spiritual semantic rejection until explicit spiritual evidence dispatch
        // is implemented after the causal RED. Mortal factory/IDs stay unchanged.
        return WoundDiagnosisEvidence.CreateRequest(transitionId, context.RequestId, operationKey,
            program["attemptId"]!.GetValue<string>(), eventRef, context.Turn, admission.Wound, proposedAfter,
            admission.Selection.Diagnosis!.DiagnosisPathId, "success", admission.RequirementFingerprint,
            request["requestFingerprint"]!.GetValue<string>());
    }
}
