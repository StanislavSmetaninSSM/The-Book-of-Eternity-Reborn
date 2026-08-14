using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private async Task NormalizePlayerSkillStateAsync(
        IReadOnlyDictionary<string, string>? backups)
    {
        await NormalizePlayerSkillStateAsync(
            "game_state/player/skills_active.json",
            "activeSkillChanges",
            "removeActiveSkills",
            backups);
        await NormalizePlayerSkillStateAsync(
            "game_state/player/skills_passive.json",
            "passiveSkillChanges",
            "removePassiveSkills",
            backups);
    }

    private async Task NormalizePlayerSkillStateAsync(
        string path,
        string changeProperty,
        string removalProperty,
        IReadOnlyDictionary<string, string>? backups)
    {
        var currentNode = await ReadNodeAsync(path);
        if (currentNode == null)
            return;

        var previousRoot = await ReadBackupNodeAsync(path, backups);
        var composed = EffectAcceptedTurnInputComposer.ComposeSkillAcceptedRoot(
            previousRoot,
            currentNode,
            changeProperty,
            removalProperty,
            path);
        await WriteIfChangedAsync(path, currentNode, composed);
    }
}
