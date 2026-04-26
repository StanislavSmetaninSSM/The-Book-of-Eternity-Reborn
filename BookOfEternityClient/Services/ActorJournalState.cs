using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class ActorJournalState
{
    public const string EntriesProperty = "entries";

    public static void NormalizeShape(JsonObject root, string actorIdProperty, string? updateProperty = null)
    {
        if (root[EntriesProperty] is not JsonArray entries)
        {
            if (!string.IsNullOrWhiteSpace(updateProperty) && root[updateProperty!] is JsonArray updates)
                root[EntriesProperty] = updates.DeepClone();
            else
                root[EntriesProperty] = new JsonArray();
        }

        if (root[EntriesProperty] is not JsonArray normalizedEntries)
            return;

        for (var i = normalizedEntries.Count - 1; i >= 0; i--)
        {
            if (normalizedEntries[i] is not JsonObject entry)
            {
                normalizedEntries.RemoveAt(i);
                continue;
            }

            NormalizeEntryObject(entry, actorIdProperty);
        }
    }

    public static JsonArray EnsureEntriesArray(JsonObject root, string actorIdProperty, string? updateProperty = null)
    {
        NormalizeShape(root, actorIdProperty, updateProperty);
        return root[EntriesProperty]!.AsArray();
    }

    public static void ApplyUpdates(JsonObject root, JsonArray updates, string actorIdProperty, string? updateProperty = null)
    {
        var entries = EnsureEntriesArray(root, actorIdProperty, updateProperty);
        foreach (var entry in updates.OfType<JsonObject>())
        {
            NormalizeEntryObject(entry, actorIdProperty);
            UpsertEntry(entries, entry);
        }
    }

    public static IEnumerable<JsonObject> CollectEntries(JsonNode? root, string actorIdProperty, string? updateProperty = null)
    {
        if (root is JsonObject obj)
        {
            if (obj[EntriesProperty] is JsonArray entries)
            {
                foreach (var entry in entries.OfType<JsonObject>())
                {
                    NormalizeEntryObject(entry, actorIdProperty);
                    yield return entry.DeepClone().AsObject();
                }
            }

            if (!string.IsNullOrWhiteSpace(updateProperty) && obj[updateProperty!] is JsonArray updates)
            {
                foreach (var entry in updates.OfType<JsonObject>())
                {
                    NormalizeEntryObject(entry, actorIdProperty);
                    yield return entry.DeepClone().AsObject();
                }
            }

            yield break;
        }

        if (root is JsonArray array)
        {
            foreach (var entry in array.OfType<JsonObject>())
            {
                NormalizeEntryObject(entry, actorIdProperty);
                yield return entry.DeepClone().AsObject();
            }
        }
    }

    public static IReadOnlyList<JsonObject> CollectEntriesForActor(JsonObject root, string actorIdProperty, string actorId)
    {
        var result = new List<JsonObject>();
        if (string.IsNullOrWhiteSpace(actorId) || root[EntriesProperty] is not JsonArray entries)
            return result;

        foreach (var entry in entries.OfType<JsonObject>())
        {
            if (!string.Equals(GetNodeString(entry[actorIdProperty]), actorId, StringComparison.OrdinalIgnoreCase))
                continue;

            result.Add(entry.DeepClone().AsObject());
        }

        return result;
    }

    public static JsonObject? FindResolutionEntry(JsonObject? root, string actorIdProperty, string actorId, string requestId)
    {
        if (root == null || string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(requestId))
            return null;

        if (root[EntriesProperty] is not JsonArray entries)
            return null;

        return entries.OfType<JsonObject>()
            .FirstOrDefault(entry =>
                string.Equals(GetNodeString(entry[actorIdProperty]), actorId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GetNodeString(entry["requestId"]), requestId, StringComparison.OrdinalIgnoreCase));
    }

    public static void NormalizeEntryObject(JsonObject entry, string actorIdProperty)
    {
        entry["entryId"] = JsonValue.Create(GetNodeString(entry["entryId"]));
        entry[actorIdProperty] = JsonValue.Create(GetNodeString(entry[actorIdProperty]));
        entry["title"] = JsonValue.Create(GetNodeString(entry["title"]));
        entry["summary"] = JsonValue.Create(GetNodeString(entry["summary"]));
        entry["eventType"] = JsonValue.Create(GetNodeString(entry["eventType"]));
        entry["consequence"] = JsonValue.Create(GetNodeString(entry["consequence"]));
        entry["attitude"] = JsonValue.Create(GetNodeString(entry["attitude"]));
        entry["intent"] = JsonValue.Create(GetNodeString(entry["intent"]));
        entry["timestamp"] = JsonValue.Create(GetNodeString(entry["timestamp"]));
        entry["turn"] = JsonValue.Create(GetNodeInt(entry["turn"]));
        var hasClosureMetadata =
            entry.ContainsKey("requestId") ||
            entry.ContainsKey("interactionType") ||
            entry.ContainsKey("status") ||
            entry.ContainsKey("responseMode");
        if (hasClosureMetadata)
        {
            entry["requestId"] = JsonValue.Create(GetNodeString(entry["requestId"]));
            entry["interactionType"] = JsonValue.Create(GetNodeString(entry["interactionType"]));
            entry["status"] = JsonValue.Create(GetNodeString(entry["status"]));
            entry["responseMode"] = JsonValue.Create(GetNodeString(entry["responseMode"]));
        }

        if (entry["tags"] is not JsonArray tags)
        {
            entry["tags"] = new JsonArray();
            return;
        }

        var normalizedTags = new JsonArray();
        foreach (var tag in tags.OfType<JsonValue>())
        {
            var value = GetNodeString(tag);
            if (!string.IsNullOrWhiteSpace(value))
                normalizedTags.Add(value);
        }

        entry["tags"] = normalizedTags;
    }

    public static JsonObject? FindEntry(JsonArray entries, string entryId)
    {
        if (string.IsNullOrWhiteSpace(entryId))
            return null;

        return entries.OfType<JsonObject>()
            .FirstOrDefault(entry => string.Equals(GetNodeString(entry["entryId"]), entryId, StringComparison.OrdinalIgnoreCase));
    }

    private static void UpsertEntry(JsonArray entries, JsonObject entry)
    {
        var entryId = GetNodeString(entry["entryId"]);
        if (string.IsNullOrWhiteSpace(entryId))
            return;

        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i] is not JsonObject existing)
                continue;

            if (!string.Equals(GetNodeString(existing["entryId"]), entryId, StringComparison.OrdinalIgnoreCase))
                continue;

            entries[i] = entry.DeepClone();
            return;
        }

        entries.Add(entry.DeepClone());
    }

    private static string GetNodeString(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var stringValue))
                return stringValue?.Trim() ?? string.Empty;

            return value.ToJsonString().Trim().Trim('"');
        }

        return string.Empty;
    }

    private static int GetNodeInt(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var intValue))
                return intValue;
            if (value.TryGetValue<long>(out var longValue))
                return (int)longValue;
            if (value.TryGetValue<string>(out var stringValue) && int.TryParse(stringValue, out var parsed))
                return parsed;
        }

        return 0;
    }
}
