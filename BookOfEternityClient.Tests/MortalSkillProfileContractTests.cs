using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Direct production-contract regressions for #1533 T101.</summary>
public sealed class MortalSkillProfileContractTests
{
    private const string NpcPath = "game_state/npcs/npc_core.json";
    private const string PlayerPassivePath = "game_state/player/skills_passive.json";

    [Fact]
    public void NpcFateCard_InvalidPassiveType_HasOneCanonicalDiagnostic()
    {
        var baseline = NpcRoot("npc_a");
        var current = baseline.DeepClone().AsObject();
        var skill = Passive();
        skill["type"] = "MegaBoost";
        current["NPCCoreChanges"] = new JsonArray(new JsonObject
        {
            ["NPCId"] = "npc_a", ["reason"] = "A new destiny",
            ["fateCardsToAdd"] = new JsonArray(Card(skill))
        });
        var authority = new NpcCoreChangesContract.Authority(
            new(StringComparer.Ordinal), new(StringComparer.Ordinal),
            new(StringComparer.Ordinal), new(StringComparer.Ordinal));

        var issues = NpcCoreChangesContract.Evaluate(current, baseline, authority,
            ValidationService.ValidateNpcCoreFateCardsAgainstProductionContract,
            detectDirectMutations: true).Issues;

        var issue = Assert.Single(issues.Where(i => i.FilePath.EndsWith(".type", StringComparison.Ordinal)));
        Assert.Equal("passive_skill_invalid_type", issue.Code);
        Assert.Equal("Skills.Passive", issue.Section);
        Assert.Equal("MegaBoost", issue.Actual);
    }

    [Theory]
    [InlineData("NPCsInScene", false)]
    [InlineData("UpdateNPCs", false)]
    [InlineData("NPCsInScene", true)]
    [InlineData("UpdateNPCs", true)]
    public void Composition_NpcNewAndUpdatedActiveProfile_IsGated(string section, bool update)
    {
        var before = NpcRoot("npc_a", update ? Active() : null);
        var bad = Active();
        bad["rarity"] = "Mythic";
        bad.Remove("combatEffect");
        var after = NpcRoot("npc_a", bad, section: section);
        var issues = Gate(before, after);

        Assert.Equal(2, issues.Count);
        Assert.All(issues, i => Assert.Equal("effect_skill_composition_invalid_profile", i.Code));
        Assert.Contains(issues, i => i.FilePath == $"{NpcPath}.{section}[0].activeSkills[0].rarity");
        Assert.Contains(issues, i => i.FilePath == $"{NpcPath}.{section}[0].activeSkills[0].combatEffect");
    }

    [Fact]
    public void Composition_MalformedPassiveBonuses_PreservesExactFieldAndDetails()
    {
        var skill = Passive();
        skill["structuredBonuses"] = "bad";
        using var document = JsonDocument.Parse(skill.ToJsonString());
        var direct = new List<ValidationIssue>();
        var path = PlayerPassivePath + ".passiveSkillChanges[0]";
        ValidationService.ValidatePassiveSkillObject(document.RootElement, path, direct);
        var expected = Assert.Single(direct);

        var issues = EffectAcceptedTurnInputComposer.ValidateAcceptedSkillComposition(
            new Dictionary<string, JsonNode?>(), new Dictionary<string, JsonNode?>
            {
                [PlayerPassivePath] = new JsonObject { ["passiveSkillChanges"] = new JsonArray(skill) }
            });
        var actual = Assert.Single(issues);
        Assert.Equal(path + ".structuredBonuses", actual.FilePath);
        Assert.Equal(expected.Message, actual.Message);
        Assert.Equal(expected.Severity, actual.Severity);
        Assert.Equal(expected.Expected, actual.Expected);
        Assert.Equal(expected.Actual, actual.Actual);
        Assert.Equal(expected.RepairHint, actual.RepairHint);
        Assert.Equal("effect_skill_composition_invalid_profile", actual.Code);
        Assert.Equal("effect_materialization", actual.Section);
    }

    [Theory]
    [InlineData("Common", "KnowledgeBased")]
    [InlineData("Uncommon", "CharacteristicBonus")]
    [InlineData("Rare", "BodyModification")]
    [InlineData("Epic", "CombatEnhancement")]
    [InlineData("Legendary", "Utility")]
    public void Composition_PlayerAndNpcPassiveProfiles_HaveEnumParity(string rarity, string type)
    {
        var skill = Passive();
        skill["rarity"] = rarity;
        skill["type"] = type;
        var roots = new Dictionary<string, JsonNode?>
        {
            [PlayerPassivePath] = new JsonObject { ["passiveSkillChanges"] = new JsonArray(skill.DeepClone()) },
            [NpcPath] = NpcRoot("npc_a", skill, passive: true)
        };
        Assert.Empty(EffectAcceptedTurnInputComposer.ValidateAcceptedSkillComposition(
            new Dictionary<string, JsonNode?>(), roots));
    }

