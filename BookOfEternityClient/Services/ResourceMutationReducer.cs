using System.Collections.ObjectModel;
using System.Collections.Immutable;

namespace BookOfEternityClient.Services;

internal sealed record ResourceOperationKey(
    string EventRef,
    string OriginKind,
    string OriginId,
    ResourceCoordinate Coordinate,
    ResourceOperation Operation);

internal sealed record ResourcePolicyBinding(
    ResourceBoundPolicy FloorPolicy,
    ResourceBoundPolicy CapPolicy,
    string AuthorityFingerprint);

internal sealed record AuthorizedResourceMutation(
    string TransitionId,
    string OperationId,
    string EventRef,
    string OriginKind,
    string OriginId,
    ResourceCoordinate Coordinate,
    ResourceOperation Operation,
    decimal Amount,
    ResourceMutationPhase Phase,
    int Priority,
    int ExecutionSequence,
    ResourcePolicyBinding PolicyBinding,
    IReadOnlyList<ResourceOperationKey> Dependencies,
    ResourceSourceEvidence SourceEvidence,
    string? ReceiptId,
    int Turn,
    ResourceMutationResultConstraint? ResultConstraint = null);

internal sealed record ResolvedResourceInitialization(
    decimal Current,
    string AuthorityFingerprint);

internal sealed record ResolvedResourceCapacityResult(
    ResolvedResourceCapacity? Capacity,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Capacity != null && Issues.Count == 0;
}

internal sealed class ResolvedResourceCapacity
{
    private ResolvedResourceCapacity(
        decimal maximum,
        ResourceCapacityBinding binding,
        ResolvedResourceInitialization? initialization)
    {
        Maximum = maximum;
        Binding = binding;
        Initialization = initialization;
    }

    internal decimal Maximum { get; }
    internal ResourceCapacityBinding Binding { get; }
    internal ResolvedResourceInitialization? Initialization { get; }

    internal ResolvedResourceCapacity AsReconfiguration() =>
        new(Maximum, Binding, initialization: null);

    internal static ResolvedResourceCapacityResult Resolve(
        ResourceDefinition definition,
        ResourceCoordinate coordinate,
        ResourceCapacityInput capacityInput,
        string? instanceAuthorityKey,
        bool includeInitialization,
        ResourceFormulaInput? initializationFormulaInput = null,
        string? expectedCapacityFingerprint = null,
        string? expectedInitializationFingerprint = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(coordinate);
        ArgumentNullException.ThrowIfNull(capacityInput);
        var issues = new List<ValidationIssue>();
        if (!OwnerMatches(capacityInput.Owner, coordinate))
        {
            AddResolutionIssue(
                issues,
                "resource_capacity_owner_mismatch",
                "capacity input owner equals the exact resource coordinate",
                DescribeOwner(capacityInput.Owner));
            return new ResolvedResourceCapacityResult(null, issues.ToArray());
        }

        var capacityResult = ResourceCapacityFormulaCatalog.ResolveCapacity(
            definition,
            capacityInput,
            expectedCapacityFingerprint);
        if (!capacityResult.IsValid)
            return new ResolvedResourceCapacityResult(null, capacityResult.Issues);

        var authorityKey = definition.CapacityPolicy.Kind switch
        {
            ResourceCapacityKind.DefinitionFixed when instanceAuthorityKey == null =>
                definition.ResourceKey,
            ResourceCapacityKind.InstanceFixed when
                ResourceMaterializationContract.IsExactIdentifier(instanceAuthorityKey) =>
                instanceAuthorityKey!,
            ResourceCapacityKind.RegisteredFormula when
                instanceAuthorityKey == null && definition.CapacityPolicy.FormulaKey != null =>
                definition.CapacityPolicy.FormulaKey,
            _ => null
        };
        if (authorityKey == null)
        {
            AddResolutionIssue(
                issues,
                "resource_capacity_authority_key_invalid",
                "one definition-compatible exact capacity authority key",
                instanceAuthorityKey ?? "null");
            return new ResolvedResourceCapacityResult(null, issues.ToArray());
        }

        var binding = new ResourceCapacityBinding(
            definition.CapacityPolicy.Kind,
            authorityKey,
            capacityResult.AuthorityFingerprint!);
        ResolvedResourceInitialization? initialization = null;
        if (includeInitialization)
        {
            ResourceInitializationInput? initializationInput =
                definition.InitializationPolicy.Kind ==
                    ResourceInitializationKind.RegisteredFormula
                    ? initializationFormulaInput == null
                        ? null
                        : new RegisteredFormulaResourceInitializationInput(
                            initializationFormulaInput,
                            capacityResult.Value!.Value,
                            binding.AuthorityFingerprint)
                    : initializationFormulaInput == null
                        ? new SealedPolicyResourceInitializationInput(
                            capacityInput.Owner,
                            capacityResult.Value!.Value,
                            binding.AuthorityFingerprint)
                        : null;
            if (initializationInput == null ||
                !OwnerMatches(initializationInput.Owner, coordinate))
            {
                AddResolutionIssue(
                    issues,
                    "resource_initialization_input_invalid",
                    "one exact definition-compatible typed initialization input",
                    initializationFormulaInput?.GetType().Name ?? "missing");
                return new ResolvedResourceCapacityResult(null, issues.ToArray());
            }

            var initializationResult = ResourceCapacityFormulaCatalog.ResolveInitialValue(
                definition,
                initializationInput,
                expectedInitializationFingerprint);
            if (!initializationResult.IsValid)
                return new ResolvedResourceCapacityResult(null, initializationResult.Issues);
            initialization = new ResolvedResourceInitialization(
                initializationResult.Value!.Value,
                initializationResult.AuthorityFingerprint!);
        }
        else if (initializationFormulaInput != null ||
                 expectedInitializationFingerprint != null)
        {
            AddResolutionIssue(
                issues,
                "resource_initialization_input_unexpected",
                "no initialization authority outside initialize",
                initializationFormulaInput?.GetType().Name ??
                expectedInitializationFingerprint ?? "unknown");
            return new ResolvedResourceCapacityResult(null, issues.ToArray());
        }

        return new ResolvedResourceCapacityResult(
            new ResolvedResourceCapacity(
                capacityResult.Value!.Value,
                binding,
                initialization),
            Array.Empty<ValidationIssue>());
    }

