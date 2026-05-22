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

        Assert.Equal("Mortal World", map.Realm);
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
        Assert.Contains(current.Details, static item => item.Key == "Биом" && item.Value.Contains("ash_coast", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(current.Details, static item => item.Key == "Последние события" && item.Value.Contains("осады", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(current.Details, static item => item.Key == "Выходы" && item.Value.Contains("винтовая лестница", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(map.Nodes, static node => node.Id == "loc_tower_base");
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
        Assert.Contains("<svg", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-map-json", html, StringComparison.Ordinal);
        Assert.Contains("Старая площадь", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocalMapViewerRenderer_UsesDarkFantasyAtlasVisualSystem()
    {
        await SeedMortalMapAsync();
        var map = await LocalMapViewService.BuildMortalWorldMapAsync(_fs);

        var html = LocalMapViewerRenderer.BuildStandaloneHtml(map);

        Assert.Contains("--atlas-parchment", html, StringComparison.Ordinal);
        Assert.Contains("atlas-texture", html, StringComparison.Ordinal);
        Assert.Contains("map-legend", html, StringComparison.Ordinal);
        Assert.Contains("Легенда карты", html, StringComparison.Ordinal);
        Assert.Contains("Текущая точка", html, StringComparison.Ordinal);
        Assert.Contains("Влияние фракций", html, StringComparison.Ordinal);
        Assert.Contains("Нет точек на выбранном уровне", html, StringComparison.Ordinal);
        Assert.Contains("map-node--selected", html, StringComparison.Ordinal);
        Assert.Contains("data-layer-state", html, StringComparison.Ordinal);
        Assert.Contains("Выберите точку на карте", html, StringComparison.Ordinal);
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

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}
