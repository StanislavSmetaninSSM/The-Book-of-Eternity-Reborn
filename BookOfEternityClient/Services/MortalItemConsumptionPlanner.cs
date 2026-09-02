using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record MortalItemConsumptionCommand(
    int FinalizationOrdinal,
    string ItemId,
    int Quantity,
    string ClaimFingerprint,
    string TransitionId,
    string AuthorityKind,
    string AuthorityId);

internal sealed record MortalItemConsumptionPlanningInput(
    int Turn,
    string BaselineFingerprint,
    MortalItemCarrierCatalogInput CarrierRoots,
    MortalItemIdentityParseResult IdentityState,
    IReadOnlyList<MortalItemConsumptionCommand> Commands,
    ResourceDefinitionCatalog Definitions,
    ResourceStateLedger ResourceState,
    ResourceSourceEvidence CapacitySourceEvidence,
    string CapacityPolicyFingerprint);

internal sealed record MortalItemConsumptionPlanningResult(
    IReadOnlyDictionary<string, JsonObject> CarrierAfterImages,
    JsonObject? IdentityIndexAfterImage,
    IReadOnlyList<JsonObject> IdentityTransitions,
    IReadOnlyList<ResourceCapacityIntent> CapacityTransitions,
    IReadOnlyList<ResourceOwnerKey> TerminalOwners,
    IReadOnlyList<ValidationIssue> Issues,
    string Fingerprint)
{
    internal bool IsValid => IdentityIndexAfterImage != null && Issues.Count == 0;
}

/// <summary>
/// Produces one detached, fail-closed item and item-resource consumption plan.
/// The planner performs no file or history access and never mints identities.
/// </summary>
internal static class MortalItemConsumptionPlanner
{
    private const string PlayerPath = "game_state/inventory/items.json";
    private const string NpcPath = "game_state/npcs/npc_core.json";
    private const string NpcCommandsPath = "game_state/npcs/npc_inventory.json";
    private const string LocationPath = "game_state/world/current_location.json";
    private const string VehiclesPath = "game_state/misc/vehicles.json";
    private const string OffscreenStoragePath =
        "game_state/world/location_storage_contents.json";
    private static readonly HashSet<string> StandardRootPaths = new(
        new[]
        {
            PlayerPath,
            NpcPath,
            NpcCommandsPath,
            LocationPath,
            VehiclesPath,
            OffscreenStoragePath
        },
        StringComparer.Ordinal);

