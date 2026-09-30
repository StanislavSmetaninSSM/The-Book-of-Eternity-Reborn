using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal class EffectIdentityFactory : CombatantIdentityFactory
{
    /// <summary>
    /// Allocates an ordinary effect identity while allowing a capture adapter to retain its causal key.
    /// </summary>
    /// <param name="key">
    /// Non-null immutable event, semantic role and subject coordinate.
    /// </param>
    /// <returns>
    /// The identity returned by the existing allocation policy.
    /// </returns>
    internal virtual string CreateEffectId(EffectIdentityAllocationKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return CreateEffectId();
    }

    /// <summary>
    /// Allocates an ordinary transition identity while allowing a capture adapter to retain its causal key.
    /// </summary>
    /// <param name="key">
    /// Non-null immutable event, semantic role and subject coordinate.
    /// </param>
    /// <returns>
    /// The identity returned by the existing allocation policy.
    /// </returns>
    internal virtual string CreateTransitionId(EffectIdentityAllocationKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return CreateTransitionId();
    }

    /// <summary>
    /// Allocates an ordinary resolution identity while allowing a capture adapter to retain its causal key.
    /// </summary>
    /// <param name="key">
    /// Non-null immutable event, semantic role and subject coordinate.
    /// </param>
    /// <returns>
    /// The identity returned by the existing allocation policy.
    /// </returns>
    internal virtual string CreateResolutionId(EffectIdentityAllocationKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return CreateResolutionId();
    }

    internal virtual string CreateEffectId() => "effect_" + Guid.NewGuid().ToString("N");

    internal virtual string CreateTransitionId() => "effect_transition_" + Guid.NewGuid().ToString("N");

    internal virtual string CreateResolutionId() => "effect_resolution_" + Guid.NewGuid().ToString("N");
}

internal sealed record EffectIdentityOwner(
    string Kind,
    string OwnerId,
    string CarrierPath,
    string Collection);

internal sealed record EffectStackCoordinate(
    string Realm,
    string TargetKind,
    string TargetId,
    string SourceKind,
    string SourceId,
    string StackKey);

internal sealed record EffectIdentityTransition(
    string TransitionId,
    string Kind,
    int Turn,
    string EventRef,
    IReadOnlyList<string> SourceEffectIds,
    IReadOnlyList<string> ResultEffectIds,
    string? ReceiptId,
    JsonObject Raw);

internal sealed record EffectIdentityEntry(
    string EffectId,
    string State,
    string Realm,
    EffectIdentityOwner Owner,
    JsonObject Target,
    JsonObject Source,
    EffectStackCoordinate StackCoordinate,
    int CreatedAtTurn,
    IReadOnlyList<EffectIdentityTransition> Transitions,
    JsonObject Raw);

internal sealed record EffectIdentitySourceCoordinate(
    string Realm,
    string Kind,
    string SourceId,
    string DefinitionKey);

internal sealed record EffectIdentitySourceGroup(
    string Realm,
    string Kind,
    string SourceId);

internal sealed record EffectIdentityParseResult(
    EffectIdentityState? State,
    IReadOnlyList<ValidationIssue> Issues);

internal sealed class EffectIdentityState
{
    internal const string StatePath = "game_state/effects/effect_identity_index.json";
    internal const int SchemaVersion = 1;

