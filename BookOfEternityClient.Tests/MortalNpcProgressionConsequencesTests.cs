using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using Contract = BookOfEternityClient.Services.MortalNpcProgressionConsequences;

namespace BookOfEternityClient.Tests;

public sealed class MortalNpcProgressionConsequencesTests
{
    [Theory]
    [InlineData(2, 3, true)]
    [InlineData(1, 3, false)]
    public void Cadence_CountsFullLevelsInThisTransition(int before, int after, bool valid)
    {
        var issues = Evaluate(Actor(before), Actor(after));
        if (valid) Assert.Empty(issues);
        else Assert.Contains(issues, p => p.Code == "npc_progression_skill_award_count");
    }

    [Fact]
    public void NewPassiveSkill_DischargeUsesActualCandidateAndDoesNotMutateInputs()
    {
        var before = Actor(1); var after = Actor(3);
        after["passiveSkills"]!.AsArray().Add(Skill("Archive", "skill_archive", 1));
        var awards = Awards("passive", "Archive", "skill_archive", "newSkill");
        var originalText = before.ToJsonString(); var candidateText = after.ToJsonString(); var awardsText = awards.ToJsonString();
        Assert.Empty(Evaluate(before, after, awards));
        Assert.Equal(originalText, before.ToJsonString());
        Assert.Equal(candidateText, after.ToJsonString());
        Assert.Equal(awardsText, awards.ToJsonString());
    }

    [Fact]
    public void ExistingActiveMasteryIncrease_DischargesAward()
    {
        Assert.Empty(Evaluate(Actor(1, mastery: 1), Actor(3, mastery: 2), MasteryAwards(1)));
    }

    [Fact]
    public void AuthoredAwardWithoutActualSkillChange_IsRejected()
    {
        Assert.Contains(Evaluate(Actor(1, mastery: 1), Actor(3, mastery: 1), MasteryAwards(1)),
            p => p.Code == "npc_progression_skill_award_unfulfilled");
    }

    [Fact]
    public void DifferentCandidateActor_CannotSupplyConsequences()
    {
        var after = Actor(3, mastery: 2); after["NPCId"] = "npc_other";
        Assert.Contains(Evaluate(Actor(1, mastery: 1), after, MasteryAwards(1)),
            p => p.Code == "npc_progression_consequence_owner");
    }

    [Fact]
    public void WrongStableSkillId_CannotDischargeMatchingName()
    {
        var awards = MasteryAwards(1); awards[0]!["skillId"] = "skill_other";
        Assert.Contains(Evaluate(Actor(1, mastery: 1), Actor(3, mastery: 2), awards),
            p => p.Code == "npc_progression_skill_award_unfulfilled");
    }

    [Fact]
    public void OneMasteryIncrease_CannotDischargeTwoAwards()
    {
        Assert.Contains(Evaluate(Actor(1, mastery: 1), Actor(5, mastery: 2), MasteryAwards(2)),
            p => p.Code == "npc_progression_skill_award_unfulfilled");
    }

    [Fact]
    public void TwoMasteryIncreases_CanDischargeTwoAwardsOnSameExistingSkill()
    {
        Assert.Empty(Evaluate(Actor(1, mastery: 1), Actor(5, mastery: 3), MasteryAwards(2)));
    }

    [Fact]
    public void NonProgressingProfile_RejectsAwards()
    {
        Assert.Contains(Evaluate(Actor(1, mastery: 1), Actor(1, mastery: 2), MasteryAwards(1),
            profile: new(false)), p => p.Code == "npc_progression_skill_award_count");
    }

