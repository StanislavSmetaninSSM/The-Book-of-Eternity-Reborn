using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

internal sealed partial class MortalItemMaterializationTestContext
{
    internal const string TransferItemId = "itm_gm_transfer";
    internal const string TransferNpcId = "npc_gm_transfer";
    internal const int TransferTurn = 43;

    internal async Task<JsonObject> ArrangeNpcToPlayerTransferAsync(
        bool recreateWithNullIdentity = false)
    {
        await BuildMortalBootstrapAsync();
        var item = MortalItemTestFixture.CreateCanonicalRoot(TransferItemId);
        await WriteJsonAsync(
            NpcCoreChangesContract.NpcCorePath,
            NpcRoot(item));
        await WriteJsonAsync(
            MortalItemIdentityState.StatePath,
            MortalItemTestFixture.CreateIndexForCarrier(
                item,
                "npc_inventory",
                TransferNpcId));
        await WriteJsonAsync(
            "game_state/npcs/item_journals.json",
            new JsonObject
            {
                ["entries"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["itemId"] = TransferItemId,
                        ["journalEntries"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["turn"] = 42,
                                ["description"] = "История должна пережить смену владельца."
                            }
                        }
                    }
                }
            });
        await CaptureValidatedPendingSnapshotAsync(TransferTurn);

        var inventory = (await ReadJsonAsync(InventoryEquipmentService.ItemsPath))!.AsObject();
        inventory["items"] ??= new JsonArray();
        if (recreateWithNullIdentity)
        {
            var raw = MortalItemTestFixture.CreateRawRoot(
                route: "player_acquisition",
                authorityKind: "turn_outcome",
                authorityId: $"turn_{TransferTurn}",
                sourceTurn: TransferTurn,
                creationRef: "new_item_recreated_transfer",
                materializationId: "mat_item_recreated_transfer");
            inventory["UpdateInventory"] = new JsonArray(raw);
        }
        else
        {
            inventory["UpdateInventory"] = new JsonArray(item.DeepClone());
        }
        await WriteJsonAsync(InventoryEquipmentService.ItemsPath, inventory);
        await WriteJsonAsync(
            "game_state/npcs/npc_inventory.json",
            new JsonObject
            {
                ["NPCInventoryRemovals"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["NPCId"] = TransferNpcId,
                        ["NPCName"] = "Передающий NPC",
                        ["itemId"] = TransferItemId
                    }
                }
            });
        return item;
    }

    internal async Task<JsonObject> ArrangePlayerToNpcTransferAsync()
    {
        await BuildMortalBootstrapAsync();
        var item = MortalItemTestFixture.CreateCanonicalRoot(TransferItemId);
        await WriteCanonicalPlayerItemAsync(
            item,
            MortalItemTestFixture.CreateIndex(item));
        await WriteJsonAsync(
            NpcCoreChangesContract.NpcCorePath,
            NpcRoot());
        await CaptureValidatedPendingSnapshotAsync(TransferTurn);

        await WriteJsonAsync(
            "game_state/inventory/item_removals.json",
            new JsonObject
            {
                ["removeInventoryItems"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["removedItemId"] = TransferItemId,
                        ["itemName"] = item["name"]!.GetValue<string>(),
                        ["currentContentsPath"] = null
                    }
                }
            });
        await WriteJsonAsync(
            "game_state/npcs/npc_inventory.json",
            new JsonObject
            {
                ["NPCInventoryAdds"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["NPCId"] = TransferNpcId,
                        ["NPCName"] = "Получающий NPC",
                        ["item"] = item.DeepClone(),
                        ["destinationContainerId"] = null
                    }
                }
            });
        return item;
    }

    internal async Task<JsonObject> ReadTransferIndexEntryAsync()
    {
        var index = MortalItemIdentityState.Parse(
            (await ReadJsonAsync(MortalItemIdentityState.StatePath))!);
        if (index.Issues.Count > 0)
            throw new InvalidOperationException(index.Issues[0].Code);
        return index.EntriesByItemId[TransferItemId];
    }

    internal async Task ForgePlayerTransferReceiptAsync()
    {
        var inventory = (await ReadJsonAsync(InventoryEquipmentService.ItemsPath))!.AsObject();
        var payload = inventory["UpdateInventory"]!.AsArray()
            .OfType<JsonObject>()
            .Single();
        payload["materializationReceipt"]!["receiptId"] = "mirec_forged_transfer";
        await WriteJsonAsync(InventoryEquipmentService.ItemsPath, inventory);
    }

    private static JsonObject NpcRoot(params JsonObject[] items) =>
        new()
        {
            ["UpdateNPCs"] = new JsonArray(),
            ["NPCsInScene"] = new JsonArray
            {
                new JsonObject
                {
                    ["NPCId"] = TransferNpcId,
                    ["name"] = "Получающий NPC",
                    ["inventory"] = new JsonArray(
                        items.Select(item => (JsonNode?)item.DeepClone()).ToArray()),
                    ["equippedItems"] = items.Length == 0
                        ? new JsonObject()
                        : new JsonObject
                        {
                            ["mainHand"] = items[0]["itemId"]!.GetValue<string>()
                        }
                }
            }
        };
}
