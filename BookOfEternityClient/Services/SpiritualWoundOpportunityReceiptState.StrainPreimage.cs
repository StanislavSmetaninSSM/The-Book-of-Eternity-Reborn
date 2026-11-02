using System.Text.Json.Nodes;
using static BookOfEternityClient.Services.SpiritualWoundStateJson;

namespace BookOfEternityClient.Services;

internal sealed partial class SpiritualWoundOpportunityReceiptState
{
    /// <summary>Retains immutable parsed comparison DATA and the exact parent transition identity for later History proof.</summary>
    private sealed record RetainedStrainParent(int GlobalOrdinal, string ConflictId, string ExchangeId, string InstanceId,
        int Turn, string CreationTransitionId, SpiritualStrainPreimageData Data);

    /// <summary>Joins already parsed DATA to one earlier materialize-III parent, without granting original or History authority.</summary>
    /// <param name="strains">Already validated comparisons paired with their containing source rows.</param>
    /// <param name="sources">Existing exact source index from this ledger parse.</param>
    /// <param name="decisions">Already validated one-to-one decisions from the same immutable ledger input.</param>
    /// <returns>Immutable comparison facts for this parsed ledger only.</returns>
    private static IReadOnlyList<RetainedStrainParent> ValidateStrainParentRelations(
        IReadOnlyList<(JsonObject Row, SpiritualStrainPreimageData Data)> strains,
        IReadOnlyDictionary<string, JsonObject> sources, IReadOnlyList<JsonObject> decisions)
    {
        if (strains.Count == 0) return [];
        // A transient domain index over the already checked bijection, not a cache or an authority registry.
        var decisionsBySource = decisions.ToDictionary(row => Text(row["sourceId"]), StringComparer.Ordinal);
        var result = new List<RetainedStrainParent>(strains.Count);
        foreach (var (row, data) in strains)
        {
            if (!sources.TryGetValue(data.ParentSourceId, out var parentRow) || parentRow is null)
                throw new FormatException("Retained strain parent source is missing.");
            var witness = Object(row["witness"]);
            var parent = Object(parentRow["witness"]);
            var evidence = Object(witness["turnEvidence"]);
            var parentEvidence = Object(parent["turnEvidence"]);
            Require(Text(row["instanceId"]) == Text(parentRow["instanceId"]) &&
                Integer(parentRow["ordinal"]) < Integer(row["ordinal"]) &&
                Integer(parent["sourceOrdinal"]) < Integer(witness["sourceOrdinal"]) &&
                Integer(parent["exchangeOrdinal"]) < Integer(witness["exchangeOrdinal"]) &&
                Text(parent["conflictId"]) == Text(witness["conflictId"]) &&
                Text(parent["affectedSide"]) == "player" &&
                JsonNode.DeepEquals(parent["affectedActor"], witness["affectedActor"]),
                "Strain parent must precede this exchange for the same instance and affected actor.");
            foreach (var field in new[] { "sessionId", "requestId", "snapshotToken", "turn", "realm",
                         "originalSnapshotFingerprint", "bounds", "acceptedD20Values" })
                Require(JsonNode.DeepEquals(parentEvidence[field], evidence[field]), "Strain parent original context differs.");
            if (!decisionsBySource.TryGetValue(data.ParentSourceId, out var selected) || selected is null)
                throw new FormatException("Retained strain parent decision is missing.");
            Require(Text(selected["decision"]) == "materialize" &&
                Integer(selected["selectedSeverityRank"], 3, 3) == 3 && parent["retraumaWoundId"] is null &&
                Text(selected["woundId"]) == data.ParentWound.WoundId &&
                Text(selected["transitionId"]) == data.ParentWound.LastTransition.TransitionId,
                "Strain comparison requires its exact earlier creation decision, wound, transition and rank.");
            var key = SpiritualWoundSourceWitness.HarmEventKey(Text(parentEvidence["sessionId"]),
                Text(parentEvidence["requestId"]), Text(parentEvidence["snapshotToken"]),
                Text(parent["conflictId"]), Text(parent["exchangeId"]), Text(parent["affectedSide"]));
            Require(data.ParentWound.Origin.EventRef == "spiritual_wound_event_" + key &&
                data.ParentWound.Origin.OpportunityId == "spiritual_wound_" + key &&
                Text(selected["opportunityRef"]) == data.ParentWound.Origin.OpportunityId &&
                data.ParentWound.Origin.SourceKind == (Text(parent["appliedArtKind"]) == "standard"
                    ? "spiritual_standard_art" : "spiritual_art") &&
                data.ParentWound.Origin.SourceId == Text(parent["appliedArtId"]),
                "Strain parent envelope source and opportunity differ from the creation decision.");
            result.Add(new((int)Integer(row["ordinal"]), Text(witness["conflictId"]), Text(witness["exchangeId"]), Text(row["instanceId"]),
                (int)Integer(evidence["turn"]), data.ParentWound.LastTransition.TransitionId, data));
        }
        return result.ToArray();
    }