    private static bool OwnerMatches(
        ResourceFormulaOwner owner,
        ResourceCoordinate coordinate) =>
        string.Equals(owner.Realm, coordinate.Realm, StringComparison.Ordinal) &&
        owner.OwnerKind == coordinate.OwnerKind &&
        string.Equals(
            owner.ResourceOwnerId,
            coordinate.ResourceOwnerId,
            StringComparison.Ordinal);

    private static string DescribeOwner(ResourceFormulaOwner owner) =>
        $"{owner.Realm}/" +
        $"{ResourceDefinitionCatalog.GetOwnerKindToken(owner.OwnerKind)}/" +
        owner.ResourceOwnerId;

    private static void AddResolutionIssue(
        List<ValidationIssue> issues,
        string code,
        string expected,
        string actual) =>
        ResourceMaterializationContract.AddIssue(
            issues,
            ResourceMaterializationContract.StatePath,
            code,
            expected,
            actual);
}

internal sealed record AuthorizedResourceCapacityTransition(
    string TransitionId,
    string OperationId,
    string EventRef,
    string OriginKind,
    string OriginId,
    ResourceCoordinate Coordinate,
    ResourceCapacityOperation Operation,
    ResolvedResourceCapacity? ResolvedCapacity,
    ResourceCurrentDisposition? CurrentDisposition,
    ResourceMutationPhase Phase,
    int Priority,
    int ExecutionSequence,
    ResourceSourceEvidence SourceEvidence,
    string PolicyFingerprint,
    string? ReceiptId,
    int Turn);

internal sealed record ResourceMutationResult(
    ResourceWorkingLedger? WorkingLedger,
    IReadOnlyList<ResourceAppliedEvent> Events,
    ResourceTransition? Transition,
    ResourceTransition? ReplayTransition,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid =>
        WorkingLedger != null && Issues.Count == 0 &&
        (Transition != null || ReplayTransition != null);
}

internal sealed record ResourceCapacityResult(
    ResourceWorkingLedger? WorkingLedger,
    ResourceTransition? Transition,
    ResourceTransition? ReplayTransition,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid =>
        WorkingLedger != null && Issues.Count == 0 &&
        (Transition != null || ReplayTransition != null);
}

internal sealed class ResourceWorkingLedger
{
    private readonly ImmutableDictionary<ResourceCoordinate, ResourceStateEntry> _entries;

    internal ResourceWorkingLedger(IEnumerable<ResourceStateEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = entries.ToImmutableDictionary(
            static value => value.Coordinate,
            static value => value,
            ResourceCoordinateComparer.Instance);
    }

    private ResourceWorkingLedger(
        ImmutableDictionary<ResourceCoordinate, ResourceStateEntry> entries) =>
        _entries = entries;

    internal static ResourceWorkingLedger Empty => new(Array.Empty<ResourceStateEntry>());

    internal IReadOnlyList<ResourceStateEntry> Entries =>
        new ReadOnlyCollection<ResourceStateEntry>(_entries.Values
            .OrderBy(static entry => entry.Coordinate.Realm, StringComparer.Ordinal)
            .ThenBy(static entry => entry.Coordinate.OwnerKind)
            .ThenBy(static entry => entry.Coordinate.ResourceOwnerId, StringComparer.Ordinal)
            .ThenBy(static entry => entry.Coordinate.ResourceKey, StringComparer.Ordinal)
            .ToArray());

    internal int Count => _entries.Count;

    internal string Fingerprint => Freeze().Fingerprint;

    internal bool TryResolve(
        ResourceCoordinate coordinate,
        out ResourceStateEntry? entry) =>
        _entries.TryGetValue(coordinate, out entry);

    internal ResourceStateEntry Resolve(ResourceCoordinate coordinate) =>
        _entries.TryGetValue(coordinate, out var entry)
            ? entry
            : throw new KeyNotFoundException(
                $"Resource coordinate '{coordinate}' is not present in the working ledger.");

    internal ResourceStateLedger Freeze() => new(_entries.Values);

    internal ResourceWorkingLedger WithEntry(ResourceStateEntry entry) =>
        new(_entries.SetItem(entry.Coordinate, entry));

    internal ResourceWorkingLedger Without(ResourceCoordinate coordinate) =>
        new(_entries.Remove(coordinate));
}

