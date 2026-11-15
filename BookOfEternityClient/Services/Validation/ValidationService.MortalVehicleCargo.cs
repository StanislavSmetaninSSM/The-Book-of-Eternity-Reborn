using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private static string? DecodeObservedCanonicalInput(CanonicalBeforeImage observation)
    {
        var bytes = observation.Bytes;
        if (bytes is null)
            return null;
        using var reader = new StreamReader(new MemoryStream(bytes), detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private sealed record VehicleBirthLocationInputs(
        IReadOnlyDictionary<string, CanonicalBeforeImage> Observations,
        MortalLocationAcceptedTurnPlan? LocationPlan, JsonObject AcceptedMap);

    private async Task<VehicleBirthLocationInputs?> CaptureFreshVehicleCargoInputsAsync(
        MortalItemAcceptedTurnNormalizationSnapshot? snapshot,
        FileSystemManager.CanonicalWriteLease lease,
        List<ValidationIssue> issues)
    {
        // Location projections also depend on these four current roots. Other location
        // companions are signed pre-turn inputs, not mutable current planning sources.
        var paths = MortalItemCanonicalProjectionPlanner.ProjectionRootPaths.Concat(new[]
        {
            MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationIdentityState.StatePath,
            MortalBootstrapLocationScaffold.StatePath,
            FactionCoreChangesContract.FactionCorePath,
            MortalStorageAccessProjection.StatePath
        });
        var observations = await CaptureResourceBeforeImagesAsync(paths, writeLease: lease);
        var localIssues = new List<ValidationIssue>();
        var locationPlan = await ValidateRawMortalLocationAcceptedTurnPlanAsync(
            localIssues, lease, currentObservations: observations);
        var acceptedMap = locationPlan?.FinalWorldMap ??
            ParseProjectionRoot(DecodeObservedCanonicalInput(observations[MortalLocationMaterializationContract.WorldMapPath]),
                MortalLocationMaterializationContract.WorldMapPath, localIssues) as JsonObject ?? new JsonObject();
        var current = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var path in snapshot is null ? Array.Empty<string>() : MortalItemCanonicalProjectionPlanner.ProjectionRootPaths)
        {
            current[path] = ParseProjectionRoot(DecodeObservedCanonicalInput(observations[path]), path, localIssues);
        }
        // Match the effective roots used by original item intake, not raw movement
        // commands. Keep their raw observations above for publication-time fencing.
        if (locationPlan?.FinalCurrentLocation is { } location)
            current[StorageTransportMoveService.CurrentLocationPath] = location.DeepClone();
        if (locationPlan?.FinalStorageContents is { } storage)
            current[MortalLocationStorageContentsState.StatePath] = storage.DeepClone();
        if (localIssues.Any(issue => issue.Severity == IssueSeverity.Error) ||
            snapshot is not null && !snapshot.MatchesProjectionRoots(current, snapshot.CloneBackupProjectionRoots()))
        {
            issues.AddRange(localIssues);
            issues.Add(ResourceIssue(StorageTransportMoveService.VehiclesPath,
                "resource_owner_vehicle_cargo_projection_invalid",
                "unchanged accepted item projection dependencies", "current source drift or invalid input"));
            return null;
        }
        return new VehicleBirthLocationInputs(observations, locationPlan, acceptedMap);
    }
}
