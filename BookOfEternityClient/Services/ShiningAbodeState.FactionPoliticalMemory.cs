using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class ShiningAbodeState
{
    public static void ApplyFactionPoliticalUpdateSurfaces(JsonObject root)
    {
        ApplyFactionChronicleUpdates(root);
        ApplyFactionInfluenceUpdates(root);
        ApplyFactionStrategicMemoryUpdates(root);
        ApplyFactionResourceLedgerUpdates(root);

        root.Remove(FactionChronicleUpdatesProperty);
        root.Remove(FactionInfluenceUpdatesProperty);
        root.Remove(FactionStrategicMemoryUpdatesProperty);
        root.Remove(FactionResourceLedgerUpdatesProperty);
    }

    private static void ApplyFactionChronicleUpdates(JsonObject root)
    {
        foreach (var update in EnumerateFactionPoliticalCommands(root, FactionChronicleUpdatesProperty))
        {
            var faction = ResolveFactionForPoliticalUpdate(root, update, FactionChronicleUpdatesProperty);
            if (faction == null)
                continue;

            var entry = CloneCommandWithoutFactionId(update);
            UpsertObjectById(EnsureArray(faction, FactionChronicleProperty), entry, "entryId");
        }
    }

    private static void ApplyFactionInfluenceUpdates(JsonObject root)
    {
        foreach (var update in EnumerateFactionPoliticalCommands(root, FactionInfluenceUpdatesProperty))
        {
            var faction = ResolveFactionForPoliticalUpdate(root, update, FactionInfluenceUpdatesProperty);
            if (faction == null)
                continue;

            var zone = CloneCommandWithoutFactionId(update);
            UpsertObjectById(EnsureArray(faction, FactionInfluenceProperty), zone, "zoneId");
        }
    }

    private static void ApplyFactionStrategicMemoryUpdates(JsonObject root)
    {
        foreach (var update in EnumerateFactionPoliticalCommands(root, FactionStrategicMemoryUpdatesProperty))
        {
            var faction = ResolveFactionForPoliticalUpdate(root, update, FactionStrategicMemoryUpdatesProperty);
            if (faction == null)
                continue;

            var memory = faction[FactionStrategicMemoryProperty] as JsonObject ?? new JsonObject();
            foreach (var property in update)
            {
                if (property.Key.Equals("factionId", StringComparison.OrdinalIgnoreCase))
                    continue;

                memory[property.Key] = property.Value?.DeepClone();
            }

            faction[FactionStrategicMemoryProperty] = memory;
        }
    }

    private static void ApplyFactionResourceLedgerUpdates(JsonObject root)
    {
        foreach (var update in EnumerateFactionPoliticalCommands(root, FactionResourceLedgerUpdatesProperty))
        {
            var faction = ResolveFactionForPoliticalUpdate(root, update, FactionResourceLedgerUpdatesProperty);
            if (faction == null)
                continue;

            var entry = CloneCommandWithoutFactionId(update);
            UpsertObjectById(EnsureArray(faction, FactionResourceLedgerProperty), entry, "entryId");
        }
    }

    private static IEnumerable<JsonObject> EnumerateFactionPoliticalCommands(JsonObject root, string propertyName)
    {
        if (!root.TryGetPropertyValue(propertyName, out var node) || node == null)
            yield break;

        if (node is JsonObject single)
        {
            yield return single;
            yield break;
        }

        if (node is not JsonArray array)
        {
            MarkInvalidFactionPoliticalCommand(root, node, $"{propertyName} must be object or array");
            yield break;
        }

        foreach (var item in array)
        {
            if (item is JsonObject command)
            {
                yield return command;
                continue;
            }

            MarkInvalidFactionPoliticalCommand(root, item, $"{propertyName} entries must be objects");
        }
    }

    private static JsonObject? ResolveFactionForPoliticalUpdate(JsonObject root, JsonObject update, string propertyName)
    {
        var factionId = GetNodeString(update["factionId"]);
        if (string.IsNullOrWhiteSpace(factionId))
        {
            MarkInvalidFactionPoliticalCommand(root, update, $"{propertyName} requires factionId");
            return null;
        }

        var faction = FindFaction(root, factionId);
        if (faction != null)
            return faction;

        MarkInvalidFactionPoliticalCommand(root, update, $"{propertyName} target faction not found: {factionId}");
        return null;
    }

    private static JsonObject CloneCommandWithoutFactionId(JsonObject source)
    {
        var clone = CloneObject(source);
        clone.Remove("factionId");
        return clone;
    }

    private static void UpsertObjectById(JsonArray target, JsonObject source, string identityProperty)
    {
        var sourceId = GetNodeString(source[identityProperty]);
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            target.Add(source);
            return;
        }

        for (var index = 0; index < target.Count; index++)
        {
            if (target[index] is JsonObject existing &&
                string.Equals(GetNodeString(existing[identityProperty]), sourceId, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var property in source)
                    existing[property.Key] = property.Value?.DeepClone();
                return;
            }
        }

        target.Add(source);
    }

    private static void MarkInvalidFactionPoliticalCommand(JsonObject root, JsonNode? command, string reason)
    {
        root[LastInvalidFactionPoliticalCommandProperty] = command?.DeepClone();
        root[LastInvalidFactionPoliticalCommandReasonProperty] = reason;
    }
}
