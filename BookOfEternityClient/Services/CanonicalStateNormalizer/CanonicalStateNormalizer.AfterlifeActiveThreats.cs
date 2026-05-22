using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private async Task NormalizeAfterlifeActiveThreatsAsync(IReadOnlyDictionary<string, string>? backups)
    {
        var currentNode = await ReadNodeAsync(AfterlifeActiveThreatState.StatePath);
        if (currentNode == null)
            return;

        if (currentNode is not JsonObject currentRoot)
            return;

        var previousRoot = await ReadBackupObjectAsync(AfterlifeActiveThreatState.StatePath, backups);
        var result = AfterlifeActiveThreatState.ProjectCanonicalRoot(currentRoot, previousRoot);
        await WriteIfChangedAsync(AfterlifeActiveThreatState.StatePath, currentNode, result);
    }
}
