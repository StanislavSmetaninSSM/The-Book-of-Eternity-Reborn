using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private async Task NormalizeChaosSeaGuardianPoliticsAsync(IReadOnlyDictionary<string, string>? backups)
    {
        var currentNode = await ReadNodeAsync(ChaosSeaGuardianPoliticsState.StatePath);
        if (currentNode is not JsonObject currentRoot)
            return;

        var previousRoot = await ReadBackupObjectAsync(ChaosSeaGuardianPoliticsState.StatePath, backups);
        var result = ChaosSeaGuardianPoliticsState.ProjectCanonicalRoot(currentRoot, previousRoot);
        await WriteIfChangedAsync(ChaosSeaGuardianPoliticsState.StatePath, currentNode, result);
    }
}
