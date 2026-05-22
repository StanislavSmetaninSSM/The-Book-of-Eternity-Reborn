using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private async Task NormalizeAfterlifeStoryOutlineAsync(IReadOnlyDictionary<string, string>? backups)
    {
        var currentNode = await ReadNodeAsync(AfterlifeStoryOutlineState.StatePath);
        if (currentNode == null)
            return;

        if (currentNode is not JsonObject currentRoot)
            return;

        var previousRoot = await ReadBackupObjectAsync(AfterlifeStoryOutlineState.StatePath, backups);
        var result = AfterlifeStoryOutlineState.ProjectCanonicalRoot(currentRoot, previousRoot);
        await WriteIfChangedAsync(AfterlifeStoryOutlineState.StatePath, currentNode, result);
    }
}
