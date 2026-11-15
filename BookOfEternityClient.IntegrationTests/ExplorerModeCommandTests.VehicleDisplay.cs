using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerModeCommandTests
{
    [Theory]
    [InlineData("vehicle_display", false, "Active", true)]
    [InlineData("VEHICLE_DISPLAY", true, "Parked", false)]
    [InlineData(null, true, "Parked", false)]
    public async Task TryProcessCommand_VehicleDisplay_SelectionUsesExactCanonicalActiveVehicleId(
        string? selectedId, bool legacyActive, string availability, bool expectedSelected)
    {
        await SeedMortalStateAsync();
        var root = VehicleDisplayRoot();
        root["activeVehicleId"] = selectedId;
        root["vehicles"]![0]!["availability"] = availability;
        root["vehicles"]![0]!["isActive"] = legacyActive;
        await _fs.WriteFileAtomicAsync(StorageTransportMoveService.VehiclesPath, root.ToJsonString());
        var before = await _fs.ReadFileBytesAsync(StorageTransportMoveService.VehiclesPath);
        _console.QueueSelection("Транспорт", "Каноническая телега", "← Назад");

        await _explorer.TryProcessCommand("/транспорт");

        AssertNoHiddenExplorerErrors("vehicle_display_selection");
        var choices = _console.SelectionChoicesHistory.First(p => p.Title.Contains("Транспорт", StringComparison.Ordinal)).Choices;
        var vehicleChoice = Assert.Single(choices.Where(c => c.Contains("Каноническая телега", StringComparison.Ordinal)));
        Assert.Equal(expectedSelected, vehicleChoice.Contains("✓", StringComparison.Ordinal));
        Assert.Contains(availability == "Active" ? "Активен (оседлан/управляется)" : "Припаркован", ExtractRenderedText(), StringComparison.Ordinal);
        Assert.Equal(before, await _fs.ReadFileBytesAsync(StorageTransportMoveService.VehiclesPath));
    }

    [Fact]
    public async Task TryProcessCommand_VehicleDisplay_RejectsLegacyRootArray()
    {
        await SeedMortalStateAsync();
        await _fs.WriteFileAtomicAsync(StorageTransportMoveService.VehiclesPath,
            new JsonArray(VehicleDisplayVehicle("PRIVATE_ROOT_ARRAY")).ToJsonString());

        await _explorer.TryProcessCommand("/транспорт");

        AssertNoHiddenExplorerErrors("vehicle_display_root_array");
        Assert.Empty(_console.SelectionChoicesHistory);
        Assert.DoesNotContain("PRIVATE_ROOT_ARRAY", ExtractRenderedText(), StringComparison.Ordinal);
        Assert.Contains("Транспорта нет", ExtractRenderedText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_VehicleDisplay_IgnoresPendingAndHistoricalVehicles()
    {
        await SeedMortalStateAsync();
        var root = VehicleDisplayRoot();
        root["UpdateVehicles"] = new JsonArray(VehicleDisplayVehicle("PRIVATE_PENDING_VEHICLE"));
        root["pendingUpdateVehicles"] = new JsonArray(VehicleDisplayVehicle("PRIVATE_PENDING_LOWER"));
        root["birthReceipts"] = new JsonArray(new JsonObject { ["birthState"] = VehicleDisplayVehicle("PRIVATE_HISTORY") });
        await _fs.WriteFileAtomicAsync(StorageTransportMoveService.VehiclesPath, root.ToJsonString());
        _console.QueueSelection("Транспорт", "Каноническая телега", "← Назад");

        await _explorer.TryProcessCommand("/транспорт");

        AssertNoHiddenExplorerErrors("vehicle_display_pending");
        var choices = _console.SelectionChoicesHistory.First(p => p.Title.Contains("Транспорт", StringComparison.Ordinal)).Choices;
        Assert.Equal(2, choices.Count);
        Assert.DoesNotContain(choices, c => c.Contains("PRIVATE_", StringComparison.Ordinal));
        Assert.Contains("Припаркован", ExtractRenderedText(), StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_", ExtractRenderedText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_VehicleDisplay_DoesNotInferAvailabilityFromLegacyActiveFlag()
    {
        await SeedMortalStateAsync();
        var root = VehicleDisplayRoot();
        var vehicle = root["vehicles"]![0]!.AsObject();
        vehicle.Remove("availability");
        vehicle["isActive"] = true;
        vehicle["status"] = "PRIVATE_LEGACY_STATUS";
        await _fs.WriteFileAtomicAsync(StorageTransportMoveService.VehiclesPath, root.ToJsonString());
        _console.QueueSelection("Транспорт", "Каноническая телега", "← Назад");

        await _explorer.TryProcessCommand("/транспорт");

        AssertNoHiddenExplorerErrors("vehicle_display_legacy_availability");
        Assert.DoesNotContain("Активен", ExtractRenderedText(), StringComparison.Ordinal);
        Assert.Contains("не указано", ExtractRenderedText(), StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_LEGACY_STATUS", ExtractRenderedText(), StringComparison.Ordinal);
    }

    private static JsonObject VehicleDisplayRoot() => new()
    {
        ["activeVehicleId"] = null,
        ["vehicles"] = new JsonArray(VehicleDisplayVehicle("Каноническая телега"))
    };

    private static JsonObject VehicleDisplayVehicle(string name) => new()
    {
        ["vehicleId"] = "vehicle_display",
        ["name"] = name,
        ["type"] = "Vehicle",
        ["availability"] = "Parked",
        ["currentLocationId"] = "loc_display",
        ["description"] = "Телега с закрытым кузовом.",
        ["capacity"] = 2,
        ["volume"] = 10,
        ["hasFullAccess"] = true
    };
}
