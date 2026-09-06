using System.Collections.Immutable;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal static partial class WoundResponseInputComposer
{
    private static readonly IReadOnlySet<string> AlternativeResponseFields = Set(
        "authoringRequestRef", "decision", "route", "diagnosisPath");

    internal static WoundAlternativeTreatmentResponseParseResult ParseAlternativeTreatmentAuthoring(
        JsonElement entry,
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var issues = new List<ValidationIssue>();
        try
        {
            var objectValid = TryReadObject(
                entry,
                path,
                AlternativeResponseFields,
                AlternativeResponseFields,
                issues,
                out var fields);
            ValidateNoDuplicateProperties(entry, path, issues);
            if (!objectValid || issues.Count != 0)
                return AlternativeResponseResult(issues);

            var reference = ReadString(fields, "authoringRequestRef", path, issues);
            if (reference is not null &&
                (!ResourceMaterializationContract.IsExactIdentifier(reference) ||
                 reference.Any(character => char.IsWhiteSpace(character) ||
                     character is '/' or '\\' or ':')))
            {
                Add(
                    issues,
                    path + ".authoringRequestRef",
                    "wound_response_invalid_field",
                    "safe opaque exact reference",
                    reference);
            }

            var decision = ReadString(fields, "decision", path, issues);
            GmTreatmentRouteDraft? route = null;
            MortalWoundDiagnosisPathDefinition? diagnosisPath = null;
            if (decision == "author")
            {
                var routeResult = MortalWoundTreatmentContract.ParseGmRouteDraftShape(
                    fields["route"],
                    path + ".route");
                issues.AddRange(routeResult.Issues);
                route = routeResult.Route;

                var pathElement = fields["diagnosisPath"];
                var visibleRoute = fields["route"].ValueKind == JsonValueKind.Object &&
                    fields["route"].TryGetProperty("visibility", out var visibility) &&
                    visibility.ValueKind == JsonValueKind.String &&
                    visibility.GetString() is "public" or "known_to_player";
                if (visibleRoute && pathElement.ValueKind != JsonValueKind.Null)
                {
                    Add(issues, path + ".diagnosisPath", "wound_response_invalid_field",
                        "explicit null for a visible route", pathElement.GetRawText());
                }
                else if (pathElement.ValueKind == JsonValueKind.Object)
                {
                    var pathResult = MortalWoundTreatmentContract.ParseDiagnosisPathShape(
                        pathElement,
                        path + ".diagnosisPath");
                    issues.AddRange(pathResult.Issues);
                    diagnosisPath = pathResult.DiagnosisPath;
                }
                else if (pathElement.ValueKind != JsonValueKind.Null)
                {
                    Add(
                        issues,
                        path + ".diagnosisPath",
                        "wound_response_invalid_field",
                        "complete diagnosis path or explicit null",
                        pathElement.GetRawText());
                }

                if (!visibleRoute && route is not null &&
                    !HasAlternativeTreatmentMemberPair(
                        route.RouteId,
                        route.Visibility,
                        diagnosisPath))
                {
                    Add(
                        issues,
                        path + ".diagnosisPath",
                        "wound_response_invalid_field",
                        "one non-GM-only revealing path only for a hidden route",
                        pathElement.GetRawText());
                }
            }
            else if (decision == "decline")
            {
                foreach (var name in new[] { "route", "diagnosisPath" })
                {
                    if (fields[name].ValueKind != JsonValueKind.Null)
                    {
                        Add(
                            issues,
                            path + "." + name,
                            "wound_response_invalid_field",
                            "explicit null for decline",
                            fields[name].GetRawText());
                    }
                }
            }
            else
            {
                Add(
                    issues,
                    path + ".decision",
                    "wound_response_invalid_field",
                    "author or decline",
                    fields["decision"].GetRawText());
            }

            return AlternativeResponseResult(
                issues,
                issues.Count == 0
                    ? new[]
                    {
                        new WoundAlternativeTreatmentResponseDraft(
                            reference!, decision!, route, diagnosisPath)
                    }
                    : Array.Empty<WoundAlternativeTreatmentResponseDraft>());
        }
        catch (Exception exception) when (exception is JsonException or
                                          InvalidOperationException or
                                          FormatException or
                                          OverflowException)
        {
            Add(
                issues,
                path,
                "wound_response_invalid_field",
                "one complete alternative treatment response",
                exception.GetType().Name);
            return AlternativeResponseResult(issues);
        }
    }

    internal static WoundAlternativeTreatmentResponseParseResult ParseAlternativeTreatmentAuthorings(
        JsonElement entries,
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var issues = new List<ValidationIssue>();
        var drafts = new List<WoundAlternativeTreatmentResponseDraft>();
        if (entries.ValueKind != JsonValueKind.Array ||
            entries.GetArrayLength() > MaximumDecisions)
        {
            Add(
                issues,
                path,
                "wound_response_invalid_field",
                $"array of 0–{MaximumDecisions} authorings",
                entries.ValueKind == JsonValueKind.Array
                    ? entries.GetArrayLength().ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : entries.ValueKind.ToString());
            return AlternativeResponseResult(issues);
        }

        var exact = new HashSet<string>(StringComparer.Ordinal);
        var confusable = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var entry in entries.EnumerateArray())
        {
            var itemPath = $"{path}[{index++}]";
            var parsed = ParseAlternativeTreatmentAuthoring(entry, itemPath);
            issues.AddRange(parsed.Issues);
            if (!parsed.IsValid)
                continue;
            var draft = parsed.Drafts.Single();
            if (!exact.Add(draft.AuthoringRequestRef) ||
                !confusable.Add(ExactIdentifierConfusableKey.Build(draft.AuthoringRequestRef)))
            {
                Add(
                    issues,
                    itemPath + ".authoringRequestRef",
                    "wound_response_duplicate_reference",
                    "exact and confusable-unique authoring request reference",
                    draft.AuthoringRequestRef);
            }
            drafts.Add(draft);
        }
        return AlternativeResponseResult(issues, drafts);
    }

    private static bool HasAlternativeTreatmentMemberPair(
        string routeId,
        string visibility,
        MortalWoundDiagnosisPathDefinition? path) =>
        visibility == "hidden"
            ? path is not null &&
              path.Visibility != "gm_only" &&
              path.Reveals.Any(fact => fact.CanonicalValue == "route:" + routeId)
            : path is null;

    private static WoundAlternativeTreatmentResponseParseResult AlternativeResponseResult(
        IReadOnlyCollection<ValidationIssue> issues,
        IEnumerable<WoundAlternativeTreatmentResponseDraft>? drafts = null)
    {
        var resultIssues = issues.Select(issue => new ValidationIssue(
            issue.FilePath,
            issue.Severity,
            "The alternative treatment response violates the complete local authoring contract.",
            code: issue.Code,
            actor: issue.Actor,
            section: "wound_treatment_authorings",
            expected: issue.Expected,
            actual: issue.Actual,
            repairHint:
                "Keep the offered authoring request reference and correct only the indicated route/path fields.",
            category: issue.Category)).ToImmutableArray();
        return new WoundAlternativeTreatmentResponseParseResult(
            resultIssues.IsEmpty,
            resultIssues,
            resultIssues.IsEmpty
                ? (drafts ?? Array.Empty<WoundAlternativeTreatmentResponseDraft>()).ToImmutableArray()
                : ImmutableArray<WoundAlternativeTreatmentResponseDraft>.Empty);
    }

    internal static void WriteAlternativeTreatmentAuthoringCanonical(
        Utf8JsonWriter writer,
        WoundAlternativeTreatmentResponseDraft draft)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(draft);
        using var stream = new MemoryStream();
        using (var buffer = new Utf8JsonWriter(stream))
        {
            buffer.WriteStartObject();
            buffer.WriteString("authoringRequestRef", draft.AuthoringRequestRef);
            buffer.WriteString("decision", draft.Decision);
            buffer.WritePropertyName("route");
            if (draft.Route is null)
                buffer.WriteNullValue();
            else
                MortalWoundTreatmentContract.WriteGmRouteDraft(buffer, draft.Route);
            buffer.WritePropertyName("diagnosisPath");
            if (draft.DiagnosisPath is null)
                buffer.WriteNullValue();
            else
                MortalWoundTreatmentContract.WriteDiagnosisPathCanonical(buffer, draft.DiagnosisPath);
            buffer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(stream.ToArray());
        var verified = ParseAlternativeTreatmentAuthoring(
            document.RootElement,
            "woundTreatmentAuthorings[0]");
        if (!verified.IsValid)
        {
            throw new InvalidOperationException(
                "Cannot write an invalid alternative response: " +
                string.Join("; ", verified.Issues.Select(issue => issue.Code)));
        }
        document.RootElement.WriteTo(writer);
    }
}
