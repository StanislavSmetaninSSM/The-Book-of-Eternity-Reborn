using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed class MortalWoundTreatmentCapabilityOperationLimits
{
    private readonly ReadOnlyCollection<string> _removableComplicationKinds;

    [System.Text.Json.Serialization.JsonConstructor]
    private MortalWoundTreatmentCapabilityOperationLimits(
        bool mayStabilize,
        int maximumRecoveryPoints,
        int maximumSeverityReductionSteps,
        IReadOnlyList<string> removableComplicationKinds,
        bool mayHealAtSeverityI,
        int maximumCosmeticHealLegacies,
        int maximumMechanicalEffectHealLegacies)
    {
        ArgumentNullException.ThrowIfNull(removableComplicationKinds);
        MayStabilize = mayStabilize;
        MaximumRecoveryPoints = maximumRecoveryPoints;
        MaximumSeverityReductionSteps = maximumSeverityReductionSteps;
        _removableComplicationKinds = new ReadOnlyCollection<string>(
            removableComplicationKinds.ToArray());
        MayHealAtSeverityI = mayHealAtSeverityI;
        MaximumCosmeticHealLegacies = maximumCosmeticHealLegacies;
        MaximumMechanicalEffectHealLegacies = maximumMechanicalEffectHealLegacies;
    }

    public bool MayStabilize { get; }
    public int MaximumRecoveryPoints { get; }
    public int MaximumSeverityReductionSteps { get; }
    public IReadOnlyList<string> RemovableComplicationKinds =>
        _removableComplicationKinds;
    public bool MayHealAtSeverityI { get; }
    public int MaximumCosmeticHealLegacies { get; }
    public int MaximumMechanicalEffectHealLegacies { get; }

    internal static MortalWoundTreatmentCapabilityOperationLimits Create(
        bool mayStabilize,
        int maximumRecoveryPoints,
        int maximumSeverityReductionSteps,
        IReadOnlyList<string> removableComplicationKinds,
        bool mayHealAtSeverityI,
        int maximumCosmeticHealLegacies,
        int maximumMechanicalEffectHealLegacies) => new(
        mayStabilize,
        maximumRecoveryPoints,
        maximumSeverityReductionSteps,
        removableComplicationKinds,
        mayHealAtSeverityI,
        maximumCosmeticHealLegacies,
        maximumMechanicalEffectHealLegacies);
}

internal sealed record MortalWoundTreatmentCapabilityDefinition(
    int SchemaVersion,
    string CapabilityRef,
    string WoundDomain,
    int MinimumSeverityRank,
    int MaximumSeverityRank,
    MortalWoundTreatmentCapabilityOperationLimits OperationLimits);

internal sealed record MortalWoundTreatmentCapabilitySkillSource(
    string OwnerKind,
    string OwnerId,
    string SkillKind,
    string SkillId,
    string DisplayName,
    string Lifecycle,
    bool Active,
    string SourcePath,
    IReadOnlyList<MortalWoundTreatmentCapabilityDefinition> Capabilities);

internal sealed record MortalWoundTreatmentCapabilityCatalogResult(
    bool IsValid,
    IReadOnlyList<ValidationIssue> Issues,
    IReadOnlyList<MortalWoundTreatmentCapabilitySkillSource> Sources);

/// <summary>
/// Strict, detached parser for the optional wound-treatment capability extension on
/// canonical Mortal skill rows. Shape validation deliberately does not grant a proof.
/// </summary>
internal static class MortalWoundTreatmentCapabilityContract
{
    private const string ExtensionProperty = "mortalWoundTreatmentCapabilities";
    private const int MaximumWoundLegacyRows = 8;
    private static readonly HashSet<string> CapabilityFields = new(StringComparer.Ordinal)
    {
        "schemaVersion", "capabilityRef", "woundDomain", "minimumSeverityRank", "maximumSeverityRank", "operationLimits"
    };
    private static readonly HashSet<string> OperationLimitFields = new(StringComparer.Ordinal)
    {
        "mayStabilize", "maximumRecoveryPoints", "maximumSeverityReductionSteps", "removableComplicationKinds",
        "mayHealAtSeverityI", "maximumCosmeticHealLegacies", "maximumMechanicalEffectHealLegacies"
    };
    private static readonly HashSet<string> ComplicationKinds = new(StringComparer.Ordinal)
    {
        "bleeding", "infection", "pain", "impairment", "systemic_instability", "spiritual_instability", "other"
    };

