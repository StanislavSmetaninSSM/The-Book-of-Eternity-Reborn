using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

internal sealed partial class MortalItemMaterializationTestContext
{
    internal async Task<(string ParentCreationRef, string ChildCreationRef)>
        ArrangeSameTurnPlayerContainerAsync()
    {
        const string parentCreationRef = "new_item_same_turn_container";
        const string childCreationRef = "new_item_same_turn_child";
        await ArrangeEmptyMortalTurnAsync(RouteTurn);

        var parent = MortalItemTestFixture.CreateRawRoot(
            creationRef: parentCreationRef,
            materializationId: "mat_item_same_turn_container");
        parent["name"] = "Тестовый контейнер";
        parent["isContainer"] = true;
        parent["capacity"] = 10;
        parent["materialization"]!["sections"]!["container"] = new JsonObject
        {
            ["state"] = "populated",
            ["reason"] = null
        };

        var child = MortalItemTestFixture.CreateRawRoot(
            creationRef: childCreationRef,
            materializationId: "mat_item_same_turn_child");
        child["name"] = "Вложенный тестовый предмет";
        child["contentsPath"] = new JsonArray(parentCreationRef);
        await WritePlayerUpdateAsync(parent, child);

        return (parentCreationRef, childCreationRef);
    }

    internal async Task ArrangeUnresolvedInlineCompanionAsync(string surface)
    {
        const string missingReference = "new_item_missing_inline_owner";
        await ArrangeEmptyMortalTurnAsync(RouteTurn);

        switch (surface)
        {
            case "equipment":
            {
                var root = (await ReadJsonAsync(InventoryEquipmentService.ItemsPath))!.AsObject();
                root["equipment"] = new JsonObject { ["mainHand"] = missingReference };
                await WriteJsonAsync(InventoryEquipmentService.ItemsPath, root);
                break;
            }
            case "storage":
            {
                var root = (await ReadJsonAsync(
                    StorageTransportMoveService.CurrentLocationPath))!.AsObject();
                var location = root["currentLocationData"] as JsonObject ?? root;
                location["locationStorages"] = new JsonArray(
                    new JsonObject
                    {
                        ["storageId"] = "storage_inline_reference",
                        ["contents"] = new JsonArray(),
                        ["itemIds"] = new JsonArray(missingReference)
                    });
                await WriteJsonAsync(StorageTransportMoveService.CurrentLocationPath, root);
                break;
            }
            case "contents_path":
            {
                var child = MortalItemTestFixture.CreateRawRoot();
                child["contentsPath"] = new JsonArray(missingReference);
                await WritePlayerUpdateAsync(child);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(surface), surface, null);
        }
    }

    internal async Task WriteSameTurnCompanionReferenceAsync(
        string companion,
        string creationRef)
    {
        switch (companion)
        {
            case "equipment":
            {
                var root = (await ReadJsonAsync(InventoryEquipmentService.ItemsPath))!.AsObject();
                root["equipment"] = new JsonObject { ["mainHand"] = creationRef };
                await WriteJsonAsync(InventoryEquipmentService.ItemsPath, root);
                break;
            }
            case "item_text":
                await WriteJsonAsync(
                    "game_state/inventory/item_text_updates.json",
                    new JsonObject
                    {
                        ["updateItemTextContents"] = new JsonArray(
                            new JsonObject
                            {
                                ["creationRef"] = creationRef,
                                ["textToAppend"] = "Текст созданного предмета."
                            })
                    });
                break;
            case "journal":
                await WriteJsonAsync(
                    "game_state/npcs/item_journals.json",
                    new JsonObject
                    {
                        ["itemJournalUpdates"] = new JsonArray(
                            new JsonObject
                            {
                                ["creationRef"] = creationRef,
                                ["entryToAppend"] = "Первая запись предмета."
                            })
                    });
                break;
            case "bond":
                await WriteJsonAsync(
                    "game_state/inventory/item_bonds.json",
                    new JsonObject
                    {
                        ["itemBondLevelChanges"] = new JsonArray(
                            new JsonObject
                            {
                                ["creationRef"] = creationRef,
                                ["newBondLevel"] = 1,
                                ["changeReason"] = "Первое прикосновение."
                            })
                    });
                break;
            case "recipe":
                await WriteJsonAsync(
                    "game_state/inventory/recipes.json",
                    new JsonObject
                    {
                        ["recipes"] = new JsonArray(),
                        ["addOrUpdateRecipes"] = new JsonArray(
                            new JsonObject
                            {
                                ["recipeId"] = "recipe_same_turn_item",
                                ["resultItemId"] = creationRef
                            })
                    });
                break;
            case "quest_reward":
                await WriteJsonAsync(
                    "game_state/quests/quest_history.json",
                    new JsonObject
                    {
                        ["questHistory"] = new JsonArray(),
                        ["questRewards"] = new JsonArray(
                            new JsonObject
                            {
                                ["questId"] = "quest_same_turn_item",
                                ["itemsReceived"] = new JsonArray(
                                    new JsonObject
                                    {
                                        ["creationRef"] = creationRef,
                                        ["displayName"] = "Тестовый предмет"
                                    })
                            }),
                        ["questChains"] = new JsonArray()
                    });
                break;
            case "storage_reference":
            {
                var root = (await ReadJsonAsync(
                    StorageTransportMoveService.CurrentLocationPath))!.AsObject();
                var location = root["currentLocationData"] as JsonObject ?? root;
                var storage = location["locationStorages"]!.AsArray()[0]!.AsObject();
                storage["itemIds"] = new JsonArray(creationRef);
                await WriteJsonAsync(StorageTransportMoveService.CurrentLocationPath, root);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(companion), companion, null);
        }
    }