    [Theory]
    [InlineData("strength", true)]
    [InlineData("invented", false)]
    public void FixedIncrements_RequireExactWorldAuthorizedKeys(string key, bool valid)
    {
        var before = Actor(1); var after = Actor(2);
        before["characteristics"]![key] = 4; after["characteristics"]![key] = 6;
        var policy = new Contract.CharacteristicPolicy(Contract.CharacteristicMode.FixedIncrements,
            new Dictionary<string, int> { [key] = 2 });
        var issues = Evaluate(before, after, policy: policy);
        if (valid) Assert.Empty(issues);
        else Assert.Contains(issues, p => p.Code == "npc_progression_characteristic_authority");
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void AllocatedPoints_RequireActualExactBudget(int increase, bool valid)
    {
        var before = Actor(1); var after = Actor(2);
        after["characteristics"]!["strength"] = 4 + increase;
        var policy = new Contract.CharacteristicPolicy(Contract.CharacteristicMode.AllocatedPoints,
            PointsPerLevel: 3, AllocationKeys: new HashSet<string>(StringComparer.Ordinal) { "strength" });
        var issues = Evaluate(before, after, policy: policy);
        if (valid) Assert.Empty(issues);
        else Assert.Contains(issues, p => p.Code == "npc_progression_characteristic_delta");
    }

    [Fact]
    public void AbsentWorldCharacteristicPolicy_PreservesCharacteristics()
    {
        var after = Actor(2); after["characteristics"]!["strength"] = 5;
        Assert.Contains(Evaluate(Actor(1), after), p => p.Code == "npc_progression_characteristic_delta");
    }

    [Fact]
    public void FixedPolicy_CallerCaseInsensitiveComparerDoesNotAuthorizeDifferentKey()
    {
        var before = Actor(1); var after = Actor(2);
        before["characteristics"]!["Strength"] = 10;
        after["characteristics"]!["Strength"] = 11;
        after["characteristics"]!["strength"] = 5;
        var policy = new Contract.CharacteristicPolicy(Contract.CharacteristicMode.FixedIncrements,
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["strength"] = 1 });
        Assert.Contains(Evaluate(before, after, policy: policy),
            p => p.Code == "npc_progression_characteristic_delta" && p.Field == "Strength");
    }

    [Fact]
    public void NoGrowthPolicy_PreservesUnchangedLargeFiniteJsonNumber()
    {
        var before = Actor(1); var after = Actor(2);
        before["characteristics"]!["strength"] = JsonNode.Parse("1e100");
        after["characteristics"]!["strength"] = JsonNode.Parse("1e100");
        Assert.Empty(Evaluate(before, after));
    }

    [Fact]
    public void NoGrowthPolicy_RejectsTinyNonzeroJsonDeltaWithoutDecimalRounding()
    {
        var before = Actor(1); var after = Actor(2);
        before["characteristics"]!["strength"] = JsonNode.Parse("0");
        after["characteristics"]!["strength"] = JsonNode.Parse("1e-29");
        Assert.Contains(Evaluate(before, after), p => p.Code == "npc_progression_characteristic_delta");
    }

    [Fact]
    public void FixedIncrement_AcceptsExactIntegerDeltaOnTypedFractionalBaseline()
    {
        var before = Actor(1); var after = Actor(2);
        before["characteristics"]!["strength"] = JsonValue.Create(4.5);
        after["characteristics"]!["strength"] = JsonValue.Create(5.5);
        var policy = new Contract.CharacteristicPolicy(Contract.CharacteristicMode.FixedIncrements,
            new Dictionary<string, int> { ["strength"] = 1 });
        Assert.Empty(Evaluate(before, after, policy: policy));
    }

    [Fact]
    public void ConfusableAwardReference_DoesNotResolveExactSkill()
    {
        var awards = MasteryAwards(1);
        awards[0]!["skillName"] = "Gu\u0430rd"; // Cyrillic a is not the canonical Latin a.
        Assert.Contains(Evaluate(Actor(1, mastery: 1), Actor(3, mastery: 2), awards),
            p => p.Code == "npc_progression_skill_award_unfulfilled");
    }

    [Fact]
    public void ConflictingMasteryAlias_CannotBackAnAward()
    {
        var after = Actor(3, mastery: 2);
        after["activeSkills"]![0]!["currentMasteryLevel"] = 3;
        Assert.Contains(Evaluate(Actor(1, mastery: 1), after, MasteryAwards(1)),
            p => p.Code == "npc_progression_skill_catalog_invalid");
    }

