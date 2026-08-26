using System.Text.Json;

namespace BookOfEternityClient.Services;

internal static class EffectSourceDefinitionContract
{
    internal const string CanonicalWorldTimeAuthority = "world_time.currentTimeInMinutes";

    private sealed record ComponentValidationResult(
        HashSet<string> ParameterNames,
        HashSet<string> ComponentIds,
        HashSet<string> Profiles);

    private static readonly HashSet<string> DefinitionFields = Set(
        "schemaVersion", "definitionKey", "display", "allowedRealms", "allowedTargetKinds",
        "components", "parameterBounds", "stacking", "lifetime", "triggers", "removal", "links");

    private static readonly HashSet<string> RequiredFields = new(DefinitionFields, StringComparer.Ordinal);

    private static readonly HashSet<string> ClientOwnedFields = Set(
        "effectId", "state", "currentStacks", "remainingTurns", "remainingUses", "chronology",
        "materialization", "materializationReceipt", "materializationId", "receipt", "receiptId",
        "receiptSeal", "transitionId", "transitions", "identityIndex", "carrierPath",
        "terminalState", "terminalReason", "createdAtTurn", "lastTransitionId", "lastTransitionTurn");

    private static readonly HashSet<string> Realms = Set("mortal_world", "chaos_sea", "shining_abode");
    private static readonly HashSet<string> TargetKinds = Set(
        "player", "npc", "combatant", "guardian", "resident", "radiant_actor",
        "afterlife_actor", "spiritual_conflict_side");
    private static readonly HashSet<string> SpiritualWoundRealms = Set(
        "chaos_sea", "shining_abode");
    private static readonly HashSet<string> SpiritualWoundTargetKinds = Set(
        "player", "guardian", "resident", "radiant_actor", "afterlife_actor");
    private static readonly HashSet<string> Categories = Set("buff", "debuff", "condition", "environmental", "mixed");
    private static readonly HashSet<string> Visibilities = Set("visible", "hidden", "gm_only");
    private static readonly HashSet<string> StackPolicies = Set("independent", "stack", "refresh", "replace", "merge");
    private static readonly HashSet<string> AtMaximumPolicies = Set("no_change", "refresh", "component_response");
    private static readonly HashSet<string> RefreshModes = Set("reset", "extend");
    private static readonly HashSet<string> MergeRules = Set("sum", "minimum", "maximum", "profile_specific");
    private static readonly HashSet<string> LifetimeModes = Set(
        "turns", "uses", "until_time", "scene", "source_bound", "condition_bound", "permanent", "manual");
    private static readonly HashSet<string> AdvancePhases = Set(
        "owner_turn_start", "owner_turn_end", "world_turn_start", "world_turn_end",
        "afterlife_exchange_end");
    private static readonly IReadOnlySet<string> EventTypes =
        EffectEventTypeCatalog.Registered;
    private static readonly HashSet<string> ResolutionModes = Set("deterministic", "bounded_receipt");
    private static readonly HashSet<string> LossPolicies = Set("expire", "suspend", "no_change");
    private static readonly HashSet<string> LinkKinds = Set(
        "wound", "skill", "spiritual_art", "item", "quest", "location", "hazard", "faction",
        "world_event", "fate_card", "combat");
    private static readonly HashSet<string> LinkRoles = Set("source", "condition", "context", "cleanup_companion");

