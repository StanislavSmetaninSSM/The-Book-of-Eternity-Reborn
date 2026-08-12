using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal enum MortalItemTransferCommandSurface
{
    PlayerUpdate,
    NpcAdd,
    PlayerRemoval,
    NpcRemoval
}

internal sealed record MortalItemAcceptedTransfer(
    string ItemId,
    MortalItemCarrierCoordinate SourceCarrier,
    MortalItemCarrierCoordinate DestinationCarrier,
    int Quantity,
    int Turn,
    string AuthorityKind,
    string AuthorityId,
    MortalItemTransferCommandSurface DestinationSurface,
    int DestinationIndex,
    MortalItemTransferCommandSurface RemovalSurface,
    int RemovalIndex);

internal sealed record MortalItemAcceptedTransferCatalog(
    IReadOnlyList<MortalItemAcceptedTransfer> Transfers,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal const string PlayerRemovalPath = "game_state/inventory/item_removals.json";
    internal const string NpcCommandsPath = "game_state/npcs/npc_inventory.json";

    internal static async Task<MortalItemAcceptedTransferCatalog> BuildAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        MortalItemCarrierCatalog previous,
        MortalItemCarrierCatalog current,
        int acceptedTurn)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        var player = await ReadObjectAsync(
            fs,
            writeLease,
            InventoryEquipmentService.ItemsPath);
        var npcCommands = await ReadObjectAsync(fs, writeLease, NpcCommandsPath);
        var playerRemovals = await ReadObjectAsync(fs, writeLease, PlayerRemovalPath);
        var destinations = CollectDestinations(player, npcCommands);
        var removals = CollectRemovals(playerRemovals, npcCommands);
        var issues = new List<ValidationIssue>();
        var transfers = new List<MortalItemAcceptedTransfer>();

        DetectRemoveAndRecreate(previous, destinations, removals, issues);

        foreach (var duplicate in destinations
                     .Where(candidate => candidate.ItemId != null)
                     .GroupBy(candidate => candidate.ItemId!, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            issues.Add(Issue(
                duplicate.First().Path,
                "mortal_item_transfer_duplicate_destination",
                duplicate.Key,
                "one exact destination add/update command",
                $"{duplicate.Count()} destination commands",
                duplicate.Select(candidate => candidate.FilePath).ToArray()));
        }

        foreach (var destination in destinations)
        {
            if (destination.ItemId == null || destination.IsRawCreation)
                continue;
            if (!previous.ByItemId.TryGetValue(destination.ItemId, out var previousOccurrences) ||
                previousOccurrences.Count != 1)
            {
                issues.Add(Issue(
                    destination.Path,
                    "mortal_item_transfer_unknown_existing_item",
                    destination.ItemId,
                    "one exact pre-turn item identity",
                    "missing or ambiguous",
                    destination.FilePath));
                continue;
            }

            var source = previousOccurrences[0];
            if (!ImmutableEvidenceEquals(
                    source.Item[MortalItemMaterializationContract.EnvelopeProperty],
                    destination.Item[MortalItemMaterializationContract.EnvelopeProperty]))
            {
                issues.Add(Issue(
                    destination.Path,
                    "mortal_item_materialization_immutable_envelope_rewrite",
                    destination.ItemId,
                    "exact pre-turn materialization envelope",
                    "changed or missing envelope",
                    new[] { source.FilePath, destination.FilePath }));
                continue;
            }
            if (!ImmutableEvidenceEquals(
                    source.Item[MortalItemMaterializationContract.ReceiptProperty],
                    destination.Item[MortalItemMaterializationContract.ReceiptProperty]))
            {
                issues.Add(Issue(
                    destination.Path,
                    "mortal_item_materialization_immutable_receipt_rewrite",
                    destination.ItemId,
                    "exact pre-turn materialization receipt",
                    "changed or missing receipt",
                    new[] { source.FilePath, destination.FilePath }));
                continue;
            }
            if (SameCarrier(source.Carrier, destination.Carrier))
                continue;

            var matchingRemovals = removals.Where(removal =>
                    string.Equals(removal.ItemId, destination.ItemId, StringComparison.Ordinal) &&
                    SameRemovalRoot(removal.Carrier, source.Carrier))
                .ToArray();
            if (matchingRemovals.Length != 1)
            {
                issues.Add(Issue(
                    destination.Path,
                    "mortal_item_transfer_missing_removal",
                    destination.ItemId,
                    "one exact removal from the pre-turn source carrier",
                    $"{matchingRemovals.Length} matching removals",
                    new[] { destination.FilePath, SourceRemovalPath(source.Carrier) }));
                continue;
            }

            if (!current.ByItemId.TryGetValue(destination.ItemId, out var currentOccurrences) ||
                currentOccurrences.Count != 1 ||
                !SameCarrier(currentOccurrences[0].Carrier, source.Carrier))
            {
                issues.Add(Issue(
                    destination.Path,
                    "mortal_item_transfer_source_changed_before_normalization",
                    destination.ItemId,
                    "one unchanged physical source carrier before client normalization",
                    currentOccurrences == null
                        ? "missing"
                        : $"{currentOccurrences.Count} current carrier occurrences",
                    new[] { source.FilePath, destination.FilePath }));
                continue;
            }

            if (!TransferPayloadEquals(source.Item, destination.Item) ||
                !DestinationPathMatchesPayload(destination))
            {
                issues.Add(Issue(
                    destination.Path,
                    "mortal_item_transfer_payload_mismatch",
                    destination.ItemId,
                    "the exact pre-turn item with only destination contentsPath changed",
                    "semantic, identity, receipt, envelope, count, or placement mismatch",
                    new[] { source.FilePath, destination.FilePath }));
                continue;
            }

            using var document = JsonDocument.Parse(destination.Item.ToJsonString());
            var contractIssues = MortalItemMaterializationContract.Validate(
                document.RootElement,
                destination.Path,
                MortalItemMaterializationPhase.CanonicalPostSeal);
            if (contractIssues.Count > 0)
            {
                issues.Add(contractIssues[0]);
                continue;
            }

            if (!TryReadPositiveInt(destination.Item["count"], out var quantity))
            {
                issues.Add(Issue(
                    destination.Path,
                    "mortal_item_transfer_payload_mismatch",
                    destination.ItemId,
                    "positive integer count",
                    destination.Item["count"]?.ToJsonString() ?? "missing",
                    destination.FilePath));
                continue;
            }

            var removal = matchingRemovals[0];
            transfers.Add(new MortalItemAcceptedTransfer(
                destination.ItemId,
                source.Carrier,
                destination.Carrier,
                quantity,
                acceptedTurn,
                "gm_inventory_transfer",
                $"gm_inventory_transfer:{acceptedTurn}:{destination.ItemId}",
                destination.Surface,
                destination.Index,
                removal.Surface,
                removal.Index));
        }

        return new MortalItemAcceptedTransferCatalog(transfers, issues);
    }

    private static void DetectRemoveAndRecreate(
        MortalItemCarrierCatalog previous,
        IReadOnlyList<DestinationCandidate> destinations,
        IReadOnlyList<RemovalCandidate> removals,
        List<ValidationIssue> issues)
    {
        foreach (var destination in destinations.Where(candidate => candidate.IsRawCreation))
        {
            foreach (var removal in removals)
            {
                if (!previous.ByItemId.TryGetValue(removal.ItemId, out var occurrences) ||
                    occurrences.Count != 1 ||
                    !SameRemovalRoot(removal.Carrier, occurrences[0].Carrier) ||
                    !TransferSemanticProjectionEquals(occurrences[0].Item, destination.Item))
                {
                    continue;
                }

                issues.Add(Issue(
                    destination.Path,
                    "mortal_item_transfer_recreation_forbidden",
                    removal.ItemId,
                    "the exact existing itemId and immutable receipt/envelope in a transfer payload",
                    "null-ID recreation matching a removed existing item",
                    new[] { destination.FilePath, removal.FilePath }));
                break;
            }
        }
    }

    private static IReadOnlyList<DestinationCandidate> CollectDestinations(
        JsonObject? player,
        JsonObject? npcCommands)
    {
        var result = new List<DestinationCandidate>();
        if (player?["UpdateInventory"] is JsonArray playerUpdates)
        {
            for (var index = 0; index < playerUpdates.Count; index++)
            {
                if (playerUpdates[index] is not JsonObject item)
                    continue;
                var itemId = ReadExactString(item["itemId"]);
                result.Add(new DestinationCandidate(
                    itemId,
                    IsRawCreation(item),
                    new MortalItemCarrierCoordinate(
                        "player_inventory",
                        "player",
                        null,
                        ReadContentsPath(item)),
                    item,
                    MortalItemTransferCommandSurface.PlayerUpdate,
                    index,
                    InventoryEquipmentService.ItemsPath,
                    $"{InventoryEquipmentService.ItemsPath}.UpdateInventory[{index}]"));
            }
        }

        if (npcCommands?["NPCInventoryAdds"] is JsonArray npcAdds)
        {
            for (var index = 0; index < npcAdds.Count; index++)
            {
                if (npcAdds[index] is not JsonObject command ||
                    command["item"] is not JsonObject item)
                {
                    continue;
                }
                var npcId = ReadNpcId(command);
                var destinationContainerId = ReadExactString(command["destinationContainerId"]);
                var path = destinationContainerId == null
                    ? Array.Empty<string>()
                    : new[] { destinationContainerId };
                result.Add(new DestinationCandidate(
                    ReadExactString(item["itemId"]),
                    IsRawCreation(item),
                    new MortalItemCarrierCoordinate(
                        "npc_inventory",
                        npcId ?? string.Empty,
                        null,
                        path),
                    item,
                    MortalItemTransferCommandSurface.NpcAdd,
                    index,
                    NpcCommandsPath,
                    $"{NpcCommandsPath}.NPCInventoryAdds[{index}].item"));
            }
        }
        return result;
    }

    private static IReadOnlyList<RemovalCandidate> CollectRemovals(
        JsonObject? playerRemovals,
        JsonObject? npcCommands)
    {
        var result = new List<RemovalCandidate>();
        if (playerRemovals?["removeInventoryItems"] is JsonArray player)
        {
            for (var index = 0; index < player.Count; index++)
            {
                if (player[index] is not JsonObject command ||
                    ReadExactString(command["removedItemId"]) is not { } itemId)
                {
                    continue;
                }
                result.Add(new RemovalCandidate(
                    itemId,
                    new MortalItemCarrierCoordinate(
                        "player_inventory",
                        "player",
                        null,
                        ReadNullablePath(command["currentContentsPath"])),
                    MortalItemTransferCommandSurface.PlayerRemoval,
                    index,
                    PlayerRemovalPath));
            }
        }

        if (npcCommands?["NPCInventoryRemovals"] is JsonArray npc)
        {
            for (var index = 0; index < npc.Count; index++)
            {
                if (npc[index] is not JsonObject command ||
                    ReadExactString(command["itemId"]) is not { } itemId ||
                    ReadNpcId(command) is not { } npcId)
                {
                    continue;
                }
                result.Add(new RemovalCandidate(
                    itemId,
                    new MortalItemCarrierCoordinate(
                        "npc_inventory",
                        npcId,
                        null,
                        Array.Empty<string>()),
                    MortalItemTransferCommandSurface.NpcRemoval,
                    index,
                    NpcCommandsPath));
            }
        }
        return result;
    }

    private static bool DestinationPathMatchesPayload(DestinationCandidate destination) =>
        ReadContentsPath(destination.Item).SequenceEqual(
            destination.Carrier.ContainerPath,
            StringComparer.Ordinal);

    private static bool TransferPayloadEquals(JsonObject previous, JsonObject current)
    {
        var previousCopy = previous.DeepClone().AsObject();
        var currentCopy = current.DeepClone().AsObject();
        previousCopy["contentsPath"] = null;
        currentCopy["contentsPath"] = null;
        return MortalItemMaterializationContract.ImmutableEvidenceEquals(
            previousCopy,
            currentCopy);
    }

    private static bool ImmutableEvidenceEquals(JsonNode? previous, JsonNode? current) =>
        previous != null &&
        current != null &&
        MortalItemMaterializationContract.ImmutableEvidenceEquals(previous, current);

    private static bool TransferSemanticProjectionEquals(JsonObject previous, JsonObject raw)
    {
        var previousCopy = previous.DeepClone().AsObject();
        var rawCopy = raw.DeepClone().AsObject();
        foreach (var property in new[]
                 {
                     "itemId", "id", "initialId", "existedId", "creationRef",
                     MortalItemMaterializationContract.EnvelopeProperty,
                     MortalItemMaterializationContract.ReceiptProperty,
                     "contentsPath"
                 })
        {
            previousCopy.Remove(property);
            rawCopy.Remove(property);
        }
        return MortalItemMaterializationContract.ImmutableEvidenceEquals(
            previousCopy,
            rawCopy);
    }

    private static bool IsRawCreation(JsonObject item) =>
        ReadExactString(item["creationRef"]) != null;

    private static bool SameCarrier(
        MortalItemCarrierCoordinate left,
        MortalItemCarrierCoordinate right) =>
        SameRemovalRoot(left, right) &&
        left.ContainerPath.SequenceEqual(right.ContainerPath, StringComparer.Ordinal);

    private static bool SameRemovalRoot(
        MortalItemCarrierCoordinate removal,
        MortalItemCarrierCoordinate source) =>
        string.Equals(removal.Kind, source.Kind, StringComparison.Ordinal) &&
        string.Equals(removal.OwnerId, source.OwnerId, StringComparison.Ordinal) &&
        string.Equals(removal.ContainerId, source.ContainerId, StringComparison.Ordinal) &&
        (removal.Kind == "npc_inventory" ||
         removal.ContainerPath.SequenceEqual(source.ContainerPath, StringComparer.Ordinal));

    private static string SourceRemovalPath(MortalItemCarrierCoordinate source) =>
        source.Kind == "player_inventory" ? PlayerRemovalPath : NpcCommandsPath;

    private static IReadOnlyList<string> ReadContentsPath(JsonObject item) =>
        ReadNullablePath(item["contentsPath"]);

    private static IReadOnlyList<string> ReadNullablePath(JsonNode? node)
    {
        if (node == null)
            return Array.Empty<string>();
        if (node is not JsonArray array)
            return new[] { string.Empty };
        var result = new List<string>(array.Count);
        foreach (var child in array)
            result.Add(ReadExactString(child) ?? string.Empty);
        return result;
    }

    private static string? ReadNpcId(JsonObject command) =>
        ReadExactString(command["NPCId"]) ??
        ReadExactString(command["npcId"]) ??
        ReadExactString(command["id"]) ??
        ReadExactString(command["initialId"]);

    private static string? ReadExactString(JsonNode? node)
    {
        if (node is not JsonValue value || !value.TryGetValue<string>(out var text) ||
            string.IsNullOrWhiteSpace(text) ||
            !string.Equals(text, text.Trim(), StringComparison.Ordinal))
        {
            return null;
        }
        return text;
    }

    private static bool TryReadPositiveInt(JsonNode? node, out int value)
    {
        value = 0;
        return node is JsonValue jsonValue &&
               jsonValue.TryGetValue(out value) &&
               value > 0;
    }

    private static async Task<JsonObject?> ReadObjectAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string path)
    {
        var json = writeLease == null
            ? await fs.ReadFileAsync(path)
            : await fs.ReadFileAsync(writeLease, path);
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ValidationIssue Issue(
        string path,
        string code,
        string itemId,
        string expected,
        string actual,
        params string[] targets) =>
        new(
            path,
            IssueSeverity.Error,
            "Accepted Mortal item transfer commands do not describe one exact identity-preserving move.",
            code: code,
            actor: $"mortal_item:existing:{itemId}",
            section: "MortalItemMaterialization",
            expected: expected,
            actual: actual,
            repairHint: "Pair one exact-ID source removal with one unchanged full-item destination payload; preserve itemId, envelope, receipt, count, and companions.",
            repairTargetFiles: targets.Distinct(StringComparer.Ordinal).ToArray());

    private sealed record DestinationCandidate(
        string? ItemId,
        bool IsRawCreation,
        MortalItemCarrierCoordinate Carrier,
        JsonObject Item,
        MortalItemTransferCommandSurface Surface,
        int Index,
        string FilePath,
        string Path);

    private sealed record RemovalCandidate(
        string ItemId,
        MortalItemCarrierCoordinate Carrier,
        MortalItemTransferCommandSurface Surface,
        int Index,
        string FilePath);
}
