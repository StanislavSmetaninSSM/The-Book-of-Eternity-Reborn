using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal enum MortalWoundDeteriorationPolicyClassification
{
    StrictlyWorsening,
    Neutral,
    Beneficial
}

internal enum MortalWoundDeteriorationResultKind
{
    IncreaseSeverity,
    AddComplication,
    DeathContour,
    NoChange,
    AddRecovery
}

internal sealed record MortalWoundDeteriorationPolicyDefinition(
    string PolicyRef,
    string ConditionKey,
    long GraceMinutes,
    long CadenceMinutes,
    MortalWoundDeteriorationResultKind ResultKind,
    MortalWoundDeteriorationPolicyClassification Classification,
    JsonElement Result,
    int AdditionalDefinitions,
    int AdditionalRoots,
    int AdditionalConsequenceSlots,
    string CanonicalProjection);

internal sealed record MortalWoundDeteriorationPolicyParseResult(
    bool IsValid,
    IReadOnlyList<ValidationIssue> Issues,
    MortalWoundDeteriorationPolicyDefinition? Policy);

/// <summary>
/// Pure closed-schema parser for one GM-authored Mortal deterioration policy. It
/// recognizes the two explicit non-adverse sentinels so the authority layer can
/// diagnose their semantics precisely, but it never turns them into authority.
/// </summary>
internal static class MortalWoundDeteriorationPolicyContract
{
    private static readonly IReadOnlySet<string> PolicyFields = Set(
        "policyRef",
        "unmetConditions",
        "graceMinutes",
        "cadenceMinutes",
        "result");
    private static readonly IReadOnlySet<string> ScalarResultFields = Set("kind");
    private static readonly IReadOnlySet<string> ComplicationResultFields = Set(
        "kind",
        "complicationDraft");

