using System.Text.Json;
using System.Text.Json.Nodes;
using static BookOfEternityClient.Services.SpiritualWoundStateJson;

namespace BookOfEternityClient.Services;

internal sealed partial class SpiritualWoundDecisionPendingState
{
    internal static bool HasHealing(JsonObject packet) => packet.ContainsKey("healingSources");
    internal static bool HasPendingHealing(JsonObject packet) => HasHealing(packet) &&
        Integer(packet["cursor"]!["nextHealingSourceOrdinal"]) < packet["healingSources"]!.AsArray().Count;
    internal static bool AllSourcesHandled(JsonObject packet) =>
        Integer(packet["cursor"]!["nextSourceOrdinal"]) == packet["sources"]!.AsArray().Count && !HasPendingHealing(packet);

    private static bool HealingAdvanceAgrees(JsonObject prior, JsonObject next)
    {
        if (!HasHealing(next)) return !HasHealing(prior);
        if (!HasHealing(prior)) return false;
        var sourceCount = prior["healingSources"]!.AsArray().Count;
        var oldDecisions = prior["healingDecisions"]!.AsArray().Count;
        var frontier = Integer(prior["cursor"]!["exchangeOrdinal"]);
        if (next["healingSources"]!.AsArray().Skip(sourceCount).Any(s => Integer(s!["witness"]!["turnExchangeOrdinal"]) < frontier)) return false;
        return !next["healingDecisions"]!.AsArray().Skip(oldDecisions).Any(d =>
            Integer(d!["sourceOrdinal"]) >= sourceCount || Integer(d["waveOrdinal"]) != Integer(prior["cursor"]!["waveOrdinal"]));
    }

    // Closed schema2 is mandatory for healing; no optional widening of the
    // actively emitted injury-only schema1 or empty packet wrapper.
    private static void ValidateHealingPacket(JsonObject packet, int version, long closedExchanges,
        int nextInjurySource, long wave, int exchangeCount)
    {
        if (version == 1) return;
        var sources = Rows(packet["healingSources"], 1);
        Require(sources.Length == 1, "Combat packet requires its one original healing source.");
        var next = (int)Integer(packet["cursor"]!["nextHealingSourceOrdinal"], 0, sources.Length);
        var decisions = Rows(packet["healingDecisions"], 1);
        Require(decisions.Length == next, "Healing decisions must biject the handled source prefix.");
        var packetClaims = packet["diceClaims"]!.AsArray();
        for (var index = 0; index < sources.Length; index++)
        {
            var row = sources[index]; Closed(row, "sourceOrdinal turnEvidence witness");
            Integer(row["sourceOrdinal"], index, index);
            var witness = Object(row["witness"]);
            var source = SpiritualCombatHealingPersistedResult.ReadSource(witness);
            var ordinal = (int)Integer(witness["turnExchangeOrdinal"], 0, closedExchanges - 1);
            var evidence = Object(row["turnEvidence"]);
            SpiritualWoundTurnEvidence.Validate(evidence, ordinal);
            foreach (var field in new[] { "sessionId", "requestId", "snapshotToken", "turn", "realm", "originalSnapshotFingerprint", "bounds" })
                Require(JsonNode.DeepEquals(evidence[field], packet[field]), "Healing original context differs.");
            foreach (var field in new[] { "sessionId", "requestId", "snapshotToken", "turn", "realm" })
                Require(JsonNode.DeepEquals(witness[field], packet[field]), "Healing source tuple differs.");
            var claims = new JsonArray(packetClaims.Where(c => Integer(c!["exchangeOrdinal"]) == ordinal).Select(c => c!.DeepClone()).ToArray());
            Require(JsonNode.DeepEquals(evidence["diceClaims"], claims), "Healing exchange claims differ from the shared original claim set.");
            var actual = witness["exchange"]!["diceAudit"]!["diceUsed"]!.AsArray().Select(n => Object(n!))
                .OrderBy(d => Integer(d["sourceIndex"])).ToArray();
            Require(actual.Length == claims.Count && actual.Select((d, i) =>
                JsonNode.DeepEquals(d["sourceIndex"], claims[i]!["sourceIndex"]) &&
                JsonNode.DeepEquals(d["value"], claims[i]!["value"])).All(v => v), "Healing exchange changed an actual dice claim.");
            if (index >= next) Require(closedExchanges == ordinal + 1 && packet["stagedDecisions"]!.AsArray().Count == 0,
                "Healing must be handled at its exact exchange before injury decisions.");
            if (index < next)
            {
                var decision = decisions[index];
                ValidateHealingDecision(decision, row);
                Integer(decision["waveOrdinal"], 0, Math.Min(wave, exchangeCount - 1));
                Require(Text(decision["disposition"]) == source.Disposition, "Healing disposition changed.");
            }
        }
    }

