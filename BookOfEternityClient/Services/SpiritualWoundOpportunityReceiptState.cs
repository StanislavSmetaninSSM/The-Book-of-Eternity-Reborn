using System.Text.Json;
using System.Text.Json.Nodes;
using static BookOfEternityClient.Services.SpiritualWoundStateJson;

namespace BookOfEternityClient.Services;

/// <summary>
/// Validates immutable spiritual opportunity history without granting source or publication authority.
/// </summary>
internal sealed class SpiritualWoundOpportunityReceiptState
{
    /// <summary>
    /// Identifies the private client-owned accepted spiritual opportunity ledger.
    /// </summary>
    internal const string StatePath = "game_state/wounds/spiritual_wound_opportunity_receipts.json";

    private const int MaximumRows = 20_000;
    private readonly JsonObject _root;

    /// <summary>
    /// Retains a detached private copy of a validated complete ledger.
    /// </summary>
    /// <param name="root">
    /// Validated receipt root.
    /// </param>
    private SpiritualWoundOpportunityReceiptState(JsonObject root) => _root = root.DeepClone().AsObject();

    /// <summary>
    /// Checks closed shapes, lifecycle references and the source-to-decision bijection.
    /// Original snapshots and matching wound publication must be proved by their owning authorities.
    /// </summary>
    /// <param name="json">
    /// Complete serialized ledger; missing or malformed content fails closed.
    /// </param>
    /// <param name="path">
    /// Nonempty owning receipt path used in diagnostics.
    /// </param>
    /// <returns>
    /// Detached validated state or diagnostics without partial state.
    /// </returns>
    internal static SpiritualWoundReceiptParseResult Parse(string? json, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            var root = SpiritualWoundStateJson.Parse(json);
            Closed(root, "schemaVersion nextInstanceOrdinal nextClosureOrdinal nextSourceOrdinal nextDecisionOrdinal instances closures sources decisions");
            var lifecycle = new JsonObject();
            foreach (var field in new[] { "schemaVersion", "nextInstanceOrdinal", "nextClosureOrdinal", "instances", "closures" })
                lifecycle[field] = root[field]?.DeepClone();
            Require(SpiritualWoundConflictInstanceState.Parse(lifecycle.ToJsonString(), path).IsValid,
                "Invalid receipt lifecycle projection.");
            var sources = Rows(root, "sources", "nextSourceOrdinal");
            var decisions = Rows(root, "decisions", "nextDecisionOrdinal");
            var instances = root["instances"]!.AsArray().ToDictionary(
                row => Text(row!["instanceId"]), row => row!.AsObject(), StringComparer.Ordinal);
            var closures = root["closures"]!.AsArray().ToDictionary(
                row => Text(row!["instanceId"]), row => Integer(row!["terminalTurn"]), StringComparer.Ordinal);
            var sourceById = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
            var coordinates = new HashSet<(string Instance, string Exchange, string Side)>();
            var exchanges = new Dictionary<(string Instance, string Exchange), (string Session, string Request, long Ordinal)>();
            var instanceCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var row in sources)
            {
                Closed(row, "ordinal instanceId instanceSourceOrdinal witness");
                var instanceId = Text(row["instanceId"]);
                Require(instances.TryGetValue(instanceId, out var instance), "Unknown source instance.");
                var witness = Object(row["witness"]);
                Require(SpiritualWoundSourceWitness.Validate(witness).MaximumSeverityRank > 0 ||
                    witness["guaranteedSeverityRank"] is not null,
                    "Only positive or guaranteed sources have accepted decisions.");
                var evidence = Object(witness["turnEvidence"]);
                Require(Text(witness["conflictId"]) == Text(instance!["displayConflictId"]) &&
                    Text(evidence["realm"]) == Text(instance["realm"]), "Source belongs to another conflict.");
                var turn = Integer(evidence["turn"], Integer(instance["startTurn"]));
                Require(!closures.TryGetValue(instanceId, out var terminalTurn) || turn <= terminalTurn,
                    "Source follows instance closure.");
                var next = instanceCounts.GetValueOrDefault(instanceId) + 1;
                Integer(row["instanceSourceOrdinal"], next, next);
                instanceCounts[instanceId] = next;
                Require(sourceById.TryAdd(Text(witness["sourceId"]), row), "Duplicate source identity.");
                Require(coordinates.Add((instanceId, Text(witness["exchangeId"]), Text(witness["affectedSide"]))),
                    "An exchange-side coordinate cannot be allocated twice.");
                var exchangeKey = (instanceId, Text(witness["exchangeId"]));
                var request = OriginalRequest(witness);
                var exchangeIdentity = (request.Session, request.Request, Integer(witness["exchangeOrdinal"]));
                Require(!exchanges.TryGetValue(exchangeKey, out var previousExchange) || previousExchange == exchangeIdentity,
                    "An exchange cannot be reused by another original request or position.");
                exchanges[exchangeKey] = exchangeIdentity;
            }
            Require(sources.Length == decisions.Length, "Every source requires one accepted decision.");
            ValidateTurnCoherence(sources);
            var decided = new HashSet<string>(StringComparer.Ordinal);
            var decisionIds = new HashSet<string>(StringComparer.Ordinal);
            var opportunityIds = new HashSet<string>(StringComparer.Ordinal);
            var transitionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in decisions)
                ValidateDecision(row, sourceById, decided, decisionIds, opportunityIds, transitionIds);
            ValidateDecisionWaves(decisions);
            return new(true, new(root), Array.Empty<ValidationIssue>());
        }
        catch (Exception error) when (error is JsonException or FormatException or InvalidOperationException or OverflowException)
        {
            return new(false, null, Array.AsReadOnly(new[]
            {
                new ValidationIssue(path, IssueSeverity.Error, "Invalid spiritual wound receipt history.",
                    code: "spiritual_wound_receipt_invalid_state", section: "AcceptedTurnWoundMaterialization")
            }));
        }
    }

    /// <summary>
    /// Serializes a validated ledger without exposing its mutable internal tree.
    /// </summary>
    /// <param name="state">
    /// Non-null parsed ledger.
    /// </param>
    /// <returns>
    /// Deterministic JSON text.
    /// </returns>
    internal static string SerializeCanonical(SpiritualWoundOpportunityReceiptState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Canonical(state._root);
    }

    /// <summary>
    /// Checks whether one accepted non-retrauma decision records the exact original wound creation.
    /// This historical agreement does not grant current opportunity or publication authority.
    /// </summary>
    /// <param name="woundId">
    /// Wound identity from the independently validated signed baseline, including healed identities.
    /// </param>
    /// <param name="opportunityRef">
    /// Immutable original opportunity reference, retained by the wound or derived from its creation event.
    /// </param>
    /// <param name="creation">
    /// Matching first creation transition from that baseline's validated history.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when exactly one decision covers the creation; otherwise, <see langword="false"/>.
    /// </returns>
    internal bool CoversOriginalCreation(string woundId, string opportunityRef, WoundHistoryTransition creation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(woundId);
        ArgumentException.ThrowIfNullOrWhiteSpace(opportunityRef);
        ArgumentNullException.ThrowIfNull(creation);
        if (creation.Kind != "create" || creation.WoundTransitionOrdinal != 1 || creation.WoundId != woundId)
            return false;
        return _root["decisions"]!.AsArray().Count(node =>
        {
            var row = node!.AsObject();
            if (Text(row["decision"]) != "materialize" || Text(row["woundId"]) != woundId ||
                Text(row["transitionId"]) != creation.TransitionId || Text(row["opportunityRef"]) != opportunityRef)
                return false;
            var witness = Object(row["sourceWitness"]);
            var evidence = Object(witness["turnEvidence"]);
            var key = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
            {
                "book_of_eternity.spiritual_wound_source_event", "1",
                Text(evidence["sessionId"]), Text(evidence["requestId"]), Text(evidence["snapshotToken"]),
                Text(witness["conflictId"]), Text(witness["exchangeId"]), Text(witness["affectedSide"])
            })[7..];
            return opportunityRef == "spiritual_wound_" + key &&
                creation.EventRef == "spiritual_wound_event_" + key &&
                witness["retraumaWoundId"] is null;
        }) == 1;
    }

    /// <summary>
    /// Describes the first receipt claiming a new wound for one side of an instance.
    /// This detached claim is not current wound or source authority.
    /// </summary>
    /// <param name="WoundId">
    /// Recorded identity to verify against the current owned wound view.
    /// </param>
    /// <param name="CreateTransitionId">
    /// Recorded first transition to verify as an actual creation in wound history.
    /// </param>
    internal sealed record RecordedConflictSideWound(string WoundId, string CreateTransitionId);

    /// <summary>
    /// Returns a detached historical claim or conflicts between accepted same-side rows.
    /// </summary>
    /// <param name="Wound">
    /// First recorded wound, or <see langword="null"/> when none exists or history conflicts.
    /// </param>
    /// <param name="Issues">
    /// Conflicting same-side wound identities; empty for a consistent or absent record.
    /// </param>
    internal sealed record RecordedConflictSideLookup(RecordedConflictSideWound? Wound,
        IReadOnlyList<ValidationIssue> Issues);

    /// <summary>
    /// Finds the first non-retrauma materialization for one durable conflict side.
    /// All later non-retrauma materializations must name that same wound.
    /// </summary>
    /// <param name="instanceId">
    /// Exact instance identity independently resolved from signed original evidence.
    /// </param>
    /// <param name="side">
    /// Exact affected side, either player or opposition.
    /// </param>
    /// <returns>
    /// A detached recorded identity, no record, or a conflict that requires rejection.
    /// </returns>
    internal RecordedConflictSideLookup FindRecordedConflictSideWound(string instanceId, string side)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        if (side is not ("player" or "opposition"))
            throw new ArgumentException("An exact spiritual conflict side is required.", nameof(side));
        RecordedConflictSideWound? recorded = null;
        foreach (var node in _root["decisions"]!.AsArray())
        {
            var row = node!.AsObject();
            if (Text(row["instanceId"]) != instanceId || Text(row["decision"]) != "materialize")
                continue;
            var witness = Object(row["sourceWitness"]);
            if (Text(witness["affectedSide"]) != side || witness["retraumaWoundId"] is not null)
                continue;
            var woundId = Text(row["woundId"]);
            if (recorded is null)
                recorded = new(woundId, Text(row["transitionId"]));
            else if (recorded.WoundId != woundId)
                return new(null, [new ValidationIssue(StatePath, IssueSeverity.Error,
                    "The same conflict side has conflicting new wound identities.",
                    code: "spiritual_wound_conflict_side_history_invalid",
                    section: "AcceptedTurnWoundMaterialization")]);
        }
        return new(recorded, []);
    }

    /// <summary>
    /// Preserves every accepted row and prevents new sources from reopening a closed instance.
    /// This pure comparison never emits wound or resource commands.
    /// </summary>
    /// <param name="before">
    /// Previously parsed complete ledger.
    /// </param>
    /// <param name="candidate">
    /// Independently parsed candidate ledger.
    /// </param>
    /// <returns>
    /// Detached appended or exact-replay state; conflict exposes no after-state.
    /// </returns>
    internal static SpiritualWoundReceiptAppendResult PlanAppend(
        SpiritualWoundOpportunityReceiptState before, SpiritualWoundOpportunityReceiptState candidate)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(candidate);
        foreach (var field in new[] { "instances", "closures", "sources", "decisions" })
        {
            var prior = before._root[field]!.AsArray();
            var next = candidate._root[field]!.AsArray();
            if (prior.Count > next.Count || prior.Where((row, index) => !JsonNode.DeepEquals(row, next[index])).Any())
                return new("conflict", null);
        }
        var closed = before._root["closures"]!.AsArray()
            .Select(row => Text(row!["instanceId"])).ToHashSet(StringComparer.Ordinal);
        var acceptedTurns = before._root["sources"]!.AsArray()
            .Select(row => OriginalRequest(Object(row!["witness"]))).ToHashSet();
        if (candidate._root["sources"]!.AsArray().Skip(before._root["sources"]!.AsArray().Count)
            .Any(row => closed.Contains(Text(row!["instanceId"])) ||
                acceptedTurns.Contains(OriginalRequest(Object(row["witness"])))))
            return new("conflict", null);
        return new(JsonNode.DeepEquals(before._root, candidate._root) ? "exact_replay" : "appended",
            new(candidate._root));
    }

    /// <summary>
    /// Joins one closed decision to its exact source and retained original-turn binding.
    /// </summary>
    /// <param name="row">
    /// Ordered decision row.
    /// </param>
    /// <param name="sources">
    /// Validated source rows indexed by exact source ID.
    /// </param>
    /// <param name="decided">
    /// Source IDs already assigned a decision.
    /// </param>
    /// <param name="decisionIds">
    /// Decision IDs already encountered.
    /// </param>
    /// <param name="opportunityIds">
    /// Opportunity references already encountered.
    /// </param>
    /// <param name="transitionIds">
    /// Materialization transition IDs already encountered.
    /// </param>
    private static void ValidateDecision(JsonObject row, IReadOnlyDictionary<string, JsonObject> sources,
        HashSet<string> decided, HashSet<string> decisionIds, HashSet<string> opportunityIds,
        HashSet<string> transitionIds)
    {
        var decision = Text(row["decision"]);
        Closed(row, decision == "guarantee_satisfied"
            ? "ordinal decisionId instanceId sourceId opportunityRef sourceWitness binding decision selectedSeverityRank satisfiedSeverityRank woundId transitionId sourceFingerprint decisionFingerprint"
            : "ordinal decisionId instanceId sourceId opportunityRef sourceWitness binding decision selectedSeverityRank woundId transitionId sourceFingerprint decisionFingerprint");
        Require(decisionIds.Add(Text(row["decisionId"])), "Duplicate decision ID.");
        Require(opportunityIds.Add(Text(row["opportunityRef"])), "Duplicate opportunity reference.");
        var sourceId = Text(row["sourceId"]);
        Require(sources.TryGetValue(sourceId, out var source) && decided.Add(sourceId),
            "Unknown or already decided source.");
        var witness = Object(source!["witness"]);
        Integer(row["ordinal"], Integer(source["ordinal"]), Integer(source["ordinal"]));
        Require(Text(row["instanceId"]) == Text(source["instanceId"]) &&
            JsonNode.DeepEquals(row["sourceWitness"], witness) &&
            Fingerprint(row["sourceFingerprint"]) == Fingerprint(witness["sourceFingerprint"]),
            "Decision source evidence differs.");
        var binding = Object(row["binding"]);
        Closed(binding, "sessionId requestId snapshotToken turn continuationGeneration waveOrdinal sourceOrdinal");
        var evidence = Object(witness["turnEvidence"]);
        foreach (var field in new[] { "sessionId", "requestId", "snapshotToken" })
            Require(Text(binding[field]) == Text(evidence[field]), "Decision original-turn identity differs.");
        Integer(binding["turn"], Integer(evidence["turn"]), Integer(evidence["turn"]));
        var count = Integer(evidence["bounds"]!["exchangeCount"]);
        Integer(binding["continuationGeneration"], 1, count);
        Integer(binding["waveOrdinal"], 0, count - 1);
        Integer(binding["sourceOrdinal"], Integer(witness["sourceOrdinal"]), Integer(witness["sourceOrdinal"]));
        if (decision == "none")
            Require(row["selectedSeverityRank"] is null && row["woundId"] is null && row["transitionId"] is null &&
                witness["guaranteedSeverityRank"] is null, "Decline carries materialization or violates a guarantee.");
        else if (decision == "guarantee_satisfied")
        {
            var guarantee = Integer(witness["guaranteedSeverityRank"], 1, 4);
            Require(witness["retraumaWoundId"] is null &&
                row["selectedSeverityRank"] is null && row["transitionId"] is null &&
                Integer(row["satisfiedSeverityRank"], guarantee, 4) >= guarantee,
                "An already satisfied guarantee must retain its exact conflict wound without a transition.");
            Text(row["woundId"]);
        }
        else
        {
            Require(decision == "materialize", "Unknown decision kind.");
            var minimum = witness["guaranteedSeverityRank"] is null ? 1 : Integer(witness["guaranteedSeverityRank"], 1, 4);
            var rank = Integer(row["selectedSeverityRank"], minimum, Integer(witness["maximumSeverityRank"], 1, 4));
            Require(witness["guaranteedSeverityRank"] is null || rank == minimum,
                "Materialization differs from the exact guaranteed severity.");
            Text(row["woundId"]);
            if (witness["retraumaWoundId"] is not null)
            {
                var retained = WoundMaterializationContract.Parse(
                    System.Text.Encoding.UTF8.GetString(DecodeJsonObjectBytes(witness["retraumaWoundJsonBase64"])),
                    "retainedReceipt.retraumaWound");
                Require(retained.IsValid && Text(row["woundId"]) == Text(witness["retraumaWoundId"]) &&
                    rank > retained.Wound!.Severity.Rank, "Re-trauma must worsen its exact retained target.");
            }
            Require(transitionIds.Add(Text(row["transitionId"])), "Duplicate wound transition ID.");
        }
        Require(Fingerprint(row["decisionFingerprint"]) == Hash(row, "decision", "decisionFingerprint"),
            "Decision digest differs.");
    }

    /// <summary>
    /// Requires one shared wave per exchange and causal wave progression within each original request.
    /// </summary>
    /// <param name="decisions">
    /// Validated decisions in accepted source order.
    /// </param>
    private static void ValidateDecisionWaves(IEnumerable<JsonObject> decisions)
    {
        (string Session, string Request)? previousRequest = null;
        var previousExchange = -1L;
        var previousWave = -1L;
        var previousGeneration = 0L;
        foreach (var row in decisions)
        {
            var witness = Object(row["sourceWitness"]);
            var request = OriginalRequest(witness);
            var exchange = Integer(witness["exchangeOrdinal"]);
            var wave = Integer(row["binding"]!["waveOrdinal"]);
            var generation = Integer(row["binding"]!["continuationGeneration"]);
            if (request == previousRequest)
                Require(exchange == previousExchange
                    ? wave == previousWave && generation == previousGeneration
                    : wave > previousWave && generation > previousGeneration,
                    "Decision bindings disagree with one causal wave per eligible exchange.");
            previousRequest = request;
            previousExchange = exchange;
            previousWave = wave;
            previousGeneration = generation;
        }
    }

    /// <summary>
    /// Compares retained original-turn contexts and prevents duplicate exchange claims or reordered sources.
    /// </summary>
    /// <param name="sources">
    /// Validated rows containing instanceId and witness, in retained source order.
    /// Pending projections may include zero-ceiling sources.
    /// </param>
    internal static void ValidateTurnCoherence(IEnumerable<JsonObject> sources)
    {
        var ordered = sources.ToArray();
        var completed = new HashSet<(string Session, string Request)>();
        (string Session, string Request)? lastRequest = null;
        var lastTurns = new Dictionary<(string Instance, string Session), long>();
        foreach (var row in ordered)
        {
            var witness = Object(row["witness"]);
            var request = OriginalRequest(witness);
            if (lastRequest != request)
            {
                Require(completed.Add(request), "An accepted original request cannot reopen later in history.");
                lastRequest = request;
            }
            var key = (Text(row["instanceId"]), request.Session);
            var number = Integer(witness["turnEvidence"]!["turn"]);
            Require(!lastTurns.TryGetValue(key, out var previous) || number >= previous,
                "Instance history cannot move backward within a session.");
            lastTurns[key] = number;
        }
        var turns = ordered.GroupBy(row => OriginalRequest(Object(row["witness"])));
        foreach (var turn in turns)
        {
            JsonObject? original = null;
            string? instance = null;
            JsonObject? previousWitness = null;
            var previousSource = -1L;
            var previousSlot = -1L;
            var previousExchange = -1L;
            var claimed = new HashSet<long>();
            foreach (var row in turn)
            {
                var witness = Object(row["witness"]);
                var evidence = Object(witness["turnEvidence"]);
                var instanceId = Text(row["instanceId"]);
                if (instance is null) instance = instanceId;
                else Require(instanceId == instance, "One original packet cannot span conflict instances.");
                var context = evidence.DeepClone().AsObject();
                context.Remove("diceClaims");
                if (original is null) original = context;
                else Require(JsonNode.DeepEquals(original, context), "Retained original-turn contexts disagree.");
                var source = Integer(witness["sourceOrdinal"]);
                var exchange = Integer(witness["exchangeOrdinal"]);
                var slot = checked(2 * exchange + (Text(witness["affectedSide"]) == "opposition" ? 1 : 0));
                Require(slot > previousSlot && source - previousSource <= slot - previousSlot,
                    "Source positions exceed available causal side slots.");
                Require(source > previousSource && exchange >= previousExchange,
                    "Accepted sources must retain their original causal order.");
                if (exchange == previousExchange)
                {
                    Require(Text(witness["exchangeId"]) == Text(previousWitness!["exchangeId"]) &&
                        source == previousSource + 1 &&
                        Text(previousWitness["affectedSide"]) == "player" &&
                        Text(witness["affectedSide"]) == "opposition" &&
                        Text(witness["exchangeJsonBase64"]) == Text(previousWitness["exchangeJsonBase64"]) &&
                        JsonNode.DeepEquals(evidence["diceClaims"], previousWitness["turnEvidence"]!["diceClaims"]),
                        "Both sides must retain the same exchange and claims.");
                }
                else
                    foreach (var claim in evidence["diceClaims"]!.AsArray())
                        Require(claimed.Add(Integer(claim!["sourceIndex"])),
                            "An original die cannot belong to different exchanges.");
                previousSource = source;
                previousSlot = slot;
                previousExchange = exchange;
                previousWitness = witness;
            }
        }
    }

    /// <summary>
    /// Reads the retained original request identity, independent of continuation generation.
    /// </summary>
    /// <param name="witness">
    /// Fully validated source witness.
    /// </param>
    /// <returns>
    /// Exact session and original request IDs.
    /// </returns>
    private static (string Session, string Request) OriginalRequest(JsonObject witness) =>
        (Text(witness["turnEvidence"]!["sessionId"]), Text(witness["turnEvidence"]!["requestId"]));

    /// <summary>
    /// Reads a bounded ordered collection and checks its contiguous ordinal counter.
    /// </summary>
    /// <param name="root">
    /// Closed complete ledger root.
    /// </param>
    /// <param name="field">
    /// Exact collection field.
    /// </param>
    /// <param name="counter">
    /// Matching next-ordinal field.
    /// </param>
    /// <returns>
    /// Ordered row objects from the private parse tree.
    /// </returns>
    private static JsonObject[] Rows(JsonObject root, string field, string counter)
    {
        if (root[field] is not JsonArray array || array.Count > MaximumRows)
            throw new FormatException("Bounded row array required.");
        Integer(root[counter], array.Count + 1, array.Count + 1);
        var rows = new JsonObject[array.Count];
        for (var index = 0; index < rows.Length; index++)
        {
            rows[index] = Object(array[index]);
            Integer(rows[index]["ordinal"], index + 1, index + 1);
        }
        return rows;
    }

    /// <summary>
    /// Requires a present JSON object without coercion.
    /// </summary>
    /// <param name="node">
    /// Required object node.
    /// </param>
    /// <returns>
    /// The object, or a format failure for any other kind.
    /// </returns>
    private static JsonObject Object(JsonNode? node) => node as JsonObject ?? throw new FormatException("Object required.");
}

/// <summary>
/// Carries validated comparison history or diagnostics without partial state.
/// </summary>
/// <param name="IsValid">
/// <see langword="true"/> when the complete receipt contract is internally consistent; otherwise <see langword="false"/>.
/// </param>
/// <param name="State">
/// Detached validated ledger, or <see langword="null"/> on failure.
/// </param>
/// <param name="Issues">
/// Read-only errors, empty on success.
/// </param>
internal sealed record SpiritualWoundReceiptParseResult(
    bool IsValid, SpiritualWoundOpportunityReceiptState? State, IReadOnlyList<ValidationIssue> Issues);

/// <summary>
/// Describes a pure append comparison without writing canonical state.
/// </summary>
/// <param name="Disposition">
/// One of appended, exact_replay or conflict.
/// </param>
/// <param name="After">
/// Detached candidate ledger, or <see langword="null"/> for conflict.
/// </param>
internal sealed record SpiritualWoundReceiptAppendResult(
    string Disposition, SpiritualWoundOpportunityReceiptState? After);