    internal static IReadOnlyList<ValidationIssue> ValidateArray(
        JsonElement definitions,
        string path,
        string realm)
    {
        var issues = new List<ValidationIssue>();
        if (definitions.ValueKind != JsonValueKind.Array)
        {
            Add(issues, path, "effect_source_definition_invalid_field", "activeEffectDefinitions array", definitions.ValueKind.ToString());
            return issues;
        }
        if (!Realms.Contains(realm))
        {
            Add(issues, path, "effect_source_definition_invalid_field", "current registered source realm", realm);
            return issues;
        }

        FindDuplicateProperties(definitions, path, issues);
        var exactKeys = new HashSet<string>(StringComparer.Ordinal);
        var aliasKeys = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var definition in definitions.EnumerateArray())
        {
            var definitionPath = $"{path}[{index++}]";
            ValidateDefinition(definition, definitionPath, realm, exactKeys, aliasKeys, issues);
        }
        issues.AddRange(EffectReactionContract.ValidateDefinitionGraph(
            definitions,
            path));
        return issues;
    }

    private static void ValidateDefinition(
        JsonElement definition,
        string path,
        string realm,
        HashSet<string> exactKeys,
        HashSet<string> aliasKeys,
        List<ValidationIssue> issues)
    {
        if (definition.ValueKind != JsonValueKind.Object)
        {
            Add(issues, path, "effect_source_definition_invalid_field", "closed source definition object", definition.ValueKind.ToString());
            return;
        }

        foreach (var property in definition.EnumerateObject())
        {
            if (ClientOwnedFields.Contains(property.Name))
            {
                Add(issues, path + "." + property.Name, "effect_source_definition_client_field_forbidden", "field absent from static source authority", property.Value.GetRawText());
            }
            else if (!DefinitionFields.Contains(property.Name))
            {
                Add(issues, path + "." + property.Name, "effect_source_definition_unknown_field", "registered static definition field", property.Name);
            }
        }

        foreach (var field in RequiredFields)
        {
            if (!definition.TryGetProperty(field, out _))
                Add(issues, path + "." + field, "effect_source_definition_missing_field", "required complete source policy field", "missing");
        }

        RequireExactInt(definition, path, "schemaVersion", EffectMaterializationContract.SchemaVersion, issues);
        var definitionKey = RequireExactIdentifier(definition, path, "definitionKey", issues);
        if (definitionKey != null)
        {
            if (!exactKeys.Add(definitionKey))
            {
                Add(issues, path + ".definitionKey", "effect_source_definition_duplicate_key", "one exact definitionKey per source", definitionKey);
            }
            else if (!aliasKeys.Add(MortalLocationIdentityState.BuildConfusableKey(definitionKey)))
            {
                Add(issues, path + ".definitionKey", "effect_source_definition_confusable_key", "one exact/confusable definitionKey per source", definitionKey);
            }
        }

        ValidateDisplay(definition, path, issues);
        ValidateClosedStringArray(definition, path, "allowedRealms", Realms, requireRealm: realm, issues);
        ValidateClosedStringArray(definition, path, "allowedTargetKinds", TargetKinds, requireRealm: null, issues);
        var componentValidation = ValidateComponents(definition, path, issues);
        ValidateParameterBounds(
            definition,
            path,
            componentValidation.ParameterNames,
            issues);
        ValidateStacking(
            definition,
            path,
            componentValidation.Profiles,
            issues);
        var consumingEventTypes = ValidateLifetime(definition, path, issues);
        ValidateAfterlifeConditionAdapter(
            definition,
            path,
            realm,
            componentValidation,
            issues);
        ValidateTriggers(
            definition,
            path,
            componentValidation.ComponentIds,
            consumingEventTypes,
            issues);
        ValidateRemoval(definition, path, issues);
        ValidateLinks(definition, path, issues);
        ValidateSpiritualWoundAdapter(
            definition,
            path,
            realm,
            componentValidation,
            issues);
    }

    private static void ValidateAfterlifeConditionAdapter(
        JsonElement definition,
        string path,
        string realm,
        ComponentValidationResult components,
        List<ValidationIssue> issues)
    {
        if (!components.Profiles.Contains("afterlife_combat_condition"))
            return;

        var isOneSpecializedComponent = components.ComponentIds.Count == 1 &&
            components.Profiles.Count == 1;
        var isAfterlifeRealm = realm is "chaos_sea" or "shining_abode";
        var hasOnlyAfterlifeRealms = definition.TryGetProperty(
                "allowedRealms",
                out var allowedRealms) &&
            allowedRealms.ValueKind == JsonValueKind.Array &&
            allowedRealms.GetArrayLength() > 0 &&
            allowedRealms.EnumerateArray().All(value =>
                TryReadExactIdentifier(value, out var allowedRealm) &&
                allowedRealm is "chaos_sea" or "shining_abode");
        var hasOnlySpiritualTargets = definition.TryGetProperty(
                "allowedTargetKinds",
                out var allowedTargets) &&
            allowedTargets.ValueKind == JsonValueKind.Array &&
            allowedTargets.GetArrayLength() == 1 &&
            TryReadExactIdentifier(
                allowedTargets.EnumerateArray().Single(),
                out var allowedTarget) &&
            string.Equals(
                allowedTarget,
                "spiritual_conflict_side",
                StringComparison.Ordinal);
        var hasSupportedLifetime = false;
        if (definition.TryGetProperty("lifetime", out var lifetime) &&
            lifetime.ValueKind == JsonValueKind.Object &&
            TryReadExactIdentifier(lifetime, "mode", out var mode))
        {
            hasSupportedLifetime = mode switch
            {
                "turns" => TryReadExactIdentifier(
                        lifetime,
                        "advancePhase",
                        out var phase) &&
                    string.Equals(
                        phase,
                        "afterlife_exchange_end",
                        StringComparison.Ordinal),
                "uses" => lifetime.TryGetProperty(
                        "consumingEventTypes",
                        out var consumingTypes) &&
                    consumingTypes.ValueKind == JsonValueKind.Array &&
                    consumingTypes.GetArrayLength() == 1 &&
                    TryReadExactIdentifier(
                        consumingTypes.EnumerateArray().Single(),
                        out var consumingType) &&
                    string.Equals(
                        consumingType,
                        "afterlife_exchange_end",
                        StringComparison.Ordinal),
                "scene" => true,
                _ => false
            };
        }

        if (isOneSpecializedComponent &&
            isAfterlifeRealm &&
            hasOnlyAfterlifeRealms &&
            hasOnlySpiritualTargets &&
            hasSupportedLifetime)
        {
            return;
        }

        Add(
            issues,
            path + ".lifetime",
            "effect_source_definition_afterlife_condition_lifetime_invalid",
            "one afterlife_combat_condition component targeting only spiritual_conflict_side with bounded uses, afterlife exchanges, or scene lifetime",
            definition.GetRawText());
    }

    private static void ValidateSpiritualWoundAdapter(
        JsonElement definition,
        string path,
        string realm,
        ComponentValidationResult components,
        List<ValidationIssue> issues)
    {
        if (!components.Profiles.Any(
                SpiritualWoundEffectProfileCatalog.RegisteredProfiles.Contains))
        {
            return;
        }

        var hasOnlyAfterlifeRealms =
            definition.TryGetProperty("allowedRealms", out var allowedRealms) &&
            IsNonEmptySubset(allowedRealms, SpiritualWoundRealms) &&
            ContainsExactValue(allowedRealms, realm);
        if (!SpiritualWoundRealms.Contains(realm) || !hasOnlyAfterlifeRealms)
        {
            Add(
                issues,
                path + ".allowedRealms",
                "effect_source_definition_spiritual_wound_realm_invalid",
                "non-empty subset of chaos_sea | shining_abode containing the current afterlife realm",
                Describe(definition, "allowedRealms"));
        }

        if (!definition.TryGetProperty("allowedTargetKinds", out var allowedTargets) ||
            !IsNonEmptySubset(allowedTargets, SpiritualWoundTargetKinds))
        {
            Add(
                issues,
                path + ".allowedTargetKinds",
                "effect_source_definition_spiritual_wound_target_invalid",
                "non-empty subset of player | guardian | resident | radiant_actor | afterlife_actor",
                Describe(definition, "allowedTargetKinds"));
        }

        ValidateSpiritualWoundLinks(definition, path, issues);
    }

    private static bool IsNonEmptySubset(
        JsonElement values,
        IReadOnlySet<string> allowed)
    {
        if (values.ValueKind != JsonValueKind.Array || values.GetArrayLength() == 0)
            return false;

        var exact = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values.EnumerateArray())
        {
            if (!TryReadExactIdentifier(value, out var text) ||
                !allowed.Contains(text) ||
                !exact.Add(text))
            {
                return false;
            }
        }
        return true;
    }

    private static bool ContainsExactValue(JsonElement values, string expected) =>
        values.ValueKind == JsonValueKind.Array &&
        values.EnumerateArray().Any(value =>
            TryReadExactIdentifier(value, out var text) &&
            string.Equals(text, expected, StringComparison.Ordinal));

    private static void ValidateSpiritualWoundLinks(
        JsonElement definition,
        string path,
        List<ValidationIssue> issues)
    {
        if (!definition.TryGetProperty("links", out var links) ||
            links.ValueKind != JsonValueKind.Array ||
            links.GetArrayLength() == 0)
        {
            AddSpiritualWoundLinkIssue(
                issues,
                path + ".links",
                Describe(definition, "links"));
            return;
        }

        var sourceCount = 0;
        var sourceAliases = new HashSet<string>(StringComparer.Ordinal);
        var emittedTargetedIssue = false;
        string? firstNonSourceWoundRolePath = null;
        string? firstNonSourceWoundRole = null;
        var index = 0;
        foreach (var link in links.EnumerateArray())
        {
            var linkPath = $"{path}.links[{index++}]";
            if (link.ValueKind != JsonValueKind.Object)
                continue;

            var hasKind = TryReadExactIdentifier(link, "kind", out var kind);
            var hasRole = TryReadExactIdentifier(link, "role", out var role);
            if (hasRole && string.Equals(role, "source", StringComparison.Ordinal) &&
                (!hasKind || !string.Equals(kind, "wound", StringComparison.Ordinal)))
            {
                AddSpiritualWoundLinkIssue(
                    issues,
                    linkPath + ".kind",
                    Describe(link, "kind"));
                emittedTargetedIssue = true;
                continue;
            }

            if (!hasKind || !string.Equals(kind, "wound", StringComparison.Ordinal))
                continue;

            if (hasRole &&
                (string.Equals(role, "context", StringComparison.Ordinal) ||
                 string.Equals(role, "condition", StringComparison.Ordinal)))
            {
                firstNonSourceWoundRolePath ??= linkPath + ".role";
                firstNonSourceWoundRole ??= role;
                continue;
            }

            if (!hasRole || !string.Equals(role, "source", StringComparison.Ordinal))
            {
                AddSpiritualWoundLinkIssue(
                    issues,
                    linkPath + ".role",
                    Describe(link, "role"));
                emittedTargetedIssue = true;
                continue;
            }

            sourceCount++;
            if (!TryReadExactIdentifier(link, "targetId", out var targetId) ||
                sourceCount > 1 ||
                !sourceAliases.Add(MortalLocationIdentityState.BuildConfusableKey(targetId)))
            {
                AddSpiritualWoundLinkIssue(
                    issues,
                    linkPath,
                    link.GetRawText());
                emittedTargetedIssue = true;
            }
        }

        if (sourceCount == 0 && !emittedTargetedIssue)
        {
            AddSpiritualWoundLinkIssue(
                issues,
                firstNonSourceWoundRolePath ?? path + ".links",
                firstNonSourceWoundRole ?? links.GetRawText());
        }
    }

    private static void AddSpiritualWoundLinkIssue(
        List<ValidationIssue> issues,
        string path,
        string actual) =>
        Add(
            issues,
            path,
            "effect_source_definition_spiritual_wound_link_invalid",
            "exactly one exact/confusable-unique wound link with role source; independent context siblings may remain",
            actual);

    private static void ValidateDisplay(JsonElement root, string path, List<ValidationIssue> issues)
    {
        if (!TryGetObject(root, path, "display", issues, out var display))
            return;
        var displayPath = path + ".display";
        ValidateClosedObject(display, displayPath, Set("name", "description", "category", "visibility"), issues);
        RequireReadableString(display, displayPath, "name", issues);
        RequireReadableString(display, displayPath, "description", issues);
        RequireClosedString(display, displayPath, "category", Categories, issues);
        RequireClosedString(display, displayPath, "visibility", Visibilities, issues);
    }

    private static ComponentValidationResult ValidateComponents(
        JsonElement root,
        string path,
        List<ValidationIssue> issues)
    {
        var parameterNames = new HashSet<string>(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var profiles = new HashSet<string>(StringComparer.Ordinal);
        if (!root.TryGetProperty("components", out var components) ||
            components.ValueKind != JsonValueKind.Array ||
            components.GetArrayLength() == 0)
        {
            Add(issues, path + ".components", "effect_source_definition_invalid_components", "non-empty registered component template array", Describe(root, "components"));
            return new ComponentValidationResult(parameterNames, ids, profiles);
        }

        var componentIssues = new List<ValidationIssue>();
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var component in components.EnumerateArray())
        {
            var componentPath = $"{path}.components[{index++}]";
            EffectComponentProfiles.ValidateComponent(component, componentPath, componentIssues);
            if (component.ValueKind != JsonValueKind.Object)
                continue;
            if (TryReadExactIdentifier(component, "componentId", out var componentId) &&
                (!ids.Add(componentId) || !aliases.Add(MortalLocationIdentityState.BuildConfusableKey(componentId))))
            {
                Add(issues, componentPath + ".componentId", "effect_source_definition_invalid_components", "unique exact/confusable componentId", componentId);
            }
            if (TryReadExactIdentifier(component, "profile", out var profile))
                profiles.Add(profile);
            if (component.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in payload.EnumerateObject())
                    parameterNames.Add(property.Name);
            }
        }

        foreach (var issue in componentIssues)
        {
            Add(
                issues,
                issue.FilePath,
                "effect_source_definition_invalid_components",
                issue.Expected ?? "complete registered component template",
                issue.Actual ?? issue.Message);
        }
        return new ComponentValidationResult(parameterNames, ids, profiles);
    }

    private static void ValidateParameterBounds(
        JsonElement root,
        string path,
        IReadOnlySet<string> componentParameters,
        List<ValidationIssue> issues)
    {
        if (!TryGetObject(root, path, "parameterBounds", issues, out var bounds))
            return;
        var boundsPath = path + ".parameterBounds";
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in bounds.EnumerateObject())
        {
            var parameterPath = boundsPath + "." + parameter.Name;
            if (parameter.Name.Length == 0 || !string.Equals(parameter.Name, parameter.Name.Trim(), StringComparison.Ordinal) ||
                !aliases.Add(MortalLocationIdentityState.BuildConfusableKey(parameter.Name)) ||
                !componentParameters.Contains(parameter.Name))
            {
                Add(issues, parameterPath, "effect_source_definition_invalid_parameter_bound", "one exact component payload parameter", parameter.Name);
            }
            ValidateParameterBound(parameter.Value, parameterPath, issues);
        }
    }

    private static void ValidateParameterBound(
        JsonElement bound,
        string path,
        List<ValidationIssue> issues)
    {
        if (bound.ValueKind != JsonValueKind.Object)
        {
            Add(issues, path, "effect_source_definition_invalid_parameter_bound", "closed parameter bound object", bound.GetRawText());
            return;
        }
        ValidateClosedObject(bound, path, Set("kind", "minimum", "maximum", "allowedValues", "required"), issues, "effect_source_definition_invalid_parameter_bound");
        var kind = RequireClosedString(bound, path, "kind", Set("number", "integer", "enum", "identity", "boolean"), issues, "effect_source_definition_invalid_parameter_bound");
        if (kind is "number" or "integer")
        {
            var hasMinimum = TryReadFinite(bound, "minimum", out var minimum);
            var hasMaximum = TryReadFinite(bound, "maximum", out var maximum);
            if (!hasMinimum)
                Add(issues, path + ".minimum", "effect_source_definition_invalid_parameter_bound", "finite minimum", Describe(bound, "minimum"));
            if (!hasMaximum)
                Add(issues, path + ".maximum", "effect_source_definition_invalid_parameter_bound", "finite maximum", Describe(bound, "maximum"));
            if (hasMinimum && hasMaximum && minimum > maximum)
                Add(issues, path, "effect_source_definition_invalid_parameter_bound", "minimum <= maximum", bound.GetRawText());
            if (kind == "integer" &&
                (hasMinimum && minimum != Math.Truncate(minimum) || hasMaximum && maximum != Math.Truncate(maximum)))
            {
                Add(issues, path, "effect_source_definition_invalid_parameter_bound", "integer minimum and maximum", bound.GetRawText());
            }
        }
        else if (kind == "enum")
        {
            RequireExactStringArray(bound, path, "allowedValues", allowEmpty: false, issues, "effect_source_definition_invalid_parameter_bound");
        }
        else if (bound.TryGetProperty("allowedValues", out var values) && values.ValueKind != JsonValueKind.Null)
        {
            Add(issues, path + ".allowedValues", "effect_source_definition_invalid_parameter_bound", "field absent or null for this bound kind", values.GetRawText());
        }

        if (bound.TryGetProperty("required", out var required) && required.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            Add(issues, path + ".required", "effect_source_definition_invalid_parameter_bound", "boolean when present", required.GetRawText());
    }

    private static void ValidateStacking(
        JsonElement root,
        string path,
        IReadOnlySet<string> componentProfiles,
        List<ValidationIssue> issues)
    {
        if (!TryGetObject(root, path, "stacking", issues, out var stacking))
            return;
        var stackingPath = path + ".stacking";
        ValidateClosedObject(stacking, stackingPath, Set("stackKey", "policy", "maxStacks", "atMaximum", "refreshMode", "mergeRule"), issues);
        RequireExactIdentifier(stacking, stackingPath, "stackKey", issues);
        var policy = RequireClosedString(stacking, stackingPath, "policy", StackPolicies, issues);
        RequirePositiveInt(stacking, stackingPath, "maxStacks", issues);
        RequireClosedString(stacking, stackingPath, "atMaximum", AtMaximumPolicies, issues);
        ValidateNullableClosedString(stacking, stackingPath, "refreshMode", RefreshModes, policy == "refresh", issues);
        var mergeRule = ValidateNullableClosedString(
            stacking,
            stackingPath,
            "mergeRule",
            MergeRules,
            policy == "merge",
            issues);
        if (policy == "merge" && mergeRule != null)
        {
            foreach (var profile in componentProfiles.OrderBy(
                         static value => value,
                         StringComparer.Ordinal))
            {
                if (EffectComponentProfiles.TryGetDescriptor(profile, out var descriptor) &&
                    !descriptor.LegalMergeReducers.Contains(mergeRule))
                {
                    Add(
                        issues,
                        stackingPath + ".mergeRule",
                        "effect_source_definition_invalid_merge_rule",
                        $"merge reducer registered for component profile {profile}",
                        mergeRule);
                }
            }
        }
    }

    private static HashSet<string>? ValidateLifetime(
        JsonElement root,
        string path,
        List<ValidationIssue> issues)
    {
        if (!TryGetObject(root, path, "lifetime", issues, out var lifetime))
            return null;
        var lifetimePath = path + ".lifetime";
        var mode = RequireClosedString(lifetime, lifetimePath, "mode", LifetimeModes, issues);
        if (mode == null)
            return null;

        switch (mode)
        {
            case "turns":
                ValidateClosedObject(lifetime, lifetimePath, Set("mode", "initialTurns", "advancePhase"), issues);
                RequirePositiveInt(lifetime, lifetimePath, "initialTurns", issues);
                RequireClosedString(lifetime, lifetimePath, "advancePhase", AdvancePhases, issues);
                break;
            case "uses":
                ValidateClosedObject(lifetime, lifetimePath, Set("mode", "initialUses", "consumingEventTypes"), issues);
                RequirePositiveInt(lifetime, lifetimePath, "initialUses", issues);
                ValidateClosedStringArray(lifetime, lifetimePath, "consumingEventTypes", EventTypes, requireRealm: null, issues);
                return ReadRegisteredStringSet(
                    lifetime,
                    "consumingEventTypes",
                    EventTypes);
            case "until_time":
                ValidateClosedObject(lifetime, lifetimePath, Set("mode", "duration", "timeAuthority"), issues);
                RequirePositiveInt(lifetime, lifetimePath, "duration", issues);
                RequireClosedString(
                    lifetime,
                    lifetimePath,
                    "timeAuthority",
                    Set(CanonicalWorldTimeAuthority),
                    issues);
                break;
            case "scene":
                ValidateClosedObject(lifetime, lifetimePath, Set("mode", "onSceneExit"), issues);
                RequireClosedString(lifetime, lifetimePath, "onSceneExit", Set("expire", "suspend"), issues);
                break;
            case "source_bound":
                ValidateClosedObject(lifetime, lifetimePath, Set("mode", "activePredicate", "onSourceLoss"), issues);
                var activePredicate = RequireExactIdentifier(
                    lifetime,
                    lifetimePath,
                    "activePredicate",
                    issues);
                if (activePredicate != null &&
                    !EffectSourcePredicateCatalog.IsRegistered(activePredicate))
                {
                    Add(
                        issues,
                        lifetimePath + ".activePredicate",
                        "effect_source_definition_invalid_active_predicate",
                        "registered active predicate: active, carried, equipped, or unlocked",
                        activePredicate);
                }
                RequireClosedString(lifetime, lifetimePath, "onSourceLoss", Set("expire", "suspend"), issues);
                break;
            case "condition_bound":
                ValidateClosedObject(lifetime, lifetimePath, Set("mode", "conditionKey", "operandBounds", "onConditionLoss"), issues);
                RequireExactIdentifier(lifetime, lifetimePath, "conditionKey", issues);
                RequireObject(lifetime, lifetimePath, "operandBounds", issues);
                RequireClosedString(lifetime, lifetimePath, "onConditionLoss", Set("expire", "suspend"), issues);
                break;
            case "permanent":
                ValidateClosedObject(lifetime, lifetimePath, Set("mode"), issues);
                break;
            case "manual":
                ValidateClosedObject(lifetime, lifetimePath, Set("mode", "authorities"), issues);
                RequireExactStringArray(lifetime, lifetimePath, "authorities", allowEmpty: false, issues);
                break;
        }
        return null;
    }

    private static void ValidateTriggers(
        JsonElement root,
        string path,
        IReadOnlySet<string> componentIds,
        IReadOnlySet<string>? consumingEventTypes,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("triggers", out var triggers) || triggers.ValueKind != JsonValueKind.Array)
        {
            Add(issues, path + ".triggers", "effect_source_definition_invalid_field", "trigger template array", Describe(root, "triggers"));
            return;
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        var seenConsumingEventTypes = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var trigger in triggers.EnumerateArray())
        {
            var triggerPath = $"{path}.triggers[{index++}]";
            if (trigger.ValueKind != JsonValueKind.Object)
            {
                Add(issues, triggerPath, "effect_source_definition_invalid_field", "closed trigger template", trigger.GetRawText());
                continue;
            }
            ValidateClosedObject(trigger, triggerPath, Set("triggerId", "eventType", "priority", "componentIds", "consumeUses", "resolutionMode"), issues);
            var id = RequireExactIdentifier(trigger, triggerPath, "triggerId", issues);
            if (id != null && (!ids.Add(id) || !aliases.Add(MortalLocationIdentityState.BuildConfusableKey(id))))
                Add(issues, triggerPath + ".triggerId", "effect_source_definition_invalid_field", "unique exact/confusable triggerId", id);
            var eventType = RequireClosedString(
                trigger,
                triggerPath,
                "eventType",
                EventTypes,
                issues);
            RequireBoundedInt(trigger, triggerPath, "priority", -10_000, 10_000, issues);
            ValidateTriggerComponentReferences(
                trigger,
                triggerPath,
                componentIds,
                issues);
            var consumesUses = trigger.TryGetProperty("consumeUses", out var consumesNode) &&
                consumesNode.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? consumesNode.GetBoolean()
                    : (bool?)null;
            RequireBoolean(trigger, triggerPath, "consumeUses", issues);
            if (eventType != null && consumesUses.HasValue &&
                consumesUses.Value != (consumingEventTypes?.Contains(eventType) == true))
            {
                Add(
                    issues,
                    triggerPath + ".consumeUses",
                    "effect_source_definition_consuming_trigger_mismatch",
                    consumingEventTypes == null
                        ? "false outside uses lifetime"
                        : "true exactly for lifetime.consumingEventTypes",
                    consumesUses.Value.ToString());
            }
            if (eventType != null && consumesUses == true)
                seenConsumingEventTypes.Add(eventType);
            RequireClosedString(trigger, triggerPath, "resolutionMode", ResolutionModes, issues);
        }

        if (consumingEventTypes != null &&
            !seenConsumingEventTypes.SetEquals(consumingEventTypes))
        {
            Add(
                issues,
                path + ".lifetime.consumingEventTypes",
                "effect_source_definition_consuming_trigger_mismatch",
                "exact event-type set represented by consumeUses=true triggers",
                string.Join(",", seenConsumingEventTypes.OrderBy(
                    static value => value,
                    StringComparer.Ordinal)));
        }
    }

    private static void ValidateTriggerComponentReferences(
        JsonElement trigger,
        string path,
        IReadOnlySet<string> componentIds,
        List<ValidationIssue> issues)
    {
        if (!trigger.TryGetProperty("componentIds", out var references) ||
            references.ValueKind != JsonValueKind.Array ||
            references.GetArrayLength() == 0)
        {
            Add(
                issues,
                path + ".componentIds",
                "effect_source_definition_invalid_trigger_component",
                "non-empty componentId array resolving inside this definition",
                Describe(trigger, "componentIds"));
            return;
        }

        var exact = new HashSet<string>(StringComparer.Ordinal);
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var reference in references.EnumerateArray())
        {
            var referencePath = $"{path}.componentIds[{index++}]";
            if (!TryReadExactIdentifier(reference, out var componentId) ||
                !exact.Add(componentId) ||
                !aliases.Add(MortalLocationIdentityState.BuildConfusableKey(componentId)) ||
                !componentIds.Contains(componentId))
            {
                Add(
                    issues,
                    referencePath,
                    "effect_source_definition_invalid_trigger_component",
                    "one unique exact componentId from this definition",
                    reference.GetRawText());
            }
        }
    }

    private static HashSet<string> ReadRegisteredStringSet(
        JsonElement root,
        string field,
        IReadOnlySet<string> allowed)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (!root.TryGetProperty(field, out var values) ||
            values.ValueKind != JsonValueKind.Array)
        {
            return result;
        }
        foreach (var value in values.EnumerateArray())
        {
            if (TryReadExactIdentifier(value, out var text) && allowed.Contains(text))
                result.Add(text);
        }
        return result;
    }

    private static void ValidateRemoval(JsonElement root, string path, List<ValidationIssue> issues)
    {
        if (!TryGetObject(root, path, "removal", issues, out var removal))
            return;
        var removalPath = path + ".removal";
        ValidateClosedObject(removal, removalPath, Set("dispelCategories", "cureKinds", "onSourceLoss", "onConditionLoss", "manualAuthorities"), issues);
        RequireExactStringArray(removal, removalPath, "dispelCategories", allowEmpty: true, issues);
        RequireExactStringArray(removal, removalPath, "cureKinds", allowEmpty: true, issues);
        RequireClosedString(removal, removalPath, "onSourceLoss", LossPolicies, issues);
        ValidateNullableClosedString(removal, removalPath, "onConditionLoss", LossPolicies, false, issues);
        RequireExactStringArray(removal, removalPath, "manualAuthorities", allowEmpty: true, issues);
    }

    private static void ValidateLinks(JsonElement root, string path, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("links", out var links) || links.ValueKind != JsonValueKind.Array)
        {
            Add(issues, path + ".links", "effect_source_definition_invalid_field", "link template array", Describe(root, "links"));
            return;
        }
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var link in links.EnumerateArray())
        {
            var linkPath = $"{path}.links[{index++}]";
            if (link.ValueKind != JsonValueKind.Object)
            {
                Add(issues, linkPath, "effect_source_definition_invalid_field", "closed link template", link.GetRawText());
                continue;
            }
            ValidateClosedObject(link, linkPath, Set("kind", "targetId", "role"), issues);
            var kind = RequireClosedString(link, linkPath, "kind", LinkKinds, issues);
            var target = RequireExactIdentifier(link, linkPath, "targetId", issues);
            var role = RequireClosedString(link, linkPath, "role", LinkRoles, issues);
            if (kind != null && target != null && role != null && !keys.Add(kind + "\u001f" + MortalLocationIdentityState.BuildConfusableKey(target) + "\u001f" + role))
                Add(issues, linkPath, "effect_source_definition_invalid_field", "unique exact/confusable link template", link.GetRawText());
        }
    }

    private static void ValidateClosedStringArray(
        JsonElement root,
        string path,
        string field,
        IReadOnlySet<string> allowed,
        string? requireRealm,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var array) || array.ValueKind != JsonValueKind.Array || array.GetArrayLength() == 0)
        {
            Add(issues, path + "." + field, "effect_source_definition_invalid_field", "non-empty registered string array", Describe(root, field));
            return;
        }
        var values = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var itemPath = $"{path}.{field}[{index++}]";
            if (!TryReadExactIdentifier(item, out var text) || !allowed.Contains(text) || !values.Add(text))
                Add(issues, itemPath, "effect_source_definition_invalid_field", "one unique registered exact value", item.GetRawText());
        }
        if (requireRealm != null && !values.Contains(requireRealm))
            Add(issues, path + "." + field, "effect_source_definition_invalid_field", $"array containing source realm {requireRealm}", array.GetRawText());
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
        Add(issues, path + "." + field, "effect_source_definition_invalid_field", "object", Describe(root, field));
        return false;
    }

    private static void RequireObject(JsonElement root, string path, string field, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.Object)
            Add(issues, path + "." + field, "effect_source_definition_invalid_field", "object", Describe(root, field));
    }

    private static string? RequireClosedString(
        JsonElement root,
        string path,
        string field,
        IReadOnlySet<string> allowed,
        List<ValidationIssue> issues,
        string code = "effect_source_definition_invalid_field")
    {
        var value = RequireExactIdentifier(root, path, field, issues, code);
        if (value != null && !allowed.Contains(value))
        {
            Add(issues, path + "." + field, code, string.Join(" | ", allowed.OrderBy(static item => item, StringComparer.Ordinal)), value);
            return null;
        }
        return value;
    }

    private static string? RequireExactIdentifier(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues,
        string code = "effect_source_definition_invalid_field")
    {
        if (root.TryGetProperty(field, out var value) && TryReadExactIdentifier(value, out var text))
            return text;
        Add(issues, path + "." + field, code, "exact non-empty string without surrounding whitespace", Describe(root, field));
        return null;
    }

    private static void RequireReadableString(JsonElement root, string path, string field, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            Add(issues, path + "." + field, "effect_source_definition_invalid_field", "non-empty player-readable string", Describe(root, field));
    }

    private static int? RequirePositiveInt(JsonElement root, string path, string field, List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number > 0)
            return number;
        Add(issues, path + "." + field, "effect_source_definition_invalid_field", "positive integer", Describe(root, field));
        return null;
    }

    private static void RequireBoundedInt(JsonElement root, string path, string field, int minimum, int maximum, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var number) || number < minimum || number > maximum)
            Add(issues, path + "." + field, "effect_source_definition_invalid_field", $"integer from {minimum} through {maximum}", Describe(root, field));
    }

    private static void RequireExactInt(JsonElement root, string path, string field, int expected, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number) || number != expected)
            Add(issues, path + "." + field, "effect_source_definition_invalid_field", expected.ToString(), Describe(root, field));
    }

    private static void RequireBoolean(JsonElement root, string path, string field, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) || value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            Add(issues, path + "." + field, "effect_source_definition_invalid_field", "boolean", Describe(root, field));
    }

    private static string? ValidateNullableClosedString(
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
                Add(issues, path + "." + field, "effect_source_definition_invalid_field", string.Join(" | ", allowed), "null or missing");
            return null;
        }
        if (!TryReadExactIdentifier(value, out var text) || !allowed.Contains(text))
        {
            Add(issues, path + "." + field, "effect_source_definition_invalid_field", string.Join(" | ", allowed), value.GetRawText());
            return null;
        }
        return text;
    }

    private static void RequireExactStringArray(
        JsonElement root,
        string path,
        string field,
        bool allowEmpty,
        List<ValidationIssue> issues,
        string code = "effect_source_definition_invalid_field")
    {
        if (!root.TryGetProperty(field, out var array) || array.ValueKind != JsonValueKind.Array || (!allowEmpty && array.GetArrayLength() == 0))
        {
            Add(issues, path + "." + field, code, allowEmpty ? "exact string array" : "non-empty exact string array", Describe(root, field));
            return;
        }
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var itemPath = $"{path}.{field}[{index++}]";
            if (!TryReadExactIdentifier(item, out var value) || !keys.Add(MortalLocationIdentityState.BuildConfusableKey(value)))
                Add(issues, itemPath, code, "one unique exact/confusable non-empty string", item.GetRawText());
        }
    }

    private static void ValidateClosedObject(
        JsonElement value,
        string path,
        IReadOnlySet<string> allowed,
        List<ValidationIssue> issues,
        string code = "effect_source_definition_invalid_field")
    {
        foreach (var property in value.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
                Add(issues, path + "." + property.Name, code, "registered current-schema field", property.Name);
        }
    }

    private static void FindDuplicateProperties(JsonElement value, string path, List<ValidationIssue> issues)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in value.EnumerateObject())
                {
                    var propertyPath = path + "." + property.Name;
                    if (!names.Add(property.Name))
                        Add(issues, propertyPath, "effect_source_definition_duplicate_property", "one occurrence of each exact property", property.Name);
                    FindDuplicateProperties(property.Value, propertyPath, issues);
                }
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in value.EnumerateArray())
                    FindDuplicateProperties(item, $"{path}[{index++}]", issues);
                break;
        }
    }

    private static bool TryReadFinite(JsonElement root, string field, out double number)
    {
        number = 0;
        return root.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Number &&
               value.TryGetDouble(out number) && double.IsFinite(number);
    }

    private static bool TryReadExactIdentifier(JsonElement root, string field, out string value)
    {
        value = string.Empty;
        return root.TryGetProperty(field, out var element) && TryReadExactIdentifier(element, out value);
    }

    private static bool TryReadExactIdentifier(JsonElement value, out string text)
    {
        text = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
        return text.Length > 0 && string.Equals(text, text.Trim(), StringComparison.Ordinal);
    }

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
            "Static active-effect definition violates its closed source-authority contract.",
            code: code,
            section: "effect_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Resubmit one complete source definition without active identity, history, receipt, or unknown policy fields."));
}
