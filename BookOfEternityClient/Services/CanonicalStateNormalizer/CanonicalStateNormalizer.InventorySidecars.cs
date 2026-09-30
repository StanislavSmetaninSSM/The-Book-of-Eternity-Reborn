using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private async Task NormalizeInventoryItemsAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/inventory/items.json";
        var currentNode = await ReadNodeAsync(path);
        if (currentNode is not JsonObject currentRoot)
            return;

        if (MortalItemPublicationTailTransforms.InventoryItemsJournal(currentRoot) is not
            JsonObject result)
        {
            return;
        }

        await WriteIfChangedAsync(path, currentNode, result);
    }

    private async Task NormalizeInventoryItemBondsAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/inventory/item_bonds.json";
        var currentNode = await ReadNodeAsync(path);
        if (currentNode is not JsonObject currentRoot) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        if (MortalItemPublicationTailTransforms.ItemBonds(currentRoot, previous) is not
            JsonObject result)
        {
            return;
        }

        await WriteIfChangedAsync(path, currentNode, result);
    }

    private async Task NormalizeInventoryItemTextsAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/inventory/item_text_updates.json";
        var currentNode = await ReadNodeAsync(path);
        if (currentNode is not JsonObject currentRoot) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        if (MortalItemPublicationTailTransforms.ItemTexts(currentRoot, previous) is not
            JsonObject result)
        {
            return;
        }

        await WriteIfChangedAsync(path, currentNode, result);
    }

    private async Task NormalizeItemJournalsAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/npcs/item_journals.json";
        var currentNode = await ReadNodeAsync(path);
        if (currentNode is not JsonObject currentRoot) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        if (MortalItemPublicationTailTransforms.NpcItemJournals(currentRoot, previous) is not
            JsonObject result)
        {
            return;
        }

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