    // Checkpoint child validation is structural DATA. Its owning cold replay
    // must execute the real source and compare the entire closed wrapper.
    internal static void ValidateHealingSubmissionOutcome(JsonObject outcome)
    {
        Closed(outcome, "sourceId sourceOrdinal waveOrdinal disposition resourceTransition result decisionFingerprint");
        Text(outcome["sourceId"]); Integer(outcome["sourceOrdinal"], 0, 0); Integer(outcome["waveOrdinal"], 0);
        Require(Fingerprint(outcome["decisionFingerprint"]) == Hash(outcome, "staged_combat_healing", "decisionFingerprint"), "Healing submission digest differs.");
        if (Text(outcome["disposition"]) == "resolved")
        {
            var result = Object(outcome["result"]);
            ValidateHealingDecision(outcome, new JsonObject { ["sourceOrdinal"] = 0, ["witness"] = result["source"]!.DeepClone() });
        }
        else
        {
            Require(Text(outcome["disposition"]) == "blocked" && outcome["result"] is null, "Unknown combat outcome or blocked stages.");
            var spend = Object(outcome["resourceTransition"]).Deserialize<ResourceTransition>() ?? throw new FormatException("Missing paid transition.");
            Require(JsonNode.DeepEquals(outcome["resourceTransition"], JsonSerializer.SerializeToNode(spend)) &&
                spend.Operation == ResourceTransitionOperation.Spend && spend.OriginKind == "afterlife_cost" && spend.Turn > 0 &&
                spend.Coordinate == new ResourceCoordinate("chaos_sea", ResourceOwnerKind.AfterlifeActor, "player_soul", "spiritual_action_points") &&
                spend.AppliedAmount >= 2 && spend.RequestedAmount == spend.AppliedAmount, "Invalid blocked paid DATA.");
        }
    }

    internal static void ValidateHealingDecision(JsonObject decision, JsonObject sourceRow)
    {
        Closed(decision, "sourceId sourceOrdinal waveOrdinal disposition resourceTransition result decisionFingerprint");
        var witness = Object(sourceRow["witness"]);
        var source = SpiritualCombatHealingPersistedResult.ReadSource(witness);
        Require(Text(decision["sourceId"]) == source.SourceId &&
            JsonNode.DeepEquals(decision["sourceOrdinal"], sourceRow["sourceOrdinal"]) &&
            Text(decision["disposition"]) == source.Disposition, "Healing decision has a foreign source.");
        var spend = Object(decision["resourceTransition"]).Deserialize<ResourceTransition>() ?? throw new FormatException("Missing healing spend.");
        Require(JsonNode.DeepEquals(decision["resourceTransition"], JsonSerializer.SerializeToNode(spend)), "Healing spend shape differs.");
        SpiritualCombatHealingPersistedResult.ValidateSpend(witness, spend);
        if (source.Disposition == "blocked") Require(decision["result"] is null, "Blocked healing cannot contain wound stages.");
        else
        {
            using var doc = JsonDocument.Parse(Object(decision["result"]).ToJsonString());
            var issues = new List<ValidationIssue>();
            var result = SpiritualCombatHealingPersistedResult.Parse(doc.RootElement, "pending.healing.result", issues);
            Require(result is not null && issues.Count == 0 && JsonNode.DeepEquals(result.Source, witness) &&
                JsonNode.DeepEquals(result.ToCanonicalJson()["resourceTransition"], decision["resourceTransition"]), "Healing result differs from source/spend.");
        }
        Require(Fingerprint(decision["decisionFingerprint"]) == Hash(decision, "staged_combat_healing", "decisionFingerprint"), "Healing decision digest differs.");
    }
}