internal static class ResourceMutationReducer
{
    internal static ResourceMutationResult Reduce(
        ResourceWorkingLedger ledger,
        ResourceHistoryWorkingSet history,
        AuthorizedResourceMutation mutation,
        ResourceDefinitionCatalog definitions)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(mutation);
        ArgumentNullException.ThrowIfNull(definitions);
        var issues = ValidateCommonMutation(mutation, definitions, out var definition);
        if (issues.Count != 0 || definition == null)
            return MutationFailure(issues);

        var transitionOperation = ToTransitionOperation(mutation.Operation);
        var probe = new ResourceReplayProbe(
            mutation.EventRef,
            mutation.OriginKind,
            mutation.OriginId,
            mutation.Coordinate,
            transitionOperation,
            mutation.Amount,
            mutation.Phase,
            mutation.Priority,
            mutation.ExecutionSequence,
            null,
            mutation.SourceEvidence,
            mutation.PolicyBinding.AuthorityFingerprint,
            mutation.ReceiptId);
        var replay = history.ResolveReplay(probe);
        if (replay.Disposition == ResourceReplayDisposition.Exact)
        {
            return new ResourceMutationResult(
                ledger,
                Array.Empty<ResourceAppliedEvent>(),
                null,
                replay.Transition,
                Array.Empty<ValidationIssue>());
        }
        if (replay.Disposition == ResourceReplayDisposition.Conflict)
            return MutationFailure(replay.Issues);

        if (!ledger.TryResolve(mutation.Coordinate, out var beforeEntry) ||
            beforeEntry == null)
        {
            Add(issues, "resource_mutation_coordinate_missing",
                "one exact live resource coordinate", Describe(mutation.Coordinate));
            return MutationFailure(issues);
        }
        if (beforeEntry.State != ResourceLifecycleState.Active)
        {
            Add(issues, "resource_mutation_state_inactive",
                "active resource state", beforeEntry.State.ToString());
            return MutationFailure(issues);
        }

        var subtracts = mutation.Operation is ResourceOperation.Damage or ResourceOperation.Spend;
        var arithmeticExact = subtracts
            ? ResourceMaterializationContract.TrySubtractExact(
                beforeEntry.Current,
                mutation.Amount,
                out var requestedCandidate)
            : ResourceMaterializationContract.TryAddExact(
                beforeEntry.Current,
                mutation.Amount,
                out requestedCandidate);
        if (!arithmeticExact)
        {
            Add(issues, "resource_mutation_arithmetic_inexact",
                "exactly representable non-overflowing requested candidate",
                $"{beforeEntry.Current}/{mutation.Amount}");
            return MutationFailure(issues);
        }

        if (mutation.ResultConstraint is { } constraint &&
            (constraint.RejectBelow.HasValue &&
             requestedCandidate < constraint.RejectBelow.Value ||
             constraint.RejectAbove.HasValue &&
             requestedCandidate > constraint.RejectAbove.Value))
        {
            Add(
                issues,
                "resource_mutation_result_constraint_violated",
                "requested result within the source-sealed exact result constraint",
                requestedCandidate.ToString());
            return MutationFailure(issues);
        }

        var outcome = ResourceTransitionOutcome.Applied;
        var afterCurrent = requestedCandidate;
        if (requestedCandidate < definition.MinimumPolicy.Value)
        {
            if (mutation.PolicyBinding.FloorPolicy != ResourceBoundPolicy.ClampToMinimum)
            {
                Add(issues, "resource_mutation_below_minimum",
                    "candidate at or above minimum or authorized minimum clamp",
                    requestedCandidate.ToString());
                return MutationFailure(issues);
            }
            afterCurrent = definition.MinimumPolicy.Value;
            outcome = ResourceTransitionOutcome.ClampedMinimum;
        }
        else if (requestedCandidate > beforeEntry.Maximum)
        {
            if (mutation.PolicyBinding.CapPolicy != ResourceBoundPolicy.ClampToMaximum)
            {
                Add(issues, "resource_mutation_above_maximum",
                    "candidate at or below maximum or authorized maximum clamp",
                    requestedCandidate.ToString());
                return MutationFailure(issues);
            }
            afterCurrent = beforeEntry.Maximum;
            outcome = ResourceTransitionOutcome.ClampedMaximum;
        }

        var appliedExact = subtracts
            ? ResourceMaterializationContract.TrySubtractExact(
                beforeEntry.Current,
                afterCurrent,
                out var appliedAmount)
            : ResourceMaterializationContract.TrySubtractExact(
                afterCurrent,
                beforeEntry.Current,
                out appliedAmount);
        if (!appliedExact || appliedAmount < 0m ||
            !IsDefinitionNumberValid(appliedAmount, definition, alignToMinimum: false) ||
            !IsDefinitionNumberValid(afterCurrent, definition, alignToMinimum: true))
        {
            Add(issues, "resource_mutation_arithmetic_inexact",
                "exact quantum-aligned applied amount and after-state",
                $"after={afterCurrent};applied={appliedAmount}");
            return MutationFailure(issues);
        }

