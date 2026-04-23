using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class GuardianGachaChargeRules
{
    public static int GetBaseChargesPerReturnForReputation(int reputation) => reputation switch
    {
        <= -51 => 0,
        <= 49 => 1,
        <= 129 => 2,
        _ => 3
    };

    public static int GetChargesPerReturnForReputation(int reputation, int abodePower)
        => GetBaseChargesPerReturnForReputation(reputation) + AbodePowerRules.GetBonusGachaCharges(abodePower);

    public static int GetChargesPerReturnForReputation(int reputation, GuardianProjectState.ResolvedGuardianDerivedState derivedState)
        => GetBaseChargesPerReturnForReputation(reputation) + derivedState.BonusGachaCharges;

    public static int GetChargesPerReturnForGuardian(JsonObject guardian)
    {
        var reputation = ResolveGuardianReputation(guardian);
        AbodePowerRules.EnsureCanonicalState(guardian);
        return GetChargesPerReturnForReputation(reputation, AbodePowerRules.GetCurrentPower(guardian)) +
               PlayerGuardianFoundationState.GetFounderExtraGachaCharges(guardian);
    }

    public static int GetChargesPerReturnForGuardian(JsonElement guardian)
    {
        var reputation = ResolveGuardianReputation(guardian);
        return GetChargesPerReturnForReputation(reputation, AbodePowerRules.GetCurrentPower(guardian)) +
               PlayerGuardianFoundationState.GetFounderExtraGachaCharges(guardian);
    }

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
        var chargesPerReturn = GetChargesPerReturnForGuardian(guardian);

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
        gachaSystem["chargesUsedThisReturn"] = ClampUsedCharges(usedCharges, chargesPerReturn);
        guardian["gachaSystem"] = gachaSystem;

        return (chargesPerReturn, ClampUsedCharges(usedCharges, chargesPerReturn));
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