    internal static MortalWoundTreatmentCapabilityCatalogResult ParseActorCatalog(
        string ownerKind,
        string ownerId,
        JsonObject activeSkillsRoot,
        string activeSourcePath,
        JsonObject passiveSkillsRoot,
        string passiveSourcePath)
    {
        ArgumentNullException.ThrowIfNull(activeSkillsRoot);
        ArgumentNullException.ThrowIfNull(passiveSkillsRoot);
        var issues = new List<ValidationIssue>();
        var activeSources = ParseSkillSources(
            ownerKind, ownerId, "active", activeSkillsRoot, activeSourcePath,
            "activeSkillChanges", "activeSkills", issues);
        var passiveSources = ParseSkillSources(
            ownerKind, ownerId, "passive", passiveSkillsRoot, passiveSourcePath,
            "passiveSkillChanges", "passiveSkills", issues);
        var allSources = activeSources.Concat(passiveSources).ToArray();

        var skillIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var capabilityRefs = new Dictionary<string, string>(StringComparer.Ordinal);
        var hasCapabilities = allSources.Any(static source => source.Capabilities.Count > 0);
        foreach (var source in allSources)
        {
            if (hasCapabilities && !string.IsNullOrEmpty(source.SkillId))
            {
                RegisterExactAndConfusable(
                    source.SkillId,
                    source.SourcePath + ".skillId",
                    skillIds,
                    issues,
                    "mortal_wound_treatment_capability_skill_id_invalid",
                    "mortal_wound_treatment_capability_skill_id_confusable");
            }

            foreach (var capability in source.Capabilities)
            {
                RegisterExactAndConfusable(
                    capability.CapabilityRef,
                    source.SourcePath + "." + ExtensionProperty + ".capabilityRef",
                    capabilityRefs,
                    issues,
                    "mortal_wound_treatment_capability_ref_invalid",
                    "mortal_wound_treatment_capability_ref_confusable");
            }
        }

        var valid = !issues.Any(issue => issue.Severity == IssueSeverity.Error);
        return new MortalWoundTreatmentCapabilityCatalogResult(
            valid,
            issues.ToArray(),
            valid
                ? allSources.Where(static source => source.Capabilities.Count > 0).ToArray()
                : Array.Empty<MortalWoundTreatmentCapabilitySkillSource>());
    }

    internal static void ValidateSkillExtension(
        JsonElement skill,
        string skillPath,
        ICollection<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        if (skill.ValueKind != JsonValueKind.Object ||
            !skill.TryGetProperty(ExtensionProperty, out var extension))
            return;

        var capabilities = ParseCapabilities(extension, skillPath, issues);
        if (capabilities.Count > 0 &&
            (!skill.TryGetProperty("skillId", out var skillId) ||
             skillId.ValueKind != JsonValueKind.String ||
             !ResourceMaterializationContract.IsExactIdentifier(skillId.GetString())))
        {
            AddIssue(
                issues,
                skillPath + ".skillId",
                "mortal_wound_treatment_capability_skill_id_invalid",
                "An extension-bearing skill requires one exact permanent skillId.");
        }
    }

