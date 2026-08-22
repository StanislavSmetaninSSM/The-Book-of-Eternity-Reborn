using System.Text.Json;

namespace BookOfEternityClient.Services;

internal enum EffectMaterializationPhase
{
    RawDefinition,
    CanonicalActive
}

internal enum EffectCarrierKind
{
    Player,
    Npc,
    Combatant,
    AfterlifeProfile,
    SpiritualConflict
}

internal static class EffectMaterializationContract
{
    internal const int SchemaVersion = 1;

    private static readonly HashSet<string> RootFields = Set(
        "schemaVersion", "entityKind", "effectId", "state", "realm", "target", "display",
        "source", "components", "lifetime", "stacking", "triggers", "removal", "links",
        "chronology");
    private static readonly HashSet<string> AfterlifeConditionRootFields = new(
        RootFields.Concat(AfterlifeSpiritualConflictState.CombatConditionProjectionFields),
        StringComparer.Ordinal);

    private static readonly HashSet<string> States = Set("active", "suspended");
    private static readonly HashSet<string> Realms = Set("mortal_world", "chaos_sea", "shining_abode");
    private static readonly HashSet<string> TargetKinds = Set(
        "player", "npc", "combatant", "guardian", "resident", "radiant_actor",
        "afterlife_actor", "spiritual_conflict_side");
    private static readonly HashSet<string> SourceKinds = Set(
        "skill", "spiritual_art", "item", "wound", "quest", "location", "hazard", "faction",
        "world_event", "fate_card", "combat_action");
    private static readonly HashSet<string> DisplayCategories = Set(
        "buff", "debuff", "condition", "environmental", "mixed");
    private static readonly HashSet<string> Visibilities = Set("visible", "hidden", "gm_only");
    private static readonly HashSet<string> LifetimeModes = Set(
        "turns", "uses", "until_time", "scene", "source_bound", "condition_bound",
        "permanent", "manual");
    private static readonly HashSet<string> AdvancePhases = Set(
        "owner_turn_start", "owner_turn_end", "world_turn_start", "world_turn_end",
        "afterlife_exchange_end");
    private static readonly HashSet<string> StackPolicies = Set(
        "independent", "stack", "refresh", "replace", "merge");
    private static readonly HashSet<string> RefreshModes = Set("reset", "extend");
    private static readonly HashSet<string> MergeRules = Set(
        "sum", "minimum", "maximum", "profile_specific");
    private static readonly IReadOnlySet<string> EventTypes =
        EffectEventTypeCatalog.Registered;
    private static readonly HashSet<string> ResolutionModes = Set("deterministic", "bounded_receipt");
    private static readonly HashSet<string> SourceLossPolicies = Set("expire", "suspend", "no_change");
    private static readonly HashSet<string> LinkKinds = Set(
        "wound", "skill", "spiritual_art", "item", "quest", "location", "hazard", "faction",
        "world_event", "fate_card", "combat");
    private static readonly HashSet<string> LinkRoles = Set(
        "source", "condition", "context", "cleanup_companion");

    internal static IReadOnlyList<ValidationIssue> Validate(
        JsonElement effect,
        string path,
        EffectMaterializationPhase phase) =>
        Validate(effect, path, phase, RootFields);

    internal static IReadOnlyList<ValidationIssue> ValidateAfterlifeCombatCondition(
        JsonElement effect,
        string path) =>
        Validate(
            effect,
            path,
            EffectMaterializationPhase.CanonicalActive,
            AfterlifeConditionRootFields);

