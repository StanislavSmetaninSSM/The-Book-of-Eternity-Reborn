using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalNpcSkillComposerTests
{
    [Fact]
    public void Compose_NameBasedUpdatePreservesMirrorsExportsAndInputs()
    {
        var before = Root(new JsonObject { ["skillName"] = "Spark", ["masteryLevel"] = 1 });
        var command = new MortalNpcSkillComposer.Command("npc_001",
            new JsonArray(new JsonObject { ["skillName"] = "Spark", ["masteryLevel"] = 2 }), null);
        var original = before.ToJsonString();
        var commandOriginal = command.SkillChanges!.ToJsonString();
        var old = new Dictionary<string, JsonObject>();
        var after = new Dictionary<string, JsonObject>();
        var touched = new HashSet<string>();
        var issues = new List<ValidationIssue>();

        var result = MortalNpcSkillComposer.Compose(before, [command], [], old, after, touched, issues, Issue);

        Assert.NotNull(result);
        Assert.Empty(issues);
        Assert.Equal(original, before.ToJsonString());
        Assert.Equal(commandOriginal, command.SkillChanges.ToJsonString());
        Assert.Equal(result["NPCsInScene"]![0]!.ToJsonString(), result["UpdateNPCs"]![0]!.ToJsonString());
        Assert.Equal(2, result["NPCsInScene"]![0]!["activeSkills"]![0]!["masteryLevel"]!.GetValue<int>());
        Assert.Equal(1, old["npc:npc_001:active"]["activeSkills"]![0]!["masteryLevel"]!.GetValue<int>());
        Assert.Equal(2, after["npc:npc_001:active"]["activeSkills"]![0]!["masteryLevel"]!.GetValue<int>());
        Assert.Equal("npc:npc_001", Assert.Single(touched));
    }

    [Fact]
    public void Compose_ExactIdFallbackPreservesExistingOwnerDisambiguation()
    {
        var before = Root(new JsonObject
        {
            ["skillId"] = "skill_exact", ["skillName"] = "Shared name", ["displayName"] = "Before"
        });
        foreach (var section in new[] { "NPCsInScene", "UpdateNPCs" })
            before[section]![0]!["activeSkills"]!.AsArray().Add(new JsonObject
            {
                ["skillId"] = "skill_other", ["skillName"] = "Shared name", ["displayName"] = "Untouched"
            });
        var command = new MortalNpcSkillComposer.Command("npc_001", new JsonArray(new JsonObject
        {
            ["skillId"] = "skill_exact", ["skillName"] = "Shared name", ["displayName"] = "After"
        }), null);
        // Prove the ordinary primitive refuses this input, so success below exercises
        // the existing exact-ID-only fallback rather than the ordinary update route.
        Assert.Throws<InvalidDataException>(() => EffectAcceptedTurnInputComposer.ComposeSkillAcceptedRoot(
            new JsonObject { ["activeSkillChanges"] = before["NPCsInScene"]![0]!["activeSkills"]!.DeepClone() },
            new JsonObject { ["activeSkillChanges"] = command.SkillChanges!.DeepClone() },
            "activeSkillChanges", "removeActiveSkills"));
        var issues = new List<ValidationIssue>();

        var result = MortalNpcSkillComposer.Compose(before, [command], [], new Dictionary<string, JsonObject>(),
            new Dictionary<string, JsonObject>(), new HashSet<string>(), issues, Issue);

        Assert.Empty(issues);
        Assert.NotNull(result);
        Assert.Equal("After", result["NPCsInScene"]![0]!["activeSkills"]![0]!["displayName"]!.GetValue<string>());
        Assert.Equal("Untouched", result["NPCsInScene"]![0]!["activeSkills"]![1]!["displayName"]!.GetValue<string>());
        Assert.Equal("Before", before["NPCsInScene"]![0]!["activeSkills"]![0]!["displayName"]!.GetValue<string>());
    }

    [Fact]
    public void Compose_DivergentMirrorsRetainOriginalRefusalCodeAndNoExports()
    {
        var before = Root(new JsonObject { ["skillName"] = "Spark" });
        before["UpdateNPCs"]![0]!["name"] = "Different mirror";
        var old = new Dictionary<string, JsonObject>();
        var after = new Dictionary<string, JsonObject>();
        var touched = new HashSet<string>();
        var issues = new List<ValidationIssue>();

        var result = MortalNpcSkillComposer.Compose(before, [], [], old, after, touched, issues, Issue);

        Assert.Null(result);
        var issue = Assert.Single(issues);
        Assert.Equal("mortal_wound_treatment_publication_npc_selector_ambiguous", issue.Code);
        Assert.Equal("game_state/npcs/npc_core.json", issue.FilePath);
        Assert.Empty(old);
        Assert.Empty(after);
        Assert.Empty(touched);
    }

    [Fact]
    public void Compose_PreexistingIssueRefusesWithoutComposingCommands()
    {
        var before = Root(new JsonObject { ["skillName"] = "Spark" });
        var existing = Issue("existing", "existing_error", "valid", "invalid");
        var issues = new List<ValidationIssue> { existing };
        var touched = new HashSet<string>();
        var result = MortalNpcSkillComposer.Compose(before, [], [], new Dictionary<string, JsonObject>(),
            new Dictionary<string, JsonObject>(), touched, issues, Issue);
        Assert.Null(result);
        Assert.Same(existing, Assert.Single(issues));
        Assert.Empty(touched);
    }

    private static JsonObject Root(JsonObject skill)
    {
        var actor = new JsonObject
        {
            ["NPCId"] = "npc_001", ["name"] = "Canonical NPC",
            ["activeSkills"] = new JsonArray(skill), ["passiveSkills"] = new JsonArray()
        };
        return new JsonObject
        {
            ["NPCsInScene"] = new JsonArray(actor),
            ["UpdateNPCs"] = new JsonArray(actor.DeepClone())
        };
    }

    private static ValidationIssue Issue(string path, string code, string expected, string actual) =>
        new(path, IssueSeverity.Error, "Composition rejected.", code: code, expected: expected, actual: actual);
}
