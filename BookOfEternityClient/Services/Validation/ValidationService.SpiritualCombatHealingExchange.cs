using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed partial class SpiritualOriginalTurnCapture
    {
        internal bool HasCombatHealingOrigin => IsCurrentOwner && _combatHealingOrigin is not null;

        // Prospective source preparation calls this only for comparison. It does
        // not register a selection, mutate this capture or issue draft authority.
        internal void ValidateCombatHealingDeclaration(JsonObject conflict, JsonObject exchange, int turnExchangeOrdinal)
        {
            _ = ResolveCombatHealingExchange(conflict, exchange, turnExchangeOrdinal);
        }

        private sealed record CombatHealingExchangeData(string SourceId, string EventRef,
            string Disposition, SpiritualHealingResolution Resolution, string WitnessJson, string ExchangeJson);

        private CombatHealingExchangeData ResolveCombatHealingExchange(JsonObject conflict, JsonObject exchange, int turnExchangeOrdinal)
        {
            if (!IsCurrentOwner || _combatHealingOrigin is not { } origin || !_usesOriginalPrefix ||
                _originalPrefix is null || _resources is null || _effects is null ||
                !OwnsMechanicsOwner(_resources, _source) || turnExchangeOrdinal != 0 ||
                AfterlifeSpiritualConflictState.GetNodeString(conflict["conflictId"]) != origin.Intent.ConflictId ||
                !ConflictTokenEquals(AfterlifeSpiritualConflictState.GetNodeString(conflict["sideModel"]), "direct_duel") ||
                !ConflictTokenEquals(AfterlifeSpiritualConflictState.GetNodeString(exchange["operationType"]), AfterlifeActionCostRules.SpiritualHealingOperation) ||
                ResolveIncomingActionOperations(exchange).Any(operation => ConflictTokenEquals(operation, AfterlifeActionCostRules.SpiritualHealingOperation)) ||
                EnumerateSpecialArtAuditObjects(exchange).Any(SpecialArtAuditOwnerIsPlayer))
                throw new InvalidOperationException("The actual combat intent requires exactly its next standard player-self healing exchange.");
            var id = AfterlifeSpiritualConflictState.GetNodeString(exchange["exchangeId"]);
            if (!ResourceMaterializationContract.IsExactIdentifier(id) || exchange["diceAudit"] is not JsonObject audit ||
                audit["diceUsed"] is not JsonArray dice)
                throw new InvalidOperationException("Combat healing requires its admitted exchange and original dice audit.");
            var rows = dice.OfType<JsonObject>().Where(row => ConflictTokenEquals(
                AfterlifeSpiritualConflictState.GetNodeString(row["side"]), "player", "playerSide", "soul")).ToArray();
            var mode = (AfterlifeSpiritualConflictState.GetNodeString(audit["rollMode"]?["player"]?["effectiveMode"]) ?? "normal").ToLowerInvariant();
            var claim = new JsonArray();
            var values = new List<int>();
            var indices = new HashSet<int>();
            foreach (var row in rows)
            {
                if (row["sourceIndex"] is not JsonValue indexValue || !indexValue.TryGetValue<int>(out var index) ||
                    index < 0 || index >= origin.AcceptedDice.Count || !indices.Add(index) ||
                    row["value"] is not JsonValue value || !value.TryGetValue<int>(out var roll) || origin.AcceptedDice[index] != roll)
                    throw new InvalidOperationException("Combat healing must retain the actual admitted original player dice.");
                values.Add(roll);
                claim.Add(new JsonObject { ["sourceIndex"] = index, ["value"] = roll,
                    ["selection"] = row["selection"]?.DeepClone() });
            }
            var resolution = SpiritualHealingResolver.ResolvePrepared(origin.Source.Source, mode, values);
            if (!resolution.IsValid || resolution.Resolution is not { } resolved || !resolved.Calculation.HasSufficientTier)
                throw new InvalidOperationException("Combat healing and its C2 player roll have incompatible source or roll mode.");
            var selected = rows.Length == 1 ? rows[0] : rows.Single(row =>
                ConflictTokenEquals(AfterlifeSpiritualConflictState.GetNodeString(row["selection"]), "selected"));
            if (selected["value"]!.GetValue<int>() != resolved.Calculation.Input.NaturalRoll)
                throw new InvalidOperationException("Combat healing cannot replace the die selected by the admitted exchange.");
            var outcome = AfterlifeSpiritualConflictState.GetNodeString(exchange["outcome"])?.ToLowerInvariant();
            var disposition = outcome is "success" or "partial_success" ? "resolved"
                : outcome is "blocked" or "countered" or "setback" or "no_effect" ? "blocked"
                : throw new InvalidOperationException("Unknown combat healing action disposition.");
            var coordinate = WoundAcceptedTurnFingerprintWriter.Compute(new[]
                { "book_of_eternity.spiritual_combat_healing.source", "1", _source.SnapshotToken, origin.Intent.Fingerprint, id! })[7..];
            var sourceId = "spiritual_combat_healing_source_" + coordinate;
            var accepted = WoundAcceptedEventAuthorityComposer.ComposeDefaultAcceptedTurn(
                _source.SessionId, _source.RequestId, _source.SnapshotToken, _source.TurnNumber);
            if (!accepted.Success) throw new InvalidOperationException("Combat healing has no actual accepted-turn event.");
            var eventRef = accepted.Events.Single().EventRef;
            var witness = new JsonObject
            {
                ["schemaVersion"] = 1, ["kind"] = "spiritual_combat_healing_source", ["sourceId"] = sourceId, ["eventRef"] = eventRef,
                ["sessionId"] = _source.SessionId, ["requestId"] = _source.RequestId, ["snapshotToken"] = _source.SnapshotToken,
                ["turn"] = _source.TurnNumber, ["realm"] = "chaos_sea", ["conflictId"] = origin.Intent.ConflictId,
                ["exchangeId"] = id, ["exchangeOrdinal"] = origin.Intent.NextExchangeOrdinal, ["turnExchangeOrdinal"] = turnExchangeOrdinal,
                ["intent"] = origin.Intent.Root, ["beforeWound"] = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(origin.Source.Wound)),
                ["healingSource"] = JsonSerializer.SerializeToNode(origin.Source.Source), ["rollMode"] = mode,
                ["diceClaim"] = claim, ["calculation"] = JsonSerializer.SerializeToNode(resolved.Calculation), ["disposition"] = disposition,
                ["exchange"] = exchange.DeepClone(),
                ["exchangeFingerprint"] = WoundAcceptedTurnFingerprintWriter.Compute(new[] { WoundAcceptedTurnFingerprintWriter.CanonicalJson(exchange) })
            };
            witness["sourceFingerprint"] = WoundAcceptedTurnFingerprintWriter.Compute(new[]
                { "book_of_eternity.spiritual_combat_healing.witness", "1", WoundAcceptedTurnFingerprintWriter.CanonicalJson(witness) });
            return new(sourceId, eventRef, disposition, resolved, witness.ToJsonString(), exchange.ToJsonString());
        }
    }

    internal sealed partial class SpiritualWoundSourceSession
    {
        private void ValidateCombatHealingDeclaration(JsonObject conflict, JsonObject exchange)
        {
            var playerHealing = ConflictTokenEquals(ExactString(exchange["operationType"]), AfterlifeActionCostRules.SpiritualHealingOperation);
            var oppositionHealing = ResolveIncomingActionOperations(exchange).Any(operation =>
                ConflictTokenEquals(operation, AfterlifeActionCostRules.SpiritualHealingOperation));
            if (!playerHealing && !oppositionHealing && (_prefixCapture?.HasCombatHealingOrigin != true || _checkedExchanges.Count != 0)) return;
            if (_prefixCapture is null) throw new InvalidOperationException("Combat healing needs its actual original capture and intent.");
            _prefixCapture.ValidateCombatHealingDeclaration(conflict, exchange, _checkedExchanges.Count);
        }
    }
}
