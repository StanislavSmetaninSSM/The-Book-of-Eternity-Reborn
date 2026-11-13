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
            ["progressionProfile"] = new JsonObject { ["schemaVersion"] = 1, ["mode"] = "standard" }
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
}
