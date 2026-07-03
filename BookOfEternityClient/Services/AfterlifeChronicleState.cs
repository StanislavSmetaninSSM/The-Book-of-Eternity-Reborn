using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

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

    private static readonly (Regex Pattern, string Replacement)[] PlayerFacingRealmTermReplacements =
    {
        (new Regex(@"\bMortal\s*World\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "смертный мир"),
        (new Regex(@"\bChaos\s*Sea\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "Море Хаоса"),
        (new Regex(@"\bShining\s*Abode\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "Сияющая Обитель"),
        (new Regex(@"\bafterlife\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "посмертие")
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
        NormalizePlayerFacingChronicleText(result);
        return result;
    }

    private static void NormalizePlayerFacingChronicleText(JsonObject root)
    {
        if (root[ChroniclesProperty] is not JsonArray chronicles)
            return;

        foreach (var chronicle in chronicles.OfType<JsonObject>())
            NormalizeChroniclePlayerFacingText(chronicle);
    }

    private static void NormalizeChroniclePlayerFacingText(JsonObject chronicle)
    {
        NormalizeStringProperty(chronicle, "displayName");
        NormalizeStringProperty(chronicle, "lastEventsDescription");
        NormalizeStringArrayProperty(chronicle, "eventDescriptions");
        NormalizeStringArrayProperty(chronicle, "persistentConsequences");
        NormalizeStringArrayProperty(chronicle, "openThreads");

        if (chronicle["participants"] is not JsonArray participants)
            return;

        foreach (var participant in participants.OfType<JsonObject>())
            NormalizeStringProperty(participant, "displayName");
    }

    private static void NormalizeStringProperty(JsonObject root, string propertyName)
    {
        if (!root.TryGetPropertyValue(propertyName, out var node) ||
            node is not JsonValue value ||
            !value.TryGetValue<string>(out var text))
        {
            return;
        }

        var normalized = NormalizePlayerFacingRealmTerms(text);
        if (!string.Equals(normalized, text, StringComparison.Ordinal))
            root[propertyName] = normalized;
    }

    private static void NormalizeStringArrayProperty(JsonObject root, string propertyName)
    {
        if (root[propertyName] is not JsonArray values)
            return;

        for (var index = 0; index < values.Count; index++)
        {
            if (values[index] is not JsonValue value ||
                !value.TryGetValue<string>(out var text))
            {
                continue;
            }

            var normalized = NormalizePlayerFacingRealmTerms(text);
            if (!string.Equals(normalized, text, StringComparison.Ordinal))
                values[index] = normalized;
        }
    }

    private static string NormalizePlayerFacingRealmTerms(string text)
    {
        var result = text;
        foreach (var (pattern, replacement) in PlayerFacingRealmTermReplacements)
            result = pattern.Replace(result, replacement);

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
        var replacement = chronicle.DeepClone().AsObject();
        EnsureCanonicalEventArchive(replacement);

        var chronicleId = GetNodeString(chronicle["chronicleId"]);
        if (string.IsNullOrWhiteSpace(chronicleId))
        {
            chronicles.Add(replacement);
            return;
        }

        for (var index = 0; index < chronicles.Count; index++)
        {
            if (chronicles[index] is JsonObject existing &&
                string.Equals(GetNodeString(existing["chronicleId"]), chronicleId, StringComparison.OrdinalIgnoreCase))
            {
                MergeCanonicalEventArchive(replacement, existing);
                chronicles[index] = replacement;
                return;
            }
        }

        chronicles.Add(replacement);
    }

    private static void EnsureCanonicalEventArchive(JsonObject chronicle)
    {
        if (!chronicle.ContainsKey("eventDescriptions"))
            chronicle["eventDescriptions"] = new JsonArray();
    }

    private static void MergeCanonicalEventArchive(JsonObject replacement, JsonObject existing)
    {
        if (!replacement.ContainsKey("eventDescriptions"))
            replacement["eventDescriptions"] = new JsonArray();

        if (replacement["eventDescriptions"] is not JsonArray replacementEvents)
            return;

        if (existing["eventDescriptions"] is JsonArray existingEvents)
        {
            foreach (var existingEvent in existingEvents.OfType<JsonValue>())
            {
                if (existingEvent.TryGetValue<string>(out var eventText))
                    AddUniqueEventDescription(replacementEvents, eventText);
            }
        }

        var previousLastEvents = GetNodeString(existing["lastEventsDescription"]);
        var newLastEvents = GetNodeString(replacement["lastEventsDescription"]);
        if (!string.Equals(previousLastEvents, newLastEvents, StringComparison.Ordinal))
            AddUniqueEventDescription(replacementEvents, previousLastEvents);
    }

    private static void AddUniqueEventDescription(JsonArray events, string? eventText)
    {
        if (string.IsNullOrWhiteSpace(eventText))
            return;

        var normalized = eventText.Trim();
        if (events.OfType<JsonValue>().Any(value =>
                value.TryGetValue<string>(out var existing) &&
                string.Equals(existing, normalized, StringComparison.Ordinal)))
        {
            return;
        }

        events.Add(normalized);
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
