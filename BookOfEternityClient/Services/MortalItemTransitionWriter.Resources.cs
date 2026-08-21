using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed partial class MortalItemTransitionWriter
{
    private async Task<ItemResourcePreparation> PrepareTerminalItemResourcesAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalItemTransitionIntent intent,
        string itemId)
    {
        var loaded = await LoadItemResourcesAsync(writeLease, intent.Turn);
        if (!loaded.Success)
            return ItemResourcePreparation.Failed(loaded.Error!);
        var coordinates = loaded.State!.Entries
            .Where(entry =>
                entry.Coordinate.OwnerKind == ResourceOwnerKind.Item &&
                string.Equals(
                    entry.Coordinate.ResourceOwnerId,
                    itemId,
                    StringComparison.Ordinal))
            .OrderBy(entry => entry.Coordinate.ResourceKey, StringComparer.Ordinal)
            .ToArray();
        if (coordinates.Length == 0)
            return ItemResourcePreparation.Empty;

        var capacityTransitions = coordinates
            .Select((entry, index) => CreateRetirementIntent(
                intent,
                entry,
                index + 1))
            .ToArray();
        return BuildItemResourcePlan(
            loaded,
            intent,
            Array.Empty<ResourceMutationIntent>(),
            capacityTransitions);
    }

    private async Task<ItemResourcePreparation> PrepareSplitItemResourcesAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalItemTransitionIntent intent,
        string sourceItemId,
        string childItemId,
        int sourceQuantity,
        int childQuantity)
    {
        var loaded = await LoadItemResourcesAsync(writeLease, intent.Turn);
        if (!loaded.Success)
            return ItemResourcePreparation.Failed(loaded.Error!);
        var sourceEntries = loaded.State!.Entries
            .Where(entry =>
                entry.Coordinate.OwnerKind == ResourceOwnerKind.Item &&
                string.Equals(
                    entry.Coordinate.ResourceOwnerId,
                    sourceItemId,
                    StringComparison.Ordinal))
            .OrderBy(entry => entry.Coordinate.ResourceKey, StringComparer.Ordinal)
            .ToArray();
        if (sourceEntries.Length == 0)
            return ItemResourcePreparation.Empty;
        if (intent.ResourceDisposition != MortalItemResourceStackDisposition.ProportionalExact)
        {
            return ItemResourcePreparation.Failed(
                "Resource-bearing split требует явный зарегистрированный proportional_exact disposition.");
        }

        var capacities = new List<ResourceCapacityIntent>();
        var mutations = new List<ResourceMutationIntent>();
        var ordinal = 0;
        foreach (var source in sourceEntries)
        {
            if (!TryResolveStackDefinition(
                    loaded.Definitions!,
                    source,
                    out var definition,
                    out var definitionError))
            {
                return ItemResourcePreparation.Failed(definitionError!);
            }
            if (!ResourceMaterializationContract.TryScaleRatioExact(
                    source.Maximum,
                    childQuantity,
                    sourceQuantity,
                    out var childMaximum) ||
                !ResourceMaterializationContract.TrySubtractExact(
                    source.Maximum,
                    childMaximum,
                    out var parentMaximum) ||
                !ResourceMaterializationContract.TryScaleRatioExact(
                    source.Current,
                    childQuantity,
                    sourceQuantity,
                    out var childCurrent) ||
                !ResourceMaterializationContract.TrySubtractExact(
                    source.Current,
                    childCurrent,
                    out var parentCurrent) ||
                childMaximum <= 0m || parentMaximum <= 0m ||
                !ResourceMaterializationContract.IsQuantumAligned(
                    childMaximum,
                    definition!.MinimumPolicy.Value,
                    definition.Quantum) ||
                !ResourceMaterializationContract.IsQuantumAligned(
                    parentMaximum,
                    definition.MinimumPolicy.Value,
                    definition.Quantum) ||
                !ResourceMaterializationContract.IsQuantumAligned(
                    childCurrent,
                    definition.MinimumPolicy.Value,
                    definition.Quantum) ||
                !ResourceMaterializationContract.IsQuantumAligned(
                    parentCurrent,
                    definition.MinimumPolicy.Value,
                    definition.Quantum))
            {
                return ItemResourcePreparation.Failed(
                    $"Resource '{source.Coordinate.ResourceKey}' нельзя разделить proportional_exact без округления.");
            }

            var parentResolved = ResolveStackCapacity(
                definition,
                source.Coordinate,
                parentMaximum,
                intent,
                "split_parent",
                includeInitialization: false);
            if (!parentResolved.IsValid || parentResolved.Capacity == null)
                return ItemResourcePreparation.Failed(DescribeResourceIssues(parentResolved.Issues));
            ordinal++;
            capacities.Add(CreateCapacityIntent(
                intent,
                source.Coordinate,
                ResourceCapacityOperation.Reconfigure,
                parentResolved.Capacity,
                ResourceCurrentDisposition.ScaleRatioExact,
                ordinal,
                "item_stack_split"));

            var childCoordinate = source.Coordinate with { ResourceOwnerId = childItemId };
            var childResolved = ResolveStackCapacity(
                definition,
                childCoordinate,
                childMaximum,
                intent,
                "split_child",
                includeInitialization: true);
            if (!childResolved.IsValid || childResolved.Capacity == null ||
                childResolved.Capacity.Initialization == null)
            {
                return ItemResourcePreparation.Failed(DescribeResourceIssues(childResolved.Issues));
            }
            ordinal++;
            capacities.Add(CreateCapacityIntent(
                intent,
                childCoordinate,
                ResourceCapacityOperation.Initialize,
                childResolved.Capacity,
                ResourceCurrentDisposition.InitializeFromDefinition,
                ordinal,
                "item_stack_split"));
            if (!TryAddAdjustmentMutation(
                    mutations,
                    intent,
                    definition,
                    childCoordinate,
                    childResolved.Capacity.Initialization.Current,
                    childCurrent,
                    ref ordinal,
                    out var adjustmentError))
            {
                return ItemResourcePreparation.Failed(adjustmentError!);
            }
        }

        return BuildItemResourcePlan(loaded, intent, mutations, capacities);
    }

    private async Task<ItemResourcePreparation> PrepareMergeItemResourcesAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalItemTransitionIntent intent,
        IReadOnlyList<string> sourceItemIds,
        string survivorItemId)
    {
        var loaded = await LoadItemResourcesAsync(writeLease, intent.Turn);
        if (!loaded.Success)
            return ItemResourcePreparation.Failed(loaded.Error!);
        var sourceIds = sourceItemIds.ToHashSet(StringComparer.Ordinal);
        var entries = loaded.State!.Entries
            .Where(entry =>
                entry.Coordinate.OwnerKind == ResourceOwnerKind.Item &&
                sourceIds.Contains(entry.Coordinate.ResourceOwnerId))
            .OrderBy(entry => entry.Coordinate.ResourceKey, StringComparer.Ordinal)
            .ThenBy(entry => entry.Coordinate.ResourceOwnerId, StringComparer.Ordinal)
            .ToArray();
        if (entries.Length == 0)
            return ItemResourcePreparation.Empty;
        if (intent.ResourceDisposition != MortalItemResourceStackDisposition.ProportionalExact)
        {
            return ItemResourcePreparation.Failed(
                "Resource-bearing merge требует явный зарегистрированный proportional_exact disposition.");
        }

        var capacities = new List<ResourceCapacityIntent>();
        var mutations = new List<ResourceMutationIntent>();
        var ordinal = 0;
        foreach (var group in entries.GroupBy(
                     entry => entry.Coordinate.ResourceKey,
                     StringComparer.Ordinal))
        {
            var ordered = group.OrderBy(
                    entry => entry.Coordinate.ResourceOwnerId,
                    StringComparer.Ordinal)
                .ToArray();
            if (!TryResolveStackDefinition(
                    loaded.Definitions!,
                    ordered[0],
                    out var definition,
                    out var definitionError))
            {
                return ItemResourcePreparation.Failed(definitionError!);
            }
            if (ordered.Any(entry => !TryResolveStackDefinition(
                    loaded.Definitions!,
                    entry,
                    out _,
                    out _)))
            {
                return ItemResourcePreparation.Failed(
                    $"Resource '{group.Key}' имеет несовместимую stack authority.");
            }

            if (!TrySumExact(ordered.Select(static entry => entry.Maximum), out var totalMaximum) ||
                !TrySumExact(ordered.Select(static entry => entry.Current), out var totalCurrent))
            {
                return ItemResourcePreparation.Failed(
                    $"Resource '{group.Key}' нельзя объединить без потери decimal precision.");
            }

            var survivor = ordered.SingleOrDefault(entry => string.Equals(
                entry.Coordinate.ResourceOwnerId,
                survivorItemId,
                StringComparison.Ordinal));
            var survivorCoordinate = new ResourceCoordinate(
                "mortal_world",
                ResourceOwnerKind.Item,
                survivorItemId,
                group.Key);
            decimal beforeAdjustment;
            if (survivor == null)
            {
                var resolved = ResolveStackCapacity(
                    definition!,
                    survivorCoordinate,
                    totalMaximum,
                    intent,
                    "merge_survivor_initialize",
                    includeInitialization: true);
                if (!resolved.IsValid || resolved.Capacity?.Initialization == null)
                    return ItemResourcePreparation.Failed(DescribeResourceIssues(resolved.Issues));
                ordinal++;
                capacities.Add(CreateCapacityIntent(
                    intent,
                    survivorCoordinate,
                    ResourceCapacityOperation.Initialize,
                    resolved.Capacity,
                    ResourceCurrentDisposition.InitializeFromDefinition,
                    ordinal,
                    "item_stack_merge"));
                beforeAdjustment = resolved.Capacity.Initialization.Current;
            }
            else
            {
                beforeAdjustment = survivor.Current;
                if (survivor.Maximum != totalMaximum)
                {
                    var resolved = ResolveStackCapacity(
                        definition!,
                        survivorCoordinate,
                        totalMaximum,
                        intent,
                        "merge_survivor_reconfigure",
                        includeInitialization: false);
                    if (!resolved.IsValid || resolved.Capacity == null)
                        return ItemResourcePreparation.Failed(DescribeResourceIssues(resolved.Issues));
                    ordinal++;
                    capacities.Add(CreateCapacityIntent(
                        intent,
                        survivorCoordinate,
                        ResourceCapacityOperation.Reconfigure,
                        resolved.Capacity,
                        ResourceCurrentDisposition.Preserve,
                        ordinal,
                        "item_stack_merge"));
                }
            }

            if (!TryAddAdjustmentMutation(
                    mutations,
                    intent,
                    definition!,
                    survivorCoordinate,
                    beforeAdjustment,
                    totalCurrent,
                    ref ordinal,
                    out var adjustmentError))
            {
                return ItemResourcePreparation.Failed(adjustmentError!);
            }

            foreach (var contributor in ordered.Where(entry => !string.Equals(
                         entry.Coordinate.ResourceOwnerId,
                         survivorItemId,
                         StringComparison.Ordinal)))
            {
                ordinal++;
                capacities.Add(CreateRetirementIntent(intent, contributor, ordinal));
            }
        }

        return BuildItemResourcePlan(loaded, intent, mutations, capacities);
    }

    private async Task<ItemResourceLoadResult> LoadItemResourcesAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        int turn)
    {
        var definitionsJson = await _fs.ReadFileAsync(
            writeLease,
            ResourceMaterializationContract.DefinitionsPath);
        var stateJson = await _fs.ReadFileAsync(
            writeLease,
            ResourceMaterializationContract.StatePath);
        var historyJson = await _fs.ReadFileAsync(
            writeLease,
            ResourceMaterializationContract.HistoryPath);
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            definitionsJson,
            allowMissingPristine: false);
        if (!definitions.IsValid || definitions.Catalog == null)
            return ItemResourceLoadResult.Failed(DescribeResourceIssues(definitions.Issues));
        var state = ResourceStateContract.ParseCanonical(
            stateJson,
            definitions.Catalog,
            allowMissingPristine: false);
        if (!state.IsValid || state.Ledger == null)
            return ItemResourceLoadResult.Failed(DescribeResourceIssues(state.Issues));
        var history = ResourceHistoryState.ParseCanonical(
            historyJson,
            definitions.Catalog,
            allowMissingPristine: false);
        if (!history.IsValid || history.History == null)
            return ItemResourceLoadResult.Failed(DescribeResourceIssues(history.Issues));
        var agreement = history.History.ValidateStateAgreement(state.Ledger);
        if (agreement.Count != 0)
            return ItemResourceLoadResult.Failed(DescribeResourceIssues(agreement));
        var currentTurnSequences = history.History.Transitions
            .Where(transition => transition.Turn == turn)
            .Select(transition => transition.ExecutionSequence)
            .ToArray();
        if (currentTurnSequences.Contains(int.MaxValue))
            return ItemResourceLoadResult.Failed("Resource history исчерпал executionSequence этого хода.");
        var offset = currentTurnSequences.Length == 0
            ? 0
            : currentTurnSequences.Max() + 1;
        return new ItemResourceLoadResult(
            true,
            definitions.Catalog,
            state.Ledger,
            history.History,
            definitionsJson!,
            stateJson!,
            historyJson!,
            offset,
            null);
    }

    private static ItemResourcePreparation BuildItemResourcePlan(
        ItemResourceLoadResult loaded,
        MortalItemTransitionIntent intent,
        IReadOnlyList<ResourceMutationIntent> mutations,
        IReadOnlyList<ResourceCapacityIntent> capacities)
    {
        if (mutations.Count == 0 && capacities.Count == 0)
            return ItemResourcePreparation.Empty;
        var sourceFingerprint = CreateItemResourceAuthorityFingerprint(
            intent,
            "mutation_source");
        var sources = ResourceMutationSourceCatalog.Create(
            mutations.Count == 0
                ? Array.Empty<ResourceMutationSourceExport>()
                : new[]
                {
                    new ResourceMutationSourceExport(
                        "registered_system_outcome",
                        intent.AuthorityId,
                        sourceFingerprint,
                        ResourceMutationSourceState.Active,
                        SameTurn: true)
                });
        if (!sources.IsValid || sources.Catalog == null)
            return ItemResourcePreparation.Failed(DescribeResourceIssues(sources.Issues));
        var planned = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                intent.Turn,
                loaded.Definitions!,
                loaded.State!,
                loaded.History!,
                sources.Catalog,
                mutations,
                capacities,
                loaded.ExecutionSequenceOffset),
            new AcceptedMechanicsIdentityFactory());
        if (!planned.IsValid || planned.StateAfterImage == null ||
            planned.HistoryAfterImage == null)
        {
            return ItemResourcePreparation.Failed(DescribeResourceIssues(planned.Issues));
        }

        return new ItemResourcePreparation(
            true,
            new[]
            {
                CoordinatedStateWriteHelper.CreateGuardWrite(
                    ResourceMaterializationContract.DefinitionsPath,
                    loaded.DefinitionsJson),
                new CoordinatedStateWriteHelper.PlannedWrite(
                    ResourceMaterializationContract.StatePath,
                    loaded.StateJson,
                    PrettyCanonical(planned.StateAfterImage.ToCanonicalJson()),
                    RequireCurrentBaseline: true),
                new CoordinatedStateWriteHelper.PlannedWrite(
                    ResourceMaterializationContract.HistoryPath,
                    loaded.HistoryJson,
                    PrettyCanonical(planned.HistoryAfterImage.ToCanonicalJson()),
                    RequireCurrentBaseline: true)
            },
            null);
    }

    private static ResourceCapacityIntent CreateRetirementIntent(
        MortalItemTransitionIntent intent,
        ResourceStateEntry entry,
        int ordinal)
    {
        var fingerprint = CreateItemResourceAuthorityFingerprint(
            intent,
            "retire",
            entry.Coordinate,
            entry.Chronology.LastTransitionId);
        return new ResourceCapacityIntent(
            $"turn_{intent.Turn}:item_resource_retire:{ordinal}",
            "owner_lifecycle",
            entry.Coordinate.ResourceOwnerId,
            entry.Coordinate,
            ResourceCapacityOperation.Retire,
            ResolvedCapacity: null,
            CurrentDisposition: null,
            ResourceMutationPhase.RegisteredSystemOutcome,
            Priority: 90,
            new ResourceSourceEvidence(
                "owner_lifecycle",
                entry.Coordinate.ResourceOwnerId,
                fingerprint),
            fingerprint,
            ReceiptId: null);
    }

    private static ResourceCapacityIntent CreateCapacityIntent(
        MortalItemTransitionIntent intent,
        ResourceCoordinate coordinate,
        ResourceCapacityOperation operation,
        ResolvedResourceCapacity resolved,
        ResourceCurrentDisposition disposition,
        int ordinal,
        string originKind)
    {
        var fingerprint = CreateItemResourceAuthorityFingerprint(
            intent,
            originKind,
            coordinate,
            resolved.Binding.AuthorityFingerprint);
        return new ResourceCapacityIntent(
            $"turn_{intent.Turn}:{originKind}:{ordinal}",
            originKind,
            intent.AuthorityId,
            coordinate,
            operation,
            resolved,
            disposition,
            ResourceMutationPhase.RegisteredSystemOutcome,
            Priority: 80,
            new ResourceSourceEvidence(
                originKind,
                intent.AuthorityId,
                fingerprint),
            operation == ResourceCapacityOperation.Initialize
                ? resolved.Initialization!.AuthorityFingerprint
                : resolved.Binding.AuthorityFingerprint,
            ReceiptId: null);
    }

    private static ResolvedResourceCapacityResult ResolveStackCapacity(
        ResourceDefinition definition,
        ResourceCoordinate coordinate,
        decimal maximum,
        MortalItemTransitionIntent intent,
        string purpose,
        bool includeInitialization)
    {
        var capacityFingerprint = CreateItemResourceAuthorityFingerprint(
            intent,
            purpose,
            coordinate,
            maximum.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return ResolvedResourceCapacity.Resolve(
            definition,
            coordinate,
            new InstanceFixedCapacityInput(
                new ResourceFormulaOwner(
                    coordinate.Realm,
                    coordinate.OwnerKind,
                    coordinate.ResourceOwnerId),
                maximum,
                capacityFingerprint),
            intent.AuthorityId,
            includeInitialization);
    }

    private static bool TryResolveStackDefinition(
        ResourceDefinitionCatalog definitions,
        ResourceStateEntry entry,
        out ResourceDefinition? definition,
        out string? error)
    {
        definitions.TryResolveExact(entry.Coordinate.ResourceKey, out definition);
        if (definition == null ||
            definition.CapacityPolicy.Kind != ResourceCapacityKind.InstanceFixed ||
            entry.CapacityBinding.Kind != ResourceCapacityKind.InstanceFixed ||
            entry.State != ResourceLifecycleState.Active)
        {
            error =
                $"Resource '{entry.Coordinate.ResourceKey}' не поддерживает зарегистрированный proportional_exact stack transition.";
            return false;
        }
        error = null;
        return true;
    }

    private static bool TryAddAdjustmentMutation(
        List<ResourceMutationIntent> mutations,
        MortalItemTransitionIntent intent,
        ResourceDefinition definition,
        ResourceCoordinate coordinate,
        decimal before,
        decimal after,
        ref int ordinal,
        out string? error)
    {
        error = null;
        if (before == after)
            return true;
        ResourceOperation operation;
        decimal amount;
        if (after < before)
        {
            if (!ResourceMaterializationContract.TrySubtractExact(before, after, out amount))
            {
                error = $"Resource '{coordinate.ResourceKey}' adjustment теряет decimal precision.";
                return false;
            }
            operation = definition.AllowedOperations.Contains(ResourceOperation.Damage)
                ? ResourceOperation.Damage
                : definition.AllowedOperations.Contains(ResourceOperation.Spend)
                    ? ResourceOperation.Spend
                    : default;
            if (!definition.AllowedOperations.Contains(operation))
            {
                error = $"Resource '{coordinate.ResourceKey}' не имеет зарегистрированной операции уменьшения.";
                return false;
            }
        }
        else
        {
            if (!ResourceMaterializationContract.TrySubtractExact(after, before, out amount))
            {
                error = $"Resource '{coordinate.ResourceKey}' adjustment теряет decimal precision.";
                return false;
            }
            operation = definition.AllowedOperations.Contains(ResourceOperation.Restore)
                ? ResourceOperation.Restore
                : definition.AllowedOperations.Contains(ResourceOperation.Gain)
                    ? ResourceOperation.Gain
                    : default;
            if (!definition.AllowedOperations.Contains(operation))
            {
                error = $"Resource '{coordinate.ResourceKey}' не имеет зарегистрированной операции увеличения.";
                return false;
            }
        }

        ordinal++;
        mutations.Add(new ResourceMutationIntent(
            $"turn_{intent.Turn}:item_stack_adjust:{ordinal}",
            coordinate,
            amount,
            new ResourceMutationSourceRequest(
                "registered_system_outcome",
                intent.AuthorityId,
                operation),
            Array.Empty<ResourceOperationKey>(),
            Array.Empty<ResourceMutationEventRequirement>(),
            ReceiptId: null));
        return true;
    }

    private static bool TrySumExact(IEnumerable<decimal> values, out decimal result)
    {
        result = 0m;
        foreach (var value in values)
        {
            if (!ResourceMaterializationContract.TryAddExact(result, value, out result))
                return false;
        }
        return true;
    }

    private static string CreateItemResourceAuthorityFingerprint(
        MortalItemTransitionIntent intent,
        string purpose,
        ResourceCoordinate? coordinate = null,
        string? value = null)
    {
        using var fingerprint = new ResourceFingerprintBuilder(
            "mortal-item-resource-transition-v1");
        fingerprint.Append(intent.Kind.ToString());
        fingerprint.Append(intent.Turn);
        fingerprint.Append(intent.AuthorityKind);
        fingerprint.Append(intent.AuthorityId);
        fingerprint.Append(purpose);
        foreach (var sourceId in intent.SourceItemIds.OrderBy(
                     static itemId => itemId,
                     StringComparer.Ordinal))
        {
            fingerprint.Append(sourceId);
        }
        fingerprint.Append(intent.SurvivorItemId ?? string.Empty);
        fingerprint.Append(intent.ResourceDisposition?.ToString() ?? string.Empty);
        if (coordinate != null)
            ResourceStateContract.AppendCoordinate(fingerprint, coordinate);
        fingerprint.Append(value ?? string.Empty);
        return fingerprint.Build();
    }

    private static string PrettyCanonical(string json) =>
        JsonNode.Parse(json)!.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);

    private static string DescribeResourceIssues(IReadOnlyList<ValidationIssue> issues) =>
        issues.Count == 0
            ? "Resource transition не сформировал допустимый after-image."
            : $"Resource transition отклонён: {issues[0].Code}: {issues[0].Message}";

    private sealed record ItemResourcePreparation(
        bool Success,
        IReadOnlyList<CoordinatedStateWriteHelper.PlannedWrite> Writes,
        string? Error)
    {
        internal static ItemResourcePreparation Empty { get; } =
            new(true, Array.Empty<CoordinatedStateWriteHelper.PlannedWrite>(), null);

        internal static ItemResourcePreparation Failed(string error) =>
            new(false, Array.Empty<CoordinatedStateWriteHelper.PlannedWrite>(), error);
    }

    private sealed record ItemResourceLoadResult(
        bool Success,
        ResourceDefinitionCatalog? Definitions,
        ResourceStateLedger? State,
        ResourceHistoryState? History,
        string? DefinitionsJson,
        string? StateJson,
        string? HistoryJson,
        int ExecutionSequenceOffset,
        string? Error)
    {
        internal static ItemResourceLoadResult Failed(string error) =>
            new(false, null, null, null, null, null, null, 0, error);
    }
}
