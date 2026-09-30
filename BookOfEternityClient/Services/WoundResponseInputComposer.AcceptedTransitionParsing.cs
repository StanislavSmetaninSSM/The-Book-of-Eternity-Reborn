using System.Text.Json;

namespace BookOfEternityClient.Services;

internal static partial class WoundResponseInputComposer
{
    private static readonly IReadOnlySet<string> AcceptedTransitionFields = Set(
        "kind", "commandRef", "transitionKind", "operationKey", "authority", "result", "finalSceneText");
    private static readonly IReadOnlySet<string> DiagnosisAuthorityFields = Set(
        "commandRef", "operationKey", "attemptId", "woundId", "diagnosisPathId",
        "expectedBeforeFingerprint", "pathFingerprint", "requirementAuthorityFingerprint",
        "checkResultFingerprint", "authorityFingerprint");
    private static readonly IReadOnlySet<string> DiagnosisCommandResultFields = Set(
        "result", "revealedFacts", "resultFingerprint");
    private static readonly IReadOnlySet<string> AlternativeAuthorityFields = Set(
        "authoringRequestRef", "requestAuthorityFingerprint", "operationKey", "woundId", "eventRef",
        "addedRouteId", "addedDiagnosisPathId", "expectedBeforeFingerprint", "expectedAfterFingerprint",
        "routeFingerprint", "diagnosisPathFingerprint", "evidenceAuthorityFingerprint",
        "requirementAuthorityFingerprint", "authorityFingerprint");
    private static readonly IReadOnlySet<string> AlternativeCommandResultFields = Set(
        "decision", "route", "diagnosisPath", "resultFingerprint");

