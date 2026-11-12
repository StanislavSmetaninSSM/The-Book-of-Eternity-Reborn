using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class TrainingServiceTests
{
    [Theory]
    [InlineData("skill_upper", "Fireball")]
    [InlineData("skill_lower", "fireball")]
    public async Task BuyTrainingAsync_MortalPractice_UpdatesOnlyExactCaseSibling(
        string targetId, string targetName)
    {
        await SeedMortalSoulStateAsync();
        await SeedMortalPracticeTeacherAsync();
        await SeedMortalPlayerProgressAsync(money: 500, currentLevelExperience: 400, experienceForNextLevel: 1000);
        await SeedEmptyPlayerSkillsAsync();
        var npcRoot = JsonNode.Parse(await _fs.ReadFileAsync("game_state/npcs/npc_core.json") ?? "{}")!.AsObject();
        var teacher = npcRoot["UpdateNPCs"]![0]!.AsObject();
        var taught = teacher["teacherProfile"]!["skills"]![0]!.AsObject();
        taught["skillId"] = targetId;
        taught["skillName"] = targetName;
        var showcase = teacher["trainingShowcase"]!.AsObject();
        var offer = showcase["offers"]![0]!.AsObject();
        offer["targetId"] = targetId;
        offer["targetName"] = targetName;
        showcase["sourceActorSnapshotHash"] = TrainingService.ComputeSourceSnapshotHash(teacher);
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", npcRoot.ToJsonString());

        var skills = new JsonArray();
        var mastery = new JsonArray();
        foreach (var (id, name) in new[] { ("skill_upper", "Fireball"), ("skill_lower", "fireball") })
        {
            skills.Add(new JsonObject
            {
                ["skillId"] = id,
                ["skillName"] = name,
                ["skillDescription"] = "An exact independent skill.",
                ["rarity"] = "Common",
                ["actionCost"] = "Fast",
                ["combatEffect"] = new JsonObject
                {
                    ["isActivatedEffect"] = true,
                    ["actionName"] = name,
                    ["actionDescription"] = "An exact independent action.",
                    ["damageType"] = "fire",
                    ["baseDamage"] = 1,
                    ["range"] = "ranged",
                    ["actionCost"] = "Fast",
                    ["actionPointCost"] = 1,
                    ["cooldown"] = 0
                }
            });
            mastery.Add(new JsonObject
            {
                ["skillId"] = id,
                ["skillName"] = name,
                ["newMasteryLevel"] = 1,
                ["newCurrentMasteryProgress"] = 1,
                ["newMasteryProgressNeeded"] = 5,
                ["masteryLeveledUp"] = false
            });
        }
        var activeBefore = new JsonObject { ["activeSkillChanges"] = skills }.ToJsonString();
        await _fs.WriteFileAtomicAsync("game_state/player/skills_active.json", activeBefore);
        await _fs.WriteFileAtomicAsync("game_state/player/skill_mastery.json",
            new JsonObject { ["skillMasteryChanges"] = mastery }.ToJsonString());

        var result = await CreateService().BuyTrainingAsync("npc_hunter_001", "offer_knife_practice", currentTurn: 15);

        Assert.True(result.Success);
        Assert.False(_fs.FileExists(TrainingRequestState.PendingRequestPath));
        Assert.Equal(activeBefore, await _fs.ReadFileAsync("game_state/player/skills_active.json"));
        var after = JsonNode.Parse(await _fs.ReadFileAsync("game_state/player/skill_mastery.json") ?? "{}")!["skillMasteryChanges"]!.AsArray();
        Assert.Equal(2, after.Count);
        foreach (var row in after.OfType<JsonObject>())
        {
            var selected = row["skillId"]!.GetValue<string>() == targetId;
            Assert.Equal(selected ? 3 : 1, row["newCurrentMasteryProgress"]!.GetValue<int>());
            Assert.Equal(1, row["newMasteryLevel"]!.GetValue<int>());
            Assert.Equal(5, row["newMasteryProgressNeeded"]!.GetValue<int>());
        }
    }

    [Theory]
    [InlineData("rarity", "ImpossibleRarity")]
    [InlineData("actionCost", "ImpossibleActionCost")]
    [InlineData("scalingCharacteristic", "ImaginaryCharacteristic")]
    public async Task BuyTrainingAsync_MortalUnlock_DoesNotPublishInvalidProfile(string field, string value)
    {
        await SeedMortalSoulStateAsync();
        await SeedMortalActiveUnlockTeacherAsync(includeCombatEffect: true);
        await SeedMortalPlayerProgressAsync(money: 500, currentLevelExperience: 400, experienceForNextLevel: 1000);
        await SeedEmptyPlayerSkillsAsync();
        var npcRoot = JsonNode.Parse(await _fs.ReadFileAsync("game_state/npcs/npc_core.json") ?? "{}")!.AsObject();
        var teacher = npcRoot["UpdateNPCs"]![0]!.AsObject();
        teacher["trainingShowcase"]!["offers"]![0]![field] = value;
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", npcRoot.ToJsonString());
        var activeBefore = await _fs.ReadFileAsync("game_state/player/skills_active.json");
        var masteryBefore = await _fs.ReadFileAsync("game_state/player/skill_mastery.json");

        _ = await CreateService().BuyTrainingAsync("npc_duelist_001", "offer_lunge_unlock", currentTurn: 14);

        // Rejection or existing pending-GM evolution is acceptable; invalid local publication is not.
        Assert.Equal(activeBefore, await _fs.ReadFileAsync("game_state/player/skills_active.json"));
        Assert.Equal(masteryBefore, await _fs.ReadFileAsync("game_state/player/skill_mastery.json"));
    }

    [Fact]
    public async Task BuyTrainingAsync_MortalUnlock_ValidProfileStillPublishes()
    {
        await SeedMortalSoulStateAsync();
        await SeedMortalActiveUnlockTeacherAsync(includeCombatEffect: true);
        await SeedMortalPlayerProgressAsync(money: 500, currentLevelExperience: 400, experienceForNextLevel: 1000);
        await SeedEmptyPlayerSkillsAsync();

        var result = await CreateService().BuyTrainingAsync("npc_duelist_001", "offer_lunge_unlock", currentTurn: 14);

        Assert.True(result.Success);
        Assert.False(_fs.FileExists(TrainingRequestState.PendingRequestPath));
        using var doc = JsonDocument.Parse(await _fs.ReadFileAsync("game_state/player/skills_active.json") ?? "{}");
        var skill = Assert.Single(doc.RootElement.GetProperty("activeSkillChanges").EnumerateArray());
        var issues = new List<ValidationIssue>();
        MortalSkillProfileContract.ValidateActiveSkillObject(skill, "skill", issues);
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.Equal("quick_lunge", skill.GetProperty("skillId").GetString());
    }

}