    internal static IReadOnlyList<ValidationIssue> ValidateComposedActorCatalog(
        string ownerKind,
        string ownerId,
        JsonObject currentActiveRoot,
        string activeSourcePath,
        JsonObject currentPassiveRoot,
        string passiveSourcePath,
        JsonObject composedActiveRoot,
        JsonObject composedPassiveRoot)
    {
        var issues = new List<ValidationIssue>();
        var current = ParseActorCatalog(
            ownerKind, ownerId, currentActiveRoot, activeSourcePath, currentPassiveRoot, passiveSourcePath);
        var composed = ParseActorCatalog(
            ownerKind, ownerId, composedActiveRoot, activeSourcePath, composedPassiveRoot, passiveSourcePath);
        issues.AddRange(current.Issues);
        issues.AddRange(composed.Issues);
        if (!current.IsValid || !composed.IsValid)
            return issues;

        var currentExtensions = BuildExtensionMap(currentActiveRoot, currentPassiveRoot);
        var composedExtensions = BuildExtensionMap(composedActiveRoot, composedPassiveRoot);
        foreach (var (key, currentExtension) in currentExtensions.Where(static pair => pair.Value.IsPresent))
        {
            if (!composedExtensions.TryGetValue(key, out var composedExtension) ||
                !currentExtension.IsByteSemanticallyEqualTo(composedExtension))
            {
                AddIssue(
                    issues,
                    key.Path,
                    "mortal_wound_treatment_capability_preservation_invalid",
                    "A pre-existing wound-treatment capability extension must be preserved exactly in the composed skill catalog.");
            }
        }

        foreach (var (key, _) in composedExtensions.Where(static pair => pair.Value.IsPresent))
        {
            if (!currentExtensions.TryGetValue(key, out var currentExtension) || !currentExtension.IsPresent)
            {
                AddIssue(
                    issues,
                    key.Path,
                    "mortal_wound_treatment_capability_synthesized",
                    "The skill composer may not synthesize a wound-treatment capability extension.");
            }
        }

        return issues;
    }

    /// <summary>
    /// Parses canonical skill capability sources while retaining their active flags.
    /// </summary>
    /// <param name="ownerKind">
    /// The actor kind owning the skill rows.
    /// </param>
    /// <param name="ownerId">
    /// The exact actor identity owning the skill rows.
    /// </param>
    /// <param name="skillKind">
    /// The active or passive catalog kind.
    /// </param>
    /// <param name="root">
    /// The canonical skill container.
    /// </param>
    /// <param name="sourcePath">
    /// The source file path used to retain each row's coordinates.
    /// </param>
    /// <param name="preferredArrayName">
    /// The skill array field selected when it contains an array.
    /// </param>
    /// <param name="alternateArrayName">
    /// The skill array field considered when the preferred field is not an array.
    /// </param>
    /// <param name="issues">
    /// The collection receiving invalid capability and permanent skill identity issues.
    /// </param>
    /// <returns>
    /// The parsed sources. An absent active field defaults to <see langword="true"/>;
    /// a present non-boolean or <see langword="null"/> field yields <see langword="false"/>.
    /// </returns>
    private static IReadOnlyList<MortalWoundTreatmentCapabilitySkillSource> ParseSkillSources(
        string ownerKind,
        string ownerId,
        string skillKind,
        JsonObject root,
        string sourcePath,
        string preferredArrayName,
        string alternateArrayName,
        ICollection<ValidationIssue> issues)
    {
        var arrayName = root[preferredArrayName] is JsonArray ? preferredArrayName : alternateArrayName;
        if (root[arrayName] is not JsonArray skills)
            return Array.Empty<MortalWoundTreatmentCapabilitySkillSource>();

        var sources = new List<MortalWoundTreatmentCapabilitySkillSource>();
        for (var index = 0; index < skills.Count; index++)
        {
            if (skills[index] is not JsonObject skill)
                continue;

            var path = $"{sourcePath}.{arrayName}[{index}]";
            var skillId = ReadString(skill, "skillId") ?? string.Empty;
            var displayName = ReadString(skill, "displayName") ?? ReadString(skill, "skillName") ?? string.Empty;
            var capabilities = Array.Empty<MortalWoundTreatmentCapabilityDefinition>();
            if (skill.TryGetPropertyValue(ExtensionProperty, out var extension))
                capabilities = ParseCapabilities(extension, path, issues).ToArray();

            if (capabilities.Length > 0 && !ResourceMaterializationContract.IsExactIdentifier(skillId))
            {
                AddIssue(
                    issues,
                    path + ".skillId",
                    "mortal_wound_treatment_capability_skill_id_invalid",
                    "An extension-bearing skill requires one exact permanent skillId.");
            }

            var active = skill["active"] is JsonValue activeValue &&
                         activeValue.TryGetValue<bool>(out var activeFlag)
                ? activeFlag
                : !skill.ContainsKey("active");
            sources.Add(new MortalWoundTreatmentCapabilitySkillSource(
                ownerKind,
                ownerId,
                skillKind,
                skillId,
                displayName,
                "active",
                active,
                path,
                capabilities));
        }

        return sources;
    }

