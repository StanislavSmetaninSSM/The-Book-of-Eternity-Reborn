using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record MortalItemIdentityParseResult(
    JsonObject Root,
    IReadOnlyDictionary<string, JsonObject> EntriesByItemId,
    IReadOnlyList<ValidationIssue> Issues);

internal enum MortalItemAcceptedCreationEvidenceMatch
{
    None,
    Exact,
    Confusable
}

internal sealed class MortalItemAcceptedRootCreationEvidence
{
    private readonly HashSet<string> _exactMaterializationIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _exactCreationRefs = new(StringComparer.Ordinal);
    private readonly HashSet<string> _confusableMaterializationIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _confusableCreationRefs = new(StringComparer.Ordinal);

    internal void AddMaterializationId(string value)
    {
        _exactMaterializationIds.Add(value);
        _confusableMaterializationIds.Add(
            MortalItemIdentityRules.BuildConfusableKey(value));
    }

    internal void AddCreationRef(string value)
    {
        _exactCreationRefs.Add(value);
        _confusableCreationRefs.Add(
            MortalItemIdentityRules.BuildConfusableKey(value));
    }

    internal MortalItemAcceptedCreationEvidenceMatch Match(
        string? materializationId,
        string? creationRef)
    {
        var materializationMatch = MatchField(
            materializationId,
            _exactMaterializationIds,
            _confusableMaterializationIds);
        var creationRefMatch = MatchField(
            creationRef,
            _exactCreationRefs,
            _confusableCreationRefs);
        if (materializationMatch == MortalItemAcceptedCreationEvidenceMatch.Exact ||
            creationRefMatch == MortalItemAcceptedCreationEvidenceMatch.Exact)
        {
            return MortalItemAcceptedCreationEvidenceMatch.Exact;
        }

        return materializationMatch == MortalItemAcceptedCreationEvidenceMatch.Confusable ||
               creationRefMatch == MortalItemAcceptedCreationEvidenceMatch.Confusable
            ? MortalItemAcceptedCreationEvidenceMatch.Confusable
            : MortalItemAcceptedCreationEvidenceMatch.None;
    }

    private static MortalItemAcceptedCreationEvidenceMatch MatchField(
        string? value,
        HashSet<string> exactValues,
        HashSet<string> confusableValues)
    {
        if (string.IsNullOrEmpty(value))
            return MortalItemAcceptedCreationEvidenceMatch.None;
        if (exactValues.Contains(value))
            return MortalItemAcceptedCreationEvidenceMatch.Exact;
        return confusableValues.Contains(MortalItemIdentityRules.BuildConfusableKey(value))
            ? MortalItemAcceptedCreationEvidenceMatch.Confusable
            : MortalItemAcceptedCreationEvidenceMatch.None;
    }
}

internal static class MortalItemIdentityRules
{
    internal static bool IsExactIdentity(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal);

    internal static bool TryReadExactSidecarItemId(JsonElement entry, out string itemId)
    {
        itemId = string.Empty;
        if (entry.ValueKind != JsonValueKind.Object ||
            !TryReadOptionalExactIdentity(entry, "itemId", out var hasItemId, out var directItemId) ||
            !TryReadOptionalExactIdentity(entry, "existedId", out var hasExistedId, out var existedItemId) ||
            (!hasItemId && !hasExistedId) ||
            (hasItemId && hasExistedId && !string.Equals(directItemId, existedItemId, StringComparison.Ordinal)))
        {
            return false;
        }

        itemId = hasExistedId ? existedItemId : directItemId;
        return true;
    }

    internal static bool TryReadExactSidecarItemId(JsonObject entry, out string itemId)
    {
        itemId = string.Empty;
        if (!TryReadOptionalExactIdentity(entry, "itemId", out var hasItemId, out var directItemId) ||
            !TryReadOptionalExactIdentity(entry, "existedId", out var hasExistedId, out var existedItemId) ||
            (!hasItemId && !hasExistedId) ||
            (hasItemId && hasExistedId && !string.Equals(directItemId, existedItemId, StringComparison.Ordinal)))
        {
            return false;
        }

        itemId = hasExistedId ? existedItemId : directItemId;
        return true;
    }

    internal static string BuildConfusableKey(string value)
    {
        var trimmed = value.Trim();
        try
        {
            return trimmed.Normalize(NormalizationForm.FormC)
                .ToUpper(CultureInfo.InvariantCulture);
        }
        catch (ArgumentException)
        {
            return trimmed.ToUpper(CultureInfo.InvariantCulture);
        }
    }

    private static bool TryReadOptionalExactIdentity(
        JsonElement entry,
        string propertyName,
        out bool present,
        out string identity)
    {
        present = entry.TryGetProperty(propertyName, out var value);
        identity = string.Empty;
        if (!present)
            return true;

        identity = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
        return IsExactIdentity(identity);
    }

    private static bool TryReadOptionalExactIdentity(
        JsonObject entry,
        string propertyName,
        out bool present,
        out string identity)
    {
        present = entry.ContainsKey(propertyName);
        identity = string.Empty;
        if (!present)
            return true;

        if (entry[propertyName] is not JsonValue value ||
            !value.TryGetValue<string>(out identity!))
        {
            identity = string.Empty;
            return false;
        }

        return IsExactIdentity(identity);
    }
}

internal static class MortalItemIdentityState
{
    internal const string StatePath = "game_state/inventory/item_identity_index.json";
    internal const int SchemaVersion = 1;

    private static readonly string[] RootFieldOrder = { "schemaVersion", "entries" };

    private static readonly string[] EntryFieldOrder =
    {
        "itemId", "receiptId", "state", "currentCarrier", "originMaterializationIds",
        "originCreationRefs", "parentItemIds", "mergedIntoItemId", "transitions"
    };

    private static readonly string[] CarrierFieldOrder =
    {
        "kind", "ownerId", "containerId", "containerPath"
    };

    private static readonly string[] TransitionFieldOrder =
    {
        "transitionId", "kind", "turn", "sourceItemIds", "sourceCarrier",
        "destinationCarrier", "quantityBefore", "quantityAfter", "authorityKind", "authorityId"
    };

    private static readonly HashSet<string> RootFields = new(RootFieldOrder, StringComparer.Ordinal);
    private static readonly HashSet<string> EntryFields = new(EntryFieldOrder, StringComparer.Ordinal);
    private static readonly HashSet<string> CarrierFields = new(CarrierFieldOrder, StringComparer.Ordinal);
    private static readonly HashSet<string> TransitionFields = new(TransitionFieldOrder, StringComparer.Ordinal);

    private static readonly HashSet<string> EntryStates = new(StringComparer.Ordinal)
    {
        "active", "merged", "consumed", "destroyed"
    };

    private static readonly HashSet<string> CarrierKinds = new(StringComparer.Ordinal)
    {
        "player_inventory", "npc_inventory", "location_storage", "vehicle_inventory"
    };

    private static readonly HashSet<string> TransitionKinds = new(StringComparer.Ordinal)
    {
        "create", "transfer", "split", "merge", "consume", "destroy", "semantic_update"
    };

    internal static JsonObject CreateEmptyRoot() =>
        new()
        {
            ["schemaVersion"] = SchemaVersion,
            ["entries"] = new JsonArray()
        };

    internal static MortalItemIdentityParseResult Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return InvalidRoot("missing or blank index JSON");

