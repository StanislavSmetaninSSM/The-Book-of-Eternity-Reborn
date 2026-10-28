using System.Text.Json;
using System.Text.Json.Nodes;
using static BookOfEternityClient.Services.SpiritualWoundStateJson;

namespace BookOfEternityClient.Services;

internal sealed partial class SpiritualWoundOpportunityReceiptState
{
    private static IEnumerable<(string Session, string Request)> HealingRequests(JsonObject root) =>
        root["healingSources"] is JsonArray rows
            ? rows.Select(row => (Text(row!["witness"]!["sessionId"]), Text(row["witness"]!["requestId"]))) : [];

    private static bool HealingAppendAgrees(JsonObject before, JsonObject next)
    {
        var priorSources = before["healingSources"] as JsonArray;
        var nextSources = next["healingSources"] as JsonArray;
        if (priorSources is not null && nextSources is null) return false;
        if (nextSources is null) return true;
        foreach (var field in new[] { "healingSources", "healingDecisions" })
        {
            var old = before[field] as JsonArray ?? new JsonArray(); var rows = next[field]!.AsArray();
            if (rows.Count < old.Count || old.Where((row, i) => !JsonNode.DeepEquals(row, rows[i])).Any()) return false;
        }
        var accepted = before["sources"]!.AsArray().Select(row => OriginalRequest(Object(row!["witness"])))
            .Concat(HealingRequests(before)).ToHashSet();
        var closed = before["closures"]!.AsArray().Select(row => Text(row!["instanceId"])).ToHashSet(StringComparer.Ordinal);
        return !nextSources.Skip(priorSources?.Count ?? 0).Any(row => closed.Contains(Text(row!["instanceId"])) ||
            accepted.Contains((Text(row["witness"]!["sessionId"]), Text(row["witness"]!["requestId"]))));
    }

    private static void ValidateHealingLedger(JsonObject root, int version,
        IReadOnlyDictionary<string, JsonObject> instances, IReadOnlyDictionary<string, long> closures,
        JsonObject[] injurySources, JsonObject[] injuryDecisions)
    {
        if (version == 1) return;
        var sources = Rows(root, "healingSources", "nextHealingSourceOrdinal");
        var decisions = Rows(root, "healingDecisions", "nextHealingDecisionOrdinal");
        Require(sources.Length > 0 && sources.Length == decisions.Length, "Healing sources/outcomes must form a nonempty bijection.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var requests = new HashSet<(string Session, string Request)>();
        var exchanges = new HashSet<(string Instance, string Exchange)>();
        for (var index = 0; index < sources.Length; index++)
        {
            var row = sources[index]; Closed(row, "ordinal instanceId turnEvidence witness");
            var witness = Object(row["witness"]); var source = SpiritualCombatHealingPersistedResult.ReadSource(witness);
            var instanceId = Text(row["instanceId"]);
            Require(instances.TryGetValue(instanceId, out var instance) &&
                source.Intent.ConflictId == Text(instance!["displayConflictId"]) && Text(instance["realm"]) == "chaos_sea" &&
                source.Turn >= Integer(instance["startTurn"]) && (!closures.TryGetValue(instanceId, out var terminal) || source.Turn <= terminal),
                "Healing source differs from its real conflict lifecycle.");
            var request = (Text(witness["sessionId"]), Text(witness["requestId"]));
            Require(ids.Add(source.SourceId) && requests.Add(request) && exchanges.Add((instanceId, source.ExchangeId)),
                "A combat healing request/source cannot be accepted twice.");
            var evidence = Object(row["turnEvidence"]);
            var ordinal = (int)Integer(witness["turnExchangeOrdinal"]);
            SpiritualWoundTurnEvidence.Validate(evidence, ordinal);
            foreach (var field in new[] { "sessionId", "requestId", "snapshotToken", "turn", "realm" })
                Require(JsonNode.DeepEquals(witness[field], evidence[field]), "Healing durable original tuple differs.");
            var dice = witness["exchange"]!["diceAudit"]!["diceUsed"]!.AsArray().Select(n => Object(n!))
                .OrderBy(d => Integer(d["sourceIndex"])).ToArray();
            var claims = evidence["diceClaims"]!.AsArray();
            Require(dice.Length == claims.Count && dice.Select((d, i) => JsonNode.DeepEquals(d["sourceIndex"], claims[i]!["sourceIndex"]) &&
                JsonNode.DeepEquals(d["value"], claims[i]!["value"])).All(v => v), "Healing durable claims differ from actual exchange.");
            var decision = decisions[index]; Closed(decision, "ordinal instanceId outcome");
            Require(Text(decision["instanceId"]) == instanceId, "Healing decision instance differs.");
            var outcome = Object(decision["outcome"]);
            var pendingSource = new JsonObject { ["sourceOrdinal"] = 0, ["turnEvidence"] = evidence.DeepClone(), ["witness"] = witness.DeepClone() };
            SpiritualWoundDecisionPendingState.ValidateHealingDecision(outcome, pendingSource);
            // This first slice admits exactly one new exchange at wave0. Injury
            // and healing in that exchange share the same wave and original pool.
            Integer(outcome["waveOrdinal"], 0, 0);
            foreach (var injury in injurySources.Where(s => OriginalRequest(Object(s["witness"])) == request))
            {
                var other = Object(injury["witness"]); var context = Object(other["turnEvidence"]);
                var baseContext = context.DeepClone().AsObject(); baseContext.Remove("diceClaims");
                var healingContext = evidence.DeepClone().AsObject(); healingContext.Remove("diceClaims");
                Require(Text(injury["instanceId"]) == instanceId && Text(other["conflictId"]) == source.Intent.ConflictId &&
                    JsonNode.DeepEquals(baseContext, healingContext), "Injury/healing original contexts differ.");
                if (Text(other["exchangeId"]) == source.ExchangeId)
                {
                    Require(Integer(other["exchangeOrdinal"]) == ordinal && JsonNode.DeepEquals(context["diceClaims"], evidence["diceClaims"]),
                        "One exchange must retain the same original claims.");
                    foreach (var bound in injuryDecisions.Where(d => Text(d["sourceId"]) == Text(other["sourceId"])))
                        Require(Integer(bound["binding"]!["continuationGeneration"]) == 1 && Integer(bound["binding"]!["waveOrdinal"]) == 0,
                            "Counterharm and healing are one eligible wave.");
                }
                else
                {
                    Require(Integer(other["exchangeOrdinal"]) > ordinal, "Ordinary injury must follow the selected healing exchange.");
                    var healingClaims = evidence["diceClaims"]!.AsArray().Select(c => Integer(c!["sourceIndex"])).ToHashSet();
                    Require(!context["diceClaims"]!.AsArray().Any(c => healingClaims.Contains(Integer(c!["sourceIndex"]))),
                        "A later ordinary exchange cannot reclaim healing dice.");
                    foreach (var bound in injuryDecisions.Where(d => Text(d["sourceId"]) == Text(other["sourceId"])))
                        Require(Integer(bound["binding"]!["continuationGeneration"]) > 1 && Integer(bound["binding"]!["waveOrdinal"]) > 0,
                            "Later injury must follow the healing wave and generation.");
                }
            }
        }
    }
}

internal sealed partial class SpiritualWoundOpportunityReceiptState
{
    internal bool HasCombatHealing => _root["healingSources"] is JsonArray;

