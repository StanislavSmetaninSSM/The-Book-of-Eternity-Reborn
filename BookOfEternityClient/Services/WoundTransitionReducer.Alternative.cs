using System.Collections.Immutable;

namespace BookOfEternityClient.Services;

internal static partial class WoundTransitionReducer
{
    private static void ValidateAlternativeEvidenceIntegrity(
        WoundTransitionRequest request, List<ValidationIssue> issues)
    {
        if (request.Evidence is WoundAlternativeTreatmentEvidence evidence &&
            SafeAlternativeRequestReference(evidence.AuthorityRef) && Exact(evidence.WoundId) && Exact(evidence.AddedRouteId) &&
            (evidence.AddedDiagnosisPathId is null || Exact(evidence.AddedDiagnosisPathId)) &&
            Fingerprint(evidence.ExpectedBeforeFingerprint) && Fingerprint(evidence.ExpectedAfterFingerprint) &&
            Fingerprint(evidence.RequestAuthorityFingerprint) && Fingerprint(evidence.EvidenceAuthorityFingerprint) &&
            Fingerprint(evidence.RequirementAuthorityFingerprint) && Fingerprint(evidence.RouteFingerprint) &&
            (evidence.AddedDiagnosisPathId is null ? evidence.DiagnosisPathFingerprint is null : Fingerprint(evidence.DiagnosisPathFingerprint)) &&
            evidence.TransitionResult is not null)
        {
            try
            {
                if (request.Before is not null && request.ProposedAfter is not null && evidence.MatchesRequest(request))
                    return;
            }
            catch (Exception exception) when (
                exception is ArgumentException or InvalidOperationException or NullReferenceException)
            {
                // Tampering with a typed object must yield rejection, not partial intents.
            }
        }
        Add(issues, "wound_transition_alternative_evidence_invalid",
            "unchanged factory-sealed alternative request, exact identities, complete local images/result and lowercase authority seals",
            request.TransitionId);
    }

    private static bool SafeAlternativeRequestReference(string? value) =>
        Exact(value) && !value!.Any(static character =>
            character is '/' or '\\' or ':' || char.IsWhiteSpace(character));

    private static void ValidateAlternativeAppend(WoundTransitionRequest request,
        WoundMaterializationEnvelope before, WoundMaterializationEnvelope after, List<ValidationIssue> issues)
    {
        if (!ActivePair(before, after) ||
            before.Classification.Domain != "physical" || after.Classification.Domain != "physical" ||
            before.Owner.Realm != "mortal_world" || after.Owner.Realm != "mortal_world")
        {
            ActiveSourceInvalid(issues, before, after);
            return;
        }
        var evidence = (WoundAlternativeTreatmentEvidence)request.Evidence;
        var prior = before.Treatment;
        var current = after.Treatment;
        if (current.Routes.Count == prior.Routes.Count + 1)
        {
            var added = current.Routes[^1];
            var hidden = added.Visibility == "hidden";
            var expectedKnown = hidden ? prior.KnownRouteIds : prior.KnownRouteIds.Append(added.RouteId).ToImmutableArray();
            var validSelection = string.Equals(added.RouteId, evidence.AddedRouteId, StringComparison.Ordinal) &&
                added.Visibility is "public" or "known_to_player" or "hidden" &&
                SequenceEqual(current.KnownRouteIds, expectedKnown) &&
                current.DiagnosisPaths.Count == prior.DiagnosisPaths.Count + (hidden ? 1 : 0);
            if (validSelection)
            {
                validSelection = hidden
                    ? evidence.AddedDiagnosisPathId is not null &&
                      string.Equals(current.DiagnosisPaths[^1].DiagnosisPathId, evidence.AddedDiagnosisPathId, StringComparison.Ordinal) &&
                      current.DiagnosisPaths[^1].Visibility != "gm_only" &&
                      current.DiagnosisPaths[^1].Reveals.Contains("route:" + added.RouteId, StringComparer.Ordinal)
                    : evidence.AddedDiagnosisPathId is null;
            }
            // Normalize has already checked the complete registered route shapes,
            // fact identities/bounds and least-fixed-point discovery graph. Only
            // erase the allowed append and metadata here; every old byte stays owned.
            var restored = after with
            {
                Treatment = current with
                {
                    Routes = current.Routes.Take(prior.Routes.Count).ToImmutableArray(),
                    DiagnosisPaths = current.DiagnosisPaths.Take(prior.DiagnosisPaths.Count).ToImmutableArray(),
                    KnownRouteIds = prior.KnownRouteIds
                },
                LastTransition = before.LastTransition
            };
            if (validSelection && CanonicalEqual(before, restored))
                return;
        }
        Add(issues, "wound_transition_alternative_append_invalid",
            "exactly one selected complete route append, its optional reachable hidden path, and no prior fact or unrelated wound change",
            evidence.AddedRouteId);
    }
}
