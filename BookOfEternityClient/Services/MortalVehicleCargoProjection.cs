using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Operation-local identity index from already admitted, frozen item routes.</summary>
internal static class MortalVehicleCargoProjection
{
    internal static IReadOnlyDictionary<string, string> BirthIds(MortalItemRouteAuthorityCatalog routes)
    {
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        var references = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var route in routes.ByCreationRef.Values.Where(route => route.Route == "new_vehicle_inventory"))
        {
            if (route.AuthorityKind != "new_vehicle" || route.Destination.Kind != "vehicle_inventory" ||
                !ResourceMaterializationContract.IsExactIdentifier(route.AuthorityId) ||
                !ResourceMaterializationContract.IsExactIdentifier(route.Destination.OwnerId))
                throw new InvalidDataException("Vehicle cargo requires exact admitted birth authority and carrier.");
            if (ids.TryGetValue(route.AuthorityId, out var oldId) && oldId != route.Destination.OwnerId ||
                references.TryGetValue(route.Destination.OwnerId, out var oldRef) && oldRef != route.AuthorityId)
                throw new InvalidDataException("Vehicle birth routes contain conflicting identity mappings.");
            ids[route.AuthorityId] = route.Destination.OwnerId;
            references[route.Destination.OwnerId] = route.AuthorityId;
        }
        return ids;
    }

    internal static bool HasCargo(JsonObject root) => root["UpdateVehicles"] is JsonArray updates &&
        updates.OfType<JsonObject>().Any(vehicle => vehicle["vehicleRef"] is not null &&
            vehicle["inventory"] is JsonArray inventory && inventory.Count > 0);
}
