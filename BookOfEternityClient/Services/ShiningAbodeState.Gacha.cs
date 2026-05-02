using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class ShiningAbodeState
{
    private static readonly ResourceCost ShiningGachaPullCost = new(30, 0);

    public static ResourceCost GetShiningGachaPullCost() => ShiningGachaPullCost;

    public static int GetShiningGachaChargesPerReturn(int radianceTier) => 1 + Math.Clamp(radianceTier, 0, 4);

    public static JsonObject EnsureGachaSystemObject(JsonObject root)
    {
        if (root["gachaSystem"] is JsonObject gachaSystem)
            return gachaSystem;

        gachaSystem = BuildDefaultGachaSystemObject();
        root["gachaSystem"] = gachaSystem;
        return gachaSystem;
    }

    public static string ResolveShiningReturnCycleId(JsonObject root, JsonObject? soulRoot)
    {
        var gachaSystem = EnsureGachaSystemObject(root);
        var currentReturnCycleId = GetNodeString(gachaSystem["currentReturnCycleId"]);
        if (!string.IsNullOrWhiteSpace(currentReturnCycleId))
            return currentReturnCycleId!;

        var currentIncarnation = GetNodeInt(soulRoot?["currentIncarnation"], 0);
        return GetTradeCycleId(currentIncarnation);
    }

    public static int GetRemainingShiningGachaCharges(JsonObject root)
    {
        var gachaSystem = EnsureGachaSystemObject(root);
        var chargesPerReturn = Math.Max(0, GetNodeInt(gachaSystem["chargesPerReturn"], 0));
        var chargesUsedThisReturn = Math.Clamp(GetNodeInt(gachaSystem["chargesUsedThisReturn"], 0), 0, chargesPerReturn);
        return Math.Max(0, chargesPerReturn - chargesUsedThisReturn);
    }

    public static int GetProjectedShiningGachaBonusSteps(JsonObject root, JsonObject? residentRoot, JsonObject faction)
    {
        var bonusSteps = 0;
        var radianceTier = GetNodeInt(root["radiance"]?["tier"], 0);
        if (radianceTier >= 2)
            bonusSteps += 1;

        if (GetNodeInt(faction["factionStrength"], 0) >= 75)
            bonusSteps += 1;

        if (FactionHasSupportedProjectArchetype(faction, ProjectArchetypeRefinement) &&
            HasFactionRole(residentRoot, GetNodeString(faction["factionId"]), ResidentRoleForgeSupport))
        {
            bonusSteps += 1;
        }

        return Math.Clamp(bonusSteps, 0, 2);
    }

    public static bool TryQuoteRelicGachaPull(
        JsonObject root,
        JsonObject soulRoot,
        JsonObject? residentRoot,
        string? factionId,
        out ResourceCost cost,
        out int projectedBonusSteps,
        out string returnCycleId,
        out string? error)
    {
        cost = default;
        projectedBonusSteps = 0;
        returnCycleId = string.Empty;
        if (!EnsureOrdinaryActiveState(root, out error))
            return false;

        if (!TryGetFaction(root, factionId ?? string.Empty, out var faction, out error))
            return false;

        var gachaSystem = EnsureGachaSystemObject(root);
        var currentIncarnation = GetNodeInt(soulRoot["currentIncarnation"], 0);
        var expectedReturnCycleId = GetTradeCycleId(currentIncarnation);
        var syncedReturnCycleId = GetNodeString(gachaSystem["currentReturnCycleId"]);
        if (!string.IsNullOrWhiteSpace(syncedReturnCycleId) &&
            !string.Equals(syncedReturnCycleId, expectedReturnCycleId, StringComparison.OrdinalIgnoreCase))
        {
            error = "Сияющая гача ждёт синхронизации нового return-cycle. Вернись в Сияющую Обитель заново.";
            return false;
        }

        returnCycleId = string.IsNullOrWhiteSpace(syncedReturnCycleId)
            ? expectedReturnCycleId
            : syncedReturnCycleId!;
        cost = GetShiningGachaPullCost();
        projectedBonusSteps = GetProjectedShiningGachaBonusSteps(root, residentRoot, faction);

        var chargesPerReturn = Math.Max(0, GetNodeInt(gachaSystem["chargesPerReturn"], GetShiningGachaChargesPerReturn(GetNodeInt(root["radiance"]?["tier"], 0))));
        var chargesUsedThisReturn = Math.Clamp(GetNodeInt(gachaSystem["chargesUsedThisReturn"], 0), 0, chargesPerReturn);
        if (chargesUsedThisReturn >= chargesPerReturn)
        {
            error = "В этом возвращении больше нет попыток сияющей гачи.";
            return false;
        }

        if (GetGachaInkFeathersCurrent(soulRoot) < cost.Feathers)
        {
            error = $"Недостаточно Перьев. Нужно {cost.Feathers}.";
            return false;
        }

        error = null;
        return true;
    }

    public static bool TryApplyRelicGachaAccounting(
        JsonObject root,
        JsonObject soulRoot,
        JsonObject? residentRoot,
        string? factionId,
        string? requestId,
        string? relicId,
        string? relicName,
        string? baseRarity,
        string? finalRarity,
        int resolvedAtTurn,
        string? resolvedAtUtc,
        out ResourceCost cost,
        out int projectedBonusSteps,
        out string? error)
    {
        if (!TryQuoteRelicGachaPull(
                root,
                soulRoot,
                residentRoot,
                factionId,
                out cost,
                out projectedBonusSteps,
                out var returnCycleId,
                out error))
        {
            return false;
        }

        var gachaSystem = EnsureGachaSystemObject(root);
        var current = Math.Max(0, GetGachaInkFeathersCurrent(soulRoot) - cost.Feathers);
        var inkFeathers = soulRoot["inkFeathers"] as JsonObject ?? new JsonObject();
        inkFeathers["current"] = current;
        soulRoot["inkFeathers"] = inkFeathers;

        gachaSystem["currentReturnCycleId"] = returnCycleId;
        gachaSystem["chargesUsedThisReturn"] = GetNodeInt(gachaSystem["chargesUsedThisReturn"], 0) + 1;
        var history = EnsureArray(gachaSystem, "gachaHistory");
        history.Add(new JsonObject
        {
            ["requestId"] = requestId ?? string.Empty,
            ["factionId"] = factionId ?? string.Empty,
            ["factionName"] = GetNodeString(FindFaction(root, factionId)?["charter"]?["factionName"]) ?? factionId ?? string.Empty,
            ["returnCycleId"] = returnCycleId,
            ["costInFeathers"] = cost.Feathers,
            ["baseRarity"] = (baseRarity ?? string.Empty).Trim(),
            ["finalRarity"] = (finalRarity ?? string.Empty).Trim(),
            ["relicId"] = relicId ?? string.Empty,
            ["relicName"] = relicName ?? string.Empty,
            ["turnNumber"] = Math.Max(0, resolvedAtTurn),
            ["timestamp"] = string.IsNullOrWhiteSpace(resolvedAtUtc) ? DateTime.UtcNow.ToString("o") : resolvedAtUtc
        });

        error = null;
        return true;
    }

    private static int GetGachaInkFeathersCurrent(JsonObject soulRoot)
    {
        if (soulRoot["inkFeathers"] is JsonObject inkFeathers)
            return GetNodeInt(inkFeathers["current"], 0);

        return GetNodeInt(soulRoot["inkFeathers"], 0);
    }

    public static bool SyncShiningReturnCycle(JsonObject root, int currentIncarnation, out bool cycleChanged)
    {
        cycleChanged = false;
        var gachaSystem = EnsureGachaSystemObject(root);
        var nextReturnCycleId = GetTradeCycleId(currentIncarnation);
        var currentReturnCycleId = GetNodeString(gachaSystem["currentReturnCycleId"]) ?? string.Empty;
        if (string.Equals(currentReturnCycleId, nextReturnCycleId, StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrWhiteSpace(currentReturnCycleId))
        {
            gachaSystem["currentReturnCycleId"] = nextReturnCycleId;
            gachaSystem["chargesUsedThisReturn"] = 0;
            return true;
        }

        gachaSystem["currentReturnCycleId"] = nextReturnCycleId;
        gachaSystem["chargesUsedThisReturn"] = 0;
        cycleChanged = true;
        return true;
    }

    private static void NormalizeGachaSystemObject(JsonObject gachaSystem, int radianceTier)
    {
        var chargesPerReturn = GetShiningGachaChargesPerReturn(radianceTier);
        gachaSystem["chargesPerReturn"] = chargesPerReturn;
        var currentReturnCycleId = GetNodeString(gachaSystem["currentReturnCycleId"]) ?? string.Empty;
        gachaSystem["currentReturnCycleId"] = currentReturnCycleId;
        gachaSystem["chargesUsedThisReturn"] = string.IsNullOrWhiteSpace(currentReturnCycleId)
            ? 0
            : Math.Clamp(GetNodeInt(gachaSystem["chargesUsedThisReturn"], 0), 0, chargesPerReturn);

        var history = EnsureArray(gachaSystem, "gachaHistory");
        for (var i = history.Count - 1; i >= 0; i--)
        {
            if (history[i] is not JsonObject entry)
            {
                history.RemoveAt(i);
                continue;
            }

            entry["requestId"] = GetNodeString(entry["requestId"]) ?? string.Empty;
            entry["factionId"] = GetNodeString(entry["factionId"]) ?? string.Empty;
            entry["factionName"] = GetNodeString(entry["factionName"]) ?? string.Empty;
            entry["returnCycleId"] = GetNodeString(entry["returnCycleId"]) ?? string.Empty;
            entry["costInFeathers"] = Math.Max(0, GetNodeInt(entry["costInFeathers"], 0));
            entry["baseRarity"] = GetNodeString(entry["baseRarity"]) ?? string.Empty;
            entry["finalRarity"] = GetNodeString(entry["finalRarity"]) ?? string.Empty;
            entry["relicId"] = GetNodeString(entry["relicId"]) ?? string.Empty;
            entry["relicName"] = GetNodeString(entry["relicName"]) ?? string.Empty;
            entry["turnNumber"] = Math.Max(0, GetNodeInt(entry["turnNumber"], 0));
            entry["timestamp"] = GetNodeString(entry["timestamp"]) ?? string.Empty;
        }
    }
}
