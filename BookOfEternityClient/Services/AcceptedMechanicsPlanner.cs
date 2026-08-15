using System.Collections.Frozen;
using System.Collections.ObjectModel;

namespace BookOfEternityClient.Services;

internal sealed record ResourceMutationEventRequirement(
    ResourceOperationKey Producer,
    string EventKind);

internal sealed record ResourceMutationIntent(
    string EventRef,
    ResourceCoordinate Coordinate,
    decimal Amount,
    ResourceMutationSourceRequest Source,
    IReadOnlyList<ResourceOperationKey> Dependencies,
    IReadOnlyList<ResourceMutationEventRequirement> EventRequirements,
    string? ReceiptId)
{
    internal ResourceOperationKey Key => new(
        EventRef,
        Source.SourceKind,
        Source.SourceId,
        Coordinate,
        Source.Operation);
}

internal sealed record ResourceCapacityIntent(
    string EventRef,
    string OriginKind,
    string OriginId,
    ResourceCoordinate Coordinate,
    ResourceCapacityOperation Operation,
    ResolvedResourceCapacity? ResolvedCapacity,
    ResourceCurrentDisposition? CurrentDisposition,
    ResourceMutationPhase Phase,
    int Priority,
    ResourceSourceEvidence SourceEvidence,
    string PolicyFingerprint,
    string? ReceiptId)
{
    internal ResourceCapacityOperationKey Key => new(
        EventRef,
        OriginKind,
        OriginId,
        Coordinate,
        Operation);
}

internal sealed record ResourceCapacityOperationKey(
    string EventRef,
    string OriginKind,
    string OriginId,
    ResourceCoordinate Coordinate,
    ResourceCapacityOperation Operation);

internal sealed class AcceptedMechanicsResourceInput
{
    private readonly ResourceMutationIntent[] _mutations;
    private readonly ResourceCapacityIntent[] _capacityTransitions;

    internal AcceptedMechanicsResourceInput(
        int Turn,
        ResourceDefinitionCatalog Definitions,
        ResourceStateLedger State,
        ResourceHistoryState History,
        ResourceMutationSourceCatalog Sources,
        IReadOnlyList<ResourceMutationIntent> Mutations,
        IReadOnlyList<ResourceCapacityIntent>? CapacityTransitions = null)
    {
        if (Turn <= 0)
            throw new ArgumentOutOfRangeException(nameof(Turn));
        this.Turn = Turn;
        this.Definitions = Definitions ?? throw new ArgumentNullException(nameof(Definitions));
        this.State = State ?? throw new ArgumentNullException(nameof(State));
        this.History = History ?? throw new ArgumentNullException(nameof(History));
        this.Sources = Sources ?? throw new ArgumentNullException(nameof(Sources));
        ArgumentNullException.ThrowIfNull(Mutations);
        _mutations = Mutations.Select(Clone).ToArray();
        _capacityTransitions = CapacityTransitions?.Select(value =>
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(value.Coordinate);
            ArgumentNullException.ThrowIfNull(value.SourceEvidence);
            return value;
        }).ToArray() ?? Array.Empty<ResourceCapacityIntent>();
    }

    internal int Turn { get; }
    internal ResourceDefinitionCatalog Definitions { get; }
    internal ResourceStateLedger State { get; }
    internal ResourceHistoryState History { get; }
    internal ResourceMutationSourceCatalog Sources { get; }
    internal IReadOnlyList<ResourceMutationIntent> Mutations =>
        Array.AsReadOnly(_mutations.Select(Clone).ToArray());
    internal IReadOnlyList<ResourceCapacityIntent> CapacityTransitions =>
        Array.AsReadOnly(_capacityTransitions.ToArray());

    private static ResourceMutationIntent Clone(ResourceMutationIntent value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(value.Coordinate);
        ArgumentNullException.ThrowIfNull(value.Source);
        ArgumentNullException.ThrowIfNull(value.Dependencies);
        ArgumentNullException.ThrowIfNull(value.EventRequirements);
        foreach (var dependency in value.Dependencies)
            ArgumentNullException.ThrowIfNull(dependency);
        foreach (var requirement in value.EventRequirements)
        {
            ArgumentNullException.ThrowIfNull(requirement);
            ArgumentNullException.ThrowIfNull(requirement.Producer);
        }
        return value with
        {
            Dependencies = value.Dependencies.ToArray(),
            EventRequirements = value.EventRequirements.ToArray()
        };
    }
}

