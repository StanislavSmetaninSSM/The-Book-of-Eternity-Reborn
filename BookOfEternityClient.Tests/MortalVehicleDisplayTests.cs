using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalVehicleDisplayTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-vehicle-display-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemManager _fs;
    private readonly StateManager _state;

    public MortalVehicleDisplayTests()
    {
        Directory.CreateDirectory(_root);
        _fs = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _state = new StateManager(_fs, new GameSettings(), NullLogger<StateManager>.Instance);
    }

    [Fact]
    public async Task Overview_OnlyCanonicalVehiclesProduceCardsCountsAndActions()
    {
        var root = CanonicalRoot();
        root["UpdateVehicles"] = new JsonArray(Vehicle("vehicle_pending", "PRIVATE_PENDING_VEHICLE"));
        root["pendingUpdateVehicles"] = new JsonArray(Vehicle("vehicle_pending_lower", "PRIVATE_PENDING_LOWER"));
        root["birthReceipts"] = new JsonArray(new JsonObject { ["birthState"] = Vehicle("vehicle_removed", "PRIVATE_HISTORICAL_VEHICLE") });
        await WriteVehiclesAsync(root);
        await _fs.WriteFileAtomicAsync("game_state/world/world_map.json", """{"transportRoutes":[{}]}""");
        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", """{"availableTransport":[{}]}""");

        var result = await BuildAsync("/transport");

        var card = Assert.Single(Dossiers(result.Blocks).Where(d => d.EntityType == "transport-summary"));
        Assert.Equal("Каноническая телега", card.Title);
        Assert.Single(result.Actions);
        Assert.Equal("/transport vehicle vehicle_canonical", result.Actions[0].Command);
        Assert.DoesNotContain("PRIVATE_", Payload(result), StringComparison.Ordinal);
        Assert.DoesNotContain(Facts(result.Blocks), f => f.Key is "Маршрутов" or "Доступного транспорта");
        var console = new TestExplorerConsole();
        ExplorerCommandResultConsoleRenderer.Render(console, result);
        Assert.NotEmpty(console.Rendered);

        var pendingDetail = await BuildAsync("/transport vehicle vehicle_pending");
        Assert.DoesNotContain(Dossiers(pendingDetail.Blocks), d => d.EntityType == "transport-detail");
        Assert.DoesNotContain("PRIVATE_", Payload(pendingDetail), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/transport")]
    [InlineData("/транспорт")]
    public async Task Overview_RootArraysAreNotCanonicalVehicles(string command)
    {
        await WriteVehiclesAsync(new JsonArray(Vehicle("vehicle_old", "PRIVATE_ROOT_ARRAY")));

        var result = await BuildAsync(command);

        Assert.Empty(result.Actions);
        Assert.DoesNotContain(Dossiers(result.Blocks), d => d.EntityType == "transport-summary");
        Assert.DoesNotContain("PRIVATE_ROOT_ARRAY", Payload(result), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" vehicle_alias")]
    public async Task Overview_ExcludesVehicleWithoutExactCanonicalIdentity(string? vehicleId)
    {
        var root = CanonicalRoot();
        var legacy = Vehicle("vehicle_alias", "PRIVATE_ID_ALIAS_VEHICLE");
        if (vehicleId is null)
            legacy.Remove("vehicleId");
        else
            legacy["vehicleId"] = vehicleId;
        legacy["id"] = "vehicle_alias";
        root["vehicles"]!.AsArray().Add(legacy);
        await WriteVehiclesAsync(root);

        var result = await BuildAsync("/transport");

        Assert.Equal("Каноническая телега", Assert.Single(Dossiers(result.Blocks).Where(d => d.EntityType == "transport-summary")).Title);
        Assert.Equal("/transport vehicle vehicle_canonical", Assert.Single(result.Actions).Command);
        Assert.Equal("1", Assert.Single(Facts(result.Blocks).Where(f => f.Key == "Транспорта")).Value);
        Assert.DoesNotContain("PRIVATE_ID_ALIAS_VEHICLE", Payload(result), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("vehicle_canonical", true, "Active")]
    [InlineData("VEHICLE_CANONICAL", false, "Parked")]
    [InlineData(null, false, "Parked")]
    public async Task Selection_UsesExactCanonicalActiveVehicleIdInOverviewAndDetail(
        string? selectedId, bool expectedSelected, string availability)
    {
        var root = CanonicalRoot();
        root["activeVehicleId"] = selectedId;
        root["vehicles"]![0]!["availability"] = availability;
        root["vehicles"]![0]!["isActive"] = !expectedSelected;
        await WriteVehiclesAsync(root);

        foreach (var command in new[] { "/transport", "/transport vehicle vehicle_canonical" })
        {
            var result = await BuildAsync(command);
            var selected = Assert.Single(Facts(result.Blocks).Where(f => f.Key == "Выбран"));
            Assert.Equal(expectedSelected ? "да" : "нет", selected.Value);
            Assert.Contains(Facts(result.Blocks), f =>
                f.Key is "Состояние" or "Доступность" &&
                f.Value == (availability == "Active" ? "Активен (оседлан/управляется)" : "Припаркован"));
        }
    }

    [Theory]
    [InlineData("123", "123")]
    [InlineData("true", "true")]
    public async Task Selection_DoesNotCoerceNonStringActiveIdentity(string selectedJson, string vehicleId)
    {
        var root = CanonicalRoot();
        root["activeVehicleId"] = JsonNode.Parse(selectedJson);
        root["vehicles"]![0]!["vehicleId"] = vehicleId;
        await WriteVehiclesAsync(root);

        foreach (var command in new[] { "/transport", "/transport vehicle " + vehicleId })
        {
            var result = await BuildAsync(command);
            Assert.Equal("нет", Assert.Single(Facts(result.Blocks).Where(f => f.Key == "Выбран")).Value);
            Assert.Contains(Dossiers(result.Blocks), d => d.Title.Contains("Каноническая телега", StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData("Active", true)]
    [InlineData("Pocket", false)]
    public async Task Detail_LegacyStatusAndIsActiveDoNotSupplyAvailability(string status, bool isActive)
    {
        var root = CanonicalRoot();
        var vehicle = root["vehicles"]![0]!.AsObject();
        vehicle.Remove("availability");
        vehicle["status"] = status;
        vehicle["isActive"] = isActive;
        await WriteVehiclesAsync(root);

        var result = await BuildAsync("/transport vehicle vehicle_canonical");

        Assert.Equal("не указано", Assert.Single(Facts(result.Blocks).Where(f => f.Key == "Доступность")).Value);
    }

    [Theory]
    [InlineData("Mount", "Active", "Ездовое животное", "Активен (оседлан/управляется)")]
    [InlineData("Vehicle", "Parked", "Транспорт", "Припаркован")]
    [InlineData("Summonable", "Pocket", "Призываемый", "В кармане (призываемый)")]
    public async Task CanonicalTypeAvailabilityAndLocationRemainReadable(
        string type, string availability, string expectedType, string expectedAvailability)
    {
        var root = CanonicalRoot();
        var vehicle = root["vehicles"]![0]!.AsObject();
        vehicle["type"] = type;
        vehicle["availability"] = availability;
        root["activeVehicleId"] = availability == "Active" ? "vehicle_canonical" : null;
        await WriteVehiclesAsync(root);

        var result = await BuildAsync("/transport vehicle vehicle_canonical");

        Assert.Contains(Facts(result.Blocks), f => f.Key == "Тип" && f.Value == expectedType);
        Assert.Contains(Facts(result.Blocks), f => f.Key == "Доступность" && f.Value == expectedAvailability);
        Assert.Contains(Facts(result.Blocks), f => f.Key == "Где" && f.Value == "loc_canonical");
    }

    [Fact]
    public async Task Detail_CanonicalLocationWinsWithoutReadingLegacyAlias()
    {
        var root = CanonicalRoot();
        root["vehicles"]![0]!["currentLocation"] = "PRIVATE_LEGACY_LOCATION";
        await WriteVehiclesAsync(root);

        var result = await BuildAsync("/transport vehicle vehicle_canonical");

        Assert.Contains(Facts(result.Blocks), f => f.Key == "Где" && f.Value == "loc_canonical");
        Assert.DoesNotContain("PRIVATE_LEGACY_LOCATION", Payload(result), StringComparison.Ordinal);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private Task WriteVehiclesAsync(JsonNode root) =>
        _fs.WriteFileAtomicAsync(StorageTransportMoveService.VehiclesPath, root.ToJsonString());

    private async Task<ExplorerCommandResult> BuildAsync(string command) =>
        Assert.IsType<ExplorerCommandResult>(await ExplorerMortalWorldCommandResultBuilder.TryBuildAsync(command, _state, _fs));

    private static JsonObject CanonicalRoot() => new()
    {
        ["activeVehicleId"] = null,
        ["vehicles"] = new JsonArray(Vehicle("vehicle_canonical", "Каноническая телега"))
    };

    private static JsonObject Vehicle(string id, string name) => new()
    {
        ["vehicleId"] = id,
        ["name"] = name,
        ["type"] = "Vehicle",
        ["availability"] = "Parked",
        ["currentLocationId"] = "loc_canonical",
        ["description"] = "Телега с закрытым кузовом.",
        ["capacity"] = 2,
        ["volume"] = 10,
        ["hasFullAccess"] = true,
        ["inventory"] = new JsonArray()
    };

    private static string Payload(ExplorerCommandResult result) => JsonSerializer.Serialize(result,
        new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private static IEnumerable<UiEntityDossierBlock> Dossiers(IEnumerable<UiBlock> blocks)
    {
        foreach (var dossier in blocks.OfType<UiEntityDossierBlock>())
        {
            yield return dossier;
            foreach (var nested in Dossiers(dossier.Sections.SelectMany(s => s.Blocks)))
                yield return nested;
        }
    }

    private static IEnumerable<UiKeyValueItem> Facts(IEnumerable<UiBlock> blocks)
    {
        foreach (var block in blocks)
        {
            if (block is UiKeyValueGridBlock grid)
                foreach (var item in grid.Items) yield return item;
            if (block is UiEntityDossierBlock dossier)
                foreach (var item in Facts(dossier.Sections.SelectMany(s => s.Blocks))) yield return item;
        }
    }
}