    /// <summary>Checks each immutable full parent envelope against its exact first-create History after-image.</summary>
    internal bool HasExactStrainHistory(WoundHistoryState history)
    {
        ArgumentNullException.ThrowIfNull(history);
        foreach (var parent in _strainParents)
        {
            var wound = parent.Data.ParentWound;
            if (!history.TryResolveExactTransition(parent.CreationTransitionId, out var creation) || creation is null ||
                creation.Kind != "create" || creation.WoundTransitionOrdinal != 1 || creation.Terminal ||
                creation.WoundId != wound.WoundId || creation.Turn != parent.Turn ||
                creation.EventRef != wound.Origin.EventRef ||
                creation.AfterFingerprint != parent.Data.ParentSemanticFingerprint)
                return false;
        }
        return true;
    }

    /// <summary>Requires each newly appended parent to equal the actual owner-sealed insertion and its completed History.</summary>
    internal bool HasCurrentStrainInsertions(int firstNewSourceOrdinal, SpiritualLiveWoundCompletion? live)
    {
        var current = _strainParents.Where(row => row.GlobalOrdinal >= firstNewSourceOrdinal).ToArray();
        if (current.Length == 0) return true;
        if (live is null) return false;
        var history = WoundHistoryState.Parse(live.FinalState.History.ToJsonString(), WoundHistoryState.HistoryPath);
        if (!history.IsValid || history.State is null || !HasExactStrainHistory(history.State)) return false;
        // Read each detached insertion envelope once, preserving the sealed completion's ownership rules.
        var creations = live.Insertions.Where(row => !row.IsCombatHealing).Select(row => row.Wound)
            .Where(wound => wound.LastTransition.Kind == "create" && wound.LastTransition.Ordinal == 1)
            .Select(wound => (Wound: wound, Fingerprint: WoundIdentityState.ComputeSemanticFingerprint(wound))).ToArray();
        return current.All(parent => creations.Count(row => row.Wound.WoundId == parent.Data.ParentWound.WoundId &&
            row.Wound.LastTransition.TransitionId == parent.CreationTransitionId &&
            row.Fingerprint == parent.Data.ParentSemanticFingerprint) == 1);
    }

    /// <summary>
    /// Reads historical comparison DATA only for the unique open instance and exact accepted actual exchange.
    /// The caller must independently authenticate this ledger and its matching History before invoking it.
    /// </summary>
    internal JsonObject? ReadRetainedStrainOrdinaryAfter(string realm, string conflictId, JsonObject actual,
        bool allowHistoricalSummaryDrift)
    {
        var exchangeId = AfterlifeSpiritualConflictState.GetNodeString(actual["exchangeId"]);
        if (exchangeId is null) return null;
        var closed = _root["closures"]!.AsArray().Select(row => Text(row!["instanceId"])).ToHashSet(StringComparer.Ordinal);
        var instances = _root["instances"]!.AsArray().OfType<JsonObject>().Where(row =>
            Text(row["realm"]) == realm && Text(row["displayConflictId"]) == conflictId &&
            !closed.Contains(Text(row["instanceId"]))).ToArray();
        if (instances.Length != 1) return null;
        var instanceId = Text(instances[0]["instanceId"]);
        var candidates = _strainParents.Where(parent => parent.InstanceId == instanceId &&
            parent.ConflictId == conflictId && parent.ExchangeId == exchangeId).ToArray();
        if (candidates.Length != 1) return null;
        var data = candidates[0].Data;
        var accepted = data.ReadActualExchange();
        if (!ValidationService.MatchesRetainedSpiritualHistoryExchange(accepted, actual, allowHistoricalSummaryDrift))
            return null;
        return data.ReadOrdinaryAfter(accepted);
    }
}