internal sealed record AcceptedMechanicsPlannerStatistics(
    int HistorySeedCount,
    int HistoryAppendCount,
    int HistoryFreezeCount);

internal sealed class AcceptedMechanicsResourcePlanningResult
{
    private readonly ResourceAppliedEvent[] _events;
    private readonly ResourceTransition[] _appliedTransitions;
    private readonly ResourceTransition[] _replayTransitions;
    private readonly ValidationIssue[] _issues;

    internal AcceptedMechanicsResourcePlanningResult(
        ResourceStateLedger? stateAfterImage,
        ResourceHistoryState? historyAfterImage,
        IReadOnlyList<ResourceAppliedEvent> events,
        IReadOnlyList<ResourceTransition> appliedTransitions,
        IReadOnlyList<ResourceTransition> replayTransitions,
        IReadOnlyList<ValidationIssue> issues,
        AcceptedMechanicsPlannerStatistics statistics)
    {
        StateAfterImage = stateAfterImage;
        HistoryAfterImage = historyAfterImage;
        _events = (events ?? throw new ArgumentNullException(nameof(events))).ToArray();
        _appliedTransitions = (appliedTransitions ??
            throw new ArgumentNullException(nameof(appliedTransitions))).ToArray();
        _replayTransitions = (replayTransitions ??
            throw new ArgumentNullException(nameof(replayTransitions))).ToArray();
        _issues = (issues ?? throw new ArgumentNullException(nameof(issues))).ToArray();
        Statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
    }

    internal ResourceStateLedger? StateAfterImage { get; }
    internal ResourceHistoryState? HistoryAfterImage { get; }
    internal IReadOnlyList<ResourceAppliedEvent> Events =>
        Array.AsReadOnly(_events.ToArray());
    internal IReadOnlyList<ResourceTransition> AppliedTransitions =>
        Array.AsReadOnly(_appliedTransitions.ToArray());
    internal IReadOnlyList<ResourceTransition> ReplayTransitions =>
        Array.AsReadOnly(_replayTransitions.ToArray());
    internal IReadOnlyList<ValidationIssue> Issues =>
        Array.AsReadOnly(_issues.ToArray());
    internal AcceptedMechanicsPlannerStatistics Statistics { get; }
    internal bool IsValid =>
        StateAfterImage != null && HistoryAfterImage != null && Issues.Count == 0;
}

internal sealed class AcceptedMechanicsIdentityFactory
{
    private readonly Func<Guid> _guidFactory;

    internal AcceptedMechanicsIdentityFactory(Func<Guid>? guidFactory = null) =>
        _guidFactory = guidFactory ?? Guid.NewGuid;

    internal string CreateOperationId() =>
        "resource_operation_" + _guidFactory().ToString("N");

    internal string CreateTransitionId() =>
        "resource_transition_" + _guidFactory().ToString("N");
}

