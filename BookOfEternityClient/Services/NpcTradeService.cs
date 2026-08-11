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
    private readonly ILocalInteractionScopeResolver _localScopeService;

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
        : this(fs, logger, new LocalInteractionScopeService(fs))
    {
    }

    internal NpcTradeService(
        FileSystemManager fs,
        ILogger<NpcTradeService> logger,
        ILocalInteractionScopeResolver localScopeService)
    {
        _fs = fs;
        _logger = logger;
        _localScopeService = localScopeService;
    }

    public async Task<IReadOnlyList<NpcTradeTarget>> GetCurrentLocationTradeTargetsAsync()
    {
        var localScope = await ResolveMortalTradeScopeAsync();
        if (localScope == null)
            return Array.Empty<NpcTradeTarget>();

        var root = await ReadNpcRootAsync();
        if (root == null)
            return Array.Empty<NpcTradeTarget>();

        var finalScope = await ResolveMortalTradeScopeAsync();
        var finalRoot = await ReadNpcRootAsync();
        return finalScope == null || finalRoot == null
            ? Array.Empty<NpcTradeTarget>()
            : BuildCurrentLocationTradeTargets(finalRoot, finalScope);
    }

    private static IReadOnlyList<NpcTradeTarget> BuildCurrentLocationTradeTargets(
        JsonObject root,
        LocalInteractionScope localScope)
    {
        var currentLocationId = localScope.LocationId;
        var currentLocationName = localScope.LocationName;
        var targets = new List<NpcTradeTarget>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var array in EnumerateNpcArrays(root))
        {
            foreach (var npc in array.OfType<JsonObject>())
            {
                var npcId = GetNpcIdentity(npc);
                if (string.IsNullOrWhiteSpace(npcId) || seen.Contains(npcId))
                    continue;

                var npcLocationId = GetNodeString(npc["currentLocationId"]) ?? string.Empty;
                var npcLocationName = GetNodeString(npc["currentLocation"]) ?? string.Empty;
                if (!LocalInteractionScopeService.IsMortalActorLocal(localScope, npc))
                {
                    continue;
                }

                var availability = EvaluateTradeAvailability(npc, currentLocationId, currentLocationName);
                if (!availability.IsMerchant)
                    continue;

                seen.Add(npcId);

                targets.Add(new NpcTradeTarget(
                    npcId,
                    GetNodeString(npc["name"]) ?? GetNodeString(npc["npcName"]) ?? npcId,
                    availability.MerchantProfileDisplay,
                    string.IsNullOrWhiteSpace(npcLocationName) ? currentLocationName : npcLocationName,
                    availability.TradeAvailable,
                    availability.BlockReason));
            }
        }

        return targets
            .OrderByDescending(static target => target.TradeAvailable)
            .ThenBy(static target => target.NpcName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<NpcTradeView?> EnsureTradeInventoryAsync(
        string npcId,
        int currentTurn,
        bool createPendingRequests = true) =>
        await EnsureTradeInventoryCoreAsync(
            writeLease: null,
            npcId,
            currentTurn,
            createPendingRequests);

    internal async Task<NpcTradeView?> EnsureTradeInventoryAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string npcId,
        int currentTurn,
        bool createPendingRequests = true) =>
        await EnsureTradeInventoryCoreAsync(
            writeLease,
            npcId,
            currentTurn,
            createPendingRequests);

    private async Task<NpcTradeView?> EnsureTradeInventoryCoreAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        string npcId,
        int currentTurn,
        bool createPendingRequests)
    {
        if (currentTurn <= 0)
            throw new ArgumentOutOfRangeException(nameof(currentTurn), "Подготовка или проверка витрины НПС требует актуальный номер хода.");

        var localScope = await ResolveMortalTradeScopeAsync(writeLease);
        if (localScope == null)
            return null;

        var npcRoot = await ReadNpcRootAsync(writeLease);
        var itemsRoot = await ReadInventoryRootAsync(writeLease);
        var statusRoot = await ReadPlayerStatusRootAsync(writeLease);
        if (npcRoot == null || itemsRoot == null || statusRoot == null)
            return null;
        var npcRootBaseline = npcRoot.ToJsonString(JsonOpts);

        var npc = FindNpcEntry(npcRoot, npcId);
        if (npc == null || !LocalInteractionScopeService.IsMortalActorLocal(localScope, npc))
            return null;

        var currentWorldMinutes = await ResolveCurrentWorldMinutesAsync(writeLease);
        var (changed, view) = await EnsureNpcTradeInventoryStateAsync(
            writeLease,
            localScope,
            npcRoot,
            npc,
            statusRoot,
            currentWorldMinutes,
            currentTurn,
            npcRootBaseline,
            createPendingRequests);
        if (changed)
        {
            var commitScope = await ResolveCurrentMortalTradeTargetScopeAsync(
                writeLease,
                npcId,
                npcRootBaseline);
            if (commitScope == null)
                return null;

            if (!await TryCommitAsync(
                    writeLease,
                    CoordinatedStateWriteHelper.CreateAuthorityGuardWrites(commitScope)
                        .Concat(new[]
                        {
                            new CoordinatedStateWriteHelper.PlannedWrite(
                                NpcCorePath,
                                npcRootBaseline,
                                npcRoot.ToJsonString(JsonOpts),
                                true)
                        })
                        .ToArray()))
            {
                return null;
            }
        }

        if (!await IsCurrentMortalTradeTargetAsync(
                writeLease,
                npcId,
                npcRoot.ToJsonString(JsonOpts)))
            return null;

        return view;
    }

    public async Task<IReadOnlyList<NpcSellOffer>> GetSellableItemsAsync(string npcId)
    {
        var localScope = await ResolveMortalTradeScopeAsync();
        if (localScope == null)
            return Array.Empty<NpcSellOffer>();

        var npcRoot = await ReadNpcRootAsync();
        var itemsRoot = await ReadInventoryRootAsync();
        var statusRoot = await ReadPlayerStatusRootAsync();
        if (npcRoot == null || itemsRoot == null || statusRoot == null)
            return Array.Empty<NpcSellOffer>();

        var npc = FindNpcEntry(npcRoot, npcId);
        if (npc == null || !LocalInteractionScopeService.IsMortalActorLocal(localScope, npc))
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
        var offers = items.OfType<JsonObject>()
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

        return await IsCurrentMortalTradeTargetAsync(npcId, npcRoot.ToJsonString(JsonOpts))
            ? offers
            : Array.Empty<NpcSellOffer>();
    }

    public async Task<NpcTradeOperationResult> BuyAsync(
        string npcId,
        string slotId,
        int currentTurn) =>
        await BuyCoreAsync(writeLease: null, npcId, slotId, currentTurn);

    internal async Task<NpcTradeOperationResult> BuyAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string npcId,
        string slotId,
        int currentTurn) =>
        await BuyCoreAsync(writeLease, npcId, slotId, currentTurn);

    private async Task<NpcTradeOperationResult> BuyCoreAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        string npcId,
        string slotId,
        int currentTurn)
    {
        if (currentTurn <= 0)
            return new NpcTradeOperationResult(false, false, "Локальная покупка товара требует актуальный номер хода.");
        if (writeLease == null)
        {
            await using var ownedLease = await _fs.AcquireCanonicalWriteLeaseAsync();
            return await BuyCoreAsync(ownedLease, npcId, slotId, currentTurn);
        }

        var localScope = await ResolveMortalTradeScopeAsync(writeLease);
        if (localScope == null)
            return new NpcTradeOperationResult(false, false, "Торговля с НПС недоступна в текущем мире или локации.");

        var npcRoot = await ReadNpcRootAsync(writeLease);
        var itemsRoot = await ReadInventoryRootAsync(writeLease);
        var statusRoot = await ReadPlayerStatusRootAsync(writeLease);
        if (npcRoot == null || itemsRoot == null || statusRoot == null)
            return new NpcTradeOperationResult(false, false, "Не удалось прочитать состояние торговли, инвентаря или денег.");
        var npcRootBaseline = npcRoot.ToJsonString(JsonOpts);

        var npc = FindNpcEntry(npcRoot, npcId);
        if (npc == null)
            return new NpcTradeOperationResult(false, false, "Торговец не найден.");
        if (!LocalInteractionScopeService.IsMortalActorLocal(localScope, npc))
            return new NpcTradeOperationResult(false, false, "Этот торговец не находится в вашей текущей локации.");

        var currentWorldMinutes = await ResolveCurrentWorldMinutesAsync(writeLease);
        var (changed, view) = await EnsureNpcTradeInventoryStateAsync(
            writeLease,
            localScope,
            npcRoot,
            npc,
            statusRoot,
            currentWorldMinutes,
            currentTurn,
            npcRootBaseline);
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

        var itemId = GetNodeString(slot["itemId"]) ?? GetNodeString(itemData["itemId"]);
        if (string.IsNullOrWhiteSpace(itemId) ||
            !string.Equals(itemId, GetNodeString(itemData["itemId"]), StringComparison.Ordinal))
        {
            return new NpcTradeOperationResult(false, false, "Витрина не связывает слот с точным физическим предметом.");
        }
        var stockItem = FindNpcInventoryItemExact(npc, itemId);
        JsonObject? rawTradeOutput = null;
        var tradeOutputSourceTurn = 0;
        string? tradeOutputReceiptId = null;
        if (stockItem == null && !TryBuildTemplateTradeOutput(
                npc,
                tradeInventory,
                slot,
                itemData,
                out rawTradeOutput,
                out tradeOutputSourceTurn,
                out tradeOutputReceiptId,
                out var templateError))
        {
            return new NpcTradeOperationResult(false, false, templateError!);
        }
        var quantity = GetNodeInt(stockItem?["count"] ?? rawTradeOutput?["count"], 0);
        if (quantity <= 0)
            return new NpcTradeOperationResult(false, false, "Количество физического товара повреждено.");
        if (rawTradeOutput != null && tradeOutputSourceTurn > currentTurn)
            return new NpcTradeOperationResult(false, false, "GM-шаблон товара относится к будущему ходу.");

        var commitScope = await ResolveCurrentMortalTradeTargetScopeAsync(
            writeLease,
            npcId,
            npcRootBaseline);
        if (commitScope == null)
            return new NpcTradeOperationResult(false, false, "Торговец покинул текущую локацию до завершения покупки. Деньги не списаны.");

        var canonicalNpcId = GetNpcIdentity(npc);
        var repricedInventory = changed
            ? npc["tradeInventory"]?.DeepClone()
            : null;
        var settlementMutation = new MortalItemTransitionMutation(
                new[] { NpcCorePath, PlayerStatusPath },
                context =>
                {
                    var mutableNpcRoot = context.GetRequiredRoot(NpcCorePath);
                    var mutableNpc = FindNpcEntryExact(mutableNpcRoot, canonicalNpcId);
                    if (mutableNpc == null)
                        return "Торговец изменился до фиксации покупки.";
                    if (repricedInventory != null)
                        mutableNpc["tradeInventory"] = repricedInventory.DeepClone();
                    if (mutableNpc["tradeInventory"]?["items"] is not JsonArray mutableSlots)
                        return "Витрина торговца исчезла до фиксации покупки.";
                    var mutableSlot = mutableSlots.OfType<JsonObject>().SingleOrDefault(candidate =>
                        string.Equals(GetNodeString(candidate["slotId"]), slotId, StringComparison.Ordinal));
                    if (mutableSlot == null || GetNodeBool(mutableSlot["soldOut"]) ||
                        !string.Equals(GetNodeString(mutableSlot["itemId"]), itemId, StringComparison.Ordinal))
                    {
                        return "Выбранный слот изменился до фиксации покупки.";
                    }

                    var mutableStatus = context.GetRequiredRoot(PlayerStatusPath);
                    if (GetNodeInt(mutableStatus["money"], 0) != money)
                        return "Баланс игрока изменился до фиксации покупки.";
                    mutableStatus["money"] = money - price;
                    mutableSlot["soldOut"] = true;
                    return null;
                },
                CoordinatedStateWriteHelper.CreateAuthorityGuardWrites(commitScope));
        var writer = new MortalItemTransitionWriter(_fs);
        var transition = stockItem != null
            ? await writer.ExecuteAsync(
                writeLease,
                new MortalItemTransitionIntent(
                    MortalItemTransitionKind.Transfer,
                    new[] { itemId },
                    NpcItemCarrier(canonicalNpcId),
                    PlayerItemCarrier(),
                    quantity,
                    currentTurn,
                    "npc_trade_buy",
                    $"npc_trade_buy:{canonicalNpcId}:{slotId}:{currentTurn}"),
                settlementMutation)
            : await writer.CreateAsync(
                writeLease,
                rawTradeOutput!,
                PlayerItemCarrier(),
                tradeOutputSourceTurn,
                "npc_trade_receipt",
                tradeOutputReceiptId!,
                settlementMutation);
        if (!transition.Success)
        {
            return new NpcTradeOperationResult(
                false,
                false,
                $"Покупка не зафиксирована: {transition.Message}");
        }

        var itemName = GetNodeString(itemData["name"]) ?? "Товар";
        return new NpcTradeOperationResult(true, true, $"Куплен товар «{itemName}» за {price}.");
    }

    public async Task<NpcTradeOperationResult> SellAsync(
        string npcId,
        string itemId,
        int currentTurn) =>
        await SellCoreAsync(writeLease: null, npcId, itemId, currentTurn);

    internal async Task<NpcTradeOperationResult> SellAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string npcId,
        string itemId,
        int currentTurn) =>
        await SellCoreAsync(writeLease, npcId, itemId, currentTurn);

    private async Task<NpcTradeOperationResult> SellCoreAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        string npcId,
        string itemId,
        int currentTurn)
    {
        if (currentTurn <= 0)
            return new NpcTradeOperationResult(false, false, "Локальная продажа товара требует актуальный номер хода.");
        if (writeLease == null)
        {
            await using var ownedLease = await _fs.AcquireCanonicalWriteLeaseAsync();
            return await SellCoreAsync(ownedLease, npcId, itemId, currentTurn);
        }

        var localScope = await ResolveMortalTradeScopeAsync(writeLease);
        if (localScope == null)
            return new NpcTradeOperationResult(false, false, "Торговля с НПС недоступна в текущем мире или локации.");

        var npcRoot = await ReadNpcRootAsync(writeLease);
        var itemsRoot = await ReadInventoryRootAsync(writeLease);
        var statusRoot = await ReadPlayerStatusRootAsync(writeLease);
        if (npcRoot == null || itemsRoot == null || statusRoot == null)
            return new NpcTradeOperationResult(false, false, "Не удалось прочитать состояние торговли, инвентаря или денег.");
        var npcRootBaseline = npcRoot.ToJsonString(JsonOpts);

        var npc = FindNpcEntry(npcRoot, npcId);
        if (npc == null)
            return new NpcTradeOperationResult(false, false, "Торговец не найден.");
        if (!LocalInteractionScopeService.IsMortalActorLocal(localScope, npc))
            return new NpcTradeOperationResult(false, false, "Этот торговец не находится в вашей текущей локации.");

        if (!NpcTradeAllowedHere(npc, localScope, out var blockedReason))
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
        var canonicalItemId = GetNodeString(item["itemId"]);
        if (!string.Equals(canonicalItemId, itemId, StringComparison.Ordinal))
            return new NpcTradeOperationResult(false, false, "Товар не найден по точному itemId.");
        if (IsQuestBoundItem(item))
            return new NpcTradeOperationResult(false, false, "Этот предмет нельзя продать через локальную торговлю.");
        if (IsSoulRelicLikeItem(item))
            return new NpcTradeOperationResult(false, false, "Реликвии души нельзя продать через локальную торговлю НПС.");

        var npcTrade = ReadNpcTradeValue(npc);
        var playerTrade = await ReadPlayerTradeAsync(writeLease);
        var relation = ReadNpcRelationshipLevel(npc);
        var pricingTier = GetPricingTier(relation);
        var currentWorldMinutes = await ResolveCurrentWorldMinutesAsync(writeLease);
        var rarity = GetItemRarity(item);
        var baseSellPrice = GetBaseSellPrice(item, rarity);
        var price = ComputeSellPrice(baseSellPrice, playerTrade, npcTrade, pricingTier);
        if (price <= 0)
            return new NpcTradeOperationResult(false, false, "Цена продажи повреждена.");

        var commitScope = await ResolveCurrentMortalTradeTargetScopeAsync(
            writeLease,
            npcId,
            npcRootBaseline);
        if (commitScope == null)
            return new NpcTradeOperationResult(false, false, "Торговец покинул текущую локацию до завершения продажи. Предмет не изменён.");

        var quantity = GetNodeInt(item["count"], 0);
        if (quantity <= 0)
            return new NpcTradeOperationResult(false, false, "Количество продаваемого предмета повреждено.");
        var merchantProfile = ResolveMerchantProfile(npc)?.Key ?? DefaultMerchantProfileKey;
        var canonicalNpcId = GetNpcIdentity(npc);
        var npcName = GetNodeString(npc["name"]) ?? canonicalNpcId;
        var moneyBefore = GetNodeInt(statusRoot["money"], 0);
        var transition = await new MortalItemTransitionWriter(_fs).ExecuteAsync(
            writeLease,
            new MortalItemTransitionIntent(
                MortalItemTransitionKind.Transfer,
                new[] { canonicalItemId! },
                PlayerItemCarrier(),
                NpcItemCarrier(canonicalNpcId),
                quantity,
                currentTurn,
                "npc_trade_sell",
                $"npc_trade_sell:{canonicalNpcId}:{canonicalItemId}:{currentTurn}:{Guid.NewGuid():N}"),
            new MortalItemTransitionMutation(
                new[] { PlayerStatusPath },
                context =>
                {
                    var mutableNpc = FindNpcEntryExact(
                        context.GetRequiredRoot(NpcCorePath),
                        canonicalNpcId);
                    if (mutableNpc == null)
                        return "Торговец изменился до фиксации продажи.";
                    var mutableStatus = context.GetRequiredRoot(PlayerStatusPath);
                    if (GetNodeInt(mutableStatus["money"], 0) != moneyBefore)
                        return "Баланс игрока изменился до фиксации продажи.";

                    EnsureBuybackInventoryArray(mutableNpc).Add(CreateBuybackEntry(
                        canonicalNpcId,
                        npcName,
                        CreateTradeItemProjection(context.Item),
                        merchantProfile,
                        price,
                        currentTurn,
                        currentWorldMinutes));
                    mutableStatus["money"] = moneyBefore + price;
                    return null;
                },
                CoordinatedStateWriteHelper.CreateAuthorityGuardWrites(commitScope)));
        if (!transition.Success)
        {
            return new NpcTradeOperationResult(
                false,
                false,
                $"Продажа не зафиксирована: {transition.Message}");
        }

        var itemName = GetNodeString(item["name"]) ?? "Товар";
        return new NpcTradeOperationResult(true, true, $"Продан товар «{itemName}» за {price}.");
    }

    public async Task<NpcTradeOperationResult> BuyBackAsync(
        string npcId,
        string buybackEntryId,
        int currentTurn) =>
        await BuyBackCoreAsync(writeLease: null, npcId, buybackEntryId, currentTurn);

    internal async Task<NpcTradeOperationResult> BuyBackAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string npcId,
        string buybackEntryId,
        int currentTurn) =>
        await BuyBackCoreAsync(writeLease, npcId, buybackEntryId, currentTurn);

    private async Task<NpcTradeOperationResult> BuyBackCoreAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        string npcId,
        string buybackEntryId,
        int currentTurn)
    {
        if (currentTurn <= 0)
            return new NpcTradeOperationResult(false, false, "Локальный выкуп товара требует актуальный номер хода.");
        if (writeLease == null)
        {
            await using var ownedLease = await _fs.AcquireCanonicalWriteLeaseAsync();
            return await BuyBackCoreAsync(ownedLease, npcId, buybackEntryId, currentTurn);
        }

        var localScope = await ResolveMortalTradeScopeAsync(writeLease);
        if (localScope == null)
            return new NpcTradeOperationResult(false, false, "Торговля с НПС недоступна в текущем мире или локации.");

        var npcRoot = await ReadNpcRootAsync(writeLease);
        var itemsRoot = await ReadInventoryRootAsync(writeLease);
        var statusRoot = await ReadPlayerStatusRootAsync(writeLease);
        if (npcRoot == null || itemsRoot == null || statusRoot == null)
            return new NpcTradeOperationResult(false, false, "Не удалось прочитать состояние торговли, инвентаря или денег.");
        var npcRootBaseline = npcRoot.ToJsonString(JsonOpts);

        var npc = FindNpcEntry(npcRoot, npcId);
        if (npc == null)
            return new NpcTradeOperationResult(false, false, "Торговец не найден.");
        if (!LocalInteractionScopeService.IsMortalActorLocal(localScope, npc))
            return new NpcTradeOperationResult(false, false, "Этот торговец не находится в вашей текущей локации.");

        if (!NpcTradeAllowedHere(npc, localScope, out var blockedReason))
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
        var itemId = GetNodeString(buybackEntry["itemId"]);
        if (string.IsNullOrWhiteSpace(itemId) ||
            !string.Equals(itemId, GetNodeString(itemData["itemId"]), StringComparison.Ordinal))
        {
            return new NpcTradeOperationResult(false, false, "Запись обратного выкупа не связана с точным предметом.");
        }
        var physicalItem = FindNpcInventoryItemExact(npc, itemId);
        if (physicalItem == null)
            return new NpcTradeOperationResult(false, false, "Физический предмет обратного выкупа отсутствует у торговца.");
        var quantity = GetNodeInt(physicalItem["count"], 0);
        if (quantity <= 0)
            return new NpcTradeOperationResult(false, false, "Количество предмета обратного выкупа повреждено.");

        var price = GetNodeInt(buybackEntry["buybackPrice"], GetNodeInt(buybackEntry["soldForPrice"], 0));
        if (price <= 0)
            return new NpcTradeOperationResult(false, false, "Цена обратного выкупа повреждена.");

        var money = GetNodeInt(statusRoot["money"], 0);
        if (money < price)
            return new NpcTradeOperationResult(false, false, "Недостаточно денег.");

        var commitScope = await ResolveCurrentMortalTradeTargetScopeAsync(
            writeLease,
            npcId,
            npcRootBaseline);
        if (commitScope == null)
            return new NpcTradeOperationResult(false, false, "Торговец покинул текущую локацию до завершения выкупа. Деньги не списаны.");

        var canonicalNpcId = GetNpcIdentity(npc);
        var reboughtAtUtc = DateTimeOffset.UtcNow.ToString("O");
        var transition = await new MortalItemTransitionWriter(_fs).ExecuteAsync(
            writeLease,
            new MortalItemTransitionIntent(
                MortalItemTransitionKind.Transfer,
                new[] { itemId },
                NpcItemCarrier(canonicalNpcId),
                PlayerItemCarrier(),
                quantity,
                currentTurn,
                "npc_trade_buyback",
                $"npc_trade_buyback:{canonicalNpcId}:{buybackEntryId}:{currentTurn}"),
            new MortalItemTransitionMutation(
                new[] { PlayerStatusPath },
                context =>
                {
                    var mutableNpc = FindNpcEntryExact(
                        context.GetRequiredRoot(NpcCorePath),
                        canonicalNpcId);
                    if (mutableNpc?[BuybackInventoryProperty] is not JsonArray mutableBuyback)
                        return "Запись обратного выкупа исчезла до фиксации.";
                    var mutableEntry = mutableBuyback.OfType<JsonObject>().SingleOrDefault(entry =>
                        string.Equals(
                            GetNodeString(entry["buybackEntryId"]),
                            buybackEntryId,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            GetNodeString(entry["status"]),
                            BuybackStatusAvailable,
                            StringComparison.Ordinal) &&
                        string.Equals(GetNodeString(entry["itemId"]), itemId, StringComparison.Ordinal));
                    if (mutableEntry == null)
                        return "Запись обратного выкупа изменилась до фиксации.";

                    var mutableStatus = context.GetRequiredRoot(PlayerStatusPath);
                    if (GetNodeInt(mutableStatus["money"], 0) != money)
                        return "Баланс игрока изменился до фиксации выкупа.";
                    mutableStatus["money"] = money - price;
                    mutableEntry["status"] = BuybackStatusRebought;
                    mutableEntry["reboughtAtTurn"] = currentTurn;
                    mutableEntry["reboughtAtUtc"] = reboughtAtUtc;
                    return null;
                },
                CoordinatedStateWriteHelper.CreateAuthorityGuardWrites(commitScope)));
        if (!transition.Success)
        {
            return new NpcTradeOperationResult(
                false,
                false,
                $"Обратный выкуп не зафиксирован: {transition.Message}");
        }

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

    internal static string? ResolveMerchantProfileCode(string? explicitProfile) =>
        TryNormalizeMerchantProfileCode(explicitProfile, out var normalizedProfile)
            ? normalizedProfile
            : null;

    internal static NpcTradeAvailability EvaluateTradeAvailability(JsonElement npc, string currentLocationId, string currentLocationName)
    {
        var merchantProfile = ResolveMerchantProfileCode(
            npc.TryGetProperty("tradeState", out var tradeState) && tradeState.ValueKind == JsonValueKind.Object
                ? GetFirstNonEmptyString(tradeState, "merchantProfile")
                : null);

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
            GetNodeString(tradeState?["merchantProfile"]));

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
        FileSystemManager.CanonicalWriteLease? writeLease,
        LocalInteractionScope localScope,
        JsonObject root,
        JsonObject npc,
        JsonObject statusRoot,
        int currentWorldMinutes,
        int currentTurn,
        string npcRootBaseline,
        bool createPendingRequests = true)
    {
        var npcId = GetNpcIdentity(npc);
        var npcName = GetNodeString(npc["name"]) ?? npcId;
        var blocked = !NpcTradeAllowedHere(npc, localScope, out var blockedReason);
        var npcTrade = ReadNpcTradeValue(npc);
        var playerTrade = ReadPlayerTradeSync(writeLease);
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

                var request = await FindPendingTradeRequestAsync(writeLease, npcId, tradeCycleId);
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
                {
                    var requestScope = await ResolveCurrentMortalTradeTargetScopeAsync(
                        writeLease,
                        npcId,
                        npcRootBaseline);
                    if (requestScope == null)
                        return (false, null);

                    await EnsureTradeRequestHealthyAsync(writeLease);
                }
            }
            else
            {
                var derivedTradeSlotCount = ComputeSlotCount(ReadNpcLevel(npc), npcTrade);
                var request = await FindPendingTradeRequestAsync(writeLease, npcId, tradeCycleId);
                inventoryRequestPending = NpcTradeRequestState.MatchesCurrentContract(
                    request,
                    npcId,
                    merchantProfile,
                    tradeCycleId,
                    derivedTradeSlotCount,
                    refreshAfterWorldMinutes);

                if (!inventoryRequestPending && createPendingRequests)
                {
                    var requestScope = await ResolveCurrentMortalTradeTargetScopeAsync(
                        writeLease,
                        npcId,
                        npcRootBaseline);
                    if (requestScope == null)
                        return (false, null);

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
                    if (!await TryWriteScopedTradeRequestAsync(
                            writeLease,
                            request,
                            requestScope,
                            npcRootBaseline))
                    {
                        return (false, null);
                    }
                    inventoryRequestPending = true;
                    inventoryRequestCreatedThisCall = true;
                }

                if (inventoryRequestPending && request != null)
                    pendingGmAction = BuildNpcTradePendingGmAction(request);

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
            playerTrade,
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

    private static string BuildNpcTradePendingGmAction(NpcTradeRequestState.PendingNpcTradeInventoryRequest request) =>
        $"[{NpcTradeRequestState.ActionTag}] Игрок открывает торговлю с NPC {request.NpcName} ({request.NpcId}), но explicit витрина отсутствует или устарела для текущего world-time cycle. " +
        $"Обязательно прочитай {NpcTradeRequestState.PendingRequestPath} как client-authored contract. " +
        "Материализуй explicit npc.tradeInventory для указанного tradeCycleId и не выводи ассортимент клиентом. " +
        $"После materialization закрой запрос canonical receipt через {NpcTradeRequestState.UpdateReceiptsProperty} в npc_core.json. " +
        "Витрина должна уважать merchantProfile, tradeCycleId, refreshAfterWorldDate и derivedTradeSlotCount из request.";

    private NpcTradeView BuildTradeView(
        JsonObject npc,
        JsonObject statusRoot,
        int playerTrade,
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

    private async Task<JsonObject?> ReadNpcRootAsync(
        FileSystemManager.CanonicalWriteLease? writeLease = null)
    {
        try
        {
            var json = await ReadAsync(writeLease, NpcCorePath);
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

    private async Task<JsonObject?> ReadInventoryRootAsync(
        FileSystemManager.CanonicalWriteLease? writeLease = null)
    {
        try
        {
            var json = await ReadAsync(writeLease, ItemsPath);
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

    private async Task<JsonObject?> ReadPlayerStatusRootAsync(
        FileSystemManager.CanonicalWriteLease? writeLease = null)
    {
        try
        {
            var json = await ReadAsync(writeLease, PlayerStatusPath);
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

    private async Task<int> ResolveCurrentWorldMinutesAsync(
        FileSystemManager.CanonicalWriteLease? writeLease = null)
    {
        try
        {
            var json = await ReadAsync(writeLease, WorldTimePath);
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

    private static JsonObject? FindNpcEntryExact(JsonObject root, string npcId)
    {
        var matches = EnumerateNpcArrays(root)
            .SelectMany(static array => array.OfType<JsonObject>())
            .Where(item => string.Equals(GetNpcIdentity(item), npcId, StringComparison.Ordinal))
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static JsonObject? FindNpcInventoryItemExact(JsonObject npc, string itemId)
    {
        if (npc["inventory"] is not JsonArray inventory)
            return null;
        var matches = inventory.OfType<JsonObject>()
            .Where(item => string.Equals(GetNodeString(item["itemId"]), itemId, StringComparison.Ordinal))
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static bool TryBuildTemplateTradeOutput(
        JsonObject npc,
        JsonObject tradeInventory,
        JsonObject slot,
        JsonObject itemData,
        out JsonObject? rawItem,
        out int sourceTurn,
        out string? authorityId,
        out string? error)
    {
        rawItem = null;
        sourceTurn = 0;
        authorityId = null;
        error = null;

        var npcId = GetNpcIdentity(npc);
        var slotId = GetNodeString(slot["slotId"]);
        var tradeCycleId = GetNodeString(tradeInventory["tradeCycleId"]);
        var merchantProfile = GetNodeString(slot["merchantProfile"]);
        if (string.IsNullOrWhiteSpace(npcId) || string.IsNullOrWhiteSpace(slotId) ||
            string.IsNullOrWhiteSpace(tradeCycleId) || string.IsNullOrWhiteSpace(merchantProfile) ||
            tradeInventory["items"] is not JsonArray slots || slots.Count < 1)
        {
            error = "Витрина не содержит точную authority для materialization товара.";
            return false;
        }

        var matchingReceipts = (npc[NpcTradeRequestState.ReceiptsProperty] as JsonArray)?
            .OfType<JsonObject>()
            .Where(receipt =>
                string.Equals(GetNodeString(receipt["npcId"]), npcId, StringComparison.Ordinal) &&
                string.Equals(GetNodeString(receipt["tradeCycleId"]), tradeCycleId, StringComparison.Ordinal) &&
                string.Equals(GetNodeString(receipt["merchantProfile"]), merchantProfile, StringComparison.Ordinal) &&
                string.Equals(
                    GetNodeString(receipt["status"]),
                    NpcTradeRequestState.ReceiptStatusReady,
                    StringComparison.Ordinal) &&
                GetNodeInt(receipt["itemCount"], 0) == slots.Count)
            .ToArray() ?? Array.Empty<JsonObject>();
        if (matchingReceipts.Length != 1)
        {
            error = "Шаблон товара не связан с единственным ready receipt витрины.";
            return false;
        }

        var receipt = matchingReceipts[0];
        authorityId = GetNodeString(receipt["requestId"]);
        sourceTurn = GetNodeInt(receipt["resolvedAtTurn"], 0);
        if (string.IsNullOrWhiteSpace(authorityId) || sourceTurn < 1 ||
            itemData[MortalItemMaterializationContract.EnvelopeProperty] is not JsonObject envelope ||
            !string.Equals(GetNodeString(envelope["route"]), "trade_output", StringComparison.Ordinal) ||
            !string.Equals(GetNodeString(envelope["creationRef"]), slotId, StringComparison.Ordinal) ||
            GetNodeInt(envelope["sourceTurn"], 0) != sourceTurn ||
            envelope["sourceAuthority"] is not JsonObject sourceAuthority ||
            !string.Equals(
                GetNodeString(sourceAuthority["kind"]),
                "npc_trade_receipt",
                StringComparison.Ordinal) ||
            !string.Equals(GetNodeString(sourceAuthority["authorityId"]), authorityId, StringComparison.Ordinal))
        {
            error = "GM-шаблон товара не связан с exact slotId, sourceTurn и npc_trade_receipt.";
            return false;
        }

        rawItem = itemData.DeepClone().AsObject();
        foreach (var property in new[]
                 {
                     "itemId", "id", "initialId", "existedId",
                     MortalItemMaterializationContract.ReceiptProperty
                 })
        {
            rawItem.Remove(property);
        }
        rawItem["existedId"] = null;
        rawItem["creationRef"] = slotId;
        return true;
    }

    private static MortalItemCarrierCoordinate PlayerItemCarrier() =>
        new("player_inventory", "player", null, Array.Empty<string>());

    private static MortalItemCarrierCoordinate NpcItemCarrier(string npcId) =>
        new("npc_inventory", npcId, null, Array.Empty<string>());

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

    private static bool NpcTradeAllowedHere(
        JsonObject npc,
        LocalInteractionScope localScope,
        out string? blockedReason)
    {
        var availability = EvaluateTradeAvailability(
            npc,
            localScope.LocationId,
            localScope.LocationName);
        blockedReason = availability.BlockReason;
        return availability.TradeAvailable;
    }

    private async Task<LocalInteractionScope?> ResolveMortalTradeScopeAsync(
        FileSystemManager.CanonicalWriteLease? writeLease = null)
    {
        var scope = writeLease == null
            ? await _localScopeService.ResolveAsync()
            : await _localScopeService.ResolveAsync(writeLease);
        return scope.IsResolved && scope.RealmKind == LocalInteractionRealmKind.Mortal ? scope : null;
    }

    private async Task<bool> IsCurrentMortalTradeTargetAsync(string npcId, string? expectedNpcRootJson = null) =>
        await ResolveCurrentMortalTradeTargetScopeAsync(writeLease: null, npcId, expectedNpcRootJson) != null;

    private async Task<bool> IsCurrentMortalTradeTargetAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        string npcId,
        string? expectedNpcRootJson = null) =>
        await ResolveCurrentMortalTradeTargetScopeAsync(writeLease, npcId, expectedNpcRootJson) != null;

    private async Task<LocalInteractionScope?> ResolveCurrentMortalTradeTargetScopeAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        string npcId,
        string? expectedNpcRootJson = null)
    {
        var scope = await ResolveMortalTradeScopeAsync(writeLease);
        if (scope == null)
            return null;

        var root = await ReadNpcRootAsync(writeLease);
        if (root == null ||
            (expectedNpcRootJson != null &&
             !string.Equals(root.ToJsonString(JsonOpts), expectedNpcRootJson, StringComparison.Ordinal)))
        {
            return null;
        }

        var npc = FindNpcEntry(root, npcId);
        return npc != null && LocalInteractionScopeService.IsMortalActorLocal(scope, npc)
            ? scope
            : null;
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
            GetNodeString(tradeState?["merchantProfile"]));
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
        return LocalInteractionScopeService.MatchesLocation(
            npcLocationId,
            npcLocationName,
            currentLocationId,
            currentLocationName);
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

    private int ReadPlayerTradeSync(
        FileSystemManager.CanonicalWriteLease? writeLease = null)
    {
        foreach (var path in new[] { "game_state/misc/characteristics.json", "game_state/player/player_status.json", PlayerStatusPath })
        {
            try
            {
                var json = ReadAsync(writeLease, path).GetAwaiter().GetResult();
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

    private async Task<int> ReadPlayerTradeAsync(
        FileSystemManager.CanonicalWriteLease? writeLease = null) =>
        await Task.FromResult(ReadPlayerTradeSync(writeLease));

    private Task<string?> ReadAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        string path) =>
        writeLease == null
            ? _fs.ReadFileAsync(path)
            : _fs.ReadFileAsync(writeLease, path);

    private Task<bool> TryCommitAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        params CoordinatedStateWriteHelper.PlannedWrite[] writes) =>
        writeLease == null
            ? CoordinatedStateWriteHelper.TryCommitAsync(_fs, writes)
            : CoordinatedStateWriteHelper.TryCommitAsync(_fs, writeLease, writes);

    private Task<NpcTradeRequestState.PendingNpcTradeInventoryRequest?> FindPendingTradeRequestAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        string npcId,
        string tradeCycleId) =>
        writeLease == null
            ? NpcTradeRequestState.FindPendingRequestAsync(_fs, npcId, tradeCycleId)
            : NpcTradeRequestState.FindPendingRequestAsync(_fs, writeLease, npcId, tradeCycleId);

    private Task EnsureTradeRequestHealthyAsync(
        FileSystemManager.CanonicalWriteLease? writeLease) =>
        writeLease == null
            ? NpcTradeRequestState.EnsureHealthyAsync(_fs, "Mortal World")
            : NpcTradeRequestState.EnsureHealthyAsync(_fs, writeLease, "Mortal World");

    private Task<bool> TryWriteScopedTradeRequestAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        NpcTradeRequestState.PendingNpcTradeInventoryRequest request,
        LocalInteractionScope scope,
        string expectedNpcRootJson) =>
        writeLease == null
            ? NpcTradeRequestState.TryWriteScopedRequestAsync(
                _fs,
                request,
                scope,
                expectedNpcRootJson)
            : NpcTradeRequestState.TryWriteScopedRequestAsync(
                _fs,
                writeLease,
                request,
                scope,
                expectedNpcRootJson);

    private static int ReadNpcRelationshipLevel(JsonObject npc) => GetNodeInt(npc["relationshipLevel"], 0);

    private static void NormalizeInventoryShape(JsonObject root)
    {
        if (root["items"] is not JsonArray)
            root["items"] = new JsonArray();
        if (root["equipment"] is not JsonObject)
        {
            root["equipment"] = new JsonObject
            {
                ["head"] = null,
                ["body"] = null,
                ["hands"] = null,
                ["feet"] = null,
                ["mainHand"] = null,
                ["offHand"] = null,
                ["neck"] = null,
                ["ring1"] = null,
                ["ring2"] = null
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
        return item["isQuestItem"] is JsonValue questValue &&
               questValue.TryGetValue<bool>(out var isQuestItem) &&
               isQuestItem;
    }

    private static bool IsSoulRelicLikeItem(JsonObject item)
    {
        return !string.IsNullOrWhiteSpace(
            GetNodeString(item["relicId"]));
    }

    private static int FindInventoryItemIndex(JsonArray items, string itemId)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is not JsonObject item)
                continue;
            var existingId = GetNodeString(item["itemId"]) ?? GetNodeString(item["id"]) ?? GetNodeString(item["existedId"]);
            if (!string.IsNullOrWhiteSpace(existingId) && string.Equals(existingId, itemId, StringComparison.Ordinal))
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

    private static JsonObject CreateTradeItemProjection(JsonObject item)
    {
        var projection = new JsonObject();
        foreach (var property in new[]
                 {
                     "itemId", "name", "description", "type", "tradeItemClass", "quality", "rarity",
                     "price", "baseSellPrice", "count", "weight", "volume", "group", "durability",
                     "isContainer", "isConsumption"
                 })
        {
            if (item[property] != null)
                projection[property] = item[property]!.DeepClone();
        }

        var itemId = GetNodeString(item["itemId"]) ?? string.Empty;
        projection["itemId"] = itemId;
        projection["name"] = GetNodeString(item["name"]) ?? "Товар";
        projection["description"] = GetNodeString(item["description"]) ?? projection["name"]!.GetValue<string>();
        projection["price"] = Math.Max(1, GetNodeInt(item["price"], GetBaseBuyPrice(GetItemRarity(item))));
        projection["baseSellPrice"] = Math.Max(0, GetNodeInt(item["baseSellPrice"], GetBaseSellPrice(GetItemRarity(item))));
        return projection;
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
        GetNodeString(npc["NPCId"])
        ?? GetNodeString(npc["npcId"])
        ?? GetNodeString(npc["id"])
        ?? GetNodeString(npc["initialId"])
        ?? "";

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
