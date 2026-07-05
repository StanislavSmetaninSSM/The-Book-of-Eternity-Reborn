using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class LocalMapViewerServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public LocalMapViewerServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-map-viewer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task BuildMortalWorldMapAsync_ProjectsCurrentLocationNodesLinksAndZLevels()
    {
        await SeedMortalMapAsync();

        var map = await LocalMapViewService.BuildMortalWorldMapAsync(_fs);

        Assert.Equal("Смертный мир", map.Realm);
        Assert.Equal("loc_square", map.CurrentNodeId);
        Assert.Contains(map.ZLevels, static level => level.Z == 0 && level.Label.Contains("зем", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(map.ZLevels, static level => level.Z == -1);
        Assert.Contains(map.Layers, static layer => layer.Id == "world" && layer.IsDefault);
        Assert.Contains(map.Nodes, static node => node.Id == "loc_square" && node.IsCurrent && node.X == 10 && node.Y == 20 && node.Z == 0);
        Assert.Contains(map.Nodes, static node => node.Id == "loc_catacombs" && node.Z == -1);
        Assert.Contains(map.Links, static link => link.SourceNodeId == "loc_square" && link.TargetNodeId == "loc_catacombs");
    }

    [Fact]
    public async Task BuildMortalWorldMapAsync_ProjectsWrappedCurrentLocationAndLocationCardMetadata()
    {
        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", """
        {
          "currentLocationData": {
            "locationId": "loc_tower_roof",
            "name": "Крыша башни",
            "locationType": "tower",
            "description": "Ветер режет лицо, а внизу мерцает город.",
            "knownState": "visited",
            "discovered": true,
            "biome": "ash_coast",
            "lastEventsDescription": "На зубцах остались следы недавней осады.",
            "coordinates": { "x": 4, "y": 9, "z": 2 },
            "adjacencyMap": [
              {
                "targetLocationId": "loc_tower_base",
                "targetLocationName": "Нижний зал башни",
                "direction": "винтовая лестница",
                "linkState": "stable",
                "targetCoordinates": { "x": 4, "y": 9, "z": 0 }
              }
            ]
          }
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_map.json", """
        {
          "locations": [
            {
              "locationId": "loc_tower_base",
              "locationName": "Нижний зал башни",
              "locationType": "hall",
              "description": "Здесь пахнет копотью и старым железом.",
              "knownState": "known",
              "coordinates": { "x": 4, "y": 9, "z": 0 }
            }
          ]
        }
        """);

        var map = await LocalMapViewService.BuildMortalWorldMapAsync(_fs);

        var current = Assert.Single(map.Nodes, node => node.Id == "loc_tower_roof");
        Assert.True(current.IsCurrent);
        Assert.Equal(2, current.Z);
        Assert.Contains(map.ZLevels, static level => level.Z == 2 && level.Label.Contains("+2", StringComparison.Ordinal));
        Assert.Contains(current.Details, static item => item.Key == "Известность" && item.Value.Contains("visited", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(current.Details, static item => item.Key == "Открыта" && item.Value.Contains("да", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(current.Details, static item => item.Key == "Биом" && item.Value.Contains("ash coast", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(current.Details, static item => item.Key == "Последние события" && item.Value.Contains("осады", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(current.Details, static item => item.Key == "Выходы" && item.Value.Contains("винтовая лестница", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(map.Nodes, static node => node.Id == "loc_tower_base");
    }

    [Fact]
    public async Task BuildMortalWorldMapAsync_DistinguishesCreatedLocationsFromExitPlaceholdersAndAttachesImages()
    {
        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", """
        {
          "locationId": "loc_parlor",
          "name": "Гостиная виконта",
          "description": "Комната с тяжёлыми шторами и холодным камином.",
          "coordinates": { "x": 0, "y": 0, "z": 0 },
          "adjacencyMap": [
            {
              "targetLocationId": "loc_gallery",
              "targetLocationName": "Галерея портретов",
              "direction": "на север",
              "targetCoordinates": { "x": 3, "y": 0, "z": 0 }
            },
            {
              "targetLocationId": "loc_servant_stairs",
              "targetLocationName": "Служебная лестница",
              "direction": "за ширмой",
              "targetCoordinates": { "x": -2, "y": -1, "z": 0 }
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_map.json", """
        {
          "locations": [
            {
              "locationId": "loc_gallery",
              "locationName": "Галерея портретов",
              "description": "Пыльные портреты следят за каждым шагом.",
              "knownState": "visited",
              "coordinates": { "x": 3, "y": 0, "z": 0 }
            }
          ]
        }
        """);
        var imageDir = _fs.ResolvePath("images/locations");
        Directory.CreateDirectory(imageDir);
        await File.WriteAllBytesAsync(Path.Combine(imageDir, "loc_gallery.png"), TinyPng);

        var map = await LocalMapViewService.BuildMortalWorldMapAsync(_fs);

        var gallery = Assert.Single(map.Nodes, node => node.Id == "loc_gallery");
        Assert.False(gallery.IsPlaceholder);
        Assert.Equal("Пыльные портреты следят за каждым шагом.", Assert.Single(gallery.Details, item => item.Key == "Описание").Value);
        Assert.StartsWith("/api/media/", gallery.ImageUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("images/locations", gallery.ImageUrl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Галерея портретов", gallery.ImageAltText, StringComparison.Ordinal);

        var stairs = Assert.Single(map.Nodes, node => node.Id == "loc_servant_stairs");
        Assert.True(stairs.IsPlaceholder);
        Assert.Contains(stairs.Details, static item =>
            item.Key == "Состояние" &&
            item.Value.Contains("известный выход", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(stairs.ImageUrl);
    }

    [Fact]
    public async Task BuildMortalWorldMapAsync_UsesSchematicFallbackForMissingCoordinates()
    {
        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", """
        {
          "locationId": "loc_fog_gate",
          "name": "Туманные ворота",
          "locationType": "gate",
          "adjacencyMap": [
            {
              "targetLocationId": "loc_nameless_field",
              "targetLocationName": "Безымянное поле",
              "direction": "за воротами"
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_map.json", """
        {
          "locationUpdates": [
            {
              "locationId": "loc_nameless_field",
              "locationName": "Безымянное поле",
              "description": "Координаты этого места ещё не закреплены."
            }
          ]
        }
        """);

        var map = await LocalMapViewService.BuildMortalWorldMapAsync(_fs);

        Assert.Equal(2, map.Nodes.Count);
        Assert.All(map.Nodes, node =>
        {
            Assert.True(Math.Abs(node.X) > 0.01 || Math.Abs(node.Y) > 0.01);
            Assert.Contains(node.Details, static item => item.Key == "Координаты" && item.Value.Contains("схемат", StringComparison.OrdinalIgnoreCase));
        });
        Assert.Contains(map.Links, static link => link.SourceNodeId == "loc_fog_gate" && link.TargetNodeId == "loc_nameless_field");
    }

    [Fact]
    public async Task BuildMortalWorldMapAsync_ProjectsPoliticalControlRegionsAndContestedLocations()
    {
        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", """
        {
          "locationId": "loc_gate",
          "name": "Ворота Серой Короны",
          "coordinates": { "x": 0, "y": 0, "z": 0 },
          "factionControl": [
            { "factionId": "f_crown", "factionName": "Серая Корона", "controlType": "Military", "controlLevel": 76 }
          ],
          "adjacencyMap": [
            {
              "targetLocationId": "loc_barracks",
              "targetLocationName": "Казармы Серой Короны",
              "targetCoordinates": { "x": 4, "y": 0, "z": 0 }
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_map.json", """
        {
          "locations": [
            {
              "locationId": "loc_barracks",
              "locationName": "Казармы Серой Короны",
              "coordinates": { "x": 4, "y": 0, "z": 0 },
              "factionControl": [
                { "factionId": "f_crown", "factionName": "Серая Корона", "controlType": "Military", "controlLevel": 82 }
              ]
            },
            {
              "locationId": "loc_market",
              "locationName": "Спорный рынок",
              "coordinates": { "x": 2, "y": 3, "z": 0 },
              "factionControl": [
                { "factionId": "f_crown", "factionName": "Серая Корона", "controlType": "Economic", "controlLevel": 48 },
                { "factionId": "f_syndicate", "factionName": "Синдикат Тени", "controlType": "Covert", "controlLevel": 45 }
              ]
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/factions/faction_core.json", """
        {
          "factions": [
            {
              "id": "f_crown",
              "name": "Серая Корона",
              "controlledTerritories": [
                { "locationId": "loc_gate", "locationName": "Ворота Серой Короны" },
                { "locationId": "loc_barracks", "locationName": "Казармы Серой Короны" }
              ]
            }
          ]
        }
        """);

        var map = await LocalMapViewService.BuildMortalWorldMapAsync(_fs);

        var gate = Assert.Single(map.Nodes, node => node.Id == "loc_gate");
        Assert.Equal("f_crown", gate.OwnerFactionId);
        Assert.Equal("Серая Корона", gate.OwnerFactionName);
        Assert.Equal(76, gate.Influence["f_crown"]);
        Assert.Contains(gate.Details, static item => item.Key == "Контроль фракций" && item.Value.Contains("Military 76", StringComparison.Ordinal));
        Assert.Contains(map.Regions, static region =>
            region.OwnerFactionId == "f_crown" &&
            region.NodeIds.Contains("loc_gate", StringComparer.OrdinalIgnoreCase) &&
            region.NodeIds.Contains("loc_barracks", StringComparer.OrdinalIgnoreCase));

        var market = Assert.Single(map.Nodes, node => node.Id == "loc_market");
        Assert.Contains(market.Details, static item => item.Key == "Статус контроля" && item.Value.Contains("спорная", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(48, market.Influence["f_crown"]);
        Assert.Equal(45, market.Influence["f_syndicate"]);
    }

    [Fact]
    public async Task BuildMortalWorldMapAsync_KeepsNoFactionLocationsNeutral()
    {
        await SeedMortalMapAsync();

        var map = await LocalMapViewService.BuildMortalWorldMapAsync(_fs);

        Assert.Empty(map.Regions);
        Assert.All(map.Nodes, node =>
        {
            Assert.True(string.IsNullOrWhiteSpace(node.OwnerFactionId));
            Assert.Empty(node.Influence);
            Assert.DoesNotContain(node.Details, static item => item.Key == "Контроль фракций");
        });
    }

    [Fact]
    public async Task BuildChaosSeaMapAsync_ProjectsDiscoveredCurrentAndActiveGuardianAbodes()
    {
        await SeedChaosSeaMapAsync();

        var map = await LocalMapViewService.BuildChaosSeaMapAsync(_fs);

        Assert.Equal("Море Хаоса", map.Realm);
        Assert.Equal("abode_azalia", map.CurrentNodeId);
        Assert.Contains(map.Layers, static layer => layer.Id == "chaos_sea" && layer.IsDefault);
        Assert.Contains(map.ZLevels, static level => level.Z == 0 && level.Label.Contains("созвезд", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(map.Nodes, static node => node.Id == "abode_azalia" && node.IsCurrent && node.Label == "Сад Ночных Роз");
        Assert.Contains(map.Nodes, static node => node.Id == "abode_lucian" && node.Label == "Зал Серебряного Клинка");
        Assert.DoesNotContain(map.Nodes, static node => node.Id == "abode_locked");
        Assert.Contains(map.Links, static link => link.SourceNodeId == "abode_azalia" && link.TargetNodeId == "abode_lucian");

        var current = Assert.Single(map.Nodes, node => node.Id == "abode_azalia");
        Assert.Contains(current.Details, static item => item.Key == "Хранитель" && item.Value.Contains("Азалия", StringComparison.Ordinal));
        Assert.Contains(current.Details, static item => item.Key == "Активный Хранитель" && item.Value == "да");
        Assert.Contains(current.Details, static item => item.Key == "Домен" && item.Value.Contains("Память", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(current.Details, static item => item.Key == "Репутация" && item.Value.Contains("14", StringComparison.Ordinal));
        Assert.Contains(current.Details, static item => item.Key == "Сила Обители" && item.Value.Contains("44", StringComparison.Ordinal));
        Assert.Contains(current.Details, static item => item.Key == "Резиденты" && item.Value.Contains("2", StringComparison.Ordinal));
        Assert.Contains(current.Details, static item => item.Key == "Проекты" && item.Value.Contains("1", StringComparison.Ordinal));
        Assert.Contains(current.Details, static item => item.Key == "Действия" && item.Value.Contains("подношение", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BuildChaosSeaMapAsync_UsesStableLayoutByAbodeIdentity()
    {
        await SeedChaosSeaMapAsync();
        var first = await LocalMapViewService.BuildChaosSeaMapAsync(_fs);
        var firstLucian = Assert.Single(first.Nodes, node => node.Id == "abode_lucian");

        await SeedChaosSeaMapAsync(reverseGuardians: true);
        var second = await LocalMapViewService.BuildChaosSeaMapAsync(_fs);
        var secondLucian = Assert.Single(second.Nodes, node => node.Id == "abode_lucian");

        Assert.Equal(firstLucian.X, secondLucian.X);
        Assert.Equal(firstLucian.Y, secondLucian.Y);
        Assert.Equal(firstLucian.Z, secondLucian.Z);
    }

    [Fact]
    public async Task BuildShiningAbodeMapAsync_ProjectsHallsFactionsAndPoliticalCards()
    {
        await SeedShiningAbodeMapAsync();

        var map = await LocalMapViewService.BuildShiningAbodeMapAsync(_fs);

        Assert.Equal("Сияющая Обитель", map.Realm);
        Assert.Equal("hall_dawn", map.CurrentNodeId);
        Assert.Contains(map.Layers, static layer => layer.Id == "shining_abode" && layer.IsDefault);
        Assert.Contains(map.ZLevels, static level => level.Z == 0 && level.Label.Contains("мандал", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(map.Nodes, static node => node.Id == "hall_dawn" && node.IsCurrent && node.Label == "Зал Рассвета");
        Assert.Contains(map.Nodes, static node => node.Id == "faction_lanterns" && node.Label == "Фонари Рассвета");
        Assert.Contains(map.Links, static link => link.SourceNodeId == "hall_dawn" && link.TargetNodeId == "faction_lanterns");
        Assert.Contains(map.Regions, static region =>
            region.OwnerFactionId == "faction_lanterns" &&
            region.NodeIds.Contains("hall_dawn", StringComparer.OrdinalIgnoreCase) &&
            region.NodeIds.Contains("faction_lanterns", StringComparer.OrdinalIgnoreCase));

        var hall = Assert.Single(map.Nodes, node => node.Id == "hall_dawn");
        Assert.Equal("faction_lanterns", hall.OwnerFactionId);
        Assert.Equal(64, hall.Influence["faction_lanterns"]);
        Assert.Contains(hall.Details, static item => item.Key == "Контроль фракций" && item.Value.Contains("Сила фракции 64", StringComparison.Ordinal));
        Assert.Contains(hall.Details, static item => item.Key == "Описание" && item.Value.Contains("солнечные арки", StringComparison.OrdinalIgnoreCase));

        var faction = Assert.Single(map.Nodes, node => node.Id == "faction_lanterns");
        Assert.Contains(faction.Details, static item => item.Key == "Зал" && item.Value.Contains("Зал Рассвета", StringComparison.Ordinal));
        Assert.Contains(faction.Details, static item => item.Key == "Лидерство" && item.Value.Contains("устойчив", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(faction.Details, static item => item.Key == "Резиденты" && item.Value.Contains("Светозарный судья", StringComparison.Ordinal));
        Assert.Contains(faction.Details, static item => item.Key == "Проекты" && item.Value.Contains("Световой мост", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BuildShiningAbodeMapAsync_UsesFactionTerritorialInfluenceZones()
    {
        await SeedShiningAbodeMapAsync();

        var json = await _fs.ReadFileAsync("game_state/meta/shining_abode_state.json");
        using var doc = System.Text.Json.JsonDocument.Parse(json!);
        var root = System.Text.Json.Nodes.JsonNode.Parse(doc.RootElement.GetRawText())!.AsObject();
        var faction = root["factions"]!.AsArray().OfType<System.Text.Json.Nodes.JsonObject>()
            .Single(item => item["factionId"]?.GetValue<string>() == "faction_lanterns");
        faction[ShiningAbodeState.FactionInfluenceProperty] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject
        {
            ["zoneId"] = "zone_archive",
            ["scopeType"] = "hall",
            ["scopeId"] = "hall_archive",
            ["displayName"] = "Архивная дуга",
            ["controlLevel"] = 81,
            ["influenceValue"] = 81,
            ["publicStatus"] = "dominant",
            ["updatedAtTurn"] = 99
        });
        await _fs.WriteFileAtomicAsync("game_state/meta/shining_abode_state.json", root.ToJsonString());

        var map = await LocalMapViewService.BuildShiningAbodeMapAsync(_fs);

        var archiveHall = Assert.Single(map.Nodes, node => node.Id == "hall_archive");
        Assert.Equal("faction_lanterns", archiveHall.OwnerFactionId);
        Assert.Equal(81, archiveHall.Influence["faction_lanterns"]);
        Assert.Contains(archiveHall.Details, static item =>
            item.Key == "Контроль фракций" &&
            item.Value.Contains("Архивная дуга", StringComparison.Ordinal));
        Assert.Contains(map.Regions, static region =>
            region.OwnerFactionId == "faction_lanterns" &&
            region.NodeIds.Contains("hall_archive", StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BuildShiningAbodeMapAsync_CreatesFallbackHallForUnplacedFactions()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/shining_abode_state.json", """
        {
          "availability": "active",
          "factions": [
            {
              "factionId": "faction_exiles",
              "name": "Изгнанные лампы",
              "factionStrength": 33
            }
          ]
        }
        """);

        var map = await LocalMapViewService.BuildShiningAbodeMapAsync(_fs);

        Assert.Equal("Сияющая Обитель", map.Realm);
        Assert.Contains(map.Nodes, static node => node.Id == "hall_unassigned" && node.Label.Contains("Без закреплённого зала", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(map.Nodes, static node => node.Id == "faction_exiles" && node.OwnerFactionId == "faction_exiles");
        Assert.Contains(map.Links, static link => link.SourceNodeId == "hall_unassigned" && link.TargetNodeId == "faction_exiles");
    }

    [Fact]
    public async Task LocalMapViewerLauncher_WritesHtmlAndReturnsFallbackWhenOpenFails()
    {
        await SeedMortalMapAsync();
        var map = await LocalMapViewService.BuildMortalWorldMapAsync(_fs);

        var result = await LocalMapViewerLauncher.WriteAndOpenAsync(
            _fs,
            map,
            _ => throw new InvalidOperationException("browser disabled"));

        Assert.False(result.Opened);
        Assert.Equal("output/map_viewer.html", result.RelativePath);
        Assert.True(File.Exists(_fs.ResolvePath(result.RelativePath)));
        Assert.Contains("browser disabled", result.Error, StringComparison.OrdinalIgnoreCase);
        var html = await File.ReadAllTextAsync(_fs.ResolvePath(result.RelativePath));
        // The unified standalone HTML embeds the React+MapAtlas bundle and
        // the MapViewDto JSON. The bundle auto-mounts MapAtlas on load.
        Assert.Contains(LocalMapViewerAssets.Bundle, html, StringComparison.Ordinal);
        Assert.Contains(LocalMapViewerAssets.Global, html, StringComparison.Ordinal);
        Assert.Contains("id=" + "\"map-viewer-data\"", html, StringComparison.Ordinal);
        Assert.Contains("Старая площадь", html, StringComparison.Ordinal);
        // The deleted vanilla viewer must not be referenced.
        Assert.DoesNotContain("BookOfEternityMapViewer.mountStandalone", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocalMapViewerLauncher_WritesHtmlWithoutOpeningBrowserUnderTests()
    {
        await SeedMortalMapAsync();
        var map = await LocalMapViewService.BuildMortalWorldMapAsync(_fs);

        var result = await LocalMapViewerLauncher.WriteAndOpenAsync(_fs, map);

        Assert.False(result.Opened);
        Assert.Equal("output/map_viewer.html", result.RelativePath);
        Assert.True(File.Exists(_fs.ResolvePath(result.RelativePath)));
        Assert.Contains("disabled", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LocalMapViewerRenderer_EmbedsUnifiedAtlasBundle()
    {
        await SeedMortalMapAsync();
        var map = await LocalMapViewService.BuildMortalWorldMapAsync(_fs);

        var html = LocalMapViewerRenderer.BuildStandaloneHtml(map);

        // The standalone HTML embeds the unified bundle (React + MapAtlas +
        // inlined CSS) and the MapViewDto JSON. MapAtlas renders client-side,
        // so the visual chrome comes from the bundle, not from this HTML.
        Assert.Contains(LocalMapViewerAssets.Bundle, html, StringComparison.Ordinal);
        Assert.Contains(LocalMapViewerAssets.Global, html, StringComparison.Ordinal);
        Assert.Contains("id=" + "\"map-viewer-data\"", html, StringComparison.Ordinal);
        // Player-facing content flows through the embedded DTO JSON.
        Assert.Contains("Старая площадь", html, StringComparison.Ordinal);
        Assert.Contains("schemaVersion", html, StringComparison.Ordinal);
        // The standalone shell is a minimal HTML document; the old vanilla
        // viewer string invariants must not be present.
        Assert.DoesNotContain("BookOfEternityMapViewer", html, StringComparison.Ordinal);
        Assert.DoesNotContain("function renderMapBlock", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocalMapViewerRenderer_EmbedsStandaloneMountPoint()
    {
        await SeedMortalMapAsync();
        var map = await LocalMapViewService.BuildMortalWorldMapAsync(_fs);

        var html = LocalMapViewerRenderer.BuildStandaloneHtml(map);

        // The bundle is the single renderer; the standalone HTML provides a
        // root element + JSON data tag that the IIFE auto-mounts into.
        Assert.Contains("id=" + "\"map-viewer-root\"", html, StringComparison.Ordinal);
        Assert.Contains("id=" + "\"map-viewer-data\"", html, StringComparison.Ordinal);
        Assert.Contains(LocalMapViewerAssets.Bundle, html, StringComparison.Ordinal);
        // The bundle exposes the unified mount global; the deleted constants
        // (StyleSheet/Script) and vanilla mount call are gone.
        Assert.DoesNotContain("BookOfEternityMapViewer.mountStandalone", html, StringComparison.Ordinal);
    }

    private async Task SeedMortalMapAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", """
        {
          "locationId": "loc_square",
          "name": "Старая площадь",
          "locationType": "city_square",
          "description": "Площадь с высохшим фонтаном.",
          "coordinates": { "x": 10, "y": 20, "z": 0 },
          "adjacencyMap": [
            {
              "targetLocationId": "loc_catacombs",
              "name": "Катакомбы",
              "direction": "вниз",
              "linkState": "dangerous",
              "targetCoordinates": { "x": 10, "y": 19, "z": -1 }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/world_map.json", """
        {
          "newLocations": [
            {
              "locationId": "loc_catacombs",
              "locationName": "Катакомбы",
              "locationType": "dungeon",
              "description": "Слепые коридоры под площадью.",
              "coordinates": { "x": 10, "y": 19, "z": -1 }
            }
          ]
        }
        """);
    }

    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");

    private async Task SeedChaosSeaMapAsync(bool reverseGuardians = false)
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", reverseGuardians ? ChaosSeaMapJsonReversed : ChaosSeaMapJson);
    }

    private async Task SeedShiningAbodeMapAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/shining_abode_state.json", """
        {
          "availability": "active",
          "currentHallId": "hall_dawn",
          "halls": [
            {
              "hallId": "hall_dawn",
              "hallName": "Зал Рассвета",
              "description": "Здесь солнечные арки держат договоры фракций."
            },
            {
              "hallId": "hall_archive",
              "hallName": "Архив Звёздной Пыли"
            }
          ],
          "factions": [
            {
              "factionId": "faction_lanterns",
              "hallId": "hall_dawn",
              "factionStrength": 64,
              "charter": { "factionName": "Фонари Рассвета" },
              "leadership": { "headActorType": "resident", "headActorId": "resident_judge", "leadershipState": "secure" },
              "projects": [
                { "projectId": "project_bridge", "displayName": "Световой мост", "status": "active" }
              ]
            },
            {
              "factionId": "faction_dust",
              "hallId": "hall_archive",
              "factionStrength": 41,
              "name": "Пыльные Архивариусы"
            }
          ],
          "residents": [
            {
              "residentId": "resident_judge",
              "displayName": "Светозарный судья",
              "shiningFactionId": "faction_lanterns",
              "hallId": "hall_dawn",
              "politicalStatus": "elder"
            }
          ]
        }
        """);
    }

    private const string ChaosSeaMapJson = """
    {
      "activeGuardian": {
        "guardianId": "guardian_azalia",
        "guardianName": "Азалия"
      },
      "chaosSeaNavigation": {
        "currentAbodeId": "abode_azalia",
        "discoveredAbodes": [
          { "abodeId": "abode_azalia", "name": "Сад Ночных Роз", "guardianId": "guardian_azalia" },
          { "abodeId": "abode_lucian", "name": "Зал Серебряного Клинка", "guardianId": "guardian_lucian" }
        ]
      },
      "guardians": [
        {
          "guardianId": "guardian_azalia",
          "canonicalName": "Азалия",
          "domain": "Память и запретные клятвы",
          "relationshipData": { "currentReputation": 14 },
          "abode": {
            "abodeId": "abode_azalia",
            "name": "Сад Ночных Роз",
            "isDiscovered": true,
            "residents": [
              { "residentId": "resident_1" },
              { "residentId": "resident_2" }
            ],
            "availableActions": ["подношение", "торг"]
          },
          "abodePower": { "currentPower": 44, "maxPower": 100 },
          "projects": [
            { "projectId": "project_roses", "status": "active" }
          ]
        },
        {
          "guardianId": "guardian_lucian",
          "canonicalName": "Люциан",
          "domain": "Клятвы и клинок",
          "relationshipData": { "currentReputation": -3 },
          "abode": {
            "abodeId": "abode_lucian",
            "name": "Зал Серебряного Клинка",
            "isDiscovered": true
          },
          "abodePower": { "currentPower": 31, "maxPower": 90 }
        },
        {
          "guardianId": "guardian_locked",
          "canonicalName": "Безымянный",
          "domain": "Запертая звезда",
          "abode": {
            "abodeId": "abode_locked",
            "name": "Запертая Обитель",
            "isDiscovered": false
          }
        }
      ]
    }
    """;

    private const string ChaosSeaMapJsonReversed = """
    {
      "activeGuardian": {
        "guardianId": "guardian_azalia",
        "guardianName": "Азалия"
      },
      "chaosSeaNavigation": {
        "currentAbodeId": "abode_azalia",
        "discoveredAbodes": [
          { "abodeId": "abode_azalia", "name": "Сад Ночных Роз", "guardianId": "guardian_azalia" },
          { "abodeId": "abode_lucian", "name": "Зал Серебряного Клинка", "guardianId": "guardian_lucian" }
        ]
      },
      "guardians": [
        {
          "guardianId": "guardian_locked",
          "canonicalName": "Безымянный",
          "domain": "Запертая звезда",
          "abode": {
            "abodeId": "abode_locked",
            "name": "Запертая Обитель",
            "isDiscovered": false
          }
        },
        {
          "guardianId": "guardian_lucian",
          "canonicalName": "Люциан",
          "domain": "Клятвы и клинок",
          "relationshipData": { "currentReputation": -3 },
          "abode": {
            "abodeId": "abode_lucian",
            "name": "Зал Серебряного Клинка",
            "isDiscovered": true
          },
          "abodePower": { "currentPower": 31, "maxPower": 90 }
        },
        {
          "guardianId": "guardian_azalia",
          "canonicalName": "Азалия",
          "domain": "Память и запретные клятвы",
          "relationshipData": { "currentReputation": 14 },
          "abode": {
            "abodeId": "abode_azalia",
            "name": "Сад Ночных Роз",
            "isDiscovered": true,
            "residents": [
              { "residentId": "resident_1" },
              { "residentId": "resident_2" }
            ],
            "availableActions": ["подношение", "торг"]
          },
          "abodePower": { "currentPower": 44, "maxPower": 100 },
          "projects": [
            { "projectId": "project_roses", "status": "active" }
          ]
        }
      ]
    }
    """;

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}