internal static class AcceptedMechanicsPlanner
{
    internal static AcceptedMechanicsResourcePlanningResult BuildResources(
        AcceptedMechanicsResourceInput input,
        AcceptedMechanicsIdentityFactory identityFactory)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(identityFactory);
        var agreementIssues = input.History.ValidateStateAgreement(input.State);
        if (agreementIssues.Count != 0)
            return Failure(agreementIssues, Statistics());
        if (input.CapacityTransitions.Count >
            ResourceMaterializationContract.MaxCapacityTransitionsPerTurn)
        {
            return Failure(
                Issue(
                    "resource_planner_capacity_limit_exceeded",
                    $"at most {ResourceMaterializationContract.MaxCapacityTransitionsPerTurn} capacity transitions",
                    input.CapacityTransitions.Count.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)),
                Statistics());
        }

        var identityRegistry = new AllocatedIdentityRegistry();
        var capacityPreparation = PrepareCapacityTransitions(
            input.CapacityTransitions,
            identityFactory,
            identityRegistry);
        if (capacityPreparation.Issues.Count != 0)
            return Failure(capacityPreparation.Issues, Statistics());
        var preparation = PrepareMutations(
            input,
            identityFactory,
            identityRegistry);
        if (preparation.Issues.Count != 0)
            return Failure(preparation.Issues, Statistics());
        var preparedMutations = preparation.Mutations;
        if (preparedMutations.Count(static value =>
                value.Route.Phase != ResourceMutationPhase.EffectTrigger) >
            ResourceMaterializationContract.MaxMutationsBeforeTriggers)
        {
            return Failure(
                Issue(
                    "resource_planner_mutation_limit_exceeded",
                    $"at most {ResourceMaterializationContract.MaxMutationsBeforeTriggers} pre-trigger mutations",
                    preparedMutations.Count(static value =>
                        value.Route.Phase != ResourceMutationPhase.EffectTrigger).ToString(
                            System.Globalization.CultureInfo.InvariantCulture)),
                Statistics());
        }

        var graphResult = BuildGraph(preparedMutations);
        if (!graphResult.IsValid)
            return Failure(graphResult.Issues, Statistics());

        var workingLedger = new ResourceWorkingLedger(input.State.Entries);
        var workingHistory = new ResourceHistoryWorkingSet(input.History);
        var events = new List<ResourceAppliedEvent>();
        var appliedTransitions = new List<ResourceTransition>();
        var replayTransitions = new List<ResourceTransition>();
        var executionSequence = 0;

        foreach (var capacity in capacityPreparation.Transitions)
        {
            var result = ResourceMutationReducer.ApplyCapacityTransition(
                workingLedger,
                workingHistory,
                new AuthorizedResourceCapacityTransition(
                    capacity.TransitionId,
                    capacity.OperationId,
                    capacity.Intent.EventRef,
                    capacity.Intent.OriginKind,
                    capacity.Intent.OriginId,
                    capacity.Intent.Coordinate,
                    capacity.Intent.Operation,
                    capacity.Intent.ResolvedCapacity,
                    capacity.Intent.CurrentDisposition,
                    capacity.Intent.Phase,
                    capacity.Intent.Priority,
                    executionSequence++,
                    capacity.Intent.SourceEvidence,
                    capacity.Intent.PolicyFingerprint,
                    capacity.Intent.ReceiptId,
                    input.Turn),
                input.Definitions);
            if (!result.IsValid)
                return Failure(result.Issues, Statistics(workingHistory));
            workingLedger = result.WorkingLedger!;
            if (result.Transition != null)
                appliedTransitions.Add(result.Transition);
            else
                replayTransitions.Add(result.ReplayTransition!);
        }

        var byOperationId = preparedMutations.ToDictionary(
            static value => value.OperationId,
            StringComparer.Ordinal);
        var producedEvents = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var graphNode in graphResult.Graph!.OrderedNodes)
        {
            var prepared = byOperationId[graphNode.OperationId];
            if (!RequirementsSatisfied(prepared, producedEvents))
                continue;

            var mutation = prepared.Intent;
            var result = ResourceMutationReducer.Reduce(
                workingLedger,
                workingHistory,
                new AuthorizedResourceMutation(
                    prepared.TransitionId,
                    prepared.OperationId,
                    mutation.EventRef,
                    mutation.Source.SourceKind,
                    mutation.Source.SourceId,
                    mutation.Coordinate,
                    mutation.Source.Operation,
                    mutation.Amount,
                    prepared.Route.Phase,
                    prepared.Route.Priority,
                    executionSequence++,
                    prepared.Route.PolicyBinding,
                    mutation.Dependencies,
                    prepared.Route.SourceEvidence,
                    mutation.ReceiptId,
                    input.Turn),
                input.Definitions);
            if (!result.IsValid)
                return Failure(result.Issues, Statistics(workingHistory));
            workingLedger = result.WorkingLedger!;
            if (result.Transition != null)
            {
                appliedTransitions.Add(result.Transition);
                events.AddRange(result.Events);
                producedEvents[prepared.OperationId] = result.Events
                    .Select(static value => value.EventKind)
                    .ToHashSet(StringComparer.Ordinal);
            }
            else
            {
                replayTransitions.Add(result.ReplayTransition!);
                producedEvents[prepared.OperationId] = new HashSet<string>(StringComparer.Ordinal);
            }
        }

        var stateAfterImage = workingLedger.Freeze();
        var frozen = workingHistory.Freeze(input.Definitions);
        if (!frozen.IsValid || frozen.History == null)
            return Failure(frozen.Issues, Statistics(workingHistory));
        var finalAgreement = frozen.History.ValidateStateAgreement(stateAfterImage);
        if (finalAgreement.Count != 0)
            return Failure(finalAgreement, Statistics(workingHistory));

        return new AcceptedMechanicsResourcePlanningResult(
            stateAfterImage,
            frozen.History,
            events,
            appliedTransitions,
            replayTransitions,
            Array.Empty<ValidationIssue>(),
            Statistics(workingHistory));
    }

    private static PreparationResult PrepareMutations(
        AcceptedMechanicsResourceInput input,
        AcceptedMechanicsIdentityFactory identityFactory,
        AllocatedIdentityRegistry identityRegistry)
    {
        var issues = new List<ValidationIssue>();
        var unresolved = new List<UnresolvedMutation>();
        var keys = new HashSet<ResourceOperationKey>();
        foreach (var mutation in input.Mutations)
        {
            if (!keys.Add(mutation.Key))
            {
                AddIssue(
                    issues,
                    "resource_planner_duplicate_operation",
                    "one mutation per exact replay key",
                    Describe(mutation.Key));
                continue;
            }
            if (!input.Definitions.TryResolveExact(
                    mutation.Coordinate.ResourceKey,
                    out var definition) ||
                definition == null)
            {
                AddIssue(
                    issues,
                    "resource_planner_definition_unknown",
                    "one exact sealed resource definition",
                    mutation.Coordinate.ResourceKey);
                continue;
            }

            var source = input.Sources.Resolve(mutation.Source, definition);
            if (!source.IsValid || source.Route == null)
            {
                issues.AddRange(source.Issues);
                continue;
            }
            unresolved.Add(new UnresolvedMutation(mutation, source.Route));
        }
        if (issues.Count != 0)
            return new PreparationResult(Array.Empty<PreparedMutation>(), issues);

        var ordered = unresolved
            .OrderBy(static value => value.Route.Phase)
            .ThenBy(static value => value.Route.Priority)
            .ThenBy(static value => value.Intent.Source.SourceId, StringComparer.Ordinal)
            .ThenBy(static value => value.Intent.EventRef, StringComparer.Ordinal)
            .ThenBy(static value => value.Intent.Coordinate.Realm, StringComparer.Ordinal)
            .ThenBy(static value => value.Intent.Coordinate.OwnerKind)
            .ThenBy(static value => value.Intent.Coordinate.ResourceOwnerId, StringComparer.Ordinal)
            .ThenBy(static value => value.Intent.Coordinate.ResourceKey, StringComparer.Ordinal)
            .ThenBy(static value => value.Intent.Source.Operation)
            .ToArray();
        var prepared = new List<PreparedMutation>(ordered.Length);
        foreach (var value in ordered)
        {
            var operationId = identityFactory.CreateOperationId();
            var transitionId = identityFactory.CreateTransitionId();
            identityRegistry.ValidateOperation(operationId, issues);
            identityRegistry.ValidateTransition(transitionId, issues);
            prepared.Add(new PreparedMutation(
                value.Intent,
                value.Route,
                operationId,
                transitionId));
        }

        return issues.Count == 0
            ? new PreparationResult(prepared, Array.Empty<ValidationIssue>())
            : new PreparationResult(Array.Empty<PreparedMutation>(), issues);
    }

    private static ResourceTriggerGraphResult BuildGraph(
        IReadOnlyList<PreparedMutation> mutations)
    {
        var issues = new List<ValidationIssue>();
        var byKey = mutations.ToDictionary(static value => value.Intent.Key);
        var nodes = new List<ResourceTriggerGraphNode>(mutations.Count);
        foreach (var mutation in mutations)
        {
            var dependencies = new List<string>();
            foreach (var dependency in mutation.Intent.Dependencies)
            {
                if (!byKey.TryGetValue(dependency, out var producer))
                {
                    AddIssue(
                        issues,
                        "resource_graph_dependency_missing",
                        "one exact existing dependency operation",
                        Describe(dependency));
                    continue;
                }
                mutation.DependencyOperationIds[dependency] = producer.OperationId;
                dependencies.Add(producer.OperationId);
            }

            var eventRequirements = new List<ResourceEventRequirement>();
            foreach (var requirement in mutation.Intent.EventRequirements)
            {
                if (!byKey.TryGetValue(requirement.Producer, out var producer))
                {
                    AddIssue(
                        issues,
                        "resource_graph_dependency_missing",
                        "one exact existing event producer operation",
                        Describe(requirement.Producer));
                    continue;
                }
                mutation.DependencyOperationIds[requirement.Producer] = producer.OperationId;
                eventRequirements.Add(new ResourceEventRequirement(
                    producer.OperationId,
                    requirement.EventKind));
            }

            nodes.Add(new ResourceTriggerGraphNode(
                mutation.OperationId,
                mutation.Route.Phase,
                mutation.Route.Priority,
                mutation.Intent.Source.SourceId,
                mutation.OperationId,
                dependencies,
                eventRequirements));
        }

        return issues.Count == 0
            ? ResourceTriggerGraph.Build(nodes)
            : new ResourceTriggerGraphResult(null, issues);
    }

    private static CapacityPreparationResult PrepareCapacityTransitions(
        IReadOnlyList<ResourceCapacityIntent> transitions,
        AcceptedMechanicsIdentityFactory identityFactory,
        AllocatedIdentityRegistry identityRegistry)
    {
        var issues = new List<ValidationIssue>();
        var keys = new HashSet<ResourceCapacityOperationKey>();
        foreach (var transition in transitions)
        {
            if (!keys.Add(transition.Key))
            {
                AddIssue(
                    issues,
                    "resource_planner_duplicate_operation",
                    "one capacity transition per exact replay key",
                    Describe(transition.Key));
            }
        }
        if (issues.Count != 0)
            return new CapacityPreparationResult(Array.Empty<PreparedCapacity>(), issues);

        var prepared = transitions
            .OrderBy(static value => value.Phase)
            .ThenBy(static value => value.Priority)
            .ThenBy(static value => value.OriginId, StringComparer.Ordinal)
            .ThenBy(static value => value.EventRef, StringComparer.Ordinal)
            .ThenBy(static value => value.Coordinate.Realm, StringComparer.Ordinal)
            .ThenBy(static value => value.Coordinate.OwnerKind)
            .ThenBy(static value => value.Coordinate.ResourceOwnerId, StringComparer.Ordinal)
            .ThenBy(static value => value.Coordinate.ResourceKey, StringComparer.Ordinal)
            .ThenBy(static value => value.Operation)
            .Select(value => new PreparedCapacity(
                value,
                identityFactory.CreateOperationId(),
                identityFactory.CreateTransitionId()))
            .ToArray();
        foreach (var value in prepared)
        {
            identityRegistry.ValidateOperation(value.OperationId, issues);
            identityRegistry.ValidateTransition(value.TransitionId, issues);
        }
        return issues.Count == 0
            ? new CapacityPreparationResult(prepared, Array.Empty<ValidationIssue>())
            : new CapacityPreparationResult(Array.Empty<PreparedCapacity>(), issues);
    }

    private static bool RequirementsSatisfied(
        PreparedMutation mutation,
        IReadOnlyDictionary<string, HashSet<string>> producedEvents)
    {
        foreach (var requirement in mutation.Intent.EventRequirements)
        {
            var producer = mutation.DependencyOperationIds[requirement.Producer];
            if (!producedEvents.TryGetValue(producer, out var events) ||
                !events.Contains(requirement.EventKind))
            {
                return false;
            }
        }
        return true;
    }

    private static void ValidateAllocatedIdentity(
        string value,
        string label,
        HashSet<string> exact,
        HashSet<string> aliases,
        List<ValidationIssue> issues)
    {
        var alias = ResourceMaterializationContract.BuildConfusableKey(value);
        if (!ResourceMaterializationContract.IsExactIdentifier(value) ||
            !exact.Add(value) ||
            !aliases.Add(alias))
        {
            AddIssue(
                issues,
                $"resource_planner_{label}_identity_invalid",
                $"one exact/confusable-unique client-owned {label} identity",
                value);
        }
    }

    private static AcceptedMechanicsResourcePlanningResult Failure(
        IEnumerable<ValidationIssue> issues,
        AcceptedMechanicsPlannerStatistics statistics) =>
        new(
            null,
            null,
            Array.Empty<ResourceAppliedEvent>(),
            Array.Empty<ResourceTransition>(),
            Array.Empty<ResourceTransition>(),
            issues.ToArray(),
            statistics);

    private static IReadOnlyList<ValidationIssue> Issue(
        string code,
        string expected,
        string actual)
    {
        var issues = new List<ValidationIssue>();
        AddIssue(issues, code, expected, actual);
        return issues;
    }

    private static AcceptedMechanicsPlannerStatistics Statistics(
        ResourceHistoryWorkingSet? history = null) =>
        new(
            history?.BaselineSeedCount ?? 0,
            history?.IncrementalAppendCount ?? 0,
            history?.FreezeCount ?? 0);

    private static string Describe(ResourceOperationKey key) =>
        $"{key.EventRef}/{key.OriginKind}/{key.OriginId}/" +
        $"{key.Coordinate.Realm}/{key.Coordinate.ResourceOwnerId}/" +
        $"{key.Coordinate.ResourceKey}/{key.Operation}";

    private static string Describe(ResourceCapacityOperationKey key) =>
        $"{key.EventRef}/{key.OriginKind}/{key.OriginId}/" +
        $"{key.Coordinate.Realm}/{key.Coordinate.ResourceOwnerId}/" +
        $"{key.Coordinate.ResourceKey}/{key.Operation}";

    private static void AddIssue(
        List<ValidationIssue> issues,
        string code,
        string expected,
        string actual) =>
        ResourceMaterializationContract.AddIssue(
            issues,
            ResourceMaterializationContract.CommandPath,
            code,
            expected,
            actual);

    private sealed record UnresolvedMutation(
        ResourceMutationIntent Intent,
        ResourceAuthorizedSourceRoute Route);

    private sealed class PreparedMutation
    {
        internal PreparedMutation(
            ResourceMutationIntent intent,
            ResourceAuthorizedSourceRoute route,
            string operationId,
            string transitionId)
        {
            Intent = intent;
            Route = route;
            OperationId = operationId;
            TransitionId = transitionId;
            DependencyOperationIds = new Dictionary<ResourceOperationKey, string>();
        }

        internal ResourceMutationIntent Intent { get; }
        internal ResourceAuthorizedSourceRoute Route { get; }
        internal string OperationId { get; }
        internal string TransitionId { get; }
        internal Dictionary<ResourceOperationKey, string> DependencyOperationIds { get; }
    }

    private sealed record PreparedCapacity(
        ResourceCapacityIntent Intent,
        string OperationId,
        string TransitionId);

    private sealed class AllocatedIdentityRegistry
    {
        private readonly HashSet<string> _operationIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _operationAliases = new(StringComparer.Ordinal);
        private readonly HashSet<string> _transitionIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _transitionAliases = new(StringComparer.Ordinal);

        internal void ValidateOperation(
            string value,
            List<ValidationIssue> issues) =>
            ValidateAllocatedIdentity(
                value,
                "operation",
                _operationIds,
                _operationAliases,
                issues);

        internal void ValidateTransition(
            string value,
            List<ValidationIssue> issues) =>
            ValidateAllocatedIdentity(
                value,
                "transition",
                _transitionIds,
                _transitionAliases,
                issues);
    }

    private sealed record CapacityPreparationResult(
        IReadOnlyList<PreparedCapacity> Transitions,
        IReadOnlyList<ValidationIssue> Issues);

    private sealed record PreparationResult(
        IReadOnlyList<PreparedMutation> Mutations,
        IReadOnlyList<ValidationIssue> Issues);
}

