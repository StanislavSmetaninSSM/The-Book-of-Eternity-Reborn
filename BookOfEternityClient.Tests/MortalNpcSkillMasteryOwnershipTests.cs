using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Cheap typed tests of the same operation-local name authority used by NpcStateFiles.</summary>
public sealed class MortalNpcSkillMasteryOwnershipTests
{
    [Theory]
    [InlineData(false, "foreign")]
    [InlineData(true, "foreign")]
    [InlineData(false, "orphan")]
    [InlineData(true, "orphan")]
    [InlineData(false, "wrong-kind")]
    [InlineData(true, "wrong-kind")]
    public void UnknownOwnerLocalSkill_HasExactIndexedBoundedDiagnostic(bool passive, string mode)
    {
        var core = mode == "orphan" ? new JsonObject() : Core(mode == "wrong-kind" ? !passive : passive,
            mode == "foreign" ? "npc_a" : "npc_b", "Whirlwind");
        var skills = Mastery(passive, "npc_b", "Whirlwind");
        var issue = Assert.Single(Evaluate(core, skills));
        Assert.Equal("npc_skill_mastery_unknown_owned_skill", issue.Code);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Equal("id:npc_b", issue.Actor);
        Assert.Equal(passive ? "Skills.Passive" : "Skills.Active", issue.Section);
        Assert.Equal("game_state/npcs/npc_skills.json." + MasterySection(passive) + "[0].skillName", issue.FilePath);
        Assert.Equal("Whirlwind", issue.Actual);
        Assert.Contains("npc_core.json", issue.Expected, StringComparison.Ordinal);
        Assert.Contains("skillChanges", issue.Expected, StringComparison.Ordinal);
        Assert.Contains("не меняй владельца", issue.RepairHint, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SameOwnerCurrentCoreAndRepeatedSkillGroups_AreValidWithoutMutation(bool passive)
    {
        var core = Core(passive, "NPC_B", "Whirlwind");
        var skills = Mastery(passive, "npc_b", "Whirlwind", "FIREBALL", "Fireball", "Padded ");
        skills[SkillSection(passive)] = new JsonArray(Group("npc_b", "FIREBALL"), Group("NPC_B", "Fireball", "Padded "));
        var originalCore = core.ToJsonString();
        var originalSkills = skills.ToJsonString();
        Assert.Empty(Evaluate(core, skills));
        Assert.Equal(originalCore, core.ToJsonString());
        Assert.Equal(originalSkills, skills.ToJsonString());
    }

    [Theory]
    [InlineData(false, "fireball")]
    [InlineData(true, "fireball")]
    [InlineData(false, "Fireball ")]
    [InlineData(true, "Fireball ")]
    [InlineData(false, "Fіreball")]
    [InlineData(true, "Fіreball")]
    public void SkillNames_AreOrdinalAndUntrimmed(bool passive, string reference)
    {
        Assert.Single(Evaluate(Core(passive, "npc_b", "Fireball"), Mastery(passive, "npc_b", reference)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TwoOwnersWithSameSkill_DoNotConflict(bool passive)
    {
        var core = Core(passive, "npc_a", "Whirlwind");
        core["UpdateNPCs"]!.AsArray().Add(Core(passive, "npc_b", "Whirlwind")["UpdateNPCs"]![0]!.DeepClone());
        var skills = Mastery(passive, "npc_a", "Whirlwind");
        skills[MasterySection(passive)]!.AsArray().Add(Mastery(passive, "npc_b", "Whirlwind")[MasterySection(passive)]![0]!.DeepClone());
        Assert.Empty(Evaluate(core, skills));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NameFallback_DoesNotOverrideIdOrAliasNameOnlyToId(bool passive)
    {
        var core = Core(passive, "npc_a", "Whirlwind");
        var actor = core["UpdateNPCs"]![0]!.AsObject();
        actor["NPCName"] = "Trainer";
        var skills = Mastery(passive, "npc_b", "Whirlwind");
        var row = skills[MasterySection(passive)]![0]!.AsObject();
        row["NPCName"] = "Trainer";
        Assert.Single(Evaluate(core, skills));
        row.Remove("NPCId");
        Assert.Single(Evaluate(core, skills));
        actor.Remove("NPCId");
        row["NPCName"] = "TRAINER";
        Assert.Empty(Evaluate(core, skills));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MalformedRows_DoNotFabricateOwnershipFindings(bool passive)
    {
        var skills = new JsonObject { [MasterySection(passive)] = new JsonArray(null, 2,
            new JsonObject(), new JsonObject { ["NPCId"] = "npc_b", ["skillName"] = 5 },
            new JsonObject { ["skillName"] = "Whirlwind" },
            new JsonObject { ["NPCId"] = "npc_b", ["skillName"] = " " }) };
        Assert.Empty(Evaluate(new JsonObject(), skills));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingShapeAndDuplicateDiagnostics_RemainIndependent(bool passive)
    {
        var skills = Mastery(passive, "npc_b", "Whirlwind", "Whirlwind");
        skills[MasterySection(passive)]![1]!["newMasteryLevel"] = "invalid";
        using var document = JsonDocument.Parse(skills.ToJsonString());
        var issues = Evaluate(new JsonObject(), skills);
        ValidationService.ValidateNpcSkillMastery(document.RootElement, "game_state/npcs/npc_skills.json", issues,
            MasterySection(passive), passive);
        Assert.Equal(2, issues.Count(i => i.Code == "npc_skill_mastery_unknown_owned_skill"));
        Assert.Single(issues, i => i.Code == "skill_state_duplicate_exact_name");
        Assert.Contains(issues, i => i.FilePath.EndsWith("[1].newMasteryLevel", StringComparison.Ordinal));
    }

    private static List<ValidationIssue> Evaluate(JsonObject core, JsonObject skills)
    {
        using var coreDocument = JsonDocument.Parse(core.ToJsonString());
        using var skillDocument = JsonDocument.Parse(skills.ToJsonString());
        var authority = new ValidationService.NpcSkillMasteryOwnership();
        authority.CollectCore(coreDocument.RootElement);
        var issues = new List<ValidationIssue>();
        authority.ValidateSkills(skillDocument.RootElement, "game_state/npcs/npc_skills.json", issues);
        return issues;
    }
    private static string MasterySection(bool passive) => passive ? "NPCPassiveSkillMasteryChanges" : "NPCSkillMasteryChanges";
    private static string SkillSection(bool passive) => passive ? "NPCPassiveSkillChanges" : "NPCActiveSkillChanges";
    private static JsonObject Core(bool passive, string owner, params string[] names) => new()
    {
        ["UpdateNPCs"] = new JsonArray(new JsonObject
        { ["NPCId"] = owner, [passive ? "passiveSkills" : "activeSkills"] = new JsonArray(names.Select(Skill).ToArray()) })
    };
    private static JsonObject Skill(string name) => new() { ["skillName"] = name };
    private static JsonObject Group(string owner, params string[] names) => new()
    { ["NPCId"] = owner, ["skillChanges"] = new JsonArray(names.Select(Skill).ToArray()) };
    private static JsonObject Mastery(bool passive, string owner, params string[] names) => new()
    {
        [MasterySection(passive)] = new JsonArray(names.Select(name => (JsonNode)new JsonObject
        { ["NPCId"] = owner, ["skillName"] = name, ["newMasteryLevel"] = 1,
            ["newCurrentMasteryProgress"] = 0, ["newMasteryProgressNeeded"] = 100 }).ToArray())
    };
}
