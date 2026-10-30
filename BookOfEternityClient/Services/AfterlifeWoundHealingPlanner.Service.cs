using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class AfterlifeWoundHealingPlanner
{
    internal sealed partial class Continuation
    {
        internal const string ServiceSoulPath = "game_state/meta/soul_state.json";
        internal bool HasServicePayment => _data?.ServiceQuote is not null;
        internal int? ServiceAmount => _data?.ServicePayment?.Amount;
        internal string? ServiceWoundId => _data?.ServiceQuote?.WoundId;
        private string? _expectedServiceAfterImageAdmissionFingerprint;

        /// <summary>Binds existing client projector DATA to this live continuation before comparing any proposed image.</summary>
        /// <param name="projectedProfiles">The existing projection from signed originals and this owner's retained accepted report.</param>
        private void BindProjectedServiceAdmission(JsonObject projectedProfiles)
        {
            if (!HasServicePayment) return;
            RequireExecutionCurrent();
            var quote = _data!.ServiceQuote!;
            var admission = AfterlifeWoundTreatmentAuthority.ReadCurrentServiceAdmission(
                _parsedSourceRoots.GetValueOrDefault(AfterlifeWoundTreatmentAuthority.ServiceLocalityPaths[0]) as JsonObject,
                projectedProfiles, quote.Provider, quote.LocationRef,
                _parsedSourceRoots.GetValueOrDefault(ShiningAbodeState.StatePath) as JsonObject,
                _parsedSourceRoots.GetValueOrDefault(GuardianAbodeResidentState.StatePath) as JsonObject);
            if (_expectedServiceAfterImageAdmissionFingerprint is not null &&
                _expectedServiceAfterImageAdmissionFingerprint != admission.ProviderFingerprint)
                throw new InvalidDataException("The owned client-projected service admission changed within its operation.");
            _expectedServiceAfterImageAdmissionFingerprint = admission.ProviderFingerprint;
        }

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
        /// <summary>Checks exact composed payment, provider tuple and History at both existing publication boundaries.</summary>
        /// <param name="soul">The actual proposed Soul after-image.</param>
        /// <param name="profiles">The actual proposed wound/profile carrier.</param>
        /// <param name="history">The actual proposed immutable treatment History.</param>
        /// <param name="residents">A composed roster, or this owner's already checked original/normalized roster.</param>
        /// <param name="shining">A composed Shining state, or this owner's already checked original state.</param>
        internal void RequireServiceComposedImages(JsonObject soul, JsonObject profiles, JsonObject history,
            JsonObject? residents = null, JsonObject? shining = null)
        {
            if (!HasServicePayment) return;
            var data = _data ?? throw new InvalidDataException("Service source DATA is absent.");
            var quote = data.ServiceQuote!;
            var payment = data.ServicePayment ?? throw new InvalidDataException("Service payment DATA is absent.");
            if (_published || WoundAcceptedTurnFingerprintWriter.CanonicalJson(soul["inkFeathers"]) != payment.WalletAfterJson)
                throw new InvalidDataException("Common Soul after-image does not contain the exact service charge.");
            if (_expectedServiceAfterImageAdmissionFingerprint is null)
                throw new InvalidDataException("Service after-image has no owned client projection.");
            var admission = AfterlifeWoundTreatmentAuthority.ReadCurrentServiceAdmission(
                _parsedSourceRoots.GetValueOrDefault(AfterlifeWoundTreatmentAuthority.ServiceLocalityPaths[0]) as JsonObject,
                profiles, quote.Provider, quote.LocationRef, shining ?? _parsedSourceRoots.GetValueOrDefault(ShiningAbodeState.StatePath) as JsonObject,
                residents ?? _parsedSourceRoots.GetValueOrDefault(GuardianAbodeResidentState.StatePath) as JsonObject);
            if (admission.ProviderFingerprint != _expectedServiceAfterImageAdmissionFingerprint)
                throw new InvalidDataException("Accepted membership, locality or profile changes invalidate the exact service provider.");
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
            RequireServiceComposedImages(soul, profiles, history,
                writes.GetValueOrDefault(GuardianAbodeResidentState.StatePath),
                writes.GetValueOrDefault(ShiningAbodeState.StatePath));
        }
    }
}
