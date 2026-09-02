using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record CanonicalResourceOwnerAuthorityResult(
    ResourceOwnerAuthority? Authority,
    IReadOnlyList<ResourceOwnerCapacityDraft> CapacityDrafts,
    string? CanonicalAuthorityJson,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Authority != null && Issues.Count == 0;
}

internal enum CanonicalResourceOwnerAuthorityPurpose
{
    ExistingSessionValidation,
    FinalAfterImage,
    ExplicitBootstrap
}

internal static class CanonicalResourceOwnerAuthorityComposer
{
    internal const string AuthorityPath =
        "game_state/resources/resource_owner_authority.json";

    private static readonly string[] MortalItemCompanionPaths =
    {
        "game_state/inventory/item_bonds.json",
        "game_state/inventory/item_text_updates.json",
        "game_state/inventory/recipes.json",
        "game_state/npcs/item_journals.json",
        "game_state/quests/quest_history.json"
    };

    internal static readonly IReadOnlyList<string> SourceAuthorityPaths =
        new[]
        {
            InventoryEquipmentService.ItemsPath,
            NpcCoreChangesContract.NpcCorePath,
            StorageTransportMoveService.CurrentLocationPath,
            StorageTransportMoveService.VehiclesPath,
            MortalLocationStorageContentsState.StatePath,
            MortalItemIdentityState.StatePath,
            EffectCarrierCatalog.EnemiesPath,
            EffectCarrierCatalog.AlliesPath,
            AfterlifeEntityProfileState.StatePath,
            AfterlifeSpiritualConflictState.StatePath,
            "game_state/meta/soul_state.json",
            ShiningAbodeState.StatePath,
            "game_state/meta/guardians.json"
        }
        .Concat(MortalItemCompanionPaths)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(static path => path, StringComparer.Ordinal)
        .ToArray();

    internal static async Task<CanonicalResourceOwnerAuthorityResult> ComposeAsync(
        ResourceDefinitionCatalog definitions,
        Func<string, Task<string?>> readDocumentAsync,
        ResourceStateLedger? state,
        ResourceHistoryState? history,
        CanonicalResourceOwnerAuthorityPurpose purpose,
        IReadOnlyList<ResourceOwnerKey>? additionalHistoricalOwners = null,
        IReadOnlyDictionary<string, string>? sameTurnItemRefs = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(readDocumentAsync);

        var issues = new List<ValidationIssue>();
        var documents = new Dictionary<string, string?>(StringComparer.Ordinal);

        async Task<string?> ReadAsync(string path)
        {
            if (!documents.TryGetValue(path, out var json))
            {
                json = await readDocumentAsync(path);
                documents.Add(path, json);
            }
            return json;
        }

        var inventory = ParseOptionalObject(
            await ReadAsync(InventoryEquipmentService.ItemsPath),
            InventoryEquipmentService.ItemsPath,
            issues);
        var npcCore = ParseOptionalObject(
            await ReadAsync(NpcCoreChangesContract.NpcCorePath),
            NpcCoreChangesContract.NpcCorePath,
            issues);
        var currentLocation = ParseOptionalObject(
            await ReadAsync(StorageTransportMoveService.CurrentLocationPath),
            StorageTransportMoveService.CurrentLocationPath,
            issues);
        var vehicles = ParseOptionalVehicles(
            await ReadAsync(StorageTransportMoveService.VehiclesPath),
            StorageTransportMoveService.VehiclesPath,
            issues);
        var offscreenStorage = ParseOptionalObject(
            await ReadAsync(MortalLocationStorageContentsState.StatePath),
            MortalLocationStorageContentsState.StatePath,
            issues);
        var companions = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var path in MortalItemCompanionPaths)
        {
            var root = ParseOptionalObject(await ReadAsync(path), path, issues);
            if (root != null)
                companions.Add(path, root);
        }

        var items = MortalItemCarrierCatalog.Build(new MortalItemCarrierCatalogInput(
            inventory,
            npcCore,
            NpcInventoryCommands: null,
            currentLocation,
            vehicles,
            companions,
            offscreenStorage));
        foreach (var issue in items.Issues)
        {
            issues.Add(new ValidationIssue(
                issue.Path,
                IssueSeverity.Error,
                issue.Message,
                code: issue.Code,
                actor: issue.Identity == null
                    ? "mortal_item:unknown"
                    : $"mortal_item:existing:{issue.Identity}",
                section: "ResourceMaterialization",
                expected: "one exact unambiguous canonical item owner",
                actual: issue.Identity ?? "missing",
                repairTargetFiles: new[] { issue.Path }));
        }
        var historicalItemOwners = DeriveHistoricalItemOwners(
            await ReadAsync(MortalItemIdentityState.StatePath),
            issues);

