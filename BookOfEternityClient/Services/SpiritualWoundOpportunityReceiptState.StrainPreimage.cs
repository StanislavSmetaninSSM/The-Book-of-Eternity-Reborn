using System.Text.Json.Nodes;
using static BookOfEternityClient.Services.SpiritualWoundStateJson;

namespace BookOfEternityClient.Services;

internal sealed partial class SpiritualWoundOpportunityReceiptState
{
    /// <summary>Retains immutable parsed comparison DATA and the exact parent transition identity for later History proof.</summary>
    private sealed record RetainedStrainParent(string ConflictId, string ExchangeId, string InstanceId,
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
            result.Add(new(Text(witness["conflictId"]), Text(witness["exchangeId"]), Text(row["instanceId"]),
                (int)Integer(evidence["turn"]), data.ParentWound.LastTransition.TransitionId, data));
        }
        return result.ToArray();
    }
}