    internal async Task<JsonNode?> ReadSameTurnCompanionSurfaceAsync(string companion)
    {
        return companion switch
        {
            "equipment" => (await ReadJsonAsync(InventoryEquipmentService.ItemsPath))?["equipment"],
            "item_text" => await ReadJsonAsync("game_state/inventory/item_text_updates.json"),
            "journal" => await ReadJsonAsync("game_state/npcs/item_journals.json"),
            "bond" => await ReadJsonAsync("game_state/inventory/item_bonds.json"),
            "recipe" => await ReadJsonAsync("game_state/inventory/recipes.json"),
            "quest_reward" => await ReadJsonAsync("game_state/quests/quest_history.json"),
            "storage_reference" =>
                (await ReadJsonAsync(StorageTransportMoveService.CurrentLocationPath))?
                ["locationStorages"]?[0]?["itemIds"] ??
                (await ReadJsonAsync(StorageTransportMoveService.CurrentLocationPath))?
                ["currentLocationData"]?["locationStorages"]?[0]?["itemIds"],
            _ => throw new ArgumentOutOfRangeException(nameof(companion), companion, null)
        };
    }

    internal async Task<string> ReadSingleActiveMortalItemIdAsync()
    {
        var index = (await ReadJsonAsync(MortalItemIdentityState.StatePath))!.AsObject();
        return index["entries"]!.AsArray()
            .OfType<JsonObject>()
            .Single(entry => string.Equals(
                entry["state"]!.GetValue<string>(),
                "active",
                StringComparison.Ordinal))["itemId"]!
            .GetValue<string>();
    }

    internal async Task WriteExistingNpcSameTurnEquipmentCommandAsync(
        string creationRef)
    {
        var root = (await ReadJsonAsync("game_state/npcs/npc_inventory.json"))!.AsObject();
        root["NPCEquipmentChanges"] = new JsonArray(
            new JsonObject
            {
                ["NPCId"] = ExistingNpcId,
                ["NPCName"] = "Маршрутный NPC",
                ["action"] = "equip",
                ["itemCreationRef"] = creationRef,
                ["itemId"] = null,
                ["itemName"] = "Тестовый предмет",
                ["targetSlots"] = new JsonArray("mainHand")
            });
        await WriteJsonAsync("game_state/npcs/npc_inventory.json", root);
    }

    internal async Task AppendUnrelatedNpcEquipmentCommandAsync()
    {
        var root = (await ReadJsonAsync(
            "game_state/npcs/npc_inventory.json"))!.AsObject();
        var commands = root["NPCEquipmentChanges"]!.AsArray();
        commands.Add(new JsonObject
        {
            ["NPCId"] = ExistingNpcId,
            ["itemId"] = "itm_unrelated_existing_command",
            ["action"] = "equip",
            ["targetSlots"] = new JsonArray("offHand")
        });
        await WriteJsonAsync("game_state/npcs/npc_inventory.json", root);
    }

    internal async Task<string?> ReadExistingNpcEquippedItemAsync(string slot)
    {
        var root = (await ReadJsonAsync(NpcCoreChangesContract.NpcCorePath))!.AsObject();
        var npc = root["NPCsInScene"]!.AsArray()
            .OfType<JsonObject>()
            .Single(actor => string.Equals(
                actor["NPCId"]?.GetValue<string>(),
                ExistingNpcId,
                StringComparison.Ordinal));
        return npc["equippedItems"]?[slot]?.GetValue<string>();
    }

    internal async Task WriteNewNpcSameTurnEquipmentReferenceAsync(
        string creationRef)
    {
        var root = (await ReadJsonAsync(NpcCoreChangesContract.NpcCorePath))!.AsObject();
        root["UpdateNPCs"]![0]!["equippedItems"] =
            new JsonObject { ["mainHand"] = creationRef };
        await WriteJsonAsync(NpcCoreChangesContract.NpcCorePath, root);
    }

    internal async Task<string?> ReadNewNpcEquippedItemAsync(string slot)
    {
        var root = (await ReadJsonAsync(NpcCoreChangesContract.NpcCorePath))!.AsObject();
        return root["UpdateNPCs"]?[0]?["equippedItems"]?[slot]?.GetValue<string>();
    }

    internal static bool ContainsExactString(JsonNode? node, string expected)
    {
        return node switch
        {
            JsonValue value when value.TryGetValue<string>(out var text) =>
                string.Equals(text, expected, StringComparison.Ordinal),
            JsonObject obj => obj.Any(pair => ContainsExactString(pair.Value, expected)),
            JsonArray array => array.Any(item => ContainsExactString(item, expected)),
            _ => false
        };
    }
}
