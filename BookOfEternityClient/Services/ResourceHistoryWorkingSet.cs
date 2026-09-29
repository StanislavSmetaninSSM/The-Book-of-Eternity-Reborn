using System.Collections.ObjectModel;

namespace BookOfEternityClient.Services;

internal sealed record ResourceHistoryWorkingAppendResult(
    ResourceTransition? Transition,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Transition != null && Issues.Count == 0;
}

internal sealed class ResourceHistoryWorkingSet
{
    private readonly ResourceHistoryState _baseline;
    private readonly List<ResourceTransition> _pending = new();
    private readonly Dictionary<ReplayKey, ResourceTransition> _replays = new();
    private readonly HashSet<string> _transitionIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _transitionAliases = new(StringComparer.Ordinal);
    private readonly HashSet<string> _operationIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _operationAliases = new(StringComparer.Ordinal);
    private readonly HashSet<string> _receiptIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _receiptAliases = new(StringComparer.Ordinal);
    private readonly HashSet<ExecutionSlot> _executionSlots = new();
    private readonly Dictionary<ResourceCoordinate, ResourceTransition> _tails =
        new(ResourceCoordinateComparer.Instance);
    private readonly HashSet<ResourceCoordinate> _terminalCoordinates =
        new(ResourceCoordinateComparer.Instance);
    private bool _frozen;

    internal ResourceHistoryWorkingSet(ResourceHistoryState baseline)
    {
        _baseline = baseline ?? throw new ArgumentNullException(nameof(baseline));
        BaselineSeedCount = 1;
        foreach (var transition in baseline.Transitions)
            Seed(transition);
    }

    internal int BaselineSeedCount { get; }
    internal int BaselineTransitionVisitCount { get; private set; }
    internal int PendingCount => _pending.Count;
    internal int IncrementalAppendCount { get; private set; }
    internal int FreezeCount { get; private set; }
    internal int WholeHistoryRebuildCount { get; private set; }
    internal int FullHistoryValidationTransitionVisitCount { get; private set; }
    internal int ReplayIdentityLookupCount { get; private set; }
    internal long TotalWorkUnits =>
        (long)BaselineTransitionVisitCount +
        IncrementalAppendCount +
        FullHistoryValidationTransitionVisitCount +
        ReplayIdentityLookupCount;
    internal IReadOnlyList<ResourceTransition> PendingTransitions =>
        new ReadOnlyCollection<ResourceTransition>(_pending.ToArray());

    internal bool IsTerminal(ResourceCoordinate coordinate) =>
        _terminalCoordinates.Contains(coordinate);

    internal bool TryResolveReplayIdentity(
        string eventRef,
        string originKind,
        string originId,
        ResourceCoordinate coordinate,
        ResourceTransitionOperation operation,
        out ResourceTransition? transition)
    {
        ReplayIdentityLookupCount++;
        return _replays.TryGetValue(
            new ReplayKey(eventRef, originKind, originId, coordinate, operation),
            out transition);
    }

    internal ResourceReplayResult ResolveReplay(ResourceReplayProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        if (!_replays.TryGetValue(ToReplayKey(probe), out var existing))
        {
            return new ResourceReplayResult(
                ResourceReplayDisposition.None,
                null,
                Array.Empty<ValidationIssue>());
        }
        if (ResourceHistoryState.ReplaySemanticsMatch(existing, probe))
        {
            return new ResourceReplayResult(
                ResourceReplayDisposition.Exact,
                existing,
                Array.Empty<ValidationIssue>());
        }

        var issues = new List<ValidationIssue>();
        Add(
            issues,
            "resource_transition_conflicting_replay",
            "exact prior replay semantics",
            Describe(ToReplayKey(probe)));
        return new ResourceReplayResult(
            ResourceReplayDisposition.Conflict,
            existing,
            issues);
    }

    internal ResourceHistoryWorkingAppendResult TryAppend(ResourceTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        EnsureMutable();
        var issues = new List<ValidationIssue>();
        ValidateIdentity(
            transition.TransitionId,
            "transition",
            _transitionIds,
            _transitionAliases,
            issues);
        ValidateIdentity(
            transition.OperationId,
            "operation",
            _operationIds,
            _operationAliases,
            issues);
        if (transition.ReceiptId != null)
        {
            ValidateIdentity(
                transition.ReceiptId,
                "receipt",
                _receiptIds,
                _receiptAliases,
                issues);
        }
        if (_executionSlots.Contains(new ExecutionSlot(
                transition.Turn,
                transition.ExecutionSequence)))
        {
            Add(
                issues,
                "resource_history_duplicate_execution_sequence",
                "unique executionSequence within accepted turn",
                $"turn={transition.Turn};sequence={transition.ExecutionSequence}");
        }
        if (_replays.TryGetValue(ToReplayKey(transition), out var prior))
        {
            Add(
                issues,
                ResourceHistoryState.ReplaySemanticsMatch(
                    prior,
                    ResourceHistoryState.CreateReplayProbe(transition))
                    ? "resource_history_duplicate_replay_entry"
                    : "resource_transition_conflicting_replay",
                "one transition per replay key",
                Describe(ToReplayKey(transition)));
        }
        if (_terminalCoordinates.Contains(transition.Coordinate))
        {
            Add(
                issues,
                "resource_history_transition_after_terminal",
                "no transition after retire",
                transition.TransitionId);
        }
        if (_tails.TryGetValue(transition.Coordinate, out var tail))
        {
            if (tail.AfterState == null ||
                transition.BeforeState == null ||
                tail.AfterState != transition.BeforeState)
            {
                Add(
                    issues,
                    "resource_history_continuity_mismatch",
                    "new beforeState equals prior afterState",
                    transition.TransitionId);
            }
        }
        else if (transition.Operation != ResourceTransitionOperation.Initialize ||
                 transition.BeforeState != null)
        {
            Add(
                issues,
                "resource_history_continuity_mismatch",
                "first coordinate transition initializes from absence",
                transition.TransitionId);
        }

        if (issues.Count != 0)
            return new ResourceHistoryWorkingAppendResult(null, issues.ToArray());

        Index(transition);
        _pending.Add(transition);
        IncrementalAppendCount++;
        return new ResourceHistoryWorkingAppendResult(
            transition,
            Array.Empty<ValidationIssue>());
    }

