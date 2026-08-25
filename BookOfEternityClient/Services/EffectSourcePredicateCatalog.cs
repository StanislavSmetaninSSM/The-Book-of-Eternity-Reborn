using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class EffectSourcePredicateCatalog
{
    private static readonly HashSet<string> Registered = new(StringComparer.Ordinal)
    {
        "active",
        "carried",
        "equipped",
        "unlocked"
    };

    internal static bool IsRegistered(string predicate) => Registered.Contains(predicate);

    internal static bool IsAllowedForSource(string sourceKind, string predicate) =>
        predicate switch
        {
            "active" => true,
            "carried" or "equipped" => string.Equals(sourceKind, "item", StringComparison.Ordinal),
            "unlocked" => sourceKind is "skill" or "spiritual_art" or "fate_card" or "combat_action",
            _ => false
        };

    internal static string? RequiredPredicate(JsonObject definition)
    {
        if (definition["lifetime"] is not JsonObject lifetime ||
            !string.Equals(ReadExact(lifetime["mode"]), "source_bound", StringComparison.Ordinal))
        {
            return null;
        }

        return ReadExact(lifetime["activePredicate"]);
    }

    internal static IReadOnlySet<string> NormalizeSatisfied(
        bool active,
        IEnumerable<string>? predicates)
    {
        var result = predicates == null
            ? new HashSet<string>(StringComparer.Ordinal)
            : predicates.Where(IsRegistered).ToHashSet(StringComparer.Ordinal);
        if (active)
            result.Add("active");
        return result;
    }

    private static string? ReadExact(JsonNode? node) =>
        node is JsonValue value &&
        value.TryGetValue<string>(out var text) &&
        !string.IsNullOrWhiteSpace(text) &&
        string.Equals(text, text.Trim(), StringComparison.Ordinal)
            ? text
            : null;
}