        var mortalRoots = new MortalResourceOwnerRoots(
            npcCore ?? EmptyCollection("NPCsInScene"),
            ParseOwnerRoot(
                await ReadAsync(EffectCarrierCatalog.EnemiesPath),
                EffectCarrierCatalog.EnemiesPath,
                static () => EmptyCollection("enemiesData"),
                issues),
            ParseOwnerRoot(
                await ReadAsync(EffectCarrierCatalog.AlliesPath),
                EffectCarrierCatalog.AlliesPath,
                static () => EmptyCollection("alliesData"),
                issues),
            vehicles ?? EmptyCollection("vehicles"));
        var afterlifeRoots = new AfterlifeResourceOwnerRoots(
            ParseOwnerRoot(
                await ReadAsync(AfterlifeEntityProfileState.StatePath),
                AfterlifeEntityProfileState.StatePath,
                AfterlifeEntityProfileState.CreateDefaultRoot,
                issues),
            ParseOwnerRoot(
                await ReadAsync(AfterlifeSpiritualConflictState.StatePath),
                AfterlifeSpiritualConflictState.StatePath,
                AfterlifeSpiritualConflictState.CreateDefaultRoot,
                issues),
            ParseOwnerRoot(
                await ReadAsync("game_state/meta/soul_state.json"),
                "game_state/meta/soul_state.json",
                static () => new JsonObject(),
                issues),
            ParseOwnerRoot(
                await ReadAsync(ShiningAbodeState.StatePath),
                ShiningAbodeState.StatePath,
                ShiningAbodeState.CreateDefaultState,
                issues),
            ParseOwnerRoot(
                await ReadAsync("game_state/meta/guardians.json"),
                "game_state/meta/guardians.json",
                static () => new JsonObject(),
                issues));

        if (issues.Count != 0)
            return Invalid(issues);

        var mortal = sameTurnItemRefs is { Count: > 0 }
            ? ComposeCanonicalWithSameTurnItems(
                definitions,
                mortalRoots,
                items,
                sameTurnItemRefs,
                issues)
            : MortalResourceOwnerComposer.ComposeCanonical(
                definitions,
                mortalRoots,
                items);
        if (issues.Count != 0)
            return Invalid(issues);
        var afterlife = AfterlifeResourceOwnerComposer.Compose(
            new AfterlifeResourceOwnerCompositionInput(
                definitions,
                afterlifeRoots,
                afterlifeRoots));
        var combined = ResourceOwnerComposition.Combine(mortal, afterlife);
        if (combined.Authority == null || combined.Issues.Count != 0)
            return Invalid(combined.Issues);

        var historicalOwners = history == null
            ? Array.Empty<ResourceOwnerKey>()
            : DeriveHistoricalOwners(history);
        var exported = combined.Authority.ExportInput();
        var authority = ResourceOwnerAuthority.Build(new ResourceOwnerAuthorityInput(
            exported.PreTurnOwners,
            exported.SameTurnOwners,
            exported.HistoricalOwners
                .Concat(historicalOwners)
                .Concat(historicalItemOwners)
                .Concat(additionalHistoricalOwners ?? Array.Empty<ResourceOwnerKey>())
                .Distinct()
                .ToArray()));
        if (authority.Issues.Count != 0)
            return Invalid(authority.Issues);

        if (state != null && history != null)
        {
            ValidateCapacityDraftAgreement(
                combined.CapacityDrafts,
                state,
                history,
                issues);
            var canonicalJson = CreateCanonicalAuthorityJson(
                authority,
                state,
                history);
            var persistedJson = purpose ==
                                CanonicalResourceOwnerAuthorityPurpose.FinalAfterImage ||
                                purpose ==
                                CanonicalResourceOwnerAuthorityPurpose.ExplicitBootstrap
                ? null
                : await ReadAsync(AuthorityPath);
            if ((purpose == CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation &&
                 persistedJson == null) ||
                (persistedJson != null &&
                 (!TryParseStrictObject(persistedJson, out var persistedRoot) ||
                  !JsonNode.DeepEquals(
                      persistedRoot,
                      JsonNode.Parse(canonicalJson)!.AsObject()))))
            {
                issues.Add(new ValidationIssue(
                    AuthorityPath,
                    IssueSeverity.Error,
                    "Persisted resource owner authority differs from the exact recomposed authority.",
                    code: "resource_owner_authority_root_stale",
                    section: "ResourceMaterialization",
                    expected: "exact recomposed historical owners and capacity authority",
                    actual: "missing, forged, or stale authority content",
                    repairTargetFiles: new[] { AuthorityPath }));
            }
            if (issues.Count != 0)
                return Invalid(issues);
            return new CanonicalResourceOwnerAuthorityResult(
                authority,
                combined.CapacityDrafts,
                canonicalJson,
                Array.Empty<ValidationIssue>());
        }

