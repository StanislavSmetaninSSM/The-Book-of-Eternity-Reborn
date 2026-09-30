using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private async Task NormalizePlayerSkillStateAsync(
        IReadOnlyDictionary<string, string>? backups)
    {
        const string activePath = "game_state/player/skills_active.json";
        const string passivePath = "game_state/player/skills_passive.json";
        var currentActive = await ReadNodeAsync(activePath);
        var currentPassive = await ReadNodeAsync(passivePath);
        if (currentActive is null && currentPassive is null)
            return;

        JsonObject? composedActive = null;
        JsonObject? composedPassive = null;
        if (currentActive is not null)
        {
            composedActive = EffectAcceptedTurnInputComposer.ComposeSkillAcceptedRoot(
                await ReadBackupNodeAsync(activePath, backups),
                currentActive,
                "activeSkillChanges",
                "removeActiveSkills",
                activePath);
        }
        if (currentPassive is not null)
        {
            composedPassive = EffectAcceptedTurnInputComposer.ComposeSkillAcceptedRoot(
                await ReadBackupNodeAsync(passivePath, backups),
                currentPassive,
                "passiveSkillChanges",
                "removePassiveSkills",
                passivePath);
        }

        var capabilityIssues = MortalWoundTreatmentCapabilityContract.ValidateComposedActorCatalog(
            "player",
            "player",
            currentActive as JsonObject ?? new JsonObject(),
            activePath,
            currentPassive as JsonObject ?? new JsonObject(),
            passivePath,
            composedActive ?? new JsonObject(),
            composedPassive ?? new JsonObject());
        if (capabilityIssues.Count > 0)
        {
            throw new InvalidOperationException(
                "Mortal wound-treatment capability normalizer invariant failed: " +
                capabilityIssues[0].Code);
        }

        if (currentActive is not null && composedActive is not null)
            await WriteIfChangedAsync(activePath, currentActive, composedActive);
        if (currentPassive is not null && composedPassive is not null)
            await WriteIfChangedAsync(passivePath, currentPassive, composedPassive);
    }
}
