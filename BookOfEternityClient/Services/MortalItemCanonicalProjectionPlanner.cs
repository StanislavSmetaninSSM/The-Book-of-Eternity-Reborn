using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed record MortalItemCanonicalProjectionInput(
    int Turn,
    MortalItemAcceptedTurnNormalizationSnapshot Snapshot,
    MortalItemRouteAuthorityCatalog RouteCatalog,
    IReadOnlyDictionary<string, JsonNode?> CurrentRoots,
    IReadOnlyDictionary<string, JsonNode?> BackupRoots,
    MortalItemIdentityParseResult IdentityState);

internal sealed record MortalItemCanonicalProjectionResult(
    IReadOnlyDictionary<string, JsonNode?> ItemPhaseAfterImages,
    JsonObject IdentityIndexAfterImage,
    IReadOnlyList<ValidationIssue> Issues,
    string Fingerprint)
{
    internal bool IsValid => Issues.Count == 0;
}

/// <summary>
/// Deterministically reproduces the accepted Mortal item phase from frozen
/// validation authority. This type performs no reads, writes, catalog
/// classification, or identity minting.
/// </summary>
internal static class MortalItemCanonicalProjectionPlanner
{
    internal static IReadOnlyList<string> ProjectionRootPaths { get; } =
        Array.AsReadOnly(new[]
        {
            InventoryEquipmentService.ItemsPath,
            NpcCoreChangesContract.NpcCorePath,
            MortalItemAcceptedTransferCatalog.NpcCommandsPath,
            MortalItemAcceptedTransferCatalog.PlayerRemovalPath,
            StorageTransportMoveService.CurrentLocationPath,
            MortalLocationStorageContentsState.StatePath,
            StorageTransportMoveService.VehiclesPath,
            MortalItemIdentityState.StatePath,
            "game_state/quests/quest_history.json",
            "game_state/inventory/item_bonds.json",
            "game_state/inventory/item_text_updates.json",
            "game_state/inventory/recipes.json",
            "game_state/npcs/item_journals.json"
        });

    internal static MortalItemCanonicalProjectionResult Project(
        MortalItemCanonicalProjectionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var issues = ValidateInput(input);
        if (issues.Count > 0)
            return Invalid(input, issues);

        var transferIds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var transfer in input.Snapshot.Transfers)
        {
            if (!input.Snapshot.TryGetTransferTransitionId(
                    transfer.ItemId,
                    out var transitionId))
            {
                issues.Add(Issue(
                    MortalItemIdentityState.StatePath,
                    "mortal_item_projection_transfer_transition_missing",
                    transfer.ItemId,
                    "one snapshot-owned transfer transition ID",
                    "missing"));
                return Invalid(input, issues);
            }
            transferIds.Add(transfer.ItemId, transitionId);
        }

        var transferred = MortalItemTransferPlanner.Plan(
            input.CurrentRoots,
            input.IdentityState,
            input.Snapshot.Transfers,
            transferIds);
        if (!transferred.IsValid)
            return Invalid(input, transferred.Issues);

