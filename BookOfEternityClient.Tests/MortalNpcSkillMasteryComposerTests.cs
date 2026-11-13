using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalNpcSkillMasteryComposerTests
{
    [Fact]
    public void ComposeWithMastery_UpdatesActiveAndPassiveMirrorsWithoutMutatingInputs()
    {
        var before = Root();
        var original = before.ToJsonString();
        var result = Run(before, [], [], [Active()], [new("npc_001", "Guard", "guard", 3, NewMaxMasteryLevel: 8)], out var issues);
        Assert.Empty(issues);
        Assert.NotNull(result);
        var actor = result["NPCsInScene"]![0]!;
        Assert.Equal(3, actor["activeSkills"]![0]!["masteryLevel"]!.GetValue<int>());
        Assert.Equal(7, actor["activeSkills"]![0]!["currentMasteryProgress"]!.GetValue<int>());
        Assert.Equal(40, actor["activeSkills"]![0]!["masteryProgressNeeded"]!.GetValue<int>());
        Assert.Equal(3, actor["passiveSkills"]![0]!["masteryLevel"]!.GetValue<int>());
        Assert.Equal(8, actor["passiveSkills"]![0]!["maxMasteryLevel"]!.GetValue<int>());
        Assert.Equal("preserve", actor["activeSkills"]![0]!["description"]!.GetValue<string>());
        Assert.Equal("Canonical", actor["name"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(actor, result["UpdateNPCs"]![0]));
        Assert.Equal(original, before.ToJsonString());
    }

    [Fact]
    public void ComposeWithMastery_AppliesProfileBeforeMastery()
    {
        var profile = new JsonObject { ["skillName"] = "Frost", ["skillId"] = "frost", ["masteryLevel"] = 1 };
        var command = new MortalNpcSkillComposer.Command("npc_001", new JsonArray(profile), null);
        var original = command.SkillChanges!.ToJsonString();
        var result = Run(Root(), [command], [], [Active() with { SkillName = "Frost", SkillId = "frost" }], [], out var issues);
        Assert.Empty(issues);
        Assert.NotNull(result);
        var frost = result["NPCsInScene"]![0]!["activeSkills"]!.AsArray().Single(x => x!["skillName"]!.GetValue<string>() == "Frost");
        Assert.Equal(3, frost!["masteryLevel"]!.GetValue<int>());
        Assert.Equal(original, command.SkillChanges.ToJsonString());
    }

    [Fact]
    public void ComposeWithMastery_RemovedSkillCannotBeResurrected()
    {
        var before = Root();
        var original = before.ToJsonString();
        var result = Run(before, [new("npc_001", null, new JsonArray("Spark"))], [], [Active()], [], out var issues);
        Assert.Null(result);
        Assert.NotEmpty(issues);
        Assert.Equal(original, before.ToJsonString());
    }

    [Theory]
    [InlineData("npc_other", "Spark", "spark")]
    [InlineData("npc_001", "spark", "spark")]
    [InlineData("npc_001", "Spаrk", "spark")] // Cyrillic a is not the exact canonical name.
    [InlineData("npc_001", "Spark", "other")]
    [InlineData("npc_001", "Guard", "guard")] // Wrong kind: Guard is passive.
    public void ComposeWithMastery_RejectsWrongOwnerKindNameOrId(string owner, string name, string id)
    {
        var result = Run(Root(), [], [], [Active() with { NpcId = owner, SkillName = name, SkillId = id }], [], out var issues);
        Assert.Null(result);
        Assert.NotEmpty(issues);
    }

    [Theory]
    [InlineData("Spark")]
    [InlineData("Spаrk")]
    public void ComposeWithMastery_RejectsAmbiguousOrConfusableCatalog(string otherName)
    {
        var before = Root();
        foreach (var section in new[] { "NPCsInScene", "UpdateNPCs" })
            before[section]![0]!["activeSkills"]!.AsArray().Add(new JsonObject { ["skillName"] = otherName, ["skillId"] = "other", ["masteryLevel"] = 1 });
        var result = Run(before, [], [], [Active()], [], out var issues);
        Assert.Null(result);
        Assert.NotEmpty(issues);
    }

    [Fact]
    public void ComposeWithMastery_PassiveWithoutMaxPreservesExistingMaximum()
    {
        var result = Run(Root(), [], [], [], [new("npc_001", "Guard", null, 3)], out var issues);
        Assert.Empty(issues);
        Assert.NotNull(result);
        Assert.Equal(3, result["NPCsInScene"]![0]!["passiveSkills"]![0]!["masteryLevel"]!.GetValue<int>());
        Assert.Equal(5, result["NPCsInScene"]![0]!["passiveSkills"]![0]!["maxMasteryLevel"]!.GetValue<int>());
    }

    [Fact]
    public void ComposeWithMastery_DuplicateClaimsRefuseWholeCandidate()
    {
        var before = Root();
        var original = before.ToJsonString();
        var result = Run(before, [], [], [Active(), Active()], [], out var issues);
        Assert.Null(result);
        Assert.NotEmpty(issues);
        Assert.Equal(original, before.ToJsonString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ComposeWithMastery_MissingRequiredActiveValueRefuses(bool missingProgress)
    {
        var command = missingProgress ? Active() with { NewCurrentMasteryProgress = null }
            : Active() with { NewMasteryProgressNeeded = null };
        var result = Run(Root(), [], [], [command], [], out var issues);
        Assert.Null(result);
        Assert.NotEmpty(issues);
    }

    [Fact]
    public void ComposeWithMastery_MalformedCanonicalIdRefusesWithoutThrowing()
    {
        var before = Root();
        foreach (var section in new[] { "NPCsInScene", "UpdateNPCs" })
            before[section]![0]!["activeSkills"]![0]!["skillId"] = 7;
        var result = Run(before, [], [], [Active()], [], out var issues);
        Assert.Null(result);
        Assert.NotEmpty(issues);
    }

    [Fact]
    public void ComposeWithMastery_DivergentMirrorsRefuseBeforeMastery()
    {
        var before = Root();
        before["UpdateNPCs"]![0]!["name"] = "Diverged";
        var result = Run(before, [], [], [Active()], [], out var issues);
        Assert.Null(result);
        Assert.NotEmpty(issues);
    }

    [Fact]
    public void Consistency_RetainedMasteryAliasMovesWithCanonicalLevel()
    {
        var before = Root();
        foreach (var section in new[] { "NPCsInScene", "UpdateNPCs" })
            before[section]![0]!["activeSkills"]![0]!["currentMasteryLevel"] = 1;
        var result = Run(before, [], [], [Active()], [], out var issues);
        Assert.Empty(issues);
        Assert.NotNull(result);
        Assert.Equal(3, result["NPCsInScene"]![0]!["activeSkills"]![0]!["currentMasteryLevel"]!.GetValue<int>());
    }

    [Fact]
    public void Consistency_ContradictoryMasteryAliasesRefuse()
    {
        var before = Root();
        foreach (var section in new[] { "NPCsInScene", "UpdateNPCs" })
            before[section]![0]!["activeSkills"]![0]!["currentMasteryLevel"] = 2;
        Assert.Null(Run(before, [], [], [Active()], [], out var issues));
        Assert.NotEmpty(issues);
    }

    [Fact]
    public void Consistency_ExactIdAliasResolves()
    {
        var before = Root();
        foreach (var section in new[] { "NPCsInScene", "UpdateNPCs" })
        {
            var skill = before[section]![0]!["activeSkills"]![0]!.AsObject();
            skill.Remove("skillId");
            skill["id"] = "spark";
        }
        var result = Run(before, [], [], [Active()], [], out var issues);
        Assert.Empty(issues);
        Assert.NotNull(result);
        Assert.Equal(3, result["NPCsInScene"]![0]!["activeSkills"]![0]!["masteryLevel"]!.GetValue<int>());
    }

    [Fact]
    public void Consistency_ContradictoryIdAliasesRefuse()
    {
        var before = Root();
        foreach (var section in new[] { "NPCsInScene", "UpdateNPCs" })
            before[section]![0]!["activeSkills"]![0]!["id"] = "other";
        Assert.Null(Run(before, [], [], [Active()], [], out var issues));
        Assert.NotEmpty(issues);
    }

    [Theory]
    [InlineData(0, 0, 20)]
    [InlineData(2, -1, 20)]
    [InlineData(2, 0, 0)]
    public void Consistency_InvalidActiveBoundsRefuse(int level, int progress, int needed)
    {
        var command = Active() with { NewMasteryLevel = level, NewCurrentMasteryProgress = progress, NewMasteryProgressNeeded = needed };
        Assert.Null(Run(Root(), [], [], [command], [], out var issues));
        Assert.NotEmpty(issues);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(4)]
    [InlineData(0)]
    public void Consistency_PassiveResultCannotExceedPositiveCap(int? maximum)
    {
        Assert.Null(Run(Root(), [], [], [], [new("npc_001", "Guard", "guard", 6, NewMaxMasteryLevel: maximum)], out var issues));
        Assert.NotEmpty(issues);
    }

    [Fact]
    public void Consistency_ActiveCommandCannotIntroduceCap()
    {
        Assert.Null(Run(Root(), [], [], [Active() with { NewMaxMasteryLevel = 10 }], [], out var issues));
        Assert.NotEmpty(issues);
    }

    [Fact]
    public void ProfileConsistency_MalformedPresentIdentityRefusesWithoutMastery()
    {
        var profile = new JsonObject { ["skillName"] = "Frost", ["skillId"] = 9, ["masteryLevel"] = 1, ["currentMasteryProgress"] = 0, ["masteryProgressNeeded"] = 20 };
        Assert.Null(Run(Root(), [new("npc_001", new JsonArray(profile), null)], [], [], [], out var issues));
        Assert.NotEmpty(issues);
    }

    [Fact]
    public void ProfileConsistency_PassiveOverCapRefusesWithoutMastery()
    {
        var profile = new JsonObject { ["skillName"] = "Guard", ["skillId"] = "guard", ["masteryLevel"] = 6, ["maxMasteryLevel"] = 5 };
        Assert.Null(Run(Root(), [], [new("npc_001", new JsonArray(profile), null)], [], [], out var issues));
        Assert.NotEmpty(issues);
    }

    [Fact]
    public void ProfileConsistency_ContradictoryAliasesRefuseWithoutMastery()
    {
        var profile = new JsonObject { ["skillName"] = "Frost", ["skillId"] = "frost", ["id"] = "other", ["masteryLevel"] = 1, ["currentMasteryProgress"] = 0, ["masteryProgressNeeded"] = 20 };
        Assert.Null(Run(Root(), [new("npc_001", new JsonArray(profile), null)], [], [], [], out var issues));
        Assert.NotEmpty(issues);
    }

    private static MortalNpcSkillComposer.MasteryCommand Active() => new("npc_001", "Spark", "spark", 3, 7, 40);

    private static JsonObject? Run(JsonObject before, IReadOnlyList<MortalNpcSkillComposer.Command> active,
        IReadOnlyList<MortalNpcSkillComposer.Command> passive, IReadOnlyList<MortalNpcSkillComposer.MasteryCommand> activeMastery,
        IReadOnlyList<MortalNpcSkillComposer.MasteryCommand> passiveMastery, out List<ValidationIssue> issues)
    {
        issues = [];
        return MortalNpcSkillComposer.ComposeWithMastery(before, active, passive, activeMastery, passiveMastery, issues,
            (path, code, expected, actual) => new ValidationIssue(path, IssueSeverity.Error, "Rejected", code: code, expected: expected, actual: actual));
    }

    private static JsonObject Root()
    {
        var actor = new JsonObject
        {
            ["NPCId"] = "npc_001", ["name"] = "Canonical",
            ["activeSkills"] = new JsonArray(new JsonObject { ["skillName"] = "Spark", ["skillId"] = "spark", ["masteryLevel"] = 1,
                ["currentMasteryProgress"] = 0, ["masteryProgressNeeded"] = 20, ["description"] = "preserve" }),
            ["passiveSkills"] = new JsonArray(new JsonObject { ["skillName"] = "Guard", ["skillId"] = "guard", ["masteryLevel"] = 1, ["maxMasteryLevel"] = 5 })
        };
        return new JsonObject { ["NPCsInScene"] = new JsonArray(actor), ["UpdateNPCs"] = new JsonArray(actor.DeepClone()) };
    }
}
