using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Pure decoding/fingerprinting of current-world NPC policy DATA. Existing callers own all reads and leases.</summary>
internal static class MortalNpcWorldProgressionPolicy
{
    internal const string Path = "lore/current_world/world_setting.json";

    internal static MortalNpcProgressionConsequences.ParseResult<MortalNpcProgressionConsequences.CharacteristicPolicy> Parse(string? text)
    {
        if (text is null) return MortalNpcProgressionConsequences.ParseWorldPolicy(null);
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return Invalid();
            var selected = new JsonObject();
            var key = MortalLocationIdentityState.BuildConfusableKey("npcProgression");
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (MortalLocationIdentityState.BuildConfusableKey(property.Name) != key) continue;
                if (property.Name != "npcProgression" || selected.ContainsKey(property.Name) || !Unique(property.Value))
                    return Invalid();
                selected[property.Name] = JsonNode.Parse(property.Value.GetRawText());
            }
            return MortalNpcProgressionConsequences.ParseWorldPolicy(selected);
        }
        catch (JsonException) { return Invalid(); }
    }

    private static bool Unique(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array) return element.EnumerateArray().All(Unique);
        if (element.ValueKind != JsonValueKind.Object) return true;
        var keys = new HashSet<string>(StringComparer.Ordinal);
        return element.EnumerateObject().All(property => keys.Add(property.Name) && Unique(property.Value));
    }

    private static MortalNpcProgressionConsequences.ParseResult<MortalNpcProgressionConsequences.CharacteristicPolicy> Invalid() =>
        new(null, [new("npc_progression_consequence_policy_invalid", "npcProgression")]);

    internal static string Fingerprint(NpcCoreChangesContract.Authority authority)
    {
        var parsed = authority.NpcCharacteristicPolicy;
        var policy = parsed.Value;
        var root = new JsonObject
        {
            ["valid"] = parsed.IsValid,
            ["mode"] = policy?.Mode.ToString(),
            ["pointsPerLevel"] = policy?.PointsPerLevel,
            ["perLevel"] = policy?.PerLevel is null ? null : new JsonObject(policy.PerLevel
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new KeyValuePair<string, JsonNode?>(pair.Key, JsonValue.Create(pair.Value)))),
            ["allocationKeys"] = policy?.AllocationKeys is null ? null : Strings(policy.AllocationKeys),
            ["worldCharacteristicKeys"] = Strings(authority.WorldCharacteristicKeys),
            ["problems"] = new JsonArray(parsed.Problems.OrderBy(problem => problem.Field, StringComparer.Ordinal)
                .ThenBy(problem => problem.Code, StringComparer.Ordinal)
                .Select(problem => (JsonNode)new JsonObject { ["field"] = problem.Field, ["code"] = problem.Code }).ToArray())
        };
        return WoundAcceptedTurnFingerprintWriter.CanonicalJson(root);
    }

    private static JsonArray Strings(IEnumerable<string> values) => new(values.OrderBy(value => value, StringComparer.Ordinal)
        .Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
}
