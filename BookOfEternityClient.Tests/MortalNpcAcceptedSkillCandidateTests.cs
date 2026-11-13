using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalNpcAcceptedSkillCandidateTests
{
    [Fact]
    public void Compose_ConsecutiveSkillOnlyGrantsAndOmission_PreserveCanonicalSkillsAndConsumeCommands()
    {
        var original = Core();
        var command = Grant("Archive A");
        command["unrelatedMetadata"] = "retain";
        var originalBytes = original.ToJsonString();
        var commandBytes = command.ToJsonString();
        var first = Compose(original, new(), original, command);
        Assert.True(first.IsValid);
        Assert.Equal("Archive A", Assert.Single(Skills(first.Core!))["skillName"]!.GetValue<string>());
        Assert.False(first.Sidecar!.ContainsKey("NPCPassiveSkillChanges"));
        Assert.Equal("retain", first.Sidecar["unrelatedMetadata"]!.GetValue<string>());
        Assert.Equal(originalBytes, original.ToJsonString());
        Assert.Equal(commandBytes, command.ToJsonString());

        var secondCommands = first.Sidecar.DeepClone().AsObject();
        secondCommands["NPCPassiveSkillChanges"] = Grant("Archive B")["NPCPassiveSkillChanges"]!.DeepClone();
        var second = Compose(first.Core!, first.Sidecar, first.Core!, secondCommands);
        Assert.True(second.IsValid);
        Assert.Equal(new[] { "Archive A", "Archive B" }, Skills(second.Core!)
            .Select(skill => skill["skillName"]!.GetValue<string>()).OrderBy(name => name, StringComparer.Ordinal));
        Assert.False(second.Sidecar!.ContainsKey("NPCPassiveSkillChanges"));
        var third = Compose(second.Core!, second.Sidecar, second.Core!, second.Sidecar);
        Assert.True(third.IsValid);
        Assert.True(JsonNode.DeepEquals(second.Core, third.Core));
        Assert.True(JsonNode.DeepEquals(second.Sidecar, third.Sidecar));
        third.Core!["NPCsInScene"]![0]!["name"] = "detached";
        Assert.Equal("Archivist", second.Core!["NPCsInScene"]![0]!["name"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("NPCActiveSkillChanges")]
    [InlineData("NPCPassiveSkillChanges")]
    [InlineData("NPCSkillMasteryChanges")]
    [InlineData("NPCPassiveSkillMasteryChanges")]
    public void Compose_NonemptyOriginalPendingCommands_RefusesWithoutCandidate(string property)
    {
        var originalSidecar = new JsonObject { [property] = new JsonArray(new JsonObject()) };
        var result = Compose(Core(), originalSidecar, Core(), new());
        Assert.False(result.IsValid);
        Assert.Null(result.Core);
        Assert.Null(result.Sidecar);
        Assert.Contains(result.Issues, issue => issue.Code == "npc_skill_candidate_original_pending" &&
            issue.FilePath == "game_state/npcs/npc_skills.json." + property);
    }

    [Fact]
    public void Compose_DirectCoreSkillRewrite_RemainsForbidden()
    {
        var original = Core();
        var current = original.DeepClone().AsObject();
        current["NPCsInScene"]![0]!["passiveSkills"] = new JsonArray(Skill("Bypass"));
        var result = Compose(original, new(), current, new());
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "npc_existing_core_direct_mutation_forbidden");
        Assert.Null(result.Core);
    }

    [Fact]
    public void Compose_CoreCommandAndSkillGrant_UseOneDetachedCandidate()
    {
        var original = Core();
        var current = original.DeepClone().AsObject();
        current["NPCCoreChanges"] = new JsonArray(new JsonObject
        {
            ["NPCId"] = "npc_archive", ["reason"] = "A changed outlook",
            ["profile"] = new JsonObject { ["worldview"] = "Share knowledge" }
        });
        var result = Compose(original, new(), current, Grant("Archive A"));
        Assert.True(result.IsValid);
        Assert.False(result.Core!.ContainsKey("NPCCoreChanges"));
        Assert.Equal("Share knowledge", result.Core["NPCsInScene"]![0]!["worldview"]!.GetValue<string>());
        Assert.Single(Skills(result.Core));
    }

    [Fact]
    public void Compose_InvalidAuthoredSkill_UsesProductionProfileValidation()
    {
        var commands = Grant("Archive A");
        commands["NPCPassiveSkillChanges"]![0]!["skillChanges"]![0]!["rarity"] = "Invented";
        var result = Compose(Core(), new(), Core(), commands);
        Assert.False(result.IsValid);
        Assert.Null(result.Core);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("skill_invalid_rarity", issue.Code);
        Assert.Equal("game_state/npcs/npc_skills.json.NPCPassiveSkillChanges[0].skillChanges[0].rarity", issue.FilePath);
    }

    [Fact]
    public void Compose_PassiveMastery_UpdatesCanonicalSkillAndConsumesOnlyPendingProperties()
    {
        var original = Core();
        original["NPCsInScene"]![0]!["passiveSkills"] = new JsonArray(Skill("Archive A"));
        var sidecar = new JsonObject
        {
            ["NPCPassiveSkillMasteryChanges"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = "npc_archive", ["skillName"] = "Archive A", ["newMasteryLevel"] = 2
            }),
            ["other"] = new JsonObject { ["keep"] = true }
        };
        var result = Compose(original, new(), original, sidecar);
        Assert.True(result.IsValid);
        Assert.Equal(2, Assert.Single(Skills(result.Core!))["masteryLevel"]!.GetValue<int>());
        Assert.False(result.Sidecar!.ContainsKey("NPCPassiveSkillMasteryChanges"));
        Assert.True(JsonNode.DeepEquals(sidecar["other"], result.Sidecar["other"]));
        Assert.Equal(1, Assert.Single(Skills(original))["masteryLevel"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("NPC_ARCHIVE")]
    [InlineData("npc_missing")]
    public void Compose_NonExactCurrentSkillOwner_Refuses(string actor)
    {
        var sidecar = Grant("Archive A");
        sidecar["NPCPassiveSkillChanges"]![0]!["npcId"] = actor;
        var result = Compose(Core(), new(), Core(), sidecar);
        Assert.False(result.IsValid);
        Assert.Null(result.Core);
        Assert.Null(result.Sidecar);
    }

    [Theory]
    [InlineData("npcPassiveSkillChanges")]
    [InlineData("NPCPASSIVESKILLCHANGES")]
    public void Compose_MiscasedPendingProperty_IsNotPreservedAsUnrelatedMetadata(string property)
    {
        var sidecar = new JsonObject { [property] = Grant("Archive A")["NPCPassiveSkillChanges"]!.DeepClone() };
        var result = Compose(Core(), new(), Core(), sidecar);
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "npc_skill_candidate_invalid_property_case");
    }

    [Fact]
    public void Compose_MasteringRemovedSkill_RefusesWholeCandidate()
    {
        var original = Core();
        original["NPCsInScene"]![0]!["passiveSkills"] = new JsonArray(Skill("Archive A"));
        var sidecar = new JsonObject
        {
            ["NPCPassiveSkillChanges"] = new JsonArray(new JsonObject
            {
                ["npcId"] = "npc_archive", ["skillsToRemove"] = new JsonArray("Archive A")
            }),
            ["NPCPassiveSkillMasteryChanges"] = new JsonArray(new JsonObject
            {
                ["npcId"] = "npc_archive", ["skillName"] = "Archive A", ["newMasteryLevel"] = 2
            })
        };
        var result = Compose(original, new(), original, sidecar);
        Assert.False(result.IsValid);
        Assert.Null(result.Core);
        Assert.Null(result.Sidecar);
        Assert.Single(Skills(original));
    }

    [Fact]
    public void Compose_DistinctAuthoredNamesWithSameStableSkillId_RejectsRatherThanOverwriting()
    {
        var first = Skill("Archive A");
        var second = Skill("Archive B");
        first["skillId"] = "skill_shared";
        second["skillId"] = "skill_shared";
        var sidecar = new JsonObject
        {
            ["NPCPassiveSkillChanges"] = new JsonArray(new JsonObject
            {
                ["npcId"] = "npc_archive", ["skillChanges"] = new JsonArray(first, second)
            })
        };
        var before = sidecar.ToJsonString();
        var result = Compose(Core(), new(), Core(), sidecar);
        Assert.False(result.IsValid);
        Assert.Null(result.Core);
        Assert.Null(result.Sidecar);
        Assert.Contains(result.Issues, issue => issue.Code == "npc_skill_candidate_duplicate_skill_id");
        Assert.Equal(before, sidecar.ToJsonString());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("42")]
    public void Compose_MalformedOriginalPendingValue_Refuses(string json)
    {
        var original = new JsonObject { ["NPCPassiveSkillChanges"] = JsonNode.Parse(json) };
        var result = Compose(Core(), original, Core(), new());
        Assert.False(result.IsValid);
        Assert.Null(result.Core);
        Assert.Null(result.Sidecar);
        Assert.Contains(result.Issues, issue => issue.Code == "npc_skill_candidate_original_pending");
    }

    [Fact]
    public void Compose_ActiveGrantAndMastery_ProducesCanonicalFinalProfile()
    {
        var active = ActiveSkill();
        var sidecar = new JsonObject
        {
            ["NPCActiveSkillChanges"] = new JsonArray(new JsonObject
            {
                ["npcId"] = "npc_archive", ["skillChanges"] = new JsonArray(active)
            }),
            ["NPCSkillMasteryChanges"] = new JsonArray(new JsonObject
            {
                ["npcId"] = "npc_archive", ["skillName"] = "Strike", ["skillId"] = "skill_strike",
                ["newMasteryLevel"] = 2, ["newCurrentMasteryProgress"] = 0, ["newMasteryProgressNeeded"] = 10
            })
        };
        var result = Compose(Core(), new(), Core(), sidecar);
        Assert.True(result.IsValid);
        var skill = Assert.Single(result.Core!["NPCsInScene"]![0]!["activeSkills"]!.AsArray());
        Assert.Equal("skill_strike", skill!["skillId"]!.GetValue<string>());
        Assert.Equal(2, skill["masteryLevel"]!.GetValue<int>());
        Assert.Equal(0, skill["currentMasteryProgress"]!.GetValue<int>());
        Assert.Equal(10, skill["masteryProgressNeeded"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(active["combatEffect"], skill["combatEffect"]));
        Assert.Empty(result.Sidecar!);
    }

    [Fact]
    public void Compose_ConflictingOwnerAliases_Refuses()
    {
        var sidecar = Grant("Archive A");
        sidecar["NPCPassiveSkillChanges"]![0]!["NPCId"] = "npc_other";
        var result = Compose(Core(), new(), Core(), sidecar);
        Assert.False(result.IsValid);
        Assert.Null(result.Core);
        Assert.Null(result.Sidecar);
        Assert.Contains(result.Issues, issue => issue.Code == "npc_skill_candidate_invalid_owner");
    }

    [Fact]
    public void Compose_CanonicalMirrors_ReceiveIdenticalDetachedSkillResults()
    {
        var original = Core();
        original["UpdateNPCs"] = original["NPCsInScene"]!.DeepClone();
        var result = Compose(original, new(), original, Grant("Archive A"));
        Assert.True(result.IsValid);
        Assert.Single(Skills(result.Core!));
        Assert.True(JsonNode.DeepEquals(result.Core!["NPCsInScene"]![0], result.Core["UpdateNPCs"]![0]));
        Assert.Empty(Skills(original));
        result.Core["NPCsInScene"]![0]!["passiveSkills"]![0]!["skillDescription"] = "detached";
        Assert.Equal("Archive knowledge", result.Core["UpdateNPCs"]![0]!["passiveSkills"]![0]!["skillDescription"]!.GetValue<string>());
    }

    private static JsonObject ActiveSkill() => new JsonObject
        {
            ["skillId"] = "skill_strike", ["skillName"] = "Strike",
            ["skillDescription"] = "A precise strike", ["rarity"] = "Common",
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

    [Theory]
    [InlineData("false", true)]
    [InlineData("true", true)]
    [InlineData("\"false\"", false)]
    public void Compose_CurrentMasteryLeveledUpMetadata_AcceptsOnlyBooleanWithoutGrantAuthority(string json, bool valid)
    {
        var original = Core();
        original["NPCsInScene"]![0]!["activeSkills"] = new JsonArray(ActiveSkill());
        var sidecar = new JsonObject
        {
            ["NPCSkillMasteryChanges"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = "npc_archive", ["skillName"] = "Strike", ["newMasteryLevel"] = 2,
                ["newCurrentMasteryProgress"] = 11, ["newMasteryProgressNeeded"] = 100,
                ["masteryLeveledUp"] = JsonNode.Parse(json)
            })
        };
        var result = Compose(original, new(), original, sidecar);
        Assert.Equal(valid, result.IsValid);
        if (valid)
        {
            var skill = Assert.Single(result.Core!["NPCsInScene"]![0]!["activeSkills"]!.AsArray());
            Assert.Equal(2, skill!["masteryLevel"]!.GetValue<int>());
            Assert.Equal(11, skill["currentMasteryProgress"]!.GetValue<int>());
            Assert.False(skill.AsObject().ContainsKey("masteryLeveledUp"));
            Assert.Empty(result.Sidecar!);
        }
        else
        {
            Assert.Null(result.Core);
            Assert.Null(result.Sidecar);
            Assert.NotEmpty(result.Issues);
        }
    }

    [Fact]
    public void Compose_CurrentNpcNameMetadata_IsDisplayOnlyOnProfileAndMasteryRows()
    {
        var sidecar = Grant("Archive A");
        sidecar["NPCPassiveSkillChanges"]![0]!["NPCName"] = "A display label, not a selector";
        sidecar["NPCPassiveSkillMasteryChanges"] = new JsonArray(new JsonObject
        {
            ["NPCId"] = "npc_archive", ["NPCName"] = "Another display label",
            ["skillName"] = "Archive A", ["newMasteryLevel"] = 2
        });
        var result = Compose(Core(), new(), Core(), sidecar);
        Assert.True(result.IsValid);
        Assert.Equal("Archivist", result.Core!["NPCsInScene"]![0]!["name"]!.GetValue<string>());
        Assert.Equal(2, Assert.Single(Skills(result.Core))["masteryLevel"]!.GetValue<int>());
        Assert.Empty(result.Sidecar!);
    }

    [Fact]
    public void Compose_NpcNameWithoutPermanentId_DoesNotResolveOwner()
    {
        var sidecar = Grant("Archive A");
        var row = sidecar["NPCPassiveSkillChanges"]![0]!.AsObject();
        row.Remove("npcId");
        row["NPCName"] = "Archivist";
        var result = Compose(Core(), new(), Core(), sidecar);
        Assert.False(result.IsValid);
        Assert.Null(result.Core);
        Assert.Null(result.Sidecar);
        Assert.Contains(result.Issues, issue => issue.Code == "npc_skill_candidate_invalid_owner");
    }

    private static MortalNpcAcceptedSkillCandidate.Result Compose(JsonObject original, JsonObject originalSidecar,
        JsonObject current, JsonObject sidecar) => MortalNpcAcceptedSkillCandidate.Compose(original, originalSidecar,
        current, sidecar, new NpcCoreChangesContract.Authority(new(StringComparer.Ordinal), new(StringComparer.Ordinal),
            new(StringComparer.Ordinal), new(StringComparer.Ordinal)));

    private static JsonObject Core() => new()
    {
        ["NPCsInScene"] = new JsonArray(new JsonObject
        {
            ["NPCId"] = "npc_archive", ["name"] = "Archivist", ["worldview"] = "Guard knowledge",
            ["activeSkills"] = new JsonArray(), ["passiveSkills"] = new JsonArray()
        })
    };

    private static JsonObject Grant(string name) => new()
    {
        ["NPCPassiveSkillChanges"] = new JsonArray(new JsonObject
        {
            ["npcId"] = "npc_archive", ["skillChanges"] = new JsonArray(Skill(name))
        })
    };

    private static JsonObject Skill(string name) => new()
    {
        ["skillName"] = name, ["skillDescription"] = "Archive knowledge", ["rarity"] = "Common",
        ["type"] = "KnowledgeBased", ["group"] = "Archives", ["masteryLevel"] = 1,
        ["structuredBonuses"] = null,
        ["maxMasteryLevel"] = 5, ["knowledgeDomain"] = "Archives",
        ["unlockedActiveSkillsCount"] = 0, ["maxUnlockableActiveSkills"] = 2
    };

    private static IEnumerable<JsonObject> Skills(JsonObject root) =>
        root["NPCsInScene"]![0]!["passiveSkills"]!.AsArray().OfType<JsonObject>();
}
