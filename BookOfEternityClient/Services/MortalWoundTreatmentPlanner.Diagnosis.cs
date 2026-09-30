using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class MortalWoundTreatmentPlanner
{
    internal static WoundTransitionRequest CreateDiagnosisTransition(
        string transitionId,
        string commandRef,
        string operationKey,
        string attemptId,
        string eventRef,
        int turn,
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope proposedAfter,
        string diagnosisPathId,
        string resultKind,
        string requirementAuthorityFingerprint,
        string checkResultFingerprint) => WoundDiagnosisEvidence.CreateRequest(
            transitionId, commandRef, operationKey, attemptId, eventRef, turn,
            before, proposedAfter, diagnosisPathId, resultKind,
            requirementAuthorityFingerprint, checkResultFingerprint);
}

// Local, detached diagnosis authority. External seals are only bindings here;
// the eventual publisher must independently revalidate their fresh world sources.
internal sealed record WoundDiagnosisEvidence : WoundTransitionEvidence
{
    private WoundDiagnosisEvidence(string commandRef, string beforeFingerprint,
        string afterFingerprint, string woundId, string attemptId, string diagnosisPathId,
        string resultKind, string requirementAuthorityFingerprint, string checkResultFingerprint,
        string diagnosisPathFingerprint, WoundDiagnosisTransitionResult transitionResult)
        : base(commandRef, beforeFingerprint, afterFingerprint)
    {
        WoundId = woundId;
        AttemptId = attemptId;
        DiagnosisPathId = diagnosisPathId;
        ResultKind = resultKind;
        RequirementAuthorityFingerprint = requirementAuthorityFingerprint;
        CheckResultFingerprint = checkResultFingerprint;
        DiagnosisPathFingerprint = diagnosisPathFingerprint;
        TransitionResult = transitionResult;
        RevealedFacts = transitionResult.RevealedFacts;
    }

    public string WoundId { get; init; }
    public string AttemptId { get; init; }
    public string DiagnosisPathId { get; init; }
    public string ResultKind { get; init; }
    public string RequirementAuthorityFingerprint { get; init; }
    public string CheckResultFingerprint { get; init; }
    public string DiagnosisPathFingerprint { get; init; }
    public IReadOnlyList<string> RevealedFacts { get; init; }
    public WoundDiagnosisTransitionResult TransitionResult { get; init; }
    private string RequestFingerprint { get; init; } = string.Empty;

    internal static WoundTransitionRequest CreateRequest(string transitionId, string commandRef,
        string operationKey, string attemptId, string eventRef, int turn,
        WoundMaterializationEnvelope before, WoundMaterializationEnvelope proposedAfter,
        string diagnosisPathId, string resultKind, string requirementAuthorityFingerprint,
        string checkResultFingerprint)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(proposedAfter);
        // Do not normalize or repair the proposed facts: legal structure with an
        // illegal delta must remain available to the reducer's semantic diagnostics.
        var detachedBefore = WoundAcceptedTurnData.CloneWound(before)!;
        var detachedAfter = WoundAcceptedTurnData.CloneWound(proposedAfter)!;
        var path = detachedBefore.Treatment.DiagnosisPaths.SingleOrDefault(value =>
            string.Equals(value.DiagnosisPathId, diagnosisPathId, StringComparison.Ordinal));
        var facts = resultKind == "success" && path is not null
            ? path.Reveals.ToImmutableArray()
            : ImmutableArray<string>.Empty;
        var unsealedResult = new WoundDiagnosisTransitionResult(diagnosisPathId, resultKind,
            facts, string.Empty);
        var result = new WoundDiagnosisTransitionResult(diagnosisPathId, resultKind, facts,
            WoundHistoryState.ComputeTransitionResultFingerprint(unsealedResult.ToCanonicalJson()));
        var evidence = new WoundDiagnosisEvidence(commandRef,
            WoundIdentityState.ComputeSemanticFingerprint(detachedBefore),
            WoundIdentityState.ComputeSemanticFingerprint(detachedAfter), detachedBefore.WoundId,
            attemptId, diagnosisPathId, resultKind, requirementAuthorityFingerprint,
            checkResultFingerprint, ComputePathFingerprint(detachedBefore, diagnosisPathId), result);
        var request = new WoundTransitionRequest("diagnose", transitionId, operationKey,
            eventRef, turn, detachedBefore, detachedAfter, evidence);
        return request with { Evidence = evidence with { RequestFingerprint = evidence.ComputeRequestFingerprint(request) } };
    }

    internal bool MatchesRequest(WoundTransitionRequest request) =>
        string.Equals(RequestFingerprint, ComputeRequestFingerprint(request), StringComparison.Ordinal) &&
        string.Equals(WoundId, request.Before!.WoundId, StringComparison.Ordinal) &&
        string.Equals(ExpectedBeforeFingerprint, WoundIdentityState.ComputeSemanticFingerprint(request.Before), StringComparison.Ordinal) &&
        string.Equals(ExpectedAfterFingerprint, WoundIdentityState.ComputeSemanticFingerprint(request.ProposedAfter!), StringComparison.Ordinal) &&
        string.Equals(DiagnosisPathFingerprint, ComputePathFingerprint(request.Before, DiagnosisPathId), StringComparison.Ordinal) &&
        string.Equals(DiagnosisPathId, TransitionResult.DiagnosisPathId, StringComparison.Ordinal) &&
        string.Equals(ResultKind, TransitionResult.Result, StringComparison.Ordinal) &&
        RevealedFacts.SequenceEqual(TransitionResult.RevealedFacts, StringComparer.Ordinal) &&
        string.Equals(TransitionResult.ResultFingerprint,
            WoundHistoryState.ComputeTransitionResultFingerprint(TransitionResult.ToCanonicalJson()), StringComparison.Ordinal);

    private string ComputeRequestFingerprint(WoundTransitionRequest request) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound.diagnosis_request", "1",
            request.Kind, request.TransitionId, request.OperationKey, request.EventRef,
            request.Turn.ToString(CultureInfo.InvariantCulture), AuthorityRef, WoundId,
            AttemptId, DiagnosisPathId, ResultKind, ExpectedBeforeFingerprint,
            ExpectedAfterFingerprint, RequirementAuthorityFingerprint, CheckResultFingerprint,
            DiagnosisPathFingerprint,
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(new JsonArray(
                RevealedFacts.Select(static fact => (JsonNode)fact).ToArray())),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(TransitionResult.ToCanonicalJson())
        });

    internal static string ComputePathFingerprint(WoundMaterializationEnvelope before, string pathId)
    {
        // Reuse the complete production path projection (including empty check,
        // requirements, readable text, and ordered facts), never a mechanics hash.
        var canonical = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(before))!;
        var path = canonical["treatment"]!["diagnosisPaths"]!.AsArray().SingleOrDefault(value =>
            string.Equals(value!["diagnosisPathId"]!.GetValue<string>(), pathId, StringComparison.Ordinal));
        return MortalWoundTreatmentMemberFingerprint.ComputeDiagnosisPath(path);
    }
}
