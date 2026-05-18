using System.Text.Json;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private void ValidateAfterlifeEntityProfileStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                AfterlifeEntityProfileState.StatePath,
                IssueSeverity.Error,
                "afterlife_entity_profiles.json должен быть JSON object.",
                code: "afterlife_entity_profile_invalid_root",
                section: "AfterlifeEntityProfiles",
                expected: "object with profiles[]",
                actual: root.ValueKind.ToString()));
            return;
        }

        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "schemaVersion", "AfterlifeEntityProfiles");

        var hasProfiles = root.TryGetProperty(AfterlifeEntityProfileState.ProfilesProperty, out var profiles);
        var hasResponseProfiles = root.TryGetProperty(AfterlifeEntityProfileState.ResponseProfilesProperty, out var responseProfiles);
        var hasUpdates = root.TryGetProperty(AfterlifeEntityProfileState.UpdateProperty, out var updates);
        var hasCustomStateChanges = root.TryGetProperty(AfterlifeEntityProfileState.CustomStateChangesProperty, out var customStateChanges);
        var hasProgressionOverrides = root.TryGetProperty(AfterlifeEntityProfileState.ProgressionOverridesProperty, out var progressionOverrides);
        var hasSpecialArtLearningReceipts = root.TryGetProperty(AfterlifeEntityProfileState.SpecialArtLearningReceiptsProperty, out var specialArtLearningReceipts);
        var hasInvalidProgressionOverride = root.TryGetProperty(AfterlifeEntityProfileState.LastInvalidProgressionOverrideProperty, out _);
        if (!hasProfiles && !hasResponseProfiles && !hasUpdates && !hasCustomStateChanges && !hasProgressionOverrides && !hasSpecialArtLearningReceipts)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{AfterlifeEntityProfileState.ProfilesProperty}",
                IssueSeverity.Error,
                "afterlife_entity_profiles.json должен содержать profiles[], afterlifeEntityProfileUpdates[], afterlifeEntityCustomStateChanges[], afterlifeEntityProgressionOverrides[] или afterlifeSpecialArtLearningReceipts[].",
                code: "afterlife_entity_profile_missing_profiles",
                section: "AfterlifeEntityProfiles",
                expected: "profiles[] / afterlifeEntityProfileUpdates[] / afterlifeEntityCustomStateChanges[] / afterlifeEntityProgressionOverrides[] / afterlifeSpecialArtLearningReceipts[]"));
        }

        if (hasInvalidProgressionOverride)
        {
            var reason = root.TryGetProperty(AfterlifeEntityProfileState.LastInvalidProgressionOverrideReasonProperty, out var reasonNode)
                ? reasonNode.ToString()
                : "invalid override";
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{AfterlifeEntityProfileState.LastInvalidProgressionOverrideProperty}",
                IssueSeverity.Error,
                "afterlifeEntityProgressionOverrides не был применён: цель authority отсутствует или specialArtTierDeltas ссылается на неизвестное особое духовное искусство.",
                code: "afterlife_entity_profile_progression_override_invalid_authority",
                section: "AfterlifeEntityProfiles",
                expected: "valid target profile and known specialArts[].artId keys",
                actual: reason));
        }

        var profileAuthority = BuildAfterlifeEntityProfileAuthorityLookup(
            profiles,
            hasProfiles,
            responseProfiles,
            hasResponseProfiles,
            updates,
            hasUpdates);
        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ValidateProfileArrayIfPresent(profiles, hasProfiles, $"{contextPrefix}.{AfterlifeEntityProfileState.ProfilesProperty}", identities, issues);
        ValidateProfileArrayIfPresent(responseProfiles, hasResponseProfiles, $"{contextPrefix}.{AfterlifeEntityProfileState.ResponseProfilesProperty}", identities, issues);
        ValidateProfileArrayIfPresent(updates, hasUpdates, $"{contextPrefix}.{AfterlifeEntityProfileState.UpdateProperty}", identities, issues);
        ValidateAfterlifeEntityCustomStateChangesIfPresent(
            customStateChanges,
            hasCustomStateChanges,
            $"{contextPrefix}.{AfterlifeEntityProfileState.CustomStateChangesProperty}",
            issues);
        ValidateAfterlifeEntityProgressionOverridesIfPresent(
            progressionOverrides,
            hasProgressionOverrides,
            $"{contextPrefix}.{AfterlifeEntityProfileState.ProgressionOverridesProperty}",
            profileAuthority,
            issues);
        ValidateAfterlifeSpecialArtLearningReceiptsIfPresent(
            specialArtLearningReceipts,
            hasSpecialArtLearningReceipts,
            $"{contextPrefix}.{AfterlifeEntityProfileState.SpecialArtLearningReceiptsProperty}",
            issues);
    }

    private void ValidateProfileArrayIfPresent(
        JsonElement profiles,
        bool hasProfiles,
        string context,
        HashSet<string> identities,
        List<ValidationIssue> issues)
    {
        if (!hasProfiles)
            return;

        if (profiles.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "Профили сущностей посмертия должны быть массивом.",
                code: "afterlife_entity_profile_profiles_not_array",
                section: "AfterlifeEntityProfiles",
                expected: "array",
                actual: profiles.ValueKind.ToString()));
            return;
        }

        var index = 0;
        foreach (var profile in profiles.EnumerateArray())
        {
            ValidateAfterlifeEntityProfile(profile, $"{context}[{index++}]", identities, issues);
        }
    }

    private void ValidateAfterlifeEntityProfile(
        JsonElement profile,
        string context,
        HashSet<string> identities,
        List<ValidationIssue> issues)
    {
        if (profile.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "Каждый профиль сущности посмертия должен быть object.",
                code: "afterlife_entity_profile_entry_not_object",
                section: "AfterlifeEntityProfiles",
                expected: "object",
                actual: profile.ValueKind.ToString()));
            return;
        }

        var actorType = RequireProfileString(profile, context, "actorType", "afterlife_entity_profile_missing_actor_type", issues);
        var actorId = GetProfileString(profile, "actorId") ?? GetProfileString(profile, "actorRef");
        if (string.IsNullOrWhiteSpace(actorId))
        {
            issues.Add(new ValidationIssue(
                $"{context}.actorId",
                IssueSeverity.Error,
                "Профиль сущности посмертия должен иметь actorId или actorRef.",
                code: "afterlife_entity_profile_missing_actor_id",
                section: "AfterlifeEntityProfiles",
                expected: "non-empty actorId or actorRef"));
        }

        RequireProfileString(profile, context, "displayName", "afterlife_entity_profile_missing_display_name", issues);
        var realm = RequireProfileString(profile, context, "realm", "afterlife_entity_profile_missing_realm", issues);
        if (!string.IsNullOrWhiteSpace(actorType) && !AfterlifeEntityProfileState.ActorTypes.Contains(actorType))
        {
            issues.Add(new ValidationIssue(
                $"{context}.actorType",
                IssueSeverity.Error,
                "actorType не поддерживается для профиля сущности посмертия.",
                code: "afterlife_entity_profile_invalid_actor_type",
                section: "AfterlifeEntityProfiles",
                expected: string.Join("/", AfterlifeEntityProfileState.ActorTypes.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                actual: actorType));
        }

        if (!string.IsNullOrWhiteSpace(realm) && !AfterlifeEntityProfileState.Realms.Contains(realm))
        {
            issues.Add(new ValidationIssue(
                $"{context}.realm",
                IssueSeverity.Error,
                "realm профиля должен быть загробным realm.",
                code: "afterlife_entity_profile_invalid_realm",
                section: "AfterlifeEntityProfiles",
                expected: "Chaos Sea / Shining Abode",
                actual: realm));
        }

        if (!string.IsNullOrWhiteSpace(actorType) && !string.IsNullOrWhiteSpace(actorId))
        {
            var identity = $"{actorType}:{actorId}";
            if (!identities.Add(identity))
            {
                issues.Add(new ValidationIssue(
                    context,
                    IssueSeverity.Error,
                    "Дубликат профиля сущности посмертия для actorType + actorId/actorRef.",
                    code: "afterlife_entity_profile_duplicate_actor",
                    section: "AfterlifeEntityProfiles",
                    expected: "unique actorType + actorId/actorRef",
                    actual: identity));
            }
        }

        ValidateAfterlifeProfileCurrencies(profile, context, issues);
        ValidateAfterlifeProfileProgression(profile, context, issues);
        ValidateAfterlifeProfileStandardArts(profile, context, issues);
        ValidateAfterlifeProfileSpecialArts(profile, context, issues);
        ValidateAfterlifeProfileCustomStates(profile, context, issues);
        ValidateAfterlifeProfileSoulDissipation(profile, context, issues);
        ValidateAfterlifeProfileProgressionStrategy(profile, context, issues);
        ValidateAfterlifeProfileProgressionLedger(profile, context, issues);
        ValidateAfterlifeProfileLedger(profile, context, issues);
        ValidateStringArrayIfPresent(profile, context, "warnings", "afterlife_entity_profile_warnings_not_array", issues);
    }

    private void ValidateAfterlifeProfileCurrencies(JsonElement profile, string context, List<ValidationIssue> issues)
    {
        if (!TryRequireProfileObject(profile, context, "currencies", "afterlife_entity_profile_missing_currencies", issues, out var currencies))
            return;

        ValidateProfileNonNegativeInt(currencies, $"{context}.currencies", "inkFeathers", "afterlife_entity_profile_negative_currency", issues);
        ValidateProfileNonNegativeInt(currencies, $"{context}.currencies", "lightSparks", "afterlife_entity_profile_negative_currency", issues);
    }

    private void ValidateAfterlifeProfileProgression(JsonElement profile, string context, List<ValidationIssue> issues)
    {
        if (!TryRequireProfileObject(profile, context, "progression", "afterlife_entity_profile_missing_progression", issues, out var progression))
            return;

        ValidateProgressionTrack(progression, $"{context}.progression", "enlightenment", issues);
        ValidateProgressionTrack(progression, $"{context}.progression", "radiance", issues);
    }

    private void ValidateProgressionTrack(JsonElement progression, string context, string propertyName, List<ValidationIssue> issues)
    {
        if (!TryRequireProfileObject(progression, context, propertyName, "afterlife_entity_profile_missing_progression_track", issues, out var track))
            return;

        ValidateProfileNonNegativeInt(track, $"{context}.{propertyName}", "experience", "afterlife_entity_profile_invalid_progression_value", issues);
        ValidateProfileTier(track, $"{context}.{propertyName}", "tier", "afterlife_entity_profile_invalid_progression_value", issues);
    }

    private void ValidateAfterlifeProfileStandardArts(JsonElement profile, string context, List<ValidationIssue> issues)
    {
        if (!TryRequireProfileObject(profile, context, "standardArts", "afterlife_entity_profile_missing_standard_arts", issues, out var standardArts))
            return;

        foreach (var property in standardArts.EnumerateObject())
        {
            if (!AfterlifeEntityProfileState.StandardArtIds.Contains(property.Name))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.standardArts.{property.Name}",
                    IssueSeverity.Error,
                    "standardArts содержит неизвестное духовное искусство.",
                    code: "afterlife_entity_profile_unknown_standard_art",
                    section: "AfterlifeEntityProfiles",
                    expected: string.Join("/", AfterlifeEntityProfileState.StandardArtIds.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                    actual: property.Name));
                continue;
            }

            if (!TryGetProfileInt(property.Value, out var tier) || tier < 0 || tier > AfterlifeEntityProfileState.MaxProfileTier)
            {
                issues.Add(new ValidationIssue(
                    $"{context}.standardArts.{property.Name}",
                    IssueSeverity.Error,
                    "Тир стандартного духовного искусства должен быть 0..5.",
                    code: "afterlife_entity_profile_invalid_standard_art_tier",
                    section: "AfterlifeEntityProfiles",
                    expected: "integer 0..5",
                    actual: property.Value.ToString()));
            }
        }
    }

    private void ValidateAfterlifeProfileCustomStates(JsonElement profile, string context, List<ValidationIssue> issues)
    {
        if (!profile.TryGetProperty(AfterlifeEntityProfileState.CustomStatesProperty, out var customStates))
            return;

        if (customStates.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{context}.{AfterlifeEntityProfileState.CustomStatesProperty}",
                IssueSeverity.Error,
                "customStates профиля сущности посмертия должен быть array.",
                code: "afterlife_entity_profile_custom_states_not_array",
                section: "AfterlifeEntityProfiles",
                expected: "array",
                actual: customStates.ValueKind.ToString()));
            return;
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var state in customStates.EnumerateArray())
        {
            var stateContext = $"{context}.{AfterlifeEntityProfileState.CustomStatesProperty}[{index++}]";
            ValidateAfterlifeEntityCustomStateObject(state, stateContext, issues, ids);
        }
    }

    private void ValidateAfterlifeEntityCustomStateChangesIfPresent(
        JsonElement changes,
        bool hasChanges,
        string context,
        List<ValidationIssue> issues)
    {
        if (!hasChanges)
            return;

        if (changes.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "afterlifeEntityCustomStateChanges должен быть array.",
                code: "afterlife_entity_profile_custom_state_changes_not_array",
                section: "AfterlifeEntityProfiles",
                expected: "array",
                actual: changes.ValueKind.ToString()));
            return;
        }

        var index = 0;
        foreach (var change in changes.EnumerateArray())
        {
            var changeContext = $"{context}[{index++}]";
            ValidateAfterlifeEntityCustomStateChange(change, changeContext, issues);
        }
    }

    private void ValidateAfterlifeEntityCustomStateChange(JsonElement change, string context, List<ValidationIssue> issues)
    {
        if (change.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "afterlifeEntityCustomStateChanges entry должен быть object.",
                code: "afterlife_entity_profile_custom_state_change_not_object",
                section: "AfterlifeEntityProfiles",
                expected: "object",
                actual: change.ValueKind.ToString()));
            return;
        }

        var actorType = RequireProfileString(change, context, "actorType", "afterlife_entity_profile_custom_state_change_missing_actor_type", issues);
        if (!string.IsNullOrWhiteSpace(actorType) && !AfterlifeEntityProfileState.ActorTypes.Contains(actorType))
        {
            issues.Add(new ValidationIssue(
                $"{context}.actorType",
                IssueSeverity.Error,
                "actorType custom state change должен ссылаться на сущность посмертия.",
                code: "afterlife_entity_profile_custom_state_change_invalid_actor_type",
                section: "AfterlifeEntityProfiles",
                expected: string.Join("/", AfterlifeEntityProfileState.ActorTypes.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                actual: actorType));
        }

        if (string.IsNullOrWhiteSpace(GetProfileString(change, "actorId")) &&
            string.IsNullOrWhiteSpace(GetProfileString(change, "actorRef")))
        {
            issues.Add(new ValidationIssue(
                $"{context}.actorId",
                IssueSeverity.Error,
                "custom state change должен иметь actorId или actorRef target profile.",
                code: "afterlife_entity_profile_custom_state_change_missing_actor_id",
                section: "AfterlifeEntityProfiles",
                expected: "non-empty actorId or actorRef"));
        }

        var hasUpserts = change.TryGetProperty("statesToAddOrUpdate", out var upserts);
        var hasRemovals = change.TryGetProperty("statesToRemove", out var removals);
        if (!hasUpserts && !hasRemovals)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "afterlifeEntityCustomStateChanges entry должен содержать statesToAddOrUpdate и/или statesToRemove.",
                code: "afterlife_entity_profile_custom_state_change_empty",
                section: "AfterlifeEntityProfiles",
                expected: "statesToAddOrUpdate[] and/or statesToRemove[]"));
        }

        if (hasUpserts)
        {
            if (upserts.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new ValidationIssue(
                    $"{context}.statesToAddOrUpdate",
                    IssueSeverity.Error,
                    "statesToAddOrUpdate должен быть array.",
                    code: "afterlife_entity_profile_custom_state_upserts_not_array",
                    section: "AfterlifeEntityProfiles",
                    expected: "array",
                    actual: upserts.ValueKind.ToString()));
            }
            else
            {
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var upsertIndex = 0;
                foreach (var state in upserts.EnumerateArray())
                    ValidateAfterlifeEntityCustomStateObject(state, $"{context}.statesToAddOrUpdate[{upsertIndex++}]", issues, ids);
            }
        }

        if (hasRemovals)
            ValidateAfterlifeEntityCustomStateRemovalArray(removals, $"{context}.statesToRemove", issues);
    }

    private void ValidateAfterlifeEntityCustomStateRemovalArray(JsonElement removals, string context, List<ValidationIssue> issues)
    {
        if (removals.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "statesToRemove должен быть array.",
                code: "afterlife_entity_profile_custom_state_removals_not_array",
                section: "AfterlifeEntityProfiles",
                expected: "array",
                actual: removals.ValueKind.ToString()));
            return;
        }

        var index = 0;
        foreach (var removal in removals.EnumerateArray())
        {
            if (removal.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(removal.GetString()))
            {
                issues.Add(new ValidationIssue(
                    $"{context}[{index}]",
                    IssueSeverity.Error,
                    "statesToRemove должен содержать non-empty stateId strings.",
                    code: "afterlife_entity_profile_custom_state_remove_invalid_id",
                    section: "AfterlifeEntityProfiles",
                    expected: "non-empty string",
                    actual: removal.ToString()));
            }

            index++;
        }
    }

    private void ValidateAfterlifeEntityCustomStateObject(
        JsonElement state,
        string context,
        List<ValidationIssue> issues,
        HashSet<string> ids)
    {
        if (state.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "custom state сущности посмертия должен быть object.",
                code: "afterlife_entity_profile_custom_state_not_object",
                section: "AfterlifeEntityProfiles",
                expected: "object",
                actual: state.ValueKind.ToString()));
            return;
        }

        var stateId = RequireProfileString(state, context, "stateId", "afterlife_entity_profile_custom_state_missing_id", issues);
        if (!string.IsNullOrWhiteSpace(stateId) && !ids.Add(stateId))
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "Дубликат customStates.stateId в профиле/команде сущности посмертия.",
                code: "afterlife_entity_profile_duplicate_custom_state",
                section: "AfterlifeEntityProfiles",
                expected: "unique stateId",
                actual: stateId));
        }

        var missingCoreFields = new List<string>();
        if (!HasAnyProfileString(state, "stateName", "name", "title"))
            missingCoreFields.Add("stateName/name/title");
        if (!state.TryGetProperty("currentValue", out _))
            missingCoreFields.Add("currentValue");
        if (!state.TryGetProperty("minValue", out _))
            missingCoreFields.Add("minValue");
        if (!state.TryGetProperty("maxValue", out _))
            missingCoreFields.Add("maxValue");
        if (!HasAnyProfileString(state, "description", "summary"))
            missingCoreFields.Add("description/summary");
        if (!state.TryGetProperty("progressionRule", out _))
            missingCoreFields.Add("progressionRule");
        if (!state.TryGetProperty("thresholds", out _))
            missingCoreFields.Add("thresholds");

        if (missingCoreFields.Count > 0)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "custom state сущности посмертия не содержит обязательные поля.",
                code: "afterlife_entity_profile_custom_state_missing_required_fields",
                section: "AfterlifeEntityProfiles",
                expected: "stateId, stateName/name/title, currentValue, minValue, maxValue, description/summary, progressionRule, thresholds",
                actual: string.Join(", ", missingCoreFields)));
            return;
        }

        RequireNumberOrStringProfileField(state, context, "currentValue", issues);
        RequireNumberOrStringProfileField(state, context, "minValue", issues);
        RequireNumberOrStringProfileField(state, context, "maxValue", issues);

        if (state.TryGetProperty("progressionRule", out var progressionRule))
        {
            if (progressionRule.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue(
                    $"{context}.progressionRule",
                    IssueSeverity.Error,
                    "progressionRule custom state должен быть object.",
                    code: "afterlife_entity_profile_custom_state_progression_rule_not_object",
                    section: "AfterlifeEntityProfiles",
                    expected: "object",
                    actual: progressionRule.ValueKind.ToString()));
            }
            else
            {
                RequireNumberOrStringProfileField(progressionRule, $"{context}.progressionRule", "changePerTurn", issues);
                RequireProfileString(progressionRule, $"{context}.progressionRule", "description", "afterlife_entity_profile_custom_state_progression_rule_missing_description", issues);
            }
        }

        if (state.TryGetProperty("thresholds", out var thresholds) && thresholds.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{context}.thresholds",
                IssueSeverity.Error,
                "thresholds custom state должен быть array.",
                code: "afterlife_entity_profile_custom_state_thresholds_not_array",
                section: "AfterlifeEntityProfiles",
                expected: "array",
                actual: thresholds.ValueKind.ToString()));
        }
    }

    private void ValidateAfterlifeProfileSpecialArts(JsonElement profile, string context, List<ValidationIssue> issues)
    {
        if (!TryRequireProfileArray(profile, context, "specialArts", "afterlife_entity_profile_missing_special_arts", issues, out var specialArts))
            return;

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var art in specialArts.EnumerateArray())
        {
            var artContext = $"{context}.specialArts[{index++}]";
            if (art.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue(
                    artContext,
                    IssueSeverity.Error,
                    "Особое духовное искусство должно быть object.",
                    code: "afterlife_entity_profile_special_art_not_object",
                    section: "AfterlifeEntityProfiles",
                    expected: "object",
                    actual: art.ValueKind.ToString()));
                continue;
            }

            var artId = RequireProfileString(art, artContext, "artId", "afterlife_entity_profile_special_art_missing_id", issues);
            if (!string.IsNullOrWhiteSpace(artId) && !ids.Add(artId))
            {
                issues.Add(new ValidationIssue(
                    artContext,
                    IssueSeverity.Error,
                    "Дубликат specialArts.artId.",
                    code: "afterlife_entity_profile_duplicate_special_art",
                    section: "AfterlifeEntityProfiles",
                    expected: "unique special art ids",
                    actual: artId));
            }

            RequireProfileString(art, artContext, "displayName", "afterlife_entity_profile_special_art_missing_display_name", issues);
            RequireProfileString(art, artContext, "effectSummary", "afterlife_entity_profile_special_art_missing_effect", issues);
            var ownerActorType = RequireProfileString(art, artContext, "ownerActorType", "afterlife_entity_profile_special_art_missing_owner_actor_type", issues);
            RequireProfileString(art, artContext, "ownerActorId", "afterlife_entity_profile_special_art_missing_owner_actor_id", issues);
            if (!string.IsNullOrWhiteSpace(ownerActorType) && !AfterlifeEntityProfileState.ActorTypes.Contains(ownerActorType))
            {
                issues.Add(new ValidationIssue(
                    $"{artContext}.ownerActorType",
                    IssueSeverity.Error,
                    "ownerActorType особого искусства должен быть известным afterlife actor type.",
                    code: "afterlife_entity_profile_special_art_invalid_owner_actor_type",
                    section: "AfterlifeEntityProfiles",
                    expected: string.Join("/", AfterlifeEntityProfileState.ActorTypes.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                    actual: ownerActorType));
            }

            var baseOperation = RequireProfileString(art, artContext, "baseOperation", "afterlife_entity_profile_special_art_missing_base_operation", issues);
            if (!string.IsNullOrWhiteSpace(baseOperation) &&
                !AfterlifeEntityProfileState.SpecialArtBaseOperations.Contains(baseOperation))
            {
                issues.Add(new ValidationIssue(
                    $"{artContext}.baseOperation",
                    IssueSeverity.Error,
                    "baseOperation особого искусства должен ссылаться на стандартное духовное действие.",
                    code: "afterlife_entity_profile_invalid_special_art_base_operation",
                    section: "AfterlifeEntityProfiles",
                    expected: string.Join("/", AfterlifeEntityProfileState.SpecialArtBaseOperations.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                    actual: baseOperation));
            }

            ValidateProfileTier(art, artContext, "tier", "afterlife_entity_profile_invalid_special_art_tier", issues);
            if (!art.TryGetProperty("costMultiplierPercent", out var costMultiplier) ||
                !TryGetProfileInt(costMultiplier, out var multiplier) ||
                multiplier <= 100)
            {
                issues.Add(new ValidationIssue(
                    $"{artContext}.costMultiplierPercent",
                    IssueSeverity.Error,
                    "costMultiplierPercent особого искусства должен быть > 100, потому что особое искусство дороже базового действия.",
                    code: "afterlife_entity_profile_invalid_special_art_cost_multiplier",
                    section: "AfterlifeEntityProfiles",
                    expected: "integer > 100",
                    actual: art.TryGetProperty("costMultiplierPercent", out var actualCostMultiplier) ? actualCostMultiplier.ToString() : "missing"));
            }

            if (!art.TryGetProperty("upgradeCost", out var upgradeCost))
            {
                issues.Add(new ValidationIssue(
                    $"{artContext}.upgradeCost",
                    IssueSeverity.Error,
                    "upgradeCost особого искусства должен описывать цену прокачки.",
                    code: "afterlife_entity_profile_special_art_missing_upgrade_cost",
                    section: "AfterlifeEntityProfiles",
                    expected: "object with non-negative currency costs",
                    actual: "missing"));
            }
            else
            {
                ValidateNonNegativeIntegerObject(upgradeCost, $"{artContext}.upgradeCost", issues, "afterlife_entity_profile_special_art_invalid_upgrade_cost");
            }

            if (art.TryGetProperty("canTeachPlayer", out var canTeachPlayerNode) &&
                canTeachPlayerNode.ValueKind == JsonValueKind.True)
            {
                if (!art.TryGetProperty("trainingConditions", out var trainingConditions) ||
                    trainingConditions.ValueKind != JsonValueKind.Array ||
                    !trainingConditions.EnumerateArray().Any(condition =>
                        condition.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(condition.GetString())))
                {
                    issues.Add(new ValidationIssue(
                        $"{artContext}.trainingConditions",
                        IssueSeverity.Error,
                        "Если особое искусство можно преподавать игроку, trainingConditions должен содержать хотя бы одно условие обучения.",
                        code: "afterlife_entity_profile_special_art_missing_training_conditions",
                        section: "AfterlifeEntityProfiles",
                        expected: "non-empty string array",
                        actual: art.TryGetProperty("trainingConditions", out var actualTraining) ? actualTraining.ToString() : "missing"));
                }
            }
            else if (art.TryGetProperty("trainingConditions", out var trainingConditions) &&
                     trainingConditions.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new ValidationIssue(
                    $"{artContext}.trainingConditions",
                    IssueSeverity.Error,
                    "trainingConditions особого искусства должен быть array.",
                    code: "afterlife_entity_profile_special_art_training_conditions_not_array",
                    section: "AfterlifeEntityProfiles",
                    expected: "array",
                    actual: trainingConditions.ValueKind.ToString()));
            }
        }
    }

    private void ValidateAfterlifeSpecialArtLearningReceiptsIfPresent(
        JsonElement receipts,
        bool hasReceipts,
        string context,
        List<ValidationIssue> issues)
    {
        if (!hasReceipts)
            return;

        if (receipts.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "afterlifeSpecialArtLearningReceipts должен быть array.",
                code: "afterlife_entity_profile_special_art_learning_not_array",
                section: "AfterlifeEntityProfiles",
                expected: "array",
                actual: receipts.ValueKind.ToString()));
            return;
        }

        var index = 0;
        foreach (var receipt in receipts.EnumerateArray())
        {
            var receiptContext = $"{context}[{index++}]";
            if (receipt.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue(
                    receiptContext,
                    IssueSeverity.Error,
                    "afterlifeSpecialArtLearningReceipts entry должен быть object.",
                    code: "afterlife_entity_profile_special_art_learning_not_object",
                    section: "AfterlifeEntityProfiles",
                    expected: "object",
                    actual: receipt.ValueKind.ToString()));
                continue;
            }

            RequireProfileString(receipt, receiptContext, "receiptId", "afterlife_entity_profile_special_art_learning_missing_receipt_id", issues);
            var teacherActorType = RequireProfileString(receipt, receiptContext, "teacherActorType", "afterlife_entity_profile_special_art_learning_missing_teacher_actor_type", issues);
            if (!string.IsNullOrWhiteSpace(teacherActorType) && !AfterlifeEntityProfileState.ActorTypes.Contains(teacherActorType))
            {
                issues.Add(new ValidationIssue(
                    $"{receiptContext}.teacherActorType",
                    IssueSeverity.Error,
                    "teacherActorType должен быть known afterlife actor type.",
                    code: "afterlife_entity_profile_special_art_learning_invalid_teacher_actor_type",
                    section: "AfterlifeEntityProfiles",
                    expected: string.Join("/", AfterlifeEntityProfileState.ActorTypes.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                    actual: teacherActorType));
            }

            RequireProfileString(receipt, receiptContext, "teacherActorId", "afterlife_entity_profile_special_art_learning_missing_teacher_actor_id", issues);
            RequireProfileString(receipt, receiptContext, "artId", "afterlife_entity_profile_special_art_learning_missing_art_id", issues);
            RequireProfileString(receipt, receiptContext, "playerActorId", "afterlife_entity_profile_special_art_learning_missing_player_actor_id", issues);
            RequireProfileString(receipt, receiptContext, "roleplayEvidence", "afterlife_entity_profile_special_art_learning_missing_roleplay_evidence", issues);
            RequireProfileString(receipt, receiptContext, "summary", "afterlife_entity_profile_special_art_learning_missing_summary", issues);
            ValidateProfileNonNegativeInt(receipt, receiptContext, "learnedAtTurn", "afterlife_entity_profile_special_art_learning_invalid_turn", issues);

            if (!receipt.TryGetProperty("trainingConditionSatisfied", out var condition) ||
                condition.ValueKind != JsonValueKind.True)
            {
                issues.Add(new ValidationIssue(
                    $"{receiptContext}.trainingConditionSatisfied",
                    IssueSeverity.Error,
                    "trainingConditionSatisfied должен быть true: ГМ должен явно признать ролевое обучение.",
                    code: "afterlife_entity_profile_special_art_learning_condition_not_satisfied",
                    section: "AfterlifeEntityProfiles",
                    expected: "true",
                    actual: receipt.TryGetProperty("trainingConditionSatisfied", out var actual) ? actual.ToString() : "missing"));
            }
        }
    }

    private void ValidateAfterlifeEntityProgressionOverridesIfPresent(
        JsonElement overrides,
        bool hasOverrides,
        string context,
        IReadOnlyDictionary<string, JsonElement> profileAuthority,
        List<ValidationIssue> issues)
    {
        if (!hasOverrides)
            return;

        if (overrides.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "afterlifeEntityProgressionOverrides должен быть array.",
                code: "afterlife_entity_profile_progression_overrides_not_array",
                section: "AfterlifeEntityProfiles",
                expected: "array",
                actual: overrides.ValueKind.ToString()));
            return;
        }

        var index = 0;
        foreach (var item in overrides.EnumerateArray())
            ValidateAfterlifeEntityProgressionOverride(item, $"{context}[{index++}]", profileAuthority, issues);
    }

    private void ValidateAfterlifeEntityProgressionOverride(
        JsonElement item,
        string context,
        IReadOnlyDictionary<string, JsonElement> profileAuthority,
        List<ValidationIssue> issues)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "afterlifeEntityProgressionOverrides entry должен быть object.",
                code: "afterlife_entity_profile_progression_override_not_object",
                section: "AfterlifeEntityProfiles",
                expected: "object",
                actual: item.ValueKind.ToString()));
            return;
        }

        RequireProfileString(item, context, "actorType", "afterlife_entity_profile_progression_override_missing_actor_type", issues);
        if (string.IsNullOrWhiteSpace(GetProfileString(item, "actorId")) &&
            string.IsNullOrWhiteSpace(GetProfileString(item, "actorRef")))
        {
            issues.Add(new ValidationIssue(
                $"{context}.actorId",
                IssueSeverity.Error,
                "progression override должен иметь actorId или actorRef.",
                code: "afterlife_entity_profile_progression_override_missing_actor_id",
                section: "AfterlifeEntityProfiles",
                expected: "non-empty actorId or actorRef"));
        }

        RequireProfileString(item, context, "cycleKey", "afterlife_entity_profile_progression_override_missing_cycle_key", issues);
        RequireProfileString(item, context, "reason", "afterlife_entity_profile_progression_override_missing_reason", issues);
        RequireProfileString(item, context, "summary", "afterlife_entity_profile_progression_override_missing_summary", issues);

        var hasCurrencyDeltas = item.TryGetProperty("currencyDeltas", out var currencyDeltas);
        var hasStandardArtDeltas = item.TryGetProperty("standardArtTierDeltas", out var artDeltas);
        var hasSpecialArtDeltas = item.TryGetProperty("specialArtTierDeltas", out var specialArtDeltas);
        var hasSoulDissipationDelta = item.TryGetProperty("soulDissipationTierDelta", out var soulDissipationDelta);
        var hasProgressionDeltas = item.TryGetProperty("progressionExperienceDeltas", out var progressionDeltas);
        if (!hasCurrencyDeltas && !hasStandardArtDeltas && !hasSpecialArtDeltas && !hasSoulDissipationDelta && !hasProgressionDeltas)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "progression override должен содержать хотя бы одну дельту.",
                code: "afterlife_entity_profile_progression_override_empty",
                section: "AfterlifeEntityProfiles",
                expected: "currencyDeltas / standardArtTierDeltas / specialArtTierDeltas / soulDissipationTierDelta / progressionExperienceDeltas"));
        }

        if (hasCurrencyDeltas)
            ValidateSignedIntegerObject(currencyDeltas, $"{context}.currencyDeltas", issues, "afterlife_entity_profile_progression_override_invalid_currency_delta");
        if (hasStandardArtDeltas)
            ValidateStandardArtTierDeltaObject(artDeltas, $"{context}.standardArtTierDeltas", issues);
        if (hasSpecialArtDeltas)
        {
            var targetProfile = ResolveAfterlifeEntityProgressionOverrideTargetProfile(item, profileAuthority);
            ValidateSpecialArtTierDeltaObject(specialArtDeltas, $"{context}.specialArtTierDeltas", issues, targetProfile);
        }
        if (hasSoulDissipationDelta)
            ValidateSoulDissipationTierDelta(soulDissipationDelta, $"{context}.soulDissipationTierDelta", issues);
        if (hasProgressionDeltas)
            ValidateSignedIntegerObject(progressionDeltas, $"{context}.progressionExperienceDeltas", issues, "afterlife_entity_profile_progression_override_invalid_progression_delta");
    }

    private void ValidateAfterlifeProfileSoulDissipation(JsonElement profile, string context, List<ValidationIssue> issues)
    {
        if (!profile.TryGetProperty("soulDissipationTier", out var value))
        {
            issues.Add(new ValidationIssue(
                $"{context}.soulDissipationTier",
                IssueSeverity.Error,
                "Профиль должен явно хранить soulDissipationTier.",
                code: "afterlife_entity_profile_missing_soul_dissipation_tier",
                section: "AfterlifeEntityProfiles",
                expected: "integer 0..5"));
            return;
        }

        ValidateProfileTier(profile, context, "soulDissipationTier", "afterlife_entity_profile_invalid_soul_dissipation_tier", issues);
    }

    private void ValidateAfterlifeProfileProgressionStrategy(JsonElement profile, string context, List<ValidationIssue> issues)
    {
        if (!TryRequireProfileObject(profile, context, "progressionStrategy", "afterlife_entity_profile_missing_progression_strategy", issues, out var strategy))
            return;

        RequireProfileString(strategy, $"{context}.progressionStrategy", "strategyId", "afterlife_entity_profile_strategy_missing_id", issues);
        RequireProfileString(strategy, $"{context}.progressionStrategy", "summary", "afterlife_entity_profile_strategy_missing_summary", issues);
        if (TryRequireProfileArray(strategy, $"{context}.progressionStrategy", "priorityOrder", "afterlife_entity_profile_strategy_missing_priority_order", issues, out var priorityOrder))
        {
            var specialArtIds = ReadProfileSpecialArtIds(profile);
            var index = 0;
            foreach (var item in priorityOrder.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
                {
                    issues.Add(new ValidationIssue(
                        $"{context}.progressionStrategy.priorityOrder[{index}]",
                        IssueSeverity.Error,
                        "priorityOrder должен содержать non-empty string entries.",
                        code: "afterlife_entity_profile_strategy_invalid_priority",
                        section: "AfterlifeEntityProfiles",
                        expected: "non-empty string",
                        actual: item.ToString()));
                    index++;
                    continue;
                }

                var priority = item.GetString()!.Trim();
                if (!IsKnownAfterlifeProgressionPriority(priority, specialArtIds))
                {
                    issues.Add(new ValidationIssue(
                        $"{context}.progressionStrategy.priorityOrder[{index}]",
                        IssueSeverity.Error,
                        "priorityOrder содержит неизвестное направление прокачки.",
                        code: "afterlife_entity_profile_strategy_unknown_priority",
                        section: "AfterlifeEntityProfiles",
                        expected: "standard art id / specialArts[].artId / enlightenment / radiance / soul_dissipation",
                        actual: priority));
                }

                index++;
            }
        }

        ValidateProfileNonNegativeIntIfPresent(strategy, $"{context}.progressionStrategy", "lastUpdatedAtTurn", "afterlife_entity_profile_strategy_invalid_turn", issues);
        if (strategy.TryGetProperty("resourceReserve", out var reserve))
            ValidateNonNegativeIntegerObject(reserve, $"{context}.progressionStrategy.resourceReserve", issues, "afterlife_entity_profile_strategy_invalid_reserve");
        ValidateStrategySpendCategoryArrayIfPresent(strategy, $"{context}.progressionStrategy", "allowedSpends", "afterlife_entity_profile_strategy_allowed_spends_not_array", issues);
        ValidateStrategySpendCategoryArrayIfPresent(strategy, $"{context}.progressionStrategy", "forbiddenSpends", "afterlife_entity_profile_strategy_forbidden_spends_not_array", issues);
        if (strategy.TryGetProperty("lastAutoProgressionCycleKey", out var lastCycleKey) &&
            (lastCycleKey.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(lastCycleKey.GetString())))
        {
            issues.Add(new ValidationIssue(
                $"{context}.progressionStrategy.lastAutoProgressionCycleKey",
                IssueSeverity.Error,
                "lastAutoProgressionCycleKey должен быть non-empty string, если указан.",
                code: "afterlife_entity_profile_strategy_invalid_last_auto_cycle",
                section: "AfterlifeEntityProfiles",
                expected: "non-empty string",
                actual: lastCycleKey.ToString()));
        }
    }

    private void ValidateStrategySpendCategoryArrayIfPresent(
        JsonElement root,
        string context,
        string propertyName,
        string arrayCode,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(propertyName, out var array))
            return;

        if (array.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{context}.{propertyName}",
                IssueSeverity.Error,
                $"{propertyName} должен быть array.",
                code: arrayCode,
                section: "AfterlifeEntityProfiles",
                expected: "array",
                actual: array.ValueKind.ToString()));
            return;
        }

        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var itemContext = $"{context}.{propertyName}[{index}]";
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                issues.Add(new ValidationIssue(
                    itemContext,
                    IssueSeverity.Error,
                    $"{propertyName} должен содержать non-empty string entries.",
                    code: "afterlife_entity_profile_invalid_string_array_entry",
                    section: "AfterlifeEntityProfiles",
                    expected: "non-empty string",
                    actual: item.ToString()));
                index++;
                continue;
            }

            var category = item.GetString()!.Trim();
            if (!AfterlifeEntityProfileState.ProgressionSpendCategories.Contains(category))
            {
                issues.Add(new ValidationIssue(
                    itemContext,
                    IssueSeverity.Error,
                    $"{propertyName} содержит неизвестную категорию траты автопрокачки.",
                    code: "afterlife_entity_profile_strategy_unknown_spend_category",
                    section: "AfterlifeEntityProfiles",
                    expected: string.Join("/", AfterlifeEntityProfileState.ProgressionSpendCategories.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                    actual: category));
            }

            index++;
        }
    }

    private void ValidateAfterlifeProfileProgressionLedger(JsonElement profile, string context, List<ValidationIssue> issues)
    {
        if (!profile.TryGetProperty(AfterlifeEntityProfileState.ProgressionLedgerProperty, out var ledger))
            return;

        if (ledger.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{context}.{AfterlifeEntityProfileState.ProgressionLedgerProperty}",
                IssueSeverity.Error,
                "progressionLedger должен быть array.",
                code: "afterlife_entity_profile_progression_ledger_not_array",
                section: "AfterlifeEntityProfiles",
                expected: "array",
                actual: ledger.ValueKind.ToString()));
            return;
        }

        var index = 0;
        foreach (var entry in ledger.EnumerateArray())
            ValidateAfterlifeProfileProgressionLedgerEntry(entry, $"{context}.{AfterlifeEntityProfileState.ProgressionLedgerProperty}[{index++}]", issues);
    }

    private void ValidateAfterlifeProfileProgressionLedgerEntry(JsonElement entry, string context, List<ValidationIssue> issues)
    {
        if (entry.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "progressionLedger entry должен быть object.",
                code: "afterlife_entity_profile_progression_ledger_entry_not_object",
                section: "AfterlifeEntityProfiles",
                expected: "object",
                actual: entry.ValueKind.ToString()));
            return;
        }

        RequireProfileString(entry, context, "entryId", "afterlife_entity_profile_progression_ledger_missing_entry_id", issues);
        RequireProfileString(entry, context, "cycleKey", "afterlife_entity_profile_progression_ledger_missing_cycle_key", issues);
        RequireProfileString(entry, context, "source", "afterlife_entity_profile_progression_ledger_missing_source", issues);
        RequireProfileString(entry, context, "summary", "afterlife_entity_profile_progression_ledger_missing_summary", issues);

        if (entry.TryGetProperty("income", out var income))
            ValidateNonNegativeIntegerObject(income, $"{context}.income", issues, "afterlife_entity_profile_progression_ledger_negative_amount");
        if (entry.TryGetProperty("spending", out var spending))
            ValidateNonNegativeIntegerObject(spending, $"{context}.spending", issues, "afterlife_entity_profile_progression_ledger_negative_amount");
        if (entry.TryGetProperty("upgrades", out var upgrades) && upgrades.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{context}.upgrades",
                IssueSeverity.Error,
                "progressionLedger.upgrades должен быть array.",
                code: "afterlife_entity_profile_progression_ledger_upgrades_not_array",
                section: "AfterlifeEntityProfiles",
                expected: "array",
                actual: upgrades.ValueKind.ToString()));
        }
    }

    private void ValidateAfterlifeProfileLedger(JsonElement profile, string context, List<ValidationIssue> issues)
    {
        if (!TryRequireProfileArray(profile, context, "ledger", "afterlife_entity_profile_missing_ledger", issues, out var ledger))
            return;

        var index = 0;
        foreach (var entry in ledger.EnumerateArray())
        {
            var entryContext = $"{context}.ledger[{index++}]";
            if (entry.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue(
                    entryContext,
                    IssueSeverity.Error,
                    "ledger entry должен быть object.",
                    code: "afterlife_entity_profile_ledger_entry_not_object",
                    section: "AfterlifeEntityProfiles",
                    expected: "object",
                    actual: entry.ValueKind.ToString()));
                continue;
            }

            RequireProfileString(entry, entryContext, "entryId", "afterlife_entity_profile_ledger_missing_entry_id", issues);
            RequireProfileString(entry, entryContext, "summary", "afterlife_entity_profile_ledger_missing_summary", issues);
            ValidateProfileNonNegativeIntIfPresent(entry, entryContext, "turnNumber", "afterlife_entity_profile_ledger_invalid_turn", issues);
        }
    }

    private void ValidateStringArrayIfPresent(
        JsonElement root,
        string context,
        string propertyName,
        string code,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(propertyName, out var array))
            return;

        if (array.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{context}.{propertyName}",
                IssueSeverity.Error,
                $"{propertyName} должен быть array.",
                code: code,
                section: "AfterlifeEntityProfiles",
                expected: "array",
                actual: array.ValueKind.ToString()));
            return;
        }

        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{propertyName}[{index}]",
                    IssueSeverity.Error,
                    $"{propertyName} должен содержать non-empty string entries.",
                    code: "afterlife_entity_profile_invalid_string_array_entry",
                    section: "AfterlifeEntityProfiles",
                    expected: "non-empty string",
                    actual: item.ToString()));
            }

            index++;
        }
    }

    private bool TryRequireProfileObject(
        JsonElement root,
        string context,
        string propertyName,
        string code,
        List<ValidationIssue> issues,
        out JsonElement value)
    {
        value = default;
        if (!root.TryGetProperty(propertyName, out var node))
        {
            issues.Add(new ValidationIssue(
                $"{context}.{propertyName}",
                IssueSeverity.Error,
                $"{propertyName} обязателен в профиле сущности посмертия.",
                code: code,
                section: "AfterlifeEntityProfiles",
                expected: "object",
                actual: "missing"));
            return false;
        }

        if (node.ValueKind == JsonValueKind.Object)
        {
            value = node;
            return true;
        }

        issues.Add(new ValidationIssue(
            $"{context}.{propertyName}",
            IssueSeverity.Error,
            $"{propertyName} должен быть object.",
            code: code,
            section: "AfterlifeEntityProfiles",
            expected: "object",
            actual: node.ValueKind.ToString()));
        return false;
    }

    private bool TryRequireProfileArray(
        JsonElement root,
        string context,
        string propertyName,
        string code,
        List<ValidationIssue> issues,
        out JsonElement value)
    {
        value = default;
        if (!root.TryGetProperty(propertyName, out var node))
        {
            issues.Add(new ValidationIssue(
                $"{context}.{propertyName}",
                IssueSeverity.Error,
                $"{propertyName} обязателен в профиле сущности посмертия.",
                code: code,
                section: "AfterlifeEntityProfiles",
                expected: "array",
                actual: "missing"));
            return false;
        }

        if (node.ValueKind == JsonValueKind.Array)
        {
            value = node;
            return true;
        }

        issues.Add(new ValidationIssue(
            $"{context}.{propertyName}",
            IssueSeverity.Error,
            $"{propertyName} должен быть array.",
            code: code,
            section: "AfterlifeEntityProfiles",
            expected: "array",
            actual: node.ValueKind.ToString()));
        return false;
    }

    private static string? RequireProfileString(
        JsonElement root,
        string context,
        string propertyName,
        string code,
        List<ValidationIssue> issues)
    {
        var value = GetProfileString(root, propertyName);
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        issues.Add(new ValidationIssue(
            $"{context}.{propertyName}",
            IssueSeverity.Error,
            $"{propertyName} должен быть non-empty string.",
            code: code,
            section: "AfterlifeEntityProfiles",
            expected: "non-empty string",
            actual: root.TryGetProperty(propertyName, out var node) ? node.ToString() : "missing"));
        return null;
    }

    private static string? GetProfileString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;

    private static void ValidateProfileTier(
        JsonElement root,
        string context,
        string propertyName,
        string code,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(propertyName, out var value) ||
            !TryGetProfileInt(value, out var tier) ||
            tier < 0 ||
            tier > AfterlifeEntityProfileState.MaxProfileTier)
        {
            issues.Add(new ValidationIssue(
                $"{context}.{propertyName}",
                IssueSeverity.Error,
                $"{propertyName} должен быть integer 0..5.",
                code: code,
                section: "AfterlifeEntityProfiles",
                expected: "integer 0..5",
                actual: root.TryGetProperty(propertyName, out var actual) ? actual.ToString() : "missing"));
        }
    }

    private static void ValidateProfileNonNegativeInt(
        JsonElement root,
        string context,
        string propertyName,
        string code,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(propertyName, out var value) ||
            !TryGetProfileInt(value, out var integer) ||
            integer < 0)
        {
            issues.Add(new ValidationIssue(
                $"{context}.{propertyName}",
                IssueSeverity.Error,
                $"{propertyName} должен быть неотрицательным integer.",
                code: code,
                section: "AfterlifeEntityProfiles",
                expected: "non-negative integer",
                actual: root.TryGetProperty(propertyName, out var actual) ? actual.ToString() : "missing"));
        }
    }

    private static void ValidateProfileNonNegativeIntIfPresent(
        JsonElement root,
        string context,
        string propertyName,
        string code,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(propertyName, out var value))
            return;

        if (!TryGetProfileInt(value, out var integer) || integer < 0)
        {
            issues.Add(new ValidationIssue(
                $"{context}.{propertyName}",
                IssueSeverity.Error,
                $"{propertyName} должен быть неотрицательным integer.",
                code: code,
                section: "AfterlifeEntityProfiles",
                expected: "non-negative integer",
                actual: value.ToString()));
        }
    }

    private static bool HasAnyProfileString(JsonElement root, params string[] propertyNames) =>
        propertyNames.Any(propertyName => !string.IsNullOrWhiteSpace(GetProfileString(root, propertyName)));

    private static void RequireNumberOrStringProfileField(
        JsonElement root,
        string context,
        string propertyName,
        List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(propertyName, out var value) &&
            (value.ValueKind == JsonValueKind.Number ||
             (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))))
        {
            return;
        }

        issues.Add(new ValidationIssue(
            $"{context}.{propertyName}",
            IssueSeverity.Error,
            $"{propertyName} custom state должен быть number или non-empty string.",
            code: "afterlife_entity_profile_custom_state_invalid_number_or_string",
            section: "AfterlifeEntityProfiles",
            expected: "number or non-empty string",
            actual: root.TryGetProperty(propertyName, out var actual) ? actual.ToString() : "missing"));
    }

    private static void ValidateNonNegativeIntegerObject(
        JsonElement root,
        string context,
        List<ValidationIssue> issues,
        string code)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "Поле должно быть object с неотрицательными integer values.",
                code: code,
                section: "AfterlifeEntityProfiles",
                expected: "object",
                actual: root.ValueKind.ToString()));
            return;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (!TryGetProfileInt(property.Value, out var value) || value < 0)
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{property.Name}",
                    IssueSeverity.Error,
                    "Значение должно быть неотрицательным integer.",
                    code: code,
                    section: "AfterlifeEntityProfiles",
                    expected: "non-negative integer",
                    actual: property.Value.ToString()));
            }
        }
    }

    private static void ValidateSignedIntegerObject(
        JsonElement root,
        string context,
        List<ValidationIssue> issues,
        string code)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "Поле должно быть object с integer values.",
                code: code,
                section: "AfterlifeEntityProfiles",
                expected: "object",
                actual: root.ValueKind.ToString()));
            return;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (!TryGetProfileInt(property.Value, out _))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{property.Name}",
                    IssueSeverity.Error,
                    "Значение override delta должно быть integer.",
                    code: code,
                    section: "AfterlifeEntityProfiles",
                    expected: "integer",
                    actual: property.Value.ToString()));
            }
        }
    }

    private static void ValidateStandardArtTierDeltaObject(JsonElement root, string context, List<ValidationIssue> issues)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "standardArtTierDeltas должен быть object.",
                code: "afterlife_entity_profile_progression_override_invalid_standard_art_delta",
                section: "AfterlifeEntityProfiles",
                expected: "object",
                actual: root.ValueKind.ToString()));
            return;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (!AfterlifeEntityProfileState.StandardArtIds.Contains(property.Name) ||
                !TryGetProfileInt(property.Value, out _))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{property.Name}",
                    IssueSeverity.Error,
                    "standardArtTierDeltas должен содержать известные standard art ids с integer delta.",
                    code: "afterlife_entity_profile_progression_override_invalid_standard_art_delta",
                    section: "AfterlifeEntityProfiles",
                    expected: string.Join("/", AfterlifeEntityProfileState.StandardArtIds.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                    actual: property.Value.ToString()));
            }
        }
    }

    private static JsonElement? ResolveAfterlifeEntityProgressionOverrideTargetProfile(
        JsonElement item,
        IReadOnlyDictionary<string, JsonElement> profileAuthority)
    {
        var key = BuildAfterlifeEntityProfileIdentityKey(item);
        if (string.IsNullOrWhiteSpace(key))
            return null;

        return profileAuthority.TryGetValue(key, out var profile)
            ? profile
            : null;
    }

    private static Dictionary<string, JsonElement> BuildAfterlifeEntityProfileAuthorityLookup(
        JsonElement profiles,
        bool hasProfiles,
        JsonElement responseProfiles,
        bool hasResponseProfiles,
        JsonElement updates,
        bool hasUpdates)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        AddAfterlifeEntityProfileAuthority(profiles, hasProfiles, result);
        AddAfterlifeEntityProfileAuthority(responseProfiles, hasResponseProfiles, result);
        AddAfterlifeEntityProfileAuthority(updates, hasUpdates, result);
        return result;
    }

    private static void AddAfterlifeEntityProfileAuthority(
        JsonElement profiles,
        bool hasProfiles,
        Dictionary<string, JsonElement> result)
    {
        if (!hasProfiles || profiles.ValueKind != JsonValueKind.Array)
            return;

        foreach (var profile in profiles.EnumerateArray())
        {
            var key = BuildAfterlifeEntityProfileIdentityKey(profile);
            if (!string.IsNullOrWhiteSpace(key))
                result[key] = profile;
        }
    }

    private static string? BuildAfterlifeEntityProfileIdentityKey(JsonElement profile)
    {
        if (profile.ValueKind != JsonValueKind.Object)
            return null;

        var actorType = GetProfileString(profile, "actorType");
        var actorId = GetProfileString(profile, "actorId") ?? GetProfileString(profile, "actorRef");
        return string.IsNullOrWhiteSpace(actorType) || string.IsNullOrWhiteSpace(actorId)
            ? null
            : $"{actorType}:{actorId}";
    }

    private static void ValidateSpecialArtTierDeltaObject(
        JsonElement root,
        string context,
        List<ValidationIssue> issues,
        JsonElement? targetProfile = null)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "specialArtTierDeltas должен быть object.",
                code: "afterlife_entity_profile_progression_override_invalid_special_art_delta",
                section: "AfterlifeEntityProfiles",
                expected: "object",
                actual: root.ValueKind.ToString()));
            return;
        }

        var knownSpecialArtIds = targetProfile.HasValue
            ? ReadAfterlifeProfileSpecialArtIds(targetProfile.Value)
            : null;
        foreach (var property in root.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(property.Name) ||
                !TryGetProfileInt(property.Value, out var delta) ||
                delta < -AfterlifeEntityProfileState.MaxProfileTier ||
                delta > AfterlifeEntityProfileState.MaxProfileTier)
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{property.Name}",
                    IssueSeverity.Error,
                    "specialArtTierDeltas должен содержать artId особого искусства с integer delta -5..5.",
                    code: "afterlife_entity_profile_progression_override_invalid_special_art_delta",
                    section: "AfterlifeEntityProfiles",
                    expected: "non-empty special art id -> integer -5..5",
                    actual: property.Value.ToString()));
                continue;
            }

            if (knownSpecialArtIds != null && !knownSpecialArtIds.Contains(property.Name))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{property.Name}",
                    IssueSeverity.Error,
                    "specialArtTierDeltas ссылается на неизвестное особое духовное искусство целевого профиля.",
                    code: "afterlife_entity_profile_progression_override_unknown_special_art",
                    section: "AfterlifeEntityProfiles",
                    expected: "existing specialArts[].artId on the target profile",
                    actual: property.Name));
            }
        }
    }

    private static HashSet<string> ReadAfterlifeProfileSpecialArtIds(JsonElement profile)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (profile.ValueKind != JsonValueKind.Object ||
            !profile.TryGetProperty("specialArts", out var specialArts) ||
            specialArts.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var specialArt in specialArts.EnumerateArray())
        {
            if (specialArt.ValueKind != JsonValueKind.Object)
                continue;

            var artId = GetProfileString(specialArt, "artId");
            if (!string.IsNullOrWhiteSpace(artId))
                result.Add(artId);
        }

        return result;
    }

    private static void ValidateSoulDissipationTierDelta(JsonElement value, string context, List<ValidationIssue> issues)
    {
        if (!TryGetProfileInt(value, out var delta) ||
            delta < -AfterlifeEntityProfileState.MaxProfileTier ||
            delta > AfterlifeEntityProfileState.MaxProfileTier)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "soulDissipationTierDelta должен быть integer -5..5.",
                code: "afterlife_entity_profile_progression_override_invalid_soul_dissipation_delta",
                section: "AfterlifeEntityProfiles",
                expected: "integer -5..5",
                actual: value.ToString()));
        }
    }

    private static HashSet<string> ReadProfileSpecialArtIds(JsonElement profile)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!profile.TryGetProperty("specialArts", out var specialArts) || specialArts.ValueKind != JsonValueKind.Array)
            return ids;

        foreach (var art in specialArts.EnumerateArray())
        {
            if (art.ValueKind != JsonValueKind.Object)
                continue;

            var artId = GetProfileString(art, "artId");
            if (!string.IsNullOrWhiteSpace(artId))
                ids.Add(artId);
        }

        return ids;
    }

    private static bool IsKnownAfterlifeProgressionPriority(string priority, HashSet<string> specialArtIds) =>
        AfterlifeEntityProfileState.StandardArtIds.Contains(priority) ||
        specialArtIds.Contains(priority) ||
        string.Equals(priority, "enlightenment", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(priority, "radiance", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(priority, "soul_dissipation", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(priority, "soulDissipation", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetProfileInt(JsonElement value, out int integer)
    {
        integer = 0;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out integer);
    }
}
