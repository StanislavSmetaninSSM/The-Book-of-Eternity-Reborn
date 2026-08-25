using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal enum ResourceTransitionOperation
{
    Initialize,
    Reconfigure,
    Suspend,
    Resume,
    Retire,
    Damage,
    Restore,
    Spend,
    Gain
}

internal enum ResourceMutationPhase
{
    DirectCost,
    DirectOutcome,
    RegisteredSystemOutcome,
    EffectTrigger
}

internal enum ResourceTransitionOutcome
{
    Applied,
    ClampedMinimum,
    ClampedMaximum
}

internal enum ResourceCapacityDisposition
{
    InitializeFromDefinition,
    Preserve,
    ClampToNewMaximum,
    ScaleRatioExact
}

internal enum ResourceReplayDisposition
{
    None,
    Exact,
    Conflict
}

internal sealed record ResourceSourceEvidence(
    string SourceKind,
    string SourceId,
    string AuthorityFingerprint);

internal sealed record ResourceTransition(
    string TransitionId,
    string OperationId,
    string EventRef,
    string OriginKind,
    string OriginId,
    ResourceMutationPhase Phase,
    int Priority,
    int ExecutionSequence,
    ResourceCoordinate Coordinate,
    ResourceTransitionOperation Operation,
    decimal RequestedAmount,
    decimal AppliedAmount,
    ResourceTransitionOutcome Outcome,
    ResourceCapacityDisposition? CapacityDisposition,
    ResourceStateSnapshot? BeforeState,
    ResourceStateSnapshot? AfterState,
    ResourceSourceEvidence SourceEvidence,
    string PolicyFingerprint,
    string? ReceiptId,
    int Turn);

internal sealed record ResourceReplayProbe(
    string EventRef,
    string OriginKind,
    string OriginId,
    ResourceCoordinate Coordinate,
    ResourceTransitionOperation Operation,
    decimal RequestedAmount,
    ResourceMutationPhase Phase,
    int Priority,
    int ExecutionSequence,
    ResourceCapacityDisposition? CapacityDisposition,
    ResourceSourceEvidence SourceEvidence,
    string PolicyFingerprint,
    string? ReceiptId);

internal sealed record ResourceReplayResult(
    ResourceReplayDisposition Disposition,
    ResourceTransition? Transition,
    IReadOnlyList<ValidationIssue> Issues);

internal sealed record ResourceHistoryAppendResult(
    ResourceHistoryState? History,
    ResourceTransition? Transition,
    bool IsReplay,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => History != null && Transition != null && Issues.Count == 0;
}

internal sealed record ResourceHistoryStateResult(
    ResourceHistoryState? History,
    IReadOnlyList<ValidationIssue> Issues,
    bool IsMissing = false)
{
    internal bool IsValid => History != null && Issues.Count == 0;
}

internal sealed class ResourceHistoryState
{
    private static readonly FrozenSet<string> RootFields = Set("schemaVersion", "entries");
    private static readonly FrozenSet<string> TransitionFields = Set(
        "transitionId",
        "operationId",
        "eventRef",
        "originKind",
        "originId",
        "phase",
        "priority",
        "executionSequence",
        "coordinate",
        "operation",
        "requestedAmount",
        "appliedAmount",
        "outcome",
        "capacityDisposition",
        "beforeState",
        "afterState",
        "sourceEvidence",
        "policyFingerprint",
        "receiptId",
        "turn");
    private static readonly FrozenSet<string> SourceEvidenceFields = Set(
        "sourceKind",
        "sourceId",
        "authorityFingerprint");

    private readonly FrozenDictionary<string, ResourceTransition> _byTransitionId;
    private readonly FrozenDictionary<ResourceReplayKey, ResourceTransition> _byReplayKey;
    private readonly FrozenSet<ResourceCoordinate> _terminalCoordinates;

    private ResourceHistoryState(
        IEnumerable<ResourceTransition> transitions,
        ResourceAuthorityWorkMeter? workMeter = null)
    {
        IComparer<ResourceTransition> comparer = ResourceTransitionComparer.Instance;
        if (workMeter != null)
        {
            comparer = new ResourceAuthorityCountingComparer<ResourceTransition>(
                comparer,
                workMeter.CompareInitialHistory);
        }
        var ordered = transitions
            .OrderBy(static transition => transition, comparer)
            .ToArray();
        Transitions = new ReadOnlyCollection<ResourceTransition>(ordered);
        workMeter?.VisitInitialHistoryIndex(ordered.Length * 3L);
        _byTransitionId = ordered.ToFrozenDictionary(
            static transition => transition.TransitionId,
            StringComparer.Ordinal);
        _byReplayKey = ordered.ToFrozenDictionary(ToReplayKey);
        _terminalCoordinates = ordered
            .Where(static transition => transition.Operation == ResourceTransitionOperation.Retire)
            .Select(static transition => transition.Coordinate)
            .ToFrozenSet(ResourceCoordinateComparer.Instance);
        Fingerprint = ComputeFingerprint(ordered, workMeter);
    }

    internal IReadOnlyList<ResourceTransition> Transitions { get; }

    internal string Fingerprint { get; }