    internal static MortalItemConsumptionPlanningResult Plan(
        MortalItemConsumptionPlanningInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var issues = ValidateInput(input);
        if (issues.Count > 0)
            return Invalid(input, issues);

        var roots = CloneRoots(input.CarrierRoots);
        var rootMap = MapRoots(roots);
        var catalog = MortalItemCarrierCatalog.Build(roots);
        if (catalog.Issues.Count > 0)
        {
            issues.AddRange(catalog.Issues.Select(CatalogIssue));
            return Invalid(input, issues);
        }

        var beforeIndex = MortalItemIdentityState.Parse(
            input.IdentityState.Root.DeepClone());
        if (beforeIndex.Issues.Count > 0)
            return Invalid(input, beforeIndex.Issues);
        var currentIndex = MortalItemIdentityState.Parse(beforeIndex.Root.DeepClone());
        var resourceState = new ResourceStateLedger(input.ResourceState.Entries);
        var transitions = new List<JsonObject>();
        var capacities = new List<ResourceCapacityIntent>();
        var terminalOwners = new List<ResourceOwnerKey>();
        var usedTransitionKeys = CollectTransitionKeys(beforeIndex);

        foreach (var command in input.Commands)
        {
            var commandIssue = ValidateCommand(
                input,
                command,
                catalog,
                currentIndex,
                usedTransitionKeys,
                rootMap,
                out var occurrence,
                out var item,
                out var entry,
                out var sourceCount);
            if (commandIssue != null)
                return Invalid(input, new[] { commandIssue });

            var remainingCount = sourceCount - command.Quantity;
            if (remainingCount < 0)
            {
                return Invalid(input, new[]
                {
                    Issue(
                        command.ItemId,
                        "mortal_item_consumption_quantity_exceeds_stack",
                        $"quantity <= {sourceCount}",
                        command.Quantity.ToString(CultureInfo.InvariantCulture))
                });
            }

            if (remainingCount > 0)
            {
                var resourcePlan = MortalItemOwnedResourceConsumptionPlanner.Plan(
                    input.Turn,
                    command,
                    sourceCount,
                    remainingCount,
                    input.Definitions,
                    resourceState,
                    input.CapacitySourceEvidence,
                    input.CapacityPolicyFingerprint);
                if (!resourcePlan.IsValid || resourcePlan.StateAfterImage == null)
                    return Invalid(input, resourcePlan.Issues);
                resourceState = resourcePlan.StateAfterImage;
                capacities.AddRange(resourcePlan.CapacityTransitions);
                item!["count"] = remainingCount;
            }
            else
            {
                if (HasUnsafeCompanionReference(catalog, command.ItemId))
                {
                    return Invalid(input, new[]
                    {
                        Issue(
                            command.ItemId,
                            "mortal_item_consumption_companion_authority_missing",
                            "only supported inline equipment or separate atomic companion authority",
                            "container, quest, bond, or other companion reference")
                    });
                }

                ClearInlineEquipmentReference(rootMap, occurrence!.Carrier, command.ItemId);
                if (item!.Parent is not JsonArray carrierItems ||
                    !carrierItems.Remove(item))
                {
                    return Invalid(input, new[]
                    {
                        Issue(
                            command.ItemId,
                            "mortal_item_consumption_carrier_changed",
                            "the exact indexed item occurrence",
                            "missing before terminal removal")
                    });
                }
                entry!["state"] = "consumed";
                entry["currentCarrier"] = null;
                entry["mergedIntoItemId"] = null;
                terminalOwners.Add(new ResourceOwnerKey(
                    "mortal_world",
                    ResourceOwnerKind.Item,
                    command.ItemId));
            }

            var carrierNode = CreateCarrierNode(occurrence!.Carrier);
            var transition = MortalItemIdentityState.CreateTransition(
                "consume",
                input.Turn,
                new[] { command.ItemId },
                carrierNode,
                remainingCount == 0 ? null : carrierNode,
                sourceCount,
                remainingCount,
                command.AuthorityKind,
                command.AuthorityId,
                command.TransitionId);
            MortalItemIdentityState.AppendTransition(entry!, transition);
            transitions.Add(transition.DeepClone().AsObject());
            usedTransitionKeys.Add(MortalItemIdentityRules.BuildConfusableKey(
                command.TransitionId));

            currentIndex = MortalItemIdentityState.Parse(currentIndex.Root);
            if (currentIndex.Issues.Count > 0)
                return Invalid(input, currentIndex.Issues);
        }

        var continuityIssues = MortalItemIdentityState.ValidateAgainst(
            beforeIndex,
            currentIndex);
        if (continuityIssues.Count > 0)
            return Invalid(input, continuityIssues);
        var composedIssues = ValidateAfterImage(catalog, roots, currentIndex, input.Commands);
        if (composedIssues.Count > 0)
            return Invalid(input, composedIssues);

        var afterImages = MapRoots(roots).ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value,
            StringComparer.Ordinal);
        var result = new MortalItemConsumptionPlanningResult(
            afterImages,
            currentIndex.Root.DeepClone().AsObject(),
            transitions.ToArray(),
            capacities.ToArray(),
            terminalOwners.Distinct().ToArray(),
            Array.Empty<ValidationIssue>(),
            string.Empty);
        return result with { Fingerprint = Fingerprint(input, result) };
    }

    private static List<ValidationIssue> ValidateInput(
        MortalItemConsumptionPlanningInput input)
    {
        var issues = new List<ValidationIssue>();
        if (input.Turn < 1 ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(
                input.BaselineFingerprint) ||
            !ResourceMaterializationContract.IsExactIdentifier(
                input.CapacitySourceEvidence.SourceKind) ||
            !ResourceMaterializationContract.IsExactIdentifier(
                input.CapacitySourceEvidence.SourceId) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(
                input.CapacitySourceEvidence.AuthorityFingerprint) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(
                input.CapacityPolicyFingerprint))
        {
            issues.Add(Issue(
                "input",
                "mortal_item_consumption_authority_invalid",
                "exact turn, baseline, source, and capacity policy authority",
                "invalid"));
        }
        if (input.IdentityState.Issues.Count > 0)
            issues.AddRange(input.IdentityState.Issues);
        foreach (var path in input.CarrierRoots.CompanionRoots.Keys)
        {
            if (!StandardRootPaths.Contains(path))
                continue;
            issues.Add(Issue(
                path,
                "mortal_item_consumption_root_registry_collision",
                "one collision-free companion root registry",
                path));
        }
        if (input.Commands.Count == 0)
        {
            issues.Add(Issue(
                "commands",
                "mortal_item_consumption_commands_missing",
                "one or more ordered consumption commands",
                "empty"));
        }
        var priorFinalizationOrdinal = 0;
        for (var index = 0; index < input.Commands.Count; index++)
        {
            var command = input.Commands[index];
            if (command.FinalizationOrdinal <= priorFinalizationOrdinal)
            {
                issues.Add(Issue(
                    command.ItemId,
                    "mortal_item_consumption_ordinal_not_contiguous",
                    $"> {priorFinalizationOrdinal.ToString(CultureInfo.InvariantCulture)}",
                    command.FinalizationOrdinal.ToString(CultureInfo.InvariantCulture)));
            }
            priorFinalizationOrdinal = command.FinalizationOrdinal;
            if (command.Quantity <= 0 ||
                !ResourceMaterializationContract.IsExactIdentifier(command.ItemId) ||
                !ResourceMaterializationContract.IsAuthorityFingerprint(
                    command.ClaimFingerprint) ||
                !ResourceMaterializationContract.IsExactIdentifier(command.AuthorityKind) ||
                !ResourceMaterializationContract.IsExactIdentifier(command.AuthorityId) ||
                !ResourceMaterializationContract.IsExactIdentifier(command.TransitionId) ||
                !command.TransitionId.StartsWith("mitrn_", StringComparison.Ordinal))
            {
                issues.Add(Issue(
                    command.ItemId,
                    "mortal_item_consumption_command_invalid",
                    "positive quantity and exact client-minted item/claim/transition/authority identities",
                    command.TransitionId));
            }
        }
        return issues;
    }

    private static ValidationIssue? ValidateCommand(
        MortalItemConsumptionPlanningInput input,
        MortalItemConsumptionCommand command,
        MortalItemCarrierCatalog catalog,
        MortalItemIdentityParseResult index,
        IReadOnlySet<string> usedTransitionKeys,
        IReadOnlyDictionary<string, JsonObject> roots,
        out MortalItemCarrierOccurrence? occurrence,
        out JsonObject? item,
        out JsonObject? entry,
        out int sourceCount)
    {
        occurrence = null;
        item = null;
        entry = null;
        sourceCount = 0;
        var transitionKey = MortalItemIdentityRules.BuildConfusableKey(command.TransitionId);
        if (usedTransitionKeys.Contains(transitionKey))
        {
            return Issue(
                command.ItemId,
                "mortal_item_consumption_transition_identity_reused",
                "one globally unique non-confusable mitrn_ transition identity",
                command.TransitionId);
        }
        if (!catalog.ByItemId.TryGetValue(command.ItemId, out var occurrences) ||
            occurrences.Count != 1)
        {
            return Issue(
                command.ItemId,
                "mortal_item_consumption_carrier_mismatch",
                "one exact active carrier occurrence",
                occurrences?.Count.ToString(CultureInfo.InvariantCulture) ?? "missing");
        }
        occurrence = occurrences[0];
        if (!roots.TryGetValue(occurrence.FilePath, out var carrierRoot) ||
            FindCanonicalItem(carrierRoot, command.ItemId) is not { } resolvedItem)
        {
            return Issue(
                command.ItemId,
                "mortal_item_consumption_carrier_mismatch",
                "one exact mutable detached carrier occurrence",
                "missing");
        }
        item = resolvedItem;
        if (!index.EntriesByItemId.TryGetValue(command.ItemId, out entry) ||
            !string.Equals(ReadExactString(entry["state"]), "active", StringComparison.Ordinal) ||
            !JsonNode.DeepEquals(entry["currentCarrier"], CreateCarrierNode(occurrence.Carrier)))
        {
            return Issue(
                command.ItemId,
                "mortal_item_consumption_identity_carrier_mismatch",
                "one active identity at the exact carrier",
                "changed");
        }
        if (!TryReadPositiveInt(item["count"], out sourceCount) ||
            entry["transitions"] is not JsonArray { Count: > 0 } history ||
            history[^1] is not JsonObject last ||
            !TryReadPositiveInt(last["quantityAfter"], out var indexedCount) ||
            indexedCount != sourceCount)
        {
            return Issue(
                command.ItemId,
                "mortal_item_consumption_quantity_history_mismatch",
                "carrier count equals current transition quantity",
                "changed");
        }
        var receiptId = ReadExactString(
            item[MortalItemMaterializationContract.ReceiptProperty]?["receiptId"]);
        if (!string.Equals(receiptId, ReadExactString(entry["receiptId"]), StringComparison.Ordinal))
        {
            return Issue(
                command.ItemId,
                "mortal_item_consumption_receipt_mismatch",
                "carrier receipt equals identity receipt",
                "changed");
        }
        if (history.OfType<JsonObject>().Any(transition =>
                string.Equals(
                    ReadExactString(transition["authorityKind"]),
                    command.AuthorityKind,
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadExactString(transition["authorityId"]),
                    command.AuthorityId,
                    StringComparison.Ordinal)))
        {
            return Issue(
                command.ItemId,
                "mortal_item_consumption_authority_replay",
                "one unapplied exact item transition authority",
                command.AuthorityId);
        }
        return null;
    }

    private static JsonObject? FindCanonicalItem(JsonNode? root, string itemId)
    {
        JsonObject? match = null;
        Visit(root);
        return ReferenceEquals(match, MultipleMatches) ? null : match;

        void Visit(JsonNode? node)
        {
            if (node == null || ReferenceEquals(match, MultipleMatches))
                return;
            if (node is JsonObject obj)
            {
                if (obj[MortalItemMaterializationContract.ReceiptProperty] is JsonObject &&
                    string.Equals(ReadExactString(obj["itemId"]), itemId, StringComparison.Ordinal))
                {
                    match = match == null ? obj : MultipleMatches;
                    return;
                }
                foreach (var pair in obj)
                    Visit(pair.Value);
            }
            else if (node is JsonArray array)
            {
                foreach (var value in array)
                    Visit(value);
            }
        }
    }

    private static readonly JsonObject MultipleMatches = new();

    private static IReadOnlyList<ValidationIssue> ValidateAfterImage(
        MortalItemCarrierCatalog beforeCatalog,
        MortalItemCarrierCatalogInput roots,
        MortalItemIdentityParseResult index,
        IReadOnlyList<MortalItemConsumptionCommand> commands)
    {
        var issues = new List<ValidationIssue>();
        var afterCatalog = MortalItemCarrierCatalog.Build(roots);
        issues.AddRange(afterCatalog.Issues.Select(CatalogIssue));
        foreach (var command in commands)
        {
            if (!index.EntriesByItemId.TryGetValue(command.ItemId, out var entry))
            {
                issues.Add(Issue(
                    command.ItemId,
                    "mortal_item_consumption_identity_missing_after_plan",
                    "the exact preserved identity entry",
                    "missing"));
                continue;
            }
            var state = ReadExactString(entry["state"]);
            afterCatalog.ByItemId.TryGetValue(command.ItemId, out var occurrences);
            if (state == "active" && occurrences?.Count != 1 ||
                state == "consumed" && occurrences is { Count: > 0 })
            {
                issues.Add(Issue(
                    command.ItemId,
                    "mortal_item_consumption_after_image_mismatch",
                    "active identity at one carrier or consumed identity at none",
                    state ?? "missing"));
            }
            if (state == "consumed" &&
                afterCatalog.ByCompanionReference.ContainsKey(command.ItemId))
            {
                issues.Add(Issue(
                    command.ItemId,
                    "mortal_item_consumption_companion_survived",
                    "no terminal companion reference",
                    "present"));
            }
        }
        return issues;
    }

    private static HashSet<string> CollectTransitionKeys(
        MortalItemIdentityParseResult index)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var transition in index.EntriesByItemId.Values
                     .SelectMany(static entry =>
                         (entry["transitions"] as JsonArray)?.OfType<JsonObject>() ??
                         Enumerable.Empty<JsonObject>()))
        {
            if (ReadExactString(transition["transitionId"]) is { } transitionId)
                result.Add(MortalItemIdentityRules.BuildConfusableKey(transitionId));
        }
        return result;
    }

    private static MortalItemCarrierCatalogInput CloneRoots(
        MortalItemCarrierCatalogInput roots) => new(
        roots.PlayerInventory?.DeepClone().AsObject(),
        roots.NpcCore?.DeepClone().AsObject(),
        roots.NpcInventoryCommands?.DeepClone().AsObject(),
        roots.CurrentLocation?.DeepClone().AsObject(),
        roots.Vehicles?.DeepClone().AsObject(),
        roots.CompanionRoots.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DeepClone().AsObject(),
            StringComparer.Ordinal),
        roots.OffscreenLocationStorageContents?.DeepClone().AsObject());

    private static Dictionary<string, JsonObject> MapRoots(
        MortalItemCarrierCatalogInput roots)
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        Add(PlayerPath, roots.PlayerInventory);
        Add(NpcPath, roots.NpcCore);
        Add(NpcCommandsPath, roots.NpcInventoryCommands);
        Add(LocationPath, roots.CurrentLocation);
        Add(VehiclesPath, roots.Vehicles);
        Add(OffscreenStoragePath, roots.OffscreenLocationStorageContents);
        foreach (var pair in roots.CompanionRoots)
            result.TryAdd(pair.Key, pair.Value);
        return result;

        void Add(string path, JsonObject? root)
        {
            if (root != null)
                result.Add(path, root);
        }
    }

    private static bool HasUnsafeCompanionReference(
        MortalItemCarrierCatalog catalog,
        string itemId) =>
        catalog.ByCompanionReference.TryGetValue(itemId, out var references) &&
        references.Any(static reference => !IsInlineEquipmentReference(reference));

    private static bool IsInlineEquipmentReference(MortalItemCompanionReference reference) =>
        string.Equals(
            reference.FilePath,
            StorageTransportMoveService.InventoryPath,
            StringComparison.OrdinalIgnoreCase) &&
        (reference.JsonPath.Contains(".equipment", StringComparison.OrdinalIgnoreCase) ||
         reference.JsonPath.Contains(".equippedItems", StringComparison.OrdinalIgnoreCase));

    private static void ClearInlineEquipmentReference(
        IReadOnlyDictionary<string, JsonObject> roots,
        MortalItemCarrierCoordinate source,
        string itemId)
    {
        JsonObject? owner = source.Kind switch
        {
            "player_inventory" => roots.GetValueOrDefault(PlayerPath),
            "npc_inventory" => ResolveNpc(
                roots.GetValueOrDefault(NpcPath),
                source.OwnerId),
            _ => null
        };
        if (owner == null)
            return;
        RemoveExactEquipmentReference(owner["equipment"], itemId);
        RemoveExactEquipmentReference(owner["equippedItems"], itemId);
    }

    private static JsonObject? ResolveNpc(JsonObject? root, string npcId)
    {
        var matches = new List<JsonObject>();
        foreach (var section in new[] { "UpdateNPCs", "NPCsInScene" })
        {
            if (root?[section] is JsonArray npcs)
            {
                matches.AddRange(npcs.OfType<JsonObject>().Where(npc =>
                    new[] { "NPCId", "npcId", "id", "initialId" }.Any(property =>
                        string.Equals(
                            ReadExactString(npc[property]),
                            npcId,
                            StringComparison.Ordinal))));
            }
        }
        return matches.Count == 1 ? matches[0] : null;
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

    private static JsonObject CreateCarrierNode(MortalItemCarrierCoordinate carrier) => new()
    {
        ["kind"] = carrier.Kind,
        ["ownerId"] = carrier.OwnerId,
        ["containerId"] = carrier.ContainerId,
        ["containerPath"] = new JsonArray(carrier.ContainerPath
            .Select(static value => (JsonNode?)JsonValue.Create(value)).ToArray())
    };

    private static MortalItemConsumptionPlanningResult Invalid(
        MortalItemConsumptionPlanningInput input,
        IEnumerable<ValidationIssue> issues)
    {
        var array = issues.Select(CloneIssue).ToArray();
        var result = new MortalItemConsumptionPlanningResult(
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            null,
            Array.Empty<JsonObject>(),
            Array.Empty<ResourceCapacityIntent>(),
            Array.Empty<ResourceOwnerKey>(),
            array,
            string.Empty);
        return result with { Fingerprint = Fingerprint(input, result) };
    }

    private static ValidationIssue CloneIssue(ValidationIssue issue)
    {
        var clone = WoundAcceptedTurnData.CloneIssue(issue);
        if (clone.MortalItemRepairContext is { } context)
        {
            clone.MortalItemRepairContext = context with
            {
                SourceCarrier = CloneCarrier(context.SourceCarrier),
                DestinationCarrier = CloneCarrier(context.DestinationCarrier)
            };
        }
        return clone;
    }

    private static MortalItemCarrierCoordinate? CloneCarrier(
        MortalItemCarrierCoordinate? carrier) =>
        carrier is null
            ? null
            : carrier with { ContainerPath = carrier.ContainerPath.ToArray() };

    private static string Fingerprint(
        MortalItemConsumptionPlanningInput input,
        MortalItemConsumptionPlanningResult result)
    {
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_item.consumption_plan",
            "1",
            input.Turn.ToString(CultureInfo.InvariantCulture),
            input.BaselineFingerprint,
            input.Definitions.ToCanonicalJson(),
            input.ResourceState.ToCanonicalJson(),
            input.CapacitySourceEvidence.SourceKind,
            input.CapacitySourceEvidence.SourceId,
            input.CapacitySourceEvidence.AuthorityFingerprint,
            input.CapacityPolicyFingerprint,
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(input.IdentityState.Root)
        };
        foreach (var pair in EnumerateRoots(input.CarrierRoots))
        {
            fields.Add("input_root");
            fields.Add(pair.Key);
            fields.Add(pair.Value == null
                ? "missing"
                : WoundAcceptedTurnFingerprintWriter.CanonicalJson(pair.Value));
        }
        fields.Add("input_issue_count");
        fields.Add(input.IdentityState.Issues.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var issue in input.IdentityState.Issues)
            AppendIssue(fields, "input_issue", issue);
        fields.Add("command_count");
        fields.Add(input.Commands.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var command in input.Commands)
        {
            fields.Add(command.FinalizationOrdinal.ToString(CultureInfo.InvariantCulture));
            fields.Add(command.ItemId);
            fields.Add(command.Quantity.ToString(CultureInfo.InvariantCulture));
            fields.Add(command.ClaimFingerprint);
            fields.Add(command.TransitionId);
            fields.Add(command.AuthorityKind);
            fields.Add(command.AuthorityId);
        }
        foreach (var pair in result.CarrierAfterImages
                     .OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            fields.Add("after_root");
            fields.Add(pair.Key);
            fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(pair.Value));
        }
        fields.Add(result.IdentityIndexAfterImage == null
            ? "no_index"
            : WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                result.IdentityIndexAfterImage));
        fields.Add("identity_transition_count");
        fields.Add(result.IdentityTransitions.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var transition in result.IdentityTransitions)
            fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(transition));
        fields.Add("capacity_transition_count");
        fields.Add(result.CapacityTransitions.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var capacity in result.CapacityTransitions)
        {
            fields.Add(capacity.EventRef);
            fields.Add(capacity.OriginKind);
            fields.Add(capacity.OriginId);
            fields.Add(capacity.Coordinate.Realm);
            fields.Add(capacity.Coordinate.OwnerKind.ToString());
            fields.Add(capacity.Coordinate.ResourceOwnerId);
            fields.Add(capacity.Coordinate.ResourceKey);
            fields.Add(capacity.Operation.ToString());
            fields.Add(capacity.ResolvedCapacity?.Maximum.ToString(CultureInfo.InvariantCulture));
            fields.Add(capacity.ResolvedCapacity?.Binding.Kind.ToString());
            fields.Add(capacity.ResolvedCapacity?.Binding.AuthorityKey);
            fields.Add(capacity.ResolvedCapacity?.Binding.AuthorityFingerprint);
            fields.Add(capacity.ResolvedCapacity?.Initialization?.Current.ToString(
                CultureInfo.InvariantCulture));
            fields.Add(capacity.ResolvedCapacity?.Initialization?.AuthorityFingerprint);
            fields.Add(capacity.CurrentDisposition?.ToString());
            fields.Add(capacity.Phase.ToString());
            fields.Add(capacity.Priority.ToString(CultureInfo.InvariantCulture));
            fields.Add(capacity.SourceEvidence.SourceKind);
            fields.Add(capacity.SourceEvidence.SourceId);
            fields.Add(capacity.SourceEvidence.AuthorityFingerprint);
            fields.Add(capacity.PolicyFingerprint);
            fields.Add(capacity.ReceiptId);
        }
        fields.Add("terminal_owner_count");
        fields.Add(result.TerminalOwners.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var owner in result.TerminalOwners)
        {
            fields.Add(owner.Realm);
            fields.Add(owner.OwnerKind.ToString());
            fields.Add(owner.ResourceOwnerId);
        }
        fields.Add("result_issue_count");
        fields.Add(result.Issues.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var issue in result.Issues)
            AppendIssue(fields, "result_issue", issue);
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static IEnumerable<KeyValuePair<string, JsonObject?>> EnumerateRoots(
        MortalItemCarrierCatalogInput roots)
    {
        yield return new KeyValuePair<string, JsonObject?>(PlayerPath, roots.PlayerInventory);
        yield return new KeyValuePair<string, JsonObject?>(NpcPath, roots.NpcCore);
        yield return new KeyValuePair<string, JsonObject?>(
            NpcCommandsPath,
            roots.NpcInventoryCommands);
        yield return new KeyValuePair<string, JsonObject?>(LocationPath, roots.CurrentLocation);
        yield return new KeyValuePair<string, JsonObject?>(VehiclesPath, roots.Vehicles);
        yield return new KeyValuePair<string, JsonObject?>(
            OffscreenStoragePath,
            roots.OffscreenLocationStorageContents);
        foreach (var pair in roots.CompanionRoots.OrderBy(
                     static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            yield return new KeyValuePair<string, JsonObject?>(pair.Key, pair.Value);
        }
    }

    private static void AppendIssue(
        ICollection<string?> fields,
        string marker,
        ValidationIssue issue)
    {
        fields.Add(marker);
        fields.Add(issue.FilePath);
        fields.Add(issue.Severity.ToString());
        fields.Add(issue.Message);
        fields.Add(issue.Category.ToString());
        fields.Add(issue.Code);
        fields.Add(issue.Actor);
        fields.Add(issue.Section);
        fields.Add(issue.Expected);
        fields.Add(issue.Actual);
        fields.Add(issue.RepairHint);
        fields.Add(issue.RepairTargetFiles.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var path in issue.RepairTargetFiles)
            fields.Add(path);
        fields.Add(issue.FactionRepairClassification?.ToString());

        var item = issue.MortalItemRepairContext;
        fields.Add(item == null ? "no_mortal_item_context" : "mortal_item_context");
        if (item != null)
        {
            fields.Add(item.Coordinate);
            fields.Add(item.TransitionClass);
            fields.Add(item.Route);
            AppendCarrier(fields, item.SourceCarrier);
            AppendCarrier(fields, item.DestinationCarrier);
            fields.Add(item.ExpectedAuthority);
            fields.Add(item.ActualEvidence);
            fields.Add(item.RequiredCompanionTargets.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var target in item.RequiredCompanionTargets)
                fields.Add(target);
        }

        var location = issue.MortalLocationRepairContext;
        fields.Add(location == null
            ? "no_mortal_location_context"
            : "mortal_location_context");
        if (location != null)
        {
            fields.Add(location.CarrierPath);
            fields.Add(location.EntityKind);
            fields.Add(location.InitialId);
            fields.Add(location.MaterializationId);
            fields.Add(location.RepairableFields.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var field in location.RepairableFields)
                fields.Add(field);
            fields.Add(location.ExistingId);
            fields.Add(location.ExpectedSourceTurn?.ToString(CultureInfo.InvariantCulture));
            fields.Add(location.ExpectedSourceAuthorityKind);
            fields.Add(location.ExpectedSourceAuthorityId);
        }

        var effect = issue.EffectRepairContext;
        fields.Add(effect == null ? "no_effect_context" : "effect_context");
        if (effect != null)
        {
            fields.Add(effect.Actor);
            fields.Add(effect.Route);
            fields.Add(effect.RawCoordinate);
            fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(effect.ExpectedSource));
            fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(effect.ExpectedTarget));
            fields.Add(effect.ExpectedDefinitionKey);
            fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(effect.ExpectedEventRef));
            fields.Add(effect.ExpectedValueJson);
        }

        var wound = issue.WoundRepairContext;
        fields.Add(wound == null ? "no_wound_context" : "wound_context");
        if (wound != null)
        {
            fields.Add(wound.SessionId);
            fields.Add(wound.RequestId);
            fields.Add(wound.SnapshotToken);
            fields.Add(wound.Kind);
            fields.Add(wound.CandidateRef);
            fields.Add(wound.SemanticFingerprint);
            fields.Add(wound.OpportunityRef);
            fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(wound.SafeContext));
            fields.Add(wound.AllowedDecisions.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var decision in wound.AllowedDecisions)
                fields.Add(decision);
            fields.Add(wound.MinimumSeverity);
            fields.Add(wound.MaximumSeverity);
            fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(wound.RejectedDecision));
            fields.Add(wound.OpportunityAuthorityFingerprint);
        }
    }

    private static void AppendCarrier(
        ICollection<string?> fields,
        MortalItemCarrierCoordinate? carrier)
    {
        fields.Add(carrier == null ? "no_carrier" : "carrier");
        if (carrier == null)
            return;
        fields.Add(carrier.Kind);
        fields.Add(carrier.OwnerId);
        fields.Add(carrier.ContainerId);
        fields.Add(carrier.ContainerPath.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var segment in carrier.ContainerPath)
            fields.Add(segment);
    }

    private static ValidationIssue CatalogIssue(MortalItemCarrierCatalogIssue issue) =>
        Issue(
            issue.Identity ?? "catalog",
            issue.Code,
            "one exact unambiguous carrier graph",
            issue.Message);

    private static ValidationIssue Issue(
        string itemId,
        string code,
        string expected,
        string actual) => new(
        MortalItemIdentityState.StatePath,
        IssueSeverity.Error,
        "Mortal item consumption planning failed.",
        code: code,
        actor: "mortal_item:" + itemId,
        section: "MortalItemMaterialization",
        expected: expected,
        actual: actual,
        repairHint: "Reject the complete item consumption plan and rebuild its sealed authority.",
        repairTargetFiles: new[] { MortalItemIdentityState.StatePath });

    private static string? ReadExactString(JsonNode? node) =>
        node is JsonValue value &&
        value.TryGetValue<string>(out var text) &&
        ResourceMaterializationContract.IsExactIdentifier(text)
            ? text
            : null;

    private static bool TryReadPositiveInt(JsonNode? node, out int value)
    {
        value = 0;
        return node is JsonValue scalar && scalar.TryGetValue(out value) && value > 0;
    }
}
