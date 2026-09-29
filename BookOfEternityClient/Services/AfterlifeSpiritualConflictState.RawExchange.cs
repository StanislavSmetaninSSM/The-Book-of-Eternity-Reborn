using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public static partial class AfterlifeSpiritualConflictState
{
    /// <summary>
    /// Enumerates first-seen nonnull exchange rows using the same identity precedence as composition.
    /// </summary>
    /// <param name="source">
    /// Ordered input rows, or null for an absent log.
    /// </param>
    /// <param name="seen">
    /// Identities already selected from higher-priority carriers.
    /// </param>
    /// <returns>
    /// Selected rows and their original array indices without cloning or modifying the source.
    /// </returns>
    private static IEnumerable<(JsonNode Item, int Index)> EnumerateNewExchangeLogItems(
        JsonArray? source, HashSet<string> seen)
    {
        if (source is null) yield break;
        for (var index = 0; index < source.Count; index++)
            if (source[index] is { } item && seen.Add(GetExchangeLogItemIdentity(item)))
                yield return (item, index);
    }

    /// <summary>
    /// Resolves a projected active exchange to the raw row that actually supplies it.
    /// </summary>
    /// <param name="original">
    /// Signed original conflict root providing immutable historical rows.
    /// </param>
    /// <param name="raw">
    /// Committed direct root or exchange update whose duplicate keys were already rejected.
    /// </param>
    /// <param name="index">
    /// Zero-based projected exchange index from an owner-derived diagnostic.
    /// </param>
    /// <returns>
    /// The selected raw row and exact pointer, or null for historical, terminal or unsupported coordinates.
    /// </returns>
    internal static (JsonObject Exchange, string Pointer)? ResolveRawExchange(
        JsonObject original, JsonObject raw, int index)
    {
        if (index < 0) return null;
        if (!raw.ContainsKey(ResponseField))
        {
            var log = raw["activeConflict"]?["exchangeLog"] as JsonArray;
            return log is not null && index < log.Count && log[index] is JsonObject exchange
                ? (exchange, $"/activeConflict/exchangeLog/{index}") : null;
        }
        if (raw[ResponseField] is not JsonObject update ||
            !string.Equals(GetNodeString(update["mode"]), ModeExchange, StringComparison.OrdinalIgnoreCase) ||
            update["exchange"] is not JsonObject explicitExchange)
            return null;
        var historical = original["activeConflict"]?["exchangeLog"] as JsonArray ?? [];
        var canonical = historical.DeepClone().AsArray();
        canonical.Add(explicitExchange.DeepClone());
        var replacementKey = update["activeConflictAfter"] is JsonObject
            ? "activeConflictAfter" : "conflictStateAfter";
        if (update[replacementKey] is not JsonObject replacement)
            return index == historical.Count
                ? (explicitExchange, $"/{ResponseField}/exchange") : null;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var projectedIndex = 0;
        foreach (var (item, rawIndex) in EnumerateNewExchangeLogItems(canonical, seen))
        {
            if (projectedIndex++ != index) continue;
            return rawIndex == historical.Count && item is JsonObject
                ? (explicitExchange, $"/{ResponseField}/exchange") : null;
        }
        foreach (var (item, rawIndex) in EnumerateNewExchangeLogItems(replacement["exchangeLog"] as JsonArray, seen))
            if (projectedIndex++ == index)
                return item is JsonObject exchange
                    ? (exchange, $"/{ResponseField}/{replacementKey}/exchangeLog/{rawIndex}") : null;
        return null;
    }
}
