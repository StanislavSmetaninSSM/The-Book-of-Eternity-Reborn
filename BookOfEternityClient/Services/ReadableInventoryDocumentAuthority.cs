using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class ReadableInventoryDocumentAuthority
{
    public const string MissingDetailAuthorityCode = "readable_document_missing_detail_authority";

    private static readonly string[] DocumentTerms =
    [
        "book", "letter", "scroll", "note", "document", "inscription", "diary", "journal",
        "книга", "письмо", "свиток", "записка", "документ", "надпись", "дневник", "журнал"
    ];

    private static readonly string[] ItemIdentityFields = ["existedId", "itemId", "id"];
    private static readonly string[] ItemNameFields = ["name", "itemName"];
    private static readonly string[] ReasonFields =
    [
        "unreadableReason", "sealedReason", "lockedReason", "unknownReason",
        "readingBlockedReason", "readBlockedReason", "cannotReadReason",
        "inaccessibleReason", "unavailableReason"
    ];

    public static IReadOnlyList<ReadableInventoryDocument> ResolveDocuments(
        JsonNode? inventoryRoot,
        JsonNode? itemTextRoot,
        JsonNode? itemJournalRoot)
    {
        var textEntries = CollectItemTextEntries(itemTextRoot);
        var journalEntries = CollectItemJournalEntries(itemJournalRoot);
        var textById = BuildEntryMap(textEntries, static entry => entry.Identities);
        var textByName = BuildEntryMap(textEntries, static entry => entry.Identities.Count > 0 || string.IsNullOrWhiteSpace(entry.Name) ? [] : [entry.Name]);
        var journalById = BuildEntryMap(journalEntries, static entry => entry.Identities);
        var journalByName = BuildEntryMap(journalEntries, static entry => entry.Identities.Count > 0 || string.IsNullOrWhiteSpace(entry.Name) ? [] : [entry.Name]);

        var result = new List<ReadableInventoryDocument>();
        foreach (var item in EnumerateInventoryItemObjects(inventoryRoot))
        {
            if (!IsDocumentLikeItem(item))
                continue;

            var identities = ReadIdentityValues(item, ItemIdentityFields);
            var name = FirstNonEmpty(ReadNodeString(item, ItemNameFields)) ?? "Безымянный документ";
            var text = new List<string>();
            text.AddRange(ReadStringValues(item["textContent"]));
            text.AddRange(ResolveSidecarText(identities, name, textById, textByName));
            text.AddRange(ResolveSidecarText(identities, name, journalById, journalByName));

            result.Add(new ReadableInventoryDocument(
                Identities: identities,
                Name: name,
                ContextIdentity: FirstNonEmpty(identities) ?? name,
                TextEntries: Deduplicate(text),
                UnreadableReason: ReadUnreadableReason(item)));
        }

        return result;
    }

    public static IReadOnlyList<ReadableInventoryTextEntry> CollectItemTextEntries(JsonNode? root) =>
        CollectSidecarEntries(root, ["entries", "updateItemTextContents"], ReadItemTextEntryText);

    public static IReadOnlyList<ReadableInventoryTextEntry> CollectItemJournalEntries(JsonNode? root) =>
        CollectSidecarEntries(root, ["entries", "itemJournals", "itemJournalUpdates"], ReadItemJournalEntryText);

    public static bool SidecarMatchesDocument(ReadableInventoryTextEntry sidecar, ReadableInventoryDocument document)
    {
        if (sidecar.Identities.Count > 0)
        {
            return document.Identities.Any(identity =>
                sidecar.Identities.Contains(identity, StringComparer.OrdinalIgnoreCase));
        }

        return !string.IsNullOrWhiteSpace(sidecar.Name) &&
               string.Equals(sidecar.Name, document.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<JsonObject> EnumerateInventoryItemObjects(JsonNode? root)
    {
        if (root is JsonArray rootArray)
        {
            foreach (var item in rootArray.OfType<JsonObject>())
                yield return item;
            yield break;
        }

        if (root is not JsonObject obj)
            yield break;

        foreach (var propertyName in new[] { "items", "UpdateInventory" })
        {
            if (obj[propertyName] is not JsonArray items)
                continue;

            foreach (var item in items.OfType<JsonObject>())
                yield return item;
        }
    }

    private static IReadOnlyList<ReadableInventoryTextEntry> CollectSidecarEntries(
        JsonNode? root,
        IReadOnlyList<string> collectionNames,
        Func<JsonObject, IReadOnlyList<string>> readText)
    {
        var result = new List<ReadableInventoryTextEntry>();
        foreach (var entry in EnumerateSidecarObjects(root, collectionNames))
        {
            var text = readText(entry);
            if (text.Count == 0)
                continue;

            var identities = ReadIdentityValues(entry, ItemIdentityFields);
            var name = FirstNonEmpty(ReadNodeString(entry, "itemName", "name")) ?? string.Empty;
            result.Add(new ReadableInventoryTextEntry(identities, name, text));
        }

        return result;
    }

    private static IEnumerable<JsonObject> EnumerateSidecarObjects(JsonNode? root, IReadOnlyList<string> collectionNames)
    {
        if (root is JsonArray rootArray)
        {
            foreach (var item in rootArray.OfType<JsonObject>())
                yield return item;
            yield break;
        }

        if (root is not JsonObject obj)
            yield break;

        foreach (var collectionName in collectionNames)
        {
            if (obj[collectionName] is not JsonArray entries)
                continue;

            foreach (var item in entries.OfType<JsonObject>())
                yield return item;
        }
    }

    private static Dictionary<string, List<string>> BuildEntryMap(
        IReadOnlyList<ReadableInventoryTextEntry> entries,
        Func<ReadableInventoryTextEntry, IReadOnlyList<string>> keySelector)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            foreach (var key in keySelector(entry))
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (!map.TryGetValue(key, out var values))
                {
                    values = [];
                    map[key] = values;
                }

                values.AddRange(entry.TextEntries);
            }
        }

        return map;
    }

    private static IReadOnlyList<string> ResolveSidecarText(
        IReadOnlyList<string> identities,
        string name,
        Dictionary<string, List<string>> byId,
        Dictionary<string, List<string>> byName)
    {
        var result = new List<string>();
        foreach (var identity in identities)
        {
            if (byId.TryGetValue(identity, out var text))
                result.AddRange(text);
        }

        if (result.Count > 0)
            return result;

        return !string.IsNullOrWhiteSpace(name) && byName.TryGetValue(name, out var fallbackText)
            ? fallbackText
            : [];
    }

    private static IReadOnlyList<string> ReadItemTextEntryText(JsonObject entry)
    {
        var result = new List<string>();
        result.AddRange(ReadStringValues(entry["textContent"]));
        result.AddRange(ReadStringValues(entry["textToAppend"]));
        result.AddRange(ReadStringValues(entry["content"]));
        result.AddRange(ReadStringValues(entry["text"]));
        return Deduplicate(result);
    }

    private static IReadOnlyList<string> ReadItemJournalEntryText(JsonObject entry)
    {
        var result = new List<string>();
        result.AddRange(ReadStringValues(entry["entryToAppend"]));
        result.AddRange(ReadStringValues(entry["entry"]));
        result.AddRange(ReadStringValues(entry["journal"]));
        foreach (var journalEntry in ReadJournalEntries(entry["journalEntries"]))
            result.Add(journalEntry);

        return Deduplicate(result);
    }

    private static IEnumerable<string> ReadJournalEntries(JsonNode? node)
    {
        foreach (var item in EnumerateNodeItems(node))
        {
            if (TryGetScalarString(item, out var scalar) && !string.IsNullOrWhiteSpace(scalar))
            {
                yield return scalar;
                continue;
            }

            if (item is JsonObject obj)
            {
                var text = FirstNonEmpty(ReadNodeString(obj, "description", "text", "event", "spiritVoice"));
                if (!string.IsNullOrWhiteSpace(text))
                    yield return text;
            }
        }
    }

    private static IReadOnlyList<string> ReadStringValues(JsonNode? node)
    {
        var result = new List<string>();
        foreach (var item in EnumerateNodeItems(node))
        {
            if (TryGetScalarString(item, out var text) && !string.IsNullOrWhiteSpace(text))
                result.Add(text.Trim());
        }

        return result;
    }

    private static IEnumerable<JsonNode?> EnumerateNodeItems(JsonNode? node)
    {
        if (node == null || node.GetValueKind() == JsonValueKind.Null)
            yield break;

        if (node is JsonArray array)
        {
            foreach (var item in array)
                yield return item;
            yield break;
        }

        yield return node;
    }

    private static bool IsDocumentLikeItem(JsonObject item)
    {
        foreach (var value in ReadDocumentClassificationValues(item))
        {
            if (ContainsDocumentTerm(value))
                return true;
        }

        return false;
    }

    private static IEnumerable<string> ReadDocumentClassificationValues(JsonObject item)
    {
        foreach (var value in ReadNodeString(item, "type", "group", "name", "itemName", "category", "itemType"))
        {
            if (!string.IsNullOrWhiteSpace(value))
                yield return value;
        }

        foreach (var metadataProperty in new[] { "metadata", "tags", "keywords", "labels", "customProperties" })
        {
            foreach (var value in FlattenStringValues(item[metadataProperty]))
                yield return value;
        }
    }

    private static IEnumerable<string> FlattenStringValues(JsonNode? node)
    {
        if (node == null || node.GetValueKind() == JsonValueKind.Null)
            yield break;

        if (TryGetScalarString(node, out var scalar) && !string.IsNullOrWhiteSpace(scalar))
        {
            yield return scalar;
            yield break;
        }

        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                foreach (var value in FlattenStringValues(item))
                    yield return value;
            }
            yield break;
        }

        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                foreach (var value in FlattenStringValues(property.Value))
                    yield return value;
            }
        }
    }

    private static bool ContainsDocumentTerm(string value)
    {
        foreach (var term in DocumentTerms)
        {
            if (value.Contains(term, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string? ReadUnreadableReason(JsonObject item)
    {
        var directReason = FirstNonEmpty(ReadNodeString(item, ReasonFields));
        if (!string.IsNullOrWhiteSpace(directReason))
            return directReason;

        foreach (var stateProperty in new[] { "readableState", "readingState", "unreadableState", "sealedState" })
        {
            if (item[stateProperty] is JsonObject state)
            {
                var nestedReason = FirstNonEmpty(ReadNodeString(state, ReasonFields.Concat(["reason"]).ToArray()));
                if (!string.IsNullOrWhiteSpace(nestedReason))
                    return nestedReason;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ReadIdentityValues(JsonObject obj, IReadOnlyList<string> propertyNames)
    {
        var result = new List<string>();
        foreach (var value in ReadNodeString(obj, propertyNames))
        {
            if (!string.IsNullOrWhiteSpace(value))
                result.Add(value);
        }

        return Deduplicate(result);
    }

    private static IEnumerable<string> ReadNodeString(JsonObject obj, params string[] propertyNames) =>
        ReadNodeString(obj, (IReadOnlyList<string>)propertyNames);

    private static IEnumerable<string> ReadNodeString(JsonObject obj, IReadOnlyList<string> propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryGetScalarString(obj[propertyName], out var value) && !string.IsNullOrWhiteSpace(value))
                yield return value.Trim();
        }
    }

    private static bool TryGetScalarString(JsonNode? node, out string value)
    {
        value = string.Empty;
        if (node is not JsonValue jsonValue)
            return false;

        if (jsonValue.TryGetValue<string>(out var text))
        {
            value = text;
            return true;
        }

        if (jsonValue.TryGetValue<int>(out var intValue))
        {
            value = intValue.ToString();
            return true;
        }

        if (jsonValue.TryGetValue<long>(out var longValue))
        {
            value = longValue.ToString();
            return true;
        }

        if (jsonValue.TryGetValue<bool>(out var boolValue))
        {
            value = boolValue ? "true" : "false";
            return true;
        }

        return false;
    }

    private static string? FirstNonEmpty(IEnumerable<string> values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));

    private static IReadOnlyList<string> Deduplicate(IEnumerable<string> values) =>
        values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}

internal sealed record ReadableInventoryDocument(
    IReadOnlyList<string> Identities,
    string Name,
    string ContextIdentity,
    IReadOnlyList<string> TextEntries,
    string? UnreadableReason)
{
    public bool HasReadableAuthority => TextEntries.Count > 0;
    public bool HasUnreadableReason => !string.IsNullOrWhiteSpace(UnreadableReason);
}

internal sealed record ReadableInventoryTextEntry(
    IReadOnlyList<string> Identities,
    string Name,
    IReadOnlyList<string> TextEntries);
