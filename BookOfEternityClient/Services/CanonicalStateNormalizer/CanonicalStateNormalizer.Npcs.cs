using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private async Task NormalizeNpcJournalsAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/npcs/npc_journals.json";
        var currentNode = await ReadNodeAsync(path);
        if (currentNode is not JsonObject currentObj)
            return;

        var result = CloneObject(currentObj);
        var changed = false;

        foreach (var collectionName in new[] { "NPCJournals", "npcJournals" })
        {
            if (result[collectionName] is not JsonArray journals)
                continue;

            foreach (var journal in journals.OfType<JsonObject>())
                changed |= NormalizeNpcJournalEntry(journal);
        }

        if (changed)
            await WriteIfChangedAsync(path, currentNode, result);
    }

    private static bool NormalizeNpcJournalEntry(JsonObject journal)
    {
        var changed = false;
        var fallbackNote = FirstNonEmptyString(
            GetNodeString(journal["lastJournalNote"]),
            GetNodeString(journal["entry"]),
            GetNodeString(journal["note"]),
            GetNodeString(journal["text"]),
            GetNodeString(journal["description"]));

        if (string.IsNullOrWhiteSpace(GetNodeString(journal["lastJournalNote"])) &&
            !string.IsNullOrWhiteSpace(fallbackNote))
        {
            journal["lastJournalNote"] = fallbackNote;
            changed = true;
        }

        if (journal["journalEntries"] is not JsonArray journalEntries)
        {
            if (string.IsNullOrWhiteSpace(fallbackNote))
                return changed;

            journal["journalEntries"] = new JsonArray(BuildNpcJournalEntryObject(fallbackNote, journal));
            return true;
        }

        for (var index = 0; index < journalEntries.Count; index++)
        {
            var item = journalEntries[index];
            switch (item)
            {
                case JsonValue value when value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text):
                    journalEntries[index] = BuildNpcJournalEntryObject(text, journal);
                    changed = true;
                    break;
                case JsonObject obj:
                    changed |= EnsureNpcJournalEntryDescription(obj, fallbackNote);
                    break;
            }
        }

        if (journalEntries.Count == 0 && !string.IsNullOrWhiteSpace(fallbackNote))
        {
            journalEntries.Add(BuildNpcJournalEntryObject(fallbackNote, journal));
            changed = true;
        }

        return changed;
    }

    private static JsonObject BuildNpcJournalEntryObject(string description, JsonObject parent)
    {
        var entry = new JsonObject
        {
            ["description"] = description
        };

        CopyStringIfPresent(parent, entry, "timestamp");
        CopyStringIfPresent(parent, entry, "event");
        CopyStringIfPresent(parent, entry, "emotionalImpact");
        CopyStringIfPresent(parent, entry, "relationshipChange");
        return entry;
    }

    private static bool EnsureNpcJournalEntryDescription(JsonObject entry, string? fallbackNote)
    {
        if (!string.IsNullOrWhiteSpace(GetNodeString(entry["description"])))
            return false;

        var description = FirstNonEmptyString(
            GetNodeString(entry["note"]),
            GetNodeString(entry["entry"]),
            GetNodeString(entry["text"]),
            fallbackNote);
        if (string.IsNullOrWhiteSpace(description))
            return false;

        entry["description"] = description;
        return true;
    }

    private static void CopyStringIfPresent(JsonObject source, JsonObject destination, string propertyName)
    {
        var value = GetNodeString(source[propertyName]);
        if (!string.IsNullOrWhiteSpace(value))
            destination[propertyName] = value;
    }

    private static string FirstNonEmptyString(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private async Task NormalizeNpcTradeCoreAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/npcs/npc_core.json";
        var currentNode = await ReadNodeAsync(path);
        if (currentNode is not JsonObject currentObj)
            return;

        var result = CloneObject(currentObj);
        var changed = false;

        if (result[NpcTradeRequestState.UpdateReceiptsProperty] is JsonArray receiptUpdates)
        {
            NpcTradeRequestState.ApplyReceiptUpdates(result, receiptUpdates);
            result.Remove(NpcTradeRequestState.UpdateReceiptsProperty);
            changed = true;
        }

        foreach (var npcs in GuardianPolicyContracts.EnumerateCanonicalNpcObjectArrays(result))
        {
            foreach (var npc in npcs.OfType<JsonObject>())
            {
                var before = npc.ToJsonString();
                NpcTradeRequestState.NormalizeNpcTradeReceiptsShape(npc);
                if (!string.Equals(before, npc.ToJsonString(), StringComparison.Ordinal))
                    changed = true;
            }
        }

        if (changed)
            await WriteIfChangedAsync(path, currentNode, result);
    }
}
