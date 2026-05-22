namespace BookOfEternityClient.Services;

internal static class AfterlifeActionCostRules
{
    internal static readonly IReadOnlyDictionary<string, Definition> Definitions =
        new Dictionary<string, Definition>(StringComparer.OrdinalIgnoreCase)
        {
            ["pressure"] = new(3, 1),
            ["guard"] = new(2, 1),
            ["counter"] = new(4, 2),
            ["maneuver"] = new(3, 1),
            ["binding"] = new(4, 2),
            ["force_binding"] = new(5, 2),
            ["break_binding"] = new(3, 1),
            ["incarnation_resistance"] = new(3, 1),
            ["champion_coordination"] = new(2, 1),
            ["recover_spiritual_power"] = new(0, 0)
        };

    internal sealed record Definition(int BaseCost, int MinCost);

    internal static bool HasCost(string? operationType) =>
        !string.IsNullOrWhiteSpace(operationType) &&
        Definitions.ContainsKey(operationType);

    internal static bool TryGetDefinition(string? operationType, out Definition definition)
    {
        if (!string.IsNullOrWhiteSpace(operationType) &&
            Definitions.TryGetValue(operationType, out definition!))
        {
            return true;
        }

        definition = new Definition(0, 0);
        return false;
    }

    internal static int ResolveStandardEffectiveCost(Definition definition, int artTier) =>
        Math.Max(definition.MinCost, definition.BaseCost - Math.Max(0, artTier));

    internal static int ComputeSpecialArtEffectiveCost(int minCost, int standardEffectiveCost, int specialMultiplier)
    {
        var multiplied = ((long)Math.Max(0, standardEffectiveCost) * Math.Max(0, specialMultiplier) + 99) / 100;
        var capped = Math.Min(int.MaxValue, Math.Max(minCost, multiplied));
        return (int)capped;
    }
}