        var transition = CreateTransition(
            mutation.TransitionId,
            mutation.OperationId,
            mutation.EventRef,
            mutation.OriginKind,
            mutation.OriginId,
            mutation.Phase,
            mutation.Priority,
            mutation.ExecutionSequence,
            mutation.Coordinate,
            transitionOperation,
            mutation.Amount,
            appliedAmount,
            outcome,
            null,
            beforeEntry.Snapshot,
            beforeEntry.Snapshot with { Current = afterCurrent },
            mutation.SourceEvidence,
            mutation.PolicyBinding.AuthorityFingerprint,
            mutation.ReceiptId,
            mutation.Turn);
        var appended = history.TryAppend(transition);
        if (!appended.IsValid)
            return MutationFailure(appended.Issues);

        var afterEntry = beforeEntry with
        {
            Current = afterCurrent,
            Chronology = AdvanceChronology(
                beforeEntry.Chronology,
                transition.TransitionId,
                transition.EventRef,
                transition.Turn)
        };
        var updatedLedger = ledger.WithEntry(afterEntry);
        var events = CreateEvents(
            mutation,
            definition,
            beforeEntry,
            afterEntry,
            appliedAmount);
        return new ResourceMutationResult(
            updatedLedger,
            events,
            transition,
            null,
            Array.Empty<ValidationIssue>());
    }

    internal static ResourceCapacityResult ApplyCapacityTransition(
        ResourceWorkingLedger ledger,
        ResourceHistoryWorkingSet history,
        AuthorizedResourceCapacityTransition transition,
        ResourceDefinitionCatalog definitions)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(definitions);
        var issues = ValidateCapacityInput(transition, definitions, out var definition);
        if (issues.Count != 0 || definition == null)
            return CapacityFailure(issues);

        var operation = ToTransitionOperation(transition.Operation);
        var capacityDisposition = ToHistoryDisposition(transition.CurrentDisposition);
        var probe = new ResourceReplayProbe(
            transition.EventRef,
            transition.OriginKind,
            transition.OriginId,
            transition.Coordinate,
            operation,
            0m,
            transition.Phase,
            transition.Priority,
            transition.ExecutionSequence,
            capacityDisposition,
            transition.SourceEvidence,
            transition.PolicyFingerprint,
            transition.ReceiptId);
        var replay = history.ResolveReplay(probe);
        if (replay.Disposition == ResourceReplayDisposition.Exact)
        {
            return new ResourceCapacityResult(
                ledger,
                null,
                replay.Transition,
                Array.Empty<ValidationIssue>());
        }
        if (replay.Disposition == ResourceReplayDisposition.Conflict)
            return CapacityFailure(replay.Issues);
        if (history.IsTerminal(transition.Coordinate))
        {
            Add(issues, "resource_capacity_terminal_coordinate",
                "no transition after terminal retirement", Describe(transition.Coordinate));
            return CapacityFailure(issues);
        }

        ledger.TryResolve(transition.Coordinate, out var beforeEntry);
        ResourceStateSnapshot? afterSnapshot;
        ResourceTransitionOutcome outcome = ResourceTransitionOutcome.Applied;
        switch (transition.Operation)
        {
            case ResourceCapacityOperation.Initialize:
                if (beforeEntry != null)
                {
                    Add(issues, "resource_capacity_coordinate_exists",
                        "absent coordinate before initialization", Describe(transition.Coordinate));
                    return CapacityFailure(issues);
                }
                if (transition.CurrentDisposition != ResourceCurrentDisposition.InitializeFromDefinition ||
                    transition.ResolvedCapacity == null ||
                    transition.ResolvedCapacity.Initialization == null ||
                    !ValidateResolvedCapacity(
                        transition.ResolvedCapacity,
                        transition.Coordinate,
                        definition,
                        issues))
                {
                    Add(issues, "resource_capacity_initialization_invalid",
                        "resolved capacity and initialize_from_definition disposition",
                        Describe(transition.Coordinate));
                    return CapacityFailure(issues);
                }
                var initial = ResolveInitialCurrent(
                    definition,
                    transition.ResolvedCapacity,
                    issues);
                if (!initial.HasValue)
                    return CapacityFailure(issues);
                afterSnapshot = new ResourceStateSnapshot(
                    initial.Value,
                    transition.ResolvedCapacity.Maximum,
                    transition.ResolvedCapacity.Binding,
                    ResourceLifecycleState.Active);
                break;

            case ResourceCapacityOperation.Reconfigure:
                if (beforeEntry == null)
                {
                    Add(issues, "resource_capacity_coordinate_missing",
                        "live coordinate before reconfiguration", Describe(transition.Coordinate));
                    return CapacityFailure(issues);
                }
                if (transition.ResolvedCapacity == null ||
                    transition.ResolvedCapacity.Initialization != null ||
                    transition.CurrentDisposition is null or
                        ResourceCurrentDisposition.InitializeFromDefinition ||
                    !ValidateResolvedCapacity(
                        transition.ResolvedCapacity,
                        transition.Coordinate,
                        definition,
                        issues))
                {
                    Add(issues, "resource_capacity_reconfigure_invalid",
                        "resolved changed capacity and one reconfigure disposition",
                        Describe(transition.Coordinate));
                    return CapacityFailure(issues);
                }
                if (beforeEntry.Maximum == transition.ResolvedCapacity.Maximum &&
                    beforeEntry.CapacityBinding == transition.ResolvedCapacity.Binding)
                {
                    Add(issues, "resource_capacity_unchanged",
                        "changed maximum or capacity authority", Describe(transition.Coordinate));
                    return CapacityFailure(issues);
                }
                var reconfiguredCurrent = ResolveReconfiguredCurrent(
                    beforeEntry,
                    transition.ResolvedCapacity,
                    transition.CurrentDisposition.Value,
                    definition,
                    issues,
                    out outcome);
                if (!reconfiguredCurrent.HasValue)
                    return CapacityFailure(issues);
                afterSnapshot = new ResourceStateSnapshot(
                    reconfiguredCurrent.Value,
                    transition.ResolvedCapacity.Maximum,
                    transition.ResolvedCapacity.Binding,
                    beforeEntry.State);
                break;

            case ResourceCapacityOperation.Suspend:
                if (!ValidateLifecycleInput(
                        beforeEntry,
                        ResourceLifecycleState.Active,
                        transition,
                        issues))
                    return CapacityFailure(issues);
                afterSnapshot = beforeEntry!.Snapshot with
                {
                    State = ResourceLifecycleState.Suspended
                };
                break;

            case ResourceCapacityOperation.Resume:
                if (!ValidateLifecycleInput(
                        beforeEntry,
                        ResourceLifecycleState.Suspended,
                        transition,
                        issues))
                    return CapacityFailure(issues);
                afterSnapshot = beforeEntry!.Snapshot with
                {
                    State = ResourceLifecycleState.Active
                };
                break;

            case ResourceCapacityOperation.Retire:
                if (beforeEntry == null || transition.ResolvedCapacity != null ||
                    transition.CurrentDisposition != null ||
                    !string.Equals(
                        transition.SourceEvidence.SourceKind,
                        "owner_lifecycle",
                        StringComparison.Ordinal))
                {
                    Add(issues, "resource_capacity_retire_invalid",
                        "live coordinate and exact owner_lifecycle terminal evidence",
                        Describe(transition.Coordinate));
                    return CapacityFailure(issues);
                }
                afterSnapshot = null;
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }

        var resourceTransition = CreateTransition(
            transition.TransitionId,
            transition.OperationId,
            transition.EventRef,
            transition.OriginKind,
            transition.OriginId,
            transition.Phase,
            transition.Priority,
            transition.ExecutionSequence,
            transition.Coordinate,
            operation,
            0m,
            0m,
            outcome,
            capacityDisposition,
            beforeEntry?.Snapshot,
            afterSnapshot,
            transition.SourceEvidence,
            transition.PolicyFingerprint,
            transition.ReceiptId,
            transition.Turn);
        var appended = history.TryAppend(resourceTransition);
        if (!appended.IsValid)
            return CapacityFailure(appended.Issues);

        ResourceWorkingLedger updatedLedger;
        if (afterSnapshot == null)
        {
            updatedLedger = ledger.Without(transition.Coordinate);
        }
        else if (beforeEntry == null)
        {
            updatedLedger = ledger.WithEntry(new ResourceStateEntry(
                transition.Coordinate,
                afterSnapshot.Current,
                afterSnapshot.Maximum,
                afterSnapshot.CapacityBinding,
                afterSnapshot.State,
                new ResourceChronology(
                    transition.Turn,
                    transition.EventRef,
                    transition.TransitionId,
                    transition.EventRef,
                    transition.Turn)));
        }
        else
        {
            updatedLedger = ledger.WithEntry(beforeEntry with
            {
                Current = afterSnapshot.Current,
                Maximum = afterSnapshot.Maximum,
                CapacityBinding = afterSnapshot.CapacityBinding,
                State = afterSnapshot.State,
                Chronology = AdvanceChronology(
                    beforeEntry.Chronology,
                    transition.TransitionId,
                    transition.EventRef,
                    transition.Turn)
            });
        }

        return new ResourceCapacityResult(
            updatedLedger,
            resourceTransition,
            null,
            Array.Empty<ValidationIssue>());
    }

    private static List<ValidationIssue> ValidateCommonMutation(
        AuthorizedResourceMutation mutation,
        ResourceDefinitionCatalog definitions,
        out ResourceDefinition? definition)
    {
        var issues = new List<ValidationIssue>();
        definitions.TryResolveExact(mutation.Coordinate.ResourceKey, out definition);
        if (definition == null)
        {
            Add(issues, "resource_mutation_definition_unknown",
                "exact sealed resource definition", mutation.Coordinate.ResourceKey);
            return issues;
        }
        if (!definition.AllowedOwnerKinds.Contains(mutation.Coordinate.OwnerKind))
        {
            Add(issues, "resource_mutation_owner_forbidden",
                "owner kind allowed by sealed definition",
                mutation.Coordinate.OwnerKind.ToString());
        }
        if (!definition.AllowedOperations.Contains(mutation.Operation))
        {
            Add(issues, "resource_mutation_operation_forbidden",
                "operation allowed by sealed definition", mutation.Operation.ToString());
        }
        if (mutation.Amount <= 0m ||
            !IsDefinitionNumberValid(mutation.Amount, definition, alignToMinimum: false))
        {
            Add(issues, "resource_mutation_amount_invalid",
                "positive exact definition-quantized amount", mutation.Amount.ToString());
        }
        if (mutation.ResultConstraint is { } constraint)
        {
            var invalidBelow = constraint.RejectBelow.HasValue &&
                (!IsDefinitionNumberValid(
                    constraint.RejectBelow.Value,
                    definition,
                    alignToMinimum: true) ||
                 constraint.RejectBelow.Value < definition.MinimumPolicy.Value);
            var invalidAbove = constraint.RejectAbove.HasValue &&
                (!IsDefinitionNumberValid(
                    constraint.RejectAbove.Value,
                    definition,
                    alignToMinimum: true) ||
                 constraint.RejectAbove.Value < definition.MinimumPolicy.Value);
            if (invalidBelow || invalidAbove ||
                constraint.RejectBelow.HasValue &&
                constraint.RejectAbove.HasValue &&
                constraint.RejectBelow.Value > constraint.RejectAbove.Value)
            {
                Add(
                    issues,
                    "resource_mutation_result_constraint_invalid",
                    "null or exact definition-aligned result bounds ordered above the resource minimum",
                    $"below={constraint.RejectBelow};above={constraint.RejectAbove}");
            }
        }
        ValidateIdentityAndEvidence(
            mutation.TransitionId,
            mutation.OperationId,
            mutation.EventRef,
            mutation.OriginKind,
            mutation.OriginId,
            mutation.ExecutionSequence,
            mutation.Turn,
            mutation.SourceEvidence,
            mutation.PolicyBinding.AuthorityFingerprint,
            mutation.ReceiptId,
            issues);
        return issues;
    }

    private static List<ValidationIssue> ValidateCapacityInput(
        AuthorizedResourceCapacityTransition transition,
        ResourceDefinitionCatalog definitions,
        out ResourceDefinition? definition)
    {
        var issues = new List<ValidationIssue>();
        definitions.TryResolveExact(transition.Coordinate.ResourceKey, out definition);
        if (definition == null)
        {
            Add(issues, "resource_capacity_definition_unknown",
                "exact sealed resource definition", transition.Coordinate.ResourceKey);
            return issues;
        }
        if (!definition.AllowedOwnerKinds.Contains(transition.Coordinate.OwnerKind))
        {
            Add(issues, "resource_capacity_owner_forbidden",
                "owner kind allowed by sealed definition",
                transition.Coordinate.OwnerKind.ToString());
        }
        ValidateIdentityAndEvidence(
            transition.TransitionId,
            transition.OperationId,
            transition.EventRef,
            transition.OriginKind,
            transition.OriginId,
            transition.ExecutionSequence,
            transition.Turn,
            transition.SourceEvidence,
            transition.PolicyFingerprint,
            transition.ReceiptId,
            issues);
        if (transition.ResolvedCapacity != null)
        {
            if (transition.Operation == ResourceCapacityOperation.Initialize &&
                transition.ResolvedCapacity.Initialization == null)
            {
                Add(issues, "resource_capacity_initialization_invalid",
                    "resolved capacity with exact initialization authority",
                    Describe(transition.Coordinate));
                return issues;
            }
            var expectedPolicyFingerprint = transition.Operation ==
                ResourceCapacityOperation.Initialize
                ? transition.ResolvedCapacity.Initialization?.AuthorityFingerprint
                : transition.ResolvedCapacity.Binding.AuthorityFingerprint;
            if (expectedPolicyFingerprint == null ||
                !string.Equals(
                    transition.PolicyFingerprint,
                    expectedPolicyFingerprint,
                    StringComparison.Ordinal))
            {
                Add(issues, "resource_capacity_policy_binding_invalid",
                    "policy fingerprint equals the exact resolved capacity authority",
                    transition.PolicyFingerprint);
            }
        }
        return issues;
    }

    private static void ValidateIdentityAndEvidence(
        string transitionId,
        string operationId,
        string eventRef,
        string originKind,
        string originId,
        int executionSequence,
        int turn,
        ResourceSourceEvidence source,
        string policyFingerprint,
        string? receiptId,
        List<ValidationIssue> issues)
    {
        if (!ResourceMaterializationContract.IsExactIdentifier(transitionId) ||
            !ResourceMaterializationContract.IsExactIdentifier(operationId) ||
            !ResourceMaterializationContract.IsExactIdentifier(eventRef) ||
            !ResourceMaterializationContract.IsExactIdentifier(originKind) ||
            !ResourceMaterializationContract.IsExactIdentifier(originId) ||
            executionSequence < 0 || turn < 0 ||
            !ResourceMaterializationContract.IsExactIdentifier(source.SourceKind) ||
            !ResourceMaterializationContract.IsExactIdentifier(source.SourceId) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(source.AuthorityFingerprint) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(policyFingerprint) ||
            receiptId != null && !ResourceMaterializationContract.IsExactIdentifier(receiptId))
        {
            Add(issues, "resource_mutation_authority_invalid",
                "exact client-owned identity, chronology, source, and policy authority",
                $"transition={transitionId};operation={operationId};event={eventRef}");
        }
    }

    private static bool ValidateResolvedCapacity(
        ResolvedResourceCapacity capacity,
        ResourceCoordinate coordinate,
        ResourceDefinition definition,
        List<ValidationIssue> issues)
    {
        var maximumMayEqualMinimum =
            definition.CapacityPolicy.Kind == ResourceCapacityKind.RegisteredFormula;
        var valid = capacity.Maximum >= definition.MinimumPolicy.Value &&
            (maximumMayEqualMinimum ||
             capacity.Maximum > definition.MinimumPolicy.Value) &&
            IsDefinitionNumberValid(capacity.Maximum, definition, alignToMinimum: true) &&
            ResourceMaterializationContract.IsExactIdentifier(capacity.Binding.AuthorityKey) &&
            ResourceMaterializationContract.IsAuthorityFingerprint(
                capacity.Binding.AuthorityFingerprint);
        valid &= definition.CapacityPolicy.Kind switch
        {
            ResourceCapacityKind.DefinitionFixed =>
                capacity.Binding.Kind == ResourceCapacityKind.DefinitionFixed &&
                capacity.Maximum == definition.CapacityPolicy.Value,
            ResourceCapacityKind.InstanceFixed =>
                capacity.Binding.Kind == ResourceCapacityKind.InstanceFixed,
            ResourceCapacityKind.RegisteredFormula =>
                capacity.Binding.Kind == ResourceCapacityKind.RegisteredFormula &&
                string.Equals(
                    capacity.Binding.AuthorityKey,
                    definition.CapacityPolicy.FormulaKey,
                    StringComparison.Ordinal),
            _ => false
        };
        if (!valid)
        {
            Add(issues, "resource_capacity_binding_invalid",
                "exact definition-compatible capacity binding and maximum",
                Describe(coordinate));
        }
        return valid;
    }

    private static decimal? ResolveInitialCurrent(
        ResourceDefinition definition,
        ResolvedResourceCapacity capacity,
        List<ValidationIssue> issues)
    {
        var result = definition.InitializationPolicy.Kind switch
        {
            ResourceInitializationKind.Minimum or
            ResourceInitializationKind.Maximum or
            ResourceInitializationKind.Fixed or
            ResourceInitializationKind.RegisteredFormula =>
                capacity.Initialization?.Current,
            _ => null
        };
        if (!result.HasValue || result < definition.MinimumPolicy.Value ||
            result > capacity.Maximum ||
            !IsDefinitionNumberValid(result.Value, definition, alignToMinimum: true))
        {
            Add(issues, "resource_capacity_initialization_invalid",
                "exact definition-authorized initial current",
                result?.ToString() ?? "null");
            return null;
        }
        return result;
    }

    private static decimal? ResolveReconfiguredCurrent(
        ResourceStateEntry before,
        ResolvedResourceCapacity capacity,
        ResourceCurrentDisposition disposition,
        ResourceDefinition definition,
        List<ValidationIssue> issues,
        out ResourceTransitionOutcome outcome)
    {
        outcome = ResourceTransitionOutcome.Applied;
        decimal? result = disposition switch
        {
            ResourceCurrentDisposition.Preserve when before.Current <= capacity.Maximum =>
                before.Current,
            ResourceCurrentDisposition.ClampToNewMaximum =>
                Math.Min(before.Current, capacity.Maximum),
            ResourceCurrentDisposition.ScaleRatioExact when
                ResourceMaterializationContract.TryScaleRatioExact(
                    before.Current,
                    capacity.Maximum,
                    before.Maximum,
                    out var scaled) => scaled,
            _ => null
        };
        if (disposition == ResourceCurrentDisposition.ClampToNewMaximum &&
            result.HasValue && result.Value < before.Current)
        {
            outcome = ResourceTransitionOutcome.ClampedMaximum;
        }
        if (!result.HasValue)
        {
            Add(issues,
                disposition == ResourceCurrentDisposition.ScaleRatioExact
                    ? "resource_capacity_ratio_inexact"
                    : "resource_capacity_reconfigure_invalid",
                "exact authorized reconfiguration result",
                $"before={before.Current}/{before.Maximum};afterMax={capacity.Maximum}");
            return null;
        }
        if (result < definition.MinimumPolicy.Value || result > capacity.Maximum ||
            !IsDefinitionNumberValid(result.Value, definition, alignToMinimum: true))
        {
            Add(issues,
                disposition == ResourceCurrentDisposition.ScaleRatioExact
                    ? "resource_capacity_ratio_inexact"
                    : "resource_capacity_reconfigure_invalid",
                "in-range exact quantum-aligned reconfiguration result",
                result.Value.ToString());
            return null;
        }
        return result;
    }

    private static bool ValidateLifecycleInput(
        ResourceStateEntry? before,
        ResourceLifecycleState requiredState,
        AuthorizedResourceCapacityTransition transition,
        List<ValidationIssue> issues)
    {
        if (before != null && before.State == requiredState &&
            transition.ResolvedCapacity == null && transition.CurrentDisposition == null)
            return true;
        Add(issues, "resource_capacity_lifecycle_invalid",
            $"live {requiredState} coordinate and no capacity/disposition payload",
            Describe(transition.Coordinate));
        return false;
    }

    private static bool IsDefinitionNumberValid(
        decimal value,
        ResourceDefinition definition,
        bool alignToMinimum) =>
        (definition.NumericKind != ResourceNumericKind.Integer ||
         ResourceMaterializationContract.IsIntegral(value)) &&
        ResourceMaterializationContract.IsQuantumAligned(
            value,
            alignToMinimum ? definition.MinimumPolicy.Value : 0m,
            definition.Quantum);

    private static ResourceTransition CreateTransition(
        string transitionId,
        string operationId,
        string eventRef,
        string originKind,
        string originId,
        ResourceMutationPhase phase,
        int priority,
        int executionSequence,
        ResourceCoordinate coordinate,
        ResourceTransitionOperation operation,
        decimal requested,
        decimal applied,
        ResourceTransitionOutcome outcome,
        ResourceCapacityDisposition? disposition,
        ResourceStateSnapshot? before,
        ResourceStateSnapshot? after,
        ResourceSourceEvidence source,
        string policyFingerprint,
        string? receiptId,
        int turn) =>
        new(
            transitionId,
            operationId,
            eventRef,
            originKind,
            originId,
            phase,
            priority,
            executionSequence,
            coordinate,
            operation,
            requested,
            applied,
            outcome,
            disposition,
            before,
            after,
            source,
            policyFingerprint,
            receiptId,
            turn);

    private static IReadOnlyList<ResourceAppliedEvent> CreateEvents(
        AuthorizedResourceMutation mutation,
        ResourceDefinition definition,
        ResourceStateEntry before,
        ResourceStateEntry after,
        decimal appliedAmount)
    {
        var events = new List<ResourceAppliedEvent>();
        AddEvent(PrimaryEvent(mutation.Operation));
        if (before.Current > definition.MinimumPolicy.Value &&
            after.Current == definition.MinimumPolicy.Value)
            AddEvent("resource_depleted");
        if (before.Current < before.Maximum && after.Current == after.Maximum)
            AddEvent("resource_filled");
        return events.AsReadOnly();

        void AddEvent(string kind) => events.Add(new ResourceAppliedEvent(
            kind,
            mutation.OperationId,
            mutation.EventRef,
            mutation.Coordinate,
            before.Current,
            after.Current,
            appliedAmount,
            mutation.Turn,
            mutation.ExecutionSequence,
            mutation.SourceEvidence.AuthorityFingerprint));
    }

    private static string PrimaryEvent(ResourceOperation operation) => operation switch
    {
        ResourceOperation.Damage => "resource_damaged",
        ResourceOperation.Restore => "resource_restored",
        ResourceOperation.Spend => "resource_spent",
        ResourceOperation.Gain => "resource_gained",
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
    };

    private static ResourceTransitionOperation ToTransitionOperation(
        ResourceOperation operation) => operation switch
    {
        ResourceOperation.Damage => ResourceTransitionOperation.Damage,
        ResourceOperation.Restore => ResourceTransitionOperation.Restore,
        ResourceOperation.Spend => ResourceTransitionOperation.Spend,
        ResourceOperation.Gain => ResourceTransitionOperation.Gain,
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
    };

    private static ResourceTransitionOperation ToTransitionOperation(
        ResourceCapacityOperation operation) => operation switch
    {
        ResourceCapacityOperation.Initialize => ResourceTransitionOperation.Initialize,
        ResourceCapacityOperation.Reconfigure => ResourceTransitionOperation.Reconfigure,
        ResourceCapacityOperation.Suspend => ResourceTransitionOperation.Suspend,
        ResourceCapacityOperation.Resume => ResourceTransitionOperation.Resume,
        ResourceCapacityOperation.Retire => ResourceTransitionOperation.Retire,
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
    };

    private static ResourceCapacityDisposition? ToHistoryDisposition(
        ResourceCurrentDisposition? disposition) => disposition switch
    {
        ResourceCurrentDisposition.InitializeFromDefinition =>
            ResourceCapacityDisposition.InitializeFromDefinition,
        ResourceCurrentDisposition.Preserve => ResourceCapacityDisposition.Preserve,
        ResourceCurrentDisposition.ClampToNewMaximum =>
            ResourceCapacityDisposition.ClampToNewMaximum,
        ResourceCurrentDisposition.ScaleRatioExact =>
            ResourceCapacityDisposition.ScaleRatioExact,
        null => null,
        _ => throw new ArgumentOutOfRangeException(nameof(disposition), disposition, null)
    };

    private static ResourceChronology AdvanceChronology(
        ResourceChronology chronology,
        string transitionId,
        string eventRef,
        int turn) =>
        chronology with
        {
            LastTransitionId = transitionId,
            LastEventRef = eventRef,
            LastTransitionTurn = turn
        };

    private static ResourceMutationResult MutationFailure(
        IEnumerable<ValidationIssue> issues) =>
        new(
            null,
            Array.Empty<ResourceAppliedEvent>(),
            null,
            null,
            issues.ToArray());

    private static ResourceCapacityResult CapacityFailure(
        IEnumerable<ValidationIssue> issues) =>
        new(null, null, null, issues.ToArray());

    private static string Describe(ResourceCoordinate coordinate) =>
        $"{coordinate.Realm}/" +
        $"{ResourceDefinitionCatalog.GetOwnerKindToken(coordinate.OwnerKind)}/" +
        $"{coordinate.ResourceOwnerId}/{coordinate.ResourceKey}";

    private static void Add(
        List<ValidationIssue> issues,
        string code,
        string expected,
        string actual) =>
        ResourceMaterializationContract.AddIssue(
            issues,
            ResourceMaterializationContract.StatePath,
            code,
            expected,
            actual);
}
