using System.Text.Json;
using System.Text.Json.Nodes;
namespace BookOfEternityClient.Services;

internal sealed record MortalStorageAccessProjectionResult(JsonObject? Root, IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Root != null && Issues.Count == 0;
}

/// <summary>Client-owned effective access DATA derived solely from accepted location metadata.
/// Pending commands are preserved, never executed or treated as permissions.</summary>
internal static class MortalStorageAccessProjection
{
    internal const string StatePath = "game_state/misc/storage_access.json";
    internal static readonly string[] PendingFields = { "grantStorageAccess", "revokeStorageAccess", "shareStorageAccess" };
    internal static JsonObject CreateEmptyRoot() => new() { ["schemaVersion"] = 1, ["entries"] = new JsonArray() };

    internal static MortalStorageAccessProjectionResult Build(JsonObject map, JsonObject? current)
    {
        var issues = new List<ValidationIssue>(); var result = CreateEmptyRoot();
        foreach (var field in PendingFields)
            if (current?.ContainsKey(field) == true) result[field] = current[field]?.DeepClone();
        if (Exact(map["realm"]) != "mortal_world" || map["locations"] is not JsonArray locations)
            return new(null, new[] { Issue("entries", "invalid", "Accepted Mortal map locations are required.") });
        var locationKeys = new HashSet<string>(StringComparer.Ordinal);
        var entries = new List<(string Location, string Storage, JsonObject Entry)>();
        foreach (var locationNode in locations)
        {
            if (locationNode is not JsonObject location || Exact(location["realm"]) != "mortal_world")
            { issues.Add(Issue("entries", "invalid", "Every accepted location must be a Mortal location object.")); continue; }
            var locationId = Exact(location["locationId"]);
            if (locationId is null || !locationKeys.Add(MortalLocationIdentityState.BuildConfusableKey(locationId)))
            { issues.Add(Issue("entries", "invalid", "Location identities must be exact and unique.")); continue; }
            if (location["locationStorages"] is not JsonArray storages)
            { issues.Add(Issue("entries", "invalid", "Canonical locationStorages must be an explicit array.")); continue; }
            var storageKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var storage in storages)
            {
                if (storage is not JsonObject metadata || Exact(metadata["storageId"]) is not { } storageId ||
                    !storageKeys.Add(MortalLocationIdentityState.BuildConfusableKey(storageId)))
                { issues.Add(Issue("entries", "invalid", "Storage coordinates must be exact and unique.")); continue; }
                var shape = MortalLocationStorageMetadataContract.Validate(JsonSerializer.SerializeToElement(metadata), StatePath + ".entries");
                if (shape.Count != 0) { issues.AddRange(shape); continue; }
                entries.Add((locationId, storageId, new JsonObject
                {
                    ["locationId"] = locationId, ["storageId"] = storageId,
                    ["owner"] = metadata["owner"]?.DeepClone(), ["authorizedUsers"] = metadata["authorizedUsers"]?.DeepClone(),
                    ["hasFullAccess"] = metadata["hasFullAccess"]!.DeepClone()
                }));
            }
        }
        foreach (var entry in entries.OrderBy(e => e.Location, StringComparer.Ordinal).ThenBy(e => e.Storage, StringComparer.Ordinal))
            result["entries"]!.AsArray().Add(entry.Entry);
        return new(issues.Count == 0 ? result : null, issues);
    }

    /// <summary>Uses the existing accepted location and visibility projection for display only.
    /// Full effective state remains canonical; hidden, rumored and rejected locations expose no storage entries.</summary>
    internal static JsonObject? CreatePlayerView(JsonObject? map, JsonNode? currentLocation,
        JsonNode? identityIndex, JsonObject? access)
    {
        if (map is null || ValidateCanonical(map, access).Count != 0) return null;
        var catalog = MortalLocationPlayerProjection.Create(map, currentLocation, identityIndex);
        var visible = catalog.Locations.Where(location => location.DetailSelector is not null)
            .Select(location => location.Identity).ToHashSet(StringComparer.Ordinal);
        // Labels are display-only and come from the same accepted, visible map.
        // Technical coordinates remain selectors, not the only player-facing identity.
        var labels = new Dictionary<(string Location, string Storage), (string Location, string Storage)>();
        foreach (var location in map["locations"]!.AsArray().OfType<JsonObject>())
        {
            var locationId = location["locationId"]!.GetValue<string>();
            if (!visible.Contains(locationId)) continue;
            foreach (var storage in location["locationStorages"]!.AsArray().OfType<JsonObject>())
                labels.Add((locationId, storage["storageId"]!.GetValue<string>()),
                    (location["displayName"]!.GetValue<string>(), storage["name"]!.GetValue<string>()));
        }
        var result = access!.DeepClone().AsObject();
        var entries = new JsonArray();
        foreach (var entry in access["entries"]!.AsArray().OfType<JsonObject>())
        {
            if (!labels.TryGetValue((entry["locationId"]!.GetValue<string>(), entry["storageId"]!.GetValue<string>()), out var label)) continue;
            var displayed = entry.DeepClone().AsObject();
            displayed["locationName"] = label.Location;
            displayed["storageName"] = label.Storage;
            entries.Add(displayed);
        }
        result["entries"] = entries;
        return result;
    }

    internal static IReadOnlyList<ValidationIssue> ValidateContinuity(JsonObject? original, JsonObject? raw)
    {
        var issues = new List<ValidationIssue>();
        foreach (var field in new[] { "schemaVersion", "entries" })
            if ((original?.ContainsKey(field) == true) != (raw?.ContainsKey(field) == true) ||
                !JsonNode.DeepEquals(original?[field], raw?[field]))
                issues.Add(Issue(field, "client_owned_mutation", "Preserve the exact signed-original effective access projection; only pending command fields are GM-authored."));
        if (raw != null)
            foreach (var field in raw)
                if (field.Key is not ("schemaVersion" or "entries") && !PendingFields.Contains(field.Key, StringComparer.Ordinal))
                    issues.Add(Issue(field.Key, "invalid", "Unknown access root field."));
        return issues;
    }

    internal static IReadOnlyList<ValidationIssue> ValidateCanonical(JsonObject map, JsonObject? current)
    {
        var expected = Build(map, current);
        if (!expected.IsValid) return expected.Issues;
        if (current == null || !JsonNode.DeepEquals(current["schemaVersion"], expected.Root!["schemaVersion"]) ||
            !JsonNode.DeepEquals(current["entries"], expected.Root!["entries"]))
            return new[] { Issue("entries", "projection_mismatch", "Effective storage access must exactly match accepted location metadata; do not repair client-owned entries through GM commands.") };
        return ValidateContinuity(current, current);
    }

    private static string? Exact(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var s) &&
        ResourceMaterializationContract.IsExactIdentifier(s) ? s : null;
    private static ValidationIssue Issue(string field, string suffix, string message) => new(StatePath + "." + field,
        IssueSeverity.Error, message, code: "mortal_location_storage_access_" + suffix, section: "StorageAccess",
        repairHint: "Restore the client-owned projection from accepted state. Correct only explicitly identified location storage metadata or pending command fields; never author entries.");
}
