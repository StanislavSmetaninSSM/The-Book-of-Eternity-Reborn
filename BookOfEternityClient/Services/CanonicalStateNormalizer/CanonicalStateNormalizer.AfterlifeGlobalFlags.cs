using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private async Task NormalizeAfterlifeGlobalFlagsAsync(IReadOnlyDictionary<string, string>? backups)
    {
        var currentNode = await ReadNodeAsync(AfterlifeGlobalFlagState.StatePath);
        if (currentNode == null)
            return;

        if (currentNode is not JsonObject currentRoot)
            return;

        var previousRoot = await ReadBackupObjectAsync(AfterlifeGlobalFlagState.StatePath, backups);
        var result = AfterlifeGlobalFlagState.ProjectCanonicalRoot(currentRoot, previousRoot);
        await WriteIfChangedAsync(AfterlifeGlobalFlagState.StatePath, currentNode, result);
    }
}
