using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class NpcTradeServiceRequestFlowTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public NpcTradeServiceRequestFlowTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-npc-trade-request-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task EnsureTradeInventoryAsync_MissingInventory_CreatesPendingRequestInsteadOfGeneratingStock()
    {
        await SeedBaseStateAsync(includeTradeInventory: false, includeTradeReceipt: false);

        var service = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance);
        var view = await service.EnsureTradeInventoryAsync("npc_merchant_001", currentTurn: 7);

        Assert.NotNull(view);
        Assert.False(view!.InventoryReady);
        Assert.True(view.InventoryRequestPending);
        Assert.True(view.InventoryRequestCreatedThisCall);
        Assert.NotNull(view.PendingGmAction);

        var pendingRaw = await _fs.ReadFileAsync(NpcTradeRequestState.PendingRequestPath);
        Assert.NotNull(pendingRaw);
        Assert.Contains("\"npcId\": \"npc_merchant_001\"", pendingRaw, StringComparison.Ordinal);
        Assert.Contains("\"tradeCycleId\": \"world_trade_0\"", pendingRaw, StringComparison.Ordinal);

        var npcRaw = await _fs.ReadFileAsync("game_state/npcs/npc_core.json");
        Assert.DoesNotContain("\"tradeInventory\":", npcRaw ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureTradeInventoryAsync_SameTurnInitialIdMerchant_CreatesPendingRequestForInitialId()
    {
        await SeedBaseStateAsync(includeTradeInventory: false, includeTradeReceipt: false, useSameTurnInitialId: true);

        var service = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance);
        var view = await service.EnsureTradeInventoryAsync("npc_merchant_initial_001", currentTurn: 7);

        Assert.NotNull(view);
        Assert.False(view!.InventoryReady);
        Assert.True(view.InventoryRequestPending);
        Assert.True(view.InventoryRequestCreatedThisCall);

        var pendingRaw = await _fs.ReadFileAsync(NpcTradeRequestState.PendingRequestPath);
        Assert.NotNull(pendingRaw);
        Assert.Contains("\"npcId\": \"npc_merchant_initial_001\"", pendingRaw, StringComparison.Ordinal);
        Assert.Contains("\"tradeCycleId\": \"world_trade_0\"", pendingRaw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureTradeInventoryAsync_MatchingInventoryAndReceipt_ReturnsReadyAndClearsPendingRequest()
    {
        await SeedBaseStateAsync(includeTradeInventory: true, includeTradeReceipt: true);
        await _fs.WriteFileAtomicAsync(
            NpcTradeRequestState.PendingRequestPath,
            """
            {
              "requests": [
                {
                  "requestId": "npc_trade_req_seed_001",
                  "npcId": "npc_merchant_001",
                  "npcName": "Марек",
                  "merchantProfile": "GeneralGoods",
                  "tradeCycleId": "world_trade_0",
                  "derivedTradeSlotCount": 7,
                  "createdAtTurn": 7,
                  "createdAtUtc": "2026-03-28T00:00:00Z",
                  "createdAtWorldDate": 100,
                  "refreshAfterWorldDate": 43200
                }
              ]
            }
            """);

        var service = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance);
        var view = await service.EnsureTradeInventoryAsync("npc_merchant_001", currentTurn: 7);

        Assert.NotNull(view);
        Assert.True(view!.InventoryReady);
        Assert.False(view.InventoryRequestCreatedThisCall);
        Assert.False(view.InventoryRequestPending);
        Assert.Equal(7, view.Offers.Count);

        var pendingRaw = await _fs.ReadFileAsync(NpcTradeRequestState.PendingRequestPath);
        Assert.True(string.IsNullOrWhiteSpace(pendingRaw));
    }

    [Fact]
    public async Task SellAsync_SellableItem_CreatesBuybackEntryAndAwardsMoney()
    {
        await SeedBaseStateAsync(includeTradeInventory: true, includeTradeReceipt: true, includeSellableInventoryItem: true);

        var service = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance);
        var result = await service.SellAsync("npc_merchant_001", "item_sell_lantern_001", currentTurn: 8);

        Assert.True(result.Success);
        Assert.True(result.StateChanged);

        using var inventoryDoc = JsonDocument.Parse(await _fs.ReadFileAsync("game_state/inventory/items.json") ?? "{}");
        Assert.False(inventoryDoc.RootElement.GetProperty("items").EnumerateArray().Any());

        using var statusDoc = JsonDocument.Parse(await _fs.ReadFileAsync("game_state/core/player_status.json") ?? "{}");
        Assert.True(statusDoc.RootElement.GetProperty("money").GetInt32() > 500);

        using var npcDoc = JsonDocument.Parse(await _fs.ReadFileAsync("game_state/npcs/npc_core.json") ?? "{}");
        var merchant = npcDoc.RootElement.GetProperty("UpdateNPCs")[0];
        var buybackEntry = merchant.GetProperty("buybackInventory")[0];
        Assert.Equal("npc_merchant_001", buybackEntry.GetProperty("npcId").GetString());
        Assert.Equal("item_sell_lantern_001", buybackEntry.GetProperty("itemId").GetString());
        Assert.Equal("available", buybackEntry.GetProperty("status").GetString());
        Assert.True(buybackEntry.GetProperty("buybackPrice").GetInt32() > 0);
    }

    [Fact]
    public async Task BuyBackAsync_AvailableEntry_ReturnsItemAndMarksEntryRebought()
    {
        await SeedBaseStateAsync(includeTradeInventory: false, includeTradeReceipt: false, includeBuybackInventory: true);

        var service = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance);
        var result = await service.BuyBackAsync("npc_merchant_001", "npc_buyback_001", currentTurn: 9);

        Assert.True(result.Success);
        Assert.True(result.StateChanged);

        using var inventoryDoc = JsonDocument.Parse(await _fs.ReadFileAsync("game_state/inventory/items.json") ?? "{}");
        Assert.Contains(inventoryDoc.RootElement.GetProperty("items").EnumerateArray(),
            item => string.Equals(item.GetProperty("itemId").GetString(), "item_sell_lantern_001", StringComparison.Ordinal));

        using var npcDoc = JsonDocument.Parse(await _fs.ReadFileAsync("game_state/npcs/npc_core.json") ?? "{}");
        var buybackEntry = npcDoc.RootElement.GetProperty("UpdateNPCs")[0].GetProperty("buybackInventory")[0];
        Assert.Equal("rebought", buybackEntry.GetProperty("status").GetString());
        Assert.Equal(9, buybackEntry.GetProperty("reboughtAtTurn").GetInt32());
    }

    [Fact]
    public async Task BuyAsync_WithoutValidCurrentTurn_FailsWithoutMutatingState()
    {
        await SeedBaseStateAsync(includeTradeInventory: true, includeTradeReceipt: true);

        var service = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance);
        var beforeInventory = await _fs.ReadFileAsync("game_state/inventory/items.json");
        var beforeStatus = await _fs.ReadFileAsync("game_state/core/player_status.json");
        var beforeNpc = await _fs.ReadFileAsync("game_state/npcs/npc_core.json");

        var result = await service.BuyAsync("npc_merchant_001", "npc_trade_slot_001", currentTurn: 0);

        Assert.False(result.Success);
        Assert.False(result.StateChanged);
        Assert.Contains("номер хода", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeInventory, await _fs.ReadFileAsync("game_state/inventory/items.json"));
        Assert.Equal(beforeStatus, await _fs.ReadFileAsync("game_state/core/player_status.json"));
        Assert.Equal(beforeNpc, await _fs.ReadFileAsync("game_state/npcs/npc_core.json"));
    }

    [Fact]
    public async Task BuyAsync_MinimalTradeItemData_WritesCanonicalInventoryItemShape()
    {
        await SeedBaseStateAsync(includeTradeInventory: true, includeTradeReceipt: true);

        var service = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance);
        var result = await service.BuyAsync("npc_merchant_001", "npc_trade_slot_001", currentTurn: 8);

        Assert.True(result.Success);
        Assert.True(result.StateChanged);

        using var inventoryDoc = JsonDocument.Parse(await _fs.ReadFileAsync("game_state/inventory/items.json") ?? "{}");
        var item = Assert.Single(inventoryDoc.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("npc_item_merchant_001", item.GetProperty("itemId").GetString());
        Assert.Equal("npc_item_merchant_001", item.GetProperty("id").GetString());
        Assert.Equal("npc_item_merchant_001", item.GetProperty("existedId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("image_prompt").GetString()));
        Assert.Equal("100%", item.GetProperty("durability").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("contentsPath").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("equipmentSlot").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("accessoryForSlot").ValueKind);
        Assert.False(item.GetProperty("isContainer").GetBoolean());
        Assert.False(item.GetProperty("isConsumption").GetBoolean());
        Assert.False(item.GetProperty("requiresTwoHands").GetBoolean());
        Assert.Equal(1, item.GetProperty("count").GetInt32());
        Assert.Equal(JsonValueKind.Number, item.GetProperty("weight").ValueKind);
        Assert.Equal(JsonValueKind.Number, item.GetProperty("volume").ValueKind);
    }

    [Fact]
    public async Task SellAsync_WithoutValidCurrentTurn_FailsWithoutMutatingState()
    {
        await SeedBaseStateAsync(includeTradeInventory: true, includeTradeReceipt: true, includeSellableInventoryItem: true);

        var service = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance);
        var beforeInventory = await _fs.ReadFileAsync("game_state/inventory/items.json");
        var beforeStatus = await _fs.ReadFileAsync("game_state/core/player_status.json");
        var beforeNpc = await _fs.ReadFileAsync("game_state/npcs/npc_core.json");

        var result = await service.SellAsync("npc_merchant_001", "item_sell_lantern_001", currentTurn: 0);

        Assert.False(result.Success);
        Assert.False(result.StateChanged);
        Assert.Contains("номер хода", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeInventory, await _fs.ReadFileAsync("game_state/inventory/items.json"));
        Assert.Equal(beforeStatus, await _fs.ReadFileAsync("game_state/core/player_status.json"));
        Assert.Equal(beforeNpc, await _fs.ReadFileAsync("game_state/npcs/npc_core.json"));
    }

    [Fact]
    public async Task BuyBackAsync_WithoutValidCurrentTurn_FailsWithoutMutatingState()
    {
        await SeedBaseStateAsync(includeTradeInventory: false, includeTradeReceipt: false, includeBuybackInventory: true);

        var service = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance);
        var beforeInventory = await _fs.ReadFileAsync("game_state/inventory/items.json");
        var beforeStatus = await _fs.ReadFileAsync("game_state/core/player_status.json");
        var beforeNpc = await _fs.ReadFileAsync("game_state/npcs/npc_core.json");

        var result = await service.BuyBackAsync("npc_merchant_001", "npc_buyback_001", currentTurn: 0);

        Assert.False(result.Success);
        Assert.False(result.StateChanged);
        Assert.Contains("номер хода", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeInventory, await _fs.ReadFileAsync("game_state/inventory/items.json"));
        Assert.Equal(beforeStatus, await _fs.ReadFileAsync("game_state/core/player_status.json"));
        Assert.Equal(beforeNpc, await _fs.ReadFileAsync("game_state/npcs/npc_core.json"));
    }

    [Fact]
    public async Task BuyAsync_StaleInventoryRequest_UsesRealCurrentTurnInPendingRequest()
    {
        await SeedBaseStateAsync(includeTradeInventory: false, includeTradeReceipt: false);

        var service = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance);
        var result = await service.BuyAsync("npc_merchant_001", "npc_trade_slot_001", currentTurn: 11);

        Assert.False(result.Success);
        Assert.False(result.StateChanged);

        var request = (await NpcTradeRequestState.ReadRequestsAsync(_fs)).Single();
        Assert.Equal(11, request.CreatedAtTurn);
        Assert.Equal("npc_merchant_001", request.NpcId);
        Assert.Equal("world_trade_0", request.TradeCycleId);
    }

    [Fact]
    public async Task EnsureTradeInventoryAsync_PendingStockStillReturnsAvailableBuybackOffers()
    {
        await SeedBaseStateAsync(includeTradeInventory: false, includeTradeReceipt: false, includeBuybackInventory: true);

        var service = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance);
        var view = await service.EnsureTradeInventoryAsync("npc_merchant_001", currentTurn: 7);

        Assert.NotNull(view);
        Assert.False(view!.InventoryReady);
        Assert.True(view.InventoryRequestPending);
        Assert.Single(view.BuybackOffers);
        Assert.Equal("npc_buyback_001", view.BuybackOffers[0].BuybackEntryId);
    }

    [Fact]
    public async Task EnsureHealthyAsync_UnresolvedRealm_PreservesPendingNpcTradeRequest()
    {
        await SeedBaseStateAsync(includeTradeInventory: false, includeTradeReceipt: false);
        await NpcTradeRequestState.WriteRequestAsync(_fs, new NpcTradeRequestState.PendingNpcTradeInventoryRequest
        {
            RequestId = "npc_trade_req_unresolved",
            NpcId = "npc_merchant_001",
            NpcName = "Марек",
            MerchantProfile = "GeneralGoods",
            TradeCycleId = "world_trade_0",
            DerivedTradeSlotCount = 7,
            CreatedAtTurn = 9,
            CreatedAtUtc = "2026-03-28T00:00:00Z",
            CreatedAtWorldDate = 100,
            RefreshAfterWorldDate = 43200
        });

        await NpcTradeRequestState.EnsureHealthyAsync(_fs, "");

        var requests = await NpcTradeRequestState.ReadRequestsAsync(_fs);
        var request = Assert.Single(requests);
        Assert.Equal("npc_trade_req_unresolved", request.RequestId);
        Assert.True(_fs.FileExists(NpcTradeRequestState.PendingRequestPath));
    }

    private async Task SeedBaseStateAsync(
        bool includeTradeInventory,
        bool includeTradeReceipt,
        bool includeSellableInventoryItem = false,
        bool includeBuybackInventory = false,
        bool useSameTurnInitialId = false)
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/core/player_status.json", """
        {
          "money": 500,
          "trade": 12
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/world_time.json", """
        {
          "currentTimeInMinutes": 100
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", """
        {
          "locationId": "loc_market_square",
          "name": "Рыночная площадь"
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", """
        {
          "items": [],
          "equipment": {}
        }
        """);

        if (includeSellableInventoryItem)
        {
            await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", """
            {
              "items": [
                {
                  "itemId": "item_sell_lantern_001",
                  "name": "Походный фонарь",
                  "quality": "Common",
                  "type": "tool",
                  "price": 20,
                  "baseSellPrice": 8
                }
              ],
              "equipment": {}
            }
            """);
        }

        var inventoryBlock = includeTradeInventory
            ? """
              ,
              "tradeInventory": {
                "tradeCycleId": "world_trade_0",
                "generatedAtWorldDate": 100,
                "refreshAfterWorldDate": 43200,
                "generationTradeTier": "Good",
                "pricingTradeTier": "Neutral",
                "items": [
                  {
                    "slotId": "npc_trade_slot_001",
                    "itemId": "npc_item_merchant_001",
                    "price": 110,
                    "merchantProfile": "GeneralGoods",
                    "soldOut": false,
                    "itemData": {
                      "itemId": "npc_item_merchant_001",
                      "name": "Полевой набор торговца",
                      "description": "Тестовый authored ассортимент.",
                      "type": "Tool",
                      "tradeItemClass": "Functional",
                      "quality": "Rare",
                      "price": 90,
                      "baseSellPrice": 36,
                      "weight": "1.0",
                      "group": "Инструменты"
                    }
                  },
                  {
                    "slotId": "npc_trade_slot_002",
                    "itemId": "npc_item_merchant_002",
                    "price": 37,
                    "merchantProfile": "GeneralGoods",
                    "soldOut": false,
                    "itemData": {
                      "itemId": "npc_item_merchant_002",
                      "name": "Карта соседних кварталов",
                      "description": "Тестовый authored ассортимент.",
                      "type": "Document",
                      "tradeItemClass": "FlavorOrUtility",
                      "quality": "Common",
                      "price": 30,
                      "baseSellPrice": 12,
                      "weight": "0.1",
                      "group": "Документы и медиа"
                    }
                  },
                  {
                    "slotId": "npc_trade_slot_003",
                    "itemId": "npc_item_merchant_003",
                    "price": 25,
                    "merchantProfile": "GeneralGoods",
                    "soldOut": false,
                    "itemData": {
                      "itemId": "npc_item_merchant_003",
                      "name": "Запас крепежа",
                      "description": "Тестовый authored ассортимент.",
                      "type": "Material",
                      "tradeItemClass": "Material",
                      "quality": "Common",
                      "price": 20,
                      "baseSellPrice": 8,
                      "weight": "0.4",
                      "group": "Материалы"
                    }
                  },
                  {
                    "slotId": "npc_trade_slot_004",
                    "itemId": "npc_item_merchant_004",
                    "price": 49,
                    "merchantProfile": "GeneralGoods",
                    "soldOut": false,
                    "itemData": {
                      "itemId": "npc_item_merchant_004",
                      "name": "Дорожный фонарь",
                      "description": "Тестовый authored ассортимент.",
                      "type": "Tool",
                      "tradeItemClass": "Functional",
                      "quality": "Uncommon",
                      "price": 40,
                      "baseSellPrice": 16,
                      "weight": "0.8",
                      "group": "Инструменты"
                    }
                  },
                  {
                    "slotId": "npc_trade_slot_005",
                    "itemId": "npc_item_merchant_005",
                    "price": 74,
                    "merchantProfile": "GeneralGoods",
                    "soldOut": false,
                    "itemData": {
                      "itemId": "npc_item_merchant_005",
                      "name": "Плотный плащ",
                      "description": "Тестовый authored ассортимент.",
                      "type": "Armor",
                      "tradeItemClass": "Functional",
                      "quality": "Uncommon",
                      "price": 60,
                      "baseSellPrice": 24,
                      "weight": "1.5",
                      "group": "Защита"
                    }
                  },
                  {
                    "slotId": "npc_trade_slot_006",
                    "itemId": "npc_item_merchant_006",
                    "price": 61,
                    "merchantProfile": "GeneralGoods",
                    "soldOut": false,
                    "itemData": {
                      "itemId": "npc_item_merchant_006",
                      "name": "Складной кофр",
                      "description": "Тестовый authored ассортимент.",
                      "type": "Container",
                      "tradeItemClass": "Functional",
                      "quality": "Uncommon",
                      "price": 50,
                      "baseSellPrice": 20,
                      "weight": "1.2",
                      "group": "Контейнеры"
                    }
                  },
                  {
                    "slotId": "npc_trade_slot_007",
                    "itemId": "npc_item_merchant_007",
                    "price": 31,
                    "merchantProfile": "GeneralGoods",
                    "soldOut": false,
                    "itemData": {
                      "itemId": "npc_item_merchant_007",
                      "name": "Записная книжка",
                      "description": "Тестовый authored ассортимент.",
                      "type": "Document",
                      "tradeItemClass": "FlavorOrUtility",
                      "quality": "Common",
                      "price": 25,
                      "baseSellPrice": 10,
                      "weight": "0.1",
                      "group": "Документы и медиа"
                    }
                  }
                ]
              }
            """
            : "";

        var receiptsBlock = includeTradeInventory && includeTradeReceipt
            ? """
              ,
              "tradeInventoryReceipts": [
                {
                  "requestId": "npc_trade_req_seed_001",
                  "npcId": "npc_merchant_001",
                  "npcName": "Марек",
                  "tradeCycleId": "world_trade_0",
                  "merchantProfile": "GeneralGoods",
                  "status": "ready",
                  "itemCount": 7,
                  "resolvedAtTurn": 7,
                  "resolvedAtUtc": "2026-03-28T00:05:00Z"
                }
              ]
            """
            : "";

        var buybackBlock = includeBuybackInventory
            ? """
              ,
              "buybackInventory": [
                {
                  "buybackEntryId": "npc_buyback_001",
                  "npcId": "npc_merchant_001",
                  "npcName": "Марек",
                  "itemId": "item_sell_lantern_001",
                  "itemData": {
                    "itemId": "item_sell_lantern_001",
                    "name": "Походный фонарь",
                    "description": "Ранее проданный фонарь.",
                    "type": "tool",
                    "tradeItemClass": "Functional",
                    "quality": "Common",
                    "price": 20,
                    "baseSellPrice": 8
                  },
                  "soldByPlayerAtTurn": 6,
                  "soldByPlayerAtUtc": "2026-03-28T00:04:00Z",
                  "soldAtWorldDate": 95,
                  "soldForPrice": 8,
                  "buybackPrice": 8,
                  "acquiredFromPlayer": true,
                  "sourceMerchantProfile": "GeneralGoods",
                  "status": "available"
                }
              ]
            """
            : "";

        var npcIdLiteral = useSameTurnInitialId ? "null" : "\"npc_merchant_001\"";

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", $$"""
        {
          "UpdateNPCs": [
            {
              "npcId": {{npcIdLiteral}},
              "initialId": "npc_merchant_initial_001",
              "name": "Марек",
              "currentLocationId": "loc_market_square",
              "currentLocation": "Рыночная площадь",
              "level": 10,
              "relationshipLevel": 80,
              "characteristics": { "modifiedTrade": 14 },
              "tradeState": {
                "canTrade": true,
                "merchantProfile": "GeneralGoods"
              }{{inventoryBlock}}{{receiptsBlock}}{{buybackBlock}}
            }
          ]
        }
        """);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // Ignore temp cleanup failures.
        }
    }
}