internal sealed record ResourceEventRequirement(
    string ProducerNodeId,
    string EventKind);

internal sealed record ResourceTriggerGraphNode(
    string NodeId,
    ResourceMutationPhase Phase,
    int Priority,
    string OriginId,
    string OperationId,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<ResourceEventRequirement> EventRequirements);

internal sealed record ResourceTriggerGraphResult(
    ResourceTriggerGraph? Graph,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Graph != null && Issues.Count == 0;
}

internal sealed class ResourceTriggerGraph
{
    private static readonly FrozenSet<string> EventKinds = new[]
    {
        "resource_damaged",
        "resource_restored",
        "resource_spent",
        "resource_gained",
        "resource_depleted",
        "resource_filled"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly IComparer<ResourceTriggerGraphNode> ReadyComparer =
        Comparer<ResourceTriggerGraphNode>.Create(static (left, right) =>
        {
            var comparison = left.Phase.CompareTo(right.Phase);
            if (comparison != 0)
                return comparison;

            comparison = left.Priority.CompareTo(right.Priority);
            if (comparison != 0)
                return comparison;

            comparison = string.CompareOrdinal(left.OriginId, right.OriginId);
            if (comparison != 0)
                return comparison;

            comparison = string.CompareOrdinal(left.OperationId, right.OperationId);
            return comparison != 0
                ? comparison
                : string.CompareOrdinal(left.NodeId, right.NodeId);
        });

    private ResourceTriggerGraph(
        IReadOnlyList<ResourceTriggerGraphNode> orderedNodes,
        int maximumDepth)
    {
        OrderedNodes = orderedNodes;
        MaximumDepth = maximumDepth;
    }

    internal IReadOnlyList<ResourceTriggerGraphNode> OrderedNodes { get; }
    internal int MaximumDepth { get; }

    internal static ResourceTriggerGraphResult Build(
        IEnumerable<ResourceTriggerGraphNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var candidates = nodes.ToArray();
        var issues = new List<ValidationIssue>();
        if (candidates.Length > ResourceMaterializationContract.MaxTriggerNodes)
        {
            AddIssue(
                issues,
                "resource_graph_node_limit_exceeded",
                $"at most {ResourceMaterializationContract.MaxTriggerNodes} graph nodes",
                candidates.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return Invalid(issues);
        }

        var byId = new Dictionary<string, ResourceTriggerGraphNode>(StringComparer.Ordinal);
        var confusableIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            if (!ValidateNodeShape(candidate, issues))
                continue;

            if (!byId.TryAdd(candidate.NodeId, candidate) ||
                !confusableIds.Add(
                    ResourceMaterializationContract.BuildConfusableKey(candidate.NodeId)))
            {
                AddIssue(
                    issues,
                    "resource_graph_duplicate_node",
                    "one exact/confusable node identity",
                    candidate.NodeId);
            }
        }

        if (issues.Count != 0)
            return Invalid(issues);

        var parents = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var children = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            parents[candidate.NodeId] = new HashSet<string>(StringComparer.Ordinal);
            children[candidate.NodeId] = new HashSet<string>(StringComparer.Ordinal);
        }

        foreach (var candidate in candidates)
        {
            foreach (var dependency in candidate.Dependencies)
            {
                RegisterDependency(
                    candidate,
                    dependency,
                    byId,
                    parents,
                    children,
                    issues);
            }

            foreach (var requirement in candidate.EventRequirements)
            {
                if (requirement.EventKind == null ||
                    !EventKinds.Contains(requirement.EventKind))
                {
                    AddIssue(
                        issues,
                        "resource_graph_event_invalid",
                        "one closed resource event kind",
                        requirement.EventKind ?? "null");
                    continue;
                }

                RegisterDependency(
                    candidate,
                    requirement.ProducerNodeId,
                    byId,
                    parents,
                    children,
                    issues);
            }
        }

        if (issues.Count != 0)
            return Invalid(issues);

        var depths = new Dictionary<string, int>(StringComparer.Ordinal);
        var indegrees = parents.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.Count,
            StringComparer.Ordinal);
        var ready = new SortedSet<ResourceTriggerGraphNode>(ReadyComparer);
        foreach (var candidate in candidates)
        {
            if (indegrees[candidate.NodeId] == 0)
                ready.Add(candidate);
        }

