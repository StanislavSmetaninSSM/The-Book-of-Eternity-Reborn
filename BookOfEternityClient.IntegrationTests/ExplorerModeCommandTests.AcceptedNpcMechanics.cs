using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerModeCommandTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShowNpcDetailPanel_AcceptedSkillsWithoutSidecar_HidesPrivateMechanics(bool debug)
    {
        using var actor = JsonDocument.Parse("""
            {"NPCId":"npc_accepted","name":"Accepted NPC","level":2,"experience":17,"experienceForNextLevel":565,
             "snapshotToken":"PRIVATE_SENTINEL_snapshot","requestId":"PRIVATE_SENTINEL_request",
             "receipt":"PRIVATE_SENTINEL_receipt","source":{"sessionId":"PRIVATE_SENTINEL_session"},
             "activeSkills":[{"skillName":"Fireball","masteryLevel":7,"currentMasteryProgress":23,
               "masteryProgressNeeded":91,"maxMasteryLevel":12,"requestId":"PRIVATE_SENTINEL_skill"}]}
            """);
        var method = typeof(ExplorerMode).GetMethod("ShowNpcDetailPanel", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)method.Invoke(_explorer,
            [actor.RootElement, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
             null, null, null, null, null, null, debug, null, null, null, null, null, null, null])!;
        var text = ExtractRenderedText();
        Assert.Contains("Fireball", text, StringComparison.Ordinal);
        Assert.Contains("мастерство 7", text, StringComparison.Ordinal);
        Assert.Contains("23/91", text, StringComparison.Ordinal);
        Assert.Contains("17/565", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_SENTINEL", text, StringComparison.Ordinal);
    }
}
