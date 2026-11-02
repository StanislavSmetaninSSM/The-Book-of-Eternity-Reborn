using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// T101 / FR-105..108: canonical skill profile schema consolidation — one shared
/// validator for constructor and delta paths (player + NPC), canonical rarity/type
/// enums, free-form passive group.
/// </summary>
public sealed partial class MortalSkillReferenceAuthorityTests
{
    [Fact]
    public async Task PlayerStateFiles_ActiveSkillUnknownRarity_FailsWithSharedCode()
    {
        await _fs.WriteFileAtomicAsync("game_state/player/skills_active.json", """
        {
          "activeSkillChanges": [
            {
              "skillName": "Мифический выпад",
              "skillDescription": "Проверочный навык с неканонической редкостью.",
              "rarity": "Mythic",
              "combatEffect": {
                "actionName": "Мифический выпад",
                "actionCost": "Main",
                "effects": [
                  {
                    "effectType": "Damage",
                    "targetType": "Enemy",
                    "effectDescription": "Проверочный эффект.",
                    "value": "45%",
                    "poiseDamage": "5%"
                  }
                ],
                "isActivatedEffect": true
              },
              "scalingCharacteristic": "Intelligence"
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync(GameStateValidationPhase.PlayerStateFiles);

        Assert.Contains(issues, issue => issue.Code == "skill_invalid_rarity");
    }

    [Fact]
    public async Task PlayerStateFiles_PassiveSkillUnknownRarity_FailsWithSharedCode()
    {
        await WritePassiveSkillAsync(rarity: "Mythic");

        var issues = await _validator.ValidateGameStateAsync(GameStateValidationPhase.PlayerStateFiles);

        Assert.Contains(issues, issue => issue.Code == "skill_invalid_rarity");
    }

    [Fact]
    public async Task NpcStateFiles_NpcSkillUnknownRarity_FailsWithSharedCode()
    {
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_skills.json", """
        {
          "NPCActiveSkillChanges": [
            {
              "NPCName": "Тренер Гараст",
              "skillChanges": [
                {
                  "skillName": "Мифический удар",
                  "skillDescription": "Проверочный NPC-навык с неканонической редкостью.",
                  "rarity": "Mythic",
                  "combatEffect": {
                    "actionName": "Мифический удар",
                    "actionCost": "Main",
                    "effects": [
                      {
                        "effectType": "Damage",
                        "targetType": "Enemy",
                        "effectDescription": "Проверочный эффект.",
                        "value": "30%",
                        "poiseDamage": "5%"
                      }
                    ],
                    "isActivatedEffect": true
                  }
                }
              ]
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync(GameStateValidationPhase.NpcStateFiles);

        Assert.Contains(issues, issue => issue.Code == "skill_invalid_rarity");
    }

    /// <summary>
    /// Recorded decision guard: passive `group` stays a free-form narrative label
    /// (GM examples author groups like "Архивное дело"); no group diagnostic ever.
    /// Passes before and after the consolidation.
    /// </summary>
    [Fact]
    public async Task PlayerStateFiles_PassiveSkillFreeFormGroup_Passes()
    {
        await WritePassiveSkillAsync(group: "Архивное дело");

        var issues = await _validator.ValidateGameStateAsync(GameStateValidationPhase.PlayerStateFiles);

        Assert.DoesNotContain(issues, issue =>
            issue.Code is not null &&
            issue.Code.Contains("group", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Positive guard: every canonical rarity ladder value is accepted on the player
    /// delta path. Passes before and after the enum check exists.
    /// </summary>
    [Theory]
    [InlineData("Common")]
    [InlineData("Uncommon")]
    [InlineData("Rare")]
    [InlineData("Epic")]
    [InlineData("Legendary")]
    public async Task PlayerStateFiles_AllCanonicalRarityValues_Pass(string rarity)
    {
        await WritePassiveSkillAsync(rarity: rarity);

        var issues = await _validator.ValidateGameStateAsync(GameStateValidationPhase.PlayerStateFiles);

        Assert.DoesNotContain(issues, issue => issue.Code == "skill_invalid_rarity");
    }

    /// <summary>
    /// Byte-compatibility guard (FR-108): an already-diagnosed payload keeps its
    /// existing code after the validators delegate to the shared contract.
    /// Passes immediately; must still pass after the refactor.
    /// </summary>
    [Fact]
    public async Task PlayerStateFiles_PassiveSkillMissingMasteryLevel_KeepsLegacyCode()
    {
        await _fs.WriteFileAtomicAsync("game_state/player/skills_passive.json", """
        {
          "passiveSkillChanges": [
            {
              "skillName": "Архивное чутьё",
              "skillDescription": "Проверочный passive skill без masteryLevel.",
              "rarity": "Common",
              "type": "KnowledgeBased",
              "group": "Архивное дело",
              "maxMasteryLevel": 5,
              "structuredBonuses": null
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync(GameStateValidationPhase.PlayerStateFiles);

        Assert.Contains(issues, issue =>
            issue.Code == "passive_skill_missing_mastery_level");
    }

    private async Task WritePassiveSkillAsync(string rarity = "Common", string group = "Архивное дело")
    {
        await _fs.WriteFileAtomicAsync("game_state/player/skills_passive.json", $$"""
        {
          "passiveSkillChanges": [
            {
              "skillName": "Архивное чутьё",
              "skillDescription": "Проверочный passive skill.",
              "rarity": "{{rarity}}",
              "type": "KnowledgeBased",
              "group": "{{group}}",
              "masteryLevel": 1,
              "maxMasteryLevel": 5,
              "structuredBonuses": null
            }
          ]
        }
        """);
    }
}
