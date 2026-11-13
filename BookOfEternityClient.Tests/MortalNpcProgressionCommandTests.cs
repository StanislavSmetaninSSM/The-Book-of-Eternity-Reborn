using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalNpcProgressionCommandTests
{
    [Fact]
    public void ExistingCommand_ImpossibleAbsoluteLevelWithoutDelta_IsRejected()
    {
        var actor = new JsonObject
        {
            ["NPCId"] = "npc_progression", ["level"] = 1, ["experience"] = 10,
            ["experienceForNextLevel"] = 100,
            ["progressionProfile"] = new JsonObject { ["schemaVersion"] = 1, ["mode"] = "standard", ["consequences"] = JsonNode.Parse("""{"skills":{"levelsPerAward":2,"allowedAwards":["newSkill","masteryIncrease"]},"characteristics":{"mode":"currentWorld"}}""") }
        };
        var before = new JsonObject { ["NPCsInScene"] = new JsonArray(actor) };
        var current = before.DeepClone().AsObject();
        current["NPCCoreChanges"] = new JsonArray(new JsonObject
        {
            ["NPCId"] = "npc_progression", ["reason"] = "An impossible level jump",
            ["progression"] = new JsonObject
            {
                ["level"] = 99, ["experience"] = 10, ["experienceForNextLevel"] = 100
            }
        });
        var authority = new NpcCoreChangesContract.Authority(
            new(StringComparer.Ordinal), new(StringComparer.Ordinal),
            new(StringComparer.Ordinal), new(StringComparer.Ordinal));
        var result = NpcCoreChangesContract.Evaluate(current, before, authority,
            ValidationService.ValidateNpcCoreFateCardsAgainstProductionContract, detectDirectMutations: true);
        Assert.Contains(result.Issues, issue => issue.Code == "npc_core_changes_progression_invalid");
        Assert.False(result.CanApply);
    }
    [Fact]
    public void ExistingCommand_ExactBoundaryAppliesTupleWithoutPersistingDelta()
    {
        var actor = new JsonObject
        {
            ["NPCId"] = "npc_progression", ["level"] = 1, ["experience"] = 99,
            ["experienceForNextLevel"] = 100,
            ["progressionProfile"] = new JsonObject { ["schemaVersion"] = 1, ["mode"] = "standard", ["consequences"] = JsonNode.Parse("""{"skills":{"levelsPerAward":2,"allowedAwards":["newSkill","masteryIncrease"]},"characteristics":{"mode":"currentWorld"}}""") }
        };
        var before = new JsonObject { ["NPCsInScene"] = new JsonArray(actor) };
        var beforeBytes = before.ToJsonString();
        var current = before.DeepClone().AsObject();
        current["UpdateNPCs"] = new JsonArray(actor.DeepClone());
        current["NPCCoreChanges"] = new JsonArray(new JsonObject
        {
            ["NPCId"] = "npc_progression", ["reason"] = "Two earned experience points",
            ["progression"] = new JsonObject
            {
                ["source"] = new JsonObject { ["sessionId"] = "session_p1", ["requestId"] = "request_p1", ["turnNumber"] = 1 },
                ["experienceDelta"] = 2, ["level"] = 2,
                ["experience"] = 1, ["experienceForNextLevel"] = 565
            }
        });
        var authority = new NpcCoreChangesContract.Authority(
            new(StringComparer.Ordinal), new(StringComparer.Ordinal),
            new(StringComparer.Ordinal), new(StringComparer.Ordinal))
        { ProgressionSource = new("session_p1", "request_p1", 1, "private_snapshot", "mortal_world") };
        var result = NpcCoreChangesContract.Evaluate(current, before, authority,
            ValidationService.ValidateNpcCoreFateCardsAgainstProductionContract, detectDirectMutations: true);
        Assert.Empty(result.Issues);
        Assert.True(result.CanApply);
        NpcCoreChangesContract.Apply(current, result);
        Assert.False(current.ContainsKey("NPCCoreChanges"));
        foreach (var section in new[] { "NPCsInScene", "UpdateNPCs" })
        {
            var applied = current[section]![0]!.AsObject();
            Assert.Equal(2, applied["level"]!.GetValue<int>());
            Assert.Equal(1, applied["experience"]!.GetValue<int>());
            Assert.Equal(565, applied["experienceForNextLevel"]!.GetValue<int>());
            Assert.False(applied.ContainsKey("experienceDelta"));
            Assert.Equal(actor["progressionProfile"]!.ToJsonString(), applied["progressionProfile"]!.ToJsonString());
        }
        Assert.Equal(beforeBytes, before.ToJsonString());
    }

    [Fact]
    public void WorkedExample_UsesTheActualNumericContract()
    {
        var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", "E_CLI_Mortal_Npc_Progression.txt"));
        var blocks = System.Text.RegularExpressions.Regex.Matches(text, @"```json\s*([\s\S]*?)```");
        Assert.Equal(2, blocks.Count);
        var actor = JsonNode.Parse(blocks[0].Groups[1].Value)!.AsObject();
        var response = JsonNode.Parse(blocks[1].Groups[1].Value)!.AsObject();
        var command = Assert.Single(response["NPCCoreChanges"]!.AsArray()).AsObject();
        Assert.Equal(actor["NPCId"]!.GetValue<string>(), command["NPCId"]!.GetValue<string>());
        var result = MortalNpcProgressionContract.Evaluate(actor, command["progression"]!.AsObject());
        Assert.True(result.IsValid, string.Join("; ", result.Problems));
        Assert.Equal(new MortalNpcProgressionContract.ProgressionTuple(2, 0, 565), result.Value);
    }

}