    private static readonly HashSet<string> RootFields = Set("schemaVersion", "entries");
    private static readonly HashSet<string> EntryFields = Set(
        "effectId", "state", "realm", "owner", "target", "source", "stackCoordinate",
        "createdAtTurn", "transitions");
    private static readonly HashSet<string> OwnerFields = Set("kind", "ownerId", "carrierPath", "collection");
    private static readonly HashSet<string> TargetFields = Set("kind", "targetId");
    private static readonly HashSet<string> SourceFields = Set("kind", "sourceId", "definitionKey");
    private static readonly HashSet<string> StackFields = Set(
        "realm", "targetKind", "targetId", "sourceKind", "sourceId", "stackKey");
    private static readonly HashSet<string> TransitionFields = Set(
        "transitionId", "kind", "turn", "eventRef", "sourceEffectIds", "resultEffectIds", "receiptId");
    private static readonly HashSet<string> ActiveStates = Set("active", "suspended");
    private static readonly HashSet<string> TerminalStates = Set("expired", "dispelled", "removed", "replaced");
    private static readonly HashSet<string> AllStates = new(
        ActiveStates.Concat(TerminalStates),
        StringComparer.Ordinal);
    private static readonly HashSet<string> Realms = Set("mortal_world", "chaos_sea", "shining_abode");
    private static readonly HashSet<string> OwnerKinds = Set(
        "player", "npc", "combatant", "guardian", "resident", "radiant_actor",
        "afterlife_actor", "spiritual_conflict_side");
    private static readonly HashSet<string> TransitionKinds = Set(
        "create", "stack", "refresh", "replace", "merge", "trigger", "consume", "suspend",
        "resume", "expire", "dispel", "remove");
    private static readonly HashSet<string> TerminalTransitionKinds = Set("expire", "dispel", "remove", "replace");

    private readonly JsonObject _root;
    private readonly Dictionary<string, EffectIdentityEntry> _entries;
    private readonly Dictionary<
        EffectIdentitySourceCoordinate,
        EffectIdentityEntry[]> _entriesBySourceCoordinate;
    private readonly Dictionary<
        EffectIdentitySourceGroup,
        EffectIdentityEntry[]> _entriesBySourceGroup;
    private readonly Dictionary<string, EffectIdentityEntry[]>
        _firstCreateChildrenByParent;

