using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class MortalItemLocalActionPolicy
{
    internal static void NormalizePlacementForDestination(
        JsonObject item,
        MortalItemCarrierCoordinate destination)
    {
        if (!string.Equals(destination.Kind, "player_inventory", StringComparison.Ordinal))
            return;

        item.Remove("isCarried");
        item.Remove("currentLocationId");
        item.Remove("currentLocationName");
    }

    internal static bool IsCarriedByPlayer(JsonObject item)
    {
        if (!item.TryGetPropertyValue("isCarried", out var node))
            return true;

        return node is JsonValue value &&
               value.TryGetValue<bool>(out var isCarried) &&
               isCarried;
    }

    internal static bool IsCarriedByPlayer(JsonElement item)
    {
        if (!item.TryGetProperty("isCarried", out var node))
            return true;

        return node.ValueKind == JsonValueKind.True;
    }

    internal static bool IsQuestBound(JsonObject item)
    {
        if (!item.TryGetPropertyValue("questLinks", out var node))
            return false;

        return node is JsonArray links
            ? links.Count > 0
            : true;
    }
}
