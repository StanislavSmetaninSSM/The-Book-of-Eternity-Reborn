using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

internal static class MortalItemPlayerProjection
{
    private static readonly HashSet<string> InternalAuthorityFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "materialization",
        "materializationReceipt",
        "creationRef",
        "itemCreationRef",
        "materializationId",
        "image_prompt",
        "sourceAuthority",
        "sourceTurn",
        "receipt",
        "receiptId",
        "receiptSeal",
        "seal",
        "schemaVersion",
        "instanceKind",
        "parentItemIds",
        "parentItemId",
        "itemId",
        "itemIds",
        "itemRef",
        "existedId",
        "initialId",
        "clientAssignedItemId",
        "clientAuthority",
        "identityAuthority",
        "identityIndex",
        "itemIdentityIndex",
        "identityEntry",
        "indexEntry",
        "originMaterializationIds",
        "originCreationRefs",
        "lineage",
        "currentCarrier",
        "carrierCoordinate",
        "carrierPath",
        "ownerId",
        "containerId",
        "containerItemId",
        "containerPath",
        "contentsPath",
        "currentLocationId",
        "sourceCarrier",
        "destinationCarrier",
        "sourceItemId",
        "targetItemId",
        "targetItemIds",
        "rewardItemId",
        "destinationItemId",
        "resultItemId",
        "removedItemId",
        "destinationContainerId",
        "currentContentsPath",
        "transitions",
        "transitionId",
        "sourceItemIds",
        "quantityBefore",
        "quantityAfter",
        "mergedIntoItemId",
        "equippedItems",
        "equipment",
        "equipmentSlots",
        "authorityKind",
        "authorityId",
        "requestId",
        "slotId",
        "tradeCycleId",
        "rewardId",
        "acceptedAtTurn",
        "repairPacket",
        "repairRequest",
        "repairTargets",
        "expectedAuthority",
        "actualEvidence",
        "targetFiles",
        "canonicalActorNames",
        "missingFields",
        "exactFieldCorrections",
        "requiredCompanionTargets",
        "templateRefs",
        "expectedShape",
        "safeCorrectionRules",
        "transitionClass",
        "repairHint",
        "validationCode",
        "validationCodes",
        "validationIssue",
        "validationIssues",
        "filePath",
        "sourcePath",
        "targetPath",
        "UpdateInventory",
        "NPCInventoryAdds",
        "UpdateNpcTradeInventoryReceipts",
        "lootForCurrentTurn",
        "removeInventoryItems",
        "NPCInventoryRemovals"
    };

    private static readonly HashSet<string> RepairPacketKinds = new(StringComparer.Ordinal)
    {
        "mortal_item_materialization_repair",
        "mortal_item_identity_authority_repair"
    };

    private static readonly HashSet<string> RepairPacketSignatureFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "targetFiles",
        "templateRefs",
        "canonicalActorNames",
        "transitionClass",
        "missingFields",
        "exactFieldCorrections",
        "requiredCompanionTargets",
        "expectedAuthority",
        "actualEvidence",
        "expectedShape",
        "safeCorrectionRules"
    };

    private static readonly HashSet<string> TransitionSignatureFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "sourceItemIds",
        "sourceCarrier",
        "destinationCarrier",
        "quantityBefore",
        "quantityAfter",
        "authorityKind",
        "authorityId"
    };

    private static readonly HashSet<string> MaterializationEnvelopeFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "schemaVersion",
        "materializationId",
        "realm",
        "route",
        "sourceTurn",
        "sourceAuthority",
        "creationRef",
        "state",
        "sections"
    };

    private static readonly HashSet<string> IdentityEntryFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "itemId",
        "receiptId",
        "state",
        "currentCarrier",
        "originMaterializationIds",
        "originCreationRefs",
        "parentItemIds",
        "mergedIntoItemId",
        "transitions"
    };

    private static readonly HashSet<string> CarrierCoordinateFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "kind",
        "ownerId",
        "containerId",
        "containerPath"
    };

    private static readonly HashSet<string> SourceAuthorityFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "kind",
        "authorityId"
    };

    internal static bool IsInternalField(string? fieldName) =>
        !string.IsNullOrWhiteSpace(fieldName) && InternalAuthorityFieldNames.Contains(fieldName);

    internal static bool IsInternalItemField(string? fieldName) =>
        string.Equals(fieldName, "route", StringComparison.OrdinalIgnoreCase) ||
        IsInternalField(fieldName);

    internal static bool IsInternalItemDto(JsonElement value) =>
        value.ValueKind == JsonValueKind.Object && IsInternalItemDtoShape(value);

    internal static bool TryReadExactSidecarItemId(JsonElement entry, out string itemId) =>
        MortalItemIdentityRules.TryReadExactSidecarItemId(entry, out itemId);

    internal static bool TryReadExactSidecarItemId(JsonObject entry, out string itemId) =>
        MortalItemIdentityRules.TryReadExactSidecarItemId(entry, out itemId);

    internal static JsonElement? FindUniqueExactSidecarEntry(JsonElement root, string itemIdentity)
    {
        if (string.IsNullOrWhiteSpace(itemIdentity) ||
            root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("entries", out var entries) ||
            entries.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        JsonElement? match = null;
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object ||
                !TryReadExactSidecarItemId(entry, out var entryIdentity) ||
                !string.Equals(entryIdentity, itemIdentity, StringComparison.Ordinal))
            {
                continue;
            }

            if (match.HasValue)
                return null;
            match = entry;
        }

        return match;
    }

    internal static JsonObject? FindUniqueExactSidecarEntry(JsonNode? root, string itemIdentity)
    {
        if (string.IsNullOrWhiteSpace(itemIdentity) ||
            root is not JsonObject obj ||
            obj["entries"] is not JsonArray entries)
        {
            return null;
        }

        JsonObject? match = null;
        foreach (var entry in entries.OfType<JsonObject>())
        {
            if (!TryReadExactSidecarItemId(entry, out var entryIdentity) ||
                !string.Equals(entryIdentity, itemIdentity, StringComparison.Ordinal))
            {
                continue;
            }

            if (match != null)
                return null;
            match = entry;
        }

        return match;
    }

    internal static JsonNode? CloneSemanticValue(JsonNode? node) =>
        CloneSemanticValue(node, itemContext: false);

    internal static JsonNode? CloneItemSemanticValue(JsonNode? node) =>
        CloneSemanticValue(node, itemContext: true);

    private static JsonNode? CloneSemanticValue(JsonNode? node, bool itemContext)
    {
        return node switch
        {
            null => null,
            JsonObject obj when itemContext && IsInternalItemDtoShape(obj) => null,
            JsonObject obj => CloneSemanticObject(obj, itemContext),
            JsonArray array => CloneSemanticArray(array, itemContext),
            _ => node.DeepClone()
        };
    }

    internal static string FormatSemanticValue(JsonElement value, string? fieldName = null)
    {
        if (IsInternalItemField(fieldName))
            return string.Empty;
        if (value.ValueKind == JsonValueKind.Object && IsInternalItemDtoShape(value))
            return string.Empty;

        return value.ValueKind switch
        {
            JsonValueKind.Array => string.Join("; ", value.EnumerateArray()
                .Select(item => FormatSemanticValue(item, fieldName))
                .Where(static item => !string.IsNullOrWhiteSpace(item))),
            JsonValueKind.Object => string.Join("; ", value.EnumerateObject()
                .Where(property => !IsInternalItemField(property.Name))
                .Select(property => FormatFieldValue(
                    property.Name,
                    FormatSemanticValue(property.Value, property.Name)))
                .Where(static item => !string.IsNullOrWhiteSpace(item))),
            _ => StructuredBonusDisplay.FormatValue(value, fieldName)
        };
    }

    private static JsonObject CloneSemanticObject(JsonObject source, bool itemContext)
    {
        var result = new JsonObject();
        foreach (var property in source)
        {
            var isInternal = itemContext
                ? IsInternalItemField(property.Key)
                : IsInternalField(property.Key);
            if (isInternal)
                continue;

            var projected = CloneSemanticValue(property.Value, itemContext);
            if (property.Value != null && projected == null)
                continue;
            result[property.Key] = projected;
        }

        return result;
    }

    private static JsonArray CloneSemanticArray(JsonArray source, bool itemContext)
    {
        var result = new JsonArray();
        foreach (var value in source)
        {
            var projected = CloneSemanticValue(value, itemContext);
            if (value != null && projected == null)
                continue;
            result.Add(projected);
        }

        return result;
    }

    private static bool IsInternalItemDtoShape(JsonObject source)
    {
        var fields = new HashSet<string>(
            source.Select(static property => property.Key),
            StringComparer.OrdinalIgnoreCase);
        return IsInternalItemDtoShape(fields, TryReadString(source, "kind")) ||
               ContainsIdentityIndexEntry(source);
    }

    private static bool IsInternalItemDtoShape(JsonElement source)
    {
        var fields = new HashSet<string>(
            source.EnumerateObject().Select(static property => property.Name),
            StringComparer.OrdinalIgnoreCase);
        return IsInternalItemDtoShape(fields, TryReadString(source, "kind")) ||
               ContainsIdentityIndexEntry(source);
    }

    private static bool IsInternalItemDtoShape(HashSet<string> fields, string? kind)
    {
        if (kind != null && RepairPacketKinds.Contains(kind))
            return true;
        if (fields.Count(RepairPacketSignatureFieldNames.Contains) >= 2)
            return true;
        if (fields.Contains("transitionId") &&
            fields.Count(TransitionSignatureFieldNames.Contains) >= 4)
            return true;
        if (fields.IsSupersetOf(MaterializationEnvelopeFieldNames))
            return true;
        if (fields.IsSupersetOf(IdentityEntryFieldNames))
            return true;
        if (fields.IsSupersetOf(SourceAuthorityFieldNames))
            return true;

        return fields.IsSupersetOf(CarrierCoordinateFieldNames);
    }

    private static bool ContainsIdentityIndexEntry(JsonObject source)
    {
        var entries = source.FirstOrDefault(property =>
            string.Equals(property.Key, "entries", StringComparison.OrdinalIgnoreCase)).Value as JsonArray;
        return entries != null && entries.OfType<JsonObject>().Any(entry =>
        {
            var fields = new HashSet<string>(
                entry.Select(static property => property.Key),
                StringComparer.OrdinalIgnoreCase);
            return fields.IsSupersetOf(IdentityEntryFieldNames);
        });
    }

    private static bool ContainsIdentityIndexEntry(JsonElement source)
    {
        foreach (var property in source.EnumerateObject())
        {
            if (!string.Equals(property.Name, "entries", StringComparison.OrdinalIgnoreCase) ||
                property.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            return property.Value.EnumerateArray().Any(entry =>
            {
                if (entry.ValueKind != JsonValueKind.Object)
                    return false;
                var fields = new HashSet<string>(
                    entry.EnumerateObject().Select(static field => field.Name),
                    StringComparer.OrdinalIgnoreCase);
                return fields.IsSupersetOf(IdentityEntryFieldNames);
            });
        }

        return false;
    }

    private static string? TryReadString(JsonObject source, string fieldName)
    {
        var value = source.FirstOrDefault(property =>
            string.Equals(property.Key, fieldName, StringComparison.OrdinalIgnoreCase)).Value;
        return value is JsonValue scalar && scalar.TryGetValue<string>(out var text)
            ? text
            : null;
    }

    private static string? TryReadString(JsonElement source, string fieldName)
    {
        foreach (var property in source.EnumerateObject())
        {
            if (string.Equals(property.Name, fieldName, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }

    private static string FormatFieldValue(string fieldName, string value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : $"{StructuredBonusDisplay.FieldLabel(fieldName)}: {value}";
}
