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
        var ring = CreateAcceptedConsoleItemFromJson(
            "ring_001",
            """{"name":"Перстень дома Вальмонт","type":"Кольцо","count":1,"equipmentSlot":"Finger1"}""",
            "equipment");
        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(ring),
                ["equippedItems"] = new JsonObject()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
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
        var ring = CreateAcceptedConsoleItemFromJson(
            "ring_bracket_001",
            """{"name":"Перстень [debug]","type":"Кольцо","count":1,"equipmentSlot":"Finger1"}""",
            "equipment");
        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["resources"] = new JsonObject
                {
                    ["осколки [debug]"] = "3 [card_alpha, card_beta]"
                },
                ["items"] = new JsonArray(ring),
                ["equippedItems"] = new JsonObject
                {
                    ["Finger1"] = "ring_bracket_001"
                }
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
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
    public async Task TryProcessCommand_InventoryStorageCount_UsesAcceptedItemsOnly()
    {
        await SeedMortalStateAsync();
        var accepted = CreateAcceptedConsoleItemFromJson(
            "stored_accepted_001",
            """{"name":"Принятая карта","type":"document","count":1}""");
        var carried = CreateAcceptedConsoleItemFromJson(
            "carried_storage_counter_001",
            """{"name":"Ключ от сундука","type":"tool","count":1}""");
        var rejected = MortalItemTestFixture.CreateRawRoot(
            creationRef: "new_item_rejected_storage_count",
            materializationId: "mat_item_rejected_storage_count");
        rejected["name"] = "НЕПРИНЯТЫЙ СЧЁТЧИК ХРАНИЛИЩА";

        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(carried),
                ["equippedItems"] = new JsonObject()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        await _fs.WriteFileAtomicAsync(
            "game_state/world/current_location.json",
            new JsonObject
            {
                ["name"] = "Тестовая площадь",
                ["locationStorages"] = new JsonArray(
                    new JsonObject
                    {
                        ["storageId"] = "storage_count_001",
                        ["name"] = "Сундук счётчика",
                        ["hasFullAccess"] = true,
                        ["contents"] = new JsonArray(accepted, rejected)
                    })
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/инв"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("inventory_storage_accepted_count");
        var choices = _console.SelectionChoicesHistory
            .Where(entry => entry.Title.Contains("Инвентарь", StringComparison.OrdinalIgnoreCase))
            .SelectMany(entry => entry.Choices)
            .ToArray();
        Assert.Contains(choices, choice => choice.Contains("Сундук счётчика (1 пр.)", StringComparison.Ordinal));
        Assert.DoesNotContain(choices, choice => choice.Contains("Сундук счётчика (2 пр.)", StringComparison.Ordinal));
        Assert.DoesNotContain(choices, choice => choice.Contains("НЕПРИНЯТЫЙ СЧЁТЧИК", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TryProcessCommand_InventoryDetail_UnresolvedMechanicalSummaryShowsReason()
    {
        await SeedMortalStateAsync();
        var glove = CreateAcceptedConsoleItemFromJson(
            "sealed_glove_1",
            """
            {
              "name":"Запечатанная перчатка",
              "description":"Руны на коже перчатки закрыты тусклой печатью.",
              "type":"Перчатки",
              "count":1,
              "bonuses":["Аркановедение +1"],
              "mechanicalSummaryAuthority":"Unresolved",
              "mechanicalSummaryUnresolvedReason":"Руны запечатаны, эффект станет ясен после ритуала распознавания."
            }
            """,
            "mechanics");
        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(glove),
                ["equippedItems"] = new JsonObject()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
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
        var glove = CreateAcceptedConsoleItemFromJson(
            "runic_glove_1",
            """
            {
              "name":"Руническая перчатка",
              "description":"Узоры мерцают тусклым золотом.",
              "type":"Артефакт",
              "count":1,
              "structuredBonuses":[
                {
                  "targetType":"skill",
                  "skill":"Чувство магических потоков",
                  "valueType":"Flat",
                  "value":2,
                  "source":"Руническая перчатка",
                  "summary":"Чувство магических потоков +2",
                  "stackingRule":"replace [debug]",
                  "experimentalKey":"raw [value]"
                }
              ]
            }
            """,
            "mechanics");
        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(glove),
                ["equippedItems"] = new JsonObject()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
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
    public async Task TryProcessCommand_InventoryDetail_HidesMortalItemAuthorityAndPreservesSemanticCatchAll()
    {
        await SeedMortalStateAsync();
        var item = MortalItemTestFixture.CreateCanonicalRoot("itm_projection_console");
        item["name"] = "Кузнечный молот северной артели";
        item["description"] = "На бойке осталась узнаваемая насечка мастера.";
        item["type"] = "tool";
        item["volume"] = 7.25;
        item["isConsumption"] = true;
        item["unreadableReason"] = "Текст закрыт соляной коркой.";
        item["sealedReason"] = "Печать северной артели не снята.";
        item["lockedReason"] = "Замок отвечает только владельцу.";
        item["makerTradition"] = "Клеймо северной артели";
        item["route"] = "PRIVATE_CONSOLE_ROOT_ROUTE";
        item["requestId"] = "PRIVATE_CONSOLE_ROOT_REQUEST_ID";
        item["slotId"] = "PRIVATE_CONSOLE_ROOT_SLOT_ID";
        item["tradeCycleId"] = "PRIVATE_CONSOLE_ROOT_TRADE_CYCLE_ID";
        item["rewardId"] = "PRIVATE_CONSOLE_ROOT_REWARD_ID";
        item["UpdateInventory"] = new JsonArray("PRIVATE_CONSOLE_ROOT_UPDATE_INVENTORY");
        item["artisanSignals"] = new JsonArray(
            "Северный рунный узор",
            new JsonObject
            {
                ["label"] = "Метка мастера",
                ["receiptId"] = "PRIVATE_CONSOLE_ARRAY_RECEIPT"
            });
        item["structuredBonuses"] = new JsonArray(
            new JsonObject
            {
                ["summary"] = "Точный хват",
                ["experimentalKey"] = "Семантическое поле бонуса",
                ["creationRef"] = "PRIVATE_CONSOLE_NESTED_CREATION_REF",
                ["receiptId"] = "PRIVATE_CONSOLE_NESTED_RECEIPT",
                ["lineage"] = "PRIVATE_CONSOLE_NESTED_LINEAGE",
                ["currentCarrier"] = "PRIVATE_CONSOLE_NESTED_CARRIER",
                ["carrierPath"] = "PRIVATE_CONSOLE_NESTED_PATH",
                ["sourceAuthority"] = "PRIVATE_CONSOLE_NESTED_SOURCE_AUTHORITY",
                ["sourceTurn"] = "PRIVATE_CONSOLE_NESTED_SOURCE_TURN",
                ["repairPacket"] = "PRIVATE_CONSOLE_NESTED_REPAIR",
                ["validationCode"] = "PRIVATE_CONSOLE_NESTED_VALIDATION",
                ["requestId"] = "PRIVATE_CONSOLE_NESTED_REQUEST_ID",
                ["slotId"] = "PRIVATE_CONSOLE_NESTED_SLOT_ID",
                ["tradeCycleId"] = "PRIVATE_CONSOLE_NESTED_TRADE_CYCLE_ID",
                ["rewardId"] = "PRIVATE_CONSOLE_NESTED_REWARD_ID",
                ["condition"] = new JsonObject
                {
                    ["trigger"] = "При работе с рунами",
                    ["receiptSeal"] = "PRIVATE_CONSOLE_NESTED_SEAL",
                    ["image_prompt"] = "PRIVATE_CONSOLE_NESTED_IMAGE_PROMPT",
                    ["itemCreationRef"] = "PRIVATE_CONSOLE_ITEM_CREATION_REF",
                    ["itemRef"] = "PRIVATE_CONSOLE_ITEM_REF",
                    ["sourceItemId"] = "PRIVATE_CONSOLE_SOURCE_ITEM_ID",
                    ["targetItemId"] = "PRIVATE_CONSOLE_TARGET_ITEM_ID",
                    ["parentItemId"] = "PRIVATE_CONSOLE_PARENT_ITEM_ID",
                    ["containerItemId"] = "PRIVATE_CONSOLE_CONTAINER_ITEM_ID",
                    ["rewardItemId"] = "PRIVATE_CONSOLE_REWARD_ITEM_ID",
                    ["destinationItemId"] = "PRIVATE_CONSOLE_DESTINATION_ITEM_ID",
                    ["resultItemId"] = "PRIVATE_CONSOLE_RESULT_ITEM_ID",
                    ["removedItemId"] = "PRIVATE_CONSOLE_REMOVED_ITEM_ID",
                    ["destinationContainerId"] = "PRIVATE_CONSOLE_DESTINATION_CONTAINER_ID",
                    ["currentContentsPath"] = "PRIVATE_CONSOLE_CURRENT_CONTENTS_PATH",
                    ["itemIds"] = new JsonArray("PRIVATE_CONSOLE_ITEM_IDS"),
                    ["targetItemIds"] = new JsonArray("PRIVATE_CONSOLE_TARGET_ITEM_IDS"),
                    ["UpdateInventory"] = new JsonArray("PRIVATE_CONSOLE_NESTED_UPDATE_INVENTORY"),
                    ["NPCInventoryAdds"] = new JsonArray("PRIVATE_CONSOLE_NESTED_NPC_INVENTORY_ADDS"),
                    ["UpdateNpcTradeInventoryReceipts"] = new JsonArray("PRIVATE_CONSOLE_NESTED_TRADE_RECEIPTS"),
                    ["lootForCurrentTurn"] = new JsonArray("PRIVATE_CONSOLE_NESTED_LOOT"),
                    ["removeInventoryItems"] = new JsonArray("PRIVATE_CONSOLE_NESTED_REMOVE_INVENTORY"),
                    ["NPCInventoryRemovals"] = new JsonArray("PRIVATE_CONSOLE_NESTED_NPC_REMOVALS")
                }
            });
        item["customProperties"] = new JsonArray(
            new JsonObject
            {
                ["interactionType"] = "onUse",
                ["targetStateName"] = "кузнечная руна",
                ["changeValue"] = "+1",
                ["description"] = "Руна отвечает владельцу.",
                ["ritualPattern"] = "Три удара пробуждают память молота.",
                ["condition"] = new JsonObject
                {
                    ["weatherRule"] = "В грозу руна звучит громче.",
                    ["requestId"] = "PRIVATE_CONSOLE_CUSTOM_PROPERTY_REQUEST",
                    ["ritualGuidance"] = new JsonObject
                    {
                        ["title"] = "Памятка кузнеца",
                        ["steps"] = new JsonArray("Ударить по наковальне трижды")
                    },
                    ["repairDebug"] = new JsonObject
                    {
                        ["kind"] = "mortal_item_materialization_repair",
                        ["priority"] = "critical",
                        ["title"] = "Служебное задание ремонта предмета",
                        ["steps"] = new JsonArray("Открыть validation_repair_request.json"),
                        ["doNotDo"] = new JsonArray("Не изменять item_identity_index.json"),
                        ["expectedAuthority"] = "Внутреннее служебное поле ремонта: expectedAuthority",
                        ["actualEvidence"] = "Внутреннее служебное поле ремонта: actualEvidence",
                        ["targetFiles"] = new JsonArray("Внутреннее служебное поле ремонта: targetFiles"),
                        ["canonicalActorNames"] = new JsonArray("Внутреннее служебное поле ремонта: canonicalActorNames"),
                        ["missingFields"] = new JsonArray("Внутреннее служебное поле ремонта: missingFields"),
                        ["exactFieldCorrections"] = new JsonArray("Внутреннее служебное поле ремонта: exactFieldCorrections"),
                        ["requiredCompanionTargets"] = new JsonArray("Внутреннее служебное поле ремонта: requiredCompanionTargets"),
                        ["templateRefs"] = new JsonArray("Внутреннее служебное поле ремонта: templateRefs"),
                        ["expectedShape"] = "Внутреннее служебное поле ремонта: expectedShape",
                        ["safeCorrectionRules"] = new JsonArray("Внутреннее служебное поле ремонта: safeCorrectionRules"),
                        ["transitionClass"] = "Внутреннее служебное поле ремонта: transitionClass",
                        ["repairHint"] = "Внутреннее служебное поле ремонта: repairHint"
                    }
                }
            },
            new JsonObject
            {
                ["interactionType"] = "onEquip",
                ["target"] = "запасной руне",
                ["summary"] = "Знак ждёт подходящего часа."
            },
            new JsonObject
            {
                ["kind"] = "mortal_item_materialization_repair",
                ["priority"] = "critical",
                ["title"] = "Прямой служебный пакет в свойствах предмета",
                ["targetFiles"] = new JsonArray("game_state/inventory/items.json"),
                ["expectedAuthority"] = new JsonArray("receipt"),
                ["actualEvidence"] = new JsonArray("creationRef"),
                ["steps"] = new JsonArray("Открыть validation_repair_request.json")
            });
        item["combatEffect"] = new JsonArray(
            new JsonObject
            {
                ["isActivatedEffect"] = true,
                ["actionName"] = "Зеркальный заслон",
                ["actionCost"] = "Main",
                ["targetPriority"] = "enemy",
                ["scalingCharacteristic"] = "strength",
                ["effects"] = new JsonArray(
                    new JsonObject
                    {
                        ["effectType"] = "DamageReduction",
                        ["value"] = "15%",
                        ["targetType"] = "self",
                        ["targetsCount"] = 3,
                        ["duration"] = 2,
                        ["damageThreshold"] = 11,
                        ["effectDescription"] = "Молот поднимает зеркальный заслон."
                    })
            },
            new JsonObject
            {
                ["actionName"] = "Действие без указанного режима",
                ["effects"] = new JsonArray()
            });
        item["ownerBondLevelCurrent"] = 12;
        item["ownerBondLevelMax"] = 80;
        item["quality"] = "Rare";
        item["rarity"] = "Rare";
        var lockedFateCard = MortalItemTestFixture.CreateItemFateCard(
            "card_northern_artel_sign",
            "Знак северной артели",
            isUnlocked: false,
            unlockConditions: new JsonObject
            {
                ["ownerBondLevel"] = 35,
                ["requiredMaterials"] = new JsonArray(
                    new JsonObject
                    {
                        ["materialName"] = "Звёздная пыль",
                        ["quantity"] = 2,
                        ["receiptId"] = "PRIVATE_CONSOLE_FATE_MATERIAL_RECEIPT"
                    }),
                ["plotConditionDescription"] = "Завершить работу в северной кузнице",
                ["conjunction"] = "AND"
            },
            rewards: new JsonObject
            {
                ["description"] = "Молот узнает завершённый знак мастера."
            },
            description: "Молот узнаёт работу своего мастера.",
            imagePrompt: "northern artisan sigil glowing on a forge hammer");
        var unlockedFateCard = MortalItemTestFixture.CreateItemFateCard(
            "card_northern_artel_legacy",
            "Наследие северной артели",
            isUnlocked: true,
            rewards: new JsonObject
            {
                ["description"] = "Открывает тайную технику рунного удара.",
                ["improvedBonuses"] = new JsonArray("Точный хват усиливается до +3"),
                ["newCombatEffects"] = new JsonArray(
                    new JsonObject
                    {
                        ["isActivatedEffect"] = true,
                        ["actionName"] = "Рунный отклик молота",
                        ["actionCost"] = "Fast",
                        ["targetPriority"] = "enemy",
                        ["scalingCharacteristic"] = "dexterity",
                        ["effects"] = new JsonArray(
                            new JsonObject
                            {
                                ["effectType"] = "Damage",
                                ["value"] = "10%",
                                ["targetType"] = "enemy",
                                ["targetTypeDisplayName"] = "заклеймённая цель",
                                ["targetsCount"] = 2,
                                ["duration"] = 2,
                                ["poiseDamage"] = "4%",
                                ["effectDescription"] = "Рунный импульс поражает цель",
                                ["currentCarrier"] = "PRIVATE_CONSOLE_FATE_COMBAT_CARRIER"
                            },
                            new JsonObject
                            {
                                ["effectType"] = "DamageReduction",
                                ["value"] = "8%",
                                ["targetType"] = "self",
                                ["targetsCount"] = 1,
                                ["duration"] = 2,
                                ["damageThreshold"] = 9,
                                ["effectDescription"] = "Рунный заслон смягчает удар"
                            }),
                    },
                    new JsonObject
                    {
                        ["isActivatedEffect"] = false,
                        ["actionName"] = "Память рунного хвата",
                        ["targetPriority"] = "self",
                        ["scalingCharacteristic"] = "wisdom",
                        ["effects"] = new JsonArray(
                            new JsonObject
                            {
                                ["effectType"] = "Buff",
                                ["value"] = "5%",
                                ["targetType"] = "self",
                                ["targetsCount"] = 1,
                                ["duration"] = 3,
                                ["effectDescription"] = "Руны направляют руку владельца"
                            })
                    }),
                ["statBoostsToItemItself"] = new JsonArray("+10% к максимальной прочности"),
                ["changesDescriptionTo"] = "На бойке проступили завершённые руны.",
                ["changesImagePromptTo"] = "forge hammer with completed glowing runes",
                ["otherNarrativeChanges"] = "Мастера северной артели узнают владельца молота.",
                ["repairPacket"] = "PRIVATE_CONSOLE_FATE_REWARD_REPAIR"
            },
            description: "Завершённая карта наследия северных мастеров.",
            imagePrompt: "completed northern artisan sigil on a forge hammer");
        item["fateCards"] = new JsonArray(lockedFateCard);
        item["questLinks"] = new JsonArray(
            new JsonObject
            {
                ["questName"] = "Наследие северной артели",
                ["role"] = "ключ к кузнице",
                ["stage"] = "после ритуала",
                ["condition"] = new JsonObject
                {
                    ["weather"] = "гроза над северной кузницей",
                    ["requestId"] = "PRIVATE_CONSOLE_QUEST_LINK_REQUEST"
                }
            });
        item["materialization"]!["sections"]!["mechanics"] = new JsonObject
        {
            ["state"] = "populated",
            ["reason"] = null
        };
        item["materialization"]!["sections"]!["bondsAndFateCards"] = new JsonObject
        {
            ["state"] = "populated",
            ["reason"] = null
        };
        item["materialization"]!["sections"]!["questRole"] = new JsonObject
        {
            ["state"] = "populated",
            ["reason"] = null
        };
        item["materialization"]!["sections"]!["consumption"] = new JsonObject
        {
            ["state"] = "populated",
            ["reason"] = null
        };
        item["materialization"]!["sections"]!["readableOrSentient"] = new JsonObject
        {
            ["state"] = "populated",
            ["reason"] = null
        };
        MortalItemTestFixture.ResealCanonical(item);
        using (var fixtureDocument = JsonDocument.Parse(item.ToJsonString()))
        {
            Assert.Empty(MortalItemMaterializationContract.Validate(
                fixtureDocument.RootElement,
                "console projection fixture",
                MortalItemMaterializationPhase.CanonicalPostSeal));
        }

        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(item),
                ["equippedItems"] = new JsonObject()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync(
            "game_state/inventory/item_bonds.json",
            new JsonObject
            {
                ["entries"] = new JsonArray(
                    new JsonObject
                    {
                        ["itemId"] = "itm_projection_console",
                        ["existedId"] = "itm_projection_console_other",
                        ["ownerBondLevelCurrent"] = 99,
                        ["ownerBondLevelMax"] = 99,
                        ["lastBondChangeReason"] = "CONFLICTING_CONSOLE_BOND_REASON"
                    },
                    new JsonObject
                    {
                        ["itemId"] = "ITM_PROJECTION_CONSOLE",
                        ["itemName"] = "Кузнечный молот северной артели",
                        ["ownerBondLevelCurrent"] = 12,
                        ["ownerBondLevelMax"] = 80,
                        ["lastBondChangeReason"] = "WRONG_CASE_CONSOLE_BOND_REASON",
                        ["fateCards"] = new JsonArray(
                            MortalItemTestFixture.CreateItemFateCard(
                                "card_wrong_case_console",
                                "WRONG_CASE_CONSOLE_FATE_CARD",
                                isUnlocked: true))
                    },
                    new JsonObject
                    {
                        ["itemId"] = "itm_projection_console",
                        ["itemName"] = "Кузнечный молот северной артели",
                        ["ownerBondLevelCurrent"] = 12,
                        ["ownerBondLevelMax"] = 80,
                        ["lastBondChangeReason"] = "EXACT_CONSOLE_BOND_REASON",
                        ["fateCards"] = new JsonArray(
                            lockedFateCard.DeepClone(),
                            unlockedFateCard)
                    }),
                ["itemBondLevelChanges"] = new JsonArray(
                    new JsonObject
                    {
                        ["itemName"] = "Кузнечный молот северной артели",
                        ["lastBondChangeReason"] = "RAW_NAME_CONSOLE_BOND_REASON"
                    })
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync(
            "game_state/inventory/item_resources.json",
            new JsonObject
            {
                ["entries"] = new JsonArray(
                    new JsonObject
                    {
                        ["itemId"] = "itm_projection_console",
                        ["resource"] = "DUPLICATE_CONSOLE_RESOURCE_FIRST",
                        ["maximumResource"] = 5,
                        ["resourceType"] = "заряды"
                    },
                    new JsonObject
                    {
                        ["itemId"] = "itm_projection_console",
                        ["resource"] = "DUPLICATE_CONSOLE_RESOURCE_SECOND",
                        ["maximumResource"] = 7,
                        ["resourceType"] = "заряды"
                    })
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync(
            "game_state/inventory/item_text_updates.json",
            new JsonObject
            {
                ["updateItemTextContents"] = new JsonArray(
                    new JsonObject
                    {
                        ["itemName"] = "Кузнечный молот северной артели",
                        ["textContent"] = new JsonArray("RAW_NAME_CONSOLE_TEXT_MARKER")
                    })
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _stateManager.RefreshGameStateAsync();

        var exception = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/инв"));

        Assert.Null(exception);
        AssertNoHiddenExplorerErrors("inventory_item_materialization_privacy");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Кузнечный молот северной артели", renderedText, StringComparison.Ordinal);
        Assert.Contains("7.25 дм³", renderedText, StringComparison.Ordinal);
        Assert.Contains("Расходуемый предмет", renderedText, StringComparison.Ordinal);
        Assert.Contains("Текст закрыт соляной коркой.", renderedText, StringComparison.Ordinal);
        Assert.Contains("Печать северной артели не снята.", renderedText, StringComparison.Ordinal);
        Assert.Contains("Замок отвечает только владельцу.", renderedText, StringComparison.Ordinal);
        Assert.Contains("Клеймо северной артели", renderedText, StringComparison.Ordinal);
        Assert.Contains("Северный рунный узор", renderedText, StringComparison.Ordinal);
        Assert.Contains("Метка мастера", renderedText, StringComparison.Ordinal);
        Assert.Contains("Семантическое поле бонуса", renderedText, StringComparison.Ordinal);
        Assert.Contains("При работе с рунами", renderedText, StringComparison.Ordinal);
        Assert.Contains("Три удара пробуждают память молота", renderedText, StringComparison.Ordinal);
        Assert.Contains("В грозу руна звучит громче", renderedText, StringComparison.Ordinal);
        Assert.Contains("Памятка кузнеца", renderedText, StringComparison.Ordinal);
        Assert.Contains("Ударить по наковальне трижды", renderedText, StringComparison.Ordinal);
        Assert.Contains("запасной руне", renderedText, StringComparison.Ordinal);
        Assert.Contains("Знак ждёт подходящего часа", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("+ к запасной руне", renderedText, StringComparison.Ordinal);
        Assert.Contains("Зеркальный заслон", renderedText, StringComparison.Ordinal);
        Assert.Contains("Действие без указанного режима", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Действие без указанного режима (пассивный)", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("приоритет цели: противник", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("масштабирование: Сила", renderedText, StringComparison.Ordinal);
        Assert.Contains("×3 целей", renderedText, StringComparison.Ordinal);
        Assert.Contains("порог: 11", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("12/80", renderedText, StringComparison.Ordinal);
        Assert.Contains("EXACT_CONSOLE_BOND_REASON", renderedText, StringComparison.Ordinal);
        Assert.Contains("Знак северной артели", renderedText, StringComparison.Ordinal);
        Assert.Contains("2× Звёздная пыль", renderedText, StringComparison.Ordinal);
        Assert.Contains("Завершить работу в северной кузнице", renderedText, StringComparison.Ordinal);
        Assert.Contains("Точный хват усиливается до +3", renderedText, StringComparison.Ordinal);
        Assert.Contains("Рунный отклик молота", renderedText, StringComparison.Ordinal);
        Assert.Contains("активируемый", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("стоимость: быстрое действие", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("приоритет цели: противник", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("масштабирование: Ловкость", renderedText, StringComparison.Ordinal);
        Assert.Contains("Рунный импульс поражает цель (10%)", renderedText, StringComparison.Ordinal);
        Assert.Contains("цель: заклеймённая цель", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("целей: 2", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("длительность: 2", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("урон равновесию: 4%", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Рунный заслон смягчает удар (8%)", renderedText, StringComparison.Ordinal);
        Assert.Contains("порог урона: 9", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Память рунного хвата", renderedText, StringComparison.Ordinal);
        Assert.Contains("пассивный", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Руны направляют руку владельца (5%)", renderedText, StringComparison.Ordinal);
        Assert.Contains("+10% к максимальной прочности", renderedText, StringComparison.Ordinal);
        Assert.Contains("На бойке проступили завершённые руны.", renderedText, StringComparison.Ordinal);
        Assert.Contains("Облик предмета изменится", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("forge hammer with completed glowing runes", renderedText, StringComparison.Ordinal);
        Assert.Contains("Мастера северной артели узнают владельца молота.", renderedText, StringComparison.Ordinal);
        Assert.Contains("Наследие северной артели", renderedText, StringComparison.Ordinal);
        Assert.Contains("после ритуала", renderedText, StringComparison.Ordinal);
        Assert.Contains("гроза над северной кузницей", renderedText, StringComparison.Ordinal);
        Assert.Equal(
            1,
            renderedText.Split("Знак северной артели", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("WRONG_CASE_CONSOLE_BOND_REASON", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("CONFLICTING_CONSOLE_BOND_REASON", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("RAW_NAME_CONSOLE_", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("DUPLICATE_CONSOLE_RESOURCE_FIRST", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("DUPLICATE_CONSOLE_RESOURCE_SECOND", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_CONSOLE_", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Внутреннее служебное поле ремонта", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Служебное задание ремонта предмета", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Прямой служебный пакет в свойствах предмета", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Открыть validation_repair_request.json", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Не изменять item_identity_index.json", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("materializationReceipt", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("materializationId", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("parentItemIds", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("item_identity_index.json", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mortal_item_materialization_repair", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Inventory_DoesNotProjectUnacceptedSameFileCandidate()
    {
        await SeedMortalStateAsync();
        var acceptedItem = MortalItemTestFixture.CreateCanonicalRoot("itm_accepted_projection_console");
        acceptedItem["name"] = "Принятый клинок дозорного";
        MortalItemTestFixture.ResealCanonical(acceptedItem);
        var rawItem = MortalItemTestFixture.CreateRawRoot(
            creationRef: "new_item_unaccepted_projection",
            materializationId: "mat_item_unaccepted_projection");
        rawItem["name"] = "НЕПРИНЯТЫЙ ПРЕДМЕТ ИЗ РЕМОНТА";

        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(acceptedItem, rawItem.DeepClone()),
                ["UpdateInventory"] = new JsonArray(rawItem),
                ["equipment"] = new JsonObject
                {
                    ["mainHand"] = new JsonObject
                    {
                        ["creationRef"] = "new_item_unaccepted_projection",
                        ["name"] = "Принятый клинок дозорного"
                    },
                    ["offHand"] = "ITM_ACCEPTED_PROJECTION_CONSOLE"
                }
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _stateManager.RefreshGameStateAsync();

        var exception = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/инв"));

        Assert.Null(exception);
        AssertNoHiddenExplorerErrors("inventory_unaccepted_candidate_privacy");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Принятый клинок дозорного", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("(экипировано)", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("НЕПРИНЯТЫЙ ПРЕДМЕТ", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("new_item_unaccepted_projection", renderedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_Inventory_SameNameItemsUseExactIdentityForEquipmentActions()
    {
        await SeedMortalStateAsync();
        var equipped = CreateCanonicalManagementItem(
            "itm_console_twin_a",
            "Парный клинок",
            count: 1,
            equipmentSlot: "MainHand");
        var backpack = CreateCanonicalManagementItem(
            "itm_console_twin_b",
            "Парный клинок",
            count: 1,
            equipmentSlot: "MainHand");
        await SeedCanonicalManagementInventoryAsync(
            new[] { equipped, backpack },
            new JsonObject { ["MainHand"] = "itm_console_twin_a" });
        await _stateManager.RefreshGameStateAsync();

        var firstPass = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/инв"));
        Assert.Null(firstPass);
        var backpackChoice = _console.SelectionChoicesHistory
            .Where(static entry => entry.Title.Contains("Инвентарь", StringComparison.OrdinalIgnoreCase))
            .SelectMany(static entry => entry.Choices)
            .First(static choice => choice.Contains("(вариант 2)", StringComparison.Ordinal));
        Assert.DoesNotContain("id=", backpackChoice, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("itm_console_twin", backpackChoice, StringComparison.Ordinal);
        _console.QueueSelection("Инвентарь", backpackChoice);

        var secondPass = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/инв"));

        Assert.Null(secondPass);
        AssertNoHiddenExplorerErrors("inventory_same_name_exact_equipment_identity");
        var actionPrompt = _console.SelectionChoicesHistory.Last(
            static entry => entry.Title.Contains("Действие", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("⚔ Экипировать", actionPrompt.Choices);
        Assert.DoesNotContain(
            actionPrompt.Choices,
            static choice => choice.Contains("Снять", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryProcessCommand_Inventory_InvalidEquipmentMapDoesNotProjectSiblingSlot()
    {
        await SeedMortalStateAsync();
        var blade = CreateCanonicalManagementItem(
            "itm_console_atomic_equipment",
            "Клинок атомарной экипировки",
            count: 1,
            equipmentSlot: "MainHand");
        await SeedCanonicalManagementInventoryAsync(
            new[] { blade },
            new JsonObject
            {
                ["MainHand"] = "itm_console_atomic_equipment",
                ["UnknownSlot"] = null
            });
        await _stateManager.RefreshGameStateAsync();

        var exception = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/инв"));

        Assert.Null(exception);
        AssertNoHiddenExplorerErrors("inventory_atomic_equipment_projection");
        var inventoryChoices = _console.SelectionChoicesHistory
            .Where(static entry => entry.Title.Contains("Инвентарь", StringComparison.OrdinalIgnoreCase))
            .SelectMany(static entry => entry.Choices)
            .ToArray();
        Assert.Contains(inventoryChoices, static choice =>
            choice.Contains("Клинок атомарной экипировки", StringComparison.Ordinal));
        Assert.DoesNotContain(inventoryChoices, static choice =>
            choice.StartsWith("⚔", StringComparison.Ordinal));
        Assert.DoesNotContain(
            _console.SelectionChoicesHistory
                .Where(static entry => entry.Title.Contains("Действие", StringComparison.OrdinalIgnoreCase))
                .SelectMany(static entry => entry.Choices),
            static choice => choice.Contains("Снять", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryProcessCommand_Inventory_ArraySlotsRenderPlayerFacingChoicesAndEquipAction()
    {
        await SeedMortalStateAsync();
        var blade = CreateAcceptedConsoleItemFromJson(
            "itm_console_versatile_blade",
            """
            {
              "name": "Клинок переменного хвата",
              "type": "weapon",
              "count": 1,
              "equipmentSlot": ["MainHand", "OffHand"],
              "accessoryForSlot": null,
              "requiresTwoHands": false
            }
            """,
            "equipment");
        await SeedCanonicalManagementInventoryAsync(
            new[] { blade },
            new JsonObject { ["MainHand"] = null, ["OffHand"] = null });
        await _stateManager.RefreshGameStateAsync();

        var exception = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/инв"));

        Assert.Null(exception);
        AssertNoHiddenExplorerErrors("inventory_array_slots_console");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Основная рука", renderedText, StringComparison.Ordinal);
        Assert.Contains("Вторая рука", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"MainHand\"", renderedText, StringComparison.Ordinal);
        var actionPrompt = _console.SelectionChoicesHistory.Last(
            static entry => entry.Title.Contains("Действие", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("⚔ Экипировать", actionPrompt.Choices);
    }

    [Fact]
    public async Task TryProcessCommand_Inventory_AccessoryOffersOnlyFreeUniversalSlots()
    {
        await SeedMortalStateAsync();
        var bandolier = CreateAcceptedConsoleItemFromJson(
            "itm_console_archive_bandolier",
            """
            {
              "name": "Архивный бандольер",
              "type": "accessory",
              "count": 1,
              "equipmentSlot": null,
              "accessoryForSlot": ["Chest", "Back"],
              "requiresTwoHands": false
            }
            """,
            "equipment");
        var occupiedAccessory = MortalItemTestFixture.CreateCanonicalRoot("itm_console_accessory_occupant");
        occupiedAccessory["name"] = "Занятый амулетный футляр";
        MortalItemTestFixture.ResealCanonical(occupiedAccessory);
        await SeedCanonicalManagementInventoryAsync(
            new[] { bandolier, occupiedAccessory },
            new JsonObject
            {
                ["Accessory1"] = "itm_console_accessory_occupant",
                ["Accessory2"] = null,
                ["Accessory3"] = null,
                ["Accessory4"] = null
            });
        await _stateManager.RefreshGameStateAsync();

        var firstPass = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/инв"));
        Assert.Null(firstPass);
        var bandolierChoice = _console.SelectionChoicesHistory
            .Where(static entry => entry.Title.Contains("Инвентарь", StringComparison.OrdinalIgnoreCase))
            .SelectMany(static entry => entry.Choices)
            .First(static choice => choice.Contains("Архивный бандольер", StringComparison.Ordinal));
        _console.QueueSelection("Инвентарь", bandolierChoice);
        _console.QueueSelection("Действие", "⚔ Экипировать");
        _console.QueueSelection("В какой слот", "← Отмена");

        var exception = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/инв"));

        Assert.Null(exception);
        AssertNoHiddenExplorerErrors("inventory_accessory_slots_console");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Грудь", renderedText, StringComparison.Ordinal);
        Assert.Contains("Спина", renderedText, StringComparison.Ordinal);
        var slotPrompt = _console.SelectionChoicesHistory.Last(
            static entry => entry.Title.Contains("В какой слот", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(slotPrompt.Choices, static choice => choice.Contains("Accessory1", StringComparison.Ordinal));
        Assert.Contains(slotPrompt.Choices, static choice => choice.Contains("Accessory2", StringComparison.Ordinal));
        Assert.Contains(slotPrompt.Choices, static choice => choice.Contains("Accessory3", StringComparison.Ordinal));
        Assert.Contains(slotPrompt.Choices, static choice => choice.Contains("Accessory4", StringComparison.Ordinal));
        Assert.DoesNotContain(slotPrompt.Choices, static choice => choice.Contains("(Chest)", StringComparison.Ordinal));
        Assert.DoesNotContain(slotPrompt.Choices, static choice => choice.Contains("(Back)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TryProcessCommand_Npcs_ProjectsAcceptedInventoryAndIgnoresRejectedCandidates()
    {
        await SeedMortalStateAsync();
        var accepted = MortalItemTestFixture.CreateCanonicalRoot("itm_npc_projection_console");
        accepted["name"] = "Принятый посох архивариуса";
        MortalItemTestFixture.ResealCanonical(accepted);
        var pending = MortalItemTestFixture.CreateRawRoot(
            creationRef: "new_item_pending_npc_console",
            materializationId: "mat_item_pending_npc_console");
        pending["name"] = "UNACCEPTED_NPC_CONSOLE_MARKER";

        await _fs.WriteFileAtomicAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(
                    new JsonObject
                    {
                        ["npcId"] = "npc_projection_console",
                        ["name"] = "Архивариус Орна",
                        ["inventory"] = new JsonArray(accepted, pending.DeepClone()),
                        ["equippedItems"] = new JsonObject
                        {
                            ["MainHand"] = "itm_npc_projection_console",
                            ["UnknownSlot"] = "Непринятый предмет экипировки NPC"
                        }
                    })
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync(
            "game_state/npcs/npc_inventory.json",
            new JsonObject
            {
                ["NPCInventoryAdds"] = new JsonArray(
                    new JsonObject
                    {
                        ["NPCId"] = "npc_projection_console",
                        ["NPCName"] = "Архивариус Орна",
                        ["item"] = pending
                    })
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync(
            "game_state/npcs/item_journals.json",
            new JsonObject
            {
                ["entries"] = new JsonArray(
                    new JsonObject
                    {
                        ["itemId"] = "itm_npc_projection_console",
                        ["journalEntries"] = new JsonArray(
                            new JsonObject
                            {
                                ["event"] = "Скрытая запись NPC",
                                ["description"] = "NPC_ITEM_JOURNAL_SIDECAR_MARKER"
                            })
                    })
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        _console.QueueSelection("Действие", "📦 Осмотреть предметы", "← Назад", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var exception = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/нпс"));

        Assert.Null(exception);
        AssertNoHiddenExplorerErrors("npc_item_materialization_projection");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Принятый посох архивариуса", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("(экипировано)", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UNACCEPTED_NPC_CONSOLE_MARKER", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Непринятый предмет экипировки NPC", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("new_item_pending_npc_console", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("NPC_ITEM_JOURNAL_SIDECAR_MARKER", renderedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_InventoryQuestItemDetail_HidesRawBookkeepingAndLocalizesEnums()
    {
        await SeedMortalStateAsync();
        var bandage = CreateAcceptedConsoleItemFromJson(
            "seal_bandage_001",
            """
            {
              "name":"Запятнанная повязка с чёрной печатью",
              "description":"Срезанная с раненого льняная повязка.",
              "type":"QuestItem",
              "quality":"Common",
              "rarity":"Common",
              "count":1,
              "value":0,
              "weight":0.1,
              "durability":"100%",
              "equipmentSlot":"Accessory1",
              "group":"Стартовые зацепки",
              "textContent":["Чёрная печать похожа на знак запрещённого братства."],
              "currentLocationId":"loc_life_001_start",
              "currentLocationName":"Дом лекаря Вирента: задняя лечебница",
              "isCarried":false,
              "isEquipped":false,
              "visibility":"known"
            }
            """,
            "equipment",
            "readableOrSentient");
        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(bandage),
                ["equippedItems"] = new JsonObject()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
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
        var glove = CreateAcceptedConsoleItemFromJson(
            "glove_journal_anchor_001",
            """
            {
              "name":"Руническая перчатка",
              "description":"Кожа перчатки хранит слабый золотой отблеск.",
              "type":"Артефакт",
              "count":1,
              "equipmentSlot":null,
              "journalEntries":["#[4]. Предмет найден на столе у окна."]
            }
            """,
            "readableOrSentient");
        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(glove),
                ["equippedItems"] = new JsonObject()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
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
        var apple = CreateAcceptedConsoleItemFromJson(
            "item_apple_001",
            """{"name":"Яблоко","count":1}""");
        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["money"] = 10,
                ["items"] = new JsonArray(apple),
                ["equippedItems"] = new JsonObject()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync(
            MortalItemIdentityState.StatePath,
            MortalItemTestFixture.CreateIndex(apple).ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await WriteJsonAsync("game_state/world/current_location.json", new
        {
            locationId = "loc_storage_move_001",
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
        var rope = CreateAcceptedConsoleItemFromJson(
            "item_rope_001",
            """{"name":"Веревка","count":1}""");
        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(rope),
                ["equippedItems"] = new JsonObject()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync(
            MortalItemIdentityState.StatePath,
            MortalItemTestFixture.CreateIndex(rope).ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
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
            equipmentSlot: "MainHand");
        await SeedCanonicalManagementInventoryAsync(
            new[] { blade },
            new JsonObject { ["MainHand"] = "itm_console_blade" });
        _console.QueueSelection("Действие", "[red]🗑 Выбросить[/]");
        _console.QueueAnyConfirmResponse(true);
        await _stateManager.RefreshGameStateAsync();

        var exception = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/инв"));

        Assert.Null(exception);
        AssertNoHiddenExplorerErrors("inventory_drop_identity");
        var inventory = JsonNode.Parse(
            (await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath))!)!.AsObject();
        Assert.Empty(inventory["items"]!.AsArray());
        Assert.Null(inventory["equippedItems"]!["MainHand"]);
        var index = MortalItemIdentityState.Parse(
            (await _fs.ReadFileAsync(MortalItemIdentityState.StatePath))!);
        var entry = index.EntriesByItemId["itm_console_blade"];
        Assert.Equal("destroyed", entry["state"]!.GetValue<string>());
        Assert.Null(entry["currentCarrier"]);
        Assert.Equal("destroy", entry["transitions"]!.AsArray()[^1]!["kind"]!.GetValue<string>());
    }

    [Fact]
    public async Task TryProcessCommand_InventoryDropTechnicalTransitionFailureIsPlayerSafe()
    {
        await SeedMortalStateAsync();
        var blade = CreateCanonicalManagementItem(
            "itm_console_private_failure",
            "Клинок с повреждённой записью",
            count: 1,
            equipmentSlot: "MainHand");
        await SeedCanonicalManagementInventoryAsync(
            new[] { blade },
            new JsonObject { ["MainHand"] = "itm_console_private_failure" });
        var index = MortalItemIdentityState.Parse(
            (await _fs.ReadFileAsync(MortalItemIdentityState.StatePath))!).Root;
        var entry = Assert.Single(index["entries"]!.AsArray().OfType<JsonObject>());
        entry["receiptId"] = "PRIVATE_CONSOLE_FAILURE_RECEIPT";
        await _fs.WriteFileAtomicAsync(
            MortalItemIdentityState.StatePath,
            index.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var beforeInventory = await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath);
        var beforeIndex = await _fs.ReadFileAsync(MortalItemIdentityState.StatePath);
        _console.QueueSelection("Действие", "[red]🗑 Выбросить[/]");
        _console.QueueAnyConfirmResponse(true);
        await _stateManager.RefreshGameStateAsync();

        var exception = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/инв"));
        var playerText = ExtractRenderedText() + "\n" + string.Join("\n", _console.MarkupLines);

        Assert.Null(exception);
        Assert.Contains("состоян", playerText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("itm_console_private_failure", playerText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PRIVATE_CONSOLE_FAILURE_RECEIPT", playerText, StringComparison.Ordinal);
        Assert.DoesNotContain("receipt", playerText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("identity", playerText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("индекс", playerText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("materialization", playerText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeInventory, await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath));
        Assert.Equal(beforeIndex, await _fs.ReadFileAsync(MortalItemIdentityState.StatePath));
    }

    [Fact]
    public async Task TryProcessCommand_AutoDiscardFailedTransitionDoesNotClaimDeletionOrRetry()
    {
        await SeedMortalStateAsync();
        var blade = CreateCanonicalManagementItem(
            "itm_console_failed_auto_discard",
            "Сломанный клинок с защищённой записью",
            count: 1,
            equipmentSlot: "MainHand");
        blade["isBroken"] = true;
        MortalItemTestFixture.ResealCanonical(blade);
        await SeedCanonicalManagementInventoryAsync(
            new[] { blade },
            new JsonObject { ["MainHand"] = null });
        var index = MortalItemIdentityState.Parse(
            (await _fs.ReadFileAsync(MortalItemIdentityState.StatePath))!).Root;
        var entry = Assert.Single(index["entries"]!.AsArray().OfType<JsonObject>());
        entry["receiptId"] = "PRIVATE_AUTO_DISCARD_RECEIPT";
        await _fs.WriteFileAtomicAsync(
            MortalItemIdentityState.StatePath,
            index.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        _settings.AutoDiscardBrokenItems = true;
        _console.ReadKeyCallback = () => _settings.AutoDiscardBrokenItems = false;
        await _stateManager.RefreshGameStateAsync();

        var exception = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/инв"));
        var playerText = ExtractRenderedText() + "\n" + string.Join("\n", _console.MarkupLines);

        Assert.Null(exception);
        Assert.DoesNotContain("Авто-выброс:", playerText, StringComparison.Ordinal);
        Assert.DoesNotContain("удалено", playerText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PRIVATE_AUTO_DISCARD_RECEIPT", playerText, StringComparison.Ordinal);
        var inventory = JsonNode.Parse(
            (await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath))!)!.AsObject();
        Assert.Single(inventory["items"]!.AsArray());
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
        JsonObject equippedItems)
    {
        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(items.Select(item => (JsonNode?)item.DeepClone()).ToArray()),
                ["equippedItems"] = equippedItems
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

    private static JsonObject CreateAcceptedConsoleItemFromJson(
        string itemId,
        string json,
        params string[] populatedSections)
    {
        var semantic = JsonNode.Parse(json)?.AsObject() ??
                       throw new InvalidOperationException("Accepted console fixture must be a JSON object.");
        var name = semantic["name"]?.GetValue<string>() ??
                   semantic["itemName"]?.GetValue<string>() ??
                   throw new InvalidOperationException("Accepted console fixture requires a name.");
        var item = MortalItemTestFixture.CreateCanonicalRoot(itemId);
        item["name"] = name;
        item["description"] = $"Тестовый принятый предмет «{name}».";
        foreach (var property in semantic)
        {
            if (property.Key is "itemId" or "existedId" or "id" or "creationRef" or
                "materialization" or "materializationReceipt")
            {
                continue;
            }

            item[property.Key] = property.Value?.DeepClone();
        }

        foreach (var section in populatedSections)
        {
            item["materialization"]!["sections"]![section] = new JsonObject
            {
                ["state"] = "populated",
                ["reason"] = null
            };
        }

        MortalItemTestFixture.ResealCanonical(item);
        using var document = JsonDocument.Parse(item.ToJsonString());
        var issues = MortalItemMaterializationContract.Validate(
            document.RootElement,
            $"accepted console fixture {itemId}",
            MortalItemMaterializationPhase.CanonicalPostSeal);
        if (issues.Count != 0)
            throw new InvalidOperationException(string.Join(" | ", issues.Select(issue => issue.Message)));
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