    private EffectIdentityState(JsonObject root, IEnumerable<EffectIdentityEntry> entries)
    {
        var materialized = entries.ToArray();
        _root = root;
        _entries = materialized.ToDictionary(
            static entry => entry.EffectId,
            StringComparer.Ordinal);
        _entriesBySourceCoordinate = materialized
            .GroupBy(CreateSourceCoordinate)
            .ToDictionary(
                static group => group.Key,
                static group => group
                    .OrderBy(static entry => entry.EffectId, StringComparer.Ordinal)
                    .ToArray());
        _entriesBySourceGroup = materialized
            .GroupBy(CreateSourceGroup)
            .ToDictionary(
                static group => group.Key,
                static group => group
                    .OrderBy(static entry => entry.EffectId, StringComparer.Ordinal)
                    .ToArray());
        _firstCreateChildrenByParent = materialized
            .Where(static entry =>
                entry.Transitions.Count != 0 &&
                string.Equals(
                    entry.Transitions[0].Kind,
                    "create",
                    StringComparison.Ordinal))
            .SelectMany(static entry => entry.Transitions[0].SourceEffectIds
                .Select(parent => (Parent: parent, Child: entry)))
            .GroupBy(static pair => pair.Parent, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group
                    .Select(static pair => pair.Child)
                    .OrderBy(static entry => entry.EffectId, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);
    }

    internal IReadOnlyCollection<EffectIdentityEntry> Entries => _entries.Values;

    internal bool TryGetEntry(string effectId, out EffectIdentityEntry entry) =>
        _entries.TryGetValue(effectId, out entry!);

    internal IReadOnlyList<EffectIdentityEntry> ResolveSourceCoordinate(
        EffectIdentitySourceCoordinate coordinate) =>
        _entriesBySourceCoordinate.TryGetValue(coordinate, out var entries)
            ? Array.AsReadOnly(entries.ToArray())
            : Array.Empty<EffectIdentityEntry>();

    internal IReadOnlyList<EffectIdentityEntry> ResolveSourceGroup(
        EffectIdentitySourceGroup group) =>
        _entriesBySourceGroup.TryGetValue(group, out var entries)
            ? Array.AsReadOnly(entries.ToArray())
            : Array.Empty<EffectIdentityEntry>();

    internal IReadOnlyList<EffectIdentityEntry> ResolveFirstCreateChildren(
        string parentEffectId) =>
        _firstCreateChildrenByParent.TryGetValue(parentEffectId, out var entries)
            ? Array.AsReadOnly(entries.ToArray())
            : Array.Empty<EffectIdentityEntry>();

    internal JsonObject ToJson() => _root.DeepClone().AsObject();

    private static EffectIdentitySourceCoordinate CreateSourceCoordinate(
        EffectIdentityEntry entry) =>
        new(
            entry.Realm,
            entry.Source["kind"]!.GetValue<string>(),
            entry.Source["sourceId"]!.GetValue<string>(),
            entry.Source["definitionKey"]!.GetValue<string>());

    private static EffectIdentitySourceGroup CreateSourceGroup(
        EffectIdentityEntry entry) =>
        new(
            entry.Realm,
            entry.Source["kind"]!.GetValue<string>(),
            entry.Source["sourceId"]!.GetValue<string>());

    internal static EffectIdentityParseResult Parse(JsonElement root, string path)
    {
        var issues = new List<ValidationIssue>();
        if (root.ValueKind != JsonValueKind.Object)
        {
            Add(issues, path, "effect_identity_invalid_root", "closed identity-index object", root.ValueKind.ToString());
            return new EffectIdentityParseResult(null, issues);
        }

        FindDuplicateProperties(root, path, issues);
        ValidateClosedObject(root, path, RootFields, issues);
        RequireFields(root, path, RootFields, issues);
        RequireExactInt(root, path, "schemaVersion", SchemaVersion, issues);
        if (!root.TryGetProperty("entries", out var entriesElement) || entriesElement.ValueKind != JsonValueKind.Array)
        {
            Add(issues, path + ".entries", "effect_identity_invalid_field", "array", Describe(root, "entries"));
            return new EffectIdentityParseResult(null, issues);
        }

        var entries = new List<EffectIdentityEntry>();
        var identities = new IdentityRegistry();
        var transitionIds = new IdentityRegistry();
        var receiptIds = new IdentityRegistry();
        var replayEvents = new IdentityRegistry();
        var index = 0;
        foreach (var entryElement in entriesElement.EnumerateArray())
        {
            var entryPath = $"{path}.entries[{index++}]";
            var entry = ParseEntry(
                entryElement,
                entryPath,
                identities,
                transitionIds,
                receiptIds,
                replayEvents,
                issues);
            if (entry != null)
                entries.Add(entry);
        }

        if (issues.Count > 0)
            return new EffectIdentityParseResult(null, issues);

        var parsedRoot = JsonNode.Parse(root.GetRawText())?.AsObject();
        return parsedRoot == null
            ? new EffectIdentityParseResult(null, new[]
            {
                NewIssue(path, "effect_identity_invalid_root", "parseable identity-index object", root.GetRawText())
            })
            : new EffectIdentityParseResult(new EffectIdentityState(parsedRoot, entries), Array.Empty<ValidationIssue>());
    }

    internal static IReadOnlyList<ValidationIssue> ValidateClientOwnedContinuity(
        JsonElement before,
        JsonElement current,
        string path)
    {
        var issues = new List<ValidationIssue>();
        var beforeNode = JsonNode.Parse(before.GetRawText());
        var currentNode = JsonNode.Parse(current.GetRawText());
        if (!JsonNode.DeepEquals(beforeNode, currentNode))
        {
            Add(
                issues,
                path,
                "effect_identity_direct_mutation",
                "identity index byte-semantically unchanged outside the accepted effect plan",
                current.GetRawText());
        }
        return issues;
    }

    internal IReadOnlyList<ValidationIssue> ValidateTerminalContinuity(
        EffectIdentityState current,
        string path)
    {
        ArgumentNullException.ThrowIfNull(current);
        var issues = new List<ValidationIssue>();
        foreach (var previousEntry in _entries.Values.Where(entry => TerminalStates.Contains(entry.State)))
        {
            if (!current._entries.TryGetValue(previousEntry.EffectId, out var currentEntry) ||
                !JsonNode.DeepEquals(previousEntry.Raw, currentEntry.Raw))
            {
                Add(
                    issues,
                    path + ".entries",
                    "effect_identity_terminal_history_mutated",
                    $"immutable terminal entry for {previousEntry.EffectId}",
                    currentEntry?.Raw.ToJsonString() ?? "missing");
            }
        }
        return issues;
    }

    private static EffectIdentityEntry? ParseEntry(
        JsonElement entry,
        string path,
        IdentityRegistry identities,
        IdentityRegistry transitionIds,
        IdentityRegistry receiptIds,
        IdentityRegistry replayEvents,
        List<ValidationIssue> issues)
    {
        if (entry.ValueKind != JsonValueKind.Object)
        {
            Add(issues, path, "effect_identity_invalid_field", "closed identity entry object", entry.ValueKind.ToString());
            return null;
        }

        var issueCount = issues.Count;
        ValidateClosedObject(entry, path, EntryFields, issues);
        RequireFields(entry, path, EntryFields, issues);
        var effectId = RequireExactIdentifier(entry, path, "effectId", issues);
        if (effectId != null)
            identities.Register(effectId, path + ".effectId", "effect_identity_duplicate_id", "effect_identity_confusable_id", issues);
        var state = RequireClosedString(entry, path, "state", AllStates, issues);
        var realm = RequireClosedString(entry, path, "realm", Realms, issues);
        var owner = ParseOwner(entry, path, issues);
        var target = ParseClosedObject(entry, path, "target", TargetFields, issues);
        var source = ParseClosedObject(entry, path, "source", SourceFields, issues);
        if (target != null)
        {
            RequireClosedString(target.Value, path + ".target", "kind", OwnerKinds, issues);
            RequireExactIdentifier(target.Value, path + ".target", "targetId", issues);
        }
        if (source != null)
        {
            RequireExactIdentifier(source.Value, path + ".source", "kind", issues);
            RequireExactIdentifier(source.Value, path + ".source", "sourceId", issues);
            RequireExactIdentifier(source.Value, path + ".source", "definitionKey", issues);
        }
        var stack = ParseStack(entry, path, issues);
        var createdAtTurn = RequirePositiveInt(entry, path, "createdAtTurn", issues);
        var transitions = ParseTransitions(
            entry,
            path,
            transitionIds,
            receiptIds,
            replayEvents,
            issues);

        if (effectId != null && state != null && transitions.Count > 0)
            ValidateStateTransitionAgreement(effectId, state, transitions[^1], path, issues);
        if (createdAtTurn.HasValue && transitions.Count > 0 && transitions[0].Turn < createdAtTurn.Value)
            Add(issues, path + ".transitions[0].turn", "effect_identity_invalid_transition", "turn >= createdAtTurn", transitions[0].Turn.ToString());

        if (issues.Count != issueCount || effectId == null || state == null || realm == null ||
            owner == null || target == null || source == null || stack == null || !createdAtTurn.HasValue)
        {
            return null;
        }

        return new EffectIdentityEntry(
            effectId,
            state,
            realm,
            owner,
            JsonNode.Parse(target.Value.GetRawText())!.AsObject(),
            JsonNode.Parse(source.Value.GetRawText())!.AsObject(),
            stack,
            createdAtTurn.Value,
            transitions,
            JsonNode.Parse(entry.GetRawText())!.AsObject());
    }

    private static EffectIdentityOwner? ParseOwner(
        JsonElement entry,
        string path,
        List<ValidationIssue> issues)
    {
        var owner = ParseClosedObject(entry, path, "owner", OwnerFields, issues);
        if (owner == null)
            return null;
        var ownerPath = path + ".owner";
        var kind = RequireClosedString(owner.Value, ownerPath, "kind", OwnerKinds, issues);
        var ownerId = RequireExactIdentifier(owner.Value, ownerPath, "ownerId", issues);
        var carrierPath = RequireExactIdentifier(owner.Value, ownerPath, "carrierPath", issues);
        var collection = RequireExactIdentifier(owner.Value, ownerPath, "collection", issues);
        return kind == null || ownerId == null || carrierPath == null || collection == null
            ? null
            : new EffectIdentityOwner(kind, ownerId, carrierPath, collection);
    }

    private static EffectStackCoordinate? ParseStack(
        JsonElement entry,
        string path,
        List<ValidationIssue> issues)
    {
        var stack = ParseClosedObject(entry, path, "stackCoordinate", StackFields, issues);
        if (stack == null)
            return null;
        var stackPath = path + ".stackCoordinate";
        var realm = RequireClosedString(stack.Value, stackPath, "realm", Realms, issues);
        var targetKind = RequireClosedString(stack.Value, stackPath, "targetKind", OwnerKinds, issues);
        var targetId = RequireExactIdentifier(stack.Value, stackPath, "targetId", issues);
        var sourceKind = RequireExactIdentifier(stack.Value, stackPath, "sourceKind", issues);
        var sourceId = RequireExactIdentifier(stack.Value, stackPath, "sourceId", issues);
        var stackKey = RequireExactIdentifier(stack.Value, stackPath, "stackKey", issues);
        return realm == null || targetKind == null || targetId == null || sourceKind == null || sourceId == null || stackKey == null
            ? null
            : new EffectStackCoordinate(realm, targetKind, targetId, sourceKind, sourceId, stackKey);
    }

    private static List<EffectIdentityTransition> ParseTransitions(
        JsonElement entry,
        string path,
        IdentityRegistry transitionIds,
        IdentityRegistry receiptIds,
        IdentityRegistry replayEvents,
        List<ValidationIssue> issues)
    {
        var result = new List<EffectIdentityTransition>();
        if (!entry.TryGetProperty("transitions", out var transitions) ||
            transitions.ValueKind != JsonValueKind.Array ||
            transitions.GetArrayLength() == 0)
        {
            Add(issues, path + ".transitions", "effect_identity_invalid_transition", "non-empty immutable transition array", Describe(entry, "transitions"));
            return result;
        }

        var index = 0;
        var previousTurn = 0;
        foreach (var transition in transitions.EnumerateArray())
        {
            var transitionPath = $"{path}.transitions[{index++}]";
            if (transition.ValueKind != JsonValueKind.Object)
            {
                Add(issues, transitionPath, "effect_identity_invalid_transition", "closed transition object", transition.ValueKind.ToString());
                continue;
            }
            var issueCount = issues.Count;
            ValidateClosedObject(transition, transitionPath, TransitionFields, issues);
            RequireFields(transition, transitionPath, TransitionFields, issues);
            var transitionId = RequireExactIdentifier(transition, transitionPath, "transitionId", issues);
            if (transitionId != null)
                transitionIds.Register(transitionId, transitionPath + ".transitionId", "effect_identity_duplicate_transition", "effect_identity_confusable_transition", issues);
            var kind = RequireClosedString(transition, transitionPath, "kind", TransitionKinds, issues);
            var turn = RequirePositiveInt(transition, transitionPath, "turn", issues);
            var eventRef = RequireExactIdentifier(transition, transitionPath, "eventRef", issues);
            if (eventRef != null)
                replayEvents.Register(eventRef, transitionPath + ".eventRef", "effect_identity_replay_conflict", "effect_identity_replay_conflict", issues);
            var sourceIds = ParseIdentityArray(transition, transitionPath, "sourceEffectIds", issues);
            var resultIds = ParseIdentityArray(transition, transitionPath, "resultEffectIds", issues);
            var receiptId = ParseNullableIdentifier(transition, transitionPath, "receiptId", issues);
            if (receiptId != null)
                receiptIds.Register(receiptId, transitionPath + ".receiptId", "effect_identity_receipt_replay", "effect_identity_receipt_replay", issues);
            if (turn.HasValue && turn < previousTurn)
                Add(issues, transitionPath + ".turn", "effect_identity_invalid_transition", "non-decreasing positive transition turn", turn.Value.ToString());
            if (turn.HasValue)
                previousTurn = turn.Value;
            if (sourceIds.Count == 0 && resultIds.Count == 0)
                Add(issues, transitionPath, "effect_identity_invalid_transition", "at least one source or result effect identity", transition.GetRawText());

            if (issues.Count == issueCount && transitionId != null && kind != null && turn.HasValue && eventRef != null)
            {
                result.Add(new EffectIdentityTransition(
                    transitionId,
                    kind,
                    turn.Value,
                    eventRef,
                    sourceIds,
                    resultIds,
                    receiptId,
                    JsonNode.Parse(transition.GetRawText())!.AsObject()));
            }
        }
        return result;
    }

    private static void ValidateStateTransitionAgreement(
        string effectId,
        string state,
        EffectIdentityTransition last,
        string path,
        List<ValidationIssue> issues)
    {
        if (ActiveStates.Contains(state))
        {
            if (TerminalTransitionKinds.Contains(last.Kind) || !last.ResultEffectIds.Contains(effectId, StringComparer.Ordinal))
            {
                Add(issues, path + ".state", "effect_identity_active_terminal_conflict", "active/suspended entry whose latest transition retains effectId", state);
            }
            return;
        }

        var expectedKind = state switch
        {
            "expired" => "expire",
            "dispelled" => "dispel",
            "removed" => "remove",
            "replaced" => "replace",
            _ => string.Empty
        };
        if (!string.Equals(last.Kind, expectedKind, StringComparison.Ordinal) ||
            !last.SourceEffectIds.Contains(effectId, StringComparer.Ordinal) ||
            last.ResultEffectIds.Contains(effectId, StringComparer.Ordinal))
        {
            Add(issues, path + ".state", "effect_identity_terminal_evidence_mismatch", $"terminal {state} entry ending with {expectedKind} transition", last.Raw.ToJsonString());
        }
    }

    private static List<string> ParseIdentityArray(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        var result = new List<string>();
        if (!root.TryGetProperty(field, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            Add(issues, path + "." + field, "effect_identity_invalid_transition", "identity array", Describe(root, field));
            return result;
        }
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var itemPath = $"{path}.{field}[{index++}]";
            if (!TryReadExactIdentifier(item, out var identity) || !exact.Add(identity) ||
                !aliases.Add(MortalLocationIdentityState.BuildConfusableKey(identity)))
            {
                Add(issues, itemPath, "effect_identity_invalid_transition", "one unique exact/confusable effect identity", item.GetRawText());
                continue;
            }
            result.Add(identity);
        }
        return result;
    }

    private static string? ParseNullableIdentifier(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value))
        {
            Add(issues, path + "." + field, "effect_identity_missing_field", "exact identity or null", "missing");
            return null;
        }
        if (value.ValueKind == JsonValueKind.Null)
            return null;
        if (TryReadExactIdentifier(value, out var identity))
            return identity;
        Add(issues, path + "." + field, "effect_identity_invalid_transition", "exact identity or null", value.GetRawText());
        return null;
    }

    private static JsonElement? ParseClosedObject(
        JsonElement root,
        string path,
        string field,
        IReadOnlySet<string> fields,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            Add(issues, path + "." + field, "effect_identity_invalid_field", "closed object", Describe(root, field));
            return null;
        }
        ValidateClosedObject(value, path + "." + field, fields, issues);
        RequireFields(value, path + "." + field, fields, issues);
        return value;
    }

    private static void RequireFields(
        JsonElement root,
        string path,
        IReadOnlySet<string> fields,
        List<ValidationIssue> issues)
    {
        foreach (var field in fields)
        {
            if (!root.TryGetProperty(field, out _))
                Add(issues, path + "." + field, "effect_identity_missing_field", "required identity-authority field", "missing");
        }
    }

    private static string? RequireClosedString(
        JsonElement root,
        string path,
        string field,
        IReadOnlySet<string> allowed,
        List<ValidationIssue> issues)
    {
        var result = RequireExactIdentifier(root, path, field, issues);
        if (result != null && !allowed.Contains(result))
        {
            Add(issues, path + "." + field, "effect_identity_invalid_field", string.Join(" | ", allowed), result);
            return null;
        }
        return result;
    }

    private static string? RequireExactIdentifier(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(field, out var value) && TryReadExactIdentifier(value, out var result))
            return result;
        Add(issues, path + "." + field, "effect_identity_invalid_field", "exact non-empty identifier without surrounding whitespace", Describe(root, field));
        return null;
    }

    private static int? RequirePositiveInt(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var number) && number > 0)
        {
            return number;
        }
        Add(issues, path + "." + field, "effect_identity_invalid_transition", "positive integer", Describe(root, field));
        return null;
    }

    private static void RequireExactInt(
        JsonElement root,
        string path,
        string field,
        int expected,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var actual) || actual != expected)
        {
            Add(issues, path + "." + field, "effect_identity_invalid_field", expected.ToString(), Describe(root, field));
        }
    }

    private static void ValidateClosedObject(
        JsonElement root,
        string path,
        IReadOnlySet<string> fields,
        List<ValidationIssue> issues)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (!fields.Contains(property.Name))
                Add(issues, path + "." + property.Name, "effect_identity_unknown_field", "registered identity-authority field", property.Name);
        }
    }

    private static void FindDuplicateProperties(
        JsonElement value,
        string path,
        List<ValidationIssue> issues)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in value.EnumerateObject())
                {
                    var propertyPath = path + "." + property.Name;
                    if (!names.Add(property.Name))
                        Add(issues, propertyPath, "effect_identity_duplicate_property", "one occurrence of each exact property", property.Name);
                    FindDuplicateProperties(property.Value, propertyPath, issues);
                }
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in value.EnumerateArray())
                    FindDuplicateProperties(item, $"{path}[{index++}]", issues);
                break;
        }
    }

    private static bool TryReadExactIdentifier(JsonElement value, out string identity)
    {
        identity = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
        return identity.Length > 0 && string.Equals(identity, identity.Trim(), StringComparison.Ordinal);
    }

    private static string Describe(JsonElement root, string field) =>
        root.TryGetProperty(field, out var value) ? value.GetRawText() : "missing";

    private static HashSet<string> Set(params string[] values) => new(values, StringComparer.Ordinal);

    private static void Add(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        issues.Add(NewIssue(path, code, expected, actual));

    private static ValidationIssue NewIssue(string path, string code, string expected, string actual) =>
        new(
            path,
            IssueSeverity.Error,
            "Effect identity index violates client-owned identity authority.",
            code: code,
            section: "effect_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Restore the validated client-owned effect identity history; do not author or retarget identity/index evidence.");

    private sealed class IdentityRegistry
    {
        private readonly HashSet<string> _exact = new(StringComparer.Ordinal);
        private readonly HashSet<string> _aliases = new(StringComparer.Ordinal);

        internal void Register(
            string identity,
            string path,
            string duplicateCode,
            string confusableCode,
            List<ValidationIssue> issues)
        {
            if (!_exact.Add(identity))
            {
                Add(issues, path, duplicateCode, "globally unique exact identity", identity);
                return;
            }
            if (!_aliases.Add(MortalLocationIdentityState.BuildConfusableKey(identity)))
                Add(issues, path, confusableCode, "globally unique exact/confusable identity", identity);
        }
    }
}