    private static IReadOnlyList<ValidationIssue> Validate(
        JsonElement effect,
        string path,
        EffectMaterializationPhase phase,
        IReadOnlySet<string> allowedRootFields)
    {
        var issues = new List<ValidationIssue>();
        if (effect.ValueKind != JsonValueKind.Object)
        {
            Add(issues, path, "effect_materialization_invalid_field", "canonical active effect object", effect.ValueKind.ToString());
            return issues;
        }

        FindDuplicateProperties(effect, path, issues, "effect_materialization_duplicate_property");
        ValidateClosedObject(effect, path, allowedRootFields, issues, "effect_materialization_unknown_field");
        foreach (var field in RootFields)
        {
            if (!effect.TryGetProperty(field, out _))
                Add(issues, path + "." + field, "effect_materialization_missing_field", "required canonical active-effect field", "missing");
        }

        if (phase != EffectMaterializationPhase.CanonicalActive)
        {
            Add(issues, path, "effect_materialization_invalid_field", "CanonicalActive validation phase", phase.ToString());
            return issues;
        }

        RequireExactInt(effect, path, "schemaVersion", SchemaVersion, issues);
        RequireClosedString(effect, path, "entityKind", Set("active_effect"), issues);
        RequireExactIdentifier(effect, path, "effectId", issues);
        RequireClosedString(effect, path, "state", States, issues);
        RequireClosedString(effect, path, "realm", Realms, issues);
        ValidateTarget(effect, path, issues);
        ValidateDisplay(effect, path, issues);
        ValidateSource(effect, path, issues);
        var componentIds = ValidateComponents(effect, path, issues);
        ValidateLifetime(effect, path, issues);
        ValidateStacking(effect, path, issues);
        ValidateTriggers(effect, path, componentIds, issues);
        ValidateRemoval(effect, path, issues);
        ValidateLinks(effect, path, issues);
        ValidateChronology(effect, path, issues);
        return issues;
    }

    internal static IReadOnlyList<ValidationIssue> ValidateCarrier(
        JsonElement? carrier,
        string path,
        EffectCarrierKind kind)
    {
        var issues = new List<ValidationIssue>();
        if (!carrier.HasValue)
            return issues;

        var root = carrier.Value;
        if (root.ValueKind != JsonValueKind.Object)
        {
            Add(issues, path, "effect_materialization_legacy_carrier_unsupported", "missing pristine carrier or current canonical carrier object", root.ValueKind.ToString());
            return issues;
        }

        FindDuplicateProperties(root, path, issues, "effect_materialization_duplicate_property");
        switch (kind)
        {
            case EffectCarrierKind.Player:
                ValidatePlayerCarrier(root, path, issues);
                break;
            case EffectCarrierKind.Npc:
                ValidateNpcCarrier(root, path, issues);
                break;
            default:
                ValidateEmbeddedCarrier(root, path, kind, issues);
                break;
        }
        return issues;
    }

    private static void ValidatePlayerCarrier(
        JsonElement root,
        string path,
        List<ValidationIssue> issues)
    {
        var fields = Set("schemaVersion", "activeEffects");
        if (!HasExactFields(root, fields))
        {
            Add(issues, path, "effect_materialization_legacy_carrier_unsupported", "{schemaVersion, activeEffects} current carrier", root.GetRawText());
            return;
        }

        RequireExactInt(root, path, "schemaVersion", SchemaVersion, issues);
        ValidateEffectArray(root, path, "activeEffects", issues);
    }

