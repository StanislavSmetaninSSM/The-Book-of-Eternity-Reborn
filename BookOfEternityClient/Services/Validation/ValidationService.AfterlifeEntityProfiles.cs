using System.Globalization;
using System.Text.Json;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private static readonly HashSet<string> AfterlifeEntityCurrencyDeltaKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "inkFeathers",
        "lightSparks"
    };

    private static readonly HashSet<string> AfterlifeEntityProgressionDeltaKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "enlightenment",
        "radiance"
    };

    private static readonly HashSet<string> AfterlifeActorQuestStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "active",
        "blocked",
        "completed",
        "failed",
        "cancelled"
    };

    private static readonly HashSet<string> AfterlifeActorActivityOutcomes = new(StringComparer.OrdinalIgnoreCase)
    {
        "completed",
        "failed",
        "cancelled",
        "blocked"
    };

    private static readonly HashSet<string> AfterlifeSpecialArtCombatEffectAxes = new(StringComparer.OrdinalIgnoreCase)
    {
        "rollMode",
        "conflictPosition",
        "controlState",
        "sideStrain",
        "playerSideStrain",
        "oppositionSideStrain",
        "tempoAdvantage",
        "counterPayoff",
        "actionEconomy",
        "actionCostAudit",
        "combatCondition",
        "combatConditions"
    };

    private static readonly string[] AfterlifeSpecialArtCombatEffectRequiredFields =
    [
        "summary",
        "trigger",
        "mechanicalAxis",
        "allowedPayoff",
        "limit",
        "auditRequirement"
    ];

    private static readonly HashSet<string> AfterlifeFateCardStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "locked",
        "hidden",
        "available",
        "unlocked"
    };

    private static readonly HashSet<string> AfterlifeRelationshipLockDirections = new(StringComparer.OrdinalIgnoreCase)
    {
        "positive",
        "negative"
    };

    private static readonly HashSet<string> AfterlifeMaskDeceptionRiskLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        "low",
        "medium",
        "high",
        "critical"
    };

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
        var hasFateCardUnlocks = root.TryGetProperty(AfterlifeEntityProfileState.FateCardUnlocksProperty, out var fateCardUnlocks);
        var hasProgressionOverrides = root.TryGetProperty(AfterlifeEntityProfileState.ProgressionOverridesProperty, out var progressionOverrides);
        var hasSpecialArtLearningReceipts = root.TryGetProperty(AfterlifeEntityProfileState.SpecialArtLearningReceiptsProperty, out var specialArtLearningReceipts);
        var hasGoalUpdates = root.TryGetProperty(AfterlifeEntityProfileState.GoalUpdatesProperty, out var goalUpdates);
        var hasQuestUpdates = root.TryGetProperty(AfterlifeEntityProfileState.QuestUpdatesProperty, out var questUpdates);
        var hasActivityUpdates = root.TryGetProperty(AfterlifeEntityProfileState.ActivityUpdatesProperty, out var activityUpdates);
        var hasActivityCompletions = root.TryGetProperty(AfterlifeEntityProfileState.CompleteActivitiesProperty, out var activityCompletions);
        var hasRelationshipChanges = root.TryGetProperty(AfterlifeEntityProfileState.RelationshipChangesProperty, out var relationshipChanges);
        var hasRelationshipLockUpdates = root.TryGetProperty(AfterlifeEntityProfileState.RelationshipLockUpdatesProperty, out var relationshipLockUpdates);
        var hasBreakthroughQuestUpdates = root.TryGetProperty(AfterlifeEntityProfileState.BreakthroughQuestUpdatesProperty, out var breakthroughQuestUpdates);
        var hasMaskAdds = root.TryGetProperty(AfterlifeEntityProfileState.MaskAddsProperty, out var maskAdds);
        var hasMaskUpdates = root.TryGetProperty(AfterlifeEntityProfileState.MaskUpdatesProperty, out var maskUpdates);
        var hasMaskRemovals = root.TryGetProperty(AfterlifeEntityProfileState.MaskRemovalsProperty, out var maskRemovals);
        var hasActiveMaskChanges = root.TryGetProperty(AfterlifeEntityProfileState.ActiveMaskChangesProperty, out var activeMaskChanges);
        var hasInvalidProgressionOverride = root.TryGetProperty(AfterlifeEntityProfileState.LastInvalidProgressionOverrideProperty, out _);
        var hasInvalidProfileCommand = root.TryGetProperty(AfterlifeEntityProfileState.LastInvalidCommandProperty, out _);
        if (!hasProfiles && !hasResponseProfiles && !hasUpdates && !hasCustomStateChanges && !hasProgressionOverrides && !hasSpecialArtLearningReceipts &&
            !hasFateCardUnlocks && !hasGoalUpdates && !hasQuestUpdates && !hasActivityUpdates && !hasActivityCompletions &&
            !hasRelationshipChanges && !hasRelationshipLockUpdates && !hasBreakthroughQuestUpdates &&
            !hasMaskAdds && !hasMaskUpdates && !hasMaskRemovals && !hasActiveMaskChanges)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{AfterlifeEntityProfileState.ProfilesProperty}",
                IssueSeverity.Error,
                "afterlife_entity_profiles.json должен содержать profiles[], afterlifeEntityProfileUpdates[], afterlifeEntityCustomStateChanges[], afterlifeFateCardUnlocks[], actor agency command surfaces, relationship gate command surfaces, mask command surfaces, afterlifeEntityProgressionOverrides[] или afterlifeSpecialArtLearningReceipts[].",
                code: "afterlife_entity_profile_missing_profiles",
                section: "AfterlifeEntityProfiles",
                expected: "profiles[] / afterlifeEntityProfileUpdates[] / afterlifeEntityCustomStateChanges[] / afterlifeFateCardUnlocks[] / afterlifeActorGoalUpdates[] / afterlifeActorQuestUpdates[] / afterlifeActorActivityUpdates[] / completeAfterlifeActorActivities[] / afterlifeRelationshipChanges[] / afterlifeRelationshipLockUpdates[] / afterlifeBreakthroughQuestUpdates[] / afterlifeActorMaskAdds[] / afterlifeActorMaskUpdates[] / afterlifeActorMaskRemovals[] / afterlifeActorActiveMaskChanges[] / afterlifeEntityProgressionOverrides[] / afterlifeSpecialArtLearningReceipts[]"));
        }

        if (hasInvalidProgressionOverride)
        {
            var reason = root.TryGetProperty(AfterlifeEntityProfileState.LastInvalidProgressionOverrideReasonProperty, out var reasonNode)
                ? reasonNode.ToString()
                : "invalid override";
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{AfterlifeEntityProfileState.LastInvalidProgressionOverrideProperty}",
                IssueSeverity.Error,
                "afterlifeEntityProgressionOverrides не был применён: форма override повреждена, цель authority отсутствует, или delta ссылается на неподдерживаемое/неизвестное духовное искусство.",
                code: "afterlife_entity_profile_progression_override_invalid_authority",
                section: "AfterlifeEntityProfiles",
                expected: "valid override shape, valid target profile, supported delta keys, and known specialArts[].artId keys",
                actual: reason));
        }

        if (hasInvalidProfileCommand)
        {
            var reason = root.TryGetProperty(AfterlifeEntityProfileState.LastInvalidCommandReasonProperty, out var reasonNode)
                ? reasonNode.ToString()
                : "invalid command";
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{AfterlifeEntityProfileState.LastInvalidCommandProperty}",
                IssueSeverity.Error,
                "Командная поверхность профилей сущностей посмертия не была применена: форма команды повреждена, цель/учитель/игрок/особое искусство отсутствует в authority, либо искусство нельзя обучать игроку.",
                code: "afterlife_entity_profile_command_invalid_authority",
                section: "AfterlifeEntityProfiles",
                expected: "valid command shape / known target profile / known teachable source special art",
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
        ValidateProfileArrayIfPresent(responseProfiles, hasResponseProfiles, $"{contextPrefix}.{AfterlifeEntityProfileState.ResponseProfilesProperty}", identities, issues, requireCurrentSpecialArtCombatEffect: true, requireActorMaterialization: true);
        ValidateProfileArrayIfPresent(
            updates,
            hasUpdates,
            $"{contextPrefix}.{AfterlifeEntityProfileState.UpdateProperty}",
            identities,
            issues,
            requireCurrentSpecialArtCombatEffect: true,
            requireActorMaterialization: true);
        ValidateAfterlifeEntityProfileFullCarrierAuthority(
            updates,
            hasUpdates,
            $"{contextPrefix}.{AfterlifeEntityProfileState.UpdateProperty}",
            issues);
        ValidateAfterlifeEntityCustomStateChangesIfPresent(
            customStateChanges,
            hasCustomStateChanges,
            $"{contextPrefix}.{AfterlifeEntityProfileState.CustomStateChangesProperty}",
            profileAuthority,
            issues);
        ValidateAfterlifeFateCardUnlocksIfPresent(
            fateCardUnlocks,
            hasFateCardUnlocks,
            $"{contextPrefix}.{AfterlifeEntityProfileState.FateCardUnlocksProperty}",
            profileAuthority,
            issues);
        ValidateAfterlifeActorGoalUpdatesIfPresent(
            goalUpdates,
            hasGoalUpdates,
            $"{contextPrefix}.{AfterlifeEntityProfileState.GoalUpdatesProperty}",
            profileAuthority,
            issues);
        ValidateAfterlifeActorQuestUpdatesIfPresent(
            questUpdates,
            hasQuestUpdates,
            $"{contextPrefix}.{AfterlifeEntityProfileState.QuestUpdatesProperty}",
            profileAuthority,
            issues);
        ValidateAfterlifeActorActivityUpdatesIfPresent(
            activityUpdates,
            hasActivityUpdates,
            $"{contextPrefix}.{AfterlifeEntityProfileState.ActivityUpdatesProperty}",
            profileAuthority,
            issues);
        ValidateCompleteAfterlifeActorActivitiesIfPresent(
            activityCompletions,
            hasActivityCompletions,
            $"{contextPrefix}.{AfterlifeEntityProfileState.CompleteActivitiesProperty}",
            profileAuthority,
            issues);
        ValidateAfterlifeRelationshipChangesIfPresent(
            relationshipChanges,
            hasRelationshipChanges,
            $"{contextPrefix}.{AfterlifeEntityProfileState.RelationshipChangesProperty}",
            profileAuthority,
            issues);
        ValidateAfterlifeRelationshipLockUpdatesIfPresent(
            relationshipLockUpdates,
            hasRelationshipLockUpdates,
            $"{contextPrefix}.{AfterlifeEntityProfileState.RelationshipLockUpdatesProperty}",
            profileAuthority,
            issues);
        ValidateAfterlifeBreakthroughQuestUpdatesIfPresent(
            breakthroughQuestUpdates,
            hasBreakthroughQuestUpdates,
            $"{contextPrefix}.{AfterlifeEntityProfileState.BreakthroughQuestUpdatesProperty}",
            profileAuthority,
            issues);
        ValidateAfterlifeActorMaskAddsIfPresent(
            maskAdds,
            hasMaskAdds,
            $"{contextPrefix}.{AfterlifeEntityProfileState.MaskAddsProperty}",
            profileAuthority,
            issues);
        ValidateAfterlifeActorMaskUpdatesIfPresent(
            maskUpdates,
            hasMaskUpdates,
            $"{contextPrefix}.{AfterlifeEntityProfileState.MaskUpdatesProperty}",
            profileAuthority,
            issues);
        ValidateAfterlifeActorMaskRemovalsIfPresent(
            maskRemovals,
            hasMaskRemovals,
            $"{contextPrefix}.{AfterlifeEntityProfileState.MaskRemovalsProperty}",
            profileAuthority,
            issues);
        ValidateAfterlifeActorActiveMaskChangesIfPresent(
            activeMaskChanges,
            hasActiveMaskChanges,
            $"{contextPrefix}.{AfterlifeEntityProfileState.ActiveMaskChangesProperty}",
            profileAuthority,
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
            profileAuthority,
            issues);
    }

    private void ValidateProfileArrayIfPresent(
        JsonElement profiles,
        bool hasProfiles,
        string context,
        HashSet<string> identities,
        List<ValidationIssue> issues,
        bool requireCurrentSpecialArtCombatEffect = false,
        bool requireActorMaterialization = false)
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
            ValidateAfterlifeEntityProfile(
                profile,
                $"{context}[{index++}]",
                identities,
                issues,
                requireCurrentSpecialArtCombatEffect,
                requireActorMaterialization);
        }
    }

    private void ValidateAfterlifeEntityProfile(
        JsonElement profile,
        string context,
        HashSet<string> identities,
        List<ValidationIssue> issues,
        bool requireCurrentSpecialArtCombatEffect = false,
        bool requireActorMaterialization = false)
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

        var actorIsPlayerSoul = string.Equals(actorType, "player_soul", StringComparison.OrdinalIgnoreCase);
        var idIsPlayerSoul = string.Equals(actorId, "player_soul", StringComparison.OrdinalIgnoreCase);
        if (actorIsPlayerSoul != idIsPlayerSoul)
        {
            issues.Add(new ValidationIssue(
                $"{context}.actorId",
                IssueSeverity.Error,
                "Идентичность player_soul зарезервирована: профиль души игрока должен использовать actorType=player_soul и actorId/actorRef=player_soul, а не-профили игрока не могут использовать actorId=player_soul.",
                code: "afterlife_entity_profile_player_identity_mismatch",
                section: "AfterlifeEntityProfiles",
                expected: "actorType=player_soul iff actorId/actorRef=player_soul",
                actual: $"{actorType ?? "missing"}:{actorId ?? "missing"}"));
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

            issues.AddRange(ActorMaterializationContract.ValidateAfterlifeProfile(
                profile,
                context,
                requireActorMaterialization,
                canTradeEvidence: requireActorMaterialization ? null : false));
        }

        ValidateAfterlifeProfileCurrencies(profile, context, issues);
        ValidateAfterlifeProfileProgression(profile, context, issues);
        ValidateAfterlifeProfileStandardArts(profile, context, issues);
        ValidateAfterlifeProfileSpecialArts(profile, context, actorType, actorId, issues, requireCurrentSpecialArtCombatEffect);
        ValidateAfterlifeProfileCustomStates(profile, context, issues);
        ValidateAfterlifeProfileFateCards(profile, context, issues);
        ValidateAfterlifeProfileSoulDissipation(profile, context, issues);
        ValidateAfterlifeProfileProgressionStrategy(profile, context, issues);
        ValidateAfterlifeProfileProgressionLedger(profile, context, issues);
        ValidateAfterlifeProfileLedger(profile, context, issues);
        ValidateAfterlifeProfileAgency(profile, context, issues);
        ValidateAfterlifeProfileRelationships(profile, context, issues);
        ValidateAfterlifeProfileMasks(profile, context, issues);
        ValidateStringArrayIfPresent(profile, context, "warnings", "afterlife_entity_profile_warnings_not_array", issues);
    }

    private void ValidateAfterlifeProfileCurrencies(JsonElement profile, string context, List<ValidationIssue> issues)
    {
        if (!TryRequireProfileObject(profile, context, "currencies", "afterlife_entity_profile_missing_currencies", issues, out var currencies))
            return;

        ValidateProfileNonNegativeInt(currencies, $"{context}.currencies", "inkFeathers", "afterlife_entity_profile_negative_currency", issues);
        ValidateProfileNonNegativeInt(currencies, $"{context}.currencies", "lightSparks", "afterlife_entity_profile_negative_currency", issues);
        if (IsChaosSeaProfile(profile) &&
            currencies.TryGetProperty("lightSparks", out var lightSparksNode) &&
            TryGetProfileInt(lightSparksNode, out var lightSparks) &&
            lightSparks > 0)
        {
            issues.Add(new ValidationIssue(
                $"{context}.currencies.lightSparks",
                IssueSeverity.Error,
                "Профили сущностей Моря Хаоса не могут хранить Искры Света.",
                code: "afterlife_entity_profile_chaos_light_sparks_forbidden",
                section: "AfterlifeEntityProfiles",
                expected: "lightSparks = 0 for Chaos Sea profiles",
                actual: lightSparks.ToString()));
        }
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

    private void ValidateAfterlifeProfileFateCards(JsonElement profile, string context, List<ValidationIssue> issues)
    {
        if (!profile.TryGetProperty("fateCards", out var fateCards))
            return;

        if (fateCards.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{context}.fateCards",
                IssueSeverity.Error,
                "fateCards профиля сущности посмертия должен быть array.",
                code: "afterlife_entity_profile_fate_cards_not_array",
                section: "AfterlifeEntityProfiles",
                expected: "array",
                actual: fateCards.ValueKind.ToString()));
            return;
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var card in fateCards.EnumerateArray())
        {
            var cardContext = $"{context}.fateCards[{index++}]";
            if (card.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue(
                    cardContext,
                    IssueSeverity.Error,
                    "fateCards[] entry должен быть object.",
                    code: "afterlife_entity_profile_fate_card_not_object",
                    section: "AfterlifeEntityProfiles",
                    expected: "object",
                    actual: card.ValueKind.ToString()));
                continue;
            }

            var cardId = RequireProfileString(card, cardContext, "cardId", "afterlife_entity_profile_fate_card_missing_card_id", issues);
            RequireProfileString(card, cardContext, "nameRu", "afterlife_entity_profile_fate_card_missing_name_ru", issues);
            var status = RequireProfileString(card, cardContext, "status", "afterlife_entity_profile_fate_card_missing_status", issues);
            RequireProfileString(card, cardContext, "storyMeaning", "afterlife_entity_profile_fate_card_missing_story_meaning", issues);

            if (!string.IsNullOrWhiteSpace(cardId) && !ids.Add(cardId))
            {
                issues.Add(new ValidationIssue(
                    $"{cardContext}.cardId",
                    IssueSeverity.Error,
                    "fateCards[] не должен содержать дубликаты cardId.",
                    code: "afterlife_entity_profile_fate_card_duplicate",
                    section: "AfterlifeEntityProfiles",
                    expected: "unique cardId",
                    actual: cardId));
            }

            if (!string.IsNullOrWhiteSpace(status) && !AfterlifeFateCardStatuses.Contains(status))
            {
                issues.Add(new ValidationIssue(
                    $"{cardContext}.status",
                    IssueSeverity.Error,
                    "status карты судьбы Хранителя должен быть supported state.",
                    code: "afterlife_entity_profile_fate_card_invalid_status",
                    section: "AfterlifeEntityProfiles",
                    expected: string.Join("/", AfterlifeFateCardStatuses.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                    actual: status));
            }

            if (!card.TryGetProperty("unlockConditions", out var unlockConditions) ||
                unlockConditions.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                issues.Add(new ValidationIssue(
                    $"{cardContext}.unlockConditions",
                    IssueSeverity.Error,
                    "fateCards[] должен описывать условия открытия карты судьбы.",
                    code: "afterlife_entity_profile_fate_card_missing_unlock_conditions",
                    section: "AfterlifeEntityProfiles",
                    expected: "unlockConditions object/string/array"));
            }

            var isUnlocked = string.Equals(status, "unlocked", StringComparison.OrdinalIgnoreCase);
            var hasEffects = HasFateCardMechanicalEffects(card);
            ValidateFateCardEffectArrays(card, cardContext, issues);

            if (!isUnlocked && hasEffects)
            {
                issues.Add(new ValidationIssue(
                    cardContext,
                    IssueSeverity.Error,
                    "Locked/hidden/available карта судьбы не может содержать активные механические эффекты; эффекты появляются только после afterlifeFateCardUnlocks[].",
                    code: "afterlife_entity_profile_fate_card_locked_effects_active",
                    section: "AfterlifeEntityProfiles",
                    expected: "no guardianEffects/playerUnlocks/politicalEffects/combatEffects/trainingUnlocks until status=unlocked",
                    actual: status ?? "missing"));
            }

            if (isUnlocked)
            {
                ValidateProfileNonNegativeInt(card, cardContext, "appliedAtTurn", "afterlife_entity_profile_fate_card_invalid_applied_turn", issues);
                if (!HasFateCardEvidence(card))
                {
                    issues.Add(new ValidationIssue(
                        $"{cardContext}.evidence",
                        IssueSeverity.Error,
                        "Unlocked карта судьбы требует evidence или evidenceSummary.",
                        code: "afterlife_entity_profile_fate_card_missing_evidence",
                        section: "AfterlifeEntityProfiles",
                        expected: "non-empty evidence object or evidenceSummary"));
                }

                if (!hasEffects)
                {
                    issues.Add(new ValidationIssue(
                        cardContext,
                        IssueSeverity.Error,
                        "Unlocked карта судьбы должна иметь хотя бы один активный механический эффект.",
                        code: "afterlife_entity_profile_fate_card_unlocked_missing_effects",
                        section: "AfterlifeEntityProfiles",
                        expected: "guardianEffects/playerUnlocks/politicalEffects/combatEffects/trainingUnlocks"));
                }
            }
        }
    }

    private void ValidateAfterlifeFateCardUnlocksIfPresent(
        JsonElement unlocks,
        bool hasUnlocks,
        string context,
        IReadOnlyDictionary<string, JsonElement> profileAuthority,
        List<ValidationIssue> issues)
    {
        if (!hasUnlocks)
            return;

        if (unlocks.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "afterlifeFateCardUnlocks должен быть array.",
                code: "afterlife_entity_profile_fate_card_unlocks_not_array",
                section: "AfterlifeEntityProfiles",
                expected: "array",
                actual: unlocks.ValueKind.ToString()));
            return;
        }

        var index = 0;
        foreach (var unlock in unlocks.EnumerateArray())
        {
            var unlockContext = $"{context}[{index++}]";
            var targetProfile = ValidateAfterlifeActorAgencyCommandTarget(unlock, unlockContext, profileAuthority, "fate_card_unlock", issues);
            if (targetProfile == null)
                continue;

            var cardId = RequireProfileString(unlock, unlockContext, "cardId", "afterlife_entity_profile_fate_card_unlock_missing_card_id", issues);
            ValidateProfileNonNegativeInt(unlock, unlockContext, "appliedAtTurn", "afterlife_entity_profile_fate_card_unlock_invalid_turn", issues);
            ValidateFateCardEffectArrays(unlock, unlockContext, issues);

            if (!HasFateCardEvidence(unlock))
            {
                issues.Add(new ValidationIssue(
                    $"{unlockContext}.evidence",
                    IssueSeverity.Error,
                    "afterlifeFateCardUnlocks entry должен иметь evidence или evidenceSummary.",
                    code: "afterlife_entity_profile_fate_card_unlock_missing_evidence",
                    section: "AfterlifeEntityProfiles",
                    expected: "non-empty evidence object or evidenceSummary"));
            }

            if (!HasFateCardMechanicalEffects(unlock))
            {
                issues.Add(new ValidationIssue(
                    unlockContext,
                    IssueSeverity.Error,
                    "afterlifeFateCardUnlocks должен применить хотя бы один проверяемый механический эффект.",
                    code: "afterlife_entity_profile_fate_card_unlock_missing_effects",
                    section: "AfterlifeEntityProfiles",
                    expected: "guardianEffects/playerUnlocks/politicalEffects/combatEffects/trainingUnlocks"));
            }

            if (!string.IsNullOrWhiteSpace(cardId) &&
                !ProfileContainsFateCard(targetProfile.Value, cardId))
            {
                issues.Add(new ValidationIssue(
                    $"{unlockContext}.cardId",
                    IssueSeverity.Error,
                    "afterlifeFateCardUnlocks должен ссылаться на существующую fateCards[].cardId целевого профиля.",
                    code: "afterlife_entity_profile_fate_card_unlock_unknown_card",
                    section: "AfterlifeEntityProfiles",
                    expected: "cardId exists in target profile fateCards[]",
                    actual: cardId));
            }
        }
    }

    private void ValidateAfterlifeEntityCustomStateChangesIfPresent(
        JsonElement changes,
        bool hasChanges,
        string context,
        IReadOnlyDictionary<string, JsonElement> profileAuthority,
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
            if (change.ValueKind != JsonValueKind.Object)
                continue;

            var targetKey = BuildAfterlifeEntityProfileIdentityKey(change);
            if (!string.IsNullOrWhiteSpace(targetKey) && !profileAuthority.ContainsKey(targetKey))
            {
                issues.Add(new ValidationIssue(
                    changeContext,
                    IssueSeverity.Error,
                    "afterlifeEntityCustomStateChanges должен ссылаться на существующий профиль сущности посмертия; неизвестная цель не должна исчезать no-op.",
                    code: "afterlife_entity_profile_custom_state_change_unknown_target",
                    section: "AfterlifeEntityProfiles",
                    expected: "actorType + actorId/actorRef present in profiles[] or afterlifeEntityProfileUpdates[]",
                    actual: targetKey));
            }
        }
    }

    private static bool HasFateCardEvidence(JsonElement cardOrUnlock)
    {
        if (cardOrUnlock.ValueKind != JsonValueKind.Object)
            return false;

        if (cardOrUnlock.TryGetProperty("evidence", out var evidence) &&
            evidence.ValueKind == JsonValueKind.Object &&
            evidence.EnumerateObject().Any())
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(GetProfileString(cardOrUnlock, "evidenceSummary"));
    }

    private static bool HasFateCardMechanicalEffects(JsonElement cardOrUnlock)
    {
        if (cardOrUnlock.ValueKind != JsonValueKind.Object)
            return false;

        return AfterlifeEntityProfileState.FateCardMechanicalEffectProperties.Any(propertyName =>
            cardOrUnlock.TryGetProperty(propertyName, out var effects) &&
            effects.ValueKind == JsonValueKind.Array &&
            effects.GetArrayLength() > 0);
    }

    private static bool ProfileContainsFateCard(JsonElement profile, string cardId)
    {
        if (profile.ValueKind != JsonValueKind.Object ||
            !profile.TryGetProperty("fateCards", out var fateCards) ||
            fateCards.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return fateCards.EnumerateArray().Any(card =>
            card.ValueKind == JsonValueKind.Object &&
            string.Equals(GetProfileString(card, "cardId"), cardId, StringComparison.OrdinalIgnoreCase));
    }

    private void ValidateFateCardEffectArrays(JsonElement cardOrUnlock, string context, List<ValidationIssue> issues)
    {
        foreach (var propertyName in AfterlifeEntityProfileState.FateCardMechanicalEffectProperties)
        {
            if (!cardOrUnlock.TryGetProperty(propertyName, out var effects))
                continue;

            if (effects.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{propertyName}",
                    IssueSeverity.Error,
                    "Механические эффекты карты судьбы должны быть массивом.",
                    code: "afterlife_entity_profile_fate_card_effects_not_array",
                    section: "AfterlifeEntityProfiles",
                    expected: "array",
                    actual: effects.ValueKind.ToString()));
                continue;
            }

            var index = 0;
            foreach (var effect in effects.EnumerateArray())
            {
                var effectContext = $"{context}.{propertyName}[{index++}]";
                if (effect.ValueKind != JsonValueKind.Object)
                {
                    issues.Add(new ValidationIssue(
                        effectContext,
                        IssueSeverity.Error,
                        "Каждый механический эффект карты судьбы должен быть object.",
                        code: "afterlife_entity_profile_fate_card_effect_not_object",
                        section: "AfterlifeEntityProfiles",
                        expected: "object",
                        actual: effect.ValueKind.ToString()));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(GetProfileString(effect, "summary")))
                {
                    issues.Add(new ValidationIssue(
                        $"{effectContext}.summary",
                        IssueSeverity.Error,
                        "Механический эффект карты судьбы должен иметь summary, чтобы GM и игрок понимали применимый результат.",
                        code: "afterlife_entity_profile_fate_card_effect_missing_summary",
                        section: "AfterlifeEntityProfiles",
                        expected: "non-empty summary"));
                }
            }
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
        else
        {
            var hasUpsertOperations = hasUpserts &&
                                      upserts.ValueKind == JsonValueKind.Array &&
                                      upserts.GetArrayLength() > 0;
            var hasRemovalOperations = hasRemovals &&
                                       removals.ValueKind == JsonValueKind.Array &&
                                       removals.GetArrayLength() > 0;
            var allPresentChildrenAreArrays =
                (!hasUpserts || upserts.ValueKind == JsonValueKind.Array) &&
                (!hasRemovals || removals.ValueKind == JsonValueKind.Array);
            if (allPresentChildrenAreArrays && !hasUpsertOperations && !hasRemovalOperations)
            {
                issues.Add(new ValidationIssue(
                    context,
                    IssueSeverity.Error,
                    "afterlifeEntityCustomStateChanges entry должен содержать хотя бы одно добавление/обновление или удаление.",
                    code: "afterlife_entity_profile_custom_state_change_empty",
                    section: "AfterlifeEntityProfiles",
                    expected: "non-empty statesToAddOrUpdate[] and/or statesToRemove[]"));
            }
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

    private void ValidateAfterlifeActorGoalUpdatesIfPresent(
        JsonElement updates,
        bool hasUpdates,
        string context,
        IReadOnlyDictionary<string, JsonElement> profileAuthority,
        List<ValidationIssue> issues)
    {
        if (!hasUpdates)
            return;

        if (updates.ValueKind != JsonValueKind.Array)
        {
            AddAgencyIssue(context, "afterlife_entity_profile_agency_goal_updates_not_array", "afterlifeActorGoalUpdates должен быть array.", "array", updates.ValueKind.ToString(), issues);
            return;
        }

        var index = 0;
        foreach (var update in updates.EnumerateArray())
        {
            var updateContext = $"{context}[{index++}]";
            if (ValidateAfterlifeActorAgencyCommandTarget(update, updateContext, profileAuthority, "goal", issues) == null)
                continue;

            RequireProfileString(update, updateContext, "goalId", "afterlife_entity_profile_agency_goal_missing_goal_id", issues);
            RequireProfileString(update, updateContext, "shortTermGoal", "afterlife_entity_profile_agency_goal_missing_short_term_goal", issues);
            RequireProfileString(update, updateContext, "longTermGoal", "afterlife_entity_profile_agency_goal_missing_long_term_goal", issues);
            RequireProfileString(update, updateContext, "plan", "afterlife_entity_profile_agency_goal_missing_plan", issues);
            RequireProfileString(update, updateContext, "gmThoughtsSummary", "afterlife_entity_profile_agency_goal_missing_gm_thoughts", issues);
            ValidateProfileNonNegativeInt(update, updateContext, "updatedAtTurn", "afterlife_entity_profile_agency_goal_invalid_turn", issues);
        }
    }

    private void ValidateAfterlifeActorQuestUpdatesIfPresent(
        JsonElement updates,
        bool hasUpdates,
        string context,
        IReadOnlyDictionary<string, JsonElement> profileAuthority,
        List<ValidationIssue> issues)
    {
        if (!hasUpdates)
            return;

        if (updates.ValueKind != JsonValueKind.Array)
        {
            AddAgencyIssue(context, "afterlife_entity_profile_agency_quest_updates_not_array", "afterlifeActorQuestUpdates должен быть array.", "array", updates.ValueKind.ToString(), issues);
            return;
        }

        var index = 0;
        foreach (var update in updates.EnumerateArray())
        {
            var updateContext = $"{context}[{index++}]";
            if (ValidateAfterlifeActorAgencyCommandTarget(update, updateContext, profileAuthority, "quest", issues) == null)
                continue;

            RequireProfileString(update, updateContext, "questId", "afterlife_entity_profile_agency_quest_missing_quest_id", issues);
            RequireProfileString(update, updateContext, "goalId", "afterlife_entity_profile_agency_quest_missing_goal_id", issues);
            RequireProfileString(update, updateContext, "title", "afterlife_entity_profile_agency_quest_missing_title", issues);
            var status = RequireProfileString(update, updateContext, "status", "afterlife_entity_profile_agency_quest_missing_status", issues);
            RequireProfileString(update, updateContext, "planSummary", "afterlife_entity_profile_agency_quest_missing_plan_summary", issues);
            RequireProfileString(update, updateContext, "successCondition", "afterlife_entity_profile_agency_quest_missing_success_condition", issues);
            ValidateProfileNonNegativeInt(update, updateContext, "createdAtTurn", "afterlife_entity_profile_agency_quest_invalid_turn", issues);
            ValidateActorQuestStatus(status, $"{updateContext}.status", issues);
        }
    }

    private void ValidateAfterlifeActorActivityUpdatesIfPresent(
        JsonElement updates,
        bool hasUpdates,
        string context,
        IReadOnlyDictionary<string, JsonElement> profileAuthority,
        List<ValidationIssue> issues)
    {
        if (!hasUpdates)
            return;

        if (updates.ValueKind != JsonValueKind.Array)
        {
            AddAgencyIssue(context, "afterlife_entity_profile_agency_activity_updates_not_array", "afterlifeActorActivityUpdates должен быть array.", "array", updates.ValueKind.ToString(), issues);
            return;
        }

        var index = 0;
        foreach (var update in updates.EnumerateArray())
        {
            var updateContext = $"{context}[{index++}]";
            if (ValidateAfterlifeActorAgencyCommandTarget(update, updateContext, profileAuthority, "activity", issues) == null)
                continue;

            RequireProfileString(update, updateContext, "activityId", "afterlife_entity_profile_agency_activity_missing_activity_id", issues);
            RequireProfileString(update, updateContext, "goalId", "afterlife_entity_profile_agency_activity_missing_goal_id", issues);
            RequireProfileString(update, updateContext, "linkedQuestId", "afterlife_entity_profile_agency_activity_missing_linked_quest_id", issues);
            RequireProfileString(update, updateContext, "activityType", "afterlife_entity_profile_agency_activity_missing_type", issues);
            RequireProfileString(update, updateContext, "summary", "afterlife_entity_profile_agency_activity_missing_summary", issues);
            var status = RequireProfileString(update, updateContext, "status", "afterlife_entity_profile_agency_activity_missing_status", issues);
            RequireProfileString(update, updateContext, "gmThoughtsSummary", "afterlife_entity_profile_agency_activity_missing_gm_thoughts", issues);
            ValidateProfileNonNegativeInt(update, updateContext, "startedAtTurn", "afterlife_entity_profile_agency_activity_invalid_turn", issues);
            ValidateActorActivityStatus(status, $"{updateContext}.status", issues);
        }
    }

    private void ValidateCompleteAfterlifeActorActivitiesIfPresent(
        JsonElement completions,
        bool hasCompletions,
        string context,
        IReadOnlyDictionary<string, JsonElement> profileAuthority,
        List<ValidationIssue> issues)
    {
        if (!hasCompletions)
            return;

        if (completions.ValueKind != JsonValueKind.Array)
        {
            AddAgencyIssue(context, "afterlife_entity_profile_agency_activity_completions_not_array", "completeAfterlifeActorActivities должен быть array.", "array", completions.ValueKind.ToString(), issues);
            return;
        }

        var index = 0;
        foreach (var completion in completions.EnumerateArray())
        {
            var completionContext = $"{context}[{index++}]";
            var targetProfile = ValidateAfterlifeActorAgencyCommandTarget(completion, completionContext, profileAuthority, "activity_completion", issues);
            if (targetProfile == null)
                continue;

            var activityId = RequireProfileString(completion, completionContext, "activityId", "afterlife_entity_profile_agency_activity_completion_missing_activity_id", issues);
            var outcome = RequireProfileString(completion, completionContext, "outcome", "afterlife_entity_profile_agency_activity_completion_missing_outcome", issues);
            RequireProfileString(completion, completionContext, "summary", "afterlife_entity_profile_agency_activity_completion_missing_summary", issues);
            ValidateProfileNonNegativeInt(completion, completionContext, "completedAtTurn", "afterlife_entity_profile_agency_activity_completion_invalid_turn", issues);
            ValidateActorActivityOutcome(outcome, $"{completionContext}.outcome", issues);
            if (completion.TryGetProperty("resultingQuestStatus", out var resultingQuestStatus) &&
                resultingQuestStatus.ValueKind != JsonValueKind.Null)
            {
                ValidateActorQuestStatus(GetProfileString(completion, "resultingQuestStatus"), $"{completionContext}.resultingQuestStatus", issues);
            }

            if (!string.IsNullOrWhiteSpace(activityId) &&
                (!targetProfile.Value.TryGetProperty("currentActivity", out var currentActivity) ||
                 currentActivity.ValueKind != JsonValueKind.Object ||
                 !string.Equals(GetProfileString(currentActivity, "activityId"), activityId, StringComparison.OrdinalIgnoreCase)))
            {
                AddAgencyIssue(
                    $"{completionContext}.activityId",
                    "afterlife_entity_profile_agency_activity_completion_without_current_activity",
                    "completeAfterlifeActorActivities должен закрывать текущую currentActivity целевой сущности.",
                    "activityId equals target currentActivity.activityId",
                    activityId,
                    issues);
            }
        }
    }

    private void ValidateAfterlifeRelationshipChangesIfPresent(
        JsonElement changes,
        bool hasChanges,
        string context,
        IReadOnlyDictionary<string, JsonElement> profileAuthority,
        List<ValidationIssue> issues)
    {
        if (!hasChanges)
            return;

        if (changes.ValueKind != JsonValueKind.Array)
        {
            AddRelationshipIssue(context, "afterlife_entity_profile_relationship_changes_not_array", "afterlifeRelationshipChanges должен быть array.", "array", changes.ValueKind.ToString(), issues);
            return;
        }

        var index = 0;
        foreach (var change in changes.EnumerateArray())
        {
            var changeContext = $"{context}[{index++}]";
            if (ValidateAfterlifeActorAgencyCommandTarget(change, changeContext, profileAuthority, "relationship_change", issues) == null)
                continue;

            ValidateRelationshipIdentityFields(change, changeContext, issues);
            var hasValue = change.TryGetProperty("value", out var value);
            var hasDelta = change.TryGetProperty("valueDelta", out var valueDelta);
            if (!hasValue && !hasDelta)
            {
                AddRelationshipIssue(changeContext, "afterlife_entity_profile_relationship_change_missing_value", "afterlifeRelationshipChanges должен содержать value или valueDelta.", "value or valueDelta", "missing", issues);
            }
            else
            {
                if (hasValue)
                    ValidateRelationshipValue(value, $"{changeContext}.value", issues);
                if (hasDelta && !TryGetProfileInt(valueDelta, out _))
                    AddRelationshipIssue($"{changeContext}.valueDelta", "afterlife_entity_profile_relationship_change_invalid_delta", "valueDelta должен быть integer.", "integer", valueDelta.ToString(), issues);
            }

            RequireProfileString(change, changeContext, "reason", "afterlife_entity_profile_relationship_change_missing_reason", issues);
            RequireProfileString(change, changeContext, "evidence", "afterlife_entity_profile_relationship_change_missing_evidence", issues);
            RequireProfileString(change, changeContext, "gmThoughtsSummary", "afterlife_entity_profile_relationship_change_missing_gm_thoughts", issues);
            ValidateProfileNonNegativeInt(change, changeContext, "updatedAtTurn", "afterlife_entity_profile_relationship_invalid_turn", issues);
        }
    }

    private void ValidateAfterlifeRelationshipLockUpdatesIfPresent(
        JsonElement updates,
        bool hasUpdates,
        string context,
        IReadOnlyDictionary<string, JsonElement> profileAuthority,
        List<ValidationIssue> issues)
    {
        if (!hasUpdates)
            return;

        if (updates.ValueKind != JsonValueKind.Array)
        {
            AddRelationshipIssue(context, "afterlife_entity_profile_relationship_lock_updates_not_array", "afterlifeRelationshipLockUpdates должен быть array.", "array", updates.ValueKind.ToString(), issues);
            return;
        }

        var index = 0;
        foreach (var update in updates.EnumerateArray())
        {
            var updateContext = $"{context}[{index++}]";
            if (ValidateAfterlifeActorAgencyCommandTarget(update, updateContext, profileAuthority, "relationship_lock", issues) == null)
                continue;

            ValidateRelationshipIdentityFields(update, updateContext, issues);
            RequireProfileString(update, updateContext, "gmThoughtsSummary", "afterlife_entity_profile_relationship_lock_missing_gm_thoughts", issues);
            if (!update.TryGetProperty("relationshipLock", out var relationshipLock) || relationshipLock.ValueKind != JsonValueKind.Object)
            {
                AddRelationshipIssue($"{updateContext}.relationshipLock", "afterlife_entity_profile_relationship_lock_missing", "afterlifeRelationshipLockUpdates должен содержать relationshipLock object.", "relationshipLock object", update.TryGetProperty("relationshipLock", out var actual) ? actual.ToString() : "missing", issues);
                continue;
            }

            ValidateRelationshipLock(relationshipLock, $"{updateContext}.relationshipLock", issues);
        }
    }

    private void ValidateAfterlifeBreakthroughQuestUpdatesIfPresent(
        JsonElement updates,
        bool hasUpdates,
        string context,
        IReadOnlyDictionary<string, JsonElement> profileAuthority,
        List<ValidationIssue> issues)
    {
        if (!hasUpdates)
            return;

        if (updates.ValueKind != JsonValueKind.Array)
        {
            AddRelationshipIssue(context, "afterlife_entity_profile_relationship_quest_updates_not_array", "afterlifeBreakthroughQuestUpdates должен быть array.", "array", updates.ValueKind.ToString(), issues);
            return;
        }

        var index = 0;
        foreach (var update in updates.EnumerateArray())
        {
            var updateContext = $"{context}[{index++}]";
            if (ValidateAfterlifeActorAgencyCommandTarget(update, updateContext, profileAuthority, "relationship_quest", issues) == null)
                continue;

            RequireProfileString(update, updateContext, "relationshipId", "afterlife_entity_profile_relationship_missing_relationship_id", issues);
            ValidateRelationshipGateQuest(update, updateContext, issues);
        }
    }

    private void ValidateAfterlifeProfileRelationships(JsonElement profile, string context, List<ValidationIssue> issues)
    {
        if (!profile.TryGetProperty(AfterlifeEntityProfileState.RelationshipsProperty, out var relationships))
            return;

        if (relationships.ValueKind != JsonValueKind.Array)
        {
            AddRelationshipIssue($"{context}.relationships", "afterlife_entity_profile_relationships_not_array", "relationships профиля сущности посмертия должен быть array.", "array", relationships.ValueKind.ToString(), issues);
            return;
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var relationship in relationships.EnumerateArray())
        {
            var relationshipContext = $"{context}.relationships[{index++}]";
            if (relationship.ValueKind != JsonValueKind.Object)
            {
                AddRelationshipIssue(relationshipContext, "afterlife_entity_profile_relationship_not_object", "relationships[] entry должен быть object.", "object", relationship.ValueKind.ToString(), issues);
                continue;
            }

            var relationshipId = ValidateRelationshipIdentityFields(relationship, relationshipContext, issues);
            if (!string.IsNullOrWhiteSpace(relationshipId) && !ids.Add(relationshipId))
                AddRelationshipIssue($"{relationshipContext}.relationshipId", "afterlife_entity_profile_relationship_duplicate_id", "relationships[] не должен содержать дубликаты relationshipId.", "unique relationshipId", relationshipId, issues);

            if (relationship.TryGetProperty("value", out var value))
                ValidateRelationshipValue(value, $"{relationshipContext}.value", issues);
            else
                AddRelationshipIssue($"{relationshipContext}.value", "afterlife_entity_profile_relationship_missing_value", "relationship должен содержать value.", "integer -100..100", "missing", issues);

            RequireProfileString(relationship, relationshipContext, "relationshipTier", "afterlife_entity_profile_relationship_missing_tier", issues);

            if (relationship.TryGetProperty("relationshipLock", out var relationshipLock) && relationshipLock.ValueKind != JsonValueKind.Null)
                ValidateRelationshipLock(relationshipLock, $"{relationshipContext}.relationshipLock", issues);

            if (relationship.TryGetProperty(AfterlifeEntityProfileState.RelationshipGateQuestsProperty, out var gateQuests))
                ValidateRelationshipGateQuestArray(gateQuests, $"{relationshipContext}.{AfterlifeEntityProfileState.RelationshipGateQuestsProperty}", issues);

            ValidateRelationshipThresholdGate(relationship, relationshipContext, issues);
        }
    }

    private string? ValidateRelationshipIdentityFields(JsonElement relationship, string context, List<ValidationIssue> issues)
    {
        var relationshipId = RequireProfileString(relationship, context, "relationshipId", "afterlife_entity_profile_relationship_missing_relationship_id", issues);
        var axis = RequireProfileString(relationship, context, "axis", "afterlife_entity_profile_relationship_missing_axis", issues);
        if (!string.IsNullOrWhiteSpace(axis) && !AfterlifeEntityProfileState.RelationshipAxes.Contains(axis))
        {
            AddRelationshipIssue(
                $"{context}.axis",
                "afterlife_entity_profile_relationship_invalid_axis",
                "axis отношения посмертия должен быть поддерживаемым типом связи.",
                string.Join("/", AfterlifeEntityProfileState.RelationshipAxes.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                axis,
                issues);
        }

        var targetType = RequireProfileString(relationship, context, "targetActorType", "afterlife_entity_profile_relationship_missing_target_type", issues);
        if (!string.IsNullOrWhiteSpace(targetType) && !AfterlifeEntityProfileState.ActorTypes.Contains(targetType))
        {
            AddRelationshipIssue(
                $"{context}.targetActorType",
                "afterlife_entity_profile_relationship_invalid_target_type",
                "targetActorType отношения должен ссылаться на сущность посмертия.",
                string.Join("/", AfterlifeEntityProfileState.ActorTypes.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                targetType,
                issues);
        }

        if (string.IsNullOrWhiteSpace(GetProfileString(relationship, "targetActorId")) &&
            string.IsNullOrWhiteSpace(GetProfileString(relationship, "targetActorRef")))
        {
            AddRelationshipIssue($"{context}.targetActorId", "afterlife_entity_profile_relationship_missing_target_id", "relationship должен иметь targetActorId или targetActorRef.", "non-empty targetActorId or targetActorRef", "missing", issues);
        }

        return relationshipId;
    }

    private void ValidateRelationshipLock(JsonElement relationshipLock, string context, List<ValidationIssue> issues)
    {
        if (relationshipLock.ValueKind != JsonValueKind.Object)
        {
            AddRelationshipIssue(context, "afterlife_entity_profile_relationship_lock_not_object", "relationshipLock должен быть object.", "object", relationshipLock.ValueKind.ToString(), issues);
            return;
        }

        var lockState = RequireProfileString(relationshipLock, context, "lockState", "afterlife_entity_profile_relationship_lock_missing_state", issues);
        if (!string.IsNullOrWhiteSpace(lockState) && !AfterlifeEntityProfileState.RelationshipLockStates.Contains(lockState))
        {
            AddRelationshipIssue($"{context}.lockState", "afterlife_entity_profile_relationship_lock_invalid_state", "lockState должен быть поддерживаемым состоянием гейта отношений.", string.Join("/", AfterlifeEntityProfileState.RelationshipLockStates), lockState, issues);
        }

        var direction = RequireProfileString(relationshipLock, context, "direction", "afterlife_entity_profile_relationship_lock_missing_direction", issues);
        if (!string.IsNullOrWhiteSpace(direction) && !AfterlifeRelationshipLockDirections.Contains(direction))
            AddRelationshipIssue($"{context}.direction", "afterlife_entity_profile_relationship_lock_invalid_direction", "direction должен быть positive или negative.", "positive/negative", direction, issues);

        ValidateProfileNonNegativeInt(relationshipLock, context, "updatedAtTurn", "afterlife_entity_profile_relationship_invalid_turn", issues);
        if (!relationshipLock.TryGetProperty("threshold", out var threshold) || !TryGetProfileInt(threshold, out var thresholdValue) || thresholdValue is < -100 or > 100)
            AddRelationshipIssue($"{context}.threshold", "afterlife_entity_profile_relationship_lock_invalid_threshold", "threshold должен быть integer -100..100.", "integer -100..100", relationshipLock.TryGetProperty("threshold", out var actual) ? actual.ToString() : "missing", issues);

        RequireProfileString(relationshipLock, context, "reason", "afterlife_entity_profile_relationship_lock_missing_reason", issues);
        RequireProfileString(relationshipLock, context, "evidence", "afterlife_entity_profile_relationship_lock_missing_evidence", issues);

        if (string.Equals(lockState, "positive_locked", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(GetProfileString(relationshipLock, "breakthroughQuestId")))
        {
            AddRelationshipIssue($"{context}.breakthroughQuestId", "afterlife_entity_profile_relationship_positive_lock_missing_breakthrough", "positive_locked relationshipLock требует breakthroughQuestId.", "non-empty breakthroughQuestId", "missing", issues);
        }

        if (string.Equals(lockState, "negative_locked", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(GetProfileString(relationshipLock, "redemptionQuestId")))
        {
            AddRelationshipIssue($"{context}.redemptionQuestId", "afterlife_entity_profile_relationship_negative_lock_missing_redemption", "negative_locked relationshipLock требует redemptionQuestId.", "non-empty redemptionQuestId", "missing", issues);
        }

        if (string.Equals(lockState, "point_of_no_return", StringComparison.OrdinalIgnoreCase))
        {
            var pointOfNoReturn = relationshipLock.TryGetProperty("pointOfNoReturn", out var pointOfNoReturnNode) &&
                                  pointOfNoReturnNode.ValueKind == JsonValueKind.True;
            var hasProof = !string.IsNullOrWhiteSpace(GetProfileString(relationshipLock, "proofSummary")) ||
                           (relationshipLock.TryGetProperty("proof", out var proof) &&
                            proof.ValueKind == JsonValueKind.Object &&
                            proof.EnumerateObject().Any());
            if (!pointOfNoReturn || !hasProof)
            {
                AddRelationshipIssue($"{context}.proofSummary", "afterlife_entity_profile_relationship_point_of_no_return_missing_proof", "point_of_no_return требует pointOfNoReturn=true и proof/proofSummary.", "pointOfNoReturn=true + proofSummary/proof", relationshipLock.ToString(), issues);
            }
        }
    }

    private void ValidateRelationshipThresholdGate(JsonElement relationship, string context, List<ValidationIssue> issues)
    {
        if (!relationship.TryGetProperty("value", out var valueNode) ||
            !TryGetProfileInt(valueNode, out var value))
        {
            return;
        }

        relationship.TryGetProperty("relationshipLock", out var relationshipLock);
        if (value >= 50 && !HasPositiveRelationshipGate(relationship, relationshipLock))
        {
            AddRelationshipIssue(
                $"{context}.value",
                "afterlife_entity_profile_relationship_positive_threshold_missing_gate",
                "Отношение достигло важного положительного порога, но не содержит active breakthrough lock или завершённый breakthrough proof.",
                "positive relationship gate lock/proof",
                value.ToString(CultureInfo.InvariantCulture),
                issues);
        }

        if (value <= -50 && !HasNegativeRelationshipGate(relationship, relationshipLock))
        {
            AddRelationshipIssue(
                $"{context}.value",
                "afterlife_entity_profile_relationship_negative_threshold_missing_gate",
                "Отношение достигло важного отрицательного порога, но не содержит redemption lock, point_of_no_return proof или завершённый redemption proof.",
                "negative relationship gate lock/proof",
                value.ToString(CultureInfo.InvariantCulture),
                issues);
        }
    }

    private static bool HasPositiveRelationshipGate(JsonElement relationship, JsonElement relationshipLock)
    {
        return HasRelationshipLockStateWithQuest(relationshipLock, "positive_locked", "breakthroughQuestId") ||
               HasCompletedRelationshipGateProof(relationship, "breakthrough", "breakthroughQuestId");
    }

    private static bool HasNegativeRelationshipGate(JsonElement relationship, JsonElement relationshipLock)
    {
        return HasRelationshipLockStateWithQuest(relationshipLock, "negative_locked", "redemptionQuestId") ||
               HasValidPointOfNoReturnLock(relationshipLock) ||
               HasCompletedRelationshipGateProof(relationship, "redemption", "redemptionQuestId");
    }

    private static bool HasRelationshipLockStateWithQuest(JsonElement relationshipLock, string lockState, string questField)
    {
        return relationshipLock.ValueKind == JsonValueKind.Object &&
               string.Equals(GetProfileString(relationshipLock, "lockState"), lockState, StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(GetProfileString(relationshipLock, questField));
    }

    private static bool HasValidPointOfNoReturnLock(JsonElement relationshipLock)
    {
        if (relationshipLock.ValueKind != JsonValueKind.Object ||
            !string.Equals(GetProfileString(relationshipLock, "lockState"), "point_of_no_return", StringComparison.OrdinalIgnoreCase) ||
            !relationshipLock.TryGetProperty("pointOfNoReturn", out var pointOfNoReturn) ||
            pointOfNoReturn.ValueKind != JsonValueKind.True)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(GetProfileString(relationshipLock, "proofSummary")) ||
               (relationshipLock.TryGetProperty("proof", out var proof) &&
                proof.ValueKind == JsonValueKind.Object &&
                proof.EnumerateObject().Any());
    }

    private static bool HasCompletedRelationshipGateProof(JsonElement relationship, string questType, string clearField)
    {
        if (!relationship.TryGetProperty(AfterlifeEntityProfileState.RelationshipGateQuestsProperty, out var quests) ||
            quests.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var quest in quests.EnumerateArray())
        {
            if (quest.ValueKind == JsonValueKind.Object &&
                string.Equals(GetProfileString(quest, "questType"), questType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GetProfileString(quest, "status"), "completed", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GetProfileString(quest, clearField), "_clear_", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(GetProfileString(quest, "evidence")))
            {
                return true;
            }
        }

        return false;
    }

    private void ValidateRelationshipGateQuestArray(JsonElement quests, string context, List<ValidationIssue> issues)
    {
        if (quests.ValueKind != JsonValueKind.Array)
        {
            AddRelationshipIssue(context, "afterlife_entity_profile_relationship_gate_quests_not_array", "relationshipGateQuests должен быть array.", "array", quests.ValueKind.ToString(), issues);
            return;
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var quest in quests.EnumerateArray())
        {
            var questContext = $"{context}[{index++}]";
            var questId = ValidateRelationshipGateQuest(quest, questContext, issues);
            if (!string.IsNullOrWhiteSpace(questId) && !ids.Add(questId))
                AddRelationshipIssue($"{questContext}.questId", "afterlife_entity_profile_relationship_duplicate_gate_quest", "relationshipGateQuests не должен содержать дубликаты questId.", "unique questId", questId, issues);
        }
    }

    private string? ValidateRelationshipGateQuest(JsonElement quest, string context, List<ValidationIssue> issues)
    {
        if (quest.ValueKind != JsonValueKind.Object)
        {
            AddRelationshipIssue(context, "afterlife_entity_profile_relationship_gate_quest_not_object", "relationship gate quest должен быть object.", "object", quest.ValueKind.ToString(), issues);
            return null;
        }

        var questId = RequireProfileString(quest, context, "questId", "afterlife_entity_profile_relationship_gate_quest_missing_id", issues);
        var questType = RequireProfileString(quest, context, "questType", "afterlife_entity_profile_relationship_gate_quest_missing_type", issues);
        if (!string.IsNullOrWhiteSpace(questType) && !AfterlifeEntityProfileState.RelationshipGateQuestTypes.Contains(questType))
            AddRelationshipIssue($"{context}.questType", "afterlife_entity_profile_relationship_gate_quest_invalid_type", "questType должен быть breakthrough или redemption.", "breakthrough/redemption", questType, issues);

        var status = RequireProfileString(quest, context, "status", "afterlife_entity_profile_relationship_gate_quest_missing_status", issues);
        if (!string.IsNullOrWhiteSpace(status) && !AfterlifeEntityProfileState.RelationshipGateQuestStatuses.Contains(status))
            AddRelationshipIssue($"{context}.status", "afterlife_entity_profile_relationship_gate_quest_invalid_status", "status relationship gate quest должен быть поддерживаемым lifecycle token.", string.Join("/", AfterlifeEntityProfileState.RelationshipGateQuestStatuses), status, issues);

        RequireProfileString(quest, context, "title", "afterlife_entity_profile_relationship_gate_quest_missing_title", issues);
        RequireProfileString(quest, context, "sceneSummary", "afterlife_entity_profile_relationship_gate_quest_missing_scene", issues);
        RequireProfileString(quest, context, "successCondition", "afterlife_entity_profile_relationship_gate_quest_missing_success_condition", issues);
        RequireProfileString(quest, context, "gmThoughtsSummary", "afterlife_entity_profile_relationship_gate_quest_missing_gm_thoughts", issues);
        ValidateProfileNonNegativeInt(quest, context, "updatedAtTurn", "afterlife_entity_profile_relationship_invalid_turn", issues);

        if (string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            RequireProfileString(quest, context, "evidence", "afterlife_entity_profile_relationship_gate_quest_missing_evidence", issues);
            var clearFieldName = string.Equals(questType, "redemption", StringComparison.OrdinalIgnoreCase)
                ? "redemptionQuestId"
                : "breakthroughQuestId";
            var clearValue = GetProfileString(quest, clearFieldName);
            if (!string.Equals(clearValue, "_clear_", StringComparison.OrdinalIgnoreCase))
            {
                AddRelationshipIssue($"{context}.{clearFieldName}", "afterlife_entity_profile_relationship_clear_requires_clear_keyword", "Завершение breakthrough/redemption gate может очистить linked quest id только значением _clear_.", "_clear_", clearValue ?? "missing", issues);
            }
        }

        return questId;
    }

    private void ValidateRelationshipValue(JsonElement value, string context, List<ValidationIssue> issues)
    {
        if (!TryGetProfileInt(value, out var relationValue) || relationValue is < -100 or > 100)
            AddRelationshipIssue(context, "afterlife_entity_profile_relationship_invalid_value", "value отношения должен быть integer -100..100.", "integer -100..100", value.ToString(), issues);
    }

    private static void AddRelationshipIssue(
        string context,
        string code,
        string message,
        string expected,
        string? actual,
        List<ValidationIssue> issues)
    {
        issues.Add(new ValidationIssue(
            context,
            IssueSeverity.Error,
            message,
            code: code,
            section: "AfterlifeEntityProfiles",
            expected: expected,
            actual: actual));
    }

    private void ValidateAfterlifeActorMaskAddsIfPresent(
        JsonElement adds,
        bool hasAdds,
        string context,
        IReadOnlyDictionary<string, JsonElement> profileAuthority,
        List<ValidationIssue> issues)
    {
        if (!hasAdds)
            return;

        if (adds.ValueKind != JsonValueKind.Array)
        {
            AddMaskIssue(context, "afterlife_entity_profile_mask_adds_not_array", "afterlifeActorMaskAdds должен быть array.", "array", adds.ValueKind.ToString(), issues);
            return;
        }

        var index = 0;
        foreach (var add in adds.EnumerateArray())
        {
            var addContext = $"{context}[{index++}]";
            if (ValidateAfterlifeActorAgencyCommandTarget(add, addContext, profileAuthority, "mask_add", issues) == null)
                continue;

            if (!add.TryGetProperty("mask", out var mask) || mask.ValueKind != JsonValueKind.Object)
            {
                AddMaskIssue($"{addContext}.mask", "afterlife_entity_profile_mask_add_missing_payload", "afterlifeActorMaskAdds должен содержать mask object.", "mask object", add.TryGetProperty("mask", out var actual) ? actual.ToString() : "missing", issues);
                continue;
            }

            ValidateAfterlifeMaskObject(mask, $"{addContext}.mask", issues, requireFullShape: true);
        }
    }

    private void ValidateAfterlifeActorMaskUpdatesIfPresent(
        JsonElement updates,
        bool hasUpdates,
        string context,
        IReadOnlyDictionary<string, JsonElement> profileAuthority,
        List<ValidationIssue> issues)
    {
        if (!hasUpdates)
            return;

        if (updates.ValueKind != JsonValueKind.Array)
        {
            AddMaskIssue(context, "afterlife_entity_profile_mask_updates_not_array", "afterlifeActorMaskUpdates должен быть array.", "array", updates.ValueKind.ToString(), issues);
            return;
        }

        var index = 0;
        foreach (var update in updates.EnumerateArray())
        {
            var updateContext = $"{context}[{index++}]";
            var profile = ValidateAfterlifeActorAgencyCommandTarget(update, updateContext, profileAuthority, "mask_update", issues);
            if (profile == null)
                continue;

            if (!update.TryGetProperty("maskUpdate", out var maskUpdate) || maskUpdate.ValueKind != JsonValueKind.Object)
            {
                AddMaskIssue($"{updateContext}.maskUpdate", "afterlife_entity_profile_mask_update_missing_payload", "afterlifeActorMaskUpdates должен содержать maskUpdate object.", "maskUpdate object", update.TryGetProperty("maskUpdate", out var actual) ? actual.ToString() : "missing", issues);
                continue;
            }

            var maskId = RequireProfileString(maskUpdate, $"{updateContext}.maskUpdate", "maskId", "afterlife_entity_profile_mask_missing_id", issues);
            ValidateAfterlifeMaskObject(maskUpdate, $"{updateContext}.maskUpdate", issues, requireFullShape: false);
            if (!string.IsNullOrWhiteSpace(maskId) && !ProfileHasMask(profile.Value, maskId))
            {
                AddMaskIssue($"{updateContext}.maskUpdate.maskId", "afterlife_entity_profile_mask_update_unknown_id", "afterlifeActorMaskUpdates.maskUpdate.maskId должен ссылаться на существующую masks[] целевого профиля.", "existing masks[].maskId", maskId, issues);
            }
        }
    }

    private void ValidateAfterlifeActorMaskRemovalsIfPresent(
        JsonElement removals,
        bool hasRemovals,
        string context,
        IReadOnlyDictionary<string, JsonElement> profileAuthority,
        List<ValidationIssue> issues)
    {
        if (!hasRemovals)
            return;

        if (removals.ValueKind != JsonValueKind.Array)
        {
            AddMaskIssue(context, "afterlife_entity_profile_mask_removals_not_array", "afterlifeActorMaskRemovals должен быть array.", "array", removals.ValueKind.ToString(), issues);
            return;
        }

        var index = 0;
        foreach (var removal in removals.EnumerateArray())
        {
            var removalContext = $"{context}[{index++}]";
            var profile = ValidateAfterlifeActorAgencyCommandTarget(removal, removalContext, profileAuthority, "mask_removal", issues);
            if (profile == null)
                continue;

            var maskId = RequireProfileString(removal, removalContext, "maskId", "afterlife_entity_profile_mask_missing_id", issues);
            if (!string.IsNullOrWhiteSpace(maskId) && !ProfileHasMask(profile.Value, maskId))
            {
                AddMaskIssue($"{removalContext}.maskId", "afterlife_entity_profile_mask_removal_unknown_id", "afterlifeActorMaskRemovals.maskId должен ссылаться на существующую masks[] целевого профиля.", "existing masks[].maskId", maskId, issues);
            }

            var activeMaskId = GetProfileString(profile.Value, AfterlifeEntityProfileState.ActiveMaskIdProperty);
            if (!string.IsNullOrWhiteSpace(maskId) &&
                string.Equals(activeMaskId, maskId, StringComparison.OrdinalIgnoreCase) &&
                !CommandSetsTrueSelf(removal))
            {
                AddMaskIssue(
                    $"{removalContext}.{AfterlifeEntityProfileState.ActiveMaskIdProperty}",
                    "afterlife_entity_profile_mask_remove_active_without_true_self",
                    "Активную маску нельзя удалить молча: команда удаления должна явно вернуть сущность к _true_self_.",
                    AfterlifeEntityProfileState.TrueSelfMaskId,
                    removal.ToString(),
                    issues);
            }
        }
    }

    private void ValidateAfterlifeActorActiveMaskChangesIfPresent(
        JsonElement changes,
        bool hasChanges,
        string context,
        IReadOnlyDictionary<string, JsonElement> profileAuthority,
        List<ValidationIssue> issues)
    {
        if (!hasChanges)
            return;

        if (changes.ValueKind != JsonValueKind.Array)
        {
            AddMaskIssue(context, "afterlife_entity_profile_active_mask_changes_not_array", "afterlifeActorActiveMaskChanges должен быть array.", "array", changes.ValueKind.ToString(), issues);
            return;
        }

        var index = 0;
        foreach (var change in changes.EnumerateArray())
        {
            var changeContext = $"{context}[{index++}]";
            var profile = ValidateAfterlifeActorAgencyCommandTarget(change, changeContext, profileAuthority, "active_mask_change", issues);
            if (profile == null)
                continue;

            var activeMaskId = ReadActiveMaskChangeId(change);
            if (string.IsNullOrWhiteSpace(activeMaskId))
            {
                AddMaskIssue(
                    $"{changeContext}.{AfterlifeEntityProfileState.ActiveMaskIdProperty}",
                    "afterlife_entity_profile_mask_active_requires_true_self",
                    "afterlifeActorActiveMaskChanges должен задавать activeMaskId; для снятия маски используется _true_self_, а не null.",
                    $"{AfterlifeEntityProfileState.TrueSelfMaskId} or existing masks[].maskId",
                    change.TryGetProperty(AfterlifeEntityProfileState.ActiveMaskIdProperty, out var actual) ? actual.ToString() : "missing",
                    issues);
                continue;
            }

            if (!string.Equals(activeMaskId, AfterlifeEntityProfileState.TrueSelfMaskId, StringComparison.OrdinalIgnoreCase) &&
                !ProfileHasMask(profile.Value, activeMaskId))
            {
                AddMaskIssue(
                    $"{changeContext}.{AfterlifeEntityProfileState.ActiveMaskIdProperty}",
                    "afterlife_entity_profile_mask_active_unknown_id",
                    "activeMaskId должен быть _true_self_ или ссылаться на существующую masks[].maskId целевого профиля.",
                    $"{AfterlifeEntityProfileState.TrueSelfMaskId} or existing masks[].maskId",
                    activeMaskId,
                    issues);
            }

            RequireProfileString(change, changeContext, "reason", "afterlife_entity_profile_mask_active_missing_reason", issues);
            RequireProfileString(change, changeContext, "evidence", "afterlife_entity_profile_mask_active_missing_evidence", issues);
            RequireProfileString(change, changeContext, "gmThoughtsSummary", "afterlife_entity_profile_mask_active_missing_gm_thoughts", issues);
            ValidateProfileNonNegativeInt(change, changeContext, "updatedAtTurn", "afterlife_entity_profile_mask_invalid_turn", issues);
        }
    }

    private void ValidateAfterlifeProfileMasks(JsonElement profile, string context, List<ValidationIssue> issues)
    {
        var maskIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (profile.TryGetProperty(AfterlifeEntityProfileState.MasksProperty, out var masks))
        {
            if (masks.ValueKind != JsonValueKind.Array)
            {
                AddMaskIssue($"{context}.{AfterlifeEntityProfileState.MasksProperty}", "afterlife_entity_profile_masks_not_array", "masks профиля сущности посмертия должен быть array.", "array", masks.ValueKind.ToString(), issues);
            }
            else
            {
                var index = 0;
                foreach (var mask in masks.EnumerateArray())
                {
                    var maskContext = $"{context}.{AfterlifeEntityProfileState.MasksProperty}[{index++}]";
                    var maskId = ValidateAfterlifeMaskObject(mask, maskContext, issues, requireFullShape: true);
                    if (!string.IsNullOrWhiteSpace(maskId) && !maskIds.Add(maskId))
                    {
                        AddMaskIssue($"{maskContext}.maskId", "afterlife_entity_profile_mask_duplicate_id", "masks[] не должен содержать дубликаты maskId.", "unique maskId", maskId, issues);
                    }
                }
            }
        }

        if (!profile.TryGetProperty(AfterlifeEntityProfileState.ActiveMaskIdProperty, out var activeMaskIdNode))
            return;

        var activeMaskId = activeMaskIdNode.ValueKind == JsonValueKind.String
            ? activeMaskIdNode.GetString()?.Trim()
            : null;
        if (string.IsNullOrWhiteSpace(activeMaskId))
        {
            AddMaskIssue(
                $"{context}.{AfterlifeEntityProfileState.ActiveMaskIdProperty}",
                "afterlife_entity_profile_mask_active_requires_true_self",
                "activeMaskId не может быть null/empty: для истинной личности используется _true_self_.",
                $"{AfterlifeEntityProfileState.TrueSelfMaskId} or existing masks[].maskId",
                activeMaskIdNode.ToString(),
                issues);
            return;
        }

        if (!string.Equals(activeMaskId, AfterlifeEntityProfileState.TrueSelfMaskId, StringComparison.OrdinalIgnoreCase) &&
            !maskIds.Contains(activeMaskId))
        {
            AddMaskIssue(
                $"{context}.{AfterlifeEntityProfileState.ActiveMaskIdProperty}",
                "afterlife_entity_profile_mask_active_unknown_id",
                "activeMaskId должен быть _true_self_ или ссылаться на существующую masks[].maskId профиля.",
                $"{AfterlifeEntityProfileState.TrueSelfMaskId} or existing masks[].maskId",
                activeMaskId,
                issues);
        }
    }

    private string? ValidateAfterlifeMaskObject(
        JsonElement mask,
        string context,
        List<ValidationIssue> issues,
        bool requireFullShape)
    {
        if (mask.ValueKind != JsonValueKind.Object)
        {
            AddMaskIssue(context, "afterlife_entity_profile_mask_not_object", "Маска духовной сущности должна быть object.", "object", mask.ValueKind.ToString(), issues);
            return null;
        }

        var maskId = RequireProfileString(mask, context, "maskId", "afterlife_entity_profile_mask_missing_id", issues);
        ValidateMaskStringField(mask, context, "displayName", "afterlife_entity_profile_mask_missing_display_name", requireFullShape, issues);
        ValidateMaskStringField(mask, context, "publicArchetype", "afterlife_entity_profile_mask_missing_public_archetype", requireFullShape, issues);
        ValidateMaskStringField(mask, context, "visiblePersonality", "afterlife_entity_profile_mask_missing_visible_personality", requireFullShape, issues);
        ValidateMaskStringField(mask, context, "concealedTruth", "afterlife_entity_profile_mask_missing_concealed_truth", requireFullShape, issues);

        var deceptionRisk = ValidateMaskStringField(mask, context, "deceptionRisk", "afterlife_entity_profile_mask_missing_deception_risk", requireFullShape, issues);
        if (!string.IsNullOrWhiteSpace(deceptionRisk) && !AfterlifeMaskDeceptionRiskLevels.Contains(deceptionRisk))
        {
            AddMaskIssue(
                $"{context}.deceptionRisk",
                "afterlife_entity_profile_mask_invalid_deception_risk",
                "deceptionRisk маски должен быть поддерживаемым уровнем риска обмана.",
                string.Join("/", AfterlifeMaskDeceptionRiskLevels.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                deceptionRisk,
                issues);
        }

        ValidateMaskStringArray(mask, context, "directives", "afterlife_entity_profile_mask_missing_directives", requireFullShape, issues);
        ValidateMaskStringArray(mask, context, "revealConditions", "afterlife_entity_profile_mask_missing_reveal_conditions", requireFullShape, issues);
        ValidateOptionalMaskString(mask, context, "linkedThreatId", issues);
        ValidateOptionalMaskString(mask, context, "linkedSarefAgentId", issues);
        ValidateOptionalMaskBoolean(mask, context, "isRevealed", issues);
        ValidateProfileNonNegativeIntIfPresent(mask, context, "updatedAtTurn", "afterlife_entity_profile_mask_invalid_turn", issues);
        return maskId;
    }

    private static string? ValidateMaskStringField(
        JsonElement root,
        string context,
        string propertyName,
        string missingCode,
        bool required,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            if (required)
                AddMaskIssue($"{context}.{propertyName}", missingCode, $"{propertyName} маски должен быть non-empty string.", "non-empty string", "missing", issues);
            return null;
        }

        if (property.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(property.GetString()))
            return property.GetString()!.Trim();

        AddMaskIssue($"{context}.{propertyName}", missingCode, $"{propertyName} маски должен быть non-empty string.", "non-empty string", property.ToString(), issues);
        return null;
    }

    private static void ValidateMaskStringArray(
        JsonElement root,
        string context,
        string propertyName,
        string missingCode,
        bool required,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(propertyName, out var array))
        {
            if (required)
                AddMaskIssue($"{context}.{propertyName}", missingCode, $"{propertyName} маски должен быть non-empty string array.", "non-empty string array", "missing", issues);
            return;
        }

        if (array.ValueKind != JsonValueKind.Array)
        {
            AddMaskIssue($"{context}.{propertyName}", missingCode, $"{propertyName} маски должен быть array.", "array", array.ValueKind.ToString(), issues);
            return;
        }

        var hasEntry = false;
        var index = 0;
        foreach (var entry in array.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(entry.GetString()))
            {
                hasEntry = true;
            }
            else
            {
                AddMaskIssue($"{context}.{propertyName}[{index}]", "afterlife_entity_profile_mask_invalid_string_array_entry", $"{propertyName} маски должен содержать только non-empty string entries.", "non-empty string", entry.ToString(), issues);
            }

            index++;
        }

        if (!hasEntry)
            AddMaskIssue($"{context}.{propertyName}", missingCode, $"{propertyName} маски должен содержать хотя бы одну строку.", "non-empty string array", array.ToString(), issues);
    }

    private static void ValidateOptionalMaskString(JsonElement root, string context, string propertyName, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(propertyName, out var property))
            return;

        if (property.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(property.GetString()))
            return;

        AddMaskIssue($"{context}.{propertyName}", "afterlife_entity_profile_mask_invalid_optional_string", $"{propertyName} маски должен быть non-empty string, если указан.", "non-empty string", property.ToString(), issues);
    }

    private static void ValidateOptionalMaskBoolean(JsonElement root, string context, string propertyName, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(propertyName, out var property))
            return;

        if (property.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return;

        AddMaskIssue($"{context}.{propertyName}", "afterlife_entity_profile_mask_invalid_boolean", $"{propertyName} маски должен быть boolean, если указан.", "boolean", property.ToString(), issues);
    }

    private static bool ProfileHasMask(JsonElement profile, string maskId)
    {
        if (profile.ValueKind != JsonValueKind.Object ||
            !profile.TryGetProperty(AfterlifeEntityProfileState.MasksProperty, out var masks) ||
            masks.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return masks.EnumerateArray().Any(mask =>
            mask.ValueKind == JsonValueKind.Object &&
            string.Equals(GetProfileString(mask, "maskId"), maskId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool CommandSetsTrueSelf(JsonElement command)
    {
        var activeMaskId = GetProfileString(command, AfterlifeEntityProfileState.ActiveMaskIdProperty) ??
                           GetProfileString(command, "newActiveMaskId");
        return string.Equals(activeMaskId, AfterlifeEntityProfileState.TrueSelfMaskId, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadActiveMaskChangeId(JsonElement change) =>
        GetProfileString(change, AfterlifeEntityProfileState.ActiveMaskIdProperty) ??
        GetProfileString(change, "newActiveMaskId");

    private static void AddMaskIssue(
        string context,
        string code,
        string message,
        string expected,
        string? actual,
        List<ValidationIssue> issues)
    {
        issues.Add(new ValidationIssue(
            context,
            IssueSeverity.Error,
            message,
            code: code,
            section: "AfterlifeEntityProfiles",
            expected: expected,
            actual: actual));
    }

    private void ValidateAfterlifeProfileAgency(JsonElement profile, string context, List<ValidationIssue> issues)
    {
        string? goalId = null;
        if (profile.TryGetProperty("goals", out var goals))
        {
            if (goals.ValueKind == JsonValueKind.Null)
            {
                // Canonical empty agency keeps the governed surface physically present as null.
            }
            else if (goals.ValueKind != JsonValueKind.Object)
            {
                AddAgencyIssue($"{context}.goals", "afterlife_entity_profile_agency_goals_not_object", "goals профиля духовной сущности должен быть object.", "object", goals.ValueKind.ToString(), issues);
            }
            else
            {
                goalId = RequireProfileString(goals, $"{context}.goals", "goalId", "afterlife_entity_profile_agency_goal_missing_goal_id", issues);
                RequireProfileString(goals, $"{context}.goals", "shortTermGoal", "afterlife_entity_profile_agency_goal_missing_short_term_goal", issues);
                RequireProfileString(goals, $"{context}.goals", "longTermGoal", "afterlife_entity_profile_agency_goal_missing_long_term_goal", issues);
                RequireProfileString(goals, $"{context}.goals", "plan", "afterlife_entity_profile_agency_goal_missing_plan", issues);
                RequireProfileString(goals, $"{context}.goals", "gmThoughtsSummary", "afterlife_entity_profile_agency_goal_missing_gm_thoughts", issues);
                ValidateProfileNonNegativeInt(goals, $"{context}.goals", "updatedAtTurn", "afterlife_entity_profile_agency_goal_invalid_turn", issues);
            }
        }

        var activeQuestLinks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (profile.TryGetProperty("personalQuests", out var quests))
        {
            if (quests.ValueKind != JsonValueKind.Array)
                AddAgencyIssue($"{context}.personalQuests", "afterlife_entity_profile_agency_quests_not_array", "personalQuests профиля духовной сущности должен быть array.", "array", quests.ValueKind.ToString(), issues);
            else
                ValidateAfterlifeActorPersonalQuests(quests, $"{context}.personalQuests", goalId, activeQuestLinks, issues);
        }

        if (profile.TryGetProperty("currentActivity", out var activity) && activity.ValueKind != JsonValueKind.Null)
            ValidateAfterlifeActorCurrentActivity(activity, $"{context}.currentActivity", goalId, activeQuestLinks, issues);

        if (profile.TryGetProperty("completedActivities", out var completedActivities))
            ValidateAfterlifeActorCompletedActivities(completedActivities, $"{context}.completedActivities", issues);
    }

    private void ValidateAfterlifeActorPersonalQuests(
        JsonElement quests,
        string context,
        string? currentGoalId,
        HashSet<string> activeQuestLinks,
        List<ValidationIssue> issues)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var quest in quests.EnumerateArray())
        {
            var questContext = $"{context}[{index++}]";
            if (quest.ValueKind != JsonValueKind.Object)
            {
                AddAgencyIssue(questContext, "afterlife_entity_profile_agency_quest_not_object", "personalQuests entry должен быть object.", "object", quest.ValueKind.ToString(), issues);
                continue;
            }

            var questId = RequireProfileString(quest, questContext, "questId", "afterlife_entity_profile_agency_quest_missing_quest_id", issues);
            var goalId = RequireProfileString(quest, questContext, "goalId", "afterlife_entity_profile_agency_quest_missing_goal_id", issues);
            RequireProfileString(quest, questContext, "title", "afterlife_entity_profile_agency_quest_missing_title", issues);
            var status = RequireProfileString(quest, questContext, "status", "afterlife_entity_profile_agency_quest_missing_status", issues);
            RequireProfileString(quest, questContext, "planSummary", "afterlife_entity_profile_agency_quest_missing_plan_summary", issues);
            RequireProfileString(quest, questContext, "successCondition", "afterlife_entity_profile_agency_quest_missing_success_condition", issues);
            ValidateProfileNonNegativeInt(quest, questContext, "createdAtTurn", "afterlife_entity_profile_agency_quest_invalid_turn", issues);

            if (!string.IsNullOrWhiteSpace(questId) && !ids.Add(questId))
                AddAgencyIssue($"{questContext}.questId", "afterlife_entity_profile_agency_duplicate_quest", "personalQuests не должен содержать дубликаты questId.", "unique questId", questId, issues);

            ValidateActorQuestStatus(status, $"{questContext}.status", issues);
            if (!string.IsNullOrWhiteSpace(goalId) &&
                !string.IsNullOrWhiteSpace(currentGoalId) &&
                !string.Equals(goalId, currentGoalId, StringComparison.OrdinalIgnoreCase))
            {
                AddAgencyIssue($"{questContext}.goalId", "afterlife_entity_profile_agency_quest_goal_mismatch", "personalQuests.goalId должен ссылаться на текущий goals.goalId сущности.", currentGoalId, goalId, issues);
            }

            if (!string.IsNullOrWhiteSpace(questId) &&
                !string.IsNullOrWhiteSpace(goalId) &&
                string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
            {
                activeQuestLinks.Add(BuildAfterlifeActorQuestLink(goalId, questId));
            }
        }
    }

    private void ValidateAfterlifeActorCurrentActivity(
        JsonElement activity,
        string context,
        string? currentGoalId,
        HashSet<string> activeQuestLinks,
        List<ValidationIssue> issues)
    {
        if (activity.ValueKind != JsonValueKind.Object)
        {
            AddAgencyIssue(context, "afterlife_entity_profile_agency_activity_not_object", "currentActivity профиля духовной сущности должен быть object или отсутствовать.", "object", activity.ValueKind.ToString(), issues);
            return;
        }

        RequireProfileString(activity, context, "activityId", "afterlife_entity_profile_agency_activity_missing_activity_id", issues);
        var goalId = RequireProfileString(activity, context, "goalId", "afterlife_entity_profile_agency_activity_missing_goal_id", issues);
        var linkedQuestId = RequireProfileString(activity, context, "linkedQuestId", "afterlife_entity_profile_agency_activity_missing_linked_quest_id", issues);
        RequireProfileString(activity, context, "activityType", "afterlife_entity_profile_agency_activity_missing_type", issues);
        RequireProfileString(activity, context, "summary", "afterlife_entity_profile_agency_activity_missing_summary", issues);
        var status = RequireProfileString(activity, context, "status", "afterlife_entity_profile_agency_activity_missing_status", issues);
        RequireProfileString(activity, context, "gmThoughtsSummary", "afterlife_entity_profile_agency_activity_missing_gm_thoughts", issues);
        ValidateProfileNonNegativeInt(activity, context, "startedAtTurn", "afterlife_entity_profile_agency_activity_invalid_turn", issues);
        ValidateActorActivityStatus(status, $"{context}.status", issues);

        if (string.IsNullOrWhiteSpace(goalId) || string.IsNullOrWhiteSpace(linkedQuestId))
            return;

        var matchesCurrentGoal = !string.IsNullOrWhiteSpace(currentGoalId) &&
                                 string.Equals(goalId, currentGoalId, StringComparison.OrdinalIgnoreCase);
        var matchesActiveQuest = activeQuestLinks.Contains(BuildAfterlifeActorQuestLink(goalId, linkedQuestId));
        if (!matchesCurrentGoal || !matchesActiveQuest)
        {
            AddAgencyIssue(
                $"{context}.linkedQuestId",
                "afterlife_entity_profile_agency_activity_missing_quest_link",
                "currentActivity должна ссылаться на текущую цель сущности и активный personalQuests[] этой же цели.",
                "goalId == goals.goalId and linkedQuestId points to active personalQuests[].questId",
                $"{goalId}:{linkedQuestId}",
                issues);
        }
    }

    private void ValidateAfterlifeActorCompletedActivities(JsonElement completedActivities, string context, List<ValidationIssue> issues)
    {
        if (completedActivities.ValueKind != JsonValueKind.Array)
        {
            AddAgencyIssue(context, "afterlife_entity_profile_agency_completed_activities_not_array", "completedActivities профиля духовной сущности должен быть array.", "array", completedActivities.ValueKind.ToString(), issues);
            return;
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var activity in completedActivities.EnumerateArray())
        {
            var activityContext = $"{context}[{index++}]";
            if (activity.ValueKind != JsonValueKind.Object)
            {
                AddAgencyIssue(activityContext, "afterlife_entity_profile_agency_completed_activity_not_object", "completedActivities entry должен быть object.", "object", activity.ValueKind.ToString(), issues);
                continue;
            }

            var activityId = RequireProfileString(activity, activityContext, "activityId", "afterlife_entity_profile_agency_activity_completion_missing_activity_id", issues);
            RequireProfileString(activity, activityContext, "goalId", "afterlife_entity_profile_agency_activity_missing_goal_id", issues);
            RequireProfileString(activity, activityContext, "linkedQuestId", "afterlife_entity_profile_agency_activity_missing_linked_quest_id", issues);
            var outcome = RequireProfileString(activity, activityContext, "outcome", "afterlife_entity_profile_agency_activity_completion_missing_outcome", issues);
            RequireProfileString(activity, activityContext, "completionSummary", "afterlife_entity_profile_agency_activity_completion_missing_summary", issues);
            ValidateProfileNonNegativeInt(activity, activityContext, "completedAtTurn", "afterlife_entity_profile_agency_activity_completion_invalid_turn", issues);

            if (!string.IsNullOrWhiteSpace(activityId) && !ids.Add(activityId))
                AddAgencyIssue($"{activityContext}.activityId", "afterlife_entity_profile_agency_duplicate_completed_activity", "completedActivities не должен содержать дубликаты activityId.", "unique activityId", activityId, issues);

            ValidateActorActivityOutcome(outcome, $"{activityContext}.outcome", issues);
        }
    }

    private JsonElement? ValidateAfterlifeActorAgencyCommandTarget(
        JsonElement command,
        string context,
        IReadOnlyDictionary<string, JsonElement> profileAuthority,
        string commandKind,
        List<ValidationIssue> issues)
    {
        if (command.ValueKind != JsonValueKind.Object)
        {
            AddAgencyIssue(context, $"afterlife_entity_profile_agency_{commandKind}_not_object", "Команда целей/квестов/активности духовной сущности должна быть object.", "object", command.ValueKind.ToString(), issues);
            return null;
        }

        var actorType = RequireProfileString(command, context, "actorType", $"afterlife_entity_profile_agency_{commandKind}_missing_actor_type", issues);
        if (!string.IsNullOrWhiteSpace(actorType) && !AfterlifeEntityProfileState.ActorTypes.Contains(actorType))
        {
            AddAgencyIssue(
                $"{context}.actorType",
                $"afterlife_entity_profile_agency_{commandKind}_invalid_actor_type",
                "actorType команды целей/квестов/активности должен ссылаться на сущность посмертия.",
                string.Join("/", AfterlifeEntityProfileState.ActorTypes.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                actorType,
                issues);
        }

        if (string.IsNullOrWhiteSpace(GetProfileString(command, "actorId")) &&
            string.IsNullOrWhiteSpace(GetProfileString(command, "actorRef")))
        {
            AddAgencyIssue($"{context}.actorId", $"afterlife_entity_profile_agency_{commandKind}_missing_actor_id", "Команда целей/квестов/активности должна иметь actorId или actorRef target profile.", "non-empty actorId or actorRef", "missing", issues);
        }

        var targetKey = BuildAfterlifeEntityProfileIdentityKey(command);
        if (string.IsNullOrWhiteSpace(targetKey) || !profileAuthority.TryGetValue(targetKey, out var profile))
        {
            AddAgencyIssue(
                context,
                $"afterlife_entity_profile_agency_{commandKind}_unknown_target",
                "Команда целей/квестов/активности должна ссылаться на существующий профиль духовной сущности.",
                "actorType + actorId/actorRef present in profiles[] or afterlifeEntityProfileUpdates[]",
                targetKey ?? "missing target",
                issues);
            return null;
        }

        return profile;
    }

    private static void ValidateActorQuestStatus(string? status, string context, List<ValidationIssue> issues)
    {
        if (!string.IsNullOrWhiteSpace(status) && !AfterlifeActorQuestStatuses.Contains(status))
        {
            AddAgencyIssue(context, "afterlife_entity_profile_agency_quest_invalid_status", "status личного квеста духовной сущности должен быть поддерживаемым lifecycle token.", string.Join("/", AfterlifeActorQuestStatuses.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)), status, issues);
        }
    }

    private static void ValidateActorActivityStatus(string? status, string context, List<ValidationIssue> issues)
    {
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
            AddAgencyIssue(context, "afterlife_entity_profile_agency_activity_invalid_status", "currentActivity должна быть active; завершение идёт через completeAfterlifeActorActivities.", "active", status, issues);
    }

    private static void ValidateActorActivityOutcome(string? outcome, string context, List<ValidationIssue> issues)
    {
        if (!string.IsNullOrWhiteSpace(outcome) && !AfterlifeActorActivityOutcomes.Contains(outcome))
        {
            AddAgencyIssue(context, "afterlife_entity_profile_agency_activity_completion_invalid_outcome", "outcome завершённой активности духовной сущности должен быть поддерживаемым lifecycle token.", string.Join("/", AfterlifeActorActivityOutcomes.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)), outcome, issues);
        }
    }

    private static void AddAgencyIssue(string context, string code, string message, string expected, string actual, List<ValidationIssue> issues)
    {
        issues.Add(new ValidationIssue(
            context,
            IssueSeverity.Error,
            message,
            code: code,
            section: "AfterlifeEntityProfiles",
            expected: expected,
            actual: actual));
    }

    private static string BuildAfterlifeActorQuestLink(string goalId, string questId) =>
        $"{goalId.Trim()}::{questId.Trim()}";

    private void ValidateAfterlifeProfileSpecialArts(
        JsonElement profile,
        string context,
        string? profileActorType,
        string? profileActorId,
        List<ValidationIssue> issues,
        bool requireCurrentSpecialArtCombatEffect = false)
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
            ValidateSpecialArtCombatEffect(
                art,
                artContext,
                issues,
                requireCurrentSpecialArtCombatEffect &&
                art.TryGetProperty("canTeachPlayer", out var teachableNode) &&
                teachableNode.ValueKind == JsonValueKind.True);
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

            var ownerActorId = GetProfileString(art, "ownerActorId");
            if (!string.IsNullOrWhiteSpace(ownerActorType) &&
                !string.IsNullOrWhiteSpace(ownerActorId) &&
                (!string.Equals(ownerActorType, profileActorType, StringComparison.OrdinalIgnoreCase) ||
                 !string.Equals(ownerActorId, profileActorId, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(new ValidationIssue(
                    $"{artContext}.ownerActorId",
                    IssueSeverity.Error,
                    "ownerActorType/ownerActorId особого духовного искусства должны совпадать с профилем-владельцем.",
                    code: "afterlife_entity_profile_special_art_owner_mismatch",
                    section: "AfterlifeEntityProfiles",
                    expected: $"{profileActorType}:{profileActorId}",
                    actual: $"{ownerActorType}:{ownerActorId}"));
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
                    expected: "non-empty object with positive inkFeathers and/or lightSparks cost",
                    actual: "missing"));
            }
            else
            {
                ValidateSpecialArtUpgradeCost(upgradeCost, $"{artContext}.upgradeCost", issues);
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

    private static void ValidateSpecialArtCombatEffect(
        JsonElement art,
        string artContext,
        List<ValidationIssue> issues,
        bool isRequired)
    {
        if (!art.TryGetProperty("combatEffect", out var combatEffect))
        {
            if (isRequired)
            {
                issues.Add(new ValidationIssue(
                    $"{artContext}.combatEffect",
                    IssueSeverity.Error,
                    "Текущее teachable specialArts[] должно содержать structured combatEffect для ordinary afterlife combat payoff.",
                    code: "afterlife_entity_profile_special_art_missing_combat_effect",
                    section: "AfterlifeEntityProfiles",
                    expected: "combatEffect object with summary/trigger/mechanicalAxis/allowedPayoff/limit/auditRequirement",
                    actual: "missing"));
            }

            return;
        }

        if (combatEffect.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                $"{artContext}.combatEffect",
                IssueSeverity.Error,
                "combatEffect особого духовного искусства должен быть object.",
                code: "afterlife_entity_profile_special_art_invalid_combat_effect",
                section: "AfterlifeEntityProfiles",
                expected: "object",
                actual: combatEffect.ValueKind.ToString()));
            return;
        }

        foreach (var field in AfterlifeSpecialArtCombatEffectRequiredFields)
        {
            if (!combatEffect.TryGetProperty(field, out var value) ||
                value.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(value.GetString()))
            {
                issues.Add(new ValidationIssue(
                    $"{artContext}.combatEffect.{field}",
                    IssueSeverity.Error,
                    "combatEffect должен содержать все required player/GM authority fields.",
                    code: "afterlife_entity_profile_special_art_combat_effect_missing_required_field",
                    section: "AfterlifeEntityProfiles",
                    expected: string.Join("/", AfterlifeSpecialArtCombatEffectRequiredFields),
                    actual: combatEffect.TryGetProperty(field, out var actual) ? actual.ToString() : "missing"));
            }
        }

        var summary = GetProfileString(combatEffect, "summary");
        if (!string.IsNullOrWhiteSpace(summary) && IsGenericSpecialArtCombatEffectSummary(summary))
        {
            issues.Add(new ValidationIssue(
                $"{artContext}.combatEffect.summary",
                IssueSeverity.Error,
                "combatEffect.summary должен описывать конкретную боевую нишу, а не generic placeholder.",
                code: "afterlife_entity_profile_special_art_invalid_combat_effect_summary",
                section: "AfterlifeEntityProfiles",
                expected: "specific player-facing ordinary combat niche with trigger/payoff context",
                actual: summary));
        }

        var axis = GetProfileString(combatEffect, "mechanicalAxis");
        if (!string.IsNullOrWhiteSpace(axis) && !AfterlifeSpecialArtCombatEffectAxes.Contains(axis))
        {
            issues.Add(new ValidationIssue(
                $"{artContext}.combatEffect.mechanicalAxis",
                IssueSeverity.Error,
                "combatEffect.mechanicalAxis должен ссылаться только на легальные afterlife combat surfaces.",
                code: "afterlife_entity_profile_special_art_invalid_combat_effect_axis",
                section: "AfterlifeEntityProfiles",
                expected: string.Join("/", AfterlifeSpecialArtCombatEffectAxes.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                actual: axis));
        }

        if (CombatEffectTextLooksLikeBypassOrPowerCreep(combatEffect))
        {
            issues.Add(new ValidationIssue(
                $"{artContext}.combatEffect",
                IssueSeverity.Error,
                "combatEffect не должен обходить baseOperation/tactical matrix, давать безлимитный passive bonus или использовать Mortal HP/status vocabulary.",
                code: "afterlife_entity_profile_special_art_invalid_combat_effect_scope",
                section: "AfterlifeEntityProfiles",
                expected: "bounded payoff inside legal afterlife combat axes and specialArtAudit.effectNote",
                actual: combatEffect.ToString()));
        }
    }

    private static bool IsGenericSpecialArtCombatEffectSummary(string summary)
    {
        var normalized = summary.Trim().ToLowerInvariant();
        if (normalized.Length < 24)
            return true;

        return normalized.Contains("unique effect applies", StringComparison.Ordinal) ||
               normalized.Contains("special effect", StringComparison.Ordinal) ||
               normalized.Contains("combat bonus", StringComparison.Ordinal) ||
               normalized.Contains("особый эффект", StringComparison.Ordinal) ||
               normalized.Contains("уникальный эффект", StringComparison.Ordinal) ||
               normalized.Contains("боевой бонус", StringComparison.Ordinal);
    }

    private static bool CombatEffectTextLooksLikeBypassOrPowerCreep(JsonElement combatEffect)
    {
        var text = string.Join(" ", AfterlifeSpecialArtCombatEffectRequiredFields
            .Select(field => GetProfileString(combatEffect, field))
            .Where(value => !string.IsNullOrWhiteSpace(value)))
            .ToLowerInvariant();

        return text.Contains("bypass", StringComparison.Ordinal) ||
               text.Contains("обходит", StringComparison.Ordinal) ||
               text.Contains("tactical matrix", StringComparison.Ordinal) ||
               text.Contains("безлимит", StringComparison.Ordinal) ||
               text.Contains("unlimited", StringComparison.Ordinal) ||
               text.Contains("always active", StringComparison.Ordinal) ||
               text.Contains("всегда актив", StringComparison.Ordinal) ||
               text.Contains("пассивный безлимит", StringComparison.Ordinal) ||
               text.Contains("ничего не объяснять", StringComparison.Ordinal) ||
               text.Contains("hit point", StringComparison.Ordinal) ||
               text.Contains("hitpoint", StringComparison.Ordinal) ||
               text.Contains(" hp", StringComparison.Ordinal) ||
               text.Contains("здоров", StringComparison.Ordinal) ||
               text.Contains("mortal", StringComparison.Ordinal);
    }

    private void ValidateAfterlifeSpecialArtLearningReceiptsIfPresent(
        JsonElement receipts,
        bool hasReceipts,
        string context,
        IReadOnlyDictionary<string, JsonElement> profileAuthority,
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

            var teacherActorId = RequireProfileString(receipt, receiptContext, "teacherActorId", "afterlife_entity_profile_special_art_learning_missing_teacher_actor_id", issues);
            var artId = RequireProfileString(receipt, receiptContext, "artId", "afterlife_entity_profile_special_art_learning_missing_art_id", issues);
            var playerActorId = RequireProfileString(receipt, receiptContext, "playerActorId", "afterlife_entity_profile_special_art_learning_missing_player_actor_id", issues);
            RequireProfileString(receipt, receiptContext, "roleplayEvidence", "afterlife_entity_profile_special_art_learning_missing_roleplay_evidence", issues);
            RequireProfileString(receipt, receiptContext, "summary", "afterlife_entity_profile_special_art_learning_missing_summary", issues);
            ValidateProfileNonNegativeInt(receipt, receiptContext, "learnedAtTurn", "afterlife_entity_profile_special_art_learning_invalid_turn", issues);
            if (receipt.TryGetProperty("initialTier", out var initialTier) &&
                (!TryGetProfileInt(initialTier, out var resolvedInitialTier) || resolvedInitialTier != 0))
            {
                issues.Add(new ValidationIssue(
                    $"{receiptContext}.initialTier",
                    IssueSeverity.Error,
                    "initialTier в afterlifeSpecialArtLearningReceipts не может прокачивать изученное искусство: новое особое искусство игрока всегда начинается с tier 0.",
                    code: "afterlife_entity_profile_special_art_learning_invalid_initial_tier",
                    section: "AfterlifeEntityProfiles",
                    expected: "missing or 0",
                    actual: initialTier.ToString(),
                    repairHint: "Признай обучение receipt-ом, а последующую прокачку выполняй только клиентской командой /spiritual_arts."));
            }

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

            var teacherKey = BuildAfterlifeEntityProfileIdentityKey(teacherActorType, teacherActorId);
            var playerKey = BuildAfterlifeEntityProfileIdentityKey("player_soul", playerActorId);
            var teacherProfile = ResolveAfterlifeEntityProfileAuthority(teacherKey, profileAuthority);
            var playerProfile = ResolveAfterlifeEntityProfileAuthority(playerKey, profileAuthority);

            if (!string.IsNullOrWhiteSpace(teacherKey) && teacherProfile == null)
            {
                issues.Add(new ValidationIssue(
                    $"{receiptContext}.teacherActorId",
                    IssueSeverity.Error,
                    "afterlifeSpecialArtLearningReceipts должен ссылаться на существующий профиль учителя.",
                    code: "afterlife_entity_profile_special_art_learning_unknown_teacher",
                    section: "AfterlifeEntityProfiles",
                    expected: "teacher actor profile present in profiles[] or afterlifeEntityProfileUpdates[]",
                    actual: teacherKey));
            }

            if (!string.IsNullOrWhiteSpace(playerKey) && playerProfile == null)
            {
                issues.Add(new ValidationIssue(
                    $"{receiptContext}.playerActorId",
                    IssueSeverity.Error,
                    "afterlifeSpecialArtLearningReceipts должен ссылаться на существующий профиль души игрока.",
                    code: "afterlife_entity_profile_special_art_learning_unknown_player",
                    section: "AfterlifeEntityProfiles",
                    expected: "player_soul profile present in profiles[] or afterlifeEntityProfileUpdates[]",
                    actual: playerKey));
            }

            if (teacherProfile != null && !string.IsNullOrWhiteSpace(artId))
            {
                var sourceArt = ResolveAfterlifeEntitySpecialArt(teacherProfile.Value, artId);
                if (sourceArt == null)
                {
                    issues.Add(new ValidationIssue(
                        $"{receiptContext}.artId",
                        IssueSeverity.Error,
                        "afterlifeSpecialArtLearningReceipts.artId должен существовать в specialArts[] профиля учителя.",
                        code: "afterlife_entity_profile_special_art_learning_unknown_art",
                        section: "AfterlifeEntityProfiles",
                        expected: "teacher specialArts[].artId",
                        actual: artId));
                }
                else if (!IsAfterlifeEntitySpecialArtTeachable(sourceArt.Value))
                {
                    issues.Add(new ValidationIssue(
                        $"{receiptContext}.artId",
                        IssueSeverity.Error,
                        "afterlifeSpecialArtLearningReceipts может обучать игрока только искусству с canTeachPlayer=true.",
                        code: "afterlife_entity_profile_special_art_learning_not_teachable",
                        section: "AfterlifeEntityProfiles",
                        expected: "source special art canTeachPlayer=true",
                        actual: artId));
                }
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
            ValidateSignedIntegerObject(currencyDeltas, $"{context}.currencyDeltas", issues, "afterlife_entity_profile_progression_override_invalid_currency_delta", AfterlifeEntityCurrencyDeltaKeys);
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
            ValidateSignedIntegerObject(progressionDeltas, $"{context}.progressionExperienceDeltas", issues, "afterlife_entity_profile_progression_override_invalid_progression_delta", AfterlifeEntityProgressionDeltaKeys);
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

    private static bool IsChaosSeaProfile(JsonElement profile)
    {
        var realm = GetProfileString(profile, "realm");
        return string.Equals(realm, "Chaos Sea", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(realm, "Море Хаоса", StringComparison.OrdinalIgnoreCase);
    }

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

    private static void ValidateSpecialArtUpgradeCost(JsonElement root, string context, List<ValidationIssue> issues)
    {
        const string code = "afterlife_entity_profile_special_art_invalid_upgrade_cost";
        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "upgradeCost особого искусства должен быть object с поддерживаемыми валютами прокачки.",
                code: code,
                section: "AfterlifeEntityProfiles",
                expected: "object with inkFeathers/lightSparks integer costs",
                actual: root.ValueKind.ToString()));
            return;
        }

        var hasAnyProperty = false;
        var hasPositiveSupportedCost = false;
        foreach (var property in root.EnumerateObject())
        {
            hasAnyProperty = true;
            if (!AfterlifeEntityCurrencyDeltaKeys.Contains(property.Name) ||
                !TryGetProfileInt(property.Value, out var value) ||
                value < 0)
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{property.Name}",
                    IssueSeverity.Error,
                    "upgradeCost особого искусства может использовать только inkFeathers/lightSparks с неотрицательными integer values.",
                    code: code,
                    section: "AfterlifeEntityProfiles",
                    expected: "inkFeathers/lightSparks -> non-negative integer",
                    actual: $"{property.Name}: {property.Value}"));
                continue;
            }

            hasPositiveSupportedCost = hasPositiveSupportedCost || value > 0;
        }

        if (!hasAnyProperty || !hasPositiveSupportedCost)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "upgradeCost особого искусства должен содержать хотя бы одну положительную стоимость в inkFeathers или lightSparks.",
                code: code,
                section: "AfterlifeEntityProfiles",
                expected: "inkFeathers > 0 and/or lightSparks > 0",
                actual: root.ToString()));
        }
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
        string code,
        IReadOnlySet<string> allowedKeys)
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

        if (!root.EnumerateObject().Any())
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "Поле должно содержать хотя бы один поддерживаемый integer delta.",
                code: code,
                section: "AfterlifeEntityProfiles",
                expected: string.Join("/", allowedKeys.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)) + " -> integer",
                actual: "{}"));
            return;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (!allowedKeys.Contains(property.Name) || !TryGetProfileInt(property.Value, out _))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{property.Name}",
                    IssueSeverity.Error,
                    "Значение override delta должно использовать поддерживаемый ключ и integer value.",
                    code: code,
                    section: "AfterlifeEntityProfiles",
                    expected: string.Join("/", allowedKeys.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)) + " -> integer",
                    actual: $"{property.Name}: {property.Value}"));
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

        if (!root.EnumerateObject().Any())
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "standardArtTierDeltas должен содержать хотя бы один known standard art delta.",
                code: "afterlife_entity_profile_progression_override_invalid_standard_art_delta",
                section: "AfterlifeEntityProfiles",
                expected: string.Join("/", AfterlifeEntityProfileState.StandardArtIds.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                actual: "{}"));
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

    private static JsonElement? ResolveAfterlifeEntityProfileAuthority(
        string? key,
        IReadOnlyDictionary<string, JsonElement> profileAuthority)
    {
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
        return BuildAfterlifeEntityProfileIdentityKey(actorType, actorId);
    }

    private static string? BuildAfterlifeEntityProfileIdentityKey(string? actorType, string? actorId)
    {
        return string.IsNullOrWhiteSpace(actorType) || string.IsNullOrWhiteSpace(actorId)
            ? null
            : $"{actorType}:{actorId}";
    }

    private static JsonElement? ResolveAfterlifeEntitySpecialArt(JsonElement profile, string artId)
    {
        if (profile.ValueKind != JsonValueKind.Object ||
            !profile.TryGetProperty("specialArts", out var specialArts) ||
            specialArts.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var art in specialArts.EnumerateArray())
        {
            if (art.ValueKind == JsonValueKind.Object &&
                string.Equals(GetProfileString(art, "artId"), artId, StringComparison.OrdinalIgnoreCase))
            {
                return art;
            }
        }

        return null;
    }

    private static bool IsAfterlifeEntitySpecialArtTeachable(JsonElement specialArt)
    {
        return specialArt.ValueKind == JsonValueKind.Object &&
               specialArt.TryGetProperty("canTeachPlayer", out var canTeach) &&
               canTeach.ValueKind == JsonValueKind.True;
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

        if (!root.EnumerateObject().Any())
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "specialArtTierDeltas должен содержать хотя бы один artId delta.",
                code: "afterlife_entity_profile_progression_override_invalid_special_art_delta",
                section: "AfterlifeEntityProfiles",
                expected: "non-empty special art id -> integer -5..5",
                actual: "{}"));
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
