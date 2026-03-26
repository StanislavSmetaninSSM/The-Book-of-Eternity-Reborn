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
        Assert.DoesNotContain("Походный фонарь", inventoryRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("\"money\": 500", statusRaw ?? string.Empty, StringComparison.Ordinal);
    }

}
