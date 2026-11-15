using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private sealed record VehicleBirthActorInputs(
        IReadOnlyDictionary<string, CanonicalBeforeImage> Observations,
        JsonObject? Factions);

    private static bool HasVehicleBirthActorReferences(JsonObject vehicles) =>
        vehicles["UpdateVehicles"] is JsonArray commands && commands.OfType<JsonObject>().Any(
            vehicle => vehicle["vehicleRef"] is JsonValue &&
                (vehicle["owner"]?["ownerType"]?.GetValue<string>() == "Faction" ||
                 vehicle["controller"]?["actorType"]?.GetValue<string>() == "npc"));

    private async Task<VehicleBirthActorInputs?> CaptureVehicleBirthActorInputsAsync(
        JsonObject vehicles,
        JsonObject npcCore,
        IReadOnlyDictionary<string, CanonicalBeforeImage>? cargoObservations,
        FileSystemManager.CanonicalWriteLease lease,
        List<ValidationIssue> issues)
    {
        // Reuse the same checked cargo images, not a later read of these roots.
        // Empty cargo needs only these three observations, retained for common publication.
        var observations = cargoObservations ?? await CaptureResourceBeforeImagesAsync(new[]
        {
            StorageTransportMoveService.VehiclesPath,
            NpcCoreChangesContract.NpcCorePath,
            FactionCoreChangesContract.FactionCorePath
        }, writeLease: lease);
        var observedVehicles = ReadObservedObject(StorageTransportMoveService.VehiclesPath, "vehicles");
        var observedNpc = ReadObservedObject(NpcCoreChangesContract.NpcCorePath, "NPCsInScene");
        var observedFactions = ReadObservedObject(FactionCoreChangesContract.FactionCorePath, null);
        if (!JsonNode.DeepEquals(vehicles, observedVehicles) || !JsonNode.DeepEquals(npcCore, observedNpc))
            issues.Add(ResourceIssue(StorageTransportMoveService.VehiclesPath,
                "resource_owner_vehicle_birth_reference_source_changed",
                "the same vehicle and NPC inputs used by current owner composition",
                "source differs from its captured before-image"));
        return issues.Any(issue => issue.Severity == IssueSeverity.Error)
            ? null : new VehicleBirthActorInputs(observations, observedFactions);

        JsonObject? ReadObservedObject(string path, string? missingCollection)
        {
            var json = DecodeObservedCanonicalInput(observations[path]);
            if (json is null)
                return missingCollection is null ? null : new JsonObject { [missingCollection] = new JsonArray() };
            if (TryParseStrictResourceObject(json, out var root))
                return root;
            issues.Add(ResourceIssue(path, "resource_owner_vehicle_birth_reference_root_invalid",
                "one strict object root for vehicle birth references", "malformed or non-object input"));
            return null;
        }
    }
}
