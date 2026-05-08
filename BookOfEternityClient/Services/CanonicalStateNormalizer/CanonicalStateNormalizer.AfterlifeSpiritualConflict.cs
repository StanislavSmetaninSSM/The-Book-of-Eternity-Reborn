using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private async Task NormalizeAfterlifeSpiritualConflictStateAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = AfterlifeSpiritualConflictState.StatePath;
        var currentNode = await ReadNodeAsync(path);
        if (currentNode is not JsonObject currentRoot ||
            currentRoot[AfterlifeSpiritualConflictState.ResponseField] is not JsonObject updateRoot)
        {
            return;
        }

        var baseline = await ResolveAfterlifeSpiritualConflictProjectionBaselineAsync(currentRoot, backups);
        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(baseline, updateRoot);
        await WriteIfChangedAsync(path, currentNode, projected);
    }

    private async Task<JsonObject> ResolveAfterlifeSpiritualConflictProjectionBaselineAsync(
        JsonObject currentRoot,
        IReadOnlyDictionary<string, string>? backups)
    {
        var currentWithoutWrapper = currentRoot.DeepClone()!.AsObject();
        currentWithoutWrapper.Remove(AfterlifeSpiritualConflictState.ResponseField);

        if (CanUseCurrentAfterlifeSpiritualConflictRootAsProjectionBaseline(currentWithoutWrapper))
            return currentWithoutWrapper;

        var previous = await ReadBackupObjectAsync(AfterlifeSpiritualConflictState.StatePath, backups);
        if (previous != null)
            return previous;

        return LooksLikeAfterlifeSpiritualConflictCanonicalRoot(currentWithoutWrapper)
            ? currentWithoutWrapper
            : AfterlifeSpiritualConflictState.CreateDefaultRoot();
    }

    private static bool CanUseCurrentAfterlifeSpiritualConflictRootAsProjectionBaseline(JsonObject root)
    {
        return root["activeConflict"] is JsonObject ||
               root.ContainsKey("lastInvalidUpdate") ||
               root.ContainsKey("lastInvalidUpdateReason") ||
               root.ContainsKey("lastInvalidUpdateAtUtc");
    }

    private static bool LooksLikeAfterlifeSpiritualConflictCanonicalRoot(JsonObject root)
    {
        return root.ContainsKey("schemaVersion") ||
               root.ContainsKey("activeConflict") ||
               root.ContainsKey("recentConflicts") ||
               root.ContainsKey("lastInvalidUpdate");
    }
}
