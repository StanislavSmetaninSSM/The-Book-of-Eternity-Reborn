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
