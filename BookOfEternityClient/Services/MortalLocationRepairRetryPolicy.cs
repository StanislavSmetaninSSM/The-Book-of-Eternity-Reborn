using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record MortalLocationRepairRetryObligation(
    string Actor, string Route, string? MaterializationId,
    string? RawCoordinate = null, string? StorageId = null);

// Pure existing retry decision over the two already-read raw roots. It grants no
// publication authority; subsequent raw/canonical validation remains mandatory.
internal static class MortalLocationRepairRetryPolicy
{
    internal static IReadOnlyList<MortalLocationRepairRetryObligation> Capture(
        IReadOnlyList<MortalLocationRepairPacket> packets) => packets
        .Select(packet => new MortalLocationRepairRetryObligation(
            packet.CanonicalActorNames.Single(), packet.Route, packet.ExpectedMaterializationId,
            packet.Route == "storage_addition" ? packet.RawCoordinate : null,
            packet.Route == "storage_addition" ? packet.ExpectedStorageId : null))
        .Distinct().ToArray();

    internal static bool HasExactResubmission(
        IReadOnlyList<MortalLocationRepairRetryObligation> obligations,
        JsonObject? currentRoot, JsonObject? mapRoot)
    {
        // One operation-local index over already-read raw DATA, not retained rights.
        var storageRows = (mapRoot?["worldMapUpdates"] as JsonObject)?["storagesToAdd"] as JsonArray;
        var storageCounts = new Dictionary<(string Parent, string Child), int>();
        if (obligations.Any(o => o.Route == "storage_addition") && storageRows is not null)
            foreach (var row in storageRows.OfType<JsonObject>())
                if (ReadExactMortalLocationRepairString(row, "targetLocationId") is { } parent &&
                    row["storage"] is JsonObject child &&
                    ReadExactMortalLocationRepairString(child, "storageId") is { } id)
                    storageCounts[(parent, id)] = storageCounts.GetValueOrDefault((parent, id)) + 1;
        foreach (var obligation in obligations)
        {
            if (!TryParseMortalLocationRepairActor(
                    obligation.Actor,
                    out var entityKind,
                    out var isCreation,
                    out var identity))
            {
                return false;
            }

            if (obligation.Route == "storage_addition")
            {
                const string prefix = "worldMapUpdates.storagesToAdd[";
                var coordinate = obligation.RawCoordinate;
                if (isCreation || entityKind != "mortal_location" || storageRows is null ||
                    !MortalItemIdentityRules.IsExactIdentity(obligation.StorageId) || coordinate is null ||
                    !coordinate.StartsWith(prefix, StringComparison.Ordinal) || !coordinate.EndsWith(']') ||
                    !int.TryParse(coordinate.AsSpan(prefix.Length, coordinate.Length - prefix.Length - 1),
                        System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var index) ||
                    coordinate != prefix + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]" ||
                    index < 0 || index >= storageRows.Count || storageRows[index] is not JsonObject row ||
                    ReadExactMortalLocationRepairString(row, "targetLocationId") != identity ||
                    row["storage"] is not JsonObject child ||
                    ReadExactMortalLocationRepairString(child, "storageId") != obligation.StorageId ||
                    storageCounts.GetValueOrDefault((identity, obligation.StorageId!)) != 1)
                    return false;
                continue;
            }

            var candidates = EnumerateMortalLocationRepairRouteCandidates(
                obligation.Route,
                currentRoot,
                mapRoot);
            var identityField = isCreation
                ? "initialId"
                : string.Equals(entityKind, "mortal_location", StringComparison.Ordinal)
                    ? "locationId"
                    : "linkId";
            var exactMatches = candidates.Count(candidate =>
            {
                if (!string.Equals(
                        ReadExactMortalLocationRepairString(candidate, identityField),
                        identity,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                if (!isCreation)
                    return true;

                return candidate["materialization"] is JsonObject materialization &&
                       string.Equals(
                           ReadExactMortalLocationRepairString(materialization, "materializationId"),
                           obligation.MaterializationId,
                           StringComparison.Ordinal);
            });
            if (exactMatches != 1)
                return false;
        }

        return true;
    }

    private static IEnumerable<JsonObject> EnumerateMortalLocationRepairRouteCandidates(
        string route,
        JsonObject? currentRoot,
        JsonObject? mapRoot)
    {
        if (route is "current_scene_creation" or "current_selection")
        {
            if (currentRoot?["currentLocationData"] is JsonObject current)
                yield return current;
            yield break;
        }

        var collection = route switch
        {
            "world_map_creation" => "newLocations",
            "world_map_link_creation" => "newLinks",
            "narrow_location_update" => "locationUpdates",
            "location_discovery_transition" => "locationDiscoveryTransitions",
            "link_update" => "linkUpdates",
            "link_removal" => "linkRemovals",
            _ => null
        };
        if (collection == null ||
            mapRoot?["worldMapUpdates"] is not JsonObject updates ||
            updates[collection] is not JsonArray values)
        {
            yield break;
        }

        foreach (var candidate in values.OfType<JsonObject>())
            yield return candidate;
    }

    private static bool TryParseMortalLocationRepairActor(
        string actor,
        out string entityKind,
        out bool isCreation,
        out string identity)
    {
        foreach (var descriptor in new[]
                 {
                     (Prefix: "mortal_location:new:", EntityKind: "mortal_location", IsCreation: true),
                     (Prefix: "mortal_location:existing:", EntityKind: "mortal_location", IsCreation: false),
                     (Prefix: "mortal_location_link:new:", EntityKind: "mortal_location_link", IsCreation: true),
                     (Prefix: "mortal_location_link:existing:", EntityKind: "mortal_location_link", IsCreation: false)
                 })
        {
            if (!actor.StartsWith(descriptor.Prefix, StringComparison.Ordinal))
                continue;

            identity = actor[descriptor.Prefix.Length..];
            entityKind = descriptor.EntityKind;
            isCreation = descriptor.IsCreation;
            return MortalItemIdentityRules.IsExactIdentity(identity);
        }

        entityKind = string.Empty;
        isCreation = false;
        identity = string.Empty;
        return false;
    }

    private static string? ReadExactMortalLocationRepairString(
        JsonObject source,
        string property)
    {
        return source[property] is JsonValue value &&
               value.TryGetValue<string>(out var text) &&
               MortalItemIdentityRules.IsExactIdentity(text)
            ? text
            : null;
    }

}
