using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Detached reference DATA only; not an allocation or publication authority.</summary>
internal sealed record MortalVehicleBirthLocationResult(
    IReadOnlyList<ValidationIssue> Issues,
    IReadOnlyDictionary<string, string> LocationIdsByVehicleRef)
{
    internal bool IsValid => Issues.Count == 0;
}

internal static partial class MortalLocationAcceptedTurnPlanner
{
    /// <summary>Resolves birth locations from the caller's already accepted, fenced map and
    /// existing same-turn allocation mapping. Does not read, allocate, mutate or publish.</summary>
    internal static MortalVehicleBirthLocationResult ValidateVehicleBirthLocations(
        JsonObject vehicleRoot, JsonObject acceptedMap,
        IReadOnlyDictionary<string, string> locationIdsByInitialId)
    {
        var issues = new List<ValidationIssue>();
        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        var canonical = new ExactIdentityIndex();
        var selectors = new ExactIdentityIndex();
        var mortalLocations = new HashSet<string>(StringComparer.Ordinal);
        if (acceptedMap["locations"] is JsonArray locations)
            foreach (var location in locations.OfType<JsonObject>())
                if (ReadExactString(location, "locationId") is { } id)
                {
                    canonical.Add(id);
                    selectors.Add(id);
                    if (ReadExactString(location, "realm") == "mortal_world") mortalLocations.Add(id);
                }
        // Copy mappings with ordinal identity even if a caller supplies a dictionary
        // with a permissive comparer. These are operation-local values, not a cache.
        var mappings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in locationIdsByInitialId)
        {
            mappings.Add(pair.Key, pair.Value);
            selectors.Add(pair.Key);
        }
        if (vehicleRoot["UpdateVehicles"] is JsonArray updates)
            for (var i = 0; i < updates.Count; i++)
            {
                if (updates[i] is not JsonObject birth ||
                    ReadExactString(birth, "vehicleRef") is not { } vehicleRef ||
                    birth.ContainsKey("vehicleId") || birth["currentLocationId"] is null)
                    continue;
                var selector = ReadExactString(birth, "currentLocationId");
                var resolution = selector is null ? MortalLocationReferenceResolution.Missing : selectors.Resolve(selector);
                string? target = null;
                if (resolution == MortalLocationReferenceResolution.Exact)
                {
                    target = mappings.TryGetValue(selector!, out var mapped) ? mapped : selector;
                    resolution = canonical.Resolve(target!);
                }
                var realmInvalid = resolution == MortalLocationReferenceResolution.Exact &&
                    (ReadExactString(acceptedMap, "realm") != "mortal_world" || !mortalLocations.Contains(target!));
                if (resolution != MortalLocationReferenceResolution.Exact || realmInvalid)
                {
                    var suffix = realmInvalid ? "realm_invalid" : resolution switch
                    {
                        MortalLocationReferenceResolution.Confusable => "confusable",
                        MortalLocationReferenceResolution.Ambiguous => "ambiguous",
                        _ => "unknown"
                    };
                    issues.Add(new ValidationIssue(
                        $"{StorageTransportMoveService.VehiclesPath}.UpdateVehicles[{i}].currentLocationId",
                        IssueSeverity.Error,
                        "Vehicle birth location must resolve to one exact accepted Mortal location.",
                        code: "resource_owner_vehicle_birth_location_" + suffix,
                        section: "Vehicles",
                        expected: "exact accepted locationId or exact same-turn location initialId",
                        actual: selector ?? birth["currentLocationId"]!.ToJsonString(),
                        repairHint: "Correct only this birth's currentLocationId using an exact accepted locationId or declared same-turn initialId. Do not invent locations, allocate permanent IDs, or edit canonical location evidence."));
                    continue;
                }
                resolved.Add(vehicleRef, target!);
            }
        // Invalid input never exposes a partial rewrite plan.
        if (issues.Count != 0) resolved.Clear();
        return new MortalVehicleBirthLocationResult(issues.AsReadOnly(),
            new ReadOnlyDictionary<string, string>(resolved));
    }
}