    private static void ValidateNpcCarrier(
        JsonElement root,
        string path,
        List<ValidationIssue> issues)
    {
        var visibleFields = root.EnumerateObject()
            .Where(static property => !property.Name.StartsWith("_", StringComparison.Ordinal))
            .Select(static property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        var allowedFields = Set("schemaVersion", "entries", "NPCWoundChanges");
        var hasSchemaVersion = visibleFields.Contains("schemaVersion");
        var hasEntries = visibleFields.Contains("entries");
        var hasWoundChanges = visibleFields.Contains("NPCWoundChanges");
        if (visibleFields.Except(allowedFields, StringComparer.Ordinal).Any() ||
            hasSchemaVersion != hasEntries ||
            (!hasSchemaVersion && !hasWoundChanges))
        {
            Add(
                issues,
                path,
                "effect_materialization_legacy_carrier_unsupported",
                "{schemaVersion, entries} current NPC carrier with optional NPCWoundChanges and _ metadata, or an adjacent-only NPCWoundChanges command surface",
                root.GetRawText());
            return;
        }

        if (hasWoundChanges &&
            (!root.TryGetProperty("NPCWoundChanges", out var woundChanges) ||
             woundChanges.ValueKind != JsonValueKind.Array))
        {
            Add(
                issues,
                path + ".NPCWoundChanges",
                "effect_materialization_invalid_field",
                "array",
                Describe(root, "NPCWoundChanges"));
        }

        if (!hasSchemaVersion)
            return;

        RequireExactInt(root, path, "schemaVersion", SchemaVersion, issues);
        if (!root.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
        {
            Add(issues, path + ".entries", "effect_materialization_invalid_field", "array", Describe(root, "entries"));
            return;
        }

        var index = 0;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries.EnumerateArray())
        {
            var entryPath = $"{path}.entries[{index++}]";
            if (entry.ValueKind != JsonValueKind.Object ||
                !entry.TryGetProperty("NPCId", out _) ||
                !entry.TryGetProperty("activeEffects", out _))
            {
                Add(issues, entryPath, "effect_materialization_legacy_carrier_unsupported", "entry containing NPCId and activeEffects while preserving adjacent NPC state", entry.GetRawText());
                continue;
            }

            var id = RequireExactIdentifier(entry, entryPath, "NPCId", issues);
            if (id != null && (!ids.Add(id) || !aliases.Add(MortalLocationIdentityState.BuildConfusableKey(id))))
                Add(issues, entryPath + ".NPCId", "effect_materialization_invalid_field", "unique exact/confusable NPCId", id);
            ValidateEffectArray(entry, entryPath, "activeEffects", issues);
        }
    }

    private static void ValidateEmbeddedCarrier(
        JsonElement root,
        string path,
        EffectCarrierKind kind,
        List<ValidationIssue> issues)
    {
        var candidateFields = kind switch
        {
            EffectCarrierKind.Combatant => new[] { "activeBuffs", "activeDebuffs" },
            EffectCarrierKind.AfterlifeProfile => new[] { "activeEffects" },
            EffectCarrierKind.SpiritualConflict => new[] { "combatConditions" },
            _ => Array.Empty<string>()
        };

        var found = false;
        foreach (var field in candidateFields)
        {
            if (!root.TryGetProperty(field, out _))
                continue;
            found = true;
            ValidateEffectArray(root, path, field, issues);
        }

        if (!found && root.EnumerateObject().Any())
            Add(issues, path, "effect_materialization_legacy_carrier_unsupported", "current canonical effect collection", root.GetRawText());
    }

    private static void ValidateEffectArray(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var effects) || effects.ValueKind != JsonValueKind.Array)
        {
            Add(issues, path + "." + field, "effect_materialization_invalid_field", "active effect array", Describe(root, field));
            return;
        }

