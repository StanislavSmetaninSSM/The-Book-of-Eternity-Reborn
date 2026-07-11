using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class ShiningTradeService
{
    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;
    private sealed record CoordinatedStateWrite(string Path, string PreWriteJson, string PostWriteJson);

    public sealed record ShiningTradeOffer(
        string SlotId,
        string Name,
        string Rarity,
        int PriceInFeathers,
        string Description,
        bool SoldOut,
        JsonObject RelicData);

    public sealed record ShiningTradeView(
        string FactionId,
        string FactionName,
        int FactionStrength,
        int TradeTier,
        int StockItemCount,
        string RarityCeiling,
        double ServiceMultiplier,
        bool TradeBlocked,
        string? BlockReason,
        string TradeCycleId,
        bool InventoryReady,
        bool InventoryRequestPending,
        string? InventoryStatusMessage,
        IReadOnlyList<ShiningTradeOffer> Offers);

    public sealed record ShiningTradeOperationResult(bool Success, bool StateChanged, string Message);
    public sealed record ShiningTradeAutoRefreshResult(bool StateChanged, int CreatedRequestCount, string TradeCycleId);
    private sealed record ShiningTradeAutoRefreshPlan(
        bool StateChanged,
        int CreatedRequestCount,
        string TradeCycleId,
        string? PreviousRequestsJson,
        IReadOnlyList<ShiningTradeRequestState.PendingShiningTradeInventoryRequest> Requests);

    public static async Task<IReadOnlyList<ShiningTradeView>> GetCurrentRealmTradeTargetsAsync(FileSystemManager fs)
    {
        var root = await ReadJsonObjectAsync(fs, ShiningAbodeState.StatePath);
        if (root == null)
            return Array.Empty<ShiningTradeView>();

        var localScope = await new LocalInteractionScopeService(fs).ResolveAsync();
        if (!localScope.IsResolved || localScope.RealmKind != LocalInteractionRealmKind.ShiningAbode)
            return Array.Empty<ShiningTradeView>();

        var targets = new List<ShiningTradeView>();
        foreach (var faction in SarefMainStoryState.GetPlayerVisibleShiningFactions(root)
                     .Where(faction => LocalInteractionScopeService.IsShiningFactionLocal(localScope, faction)))
        {
            var factionId = GetNodeString(faction["factionId"]);
            if (string.IsNullOrWhiteSpace(factionId))
                continue;

            var view = await ReadTradeViewAsync(fs, factionId);
            if (view != null)
                targets.Add(view);
        }

        return targets
            .OrderByDescending(static target => target.FactionStrength)
            .ThenBy(static target => target.FactionName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static Task<ShiningTradeView?> ReadTradeViewAsync(FileSystemManager fs, string factionId) =>
        ReadTradeViewAsync(fs, factionId, new LocalInteractionScopeService(fs));

    internal static async Task<ShiningTradeView?> ReadTradeViewAsync(
        FileSystemManager fs,
        string factionId,
        ILocalInteractionScopeResolver localScopeResolver)
    {
        if (!await IsFactionAvailableInCurrentHallAsync(fs, factionId, localScopeResolver))
            return null;

        var shiningRoot = await ReadJsonObjectAsync(fs, ShiningAbodeState.StatePath);
        var residentRoot = await ReadJsonObjectAsync(fs, GuardianAbodeResidentState.StatePath);
        var guardiansRoot = await ReadJsonObjectAsync(fs, "game_state/meta/guardians.json");
        var soulRoot = await ReadJsonObjectAsync(fs, "game_state/meta/soul_state.json");
        if (shiningRoot == null)
            return null;
        var shiningRootBaseline = shiningRoot.ToJsonString(JsonOpts);

        var rawOwnerStateError = ShiningAbodeState.ValidateRawOwnerStateForActionableMode(shiningRoot);
        if (rawOwnerStateError != null)
        {
            var rawFaction = ShiningAbodeState.FindFaction(shiningRoot, factionId);
            if (rawFaction == null)
                return null;

            var rawFactionName = GetNodeString(rawFaction["charter"]?["factionName"]) ?? factionId;
            var rawFactionStrength = GetNodeInt(rawFaction["factionStrength"], 0);
            if (!await IsFactionAvailableInCurrentHallAsync(fs, factionId, localScopeResolver, shiningRootBaseline))
                return null;

            return new ShiningTradeView(
                factionId,
                rawFactionName,
                rawFactionStrength,
                ShiningAbodeState.GetTradeTier(rawFactionStrength),
                ShiningAbodeState.GetTradeStockItemCount(rawFaction, residentRoot),
                ShiningAbodeState.GetTradeRarityCeiling(rawFactionStrength),
                ShiningAbodeState.GetServiceMultiplier(rawFactionStrength),
                true,
                rawOwnerStateError,
                ShiningAbodeState.GetTradeCycleId(GetNodeInt(soulRoot?["currentIncarnation"], 0)),
                false,
                false,
                rawOwnerStateError,
                Array.Empty<ShiningTradeOffer>());
        }

        ShiningAbodeState.NormalizeStateRoot(shiningRoot, residentRoot, guardiansRoot);
        var faction = ShiningAbodeState.FindFaction(shiningRoot, factionId);
        if (faction == null)
            return null;

        var factionName = GetNodeString(faction["charter"]?["factionName"]) ?? factionId;
        var factionStrength = GetNodeInt(faction["factionStrength"], 0);
        var tradeTier = ShiningAbodeState.GetTradeTier(factionStrength);
        var stockItemCount = ShiningAbodeState.GetTradeStockItemCount(faction, residentRoot);
        var rarityCeiling = ShiningAbodeState.GetTradeRarityCeiling(factionStrength);
        var serviceMultiplier = ShiningAbodeState.GetServiceMultiplier(factionStrength);
        var tradeCycleId = ShiningAbodeState.GetTradeCycleId(GetNodeInt(soulRoot?["currentIncarnation"], 0));

        var blockedReason = ResolveTradeBlockedReason(soulRoot, shiningRoot, tradeTier);
        var tradeBlocked = !string.IsNullOrWhiteSpace(blockedReason);
        var inventoryReady = false;
        var inventoryRequestPending = false;
        string? inventoryStatusMessage = null;
        var offers = new List<ShiningTradeOffer>();

        var requestState = await ShiningTradeRequestState.ReadRequestsStateAsync(fs);
        var matchingRequests = requestState.Requests
            .Where(request =>
                string.Equals(request.FactionId, factionId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(request.TradeCycleId, tradeCycleId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var matchingRequest = matchingRequests.Count == 1 ? matchingRequests[0] : null;
        var duplicatePendingRequests = matchingRequests.Count > 1;
        inventoryRequestPending = matchingRequest != null || requestState.IsMalformed || duplicatePendingRequests;

        var currentContract = new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
        {
            FactionId = factionId,
            FactionName = factionName,
            TradeCycleId = tradeCycleId,
            DerivedTradeTier = tradeTier,
            DerivedTradeSlotCount = stockItemCount,
            DerivedRarityCeiling = rarityCeiling,
            DerivedServiceMultiplier = serviceMultiplier
        };

        if (requestState.IsMalformed)
        {
            inventoryReady = false;
            inventoryStatusMessage = "Ожидающий торговый запрос сияющей фракции повреждён. Новая витрина и покупки заблокированы, пока этот контракт не будет исправлен или очищен.";
        }
        else if (!tradeBlocked &&
            matchingRequest != null &&
            ShiningTradeRequestState.HasReadyInventoryForCurrentContract(faction, matchingRequest))
        {
            inventoryReady = true;
        }
        else if (!tradeBlocked &&
                 matchingRequest == null &&
                 !duplicatePendingRequests &&
                 ShiningTradeRequestState.FindLatestAuthoritativeReadyReceiptForCurrentCycle(faction, tradeCycleId) != null &&
                 ShiningTradeRequestState.InventoryMatchesRequestContract(faction["tradeInventory"] as JsonObject, currentContract))
        {
            inventoryReady = true;
        }
        else if (!tradeBlocked)
        {
            inventoryStatusMessage = requestState.IsMalformed
                ? "Ожидающий торговый запрос сияющей фракции повреждён. Новая витрина заблокирована, пока этот контракт не будет исправлен или очищен."
                : duplicatePendingRequests
                ? BuildDuplicatePendingTradeRequestsMessage(matchingRequests)
                : inventoryRequestPending
                ? "Витрина сияющей фракции уже запрошена и ждёт канонического подтверждения."
                : "Для этой фракции витрина текущего цикла ещё не подготовлена.";
        }

        if (inventoryReady &&
            faction["tradeInventory"] is JsonObject readyInventory &&
            readyInventory["items"] is JsonArray items)
        {
            foreach (var item in items.OfType<JsonObject>())
            {
                if (item["relicData"] is not JsonObject relicData)
                    continue;

                offers.Add(new ShiningTradeOffer(
                    GetNodeString(item["slotId"]) ?? "",
                    GetNodeString(relicData["name"]) ?? "Реликвия",
                    GetNodeString(relicData["quality"]) ?? GetNodeString(relicData["rarity"]) ?? "?",
                    GetNodeInt(item["priceInFeathers"], 0),
                    GetNodeString(relicData["description"]) ?? "",
                    GetNodeBool(item["soldOut"]),
                    relicData.DeepClone().AsObject()));
            }
        }

        if (!await IsFactionAvailableInCurrentHallAsync(fs, factionId, localScopeResolver, shiningRootBaseline))
            return null;

        return new ShiningTradeView(
            factionId,
            factionName,
            factionStrength,
            tradeTier,
            stockItemCount,
            rarityCeiling,
            serviceMultiplier,
            tradeBlocked,
            blockedReason,
            tradeCycleId,
            inventoryReady,
            inventoryRequestPending,
            inventoryStatusMessage,
            offers);
    }

    private static string BuildDuplicatePendingTradeRequestsMessage(
        IReadOnlyList<ShiningTradeRequestState.PendingShiningTradeInventoryRequest> requests)
    {
        var lines = new List<string>
        {
            "Для этой фракции найдено несколько ожидающих запросов одного цикла. Торговля заблокирована, пока не останется один канонический контракт.",
            "Полный список конкурирующих запросов:"
        };

        foreach (var request in requests.OrderBy(item => item.CreatedAtTurn).ThenBy(item => item.CreatedAtUtc, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add(
                $"- requestId={request.RequestId}; factionId={request.FactionId}; factionName={request.FactionName}; tradeCycleId={request.TradeCycleId}; " +
                $"derivedTradeTier={request.DerivedTradeTier}; derivedTradeSlotCount={request.DerivedTradeSlotCount}; " +
                $"derivedRarityCeiling={request.DerivedRarityCeiling}; derivedServiceMultiplier={request.DerivedServiceMultiplier:0.00}; " +
                $"createdAtTurn={request.CreatedAtTurn}; createdAtUtc={request.CreatedAtUtc}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static Task<ShiningTradeOperationResult> RequestInventoryAsync(
        FileSystemManager fs,
        string factionId,
        int currentTurn) =>
        RequestInventoryAsync(fs, factionId, currentTurn, new LocalInteractionScopeService(fs));

    internal static async Task<ShiningTradeOperationResult> RequestInventoryAsync(
        FileSystemManager fs,
        string factionId,
        int currentTurn,
        ILocalInteractionScopeResolver localScopeResolver)
    {
        if (currentTurn <= 0)
            return new ShiningTradeOperationResult(false, false, "Shining trade request требует актуальный номер хода.");

        if (!await IsFactionAvailableInCurrentHallAsync(fs, factionId, localScopeResolver))
            return new ShiningTradeOperationResult(false, false, "Эта сияющая фракция недоступна в текущем мире или зале.");

        var shiningRoot = await ReadJsonObjectAsync(fs, ShiningAbodeState.StatePath);
        var residentRoot = await ReadJsonObjectAsync(fs, GuardianAbodeResidentState.StatePath);
        var guardiansRoot = await ReadJsonObjectAsync(fs, "game_state/meta/guardians.json");
        var soulRoot = await ReadJsonObjectAsync(fs, "game_state/meta/soul_state.json");
        if (shiningRoot == null)
            return new ShiningTradeOperationResult(false, false, "Не удалось прочитать shining_abode_state.json.");
        var shiningRootBaseline = shiningRoot.ToJsonString(JsonOpts);

        ShiningAbodeState.NormalizeStateRoot(shiningRoot, residentRoot, guardiansRoot);
        var faction = ShiningAbodeState.FindFaction(shiningRoot, factionId);
        if (faction == null)
            return new ShiningTradeOperationResult(false, false, "Фракция не найдена.");

        var strength = GetNodeInt(faction["factionStrength"], 0);
        var request = new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
        {
            FactionId = factionId,
            FactionName = GetNodeString(faction["charter"]?["factionName"]) ?? factionId,
            TradeCycleId = ShiningAbodeState.GetTradeCycleId(GetNodeInt(soulRoot?["currentIncarnation"], 0)),
            DerivedTradeTier = ShiningAbodeState.GetTradeTier(strength),
            DerivedTradeSlotCount = ShiningAbodeState.GetTradeStockItemCount(faction, residentRoot),
            DerivedRarityCeiling = ShiningAbodeState.GetTradeRarityCeiling(strength),
            DerivedServiceMultiplier = ShiningAbodeState.GetServiceMultiplier(strength),
            CreatedAtTurn = currentTurn
        };

        var error = await ShiningTradeRequestState.ValidateRequestAgainstCurrentStateAsync(fs, request);
        if (!string.IsNullOrWhiteSpace(error))
            return new ShiningTradeOperationResult(false, false, error);

        var requestScope = await ResolveFactionAvailableInCurrentHallScopeAsync(
            fs,
            factionId,
            localScopeResolver,
            shiningRootBaseline);
        if (requestScope == null)
            return new ShiningTradeOperationResult(false, false, "Фракция покинула текущий зал до создания запроса. Состояние не изменено.");

        if (!await ShiningTradeRequestState.TryWriteScopedRequestAsync(fs, request, requestScope))
            return new ShiningTradeOperationResult(false, false, "Мир или зал изменились до создания запроса. Состояние не изменено.");
        return new ShiningTradeOperationResult(true, true, $"Создан ожидающий запрос сияющей торговли для фракции «{request.FactionName}». В принятом ходе GM должен явно оформить tradeInventory и receipt.");
    }

    public static async Task<ShiningTradeAutoRefreshResult> SyncAutoRefreshRequestsForCurrentCycleAsync(
        FileSystemManager fs,
        int currentTurn)
    {
        const int maxSnapshotAttempts = 3;
        string lastTradeCycleId = string.Empty;
        for (var attempt = 0; attempt < maxSnapshotAttempts; attempt++)
        {
            var plan = await BuildAutoRefreshRequestsForCurrentCycleAsync(fs, currentTurn);
            lastTradeCycleId = plan.TradeCycleId;
            if (!plan.StateChanged)
                return new ShiningTradeAutoRefreshResult(false, 0, plan.TradeCycleId);

            if (await ShiningTradeRequestState.TryReplaceRequestsSnapshotAsync(
                    fs,
                    plan.PreviousRequestsJson,
                    plan.Requests))
            {
                return new ShiningTradeAutoRefreshResult(true, plan.CreatedRequestCount, plan.TradeCycleId);
            }
        }

        return new ShiningTradeAutoRefreshResult(false, 0, lastTradeCycleId);
    }

    public static async Task<ShiningTradeAutoRefreshResult> PreviewAutoRefreshRequestsForCurrentCycleAsync(
        FileSystemManager fs,
        JsonObject? projectedSoulRoot,
        JsonObject? projectedShiningRoot,
        int currentTurn)
    {
        var plan = await BuildAutoRefreshRequestsForCurrentCycleAsync(
            fs,
            currentTurn,
            projectedSoulRoot,
            projectedShiningRoot);
        return new ShiningTradeAutoRefreshResult(plan.StateChanged, plan.CreatedRequestCount, plan.TradeCycleId);
    }

    private static async Task<ShiningTradeAutoRefreshPlan> BuildAutoRefreshRequestsForCurrentCycleAsync(
        FileSystemManager fs,
        int currentTurn,
        JsonObject? soulRootOverride = null,
        JsonObject? shiningRootOverride = null)
    {
        var soulRoot = soulRootOverride?.DeepClone() as JsonObject ?? await ReadJsonObjectAsync(fs, "game_state/meta/soul_state.json");
        var shiningRoot = shiningRootOverride?.DeepClone() as JsonObject ?? await ReadJsonObjectAsync(fs, ShiningAbodeState.StatePath);
        var residentRoot = await ReadJsonObjectAsync(fs, GuardianAbodeResidentState.StatePath);
        var guardiansRoot = await ReadJsonObjectAsync(fs, "game_state/meta/guardians.json");
        if (soulRoot == null || shiningRoot == null)
            return new ShiningTradeAutoRefreshPlan(false, 0, string.Empty, null, Array.Empty<ShiningTradeRequestState.PendingShiningTradeInventoryRequest>());

        ShiningAbodeState.NormalizeStateRoot(shiningRoot, residentRoot, guardiansRoot);
        var blockedReason = ResolveTradeBlockedReason(soulRoot, shiningRoot, tradeTier: 1);
        if (!string.IsNullOrWhiteSpace(blockedReason))
            return new ShiningTradeAutoRefreshPlan(false, 0, string.Empty, null, Array.Empty<ShiningTradeRequestState.PendingShiningTradeInventoryRequest>());

        var tradeCycleId = ShiningAbodeState.GetTradeCycleId(GetNodeInt(soulRoot["currentIncarnation"], 0));
        var requestSnapshot = await ShiningTradeRequestState.ReadRequestsSnapshotAsync(fs);
        var requestState = requestSnapshot.State;
        if (requestState.IsMalformed)
            return new ShiningTradeAutoRefreshPlan(false, 0, tradeCycleId, requestSnapshot.Json, requestState.Requests);

        var requests = requestState.Requests.ToList();

        var requestsChanged = false;
        var createdCount = 0;
        for (var index = requests.Count - 1; index >= 0; index--)
        {
            var request = requests[index];
            if (!string.Equals(request.TradeCycleId, tradeCycleId, StringComparison.OrdinalIgnoreCase))
            {
                requests.RemoveAt(index);
                requestsChanged = true;
                continue;
            }

            var faction = ShiningAbodeState.FindFaction(shiningRoot, request.FactionId);
            if (faction == null ||
                !ShiningAbodeState.FactionHasAvailableTrade(faction) ||
                ShiningTradeRequestState.FindMatchingReceipt(faction, request) != null)
            {
                requests.RemoveAt(index);
                requestsChanged = true;
            }
        }

        foreach (var faction in ShiningAbodeState.EnsureFactionsArray(shiningRoot).OfType<JsonObject>())
        {
            if (!ShiningAbodeState.FactionHasAvailableTrade(faction))
                continue;

            var factionId = GetNodeString(faction["factionId"]) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(factionId))
                continue;

            var strength = GetNodeInt(faction["factionStrength"], 0);
            var request = new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
            {
                FactionId = factionId,
                FactionName = GetNodeString(faction["charter"]?["factionName"]) ?? factionId,
                TradeCycleId = tradeCycleId,
                DerivedTradeTier = ShiningAbodeState.GetTradeTier(strength),
                DerivedTradeSlotCount = ShiningAbodeState.GetTradeStockItemCount(faction, residentRoot),
                DerivedRarityCeiling = ShiningAbodeState.GetTradeRarityCeiling(strength),
                DerivedServiceMultiplier = ShiningAbodeState.GetServiceMultiplier(strength),
                CreatedAtTurn = currentTurn
            };

            if (ShiningTradeRequestState.HasReadyInventoryForCurrentContract(faction, request))
                continue;
            if (requests.Any(existing =>
                    string.Equals(existing.FactionId, factionId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(existing.TradeCycleId, tradeCycleId, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            requests.Add(request);
            requestsChanged = true;
            createdCount += 1;
        }

        if (!requestsChanged)
            return new ShiningTradeAutoRefreshPlan(false, 0, tradeCycleId, requestSnapshot.Json, requests);

        return new ShiningTradeAutoRefreshPlan(true, createdCount, tradeCycleId, requestSnapshot.Json, requests);
    }

    public static Task<ShiningTradeOperationResult> BuyAsync(
        FileSystemManager fs,
        string factionId,
        string slotId,
        int currentTurn) =>
        BuyAsync(fs, factionId, slotId, currentTurn, new LocalInteractionScopeService(fs));

    internal static async Task<ShiningTradeOperationResult> BuyAsync(
        FileSystemManager fs,
        string factionId,
        string slotId,
        int currentTurn,
        ILocalInteractionScopeResolver localScopeResolver)
    {
        if (currentTurn <= 0)
            return new ShiningTradeOperationResult(false, false, "Локальная покупка из сияющей витрины требует актуальный номер хода.");

        if (!await IsFactionAvailableInCurrentHallAsync(fs, factionId, localScopeResolver))
            return new ShiningTradeOperationResult(false, false, "Эта сияющая фракция недоступна в текущем мире или зале.");

        var activeTurnBlocker = AfterlifeLocalActionGuard.TryDescribeActiveGmTurnLifecycleBlocker(
            fs,
            "Локальная покупка сияющей витрины",
            "soul_state.json.inkFeathers, soulRelics и shining_abode_state.json.tradeInventory");
        if (activeTurnBlocker != null)
            return new ShiningTradeOperationResult(false, false, activeTurnBlocker);

        var pendingCostBlocker = await TryDescribePendingCostContractBlockerAsync(fs);
        if (pendingCostBlocker != null)
            return new ShiningTradeOperationResult(false, false, pendingCostBlocker);

        var readinessProbeRoot = await ReadJsonObjectAsync(fs, ShiningAbodeState.StatePath);
        var readinessProbeResidents = await ReadJsonObjectAsync(fs, GuardianAbodeResidentState.StatePath);
        var readinessProbeGuardians = await ReadJsonObjectAsync(fs, "game_state/meta/guardians.json");
        if (readinessProbeRoot != null)
        {
            ShiningAbodeState.NormalizeStateRoot(readinessProbeRoot, readinessProbeResidents, readinessProbeGuardians);
            var readinessProbeFaction = ShiningAbodeState.FindFaction(readinessProbeRoot, factionId);
            if (readinessProbeFaction?["tradeInventory"] is JsonObject probeTradeInventory &&
                probeTradeInventory["items"] is JsonArray probeItems)
            {
                var seenSlotIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in probeItems.OfType<JsonObject>())
                {
                    var existingSlotId = GetNodeString(item["slotId"]);
                    if (!string.IsNullOrWhiteSpace(existingSlotId) && !seenSlotIds.Add(existingSlotId))
                        return new ShiningTradeOperationResult(false, false, "Витрина сияющей фракции повреждена: duplicate slotId делает покупку неоднозначной.");
                }
            }
        }

        var view = await ReadTradeViewAsync(fs, factionId);
        if (view == null)
            return new ShiningTradeOperationResult(false, false, "Не удалось подготовить витрину сияющей фракции.");
        if (view.TradeBlocked)
            return new ShiningTradeOperationResult(false, false, view.BlockReason ?? "Торговля недоступна.");
        if (!view.InventoryReady)
            return new ShiningTradeOperationResult(false, false, view.InventoryStatusMessage ?? "Витрина сияющей фракции ещё не подготовлена.");
        var pendingRequestState = await ShiningTradeRequestState.ReadRequestsStateAsync(fs);
        if (pendingRequestState.IsMalformed)
            return new ShiningTradeOperationResult(false, false, "Ожидающий торговый запрос сияющей фракции повреждён. Покупка заблокирована до repair/cleanup pending_shining_trade_inventory_requests.json.");

        var shiningRoot = await ReadJsonObjectAsync(fs, ShiningAbodeState.StatePath);
        var residentRoot = await ReadJsonObjectAsync(fs, GuardianAbodeResidentState.StatePath);
        var guardiansRoot = await ReadJsonObjectAsync(fs, "game_state/meta/guardians.json");
        var soulRoot = await ReadJsonObjectAsync(fs, "game_state/meta/soul_state.json");
        if (shiningRoot == null || soulRoot == null)
            return new ShiningTradeOperationResult(false, false, "Не удалось прочитать состояние торговли или души.");
        var preBuyShiningJson = shiningRoot.ToJsonString(JsonOpts);
        var preBuySoulJson = soulRoot.ToJsonString(JsonOpts);

        var rawOwnerStateError = ShiningAbodeState.ValidateRawOwnerStateForActionableMode(shiningRoot);
        if (rawOwnerStateError != null)
            return new ShiningTradeOperationResult(false, false, rawOwnerStateError);

        ShiningAbodeState.NormalizeStateRoot(shiningRoot, residentRoot, guardiansRoot);
        var faction = ShiningAbodeState.FindFaction(shiningRoot, factionId);
        if (faction == null)
            return new ShiningTradeOperationResult(false, false, "Фракция не найдена.");

        if (faction["tradeInventory"] is JsonObject earlyTradeInventory &&
            earlyTradeInventory["items"] is JsonArray earlyItems)
        {
            var seenSlotIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in earlyItems.OfType<JsonObject>())
            {
                var existingSlotId = GetNodeString(item["slotId"]);
                if (!string.IsNullOrWhiteSpace(existingSlotId) && !seenSlotIds.Add(existingSlotId))
                    return new ShiningTradeOperationResult(false, false, "Витрина сияющей фракции повреждена: duplicate slotId делает покупку неоднозначной.");
            }
        }

        if (faction["tradeInventory"] is not JsonObject tradeInventory || tradeInventory["items"] is not JsonArray items)
            return new ShiningTradeOperationResult(false, false, "Витрина сияющей фракции недоступна.");

        var matchingSlots = items.OfType<JsonObject>()
            .Where(item => string.Equals(GetNodeString(item["slotId"]), slotId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matchingSlots.Count == 0)
            return new ShiningTradeOperationResult(false, false, "Выбранный товар не найден.");
        if (matchingSlots.Count > 1)
            return new ShiningTradeOperationResult(false, false, "Витрина сияющей фракции повреждена: duplicate slotId делает покупку неоднозначной.");

        var slot = matchingSlots[0];
        if (GetNodeBool(slot["soldOut"]))
            return new ShiningTradeOperationResult(false, false, "Этот товар уже распродан в текущей витрине.");

        var price = GetNodeInt(slot["priceInFeathers"], 0);
        if (price <= 0)
            return new ShiningTradeOperationResult(false, false, "Цена товара повреждена.");
        if (slot["relicData"] is not JsonObject relicData)
            return new ShiningTradeOperationResult(false, false, "Данные реликвии повреждены.");
        var tradeRelicId = GetNodeString(relicData["relicId"]) ?? GetNodeString(relicData["id"]);
        if (string.IsNullOrWhiteSpace(tradeRelicId))
            return new ShiningTradeOperationResult(false, false, "Данные реликвии повреждены: отсутствует relicId.");

        var commitScope = await ResolveFactionAvailableInCurrentHallScopeAsync(
            fs,
            factionId,
            localScopeResolver,
            preBuyShiningJson);
        if (commitScope == null)
            return new ShiningTradeOperationResult(false, false, "Фракция покинула текущий зал до завершения покупки. Перья не списаны.");

        NormalizeInkFeathersShape(soulRoot);
        GuardianPolicyContracts.EnsureStrictCanonicalSoulStateRootsForPolicySensitiveWrite(soulRoot);
        NormalizeSoulRelicsShape(soulRoot);
        if (SoulRelicIdExists(soulRoot, tradeRelicId))
            return new ShiningTradeOperationResult(false, false, "Покупка заблокирована: Soul Relic с таким relicId уже есть у души.");

        if (!TryModifyInkFeathers(soulRoot, -price))
            return new ShiningTradeOperationResult(false, false, "Недостаточно Чернильных Перьев.");

        var stored = ((JsonObject)soulRoot["soulRelics"]!)["stored"]!.AsArray();
        stored.Add(relicData.DeepClone().AsObject());
        slot["soldOut"] = true;
        UpdateLatestReadyReceiptSoldOutCount(faction, GetNodeString(tradeInventory["tradeCycleId"]));

        var postBuySoulJson = GuardianPolicyContracts.CreatePatchedSoulStateWriteRoot(
            soulRoot,
            new GuardianPolicyContracts.SoulStatePatchConflictContext(
                GuardianPolicyContracts.SoulStatePatchTouchedDomains.InkFeathers |
                GuardianPolicyContracts.SoulStatePatchTouchedDomains.SoulRelics,
                upsertedSoulRelicIds: new[] { tradeRelicId })).ToJsonString(JsonOpts);
        var postBuyShiningJson = shiningRoot.ToJsonString(JsonOpts);

        if (!await TryCommitCoordinatedStateWritesAsync(
                fs,
                commitScope,
                new CoordinatedStateWrite(ShiningAbodeState.StatePath, preBuyShiningJson, postBuyShiningJson),
                new CoordinatedStateWrite("game_state/meta/soul_state.json", preBuySoulJson, postBuySoulJson)))
        {
            return new ShiningTradeOperationResult(
                false,
                false,
                "Не удалось зафиксировать покупку сияющей реликвии без расхождения между состоянием души и Обители. Состояние души и витрины откатилось к исходной версии.");
        }

        var relicName = GetNodeString(relicData["name"]) ?? "Реликвия";
        return new ShiningTradeOperationResult(true, true, $"Куплена сияющая реликвия «{relicName}» за {price} 🪶.");
    }

    public static Task<bool> IsFactionAvailableInCurrentHallAsync(FileSystemManager fs, string factionId) =>
        IsFactionAvailableInCurrentHallAsync(fs, factionId, new LocalInteractionScopeService(fs));

    internal static async Task<bool> IsFactionAvailableInCurrentHallAsync(
        FileSystemManager fs,
        string factionId,
        ILocalInteractionScopeResolver localScopeResolver,
        string? expectedShiningRootJson = null) =>
        await ResolveFactionAvailableInCurrentHallScopeAsync(
            fs,
            factionId,
            localScopeResolver,
            expectedShiningRootJson) != null;

    private static async Task<LocalInteractionScope?> ResolveFactionAvailableInCurrentHallScopeAsync(
        FileSystemManager fs,
        string factionId,
        ILocalInteractionScopeResolver localScopeResolver,
        string? expectedShiningRootJson = null)
    {
        if (string.IsNullOrWhiteSpace(factionId))
            return null;

        var localScope = await localScopeResolver.ResolveAsync();
        var root = await ReadJsonObjectAsync(fs, ShiningAbodeState.StatePath);
        if (root == null)
            return null;
        if (expectedShiningRootJson != null &&
            !string.Equals(root.ToJsonString(JsonOpts), expectedShiningRootJson, StringComparison.Ordinal))
        {
            return null;
        }

        var faction = ShiningAbodeState.FindFaction(root, factionId);
        if (faction == null || SarefMainStoryState.IsHiddenWingsFaction(faction))
            return null;

        return LocalInteractionScopeService.IsShiningFactionLocal(localScope, faction)
            ? localScope
            : null;
    }

    private static async Task<string?> TryDescribePendingCostContractBlockerAsync(FileSystemManager fs)
    {
        var coreBlocker = await TryDescribePendingCoreActionCostBlockerAsync(fs);
        if (coreBlocker != null)
            return coreBlocker;

        var foundingMalformed = await ShiningFactionRequestState.IsRequestFileMalformedAsync(
            fs,
            ShiningFactionRequestState.PendingFoundingsRequestPath,
            static json => JsonSerializer.Deserialize<ShiningFactionRequestState.PendingShiningFactionFoundingRequest>(
                json,
                JsonOpts));
        if (foundingMalformed)
        {
            return "Локальная покупка сияющей витрины заблокирована: pending founding contract " +
                   $"{ShiningFactionRequestState.PendingFoundingsRequestPath} повреждён. Выполните repair/closure перед покупкой.";
        }

        var foundingRequest = (await ShiningFactionRequestState.ReadFoundingRequestsAsync(fs)).FirstOrDefault();
        if (foundingRequest != null)
        {
            return "Локальная покупка сияющей витрины заблокирована: pending founding request " +
                   $"{foundingRequest.RequestId} уже зафиксировал стоимость {foundingRequest.QuotedCostFeathers} 🪶 / " +
                   $"{foundingRequest.QuotedCostLightSparks} ✨. Дождитесь accepted/refused/withdrawn closure перед покупкой.";
        }

        return await TryDescribeLegacyDiscoveryCostBlockerAsync(fs);
    }

    private static async Task<string?> TryDescribePendingCoreActionCostBlockerAsync(FileSystemManager fs)
    {
        var pendingState = await ShiningCoreActionRequestState.ReadRequestsStateAsync(fs);
        if (pendingState.IsMalformed)
        {
            return "Локальная покупка сияющей витрины заблокирована: " +
                   $"{ShiningCoreActionRequestState.PendingActionsRequestPath} повреждён. Выполните repair/closure перед покупкой.";
        }

        var rawJson = await fs.ReadFileAsync(ShiningCoreActionRequestState.PendingActionsRequestPath);
        if (string.IsNullOrWhiteSpace(rawJson))
            return null;

        using var document = JsonDocument.Parse(rawJson);
        if (!document.RootElement.TryGetProperty(ShiningCoreActionRequestState.RequestsProperty, out var requestsNode) ||
            requestsNode.ValueKind != JsonValueKind.Array)
        {
            return "Локальная покупка сияющей витрины заблокирована: " +
                   $"{ShiningCoreActionRequestState.PendingActionsRequestPath} не содержит machine-readable requests[].";
        }

        foreach (var requestNode in requestsNode.EnumerateArray())
        {
            if (requestNode.ValueKind != JsonValueKind.Object)
            {
                return "Локальная покупка сияющей витрины заблокирована: " +
                       $"{ShiningCoreActionRequestState.PendingActionsRequestPath} содержит malformed request entry.";
            }

            var requestId = ReadJsonString(requestNode, "requestId") ?? "unknown";
            var actionType = ReadJsonString(requestNode, "actionType") ?? string.Empty;
            var hasFeathers = TryReadNonNegativeJsonInt(requestNode, "quotedCostFeathers", out var feathers);
            var hasLightSparks = TryReadNonNegativeJsonInt(requestNode, "quotedCostLightSparks", out var lightSparks);

            if ((hasFeathers && feathers > 0) || (hasLightSparks && lightSparks > 0))
            {
                return "Локальная покупка сияющей витрины заблокирована: pending Shining core action request " +
                       $"{requestId} уже зафиксировал стоимость {feathers} 🪶 / {lightSparks} ✨. Дождитесь closure перед покупкой.";
            }

            if ((!hasFeathers || !hasLightSparks) && IsPotentiallyCostBearingShiningCoreAction(actionType))
            {
                return "Локальная покупка сияющей витрины заблокирована: pending Shining core action request " +
                       $"{requestId} не имеет читаемых quotedCostFeathers/quotedCostLightSparks. Выполните repair/closure перед покупкой.";
            }
        }

        return null;
    }

    private static bool IsPotentiallyCostBearingShiningCoreAction(string? actionType)
    {
        return actionType?.Trim().ToLowerInvariant() is
            ShiningCoreActionRequestState.ActionTypeDiscoverNativeFaction or
            ShiningCoreActionRequestState.ActionTypeInvestInFaction or
            ShiningCoreActionRequestState.ActionTypeCompleteProject or
            ShiningCoreActionRequestState.ActionTypePullRelicGacha or
            ShiningCoreActionRequestState.ActionTypeForgeRelicReshape or
            ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty or
            ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand or
            ShiningCoreActionRequestState.ActionTypeForgeRelicStabilizeEcho or
            ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity;
    }

    private static string? ReadJsonString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static bool TryReadNonNegativeJsonInt(JsonElement root, string propertyName, out int value)
    {
        value = 0;
        return root.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt32(out value) &&
               value >= 0;
    }

    private static async Task<string?> TryDescribeLegacyDiscoveryCostBlockerAsync(FileSystemManager fs)
    {
        var shiningRoot = await ReadJsonObjectAsync(fs, ShiningAbodeState.StatePath);
        if (shiningRoot == null ||
            !shiningRoot.TryGetPropertyValue("pendingNativeFactionDiscovery", out var pendingDiscovery) ||
            pendingDiscovery == null)
        {
            return null;
        }

        if (pendingDiscovery is not JsonObject discovery)
        {
            return "Локальная покупка сияющей витрины заблокирована: " +
                   $"{ShiningAbodeState.StatePath}.pendingNativeFactionDiscovery повреждён. Выполните repair/closure перед покупкой.";
        }

        var requestId = GetNodeString(discovery["requestId"]) ?? "unknown";
        var costFeathers = GetNodeInt(discovery["costFeathers"], 0);
        var costLightSparks = GetNodeInt(discovery["costLightSparks"], 0);
        return "Локальная покупка сияющей витрины заблокирована: legacy pendingNativeFactionDiscovery " +
               $"{requestId} уже зафиксировал стоимость {costFeathers} 🪶 / {costLightSparks} ✨. Дождитесь closure перед покупкой.";
    }

    private static string? ResolveTradeBlockedReason(JsonObject? soulRoot, JsonObject shiningRoot, int tradeTier)
    {
        var currentRealm = GetNodeString(soulRoot?["currentRealm"]);
        if (!string.Equals(currentRealm, "Shining Abode", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(currentRealm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase))
        {
            return "Сияющая торговля доступна только при currentRealm = Shining Abode.";
        }

        if (!string.Equals(GetNodeString(shiningRoot["availability"]), ShiningAbodeState.AvailabilityActive, StringComparison.OrdinalIgnoreCase))
            return "Сияющая торговля доступна только когда Обитель активна.";
        var packageMode = ShiningAbodeState.GetPreparedIncarnationPackageMode(shiningRoot);
        if (packageMode == ShiningAbodeState.PreparedIncarnationPackageMode.ValidHandoff)
            return "Сияющая торговля недоступна, пока пакет новой жизни ждёт следующего воплощения.";
        if (packageMode == ShiningAbodeState.PreparedIncarnationPackageMode.InvalidFault)
            return "Сияющая торговля недоступна: preparedIncarnationPackage повреждён или не проходит bootstrap validation.";
        if (tradeTier <= 0)
            return "У этой фракции торговая витрина текущего цикла пока не открывается.";

        return null;
    }

    private static async Task<JsonObject?> ReadJsonObjectAsync(FileSystemManager fs, string path)
    {
        var json = await fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetNodeString(JsonNode? node)
    {
        if (node == null)
            return null;

        try
        {
            return node.GetValue<string>();
        }
        catch
        {
            return node.ToJsonString().Trim('"');
        }
    }

    private static int GetNodeInt(JsonNode? node, int fallback)
    {
        if (node == null)
            return fallback;

        try
        {
            return node.GetValue<int>();
        }
        catch
        {
            return fallback;
        }
    }

    private static bool GetNodeBool(JsonNode? node)
    {
        if (node == null)
            return false;

        try
        {
            return node.GetValue<bool>();
        }
        catch
        {
            return false;
        }
    }

    private static bool TryModifyInkFeathers(JsonObject soulRoot, int delta)
    {
        NormalizeInkFeathersShape(soulRoot);
        if (soulRoot["inkFeathers"] is not JsonObject inkObject)
            return false;

        var current = GetNodeInt(inkObject["current"], 0);
        var next = current + delta;
        if (next < 0)
            return false;

        inkObject["current"] = next;
        return true;
    }

    private static void NormalizeInkFeathersShape(JsonObject soulRoot)
    {
        if (soulRoot["inkFeathers"] is JsonObject)
            return;

        soulRoot["inkFeathers"] = new JsonObject
        {
            ["current"] = Math.Max(0, GetNodeInt(soulRoot["inkFeathers"], 0))
        };
    }

    private static void UpdateLatestReadyReceiptSoldOutCount(JsonObject faction, string? tradeCycleId)
    {
        if (string.IsNullOrWhiteSpace(tradeCycleId) ||
            faction[ShiningTradeRequestState.ReceiptsProperty] is not JsonArray receipts ||
            faction["tradeInventory"] is not JsonObject tradeInventory)
        {
            return;
        }

        var latestReceipt = receipts.OfType<JsonObject>()
            .Where(receipt =>
                string.Equals(GetNodeString(receipt["tradeCycleId"]), tradeCycleId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GetNodeString(receipt["status"]), ShiningTradeRequestState.ReceiptStatusReady, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(GetNodeString(receipt["resolvedAtUtc"])) &&
                GetNodeInt(receipt["resolvedAtTurn"], 0) > 0)
            .OrderByDescending(receipt => GetNodeInt(receipt["resolvedAtTurn"], 0))
            .ThenByDescending(receipt => GetNodeString(receipt["resolvedAtUtc"]) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (latestReceipt == null)
            return;

        latestReceipt["soldOutCount"] = ShiningTradeRequestState.GetTradeInventorySoldOutCount(tradeInventory);
    }

    private static Task<bool> TryCommitCoordinatedStateWritesAsync(
        FileSystemManager fs,
        LocalInteractionScope scope,
        params CoordinatedStateWrite[] writes)
        => CoordinatedStateWriteHelper.TryCommitAsync(
            fs,
            CoordinatedStateWriteHelper.CreateAuthorityGuardWrites(scope)
                .Concat(writes
                .Select(write => new CoordinatedStateWriteHelper.PlannedWrite(
                    write.Path,
                    write.PreWriteJson,
                    write.PostWriteJson,
                    true)))
                .ToArray());

    private static void NormalizeSoulRelicsShape(JsonObject root)
    {
        if (root["soulRelics"] is JsonArray flatRelics)
        {
            var equipped = new JsonArray();
            var stored = new JsonArray();
            foreach (var relic in flatRelics.OfType<JsonObject>())
            {
                var clone = relic.DeepClone().AsObject();
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
            return;
        }

        if (root["soulRelics"] is JsonObject soulRelics)
        {
            if (soulRelics["equipped"] is not JsonArray)
                soulRelics["equipped"] = new JsonArray();
            if (soulRelics["stored"] is not JsonArray)
                soulRelics["stored"] = new JsonArray();
            return;
        }

        root["soulRelics"] = new JsonObject
        {
            ["equipped"] = new JsonArray(),
            ["stored"] = new JsonArray()
        };
    }

    private static bool SoulRelicIdExists(JsonObject soulRoot, string relicId)
    {
        if (string.IsNullOrWhiteSpace(relicId))
            return false;

        if (soulRoot["soulRelics"] is not JsonObject soulRelics)
            return false;

        foreach (var collectionName in new[] { "equipped", "stored" })
        {
            if (soulRelics[collectionName] is not JsonArray collection)
                continue;

            foreach (var relic in collection.OfType<JsonObject>())
            {
                var existingId = GetNodeString(relic["relicId"]) ?? GetNodeString(relic["id"]);
                if (string.Equals(existingId, relicId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}
