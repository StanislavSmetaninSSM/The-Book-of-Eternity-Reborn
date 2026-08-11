using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record MortalItemCarrierCoordinate(
    string Kind,
    string OwnerId,
    string? ContainerId,
    IReadOnlyList<string> ContainerPath);

internal sealed record MortalItemCarrierOccurrence(
    string? ItemId,
    string? CreationRef,
    string? ReceiptId,
    string? MaterializationId,
    string FilePath,
    string JsonPath,
    MortalItemCarrierCoordinate Carrier,
    JsonObject Item);

internal sealed record MortalItemCompanionReference(
    string Reference,
    string PropertyName,
    string FilePath,
    string JsonPath,
    MortalItemCarrierCoordinate? ExpectedCarrier = null);

internal sealed record MortalItemCarrierCatalogIssue(
    string Code,
    string Path,
    string Message,
    string IdentityKind,
    string? Identity,
    string? FirstPath = null);

internal sealed record MortalItemCarrierCatalogInput(
    JsonObject? PlayerInventory,
    JsonObject? NpcCore,
    JsonObject? NpcInventoryCommands,
    JsonObject? CurrentLocation,
    JsonObject? Vehicles,
    IReadOnlyDictionary<string, JsonObject> CompanionRoots);

internal sealed record MortalItemCatalogScanMetrics(int Items, int Companions, int Routes)
{
    internal int TotalVisited => Items + Companions + Routes;
}

/// <summary>
/// Builds exact, ordinal indexes for every durable Mortal item carrier in one
/// deterministic pass. Companion files are walked separately and can only
/// contribute references, never carrier occurrences.
/// </summary>
internal sealed class MortalItemCarrierCatalog
{
    private const string PlayerInventoryPath = "game_state/inventory/items.json";
    private const string NpcCorePath = "game_state/npcs/npc_core.json";
    private const string NpcInventoryCommandsPath = "game_state/npcs/npc_inventory.json";
    private const string CurrentLocationPath = "game_state/world/current_location.json";
    private const string VehiclesPath = "game_state/misc/vehicles.json";

    private MortalItemCarrierCatalog(Builder builder)
    {
        Occurrences = builder.Occurrences.ToArray();
        ByItemId = Freeze(builder.ByItemId);
        ByCreationRef = Freeze(builder.ByCreationRef);
        ByReceiptId = Freeze(builder.ByReceiptId);
        ByMaterializationId = Freeze(builder.ByMaterializationId);
        ByCompanionReference = Freeze(builder.ByCompanionReference);
        Issues = builder.Issues.ToArray();
        Metrics = new MortalItemCatalogScanMetrics(
            builder.ItemsVisited,
            builder.CompanionNodesVisited,
            builder.RouteNodesVisited);
    }

    internal IReadOnlyList<MortalItemCarrierOccurrence> Occurrences { get; }

    internal IReadOnlyDictionary<string, IReadOnlyList<MortalItemCarrierOccurrence>> ByItemId { get; }

    internal IReadOnlyDictionary<string, IReadOnlyList<MortalItemCarrierOccurrence>> ByCreationRef { get; }

    internal IReadOnlyDictionary<string, IReadOnlyList<MortalItemCarrierOccurrence>> ByReceiptId { get; }

    internal IReadOnlyDictionary<string, IReadOnlyList<MortalItemCarrierOccurrence>> ByMaterializationId { get; }

    internal IReadOnlyDictionary<string, IReadOnlyList<MortalItemCompanionReference>> ByCompanionReference { get; }

    internal IReadOnlyList<MortalItemCarrierCatalogIssue> Issues { get; }

    internal MortalItemCatalogScanMetrics Metrics { get; }

