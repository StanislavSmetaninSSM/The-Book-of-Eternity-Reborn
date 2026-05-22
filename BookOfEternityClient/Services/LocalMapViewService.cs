using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public static class LocalMapViewService
{
    private const string WorldLayerId = "world";
    private const int MeaningfulFactionInfluence = 25;
    private const int ContestedControlGap = 10;
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    public static async Task<MapViewDto> BuildMortalWorldMapAsync(FileSystemManager fs)
    {
        var currentJson = await fs.ReadFileAsync("game_state/world/current_location.json");
        var worldMapJson = await fs.ReadFileAsync("game_state/world/world_map.json");
        var factionJson = await fs.ReadFileAsync("game_state/factions/faction_core.json");
        using var currentDoc = TryParse(currentJson);
        using var worldMapDoc = TryParse(worldMapJson);
        using var factionDoc = TryParse(factionJson);

        var nodes = new Dictionary<string, NodeDraft>(StringComparer.OrdinalIgnoreCase);
        var links = new Dictionary<string, MapLinkDto>(StringComparer.OrdinalIgnoreCase);
        var currentNodeId = string.Empty;

        if (currentDoc != null && currentDoc.RootElement.ValueKind == JsonValueKind.Object)
        {
            var current = UnwrapCurrentLocationRoot(currentDoc.RootElement);
            currentNodeId = ResolveLocationId(current, "current_location");
            AddLocationNode(nodes, current, currentNodeId, isCurrent: true);
            AddAdjacencyLinks(nodes, links, currentNodeId, current);
        }

        if (worldMapDoc != null && worldMapDoc.RootElement.ValueKind == JsonValueKind.Object)
        {
            var mapRoot = UnwrapWorldMapRoot(worldMapDoc.RootElement);
            AddLocationArray(nodes, mapRoot, "locations");
            AddLocationArray(nodes, mapRoot, "knownLocations");
            AddLocationArray(nodes, mapRoot, "newLocations");
            AddLocationArray(nodes, mapRoot, "locationUpdates");
            AddLinkArray(links, mapRoot, "links");
            AddLinkArray(links, mapRoot, "paths");
            AddLinkArray(links, mapRoot, "newLinks");
        }

        if (factionDoc != null && factionDoc.RootElement.ValueKind == JsonValueKind.Object)
            ApplyFactionTerritoryClaims(nodes, factionDoc.RootElement);

        ApplyFallbackLayout(nodes.Values);
        var nodeDtos = nodes.Values
            .Select(static node => node.ToDto())
            .OrderBy(static node => node.Z)
            .ThenBy(static node => node.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var zLevels = nodeDtos
            .Select(static node => node.Z)
            .Distinct()
            .Order()
            .Select(static z => new MapZLevelDto { Z = z, Label = DescribeZLevel(z) })
            .ToList();
        if (zLevels.Count == 0)
            zLevels.Add(new MapZLevelDto { Z = 0, Label = DescribeZLevel(0) });

        return new MapViewDto
        {
            Realm = "Mortal World",
            Title = "Карта смертного мира",
            CurrentNodeId = currentNodeId,
            Layers =
            [
                new MapLayerDto
                {
                    Id = WorldLayerId,
                    Label = "Мир",
                    IsDefault = true
                }
            ],
            ZLevels = zLevels,
            Nodes = nodeDtos,
            Links = links.Values
                .OrderBy(static link => link.SourceNodeId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static link => link.TargetNodeId, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Regions = BuildPoliticalRegions(nodes.Values)
        };
    }

    private static JsonDocument? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonElement UnwrapWorldMapRoot(JsonElement root) =>
        root.TryGetProperty("worldMapUpdates", out var wrapped) && wrapped.ValueKind == JsonValueKind.Object
            ? wrapped
            : root;

    private static JsonElement UnwrapCurrentLocationRoot(JsonElement root) =>
        root.TryGetProperty("currentLocationData", out var wrapped) && wrapped.ValueKind == JsonValueKind.Object
            ? wrapped
            : root;

    private static void AddLocationArray(Dictionary<string, NodeDraft> nodes, JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
            return;

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            AddLocationNode(nodes, item, ResolveLocationId(item, $"location_{nodes.Count + 1}"), isCurrent: false);
        }
    }

    private static void AddLinkArray(Dictionary<string, MapLinkDto> links, JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
            return;

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var source = GetString(item, "sourceLocationId", "sourceId", "fromLocationId");
            var target = GetString(item, "targetLocationId", "targetId", "toLocationId");
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
                continue;

            AddLink(links, source, target, GetString(item, "direction", "label"), GetString(item, "linkState", "state"));
        }
    }

    private static void AddAdjacencyLinks(
        Dictionary<string, NodeDraft> nodes,
        Dictionary<string, MapLinkDto> links,
        string sourceNodeId,
        JsonElement location)
    {
        if (string.IsNullOrWhiteSpace(sourceNodeId) ||
            !location.TryGetProperty("adjacencyMap", out var adjacency) ||
            adjacency.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var entry in adjacency.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
                continue;

            var targetId = GetString(entry, "targetLocationId", "locationId", "id");
            if (string.IsNullOrWhiteSpace(targetId))
                targetId = StableId(GetString(entry, "name", "locationName", "label"), $"adjacent_{nodes.Count + 1}");

            AddLocationNode(nodes, entry, targetId, isCurrent: false);
            AddLink(links, sourceNodeId, targetId, GetString(entry, "direction", "label"), GetString(entry, "linkState", "state"));
        }
    }

    private static void AddLocationNode(Dictionary<string, NodeDraft> nodes, JsonElement location, string id, bool isCurrent)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        if (!nodes.TryGetValue(id, out var draft))
        {
            draft = new NodeDraft { Id = id };
            nodes[id] = draft;
        }

        draft.Label = Prefer(draft.Label, GetString(location, "name", "locationName", "targetLocationName", "targetLocationId"), id);
        draft.Type = Prefer(draft.Type, GetString(location, "locationType", "type"));
        draft.Layer = WorldLayerId;
        draft.IsCurrent |= isCurrent;

        if (TryReadCoordinates(location, out var x, out var y, out var z))
        {
            draft.X = x;
            draft.Y = y;
            draft.Z = z;
            draft.HasCoordinates = true;
        }

        AddDetail(draft, "Тип", draft.Type);
        AddDetail(draft, "Регион", GetString(location, "region", "area"));
        AddDetail(draft, "Биом", GetString(location, "biome"));
        AddDetail(draft, "Известность", GetString(location, "knownState", "discoveryState", "knownStatus", "state"));
        if (TryGetBool(location, out var discovered, "discovered", "isDiscovered", "known"))
            AddDetail(draft, "Открыта", discovered ? "да" : "нет");
        AddDetail(draft, "Описание", GetString(location, "description", "shortDescription"));
        AddDetail(draft, "Последние события", GetString(location, "lastEventsDescription", "recentEvents", "currentStateSummary"));
        AddDetail(draft, "Выходы", DescribeAdjacency(location));
        AddDetail(draft, "Хранилища", DescribeArrayCount(location, "locationStorages"));
        AddDetail(draft, "Угрозы", DescribeArrayCount(location, "activeThreats"));
        if (draft.HasCoordinates)
            AddDetail(draft, "Координаты", $"{draft.X:0.#}, {draft.Y:0.#}, z={draft.Z}");

        ApplyFactionControl(draft, location);
    }

    private static void ApplyFactionControl(NodeDraft draft, JsonElement location)
    {
        if (!location.TryGetProperty("factionControl", out var factions) || factions.ValueKind != JsonValueKind.Array)
            return;

        var bestControl = int.MinValue;
        foreach (var faction in factions.EnumerateArray())
        {
            if (faction.ValueKind != JsonValueKind.Object)
                continue;

            var factionId = GetString(faction, "factionId", "id");
            var factionName = GetString(faction, "factionName", "name");
            var controlType = GetString(faction, "controlType", "type");
            var control = GetInt(faction, "controlLevel", "influence", "value");
            var key = string.IsNullOrWhiteSpace(factionId) ? factionName : factionId;
            if (!string.IsNullOrWhiteSpace(key))
                draft.Influence[key] = control;
            if (!string.IsNullOrWhiteSpace(key))
            {
                draft.FactionControls.Add(new FactionControlDraft
                {
                    FactionId = factionId,
                    FactionName = factionName,
                    ControlType = controlType,
                    ControlLevel = control
                });
            }

            if (control > bestControl)
            {
                bestControl = control;
                draft.OwnerFactionId = factionId;
                draft.OwnerFactionName = factionName;
            }
        }

        AddPoliticalDetails(draft);
    }

    private static void ApplyFactionTerritoryClaims(Dictionary<string, NodeDraft> nodes, JsonElement factionRoot)
    {
        if (!factionRoot.TryGetProperty("factions", out var factions) || factions.ValueKind != JsonValueKind.Array)
            return;

        foreach (var faction in factions.EnumerateArray())
        {
            if (faction.ValueKind != JsonValueKind.Object ||
                !faction.TryGetProperty("controlledTerritories", out var territories) ||
                territories.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var factionId = GetString(faction, "factionId", "id");
            var factionName = GetString(faction, "factionName", "name");
            if (string.IsNullOrWhiteSpace(factionId) && string.IsNullOrWhiteSpace(factionName))
                continue;

            foreach (var territory in territories.EnumerateArray())
            {
                if (territory.ValueKind != JsonValueKind.Object)
                    continue;

                var locationId = GetString(territory, "locationId", "id");
                if (string.IsNullOrWhiteSpace(locationId) || !nodes.TryGetValue(locationId, out var node))
                    continue;

                if (string.IsNullOrWhiteSpace(node.OwnerFactionId) && string.IsNullOrWhiteSpace(node.OwnerFactionName))
                {
                    node.OwnerFactionId = factionId;
                    node.OwnerFactionName = factionName;
                }

                var key = string.IsNullOrWhiteSpace(factionId) ? factionName : factionId;
                if (!string.IsNullOrWhiteSpace(key) && !node.Influence.ContainsKey(key))
                    node.Influence[key] = 100;

                if (node.FactionControls.All(existing =>
                    !string.Equals(existing.FactionId, factionId, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(existing.FactionName, factionName, StringComparison.OrdinalIgnoreCase)))
                {
                    node.FactionControls.Add(new FactionControlDraft
                    {
                        FactionId = factionId,
                        FactionName = factionName,
                        ControlType = "Territory",
                        ControlLevel = 100
                    });
                }

                AddPoliticalDetails(node);
            }
        }
    }

    private static List<MapRegionDto> BuildPoliticalRegions(IEnumerable<NodeDraft> nodes) =>
        nodes
            .Where(static node => !string.IsNullOrWhiteSpace(node.OwnerFactionId) || !string.IsNullOrWhiteSpace(node.OwnerFactionName))
            .GroupBy(
                static node => string.IsNullOrWhiteSpace(node.OwnerFactionId) ? node.OwnerFactionName : node.OwnerFactionId,
                StringComparer.OrdinalIgnoreCase)
            .Select(static group =>
            {
                var first = group.First();
                var ownerId = string.IsNullOrWhiteSpace(first.OwnerFactionId) ? group.Key : first.OwnerFactionId;
                var ownerName = string.IsNullOrWhiteSpace(first.OwnerFactionName) ? ownerId : first.OwnerFactionName;
                return new MapRegionDto
                {
                    Id = $"political_{StableId(ownerId, ownerName)}",
                    Label = ownerName,
                    OwnerFactionId = ownerId,
                    OwnerFactionName = ownerName,
                    Layer = WorldLayerId,
                    NodeIds = group.Select(static node => node.Id).Order(StringComparer.OrdinalIgnoreCase).ToList()
                };
            })
            .OrderBy(static region => region.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static void AddPoliticalDetails(NodeDraft draft)
    {
        if (draft.FactionControls.Count == 0)
            return;

        AddDetail(
            draft,
            "Контроль фракций",
            string.Join("; ", draft.FactionControls
                .OrderByDescending(static control => control.ControlLevel)
                .Select(static control =>
                {
                    var name = string.IsNullOrWhiteSpace(control.FactionName) ? control.FactionId : control.FactionName;
                    var type = string.IsNullOrWhiteSpace(control.ControlType) ? "control" : control.ControlType;
                    return $"{name}: {type} {control.ControlLevel}";
                })));

        if (IsContested(draft.FactionControls))
            AddDetail(draft, "Статус контроля", "спорная зона");
    }

    private static bool IsContested(IReadOnlyCollection<FactionControlDraft> controls)
    {
        var meaningful = controls
            .Where(static control => control.ControlLevel >= MeaningfulFactionInfluence)
            .OrderByDescending(static control => control.ControlLevel)
            .Take(2)
            .ToList();
        return meaningful.Count >= 2 && meaningful[0].ControlLevel - meaningful[1].ControlLevel <= ContestedControlGap;
    }

    private static void AddLink(Dictionary<string, MapLinkDto> links, string sourceId, string targetId, string label, string state)
    {
        var id = $"{sourceId}->{targetId}";
        if (links.ContainsKey(id))
            return;

        links[id] = new MapLinkDto
        {
            Id = id,
            SourceNodeId = sourceId,
            TargetNodeId = targetId,
            Label = label,
            State = state,
            Layer = WorldLayerId
        };
    }

    private static bool TryReadCoordinates(JsonElement value, out double x, out double y, out int z)
    {
        x = 0;
        y = 0;
        z = 0;

        if (value.TryGetProperty("targetCoordinates", out var targetCoordinates) &&
            targetCoordinates.ValueKind == JsonValueKind.Object)
        {
            return TryReadCoordinatesObject(targetCoordinates, out x, out y, out z);
        }

        if (value.TryGetProperty("coordinates", out var coordinates) &&
            coordinates.ValueKind == JsonValueKind.Object)
        {
            return TryReadCoordinatesObject(coordinates, out x, out y, out z);
        }

        return false;
    }

    private static bool TryReadCoordinatesObject(JsonElement coordinates, out double x, out double y, out int z)
    {
        x = GetDouble(coordinates, "x");
        y = GetDouble(coordinates, "y");
        z = GetInt(coordinates, "z");
        return coordinates.TryGetProperty("x", out _) && coordinates.TryGetProperty("y", out _);
    }

    private static void ApplyFallbackLayout(IEnumerable<NodeDraft> nodes)
    {
        var missing = nodes.Where(static node => !node.HasCoordinates).ToList();
        if (missing.Count == 0)
            return;

        var angleStep = Math.Tau / Math.Max(1, missing.Count);
        for (var index = 0; index < missing.Count; index++)
        {
            var angle = index * angleStep;
            missing[index].X = Math.Round(Math.Cos(angle) * 8, 2);
            missing[index].Y = Math.Round(Math.Sin(angle) * 8, 2);
            missing[index].Z = 0;
            AddDetail(missing[index], "Координаты", $"схематические: {missing[index].X:0.#}, {missing[index].Y:0.#}, z=0");
        }
    }

    private static string ResolveLocationId(JsonElement location, string fallback) =>
        StableId(GetString(location, "locationId", "id", "targetLocationId"), StableId(GetString(location, "name", "locationName"), fallback));

    private static string StableId(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var chars = value
            .Trim()
            .Select(static ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_')
            .ToArray();
        var id = new string(chars).Trim('_');
        while (id.Contains("__", StringComparison.Ordinal))
            id = id.Replace("__", "_", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(id) ? fallback : id;
    }

    private static string Prefer(string current, string candidate, string fallback = "") =>
        !string.IsNullOrWhiteSpace(current)
            ? current
            : !string.IsNullOrWhiteSpace(candidate)
                ? candidate.Trim()
                : fallback;

    private static void AddDetail(NodeDraft draft, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            draft.Details.Any(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        draft.Details.Add(new MapDetailItemDto { Key = key, Value = value.Trim() });
    }

    private static string GetString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.String)
                return value.GetString() ?? string.Empty;

            if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                return value.ToString();
        }

        return string.Empty;
    }

    private static bool TryGetBool(JsonElement element, out bool result, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
                continue;

            if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                result = value.GetBoolean();
                return true;
            }

            if (value.ValueKind == JsonValueKind.String &&
                bool.TryParse(value.GetString(), out var parsed))
            {
                result = parsed;
                return true;
            }
        }

        result = false;
        return false;
    }

    private static string DescribeAdjacency(JsonElement location)
    {
        if (!location.TryGetProperty("adjacencyMap", out var adjacency) || adjacency.ValueKind != JsonValueKind.Array)
            return string.Empty;

        var exits = new List<string>();
        foreach (var entry in adjacency.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
                continue;

            var direction = GetString(entry, "direction", "exitLabel", "label");
            var target = GetString(entry, "targetLocationName", "name", "targetLocationId", "locationId", "id");
            var state = GetString(entry, "linkState", "state");
            var text = string.Join(" - ", new[] { direction, target }.Where(static value => !string.IsNullOrWhiteSpace(value)));
            if (!string.IsNullOrWhiteSpace(state))
                text = string.IsNullOrWhiteSpace(text) ? state : $"{text} ({state})";
            if (!string.IsNullOrWhiteSpace(text))
                exits.Add(text);
        }

        return exits.Count == 0 ? string.Empty : string.Join("; ", exits);
    }

    private static string DescribeArrayCount(JsonElement location, string propertyName)
    {
        if (!location.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
            return string.Empty;

        var count = value.GetArrayLength();
        return count == 0 ? string.Empty : count.ToString(CultureInfo.InvariantCulture);
    }

    private static int GetInt(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                return number;

            if (value.ValueKind == JsonValueKind.String &&
                int.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }

    private static double GetDouble(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
            return 0;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
            return number;

        return value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out var parsed)
            ? parsed
            : 0;
    }

    private static string DescribeZLevel(int z) => z switch
    {
        > 0 => $"верхний уровень +{z}",
        < 0 => $"нижний уровень {z}",
        _ => "земля"
    };

    private sealed class NodeDraft
    {
        public string Id { get; init; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public int Z { get; set; }
        public string Layer { get; set; } = WorldLayerId;
        public bool IsCurrent { get; set; }
        public bool HasCoordinates { get; set; }
        public string OwnerFactionId { get; set; } = string.Empty;
        public string OwnerFactionName { get; set; } = string.Empty;
        public Dictionary<string, int> Influence { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<FactionControlDraft> FactionControls { get; } = [];
        public List<MapDetailItemDto> Details { get; } = [];

        public MapNodeDto ToDto() => new()
        {
            Id = Id,
            Label = string.IsNullOrWhiteSpace(Label) ? Id : Label,
            Type = Type,
            X = X,
            Y = Y,
            Z = Z,
            Layer = Layer,
            IsCurrent = IsCurrent,
            OwnerFactionId = OwnerFactionId,
            OwnerFactionName = OwnerFactionName,
            Influence = new Dictionary<string, int>(Influence, StringComparer.OrdinalIgnoreCase),
            Details = Details.ToList()
        };
    }

    private sealed class FactionControlDraft
    {
        public string FactionId { get; init; } = string.Empty;
        public string FactionName { get; init; } = string.Empty;
        public string ControlType { get; init; } = string.Empty;
        public int ControlLevel { get; init; }
    }
}

public static class LocalMapViewerRenderer
{
    public static string BuildStandaloneHtml(MapViewDto map)
    {
        var json = JsonSerializer.Serialize(map, LocalMapViewService.JsonOptions);
        var encodedJson = WebUtility.HtmlEncode(json);
        var title = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(map.Title) ? "Карта" : map.Title);
        var nodeLabels = WebUtility.HtmlEncode(string.Join(", ", map.Nodes.Select(static node => node.Label)));

        return $$"""
        <!doctype html>
        <html lang="ru">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>{{title}}</title>
          <style>
            :root {
              --atlas-ink: #2b2116;
              --atlas-muted: #6f5935;
              --atlas-gold: #cda85a;
              --atlas-blood: #7b241b;
              --atlas-moss: #285238;
              --atlas-parchment: #d8bd82;
              --atlas-parchment-deep: #a9824d;
              --atlas-shadow: rgba(22, 13, 5, .42);
            }
            * { box-sizing: border-box; }
            body { margin: 0; background: #101410; color: #f4ead0; font: 16px Georgia, "Times New Roman", serif; }
            main {
              min-height: 100vh;
              padding: clamp(1rem, 3vw, 2.5rem);
              background:
                radial-gradient(circle at 20% 12%, rgba(207, 166, 83, .24), transparent 24rem),
                radial-gradient(circle at 80% 4%, rgba(91, 32, 24, .18), transparent 22rem),
                linear-gradient(135deg, #111710, #1b1710 62%, #0d1110);
            }
            .map-viewer {
              border: 1px solid rgba(205, 168, 90, .45);
              border-radius: 1.35rem;
              padding: clamp(1rem, 2.4vw, 1.8rem);
              background: linear-gradient(145deg, rgba(32, 39, 30, .96), rgba(19, 21, 17, .94));
              box-shadow: 0 1.4rem 4rem rgba(0, 0, 0, .36), inset 0 0 0 1px rgba(255, 233, 172, .07);
            }
            h1 { margin: 0 0 .35rem; letter-spacing: .04em; color: #f7d991; }
            .map-subtitle { margin: 0 0 1rem; color: #c6b78e; }
            .map-toolbar { display: flex; flex-wrap: wrap; gap: .65rem; align-items: center; margin-bottom: .75rem; }
            .map-toolbar label { display: inline-flex; gap: .4rem; align-items: center; color: #d6c9a6; }
            select, button {
              border: 1px solid rgba(205, 168, 90, .42);
              border-radius: 999px;
              background: #1b241d;
              color: #f4ead0;
              padding: .48rem .72rem;
              box-shadow: inset 0 0 1rem rgba(0, 0, 0, .16);
            }
            button:hover, select:focus { border-color: rgba(247, 217, 145, .82); }
            .map-atlas-frame { position: relative; }
            svg {
              width: 100%;
              height: min(70vh, 48rem);
              border: 2px solid rgba(86, 55, 22, .7);
              border-radius: 1rem;
              background:
                radial-gradient(circle at 18% 22%, rgba(255, 246, 190, .25), transparent 15rem),
                radial-gradient(circle at 86% 72%, rgba(88, 47, 26, .18), transparent 18rem),
                repeating-linear-gradient(35deg, rgba(96, 65, 30, .045) 0 2px, transparent 2px 8px),
                linear-gradient(135deg, var(--atlas-parchment), var(--atlas-parchment-deep));
              box-shadow: inset 0 0 4rem rgba(65, 42, 17, .28), 0 .8rem 2rem var(--atlas-shadow);
              touch-action: none;
            }
            .atlas-texture { opacity: .36; mix-blend-mode: multiply; pointer-events: none; }
            .map-link { stroke: rgba(81, 57, 31, .82); stroke-linecap: round; stroke-dasharray: .25 .36; }
            .map-link--dangerous { stroke: var(--atlas-blood); stroke-dasharray: .08 .28; }
            .map-node { cursor: pointer; outline: none; }
            .map-node circle { filter: drop-shadow(0 .18px .18px rgba(29, 19, 8, .6)); transition: r .15s ease, stroke-width .15s ease; }
            .map-node:hover circle, .map-node:focus circle, .map-node--selected circle { r: .82; stroke-width: .2; }
            .map-node text {
              fill: var(--atlas-ink);
              font: 1.05px Georgia, "Times New Roman", serif;
              paint-order: stroke;
              stroke: rgba(247, 229, 177, .82);
              stroke-width: .18px;
              pointer-events: none;
            }
            .map-empty {
              position: absolute;
              inset: 42% 1rem auto;
              margin: 0 auto;
              width: fit-content;
              max-width: min(90%, 34rem);
              border: 1px solid rgba(86, 55, 22, .35);
              border-radius: .9rem;
              background: rgba(247, 229, 177, .74);
              color: var(--atlas-ink);
              padding: .75rem 1rem;
              box-shadow: 0 .7rem 1.4rem rgba(58, 35, 13, .18);
            }
            .map-empty[hidden] { display: none; }
            .map-legend {
              display: flex;
              flex-wrap: wrap;
              gap: .55rem .9rem;
              align-items: center;
              margin: .8rem 0;
              color: #d9c89c;
            }
            .map-legend strong { color: #f7d991; }
            .map-legend span { display: inline-flex; gap: .35rem; align-items: center; }
            .legend-swatch { width: .75rem; height: .75rem; border: 1px solid rgba(247, 217, 145, .75); border-radius: 50%; background: var(--atlas-blood); }
            .legend-swatch.current { background: var(--atlas-moss); }
            .legend-swatch.faction { background: #80501f; }
            .legend-swatch.contested { background: linear-gradient(135deg, #80501f 0 48%, var(--atlas-blood) 52%); }
            .map-region {
              fill: rgba(128, 80, 31, .16);
              stroke: rgba(105, 60, 22, .42);
              stroke-width: .18;
              stroke-dasharray: .6 .32;
              pointer-events: none;
            }
            .map-region-label {
              fill: rgba(43, 33, 22, .68);
              font: .8px Georgia, "Times New Roman", serif;
              paint-order: stroke;
              stroke: rgba(247, 229, 177, .6);
              stroke-width: .16px;
              pointer-events: none;
            }
            .map-political-halo {
              fill: rgba(128, 80, 31, .24);
              stroke: rgba(128, 80, 31, .38);
              stroke-width: .09;
              pointer-events: none;
            }
            .map-political-halo--contested {
              fill: rgba(123, 36, 27, .22);
              stroke: rgba(123, 36, 27, .58);
              stroke-dasharray: .16 .12;
            }
            .map-node--contested circle {
              stroke: var(--atlas-blood);
              stroke-dasharray: .16 .1;
            }
            .map-card {
              border: 1px solid rgba(205, 168, 90, .32);
              border-radius: 1rem;
              background: rgba(0, 0, 0, .18);
              color: #d8cfb7;
              margin-top: .85rem;
              padding: .9rem;
            }
            .map-card h2 { margin: 0 0 .65rem; color: #f7d991; }
            .map-card dl { display: grid; grid-template-columns: minmax(7rem, 12rem) 1fr; gap: .35rem .75rem; margin: 0; }
            .map-card dt { color: #bdaa7d; }
            .map-card dd { margin: 0; }
          </style>
        </head>
        <body>
          <main>
            <section class="map-viewer" data-layer-state="visible" data-map-json="{{encodedJson}}">
              <h1>{{title}}</h1>
              <p class="map-subtitle">Локальный атлас: уровни, слои, влияние фракций и точки интереса.</p>
              <p hidden>{{nodeLabels}}</p>
              <div class="map-toolbar">
                <label>Уровень <select class="map-z-filter"></select></label>
                <label>Слой <select class="map-layer-filter"></select></label>
                <label><input type="checkbox" class="map-political-toggle" checked> Политическое влияние</label>
                <button type="button" data-zoom="in">Приблизить</button>
                <button type="button" data-zoom="out">Отдалить</button>
                <button type="button" data-reset>Сброс</button>
              </div>
              <div class="map-legend" aria-label="Легенда карты">
                <strong>Легенда карты</strong>
                <span><i class="legend-swatch current"></i>Текущая точка</span>
                <span><i class="legend-swatch"></i>Обычная точка</span>
                <span><i class="legend-swatch faction"></i>Влияние фракций</span>
                <span><i class="legend-swatch contested"></i>Спорная зона</span>
              </div>
              <div class="map-atlas-frame">
                <svg viewBox="-20 -20 40 40" role="img" aria-label="Карта"></svg>
                <p class="map-empty" hidden>Нет точек на выбранном уровне или слое.</p>
              </div>
              <aside class="map-card">Выберите точку на карте.</aside>
            </section>
          </main>
          <script>
            const root = document.querySelector('.map-viewer');
            const map = JSON.parse(root.dataset.mapJson);
            const svg = root.querySelector('svg');
            const card = root.querySelector('.map-card');
            const empty = root.querySelector('.map-empty');
            const zFilter = root.querySelector('.map-z-filter');
            const layerFilter = root.querySelector('.map-layer-filter');
            const politicalToggle = root.querySelector('.map-political-toggle');
            let selectedNodeId = map.currentNodeId ?? '';
            for (const level of map.zLevels ?? []) zFilter.append(new Option(level.label, String(level.z)));
            for (const layer of map.layers ?? []) layerFilter.append(new Option(layer.label, layer.id));
            if (!layerFilter.value && map.layers?.[0]) layerFilter.value = map.layers[0].id;
            function draw() {
              const z = Number(zFilter.value || 0);
              const layer = layerFilter.value || 'world';
              const nodes = (map.nodes ?? []).filter(n => n.z === z && (n.layer ?? 'world') === layer);
              const ids = new Set(nodes.map(n => n.id));
              svg.replaceChildren();
              root.dataset.layerState = nodes.length ? 'visible' : 'hidden';
              empty.hidden = nodes.length !== 0;
              const defs = document.createElementNS('http://www.w3.org/2000/svg', 'defs');
              defs.innerHTML = '<filter id="atlas-texture"><feTurbulence type="fractalNoise" baseFrequency="0.9" numOctaves="3" seed="12"/><feColorMatrix type="saturate" values="0"/></filter>';
              const texture = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
              texture.setAttribute('class', 'atlas-texture');
              texture.setAttribute('x', '-2000'); texture.setAttribute('y', '-2000');
              texture.setAttribute('width', '4000'); texture.setAttribute('height', '4000');
              texture.setAttribute('filter', 'url(#atlas-texture)');
              svg.append(defs, texture);
              if (politicalToggle.checked) drawPoliticalOverlay(nodes);
              for (const link of map.links ?? []) {
                if (!ids.has(link.sourceNodeId) || !ids.has(link.targetNodeId)) continue;
                const a = nodes.find(n => n.id === link.sourceNodeId), b = nodes.find(n => n.id === link.targetNodeId);
                const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                line.setAttribute('x1', a.x); line.setAttribute('y1', -a.y); line.setAttribute('x2', b.x); line.setAttribute('y2', -b.y);
                line.setAttribute('class', `map-link ${link.state === 'dangerous' ? 'map-link--dangerous' : ''}`);
                line.setAttribute('stroke-width', '.16');
                svg.append(line);
              }
              for (const node of nodes) {
                const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
                g.classList.add('map-node');
                if (node.id === selectedNodeId) g.classList.add('map-node--selected');
                if (isContested(node)) g.classList.add('map-node--contested');
                g.setAttribute('tabindex', '0');
                const c = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                c.setAttribute('cx', node.x); c.setAttribute('cy', -node.y); c.setAttribute('r', node.isCurrent ? '.7' : '.48');
                c.setAttribute('fill', node.isCurrent ? '#285238' : node.ownerFactionId ? '#80501f' : '#6a2d22');
                c.setAttribute('stroke', '#f1d58b'); c.setAttribute('stroke-width', '.12');
                const t = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                t.setAttribute('x', Number(node.x) + .75); t.setAttribute('y', -Number(node.y) - .45); t.textContent = node.label;
                g.append(c, t);
                g.addEventListener('click', () => show(node));
                svg.append(g);
              }
              fit(nodes);
            }
            function drawPoliticalOverlay(nodes) {
              const byId = new Map(nodes.map(n => [n.id, n]));
              for (const region of map.regions ?? []) {
                const regionNodes = (region.nodeIds ?? []).map(id => byId.get(id)).filter(Boolean);
                if (!regionNodes.length) continue;
                const xs = regionNodes.map(n => Number(n.x ?? 0));
                const ys = regionNodes.map(n => -Number(n.y ?? 0));
                const cx = xs.reduce((a, b) => a + b, 0) / xs.length;
                const cy = ys.reduce((a, b) => a + b, 0) / ys.length;
                const radius = Math.max(2.2, ...regionNodes.map(n => Math.hypot(Number(n.x ?? 0) - cx, -Number(n.y ?? 0) - cy) + 1.6));
                const halo = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                halo.setAttribute('class', 'map-region');
                halo.setAttribute('cx', cx); halo.setAttribute('cy', cy); halo.setAttribute('r', radius);
                svg.append(halo);
                const label = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                label.setAttribute('class', 'map-region-label');
                label.setAttribute('x', cx - radius * .45); label.setAttribute('y', cy - radius * .72);
                label.textContent = region.ownerFactionName || region.label || region.ownerFactionId || '';
                svg.append(label);
              }
              for (const node of nodes) {
                if (!node.ownerFactionId && !node.ownerFactionName && !Object.keys(node.influence ?? {}).length) continue;
                const halo = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                halo.setAttribute('class', `map-political-halo ${isContested(node) ? 'map-political-halo--contested' : ''}`);
                halo.setAttribute('cx', node.x ?? 0);
                halo.setAttribute('cy', -(node.y ?? 0));
                halo.setAttribute('r', isContested(node) ? '1.22' : '1.02');
                svg.append(halo);
              }
            }
            function isContested(node) {
              return Object.values(node.influence ?? {}).filter(value => Number(value) >= 25).sort((a, b) => Number(b) - Number(a)).slice(0, 2).length >= 2 &&
                Math.abs(Number(Object.values(node.influence ?? {}).sort((a, b) => Number(b) - Number(a))[0]) - Number(Object.values(node.influence ?? {}).sort((a, b) => Number(b) - Number(a))[1])) <= 10;
            }
            function fit(nodes) {
              if (!nodes.length) return;
              const xs = nodes.map(n => Number(n.x)), ys = nodes.map(n => -Number(n.y));
              const minX = Math.min(...xs) - 3, maxX = Math.max(...xs) + 8;
              const minY = Math.min(...ys) - 3, maxY = Math.max(...ys) + 3;
              svg.setAttribute('viewBox', `${minX} ${minY} ${Math.max(8, maxX - minX)} ${Math.max(8, maxY - minY)}`);
            }
            function show(node) {
              selectedNodeId = node.id ?? '';
              for (const item of svg.querySelectorAll('.map-node')) item.classList.remove('map-node--selected');
              const selected = [...svg.querySelectorAll('.map-node')].find(item => item.textContent === node.label);
              if (selected) selected.classList.add('map-node--selected');
              const details = (node.details ?? []).map(i => `<dt>${escapeHtml(i.key)}</dt><dd>${escapeHtml(i.value)}</dd>`).join('');
              card.innerHTML = `<h2>${escapeHtml(node.label)}</h2><dl>${details}</dl>`;
            }
            function escapeHtml(v) { return String(v ?? '').replace(/[&<>"']/g, ch => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[ch])); }
            zFilter.addEventListener('change', draw);
            layerFilter.addEventListener('change', draw);
            politicalToggle.addEventListener('change', draw);
            root.querySelector('[data-reset]').addEventListener('click', draw);
            root.querySelector('[data-zoom="in"]').addEventListener('click', () => zoom(.8));
            root.querySelector('[data-zoom="out"]').addEventListener('click', () => zoom(1.25));
            function zoom(factor) {
              const [x,y,w,h] = svg.getAttribute('viewBox').split(' ').map(Number);
              const nw = w * factor, nh = h * factor;
              svg.setAttribute('viewBox', `${x + (w-nw)/2} ${y + (h-nh)/2} ${nw} ${nh}`);
            }
            draw();
          </script>
        </body>
        </html>
        """;
    }
}

public sealed record LocalMapViewerLaunchResult(bool Opened, string RelativePath, string FullPath, string Error);

public static class LocalMapViewerLauncher
{
    public const string ViewerPath = "output/map_viewer.html";

    public static async Task<LocalMapViewerLaunchResult> WriteAndOpenAsync(
        FileSystemManager fs,
        MapViewDto map,
        Action<string>? openFile = null)
    {
        var html = LocalMapViewerRenderer.BuildStandaloneHtml(map);
        await fs.WriteFileAtomicAsync(ViewerPath, html);
        var fullPath = fs.ResolvePath(ViewerPath);

        try
        {
            if (openFile != null)
            {
                openFile(fullPath);
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fullPath,
                    UseShellExecute = true
                });
            }

            return new LocalMapViewerLaunchResult(true, ViewerPath, fullPath, string.Empty);
        }
        catch (Exception ex)
        {
            return new LocalMapViewerLaunchResult(false, ViewerPath, fullPath, ex.Message);
        }
    }
}