        var index = 0;
        foreach (var effect in effects.EnumerateArray())
            issues.AddRange(Validate(effect, $"{path}.{field}[{index++}]", EffectMaterializationPhase.CanonicalActive));
    }

    private static void ValidateTarget(JsonElement root, string path, List<ValidationIssue> issues)
    {
        if (!TryGetObject(root, path, "target", issues, out var target))
            return;
        var targetPath = path + ".target";
        ValidateClosedObject(target, targetPath, Set("kind", "targetId"), issues, "effect_materialization_unknown_field");
        RequireClosedString(target, targetPath, "kind", TargetKinds, issues);
        RequireExactIdentifier(target, targetPath, "targetId", issues);
    }

    private static void ValidateDisplay(JsonElement root, string path, List<ValidationIssue> issues)
    {
        if (!TryGetObject(root, path, "display", issues, out var display))
            return;
        var displayPath = path + ".display";
        ValidateClosedObject(display, displayPath, Set("name", "description", "category", "visibility", "sourceLabel"), issues, "effect_materialization_unknown_field");
        RequireReadableString(display, displayPath, "name", issues);
        RequireReadableString(display, displayPath, "description", issues);
        RequireClosedString(display, displayPath, "category", DisplayCategories, issues);
        RequireClosedString(display, displayPath, "visibility", Visibilities, issues);
        ValidateOptionalReadableString(display, displayPath, "sourceLabel", issues);
    }

    private static void ValidateSource(JsonElement root, string path, List<ValidationIssue> issues)
    {
        if (!TryGetObject(root, path, "source", issues, out var source))
            return;
        var sourcePath = path + ".source";
        ValidateClosedObject(source, sourcePath, Set("kind", "sourceId", "definitionKey"), issues, "effect_materialization_unknown_field");
        RequireClosedString(source, sourcePath, "kind", SourceKinds, issues);
        RequireExactIdentifier(source, sourcePath, "sourceId", issues);
        RequireExactIdentifier(source, sourcePath, "definitionKey", issues);
    }

    private static HashSet<string> ValidateComponents(
        JsonElement root,
        string path,
        List<ValidationIssue> issues)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (!root.TryGetProperty("components", out var components) ||
            components.ValueKind != JsonValueKind.Array ||
            components.GetArrayLength() == 0)
        {
            Add(issues, path + ".components", "effect_materialization_invalid_field", "non-empty component array", Describe(root, "components"));
            return result;
        }

        var aliases = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var component in components.EnumerateArray())
        {
            var componentPath = $"{path}.components[{index++}]";
            EffectComponentProfiles.ValidateComponent(component, componentPath, issues);
            if (component.ValueKind != JsonValueKind.Object ||
                !TryReadExactIdentifier(component, "componentId", out var componentId))
                continue;
            if (!result.Add(componentId) || !aliases.Add(MortalLocationIdentityState.BuildConfusableKey(componentId)))
                Add(issues, componentPath + ".componentId", "effect_materialization_invalid_component", "unique exact/confusable componentId", componentId);
        }
        return result;
    }

    private static void ValidateLifetime(JsonElement root, string path, List<ValidationIssue> issues)
    {
        if (!TryGetObject(root, path, "lifetime", issues, out var lifetime))
            return;
        var lifetimePath = path + ".lifetime";
        var mode = RequireClosedString(lifetime, lifetimePath, "mode", LifetimeModes, issues);
        if (mode == null)
            return;

        switch (mode)
        {
            case "turns":
                ValidateClosedObject(lifetime, lifetimePath, Set("mode", "remainingTurns", "advancePhase", "displayText"), issues, "effect_materialization_unknown_field");
                RequirePositiveInt(lifetime, lifetimePath, "remainingTurns", issues);
                RequireClosedString(lifetime, lifetimePath, "advancePhase", AdvancePhases, issues);
                break;
            case "uses":
                ValidateClosedObject(lifetime, lifetimePath, Set("mode", "remainingUses", "consumingTriggerIds", "displayText"), issues, "effect_materialization_unknown_field");
                RequirePositiveInt(lifetime, lifetimePath, "remainingUses", issues);
                RequireExactStringArray(lifetime, lifetimePath, "consumingTriggerIds", allowEmpty: false, issues);
                break;
            case "until_time":
                ValidateClosedObject(lifetime, lifetimePath, Set("mode", "deadline", "displayText"), issues, "effect_materialization_unknown_field");
                RequireNonNegativeLong(lifetime, lifetimePath, "deadline", issues);
                break;
            case "scene":
                ValidateClosedObject(lifetime, lifetimePath, Set("mode", "sceneId", "onSceneExit", "displayText"), issues, "effect_materialization_unknown_field");
                RequireExactIdentifier(lifetime, lifetimePath, "sceneId", issues);
                RequireClosedString(lifetime, lifetimePath, "onSceneExit", Set("expire", "suspend"), issues);
                break;
            case "source_bound":
                ValidateClosedObject(lifetime, lifetimePath, Set("mode", "linkKind", "targetId", "activePredicate", "onSourceLoss", "displayText"), issues, "effect_materialization_unknown_field");
                RequireClosedString(lifetime, lifetimePath, "linkKind", LinkKinds, issues);
                RequireExactIdentifier(lifetime, lifetimePath, "targetId", issues);
                RequireExactIdentifier(lifetime, lifetimePath, "activePredicate", issues);
                RequireClosedString(lifetime, lifetimePath, "onSourceLoss", Set("expire", "suspend"), issues);
                break;
            case "condition_bound":
                ValidateClosedObject(lifetime, lifetimePath, Set("mode", "conditionKey", "operands", "onConditionLoss", "displayText"), issues, "effect_materialization_unknown_field");
                RequireExactIdentifier(lifetime, lifetimePath, "conditionKey", issues);
                RequireObject(lifetime, lifetimePath, "operands", issues);
                RequireClosedString(lifetime, lifetimePath, "onConditionLoss", Set("expire", "suspend"), issues);
                break;
            case "permanent":
                ValidateClosedObject(lifetime, lifetimePath, Set("mode", "displayText"), issues, "effect_materialization_unknown_field");
                break;
            case "manual":
                ValidateClosedObject(lifetime, lifetimePath, Set("mode", "authorities", "displayText"), issues, "effect_materialization_unknown_field");
                RequireExactStringArray(lifetime, lifetimePath, "authorities", allowEmpty: false, issues);
                break;
        }
        ValidateOptionalReadableString(lifetime, lifetimePath, "displayText", issues);
    }

    private static void ValidateStacking(JsonElement root, string path, List<ValidationIssue> issues)
    {
        if (!TryGetObject(root, path, "stacking", issues, out var stacking))
            return;
        var stackingPath = path + ".stacking";
        ValidateClosedObject(stacking, stackingPath, Set("stackKey", "policy", "maxStacks", "currentStacks", "refreshMode", "mergeRule"), issues, "effect_materialization_unknown_field");
        RequireExactIdentifier(stacking, stackingPath, "stackKey", issues);
        var policy = RequireClosedString(stacking, stackingPath, "policy", StackPolicies, issues);
        var maxStacks = RequirePositiveInt(stacking, stackingPath, "maxStacks", issues);
        var currentStacks = RequirePositiveInt(stacking, stackingPath, "currentStacks", issues);
        if (maxStacks.HasValue && currentStacks.HasValue && currentStacks > maxStacks)
            Add(issues, stackingPath + ".currentStacks", "effect_materialization_invalid_field", "currentStacks <= maxStacks", currentStacks.Value.ToString());
        if (string.Equals(policy, "independent", StringComparison.Ordinal) && (maxStacks != 1 || currentStacks != 1))
            Add(issues, stackingPath, "effect_materialization_invalid_field", "independent policy with maxStacks=currentStacks=1", stacking.GetRawText());

        ValidateNullableClosedString(stacking, stackingPath, "refreshMode", RefreshModes, required: policy == "refresh", issues);
        ValidateNullableClosedString(stacking, stackingPath, "mergeRule", MergeRules, required: policy == "merge", issues);
    }

    private static void ValidateTriggers(
        JsonElement root,
        string path,
        IReadOnlySet<string> componentIds,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("triggers", out var triggers) || triggers.ValueKind != JsonValueKind.Array)
        {
            Add(issues, path + ".triggers", "effect_materialization_invalid_field", "trigger array", Describe(root, "triggers"));
            return;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var trigger in triggers.EnumerateArray())
        {
            var triggerPath = $"{path}.triggers[{index++}]";
            if (trigger.ValueKind != JsonValueKind.Object)
            {
                Add(issues, triggerPath, "effect_materialization_invalid_field", "closed trigger object", trigger.GetRawText());
                continue;
            }
            ValidateClosedObject(trigger, triggerPath, Set("triggerId", "eventType", "priority", "componentIds", "consumeUses", "resolutionMode"), issues, "effect_materialization_unknown_field");
            var triggerId = RequireExactIdentifier(trigger, triggerPath, "triggerId", issues);
            if (triggerId != null && (!ids.Add(triggerId) || !aliases.Add(MortalLocationIdentityState.BuildConfusableKey(triggerId))))
                Add(issues, triggerPath + ".triggerId", "effect_materialization_invalid_field", "unique exact/confusable triggerId", triggerId);
            RequireClosedString(trigger, triggerPath, "eventType", EventTypes, issues);
            RequireBoundedInt(trigger, triggerPath, "priority", -10_000, 10_000, issues);
            RequireBoolean(trigger, triggerPath, "consumeUses", issues);
            RequireClosedString(trigger, triggerPath, "resolutionMode", ResolutionModes, issues);
            ValidateComponentReferences(trigger, triggerPath, componentIds, issues);
        }
    }

    private static void ValidateComponentReferences(
        JsonElement trigger,
        string path,
        IReadOnlySet<string> componentIds,
        List<ValidationIssue> issues)
    {
        if (!trigger.TryGetProperty("componentIds", out var refs) || refs.ValueKind != JsonValueKind.Array || refs.GetArrayLength() == 0)
        {
            Add(issues, path + ".componentIds", "effect_materialization_invalid_field", "non-empty componentId array", Describe(trigger, "componentIds"));
            return;
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in refs.EnumerateArray())
        {
            var itemPath = $"{path}.componentIds[{index++}]";
            if (!TryReadExactIdentifier(item, out var id) || !seen.Add(id) || !componentIds.Contains(id))
                Add(issues, itemPath, "effect_materialization_invalid_field", "one unique componentId from this effect", item.GetRawText());
        }
    }

    private static void ValidateRemoval(JsonElement root, string path, List<ValidationIssue> issues)
    {
        if (!TryGetObject(root, path, "removal", issues, out var removal))
            return;
        var removalPath = path + ".removal";
        ValidateClosedObject(removal, removalPath, Set("dispelCategories", "cureKinds", "onSourceLoss", "onConditionLoss", "manualAuthorities"), issues, "effect_materialization_unknown_field");
        RequireExactStringArray(removal, removalPath, "dispelCategories", allowEmpty: true, issues);
        RequireExactStringArray(removal, removalPath, "cureKinds", allowEmpty: true, issues);
        RequireClosedString(removal, removalPath, "onSourceLoss", SourceLossPolicies, issues);
        ValidateNullableClosedString(removal, removalPath, "onConditionLoss", Set("expire", "suspend", "no_change"), required: false, issues);
        RequireExactStringArray(removal, removalPath, "manualAuthorities", allowEmpty: true, issues);
    }

    private static void ValidateLinks(JsonElement root, string path, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("links", out var links) || links.ValueKind != JsonValueKind.Array)
        {
            Add(issues, path + ".links", "effect_materialization_invalid_field", "link array", Describe(root, "links"));
            return;
        }

        var coordinates = new HashSet<string>(StringComparer.Ordinal);
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var link in links.EnumerateArray())
        {
            var linkPath = $"{path}.links[{index++}]";
            if (link.ValueKind != JsonValueKind.Object)
            {
                Add(issues, linkPath, "effect_materialization_invalid_field", "closed link object", link.GetRawText());
                continue;
            }
            ValidateClosedObject(link, linkPath, Set("kind", "targetId", "role"), issues, "effect_materialization_unknown_field");
            var kind = RequireClosedString(link, linkPath, "kind", LinkKinds, issues);
            var targetId = RequireExactIdentifier(link, linkPath, "targetId", issues);
            var role = RequireClosedString(link, linkPath, "role", LinkRoles, issues);
            if (kind == null || targetId == null || role == null)
                continue;
            var coordinate = kind + "\u001f" + targetId + "\u001f" + role;
            var alias = kind + "\u001f" + MortalLocationIdentityState.BuildConfusableKey(targetId) + "\u001f" + role;
            if (!coordinates.Add(coordinate) || !aliases.Add(alias))
                Add(issues, linkPath, "effect_materialization_invalid_field", "unique exact/confusable link", link.GetRawText());
        }
    }

    private static void ValidateChronology(JsonElement root, string path, List<ValidationIssue> issues)
    {
        if (!TryGetObject(root, path, "chronology", issues, out var chronology))
            return;
        var chronologyPath = path + ".chronology";
        ValidateClosedObject(chronology, chronologyPath, Set("createdAtTurn", "createdEventRef", "causalEventRef", "lastTransitionId", "lastTransitionTurn"), issues, "effect_materialization_unknown_field");
        var created = RequirePositiveInt(chronology, chronologyPath, "createdAtTurn", issues);
        RequireExactIdentifier(chronology, chronologyPath, "createdEventRef", issues);
        if (chronology.TryGetProperty("causalEventRef", out _))
            RequireExactIdentifier(chronology, chronologyPath, "causalEventRef", issues);
        RequireExactIdentifier(chronology, chronologyPath, "lastTransitionId", issues);
        var last = RequirePositiveInt(chronology, chronologyPath, "lastTransitionTurn", issues);
        if (created.HasValue && last.HasValue && last < created)
            Add(issues, chronologyPath + ".lastTransitionTurn", "effect_materialization_invalid_field", "turn >= createdAtTurn", last.Value.ToString());
    }

    private static bool TryGetObject(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues,
        out JsonElement value)
    {
        if (root.TryGetProperty(field, out value) && value.ValueKind == JsonValueKind.Object)
            return true;
        Add(issues, path + "." + field, "effect_materialization_invalid_field", "object", Describe(root, field));
        return false;
    }

    private static void RequireObject(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.Object)
            Add(issues, path + "." + field, "effect_materialization_invalid_field", "object", Describe(root, field));
    }

    private static string? RequireClosedString(
        JsonElement root,
        string path,
        string field,
        IReadOnlySet<string> allowed,
        List<ValidationIssue> issues)
    {
        var value = RequireExactIdentifier(root, path, field, issues);
        if (value != null && !allowed.Contains(value))
        {
            Add(issues, path + "." + field, "effect_materialization_invalid_field", string.Join(" | ", allowed.OrderBy(static item => item, StringComparer.Ordinal)), value);
            return null;
        }
        return value;
    }

    private static void ValidateNullableClosedString(
        JsonElement root,
        string path,
        string field,
        IReadOnlySet<string> allowed,
        bool required,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            if (required)
                Add(issues, path + "." + field, "effect_materialization_invalid_field", string.Join(" | ", allowed), "null or missing");
            return;
        }
        if (!TryReadExactIdentifier(value, out var text) || !allowed.Contains(text))
            Add(issues, path + "." + field, "effect_materialization_invalid_field", string.Join(" | ", allowed), value.GetRawText());
    }

    private static string? RequireExactIdentifier(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(field, out var value) && TryReadExactIdentifier(value, out var text))
            return text;
        Add(issues, path + "." + field, "effect_materialization_invalid_field", "exact non-empty string without surrounding whitespace", Describe(root, field));
        return null;
    }

    private static void RequireReadableString(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            Add(issues, path + "." + field, "effect_materialization_invalid_field", "non-empty player-readable string", Describe(root, field));
        }
    }

    private static void ValidateOptionalReadableString(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null)
            return;
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            Add(issues, path + "." + field, "effect_materialization_invalid_field", "non-empty string or null", value.GetRawText());
    }

    private static int? RequirePositiveInt(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var number) && number > 0)
        {
            return number;
        }
        Add(issues, path + "." + field, "effect_materialization_invalid_field", "positive integer", Describe(root, field));
        return null;
    }

    private static long? RequireNonNegativeLong(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(field, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt64(out var number) &&
            number >= 0)
        {
            return number;
        }
        Add(issues, path + "." + field, "effect_materialization_invalid_field", "non-negative canonical world-time integer", Describe(root, field));
        return null;
    }

    private static void RequireBoundedInt(
        JsonElement root,
        string path,
        string field,
        int minimum,
        int maximum,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var number) || number < minimum || number > maximum)
        {
            Add(issues, path + "." + field, "effect_materialization_invalid_field", $"integer from {minimum} through {maximum}", Describe(root, field));
        }
    }

    private static void RequireExactInt(
        JsonElement root,
        string path,
        string field,
        int expected,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var actual) || actual != expected)
        {
            Add(issues, path + "." + field, "effect_materialization_invalid_field", expected.ToString(), Describe(root, field));
        }
    }

    private static void RequireBoolean(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) || value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            Add(issues, path + "." + field, "effect_materialization_invalid_field", "boolean", Describe(root, field));
    }

    private static void RequireExactStringArray(
        JsonElement root,
        string path,
        string field,
        bool allowEmpty,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var array) || array.ValueKind != JsonValueKind.Array ||
            (!allowEmpty && array.GetArrayLength() == 0))
        {
            Add(issues, path + "." + field, "effect_materialization_invalid_field", allowEmpty ? "exact string array" : "non-empty exact string array", Describe(root, field));
            return;
        }
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var itemPath = $"{path}.{field}[{index++}]";
            if (!TryReadExactIdentifier(item, out var value) || !exact.Add(value) ||
                !aliases.Add(MortalLocationIdentityState.BuildConfusableKey(value)))
            {
                Add(issues, itemPath, "effect_materialization_invalid_field", "one unique exact/confusable non-empty string", item.GetRawText());
            }
        }
    }

    private static void ValidateClosedObject(
        JsonElement value,
        string path,
        IReadOnlySet<string> allowed,
        List<ValidationIssue> issues,
        string code)
    {
        foreach (var property in value.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
                Add(issues, path + "." + property.Name, code, "registered current-schema field", property.Name);
        }
    }

    private static void FindDuplicateProperties(
        JsonElement value,
        string path,
        List<ValidationIssue> issues,
        string code)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in value.EnumerateObject())
                {
                    var propertyPath = path + "." + property.Name;
                    if (!seen.Add(property.Name))
                        Add(issues, propertyPath, code, "one occurrence of each exact property", property.Name);
                    FindDuplicateProperties(property.Value, propertyPath, issues, code);
                }
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in value.EnumerateArray())
                    FindDuplicateProperties(item, $"{path}[{index++}]", issues, code);
                break;
        }
    }

    private static bool TryReadExactIdentifier(JsonElement root, string field, out string value)
    {
        value = string.Empty;
        return root.TryGetProperty(field, out var element) && TryReadExactIdentifier(element, out value);
    }

    private static bool TryReadExactIdentifier(JsonElement element, out string value)
    {
        value = element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : string.Empty;
        return value.Length > 0 && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }

    private static bool HasExactFields(JsonElement root, IReadOnlySet<string> expected) =>
        root.ValueKind == JsonValueKind.Object &&
        root.EnumerateObject().Select(static property => property.Name).ToHashSet(StringComparer.Ordinal).SetEquals(expected);

    private static HashSet<string> Set(params string[] values) => new(values, StringComparer.Ordinal);

    private static string Describe(JsonElement root, string field) =>
        root.TryGetProperty(field, out var value) ? value.GetRawText() : "missing";

    private static void Add(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Active effect violates the complete current materialization contract.",
            code: code,
            section: "effect_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Resubmit one complete current-schema effect operation without legacy, unknown, or client-owned fields."));
}
