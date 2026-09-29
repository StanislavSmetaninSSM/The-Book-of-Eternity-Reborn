using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Retains one original signed D20 claim with its executed exchange coordinate.
    /// </summary>
    /// <param name="SourceIndex">
    /// Zero-based index in the original accepted D20 pool.
    /// </param>
    /// <param name="Value">
    /// Signed value at <paramref name="SourceIndex"/>.
    /// </param>
    internal sealed record SpiritualClosedD20Claim(int SourceIndex, int Value);

    /// <summary>
    /// Carries detached facts from one accepted closed exchange; it grants no historical wound admission.
    /// </summary>
    /// <param name="ConflictId">
    /// Original continuing conflict identity.
    /// </param>
    /// <param name="ExchangeId">
    /// Accepted closed exchange identity.
    /// </param>
    /// <param name="Ordinal">
    /// Zero-based source-owner accepted exchange position.
    /// </param>
    /// <param name="ExchangeJson">
    /// Exact validated source exchange JSON.
    /// </param>
    /// <param name="Sources">
    /// Detached harmful source facts, including zero-ceiling sources.
    /// </param>
    /// <param name="DiceClaims">
    /// Signed die claims from this exchange, including source-free dice.
    /// </param>
    /// <param name="EffectBeforeFingerprint">
    /// Owned effect boundary fingerprint before this exchange.
    /// </param>
    /// <param name="EffectAfterFingerprint">
    /// Owned effect boundary fingerprint after this exchange.
    /// </param>
    internal sealed record SpiritualClosedExchangeEvidence(
        string ConflictId, string ExchangeId, int Ordinal, string ExchangeJson,
        IReadOnlyList<PreparedSpiritualSource> Sources,
        IReadOnlyList<SpiritualClosedD20Claim> DiceClaims,
        string EffectBeforeFingerprint, string EffectAfterFingerprint);

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Holds source-owner evidence alongside the resource interval that closed it.
        /// </summary>
        /// <param name="Interval">
        /// Exact interval owned by this capture's resource executor.
        /// </param>
        /// <param name="ExchangeJson">
        /// Validated exchange JSON retained by the source owner.
        /// </param>
        /// <param name="Sources">
        /// Sources admitted by the source owner for this exchange.
        /// </param>
        /// <param name="DiceClaims">
        /// Original signed dice used by this exchange.
        /// </param>
        /// <param name="ConflictBeforeFingerprint">
        /// Source-owned conflict fingerprint immediately before the exchange.
        /// </param>
        /// <param name="ConflictAfterFingerprint">
        /// Source-owned conflict fingerprint at the closed exchange frontier.
        /// </param>
        /// <param name="ResourceResultFingerprint">
        /// Fingerprint of the exact closed resource interval result.
        /// </param>
        /// <param name="EffectPlanFingerprint">
        /// Fingerprint of the base effect plan and this interval's effect prefix.
        /// </param>
        /// <param name="DangerDeclarationFingerprint">
        /// Fingerprint of the retained original danger declaration.
        /// </param>
        /// <param name="ConflictAfter">
        /// Detached source-owned conflict root at the closed frontier.
        /// </param>
        private sealed record RetainedClosedExchange(
            AcceptedMechanicsPlanner.SpiritualExchangeInterval Interval,
            string ExchangeJson, PreparedSpiritualSource[] Sources,
            SpiritualClosedD20Claim[] DiceClaims,
            string ConflictBeforeFingerprint, string ConflictAfterFingerprint,
            string ResourceResultFingerprint, string EffectPlanFingerprint,
            string DangerDeclarationFingerprint, JsonObject ConflictAfter);

        private readonly List<RetainedClosedExchange> _closedExchangeEvidence = [];

        /// <summary>
        /// Reads the contiguous closed original exchange prefix under the current capture gate.
        /// Returned sources are comparison copies, not actionable admission capabilities.
        /// </summary>
        /// <param name="lease">
        /// Active canonical write lease authenticating this capture.
        /// </param>
        /// <returns>
        /// Detached closed exchange evidence in source-owner accepted order.
        /// </returns>
        internal IReadOnlyList<SpiritualClosedExchangeEvidence> ReadClosedExchangeEvidence(
            FileSystemManager.CanonicalWriteLease lease)
        {
            if (!_gate.Wait(0))
                throw new InvalidOperationException("The original capture is continuing.");
            try
            {
                EnsureCurrent(lease);
                if (_closedExchangeEvidence.Count != _nextResourceOrdinal ||
                    _resources == null && _closedExchangeEvidence.Count != 0)
                    throw new InvalidOperationException("The closed exchange prefix is incomplete.");
                var copies = new List<SpiritualClosedExchangeEvidence>(_closedExchangeEvidence.Count);
                for (var ordinal = 0; ordinal < _closedExchangeEvidence.Count; ordinal++)
                {
                    var retained = _closedExchangeEvidence[ordinal];
                    var interval = retained.Interval;
                    if (interval.Ordinal != ordinal || _resources?.Owns(interval) != true)
                        throw new InvalidOperationException("The closed interval lost its exact owner.");
                    copies.Add(new(interval.ConflictId, interval.ExchangeId, interval.Ordinal,
                        retained.ExchangeJson,
                        Array.AsReadOnly(retained.Sources.Select(source => source with { }).ToArray()),
                        Array.AsReadOnly(retained.DiceClaims.Select(claim => claim with { }).ToArray()),
                        interval.EffectBefore.Fingerprint, interval.EffectAfter.Fingerprint));
                }
                return Array.AsReadOnly(copies.ToArray());
            }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// Retains a validated exchange, its original dice and all admitted source facts once.
        /// The caller holds the capture gate after committing the exact source continuation.
        /// </summary>
        /// <param name="interval">
        /// Newly closed interval owned by this capture's resource executor.
        /// </param>
        private void RetainClosedExchangeEvidence(AcceptedMechanicsPlanner.SpiritualExchangeInterval interval)
        {
            if (_resources == null || !_resources.Owns(interval) ||
                interval.Ordinal != _closedExchangeEvidence.Count ||
                interval.Ordinal >= _source.CheckedExchanges.Count ||
                JsonNode.Parse(_source.CheckedExchanges[interval.Ordinal]) is not JsonObject exchange ||
                exchange["exchangeId"] is not JsonValue idValue ||
                !idValue.TryGetValue<string>(out var exchangeId) || exchangeId != interval.ExchangeId)
                throw new InvalidOperationException("The closed exchange does not match its original source owner.");
            var sources = _source.Sources.Where(source =>
                source.ConflictId == interval.ConflictId && source.ExchangeId == interval.ExchangeId).ToArray();
            if (sources.Length > 2 || sources.Any(source => !_source.Owns(source)) ||
                sources.Select(source => source.AffectedSide).Distinct(StringComparer.Ordinal).Count() != sources.Length)
                throw new InvalidOperationException("The closed exchange has contradictory source evidence.");
            var pool = _source.ReadOriginalAcceptedD20Values();
            var seen = _closedExchangeEvidence.SelectMany(entry => entry.DiceClaims)
                .Select(claim => claim.SourceIndex).ToHashSet();
            var claims = new List<SpiritualClosedD20Claim>();
            if (exchange["diceAudit"] is JsonObject audit)
            {
                if (audit["diceUsed"] is not JsonArray used)
                    throw new InvalidOperationException("The closed exchange has no validated die list.");
                foreach (var node in used)
                {
                    if (node is not JsonObject die || die["sourceIndex"] is not JsonValue indexValue ||
                        !indexValue.TryGetValue<int>(out var index) || index < 0 || index >= pool.Length ||
                        die["value"] is not JsonValue valueNode || !valueNode.TryGetValue<int>(out var value) ||
                        value != pool[index] || !seen.Add(index))
                        throw new InvalidOperationException("The closed exchange has a foreign or reused original die.");
                    claims.Add(new(index, value));
                }
            }
            else if (exchange["diceAudit"] is not null)
                throw new InvalidOperationException("The closed exchange has malformed die evidence.");
            var original = _source.ReadOriginalConflict();
            var prior = _closedExchangeEvidence.Count == 0
                ? original
                : _closedExchangeEvidence[^1].ConflictAfter.DeepClone().AsObject();
            var after = _source.ReadExecutedConflictPrefix();
            var beforeFingerprint = SpiritualWoundStateJson.Hash(prior, "closed_conflict");
            var afterFingerprint = SpiritualWoundStateJson.Hash(after, "closed_conflict");
            var declaration = SpiritualConflictDangerPolicy.ReadDeclaration(
                original["activeConflict"] as JsonObject);
            if (declaration is null || _effects is null)
                throw new InvalidOperationException("The closed exchange lacks original danger or effect ownership.");
            var dangerFingerprint = SpiritualWoundStateJson.Hash(new JsonObject
            {
                ["dangerMode"] = declaration
            }, "danger_declaration");
            var resourceFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
            {
                "book_of_eternity.spiritual_closed_resource_result", "1",
                interval.ConflictId, interval.ExchangeId,
                interval.Ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture),
                JsonSerializer.Serialize(interval.AppliedTransitions),
                JsonSerializer.Serialize(interval.ReplayTransitions),
                JsonSerializer.Serialize(interval.Events)
            });
            var effectFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
            {
                "book_of_eternity.spiritual_closed_effect_prefix", "1",
                _effects.ReadBasePlanPayloadFingerprint(), interval.EffectAfter.Fingerprint
            });
            _closedExchangeEvidence.Add(new(interval, _source.CheckedExchanges[interval.Ordinal],
                sources, claims.ToArray(), beforeFingerprint, afterFingerprint,
                resourceFingerprint, effectFingerprint, dangerFingerprint,
                after.DeepClone().AsObject()));
        }
    }
}
