using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class AfterlifeGuardianReturnCycleState
{
    internal static JsonObject ProjectNewChaosReturn(
        JsonObject currentRoot,
        int incarnation)
    {
        ArgumentNullException.ThrowIfNull(currentRoot);
        if (incarnation <= 0)
            throw new ArgumentOutOfRangeException(nameof(incarnation));

        var projected = currentRoot.DeepClone().AsObject();
        if (projected["guardians"] is not JsonArray guardians)
        {
            if (projected.ContainsKey("activeGuardian"))
            {
                throw new InvalidOperationException(
                    "guardians.json.activeGuardian cannot exist without guardians[].");
            }
            return projected;
        }

        var cycleId = $"chaos_return_{incarnation}";
        var byId = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in guardians)
        {
            if (node is not JsonObject guardian ||
                !TryReadExact(guardian["guardianId"], out var guardianId) ||
                !byId.TryAdd(guardianId, guardian) ||
                !aliases.Add(ResourceMaterializationContract.BuildConfusableKey(guardianId)))
            {
                throw new InvalidOperationException(
                    "Guardian return-cycle projection requires exact/confusable-unique guardianId values.");
            }

            if (guardian["gachaSystem"] is not JsonObject gacha)
                continue;
            GuardianGachaChargeRules.NormalizeGuardianGachaCompanionState(guardian);
            gacha = guardian["gachaSystem"]!.AsObject();
            gacha["currentReturnCycleId"] = cycleId;
        }

        if (projected["activeGuardian"] is JsonObject activeGuardian)
        {
            if (!TryReadExact(activeGuardian["guardianId"], out var activeGuardianId) ||
                !byId.TryGetValue(activeGuardianId, out var canonicalGuardian))
            {
                throw new InvalidOperationException(
                    "activeGuardian must resolve one exact guardianId before return-cycle projection.");
            }

            activeGuardian["gachaSystem"] =
                canonicalGuardian["gachaSystem"]?.DeepClone();
        }

        return projected;
    }

    private static bool TryReadExact(JsonNode? node, out string value)
    {
        value = string.Empty;
        if (node is not JsonValue scalar ||
            !scalar.TryGetValue<string>(out var text) ||
            !ResourceMaterializationContract.IsExactIdentifier(text))
        {
            return false;
        }

        value = text;
        return true;
    }
}