    internal static ResourceHistoryStateResult ParseCanonical(
        string? json,
        ResourceDefinitionCatalog definitions,
        bool allowMissingPristine,
        ResourceAuthorityWorkMeter? workMeter = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        if (json == null)
        {
            if (allowMissingPristine)
            {
                return new ResourceHistoryStateResult(
                    new ResourceHistoryState(
                        Array.Empty<ResourceTransition>(),
                        workMeter),
                    Array.Empty<ValidationIssue>(),
                    IsMissing: true);
            }

            return Failure(
                "resource_history_root_missing",
                "present canonical resource history root",
                "missing",
                isMissing: true);
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return Failure(
                "resource_history_invalid_root",
                "non-empty strict JSON object",
                "empty or whitespace-only file");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            return Failure(
                "resource_history_invalid_json",
                "well-formed strict JSON object",
                exception.GetType().Name);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Failure(
                    "resource_history_invalid_root",
                    "strict JSON object",
                    root.ValueKind.ToString());
            }

            var issues = new List<ValidationIssue>();
            ResourceMaterializationContract.FindDuplicateProperties(
                root,
                ResourceMaterializationContract.HistoryPath,
                issues,
                "resource_history_duplicate_property");
            ResourceMaterializationContract.ValidateClosedObject(
                root,
                ResourceMaterializationContract.HistoryPath,
                RootFields,
                issues,
                "resource_history_unknown_field");
            if (!TryReadInt(root, "schemaVersion", out var schemaVersion) ||
                schemaVersion != ResourceMaterializationContract.SchemaVersion)
            {
                Add(
                    issues,
                    ResourceMaterializationContract.HistoryPath + ".schemaVersion",
                    "resource_history_invalid_field",
                    ResourceMaterializationContract.SchemaVersion.ToString(
                        CultureInfo.InvariantCulture),
                    ResourceMaterializationContract.Describe(root, "schemaVersion"));
            }

            if (!root.TryGetProperty("entries", out var entries) ||
                entries.ValueKind != JsonValueKind.Array)
            {
                Add(
                    issues,
                    ResourceMaterializationContract.HistoryPath + ".entries",
                    "resource_history_invalid_field",
                    "untruncated transition array",
                    ResourceMaterializationContract.Describe(root, "entries"));
                return new ResourceHistoryStateResult(null, issues.ToArray());
            }

            var parsed = new List<ResourceTransition>();
            var index = 0;
            foreach (var value in entries.EnumerateArray())
            {
                workMeter?.VisitInitialHistoryInput();
                var path = $"{ResourceMaterializationContract.HistoryPath}.entries[{index++}]";
                var transition = ParseTransition(value, path, definitions, issues);
                if (transition != null)
                    parsed.Add(transition);
            }

            ValidateTransitions(parsed, definitions, issues, workMeter);
            return issues.Count == 0
                ? new ResourceHistoryStateResult(
                    new ResourceHistoryState(parsed, workMeter),
                    issues)
                : new ResourceHistoryStateResult(null, issues.ToArray());
        }
    }

    internal static ResourceHistoryStateResult CreateValidated(
        IEnumerable<ResourceTransition> transitions,
        ResourceDefinitionCatalog definitions,
        ResourceAuthorityWorkMeter? workMeter = null)
    {
        ArgumentNullException.ThrowIfNull(transitions);
        ArgumentNullException.ThrowIfNull(definitions);
        var candidates = transitions.ToArray();
        workMeter?.VisitInitialHistoryInput(candidates.Length);
        var issues = new List<ValidationIssue>();
        ValidateTransitions(candidates, definitions, issues, workMeter);
        return issues.Count == 0
            ? new ResourceHistoryStateResult(
                new ResourceHistoryState(candidates, workMeter),
                Array.Empty<ValidationIssue>())
            : new ResourceHistoryStateResult(null, issues.ToArray());
    }

    internal static ResourceReplayProbe CreateReplayProbe(ResourceTransition transition) =>
        ToReplayProbe(transition);

    internal static bool ReplaySemanticsMatch(
        ResourceTransition transition,
        ResourceReplayProbe probe) =>
        ReplaySemanticsEqual(transition, probe);

    internal bool TryResolveExactTransition(
        string transitionId,
        out ResourceTransition? transition) =>
        _byTransitionId.TryGetValue(transitionId, out transition);

    internal bool IsTerminal(ResourceCoordinate coordinate) =>
        _terminalCoordinates.Contains(coordinate);

    internal IReadOnlyList<ValidationIssue> ValidateStateAgreement(
        ResourceStateLedger ledger,
        ResourceAuthorityWorkMeter? workMeter = null)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        var issues = new List<ValidationIssue>();
        var historyCoordinates = new HashSet<ResourceCoordinate>(
            ResourceCoordinateComparer.Instance);
        workMeter?.VisitStateAgreementHistory(Transitions.Count);
        foreach (var group in Transitions.GroupBy(
                     static transition => transition.Coordinate,
                     ResourceCoordinateComparer.Instance))
        {
            historyCoordinates.Add(group.Key);
            IComparer<ResourceTransition> comparer = ResourceTransitionComparer.Instance;
            if (workMeter != null)
            {
                comparer = new ResourceAuthorityCountingComparer<ResourceTransition>(
                    comparer,
                    workMeter.CompareStateAgreementHistory);
            }
            var ordered = group
                .OrderBy(static transition => transition, comparer)
                .ToArray();
            var first = ordered[0];
            var latest = ordered[^1];
            var hasLiveState = ledger.TryResolveExact(group.Key, out var stateEntry);
            if (latest.AfterState == null)
            {
                if (hasLiveState)
                {
                    Add(
                        issues,
                        ResourceMaterializationContract.StatePath,
                        "resource_state_history_terminal_mismatch",
                        "retired coordinate absent from live state",
                        latest.TransitionId);
                }

                continue;
            }

            if (!hasLiveState || stateEntry == null)
            {
                Add(
                    issues,
                    ResourceMaterializationContract.StatePath,
                    "resource_state_history_live_state_missing",
                    "one live state for nonterminal history coordinate",
                    latest.TransitionId);
                continue;
            }

            if (stateEntry.Snapshot != latest.AfterState)
            {
                Add(
                    issues,
                    ResourceMaterializationContract.StatePath,
                    "resource_state_history_snapshot_mismatch",
                    "live snapshot equals latest immutable history after-state",
                    latest.TransitionId);
            }

            if (stateEntry.Chronology.CreatedAtTurn != first.Turn ||
                !string.Equals(
                    stateEntry.Chronology.CreatedEventRef,
                    first.EventRef,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    stateEntry.Chronology.LastTransitionId,
                    latest.TransitionId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    stateEntry.Chronology.LastEventRef,
                    latest.EventRef,
                    StringComparison.Ordinal) ||
                stateEntry.Chronology.LastTransitionTurn != latest.Turn)
            {
                Add(
                    issues,
                    ResourceMaterializationContract.StatePath,
                    "resource_state_history_chronology_mismatch",
                    "live chronology equals first/latest immutable transitions",
                    stateEntry.Chronology.LastTransitionId);
            }
        }

        foreach (var entry in ledger.Entries)
        {
            workMeter?.VisitStateAgreementState();
            if (!historyCoordinates.Contains(entry.Coordinate))
            {
                Add(
                    issues,
                    ResourceMaterializationContract.HistoryPath,
                    "resource_state_history_missing",
                    "immutable initialization/history for every live coordinate",
                    entry.Chronology.LastTransitionId);
            }
        }

        return issues.ToArray();
    }

    internal ResourceReplayResult ResolveReplay(ResourceReplayProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        if (!_byReplayKey.TryGetValue(ToReplayKey(probe), out var existing))
        {
            return new ResourceReplayResult(
                ResourceReplayDisposition.None,
                null,
                Array.Empty<ValidationIssue>());
        }

        if (ReplaySemanticsEqual(existing, probe))
        {
            return new ResourceReplayResult(
                ResourceReplayDisposition.Exact,
                existing,
                Array.Empty<ValidationIssue>());
        }

        var issues = new List<ValidationIssue>();
        Add(
            issues,
            ResourceMaterializationContract.HistoryPath,
            "resource_transition_conflicting_replay",
            "exact prior replay semantics",
            DescribeReplayKey(ToReplayKey(probe)));
        return new ResourceReplayResult(
            ResourceReplayDisposition.Conflict,
            existing,
            issues);
    }

    internal ResourceHistoryAppendResult Append(
        ResourceTransition transition,
        ResourceDefinitionCatalog definitions)
    {
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(definitions);
        var replay = ResolveReplay(ToReplayProbe(transition));
        if (replay.Disposition == ResourceReplayDisposition.Exact)
        {
            return new ResourceHistoryAppendResult(
                this,
                replay.Transition,
                IsReplay: true,
                Array.Empty<ValidationIssue>());
        }

        if (replay.Disposition == ResourceReplayDisposition.Conflict)
        {
            return new ResourceHistoryAppendResult(
                null,
                null,
                IsReplay: false,
                replay.Issues);
        }

        var candidates = Transitions.Append(transition).ToArray();
        var issues = new List<ValidationIssue>();
        ValidateTransitions(candidates, definitions, issues);
        if (issues.Count != 0)
        {
            return new ResourceHistoryAppendResult(
                null,
                null,
                IsReplay: false,
                issues.ToArray());
        }

        return new ResourceHistoryAppendResult(
            new ResourceHistoryState(candidates),
            transition,
            IsReplay: false,
            Array.Empty<ValidationIssue>());
    }

    internal string ToCanonicalJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", ResourceMaterializationContract.SchemaVersion);
            writer.WriteStartArray("entries");
            foreach (var transition in Transitions)
                WriteTransition(writer, transition);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static ResourceTransition? ParseTransition(
        JsonElement value,
        string path,
        ResourceDefinitionCatalog definitions,
        List<ValidationIssue> issues)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            Add(
                issues,
                path,
                "resource_history_transition_invalid",
                "strict transition object",
                value.ValueKind.ToString());
            return null;
        }

        ResourceMaterializationContract.ValidateClosedObject(
            value,
            path,
            TransitionFields,
            issues,
            "resource_history_unknown_field");
        var transitionId = ReadIdentifier(value, path, "transitionId", issues);
        var operationId = ReadIdentifier(value, path, "operationId", issues);
        var eventRef = ReadIdentifier(value, path, "eventRef", issues);
        var originKind = ReadIdentifier(value, path, "originKind", issues);
        var originId = ReadIdentifier(value, path, "originId", issues);
        var phase = ParsePhase(value, path, issues);
        var priority = ReadInteger(value, path, "priority", issues);
        var executionSequence = ReadInteger(value, path, "executionSequence", issues);
        var coordinate = value.TryGetProperty("coordinate", out var coordinateNode)
            ? ResourceStateContract.ParseCoordinateObject(
                coordinateNode,
                path + ".coordinate",
                issues)
            : MissingCoordinate(path, issues);
        var operation = ParseOperation(value, path, issues);
        var requested = ReadDecimal(value, path, "requestedAmount", issues);
        var applied = ReadDecimal(value, path, "appliedAmount", issues);
        var outcome = ParseOutcome(value, path, issues);
        var capacityDisposition = ParseCapacityDisposition(
            value,
            path,
            issues,
            out var capacityDispositionPresent);
        var before = ParseOptionalSnapshot(
            value,
            "beforeState",
            coordinate,
            definitions,
            path,
            issues,
            out var beforePresent);
        var after = ParseOptionalSnapshot(
            value,
            "afterState",
            coordinate,
            definitions,
            path,
            issues,
            out var afterPresent);
        var source = ParseSourceEvidence(value, path, issues);
        var policyFingerprint = ReadIdentifier(
            value,
            path,
            "policyFingerprint",
            issues);
        if (policyFingerprint != null &&
            !ResourceMaterializationContract.IsAuthorityFingerprint(policyFingerprint))
        {
            Add(
                issues,
                path + ".policyFingerprint",
                "resource_history_policy_invalid",
                "exact lowercase SHA-256 policy fingerprint",
                policyFingerprint);
        }
        var receiptId = ParseReceipt(value, path, issues, out var receiptPresent);
        var turn = ReadInteger(value, path, "turn", issues);

        if (transitionId == null ||
            operationId == null ||
            eventRef == null ||
            originKind == null ||
            originId == null ||
            phase == null ||
            !priority.HasValue ||
            !executionSequence.HasValue ||
            coordinate == null ||
            operation == null ||
            !requested.HasValue ||
            !applied.HasValue ||
            outcome == null ||
            !capacityDispositionPresent ||
            !beforePresent ||
            !afterPresent ||
            source == null ||
            policyFingerprint == null ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(policyFingerprint) ||
            !receiptPresent ||
            !turn.HasValue)
        {
            return null;
        }

        return new ResourceTransition(
            transitionId,
            operationId,
            eventRef,
            originKind,
            originId,
            phase.Value,
            priority.Value,
            executionSequence.Value,
            coordinate,
            operation.Value,
            requested.Value,
            applied.Value,
            outcome.Value,
            capacityDisposition,
            before,
            after,
            source,
            policyFingerprint,
            receiptId,
            turn.Value);
    }

    private static void ValidateTransitions(
        IReadOnlyList<ResourceTransition> transitions,
        ResourceDefinitionCatalog definitions,
        List<ValidationIssue> issues,
        ResourceAuthorityWorkMeter? workMeter = null)
    {
        var exactTransitionIds = new HashSet<string>(StringComparer.Ordinal);
        var confusableTransitionIds = new HashSet<string>(StringComparer.Ordinal);
        var exactOperationIds = new HashSet<string>(StringComparer.Ordinal);
        var confusableOperationIds = new HashSet<string>(StringComparer.Ordinal);
        var exactReceiptIds = new HashSet<string>(StringComparer.Ordinal);
        var confusableReceiptIds = new HashSet<string>(StringComparer.Ordinal);
        var executionSlots = new HashSet<ResourceExecutionSlot>();
        var exactCoordinates = new HashSet<ResourceCoordinate>(
            ResourceCoordinateComparer.Instance);
        var confusableCoordinates = new Dictionary<
            ConfusableHistoryCoordinate,
            ResourceCoordinate>();
        var replayEntries = new Dictionary<ResourceReplayKey, ResourceTransition>();
        foreach (var transition in transitions)
        {
            workMeter?.VisitInitialHistoryValidation();
            var path = ResourceMaterializationContract.HistoryPath + ".entries";
            ValidateTypedIdentity(
                transition.TransitionId,
                path + ".transitionId",
                "transition",
                exactTransitionIds,
                confusableTransitionIds,
                issues);
            ValidateTypedIdentity(
                transition.OperationId,
                path + ".operationId",
                "operation",
                exactOperationIds,
                confusableOperationIds,
                issues);
            if (transition.ReceiptId != null)
            {
                ValidateTypedIdentity(
                    transition.ReceiptId,
                    path + ".receiptId",
                    "receipt",
                    exactReceiptIds,
                    confusableReceiptIds,
                    issues);
            }

            if (!executionSlots.Add(new ResourceExecutionSlot(
                    transition.Turn,
                    transition.ExecutionSequence)))
            {
                Add(
                    issues,
                    path + ".executionSequence",
                    "resource_history_duplicate_execution_sequence",
                    "globally unique execution sequence within one turn",
                    $"turn={transition.Turn};sequence={transition.ExecutionSequence}");
            }

            if (exactCoordinates.Add(transition.Coordinate))
            {
                var confusableCoordinate = ToConfusableCoordinate(transition.Coordinate);
                if (confusableCoordinates.TryGetValue(
                        confusableCoordinate,
                        out var priorCoordinate) &&
                    !ResourceCoordinateComparer.Instance.Equals(
                        priorCoordinate,
                        transition.Coordinate))
                {
                    Add(
                        issues,
                        path + ".coordinate",
                        "resource_history_confusable_coordinate",
                        "globally exact/confusable-unique history coordinate",
                        DescribeCoordinate(transition.Coordinate));
                }
                else
                {
                    confusableCoordinates[confusableCoordinate] = transition.Coordinate;
                }
            }

            ValidateTypedTransition(transition, definitions, path, issues);
            var replayKey = ToReplayKey(transition);
            if (replayEntries.TryGetValue(replayKey, out var prior))
            {
                Add(
                    issues,
                    path,
                    ReplaySemanticsEqual(prior, ToReplayProbe(transition))
                        ? "resource_history_duplicate_replay_entry"
                        : "resource_transition_conflicting_replay",
                    "one immutable transition per replay key",
                    DescribeReplayKey(replayKey));
            }
            else
            {
                replayEntries.Add(replayKey, transition);
            }
        }

        ValidateCoordinateChains(transitions, issues, workMeter);
    }

    private static void ValidateTypedIdentity(
        string identity,
        string path,
        string kind,
        HashSet<string> exact,
        HashSet<string> confusable,
        List<ValidationIssue> issues)
    {
        if (!ResourceMaterializationContract.IsExactIdentifier(identity))
        {
            Add(
                issues,
                path,
                $"resource_history_{kind}_id_invalid",
                "exact client-owned identifier",
                identity);
            return;
        }

        if (!exact.Add(identity))
        {
            Add(
                issues,
                path,
                $"resource_history_duplicate_{kind}_id",
                $"globally unique {kind} identity",
                identity);
        }
        else if (!confusable.Add(ResourceMaterializationContract.BuildConfusableKey(identity)))
        {
            Add(
                issues,
                path,
                $"resource_history_confusable_{kind}_id",
                $"globally exact/confusable-unique {kind} identity",
                identity);
        }
    }

    private static void ValidateTypedTransition(
        ResourceTransition transition,
        ResourceDefinitionCatalog definitions,
        string path,
        List<ValidationIssue> issues)
    {
        if (transition.Priority < 0 ||
            transition.ExecutionSequence < 0 ||
            transition.Turn < 0 ||
            transition.RequestedAmount < 0m || transition.AppliedAmount < 0m ||
            !ResourceMaterializationContract.IsExactIdentifier(transition.EventRef) ||
            !ResourceMaterializationContract.IsExactIdentifier(transition.OriginKind) ||
            !ResourceMaterializationContract.IsExactIdentifier(transition.OriginId))
        {
            Add(
                issues,
                path,
                "resource_history_transition_invalid",
                "non-negative chronology/amounts and exact event/origin identity",
                transition.TransitionId);
        }

        if (!ResourceMaterializationContract.IsExactIdentifier(
                transition.SourceEvidence.SourceKind) ||
            !ResourceMaterializationContract.IsExactIdentifier(
                transition.SourceEvidence.SourceId) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(
                transition.SourceEvidence.AuthorityFingerprint))
        {
            Add(
                issues,
                path + ".sourceEvidence",
                "resource_history_source_invalid",
                "exact source identity and lowercase SHA-256 authority fingerprint",
                transition.SourceEvidence.ToString());
        }

        if (!ResourceMaterializationContract.IsAuthorityFingerprint(
                transition.PolicyFingerprint))
        {
            Add(
                issues,
                path + ".policyFingerprint",
                "resource_history_policy_invalid",
                "exact lowercase SHA-256 policy fingerprint",
                transition.PolicyFingerprint);
        }

        if (transition.ReceiptId != null &&
            !ResourceMaterializationContract.IsExactIdentifier(transition.ReceiptId))
        {
            Add(
                issues,
                path + ".receiptId",
                "resource_history_receipt_invalid",
                "null or exact receipt identity",
                transition.ReceiptId);
        }

        if (transition.BeforeState != null)
        {
            ResourceStateContract.TryValidateSnapshot(
                transition.Coordinate,
                transition.BeforeState,
                definitions,
                path + ".beforeState",
                issues);
        }

        if (transition.AfterState != null)
        {
            ResourceStateContract.TryValidateSnapshot(
                transition.Coordinate,
                transition.AfterState,
                definitions,
                path + ".afterState",
                issues);
        }

        if (transition.Operation is not (
                ResourceTransitionOperation.Initialize or
                ResourceTransitionOperation.Reconfigure) &&
            transition.CapacityDisposition != null)
        {
            Add(
                issues,
                path + ".capacityDisposition",
                "resource_history_capacity_disposition_invalid",
                "null outside initialize/reconfigure transitions",
                GetCapacityDispositionToken(transition.CapacityDisposition.Value));
        }

        ValidateOperationSemantics(transition, definitions, path, issues);
    }

    private static void ValidateOperationSemantics(
        ResourceTransition transition,
        ResourceDefinitionCatalog definitions,
        string path,
        List<ValidationIssue> issues)
    {
        switch (transition.Operation)
        {
            case ResourceTransitionOperation.Initialize:
                if (transition.BeforeState != null ||
                    transition.AfterState == null ||
                    transition.AfterState.State != ResourceLifecycleState.Active ||
                    transition.CapacityDisposition !=
                        ResourceCapacityDisposition.InitializeFromDefinition ||
                    !MatchesStaticInitializationPolicy(transition, definitions) ||
                    transition.RequestedAmount != 0m ||
                    transition.AppliedAmount != 0m ||
                    transition.Outcome != ResourceTransitionOutcome.Applied)
                {
                    InvalidTransition(
                        transition,
                        path,
                        issues,
                        "absent -> exact definition-initialized live evidence");
                }
                return;

            case ResourceTransitionOperation.Retire:
                if (transition.BeforeState == null ||
                    transition.AfterState != null ||
                    transition.CapacityDisposition != null ||
                    transition.RequestedAmount != 0m ||
                    transition.AppliedAmount != 0m ||
                    transition.Outcome != ResourceTransitionOutcome.Applied ||
                    !string.Equals(
                        transition.SourceEvidence.SourceKind,
                        "owner_lifecycle",
                        StringComparison.Ordinal))
                {
                    Add(
                        issues,
                        path,
                        "resource_history_terminal_invalid",
                        "live -> absent retire with exact owner_lifecycle evidence",
                        transition.TransitionId);
                }
                return;

            case ResourceTransitionOperation.Suspend:
                ValidateLifecycleTransition(
                    transition,
                    ResourceLifecycleState.Active,
                    ResourceLifecycleState.Suspended,
                    path,
                    issues);
                return;

            case ResourceTransitionOperation.Resume:
                ValidateLifecycleTransition(
                    transition,
                    ResourceLifecycleState.Suspended,
                    ResourceLifecycleState.Active,
                    path,
                    issues);
                return;

            case ResourceTransitionOperation.Reconfigure:
                ValidateReconfigureTransition(transition, path, issues);
                return;

            case ResourceTransitionOperation.Damage:
            case ResourceTransitionOperation.Restore:
            case ResourceTransitionOperation.Spend:
            case ResourceTransitionOperation.Gain:
                ValidateOrdinaryTransition(transition, definitions, path, issues);
                return;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static bool MatchesStaticInitializationPolicy(
        ResourceTransition transition,
        ResourceDefinitionCatalog definitions)
    {
        if (transition.AfterState == null ||
            !definitions.TryResolveExact(transition.Coordinate.ResourceKey, out var definition) ||
            definition == null)
        {
            return true;
        }

        return definition.InitializationPolicy.Kind switch
        {
            ResourceInitializationKind.Minimum =>
                transition.AfterState.Current == definition.MinimumPolicy.Value,
            ResourceInitializationKind.Maximum =>
                transition.AfterState.Current == transition.AfterState.Maximum,
            ResourceInitializationKind.Fixed =>
                definition.InitializationPolicy.Value.HasValue &&
                transition.AfterState.Current == definition.InitializationPolicy.Value.Value,
            ResourceInitializationKind.RegisteredFormula => true,
            _ => false
        };
    }

    private static void ValidateLifecycleTransition(
        ResourceTransition transition,
        ResourceLifecycleState beforeState,
        ResourceLifecycleState afterState,
        string path,
        List<ValidationIssue> issues)
    {
        if (transition.BeforeState == null ||
            transition.AfterState == null ||
            transition.CapacityDisposition != null ||
            transition.RequestedAmount != 0m ||
            transition.AppliedAmount != 0m ||
            transition.Outcome != ResourceTransitionOutcome.Applied ||
            transition.BeforeState.State != beforeState ||
            transition.AfterState.State != afterState ||
            transition.BeforeState.Current != transition.AfterState.Current ||
            transition.BeforeState.Maximum != transition.AfterState.Maximum ||
            transition.BeforeState.CapacityBinding != transition.AfterState.CapacityBinding)
        {
            InvalidTransition(transition, path, issues, "exact lifecycle-only transition");
        }
    }

    private static void ValidateReconfigureTransition(
        ResourceTransition transition,
        string path,
        List<ValidationIssue> issues)
    {
        var before = transition.BeforeState;
        var after = transition.AfterState;
        if (before == null ||
            after == null ||
            transition.CapacityDisposition is null or
                ResourceCapacityDisposition.InitializeFromDefinition ||
            transition.RequestedAmount != 0m ||
            transition.AppliedAmount != 0m ||
            before.State != after.State ||
            before.Maximum == after.Maximum &&
            before.CapacityBinding == after.CapacityBinding)
        {
            InvalidTransition(
                transition,
                path,
                issues,
                "complete capacity reconfiguration evidence with changed authority");
            return;
        }

        var valid = transition.CapacityDisposition switch
        {
            ResourceCapacityDisposition.Preserve =>
                after.Current == before.Current &&
                transition.Outcome == ResourceTransitionOutcome.Applied,
            ResourceCapacityDisposition.ClampToNewMaximum =>
                ValidateClampReconfigure(transition, before, after),
            ResourceCapacityDisposition.ScaleRatioExact =>
                ValidateExactRatioReconfigure(transition, before, after),
            _ => false
        };
        if (!valid)
        {
            InvalidTransition(
                transition,
                path,
                issues,
                "exact preserve, authorized maximum clamp, or exactly scaled ratio evidence");
        }
    }

    private static bool ValidateClampReconfigure(
        ResourceTransition transition,
        ResourceStateSnapshot before,
        ResourceStateSnapshot after)
    {
        var expected = Math.Min(before.Current, after.Maximum);
        var expectedOutcome = expected < before.Current
            ? ResourceTransitionOutcome.ClampedMaximum
            : ResourceTransitionOutcome.Applied;
        return after.Current == expected && transition.Outcome == expectedOutcome;
    }

    private static bool ValidateExactRatioReconfigure(
        ResourceTransition transition,
        ResourceStateSnapshot before,
        ResourceStateSnapshot after)
    {
        if (before.Maximum == 0m ||
            transition.Outcome != ResourceTransitionOutcome.Applied)
        {
            return false;
        }

        return ResourceMaterializationContract.ProductsEqualExact(
            after.Current,
            before.Maximum,
            before.Current,
            after.Maximum);
    }

    private static void ValidateOrdinaryTransition(
        ResourceTransition transition,
        ResourceDefinitionCatalog definitions,
        string path,
        List<ValidationIssue> issues)
    {
        var hasDefinition = definitions.TryResolveExact(
            transition.Coordinate.ResourceKey,
            out var definition) && definition != null;
        var amountsAlign = hasDefinition &&
            ResourceMaterializationContract.IsQuantumAligned(
                transition.RequestedAmount,
                0m,
                definition!.Quantum) &&
            ResourceMaterializationContract.IsQuantumAligned(
                transition.AppliedAmount,
                0m,
                definition.Quantum) &&
            (definition.NumericKind != ResourceNumericKind.Integer ||
             ResourceMaterializationContract.IsIntegral(transition.RequestedAmount) &&
             ResourceMaterializationContract.IsIntegral(transition.AppliedAmount));
        if (transition.BeforeState == null ||
            transition.AfterState == null ||
            transition.CapacityDisposition != null ||
            transition.RequestedAmount <= 0m ||
            transition.AppliedAmount < 0m ||
            transition.AppliedAmount > transition.RequestedAmount ||
            transition.BeforeState.State != ResourceLifecycleState.Active ||
            transition.AfterState.State != ResourceLifecycleState.Active ||
            transition.BeforeState.Maximum != transition.AfterState.Maximum ||
            transition.BeforeState.CapacityBinding != transition.AfterState.CapacityBinding ||
            !amountsAlign)
        {
            InvalidTransition(transition, path, issues, "authorized active-state ordinary mutation evidence");
            return;
        }

        var subtracts = transition.Operation is
            ResourceTransitionOperation.Damage or ResourceTransitionOperation.Spend;
        var requestedArithmeticIsExact = subtracts
            ? ResourceMaterializationContract.TrySubtractExact(
                transition.BeforeState.Current,
                transition.RequestedAmount,
                out var requestedCandidate)
            : ResourceMaterializationContract.TryAddExact(
                transition.BeforeState.Current,
                transition.RequestedAmount,
                out requestedCandidate);
        var appliedArithmeticIsExact = subtracts
            ? ResourceMaterializationContract.TrySubtractExact(
                transition.BeforeState.Current,
                transition.AppliedAmount,
                out var expectedAfter)
            : ResourceMaterializationContract.TryAddExact(
                transition.BeforeState.Current,
                transition.AppliedAmount,
                out expectedAfter);
        if (!requestedArithmeticIsExact || !appliedArithmeticIsExact)
        {
            InvalidTransition(
                transition,
                path,
                issues,
                "exactly representable non-overflowing arithmetic");
            return;
        }

        var validOutcome = transition.Outcome switch
        {
            ResourceTransitionOutcome.Applied =>
                transition.AppliedAmount == transition.RequestedAmount,
            ResourceTransitionOutcome.ClampedMinimum =>
                subtracts &&
                requestedCandidate < definition!.MinimumPolicy.Value &&
                transition.AppliedAmount < transition.RequestedAmount &&
                transition.AfterState.Current == definition!.MinimumPolicy.Value,
            ResourceTransitionOutcome.ClampedMaximum =>
                !subtracts &&
                requestedCandidate > transition.AfterState.Maximum &&
                transition.AppliedAmount < transition.RequestedAmount &&
                transition.AfterState.Current == transition.AfterState.Maximum,
            _ => false
        };
        var allowedOperation = TryMapOrdinaryOperation(
                transition.Operation,
                out var ordinaryOperation) &&
            hasDefinition &&
            definition!.AllowedOperations.Contains(ordinaryOperation);
        if (transition.AfterState.Current != expectedAfter || !validOutcome || !allowedOperation)
        {
            InvalidTransition(transition, path, issues, "exact allowed operation arithmetic and outcome");
        }
    }

    private static bool TryMapOrdinaryOperation(
        ResourceTransitionOperation operation,
        out ResourceOperation ordinaryOperation)
    {
        ordinaryOperation = operation switch
        {
            ResourceTransitionOperation.Damage => ResourceOperation.Damage,
            ResourceTransitionOperation.Restore => ResourceOperation.Restore,
            ResourceTransitionOperation.Spend => ResourceOperation.Spend,
            ResourceTransitionOperation.Gain => ResourceOperation.Gain,
            _ => default
        };
        return operation is
            ResourceTransitionOperation.Damage or
            ResourceTransitionOperation.Restore or
            ResourceTransitionOperation.Spend or
            ResourceTransitionOperation.Gain;
    }

    private static void ValidateCoordinateChains(
        IEnumerable<ResourceTransition> transitions,
        List<ValidationIssue> issues,
        ResourceAuthorityWorkMeter? workMeter)
    {
        foreach (var group in transitions.GroupBy(
                     static transition => transition.Coordinate,
                     ResourceCoordinateComparer.Instance))
        {
            IComparer<ResourceTransition> comparer = ResourceTransitionComparer.Instance;
            if (workMeter != null)
            {
                comparer = new ResourceAuthorityCountingComparer<ResourceTransition>(
                    comparer,
                    workMeter.CompareInitialHistory);
            }
            var ordered = group
                .OrderBy(static transition => transition, comparer)
                .ToArray();
            if (ordered.Length == 0)
                continue;
            if (ordered[0].Operation != ResourceTransitionOperation.Initialize ||
                ordered[0].BeforeState != null)
            {
                Add(
                    issues,
                    ResourceMaterializationContract.HistoryPath,
                    "resource_history_continuity_mismatch",
                    "first coordinate transition is initialize from absence",
                    ordered[0].TransitionId);
            }

            ResourceStateSnapshot? priorAfter = null;
            var terminal = false;
            for (var index = 0; index < ordered.Length; index++)
            {
                workMeter?.VisitInitialHistoryChain();
                var transition = ordered[index];
                if (terminal)
                {
                    Add(
                        issues,
                        ResourceMaterializationContract.HistoryPath,
                        "resource_history_transition_after_terminal",
                        "no transition after immutable retirement",
                        transition.TransitionId);
                    continue;
                }

                if (index > 0 && priorAfter != transition.BeforeState)
                {
                    Add(
                        issues,
                        ResourceMaterializationContract.HistoryPath,
                        "resource_history_continuity_mismatch",
                        "exact previous after-state equals next before-state",
                        transition.TransitionId);
                }

                priorAfter = transition.AfterState;
                terminal = transition.Operation == ResourceTransitionOperation.Retire;
            }
        }
    }

    private static ResourceStateSnapshot? ParseOptionalSnapshot(
        JsonElement root,
        string field,
        ResourceCoordinate? coordinate,
        ResourceDefinitionCatalog definitions,
        string path,
        List<ValidationIssue> issues,
        out bool present)
    {
        present = root.TryGetProperty(field, out var value);
        if (!present)
        {
            Add(
                issues,
                path + "." + field,
                "resource_history_transition_invalid",
                "explicit state snapshot object or null",
                "missing");
            return null;
        }

        if (value.ValueKind == JsonValueKind.Null)
            return null;
        if (coordinate == null)
            return null;
        return ResourceStateContract.ParseSnapshotObject(
            value,
            coordinate,
            definitions,
            path + "." + field,
            issues);
    }

    private static ResourceSourceEvidence? ParseSourceEvidence(
        JsonElement root,
        string path,
        List<ValidationIssue> issues)
    {
        var sourcePath = path + ".sourceEvidence";
        if (!root.TryGetProperty("sourceEvidence", out var value) ||
            value.ValueKind != JsonValueKind.Object)
        {
            Add(
                issues,
                sourcePath,
                "resource_history_source_invalid",
                "strict source-evidence object",
                ResourceMaterializationContract.Describe(root, "sourceEvidence"));
            return null;
        }

        ResourceMaterializationContract.ValidateClosedObject(
            value,
            sourcePath,
            SourceEvidenceFields,
            issues,
            "resource_history_unknown_field");
        var kind = ReadIdentifier(value, sourcePath, "sourceKind", issues);
        var id = ReadIdentifier(value, sourcePath, "sourceId", issues);
        var fingerprint = ReadIdentifier(
            value,
            sourcePath,
            "authorityFingerprint",
            issues);
        if (fingerprint != null &&
            !ResourceMaterializationContract.IsAuthorityFingerprint(fingerprint))
        {
            Add(
                issues,
                sourcePath + ".authorityFingerprint",
                "resource_history_source_invalid",
                "exact lowercase SHA-256 source fingerprint",
                fingerprint);
        }

        return kind != null &&
               id != null &&
               fingerprint != null &&
               ResourceMaterializationContract.IsAuthorityFingerprint(fingerprint)
            ? new ResourceSourceEvidence(kind, id, fingerprint)
            : null;
    }

    private static string? ParseReceipt(
        JsonElement root,
        string path,
        List<ValidationIssue> issues,
        out bool present)
    {
        present = root.TryGetProperty("receiptId", out var value);
        if (!present)
        {
            Add(
                issues,
                path + ".receiptId",
                "resource_history_receipt_invalid",
                "explicit null or exact receipt identity",
                "missing");
            return null;
        }

        if (value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.String ||
            !ResourceMaterializationContract.IsExactIdentifier(value.GetString()))
        {
            Add(
                issues,
                path + ".receiptId",
                "resource_history_receipt_invalid",
                "explicit null or exact receipt identity",
                value.GetRawText());
            return null;
        }

        return value.GetString();
    }

    private static ResourceCoordinate? MissingCoordinate(
        string path,
        List<ValidationIssue> issues)
    {
        Add(
            issues,
            path + ".coordinate",
            "resource_state_coordinate_invalid",
            "strict resource-coordinate object",
            "missing");
        return null;
    }

    private static ResourceMutationPhase? ParsePhase(
        JsonElement root,
        string path,
        List<ValidationIssue> issues)
    {
        var token = ReadIdentifier(root, path, "phase", issues);
        var result = token switch
        {
            "direct_cost" => ResourceMutationPhase.DirectCost,
            "direct_outcome" => ResourceMutationPhase.DirectOutcome,
            "registered_system_outcome" => ResourceMutationPhase.RegisteredSystemOutcome,
            "effect_trigger" => ResourceMutationPhase.EffectTrigger,
            _ => (ResourceMutationPhase?)null
        };
        if (token != null && result == null)
            InvalidToken(path + ".phase", "closed resource phase", token, issues);
        return result;
    }

    private static ResourceTransitionOperation? ParseOperation(
        JsonElement root,
        string path,
        List<ValidationIssue> issues)
    {
        var token = ReadIdentifier(root, path, "operation", issues);
        var result = token switch
        {
            "initialize" => ResourceTransitionOperation.Initialize,
            "reconfigure" => ResourceTransitionOperation.Reconfigure,
            "suspend" => ResourceTransitionOperation.Suspend,
            "resume" => ResourceTransitionOperation.Resume,
            "retire" => ResourceTransitionOperation.Retire,
            "damage" => ResourceTransitionOperation.Damage,
            "restore" => ResourceTransitionOperation.Restore,
            "spend" => ResourceTransitionOperation.Spend,
            "gain" => ResourceTransitionOperation.Gain,
            _ => (ResourceTransitionOperation?)null
        };
        if (token != null && result == null)
            InvalidToken(path + ".operation", "closed resource transition operation", token, issues);
        return result;
    }

    private static ResourceTransitionOutcome? ParseOutcome(
        JsonElement root,
        string path,
        List<ValidationIssue> issues)
    {
        var token = ReadIdentifier(root, path, "outcome", issues);
        var result = token switch
        {
            "applied" => ResourceTransitionOutcome.Applied,
            "clamped_minimum" => ResourceTransitionOutcome.ClampedMinimum,
            "clamped_maximum" => ResourceTransitionOutcome.ClampedMaximum,
            _ => (ResourceTransitionOutcome?)null
        };
        if (token != null && result == null)
            InvalidToken(path + ".outcome", "stored non-replay transition outcome", token, issues);
        return result;
    }

    private static ResourceCapacityDisposition? ParseCapacityDisposition(
        JsonElement root,
        string path,
        List<ValidationIssue> issues,
        out bool present)
    {
        present = root.TryGetProperty("capacityDisposition", out var value);
        if (!present)
        {
            Add(
                issues,
                path + ".capacityDisposition",
                "resource_history_capacity_disposition_invalid",
                "explicit null or closed capacity disposition",
                "missing");
            return null;
        }

        if (value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.String)
        {
            Add(
                issues,
                path + ".capacityDisposition",
                "resource_history_capacity_disposition_invalid",
                "initialize_from_definition, preserve, clamp_to_new_maximum, scale_ratio_exact, or null",
                value.ValueKind.ToString());
            return null;
        }

        var token = value.GetString();
        var disposition = token switch
        {
            "initialize_from_definition" =>
                ResourceCapacityDisposition.InitializeFromDefinition,
            "preserve" => ResourceCapacityDisposition.Preserve,
            "clamp_to_new_maximum" => ResourceCapacityDisposition.ClampToNewMaximum,
            "scale_ratio_exact" => ResourceCapacityDisposition.ScaleRatioExact,
            _ => (ResourceCapacityDisposition?)null
        };
        if (disposition == null)
        {
            Add(
                issues,
                path + ".capacityDisposition",
                "resource_history_capacity_disposition_invalid",
                "initialize_from_definition, preserve, clamp_to_new_maximum, scale_ratio_exact, or null",
                token ?? "null");
        }

        return disposition;
    }

    private static string? ReadIdentifier(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            !ResourceMaterializationContract.IsExactIdentifier(value.GetString()))
        {
            Add(
                issues,
                path + "." + field,
                field == "policyFingerprint"
                    ? "resource_history_policy_invalid"
                    : field is "sourceKind" or "sourceId" or "authorityFingerprint"
                        ? "resource_history_source_invalid"
                        : "resource_history_invalid_field",
                "trimmed FormKC-safe exact identifier",
                ResourceMaterializationContract.Describe(root, field));
            return null;
        }

        return value.GetString();
    }

    private static decimal? ReadDecimal(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) ||
            !ResourceMaterializationContract.TryReadExactDecimal(value, out var number))
        {
            Add(
                issues,
                path + "." + field,
                "resource_history_transition_invalid",
                "exact supported decimal",
                ResourceMaterializationContract.Describe(root, field));
            return null;
        }

        return number;
    }

    private static int? ReadInteger(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        if (!TryReadInt(root, field, out var value))
        {
            Add(
                issues,
                path + "." + field,
                "resource_history_transition_invalid",
                "exact integer",
                ResourceMaterializationContract.Describe(root, field));
            return null;
        }

        return value;
    }

    private static bool TryReadInt(JsonElement root, string field, out int value)
    {
        value = 0;
        return root.TryGetProperty(field, out var node) &&
               node.ValueKind == JsonValueKind.Number &&
               node.TryGetInt32(out value);
    }

    private static void InvalidToken(
        string path,
        string expected,
        string actual,
        List<ValidationIssue> issues) =>
        Add(issues, path, "resource_history_transition_invalid", expected, actual);

    private static void InvalidTransition(
        ResourceTransition transition,
        string path,
        List<ValidationIssue> issues,
        string expected) =>
        Add(
            issues,
            path,
            "resource_history_transition_invalid",
            expected,
            transition.TransitionId);

    private static ResourceReplayProbe ToReplayProbe(ResourceTransition transition) => new(
        transition.EventRef,
        transition.OriginKind,
        transition.OriginId,
        transition.Coordinate,
        transition.Operation,
        transition.RequestedAmount,
        transition.Phase,
        transition.Priority,
        transition.ExecutionSequence,
        transition.CapacityDisposition,
        transition.SourceEvidence,
        transition.PolicyFingerprint,
        transition.ReceiptId);

    private static ResourceReplayKey ToReplayKey(ResourceTransition transition) => new(
        transition.EventRef,
        transition.OriginKind,
        transition.OriginId,
        transition.Coordinate,
        transition.Operation);

    private static ResourceReplayKey ToReplayKey(ResourceReplayProbe probe) => new(
        probe.EventRef,
        probe.OriginKind,
        probe.OriginId,
        probe.Coordinate,
        probe.Operation);

    private static bool ReplaySemanticsEqual(
        ResourceTransition existing,
        ResourceReplayProbe probe) =>
        existing.RequestedAmount == probe.RequestedAmount &&
        existing.Phase == probe.Phase &&
        existing.Priority == probe.Priority &&
        existing.ExecutionSequence == probe.ExecutionSequence &&
        existing.CapacityDisposition == probe.CapacityDisposition &&
        existing.SourceEvidence == probe.SourceEvidence &&
        string.Equals(existing.PolicyFingerprint, probe.PolicyFingerprint, StringComparison.Ordinal) &&
        string.Equals(existing.ReceiptId, probe.ReceiptId, StringComparison.Ordinal);

    private static string ComputeFingerprint(
        IEnumerable<ResourceTransition> transitions,
        ResourceAuthorityWorkMeter? workMeter = null)
    {
        using var builder = new ResourceFingerprintBuilder("resource-history-v1");
        foreach (var transition in transitions)
        {
            workMeter?.VisitInitialHistoryFingerprint();
            builder.Append(transition.TransitionId);
            builder.Append(transition.OperationId);
            builder.Append(transition.EventRef);
            builder.Append(transition.OriginKind);
            builder.Append(transition.OriginId);
            builder.Append(GetPhaseToken(transition.Phase));
            builder.Append(transition.Priority);
            builder.Append(transition.ExecutionSequence);
            ResourceStateContract.AppendCoordinate(builder, transition.Coordinate);
            builder.Append(GetOperationToken(transition.Operation));
            builder.Append(transition.RequestedAmount);
            builder.Append(transition.AppliedAmount);
            builder.Append(GetOutcomeToken(transition.Outcome));
            builder.Append(transition.CapacityDisposition != null);
            if (transition.CapacityDisposition != null)
                builder.Append(GetCapacityDispositionToken(transition.CapacityDisposition.Value));
            AppendOptionalSnapshot(builder, transition.BeforeState);
            AppendOptionalSnapshot(builder, transition.AfterState);
            builder.Append(transition.SourceEvidence.SourceKind);
            builder.Append(transition.SourceEvidence.SourceId);
            builder.Append(transition.SourceEvidence.AuthorityFingerprint);
            builder.Append(transition.PolicyFingerprint);
            builder.Append(transition.ReceiptId != null);
            if (transition.ReceiptId != null)
                builder.Append(transition.ReceiptId);
            builder.Append(transition.Turn);
        }

        return builder.Build();
    }

    private static void AppendOptionalSnapshot(
        ResourceFingerprintBuilder builder,
        ResourceStateSnapshot? snapshot)
    {
        builder.Append(snapshot != null);
        if (snapshot != null)
            ResourceStateContract.AppendSnapshot(builder, snapshot);
    }

    private static void WriteTransition(Utf8JsonWriter writer, ResourceTransition transition)
    {
        writer.WriteStartObject();
        writer.WriteString("transitionId", transition.TransitionId);
        writer.WriteString("operationId", transition.OperationId);
        writer.WriteString("eventRef", transition.EventRef);
        writer.WriteString("originKind", transition.OriginKind);
        writer.WriteString("originId", transition.OriginId);
        writer.WriteString("phase", GetPhaseToken(transition.Phase));
        writer.WriteNumber("priority", transition.Priority);
        writer.WriteNumber("executionSequence", transition.ExecutionSequence);
        ResourceStateContract.WriteCoordinateObject(writer, "coordinate", transition.Coordinate);
        writer.WriteString("operation", GetOperationToken(transition.Operation));
        ResourceStateContract.WriteCanonicalDecimal(
            writer,
            "requestedAmount",
            transition.RequestedAmount);
        ResourceStateContract.WriteCanonicalDecimal(
            writer,
            "appliedAmount",
            transition.AppliedAmount);
        writer.WriteString("outcome", GetOutcomeToken(transition.Outcome));
        if (transition.CapacityDisposition == null)
            writer.WriteNull("capacityDisposition");
        else
            writer.WriteString(
                "capacityDisposition",
                GetCapacityDispositionToken(transition.CapacityDisposition.Value));
        ResourceStateContract.WriteSnapshot(writer, "beforeState", transition.BeforeState);
        ResourceStateContract.WriteSnapshot(writer, "afterState", transition.AfterState);
        writer.WriteStartObject("sourceEvidence");
        writer.WriteString("sourceKind", transition.SourceEvidence.SourceKind);
        writer.WriteString("sourceId", transition.SourceEvidence.SourceId);
        writer.WriteString(
            "authorityFingerprint",
            transition.SourceEvidence.AuthorityFingerprint);
        writer.WriteEndObject();
        writer.WriteString("policyFingerprint", transition.PolicyFingerprint);
        if (transition.ReceiptId == null)
            writer.WriteNull("receiptId");
        else
            writer.WriteString("receiptId", transition.ReceiptId);
        writer.WriteNumber("turn", transition.Turn);
        writer.WriteEndObject();
    }

    private static string GetPhaseToken(ResourceMutationPhase phase) => phase switch
    {
        ResourceMutationPhase.DirectCost => "direct_cost",
        ResourceMutationPhase.DirectOutcome => "direct_outcome",
        ResourceMutationPhase.RegisteredSystemOutcome => "registered_system_outcome",
        ResourceMutationPhase.EffectTrigger => "effect_trigger",
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null)
    };

    private static string GetOperationToken(ResourceTransitionOperation operation) => operation switch
    {
        ResourceTransitionOperation.Initialize => "initialize",
        ResourceTransitionOperation.Reconfigure => "reconfigure",
        ResourceTransitionOperation.Suspend => "suspend",
        ResourceTransitionOperation.Resume => "resume",
        ResourceTransitionOperation.Retire => "retire",
        ResourceTransitionOperation.Damage => "damage",
        ResourceTransitionOperation.Restore => "restore",
        ResourceTransitionOperation.Spend => "spend",
        ResourceTransitionOperation.Gain => "gain",
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
    };

    private static string GetOutcomeToken(ResourceTransitionOutcome outcome) => outcome switch
    {
        ResourceTransitionOutcome.Applied => "applied",
        ResourceTransitionOutcome.ClampedMinimum => "clamped_minimum",
        ResourceTransitionOutcome.ClampedMaximum => "clamped_maximum",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
    };

    private static string GetCapacityDispositionToken(
        ResourceCapacityDisposition disposition) => disposition switch
    {
        ResourceCapacityDisposition.InitializeFromDefinition => "initialize_from_definition",
        ResourceCapacityDisposition.Preserve => "preserve",
        ResourceCapacityDisposition.ClampToNewMaximum => "clamp_to_new_maximum",
        ResourceCapacityDisposition.ScaleRatioExact => "scale_ratio_exact",
        _ => throw new ArgumentOutOfRangeException(nameof(disposition), disposition, null)
    };

    private static string DescribeReplayKey(ResourceReplayKey key) =>
        $"{key.EventRef}/{key.OriginKind}/{key.OriginId}/" +
        $"{key.Coordinate.Realm}/{key.Coordinate.ResourceOwnerId}/" +
        $"{key.Coordinate.ResourceKey}/{GetOperationToken(key.Operation)}";

    private static ConfusableHistoryCoordinate ToConfusableCoordinate(
        ResourceCoordinate coordinate) => new(
        ResourceMaterializationContract.BuildConfusableKey(coordinate.Realm),
        coordinate.OwnerKind,
        ResourceMaterializationContract.BuildConfusableKey(coordinate.ResourceOwnerId),
        ResourceMaterializationContract.BuildConfusableKey(coordinate.ResourceKey));

    private static string DescribeCoordinate(ResourceCoordinate coordinate) =>
        $"{coordinate.Realm}/{ResourceDefinitionCatalog.GetOwnerKindToken(coordinate.OwnerKind)}/" +
        $"{coordinate.ResourceOwnerId}/{coordinate.ResourceKey}";

    private static ResourceHistoryStateResult Failure(
        string code,
        string expected,
        string actual,
        bool isMissing = false)
    {
        var issues = new List<ValidationIssue>();
        Add(
            issues,
            ResourceMaterializationContract.HistoryPath,
            code,
            expected,
            actual);
        return new ResourceHistoryStateResult(null, issues, isMissing);
    }

    private static void Add(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        ResourceMaterializationContract.AddIssue(
            issues,
            path,
            code,
            expected,
            actual);

    private static FrozenSet<string> Set(params string[] values) =>
        values.ToFrozenSet(StringComparer.Ordinal);

    private sealed record ResourceReplayKey(
        string EventRef,
        string OriginKind,
        string OriginId,
        ResourceCoordinate Coordinate,
        ResourceTransitionOperation Operation);

    private sealed record ResourceExecutionSlot(int Turn, int Sequence);

    private sealed record ConfusableHistoryCoordinate(
        string Realm,
        ResourceOwnerKind OwnerKind,
        string ResourceOwnerId,
        string ResourceKey);
}

internal sealed class ResourceTransitionComparer : IComparer<ResourceTransition>
{
    internal static ResourceTransitionComparer Instance { get; } = new();

    public int Compare(ResourceTransition? x, ResourceTransition? y)
    {
        if (ReferenceEquals(x, y))
            return 0;
        if (x == null)
            return -1;
        if (y == null)
            return 1;

        var result = x.Turn.CompareTo(y.Turn);
        if (result != 0)
            return result;
        result = x.ExecutionSequence.CompareTo(y.ExecutionSequence);
        if (result != 0)
            return result;
        result = x.Phase.CompareTo(y.Phase);
        if (result != 0)
            return result;
        result = x.Priority.CompareTo(y.Priority);
        if (result != 0)
            return result;
        result = string.Compare(x.OriginId, y.OriginId, StringComparison.Ordinal);
        if (result != 0)
            return result;
        result = string.Compare(x.OperationId, y.OperationId, StringComparison.Ordinal);
        if (result != 0)
            return result;
        result = string.Compare(x.EventRef, y.EventRef, StringComparison.Ordinal);
        return result != 0
            ? result
            : string.Compare(x.TransitionId, y.TransitionId, StringComparison.Ordinal);
    }
}