    private static IReadOnlyList<MortalWoundTreatmentCapabilityDefinition> ParseCapabilities(
        JsonNode? extension,
        string skillPath,
        ICollection<ValidationIssue> issues)
    {
        if (extension is null)
        {
            AddIssue(issues, skillPath + "." + ExtensionProperty,
                "mortal_wound_treatment_capability_shape_invalid",
                "mortalWoundTreatmentCapabilities must be an array.");
            return Array.Empty<MortalWoundTreatmentCapabilityDefinition>();
        }

        return ParseCapabilities(ToElement(extension), skillPath, issues);
    }

    private static IReadOnlyList<MortalWoundTreatmentCapabilityDefinition> ParseCapabilities(
        JsonElement extension,
        string skillPath,
        ICollection<ValidationIssue> issues)
    {
        if (extension.ValueKind != JsonValueKind.Array)
        {
            AddIssue(issues, skillPath + "." + ExtensionProperty,
                "mortal_wound_treatment_capability_shape_invalid",
                "mortalWoundTreatmentCapabilities must be an array.");
            return Array.Empty<MortalWoundTreatmentCapabilityDefinition>();
        }

        var parsed = new List<MortalWoundTreatmentCapabilityDefinition>();
        var index = 0;
        foreach (var item in extension.EnumerateArray())
        {
            var path = $"{skillPath}.{ExtensionProperty}[{index++}]";
            if (item.ValueKind != JsonValueKind.Object || !HasExactFields(item, CapabilityFields))
            {
                AddIssue(issues, path, "mortal_wound_treatment_capability_shape_invalid",
                    "The capability extension has an unknown, missing, or malformed field.");
                continue;
            }

            if (!TryReadInt32(item, "schemaVersion", out var schemaVersion) || schemaVersion != 1)
            {
                AddIssue(issues, path + ".schemaVersion", "mortal_wound_treatment_capability_schema_invalid",
                    "Only schemaVersion 1 is supported.");
                continue;
            }

            if (!TryReadExactIdentifier(item, "capabilityRef", out var capabilityRef))
            {
                AddIssue(issues, path + ".capabilityRef", "mortal_wound_treatment_capability_ref_invalid",
                    "capabilityRef must be an exact identifier.");
                continue;
            }

            if (!TryReadString(item, "woundDomain", out var woundDomain) ||
                !string.Equals(woundDomain, "physical", StringComparison.Ordinal))
            {
                AddIssue(issues, path + ".woundDomain", "mortal_wound_treatment_capability_domain_invalid",
                    "schemaVersion 1 supports only the physical wound domain.");
                continue;
            }

            if (!TryReadInt32(item, "minimumSeverityRank", out var minimumSeverityRank) ||
                !TryReadInt32(item, "maximumSeverityRank", out var maximumSeverityRank) ||
                minimumSeverityRank is < 1 or > 4 || maximumSeverityRank is < 1 or > 4 ||
                minimumSeverityRank > maximumSeverityRank)
            {
                AddIssue(issues, path + ".minimumSeverityRank", "mortal_wound_treatment_capability_severity_invalid",
                    "The inclusive severity envelope must be within ranks I through IV.");
                continue;
            }

            if (!item.TryGetProperty("operationLimits", out var limitsElement) ||
                !TryParseOperationLimits(limitsElement, path + ".operationLimits", issues, out var limits))
            {
                continue;
            }

            parsed.Add(new MortalWoundTreatmentCapabilityDefinition(
                schemaVersion,
                capabilityRef,
                woundDomain,
                minimumSeverityRank,
                maximumSeverityRank,
                limits));
        }

        return parsed;
    }

