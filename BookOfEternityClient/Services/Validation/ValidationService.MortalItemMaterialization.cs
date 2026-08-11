using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private static readonly string[] MortalItemCompanionPaths =
    {
        "game_state/inventory/item_resources.json",
        "game_state/inventory/item_bonds.json",
        "game_state/inventory/item_text_updates.json",
        "game_state/inventory/recipes.json",
        "game_state/npcs/item_journals.json",
        "game_state/quests/quest_history.json"
    };

    public async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnRawMortalItemMaterializationAsync()
    {
        var issues = new List<ValidationIssue>();
        await ValidateAcceptedTurnRawMortalItemMaterializationAsync(issues);
        return issues;
    }

    public async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync()
    {
        var issues = new List<ValidationIssue>();
        await ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync(issues, null);
        return issues;
    }

    internal async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync(
            FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        var issues = new List<ValidationIssue>();
        await ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync(
            issues,
            writeLease);
        return issues;
    }

    private async Task ValidateAcceptedTurnRawMortalItemMaterializationAsync(
        List<ValidationIssue> issues)
    {
        var current = await LoadMortalItemCatalogAsync(
            writeLease: null,
            includeNpcInventoryCommands: true,
            issues);
        AddCatalogIssues(current.Catalog, issues);
        ValidateMortalItemCompanionReferences(
            current.Catalog,
            MortalItemMaterializationPhase.RawPreSeal,
            issues);

        foreach (var occurrence in current.Catalog.Occurrences)
        {
            if (IsRawMortalItemCreation(occurrence.Item))
            {
                AddContractIssues(
                    occurrence,
                    MortalItemMaterializationPhase.RawPreSeal,
                    issues);
                continue;
            }

            if (IsDurableCanonicalCarrierPath(occurrence.JsonPath))
            {
                AddContractIssues(
                    occurrence,
                    MortalItemMaterializationPhase.CanonicalPostSeal,
                    issues);
            }
        }

        var routeAuthorities = await MortalItemRouteAuthorityCatalog.BuildAsync(_fs);
        AddRouteAuthorityIssues(routeAuthorities, issues);

        var currentIndex = MortalItemIdentityState.Parse(current.IdentityIndexJson);
        issues.AddRange(currentIndex.Issues);

        var snapshotLookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        var hasRawCreations = current.Catalog.Occurrences.Any(occurrence =>
            IsRawMortalItemCreation(occurrence.Item));
        if (snapshotLookup.Status != ValidatedPendingTurnSnapshotStatus.Usable ||
            snapshotLookup.Manifest == null)
        {
            if (hasRawCreations)
                issues.Add(MissingItemSnapshotBaselineIssue("raw item creation"));
            return;
        }

        foreach (var occurrence in current.Catalog.Occurrences.Where(occurrence =>
                     IsRawMortalItemCreation(occurrence.Item)))
        {
            ValidateRawSnapshotBinding(
                occurrence,
                snapshotLookup.Manifest.TurnNumber,
                issues);
        }

        var previous = await LoadPreTurnMortalItemCatalogAsync(
            snapshotLookup,
            includeNpcInventoryCommands: false,
            issues);
        if (previous == null)
        {
            if (hasRawCreations || current.Catalog.Occurrences.Count > 0)
                issues.Add(MissingItemSnapshotBaselineIssue("tracked item authority"));
            return;
        }

        var previousIndex = MortalItemIdentityState.Parse(previous.IdentityIndexJson);
        if (previousIndex.Issues.Count > 0)
        {
            issues.Add(MissingItemSnapshotBaselineIssue("valid pre-turn item identity index"));
            return;
        }

        if (!MortalItemMaterializationContract.ImmutableEvidenceEquals(
                previousIndex.Root,
                currentIndex.Root))
        {
            issues.Add(new ValidationIssue(
                MortalItemIdentityState.StatePath,
                IssueSeverity.Error,
                "The GM-authored raw package changed the client-owned Mortal item identity index before sealing.",
                code: "mortal_item_materialization_gm_authored_client_field",
                actor: "mortal_item:index",
                section: "MortalItemMaterialization",
                expected: "index exactly equal to the validated pre-turn snapshot",
                actual: "client-owned index differs before normalization",
                repairHint: "Восстанови item_identity_index.json из validated pre-turn snapshot и убери любые GM-authored receipt/index изменения.",
                repairTargetFiles: new[] { MortalItemIdentityState.StatePath }));
        }

        ValidateRawCurrentItemContinuity(previous.Catalog, current.Catalog, issues);
    }

    private static void ValidateRawSnapshotBinding(
        MortalItemCarrierOccurrence occurrence,
        int acceptedTurn,
        List<ValidationIssue> issues)
    {
        var envelope = occurrence.Item["materialization"] as JsonObject;
        var sourceTurn = TryReadInt(envelope?["sourceTurn"]);
        if (sourceTurn != acceptedTurn)
        {
            issues.Add(new ValidationIssue(
                $"{occurrence.JsonPath}.materialization.sourceTurn",
                IssueSeverity.Error,
                "A raw Mortal item creation must bind to the active validated accepted turn.",
                code: "mortal_item_materialization_route_authority_mismatch",
                actor: occurrence.CreationRef == null
                    ? "mortal_item:new:unknown"
                    : $"mortal_item:new:{occurrence.CreationRef}",
                section: "MortalItemMaterialization",
                expected: acceptedTurn.ToString(),
                actual: sourceTurn?.ToString() ?? "missing",
                repairHint: "Скопируй exact active turn number из validated turn request; не переиспользуй envelope другого хода.",
                repairTargetFiles: new[] { occurrence.FilePath }));
        }

        var route = ReadExactString(envelope?["route"]);
        var carrierAllowed = route switch
        {
            "player_acquisition" => occurrence.Carrier.Kind == "player_inventory",
            "npc_acquisition" or "new_npc_inventory" =>
                occurrence.Carrier.Kind == "npc_inventory",
            "storage_placement" => occurrence.Carrier.Kind == "location_storage",
            "loot_acquisition" or "craft_output" or "trade_output" or "quest_reward" =>
                occurrence.Carrier.Kind is "player_inventory" or "npc_inventory" or "location_storage",
            _ => true
        };
        if (!carrierAllowed)
        {
            issues.Add(new ValidationIssue(
                occurrence.JsonPath,
                IssueSeverity.Error,
                "The raw creation route does not authorize this destination carrier kind.",
                code: "mortal_item_materialization_route_authority_mismatch",
                actor: occurrence.CreationRef == null
                    ? "mortal_item:new:unknown"
                    : $"mortal_item:new:{occurrence.CreationRef}",
                section: "MortalItemMaterialization",
                expected: $"carrier allowed by route {route ?? "missing"}",
                actual: occurrence.Carrier.Kind,
                repairHint: "Перемести raw item в разрешённый route carrier и сохрани один exact creationRef.",
                repairTargetFiles: new[] { occurrence.FilePath }));
        }
    }

    private async Task ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync(
        List<ValidationIssue> issues,
        FileSystemManager.CanonicalWriteLease? writeLease)
    {
        var current = await LoadMortalItemCatalogAsync(
            writeLease,
            includeNpcInventoryCommands: false,
            issues);
        AddCatalogIssues(current.Catalog, issues);
        ValidateMortalItemCompanionReferences(
            current.Catalog,
            MortalItemMaterializationPhase.CanonicalPostSeal,
            issues);

        foreach (var occurrence in current.Catalog.Occurrences)
        {
            AddContractIssues(
                occurrence,
                MortalItemMaterializationPhase.CanonicalPostSeal,
                issues);
        }

        var currentIndex = MortalItemIdentityState.Parse(current.IdentityIndexJson);
        issues.AddRange(currentIndex.Issues);
        ValidateCanonicalCatalogAgainstIndex(current.Catalog, currentIndex, issues);
        ValidateCanonicalContainerPaths(current.Catalog, issues);

        var snapshotLookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        if (snapshotLookup.Status != ValidatedPendingTurnSnapshotStatus.Usable ||
            snapshotLookup.Manifest == null)
        {
            return;
        }

        var previous = await LoadPreTurnMortalItemCatalogAsync(
            snapshotLookup,
            includeNpcInventoryCommands: false,
            issues);
        if (previous == null)
        {
            if (current.Catalog.Occurrences.Count > 0 ||
                currentIndex.EntriesByItemId.Count > 0)
            {
                issues.Add(MissingItemSnapshotBaselineIssue("canonical item continuity"));
            }

            return;
        }

        var previousIndex = MortalItemIdentityState.Parse(previous.IdentityIndexJson);
        if (previousIndex.Issues.Count > 0)
        {
            issues.Add(MissingItemSnapshotBaselineIssue("valid pre-turn item identity index"));
            return;
        }

        issues.AddRange(MortalItemIdentityState.ValidateAgainst(previousIndex, currentIndex));
        ValidateCanonicalImmutableEvidence(previous.Catalog, current.Catalog, issues);
    }

    private async Task<MortalItemCatalogFiles> LoadMortalItemCatalogAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        bool includeNpcInventoryCommands,
        List<ValidationIssue> issues)
    {
        var playerJson = await ReadCurrentItemFileAsync(
            writeLease,
            InventoryEquipmentService.ItemsPath);
        var npcCoreJson = await ReadCurrentItemFileAsync(
            writeLease,
            NpcCoreChangesContract.NpcCorePath);
        var npcCommandsJson = includeNpcInventoryCommands
            ? await ReadCurrentItemFileAsync(
                writeLease,
                "game_state/npcs/npc_inventory.json")
            : null;
        var currentLocationJson = await ReadCurrentItemFileAsync(
            writeLease,
            StorageTransportMoveService.CurrentLocationPath);
        var vehiclesJson = await ReadCurrentItemFileAsync(
            writeLease,
            StorageTransportMoveService.VehiclesPath);
        var identityIndexJson = await ReadCurrentItemFileAsync(
            writeLease,
            MortalItemIdentityState.StatePath);

        var companions = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var path in MortalItemCompanionPaths)
        {
            var json = await ReadCurrentItemFileAsync(writeLease, path);
            var companion = ParseOptionalObject(json);
            if (companion != null)
                companions.Add(path, companion);
        }

        var input = new MortalItemCarrierCatalogInput(
            ParseCarrierObject(playerJson, InventoryEquipmentService.ItemsPath, issues),
            ParseCarrierObject(npcCoreJson, NpcCoreChangesContract.NpcCorePath, issues),
            ParseCarrierObject(
                npcCommandsJson,
                "game_state/npcs/npc_inventory.json",
                issues),
            ParseCarrierObject(
                currentLocationJson,
                StorageTransportMoveService.CurrentLocationPath,
                issues),
            ParseVehiclesObject(vehiclesJson, issues),
            companions);
        return new MortalItemCatalogFiles(
            MortalItemCarrierCatalog.Build(input),
            identityIndexJson);
    }

    private async Task<MortalItemCatalogFiles?> LoadPreTurnMortalItemCatalogAsync(
        ValidatedPendingTurnSnapshotLookup lookup,
        bool includeNpcInventoryCommands,
        List<ValidationIssue> issues)
    {
        if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable ||
            lookup.Manifest == null)
        {
            return null;
        }

        var playerJson = await ReadValidatedPendingTurnSnapshotFileAsync(
            lookup.Manifest,
            InventoryEquipmentService.ItemsPath);
        var npcCoreJson = await ReadValidatedPendingTurnSnapshotFileAsync(
            lookup.Manifest,
            NpcCoreChangesContract.NpcCorePath);
        var npcCommandsJson = includeNpcInventoryCommands
            ? await ReadValidatedPendingTurnSnapshotFileAsync(
                lookup.Manifest,
                "game_state/npcs/npc_inventory.json")
            : null;
        var currentLocationJson = await ReadValidatedPendingTurnSnapshotFileAsync(
            lookup.Manifest,
            StorageTransportMoveService.CurrentLocationPath);
        var vehiclesJson = await ReadValidatedPendingTurnSnapshotFileAsync(
            lookup.Manifest,
            StorageTransportMoveService.VehiclesPath);
        var identityIndexJson = await ReadValidatedPendingTurnSnapshotFileAsync(
            lookup.Manifest,
            MortalItemIdentityState.StatePath);
        if (identityIndexJson == null)
            return null;

        var companions = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var path in MortalItemCompanionPaths)
        {
            var json = await ReadValidatedPendingTurnSnapshotFileAsync(
                lookup.Manifest,
                path);
            var companion = ParseOptionalObject(json);
            if (companion != null)
                companions.Add(path, companion);
        }

        var baselineIssues = new List<ValidationIssue>();
        var input = new MortalItemCarrierCatalogInput(
            ParseCarrierObject(
                playerJson,
                InventoryEquipmentService.ItemsPath,
                baselineIssues),
            ParseCarrierObject(
                npcCoreJson,
                NpcCoreChangesContract.NpcCorePath,
                baselineIssues),
            ParseCarrierObject(
                npcCommandsJson,
                "game_state/npcs/npc_inventory.json",
                baselineIssues),
            ParseCarrierObject(
                currentLocationJson,
                StorageTransportMoveService.CurrentLocationPath,
                baselineIssues),
            ParseVehiclesObject(vehiclesJson, baselineIssues),
            companions);
        var catalog = MortalItemCarrierCatalog.Build(input);
        if (baselineIssues.Count > 0 || catalog.Issues.Count > 0)
        {
            issues.Add(MissingItemSnapshotBaselineIssue("readable pre-turn item carriers"));
            return null;
        }

        return new MortalItemCatalogFiles(catalog, identityIndexJson);
    }

    private async Task<string?> ReadCurrentItemFileAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        string path)
    {
        return writeLease == null
            ? await _fs.ReadFileAsync(path)
            : await _fs.ReadFileAsync(writeLease, path);
    }

    private static JsonObject? ParseCarrierObject(
        string? json,
        string path,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var node = JsonNode.Parse(json);
            if (node is JsonObject root)
                return root;

            issues.Add(InvalidCarrierRootIssue(path, node?.GetValueKind().ToString() ?? "null"));
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or ArgumentException)
        {
            issues.Add(InvalidCarrierRootIssue(path, exception.Message));
        }

        return null;
    }

    private static JsonObject? ParseVehiclesObject(
        string? json,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var node = JsonNode.Parse(json);
            if (node is JsonObject root)
                return root;
            if (node is JsonArray vehicles)
            {
                return new JsonObject
                {
                    ["vehicles"] = vehicles.DeepClone()
                };
            }

            issues.Add(InvalidCarrierRootIssue(
                StorageTransportMoveService.VehiclesPath,
                node?.GetValueKind().ToString() ?? "null"));
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or ArgumentException)
        {
            issues.Add(InvalidCarrierRootIssue(
                StorageTransportMoveService.VehiclesPath,
                exception.Message));
        }

        return null;
    }

    private static JsonObject? ParseOptionalObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    private static ValidationIssue InvalidCarrierRootIssue(string path, string actual) =>
        new(
            path,
            IssueSeverity.Error,
            "A governed Mortal item carrier must have a readable object root.",
            code: "mortal_item_materialization_invalid_carrier_root",
            actor: "mortal_item:unknown",
            section: "MortalItemMaterialization",
            expected: "readable JSON object",
            actual: actual,
            repairHint: "Восстанови только указанный carrier-файл из validated snapshot и повтори минимальную item-операцию.",
            repairTargetFiles: new[] { path });

    private static bool IsRawMortalItemCreation(JsonObject item)
    {
        if (item.ContainsKey("creationRef"))
            return true;
        return item.TryGetPropertyValue("existedId", out var existedId) && existedId == null;
    }

    private static bool IsDurableCanonicalCarrierPath(string jsonPath) =>
        !jsonPath.Contains(".UpdateInventory[", StringComparison.Ordinal) &&
        !jsonPath.Contains(".NPCInventoryAdds[", StringComparison.Ordinal);

    private static void AddContractIssues(
        MortalItemCarrierOccurrence occurrence,
        MortalItemMaterializationPhase phase,
        List<ValidationIssue> issues)
    {
        using var document = JsonDocument.Parse(occurrence.Item.ToJsonString());
        foreach (var issue in MortalItemMaterializationContract.Validate(
                     document.RootElement,
                     occurrence.JsonPath,
                     phase))
        {
            issues.Add(CloneIssueWithTarget(issue, occurrence.FilePath));
        }
    }

    private static void AddCatalogIssues(
        MortalItemCarrierCatalog catalog,
        List<ValidationIssue> issues)
    {
        foreach (var issue in catalog.Issues)
        {
            var targetPath = ResolveCarrierFilePath(issue.Path);
            var actor = issue.IdentityKind switch
            {
                "itemId" when issue.Identity != null => $"mortal_item:existing:{issue.Identity}",
                "creationRef" when issue.Identity != null => $"mortal_item:new:{issue.Identity}",
                _ => "mortal_item:unknown"
            };
            issues.Add(new ValidationIssue(
                issue.Path,
                IssueSeverity.Error,
                issue.Message,
                code: issue.Code,
                actor: actor,
                section: "MortalItemMaterialization",
                expected: "one exact unambiguous identity and carrier occurrence",
                actual: issue.Identity ?? "missing",
                repairHint: "Исправь только названный item/carrier; не нормализуй регистр, пробелы или Unicode и не создавай второй receipt.",
                repairTargetFiles: new[] { targetPath }));
        }
    }

    private static void AddRouteAuthorityIssues(
        MortalItemRouteAuthorityCatalog catalog,
        List<ValidationIssue> issues)
    {
        foreach (var issue in catalog.Issues)
        {
            issues.Add(new ValidationIssue(
                issue.Path,
                IssueSeverity.Error,
                issue.Message,
                code: issue.Code,
                actor: issue.CreationRef == null
                    ? "mortal_item:new:unknown"
                    : $"mortal_item:new:{issue.CreationRef}",
                section: "MortalItemMaterialization",
                expected: issue.Expected,
                actual: issue.Actual,
                repairHint: "Исправь только exact route authority этого creationRef по validated request/reward/carrier; не создавай itemId или receipt вручную.",
                repairTargetFiles: new[] { issue.FilePath }));
        }
    }

    private static void ValidateMortalItemCompanionReferences(
        MortalItemCarrierCatalog catalog,
        MortalItemMaterializationPhase phase,
        List<ValidationIssue> issues)
    {
        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in catalog.ByCompanionReference)
        {
            catalog.ByItemId.TryGetValue(pair.Key, out var itemOccurrences);
            var itemMatches = itemOccurrences?.Count ?? 0;
            var rawCreationOccurrences =
                phase == MortalItemMaterializationPhase.RawPreSeal &&
                catalog.ByCreationRef.TryGetValue(pair.Key, out var creationOccurrences)
                    ? creationOccurrences
                        .Where(occurrence => IsRawMortalItemCreation(occurrence.Item))
                        .ToArray()
                    : Array.Empty<MortalItemCarrierOccurrence>();
            var creationMatches = rawCreationOccurrences.Length;
            var totalMatches = itemMatches + creationMatches;
            if (totalMatches == 1)
            {
                var resolvedOccurrence = itemMatches == 1
                    ? itemOccurrences![0]
                    : rawCreationOccurrences[0];
                ValidateMortalItemCompanionCarrierAgreement(
                    pair.Key,
                    pair.Value,
                    resolvedOccurrence,
                    issues);
                continue;
            }

            foreach (var reference in pair.Value)
            {
                var reportKey = $"{reference.FilePath}\u001f{reference.JsonPath}\u001f{pair.Key}";
                if (!reported.Add(reportKey))
                    continue;

                var looksLikeCreationReference =
                    reference.PropertyName.Contains("creation", StringComparison.OrdinalIgnoreCase);
                var looksLikePermanentReference =
                    reference.PropertyName.Equals("itemId", StringComparison.OrdinalIgnoreCase) ||
                    reference.PropertyName.Equals("existedId", StringComparison.OrdinalIgnoreCase) ||
                    reference.PropertyName.Equals("sourceItemId", StringComparison.OrdinalIgnoreCase) ||
                    reference.PropertyName.Equals("targetItemId", StringComparison.OrdinalIgnoreCase) ||
                    reference.PropertyName.Equals("parentItemId", StringComparison.OrdinalIgnoreCase) ||
                    reference.PropertyName.Equals("containerItemId", StringComparison.OrdinalIgnoreCase) ||
                    reference.PropertyName.Equals("rewardItemId", StringComparison.OrdinalIgnoreCase) ||
                    reference.PropertyName.Equals("destinationItemId", StringComparison.OrdinalIgnoreCase) ||
                    reference.PropertyName.Equals("resultItemId", StringComparison.OrdinalIgnoreCase);
                var actor = looksLikeCreationReference || creationMatches > 0
                    ? $"mortal_item:new:{pair.Key}"
                    : looksLikePermanentReference || itemMatches > 0 ||
                      phase == MortalItemMaterializationPhase.CanonicalPostSeal
                        ? $"mortal_item:existing:{pair.Key}"
                        : $"mortal_item:unresolved:{pair.Key}";
                issues.Add(new ValidationIssue(
                    reference.JsonPath,
                    IssueSeverity.Error,
                    "A Mortal item companion reference must resolve to exactly one governed item occurrence.",
                    code: "mortal_item_materialization_orphan_companion",
                    actor: actor,
                    section: "MortalItemMaterialization",
                    expected: phase == MortalItemMaterializationPhase.RawPreSeal
                        ? "one exact current itemId or same-turn creationRef"
                        : "one exact canonical itemId",
                    actual: totalMatches == 0
                        ? $"unresolved exact reference {pair.Key}"
                        : $"{totalMatches} exact carrier matches for {pair.Key}",
                    repairHint: "Исправь или удали только эту companion-ссылку; она должна указывать на один exact itemId, а до sealing также может указывать на один same-turn creationRef.",
                    repairTargetFiles: new[] { reference.FilePath }));
            }
        }
    }

    private static void ValidateMortalItemCompanionCarrierAgreement(
        string referenceIdentity,
        IReadOnlyList<MortalItemCompanionReference> references,
        MortalItemCarrierOccurrence resolvedOccurrence,
        List<ValidationIssue> issues)
    {
        foreach (var reference in references)
        {
            if (reference.ExpectedCarrier == null ||
                SameRootCarrier(
                    reference.ExpectedCarrier,
                    resolvedOccurrence.Carrier))
            {
                continue;
            }

            issues.Add(new ValidationIssue(
                reference.JsonPath,
                IssueSeverity.Error,
                "A Mortal item companion reference resolves to an item owned by a different carrier authority.",
                code: "mortal_item_materialization_companion_owner_mismatch",
                actor: resolvedOccurrence.ItemId != null
                    ? $"mortal_item:existing:{resolvedOccurrence.ItemId}"
                    : $"mortal_item:new:{referenceIdentity}",
                section: "MortalItemMaterialization",
                expected: CreateCarrierNode(reference.ExpectedCarrier).ToJsonString(),
                actual: CreateCarrierNode(resolvedOccurrence.Carrier).ToJsonString(),
                repairHint: "Ссылайся только на exact предмет того же владельца и carrier; не связывай чужой inventory/storage.",
                repairTargetFiles: new[] { reference.FilePath }));
        }
    }

    private static void ValidateRawCurrentItemContinuity(
        MortalItemCarrierCatalog previous,
        MortalItemCarrierCatalog current,
        List<ValidationIssue> issues)
    {
        foreach (var occurrence in current.Occurrences)
        {
            if (IsRawMortalItemCreation(occurrence.Item) || occurrence.ItemId == null)
                continue;

            if (!previous.ByItemId.TryGetValue(occurrence.ItemId, out var previousOccurrences) ||
                previousOccurrences.Count != 1)
            {
                if (IsDurableCanonicalCarrierPath(occurrence.JsonPath))
                {
                    issues.Add(new ValidationIssue(
                        occurrence.JsonPath,
                        IssueSeverity.Error,
                        "A new canonical item with client identity appeared before the sealing phase.",
                        code: "mortal_item_materialization_gm_authored_client_field",
                        actor: $"mortal_item:existing:{occurrence.ItemId}",
                        section: "MortalItemMaterialization",
                        expected: "raw new item with existedId null and creationRef",
                        actual: "previously unknown permanent itemId/receipt",
                        repairHint: "Верни новый предмет к raw shape: existedId=null, creationRef, полный envelope и без itemId/receipt/index.",
                        repairTargetFiles: new[] { occurrence.FilePath }));
                }

                continue;
            }

            ValidateImmutableOccurrence(
                previousOccurrences[0],
                occurrence,
                issues,
                rawPhase: true);
        }

        foreach (var previousPair in previous.ByItemId)
        {
            if (previousPair.Value.Count != 1 ||
                current.ByItemId.ContainsKey(previousPair.Key))
            {
                continue;
            }

            var previousOccurrence = previousPair.Value[0];
            issues.Add(new ValidationIssue(
                previousOccurrence.JsonPath,
                IssueSeverity.Error,
                "A pre-existing canonical item disappeared before client normalization.",
                code: "mortal_item_materialization_immutable_envelope_rewrite",
                actor: $"mortal_item:existing:{previousPair.Key}",
                section: "MortalItemMaterialization",
                expected: "pre-turn carrier item remains until an authorized client transition",
                actual: "item missing from raw current carriers",
                repairHint: "Восстанови exact pre-turn item в его carrier; выражай перенос/удаление через поддерживаемую command surface.",
                repairTargetFiles: new[] { previousOccurrence.FilePath }));
        }
    }

    private static void ValidateCanonicalCatalogAgainstIndex(
        MortalItemCarrierCatalog catalog,
        MortalItemIdentityParseResult index,
        List<ValidationIssue> issues)
    {
        foreach (var occurrence in catalog.Occurrences)
        {
            if (occurrence.ItemId == null)
                continue;

            if (!index.EntriesByItemId.TryGetValue(occurrence.ItemId, out var entry))
            {
                issues.Add(new ValidationIssue(
                    occurrence.JsonPath,
                    IssueSeverity.Error,
                    "A canonical Mortal item has no matching client identity-index entry.",
                    code: "mortal_item_materialization_missing_index_entry",
                    actor: $"mortal_item:existing:{occurrence.ItemId}",
                    section: "MortalItemMaterialization",
                    expected: "one active exact index entry",
                    actual: "missing",
                    repairHint: "Не создавай index вручную: верни raw creation package либо восстанови exact client-owned index из rollback authority.",
                    repairTargetFiles: new[] { occurrence.FilePath }));
                continue;
            }

            var state = ReadExactString(entry["state"]);
            if (!string.Equals(state, "active", StringComparison.Ordinal))
            {
                issues.Add(IndexMismatchIssue(
                    occurrence,
                    "mortal_item_materialization_index_state_mismatch",
                    "active index state",
                    state ?? "missing"));
            }

            var indexedReceiptId = ReadExactString(entry["receiptId"]);
            if (occurrence.ReceiptId == null ||
                !string.Equals(indexedReceiptId, occurrence.ReceiptId, StringComparison.Ordinal))
            {
                issues.Add(IndexMismatchIssue(
                    occurrence,
                    "mortal_item_materialization_index_receipt_mismatch",
                    occurrence.ReceiptId ?? "valid embedded receiptId",
                    indexedReceiptId ?? "missing"));
            }

            var expectedCarrier = CreateCarrierNode(occurrence.Carrier);
            if (entry["currentCarrier"] is not JsonObject indexedCarrier ||
                !MortalItemMaterializationContract.ImmutableEvidenceEquals(
                    expectedCarrier,
                    indexedCarrier))
            {
                issues.Add(IndexMismatchIssue(
                    occurrence,
                    "mortal_item_materialization_index_carrier_mismatch",
                    expectedCarrier.ToJsonString(),
                    entry["currentCarrier"]?.ToJsonString() ?? "null"));
            }

            if (occurrence.MaterializationId != null &&
                !ContainsExactString(
                    entry["originMaterializationIds"] as JsonArray,
                    occurrence.MaterializationId))
            {
                issues.Add(IndexMismatchIssue(
                    occurrence,
                    "mortal_item_materialization_index_origin_mismatch",
                    occurrence.MaterializationId,
                    entry["originMaterializationIds"]?.ToJsonString() ?? "missing"));
            }
        }

        foreach (var pair in index.EntriesByItemId)
        {
            var state = ReadExactString(pair.Value["state"]);
            var occurrenceCount = catalog.ByItemId.TryGetValue(pair.Key, out var occurrences)
                ? occurrences.Count
                : 0;
            if (string.Equals(state, "active", StringComparison.Ordinal) && occurrenceCount != 1)
            {
                issues.Add(new ValidationIssue(
                    MortalItemIdentityState.StatePath,
                    IssueSeverity.Error,
                    "An active identity-index entry must resolve to exactly one carrier item.",
                    code: "mortal_item_materialization_orphan_index_entry",
                    actor: $"mortal_item:existing:{pair.Key}",
                    section: "MortalItemMaterialization",
                    expected: "exactly one carrier occurrence",
                    actual: occurrenceCount.ToString(),
                    repairHint: "Восстанови exact carrier/index agreement; не клонируй предмет и не переиспользуй retired itemId.",
                    repairTargetFiles: new[] { MortalItemIdentityState.StatePath }));
            }
            else if (!string.Equals(state, "active", StringComparison.Ordinal) && occurrenceCount != 0)
            {
                issues.Add(new ValidationIssue(
                    MortalItemIdentityState.StatePath,
                    IssueSeverity.Error,
                    "A retired identity-index entry still resolves to a live carrier item.",
                    code: "mortal_item_materialization_retired_item_present",
                    actor: $"mortal_item:existing:{pair.Key}",
                    section: "MortalItemMaterialization",
                    expected: "zero carrier occurrences for retired identity",
                    actual: occurrenceCount.ToString(),
                    repairHint: "Убери только незаконную live-копию либо восстанови авторизованный transition; retired itemId нельзя активировать повторно.",
                    repairTargetFiles: new[] { MortalItemIdentityState.StatePath }));
            }
        }
    }

    private static void ValidateCanonicalContainerPaths(
        MortalItemCarrierCatalog catalog,
        List<ValidationIssue> issues)
    {
        foreach (var child in catalog.Occurrences)
        {
            if (child.ItemId == null || child.Carrier.ContainerPath.Count == 0)
                continue;

            var expectedParentPath = new List<string>();
            foreach (var parentId in child.Carrier.ContainerPath)
            {
                if (!catalog.ByItemId.TryGetValue(parentId, out var parentOccurrences) ||
                    parentOccurrences.Count != 1)
                {
                    issues.Add(ContainerPathIssue(
                        child,
                        "mortal_item_materialization_container_parent_missing",
                        $"one exact active parent item {parentId}",
                        parentOccurrences == null
                            ? "missing"
                            : $"{parentOccurrences.Count} occurrences",
                        child.FilePath));
                    expectedParentPath.Add(parentId);
                    continue;
                }

                var parent = parentOccurrences[0];
                if (!SameRootCarrier(child.Carrier, parent.Carrier))
                {
                    issues.Add(ContainerPathIssue(
                        child,
                        "mortal_item_materialization_container_parent_carrier_mismatch",
                        "parent in the same exact root carrier",
                        parent.Carrier.ToString(),
                        parent.FilePath));
                }

                if (!IsTrue(parent.Item["isContainer"]))
                {
                    issues.Add(ContainerPathIssue(
                        child,
                        "mortal_item_materialization_container_parent_invalid",
                        $"{parentId}.isContainer = true",
                        parent.Item["isContainer"]?.ToJsonString() ?? "missing",
                        parent.FilePath));
                }

                if (!parent.Carrier.ContainerPath.SequenceEqual(
                        expectedParentPath,
                        StringComparer.Ordinal))
                {
                    issues.Add(ContainerPathIssue(
                        child,
                        "mortal_item_materialization_container_path_chain_mismatch",
                        new JsonArray(
                            expectedParentPath.Select(value => (JsonNode?)value).ToArray())
                            .ToJsonString(),
                        new JsonArray(
                            parent.Carrier.ContainerPath.Select(value => (JsonNode?)value).ToArray())
                            .ToJsonString(),
                        parent.FilePath));
                }

                expectedParentPath.Add(parentId);
            }
        }
    }

    private static ValidationIssue ContainerPathIssue(
        MortalItemCarrierOccurrence child,
        string code,
        string expected,
        string actual,
        string parentFilePath) =>
        new(
            $"{child.JsonPath}.contentsPath",
            IssueSeverity.Error,
            "The canonical contentsPath does not resolve to an exact active container chain.",
            code: code,
            actor: $"mortal_item:existing:{child.ItemId}",
            section: "MortalItemMaterialization",
            expected: expected,
            actual: actual,
            repairHint: "Используй ordered permanent itemId chain от внешнего к непосредственному container; каждый parent должен быть active container в том же carrier.",
            repairTargetFiles: new[] { child.FilePath, parentFilePath }
                .Distinct(StringComparer.Ordinal)
                .ToArray());

    private static bool SameRootCarrier(
        MortalItemCarrierCoordinate left,
        MortalItemCarrierCoordinate right) =>
        string.Equals(left.Kind, right.Kind, StringComparison.Ordinal) &&
        string.Equals(left.OwnerId, right.OwnerId, StringComparison.Ordinal) &&
        string.Equals(left.ContainerId, right.ContainerId, StringComparison.Ordinal);

    private static bool IsTrue(JsonNode? node) =>
        node is JsonValue value &&
        value.TryGetValue<bool>(out var result) &&
        result;

    private static void ValidateCanonicalImmutableEvidence(
        MortalItemCarrierCatalog previous,
        MortalItemCarrierCatalog current,
        List<ValidationIssue> issues)
    {
        foreach (var pair in previous.ByItemId)
        {
            if (pair.Value.Count != 1 ||
                !current.ByItemId.TryGetValue(pair.Key, out var currentOccurrences) ||
                currentOccurrences.Count != 1)
            {
                continue;
            }

            ValidateImmutableOccurrence(
                pair.Value[0],
                currentOccurrences[0],
                issues,
                rawPhase: false);
        }
    }

    private static void ValidateImmutableOccurrence(
        MortalItemCarrierOccurrence previous,
        MortalItemCarrierOccurrence current,
        List<ValidationIssue> issues,
        bool rawPhase)
    {
        ValidateImmutableNode(
            previous,
            current,
            MortalItemMaterializationContract.EnvelopeProperty,
            "mortal_item_materialization_immutable_envelope_rewrite",
            rawPhase,
            issues);
        ValidateImmutableNode(
            previous,
            current,
            MortalItemMaterializationContract.ReceiptProperty,
            "mortal_item_materialization_immutable_receipt_rewrite",
            rawPhase,
            issues);
    }

    private static void ValidateImmutableNode(
        MortalItemCarrierOccurrence previous,
        MortalItemCarrierOccurrence current,
        string propertyName,
        string issueCode,
        bool rawPhase,
        List<ValidationIssue> issues)
    {
        var previousNode = previous.Item[propertyName];
        var currentNode = current.Item[propertyName];
        if (previousNode != null &&
            currentNode != null &&
            MortalItemMaterializationContract.ImmutableEvidenceEquals(
                previousNode,
                currentNode))
        {
            return;
        }

        if (previousNode == null && currentNode == null)
            return;

        issues.Add(new ValidationIssue(
            $"{current.JsonPath}.{propertyName}",
            IssueSeverity.Error,
            $"A pre-existing Mortal item changed immutable {propertyName} evidence.",
            code: issueCode,
            actor: $"mortal_item:existing:{current.ItemId}",
            section: "MortalItemMaterialization",
            expected: "exact validated pre-turn value",
            actual: currentNode?.ToJsonString() ?? "missing",
            repairHint: rawPhase
                ? $"Восстанови exact pre-turn {propertyName}; raw GM package не владеет client/accepted evidence."
                : $"Восстанови exact pre-turn {propertyName}; normalizer не должен переписывать принятую историю.",
            repairTargetFiles: new[] { current.FilePath }));
    }

    private static ValidationIssue IndexMismatchIssue(
        MortalItemCarrierOccurrence occurrence,
        string code,
        string expected,
        string actual) =>
        new(
            occurrence.JsonPath,
            IssueSeverity.Error,
            "The canonical item and client identity-index entry disagree.",
            code: code,
            actor: $"mortal_item:existing:{occurrence.ItemId}",
            section: "MortalItemMaterialization",
            expected: expected,
            actual: actual,
            repairHint: "Восстанови exact client-owned receipt/index/carrier agreement; не создавай новую identity вместо существующей.",
            repairTargetFiles: new[] { occurrence.FilePath });

    private static JsonObject CreateCarrierNode(MortalItemCarrierCoordinate carrier) =>
        new()
        {
            ["kind"] = carrier.Kind,
            ["ownerId"] = carrier.OwnerId,
            ["containerId"] = carrier.ContainerId,
            ["containerPath"] = new JsonArray(
                carrier.ContainerPath.Select(value => (JsonNode?)value).ToArray())
        };

    private static bool ContainsExactString(JsonArray? array, string expected)
    {
        if (array == null)
            return false;
        foreach (var node in array)
        {
            if (string.Equals(ReadExactString(node), expected, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string? ReadExactString(JsonNode? node)
    {
        return node is JsonValue value &&
               value.TryGetValue<string>(out var text) &&
               !string.IsNullOrEmpty(text)
            ? text
            : null;
    }

    private static int? TryReadInt(JsonNode? node)
    {
        if (node is not JsonValue value)
            return null;
        if (value.TryGetValue<int>(out var intValue))
            return intValue;
        if (value.TryGetValue<long>(out var longValue) &&
            longValue is >= int.MinValue and <= int.MaxValue)
        {
            return (int)longValue;
        }

        return null;
    }

    private static ValidationIssue CloneIssueWithTarget(
        ValidationIssue issue,
        string targetPath) =>
        new(
            issue.FilePath,
            issue.Severity,
            issue.Message,
            issue.Code,
            issue.Actor,
            issue.Section,
            issue.Expected,
            issue.Actual,
            issue.RepairHint,
            issue.Category,
            new[] { targetPath });

    private static ValidationIssue MissingItemSnapshotBaselineIssue(string actual) =>
        new(
            "game_state/control/pending_turn_snapshot.json",
            IssueSeverity.Error,
            "Mortal item materialization requires a usable validated pre-turn snapshot baseline.",
            code: "mortal_item_materialization_missing_snapshot_baseline",
            actor: "mortal_item:unknown",
            section: "MortalItemMaterialization",
            expected: "hash-validated snapshot with item carriers and client identity index",
            actual: actual,
            repairHint: "Восстанови pending-turn snapshot authority и повтори item materialization без ручного изменения receipt/index.");

    private static string ResolveCarrierFilePath(string issuePath)
    {
        foreach (var path in new[]
                 {
                     InventoryEquipmentService.ItemsPath,
                     NpcCoreChangesContract.NpcCorePath,
                     "game_state/npcs/npc_inventory.json",
                     StorageTransportMoveService.CurrentLocationPath,
                     StorageTransportMoveService.VehiclesPath,
                     MortalItemIdentityState.StatePath
                 })
        {
            if (issuePath.StartsWith(path, StringComparison.Ordinal))
                return path;
        }

        return InventoryEquipmentService.ItemsPath;
    }

    private sealed record MortalItemCatalogFiles(
        MortalItemCarrierCatalog Catalog,
        string? IdentityIndexJson);
}
