using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private void ValidateMortalVehicleBirthShape(JsonObject? original, JsonObject current,
        List<ValidationIssue> issues)
    {
        // Reuse already authenticated original DATA. This mutable simulation is local
        // to one call, never a cache or publication authority. Explicit empty state
        // must not fall back to another signed-original read.
        var states = new Dictionary<string, VehicleStateSnapshot>(StringComparer.OrdinalIgnoreCase);
        if (original?["vehicles"] is JsonArray vehicles)
        {
            foreach (var vehicle in vehicles.OfType<JsonObject>())
            {
                // State simulation needs only these scalars, not potentially large cargo.
                var state = new JsonObject();
                foreach (var key in new[] { "vehicleId", "availability", "currentLocationId" })
                    if (vehicle.TryGetPropertyValue(key, out var node))
                        state[key] = node?.DeepClone();
                var element = JsonSerializer.SerializeToElement(state);
                var id = GetFirstNonEmptyString(element, "vehicleId");
                if (!string.IsNullOrWhiteSpace(id))
                    states[id] = ReadVehicleStateSnapshot(element);
            }
        }
        // Keep original coordinates and partial-update order. Raw cargo must remain
        // RawPreSeal here; canonical item identity belongs to the existing item phase.
        ValidateVehicleArray(JsonSerializer.SerializeToElement(current["UpdateVehicles"]),
            "game_state/misc/vehicles.json.UpdateVehicles", issues,
            requireCanonicalStoredShape: false, preTurnVehicleStates: states);
    }
}
