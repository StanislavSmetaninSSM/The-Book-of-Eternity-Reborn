using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

// A parsed declaration is data, not proof that its source existed or was used.
// Only the live source owner acquires the original source and binds this data.
internal sealed record SpiritualWoundSourceEnvelope(
    int MaximumSeverityRank,
    int? GuaranteedSeverityRank)
{
    internal const string Property = "spiritualWoundEnvelope";

    internal static bool TryRead(
        JsonObject source,
        out SpiritualWoundSourceEnvelope? envelope,
        out string? error)
    {
        using var document = JsonDocument.Parse(source.ToJsonString());
        return TryRead(document.RootElement, out envelope, out error);
    }

    internal static bool TryRead(
        JsonElement source,
        out SpiritualWoundSourceEnvelope? envelope,
        out string? error)
    {
        envelope = null;
        error = null;
        if (source.ValueKind != JsonValueKind.Object)
            return Fail("source must be an object", out error);

        var declarations = source.EnumerateObject()
            .Where(property => MortalLocationIdentityState.BuildConfusableKey(property.Name) ==
                MortalLocationIdentityState.BuildConfusableKey(Property))
            .ToArray();
        if (declarations.Length == 0)
        {
            envelope = new(4, null);
            return true;
        }
        if (declarations.Length != 1 ||
            !string.Equals(declarations[0].Name, Property, StringComparison.Ordinal))
            return Fail("one exact spiritualWoundEnvelope property", out error);

        var declaration = declarations[0].Value;
        if (declaration.ValueKind != JsonValueKind.Object)
            return Fail("spiritualWoundEnvelope must be an object, not null", out error);

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in declaration.EnumerateObject())
        {
            if (!names.Add(property.Name) ||
                property.Name is not ("schemaVersion" or "maximumSeverityRank" or "guaranteedSeverityRank"))
                return Fail("closed unique spiritualWoundEnvelope properties", out error);
        }
        if (names.Count != 3 ||
            !declaration.TryGetProperty("schemaVersion", out var schema) ||
            schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version) || version != 1 ||
            !declaration.TryGetProperty("maximumSeverityRank", out var maximumNode) ||
            maximumNode.ValueKind != JsonValueKind.Number || !maximumNode.TryGetInt32(out var maximum) || maximum is < 0 or > 4 ||
            !declaration.TryGetProperty("guaranteedSeverityRank", out var guaranteeNode))
            return Fail("version 1, maximumSeverityRank integer 0..4 and explicit nullable guarantee", out error);

        int? guarantee = null;
        if (guaranteeNode.ValueKind != JsonValueKind.Null)
        {
            if (guaranteeNode.ValueKind != JsonValueKind.Number || !guaranteeNode.TryGetInt32(out var required) || required is < 1 or > 4 || required > maximum)
                return Fail("guaranteedSeverityRank null or integer 1..maximumSeverityRank", out error);
            guarantee = required;
        }
        envelope = new(maximum, guarantee);
        return true;
    }

    private static bool Fail(string message, out string? error)
    {
        error = message;
        return false;
    }
}