    private static bool TryParseOperationLimits(
        JsonElement element,
        string path,
        ICollection<ValidationIssue> issues,
        out MortalWoundTreatmentCapabilityOperationLimits limits)
    {
        limits = null!;
        if (element.ValueKind != JsonValueKind.Object || !HasExactFields(element, OperationLimitFields) ||
            !TryReadBoolean(element, "mayStabilize", out var mayStabilize) ||
            !TryReadInt32(element, "maximumRecoveryPoints", out var maximumRecoveryPoints) ||
            !TryReadInt32(element, "maximumSeverityReductionSteps", out var maximumSeverityReductionSteps) ||
            !TryReadBoolean(element, "mayHealAtSeverityI", out var mayHealAtSeverityI) ||
            !TryReadInt32(element, "maximumCosmeticHealLegacies", out var maximumCosmeticHealLegacies) ||
            !TryReadInt32(element, "maximumMechanicalEffectHealLegacies", out var maximumMechanicalEffectHealLegacies))
        {
            AddIssue(issues, path, "mortal_wound_treatment_capability_operation_limits_invalid",
                "operationLimits must use the exact closed field set and value types.");
            return false;
        }

        if (!TryReadComplicationKinds(element, path, issues, out var removableComplicationKinds))
            return false;

        if (maximumRecoveryPoints < 0 || maximumSeverityReductionSteps is < 0 or > 3 ||
            maximumCosmeticHealLegacies is < 0 or > MaximumWoundLegacyRows ||
            maximumMechanicalEffectHealLegacies is < 0 or > MaximumWoundLegacyRows)
        {
            AddIssue(issues, path, "mortal_wound_treatment_capability_operation_limits_invalid",
                "Numeric operation limits must be non-negative and severity reduction is at most three.");
            return false;
        }

        if ((long)maximumCosmeticHealLegacies + maximumMechanicalEffectHealLegacies > MaximumWoundLegacyRows)
        {
            AddIssue(issues, path, "mortal_wound_treatment_capability_legacy_limit_invalid",
                "The aggregate cosmetic and mechanical legacy limit may not exceed eight.");
            return false;
        }

        if (!mayStabilize && maximumRecoveryPoints == 0 && maximumSeverityReductionSteps == 0 &&
            removableComplicationKinds.Count == 0 && !mayHealAtSeverityI &&
            maximumCosmeticHealLegacies == 0 && maximumMechanicalEffectHealLegacies == 0)
        {
            AddIssue(issues, path, "mortal_wound_treatment_capability_empty",
                "A treatment capability must enable at least one operation.");
            return false;
        }

        limits = MortalWoundTreatmentCapabilityOperationLimits.Create(
            mayStabilize,
            maximumRecoveryPoints,
            maximumSeverityReductionSteps,
            removableComplicationKinds.ToArray(),
            mayHealAtSeverityI,
            maximumCosmeticHealLegacies,
            maximumMechanicalEffectHealLegacies);
        return true;
    }