    internal ResourceHistoryStateResult Freeze(ResourceDefinitionCatalog definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        EnsureMutable();
        _frozen = true;
        FreezeCount++;
        WholeHistoryRebuildCount++;
        FullHistoryValidationTransitionVisitCount +=
            _baseline.Transitions.Count + _pending.Count;
        return ResourceHistoryState.CreateValidated(
            _baseline.Transitions.Concat(_pending),
            definitions);
    }

    /// <summary>
    /// Validates a detached current history without freezing this working set or changing its work counters.
    /// </summary>
    /// <param name="definitions">
    /// Exact resource definitions governing the baseline and pending transitions.
    /// </param>
    /// <returns>
    /// Current validated history or diagnostics, leaving later appends available.
    /// </returns>
    internal ResourceHistoryStateResult ReadValidatedSnapshot(ResourceDefinitionCatalog definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        EnsureMutable();
        return ResourceHistoryState.CreateValidated(_baseline.Transitions.Concat(_pending), definitions);
    }

    private void Seed(ResourceTransition transition)
    {
        BaselineTransitionVisitCount++;
        _transitionIds.Add(transition.TransitionId);
        _transitionAliases.Add(Alias(transition.TransitionId));
        _operationIds.Add(transition.OperationId);
        _operationAliases.Add(Alias(transition.OperationId));
        if (transition.ReceiptId != null)
        {
            _receiptIds.Add(transition.ReceiptId);
            _receiptAliases.Add(Alias(transition.ReceiptId));
        }
        Index(transition);
    }

    private void Index(ResourceTransition transition)
    {
        _transitionIds.Add(transition.TransitionId);
        _transitionAliases.Add(Alias(transition.TransitionId));
        _operationIds.Add(transition.OperationId);
        _operationAliases.Add(Alias(transition.OperationId));
        if (transition.ReceiptId != null)
        {
            _receiptIds.Add(transition.ReceiptId);
            _receiptAliases.Add(Alias(transition.ReceiptId));
        }
        _executionSlots.Add(new ExecutionSlot(
            transition.Turn,
            transition.ExecutionSequence));
        _replays[ToReplayKey(transition)] = transition;
        _tails[transition.Coordinate] = transition;
        if (transition.Operation == ResourceTransitionOperation.Retire)
            _terminalCoordinates.Add(transition.Coordinate);
    }

    private static void ValidateIdentity(
        string value,
        string kind,
        HashSet<string> exact,
        HashSet<string> aliases,
        List<ValidationIssue> issues)
    {
        if (!ResourceMaterializationContract.IsExactIdentifier(value))
        {
            Add(
                issues,
                $"resource_history_{kind}_id_invalid",
                "exact client-owned identifier",
                value);
            return;
        }
        var alias = Alias(value);
        if (exact.Contains(value) || aliases.Contains(alias))
        {
            Add(
                issues,
                $"resource_history_duplicate_{kind}_id",
                "exact/confusable-unique client-owned identifier",
                value);
        }
    }

    private void EnsureMutable()
    {
        if (_frozen)
            throw new InvalidOperationException("A frozen resource history working set cannot be reused.");
    }

    private static string Alias(string value) =>
        ResourceMaterializationContract.BuildConfusableKey(value);

    private static ReplayKey ToReplayKey(ResourceTransition transition) => new(
        transition.EventRef,
        transition.OriginKind,
        transition.OriginId,
        transition.Coordinate,
        transition.Operation);

    private static ReplayKey ToReplayKey(ResourceReplayProbe probe) => new(
        probe.EventRef,
        probe.OriginKind,
        probe.OriginId,
        probe.Coordinate,
        probe.Operation);

    private static string Describe(ReplayKey key) =>
        $"{key.EventRef}/{key.OriginKind}/{key.OriginId}/" +
        $"{key.Coordinate.Realm}/{key.Coordinate.ResourceOwnerId}/" +
        $"{key.Coordinate.ResourceKey}/{key.Operation}";

    private static void Add(
        List<ValidationIssue> issues,
        string code,
        string expected,
        string actual) =>
        ResourceMaterializationContract.AddIssue(
            issues,
            ResourceMaterializationContract.HistoryPath,
            code,
            expected,
            actual);

    private sealed record ReplayKey(
        string EventRef,
        string OriginKind,
        string OriginId,
        ResourceCoordinate Coordinate,
        ResourceTransitionOperation Operation);

    private sealed record ExecutionSlot(int Turn, int Sequence);
}