    // The enclosing parser already checked source/outcome bijection and every
    // closed calculation. These queries return only detached comparison DATA.
    internal JsonObject? ReadCombatOutcome(string sessionId, string requestId, int turn)
    {
        if (_root["healingSources"] is not JsonArray sources) return null;
        var matching = sources.OfType<JsonObject>().Where(row =>
            Text(row["witness"]!["sessionId"]) == sessionId && Text(row["witness"]!["requestId"]) == requestId &&
            Integer(row["witness"]!["turn"]) == turn).ToArray();
        if (matching.Length == 0) return null;
        Require(matching.Length == 1, "Ambiguous accepted combat healing request.");
        var source = matching[0];
        var outcome = _root["healingDecisions"]!.AsArray().OfType<JsonObject>().Single(d =>
            Text(d["outcome"]!["sourceId"]) == Text(source["witness"]!["sourceId"]));
        return new JsonObject { ["instanceId"] = source["instanceId"]!.DeepClone(), ["witness"] = source["witness"]!.DeepClone(),
            ["turnEvidence"] = source["turnEvidence"]!.DeepClone(), ["outcome"] = outcome["outcome"]!.DeepClone() };
    }

    internal void RequireCombatHistoryResourceAgreement(WoundHistoryState history, ResourceHistoryState resources)
    {
        var results = history.Transitions.Select(t => t.TransitionResult).OfType<SpiritualCombatHealingPersistedResult>().ToArray();
        if (_root["healingSources"] is not JsonArray sourceRows)
        {
            Require(results.Length == 0, "Combat History result lost its mandatory durable healing family.");
            return;
        }
        var sources = sourceRows.OfType<JsonObject>().ToDictionary(s => Text(s["witness"]!["sourceFingerprint"]), StringComparer.Ordinal);
        var byId = sourceRows.OfType<JsonObject>().ToDictionary(s => Text(s["witness"]!["sourceId"]), StringComparer.Ordinal);
        foreach (var result in results) Require(sources.ContainsKey(result.SourceFingerprint), "Combat History result lost its mandatory durable source/outcome.");
        foreach (var row in _root["healingDecisions"]!.AsArray().OfType<JsonObject>())
        {
            var outcome = Object(row["outcome"]); var source = Object(byId[Text(outcome["sourceId"])]["witness"]);
            var spend = Object(outcome["resourceTransition"]).Deserialize<ResourceTransition>()!;
            var paid = resources.Transitions.Where(t => t.OriginKind == "afterlife_cost" && t.OriginId == Text(source["exchangeId"]) &&
                t.Coordinate == spend.Coordinate && t.Operation == ResourceTransitionOperation.Spend && t.Turn == Integer(source["turn"])).ToArray();
            Require(paid.Length == 1 && JsonNode.DeepEquals(System.Text.Json.JsonSerializer.SerializeToNode(paid[0]), outcome["resourceTransition"]),
                "Combat outcome lost its exact once-paid resource History transition.");
            var actual = results.Where(r => r.SourceFingerprint == Text(source["sourceFingerprint"])).ToArray();
            if (Text(outcome["disposition"]) == "blocked") Require(actual.Length == 0, "Blocked combat outcome fabricated wound stages.");
            else Require(actual.Length == 1 && JsonNode.DeepEquals(actual[0].ToCanonicalJson(), outcome["result"]),
                "Combat outcome lost its actual wound History result.");
        }
    }
}
