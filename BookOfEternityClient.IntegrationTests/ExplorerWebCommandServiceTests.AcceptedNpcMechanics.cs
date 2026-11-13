using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.WebUi;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWebCommandServiceTests
{
    [Theory]
    [InlineData("/npcs", "standard", "Стандартный", "17/565")]
    [InlineData("/npc section npc_accepted mechanics", "standard", "Стандартный", "17/565")]
    [InlineData("/npcs", "table", "Табличный", "7/20")]
    [InlineData("/npc section npc_accepted mechanics", "table", "Табличный", "7/20")]
    [InlineData("/npcs", "none", "Без развития", "0/0")]
    [InlineData("/npc section npc_accepted mechanics", "none", "Без развития", "0/0")]
    public async Task ExecuteAsync_AcceptedNpcMechanics_IgnoresPendingSidecarAndPrivateFields(
        string command, string mode, string profileLabel, string experience)
    {
        var core = JsonNode.Parse("""
            {"NPCsInScene":[{"NPCId":"npc_accepted","name":"Accepted NPC","level":2,"experience":17,"experienceForNextLevel":565,
              "snapshotToken":"PRIVATE_SENTINEL_snapshot","requestId":"PRIVATE_SENTINEL_request",
              "progressionReceipt":{"source":{"sessionId":"PRIVATE_SENTINEL_nested_source"}},
              "activeSkills":[{"skillName":"Fireball","masteryLevel":7,"currentMasteryProgress":23,
                "masteryProgressNeeded":91,"actionCost":"Main","receipt":"PRIVATE_SENTINEL_receipt"}]}]}
            """)!.AsObject();
        var actor = core["NPCsInScene"]![0]!;
        actor["characteristics"] = JsonNode.Parse("""
            {"setting_defined_focus":6.25,"setting_defined_resolve":0,
             "nested_authority":{"snapshotToken":"PRIVATE_SENTINEL_characteristic"}}
            """);
        actor["progressionProfile"] = JsonNode.Parse(mode switch
        {
            "none" => """{"schemaVersion":1,"mode":"none"}""",
            "table" => """{"schemaVersion":1,"mode":"table","thresholds":[10,20,30],"maxLevel":4,"capOverflow":"discard","consequences":{"skills":{"levelsPerAward":2,"allowedAwards":["newSkill","masteryIncrease"]},"characteristics":{"mode":"currentWorld"}}}""",
            _ => """{"schemaVersion":1,"mode":"standard","consequences":{"skills":{"levelsPerAward":2,"allowedAwards":["newSkill","masteryIncrease"]},"characteristics":{"mode":"currentWorld"}}}"""
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
        var originalCore = core.ToJsonString();
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", originalCore);
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_skills.json", """
            {"NPCActiveSkillChanges":[{"NPCId":"npc_accepted","skillChanges":[{"skillName":"UNACCEPTED_SKILL"}]}]}
            """);
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));
        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Fireball", text, StringComparison.Ordinal);
        Assert.Contains("23/91", text, StringComparison.Ordinal);
        Assert.Contains("основное", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Main", text, StringComparison.Ordinal);
        Assert.Contains(experience, text, StringComparison.Ordinal);
        Assert.Contains("Профиль развития", text, StringComparison.Ordinal);
        Assert.Contains(profileLabel, text, StringComparison.Ordinal);
        Assert.Contains("setting_defined_focus", text, StringComparison.Ordinal);
        Assert.Contains("6.25", text, StringComparison.Ordinal);
        Assert.Contains("setting_defined_resolve", text, StringComparison.Ordinal);
        if (mode != "none")
        {
            Assert.Contains("1 награда за каждые 2 полученных уровня", text, StringComparison.Ordinal);
            Assert.Contains("правила текущего мира", text, StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain("награда за каждые 2", text, StringComparison.Ordinal);
            Assert.DoesNotContain("правила текущего мира", text, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("nested_authority", text, StringComparison.Ordinal);
        Assert.DoesNotContain("UNACCEPTED_SKILL", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_SENTINEL", text, StringComparison.Ordinal);
        Assert.Equal(originalCore, await _fs.ReadFileAsync("game_state/npcs/npc_core.json"));
    }
}
