using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class ShiningAbodeState
{
    private static readonly ResourceCost ForgeReshapeCost = new(10, 10);
    private static readonly ResourceCost ForgeRetunePropertyCost = new(20, 15);
    private static readonly ResourceCost ForgeStrengthenBandCost = new(30, 20);
    private static readonly ResourceCost ForgeStabilizeEchoCost = new(25, 15);
    private static readonly ResourceCost ForgeUpliftRarityCost = new(45, 30);

    private static readonly string[] SoulRelicRarityLadder =
    {
        "common",
        "uncommon",
        "rare",
        "epic",
        "legendary"
    };

    public static bool IsForgeActionType(string? actionType)
    {
        return string.Equals(actionType, ShiningCoreActionRequestState.ActionTypeForgeRelicReshape, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(actionType, ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(actionType, ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(actionType, ShiningCoreActionRequestState.ActionTypeForgeRelicStabilizeEcho, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(actionType, ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity, StringComparison.OrdinalIgnoreCase);
    }

    public static int GetForgeRequiredRadianceTier(string? actionType)
    {
        return (actionType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            ShiningCoreActionRequestState.ActionTypeForgeRelicReshape => 0,
            ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty => 1,
            ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand => 2,
            ShiningCoreActionRequestState.ActionTypeForgeRelicStabilizeEcho => 3,
            ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity => 4,
            _ => int.MaxValue
        };
    }

    public static ResourceCost GetForgeBaseCost(string? actionType)
    {
        return (actionType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            ShiningCoreActionRequestState.ActionTypeForgeRelicReshape => ForgeReshapeCost,
            ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty => ForgeRetunePropertyCost,
            ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand => ForgeStrengthenBandCost,
            ShiningCoreActionRequestState.ActionTypeForgeRelicStabilizeEcho => ForgeStabilizeEchoCost,
            ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity => ForgeUpliftRarityCost,
            _ => default
        };
    }

    public static int GetTradeStockItemCount(JsonObject faction, JsonObject? residentRoot)
    {
        var factionStrength = Math.Clamp(GetNodeInt(faction["factionStrength"], 0), 0, 100);
        var tradeTier = GetTradeTier(factionStrength);
        if (tradeTier <= 0)
            return 0;

        var stockItemCount = tradeTier switch
        {
            1 => 4,
            2 => 6,
            _ => 8
        };

        stockItemCount += CountSupportedProjectsByArchetypeForFaction(faction, ProjectArchetypeProvision);
        if (HasFactionRole(residentRoot, GetNodeString(faction["factionId"]), ResidentRoleResourceSupport))
            stockItemCount += 1;

        return stockItemCount;
    }

    public static string GetTradeRarityCeiling(int factionStrength)
    {
        return GetTradeTier(factionStrength) switch
        {
            <= 0 => "none",
            1 => RarityUncommon,
            2 => RarityRare,
            _ => RarityRadiant
        };
    }

    public static bool FactionHasSupportedProjectArchetype(JsonObject? faction, string archetype) =>
        CountSupportedProjectsByArchetypeForFaction(faction, archetype) > 0;

    public static bool FactionHasAvailableTrade(JsonObject? faction) =>
        faction != null && GetTradeTier(GetNodeInt(faction["factionStrength"], 0)) >= 1;

    public static string GetTradeCycleId(int currentIncarnation) => $"shining_return_{Math.Max(0, currentIncarnation)}";

    public static bool IsSupportedTradeInventoryRarityCeiling(string? value) =>
        string.Equals(value, "none", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, RarityCommon, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, RarityUncommon, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, RarityRare, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, RarityRadiant, StringComparison.OrdinalIgnoreCase);

    public static bool IsSoulRelicRarityAllowedForTradeCeiling(string? soulRelicRarity, string? tradeCeiling)
    {
        if (string.IsNullOrWhiteSpace(soulRelicRarity) || string.IsNullOrWhiteSpace(tradeCeiling))
            return false;

        var rarityRank = ResolveSoulRelicTradeRarityRank(soulRelicRarity);
        var ceilingRank = (tradeCeiling ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "none" => 0,
            RarityCommon => 1,
            RarityUncommon => 2,
            RarityRare => 3,
            RarityRadiant => 5,
            _ => 0
        };

        return rarityRank > 0 && rarityRank <= ceilingRank;
    }

    public static bool TryQuoteForgeAction(
        JsonObject shiningRoot,
        JsonObject soulRoot,
        JsonObject? residentRoot,
        string? actionType,
        string? factionId,
        string? relicId,
        string? targetFormTag,
        int propertyIndex,
        JsonObject? replacementProperty,
        JsonArray? addedProperties,
        out ResourceCost cost,
        out string? error)
    {
        cost = default;
        if (!EnsureOrdinaryActiveState(shiningRoot, out error))
            return false;

        if (!IsForgeActionType(actionType))
        {
            error = "Неподдерживаемый forge action.";
            return false;
        }

        if (!TryGetFaction(shiningRoot, factionId ?? string.Empty, out var faction, out error))
            return false;

        var radianceTier = GetNodeInt(shiningRoot["radiance"]?["tier"], 0);
        var requiredTier = GetForgeRequiredRadianceTier(actionType);
        if (radianceTier < requiredTier)
        {
            error = $"Для {actionType} нужен Radiance tier {requiredTier} или выше.";
            return false;
        }

        if (!FactionHasSupportedProjectArchetype(faction, ProjectArchetypeRefinement))
        {
            error = "Forge требует хотя бы один supported completed refinement project в выбранной фракции.";
            return false;
        }

        if (!TryFindSoulRelic(soulRoot, relicId, out var relic))
        {
            error = "Выбранная Soul Relic не найдена в canonical soul_state.";
            return false;
        }

        switch ((actionType ?? string.Empty).Trim().ToLowerInvariant())
        {
            case ShiningCoreActionRequestState.ActionTypeForgeRelicReshape:
                var currentFormTag = GetNodeString(relic["formTag"]);
                if (string.IsNullOrWhiteSpace(currentFormTag))
                {
                    error = "reshape доступен только для relic с canonical formTag.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(targetFormTag))
                {
                    error = "reshape требует новый targetFormTag.";
                    return false;
                }

                if (string.Equals(currentFormTag, targetFormTag, StringComparison.OrdinalIgnoreCase))
                {
                    error = "Новый formTag должен отличаться от текущего.";
                    return false;
                }

                break;

            case ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty:
                if (!TryValidateRetuneRequest(relic, propertyIndex, replacementProperty, out error))
                    return false;
                break;

            case ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand:
                if (!TryValidateStrengthenBandRequest(relic, propertyIndex, out error))
                    return false;
                break;

            case ShiningCoreActionRequestState.ActionTypeForgeRelicStabilizeEcho:
                if (!IsCompanionForgeRelic(relic))
                {
                    error = "stabilize_echo доступен только для companion relic.";
                    return false;
                }

                break;

            case ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity:
                if (!TryValidateUpliftRarityRequest(relic, addedProperties, out error))
                    return false;
                break;
        }

        var baseCost = GetForgeBaseCost(actionType);
        var feathers = baseCost.Feathers;
        var lightSparks = baseCost.LightSparks;
        if (HasFactionRole(residentRoot, GetNodeString(faction["factionId"]), ResidentRoleForgeSupport))
        {
            feathers = Math.Max(1, feathers - 5);
            lightSparks = Math.Max(1, lightSparks - 5);
        }

        var adjustedCost = ShiningBlessingEffectState.AdjustForgeCostForBlessingEntitlements(
            soulRoot,
            actionType,
            new ResourceCost(feathers, lightSparks));
        feathers = adjustedCost.Feathers;
        lightSparks = adjustedCost.LightSparks;

        if (GetForgeInkFeathersCurrent(soulRoot) < feathers)
        {
            error = $"Недостаточно Перьев. Нужно {feathers}.";
            return false;
        }

        if (GetNodeInt(shiningRoot["lightSparks"], 0) < lightSparks)
        {
            error = $"Недостаточно Искр Света. Нужно {lightSparks}.";
            return false;
        }

        cost = new ResourceCost(feathers, lightSparks);
        error = null;
        return true;
    }

    public static bool TryApplyForgeAction(
        JsonObject shiningRoot,
        JsonObject soulRoot,
        JsonObject? residentRoot,
        string? actionType,
        string? factionId,
        string? relicId,
        string? targetFormTag,
        int propertyIndex,
        JsonObject? replacementProperty,
        JsonArray? addedProperties,
        int currentTurnNumber,
        string? resolvedAtUtc,
        out ResourceCost cost,
        out string? error)
    {
        cost = default;
        if (!TryQuoteForgeAction(
                shiningRoot,
                soulRoot,
                residentRoot,
                actionType,
                factionId,
                relicId,
                targetFormTag,
                propertyIndex,
                replacementProperty,
                addedProperties,
                out cost,
                out error))
        {
            return false;
        }

        var faction = FindFaction(shiningRoot, factionId)!;
        if (!TryFindSoulRelic(soulRoot, relicId, out var relic))
        {
            error = "Выбранная Soul Relic не найдена в canonical soul_state.";
            return false;
        }
        ApplyForgeFeatherCost(soulRoot, cost.Feathers);
        shiningRoot["lightSparks"] = Math.Max(0, GetNodeInt(shiningRoot["lightSparks"], 0) - cost.LightSparks);

        switch ((actionType ?? string.Empty).Trim().ToLowerInvariant())
        {
            case ShiningCoreActionRequestState.ActionTypeForgeRelicReshape:
                relic["formTag"] = targetFormTag!.Trim();
                break;

            case ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty:
                GetRelicPropertiesArray(relic)![propertyIndex] = replacementProperty!.DeepClone();
                break;

            case ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand:
                var property = GetRelicPropertiesArray(relic)![propertyIndex] as JsonObject;
                if (property == null || !TryUpgradeBandValue(property["band"], out var upgradedBand))
                {
                    error = "Не удалось увеличить band выбранного свойства.";
                    return false;
                }

                property["band"] = upgradedBand;
                break;

            case ShiningCoreActionRequestState.ActionTypeForgeRelicStabilizeEcho:
                var serviceBonus = (int)Math.Floor(15 * GetServiceMultiplier(GetNodeInt(faction["factionStrength"], 0)));
                relic["companionManifestationQualityBonus"] = GetNodeInt(relic["companionManifestationQualityBonus"], 0) + Math.Max(0, serviceBonus);
                break;

            case ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity:
                var currentRarity = GetSoulRelicRarity(relic);
                var nextRarity = UpgradeSoulRelicRarity(currentRarity);
                SetSoulRelicRarity(relic, nextRarity);

                var properties = GetRelicPropertiesArray(relic)!;
                var requiredPropertyCount = GetSoulRelicMinimumPropertyCount(nextRarity);
                var existingCount = properties.Count;
                var neededCount = Math.Max(0, requiredPropertyCount - existingCount);
                if (neededCount > 0 && addedProperties != null)
                {
                    foreach (var propertyNode in addedProperties.OfType<JsonObject>().Take(neededCount))
                        properties.Add(propertyNode.DeepClone());
                }

                break;
        }

        ShiningBlessingEffectState.ConsumeForgeEntitlements(
            soulRoot,
            actionType,
            currentTurnNumber,
            resolvedAtUtc);

        error = null;
        return true;
    }

    private static void ApplyForgeFeatherCost(JsonObject soulRoot, int feathers)
    {
        if (feathers <= 0)
            return;

        var inkFeathers = soulRoot["inkFeathers"] as JsonObject ?? new JsonObject();
        inkFeathers["current"] = Math.Max(0, GetForgeInkFeathersCurrent(soulRoot) - feathers);
        soulRoot["inkFeathers"] = inkFeathers;
    }

    private static int GetForgeInkFeathersCurrent(JsonObject soulRoot)
    {
        if (soulRoot["inkFeathers"] is JsonObject inkFeathers)
            return GetNodeInt(inkFeathers["current"], 0);

        return GetNodeInt(soulRoot["inkFeathers"], 0);
    }

    private static int CountSupportedProjectsByArchetypeForFaction(JsonObject? faction, string archetype)
    {
        if (faction?["projects"] is not JsonArray projects)
            return 0;

        return projects.OfType<JsonObject>().Count(project =>
            string.Equals(GetNodeString(project["status"]), ProjectStatusCompleted, StringComparison.OrdinalIgnoreCase) &&
            GetNodeBool(project["isSupported"]) &&
            string.Equals(GetNodeString(project["projectArchetype"]), archetype, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryValidateRetuneRequest(
        JsonObject relic,
        int propertyIndex,
        JsonObject? replacementProperty,
        out string? error)
    {
        if (!TryGetRelicProperty(relic, propertyIndex, out var currentProperty, out error))
            return false;

        if (replacementProperty == null)
        {
            error = "retune_property требует replacementProperty object.";
            return false;
        }

        if (replacementProperty["band"] == null || currentProperty["band"] == null)
        {
            error = "И текущее, и новое свойство должны содержать band.";
            return false;
        }

        if (!TryNormalizeBandKey(currentProperty["band"], out var currentBand) ||
            !TryNormalizeBandKey(replacementProperty["band"], out var replacementBand))
        {
            error = "retune_property поддерживает только canonical band formats.";
            return false;
        }

        if (!string.Equals(currentBand, replacementBand, StringComparison.Ordinal))
        {
            error = "replacementProperty должен быть того же band, что и заменяемое свойство.";
            return false;
        }

        if (JsonNode.DeepEquals(currentProperty, replacementProperty))
        {
            error = "replacementProperty должен отличаться от текущего свойства.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateStrengthenBandRequest(JsonObject relic, int propertyIndex, out string? error)
    {
        if (!TryGetRelicProperty(relic, propertyIndex, out var property, out error))
            return false;

        if (property["band"] == null)
        {
            error = "strengthen_band требует property.band.";
            return false;
        }

        if (!TryUpgradeBandValue(property["band"], out _))
        {
            error = "Для выбранного property.band нет следующего canonical шага.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateUpliftRarityRequest(JsonObject relic, JsonArray? addedProperties, out string? error)
    {
        var currentRarity = GetSoulRelicRarity(relic);
        if (!AbodePowerRules.IsCanonicalSoulRelicRarity(currentRarity))
        {
            error = "uplift_rarity требует canonical Soul Relic rarity.";
            return false;
        }

        var nextRarity = UpgradeSoulRelicRarity(currentRarity);
        if (string.Equals(nextRarity, currentRarity, StringComparison.OrdinalIgnoreCase))
        {
            error = "Эта реликвия уже находится на верхней rarity ступени.";
            return false;
        }

        var properties = GetRelicPropertiesArray(relic);
        if (properties == null)
        {
            error = "uplift_rarity требует canonical properties array.";
            return false;
        }

        var requiredCount = GetSoulRelicMinimumPropertyCount(nextRarity);
        var missingCount = Math.Max(0, requiredCount - properties.Count);
        if (missingCount == 0)
        {
            error = null;
            return true;
        }

        var addedPropertyObjects = addedProperties?.OfType<JsonObject>().ToList() ?? new List<JsonObject>();
        if (addedPropertyObjects.Count < missingCount)
        {
            error = $"Для uplift_rarity не хватает {missingCount} additional properties до минимального property count новой rarity.";
            return false;
        }

        foreach (var propertyNode in addedPropertyObjects.Take(missingCount))
        {
            if (propertyNode["band"] == null)
            {
                error = "Каждое added property для uplift_rarity должно содержать band.";
                return false;
            }

            var propertyIdentity = GetForgePropertyIdentity(propertyNode);
            if (string.IsNullOrWhiteSpace(propertyIdentity))
            {
                error = "Каждое added property для uplift_rarity должно иметь propertyId, name или stat.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool TryGetRelicProperty(JsonObject relic, int propertyIndex, out JsonObject property, out string? error)
    {
        property = null!;
        var properties = GetRelicPropertiesArray(relic);
        if (properties == null)
        {
            error = "Для этой forge action реликвия должна содержать canonical properties array.";
            return false;
        }

        if (propertyIndex < 0 || propertyIndex >= properties.Count)
        {
            error = "Выбранный propertyIndex выходит за пределы properties array.";
            return false;
        }

        if (properties[propertyIndex] is not JsonObject propertyObject)
        {
            error = "properties[propertyIndex] должен быть object.";
            return false;
        }

        property = propertyObject;
        error = null;
        return true;
    }

    private static JsonArray? GetRelicPropertiesArray(JsonObject relic)
    {
        if (relic["properties"] is JsonArray properties)
            return properties;

        return null;
    }

    private static bool IsCompanionForgeRelic(JsonObject relic)
    {
        return string.Equals(GetNodeString(relic["relicType"]), GuardianAbodeResidentState.RelicTypeCompanionEcho, StringComparison.OrdinalIgnoreCase) ||
               relic["companionSeed"] is JsonObject ||
               relic["companionManifestationQualityBonus"] != null;
    }

    private static string GetSoulRelicRarity(JsonObject relic)
    {
        var rarity = GetNodeString(relic["quality"]);
        if (string.IsNullOrWhiteSpace(rarity))
            rarity = GetNodeString(relic["rarity"]);

        return (rarity ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static void SetSoulRelicRarity(JsonObject relic, string rarity)
    {
        if (!string.IsNullOrWhiteSpace(GetNodeString(relic["quality"])) || relic.ContainsKey("quality"))
            relic["quality"] = rarity;

        if (!string.IsNullOrWhiteSpace(GetNodeString(relic["rarity"])) || relic.ContainsKey("rarity"))
            relic["rarity"] = rarity;

        if (!relic.ContainsKey("quality") && !relic.ContainsKey("rarity"))
            relic["quality"] = rarity;
    }

    private static string UpgradeSoulRelicRarity(string rarity)
    {
        var index = Array.FindIndex(SoulRelicRarityLadder, candidate => string.Equals(candidate, rarity, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            return rarity;

        return index >= SoulRelicRarityLadder.Length - 1
            ? SoulRelicRarityLadder[^1]
            : SoulRelicRarityLadder[index + 1];
    }

    private static int GetSoulRelicMinimumPropertyCount(string rarity)
    {
        return rarity.Trim().ToLowerInvariant() switch
        {
            "common" => 1,
            "uncommon" => 2,
            "rare" => 3,
            "epic" => 4,
            "legendary" => 5,
            _ => 1
        };
    }

    private static bool TryUpgradeBandValue(JsonNode? bandNode, out JsonNode upgradedBand)
    {
        upgradedBand = JsonValue.Create("unknown")!;
        if (!TryNormalizeBandKey(bandNode, out var bandKey))
            return false;

        if (bandKey.StartsWith("i:", StringComparison.Ordinal) &&
            int.TryParse(bandKey["i:".Length..], out var numericBand))
        {
            upgradedBand = JsonValue.Create(numericBand + 1)!;
            return true;
        }

        var currentIndex = Array.FindIndex(SoulRelicRarityLadder, candidate => string.Equals(candidate, bandKey, StringComparison.OrdinalIgnoreCase));
        if (currentIndex < 0 || currentIndex >= SoulRelicRarityLadder.Length - 1)
            return false;

        upgradedBand = JsonValue.Create(SoulRelicRarityLadder[currentIndex + 1])!;
        return true;
    }

    private static bool TryNormalizeBandKey(JsonNode? bandNode, out string bandKey)
    {
        bandKey = string.Empty;
        if (bandNode == null)
            return false;

        if (bandNode is JsonValue value)
        {
            if (value.TryGetValue<int>(out var intBand))
            {
                bandKey = $"i:{intBand}";
                return true;
            }

            if (value.TryGetValue<string>(out var stringBand))
            {
                var normalized = stringBand.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(normalized))
                    return false;

                if (int.TryParse(normalized, out var parsedBand))
                {
                    bandKey = $"i:{parsedBand}";
                    return true;
                }

                if (SoulRelicRarityLadder.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    bandKey = normalized;
                    return true;
                }
            }
        }

        return false;
    }

    private static string GetForgePropertyIdentity(JsonObject property)
    {
        return GetNodeString(property["propertyId"]) ??
               GetNodeString(property["name"]) ??
               GetNodeString(property["stat"]) ??
               string.Empty;
    }

    private static int ResolveSoulRelicTradeRarityRank(string? rarity) => (rarity ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "common" => 1,
        "uncommon" => 2,
        "rare" => 3,
        "epic" => 4,
        "legendary" => 5,
        _ => 0
    };

    private static bool TryFindSoulRelic(JsonObject soulRoot, string? relicId, out JsonObject relic)
    {
        relic = null!;
        if (string.IsNullOrWhiteSpace(relicId))
            return false;

        var soulRelicsNode = soulRoot["soulRelics"];
        if (soulRelicsNode is JsonObject soulRelicsObject)
        {
            foreach (var collectionName in new[] { "equipped", "stored" })
            {
                if (soulRelicsObject[collectionName] is not JsonArray collection)
                    continue;

                foreach (var relicNode in collection.OfType<JsonObject>())
                {
                    var existingId = GetNodeString(relicNode["relicId"]) ?? GetNodeString(relicNode["id"]);
                    if (!string.Equals(existingId, relicId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    relic = relicNode;
                    return true;
                }
            }
        }
        else if (soulRelicsNode is JsonArray flatCollection)
        {
            foreach (var relicNode in flatCollection.OfType<JsonObject>())
            {
                var existingId = GetNodeString(relicNode["relicId"]) ?? GetNodeString(relicNode["id"]);
                if (!string.Equals(existingId, relicId, StringComparison.OrdinalIgnoreCase))
                    continue;

                relic = relicNode;
                return true;
            }
        }

        return false;
    }
}
