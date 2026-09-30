namespace BookOfEternityClient.Services;

internal static partial class WoundTransitionReducer
{
    private static void ValidateDiagnosisEvidenceIntegrity(
        WoundTransitionRequest request, List<ValidationIssue> issues)
    {
        if (request.Evidence is not WoundDiagnosisEvidence evidence)
        {
            EvidenceKindMismatch(issues, "WoundDiagnosisEvidence", request.Evidence);
            return;
        }
        if (!Exact(evidence.AuthorityRef) || !Exact(evidence.WoundId) ||
            !Exact(evidence.AttemptId) || !Exact(evidence.DiagnosisPathId) ||
            evidence.ResultKind is not ("success" or "failure") ||
            !Fingerprint(evidence.ExpectedBeforeFingerprint) ||
            !Fingerprint(evidence.ExpectedAfterFingerprint) ||
            !Fingerprint(evidence.DiagnosisPathFingerprint) ||
            !Fingerprint(evidence.RequirementAuthorityFingerprint) ||
            !Fingerprint(evidence.CheckResultFingerprint) ||
            evidence.RevealedFacts is null ||
            evidence.RevealedFacts.Count > WoundMaterializationContract.MaxRequirementsPerTreatmentMember ||
            !ExactUnique(evidence.RevealedFacts) ||
            evidence.TransitionResult is null)
        {
            Add(issues, "wound_transition_evidence_invalid",
                "factory-owned complete diagnosis evidence with exact identities, bounded facts and lowercase SHA-256 seals",
                evidence.AuthorityRef ?? "null");
            return;
        }

        try
        {
            if (request.Before is not null && request.ProposedAfter is not null &&
                evidence.MatchesRequest(request))
                return;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or NullReferenceException)
        {
            // A forged object is a diagnostic rejection, never a factory bypass.
        }
        Add(issues, "wound_transition_diagnosis_evidence_mismatch",
            "unchanged factory-sealed request, command, attempt, path, result and complete local images",
            request.TransitionId);
    }
}
