using System.Text.Json;

namespace BookOfEternityClient.Services;

/// <summary>Raw admission for the optional, preselection QTE physical injury declaration.</summary>
internal static class QtePhysicalInjuryContract
{
    private static readonly HashSet<string> Fields = new(StringComparer.Ordinal)
    {
        "schemaVersion", "resourceCommandOrdinal", "maximumSeverityRank", "cause", "allowedLocationKinds"
    };

    private static readonly HashSet<string> LocationKinds = new(StringComparer.Ordinal)
    {
        "anatomical", "systemic", "mental", "spiritual_axis", "other"
    };

    internal static bool HasDeclaration(JsonElement root) =>
        DeclaredOutcomes(root, new List<ValidationIssue>()).Any();

    internal static IReadOnlyList<ValidationIssue> ValidateRawOffer(JsonElement root)
    {
        var issues = new List<ValidationIssue>();
        foreach (var (outcome, path) in DeclaredOutcomes(root, issues))
        {
            var injury = outcome.GetProperty("physicalInjury");
            if (injury.ValueKind != JsonValueKind.Object)
            {
                Add(issues, path, "closed non-null physicalInjury object", injury.ValueKind.ToString());
                continue;
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in injury.EnumerateObject())
            {
                if (!Fields.Contains(field.Name) ||
                    !names.Add(ResourceMaterializationContract.BuildConfusableKey(field.Name)))
                    Add(issues, path + "." + field.Name, "one exact registered declaration field", field.Name);
            }

            ReadInteger(injury, "schemaVersion", 1, 1, path, issues);
            var ordinal = ReadInteger(injury, "resourceCommandOrdinal", 1,
                ResourceMaterializationContract.MaxMutationsBeforeTriggers, path, issues);
            ReadInteger(injury, "maximumSeverityRank", 1, 4, path, issues);

            var cause = injury.TryGetProperty("cause", out var causeValue) &&
                        causeValue.ValueKind == JsonValueKind.String ? causeValue.GetString() : null;
            if (string.IsNullOrWhiteSpace(cause) || cause.Length > 2000 ||
                !string.Equals(cause, cause.Trim(), StringComparison.Ordinal) || cause.Any(char.IsControl))
                Add(issues, path + ".cause", "trimmed nonempty readable string, at most 2000 characters, no controls",
                    causeValue.ValueKind.ToString());

            if (!injury.TryGetProperty("allowedLocationKinds", out var locations) ||
                locations.ValueKind != JsonValueKind.Array || locations.GetArrayLength() is < 1 or > 5)
            {
                Add(issues, path + ".allowedLocationKinds", "1..5 unique exact legal location kinds",
                    locations.ValueKind.ToString());
            }
            else
            {
                var kinds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var location in locations.EnumerateArray())
                {
                    if (location.ValueKind != JsonValueKind.String ||
                        !LocationKinds.Contains(location.GetString()!) || !kinds.Add(location.GetString()!))
                        Add(issues, path + ".allowedLocationKinds", "unique anatomical|systemic|mental|spiritual_axis|other",
                            location.GetRawText());
                }
            }

            if (ordinal == null)
                continue;
            if (!outcome.TryGetProperty("responseFragment", out var fragment) ||
                fragment.ValueKind != JsonValueKind.Object ||
                !fragment.TryGetProperty("resourceChanges", out var changes) ||
                changes.ValueKind != JsonValueKind.Array || ordinal > changes.GetArrayLength())
            {
                Add(issues, path + ".resourceCommandOrdinal", "one-based index into the actual responseFragment.resourceChanges array",
                    ordinal.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                continue;
            }

            // Bind the actual raw array position before any DTO or JsonNode conversion.
            // The existing terminal resource validator still owns the full command grammar and event identity.
            var command = changes[ordinal.Value - 1];
            if (changes.GetArrayLength() > ResourceMaterializationContract.MaxMutationsBeforeTriggers ||
                !ExactString(command, "operation", "damage") || !ExactString(command, "resourceKey", "health") ||
                !command.TryGetProperty("target", out var target) || target.ValueKind != JsonValueKind.Object ||
                target.EnumerateObject().Count() != 2 || !ExactString(target, "kind", "player") ||
                !ExactString(target, "targetId", "player_current"))
                Add(issues, path + ".resourceCommandOrdinal", "existing exact damage command for player/player_current health",
                    "invalid or non-health resource command");
        }
        return issues;
    }

