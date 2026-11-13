using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.WebUi;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWebCommandServiceTests
{
    [Theory]
    [InlineData("/npcs")]
    [InlineData("/npc section npc_accepted mechanics")]
    public async Task ExecuteAsync_AcceptedNpcMechanics_IgnoresPendingSidecarAndPrivateFields(string command)
    {
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
            {"NPCsInScene":[{"NPCId":"npc_accepted","name":"Accepted NPC","level":2,"experience":17,"experienceForNextLevel":565,
              "snapshotToken":"PRIVATE_SENTINEL_snapshot","requestId":"PRIVATE_SENTINEL_request",
              "activeSkills":[{"skillName":"Fireball","masteryLevel":7,"currentMasteryProgress":23,
                "masteryProgressNeeded":91,"maxMasteryLevel":12,"receipt":"PRIVATE_SENTINEL_receipt"}]}]}
            """);
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_skills.json", """
            {"NPCActiveSkillChanges":[{"NPCId":"npc_accepted","skillChanges":[{"skillName":"UNACCEPTED_SKILL"}]}]}
            """);
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));
        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Fireball", text, StringComparison.Ordinal);
        Assert.Contains("23/91", text, StringComparison.Ordinal);
        Assert.Contains("17/565", text, StringComparison.Ordinal);
        Assert.DoesNotContain("UNACCEPTED_SKILL", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_SENTINEL", text, StringComparison.Ordinal);
    }
}
