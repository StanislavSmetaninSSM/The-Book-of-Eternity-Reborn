using System.Text;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Shared pure forms of the ordinary selected-root normalizer tail.</summary>
internal static class MortalItemPublicationTailTransforms
{
    internal static JsonNode? QuestHistory(JsonNode? current, JsonNode? previous)
    {
        if (current is not JsonObject currentRoot)
            return current?.DeepClone();
        var previousRoot = previous as JsonObject;
        var result = (previousRoot ?? new JsonObject()).DeepClone().AsObject();
        var history = new List<JsonObject>();
        foreach (var quest in CanonicalStateNormalizer
                     .CollectQuestHistoryEntries(previousRoot).OfType<JsonObject>())
        {
            CanonicalStateNormalizer.UpsertByIdentity(
                history, quest, "questId", "questName", "title", "name");
        }
        foreach (var quest in CanonicalStateNormalizer
                     .CollectQuestHistoryEntries(currentRoot).OfType<JsonObject>())
        {
            CanonicalStateNormalizer.UpsertByIdentity(
                history, quest, "questId", "questName", "title", "name");
        }
        var rewards = new List<JsonObject>();
        CanonicalStateNormalizer.CollectNamedObjectEntries(
            previousRoot, "questRewards", rewards);
        CanonicalStateNormalizer.CollectNamedObjectEntries(
            currentRoot, "questRewards", rewards);
        var chains = new List<JsonObject>();
        CanonicalStateNormalizer.CollectNamedObjectEntries(
            previousRoot, "questChains", chains);
        CanonicalStateNormalizer.CollectNamedObjectEntries(
            currentRoot, "questChains", chains);
        result["questHistory"] = CanonicalStateNormalizer.ToArray(history);
        SetOrRemove(result, "questRewards", rewards);
        SetOrRemove(result, "questChains", chains);
        result.Remove("questLog");
        result.Remove("quests");
        return result;
    }

