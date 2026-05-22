using System.Diagnostics;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public static class LocalMapViewService
{
    private const string WorldLayerId = "world";
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    public static async Task<MapViewDto> BuildMortalWorldMapAsync(FileSystemManager fs)
    {
        var currentJson = await fs.ReadFileAsync("game_state/world/current_location.json");
        var worldMapJson = await fs.ReadFileAsync("game_state/world/world_map.json");
        using var currentDoc = TryParse(currentJson);
        using var worldMapDoc = TryParse(worldMapJson);

        var nodes = new Dictionary<string, NodeDraft>(StringComparer.OrdinalIgnoreCase);
        var links = new Dictionary<string, MapLinkDto>(StringComparer.OrdinalIgnoreCase);
        var currentNodeId = string.Empty;

        if (currentDoc != null && currentDoc.RootElement.ValueKind == JsonValueKind.Object)
        {
            var current = currentDoc.RootElement;
            currentNodeId = ResolveLocationId(current, "current_location");
            AddLocationNode(nodes, current, currentNodeId, isCurrent: true);
            AddAdjacencyLinks(nodes, links, currentNodeId, current);
        }

        if (worldMapDoc != null && worldMapDoc.RootElement.ValueKind == JsonValueKind.Object)
        {
            var mapRoot = UnwrapWorldMapRoot(worldMapDoc.RootElement);
            AddLocationArray(nodes, mapRoot, "newLocations");
            AddLocationArray(nodes, mapRoot, "locationUpdates");
            AddLinkArray(links, mapRoot, "newLinks");
        }

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
                .ToList()
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
        AddDetail(draft, "Описание", GetString(location, "description", "shortDescription"));
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
            var control = GetInt(faction, "controlLevel", "influence", "value");
            var key = string.IsNullOrWhiteSpace(factionId) ? factionName : factionId;
            if (!string.IsNullOrWhiteSpace(key))
                draft.Influence[key] = control;

            if (control > bestControl)
            {
                bestControl = control;
                draft.OwnerFactionId = factionId;
                draft.OwnerFactionName = factionName;
            }
        }
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
            body { margin: 0; background: #111612; color: #f2ead2; font: 16px Georgia, "Times New Roman", serif; }
            main { min-height: 100vh; padding: 1rem; background: radial-gradient(circle at 20% 12%, rgba(220,177,85,.18), transparent 22rem), #111612; }
            .map-viewer { border: 1px solid rgba(220,177,85,.35); border-radius: 1rem; padding: 1rem; background: rgba(25,32,28,.92); }
            .map-toolbar { display: flex; flex-wrap: wrap; gap: .65rem; align-items: center; margin-bottom: .75rem; }
            select, button { border: 1px solid rgba(220,177,85,.35); border-radius: .65rem; background: #1c241f; color: #f2ead2; padding: .45rem .65rem; }
            svg { width: 100%; height: min(70vh, 46rem); border-radius: .85rem; background: #d7c190; box-shadow: inset 0 0 4rem rgba(65,42,17,.25); }
            .map-card { margin-top: .75rem; color: #d8cfb7; }
          </style>
        </head>
        <body>
          <main>
            <section class="map-viewer" data-map-json="{{encodedJson}}">
              <h1>{{title}}</h1>
              <p hidden>{{nodeLabels}}</p>
              <div class="map-toolbar">
                <label>Уровень <select class="map-z-filter"></select></label>
                <label>Слой <select class="map-layer-filter"></select></label>
                <button type="button" data-zoom="in">Приблизить</button>
                <button type="button" data-zoom="out">Отдалить</button>
                <button type="button" data-reset>Сброс</button>
              </div>
              <svg viewBox="-20 -20 40 40" role="img" aria-label="Карта"></svg>
              <aside class="map-card">Выберите точку на карте.</aside>
            </section>
          </main>
          <script>
            const root = document.querySelector('.map-viewer');
            const map = JSON.parse(root.dataset.mapJson);
            const svg = root.querySelector('svg');
            const card = root.querySelector('.map-card');
            const zFilter = root.querySelector('.map-z-filter');
            const layerFilter = root.querySelector('.map-layer-filter');
            for (const level of map.zLevels ?? []) zFilter.append(new Option(level.label, String(level.z)));
            for (const layer of map.layers ?? []) layerFilter.append(new Option(layer.label, layer.id));
            if (!layerFilter.value && map.layers?.[0]) layerFilter.value = map.layers[0].id;
            function draw() {
              const z = Number(zFilter.value || 0);
              const layer = layerFilter.value || 'world';
              const nodes = (map.nodes ?? []).filter(n => n.z === z && (n.layer ?? 'world') === layer);
              const ids = new Set(nodes.map(n => n.id));
              svg.replaceChildren();
              for (const link of map.links ?? []) {
                if (!ids.has(link.sourceNodeId) || !ids.has(link.targetNodeId)) continue;
                const a = nodes.find(n => n.id === link.sourceNodeId), b = nodes.find(n => n.id === link.targetNodeId);
                const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                line.setAttribute('x1', a.x); line.setAttribute('y1', -a.y); line.setAttribute('x2', b.x); line.setAttribute('y2', -b.y);
                line.setAttribute('stroke', '#6d5630'); line.setAttribute('stroke-width', '.16');
                svg.append(line);
              }
              for (const node of nodes) {
                const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
                g.setAttribute('tabindex', '0');
                const c = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                c.setAttribute('cx', node.x); c.setAttribute('cy', -node.y); c.setAttribute('r', node.isCurrent ? '.7' : '.48');
                c.setAttribute('fill', node.isCurrent ? '#244d2d' : '#6a2d22');
                c.setAttribute('stroke', '#f1d58b'); c.setAttribute('stroke-width', '.12');
                const t = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                t.setAttribute('x', Number(node.x) + .75); t.setAttribute('y', -Number(node.y) - .45); t.textContent = node.label;
                t.setAttribute('font-size', '1.1'); t.setAttribute('fill', '#2c2112');
                g.append(c, t);
                g.addEventListener('click', () => show(node));
                svg.append(g);
              }
              fit(nodes);
            }
            function fit(nodes) {
              if (!nodes.length) return;
              const xs = nodes.map(n => Number(n.x)), ys = nodes.map(n => -Number(n.y));
              const minX = Math.min(...xs) - 3, maxX = Math.max(...xs) + 8;
              const minY = Math.min(...ys) - 3, maxY = Math.max(...ys) + 3;
              svg.setAttribute('viewBox', `${minX} ${minY} ${Math.max(8, maxX - minX)} ${Math.max(8, maxY - minY)}`);
            }
            function show(node) {
              const details = (node.details ?? []).map(i => `<dt>${escapeHtml(i.key)}</dt><dd>${escapeHtml(i.value)}</dd>`).join('');
              card.innerHTML = `<h2>${escapeHtml(node.label)}</h2><dl>${details}</dl>`;
            }
            function escapeHtml(v) { return String(v ?? '').replace(/[&<>"']/g, ch => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[ch])); }
            zFilter.addEventListener('change', draw);
            layerFilter.addEventListener('change', draw);
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
