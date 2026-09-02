using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed record MortalItemTransferPlanningResult(
    IReadOnlyDictionary<string, JsonNode?> Roots,
    JsonObject IdentityIndexAfterImage,
    IReadOnlyList<ValidationIssue> Issues,
    string Fingerprint)
{
    internal bool IsValid => Issues.Count == 0;
}

/// <summary>
/// Applies already accepted whole-stack transfers to detached canonical roots.
/// Classification, identity minting, file IO, and writes remain outside this
/// pure transform.
/// </summary>
internal static class MortalItemTransferPlanner
{
    internal static MortalItemTransferPlanningResult Plan(
        IReadOnlyDictionary<string, JsonNode?> currentRoots,
        MortalItemIdentityParseResult identityState,
        IReadOnlyList<MortalItemAcceptedTransfer> transfers,
        IReadOnlyDictionary<string, string> transitionIdsByItemId,
        bool removeAcceptedCommands = true)
    {
        ArgumentNullException.ThrowIfNull(currentRoots);
        ArgumentNullException.ThrowIfNull(identityState);
        ArgumentNullException.ThrowIfNull(transfers);
        ArgumentNullException.ThrowIfNull(transitionIdsByItemId);

        var roots = MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(currentRoots);
        var initialIndex = MortalItemIdentityState.Parse(identityState.Root.DeepClone());
        var index = MortalItemIdentityState.Parse(initialIndex.Root.DeepClone());
        var issues = new List<ValidationIssue>();
        if (initialIndex.Issues.Count > 0)
            issues.AddRange(initialIndex.Issues);

        foreach (var transfer in transfers)
        {
            if (!transitionIdsByItemId.TryGetValue(transfer.ItemId, out var transitionId))
            {
                issues.Add(Issue(
                    MortalItemIdentityState.StatePath,
                    "mortal_item_projection_transfer_transition_missing",
                    transfer.ItemId,
                    "one snapshot-owned explicit transfer transition ID",
                    "missing"));
                break;
            }

            var error = ApplyTransfer(roots, index, transfer, transitionId);
            if (error != null)
            {
                issues.Add(Issue(
                    MortalItemIdentityState.StatePath,
                    "mortal_item_projection_transfer_invalid",
                    transfer.ItemId,
                    "the exact accepted whole-stack transfer",
                    error));
                break;
            }
        }

        if (issues.Count == 0 && removeAcceptedCommands)
        {
            try
            {
                RemoveAcceptedCommands(roots, transfers);
            }
            catch (InvalidDataException exception)
            {
                issues.Add(Issue(
                    MortalItemAcceptedTransferCatalog.NpcCommandsPath,
                    "mortal_item_projection_transfer_command_stale",
                    "accepted transfers",
                    "the exact accepted command indexes",
                    exception.Message));
            }
        }

        var normalized = MortalItemIdentityState.Parse(index.Root);
        if (issues.Count == 0 && normalized.Issues.Count > 0)
            issues.AddRange(normalized.Issues);
        if (issues.Count == 0)
        {
            var continuity = MortalItemIdentityState.ValidateAgainst(initialIndex, normalized);
            if (continuity.Count > 0)
                issues.AddRange(continuity);
        }

        if (issues.Count > 0)
        {
            return new MortalItemTransferPlanningResult(
                new Dictionary<string, JsonNode?>(StringComparer.Ordinal),
                MortalItemIdentityState.CreateEmptyRoot(),
                issues.ToArray(),
                Fingerprint(currentRoots, null, issues));
        }

        var identityIndexAfterImage = transfers.Count == 0 &&
                                      currentRoots.GetValueOrDefault(
                                          MortalItemIdentityState.StatePath) is
                                          JsonObject unchangedIndex
            ? unchangedIndex.DeepClone().AsObject()
            : normalized.Root.DeepClone().AsObject();
        roots[MortalItemIdentityState.StatePath] =
            identityIndexAfterImage.DeepClone();
        return new MortalItemTransferPlanningResult(
            MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(roots),
            identityIndexAfterImage,
            Array.Empty<ValidationIssue>(),
            Fingerprint(
                roots,
                identityIndexAfterImage,
                Array.Empty<ValidationIssue>()));
    }

