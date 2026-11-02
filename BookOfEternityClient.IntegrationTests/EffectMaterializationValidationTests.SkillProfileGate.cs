using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectMaterializationValidationTests
{
    /// <summary>
    /// T101 / FR-107: the composition (constructor) path must validate the profile of
    /// every skill object this turn's accepted changes write. A brand-new active skill
    /// without combatEffect currently passes composition silently.
    /// </summary>
    [Fact]
    public async Task RawValidation_SkillCompositionRejectsActiveSkillMissingCombatEffect()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string path = "game_state/player/skills_active.json";
        await context.WriteJsonAsync(
            path,
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(),
                ["removeActiveSkills"] = new JsonArray()
            });
        await context.CaptureValidatedPendingSnapshotAsync();
        var invalid = CreateValidActiveSkill("skill_no_combat_effect", "Безликий выпад");
        invalid.Remove("combatEffect");
        await context.WriteJsonAsync(
            path,
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(invalid),
                ["removeActiveSkills"] = new JsonArray()
            });

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_skill_composition_invalid_profile");
    }

    /// <summary>
    /// T101 / FR-106/FR-107: rarity is a canonical enum on the shared profile contract;
    /// the composition gate must reject out-of-ladder values for this-turn writes.
    /// </summary>
    [Fact]
    public async Task RawValidation_SkillCompositionRejectsUnknownRarity()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string path = "game_state/player/skills_active.json";
        await context.WriteJsonAsync(
            path,
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(),
                ["removeActiveSkills"] = new JsonArray()
            });
        await context.CaptureValidatedPendingSnapshotAsync();
        var invalid = CreateValidActiveSkill("skill_mythic_rarity", "Мифический выпад");
        invalid["rarity"] = "Mythic";
        await context.WriteJsonAsync(
            path,
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(invalid),
                ["removeActiveSkills"] = new JsonArray()
            });

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Equal(
            1,
            issues.Count(issue => issue.Code == "effect_skill_composition_invalid_profile"));
    }

    /// <summary>
    /// T101 / FR-107: a full-object update IS a this-turn write, so the gate must
    /// validate it — rewriting an existing skill with an out-of-ladder rarity fails
    /// closed instead of silently replacing the stored profile.
    /// </summary>
    [Fact]
    public async Task RawValidation_SkillCompositionRejectsUpdatedSkillUnknownRarity()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string path = "game_state/player/skills_active.json";
        await context.WriteJsonAsync(
            path,
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(
                    CreateValidActiveSkill("skill_update_target", "Старый выпад")),
                ["removeActiveSkills"] = new JsonArray()
            });
        await context.CaptureValidatedPendingSnapshotAsync();

        var update = CreateValidActiveSkill("skill_update_target", "Старый выпад");
        update["rarity"] = "Mythic";
        update["skillDescription"] = "Полное новое состояние навыка.";
        await context.WriteJsonAsync(
            path,
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(update),
                ["removeActiveSkills"] = new JsonArray()
            });

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_skill_composition_invalid_profile");
    }

    /// <summary>
    /// T101 / FR-107 scope guard: a pre-turn skill this turn did not touch is carried
    /// over by composition and must NOT be re-validated by the constructor gate, even
    /// when its stored profile violates the rarity ladder. Positive guard — passes
    /// before and after the gate exists.
    /// </summary>
    [Fact]
    public async Task RawValidation_SkillCompositionIgnoresCarriedOverProfile()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string path = "game_state/player/skills_active.json";
        var legacy = CreateValidActiveSkill("skill_legacy_mythic", "Древний след");
        legacy["rarity"] = "Mythic";
        await context.WriteJsonAsync(
            path,
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(legacy),
                ["removeActiveSkills"] = new JsonArray()
            });
        await context.CaptureValidatedPendingSnapshotAsync();

        // The accepted turn writes only the new valid skill; the legacy one is
        // carried over from the pre-turn root and is outside the gate's scope.
        var validNew = CreateValidActiveSkill("skill_valid_new", "Новый выпад");
        await context.WriteJsonAsync(
            path,
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(validNew),
                ["removeActiveSkills"] = new JsonArray()
            });

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code == "effect_skill_composition_invalid_profile");
    }
}
