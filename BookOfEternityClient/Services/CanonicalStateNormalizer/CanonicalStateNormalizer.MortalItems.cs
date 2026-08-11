using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private async Task NormalizeMortalItemsAsync(
        IReadOnlyDictionary<string, string>? backups)
    {
        _ = backups;

        var currentJson = await ReadCanonicalFileAsync(InventoryEquipmentService.ItemsPath);
        if (string.IsNullOrWhiteSpace(currentJson))
            return;

        JsonObject current;
        try
        {
            current = JsonNode.Parse(currentJson) as JsonObject ??
                      throw new InvalidDataException(
                          $"{InventoryEquipmentService.ItemsPath} must have an object root.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"{InventoryEquipmentService.ItemsPath} contains malformed JSON.",
                exception);
        }

        if (!current.TryGetPropertyValue("UpdateInventory", out var updateNode))
            return;
        if (updateNode is not JsonArray updates)
        {
            throw new InvalidDataException(
                $"{InventoryEquipmentService.ItemsPath}.UpdateInventory must be an array.");
        }

        var items = current["items"] as JsonArray ??
                    throw new InvalidDataException(
                        $"{InventoryEquipmentService.ItemsPath}.items must be an array before item sealing.");
        var acceptedTurn = await TryReadCurrentTurnNumberAsync();
        if (acceptedTurn < 1)
        {
            throw new InvalidOperationException(
                "Mortal item sealing requires a positive accepted turn number in input/turn_request.json.");
        }

        var indexJson = await ReadCanonicalFileAsync(MortalItemIdentityState.StatePath);
        var parsedIndex = MortalItemIdentityState.Parse(indexJson);
        if (parsedIndex.Issues.Count > 0)
        {
            throw new InvalidDataException(
                "Mortal item sealing requires a valid client-owned item identity index.");
        }

        var index = parsedIndex.Root.DeepClone().AsObject();
        var indexEntries = index["entries"]!.AsArray();
        var pending = new List<PendingPlayerItemCreation>(updates.Count);
        var creationMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var knownItemIds = new HashSet<string>(
            parsedIndex.EntriesByItemId.Keys,
            StringComparer.Ordinal);
        foreach (var item in items.OfType<JsonObject>())
        {
            var existingItemId = ReadExactMortalItemIdentity(item["itemId"]);
            if (existingItemId != null)
                knownItemIds.Add(existingItemId);
        }

        for (var updateIndex = 0; updateIndex < updates.Count; updateIndex++)
        {
            if (updates[updateIndex] is not JsonObject rawItem)
            {
                throw new InvalidDataException(
                    $"{InventoryEquipmentService.ItemsPath}.UpdateInventory[{updateIndex}] must be an object.");
            }

            EnsureRawPlayerItemCreation(rawItem, updateIndex, acceptedTurn);
            var creationRef = RequireExactMortalItemIdentity(
                rawItem["creationRef"],
                $"UpdateInventory[{updateIndex}].creationRef");
            if (creationMap.ContainsKey(creationRef))
            {
                throw new InvalidDataException(
                    $"Duplicate exact Mortal item creationRef '{creationRef}'.");
            }

            var itemId = CreateUniqueMortalItemId(knownItemIds);
            creationMap.Add(creationRef, itemId);
            pending.Add(new PendingPlayerItemCreation(rawItem, itemId));
        }

        foreach (var pendingCreation in pending)
        {
            var canonicalItem = pendingCreation.RawItem.DeepClone().AsObject();
            RewriteMortalItemContentsPath(canonicalItem, creationMap);

            var receipt = MortalItemIdentityState.CreateRootReceipt(
                canonicalItem,
                pendingCreation.ItemId,
                acceptedTurn);
            canonicalItem["itemId"] = pendingCreation.ItemId;
            canonicalItem["existedId"] = pendingCreation.ItemId;
            canonicalItem.Remove("creationRef");
            canonicalItem["materializationReceipt"] = receipt;

            items.Add(canonicalItem);
            indexEntries.Add(CreatePlayerItemIdentityEntry(
                canonicalItem,
                receipt,
                acceptedTurn));
        }

        RewriteMortalItemCreationReferences(current["equipment"], creationMap);
        RewriteMortalItemCreationReferences(current["equippedItems"], creationMap);
        current.Remove("UpdateInventory");

        var normalizedIndex = MortalItemIdentityState.Parse(index);
        if (normalizedIndex.Issues.Count > 0)
        {
            throw new InvalidDataException(
                "Client-created Mortal item identity entries failed their closed schema.");
        }

        await WriteCanonicalFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            current.ToJsonString(JsonOpts));
        await WriteCanonicalFileAtomicAsync(
            MortalItemIdentityState.StatePath,
            normalizedIndex.Root.ToJsonString(JsonOpts));
    }

    private static void EnsureRawPlayerItemCreation(
        JsonObject rawItem,
        int updateIndex,
        int acceptedTurn)
    {
        using var document = JsonDocument.Parse(rawItem.ToJsonString());
        var issues = MortalItemMaterializationContract.Validate(
            document.RootElement,
            $"{InventoryEquipmentService.ItemsPath}.UpdateInventory[{updateIndex}]",
            MortalItemMaterializationPhase.RawPreSeal);
        if (issues.Count > 0)
        {
            throw new InvalidDataException(
                $"UpdateInventory[{updateIndex}] is not a valid raw Mortal item creation: {issues[0].Code}.");
        }

        var envelope = rawItem[MortalItemMaterializationContract.EnvelopeProperty]!.AsObject();
        if (envelope["sourceTurn"] is not JsonValue sourceTurnNode ||
            !sourceTurnNode.TryGetValue<int>(out var sourceTurn) ||
            sourceTurn != acceptedTurn)
        {
            throw new InvalidDataException(
                $"UpdateInventory[{updateIndex}] is not bound to accepted turn {acceptedTurn}.");
        }
    }

    private static JsonObject CreatePlayerItemIdentityEntry(
        JsonObject item,
        JsonObject receipt,
        int acceptedTurn)
    {
        var itemId = RequireExactMortalItemIdentity(item["itemId"], "itemId");
        var envelope = item[MortalItemMaterializationContract.EnvelopeProperty]!.AsObject();
        var materializationId = RequireExactMortalItemIdentity(
            envelope["materializationId"],
            "materialization.materializationId");
        var authority = envelope["sourceAuthority"]!.AsObject();
        var authorityKind = RequireExactMortalItemIdentity(
            authority["kind"],
            "materialization.sourceAuthority.kind");
        var authorityId = RequireExactMortalItemIdentity(
            authority["authorityId"],
            "materialization.sourceAuthority.authorityId");
        var quantity = ReadMortalItemQuantity(item);
        var containerPath = item["contentsPath"] is JsonArray path
            ? path.DeepClone().AsArray()
            : new JsonArray();
        var carrier = new JsonObject
        {
            ["kind"] = "player_inventory",
            ["ownerId"] = "player",
            ["containerId"] = null,
            ["containerPath"] = containerPath
        };
        var transition = MortalItemIdentityState.CreateTransition(
            "create",
            acceptedTurn,
            Array.Empty<string>(),
            sourceCarrier: null,
            destinationCarrier: carrier,
            quantityBefore: 0,
            quantityAfter: quantity,
            authorityKind,
            authorityId);

        return new JsonObject
        {
            ["itemId"] = itemId,
            ["receiptId"] = RequireExactMortalItemIdentity(receipt["receiptId"], "receiptId"),
            ["state"] = "active",
            ["currentCarrier"] = carrier.DeepClone(),
            ["originMaterializationIds"] = new JsonArray(materializationId),
            ["parentItemIds"] = new JsonArray(),
            ["mergedIntoItemId"] = null,
            ["transitions"] = new JsonArray(transition)
        };
    }

    private static int ReadMortalItemQuantity(JsonObject item)
    {
        if (item["count"] is JsonValue countNode &&
            countNode.TryGetValue<int>(out var quantity) &&
            quantity > 0)
        {
            return quantity;
        }

        throw new InvalidDataException("A sealed Mortal item requires a non-negative integer count.");
    }

    private static void RewriteMortalItemContentsPath(
        JsonObject item,
        IReadOnlyDictionary<string, string> creationMap)
    {
        if (item["contentsPath"] is not JsonArray path)
            return;

        for (var index = 0; index < path.Count; index++)
        {
            var reference = ReadExactMortalItemIdentity(path[index]);
            if (reference != null && creationMap.TryGetValue(reference, out var itemId))
                path[index] = itemId;
        }
    }

    private static void RewriteMortalItemCreationReferences(
        JsonNode? node,
        IReadOnlyDictionary<string, string> creationMap)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj.ToArray())
                {
                    if (property.Value is JsonValue &&
                        ReadExactMortalItemIdentity(property.Value) is { } reference &&
                        creationMap.TryGetValue(reference, out var itemId))
                    {
                        obj[property.Key] = itemId;
                    }
                    else
                    {
                        RewriteMortalItemCreationReferences(property.Value, creationMap);
                    }
                }
                break;
            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    if (array[index] is JsonValue &&
                        ReadExactMortalItemIdentity(array[index]) is { } reference &&
                        creationMap.TryGetValue(reference, out var itemId))
                    {
                        array[index] = itemId;
                    }
                    else
                    {
                        RewriteMortalItemCreationReferences(array[index], creationMap);
                    }
                }
                break;
        }
    }

    private static string CreateUniqueMortalItemId(ISet<string> knownItemIds)
    {
        while (true)
        {
            var candidate = "itm_" + Guid.NewGuid().ToString("N");
            if (knownItemIds.Add(candidate))
                return candidate;
        }
    }

    private static string RequireExactMortalItemIdentity(JsonNode? node, string field)
    {
        var value = ReadExactMortalItemIdentity(node);
        return value ?? throw new InvalidDataException(
            $"Mortal item identity field '{field}' must be a non-empty exact string.");
    }

    private static string? ReadExactMortalItemIdentity(JsonNode? node)
    {
        if (node is not JsonValue value ||
            !value.TryGetValue<string>(out var text) ||
            string.IsNullOrWhiteSpace(text) ||
            !string.Equals(text, text.Trim(), StringComparison.Ordinal))
        {
            return null;
        }

        return text;
    }

    private sealed record PendingPlayerItemCreation(JsonObject RawItem, string ItemId);
}

