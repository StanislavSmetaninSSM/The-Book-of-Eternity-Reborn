using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class AfterlifeChronicleState
{
    public const string StatePath = "game_state/meta/afterlife_chronicles.json";
    public const string ChroniclesProperty = "chronicles";
    public const string UpdateProperty = "afterlifeChronicleUpdates";
    public const string LastInvalidUpdateProperty = "lastInvalidChronicleUpdate";
    public const string LastInvalidUpdateReasonProperty = "lastInvalidChronicleUpdateReason";
    public const int SchemaVersion = 1;

    public static readonly HashSet<string> ScopeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "chaos_sea_region",
        "shining_abode_district",
        "guardian_abode",
        "guardian_scene",
        "resident_scene",
        "faction_zone",
        "memory_scene",
        "source_of_light",
        "saref_story"
    };

    public static JsonObject CreateDefaultRoot() =>
        new()
        {
            ["schemaVersion"] = SchemaVersion,
            [ChroniclesProperty] = new JsonArray()
        };

    public static JsonObject ProjectCanonicalRoot(JsonObject? currentRoot, JsonObject? previousRoot)
    {
        var result = CreateDefaultRoot();
        UpsertChronicles(result, previousRoot?[ChroniclesProperty]);
        UpsertChronicles(result, currentRoot?[ChroniclesProperty]);
        ApplyChronicleUpdates(result, currentRoot?[UpdateProperty]);
        result.Remove(UpdateProperty);
        return result;
    }

    private static void ApplyChronicleUpdates(JsonObject result, JsonNode? updatesNode)
    {
        if (updatesNode == null)
            return;

        if (updatesNode is JsonObject singleUpdate)
        {
            ApplyChronicleUpdate(result, singleUpdate);
            return;
        }

        if (updatesNode is not JsonArray updates)
        {
            result[LastInvalidUpdateProperty] = updatesNode.DeepClone();
            result[LastInvalidUpdateReasonProperty] = "afterlifeChronicleUpdates must be an object or array";
            return;
        }

        foreach (var update in updates)
        {
            if (update is JsonObject updateObject)
            {
                ApplyChronicleUpdate(result, updateObject);
                continue;
            }

            result[LastInvalidUpdateProperty] = update?.DeepClone();
            result[LastInvalidUpdateReasonProperty] = "afterlifeChronicleUpdates entries must be objects";
        }
    }

    private static void ApplyChronicleUpdate(JsonObject result, JsonObject update)
    {
        var chronicles = EnsureChronicles(result);
        var chronicleId = GetNodeString(update["chronicleId"]);
        if (string.IsNullOrWhiteSpace(chronicleId))
        {
            result[LastInvalidUpdateProperty] = update.DeepClone();
            result[LastInvalidUpdateReasonProperty] = "chronicleId is required";
            return;
        }

        var existing = FindChronicle(chronicles, chronicleId);
        var replacement = existing?.DeepClone().AsObject() ?? new JsonObject
        {
            ["chronicleId"] = chronicleId,
            ["eventDescriptions"] = new JsonArray()
        };

        ArchivePreviousLastEventsDescription(replacement, update["lastEventsDescription"]);
        CopyIfPresent(update, replacement, "scopeType");
        CopyIfPresent(update, replacement, "scopeId");
        CopyIfPresent(update, replacement, "displayName");
        CopyIfPresent(update, replacement, "lastEventsDescription");
        CopyIfPresent(update, replacement, "persistentConsequences");
        CopyIfPresent(update, replacement, "openThreads");
        CopyIfPresent(update, replacement, "lastUpdatedTurn");

        if (existing == null)
        {
            chronicles.Add(replacement);
            return;
        }

        for (var index = 0; index < chronicles.Count; index++)
        {
            if (chronicles[index] is JsonObject candidate &&
                string.Equals(GetNodeString(candidate["chronicleId"]), chronicleId, StringComparison.OrdinalIgnoreCase))
            {
                chronicles[index] = replacement;
                return;
            }
        }
    }

    private static void UpsertChronicles(JsonObject result, JsonNode? chroniclesNode)
    {
        if (chroniclesNode is not JsonArray chronicles)
            return;

        var resultChronicles = EnsureChronicles(result);
        foreach (var chronicle in chronicles.OfType<JsonObject>())
            UpsertChronicle(resultChronicles, chronicle);
    }

    private static void UpsertChronicle(JsonArray chronicles, JsonObject chronicle)
    {
        var chronicleId = GetNodeString(chronicle["chronicleId"]);
        if (string.IsNullOrWhiteSpace(chronicleId))
        {
            chronicles.Add(chronicle.DeepClone());
            return;
        }

        for (var index = 0; index < chronicles.Count; index++)
        {
            if (chronicles[index] is JsonObject existing &&
                string.Equals(GetNodeString(existing["chronicleId"]), chronicleId, StringComparison.OrdinalIgnoreCase))
            {
                chronicles[index] = chronicle.DeepClone();
                return;
            }
        }

        chronicles.Add(chronicle.DeepClone());
    }

    private static JsonObject? FindChronicle(JsonArray chronicles, string chronicleId)
    {
        foreach (var chronicle in chronicles.OfType<JsonObject>())
        {
            if (string.Equals(GetNodeString(chronicle["chronicleId"]), chronicleId, StringComparison.OrdinalIgnoreCase))
                return chronicle;
        }

        return null;
    }

    private static void ArchivePreviousLastEventsDescription(JsonObject replacement, JsonNode? newLastEventsNode)
    {
        var previousLastEvents = GetNodeString(replacement["lastEventsDescription"]);
        var newLastEvents = GetNodeString(newLastEventsNode);
        if (string.IsNullOrWhiteSpace(previousLastEvents) ||
            string.Equals(previousLastEvents, newLastEvents, StringComparison.Ordinal))
        {
            return;
        }

        var events = EnsureArray(replacement, "eventDescriptions");
        if (!events.OfType<JsonValue>().Any(value =>
                string.Equals(value.GetValue<string>(), previousLastEvents, StringComparison.Ordinal)))
        {
            events.Add(previousLastEvents);
        }
    }

    private static JsonArray EnsureChronicles(JsonObject root) =>
        EnsureArray(root, ChroniclesProperty);

    private static JsonArray EnsureArray(JsonObject root, string propertyName)
    {
        if (root[propertyName] is JsonArray existing)
            return existing;

        var arr = new JsonArray();
        root[propertyName] = arr;
        return arr;
    }

    private static void CopyIfPresent(JsonObject source, JsonObject target, string propertyName)
    {
        if (!source.TryGetPropertyValue(propertyName, out var value))
            return;

        target[propertyName] = value?.DeepClone();
    }

    public static string? GetNodeString(JsonNode? node)
    {
        if (node == null)
            return null;

        if (node is JsonValue value && value.TryGetValue<string>(out var result))
            return string.IsNullOrWhiteSpace(result) ? null : result.Trim();

        return null;
    }
}
