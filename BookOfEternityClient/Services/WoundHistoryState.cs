using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal enum WoundHistoryReplayDisposition
{
    None,
    Exact,
    Conflict
}

internal sealed record WoundHistoryTransition(
    string TransitionId,
    string WoundId,
    int Ordinal,
    int WoundTransitionOrdinal,
    string Kind,
    int Turn,
    string EventRef,
    string OperationKey,
    string BeforeFingerprint,
    string AfterFingerprint,
    string SourceFingerprint,
    string? AttemptId,
    string ReadableSummary,
    bool Terminal);

internal sealed record WoundHistoryReplayProbe(
    string OperationKey,
    string WoundId,
    string Kind,
    int Turn,
    string EventRef,
    string BeforeFingerprint,
    string AfterFingerprint,
    string SourceFingerprint,
    string? AttemptId,
    string ReadableSummary,
    bool Terminal);

internal sealed record WoundHistoryReplayResult(
    WoundHistoryReplayDisposition Disposition,
    WoundHistoryTransition? Transition,
    IReadOnlyList<ValidationIssue> Issues);

internal sealed record WoundHistoryParseResult(
    WoundHistoryState? State,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => State is not null && Issues.Count == 0;
}

internal sealed class WoundHistoryState
{
    internal const int SchemaVersion = 1;
    internal const int MaxTransitions = 20_000;
    internal const string HistoryPath = "game_state/wounds/wound_history.json";

    private static readonly IReadOnlySet<string> RootFields = Set(
        "schemaVersion", "nextOrdinal", "transitions");
    private static readonly IReadOnlySet<string> TransitionFields = Set(
        "transitionId", "woundId", "ordinal", "woundTransitionOrdinal", "kind", "turn",
        "eventRef", "operationKey", "beforeFingerprint", "afterFingerprint",
        "sourceFingerprint", "attemptId", "readableSummary", "terminal");
    private static readonly IReadOnlySet<string> Kinds = Set(
        "create", "worsen", "complicate", "diagnose", "stabilize", "treat",
        "recover", "heal", "legacy", "archive");
    private static readonly IReadOnlySet<string> PostTerminalKinds = Set("legacy", "archive");
    private static readonly IReadOnlySet<string> TerminalKinds = Set("heal", "archive");

    private readonly ImmutableArray<WoundHistoryTransition> _transitions;
    private readonly IReadOnlyDictionary<string, WoundHistoryTransition> _byTransitionId;
    private readonly IReadOnlyDictionary<string, WoundHistoryTransition> _byOperationKey;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<WoundHistoryTransition>>
        _byWoundId;