    private static string? ApplyTransfer(
        IReadOnlyDictionary<string, JsonNode?> roots,
        MortalItemIdentityParseResult index,
        MortalItemAcceptedTransfer transfer,
        string transitionId)
    {
        var catalog = BuildPhysicalCatalog(roots);
        if (catalog.Issues.Count > 0)
            return catalog.Issues[0].Message;
        if (!catalog.ByItemId.TryGetValue(transfer.ItemId, out var occurrences) ||
            occurrences.Count != 1 ||
            !SameCarrier(occurrences[0].Carrier, transfer.SourceCarrier))
        {
            return "The exact accepted source carrier no longer contains the item once.";
        }
        if (!index.EntriesByItemId.TryGetValue(transfer.ItemId, out var entry) ||
            !string.Equals(ReadExactString(entry["state"]), "active", StringComparison.Ordinal) ||
            !CarrierNodeEquals(entry["currentCarrier"], transfer.SourceCarrier))
        {
            return "The item identity is not active at the accepted source carrier.";
        }

        var source = ResolveCarrierArray(roots, transfer.SourceCarrier, false, out var error);
        if (source == null)
            return error;
        var matches = source.OfType<JsonObject>().Where(item => string.Equals(
            ReadExactString(item["itemId"]),
            transfer.ItemId,
            StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
            return "The accepted source item is missing or ambiguous.";
        var item = matches[0];
        if (!TryReadPositiveInt(item["count"], out var quantity) ||
            quantity != transfer.Quantity)
        {
            return "The accepted transfer quantity differs from the source stack.";
        }

        var destination = ResolveCarrierArray(
            roots,
            transfer.DestinationCarrier,
            true,
            out error);
        if (destination == null)
            return error;
        if (!ValidateDestinationContainerPath(
                destination,
                transfer.ItemId,
                transfer.DestinationCarrier.ContainerPath,
                out error))
        {
            return error;
        }

        var immutableEnvelope = item[MortalItemMaterializationContract.EnvelopeProperty]
            ?.DeepClone();
        var immutableReceipt = item[MortalItemMaterializationContract.ReceiptProperty]
            ?.DeepClone();
        ClearInlineEquipmentReference(roots, transfer.SourceCarrier, transfer.ItemId);
        source.Remove(item);
        item["contentsPath"] = transfer.DestinationCarrier.ContainerPath.Count == 0
            ? null
            : new JsonArray(transfer.DestinationCarrier.ContainerPath
                .Select(static value => (JsonNode?)JsonValue.Create(value)).ToArray());
        MortalItemLocalActionPolicy.NormalizePlacementForDestination(
            item,
            transfer.DestinationCarrier);
        destination.Add(item);

        entry["currentCarrier"] = CreateCarrierNode(transfer.DestinationCarrier);
        MortalItemIdentityState.AppendTransition(
            entry,
            MortalItemIdentityState.CreateTransition(
                "transfer",
                transfer.Turn,
                new[] { transfer.ItemId },
                CreateCarrierNode(transfer.SourceCarrier),
                CreateCarrierNode(transfer.DestinationCarrier),
                transfer.Quantity,
                transfer.Quantity,
                transfer.AuthorityKind,
                transfer.AuthorityId,
                transitionId));

        if (!JsonNode.DeepEquals(
                immutableEnvelope,
                item[MortalItemMaterializationContract.EnvelopeProperty]) ||
            !JsonNode.DeepEquals(
                immutableReceipt,
                item[MortalItemMaterializationContract.ReceiptProperty]))
        {
            return "The transfer changed immutable materialization evidence.";
        }

        var after = BuildPhysicalCatalog(roots);
        if (after.Issues.Count > 0 ||
            !after.ByItemId.TryGetValue(transfer.ItemId, out var afterOccurrences) ||
            afterOccurrences.Count != 1 ||
            !SameCarrier(afterOccurrences[0].Carrier, transfer.DestinationCarrier))
        {
            return after.Issues.FirstOrDefault()?.Message ??
                   "The transfer did not finish at one exact destination carrier.";
        }
        return null;
    }

    private static MortalItemCarrierCatalog BuildPhysicalCatalog(
        IReadOnlyDictionary<string, JsonNode?> roots)
    {
        var companions = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var path in new[]
                 {
                     "game_state/inventory/item_bonds.json",
                     "game_state/inventory/item_text_updates.json",
                     "game_state/inventory/recipes.json",
                     "game_state/npcs/item_journals.json",
                     "game_state/quests/quest_history.json"
                 })
        {
            if (roots.GetValueOrDefault(path) is JsonObject root)
                companions.Add(path, root);
        }
        return MortalItemCarrierCatalog.Build(new MortalItemCarrierCatalogInput(
            roots.GetValueOrDefault(InventoryEquipmentService.ItemsPath) as JsonObject,
            roots.GetValueOrDefault(NpcCoreChangesContract.NpcCorePath) as JsonObject,
            null,
            roots.GetValueOrDefault(StorageTransportMoveService.CurrentLocationPath) as JsonObject,
            WrapVehiclesForCatalog(
                roots.GetValueOrDefault(StorageTransportMoveService.VehiclesPath)),
            companions,
            roots.GetValueOrDefault(MortalLocationStorageContentsState.StatePath) as JsonObject));
    }

    private static JsonObject? WrapVehiclesForCatalog(JsonNode? node) => node switch
    {
        JsonObject root => root,
        JsonArray vehicles => new JsonObject { ["vehicles"] = vehicles.DeepClone() },
        _ => null
    };

    private static JsonArray? ResolveCarrierArray(
        IReadOnlyDictionary<string, JsonNode?> roots,
        MortalItemCarrierCoordinate carrier,
        bool createIfMissing,
        out string? error)
    {
        error = null;
        switch (carrier.Kind)
        {
            case "player_inventory":
                return ResolvePropertyArray(
                    roots.GetValueOrDefault(InventoryEquipmentService.ItemsPath) as JsonObject,
                    "items",
                    createIfMissing,
                    "Player inventory is absent.",
                    out error);
            case "npc_inventory":
            {
                var npc = ResolveNpc(
                    roots.GetValueOrDefault(NpcCoreChangesContract.NpcCorePath) as JsonObject,
                    carrier.OwnerId,
                    out error);
                return npc == null
                    ? null
                    : ResolvePropertyArray(
                        npc,
                        "inventory",
                        createIfMissing,
                        "NPC inventory is absent.",
                        out error);
            }
            case "location_storage":
            {
                var root = roots.GetValueOrDefault(
                    StorageTransportMoveService.CurrentLocationPath) as JsonObject;
                var location = root?["currentLocationData"] as JsonObject ?? root;
                if (location == null ||
                    !MatchesAnyIdentity(location, carrier.OwnerId, "locationId", "id") ||
                    location["locationStorages"] is not JsonArray storages)
                {
                    error = "The exact current location storage root is absent.";
                    return null;
                }
                var matches = storages.OfType<JsonObject>().Where(storage =>
                    MatchesAnyIdentity(storage, carrier.ContainerId ?? string.Empty, "storageId"))
                    .ToArray();
                if (matches.Length != 1)
                {
                    error = "The exact current location storage is missing or ambiguous.";
                    return null;
                }
                return ResolvePropertyArray(
                    matches[0],
                    "contents",
                    createIfMissing,
                    "Location storage contents are absent.",
                    out error);
            }
            case "vehicle_inventory":
            {
                var vehicleRoot = roots.GetValueOrDefault(
                    StorageTransportMoveService.VehiclesPath);
                var vehicles = vehicleRoot switch
                {
                    JsonArray legacy => legacy,
                    JsonObject wrapper => wrapper["vehicles"] as JsonArray,
                    _ => null
                };
                if (vehicles == null)
                {
                    error = "Vehicle inventory authority is absent.";
                    return null;
                }
                var matches = vehicles.OfType<JsonObject>().Where(vehicle =>
                    MatchesAnyIdentity(vehicle, carrier.OwnerId, "vehicleId", "id"))
                    .ToArray();
                if (matches.Length != 1)
                {
                    error = "The exact vehicle is missing or ambiguous.";
                    return null;
                }
                return ResolvePropertyArray(
                    matches[0],
                    "inventory",
                    createIfMissing,
                    "Vehicle inventory is absent.",
                    out error);
            }
            default:
                error = "The accepted carrier kind is unsupported.";
                return null;
        }
    }

    private static JsonArray? ResolvePropertyArray(
        JsonObject? owner,
        string property,
        bool createIfMissing,
        string missingError,
        out string? error)
    {
        error = null;
        if (owner?[property] is JsonArray existing)
            return existing;
        if (owner == null || !createIfMissing)
        {
            error = missingError;
            return null;
        }
        var created = new JsonArray();
        owner[property] = created;
        return created;
    }

    private static JsonObject? ResolveNpc(
        JsonObject? root,
        string npcId,
        out string? error)
    {
        error = null;
        var matches = new List<JsonObject>();
        foreach (var section in new[] { "UpdateNPCs", "NPCsInScene" })
        {
            if (root?[section] is JsonArray npcs)
                matches.AddRange(npcs.OfType<JsonObject>().Where(npc =>
                    MatchesAnyIdentity(npc, npcId, "NPCId", "npcId", "id", "initialId")));
        }
        if (matches.Count == 1)
            return matches[0];
        error = "The exact NPC is missing or ambiguous.";
        return null;
    }

    private static bool ValidateDestinationContainerPath(
        JsonArray destination,
        string movingItemId,
        IReadOnlyList<string> path,
        out string? error)
    {
        error = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parentId in path)
        {
            if (!seen.Add(parentId) || string.Equals(parentId, movingItemId, StringComparison.Ordinal))
            {
                error = "The destination container path is cyclic.";
                return false;
            }
            var matches = destination.OfType<JsonObject>().Where(item => string.Equals(
                ReadExactString(item["itemId"]), parentId, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1 || !ReadBool(matches[0]["isContainer"]))
            {
                error = "Every destination path element must be one active container.";
                return false;
            }
        }
        return true;
    }

    private static void ClearInlineEquipmentReference(
        IReadOnlyDictionary<string, JsonNode?> roots,
        MortalItemCarrierCoordinate source,
        string itemId)
    {
        JsonObject? owner = source.Kind switch
        {
            "player_inventory" =>
                roots.GetValueOrDefault(InventoryEquipmentService.ItemsPath) as JsonObject,
            "npc_inventory" => ResolveNpc(
                roots.GetValueOrDefault(NpcCoreChangesContract.NpcCorePath) as JsonObject,
                source.OwnerId,
                out _),
            _ => null
        };
        if (owner == null)
            return;
        RemoveExactEquipmentReference(owner["equipment"], itemId);
        RemoveExactEquipmentReference(owner["equippedItems"], itemId);
    }

    private static void RemoveExactEquipmentReference(JsonNode? node, string itemId)
    {
        if (node is JsonArray array)
        {
            for (var index = array.Count - 1; index >= 0; index--)
            {
                if (string.Equals(ReadExactString(array[index]), itemId, StringComparison.Ordinal))
                    array.RemoveAt(index);
                else
                    RemoveExactEquipmentReference(array[index], itemId);
            }
            return;
        }
        if (node is not JsonObject obj)
            return;
        foreach (var pair in obj.ToArray())
        {
            if (string.Equals(ReadExactString(pair.Value), itemId, StringComparison.Ordinal))
                obj[pair.Key] = null;
            else
                RemoveExactEquipmentReference(pair.Value, itemId);
        }
    }

    private static void RemoveAcceptedCommands(
        IReadOnlyDictionary<string, JsonNode?> roots,
        IReadOnlyList<MortalItemAcceptedTransfer> transfers)
    {
        RemoveCommandIndexes(
            roots.GetValueOrDefault(InventoryEquipmentService.ItemsPath) as JsonObject,
            "UpdateInventory",
            transfers.Where(static transfer =>
                    transfer.DestinationSurface == MortalItemTransferCommandSurface.PlayerUpdate)
                .Select(static transfer => transfer.DestinationIndex));
        var npcCommands = roots.GetValueOrDefault(
            MortalItemAcceptedTransferCatalog.NpcCommandsPath) as JsonObject;
        RemoveCommandIndexes(
            npcCommands,
            "NPCInventoryAdds",
            transfers.Where(static transfer =>
                    transfer.DestinationSurface == MortalItemTransferCommandSurface.NpcAdd)
                .Select(static transfer => transfer.DestinationIndex));
        RemoveCommandIndexes(
            npcCommands,
            "NPCInventoryRemovals",
            transfers.Where(static transfer =>
                    transfer.RemovalSurface == MortalItemTransferCommandSurface.NpcRemoval)
                .Select(static transfer => transfer.RemovalIndex));
        RemoveCommandIndexes(
            roots.GetValueOrDefault(
                MortalItemAcceptedTransferCatalog.PlayerRemovalPath) as JsonObject,
            "removeInventoryItems",
            transfers.Where(static transfer =>
                    transfer.RemovalSurface == MortalItemTransferCommandSurface.PlayerRemoval)
                .Select(static transfer => transfer.RemovalIndex));
    }

    private static void RemoveCommandIndexes(
        JsonObject? root,
        string property,
        IEnumerable<int> indexes)
    {
        var ordered = indexes.Distinct().OrderByDescending(static index => index).ToArray();
        if (ordered.Length == 0)
            return;
        if (root?[property] is not JsonArray commands)
            throw new InvalidDataException($"Accepted transfer command surface '{property}' is absent.");
        foreach (var index in ordered)
        {
            if (index < 0 || index >= commands.Count)
                throw new InvalidDataException($"Accepted transfer command index {index} is stale.");
            commands.RemoveAt(index);
        }
        if (commands.Count == 0)
            root.Remove(property);
    }

    private static JsonObject CreateCarrierNode(MortalItemCarrierCoordinate carrier) => new()
    {
        ["kind"] = carrier.Kind,
        ["ownerId"] = carrier.OwnerId,
        ["containerId"] = carrier.ContainerId,
        ["containerPath"] = new JsonArray(carrier.ContainerPath
            .Select(static value => (JsonNode?)JsonValue.Create(value)).ToArray())
    };

    private static bool CarrierNodeEquals(JsonNode? node, MortalItemCarrierCoordinate carrier) =>
        node is JsonObject obj &&
        string.Equals(ReadExactString(obj["kind"]), carrier.Kind, StringComparison.Ordinal) &&
        string.Equals(ReadExactString(obj["ownerId"]), carrier.OwnerId, StringComparison.Ordinal) &&
        string.Equals(ReadExactString(obj["containerId"]), carrier.ContainerId, StringComparison.Ordinal) &&
        ReadStringArray(obj["containerPath"]).SequenceEqual(carrier.ContainerPath, StringComparer.Ordinal);

    private static bool SameCarrier(
        MortalItemCarrierCoordinate left,
        MortalItemCarrierCoordinate right) =>
        string.Equals(left.Kind, right.Kind, StringComparison.Ordinal) &&
        string.Equals(left.OwnerId, right.OwnerId, StringComparison.Ordinal) &&
        string.Equals(left.ContainerId, right.ContainerId, StringComparison.Ordinal) &&
        left.ContainerPath.SequenceEqual(right.ContainerPath, StringComparer.Ordinal);

    private static bool MatchesAnyIdentity(
        JsonObject value,
        string expected,
        params string[] properties) => properties.Any(property =>
        string.Equals(ReadExactString(value[property]), expected, StringComparison.Ordinal));

    private static string? ReadExactString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) &&
        !string.IsNullOrEmpty(text) &&
        string.Equals(text, text.Trim(), StringComparison.Ordinal)
            ? text
            : null;

