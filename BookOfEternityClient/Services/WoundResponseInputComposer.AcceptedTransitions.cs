using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class WoundResponseInputComposer
{
    // This seal proves only integrity of the closed local projection, never fresh
    // gameplay authority. Unsupported publication/capture consumers must stay gated.
    internal static JsonObject ComposeAcceptedTransitionCommandRoot(
        WoundAcceptedTurnBinding binding, WoundTransitionRequest request, string finalSceneText)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(request);
        if (!HasAcceptedTransitionBinding(binding) || request.Turn != binding.Turn ||
            binding.AcceptedEvents.Count(value => value.EventRef == request.EventRef) != 1 ||
            request.Before?.Owner.Realm != "mortal_world" ||
            request.Kind is not ("diagnose" or "author_alternative_treatment"))
            throw new ArgumentException("The transition requires its exact Mortal accepted-turn binding.");
        var reduced = WoundTransitionReducer.Reduce(request);
        if (!reduced.IsValid)
            throw new InvalidOperationException("The local transition is invalid: " +
                string.Join("; ", reduced.Issues.Select(issue => issue.Code)));
        var history = reduced.Intents.OfType<WoundTransitionHistoryIntent>().Single();
        WoundAcceptedTransitionCommandDraft draft;
        if (request.Evidence is WoundDiagnosisEvidence diagnosis &&
            history.TransitionResult is WoundDiagnosisTransitionResult diagnosisResult)
        {
            draft = new WoundDiagnosisCommandDraft(diagnosis.AuthorityRef, request.OperationKey,
                finalSceneText, new WoundDiagnosisCommandAuthority(diagnosis.AuthorityRef,
                    request.OperationKey, diagnosis.AttemptId, diagnosis.WoundId,
                    diagnosis.DiagnosisPathId, diagnosis.ExpectedBeforeFingerprint,
                    diagnosis.DiagnosisPathFingerprint, diagnosis.RequirementAuthorityFingerprint,
                    diagnosis.CheckResultFingerprint, string.Empty), diagnosisResult);
        }
        else if (request.Evidence is WoundAlternativeTreatmentEvidence alternative &&
                 history.TransitionResult is WoundAlternativeTreatmentTransitionResult alternativeResult)
        {
            var wound = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(reduced.ProposedAfter!))!;
            var routeNode = wound["treatment"]!["routes"]!.AsArray().Single(value =>
                value!["routeId"]!.GetValue<string>() == alternative.AddedRouteId)!;
            var route = MortalWoundTreatmentContract.ParseRouteShape(
                JsonSerializer.SerializeToElement(routeNode), "accepted_transition.route").Route
                ?? throw new InvalidOperationException("The accepted route has no complete typed projection.");
            MortalWoundDiagnosisPathDefinition? path = null;
            if (alternative.AddedDiagnosisPathId is not null)
            {
                var pathNode = wound["treatment"]!["diagnosisPaths"]!.AsArray().Single(value =>
                    value!["diagnosisPathId"]!.GetValue<string>() == alternative.AddedDiagnosisPathId)!;
                path = MortalWoundTreatmentContract.ParseDiagnosisPathShape(
                    JsonSerializer.SerializeToElement(pathNode), "accepted_transition.diagnosisPath").DiagnosisPath
                    ?? throw new InvalidOperationException("The accepted path has no complete typed projection.");
            }
            draft = new WoundAlternativeTreatmentCommandDraft(alternative.AuthorityRef, request.OperationKey,
                finalSceneText, new WoundAlternativeTreatmentCommandAuthority(alternative.AuthorityRef,
                    alternative.RequestAuthorityFingerprint, request.OperationKey, alternative.WoundId,
                    request.EventRef, alternative.AddedRouteId, alternative.AddedDiagnosisPathId,
                    alternative.ExpectedBeforeFingerprint, alternative.ExpectedAfterFingerprint,
                    alternative.RouteFingerprint, alternative.DiagnosisPathFingerprint,
                    alternative.EvidenceAuthorityFingerprint, alternative.RequirementAuthorityFingerprint,
                    string.Empty), route, path, alternativeResult);
        }
        else
            throw new InvalidOperationException("The accepted transition evidence/result kind does not agree.");

        var row = SerializeAcceptedTransition(draft, out var resultFingerprint);
        row["authority"]!["authorityFingerprint"] = ComputeAcceptedTransitionWireFingerprint(
            binding, draft.CommandRef, draft.TransitionKind, draft.OperationKey,
            row["authority"]!.AsObject(), resultFingerprint, draft.FinalSceneText);
        var root = AcceptedTransitionRoot(binding, new JsonArray(row));
        var parsed = ParseCommandRoot(JsonSerializer.SerializeToElement(root));
        if (!parsed.Success)
            throw new InvalidOperationException("The accepted projection is not a closed command: " +
                string.Join("; ", parsed.Issues.Select(issue => issue.Code)));
        return root;
    }

    private static bool HasAcceptedTransitionBinding(WoundAcceptedTurnBinding binding)
    {
        var issues = new List<ValidationIssue>();
        ValidateBindingAndOpportunities(binding, Array.Empty<WoundOpportunityAuthority>(), issues);
        return issues.Count == 0 && binding.Realm == "mortal_world" &&
            binding.AcceptedEvents is { Count: > 0 } &&
            ExactAndConfusableUnique(binding.AcceptedEvents.Select(value => value.EventRef));
    }

    private static JsonObject AcceptedTransitionRoot(WoundAcceptedTurnBinding binding, JsonArray rows) => new()
    {
        ["schemaVersion"] = 1, ["sessionId"] = binding.SessionId,
        ["requestId"] = binding.RequestId, ["snapshotToken"] = binding.SnapshotToken,
        ["commands"] = rows
    };

    private static JsonObject? RecomposeAcceptedTransitions(
        WoundAcceptedTurnBinding binding, WoundResponseCommandParsingResult parsed)
    {
        if (!HasAcceptedTransitionBinding(binding))
            return null;
        var original = parsed.CommandRoot!;
        if (original["sessionId"]?.GetValue<string>() != binding.SessionId ||
            original["requestId"]?.GetValue<string>() != binding.RequestId ||
            original["snapshotToken"]?.GetValue<string>() != binding.SnapshotToken)
            return null;
        var rows = new JsonArray();
        foreach (var draft in parsed.AcceptedTransitionCommands)
        {
            if (draft is not (WoundDiagnosisCommandDraft or WoundAlternativeTreatmentCommandDraft))
                return null;
            if (draft is WoundAlternativeTreatmentCommandDraft alternative &&
                binding.AcceptedEvents.Count(value => value.EventRef == alternative.Authority.EventRef) != 1)
                return null;
            var row = SerializeAcceptedTransition(draft, out var fingerprint);
            var authority = row["authority"]!.AsObject();
            var carriedResult = draft switch
            {
                WoundDiagnosisCommandDraft diagnosis => diagnosis.Result.ToCanonicalJson(),
                WoundAlternativeTreatmentCommandDraft alternativeDraft => alternativeDraft.Result.ToCanonicalJson(),
                _ => throw new InvalidOperationException("Unknown accepted transition draft type.")
            };
            if (WoundHistoryState.ComputeTransitionResultFingerprint(carriedResult) != fingerprint ||
                row["result"]!["resultFingerprint"]?.GetValue<string>() != fingerprint ||
                authority["authorityFingerprint"]?.GetValue<string>() != ComputeAcceptedTransitionWireFingerprint(
                    binding, draft.CommandRef, draft.TransitionKind, draft.OperationKey,
                    authority, fingerprint, draft.FinalSceneText))
                return null;
            if (draft is WoundAlternativeTreatmentCommandDraft authored &&
                (authored.Authority.RouteFingerprint != MortalWoundTreatmentMemberFingerprint.ComputeRoute(row["result"]!["route"]) ||
                 authored.Authority.DiagnosisPathFingerprint != (authored.DiagnosisPath is null ? null :
                     MortalWoundTreatmentMemberFingerprint.ComputeDiagnosisPath(row["result"]!["diagnosisPath"]))))
                return null;
            rows.Add(row);
        }
        var root = AcceptedTransitionRoot(binding, rows);
        return ParseCommandRoot(JsonSerializer.SerializeToElement(root)).Success ? root : null;
    }

    private static JsonObject SerializeAcceptedTransition(
        WoundAcceptedTransitionCommandDraft draft, out string verifiedResultFingerprint)
    {
        JsonObject authority;
        JsonObject result;
        WoundTransitionResult reconstructed;
        switch (draft)
        {
            case WoundDiagnosisCommandDraft diagnosis:
                var d = diagnosis.Authority;
                authority = new JsonObject
                {
                    ["commandRef"] = d.CommandRef, ["operationKey"] = d.OperationKey,
                    ["attemptId"] = d.AttemptId, ["woundId"] = d.WoundId,
                    ["diagnosisPathId"] = d.DiagnosisPathId,
                    ["expectedBeforeFingerprint"] = d.ExpectedBeforeFingerprint,
                    ["pathFingerprint"] = d.PathFingerprint,
                    ["requirementAuthorityFingerprint"] = d.RequirementAuthorityFingerprint,
                    ["checkResultFingerprint"] = d.CheckResultFingerprint,
                    ["authorityFingerprint"] = d.AuthorityFingerprint
                };
                result = new JsonObject
                {
                    ["result"] = diagnosis.Result.Result,
                    ["revealedFacts"] = new JsonArray(diagnosis.Result.RevealedFacts.Select(value => (JsonNode)value).ToArray()),
                    ["resultFingerprint"] = diagnosis.Result.ResultFingerprint
                };
                reconstructed = new WoundDiagnosisTransitionResult(d.DiagnosisPathId,
                    diagnosis.Result.Result, diagnosis.Result.RevealedFacts, string.Empty);
                break;
            case WoundAlternativeTreatmentCommandDraft alternative:
                var a = alternative.Authority;
                authority = new JsonObject
                {
                    ["authoringRequestRef"] = a.AuthoringRequestRef,
                    ["requestAuthorityFingerprint"] = a.RequestAuthorityFingerprint,
                    ["operationKey"] = a.OperationKey, ["woundId"] = a.WoundId,
                    ["eventRef"] = a.EventRef, ["addedRouteId"] = a.AddedRouteId,
                    ["addedDiagnosisPathId"] = a.AddedDiagnosisPathId,
                    ["expectedBeforeFingerprint"] = a.ExpectedBeforeFingerprint,
                    ["expectedAfterFingerprint"] = a.ExpectedAfterFingerprint,
                    ["routeFingerprint"] = a.RouteFingerprint,
                    ["diagnosisPathFingerprint"] = a.DiagnosisPathFingerprint,
                    ["evidenceAuthorityFingerprint"] = a.EvidenceAuthorityFingerprint,
                    ["requirementAuthorityFingerprint"] = a.RequirementAuthorityFingerprint,
                    ["authorityFingerprint"] = a.AuthorityFingerprint
                };
                var route = WriteAcceptedMember(writer => MortalWoundTreatmentContract.WriteRouteCanonical(writer, alternative.Route));
                var path = alternative.DiagnosisPath is null ? null : WriteAcceptedMember(writer =>
                    MortalWoundTreatmentContract.WriteDiagnosisPathCanonical(writer, alternative.DiagnosisPath));
                result = new JsonObject
                {
                    ["decision"] = "author", ["route"] = route, ["diagnosisPath"] = path,
                    ["resultFingerprint"] = alternative.Result.ResultFingerprint
                };
                reconstructed = new WoundAlternativeTreatmentTransitionResult(a.AuthoringRequestRef,
                    a.AddedRouteId, a.AddedDiagnosisPathId,
                    MortalWoundTreatmentMemberFingerprint.ComputeRoute(route),
                    path is null ? null : MortalWoundTreatmentMemberFingerprint.ComputeDiagnosisPath(path), string.Empty);
                break;
            default:
                throw new InvalidOperationException("Unknown accepted transition draft type.");
        }
        verifiedResultFingerprint = WoundHistoryState.ComputeTransitionResultFingerprint(reconstructed.ToCanonicalJson());
        return new JsonObject
        {
            ["kind"] = "accepted_transition", ["commandRef"] = draft.CommandRef,
            ["transitionKind"] = draft.TransitionKind, ["operationKey"] = draft.OperationKey,
            ["authority"] = authority, ["result"] = result, ["finalSceneText"] = draft.FinalSceneText
        };
    }

    private static JsonObject WriteAcceptedMember(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            write(writer);
        return JsonNode.Parse(stream.ToArray())!.AsObject();
    }

    private static string ComputeAcceptedTransitionWireFingerprint(
        WoundAcceptedTurnBinding binding, string commandRef, string transitionKind,
        string operationKey, JsonObject authority, string resultFingerprint, string? finalSceneText)
    {
        var unsealedAuthority = authority.DeepClone().AsObject();
        unsealedAuthority.Remove("authorityFingerprint");
        return WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.wound.accepted_transition_wire", "1",
            binding.SessionId, binding.RequestId, binding.SnapshotToken, binding.Realm,
            binding.Turn.ToString(System.Globalization.CultureInfo.InvariantCulture),
            binding.AcceptedEventsFingerprint, "accepted_transition", commandRef,
            transitionKind, operationKey, finalSceneText, resultFingerprint,
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(unsealedAuthority)
        });
    }
}
