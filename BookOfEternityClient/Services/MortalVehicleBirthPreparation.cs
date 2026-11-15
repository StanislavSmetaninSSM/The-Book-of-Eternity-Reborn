using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record MortalVehicleBirthPreparationResult(
    MortalVehicleBirthPreparation? Preparation, IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Preparation is not null && Issues.Count == 0;
}

/// <summary>Detached allocation DATA only; never acceptance or publication authority.</summary>
internal sealed class MortalVehicleBirthPreparation
{
    private readonly IReadOnlyDictionary<string, string> _ids;
    private MortalVehicleBirthPreparation(Dictionary<string, string> ids) =>
        _ids = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(ids, StringComparer.Ordinal));

    internal bool TryResolve(string vehicleRef, out string vehicleId) => _ids.TryGetValue(vehicleRef, out vehicleId!);

    internal static bool HasBirthCommands(JsonObject raw) => raw["UpdateVehicles"] is JsonArray updates &&
        updates.OfType<JsonObject>().Any(update => update["vehicleRef"] is not null);

    internal static MortalVehicleBirthPreparationResult Prepare(
        JsonObject before, JsonObject raw, VehicleIdentityFactory factory)
    {
        var issues = new List<ValidationIssue>();
        var permanent = new HashSet<string>(StringComparer.Ordinal);
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        ReadVehicles(before, "before", permanent, aliases, requireOriginal: false);
        var current = new HashSet<string>(StringComparer.Ordinal);
        ReadVehicles(raw, "current", current, new HashSet<string>(StringComparer.Ordinal), requireOriginal: true);
        if (!permanent.SetEquals(current))
            Add("vehicles", "retain the original canonical vehicle collection; use lifecycle commands");
        if (raw["UpdateVehicles"] is not JsonArray updates)
        {
            Add("UpdateVehicles", "vehicle command array");
            return Invalid();
        }
        var refs = new List<string>();
        var refSet = new HashSet<string>(StringComparer.Ordinal);
        var refAliases = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < updates.Count; i++)
        {
            var path = $"UpdateVehicles[{i}]";
            if (updates[i] is not JsonObject update)
            {
                Add(path, "vehicle command object");
                continue;
            }
            var hasRef = update["vehicleRef"] is not null;
            var hasId = update["vehicleId"] is not null;
            if (hasRef == hasId)
            {
                Add(path, "exactly one vehicleRef or existing vehicleId");
                continue;
            }
            if (!hasRef)
            {
                if (Exact(update["vehicleId"]) is not string id || !permanent.Contains(id))
                    Add(path + ".vehicleId", "exact pre-turn vehicle ID");
                continue;
            }
            if (Exact(update["vehicleRef"]) is not string reference || !refSet.Add(reference) ||
                !refAliases.Add(ResourceMaterializationContract.BuildConfusableKey(reference)))
            {
                Add(path + ".vehicleRef", "unique exact/confusable birth reference");
                continue;
            }
            issues.AddRange(MortalTransportStorageContract.ValidateVehicleBirth(
                JsonSerializer.SerializeToElement(update), StorageTransportMoveService.VehiclesPath + "." + path));
            refs.Add(reference);
        }
        if (issues.Count != 0)
            return Invalid();

        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var reference in refs)
        {
            var id = factory.CreateVehicleId(reference);
            if (!ResourceMaterializationContract.IsExactIdentifier(id) || !permanent.Add(id) ||
                !aliases.Add(ResourceMaterializationContract.BuildConfusableKey(id)))
            {
                Add("UpdateVehicles", "client-generated unique exact vehicle ID");
                return Invalid();
            }
            ids.Add(reference, id);
        }
        return new(new MortalVehicleBirthPreparation(ids), Array.Empty<ValidationIssue>());

        void ReadVehicles(JsonObject root, string label, HashSet<string> ids, HashSet<string> seenAliases, bool requireOriginal)
        {
            if (root["vehicles"] is null)
                return;
            if (root["vehicles"] is not JsonArray vehicles)
            {
                Add("vehicles", label + " canonical vehicle array");
                return;
            }
            for (var i = 0; i < vehicles.Count; i++)
            {
                if (vehicles[i] is not JsonObject vehicle || vehicle.ContainsKey("vehicleRef") ||
                    Exact(vehicle["vehicleId"]) is not string id || !ids.Add(id) ||
                    !seenAliases.Add(ResourceMaterializationContract.BuildConfusableKey(id)) ||
                    (requireOriginal && !permanent.Contains(id)))
                    Add($"vehicles[{i}]", label + " original unique exact vehicle identity");
            }
        }
        void Add(string path, string expected) => issues.Add(new ValidationIssue(
            StorageTransportMoveService.VehiclesPath + "." + path, IssueSeverity.Error,
            "Vehicle birth identity preparation rejected.", code: "resource_owner_vehicle_preparation_invalid",
            section: "Vehicles", expected: expected, actual: "invalid identity input"));
        MortalVehicleBirthPreparationResult Invalid() => new(null, issues.ToArray());
    }

    private static string? Exact(JsonNode? node) => node is JsonValue value &&
        value.TryGetValue<string>(out var text) && ResourceMaterializationContract.IsExactIdentifier(text) ? text : null;
}
