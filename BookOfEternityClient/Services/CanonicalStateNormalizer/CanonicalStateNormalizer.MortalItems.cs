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
        await NormalizeMortalItemTransfersAsync(backups);

        var playerRoot = await ReadMortalItemObjectRootAsync(
            InventoryEquipmentService.ItemsPath);
        var npcRoot = await ReadMortalItemObjectRootAsync(
            NpcCoreChangesContract.NpcCorePath);
        var npcCommandsRoot = await ReadMortalItemObjectRootAsync(
            "game_state/npcs/npc_inventory.json");
        var locationRoot = await ReadMortalItemObjectRootAsync(
            StorageTransportMoveService.CurrentLocationPath);
        var vehiclesRoot = await ReadMortalItemVehiclesRootAsync();
        var routeCatalog = await MortalItemRouteAuthorityCatalog.BuildAsync(
            _fs,
            _writeLease);
        if (routeCatalog.Issues.Count > 0)
        {
            throw new InvalidDataException(
                $"Mortal item route authority failed: {routeCatalog.Issues[0].Code}.");
        }

        var acceptedTurn = await TryReadCurrentTurnNumberAsync();
        var indexJson = await ReadCanonicalFileAsync(MortalItemIdentityState.StatePath);
        var parsedIndex = MortalItemIdentityState.Parse(indexJson);
        var index = parsedIndex.Root.DeepClone().AsObject();
        var indexEntries = index["entries"]!.AsArray();
        var pending = new List<PendingMortalItemCreation>();
        var creationMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var knownItemIds = new HashSet<string>(
            parsedIndex.EntriesByItemId.Keys,
            StringComparer.Ordinal);
        var currentCarrierCatalog = MortalItemCarrierCatalog.Build(
            new MortalItemCarrierCatalogInput(
                playerRoot,
                npcRoot,
                npcCommandsRoot,
                locationRoot,
                vehiclesRoot,
                new Dictionary<string, JsonObject>(StringComparer.Ordinal)));
        var npcCommandIndex = MortalNpcCommandIndex.Build(npcRoot);
        foreach (var occurrence in currentCarrierCatalog.Occurrences)
        {
            var existingItemId = occurrence.ItemId;
            if (existingItemId != null)
                knownItemIds.Add(existingItemId);
        }

        void AddPending(
            JsonObject rawItem,
            string itemPath,
            Action<JsonObject> store)
        {
            EnsureRawMortalItemCreation(rawItem, itemPath, acceptedTurn);
            var creationRef = RequireExactMortalItemIdentity(
                rawItem["creationRef"],
                $"{itemPath}.creationRef");
            if (creationMap.ContainsKey(creationRef))
            {
                throw new InvalidDataException(
                    $"Duplicate exact Mortal item creationRef '{creationRef}'.");
            }
            if (!routeCatalog.ByCreationRef.TryGetValue(creationRef, out var authority))
            {
                throw new InvalidDataException(
                    $"Mortal item creationRef '{creationRef}' has no exact route authority.");
            }

            var itemId = CreateUniqueMortalItemId(knownItemIds);
            creationMap.Add(creationRef, itemId);
            pending.Add(new PendingMortalItemCreation(
                rawItem,
                itemId,
                authority,
                store));
        }

        var playerChanged = CollectPlayerMortalItemCreations(
            playerRoot,
            AddPending);
        var npcCoreChanged = CollectNpcCoreMortalItemCreations(
            npcRoot,
            AddPending);
        var npcCommandChanges = CollectNpcCommandMortalItemCreations(
            npcCommandIndex,
            npcCommandsRoot,
            AddPending);
        var npcCommandsChanged = npcCommandChanges.CommandsChanged;
        npcCoreChanged |= npcCommandChanges.NpcCoreChanged;
        var locationChanged = CollectLocationMortalItemCreations(
            locationRoot,
            AddPending);
        if (pending.Count == 0)
            return;
        if (acceptedTurn < 1)
        {
            throw new InvalidOperationException(
                "Mortal item sealing requires a positive accepted turn number in input/turn_request.json.");
        }
        if (parsedIndex.Issues.Count > 0)
        {
            throw new InvalidDataException(
                "Mortal item sealing requires a valid client-owned item identity index.");
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

            pendingCreation.Store(canonicalItem);
            indexEntries.Add(CreateMortalItemIdentityEntry(
                canonicalItem,
                receipt,
                acceptedTurn,
                pendingCreation.Authority,
                creationMap));
        }

        npcCommandIndex.RefreshInventoryItems();

        playerChanged |= RewriteMortalItemCreationReferences(playerRoot, creationMap);
        npcCoreChanged |= RewriteMortalItemCreationReferences(npcRoot, creationMap);
        npcCommandsChanged |= RewriteMortalItemCreationReferences(
            npcCommandsRoot,
            creationMap);
        var equipmentChanges = ApplyMortalNpcEquipmentCommands(
            npcCommandIndex,
            npcCommandsRoot,
            new HashSet<string>(creationMap.Values, StringComparer.Ordinal));
        npcCoreChanged |= equipmentChanges.NpcCoreChanged;
        npcCommandsChanged |= equipmentChanges.CommandsChanged;
        locationChanged |= RewriteMortalItemCreationReferences(locationRoot, creationMap);

        var companionRoots = await ReadMortalItemCompanionRootsAsync();
        var changedCompanions = new List<KeyValuePair<string, JsonObject>>();
        foreach (var pair in companionRoots)
        {
            if (RewriteMortalItemCreationReferences(pair.Value, creationMap))
                changedCompanions.Add(pair);
        }

        var normalizedIndex = MortalItemIdentityState.Parse(index);
        if (normalizedIndex.Issues.Count > 0)
        {
            throw new InvalidDataException(
                "Client-created Mortal item identity entries failed their closed schema.");
        }

        if (playerChanged && playerRoot != null)
        {
            await WriteCanonicalFileAtomicAsync(
                InventoryEquipmentService.ItemsPath,
                playerRoot.ToJsonString(JsonOpts));
        }
        if (npcCoreChanged && npcRoot != null)
        {
            await WriteCanonicalFileAtomicAsync(
                NpcCoreChangesContract.NpcCorePath,
                npcRoot.ToJsonString(JsonOpts));
        }
        if (npcCommandsChanged && npcCommandsRoot != null)
        {
            await WriteCanonicalFileAtomicAsync(
                "game_state/npcs/npc_inventory.json",
                npcCommandsRoot.ToJsonString(JsonOpts));
        }
        if (locationChanged && locationRoot != null)
        {
            await WriteCanonicalFileAtomicAsync(
                StorageTransportMoveService.CurrentLocationPath,
                locationRoot.ToJsonString(JsonOpts));
        }
        foreach (var pair in changedCompanions)
        {
            await WriteCanonicalFileAtomicAsync(
                pair.Key,
                pair.Value.ToJsonString(JsonOpts));
        }
        await WriteCanonicalFileAtomicAsync(
            MortalItemIdentityState.StatePath,
            normalizedIndex.Root.ToJsonString(JsonOpts));
    }

    private async Task NormalizeMortalItemTransfersAsync(
        IReadOnlyDictionary<string, string>? backups)
    {
        var previousPlayer = await ReadBackupObjectAsync(
            InventoryEquipmentService.ItemsPath,
            backups);
        var previousNpc = await ReadBackupObjectAsync(
            NpcCoreChangesContract.NpcCorePath,
            backups);
        var previousLocation = await ReadBackupObjectAsync(
            StorageTransportMoveService.CurrentLocationPath,
            backups);
        var previousVehicles = WrapMortalItemVehicles(
            await ReadBackupNodeAsync(StorageTransportMoveService.VehiclesPath, backups));
        var previousCatalog = MortalItemCarrierCatalog.Build(
            new MortalItemCarrierCatalogInput(
                previousPlayer,
                previousNpc,
                null,
                previousLocation,
                previousVehicles,
                new Dictionary<string, JsonObject>(StringComparer.Ordinal)));

        var currentPlayer = await ReadMortalItemObjectRootAsync(
            InventoryEquipmentService.ItemsPath);
        var currentNpc = await ReadMortalItemObjectRootAsync(
            NpcCoreChangesContract.NpcCorePath);
        var currentLocation = await ReadMortalItemObjectRootAsync(
            StorageTransportMoveService.CurrentLocationPath);
        var currentVehicles = await ReadMortalItemVehiclesRootAsync();
        var currentCatalog = MortalItemCarrierCatalog.Build(
            new MortalItemCarrierCatalogInput(
                currentPlayer,
                currentNpc,
                null,
                currentLocation,
                currentVehicles,
                new Dictionary<string, JsonObject>(StringComparer.Ordinal)));
        if (previousCatalog.Issues.Count > 0 || currentCatalog.Issues.Count > 0)
        {
            var issue = previousCatalog.Issues.FirstOrDefault() ?? currentCatalog.Issues[0];
            throw new InvalidDataException(
                $"Mortal item transfer carrier authority failed: {issue.Code}.");
        }

        var acceptedTurn = await TryReadCurrentTurnNumberAsync();
        var transfers = await MortalItemAcceptedTransferCatalog.BuildAsync(
            _fs,
            _writeLease,
            previousCatalog,
            currentCatalog,
            acceptedTurn);
        if (transfers.Issues.Count > 0)
        {
            throw new InvalidDataException(
                $"Mortal item transfer authority failed: {transfers.Issues[0].Code}.");
        }
        if (transfers.Transfers.Count == 0)
            return;

        if (_writeLease == null)
        {
            await using var ownedLease = await _fs.AcquireCanonicalWriteLeaseAsync();
            await ApplyMortalItemTransfersAsync(ownedLease, transfers.Transfers);
        }
        else
        {
            await ApplyMortalItemTransfersAsync(_writeLease, transfers.Transfers);
        }
        await RemoveAppliedMortalItemTransferCommandsAsync(transfers.Transfers);
    }

    private async Task ApplyMortalItemTransfersAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        IReadOnlyList<MortalItemAcceptedTransfer> transfers)
    {
        var writer = new MortalItemTransitionWriter(_fs);
        foreach (var transfer in transfers)
        {
            var result = await writer.ExecuteAsync(
                writeLease,
                new MortalItemTransitionIntent(
                    MortalItemTransitionKind.Transfer,
                    new[] { transfer.ItemId },
                    transfer.SourceCarrier,
                    transfer.DestinationCarrier,
                    transfer.Quantity,
                    transfer.Turn,
                    transfer.AuthorityKind,
                    transfer.AuthorityId));
            if (!result.Success)
            {
                throw new InvalidDataException(
                    $"Mortal item transfer '{transfer.ItemId}' failed: {result.Message}");
            }
        }
    }

    private async Task RemoveAppliedMortalItemTransferCommandsAsync(
        IReadOnlyList<MortalItemAcceptedTransfer> transfers)
    {
        var player = await ReadMortalItemObjectRootAsync(
            InventoryEquipmentService.ItemsPath);
        var npcCommands = await ReadMortalItemObjectRootAsync(
            MortalItemAcceptedTransferCatalog.NpcCommandsPath);
        var playerRemovals = await ReadMortalItemObjectRootAsync(
            MortalItemAcceptedTransferCatalog.PlayerRemovalPath);

        var playerChanged = RemoveCommandIndexes(
            player,
            "UpdateInventory",
            transfers
                .Where(transfer => transfer.DestinationSurface == MortalItemTransferCommandSurface.PlayerUpdate)
                .Select(transfer => transfer.DestinationIndex));
        var npcAddsChanged = RemoveCommandIndexes(
            npcCommands,
            "NPCInventoryAdds",
            transfers
                .Where(transfer => transfer.DestinationSurface == MortalItemTransferCommandSurface.NpcAdd)
                .Select(transfer => transfer.DestinationIndex));
        var npcRemovalsChanged = RemoveCommandIndexes(
            npcCommands,
            "NPCInventoryRemovals",
            transfers
                .Where(transfer => transfer.RemovalSurface == MortalItemTransferCommandSurface.NpcRemoval)
                .Select(transfer => transfer.RemovalIndex));
        var playerRemovalsChanged = RemoveCommandIndexes(
            playerRemovals,
            "removeInventoryItems",
            transfers
                .Where(transfer => transfer.RemovalSurface == MortalItemTransferCommandSurface.PlayerRemoval)
                .Select(transfer => transfer.RemovalIndex));

        if (playerChanged && player != null)
        {
            await WriteCanonicalFileAtomicAsync(
                InventoryEquipmentService.ItemsPath,
                player.ToJsonString(JsonOpts));
        }
        if ((npcAddsChanged || npcRemovalsChanged) && npcCommands != null)
        {
            await WriteCanonicalFileAtomicAsync(
                MortalItemAcceptedTransferCatalog.NpcCommandsPath,
                npcCommands.ToJsonString(JsonOpts));
        }
        if (playerRemovalsChanged && playerRemovals != null)
        {
            await WriteCanonicalFileAtomicAsync(
                MortalItemAcceptedTransferCatalog.PlayerRemovalPath,
                playerRemovals.ToJsonString(JsonOpts));
        }
    }

    private static bool RemoveCommandIndexes(
        JsonObject? root,
        string property,
        IEnumerable<int> indexes)
    {
        if (root?[property] is not JsonArray array)
            return false;
        var ordered = indexes.Distinct().OrderByDescending(index => index).ToArray();
        if (ordered.Length == 0)
            return false;
        foreach (var index in ordered)
        {
            if (index < 0 || index >= array.Count)
                throw new InvalidDataException($"Mortal item transfer command index {index} is stale.");
            array.RemoveAt(index);
        }
        if (array.Count == 0)
            root.Remove(property);
        return true;
    }

    private static JsonObject? WrapMortalItemVehicles(JsonNode? node) =>
        node switch
        {
            JsonObject root => root,
            JsonArray vehicles => new JsonObject { ["vehicles"] = vehicles.DeepClone() },
            _ => null
        };

    private static void EnsureRawMortalItemCreation(
        JsonObject rawItem,
        string itemPath,
        int acceptedTurn)
    {
        using var document = JsonDocument.Parse(rawItem.ToJsonString());
        var issues = MortalItemMaterializationContract.Validate(
            document.RootElement,
            itemPath,
            MortalItemMaterializationPhase.RawPreSeal);
        if (issues.Count > 0)
        {
            throw new InvalidDataException(
                $"{itemPath} is not a valid raw Mortal item creation: {issues[0].Code}.");
        }

        var envelope = rawItem[MortalItemMaterializationContract.EnvelopeProperty]!.AsObject();
        if (envelope["sourceTurn"] is not JsonValue sourceTurnNode ||
            !sourceTurnNode.TryGetValue<int>(out var sourceTurn) ||
            sourceTurn != acceptedTurn)
        {
            throw new InvalidDataException(
                $"{itemPath} is not bound to accepted turn {acceptedTurn}.");
        }
    }

    private async Task<JsonObject?> ReadMortalItemObjectRootAsync(string path)
    {
        var json = await ReadCanonicalFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonNode.Parse(json) as JsonObject ??
                   throw new InvalidDataException($"{path} must have an object root.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{path} contains malformed JSON.", exception);
        }
    }

    private async Task<JsonObject?> ReadMortalItemVehiclesRootAsync()
    {
        var json = await ReadCanonicalFileAsync(StorageTransportMoveService.VehiclesPath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var node = JsonNode.Parse(json);
            return node switch
            {
                JsonObject root => root,
                JsonArray vehicles => new JsonObject { ["vehicles"] = vehicles.DeepClone() },
                _ => throw new InvalidDataException(
                    $"{StorageTransportMoveService.VehiclesPath} must have an object or array root.")
            };
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"{StorageTransportMoveService.VehiclesPath} contains malformed JSON.",
                exception);
        }
    }

    private async Task<IReadOnlyDictionary<string, JsonObject>>
        ReadMortalItemCompanionRootsAsync()
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var path in new[]
                 {
                     "game_state/inventory/item_resources.json",
                     "game_state/inventory/item_bonds.json",
                     "game_state/inventory/item_text_updates.json",
                     "game_state/inventory/recipes.json",
                     "game_state/npcs/item_journals.json",
                     "game_state/quests/quest_history.json"
                 })
        {
            var root = await ReadMortalItemObjectRootAsync(path);
            if (root != null)
                result.Add(path, root);
        }

        return result;
    }

    private static bool CollectPlayerMortalItemCreations(
        JsonObject? root,
        Action<JsonObject, string, Action<JsonObject>> addPending)
    {
        if (root == null || !root.TryGetPropertyValue("UpdateInventory", out var updateNode))
            return false;
        if (updateNode is not JsonArray updates)
        {
            throw new InvalidDataException(
                $"{InventoryEquipmentService.ItemsPath}.UpdateInventory must be an array.");
        }

        var items = root["items"] as JsonArray ??
                    throw new InvalidDataException(
                        $"{InventoryEquipmentService.ItemsPath}.items must be an array before item sealing.");
        var retained = new JsonArray();
        var changed = false;
        for (var index = 0; index < updates.Count; index++)
        {
            if (updates[index] is JsonObject item && IsRawMortalItemCreation(item))
            {
                var itemPath = $"{InventoryEquipmentService.ItemsPath}.UpdateInventory[{index}]";
                addPending(item, itemPath, canonical => items.Add(canonical));
                changed = true;
            }
            else
            {
                retained.Add(updates[index]?.DeepClone());
            }
        }

        if (!changed)
            return false;
        if (retained.Count == 0)
            root.Remove("UpdateInventory");
        else
            root["UpdateInventory"] = retained;
        return true;
    }

    private static bool CollectNpcCoreMortalItemCreations(
        JsonObject? root,
        Action<JsonObject, string, Action<JsonObject>> addPending)
    {
        if (root == null)
            return false;

        var changed = false;
        foreach (var section in new[] { "UpdateNPCs", "NPCsInScene" })
        {
            if (root[section] is not JsonArray npcs)
                continue;
            for (var npcIndex = 0; npcIndex < npcs.Count; npcIndex++)
            {
                if (npcs[npcIndex] is not JsonObject npc ||
                    npc["inventory"] is not JsonArray inventory)
                {
                    continue;
                }

                for (var itemIndex = 0; itemIndex < inventory.Count; itemIndex++)
                {
                    if (inventory[itemIndex] is not JsonObject item ||
                        !IsRawMortalItemCreation(item))
                    {
                        continue;
                    }

                    var capturedIndex = itemIndex;
                    var itemPath =
                        $"{NpcCoreChangesContract.NpcCorePath}.{section}[{npcIndex}].inventory[{itemIndex}]";
                    addPending(
                        item,
                        itemPath,
                        canonical => inventory[capturedIndex] = canonical);
                    changed = true;
                }
            }
        }

        return changed;
    }

    private static MortalItemNpcCommandCollectionResult
        CollectNpcCommandMortalItemCreations(
            MortalNpcCommandIndex npcCommandIndex,
            JsonObject? commandsRoot,
            Action<JsonObject, string, Action<JsonObject>> addPending)
    {
        if (commandsRoot?["NPCInventoryAdds"] is not JsonArray adds)
            return new MortalItemNpcCommandCollectionResult(false, false);

        var retained = new JsonArray();
        var commandsChanged = false;
        var npcCoreChanged = false;
        for (var index = 0; index < adds.Count; index++)
        {
            if (adds[index] is not JsonObject command ||
                command["item"] is not JsonObject item ||
                !IsRawMortalItemCreation(item))
            {
                retained.Add(adds[index]?.DeepClone());
                continue;
            }

            var npcId = ReadMortalNpcIdentity(command) ??
                        throw new InvalidDataException(
                            $"NPCInventoryAdds[{index}] requires one exact NPC identity.");
            if (!npcCommandIndex.TryGetUniqueOwner(npcId, out var owner))
            {
                throw new InvalidDataException(
                    $"NPCInventoryAdds[{index}] must resolve exact NPC '{npcId}' once.");
            }

            var inventory = owner["inventory"] as JsonArray;
            if (inventory == null)
            {
                inventory = new JsonArray();
                owner["inventory"] = inventory;
            }

            if (ReadExactMortalItemIdentity(command["destinationContainerId"]) is { } containerId)
            {
                item["contentsPath"] = new JsonArray(containerId);
            }

            var itemPath =
                $"game_state/npcs/npc_inventory.json.NPCInventoryAdds[{index}].item";
            addPending(item, itemPath, canonical => inventory.Add(canonical));
            commandsChanged = true;
            npcCoreChanged = true;
        }

        if (commandsChanged)
        {
            if (retained.Count == 0)
                commandsRoot.Remove("NPCInventoryAdds");
            else
                commandsRoot["NPCInventoryAdds"] = retained;
        }

        return new MortalItemNpcCommandCollectionResult(
            commandsChanged,
            npcCoreChanged);
    }

    private static bool CollectLocationMortalItemCreations(
        JsonObject? root,
        Action<JsonObject, string, Action<JsonObject>> addPending)
    {
        if (root == null)
            return false;

        var location = root["currentLocationData"] as JsonObject ?? root;
        var locationPath = ReferenceEquals(location, root)
            ? StorageTransportMoveService.CurrentLocationPath
            : $"{StorageTransportMoveService.CurrentLocationPath}.currentLocationData";
        if (location["locationStorages"] is not JsonArray storages)
            return false;

        var changed = false;
        for (var storageIndex = 0; storageIndex < storages.Count; storageIndex++)
        {
            if (storages[storageIndex] is not JsonObject storage ||
                storage["contents"] is not JsonArray contents)
            {
                continue;
            }

            for (var itemIndex = 0; itemIndex < contents.Count; itemIndex++)
            {
                if (contents[itemIndex] is not JsonObject item ||
                    !IsRawMortalItemCreation(item))
                {
                    continue;
                }

                var capturedIndex = itemIndex;
                var itemPath =
                    $"{locationPath}.locationStorages[{storageIndex}].contents[{itemIndex}]";
                addPending(
                    item,
                    itemPath,
                    canonical => contents[capturedIndex] = canonical);
                changed = true;
            }
        }

        return changed;
    }

    private static MortalItemNpcCommandCollectionResult ApplyMortalNpcEquipmentCommands(
        MortalNpcCommandIndex npcCommandIndex,
        JsonObject? commandsRoot,
        IReadOnlySet<string> createdItemIds)
    {
        if (commandsRoot?["NPCEquipmentChanges"] is not JsonArray commands)
            return new MortalItemNpcCommandCollectionResult(false, false);

        var retained = new JsonArray();
        var applied = false;
        foreach (var commandNode in commands)
        {
            if (commandNode is not JsonObject command ||
                ReadExactMortalItemIdentity(command["itemId"]) is not { } itemId ||
                !createdItemIds.Contains(itemId))
            {
                retained.Add(commandNode?.DeepClone());
                continue;
            }

            var npcId = ReadMortalNpcIdentity(command) ??
                        throw new InvalidDataException(
                            "NPCEquipmentChanges requires one exact NPC identity.");
            if (!npcCommandIndex.TryGetUniqueOwner(npcId, out var owner))
            {
                throw new InvalidDataException(
                    $"NPCEquipmentChanges must resolve exact NPC '{npcId}' once.");
            }

            if (!npcCommandIndex.InventoryItemOccursExactlyOnce(npcId, itemId))
            {
                throw new InvalidDataException(
                    $"NPCEquipmentChanges itemId '{itemId}' must resolve once in NPC '{npcId}' inventory.");
            }

            var action = ReadExactMortalItemIdentity(command["action"]);
            var equipped = owner["equippedItems"] as JsonObject;
            if (equipped == null)
            {
                equipped = new JsonObject();
                owner["equippedItems"] = equipped;
            }

            switch (action)
            {
                case "equip":
                    foreach (var slot in ReadMortalItemEquipmentSlots(
                                 command["targetSlots"],
                                 "targetSlots"))
                    {
                        equipped[slot] = itemId;
                    }
                    break;
                case "unequip":
                    foreach (var slot in ReadMortalItemEquipmentSlots(
                                 command["sourceSlots"],
                                 "sourceSlots"))
                    {
                        if (string.Equals(
                                ReadExactMortalItemIdentity(equipped[slot]),
                                itemId,
                                StringComparison.Ordinal))
                        {
                            equipped.Remove(slot);
                        }
                    }
                    break;
                default:
                    throw new InvalidDataException(
                        "NPCEquipmentChanges.action must be exact 'equip' or 'unequip'.");
            }

            applied = true;
        }

        if (!applied)
            return new MortalItemNpcCommandCollectionResult(false, false);
        if (retained.Count == 0)
            commandsRoot.Remove("NPCEquipmentChanges");
        else
            commandsRoot["NPCEquipmentChanges"] = retained;
        return new MortalItemNpcCommandCollectionResult(true, true);
    }

    private static IReadOnlyList<string> ReadMortalItemEquipmentSlots(
        JsonNode? node,
        string field)
    {
        if (node is not JsonArray slots || slots.Count == 0)
        {
            throw new InvalidDataException(
                $"NPCEquipmentChanges.{field} must be a non-empty exact string array.");
        }

        var result = new List<string>(slots.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var slotNode in slots)
        {
            var slot = ReadExactMortalItemIdentity(slotNode);
            if (slot == null || !seen.Add(slot))
            {
                throw new InvalidDataException(
                    $"NPCEquipmentChanges.{field} contains an invalid or duplicate slot.");
            }
            result.Add(slot);
        }

        return result;
    }

    private static IEnumerable<JsonObject> EnumerateMortalNpcObjects(JsonObject? root)
    {
        if (root == null)
            yield break;
        foreach (var section in new[] { "UpdateNPCs", "NPCsInScene" })
        {
            if (root[section] is not JsonArray npcs)
                continue;
            foreach (var npc in npcs.OfType<JsonObject>())
                yield return npc;
        }
    }

    internal static int MeasureMortalNpcCommandIndexWork(
        JsonObject? npcRoot,
        JsonObject? commandsRoot)
    {
        var index = MortalNpcCommandIndex.Build(npcRoot);
        if (commandsRoot?["NPCInventoryAdds"] is JsonArray adds)
        {
            foreach (var node in adds)
            {
                if (node is JsonObject command &&
                    command["item"] is JsonObject item &&
                    IsRawMortalItemCreation(item) &&
                    ReadMortalNpcIdentity(command) is { } npcId)
                {
                    _ = index.TryGetUniqueOwner(npcId, out _);
                }
            }
        }

        index.RefreshInventoryItems();
        if (commandsRoot?["NPCEquipmentChanges"] is JsonArray equipmentChanges)
        {
            foreach (var node in equipmentChanges)
            {
                if (node is JsonObject command &&
                    ReadMortalNpcIdentity(command) is { } npcId &&
                    ReadExactMortalItemIdentity(command["itemId"]) is { } itemId)
                {
                    _ = index.TryGetUniqueOwner(npcId, out _);
                    _ = index.InventoryItemOccursExactlyOnce(npcId, itemId);
                }
            }
        }

        return index.WorkUnits;
    }

    private static string? ReadMortalNpcIdentity(JsonObject obj) =>
        ReadExactMortalItemIdentity(obj["NPCId"]) ??
        ReadExactMortalItemIdentity(obj["npcId"]) ??
        ReadExactMortalItemIdentity(obj["id"]) ??
        ReadExactMortalItemIdentity(obj["initialId"]);

    private sealed class MortalNpcCommandIndex
    {
        private readonly Dictionary<string, List<JsonObject>> _ownersById =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, int>>
            _inventoryItemCountsByNpcId = new(StringComparer.Ordinal);

        internal int WorkUnits { get; private set; }

        internal static MortalNpcCommandIndex Build(JsonObject? root)
        {
            var result = new MortalNpcCommandIndex();
            foreach (var npc in EnumerateMortalNpcObjects(root))
            {
                result.WorkUnits++;
                var npcId = ReadMortalNpcIdentity(npc);
                if (npcId == null)
                    continue;
                if (!result._ownersById.TryGetValue(npcId, out var owners))
                {
                    owners = new List<JsonObject>();
                    result._ownersById.Add(npcId, owners);
                }
                owners.Add(npc);
            }

            return result;
        }

        internal bool TryGetUniqueOwner(string npcId, out JsonObject owner)
        {
            WorkUnits++;
            if (_ownersById.TryGetValue(npcId, out var owners) &&
                owners.Count == 1)
            {
                owner = owners[0];
                return true;
            }

            owner = null!;
            return false;
        }

        internal void RefreshInventoryItems()
        {
            _inventoryItemCountsByNpcId.Clear();
            foreach (var pair in _ownersById)
            {
                WorkUnits++;
                if (pair.Value.Count != 1)
                    continue;

                var counts = new Dictionary<string, int>(StringComparer.Ordinal);
                if (pair.Value[0]["inventory"] is JsonArray inventory)
                {
                    foreach (var node in inventory)
                    {
                        WorkUnits++;
                        if (node is not JsonObject item ||
                            ReadExactMortalItemIdentity(item["itemId"]) is not { } itemId)
                        {
                            continue;
                        }

                        counts[itemId] = counts.GetValueOrDefault(itemId) + 1;
                    }
                }

                _inventoryItemCountsByNpcId.Add(pair.Key, counts);
            }
        }

        internal bool InventoryItemOccursExactlyOnce(string npcId, string itemId)
        {
            WorkUnits++;
            return _inventoryItemCountsByNpcId.TryGetValue(npcId, out var counts) &&
                   counts.GetValueOrDefault(itemId) == 1;
        }
    }

    private static bool IsRawMortalItemCreation(JsonObject item) =>
        item.ContainsKey("creationRef") ||
        item.TryGetPropertyValue("existedId", out var existedId) && existedId == null;

    private static JsonObject CreateMortalItemIdentityEntry(
        JsonObject item,
        JsonObject receipt,
        int acceptedTurn,
        MortalItemRouteAuthority routeAuthority,
        IReadOnlyDictionary<string, string> creationMap)
    {
        var itemId = RequireExactMortalItemIdentity(item["itemId"], "itemId");
        var envelope = item[MortalItemMaterializationContract.EnvelopeProperty]!.AsObject();
        var materializationId = RequireExactMortalItemIdentity(
            envelope["materializationId"],
            "materialization.materializationId");
        var quantity = ReadMortalItemQuantity(item);
        var carrier = CreateMortalItemCarrierNode(
            RewriteMortalItemCarrierCoordinate(routeAuthority.Destination, creationMap));
        var transition = MortalItemIdentityState.CreateTransition(
            "create",
            acceptedTurn,
            routeAuthority.SourceItemIds,
            sourceCarrier: null,
            destinationCarrier: carrier,
            quantityBefore: 0,
            quantityAfter: quantity,
            routeAuthority.AuthorityKind,
            routeAuthority.AuthorityId);

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

        throw new InvalidDataException("A sealed Mortal item requires a positive integer count.");
    }

    private static MortalItemCarrierCoordinate RewriteMortalItemCarrierCoordinate(
        MortalItemCarrierCoordinate carrier,
        IReadOnlyDictionary<string, string> creationMap) =>
        carrier with
        {
            ContainerPath = carrier.ContainerPath
                .Select(reference => creationMap.GetValueOrDefault(reference, reference))
                .ToArray()
        };

    private static JsonObject CreateMortalItemCarrierNode(
        MortalItemCarrierCoordinate carrier) =>
        new()
        {
            ["kind"] = carrier.Kind,
            ["ownerId"] = carrier.OwnerId,
            ["containerId"] = carrier.ContainerId,
            ["containerPath"] = new JsonArray(
                carrier.ContainerPath
                    .Select(value => (JsonNode?)JsonValue.Create(value))
                    .ToArray())
        };

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

    private static bool RewriteMortalItemCreationReferences(
        JsonNode? node,
        IReadOnlyDictionary<string, string> creationMap) =>
        RewriteMortalItemCreationReferences(
            node,
            creationMap,
            scalarValuesAreReferences: false);

    private static bool RewriteMortalItemCreationReferences(
        JsonNode? node,
        IReadOnlyDictionary<string, string> creationMap,
        bool scalarValuesAreReferences)
    {
        var changed = false;
        switch (node)
        {
            case JsonObject obj:
                changed |= ReplaceMortalItemCreationAlias(obj, "creationRef", creationMap);
                changed |= ReplaceMortalItemCreationAlias(obj, "itemCreationRef", creationMap);
                foreach (var property in obj.ToArray())
                {
                    if (property.Key is
                        MortalItemMaterializationContract.EnvelopeProperty or
                        MortalItemMaterializationContract.ReceiptProperty)
                    {
                        continue;
                    }

                    var propertyValuesAreReferences =
                        MortalItemReferenceMapProperties.Contains(property.Key) ||
                        MortalItemReferenceArrayProperties.Contains(property.Key);
                    if ((scalarValuesAreReferences ||
                         MortalItemDirectReferenceProperties.Contains(property.Key)) &&
                        property.Value is JsonValue &&
                        ReadExactMortalItemIdentity(property.Value) is { } reference &&
                        creationMap.TryGetValue(reference, out var itemId))
                    {
                        obj[property.Key] = itemId;
                        changed = true;
                    }
                    else
                    {
                        changed |= RewriteMortalItemCreationReferences(
                            property.Value,
                            creationMap,
                            propertyValuesAreReferences);
                    }
                }
                break;
            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    if (scalarValuesAreReferences &&
                        array[index] is JsonValue &&
                        ReadExactMortalItemIdentity(array[index]) is { } reference &&
                        creationMap.TryGetValue(reference, out var itemId))
                    {
                        array[index] = itemId;
                        changed = true;
                    }
                    else
                    {
                        changed |= RewriteMortalItemCreationReferences(
                            array[index],
                            creationMap,
                            scalarValuesAreReferences: false);
                    }
                }
                break;
        }

        return changed;
    }

    private static bool ReplaceMortalItemCreationAlias(
        JsonObject obj,
        string aliasProperty,
        IReadOnlyDictionary<string, string> creationMap)
    {
        var reference = ReadExactMortalItemIdentity(obj[aliasProperty]);
        if (reference == null || !creationMap.TryGetValue(reference, out var itemId))
            return false;

        if (obj.TryGetPropertyValue("itemId", out var existingItemId) &&
            existingItemId != null &&
            !string.Equals(
                ReadExactMortalItemIdentity(existingItemId),
                itemId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Same-turn {aliasProperty} '{reference}' conflicts with an existing itemId.");
        }

        obj.Remove(aliasProperty);
        obj["itemId"] = itemId;
        return true;
    }

    private static readonly HashSet<string> MortalItemDirectReferenceProperties =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "itemId",
            "existedId",
            "itemRef",
            "sourceItemId",
            "targetItemId",
            "parentItemId",
            "containerItemId",
            "rewardItemId",
            "destinationItemId",
            "resultItemId"
        };

    private static readonly HashSet<string> MortalItemReferenceArrayProperties =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "itemIds",
            "sourceItemIds",
            "targetItemIds",
            "parentItemIds",
            "contentsPath",
            "itemsReceived"
        };

    private static readonly HashSet<string> MortalItemReferenceMapProperties =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "equipment",
            "equippedItems",
            "equipmentSlots"
        };

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

    private sealed record PendingMortalItemCreation(
        JsonObject RawItem,
        string ItemId,
        MortalItemRouteAuthority Authority,
        Action<JsonObject> Store);

    private sealed record MortalItemNpcCommandCollectionResult(
        bool CommandsChanged,
        bool NpcCoreChanged);
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