        return new CanonicalResourceOwnerAuthorityResult(
            authority,
            combined.CapacityDrafts,
            null,
            Array.Empty<ValidationIssue>());
    }

    private static ResourceOwnerCompositionResult ComposeCanonicalWithSameTurnItems(
        ResourceDefinitionCatalog definitions,
        MortalResourceOwnerRoots roots,
        MortalItemCarrierCatalog items,
        IReadOnlyDictionary<string, string> sameTurnItemRefs,
        ICollection<ValidationIssue> issues)
    {
        var acceptedItems = new List<MortalItemAcceptedTurnOwner>();
        var acceptedRefs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in sameTurnItemRefs.OrderBy(
                     static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            if (!ResourceMaterializationContract.IsExactIdentifier(pair.Key) ||
                !ResourceMaterializationContract.IsExactIdentifier(pair.Value) ||
                !acceptedRefs.Add(pair.Value))
            {
                issues.Add(new ValidationIssue(
                    MortalItemIdentityState.StatePath,
                    IssueSeverity.Error,
                    "Canonical same-turn item owner has ambiguous accepted authority.",
                    code: "resource_owner_item_same_turn_authority_invalid",
                    section: "ResourceMaterialization",
                    expected: "one exact unique itemId -> itemRef authority mapping",
                    actual: $"{pair.Key} -> {pair.Value}",
                    repairTargetFiles: new[] { MortalItemIdentityState.StatePath }));
            }
        }

        var seenSameTurnItemIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var occurrence in items.Occurrences.OrderBy(
                     static value => value.JsonPath,
                     StringComparer.Ordinal))
        {
            if (!ResourceMaterializationContract.IsExactIdentifier(
                    occurrence.ItemId))
            {
                issues.Add(new ValidationIssue(
                    occurrence.JsonPath,
                    IssueSeverity.Error,
                    "Canonical item owner has no exact sealed identity.",
                    code: "resource_owner_item_canonical_identity_missing",
                    section: "ResourceMaterialization",
                    expected: "one exact sealed itemId",
                    actual: occurrence.ItemId ?? occurrence.CreationRef ?? "missing",
                    repairTargetFiles: new[] { occurrence.FilePath }));
                continue;
            }

            var sameTurn = sameTurnItemRefs.TryGetValue(
                occurrence.ItemId!,
                out var itemRef);
            if (sameTurn &&
                !seenSameTurnItemIds.Add(occurrence.ItemId!))
            {
                issues.Add(new ValidationIssue(
                    occurrence.JsonPath,
                    IssueSeverity.Error,
                    "Canonical same-turn item owner has ambiguous accepted authority.",
                    code: "resource_owner_item_same_turn_authority_invalid",
                    section: "ResourceMaterialization",
                    expected: "one exact unique accepted itemRef",
                    actual: itemRef ?? "missing",
                    repairTargetFiles: new[] { occurrence.FilePath }));
                continue;
            }

            acceptedItems.Add(new MortalItemAcceptedTurnOwner(
                occurrence.ItemId!,
                sameTurn ? itemRef : null,
                occurrence.FilePath,
                occurrence.JsonPath,
                occurrence.Carrier with
                {
                    ContainerPath = occurrence.Carrier.ContainerPath.ToArray()
                },
                occurrence.Item.DeepClone().AsObject(),
                sameTurn));
        }

        foreach (var missingItemId in sameTurnItemRefs.Keys
                     .Except(seenSameTurnItemIds, StringComparer.Ordinal)
                     .OrderBy(static value => value, StringComparer.Ordinal))
        {
            issues.Add(new ValidationIssue(
                MortalItemIdentityState.StatePath,
                IssueSeverity.Error,
                "Canonical same-turn item authority has no exact materialized owner.",
                code: "resource_owner_item_same_turn_authority_missing",
                section: "ResourceMaterialization",
                expected: "one exact materialized item owner",
                actual: missingItemId,
                repairTargetFiles: new[] { MortalItemIdentityState.StatePath }));
        }

        return issues.Count == 0
            ? MortalResourceOwnerComposer.Compose(
                new MortalResourceOwnerCompositionInput(
                    definitions,
                    roots,
                    roots,
                    acceptedItems: acceptedItems))
            : new ResourceOwnerCompositionResult(
                null,
                new Dictionary<string, JsonObject>(StringComparer.Ordinal),
                Array.Empty<AcceptedMechanicsOwnerTransition>(),
                null,
                Array.Empty<ResourceOwnerCapacityDraft>(),
                Array.Empty<ResourceOwnerKey>(),
                issues.ToArray());
    }

    private static void ValidateCapacityDraftAgreement(
        IReadOnlyList<ResourceOwnerCapacityDraft> drafts,
        ResourceStateLedger state,
        ResourceHistoryState history,
        List<ValidationIssue> issues)
    {
        foreach (var draft in drafts)
        {
            ResourceStateSnapshot? snapshot = state.TryResolveExact(
                draft.Coordinate,
                out var entry)
                ? entry!.Snapshot
                : history.Transitions
                    .Where(transition => transition.Coordinate == draft.Coordinate)
                    .OrderBy(static transition => transition.Turn)
                    .ThenBy(static transition => transition.ExecutionSequence)
                    .LastOrDefault()?.BeforeState;
            var resolved = draft.ResolvedCapacity.Capacity;
            if (snapshot == null || resolved == null ||
                snapshot.Maximum != draft.AcceptedMaximum ||
                resolved.Maximum != draft.AcceptedMaximum ||
                snapshot.CapacityBinding != resolved.Binding)
            {
                issues.Add(new ValidationIssue(
                    AuthorityPath + ".capacityDrafts",
                    IssueSeverity.Error,
                    "Composed resource owner capacity draft differs from canonical state/history.",
                    code: "resource_owner_capacity_draft_stale",
                    section: "ResourceMaterialization",
                    expected: "exact accepted maximum and capacity binding authority fingerprint",
                    actual: DescribeCoordinate(draft.Coordinate),
                    repairTargetFiles: new[]
                    {
                        AuthorityPath,
                        ResourceMaterializationContract.StatePath,
                        ResourceMaterializationContract.HistoryPath
                    }));
            }
        }
    }

    internal static string CreateCanonicalAuthorityJson(
        ResourceOwnerAuthority authority,
        ResourceStateLedger state,
        ResourceHistoryState history)
    {
        var exported = authority.ExportInput();
        var historicalOwners = exported.HistoricalOwners
            .Concat(DeriveHistoricalOwners(history))
            .Distinct()
            .ToArray();
        var snapshots = state.Entries.ToDictionary(
            static entry => entry.Coordinate,
            static entry => entry.Snapshot,
            ResourceCoordinateComparer.Instance);
        foreach (var transition in history.Transitions
                     .Where(static transition =>
                         transition.Operation == ResourceTransitionOperation.Retire &&
                         transition.BeforeState != null)
                     .OrderBy(static transition => transition.Turn)
                     .ThenBy(static transition => transition.ExecutionSequence))
        {
            snapshots[transition.Coordinate] = transition.BeforeState!;
        }

        var root = new JsonObject
        {
            ["schemaVersion"] = ResourceMaterializationContract.SchemaVersion,
            ["historicalOwners"] = new JsonArray(historicalOwners
                .OrderBy(static owner => owner.Realm, StringComparer.Ordinal)
                .ThenBy(static owner => owner.OwnerKind)
                .ThenBy(static owner => owner.ResourceOwnerId, StringComparer.Ordinal)
                .Select(owner => (JsonNode)new JsonObject
                {
                    ["realm"] = owner.Realm,
                    ["ownerKind"] = ResourceDefinitionCatalog.GetOwnerKindToken(owner.OwnerKind),
                    ["resourceOwnerId"] = owner.ResourceOwnerId
                }).ToArray()),
            ["capacityDrafts"] = new JsonArray(snapshots
                .OrderBy(static pair => pair.Key.Realm, StringComparer.Ordinal)
                .ThenBy(static pair => pair.Key.OwnerKind)
                .ThenBy(static pair => pair.Key.ResourceOwnerId, StringComparer.Ordinal)
                .ThenBy(static pair => pair.Key.ResourceKey, StringComparer.Ordinal)
                .Select(pair => (JsonNode)new JsonObject
                {
                    ["realm"] = pair.Key.Realm,
                    ["ownerKind"] = ResourceDefinitionCatalog.GetOwnerKindToken(pair.Key.OwnerKind),
                    ["resourceOwnerId"] = pair.Key.ResourceOwnerId,
                    ["resourceKey"] = pair.Key.ResourceKey,
                    ["acceptedMaximum"] = pair.Value.Maximum,
                    ["capacityBinding"] = new JsonObject
                    {
                        ["kind"] = ResourceStateContract.GetCapacityKindToken(
                            pair.Value.CapacityBinding.Kind),
                        ["authorityKey"] = pair.Value.CapacityBinding.AuthorityKey,
                        ["authorityFingerprint"] =
                            pair.Value.CapacityBinding.AuthorityFingerprint
                    }
                }).ToArray())
        };
        return root.ToJsonString();
    }

    private static ResourceOwnerKey[] DeriveHistoricalOwners(
        ResourceHistoryState history) =>
        history.Transitions
            .GroupBy(
                static transition => new ResourceOwnerKey(
                    transition.Coordinate.Realm,
                    transition.Coordinate.OwnerKind,
                    transition.Coordinate.ResourceOwnerId))
            .Where(static ownerGroup => ownerGroup
                .GroupBy(static transition => transition.Coordinate)
                .All(static coordinateGroup => coordinateGroup
                    .OrderBy(static transition => transition.Turn)
                    .ThenBy(static transition => transition.ExecutionSequence)
                    .Last().Operation == ResourceTransitionOperation.Retire))
            .Select(static group => group.Key)
            .Distinct()
            .ToArray();

    private static ResourceOwnerKey[] DeriveHistoricalItemOwners(
        string? identityJson,
        ICollection<ValidationIssue> issues)
    {
        if (identityJson is null)
            return Array.Empty<ResourceOwnerKey>();
        var parsed = MortalItemIdentityState.Parse(identityJson);
        if (parsed.Issues.Count != 0)
        {
            foreach (var issue in parsed.Issues)
                issues.Add(issue);
            return Array.Empty<ResourceOwnerKey>();
        }
        return parsed.EntriesByItemId
            .Where(static pair =>
                pair.Value["state"]?.GetValue<string>() is
                    "merged" or "consumed" or "destroyed")
            .Select(static pair => new ResourceOwnerKey(
                "mortal_world",
                ResourceOwnerKind.Item,
                pair.Key))
            .Distinct()
            .ToArray();
    }

    private static string DescribeCoordinate(ResourceCoordinate coordinate) =>
        $"{coordinate.Realm}/" +
        $"{ResourceDefinitionCatalog.GetOwnerKindToken(coordinate.OwnerKind)}/" +
        $"{coordinate.ResourceOwnerId}/{coordinate.ResourceKey}";

    private static JsonObject? ParseOptionalObject(
        string? json,
        string path,
        List<ValidationIssue> issues)
    {
        if (json == null)
            return null;
        return TryParseStrictObject(json, out var root)
            ? root
            : AddInvalidRoot(path, issues);
    }

    private static JsonObject? ParseOptionalVehicles(
        string? json,
        string path,
        List<ValidationIssue> issues)
    {
        var parsed = MortalItemProjectionRootParser.Parse(json, path);
        if (!parsed.IsValid)
            return AddInvalidRoot(path, issues);
        return MortalItemProjectionRootParser.ToCarrierCatalogObject(
            parsed.Root,
            path);
    }

    private static JsonObject ParseOwnerRoot(
        string? json,
        string path,
        Func<JsonObject> missingFactory,
        List<ValidationIssue> issues)
    {
        if (json == null)
            return missingFactory();
        return TryParseStrictObject(json, out var root)
            ? root
            : AddInvalidRoot(path, issues) ?? missingFactory();
    }

    private static JsonObject? AddInvalidRoot(
        string path,
        List<ValidationIssue> issues)
    {
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Canonical resource owner root must be one strict JSON object.",
            code: "resource_owner_canonical_root_invalid",
            section: "ResourceMaterialization",
            expected: "strict object root or proven absence",
            actual: "malformed, duplicate-property, or non-object root",
            repairTargetFiles: new[] { path }));
        return null;
    }

    private static bool TryParseStrictObject(string json, out JsonObject root)
    {
        root = new JsonObject();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;
            var duplicateIssues = new List<ValidationIssue>();
            ResourceMaterializationContract.FindDuplicateProperties(
                document.RootElement,
                "resourceOwnerRoot",
                duplicateIssues,
                "resource_owner_duplicate_property");
            if (duplicateIssues.Count != 0)
                return false;
            root = JsonNode.Parse(document.RootElement.GetRawText())!.AsObject();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static JsonObject EmptyCollection(string property) => new()
    {
        [property] = new JsonArray()
    };

    private static CanonicalResourceOwnerAuthorityResult Invalid(
        IEnumerable<ValidationIssue> issues) =>
        new(
            null,
            Array.Empty<ResourceOwnerCapacityDraft>(),
            null,
            issues.ToArray());
}
