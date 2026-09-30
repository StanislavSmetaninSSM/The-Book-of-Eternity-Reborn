using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using SpiritualPublicationReceipt = BookOfEternityClient.Services.ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationReceipt;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private abstract record MortalItemAcceptedTurnNormalizationMode
    {
        internal sealed record Validated(
            MortalItemAcceptedTurnNormalizationSnapshot Snapshot)
            : MortalItemAcceptedTurnNormalizationMode;

        internal sealed record NoAcceptedBinding
            : MortalItemAcceptedTurnNormalizationMode;

        internal sealed record ClientOwnedBootstrap
            : MortalItemAcceptedTurnNormalizationMode;
    }

    private MortalItemAcceptedTurnNormalizationMode
        CaptureMortalItemAcceptedTurnNormalizationMode(
            AcceptedMechanicsNormalizationPreflight acceptedMechanicsPreflight)
    {
        ArgumentNullException.ThrowIfNull(acceptedMechanicsPreflight);
        if (acceptedMechanicsPreflight is AcceptedMechanicsNormalizationPreflight.NoPlan)
            return new MortalItemAcceptedTurnNormalizationMode.NoAcceptedBinding();

        var validated = (AcceptedMechanicsNormalizationPreflight.Validated)
            acceptedMechanicsPreflight;
        var writeLease = _writeLease ?? throw new InvalidOperationException(
            "Mortal item accepted-turn authority requires the owning canonical write lease.");
        if (!MortalItemAcceptedTurnAuthority.TryCaptureNormalizationSnapshot(
                _fs,
                writeLease,
                validated.Binding.SessionId,
                validated.Binding.SnapshotToken,
                validated.Binding.Turn,
                out var snapshot))
        {
            throw new InvalidDataException(
                "Mortal item accepted-turn authority cache is missing or its immutable allocation map does not match the exact validated common-plan session and snapshot binding.");
        }

        var itemPublication = validated.Plan
            .TreatmentResourcePublicationAuthority?.ItemPublicationAuthority;
        var ownerAuthorityMatches = itemPublication is null
            ? snapshot.MatchesAcceptedOwnerAuthority(validated.Plan.OwnerAuthority)
            : itemPublication.MatchesNormalizationSnapshot(
                snapshot,
                validated.Plan.OwnerAuthority);
        if (!ownerAuthorityMatches)
        {
            throw new InvalidDataException(
                "Mortal item accepted-turn authority cache is missing or its immutable allocation map does not match the exact validated common-plan session and snapshot binding.");
        }

        return new MortalItemAcceptedTurnNormalizationMode.Validated(snapshot);
    }

    private async Task NormalizeMortalItemsAsync(
        IReadOnlyDictionary<string, string>? backups,
        IReadOnlyList<MortalLocationStorageCoordinate>? acceptedStorageCoordinates,
        MortalItemAcceptedTurnNormalizationMode mode)
    {
        ArgumentNullException.ThrowIfNull(mode);
        if (mode is MortalItemAcceptedTurnNormalizationMode.Validated validated)
        {
            var currentRoots = validated.Snapshot.CloneCurrentProjectionRoots();
            IReadOnlyDictionary<string, JsonNode?> projectedRoots;
            if (validated.Snapshot.CloneFinalBaseline() is { } finalBaseline)
            {
                var itemPhase = validated.Snapshot.CloneItemPhase();
                if (finalBaseline.Issues.Count != 0 ||
                    itemPhase is null ||
                    itemPhase.Issues.Count != 0 ||
                    !validated.Snapshot.RecomputesFinalPublicationBaseline())
                {
                    throw new InvalidDataException(
                        "Accepted Mortal item final publication baseline is invalid.");
                }
                projectedRoots = itemPhase.ItemPhaseAfterImages;
            }
            else
            {
                var backupRoots = validated.Snapshot.CloneBackupProjectionRoots();
                var identityRoot = currentRoots[MortalItemIdentityState.StatePath]
                    as JsonObject ?? throw new InvalidDataException(
                        "Accepted Mortal item projection requires its frozen identity root.");
                var identity = MortalItemIdentityState.Parse(identityRoot.DeepClone());
                var projected = MortalItemCanonicalProjectionPlanner.Project(
                    new MortalItemCanonicalProjectionInput(
                        validated.Snapshot.Turn,
                        validated.Snapshot,
                        validated.Snapshot.CloneRouteCatalog(),
                        currentRoots,
                        backupRoots,
                        identity));
                if (!projected.IsValid)
                {
                    throw new InvalidDataException(
                        $"Accepted Mortal item projection failed: {projected.Issues[0].Code}.");
                }
                projectedRoots = projected.ItemPhaseAfterImages;
            }
            foreach (var path in MortalItemCanonicalProjectionPlanner.ProjectionRootPaths)
            {
                var after = projectedRoots[path];
                if (JsonNode.DeepEquals(currentRoots[path], after))
                    continue;
                if (after == null)
                {
                    throw new InvalidDataException(
                        $"Accepted Mortal item projection cannot remove registered root '{path}'.");
                }
                await WriteCanonicalFileAtomicAsync(path, after.ToJsonString(JsonOpts));
            }
            return;
        }

        await NormalizeOrdinaryMortalItemsAsync(
            backups,
            acceptedStorageCoordinates,
            mode);
    }

    private async Task NormalizeOrdinaryMortalItemsAsync(
        IReadOnlyDictionary<string, string>? backups,
        IReadOnlyList<MortalLocationStorageCoordinate>? acceptedStorageCoordinates,
        MortalItemAcceptedTurnNormalizationMode mode)
    {
        ArgumentNullException.ThrowIfNull(mode);
        if (mode is not MortalItemAcceptedTurnNormalizationMode.ClientOwnedBootstrap)
            await NormalizeOrdinaryMortalItemTransfersAsync(backups);

        var playerRoot = await ReadMortalItemObjectRootAsync(
            InventoryEquipmentService.ItemsPath);
        var npcRoot = await ReadMortalItemObjectRootAsync(
            NpcCoreChangesContract.NpcCorePath);
        var npcCommandsRoot = await ReadMortalItemObjectRootAsync(
            "game_state/npcs/npc_inventory.json");
        var locationRoot = await ReadMortalItemObjectRootAsync(
            StorageTransportMoveService.CurrentLocationPath);
        var offscreenLocationStorageRoot = await ReadMortalItemObjectRootAsync(
            MortalLocationStorageContentsState.StatePath);
        var vehiclesRoot = await ReadMortalItemVehiclesRootAsync();
        var routeCatalog = await BuildOrdinaryMortalItemRouteAuthorityCatalogAsync(
            acceptedStorageCoordinates);
        if (routeCatalog.Issues.Count > 0)
        {
            throw new InvalidDataException(
                $"Mortal item route authority failed: {routeCatalog.Issues[0].Code}.");
        }

        var acceptedTurn = mode is MortalItemAcceptedTurnNormalizationMode.Validated validatedMode
            ? validatedMode.Snapshot.Turn
            : await TryReadCurrentTurnNumberAsync();
        var indexJson = await ReadCanonicalFileAsync(MortalItemIdentityState.StatePath);
        var parsedIndex = MortalItemIdentityState.Parse(indexJson);
        var acceptedCreationEvidence =
            MortalItemIdentityState.BuildAcceptedRootCreationEvidence(parsedIndex);
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
                new Dictionary<string, JsonObject>(StringComparer.Ordinal),
                offscreenLocationStorageRoot));
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
            var materializationId = RequireExactMortalItemIdentity(
                rawItem[MortalItemMaterializationContract.EnvelopeProperty]?["materializationId"],
                $"{itemPath}.materialization.materializationId");
            if (creationMap.ContainsKey(creationRef))
            {
                throw new InvalidDataException(
                    $"Duplicate exact Mortal item creationRef '{creationRef}'.");
            }
            if (parsedIndex.Issues.Count > 0)
            {
                throw new InvalidDataException(
                    "Mortal item sealing requires a valid client-owned item identity index.");
            }
            var acceptedCreationMatch = acceptedCreationEvidence.Match(
                materializationId,
                creationRef);
            if (acceptedCreationMatch != MortalItemAcceptedCreationEvidenceMatch.None)
            {
                throw new InvalidDataException(
                    acceptedCreationMatch == MortalItemAcceptedCreationEvidenceMatch.Confusable
                        ? $"Mortal item creationRef '{creationRef}' or materializationId '{materializationId}' is a case, whitespace, or Unicode-normalization alias of accepted identity history."
                        : $"Mortal item creationRef '{creationRef}' or materializationId '{materializationId}' was already accepted by active or retired identity history.");
            }
            if (!routeCatalog.ByCreationRef.TryGetValue(creationRef, out var authority))
            {
                throw new InvalidDataException(
                    $"Mortal item creationRef '{creationRef}' has no exact route authority.");
            }

            string itemId;
            if (mode is MortalItemAcceptedTurnNormalizationMode.Validated validated)
            {
                if (!validated.Snapshot.TryGetAllocatedItemId(
                        creationRef,
                        out var allocatedItemId))
                {
                    throw new InvalidDataException(
                        $"Mortal item accepted-turn authority cache is missing or does not match the validated common-plan binding for creationRef '{creationRef}'.");
                }
                if (!knownItemIds.Add(allocatedItemId))
                {
                    throw new InvalidDataException(
                        $"Reserved Mortal item identity '{allocatedItemId}' collides with current identity authority.");
                }
                itemId = allocatedItemId;
            }
            else if (mode is MortalItemAcceptedTurnNormalizationMode.ClientOwnedBootstrap)
            {
                if (!itemPath.StartsWith(
                        InventoryEquipmentService.ItemsPath + ".UpdateInventory[",
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Client-owned bootstrap normalization rejects GM-authored raw Mortal item surface '{itemPath}'.");
                }
                if (rawItem.ContainsKey("resourceMaterialization"))
                {
                    throw new InvalidOperationException(
                        $"Client-owned bootstrap normalization rejects GM-authored resource materialization at '{itemPath}'.");
                }
                itemId = CreateUniqueMortalItemId(knownItemIds);
            }
            else
            {
                throw new InvalidDataException(
                    $"Mortal item accepted-turn authority requires a validated common-plan binding before sealing raw creationRef '{creationRef}'.");
            }
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
        var offscreenLocationStorageChanged =
            CollectOffscreenLocationStorageMortalItemCreations(
                offscreenLocationStorageRoot,
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
            MortalItemLocalActionPolicy.NormalizePlacementForDestination(
                canonicalItem,
                RewriteMortalItemCarrierCoordinate(
                    pendingCreation.Authority.Destination,
                    creationMap));
            canonicalItem.Remove("resourceMaterialization");

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
        offscreenLocationStorageChanged |= RewriteMortalItemCreationReferences(
            offscreenLocationStorageRoot,
            creationMap);

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
        if (offscreenLocationStorageChanged && offscreenLocationStorageRoot != null)
        {
            await WriteCanonicalFileAtomicAsync(
                MortalLocationStorageContentsState.StatePath,
                offscreenLocationStorageRoot.ToJsonString(JsonOpts));
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

    private async Task PreflightNoAcceptedBindingMortalItemsAsync()
    {
        foreach (var path in new[]
                 {
                     InventoryEquipmentService.ItemsPath,
                     NpcCoreChangesContract.NpcCorePath,
                     "game_state/npcs/npc_inventory.json",
                     StorageTransportMoveService.CurrentLocationPath,
                     MortalLocationStorageContentsState.StatePath,
                     StorageTransportMoveService.VehiclesPath
                 })
        {
            var json = await ReadCanonicalFileAsync(path);
            if (json == null)
                continue;

            JsonNode? root;
            try
            {
                root = JsonNode.Parse(json);
            }
            catch (JsonException)
            {
                continue;
            }

            if (ContainsRawMortalItemCreation(root))
            {
                throw new InvalidDataException(
                    "Mortal item accepted-turn authority requires a validated common-plan binding before sealing raw creation authority.");
            }
        }
    }

    private static bool ContainsRawMortalItemCreation(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            if (IsRawMortalItemCreation(obj))
                return true;
            return obj.Any(static pair =>
                !IsMortalItemIdentityEvidenceProperty(pair.Key) &&
                ContainsRawMortalItemCreation(pair.Value));
        }
        return node is JsonArray array &&
               array.Any(ContainsRawMortalItemCreation);
    }

    private static bool ContainsForbiddenBootstrapMaterializationSurface(
        JsonNode? node,
        bool allowPlayerBootstrapItem)
    {
        if (allowPlayerBootstrapItem &&
            node is JsonObject playerRoot &&
            playerRoot["UpdateInventory"] is JsonArray updates)
        {
            foreach (var pair in playerRoot)
            {
                if (string.Equals(pair.Key, "UpdateInventory", StringComparison.Ordinal))
                    continue;
                if (ContainsGmMaterializationSurface(pair.Value))
                    return true;
            }

            foreach (var update in updates)
            {
                if (update is JsonObject item && IsRawMortalItemCreation(item))
                {
                    if (ContainsForbiddenPlayerBootstrapItemEnvelope(item))
                        return true;
                    continue;
                }

                if (ContainsGmMaterializationSurface(update))
                    return true;
            }
            return false;
        }

        return ContainsGmMaterializationSurface(node);
    }

    private static bool ContainsForbiddenPlayerBootstrapItemEnvelope(JsonObject item)
    {
        if (item.ContainsKey("resourceMaterialization") ||
            item.ContainsKey("activeEffectDefinitions") ||
            item.ContainsKey("effectChanges") ||
            item.ContainsKey("ownerMaterialization"))
        {
            return true;
        }

        foreach (var pair in item)
        {
            if (string.Equals(
                    pair.Key,
                    MortalItemMaterializationContract.EnvelopeProperty,
                    StringComparison.Ordinal))
            {
                if (ContainsForbiddenNestedBootstrapEnvelope(
                        pair.Value,
                        allowCurrentItemEnvelopeCreationRef: true))
                    return true;
                continue;
            }
            if (ContainsGmMaterializationSurface(pair.Value))
                return true;
        }
        return false;
    }

    private static bool ContainsForbiddenNestedBootstrapEnvelope(
        JsonNode? node,
        bool allowCurrentItemEnvelopeCreationRef = false)
    {
        if (node is JsonArray array)
            return array.Any(static child =>
                ContainsForbiddenNestedBootstrapEnvelope(child));
        if (node is not JsonObject obj)
            return false;
        if (obj.ContainsKey("resourceMaterialization") ||
            obj.ContainsKey("activeEffectDefinitions") ||
            obj.ContainsKey("effectChanges") ||
            obj.ContainsKey("ownerMaterialization") ||
            (!allowCurrentItemEnvelopeCreationRef &&
             (IsRawMortalItemCreation(obj) ||
              (obj.ContainsKey(MortalItemMaterializationContract.EnvelopeProperty) &&
               !obj.ContainsKey(MortalItemMaterializationContract.ReceiptProperty)))))
        {
            return true;
        }
        return obj.Any(static pair =>
            ContainsForbiddenNestedBootstrapEnvelope(pair.Value));
    }

    private static bool ContainsGmMaterializationSurface(JsonNode? node)
    {
        if (node is JsonArray array)
            return array.Any(ContainsGmMaterializationSurface);
        if (node is not JsonObject obj)
            return false;

        if (obj.ContainsKey("resourceMaterialization") ||
            obj.ContainsKey("activeEffectDefinitions") ||
            obj.ContainsKey("effectChanges") ||
            obj.ContainsKey("ownerMaterialization") ||
            IsRawMortalItemCreation(obj))
        {
            return true;
        }

        if (obj[MortalItemMaterializationContract.EnvelopeProperty] is JsonObject &&
            !obj.ContainsKey(MortalItemMaterializationContract.ReceiptProperty))
        {
            return true;
        }

        return obj.Any(static pair =>
            !IsMortalItemIdentityEvidenceProperty(pair.Key) &&
            ContainsGmMaterializationSurface(pair.Value));
    }

    private static bool IsMortalItemIdentityEvidenceProperty(string propertyName) =>
        string.Equals(
            propertyName,
            MortalItemMaterializationContract.EnvelopeProperty,
            StringComparison.Ordinal) ||
        string.Equals(
            propertyName,
            MortalItemMaterializationContract.ReceiptProperty,
            StringComparison.Ordinal);

    internal static void EnsureRawMortalItemCreation(
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

    internal static bool CollectPlayerMortalItemCreations(
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

    internal static bool CollectNpcCoreMortalItemCreations(
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

    internal static MortalItemNpcCommandCollectionResult
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
            if (!npcCommandIndex.TryGetOwners(npcId, out var owners))
            {
                throw new InvalidDataException(
                    $"NPCInventoryAdds[{index}] must resolve one exact logical NPC '{npcId}'.");
            }

            var inventories = new List<JsonArray>(owners.Count);
            foreach (var owner in owners)
            {
                var inventory = owner["inventory"] as JsonArray;
                if (inventory == null)
                {
                    inventory = new JsonArray();
                    owner["inventory"] = inventory;
                }
                inventories.Add(inventory);
            }

            if (ReadExactMortalItemIdentity(command["destinationContainerId"]) is { } containerId)
            {
                item["contentsPath"] = new JsonArray(containerId);
            }

            var itemPath =
                $"game_state/npcs/npc_inventory.json.NPCInventoryAdds[{index}].item";
            addPending(item, itemPath, canonical =>
            {
                foreach (var inventory in inventories)
                    inventory.Add(canonical.DeepClone());
            });
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

    internal static bool CollectLocationMortalItemCreations(
        JsonObject? root,
        Action<JsonObject, string, Action<JsonObject>> addPending)
    {
        if (root == null)
            return false;

        var location = MortalItemCurrentLocationCarrier.Select(root)!;
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

    internal static bool CollectOffscreenLocationStorageMortalItemCreations(
        JsonObject? root,
        Action<JsonObject, string, Action<JsonObject>> addPending)
    {
        if (root?["entries"] is not JsonArray entries)
            return false;

        var changed = false;
        for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
        {
            if (entries[entryIndex] is not JsonObject entry ||
                entry["contents"] is not JsonArray contents)
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
                    $"{MortalLocationStorageContentsState.StatePath}.entries[{entryIndex}].contents[{itemIndex}]";
                addPending(
                    item,
                    itemPath,
                    canonical => contents[capturedIndex] = canonical);
                changed = true;
            }
        }

        return changed;
    }

    internal static MortalItemNpcCommandCollectionResult ApplyMortalNpcEquipmentCommands(
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
            if (!npcCommandIndex.TryGetOwners(npcId, out var owners))
            {
                throw new InvalidDataException(
                    $"NPCEquipmentChanges must resolve one exact logical NPC '{npcId}'.");
            }

            if (!npcCommandIndex.InventoryItemOccursExactlyOnce(npcId, itemId))
            {
                throw new InvalidDataException(
                    $"NPCEquipmentChanges itemId '{itemId}' must resolve once in NPC '{npcId}' inventory.");
            }

            var action = ReadExactMortalItemIdentity(command["action"]);
            foreach (var owner in owners)
            {
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
                    _ = index.TryGetOwners(npcId, out _);
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
                    _ = index.TryGetOwners(npcId, out _);
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

    internal sealed class MortalNpcCommandIndex
    {
        private readonly Dictionary<
            string,
            List<(string Section, JsonObject Actor)>> _ownersById =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> _confusableOwnerIds =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, int>>
            _inventoryItemCountsByNpcId = new(StringComparer.Ordinal);

        internal int WorkUnits { get; private set; }

        internal static MortalNpcCommandIndex Build(JsonObject? root)
        {
            var result = new MortalNpcCommandIndex();
            if (root == null)
                return result;
            foreach (var section in GuardianPolicyContracts
                         .NpcCoreCanonicalNpcObjectSections)
            {
                if (root[section] is not JsonArray npcs)
                    continue;
                foreach (var npc in npcs.OfType<JsonObject>())
                {
                    result.WorkUnits++;
                    var npcId = ReadMortalNpcIdentity(npc);
                    if (npcId == null)
                        continue;
                    if (!result._ownersById.TryGetValue(npcId, out var owners))
                    {
                        owners = new List<(string Section, JsonObject Actor)>();
                        result._ownersById.Add(npcId, owners);
                    }
                    owners.Add((section, npc));
                }
            }

            foreach (var group in result._ownersById.Keys.GroupBy(
                         ResourceMaterializationContract.BuildConfusableKey,
                         StringComparer.Ordinal).Where(static group => group.Count() > 1))
            {
                result._confusableOwnerIds.UnionWith(group);
            }

            return result;
        }

        internal bool TryGetOwners(
            string npcId,
            out IReadOnlyList<JsonObject> owners)
        {
            WorkUnits++;
            if (!_confusableOwnerIds.Contains(npcId) &&
                _ownersById.TryGetValue(npcId, out var copies) &&
                MortalItemNpcMirrorPolicy.IsSupportedLogicalActor(npcId, copies))
            {
                owners = copies.Select(static copy => copy.Actor).ToArray();
                return true;
            }

            owners = Array.Empty<JsonObject>();
            return false;
        }

        internal void RefreshInventoryItems()
        {
            _inventoryItemCountsByNpcId.Clear();
            foreach (var pair in _ownersById)
            {
                WorkUnits++;
                if (_confusableOwnerIds.Contains(pair.Key) ||
                    !MortalItemNpcMirrorPolicy.IsSupportedLogicalActor(
                        pair.Key,
                        pair.Value))
                    continue;

                var counts = new Dictionary<string, int>(StringComparer.Ordinal);
                if (pair.Value[0].Actor["inventory"] is JsonArray inventory)
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

    internal static bool IsRawMortalItemCreation(JsonObject item) =>
        item.ContainsKey("creationRef") ||
        item.TryGetPropertyValue("existedId", out var existedId) && existedId == null;

    internal static JsonObject CreateMortalItemIdentityEntry(
        JsonObject item,
        JsonObject receipt,
        int acceptedTurn,
        MortalItemRouteAuthority routeAuthority,
        IReadOnlyDictionary<string, string> creationMap,
        string? transitionId = null)
    {
        var itemId = RequireExactMortalItemIdentity(item["itemId"], "itemId");
        var envelope = item[MortalItemMaterializationContract.EnvelopeProperty]!.AsObject();
        var materializationId = RequireExactMortalItemIdentity(
            envelope["materializationId"],
            "materialization.materializationId");
        var quantity = ReadMortalItemQuantity(item);
        var carrier = CreateMortalItemCarrierNode(
            RewriteMortalItemCarrierCoordinate(routeAuthority.Destination, creationMap));
        var transition = transitionId == null
            ? MortalItemIdentityState.CreateTransition(
                "create",
                acceptedTurn,
                routeAuthority.SourceItemIds,
                sourceCarrier: null,
                destinationCarrier: carrier,
                quantityBefore: 0,
                quantityAfter: quantity,
                routeAuthority.AuthorityKind,
                routeAuthority.AuthorityId)
            : MortalItemIdentityState.CreateTransition(
                "create",
                acceptedTurn,
                routeAuthority.SourceItemIds,
                sourceCarrier: null,
                destinationCarrier: carrier,
                quantityBefore: 0,
                quantityAfter: quantity,
                routeAuthority.AuthorityKind,
                routeAuthority.AuthorityId,
                transitionId);

        return new JsonObject
        {
            ["itemId"] = itemId,
            ["receiptId"] = RequireExactMortalItemIdentity(receipt["receiptId"], "receiptId"),
            ["state"] = "active",
            ["currentCarrier"] = carrier.DeepClone(),
            ["originMaterializationIds"] = new JsonArray(materializationId),
            ["originCreationRefs"] = new JsonArray(
                RequireExactMortalItemIdentity(receipt["creationRef"], "creationRef")),
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

    internal static MortalItemCarrierCoordinate RewriteMortalItemCarrierCoordinate(
        MortalItemCarrierCoordinate carrier,
        IReadOnlyDictionary<string, string> creationMap) =>
        carrier with
        {
            ContainerPath = carrier.ContainerPath
                .Select(reference => creationMap.GetValueOrDefault(reference, reference))
                .ToArray()
        };

    internal static JsonObject CreateMortalItemCarrierNode(
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

    internal static void RewriteMortalItemContentsPath(
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

    internal static bool RewriteMortalItemCreationReferences(
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

    internal sealed record MortalItemNpcCommandCollectionResult(
        bool CommandsChanged,
        bool NpcCoreChanged);
}

internal static class AcceptedTurnCanonicalStateRefresh
{
    /// <summary>
    /// Reports canonical refresh diagnostics and the completed publication's detached handoffs.
    /// </summary>
    /// <param name="Issues">
    /// Validation diagnostics collected during canonical refresh and publication read-back.
    /// </param>
    /// <param name="MechanicsPlan">
    /// Accepted plan, or <see langword="null"/> when no plan was accepted.
    /// </param>
    /// <param name="TreatmentResourcePublicationTransaction">
    /// Treatment transaction requiring caller settlement, or <see langword="null"/> for other paths.
    /// </param>
    /// <param name="SpiritualWoundOutput">
    /// Detached spiritual presentation and exact output witnesses; <see langword="null"/> for ordinary or failed publication.
    /// </param>
    /// <param name="SpiritualConflictValidation">
    /// Published-only causal comparisons, or <see langword="null"/> without a successful spiritual publication.
    /// </param>
    internal sealed record Result(
        IReadOnlyList<ValidationIssue> Issues,
        AcceptedMechanicsPlan? MechanicsPlan,
        MortalWoundTreatmentResourcePublicationTransaction?
            TreatmentResourcePublicationTransaction = null,
        SpiritualWoundPublishedOutput? SpiritualWoundOutput = null,
        ValidationService.SpiritualOriginalTurnCapture.SpiritualCompletedConflictValidation?
            SpiritualConflictValidation = null);

    private static Task<MortalWoundTreatmentPublicationProbeResult>
        ProbeTreatmentResourcePublicationTransactionAsync(
            FileSystemManager fs,
            MortalWoundTreatmentResourcePublicationTransaction transaction) =>
        transaction.ProbeAsync(fs);

    private static Task<MortalWoundTreatmentPublicationOperationResult>
        CompleteTreatmentResourcePublicationTransactionAsync(
            FileSystemManager fs,
            MortalWoundTreatmentResourcePublicationTransaction transaction) =>
        transaction.CompleteAsync(fs);

    private static Task<MortalWoundTreatmentPublicationOperationResult>
        CompensateTreatmentResourcePublicationTransactionAsync(
            FileSystemManager fs,
            MortalWoundTreatmentResourcePublicationTransaction transaction) =>
        transaction.CompensateAsync(fs);

    private static Task<MortalWoundTreatmentPublicationOperationResult>
        ReleaseTreatmentResourcePublicationTerminalAsync(
            FileSystemManager fs,
            MortalWoundTreatmentResourcePublicationTransaction transaction,
            string reason) =>
        transaction.ReleaseTerminalAsync(fs, reason);

    internal static async Task<MortalWoundTreatmentPublicationOperationResult?>
        ReleaseValidatedTreatmentPublicationBeforeCanonicalRefreshAsync(
            FileSystemManager fs,
            string reason)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        await using var writeLease = await fs.AcquireCanonicalWriteLeaseAsync();
        if (!AcceptedMechanicsPlanAuthority.TryPeekValidated(
                fs,
                writeLease,
                out var binding,
                out var peeked))
        {
            return null;
        }
        if (!peeked.Success || peeked.Plan is null)
        {
            throw new InvalidDataException(
                "Pre-canonical treatment cleanup found an incomplete validated accepted-mechanics plan.");
        }

        var plan = peeked.Plan;
        var authority = plan.TreatmentResourcePublicationAuthority;
        if (authority is null || !authority.RequiresCoordinatedSettlement)
            return null;
        if (!authority.HasValidSeal())
        {
            throw new InvalidDataException(
                "Pre-canonical treatment cleanup requires the exact sealed resource-publication authority.");
        }

        var hasCurrentMortalItemSnapshot =
            MortalItemAcceptedTurnAuthority.TryCaptureNormalizationSnapshot(
                fs,
                writeLease,
                binding.SessionId,
                binding.SnapshotToken,
                binding.Turn,
                out var mortalItemSnapshot);
        if (hasCurrentMortalItemSnapshot &&
            !mortalItemSnapshot.MatchesAcceptedOwnerAuthority(plan.OwnerAuthority))
        {
            throw new InvalidDataException(
                "Pre-canonical treatment cleanup could not capture the exact Mortal item authority snapshot.");
        }

        var beforeImages = await CaptureBeforeImagesAsync(fs, writeLease);
        var commandBefore = beforeImages.SingleOrDefault(value => string.Equals(
            value.Path,
            AcceptedMechanicsPlan.WoundCommandPath,
            StringComparison.Ordinal));
        var pendingBefore = beforeImages.SingleOrDefault(value => string.Equals(
            value.Path,
            WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
            StringComparison.Ordinal));
        if (commandBefore?.Bytes is not { } commandBytes ||
            pendingBefore is null)
        {
            throw new InvalidDataException(
                "Pre-canonical treatment cleanup requires exact command and pending before-images.");
        }
        var command = ParseTreatmentDurableRoot(
            commandBytes,
            AcceptedMechanicsPlan.WoundCommandPath);
        var pending = pendingBefore.Bytes is { } pendingBytes
            ? ParseTreatmentDurableRoot(
                pendingBytes,
                WoundAcceptedTurnSnapshotContract.PendingResolutionPath)
            : null;
        if (!MortalWoundTreatmentDurableSurfaceQuarantine.ContainsExactRequestRows(
                command,
                pending,
                authority.RequestAuthority.Coordinates.OperationKey,
                authority.RequestAuthority.Coordinates.AttemptId,
                authority.RequestFingerprint))
        {
            throw new InvalidDataException(
                "Pre-canonical treatment cleanup could not prove the exact durable request row before take.");
        }

        MortalWoundTreatmentResourcePublicationTransaction? transaction = null;
        try
        {
            var tookPublication = hasCurrentMortalItemSnapshot
                ? AcceptedMechanicsPlanAuthority
                    .TryTakeCurrentValidatedTreatmentPublicationForTerminalRelease(
                    fs,
                    writeLease,
                    binding,
                    mortalItemSnapshot,
                    out var taken,
                    out var receipt)
                : AcceptedMechanicsPlanAuthority
                    .TryTakeValidatedTreatmentPublicationForTerminalRelease(
                        fs,
                        writeLease,
                        binding,
                        out taken,
                        out receipt);
            if (!tookPublication ||
                !taken.Success || taken.Plan is null ||
                !ReferenceEquals(plan, taken.Plan))
            {
                throw new InvalidDataException(
                    "Pre-canonical treatment cleanup could not take the exact validated publication plan.");
            }

            transaction = MortalWoundTreatmentResourcePublicationTransaction.Create(
                fs,
                plan,
                binding,
                receipt,
                beforeImages);
            var released = await transaction.ReleaseTerminalUnderLeaseAsync(
                fs,
                writeLease,
                reason);
            var releasedExactly = released.IsValid &&
                                  released.Issues.Count == 0 &&
                                  released.ChangedCount == 1 &&
                                  released.Outcome ==
                                      MortalWoundTreatmentPublicationTransactionOutcome
                                          .Released;
            var safelyRestartBlocked =
                MortalWoundTreatmentResourcePublicationTransaction
                    .IsExactProvenTerminalReleaseFailure(released);
            if (!releasedExactly && !safelyRestartBlocked)
            {
                throw new InvalidOperationException(
                    "Pre-canonical treatment cleanup did not atomically quarantine and release the exact held request.");
            }
            return released;
        }
        catch (SessionReplacedException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (transaction is null)
                throw;

            try
            {
                await transaction.FinishHelperFailureAsync(fs, writeLease);
            }
            catch (Exception settlementException)
            {
                throw new AggregateException(
                    "Pre-canonical treatment cleanup failed and its transaction could not be settled safely.",
                    exception,
                    settlementException);
            }
            ExceptionDispatchInfo.Capture(exception).Throw();
            throw;
        }
    }

    private static JsonObject ParseTreatmentDurableRoot(
        byte[] bytes,
        string path)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        var offset = bytes.AsSpan().StartsWith(preamble) ? preamble.Length : 0;
        try
        {
            return JsonNode.Parse(bytes.AsSpan(offset)) as JsonObject ??
                   throw new InvalidDataException(
                       $"Treatment durable root '{path}' is not a JSON object.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Treatment durable root '{path}' is malformed.",
                exception);
        }
    }

    internal static async Task<IReadOnlyList<ValidationIssue>> NormalizeAndValidateAsync(
        FileSystemManager fs,
        CanonicalStateNormalizer normalizer,
        ValidationService validator,
        IReadOnlyDictionary<string, string> backups)
    {
        var result = await NormalizeAndValidateWithPlanAsync(
            fs,
            normalizer,
            validator,
            backups);
        return result.Issues;
    }

    internal static async Task<Result> NormalizeAndValidateWithPlanAsync(
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
        var boundNormalizer = normalizer.BindTo(writeLease);
        var spiritualPreflight = await boundNormalizer.PrevalidateSpiritualPublicationTransactionAsync();
        var beforeImages = await CaptureBeforeImagesAsync(fs, writeLease);
        if (spiritualPreflight is not null)
        {
            var signed = spiritualPreflight.Authority.SignedRollbackImages;
            foreach (var image in beforeImages)
            {
                if (!signed.ContainsKey(image.Path))
                    throw new InvalidDataException($"Spiritual publication lacks signed rollback coverage for '{image.Path}'.");
            }
            beforeImages = signed.Select(pair => new MortalWoundTreatmentPublicationBeforeImage(
                pair.Key, pair.Value.Bytes)).ToArray();
        }
        SpiritualPublicationReceipt? spiritualReceipt = null;
        var spiritualWritesStarted = false;
        MortalWoundTreatmentResourcePublicationTransaction? treatmentTransaction = null;
        try
        {
            var treatmentPreflight = spiritualPreflight is null
                ? await boundNormalizer.PrevalidateTreatmentResourcePublicationTransactionAsync()
                : null;
            AcceptedMechanicsPlan? mechanicsPlan;
            if (spiritualPreflight is not null)
            {
                var inputIssues = await spiritualPreflight.Authority.ValidateCurrentInputsAsync(fs, writeLease);
                if (inputIssues.Count != 0)
                    throw new InvalidDataException(string.Join("; ", inputIssues.Select(issue =>
                        $"{issue.Code}: {issue.FilePath}")));
                if (!AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(
                        fs, writeLease, spiritualPreflight.Authority, out spiritualReceipt))
                    throw new InvalidDataException("The completed spiritual publication could not take its exact handoff.");
                spiritualWritesStarted = true;
                mechanicsPlan = await boundNormalizer.NormalizeAccumulatedStateWithSpiritualPublicationTransactionAsync(
                    backups, spiritualPreflight, spiritualReceipt);
            }
            else if (treatmentPreflight is null)
            {
                mechanicsPlan = await boundNormalizer
                    .NormalizeAccumulatedStateWithPlanAsync(backups);
            }
            else
            {
                if (!AcceptedMechanicsPlanAuthority
                        .TryTakeValidatedTreatmentPublication(
                            fs,
                            writeLease,
                            treatmentPreflight.Binding,
                            treatmentPreflight.MortalItemSnapshot,
                            out var taken,
                            out var receipt) ||
                    !taken.Success || taken.Plan is null ||
                    !ReferenceEquals(treatmentPreflight.Plan, taken.Plan))
                {
                    throw new InvalidDataException(
                        "The held Mortal wound-treatment publication transaction could not take the exact validated plan.");
                }

                treatmentTransaction =
                    MortalWoundTreatmentResourcePublicationTransaction.Create(
                        fs,
                        treatmentPreflight.Plan,
                        treatmentPreflight.Binding,
                        receipt,
                        beforeImages);
                mechanicsPlan = await boundNormalizer
                    .NormalizeAccumulatedStateWithTreatmentPublicationTransactionAsync(
                        backups,
                        treatmentPreflight,
                        receipt);
            }
            var issues = new List<ValidationIssue>();
            issues.AddRange(await validator
                .ValidateAcceptedTurnCanonicalMortalLocationMaterializationAsync(writeLease));
            issues.AddRange(await validator
                .ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync(writeLease));
            issues.AddRange(await validator
                .ValidateAcceptedTurnCanonicalResourceMaterializationAsync(writeLease));
            issues.AddRange(await validator
                .ValidateAcceptedTurnCanonicalEffectMaterializationAsync(writeLease));
            if (mechanicsPlan?.WoundStageBundle is not null)
            {
                issues.AddRange(await WoundAcceptedTurnSnapshotContract
                    .ValidatePublishedOutputAuthorityAsync(
                        mechanicsPlan,
                        path => fs.ReadFileBytesAsync(writeLease, path)));
            }
            if (mechanicsPlan is { AwaitsPendingResolution: false } &&
                (mechanicsPlan.WoundStageBundle is not null || mechanicsPlan.LiveWoundProofFingerprint is not null))
                issues.AddRange(await validator.ValidateAcceptedTurnCanonicalWoundMaterializationAsync(writeLease));
            var spiritualOutput = spiritualReceipt is null ? null :
                await SpiritualWoundPublishedOutput.BindAsync(fs, writeLease, spiritualReceipt);
            if (spiritualOutput is not null)
                issues.AddRange(spiritualOutput.Issues);
            if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
            {
                await RestoreBeforeImagesAsync(fs, writeLease, beforeImages);
                if (spiritualReceipt is not null)
                    AcceptedMechanicsPlanAuthority.FailSpiritualPublication(fs, writeLease, spiritualReceipt);
                mechanicsPlan = null;
                spiritualOutput = null;
            }
            else if (treatmentTransaction is not null)
            {
                await treatmentTransaction.CapturePublishedAgreementAsync(
                    fs,
                    writeLease);
            }
            if (mechanicsPlan is not null && spiritualReceipt is not null &&
                !AcceptedMechanicsPlanAuthority.CompleteSpiritualPublication(fs, writeLease, spiritualReceipt))
                throw new InvalidDataException("Spiritual transaction ownership changed before acceptance.");
            return new Result(issues, mechanicsPlan, treatmentTransaction, spiritualOutput,
                mechanicsPlan is null ? null : spiritualReceipt?.Authority.CompletedConflictValidation);
        }
        catch (Exception exception)
        {
            var rollbackFailures = new List<Exception>();
            try
            {
                if (spiritualPreflight is null || spiritualWritesStarted)
                    await RestoreBeforeImagesAsync(fs, writeLease, beforeImages);
            }
            catch (Exception rollbackException)
            {
                rollbackFailures.Add(rollbackException);
            }
            if (spiritualReceipt is not null)
                AcceptedMechanicsPlanAuthority.FailSpiritualPublication(fs, writeLease, spiritualReceipt);

            if (treatmentTransaction is not null)
            {
                try
                {
                    await treatmentTransaction.FinishHelperFailureAsync(
                        fs,
                        writeLease);
                }
                catch (Exception rollbackException)
                {
                    rollbackFailures.Add(rollbackException);
                }
            }

            if (rollbackFailures.Count != 0)
            {
                throw new AggregateException(
                    "Accepted-turn canonical normalization failed and exact rollback also failed.",
                    new[] { exception }.Concat(rollbackFailures));
            }

            ExceptionDispatchInfo.Capture(exception).Throw();
            throw;
        }
    }

    private static async Task<IReadOnlyList<
        MortalWoundTreatmentPublicationBeforeImage>> CaptureBeforeImagesAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        var beforeImages = new List<MortalWoundTreatmentPublicationBeforeImage>(
            CanonicalStateNormalizer.NormalizerRollbackTrackedFiles.Length);
        foreach (var path in CanonicalStateNormalizer.NormalizerRollbackTrackedFiles
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            beforeImages.Add(new MortalWoundTreatmentPublicationBeforeImage(
                path,
                await fs.ReadFileBytesAsync(writeLease, path)));
        }

        return beforeImages;
    }

    private static async Task RestoreBeforeImagesAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        IReadOnlyList<MortalWoundTreatmentPublicationBeforeImage> beforeImages)
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

}
