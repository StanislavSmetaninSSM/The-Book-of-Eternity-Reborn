using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerModeCommandTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TryProcessCommand_AcceptedSkillsWithoutSidecar_HidesPrivateMechanics(bool debug)
    {
        await SeedMortalStateAsync();
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
            {"NPCsInScene":[
            {"NPCId":"npc_accepted","name":"Accepted NPC","level":2,"experience":17,"experienceForNextLevel":565,
             "snapshotToken":"PRIVATE_SENTINEL_snapshot","requestId":"PRIVATE_SENTINEL_request",
             "receipt":"PRIVATE_SENTINEL_receipt","source":{"sessionId":"PRIVATE_SENTINEL_session"},
             "activeSkills":[{"skillName":"Fireball","masteryLevel":7,"currentMasteryProgress":23,
               "masteryProgressNeeded":91,"actionCost":"Fast","requestId":"PRIVATE_SENTINEL_skill"}]}
            ]}
            """);
        await _stateManager.RefreshGameStateAsync();
        _settings.AllowHistoryManipulation = debug;
        await _explorer.TryProcessCommand("/нпс");
        AssertNoHiddenExplorerErrors("accepted_npc_mechanics");
        var text = ExtractRenderedText();
        Assert.Contains("Fireball", text, StringComparison.Ordinal);
        Assert.Contains("мастерство 7", text, StringComparison.Ordinal);
        Assert.Contains("23/91", text, StringComparison.Ordinal);
        Assert.Contains("быстрое", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Fast", text, StringComparison.Ordinal);
        Assert.Contains("17/565", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_SENTINEL", text, StringComparison.Ordinal);
    }
}
