using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
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
    public async Task GetCurrentLocationTradeTargetsAsync_ScopeChangesBeforeReturn_HidesMerchant()
    {
        await SeedBaseStateAsync(includeTradeInventory: true, includeTradeReceipt: true);
        var initialScope = await new LocalInteractionScopeService(_fs).ResolveAsync();
        var resolver = new SequenceLocalInteractionScopeResolver(
            initialScope,
            LocalInteractionScope.Unresolved(LocalInteractionRealmKind.Mortal, "Локация изменилась."));
        var service = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance, resolver);

        var targets = await service.GetCurrentLocationTradeTargetsAsync();

        Assert.Empty(targets);
        Assert.True(resolver.ResolveCallCount >= 2);
    }

    [Fact]
    public async Task EnsureTradeInventoryAsync_ScopeChangesBeforeReturn_HidesMerchantDetails()
    {
        await SeedBaseStateAsync(includeTradeInventory: true, includeTradeReceipt: true);
        var initialScope = await new LocalInteractionScopeService(_fs).ResolveAsync();
        var resolver = new SequenceLocalInteractionScopeResolver(
            initialScope,
            LocalInteractionScope.Unresolved(LocalInteractionRealmKind.Mortal, "Локация изменилась."));
        var service = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance, resolver);

        var view = await service.EnsureTradeInventoryAsync(
            "npc_merchant_001",
            currentTurn: 7,
            createPendingRequests: false);

        Assert.Null(view);
        Assert.True(resolver.ResolveCallCount >= 2);
    }

    [Fact]
    public async Task EnsureTradeInventoryAsync_MerchantChangesBeforeRepriceCommit_PreservesConcurrentUpdate()
    {
        await SeedBaseStateAsync(includeTradeInventory: true, includeTradeReceipt: true);
        var root = JsonNode.Parse((await _fs.ReadFileAsync("game_state/npcs/npc_core.json"))!)!.AsObject();
        root["UpdateNPCs"]!.AsArray()[0]!.AsObject()["relationshipLevel"] = 150;
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", root.ToJsonString());
        var initialScope = await new LocalInteractionScopeService(_fs).ResolveAsync();
        var resolver = new SequenceLocalInteractionScopeResolver(
            async callCount =>
            {
                if (callCount != 2)
                    return;

                var latest = JsonNode.Parse((await _fs.ReadFileAsync("game_state/npcs/npc_core.json"))!)!.AsObject();
                latest["UpdateNPCs"]!.AsArray()[0]!.AsObject()["concurrentGmMarker"] = "preserve";
                await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", latest.ToJsonString());
            },
            initialScope,
            initialScope,
            initialScope);
        var service = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance, resolver);

        var view = await service.EnsureTradeInventoryAsync(
            "npc_merchant_001",
            currentTurn: 7,
            createPendingRequests: false);

        Assert.Null(view);
        Assert.Contains(
            "concurrentGmMarker",
            await _fs.ReadFileAsync("game_state/npcs/npc_core.json") ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSellableItemsAsync_ScopeChangesBeforeReturn_HidesMerchantOffers()
    {
        await SeedBaseStateAsync(
            includeTradeInventory: true,
            includeTradeReceipt: true,
            includeSellableInventoryItem: true);
        var initialScope = await new LocalInteractionScopeService(_fs).ResolveAsync();
        var resolver = new SequenceLocalInteractionScopeResolver(
            initialScope,
            LocalInteractionScope.Unresolved(LocalInteractionRealmKind.Mortal, "Локация изменилась."));
        var service = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance, resolver);

        var offers = await service.GetSellableItemsAsync("npc_merchant_001");

        Assert.Empty(offers);
        Assert.True(resolver.ResolveCallCount >= 2);
    }

    [Fact]
    public async Task GetSellableItemsAsync_HidesRejectedItemsAndExactEquippedCanonicalItem()
    {
        await SeedBaseStateAsync(
            includeTradeInventory: true,
            includeTradeReceipt: true,
            includeSellableInventoryItem: true);
        var inventory = JsonNode.Parse(
            (await _fs.ReadFileAsync("game_state/inventory/items.json"))!)!.AsObject();
        inventory["items"]!.AsArray().Add(new JsonObject
        {
            ["itemId"] = "item_rejected_trade_001",
            ["existedId"] = "item_rejected_trade_001",
            ["name"] = "Непринятый предмет продажи",
            ["quality"] = "Common",
            ["type"] = "tool",
            ["price"] = 20,
            ["baseSellPrice"] = 8
        });
        inventory["equippedItems"] = new JsonObject
        {
            ["MainHand"] = "item_sell_lantern_001"
        };
        await _fs.WriteFileAtomicAsync(
            "game_state/inventory/items.json",
            inventory.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var service = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance);

        var offers = await service.GetSellableItemsAsync("npc_merchant_001");

        Assert.Empty(offers);
    }

    [Fact]
    public async Task SellAsync_CaseVariantEquipmentReferenceFailsClosedWithoutMutation()
    {
        await SeedBaseStateAsync(
            includeTradeInventory: true,
            includeTradeReceipt: true,
            includeSellableInventoryItem: true);
        var inventory = JsonNode.Parse(
            (await _fs.ReadFileAsync("game_state/inventory/items.json"))!)!.AsObject();
        inventory["equippedItems"] = new JsonObject
        {
            ["MainHand"] = "ITEM_SELL_LANTERN_001"
        };
        await _fs.WriteFileAtomicAsync(
            "game_state/inventory/items.json",
            inventory.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var service = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance);
        var beforeInventory = await _fs.ReadFileAsync("game_state/inventory/items.json");
        var beforeNpc = await _fs.ReadFileAsync("game_state/npcs/npc_core.json");
        var beforeStatus = await _fs.ReadFileAsync("game_state/core/player_status.json");
        var beforeIndex = await _fs.ReadFileAsync(MortalItemIdentityState.StatePath);

        var offers = await service.GetSellableItemsAsync("npc_merchant_001");
        var result = await service.SellAsync(
            "npc_merchant_001",
            "item_sell_lantern_001",
            currentTurn: 8);

        Assert.Empty(offers);
        Assert.False(result.Success);
        Assert.False(result.StateChanged);
        Assert.Contains("состояни", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("equippedItems", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("itemId", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ITEM_SELL_LANTERN_001", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("receipt", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeInventory, await _fs.ReadFileAsync("game_state/inventory/items.json"));
        Assert.Equal(beforeNpc, await _fs.ReadFileAsync("game_state/npcs/npc_core.json"));
        Assert.Equal(beforeStatus, await _fs.ReadFileAsync("game_state/core/player_status.json"));
        Assert.Equal(beforeIndex, await _fs.ReadFileAsync(MortalItemIdentityState.StatePath));
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
    public async Task EnsureTradeInventoryAsync_MortalAuthorityChangesBeforeRequestCommit_DoesNotCreatePending()
    {
        await SeedBaseStateAsync(includeTradeInventory: false, includeTradeReceipt: false);
        var initialScope = await new LocalInteractionScopeService(_fs).ResolveAsync();
        var resolver = new SequenceLocalInteractionScopeResolver(
            async callCount =>
            {
                if (callCount != 2)
                    return;

                await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", """
                {
                  "locationId": "loc_remote",
                  "name": "Дальний тракт"
                }
                """);
            },
            initialScope,
            initialScope,
            initialScope);
        var service = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance, resolver);

        var view = await service.EnsureTradeInventoryAsync("npc_merchant_001", currentTurn: 7);

        Assert.Null(view);
        Assert.False(_fs.FileExists(NpcTradeRequestState.PendingRequestPath));
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

        Assert.True(result.Success, result.Message);
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

        Assert.True(result.Success, result.Message);
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
    public async Task SellBuybackSell_SameTurnUsesDistinctLocalCommandAuthorities()
    {
        await SeedBaseStateAsync(
            includeTradeInventory: true,
            includeTradeReceipt: true,
            includeSellableInventoryItem: true);
        var service = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance);

        var firstSell = await service.SellAsync(
            "npc_merchant_001",
            "item_sell_lantern_001",
            currentTurn: 8);
        Assert.True(firstSell.Success, firstSell.Message);
        var firstNpcRoot = JsonNode.Parse(
            (await _fs.ReadFileAsync(NpcCoreChangesContract.NpcCorePath))!)!.AsObject();
        var firstEntry = Assert.Single(
            firstNpcRoot["UpdateNPCs"]![0]!["buybackInventory"]!
                .AsArray()
                .OfType<JsonObject>());
        var firstEntryId = firstEntry["buybackEntryId"]!.GetValue<string>();

        var buyback = await service.BuyBackAsync(
            "npc_merchant_001",
            firstEntryId,
            currentTurn: 8);
        var secondSell = await service.SellAsync(
            "npc_merchant_001",
            "item_sell_lantern_001",
            currentTurn: 8);

        Assert.True(buyback.Success, buyback.Message);
        Assert.True(secondSell.Success, secondSell.Message);
        var index = MortalItemIdentityState.Parse(
            await _fs.ReadFileAsync(MortalItemIdentityState.StatePath));
        var identity = index.EntriesByItemId["item_sell_lantern_001"];
        var sellAuthorities = identity["transitions"]!.AsArray()
            .OfType<JsonObject>()
            .Where(transition =>
                transition["authorityKind"]?.GetValue<string>() == "npc_trade_sell")
            .Select(transition => transition["authorityId"]!.GetValue<string>())
            .ToArray();
        Assert.Equal(2, sellAuthorities.Length);
        Assert.Equal(2, sellAuthorities.Distinct(StringComparer.Ordinal).Count());
        var npcRoot = JsonNode.Parse(
            (await _fs.ReadFileAsync(NpcCoreChangesContract.NpcCorePath))!)!.AsObject();
        var buybackEntries = npcRoot["UpdateNPCs"]![0]!["buybackInventory"]!
            .AsArray()
            .OfType<JsonObject>()
            .ToArray();
        Assert.Equal(2, buybackEntries.Length);
        Assert.Single(
            buybackEntries,
            entry => entry["status"]?.GetValue<string>() == "available");
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
    public async Task BuyAsync_TemplateOnlyTradeOutput_SealsIndependentCanonicalItem()
    {
        await SeedBaseStateAsync(
            includeTradeInventory: true,
            includeTradeReceipt: true,
            materializeTradeStock: false);

        var service = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance);
        var result = await service.BuyAsync("npc_merchant_001", "npc_trade_slot_001", currentTurn: 8);

        Assert.True(result.Success, result.Message);
        Assert.True(result.StateChanged);

        using var inventoryDoc = JsonDocument.Parse(await _fs.ReadFileAsync("game_state/inventory/items.json") ?? "{}");
        var item = Assert.Single(inventoryDoc.RootElement.GetProperty("items").EnumerateArray());
        Assert.StartsWith("itm_", item.GetProperty("itemId").GetString(), StringComparison.Ordinal);
        Assert.NotEqual("npc_item_merchant_001", item.GetProperty("itemId").GetString());
        Assert.Equal(item.GetProperty("itemId").GetString(), item.GetProperty("existedId").GetString());
        Assert.False(item.TryGetProperty("id", out _));
        Assert.Equal(JsonValueKind.Object, item.GetProperty("materialization").ValueKind);
        Assert.Equal(JsonValueKind.Object, item.GetProperty("materializationReceipt").ValueKind);
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
    public async Task BuyAsync_TypeAndGroupProseDoNotCreateContainerOrConsumptionAuthority()
    {
        await SeedBaseStateAsync(includeTradeInventory: true, includeTradeReceipt: true);

        var service = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance);
        var result = await service.BuyAsync("npc_merchant_001", "npc_trade_slot_006", currentTurn: 8);

        Assert.True(result.Success, result.Message);
        using var inventoryDoc = JsonDocument.Parse(
            await _fs.ReadFileAsync("game_state/inventory/items.json") ?? "{}");
        var item = Assert.Single(inventoryDoc.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("Container", item.GetProperty("type").GetString());
        Assert.Equal("Контейнеры", item.GetProperty("group").GetString());
        Assert.False(item.GetProperty("isContainer").GetBoolean());
        Assert.False(item.GetProperty("isConsumption").GetBoolean());
    }

    [Fact]
    public async Task GetSellableItemsAsync_UsesCanonicalRelicAndQuestLinksWithoutProseOrLegacyFlagInference()
    {
        await SeedBaseStateAsync(
            includeTradeInventory: true,
            includeTradeReceipt: true);
        var items = new[]
        {
            CreateCanonicalSellableItem("sr_ordinary_tool", "Обычный резец", item => item["type"] = "tool"),
            CreateCanonicalSellableItem("item_type_prose", "Сувенир", item => item["type"] = "soul relic replica"),
            CreateCanonicalSellableItem("item_group_prose", "Театральный реквизит", item => item["group"] = "Реликвия души — декорации"),
            CreateCanonicalSellableItem("item_legacy_field", "Архивный муляж", item => item["soulRelicId"] = "legacy_non_authority"),
            CreateCanonicalSellableItem("item_quest_group_prose", "Театральный реквизит задания", item => item["group"] = "Quest"),
            CreateCanonicalSellableItem("item_explicit_non_quest", "Обычная памятка", item =>
            {
                item["group"] = "Quest";
                item["isQuestItem"] = false;
            }),
            CreateCanonicalSellableItem("item_canonical_relic", "Настоящая реликвия", item =>
            {
                item["quality"] = "Rare";
                item["rarity"] = "Rare";
                item["relicId"] = "relic_authority_001";
                item["price"] = 200;
                item["baseSellPrice"] = 80;
            }),
            CreateCanonicalSellableItem("item_legacy_quest_flag", "Архивный флаг задания", item => item["isQuestItem"] = true),
            CreateCanonicalSellableItem("item_canonical_quest", "Подлинный предмет задания", item =>
            {
                item["questLinks"] = new JsonArray(
                    new JsonObject
                    {
                        ["questId"] = "quest_trade_guard",
                        ["role"] = "required"
                    });
                item["materialization"]!["sections"]!["questRole"] = new JsonObject
                {
                    ["state"] = "populated",
                    ["reason"] = null
                };
            })
        };
        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(items.Select(item => (JsonNode?)item.DeepClone()).ToArray()),
                ["equippedItems"] = new JsonObject()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync(
            MortalItemIdentityState.StatePath,
            MortalItemTestFixture.CreateIndex(items)
                .ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var service = new NpcTradeService(
            _fs,
            NullLogger<NpcTradeService>.Instance);

        var offers = await service.GetSellableItemsAsync(
            "npc_merchant_001");

        Assert.Equal(
            [
                "item_explicit_non_quest",
                "item_group_prose",
                "item_legacy_field",
                "item_legacy_quest_flag",
                "item_quest_group_prose",
                "item_type_prose",
                "sr_ordinary_tool"
            ],
            offers
                .Select(offer => offer.ItemId)
                .OrderBy(itemId => itemId, StringComparer.Ordinal)
                .ToArray());
        Assert.DoesNotContain(
            offers,
            offer => offer.ItemId == "item_canonical_relic");
        Assert.DoesNotContain(
            offers,
            offer => offer.ItemId == "item_canonical_quest");
    }

    [Fact]
    public async Task SellAsync_BlocksCanonicalQuestLinkedAndExplicitlyNonCarriedItemsWithoutMutation()
    {
        await SeedBaseStateAsync(includeTradeInventory: true, includeTradeReceipt: true);
        var questLinked = CreateCanonicalSellableItem(
            "item_quest_linked_sale_guard",
            "Печать незавершённого задания",
            item =>
            {
                item["questLinks"] = new JsonArray(
                    new JsonObject
                    {
                        ["questId"] = "quest_sale_guard",
                        ["role"] = "required"
                    });
                item["materialization"]!["sections"]!["questRole"] = new JsonObject
                {
                    ["state"] = "populated",
                    ["reason"] = null
                };
            });
        var locationItem = CreateCanonicalSellableItem(
            "item_location_sale_guard",
            "Фонарь у ворот",
            item =>
            {
                item["isCarried"] = false;
                item["currentLocationName"] = "Северные ворота";
            });
        var items = new[] { questLinked, locationItem };
        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(items.Select(item => (JsonNode?)item.DeepClone()).ToArray()),
                ["equippedItems"] = new JsonObject()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync(
            MortalItemIdentityState.StatePath,
            MortalItemTestFixture.CreateIndex(items)
                .ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var service = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance);
        var beforeInventory = await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath);
        var beforeNpc = await _fs.ReadFileAsync(NpcCoreChangesContract.NpcCorePath);
        var beforeStatus = await _fs.ReadFileAsync("game_state/core/player_status.json");
        var beforeIndex = await _fs.ReadFileAsync(MortalItemIdentityState.StatePath);

        var offers = await service.GetSellableItemsAsync("npc_merchant_001");
        var questSale = await service.SellAsync(
            "npc_merchant_001",
            "item_quest_linked_sale_guard",
            currentTurn: 8);
        var locationSale = await service.SellAsync(
            "npc_merchant_001",
            "item_location_sale_guard",
            currentTurn: 8);

        Assert.Empty(offers);
        Assert.False(questSale.Success);
        Assert.False(questSale.StateChanged);
        Assert.False(locationSale.Success);
        Assert.False(locationSale.StateChanged);
        Assert.Equal(beforeInventory, await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath));
        Assert.Equal(beforeNpc, await _fs.ReadFileAsync(NpcCoreChangesContract.NpcCorePath));
        Assert.Equal(beforeStatus, await _fs.ReadFileAsync("game_state/core/player_status.json"));
        Assert.Equal(beforeIndex, await _fs.ReadFileAsync(MortalItemIdentityState.StatePath));
    }

    [Fact]
    public async Task BuyAsync_MortalScopeChangesBeforeCommit_BlocksWithoutMutation()
    {
        await SeedBaseStateAsync(includeTradeInventory: true, includeTradeReceipt: true);
        var initialScope = await new LocalInteractionScopeService(_fs).ResolveAsync();
        var resolver = new SequenceLocalInteractionScopeResolver(
            initialScope,
            LocalInteractionScope.Unresolved(LocalInteractionRealmKind.Mortal, "Локация изменилась."));
        var service = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance, resolver);
        var inventoryBefore = await _fs.ReadFileAsync("game_state/inventory/items.json");
        var statusBefore = await _fs.ReadFileAsync("game_state/core/player_status.json");
        var npcBefore = await _fs.ReadFileAsync("game_state/npcs/npc_core.json");

        var result = await service.BuyAsync("npc_merchant_001", "npc_trade_slot_001", currentTurn: 8);

        Assert.False(result.Success);
        Assert.False(result.StateChanged);
        Assert.True(resolver.ResolveCallCount >= 2);
        Assert.Equal(inventoryBefore, await _fs.ReadFileAsync("game_state/inventory/items.json"));
        Assert.Equal(statusBefore, await _fs.ReadFileAsync("game_state/core/player_status.json"));
        Assert.Equal(npcBefore, await _fs.ReadFileAsync("game_state/npcs/npc_core.json"));
    }

    [Fact]
    public async Task BuyAsync_SecondSettlementWriteFailureRestoresExactBeforeImages()
    {
        await SeedBaseStateAsync(includeTradeInventory: true, includeTradeReceipt: true);
        var inventoryBefore = await _fs.ReadFileAsync("game_state/inventory/items.json");
        var statusBefore = await _fs.ReadFileAsync("game_state/core/player_status.json");
        var npcBefore = await _fs.ReadFileAsync("game_state/npcs/npc_core.json");
        var indexBefore = await _fs.ReadFileAsync(MortalItemIdentityState.StatePath);
        var injected = false;
        var faultingFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = path =>
                {
                    if (!injected && string.Equals(
                            path,
                            "game_state/core/player_status.json",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        injected = true;
                        throw new IOException("injected trade settlement write failure");
                    }
                    return Task.CompletedTask;
                }
            });
        var service = new NpcTradeService(faultingFs, NullLogger<NpcTradeService>.Instance);

        var result = await service.BuyAsync("npc_merchant_001", "npc_trade_slot_001", currentTurn: 8);

        Assert.True(injected);
        Assert.False(result.Success);
        Assert.False(result.StateChanged);
        Assert.Equal(inventoryBefore, await _fs.ReadFileAsync("game_state/inventory/items.json"));
        Assert.Equal(statusBefore, await _fs.ReadFileAsync("game_state/core/player_status.json"));
        Assert.Equal(npcBefore, await _fs.ReadFileAsync("game_state/npcs/npc_core.json"));
        Assert.Equal(indexBefore, await _fs.ReadFileAsync(MortalItemIdentityState.StatePath));
    }

    [Fact]
    public async Task BuyAsync_AmbiguousAfterlifeRealm_BlocksWithoutMutation()
    {
        await SeedBaseStateAsync(includeTradeInventory: true, includeTradeReceipt: true);
        await _fs.WriteFileAtomicAsync(
            "game_state/meta/soul_state.json",
            """
            {
              "currentRealm": "afterlife",
              "currentIncarnation": 0
            }
            """);
        var service = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance);
        var inventoryBefore = await _fs.ReadFileAsync("game_state/inventory/items.json");
        var statusBefore = await _fs.ReadFileAsync("game_state/core/player_status.json");
        var npcBefore = await _fs.ReadFileAsync("game_state/npcs/npc_core.json");

        var result = await service.BuyAsync("npc_merchant_001", "npc_trade_slot_001", currentTurn: 8);

        Assert.False(result.Success);
        Assert.False(result.StateChanged);
        Assert.Equal(inventoryBefore, await _fs.ReadFileAsync("game_state/inventory/items.json"));
        Assert.Equal(statusBefore, await _fs.ReadFileAsync("game_state/core/player_status.json"));
        Assert.Equal(npcBefore, await _fs.ReadFileAsync("game_state/npcs/npc_core.json"));
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
        var npcRoot = JsonNode.Parse(
            await _fs.ReadFileAsync("game_state/npcs/npc_core.json") ?? "{}")!.AsObject();
        var buybackInventory = npcRoot["UpdateNPCs"]![0]!["buybackInventory"]!.AsArray();
        buybackInventory.Add(new JsonObject
        {
            ["buybackEntryId"] = "npc_buyback_orphan",
            ["itemId"] = "item_orphan_buyback",
            ["itemData"] = new JsonObject
            {
                ["itemId"] = "item_orphan_buyback",
                ["name"] = "НЕПРИНЯТЫЙ ORPHAN BUYBACK",
                ["quality"] = "Common"
            },
            ["buybackPrice"] = 8,
            ["soldForPrice"] = 8,
            ["status"] = "available"
        });
        buybackInventory.Add(new JsonObject
        {
            ["buybackEntryId"] = "npc_buyback_mismatch",
            ["itemId"] = "item_sell_lantern_001",
            ["itemData"] = new JsonObject
            {
                ["itemId"] = "item_other_buyback",
                ["name"] = "НЕСОВПАДАЮЩИЙ BUYBACK",
                ["quality"] = "Common"
            },
            ["buybackPrice"] = 8,
            ["soldForPrice"] = 8,
            ["status"] = "available"
        });
        buybackInventory.Add(new JsonObject
        {
            ["buybackEntryId"] = "npc_buyback_wrong_case",
            ["itemId"] = "ITEM_SELL_LANTERN_001",
            ["itemData"] = new JsonObject
            {
                ["itemId"] = "ITEM_SELL_LANTERN_001",
                ["name"] = "BUYBACK С НЕТОЧНЫМ ID",
                ["quality"] = "Common"
            },
            ["buybackPrice"] = 8,
            ["soldForPrice"] = 8,
            ["status"] = "available"
        });
        await _fs.WriteFileAtomicAsync(
            "game_state/npcs/npc_core.json",
            npcRoot.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var service = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance);
        var view = await service.EnsureTradeInventoryAsync("npc_merchant_001", currentTurn: 7);

        Assert.NotNull(view);
        Assert.False(view!.InventoryReady);
        Assert.True(view.InventoryRequestPending);
        Assert.Single(view.BuybackOffers);
        Assert.Equal("npc_buyback_001", view.BuybackOffers[0].BuybackEntryId);
        Assert.DoesNotContain(view.BuybackOffers, offer => offer.Name.Contains("BUYBACK", StringComparison.Ordinal));
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
        bool useSameTurnInitialId = false,
        bool materializeTradeStock = true)
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
          "equippedItems": {}
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
              "equippedItems": {}
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

        var npcIdentityFields = useSameTurnInitialId
            ? "\"npcId\": null,\n              \"initialId\": \"npc_merchant_initial_001\""
            : "\"npcId\": \"npc_merchant_001\"";

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", $$"""
        {
          "UpdateNPCs": [
            {
              {{npcIdentityFields}},
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

        var npcRoot = JsonNode.Parse(
            (await _fs.ReadFileAsync("game_state/npcs/npc_core.json"))!)!.AsObject();
        var npc = npcRoot["UpdateNPCs"]!.AsArray()[0]!.AsObject();
        var canonicalNpcId = useSameTurnInitialId
            ? "npc_merchant_initial_001"
            : "npc_merchant_001";
        if (includeTradeInventory && !materializeTradeStock)
        {
            var slot = npc["tradeInventory"]!["items"]!.AsArray()[0]!.AsObject();
            var offerData = slot["itemData"]!.AsObject();
            var template = MortalItemTestFixture.CreateRawRoot(
                route: "trade_output",
                authorityKind: "npc_trade_receipt",
                authorityId: "npc_trade_req_seed_001",
                sourceTurn: 7,
                creationRef: "npc_trade_slot_001",
                materializationId: "mat_trade_slot_001");
            foreach (var property in new[]
                     {
                         "name", "description", "type", "tradeItemClass", "quality", "price",
                         "baseSellPrice", "weight", "group"
                     })
            {
                template[property] = offerData[property]?.DeepClone();
            }
            template["rarity"] = offerData["quality"]!.DeepClone();
            template["weight"] = 1.0;
            template.Remove("existedId");
            template.Remove("creationRef");
            template["itemId"] = "npc_item_merchant_001";
            slot["itemData"] = template;
        }
        var stockItems = includeTradeInventory && materializeTradeStock
            ? npc["tradeInventory"]!["items"]!.AsArray()
                .OfType<JsonObject>()
                .Select(slot => MortalItemTestFixture.CreateCanonicalTradeStock(
                    slot,
                    canonicalNpcId,
                    acceptedAtTurn: 5))
                .ToArray()
            : Array.Empty<JsonObject>();
        var soldItem = includeSellableInventoryItem
            ? MortalItemTestFixture.CreateCanonicalRootAtTurn(
                "item_sell_lantern_001",
                acceptedAtTurn: 5,
                route: "player_acquisition",
                authorityKind: "turn_outcome",
                authorityId: "turn_5",
                name: "Походный фонарь",
                price: 20,
                baseSellPrice: 8)
            : null;
        var buybackItem = includeBuybackInventory && !includeSellableInventoryItem
            ? MortalItemTestFixture.CreateCanonicalRootAtTurn(
                "item_sell_lantern_001",
                acceptedAtTurn: 5,
                route: "player_acquisition",
                authorityKind: "turn_outcome",
                authorityId: "turn_5",
                name: "Походный фонарь",
                price: 20,
                baseSellPrice: 8)
            : null;

        npc["inventory"] = new JsonArray(
            stockItems.Cast<JsonObject?>().Append(buybackItem)
                .Where(static item => item != null)
                .Select(item => (JsonNode?)item!.DeepClone())
                .ToArray());
        await _fs.WriteFileAtomicAsync(
            "game_state/npcs/npc_core.json",
            npcRoot.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync(
            "game_state/inventory/items.json",
            new JsonObject
            {
                ["items"] = soldItem == null
                    ? new JsonArray()
                    : new JsonArray(soldItem.DeepClone()),
                ["equippedItems"] = new JsonObject()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var indexedCarriers = stockItems
            .Select(item => (item, "npc_inventory", canonicalNpcId, (string?)null))
            .ToList();
        if (soldItem != null)
            indexedCarriers.Add((soldItem, "player_inventory", "player", null));
        if (buybackItem != null)
            indexedCarriers.Add((buybackItem, "npc_inventory", canonicalNpcId, null));
        await _fs.WriteFileAtomicAsync(
            MortalItemIdentityState.StatePath,
            MortalItemTestFixture.CreateIndexForCarriers(indexedCarriers.ToArray())
                .ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private static JsonObject CreateCanonicalSellableItem(
        string itemId,
        string name,
        Action<JsonObject>? configure = null)
    {
        var item = MortalItemTestFixture.CreateRawRoot(
            route: "player_acquisition",
            authorityKind: "turn_outcome",
            authorityId: "turn_5",
            sourceTurn: 5,
            creationRef: $"new_item_{itemId}",
            materializationId: $"mat_item_{itemId}");
        item["name"] = name;
        item["description"] = $"Тестовый canonical предмет «{name}».";
        item["price"] = 20;
        item["baseSellPrice"] = 8;
        configure?.Invoke(item);

        var receipt = MortalItemIdentityState.CreateRootReceipt(item, itemId, acceptedTurn: 5);
        item["itemId"] = itemId;
        item["existedId"] = itemId;
        item.Remove("creationRef");
        item["materializationReceipt"] = receipt;
        return item;
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
