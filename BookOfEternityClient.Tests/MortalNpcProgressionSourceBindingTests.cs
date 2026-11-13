using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalNpcProgressionSourceBindingTests
{
    [Fact]
    public void ExistingProgression_WithoutTrustedTurnContext_IsRejected()
    {
        var actor = JsonNode.Parse("""
            {"NPCId":"npc_source","level":1,"experience":99,"experienceForNextLevel":100,
             "progressionProfile":{"schemaVersion":1,"mode":"standard"}}
            """)!.AsObject();
        var before = new JsonObject { ["NPCsInScene"] = new JsonArray(actor) };
        var current = before.DeepClone().AsObject();
        current["NPCCoreChanges"] = JsonNode.Parse("""
            [{"NPCId":"npc_source","reason":"Earned two XP",
              "progression":{"experienceDelta":2,"level":2,"experience":1,"experienceForNextLevel":565}}]
            """);
        var authority = new NpcCoreChangesContract.Authority(
            new(StringComparer.Ordinal), new(StringComparer.Ordinal),
            new(StringComparer.Ordinal), new(StringComparer.Ordinal));

        var evaluated = NpcCoreChangesContract.Evaluate(current, before, authority,
            ValidationService.ValidateNpcCoreFateCardsAgainstProductionContract, detectDirectMutations: true);

        Assert.False(evaluated.CanApply);
    }
}
