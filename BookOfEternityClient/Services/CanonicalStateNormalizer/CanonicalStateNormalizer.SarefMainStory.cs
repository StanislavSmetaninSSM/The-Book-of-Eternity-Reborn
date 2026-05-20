using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private async Task NormalizeSarefMainStoryStateAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = SarefMainStoryState.StatePath;
        var currentNode = await ReadNodeAsync(path);
        if (currentNode is not JsonObject currentRoot)
            return;

        if (currentRoot[SarefMainStoryState.StateResponseField] is JsonObject canonicalRoot)
        {
            await WriteIfChangedAsync(path, currentNode, canonicalRoot.DeepClone().AsObject());
            return;
        }

        if (currentRoot[SarefMainStoryState.ResponseField] is not JsonObject updateRoot)
            return;

        var baseline = await ResolveSarefProjectionBaselineAsync(currentRoot, backups);
        var projected = SarefMainStoryState.ApplyUpdate(baseline, updateRoot);
        await WriteIfChangedAsync(path, currentNode, projected);
    }

    private async Task<JsonObject> ResolveSarefProjectionBaselineAsync(
        JsonObject currentRoot,
        IReadOnlyDictionary<string, string>? backups)
    {
        var currentWithoutWrapper = currentRoot.DeepClone()!.AsObject();
        currentWithoutWrapper.Remove(SarefMainStoryState.ResponseField);

        if (LooksLikeSarefCanonicalRoot(currentWithoutWrapper))
            return currentWithoutWrapper;

        var previous = await ReadBackupObjectAsync(SarefMainStoryState.StatePath, backups);
        if (previous != null)
            return previous;

        return SarefMainStoryState.CreateDefaultRoot();
    }

    private static bool LooksLikeSarefCanonicalRoot(JsonObject root) =>
        root.ContainsKey("schemaVersion") ||
        root.ContainsKey("revealStage") ||
        root.ContainsKey("sarefRevelations") ||
        root.ContainsKey("wingsInfiltration");
}
