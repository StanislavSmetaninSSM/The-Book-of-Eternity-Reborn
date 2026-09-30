using System.Collections.ObjectModel;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal static class MortalWoundTreatmentSceneAuthorityContract
{
    internal const string FacilityKind = "mortal_wound_treatment_facility";
    internal const string EnvironmentKind = "mortal_wound_treatment_environment";
    internal const string ConsentKind = "mortal_wound_treatment_consent";

    private static readonly HashSet<string> ActorKinds = new(StringComparer.Ordinal)
    {
        "player", "npc", "combatant", "combatant_member"
    };

    internal static bool IsRecognized(JsonElement row)
    {
        if (row.ValueKind != JsonValueKind.Object)
            return false;
        var kindKey = ExactIdentifierConfusableKey.Build("kind");
        return row.EnumerateObject().Any(property =>
            string.Equals(
                ExactIdentifierConfusableKey.Build(property.Name),
                kindKey,
                StringComparison.Ordinal) &&
            property.Value.ValueKind == JsonValueKind.String &&
            property.Value.GetString() is FacilityKind or EnvironmentKind or ConsentKind);
    }

    internal static MortalWoundTreatmentSceneAuthorityResult Parse(
        JsonElement customStates,
        string context)
    {
        var issues = new List<ValidationIssue>();
        var facilities = new List<MortalWoundTreatmentSceneFacility>();
        var environments = new List<MortalWoundTreatmentSceneEnvironment>();
        var consents = new List<MortalWoundTreatmentSceneConsent>();
        var identities = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            [FacilityKind] = new(StringComparer.Ordinal),
            [EnvironmentKind] = new(StringComparer.Ordinal),
            [ConsentKind] = new(StringComparer.Ordinal)
        };

        if (customStates.ValueKind != JsonValueKind.Array)
        {
            AddInvalid(issues, context, "array", customStates.ValueKind.ToString());
            return Result(issues, facilities, environments, consents);
        }

        var index = 0;
        foreach (var row in customStates.EnumerateArray())
        {
            var path = $"{context}[{index++}]";
            if (row.ValueKind != JsonValueKind.Object)
                continue;

            if (!IsRecognized(row))
                continue;

            var kindProperties = row.EnumerateObject()
                .Where(property => string.Equals(property.Name, "kind", StringComparison.Ordinal))
                .ToArray();
            if (kindProperties.Length != 1 || kindProperties[0].Value.ValueKind != JsonValueKind.String)
            {
                AddInvalid(issues, path + ".kind", "one exact reserved kind property", row.GetRawText());
                continue;
            }

            var kind = kindProperties[0].Value.GetString()!;

            var expectedFields = kind switch
            {
                FacilityKind => new[] { "kind", "schemaVersion", "facilityId", "displayName", "available" },
                EnvironmentKind => new[] { "kind", "schemaVersion", "environmentId", "displayName", "state" },
                _ => new[]
                {
                    "kind", "schemaVersion", "consentRef", "displayName", "providerKind",
                    "providerId", "targetKind", "targetId", "status"
                }
            };
            if (!HasExactClosedShape(row, expectedFields))
            {
                AddInvalid(issues, path, string.Join(',', expectedFields), row.GetRawText());
                continue;
            }

            if (!TryReadExactInt(row, "schemaVersion", out var schemaVersion) || schemaVersion != 1)
            {
                AddInvalid(issues, path + ".schemaVersion", "1", ReadRaw(row, "schemaVersion"));
                continue;
            }

            if (!TryReadDiagnosticText(row, "displayName", out var displayName))
            {
                AddInvalid(issues, path + ".displayName", "non-empty exact string", ReadRaw(row, "displayName"));
                continue;
            }

            string identity;
            switch (kind)
            {
                case FacilityKind:
                    if (!TryReadIdentifier(row, "facilityId", out identity) ||
                        !TryReadBoolean(row, "available", out var available))
                    {
                        AddInvalid(issues, path, "closed facility row", row.GetRawText());
                        continue;
                    }
                    if (!RegisterIdentity(kind, identity, path + ".facilityId", identities, issues))
                        continue;
                    facilities.Add(new(identity, displayName, available));
                    break;
                case EnvironmentKind:
                    if (!TryReadIdentifier(row, "environmentId", out identity) ||
                        !TryReadIdentifier(row, "state", out var state))
                    {
                        AddInvalid(issues, path, "closed environment row", row.GetRawText());
                        continue;
                    }
                    if (!RegisterIdentity(kind, identity, path + ".environmentId", identities, issues))
                        continue;
                    environments.Add(new(identity, displayName, state));
                    break;
                default:
                    if (!TryReadIdentifier(row, "consentRef", out identity) ||
                        !TryReadDiagnosticText(row, "providerKind", out var providerKind) ||
                        !TryReadIdentifier(row, "providerId", out var providerId) ||
                        !TryReadDiagnosticText(row, "targetKind", out var targetKind) ||
                        !TryReadIdentifier(row, "targetId", out var targetId) ||
                        !TryReadDiagnosticText(row, "status", out var status) ||
                        !ActorKinds.Contains(providerKind) ||
                        !ActorKinds.Contains(targetKind) ||
                        status is not ("granted" or "withdrawn"))
                    {
                        AddInvalid(issues, path, "closed consent row with recognized actor kinds/status", row.GetRawText());
                        continue;
                    }
                    if (!RegisterIdentity(kind, identity, path + ".consentRef", identities, issues))
                        continue;
                    consents.Add(new(
                        identity,
                        displayName,
                        providerKind,
                        providerId,
                        targetKind,
                        targetId,
                        status));
                    break;
            }
        }

        return Result(issues, facilities, environments, consents);
    }

    private static bool HasExactClosedShape(JsonElement row, IReadOnlyCollection<string> expected)
    {
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var confusable = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in row.EnumerateObject())
        {
            if (!exact.Add(property.Name) ||
                !confusable.Add(ExactIdentifierConfusableKey.Build(property.Name)) ||
                !expected.Contains(property.Name, StringComparer.Ordinal))
                return false;
        }
        return exact.Count == expected.Count;
    }

    private static bool RegisterIdentity(
        string kind,
        string identity,
        string path,
        IReadOnlyDictionary<string, HashSet<string>> identities,
        ICollection<ValidationIssue> issues)
    {
        if (identities[kind].Add(ExactIdentifierConfusableKey.Build(identity)))
            return true;
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Mortal wound treatment scene identifiers must be exact/confusable-unique within their kind.",
            code: "mortal_wound_treatment_scene_state_ambiguous",
            section: "mortal_location_materialization",
            expected: "one exact/confusable-unique identifier",
            actual: identity));
        return false;
    }

    private static bool TryReadDiagnosticText(JsonElement row, string name, out string value)
    {
        value = string.Empty;
        return row.TryGetProperty(name, out var property) &&
               property.ValueKind == JsonValueKind.String &&
               !string.IsNullOrWhiteSpace(value = property.GetString()!) &&
               string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }

    private static bool TryReadIdentifier(JsonElement row, string name, out string value) =>
        TryReadDiagnosticText(row, name, out value) &&
        ResourceMaterializationContract.IsExactIdentifier(value);

    private static bool TryReadExactInt(JsonElement row, string name, out int value)
    {
        value = 0;
        return row.TryGetProperty(name, out var property) && property.TryGetInt32(out value);
    }

    private static bool TryReadBoolean(JsonElement row, string name, out bool value)
    {
        value = false;
        if (!row.TryGetProperty(name, out var property) ||
            property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            return false;
        value = property.GetBoolean();
        return true;
    }

    private static string ReadRaw(JsonElement row, string name) =>
        row.TryGetProperty(name, out var property) ? property.GetRawText() : "missing";

    private static void AddInvalid(
        ICollection<ValidationIssue> issues,
        string path,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Recognized Mortal wound treatment scene state is malformed.",
            code: "mortal_wound_treatment_scene_state_invalid",
            section: "mortal_location_materialization",
            expected: expected,
            actual: actual));

    private static MortalWoundTreatmentSceneAuthorityResult Result(
        List<ValidationIssue> issues,
        List<MortalWoundTreatmentSceneFacility> facilities,
        List<MortalWoundTreatmentSceneEnvironment> environments,
        List<MortalWoundTreatmentSceneConsent> consents) => new(
            issues.Count == 0,
            Array.AsReadOnly(issues.ToArray()),
            Array.AsReadOnly(facilities.ToArray()),
            Array.AsReadOnly(environments.ToArray()),
            Array.AsReadOnly(consents.ToArray()));
}

internal sealed record MortalWoundTreatmentSceneAuthorityResult(
    bool IsValid,
    ReadOnlyCollection<ValidationIssue> Issues,
    ReadOnlyCollection<MortalWoundTreatmentSceneFacility> Facilities,
    ReadOnlyCollection<MortalWoundTreatmentSceneEnvironment> Environments,
    ReadOnlyCollection<MortalWoundTreatmentSceneConsent> Consents);

internal sealed record MortalWoundTreatmentSceneFacility(
    string FacilityId,
    string DisplayName,
    bool Available);

internal sealed record MortalWoundTreatmentSceneEnvironment(
    string EnvironmentId,
    string DisplayName,
    string State);

internal sealed record MortalWoundTreatmentSceneConsent(
    string ConsentRef,
    string DisplayName,
    string ProviderKind,
    string ProviderId,
    string TargetKind,
    string TargetId,
    string Status);
