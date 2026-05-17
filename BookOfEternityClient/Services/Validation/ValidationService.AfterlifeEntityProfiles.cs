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
        if (!hasProfiles && !hasResponseProfiles && !hasUpdates)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{AfterlifeEntityProfileState.ProfilesProperty}",
                IssueSeverity.Error,
                "afterlife_entity_profiles.json должен содержать profiles[] или afterlifeEntityProfileUpdates[].",
                code: "afterlife_entity_profile_missing_profiles",
                section: "AfterlifeEntityProfiles",
                expected: "profiles[] or afterlifeEntityProfileUpdates[]"));
        }

        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ValidateProfileArrayIfPresent(profiles, hasProfiles, $"{contextPrefix}.{AfterlifeEntityProfileState.ProfilesProperty}", identities, issues);
        ValidateProfileArrayIfPresent(responseProfiles, hasResponseProfiles, $"{contextPrefix}.{AfterlifeEntityProfileState.ResponseProfilesProperty}", identities, issues);
        ValidateProfileArrayIfPresent(updates, hasUpdates, $"{contextPrefix}.{AfterlifeEntityProfileState.UpdateProperty}", identities, issues);
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
        ValidateAfterlifeProfileSoulDissipation(profile, context, issues);
        ValidateAfterlifeProfileProgressionStrategy(profile, context, issues);
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
            if (art.TryGetProperty("costMultiplierPercent", out var costMultiplier) &&
                (!TryGetProfileInt(costMultiplier, out var multiplier) || multiplier < 100))
            {
                issues.Add(new ValidationIssue(
                    $"{artContext}.costMultiplierPercent",
                    IssueSeverity.Error,
                    "costMultiplierPercent особого искусства должен быть >= 100.",
                    code: "afterlife_entity_profile_invalid_special_art_cost_multiplier",
                    section: "AfterlifeEntityProfiles",
                    expected: "integer >= 100",
                    actual: costMultiplier.ToString()));
            }
        }
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
                }

                index++;
            }
        }

        ValidateProfileNonNegativeIntIfPresent(strategy, $"{context}.progressionStrategy", "lastUpdatedAtTurn", "afterlife_entity_profile_strategy_invalid_turn", issues);
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

    private static bool TryGetProfileInt(JsonElement value, out int integer)
    {
        integer = 0;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out integer);
    }
}
