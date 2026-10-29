using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class AfterlifeWoundHealingPlanner
{
    internal sealed partial class Continuation
    {
        internal const string ServiceSoulPath = "game_state/meta/soul_state.json";
        internal bool HasServicePayment => _data?.ServiceQuote is not null;

        // Compose before the existing common seal. A projected Soul may contain
        // other accepted domains, but its wallet must still be the exact original.
        internal JsonObject ComposeServiceSoulAfterImage(JsonObject? projected)
        {
            if (_published || _data?.ServiceQuote is not { } quote || _data.ServicePayment is not { } payment ||
                _parsedSourceRoots.GetValueOrDefault(ServiceSoulPath) is not JsonObject original)
                throw new InvalidDataException("Only the live service owner can compose its original wallet contribution.");
            if (AfterlifeHealingServicePlanner.PreparePayment(quote, payment.AttemptId, original) != payment)
                throw new InvalidDataException("Retained service payment differs from its actual original wallet.");
            return AfterlifeHealingServicePlanner.ComposeSoulAfterImage(projected ?? original, quote, payment.AttemptId, payment);
        }

        // Used both at assembly and just before the actual atomic publication.
        // The sealed common plan and its existing byte-for-byte C4 read-back own
        // physical payment evidence; a detached receipt/History amount cannot pass.
        internal void RequireServiceComposedImages(JsonObject soul, JsonObject profiles, JsonObject history)
        {
            if (!HasServicePayment) return;
            var quote = _data!.ServiceQuote!;
            var payment = _data.ServicePayment ?? throw new InvalidDataException("Service payment DATA is absent.");
            if (_published || WoundAcceptedTurnFingerprintWriter.CanonicalJson(soul["inkFeathers"]) != payment.WalletAfterJson)
                throw new InvalidDataException("Common Soul after-image does not contain the exact service charge.");
            var actor = (profiles["profiles"] as JsonArray)?.OfType<JsonObject>()
                .Where(p => WoundCarrierCollectionAuthority.AfterlifeProfileMatches(p, quote.Provider)).ToArray();
            if (actor is not { Length: 1 } ||
                AfterlifeHealingServiceContract.ReadPublicCurrencyProfile(quote.Provider, actor[0]) is not { } current ||
                current.Fingerprint != quote.ServiceFingerprint)
                throw new InvalidDataException("Accepted profile changes invalidate the current service quote.");
            var rows = history["transitions"] as JsonArray ?? throw new InvalidDataException("Service History is absent.");
            var index = rows.Count - _stages.Count;
            var result = index < 0 ? null : rows[index]?["transitionResult"] as JsonObject;
            if (result is null || result["schemaVersion"]?.GetValue<int>() != 7 || result["mode"]?.GetValue<string>() != "service" ||
                !JsonNode.DeepEquals(result["request"], _request) ||
                !JsonNode.DeepEquals(result["request"]?["payment"], payment.ToJson()) ||
                result["request"]?["attempt"]?["attemptId"]?.GetValue<string>() != payment.AttemptId)
                throw new InvalidDataException("Published History is not the exact paid attempt and wallet evidence.");
        }

        internal void RequireServicePublication(IReadOnlyDictionary<string, JsonObject> writes)
        {
            if (!HasServicePayment) return;
            if (!writes.TryGetValue(ServiceSoulPath, out var soul) ||
                !writes.TryGetValue(WoundCarrierCatalog.AfterlifeProfilesPath, out var profiles) ||
                !writes.TryGetValue(WoundHistoryState.HistoryPath, out var history))
                throw new InvalidDataException("Service cannot publish separately from Soul, wound/effects and History.");
            RequireServiceComposedImages(soul, profiles, history);
        }
    }
}
