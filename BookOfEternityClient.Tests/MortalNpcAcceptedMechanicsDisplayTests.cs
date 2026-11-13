using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalNpcAcceptedMechanicsDisplayTests
{
    [Fact]
    public void SharedDrilldown_LargeFiniteParsedCharacteristicIsNotNarrowedToFloat()
    {
        var actor = ProgressionActor("standard");
        actor["characteristics"] = JsonNode.Parse("""{"setting_defined_scale":1e100,"snapshotHash":123}""");
        var projection = MortalNpcAcceptedMechanicsProjection.Build(actor);
        var row = Assert.Single(projection.CharacteristicRows);
        Assert.Equal("setting_defined_scale", row.Key);
        Assert.Equal("1e100", row.Value);
    }

    [Fact]
    public void ConsoleAdditionalScalars_ExcludeMalformedProfileAndManifestHash()
    {
        using var actor = JsonDocument.Parse("""
            {"progressionProfile":"PRIVATE_SENTINEL_profile","manifestPayloadHash":"PRIVATE_SENTINEL_manifest",
             "source":"PRIVATE_SENTINEL_source","custom_public_note":"Visible detail"}
            """);
        var text = string.Join("\n", ExplorerMode.BuildNpcAdditionalScalarLines(actor.RootElement));
        Assert.DoesNotContain("PRIVATE_SENTINEL", text, StringComparison.Ordinal);
        Assert.Contains("Visible detail", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("standard", "Стандартный")]
    [InlineData("table", "Табличный")]
    [InlineData("none", "Без развития")]
    public void SharedDrilldown_ExplicitProfileAndSettingCharacteristicsArePublic(string mode, string label)
    {
        var actor = ProgressionActor(mode);
        var before = actor.ToJsonString();
        var projection = NpcDetailSectionProjection.Build(actor, new NpcDetailSectionDocuments());
        var mechanics = Assert.Single(projection.Sections, section => section.Id == "mechanics");
        var text = JsonSerializer.Serialize(mechanics.Blocks, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        Assert.Contains("Профиль развития", text, StringComparison.Ordinal);
        Assert.Contains(label, text, StringComparison.Ordinal);
        Assert.Contains("setting_defined_focus", text, StringComparison.Ordinal);
        Assert.Contains("6.25", text, StringComparison.Ordinal);
        Assert.Contains("setting_defined_resolve", text, StringComparison.Ordinal);
        if (mode == "none")
        {
            Assert.DoesNotContain("награда за каждые 2", text, StringComparison.Ordinal);
            Assert.DoesNotContain("правила текущего мира", text, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("1 награда за каждые 2 полученных уровня", text, StringComparison.Ordinal);
            Assert.Contains("правила текущего мира", text, StringComparison.Ordinal);
            Assert.Contains("Излишек опыта: отбрасывается", text, StringComparison.Ordinal);
            if (mode == "standard")
            {
                Assert.Contains("Предел уровня: 5", text, StringComparison.Ordinal);
                Assert.Contains("Потеря опыта: разрешена", text, StringComparison.Ordinal);
            }
            else
            {
                Assert.Contains("Предел уровня: 4", text, StringComparison.Ordinal);
                Assert.Contains("Пороги: 10, 20, 30", text, StringComparison.Ordinal);
            }
        }
        Assert.DoesNotContain("PRIVATE_SENTINEL", text, StringComparison.Ordinal);
        Assert.Equal(before, actor.ToJsonString());
    }

    [Fact]
    public void SharedDrilldown_ProfileMetadataAndNestedCharacteristicObjectsCannotLeak()
    {
        var actor = ProgressionActor("standard");
        actor["progressionProfile"]!["source"] = JsonNode.Parse("""{"requestId":"PRIVATE_SENTINEL_profile"}""");
        actor["progressionProfile"]!["consequences"]!["receipt"] = "PRIVATE_SENTINEL_consequence_receipt";
        actor["characteristics"]!["nested_authority"] = JsonNode.Parse("""{"snapshotToken":"PRIVATE_SENTINEL_characteristic"}""");
        var before = actor.ToJsonString();
        var projection = NpcDetailSectionProjection.Build(actor, new NpcDetailSectionDocuments(
            Skills: JsonNode.Parse("""{"NPCActiveSkillChanges":[{"NPCId":"npc_accepted","skillChanges":[{"skillName":"UNACCEPTED_SKILL"}]}]}""")));
        var text = JsonSerializer.Serialize(projection, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        Assert.Contains("setting_defined_focus", text, StringComparison.Ordinal);
        Assert.DoesNotContain("nested_authority", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_SENTINEL", text, StringComparison.Ordinal);
        Assert.DoesNotContain("UNACCEPTED_SKILL", text, StringComparison.Ordinal);
        Assert.Equal(before, actor.ToJsonString());
    }

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
        Assert.Equal(2, skill.Data["cooldownTurns"]!.GetValue<int>());
        Assert.Equal(1, skill.Data["actionCost"]!.GetValue<int>());
        Assert.Equal(2.5m, skill.Data["energyCost"]!.GetValue<decimal>());
        Assert.Equal(7, skill.MasteryLevel);
        Assert.Equal(23, skill.CurrentMasteryProgress);
        Assert.Equal(91, skill.MasteryProgressNeeded);
        Assert.Equal(passive ? 12 : (int?)null, skill.MaxMasteryLevel);
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

    [Fact]
    public void AcceptedProjection_SameNameAcrossKindsAndRejectedSidecarStaySeparate()
    {
        var actor = Actor(false);
        actor["passiveSkills"] = Actor(true)["passiveSkills"]!.DeepClone();
        actor["passiveSkills"]![0]!["masteryLevel"] = 3;
        var projected = MortalNpcAcceptedMechanicsProjection.Build(actor);
        Assert.Equal(7, Assert.Single(projected.Active).MasteryLevel);
        Assert.Equal(3, Assert.Single(projected.Passive).MasteryLevel);
        var sidecar = JsonNode.Parse("""
            {"NPCActiveSkillChanges":[{"NPCId":"npc_accepted","skillChanges":[{"skillName":"UNACCEPTED_SKILL"}]}]}
            """);
        var display = NpcDetailSectionProjection.Build(actor, new NpcDetailSectionDocuments(Skills: sidecar));
        Assert.DoesNotContain("UNACCEPTED_SKILL", JsonSerializer.Serialize(display), StringComparison.Ordinal);
        using var doc = JsonDocument.Parse(actor.ToJsonString());
        Assert.True(ExplorerMode.BuildNpcSkillDisplay(doc, "another_owner", "Accepted NPC").IsEmpty);
    }

    [Theory]
    [InlineData("Fast", "быстрое")]
    [InlineData("Main", "основное")]
    [InlineData("Free", "свободное")]
    public void SharedDrilldown_LocalizesCanonicalActionCost(string cost, string label)
    {
        var actor = Actor(false);
        actor["activeSkills"]![0]!["actionCost"] = cost;
        var projection = NpcDetailSectionProjection.Build(actor, new NpcDetailSectionDocuments());
        var text = JsonSerializer.Serialize(projection, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        Assert.Contains(label, text, StringComparison.Ordinal);
        Assert.DoesNotContain(cost, text, StringComparison.Ordinal);
    }

    private static JsonObject Actor(bool passive)
    {
        var actor = JsonNode.Parse("""
            {"NPCId":"npc_accepted","name":"Accepted NPC","level":2,"experience":17,"experienceForNextLevel":565,
             "source":{"requestId":"PRIVATE_SENTINEL_actor_request"},"snapshotToken":"PRIVATE_SENTINEL_snapshot",
             "progressionReceipt":{"receiptId":"PRIVATE_SENTINEL_receipt"}}
            """)!.AsObject();
        actor[passive ? "passiveSkills" : "activeSkills"] = JsonNode.Parse("""
            [{"skillName":"Fireball","description":"A bright controlled flame","type":"Combat","cooldownTurns":2,"actionCost":1,"energyCost":2.5,
              "masteryLevel":7,"currentMasteryProgress":23,"masteryProgressNeeded":91,
              "source":{"sessionId":"PRIVATE_SENTINEL_skill_session"},"requestId":"PRIVATE_SENTINEL_skill_request",
              "receipt":"PRIVATE_SENTINEL_skill_receipt"}]
            """);
        if (passive) actor["passiveSkills"]![0]!["maxMasteryLevel"] = 12;
        return actor;
    }

    private static JsonObject ProgressionActor(string mode)
    {
        var actor = Actor(false);
        actor["passiveSkills"] = new JsonArray();
        actor["characteristics"] = JsonNode.Parse("""{"setting_defined_focus":6.25,"setting_defined_resolve":0}""");
        actor["progressionProfile"] = JsonNode.Parse(mode switch
        {
            "none" => """{"schemaVersion":1,"mode":"none"}""",
            "table" => """{"schemaVersion":1,"mode":"table","thresholds":[10,20,30],"maxLevel":4,"capOverflow":"discard","consequences":{"skills":{"levelsPerAward":2,"allowedAwards":["newSkill","masteryIncrease"]},"characteristics":{"mode":"currentWorld"}}}""",
            _ => """{"schemaVersion":1,"mode":"standard","allowExperienceLoss":true,"maxLevel":5,"capOverflow":"discard","consequences":{"skills":{"levelsPerAward":2,"allowedAwards":["newSkill","masteryIncrease"]},"characteristics":{"mode":"currentWorld"}}}"""
        });
        if (mode == "table")
        {
            actor["experience"] = 7;
            actor["experienceForNextLevel"] = 20;
        }
        if (mode == "none")
        {
            actor["level"] = 0;
            actor["experience"] = 0;
            actor["experienceForNextLevel"] = 0;
        }
        return actor;
    }
}