        try
        {
            return ApplyCreations(input, transferred.Roots, transferred.IdentityIndexAfterImage);
        }
        catch (Exception exception) when (exception is InvalidDataException or
                                           InvalidOperationException or
                                           ArgumentException)
        {
            issues.Add(Issue(
                MortalItemIdentityState.StatePath,
                "mortal_item_projection_creation_invalid",
                "accepted creations",
                "the exact frozen creation authority",
                exception.Message));
            return Invalid(input, issues);
        }
    }

    private static MortalItemCanonicalProjectionResult ApplyCreations(
        MortalItemCanonicalProjectionInput input,
        IReadOnlyDictionary<string, JsonNode?> transferredRoots,
        JsonObject transferredIndex)
    {
        var roots = MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(transferredRoots);
        var playerRoot = roots[InventoryEquipmentService.ItemsPath] as JsonObject;
        var npcRoot = roots[NpcCoreChangesContract.NpcCorePath] as JsonObject;
        var npcCommandsRoot = roots[MortalItemAcceptedTransferCatalog.NpcCommandsPath]
            as JsonObject;
        var locationRoot = roots[StorageTransportMoveService.CurrentLocationPath]
            as JsonObject;
        var offscreenRoot = roots[MortalLocationStorageContentsState.StatePath]
            as JsonObject;
        var parsedIndex = MortalItemIdentityState.Parse(transferredIndex.DeepClone());
        if (parsedIndex.Issues.Count > 0)
            return Invalid(input, parsedIndex.Issues);
        var acceptedEvidence = MortalItemIdentityState.BuildAcceptedRootCreationEvidence(
            parsedIndex);
        var index = parsedIndex.Root.DeepClone().AsObject();
        var indexEntries = index["entries"]!.AsArray();
        var pending = new List<PendingCreation>();
        var creationMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var knownItemIds = new HashSet<string>(parsedIndex.EntriesByItemId.Keys,
            StringComparer.Ordinal);
        var currentCatalog = BuildCatalog(roots, includeNpcCommands: true);
        if (currentCatalog.Issues.Count > 0)
        {
            return Invalid(input, new[]
            {
                Issue(
                    currentCatalog.Issues[0].Path,
                    currentCatalog.Issues[0].Code,
                    currentCatalog.Issues[0].Identity ?? "unknown",
                    "one valid detached carrier graph",
                    currentCatalog.Issues[0].Message)
            });
        }
        foreach (var occurrence in currentCatalog.Occurrences)
        {
            if (occurrence.ItemId != null)
                knownItemIds.Add(occurrence.ItemId);
        }

        void AddPending(JsonObject rawItem, string itemPath, Action<JsonObject> store)
        {
            CanonicalStateNormalizer.EnsureRawMortalItemCreation(
                rawItem,
                itemPath,
                input.Turn);
            var creationRef = RequireExactIdentity(rawItem["creationRef"],
                itemPath + ".creationRef");
            var materializationId = RequireExactIdentity(
                rawItem[MortalItemMaterializationContract.EnvelopeProperty]?["materializationId"],
                itemPath + ".materialization.materializationId");
            if (!creationMap.TryAdd(creationRef, string.Empty))
                throw new InvalidDataException($"Duplicate creationRef '{creationRef}'.");
            if (acceptedEvidence.Match(materializationId, creationRef) !=
                MortalItemAcceptedCreationEvidenceMatch.None)
            {
                throw new InvalidDataException(
                    $"Accepted creation evidence '{creationRef}' was already used.");
            }
            if (!input.RouteCatalog.ByCreationRef.TryGetValue(creationRef, out var route))
                throw new InvalidDataException($"Missing route authority for '{creationRef}'.");
            if (!input.Snapshot.TryGetAllocatedItemId(creationRef, out var itemId))
                throw new InvalidDataException($"Missing allocated item ID for '{creationRef}'.");
            if (!input.Snapshot.TryGetRootReceiptId(creationRef, out var receiptId))
                throw new InvalidDataException($"Missing root receipt ID for '{creationRef}'.");
            if (!input.Snapshot.TryGetCreateTransitionId(creationRef, out var transitionId))
                throw new InvalidDataException($"Missing create transition ID for '{creationRef}'.");
            if (!knownItemIds.Add(itemId))
                throw new InvalidDataException($"Allocated item ID '{itemId}' collides.");
            creationMap[creationRef] = itemId;
            pending.Add(new PendingCreation(
                rawItem,
                itemId,
                receiptId,
                transitionId,
                route,
                store));
        }

        var npcCommandIndex = CanonicalStateNormalizer.MortalNpcCommandIndex.Build(npcRoot);
        CanonicalStateNormalizer.CollectPlayerMortalItemCreations(playerRoot, AddPending);
        CanonicalStateNormalizer.CollectNpcCoreMortalItemCreations(npcRoot, AddPending);
        CanonicalStateNormalizer.CollectNpcCommandMortalItemCreations(
            npcCommandIndex,
            npcCommandsRoot,
            AddPending);
        CanonicalStateNormalizer.CollectLocationMortalItemCreations(locationRoot, AddPending);
        CanonicalStateNormalizer.CollectOffscreenLocationStorageMortalItemCreations(
            offscreenRoot,
            AddPending);

        foreach (var creation in pending)
        {
            var canonicalItem = creation.RawItem.DeepClone().AsObject();
            CanonicalStateNormalizer.RewriteMortalItemContentsPath(canonicalItem, creationMap);
            MortalItemLocalActionPolicy.NormalizePlacementForDestination(
                canonicalItem,
                CanonicalStateNormalizer.RewriteMortalItemCarrierCoordinate(
                    creation.Route.Destination,
                    creationMap));
            canonicalItem.Remove("resourceMaterialization");
            var receipt = MortalItemIdentityState.CreateRootReceipt(
                canonicalItem,
                creation.ItemId,
                input.Turn,
                creation.ReceiptId);
            canonicalItem["itemId"] = creation.ItemId;
            canonicalItem["existedId"] = creation.ItemId;
            canonicalItem.Remove("creationRef");
            canonicalItem[MortalItemMaterializationContract.ReceiptProperty] = receipt;
            creation.Store(canonicalItem);
            indexEntries.Add(CanonicalStateNormalizer.CreateMortalItemIdentityEntry(
                canonicalItem,
                receipt,
                input.Turn,
                creation.Route,
                creationMap,
                creation.TransitionId));
        }

        npcCommandIndex.RefreshInventoryItems();
        CanonicalStateNormalizer.RewriteMortalItemCreationReferences(playerRoot, creationMap);
        CanonicalStateNormalizer.RewriteMortalItemCreationReferences(npcRoot, creationMap);
        CanonicalStateNormalizer.RewriteMortalItemCreationReferences(npcCommandsRoot, creationMap);
        CanonicalStateNormalizer.ApplyMortalNpcEquipmentCommands(
            npcCommandIndex,
            npcCommandsRoot,
            new HashSet<string>(creationMap.Values, StringComparer.Ordinal));
        CanonicalStateNormalizer.RewriteMortalItemCreationReferences(locationRoot, creationMap);
        CanonicalStateNormalizer.RewriteMortalItemCreationReferences(offscreenRoot, creationMap);
        foreach (var path in ProjectionRootPaths.Skip(8))
        {
            if (roots[path] is JsonObject companion)
                CanonicalStateNormalizer.RewriteMortalItemCreationReferences(
                    companion,
                    creationMap);
        }

        var normalizedIndex = MortalItemIdentityState.Parse(index);
        if (normalizedIndex.Issues.Count > 0)
            return Invalid(input, normalizedIndex.Issues);
        var identityIndexAfterImage = pending.Count == 0
            ? transferredIndex.DeepClone().AsObject()
            : normalizedIndex.Root.DeepClone().AsObject();
        roots[MortalItemIdentityState.StatePath] =
            identityIndexAfterImage.DeepClone();
        var resultRoots = MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(roots);
        return new MortalItemCanonicalProjectionResult(
            resultRoots,
            identityIndexAfterImage,
            Array.Empty<ValidationIssue>(),
            Fingerprint(
                input,
                resultRoots,
                identityIndexAfterImage,
                Array.Empty<ValidationIssue>()));
    }

    private static List<ValidationIssue> ValidateInput(
        MortalItemCanonicalProjectionInput input)
    {
        var issues = new List<ValidationIssue>();
        if (input.Turn < 1 || input.Snapshot.Turn != input.Turn)
        {
            issues.Add(Issue(
                MortalItemIdentityState.StatePath,
                "mortal_item_projection_turn_mismatch",
                "accepted turn",
                input.Snapshot.Turn.ToString(System.Globalization.CultureInfo.InvariantCulture),
                input.Turn.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        ValidateExactPaths(input.CurrentRoots, "current", issues);
        ValidateExactPaths(input.BackupRoots, "backup", issues);
        if (issues.Count > 0)
            return issues;
        if (!input.Snapshot.MatchesRouteCatalog(input.RouteCatalog))
        {
            issues.Add(Issue(
                MortalItemIdentityState.StatePath,
                "mortal_item_projection_route_authority_mismatch",
                "routes",
                "the exact frozen route authority",
                "changed"));
        }
        if (!input.Snapshot.MatchesProjectionRoots(input.CurrentRoots, input.BackupRoots))
        {
            issues.Add(Issue(
                MortalItemIdentityState.StatePath,
                "mortal_item_projection_root_proof_mismatch",
                "roots",
                "the exact frozen current and backup root graph",
                "changed path, presence, topology, or content"));
        }
        var currentIdentity = MortalItemIdentityState.Parse(
            input.CurrentRoots[MortalItemIdentityState.StatePath]);
        if (currentIdentity.Issues.Count != 0 ||
            !JsonNode.DeepEquals(currentIdentity.Root, input.IdentityState.Root))
        {
            issues.Add(Issue(
                MortalItemIdentityState.StatePath,
                "mortal_item_projection_identity_mismatch",
                "identity",
                "the exact current identity root",
                "changed"));
        }
        if (input.IdentityState.Issues.Count > 0)
            issues.AddRange(input.IdentityState.Issues);
        return issues;
    }

    private static void ValidateExactPaths(
        IReadOnlyDictionary<string, JsonNode?> roots,
        string stage,
        ICollection<ValidationIssue> issues)
    {
        var expected = ProjectionRootPaths.ToHashSet(StringComparer.Ordinal);
        if (roots.Count != ProjectionRootPaths.Count ||
            !roots.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expected))
        {
            issues.Add(Issue(
                MortalItemIdentityState.StatePath,
                "mortal_item_projection_root_set_mismatch",
                stage,
                string.Join(",", ProjectionRootPaths),
                string.Join(",", roots.Keys.OrderBy(static value => value,
                    StringComparer.Ordinal))));
            return;
        }
        foreach (var pair in roots)
        {
            if (pair.Value == null)
                continue;
            var valid = string.Equals(
                    pair.Key,
                    StorageTransportMoveService.VehiclesPath,
                    StringComparison.Ordinal)
                ? pair.Value is JsonObject or JsonArray
                : pair.Value is JsonObject;
            if (!valid)
            {
                issues.Add(Issue(
                    pair.Key,
                    "mortal_item_projection_root_topology_invalid",
                    stage,
                    string.Equals(pair.Key, StorageTransportMoveService.VehiclesPath,
                        StringComparison.Ordinal)
                        ? "object or legacy array root"
                        : "object root",
                    pair.Value.GetType().Name));
            }
        }
    }

    internal static MortalItemCarrierCatalog BuildCatalog(
        IReadOnlyDictionary<string, JsonNode?> roots,
        bool includeNpcCommands)
    {
        var companions = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var path in ProjectionRootPaths.Skip(8))
        {
            if (roots[path] is JsonObject root)
                companions.Add(path, root);
        }
        var vehicleRoot = roots[StorageTransportMoveService.VehiclesPath] switch
        {
            JsonObject obj => obj,
            JsonArray array => new JsonObject { ["vehicles"] = array.DeepClone() },
            _ => null
        };
        return MortalItemCarrierCatalog.Build(new MortalItemCarrierCatalogInput(
            roots[InventoryEquipmentService.ItemsPath] as JsonObject,
            roots[NpcCoreChangesContract.NpcCorePath] as JsonObject,
            includeNpcCommands
                ? roots[MortalItemAcceptedTransferCatalog.NpcCommandsPath] as JsonObject
                : null,
            roots[StorageTransportMoveService.CurrentLocationPath] as JsonObject,
            vehicleRoot,
            companions,
            roots[MortalLocationStorageContentsState.StatePath] as JsonObject));
    }

    private static MortalItemCanonicalProjectionResult Invalid(
        MortalItemCanonicalProjectionInput input,
        IEnumerable<ValidationIssue> issues)
    {
        var array = issues.ToArray();
        return new MortalItemCanonicalProjectionResult(
            new Dictionary<string, JsonNode?>(StringComparer.Ordinal),
            MortalItemIdentityState.CreateEmptyRoot(),
            array,
            Fingerprint(input, null, null, array));
    }

    private static string RequireExactIdentity(JsonNode? node, string path)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var text) &&
            ResourceMaterializationContract.IsExactIdentifier(text))
        {
            return text;
        }
        throw new InvalidDataException($"'{path}' requires one exact identity.");
    }

    private static ValidationIssue Issue(
        string path,
        string code,
        string actor,
        string expected,
        string actual) => new(
        path,
        IssueSeverity.Error,
        "Accepted Mortal item canonical projection failed.",
        code: code,
        actor: "mortal_item:" + actor,
        section: "MortalItemMaterialization",
        expected: expected,
        actual: actual,
        repairHint: "Reject the accepted projection and rebuild raw-validation authority.",
        repairTargetFiles: new[] { path });

    private static string Fingerprint(
        MortalItemCanonicalProjectionInput input,
        IReadOnlyDictionary<string, JsonNode?>? roots,
        JsonObject? index,
        IReadOnlyList<ValidationIssue> issues)
    {
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_item.canonical_projection",
            "1",
            input.Turn.ToString(System.Globalization.CultureInfo.InvariantCulture),
            input.Snapshot.ProjectionProofFingerprint
        };
        foreach (var pair in input.CurrentRoots.OrderBy(static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            fields.Add("current");
            fields.Add(pair.Key);
            fields.Add(pair.Value == null
                ? "missing"
                : WoundAcceptedTurnFingerprintWriter.CanonicalJson(pair.Value));
        }
        foreach (var pair in input.BackupRoots.OrderBy(static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            fields.Add("backup");
            fields.Add(pair.Key);
            fields.Add(pair.Value == null
                ? "missing"
                : WoundAcceptedTurnFingerprintWriter.CanonicalJson(pair.Value));
        }
        foreach (var pair in roots?.OrderBy(static pair => pair.Key, StringComparer.Ordinal) ??
                             Enumerable.Empty<KeyValuePair<string, JsonNode?>>())
        {
            fields.Add("after");
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

    private sealed record PendingCreation(
        JsonObject RawItem,
        string ItemId,
        string ReceiptId,
        string TransitionId,
        MortalItemRouteAuthority Route,
        Action<JsonObject> Store);
}
