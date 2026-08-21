using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed class MortalResourceOwnerRoots
{
    private readonly JsonObject _npcCore;
    private readonly JsonObject _enemyCombatants;
    private readonly JsonObject _allyCombatants;
    private readonly JsonObject _vehicles;

    internal MortalResourceOwnerRoots(
        JsonObject? npcCore,
        JsonObject? enemyCombatants,
        JsonObject? allyCombatants,
        JsonObject? vehicles)
    {
        _npcCore = CloneOrEmpty(npcCore);
        _enemyCombatants = CloneOrEmpty(enemyCombatants);
        _allyCombatants = CloneOrEmpty(allyCombatants);
        _vehicles = CloneOrEmpty(vehicles);
    }

    internal JsonObject NpcCore => _npcCore.DeepClone().AsObject();

    internal JsonObject EnemyCombatants => _enemyCombatants.DeepClone().AsObject();

    internal JsonObject AllyCombatants => _allyCombatants.DeepClone().AsObject();

    internal JsonObject Vehicles => _vehicles.DeepClone().AsObject();

    private static JsonObject CloneOrEmpty(JsonObject? value) =>
        value?.DeepClone().AsObject() ?? new JsonObject();
}

internal sealed class MortalResourceOwnerCompositionInput
{
    private readonly Dictionary<ResourceOwnerKind, string[]> _sameTurnCapabilities;
    private readonly MortalItemAcceptedTurnOwner[] _acceptedItems;
    private readonly HashSet<string> _missingGovernedItemIds;

    internal MortalResourceOwnerCompositionInput(
        ResourceDefinitionCatalog definitions,
        MortalResourceOwnerRoots preTurn,
        MortalResourceOwnerRoots accepted,
        IReadOnlyDictionary<ResourceOwnerKind, IReadOnlyList<string>>? sameTurnCapabilities = null,
        IReadOnlyList<MortalItemAcceptedTurnOwner>? acceptedItems = null,
        IReadOnlySet<string>? missingGovernedItemIds = null)
    {
        Definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        PreTurn = preTurn ?? throw new ArgumentNullException(nameof(preTurn));
        Accepted = accepted ?? throw new ArgumentNullException(nameof(accepted));
        _sameTurnCapabilities = sameTurnCapabilities?.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value
                .Where(ResourceMaterializationContract.IsExactIdentifier)
                .Distinct(StringComparer.Ordinal)
                 .OrderBy(static value => value, StringComparer.Ordinal)
                 .ToArray()) ?? new Dictionary<ResourceOwnerKind, string[]>();
        _acceptedItems = acceptedItems?.Select(static owner => owner with
        {
            Carrier = owner.Carrier with
            {
                ContainerPath = owner.Carrier.ContainerPath.ToArray()
            },
            Item = owner.Item.DeepClone().AsObject()
        }).ToArray() ?? Array.Empty<MortalItemAcceptedTurnOwner>();
        _missingGovernedItemIds = missingGovernedItemIds == null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(missingGovernedItemIds, StringComparer.Ordinal);
    }

    internal ResourceDefinitionCatalog Definitions { get; }

    internal MortalResourceOwnerRoots PreTurn { get; }

    internal MortalResourceOwnerRoots Accepted { get; }

    internal IReadOnlyList<string> GetSameTurnCapabilities(ResourceOwnerKind ownerKind) =>
        _sameTurnCapabilities.TryGetValue(ownerKind, out var capabilities)
            ? Array.AsReadOnly(capabilities.ToArray())
            : Array.Empty<string>();

    internal IReadOnlyList<MortalItemAcceptedTurnOwner> AcceptedItems =>
        Array.AsReadOnly(_acceptedItems.Select(static owner => owner with
        {
            Carrier = owner.Carrier with
            {
                ContainerPath = owner.Carrier.ContainerPath.ToArray()
            },
            Item = owner.Item.DeepClone().AsObject()
        }).ToArray());

    internal IReadOnlySet<string> MissingGovernedItemIds =>
        new HashSet<string>(_missingGovernedItemIds, StringComparer.Ordinal);
}

internal sealed record ResourceOwnerCompositionResult(
    ResourceOwnerAuthority? Authority,
    IReadOnlyDictionary<string, JsonObject> OwnerCompanionAfterImages,
    IReadOnlyList<AcceptedMechanicsOwnerTransition> OwnerTransitions,
    CombatantIdentityState? CombatantIdentities,
    IReadOnlyList<ResourceOwnerCapacityDraft> CapacityDrafts,
    IReadOnlyList<ResourceOwnerKey> TerminalOwners,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Authority != null && Issues.Count == 0;
}

internal sealed record ResourceOwnerCapacityDraft(
    ResourceCoordinate Coordinate,
    decimal AcceptedMaximum,
    ResolvedResourceCapacityResult ResolvedCapacity,
    ResourceSourceEvidence SourceEvidence);

internal static class MortalResourceOwnerComposer
{
    private const string Realm = "mortal_world";

    internal static ResourceOwnerCompositionResult ComposeCanonical(
        ResourceDefinitionCatalog definitions,
        MortalResourceOwnerRoots roots,
        MortalItemCarrierCatalog items)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(items);
        var issues = new List<ValidationIssue>();
        var itemOwners = new List<MortalItemAcceptedTurnOwner>();
        foreach (var occurrence in items.Occurrences.OrderBy(
                     static value => value.JsonPath,
                     StringComparer.Ordinal))
        {
            if (!ResourceMaterializationContract.IsExactIdentifier(occurrence.ItemId))
            {
                Add(
                    issues,
                    occurrence.JsonPath,
                    "resource_owner_item_canonical_identity_missing",
                    "one exact sealed itemId on every canonical item owner",
                    occurrence.ItemId ?? occurrence.CreationRef ?? "missing");
                continue;
            }

            itemOwners.Add(new MortalItemAcceptedTurnOwner(
                occurrence.ItemId!,
                ItemRef: null,
                occurrence.FilePath,
                occurrence.JsonPath,
                occurrence.Carrier with
                {
                    ContainerPath = occurrence.Carrier.ContainerPath.ToArray()
                },
                occurrence.Item.DeepClone().AsObject(),
                SameTurn: false));
        }

        if (issues.Count != 0)
            return Invalid(issues);