    internal static JsonNode? NpcCore(
        JsonNode? current,
        JsonNode? previous,
        NpcCoreChangesContract.Authority authority,
        CanonicalBeforeImage npcTradePending,
        CanonicalBeforeImage trainingPending)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(npcTradePending);
        ArgumentNullException.ThrowIfNull(trainingPending);
        if (current is not JsonObject currentRoot ||
            !currentRoot.ContainsKey(NpcCoreChangesContract.PropertyName) ||
            previous is not JsonObject previousRoot)
        {
            return current?.DeepClone();
        }
        var result = currentRoot.DeepClone().AsObject();
        var accepted = MortalActorAcceptedTurnAuthority.Create(
            result,
            Decode(npcTradePending),
            Decode(trainingPending));
        var evaluation = NpcCoreChangesContract.Evaluate(
            result,
            previousRoot,
            authority,
            ValidationService.ValidateNpcCoreFateCardsAgainstProductionContract,
            detectDirectMutations: true,
            accepted);
        if (evaluation.CanApply)
            NpcCoreChangesContract.Apply(result, evaluation);
        return result;
    }

    internal static JsonNode? NpcTrade(
        JsonNode? current,
        JsonNode? previous,
        MortalItemNpcTradeTailDisposition disposition)
    {
        if (current is not JsonObject currentRoot ||
            disposition == MortalItemNpcTradeTailDisposition
                .SkipUntouchedTreatmentContinuation)
        {
            return current?.DeepClone();
        }
        var result = currentRoot.DeepClone().AsObject();
        CanonicalStateNormalizer.PreserveHistoricalMortalMaterialization(
            result,
            previous as JsonObject);
        CanonicalStateNormalizer.NormalizeMortalTeacherTrainingShowcasePatches(result);
        if (result[NpcTradeRequestState.UpdateReceiptsProperty] is JsonArray updates)
        {
            NpcTradeRequestState.ApplyReceiptUpdates(result, updates);
            result.Remove(NpcTradeRequestState.UpdateReceiptsProperty);
        }
        foreach (var npcs in GuardianPolicyContracts.EnumerateCanonicalNpcObjectArrays(result))
        {
            foreach (var npc in npcs.OfType<JsonObject>())
                NpcTradeRequestState.NormalizeNpcTradeReceiptsShape(npc);
        }
        return result;
    }

    internal static JsonNode? InventoryItemsJournal(JsonNode? current)
    {
        var result = current?.DeepClone();
        if (result != null)
            CanonicalStateNormalizer.NormalizeInventoryItemJournalEntries(result);
        return result;
    }

    internal static JsonNode? ItemBonds(JsonNode? current, JsonNode? previous)
    {
        if (current is not JsonObject currentRoot)
            return current?.DeepClone();
        var previousRoot = previous as JsonObject;
        var result = (previousRoot ?? new JsonObject()).DeepClone().AsObject();
        var entries = new JsonArray();
        foreach (var entry in CanonicalStateNormalizer.CollectInventorySidecarEntries(
                     previousRoot, "entries"))
        {
            CanonicalStateNormalizer.UpsertByIdentity(
                entries, entry, "existedId", "itemId", "id", "itemName", "name");
        }
        foreach (var entry in CanonicalStateNormalizer.CollectInventorySidecarEntries(
                     currentRoot, "entries"))
        {
            CanonicalStateNormalizer.UpsertByIdentity(
                entries, entry, "existedId", "itemId", "id", "itemName", "name");
        }
        if (currentRoot["itemBondLevelChanges"] is JsonArray bondChanges)
            CanonicalStateNormalizer.ApplyInventoryBondCommands(entries, bondChanges);
        if (currentRoot["itemFateCardUnlocks"] is JsonArray fateCardUnlocks)
            CanonicalStateNormalizer.ApplyInventoryFateCardUnlockCommands(
                entries, fateCardUnlocks);
        result["entries"] = entries;
        result.Remove("itemBondLevelChanges");
        result.Remove("itemFateCardUnlocks");
        return result;
    }

    internal static JsonNode? ItemTexts(JsonNode? current, JsonNode? previous)
    {
        if (current is not JsonObject currentRoot)
            return current?.DeepClone();
        var previousRoot = previous as JsonObject;
        var result = (previousRoot ?? new JsonObject()).DeepClone().AsObject();
        var entries = new JsonArray();
        foreach (var entry in CanonicalStateNormalizer.CollectInventoryTextEntries(previousRoot))
        {
            CanonicalStateNormalizer.UpsertByIdentity(
                entries, entry, "existedId", "itemId", "id", "itemName", "name");
        }
        foreach (var entry in CanonicalStateNormalizer.CollectInventoryTextEntries(currentRoot))
        {
            CanonicalStateNormalizer.UpsertByIdentity(
                entries, entry, "existedId", "itemId", "id", "itemName", "name");
        }
        if (currentRoot["updateItemTextContents"] is JsonArray updates)
            CanonicalStateNormalizer.ApplyInventoryTextCommands(entries, updates);
        result["entries"] = entries;
        result.Remove("updateItemTextContents");
        return result;
    }

    internal static JsonNode? NpcItemJournals(JsonNode? current, JsonNode? previous)
    {
        if (current is not JsonObject currentRoot)
            return current?.DeepClone();
        var previousRoot = previous as JsonObject;
        var result = (previousRoot ?? new JsonObject()).DeepClone().AsObject();
        var entries = new JsonArray();
        foreach (var entry in CanonicalStateNormalizer.CollectInventorySidecarEntries(
                     previousRoot, "entries", "itemJournals"))
        {
            CanonicalStateNormalizer.UpsertByIdentity(
                entries, entry, "itemId", "existedId", "id", "itemName", "name");
        }
        foreach (var entry in CanonicalStateNormalizer.CollectInventorySidecarEntries(
                     currentRoot, "entries", "itemJournals"))
        {
            CanonicalStateNormalizer.UpsertByIdentity(
                entries, entry, "itemId", "existedId", "id", "itemName", "name");
        }
        if (currentRoot["itemJournalUpdates"] is JsonArray updates)
            CanonicalStateNormalizer.ApplyItemJournalCommands(entries, updates);
        result["entries"] = entries;
        result.Remove("itemJournals");
        result.Remove("itemJournalUpdates");
        CanonicalStateNormalizer.NormalizeInventoryItemJournalEntries(result);
        return result;
    }

    private static void SetOrRemove(
        JsonObject root,
        string property,
        IReadOnlyCollection<JsonObject> values)
    {
        if (values.Count == 0)
            root.Remove(property);
        else
            root[property] = CanonicalStateNormalizer.ToArray(values);
    }

    private static string? Decode(CanonicalBeforeImage image)
    {
        if (!image.Existed || image.Bytes is not { } bytes)
            return null;
        var preamble = Encoding.UTF8.GetPreamble();
        var offset = bytes.AsSpan().StartsWith(preamble) ? preamble.Length : 0;
        return new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
    }
}
