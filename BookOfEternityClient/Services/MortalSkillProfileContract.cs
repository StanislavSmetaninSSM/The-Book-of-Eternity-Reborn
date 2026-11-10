using System.Text.Json;
using BookOfEternityClient.Configuration;
using static BookOfEternityClient.Services.ValidationService;

namespace BookOfEternityClient.Services;

/// <summary>
/// Canonical shared skill profile contract (T101 / FR-105..108): the single
/// active/passive validation algorithms and enums used by every skill validation
/// surface — player delta path, NPC delta path, NPC core changes contract and
/// the accepted-turn effect composition gate.
/// </summary>
internal static class MortalSkillProfileContract
{
    /// <summary>
    /// Canonical rarity ladder for active and passive skill profiles.
    /// Comparison is ordinal case-insensitive: stored fixtures may author
    /// lowercase values, the ladder itself is the display form.
    /// </summary>
    internal static readonly HashSet<string> AllowedSkillRarities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Common",
        "Uncommon",
        "Rare",
        "Epic",
        "Legendary"
    };

    /// <summary>
    /// Canonical passive skill <c>type</c> enum shared by the player delta
    /// validator and the NPC core changes contract. Passive <c>group</c> stays
    /// a free-form narrative label by recorded Part 3 decision and is not
    /// constrained here.
    /// </summary>
    internal static readonly HashSet<string> AllowedPassiveSkillTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "KnowledgeBased",
        "CharacteristicBonus",
        "BodyModification",
        "CombatEnhancement",
        "Utility"
    };

    /// <summary>
    /// Validates that a skill object's <c>rarity</c> value belongs to the
    /// canonical ladder. Missing, non-string or empty values are left to the
    /// required-field validation of the calling surface; this check only
    /// rejects present non-empty out-of-ladder values.
    /// </summary>
    /// <param name="skill">
    /// The skill object being validated.
    /// </param>
    /// <param name="itemContext">
    /// Diagnostic path prefix of the skill object.
    /// </param>
    /// <param name="section">
    /// Canonical section identifier for the produced issue.
    /// </param>
    /// <param name="issues">
    /// The issue collection that receives the diagnostic.
    /// </param>
    internal static void ValidateSkillRarity(
        JsonElement skill,
        string itemContext,
        string section,
        List<ValidationIssue> issues)
    {
        if (!skill.TryGetProperty("rarity", out var rarityNode) ||
            rarityNode.ValueKind != JsonValueKind.String)
            return;

        var rarity = rarityNode.GetString();
        if (string.IsNullOrWhiteSpace(rarity) || AllowedSkillRarities.Contains(rarity))
            return;

        issues.Add(new ValidationIssue(
            $"{itemContext}.rarity",
            IssueSeverity.Error,
            "Skill rarity должен быть одним из canonical enum значений",
            code: "skill_invalid_rarity",
            section: section,
            expected: string.Join(" | ", AllowedSkillRarities),
            actual: rarity,
            repairHint: "Используй skill rarity только из contract enum: Common, Uncommon, Rare, Epic, Legendary."));
    }

    /// <summary>
    /// Validates one canonical active skill profile object. Shared by the
    /// player delta path, the NPC delta path and the accepted-turn effect
    /// composition gate (T101 / FR-105).
    /// </summary>
    /// <param name="item">
    /// The active skill object being validated.
    /// </param>
    /// <param name="itemContext">
    /// Diagnostic path prefix of the skill object.
    /// </param>
    /// <param name="issues">
    /// The issue collection that receives diagnostics.
    /// </param>
    internal static void ValidateActiveSkillObject(JsonElement item, string itemContext, List<ValidationIssue> issues)
    {
        MortalWoundTreatmentCapabilityContract.ValidateSkillExtension(item, itemContext, issues);
        ValidateActiveEffectDefinitionsIfPresent(
            item,
            itemContext,
            "mortal_world",
            issues);
        RequireString(item, itemContext, issues, "skillName");
        RequireString(item, itemContext, issues, "skillDescription");
        RequireString(item, itemContext, issues, "rarity");
        ValidateSkillRarity(item, itemContext, "Skills.Active", issues);
        ValidateSkillActionCostField(item, itemContext, issues, "actionCost", "Skills.Active");

        if (!item.TryGetProperty("combatEffect", out var combatEffect))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.combatEffect",
                IssueSeverity.Error,
                "Active Skill Object должен содержать combatEffect",
                code: "active_skill_missing_combat_effect",
                section: "Skills.Active",
                expected: "combatEffect object",
                actual: "missing combatEffect",
                repairHint: "Передай полный Active Skill Object с combatEffect object по canonical rules contract."));
        }
        else if (!RequireObject(combatEffect, $"{itemContext}.combatEffect", issues))
        {
            // RequireObject already emitted a root-cause error.
        }
        else
        {
            ValidateCombatActionObject(
                combatEffect,
                $"{itemContext}.combatEffect",
                issues,
                requireActionNameForActivatedEffect: false,
                section: "Skills.Active");

            if (!combatEffect.TryGetProperty("isActivatedEffect", out var isActivatedEffect))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.combatEffect.isActivatedEffect",
                    IssueSeverity.Error,
                    "combatEffect для active skill должен явно указывать isActivatedEffect=true",
                    code: "active_skill_missing_activation_flag",
                    section: "Skills.Active",
                    expected: "true",
                    actual: "missing",
                    repairHint: "Для active skill сохрани canonical combatEffect с isActivatedEffect=true."));
            }
            else if (isActivatedEffect.ValueKind != JsonValueKind.True)
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.combatEffect.isActivatedEffect",
                    IssueSeverity.Error,
                    "combatEffect для active skill должен иметь isActivatedEffect=true",
                    code: "active_skill_invalid_activation_flag",
                    section: "Skills.Active",
                    expected: "true",
                    actual: isActivatedEffect.ValueKind.ToString(),
                    repairHint: "Active skill должен использовать canonical activated combatEffect с isActivatedEffect=true."));
            }
        }

        if (item.TryGetProperty("scalingCharacteristic", out var scalingCharacteristic) &&
            scalingCharacteristic.ValueKind != JsonValueKind.Null)
        {
            if (scalingCharacteristic.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(scalingCharacteristic.GetString()))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.scalingCharacteristic",
                    IssueSeverity.Error,
                    "scalingCharacteristic должен быть непустой строкой или null",
                    code: "active_skill_invalid_scaling_characteristic",
                    section: "Skills.Active",
                    repairHint: "Укажи canonical характеристику строкой или явно передай null, если навык не масштабируется от характеристики."));
            }
            else
            {
                var characteristic = scalingCharacteristic.GetString() ?? string.Empty;
                if (!Characteristics.All.Contains(characteristic, StringComparer.OrdinalIgnoreCase))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.scalingCharacteristic",
                        IssueSeverity.Error,
                        "scalingCharacteristic должен быть одной из canonical характеристик",
                        code: "active_skill_unknown_scaling_characteristic",
                        section: "Skills.Active",
                        expected: string.Join(" | ", Characteristics.All),
                        actual: characteristic,
                        repairHint: "Используй scalingCharacteristic только из canonical списка характеристик игрового контракта."));
                }
            }
        }

        ValidateOptionalBool(item, itemContext, issues, "scalesValue");
        ValidateOptionalBool(item, itemContext, issues, "scalesDuration");
        ValidateOptionalBool(item, itemContext, issues, "scalesChance");
        ValidateOptionalNumberOrString(item, itemContext, issues, "energyCost");
        ValidateOptionalNumberOrString(item, itemContext, issues, "cooldownTurns");
        ValidateOptionalNumberOrString(item, itemContext, issues, "timeCost");
    }

    /// <summary>
    /// Validates one canonical passive skill profile object. Shared by the
    /// player delta path, the NPC delta path and the accepted-turn effect
    /// composition gate (T101 / FR-105).
    /// </summary>
    /// <param name="item">
    /// The passive skill object being validated.
    /// </param>
    /// <param name="itemContext">
    /// Diagnostic path prefix of the skill object.
    /// </param>
    /// <param name="issues">
    /// The issue collection that receives diagnostics.
    /// </param>
    internal static void ValidatePassiveSkillObject(JsonElement item, string itemContext, List<ValidationIssue> issues)
    {
        MortalWoundTreatmentCapabilityContract.ValidateSkillExtension(item, itemContext, issues);
        ValidateActiveEffectDefinitionsIfPresent(
            item,
            itemContext,
            "mortal_world",
            issues);
        RequireString(item, itemContext, issues, "skillName");
        RequireString(item, itemContext, issues, "skillDescription");
        RequireString(item, itemContext, issues, "rarity");
        ValidateSkillRarity(item, itemContext, "Skills.Passive", issues);
        var passiveType = RequireString(item, itemContext, issues, "type");
        RequireString(item, itemContext, issues, "group");
        if (!item.TryGetProperty("masteryLevel", out _))
            issues.Add(new ValidationIssue(
                $"{itemContext}.masteryLevel",
                IssueSeverity.Error,
                "Отсутствует обязательное поле passive skill: masteryLevel",
                code: "passive_skill_missing_mastery_level",
                section: "Skills.Passive",
                expected: "integer masteryLevel",
                actual: "missing",
                repairHint: "Добавь в passive skill canonical поле masteryLevel как целое число."));
        else
            ValidateIntegerField(item, itemContext, issues, "masteryLevel");
        if (!item.TryGetProperty("maxMasteryLevel", out _))
            issues.Add(new ValidationIssue(
                $"{itemContext}.maxMasteryLevel",
                IssueSeverity.Error,
                "Отсутствует обязательное поле passive skill: maxMasteryLevel",
                code: "passive_skill_missing_max_mastery_level",
                section: "Skills.Passive",
                expected: "integer maxMasteryLevel",
                actual: "missing",
                repairHint: "Добавь в passive skill canonical поле maxMasteryLevel как целое число."));
        else
            ValidateIntegerField(item, itemContext, issues, "maxMasteryLevel");

        if (!string.IsNullOrWhiteSpace(passiveType) && !AllowedPassiveSkillTypes.Contains(passiveType))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.type",
                IssueSeverity.Error,
                "Passive skill type должен быть одним из canonical enum значений",
                code: "passive_skill_invalid_type",
                section: "Skills.Passive",
                expected: string.Join(" | ", AllowedPassiveSkillTypes),
                actual: passiveType,
                repairHint: "Используй passive skill type только из contract enum: KnowledgeBased, CharacteristicBonus, BodyModification, CombatEnhancement, Utility."));
        }

        if (!item.TryGetProperty("structuredBonuses", out var structuredBonuses))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.structuredBonuses",
                IssueSeverity.Error,
                "Passive Skill Object должен содержать structuredBonuses как canonical primary bonus source",
                code: "passive_skill_missing_structured_bonuses",
                section: "Skills.Passive",
                expected: "structuredBonuses array or null",
                actual: "missing structuredBonuses",
                repairHint: "Добавь поле structuredBonuses в Passive Skill Object. Для purely narrative skill можешь явно передать null."));
        }
        else if (structuredBonuses.ValueKind != JsonValueKind.Null)
        {
            RequireArrayOfObjects(structuredBonuses, $"{itemContext}.structuredBonuses", issues);
        }

        if (item.TryGetProperty("playerStatBonus", out var playerStatBonus) &&
            playerStatBonus.ValueKind != JsonValueKind.Null &&
            (playerStatBonus.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(playerStatBonus.GetString())))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.playerStatBonus",
                IssueSeverity.Error,
                "playerStatBonus должен быть непустой строкой или null",
                code: "passive_skill_invalid_player_stat_bonus",
                section: "Skills.Passive",
                repairHint: "Если используешь playerStatBonus, передай display-only summary строкой; иначе явно передай null."));
        }

        if (structuredBonuses.ValueKind == JsonValueKind.Array &&
            structuredBonuses.GetArrayLength() > 0 &&
            (!item.TryGetProperty("playerStatBonus", out var playerStatBonusMirror) ||
             playerStatBonusMirror.ValueKind != JsonValueKind.String ||
             string.IsNullOrWhiteSpace(playerStatBonusMirror.GetString())))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.playerStatBonus",
                IssueSeverity.Error,
                "Passive skill с mechanical structuredBonuses должен содержать playerStatBonus summary",
                code: "passive_skill_missing_player_stat_bonus_mirror",
                section: "Skills.Passive",
                expected: "non-empty playerStatBonus when structuredBonuses are present",
                actual: !item.TryGetProperty("playerStatBonus", out _) ? "missing" : "null/empty",
                repairHint: "Если passive skill несёт mechanical structuredBonuses, сохрани рядом непустой playerStatBonus как canonical summary для UI и rules contract."));
        }

        if (item.TryGetProperty("combatEffect", out var combatEffect))
        {
            if (RequireObject(combatEffect, $"{itemContext}.combatEffect", issues))
            {
                ValidateCombatActionObject(
                    combatEffect,
                    $"{itemContext}.combatEffect",
                    issues,
                    requireActionNameForActivatedEffect: false,
                    section: "Skills.Passive");

                if (combatEffect.TryGetProperty("isActivatedEffect", out var isActivatedEffect) &&
                    isActivatedEffect.ValueKind == JsonValueKind.True)
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.combatEffect.isActivatedEffect",
                        IssueSeverity.Error,
                        "Passive skill combatEffect не должен быть activated effect",
                        code: "passive_skill_invalid_activation_flag",
                        section: "Skills.Passive",
                        expected: "false or omitted",
                        actual: "true",
                        repairHint: "Для passive skill оставь isActivatedEffect=false или не указывай его вовсе."));
                }
            }
        }

        ValidateOptionalString(item, itemContext, issues, "effectDetails");
        ValidateOptionalString(item, itemContext, issues, "knowledgeDomain");
        ValidateOptionalNumberOrString(item, itemContext, issues, "unlockedActiveSkillsCount");
        ValidateOptionalNumberOrString(item, itemContext, issues, "maxUnlockableActiveSkills");
    }

    private static void ValidateSkillActionCostField(JsonElement item, string itemContext, List<ValidationIssue> issues, string propName,
        string section)
    {
        if (!item.TryGetProperty(propName, out var actionCostNode))
            return;

        if (actionCostNode.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(actionCostNode.GetString()))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.{propName}",
                IssueSeverity.Error,
                $"{propName} должен быть непустой строкой",
                code: "skill_invalid_action_cost_type",
                section: section));
            return;
        }

        var actionCost = actionCostNode.GetString() ?? string.Empty;
        if (!AllowedCombatActionCosts.Contains(actionCost))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.{propName}",
                IssueSeverity.Error,
                $"{propName} должен быть одним из canonical enum значений",
                code: "skill_invalid_action_cost",
                section: section,
                expected: string.Join(" | ", AllowedCombatActionCosts),
                actual: actionCost,
                repairHint: "Используй actionCost только из canonical skill contract: Main, Fast или Free."));
        }
    }

}