        return Compose(new MortalResourceOwnerCompositionInput(
            definitions,
            roots,
            roots,
            acceptedItems: itemOwners));
    }

    internal static ResourceOwnerCompositionResult Compose(
        MortalResourceOwnerCompositionInput input) =>
        Compose(input, new CombatantIdentityFactory(), new VehicleIdentityFactory());

    internal static ResourceOwnerCompositionResult Compose(
        MortalResourceOwnerCompositionInput input,
        CombatantIdentityFactory identityFactory) =>
        Compose(input, identityFactory, new VehicleIdentityFactory());

    internal static ResourceOwnerCompositionResult Compose(
        MortalResourceOwnerCompositionInput input,
        CombatantIdentityFactory identityFactory,
        VehicleIdentityFactory vehicleIdentityFactory)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(identityFactory);
        ArgumentNullException.ThrowIfNull(vehicleIdentityFactory);
        var issues = new List<ValidationIssue>();
        var preTurnExports = new List<ResourceOwnerExport>
        {
            CreateExport(
                input.Definitions,
                ResourceOwnerKind.Player,
                "player_current")
        };
        var sameTurnExports = new List<ResourceOwnerExport>();
        var historicalOwners = new List<ResourceOwnerKey>();
        var materializationCandidates = new List<ResourceOwnerMaterializationCandidate>();
        var companionAfterImages = new Dictionary<string, JsonObject>(
            StringComparer.Ordinal);
        var ownerTransitions = new List<AcceptedMechanicsOwnerTransition>();
        var preTurnNpcCore = input.PreTurn.NpcCore;
        var npcCore = input.Accepted.NpcCore;
        var preTurnEnemies = input.PreTurn.EnemyCombatants;
        var enemies = input.Accepted.EnemyCombatants;
        var preTurnAllies = input.PreTurn.AllyCombatants;
        var allies = input.Accepted.AllyCombatants;
        var preTurnVehicles = input.PreTurn.Vehicles;
        var vehicles = input.Accepted.Vehicles;

        var sameTurnNpcIdsByRef = new Dictionary<string, string>(StringComparer.Ordinal);
        var npcIds = ComposeNpcOwners(
            preTurnNpcCore,
            npcCore,
            input.Definitions,
            preTurnExports,
            sameTurnExports,
            sameTurnNpcIdsByRef,
            ownerTransitions,
            materializationCandidates,
            issues);
        ResolveSameTurnNamedCombatantRefs(
            enemies,
            "enemiesData",
            sameTurnNpcIdsByRef,
            issues);
        ResolveSameTurnNamedCombatantRefs(
            allies,
            "alliesData",
            sameTurnNpcIdsByRef,
            issues);
        var claimedNpcIds = new HashSet<string>(StringComparer.Ordinal);
        ValidateNamedCombatantBindings(
            enemies,
            "enemiesData",
            npcIds,
            claimedNpcIds,
            issues);
        ValidateNamedCombatantBindings(
            allies,
            "alliesData",
            npcIds,
            claimedNpcIds,
            issues);

        if (issues.Count != 0)
            return Invalid(issues);

        ComposeVehicleOwners(
            preTurnVehicles,
            vehicles,
            input.Definitions,
            preTurnExports,
            sameTurnExports,
            historicalOwners,
            companionAfterImages,
            vehicleIdentityFactory,
            materializationCandidates,
            issues);
        if (issues.Count != 0)
            return Invalid(issues);

        ComposeItemOwners(
            input.AcceptedItems,
            input.Definitions,
            preTurnExports,
            sameTurnExports,
            materializationCandidates,
            issues);
        if (issues.Count != 0)
            return Invalid(issues);

        ComposeCombatOwners(
            preTurnEnemies,
            enemies,
            preTurnAllies,
            allies,
            input.Definitions,
            identityFactory,
            preTurnExports,
            sameTurnExports,
            historicalOwners,
            companionAfterImages,
            materializationCandidates,
            issues,
            out var combatantIdentities);
        if (issues.Count != 0)
            return Invalid(issues);

        ApplySameTurnCapabilities(input, preTurnExports);
        ApplySameTurnCapabilities(input, sameTurnExports);

        var capacityDrafts = ResourceOwnerMaterializationPlanner.ComposeCapacityDrafts(
            input.Definitions,
            sameTurnExports,
            materializationCandidates,
            issues);
        if (issues.Count != 0)
            return Invalid(issues);

        var authority = ResourceOwnerAuthority.Build(new ResourceOwnerAuthorityInput(
            preTurnExports,
            sameTurnExports,
            historicalOwners));
        if (authority.Issues.Count != 0)
            return Invalid(authority.Issues);

        return new ResourceOwnerCompositionResult(
            authority,
            new ReadOnlyDictionary<string, JsonObject>(
                companionAfterImages),
            Array.AsReadOnly(ownerTransitions.Select(static value => value.Clone()).ToArray()),
            combatantIdentities,
            Array.AsReadOnly(capacityDrafts.ToArray()),
            Array.AsReadOnly(historicalOwners.ToArray()),
            Array.Empty<ValidationIssue>());
    }

    private static void ComposeItemOwners(
        IReadOnlyList<MortalItemAcceptedTurnOwner> acceptedItems,
        ResourceDefinitionCatalog definitions,
        List<ResourceOwnerExport> preTurnExports,
        List<ResourceOwnerExport> sameTurnExports,
        List<ResourceOwnerMaterializationCandidate> materializationCandidates,
        List<ValidationIssue> issues)
    {
        var itemIds = new HashSet<string>(StringComparer.Ordinal);
        var itemAliases = new HashSet<string>(StringComparer.Ordinal);
        var refs = new HashSet<string>(StringComparer.Ordinal);
        var refAliases = new HashSet<string>(StringComparer.Ordinal);
        foreach (var owner in acceptedItems.OrderBy(
                     static value => value.ItemId,
                     StringComparer.Ordinal))
        {
            var path = owner.JsonPath;
            if (!ResourceMaterializationContract.IsExactIdentifier(owner.ItemId) ||
                !itemIds.Add(owner.ItemId) ||
                !itemAliases.Add(ResourceMaterializationContract.BuildConfusableKey(owner.ItemId)))
            {
                Add(
                    issues,
                    path,
                    "resource_owner_item_identity_ambiguous",
                    "one exact/confusable-unique client-owned itemId",
                    owner.ItemId);
                continue;
            }

            RejectLegacyResourceValues(
                owner.Item,
                path,
                new[] { "durability", "maxDurability" },
                issues);
            if (owner.SameTurn)
            {
                if (!ResourceMaterializationContract.IsExactIdentifier(owner.ItemRef) ||
                    !refs.Add(owner.ItemRef!) ||
                    !refAliases.Add(ResourceMaterializationContract.BuildConfusableKey(owner.ItemRef!)))
                {
                    Add(
                        issues,
                        path,
                        "resource_owner_item_ref_ambiguous",
                        "one exact/confusable-unique validated item creationRef",
                        owner.ItemRef ?? "missing");
                    continue;
                }

                var export = CreateExport(
                    definitions,
                    ResourceOwnerKind.Item,
                    owner.ItemId,
                    sameTurn: true,
                    ownerRef: owner.ItemRef);
                sameTurnExports.Add(export);
                if (owner.Item.ContainsKey("resourceMaterialization"))
                {
                    CaptureMaterializationCandidate(
                        owner.Item.DeepClone().AsObject(),
                        path,
                        export.Key,
                        owner.ItemRef!,
                        Array.Empty<string>(),
                        materializationCandidates,
                        issues);
                }
                continue;
            }

            if (owner.ItemRef != null)
            {
                Add(
                    issues,
                    path,
                    "resource_owner_item_ref_residual",
                    "no temporary item ref on an existing item owner",
                    owner.ItemRef);
                continue;
            }
            RejectExistingMaterialization(owner.Item, path, issues);
            preTurnExports.Add(CreateExport(
                definitions,
                ResourceOwnerKind.Item,
                owner.ItemId));
        }
    }

    private static void ComposeVehicleOwners(
        JsonObject preTurnRoot,
        JsonObject root,
        ResourceDefinitionCatalog definitions,
        List<ResourceOwnerExport> preTurnExports,
        List<ResourceOwnerExport> sameTurnExports,
        List<ResourceOwnerKey> historicalOwners,
        Dictionary<string, JsonObject> companionAfterImages,
        VehicleIdentityFactory identityFactory,
        List<ResourceOwnerMaterializationCandidate> materializationCandidates,
        List<ValidationIssue> issues)
    {
        if (preTurnRoot.TryGetPropertyValue("vehicles", out var preTurnVehiclesNode) &&
            preTurnVehiclesNode is not JsonArray)
        {
            Add(
                issues,
                StorageTransportMoveService.VehiclesPath + ".vehicles",
                "resource_owner_vehicle_collection_invalid",
                "canonical pre-turn vehicles array",
                Describe(preTurnVehiclesNode));
            return;
        }
        if (root.TryGetPropertyValue("vehicles", out var vehiclesNode) &&
            vehiclesNode is not JsonArray)
        {
            Add(
                issues,
                StorageTransportMoveService.VehiclesPath + ".vehicles",
                "resource_owner_vehicle_collection_invalid",
                "canonical vehicles array",
                Describe(vehiclesNode));
            return;
        }

        var preTurnVehicles = preTurnVehiclesNode as JsonArray ?? new JsonArray();
        var vehicles = vehiclesNode as JsonArray ?? new JsonArray();
        var finalVehicles = vehicles.DeepClone().AsArray();

        var preTurnIds = new HashSet<string>(StringComparer.Ordinal);
        var preTurnAliases = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < preTurnVehicles.Count; index++)
        {
            var path = $"{StorageTransportMoveService.VehiclesPath}.vehicles[{index}]";
            if (preTurnVehicles[index] is not JsonObject vehicle ||
                !TryReadExact(vehicle["vehicleId"], out var vehicleId))
            {
                Add(
                    issues,
                    path + ".vehicleId",
                    "resource_owner_vehicle_id_invalid",
                    "one exact permanent pre-turn vehicleId",
                    Describe(preTurnVehicles[index]?["vehicleId"]));
                continue;
            }
            if (vehicle.ContainsKey("vehicleRef") ||
                !preTurnIds.Add(vehicleId) ||
                !preTurnAliases.Add(
                    ResourceMaterializationContract.BuildConfusableKey(vehicleId)))
            {
                Add(
                    issues,
                    path + ".vehicleId",
                    vehicle.ContainsKey("vehicleRef")
                        ? "resource_owner_vehicle_ref_residual"
                        : "resource_owner_vehicle_identity_ambiguous",
                    "one exact/confusable-unique permanent pre-turn vehicleId and no vehicleRef",
                    vehicle.ToJsonString());
                continue;
            }
            RejectLegacyResourceValues(
                vehicle,
                path,
                new[] { "currentHealth", "maxHealth" },
                issues);
            RejectExistingMaterialization(vehicle, path, issues);
            preTurnExports.Add(CreateExport(
                definitions,
                ResourceOwnerKind.Vehicle,
                vehicleId));
        }
        if (issues.Count != 0)
            return;

        var exact = new HashSet<string>(StringComparer.Ordinal);
        var confusable = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < vehicles.Count; index++)
        {
            var path = $"game_state/misc/vehicles.json.vehicles[{index}]";
            if (vehicles[index] is not JsonObject vehicle ||
                !TryReadExact(vehicle["vehicleId"], out var vehicleId))
            {
                Add(
                    issues,
                    path + ".vehicleId",
                    "resource_owner_vehicle_id_invalid",
                    "one exact permanent vehicleId",
                    Describe(vehicles[index]?["vehicleId"]));
                continue;
            }

            if (!exact.Add(vehicleId) ||
                !confusable.Add(ResourceMaterializationContract.BuildConfusableKey(vehicleId)))
            {
                Add(
                    issues,
                    path + ".vehicleId",
                    "resource_owner_vehicle_identity_ambiguous",
                    "one exact/confusable-unique permanent vehicleId",
                    vehicleId);
                continue;
            }

            if (!preTurnIds.Contains(vehicleId))
            {
                Add(
                    issues,
                    path + ".vehicleId",
                    "resource_owner_vehicle_preassigned_id_forbidden",
                    "exact validated pre-turn vehicleId or same-turn vehicleRef",
                    vehicleId);
                continue;
            }

            if (vehicle.ContainsKey("vehicleRef"))
            {
                Add(
                    issues,
                    path + ".vehicleRef",
                    "resource_owner_vehicle_ref_residual",
                    "field absent from canonical vehicle state",
                    Describe(vehicle["vehicleRef"]));
                continue;
            }
            RejectLegacyResourceValues(
                vehicle,
                path,
                new[] { "currentHealth", "maxHealth" },
                issues);
            RejectExistingMaterialization(vehicle, path, issues);
        }

        foreach (var missingVehicleId in preTurnIds.Except(exact, StringComparer.Ordinal))
        {
            Add(
                issues,
                StorageTransportMoveService.VehiclesPath + ".vehicles",
                "resource_owner_vehicle_direct_removal_forbidden",
                "pre-turn vehicle retained until exact removeVehicles lifecycle command",
                missingVehicleId);
        }
        if (issues.Count != 0)
            return;

        if (root.TryGetPropertyValue("UpdateVehicles", out var updatesNode) &&
            updatesNode is not JsonArray)
        {
            Add(
                issues,
                StorageTransportMoveService.VehiclesPath + ".UpdateVehicles",
                "resource_owner_vehicle_updates_invalid",
                "accepted vehicle update array",
                Describe(updatesNode));
            return;
        }

        var refs = new HashSet<string>(StringComparer.Ordinal);
        var refAliases = new HashSet<string>(StringComparer.Ordinal);
        var updates = updatesNode as JsonArray;
        for (var index = 0; index < (updates?.Count ?? 0); index++)
        {
            var path = $"{StorageTransportMoveService.VehiclesPath}.UpdateVehicles[{index}]";
            if (updates![index] is not JsonObject update)
            {
                Add(
                    issues,
                    path,
                    "resource_owner_vehicle_update_invalid",
                    "vehicle creation/update object",
                    Describe(updates[index]));
                continue;
            }

            var hasRef = update.TryGetPropertyValue("vehicleRef", out var refNode) &&
                         refNode != null;
            var hasId = update.TryGetPropertyValue("vehicleId", out var idNode) &&
                        idNode != null;
            if (hasRef == hasId)
            {
                Add(
                    issues,
                    path,
                    "resource_owner_vehicle_identity_selector_invalid",
                    "exactly one vehicleRef for creation or vehicleId for update",
                    update.ToJsonString());
                continue;
            }

            if (hasRef)
            {
                if (!TryReadExact(refNode, out var vehicleRef) ||
                    !refs.Add(vehicleRef) ||
                    !refAliases.Add(ResourceMaterializationContract.BuildConfusableKey(vehicleRef)))
                {
                    Add(
                        issues,
                        path + ".vehicleRef",
                        "resource_owner_vehicle_ref_ambiguous",
                        "one exact/confusable-unique same-turn vehicleRef",
                        Describe(refNode));
                    continue;
                }

                var vehicleId = identityFactory.CreateVehicleId();
                if (!ResourceMaterializationContract.IsExactIdentifier(vehicleId) ||
                    !exact.Add(vehicleId) ||
                    !confusable.Add(ResourceMaterializationContract.BuildConfusableKey(vehicleId)))
                {
                    Add(
                        issues,
                        path + ".vehicleId",
                        "resource_owner_vehicle_generated_id_invalid",
                        "one exact/confusable-unique client-owned vehicleId",
                        vehicleId);
                    continue;
                }

                var created = update.DeepClone().AsObject();
                created.Remove("vehicleRef");
                created["vehicleId"] = vehicleId;
                finalVehicles.Add(created);
                var export = CreateExport(
                    definitions,
                    ResourceOwnerKind.Vehicle,
                    vehicleId,
                    sameTurn: true,
                    vehicleRef);
                sameTurnExports.Add(export);
                CaptureMaterializationCandidate(
                    created,
                    path,
                    export.Key,
                    vehicleRef,
                    new[] { "health" },
                    materializationCandidates,
                    issues);
                RejectLegacyResourceValues(
                    update,
                    path,
                    new[] { "currentHealth", "maxHealth" },
                    issues);
                continue;
            }

            if (!TryReadExact(idNode, out var existingId) || !exact.Contains(existingId))
            {
                Add(
                    issues,
                    path + ".vehicleId",
                    "resource_owner_vehicle_update_target_unresolved",
                    "one exact pre-turn permanent vehicleId",
                    Describe(idNode));
                continue;
            }
            RejectLegacyResourceValues(
                update,
                path,
                new[] { "currentHealth", "maxHealth" },
                issues);
            RejectExistingMaterialization(update, path, issues);

            var matches = finalVehicles
                .OfType<JsonObject>()
                .Where(vehicle =>
                    TryReadExact(vehicle["vehicleId"], out var candidateId) &&
                    string.Equals(candidateId, existingId, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                Add(
                    issues,
                    path + ".vehicleId",
                    "resource_owner_vehicle_update_target_ambiguous",
                    "one exact canonical vehicle",
                    existingId);
                continue;
            }
            foreach (var property in update)
            {
                if (!string.Equals(property.Key, "vehicleId", StringComparison.Ordinal))
                    matches[0][property.Key] = property.Value?.DeepClone();
            }
        }

        if (root.TryGetPropertyValue("removeVehicles", out var removalsNode) &&
            removalsNode is not JsonArray)
        {
            Add(
                issues,
                StorageTransportMoveService.VehiclesPath + ".removeVehicles",
                "resource_owner_vehicle_removals_invalid",
                "array of exact pre-turn vehicleId values",
                Describe(removalsNode));
            return;
        }

        var removed = new HashSet<string>(StringComparer.Ordinal);
        var removedAliases = new HashSet<string>(StringComparer.Ordinal);
        if (removalsNode is JsonArray removals)
        {
            for (var index = 0; index < removals.Count; index++)
            {
                var path = $"{StorageTransportMoveService.VehiclesPath}.removeVehicles[{index}]";
                if (!TryReadExact(removals[index], out var vehicleId) ||
                    !removed.Add(vehicleId) ||
                    !removedAliases.Add(ResourceMaterializationContract.BuildConfusableKey(vehicleId)))
                {
                    Add(
                        issues,
                        path,
                        "resource_owner_vehicle_removal_ambiguous",
                        "one exact/confusable-unique pre-turn vehicleId",
                        Describe(removals[index]));
                    continue;
                }

                var matches = finalVehicles
                    .Select((node, vehicleIndex) => (node, vehicleIndex))
                    .Where(candidate =>
                        candidate.node is JsonObject vehicle &&
                        TryReadExact(vehicle["vehicleId"], out var candidateId) &&
                        string.Equals(candidateId, vehicleId, StringComparison.Ordinal))
                    .ToArray();
                if (matches.Length != 1 ||
                    !preTurnExports.Any(export =>
                        export.Key.OwnerKind == ResourceOwnerKind.Vehicle &&
                        string.Equals(
                            export.Key.ResourceOwnerId,
                            vehicleId,
                            StringComparison.Ordinal)))
                {
                    Add(
                        issues,
                        path,
                        "resource_owner_vehicle_removal_target_unresolved",
                        "one exact pre-turn vehicleId",
                        vehicleId);
                    continue;
                }

                finalVehicles.RemoveAt(matches[0].vehicleIndex);
                preTurnExports.RemoveAll(export =>
                    export.Key.OwnerKind == ResourceOwnerKind.Vehicle &&
                    string.Equals(
                        export.Key.ResourceOwnerId,
                        vehicleId,
                        StringComparison.Ordinal));
                historicalOwners.Add(new ResourceOwnerKey(
                    Realm,
                    ResourceOwnerKind.Vehicle,
                    vehicleId));
            }
        }

        if (issues.Count != 0)
            return;

        if (updates == null && removalsNode == null)
            return;

        var afterImage = root.DeepClone().AsObject();
        afterImage.Remove("UpdateVehicles");
        afterImage.Remove("removeVehicles");
        afterImage["vehicles"] = finalVehicles;
        companionAfterImages[StorageTransportMoveService.VehiclesPath] = afterImage;
    }

    private static void ComposeCombatOwners(
        JsonObject preTurnEnemies,
        JsonObject enemies,
        JsonObject preTurnAllies,
        JsonObject allies,
        ResourceDefinitionCatalog definitions,
        CombatantIdentityFactory identityFactory,
        List<ResourceOwnerExport> preTurnExports,
        List<ResourceOwnerExport> sameTurnExports,
        List<ResourceOwnerKey> historicalOwners,
        Dictionary<string, JsonObject> companionAfterImages,
        List<ResourceOwnerMaterializationCandidate> materializationCandidates,
        List<ValidationIssue> issues,
        out CombatantIdentityState? combatantIdentities)
    {
        combatantIdentities = null;
        RejectCombatLegacyValues(
            preTurnEnemies,
            "enemiesData",
            EffectCarrierCatalog.EnemiesPath,
            issues);
        RejectCombatLegacyValues(
            preTurnAllies,
            "alliesData",
            EffectCarrierCatalog.AlliesPath,
            issues);
        RejectCombatLegacyValues(
            enemies,
            "enemiesData",
            EffectCarrierCatalog.EnemiesPath,
            issues);
        RejectCombatLegacyValues(
            allies,
            "alliesData",
            EffectCarrierCatalog.AlliesPath,
            issues);
        if (issues.Count != 0)
            return;

        var preTurnCombatants = new JsonArray();
        var preTurnMembers = new JsonArray();
        CollectCombatIdentityCandidates(
            preTurnEnemies,
            "enemiesData",
            preTurnCombatants,
            preTurnMembers,
            new List<(JsonArray Collection, int Index)>(),
            new List<(JsonArray Collection, int Index)>());
        CollectCombatIdentityCandidates(
            preTurnAllies,
            "alliesData",
            preTurnCombatants,
            preTurnMembers,
            new List<(JsonArray Collection, int Index)>(),
            new List<(JsonArray Collection, int Index)>());
        issues.AddRange(CombatantIdentityState.ValidateCanonical(
            preTurnCombatants,
            preTurnMembers));
        if (issues.Count != 0)
            return;

        var preTurnCombatantIds = ReadIdentitySet(
            preTurnCombatants,
            "combatantId");
        var preTurnMemberIds = ReadIdentitySet(
            preTurnMembers,
            "memberId");
        var combatants = new JsonArray();
        var members = new JsonArray();
        var combatantCoordinates = new List<(JsonArray Collection, int Index)>();
        var memberCoordinates = new List<(JsonArray Collection, int Index)>();

        CollectCombatIdentityCandidates(
            enemies,
            "enemiesData",
            combatants,
            members,
            combatantCoordinates,
            memberCoordinates);
        CollectCombatIdentityCandidates(
            allies,
            "alliesData",
            combatants,
            members,
            combatantCoordinates,
            memberCoordinates);

        var identityBuild = CombatantIdentityState.BuildNew(
            combatants,
            members,
            identityFactory);
        if (identityBuild.Issues.Count != 0 || identityBuild.State == null)
        {
            issues.AddRange(identityBuild.Issues);
            return;
        }
        combatantIdentities = identityBuild.State;

        for (var index = 0; index < combatantCoordinates.Count; index++)
        {
            var coordinate = combatantCoordinates[index];
            coordinate.Collection[coordinate.Index] =
                identityBuild.RewrittenCombatants[index]?.DeepClone();
        }
        for (var index = 0; index < memberCoordinates.Count; index++)
        {
            var coordinate = memberCoordinates[index];
            coordinate.Collection[coordinate.Index] =
                identityBuild.RewrittenMembers[index]?.DeepClone();
        }

        AddAnonymousCombatantExports(
            identityBuild.RewrittenCombatants,
            combatants,
            preTurnCombatantIds,
            definitions,
            preTurnExports,
            sameTurnExports,
            materializationCandidates,
            issues);
        AddGroupMemberExports(
            identityBuild.RewrittenMembers,
            members,
            preTurnMemberIds,
            definitions,
            preTurnExports,
            sameTurnExports,
            materializationCandidates,
            issues);
        if (issues.Count != 0)
            return;

        var currentCombatantIds = ReadIdentitySet(
            identityBuild.RewrittenCombatants,
            "combatantId");
        var currentMemberIds = ReadIdentitySet(
            identityBuild.RewrittenMembers,
            "memberId");
        foreach (var removedId in preTurnCombatantIds.Except(
                     currentCombatantIds,
                     StringComparer.Ordinal))
        {
            historicalOwners.Add(new ResourceOwnerKey(
                Realm,
                ResourceOwnerKind.Combatant,
                removedId));
        }
        foreach (var removedId in preTurnMemberIds.Except(
                     currentMemberIds,
                     StringComparer.Ordinal))
        {
            historicalOwners.Add(new ResourceOwnerKey(
                Realm,
                ResourceOwnerKind.CombatGroupMember,
                removedId));
        }

        StripMaterializationEnvelopes(enemies, "enemiesData");
        StripMaterializationEnvelopes(allies, "alliesData");

        if (!JsonNode.DeepEquals(preTurnEnemies, enemies))
            companionAfterImages[EffectCarrierCatalog.EnemiesPath] = enemies.DeepClone().AsObject();
        if (!JsonNode.DeepEquals(preTurnAllies, allies))
            companionAfterImages[EffectCarrierCatalog.AlliesPath] = allies.DeepClone().AsObject();
    }

    private static HashSet<string> ReadIdentitySet(
        JsonArray owners,
        string propertyName) =>
        owners
            .OfType<JsonObject>()
            .Select(owner => TryReadExact(owner[propertyName], out var value)
                ? value
                : null)
            .Where(static value => value != null)
            .Select(static value => value!)
            .ToHashSet(StringComparer.Ordinal);

    private static void CollectCombatIdentityCandidates(
        JsonObject root,
        string collectionName,
        JsonArray combatants,
        JsonArray members,
        List<(JsonArray Collection, int Index)> combatantCoordinates,
        List<(JsonArray Collection, int Index)> memberCoordinates)
    {
        if (root[collectionName] is not JsonArray collection)
            return;

        for (var index = 0; index < collection.Count; index++)
        {
            if (collection[index] is not JsonObject combatant || !IsGroup(combatant))
            {
                combatants.Add(collection[index]?.DeepClone());
                combatantCoordinates.Add((collection, index));
                continue;
            }

            if (combatant["members"] is not JsonArray groupMembers)
                continue;
            for (var memberIndex = 0; memberIndex < groupMembers.Count; memberIndex++)
            {
                members.Add(groupMembers[memberIndex]?.DeepClone());
                memberCoordinates.Add((groupMembers, memberIndex));
            }
        }
    }

    private static void AddAnonymousCombatantExports(
        JsonArray rewritten,
        JsonArray original,
        IReadOnlySet<string> preTurnIds,
        ResourceDefinitionCatalog definitions,
        List<ResourceOwnerExport> preTurnExports,
        List<ResourceOwnerExport> sameTurnExports,
        List<ResourceOwnerMaterializationCandidate> materializationCandidates,
        List<ValidationIssue> issues)
    {
        for (var index = 0; index < rewritten.Count; index++)
        {
            if (rewritten[index] is not JsonObject combatant ||
                combatant["NPCId"] != null ||
                !TryReadExact(combatant["combatantId"], out var combatantId))
            {
                continue;
            }

            var ownerRef = original[index] is JsonObject raw &&
                           TryReadExact(raw["combatantRef"], out var combatantRef)
                ? combatantRef
                : null;
            if (ownerRef == null && !preTurnIds.Contains(combatantId))
            {
                Add(
                    issues,
                    $"combatants[{index}].combatantId",
                    "resource_owner_combatant_preassigned_id_forbidden",
                    "pre-turn permanent combatantId or same-turn combatantRef",
                    combatantId);
                continue;
            }
            var export = CreateExport(
                definitions,
                ResourceOwnerKind.Combatant,
                combatantId,
                sameTurn: ownerRef != null,
                ownerRef);
            (ownerRef == null ? preTurnExports : sameTurnExports).Add(export);
            if (ownerRef == null)
            {
                RejectExistingMaterialization(
                    combatant,
                    $"combatants[{index}]",
                    issues);
            }
            else
            {
                CaptureMaterializationCandidate(
                    combatant,
                    $"combatants[{index}]",
                    export.Key,
                    ownerRef,
                    new[] { "health", "poise" },
                    materializationCandidates,
                    issues);
            }
        }
    }

    private static void AddGroupMemberExports(
        JsonArray rewritten,
        JsonArray original,
        IReadOnlySet<string> preTurnIds,
        ResourceDefinitionCatalog definitions,
        List<ResourceOwnerExport> preTurnExports,
        List<ResourceOwnerExport> sameTurnExports,
        List<ResourceOwnerMaterializationCandidate> materializationCandidates,
        List<ValidationIssue> issues)
    {
        for (var index = 0; index < rewritten.Count; index++)
        {
            if (rewritten[index] is not JsonObject member ||
                !TryReadExact(member["memberId"], out var memberId))
            {
                continue;
            }

            var ownerRef = original[index] is JsonObject raw &&
                           TryReadExact(raw["memberRef"], out var memberRef)
                ? memberRef
                : null;
            if (ownerRef == null && !preTurnIds.Contains(memberId))
            {
                Add(
                    issues,
                    $"members[{index}].memberId",
                    "resource_owner_member_preassigned_id_forbidden",
                    "pre-turn permanent memberId or same-turn memberRef",
                    memberId);
                continue;
            }
            var export = CreateExport(
                definitions,
                ResourceOwnerKind.CombatGroupMember,
                memberId,
                sameTurn: ownerRef != null,
                ownerRef);
            (ownerRef == null ? preTurnExports : sameTurnExports).Add(export);
            if (ownerRef == null)
            {
                RejectExistingMaterialization(
                    member,
                    $"members[{index}]",
                    issues);
            }
            else
            {
                CaptureMaterializationCandidate(
                    member,
                    $"members[{index}]",
                    export.Key,
                    ownerRef,
                    new[] { "health", "poise" },
                    materializationCandidates,
                    issues);
            }
        }
    }

    private static bool IsGroup(JsonObject combatant) =>
        combatant["isGroup"] is JsonValue value &&
        value.TryGetValue<bool>(out var isGroup) &&
        isGroup;

    private static HashSet<string> ComposeNpcOwners(
        JsonObject preTurnRoot,
        JsonObject acceptedRoot,
        ResourceDefinitionCatalog definitions,
        List<ResourceOwnerExport> preTurnExports,
        List<ResourceOwnerExport> sameTurnExports,
        Dictionary<string, string> sameTurnNpcIdsByRef,
        List<AcceptedMechanicsOwnerTransition> ownerTransitions,
        List<ResourceOwnerMaterializationCandidate> materializationCandidates,
        List<ValidationIssue> issues)
    {
        var preTurnIds = new HashSet<string>(StringComparer.Ordinal);
        var ownerAliases = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var npc in GuardianPolicyContracts.EnumerateCanonicalNpcObjects(preTurnRoot))
        {
            var path = $"game_state/npcs/npc_core.json.preTurnNpcs[{index++}].NPCId";
            if (!TryReadExact(npc["NPCId"], out var npcId))
            {
                Add(
                    issues,
                    path,
                    "resource_owner_npc_id_invalid",
                    "one exact permanent NPCId",
                    Describe(npc["NPCId"]));
                continue;
            }

            if (!preTurnIds.Add(npcId) ||
                !ownerAliases.Add(ResourceMaterializationContract.BuildConfusableKey(npcId)))
            {
                Add(
                    issues,
                    path,
                    "resource_owner_npc_identity_ambiguous",
                    "one exact/confusable-unique canonical NPC identity",
                    npcId);
                continue;
            }
            RejectLegacyResourceValues(
                npc,
                path,
                new[] { "currentHealthPercentage", "maxHealthPercentage" },
                issues);
            RejectExistingMaterialization(npc, path, issues);
        }

        var acceptedIds = new HashSet<string>(StringComparer.Ordinal);
        var sameTurnRefs = new HashSet<string>(StringComparer.Ordinal);
        var sameTurnAliases = new HashSet<string>(StringComparer.Ordinal);
        index = 0;
        foreach (var npc in GuardianPolicyContracts.EnumerateCanonicalNpcObjects(acceptedRoot))
        {
            var path = $"game_state/npcs/npc_core.json.npcs[{index++}]";
            if (TryReadExact(npc["NPCId"], out var npcId))
            {
                if (!preTurnIds.Contains(npcId))
                {
                    Add(
                        issues,
                        path + ".NPCId",
                        "resource_owner_npc_preassigned_id_forbidden",
                        "exact validated pre-turn NPCId or same-turn initialId",
                        npcId);
                    continue;
                }
                if (!acceptedIds.Add(npcId))
                    continue;

                RejectLegacyResourceValues(
                    npc,
                    path,
                    new[] { "currentHealthPercentage", "maxHealthPercentage" },
                    issues);
                RejectExistingMaterialization(npc, path, issues);
                preTurnExports.Add(CreateExport(
                    definitions,
                    ResourceOwnerKind.Npc,
                    npcId));
                continue;
            }

            var hasNullNpcId = npc.TryGetPropertyValue("NPCId", out var npcIdNode) &&
                               npcIdNode == null;
            if (!hasNullNpcId || !TryReadExact(npc["initialId"], out var initialId))
            {
                Add(
                    issues,
                    path,
                    "resource_owner_npc_id_invalid",
                    "pre-turn permanent NPCId or NPCId null plus exact same-turn initialId",
                    npc.ToJsonString());
                continue;
            }

            var alias = ResourceMaterializationContract.BuildConfusableKey(initialId);
            if (!sameTurnRefs.Add(initialId) ||
                !sameTurnAliases.Add(alias) ||
                ownerAliases.Contains(alias))
            {
                Add(
                    issues,
                    path + ".initialId",
                    "resource_owner_npc_same_turn_identity_ambiguous",
                    "one exact/confusable-unique same-turn NPC initialId",
                    initialId);
                continue;
            }

            acceptedIds.Add(initialId);
            sameTurnNpcIdsByRef.Add(initialId, initialId);
            var export = CreateExport(
                definitions,
                ResourceOwnerKind.Npc,
                initialId,
                sameTurn: true,
                initialId);
            sameTurnExports.Add(export);
            RejectLegacyResourceValues(
                npc,
                path,
                new[] { "currentHealthPercentage", "maxHealthPercentage" },
                issues);
            var expectedMaterialization =
                (npc["resourceMaterialization"] as JsonObject)?.DeepClone().AsObject();
            CaptureMaterializationCandidate(
                npc,
                path,
                export.Key,
                initialId,
                new[] { "health" },
                materializationCandidates,
                issues);
            if (expectedMaterialization != null)
            {
                ownerTransitions.Add(
                    AcceptedMechanicsOwnerTransition.CreateMortalNpcCreation(
                        initialId,
                        initialId,
                        expectedMaterialization));
            }
        }
        return acceptedIds;
    }

    private static void ResolveSameTurnNamedCombatantRefs(
        JsonObject root,
        string collectionName,
        IReadOnlyDictionary<string, string> sameTurnNpcIdsByRef,
        List<ValidationIssue> issues)
    {
        if (root[collectionName] is not JsonArray combatants)
            return;

        for (var index = 0; index < combatants.Count; index++)
        {
            if (combatants[index] is not JsonObject combatant ||
                !combatant.TryGetPropertyValue("npcRef", out var refNode) ||
                refNode == null)
            {
                continue;
            }

            var path = $"game_state/combat/{collectionName}.json.{collectionName}[{index}]";
            if (!TryReadExact(refNode, out var npcRef) ||
                !sameTurnNpcIdsByRef.TryGetValue(npcRef, out var npcId))
            {
                Add(
                    issues,
                    path + ".npcRef",
                    "resource_owner_combat_npc_ref_unresolved",
                    "one exact accepted same-turn NPC initialId",
                    Describe(refNode));
                continue;
            }
            if (combatant["NPCId"] != null ||
                combatant["combatantId"] != null ||
                combatant["combatantRef"] != null)
            {
                Add(
                    issues,
                    path,
                    "resource_owner_named_combatant_identity_duplicated",
                    "same-turn named combat representation bound only through npcRef",
                    combatant.ToJsonString());
                continue;
            }

            combatant.Remove("npcRef");
            combatant["NPCId"] = npcId;
        }
    }

    private static void ValidateNamedCombatantBindings(
        JsonObject root,
        string collectionName,
        IReadOnlySet<string> npcIds,
        ISet<string> claimedNpcIds,
        List<ValidationIssue> issues)
    {
        if (root[collectionName] is not JsonArray combatants)
            return;

        for (var index = 0; index < combatants.Count; index++)
        {
            if (combatants[index] is not JsonObject combatant ||
                !combatant.TryGetPropertyValue("NPCId", out var npcNode) ||
                npcNode == null)
            {
                continue;
            }

            var path = $"game_state/combat/{collectionName}.json.{collectionName}[{index}]";
            if (!TryReadExact(npcNode, out var npcId) || !npcIds.Contains(npcId))
            {
                Add(
                    issues,
                    path + ".NPCId",
                    "resource_owner_combat_npc_binding_unresolved",
                    "one exact composed permanent NPCId",
                    Describe(npcNode));
            }
            else if (!claimedNpcIds.Add(npcId))
            {
                Add(
                    issues,
                    path + ".NPCId",
                    "resource_owner_named_combatant_binding_duplicate",
                    "one combat representation for each exact named NPCId",
                    npcId);
            }
            if (combatant.ContainsKey("combatantId") || combatant.ContainsKey("combatantRef"))
            {
                Add(
                    issues,
                    path,
                    "resource_owner_named_combatant_identity_duplicated",
                    "named combat representation bound only through NPCId",
                    combatant.ToJsonString());
            }
        }
    }

    private static ResourceOwnerExport CreateExport(
        ResourceDefinitionCatalog definitions,
        ResourceOwnerKind ownerKind,
        string ownerId,
        bool sameTurn = false,
        string? ownerRef = null,
        IEnumerable<string>? additionalCapabilities = null)
    {
        var capabilities = definitions.Definitions
            .Where(definition => definition.AllowedOwnerKinds.Contains(ownerKind))
            .Select(static definition => definition.ResourceKey)
            .ToHashSet(StringComparer.Ordinal);
        if (additionalCapabilities != null)
            capabilities.UnionWith(additionalCapabilities);
        using var fingerprint = new ResourceFingerprintBuilder(
            "mortal-resource-owner-v1");
        fingerprint.Append(Realm);
        fingerprint.Append(ResourceDefinitionCatalog.GetOwnerKindToken(ownerKind));
        fingerprint.Append(ownerId);
        fingerprint.Append(sameTurn ? "same_turn" : "pre_turn");
        fingerprint.Append(ownerRef ?? string.Empty);
        foreach (var capability in capabilities.OrderBy(
                     static value => value,
                     StringComparer.Ordinal))
        {
            fingerprint.Append(capability);
        }

        return new ResourceOwnerExport(
            new ResourceOwnerKey(Realm, ownerKind, ownerId),
            ResourceOwnerLifecycle.Active,
            sameTurn,
            ownerRef,
            BoundNpcId: null,
            capabilities,
            fingerprint.Build());
    }

    private static void ApplySameTurnCapabilities(
        MortalResourceOwnerCompositionInput input,
        List<ResourceOwnerExport> exports)
    {
        for (var index = 0; index < exports.Count; index++)
        {
            var current = exports[index];
            var additions = input.GetSameTurnCapabilities(current.Key.OwnerKind);
            if (additions.Count == 0)
                continue;
            exports[index] = CreateExport(
                input.Definitions,
                current.Key.OwnerKind,
                current.Key.ResourceOwnerId,
                current.SameTurn,
                current.OwnerRef,
                additions);
        }
    }

    private static void CaptureMaterializationCandidate(
        JsonObject owner,
        string path,
        ResourceOwnerKey ownerKey,
        string ownerRef,
        IReadOnlyList<string> requiredResourceKeys,
        List<ResourceOwnerMaterializationCandidate> candidates,
        List<ValidationIssue> issues)
    {
        if (owner["resourceMaterialization"] is not JsonObject envelope)
        {
            Add(
                issues,
                path + ".resourceMaterialization",
                "resource_owner_materialization_required",
                "one closed resource materialization envelope for a new owner",
                Describe(owner["resourceMaterialization"]));
            return;
        }

        candidates.Add(new ResourceOwnerMaterializationCandidate(
            path,
            ownerKey,
            ownerRef,
            envelope.DeepClone().AsObject(),
            requiredResourceKeys.ToArray()));
        owner.Remove("resourceMaterialization");
    }

    private static void RejectExistingMaterialization(
        JsonObject owner,
        string path,
        List<ValidationIssue> issues)
    {
        if (!owner.ContainsKey("resourceMaterialization"))
            return;
        Add(
            issues,
            path + ".resourceMaterialization",
            "resource_owner_materialization_existing_forbidden",
            "field absent for every existing owner",
            Describe(owner["resourceMaterialization"]));
    }

    private static void RejectLegacyResourceValues(
        JsonObject owner,
        string path,
        IEnumerable<string> fields,
        List<ValidationIssue> issues)
    {
        foreach (var field in fields.Where(owner.ContainsKey))
        {
            Add(
                issues,
                path + "." + field,
                "resource_owner_legacy_value_forbidden",
                "resource value absent; use the unified resource ledger",
                field);
        }
    }

    private static void RejectCombatLegacyValues(
        JsonObject root,
        string collectionName,
        string rootPath,
        List<ValidationIssue> issues)
    {
        if (root[collectionName] is not JsonArray combatants)
            return;
        for (var index = 0; index < combatants.Count; index++)
        {
            if (combatants[index] is not JsonObject combatant)
                continue;
            var path = $"{rootPath}.{collectionName}[{index}]";
            RejectLegacyResourceValues(
                combatant,
                path,
                new[]
                {
                    "currentHealth", "maxHealth", "currentPoise", "maxPoise",
                    "healthStates"
                },
                issues);
            if (combatant["NPCId"] != null || combatant["npcRef"] != null ||
                IsGroup(combatant))
            {
                RejectExistingMaterialization(combatant, path, issues);
            }
            if (combatant["members"] is not JsonArray members)
                continue;
            for (var memberIndex = 0; memberIndex < members.Count; memberIndex++)
            {
                if (members[memberIndex] is not JsonObject member)
                    continue;
                RejectLegacyResourceValues(
                    member,
                    $"{path}.members[{memberIndex}]",
                    new[]
                    {
                        "currentHealth", "maxHealth", "currentPoise", "maxPoise"
                    },
                    issues);
            }
        }
    }

    private static void StripMaterializationEnvelopes(
        JsonObject root,
        string collectionName)
    {
        if (root[collectionName] is not JsonArray combatants)
            return;
        foreach (var combatant in combatants.OfType<JsonObject>())
        {
            combatant.Remove("resourceMaterialization");
            if (combatant["members"] is not JsonArray members)
                continue;
            foreach (var member in members.OfType<JsonObject>())
                member.Remove("resourceMaterialization");
        }
    }

    private static bool TryReadDecimal(JsonNode? node, out decimal value)
    {
        value = default;
        if (node == null)
            return false;
        try
        {
            using var document = JsonDocument.Parse(node.ToJsonString());
            return document.RootElement.ValueKind == JsonValueKind.Number &&
                   document.RootElement.TryGetDecimal(out value);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static ResourceOwnerCompositionResult Invalid(
        IEnumerable<ValidationIssue> issues) =>
        new(
            null,
            new ReadOnlyDictionary<string, JsonObject>(
                new Dictionary<string, JsonObject>(StringComparer.Ordinal)),
            Array.Empty<AcceptedMechanicsOwnerTransition>(),
            null,
            Array.Empty<ResourceOwnerCapacityDraft>(),
            Array.Empty<ResourceOwnerKey>(),
            issues.ToArray());

    private static bool TryReadExact(JsonNode? node, out string value)
    {
        value = node is JsonValue jsonValue &&
                jsonValue.TryGetValue<string>(out var text)
            ? text
            : string.Empty;
        return ResourceMaterializationContract.IsExactIdentifier(value);
    }

    private static string Describe(JsonNode? node) =>
        node?.ToJsonString() ?? "missing";

    private static void Add(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Mortal resource owner composition violates exact accepted-turn authority.",
            code: code,
            section: "resource_owner_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Use one exact validated permanent owner identity or its accepted same-turn ref; never create a second combat-local resource owner for a named NPC."));

}
