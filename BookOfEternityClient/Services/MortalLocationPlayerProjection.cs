using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.UI;

namespace BookOfEternityClient.Services;

internal sealed record MortalLocationPlayerLocation(
    string Identity,
    string DiscoveryTier,
    string Label,
    string? RumorSummary,
    bool IsCurrent,
    string? DetailSelector,
    IReadOnlyList<MortalLocationPlayerFactionControl> FactionControls,
    JsonObject Data);

internal sealed record MortalLocationPlayerFactionControl(
    string Identity,
    string Label,
    string ControlType,
    int ControlLevel);

internal sealed record MortalLocationPlayerLink(
    string Identity,
    string DiscoveryTier,
    string SourceIdentity,
    string TargetIdentity,
    string LinkSelector,
    string? TravelTargetSelector,
    JsonObject Data);

internal sealed class MortalLocationPlayerCatalog
{
    internal MortalLocationPlayerCatalog(
        IReadOnlyList<MortalLocationPlayerLocation> locations,
        IReadOnlyList<MortalLocationPlayerLink> links,
        string? currentLocationId)
    {
        Locations = locations;
        Links = links;
        CurrentLocationId = currentLocationId;
    }

    internal IReadOnlyList<MortalLocationPlayerLocation> Locations { get; }

    internal IReadOnlyList<MortalLocationPlayerLink> Links { get; }

    internal string? CurrentLocationId { get; }

    internal bool TryGetLocation(string selector, out MortalLocationPlayerLocation? location)
    {
        location = Locations.FirstOrDefault(candidate =>
            string.Equals(candidate.Identity, selector, StringComparison.Ordinal));
        return location != null;
    }
}

internal static class MortalLocationPlayerProjection
{
    private static readonly HashSet<string> WorldMapFields = new(StringComparer.Ordinal)
    {
        "schemaVersion",
        "realm",
        "locations",
        "links"
    };