    [Fact]
    public void Allocation_CannotFundPositiveAwardWithNegativeCharacteristicDelta()
    {
        var after = Actor(2);
        after["characteristics"]!["strength"] = 8;
        after["characteristics"]!["endurance"] = 1;
        var policy = new Contract.CharacteristicPolicy(Contract.CharacteristicMode.AllocatedPoints,
            PointsPerLevel: 3, AllocationKeys: new HashSet<string>(StringComparer.Ordinal) { "strength", "endurance" });
        Assert.Contains(Evaluate(Actor(1), after, policy: policy),
            p => p.Code == "npc_progression_characteristic_delta");
    }

    [Fact]
    public void FixedIncrement_RejectsInt32AwardBudgetOverflow()
    {
        var before = Actor(1, mastery: 1); var after = Actor(3, mastery: 2);
        after["characteristics"]!["strength"] = 4L + 2L * int.MaxValue;
        var policy = new Contract.CharacteristicPolicy(Contract.CharacteristicMode.FixedIncrements,
            new Dictionary<string, int> { ["strength"] = int.MaxValue });
        Assert.Contains(Evaluate(before, after, MasteryAwards(1), policy),
            p => p.Code == "npc_progression_characteristic_authority");
    }

    [Fact]
    public void Allocation_RejectsSubDecimalPrecisionOverspend()
    {
        var before = Actor(1); var after = Actor(2);
        before["characteristics"]!["strength"] = JsonNode.Parse("0");
        after["characteristics"]!["strength"] = JsonNode.Parse("1.00000000000000000000000000001");
        var policy = new Contract.CharacteristicPolicy(Contract.CharacteristicMode.AllocatedPoints,
            PointsPerLevel: 1, AllocationKeys: new HashSet<string>(StringComparer.Ordinal) { "strength" });
        Assert.Contains(Evaluate(before, after, policy: policy),
            p => p.Code == "npc_progression_characteristic_delta");
    }

    [Theory]
    [InlineData("npcId")]
    [InlineData("id")]
    public void ExistingStrictOwnerAlias_CanDischargeActualMasteryAward(string alias)
    {
        var before = Actor(1, mastery: 1); var after = Actor(3, mastery: 2);
        before.Remove("NPCId"); after.Remove("NPCId");
        before[alias] = "npc_source"; after[alias] = "npc_source";
        Assert.Empty(Evaluate(before, after, MasteryAwards(1)));
    }

    [Fact]
    public void ConflictingOwnerAliases_CannotDischargeActualMasteryAward()
    {
        var before = Actor(1, mastery: 1); var after = Actor(3, mastery: 2);
        after["npcId"] = "npc_other";
        Assert.Contains(Evaluate(before, after, MasteryAwards(1)),
            problem => problem.Code == "npc_progression_consequence_owner");
    }

    private static IReadOnlyList<Contract.Problem> Evaluate(JsonObject before, JsonObject after,
        JsonArray? awards = null, Contract.CharacteristicPolicy? policy = null, Contract.ProfileConsequences? profile = null) =>
        Contract.Evaluate(before, after, awards, profile ?? new(true),
            policy ?? new(Contract.CharacteristicMode.None), new HashSet<string>(StringComparer.Ordinal) { "strength", "endurance" });

    private static JsonObject Actor(int level, int? mastery = null) => new()
    {
        ["NPCId"] = "npc_source", ["level"] = level,
        ["characteristics"] = new JsonObject { ["strength"] = 4, ["endurance"] = 2 },
        ["activeSkills"] = mastery is int value ? new JsonArray(Skill("Guard", "skill_guard", value)) : new JsonArray(),
        ["passiveSkills"] = new JsonArray()
    };

    // Existing skill-contract owners validate full profiles; this helper consumes their canonical identity/mastery projection.
    private static JsonObject Skill(string name, string id, int mastery) => new()
    {
        ["skillName"] = name, ["skillId"] = id, ["masteryLevel"] = mastery
    };
    private static JsonArray Awards(string kind, string name, string id, string award) => new(new JsonObject
    {
        ["kind"] = kind, ["skillName"] = name, ["skillId"] = id, ["award"] = award
    });
    private static JsonArray MasteryAwards(int count)
    {
        var result = new JsonArray();
        for (var index = 0; index < count; index++) result.Add(Awards("active", "Guard", "skill_guard", "masteryIncrease")[0]!.DeepClone());
        return result;
    }
}
