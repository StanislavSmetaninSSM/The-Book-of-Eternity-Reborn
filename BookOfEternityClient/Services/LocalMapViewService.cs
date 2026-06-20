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
    private const string ChaosSeaLayerId = "chaos_sea";
    private const string ShiningAbodeLayerId = "shining_abode";
    private const int MeaningfulFactionInfluence = 25;
    private const int ContestedControlGap = 10;
    private const string ImageVersionSeparator = "__img_";
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    public static async Task<MapViewDto> BuildCurrentRealmMapAsync(FileSystemManager fs)
    {
        var soulJson = await fs.ReadFileAsync("game_state/meta/soul_state.json");
        using var soulDoc = TryParse(soulJson);
        var currentRealm = soulDoc?.RootElement.ValueKind == JsonValueKind.Object
            ? GetString(soulDoc.RootElement, "currentRealm")
            : string.Empty;

        if (RealmSemantics.IsChaosSea(currentRealm))
            return await BuildChaosSeaMapAsync(fs);
        if (RealmSemantics.IsShiningRealm(currentRealm))
            return await BuildShiningAbodeMapAsync(fs);

        return await BuildMortalWorldMapAsync(fs);
    }

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
        FinalizePlaceholderDetails(nodes.Values);
        AttachLocationImages(fs, nodes.Values);
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

    public static async Task<MapViewDto> BuildChaosSeaMapAsync(FileSystemManager fs)
    {
        var guardiansJson = await fs.ReadFileAsync("game_state/meta/guardians.json");
        using var guardiansDoc = TryParse(guardiansJson);

        var nodes = new Dictionary<string, NodeDraft>(StringComparer.OrdinalIgnoreCase);
        var links = new Dictionary<string, MapLinkDto>(StringComparer.OrdinalIgnoreCase);
        var currentAbodeId = string.Empty;

        if (guardiansDoc != null && guardiansDoc.RootElement.ValueKind == JsonValueKind.Object)
        {
            var root = guardiansDoc.RootElement;
            var activeGuardianId = root.TryGetProperty("activeGuardian", out var activeGuardian) &&
                                   activeGuardian.ValueKind == JsonValueKind.Object
                ? GetString(activeGuardian, "guardianId", "id")
                : string.Empty;

            var discoveredAbodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var hintedAbodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lockedAbodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (root.TryGetProperty("chaosSeaNavigation", out var navigation) &&
                navigation.ValueKind == JsonValueKind.Object)
            {
                currentAbodeId = GetString(navigation, "currentAbodeId", "abodeId");
                AddNavigationAbodeArray(nodes, discoveredAbodeIds, navigation, "discoveredAbodes", "открыта");
                AddNavigationAbodeArray(nodes, discoveredAbodeIds, navigation, "knownAbodes", "открыта");
                AddNavigationAbodeArray(nodes, hintedAbodeIds, navigation, "hintedAbodes", "намёк");
                AddNavigationAbodeArray(nodes, hintedAbodeIds, navigation, "rumoredAbodes", "намёк");
                AddNavigationAbodeArray(nodes, lockedAbodeIds, navigation, "lockedAbodes", "закрыта");
                AddNavigationAbodeArray(nodes, lockedAbodeIds, navigation, "unknownAbodes", "неизвестна");
                AddChaosNavigationLinks(links, navigation);
            }

            if (root.TryGetProperty("guardians", out var guardians) && guardians.ValueKind == JsonValueKind.Array)
            {
                foreach (var guardian in guardians.EnumerateArray())
                {
                    if (guardian.ValueKind != JsonValueKind.Object)
                        continue;

                    AddGuardianAbodeNode(
                        nodes,
                        guardian,
                        activeGuardianId,
                        currentAbodeId,
                        discoveredAbodeIds,
                        hintedAbodeIds,
                        lockedAbodeIds);
                }
            }

            if (string.IsNullOrWhiteSpace(currentAbodeId) &&
                !string.IsNullOrWhiteSpace(activeGuardianId) &&
                nodes.Values.FirstOrDefault(node => string.Equals(node.GuardianId, activeGuardianId, StringComparison.OrdinalIgnoreCase)) is { } activeNode)
            {
                currentAbodeId = activeNode.Id;
            }
        }

        if (!string.IsNullOrWhiteSpace(currentAbodeId) && nodes.TryGetValue(currentAbodeId, out var currentNode))
        {
            currentNode.IsCurrent = true;
            AddDetail(currentNode, "Текущая Обитель", "да");
        }

        ApplyChaosSeaLayout(nodes.Values, currentAbodeId);
        AddChaosSeaFallbackLinks(links, nodes.Values, currentAbodeId);

        var nodeDtos = nodes.Values
            .Select(static node => node.ToDto())
            .OrderByDescending(static node => node.IsCurrent)
            .ThenBy(static node => node.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new MapViewDto
        {
            Realm = "Chaos Sea",
            Title = "Карта Моря Хаоса: созвездие Обителей",
            CurrentNodeId = currentAbodeId,
            Layers =
            [
                new MapLayerDto
                {
                    Id = ChaosSeaLayerId,
                    Label = "Море Хаоса",
                    IsDefault = true
                }
            ],
            ZLevels = [new MapZLevelDto { Z = 0, Label = "созвездие обителей" }],
            Nodes = nodeDtos,
            Links = links.Values
                .OrderBy(static link => link.SourceNodeId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static link => link.TargetNodeId, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    public static async Task<MapViewDto> BuildShiningAbodeMapAsync(FileSystemManager fs)
    {
        var shiningJson = await fs.ReadFileAsync("game_state/meta/shining_abode_state.json");
        using var shiningDoc = TryParse(shiningJson);

        var nodes = new Dictionary<string, NodeDraft>(StringComparer.OrdinalIgnoreCase);
        var links = new Dictionary<string, MapLinkDto>(StringComparer.OrdinalIgnoreCase);
        var currentHallId = string.Empty;

        if (shiningDoc != null && shiningDoc.RootElement.ValueKind == JsonValueKind.Object)
        {
            var root = shiningDoc.RootElement;
            currentHallId = GetString(root, "currentHallId", "activeHallId", "selectedHallId");
            AddShiningHallArray(nodes, root);
            AddShiningFactionArray(nodes, links, root);
        }

        if (!string.IsNullOrWhiteSpace(currentHallId) && nodes.TryGetValue(currentHallId, out var currentHall))
        {
            currentHall.IsCurrent = true;
            AddDetail(currentHall, "Текущий зал", "да");
        }

        ApplyShiningAbodeLayout(nodes.Values, currentHallId);

        var nodeDtos = nodes.Values
            .Select(static node => node.ToDto())
            .OrderByDescending(static node => node.IsCurrent)
            .ThenBy(static node => node.Type, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static node => node.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new MapViewDto
        {
            Realm = "Shining Abode",
            Title = "Карта Сияющей Обители: залы и фракции",
            CurrentNodeId = currentHallId,
            Layers =
            [
                new MapLayerDto
                {
                    Id = ShiningAbodeLayerId,
                    Label = "Сияющая Обитель",
                    IsDefault = true
                }
            ],
            ZLevels = [new MapZLevelDto { Z = 0, Label = "мандала залов" }],
            Nodes = nodeDtos,
            Links = links.Values
                .OrderBy(static link => link.SourceNodeId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static link => link.TargetNodeId, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Regions = BuildPoliticalRegions(nodes.Values)
        };
    }

    private static void AddShiningHallArray(Dictionary<string, NodeDraft> nodes, JsonElement root)
    {
        if (!root.TryGetProperty("halls", out var halls) || halls.ValueKind != JsonValueKind.Array)
            return;

        foreach (var hall in halls.EnumerateArray())
        {
            if (hall.ValueKind != JsonValueKind.Object)
                continue;

            var hallId = GetString(hall, "hallId", "id");
            if (string.IsNullOrWhiteSpace(hallId))
                hallId = StableId(GetString(hall, "hallName", "name", "displayName"), $"hall_{nodes.Count + 1}");
            var draft = EnsureShiningHallNode(nodes, hallId, GetString(hall, "hallName", "name", "displayName"));
            AddDetail(draft, "Тип", GetString(hall, "hallType", "type"));
            AddDetail(draft, "Описание", GetString(hall, "description", "summary"));
            AddDetail(draft, "Статус", GetString(hall, "status", "state"));
        }
    }

    private static void AddShiningFactionArray(
        Dictionary<string, NodeDraft> nodes,
        Dictionary<string, MapLinkDto> links,
        JsonElement root)
    {
        if (!root.TryGetProperty("factions", out var factions) || factions.ValueKind != JsonValueKind.Array)
            return;

        foreach (var faction in factions.EnumerateArray())
        {
            if (faction.ValueKind != JsonValueKind.Object)
                continue;

            var factionId = GetString(faction, "factionId", "id");
            if (string.IsNullOrWhiteSpace(factionId))
                factionId = StableId(GetShiningFactionName(faction), $"faction_{nodes.Count + 1}");

            var factionName = GetShiningFactionName(faction);
            var hallId = GetString(faction, "hallId", "homeHallId", "controlledHallId");
            if (string.IsNullOrWhiteSpace(hallId))
                hallId = "hall_unassigned";

            var hall = EnsureShiningHallNode(
                nodes,
                hallId,
                string.Equals(hallId, "hall_unassigned", StringComparison.OrdinalIgnoreCase)
                    ? "Без закреплённого зала"
                    : hallId);
            var strength = GetInt(faction, "factionStrength", "strength", "influence", "controlLevel");
            var factionNode = GetOrCreateNode(nodes, factionId);
            factionNode.Layer = ShiningAbodeLayerId;
            factionNode.Type = "shining_faction";
            factionNode.ParentHallId = hallId;
            factionNode.OwnerFactionId = factionId;
            factionNode.OwnerFactionName = factionName;
            factionNode.Influence[factionId] = strength;
            factionNode.FactionControls.Add(new FactionControlDraft
            {
                FactionId = factionId,
                FactionName = factionName,
                ControlType = "Сила фракции",
                ControlLevel = strength
            });
            factionNode.Label = Prefer(factionNode.Label, factionName, factionId);

            ApplyShiningFactionInfluenceToHall(hall, factionId, factionName, strength);
            AddShiningLink(links, hallId, factionId, "политическое влияние", "known");
            AddShiningFactionTerritorialInfluence(nodes, links, faction, factionId, factionName);

            AddDetail(factionNode, "Фракция", factionName);
            AddDetail(factionNode, "Зал", hall.Label);
            AddDetail(factionNode, "Сила", strength > 0 ? strength.ToString(CultureInfo.InvariantCulture) : string.Empty);
            AddDetail(factionNode, "Лидерство", DescribeShiningLeadership(faction));
            AddDetail(factionNode, "Резиденты", DescribeShiningFactionResidents(root, factionId));
            AddDetail(factionNode, "Проекты", DescribeShiningFactionProjects(faction, root, factionId));
            AddPoliticalDetails(factionNode);
        }

        foreach (var hall in nodes.Values.Where(static node => string.Equals(node.Type, "shining_hall", StringComparison.OrdinalIgnoreCase)))
            AddPoliticalDetails(hall);
    }

    private static void AddShiningFactionTerritorialInfluence(
        Dictionary<string, NodeDraft> nodes,
        Dictionary<string, MapLinkDto> links,
        JsonElement faction,
        string factionId,
        string factionName)
    {
        if (!faction.TryGetProperty(ShiningAbodeState.FactionInfluenceProperty, out var zones) ||
            zones.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var zone in zones.EnumerateArray())
        {
            if (zone.ValueKind != JsonValueKind.Object)
                continue;

            var scopeId = GetString(zone, "scopeId", "hallId", "locationId", "zoneId");
            if (string.IsNullOrWhiteSpace(scopeId))
                continue;

            var scopeType = GetString(zone, "scopeType", "type");
            var label = GetString(zone, "displayName", "zoneName", "name", "scopeId");
            var controlLevel = GetInt(zone, "controlLevel", "influenceValue");
            var target = IsShiningHallScope(scopeType)
                ? EnsureShiningHallNode(nodes, scopeId, string.IsNullOrWhiteSpace(label) ? scopeId : label)
                : GetOrCreateNode(nodes, scopeId);
            target.Layer = ShiningAbodeLayerId;
            if (string.IsNullOrWhiteSpace(target.Type))
                target.Type = string.IsNullOrWhiteSpace(scopeType) ? "shining_influence_zone" : scopeType;
            target.Label = Prefer(target.Label, label, scopeId);

            ApplyShiningFactionZoneInfluence(target, factionId, factionName, label, controlLevel);
            AddShiningLink(links, factionId, scopeId, "зона влияния", GetString(zone, "publicStatus", "state", "known"));
        }
    }

    private static NodeDraft EnsureShiningHallNode(Dictionary<string, NodeDraft> nodes, string hallId, string label)
    {
        var draft = GetOrCreateNode(nodes, hallId);
        draft.Layer = ShiningAbodeLayerId;
        draft.Type = "shining_hall";
        draft.Label = Prefer(draft.Label, label, hallId);
        return draft;
    }

    private static void ApplyShiningFactionInfluenceToHall(NodeDraft hall, string factionId, string factionName, int strength)
    {
        if (!string.IsNullOrWhiteSpace(factionId))
            hall.Influence[factionId] = strength;

        hall.FactionControls.Add(new FactionControlDraft
        {
            FactionId = factionId,
            FactionName = factionName,
            ControlType = "Сила фракции",
            ControlLevel = strength
        });

        var previousBest = hall.FactionControls
            .Where(static control => !string.IsNullOrWhiteSpace(control.FactionId) || !string.IsNullOrWhiteSpace(control.FactionName))
            .OrderByDescending(static control => control.ControlLevel)
            .FirstOrDefault();
        if (previousBest != null)
        {
            hall.OwnerFactionId = previousBest.FactionId;
            hall.OwnerFactionName = previousBest.FactionName;
        }
    }

    private static void ApplyShiningFactionZoneInfluence(
        NodeDraft node,
        string factionId,
        string factionName,
        string zoneLabel,
        int controlLevel)
    {
        if (!string.IsNullOrWhiteSpace(factionId))
        {
            node.Influence[factionId] = node.Influence.TryGetValue(factionId, out var previous)
                ? Math.Max(previous, controlLevel)
                : controlLevel;
        }

        node.FactionControls.Add(new FactionControlDraft
        {
            FactionId = factionId,
            FactionName = factionName,
            ControlType = string.IsNullOrWhiteSpace(zoneLabel) ? "Зона влияния" : zoneLabel,
            ControlLevel = controlLevel
        });

        var best = node.FactionControls
            .Where(static control => !string.IsNullOrWhiteSpace(control.FactionId) || !string.IsNullOrWhiteSpace(control.FactionName))
            .OrderByDescending(static control => control.ControlLevel)
            .FirstOrDefault();
        if (best != null)
        {
            node.OwnerFactionId = best.FactionId;
            node.OwnerFactionName = best.FactionName;
        }
    }

    private static bool IsShiningHallScope(string scopeType) =>
        string.IsNullOrWhiteSpace(scopeType) ||
        string.Equals(scopeType, "hall", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(scopeType, "shining_hall", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(scopeType, "district", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(scopeType, "location", StringComparison.OrdinalIgnoreCase);

    private static string GetShiningFactionName(JsonElement faction)
    {
        var name = GetString(faction, "factionName", "name", "displayName");
        if (!string.IsNullOrWhiteSpace(name))
            return name;

        return faction.TryGetProperty("charter", out var charter) && charter.ValueKind == JsonValueKind.Object
            ? GetString(charter, "factionName", "name", "displayName")
            : string.Empty;
    }

    private static string DescribeShiningLeadership(JsonElement faction)
    {
        if (!faction.TryGetProperty("leadership", out var leadership) || leadership.ValueKind != JsonValueKind.Object)
            return string.Empty;

        var parts = new[]
        {
            GetString(leadership, "leadershipState", "state"),
            GetString(leadership, "headActorType", "leaderType"),
            GetString(leadership, "headActorId", "leaderId")
        }.Where(static value => !string.IsNullOrWhiteSpace(value));
        return string.Join(", ", parts);
    }

    private static string DescribeShiningFactionResidents(JsonElement root, string factionId)
    {
        var names = new List<string>();
        AddShiningResidentNames(names, root, "residents", factionId);
        AddShiningResidentNames(names, root, "shiningResidents", factionId);
        AddShiningResidentNames(names, root, "shiningPoliticalActors", factionId);
        return names.Count == 0 ? string.Empty : string.Join("; ", names.Take(6));
    }

    private static void AddShiningResidentNames(List<string> names, JsonElement root, string propertyName, string factionId)
    {
        if (!root.TryGetProperty(propertyName, out var residents) || residents.ValueKind != JsonValueKind.Array)
            return;

        foreach (var resident in residents.EnumerateArray())
        {
            if (resident.ValueKind != JsonValueKind.Object)
                continue;

            var residentFaction = GetString(resident, "shiningFactionId", "factionId", "affiliatedFactionId");
            if (!string.Equals(residentFaction, factionId, StringComparison.OrdinalIgnoreCase))
                continue;

            var name = GetString(resident, "displayName", "residentName", "name", "actorName");
            if (!string.IsNullOrWhiteSpace(name))
                names.Add(name);
        }
    }

    private static string DescribeShiningFactionProjects(JsonElement faction, JsonElement root, string factionId)
    {
        var names = new List<string>();
        AddShiningProjectNames(names, faction, "projects", factionId, requireFactionMatch: false);
        AddShiningProjectNames(names, faction, "activeProjects", factionId, requireFactionMatch: false);
        AddShiningProjectNames(names, root, "projects", factionId, requireFactionMatch: true);
        AddShiningProjectNames(names, root, "factionProjects", factionId, requireFactionMatch: true);
        return names.Count == 0 ? string.Empty : string.Join("; ", names.Take(6));
    }

    private static void AddShiningProjectNames(
        List<string> names,
        JsonElement source,
        string propertyName,
        string factionId,
        bool requireFactionMatch)
    {
        if (!source.TryGetProperty(propertyName, out var projects) || projects.ValueKind != JsonValueKind.Array)
            return;

        foreach (var project in projects.EnumerateArray())
        {
            if (project.ValueKind != JsonValueKind.Object)
                continue;

            if (requireFactionMatch)
            {
                var projectFactionId = GetString(project, "factionId", "shiningFactionId", "ownerFactionId");
                if (!string.Equals(projectFactionId, factionId, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            var name = GetString(project, "displayName", "projectName", "title", "name", "projectId");
            var status = GetString(project, "status", "state");
            if (!string.IsNullOrWhiteSpace(status))
                name = string.IsNullOrWhiteSpace(name) ? status : $"{name} ({status})";
            if (!string.IsNullOrWhiteSpace(name))
                names.Add(name);
        }
    }

    private static void AddShiningLink(Dictionary<string, MapLinkDto> links, string sourceId, string targetId, string label, string state)
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
            Layer = ShiningAbodeLayerId,
            Z = 0
        };
    }

    private static void ApplyShiningAbodeLayout(IEnumerable<NodeDraft> nodes, string currentHallId)
    {
        var nodeList = nodes.ToList();
        var halls = nodeList
            .Where(static node => string.Equals(node.Type, "shining_hall", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(node => string.Equals(node.Id, currentHallId, StringComparison.OrdinalIgnoreCase))
            .ThenBy(static node => node.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var angleStep = Math.Tau / Math.Max(1, halls.Count);
        for (var index = 0; index < halls.Count; index++)
        {
            var hall = halls[index];
            hall.Z = 0;
            hall.HasCoordinates = true;
            if (!string.IsNullOrWhiteSpace(currentHallId) &&
                string.Equals(hall.Id, currentHallId, StringComparison.OrdinalIgnoreCase))
            {
                hall.X = 0;
                hall.Y = 0;
                AddDetail(hall, "Координаты", "центр мандалы залов");
                continue;
            }

            var angle = index * angleStep + (StableHash(hall.Id) % 1000) / 1000.0 * 0.35;
            var radius = 6.2 + (StableHash(hall.Label) % 4) * 0.55;
            hall.X = Math.Round(Math.Cos(angle) * radius, 2);
            hall.Y = Math.Round(Math.Sin(angle) * radius, 2);
            AddDetail(hall, "Координаты", $"мандала: {hall.X:0.#}, {hall.Y:0.#}");
        }

        var hallsById = halls.ToDictionary(static hall => hall.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var faction in nodeList.Where(static node => string.Equals(node.Type, "shining_faction", StringComparison.OrdinalIgnoreCase)))
        {
            faction.Z = 0;
            faction.HasCoordinates = true;
            hallsById.TryGetValue(faction.ParentHallId, out var hall);
            var baseX = hall?.X ?? 0;
            var baseY = hall?.Y ?? 0;
            var angle = StableHash(faction.Id) / (double)uint.MaxValue * Math.Tau;
            faction.X = Math.Round(baseX + Math.Cos(angle) * 2.3, 2);
            faction.Y = Math.Round(baseY + Math.Sin(angle) * 2.3, 2);
            AddDetail(faction, "Координаты", $"политическая орбита: {faction.X:0.#}, {faction.Y:0.#}");
        }
    }

    private static void AddNavigationAbodeArray(
        Dictionary<string, NodeDraft> nodes,
        ISet<string> abodeIds,
        JsonElement navigation,
        string propertyName,
        string discoveryState)
    {
        if (!navigation.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
            return;

        foreach (var entry in array.EnumerateArray())
        {
            var abodeId = entry.ValueKind == JsonValueKind.Object
                ? GetString(entry, "abodeId", "id")
                : entry.ValueKind == JsonValueKind.String
                    ? entry.GetString() ?? string.Empty
                    : string.Empty;
            if (string.IsNullOrWhiteSpace(abodeId))
                continue;

            abodeIds.Add(abodeId);
            var draft = GetOrCreateNode(nodes, abodeId);
            draft.Layer = ChaosSeaLayerId;
            draft.Type = Prefer(draft.Type, "guardian_abode");
            draft.Label = Prefer(draft.Label, entry.ValueKind == JsonValueKind.Object ? GetString(entry, "name", "title", "abodeName") : string.Empty, abodeId);
            draft.GuardianId = Prefer(draft.GuardianId, entry.ValueKind == JsonValueKind.Object ? GetString(entry, "guardianId") : string.Empty);
            AddDetail(draft, "Открытие", discoveryState);
            AddDetail(draft, "Хранитель", entry.ValueKind == JsonValueKind.Object ? GetString(entry, "guardianName", "guardianDisplayName") : string.Empty);
        }
    }

    private static void AddGuardianAbodeNode(
        Dictionary<string, NodeDraft> nodes,
        JsonElement guardian,
        string activeGuardianId,
        string currentAbodeId,
        IReadOnlySet<string> discoveredAbodeIds,
        IReadOnlySet<string> hintedAbodeIds,
        IReadOnlySet<string> lockedAbodeIds)
    {
        if (!guardian.TryGetProperty("abode", out var abode) || abode.ValueKind != JsonValueKind.Object)
            return;

        var abodeId = GetString(abode, "abodeId", "id");
        if (string.IsNullOrWhiteSpace(abodeId))
            return;

        var guardianId = GetString(guardian, "guardianId", "id");
        var isCurrent = string.Equals(abodeId, currentAbodeId, StringComparison.OrdinalIgnoreCase);
        var isActiveGuardian = !string.IsNullOrWhiteSpace(guardianId) &&
                               string.Equals(guardianId, activeGuardianId, StringComparison.OrdinalIgnoreCase);
        var isDiscovered = discoveredAbodeIds.Contains(abodeId) ||
                           isCurrent ||
                           isActiveGuardian ||
                           (TryGetBool(abode, out var abodeDiscovered, "isDiscovered", "discovered", "known") && abodeDiscovered);
        var isHinted = hintedAbodeIds.Contains(abodeId);
        var isLocked = lockedAbodeIds.Contains(abodeId);
        if (!isDiscovered && !isHinted && !isLocked)
            return;

        var draft = GetOrCreateNode(nodes, abodeId);
        draft.Layer = ChaosSeaLayerId;
        draft.Type = isLocked ? "locked_guardian_abode" : isHinted ? "hinted_guardian_abode" : "guardian_abode";
        draft.Label = Prefer(draft.Label, GetString(abode, "name", "title", "abodeName"), abodeId);
        draft.IsCurrent |= isCurrent;
        draft.GuardianId = Prefer(draft.GuardianId, guardianId);
        draft.Domain = Prefer(draft.Domain, GetString(guardian, "domain", "guardianDomain"));

        AddDetail(draft, "Открытие", isDiscovered ? "открыта" : isHinted ? "намёк" : "закрыта");
        AddDetail(draft, "Хранитель", GetString(guardian, "canonicalName", "guardianName", "name", "displayName"));
        AddDetail(draft, "Активный Хранитель", isActiveGuardian ? "да" : string.Empty);
        AddDetail(draft, "Домен", draft.Domain);
        AddDetail(draft, "Репутация", DescribeGuardianReputation(guardian));
        AddDetail(draft, "Сила Обители", DescribeAbodePower(guardian, abode));
        AddDetail(draft, "Резиденты", DescribeFirstArrayCount(abode, guardian, "residents", "abodeResidents", "residentCompanions"));
        AddDetail(draft, "Проекты", DescribeFirstArrayCount(guardian, abode, "projects", "guardianProjects", "activeProjects"));
        AddDetail(draft, "Действия", DescribeStringArray(abode, "availableActions", "actions", "availableAbodeActions"));
    }

    private static NodeDraft GetOrCreateNode(Dictionary<string, NodeDraft> nodes, string id)
    {
        if (!nodes.TryGetValue(id, out var draft))
        {
            draft = new NodeDraft { Id = id };
            nodes[id] = draft;
        }

        return draft;
    }

    private static string DescribeGuardianReputation(JsonElement guardian)
    {
        if (guardian.TryGetProperty("relationshipData", out var relationship) && relationship.ValueKind == JsonValueKind.Object)
        {
            var reputation = GetInt(relationship, "currentReputation", "reputation", "value");
            if (relationship.TryGetProperty("currentReputation", out _) ||
                relationship.TryGetProperty("reputation", out _) ||
                relationship.TryGetProperty("value", out _))
            {
                return reputation.ToString(CultureInfo.InvariantCulture);
            }
        }

        if (guardian.TryGetProperty("currentReputation", out _) || guardian.TryGetProperty("reputation", out _))
            return GetInt(guardian, "currentReputation", "reputation").ToString(CultureInfo.InvariantCulture);

        return string.Empty;
    }

    private static string DescribeAbodePower(JsonElement guardian, JsonElement abode)
    {
        var power = guardian.TryGetProperty("abodePower", out var guardianPower) && guardianPower.ValueKind == JsonValueKind.Object
            ? guardianPower
            : abode.TryGetProperty("abodePower", out var abodePower) && abodePower.ValueKind == JsonValueKind.Object
                ? abodePower
                : default;

        if (power.ValueKind != JsonValueKind.Object)
            return string.Empty;

        var current = GetInt(power, "currentPower", "power", "current");
        var max = GetInt(power, "maxPower", "maximum", "max");
        return max > 0
            ? $"{current}/{max}"
            : current > 0
                ? current.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
    }

    private static string DescribeFirstArrayCount(JsonElement primary, JsonElement secondary, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var count = TryCountArray(primary, propertyName);
            if (count > 0)
                return count.ToString(CultureInfo.InvariantCulture);

            count = TryCountArray(secondary, propertyName);
            if (count > 0)
                return count.ToString(CultureInfo.InvariantCulture);
        }

        return string.Empty;
    }

    private static int TryCountArray(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.Array
            ? value.GetArrayLength()
            : 0;

    private static string DescribeStringArray(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty(propertyName, out var array) ||
                array.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var values = array.EnumerateArray()
                .Select(static item => item.ValueKind == JsonValueKind.String
                    ? item.GetString()
                    : item.ValueKind == JsonValueKind.Object
                        ? GetString(item, "label", "name", "actionType", "id")
                        : string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Take(6)
                .ToList();
            if (values.Count > 0)
                return string.Join("; ", values);
        }

        return string.Empty;
    }

    private static void AddChaosNavigationLinks(Dictionary<string, MapLinkDto> links, JsonElement navigation)
    {
        AddChaosLinkArray(links, navigation, "links");
        AddChaosLinkArray(links, navigation, "routes");
        AddChaosLinkArray(links, navigation, "knownRoutes");
    }

    private static void AddChaosLinkArray(Dictionary<string, MapLinkDto> links, JsonElement navigation, string propertyName)
    {
        if (!navigation.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
            return;

        foreach (var entry in array.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
                continue;

            var source = GetString(entry, "sourceAbodeId", "fromAbodeId", "sourceId", "from");
            var target = GetString(entry, "targetAbodeId", "toAbodeId", "targetId", "to");
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
                continue;

            AddChaosLink(links, source, target, GetString(entry, "label", "routeType", "state"), GetString(entry, "state", "routeState"));
        }
    }

    private static void AddChaosSeaFallbackLinks(Dictionary<string, MapLinkDto> links, IEnumerable<NodeDraft> nodes, string currentAbodeId)
    {
        if (string.IsNullOrWhiteSpace(currentAbodeId))
            return;

        foreach (var node in nodes.OrderBy(static node => node.Label, StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(node.Id, currentAbodeId, StringComparison.OrdinalIgnoreCase))
                continue;

            AddChaosLink(links, currentAbodeId, node.Id, "навигационная нить", "known");
        }
    }

    private static void AddChaosLink(Dictionary<string, MapLinkDto> links, string sourceId, string targetId, string label, string state)
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
            Layer = ChaosSeaLayerId,
            Z = 0
        };
    }

    private static void ApplyChaosSeaLayout(IEnumerable<NodeDraft> nodes, string currentAbodeId)
    {
        foreach (var node in nodes)
        {
            node.Z = 0;
            node.HasCoordinates = true;
            if (!string.IsNullOrWhiteSpace(currentAbodeId) &&
                string.Equals(node.Id, currentAbodeId, StringComparison.OrdinalIgnoreCase))
            {
                node.X = 0;
                node.Y = 0;
                AddDetail(node, "Координаты", "центр навигационного созвездия");
                continue;
            }

            var identityHash = StableHash($"{node.Id}|{node.Domain}");
            var angle = identityHash / (double)uint.MaxValue * Math.Tau;
            var ring = 6.8 + (StableHash(node.Domain) % 4) * 1.35;
            var jitter = (identityHash % 1000) / 1000.0 * 1.1;
            var radius = ring + jitter;
            node.X = Math.Round(Math.Cos(angle) * radius, 2);
            node.Y = Math.Round(Math.Sin(angle) * radius, 2);
            AddDetail(node, "Координаты", $"созвездие: {node.X:0.#}, {node.Y:0.#}");
        }
    }

    private static uint StableHash(string value)
    {
        unchecked
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;
            var hash = offset;
            foreach (var ch in value ?? string.Empty)
                hash = (hash ^ char.ToUpperInvariant(ch)) * prime;
            return hash;
        }
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

            AddLocationNode(nodes, item, ResolveLocationId(item, $"location_{nodes.Count + 1}"), isCurrent: false, isPlaceholder: false);
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

            AddLocationNode(nodes, entry, targetId, isCurrent: false, isPlaceholder: true);
            AddLink(links, sourceNodeId, targetId, GetString(entry, "direction", "label"), GetString(entry, "linkState", "state"));
        }
    }

    private static void AddLocationNode(Dictionary<string, NodeDraft> nodes, JsonElement location, string id, bool isCurrent, bool isPlaceholder = false)
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
        draft.IsPlaceholder = draft.IsPlaceholder || isPlaceholder;
        if (!isPlaceholder)
            draft.IsPlaceholder = false;

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

    private static void FinalizePlaceholderDetails(IEnumerable<NodeDraft> nodes)
    {
        foreach (var node in nodes.Where(static node => node.IsPlaceholder))
            AddDetail(node, "Состояние", "известный выход; подробная локация ещё не открыта");
    }

    private static void AttachLocationImages(FileSystemManager fs, IEnumerable<NodeDraft> nodes)
    {
        var locationImagesDir = fs.ResolvePath("images/locations");
        if (!Directory.Exists(locationImagesDir))
            return;

        var candidates = Directory
            .EnumerateFiles(locationImagesDir, "*.*", SearchOption.TopDirectoryOnly)
            .Where(IsSupportedImageFile)
            .ToList();
        if (candidates.Count == 0)
            return;

        foreach (var node in nodes.Where(static node => !node.IsPlaceholder))
        {
            var imagePath = FindLatestImageCandidate(candidates, node.Id, node.Label);
            if (string.IsNullOrWhiteSpace(imagePath))
                continue;

            var relativePath = NormalizeRelativePath(Path.GetRelativePath(fs.GameSessionPath, imagePath));
            var mediaId = LocalMediaService.CreateMediaIdForRelativePath(relativePath);
            node.ImageUrl = "/api/media/" + Uri.EscapeDataString(mediaId);
            node.ImageAltText = $"Изображение локации «{(string.IsNullOrWhiteSpace(node.Label) ? node.Id : node.Label)}»";
        }
    }

    private static string? FindLatestImageCandidate(IEnumerable<string> candidates, params string[] keys)
    {
        var safeKeys = keys
            .Select(SanitizeImageFileKey)
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (safeKeys.Length == 0)
            return null;

        return candidates
            .Where(path =>
            {
                var stem = Path.GetFileNameWithoutExtension(path);
                return safeKeys.Any(key =>
                    stem.Equals(key, StringComparison.OrdinalIgnoreCase) ||
                    stem.StartsWith(key + ImageVersionSeparator, StringComparison.OrdinalIgnoreCase));
            })
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static string SanitizeImageFileKey(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        if (sanitized.Length > 80)
            sanitized = sanitized[..80];
        sanitized = sanitized.Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(sanitized) ? "entity" : sanitized;
    }

    private static bool IsSupportedImageFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp";
    }

    private static string NormalizeRelativePath(string relativePath) =>
        relativePath.Trim().Replace('\\', '/').TrimStart('/');

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
        public string GuardianId { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string ParentHallId { get; set; } = string.Empty;
        public string OwnerFactionId { get; set; } = string.Empty;
        public string OwnerFactionName { get; set; } = string.Empty;
        public bool IsPlaceholder { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string ImageAltText { get; set; } = string.Empty;
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
            Details = Details.ToList(),
            IsPlaceholder = IsPlaceholder,
            ImageUrl = ImageUrl,
            ImageAltText = ImageAltText
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

        return $$"""
        <!doctype html>
        <html lang="ru">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>{{title}}</title>
          <style>
            :root {
              color-scheme: dark;
              --bg: #101410;
              --panel: rgba(32, 39, 30, .96);
              --line: rgba(205, 168, 90, .45);
              --text: #f4ead0;
              --muted: #c6b78e;
              --accent: #f7d991;
            }
            * { box-sizing: border-box; }
            body { margin: 0; background: #101410; color: var(--text); font: 16px Georgia, "Times New Roman", serif; }
            main {
              min-height: 100vh;
              padding: clamp(1rem, 3vw, 2.5rem);
              background:
                radial-gradient(circle at 20% 12%, rgba(207, 166, 83, .24), transparent 24rem),
                radial-gradient(circle at 80% 4%, rgba(91, 32, 24, .18), transparent 22rem),
                linear-gradient(135deg, #111710, #1b1710 62%, #0d1110);
            }
            .map-shell {
              border: 1px solid var(--line);
              border-radius: 1.35rem;
              padding: clamp(1rem, 2.4vw, 1.8rem);
              background: linear-gradient(145deg, rgba(32, 39, 30, .96), rgba(19, 21, 17, .94));
              box-shadow: 0 1.4rem 4rem rgba(0, 0, 0, .36), inset 0 0 0 1px rgba(255, 233, 172, .07);
            }
            button, select {
              border: 1px solid rgba(205, 168, 90, .42);
              border-radius: 999px;
              background: #1b241d;
              color: var(--text);
              padding: .48rem .72rem;
              box-shadow: inset 0 0 1rem rgba(0, 0, 0, .16);
            }
            button:hover, select:focus { border-color: rgba(247, 217, 145, .82); }
            .secondary { color: var(--text); }
            .map-canvas { height: min(70vh, 48rem); }
          </style>
          <style>
            {{LocalMapViewerAssets.StyleSheet}}
          </style>
        </head>
        <body>
          <main>
            <div id="map-viewer-root" class="map-shell" data-map-json="{{encodedJson}}"></div>
          </main>
          <script>
            {{LocalMapViewerAssets.Script}}
          </script>
          <script>
            BookOfEternityMapViewer.mountStandalone(document.getElementById('map-viewer-root'));
          </script>
        </body>
        </html>
        """;
    }
}

public sealed record LocalMapViewerLaunchResult(bool Opened, string RelativePath, string FullPath, string Error);

public static class LocalMapViewerLauncher
{
    public const string DisableBrowserOpenEnvironmentVariable = "BOOK_OF_ETERNITY_DISABLE_BROWSER_OPEN";
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
            if (openFile is null && IsBrowserOpenDisabled())
            {
                return new LocalMapViewerLaunchResult(
                    false,
                    ViewerPath,
                    fullPath,
                    "Browser launch disabled for this process.");
            }

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

    private static bool IsBrowserOpenDisabled()
    {
        var configured = Environment.GetEnvironmentVariable(DisableBrowserOpenEnvironmentVariable);
        if (string.Equals(configured, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(configured, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(configured, "yes", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return AppDomain.CurrentDomain.GetAssemblies().Any(static assembly =>
            assembly.GetName().Name?.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) == true);
    }
}
