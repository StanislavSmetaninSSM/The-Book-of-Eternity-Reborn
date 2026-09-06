using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class WoundRepairPacketBuilder
{
    private static bool IsValidPersistedAlternativePacket(IReadOnlyDictionary<string, JsonElement> fields)
    {
        var payload = fields["preservedProposal"];
        if (payload.ValueKind != JsonValueKind.Object ||
            payload.EnumerateObject().Any(property => property.Name is not ("route" or "diagnosisPath")) ||
            ContainsAlternativePrivateData(JsonNode.Parse(payload.GetRawText()), NoPrivateValues) ||
            ContainsAlternativePrivateData(JsonNode.Parse(fields["safeContext"].GetRawText()), NoPrivateValues) ||
            fields["issues"].ValueKind != JsonValueKind.Array || fields["issues"].GetArrayLength() == 0)
            return false;
        var issues = new List<WoundRepairPacketIssue>();
        var distinct = new HashSet<(string Path, string Code)>();
        foreach (var value in fields["issues"].EnumerateArray())
        {
            if (!TryReadPersistedClosedObject(value, PersistedIssueFields, out var issue) ||
                !TryReadPersistedString(issue, "path", out var path) ||
                !TryReadPersistedString(issue, "code", out var code) ||
                !TryReadPersistedString(issue, "expected", out var expected) ||
                !TryReadPersistedString(issue, "actual", out var actual) || actual.Length > EvidenceLimit ||
                !IsAlternativeDiagnosticPath(path, code) || AlternativeExpected(path, code) != expected ||
                UnsafeEvidence(actual, NoPrivateValues) || !distinct.Add((path, code)))
                return false;
            issues.Add(new(path, code, expected, actual));
        }
        if (issues.Any(issue => issues.Any(other => IsStrictAlternativeDescendant(issue.Path, other.Path))))
            return false;
        if (!HasOnlyAlternativeMaskHoles(payload, string.Empty, issues))
            return false;
        foreach (var issue in issues)
        {
            var exists = TryResolveAlternativePersistedPath(payload, issue.Path, out var value);
            if (issue.Code == "wound_alternative_required_route_reveal_missing")
            {
                if (!exists || value.ValueKind != JsonValueKind.Array ||
                    value.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
                    return false;
            }
            else if (exists && (value.ValueKind != JsonValueKind.Null || !issue.Path.EndsWith(']')))
                return false;
        }
        foreach (var root in new[] { "route", "diagnosisPath" })
        {
            if (!payload.TryGetProperty(root, out var member))
            {
                if (!issues.Any(issue => issue.Path == root)) return false;
            }
            else if (member.ValueKind != JsonValueKind.Object &&
                     !(root == "diagnosisPath" && member.ValueKind == JsonValueKind.Null))
                return false;
        }
        if (!TryReadPersistedClosedObject(fields["requiredResponseShape"],
                PersistedFieldSet("woundTreatmentAuthorings", "response"), out var shape) ||
            !TryReadPersistedString(shape, "response", out var response) || response != "complete accepted scene" ||
            shape["woundTreatmentAuthorings"].ValueKind != JsonValueKind.Array ||
            shape["woundTreatmentAuthorings"].GetArrayLength() != 1 ||
            !TryReadPersistedClosedObject(shape["woundTreatmentAuthorings"][0],
                PersistedFieldSet("authoringRequestRef", "decision", "route", "diagnosisPath"), out var author) ||
            !TryReadPersistedString(author, "authoringRequestRef", out var reference) || !Exact(reference) ||
            reference.Any(character => char.IsWhiteSpace(character) || character is '/' or '\\' or ':') ||
            UnsafeEvidence(reference, NoPrivateValues) ||
            !TryReadPersistedString(author, "decision", out var decision) || decision != "author")
            return false;
        foreach (var root in new[] { "route", "diagnosisPath" })
        {
            var paths = issues.Select(issue => issue.Path).Where(path => path == root || path.StartsWith(root + ".", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal).ToArray();
            var nullPath = root == "diagnosisPath" && paths.Length == 0 &&
                payload.TryGetProperty(root, out var member) && member.ValueKind == JsonValueKind.Null;
            if (nullPath)
            {
                if (author[root].ValueKind != JsonValueKind.Null) return false;
            }
            else if (!TryReadPersistedClosedObject(author[root], PersistedProposalRequirementFields, out var recipe) ||
                     !TryReadPersistedString(recipe, "base", out var baseline) || baseline != "preservedProposal." + root ||
                     recipe["correctOnly"].ValueKind != JsonValueKind.Array ||
                     recipe["correctOnly"].EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String) ||
                     !paths.SequenceEqual(recipe["correctOnly"].EnumerateArray().Select(item => item.GetString()), StringComparer.Ordinal))
                return false;
        }
        return true;
    }

    private static bool HasOnlyAlternativeMaskHoles(JsonElement value, string path,
        IReadOnlyList<WoundRepairPacketIssue> issues)
    {
        if (value.ValueKind == JsonValueKind.Object)
            return value.EnumerateObject().All(property => HasOnlyAlternativeMaskHoles(property.Value,
                path.Length == 0 ? property.Name : path + "." + property.Name, issues));
        if (value.ValueKind != JsonValueKind.Array) return true;
        return value.EnumerateArray().Select((item, index) => (item, path: $"{path}[{index}]"))
            .All(row => row.item.ValueKind == JsonValueKind.Null
                ? issues.Any(issue => issue.Path == row.path && issue.Code != "wound_alternative_required_route_reveal_missing")
                : HasOnlyAlternativeMaskHoles(row.item, row.path, issues));
    }

    private static bool TryResolveAlternativePersistedPath(JsonElement root, string path, out JsonElement value)
    {
        value = root;
        if (!TryParsePath(path, out var parts)) return false;
        foreach (var part in parts)
        {
            if (part is string name && value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var child))
                value = child;
            else if (part is int ordinal && value.ValueKind == JsonValueKind.Array && ordinal < value.GetArrayLength())
                value = value[ordinal];
            else return false;
        }
        return true;
    }
}
