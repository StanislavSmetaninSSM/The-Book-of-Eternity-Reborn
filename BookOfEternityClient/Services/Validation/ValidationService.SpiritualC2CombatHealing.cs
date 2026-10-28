using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed partial class SpiritualOriginalTurnCapture
    {
        private JsonObject? _combatHealingOutcome;
        private EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundInsertion? _registeredCombatHealingInsertion;

        // Called only inside the existing owned saved-decision/replay operation,
        // after the initial pair has been authenticated/reconstructed. No files
        // are published here; the same saved transport seals the real advance.
        private C2SelectedDecision ApplyC2CombatHealingDecision(SpiritualWoundDecisionPendingState priorPending)
        {
            if (!IsCurrentOwner || !_usesColdOriginalInputs || _combatHealingOrigin is null ||
                _effects is null || _resources is null || _combatHealingOutcome is not null ||
                _closedExchangeEvidence.Count != 1 || _effects.WoundReadVersion != 0 || _registeredWoundInsertions.Count != 0)
                throw new InvalidOperationException("Combat requires its original undecided initial C2 frontier.");
            var root = SpiritualWoundStateJson.Parse(SpiritualWoundDecisionPendingState.SerializeCanonical(priorPending));
            var packet = root["pending"]!.AsObject();
            if (!SpiritualWoundDecisionPendingState.HasPendingHealing(packet))
                throw new InvalidOperationException("An owner-derived unfinished healing source is required.");
            var row = packet["healingSources"]![0]!.AsObject();
            var interval = _closedExchangeEvidence[0].Interval;
            var exchange = SpiritualWoundStateJson.Parse(_closedExchangeEvidence[0].ExchangeJson);
            var source = _closedExchangeEvidence[0].Healing ?? throw new InvalidOperationException("Actual retained healing source required.");
            if (!JsonNode.DeepEquals(row["witness"], SpiritualWoundStateJson.Parse(source.WitnessJson)))
                throw new InvalidDataException("Healing packet differs from the real retained original source.");
            var spend = CombatHealingSelection.ReadCombatSpend(interval, exchange);
            var issues = new List<ValidationIssue>();
            var selection = CombatHealingSelection.CaptureAtClosedInterval(this, interval, issues);
            if (issues.Count != 0 || source.Disposition == "resolved" && selection is null)
                throw new InvalidDataException("Healing selection could not be admitted from the actual owners.");
            SpiritualCombatHealingPersistedResult? result = null;
            if (selection is not null)
            {
                var before = _effects.AdvanceForWoundInsertion(selection, issues);
                if (before is null || issues.Count != 0) throw new InvalidDataException("Combat wound-before admission failed.");
                var prepared = before.Prepare(); issues.AddRange(prepared.Issues);
                if (!prepared.Success || issues.Count != 0) throw new InvalidDataException("Combat preparation failed.");
                var insertion = before.Apply(issues);
                if (insertion is null || issues.Count != 0) throw new InvalidDataException("Combat wound insertion failed.");
                var registered = _resources.RegisterWoundInstances(insertion, issues);
                if (registered is null || issues.Count != 0) throw new InvalidDataException("Combat routing registration failed.");
                _effects.AcceptInstalledWoundRouting(insertion, _resources);
                _registeredCombatHealingInsertion = insertion;
                result = SpiritualCombatHealingPersistedResult.Create(selection, insertion.CombatStages);
            }
            var decision = new JsonObject
            {
                ["sourceId"] = source.SourceId, ["sourceOrdinal"] = 0,
                ["waveOrdinal"] = packet["cursor"]!["waveOrdinal"]!.DeepClone(), ["disposition"] = source.Disposition,
                ["resourceTransition"] = JsonSerializer.SerializeToNode(spend), ["result"] = result?.ToCanonicalJson(), ["decisionFingerprint"] = ""
            };
            decision["decisionFingerprint"] = SpiritualWoundStateJson.Hash(decision, "staged_combat_healing", "decisionFingerprint");
            SpiritualWoundDecisionPendingState.ValidateHealingDecision(decision, row);
            packet["healingDecisions"]!.AsArray().Add(decision.DeepClone());
            packet["cursor"]!["nextHealingSourceOrdinal"] = 1;
            _combatHealingOutcome = decision.DeepClone().AsObject();
            var staged = new JsonObject { ["family"] = "combat_healing", ["decisionFingerprint"] = decision["decisionFingerprint"]!.DeepClone(), ["outcome"] = decision.DeepClone() };
            return new(root, staged, interval, null, null);
        }
    }
}