    internal static IReadOnlyList<ValidationIssue> ValidateCanonicalHealth(
        JsonElement root, string? definitionsJson, string? stateJson)
    {
        var declarations = DeclaredOutcomes(root, new List<ValidationIssue>()).ToArray();
        if (declarations.Length == 0)
            return Array.Empty<ValidationIssue>();

        var definitions = ResourceDefinitionCatalog.ParseCanonical(definitionsJson, allowMissingPristine: false);
        var catalog = definitions.Catalog;
        var valid = definitions.IsValid && catalog != null &&
                    catalog.TryResolveExact("health", out var definition) && definition != null &&
                    definition.CapacityPolicy.Kind == ResourceCapacityKind.RegisteredFormula &&
                    string.Equals(definition.CapacityPolicy.FormulaKey,
                        ResourceCapacityFormulaCatalog.MortalHealthCapacityV1, StringComparison.Ordinal) &&
                    definition.AllowedOwnerKinds.Contains(ResourceOwnerKind.Player) &&
                    definition.AllowedOperations.Contains(ResourceOperation.Damage);
        if (valid)
        {
            var state = ResourceStateContract.ParseCanonical(stateJson, catalog!, allowMissingPristine: false);
            valid = state.IsValid && state.Ledger != null &&
                    state.Ledger.TryResolveExact(new ResourceCoordinate("mortal_world", ResourceOwnerKind.Player,
                        "player_current", "health"), out var health) && health != null &&
                    health.State == ResourceLifecycleState.Active &&
                    health.CapacityBinding.Kind == ResourceCapacityKind.RegisteredFormula &&
                    string.Equals(health.CapacityBinding.AuthorityKey,
                        ResourceCapacityFormulaCatalog.MortalHealthCapacityV1, StringComparison.Ordinal);
        }
        if (valid)
            return Array.Empty<ValidationIssue>();

        var issues = new List<ValidationIssue>();
        foreach (var (_, path) in declarations)
            Add(issues, path + ".resourceCommandOrdinal", "canonical health definition and active mortal_world/player/player_current instance",
                "missing or invalid canonical health binding");
        return issues;
    }

    private static IEnumerable<(JsonElement Outcome, string Path)> DeclaredOutcomes(
        JsonElement root, List<ValidationIssue> issues)
    {
        if (root.ValueKind != JsonValueKind.Object)
            yield break;
        var declarationKey = ResourceMaterializationContract.BuildConfusableKey("physicalInjury");
        var outcomesKey = ResourceMaterializationContract.BuildConfusableKey("terminalOutcomes");
        var outcomeArrays = root.EnumerateObject().Where(property =>
            string.Equals(ResourceMaterializationContract.BuildConfusableKey(property.Name), outcomesKey,
                StringComparison.Ordinal)).ToArray();
        foreach (var arrayProperty in outcomeArrays)
        {
            if (arrayProperty.Value.ValueKind != JsonValueKind.Array)
                continue;
            var index = 0;
            foreach (var outcome in arrayProperty.Value.EnumerateArray())
            {
                var path = $"{QteSceneService.QteOfferPath}.terminalOutcomes[{index++}].physicalInjury";
                if (outcome.ValueKind != JsonValueKind.Object)
                    continue;
                var found = false;
                foreach (var property in outcome.EnumerateObject())
                {
                    if (!string.Equals(ResourceMaterializationContract.BuildConfusableKey(property.Name), declarationKey,
                            StringComparison.Ordinal))
                        continue;
                    if (found || !string.Equals(property.Name, "physicalInjury", StringComparison.Ordinal))
                        Add(issues, path, "one exact physicalInjury property, or omission", property.Name);
                    found = true;
                }
                if (found && (outcomeArrays.Length != 1 ||
                    !string.Equals(arrayProperty.Name, "terminalOutcomes", StringComparison.Ordinal)))
                    Add(issues, path, "one exact terminalOutcomes array for declared injury", arrayProperty.Name);
                if (outcome.TryGetProperty("physicalInjury", out _))
                    yield return (outcome, path);
            }
        }
    }

    private static int? ReadInteger(JsonElement injury, string field, int minimum, int maximum,
        string path, List<ValidationIssue> issues)
    {
        if (injury.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var number) && number >= minimum && number <= maximum)
            return number;
        Add(issues, path + "." + field, $"integer {minimum}..{maximum}", value.ValueKind.ToString());
        return null;
    }

    private static bool ExactString(JsonElement value, string field, string expected) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(field, out var token) &&
        token.ValueKind == JsonValueKind.String && string.Equals(token.GetString(), expected, StringComparison.Ordinal);

    private static void Add(List<ValidationIssue> issues, string path, string expected, string actual) =>
        issues.Add(new ValidationIssue(path, IssueSeverity.Error,
            "QTE physicalInjury declaration rejected before selection.", code: "qte_physical_injury_invalid",
            section: "QTE", expected: expected, actual: actual,
            repairHint: "Omit physicalInjury for a resource-only outcome, or supply the exact five-field declaration for existing player health damage."));
}
