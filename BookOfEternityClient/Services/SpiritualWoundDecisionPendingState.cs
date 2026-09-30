using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using static BookOfEternityClient.Services.SpiritualWoundStateJson;

namespace BookOfEternityClient.Services;

/// <summary>
/// Checks a detached unfinished spiritual decision packet without authenticating its origin or authorizing writes.
/// </summary>
internal sealed class SpiritualWoundDecisionPendingState
{
    /// <summary>
    /// Identifies the private client-owned unfinished spiritual decision root.
    /// </summary>
    internal const string StatePath = "game_state/control/pending_spiritual_wound_decisions.json";

    private readonly JsonObject _root;
    private readonly HashSet<string> _registeredPaths;

    /// <summary>
    /// Retains a private copy of a structurally validated packet root.
    /// </summary>
    /// <param name="root">
    /// Validated complete root, including an explicitly empty pending value.
    /// </param>
    /// <param name="registeredPaths">
    /// Trusted original capture inventory retained as detached comparison context.
    /// </param>
    private SpiritualWoundDecisionPendingState(JsonObject root, IEnumerable<string> registeredPaths)
    {
        _root = root.DeepClone().AsObject();
        _registeredPaths = registeredPaths.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Checks closed comparison evidence, source order, staged decisions and exact retained bytes.
    /// Ordinary response/proposal admission and source reconstruction remain the original owner's responsibility.
    /// </summary>
    /// <param name="json">
    /// Serialized root; absent or malformed text fails closed.
    /// </param>
    /// <param name="path">
    /// Nonempty owning path used for diagnostics.
    /// </param>
    /// <param name="registeredPaths">
    /// Trusted registered capture inventory supplied by the owner, including registered dynamic paths.
    /// </param>
    /// <returns>
    /// Detached structurally valid state or diagnostics without partial state.
    /// </returns>
    internal static SpiritualWoundPendingParseResult Parse(string? json, string path, IEnumerable<string> registeredPaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(registeredPaths);
        var paths = registeredPaths.ToHashSet(StringComparer.Ordinal);
        try
        {
            var root = SpiritualWoundStateJson.Parse(json);
            Closed(root, "schemaVersion pending");
            Integer(root["schemaVersion"], 1, 1);
            if (root["pending"] is not null) ValidatePacket(Object(root["pending"]), paths);
            return new(true, new(root, paths), Array.Empty<ValidationIssue>());
        }
        catch (Exception error) when (error is JsonException or FormatException or InvalidOperationException or OverflowException)
        {
            return new(false, null, Array.AsReadOnly(new[]
            {
                new ValidationIssue(path, IssueSeverity.Error, "Invalid unfinished spiritual wound decisions.",
                    code: "spiritual_wound_pending_invalid_state", section: "AcceptedTurnWoundMaterialization")
            }));
        }
    }

    /// <summary>
    /// Serializes the private detached root without changing retained payload bytes.
    /// </summary>
    /// <param name="state">
    /// Non-null parsed comparison state.
    /// </param>
    /// <returns>
    /// Deterministic wrapper JSON.
    /// </returns>
    internal static string SerializeCanonical(SpiritualWoundDecisionPendingState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Canonical(state._root);
    }

    /// <summary>
    /// Gets the fingerprint sealed by a nonempty, structurally parsed C1 packet.
    /// </summary>
    internal string PacketFingerprint => _root["pending"] is JsonObject packet
        ? Fingerprint(packet["packetFingerprint"])
        : throw new InvalidOperationException("An empty pending root has no packet fingerprint.");

    /// <summary>
    /// Gets whether this parsed root contains an unfinished decision packet.
    /// </summary>
    internal bool HasPending => _root["pending"] is JsonObject;

    /// <summary>
    /// Compares unfinished replacements while preserving original evidence and every staged prefix.
    /// A successful comparison is not permission to persist, consume or publish either state.
    /// </summary>
    /// <param name="before">
    /// Previously parsed unfinished state.
    /// </param>
    /// <param name="candidate">
    /// Independently parsed replacement; the owner must additionally validate draft and candidate-image changes.
    /// </param>
    /// <returns>
    /// Detached started, advanced or exact-replay state; conflict exposes no after-state.
    /// </returns>
    internal static SpiritualWoundPendingAdvanceResult PlanAdvance(
        SpiritualWoundDecisionPendingState before, SpiritualWoundDecisionPendingState candidate)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(candidate);
        if (!before._registeredPaths.SetEquals(candidate._registeredPaths)) return new("conflict", null);
        if (JsonNode.DeepEquals(before._root, candidate._root))
            return new("exact_replay", new(candidate._root, candidate._registeredPaths));
        if (candidate._root["pending"] is not JsonObject next) return new("conflict", null);
        if (before._root["pending"] is not JsonObject prior)
            return Integer(next["continuationGeneration"]) == 1 && next["stagedDecisions"]!.AsArray().Count == 0
                ? new("started", new(candidate._root, candidate._registeredPaths)) : new("conflict", null);
        foreach (var field in new[] { "sessionId", "requestId", "snapshotToken", "turn", "realm",
            "conflictInstanceRef", "originalSnapshotFingerprint", "bounds", "beforeImages" })
            if (!JsonNode.DeepEquals(prior[field], next[field])) return new("conflict", null);
        foreach (var field in new[] { "sources", "stagedDecisions", "diceClaims" })
        {
            var oldRows = prior[field]!.AsArray();
            var newRows = next[field]!.AsArray();
            if (oldRows.Count > newRows.Count || oldRows.Where((row, index) => !JsonNode.DeepEquals(row, newRows[index])).Any())
                return new("conflict", null);
        }
        foreach (var field in new[] { "exchangeOrdinal", "waveOrdinal", "nextSourceOrdinal" })
            if (Integer(next["cursor"]![field]) < Integer(prior["cursor"]![field])) return new("conflict", null);
        var oldSourceCount = prior["sources"]!.AsArray().Count;
        var oldFrontier = Integer(prior["cursor"]!["exchangeOrdinal"]);
        var newSources = next["sources"]!.AsArray().Skip(oldSourceCount).Select(Object).ToArray();
        var newClaims = next["diceClaims"]!.AsArray().Skip(prior["diceClaims"]!.AsArray().Count);
        if (newSources.Any(source => Integer(source["exchangeOrdinal"]) < oldFrontier) ||
            newClaims.Any(claim => Integer(claim!["exchangeOrdinal"]) < oldFrontier))
            return new("conflict", null);
        var newDecisions = next["stagedDecisions"]!.AsArray().Skip(prior["stagedDecisions"]!.AsArray().Count);
        if (newDecisions.Any(decision => Integer(decision!["sourceOrdinal"]) >= oldSourceCount ||
            Integer(decision["waveOrdinal"]) != Integer(prior["cursor"]!["waveOrdinal"])))
            return new("conflict", null);
        var newEligibleExchanges = newSources.Where(source =>
                Integer(source["maximumSeverityRank"]) > 0 || source["guaranteedSeverityRank"] is not null)
            .Select(source => Integer(source["exchangeOrdinal"])).Distinct().Count();
        var oldGeneration = Integer(prior["continuationGeneration"]);
        var generation = Integer(next["continuationGeneration"]);
        if (newEligibleExchanges > 1 || generation != oldGeneration + newEligibleExchanges)
            return new("conflict", null);
        if (generation == oldGeneration &&
            Integer(next["cursor"]!["waveOrdinal"]) != Integer(prior["cursor"]!["waveOrdinal"]) &&
            Integer(next["cursor"]!["waveOrdinal"]) != Integer(next["bounds"]!["exchangeCount"]))
            return new("conflict", null);
        if (generation > oldGeneration &&
            (Integer(next["cursor"]!["waveOrdinal"]) <= Integer(prior["cursor"]!["waveOrdinal"]) ||
             Integer(next["cursor"]!["nextSourceOrdinal"]) < prior["sources"]!.AsArray().Count))
            return new("conflict", null);
        if (generation == oldGeneration && JsonNode.DeepEquals(prior["cursor"], next["cursor"]) &&
            prior["sources"]!.AsArray().Count == next["sources"]!.AsArray().Count &&
            prior["stagedDecisions"]!.AsArray().Count == next["stagedDecisions"]!.AsArray().Count)
            return new("conflict", null);
        return new("advanced", new(candidate._root, candidate._registeredPaths));
    }

