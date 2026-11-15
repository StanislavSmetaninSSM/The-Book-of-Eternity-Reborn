using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Checks reduction of active selection by the existing vehicle owner composer.</summary>
public sealed class MortalVehicleActiveSelectionTests
{
    [Fact]
    public void Compose_ConsumesSelectionOnlyWithoutReallocatingVehicle()
    {
        var before = Root(Vehicle("vehicle_cart", "Active"));
        var current = before.DeepClone().AsObject();
        current["activeVehicleChange"] = "vehicle_cart";
        var result = Compose(before, current);
        Assert.Empty(result.Issues);
        Assert.True(result.OwnerCompanionAfterImages.TryGetValue(StorageTransportMoveService.VehiclesPath, out var after));
        Assert.Equal("vehicle_cart", after!["activeVehicleId"]!.GetValue<string>());
        Assert.False(after.ContainsKey("activeVehicleChange"));
        Assert.Single(after["vehicles"]!.AsArray());
        Assert.Equal("vehicle_cart", current["activeVehicleChange"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("VEHICLE_CART")]
    [InlineData(" vehicle_cart ")]
    public void Compose_RejectsUnresolvedSelection(string selected)
    {
        var before = Root(Vehicle("vehicle_cart", "Parked"));
        var current = before.DeepClone().AsObject();
        current["activeVehicleChange"] = selected;
        var result = Compose(before, current);
        Assert.Contains(result.Issues, issue => issue.Code == "vehicle_active_selection_invalid");
        Assert.Empty(result.OwnerCompanionAfterImages);
    }

    [Fact]
    public void Compose_RejectsParkedSelection()
    {
        var before = Root(Vehicle("vehicle_cart", "Parked"));
        var current = before.DeepClone().AsObject();
        current["activeVehicleChange"] = "vehicle_cart";
        var result = Compose(before, current);
        Assert.Contains(result.Issues, issue => issue.Code == "vehicle_active_selection_invalid");
    }

    [Fact]
    public void Compose_ResolvesSameTurnRefUsingAllocatedIdentity()
    {
        var before = Root();
        var created = Vehicle("unused", "Active");
        created.Remove("vehicleId");
        created["vehicleRef"] = "new_cart";
        created["resourceMaterialization"] = new JsonObject
        {
            ["resources"] = new JsonArray(new JsonObject { ["resourceKey"] = "health", ["maximum"] = 100 })
        };
        var current = Root();
        current["UpdateVehicles"] = new JsonArray(created);
        current["activeVehicleChange"] = "new_cart";
        var result = Compose(before, current);
        Assert.Empty(result.Issues);
        var after = result.OwnerCompanionAfterImages[StorageTransportMoveService.VehiclesPath];
        Assert.Equal(after["vehicles"]![0]!["vehicleId"]!.GetValue<string>(), after["activeVehicleId"]!.GetValue<string>());
        Assert.False(after.ContainsKey("activeVehicleChange"));
    }

    private static ResourceOwnerCompositionResult Compose(JsonObject before, JsonObject current) =>
        MortalResourceOwnerComposer.Compose(new MortalResourceOwnerCompositionInput(
            ResourceDefinitionCatalog.CreateBuiltIn(), Roots(before), Roots(current)));

    private static MortalResourceOwnerRoots Roots(JsonObject vehicles) => new(
        new JsonObject { ["NPCsInScene"] = new JsonArray() },
        new JsonObject { ["enemiesData"] = new JsonArray() },
        new JsonObject { ["alliesData"] = new JsonArray() }, vehicles);

    private static JsonObject Root(params JsonObject[] vehicles) => new()
    {
        ["vehicles"] = new JsonArray(vehicles.Select(x => (JsonNode)x).ToArray())
    };

    private static JsonObject Vehicle(string id, string availability) => new()
    {
        ["vehicleId"] = id, ["name"] = "Cart", ["availability"] = availability,
        ["currentLocationId"] = availability == "Parked" ? "location_market" : null
    };
}