internal static class AcceptedTurnCanonicalStateRefresh
{
    internal static async Task<IReadOnlyList<ValidationIssue>> NormalizeAndValidateAsync(
        FileSystemManager fs,
        CanonicalStateNormalizer normalizer,
        ValidationService validator,
        IReadOnlyDictionary<string, string> backups)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(normalizer);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(backups);

        await using var writeLease = await fs.AcquireCanonicalWriteLeaseAsync();
        var beforeImages = await CaptureBeforeImagesAsync(fs, writeLease);
        try
        {
            await normalizer.BindTo(writeLease).NormalizeAccumulatedStateAsync(backups);
            var issues = await validator
                .ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync(writeLease);
            if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
                await RestoreBeforeImagesAsync(fs, writeLease, beforeImages);
            return issues;
        }
        catch (Exception exception)
        {
            try
            {
                await RestoreBeforeImagesAsync(fs, writeLease, beforeImages);
            }
            catch (Exception rollbackException)
            {
                throw new AggregateException(
                    "Accepted-turn canonical normalization failed and exact rollback also failed.",
                    exception,
                    rollbackException);
            }

            ExceptionDispatchInfo.Capture(exception).Throw();
            throw;
        }
    }

    private static async Task<IReadOnlyList<CanonicalBeforeImage>> CaptureBeforeImagesAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        var beforeImages = new List<CanonicalBeforeImage>(
            CanonicalStateNormalizer.NormalizerRollbackTrackedFiles.Length);
        foreach (var path in CanonicalStateNormalizer.NormalizerRollbackTrackedFiles
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            beforeImages.Add(new CanonicalBeforeImage(
                path,
                await fs.ReadFileBytesAsync(writeLease, path)));
        }

        return beforeImages;
    }

    private static async Task RestoreBeforeImagesAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        IReadOnlyList<CanonicalBeforeImage> beforeImages)
    {
        var failures = new List<Exception>();
        for (var index = beforeImages.Count - 1; index >= 0; index--)
        {
            var beforeImage = beforeImages[index];
            try
            {
                var current = await fs.ReadFileBytesAsync(writeLease, beforeImage.Path);
                if (beforeImage.Bytes == null)
                {
                    if (current != null)
                        fs.DeleteFile(writeLease, beforeImage.Path);
                    continue;
                }

                if (current != null && current.AsSpan().SequenceEqual(beforeImage.Bytes))
                    continue;
                await fs.WriteFileAtomicBytesAsync(
                    writeLease,
                    beforeImage.Path,
                    beforeImage.Bytes);
            }
            catch (Exception exception)
            {
                failures.Add(new InvalidOperationException(
                    $"Failed to restore exact canonical before-image for '{beforeImage.Path}'.",
                    exception));
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(
                "One or more accepted-turn canonical before-images could not be restored.",
                failures);
        }
    }

    private sealed record CanonicalBeforeImage(string Path, byte[]? Bytes);
}
