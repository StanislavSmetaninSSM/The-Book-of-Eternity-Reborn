using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record MortalLocationRepairRetryObligation(
    string Actor, string Route, string? MaterializationId,
    string? RawCoordinate = null, string? StorageId = null,
    MortalStorageBirthItemRetryBinding? StorageItems = null);

/// <summary>Immutable raw identity DATA for the existing storage-addition retry owner.
/// The expected source is derived from the command, not copied from a possibly wrong item field.</summary>
internal sealed class MortalStorageBirthItemRetryBinding
{
    private sealed record Item(int Ordinal, string CreationRef, string? MaterializationId);
    private readonly Item[] _items;
    private MortalStorageBirthItemRetryBinding(Item[] items, string locationId, string storageId, int turn)
    {
        _items = items;
        LocationId = locationId; StorageId = storageId; SourceTurn = turn;
        // Stable value key permits exact agreement across independently detached issue contexts.
        ValueKey = System.Text.Json.JsonSerializer.Serialize(new { locationId, storageId, turn, items });
    }
    internal string LocationId { get; }
    internal string StorageId { get; }
    internal int SourceTurn { get; }
    internal string ValueKey { get; }
    internal static MortalStorageBirthItemRetryBinding? Capture(JsonObject storage, string? locationId, string? storageId, int turn)
    {
        if (!MortalItemIdentityRules.IsExactIdentity(locationId) || !MortalItemIdentityRules.IsExactIdentity(storageId) ||
            storage["contents"] is not JsonArray contents) return null;
        var items = new List<Item>();
        var references = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < contents.Count; index++)
        {
            if (contents[index] is not JsonObject item || Exact(item, "creationRef") is not { } reference ||
                !references.Add(reference) || item["itemId"] is not null || item["existedId"] is not null)
                return null;
            items.Add(new(index, reference, item["materialization"] is JsonObject envelope ? Exact(envelope, "materializationId") : null));
        }
        return new(items.ToArray(), locationId!, storageId!, turn);
    }
    internal bool ContainsItemPath(string path, string operationRef)
    {
        var prefix = MortalLocationMaterializationContract.WorldMapPath + "." + operationRef + ".storage.contents[";
        return _items.Any(item => path == prefix + item.Ordinal + "]" || path.StartsWith(prefix + item.Ordinal + "].", StringComparison.Ordinal));
    }
    internal bool Matches(JsonObject storage)
    {
        if (storage["contents"] is not JsonArray contents || contents.Count != _items.Length) return false;
        foreach (var binding in _items)
        {
            if (contents[binding.Ordinal] is not JsonObject item || Exact(item, "creationRef") != binding.CreationRef ||
                item["materialization"] is not JsonObject envelope || Exact(envelope, "creationRef") != binding.CreationRef ||
                binding.MaterializationId is not null && Exact(envelope, "materializationId") != binding.MaterializationId ||
                Exact(envelope, "route") != "storage_placement" ||
                envelope["sourceTurn"] is not JsonValue turnValue || !turnValue.TryGetValue<int>(out var turn) || turn != SourceTurn ||
                envelope["sourceAuthority"] is not JsonObject source || Exact(source, "kind") != "location_storage" ||
                Exact(source, "authorityId") != LocationId + ":" + StorageId)
                return false;
        }
        return true;
    }
    private static string? Exact(JsonObject root, string field) => root[field] is JsonValue value &&
        value.TryGetValue<string>(out var text) && MortalItemIdentityRules.IsExactIdentity(text) ? text : null;
}

// Pure existing retry decision over the two already-read raw roots. It grants no
// publication authority; subsequent raw/canonical validation remains mandatory.
internal static class MortalLocationRepairRetryPolicy
{
    internal static IReadOnlyList<MortalLocationRepairRetryObligation> Capture(
        IReadOnlyList<MortalLocationRepairPacket> packets) => packets
        .Select(packet => new MortalLocationRepairRetryObligation(
            packet.CanonicalActorNames.Single(), packet.Route, packet.ExpectedMaterializationId,
            packet.Route == "storage_addition" ? packet.RawCoordinate : null,
            packet.Route == "storage_addition" ? packet.ExpectedStorageId : null,
            packet.Route == "storage_addition" ? packet.StorageItemRetryBinding : null))
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
                    storageCounts.GetValueOrDefault((identity, obligation.StorageId!)) != 1 ||
                    obligation.StorageItems is { } items &&
                        (items.LocationId != identity || items.StorageId != obligation.StorageId || !items.Matches(child)))
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
