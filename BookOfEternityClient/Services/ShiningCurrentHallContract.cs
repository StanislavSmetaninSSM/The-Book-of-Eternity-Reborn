using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Optional exact locality DATA. No ordinary GM turn may move this coordinate.</summary>
internal static class ShiningCurrentHallContract
{
    internal const string Property = "currentHallId";

    internal static string? Validate(JsonObject root) => ValidateCore(root.ContainsKey(Property),
        AfterlifeHealingServiceContract.Text(root[Property]),
        (root["halls"] as JsonArray)?.OfType<JsonObject>().Select(h => AfterlifeHealingServiceContract.Text(h["hallId"])) ?? [],
        root.Any(p => IsAlias(p.Key)));

    internal static string? Validate(JsonElement root)
    {
        var present = root.TryGetProperty(Property, out var coordinate);
        var id = present && coordinate.ValueKind == JsonValueKind.String ? coordinate.GetString() : null;
        IEnumerable<string?> halls = root.TryGetProperty("halls", out var rows) && rows.ValueKind == JsonValueKind.Array
            ? rows.EnumerateArray().Where(h => h.ValueKind == JsonValueKind.Object).Select(h =>
                h.TryGetProperty("hallId", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null)
            : [];
        return ValidateCore(present, id, halls, root.EnumerateObject().Any(p => IsAlias(p.Name)) || root.EnumerateObject().Count(p => p.Name == Property) > 1);
    }

    private static bool IsAlias(string key) =>
        key.Equals(Property, StringComparison.OrdinalIgnoreCase) && key != Property ||
        key.Equals("activeHallId", StringComparison.OrdinalIgnoreCase) || key.Equals("selectedHallId", StringComparison.OrdinalIgnoreCase);

    private static string? ValidateCore(bool present, string? id, IEnumerable<string?> halls, bool alias)
    {
        if (alias) return "Current Shining locality requires the exact currentHallId key, without aliases.";
        if (!present) return null; // Missing coordinate leaves locality unresolved, never inferred.
        if (!ResourceMaterializationContract.IsExactIdentifier(id))
            return "currentHallId requires an exact nonempty string identifier.";
        var matches = 0; var exact = false;
        foreach (var hall in halls)
            if (string.Equals(hall, id, StringComparison.OrdinalIgnoreCase))
            { matches++; exact |= string.Equals(hall, id, StringComparison.Ordinal); }
        return matches == 1 && exact ? null : "currentHallId must identify exactly one canonical halls[].hallId.";
    }

    internal static bool Preserves(JsonObject? original, JsonObject? current)
    {
        var before = original?.ContainsKey(Property) == true;
        var after = current?.ContainsKey(Property) == true;
        return before == after && (!before || JsonNode.DeepEquals(original![Property], current![Property]));
    }
}
