using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed record SpiritualShiningPatientAdmission(WoundOwnerCoordinate Target, SpiritualAidLocality Locality,
    LocalInteractionScope Scope, string Fingerprint, JsonObject Profile, bool IsFactionHead);

internal static partial class SpiritualTreatmentParticipation
{
    // One treatment-menu operation validates each exact candidate once. Consumers
    // reuse this private DATA until the menu is exported; confirmation reloads it.
    internal static IReadOnlyList<SpiritualShiningPatientAdmission> ReadShiningPatientAdmissions(
        JsonObject? shining, JsonObject profiles, JsonObject? residents)
    {
        var admitted = new List<SpiritualShiningPatientAdmission>();
        try
        {
            var scope = ShiningCurrentHallContract.RequireCurrentScope(shining);
            var factions = (shining?["factions"] as JsonArray)?.OfType<JsonObject>().Where(f =>
                string.Equals(Text(f, "hallId"), scope.LocationId, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (factions is not { Length: 1 } || Text(factions[0], "hallId") != scope.LocationId) return admitted.AsReadOnly();
            var locality = SpiritualAidLocality.ForFaction(Text(factions[0], "factionId"), scope.LocationId!);
            foreach (var profile in (profiles["profiles"] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                if (Text(profile, "actorType") is not ("resident" or "radiant_actor")) continue;
                try
                {
                    admitted.Add(RequireShiningPatientAdmission(shining, profiles, residents,
                        new("shining_abode", Text(profile, "actorType"), ProfileId(profile), WoundCarrierCatalog.AfterlifeProfilesPath), locality));
                }
                catch (Exception failure) when (failure is JsonException or InvalidDataException or InvalidOperationException or ArgumentException or OverflowException) { }
            }
        }
        catch (Exception failure) when (failure is JsonException or InvalidDataException or InvalidOperationException or ArgumentException or OverflowException) { }
        return admitted.AsReadOnly();
    }

    // Operation-owned DATA. This does not issue consent, a lease, a die or publication authority.
    internal static SpiritualShiningPatientAdmission RequireShiningPatientAdmission(JsonObject? shining,
        JsonObject profiles, JsonObject? residents, WoundOwnerCoordinate target, SpiritualAidLocality locality)
    {
        if (target is not { Realm: "shining_abode", OwnerKind: "resident" or "radiant_actor", CarrierPath: WoundCarrierCatalog.AfterlifeProfilesPath } ||
            !ResourceMaterializationContract.IsExactIdentifier(target.OwnerId) || locality.Value is not SpiritualAidLocality.ShiningFactionHall selected)
            throw new InvalidDataException("Patient admission requires an exact Shining member/head and faction hall.");
        if (ShiningAbodeState.GetPreparedIncarnationPackageMode(shining) != ShiningAbodeState.PreparedIncarnationPackageMode.Absent)
            throw new InvalidDataException("Patient admission requires ordinary Shining mode without an incarnation handoff.");
        var scope = ShiningCurrentHallContract.RequireCurrentScope(shining, target.OwnerKind == "resident" ? residents : null);
        var factions = (shining?["factions"] as JsonArray)?.OfType<JsonObject>().Where(f =>
            string.Equals(Text(f, "factionId"), selected.FactionId, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (factions is not { Length: 1 } || Text(factions[0], "factionId") != selected.FactionId ||
            !SarefMainStoryState.IsPlayerVisibleShiningFaction(factions[0]) || Text(factions[0], "hallId") != selected.HallId ||
            scope.LocationId != selected.HallId || factions[0]["factionLifecycle"] is not JsonObject lifecycle ||
            Text(lifecycle, "state") is not ("active" or "weakened" or "leaderless"))
            throw new InvalidDataException("Patient admission lost its exact revealed current faction/hall.");
        var actors = (profiles["profiles"] as JsonArray)?.OfType<JsonObject>().Where(p =>
            string.Equals(Text(p, "actorType"), target.OwnerKind, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(ProfileId(p), target.OwnerId, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (actors is not { Length: 1 } || Text(actors[0], "actorType") != target.OwnerKind || ProfileId(actors[0]) != target.OwnerId ||
            !WoundCarrierCollectionAuthority.AfterlifeProfileMatches(actors[0], target) ||
            Text(actors[0], "lifeState") != "alive" || !AfterlifeProfileVisibility.IsVisibleToPlayer(actors[0]) ||
            Text(actors[0], "locationId") != selected.HallId || !LocalInteractionScopeService.IsAfterlifeActorLocal(scope, actors[0]) ||
            !HasShiningPatientMaterialization(actors[0]))
            throw new InvalidDataException("Patient admission requires one exact living visible materialized local profile.");
        var tuple = new JsonObject
        {
            ["target"] = SpiritualHealingRequestProjection.OwnerJson(target), ["locality"] = LocalityJson(locality),
            ["lifecycle"] = lifecycle.DeepClone(), ["factionVisibility"] = factions[0]["visibility"]?.DeepClone(),
            ["sarefVisibility"] = factions[0]["sarefVisibility"]?.DeepClone(),
            ["lifeState"] = actors[0]["lifeState"]?.DeepClone(), ["visibility"] = actors[0]["visibility"]?.DeepClone(),
            ["isPlayerVisible"] = actors[0]["isPlayerVisible"]?.DeepClone(),
            ["materialization"] = actors[0]["materialization"]?.DeepClone()
        };
        if (target.OwnerKind == "resident")
        {
            var rows = (residents?["entries"] as JsonArray)?.OfType<JsonObject>().Where(r =>
                string.Equals(Text(r, "residentId"), target.OwnerId, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (rows is not { Length: 1 } || Text(rows[0], "residentId") != target.OwnerId ||
                Text(rows[0], "ascensionState") != ShiningAbodeState.AscensionStateAscended ||
                Text(rows[0], "shiningFactionId") != selected.FactionId)
                throw new InvalidDataException("Patient admission lost its exact ascended resident membership.");
            // The exact profile was materialization-validated above. A patient
            // needs membership, not a second roster/provider capability projection.
            tuple["residentRole"] = rows[0]["residentRole"]?.DeepClone();
            tuple["ascensionState"] = rows[0]["ascensionState"]?.DeepClone();
            tuple["shiningFactionId"] = rows[0]["shiningFactionId"]?.DeepClone();
        }
        else
        {
            var leadership = factions[0]["leadership"] as JsonObject;
            var political = (shining?["shiningPoliticalActors"] as JsonArray)?.OfType<JsonObject>().Where(a =>
                string.Equals(Text(a, "actorId"), target.OwnerId, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (leadership is null || Text(leadership, "headActorType") != "radiant_actor" || Text(leadership, "headActorId") != target.OwnerId ||
                Text(leadership, "leadershipState") is not (ShiningAbodeState.LeadershipStateSecure or ShiningAbodeState.LeadershipStateContested) ||
                Text(lifecycle, "state") == "leaderless" || political is not { Length: 1 } || Text(political[0], "actorId") != target.OwnerId ||
                Text(political[0], "actorType") != "radiant_actor" || Text(political[0], "currentFactionId") != selected.FactionId ||
                Text(political[0], "politicalStatus") != "head")
                throw new InvalidDataException("Patient admission lost its exact occupied radiant head/political relationship.");
            tuple["leadership"] = leadership.DeepClone();
            tuple["currentFactionId"] = political[0]["currentFactionId"]?.DeepClone();
            tuple["politicalStatus"] = political[0]["politicalStatus"]?.DeepClone();
        }
        return new(target, locality, scope, WoundAcceptedTurnFingerprintWriter.Compute(new[]
            { "book_of_eternity.shining_patient_admission", "1", WoundAcceptedTurnFingerprintWriter.CanonicalJson(tuple) }),
            actors[0], target.OwnerKind == "radiant_actor");
    }
    private static bool HasShiningPatientMaterialization(JsonObject profile)
    {
        using var computation = FileSystemManager.MeasureCurrentPassiveOperation("SpiritualTreatment.ShiningPatient.Materialization");
        return ActorMaterializationContract.ValidateHistoricalAfterlifeProfile(JsonSerializer.SerializeToElement(profile),
            AfterlifeEntityProfileState.StatePath).Count == 0;
    }

}
