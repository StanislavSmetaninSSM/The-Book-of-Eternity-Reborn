using System.Text.Json;

namespace BookOfEternityClient.Services;

/// <summary>
/// Canonical shared skill profile contract (T101 / FR-105..108): the single
/// rarity ladder and passive skill type enum used by every skill validation
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
}
