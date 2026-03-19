using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public sealed class GuardianTradeService
{
    private readonly FileSystemManager _fs;
    private readonly ILogger<GuardianTradeService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private const string GuardiansPath = "game_state/meta/guardians.json";
    private const string SoulStatePath = "game_state/meta/soul_state.json";

    private enum TradeReputationTier
    {
        Hostile,
        Neutral,
        Friendly,
        Devoted,
        Legendary
    }

    private sealed record DomainTradeProfile(
        string Domain,
        string Category,
        string PrimaryStat,
        string SecondaryStat,
        string TertiaryStat,
        string ActionBonusKey,
        string[] ThemeWords,
        string PassiveFlavor);

    public sealed record GuardianTradeOffer(
        string SlotId,
        string Name,
        string Rarity,
        int PriceInFeathers,
        string Description,
        string DomainTag,
        bool SoldOut,
        JsonObject RelicData);

    public sealed record GuardianTradeView(
        string GuardianId,
        string GuardianName,
        string Domain,
        string DomainDisplay,
        int CurrentReputation,
        string ReputationTierLabel,
        bool TradeBlocked,
        string? BlockReason,
        string TradeCycleId,
        IReadOnlyList<GuardianTradeOffer> Offers);

    public sealed record GuardianSellOffer(
        string RelicId,
        string Name,
        string Rarity,
        int PriceInFeathers,
        string Description);

    public sealed record GuardianTradeOperationResult(bool Success, bool StateChanged, string Message);

    private static readonly IReadOnlyDictionary<string, DomainTradeProfile> DomainProfiles =
        new Dictionary<string, DomainTradeProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["Combat"] = new(
                "Combat", "Combat",
                Characteristics.Strength, Characteristics.Constitution, Characteristics.Speed, "combatBonus",
                new[] { "Железной Доблести", "Победного Натиска", "Арены", "Ветерана", "Стального Обета", "Воинской Чести" },
                "Пассивно укрепляет решимость в бою."),
            ["Magic"] = new(
                "Magic", "Magical",
                Characteristics.Intelligence, Characteristics.Wisdom, Characteristics.Faith, "magicBonus",
                new[] { "Звёздной Мысли", "Тихого Эфира", "Лунного Пламени", "Арканного Узора", "Безмолвного Заклинателя", "Сумеречной Руны" },
                "Пассивно усиливает контроль над тонкими потоками магии."),
            ["Social"] = new(
                "Social", "Social",
                Characteristics.Attractiveness, Characteristics.Persuasion, Characteristics.Trade, "socialBonus",
                new[] { "Шёлкового Голоса", "Дворцовой Улыбки", "Доверия", "Сердечного Узора", "Изящного Жеста", "Салонной Грации" },
                "Пассивно усиливает первое впечатление и эмоциональный отклик."),
            ["Crafting"] = new(
                "Crafting", "Utility",
                Characteristics.Intelligence, Characteristics.Trade, Characteristics.Wisdom, "skillBonus",
                new[] { "Мастерской Искры", "Кузни Памяти", "Точного Резца", "Тихого Ремесла", "Гранёной Идеи", "Золотых Рук" },
                "Пассивно помогает находить более точные решения в ремесле."),
            ["Survival"] = new(
                "Survival", "Utility",
                Characteristics.Dexterity, Characteristics.Perception, Characteristics.Constitution, "globalBonus",
                new[] { "Туманных Троп", "Зоркого Следа", "Дальнего Костра", "Скрытого Следопыта", "Ночного Шага", "Крепкой Стужи" },
                "Пассивно помогает выстоять в трудных условиях."),
            ["Knowledge"] = new(
                "Knowledge", "Utility",
                Characteristics.Intelligence, Characteristics.Wisdom, Characteristics.Perception, "skillBonus",
                new[] { "Архивной Пыли", "Забытого Свитка", "Тихой Библиотеки", "Старинной Загадки", "Памяти Учёного", "Пера Летописца" },
                "Пассивно упорядочивает мысли и удерживает важные детали."),
            ["Trade"] = new(
                "Trade", "Utility",
                Characteristics.Trade, Characteristics.Persuasion, Characteristics.Luck, "globalBonus",
                new[] { "Честной Сделки", "Золотого Слова", "Рыночной Хитрости", "Весов Судьбы", "Счётной Книги", "Удачного Торга" },
                "Пассивно помогает замечать выгодные возможности раньше других.")
        };

    public GuardianTradeService(FileSystemManager fs, ILogger<GuardianTradeService> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public async Task<GuardianTradeView?> EnsureTradeInventoryAsync(string guardianId, int currentIncarnation)
    {
        var root = await ReadGuardiansRootAsync();
        if (root == null)
            return null;

        var guardian = FindGuardian(root, guardianId);
        if (guardian == null)
            return null;

        var (_, view, changed) = EnsureTradeInventoryState(root, guardian, currentIncarnation);
        if (changed)
            await _fs.WriteFileAtomicAsync(GuardiansPath, root.ToJsonString(JsonOpts));

        return view;
    }

    public async Task<IReadOnlyList<GuardianSellOffer>> GetSellableRelicsAsync(string guardianId)
    {
        var guardiansRoot = await ReadGuardiansRootAsync();
        var soulRoot = await ReadSoulStateRootAsync();
        if (guardiansRoot == null || soulRoot == null)
            return Array.Empty<GuardianSellOffer>();

        var guardian = FindGuardian(guardiansRoot, guardianId);
        if (guardian == null)
            return Array.Empty<GuardianSellOffer>();

        if (!GuardianTradeAllowedHere(guardiansRoot, guardian))
            return Array.Empty<GuardianSellOffer>();

        var tier = GetTradeReputationTier(ReadGuardianReputation(guardian));
        if (tier == TradeReputationTier.Hostile)
            return Array.Empty<GuardianSellOffer>();

        NormalizeSoulRelicsShape(soulRoot);
        var stored = ((JsonObject)soulRoot["soulRelics"]!)["stored"]?.AsArray();
        if (stored == null)
            return Array.Empty<GuardianSellOffer>();

        return stored.OfType<JsonObject>()
            .Select(relic =>
            {
                var rarity = GetRelicRarity(relic);
                return new GuardianSellOffer(
                    GetNodeString(relic["relicId"]) ?? GetNodeString(relic["id"]) ?? "",
                    GetNodeString(relic["name"]) ?? "Неизвестная реликвия",
                    rarity,
                    ComputeSellPrice(rarity, tier),
                    GetNodeString(relic["description"]) ?? "");
            })
            .Where(offer => !string.IsNullOrWhiteSpace(offer.RelicId))
            .OrderByDescending(offer => GetRarityRank(offer.Rarity))
            .ThenBy(offer => offer.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<GuardianTradeOperationResult> BuyAsync(string guardianId, string slotId, int currentIncarnation)
    {
        var guardiansRoot = await ReadGuardiansRootAsync();
        var soulRoot = await ReadSoulStateRootAsync();
        if (guardiansRoot == null || soulRoot == null)
            return new GuardianTradeOperationResult(false, false, "Не удалось прочитать состояние торговли или души.");

        var guardian = FindGuardian(guardiansRoot, guardianId);
        if (guardian == null)
            return new GuardianTradeOperationResult(false, false, "Хранитель не найден.");

        if (!GuardianTradeAllowedHere(guardiansRoot, guardian))
            return new GuardianTradeOperationResult(false, false, "Торговать можно только с текущим активным Хранителем в обители, где вы сейчас находитесь.");

        var (_, view, changed) = EnsureTradeInventoryState(guardiansRoot, guardian, currentIncarnation);
        if (view.TradeBlocked)
            return new GuardianTradeOperationResult(false, false, view.BlockReason ?? "Торговля недоступна.");

        if (guardian["tradeInventory"] is not JsonObject tradeInventory || tradeInventory["items"] is not JsonArray items)
            return new GuardianTradeOperationResult(false, false, "Витрина Хранителя недоступна.");

        var slot = items.OfType<JsonObject>().FirstOrDefault(item =>
            string.Equals(GetNodeString(item["slotId"]), slotId, StringComparison.OrdinalIgnoreCase));
        if (slot == null)
            return new GuardianTradeOperationResult(false, false, "Выбранный товар не найден.");

        if (GetNodeBool(slot["soldOut"]))
            return new GuardianTradeOperationResult(false, false, "Этот товар уже выкуплен в текущем возвращении.");

        var price = GetNodeInt(slot["priceInFeathers"], 0);
        if (price <= 0)
            return new GuardianTradeOperationResult(false, false, "Цена товара повреждена.");

        if (!TryModifyInkFeathers(soulRoot, -price))
            return new GuardianTradeOperationResult(false, false, "Недостаточно Чернильных Перьев.");

        if (slot["relicData"] is not JsonObject relicData)
            return new GuardianTradeOperationResult(false, false, "Данные реликвии повреждены.");

        NormalizeSoulRelicsShape(soulRoot);
        var stored = ((JsonObject)soulRoot["soulRelics"]!)["stored"]!.AsArray();
        UpsertRelic(stored, CloneObject(relicData));
        slot["soldOut"] = true;
        SyncActiveGuardian(guardiansRoot, guardianId, guardian);

        await _fs.WriteFileAtomicAsync(SoulStatePath, soulRoot.ToJsonString(JsonOpts));
        await _fs.WriteFileAtomicAsync(GuardiansPath, guardiansRoot.ToJsonString(JsonOpts));

        var relicName = GetNodeString(relicData["name"]) ?? "Реликвия";
        return new GuardianTradeOperationResult(true, true, $"Куплена реликвия «{relicName}» за {price} 🪶.");
    }

    public async Task<GuardianTradeOperationResult> SellAsync(string guardianId, string relicId)
    {
        var guardiansRoot = await ReadGuardiansRootAsync();
        var soulRoot = await ReadSoulStateRootAsync();
        if (guardiansRoot == null || soulRoot == null)
            return new GuardianTradeOperationResult(false, false, "Не удалось прочитать состояние торговли или души.");

        var guardian = FindGuardian(guardiansRoot, guardianId);
        if (guardian == null)
            return new GuardianTradeOperationResult(false, false, "Хранитель не найден.");

        if (!GuardianTradeAllowedHere(guardiansRoot, guardian))
            return new GuardianTradeOperationResult(false, false, "Торговать можно только с текущим активным Хранителем в обители, где вы сейчас находитесь.");

        var tier = GetTradeReputationTier(ReadGuardianReputation(guardian));
        if (tier == TradeReputationTier.Hostile)
            return new GuardianTradeOperationResult(false, false, "Этот Хранитель отказывается торговать с вами.");

        NormalizeSoulRelicsShape(soulRoot);
        var stored = ((JsonObject)soulRoot["soulRelics"]!)["stored"]?.AsArray();
        if (stored == null)
            return new GuardianTradeOperationResult(false, false, "Хранилище реликвий недоступно.");

        var relic = TakeRelic(stored, relicId);
        if (relic == null)
            return new GuardianTradeOperationResult(false, false, "Реликвия не найдена в хранилище.");

        var price = ComputeSellPrice(GetRelicRarity(relic), tier);
        if (!TryModifyInkFeathers(soulRoot, price))
            return new GuardianTradeOperationResult(false, false, "Не удалось обновить баланс перьев.");

        await _fs.WriteFileAtomicAsync(SoulStatePath, soulRoot.ToJsonString(JsonOpts));

        var relicName = GetNodeString(relic["name"]) ?? "Реликвия";
        return new GuardianTradeOperationResult(true, true, $"Продана реликвия «{relicName}» за {price} 🪶.");
    }

    private (TradeReputationTier Tier, GuardianTradeView View, bool Changed) EnsureTradeInventoryState(JsonObject root, JsonObject guardian, int currentIncarnation)
    {
        var guardianId = GetNodeString(guardian["guardianId"]) ?? "";
        var reputation = ReadGuardianReputation(guardian);
        var tier = GetTradeReputationTier(reputation);
        var cycleId = GetTradeCycleId(currentIncarnation);
        var blocked = tier == TradeReputationTier.Hostile || !GuardianTradeAllowedHere(root, guardian);
        var changed = false;

        if (!blocked)
        {
            var tradeInventory = guardian["tradeInventory"] as JsonObject;
            if (!TradeInventoryMatchesCycle(tradeInventory, cycleId, GetNodeString(guardian["domain"]) ?? "Knowledge"))
            {
                guardian["tradeInventory"] = GenerateTradeInventory(
                    guardianId,
                    GetNodeString(guardian["name"]) ?? guardianId,
                    GetNodeString(guardian["domain"]) ?? "Knowledge",
                    tier,
                    cycleId);
                changed = true;
            }
            else if (tradeInventory != null)
            {
                changed = RepriceTradeInventory(tradeInventory, tier);
            }

            if (changed)
                SyncActiveGuardian(root, guardianId, guardian);
        }

        return (tier, BuildTradeView(root, guardian, cycleId, blocked), changed);
    }

    private GuardianTradeView BuildTradeView(JsonObject root, JsonObject guardian, string cycleId, bool blocked)
    {
        var guardianId = GetNodeString(guardian["guardianId"]) ?? "";
        var guardianName = GetNodeString(guardian["name"]) ?? guardianId;
        var domain = GetNodeString(guardian["domain"]) ?? "Knowledge";
        var rep = ReadGuardianReputation(guardian);
        var offers = new List<GuardianTradeOffer>();

        if (!blocked &&
            guardian["tradeInventory"] is JsonObject tradeInventory &&
            tradeInventory["items"] is JsonArray items)
        {
            foreach (var item in items.OfType<JsonObject>())
            {
                if (item["relicData"] is not JsonObject relicData)
                    continue;

                offers.Add(new GuardianTradeOffer(
                    GetNodeString(item["slotId"]) ?? "",
                    GetNodeString(relicData["name"]) ?? "Реликвия",
                    GetRelicRarity(relicData),
                    GetNodeInt(item["priceInFeathers"], 0),
                    GetNodeString(relicData["description"]) ?? "",
                    GetNodeString(item["domainTag"]) ?? domain,
                    GetNodeBool(item["soldOut"]),
                    CloneObject(relicData)));
            }
        }

        return new GuardianTradeView(
            guardianId,
            guardianName,
            domain,
            GetDomainDisplay(domain),
            rep,
            GetReputationTierLabel(rep),
            blocked,
            blocked ? BuildTradeBlockedReason(root, guardian, rep) : null,
            cycleId,
            offers);
    }

    private static JsonObject GenerateTradeInventory(string guardianId, string guardianName, string domain, TradeReputationTier tier, string cycleId)
    {
        var profile = GetProfile(domain);
        var rarities = GetRarityPattern(tier);
        var themes = PickThemeWords(profile.ThemeWords, guardianId, cycleId, 4);
        var items = new JsonArray();

        for (var slotIndex = 0; slotIndex < 4; slotIndex++)
        {
            var rarity = rarities[slotIndex];
            items.Add(new JsonObject
            {
                ["slotId"] = $"trade_{guardianId}_{cycleId}_{slotIndex + 1}",
                ["priceInFeathers"] = ComputeBuyPrice(rarity, tier),
                ["domainTag"] = domain,
                ["soldOut"] = false,
                ["relicData"] = GenerateRelicData(profile, guardianId, guardianName, rarity, themes[slotIndex], slotIndex, cycleId)
            });
        }

        return new JsonObject
        {
            ["tradeCycleId"] = cycleId,
            ["generatedAtUtc"] = DateTime.UtcNow.ToString("o"),
            ["generationReputationTier"] = GetTradeTierCode(tier),
            ["pricingReputationTier"] = GetTradeTierCode(tier),
            ["items"] = items
        };
    }

    private static JsonObject GenerateRelicData(DomainTradeProfile profile, string guardianId, string guardianName, string rarity, string themeWord, int slotIndex, string cycleId)
    {
        var slot = slotIndex switch
        {
            0 => "Ring",
            1 => "Neck",
            2 => "Hands",
            _ => "Back"
        };

        var name = slotIndex switch
        {
            0 => $"Печать {themeWord}",
            1 => $"Колье {themeWord}",
            2 => $"Знак {themeWord}",
            _ => $"Плащ {themeWord}"
        };

        var primary = GetPrimaryStatBonus(rarity);
        var secondary = GetSecondaryStatBonus(rarity);
        var action = GetActionBonusValue(rarity);
        var characteristicBonuses = new JsonObject();
        var actionCheckBonuses = new JsonObject();
        var bonuses = new JsonArray();
        var passiveEffects = new JsonArray();

        switch (slotIndex)
        {
            case 0:
                characteristicBonuses[profile.PrimaryStat] = primary;
                bonuses.Add($"+{primary} к {Characteristics.RussianNames.GetValueOrDefault(profile.PrimaryStat, profile.PrimaryStat)}");
                break;
            case 1:
                characteristicBonuses[profile.SecondaryStat] = primary;
                characteristicBonuses[profile.PrimaryStat] = secondary;
                bonuses.Add($"+{primary} к {Characteristics.RussianNames.GetValueOrDefault(profile.SecondaryStat, profile.SecondaryStat)}");
                bonuses.Add($"+{secondary} к {Characteristics.RussianNames.GetValueOrDefault(profile.PrimaryStat, profile.PrimaryStat)}");
                break;
            case 2:
                characteristicBonuses[profile.TertiaryStat] = secondary;
                actionCheckBonuses[profile.ActionBonusKey] = action;
                bonuses.Add($"+{secondary} к {Characteristics.RussianNames.GetValueOrDefault(profile.TertiaryStat, profile.TertiaryStat)}");
                bonuses.Add(DescribeActionBonus(profile.ActionBonusKey, action));
                passiveEffects.Add(profile.PassiveFlavor);
                break;
            default:
                characteristicBonuses[profile.PrimaryStat] = secondary;
                characteristicBonuses[profile.SecondaryStat] = secondary;
                actionCheckBonuses[profile.ActionBonusKey] = Math.Max(1, action - 1);
                bonuses.Add($"+{secondary} к {Characteristics.RussianNames.GetValueOrDefault(profile.PrimaryStat, profile.PrimaryStat)}");
                bonuses.Add($"+{secondary} к {Characteristics.RussianNames.GetValueOrDefault(profile.SecondaryStat, profile.SecondaryStat)}");
                bonuses.Add(DescribeActionBonus(profile.ActionBonusKey, Math.Max(1, action - 1)));
                passiveEffects.Add(profile.PassiveFlavor);
                break;
        }

        return new JsonObject
        {
            ["relicId"] = $"trade_{SanitizeId(guardianId)}_{profile.Domain.ToLowerInvariant()}_{SanitizeId(themeWord)}_{cycleId}_{slotIndex + 1}",
            ["name"] = name,
            ["rarity"] = rarity,
            ["quality"] = rarity,
            ["category"] = profile.Category,
            ["slot"] = slot,
            ["description"] = BuildRelicDescription(profile, name, slotIndex),
            ["effects"] = new JsonObject
            {
                ["characteristicBonuses"] = characteristicBonuses,
                ["actionCheckBonuses"] = actionCheckBonuses
            },
            ["bonuses"] = bonuses,
            ["passiveEffects"] = passiveEffects,
            ["equipmentData"] = new JsonObject
            {
                ["equipSlot"] = slot,
                ["enlightenmentRequirement"] = GetEnlightenmentRequirement(rarity)
            },
            ["acquisitionData"] = new JsonObject
            {
                ["sourceGuardian"] = guardianId,
                ["acquisitionStory"] = $"Выкуплена у Хранителя «{guardianName}» из его доменной витрины."
            },
            ["gameplayStatus"] = new JsonObject
            {
                ["equipped"] = false,
                ["currentSlot"] = null
            }
        };
    }

    private async Task<JsonObject?> ReadGuardiansRootAsync()
    {
        var json = await _fs.ReadFileAsync(GuardiansPath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать guardians.json для торговли");
            return null;
        }
    }

    private async Task<JsonObject?> ReadSoulStateRootAsync()
    {
        var json = await _fs.ReadFileAsync(SoulStatePath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать soul_state.json для торговли");
            return null;
        }
    }

    private static JsonObject? FindGuardian(JsonObject root, string guardianId)
    {
        if (root["guardians"] is not JsonArray guardians)
            return null;

        return guardians.OfType<JsonObject>().FirstOrDefault(g =>
            string.Equals(GetNodeString(g["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase));
    }

    private static (string CurrentAbodeId, string ActiveGuardianId) ReadCurrentGuardianTradeContext(JsonObject root)
    {
        var currentAbodeId = "";
        var activeGuardianId = "";

        if (root["chaosSeaNavigation"] is JsonObject navigation)
            currentAbodeId = GetNodeString(navigation["currentAbodeId"]) ?? "";

        if (root["activeGuardian"] is JsonObject activeGuardian)
            activeGuardianId = GetNodeString(activeGuardian["guardianId"]) ?? "";

        return (currentAbodeId, activeGuardianId);
    }

    private static void SyncActiveGuardian(JsonObject root, string guardianId, JsonObject guardian)
    {
        if (root["activeGuardian"] is not JsonObject activeGuardian)
            return;

        if (string.Equals(GetNodeString(activeGuardian["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase))
            root["activeGuardian"] = guardian.DeepClone();
    }

    private static int ReadGuardianReputation(JsonObject guardian)
    {
        if (guardian["relationshipData"] is JsonObject relationshipData &&
            relationshipData["currentReputation"] is JsonValue relationshipValue &&
            relationshipValue.TryGetValue<int>(out var relationshipRep))
        {
            return relationshipRep;
        }

        return GetNodeInt(guardian["reputation"], 0);
    }

    private static TradeReputationTier GetTradeReputationTier(int reputation) => reputation switch
    {
        <= -51 => TradeReputationTier.Hostile,
        <= 49 => TradeReputationTier.Neutral,
        <= 129 => TradeReputationTier.Friendly,
        <= 229 => TradeReputationTier.Devoted,
        _ => TradeReputationTier.Legendary
    };

    private static string GetReputationTierLabel(int reputation)
        => ReputationScales.Resolve(ReputationScaleKind.Guardian, reputation).Label;

    private static bool GuardianTradeAllowedHere(JsonObject root, JsonObject guardian)
    {
        var (currentAbodeId, activeGuardianId) = ReadCurrentGuardianTradeContext(root);
        if (string.IsNullOrWhiteSpace(currentAbodeId) || string.IsNullOrWhiteSpace(activeGuardianId))
            return false;

        var guardianId = GetNodeString(guardian["guardianId"]);
        if (!string.Equals(guardianId, activeGuardianId, StringComparison.OrdinalIgnoreCase))
            return false;

        if (guardian["abode"] is not JsonObject abode)
            return false;

        var abodeId = GetNodeString(abode["abodeId"]);
        return !string.IsNullOrWhiteSpace(abodeId) &&
               string.Equals(abodeId, currentAbodeId, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildTradeBlockedReason(JsonObject root, JsonObject guardian, int reputation)
    {
        var (currentAbodeId, activeGuardianId) = ReadCurrentGuardianTradeContext(root);
        var guardianId = GetNodeString(guardian["guardianId"]);
        if (!string.IsNullOrWhiteSpace(activeGuardianId) &&
            !string.Equals(guardianId, activeGuardianId, StringComparison.OrdinalIgnoreCase))
        {
            return "Локальная торговля доступна только у текущего активного Хранителя.";
        }

        if (!GuardianTradeAllowedHere(root, guardian))
            return "Торговать можно только с текущим активным Хранителем в обители, где вы сейчас находитесь.";

        if (reputation <= -51)
            return "Этот Хранитель отказывается торговать из-за вашей репутации.";

        if (string.IsNullOrWhiteSpace(currentAbodeId))
            return "Текущая обитель не определена.";

        return "Торговля недоступна.";
    }

    private static string GetTradeCycleId(int currentIncarnation) => $"return_{Math.Max(0, currentIncarnation)}";

    private static bool TradeInventoryMatchesCycle(JsonObject? tradeInventory, string cycleId, string guardianDomain)
    {
        if (tradeInventory == null)
            return false;

        if (!string.Equals(GetNodeString(tradeInventory["tradeCycleId"]), cycleId, StringComparison.OrdinalIgnoreCase))
            return false;

        var generatedAtUtc = GetNodeString(tradeInventory["generatedAtUtc"]);
        if (string.IsNullOrWhiteSpace(generatedAtUtc))
            return false;

        var generationTier = GetNodeString(tradeInventory["generationReputationTier"]);
        if (!IsValidTradeTierCode(generationTier))
            return false;

        var pricingTier = GetNodeString(tradeInventory["pricingReputationTier"]);
        if (!IsValidTradeTierCode(pricingTier))
            return false;

        var parsedPricingTier = ParseTradeTierCode(pricingTier);

        if (tradeInventory["items"] is not JsonArray items || items.Count != 4)
            return false;

        return items.OfType<JsonObject>().All(item =>
            !string.IsNullOrWhiteSpace(GetNodeString(item["slotId"])) &&
            !string.IsNullOrWhiteSpace(GetNodeString(item["domainTag"])) &&
            string.Equals(GetNodeString(item["domainTag"]), guardianDomain, StringComparison.OrdinalIgnoreCase) &&
            item["priceInFeathers"] is JsonValue &&
            GetNodeInt(item["priceInFeathers"], -1) >= 0 &&
            item["soldOut"] is JsonValue soldOut &&
            (soldOut.TryGetValue<bool>(out _) || bool.TryParse(soldOut.ToString(), out _)) &&
            item["relicData"] is JsonObject relicData &&
            !string.IsNullOrWhiteSpace(GetNodeString(relicData["relicId"])) &&
            !string.IsNullOrWhiteSpace(GetNodeString(relicData["name"])) &&
            (!string.IsNullOrWhiteSpace(GetNodeString(relicData["quality"])) ||
             !string.IsNullOrWhiteSpace(GetNodeString(relicData["rarity"]))) &&
            IsRarityAllowedForGenerationTier(GetRelicRarity(relicData), generationTier!) &&
            GetNodeInt(item["priceInFeathers"], -1) == ComputeBuyPrice(GetRelicRarity(relicData), parsedPricingTier));
    }

    private static bool RepriceTradeInventory(JsonObject tradeInventory, TradeReputationTier tier)
    {
        if (tradeInventory["items"] is not JsonArray items)
            return false;

        var changed = false;
        var tierCode = GetTradeTierCode(tier);
        foreach (var item in items.OfType<JsonObject>())
        {
            if (item["relicData"] is not JsonObject relicData)
                continue;

            var expected = ComputeBuyPrice(GetRelicRarity(relicData), tier);
            if (GetNodeInt(item["priceInFeathers"], -1) != expected)
            {
                item["priceInFeathers"] = expected;
                changed = true;
            }
        }

        if (!string.Equals(GetNodeString(tradeInventory["pricingReputationTier"]), tierCode, StringComparison.OrdinalIgnoreCase))
        {
            tradeInventory["pricingReputationTier"] = tierCode;
            changed = true;
        }

        return changed;
    }

    private static string GetTradeTierCode(TradeReputationTier tier) => tier.ToString();

    private static TradeReputationTier ParseTradeTierCode(string? tierCode) => tierCode switch
    {
        nameof(TradeReputationTier.Hostile) => TradeReputationTier.Hostile,
        nameof(TradeReputationTier.Neutral) => TradeReputationTier.Neutral,
        nameof(TradeReputationTier.Friendly) => TradeReputationTier.Friendly,
        nameof(TradeReputationTier.Devoted) => TradeReputationTier.Devoted,
        nameof(TradeReputationTier.Legendary) => TradeReputationTier.Legendary,
        _ => TradeReputationTier.Hostile
    };

    internal static bool IsValidTradeTierCode(string? tierCode) =>
        tierCode is nameof(TradeReputationTier.Hostile)
            or nameof(TradeReputationTier.Neutral)
            or nameof(TradeReputationTier.Friendly)
            or nameof(TradeReputationTier.Devoted)
            or nameof(TradeReputationTier.Legendary);

    internal static int ComputeBuyPriceForTierCode(string rarity, string tierCode) =>
        ComputeBuyPrice(rarity, ParseTradeTierCode(tierCode));

    internal static bool IsRarityAllowedForGenerationTier(string rarity, string tierCode)
    {
        var rarityRank = GetRarityRank(rarity);
        var maxRank = ParseTradeTierCode(tierCode) switch
        {
            TradeReputationTier.Hostile => 0,
            TradeReputationTier.Neutral => GetRarityRank("Uncommon"),
            TradeReputationTier.Friendly => GetRarityRank("Rare"),
            TradeReputationTier.Devoted => GetRarityRank("Epic"),
            TradeReputationTier.Legendary => GetRarityRank("Epic"),
            _ => 0
        };

        return rarityRank <= maxRank;
    }

    private static string[] GetRarityPattern(TradeReputationTier tier) => tier switch
    {
        TradeReputationTier.Neutral => new[] { "Common", "Common", "Uncommon", "Uncommon" },
        TradeReputationTier.Friendly => new[] { "Common", "Uncommon", "Rare", "Rare" },
        TradeReputationTier.Devoted => new[] { "Uncommon", "Rare", "Epic", "Epic" },
        TradeReputationTier.Legendary => new[] { "Uncommon", "Rare", "Epic", "Epic" },
        _ => Array.Empty<string>()
    };

    private static DomainTradeProfile GetProfile(string domain) =>
        DomainProfiles.TryGetValue(domain, out var profile) ? profile : DomainProfiles["Knowledge"];

    private static string[] PickThemeWords(string[] source, string guardianId, string cycleId, int count)
    {
        var values = source.ToList();
        var random = new Random(ComputeStableSeed($"{guardianId}|{cycleId}|trade"));
        for (var i = values.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }

        return values.Take(count).ToArray();
    }

    private static int ComputeStableSeed(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var c in value)
                hash = hash * 31 + c;
            return hash;
        }
    }

    private static string BuildRelicDescription(DomainTradeProfile profile, string name, int slotIndex) => slotIndex switch
    {
        0 => $"{name} фокусирует чистую силу домена «{GetDomainDisplay(profile.Domain)}» и напрямую усиливает ключевой параметр.",
        1 => $"{name} закрепляет вторичный дар домена «{GetDomainDisplay(profile.Domain)}» и делает носителя устойчивее в профильных задачах.",
        2 => $"{name} помогает лучше раскрывать потенциал домена «{GetDomainDisplay(profile.Domain)}» в важных проверках и ситуациях.",
        _ => $"{name} связывает несколько сторон домена «{GetDomainDisplay(profile.Domain)}» и даёт устойчивый полезный эффект."
    };

    private static int GetPrimaryStatBonus(string rarity) => rarity switch
    {
        "Common" => 3,
        "Uncommon" => 5,
        "Rare" => 8,
        "Epic" => 12,
        _ => 3
    };

    private static int GetSecondaryStatBonus(string rarity) => rarity switch
    {
        "Common" => 1,
        "Uncommon" => 2,
        "Rare" => 4,
        "Epic" => 6,
        _ => 1
    };

    private static int GetActionBonusValue(string rarity) => rarity switch
    {
        "Common" => 1,
        "Uncommon" => 2,
        "Rare" => 4,
        "Epic" => 6,
        _ => 1
    };

    private static int GetEnlightenmentRequirement(string rarity) => rarity switch
    {
        "Common" => 0,
        "Uncommon" => 0,
        "Rare" => 1,
        "Epic" => 2,
        _ => 0
    };

    private static int ComputeBuyPrice(string rarity, TradeReputationTier tier)
    {
        var basePrice = rarity switch
        {
            "Common" => 30,
            "Uncommon" => 70,
            "Rare" => 140,
            "Epic" => 260,
            _ => 30
        };

        var multiplier = tier switch
        {
            TradeReputationTier.Neutral => 1.15,
            TradeReputationTier.Friendly => 1.00,
            TradeReputationTier.Devoted => 0.90,
            TradeReputationTier.Legendary => 0.80,
            _ => double.PositiveInfinity
        };

        return (int)Math.Ceiling(basePrice * multiplier);
    }

    private static int ComputeSellPrice(string rarity, TradeReputationTier tier)
    {
        var basePrice = rarity switch
        {
            "Common" => 10,
            "Uncommon" => 25,
            "Rare" => 60,
            "Epic" => 150,
            "Legendary" => 400,
            _ => 10
        };

        var multiplier = tier switch
        {
            TradeReputationTier.Neutral => 0.85,
            TradeReputationTier.Friendly => 1.00,
            TradeReputationTier.Devoted => 1.10,
            TradeReputationTier.Legendary => 1.20,
            _ => 0.0
        };

        return (int)Math.Floor(basePrice * multiplier);
    }

    private static string DescribeActionBonus(string key, int value) => key switch
    {
        "combatBonus" => $"+{value} к боевым проверкам",
        "magicBonus" => $"+{value} к магическим проверкам",
        "socialBonus" => $"+{value} к социальным проверкам",
        "skillBonus" => $"+{value} к профильным проверкам навыков",
        "globalBonus" => $"+{value} ко всем проверкам действия",
        _ => $"+{value} к проверкам домена"
    };

    private static string GetDomainDisplay(string domain) => domain switch
    {
        "Combat" => "Бой",
        "Magic" => "Магия",
        "Trade" => "Торговля",
        "Social" => "Общение",
        "Crafting" => "Ремесло",
        "Survival" => "Выживание",
        "Knowledge" => "Знания",
        _ => domain
    };

    private static int GetRarityRank(string rarity) => rarity switch
    {
        "Common" => 1,
        "Uncommon" => 2,
        "Rare" => 3,
        "Epic" => 4,
        "Legendary" => 5,
        _ => 0
    };

    private static string GetRelicRarity(JsonObject relic) =>
        GetNodeString(relic["quality"]) ??
        GetNodeString(relic["rarity"]) ??
        "Common";

    private static bool TryModifyInkFeathers(JsonObject soulRoot, int delta)
    {
        var node = soulRoot["inkFeathers"];
        if (node is JsonObject obj)
        {
            var current = GetNodeInt(obj["current"], 0);
            var next = current + delta;
            if (next < 0)
                return false;

            obj["current"] = next;
            return true;
        }

        var currentNumeric = GetNodeInt(node, 0);
        var nextNumeric = currentNumeric + delta;
        if (nextNumeric < 0)
            return false;

        soulRoot["inkFeathers"] = nextNumeric;
        return true;
    }

    private static void NormalizeSoulRelicsShape(JsonObject root)
    {
        if (root["soulRelics"] is JsonArray flatRelics)
        {
            var equipped = new JsonArray();
            var stored = new JsonArray();
            foreach (var relic in flatRelics.OfType<JsonObject>())
            {
                var clone = CloneObject(relic);
                var isEquipped = clone["gameplayStatus"] is JsonObject gameplayStatus &&
                                 gameplayStatus["equipped"] is JsonValue eqValue &&
                                 eqValue.TryGetValue<bool>(out var eq) && eq;
                if (isEquipped)
                    equipped.Add(clone);
                else
                    stored.Add(clone);
            }

            root["soulRelics"] = new JsonObject
            {
                ["equipped"] = equipped,
                ["stored"] = stored
            };
        }
        else if (root["soulRelics"] is JsonObject soulRelics)
        {
            if (soulRelics["equipped"] is not JsonArray)
                soulRelics["equipped"] = new JsonArray();
            if (soulRelics["stored"] is not JsonArray)
                soulRelics["stored"] = new JsonArray();
        }
        else
        {
            root["soulRelics"] = new JsonObject
            {
                ["equipped"] = new JsonArray(),
                ["stored"] = new JsonArray()
            };
        }
    }

    private static void UpsertRelic(JsonArray array, JsonObject relic)
    {
        var relicId = GetNodeString(relic["relicId"]) ?? GetNodeString(relic["id"]);
        if (string.IsNullOrWhiteSpace(relicId))
        {
            array.Add(relic.DeepClone());
            return;
        }

        for (var i = 0; i < array.Count; i++)
        {
            if (array[i] is not JsonObject existing)
                continue;

            var existingId = GetNodeString(existing["relicId"]) ?? GetNodeString(existing["id"]);
            if (string.Equals(existingId, relicId, StringComparison.OrdinalIgnoreCase))
            {
                array[i] = relic.DeepClone();
                return;
            }
        }

        array.Add(relic.DeepClone());
    }

    private static JsonObject? TakeRelic(JsonArray array, string relicId)
    {
        for (var i = 0; i < array.Count; i++)
        {
            if (array[i] is not JsonObject existing)
                continue;

            var existingId = GetNodeString(existing["relicId"]) ?? GetNodeString(existing["id"]);
            if (string.Equals(existingId, relicId, StringComparison.OrdinalIgnoreCase))
            {
                array.RemoveAt(i);
                return existing;
            }
        }

        return null;
    }

    private static JsonObject CloneObject(JsonObject source) => source.DeepClone() as JsonObject ?? new JsonObject();

    private static string? GetNodeString(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var str))
                return str;
            return value.ToJsonString().Trim('"');
        }

        return null;
    }

    private static int GetNodeInt(JsonNode? node, int fallback = 0)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var intValue))
                return intValue;
            if (value.TryGetValue<double>(out var dblValue))
                return (int)dblValue;
            if (int.TryParse(value.ToJsonString().Trim('"'), out var parsed))
                return parsed;
        }

        return fallback;
    }

    private static bool GetNodeBool(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<bool>(out var boolValue))
            return boolValue;
        return false;
    }

    private static string SanitizeId(string value)
    {
        var chars = value
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '_')
            .ToArray();
        return new string(chars).Trim('_');
    }
}
