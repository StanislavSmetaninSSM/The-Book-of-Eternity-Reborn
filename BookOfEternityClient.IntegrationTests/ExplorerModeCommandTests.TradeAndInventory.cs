using System.Text.Json;
using System.Text.Json.Nodes;
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
    public async Task TryProcessCommand_Npcs_RichNpcShowsDetailSectionMenu()
    {
        await SeedRichNpcDrilldownStateAsync();
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/нпс"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("npc_detail_drilldown_sections");
        var sectionPrompt = _console.SelectionChoicesHistory.First(
            entry => entry.Title.Contains("Разделы НПС", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(sectionPrompt.Choices,
            choice => choice.Contains("Дневник / мысли", StringComparison.Ordinal) &&
                      choice.Contains("2 записи", StringComparison.Ordinal));
        Assert.Contains(sectionPrompt.Choices,
            choice => choice.Contains("Личные квесты", StringComparison.Ordinal) &&
                      choice.Contains("1 квест", StringComparison.Ordinal));
        Assert.Contains(sectionPrompt.Choices,
            choice => choice.Contains("Активности", StringComparison.Ordinal) &&
                      choice.Contains("1 активность", StringComparison.Ordinal));
        Assert.DoesNotContain(sectionPrompt.Choices,
            choice => choice.Contains("Инвентарь", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(sectionPrompt.Choices,
            choice => choice.Contains("← Закрыть разделы НПС", StringComparison.Ordinal));
        Assert.DoesNotContain(sectionPrompt.Choices,
            choice => choice.Contains("← К списку НПС", StringComparison.Ordinal));
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
    public async Task TryProcessCommand_InventoryDetail_StructuredBonusShowsLocalizedKnownFieldNames()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/inventory/items.json", new
        {
            items = new[]
            {
                new
                {
                    itemId = "runic_glove_1",
                    name = "Руническая перчатка",
                    description = "Узоры мерцают тусклым золотом.",
                    type = "Артефакт",
                    count = 1,
                    structuredBonuses = new[]
                    {
                        new
                        {
                            targetType = "skill",
                            skill = "Чувство магических потоков",
                            valueType = "Flat",
                            value = 2,
                            source = "Руническая перчатка",
                            summary = "Чувство магических потоков +2",
                            stackingRule = "replace [debug]",
                            experimentalKey = "raw [value]"
                        }
                    }
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/инв"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("inventory_structured_bonus_value_type");
        var renderedText = ExtractRenderedText();
        Assert.DoesNotContain("поврежд", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Структурные бонусы", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Чувство магических потоков +2", renderedText, StringComparison.Ordinal);
        Assert.Contains("Тип цели: навык", renderedText, StringComparison.Ordinal);
        Assert.Contains("Навык: Чувство магических потоков", renderedText, StringComparison.Ordinal);
        Assert.Contains("Тип значения: плоский бонус", renderedText, StringComparison.Ordinal);
        Assert.Contains("Значение: 2", renderedText, StringComparison.Ordinal);
        Assert.Contains("Источник: Руническая перчатка", renderedText, StringComparison.Ordinal);
        Assert.Contains("Кратко: Чувство магических потоков +2", renderedText, StringComparison.Ordinal);
        Assert.Contains("Правило сложения: replace [debug]", renderedText, StringComparison.Ordinal);
        Assert.Contains("experimental Key: raw [value]", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("targetType:", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("valueType:", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("stackingRule:", renderedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_InventoryQuestItemDetail_HidesRawBookkeepingAndLocalizesEnums()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/inventory/items.json", new
        {
            items = new[]
            {
                new
                {
                    itemId = "seal_bandage_001",
                    name = "Запятнанная повязка с чёрной печатью",
                    description = "Срезанная с раненого льняная повязка.",
                    type = "QuestItem",
                    quality = "Common",
                    count = 1,
                    value = 0,
                    weight = 0.1,
                    durability = 100,
                    equipmentSlot = "Accessory1",
                    group = "Стартовые зацепки",
                    textContent = new[] { "Чёрная печать похожа на знак запрещённого братства." },
                    currentLocationId = "loc_life_001_start",
                    currentLocationName = "Дом лекаря Вирента: задняя лечебница",
                    isCarried = false,
                    isEquipped = false,
                    visibility = "known"
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/инв"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("inventory_quest_item_player_facing_detail");
        var renderedText = ExtractRenderedText();
        Assert.Contains("сюжетный предмет", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("обычное", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("аксессуар", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Прочность", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("100%/", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("100/", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("QuestItem", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Common", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Accessory1", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("currentLocationId", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("isCarried", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("isEquipped", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("visibility", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("value:", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("В текущей локации", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("В рюкзаке", renderedText, StringComparison.OrdinalIgnoreCase);

        var actionChoices = _console.SelectionChoicesHistory
            .Where(entry => entry.Title.Contains("Действие", StringComparison.OrdinalIgnoreCase))
            .SelectMany(entry => entry.Choices)
            .ToArray();
        Assert.DoesNotContain(actionChoices,
            choice => choice.Contains("Сложить с другим предметом", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(actionChoices,
            choice => choice.Contains("Выбросить", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryProcessCommand_InventoryDetail_StripsTechnicalTurnAnchorsFromJournalEntries()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/inventory/items.json", new
        {
            items = new[]
            {
                new
                {
                    itemId = "glove_journal_anchor_001",
                    name = "Руническая перчатка",
                    description = "Кожа перчатки хранит слабый золотой отблеск.",
                    type = "Артефакт",
                    count = 1,
                    equipmentSlot = (string?)null,
                    journalEntries = new[]
                    {
                        "#[4]. Предмет найден на столе у окна."
                    }
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/инв"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("inventory_journal_anchor_player_facing");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Предмет найден на столе у окна.", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("#[4]", renderedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_NpcPersonalityDetail_SplitsFactsAndLocalizesRelationshipValues()
    {
        await SeedMortalStateAsync();
        await WriteRawJsonAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_virent",
              "name": "Лекарь Вирент",
              "currentLocation": "Дом лекаря Вирента",
              "relationshipLevel": 0,
              "race": "Человек",
              "class": "Лекарь",
              "role": "наставник и городской лекарь",
              "rarity": "Common",
              "worldview": "Лучше нарушить правило, чем дать человеку умереть на столе.",
              "attitude": "Neutral",
              "culturalLayer": "Нижний Порт Арвельмара",
              "culturalStance": "Прагматик",
              "plans": "Закончить перевязку и выяснить, почему на пациенте чёрная печать.",
              "personalityTraits": [
                {
                  "traitName": "Милосердие с ценой",
                  "description": "Рискует ради пациента, если окружающие выдержат его правила.",
                  "valueDescription": "Может стать защитником или строгим судьёй первых ошибок ученицы."
                }
              ]
            }
          ]
        }
        """);
        _console.QueueSelection("Разделы НПС", "Личность / маски");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/нпс"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("npc_personality_player_facing_details");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Мировоззрение", renderedText, StringComparison.Ordinal);
        Assert.Contains("Нейтралитет", renderedText, StringComparison.Ordinal);
        Assert.Contains("Культурный слой", renderedText, StringComparison.Ordinal);
        Assert.Contains("Милосердие с ценой", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Отношение: Neutral", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("; Отношение", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Подробности Мировоззрение", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Название черты:", renderedText, StringComparison.Ordinal);
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
        var npcBefore = JsonNode.Parse((await _fs.ReadFileAsync("game_state/npcs/npc_core.json"))!)!.AsObject();
        var stockBefore = npcBefore["UpdateNPCs"]![0]!["inventory"]!.AsArray()
            .OfType<JsonObject>()
            .ToDictionary(item => item["itemId"]!.GetValue<string>(), StringComparer.Ordinal);
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
        var inventory = JsonNode.Parse(inventoryRaw!)!.AsObject();
        var npcAfter = JsonNode.Parse(npcRaw!)!.AsObject()["UpdateNPCs"]![0]!.AsObject();
        var soldSlot = Assert.Single(
            npcAfter["tradeInventory"]!["items"]!.AsArray(),
            item => item!["soldOut"]!.GetValue<bool>());
        var purchasedItemId = soldSlot!["itemId"]!.GetValue<string>();
        var receiptBefore = stockBefore[purchasedItemId]["materializationReceipt"]!.DeepClone();
        var envelopeBefore = stockBefore[purchasedItemId]["materialization"]!.DeepClone();
        var purchased = Assert.Single(
            inventory["items"]!.AsArray(),
            item => item!["itemId"]!.GetValue<string>() == purchasedItemId);
        Assert.True(JsonNode.DeepEquals(receiptBefore, purchased!["materializationReceipt"]));
        Assert.True(JsonNode.DeepEquals(envelopeBefore, purchased["materialization"]));
        Assert.DoesNotContain(
            npcAfter["inventory"]!.AsArray(),
            item => item!["itemId"]!.GetValue<string>() == purchasedItemId);
        var index = MortalItemIdentityState.Parse(
            (await _fs.ReadFileAsync(MortalItemIdentityState.StatePath))!);
        Assert.Equal(
            "player_inventory",
            index.EntriesByItemId[purchasedItemId]["currentCarrier"]!["kind"]!.GetValue<string>());
        Assert.Equal(2, index.EntriesByItemId[purchasedItemId]["transitions"]!.AsArray().Count);
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
        await _stateManager.RefreshGameStateAsync();

        var result = await _explorer.TryProcessCommand("/нпс");

        Assert.Equal(string.Empty, result);
        AssertNoHiddenExplorerErrors("npc_trade_pending_inventory_request");
        var pendingRaw = await _fs.ReadFileAsync(NpcTradeRequestState.PendingRequestPath);
        Assert.NotNull(pendingRaw);
        Assert.Contains("\"npcId\": \"npc_merchant_001\"", pendingRaw, StringComparison.Ordinal);
        Assert.Contains("\"tradeCycleId\": \"world_trade_0\"", pendingRaw, StringComparison.Ordinal);
        Assert.DoesNotContain(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Выберите раздел", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(_console.MarkupLines,
            line => line.Contains("Витрина подготавливается. Дождитесь завершения, ГМ работает", StringComparison.OrdinalIgnoreCase));
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
        var inventoryBefore = JsonNode.Parse(
            (await _fs.ReadFileAsync("game_state/inventory/items.json"))!)!.AsObject();
        var soldBefore = Assert.Single(inventoryBefore["items"]!.AsArray())!.AsObject();
        var receiptBefore = soldBefore["materializationReceipt"]!.DeepClone();
        var envelopeBefore = soldBefore["materialization"]!.DeepClone();
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
        var npc = JsonNode.Parse(npcRaw!)!.AsObject()["UpdateNPCs"]![0]!.AsObject();
        var soldAfter = Assert.Single(
            npc["inventory"]!.AsArray(),
            item => item!["itemId"]!.GetValue<string>() == "item_sell_lantern_001");
        Assert.True(JsonNode.DeepEquals(receiptBefore, soldAfter!["materializationReceipt"]));
        Assert.True(JsonNode.DeepEquals(envelopeBefore, soldAfter["materialization"]));
        var buybackProjection = Assert.Single(npc["buybackInventory"]!.AsArray())!["itemData"]!.AsObject();
        Assert.False(buybackProjection.ContainsKey("materialization"));
        Assert.False(buybackProjection.ContainsKey("materializationReceipt"));
        var index = MortalItemIdentityState.Parse(
            (await _fs.ReadFileAsync(MortalItemIdentityState.StatePath))!);
        Assert.Equal(
            "npc_inventory",
            index.EntriesByItemId["item_sell_lantern_001"]["currentCarrier"]!["kind"]!.GetValue<string>());
        Assert.Equal(2, index.EntriesByItemId["item_sell_lantern_001"]["transitions"]!.AsArray().Count);
    }

    [Fact]
    public async Task TryProcessCommand_NpcTradeBuyback_ReacquiresPreviouslySoldItem()
    {
        await SeedNpcTradeStateAsync(includeBuybackInventory: true);
        var npcBefore = JsonNode.Parse((await _fs.ReadFileAsync("game_state/npcs/npc_core.json"))!)!.AsObject();
        var buybackBefore = Assert.Single(
            npcBefore["UpdateNPCs"]![0]!["inventory"]!.AsArray(),
            item => item!["itemId"]!.GetValue<string>() == "item_sell_lantern_001")!.AsObject();
        var receiptBefore = buybackBefore["materializationReceipt"]!.DeepClone();
        var envelopeBefore = buybackBefore["materialization"]!.DeepClone();
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
        var inventory = JsonNode.Parse(inventoryRaw!)!.AsObject();
        var rebought = Assert.Single(
            inventory["items"]!.AsArray(),
            item => item!["itemId"]!.GetValue<string>() == "item_sell_lantern_001");
        Assert.True(JsonNode.DeepEquals(receiptBefore, rebought!["materializationReceipt"]));
        Assert.True(JsonNode.DeepEquals(envelopeBefore, rebought["materialization"]));
        var npcAfter = JsonNode.Parse(npcRaw!)!.AsObject()["UpdateNPCs"]![0]!.AsObject();
        Assert.DoesNotContain(
            npcAfter["inventory"]!.AsArray(),
            item => item!["itemId"]!.GetValue<string>() == "item_sell_lantern_001");
        var index = MortalItemIdentityState.Parse(
            (await _fs.ReadFileAsync(MortalItemIdentityState.StatePath))!);
        Assert.Equal(
            "player_inventory",
            index.EntriesByItemId["item_sell_lantern_001"]["currentCarrier"]!["kind"]!.GetValue<string>());
        Assert.Equal(2, index.EntriesByItemId["item_sell_lantern_001"]["transitions"]!.AsArray().Count);
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Выберите раздел", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Contains("🔁 Выкупить обратно", StringComparer.Ordinal));
    }

    [Fact]
    public async Task TryProcessCommand_InventoryDrop_RetiresIdentityAndClearsEquipment()
    {
        await SeedMortalStateAsync();
        var blade = CreateCanonicalManagementItem(
            "itm_console_blade",
            "Стальной клинок",
            count: 1,
            equipmentSlot: "mainHand");
        await SeedCanonicalManagementInventoryAsync(
            new[] { blade },
            new JsonObject { ["mainHand"] = "itm_console_blade" });
        _console.QueueSelection("Действие", "[red]🗑 Выбросить[/]");
        _console.QueueAnyConfirmResponse(true);
        await _stateManager.RefreshGameStateAsync();

        var exception = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/инв"));

        Assert.Null(exception);
        AssertNoHiddenExplorerErrors("inventory_drop_identity");
        var inventory = JsonNode.Parse(
            (await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath))!)!.AsObject();
        Assert.Empty(inventory["items"]!.AsArray());
        Assert.Null(inventory["equipment"]!["mainHand"]);
        var index = MortalItemIdentityState.Parse(
            (await _fs.ReadFileAsync(MortalItemIdentityState.StatePath))!);
        var entry = index.EntriesByItemId["itm_console_blade"];
        Assert.Equal("destroyed", entry["state"]!.GetValue<string>());
        Assert.Null(entry["currentCarrier"]);
        Assert.Equal("destroy", entry["transitions"]!.AsArray()[^1]!["kind"]!.GetValue<string>());
    }

    [Fact]
    public async Task TryProcessCommand_InventorySplit_CreatesDerivedReceiptAndIndexEntry()
    {
        await SeedMortalStateAsync();
        var stack = CreateCanonicalManagementItem("itm_console_stack", "Лунная трава", count: 5);
        var parentReceipt = stack["materializationReceipt"]!.DeepClone();
        await SeedCanonicalManagementInventoryAsync(new[] { stack }, new JsonObject());
        _console.QueueSelection("Действие", "✂ Разделить стопку");
        await _stateManager.RefreshGameStateAsync();

        var exception = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/инв"));

        Assert.Null(exception);
        AssertNoHiddenExplorerErrors("inventory_split_identity");
        var inventory = JsonNode.Parse(
            (await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath))!)!.AsObject();
        var items = inventory["items"]!.AsArray().OfType<JsonObject>().ToArray();
        Assert.Equal(2, items.Length);
        var parent = Assert.Single(items, item =>
            item["itemId"]!.GetValue<string>() == "itm_console_stack");
        var child = Assert.Single(items, item =>
            item["itemId"]!.GetValue<string>() != "itm_console_stack");
        Assert.Equal(4, parent["count"]!.GetValue<int>());
        Assert.Equal(1, child["count"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(parentReceipt, parent["materializationReceipt"]));
        Assert.Equal("split_derived", child["materializationReceipt"]!["instanceKind"]!.GetValue<string>());
        Assert.StartsWith("itm_", child["itemId"]!.GetValue<string>(), StringComparison.Ordinal);
        var index = MortalItemIdentityState.Parse(
            (await _fs.ReadFileAsync(MortalItemIdentityState.StatePath))!);
        Assert.Equal(2, index.EntriesByItemId.Count);
        Assert.Equal("split", index.EntriesByItemId[child["itemId"]!.GetValue<string>()]
            ["transitions"]!.AsArray()[^1]!["kind"]!.GetValue<string>());
    }

    [Fact]
    public async Task TryProcessCommand_InventoryMerge_KeepsSelectedIdentityAndRetiresContributor()
    {
        await SeedMortalStateAsync();
        var survivor = CreateCanonicalManagementItem("itm_console_merge_a", "Лунная трава", count: 2);
        var contributor = CreateCanonicalManagementItem("itm_console_merge_b", "Лунная трава", count: 3);
        var survivorReceipt = survivor["materializationReceipt"]!.DeepClone();
        await SeedCanonicalManagementInventoryAsync(
            new[] { survivor, contributor },
            new JsonObject());
        _console.QueueSelection("Действие", "📚 Сложить с другим предметом");
        await _stateManager.RefreshGameStateAsync();

        var exception = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/инв"));

        Assert.Null(exception);
        AssertNoHiddenExplorerErrors("inventory_merge_identity");
        var inventory = JsonNode.Parse(
            (await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath))!)!.AsObject();
        var merged = Assert.Single(inventory["items"]!.AsArray())!.AsObject();
        Assert.Equal("itm_console_merge_a", merged["itemId"]!.GetValue<string>());
        Assert.Equal(5, merged["count"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(survivorReceipt, merged["materializationReceipt"]));
        var index = MortalItemIdentityState.Parse(
            (await _fs.ReadFileAsync(MortalItemIdentityState.StatePath))!);
        Assert.Equal("active", index.EntriesByItemId["itm_console_merge_a"]["state"]!.GetValue<string>());
        Assert.Equal("merged", index.EntriesByItemId["itm_console_merge_b"]["state"]!.GetValue<string>());
        Assert.Equal("itm_console_merge_a",
            index.EntriesByItemId["itm_console_merge_b"]["mergedIntoItemId"]!.GetValue<string>());
    }

    private async Task SeedCanonicalManagementInventoryAsync(
        IReadOnlyList<JsonObject> items,
        JsonObject equipment)
    {
        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(items.Select(item => (JsonNode?)item.DeepClone()).ToArray()),
                ["equipment"] = equipment
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync(
            MortalItemIdentityState.StatePath,
            MortalItemTestFixture.CreateIndex(items.ToArray())
                .ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private static JsonObject CreateCanonicalManagementItem(
        string itemId,
        string name,
        int count,
        string? equipmentSlot = null)
    {
        var item = MortalItemTestFixture.CreateRawRoot(
            creationRef: $"new_item_{itemId}",
            materializationId: $"mat_item_{itemId}");
        item["name"] = name;
        item["description"] = $"Тестовый предмет «{name}».";
        item["type"] = equipmentSlot == null ? "material" : "weapon";
        item["count"] = count;
        item["equipmentSlot"] = equipmentSlot;
        if (equipmentSlot != null)
        {
            item["materialization"]!["sections"]!["equipment"] = new JsonObject
            {
                ["state"] = "populated",
                ["reason"] = null
            };
        }
        var receipt = MortalItemIdentityState.CreateRootReceipt(item, itemId, acceptedTurn: 42);
        item["itemId"] = itemId;
        item["existedId"] = itemId;
        item.Remove("creationRef");
        item["materializationReceipt"] = receipt;
        return item;
    }

    private async Task SeedRichNpcDrilldownStateAsync()
    {
        await SeedMortalStateAsync();
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_serafina",
              "name": "Серафина",
              "shortDescription": "Архивариус северных ворот.",
              "currentLocation": "Северные ворота",
              "relationshipLevel": 42,
              "personalQuests": [
                {
                  "questId": "quest_serafina_letter",
                  "questName": "Сделка на рассвете",
                  "status": "Active",
                  "description": "Серафина просит передать письмо без лишних свидетелей.",
                  "objectives": [
                    { "description": "Доставить письмо в архив", "status": "Active" }
                  ],
                  "rewards": "Получить ключ от боковой двери",
                  "failureConsequences": "Провал закроет путь через северные ворота"
                }
              ],
              "currentActivity": {
                "activityName": "Проверка печатей",
                "description": "Проверяет печати у северных ворот",
                "timeSpentMinutes": 30,
                "totalTimeCostMinutes": 60
              }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_journals.json", """
        {
          "NPCJournals": [
            {
              "NPCId": "npc_serafina",
              "NPCName": "Серафина",
              "lastJournalNote": "Сомневается, стоит ли доверять письму.",
              "journalEntries": [
                {
                  "event": "Первый разговор",
                  "description": "Заметила осторожность игрока.",
                  "relationshipChange": "+2"
                },
                {
                  "event": "Письмо найдено",
                  "description": "Сомневается, стоит ли доверять письму.",
                  "relationshipChange": "+1"
                }
              ]
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_activities.json", """
        {
          "entries": [
            {
              "NPCId": "npc_serafina",
              "activityUpdate": {
                "activityName": "Проверка печатей",
                "description": "Проверяет печати у северных ворот",
                "activeState": "active",
                "timeSpentMinutes": 30,
                "totalTimeCostMinutes": 60
              }
            }
          ]
        }
        """);
    }

}