    private static readonly HashSet<string> LocationProtocolFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "realm",
        "coordinatesAtCreation",
        "knownExits",
        "adjacencyMap",
        "locationEntries",
        "linkEntries",
        "currentLocationData",
        "worldMapUpdates",
        "newLocations",
        "locationUpdates",
        "locationDiscoveries",
        "newLinks",
        "linkUpdates",
        "linkRemovals",
        "movementSelection",
        "mortalBootstrapScaffold",
        "requiredResubmissionPaths",
        "resubmissionObligations",
        "fullTurnResubmissionRequired"
    };

    internal static MortalLocationPlayerCatalog Create(
        string? worldMapJson,
        string? currentLocationJson,
        string? identityIndexJson) =>
        Create(
            TryParse(worldMapJson),
            TryParse(currentLocationJson),
            TryParse(identityIndexJson));

    internal static MortalLocationPlayerCatalog Create(
        JsonNode? worldMap,
        JsonNode? currentLocation,
        JsonNode? identityIndex)
    {
        var empty = new MortalLocationPlayerCatalog([], [], null);
        if (worldMap is not JsonObject map ||
            !WorldMapFields.SetEquals(map.Select(static property => property.Key)) ||
            !TryReadInt(map, "schemaVersion", out var schemaVersion) || schemaVersion != 1 ||
            !string.Equals(ReadExactString(map, "realm"), "mortal_world", StringComparison.Ordinal) ||
            map["locations"] is not JsonArray locationArray ||
            map["links"] is not JsonArray linkArray)
        {
            return empty;
        }

        var identityState = MortalLocationIdentityState.Parse(identityIndex);
        if (identityState.Issues.Count != 0)
            return empty;

        var locationCandidates = locationArray
            .OfType<JsonObject>()
            .Where(location => IsAcceptedLocation(location, identityState))
            .ToArray();
        var ambiguousLocationIds = locationCandidates
            .Select(static location => ReadExactString(location, "locationId")!)
            .GroupBy(MortalLocationIdentityState.BuildConfusableKey, StringComparer.Ordinal)
            .Where(static group => group.Count() != 1)
            .Select(static group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        var ambiguousCoordinates = locationCandidates
            .Select(static location => ReadCoordinateKey(location))
            .Where(static key => key != null)
            .GroupBy(static key => key!, StringComparer.Ordinal)
            .Where(static group => group.Count() != 1)
            .Select(static group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        var acceptedLocations = locationCandidates
            .Where(location =>
            {
                var id = ReadExactString(location, "locationId")!;
                var coordinate = ReadCoordinateKey(location);
                return !ambiguousLocationIds.Contains(MortalLocationIdentityState.BuildConfusableKey(id)) &&
                       coordinate != null && !ambiguousCoordinates.Contains(coordinate);
            })
            .ToList();
        RemoveInvalidParentGraphs(acceptedLocations);
        var locationsById = acceptedLocations.ToDictionary(
            static location => ReadExactString(location, "locationId")!,
            StringComparer.Ordinal);

        var current = currentLocation as JsonObject;
        var currentId = ResolveAcceptedCurrentLocationId(current, locationsById, identityState);
        var playerLocations = acceptedLocations
            .Select(location => BuildLocation(
                location,
                string.Equals(ReadExactString(location, "locationId"), currentId, StringComparison.Ordinal)
                    ? current
                    : null,
                currentId))
            .Where(static location => location != null)
            .Select(static location => location!)
            .ToArray();
        var playerLocationsById = playerLocations.ToDictionary(
            static location => location.Identity,
            StringComparer.Ordinal);
        if (currentId == null || !playerLocationsById.ContainsKey(currentId))
            currentId = null;

        var linkCandidates = linkArray
            .OfType<JsonObject>()
            .Where(link => IsAcceptedLink(link, identityState))
            .ToArray();
        var ambiguousLinkIds = linkCandidates
            .Select(static link => ReadExactString(link, "linkId")!)
            .GroupBy(MortalLocationIdentityState.BuildConfusableKey, StringComparer.Ordinal)
            .Where(static group => group.Count() != 1)
            .Select(static group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        var playerLinks = linkCandidates
            .Where(link => IsAcceptedVisibleLink(
                link,
                ambiguousLinkIds,
                locationsById,
                playerLocationsById))
            .Select(link => BuildLink(link, currentId))
            .ToArray();

        return new MortalLocationPlayerCatalog(playerLocations, playerLinks, currentId);
    }

    private static MortalLocationPlayerLocation? BuildLocation(
        JsonObject canonical,
        JsonObject? current,
        string? currentId)
    {
        var identity = ReadExactString(canonical, "locationId")!;
        var discovery = canonical["discovery"] as JsonObject;
        var tier = ReadExactString(discovery, "tier");
        var label = ReadExactString(canonical, "displayName") ??
                    ReadExactString(canonical, "name") ??
                    "Неизвестное место";
        if (tier == "hidden")
            return null;

        if (tier == "rumored")
        {
            var rumorSummary = ReadExactString(discovery, "rumorSummary")!;
            return new MortalLocationPlayerLocation(
                identity,
                tier,
                label,
                rumorSummary,
                IsCurrent: false,
                DetailSelector: null,
                FactionControls: [],
                new JsonObject
                {
                    ["displayName"] = label,
                    ["discovery"] = new JsonObject
                    {
                        ["tier"] = tier,
                        ["rumorSummary"] = rumorSummary
                    }
                });
        }

        if (tier is not ("discovered" or "visited"))
            return null;

        var source = current ?? canonical;
        var data = SanitizeLocationObject(source);
        var isCurrent = string.Equals(identity, currentId, StringComparison.Ordinal);
        return new MortalLocationPlayerLocation(
            identity,
            tier,
            label,
            RumorSummary: null,
            isCurrent,
            DetailSelector: identity,
            ReadFactionControls(canonical),
            data);
    }

    private static IReadOnlyList<MortalLocationPlayerFactionControl> ReadFactionControls(
        JsonObject canonical)
    {
        if (canonical["factionControl"] is not JsonArray controls)
            return [];

        return controls.OfType<JsonObject>()
            .Select(control =>
            {
                var identity = ReadExactString(control, "factionId") ?? string.Empty;
                var label = ReadExactString(control, "factionName") ??
                            ReadExactString(control, "name") ??
                            string.Empty;
                var controlType = ReadExactString(control, "controlType") ??
                                  ReadExactString(control, "type") ??
                                  string.Empty;
                var controlLevel = TryReadInt(control, "controlLevel", out var level)
                    ? level
                    : TryReadInt(control, "influence", out level)
                        ? level
                        : TryReadInt(control, "value", out level)
                            ? level
                            : 0;
                return new MortalLocationPlayerFactionControl(
                    identity,
                    label,
                    controlType,
                    controlLevel);
            })
            .Where(static control =>
                !string.IsNullOrEmpty(control.Identity) || !string.IsNullOrEmpty(control.Label))
            .ToArray();
    }

    private static MortalLocationPlayerLink BuildLink(JsonObject canonical, string? currentId)
    {
        var identity = ReadExactString(canonical, "linkId")!;
        var source = ReadExactString(canonical, "sourceLocationId")!;
        var target = ReadExactString(canonical, "targetLocationId")!;
        var tier = ReadExactString(canonical["discovery"] as JsonObject, "tier")!;
        var access = canonical["access"] as JsonObject;
        var accessState = ReadExactString(access, "state");
        var canTravel = string.Equals(source, currentId, StringComparison.Ordinal) &&
                        string.Equals(accessState, "open", StringComparison.Ordinal) &&
                        access?["requirements"] is JsonArray { Count: 0 };
        return new MortalLocationPlayerLink(
            identity,
            tier,
            source,
            target,
            identity,
            canTravel ? target : null,
            SanitizeLocationObject(canonical));
    }

    private static bool IsAcceptedLocation(
        JsonObject location,
        MortalLocationIdentityState identityState)
    {
        if (location.ContainsKey("knownExits") || location.ContainsKey("adjacencyMap") ||
            ContainsMapStorageContents(location) ||
            !identityState.IsAcceptedCanonicalLocation(location))
        {
            return false;
        }

        using var document = JsonDocument.Parse(location.ToJsonString());
        return MortalLocationMaterializationContract.ValidateCanonicalLocation(
            document.RootElement,
            MortalLocationMaterializationContract.WorldMapPath + ".locations[]").Count == 0;
    }

    private static bool IsAcceptedLink(
        JsonObject link,
        MortalLocationIdentityState identityState)
    {
        if (!identityState.IsAcceptedCanonicalLink(link))
            return false;
        using var document = JsonDocument.Parse(link.ToJsonString());
        return MortalLocationMaterializationContract.ValidateCanonicalLink(
            document.RootElement,
            MortalLocationMaterializationContract.WorldMapPath + ".links[]").Count == 0;
    }

    private static bool IsAcceptedVisibleLink(
        JsonObject link,
        IReadOnlySet<string> ambiguousLinkIds,
        IReadOnlyDictionary<string, JsonObject> acceptedLocations,
        IReadOnlyDictionary<string, MortalLocationPlayerLocation> playerLocations)
    {
        var linkId = ReadExactString(link, "linkId")!;
        var source = ReadExactString(link, "sourceLocationId");
        var target = ReadExactString(link, "targetLocationId");
        var tier = ReadExactString(link["discovery"] as JsonObject, "tier");
        if (ambiguousLinkIds.Contains(MortalLocationIdentityState.BuildConfusableKey(linkId)) ||
            source == null || target == null ||
            string.Equals(source, target, StringComparison.Ordinal) ||
            !acceptedLocations.ContainsKey(source) || !acceptedLocations.ContainsKey(target) ||
            !playerLocations.TryGetValue(source, out var sourceProjection) ||
            !playerLocations.TryGetValue(target, out var targetProjection))
        {
            return false;
        }

        return tier is "discovered" or "visited" &&
               sourceProjection.DiscoveryTier is "discovered" or "visited" &&
               targetProjection.DiscoveryTier is "discovered" or "visited";
    }

    private static string? ResolveAcceptedCurrentLocationId(
        JsonObject? current,
        IReadOnlyDictionary<string, JsonObject> locationsById,
        MortalLocationIdentityState identityState)
    {
        var currentId = ReadExactString(current, "locationId");
        if (current == null || currentId == null ||
            !locationsById.TryGetValue(currentId, out var canonical) ||
            !identityState.IsAcceptedCanonicalLocation(current) ||
            !string.Equals(
                ReadExactString(current["discovery"] as JsonObject, "tier"),
                "visited",
                StringComparison.Ordinal))
        {
            return null;
        }

        using var document = JsonDocument.Parse(current.ToJsonString());
        if (MortalLocationMaterializationContract.ValidateCanonicalCurrentLocation(
                document.RootElement,
                MortalLocationMaterializationContract.CurrentLocationPath).Count != 0)
        {
            return null;
        }

        foreach (var property in canonical)
        {
            if (!current.TryGetPropertyValue(property.Key, out var currentValue) ||
                !MortalLocationMaterializationContract.SharedCurrentProjectionValueEquals(
                    property.Key,
                    property.Value,
                    currentValue))
            {
                return null;
            }
        }

        return currentId;
    }

    private static JsonObject SanitizeLocationObject(JsonObject source)
    {
        var result = new JsonObject();
        foreach (var property in source)
        {
            if (IsInternalLocationField(property.Key))
                continue;
            if (string.Equals(property.Key, "contents", StringComparison.Ordinal) &&
                property.Value is JsonArray contents)
            {
                result[property.Key] = ProjectAcceptedItems(contents);
                continue;
            }
            var projected = SanitizeLocationNode(property.Value);
            if (property.Value != null && projected == null)
                continue;
            result[property.Key] = projected;
        }
        return result;
    }

    private static JsonArray ProjectAcceptedItems(JsonArray contents)
    {
        var candidates = contents.OfType<JsonObject>()
            .Select(item => MortalItemMaterializationContract.TryReadAcceptedIdentity(item, out var identity)
                ? (Item: item, Identity: identity)
                : (Item: null, Identity: string.Empty))
            .Where(static candidate => candidate.Item != null)
            .Select(static candidate => (Item: candidate.Item!, candidate.Identity))
            .ToArray();
        var ambiguous = candidates
            .GroupBy(
                static candidate => MortalItemIdentityRules.BuildConfusableKey(candidate.Identity),
                StringComparer.Ordinal)
            .Where(static group => group.Count() != 1)
            .Select(static group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        var projected = new JsonArray();
        foreach (var candidate in candidates)
        {
            if (ambiguous.Contains(MortalItemIdentityRules.BuildConfusableKey(candidate.Identity)))
                continue;
            var item = MortalItemPlayerProjection.CloneItemSemanticValue(candidate.Item);
            var safeItem = SanitizeLocationNode(item);
            if (safeItem != null)
                projected.Add(safeItem);
        }
        return projected;
    }

    private static bool ContainsMapStorageContents(JsonObject location) =>
        location["locationStorages"] is JsonArray storages &&
        storages.OfType<JsonObject>().Any(static storage => storage.ContainsKey("contents"));

    private static JsonNode? SanitizeLocationNode(JsonNode? source)
    {
        switch (source)
        {
            case null:
                return null;
            case JsonObject obj:
                if (IsInternalLocationDtoShape(obj) ||
                    MortalItemPlayerProjection.CloneMortalMaterializationSemanticValue(obj) == null)
                {
                    return null;
                }
                return SanitizeLocationObject(obj);
            case JsonArray array:
                var projected = new JsonArray();
                foreach (var item in array)
                {
                    var safeItem = SanitizeLocationNode(item);
                    if (item != null && safeItem == null)
                        continue;
                    projected.Add(safeItem);
                }
                return projected;
            default:
                return source.DeepClone();
        }
    }

    private static bool IsInternalLocationField(string fieldName)
    {
        if (MortalItemPlayerProjection.IsInternalField(fieldName) ||
            LocationProtocolFields.Contains(fieldName))
        {
            return true;
        }

        return fieldName.EndsWith("Id", StringComparison.OrdinalIgnoreCase) ||
               fieldName.EndsWith("Ids", StringComparison.OrdinalIgnoreCase) ||
               fieldName.EndsWith("Path", StringComparison.OrdinalIgnoreCase) ||
               fieldName.EndsWith("Paths", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInternalLocationDtoShape(JsonObject source)
    {
        var fields = source.Select(static property => property.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (fields.IsSupersetOf(new[] { "schemaVersion", "realm", "locations", "links" }) ||
            fields.IsSupersetOf(new[] { "schemaVersion", "realm", "locationEntries", "linkEntries" }))
        {
            return true;
        }

        if (fields.Contains("locationId") &&
            fields.Contains("materialization") &&
            fields.Contains("materializationReceipt"))
        {
            return true;
        }
        if (fields.Contains("linkId") &&
            fields.Contains("sourceLocationId") &&
            fields.Contains("targetLocationId") &&
            fields.Contains("materializationReceipt"))
        {
            return true;
        }

        return fields.IsSupersetOf(new[]
               {
                   "locationId", "initialId", "materializationId", "receiptId", "state", "transitions"
               }) ||
               fields.IsSupersetOf(new[]
               {
                   "linkId", "initialId", "materializationId", "receiptId", "state", "transitions"
               });
    }

    private static void RemoveInvalidParentGraphs(List<JsonObject> locations)
    {
        var changed = true;
        while (changed)
        {
            var ids = locations
                .Select(static location => ReadExactString(location, "locationId")!)
                .ToHashSet(StringComparer.Ordinal);
            changed = locations.RemoveAll(location =>
            {
                var parent = ReadExactString(location, "parentLocationId");
                return parent != null && !ids.Contains(parent);
            }) > 0;
        }

        var byId = locations.ToDictionary(
            static location => ReadExactString(location, "locationId")!,
            StringComparer.Ordinal);
        var cyclic = new HashSet<string>(StringComparer.Ordinal);
        foreach (var start in byId.Keys)
        {
            var path = new HashSet<string>(StringComparer.Ordinal);
            var cursor = start;
            while (byId.TryGetValue(cursor, out var location) &&
                   ReadExactString(location, "parentLocationId") is { } parent)
            {
                if (!path.Add(cursor) || string.Equals(parent, start, StringComparison.Ordinal))
                {
                    foreach (var member in path)
                        cyclic.Add(member);
                    break;
                }
                cursor = parent;
            }
        }
        locations.RemoveAll(location => cyclic.Contains(ReadExactString(location, "locationId")!));
    }

    private static string? ReadCoordinateKey(JsonObject location)
    {
        if (location["coordinates"] is not JsonObject coordinates ||
            !TryReadInt(coordinates, "x", out var x) ||
            !TryReadInt(coordinates, "y", out var y) ||
            !TryReadInt(coordinates, "z", out var z))
        {
            return null;
        }
        return $"{x}\u001f{y}\u001f{z}";
    }

    private static string? ReadExactString(JsonObject? root, string fieldName)
    {
        if (root?[fieldName] is not JsonValue value ||
            !value.TryGetValue<string>(out var text) ||
            string.IsNullOrEmpty(text) ||
            !string.Equals(text, text.Trim(), StringComparison.Ordinal))
        {
            return null;
        }
        return text;
    }

    private static bool TryReadInt(JsonObject root, string fieldName, out int value)
    {
        value = 0;
        return root[fieldName] is JsonValue scalar && scalar.TryGetValue(out value);
    }

    private static JsonNode? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
