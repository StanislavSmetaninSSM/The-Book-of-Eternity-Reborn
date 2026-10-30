using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class WoundPlayerProjection
{
    /// <summary>Formats only the exact fresh known target proven by the application's current navigation read.</summary>
    internal static WoundPlayerProjectionResult ProjectKnownTarget(WoundPlayerReadContext context, WoundTargetChoice target)
    {
        var input = context.Input;
        if (!context.Projection.IsAvailable || !input.Effects.IsAccepted || !input.SelfVisible ||
            input.Realm is not ("chaos_sea" or "shining_abode") || context.SpiritualConflict?["activeConflict"] is not null || !target.Available ||
            target.Owner.Realm != input.Realm || target.Owner.CarrierPath != WoundCarrierCatalog.AfterlifeProfilesPath ||
            (input.Realm == "chaos_sea" ? target.Owner.OwnerKind is not ("guardian" or "resident") :
                target.Owner.OwnerKind is not ("resident" or "radiant_actor")) ||
            target.Locality is not { } locality) return Unavailable();
        try
        {
            // SelectTarget built this admission from the actual current parsed
            // sources in the same operation, without retaining it in UI state.
            if (input.Realm == "shining_abode" && !context.ShiningPatients.Any(a =>
                    a.Target == target.Owner && a.Locality == locality)) return Unavailable();
            // This does not re-prove Guardian/roster roots: SelectTargetAsync just did so
            // under its actual lease. Exact typed DATA validation precedes normalized mapping.
            var profiles = (input.Carriers.AfterlifeProfiles?["profiles"] as JsonArray)?.OfType<JsonObject>()
                .Where(p => p["actorType"]?.GetValue<string>() == target.Owner.OwnerKind &&
                    string.Equals(SpiritualTreatmentParticipation.ProfileId(p), target.Owner.OwnerId, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (profiles is not { Length: 1 } ||
                SpiritualTreatmentParticipation.ProfileId(profiles[0]) != target.Owner.OwnerId ||
                !WoundCarrierCollectionAuthority.AfterlifeProfileMatches(profiles[0], target.Owner) ||
                !AfterlifeProfileVisibility.IsVisibleToPlayer(profiles[0]) ||
                profiles[0]["locationId"]?.GetValue<string>() != locality.LocationId ||
                target.Owner.OwnerKind == "guardian" && target.Owner.OwnerId != locality.GuardianId ||
                !AfterlifeEntityProfileState.TryResolveEffectTarget(profiles[0], out var effectTarget)) return Unavailable();
            var active = new List<WoundPlayerEntry>();
            foreach (var summary in target.Wounds)
            {
                if (!context.Catalog.TryResolveOne(summary.WoundId, out var occurrence) ||
                    !input.Identities.TryGetEntry(summary.WoundId, out var identity)) return Unavailable();
                var wound = occurrence.Wound;
                // Reuse the existing immutable identity index already verified against this
                // catalog/History in the current context; do not hash the same graph twice.
                if (wound.Owner != target.Owner || wound.Lifecycle != "active" ||
                    wound.Classification.Domain != "spiritual" || !Visible(wound.Display.Visibility) ||
                    identity.Status != "active" || identity.Domain != "spiritual" ||
                    identity.Realm != target.Owner.Realm || identity.OwnerKind != target.Owner.OwnerKind ||
                    identity.OwnerId != target.Owner.OwnerId || identity.CarrierPath != target.Owner.CarrierPath ||
                    identity.SemanticFingerprint != summary.Fingerprint) return Unavailable();
                active.Add(Project(wound, input, effectTarget.Kind));
            }
            return new(true, active.Count == 0 ? "У этой цели сейчас нет известных активных ран." : "", active.ToArray(), []);
        }
        catch (Exception failure) when (failure is JsonException or InvalidDataException or InvalidOperationException or ArgumentException)
        { return Unavailable(); }
    }
}
