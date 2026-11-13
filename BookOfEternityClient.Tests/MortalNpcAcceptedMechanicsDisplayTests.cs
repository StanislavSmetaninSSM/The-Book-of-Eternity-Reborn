using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalNpcAcceptedMechanicsDisplayTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConsoleSkillDisplay_SelectedAcceptedActorRetainsMasteryWithoutSidecar(bool passive)
    {
        var actor = Actor(passive);
        var before = actor.ToJsonString();
        using var document = JsonDocument.Parse(before);
        var display = ExplorerMode.BuildNpcSkillDisplay(document, "npc_accepted", "Accepted NPC");
        var skill = Assert.Single(passive ? display.Passive : display.Active);
        Assert.Equal("Fireball", skill.Data["skillName"]!.GetValue<string>());
        Assert.Equal(7, skill.MasteryLevel);
        Assert.Equal(23, skill.CurrentMasteryProgress);
        Assert.Equal(91, skill.MasteryProgressNeeded);
        Assert.Equal(12, skill.MaxMasteryLevel);
        Assert.DoesNotContain("PRIVATE_SENTINEL", skill.Data.ToJsonString(), StringComparison.Ordinal);
        Assert.Equal(before, actor.ToJsonString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedDrilldown_AcceptedMasteryAndXpSurviveConsumedSidecar(bool emptySidecar)
    {
        var actor = Actor(false);
        var before = actor.ToJsonString();
        var projection = NpcDetailSectionProjection.Build(actor,
            new NpcDetailSectionDocuments(Skills: emptySidecar ? new JsonObject() : null));
        var mechanics = Assert.Single(projection.Sections, section => section.Id == "mechanics");
        var text = JsonSerializer.Serialize(mechanics.Blocks, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        Assert.Contains("Fireball", text, StringComparison.Ordinal);
        Assert.Contains("23/91", text, StringComparison.Ordinal);
        Assert.Contains("17/565", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_SENTINEL", text, StringComparison.Ordinal);
        Assert.Equal(before, actor.ToJsonString());
    }

    private static JsonObject Actor(bool passive)
    {
        var actor = JsonNode.Parse("""
            {"NPCId":"npc_accepted","name":"Accepted NPC","level":2,"experience":17,"experienceForNextLevel":565,
             "source":{"requestId":"PRIVATE_SENTINEL_actor_request"},"snapshotToken":"PRIVATE_SENTINEL_snapshot",
             "progressionReceipt":{"receiptId":"PRIVATE_SENTINEL_receipt"}}
            """)!.AsObject();
        actor[passive ? "passiveSkills" : "activeSkills"] = JsonNode.Parse("""
            [{"skillName":"Fireball","description":"A bright controlled flame","type":"Combat","cooldown":2,
              "masteryLevel":7,"currentMasteryProgress":23,"masteryProgressNeeded":91,"maxMasteryLevel":12,
              "source":{"sessionId":"PRIVATE_SENTINEL_skill_session"},"requestId":"PRIVATE_SENTINEL_skill_request",
              "receipt":"PRIVATE_SENTINEL_skill_receipt"}]
            """);
        return actor;
    }
}