    private WoundHistoryState(
        int nextOrdinal,
        IEnumerable<WoundHistoryTransition> transitions)
    {
        NextOrdinal = nextOrdinal;
        _transitions = transitions.ToImmutableArray();
        _byTransitionId = _transitions.ToDictionary(
            static transition => transition.TransitionId,
            StringComparer.Ordinal);
        _byOperationKey = _transitions.ToDictionary(
            static transition => transition.OperationKey,
            StringComparer.Ordinal);
        _byWoundId = _transitions
            .GroupBy(static transition => transition.WoundId, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<WoundHistoryTransition>)group.ToImmutableArray(),
                StringComparer.Ordinal);
    }

    internal int NextOrdinal { get; }

    internal IReadOnlyList<WoundHistoryTransition> Transitions => _transitions;

    internal bool TryResolveExactTransition(
        string transitionId,
        out WoundHistoryTransition? transition) =>
        _byTransitionId.TryGetValue(transitionId, out transition);

    internal bool TryResolveExactOperation(
        string operationKey,
        out WoundHistoryTransition? transition) =>
        _byOperationKey.TryGetValue(operationKey, out transition);

    internal static WoundHistoryParseResult Parse(string? json, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return InvalidResult(
                path,
                "wound_history_invalid_root",
                "non-empty strict JSON object",
                json is null ? "missing" : "empty or whitespace-only input");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            return InvalidResult(
                path,
                "wound_history_invalid_json",
                "well-formed strict JSON object",
                exception.GetType().Name);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return InvalidResult(
                    path,
                    "wound_history_invalid_root",
                    "strict JSON object",
                    root.ValueKind.ToString());
            }

            var issues = new List<ValidationIssue>();
            FindDuplicateProperties(root, path, issues);
            ValidateClosedObject(root, path, RootFields, issues);
            RequireFields(root, path, RootFields, issues);
            ReadSchemaVersion(root, path, issues);
            var nextOrdinal = ReadInt32(
                root,
                "nextOrdinal",
                path,
                1,
                MaxTransitions + 1,
                issues);

            if (!root.TryGetProperty("transitions", out var transitionsElement) ||
                transitionsElement.ValueKind != JsonValueKind.Array)
            {
                AddIssue(
                    issues,
                    path + ".transitions",
                    "wound_history_invalid_field",
                    "array",
                    Describe(root, "transitions"));
                return new WoundHistoryParseResult(null, issues.ToImmutableArray());
            }

            if (transitionsElement.GetArrayLength() > MaxTransitions)
            {
                AddIssue(
                    issues,
                    path + ".transitions",
                    "wound_history_limit_exceeded",
                    $"at most {MaxTransitions} immutable history rows",
                    transitionsElement.GetArrayLength().ToString(CultureInfo.InvariantCulture));
                return new WoundHistoryParseResult(null, issues.ToImmutableArray());
            }

            var transitions = ImmutableArray.CreateBuilder<WoundHistoryTransition>();
            var index = 0;
            foreach (var element in transitionsElement.EnumerateArray())
            {
                var transition = ParseTransition(
                    element,
                    $"{path}.transitions[{index++}]",
                    issues);
                if (transition is not null)
                    transitions.Add(transition);
            }

            ValidateState(nextOrdinal, transitions, path, issues);
            return issues.Count == 0
                ? new WoundHistoryParseResult(
                    new WoundHistoryState(nextOrdinal, transitions),
                    ImmutableArray<ValidationIssue>.Empty)
                : new WoundHistoryParseResult(null, issues.ToImmutableArray());
        }
    }

    internal static WoundHistoryParseResult CreateValidated(
        int nextOrdinal,
        IEnumerable<WoundHistoryTransition> transitions)
    {
        ArgumentNullException.ThrowIfNull(transitions);
        var candidates = transitions.ToArray();
        var issues = new List<ValidationIssue>();
        if (candidates.Length > MaxTransitions)
        {
            AddIssue(
                issues,
                HistoryPath + ".transitions",
                "wound_history_limit_exceeded",
                $"at most {MaxTransitions} immutable history rows",
                candidates.Length.ToString(CultureInfo.InvariantCulture));
            return new WoundHistoryParseResult(null, issues.ToImmutableArray());
        }

        for (var index = 0; index < candidates.Length; index++)
        {
            if (candidates[index] is null)
            {
                AddIssue(
                    issues,
                    $"{HistoryPath}.transitions[{index}]",
                    "wound_history_invalid_transition",
                    "immutable typed transition",
                    "null");
            }
        }

        if (issues.Count == 0)
            ValidateState(nextOrdinal, candidates, HistoryPath, issues);
        return issues.Count == 0
            ? new WoundHistoryParseResult(
                new WoundHistoryState(nextOrdinal, candidates),
                ImmutableArray<ValidationIssue>.Empty)
            : new WoundHistoryParseResult(null, issues.ToImmutableArray());
    }

    internal static string ComputeNonexistentBeforeFingerprint(string woundId)
    {
        ArgumentNullException.ThrowIfNull(woundId);
        var bytes = Encoding.UTF8.GetBytes("wound-history-nonexistent-v1\0" + woundId);
        return "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    internal static WoundHistoryReplayProbe CreateReplayProbe(
        WoundHistoryTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        return new WoundHistoryReplayProbe(
            transition.OperationKey,
            transition.WoundId,
            transition.Kind,
            transition.Turn,
            transition.EventRef,
            transition.BeforeFingerprint,
            transition.AfterFingerprint,
            transition.SourceFingerprint,
            transition.AttemptId,
            transition.ReadableSummary,
            transition.Terminal);
    }

    internal static bool ReplaySemanticsMatch(
        WoundHistoryTransition transition,
        WoundHistoryReplayProbe probe)
    {
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(probe);
        return string.Equals(transition.WoundId, probe.WoundId, StringComparison.Ordinal) &&
               string.Equals(transition.Kind, probe.Kind, StringComparison.Ordinal) &&
               transition.Turn == probe.Turn &&
               string.Equals(transition.EventRef, probe.EventRef, StringComparison.Ordinal) &&
               string.Equals(
                   transition.BeforeFingerprint,
                   probe.BeforeFingerprint,
                   StringComparison.Ordinal) &&
               string.Equals(
                   transition.AfterFingerprint,
                   probe.AfterFingerprint,
                   StringComparison.Ordinal) &&
               string.Equals(
                   transition.SourceFingerprint,
                   probe.SourceFingerprint,
                   StringComparison.Ordinal) &&
               string.Equals(transition.AttemptId, probe.AttemptId, StringComparison.Ordinal) &&
               string.Equals(
                   transition.ReadableSummary,
                   probe.ReadableSummary,
                   StringComparison.Ordinal) &&
               transition.Terminal == probe.Terminal;
    }

    internal WoundHistoryReplayResult ResolveReplay(WoundHistoryReplayProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        if (!_byOperationKey.TryGetValue(probe.OperationKey, out var transition))
        {
            return new WoundHistoryReplayResult(
                WoundHistoryReplayDisposition.None,
                null,
                ImmutableArray<ValidationIssue>.Empty);
        }

        if (ReplaySemanticsMatch(transition, probe))
        {
            return new WoundHistoryReplayResult(
                WoundHistoryReplayDisposition.Exact,
                transition,
                ImmutableArray<ValidationIssue>.Empty);
        }

        var issues = ImmutableArray.CreateBuilder<ValidationIssue>();
        AddIssue(
            issues,
            HistoryPath + ".transitions",
            "wound_history_conflicting_replay",
            "exact prior semantics for the same ordinal operation key",
            probe.OperationKey);
        return new WoundHistoryReplayResult(
            WoundHistoryReplayDisposition.Conflict,
            transition,
            issues.ToImmutable());
    }

    internal IReadOnlyList<ValidationIssue> ValidateAgreement(
        WoundIdentityState identities,
        WoundCarrierCatalog carriers)
    {
        ArgumentNullException.ThrowIfNull(identities);
        ArgumentNullException.ThrowIfNull(carriers);
        var issues = new List<ValidationIssue>();
        if (carriers.Issues.Count != 0)
        {
            AddIssue(
                issues,
                HistoryPath,
                "wound_history_carrier_catalog_invalid",
                "valid exact carrier catalog before history agreement",
                carriers.Issues.Count.ToString(CultureInfo.InvariantCulture));
        }

        var identitiesById = identities.Entries.ToDictionary(
            static entry => entry.WoundId,
            StringComparer.Ordinal);
        foreach (var entry in identities.Entries)
        {
            var path = HistoryPath + ".transitions";
            if (!_byWoundId.TryGetValue(entry.WoundId, out var history) ||
                history.Count == 0)
            {
                AddIssue(
                    issues,
                    path,
                    "wound_history_missing_for_identity",
                    "one contiguous immutable history chain for every wound identity",
                    entry.WoundId);
                continue;
            }

            var first = history[0];
            var latest = history[^1];
            if (entry.CreatedAtTurn != first.Turn ||
                !string.Equals(entry.CreatedEventRef, first.EventRef, StringComparison.Ordinal))
            {
                AddIssue(
                    issues,
                    path,
                    "wound_history_creation_evidence_mismatch",
                    "identity creation chronology equals first history row",
                    entry.WoundId);
            }
            if (entry.LastTransitionOrdinal != latest.WoundTransitionOrdinal)
            {
                AddIssue(
                    issues,
                    path,
                    "wound_history_latest_ordinal_mismatch",
                    latest.WoundTransitionOrdinal.ToString(CultureInfo.InvariantCulture),
                    entry.LastTransitionOrdinal.ToString(CultureInfo.InvariantCulture));
            }
            if (!string.Equals(
                    entry.SemanticFingerprint,
                    latest.AfterFingerprint,
                    StringComparison.Ordinal))
            {
                AddIssue(
                    issues,
                    path,
                    "wound_history_latest_fingerprint_mismatch",
                    latest.AfterFingerprint,
                    entry.SemanticFingerprint);
            }

            var terminalRows = history.Where(static row => row.Terminal).ToArray();
            var occurrences = carriers.Occurrences
                .Where(occurrence => string.Equals(
                    occurrence.WoundId,
                    entry.WoundId,
                    StringComparison.Ordinal))
                .ToArray();
            if (string.Equals(entry.Status, "active", StringComparison.Ordinal))
            {
                ValidateActiveAgreement(
                    entry,
                    latest,
                    terminalRows,
                    occurrences,
                    carriers,
                    issues);
            }
            else
            {
                ValidateHealedAgreement(entry, terminalRows, occurrences, issues);
            }
        }

        foreach (var woundId in _byWoundId.Keys)
        {
            if (!identitiesById.ContainsKey(woundId))
            {
                AddIssue(
                    issues,
                    HistoryPath + ".transitions",
                    "wound_history_identity_missing",
                    "one retained identity entry for every history chain",
                    woundId);
            }
        }
        foreach (var occurrence in carriers.Occurrences)
        {
            if (!identitiesById.ContainsKey(occurrence.WoundId))
            {
                AddIssue(
                    issues,
                    occurrence.JsonPath,
                    "wound_history_identity_missing",
                    "one active identity and history chain for every carrier occurrence",
                    occurrence.WoundId);
            }
        }

        return issues.ToImmutableArray();
    }

    internal static string SerializeCanonical(WoundHistoryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(
                   buffer,
                   new JsonWriterOptions
                   {
                       Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                       Indented = false
                   }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", SchemaVersion);
            writer.WriteNumber("nextOrdinal", state.NextOrdinal);
            writer.WritePropertyName("transitions");
            writer.WriteStartArray();
            foreach (var transition in state._transitions)
                WriteTransition(writer, transition);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static WoundHistoryTransition? ParseTransition(
        JsonElement element,
        string path,
        List<ValidationIssue> issues)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            AddIssue(
                issues,
                path,
                "wound_history_invalid_transition",
                "strict immutable transition object",
                element.ValueKind.ToString());
            return null;
        }

        var issueCount = issues.Count;
        ValidateClosedObject(element, path, TransitionFields, issues);
        RequireFields(element, path, TransitionFields, issues);
        var transitionId = ReadExactIdentifier(element, "transitionId", path, issues);
        var woundId = ReadExactIdentifier(element, "woundId", path, issues);
        var ordinal = ReadInt32(element, "ordinal", path, 1, MaxTransitions, issues);
        var woundOrdinal = ReadInt32(
            element,
            "woundTransitionOrdinal",
            path,
            1,
            MaxTransitions,
            issues);
        var kind = ReadKind(element, path, issues);
        var turn = ReadInt32(element, "turn", path, 0, int.MaxValue, issues);
        var eventRef = ReadExactIdentifier(element, "eventRef", path, issues);
        var operationKey = ReadExactIdentifier(element, "operationKey", path, issues);
        var beforeFingerprint = ReadFingerprint(
            element,
            "beforeFingerprint",
            path,
            issues);
        var afterFingerprint = ReadFingerprint(
            element,
            "afterFingerprint",
            path,
            issues);
        var sourceFingerprint = ReadFingerprint(
            element,
            "sourceFingerprint",
            path,
            issues);
        var attemptId = ReadNullableExactIdentifier(element, "attemptId", path, issues);
        var summary = ReadSummary(element, path, issues);
        var terminal = ReadBoolean(element, "terminal", path, issues);

        return issues.Count == issueCount
            ? new WoundHistoryTransition(
                transitionId,
                woundId,
                ordinal,
                woundOrdinal,
                kind,
                turn,
                eventRef,
                operationKey,
                beforeFingerprint,
                afterFingerprint,
                sourceFingerprint,
                attemptId,
                summary,
                terminal)
            : null;
    }

    private static void ValidateState(
        int nextOrdinal,
        IReadOnlyList<WoundHistoryTransition> transitions,
        string path,
        ICollection<ValidationIssue> issues)
    {
        if (transitions.Count > MaxTransitions)
        {
            AddIssue(
                issues,
                path + ".transitions",
                "wound_history_limit_exceeded",
                $"at most {MaxTransitions} immutable history rows",
                transitions.Count.ToString(CultureInfo.InvariantCulture));
            return;
        }
        if (nextOrdinal < 1 || nextOrdinal > MaxTransitions + 1)
        {
            AddIssue(
                issues,
                path + ".nextOrdinal",
                "wound_history_invalid_field",
                $"exact integer 1..{MaxTransitions + 1}",
                nextOrdinal.ToString(CultureInfo.InvariantCulture));
        }
        if (nextOrdinal != transitions.Count + 1)
        {
            AddIssue(
                issues,
                path + ".nextOrdinal",
                "wound_history_ordinal_discontinuity",
                (transitions.Count + 1).ToString(CultureInfo.InvariantCulture),
                nextOrdinal.ToString(CultureInfo.InvariantCulture));
        }

        var exactTransitionIds = new HashSet<string>(StringComparer.Ordinal);
        var confusableTransitionIds = new HashSet<string>(StringComparer.Ordinal);
        var exactOperationKeys = new HashSet<string>(StringComparer.Ordinal);
        var confusableOperationKeys = new HashSet<string>(StringComparer.Ordinal);
        var confusableWoundIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var byWound = new Dictionary<string, List<WoundHistoryTransition>>(StringComparer.Ordinal);

        for (var index = 0; index < transitions.Count; index++)
        {
            var transition = transitions[index];
            var rowPath = $"{path}.transitions[{index}]";
            ValidateTypedTransition(transition, rowPath, issues);
            if (transition.Ordinal != index + 1)
            {
                AddIssue(
                    issues,
                    rowPath + ".ordinal",
                    "wound_history_ordinal_discontinuity",
                    (index + 1).ToString(CultureInfo.InvariantCulture),
                    transition.Ordinal.ToString(CultureInfo.InvariantCulture));
            }
            if (index > 0 && transition.Turn < transitions[index - 1].Turn)
            {
                AddIssue(
                    issues,
                    rowPath + ".turn",
                    "wound_history_turn_regression",
                    $"turn at least {transitions[index - 1].Turn}",
                    transition.Turn.ToString(CultureInfo.InvariantCulture));
            }

            RegisterUniqueIdentifier(
                transition.TransitionId,
                rowPath + ".transitionId",
                "wound_history_duplicate_transition_id",
                "wound_history_confusable_transition_id",
                exactTransitionIds,
                confusableTransitionIds,
                issues);
            RegisterUniqueIdentifier(
                transition.OperationKey,
                rowPath + ".operationKey",
                "wound_history_duplicate_operation_key",
                "wound_history_confusable_operation_key",
                exactOperationKeys,
                confusableOperationKeys,
                issues);

            if (ResourceMaterializationContract.IsExactIdentifier(transition.WoundId))
            {
                var confusable = MortalLocationIdentityState.BuildConfusableKey(
                    transition.WoundId);
                if (confusableWoundIds.TryGetValue(confusable, out var priorWoundId) &&
                    !string.Equals(priorWoundId, transition.WoundId, StringComparison.Ordinal))
                {
                    AddIssue(
                        issues,
                        rowPath + ".woundId",
                        "wound_history_confusable_wound_id",
                        "one exact identity for each confusable wound key",
                        transition.WoundId);
                }
                else
                {
                    confusableWoundIds[confusable] = transition.WoundId;
                }

                if (!byWound.TryGetValue(transition.WoundId, out var woundRows))
                {
                    woundRows = new List<WoundHistoryTransition>();
                    byWound.Add(transition.WoundId, woundRows);
                }
                woundRows.Add(transition);
            }
        }

        foreach (var pair in byWound)
            ValidateWoundChain(pair.Key, pair.Value, path, issues);
    }

    private static void ValidateTypedTransition(
        WoundHistoryTransition transition,
        string path,
        ICollection<ValidationIssue> issues)
    {
        ValidateTypedIdentifier(transition.TransitionId, path + ".transitionId", issues);
        ValidateTypedIdentifier(transition.WoundId, path + ".woundId", issues);
        ValidateTypedIdentifier(transition.EventRef, path + ".eventRef", issues);
        ValidateTypedIdentifier(transition.OperationKey, path + ".operationKey", issues);
        if (transition.AttemptId is not null)
            ValidateTypedIdentifier(transition.AttemptId, path + ".attemptId", issues);
        if (transition.Ordinal < 1 || transition.Ordinal > MaxTransitions)
        {
            AddIssue(
                issues,
                path + ".ordinal",
                "wound_history_invalid_field",
                $"exact integer 1..{MaxTransitions}",
                transition.Ordinal.ToString(CultureInfo.InvariantCulture));
        }
        if (transition.WoundTransitionOrdinal < 1 ||
            transition.WoundTransitionOrdinal > MaxTransitions)
        {
            AddIssue(
                issues,
                path + ".woundTransitionOrdinal",
                "wound_history_invalid_field",
                $"exact integer 1..{MaxTransitions}",
                transition.WoundTransitionOrdinal.ToString(CultureInfo.InvariantCulture));
        }
        if (transition.Turn < 0)
        {
            AddIssue(
                issues,
                path + ".turn",
                "wound_history_invalid_field",
                "non-negative integer turn",
                transition.Turn.ToString(CultureInfo.InvariantCulture));
        }
        if (!Kinds.Contains(transition.Kind))
        {
            AddIssue(
                issues,
                path + ".kind",
                "wound_history_invalid_kind",
                "one exact registered version-1 transition kind",
                transition.Kind ?? "null");
        }
        ValidateTypedFingerprint(transition.BeforeFingerprint, path + ".beforeFingerprint", issues);
        ValidateTypedFingerprint(transition.AfterFingerprint, path + ".afterFingerprint", issues);
        ValidateTypedFingerprint(transition.SourceFingerprint, path + ".sourceFingerprint", issues);
        if (!IsReadableSummary(transition.ReadableSummary))
        {
            AddIssue(
                issues,
                path + ".readableSummary",
                "wound_history_invalid_summary",
                $"trimmed non-empty readable text up to {WoundMaterializationContract.MaxReadableTextLength} characters",
                transition.ReadableSummary ?? "null");
        }
    }

    private static void ValidateWoundChain(
        string woundId,
        IReadOnlyList<WoundHistoryTransition> transitions,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var terminalSeen = false;
        string? priorAfter = null;
        for (var index = 0; index < transitions.Count; index++)
        {
            var transition = transitions[index];
            var expectedWoundOrdinal = index + 1;
            if (transition.WoundTransitionOrdinal != expectedWoundOrdinal)
            {
                AddIssue(
                    issues,
                    path + ".transitions",
                    "wound_history_wound_ordinal_discontinuity",
                    $"wound {woundId} transition ordinal {expectedWoundOrdinal}",
                    transition.WoundTransitionOrdinal.ToString(CultureInfo.InvariantCulture));
            }

            if (index == 0)
            {
                if (!string.Equals(transition.Kind, "create", StringComparison.Ordinal))
                {
                    AddIssue(
                        issues,
                        path + ".transitions",
                        "wound_history_first_transition_invalid",
                        "first transition is create from sealed nonexistence",
                        transition.Kind);
                }
                var nonexistent = ComputeNonexistentBeforeFingerprint(woundId);
                if (!string.Equals(
                        transition.BeforeFingerprint,
                        nonexistent,
                        StringComparison.Ordinal))
                {
                    AddIssue(
                        issues,
                        path + ".transitions",
                        "wound_history_nonexistent_before_mismatch",
                        nonexistent,
                        transition.BeforeFingerprint);
                }
            }
            else if (!string.Equals(
                         transition.BeforeFingerprint,
                         priorAfter,
                         StringComparison.Ordinal))
            {
                AddIssue(
                    issues,
                    path + ".transitions",
                    "wound_history_chain_mismatch",
                    priorAfter ?? "missing prior after fingerprint",
                    transition.BeforeFingerprint);
            }

            if (terminalSeen)
            {
                if (!PostTerminalKinds.Contains(transition.Kind))
                {
                    AddIssue(
                        issues,
                        path + ".transitions",
                        "wound_history_transition_after_terminal",
                        "only independent legacy/archive audit after terminal evidence",
                        transition.Kind);
                }
                if (!string.Equals(
                        transition.AfterFingerprint,
                        transition.BeforeFingerprint,
                        StringComparison.Ordinal))
                {
                    AddIssue(
                        issues,
                        path + ".transitions",
                        "wound_history_terminal_state_changed",
                        "post-terminal audit preserves the sealed wound fingerprint",
                        transition.TransitionId);
                }
                if (transition.Terminal)
                {
                    AddIssue(
                        issues,
                        path + ".transitions",
                        "wound_history_multiple_terminal_rows",
                        "exactly one terminal wound row",
                        transition.TransitionId);
                }
            }
            else
            {
                if (index > 0 && string.Equals(transition.Kind, "create", StringComparison.Ordinal))
                {
                    AddIssue(
                        issues,
                        path + ".transitions",
                        "wound_history_reopen_forbidden",
                        "create only once at wound transition ordinal 1",
                        transition.TransitionId);
                }
                if (string.Equals(transition.Kind, "legacy", StringComparison.Ordinal))
                {
                    AddIssue(
                        issues,
                        path + ".transitions",
                        "wound_history_legacy_before_terminal",
                        "legacy audit only after terminal wound evidence",
                        transition.TransitionId);
                }
                if (transition.Terminal && !TerminalKinds.Contains(transition.Kind))
                {
                    AddIssue(
                        issues,
                        path + ".transitions",
                        "wound_history_invalid_terminal_kind",
                        "terminal=true only for heal or terminal archive",
                        transition.Kind);
                }
                if (!transition.Terminal &&
                    (string.Equals(transition.Kind, "heal", StringComparison.Ordinal) ||
                     string.Equals(transition.Kind, "archive", StringComparison.Ordinal)))
                {
                    AddIssue(
                        issues,
                        path + ".transitions",
                        "wound_history_missing_terminal_flag",
                        "heal/terminal archive seals terminal=true",
                        transition.Kind);
                }
            }

            if (transition.Terminal)
                terminalSeen = true;
            priorAfter = transition.AfterFingerprint;
        }
    }

    private static void ValidateActiveAgreement(
        WoundIdentityEntry entry,
        WoundHistoryTransition latest,
        IReadOnlyCollection<WoundHistoryTransition> terminalRows,
        IReadOnlyList<WoundCarrierOccurrence> occurrences,
        WoundCarrierCatalog carriers,
        ICollection<ValidationIssue> issues)
    {
        if (terminalRows.Count != 0)
        {
            AddIssue(
                issues,
                HistoryPath + ".transitions",
                "wound_history_terminal_evidence_mismatch",
                "no terminal history row for active identity",
                entry.WoundId);
        }
        if (occurrences.Count != 1 || !carriers.TryResolveOne(entry.WoundId, out var occurrence))
        {
            AddIssue(
                issues,
                HistoryPath + ".transitions",
                "wound_history_active_carrier_mismatch",
                "exactly one resolvable active carrier occurrence",
                $"{entry.WoundId}:{occurrences.Count}");
            return;
        }

        foreach (var issue in WoundIdentityState.ValidateActiveAgreement(
                     entry,
                     occurrence.Wound,
                     occurrence.JsonPath))
        {
            issues.Add(issue);
        }

        var carrierFingerprint = WoundIdentityState.ComputeSemanticFingerprint(occurrence.Wound);
        if (!string.Equals(carrierFingerprint, latest.AfterFingerprint, StringComparison.Ordinal))
        {
            AddIssue(
                issues,
                occurrence.JsonPath,
                "wound_history_active_fingerprint_mismatch",
                latest.AfterFingerprint,
                carrierFingerprint);
        }
        if (!string.Equals(
                occurrence.Wound.LastTransition.TransitionId,
                latest.TransitionId,
                StringComparison.Ordinal) ||
            occurrence.Wound.LastTransition.Ordinal != latest.WoundTransitionOrdinal ||
            occurrence.Wound.LastTransition.Turn != latest.Turn ||
            !string.Equals(
                occurrence.Wound.LastTransition.Kind,
                latest.Kind,
                StringComparison.Ordinal))
        {
            AddIssue(
                issues,
                occurrence.JsonPath + ".lastTransition",
                "wound_history_active_transition_mismatch",
                "carrier lastTransition equals latest immutable history row",
                latest.TransitionId);
        }
    }

    private static void ValidateHealedAgreement(
        WoundIdentityEntry entry,
        IReadOnlyList<WoundHistoryTransition> terminalRows,
        IReadOnlyList<WoundCarrierOccurrence> occurrences,
        ICollection<ValidationIssue> issues)
    {
        if (occurrences.Count != 0)
        {
            AddIssue(
                issues,
                HistoryPath + ".transitions",
                "wound_history_healed_carrier_present",
                "no active carrier for healed identity",
                $"{entry.WoundId}:{occurrences.Count}");
        }
        if (terminalRows.Count == 0)
        {
            AddIssue(
                issues,
                HistoryPath + ".transitions",
                "wound_history_terminal_evidence_missing",
                "exactly one terminal heal/archive row for healed identity",
                entry.WoundId);
            return;
        }
        if (terminalRows.Count != 1)
        {
            AddIssue(
                issues,
                HistoryPath + ".transitions",
                "wound_history_terminal_evidence_extra",
                "exactly one terminal heal/archive row for healed identity",
                $"{entry.WoundId}:{terminalRows.Count}");
            return;
        }
        if (!string.Equals(
                entry.TerminalTransitionId,
                terminalRows[0].TransitionId,
                StringComparison.Ordinal))
        {
            AddIssue(
                issues,
                HistoryPath + ".transitions",
                "wound_history_terminal_evidence_mismatch",
                terminalRows[0].TransitionId,
                entry.TerminalTransitionId ?? "null");
        }
    }

    private static void RegisterUniqueIdentifier(
        string value,
        string path,
        string duplicateCode,
        string confusableCode,
        HashSet<string> exact,
        HashSet<string> confusable,
        ICollection<ValidationIssue> issues)
    {
        if (!ResourceMaterializationContract.IsExactIdentifier(value))
            return;
        if (!exact.Add(value))
        {
            AddIssue(
                issues,
                path,
                duplicateCode,
                "globally unique exact identifier",
                value);
            return;
        }
        if (!confusable.Add(MortalLocationIdentityState.BuildConfusableKey(value)))
        {
            AddIssue(
                issues,
                path,
                confusableCode,
                "globally unique exact/confusable identifier",
                value);
        }
    }

    private static void ValidateTypedIdentifier(
        string? value,
        string path,
        ICollection<ValidationIssue> issues)
    {
        if (ResourceMaterializationContract.IsExactIdentifier(value))
            return;
        AddIssue(
            issues,
            path,
            "wound_history_invalid_identifier",
            "non-empty trimmed NFKC exact identifier without control or separator characters",
            value ?? "null");
    }

    private static void ValidateTypedFingerprint(
        string? value,
        string path,
        ICollection<ValidationIssue> issues)
    {
        if (ResourceMaterializationContract.IsAuthorityFingerprint(value))
            return;
        AddIssue(
            issues,
            path,
            "wound_history_invalid_fingerprint",
            "exact lowercase sha256: fingerprint with 64 hexadecimal digits",
            value ?? "null");
    }

    private static int ReadSchemaVersion(
        JsonElement root,
        string path,
        ICollection<ValidationIssue> issues)
    {
        if (root.TryGetProperty("schemaVersion", out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            string.Equals(value.GetRawText(), "1", StringComparison.Ordinal))
        {
            return SchemaVersion;
        }
        AddIssue(
            issues,
            path + ".schemaVersion",
            "wound_history_invalid_field",
            "exact integer 1",
            Describe(root, "schemaVersion"));
        return 0;
    }

    private static string ReadExactIdentifier(
        JsonElement root,
        string field,
        string path,
        ICollection<ValidationIssue> issues)
    {
        if (root.TryGetProperty(field, out var value) &&
            value.ValueKind == JsonValueKind.String &&
            ResourceMaterializationContract.IsExactIdentifier(value.GetString()))
        {
            return value.GetString()!;
        }
        AddIssue(
            issues,
            path + "." + field,
            "wound_history_invalid_identifier",
            "non-empty trimmed NFKC exact identifier without control or separator characters",
            Describe(root, field));
        return string.Empty;
    }

    private static string? ReadNullableExactIdentifier(
        JsonElement root,
        string field,
        string path,
        ICollection<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind == JsonValueKind.String &&
            ResourceMaterializationContract.IsExactIdentifier(value.GetString()))
        {
            return value.GetString();
        }
        AddIssue(
            issues,
            path + "." + field,
            "wound_history_invalid_identifier",
            "null or non-empty trimmed NFKC exact identifier without control or separator characters",
            value.GetRawText());
        return null;
    }

    private static string ReadFingerprint(
        JsonElement root,
        string field,
        string path,
        ICollection<ValidationIssue> issues)
    {
        if (root.TryGetProperty(field, out var value) &&
            value.ValueKind == JsonValueKind.String &&
            ResourceMaterializationContract.IsAuthorityFingerprint(value.GetString()))
        {
            return value.GetString()!;
        }
        AddIssue(
            issues,
            path + "." + field,
            "wound_history_invalid_fingerprint",
            "exact lowercase sha256: fingerprint with 64 hexadecimal digits",
            Describe(root, field));
        return string.Empty;
    }

    private static string ReadKind(
        JsonElement root,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var value = root.TryGetProperty("kind", out var candidate) &&
                    candidate.ValueKind == JsonValueKind.String
            ? candidate.GetString() ?? string.Empty
            : string.Empty;
        if (Kinds.Contains(value))
            return value;
        AddIssue(
            issues,
            path + ".kind",
            "wound_history_invalid_kind",
            "one exact registered version-1 transition kind",
            Describe(root, "kind"));
        return string.Empty;
    }

    private static int ReadInt32(
        JsonElement root,
        string field,
        string path,
        int minimum,
        int maximum,
        ICollection<ValidationIssue> issues)
    {
        if (root.TryGetProperty(field, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var result) &&
            value.GetRawText().IndexOfAny(new[] { '.', 'e', 'E' }) < 0 &&
            result >= minimum && result <= maximum)
        {
            return result;
        }
        AddIssue(
            issues,
            path + "." + field,
            "wound_history_invalid_field",
            $"exact integer {minimum}..{maximum}",
            Describe(root, field));
        return 0;
    }

    private static bool ReadBoolean(
        JsonElement root,
        string field,
        string path,
        ICollection<ValidationIssue> issues)
    {
        if (root.TryGetProperty(field, out var value) &&
            value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }
        AddIssue(
            issues,
            path + "." + field,
            "wound_history_invalid_field",
            "boolean",
            Describe(root, field));
        return false;
    }

    private static string ReadSummary(
        JsonElement root,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var summary = root.TryGetProperty("readableSummary", out var value) &&
                      value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
        if (IsReadableSummary(summary))
            return summary!;
        AddIssue(
            issues,
            path + ".readableSummary",
            "wound_history_invalid_summary",
            $"trimmed non-empty readable text up to {WoundMaterializationContract.MaxReadableTextLength} characters",
            Describe(root, "readableSummary"));
        return string.Empty;
    }

    private static bool IsReadableSummary(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= WoundMaterializationContract.MaxReadableTextLength &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
        !value.Any(static character => char.IsControl(character) || char.IsSurrogate(character));

    private static void ValidateClosedObject(
        JsonElement value,
        string path,
        IReadOnlySet<string> fields,
        ICollection<ValidationIssue> issues)
    {
        if (value.ValueKind != JsonValueKind.Object)
            return;
        foreach (var property in value.EnumerateObject())
        {
            if (!fields.Contains(property.Name))
            {
                AddIssue(
                    issues,
                    path + "." + property.Name,
                    "wound_history_unknown_field",
                    "registered current-schema history field",
                    property.Name);
            }
        }
    }

    private static void RequireFields(
        JsonElement value,
        string path,
        IReadOnlySet<string> fields,
        ICollection<ValidationIssue> issues)
    {
        if (value.ValueKind != JsonValueKind.Object)
            return;
        foreach (var field in fields)
        {
            if (!value.TryGetProperty(field, out _))
            {
                AddIssue(
                    issues,
                    path + "." + field,
                    "wound_history_missing_field",
                    "required final version-1 history field",
                    "missing");
            }
        }
    }

    private static void FindDuplicateProperties(
        JsonElement value,
        string path,
        ICollection<ValidationIssue> issues)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                var fields = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in value.EnumerateObject())
                {
                    var propertyPath = path + "." + property.Name;
                    if (!fields.Add(property.Name))
                    {
                        AddIssue(
                            issues,
                            propertyPath,
                            "wound_history_duplicate_property",
                            "one occurrence of each exact property",
                            property.Name);
                    }
                    FindDuplicateProperties(property.Value, propertyPath, issues);
                }
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var element in value.EnumerateArray())
                    FindDuplicateProperties(element, $"{path}[{index++}]", issues);
                break;
        }
    }

    private static void WriteTransition(
        Utf8JsonWriter writer,
        WoundHistoryTransition transition)
    {
        writer.WriteStartObject();
        writer.WriteString("transitionId", transition.TransitionId);
        writer.WriteString("woundId", transition.WoundId);
        writer.WriteNumber("ordinal", transition.Ordinal);
        writer.WriteNumber("woundTransitionOrdinal", transition.WoundTransitionOrdinal);
        writer.WriteString("kind", transition.Kind);
        writer.WriteNumber("turn", transition.Turn);
        writer.WriteString("eventRef", transition.EventRef);
        writer.WriteString("operationKey", transition.OperationKey);
        writer.WriteString("beforeFingerprint", transition.BeforeFingerprint);
        writer.WriteString("afterFingerprint", transition.AfterFingerprint);
        writer.WriteString("sourceFingerprint", transition.SourceFingerprint);
        if (transition.AttemptId is null)
            writer.WriteNull("attemptId");
        else
            writer.WriteString("attemptId", transition.AttemptId);
        writer.WriteString("readableSummary", transition.ReadableSummary);
        writer.WriteBoolean("terminal", transition.Terminal);
        writer.WriteEndObject();
    }

    private static WoundHistoryParseResult InvalidResult(
        string path,
        string code,
        string expected,
        string actual)
    {
        var issues = ImmutableArray.CreateBuilder<ValidationIssue>();
        AddIssue(issues, path, code, expected, actual);
        return new WoundHistoryParseResult(null, issues.ToImmutable());
    }

    private static void AddIssue(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Wound history violates immutable client-owned chronology authority.",
            code: code,
            actor: "Client",
            section: "wound_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Restore one closed append-only wound history chain; never infer, rewrite, truncate, reopen, case-fold, or duplicate accepted transition evidence."));

    private static string Describe(JsonElement root, string field) =>
        root.TryGetProperty(field, out var value) ? value.GetRawText() : "missing";

    private static IReadOnlySet<string> Set(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);
}
