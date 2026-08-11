using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

internal sealed partial class MortalItemMaterializationTestContext
{
    internal async Task ArrangeEmptyMortalTurnAsync(int turn = 42)
    {
        await BuildMortalBootstrapAsync();
        await CaptureValidatedPendingSnapshotAsync(turn);
    }

    internal async Task WritePlayerUpdateAsync(params JsonObject[] items)
    {
        var root = await ReadJsonAsync(InventoryEquipmentService.ItemsPath) as JsonObject ??
                   new JsonObject
                   {
                       ["items"] = new JsonArray(),
                       ["equipment"] = new JsonObject()
                   };
        root["UpdateInventory"] = CloneArray(items);
        await WriteJsonAsync(InventoryEquipmentService.ItemsPath, root);
    }

    internal async Task WriteCanonicalPlayerItemsAsync(
        JsonObject index,
        params JsonObject[] items)
    {
        await WriteJsonAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = CloneArray(items),
                ["equipment"] = new JsonObject()
            });
        await WriteJsonAsync(MortalItemIdentityState.StatePath, index);
    }

    internal Task WriteCanonicalPlayerItemAsync(JsonObject item, JsonObject? index = null) =>
        WriteCanonicalPlayerItemsAsync(
            index ?? MortalItemIdentityState.CreateEmptyRoot(),
            item);

    internal Task WriteCanonicalNpcItemAsync(string npcId, JsonObject item) =>
        WriteJsonAsync(
            NpcCoreChangesContract.NpcCorePath,
            new JsonObject
            {
                ["NPCsInScene"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["NPCId"] = npcId,
                        ["inventory"] = CloneArray(item)
                    }
                }
            });

    private static JsonArray CloneArray(params JsonObject[] items) =>
        new(items.Select(item => (JsonNode?)item.DeepClone()).ToArray());
}
