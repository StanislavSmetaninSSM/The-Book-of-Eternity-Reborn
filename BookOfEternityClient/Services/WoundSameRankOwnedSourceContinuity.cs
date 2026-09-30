namespace BookOfEternityClient.Services;

internal sealed record WoundRootDefinitionRebinding(
    string DefinitionKey, WoundWorkingReference Before, WoundWorkingReference After);
internal sealed record WoundSameRankSourceContinuityResult(
    WoundRootDefinitionRebinding? RootRebinding, string? ChangedDefinitionKey);

/// <summary>Original-to-final identity and intersecting-body continuity, independent of runtime allocation.</summary>
internal static class WoundSameRankOwnedSourceContinuity
{
    internal static WoundSameRankSourceContinuityResult Compare(
        IReadOnlyDictionary<string, WoundWorkingReference> beforeRootByDefinitionKey,
        IReadOnlyDictionary<string, WoundWorkingReference> afterRootByDefinitionKey,
        IReadOnlyDictionary<string, WoundOwnedEffectDefinitionFact> beforeDefinitions,
        IReadOnlyDictionary<string, WoundOwnedEffectDefinitionFact> afterDefinitions)
    {
        WoundRootDefinitionRebinding? root = null;
        foreach (var pair in beforeRootByDefinitionKey.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            if (afterRootByDefinitionKey.TryGetValue(pair.Key, out var after) && pair.Value != after)
            {
                root = new(pair.Key, pair.Value, after);
                break;
            }
        var changed = beforeDefinitions.Keys.Intersect(afterDefinitions.Keys, StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .FirstOrDefault(key => !string.Equals(beforeDefinitions[key].CanonicalJson,
                afterDefinitions[key].CanonicalJson, StringComparison.Ordinal));
        return new(root, changed);
    }
}
