using System.Text.Json;
using System.Text.Json.Nodes;
using static BookOfEternityClient.Services.SpiritualWoundStateJson;

namespace BookOfEternityClient.Services;

/// <summary>
/// Validates the immutable instance and closure projection of the spiritual receipt ledger.
/// This pure component is not a persisted root or a live source authority.
/// </summary>
internal sealed class SpiritualWoundConflictInstanceState
{
    private const int MaximumRows = 20_000;
    private readonly JsonObject _root;

    /// <summary>
    /// Retains a private detached copy of a fully validated projection.
    /// </summary>
    /// <param name="root">
    /// Validated instance/closure projection.
    /// </param>
    private SpiritualWoundConflictInstanceState(JsonObject root) => _root = root.DeepClone().AsObject();

    /// <summary>
    /// Validates closed shapes, digests, ordinals and instance/closure references.
    /// </summary>
    /// <param name="json">
    /// Serialized projection; missing or malformed content fails closed.
    /// </param>
    /// <param name="path">
    /// Owning receipt path used for diagnostics; must be nonempty.
    /// </param>
    /// <returns>
    /// A detached validated state or diagnostics without a partial state.
    /// </returns>
    internal static SpiritualWoundConflictInstanceParseResult Parse(string? json, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            var root = SpiritualWoundStateJson.Parse(json);
            Closed(root, "schemaVersion nextInstanceOrdinal nextClosureOrdinal instances closures");
            Integer(root["schemaVersion"], 1, 1);
            var instances = Rows(root, "instances", "nextInstanceOrdinal");
            var closures = Rows(root, "closures", "nextClosureOrdinal");
            var byId = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
            var allIds = new HashSet<string>(StringComparer.Ordinal);
            var starts = new HashSet<(string Realm, string Display, string Session,
                string Request, string Snapshot, long Turn)>();
            foreach (var instance in instances)
            {
                Closed(instance, "instanceId ordinal displayConflictId realm startSessionId startRequestId startSnapshotToken startTurn baselineConflictFingerprint instanceFingerprint");
                ValidateRowIdentity(instance, "instance", allIds);
                Text(instance["displayConflictId"]);
                Require(Text(instance["realm"]) is "chaos_sea" or "shining_abode", "Invalid spiritual realm.");
                Text(instance["startSessionId"]);
                Text(instance["startRequestId"]);
                Text(instance["startSnapshotToken"]);
                Integer(instance["startTurn"], 1);
                Fingerprint(instance["baselineConflictFingerprint"]);
                Require(starts.Add((Text(instance["realm"]), Text(instance["displayConflictId"]),
                    Text(instance["startSessionId"]), Text(instance["startRequestId"]),
                    Text(instance["startSnapshotToken"]), Integer(instance["startTurn"]))),
                    "A conflict start cannot allocate another instance.");
                byId.Add(Text(instance["instanceId"]), instance);
            }

            var closed = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
            var terminalEvents = new HashSet<string>(StringComparer.Ordinal);
            foreach (var closure in closures)
            {
                Closed(closure, "closureId ordinal instanceId terminalTurn terminalEventRef terminalConflictFingerprint closureFingerprint");
                ValidateRowIdentity(closure, "closure", allIds);
                var instanceId = Text(closure["instanceId"]);
                Require(byId.TryGetValue(instanceId, out var instance), "Closure names an unknown instance.");
                Require(closed.TryAdd(instanceId, closure), "An instance has only one terminal closure.");
                Integer(closure["terminalTurn"], Integer(instance!["startTurn"], 1));
                Require(terminalEvents.Add(MortalLocationIdentityState.BuildConfusableKey(Text(closure["terminalEventRef"]))),
                    "Terminal event references must be unique.");
                Fingerprint(closure["terminalConflictFingerprint"]);
            }

            var priorDisplays = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
            foreach (var instance in instances)
            {
                var display = Text(instance["displayConflictId"]);
                var key = Text(instance["realm"]) + ":" + MortalLocationIdentityState.BuildConfusableKey(display);
                if (priorDisplays.TryGetValue(key, out var prior))
                {
                    Require(Text(prior["displayConflictId"]) == display, "Confusable conflict display ID.");
                    Require(closed.TryGetValue(Text(prior["instanceId"]), out var closure),
                        "A reused display ID requires an accepted prior closure.");
                    Require(Integer(closure!["terminalTurn"]) <= Integer(instance["startTurn"]),
                        "New instance precedes the old instance's closure.");
                }
                priorDisplays[key] = instance;
            }
            return new(true, new(root), Array.Empty<ValidationIssue>());
        }
        catch (Exception error) when (error is JsonException or FormatException or InvalidOperationException or OverflowException)
        {
            return new(false, null, Array.AsReadOnly(new[]
            {
                new ValidationIssue(path, IssueSeverity.Error,
                    "Invalid spiritual conflict instance evidence.",
                    code: "spiritual_wound_instance_invalid_state",
                    section: "AcceptedTurnWoundMaterialization")
            }));
        }
    }

    /// <summary>
    /// Serializes the validated projection without exposing its private mutable tree.
    /// </summary>
    /// <param name="state">
    /// Parsed projection, which must not be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// Deterministic JSON text.
    /// </returns>
    internal static string SerializeCanonical(SpiritualWoundConflictInstanceState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Canonical(state._root);
    }

    /// <summary>
    /// Computes a comparison digest while excluding the derived row ID and digest itself.
    /// </summary>
    /// <param name="row">
    /// Instance or closure row; this operation neither validates nor accepts it.
    /// </param>
    /// <param name="kind">
    /// Exact domain, either instance or closure.
    /// </param>
    /// <returns>
    /// Domain-separated SHA-256 digest.
    /// </returns>
    internal static string ComputeRowFingerprint(JsonObject row, string kind)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (kind is not ("instance" or "closure")) throw new ArgumentException("Unknown row domain.", nameof(kind));
        return Hash(row, kind, kind + "Id", kind + "Fingerprint");
    }

    /// <summary>
    /// Accepts only append-only changes to a previously validated projection.
    /// </summary>
    /// <param name="before">
    /// Exact prior ledger projection.
    /// </param>
    /// <param name="candidate">
    /// Independently parsed complete candidate projection.
    /// </param>
    /// <returns>
    /// Appended or exact-replay state; conflict exposes no after-state.
    /// </returns>
    internal static SpiritualWoundConflictInstanceAppendResult PlanAppend(
        SpiritualWoundConflictInstanceState before, SpiritualWoundConflictInstanceState candidate)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(candidate);
        foreach (var field in new[] { "instances", "closures" })
        {
            var old = before._root[field]!.AsArray();
            var next = candidate._root[field]!.AsArray();
            if (old.Count > next.Count || old.Where((row, index) => !JsonNode.DeepEquals(row, next[index])).Any())
                return new("conflict", null);
        }
        return new(JsonNode.DeepEquals(before._root, candidate._root) ? "exact_replay" : "appended",
            new(candidate._root));
    }

    /// <summary>
    /// Reads a bounded row array and checks its contiguous ordinals and next counter.
    /// </summary>
    /// <param name="root">
    /// Closed root object.
    /// </param>
    /// <param name="field">
    /// Required array property.
    /// </param>
    /// <param name="counter">
    /// Matching next-ordinal property.
    /// </param>
    /// <returns>
    /// Ordered row objects from the private parse tree.
    /// </returns>
    private static JsonObject[] Rows(JsonObject root, string field, string counter)
    {
        if (root[field] is not JsonArray array || array.Count > MaximumRows)
            throw new FormatException("Bounded row array required.");
        Integer(root[counter], array.Count + 1, array.Count + 1);
        var result = new JsonObject[array.Count];
        for (var index = 0; index < result.Length; index++)
        {
            result[index] = array[index] as JsonObject ?? throw new FormatException("Row object required.");
            Integer(result[index]["ordinal"], index + 1, index + 1);
        }
        return result;
    }

    /// <summary>
    /// Checks a row's content digest and unique derived identity.
    /// </summary>
    /// <param name="row">
    /// Closed instance or closure row.
    /// </param>
    /// <param name="kind">
    /// Exact row kind and digest domain.
    /// </param>
    /// <param name="ids">
    /// IDs already encountered in the complete projection.
    /// </param>
    private static void ValidateRowIdentity(JsonObject row, string kind, HashSet<string> ids)
    {
        var expected = ComputeRowFingerprint(row, kind);
        Require(Fingerprint(row[kind + "Fingerprint"]) == expected, "Row digest mismatch.");
        var id = Text(row[kind + "Id"]);
        Require(id == "spiritual_" + kind + "_" + expected[7..] && ids.Add(id), "Invalid or duplicate row ID.");
    }
}

/// <summary>
/// Carries a pure parsed projection or its diagnostics without partial authority.
/// </summary>
/// <param name="IsValid">
/// <see langword="true"/> when parsing satisfies the state contract; otherwise <see langword="false"/>.
/// </param>
/// <param name="State">
    /// Validated projection, or <see langword="null"/> on failure.
/// </param>
/// <param name="Issues">
/// Read-only diagnostics; empty on success.
/// </param>
internal sealed record SpiritualWoundConflictInstanceParseResult(
    bool IsValid, SpiritualWoundConflictInstanceState? State, IReadOnlyList<ValidationIssue> Issues);

/// <summary>
/// Reports append, exact replay or conflict without writing canonical state.
/// </summary>
/// <param name="Disposition">
/// One of appended, exact_replay or conflict.
/// </param>
/// <param name="After">
    /// Detached candidate state, or <see langword="null"/> for conflict.
/// </param>
internal sealed record SpiritualWoundConflictInstanceAppendResult(
    string Disposition, SpiritualWoundConflictInstanceState? After);
