using System.Globalization;

namespace BookOfEternityClient.Services;

internal static partial class MortalWoundTreatmentPlanner
{
    internal static WoundTransitionRequest CreateAlternativeTreatmentTransition(
        string transitionId,
        string authoringRequestRef,
        string requestAuthorityFingerprint,
        string operationKey,
        string eventRef,
        int turn,
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope proposedAfter,
        string addedRouteId,
        string? addedDiagnosisPathId,
        string evidenceAuthorityFingerprint,
        string requirementAuthorityFingerprint) => WoundAlternativeTreatmentEvidence.CreateRequest(
            transitionId, authoringRequestRef, requestAuthorityFingerprint, operationKey,
            eventRef, turn, before, proposedAfter, addedRouteId, addedDiagnosisPathId,
            evidenceAuthorityFingerprint, requirementAuthorityFingerprint);
}

// A local checksum binds detached authoring inputs. It is not proof of fresh
// external request/evidence/requirement authority; the later producer owns that check.
internal sealed record WoundAlternativeTreatmentEvidence : WoundTransitionEvidence
{
    private WoundAlternativeTreatmentEvidence(string authoringRequestRef,
        string beforeFingerprint, string afterFingerprint, string woundId,
        string requestAuthorityFingerprint, string evidenceAuthorityFingerprint,
        string requirementAuthorityFingerprint, string addedRouteId, string? addedDiagnosisPathId,
        string routeFingerprint, string? diagnosisPathFingerprint,
        WoundAlternativeTreatmentTransitionResult transitionResult)
        : base(authoringRequestRef, beforeFingerprint, afterFingerprint)
    {
        WoundId = woundId;
        RequestAuthorityFingerprint = requestAuthorityFingerprint;
        EvidenceAuthorityFingerprint = evidenceAuthorityFingerprint;
        RequirementAuthorityFingerprint = requirementAuthorityFingerprint;
        AddedRouteId = addedRouteId;
        AddedDiagnosisPathId = addedDiagnosisPathId;
        RouteFingerprint = routeFingerprint;
        DiagnosisPathFingerprint = diagnosisPathFingerprint;
        TransitionResult = transitionResult;
    }

    public string WoundId { get; init; }
    public string RequestAuthorityFingerprint { get; init; }
    public string EvidenceAuthorityFingerprint { get; init; }
    public string RequirementAuthorityFingerprint { get; init; }
    public string AddedRouteId { get; init; }
    public string? AddedDiagnosisPathId { get; init; }
    public string RouteFingerprint { get; init; }
    public string? DiagnosisPathFingerprint { get; init; }
    public WoundAlternativeTreatmentTransitionResult TransitionResult { get; init; }
    private string RequestFingerprint { get; init; } = string.Empty;

    internal static WoundTransitionRequest CreateRequest(string transitionId,
        string authoringRequestRef, string requestAuthorityFingerprint, string operationKey,
        string eventRef, int turn, WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope proposedAfter, string addedRouteId, string? addedDiagnosisPathId,
        string evidenceAuthorityFingerprint, string requirementAuthorityFingerprint)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(proposedAfter);
        // Preserve illegal semantic deltas verbatim for reducer diagnostics; neither
        // normalize old members nor repair the selected appended member here.
        var detachedBefore = WoundAcceptedTurnData.CloneWound(before)!;
        var detachedAfter = WoundAcceptedTurnData.CloneWound(proposedAfter)!;
        var routeFingerprint = ComputeRouteFingerprint(detachedAfter, addedRouteId);
        var pathFingerprint = addedDiagnosisPathId is null ? null :
            WoundDiagnosisEvidence.ComputePathFingerprint(detachedAfter, addedDiagnosisPathId);
        var unsealedResult = new WoundAlternativeTreatmentTransitionResult(authoringRequestRef,
            addedRouteId, addedDiagnosisPathId, routeFingerprint, pathFingerprint, string.Empty);
        var result = new WoundAlternativeTreatmentTransitionResult(authoringRequestRef,
            addedRouteId, addedDiagnosisPathId, routeFingerprint, pathFingerprint,
            WoundHistoryState.ComputeTransitionResultFingerprint(unsealedResult.ToCanonicalJson()));
        var evidence = new WoundAlternativeTreatmentEvidence(authoringRequestRef,
            WoundIdentityState.ComputeSemanticFingerprint(detachedBefore),
            WoundIdentityState.ComputeSemanticFingerprint(detachedAfter), detachedBefore.WoundId,
            requestAuthorityFingerprint, evidenceAuthorityFingerprint, requirementAuthorityFingerprint,
            addedRouteId, addedDiagnosisPathId, routeFingerprint, pathFingerprint, result);
        var request = new WoundTransitionRequest("author_alternative_treatment", transitionId,
            operationKey, eventRef, turn, detachedBefore, detachedAfter, evidence);
        return request with { Evidence = evidence with { RequestFingerprint = evidence.ComputeRequestFingerprint(request) } };
    }

    internal bool MatchesRequest(WoundTransitionRequest request) =>
        string.Equals(RequestFingerprint, ComputeRequestFingerprint(request), StringComparison.Ordinal) &&
        string.Equals(WoundId, request.Before!.WoundId, StringComparison.Ordinal) &&
        string.Equals(ExpectedBeforeFingerprint, WoundIdentityState.ComputeSemanticFingerprint(request.Before), StringComparison.Ordinal) &&
        string.Equals(ExpectedAfterFingerprint, WoundIdentityState.ComputeSemanticFingerprint(request.ProposedAfter!), StringComparison.Ordinal) &&
        string.Equals(RouteFingerprint, ComputeRouteFingerprint(request.ProposedAfter!, AddedRouteId), StringComparison.Ordinal) &&
        string.Equals(DiagnosisPathFingerprint, AddedDiagnosisPathId is null ? null :
            WoundDiagnosisEvidence.ComputePathFingerprint(request.ProposedAfter!, AddedDiagnosisPathId), StringComparison.Ordinal) &&
        string.Equals(TransitionResult.ResultFingerprint,
            WoundHistoryState.ComputeTransitionResultFingerprint(TransitionResult.ToCanonicalJson()), StringComparison.Ordinal);

    private string ComputeRequestFingerprint(WoundTransitionRequest request) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound.alternative_authoring_request", "1",
            request.Kind, request.TransitionId, request.Turn.ToString(CultureInfo.InvariantCulture),
            AuthorityRef, RequestAuthorityFingerprint, request.OperationKey, request.EventRef, WoundId,
            ExpectedBeforeFingerprint, ExpectedAfterFingerprint, AddedRouteId, AddedDiagnosisPathId,
            RouteFingerprint, DiagnosisPathFingerprint, EvidenceAuthorityFingerprint,
            RequirementAuthorityFingerprint,
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(TransitionResult.ToCanonicalJson())
        });

    private static string ComputeRouteFingerprint(WoundMaterializationEnvelope wound, string routeId)
    {
        // Canonical wire projection includes display, complete mode-specific JSON,
        // ordered arrays and explicit nulls, but never parser source-location metadata.
        var canonical = System.Text.Json.Nodes.JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(wound))!;
        var route = canonical["treatment"]!["routes"]!.AsArray().SingleOrDefault(value =>
            string.Equals(value!["routeId"]!.GetValue<string>(), routeId, StringComparison.Ordinal));
        return MortalWoundTreatmentMemberFingerprint.ComputeRoute(route);
    }
}
