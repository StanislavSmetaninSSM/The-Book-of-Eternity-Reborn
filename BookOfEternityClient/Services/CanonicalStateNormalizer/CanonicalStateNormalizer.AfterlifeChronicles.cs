using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private async Task NormalizeAfterlifeChroniclesAsync(IReadOnlyDictionary<string, string>? backups)
    {
        var currentNode = await ReadNodeAsync(AfterlifeChronicleState.StatePath);
        if (currentNode == null)
            return;

        if (currentNode is not JsonObject currentRoot)
            return;

        var previousRoot = await ReadBackupObjectAsync(AfterlifeChronicleState.StatePath, backups);
        var result = AfterlifeChronicleState.ProjectCanonicalRoot(currentRoot, previousRoot);
        await WriteIfChangedAsync(AfterlifeChronicleState.StatePath, currentNode, result);
    }
}
