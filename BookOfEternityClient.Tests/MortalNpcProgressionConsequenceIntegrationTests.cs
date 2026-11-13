using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalNpcProgressionConsequenceIntegrationTests
{
    [Fact]
    public void CoreOnly_TwoLevelGainWithoutAward_RefusesFinalConsequence()
    {
        var (before, current) = Roots(3);
        var result = Evaluate(before, current, Authority());
        Assert.False(result.CanApply);
        Assert.Contains(result.Issues, issue => issue.Code == "npc_progression_skill_award_count");
    }

    [Fact]
    public void SkillCandidate_TwoLevelGainAndMatchingGrant_ValidatesAfterComposition()
    {
        var (before, current) = Roots(3, "Archive");
        var result = MortalNpcAcceptedSkillCandidate.Compose(before, new(), current, Grant(), Authority());
        Assert.True(result.IsValid, string.Join("; ", result.Issues.Select(issue => issue.Code + ": " + issue.Message)));
        var actor = result.Core!["NPCsInScene"]![0]!;
        Assert.Equal(3, actor["level"]!.GetValue<int>());
        Assert.Equal("Archive", Assert.Single(actor["passiveSkills"]!.AsArray())!["skillName"]!.GetValue<string>());
        Assert.False(result.Sidecar!.ContainsKey("NPCPassiveSkillChanges"));
    }

    [Fact]
    public void SkillCandidate_WrongSkillClaim_RefusesDespiteActualGrant()
    {
        var (before, current) = Roots(3, "Different skill");
        var result = MortalNpcAcceptedSkillCandidate.Compose(before, new(), current, Grant(), Authority());
        Assert.False(result.IsValid);
        Assert.Null(result.Core);
        Assert.Null(result.Sidecar);
        Assert.Contains(result.Issues, issue => issue.Code == "npc_progression_skill_award_unfulfilled");
    }

    [Fact]
    public void CoreOnly_OneLevelGain_DoesNotDemandSkillAward()
    {
        var (before, current) = Roots(2);
        var result = Evaluate(before, current, Authority());
        Assert.Empty(result.Issues);
        Assert.True(result.CanApply);
        NpcCoreChangesContract.Apply(current, result);
        Assert.Equal(2, current["NPCsInScene"]![0]!["level"]!.GetValue<int>());
        Assert.Empty(current["NPCsInScene"]![0]!["passiveSkills"]!.AsArray());
    }

    [Fact]
    public void CoreOnly_ClaimedButMissingSkill_CannotDischargeAward()
    {
        var (before, current) = Roots(3, "Archive");
        var result = Evaluate(before, current, Authority());
        Assert.False(result.CanApply);
        Assert.Contains(result.Issues, issue => issue.Code == "npc_progression_skill_award_unfulfilled");
    }

    [Fact]
    public void CoreOnly_FixedWorldPolicy_AcceptsExactCharacteristicGain()
    {
        var (before, current) = Roots(2);
        current["NPCCoreChanges"]![0]!["characteristicValues"] = JsonNode.Parse("{\"strength\":5}");
        var result = Evaluate(before, current, Authority(FixedPolicy()));
        Assert.Empty(result.Issues);
        Assert.True(result.CanApply);
        NpcCoreChangesContract.Apply(current, result);
        Assert.Equal(5, current["NPCsInScene"]![0]!["characteristics"]!["strength"]!.GetValue<int>());
    }

    [Fact]
    public void CoreOnly_FixedWorldPolicy_RefusesOmittedCharacteristicGain()
    {
        var (before, current) = Roots(2);
        var result = Evaluate(before, current, Authority(FixedPolicy()));
        Assert.False(result.CanApply);
        Assert.Contains(result.Issues, issue => issue.Code == "npc_progression_characteristic_delta");
    }

    [Fact]
    public void CoreOnly_MalformedPresentWorldPolicy_IsNotNoGrowthAuthority()
    {
        var (before, current) = Roots(2);
        var result = Evaluate(before, current, Authority(new JsonObject { ["npcProgression"] = null }));
        Assert.False(result.CanApply);
        Assert.Contains(result.Issues, issue => issue.Code == "npc_progression_consequence_policy_invalid");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("3")]
    public void SkillCandidate_PresentNonArrayAwards_RefusesBeforeDeferredFinalGate(string json)
    {
        var (before, current) = Roots(2);
        current["NPCCoreChanges"]![0]!["progression"]!["skillAwards"] = JsonNode.Parse(json);
        var result = MortalNpcAcceptedSkillCandidate.Compose(before, new(), current, new(), Authority());
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "npc_progression_skill_award_invalid");
    }

    [Theory]
    [InlineData("npcId")]
    [InlineData("id")]
    public void SkillCandidate_ExistingStrictOwnerAlias_PreservesIdentityAndDischargesAward(string alias)
    {
        var (before, current) = Roots(3, "Archive");
        foreach (var root in new[] { before, current })
        {
            var actor = root["NPCsInScene"]![0]!.AsObject();
            actor.Remove("NPCId");
            actor[alias] = "npc_consequences";
        }
        var result = MortalNpcAcceptedSkillCandidate.Compose(before, new(), current, Grant(), Authority());
        Assert.True(result.IsValid, string.Join("; ", result.Issues.Select(issue => issue.Code + ": " + issue.Message)));
        var final = result.Core!["NPCsInScene"]![0]!.AsObject();
        Assert.False(final.ContainsKey("NPCId"));
        Assert.Equal("npc_consequences", final[alias]!.GetValue<string>());
        Assert.Equal(3, final["level"]!.GetValue<int>());
        Assert.Equal("Archive", Assert.Single(final["passiveSkills"]!.AsArray())!["skillName"]!.GetValue<string>());
        Assert.False(result.Sidecar!.ContainsKey("NPCPassiveSkillChanges"));
    }

    [Fact]
    public void UnfulfilledSkillClaim_DiagnosticPointsToAuthorableAwardRow()
    {
        var (before, current) = Roots(3, "Different skill");
        var result = MortalNpcAcceptedSkillCandidate.Compose(before, new(), current, Grant(), Authority());
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "npc_progression_skill_award_unfulfilled" &&
            issue.FilePath == "game_state/npcs/npc_core.json.NPCCoreChanges[0].progression.skillAwards[0]");
    }

    [Fact]
    public void MissingCharacteristicGain_DiagnosticPointsToAuthorableCharacteristicValue()
    {
        var (before, current) = Roots(2);
        var result = Evaluate(before, current, Authority(FixedPolicy()));
        Assert.False(result.CanApply);
        Assert.Contains(result.Issues, issue => issue.Code == "npc_progression_characteristic_delta" &&
            issue.FilePath == "game_state/npcs/npc_core.json.NPCCoreChanges[0].characteristicValues.strength");
    }

    [Fact]
    public void UnknownWorldCharacteristicKey_DiagnosticPointsToWorldPolicyNotActorDelta()
    {
        var (before, current) = Roots(2);
        var world = JsonNode.Parse("""
            {"npcProgression":{"schemaVersion":1,"characteristics":{"mode":"fixedIncrements","perLevel":{"invented":1}}}}
            """)!.AsObject();
        var result = Evaluate(before, current, Authority(world));
        Assert.False(result.CanApply);
        Assert.Contains(result.Issues, issue => issue.Code == "npc_progression_characteristic_authority" &&
            issue.FilePath == "lore/current_world/world_setting.json.npcProgression.characteristics");
    }

    private static NpcCoreChangesContract.Evaluation Evaluate(JsonObject before, JsonObject current,
        NpcCoreChangesContract.Authority authority) => NpcCoreChangesContract.Evaluate(current, before, authority,
            ValidationService.ValidateNpcCoreFateCardsAgainstProductionContract, detectDirectMutations: true);

    private static NpcCoreChangesContract.Authority Authority(JsonObject? world = null) =>
        new(new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal),
            new(StringComparer.Ordinal) { "strength" })
        {
            ProgressionSource = new("session_consequences", "request_consequences", 7, "snapshot_consequences", "mortal_world"),
            NpcCharacteristicPolicy = MortalNpcProgressionConsequences.ParseWorldPolicy(world)
        };

    private static (JsonObject Before, JsonObject Current) Roots(int level, string? skillAward = null)
    {
        var before = JsonNode.Parse("""
            {"NPCsInScene":[{"NPCId":"npc_consequences","level":1,"experience":0,"experienceForNextLevel":10,
              "progressionProfile":{"schemaVersion":1,"mode":"table","thresholds":[10,20,30],"capOverflow":"discard",
                "consequences":{"skills":{"levelsPerAward":2,"allowedAwards":["newSkill","masteryIncrease"]},
                  "characteristics":{"mode":"currentWorld"}}},
              "characteristics":{"strength":4},"activeSkills":[],"passiveSkills":[]}]}
            """)!.AsObject();
        var current = before.DeepClone().AsObject();
        var progression = new JsonObject
        {
            ["source"] = new JsonObject { ["sessionId"] = "session_consequences", ["requestId"] = "request_consequences", ["turnNumber"] = 7 },
            ["experienceDelta"] = level == 2 ? 10 : 30,
            ["level"] = level, ["experience"] = 0, ["experienceForNextLevel"] = level == 2 ? 20 : 30
        };
        if (skillAward is not null) progression["skillAwards"] = new JsonArray(new JsonObject
        { ["kind"] = "passive", ["skillName"] = skillAward, ["award"] = "newSkill" });
        current["NPCCoreChanges"] = new JsonArray(new JsonObject
        { ["NPCId"] = "npc_consequences", ["reason"] = "Study archives", ["progression"] = progression });
        return (before, current);
    }

    private static JsonObject FixedPolicy() => JsonNode.Parse("""
        {"npcProgression":{"schemaVersion":1,"characteristics":{"mode":"fixedIncrements","perLevel":{"strength":1}}}}
        """)!.AsObject();

    private static JsonObject Grant() => JsonNode.Parse("""
        {"NPCPassiveSkillChanges":[{"NPCId":"npc_consequences","skillChanges":[{
          "skillName":"Archive","skillDescription":"Archive knowledge","rarity":"Common",
          "type":"KnowledgeBased","group":"Archives","masteryLevel":1,"maxMasteryLevel":5,
          "structuredBonuses":null,"knowledgeDomain":"Archives","unlockedActiveSkillsCount":0,"maxUnlockableActiveSkills":2}]}]}
        """)!.AsObject();
}
