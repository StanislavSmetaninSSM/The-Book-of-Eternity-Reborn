using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public sealed partial class NpcTradeService
{
    private readonly FileSystemManager _fs;
    private readonly ILogger<NpcTradeService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    private const string NpcCorePath = "game_state/npcs/npc_core.json";
    private const string ItemsPath = "game_state/inventory/items.json";
    private const string PlayerStatusPath = "game_state/core/player_status.json";
    private const string WorldTimePath = "game_state/world/world_time.json";
    private const int RefreshWindowMinutes = 30 * 24 * 60;
    private const string BuybackInventoryProperty = "buybackInventory";
    private const string BuybackStatusAvailable = "available";
    private const string BuybackStatusRebought = "rebought";
    private const string BuybackStatusRemoved = "removed";

    public NpcTradeService(FileSystemManager fs, ILogger<NpcTradeService> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public async Task<NpcTradeView?> EnsureTradeInventoryAsync(string npcId, int currentTurn, bool createPendingRequests = true)
    {
        if (currentTurn <= 0)
            throw new ArgumentOutOfRangeException(nameof(currentTurn), "Подготовка или проверка витрины НПС требует актуальный номер хода.");

        var npcRoot = await ReadNpcRootAsync();
        var itemsRoot = await ReadInventoryRootAsync();
        var statusRoot = await ReadPlayerStatusRootAsync();
        if (npcRoot == null || itemsRoot == null || statusRoot == null)
            return null;

        var npc = FindNpcEntry(npcRoot, npcId);
        if (npc == null)
            return null;

        var currentWorldMinutes = await ResolveCurrentWorldMinutesAsync();
        var (changed, view) = await EnsureNpcTradeInventoryStateAsync(
            npcRoot,
            npc,
            statusRoot,
            currentWorldMinutes,
            currentTurn,
            createPendingRequests);
        if (changed)
            await _fs.WriteFileAtomicAsync(NpcCorePath, npcRoot.ToJsonString(JsonOpts));

        return view;
    }

    public async Task<IReadOnlyList<NpcSellOffer>> GetSellableItemsAsync(string npcId)
    {
        var npcRoot = await ReadNpcRootAsync();
        var itemsRoot = await ReadInventoryRootAsync();
        var statusRoot = await ReadPlayerStatusRootAsync();
        if (npcRoot == null || itemsRoot == null || statusRoot == null)
            return Array.Empty<NpcSellOffer>();

        var npc = FindNpcEntry(npcRoot, npcId);
        if (npc == null)
            return Array.Empty<NpcSellOffer>();

        if (!NpcTradeAllowedHere(npc, out _))
            return Array.Empty<NpcSellOffer>();

        var npcTrade = ReadNpcTradeValue(npc);
        var playerTrade = await ReadPlayerTradeAsync();
        var relation = ReadNpcRelationshipLevel(npc);
        var pricingTier = GetPricingTier(relation);

        NormalizeInventoryShape(itemsRoot);
        var items = itemsRoot["items"]?.AsArray();
        if (items == null)
            return Array.Empty<NpcSellOffer>();

        var equippedRefs = CollectEquippedItemReferences(itemsRoot);
        return items.OfType<JsonObject>()
            .Where(item => !IsQuestBoundItem(item))
            .Where(item => !IsSoulRelicLikeItem(item))
            .Where(item =>
            {
                var itemId = GetNodeString(item["itemId"]) ?? GetNodeString(item["id"]) ?? GetNodeString(item["existedId"]);
                return string.IsNullOrWhiteSpace(itemId) || !equippedRefs.Contains(itemId);
            })
            .Select(item =>
            {
                var rarity = GetItemRarity(item);
                var baseSellPrice = GetBaseSellPrice(item, rarity);
                return new NpcSellOffer(
                    GetNodeString(item["itemId"]) ?? GetNodeString(item["id"]) ?? GetNodeString(item["existedId"]) ?? "",
                    GetNodeString(item["name"]) ?? "Неизвестный товар",
                    rarity,
                    ComputeSellPrice(baseSellPrice, playerTrade, npcTrade, pricingTier),
                    GetNodeString(item["description"]) ?? "",
                    CloneObject(item));
            })
            .Where(offer => !string.IsNullOrWhiteSpace(offer.ItemId))
            .OrderByDescending(offer => GetRarityRank(offer.Rarity))
            .ThenBy(offer => offer.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<NpcTradeOperationResult> BuyAsync(string npcId, string slotId, int currentTurn)
    {
        if (currentTurn <= 0)
            return new NpcTradeOperationResult(false, false, "Локальная покупка товара требует актуальный номер хода.");

        var npcRoot = await ReadNpcRootAsync();
        var itemsRoot = await ReadInventoryRootAsync();
        var statusRoot = await ReadPlayerStatusRootAsync();
        if (npcRoot == null || itemsRoot == null || statusRoot == null)
            return new NpcTradeOperationResult(false, false, "Не удалось прочитать состояние торговли, инвентаря или денег.");

        var npc = FindNpcEntry(npcRoot, npcId);
        if (npc == null)
            return new NpcTradeOperationResult(false, false, "Торговец не найден.");

        var currentWorldMinutes = await ResolveCurrentWorldMinutesAsync();
        var (changed, view) = await EnsureNpcTradeInventoryStateAsync(npcRoot, npc, statusRoot, currentWorldMinutes, currentTurn);
        if (view == null)
            return new NpcTradeOperationResult(false, false, "Не удалось подготовить витрину торговца.");
        if (view.TradeBlocked)
            return new NpcTradeOperationResult(false, false, view.BlockReason ?? "Торговля недоступна.");
        if (!view.InventoryReady)
            return new NpcTradeOperationResult(false, false, view.InventoryStatusMessage ?? "Витрина торговца ещё подготавливается.");

        if (npc["tradeInventory"] is not JsonObject tradeInventory || tradeInventory["items"] is not JsonArray items)
            return new NpcTradeOperationResult(false, false, "Витрина торговца недоступна.");

        var slot = items.OfType<JsonObject>().FirstOrDefault(item =>
            string.Equals(GetNodeString(item["slotId"]), slotId, StringComparison.OrdinalIgnoreCase));
        if (slot == null)
            return new NpcTradeOperationResult(false, false, "Выбранный товар не найден.");

        if (GetNodeBool(slot["soldOut"]))
            return new NpcTradeOperationResult(false, false, "Этот товар уже выкуплен в текущем ассортименте.");

        var price = GetNodeInt(slot["price"], 0);
        if (price <= 0)
            return new NpcTradeOperationResult(false, false, "Цена товара повреждена.");

        var money = GetNodeInt(statusRoot["money"], 0);
        if (money < price)
            return new NpcTradeOperationResult(false, false, "Недостаточно денег.");

        if (slot["itemData"] is not JsonObject itemData)
            return new NpcTradeOperationResult(false, false, "Данные товара повреждены.");

        NormalizeInventoryShape(itemsRoot);
        var inventoryItems = itemsRoot["items"]!.AsArray();
        UpsertInventoryItem(inventoryItems, CloneObject(itemData));
        statusRoot["money"] = money - price;
        slot["soldOut"] = true;
        SyncNpcEntries(npcRoot, GetNpcIdentity(npc), npc);

        await _fs.WriteFileAtomicAsync(ItemsPath, itemsRoot.ToJsonString(JsonOpts));
        await _fs.WriteFileAtomicAsync(PlayerStatusPath, statusRoot.ToJsonString(JsonOpts));
        await _fs.WriteFileAtomicAsync(NpcCorePath, npcRoot.ToJsonString(JsonOpts));

        var itemName = GetNodeString(itemData["name"]) ?? "Товар";
        return new NpcTradeOperationResult(true, true, $"Куплен товар «{itemName}» за {price}.");
    }

    public async Task<NpcTradeOperationResult> SellAsync(string npcId, string itemId, int currentTurn)
    {
        if (currentTurn <= 0)
            return new NpcTradeOperationResult(false, false, "Локальная продажа товара требует актуальный номер хода.");

        var npcRoot = await ReadNpcRootAsync();
        var itemsRoot = await ReadInventoryRootAsync();
        var statusRoot = await ReadPlayerStatusRootAsync();
        if (npcRoot == null || itemsRoot == null || statusRoot == null)
            return new NpcTradeOperationResult(false, false, "Не удалось прочитать состояние торговли, инвентаря или денег.");

        var npc = FindNpcEntry(npcRoot, npcId);
        if (npc == null)
            return new NpcTradeOperationResult(false, false, "Торговец не найден.");

        if (!NpcTradeAllowedHere(npc, out var blockedReason))
            return new NpcTradeOperationResult(false, false, blockedReason ?? "Торговля недоступна.");

        NormalizeInventoryShape(itemsRoot);
        var items = itemsRoot["items"]?.AsArray();
        if (items == null)
            return new NpcTradeOperationResult(false, false, "Инвентарь недоступен.");

        var equippedRefs = CollectEquippedItemReferences(itemsRoot);
        if (equippedRefs.Contains(itemId))
            return new NpcTradeOperationResult(false, false, "Экипированный предмет нельзя продать из этой панели.");

        var itemIndex = FindInventoryItemIndex(items, itemId);
        if (itemIndex < 0)
            return new NpcTradeOperationResult(false, false, "Товар не найден в инвентаре.");

        if (items[itemIndex] is not JsonObject item)
            return new NpcTradeOperationResult(false, false, "Данные товара повреждены.");
        if (IsQuestBoundItem(item))
            return new NpcTradeOperationResult(false, false, "Этот предмет нельзя продать через локальную торговлю.");
        if (IsSoulRelicLikeItem(item))
            return new NpcTradeOperationResult(false, false, "Реликвии души нельзя продать через локальную торговлю НПС.");

        var npcTrade = ReadNpcTradeValue(npc);
        var playerTrade = await ReadPlayerTradeAsync();
        var relation = ReadNpcRelationshipLevel(npc);
        var pricingTier = GetPricingTier(relation);
        var currentWorldMinutes = await ResolveCurrentWorldMinutesAsync();
        var rarity = GetItemRarity(item);
        var baseSellPrice = GetBaseSellPrice(item, rarity);
        var price = ComputeSellPrice(baseSellPrice, playerTrade, npcTrade, pricingTier);
        if (price <= 0)
            return new NpcTradeOperationResult(false, false, "Цена продажи повреждена.");

        var merchantProfile = ResolveMerchantProfile(npc)?.Key ?? DefaultMerchantProfileKey;
        var buybackInventory = EnsureBuybackInventoryArray(npc);
        buybackInventory.Add(CreateBuybackEntry(
            npcId,
            GetNodeString(npc["name"]) ?? npcId,
            CloneObject(item),
            merchantProfile,
            price,
            Math.Max(0, currentTurn),
            currentWorldMinutes));

        items.RemoveAt(itemIndex);
        statusRoot["money"] = GetNodeInt(statusRoot["money"], 0) + price;
        SyncNpcEntries(npcRoot, npcId, npc);

        await _fs.WriteFileAtomicAsync(ItemsPath, itemsRoot.ToJsonString(JsonOpts));
        await _fs.WriteFileAtomicAsync(PlayerStatusPath, statusRoot.ToJsonString(JsonOpts));
        await _fs.WriteFileAtomicAsync(NpcCorePath, npcRoot.ToJsonString(JsonOpts));

        var itemName = GetNodeString(item["name"]) ?? "Товар";
        return new NpcTradeOperationResult(true, true, $"Продан товар «{itemName}» за {price}.");
    }

    public async Task<NpcTradeOperationResult> BuyBackAsync(string npcId, string buybackEntryId, int currentTurn)
    {
        if (currentTurn <= 0)
            return new NpcTradeOperationResult(false, false, "Локальный выкуп товара требует актуальный номер хода.");

        var npcRoot = await ReadNpcRootAsync();
        var itemsRoot = await ReadInventoryRootAsync();
        var statusRoot = await ReadPlayerStatusRootAsync();
        if (npcRoot == null || itemsRoot == null || statusRoot == null)
            return new NpcTradeOperationResult(false, false, "Не удалось прочитать состояние торговли, инвентаря или денег.");

        var npc = FindNpcEntry(npcRoot, npcId);
        if (npc == null)
            return new NpcTradeOperationResult(false, false, "Торговец не найден.");

        if (!NpcTradeAllowedHere(npc, out var blockedReason))
            return new NpcTradeOperationResult(false, false, blockedReason ?? "Торговля недоступна.");

        if (npc[BuybackInventoryProperty] is not JsonArray buybackInventory)
            return new NpcTradeOperationResult(false, false, "У этого торговца нет товаров для обратного выкупа.");

        var buybackEntry = buybackInventory
            .OfType<JsonObject>()
            .FirstOrDefault(entry =>
                string.Equals(GetNodeString(entry["buybackEntryId"]), buybackEntryId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GetNodeString(entry["status"]), BuybackStatusAvailable, StringComparison.OrdinalIgnoreCase));
        if (buybackEntry == null)
            return new NpcTradeOperationResult(false, false, "Этот товар больше недоступен для обратного выкупа.");

        if (buybackEntry["itemData"] is not JsonObject itemData)
            return new NpcTradeOperationResult(false, false, "Данные товара для обратного выкупа повреждены.");

        var price = GetNodeInt(buybackEntry["buybackPrice"], GetNodeInt(buybackEntry["soldForPrice"], 0));
        if (price <= 0)
            return new NpcTradeOperationResult(false, false, "Цена обратного выкупа повреждена.");

        var money = GetNodeInt(statusRoot["money"], 0);
        if (money < price)
            return new NpcTradeOperationResult(false, false, "Недостаточно денег.");

        NormalizeInventoryShape(itemsRoot);
        var inventoryItems = itemsRoot["items"]!.AsArray();
        UpsertInventoryItem(inventoryItems, CloneObject(itemData));
        statusRoot["money"] = money - price;

        buybackEntry["status"] = BuybackStatusRebought;
        buybackEntry["reboughtAtTurn"] = Math.Max(0, currentTurn);
        buybackEntry["reboughtAtUtc"] = DateTimeOffset.UtcNow.ToString("O");
        SyncNpcEntries(npcRoot, npcId, npc);

        await _fs.WriteFileAtomicAsync(ItemsPath, itemsRoot.ToJsonString(JsonOpts));
        await _fs.WriteFileAtomicAsync(PlayerStatusPath, statusRoot.ToJsonString(JsonOpts));
        await _fs.WriteFileAtomicAsync(NpcCorePath, npcRoot.ToJsonString(JsonOpts));

        var itemName = GetNodeString(itemData["name"]) ?? "Товар";
        return new NpcTradeOperationResult(true, true, $"Выкуплен обратно товар «{itemName}» за {price}.");
    }

    internal static bool IsValidGenerationTierCode(string? tierCode) =>
        tierCode is nameof(GenerationTradeTier.Poor)
            or nameof(GenerationTradeTier.Standard)
            or nameof(GenerationTradeTier.Good)
            or nameof(GenerationTradeTier.Premium)
            or nameof(GenerationTradeTier.Elite);

    internal static bool IsValidPricingTierCode(string? tierCode) =>
        tierCode is nameof(PricingTradeTier.Hostile)
            or nameof(PricingTradeTier.Wary)
            or nameof(PricingTradeTier.Neutral)
            or nameof(PricingTradeTier.Warm)
            or nameof(PricingTradeTier.Trusted);

    internal static bool IsRarityAllowedForGenerationTier(string rarity, string tierCode)
    {
        var rarityRank = GetRarityRank(rarity);
        var maxRank = tierCode switch
        {
            nameof(GenerationTradeTier.Poor) => GetRarityRank("Common"),
            nameof(GenerationTradeTier.Standard) => GetRarityRank("Uncommon"),
            nameof(GenerationTradeTier.Good) => GetRarityRank("Rare"),
            nameof(GenerationTradeTier.Premium) => GetRarityRank("Epic"),
            nameof(GenerationTradeTier.Elite) => GetRarityRank("Epic"),
            _ => 0
        };
        return rarityRank <= maxRank;
    }

    internal static bool IsValidMerchantProfileCode(string? profileCode) =>
        TryNormalizeMerchantProfileCode(profileCode, out _);

    internal static bool IsValidTradeItemClassCode(string? tradeItemClass) =>
        tradeItemClass is "Functional" or "Material" or "FlavorOrUtility";

    internal static bool IsValidBuybackStatusCode(string? statusCode) =>
        statusCode is BuybackStatusAvailable or BuybackStatusRebought or BuybackStatusRemoved;

    internal static string GetMerchantProfileDisplayName(string? profileCode)
    {
        if (TryNormalizeMerchantProfileCode(profileCode, out var normalizedProfile) &&
            MerchantProfiles.TryGetValue(normalizedProfile, out var profile))
            return profile.DisplayName;

        return "Товары смертной жизни";
    }

    internal static string GetTradeItemClassDisplayName(string? tradeItemClass) => tradeItemClass switch
    {
        "Functional" => "Функциональный",
        "Material" => "Материальный",
        "FlavorOrUtility" => "Бытовой или утилитарный",
        _ => "Неизвестный"
    };

    internal static string? ResolveMerchantProfileCode(string? explicitProfile, params string?[] sourceParts)
    {
        if (TryNormalizeMerchantProfileCode(explicitProfile, out var normalizedProfile))
            return normalizedProfile;

        var source = string.Join(" ", sourceParts.Where(s => !string.IsNullOrWhiteSpace(s))).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(source))
            return null;

        if (ContainsAny(source, "контраб", "тенев", "подполь", "smugg", "black market", "fence", "fixer"))
            return "IllicitGoods";
        if (ContainsAny(source, "инжен", "техн", "mechanic", "engineer", "technician", "cyber", "электр", "diagnostic", "repair depot"))
            return "TechnicalGoods";
        if (ContainsAny(source, "антиквар", "artifact", "curio", "collect", "relic", "диковин", "коллекц"))
            return "ArtifactsAndCurios";
        if (ContainsAny(source, "декор", "роскош", "luxury", "jewel", "atelier", "tailor", "furniture", "gallery", "salon", "ювел"))
            return "LuxuryAndDecor";
        if (ContainsAny(source, "книг", "архив", "scribe", "scholar", "library", "bookseller", "media", "editor", "printer", "document", "учен", "редак"))
            return "KnowledgeAndMedia";
        if (ContainsAny(source, "аптек", "зель", "alchem", "grocer", "provision", "baker", "cook", "innkeep", "food", "bar", "cafe", "consum"))
            return "Consumables";
        if (ContainsAny(source, "оруж", "брон", "gear", "equipment", "armorer", "smith", "кузнец", "outfit", "quartermaster", "патрон"))
            return "Equipment";
        if (ContainsAny(source, "ремес", "materials", "supplier", "hardware", "workshop", "реагент", "мастерск", "склад", "fabric", "textile"))
            return "CraftingSupplies";
        if (ContainsAny(source, "торгов", "merchant", "trader", "vendor", "shopkeep", "market", "лавк"))
            return "GeneralGoods";

        return null;
    }

    internal static NpcTradeAvailability EvaluateTradeAvailability(JsonElement npc, string currentLocationId, string currentLocationName)
    {
        var merchantProfile = ResolveMerchantProfileCode(
            npc.TryGetProperty("tradeState", out var tradeState) && tradeState.ValueKind == JsonValueKind.Object
                ? GetFirstNonEmptyString(tradeState, "merchantProfile")
                : null,
            GetFirstNonEmptyString(npc, "role"),
            GetFirstNonEmptyString(npc, "occupation"),
            GetFirstNonEmptyString(npc, "class"),
            GetFirstNonEmptyString(npc, "name"));

        return BuildTradeAvailability(
            merchantProfile,
            GetFirstNonEmptyString(npc, "currentLocationId") ?? "",
            GetFirstNonEmptyString(npc, "currentLocation") ?? "",
            currentLocationId,
            currentLocationName,
            npc.TryGetProperty("tradeState", out var tradeStateNode) && tradeStateNode.ValueKind == JsonValueKind.Object
                ? tradeStateNode
                : (JsonElement?)null);
    }

    internal static NpcTradeAvailability EvaluateTradeAvailability(JsonObject npc, string currentLocationId, string currentLocationName)
    {
        var tradeState = npc["tradeState"] as JsonObject;
        var merchantProfile = ResolveMerchantProfileCode(
            GetNodeString(tradeState?["merchantProfile"]),
            GetNodeString(npc["role"]),
            GetNodeString(npc["occupation"]),
            GetNodeString(npc["class"]),
            GetNodeString(npc["name"]));

        return BuildTradeAvailability(
            merchantProfile,
            GetNodeString(npc["currentLocationId"]) ?? "",
            GetNodeString(npc["currentLocation"]) ?? "",
            currentLocationId,
            currentLocationName,
            tradeState);
    }

    internal static int ComputeBuyPriceForValidation(int basePrice, int playerTrade, int npcTrade, string pricingTierCode) =>
        ComputeBuyPrice(basePrice, playerTrade, npcTrade, ParsePricingTierCode(pricingTierCode));

    internal static int ComputeSellPriceForValidation(int baseSellPrice, int playerTrade, int npcTrade, string pricingTierCode) =>
        ComputeSellPrice(baseSellPrice, playerTrade, npcTrade, ParsePricingTierCode(pricingTierCode));

    internal static int ResolveWorldMinutes(JsonElement root)
    {
        if (TryReadIntLike(root, "currentTimeInMinutes", out var absolute))
            return absolute;

        var year = TryReadIntLike(root, "year", out var parsedYear) ? parsedYear : 0;
        var day = TryReadIntLike(root, "day", out var parsedDay)
            ? parsedDay
            : (TryReadIntLike(root, "dayOfMonth", out parsedDay) ? parsedDay : 1);
        var minutes = MapTimeOfDayToMinutes(GetFirstNonEmptyString(root, "timeOfDay") ?? "Morning");
        return Math.Max(0, ((year * 400) + Math.Max(0, day - 1)) * 1440 + minutes);
    }

    private async Task<(bool Changed, NpcTradeView? View)> EnsureNpcTradeInventoryStateAsync(
        JsonObject root,
        JsonObject npc,
        JsonObject statusRoot,
        int currentWorldMinutes,
        int currentTurn,
        bool createPendingRequests = true)
    {
        var npcId = GetNpcIdentity(npc);
        var npcName = GetNodeString(npc["name"]) ?? npcId;
        var blocked = !NpcTradeAllowedHere(npc, out var blockedReason);
        var npcTrade = ReadNpcTradeValue(npc);
        var playerTrade = ReadPlayerTradeSync();
        var relation = ReadNpcRelationshipLevel(npc);
        var generationTier = GetGenerationTier(ReadNpcLevel(npc), npcTrade, relation);
        var pricingTier = GetPricingTier(relation);
        var profile = ResolveMerchantProfile(npc);
        var merchantProfile = profile?.Key ?? DefaultMerchantProfileKey;
        var tradeCycleId = GetTradeCycleId(currentWorldMinutes);
        var refreshAfterWorldMinutes = GetRefreshAfterWorldMinutesForCycle(currentWorldMinutes);
        var changed = false;
        var inventoryReady = false;
        var inventoryRequestPending = false;
        var inventoryRequestCreatedThisCall = false;
        string? inventoryStatusMessage = null;
        string? pendingGmAction = null;

        if (!blocked)
        {
            var inventory = npc["tradeInventory"] as JsonObject;
            if (TradeInventoryMatchesCurrentContract(inventory, currentWorldMinutes, npc, playerTrade, npcTrade))
            {
                inventoryReady = true;
                if (inventory != null)
                {
                    changed = RepriceTradeInventory(inventory, playerTrade, npcTrade, pricingTier);
                    if (changed)
                        SyncNpcEntries(root, npcId, npc);
                }

                var request = await NpcTradeRequestState.FindPendingRequestAsync(_fs, npcId, tradeCycleId);
                var requestMatchesCurrentContract = NpcTradeRequestState.MatchesCurrentContract(
                    request,
                    npcId,
                    merchantProfile,
                    tradeCycleId,
                    ComputeSlotCount(ReadNpcLevel(npc), npcTrade),
                    refreshAfterWorldMinutes);
                var hasMatchingReceipt = requestMatchesCurrentContract &&
                    NpcTradeRequestState.ReceiptMatchesRequestContract(
                        NpcTradeRequestState.FindMatchingReceipt(npc, request!),
                        request!,
                        inventory);

                if (request != null && (!requestMatchesCurrentContract || hasMatchingReceipt))
                    await NpcTradeRequestState.EnsureHealthyAsync(_fs, "Mortal World");
            }
            else
            {
                var derivedTradeSlotCount = ComputeSlotCount(ReadNpcLevel(npc), npcTrade);
                var request = await NpcTradeRequestState.FindPendingRequestAsync(_fs, npcId, tradeCycleId);
                inventoryRequestPending = NpcTradeRequestState.MatchesCurrentContract(
                    request,
                    npcId,
                    merchantProfile,
                    tradeCycleId,
                    derivedTradeSlotCount,
                    refreshAfterWorldMinutes);

                if (!inventoryRequestPending && createPendingRequests)
                {
                    request = new NpcTradeRequestState.PendingNpcTradeInventoryRequest
                    {
                        NpcId = npcId,
                        NpcName = npcName,
                        MerchantProfile = merchantProfile,
                        TradeCycleId = tradeCycleId,
                        DerivedTradeSlotCount = derivedTradeSlotCount,
                        CreatedAtTurn = Math.Max(0, currentTurn),
                        CreatedAtWorldDate = currentWorldMinutes,
                        RefreshAfterWorldDate = refreshAfterWorldMinutes
                    };
                    await NpcTradeRequestState.WriteRequestAsync(_fs, request);
                    inventoryRequestPending = true;
                    inventoryRequestCreatedThisCall = true;
                    pendingGmAction =
                        $"[{NpcTradeRequestState.ActionTag}] Игрок открывает торговлю с NPC {npcName} ({npcId}), но explicit витрина отсутствует или устарела для текущего world-time cycle. " +
                        $"Обязательно прочитай {NpcTradeRequestState.PendingRequestPath} как client-authored contract. " +
                        "Материализуй explicit npc.tradeInventory для указанного tradeCycleId и не выводи ассортимент клиентом. " +
                        $"После materialization закрой запрос canonical receipt через {NpcTradeRequestState.UpdateReceiptsProperty} в npc_core.json. " +
                        "Витрина должна уважать merchantProfile, tradeCycleId, refreshAfterWorldDate и derivedTradeSlotCount из request.";
                }

                inventoryStatusMessage = inventoryRequestCreatedThisCall
                    ? "Витрина торговца подготавливается. Запрос на ассортимент отправлен GM."
                    : inventoryRequestPending
                        ? "Витрина торговца ещё подготавливается. Повторите после ответа GM."
                        : "Для этой торговли нужно запросить ассортимент торговца.";
            }
        }

        var view = BuildTradeView(
            npc,
            statusRoot,
            currentWorldMinutes,
            blocked,
            blockedReason,
            tradeCycleId,
            inventoryReady,
            inventoryRequestPending,
            inventoryRequestCreatedThisCall,
            inventoryStatusMessage,
            pendingGmAction);
        return (changed, view);
    }

    private NpcTradeView BuildTradeView(
        JsonObject npc,
        JsonObject statusRoot,
        int currentWorldMinutes,
        bool blocked,
        string? blockedReason,
        string tradeCycleId,
        bool inventoryReady,
        bool inventoryRequestPending,
        bool inventoryRequestCreatedThisCall,
        string? inventoryStatusMessage,
        string? pendingGmAction)
    {
        var npcId = GetNpcIdentity(npc);
        var npcName = GetNodeString(npc["name"]) ?? npcId;
        var npcTrade = ReadNpcTradeValue(npc);
        var playerTrade = ReadPlayerTradeSync();
        var relation = ReadNpcRelationshipLevel(npc);
        var profile = ResolveMerchantProfile(npc);
        var offers = new List<NpcTradeOffer>();
        var buybackOffers = ReadBuybackOffers(npc);

        if (!blocked &&
            inventoryReady &&
            npc["tradeInventory"] is JsonObject tradeInventory &&
            tradeInventory["items"] is JsonArray items)
        {
            foreach (var item in items.OfType<JsonObject>())
            {
                if (item["itemData"] is not JsonObject itemData)
                    continue;

                offers.Add(new NpcTradeOffer(
                    GetNodeString(item["slotId"]) ?? "",
                    GetNodeString(itemData["name"]) ?? "Товар",
                    GetItemRarity(itemData),
                    GetNodeInt(item["price"], 0),
                    GetNodeString(itemData["description"]) ?? "",
                    GetNodeString(item["merchantProfile"]) ?? profile?.Key ?? DefaultMerchantProfileKey,
                    GetNodeBool(item["soldOut"]),
                    CloneObject(itemData)));
            }
        }

        return new NpcTradeView(
            npcId,
            npcName,
            profile?.Key ?? DefaultMerchantProfileKey,
            profile?.DisplayName ?? GetMerchantProfileDisplayName(DefaultMerchantProfileKey),
            npcTrade,
            playerTrade,
            relation,
            GetNodeInt(statusRoot["money"], 0),
            blocked,
            blocked ? blockedReason : null,
            tradeCycleId,
            inventoryReady,
            inventoryRequestPending,
            inventoryRequestCreatedThisCall,
            inventoryStatusMessage,
            pendingGmAction,
            currentWorldMinutes,
            GetGeneratedAtWorldMinutes(npc["tradeInventory"] as JsonObject, currentWorldMinutes),
            GetRefreshAfterWorldMinutes(npc["tradeInventory"] as JsonObject, currentWorldMinutes),
            offers,
            buybackOffers);
    }

    private static bool TradeInventoryMatchesCurrentContract(JsonObject? tradeInventory, int currentWorldMinutes, JsonObject npc,
        int playerTrade, int npcTrade)
    {
        if (tradeInventory == null)
            return false;

        var expectedTradeCycleId = GetTradeCycleId(currentWorldMinutes);
        var generatedAt = GetNodeInt(tradeInventory["generatedAtWorldDate"], -1);
        var refreshAfter = GetNodeInt(tradeInventory["refreshAfterWorldDate"], -1);
        var tradeCycleId = GetNodeString(tradeInventory["tradeCycleId"]);
        var generationTierCode = GetNodeString(tradeInventory["generationTradeTier"]);
        var pricingTierCode = GetNodeString(tradeInventory["pricingTradeTier"]);
        if (generatedAt < 0 || refreshAfter <= generatedAt)
            return false;
        if (!string.Equals(tradeCycleId, expectedTradeCycleId, StringComparison.OrdinalIgnoreCase))
            return false;
        if (currentWorldMinutes >= refreshAfter)
            return false;
        if (!IsValidGenerationTierCode(generationTierCode) || !IsValidPricingTierCode(pricingTierCode))
            return false;

        if (tradeInventory["items"] is not JsonArray items)
            return false;
        var expectedSlotCount = ComputeSlotCount(ReadNpcLevel(npc), npcTrade);
        if (items.Count != expectedSlotCount)
            return false;

        var profile = ResolveMerchantProfile(npc);
        if (profile == null)
            return false;

        foreach (var item in items.OfType<JsonObject>())
        {
            if (string.IsNullOrWhiteSpace(GetNodeString(item["slotId"])))
                return false;
            if (!TryNormalizeMerchantProfileCode(GetNodeString(item["merchantProfile"]), out var itemProfile) ||
                !string.Equals(itemProfile, profile.Key, StringComparison.OrdinalIgnoreCase))
                return false;
            if (item["soldOut"] is not JsonValue soldNode || (!soldNode.TryGetValue<bool>(out _) && !bool.TryParse(soldNode.ToString(), out _)))
                return false;
            if (item["itemData"] is not JsonObject itemData)
                return false;

            var rarity = GetItemRarity(itemData);
            if (!IsRarityAllowedForGenerationTier(rarity, generationTierCode!))
                return false;
            if (!IsValidTradeItemClassCode(GetNodeString(itemData["tradeItemClass"])))
                return false;
            var expectedPrice = ComputeBuyPrice(GetBaseBuyPrice(itemData, rarity), playerTrade, npcTrade, ParsePricingTierCode(pricingTierCode!));
            if (GetNodeInt(item["price"], -1) != expectedPrice)
                return false;
            if (string.IsNullOrWhiteSpace(GetNodeString(itemData["itemId"])) ||
                string.IsNullOrWhiteSpace(GetNodeString(itemData["name"])) ||
                GetNodeInt(itemData["price"], 0) <= 0)
                return false;
        }

        return true;
    }

    private static bool RepriceTradeInventory(JsonObject tradeInventory, int playerTrade, int npcTrade, PricingTradeTier pricingTier)
    {
        if (tradeInventory["items"] is not JsonArray items)
            return false;

        var changed = false;
        foreach (var item in items.OfType<JsonObject>())
        {
            if (item["itemData"] is not JsonObject itemData)
                continue;
            var expected = ComputeBuyPrice(GetBaseBuyPrice(itemData, GetItemRarity(itemData)), playerTrade, npcTrade, pricingTier);
            if (GetNodeInt(item["price"], -1) != expected)
            {
                item["price"] = expected;
                changed = true;
            }
        }

        var tierCode = pricingTier.ToString();
        if (!string.Equals(GetNodeString(tradeInventory["pricingTradeTier"]), tierCode, StringComparison.OrdinalIgnoreCase))
        {
            tradeInventory["pricingTradeTier"] = tierCode;
            changed = true;
        }

        return changed;
    }

    private async Task<JsonObject?> ReadNpcRootAsync()
    {
        try
        {
            var json = await _fs.ReadFileAsync(NpcCorePath);
            if (string.IsNullOrWhiteSpace(json))
                return new JsonObject();
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать npc_core.json для локальной торговли NPC");
            return null;
        }
    }

    private async Task<JsonObject?> ReadInventoryRootAsync()
    {
        try
        {
            var json = await _fs.ReadFileAsync(ItemsPath);
            if (string.IsNullOrWhiteSpace(json))
                return new JsonObject();
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать items.json для локальной торговли NPC");
            return null;
        }
    }

    private async Task<JsonObject?> ReadPlayerStatusRootAsync()
    {
        try
        {
            var json = await _fs.ReadFileAsync(PlayerStatusPath);
            if (string.IsNullOrWhiteSpace(json))
                return new JsonObject();
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать player_status.json для локальной торговли NPC");
            return null;
        }
    }

    private async Task<int> ResolveCurrentWorldMinutesAsync()
    {
        try
        {
            var json = await _fs.ReadFileAsync(WorldTimePath);
            if (string.IsNullOrWhiteSpace(json))
                return 0;
            using var doc = JsonDocument.Parse(json);
            return ResolveWorldMinutes(doc.RootElement);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось определить мировое время для локальной торговли NPC");
            return 0;
        }
    }

    private static JsonObject? FindNpcEntry(JsonObject root, string npcId)
    {
        foreach (var arr in EnumerateNpcArrays(root))
        {
            foreach (var item in arr.OfType<JsonObject>())
            {
                if (string.Equals(GetNpcIdentity(item), npcId, StringComparison.OrdinalIgnoreCase))
                    return item;
            }
        }
        return null;
    }

    private static IEnumerable<JsonArray> EnumerateNpcArrays(JsonObject root) =>
        GuardianPolicyContracts.EnumerateCanonicalNpcObjectArrays(root);

    private static void SyncNpcEntries(JsonObject root, string npcId, JsonObject npc)
    {
        foreach (var arr in EnumerateNpcArrays(root))
        {
            for (var i = 0; i < arr.Count; i++)
            {
                if (arr[i] is not JsonObject item)
                    continue;
                if (string.Equals(GetNpcIdentity(item), npcId, StringComparison.OrdinalIgnoreCase))
                    arr[i] = CloneObject(npc);
            }
        }
    }

    private bool NpcTradeAllowedHere(JsonObject npc, out string? blockedReason)
    {
        var (currentLocationId, currentLocationName) = ReadCurrentLocationIdentitySync();
        var availability = EvaluateTradeAvailability(npc, currentLocationId, currentLocationName);
        blockedReason = availability.BlockReason;
        return availability.TradeAvailable;
    }

    private (string locationId, string locationName) ReadCurrentLocationIdentitySync()
    {
        try
        {
            var json = _fs.ReadFileAsync("game_state/world/current_location.json").GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(json))
                return ("", "");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("currentLocationData", out var wrapped) && wrapped.ValueKind == JsonValueKind.Object)
                root = wrapped;

            return (
                GetFirstNonEmptyString(root, "locationId", "currentLocationId") ?? "",
                GetFirstNonEmptyString(root, "name", "currentLocation") ?? "");
        }
        catch
        {
            return ("", "");
        }
    }

    private static MerchantProfile? ResolveMerchantProfile(JsonObject npc)
    {
        var tradeState = npc["tradeState"] as JsonObject;
        var profileCode = ResolveMerchantProfileCode(
            GetNodeString(tradeState?["merchantProfile"]),
            GetNodeString(npc["role"]),
            GetNodeString(npc["occupation"]),
            GetNodeString(npc["class"]),
            GetNodeString(npc["name"]));
        return !string.IsNullOrWhiteSpace(profileCode) && MerchantProfiles.TryGetValue(profileCode, out var profile)
            ? profile
            : null;
    }

    private static NpcTradeAvailability BuildTradeAvailability(
        string? merchantProfile,
        string npcLocationId,
        string npcLocationName,
        string currentLocationId,
        string currentLocationName,
        JsonElement? tradeState)
    {
        if (string.IsNullOrWhiteSpace(merchantProfile))
            return new NpcTradeAvailability(null, GetMerchantProfileDisplayName(null), false, "Этот НПС не является торговцем.");

        if (!IsSameTradeLocation(npcLocationId, npcLocationName, currentLocationId, currentLocationName))
        {
            return new NpcTradeAvailability(
                merchantProfile,
                GetMerchantProfileDisplayName(merchantProfile),
                false,
                "Доступна только в текущей локации торговца.");
        }

        if (tradeState == null || tradeState.Value.ValueKind != JsonValueKind.Object)
        {
            return new NpcTradeAvailability(
                merchantProfile,
                GetMerchantProfileDisplayName(merchantProfile),
                false,
                "Торговля сейчас недоступна.");
        }

        if (!tradeState.Value.TryGetProperty("canTrade", out var canTradeNode) ||
            (canTradeNode.ValueKind != JsonValueKind.True && canTradeNode.ValueKind != JsonValueKind.False))
        {
            return new NpcTradeAvailability(
                merchantProfile,
                GetMerchantProfileDisplayName(merchantProfile),
                false,
                "Торговля сейчас недоступна.");
        }

        if (canTradeNode.ValueKind == JsonValueKind.False)
        {
            return new NpcTradeAvailability(
                merchantProfile,
                GetMerchantProfileDisplayName(merchantProfile),
                false,
                GetFirstNonEmptyString(tradeState.Value, "tradeBlockedReason") ?? "Торговля сейчас недоступна.");
        }

        return new NpcTradeAvailability(
            merchantProfile,
            GetMerchantProfileDisplayName(merchantProfile),
            true,
            null);
    }

    private static NpcTradeAvailability BuildTradeAvailability(
        string? merchantProfile,
        string npcLocationId,
        string npcLocationName,
        string currentLocationId,
        string currentLocationName,
        JsonObject? tradeState)
    {
        if (string.IsNullOrWhiteSpace(merchantProfile))
            return new NpcTradeAvailability(null, GetMerchantProfileDisplayName(null), false, "Этот НПС не является торговцем.");

        if (!IsSameTradeLocation(npcLocationId, npcLocationName, currentLocationId, currentLocationName))
        {
            return new NpcTradeAvailability(
                merchantProfile,
                GetMerchantProfileDisplayName(merchantProfile),
                false,
                "Доступна только в текущей локации торговца.");
        }

        if (tradeState == null)
        {
            return new NpcTradeAvailability(
                merchantProfile,
                GetMerchantProfileDisplayName(merchantProfile),
                false,
                "Торговля сейчас недоступна.");
        }

        if (tradeState["canTrade"] is not JsonValue canTradeValue ||
            !canTradeValue.TryGetValue<bool>(out var canTrade))
        {
            return new NpcTradeAvailability(
                merchantProfile,
                GetMerchantProfileDisplayName(merchantProfile),
                false,
                "Торговля сейчас недоступна.");
        }

        if (!canTrade)
        {
            return new NpcTradeAvailability(
                merchantProfile,
                GetMerchantProfileDisplayName(merchantProfile),
                false,
                GetNodeString(tradeState["tradeBlockedReason"]) ?? "Торговля сейчас недоступна.");
        }

        return new NpcTradeAvailability(
            merchantProfile,
            GetMerchantProfileDisplayName(merchantProfile),
            true,
            null);
    }

    private static bool IsSameTradeLocation(string npcLocationId, string npcLocationName, string currentLocationId, string currentLocationName)
    {
        return
            (!string.IsNullOrWhiteSpace(currentLocationId) && string.Equals(currentLocationId, npcLocationId, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(currentLocationName) && string.Equals(currentLocationName, npcLocationName, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(currentLocationId) && string.Equals(currentLocationId, npcLocationName, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(currentLocationName) && string.Equals(currentLocationName, npcLocationId, StringComparison.OrdinalIgnoreCase));
    }

    private static GenerationTradeTier GetGenerationTier(int level, int npcTrade, int relationshipLevel)
    {
        var relationBonus = relationshipLevel switch
        {
            >= 251 => 18,
            >= 101 => 12,
            >= 0 => 6,
            >= -50 => 0,
            _ => -8
        };
        var score = level + npcTrade + relationBonus;
        return score switch
        {
            < 20 => GenerationTradeTier.Poor,
            < 35 => GenerationTradeTier.Standard,
            < 52 => GenerationTradeTier.Good,
            < 70 => GenerationTradeTier.Premium,
            _ => GenerationTradeTier.Elite
        };
    }

    private static PricingTradeTier GetPricingTier(int relationshipLevel) => relationshipLevel switch
    {
        < -100 => PricingTradeTier.Hostile,
        < 0 => PricingTradeTier.Wary,
        < 101 => PricingTradeTier.Neutral,
        < 251 => PricingTradeTier.Warm,
        _ => PricingTradeTier.Trusted
    };

    private static PricingTradeTier ParsePricingTierCode(string tierCode) => tierCode switch
    {
        nameof(PricingTradeTier.Hostile) => PricingTradeTier.Hostile,
        nameof(PricingTradeTier.Wary) => PricingTradeTier.Wary,
        nameof(PricingTradeTier.Neutral) => PricingTradeTier.Neutral,
        nameof(PricingTradeTier.Warm) => PricingTradeTier.Warm,
        nameof(PricingTradeTier.Trusted) => PricingTradeTier.Trusted,
        _ => PricingTradeTier.Neutral
    };

    private static int ComputeSlotCount(int level, int trade) => Math.Clamp(6 + (int)Math.Floor(level / 8.0) + (int)Math.Floor(trade / 15.0), 6, 20);

    private static bool TryNormalizeMerchantProfileCode(string? profileCode, out string normalizedProfile)
    {
        normalizedProfile = "";
        if (string.IsNullOrWhiteSpace(profileCode))
            return false;

        return MerchantProfileAliases.TryGetValue(profileCode.Trim(), out normalizedProfile!);
    }

    private static bool ContainsAny(string source, params string[] fragments) =>
        fragments.Any(fragment => source.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static int GetBaseBuyPrice(string rarity) => rarity switch
    {
        "Common" => 20,
        "Uncommon" => 50,
        "Rare" => 120,
        "Epic" => 280,
        _ => 20
    };

    private static int GetBaseBuyPrice(JsonObject item, string rarity) => GetNodeInt(item["price"], GetBaseBuyPrice(rarity));

    private static int GetBaseSellPrice(string rarity) => rarity switch
    {
        "Common" => 8,
        "Uncommon" => 20,
        "Rare" => 48,
        "Epic" => 112,
        _ => 8
    };

    private static int GetBaseSellPrice(JsonObject item, string rarity) => GetNodeInt(item["baseSellPrice"], GetBaseSellPrice(rarity));

    private static int ComputeBuyPrice(int basePrice, int playerTrade, int npcTrade, PricingTradeTier pricingTier)
    {
        var tradeDelta = playerTrade - npcTrade;
        var tradeModifier = 1.20 - Math.Clamp(tradeDelta * 0.01, -0.20, 0.20);
        var reputationModifier = pricingTier switch
        {
            PricingTradeTier.Hostile => 1.20,
            PricingTradeTier.Wary => 1.10,
            PricingTradeTier.Neutral => 1.00,
            PricingTradeTier.Warm => 0.92,
            PricingTradeTier.Trusted => 0.85,
            _ => 1.00
        };
        return (int)Math.Ceiling(basePrice * tradeModifier * reputationModifier);
    }

    private static int ComputeSellPrice(int basePrice, int playerTrade, int npcTrade, PricingTradeTier pricingTier)
    {
        var tradeDelta = playerTrade - npcTrade;
        var tradeModifier = 0.80 + Math.Clamp(tradeDelta * 0.01, -0.20, 0.20);
        var reputationModifier = pricingTier switch
        {
            PricingTradeTier.Hostile => 0.80,
            PricingTradeTier.Wary => 0.90,
            PricingTradeTier.Neutral => 1.00,
            PricingTradeTier.Warm => 1.08,
            PricingTradeTier.Trusted => 1.15,
            _ => 1.00
        };
        return (int)Math.Floor(basePrice * tradeModifier * reputationModifier);
    }

    private static int ReadNpcLevel(JsonObject npc) => GetNodeInt(npc["level"], 1);

    private static int ReadNpcTradeValue(JsonObject npc)
    {
        if (npc["characteristics"] is JsonObject chars)
        {
            var modified = GetNodeInt(chars["modifiedTrade"], int.MinValue);
            if (modified != int.MinValue)
                return modified;
            var standard = GetNodeInt(chars["standardTrade"], int.MinValue);
            if (standard != int.MinValue)
                return standard;
            var flat = GetNodeInt(chars["trade"], int.MinValue);
            if (flat != int.MinValue)
                return flat;
        }
        return 10;
    }

    private int ReadPlayerTradeSync()
    {
        foreach (var path in new[] { "game_state/misc/characteristics.json", "game_state/player/player_status.json", PlayerStatusPath })
        {
            try
            {
                var json = _fs.ReadFileAsync(path).GetAwaiter().GetResult();
                if (string.IsNullOrWhiteSpace(json))
                    continue;
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (TryReadIntLike(root, "modifiedTrade", out var modified))
                    return modified;
                if (TryReadIntLike(root, "trade", out var flat))
                    return flat;
            }
            catch
            {
                // ignore and try next source
            }
        }

        return 10;
    }

    private async Task<int> ReadPlayerTradeAsync() => await Task.FromResult(ReadPlayerTradeSync());

    private static int ReadNpcRelationshipLevel(JsonObject npc) => GetNodeInt(npc["relationshipLevel"], 0);

    private static void NormalizeInventoryShape(JsonObject root)
    {
        if (root["items"] is not JsonArray)
            root["items"] = new JsonArray();
        if (root["equipment"] is not JsonObject)
        {
            root["equipment"] = new JsonObject
            {
                ["head"] = null, ["body"] = null, ["hands"] = null, ["feet"] = null,
                ["mainHand"] = null, ["offHand"] = null, ["neck"] = null, ["ring1"] = null, ["ring2"] = null
            };
        }
    }

    private static HashSet<string> CollectEquippedItemReferences(JsonObject root)
    {
        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (root["equipment"] is not JsonObject eq)
            return refs;

        foreach (var prop in eq)
        {
            if (prop.Value is JsonValue value && value.TryGetValue<string>(out var str) && !string.IsNullOrWhiteSpace(str))
                refs.Add(str);
        }

        return refs;
    }

    private static bool IsQuestBoundItem(JsonObject item)
    {
        if (item["isQuestItem"] is JsonValue questValue && questValue.TryGetValue<bool>(out var isQuestItem) && isQuestItem)
            return true;
        return string.Equals(GetNodeString(item["group"]), "Quest", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSoulRelicLikeItem(JsonObject item)
    {
        if (!string.IsNullOrWhiteSpace(GetNodeString(item["relicId"])) ||
            !string.IsNullOrWhiteSpace(GetNodeString(item["soulRelicId"])))
            return true;

        var type = GetNodeString(item["type"]);
        if (!string.IsNullOrWhiteSpace(type) &&
            (type.Contains("soul relic", StringComparison.OrdinalIgnoreCase) ||
             type.Contains("реликвия души", StringComparison.OrdinalIgnoreCase)))
            return true;

        var group = GetNodeString(item["group"]);
        if (!string.IsNullOrWhiteSpace(group) &&
            (group.Contains("soul relic", StringComparison.OrdinalIgnoreCase) ||
             group.Contains("реликвия души", StringComparison.OrdinalIgnoreCase)))
            return true;

        var itemId = GetNodeString(item["itemId"]) ?? GetNodeString(item["id"]) ?? GetNodeString(item["existedId"]);
        return !string.IsNullOrWhiteSpace(itemId) &&
               (itemId.StartsWith("sr_", StringComparison.OrdinalIgnoreCase) ||
                itemId.Contains("soulrelic", StringComparison.OrdinalIgnoreCase));
    }

    private static void UpsertInventoryItem(JsonArray items, JsonObject item)
    {
        var itemId = GetNodeString(item["itemId"]) ?? GetNodeString(item["id"]) ?? GetNodeString(item["existedId"]);
        if (!string.IsNullOrWhiteSpace(itemId))
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] is not JsonObject existing)
                    continue;
                var existingId = GetNodeString(existing["itemId"]) ?? GetNodeString(existing["id"]) ?? GetNodeString(existing["existedId"]);
                if (!string.IsNullOrWhiteSpace(existingId) && string.Equals(existingId, itemId, StringComparison.OrdinalIgnoreCase))
                {
                    items[i] = item;
                    return;
                }
            }
        }

        items.Add(item);
    }

    private static int FindInventoryItemIndex(JsonArray items, string itemId)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is not JsonObject item)
                continue;
            var existingId = GetNodeString(item["itemId"]) ?? GetNodeString(item["id"]) ?? GetNodeString(item["existedId"]);
            if (!string.IsNullOrWhiteSpace(existingId) && string.Equals(existingId, itemId, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static JsonArray EnsureBuybackInventoryArray(JsonObject npc)
    {
        if (npc[BuybackInventoryProperty] is JsonArray buybackInventory)
            return buybackInventory;

        buybackInventory = new JsonArray();
        npc[BuybackInventoryProperty] = buybackInventory;
        return buybackInventory;
    }

    private static List<NpcBuybackOffer> ReadBuybackOffers(JsonObject npc)
    {
        if (npc[BuybackInventoryProperty] is not JsonArray buybackInventory)
            return new List<NpcBuybackOffer>();

        return buybackInventory
            .OfType<JsonObject>()
            .Where(entry => string.Equals(GetNodeString(entry["status"]), BuybackStatusAvailable, StringComparison.OrdinalIgnoreCase))
            .Where(entry => entry["itemData"] is JsonObject)
            .Select(entry =>
            {
                var itemData = CloneObject(entry["itemData"]!.AsObject());
                return new NpcBuybackOffer(
                    GetNodeString(entry["buybackEntryId"]) ?? "",
                    GetNodeString(entry["itemId"]) ?? GetNodeString(itemData["itemId"]) ?? "",
                    GetNodeString(itemData["name"]) ?? "Товар",
                    GetItemRarity(itemData),
                    GetNodeInt(entry["buybackPrice"], GetNodeInt(entry["soldForPrice"], 0)),
                    GetNodeInt(entry["soldForPrice"], 0),
                    GetNodeInt(entry["soldByPlayerAtTurn"], 0),
                    GetNodeString(itemData["description"]) ?? "",
                    itemData);
            })
            .Where(offer => !string.IsNullOrWhiteSpace(offer.BuybackEntryId))
            .OrderByDescending(offer => GetRarityRank(offer.Rarity))
            .ThenBy(offer => offer.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static JsonObject CreateBuybackEntry(
        string npcId,
        string npcName,
        JsonObject itemData,
        string merchantProfile,
        int soldForPrice,
        int soldAtTurn,
        int soldAtWorldDate)
    {
        var now = DateTimeOffset.UtcNow;
        var itemId = GetNodeString(itemData["itemId"]) ?? GetNodeString(itemData["id"]) ?? GetNodeString(itemData["existedId"]) ?? "";
        return new JsonObject
        {
            ["buybackEntryId"] = $"npc_buyback_{SanitizeId(npcId)}_{Guid.NewGuid():N}",
            ["npcId"] = npcId,
            ["npcName"] = npcName,
            ["itemId"] = itemId,
            ["itemData"] = itemData,
            ["soldByPlayerAtTurn"] = soldAtTurn,
            ["soldByPlayerAtUtc"] = now.ToString("O"),
            ["soldAtWorldDate"] = Math.Max(0, soldAtWorldDate),
            ["soldForPrice"] = soldForPrice,
            ["buybackPrice"] = soldForPrice,
            ["acquiredFromPlayer"] = true,
            ["sourceMerchantProfile"] = merchantProfile,
            ["status"] = BuybackStatusAvailable
        };
    }

    private static int GetRefreshAfterWorldMinutes(JsonObject? tradeInventory, int fallback) =>
        tradeInventory == null ? fallback + RefreshWindowMinutes : GetNodeInt(tradeInventory["refreshAfterWorldDate"], fallback + RefreshWindowMinutes);

    private static int GetGeneratedAtWorldMinutes(JsonObject? tradeInventory, int fallback) =>
        tradeInventory == null ? fallback : GetNodeInt(tradeInventory["generatedAtWorldDate"], fallback);

    private static int GetTradeCycleStartWorldMinutes(int currentWorldMinutes)
    {
        if (currentWorldMinutes <= 0)
            return 0;

        return currentWorldMinutes / RefreshWindowMinutes * RefreshWindowMinutes;
    }

    private static int GetRefreshAfterWorldMinutesForCycle(int currentWorldMinutes) =>
        GetTradeCycleStartWorldMinutes(currentWorldMinutes) + RefreshWindowMinutes;

    private static string GetTradeCycleId(int currentWorldMinutes) =>
        $"world_trade_{GetTradeCycleStartWorldMinutes(currentWorldMinutes)}";

    private static string GetItemRarity(JsonObject item) => GetNodeString(item["quality"]) ?? GetNodeString(item["rarity"]) ?? "Common";

    private static int GetRarityRank(string rarity) => rarity switch
    {
        "Common" => 1,
        "Uncommon" => 2,
        "Rare" => 3,
        "Epic" => 4,
        "Legendary" => 5,
        _ => 1
    };

    private static string GetNpcIdentity(JsonObject npc) =>
        GetNodeString(npc["NPCId"]) ?? GetNodeString(npc["npcId"]) ?? GetNodeString(npc["id"]) ?? "";

    private static string SanitizeId(string value)
    {
        var chars = value.Where(char.IsLetterOrDigit).ToArray();
        return chars.Length == 0 ? "item" : new string(chars).ToLowerInvariant();
    }

    private static JsonObject CloneObject(JsonObject source) => JsonNode.Parse(source.ToJsonString())!.AsObject();

    private static string? GetNodeString(JsonNode? node)
    {
        if (node == null) return null;
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var str))
                return str;
            return value.ToJsonString().Trim('"');
        }

        return node.ToJsonString();
    }

    private static int GetNodeInt(JsonNode? node, int fallback = 0)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var parsed))
                return parsed;
            if (value.TryGetValue<string>(out var str) && int.TryParse(str, out parsed))
                return parsed;
        }
        return fallback;
    }

    private static bool GetNodeBool(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<bool>(out var parsed))
                return parsed;
            if (value.TryGetValue<string>(out var str) && bool.TryParse(str, out parsed))
                return parsed;
        }
        return false;
    }

    private static string? GetFirstNonEmptyString(JsonElement root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!root.TryGetProperty(propertyName, out var value))
                continue;
            if (value.ValueKind == JsonValueKind.String)
            {
                var stringValue = value.GetString();
                if (!string.IsNullOrWhiteSpace(stringValue))
                    return stringValue;
            }
        }
        return null;
    }

    private static bool TryReadIntLike(JsonElement root, string propertyName, out int value)
    {
        value = 0;
        if (!root.TryGetProperty(propertyName, out var prop))
            return false;

        return prop.ValueKind switch
        {
            JsonValueKind.Number => prop.TryGetInt32(out value),
            JsonValueKind.String => int.TryParse(prop.GetString(), out value),
            _ => false
        };
    }

    private static int MapTimeOfDayToMinutes(string timeOfDay) => timeOfDay.ToLowerInvariant() switch
    {
        "dawn" or "рассвет" => 300,
        "morning" or "утро" => 480,
        "noon" or "day" or "день" => 720,
        "afternoon" => 900,
        "evening" or "вечер" => 1080,
        "night" or "ночь" => 1320,
        _ => 720
    };
}