    internal static MortalWoundDeteriorationPolicyParseResult Parse(
        JsonElement value,
        string path,
        string realm,
        string ownerTargetKind,
        int severityRank)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(realm);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerTargetKind);
        var issues = new List<ValidationIssue>();
        if (value.ValueKind != JsonValueKind.Object)
        {
            Add(issues, path, "closed deterioration policy object", value.ValueKind.ToString());
            return Failure(issues);
        }

        ValidateExactFields(value, path, PolicyFields, issues);
        var policyRef = ReadExactIdentifier(value, "policyRef", path, issues);
        var conditionKey = ReadSingletonCondition(value, path, issues);
        var graceMinutes = ReadInt64(value, "graceMinutes", path, 0, long.MaxValue, issues);
        var cadenceMinutes = ReadInt64(value, "cadenceMinutes", path, 1, long.MaxValue, issues);

        MortalWoundDeteriorationResultKind? resultKind = null;
        MortalWoundDeteriorationPolicyClassification? classification = null;
        var additionalSlots = 0;
        var additionalDefinitions = 0;
        var additionalRoots = 0;
        JsonElement result = default;
        var resultPath = path + ".result";
        if (!value.TryGetProperty("result", out result) ||
            result.ValueKind != JsonValueKind.Object)
        {
            Add(
                issues,
                resultPath,
                "one closed deterioration result object",
                value.TryGetProperty("result", out var actualResult)
                    ? actualResult.ValueKind.ToString()
                    : "missing");
        }
        else
        {
            var kind = ReadString(result, "kind");
            switch (kind)
            {
                case "increase_severity":
                    ValidateExactFields(result, resultPath, ScalarResultFields, issues);
                    resultKind = MortalWoundDeteriorationResultKind.IncreaseSeverity;
                    classification = MortalWoundDeteriorationPolicyClassification.StrictlyWorsening;
                    break;
                case "death_contour":
                    ValidateExactFields(result, resultPath, ScalarResultFields, issues);
                    resultKind = MortalWoundDeteriorationResultKind.DeathContour;
                    classification = MortalWoundDeteriorationPolicyClassification.StrictlyWorsening;
                    break;
                case "no_change":
                    ValidateExactFields(result, resultPath, ScalarResultFields, issues);
                    resultKind = MortalWoundDeteriorationResultKind.NoChange;
                    classification = MortalWoundDeteriorationPolicyClassification.Neutral;
                    break;
                case "add_recovery":
                    ValidateExactFields(result, resultPath, ScalarResultFields, issues);
                    resultKind = MortalWoundDeteriorationResultKind.AddRecovery;
                    classification = MortalWoundDeteriorationPolicyClassification.Beneficial;
                    break;
                case "add_complication":
                    ValidateExactFields(result, resultPath, ComplicationResultFields, issues);
                    foreach (var issue in MortalWoundTreatmentContract
                                 .ValidateDeteriorationComplicationResult(
                                     result,
                                     resultPath,
                                     realm,
                                     ownerTargetKind,
                                     severityRank))
                    {
                        Add(
                            issues,
                            issue.FilePath,
                            issue.Expected ?? "complete deterioration complication draft",
                            issue.Actual ?? issue.Code ?? "invalid");
                    }
                    resultKind = MortalWoundDeteriorationResultKind.AddComplication;
                    classification = MortalWoundDeteriorationPolicyClassification.StrictlyWorsening;
                    (additionalDefinitions, additionalRoots, additionalSlots) =
                        CountDeclaredConsequenceGraph(result);
                    break;
                default:
                    Add(
                        issues,
                        resultPath + (result.TryGetProperty("kind", out _) ? ".kind" : string.Empty),
                        "increase_severity | add_complication | death_contour | no_change | add_recovery",
                        kind.Length == 0 ? "missing" : kind);
                    break;
            }
        }

        if (issues.Count != 0 ||
            resultKind is null ||
            classification is null ||
            policyRef.Length == 0 ||
            conditionKey.Length == 0)
        {
            return Failure(issues);
        }

        var canonical = WoundAcceptedTurnFingerprintWriter.CanonicalJson(
            JsonNode.Parse(value.GetRawText()))!;
        return new MortalWoundDeteriorationPolicyParseResult(
            true,
            Array.Empty<ValidationIssue>(),
            new MortalWoundDeteriorationPolicyDefinition(
                policyRef,
                conditionKey,
                graceMinutes,
                cadenceMinutes,
                resultKind.Value,
                classification.Value,
                result.Clone(),
                additionalDefinitions,
                additionalRoots,
                additionalSlots,
                canonical));
    }

    private static (int Definitions, int Roots, int Slots)
        CountDeclaredConsequenceGraph(JsonElement result)
    {
        if (!result.TryGetProperty("complicationDraft", out var draft) ||
            !draft.TryGetProperty("consequenceDefinitions", out var definitions) ||
            definitions.ValueKind != JsonValueKind.Array)
        {
            return (0, 0, 0);
        }

        var definitionCount = definitions.GetArrayLength();
        var rootCount = 0;
        var total = 0;
        foreach (var wrapper in definitions.EnumerateArray())
        {
            if (!wrapper.TryGetProperty("root", out var root) ||
                root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("slots", out var slots) ||
                slots.ValueKind != JsonValueKind.Array)
            {
                continue;
            }
            rootCount++;
            total = checked(total + slots.GetArrayLength());
        }
        return (definitionCount, rootCount, total);
    }

    private static string ReadSingletonCondition(
        JsonElement value,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var fieldPath = path + ".unmetConditions";
        if (!value.TryGetProperty("unmetConditions", out var conditions) ||
            conditions.ValueKind != JsonValueKind.Array)
        {
            Add(
                issues,
                fieldPath,
                "an array containing exactly one condition identifier",
                value.TryGetProperty("unmetConditions", out var actual)
                    ? actual.ValueKind.ToString()
                    : "missing");
            return string.Empty;
        }
        if (conditions.GetArrayLength() != 1)
        {
            Add(
                issues,
                fieldPath,
                "exactly one condition identifier",
                conditions.GetArrayLength().ToString());
            return string.Empty;
        }
        var condition = conditions[0];
        if (condition.ValueKind != JsonValueKind.String ||
            !ResourceMaterializationContract.IsExactIdentifier(condition.GetString()))
        {
            Add(
                issues,
                fieldPath + "[0]",
                "one exact condition identifier",
                condition.GetRawText());
            return string.Empty;
        }
        return condition.GetString()!;
    }

    private static string ReadExactIdentifier(
        JsonElement value,
        string name,
        string path,
        ICollection<ValidationIssue> issues)
    {
        if (value.TryGetProperty(name, out var field) &&
            field.ValueKind == JsonValueKind.String &&
            ResourceMaterializationContract.IsExactIdentifier(field.GetString()))
        {
            return field.GetString()!;
        }
        Add(
            issues,
            path + "." + name,
            "one exact identifier",
            value.TryGetProperty(name, out var actual) ? actual.GetRawText() : "missing");
        return string.Empty;
    }

    private static long ReadInt64(
        JsonElement value,
        string name,
        string path,
        long minimum,
        long maximum,
        ICollection<ValidationIssue> issues)
    {
        if (value.TryGetProperty(name, out var field) &&
            field.ValueKind == JsonValueKind.Number &&
            field.TryGetInt64(out var parsed) &&
            parsed >= minimum &&
            parsed <= maximum)
        {
            return parsed;
        }
        Add(
            issues,
            path + "." + name,
            $"signed 64-bit integer in [{minimum}, {maximum}]",
            value.TryGetProperty(name, out var actual) ? actual.GetRawText() : "missing");
        return 0;
    }

    private static string ReadString(JsonElement value, string name) =>
        value.TryGetProperty(name, out var field) &&
        field.ValueKind == JsonValueKind.String
            ? field.GetString() ?? string.Empty
            : string.Empty;

    private static void ValidateExactFields(
        JsonElement value,
        string path,
        IReadOnlySet<string> expected,
        ICollection<ValidationIssue> issues)
    {
        var actual = value.EnumerateObject()
            .Select(static property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var missing in expected.Except(actual, StringComparer.Ordinal))
            Add(issues, path + "." + missing, "required policy field", "missing");
        foreach (var extra in actual.Except(expected, StringComparer.Ordinal))
            Add(issues, path + "." + extra, "closed policy fields", "unexpected field");
    }

    private static MortalWoundDeteriorationPolicyParseResult Failure(
        IReadOnlyList<ValidationIssue> issues) =>
        new(false, issues.ToArray(), null);

    private static void Add(
        ICollection<ValidationIssue> issues,
        string path,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Mortal wound deterioration policy violates its closed version-1 contract.",
            code: "mortal_wound_deterioration_policy_invalid",
            actor: "Client",
            section: "wound_materialization",
            expected: expected,
            actual: actual,
            repairHint:
            "Resubmit exactly one closed Mortal deterioration policy with one condition, bounded canonical minutes, and one typed result."));

    private static IReadOnlySet<string> Set(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);
}
