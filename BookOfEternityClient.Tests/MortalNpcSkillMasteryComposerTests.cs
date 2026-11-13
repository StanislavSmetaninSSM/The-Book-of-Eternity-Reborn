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
