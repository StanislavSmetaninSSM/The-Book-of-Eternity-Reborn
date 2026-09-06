using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class WoundRepairPacketBuilder
{
    private static readonly IReadOnlySet<string> NoPrivateValues = new HashSet<string>(StringComparer.Ordinal);

    private static bool TryBuildAlternativePacket(WoundRepairBuildRequest request,
        WoundRepairCandidateInput candidate, out WoundRepairPacket packet)
    {
        packet = null!;
        var original = candidate.RejectedDecision.DeepClone().AsObject();
        if (candidate.OpportunityAuthorityFingerprint is not null ||
            candidate.AllowedDecisions.Count != 2 ||
            !candidate.AllowedDecisions.ToHashSet(StringComparer.Ordinal).SetEquals(new[] { "author", "decline" }) ||
            !HasAlternativeEnvelope(original, candidate.OpportunityRef, out var decision))
            return false;
        if (decision == "decline")
            return candidate.Issues.Count == 0 && ParseAlternative(original, "woundTreatmentAuthorings[0]").IsValid;
        if (candidate.Issues.Count == 0)
            return false;

        string? prefix = null;
        foreach (var issue in candidate.Issues)
        {
            if (issue.Severity != IssueSeverity.Error ||
                issue.Section is not ("wound_materialization" or "wound_treatment_authorings") ||
                !TryAlternativeSourcePath(issue.FilePath, out var issuePrefix, out _) ||
                (prefix is not null && prefix != issuePrefix))
                return false;
            prefix = issuePrefix;
        }
        var independent = ParseAlternative(original, prefix!);
        if (independent.IsValid || candidate.Issues.Any(supplied => !independent.Issues.Any(actual =>
                actual.Code == supplied.Code && actual.FilePath == supplied.FilePath)))
            return false;

        var secrets = new HashSet<string>(StringComparer.Ordinal);
        CollectSensitiveValues(original, secrets);
        CollectSensitiveValues(candidate.SafeContext, secrets);
        if (!TryProjectSafeContext(candidate.SafeContext, out var context) ||
            ContainsAlternativePrivateData(context, secrets) ||
            UnsafeEvidence(candidate.OpportunityRef, secrets))
            return false;
        var baseline = SanitizeAlternative(AlternativePayload(original), secrets)!.AsObject();
        var sanitizedAuthor = AlternativeEnvelope(candidate.OpportunityRef, baseline);
        if (!TryProjectAlternativeDiagnostics(sanitizedAuthor, prefix!, secrets, out var projected, out var rules))
            return false;
        var preserved = baseline.DeepClone().AsObject();
        foreach (var rule in rules.Where(rule => rule.Kind == AlternativeEditKind.ReplaceOrRemove))
            MaskAlternativePath(preserved, rule.Path);

        var shape = new JsonObject
        {
            ["woundTreatmentAuthorings"] = new JsonArray(new JsonObject
            {
                ["authoringRequestRef"] = candidate.OpportunityRef,
                ["decision"] = "author",
                ["route"] = AlternativeRecipe("route", preserved, projected),
                ["diagnosisPath"] = AlternativeRecipe("diagnosisPath", preserved, projected)
            }),
            ["response"] = "complete accepted scene"
        };
        packet = new WoundRepairPacket(request, candidate, projected, context, preserved, shape,
            baseline, secrets, rules, prefix!);
        return true;
    }

    internal static bool HasAlternativeEnvelope(JsonObject original, string reference, out string decision)
    {
        decision = string.Empty;
        return original.Count == 4 && original.All(pair => pair.Key is
                   "authoringRequestRef" or "decision" or "route" or "diagnosisPath") &&
               TryReadExactString(original, "authoringRequestRef", out var actualRef) &&
               actualRef == reference && !actualRef.Any(character => char.IsWhiteSpace(character) || character is '/' or '\\' or ':') &&
               TryReadExactString(original, "decision", out decision) && decision is "author" or "decline";
    }

    internal static WoundAlternativeTreatmentResponseParseResult ParseAlternative(JsonObject original, string prefix) =>
        WoundResponseInputComposer.ParseAlternativeTreatmentAuthoring(JsonSerializer.SerializeToElement(original), prefix);

    internal static JsonObject AlternativePayload(JsonObject original)
    {
        var result = new JsonObject();
        foreach (var root in new[] { "route", "diagnosisPath" })
            if (original.TryGetPropertyValue(root, out var value))
                result[root] = value?.DeepClone();
        return result;
    }

    private static JsonObject AlternativeEnvelope(string reference, JsonObject payload) => new()
    {
        ["authoringRequestRef"] = reference, ["decision"] = "author",
        ["route"] = payload["route"]?.DeepClone(), ["diagnosisPath"] = payload["diagnosisPath"]?.DeepClone()
    };

    private static JsonNode? AlternativeRecipe(string root, JsonObject preserved,
        IReadOnlyList<WoundRepairPacketIssue> issues)
    {
        var paths = issues.Select(issue => issue.Path).Where(path => path == root || path.StartsWith(root + ".", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal).ToArray();
        if (root == "diagnosisPath" && preserved.ContainsKey(root) && preserved[root] is null && paths.Length == 0)
            return null;
        return new JsonObject { ["base"] = "preservedProposal." + root,
            ["correctOnly"] = new JsonArray(paths.Select(path => (JsonNode?)JsonValue.Create(path)).ToArray()) };
    }

    internal static bool TryProjectAlternativeDiagnostics(JsonObject author, string prefix, IReadOnlySet<string> secrets,
        out IReadOnlyList<WoundRepairPacketIssue> projected, out IReadOnlyList<AlternativeEditRule> rules)
    {
        projected = Array.Empty<WoundRepairPacketIssue>();
        rules = Array.Empty<AlternativeEditRule>();
        var parsed = ParseAlternative(author, prefix);
        if (parsed.IsValid || parsed.Issues.Length == 0)
            return false;
        var rows = new List<(WoundRepairPacketIssue Issue, AlternativeEditRule Rule)>();
        foreach (var diagnostic in parsed.Issues)
        {
            if (!TryAlternativeSourcePath(diagnostic.FilePath, out var actualPrefix, out var path) ||
                actualPrefix != prefix || diagnostic.Code is not { } code)
                return false;
            AlternativeEditRule? rule = null;
            if (path == "diagnosisPath" && code == "wound_response_invalid_field" &&
                author["route"] is JsonObject route && author["diagnosisPath"] is JsonObject diagnosis)
            {
                var routeResult = MortalWoundTreatmentContract.ParseGmRouteDraftShape(JsonSerializer.SerializeToElement(route), prefix + ".route");
                var pathResult = MortalWoundTreatmentContract.ParseDiagnosisPathShape(JsonSerializer.SerializeToElement(diagnosis), prefix + ".diagnosisPath");
                if (routeResult.Route?.Visibility == "hidden")
                {
                    if (!pathResult.IsValid)
                        continue; // A failed member parse is not permission to replace that whole member.
                    if (pathResult.DiagnosisPath!.Visibility == "gm_only")
                        path = "diagnosisPath.visibility";
                    else
                    {
                        path = "diagnosisPath.reveals";
                        code = "wound_alternative_required_route_reveal_missing";
                        rule = new AlternativeEditRule(path, AlternativeEditKind.AppendRequiredRouteFact,
                            "route:" + routeResult.Route.RouteId, diagnosis["reveals"]!.AsArray());
                    }
                }
            }
            var expected = AlternativeExpected(path, code);
            if (expected is null || !IsAlternativeDiagnosticPath(path, code) || UnsafeEvidence(path, secrets))
                return false;
            var actual = diagnostic.Actual ?? "missing";
            actual = UnsafeEvidence(actual, secrets) ? "redacted unsafe authority evidence" : Bound(actual);
            rows.Add((new(path, code, expected, actual), rule ?? new AlternativeEditRule(path, AlternativeEditKind.ReplaceOrRemove)));
        }
        var narrow = rows.Where(row => !rows.Any(other => IsStrictAlternativeDescendant(row.Issue.Path, other.Issue.Path)))
            .DistinctBy(row => (row.Issue.Path, row.Issue.Code)).ToArray();
        if (narrow.Length == 0)
            return false;
        projected = narrow.Select(row => row.Issue).ToArray();
        rules = narrow.DistinctBy(row => row.Rule.Path).Select(row => row.Rule).ToArray();
        return true;
    }

    private static bool IsStrictAlternativeDescendant(string parent, string child) =>
        TryParsePath(parent, out var left) && TryParsePath(child, out var right) &&
        left.Count < right.Count && left.SequenceEqual(right.Take(left.Count));

    private static bool TryAlternativeSourcePath(string? raw, out string prefix, out string path)
    {
        prefix = path = string.Empty;
        if (raw is null || !TryParsePath(raw, out var segments) || segments.Count < 3 ||
            segments[0] is not "woundTreatmentAuthorings" || segments[1] is not int ordinal ||
            segments[2] is not ("route" or "diagnosisPath"))
            return false;
        prefix = $"woundTreatmentAuthorings[{ordinal}]";
        path = raw[(prefix.Length + 1)..];
        return true;
    }

    private static bool IsAlternativeDiagnosticPath(string path, string code)
    {
        if (!TryParsePath(path, out var parts) || parts[0] is not ("route" or "diagnosisPath") ||
            parts.OfType<string>().Any(SensitiveKeys.Contains) || UnsafeEvidence(path, NoPrivateValues))
            return false;
        return code switch
        {
            "wound_treatment_diagnosis_fact_unknown" => parts.Count is 2 or 3 && parts[0] is "diagnosisPath" &&
                parts[1] is "requiresKnownFacts" or "reveals" && (parts.Count == 2 || parts[2] is int),
            "wound_treatment_diagnosis_path_unreachable" => path == "diagnosisPath.requiresKnownFacts",
            "wound_response_invalid_field" => path is "diagnosisPath" or "diagnosisPath.visibility",
            "wound_alternative_required_route_reveal_missing" => path == "diagnosisPath.reveals",
            "wound_treatment_route_visibility_invalid" => path == "route.visibility",
            "effect_source_parameter_forbidden" or "effect_source_parameter_required" or "effect_source_parameter_out_of_bounds" =>
                IsAlternativeParameterPath(parts),
            _ => AlternativeExpected(path, code) is not null
        };
    }

    private static bool IsAlternativeParameterPath(IReadOnlyList<object> parts)
    {
        // The result contour is deliberately closed; effectDraft is never free-standing.
        var offset = parts.Count > 4 && parts[1] is "outcomes" && parts[2] is int && parts[3] is "result" && parts[4] is int ? 5 :
            parts.Count > 3 && parts[1] is "interruption" && parts[2] is "result" && parts[3] is int ? 4 : -1;
        return parts[0] is "route" && offset > 0 && parts.Count - offset is 6 or 7 &&
            parts[offset] is "legacies" && parts[offset + 1] is int && parts[offset + 2] is "effectDraft" &&
            parts[offset + 3] is "applications" && parts[offset + 4] is int && parts[offset + 5] is "parameters" &&
            (parts.Count - offset == 6 || parts[offset + 6] is string);
    }

    private static string? AlternativeExpected(string path, string code) => (path, code) switch
    {
        ("route.mode", "wound_materialization_invalid_field") => "procedure, course, or guaranteed",
        ("diagnosisPath", "wound_response_invalid_field") => "one complete non-GM-only path revealing the new hidden route; null for a visible route",
        ("diagnosisPath.visibility", "wound_response_invalid_field") => "public, known_to_player, or hidden; preserve the complete diagnosis path",
        ("diagnosisPath.reveals", "wound_alternative_required_route_reveal_missing") => "append exactly the new route fact and preserve every existing fact in order",
        ("route.visibility", "wound_treatment_route_visibility_invalid") => "public, known_to_player, or hidden",
        (_, "wound_materialization_unknown_field") => "omit only this unknown GM-authored field and preserve every valid sibling",
        (_, "wound_materialization_missing_field") => "supply this required complete current-version route/path field",
        (_, "wound_materialization_invalid_identifier") => "one non-empty trimmed NFKC exact identifier without control or separator characters",
        (_, "wound_materialization_duplicate_identifier") => "one exact and Unicode-confusable-unique identifier in this collection",
        (_, "wound_materialization_duplicate_coordinate") => "one unique coordinate in this closed local route/path payload",
        (_, "wound_materialization_limit_exceeded") => "a collection within the documented closed current-version route/path limits",
        (_, "wound_treatment_diagnosis_fact_unknown") => "route:<routeId> or complication:<complicationId> in the documented fact grammar",
        (_, "wound_treatment_diagnosis_path_unreachable") => "at least one known-fact prerequisite for a hidden diagnosis path",
        (_, "effect_source_parameter_forbidden") => "omit this parameter not declared by its local source definition",
        (_, "effect_source_parameter_required") => "supply the required parameter declared by its local source definition",
        (_, "effect_source_parameter_out_of_bounds") => "a value inside the exact local source definition parameter bounds",
        (_, "wound_materialization_invalid_field" or "wound_materialization_consequence_slot_invalid" or
            "wound_materialization_effect_binding_invalid" or "wound_materialization_owned_source_graph_invalid") =>
            "replace only this invalid value with a complete legal current-version route/path value",
        _ => null
    };

    private static JsonNode? SanitizeAlternative(JsonNode? node, IReadOnlySet<string> secrets)
    {
        if (node is JsonObject obj)
        {
            var result = new JsonObject();
            foreach (var pair in obj)
                if (!SensitiveKeys.Contains(pair.Key) && !UnsafeEvidence(pair.Key, secrets) &&
                    !(pair.Value is JsonValue value && value.TryGetValue<string>(out var text) && UnsafeEvidence(text, secrets)))
                    result[pair.Key] = SanitizeAlternative(pair.Value, secrets);
            return result;
        }
        if (node is JsonArray array)
            return new JsonArray(array.Select(value => SanitizeAlternative(value, secrets)).ToArray());
        if (node is JsonValue scalar && scalar.TryGetValue<string>(out var stringValue) && UnsafeEvidence(stringValue, secrets))
            return null;
        return node?.DeepClone();
    }

    internal static bool ContainsAlternativePrivateData(JsonNode? node, IReadOnlySet<string> secrets) => node switch
    {
        JsonObject obj => obj.Any(pair => SensitiveKeys.Contains(pair.Key) || UnsafeEvidence(pair.Key, secrets) ||
            ContainsAlternativePrivateData(pair.Value, secrets)),
        JsonArray array => array.Any(value => ContainsAlternativePrivateData(value, secrets)),
        JsonValue value when value.TryGetValue<string>(out var text) => UnsafeEvidence(text, secrets),
        _ => false
    };

    internal static void MaskAlternativePath(JsonObject payload, string path)
    {
        if (!TryParsePath(path, out var segments))
            throw new InvalidOperationException("An alternative mask requires a validated semantic path.");
        JsonNode? current = payload;
        for (var index = 0; index < segments.Count - 1; index++)
        {
            current = (current, segments[index]) switch
            {
                (JsonObject obj, string field) => obj[field],
                (JsonArray array, int ordinal) when ordinal < array.Count => array[ordinal],
                _ => null
            };
            if (current is null)
                return;
        }
        switch (current, segments[^1])
        {
            case (JsonObject obj, string field): obj.Remove(field); break;
            case (JsonArray array, int ordinal) when ordinal < array.Count: array[ordinal] = null; break;
        }
    }
}
