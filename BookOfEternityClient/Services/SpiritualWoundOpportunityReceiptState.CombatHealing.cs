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
                Require(Text(injury["instanceId"]) == instanceId && Text(other["exchangeId"]) == source.ExchangeId &&
                    Integer(other["exchangeOrdinal"]) == ordinal && JsonNode.DeepEquals(context, evidence),
                    "Injury/healing must retain the same actual exchange and full original evidence.");
                foreach (var bound in injuryDecisions.Where(d => Text(d["sourceId"]) == Text(other["sourceId"])))
                    Require(Integer(bound["binding"]!["continuationGeneration"]) == 1 && Integer(bound["binding"]!["waveOrdinal"]) == 0,
                        "Counterharm and healing are one eligible wave.");
            }
        }
    }
}
