using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class TrainingServiceTests
{
    [Fact]
    public async Task BuyTrainingAsync_MortalUnlock_GmFallbackRechecksSkillRootBeforeCharging()
    {
        await SeedMortalSoulStateAsync();
        await SeedMortalPassiveUnlockTeacherAsync(targetValue: 2);
        await SeedMortalPlayerProgressAsync(money: 500, currentLevelExperience: 400, experienceForNextLevel: 1000);
        await SeedEmptyPlayerSkillsAsync();
        var scope = await new LocalInteractionScopeService(_fs).ResolveAsync();
        var paths = new[] { "game_state/core/player_status.json", "game_state/player/experience.json", "game_state/npcs/npc_core.json" };
        var before = new Dictionary<string, string?>();
        foreach (var path in paths) before[path] = await _fs.ReadFileAsync(path);
        const string skillPath = "game_state/player/skills_passive.json";
        var concurrent = """{"passiveSkillChanges":[{"skillId":"concurrent","skillName":"СНЯТИЕ ШКУР"}]}""";
        var resolver = new SequenceLocalInteractionScopeResolver(async call =>
        {
            if (call == 2) await _fs.WriteFileAtomicAsync(skillPath, concurrent);
        }, scope, scope);
        var service = new TrainingService(_fs, NullLogger<TrainingService>.Instance, resolver);
        var result = await service.BuyTrainingAsync("npc_skinner_001", "offer_skinning_unlock", 15);
        Assert.False(result.Success);
        foreach (var path in paths) Assert.Equal(before[path], await _fs.ReadFileAsync(path));
        Assert.Equal(concurrent, await _fs.ReadFileAsync(skillPath));
        Assert.Empty(await TrainingRequestState.ReadRequestsAsync(_fs));
    }

    [Theory]
    [InlineData(false, "fireball", false)]
    [InlineData(false, "Firеball", false)]
    [InlineData(true, "fireball", false)]
    [InlineData(true, "Firеball", false)]
    [InlineData(false, "Frostbolt", true)]
    [InlineData(true, "Frostbolt", true)]
    [InlineData(true, "fireball", false, "generic")]
    [InlineData(false, "fireball", false, "evolution")]
    [InlineData(true, "Firеball", false, "evolution")]
    public async Task BuyTrainingAsync_MortalUnlock_RejectsNewConfusableSiblingBeforeSpending(bool passive, string second, bool allowed, string mode = "explicit")
    {
        await SeedMortalSoulStateAsync();
        if (passive) await SeedMortalPassiveUnlockTeacherAsync(targetKind: mode == "generic" ? "skill_mastery" : "passive_skill_unlock");
        else await SeedMortalActiveUnlockTeacherAsync(includeCombatEffect: true);
        await SeedMortalPlayerProgressAsync(money: 500, currentLevelExperience: 400, experienceForNextLevel: 1000);
        await SeedEmptyPlayerSkillsAsync();
        var actor = passive ? "npc_skinner_001" : "npc_duelist_001";
        var offerId = passive ? "offer_skinning_unlock" : "offer_lunge_unlock";
        async Task Offer(string name, string id)
        {
            var root = JsonNode.Parse(await _fs.ReadFileAsync("game_state/npcs/npc_core.json") ?? "{}")!.AsObject();
            var teacher = root["UpdateNPCs"]![0]!.AsObject();
            teacher["teacherProfile"]!["skills"]![0]!["skillId"] = id;
            teacher["teacherProfile"]!["skills"]![0]!["skillName"] = name;
            teacher["trainingShowcase"]!["offers"]![0]!["targetId"] = id;
            teacher["trainingShowcase"]!["offers"]![0]!["targetName"] = name;
            if (id == "second_skill" && mode == "evolution") teacher["trainingShowcase"]!["offers"]![0]!["targetValue"] = 2;
            teacher["trainingShowcase"]!["sourceActorSnapshotHash"] = TrainingService.ComputeSourceSnapshotHash(teacher);
            await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", root.ToJsonString());
        }
        await Offer("Fireball", "first_skill");
        var first = await CreateService().BuyTrainingAsync(actor, offerId, 14);
        Assert.True(first.Success, first.Message);
        await Offer(second, "second_skill");
        var paths = new[] { "game_state/npcs/npc_core.json", "game_state/core/player_status.json",
            "game_state/player/experience.json", "game_state/player/skills_active.json",
            "game_state/player/skills_passive.json", "game_state/player/skill_mastery.json" };
        var before = new Dictionary<string, string?>();
        foreach (var path in paths) before[path] = await _fs.ReadFileAsync(path);
        var result = await CreateService().BuyTrainingAsync(actor, offerId, 15);
        Assert.Equal(allowed, result.Success);
        Assert.False(_fs.FileExists(TrainingRequestState.PendingRequestPath));
        if (!allowed)
            foreach (var path in paths) Assert.Equal(before[path], await _fs.ReadFileAsync(path));
        var skillPath = passive ? "game_state/player/skills_passive.json" : "game_state/player/skills_active.json";
        var section = passive ? "passiveSkillChanges" : "activeSkillChanges";
        var current = JsonNode.Parse(await _fs.ReadFileAsync(skillPath) ?? "{}")!;
        Assert.Equal(allowed ? 2 : 1, current[section]!.AsArray().Count);
        var after = current.DeepClone();
        after[section]![0]!["skillDescription"] = "Revised complete profile description";
        var issues = EffectAcceptedTurnInputComposer.ValidateAcceptedSkillComposition(
            new Dictionary<string, JsonNode?> { [skillPath] = current },
            new Dictionary<string, JsonNode?> { [skillPath] = after });
        Assert.DoesNotContain(issues, issue => issue.Code == "effect_skill_composition_name_confusable");
    }

    [Theory]
    [InlineData("skill_upper", "Fireball")]
    [InlineData("skill_lower", "fireball")]
    public async Task BuyTrainingAsync_MortalPractice_UpdatesOnlyExactCaseSibling(
        string targetId, string targetName)
    {
        await SeedExactCaseSiblingPracticeAsync(targetId, targetName);
        var activeBefore = await _fs.ReadFileAsync("game_state/player/skills_active.json");

        var result = await CreateService().BuyTrainingAsync("npc_hunter_001", "offer_knife_practice", currentTurn: 15);

        Assert.True(result.Success);
        Assert.False(_fs.FileExists(TrainingRequestState.PendingRequestPath));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(activeBefore!),
            JsonNode.Parse(await _fs.ReadFileAsync("game_state/player/skills_active.json") ?? "{}")));
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

    [Fact]
    public async Task BuyTrainingAsync_MortalPractice_ConflictingExactIdentityDoesNotMutateSkills()
    {
        await SeedExactCaseSiblingPracticeAsync("skill_upper", "Fireball");
        var npcRoot = JsonNode.Parse(await _fs.ReadFileAsync("game_state/npcs/npc_core.json") ?? "{}")!.AsObject();
        npcRoot["UpdateNPCs"]![0]!["trainingShowcase"]!["offers"]![0]!["targetName"] = "fireball";
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", npcRoot.ToJsonString());
        var paths = new[]
        {
            "game_state/player/skills_active.json", "game_state/player/skills_passive.json",
            "game_state/player/skill_mastery.json"
        };
        var before = new Dictionary<string, string?>();
        foreach (var path in paths)
            before[path] = await _fs.ReadFileAsync(path);
        var statusBefore = await _fs.ReadFileAsync("game_state/core/player_status.json");
        var experienceBefore = await _fs.ReadFileAsync("game_state/player/experience.json");

        var result = await CreateService().BuyTrainingAsync("npc_hunter_001", "offer_knife_practice", currentTurn: 15);

        foreach (var path in paths)
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(before[path] ?? "{}"),
                JsonNode.Parse(await _fs.ReadFileAsync(path) ?? "{}")), path);
        // Either reject before spending, or use the existing pending-GM path without local progression.
        if (result.Success)
            Assert.True(_fs.FileExists(TrainingRequestState.PendingRequestPath));
        else
        {
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(statusBefore ?? "{}"),
                JsonNode.Parse(await _fs.ReadFileAsync("game_state/core/player_status.json") ?? "{}")));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(experienceBefore ?? "{}"),
                JsonNode.Parse(await _fs.ReadFileAsync("game_state/player/experience.json") ?? "{}")));
        }
    }

    [Fact]
    public async Task BuyTrainingAsync_MortalPassiveUnlock_DoesNotPublishInvalidProfile()
    {
        await SeedMortalSoulStateAsync();
        await SeedMortalPassiveUnlockTeacherAsync();
        await SeedMortalPlayerProgressAsync(money: 500, currentLevelExperience: 400, experienceForNextLevel: 1000);
        await SeedEmptyPlayerSkillsAsync();
        var npcRoot = JsonNode.Parse(await _fs.ReadFileAsync("game_state/npcs/npc_core.json") ?? "{}")!.AsObject();
        npcRoot["UpdateNPCs"]![0]!["trainingShowcase"]!["offers"]![0]!["rarity"] = "ImpossibleRarity";
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", npcRoot.ToJsonString());
        var passiveBefore = await _fs.ReadFileAsync("game_state/player/skills_passive.json");
        var activeBefore = await _fs.ReadFileAsync("game_state/player/skills_active.json");
        var masteryBefore = await _fs.ReadFileAsync("game_state/player/skill_mastery.json");

        _ = await CreateService().BuyTrainingAsync("npc_skinner_001", "offer_skinning_unlock", currentTurn: 14);

        Assert.Equal(passiveBefore, await _fs.ReadFileAsync("game_state/player/skills_passive.json"));
        Assert.Equal(activeBefore, await _fs.ReadFileAsync("game_state/player/skills_active.json"));
        Assert.Equal(masteryBefore, await _fs.ReadFileAsync("game_state/player/skill_mastery.json"));
    }

    [Fact]
    public async Task BuyTrainingAsync_MortalPassiveUnlock_AllowsSameNameInActiveDomain()
    {
        await SeedExactCaseSiblingPracticeAsync("skill_upper", "Fireball");
        await SeedMortalPassiveUnlockTeacherAsync();
        var active = JsonNode.Parse(await _fs.ReadFileAsync("game_state/player/skills_active.json") ?? "{}")!.AsObject();
        var activeSkills = active["activeSkillChanges"]!.AsArray();
        activeSkills.RemoveAt(1);
        activeSkills[0]!["skillName"] = "Снятие шкур";
        await _fs.WriteFileAtomicAsync("game_state/player/skills_active.json", active.ToJsonString());
        var mastery = JsonNode.Parse(await _fs.ReadFileAsync("game_state/player/skill_mastery.json") ?? "{}")!.AsObject();
        var masteryRows = mastery["skillMasteryChanges"]!.AsArray();
        masteryRows.RemoveAt(1);
        masteryRows[0]!["skillName"] = "Снятие шкур";
        await _fs.WriteFileAtomicAsync("game_state/player/skill_mastery.json", mastery.ToJsonString());

        var result = await CreateService().BuyTrainingAsync("npc_skinner_001", "offer_skinning_unlock", currentTurn: 14);

        Assert.True(result.Success);
        Assert.False(_fs.FileExists(TrainingRequestState.PendingRequestPath));
        var passive = JsonNode.Parse(await _fs.ReadFileAsync("game_state/player/skills_passive.json") ?? "{}")!["passiveSkillChanges"]!.AsArray();
        var skill = Assert.Single(passive);
        Assert.Equal("skinning", skill!["skillId"]!.GetValue<string>());
        Assert.Equal("Снятие шкур", skill["skillName"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(active,
            JsonNode.Parse(await _fs.ReadFileAsync("game_state/player/skills_active.json") ?? "{}")));
        Assert.True(JsonNode.DeepEquals(mastery,
            JsonNode.Parse(await _fs.ReadFileAsync("game_state/player/skill_mastery.json") ?? "{}")));
    }

    [Fact]
    public async Task EnsureTrainingAsync_MortalSkillEvolutionConflictingExactIdentity_PreservesRequest()
    {
        await SeedExactCaseSiblingPracticeAsync("skill_upper", "Fireball");
        await TrainingRequestState.WriteRequestAsync(
            _fs,
            "mortal_training_skill_evolution",
            "npc_hunter_001",
            "Старый охотник",
            "npc_teacher",
            "mortal",
            createdAtTurn: 13,
            sourceActorSnapshotHash: "paid-lesson-hash",
            reason: "mastery_threshold_crossed",
            details: new JsonObject
            {
                ["targetId"] = "skill_upper",
                ["targetName"] = "fireball",
                ["targetKind"] = "active_skill_mastery",
                ["targetValue"] = 1,
                ["skillStateBefore"] = new JsonObject()
            });
        var before = Assert.Single(await TrainingRequestState.ReadRequestsAsync(_fs));

        await CreateService().EnsureTrainingAsync(currentTurn: 16, createPendingRequests: false);

        var after = Assert.Single(await TrainingRequestState.ReadRequestsAsync(_fs));
        Assert.Equal(before.RequestId, after.RequestId);
        Assert.Equal("mortal_training_skill_evolution", after.RequestKind);
        Assert.True(JsonNode.DeepEquals(before.Details, after.Details));
    }

    private async Task SeedExactCaseSiblingPracticeAsync(string targetId, string targetName)
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

    }

}
