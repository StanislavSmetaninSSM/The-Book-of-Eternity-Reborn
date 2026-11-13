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
        Assert.NotEmpty(result.Issues);
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
        Assert.Contains(result.Issues, issue => issue.FilePath.Contains("rarity", StringComparison.Ordinal));
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
        ["maxMasteryLevel"] = 5, ["knowledgeDomain"] = "Archives",
        ["unlockedActiveSkillsCount"] = 0, ["maxUnlockableActiveSkills"] = 2
    };

    private static IEnumerable<JsonObject> Skills(JsonObject root) =>
        root["NPCsInScene"]![0]!["passiveSkills"]!.AsArray().OfType<JsonObject>();
}
