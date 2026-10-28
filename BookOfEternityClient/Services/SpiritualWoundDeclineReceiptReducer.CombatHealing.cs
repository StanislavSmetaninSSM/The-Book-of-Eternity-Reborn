using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class SpiritualWoundDeclineReceiptReducer
{
    private static bool AppendCombatHealing(ValidationService.SpiritualSignedC1Origin signed,
        JsonObject packet, JsonObject ledger, string instanceId, SpiritualLiveWoundCompletion? liveWounds,
        ValidationService.SpiritualOriginalTurnCapture.AuthenticatedC3Pair? pair)
    {
        if (packet["healingSources"] is not JsonArray sources || sources.Count != 1 ||
            packet["healingDecisions"] is not JsonArray decisions || decisions.Count != 1 ||
            packet["cursor"]?["nextHealingSourceOrdinal"]?.GetValue<int>() != 1 ||
            decisions[0] is not JsonObject outcome || pair?.MatchesHealing(signed, packet, outcome) != true)
            return false;
        var row = sources[0]!.AsObject();
        SpiritualWoundDecisionPendingState.ValidateHealingDecision(outcome, row);
        var source = SpiritualCombatHealingPersistedResult.ReadSource(row["witness"]!.AsObject());
        var insertions = liveWounds?.Insertions.Where(i => i.IsCombatHealing).ToArray() ?? [];
        if (source.Disposition == "blocked")
        {
            if (insertions.Length != 0 || outcome["result"] is not null) return false;
        }
        else
        {
            if (insertions.Length != 1 || insertions[0].SourceCoordinate != "combat_healing/" + source.SourceId ||
                insertions[0].ExchangeOrdinal != 0 || outcome["result"] is not JsonObject resultRoot) return false;
            var insertion = insertions[0];
            var history = WoundHistoryState.Parse(insertion.ReducedState.History.ToJsonString(), WoundHistoryState.HistoryPath);
            if (!history.IsValid || history.State is null) return false;
            using var document = JsonDocument.Parse(resultRoot.ToJsonString());
            var issues = new List<ValidationIssue>();
            var result = SpiritualCombatHealingPersistedResult.Parse(document.RootElement, "combat.owner.result", issues);
            if (result is null || issues.Count != 0 || insertion.CombatStages.Count != result.Stages.Count ||
                !history.State.Transitions.Any(t => t.TransitionResult is SpiritualCombatHealingPersistedResult actual &&
                    actual.ResultFingerprint == result.ResultFingerprint) ||
                WoundMaterializationContract.SerializeCanonical(insertion.Wound) !=
                    WoundMaterializationContract.SerializeCanonical(result.Stages[^1].After)) return false;
        }
        if (ledger["healingSources"] is null)
        {
            ledger["schemaVersion"] = 2;
            ledger["nextHealingSourceOrdinal"] = 1; ledger["nextHealingDecisionOrdinal"] = 1;
            ledger["healingSources"] = new JsonArray(); ledger["healingDecisions"] = new JsonArray();
        }
        var sourceOrdinal = ledger["nextHealingSourceOrdinal"]!.GetValue<int>();
        var decisionOrdinal = ledger["nextHealingDecisionOrdinal"]!.GetValue<int>();
        ledger["healingSources"]!.AsArray().Add(new JsonObject
        {
            ["ordinal"] = sourceOrdinal, ["instanceId"] = instanceId,
            ["turnEvidence"] = row["turnEvidence"]!.DeepClone(), ["witness"] = row["witness"]!.DeepClone()
        });
        ledger["healingDecisions"]!.AsArray().Add(new JsonObject
        { ["ordinal"] = decisionOrdinal, ["instanceId"] = instanceId, ["outcome"] = outcome.DeepClone() });
        ledger["nextHealingSourceOrdinal"] = checked(sourceOrdinal + 1);
        ledger["nextHealingDecisionOrdinal"] = checked(decisionOrdinal + 1);
        return true;
    }
}
