using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceVehicleOwnerTests
{
    [Fact]
    public void Compose_ExistingVehicleKeepsOnePermanentHealthOwner()
    {
        var vehicles = new JsonObject
        {
            ["vehicles"] = new JsonArray(
                new JsonObject
                {
                    ["vehicleId"] = "vehicle_cart_001",
                    ["name"] = "Телега",
                    ["availability"] = "Parked",
                    ["currentLocationId"] = "location_market"
                })
        };

        var roots = Roots(vehicles);
        var result = MortalResourceOwnerComposer.Compose(
            new MortalResourceOwnerCompositionInput(
                ResourceDefinitionCatalog.CreateBuiltIn(),
                roots,
                roots));

        Assert.Empty(result.Issues);
        var authority = Assert.IsType<ResourceOwnerAuthority>(result.Authority);
        var vehicle = Assert.Single(
            authority.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.Vehicle);
        Assert.Equal("vehicle_cart_001", vehicle.Key.ResourceOwnerId);
        Assert.False(vehicle.SameTurn);
        Assert.Equal(new[] { "health" }, vehicle.ResourceCapabilities);
        Assert.Empty(result.OwnerCompanionAfterImages);
    }

    [Fact]
    public void Compose_RejectsNewVehicleWithPreassignedPermanentId()
    {
        var accepted = new JsonObject
        {
            ["vehicles"] = new JsonArray(
                new JsonObject
                {
                    ["vehicleId"] = "vehicle_forged_001",
                    ["name"] = "Подложная телега"
                })
        };

        var result = MortalResourceOwnerComposer.Compose(
            new MortalResourceOwnerCompositionInput(
                ResourceDefinitionCatalog.CreateBuiltIn(),
                Roots(new JsonObject { ["vehicles"] = new JsonArray() }),
                Roots(accepted)));

        Assert.Null(result.Authority);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_owner_vehicle_preassigned_id_forbidden");
        Assert.Empty(result.OwnerCompanionAfterImages);
    }

    [Fact]
    public void Compose_NewVehicleConsumesTemporaryRefAndPublishesPermanentOwner()
    {
        var vehicles = new JsonObject
        {
            ["vehicles"] = new JsonArray(),
            ["UpdateVehicles"] = new JsonArray(
                new JsonObject
                {
                    ["vehicleRef"] = "vehicle_ref_cart_001",
                    ["name"] = "Телега",
                    ["availability"] = "Parked",
                    ["currentLocationId"] = "location_market",
                    ["resourceMaterialization"] = new JsonObject
                    {
                        ["resources"] = new JsonArray(new JsonObject
                        {
                            ["resourceKey"] = "health",
                            ["maximum"] = 140
                        })
                    }
                })
        };
        var factory = new DeterministicVehicleIdentityFactory();

        var result = MortalResourceOwnerComposer.Compose(
            new MortalResourceOwnerCompositionInput(
                ResourceDefinitionCatalog.CreateBuiltIn(),
                Roots(new JsonObject { ["vehicles"] = new JsonArray() }),
                Roots(vehicles)),
            new CombatantIdentityFactory(),
            factory);

        Assert.Empty(result.Issues);
        Assert.Equal(1, factory.Calls);
        var authority = Assert.IsType<ResourceOwnerAuthority>(result.Authority);
        var vehicle = Assert.Single(
            authority.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.Vehicle);
        Assert.Equal("vehicle_resource_test_1", vehicle.Key.ResourceOwnerId);
        Assert.True(vehicle.SameTurn);
        Assert.Equal("vehicle_ref_cart_001", vehicle.SameTurnRef);

        var afterImage = result.OwnerCompanionAfterImages[StorageTransportMoveService.VehiclesPath];
        Assert.False(afterImage.ContainsKey("UpdateVehicles"));
        var canonicalVehicle = Assert.Single(afterImage["vehicles"]!.AsArray())!.AsObject();
        Assert.Equal("vehicle_resource_test_1", canonicalVehicle["vehicleId"]!.GetValue<string>());
        Assert.False(canonicalVehicle.ContainsKey("vehicleRef"));
        Assert.False(canonicalVehicle.ContainsKey("resourceMaterialization"));
        Assert.Null(vehicles["UpdateVehicles"]![0]!["vehicleId"]);
        Assert.NotNull(vehicles["UpdateVehicles"]![0]!["vehicleRef"]);
    }

    [Fact]
    public void Compose_DestroyedVehicleBecomesHistoricalAndCannotBeRevived()
    {
        var vehicles = new JsonObject
        {
            ["vehicles"] = new JsonArray(
                new JsonObject
                {
                    ["vehicleId"] = "vehicle_cart_001",
                    ["name"] = "Телега",
                    ["availability"] = "Parked",
                    ["currentLocationId"] = "location_market"
                }),
            ["removeVehicles"] = new JsonArray("vehicle_cart_001")
        };

        var result = MortalResourceOwnerComposer.Compose(
            new MortalResourceOwnerCompositionInput(
                ResourceDefinitionCatalog.CreateBuiltIn(),
                Roots(new JsonObject
                {
                    ["vehicles"] = vehicles["vehicles"]!.DeepClone()
                }),
                Roots(vehicles)));

        Assert.Empty(result.Issues);
        var authority = Assert.IsType<ResourceOwnerAuthority>(result.Authority);
        Assert.DoesNotContain(
            authority.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.Vehicle);
        var resolution = authority.Resolve(new ResourceOwnerRequest(
            "mortal_world",
            ResourceOwnerKind.Vehicle,
            "health",
            ResourceOwnerId: "vehicle_cart_001",
            OwnerRef: null));
        Assert.Contains(resolution.Issues, issue =>
            issue.Code == "resource_owner_historical");

        var afterImage = result.OwnerCompanionAfterImages[StorageTransportMoveService.VehiclesPath];
        Assert.Empty(afterImage["vehicles"]!.AsArray());
        Assert.False(afterImage.ContainsKey("removeVehicles"));
    }

    private sealed class DeterministicVehicleIdentityFactory : VehicleIdentityFactory
    {
        internal int Calls { get; private set; }

        internal override string CreateVehicleId() =>
            $"vehicle_resource_test_{++Calls}";
    }

    private static MortalResourceOwnerRoots Roots(JsonObject vehicles) =>
        new(
            new JsonObject { ["NPCsInScene"] = new JsonArray() },
            new JsonObject { ["enemiesData"] = new JsonArray() },
            new JsonObject { ["alliesData"] = new JsonArray() },
            vehicles);
}
