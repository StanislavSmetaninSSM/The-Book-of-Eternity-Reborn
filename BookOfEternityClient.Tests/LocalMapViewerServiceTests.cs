using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json.Nodes;
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
    public async Task BuildMortalWorldMapAsync_ProjectsReceiptBearingDirectedTopology()
    {
        var accepted = await SeedMortalMapAsync();

        var map = await LocalMapViewService.BuildMortalWorldMapAsync(_fs);

        Assert.Equal("Смертный мир", map.Realm);
        Assert.Equal(accepted.SourceLocationId, map.CurrentNodeId);
        Assert.Contains(map.ZLevels, static level => level.Z == 0 && level.Label.Contains("зем", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(map.ZLevels, static level => level.Z == -1);
        Assert.Contains(map.Layers, static layer => layer.Id == "world" && layer.IsDefault);
        Assert.Contains(map.Nodes, node => node.Id == accepted.SourceLocationId && node.IsCurrent && node.X == 10 && node.Y == 20 && node.Z == 0);
        Assert.Contains(map.Nodes, node => node.Id == accepted.TargetLocationId && node.Z == -1 && !node.IsPlaceholder);
        var link = Assert.Single(map.Links);
        Assert.Equal(accepted.LinkId, link.Id);
        Assert.Equal(accepted.SourceLocationId, link.SourceNodeId);
        Assert.Equal(accepted.TargetLocationId, link.TargetNodeId);
        Assert.DoesNotContain(map.Links, candidate =>
            candidate.SourceNodeId == accepted.TargetLocationId &&
            candidate.TargetNodeId == accepted.SourceLocationId);
    }

    [Fact]
    public async Task BuildMortalWorldMapAsync_RejectsLegacyWrappers()
    {
        var accepted = await SeedMortalMapAsync();
        await _fs.WriteFileAtomicAsync(
            "game_state/world/world_map.json",
            new JsonObject
            {
                ["worldMapUpdates"] = accepted.Plan.FinalWorldMap.DeepClone()
            }.ToJsonString());
        await _fs.WriteFileAtomicAsync(
            "game_state/world/current_location.json",
            new JsonObject
            {
                ["currentLocationData"] = accepted.Plan.FinalCurrentLocation!.DeepClone()
            }.ToJsonString());

        var map = await LocalMapViewService.BuildMortalWorldMapAsync(_fs);

        Assert.Empty(map.Nodes);
        Assert.Empty(map.Links);
        Assert.Empty(map.CurrentNodeId);
    }

    [Fact]
    public async Task BuildMortalWorldMapAsync_OmitsInvalidNodesAndNeverCreatesAdjacencyPlaceholders()
    {
        var accepted = await SeedMortalMapAsync();
        var mapState = accepted.Plan.FinalWorldMap.DeepClone().AsObject();
        var target = mapState["locations"]!.AsArray().OfType<JsonObject>()
            .Single(location => location["locationId"]!.GetValue<string>() == accepted.TargetLocationId);
        target["coordinates"]!.AsObject().Remove("z");
        var current = accepted.Plan.FinalCurrentLocation!.DeepClone().AsObject();
        current["adjacencyMap"] = new JsonArray(new JsonObject
        {
            ["targetLocationId"] = "loc_unaccepted_placeholder",
            ["targetLocationName"] = "Ложная лестница",
            ["targetCoordinates"] = new JsonObject { ["x"] = 99, ["y"] = 99 }
        });
        await _fs.WriteFileAtomicAsync("game_state/world/world_map.json", mapState.ToJsonString());
        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", current.ToJsonString());
        var imageDir = _fs.ResolvePath("images/locations");
        Directory.CreateDirectory(imageDir);
        await File.WriteAllBytesAsync(Path.Combine(imageDir, accepted.SourceLocationId + ".png"), TinyPng);

        var map = await LocalMapViewService.BuildMortalWorldMapAsync(_fs);

        var source = Assert.Single(map.Nodes);
        Assert.Equal(accepted.SourceLocationId, source.Id);
        Assert.False(source.IsPlaceholder);
        Assert.StartsWith("/api/media/", source.ImageUrl, StringComparison.Ordinal);
        Assert.DoesNotContain(map.Nodes, static node => node.Id == "loc_unaccepted_placeholder");
        Assert.DoesNotContain(map.Nodes, node => node.Id == accepted.TargetLocationId);
        Assert.Empty(map.Links);
    }

    [Fact]
    public async Task BuildMortalWorldMapAsync_RequiresOrdinalCurrentAndEndpointIdentity()
    {
        var accepted = await SeedMortalMapAsync();
        var current = accepted.Plan.FinalCurrentLocation!.DeepClone().AsObject();
        current["locationId"] = accepted.SourceLocationId.ToUpperInvariant();
        var mapState = accepted.Plan.FinalWorldMap.DeepClone().AsObject();
        var link = mapState["links"]!.AsArray().OfType<JsonObject>().Single();
        link["sourceLocationId"] = accepted.SourceLocationId.ToUpperInvariant();
        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", current.ToJsonString());
        await _fs.WriteFileAtomicAsync("game_state/world/world_map.json", mapState.ToJsonString());

        var map = await LocalMapViewService.BuildMortalWorldMapAsync(_fs);

        Assert.Equal(2, map.Nodes.Count);
        Assert.Empty(map.CurrentNodeId);
        Assert.DoesNotContain(map.Nodes, static node => node.IsCurrent);
        Assert.Empty(map.Links);
    }

    [Fact]
    public async Task BuildMortalWorldMapAsync_ProjectsPoliticalControlRegionsAndContestedLocations()
    {
        var accepted = await SeedMortalMapAsync(withFactionControl: true);
        await _fs.WriteFileAtomicAsync("game_state/factions/faction_core.json", """
        {
          "factions": [
            {
              "id": "f_crown",
              "name": "Серая Корона",
              "controlledTerritories": [
                { "locationId": "LOCATION_SOURCE", "locationName": "Старая площадь" },
                { "locationId": "LOCATION_TARGET", "locationName": "Катакомбы" }
              ]
            }
          ]
        }
        """
            .Replace("LOCATION_SOURCE", accepted.SourceLocationId, StringComparison.Ordinal)
            .Replace("LOCATION_TARGET", accepted.TargetLocationId, StringComparison.Ordinal));

        var map = await LocalMapViewService.BuildMortalWorldMapAsync(_fs);

        var gate = Assert.Single(map.Nodes, node => node.Id == accepted.SourceLocationId);
        Assert.Equal("f_crown", gate.OwnerFactionId);
        Assert.Equal("Серая Корона", gate.OwnerFactionName);
        Assert.Equal(76, gate.Influence["f_crown"]);
        Assert.Contains(gate.Details, static item => item.Key == "Контроль фракций" && item.Value.Contains("Military 76", StringComparison.Ordinal));
        Assert.Contains(map.Regions, region =>
            region.OwnerFactionId == "f_crown" &&
            region.NodeIds.Contains(accepted.SourceLocationId, StringComparer.Ordinal) &&
            region.NodeIds.Contains(accepted.TargetLocationId, StringComparer.Ordinal));

        var market = Assert.Single(map.Nodes, node => node.Id == accepted.TargetLocationId);
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
    public void LocalMapViewerAssets_BundleContainsInlinedMapAtlasStyles()
    {
        var bundle = LocalMapViewerAssets.Bundle;

        Assert.Contains("document.createElement('style')", bundle, StringComparison.Ordinal);
        Assert.Contains(".map-atlas", bundle, StringComparison.Ordinal);
        Assert.Contains(".map-vignette", bundle, StringComparison.Ordinal);
        Assert.Contains("pointer-events:none", bundle.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
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

    [Fact]
    public async Task LocalMapViewerRenderer_EmbedsParseableJsonInScriptTag()
    {
        await SeedMortalMapAsync();
        var map = await LocalMapViewService.BuildMortalWorldMapAsync(_fs);

        var html = LocalMapViewerRenderer.BuildStandaloneHtml(map);

        // The map JSON lives in <script type="application/json">. HTML-encoding
        // it (the old data-attribute wiring) would corrupt the payload because
        // browsers do NOT decode HTML entities inside <script>, so JSON.parse
        // would see &quot; literally and the page would render blank.
        Assert.DoesNotContain("&quot;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("&amp;", html, StringComparison.Ordinal);

        // Extract the script payload and confirm it parses back to the MapViewDto.
        var marker = "id=" + "\"map-viewer-data\"";
        var start = html.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, "map-viewer-data script tag not found");
        var payloadStart = html.IndexOf('>', start) + 1;
        var payloadEnd = html.IndexOf("</script>", payloadStart, StringComparison.Ordinal);
        Assert.True(payloadEnd > payloadStart, "script closing tag not found");
        var payload = html.Substring(payloadStart, payloadEnd - payloadStart);

        // The "</"-escape serialization must be reversed so the round-trip
        // matches the original DTO; System.Text.Json accepts <\/ as equivalent
        // to </, so a plain parse must succeed.
        var roundTripped = System.Text.Json.JsonSerializer.Deserialize<MapViewDto>(
            payload, LocalMapViewService.JsonOptions);
        Assert.NotNull(roundTripped);
        Assert.Equal(map.CurrentNodeId, roundTripped!.CurrentNodeId);
        Assert.Equal(map.Nodes.Count, roundTripped.Nodes.Count);
    }

    private async Task<AcceptedMortalMap> SeedMortalMapAsync(bool withFactionControl = false)
    {
        var source = CreateRawMortalLocation(
            "locref_map_square",
            "mlocmat_map_square",
            "current_scene_creation",
            "Старая площадь",
            x: 10,
            y: 20,
            z: 0,
            discoveryTier: "visited");
        var target = CreateRawMortalLocation(
            "locref_map_catacombs",
            "mlocmat_map_catacombs",
            "world_map_creation",
            "Катакомбы",
            x: 10,
            y: 19,
            z: -1,
            discoveryTier: "discovered");
        SetLocationSectionPopulated(source, "topology");
        SetLocationSectionPopulated(target, "topology");
        if (withFactionControl)
        {
            source["factionControl"] = new JsonArray(
                new JsonObject
                {
                    ["factionId"] = "f_crown",
                    ["factionName"] = "Серая Корона",
                    ["controlType"] = "Military",
                    ["controlLevel"] = 76
                });
            target["factionControl"] = new JsonArray(
                new JsonObject
                {
                    ["factionId"] = "f_crown",
                    ["factionName"] = "Серая Корона",
                    ["controlType"] = "Economic",
                    ["controlLevel"] = 48
                },
                new JsonObject
                {
                    ["factionId"] = "f_syndicate",
                    ["factionName"] = "Синдикат Тени",
                    ["controlType"] = "Covert",
                    ["controlLevel"] = 45
                });
            SetLocationSectionPopulated(source, "factionControl");
            SetLocationSectionPopulated(target, "factionControl");
        }

        var link = MortalLocationTestFixture.CreateRawLink("unused_source", "unused_target");
        link["initialId"] = "linkref_map_square_to_catacombs";
        link["sourceLocationId"] = null;
        link["sourceInitialId"] = "locref_map_square";
        link["targetLocationId"] = null;
        link["targetInitialId"] = "locref_map_catacombs";
        link["directionLabel"] = "вниз";
        link["materialization"]!["initialId"] = "linkref_map_square_to_catacombs";
        link["materialization"]!["materializationId"] = "mlinkmat_map_square_to_catacombs";

        var input = new MortalLocationAcceptedTurnInput(
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["realm"] = "mortal_world",
                ["locations"] = new JsonArray(),
                ["links"] = new JsonArray()
            },
            PreTurnCurrentLocation: null,
            PreTurnIdentityIndex: MortalLocationIdentityState.CreateEmptyRoot(),
            RawCurrentLocationData: source,
            RawWorldMapUpdates: new JsonObject
            {
                ["newLocations"] = new JsonArray(target),
                ["newLinks"] = new JsonArray(link)
            },
            Turn: 42);
        var next = 1;
        var planning = MortalLocationAcceptedTurnPlanner.Build(
            input,
            new MortalLocationIdentityFactory(() =>
                new Guid(next++, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)));
        Assert.True(planning.Success, string.Join(Environment.NewLine, planning.Issues.Select(issue => issue.Message)));
        var plan = Assert.IsType<MortalLocationAcceptedTurnPlan>(planning.Plan);
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            plan.FinalWorldMap.ToJsonString());
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            plan.FinalCurrentLocation!.ToJsonString());
        await _fs.WriteFileAtomicAsync(
            MortalLocationIdentityState.StatePath,
            plan.FinalIdentityIndex.ToJsonString());
        return new AcceptedMortalMap(
            plan,
            plan.LocationIdsByInitialId["locref_map_square"],
            plan.LocationIdsByInitialId["locref_map_catacombs"],
            plan.LinkIdsByInitialId["linkref_map_square_to_catacombs"]);
    }

    private static JsonObject CreateRawMortalLocation(
        string initialId,
        string materializationId,
        string route,
        string displayName,
        int x,
        int y,
        int z,
        string discoveryTier)
    {
        var location = MortalLocationTestFixture.CreateRawLocation(route);
        location["initialId"] = initialId;
        location["name"] = displayName;
        location["displayName"] = displayName;
        location["coordinates"] = new JsonObject { ["x"] = x, ["y"] = y, ["z"] = z };
        location["discovery"] = new JsonObject
        {
            ["tier"] = discoveryTier,
            ["audience"] = "player_known",
            ["rumorSummary"] = null
        };
        location["materialization"]!["initialId"] = initialId;
        location["materialization"]!["materializationId"] = materializationId;
        return location;
    }

    private static void SetLocationSectionPopulated(JsonObject location, string section)
    {
        location["materialization"]!["sections"]![section] = new JsonObject
        {
            ["disposition"] = "populated",
            ["reason"] = null
        };
    }

    private sealed record AcceptedMortalMap(
        MortalLocationAcceptedTurnPlan Plan,
        string SourceLocationId,
        string TargetLocationId,
        string LinkId);

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
