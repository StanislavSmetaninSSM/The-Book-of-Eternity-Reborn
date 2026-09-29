using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed partial class WoundHistoryState
{
    private static readonly IReadOnlySet<string> DiagnosisResultFields = Set(
        "kind", "diagnosisPathId", "result", "revealedFacts", "resultFingerprint");
    private static readonly IReadOnlySet<string> AlternativeResultFields = Set(
        "kind", "authoringRequestRef", "addedRouteId", "addedDiagnosisPathId",
        "routeFingerprint", "diagnosisPathFingerprint", "resultFingerprint");

    internal static string ComputeTransitionResultFingerprint(JsonObject unsealedResult)
    {
        ArgumentNullException.ThrowIfNull(unsealedResult);
        var payload = unsealedResult.DeepClone().AsObject();
        payload.Remove("resultFingerprint");
        return WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.wound.transition_result", "1",
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(payload)
        });
    }

    internal WoundHistoryParseResult AppendTransition(WoundTransitionHistoryIntent intent,
        string sourceFingerprint, string outputFingerprint, string readableSummary)
    {
        ArgumentNullException.ThrowIfNull(intent);
        var issues = new List<ValidationIssue>();
        var result = intent.TransitionResult;
        if (result is not null)
        {
            var canonical = result.ToCanonicalJson();
            // Treatment retains its existing, independently verified request/receipt seals.
            if (result is WoundDiagnosisTransitionResult or WoundAlternativeTreatmentTransitionResult)
                canonical["resultFingerprint"] = ComputeTransitionResultFingerprint(canonical);
            result = ParseTransitionResult(JsonSerializer.SerializeToElement(canonical),
                intent.Kind, HistoryPath + $".transitions[{_transitions.Length}].transitionResult", issues);
        }
        if (!string.Equals(outputFingerprint,
                ComputeOutputFingerprint(intent.OperationKey, intent.EventRef, readableSummary),
                StringComparison.Ordinal))
        {
            AddIssue(issues, HistoryPath + $".transitions[{_transitions.Length}].outputFingerprint",
                "wound_history_output_fingerprint_mismatch", "recomputed accepted output fingerprint", outputFingerprint);
        }
        if (issues.Count != 0)
            return new WoundHistoryParseResult(null, issues.ToImmutableArray());
        var woundOrdinal = _byWoundId.TryGetValue(intent.WoundId, out var chain) ? chain.Count + 1 : 1;
        var treatment = result as MortalWoundTreatmentPersistedResult;
        var row = new WoundHistoryTransition(intent.TransitionId, intent.WoundId,
            NextOrdinal, woundOrdinal, intent.Kind, intent.Turn, intent.EventRef,
            intent.OperationKey, intent.BeforeFingerprint, intent.AfterFingerprint,
            sourceFingerprint, intent.AttemptId, treatment?.Receipt.CourseId,
            treatment?.Receipt.CourseMilestoneOrdinal, intent.TickKey, null,
            outputFingerprint, readableSummary, intent.Terminal, result);
        return CreateValidated(NextOrdinal + 1, _transitions.Append(row));
    }

    /// <summary>
    /// Parses only the closed result codec registered for the outer transition kind.
    /// </summary>
    /// <param name="element">
    /// The supplied result value; omission retains the existing optional-result behavior.
    /// </param>
    /// <param name="kind">
    /// The exact outer transition kind selecting the codec.
    /// </param>
    /// <param name="path">
    /// The diagnostic path of the result.
    /// </param>
    /// <param name="issues">
    /// The collection receiving structural or semantic result failures.
    /// </param>
    /// <returns>
    /// The detached typed result, or null for an omitted optional result or rejected value.
    /// </returns>
    private static WoundTransitionResult? ParseTransitionResult(JsonElement element,
        string kind, string path, ICollection<ValidationIssue> issues)
    {
        var required = kind is "diagnose" or "author_alternative_treatment";
        // An explicitly supplied treatment result, including null, must still pass
        // the original strict treatment codec. Only omission keeps its old meaning.
        if (kind == "treat" && element.ValueKind != JsonValueKind.Undefined)
            return MortalWoundTreatmentPersistedResult.Parse(element, path, issues);
        if (kind == "recover" && element.ValueKind != JsonValueKind.Undefined)
            return MortalWoundRecoveryPersistedResult.Parse(element, path, issues);
        if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            if (required)
                AddIssue(issues, path, "wound_history_missing_field", "required closed transition result", "missing or null");
            return null;
        }
        if (!required || element.ValueKind != JsonValueKind.Object)
        {
            AddIssue(issues, path, "wound_history_invalid_field", "closed result matching the outer transition kind", element.GetRawText());
            return null;
        }

        var before = issues.Count;
        var fields = kind == "diagnose" ? DiagnosisResultFields : AlternativeResultFields;
        ValidateClosedObject(element, path, fields, issues);
        RequireFields(element, path, fields, issues);
        if (!element.TryGetProperty("kind", out var resultKind) ||
            resultKind.ValueKind != JsonValueKind.String || resultKind.GetString() != kind)
            AddIssue(issues, path + ".kind", "wound_history_invalid_field", kind, Describe(element, "kind"));
        var fingerprint = ReadFingerprint(element, "resultFingerprint", path, issues);
        WoundTransitionResult candidate;
        if (kind == "diagnose")
        {
            var pathId = ReadExactIdentifier(element, "diagnosisPathId", path, issues);
            var category = element.TryGetProperty("result", out var categoryElement) && categoryElement.ValueKind == JsonValueKind.String
                ? categoryElement.GetString()! : string.Empty;
            if (category is not ("success" or "failure"))
                AddIssue(issues, path + ".result", "wound_history_invalid_field", "success or failure", Describe(element, "result"));
            var facts = ReadResultFacts(element, path, issues);
            if ((category == "success" && facts.Count == 0) || (category == "failure" && facts.Count != 0))
                AddIssue(issues, path + ".revealedFacts", "wound_history_invalid_field", "complete ordered facts for success; no facts for failure", Describe(element, "revealedFacts"));
            candidate = new WoundDiagnosisTransitionResult(pathId, category, facts, fingerprint);
        }
        else
        {
            var requestRef = ReadExactIdentifier(element, "authoringRequestRef", path, issues);
            var routeId = ReadExactIdentifier(element, "addedRouteId", path, issues);
            var pathId = ReadNullableExactIdentifier(element, "addedDiagnosisPathId", path, issues);
            var routeFingerprint = ReadFingerprint(element, "routeFingerprint", path, issues);
            var pathFingerprint = ReadNullableFingerprint(element, "diagnosisPathFingerprint", path, issues);
            if ((pathId is null) != (pathFingerprint is null))
                AddIssue(issues, path + ".addedDiagnosisPathId", "wound_history_invalid_field", "path ID and fingerprint both null or both present", Describe(element, "addedDiagnosisPathId"));
            candidate = new WoundAlternativeTreatmentTransitionResult(requestRef, routeId,
                pathId, routeFingerprint, pathFingerprint, fingerprint);
        }
        // Do not materialize a JsonObject until the recursive duplicate guard and
        // closed shape have succeeded; duplicate JSON properties are never collapsed.
        if (issues.Count != before || issues.Any(issue =>
                issue.Code == "wound_history_duplicate_property" &&
                issue.FilePath.StartsWith(path + ".", StringComparison.Ordinal)))
            return null;
        var expected = ComputeTransitionResultFingerprint(JsonNode.Parse(element.GetRawText())!.AsObject());
        if (!string.Equals(fingerprint, expected, StringComparison.Ordinal))
        {
            AddIssue(issues, path + ".resultFingerprint", "wound_history_result_fingerprint_mismatch", expected, fingerprint);
            return null;
        }
        return candidate;
    }

    private static IReadOnlyList<string> ReadResultFacts(JsonElement element, string path,
        ICollection<ValidationIssue> issues)
    {
        var result = ImmutableArray.CreateBuilder<string>();
        if (!element.TryGetProperty("revealedFacts", out var facts) || facts.ValueKind != JsonValueKind.Array)
        {
            AddIssue(issues, path + ".revealedFacts", "wound_history_invalid_field", "ordered diagnosis fact array", Describe(element, "revealedFacts"));
            return result.ToImmutable();
        }
        if (facts.GetArrayLength() > WoundMaterializationContract.MaxRequirementsPerTreatmentMember)
        {
            AddIssue(issues, path + ".revealedFacts", "wound_history_limit_exceeded", "bounded diagnosis fact array", facts.GetArrayLength().ToString());
            return result.ToImmutable();
        }
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in facts.EnumerateArray())
        {
            var factPath = $"{path}.revealedFacts[{index++}]";
            var fact = item.ValueKind == JsonValueKind.String ? item.GetString()! : string.Empty;
            var prefix = fact.StartsWith("route:", StringComparison.Ordinal) ? "route:"
                : fact.StartsWith("complication:", StringComparison.Ordinal) ? "complication:" : null;
            if (prefix is null)
                AddIssue(issues, factPath, "wound_history_invalid_field", "route:<routeId> or complication:<complicationId>", item.GetRawText());
            else if (!ResourceMaterializationContract.IsExactIdentifier(fact[prefix.Length..]))
                AddIssue(issues, factPath, "wound_history_invalid_identifier", "exact wound-local fact identifier", fact);
            else if (!identities.Add(MortalLocationIdentityState.BuildConfusableKey(fact)))
                AddIssue(issues, factPath, "wound_history_invalid_field", "unique exact and nonconfusable diagnosis facts", fact);
            else
                result.Add(fact);
        }
        return result.ToImmutable();
    }

    private static bool TransitionResultsEqual(WoundTransitionResult? first, WoundTransitionResult? second) =>
        first is null ? second is null : second is not null &&
        string.Equals(WoundAcceptedTurnFingerprintWriter.CanonicalJson(first.ToCanonicalJson()),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(second.ToCanonicalJson()), StringComparison.Ordinal);

    /// <summary>
    /// Checks the complete result and its outer-row coordinates using the owning codec.
    /// </summary>
    /// <param name="transition">
    /// The typed history row whose result and coordinates must agree.
    /// </param>
    /// <param name="path">
    /// The diagnostic path of the outer history row.
    /// </param>
    /// <param name="issues">
    /// The collection receiving codec or coordinate failures.
    /// </param>
    private static void ValidateTransitionResult(WoundHistoryTransition transition,
        string path, ICollection<ValidationIssue> issues)
    {
        if (transition.TransitionResult is MortalWoundRecoveryPersistedResult)
        {
            ParseTransitionResult(JsonSerializer.SerializeToElement(transition.TransitionResult.ToCanonicalJson()),
                transition.Kind, path + ".transitionResult", issues);
            ValidateRecoveryPrimaryRow(transition, path, issues);
            return;
        }
        // Keep the treatment coordinate validator and its diagnostics intact.
        if (transition.TransitionResult is MortalWoundTreatmentPersistedResult)
        {
            ValidateTreatmentResult(transition, path, issues);
            return;
        }
        ParseTransitionResult(transition.TransitionResult is null ? default :
            JsonSerializer.SerializeToElement(transition.TransitionResult.ToCanonicalJson()),
            transition.Kind, path + ".transitionResult", issues);
        ValidateTreatmentResult(transition, path, issues);
    }
}
