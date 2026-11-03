using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Reuses the existing original witness builder over exhausted owned intervals and actual claims.
        /// No caller supplies a witness or permission; current C3 input/resource checks precede this DATA composition.
        /// </summary>
        private IReadOnlyList<JsonObject> ComposeCompletedZeroStrainWitnesses(string originalFingerprint)
        {
            if (!_source.TryReadInitialActiveExchangeInventory(out _, out var exchanges))
                throw new InvalidOperationException("Exact completed original exchange inventory required.");
            var dice = _source.ReadOriginalAcceptedD20Values();
            var inventory = ReadC1ImageInventoryCore();
            var bounds = new JsonObject { ["exchangeCount"] = exchanges.Length,
                ["sourceSlotCount"] = checked(2 * exchanges.Length), ["diceCount"] = dice.Length,
                ["imagePathCount"] = inventory.RegisteredPaths.Count };
            var result = new List<JsonObject>();
            var sourceOrdinal = 0;
            foreach (var entry in _closedExchangeEvidence)
            {
                var claims = new JsonArray();
                foreach (var die in entry.DiceClaims.OrderBy(row => row.SourceIndex))
                {
                    var claim = new JsonObject { ["sourceIndex"] = die.SourceIndex, ["value"] = die.Value,
                        ["exchangeOrdinal"] = entry.Interval.Ordinal, ["claimFingerprint"] = "" };
                    claim["claimFingerprint"] = SpiritualWoundTurnEvidence.ComputeClaimFingerprint(claim); claims.Add(claim);
                }
                foreach (var source in entry.Sources)
                {
                    if (source.Calculation.MaximumSeverityRank == 0 && source.GuaranteedSeverityRank is null &&
                        source.StrainPreimageJson is { } json &&
                        SpiritualWoundStateJson.Integer(SpiritualWoundStateJson.Parse(json)["version"]) == 2)
                        result.Add(ComposeFirstOfferSource(source, entry, sourceOrdinal, bounds, dice, claims, originalFingerprint));
                    sourceOrdinal++;
                }
            }
            return Array.AsReadOnly(result.ToArray());
        }
    }
}