    /// <summary>
    /// Computes a comparison digest for the serialized retained prefix, never an execution seal.
    /// </summary>
    /// <param name="packet">
    /// Packet whose five prefix fields are copied without mutation.
    /// </param>
    /// <returns>
    /// Domain-separated canonical comparison fingerprint.
    /// </returns>
    internal static string ComputePrefixFingerprint(JsonObject packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        var prefix = new JsonObject();
        foreach (var field in new[] { "cursor", "sources", "stagedDecisions", "diceClaims", "candidateAfterImages" })
            prefix[field] = packet[field]?.DeepClone();
        return Hash(prefix, "pending_prefix");
    }

    /// <summary>
    /// Validates a nonempty pending packet against its trusted path inventory.
    /// </summary>
    /// <param name="packet">
    /// Detached closed packet object.
    /// </param>
    /// <param name="paths">
    /// Exact registered capture path set.
    /// </param>
    private static void ValidatePacket(JsonObject packet, IReadOnlySet<string> paths)
    {
        Closed(packet, "sessionId requestId snapshotToken turn realm continuationGeneration conflictInstanceRef originalSnapshotFingerprint retainedPrefixFingerprint bounds cursor sources stagedDecisions diceClaims beforeImages candidateAfterImages preservedDraft packetFingerprint");
        foreach (var field in new[] { "sessionId", "requestId", "snapshotToken", "conflictInstanceRef" }) Text(packet[field]);
        Integer(packet["turn"], 1);
        Require(Text(packet["realm"]) is "chaos_sea" or "shining_abode", "Unknown spiritual realm.");
        Fingerprint(packet["originalSnapshotFingerprint"]);
        var bounds = Object(packet["bounds"]);
        SpiritualWoundTurnEvidence.ValidateBounds(bounds);
        var exchangeCount = (int)Integer(bounds["exchangeCount"], 1);
        var sourceSlots = (int)Integer(bounds["sourceSlotCount"]);
        var diceCount = (int)Integer(bounds["diceCount"]);
        Integer(bounds["imagePathCount"], paths.Count, paths.Count);
        var generation = Integer(packet["continuationGeneration"], 1, exchangeCount);
        var sources = Rows(packet["sources"], sourceSlots);
        var decisions = Rows(packet["stagedDecisions"], sourceSlots);
        var claims = Rows(packet["diceClaims"], diceCount);
        Require(sources.Length > 0, "An active decision packet requires an eligible source.");
        var cursor = Object(packet["cursor"]);
        Closed(cursor, "exchangeOrdinal waveOrdinal nextSourceOrdinal");
        var closedExchanges = Integer(cursor["exchangeOrdinal"], 0, exchangeCount);
        var wave = Integer(cursor["waveOrdinal"], 0, exchangeCount);
        var nextSource = (int)Integer(cursor["nextSourceOrdinal"], 0, sources.Length);
        Require(wave != exchangeCount || nextSource == sources.Length && closedExchanges == exchangeCount,
            "Wave exhaustion requires the complete executed and handled packet.");
        ValidateSources(packet, sources, claims, closedExchanges, nextSource, generation);
        ValidateDecisions(sources, decisions, nextSource, wave, exchangeCount);
        foreach (var pair in new[] { (Field: "beforeImages", Role: "original_before"),
            (Field: "candidateAfterImages", Role: "candidate_after") })
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in Rows(packet[pair.Field], paths.Count))
            {
                ReadImage(row, pair.Role, paths);
                Require(seen.Add(Text(row["path"])), "Duplicate image path.");
            }
        }
        var draft = Object(packet["preservedDraft"]);
        Closed(draft, "contentBase64 contentFingerprint");
        ValidateBytes(draft["contentBase64"], draft["contentFingerprint"]);
        Require(Fingerprint(packet["retainedPrefixFingerprint"]) == ComputePrefixFingerprint(packet),
            "Retained prefix digest differs.");
        Require(Fingerprint(packet["packetFingerprint"]) == Hash(packet, "pending_packet", "packetFingerprint"),
            "Packet digest differs.");
    }

    /// <summary>
    /// Joins every contiguous source and packet claim to one original context and executed frontier.
    /// </summary>
    /// <param name="packet">
    /// Closed packet containing original context and bounds.
    /// </param>
    /// <param name="sources">
    /// All retained sources, including zero ceilings.
    /// </param>
    /// <param name="claims">
    /// Complete retained claim prefix.
    /// </param>
    /// <param name="closedExchanges">
    /// Count of already executed exchanges.
    /// </param>
    /// <param name="nextSource">
    /// First unhandled source index.
    /// </param>
    /// <param name="generation">
    /// Current offered generation.
    /// </param>
    private static void ValidateSources(JsonObject packet, JsonObject[] sources, JsonObject[] claims,
        long closedExchanges, int nextSource, long generation)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var exchanges = new Dictionary<string, long>(StringComparer.Ordinal);
        var eligibleExchanges = new HashSet<long>();
        var projections = new List<JsonObject>();
        var conflictId = Text(sources[0]["conflictId"]);
        var claimsByExchange = claims.ToLookup(claim => Integer(claim["exchangeOrdinal"]));
        for (var index = 0; index < sources.Length; index++)
        {
            var source = sources[index];
            var calculation = SpiritualWoundSourceWitness.Validate(source);
            Require(Text(source["conflictId"]) == conflictId, "One packet cannot change its conflict display identity.");
            Integer(source["sourceOrdinal"], index, index);
            Require(ids.Add(Text(source["sourceId"])), "Duplicate source.");
            var exchange = Integer(source["exchangeOrdinal"], 0, closedExchanges - 1);
            var id = Text(source["exchangeId"]);
            Require(!exchanges.TryGetValue(id, out var prior) || prior == exchange, "Exchange ID was reused.");
            exchanges[id] = exchange;
            if (calculation.MaximumSeverityRank > 0 || source["guaranteedSeverityRank"] is not null)
            {
                eligibleExchanges.Add(exchange);
                if (index >= nextSource)
                    Require(closedExchanges == exchange + 1, "Later exchanges cannot precede an unresolved source decision.");
            }
            var evidence = Object(source["turnEvidence"]);
            foreach (var field in new[] { "sessionId", "requestId", "snapshotToken", "turn", "realm",
                "originalSnapshotFingerprint", "bounds" })
                Require(JsonNode.DeepEquals(evidence[field], packet[field]), "Source original context differs.");
            var exchangeClaims = new JsonArray(claimsByExchange[exchange]
                .Select(claim => claim.DeepClone()).ToArray());
            Require(JsonNode.DeepEquals(exchangeClaims, evidence["diceClaims"]), "Source claims differ from packet claims.");
            projections.Add(new JsonObject
            {
                ["instanceId"] = packet["conflictInstanceRef"]!.DeepClone(), ["witness"] = source.DeepClone()
            });
        }
        Require(eligibleExchanges.Count > 0 && generation <= eligibleExchanges.Count,
            "Generation exceeds retained eligible exchanges.");
        SpiritualWoundOpportunityReceiptState.ValidateTurnCoherence(projections);
        var pool = sources[0]["turnEvidence"]!["acceptedD20Values"]!.AsArray();
        SpiritualWoundTurnEvidence.ValidateClaims(packet["diceClaims"]!.AsArray(), pool,
            (int)Integer(packet["bounds"]!["exchangeCount"]), null);
        foreach (var claim in claims) Integer(claim["exchangeOrdinal"], 0, closedExchanges - 1);
    }

    /// <summary>
    /// Validates exactly the decided positive-source prefix and its staged proposal byte contracts.
    /// </summary>
    /// <param name="sources">
    /// Validated complete retained source prefix.
    /// </param>
    /// <param name="decisions">
    /// Ordered staged decisions.
    /// </param>
    /// <param name="nextSource">
    /// First unhandled source index.
    /// </param>
    /// <param name="wave">
    /// Current cursor wave or exhaustion boundary.
    /// </param>
    /// <param name="exchangeCount">
    /// Original admitted exchange bound.
    /// </param>
    private static void ValidateDecisions(JsonObject[] sources, JsonObject[] decisions, int nextSource, long wave, int exchangeCount)
    {
        var expected = sources.Take(nextSource).Where(source =>
            Integer(source["maximumSeverityRank"]) > 0 || source["guaranteedSeverityRank"] is not null).ToArray();
        Require(expected.Length == decisions.Length, "Decisions must cover exactly the handled positive-source prefix.");
        var opportunities = new HashSet<string>(StringComparer.Ordinal);
        var previousExchange = -1L;
        var previousWave = -1L;
        for (var index = 0; index < decisions.Length; index++)
        {
            var decision = decisions[index];
            var source = expected[index];
            var kind = Text(decision["decision"]);
            Closed(decision, kind == "guarantee_satisfied"
                ? "opportunityRef sourceId decisionFingerprint sourceOrdinal waveOrdinal decision selectedSeverityRank satisfiedWoundId satisfiedSeverityRank woundDraftBase64 woundDraftFingerprint"
                : "opportunityRef sourceId decisionFingerprint sourceOrdinal waveOrdinal decision selectedSeverityRank woundDraftBase64 woundDraftFingerprint");
            Require(opportunities.Add(Text(decision["opportunityRef"])), "Duplicate staged opportunity.");
            Require(Text(decision["sourceId"]) == Text(source["sourceId"]), "Staged source identity differs.");
            Integer(decision["sourceOrdinal"], Integer(source["sourceOrdinal"]), Integer(source["sourceOrdinal"]));
            var decisionWave = Integer(decision["waveOrdinal"], 0, Math.Min(wave, exchangeCount - 1));
            var exchange = Integer(source["exchangeOrdinal"]);
            Require(exchange == previousExchange ? decisionWave == previousWave : decisionWave > previousWave,
                "Staged waves disagree with causal exchange order.");
            previousExchange = exchange;
            previousWave = decisionWave;
            if (kind == "none")
                Require(decision["selectedSeverityRank"] is null && decision["woundDraftBase64"] is null &&
                    decision["woundDraftFingerprint"] is null && source["guaranteedSeverityRank"] is null,
                    "Decline contains a draft or violates a guarantee.");
            else if (kind == "guarantee_satisfied")
            {
                var guarantee = Integer(source["guaranteedSeverityRank"], 1, 4);
                Require(source["retraumaWoundId"] is null &&
                    decision["selectedSeverityRank"] is null &&
                    decision["woundDraftBase64"] is null &&
                    decision["woundDraftFingerprint"] is null &&
                    Integer(decision["satisfiedSeverityRank"], guarantee, 4) >= guarantee,
                    "A satisfied guarantee must be a source-bound no-transition result.");
                Text(decision["satisfiedWoundId"]);
            }
            else
            {
                Require(kind == "materialize", "Unknown staged decision kind.");
                var rank = Integer(decision["selectedSeverityRank"], 1, Integer(source["maximumSeverityRank"]));
                Require(source["guaranteedSeverityRank"] is null || rank == Integer(source["guaranteedSeverityRank"]),
                    "Staged severity differs from the exact guarantee.");
                if (source["retraumaWoundId"] is not null)
                {
                    var retained = WoundMaterializationContract.Parse(
                        System.Text.Encoding.UTF8.GetString(DecodeJsonObjectBytes(source["retraumaWoundJsonBase64"])),
                        "retainedPending.retraumaWound");
                    Require(retained.IsValid && rank > retained.Wound!.Severity.Rank,
                        "Staged re-trauma must increase retained severity.");
                }
                ValidateBytes(decision["woundDraftBase64"], decision["woundDraftFingerprint"]);
            }
            Require(Fingerprint(decision["decisionFingerprint"]) == Hash(decision, "staged_decision", "decisionFingerprint"),
                "Staged decision digest differs.");
        }
        var unresolved = sources.Skip(nextSource).FirstOrDefault(source =>
            Integer(source["maximumSeverityRank"]) > 0 || source["guaranteedSeverityRank"] is not null);
        if (unresolved is not null && previousExchange >= 0)
            Require(Integer(unresolved["exchangeOrdinal"]) == previousExchange
                ? wave == previousWave : wave > previousWave,
                "Current offered wave disagrees with staged earlier decisions.");
    }

    /// <summary>
    /// Checks exact decoded object bytes and their raw SHA-256 comparison fingerprint.
    /// </summary>
    /// <param name="content">
    /// Strict base64-encoded UTF-8 object JSON.
    /// </param>
    /// <param name="fingerprint">
    /// Required digest of decoded bytes.
    /// </param>
    private static void ValidateBytes(JsonNode? content, JsonNode? fingerprint)
    {
        var bytes = DecodeJsonObjectBytes(content);
        Require(Fingerprint(fingerprint) == "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            "Retained payload byte digest differs.");
    }

    /// <summary>
    /// Reads a bounded required array of objects without coercion.
    /// </summary>
    /// <param name="node">
    /// Required array node.
    /// </param>
    /// <param name="maximum">
    /// Inclusive count bound derived from original packet counts or the trusted inventory.
    /// </param>
    /// <returns>
    /// Ordered object rows from the parse tree.
    /// </returns>
    private static JsonObject[] Rows(JsonNode? node, int maximum)
    {
        if (node is not JsonArray array || array.Count > maximum) throw new FormatException("Bounded array required.");
        return array.Select(Object).ToArray();
    }

    /// <summary>
    /// Requires one JSON object.
    /// </summary>
    /// <param name="node">
    /// Required object value.
    /// </param>
    /// <returns>
    /// The object or a format failure for another node kind.
    /// </returns>
    private static JsonObject Object(JsonNode? node) => node as JsonObject ?? throw new FormatException("Object required.");
}

/// <summary>
/// Reports packet consistency without minting an original-turn owner.
/// </summary>
/// <param name="IsValid">
/// <see langword="true"/> for structurally consistent state; otherwise <see langword="false"/>.
/// </param>
/// <param name="State">
/// Detached comparison state, or <see langword="null"/> on failure.
/// </param>
/// <param name="Issues">
/// Read-only diagnostics, empty on success.
/// </param>
internal sealed record SpiritualWoundPendingParseResult(
    bool IsValid, SpiritualWoundDecisionPendingState? State, IReadOnlyList<ValidationIssue> Issues);

/// <summary>
/// Reports a pure unfinished-state comparison, never permission to publish or consume it.
/// </summary>
/// <param name="Disposition">
/// One of started, advanced, exact_replay or conflict.
/// </param>
/// <param name="After">
/// Detached candidate, or <see langword="null"/> for conflict.
/// </param>
internal sealed record SpiritualWoundPendingAdvanceResult(
    string Disposition, SpiritualWoundDecisionPendingState? After);
