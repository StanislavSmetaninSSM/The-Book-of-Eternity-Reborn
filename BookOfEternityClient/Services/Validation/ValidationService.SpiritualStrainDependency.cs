using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>Recognizes only the owned player's exact missing strain step.</summary>
    internal static int? StrainDependencyIndex(ValidationIssue issue)
    {
        if (issue.Code != "spiritual_wound_strain_correction_required") return null;
        var match = Regex.Match(issue.FilePath,
            @"^activeConflict\.exchangeLog\[(\d+)\]\.after\.playerSideStrain$", RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.None,
            CultureInfo.InvariantCulture, out var index) ? index : null;
    }

    /// <summary>Compares one proved ordinary exchange with exactly its bounded extra strain step.</summary>
    internal sealed class SpiritualStrainDraftCorrection
    {
        private readonly JsonObject _original;
        private readonly string _ordinary;

        internal SpiritualStrainDraftCorrection(string pointer, JsonObject original)
        {
            Pointer = pointer;
            _original = original.DeepClone().AsObject();
            _ordinary = original["after"]!["playerSideStrain"]!.GetValue<string>();
            var rank = SpiritualWoundOpportunityMath.StrainRank(_ordinary);
            if (rank is < 0 or > 4) throw new InvalidOperationException("A proved ordinary strain is required.");
            ExpectedStrain = new[] { "clear", "strained", "fractured", "overwhelmed", "broken" }[Math.Min(4, rank + 1)];
            Fields = [pointer + "/after/playerSideStrain"];
        }

        internal string Pointer { get; }
        internal string ExpectedStrain { get; }
        internal IReadOnlyList<string> Fields { get; }

        internal bool Allows(JsonNode? candidate)
        {
            if (candidate is not JsonObject exchange || exchange["after"] is not JsonObject after ||
                after["playerSideStrain"] is not JsonValue strain || !strain.TryGetValue<string>(out var token) ||
                token != _ordinary && token != ExpectedStrain) return false;
            var comparison = exchange.DeepClone().AsObject();
            comparison["after"]!["playerSideStrain"] = _ordinary;
            return JsonNode.DeepEquals(_original, comparison);
        }

        internal bool IsCorrected(JsonNode? candidate) => Allows(candidate) &&
            candidate!["after"]!["playerSideStrain"]!.GetValue<string>() == ExpectedStrain;
    }

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        private sealed record OriginalStrainComparison(int Ordinal, string ConflictId, string ExchangeId,
            string Pointer, string ExchangeJson, string WoundId, string Realm, string ActorId,
            IReadOnlyList<WoundRootEffectBinding> RootEffects);

        /// <summary>Reproves the ordinary last pressure before selection; caller data never supplies this comparison.</summary>
        private async Task<OriginalStrainComparison?> ReadOriginalStrainComparisonAsync(
            FileSystemManager.CanonicalWriteLease lease, SpiritualWoundCaptureCheckpointState checkpoint)
        {
            if (!_source.TryReadInitialActiveExchangeInventory(out var conflictId, out var ids) ||
                conflictId is null ||
                _closedExchangeEvidence.Count + 1 != ids.Length || _coldProposedInputs is not null) return null;
            var original = _source.ReadMechanicsConflict(this);
            if (original["activeConflict"] is not JsonObject conflict ||
                conflict["exchangeLog"] is not JsonArray prior ||
                ReadC2CommittedInputLayer(checkpoint)[AfterlifeSpiritualConflictState.StatePath].Bytes is not { } bytes)
                return null;
            var baseline = SpiritualWoundDependentDraftPolicy.ReadStrictRoot(SpiritualWoundStateJson.DecodeUtf8JsonText(bytes));
            // Replacement and explicit-exchange carriers retain their existing fail-closed path in this bounded consumer.
            if (baseline.ContainsKey(AfterlifeSpiritualConflictState.ResponseField) ||
                AfterlifeSpiritualConflictState.ResolveRawExchange(original, baseline,
                    prior.Count + _closedExchangeEvidence.Count) is not { } raw ||
                raw.Exchange["exchangeId"]?.GetValue<string>() != ids[^1] ||
                !ConflictTokenEquals(AfterlifeSpiritualConflictState.GetNodeString(raw.Exchange["operationType"]), "pressure") ||
                !ConflictTokenEquals(AfterlifeSpiritualConflictState.GetNodeString(raw.Exchange["outcome"]), "setback") ||
                !ExchangeDiceAuditRequired(raw.Exchange, "setback") || raw.Exchange["diceAudit"] is not JsonObject ||
                raw.Exchange["before"] is not JsonObject before || raw.Exchange["after"] is not JsonObject after ||
                !JsonNode.DeepEquals(before["playerSideStrain"], after["playerSideStrain"])) return null;
            var lawful = await SpiritualWoundSourceSession.PreparedContinuation.CheckOriginalLastSourceFreeExchangeAsync(
                _source, lease, this, baseline, _nextResourceOrdinal,
                _terminal?.ExecutionOwners ?? _input.PlanningContext!.Owners, SourceResourceBaseline);
            return lawful ? new(_nextResourceOrdinal, conflictId, ids[^1], raw.Pointer,
                raw.Exchange.ToJsonString(), "", "", "", []) : null;
        }

        /// <summary>Requires the exact current actor, selected wound roots and single applicable contribution.</summary>
        internal SpiritualStrainDraftCorrection? ReadOwnedStrainCorrection(
            AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext mechanics,
            JsonObject conflict, JsonObject exchange)
        {
            var original = _c2SelectedDecision?.StrainOriginal;
            if (original is null || !mechanics.IsCurrent || mechanics.Ordinal != original.Ordinal ||
                original.Ordinal != _nextResourceOrdinal || original.Ordinal != _closedExchangeEvidence.Count ||
                conflict["conflictId"]?.GetValue<string>() != original.ConflictId ||
                exchange["exchangeId"]?.GetValue<string>() != original.ExchangeId ||
                SpiritualWoundSourceSession.ActorKey(conflict["playerSide"]?["leadContestant"] as JsonObject) !=
                    "player_soul:" + original.ActorId)
                return null;
            var applicable = mechanics.Contributions.Where(row => row.ResolvedSide == "player" &&
                row.Actor.Kind == "player" && row.Actor.TargetId == original.ActorId &&
                ConflictTokenEquals(row.Operation, "pressure")).ToArray();
            if (applicable.Length != 1 || applicable[0] is not { Profile: "spiritual_strain_burden" } contribution ||
                contribution.CurrentStacks != 1 || contribution.WoundSource.Kind != "wound" ||
                contribution.WoundSource.SourceId != original.WoundId ||
                contribution.WoundSource.Realm != original.Realm || contribution.Actor.Realm != original.Realm ||
                contribution.Magnitude.ValueKind != System.Text.Json.JsonValueKind.Number ||
                !contribution.Magnitude.TryGetInt32(out var magnitude) || magnitude != 1 ||
                !original.RootEffects.Any(root => root.EffectId == contribution.EffectId &&
                    root.DefinitionKey == contribution.WoundSource.DefinitionKey)) return null;
            var correction = new SpiritualStrainDraftCorrection(original.Pointer,
                JsonNode.Parse(original.ExchangeJson)!.AsObject());
            return correction.Allows(exchange) ? correction : null;
        }

        internal bool CanInspectStrainTerminalEcho(IReadOnlyList<ValidationIssue> issues) =>
            _c2SelectedDecision?.StrainOriginal is not null && issues.Count != 0 && issues.All(issue =>
                issue.Code == "spiritual_source_final_strain_mismatch" && issue.FilePath == "activeConflict.playerSideStrain");
    }
}