    private static bool TryReadComplicationKinds(
        JsonElement limits,
        string path,
        ICollection<ValidationIssue> issues,
        out List<string> kinds)
    {
        kinds = new List<string>();
        if (!limits.TryGetProperty("removableComplicationKinds", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            AddIssue(issues, path + ".removableComplicationKinds", "mortal_wound_treatment_capability_operation_limits_invalid",
                "removableComplicationKinds must be an array.");
            return false;
        }

        var exact = new HashSet<string>(StringComparer.Ordinal);
        var confusable = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || item.GetString() is not { } kind ||
                !ComplicationKinds.Contains(kind) || !exact.Add(kind) ||
                !confusable.Add(ExactIdentifierConfusableKey.Build(kind)))
            {
                AddIssue(issues, path + ".removableComplicationKinds",
                    "mortal_wound_treatment_capability_complication_kind_invalid",
                    "removableComplicationKinds must contain unique closed wound complication kinds.");
                return false;
            }
            kinds.Add(kind);
        }
        return true;
    }

    private static void RegisterExactAndConfusable(
        string identifier,
        string path,
        IDictionary<string, string> seen,
        ICollection<ValidationIssue> issues,
        string invalidCode,
        string duplicateCode)
    {
        if (!ResourceMaterializationContract.IsExactIdentifier(identifier))
        {
            AddIssue(issues, path, invalidCode, "An exact identifier is required.");
            return;
        }

        var confusable = ExactIdentifierConfusableKey.Build(identifier);
        if (!seen.TryAdd(identifier, path) || seen.Any(pair =>
                !string.Equals(pair.Key, identifier, StringComparison.Ordinal) &&
                string.Equals(ExactIdentifierConfusableKey.Build(pair.Key), confusable, StringComparison.Ordinal)))
        {
            AddIssue(issues, path, duplicateCode, "Identifiers must be exact and Unicode-confusable unique for one actor.");
        }
    }

    private static Dictionary<ExtensionKey, ExtensionState> BuildExtensionMap(JsonObject activeRoot, JsonObject passiveRoot)
    {
        var map = new Dictionary<ExtensionKey, ExtensionState>();
        AddExtensions(activeRoot, "activeSkillChanges", "activeSkills", map);
        AddExtensions(passiveRoot, "passiveSkillChanges", "passiveSkills", map);
        return map;
    }

    private static void AddExtensions(
        JsonObject root,
        string preferredArrayName,
        string alternateArrayName,
        IDictionary<ExtensionKey, ExtensionState> map)
    {
        var arrayName = root[preferredArrayName] is JsonArray ? preferredArrayName : alternateArrayName;
        var array = root[arrayName] as JsonArray;
        if (array is null)
            return;
        for (var index = 0; index < array.Count; index++)
        {
            if (array[index] is not JsonObject skill)
                continue;

            var key = new ExtensionKey(
                ReadString(skill, "skillId") ?? string.Empty,
                $"{arrayName}[{index}].{ExtensionProperty}");
            map[key] = skill.TryGetPropertyValue(ExtensionProperty, out var extension)
                ? new ExtensionState(true, extension?.DeepClone())
                : new ExtensionState(false, null);
        }
    }

    private sealed record ExtensionState(bool IsPresent, JsonNode? Value)
    {
        internal bool IsByteSemanticallyEqualTo(ExtensionState other) =>
            IsPresent == other.IsPresent &&
            (!IsPresent || string.Equals(Value?.ToJsonString() ?? "null", other.Value?.ToJsonString() ?? "null", StringComparison.Ordinal));
    }

    private static JsonElement ToElement(JsonNode node)
    {
        using var document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.Clone();
    }

    private static bool HasExactFields(JsonElement item, IReadOnlySet<string> expected)
    {
        var names = item.EnumerateObject().Select(static property => property.Name).ToArray();
        return names.Length == expected.Count && names.ToHashSet(StringComparer.Ordinal).SetEquals(expected);
    }

    private static string? ReadString(JsonObject item, string property) =>
        item[property] is JsonValue value && value.TryGetValue<string>(out var parsed) ? parsed : null;

    private static bool TryReadString(JsonElement item, string property, out string value)
    {
        value = string.Empty;
        return item.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.String &&
               (value = element.GetString() ?? string.Empty).Length > 0;
    }

    private static bool TryReadExactIdentifier(JsonElement item, string property, out string value) =>
        TryReadString(item, property, out value) && ResourceMaterializationContract.IsExactIdentifier(value);

    private static bool TryReadInt32(JsonElement item, string property, out int value)
    {
        value = 0;
        return item.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value);
    }

    private static bool TryReadBoolean(JsonElement item, string property, out bool value)
    {
        value = false;
        return item.TryGetProperty(property, out var element) &&
               element.ValueKind is JsonValueKind.True or JsonValueKind.False &&
               SetBoolean(element, out value);
    }

    private static bool SetBoolean(JsonElement element, out bool value)
    {
        value = element.GetBoolean();
        return true;
    }

    private static void AddIssue(ICollection<ValidationIssue> issues, string path, string code, string message) =>
        issues.Add(new ValidationIssue(path, IssueSeverity.Error, message, code: code, section: "MortalWoundTreatmentCapability"));

    private sealed record ExtensionKey(string SkillId, string Path);
}
