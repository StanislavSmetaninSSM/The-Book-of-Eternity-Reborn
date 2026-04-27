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
    private const string BuybackRelicsProperty = "buybackRelics";
    private const string BuybackStatusAvailable = "available";
    private const string BuybackStatusRebought = "rebought";
    private const string BuybackStatusRemoved = "removed";

    private enum TradeReputationTier
    {
        Hostile,
        Neutral,
        Friendly,
        Devoted,
        Legendary
    }

    public sealed record GuardianTradeOffer(
        string SlotId,
        string Name,
        string Rarity,
        int PriceInFeathers,
        string Description,
        string DomainTag,
        bool SoldOut,
        JsonObject RelicData);

    public sealed record GuardianBuybackOffer(
        string BuybackEntryId,
        string RelicId,
        string Name,
        string Rarity,
        int PriceInFeathers,
        int SoldForPrice,
        int SoldAtTurn,
        string Description,
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
        bool InventoryReady,
        bool InventoryRequestPending,
        bool InventoryRequestCreatedThisCall,
        string? InventoryStatusMessage,
        string? PendingGmAction,
        IReadOnlyList<GuardianTradeOffer> Offers,
        IReadOnlyList<GuardianBuybackOffer> BuybackOffers);

    public sealed record GuardianSellOffer(
        string RelicId,
        string Name,
        string Rarity,
        int PriceInFeathers,
        string Description,
        JsonObject RelicData);

    public sealed record GuardianTradeOperationResult(bool Success, bool StateChanged, string Message);

    public GuardianTradeService(FileSystemManager fs, ILogger<GuardianTradeService> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public async Task<GuardianTradeView?> EnsureTradeInventoryAsync(string guardianId, int currentIncarnation, int currentTurn)
    {
        if (currentTurn <= 0)
            throw new ArgumentOutOfRangeException(nameof(currentTurn), "Подготовка или проверка витрины Хранителя требует актуальный номер хода.");

        var root = await ReadGuardiansRootAsync();
        if (root == null)
            return null;
        var trackerRoot = await ReadGuardianProjectTrackerRootAsync();

        var guardian = FindGuardian(root, guardianId);
        if (guardian == null)
            return null;

        var preGuardiansJson = root.ToJsonString(JsonOpts);
        var preTrackerJson = trackerRoot?.ToJsonString(JsonOpts);
        var (_, view, changed, trackerChanged) = await EnsureTradeInventoryStateAsync(root, guardian, currentIncarnation, currentTurn, trackerRoot);
        if (changed || trackerChanged)
        {
            var writes = new List<CoordinatedStateWriteHelper.PlannedWrite>
            {
                new(GuardiansPath, preGuardiansJson, root.ToJsonString(JsonOpts))
            };
            if (trackerChanged && trackerRoot != null)
            {
                writes.Add(new CoordinatedStateWriteHelper.PlannedWrite(
                    GuardianProjectState.TrackerPath,
                    preTrackerJson,
                    trackerRoot.ToJsonString(JsonOpts)));
            }

            if (!await CoordinatedStateWriteHelper.TryCommitAsync(_fs, writes.ToArray()))
            {
                return view with
                {
                    TradeBlocked = true,
                    BlockReason = "Не удалось безопасно зафиксировать состояние витрины Хранителя и трекера проектов без расхождения. Изменения откатились.",
                    InventoryReady = false
                };
            }
        }

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
                    GetNodeString(relic["description"]) ?? "",
                    CloneObject(relic));
            })
            .Where(offer => !string.IsNullOrWhiteSpace(offer.RelicId))
            .OrderByDescending(offer => GetRarityRank(offer.Rarity))
            .ThenBy(offer => offer.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<GuardianTradeOperationResult> BuyAsync(string guardianId, string slotId, int currentIncarnation, int currentTurn)
    {
        if (currentTurn <= 0)
            return new GuardianTradeOperationResult(false, false, "Локальная покупка реликвии требует актуальный номер хода.");

        var guardiansRoot = await ReadGuardiansRootAsync();
        var soulRoot = await ReadSoulStateRootAsync();
        var trackerRoot = await ReadGuardianProjectTrackerRootAsync();
        if (guardiansRoot == null || soulRoot == null)
            return new GuardianTradeOperationResult(false, false, "Не удалось прочитать состояние торговли или души.");

        var preBuyGuardiansJson = guardiansRoot.ToJsonString(JsonOpts);
        var preBuySoulJson = soulRoot.ToJsonString(JsonOpts);
        var preBuyTrackerJson = trackerRoot?.ToJsonString(JsonOpts);

        var guardian = FindGuardian(guardiansRoot, guardianId);
        if (guardian == null)
            return new GuardianTradeOperationResult(false, false, "Хранитель не найден.");

        if (!GuardianTradeAllowedHere(guardiansRoot, guardian))
            return new GuardianTradeOperationResult(false, false, "Торговать можно только с текущим активным Хранителем в обители, где вы сейчас находитесь.");

        var (_, view, changed, trackerChanged) = await EnsureTradeInventoryStateAsync(guardiansRoot, guardian, currentIncarnation, currentTurn, trackerRoot);
        if (view.TradeBlocked)
            return new GuardianTradeOperationResult(false, false, view.BlockReason ?? "Торговля недоступна.");
        if (!view.InventoryReady)
            return new GuardianTradeOperationResult(false, false, view.InventoryStatusMessage ?? "Витрина Хранителя ещё не подготовлена.");

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

        GuardianPolicyContracts.EnsureStrictCanonicalSoulStateRootsForPolicySensitiveWrite(soulRoot);
        if (!TryModifyInkFeathers(soulRoot, -price))
            return new GuardianTradeOperationResult(false, false, "Недостаточно Чернильных Перьев.");

        if (slot["relicData"] is not JsonObject relicData)
            return new GuardianTradeOperationResult(false, false, "Данные реликвии повреждены.");

        NormalizeSoulRelicsShape(soulRoot);
        var stored = ((JsonObject)soulRoot["soulRelics"]!)["stored"]!.AsArray();
        UpsertRelic(stored, CloneObject(relicData));
        slot["soldOut"] = true;
        SyncActiveGuardian(guardiansRoot, guardianId, guardian);

        var postBuySoulJson = GuardianPolicyContracts.CreatePatchedSoulStateWriteRoot(
            soulRoot,
            new GuardianPolicyContracts.SoulStatePatchConflictContext(
                GuardianPolicyContracts.SoulStatePatchTouchedDomains.InkFeathers |
                GuardianPolicyContracts.SoulStatePatchTouchedDomains.SoulRelics,
                upsertedSoulRelicIds: new[] { GetNodeString(relicData["relicId"]) ?? string.Empty })).ToJsonString(JsonOpts);
        var coordinatedWrites = new List<CoordinatedStateWriteHelper.PlannedWrite>
        {
            new(SoulStatePath, preBuySoulJson, postBuySoulJson),
            new(GuardiansPath, preBuyGuardiansJson, guardiansRoot.ToJsonString(JsonOpts))
        };
        if ((changed || trackerChanged) && trackerRoot != null)
        {
            coordinatedWrites.Add(new CoordinatedStateWriteHelper.PlannedWrite(
                GuardianProjectState.TrackerPath,
                preBuyTrackerJson,
                trackerRoot.ToJsonString(JsonOpts)));
        }

        if (!await CoordinatedStateWriteHelper.TryCommitAsync(_fs, coordinatedWrites.ToArray()))
        {
            return new GuardianTradeOperationResult(
                false,
                false,
                "Не удалось безопасно зафиксировать покупку без расхождения между состоянием души, Хранителя и трекера проектов. Изменения откатились к исходной версии.");
        }

        var relicName = GetNodeString(relicData["name"]) ?? "Реликвия";
        return new GuardianTradeOperationResult(true, true, $"Куплена реликвия «{relicName}» за {price} 🪶.");
    }

    public async Task<GuardianTradeOperationResult> SellAsync(string guardianId, string relicId, int currentTurn)
    {
        if (currentTurn <= 0)
            return new GuardianTradeOperationResult(false, false, "Локальная продажа реликвии требует актуальный номер хода.");

        var guardiansRoot = await ReadGuardiansRootAsync();
        var soulRoot = await ReadSoulStateRootAsync();
        if (guardiansRoot == null || soulRoot == null)
            return new GuardianTradeOperationResult(false, false, "Не удалось прочитать состояние торговли или души.");

        var preSellGuardiansJson = guardiansRoot.ToJsonString(JsonOpts);
        var preSellSoulJson = soulRoot.ToJsonString(JsonOpts);

        var guardian = FindGuardian(guardiansRoot, guardianId);
        if (guardian == null)
            return new GuardianTradeOperationResult(false, false, "Хранитель не найден.");

        if (!GuardianTradeAllowedHere(guardiansRoot, guardian))
            return new GuardianTradeOperationResult(false, false, "Торговать можно только с текущим активным Хранителем в обители, где вы сейчас находитесь.");

        var tier = GetTradeReputationTier(ReadGuardianReputation(guardian));
        if (tier == TradeReputationTier.Hostile)
            return new GuardianTradeOperationResult(false, false, "Этот Хранитель отказывается торговать с вами.");

        GuardianPolicyContracts.EnsureStrictCanonicalSoulStateRootsForPolicySensitiveWrite(soulRoot);
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

        var buybackRelics = EnsureBuybackRelicsArray(guardian);
        buybackRelics.Add(CreateBuybackEntry(
            guardianId,
            GuardianManifestation.GetDisplayName(guardian),
            CloneObject(relic),
            price,
            Math.Max(0, currentTurn)));
        SyncActiveGuardian(guardiansRoot, guardianId, guardian);

        var postSellSoulJson = GuardianPolicyContracts.CreatePatchedSoulStateWriteRoot(
            soulRoot,
            new GuardianPolicyContracts.SoulStatePatchConflictContext(
                GuardianPolicyContracts.SoulStatePatchTouchedDomains.InkFeathers |
                GuardianPolicyContracts.SoulStatePatchTouchedDomains.SoulRelics,
                removedSoulRelicIds: new[] { relicId })).ToJsonString(JsonOpts);
        if (!await CoordinatedStateWriteHelper.TryCommitAsync(
                _fs,
                new CoordinatedStateWriteHelper.PlannedWrite(SoulStatePath, preSellSoulJson, postSellSoulJson),
                new CoordinatedStateWriteHelper.PlannedWrite(GuardiansPath, preSellGuardiansJson, guardiansRoot.ToJsonString(JsonOpts))))
        {
            return new GuardianTradeOperationResult(
                false,
                false,
                "Не удалось безопасно зафиксировать продажу без расхождения между состоянием души и Хранителя. Изменения откатились к исходной версии.");
        }

        var relicName = GetNodeString(relic["name"]) ?? "Реликвия";
        return new GuardianTradeOperationResult(true, true, $"Продана реликвия «{relicName}» за {price} 🪶.");
    }

    public async Task<GuardianTradeOperationResult> BuyBackAsync(string guardianId, string buybackEntryId, int currentTurn)
    {
        if (currentTurn <= 0)
            return new GuardianTradeOperationResult(false, false, "Локальный выкуп реликвии требует актуальный номер хода.");

        var guardiansRoot = await ReadGuardiansRootAsync();
        var soulRoot = await ReadSoulStateRootAsync();
        if (guardiansRoot == null || soulRoot == null)
            return new GuardianTradeOperationResult(false, false, "Не удалось прочитать состояние торговли или души.");

        var preBuybackGuardiansJson = guardiansRoot.ToJsonString(JsonOpts);
        var preBuybackSoulJson = soulRoot.ToJsonString(JsonOpts);

        var guardian = FindGuardian(guardiansRoot, guardianId);
        if (guardian == null)
            return new GuardianTradeOperationResult(false, false, "Хранитель не найден.");

        if (!GuardianTradeAllowedHere(guardiansRoot, guardian))
            return new GuardianTradeOperationResult(false, false, "Торговать можно только с текущим активным Хранителем в обители, где вы сейчас находитесь.");

        var tier = GetTradeReputationTier(ReadGuardianReputation(guardian));
        if (tier == TradeReputationTier.Hostile)
            return new GuardianTradeOperationResult(false, false, "Этот Хранитель отказывается торговать с вами.");

        if (guardian[BuybackRelicsProperty] is not JsonArray buybackRelics)
            return new GuardianTradeOperationResult(false, false, "У этого Хранителя нет реликвий для обратного выкупа.");

        var buybackEntry = buybackRelics
            .OfType<JsonObject>()
            .FirstOrDefault(entry =>
                string.Equals(GetNodeString(entry["buybackEntryId"]), buybackEntryId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GetNodeString(entry["status"]), BuybackStatusAvailable, StringComparison.OrdinalIgnoreCase));
        if (buybackEntry == null)
            return new GuardianTradeOperationResult(false, false, "Эта реликвия больше недоступна для обратного выкупа.");

        if (buybackEntry["relicData"] is not JsonObject relicData)
            return new GuardianTradeOperationResult(false, false, "Данные реликвии для обратного выкупа повреждены.");

        var price = GetNodeInt(buybackEntry["buybackPrice"], GetNodeInt(buybackEntry["soldForPrice"], 0));
        if (price <= 0)
            return new GuardianTradeOperationResult(false, false, "Цена обратного выкупа повреждена.");

        GuardianPolicyContracts.EnsureStrictCanonicalSoulStateRootsForPolicySensitiveWrite(soulRoot);
        if (!TryModifyInkFeathers(soulRoot, -price))
            return new GuardianTradeOperationResult(false, false, "Недостаточно Чернильных Перьев.");

        NormalizeSoulRelicsShape(soulRoot);
        var stored = ((JsonObject)soulRoot["soulRelics"]!)["stored"]!.AsArray();
        UpsertRelic(stored, CloneObject(relicData));

        buybackEntry["status"] = BuybackStatusRebought;
        buybackEntry["reboughtAtTurn"] = Math.Max(0, currentTurn);
        buybackEntry["reboughtAtUtc"] = DateTimeOffset.UtcNow.ToString("O");
        SyncActiveGuardian(guardiansRoot, guardianId, guardian);

        var postBuybackSoulJson = GuardianPolicyContracts.CreatePatchedSoulStateWriteRoot(
            soulRoot,
            new GuardianPolicyContracts.SoulStatePatchConflictContext(
                GuardianPolicyContracts.SoulStatePatchTouchedDomains.InkFeathers |
                GuardianPolicyContracts.SoulStatePatchTouchedDomains.SoulRelics,
                upsertedSoulRelicIds: new[] { GetNodeString(relicData["relicId"]) ?? string.Empty })).ToJsonString(JsonOpts);
        if (!await CoordinatedStateWriteHelper.TryCommitAsync(
                _fs,
                new CoordinatedStateWriteHelper.PlannedWrite(SoulStatePath, preBuybackSoulJson, postBuybackSoulJson),
                new CoordinatedStateWriteHelper.PlannedWrite(GuardiansPath, preBuybackGuardiansJson, guardiansRoot.ToJsonString(JsonOpts))))
        {
            return new GuardianTradeOperationResult(
                false,
                false,
                "Не удалось безопасно зафиксировать обратный выкуп без расхождения между состоянием души и Хранителя. Изменения откатились к исходной версии.");
        }

        var relicName = GetNodeString(relicData["name"]) ?? "Реликвия";
        return new GuardianTradeOperationResult(true, true, $"Выкуплена обратно реликвия «{relicName}» за {price} 🪶.");
    }

    private async Task<(TradeReputationTier Tier, GuardianTradeView View, bool Changed, bool TrackerChanged)> EnsureTradeInventoryStateAsync(
        JsonObject root,
        JsonObject guardian,
        int currentIncarnation,
        int currentTurn,
        JsonObject? trackerRoot)
    {
        var guardianId = GetNodeString(guardian["guardianId"]) ?? "";
        var guardianName = GuardianManifestation.GetDisplayName(guardian);
        if (string.IsNullOrWhiteSpace(guardianName))
            guardianName = guardianId;
        var reputation = ReadGuardianReputation(guardian);
        var tier = GetTradeReputationTier(reputation);
        var cycleId = GetTradeCycleId(currentIncarnation);
        var derivedState = GuardianProjectState.ResolveGuardianDerivedState(guardian, trackerRoot);
        var blocked = tier == TradeReputationTier.Hostile || !GuardianTradeAllowedHere(root, guardian);
        var changed = false;
        var trackerChanged = false;
        var inventoryReady = false;
        var inventoryRequestPending = false;
        var inventoryRequestCreatedThisCall = false;
        string? inventoryStatusMessage = null;
        string? pendingGmAction = null;
        var blockedReason = blocked ? BuildTradeBlockedReason(root, guardian, reputation) : null;

        if (!blocked)
        {
            var tradeInventory = guardian["tradeInventory"] as JsonObject;
            var pendingRequestState = await GuardianTradeRequestState.ReadStateAsync(_fs);
            var pendingRequest = pendingRequestState.Request;
            if (TradeInventoryMatchesContract(tradeInventory, cycleId, derivedState))
            {
                var requestMatchesCurrentContract = pendingRequest != null &&
                    GuardianTradeRequestState.MatchesCurrentContract(pendingRequest, guardianId, cycleId, reputation, derivedState);
                var hasForeignLivePendingRequest = pendingRequest != null && !requestMatchesCurrentContract;
                var hasMatchingReceipt = requestMatchesCurrentContract &&
                    GuardianTradeRequestState.ReceiptMatchesRequestContract(
                        GuardianTradeRequestState.FindMatchingReceipt(guardian, pendingRequest!),
                        pendingRequest!,
                        tradeInventory);
                var hasCanonicalReadyReceipt = requestMatchesCurrentContract
                    ? hasMatchingReceipt
                    : HasCanonicalReadyReceiptForInventory(guardian, tradeInventory);
                inventoryReady = hasCanonicalReadyReceipt;
                if (tradeInventory != null)
                {
                    changed = RepriceTradeInventory(tradeInventory, tier);
                    if (changed)
                        SyncActiveGuardian(root, guardianId, guardian);
                }

                if (!hasCanonicalReadyReceipt)
                {
                    if (pendingRequestState.IsMalformed)
                    {
                        inventoryRequestPending = true;
                        inventoryStatusMessage = "pending_guardian_trade_request.json повреждён. Новый запрос и каноническое закрытие торговли заблокированы, пока pending contract не будет исправлен или очищен.";
                        return (
                            tier,
                            BuildTradeView(
                                guardian,
                                cycleId,
                                blocked,
                                blockedReason,
                                inventoryReady,
                                inventoryRequestPending,
                                inventoryRequestCreatedThisCall,
                                inventoryStatusMessage,
                                pendingGmAction),
                            changed,
                            trackerChanged);
                    }

                    if (hasForeignLivePendingRequest)
                    {
                        inventoryRequestPending = true;
                        inventoryStatusMessage = "pending_guardian_trade_request.json содержит другой живой торговый контракт. Витрина заблокирована, пока текущий pending request не будет закрыт canonical receipt или исправлен вручную.";
                        return (
                            tier,
                            BuildTradeView(
                                guardian,
                                cycleId,
                                true,
                                inventoryStatusMessage,
                                inventoryReady: false,
                                inventoryRequestPending,
                                inventoryRequestCreatedThisCall,
                                inventoryStatusMessage,
                                pendingGmAction),
                            changed,
                            trackerChanged);
                    }

                    inventoryRequestPending = requestMatchesCurrentContract;
                    if (!inventoryRequestPending)
                    {
                        var abodeId = guardian["abode"] is JsonObject abode ? GetNodeString(abode["abodeId"]) ?? "" : "";
                        pendingRequest = new GuardianTradeRequestState.PendingGuardianTradeRequest
                        {
                            GuardianId = guardianId,
                            GuardianName = guardianName,
                            AbodeId = abodeId,
                            ReturnCycleId = cycleId,
                            CurrentReputation = reputation,
                            DerivedTradeSlotCount = derivedState.TradeSlotCount,
                            EffectiveRarityCeilingBonusSteps = derivedState.EffectiveGuardianRarityCeilingBonusSteps,
                            ProjectBonusSignature = GuardianProjectState.BuildTradeBonusSignature(derivedState),
                            CreatedAtTurn = Math.Max(0, currentTurn)
                        };
                        await GuardianTradeRequestState.WriteAsync(_fs, pendingRequest);
                        inventoryRequestPending = true;
                        inventoryRequestCreatedThisCall = true;
                    }

                    inventoryStatusMessage = inventoryRequestCreatedThisCall
                        ? "Витрина Хранителя уже проявлена, но ещё не подтверждена каноническим итогом. Запрос на закрытие ассортимента отправлен GM."
                        : "Витрина Хранителя ожидает канонического подтверждения. Повторите после ответа GM.";
                    pendingGmAction ??=
                        $"[{GuardianTradeRequestState.ActionTag}] У Хранителя {guardianName} ({guardianId}) уже materialized витрина текущего return cycle, но отсутствует canonical ready receipt. " +
                        $"Обязательно закрой текущий контракт через {GuardianTradeRequestState.UpdateReceiptsProperty} в guardians.json вместо повторной генерации новой витрины.";
                }

                if (pendingRequest != null && requestMatchesCurrentContract && (hasMatchingReceipt || hasCanonicalReadyReceipt))
                    GuardianTradeRequestState.Clear(_fs);
            }
            else
            {
                if (pendingRequestState.IsMalformed)
                {
                    inventoryRequestPending = true;
                    inventoryStatusMessage = "pending_guardian_trade_request.json повреждён. Новый торговый запрос заблокирован, пока pending contract не будет исправлен или очищен.";
                    return (
                        tier,
                        BuildTradeView(
                            guardian,
                            cycleId,
                            blocked,
                            blockedReason,
                            inventoryReady,
                            inventoryRequestPending,
                            inventoryRequestCreatedThisCall,
                            inventoryStatusMessage,
                            pendingGmAction),
                        changed,
                        trackerChanged);
                }

                inventoryRequestPending = GuardianTradeRequestState.MatchesCurrentContract(
                    pendingRequest,
                    guardianId,
                    cycleId,
                    reputation,
                    derivedState);

                if (pendingRequest != null && !inventoryRequestPending)
                {
                    inventoryStatusMessage = "pending_guardian_trade_request.json содержит другой живой торговый контракт. Новый запрос на подготовку витрины заблокирован, пока текущий pending request не будет закрыт canonical receipt или исправлен вручную.";
                    return (
                        tier,
                        BuildTradeView(
                            guardian,
                            cycleId,
                            true,
                            inventoryStatusMessage,
                            inventoryReady,
                            inventoryRequestPending: true,
                            inventoryRequestCreatedThisCall,
                            inventoryStatusMessage,
                            pendingGmAction),
                        changed,
                        trackerChanged);
                }

                if (!inventoryRequestPending)
                {
                    var abodeId = guardian["abode"] is JsonObject abode ? GetNodeString(abode["abodeId"]) ?? "" : "";
                    pendingRequest = new GuardianTradeRequestState.PendingGuardianTradeRequest
                    {
                        GuardianId = guardianId,
                        GuardianName = guardianName,
                        AbodeId = abodeId,
                        ReturnCycleId = cycleId,
                        CurrentReputation = reputation,
                        DerivedTradeSlotCount = derivedState.TradeSlotCount,
                        EffectiveRarityCeilingBonusSteps = derivedState.EffectiveGuardianRarityCeilingBonusSteps,
                        ProjectBonusSignature = GuardianProjectState.BuildTradeBonusSignature(derivedState),
                        CreatedAtTurn = Math.Max(0, currentTurn)
                    };
                    await GuardianTradeRequestState.WriteAsync(_fs, pendingRequest);
                    inventoryRequestPending = true;
                    inventoryRequestCreatedThisCall = true;
                    pendingGmAction =
                        $"[{GuardianTradeRequestState.ActionTag}] Игрок открывает торговлю с Хранителем {guardianName} ({guardianId}), но актуальная витрина отсутствует или устарела. " +
                        $"Обязательно прочитай {GuardianTradeRequestState.PendingRequestPath} как client-authored contract. " +
                        "Сгенерируй explicit guardian.tradeInventory для текущего return cycle, а не выводи ассортимент из guardian.domain. " +
                        $"После materialization закрой запрос canonical receipt через {GuardianTradeRequestState.UpdateReceiptsProperty} в guardians.json. " +
                        "Витрина должна уважать derivedTradeSlotCount, generation/pricing reputation tier и projectBonusSignature из request.";
                }

                inventoryStatusMessage = inventoryRequestCreatedThisCall
                    ? "Витрина Хранителя подготавливается. Запрос на формирование ассортимента отправлен GM."
                    : "Витрина Хранителя ещё подготавливается. Повторите после ответа GM.";
            }
        }

        return (
            tier,
            BuildTradeView(
                guardian,
                cycleId,
                blocked,
                blockedReason,
                inventoryReady,
                inventoryRequestPending,
                inventoryRequestCreatedThisCall,
                inventoryStatusMessage,
                pendingGmAction),
            changed,
            trackerChanged);
    }

    private GuardianTradeView BuildTradeView(
        JsonObject guardian,
        string cycleId,
        bool blocked,
        string? blockedReason,
        bool inventoryReady,
        bool inventoryRequestPending,
        bool inventoryRequestCreatedThisCall,
        string? inventoryStatusMessage,
        string? pendingGmAction)
    {
        var guardianId = GetNodeString(guardian["guardianId"]) ?? "";
        var guardianName = GuardianManifestation.GetDisplayName(guardian);
        if (string.IsNullOrWhiteSpace(guardianName))
            guardianName = guardianId;
        var domain = GetNodeString(guardian["domain"]) ?? "";
        var rep = ReadGuardianReputation(guardian);
        var offers = new List<GuardianTradeOffer>();
        var buybackOffers = ReadBuybackOffers(guardian);

        if (!blocked && inventoryReady &&
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
            domain,
            rep,
            GetReputationTierLabel(rep),
            blocked,
            blockedReason,
            cycleId,
            inventoryReady,
            inventoryRequestPending,
            inventoryRequestCreatedThisCall,
            inventoryStatusMessage,
            pendingGmAction,
            offers,
            buybackOffers);
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

    private async Task<JsonObject?> ReadGuardianProjectTrackerRootAsync()
    {
        var json = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать guardian_projects.json для торговли Хранителя");
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

    private static bool HasCanonicalReadyReceiptForInventory(JsonObject guardian, JsonObject? tradeInventory)
    {
        if (tradeInventory == null || guardian[GuardianTradeRequestState.ReceiptsProperty] is not JsonArray receipts)
            return false;

        var guardianId = GetNodeString(guardian["guardianId"]);
        var abodeId = guardian["abode"] is JsonObject abode ? GetNodeString(abode["abodeId"]) : null;
        var tradeCycleId = GetNodeString(tradeInventory["tradeCycleId"]);
        var itemCount = GuardianTradeRequestState.GetTradeInventoryItemCount(tradeInventory);
        if (string.IsNullOrWhiteSpace(guardianId) ||
            string.IsNullOrWhiteSpace(abodeId) ||
            string.IsNullOrWhiteSpace(tradeCycleId))
        {
            return false;
        }

        var matchingReceiptCount = receipts.OfType<JsonObject>().Count(receipt =>
            string.Equals(GetNodeString(receipt["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(GetNodeString(receipt["abodeId"]), abodeId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(GetNodeString(receipt["tradeCycleId"]), tradeCycleId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(GetNodeString(receipt["status"]), GuardianTradeRequestState.ReceiptStatusReady, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(GetNodeString(receipt["requestId"])) &&
            GetNodeInt(receipt["itemCount"], -1) == itemCount &&
            GetNodeInt(receipt["resolvedAtTurn"], 0) > 0 &&
            !string.IsNullOrWhiteSpace(GetNodeString(receipt["resolvedAtUtc"])));

        return matchingReceiptCount == 1;
    }

    private static bool TradeInventoryMatchesContract(
        JsonObject? tradeInventory,
        string cycleId,
        GuardianProjectState.ResolvedGuardianDerivedState derivedState)
    {
        if (tradeInventory == null)
            return false;

        if (!string.Equals(GetNodeString(tradeInventory["tradeCycleId"]), cycleId, StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrWhiteSpace(GetNodeString(tradeInventory["generatedAtUtc"])))
            return false;

        var generationTier = GetNodeString(tradeInventory["generationReputationTier"]);
        if (!IsValidTradeTierCode(generationTier))
            return false;

        var pricingTier = GetNodeString(tradeInventory["pricingReputationTier"]);
        if (!IsValidTradeTierCode(pricingTier))
            return false;

        if (tradeInventory["items"] is not JsonArray items || items.Count != derivedState.TradeSlotCount)
            return false;

        if (GetNodeInt(tradeInventory["effectiveRarityCeilingBonusSteps"], int.MinValue) != derivedState.EffectiveGuardianRarityCeilingBonusSteps)
            return false;

        if (!string.Equals(
                GetNodeString(tradeInventory["projectBonusSignature"]) ?? string.Empty,
                GuardianProjectState.BuildTradeBonusSignature(derivedState),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return items.OfType<JsonObject>().All(item =>
            !string.IsNullOrWhiteSpace(GetNodeString(item["slotId"])) &&
            item["priceInFeathers"] is JsonValue &&
            GetNodeInt(item["priceInFeathers"], -1) >= 0 &&
            item["soldOut"] is JsonValue soldOut &&
            (soldOut.TryGetValue<bool>(out _) || bool.TryParse(soldOut.ToString(), out _)) &&
            item["relicData"] is JsonObject relicData &&
            !string.IsNullOrWhiteSpace(GetNodeString(relicData["relicId"])) &&
            !string.IsNullOrWhiteSpace(GetNodeString(relicData["name"])) &&
            (!string.IsNullOrWhiteSpace(GetNodeString(relicData["quality"])) ||
             !string.IsNullOrWhiteSpace(GetNodeString(relicData["rarity"]))) &&
            IsRarityAllowedForGenerationTier(GetRelicRarity(relicData), generationTier!, GetNodeInt(item["rarityBonusStepsApplied"], 0)));
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

    internal static bool IsValidBuybackStatusCode(string? statusCode) =>
        statusCode is BuybackStatusAvailable or BuybackStatusRebought or BuybackStatusRemoved;

    internal static int ComputeBuyPriceForTierCode(string rarity, string tierCode) =>
        ComputeBuyPrice(rarity, ParseTradeTierCode(tierCode));

    internal static bool IsRarityAllowedForGenerationTier(string rarity, string tierCode)
        => IsRarityAllowedForGenerationTier(rarity, tierCode, 0);

    internal static bool IsRarityAllowedForGenerationTier(string rarity, string tierCode, int rarityBonusStepsApplied)
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

        return rarityRank <= Math.Min(GetRarityRank("Legendary"), maxRank + Math.Max(0, rarityBonusStepsApplied));
    }

    private static int ComputeBuyPrice(string rarity, TradeReputationTier tier)
    {
        var basePrice = rarity switch
        {
            "Common" => 30,
            "Uncommon" => 70,
            "Rare" => 140,
            "Epic" => 260,
            "Legendary" => 420,
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

        return double.IsInfinity(multiplier) ? int.MaxValue : (int)Math.Ceiling(basePrice * multiplier);
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

    private static JsonArray EnsureBuybackRelicsArray(JsonObject guardian)
    {
        if (guardian[BuybackRelicsProperty] is JsonArray buybackRelics)
            return buybackRelics;

        buybackRelics = new JsonArray();
        guardian[BuybackRelicsProperty] = buybackRelics;
        return buybackRelics;
    }

    private static IReadOnlyList<GuardianBuybackOffer> ReadBuybackOffers(JsonObject guardian)
    {
        if (guardian[BuybackRelicsProperty] is not JsonArray buybackRelics)
            return Array.Empty<GuardianBuybackOffer>();

        return buybackRelics
            .OfType<JsonObject>()
            .Where(entry => string.Equals(GetNodeString(entry["status"]), BuybackStatusAvailable, StringComparison.OrdinalIgnoreCase))
            .Select(entry =>
            {
                if (entry["relicData"] is not JsonObject relicData)
                    return null;

                return new GuardianBuybackOffer(
                    GetNodeString(entry["buybackEntryId"]) ?? "",
                    GetNodeString(entry["relicId"]) ?? GetNodeString(relicData["relicId"]) ?? "",
                    GetNodeString(relicData["name"]) ?? "Реликвия",
                    GetRelicRarity(relicData),
                    GetNodeInt(entry["buybackPrice"], GetNodeInt(entry["soldForPrice"], 0)),
                    GetNodeInt(entry["soldForPrice"], 0),
                    GetNodeInt(entry["soldByPlayerAtTurn"], 0),
                    GetNodeString(relicData["description"]) ?? "",
                    CloneObject(relicData));
            })
            .Where(offer => offer != null &&
                            !string.IsNullOrWhiteSpace(offer.BuybackEntryId) &&
                            !string.IsNullOrWhiteSpace(offer.RelicId))
            .Cast<GuardianBuybackOffer>()
            .OrderByDescending(offer => GetRarityRank(offer.Rarity))
            .ThenBy(offer => offer.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static JsonObject CreateBuybackEntry(string guardianId, string? guardianName, JsonObject relicData, int soldForPrice, int currentTurn)
    {
        var relicId = GetNodeString(relicData["relicId"]) ?? GetNodeString(relicData["id"]) ?? "";
        return new JsonObject
        {
            ["buybackEntryId"] = $"guardian_buyback_{SanitizeIdentifierPart(guardianId)}_{Guid.NewGuid():N}",
            ["guardianId"] = guardianId,
            ["guardianName"] = string.IsNullOrWhiteSpace(guardianName) ? guardianId : guardianName,
            ["relicId"] = relicId,
            ["relicData"] = CloneObject(relicData),
            ["soldByPlayerAtTurn"] = currentTurn,
            ["soldByPlayerAtUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["soldForPrice"] = soldForPrice,
            ["buybackPrice"] = soldForPrice,
            ["acquiredFromPlayer"] = true,
            ["status"] = BuybackStatusAvailable
        };
    }

    private static string SanitizeIdentifierPart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";

        return new string(value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
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
}