        var ordered = new List<ResourceTriggerGraphNode>(candidates.Length);
        var maximumDepth = 0;
        while (ready.Count != 0)
        {
            var current = ready.Min!;
            ready.Remove(current);
            var currentDepth = parents[current.NodeId].Count == 0
                ? 1
                : parents[current.NodeId].Max(parent => depths[parent]) + 1;
            depths[current.NodeId] = currentDepth;
            maximumDepth = Math.Max(maximumDepth, currentDepth);
            ordered.Add(current);

            foreach (var childId in children[current.NodeId])
            {
                if (--indegrees[childId] == 0)
                    ready.Add(byId[childId]);
            }
        }

        if (ordered.Count != candidates.Length)
        {
            AddIssue(
                issues,
                "resource_graph_cycle",
                "one acyclic resource dependency graph",
                $"{candidates.Length - ordered.Count} cyclic node(s)");
            return Invalid(issues);
        }

        if (maximumDepth > ResourceMaterializationContract.MaxTriggerDepth)
        {
            AddIssue(
                issues,
                "resource_graph_depth_limit_exceeded",
                $"maximum graph depth {ResourceMaterializationContract.MaxTriggerDepth}",
                maximumDepth.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return Invalid(issues);
        }

        return new ResourceTriggerGraphResult(
            new ResourceTriggerGraph(
                new ReadOnlyCollection<ResourceTriggerGraphNode>(ordered),
                maximumDepth),
            Array.Empty<ValidationIssue>());
    }

