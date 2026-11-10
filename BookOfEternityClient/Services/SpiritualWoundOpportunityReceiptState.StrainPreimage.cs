using System.Text.Json.Nodes;
using static BookOfEternityClient.Services.SpiritualWoundStateJson;

namespace BookOfEternityClient.Services;

internal sealed partial class SpiritualWoundOpportunityReceiptState
{
    /// <summary>Retains one immutable parsed comparison per source, with every parent anchor inside its DATA.</summary>
    private sealed record RetainedStrainComparison(int GlobalOrdinal, string ConflictId, string ExchangeId, string InstanceId,
        SpiritualStrainPreimageData Data);

    /// <summary>Joins already parsed DATA to one earlier materialize-III parent or a signed-existing comparison, without granting original or History authority.</summary>
    /// <param name="strains">Already validated comparisons paired with their containing source rows.</param>
    /// <param name="sources">Existing exact source index from this ledger parse.</param>
    /// <param name="decisions">Already validated one-to-one decisions from the same immutable ledger input.</param>
    /// <returns>Immutable comparison facts for this parsed ledger only.</returns>
    private static IReadOnlyList<RetainedStrainComparison> ValidateStrainParentRelations(
        IReadOnlyList<(JsonObject Row, SpiritualStrainPreimageData Data)> strains,
        IReadOnlyDictionary<string, JsonObject> sources, IReadOnlyList<JsonObject> decisions)
    {
        if (strains.Count == 0) return [];
        // A transient domain index over the already checked bijection, not a cache or an authority registry.
        var decisionsBySource = decisions.ToDictionary(row => Text(row["sourceId"]), StringComparer.Ordinal);
        var result = new List<RetainedStrainComparison>(strains.Count);
        foreach (var (row, data) in strains)
        {
            if (data.Version is 2 or 3 or 4 or 5 or 6 or 7)
            {
                var current = Object(row["witness"]);
                result.Add(new((int)Integer(row["ordinal"]), Text(current["conflictId"]), Text(current["exchangeId"]),
                    Text(row["instanceId"]), data));
                continue;
            }
            if (!sources.TryGetValue(data.ParentSourceId!, out var parentRow) || parentRow is null)
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
            if (!decisionsBySource.TryGetValue(data.ParentSourceId!, out var selected) || selected is null)
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
                data));
        }
        return result.ToArray();
    }

    /// <summary>Distinguishes a missing active pressure comparison from an exact or contradictory retained log.</summary>
    internal enum RetainedPressureHistoryMatch { Absent, Exact, Mismatch }

    /// <summary>Reports retained v3/v4/v5/v6/v7 pressure DATA, including prior and closed instances, without granting live applicability.</summary>
    internal bool HasRetainedPressureComparisons => _strainComparisons.Any(parent => parent.Data.Version is 3 or 4 or 5 or 6 or 7);

    /// <summary>Joins every retained v3/v4/v5/v6/v7 row in the unique open instance to exactly one complete accepted actual exchange.</summary>
    /// <param name="realm">Normalized authenticated spiritual realm.</param>
    /// <param name="activeConflict">Authenticated full active conflict, or null when none remains.</param>
    /// <param name="signedCurrentTurn">Authenticated current snapshot turn controlling historical top-summary drift.</param>
    /// <returns>Absent when no active v3/v4/v5/v6/v7 applies; otherwise exact agreement or mismatch.</returns>
    internal RetainedPressureHistoryMatch MatchRetainedPressureHistory(string realm, JsonObject? activeConflict,
        int signedCurrentTurn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(realm);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(signedCurrentTurn);
        if (activeConflict is null) return RetainedPressureHistoryMatch.Absent;
        var conflictId = AfterlifeSpiritualConflictState.GetNodeString(activeConflict["conflictId"]);
        if (conflictId is null) return RetainedPressureHistoryMatch.Absent;
        var closed = _root["closures"]!.AsArray().Select(row => Text(row!["instanceId"])).ToHashSet(StringComparer.Ordinal);
        var instances = _root["instances"]!.AsArray().OfType<JsonObject>().Where(row =>
            Text(row["realm"]) == realm && Text(row["displayConflictId"]) == conflictId &&
            !closed.Contains(Text(row["instanceId"]))).ToArray();
        if (instances.Length == 0) return RetainedPressureHistoryMatch.Absent;
        if (instances.Length != 1) return RetainedPressureHistoryMatch.Mismatch;
        var instanceId = Text(instances[0]["instanceId"]);
        var retained = _strainComparisons.Where(parent => parent.InstanceId == instanceId &&
            parent.ConflictId == conflictId && parent.Data.Version is 3 or 4 or 5 or 6 or 7).ToArray();
        if (retained.Length == 0) return RetainedPressureHistoryMatch.Absent;
        if (activeConflict["exchangeLog"] is not JsonArray log) return RetainedPressureHistoryMatch.Mismatch;
        // A transient join over this one immutable frame, not a persistent cache or authority index.
        var exchanges = log.Select((node, index) => (Exchange: node as JsonObject, Index: index))
            .Where(row => row.Exchange is not null).ToLookup(row =>
                AfterlifeSpiritualConflictState.GetNodeString(row.Exchange!["exchangeId"]) ?? string.Empty, StringComparer.Ordinal);
        var previousIndex = -1;
        foreach (var comparison in retained)
        {
            var matches = exchanges[comparison.ExchangeId].ToArray();
            if (matches.Length != 1 || matches[0].Index <= previousIndex)
                return RetainedPressureHistoryMatch.Mismatch;
            var accepted = comparison.Data.ReadActualExchange();
            var priorTurn = accepted["exchangeAtTurn"] is JsonValue marker && marker.TryGetValue<int>(out var atTurn) &&
                atTurn < signedCurrentTurn;
            if (!ValidationService.MatchesRetainedSpiritualHistoryExchange(accepted, matches[0].Exchange!, priorTurn))
                return RetainedPressureHistoryMatch.Mismatch;
            // Only relative retained order is binding here; unrelated rows and raw input pointers are not authority.
            previousIndex = matches[0].Index;
        }
        return RetainedPressureHistoryMatch.Exact;
    }

    /// <summary>Checks each immutable full parent envelope against its exact retained latest-transition History after-image.</summary>
    internal bool HasExactStrainHistory(WoundHistoryState history)
    {
        ArgumentNullException.ThrowIfNull(history);
        foreach (var comparison in _strainComparisons)
            foreach (var parent in comparison.Data.Parents)
            {
                var wound = parent.ParentWound;
                if (!history.TryResolveExactTransition(wound.LastTransition.TransitionId, out var anchor) || anchor is null ||
                    anchor.Kind != wound.LastTransition.Kind ||
                    anchor.WoundTransitionOrdinal != wound.LastTransition.Ordinal || anchor.Terminal ||
                    anchor.WoundId != wound.WoundId || anchor.Turn != wound.LastTransition.Turn ||
                    comparison.Data.Version == 1 && anchor.EventRef != wound.Origin.EventRef ||
                    anchor.AfterFingerprint != parent.ParentSemanticFingerprint)
                    return false;
            }
        return true;
    }

    /// <summary>Requires each newly appended parent to equal the actual owner-sealed insertion and its completed History.</summary>
    internal bool HasCurrentStrainInsertions(int firstNewSourceOrdinal, SpiritualLiveWoundCompletion? live)
    {
        var current = _strainComparisons.Where(row => row.GlobalOrdinal >= firstNewSourceOrdinal && row.Data.Version == 1).ToArray();
        if (current.Length == 0) return true;
        if (live is null) return false;
        var history = WoundHistoryState.Parse(live.FinalState.History.ToJsonString(), WoundHistoryState.HistoryPath);
        if (!history.IsValid || history.State is null || !HasExactStrainHistory(history.State)) return false;
        // Read each detached insertion envelope once, preserving the sealed completion's ownership rules.
        var creations = live.Insertions.Where(row => !row.IsCombatHealing).Select(row => row.Wound)
            .Where(wound => wound.LastTransition.Kind == "create" && wound.LastTransition.Ordinal == 1)
            .Select(wound => (Wound: wound, Fingerprint: WoundIdentityState.ComputeSemanticFingerprint(wound))).ToArray();
        return current.All(parent => creations.Count(row => row.Wound.WoundId == parent.Data.ParentWound.WoundId &&
            row.Wound.LastTransition.TransitionId == parent.Data.ParentWound.LastTransition.TransitionId &&
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
        var candidates = _strainComparisons.Where(parent => parent.InstanceId == instanceId &&
            parent.ConflictId == conflictId && parent.ExchangeId == exchangeId).ToArray();
        if (candidates.Length != 1) return null;
        var data = candidates[0].Data;
        var accepted = data.ReadActualExchange();
        if (!ValidationService.MatchesRetainedSpiritualHistoryExchange(accepted, actual, allowHistoricalSummaryDrift))
            return null;
        return data.ReadOrdinaryAfter(accepted);
    }
}