    private static IReadOnlyList<string> ReadStringArray(JsonNode? node) =>
        node is JsonArray array
            ? array.Select(ReadExactString).Where(static value => value != null)
                .Select(static value => value!).ToArray()
            : Array.Empty<string>();

    private static bool TryReadPositiveInt(JsonNode? node, out int value)
    {
        value = 0;
        return node is JsonValue scalar && scalar.TryGetValue<int>(out value) && value > 0;
    }

    private static bool ReadBool(JsonNode? node) =>
        node is JsonValue scalar && scalar.TryGetValue<bool>(out var value) && value;

    private static ValidationIssue Issue(
        string path,
        string code,
        string actor,
        string expected,
        string actual) => new(
        path,
        IssueSeverity.Error,
        "Accepted Mortal item transfer projection failed.",
        code: code,
        actor: "mortal_item:" + actor,
        section: "MortalItemMaterialization",
        expected: expected,
        actual: actual,
        repairHint: "Reject the accepted projection and rebuild it from raw-validation authority.",
        repairTargetFiles: new[] { path });

    private static string Fingerprint(
        IReadOnlyDictionary<string, JsonNode?> roots,
        JsonObject? index,
        IReadOnlyList<ValidationIssue> issues)
    {
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_item.transfer_projection",
            "1"
        };
        foreach (var pair in roots.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            fields.Add(pair.Key);
            fields.Add(pair.Value == null
                ? "missing"
                : WoundAcceptedTurnFingerprintWriter.CanonicalJson(pair.Value));
        }
        fields.Add(index == null
            ? "no_index"
            : WoundAcceptedTurnFingerprintWriter.CanonicalJson(index));
        foreach (var issue in issues)
        {
            fields.Add(issue.Code);
            fields.Add(issue.FilePath);
            fields.Add(issue.Actual);
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }
}
