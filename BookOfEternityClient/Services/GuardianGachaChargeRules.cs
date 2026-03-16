using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class GuardianGachaChargeRules
{
    public static int GetChargesPerReturnForReputation(int reputation) => reputation switch
    {
        <= -51 => 0,
        <= 49 => 1,
        <= 129 => 2,
        _ => 3
    };

    public static int ClampUsedCharges(int usedCharges, int chargesPerReturn)
        => Math.Clamp(usedCharges, 0, Math.Max(0, chargesPerReturn));

    public static int ResolveGuardianReputation(JsonObject guardian)
    {
        if (guardian["relationshipData"] is JsonObject relationshipData &&
            relationshipData["currentReputation"] is JsonNode relationshipRep &&
            TryGetInt(relationshipRep, out var currentReputation))
        {
            return currentReputation;
        }

        if (guardian["reputation"] is JsonNode reputationNode &&
            TryGetInt(reputationNode, out var reputation))
        {
            return reputation;
        }

        return 0;
    }

    public static int ResolveGuardianReputation(JsonElement guardian)
    {
        if (guardian.TryGetProperty("relationshipData", out var relationshipData) &&
            relationshipData.ValueKind == JsonValueKind.Object &&
            relationshipData.TryGetProperty("currentReputation", out var currentReputation) &&
            currentReputation.ValueKind == JsonValueKind.Number &&
            currentReputation.TryGetInt32(out var relationshipRep))
        {
            return relationshipRep;
        }

        if (guardian.TryGetProperty("reputation", out var reputation) &&
            reputation.ValueKind == JsonValueKind.Number &&
            reputation.TryGetInt32(out var directRep))
        {
            return directRep;
        }

        return 0;
    }

    public static (int ChargesPerReturn, int ChargesUsedThisReturn) NormalizeGuardianGachaState(JsonObject guardian)
    {
        var reputation = ResolveGuardianReputation(guardian);
        var chargesPerReturn = GetChargesPerReturnForReputation(reputation);

        var gachaSystem = guardian["gachaSystem"] as JsonObject ?? new JsonObject();
        if (gachaSystem["gachaHistory"] is not JsonArray)
            gachaSystem["gachaHistory"] = new JsonArray();

        var usedCharges = 0;
        if (gachaSystem["chargesUsedThisReturn"] is JsonNode usedNode &&
            TryGetInt(usedNode, out var parsedUsed))
        {
            usedCharges = parsedUsed;
        }

        gachaSystem["chargesPerReturn"] = chargesPerReturn;
        gachaSystem["chargesUsedThisReturn"] = usedCharges;
        guardian["gachaSystem"] = gachaSystem;

        return (chargesPerReturn, usedCharges);
    }

    private static bool TryGetInt(JsonNode node, out int value)
    {
        value = 0;
        if (node is not JsonValue valueNode)
            return false;

        try
        {
            value = valueNode.GetValue<int>();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