    private static bool ValidateNodeShape(
        ResourceTriggerGraphNode? candidate,
        List<ValidationIssue> issues)
    {
        if (candidate == null)
        {
            AddIssue(
                issues,
                "resource_graph_node_invalid",
                "one non-null graph node",
                "null");
            return false;
        }

        var valid = true;
        valid &= RequireIdentifier(candidate.NodeId, "nodeId", issues);
        valid &= RequireIdentifier(candidate.OriginId, "originId", issues);
        valid &= RequireIdentifier(candidate.OperationId, "operationId", issues);
        if (!Enum.IsDefined(candidate.Phase) || candidate.Priority < 0)
        {
            AddIssue(
                issues,
                "resource_graph_node_invalid",
                "one registered phase and non-negative priority",
                $"phase={candidate.Phase};priority={candidate.Priority}");
            valid = false;
        }

        if (candidate.Dependencies == null || candidate.EventRequirements == null)
        {
            AddIssue(
                issues,
                "resource_graph_node_invalid",
                "present dependency and event requirement collections",
                "null collection");
            return false;
        }

        foreach (var dependency in candidate.Dependencies)
            valid &= RequireIdentifier(dependency, "dependency", issues);
        foreach (var requirement in candidate.EventRequirements)
        {
            if (requirement == null)
            {
                AddIssue(
                    issues,
                    "resource_graph_event_invalid",
                    "one non-null resource event requirement",
                    "null");
                valid = false;
                continue;
            }

            valid &= RequireIdentifier(
                requirement.ProducerNodeId,
                "event producer nodeId",
                issues);
            valid &= RequireIdentifier(requirement.EventKind, "event kind", issues);
        }

        return valid;
    }

