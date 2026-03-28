using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private async Task NormalizeInventoryItemResourcesAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/inventory/item_resources.json";
        var currentNode = await ReadNodeAsync(path);
        if (currentNode == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        var entries = new JsonArray();

        foreach (var entry in CollectInventorySidecarEntries(previous, "entries"))
            UpsertByIdentity(entries, entry, "existedId", "itemId", "id", "itemName", "name");

        if (currentNode is JsonObject currentObj)
        {
            foreach (var entry in CollectInventorySidecarEntries(currentObj, "entries"))
                UpsertByIdentity(entries, entry, "existedId", "itemId", "id", "itemName", "name");

            if (currentObj["inventoryItemsResources"] is JsonArray resourceChanges)
                ApplyInventoryResourceCommands(entries, resourceChanges);
        }
        else
        {
            foreach (var entry in CollectInventorySidecarEntries(currentNode, "entries"))
                UpsertByIdentity(entries, entry, "existedId", "itemId", "id", "itemName", "name");
        }

        result["entries"] = entries;
        result.Remove("inventoryItemsResources");
        await WriteIfChangedAsync(path, currentNode, result);
    }

    private async Task NormalizeInventoryItemBondsAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/inventory/item_bonds.json";
        var currentNode = await ReadNodeAsync(path);
        if (currentNode == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        var entries = new JsonArray();

        foreach (var entry in CollectInventorySidecarEntries(previous, "entries"))
            UpsertByIdentity(entries, entry, "existedId", "itemId", "id", "itemName", "name");

        if (currentNode is JsonObject currentObj)
        {
            foreach (var entry in CollectInventorySidecarEntries(currentObj, "entries"))
                UpsertByIdentity(entries, entry, "existedId", "itemId", "id", "itemName", "name");

            if (currentObj["itemBondLevelChanges"] is JsonArray bondChanges)
                ApplyInventoryBondCommands(entries, bondChanges);
            if (currentObj["itemFateCardUnlocks"] is JsonArray fateCardUnlocks)
                ApplyInventoryFateCardUnlockCommands(entries, fateCardUnlocks);
        }
        else
        {
            foreach (var entry in CollectInventorySidecarEntries(currentNode, "entries"))
                UpsertByIdentity(entries, entry, "existedId", "itemId", "id", "itemName", "name");
        }

        result["entries"] = entries;
        result.Remove("itemBondLevelChanges");
        result.Remove("itemFateCardUnlocks");
        await WriteIfChangedAsync(path, currentNode, result);
    }

    private async Task NormalizeInventoryItemTextsAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/inventory/item_text_updates.json";
        var currentNode = await ReadNodeAsync(path);
        if (currentNode == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        var entries = new JsonArray();

        foreach (var entry in CollectInventoryTextEntries(previous))
            UpsertByIdentity(entries, entry, "existedId", "itemId", "id", "itemName", "name");

        if (currentNode is JsonObject currentObj)
        {
            foreach (var entry in CollectInventoryTextEntries(currentObj))
                UpsertByIdentity(entries, entry, "existedId", "itemId", "id", "itemName", "name");

            if (currentObj["updateItemTextContents"] is JsonArray textUpdates)
                ApplyInventoryTextCommands(entries, textUpdates);
        }
        else
        {
            foreach (var entry in CollectInventoryTextEntries(currentNode))
                UpsertByIdentity(entries, entry, "existedId", "itemId", "id", "itemName", "name");
        }

        result["entries"] = entries;
        result.Remove("updateItemTextContents");
        await WriteIfChangedAsync(path, currentNode, result);
    }

    private async Task NormalizeItemJournalsAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/npcs/item_journals.json";
        var currentNode = await ReadNodeAsync(path);
        if (currentNode == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        var entries = new JsonArray();

        foreach (var entry in CollectInventorySidecarEntries(previous, "entries", "itemJournals"))
            UpsertByIdentity(entries, entry, "itemId", "existedId", "id", "itemName", "name");

        if (currentNode is JsonObject currentObj)
        {
            foreach (var entry in CollectInventorySidecarEntries(currentObj, "entries", "itemJournals"))
                UpsertByIdentity(entries, entry, "itemId", "existedId", "id", "itemName", "name");

            if (currentObj["itemJournalUpdates"] is JsonArray journalUpdates)
                ApplyItemJournalCommands(entries, journalUpdates);
        }
        else
        {
            foreach (var entry in CollectInventorySidecarEntries(currentNode, "entries", "itemJournals"))
                UpsertByIdentity(entries, entry, "itemId", "existedId", "id", "itemName", "name");
        }

        result["entries"] = entries;
        result.Remove("itemJournals");
        result.Remove("itemJournalUpdates");
        await WriteIfChangedAsync(path, currentNode, result);
    }

    private async Task NormalizeNpcInteractionJournalAsync(IReadOnlyDictionary<string, string>? backups)
    {
        await NormalizeActorJournalAsync(NpcInteractionJournalState.StatePath, NpcInteractionJournalState.UpdateProperty, NpcInteractionJournalState.ActorIdProperty, backups);
    }

    private async Task NormalizeGuardianThoughtJournalAsync(IReadOnlyDictionary<string, string>? backups)
    {
        await NormalizeActorJournalAsync(GuardianThoughtJournalState.StatePath, GuardianThoughtJournalState.UpdateProperty, GuardianThoughtJournalState.ActorIdProperty, backups);
    }

    private async Task NormalizeGuardianSocialJournalAsync(IReadOnlyDictionary<string, string>? backups)
    {
        await NormalizeActorJournalAsync(GuardianSocialJournalState.StatePath, GuardianSocialJournalState.UpdateProperty, GuardianSocialJournalState.ActorIdProperty, backups);
    }

    private async Task NormalizeActorJournalAsync(string path, string updateProperty, string actorIdProperty, IReadOnlyDictionary<string, string>? backups)
    {
        var currentNode = await ReadNodeAsync(path);
        if (currentNode == null)
            return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        var entries = ActorJournalState.EnsureEntriesArray(result, actorIdProperty, updateProperty);

        foreach (var entry in ActorJournalState.CollectEntries(previous, actorIdProperty, updateProperty))
            ActorJournalState.ApplyUpdates(result, new JsonArray(entry), actorIdProperty, updateProperty);

        foreach (var entry in ActorJournalState.CollectEntries(currentNode, actorIdProperty, updateProperty))
            ActorJournalState.ApplyUpdates(result, new JsonArray(entry), actorIdProperty, updateProperty);

        result[ActorJournalState.EntriesProperty] = entries;
        result.Remove(updateProperty);
        await WriteIfChangedAsync(path, currentNode, result);
    }

}

