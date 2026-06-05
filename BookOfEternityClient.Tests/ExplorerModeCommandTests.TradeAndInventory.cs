using System.Text.Json;
using System.Reflection;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using Spectre.Console.Rendering;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerModeCommandTests : IDisposable
{
    [Fact]
    public async Task TryProcessCommand_Npcs_UsesSharedRelationshipLabelsInChoices()
    {
        await SeedNpcTradeStateAsync();
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/нпс"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("npcs_reputation_choices");
        Assert.Contains(_console.SelectionChoicesHistory.SelectMany(entry => entry.Choices),
            choice => choice.Contains("♥ 80 (Нейтралитет)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TryProcessCommand_InventoryEscapesItemTypeInSelectionChoices()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/inventory/items.json", new
        {
            items = new[]
            {
                new
                {
                    itemId = "ring_001",
                    name = "Перстень дома Вальмонт",
                    type = "Кольцо",
                    count = 1,
                    equipmentSlot = "Finger1"
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/инв"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("inventory_escape_item_type");
        var inventoryChoices = _console.SelectionChoicesHistory
            .Where(entry => entry.Title.Contains("Инвентарь", StringComparison.OrdinalIgnoreCase))
            .SelectMany(entry => entry.Choices)
            .ToArray();
        Assert.Contains(inventoryChoices, choice => choice.Contains("[[Кольцо]]", StringComparison.Ordinal));
        Assert.DoesNotContain(inventoryChoices, choice => choice.Contains(" [Кольцо]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TryProcessCommand_InventoryPromptChoices_EscapeBracketBearingDynamicLabels()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/inventory/items.json", new
        {
            resources = new Dictionary<string, string>
            {
                ["осколки [debug]"] = "3 [card_alpha, card_beta]"
            },
            items = new[]
            {
                new
                {
                    itemId = "ring_bracket_001",
                    name = "Перстень [debug]",
                    type = "Кольцо",
                    count = 1,
                    equipmentSlot = "ring1"
                }
            },
            equipment = new
            {
                ring1 = new
                {
                    itemId = "ring_bracket_001",
                    name = "Перстень [debug]"
                }
            }
        });
        await WriteJsonAsync("game_state/world/current_location.json", new
        {
            name = "Тестовая площадь",
            locationStorages = new[]
            {
                new
                {
                    storageId = "storage_bracket_001",
                    name = "Сундук [broken",
                    hasFullAccess = true,
                    contents = Array.Empty<object>()
                },
                new
                {
                    storageId = "storage_locked_bracket_001",
                    name = "Сейф [debug]",
                    hasFullAccess = false,
                    contents = Array.Empty<object>()
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/инв"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("inventory_bracket_prompt_choices");
        AssertSelectionChoicesAreSpectreMarkupSafe("inventory_bracket_prompt_choices", "Инвентарь");
        var choices = _console.SelectionChoicesHistory
            .Where(entry => entry.Title.Contains("Инвентарь", StringComparison.OrdinalIgnoreCase))
            .SelectMany(entry => entry.Choices)
            .ToArray();
        Assert.Contains(choices, choice => choice.Contains("[[debug]]", StringComparison.Ordinal));
        Assert.Contains(choices, choice => choice.Contains("[[broken", StringComparison.Ordinal));
        Assert.Contains(choices, choice => choice.Contains("[[card_alpha, card_beta]]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TryProcessCommand_InventoryDetail_UnresolvedMechanicalSummaryShowsReason()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/inventory/items.json", new
        {
            items = new[]
            {
                new
                {
                    itemId = "sealed_glove_1",
                    name = "Запечатанная перчатка",
                    description = "Руны на коже перчатки закрыты тусклой печатью.",
                    type = "Перчатки",
                    count = 1,
                    bonuses = new[] { "Аркановедение +1" },
                    mechanicalSummaryAuthority = "Unresolved",
                    mechanicalSummaryUnresolvedReason = "Руны запечатаны, эффект станет ясен после ритуала распознавания."
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/инв"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("inventory_unresolved_mechanics_reason");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Механика не раскрыта", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Руны запечатаны", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("• Аркановедение +1", renderedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_NpcPromptChoices_EscapeBracketBearingDynamicLabels()
    {
        await SeedMortalStateAsync();
        await WriteRawJsonAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_bracket_001",
              "name": "Лира [debug]",
              "relationshipLevel": "доверие [ally]",
              "currentLocation": "Площадь [broken",
              "domain": "Домен [card_alpha, card_beta]",
              "description": "НПС с bracket-bearing authored data."
            }
          ]
        }
        """);
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/нпс"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("npcs_bracket_prompt_choices");
        AssertMatchingSelectionChoicesAreSpectreMarkupSafe(
            "npcs_bracket_prompt_choices",
            choice => choice.Contains("Лира", StringComparison.OrdinalIgnoreCase));
        var choices = _console.SelectionChoicesHistory
            .SelectMany(entry => entry.Choices)
            .Where(choice => choice.Contains("Лира", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.Contains(choices, choice => choice.Contains("[[debug]]", StringComparison.Ordinal));
        Assert.Contains(choices, choice => choice.Contains("[[ally]]", StringComparison.Ordinal));
        Assert.Contains(choices, choice => choice.Contains("[[broken", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TryProcessCommand_InventoryStorageMove_MovesItemIntoStorage()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/inventory/items.json", new
        {
            money = 10,
            items = new[]
            {
                new
                {
                    itemId = "item_apple_001",
                    name = "Яблоко"
                }
            }
        });
        await WriteJsonAsync("game_state/world/current_location.json", new
        {
            name = "Тестовая площадь",
            locationStorages = new[]
            {
                new
                {
                    storageId = "storage_chest_001",
                    name = "Сундук",
                    hasFullAccess = true,
                    contents = Array.Empty<object>()
                }
            }
        });

        _console.QueueSelection("🎒", "📦 Сундук (0 пр.) → управление");
        _console.QueueSelection("Сундук", "📥 Положить предмет в хранилище (1 в инвентаре)", "← Назад к инвентарю");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/инв"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("inventory_storage_move");
        var inventoryRaw = await _fs.ReadFileAsync("game_state/inventory/items.json");
        var locationRaw = await _fs.ReadFileAsync("game_state/world/current_location.json");
        Assert.DoesNotContain("Яблоко", inventoryRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("Яблоко", locationRaw ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]

    public async Task TryProcessCommand_TransportInventoryMove_MovesItemIntoVehicle()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/inventory/items.json", new
        {
            items = new[]
            {
                new
                {
                    itemId = "item_rope_001",
                    name = "Веревка"
                }
            },
            equipment = new { }
        });
        await WriteJsonAsync("game_state/misc/vehicles.json", new
        {
            vehicles = new[]
            {
                new
                {
                    vehicleId = "vehicle_cart_001",
                    name = "Телега",
                    type = "vehicle",
                    isActive = true,
                    inventory = Array.Empty<object>()
                }
            }
        });

        _console.QueueSelection("Действие с транспортом", "🎒 Управлять инвентарём транспорта");
        _console.QueueSelection("Телега", "📥 Положить предмет в транспорт (1 в инвентаре)", "← Назад к транспорту");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/транспорт"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("transport_inventory_move");
        var inventoryRaw = await _fs.ReadFileAsync("game_state/inventory/items.json");
        var vehiclesRaw = await _fs.ReadFileAsync("game_state/misc/vehicles.json");
        Assert.DoesNotContain("Веревка", inventoryRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("Веревка", vehiclesRaw ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]

    public async Task TryProcessCommand_NpcTradeBuy_SucceedsAndMarksOfferSoldOut()
    {
        await SeedNpcTradeStateAsync();
        _console.QueueSelection("Действие", "🛒 Торговать", "🛍 Купить");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/нпс"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("npc_trade_buy");
        var npcRaw = await _fs.ReadFileAsync("game_state/npcs/npc_core.json");
        var inventoryRaw = await _fs.ReadFileAsync("game_state/inventory/items.json");
        var statusRaw = await _fs.ReadFileAsync("game_state/core/player_status.json");
        Assert.Contains("\"soldOut\": true", npcRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"items\"", inventoryRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("\"money\": 500", statusRaw ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]

    public async Task TryProcessCommand_NpcTradeAction_IsShownWhenAvailabilityAllowsTrade()
    {
        await SeedNpcTradeStateAsync();
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/нпс"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("npc_trade_action_present");
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Действие", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Contains("🛒 Торговать", StringComparer.Ordinal));
    }

    [Fact]
    public async Task TryProcessCommand_NpcTrade_CreatesPendingInventoryRequestWhenStockIsMissing()
    {
        await SeedNpcTradeStateAsync(includeTradeInventory: false, includeTradeReceipt: false);
        _console.QueueSelection("Действие", "🛒 Торговать");
        _console.QueueSelection("Выберите раздел", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/нпс"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("npc_trade_pending_inventory_request");
        var pendingRaw = await _fs.ReadFileAsync(NpcTradeRequestState.PendingRequestPath);
        Assert.NotNull(pendingRaw);
        Assert.Contains("\"npcId\": \"npc_merchant_001\"", pendingRaw, StringComparison.Ordinal);
        Assert.Contains("\"tradeCycleId\": \"world_trade_0\"", pendingRaw, StringComparison.Ordinal);
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Выберите раздел", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Contains("🔄 Проверить витрину", StringComparer.Ordinal));
    }

    [Fact]

    public async Task TryProcessCommand_NpcTalkAction_CreatesPendingNpcSocialRequest()
    {
        await SeedNpcTradeStateAsync();
        _console.QueueSelection("Действие", "💬 Поговорить");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/нпс"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("npc_social_talk_request");
        var pendingRaw = await _fs.ReadFileAsync(ActorSocialInteractionRequestState.PendingNpcRequestPath);
        Assert.NotNull(pendingRaw);
        Assert.Contains("\"npcId\": \"npc_merchant_001\"", pendingRaw, StringComparison.Ordinal);
        Assert.Contains("\"interactionType\": \"talk\"", pendingRaw, StringComparison.Ordinal);
    }

    [Fact]

    public async Task TryProcessCommand_NpcTradeAction_IsHiddenWhenTradeIsBlocked()
    {
        await SeedNpcTradeStateAsync(canTrade: false);
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/нпс"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("npc_trade_action_hidden");
        Assert.DoesNotContain(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Действие", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Contains("🛒 Торговать", StringComparer.Ordinal));
    }

    [Fact]

    public async Task TryProcessCommand_NpcTradeSell_SucceedsAndRemovesSoldItem()
    {
        await SeedNpcTradeStateAsync(includeSellableInventoryItem: true);
        _console.QueueSelection("Выберите раздел", "💰 Продать товары");
        _console.QueueSelection("Действие", "🛒 Торговать", "💰 Продать");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/нпс"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("npc_trade_sell");
        var inventoryRaw = await _fs.ReadFileAsync("game_state/inventory/items.json");
        var statusRaw = await _fs.ReadFileAsync("game_state/core/player_status.json");
        var npcRaw = await _fs.ReadFileAsync("game_state/npcs/npc_core.json");
        Assert.DoesNotContain("Походный фонарь", inventoryRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("\"money\": 500", statusRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"buybackInventory\"", npcRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"available\"", npcRaw ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_NpcTradeBuyback_ReacquiresPreviouslySoldItem()
    {
        await SeedNpcTradeStateAsync(includeBuybackInventory: true, includeTradeInventory: false, includeTradeReceipt: false);
        _console.QueueSelection("Выберите раздел", "🔁 Выкупить обратно");
        _console.QueueSelection("Действие", "🛒 Торговать", "🔁 Выкупить");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/нпс"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("npc_trade_buyback");
        var inventoryRaw = await _fs.ReadFileAsync("game_state/inventory/items.json");
        var npcRaw = await _fs.ReadFileAsync("game_state/npcs/npc_core.json");
        Assert.Contains("Походный фонарь", inventoryRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"rebought\"", npcRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Выберите раздел", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Contains("🔁 Выкупить обратно", StringComparer.Ordinal));
    }

}