    private static bool RequireIdentifier(
        string? value,
        string label,
        List<ValidationIssue> issues)
    {
        if (ResourceMaterializationContract.IsExactIdentifier(value))
            return true;

        AddIssue(
            issues,
            "resource_graph_node_invalid",
            $"one exact {label}",
            value ?? "null");
        return false;
    }

    private static void RegisterDependency(
        ResourceTriggerGraphNode candidate,
        string dependencyId,
        IReadOnlyDictionary<string, ResourceTriggerGraphNode> byId,
        IReadOnlyDictionary<string, HashSet<string>> parents,
        IReadOnlyDictionary<string, HashSet<string>> children,
        List<ValidationIssue> issues)
    {
        if (!byId.TryGetValue(dependencyId, out var dependency))
        {
            AddIssue(
                issues,
                "resource_graph_dependency_missing",
                "one exact existing dependency nodeId",
                dependencyId);
            return;
        }

        if (candidate.Phase < dependency.Phase)
        {
            AddIssue(
                issues,
                "resource_graph_phase_inversion",
                "dependency phase not later than dependent phase",
                $"{dependency.NodeId}:{dependency.Phase}->{candidate.NodeId}:{candidate.Phase}");
            return;
        }

        if (parents[candidate.NodeId].Add(dependencyId))
            children[dependencyId].Add(candidate.NodeId);
    }

    private static ResourceTriggerGraphResult Invalid(
        List<ValidationIssue> issues) =>
        new(null, new ReadOnlyCollection<ValidationIssue>(issues));

    private static void AddIssue(
        List<ValidationIssue> issues,
        string code,
        string expected,
        string actual) =>
        ResourceMaterializationContract.AddIssue(
            issues,
            ResourceMaterializationContract.CommandPath + ".triggerGraph",
            code,
            expected,
            actual);
}
