using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class AfterlifeGlobalFlagState
{
    public const string StatePath = "game_state/meta/afterlife_global_flags.json";
    public const string FlagsProperty = "flags";
    public const string UpdateProperty = "afterlifeGlobalFlagUpdates";
    public const string LastInvalidUpdateProperty = "lastInvalidGlobalFlagUpdate";
    public const string LastInvalidUpdateReasonProperty = "lastInvalidGlobalFlagUpdateReason";
    public const int SchemaVersion = 1;

    public static readonly HashSet<string> Categories = new(StringComparer.OrdinalIgnoreCase)
    {
        "saref",
        "source_of_light",
        "guardian_memory",
        "chaos_politics",
        "shining_politics",
        "soul_dissipation",
        "realm_lifecycle",
        "relationship_gate"
    };

    public static readonly HashSet<string> States = new(StringComparer.OrdinalIgnoreCase)
    {
        "active",
        "resolved",
        "obsolete"
    };

    public static readonly HashSet<string> Visibilities = new(StringComparer.OrdinalIgnoreCase)
    {
        "visible",
        "hidden",
        "gm_only"
    };

    public static JsonObject CreateDefaultRoot() =>
        new()
        {
            ["schemaVersion"] = SchemaVersion,
            [FlagsProperty] = new JsonArray()
        };

    public static JsonObject ProjectCanonicalRoot(JsonObject? currentRoot, JsonObject? previousRoot)
    {
        var result = CreateDefaultRoot();
        UpsertFlags(result, previousRoot?[FlagsProperty]);
        UpsertFlags(result, currentRoot?[FlagsProperty]);
        ApplyFlagUpdates(result, currentRoot?[UpdateProperty]);
        result.Remove(UpdateProperty);
        return result;
    }

    private static void ApplyFlagUpdates(JsonObject result, JsonNode? updatesNode)
    {
        if (updatesNode == null)
            return;

        if (updatesNode is JsonObject singleUpdate)
        {
            ApplyFlagUpdate(result, singleUpdate);
            return;
        }

        if (updatesNode is not JsonArray updates)
        {
            result[LastInvalidUpdateProperty] = updatesNode.DeepClone();
            result[LastInvalidUpdateReasonProperty] = "afterlifeGlobalFlagUpdates must be an object or array";
            return;
        }

        foreach (var update in updates)
        {
            if (update is JsonObject updateObject)
            {
                ApplyFlagUpdate(result, updateObject);
                continue;
            }

            result[LastInvalidUpdateProperty] = update?.DeepClone();
            result[LastInvalidUpdateReasonProperty] = "afterlifeGlobalFlagUpdates entries must be objects";
        }
    }

    private static void ApplyFlagUpdate(JsonObject result, JsonObject update)
    {
        var flags = EnsureFlags(result);
        var flagId = GetNodeString(update["flagId"]);
        if (string.IsNullOrWhiteSpace(flagId))
        {
            result[LastInvalidUpdateProperty] = update.DeepClone();
            result[LastInvalidUpdateReasonProperty] = "flagId is required";
            return;
        }

        var existing = FindFlag(flags, flagId);
        var replacement = existing?.DeepClone().AsObject() ?? new JsonObject
        {
            ["flagId"] = flagId,
            ["linkedActors"] = new JsonArray(),
            ["linkedChronicles"] = new JsonArray()
        };

        CopyIfPresent(update, replacement, "category");
        CopyIfPresent(update, replacement, "state");
        CopyIfPresent(update, replacement, "visibility");
        CopyIfPresent(update, replacement, "createdAtTurn");
        CopyIfPresent(update, replacement, "updatedAtTurn");
        CopyIfPresent(update, replacement, "reason");
        CopyIfPresent(update, replacement, "evidence");
        CopyIfPresent(update, replacement, "linkedActors");
        CopyIfPresent(update, replacement, "linkedChronicles");
        CopyIfPresent(update, replacement, "obsoleteReason");

        if (existing == null)
        {
            flags.Add(replacement);
            return;
        }

        for (var index = 0; index < flags.Count; index++)
        {
            if (flags[index] is JsonObject candidate &&
                string.Equals(GetNodeString(candidate["flagId"]), flagId, StringComparison.OrdinalIgnoreCase))
            {
                flags[index] = replacement;
                return;
            }
        }
    }

    private static void UpsertFlags(JsonObject result, JsonNode? flagsNode)
    {
        if (flagsNode is not JsonArray flags)
            return;

        var resultFlags = EnsureFlags(result);
        foreach (var flag in flags.OfType<JsonObject>())
            UpsertFlag(resultFlags, flag);
    }

    private static void UpsertFlag(JsonArray flags, JsonObject flag)
    {
        var flagId = GetNodeString(flag["flagId"]);
        if (string.IsNullOrWhiteSpace(flagId))
        {
            flags.Add(flag.DeepClone());
            return;
        }

        for (var index = 0; index < flags.Count; index++)
        {
            if (flags[index] is JsonObject existing &&
                string.Equals(GetNodeString(existing["flagId"]), flagId, StringComparison.OrdinalIgnoreCase))
            {
                flags[index] = flag.DeepClone();
                return;
            }
        }

        flags.Add(flag.DeepClone());
    }

    private static JsonObject? FindFlag(JsonArray flags, string flagId)
    {
        foreach (var flag in flags.OfType<JsonObject>())
        {
            if (string.Equals(GetNodeString(flag["flagId"]), flagId, StringComparison.OrdinalIgnoreCase))
                return flag;
        }

        return null;
    }

    public static JsonObject? FindFlag(JsonObject? root, string flagId)
    {
        if (root?[FlagsProperty] is not JsonArray flags)
            return null;

        return FindFlag(flags, flagId);
    }

    private static JsonArray EnsureFlags(JsonObject root) =>
        EnsureArray(root, FlagsProperty);

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