    private static bool IsAcceptedTransitionCommand(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty("kind", out var kind) &&
        kind.ValueKind == JsonValueKind.String && kind.GetString() == "accepted_transition";

    private static WoundAcceptedTransitionCommandDraft? ParseAcceptedTransition(
        JsonElement element, int index, ICollection<ValidationIssue> issues)
    {
        var path = $"{AcceptedMechanicsPlan.WoundCommandPath}.commands[{index}]";
        var start = issues.Count;
        if (!TryReadFields(element, AcceptedTransitionFields, AcceptedTransitionFields, path, issues, out var fields))
            return null;
        var commandRef = ReadAcceptedReference(fields, "commandRef", path, issues);
        var operationKey = ReadExactIdentifier(fields, "operationKey", path, issues);
        var kind = ReadStringField(fields, "transitionKind", path, issues);
        string? scene = null;
        if (!fields.TryGetValue("finalSceneText", out var sceneElement) ||
            sceneElement.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
            InvalidAcceptedField(issues, path + ".finalSceneText", "string or null", Raw(fields, "finalSceneText"));
        else if (sceneElement.ValueKind == JsonValueKind.String)
            scene = sceneElement.GetString();
        if (kind is not ("diagnose" or "author_alternative_treatment"))
        {
            InvalidAcceptedField(issues, path + ".transitionKind", "diagnose or author_alternative_treatment", kind ?? "missing");
            return null;
        }
        fields.TryGetValue("authority", out var authorityElement);
        fields.TryGetValue("result", out var resultElement);
        var authorityFields = kind == "diagnose" ? DiagnosisAuthorityFields : AlternativeAuthorityFields;
        var resultFields = kind == "diagnose" ? DiagnosisCommandResultFields : AlternativeCommandResultFields;
        if (!TryReadFields(authorityElement, authorityFields, authorityFields, path + ".authority", issues, out var authority) |
            !TryReadFields(resultElement, resultFields, resultFields, path + ".result", issues, out var result))
            return null;
        var authorityPath = path + ".authority";
        var resultPath = path + ".result";
        var repeatedOperation = ReadExactIdentifier(authority, "operationKey", authorityPath, issues);
        MatchAcceptedField(operationKey, repeatedOperation, authorityPath + ".operationKey", issues);
        var woundId = ReadExactIdentifier(authority, "woundId", authorityPath, issues);
        var before = ReadAcceptedFingerprint(authority, "expectedBeforeFingerprint", authorityPath, issues);
        var requirement = ReadAcceptedFingerprint(authority, "requirementAuthorityFingerprint", authorityPath, issues);
        var seal = ReadAcceptedFingerprint(authority, "authorityFingerprint", authorityPath, issues);
        var resultSeal = ReadAcceptedFingerprint(result, "resultFingerprint", resultPath, issues);
        WoundAcceptedTransitionCommandDraft draft;
        if (kind == "diagnose")
        {
            var repeatedRef = ReadAcceptedReference(authority, "commandRef", authorityPath, issues);
            MatchAcceptedField(commandRef, repeatedRef, authorityPath + ".commandRef", issues);
            var attempt = ReadExactIdentifier(authority, "attemptId", authorityPath, issues);
            var pathId = ReadExactIdentifier(authority, "diagnosisPathId", authorityPath, issues);
            var pathSeal = ReadAcceptedFingerprint(authority, "pathFingerprint", authorityPath, issues);
            var check = ReadAcceptedFingerprint(authority, "checkResultFingerprint", authorityPath, issues);
            var outcome = ReadStringField(result, "result", resultPath, issues);
            if (outcome is not ("success" or "failure"))
                InvalidAcceptedField(issues, resultPath + ".result", "success or failure", outcome ?? "missing");
            var facts = ReadAcceptedDiagnosisFacts(result, resultPath, issues);
            if (outcome == "success" && facts.Count == 0 || outcome == "failure" && facts.Count != 0)
                InvalidAcceptedField(issues, resultPath + ".revealedFacts", "complete nonempty facts for success; none for failure", Raw(result, "revealedFacts"));
            draft = new WoundDiagnosisCommandDraft(commandRef!, operationKey!, scene,
                new WoundDiagnosisCommandAuthority(repeatedRef!, repeatedOperation!, attempt!, woundId!,
                    pathId!, before!, pathSeal!, requirement!, check!, seal!),
                new WoundDiagnosisTransitionResult(pathId!, outcome!, facts, resultSeal!));
        }
        else
        {
            var repeatedRef = ReadAcceptedReference(authority, "authoringRequestRef", authorityPath, issues);
            MatchAcceptedField(commandRef, repeatedRef, authorityPath + ".authoringRequestRef", issues);
            var requestSeal = ReadAcceptedFingerprint(authority, "requestAuthorityFingerprint", authorityPath, issues);
            var eventRef = ReadExactIdentifier(authority, "eventRef", authorityPath, issues);
            var routeId = ReadExactIdentifier(authority, "addedRouteId", authorityPath, issues);
            var pathId = ReadAcceptedNullable(authority, "addedDiagnosisPathId", authorityPath, issues, fingerprint: false);
            var after = ReadAcceptedFingerprint(authority, "expectedAfterFingerprint", authorityPath, issues);
            var routeSeal = ReadAcceptedFingerprint(authority, "routeFingerprint", authorityPath, issues);
            var pathSeal = ReadAcceptedNullable(authority, "diagnosisPathFingerprint", authorityPath, issues, fingerprint: true);
            var evidence = ReadAcceptedFingerprint(authority, "evidenceAuthorityFingerprint", authorityPath, issues);
            if (!TryReadString(result, "decision", out var decision) || decision != "author")
                InvalidAcceptedField(issues, resultPath + ".decision", "author (decline produces no command)", Raw(result, "decision"));
            result.TryGetValue("route", out var routeElement);
            var routeParsed = MortalWoundTreatmentContract.ParseRouteShape(routeElement, resultPath + ".route");
            foreach (var issue in routeParsed.Issues) issues.Add(issue);
            var route = routeParsed.Route;
            MortalWoundDiagnosisPathDefinition? diagnosisPath = null;
            if (!result.TryGetValue("diagnosisPath", out var pathElement) ||
                pathElement.ValueKind is not (JsonValueKind.Object or JsonValueKind.Null))
                InvalidAcceptedField(issues, resultPath + ".diagnosisPath", "complete path or explicit null", Raw(result, "diagnosisPath"));
            else if (pathElement.ValueKind == JsonValueKind.Object)
            {
                var pathParsed = MortalWoundTreatmentContract.ParseDiagnosisPathShape(pathElement, resultPath + ".diagnosisPath");
                foreach (var issue in pathParsed.Issues) issues.Add(issue);
                diagnosisPath = pathParsed.DiagnosisPath;
            }
            if (route is not null)
            {
                MatchAcceptedField(route.RouteId, routeId, authorityPath + ".addedRouteId", issues);
                if (!HasAlternativeTreatmentMemberPair(
                        route.RouteId,
                        route.Visibility,
                        diagnosisPath))
                    InvalidAcceptedField(issues, resultPath + ".diagnosisPath", "one non-GM-only revealing path only for a hidden route", Raw(result, "diagnosisPath"));
            }
            MatchAcceptedField(diagnosisPath?.DiagnosisPathId, pathId, authorityPath + ".addedDiagnosisPathId", issues);
            if ((pathId is null) != (pathSeal is null))
                InvalidAcceptedField(issues, authorityPath + ".diagnosisPathFingerprint", "path ID and seal both null or both present", Raw(authority, "diagnosisPathFingerprint"));
            if (route is null) return null;
            draft = new WoundAlternativeTreatmentCommandDraft(commandRef!, operationKey!, scene,
                new WoundAlternativeTreatmentCommandAuthority(repeatedRef!, requestSeal!, repeatedOperation!,
                    woundId!, eventRef!, routeId!, pathId, before!, after!, routeSeal!, pathSeal, evidence!, requirement!, seal!),
                route, diagnosisPath, new WoundAlternativeTreatmentTransitionResult(repeatedRef!, routeId!,
                    pathId, routeSeal!, pathSeal, resultSeal!));
        }
        return issues.Count == start ? draft : null;
    }

    private static IReadOnlyList<string> ReadAcceptedDiagnosisFacts(
        IReadOnlyDictionary<string, JsonElement> fields, string path, ICollection<ValidationIssue> issues)
    {
        var facts = new List<string>();
        if (!fields.TryGetValue("revealedFacts", out var values) || values.ValueKind != JsonValueKind.Array ||
            values.GetArrayLength() > 16)
        {
            InvalidAcceptedField(issues, path + ".revealedFacts", "at most 16 exact typed facts", Raw(fields, "revealedFacts"));
            return facts;
        }
        var index = 0;
        foreach (var value in values.EnumerateArray())
        {
            var fact = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            var colon = fact?.IndexOf(':') ?? -1;
            if (colon < 0 || fact![..colon] is not ("route" or "complication") ||
                !ResourceMaterializationContract.IsExactIdentifier(fact[(colon + 1)..]))
                InvalidAcceptedField(issues, $"{path}.revealedFacts[{index}]", "route:<id> or complication:<id>", value.GetRawText());
            else
                facts.Add(fact!);
            index++;
        }
        if (!ExactAndConfusableUnique(facts))
            InvalidAcceptedField(issues, path + ".revealedFacts", "exact/confusable-unique facts", values.GetRawText());
        return facts;
    }

    private static string? ReadAcceptedReference(IReadOnlyDictionary<string, JsonElement> fields,
        string name, string path, ICollection<ValidationIssue> issues)
    {
        var value = ReadExactIdentifier(fields, name, path, issues);
        if (value is not null && !value.Any(character => char.IsWhiteSpace(character) || character is '/' or '\\' or ':'))
            return value;
        InvalidAcceptedField(issues, path + "." + name, "safe opaque exact reference", Raw(fields, name));
        return null;
    }

    private static string? ReadAcceptedFingerprint(IReadOnlyDictionary<string, JsonElement> fields,
        string name, string path, ICollection<ValidationIssue> issues)
    {
        if (TryReadString(fields, name, out var value) && ResourceMaterializationContract.IsAuthorityFingerprint(value))
            return value;
        InvalidAcceptedField(issues, path + "." + name, "lowercase sha256 plus 64 lowercase hex digits", Raw(fields, name));
        return null;
    }

    private static string? ReadAcceptedNullable(IReadOnlyDictionary<string, JsonElement> fields,
        string name, string path, ICollection<ValidationIssue> issues, bool fingerprint) =>
        fields.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.Null ? null :
        fingerprint ? ReadAcceptedFingerprint(fields, name, path, issues) : ReadExactIdentifier(fields, name, path, issues);

    private static void MatchAcceptedField(string? expected, string? actual, string path, ICollection<ValidationIssue> issues)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
            InvalidAcceptedField(issues, path, expected ?? "null", actual ?? "null");
    }

    private static void InvalidAcceptedField(ICollection<ValidationIssue> issues, string path, string expected, string actual) =>
        AddCommandIssue(issues, path, "wound_command_invalid_field", expected, actual);

    private static void FindCommandDuplicateProperties(JsonElement value, string path, ICollection<ValidationIssue> issues)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                var childPath = path + "." + property.Name;
                if (!seen.Add(property.Name))
                    AddCommandIssue(issues, childPath, "wound_command_duplicate_field", "one exact occurrence", property.Name);
                FindCommandDuplicateProperties(property.Value, childPath, issues);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var child in value.EnumerateArray())
                FindCommandDuplicateProperties(child, $"{path}[{index++}]", issues);
        }
    }
}