        try
        {
            using var document = JsonDocument.Parse(json);
            var duplicateIssues = new List<ValidationIssue>();
            CollectDuplicateProperties(document.RootElement, StatePath, duplicateIssues);
            var node = ConvertElement(document.RootElement);
            var parsed = Parse(node);
            if (duplicateIssues.Count == 0)
                return parsed;

            return parsed with
            {
                Issues = duplicateIssues.Concat(parsed.Issues).ToArray()
            };
        }
        catch (JsonException exception)
        {
            return InvalidRoot(exception.Message);
        }
    }

    internal static MortalItemIdentityParseResult Parse(JsonNode? root)
    {
        var issues = new List<ValidationIssue>();
        if (root is not JsonObject rootObject)
            return InvalidRoot(root == null ? "missing" : "non-object JSON root");

        ValidateExactFields(rootObject, RootFields, StatePath, issues);
        if (!TryGetInt(rootObject, "schemaVersion", out var schemaVersion) ||
            schemaVersion != SchemaVersion)
        {
            issues.Add(Issue(
                $"{StatePath}.schemaVersion",
                "mortal_item_identity_invalid_index",
                "Mortal item identity index uses an unsupported schema version.",
                SchemaVersion.ToString(),
                Describe(rootObject["schemaVersion"]),
                null));
        }

        if (rootObject["entries"] is not JsonArray entries)
        {
            issues.Add(Issue(
                $"{StatePath}.entries",
                "mortal_item_identity_invalid_index",
                "Mortal item identity index entries must be an array.",
                "array",
                Describe(rootObject["entries"]),
                null));
            return new MortalItemIdentityParseResult(
                CreateEmptyRoot(),
                new Dictionary<string, JsonObject>(StringComparer.Ordinal),
                issues);
        }

        var normalizedEntries = new List<JsonObject>();
        var entriesByItemId = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var receiptIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var transitionIds = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < entries.Count; index++)
        {
            var path = $"{StatePath}.entries[{index}]";
            if (entries[index] is not JsonObject entry)
            {
                issues.Add(Issue(
                    path,
                    "mortal_item_identity_invalid_entry",
                    "Every identity-index entry must be an object.",
                    "object",
                    Describe(entries[index]),
                    null));
                continue;
            }

            ValidateEntry(
                entry,
                path,
                entriesByItemId,
                receiptIds,
                transitionIds,
                issues);
            normalizedEntries.Add(NormalizeEntry(entry));
        }

        var orderedEntries = normalizedEntries
            .OrderBy(ReadItemIdOrEmpty, StringComparer.Ordinal)
            .ToArray();
        var normalizedRoot = new JsonObject
        {
            ["schemaVersion"] = SchemaVersion,
            ["entries"] = new JsonArray(
                orderedEntries.Select(entry => (JsonNode?)entry).ToArray())
        };
        var normalizedByItemId = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var entry in orderedEntries)
        {
            var itemId = ReadItemIdOrEmpty(entry);
            if (itemId.Length > 0)
                normalizedByItemId.TryAdd(itemId, entry);
        }
        return new MortalItemIdentityParseResult(normalizedRoot, normalizedByItemId, issues);
    }

    internal static JsonObject CreateRootReceipt(
        JsonObject rawItem,
        string itemId,
        int acceptedTurn) => CreateRootReceipt(
            rawItem,
            itemId,
            acceptedTurn,
            NewId("mirec_"));

    internal static JsonObject CreateRootReceipt(
        JsonObject rawItem,
        string itemId,
        int acceptedTurn,
        string receiptId)
    {
        ArgumentNullException.ThrowIfNull(rawItem);
        RequireExactIdentity(itemId, nameof(itemId));
        RequireExactIdentity(receiptId, nameof(receiptId));
        if (acceptedTurn < 1)
            throw new ArgumentOutOfRangeException(nameof(acceptedTurn));

        var envelope = rawItem[MortalItemMaterializationContract.EnvelopeProperty]?.AsObject() ??
                       throw new InvalidOperationException("Cannot seal an item without a materialization envelope.");
        var receipt = new JsonObject
        {
            ["schemaVersion"] = SchemaVersion,
            ["receiptId"] = receiptId,
            ["itemId"] = itemId,
            ["materializationId"] = RequireExactIdentityValue(envelope, "materializationId"),
            ["acceptedAtTurn"] = acceptedTurn,
            ["creationRef"] = RequireExactIdentityValue(envelope, "creationRef"),
            ["instanceKind"] = "root",
            ["parentItemIds"] = new JsonArray()
        };
        receipt["seal"] = MortalItemMaterializationContract.ComputeSeal(rawItem, receipt);
        return receipt;
    }

    internal static JsonObject CreateSplitReceipt(
        JsonObject parent,
        string childItemId,
        int turn)
    {
        ArgumentNullException.ThrowIfNull(parent);
        RequireExactIdentity(childItemId, nameof(childItemId));
        if (turn < 1)
            throw new ArgumentOutOfRangeException(nameof(turn));

        var parentItemId = RequireExactIdentityValue(parent, "itemId");
        var envelope = parent[MortalItemMaterializationContract.EnvelopeProperty]?.AsObject() ??
                       throw new InvalidOperationException("Cannot derive a split receipt without a materialization envelope.");
        var receipt = new JsonObject
        {
            ["schemaVersion"] = SchemaVersion,
            ["receiptId"] = NewId("mirec_"),
            ["itemId"] = childItemId,
            ["materializationId"] = RequireExactIdentityValue(envelope, "materializationId"),
            ["acceptedAtTurn"] = turn,
            ["creationRef"] = RequireExactIdentityValue(envelope, "creationRef"),
            ["instanceKind"] = "split_derived",
            ["parentItemIds"] = new JsonArray(parentItemId)
        };
        receipt["seal"] = MortalItemMaterializationContract.ComputeSeal(parent, receipt);
        return receipt;
    }

    internal static JsonObject CreateTransition(
        string kind,
        int turn,
        IEnumerable<string> sourceItemIds,
        JsonObject? sourceCarrier,
        JsonObject? destinationCarrier,
        int quantityBefore,
        int quantityAfter,
        string authorityKind,
        string authorityId) => CreateTransition(
            kind,
            turn,
            sourceItemIds,
            sourceCarrier,
            destinationCarrier,
            quantityBefore,
            quantityAfter,
            authorityKind,
            authorityId,
            NewId("mitrn_"));

    internal static JsonObject CreateTransition(
        string kind,
        int turn,
        IEnumerable<string> sourceItemIds,
        JsonObject? sourceCarrier,
        JsonObject? destinationCarrier,
        int quantityBefore,
        int quantityAfter,
        string authorityKind,
        string authorityId,
        string transitionId)
    {
        if (!TransitionKinds.Contains(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (turn < 0)
            throw new ArgumentOutOfRangeException(nameof(turn));
        if (quantityBefore < 0)
            throw new ArgumentOutOfRangeException(nameof(quantityBefore));
        if (quantityAfter < 0)
            throw new ArgumentOutOfRangeException(nameof(quantityAfter));
        RequireExactIdentity(authorityKind, nameof(authorityKind));
        RequireExactIdentity(authorityId, nameof(authorityId));
        RequireExactIdentity(transitionId, nameof(transitionId));

        var sourceIds = sourceItemIds?.ToArray() ??
                        throw new ArgumentNullException(nameof(sourceItemIds));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sourceItemId in sourceIds)
        {
            RequireExactIdentity(sourceItemId, nameof(sourceItemIds));
            if (!seen.Add(sourceItemId))
                throw new ArgumentException("Source item IDs must be ordinal-unique.", nameof(sourceItemIds));
        }

        return new JsonObject
        {
            ["transitionId"] = transitionId,
            ["kind"] = kind,
            ["turn"] = turn,
            ["sourceItemIds"] = new JsonArray(
                sourceIds.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
            ["sourceCarrier"] = sourceCarrier?.DeepClone(),
            ["destinationCarrier"] = destinationCarrier?.DeepClone(),
            ["quantityBefore"] = quantityBefore,
            ["quantityAfter"] = quantityAfter,
            ["authorityKind"] = authorityKind,
            ["authorityId"] = authorityId
        };
    }

    internal static void AppendTransition(JsonObject entry, JsonObject transition)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(transition);
        if (entry["transitions"] is not JsonArray transitions)
            throw new InvalidOperationException("Identity entry does not contain a transitions array.");
        transitions.Add(transition.DeepClone());
    }

    internal static MortalItemAcceptedRootCreationEvidence
        BuildAcceptedRootCreationEvidence(MortalItemIdentityParseResult index)
    {
        ArgumentNullException.ThrowIfNull(index);
        var evidence = new MortalItemAcceptedRootCreationEvidence();
        foreach (var entry in index.EntriesByItemId.Values)
        {
            foreach (var materializationId in
                     ReadValidIdentitySet(entry["originMaterializationIds"]))
            {
                evidence.AddMaterializationId(materializationId);
            }
            foreach (var creationRef in
                     ReadValidIdentitySet(entry["originCreationRefs"]))
            {
                evidence.AddCreationRef(creationRef);
            }
        }
        return evidence;
    }

    internal static IReadOnlyList<ValidationIssue> ValidateAgainst(
        MortalItemIdentityParseResult previous,
        MortalItemIdentityParseResult current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        var issues = new List<ValidationIssue>();
        foreach (var (itemId, previousEntry) in previous.EntriesByItemId)
        {
            if (!current.EntriesByItemId.TryGetValue(itemId, out var currentEntry))
            {
                issues.Add(Issue(
                    $"{StatePath}.entries[{itemId}]",
                    "mortal_item_identity_entry_removed",
                    "Accepted Mortal item identity history cannot be removed.",
                    "the complete previous entry",
                    "missing",
                    itemId));
                continue;
            }

            ValidateImmutableEntryField(
                previousEntry,
                currentEntry,
                "receiptId",
                itemId,
                issues);
            ValidateImmutableEntryField(
                previousEntry,
                currentEntry,
                "parentItemIds",
                itemId,
                issues);
            ValidateTransitionContinuity(previousEntry, currentEntry, itemId, issues);
            ValidateAppendedTransitionQuantityContinuity(
                previousEntry,
                currentEntry,
                itemId,
                issues);
            ValidateTransitionBackedStateChange(
                previousEntry,
                currentEntry,
                itemId,
                previous.EntriesByItemId,
                current.EntriesByItemId,
                issues);
            ValidateRetirementContinuity(previousEntry, currentEntry, itemId, issues);
        }

        return issues;
    }

    private static void ValidateImmutableEntryField(
        JsonObject previous,
        JsonObject current,
        string field,
        string itemId,
        List<ValidationIssue> issues)
    {
        if (JsonNode.DeepEquals(previous[field], current[field]))
            return;

        issues.Add(Issue(
            $"{StatePath}.entries[{itemId}].{field}",
            "mortal_item_identity_protected_field_rewrite",
            "Accepted Mortal item identity evidence is immutable.",
            Describe(previous[field]),
            Describe(current[field]),
            itemId));
    }

    private static void ValidateTransitionContinuity(
        JsonObject previous,
        JsonObject current,
        string itemId,
        List<ValidationIssue> issues)
    {
        var previousTransitions = previous["transitions"] as JsonArray;
        var currentTransitions = current["transitions"] as JsonArray;
        if (previousTransitions == null || currentTransitions == null ||
            currentTransitions.Count < previousTransitions.Count)
        {
            issues.Add(TransitionHistoryIssue(itemId, previousTransitions, currentTransitions));
            return;
        }

        for (var index = 0; index < previousTransitions.Count; index++)
        {
            if (JsonNode.DeepEquals(previousTransitions[index], currentTransitions[index]))
                continue;
            issues.Add(TransitionHistoryIssue(itemId, previousTransitions, currentTransitions));
            return;
        }
    }

    private static void ValidateAppendedTransitionQuantityContinuity(
        JsonObject previous,
        JsonObject current,
        string itemId,
        List<ValidationIssue> issues)
    {
        var previousTransitions = previous["transitions"] as JsonArray;
        var currentTransitions = current["transitions"] as JsonArray;
        if (previousTransitions is not { Count: > 0 } ||
            currentTransitions == null ||
            currentTransitions.Count <= previousTransitions.Count ||
            previousTransitions[^1] is not JsonObject previousLast ||
            !TryGetInt(previousLast, "quantityAfter", out var expectedQuantity))
        {
            return;
        }

        for (var index = previousTransitions.Count; index < currentTransitions.Count; index++)
        {
            if (currentTransitions[index] is not JsonObject transition ||
                !TryGetInt(transition, "quantityBefore", out var quantityBefore) ||
                !TryGetInt(transition, "quantityAfter", out var quantityAfter))
            {
                return;
            }

            if (quantityBefore != expectedQuantity)
            {
                issues.Add(Issue(
                    $"{StatePath}.entries[{itemId}].transitions[{index}].quantityBefore",
                    "mortal_item_identity_quantity_transition_mismatch",
                    "An appended Mortal item transition must continue the exact recorded quantity history.",
                    expectedQuantity.ToString(),
                    quantityBefore.ToString(),
                    itemId));
                return;
            }

            expectedQuantity = quantityAfter;
        }
    }

    private static void ValidateRetirementContinuity(
        JsonObject previous,
        JsonObject current,
        string itemId,
        List<ValidationIssue> issues)
    {
        var previousState = ReadExactIdentity(previous["state"]);
        var currentState = ReadExactIdentity(current["state"]);
        if (previousState is not ("merged" or "consumed" or "destroyed"))
            return;

        var previousTransitionCount = (previous["transitions"] as JsonArray)?.Count ?? 0;
        var currentTransitionCount = (current["transitions"] as JsonArray)?.Count ?? 0;
        if (string.Equals(previousState, currentState, StringComparison.Ordinal) &&
            JsonNode.DeepEquals(previous["mergedIntoItemId"], current["mergedIntoItemId"]) &&
            current["currentCarrier"] == null &&
            currentTransitionCount == previousTransitionCount)
        {
            return;
        }

        issues.Add(Issue(
            $"{StatePath}.entries[{itemId}].state",
            "mortal_item_identity_retired_reactivated",
            "A retired Mortal item identity cannot be rewritten or reactivated.",
            $"state={previousState}; carrier=null; mergedInto={Describe(previous["mergedIntoItemId"])}",
            $"state={currentState ?? "missing"}; carrier={Describe(current["currentCarrier"])}; mergedInto={Describe(current["mergedIntoItemId"])}",
            itemId));
    }

    private sealed class TransitionCursor
    {
        internal TransitionCursor(
            string? state,
            JsonNode? carrier,
            int quantity,
            HashSet<string> materializationOrigins,
            HashSet<string> creationOrigins)
        {
            State = state;
            Carrier = carrier;
            Quantity = quantity;
            MaterializationOrigins = materializationOrigins;
            CreationOrigins = creationOrigins;
        }

        internal string? State { get; set; }
        internal JsonNode? Carrier { get; set; }
        internal int Quantity { get; set; }
        internal HashSet<string> MaterializationOrigins { get; set; }
        internal HashSet<string> CreationOrigins { get; set; }
    }

    private static void ValidateTransitionBackedStateChange(
        JsonObject previous,
        JsonObject current,
        string itemId,
        IReadOnlyDictionary<string, JsonObject> previousEntries,
        IReadOnlyDictionary<string, JsonObject> currentEntries,
        List<ValidationIssue> issues)
    {
        var protectedStateChanged =
            !JsonNode.DeepEquals(previous["state"], current["state"]) ||
            !JsonNode.DeepEquals(previous["currentCarrier"], current["currentCarrier"]) ||
            !JsonNode.DeepEquals(previous["mergedIntoItemId"], current["mergedIntoItemId"]) ||
            !JsonNode.DeepEquals(previous["originMaterializationIds"], current["originMaterializationIds"]) ||
            !JsonNode.DeepEquals(previous["originCreationRefs"], current["originCreationRefs"]);
        var cursor = CreateInitialTransitionCursor(previous);
        var previousTransitions = previous["transitions"] as JsonArray;
        var currentTransitions = current["transitions"] as JsonArray;
        var hasAppendedSuffix = previousTransitions != null &&
                                currentTransitions != null &&
                                currentTransitions.Count > previousTransitions.Count;
        if (!hasAppendedSuffix)
        {
            ReportDerivedOriginMismatches(cursor, current, itemId, issues);
            if (protectedStateChanged)
                issues.Add(UnrecordedStateChangeIssue(itemId, previous, current));
            return;
        }

        var valid = TryReplayTransitionSuffix(
            previous,
            current,
            itemId,
            previousEntries,
            currentEntries,
            currentTransitions!.Count,
            new HashSet<string>(StringComparer.Ordinal),
            issues,
            out var reportedSpecificMismatch,
            out cursor);
        ReportDerivedOriginMismatches(cursor, current, itemId, issues);
        if (!valid)
        {
            if (!reportedSpecificMismatch)
                issues.Add(UnrecordedStateChangeIssue(itemId, previous, current));
            return;
        }

        if (!string.Equals(
                cursor.State,
                ReadExactIdentity(current["state"]),
                StringComparison.Ordinal) ||
            !JsonNode.DeepEquals(cursor.Carrier, current["currentCarrier"]))
        {
            issues.Add(UnrecordedStateChangeIssue(itemId, previous, current));
        }
    }

    private static TransitionCursor CreateInitialTransitionCursor(JsonObject previous)
    {
        var quantity = -1;
        if (previous["transitions"] is JsonArray { Count: > 0 } transitions &&
            transitions[^1] is JsonObject lastTransition)
        {
            TryGetInt(lastTransition, "quantityAfter", out quantity);
        }

        return new TransitionCursor(
            ReadExactIdentity(previous["state"]),
            previous["currentCarrier"],
            quantity,
            ReadValidIdentitySet(previous["originMaterializationIds"]),
            ReadValidIdentitySet(previous["originCreationRefs"]));
    }

    private static bool TryReplayTransitionSuffix(
        JsonObject previous,
        JsonObject current,
        string itemId,
        IReadOnlyDictionary<string, JsonObject> previousEntries,
        IReadOnlyDictionary<string, JsonObject> currentEntries,
        int endExclusive,
        HashSet<string> replayStack,
        List<ValidationIssue>? issues,
        out bool reportedSpecificMismatch,
        out TransitionCursor cursor)
    {
        reportedSpecificMismatch = false;
        cursor = CreateInitialTransitionCursor(previous);
        if (previous["transitions"] is not JsonArray { Count: > 0 } previousTransitions ||
            current["transitions"] is not JsonArray currentTransitions ||
            cursor.Quantity < 0 ||
            endExclusive < previousTransitions.Count ||
            endExclusive > currentTransitions.Count)
        {
            return false;
        }

        for (var index = 0; index < previousTransitions.Count; index++)
        {
            if (!JsonNode.DeepEquals(previousTransitions[index], currentTransitions[index]))
                return false;
        }

        var replayKey = $"{itemId}\u001f{endExclusive}";
        if (!replayStack.Add(replayKey))
            return false;

        try
        {
            for (var index = previousTransitions.Count; index < endExclusive; index++)
            {
                var transitionReportedSpecificMismatch = false;
                if (currentTransitions[index] is not JsonObject transition ||
                    !TryApplyTransitionAtCursor(
                        previousEntries,
                        currentEntries,
                        current,
                        transition,
                        index,
                        itemId,
                        cursor,
                        replayStack,
                        issues,
                        out transitionReportedSpecificMismatch))
                {
                    reportedSpecificMismatch |= transitionReportedSpecificMismatch;
                    return false;
                }

                reportedSpecificMismatch |= transitionReportedSpecificMismatch;
            }

            return true;
        }
        finally
        {
            replayStack.Remove(replayKey);
        }
    }

    private static bool TryApplyTransitionAtCursor(
        IReadOnlyDictionary<string, JsonObject> previousEntries,
        IReadOnlyDictionary<string, JsonObject> currentEntries,
        JsonObject current,
        JsonObject transition,
        int transitionIndex,
        string itemId,
        TransitionCursor cursor,
        HashSet<string> replayStack,
        List<ValidationIssue>? issues,
        out bool reportedSpecificMismatch)
    {
        reportedSpecificMismatch = false;
        if (!string.Equals(cursor.State, "active", StringComparison.Ordinal) ||
            !TryGetInt(transition, "quantityBefore", out var quantityBefore) ||
            !TryGetInt(transition, "quantityAfter", out var quantityAfter))
        {
            return false;
        }

        var kind = ReadExactIdentity(transition["kind"]);
        if (string.Equals(kind, "transfer", StringComparison.Ordinal))
        {
            if (!IsValidTransferTransition(cursor, transition, itemId))
            {
                if (issues != null)
                {
                    ReportTransferTransitionMismatch(
                        cursor,
                        transition,
                        itemId,
                        issues);
                    reportedSpecificMismatch = true;
                }
                return false;
            }

            cursor.Carrier = transition["destinationCarrier"];
            cursor.Quantity = quantityAfter;
            return true;
        }

        var hasExactSourceIdentity = HasExactSourceIdentity(transition, itemId);
        var startsAtCursor = quantityBefore == cursor.Quantity &&
                             cursor.Quantity > 0 &&
                             JsonNode.DeepEquals(transition["sourceCarrier"], cursor.Carrier);
        var destinationCarrier = transition["destinationCarrier"];
        var nextMaterializationOrigins = cursor.MaterializationOrigins;
        var nextCreationOrigins = cursor.CreationOrigins;
        var valid = kind switch
        {
            "consume" => hasExactSourceIdentity && startsAtCursor &&
                         quantityAfter >= 0 && quantityAfter < quantityBefore &&
                         (quantityAfter == 0
                             ? destinationCarrier == null
                             : JsonNode.DeepEquals(destinationCarrier, cursor.Carrier)),
            "destroy" => hasExactSourceIdentity && startsAtCursor &&
                         quantityAfter == 0 && destinationCarrier == null,
            "split" => hasExactSourceIdentity && startsAtCursor &&
                       quantityAfter > 0 && quantityAfter < quantityBefore &&
                       JsonNode.DeepEquals(destinationCarrier, cursor.Carrier),
            "merge" => ValidateMergeTransitionAtCursor(
                previousEntries,
                currentEntries,
                current,
                transition,
                transitionIndex,
                itemId,
                cursor,
                replayStack,
                out nextMaterializationOrigins,
                out nextCreationOrigins),
            "semantic_update" => hasExactSourceIdentity && startsAtCursor &&
                                 quantityAfter > 0 &&
                                 quantityAfter != quantityBefore &&
                                 JsonNode.DeepEquals(destinationCarrier, cursor.Carrier),
            _ => false
        };
        if (!valid)
            return false;

        cursor.Quantity = quantityAfter;
        cursor.MaterializationOrigins = nextMaterializationOrigins;
        cursor.CreationOrigins = nextCreationOrigins;
        if (destinationCarrier != null)
        {
            cursor.Carrier = destinationCarrier;
            return true;
        }

        cursor.Carrier = null;
        cursor.State = kind switch
        {
            "consume" => "consumed",
            "destroy" => "destroyed",
            "merge" => "merged",
            _ => cursor.State
        };
        return true;
    }

    private static bool IsValidTransferTransition(
        TransitionCursor cursor,
        JsonObject transition,
        string itemId) =>
        HasExactSourceIdentity(transition, itemId) &&
        cursor.Quantity > 0 &&
        TryGetInt(transition, "quantityBefore", out var quantityBefore) &&
        TryGetInt(transition, "quantityAfter", out var quantityAfter) &&
        quantityBefore == cursor.Quantity &&
        quantityAfter == quantityBefore &&
        JsonNode.DeepEquals(transition["sourceCarrier"], cursor.Carrier) &&
        transition["destinationCarrier"] is JsonObject &&
        !JsonNode.DeepEquals(transition["destinationCarrier"], cursor.Carrier);

    private static void ReportTransferTransitionMismatch(
        TransitionCursor cursor,
        JsonObject transition,
        string itemId,
        List<ValidationIssue> issues) =>
        issues.Add(Issue(
            $"{StatePath}.entries[{itemId}].transitions",
            "mortal_item_identity_transfer_transition_mismatch",
            "A Mortal item transfer must start at the exact carrier/quantity cursor, name the moved identity, and preserve quantity.",
            $"sourceItemIds=[{itemId}]; sourceCarrier={Describe(cursor.Carrier)}; quantityBefore=quantityAfter={cursor.Quantity}",
            transition.ToJsonString(),
            itemId));

    private static bool HasExactSourceIdentity(JsonObject transition, string itemId) =>
        transition["sourceItemIds"] is JsonArray { Count: 1 } sourceItemIds &&
        string.Equals(
            ReadExactIdentity(sourceItemIds[0]),
            itemId,
            StringComparison.Ordinal);

    private static bool ValidateMergeTransitionAtCursor(
        IReadOnlyDictionary<string, JsonObject> previousEntries,
        IReadOnlyDictionary<string, JsonObject> currentEntries,
        JsonObject current,
        JsonObject transition,
        int transitionIndex,
        string itemId,
        TransitionCursor cursor,
        HashSet<string> replayStack,
        out HashSet<string> nextMaterializationOrigins,
        out HashSet<string> nextCreationOrigins)
    {
        nextMaterializationOrigins = new HashSet<string>(
            cursor.MaterializationOrigins,
            StringComparer.Ordinal);
        nextCreationOrigins = new HashSet<string>(
            cursor.CreationOrigins,
            StringComparer.Ordinal);
        if (transition["sourceItemIds"] is not JsonArray { Count: >= 2 } sourceItemIdNodes)
            return false;
        var sourceItemIds = ReadValidIdentitySet(sourceItemIdNodes);
        if (sourceItemIds.Count != sourceItemIdNodes.Count ||
            !sourceItemIds.Contains(itemId) ||
            !TryGetInt(transition, "quantityBefore", out var quantityBefore) ||
            !TryGetInt(transition, "quantityAfter", out var quantityAfter) ||
            quantityBefore != cursor.Quantity ||
            cursor.Quantity <= 0 ||
            !JsonNode.DeepEquals(transition["sourceCarrier"], cursor.Carrier))
        {
            return false;
        }

        if (transition["destinationCarrier"] != null)
        {
            if (!JsonNode.DeepEquals(transition["destinationCarrier"], cursor.Carrier))
                return false;

            long expectedQuantity = cursor.Quantity;
            foreach (var sourceItemId in sourceItemIds)
            {
                if (string.Equals(sourceItemId, itemId, StringComparison.Ordinal))
                    continue;
                if (!TryResolvePairedMergeParticipant(
                        previousEntries,
                        currentEntries,
                        transition,
                        sourceItemId,
                        itemId,
                        survivorRole: false,
                        replayStack,
                        out var sourceCursor,
                        out _))
                {
                    return false;
                }

                expectedQuantity += sourceCursor.Quantity;
                if (expectedQuantity > int.MaxValue)
                    return false;
                nextMaterializationOrigins.UnionWith(sourceCursor.MaterializationOrigins);
                nextCreationOrigins.UnionWith(sourceCursor.CreationOrigins);
            }

            return quantityAfter == expectedQuantity;
        }

        var mergedIntoItemId = ReadExactIdentity(current["mergedIntoItemId"]);
        if (quantityAfter != 0 ||
            mergedIntoItemId == null ||
            string.Equals(mergedIntoItemId, itemId, StringComparison.Ordinal) ||
            !sourceItemIds.Contains(mergedIntoItemId) ||
            current["transitions"] is not JsonArray currentTransitions ||
            transitionIndex != currentTransitions.Count - 1 ||
            !string.Equals(ReadExactIdentity(current["state"]), "merged", StringComparison.Ordinal) ||
            current["currentCarrier"] != null ||
            !TryResolvePairedMergeParticipant(
                previousEntries,
                currentEntries,
                transition,
                mergedIntoItemId,
                mergedIntoItemId,
                survivorRole: true,
                replayStack,
                out var survivorCursor,
                out var survivorTransition))
        {
            return false;
        }

        long survivorQuantityAfter = 0;
        foreach (var sourceItemId in sourceItemIds)
        {
            TransitionCursor sourceCursor;
            if (string.Equals(sourceItemId, itemId, StringComparison.Ordinal))
            {
                sourceCursor = cursor;
            }
            else if (string.Equals(sourceItemId, mergedIntoItemId, StringComparison.Ordinal))
            {
                sourceCursor = survivorCursor;
            }
            else if (!TryResolvePairedMergeParticipant(
                         previousEntries,
                         currentEntries,
                         transition,
                         sourceItemId,
                         mergedIntoItemId,
                         survivorRole: false,
                         replayStack,
                         out sourceCursor,
                         out _))
            {
                return false;
            }

            if (!JsonNode.DeepEquals(sourceCursor.Carrier, cursor.Carrier))
                return false;
            survivorQuantityAfter += sourceCursor.Quantity;
            if (survivorQuantityAfter > int.MaxValue)
                return false;
        }

        return TryGetInt(survivorTransition, "quantityAfter", out var recordedQuantityAfter) &&
               recordedQuantityAfter == survivorQuantityAfter;
    }

    private static bool TryResolvePairedMergeParticipant(
        IReadOnlyDictionary<string, JsonObject> previousEntries,
        IReadOnlyDictionary<string, JsonObject> currentEntries,
        JsonObject referenceTransition,
        string participantItemId,
        string survivorItemId,
        bool survivorRole,
        HashSet<string> replayStack,
        out TransitionCursor participantCursor,
        out JsonObject participantTransition)
    {
        participantCursor = null!;
        participantTransition = null!;
        if (!previousEntries.TryGetValue(participantItemId, out var previousParticipant) ||
            !currentEntries.TryGetValue(participantItemId, out var currentParticipant) ||
            previousParticipant["transitions"] is not JsonArray previousTransitions ||
            currentParticipant["transitions"] is not JsonArray currentTransitions ||
            currentTransitions.Count <= previousTransitions.Count)
        {
            return false;
        }

        var matchingIndex = -1;
        for (var index = previousTransitions.Count; index < currentTransitions.Count; index++)
        {
            if (currentTransitions[index] is not JsonObject candidate ||
                !HasMatchingMergeEnvelope(candidate, referenceTransition, survivorRole))
            {
                continue;
            }

            if (matchingIndex >= 0)
                return false;
            matchingIndex = index;
            participantTransition = candidate;
        }

        if (matchingIndex < 0 ||
            !TryReplayTransitionSuffix(
                previousParticipant,
                currentParticipant,
                participantItemId,
                previousEntries,
                currentEntries,
                matchingIndex,
                replayStack,
                issues: null,
                out _,
                out participantCursor) ||
            !string.Equals(participantCursor.State, "active", StringComparison.Ordinal) ||
            participantCursor.Quantity <= 0 ||
            !JsonNode.DeepEquals(
                participantTransition["sourceCarrier"],
                participantCursor.Carrier) ||
            !TryGetInt(
                participantTransition,
                "quantityBefore",
                out var quantityBefore) ||
            quantityBefore != participantCursor.Quantity ||
            !TryGetInt(participantTransition, "quantityAfter", out var quantityAfter))
        {
            return false;
        }

        if (survivorRole)
        {
            return quantityAfter > 0 &&
                   JsonNode.DeepEquals(
                       participantTransition["destinationCarrier"],
                       participantCursor.Carrier);
        }

        return matchingIndex == currentTransitions.Count - 1 &&
               quantityAfter == 0 &&
               participantTransition["destinationCarrier"] == null &&
               string.Equals(
                   ReadExactIdentity(currentParticipant["state"]),
                   "merged",
                   StringComparison.Ordinal) &&
               string.Equals(
                   ReadExactIdentity(currentParticipant["mergedIntoItemId"]),
                   survivorItemId,
                   StringComparison.Ordinal) &&
               currentParticipant["currentCarrier"] == null;
    }

    private static bool HasMatchingMergeEnvelope(
        JsonObject candidate,
        JsonObject reference,
        bool survivorRole) =>
        string.Equals(ReadExactIdentity(candidate["kind"]), "merge", StringComparison.Ordinal) &&
        JsonNode.DeepEquals(candidate["sourceItemIds"], reference["sourceItemIds"]) &&
        JsonNode.DeepEquals(candidate["sourceCarrier"], reference["sourceCarrier"]) &&
        (survivorRole
            ? JsonNode.DeepEquals(candidate["destinationCarrier"], candidate["sourceCarrier"])
            : candidate["destinationCarrier"] == null) &&
        JsonNode.DeepEquals(candidate["authorityKind"], reference["authorityKind"]) &&
        JsonNode.DeepEquals(candidate["authorityId"], reference["authorityId"]) &&
        JsonNode.DeepEquals(candidate["turn"], reference["turn"]);

    private static void ReportDerivedOriginMismatches(
        TransitionCursor cursor,
        JsonObject current,
        string itemId,
        List<ValidationIssue> issues)
    {
        var currentMaterializationOrigins = ReadValidIdentitySet(
            current["originMaterializationIds"]);
        if (!cursor.MaterializationOrigins.SetEquals(currentMaterializationOrigins))
        {
            issues.Add(Issue(
                $"{StatePath}.entries[{itemId}].originMaterializationIds",
                "mortal_item_identity_origin_history_rewrite",
                "Accepted origin materialization history must equal the exact union derived by the appended transition suffix.",
                string.Join(", ", cursor.MaterializationOrigins.OrderBy(
                    value => value,
                    StringComparer.Ordinal)),
                string.Join(", ", currentMaterializationOrigins.OrderBy(
                    value => value,
                    StringComparer.Ordinal)),
                itemId));
        }

        var currentCreationOrigins = ReadValidIdentitySet(current["originCreationRefs"]);
        if (!cursor.CreationOrigins.SetEquals(currentCreationOrigins))
        {
            issues.Add(Issue(
                $"{StatePath}.entries[{itemId}].originCreationRefs",
                "mortal_item_identity_origin_creation_history_rewrite",
                "Accepted root creation-reference history must equal the exact union derived by the appended transition suffix.",
                string.Join(", ", cursor.CreationOrigins.OrderBy(
                    value => value,
                    StringComparer.Ordinal)),
                string.Join(", ", currentCreationOrigins.OrderBy(
                    value => value,
                    StringComparer.Ordinal)),
                itemId));
        }
    }

    private static ValidationIssue UnrecordedStateChangeIssue(
        string itemId,
        JsonObject previous,
        JsonObject current) =>
        Issue(
            $"{StatePath}.entries[{itemId}]",
            "mortal_item_identity_unrecorded_state_change",
            "Protected Mortal item carrier, retirement, or origin state must be backed by an appended matching transition.",
            previous.ToJsonString(),
            current.ToJsonString(),
            itemId);

    private static ValidationIssue TransitionHistoryIssue(
        string itemId,
        JsonArray? previous,
        JsonArray? current) =>
        Issue(
            $"{StatePath}.entries[{itemId}].transitions",
            "mortal_item_identity_transition_history_rewrite",
            "Accepted Mortal item transition history must remain an exact prefix.",
            previous?.ToJsonString() ?? "valid previous transition array",
            current?.ToJsonString() ?? "missing",
            itemId);

    private static HashSet<string> ReadValidIdentitySet(JsonNode? node)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (node is not JsonArray array)
            return result;
        foreach (var child in array)
        {
            var value = ReadExactIdentity(child);
            if (value != null)
                result.Add(value);
        }
        return result;
    }

    private static void ValidateEntry(
        JsonObject entry,
        string path,
        Dictionary<string, JsonObject> entriesByItemId,
        Dictionary<string, string> receiptIds,
        Dictionary<string, string> transitionIds,
        List<ValidationIssue> issues)
    {
        ValidateExactFields(entry, EntryFields, path, issues);
        var itemId = ReadExactIdentity(entry["itemId"]);
        var receiptId = ReadExactIdentity(entry["receiptId"]);
        if (itemId == null)
            issues.Add(InvalidEntry($"{path}.itemId", "non-empty untrimmed itemId", Describe(entry["itemId"]), null));
        else if (!entriesByItemId.TryAdd(itemId, entry))
            issues.Add(Issue(
                $"{path}.itemId",
                "mortal_item_materialization_duplicate_item_id",
                "Permanent Mortal item IDs must be globally unique.",
                "unique ordinal itemId",
                itemId,
                itemId));

        if (receiptId == null)
            issues.Add(InvalidEntry($"{path}.receiptId", "non-empty untrimmed receiptId", Describe(entry["receiptId"]), itemId));
        else if (!receiptIds.TryAdd(receiptId, path))
            issues.Add(Issue(
                $"{path}.receiptId",
                "mortal_item_materialization_duplicate_receipt_id",
                "Client-sealed receipt IDs must be globally unique.",
                $"unique ordinal receiptId; first at {receiptIds[receiptId]}",
                receiptId,
                itemId));

        var state = ReadExactIdentity(entry["state"]);
        if (state == null || !EntryStates.Contains(state))
            issues.Add(InvalidEntry($"{path}.state", string.Join(" | ", EntryStates), state ?? Describe(entry["state"]), itemId));

        ValidateOriginIds(entry, path, itemId, issues);
        ValidateOriginCreationRefs(entry, path, itemId, issues);
        ReadIdentityArray(entry["parentItemIds"], $"{path}.parentItemIds", itemId, issues, requireNonEmpty: false);
        ValidateEntryState(entry, path, itemId, state, issues);
        ValidateTransitions(entry, path, itemId, transitionIds, issues);
        ValidateFirstTransitionShape(entry, path, itemId, issues);
        ValidateLastTransitionState(entry, path, itemId, state, issues);
    }

    private static void ValidateOriginIds(
        JsonObject entry,
        string path,
        string? itemId,
        List<ValidationIssue> issues)
    {
        var origins = ReadIdentityArray(
            entry["originMaterializationIds"],
            $"{path}.originMaterializationIds",
            itemId,
            issues,
            requireNonEmpty: true,
            requireUnique: false);
        if (origins.Count == 0)
            return;

        if (origins.Distinct(StringComparer.Ordinal).Count() != origins.Count)
        {
            issues.Add(Issue(
                $"{path}.originMaterializationIds",
                "mortal_item_identity_duplicate_origin_id",
                "Origin materialization IDs must be ordinal-unique.",
                "unique IDs",
                string.Join(", ", origins),
                itemId));
        }

        if (!origins.SequenceEqual(origins.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            issues.Add(Issue(
                $"{path}.originMaterializationIds",
                "mortal_item_identity_origin_ids_not_sorted",
                "Origin materialization IDs must use deterministic ordinal order.",
                "ordinal-sorted IDs",
                string.Join(", ", origins),
                itemId));
        }
    }

    private static void ValidateOriginCreationRefs(
        JsonObject entry,
        string path,
        string? itemId,
        List<ValidationIssue> issues)
    {
        var origins = ReadIdentityArray(
            entry["originCreationRefs"],
            $"{path}.originCreationRefs",
            itemId,
            issues,
            requireNonEmpty: true,
            requireUnique: false);
        if (origins.Count == 0)
            return;

        if (origins.Distinct(StringComparer.Ordinal).Count() != origins.Count)
        {
            issues.Add(Issue(
                $"{path}.originCreationRefs",
                "mortal_item_identity_duplicate_origin_creation_ref",
                "Origin creation references must be ordinal-unique.",
                "unique references",
                string.Join(", ", origins),
                itemId));
        }

        if (!origins.SequenceEqual(origins.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            issues.Add(Issue(
                $"{path}.originCreationRefs",
                "mortal_item_identity_origin_creation_refs_not_sorted",
                "Origin creation references must use deterministic ordinal order.",
                "ordinal-sorted references",
                string.Join(", ", origins),
                itemId));
        }
    }

    private static void ValidateEntryState(
        JsonObject entry,
        string path,
        string? itemId,
        string? state,
        List<ValidationIssue> issues)
    {
        var carrier = entry["currentCarrier"];
        var mergedIntoItemId = ReadExactIdentity(entry["mergedIntoItemId"]);
        if (state == "active")
        {
            if (carrier is not JsonObject carrierObject)
            {
                issues.Add(StateIssue(path, itemId, "active entry with one current carrier", Describe(carrier)));
            }
            else
            {
                ValidateCarrier(carrierObject, $"{path}.currentCarrier", itemId, issues);
            }

            if (entry["mergedIntoItemId"] != null)
                issues.Add(StateIssue(path, itemId, "active entry with mergedIntoItemId null", Describe(entry["mergedIntoItemId"])));
            return;
        }

        if (state is "merged" or "consumed" or "destroyed")
        {
            if (carrier != null)
                issues.Add(StateIssue(path, itemId, "retired entry with currentCarrier null", Describe(carrier)));
            if (state == "merged" && mergedIntoItemId == null)
                issues.Add(StateIssue(path, itemId, "merged entry with survivor item ID", Describe(entry["mergedIntoItemId"])));
            if (state is "consumed" or "destroyed" && entry["mergedIntoItemId"] != null)
                issues.Add(StateIssue(path, itemId, $"{state} entry with mergedIntoItemId null", Describe(entry["mergedIntoItemId"])));
        }
    }

    private static void ValidateCarrier(
        JsonObject carrier,
        string path,
        string? itemId,
        List<ValidationIssue> issues)
    {
        ValidateExactFields(carrier, CarrierFields, path, issues, itemId);
        var kind = ReadExactIdentity(carrier["kind"]);
        var ownerId = ReadExactIdentity(carrier["ownerId"]);
        var containerId = ReadExactIdentity(carrier["containerId"]);
        var valid = kind != null && CarrierKinds.Contains(kind) && ownerId != null;
        valid &= kind switch
        {
            "player_inventory" => string.Equals(ownerId, "player", StringComparison.Ordinal) && carrier["containerId"] == null,
            "npc_inventory" or "vehicle_inventory" => carrier["containerId"] == null,
            "location_storage" => containerId != null,
            _ => false
        };
        var containerPath = ReadIdentityArray(
            carrier["containerPath"],
            $"{path}.containerPath",
            itemId,
            issues,
            requireNonEmpty: false);
        valid &= containerPath.Distinct(StringComparer.Ordinal).Count() == containerPath.Count;
        if (valid)
            return;

        issues.Add(Issue(
            path,
            "mortal_item_identity_invalid_carrier",
            "Mortal item carrier coordinate is invalid.",
            "supported kind with exact owner/container requirements and an acyclic ID path",
            carrier.ToJsonString(),
            itemId));
    }

    private static void ValidateTransitions(
        JsonObject entry,
        string path,
        string? itemId,
        Dictionary<string, string> transitionIds,
        List<ValidationIssue> issues)
    {
        if (entry["transitions"] is not JsonArray transitions || transitions.Count == 0)
        {
            issues.Add(InvalidEntry(
                $"{path}.transitions",
                "non-empty transition array",
                Describe(entry["transitions"]),
                itemId));
            return;
        }

        var previousTurn = -1;
        for (var index = 0; index < transitions.Count; index++)
        {
            var transitionPath = $"{path}.transitions[{index}]";
            if (transitions[index] is not JsonObject transition)
            {
                issues.Add(InvalidEntry(transitionPath, "transition object", Describe(transitions[index]), itemId));
                continue;
            }

            ValidateExactFields(transition, TransitionFields, transitionPath, issues, itemId);
            var transitionId = ReadExactIdentity(transition["transitionId"]);
            if (transitionId == null)
                issues.Add(InvalidEntry($"{transitionPath}.transitionId", "non-empty transitionId", Describe(transition["transitionId"]), itemId));
            else if (!transitionIds.TryAdd(transitionId, transitionPath))
                issues.Add(Issue(
                    $"{transitionPath}.transitionId",
                    "mortal_item_identity_duplicate_transition_id",
                    "Mortal item transition IDs must be globally unique.",
                    $"unique transitionId; first at {transitionIds[transitionId]}",
                    transitionId,
                    itemId));

            var kind = ReadExactIdentity(transition["kind"]);
            if (kind == null || !TransitionKinds.Contains(kind))
                issues.Add(InvalidEntry($"{transitionPath}.kind", string.Join(" | ", TransitionKinds), kind ?? Describe(transition["kind"]), itemId));
            if (!TryGetInt(transition, "turn", out var turn) || turn < 0)
                issues.Add(InvalidEntry($"{transitionPath}.turn", "integer >= 0", Describe(transition["turn"]), itemId));
            else
            {
                if (turn < previousTurn)
                {
                    issues.Add(Issue(
                        $"{transitionPath}.turn",
                        "mortal_item_identity_transition_turn_order",
                        "Mortal item transition turns cannot move backward.",
                        $"integer >= {previousTurn}",
                        turn.ToString(),
                        itemId));
                }
                previousTurn = Math.Max(previousTurn, turn);
            }
            ReadIdentityArray(
                transition["sourceItemIds"],
                $"{transitionPath}.sourceItemIds",
                itemId,
                issues,
                requireNonEmpty: false,
                requireUnique: true);
            ValidateOptionalCarrier(transition["sourceCarrier"], $"{transitionPath}.sourceCarrier", itemId, issues);
            ValidateOptionalCarrier(transition["destinationCarrier"], $"{transitionPath}.destinationCarrier", itemId, issues);
            if (!TryGetInt(transition, "quantityBefore", out var quantityBefore) || quantityBefore < 0)
                issues.Add(InvalidEntry($"{transitionPath}.quantityBefore", "integer >= 0", Describe(transition["quantityBefore"]), itemId));
            if (!TryGetInt(transition, "quantityAfter", out var quantityAfter) || quantityAfter < 0)
                issues.Add(InvalidEntry($"{transitionPath}.quantityAfter", "integer >= 0", Describe(transition["quantityAfter"]), itemId));
            if (ReadExactIdentity(transition["authorityKind"]) == null)
                issues.Add(InvalidEntry($"{transitionPath}.authorityKind", "non-empty authority kind", Describe(transition["authorityKind"]), itemId));
            if (ReadExactIdentity(transition["authorityId"]) == null)
                issues.Add(InvalidEntry($"{transitionPath}.authorityId", "non-empty authority ID", Describe(transition["authorityId"]), itemId));

            if (quantityBefore >= 0 && quantityAfter >= 0 &&
                quantityBefore == quantityAfter &&
                JsonNode.DeepEquals(transition["sourceCarrier"], transition["destinationCarrier"]))
            {
                issues.Add(Issue(
                    transitionPath,
                    "mortal_item_identity_transition_noop",
                    "A client identity transition must change quantity, carrier, or both.",
                    "observable state change",
                    "same carrier and quantity",
                    itemId));
            }
        }
    }

    private static void ValidateLastTransitionState(
        JsonObject entry,
        string path,
        string? itemId,
        string? state,
        List<ValidationIssue> issues)
    {
        if (entry["transitions"] is not JsonArray { Count: > 0 } transitions ||
            transitions[^1] is not JsonObject lastTransition)
        {
            return;
        }

        var destination = lastTransition["destinationCarrier"];
        var valid = state == "active"
            ? JsonNode.DeepEquals(destination, entry["currentCarrier"])
            : state is "merged" or "consumed" or "destroyed" && destination == null;
        if (valid)
            return;

        issues.Add(Issue(
            $"{path}.transitions[{transitions.Count - 1}].destinationCarrier",
            "mortal_item_identity_transition_state_mismatch",
            "The last identity transition destination must match the entry's active or retired state.",
            Describe(entry["currentCarrier"]),
            Describe(destination),
            itemId));
    }

    private static void ValidateFirstTransitionShape(
        JsonObject entry,
        string path,
        string? itemId,
        List<ValidationIssue> issues)
    {
        if (entry["transitions"] is not JsonArray { Count: > 0 } transitions ||
            transitions[0] is not JsonObject firstTransition)
        {
            return;
        }

        var parentIds = ReadValidIdentitySet(entry["parentItemIds"]);
        var kind = ReadExactIdentity(firstTransition["kind"]);
        var valid = parentIds.Count == 0
            ? string.Equals(kind, "create", StringComparison.Ordinal) &&
              firstTransition["sourceCarrier"] == null &&
              firstTransition["destinationCarrier"] is JsonObject &&
              TryGetInt(firstTransition, "quantityBefore", out var quantityBefore) &&
              quantityBefore == 0 &&
              TryGetInt(firstTransition, "quantityAfter", out var quantityAfter) &&
              quantityAfter > 0
            : string.Equals(kind, "split", StringComparison.Ordinal) &&
              firstTransition["destinationCarrier"] is JsonObject &&
              parentIds.IsSubsetOf(ReadValidIdentitySet(firstTransition["sourceItemIds"]));
        if (valid)
            return;

        issues.Add(Issue(
            $"{path}.transitions[0]",
            "mortal_item_identity_transition_shape_mismatch",
            "The first identity transition must create a root or derive an exact split child.",
            parentIds.Count == 0
                ? "create with no source carrier, destination carrier, and quantity 0 -> positive"
                : "split referencing every direct parent and a destination carrier",
            firstTransition.ToJsonString(),
            itemId));
    }

    private static void ValidateOptionalCarrier(
        JsonNode? value,
        string path,
        string? itemId,
        List<ValidationIssue> issues)
    {
        if (value == null)
            return;
        if (value is JsonObject carrier)
        {
            ValidateCarrier(carrier, path, itemId, issues);
            return;
        }

        issues.Add(Issue(
            path,
            "mortal_item_identity_invalid_carrier",
            "Transition carrier must be an exact carrier object or null.",
            "carrier object or null",
            Describe(value),
            itemId));
    }

    private static List<string> ReadIdentityArray(
        JsonNode? node,
        string path,
        string? itemId,
        List<ValidationIssue> issues,
        bool requireNonEmpty,
        bool requireUnique = true)
    {
        var result = new List<string>();
        if (node is not JsonArray array)
        {
            issues.Add(InvalidEntry(path, "array of exact identity strings", Describe(node), itemId));
            return result;
        }

        var valid = true;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var child in array)
        {
            var value = ReadExactIdentity(child);
            if (value == null || requireUnique && !seen.Add(value))
            {
                valid = false;
                continue;
            }
            seen.Add(value);
            result.Add(value);
        }

        if (requireNonEmpty && result.Count == 0)
            valid = false;
        if (!valid)
            issues.Add(InvalidEntry(path, requireNonEmpty ? "non-empty unique exact identity array" : "unique exact identity array", array.ToJsonString(), itemId));
        return result;
    }

    private static JsonObject NormalizeEntry(JsonObject entry)
    {
        var result = new JsonObject();
        foreach (var property in EntryFieldOrder)
        {
            if (!entry.TryGetPropertyValue(property, out var value))
                continue;

            result[property] = property switch
            {
                "currentCarrier" when value is JsonObject carrier => NormalizeCarrier(carrier),
                "originMaterializationIds" or "originCreationRefs" or "parentItemIds"
                    when value is JsonArray identities =>
                    NormalizeIdentityArray(identities),
                "transitions" when value is JsonArray transitions => NormalizeTransitions(transitions),
                _ => value?.DeepClone()
            };
        }
        return result;
    }

    private static JsonArray NormalizeIdentityArray(JsonArray array)
    {
        if (array.Any(node => ReadExactIdentity(node) == null))
            return array.DeepClone().AsArray();
        return new JsonArray(
            array.Select(node => node!.GetValue<string>())
                .OrderBy(value => value, StringComparer.Ordinal)
                .Select(value => (JsonNode?)JsonValue.Create(value))
                .ToArray());
    }

    private static JsonObject NormalizeCarrier(JsonObject carrier)
    {
        var result = new JsonObject();
        foreach (var property in CarrierFieldOrder)
        {
            if (carrier.TryGetPropertyValue(property, out var value))
                result[property] = value?.DeepClone();
        }
        return result;
    }

    private static JsonArray NormalizeTransitions(JsonArray transitions) =>
        new(transitions.Select(transition =>
            transition is JsonObject obj
                ? (JsonNode?)NormalizeTransition(obj)
                : transition?.DeepClone()).ToArray());

    private static JsonObject NormalizeTransition(JsonObject transition)
    {
        var result = new JsonObject();
        foreach (var property in TransitionFieldOrder)
        {
            if (!transition.TryGetPropertyValue(property, out var value))
                continue;
            result[property] = property switch
            {
                "sourceCarrier" or "destinationCarrier" when value is JsonObject carrier =>
                    NormalizeCarrier(carrier),
                _ => value?.DeepClone()
            };
        }
        return result;
    }

    private static string ReadItemIdOrEmpty(JsonObject entry) =>
        ReadExactIdentity(entry["itemId"]) ?? string.Empty;

    private static void ValidateExactFields(
        JsonObject value,
        HashSet<string> allowedFields,
        string path,
        List<ValidationIssue> issues,
        string? itemId = null)
    {
        foreach (var property in value)
        {
            if (allowedFields.Contains(property.Key))
                continue;
            issues.Add(Issue(
                $"{path}.{property.Key}",
                "mortal_item_identity_unknown_field",
                "Client-owned Mortal item identity objects use a closed schema.",
                string.Join(" | ", allowedFields.OrderBy(field => field, StringComparer.Ordinal)),
                property.Key,
                itemId));
        }
    }

    private static void CollectDuplicateProperties(
        JsonElement value,
        string path,
        List<ValidationIssue> issues)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!seen.Add(property.Name))
                {
                    issues.Add(Issue(
                        $"{path}.{property.Name}",
                        "mortal_item_identity_duplicate_property",
                        "Client-owned Mortal item identity JSON forbids duplicate properties.",
                        "each property exactly once",
                        property.Name,
                        null));
                }
                CollectDuplicateProperties(property.Value, $"{path}.{property.Name}", issues);
            }
            return;
        }

        if (value.ValueKind != JsonValueKind.Array)
            return;
        var index = 0;
        foreach (var child in value.EnumerateArray())
            CollectDuplicateProperties(child, $"{path}[{index++}]", issues);
    }

    private static JsonNode? ConvertElement(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Object => ConvertObject(value),
            JsonValueKind.Array => new JsonArray(value.EnumerateArray().Select(ConvertElement).ToArray()),
            JsonValueKind.String => JsonValue.Create(value.GetString()),
            JsonValueKind.Number when value.TryGetInt32(out var integer) => JsonValue.Create(integer),
            JsonValueKind.Number when value.TryGetInt64(out var longInteger) => JsonValue.Create(longInteger),
            JsonValueKind.Number => JsonValue.Create(value.GetDouble()),
            JsonValueKind.True => JsonValue.Create(true),
            JsonValueKind.False => JsonValue.Create(false),
            JsonValueKind.Null => null,
            _ => null
        };

    private static JsonObject ConvertObject(JsonElement value)
    {
        var result = new JsonObject();
        foreach (var property in value.EnumerateObject())
        {
            result.Remove(property.Name);
            result[property.Name] = ConvertElement(property.Value);
        }
        return result;
    }

    private static bool TryGetInt(JsonObject value, string property, out int result)
    {
        result = default;
        return value[property] is JsonValue node && node.TryGetValue(out result);
    }

    private static string? ReadExactIdentity(JsonNode? node)
    {
        if (node is not JsonValue value || !value.TryGetValue<string>(out var text) ||
            !MortalItemIdentityRules.IsExactIdentity(text))
        {
            return null;
        }
        return text;
    }

    private static void RequireExactIdentity(string value, string parameterName)
    {
        if (!MortalItemIdentityRules.IsExactIdentity(value))
        {
            throw new ArgumentException("Identity must be non-empty and untrimmed.", parameterName);
        }
    }

    private static string RequireExactIdentityValue(JsonObject value, string property)
    {
        var result = ReadExactIdentity(value[property]);
        if (result == null)
            throw new InvalidOperationException($"Required exact identity field '{property}' is missing.");
        return result;
    }

    private static string NewId(string prefix) =>
        prefix + Guid.NewGuid().ToString("N");

    private static MortalItemIdentityParseResult InvalidRoot(string actual)
    {
        var issue = Issue(
            StatePath,
            "mortal_item_identity_invalid_index",
            "Mortal item identity index root is invalid.",
            "object with schemaVersion 1 and entries array",
            actual,
            null);
        return new MortalItemIdentityParseResult(
            CreateEmptyRoot(),
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            new[] { issue });
    }

    private static ValidationIssue InvalidEntry(
        string path,
        string expected,
        string actual,
        string? itemId) =>
        Issue(
            path,
            "mortal_item_identity_invalid_entry",
            "Mortal item identity-index entry is invalid.",
            expected,
            actual,
            itemId);

    private static ValidationIssue StateIssue(
        string path,
        string? itemId,
        string expected,
        string actual) =>
        Issue(
            path,
            "mortal_item_identity_state_mismatch",
            "Mortal item identity entry state contradicts its carrier or retirement evidence.",
            expected,
            actual,
            itemId);

    private static ValidationIssue Issue(
        string path,
        string code,
        string message,
        string? expected,
        string? actual,
        string? itemId) =>
        new(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            actor: $"mortal_item:existing:{itemId ?? "unknown"}",
            section: "MortalItemIdentity",
            expected: expected,
            actual: actual,
            repairHint: "Restore client-owned item identity state from the validated before-image; never ask the GM to author the index.");

    private static string Describe(JsonNode? node) =>
        node?.ToJsonString() ?? "missing or null";
}
