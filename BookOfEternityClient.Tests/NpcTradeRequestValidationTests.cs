using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class NpcTradeRequestValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public NpcTradeRequestValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-npc-trade-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        CopyDirectory(TestRepoPaths.BaseSessionRoot, Path.Combine(_rootPath, "game_session"));

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnNpcTradeRequestWithoutInventory_FailsResolutionContract()
    {
        var request = new
        {
            requestId = "npc_trade_req_001",
            npcId = "npc_merchant_001",
            npcName = "Марек",
            merchantProfile = "GeneralGoods",
            tradeCycleId = "world_trade_0",
            derivedTradeSlotCount = 7,
            createdAtTurn = 7,
            createdAtUtc = "2026-03-28T00:00:00Z",
            createdAtWorldDate = 100,
            refreshAfterWorldDate = 43200
        };

        await SeedBaseStateAsync(includeTradeInventory: false, includeTradeReceipt: false);
        await WriteJsonAsync(NpcTradeRequestState.PendingRequestPath, new { requests = new[] { request } });
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new { currentRealm = "Mortal World" });
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_pending_npc_trade_inventory_request.json";
        await WriteJsonAsync(backupPath, new { requests = new[] { request } });
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [NpcTradeRequestState.PendingRequestPath] = backupPath
        });

        var issues = await InvokeValidationAsync("ValidatePendingNpcTradeInventoryRequestResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "npc_trade_request_missing_inventory_resolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnNpcTradeRequestWithInventoryButWithoutReceipt_FailsResolutionContract()
    {
        var request = new
        {
            requestId = "npc_trade_req_002",
            npcId = "npc_merchant_001",
            npcName = "Марек",
            merchantProfile = "GeneralGoods",
            tradeCycleId = "world_trade_0",
            derivedTradeSlotCount = 7,
            createdAtTurn = 7,
            createdAtUtc = "2026-03-28T00:00:00Z",
            createdAtWorldDate = 100,
            refreshAfterWorldDate = 43200
        };

        await SeedBaseStateAsync(includeTradeInventory: true, includeTradeReceipt: false);
        await WriteJsonAsync(NpcTradeRequestState.PendingRequestPath, new { requests = new[] { request } });
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new { currentRealm = "Mortal World" });
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_pending_npc_trade_inventory_request_receipt.json";
        await WriteJsonAsync(backupPath, new { requests = new[] { request } });
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [NpcTradeRequestState.PendingRequestPath] = backupPath
        });

        var issues = await InvokeValidationAsync("ValidatePendingNpcTradeInventoryRequestResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "npc_trade_request_missing_receipt_resolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_NpcBuybackInventoryWithMalformedEntry_FailsValidation()
    {
        await SeedBaseStateAsync(includeTradeInventory: false, includeTradeReceipt: false, buybackBlock: """
        ,
          "buybackInventory": [
            {
              "buybackEntryId": "npc_buyback_001",
              "npcId": "npc_merchant_001",
              "itemId": "item_sell_lantern_001",
              "itemData": {
                "itemId": "other_item_id",
                "name": "Походный фонарь",
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
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "npc_buyback_entry_item_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    private async Task SeedBaseStateAsync(bool includeTradeInventory, bool includeTradeReceipt, string buybackBlock = "")
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Mortal World",
            currentIncarnation = 1
        });
        await WriteJsonAsync("game_state/core/player_status.json", new
        {
            money = 500,
            trade = 12
        });
        await WriteJsonAsync("game_state/world/world_time.json", new
        {
            currentTimeInMinutes = 100
        });
        await WriteJsonAsync("game_state/world/current_location.json", new
        {
            locationId = "loc_market_square",
            name = "Рыночная площадь"
        });

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

        var receiptBlock = includeTradeInventory && includeTradeReceipt
            ? """
            ,
              "tradeInventoryReceipts": [
                {
                  "requestId": "npc_trade_req_002",
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

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", $$"""
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_merchant_001",
              "name": "Марек",
              "currentLocationId": "loc_market_square",
              "currentLocation": "Рыночная площадь",
              "level": 10,
              "relationshipLevel": 80,
              "characteristics": { "modifiedTrade": 14 },
              "tradeState": {
                "canTrade": true,
                "merchantProfile": "GeneralGoods"
              }{{inventoryBlock}}{{receiptBlock}}{{buybackBlock}}
            }
          ]
        }
        """);
    }

    private async Task WritePendingTurnSnapshotManifestAsync(Dictionary<string, string> rollbackBackups)
    {
        var manifest = new
        {
            sessionId = "test-session",
            requestId = "test-request",
            turnNumber = 12,
            requestTimestamp = "2026-03-28T00:00:00Z",
            playerAction = "test",
            files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            snapshotFileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            clientOwnedValidationHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            rollbackBackups,
            rollbackBaselineFiles = Array.Empty<string>(),
            sourceLabel = "npc-trade-validation-tests",
            manifestPayloadHash = string.Empty
        };

        await WriteJsonAsync("game_state/control/pending_turn_snapshot.json", manifest);
    }

    private async Task<List<ValidationIssue>> InvokeValidationAsync(string methodName)
    {
        var issues = new List<ValidationIssue>();
        var method = typeof(ValidationService).GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = method!.Invoke(_validator, new object[] { issues }) as Task;
        Assert.NotNull(task);
        await task!;
        return issues;
    }

    private async Task WriteJsonAsync(string relativePath, object payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        await _fs.WriteFileAtomicAsync(relativePath, json);
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);
        foreach (var file in Directory.GetFiles(sourceDir))
            File.Copy(file, Path.Combine(destinationDir, Path.GetFileName(file)), overwrite: true);
        foreach (var directory in Directory.GetDirectories(sourceDir))
            CopyDirectory(directory, Path.Combine(destinationDir, Path.GetFileName(directory)));
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
            // ignored
        }
    }
}