    [Fact]
    public void Composition_UnchangedNpcSkill_OnMovedActorAndOtherActorWrite_IsNotGated()
    {
        var bad = Active();
        bad["rarity"] = "Mythic";
        var before = NpcRoot("npc_a", bad);
        var after = NpcRoot("npc_a", bad.DeepClone().AsObject(), section: "UpdateNPCs");
        after["NPCsInScene"] = NpcRoot("npc_b", Active())["NPCsInScene"]!.DeepClone();
        Assert.Empty(Gate(before, after));
    }

    [Fact]
    public void Composition_SameSkillOnDifferentOwner_DoesNotCountAsCarryOver()
    {
        var bad = Active();
        bad["rarity"] = "Mythic";
        Assert.Single(Gate(NpcRoot("npc_a", bad), NpcRoot("npc_b", bad.DeepClone().AsObject())));
    }

    [Fact]
    public void Composition_NpcPassiveInvalidTypeAndBonuses_ArePrecise()
    {
        var bad = Passive();
        bad["type"] = "MegaBoost";
        bad["structuredBonuses"] = "bad";
        var issues = Gate(NpcRoot("npc_a"), NpcRoot("npc_a", bad, passive: true));
        Assert.Equal(2, issues.Count);
        Assert.Contains(issues, i => i.FilePath.EndsWith(".passiveSkills[0].type", StringComparison.Ordinal));
        Assert.Contains(issues, i => i.FilePath.EndsWith(".passiveSkills[0].structuredBonuses", StringComparison.Ordinal));
    }

    [Fact]
    public void Composition_NpcCoreFateCardRewardWrite_IsGated()
    {
        var after = NpcRoot("npc_a");
        var bad = Passive();
        bad["type"] = "MegaBoost";
        after["NPCCoreChanges"] = new JsonArray(new JsonObject
        {
            ["NPCId"] = "npc_a", ["reason"] = "A new destiny",
            ["fateCardsToAdd"] = new JsonArray(Card(bad))
        });
        var issue = Assert.Single(Gate(NpcRoot("npc_a"), after));
        Assert.Equal(NpcPath + ".NPCCoreChanges[0].fateCardsToAdd[0].rewards.newPassiveSkills[0].type", issue.FilePath);
    }

    [Fact]
    public void PassiveMissingMastery_DiagnosticFieldsStayByteCompatible()
    {
        var skill = Passive();
        skill.Remove("masteryLevel");
        using var document = JsonDocument.Parse(skill.ToJsonString());
        var issues = new List<ValidationIssue>();
        ValidationService.ValidatePassiveSkillObject(document.RootElement, "profile", issues);
        var issue = Assert.Single(issues);
        Assert.Equal("profile.masteryLevel", issue.FilePath);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Equal("Отсутствует обязательное поле passive skill: masteryLevel", issue.Message);
        Assert.Equal("passive_skill_missing_mastery_level", issue.Code);
        Assert.Equal("Skills.Passive", issue.Section);
        Assert.Equal("integer masteryLevel", issue.Expected);
        Assert.Equal("missing", issue.Actual);
        Assert.Equal("Добавь в passive skill canonical поле masteryLevel как целое число.", issue.RepairHint);
    }

    private static IReadOnlyList<ValidationIssue> Gate(JsonObject before, JsonObject after) =>
        EffectAcceptedTurnInputComposer.ValidateAcceptedSkillComposition(
            new Dictionary<string, JsonNode?> { [NpcPath] = before },
            new Dictionary<string, JsonNode?> { [NpcPath] = after });

    private static JsonObject NpcRoot(string npcId, JsonObject? skill = null,
        bool passive = false, string section = "NPCsInScene") => new()
    {
        [section] = new JsonArray(new JsonObject
        {
            ["NPCId"] = npcId,
            [passive ? "passiveSkills" : "activeSkills"] = skill == null ? new JsonArray() : new JsonArray(skill)
        })
    };

    private static JsonObject Passive() => new()
    {
        ["skillName"] = "Archive", ["skillDescription"] = "Skill", ["rarity"] = "Common",
        ["type"] = "KnowledgeBased", ["group"] = "Архивное дело", ["masteryLevel"] = 1,
        ["maxMasteryLevel"] = 5, ["structuredBonuses"] = null
    };

    private static JsonObject Active() => new()
    {
        ["skillId"] = "skill_a", ["skillName"] = "Strike", ["skillDescription"] = "Skill", ["rarity"] = "Common",
        ["combatEffect"] = new JsonObject
        {
            ["isActivatedEffect"] = true, ["actionName"] = "Strike", ["actionCost"] = "Main",
            ["effects"] = new JsonArray(new JsonObject
            {
                ["effectType"] = "Damage", ["targetType"] = "Enemy", ["effectDescription"] = "Hit",
                ["value"] = "10%", ["poiseDamage"] = "5%"
            })
        }
    };

    private static JsonObject Card(JsonObject skill) => new()
    {
        ["cardId"] = "new-card", ["name"] = "Card", ["image_prompt"] = "An archivist",
        ["description"] = "Story", ["isUnlocked"] = false,
        ["rewards"] = new JsonObject { ["description"] = "Reward", ["newPassiveSkills"] = new JsonArray(skill) }
    };
}
