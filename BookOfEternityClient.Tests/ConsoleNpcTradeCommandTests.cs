using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ConsoleNpcTradeCommandTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;

    public ConsoleNpcTradeCommandTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-console-npc-trade-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _stateManager = new StateManager(_fs, new GameSettings(), NullLogger<StateManager>.Instance);
    }

    [Fact]
    [Trait("Category", "ConsoleNpcTrade")]
    public async Task TryProcessCommand_NpcTradeWithNpcId_OpensTradePanelInsteadOfNpcDossier()
    {
        await SeedNpcTradeStateAsync();
        await _stateManager.RefreshGameStateAsync();
        var console = new TestExplorerConsole();
        console.QueueSelection("Выберите раздел", "← Назад");
        var explorer = new ExplorerMode(
            _stateManager,
            _fs,
            new LocalizationManager(),
            npcTradeService: new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance),
            console: console);

        var result = await explorer.TryProcessCommand("/торговля_нпс npc_merchant_001");

        Assert.Equal(string.Empty, result);
        Assert.Contains(console.SelectionTitles, title => title.Contains("Выберите раздел", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(console.SelectionChoicesHistory, entry =>
            entry.Choices.Any(choice => choice.Contains("Купить товары", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(console.SelectionTitles, title => title.Contains("Выберите НПС", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "ConsoleNpcTrade")]
    public async Task TryProcessCommand_NpcTradeWithoutInventory_WaitsInPlaceForGmVitrine()
    {
        await SeedNpcTradeStateWithoutInventoryAsync();
        await _stateManager.RefreshGameStateAsync();
        var console = new TestExplorerConsole();
        var explorer = new ExplorerMode(
            _stateManager,
            _fs,
            new LocalizationManager(),
            npcTradeService: new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance),
            console: console);

        var result = await explorer.TryProcessCommand("/торговля_нпс npc_merchant_001");

        Assert.Equal(string.Empty, result);
        Assert.Contains(console.MarkupLines, line =>
            line.Contains("Витрина подготавливается. Дождитесь завершения, ГМ работает", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(console.MarkupLines, line =>
            line.Contains("откройте торговлю снова", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(console.SelectionTitles, title => title.Contains("Выберите раздел", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, console.ReadKeyCalls);
    }

    [Fact]
    [Trait("Category", "ConsoleNpcTrade")]
    public async Task TryProcessCommand_NpcTradeWithNpcName_OpensTradePanel()
    {
        await SeedNpcTradeStateAsync();
        await _stateManager.RefreshGameStateAsync();
        var console = new TestExplorerConsole();
        console.QueueSelection("Выберите раздел", "← Назад");
        var explorer = new ExplorerMode(
            _stateManager,
            _fs,
            new LocalizationManager(),
            npcTradeService: new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance),
            console: console);

        var result = await explorer.TryProcessCommand("/торговля_нпс Марек");

        Assert.Equal(string.Empty, result);
        Assert.Contains(console.SelectionTitles, title => title.Contains("Выберите раздел", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(console.SelectionChoicesHistory, entry =>
            entry.Choices.Any(choice => choice.Contains("Купить товары", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(console.MarkupLines, text => text.Contains("Не удалось загрузить витрину", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "ConsoleNpcTrade")]
    public async Task TryProcessCommand_NpcTradeWithoutNpcId_SelectsMerchantAndOpensTradePanel()
    {
        await SeedNpcTradeStateAsync();
        await _stateManager.RefreshGameStateAsync();
        var console = new TestExplorerConsole();
        console.QueueSelection("Выберите раздел", "← Назад");
        var explorer = new ExplorerMode(
            _stateManager,
            _fs,
            new LocalizationManager(),
            npcTradeService: new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance),
            console: console);

        var result = await explorer.TryProcessCommand("/торговля_нпс");

        Assert.Equal(string.Empty, result);
        Assert.Contains(console.SelectionTitles, title => title.Contains("Торговля с НПС", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(console.SelectionTitles, title => title.Contains("Выберите раздел", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(console.SelectionChoicesHistory, entry =>
            entry.Choices.Any(choice => choice.Contains("Марек", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(console.SelectionChoicesHistory, entry =>
            entry.Choices.Any(choice => choice.Contains("Купить товары", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(console.SelectionTitles, title => title.Contains("Выберите НПС", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "ConsoleNpcTrade")]
    public async Task TryProcessCommand_NpcTradeWithoutNpcId_SupportsSameTurnMerchantInitialId()
    {
        await SeedNpcTradeStateAsync(useSameTurnInitialId: true);
        await _stateManager.RefreshGameStateAsync();
        var console = new TestExplorerConsole();
        console.QueueSelection("Выберите раздел", "← Назад");
        var explorer = new ExplorerMode(
            _stateManager,
            _fs,
            new LocalizationManager(),
            npcTradeService: new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance),
            console: console);

        var result = await explorer.TryProcessCommand("/торговля_нпс");

        Assert.Equal(string.Empty, result);
        Assert.Contains(console.SelectionTitles, title => title.Contains("Торговля с НПС", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(console.SelectionTitles, title => title.Contains("Выберите раздел", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(console.SelectionChoicesHistory, entry =>
            entry.Choices.Any(choice => choice.Contains("Марек", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(console.SelectionTitles, title => title.Contains("Выберите НПС", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "ConsoleNpcTrade")]
    public async Task TryProcessCommand_NpcTradeWithoutNpcIdAndMissingInventory_WaitsInPlaceWithoutReturningToMerchantList()
    {
        await SeedNpcTradeStateWithoutInventoryAsync();
        await _stateManager.RefreshGameStateAsync();
        var console = new TestExplorerConsole();
        var explorer = new ExplorerMode(
            _stateManager,
            _fs,
            new LocalizationManager(),
            npcTradeService: new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance),
            console: console);

        var result = await explorer.TryProcessCommand("/торговля_нпс");

        Assert.Equal(string.Empty, result);
        Assert.Contains(console.MarkupLines, line =>
            line.Contains("Витрина подготавливается. Дождитесь завершения, ГМ работает", StringComparison.OrdinalIgnoreCase));
        Assert.Single(console.SelectionTitles, title =>
            title.Contains("Торговля с НПС", StringComparison.OrdinalIgnoreCase));
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
            // ignore temp cleanup failures
        }
    }

    private async Task SeedNpcTradeStateAsync(bool useSameTurnInitialId = false)
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        await _fs.WriteFileAtomicAsync("stories/console-npc-trade-test.json", """
        {
          "turnNumber": 12
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

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNPCs": [
            {
              "NPCId": "__NPC_ID__",
              "initialId": "__INITIAL_ID__",
              "name": "Марек",
              "currentLocationId": "loc_market_square",
              "currentLocation": "Рыночная площадь",
              "level": 10,
              "relationshipLevel": 80,
              "characteristics": { "modifiedTrade": 14 },
              "tradeState": {
                "canTrade": true,
                "merchantProfile": "GeneralGoods"
              },
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
                      "description": "Тестовый ассортимент.",
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
                      "description": "Тестовый ассортимент.",
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
                      "description": "Тестовый ассортимент.",
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
                      "description": "Тестовый ассортимент.",
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
                      "description": "Тестовый ассортимент.",
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
                    "price": 31,
                    "merchantProfile": "GeneralGoods",
                    "soldOut": false,
                    "itemData": {
                      "itemId": "npc_item_merchant_006",
                      "name": "Записная книжка",
                      "description": "Тестовый ассортимент.",
                      "type": "Document",
                      "tradeItemClass": "FlavorOrUtility",
                      "quality": "Common",
                      "price": 25,
                      "baseSellPrice": 10,
                      "weight": "0.1",
                      "group": "Документы и медиа"
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
                      "description": "Тестовый ассортимент.",
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
              },
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
            }
          ]
        }
        """);

        var raw = await _fs.ReadFileAsync("game_state/npcs/npc_core.json") ?? "";
        raw = useSameTurnInitialId
            ? raw.Replace("\"__NPC_ID__\"", "null").Replace("__INITIAL_ID__", "npc_merchant_initial_001")
            : raw.Replace("__NPC_ID__", "npc_merchant_001").Replace("__INITIAL_ID__", "npc_merchant_initial_001");
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", raw);
    }

    private async Task SeedNpcTradeStateWithoutInventoryAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        await _fs.WriteFileAtomicAsync("stories/console-npc-trade-request-test.json", """
        {
          "turnNumber": 13
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

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNPCs": [
            {
              "NPCId": "npc_merchant_001",
              "name": "Марек",
              "currentLocationId": "loc_market_square",
              "currentLocation": "Рыночная площадь",
              "level": 10,
              "relationshipLevel": 80,
              "characteristics": { "modifiedTrade": 14 },
              "tradeState": {
                "canTrade": true,
                "merchantProfile": "GeneralGoods"
              }
            }
          ]
        }
        """);
    }
}
