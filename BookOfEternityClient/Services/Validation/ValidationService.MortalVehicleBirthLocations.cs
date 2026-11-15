using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private static bool AttachVehicleBirthLocations(ResourceOwnerCompositionResult owners,
        MortalVehicleBirthPreparation preparation, MortalVehicleBirthLocationResult locations,
        List<ValidationIssue> issues)
    {
        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in locations.LocationIdsByVehicleRef)
        {
            if (!preparation.TryResolve(pair.Key, out var vehicleId))
                return Invalid();
            resolved.Add(vehicleId, pair.Value);
        }
        var attached = 0;
        if (owners.OwnerCompanionAfterImages.TryGetValue(StorageTransportMoveService.VehiclesPath, out var root) &&
            root["vehicles"] is JsonArray vehicles)
            foreach (var vehicle in vehicles.OfType<JsonObject>())
                if (vehicle["vehicleId"] is JsonValue id && id.TryGetValue<string>(out var vehicleId) &&
                    resolved.TryGetValue(vehicleId, out var locationId))
                {
                    // Change only the detached ordinary owner after-image, never raw
                    // preparation inputs or accepted location plan/creation receipts.
                    vehicle["currentLocationId"] = locationId;
                    attached++;
                }
        return attached == resolved.Count || Invalid();

        bool Invalid()
        {
            issues.Add(ResourceIssue(StorageTransportMoveService.VehiclesPath,
                "resource_owner_vehicle_birth_location_invalid",
                "one final vehicle companion for every exact birth location binding", "missing identity or companion"));
            return false;
        }
    }
}
