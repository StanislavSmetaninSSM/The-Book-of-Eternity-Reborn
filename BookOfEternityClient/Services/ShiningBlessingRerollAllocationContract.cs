using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class ShiningBlessingRerollAllocationContract
{
    internal const string PropertyName = "rerollAllocation";
    internal const string ResourceKey = "blessing_rerolls";

    internal static JsonObject Create(int amount) => new()
    {
        ["resourceKey"] = ResourceKey,
        ["amount"] = amount
    };

    internal static int ReadOrZero(JsonNode? node) =>
        TryRead(node, out var amount) ? amount : 0;

    internal static bool TryConsume(JsonObject? owner, out int amount)
    {
        amount = 0;
        if (owner == null || !owner.TryGetPropertyValue(PropertyName, out var node))
            return true;

        if (!TryRead(node, out amount))
            return false;

        owner.Remove(PropertyName);
        return true;
    }

    internal static bool TryRead(JsonNode? node, out int amount)
    {
        amount = 0;
        if (node is not JsonObject allocation ||
            allocation.Count != 2 ||
            allocation["resourceKey"] is not JsonValue resourceKeyValue ||
            !resourceKeyValue.TryGetValue<string>(out var resourceKey) ||
            !string.Equals(resourceKey, ResourceKey, StringComparison.Ordinal) ||
            allocation["amount"] is not JsonValue amountValue ||
            !amountValue.TryGetValue<int>(out amount) ||
            amount < 0)
        {
            amount = 0;
            return false;
        }

        return true;
    }
}