    internal static MortalItemCarrierCatalog Build(MortalItemCarrierCatalogInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var builder = new Builder();
        builder.ScanPlayerInventory(input.PlayerInventory);
        builder.ScanNpcCore(input.NpcCore);
        builder.ScanNpcInventoryCommands(input.NpcInventoryCommands);
        builder.ScanCurrentLocation(input.CurrentLocation);
        builder.ScanVehicles(input.Vehicles);
        builder.ScanCompanionRoots(input.CompanionRoots);
        return new MortalItemCarrierCatalog(builder);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<T>> Freeze<T>(
        Dictionary<string, List<T>> source)
    {
        var result = new Dictionary<string, IReadOnlyList<T>>(source.Count, StringComparer.Ordinal);
        foreach (var pair in source)
            result.Add(pair.Key, pair.Value.ToArray());
        return result;
    }

    private sealed class Builder
    {
        private static readonly string[] NpcSections = { "UpdateNPCs", "NPCsInScene" };
        private static readonly string[] NpcIdentityFields = { "NPCId", "npcId", "id", "initialId" };
        private static readonly HashSet<string> DirectCompanionReferenceProperties =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "itemId",
                "existedId",
                "creationRef",
                "itemRef",
                "itemCreationRef",
                "sourceItemId",
                "targetItemId",
                "parentItemId",
                "containerItemId",
                "rewardItemId",
                "destinationItemId",
                "resultItemId"
            };
        private static readonly HashSet<string> CompanionReferenceArrayProperties =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "itemIds",
                "sourceItemIds",
                "targetItemIds",
                "parentItemIds",
                "contentsPath"
            };
        private static readonly HashSet<string> CompanionReferenceMapProperties =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "equippedItems",
                "equipment",
                "equipmentSlots"
            };

        private readonly IdentityAmbiguityTracker _ambiguity;
        private readonly Dictionary<string, string> _firstItemPath = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _firstReceiptPath = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _firstRootCreationPath = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _firstRootMaterializationPath = new(StringComparer.Ordinal);

        internal Builder()
        {
            _ambiguity = new IdentityAmbiguityTracker(Issues);
        }

        internal List<MortalItemCarrierOccurrence> Occurrences { get; } = new();

        internal Dictionary<string, List<MortalItemCarrierOccurrence>> ByItemId { get; } =
            new(StringComparer.Ordinal);

        internal Dictionary<string, List<MortalItemCarrierOccurrence>> ByCreationRef { get; } =
            new(StringComparer.Ordinal);

        internal Dictionary<string, List<MortalItemCarrierOccurrence>> ByReceiptId { get; } =
            new(StringComparer.Ordinal);

        internal Dictionary<string, List<MortalItemCarrierOccurrence>> ByMaterializationId { get; } =
            new(StringComparer.Ordinal);

        internal Dictionary<string, List<MortalItemCompanionReference>> ByCompanionReference { get; } =
            new(StringComparer.Ordinal);

        internal List<MortalItemCarrierCatalogIssue> Issues { get; } = new();

        internal int ItemsVisited { get; private set; }

        internal int CompanionNodesVisited { get; private set; }

        internal int RouteNodesVisited { get; private set; }

        internal void ScanPlayerInventory(JsonObject? root)
        {
            if (root == null)
                return;

            RouteNodesVisited++;
            ScanItemArray(
                root["items"] as JsonArray,
                PlayerInventoryPath,
                $"{PlayerInventoryPath}.items",
                new MortalItemCarrierCoordinate(
                    "player_inventory",
                    "player",
                    null,
                    Array.Empty<string>()));
            ScanItemArray(
                root["UpdateInventory"] as JsonArray,
                PlayerInventoryPath,
                $"{PlayerInventoryPath}.UpdateInventory",
                new MortalItemCarrierCoordinate(
                    "player_inventory",
                    "player",
                    null,
                    Array.Empty<string>()));
            ScanInlineCompanionSurface(
                PlayerInventoryPath,
                $"{PlayerInventoryPath}.equipment",
                root["equipment"],
                new MortalItemCarrierCoordinate(
                    "player_inventory",
                    "player",
                    null,
                    Array.Empty<string>()));
            ScanInlineCompanionSurface(
                PlayerInventoryPath,
                $"{PlayerInventoryPath}.equippedItems",
                root["equippedItems"],
                new MortalItemCarrierCoordinate(
                    "player_inventory",
                    "player",
                    null,
                    Array.Empty<string>()));
        }

        internal void ScanNpcCore(JsonObject? root)
        {
            if (root == null)
                return;

            RouteNodesVisited++;
            foreach (var sectionName in NpcSections)
            {
                if (root[sectionName] is not JsonArray npcs)
                    continue;

                for (var index = 0; index < npcs.Count; index++)
                {
                    if (npcs[index] is not JsonObject npc)
                        continue;

                    RouteNodesVisited++;
                    var npcPath = $"{NpcCorePath}.{sectionName}[{index}]";
                    var inventory = npc["inventory"] as JsonArray;
                    var hasInventoryItems =
                        inventory != null && ContainsItemObject(inventory);
                    var hasEquipmentSurface =
                        ContainsExactScalarReference(npc["equippedItems"]) ||
                        ContainsExactScalarReference(npc["equipment"]);
                    var npcId = hasInventoryItems || hasEquipmentSurface
                        ? ReadCarrierIdentity(npc, NpcIdentityFields, npcPath, "npc owner")
                        : null;
                    var expectedCarrier = npcId == null
                        ? null
                        : new MortalItemCarrierCoordinate(
                            "npc_inventory",
                            npcId,
                            null,
                            Array.Empty<string>());
                    ScanInlineCompanionSurface(
                        NpcCorePath,
                        $"{npcPath}.equippedItems",
                        npc["equippedItems"],
                        expectedCarrier);
                    ScanInlineCompanionSurface(
                        NpcCorePath,
                        $"{npcPath}.equipment",
                        npc["equipment"],
                        expectedCarrier);
                    if (!hasInventoryItems)
                        continue;

                    ScanItemArray(
                        inventory!,
                        NpcCorePath,
                        $"{npcPath}.inventory",
                        new MortalItemCarrierCoordinate(
                            "npc_inventory",
                            npcId ?? string.Empty,
                            null,
                            Array.Empty<string>()));
                }
            }
        }

        internal void ScanNpcInventoryCommands(JsonObject? root)
        {
            if (root == null)
                return;

            RouteNodesVisited++;
            if (root["NPCEquipmentChanges"] is JsonArray equipmentChanges)
            {
                for (var index = 0; index < equipmentChanges.Count; index++)
                {
                    var command = equipmentChanges[index] as JsonObject;
                    var hasItemReference = command != null &&
                                           DirectCompanionReferenceProperties.Any(property =>
                                               ReadString(command[property]) != null);
                    var npcId = !hasItemReference
                        ? null
                        : ReadCarrierIdentity(
                            command!,
                            NpcIdentityFields,
                            $"{NpcInventoryCommandsPath}.NPCEquipmentChanges[{index}]",
                            "npc owner");
                    ScanCompanionNode(
                        NpcInventoryCommandsPath,
                        $"{NpcInventoryCommandsPath}.NPCEquipmentChanges[{index}]",
                        equipmentChanges[index],
                        valuesAreReferences: false,
                        npcId == null
                            ? null
                            : new MortalItemCarrierCoordinate(
                                "npc_inventory",
                                npcId,
                                null,
                                Array.Empty<string>()));
                }
            }
            if (root["NPCInventoryAdds"] is not JsonArray adds)
                return;

            for (var index = 0; index < adds.Count; index++)
            {
                if (adds[index] is not JsonObject command)
                    continue;

                RouteNodesVisited++;
                var commandPath = $"{NpcInventoryCommandsPath}.NPCInventoryAdds[{index}]";
                if (command["item"] is not JsonObject item)
                    continue;
                var npcId = ReadCarrierIdentity(command, NpcIdentityFields, commandPath, "npc owner");
                if (ReadString(command["destinationContainerId"]) is { } containerReference)
                {
                    AddCompanionReference(
                        containerReference,
                        "destinationContainerId",
                        NpcInventoryCommandsPath,
                        $"{commandPath}.destinationContainerId",
                        npcId == null
                            ? null
                            : new MortalItemCarrierCoordinate(
                                "npc_inventory",
                                npcId,
                                null,
                                Array.Empty<string>()));
                }

                AddItem(
                    item,
                    NpcInventoryCommandsPath,
                    $"{commandPath}.item",
                    new MortalItemCarrierCoordinate(
                        "npc_inventory",
                        npcId ?? string.Empty,
                        null,
                        ReadContainerPath(item, $"{commandPath}.item")));
            }
        }

        internal void ScanCurrentLocation(JsonObject? root)
        {
            if (root == null)
                return;

            RouteNodesVisited++;
            var location = root["currentLocationData"] as JsonObject ?? root;
            var locationPath = ReferenceEquals(location, root)
                ? CurrentLocationPath
                : $"{CurrentLocationPath}.currentLocationData";
            if (location["locationStorages"] is not JsonArray storages)
                return;

            string? locationId = null;
            var locationIdentityRead = false;
            for (var index = 0; index < storages.Count; index++)
            {
                if (storages[index] is not JsonObject storage)
                    continue;

                RouteNodesVisited++;
                var storagePath = $"{locationPath}.locationStorages[{index}]";
                var contents = storage["contents"] as JsonArray;
                var hasContentsItems =
                    contents != null && ContainsItemObject(contents);
                var hasInlineReferences =
                    ContainsExactScalarReference(storage["itemIds"]);
                if (!hasContentsItems && !hasInlineReferences)
                    continue;

                if (!locationIdentityRead)
                {
                    locationId = ReadCarrierIdentity(
                        location,
                        new[] { "locationId", "id" },
                        locationPath,
                        "location owner");
                    locationIdentityRead = true;
                }

                var storageId = ReadCarrierIdentity(
                    storage,
                    new[] { "storageId" },
                    storagePath,
                    "storage container");
                var expectedCarrier = locationId == null || storageId == null
                    ? null
                    : new MortalItemCarrierCoordinate(
                        "location_storage",
                        locationId,
                        storageId,
                        Array.Empty<string>());
                ScanInlineCompanionSurface(
                    CurrentLocationPath,
                    $"{storagePath}.itemIds",
                    storage["itemIds"],
                    expectedCarrier);
                if (!hasContentsItems)
                    continue;

                ScanItemArray(
                    contents!,
                    CurrentLocationPath,
                    $"{storagePath}.contents",
                    new MortalItemCarrierCoordinate(
                        "location_storage",
                        locationId ?? string.Empty,
                        storageId,
                        Array.Empty<string>()));
            }
        }

        internal void ScanVehicles(JsonObject? root)
        {
            if (root == null)
                return;

            RouteNodesVisited++;
            if (root["vehicles"] is not JsonArray vehicles)
                return;

            for (var index = 0; index < vehicles.Count; index++)
            {
                if (vehicles[index] is not JsonObject vehicle)
                    continue;

                RouteNodesVisited++;
                var vehiclePath = $"{VehiclesPath}.vehicles[{index}]";
                if (vehicle["inventory"] is not JsonArray inventory || !ContainsItemObject(inventory))
                    continue;

                var vehicleId = ReadCarrierIdentity(
                    vehicle,
                    new[] { "vehicleId", "id" },
                    vehiclePath,
                    "vehicle owner");

                ScanItemArray(
                    inventory,
                    VehiclesPath,
                    $"{vehiclePath}.inventory",
                    new MortalItemCarrierCoordinate(
                        "vehicle_inventory",
                        vehicleId ?? string.Empty,
                        null,
                        Array.Empty<string>()));
            }
        }

        internal void ScanCompanionRoots(IReadOnlyDictionary<string, JsonObject>? roots)
        {
            if (roots == null)
                return;

            foreach (var pair in roots.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                ScanCompanionNode(pair.Key, pair.Key, pair.Value, valuesAreReferences: false);
        }

        private void ScanItemArray(
            JsonArray? items,
            string filePath,
            string arrayPath,
            MortalItemCarrierCoordinate rootCarrier)
        {
            if (items == null)
                return;

            for (var index = 0; index < items.Count; index++)
            {
                if (items[index] is not JsonObject item)
                    continue;

                var itemPath = $"{arrayPath}[{index}]";
                AddItem(
                    item,
                    filePath,
                    itemPath,
                    rootCarrier with { ContainerPath = ReadContainerPath(item, itemPath) });
            }
        }

        private static bool ContainsItemObject(JsonArray items)
        {
            foreach (var item in items)
            {
                if (item is JsonObject)
                    return true;
            }

            return false;
        }

        private static bool ContainsExactScalarReference(JsonNode? node) =>
            node switch
            {
                JsonValue value => ReadString(value) != null,
                JsonObject obj => obj.Any(pair =>
                    ContainsExactScalarReference(pair.Value)),
                JsonArray array => array.Any(ContainsExactScalarReference),
                _ => false
            };

        private void AddItem(
            JsonObject item,
            string filePath,
            string jsonPath,
            MortalItemCarrierCoordinate carrier)
        {
            ItemsVisited++;

            var itemId = ReadString(item["itemId"]);
            var topLevelCreationRef = ReadString(item["creationRef"]);
            var envelope = item["materialization"] as JsonObject;
            var receipt = item["materializationReceipt"] as JsonObject;
            var envelopeCreationRef = ReadString(envelope?["creationRef"]);
            var creationRef = topLevelCreationRef ?? envelopeCreationRef;
            var materializationId = ReadString(envelope?["materializationId"]);
            var receiptId = ReadString(receipt?["receiptId"]);
            var instanceKind = ReadString(receipt?["instanceKind"]);

            if (topLevelCreationRef != null &&
                envelopeCreationRef != null &&
                !string.Equals(topLevelCreationRef, envelopeCreationRef, StringComparison.Ordinal))
            {
                Issues.Add(new MortalItemCarrierCatalogIssue(
                    "mortal_item_materialization_identity_conflict",
                    $"{jsonPath}.creationRef",
                    "Top-level and materialization creationRef values are not ordinal-equal.",
                    "creationRef",
                    topLevelCreationRef,
                    $"{jsonPath}.materialization.creationRef"));
            }

            var occurrence = new MortalItemCarrierOccurrence(
                itemId,
                creationRef,
                receiptId,
                materializationId,
                filePath,
                jsonPath,
                carrier,
                item.DeepClone().AsObject());
            Occurrences.Add(occurrence);

            for (var index = 0; index < carrier.ContainerPath.Count; index++)
            {
                AddCompanionReference(
                    carrier.ContainerPath[index],
                    "contentsPath",
                    filePath,
                    $"{jsonPath}.contentsPath[{index}]",
                    carrier);
            }

            if (itemId != null)
            {
                AddIndex(ByItemId, itemId, occurrence);
                AddUniqueIdentity(
                    _firstItemPath,
                    itemId,
                    jsonPath,
                    "itemId",
                    "mortal_item_materialization_duplicate_item_id");
                _ambiguity.Observe("itemId", itemId, $"{jsonPath}.itemId");
            }

            if (receiptId != null)
            {
                AddIndex(ByReceiptId, receiptId, occurrence);
                AddUniqueIdentity(
                    _firstReceiptPath,
                    receiptId,
                    jsonPath,
                    "receiptId",
                    "mortal_item_materialization_duplicate_receipt_id");
                _ambiguity.Observe("receiptId", receiptId, $"{jsonPath}.materializationReceipt.receiptId");
            }

            var isIndependentRoot = !string.Equals(
                instanceKind,
                "split_derived",
                StringComparison.Ordinal);
            if (creationRef != null)
            {
                AddIndex(ByCreationRef, creationRef, occurrence);
                _ambiguity.Observe("creationRef", creationRef, $"{jsonPath}.materialization.creationRef");
                if (isIndependentRoot)
                {
                    AddUniqueIdentity(
                        _firstRootCreationPath,
                        creationRef,
                        jsonPath,
                        "creationRef",
                        "mortal_item_materialization_duplicate_creation_ref");
                }
            }

            if (materializationId != null)
            {
                AddIndex(ByMaterializationId, materializationId, occurrence);
                _ambiguity.Observe(
                    "materializationId",
                    materializationId,
                    $"{jsonPath}.materialization.materializationId");
                if (isIndependentRoot)
                {
                    AddUniqueIdentity(
                        _firstRootMaterializationPath,
                        materializationId,
                        jsonPath,
                        "materializationId",
                        "mortal_item_materialization_duplicate_materialization_id");
                }
            }
        }

        private IReadOnlyList<string> ReadContainerPath(JsonObject item, string itemPath)
        {
            if (item["contentsPath"] == null)
                return Array.Empty<string>();
            if (item["contentsPath"] is not JsonArray contentsPath)
            {
                Issues.Add(new MortalItemCarrierCatalogIssue(
                    "mortal_item_materialization_invalid_container_path",
                    $"{itemPath}.contentsPath",
                    "contentsPath must be null or an ordered array of exact item IDs.",
                    "contentsPath",
                    null));
                return Array.Empty<string>();
            }

            var result = new List<string>(contentsPath.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var itemId = ReadString(item["itemId"]);
            for (var index = 0; index < contentsPath.Count; index++)
            {
                var value = ReadString(contentsPath[index]);
                if (value == null ||
                    string.IsNullOrWhiteSpace(value) ||
                    !string.Equals(value, value.Trim(), StringComparison.Ordinal))
                {
                    Issues.Add(new MortalItemCarrierCatalogIssue(
                        "mortal_item_materialization_invalid_container_path",
                        $"{itemPath}.contentsPath[{index}]",
                        "Every contentsPath entry must be a non-empty exact item ID.",
                        "contentsPath",
                        value));
                    if (value == null)
                        continue;
                }

                if (!seen.Add(value) || string.Equals(value, itemId, StringComparison.Ordinal))
                {
                    Issues.Add(new MortalItemCarrierCatalogIssue(
                        "mortal_item_materialization_invalid_container_path",
                        $"{itemPath}.contentsPath[{index}]",
                        "contentsPath cannot repeat an item ID or contain the child item itself.",
                        "contentsPath",
                        value));
                }
                result.Add(value);
                _ambiguity.Observe("containerPathItemId", value, $"{itemPath}.contentsPath[{index}]");
            }

            return result.ToArray();
        }

        private string? ReadCarrierIdentity(
            JsonObject owner,
            IReadOnlyList<string> fieldNames,
            string ownerPath,
            string identityKind)
        {
            string? resolved = null;
            string? resolvedPath = null;
            foreach (var fieldName in fieldNames)
            {
                var candidate = ReadString(owner[fieldName]);
                if (candidate == null)
                    continue;

                if (resolved == null)
                {
                    resolved = candidate;
                    resolvedPath = $"{ownerPath}.{fieldName}";
                    continue;
                }

                if (!string.Equals(resolved, candidate, StringComparison.Ordinal))
                {
                    Issues.Add(new MortalItemCarrierCatalogIssue(
                        "mortal_item_materialization_carrier_identity_conflict",
                        $"{ownerPath}.{fieldName}",
                        $"Conflicting exact aliases identify one {identityKind}.",
                        identityKind,
                        candidate,
                        resolvedPath));
                }
            }

            if (resolved == null)
            {
                Issues.Add(new MortalItemCarrierCatalogIssue(
                    "mortal_item_materialization_carrier_identity_missing",
                    ownerPath,
                    $"A non-empty exact identity is required for the {identityKind}.",
                    identityKind,
                    null));
                return null;
            }

            _ambiguity.Observe(identityKind, resolved, resolvedPath!);
            return resolved;
        }

        private void ScanCompanionNode(
            string filePath,
            string jsonPath,
            JsonNode? node,
            bool valuesAreReferences,
            MortalItemCarrierCoordinate? expectedCarrier = null)
        {
            if (node == null)
                return;
            if (node is JsonObject unavailableReward &&
                string.Equals(
                    filePath,
                    "game_state/quests/quest_history.json",
                    StringComparison.Ordinal) &&
                QuestRewardAuthority.IsExplicitlyUnavailableReward(unavailableReward))
            {
                return;
            }

            CompanionNodesVisited++;
            switch (node)
            {
                case JsonObject obj:
                    foreach (var pair in obj)
                    {
                        var childPath = $"{jsonPath}.{pair.Key}";
                        var childValuesAreReferences =
                            CompanionReferenceMapProperties.Contains(pair.Key);
                        if ((valuesAreReferences || DirectCompanionReferenceProperties.Contains(pair.Key)) &&
                            ReadString(pair.Value) is { } directReference)
                        {
                            AddCompanionReference(
                                directReference,
                                pair.Key,
                                filePath,
                                childPath,
                                expectedCarrier);
                        }

                        if (CompanionReferenceArrayProperties.Contains(pair.Key) &&
                            pair.Value is JsonArray referenceArray)
                        {
                            for (var index = 0; index < referenceArray.Count; index++)
                            {
                                if (ReadString(referenceArray[index]) is not { } reference)
                                    continue;
                                AddCompanionReference(
                                    reference,
                                    pair.Key,
                                    filePath,
                                    $"{childPath}[{index}]",
                                    expectedCarrier);
                            }
                        }

                        ScanCompanionNode(
                            filePath,
                            childPath,
                            pair.Value,
                            childValuesAreReferences,
                            expectedCarrier);
                    }
                    break;

                case JsonArray array:
                    for (var index = 0; index < array.Count; index++)
                    {
                        ScanCompanionNode(
                            filePath,
                            $"{jsonPath}[{index}]",
                            array[index],
                            valuesAreReferences,
                            expectedCarrier);
                    }
                    break;

                case JsonValue value when valuesAreReferences && ReadString(value) is { } reference:
                    AddCompanionReference(
                        reference,
                        "mapValue",
                        filePath,
                        jsonPath,
                        expectedCarrier);
                    break;
            }
        }

        private void AddCompanionReference(
            string reference,
            string propertyName,
            string filePath,
            string jsonPath,
            MortalItemCarrierCoordinate? expectedCarrier = null)
        {
            var companionReference = new MortalItemCompanionReference(
                reference,
                propertyName,
                filePath,
                jsonPath,
                expectedCarrier);
            AddIndex(ByCompanionReference, reference, companionReference);
            _ambiguity.Observe("companionReference", reference, jsonPath);
        }

        private void ScanInlineCompanionSurface(
            string filePath,
            string jsonPath,
            JsonNode? node,
            MortalItemCarrierCoordinate? expectedCarrier)
        {
            if (node == null)
                return;

            ScanCompanionNode(
                filePath,
                jsonPath,
                node,
                valuesAreReferences: true,
                expectedCarrier);
        }

        private void AddUniqueIdentity(
            Dictionary<string, string> firstPathByIdentity,
            string identity,
            string currentPath,
            string identityKind,
            string issueCode)
        {
            if (firstPathByIdentity.TryAdd(identity, currentPath))
                return;

            Issues.Add(new MortalItemCarrierCatalogIssue(
                issueCode,
                currentPath,
                $"The exact {identityKind} is already used by another carrier occurrence.",
                identityKind,
                identity,
                firstPathByIdentity[identity]));
        }

        private static void AddIndex<T>(
            Dictionary<string, List<T>> index,
            string key,
            T value)
        {
            if (!index.TryGetValue(key, out var entries))
            {
                entries = new List<T>();
                index.Add(key, entries);
            }

            entries.Add(value);
        }
    }

    private sealed class IdentityAmbiguityTracker
    {
        private readonly Dictionary<string, FirstIdentity> _firstByConfusable = new(StringComparer.Ordinal);
        private readonly HashSet<string> _reportedPairs = new(StringComparer.Ordinal);
        private readonly List<MortalItemCarrierCatalogIssue> _issues;

        internal IdentityAmbiguityTracker(List<MortalItemCarrierCatalogIssue> issues)
        {
            _issues = issues;
        }

        internal void Observe(string identityKind, string identity, string path)
        {
            var confusable = BuildConfusableKey(identity);
            var scopedKey = $"{identityKind}\u001f{confusable}";
            if (!_firstByConfusable.TryGetValue(scopedKey, out var first))
            {
                _firstByConfusable.Add(scopedKey, new FirstIdentity(identity, path));
                return;
            }

            if (string.Equals(first.Identity, identity, StringComparison.Ordinal))
                return;

            var orderedPair = string.CompareOrdinal(first.Identity, identity) <= 0
                ? $"{identityKind}\u001f{first.Identity}\u001f{identity}"
                : $"{identityKind}\u001f{identity}\u001f{first.Identity}";
            if (!_reportedPairs.Add(orderedPair))
                return;

            _issues.Add(new MortalItemCarrierCatalogIssue(
                "mortal_item_materialization_identity_ambiguity",
                path,
                $"Two {identityKind} values differ only by case, surrounding whitespace, or Unicode normalization.",
                identityKind,
                identity,
                first.Path));
        }

        private static string BuildConfusableKey(string value)
        {
            var trimmed = value.Trim();
            try
            {
                return trimmed.Normalize(NormalizationForm.FormC).ToUpper(CultureInfo.InvariantCulture);
            }
            catch (ArgumentException)
            {
                return trimmed.ToUpper(CultureInfo.InvariantCulture);
            }
        }

        private sealed record FirstIdentity(string Identity, string Path);
    }

    private static string? ReadString(JsonNode? node)
    {
        if (node is not JsonValue value ||
            !value.TryGetValue<string>(out var text) ||
            string.IsNullOrEmpty(text))
        {
            return null;
        }

        return text;
    }
}
