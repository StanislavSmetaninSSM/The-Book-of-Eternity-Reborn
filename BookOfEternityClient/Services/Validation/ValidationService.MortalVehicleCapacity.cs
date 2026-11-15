using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    // One scan of the already projected operation DATA, never a fresh file read or
    // a reusable permission. Existing publication before-images still fence it.
    private static Dictionary<string, JsonObject> ValidateMortalVehicleBirthCapacity(
        JsonObject rawVehicles,
        MortalVehicleBirthPreparation preparation,
        MortalItemCanonicalProjectionResult? itemPhase,
        IReadOnlyDictionary<string, string> creationIds,
        int sourceTurn,
        List<ValidationIssue> issues)
    {
        var evidence = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var catalog = itemPhase is null
            ? MortalItemCarrierCatalog.Build(new MortalItemCarrierCatalogInput(
                null, null, null, null, null, new Dictionary<string, JsonObject>()))
            : MortalItemCanonicalProjectionPlanner.BuildCatalog(
                itemPhase.ItemPhaseAfterImages, includeNpcCommands: true, preparation.Ids);
        ValidateCanonicalContainerPaths(catalog, issues);
        if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
            return evidence;
        if (rawVehicles["UpdateVehicles"] is not JsonArray updates)
            return evidence;
        for (var i = 0; i < updates.Count; i++)
        {
            if (updates[i] is not JsonObject birth ||
                birth["vehicleRef"] is not JsonValue reference ||
                !reference.TryGetValue<string>(out var vehicleRef) ||
                !preparation.TryResolve(vehicleRef, out var vehicleId))
                continue;
            var result = MortalCargoBirthCapacity.Validate(vehicleId, birth, catalog,
                creationIds, sourceTurn, $"{StorageTransportMoveService.VehiclesPath}.UpdateVehicles[{i}]");
            issues.AddRange(result.Issues);
            if (result.IsValid && result.Evidence is not null)
                evidence.Add(vehicleId, result.Evidence);
        }
        return evidence;
    }
}
