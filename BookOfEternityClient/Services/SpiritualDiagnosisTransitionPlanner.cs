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
        return WoundDiagnosisEvidence.CreateSpiritualRequest(context, retainedRequest, admission, proposedAfter,
            transitionId, operationKey, eventRef);
    }
}
