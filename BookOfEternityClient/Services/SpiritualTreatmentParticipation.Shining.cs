using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record SpiritualProviderAdmission(SpiritualAidLocality Locality,
    LocalInteractionScope Scope, string Fingerprint);

internal static partial class SpiritualTreatmentParticipation
{
    // Parsed, operation-owned source DATA only. The caller retains signed original,
    // physical read-set and publication fences; no access survives this operation.
    internal static SpiritualProviderAdmission RequireShiningProviderAdmission(JsonObject? guardians,
        JsonObject? residents, JsonObject profiles, JsonObject? shining, WoundOwnerCoordinate healer,
        SpiritualAidLocality locality)
    {
        RequireProviderLocality(healer, locality);
        if (healer.Realm != "shining_abode")
            throw new InvalidDataException("Shining admission requires its exact provider realm.");
        LocalInteractionScope scope;
        JsonObject tuple;
        if (locality.Value is SpiritualAidLocality.ShiningFactionHall factionLocality)
        {
            scope = ShiningCurrentHallContract.RequireCurrentScope(shining, residents);
            var factions = (shining?["factions"] as JsonArray)?.OfType<JsonObject>().Where(f =>
                string.Equals(Text(f, "factionId"), factionLocality.FactionId, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (factions is not { Length: 1 } || Text(factions[0], "factionId") != factionLocality.FactionId ||
                !SarefMainStoryState.IsPlayerVisibleShiningFaction(factions[0]) ||
                Text(factions[0], "hallId") != scope.LocationId ||
                factions[0]["factionLifecycle"] is not JsonObject lifecycle ||
                Text(lifecycle, "state") is not ("active" or "weakened" or "leaderless"))
                throw new InvalidDataException("Negotiated treatment requires its exact current visible faction and hall.");
            var members = ShiningFactionHealingSupport.ProjectVisibleRoster(factions[0], residents, profiles)
                .Where(m => m.ResidentId == healer.OwnerId && m.HealingTier is not null).ToArray();
            var rows = (residents?["entries"] as JsonArray)?.OfType<JsonObject>().Where(r =>
                string.Equals(Text(r, "residentId"), healer.OwnerId, StringComparison.OrdinalIgnoreCase)).ToArray();
            var actors = (profiles["profiles"] as JsonArray)?.OfType<JsonObject>().Where(p =>
                WoundCarrierCollectionAuthority.AfterlifeProfileMatches(p, healer)).ToArray();
            if (members is not { Length: 1 } || rows is not { Length: 1 } || actors is not { Length: 1 } ||
                Text(rows[0], "residentId") != healer.OwnerId ||
                !ShiningFactionHealingSupport.IsCurrentHealerResident(rows[0], factionLocality.FactionId) ||
                !LocalInteractionScopeService.IsAfterlifeActorLocal(scope, actors[0]))
                throw new InvalidDataException("Negotiated treatment lost its exact living materialized faction healer.");
            tuple = new JsonObject
            {
                ["factionId"] = factionLocality.FactionId, ["hallId"] = scope.LocationId,
                ["lifecycle"] = lifecycle.DeepClone(), ["factionVisibility"] = factions[0]["visibility"]?.DeepClone(),
                ["sarefVisibility"] = factions[0]["sarefVisibility"]?.DeepClone(),
                ["residentId"] = healer.OwnerId, ["ascensionState"] = rows[0]["ascensionState"]?.DeepClone(),
                ["residentRole"] = rows[0]["residentRole"]?.DeepClone(),
                ["affiliation"] = rows[0]["shiningFactionId"]?.DeepClone(),
                ["healingTier"] = members[0].HealingTier,
                ["lifeState"] = actors[0]["lifeState"]?.DeepClone(),
                ["visibility"] = actors[0]["visibility"]?.DeepClone(),
                ["isPlayerVisible"] = actors[0]["isPlayerVisible"]?.DeepClone()
            };
        }
        else if (locality.Value is SpiritualAidLocality.ElyaraShiningHall)
        {
            scope = ElyaraVisitService.RequirePlayerScope("shining_abode", guardians
                ?? throw new InvalidDataException("Negotiated Elyara requires her actual registry."), shining);
            var actor = ElyaraVisitService.RequireCanonicalActor(profiles);
            if (!WoundCarrierCollectionAuthority.AfterlifeProfileMatches(actor, healer) ||
                Text(actor, "lifeState") != "alive" || !AfterlifeProfileVisibility.IsVisibleToPlayer(actor) ||
                !LocalInteractionScopeService.IsAfterlifeActorLocal(scope, actor))
                throw new InvalidDataException("Negotiated Elyara is no longer alive, visible and co-located.");
            tuple = new JsonObject
            {
                ["hallId"] = scope.LocationId, ["lifeState"] = actor["lifeState"]?.DeepClone(),
                ["visibility"] = actor["visibility"]?.DeepClone(), ["isPlayerVisible"] = actor["isPlayerVisible"]?.DeepClone()
            };
        }
        else throw new InvalidDataException("Unsupported negotiated Shining provider locality.");
        if (locality.LocationId != scope.LocationId)
            throw new InvalidDataException("Provider offer differs from the actual current hall.");
        tuple["provider"] = SpiritualHealingRequestProjection.OwnerJson(healer);
        tuple["locality"] = LocalityJson(locality);
        return new(locality, scope, WoundAcceptedTurnFingerprintWriter.Compute(new[]
            { "negotiated_provider_admission", WoundAcceptedTurnFingerprintWriter.CanonicalJson(tuple) }));
    }
}
