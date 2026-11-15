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
        before["activeVehicleId"] = "vehicle_cart";
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
        Assert.Contains(result.Issues, issue => issue.Code == "resource_owner_vehicle_active_selection_invalid");
        Assert.Empty(result.OwnerCompanionAfterImages);
    }

    [Fact]
    public void Compose_RejectsParkedSelection()
    {
        var before = Root(Vehicle("vehicle_cart", "Parked"));
        var current = before.DeepClone().AsObject();
        current["activeVehicleChange"] = "vehicle_cart";
        var result = Compose(before, current);
        Assert.Contains(result.Issues, issue => issue.Code == "resource_owner_vehicle_active_selection_invalid");
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
        Assert.NotNull(after["activeVehicleId"]);
        Assert.Equal(after["vehicles"]![0]!["vehicleId"]!.GetValue<string>(), after["activeVehicleId"]!.GetValue<string>());
        Assert.False(after.ContainsKey("activeVehicleChange"));
    }

    [Fact]
    public void Compose_RejectsDirectCanonicalSelectionChange()
    {
        var before = Root(Vehicle("vehicle_cart", "Parked"));
        before["activeVehicleId"] = null;
        var current = before.DeepClone().AsObject();
        current["activeVehicleId"] = "vehicle_cart";
        var result = Compose(before, current);
        Assert.Contains(result.Issues, issue => issue.Code == "resource_owner_vehicle_active_selection_client_owned");
        Assert.Empty(result.OwnerCompanionAfterImages);
    }

    [Fact]
    public void Compose_RemovingSelectedVehicleClearsCanonicalSelection()
    {
        var before = Root(Vehicle("vehicle_cart", "Active"));
        before["activeVehicleId"] = "vehicle_cart";
        var current = before.DeepClone().AsObject();
        current["removeVehicles"] = new JsonArray("vehicle_cart");
        var result = Compose(before, current);
        Assert.Empty(result.Issues);
        var after = result.OwnerCompanionAfterImages[StorageTransportMoveService.VehiclesPath];
        Assert.Null(after["activeVehicleId"]);
        Assert.Empty(after["vehicles"]!.AsArray());
    }

    [Fact]
    public void Compose_RejectsParkingSelectedVehicleWithoutClearingSelection()
    {
        var before = Root(Vehicle("vehicle_cart", "Active"));
        before["activeVehicleId"] = "vehicle_cart";
        var current = before.DeepClone().AsObject();
        current["UpdateVehicles"] = new JsonArray(Vehicle("vehicle_cart", "Parked"));
        var result = Compose(before, current);
        Assert.Contains(result.Issues, issue => issue.Code == "resource_owner_vehicle_active_selection_invalid");
    }

    [Fact]
    public void Compose_ExplicitClearWithParkingConsumesCommand()
    {
        var before = Root(Vehicle("vehicle_cart", "Active"));
        before["activeVehicleId"] = "vehicle_cart";
        var current = before.DeepClone().AsObject();
        current["UpdateVehicles"] = new JsonArray(Vehicle("vehicle_cart", "Parked"));
        current["activeVehicleChange"] = null;
        var result = Compose(before, current);
        Assert.Empty(result.Issues);
        var after = result.OwnerCompanionAfterImages[StorageTransportMoveService.VehiclesPath];
        Assert.True(after.ContainsKey("activeVehicleId"));
        Assert.Null(after["activeVehicleId"]);
        Assert.False(after.ContainsKey("activeVehicleChange"));
    }

    [Fact]
    public void Compose_RejectsClearWhileVehicleStillActive()
    {
        var before = Root(Vehicle("vehicle_cart", "Active"));
        before["activeVehicleId"] = "vehicle_cart";
        var current = before.DeepClone().AsObject();
        current["activeVehicleChange"] = null;
        var result = Compose(before, current);
        Assert.Contains(result.Issues, issue => issue.Code == "resource_owner_vehicle_active_selection_invalid");
    }

    [Fact]
    public void Compose_ExplicitReplacementPreservesBothPermanentIds()
    {
        var before = Root(Vehicle("vehicle_old", "Active"), Vehicle("vehicle_new", "Parked"));
        before["activeVehicleId"] = "vehicle_old";
        var current = before.DeepClone().AsObject();
        current["UpdateVehicles"] = new JsonArray(Vehicle("vehicle_old", "Parked"), Vehicle("vehicle_new", "Active"));
        current["activeVehicleChange"] = "vehicle_new";
        var result = Compose(before, current);
        Assert.Empty(result.Issues);
        var after = result.OwnerCompanionAfterImages[StorageTransportMoveService.VehiclesPath];
        Assert.Equal("vehicle_new", after["activeVehicleId"]!.GetValue<string>());
        Assert.Equal(new[] { "vehicle_old", "vehicle_new" }, after["vehicles"]!.AsArray().Select(v => v!["vehicleId"]!.GetValue<string>()));
    }

    [Fact]
    public void Compose_RejectsTwoActiveVehicles()
    {
        var before = Root(Vehicle("vehicle_old", "Active"), Vehicle("vehicle_new", "Parked"));
        before["activeVehicleId"] = "vehicle_old";
        var current = before.DeepClone().AsObject();
        current["UpdateVehicles"] = new JsonArray(Vehicle("vehicle_new", "Active"));
        current["activeVehicleChange"] = "vehicle_new";
        var result = Compose(before, current);
        Assert.Contains(result.Issues, issue => issue.Code == "resource_owner_vehicle_active_selection_invalid");
    }

    [Fact]
    public void Compose_RejectsNewActiveVehicleWithoutSelectionCommand()
    {
        var created = Vehicle("unused", "Active");
        created.Remove("vehicleId");
        created["vehicleRef"] = "new_cart";
        created["resourceMaterialization"] = new JsonObject
        {
            ["resources"] = new JsonArray(new JsonObject { ["resourceKey"] = "health", ["maximum"] = 100 })
        };
        var current = Root();
        current["UpdateVehicles"] = new JsonArray(created);
        var result = Compose(Root(), current);
        Assert.Contains(result.Issues, issue => issue.Code == "resource_owner_vehicle_active_selection_invalid");
        Assert.Empty(result.OwnerCompanionAfterImages);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Compose_CanonicalRetentionDoesNotUseTransientNamespace(bool explicitCommand)
    {
        var before = Root(Vehicle("vehicle_cart", "Active"));
        before["activeVehicleId"] = "vehicle_cart";
        var current = before.DeepClone().AsObject();
        var created = Vehicle("unused", "Parked");
        created.Remove("vehicleId");
        created["vehicleRef"] = "vehicle_cart";
        created["resourceMaterialization"] = new JsonObject
        {
            ["resources"] = new JsonArray(new JsonObject { ["resourceKey"] = "health", ["maximum"] = 100 })
        };
        current["UpdateVehicles"] = new JsonArray(created);
        if (explicitCommand) current["activeVehicleChange"] = "vehicle_cart";
        var result = Compose(before, current);
        if (explicitCommand)
            Assert.Contains(result.Issues, issue => issue.Code == "resource_owner_vehicle_active_selection_invalid");
        else
        {
            Assert.Empty(result.Issues);
            Assert.Equal("vehicle_cart", result.OwnerCompanionAfterImages[StorageTransportMoveService.VehiclesPath]["activeVehicleId"]!.GetValue<string>());
        }
    }

    [Theory]
    [InlineData("selected")]
    [InlineData("clear")]
    [InlineData("duplicate")]
    public void Compose_UsesAdmittedCaseInsensitiveAvailability(string scenario)
    {
        var before = Root(Vehicle("vehicle_cart", scenario == "duplicate" ? "Active" : "active"));
        before["activeVehicleId"] = "vehicle_cart";
        var current = before.DeepClone().AsObject();
        current["activeVehicleChange"] = scenario == "clear" ? null : "vehicle_cart";
        if (scenario == "duplicate")
        {
            before["vehicles"]!.AsArray().Add(Vehicle("vehicle_other", "Parked"));
            current["vehicles"]!.AsArray().Add(Vehicle("vehicle_other", "Parked"));
            current["UpdateVehicles"] = new JsonArray(Vehicle("vehicle_other", "active"));
        }
        var result = Compose(before, current);
        if (scenario == "selected")
            Assert.Empty(result.Issues);
        else
            Assert.Contains(result.Issues, issue => issue.Code == "resource_owner_vehicle_active_selection_invalid");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectionAuthorityFailure_UsesProtectedRollbackInsteadOfBroadGmRepair(bool directCanonicalWrite)
    {
        var before = Root(Vehicle("vehicle_cart", "Parked"));
        before["activeVehicleId"] = null;
        var current = before.DeepClone().AsObject();
        current[directCanonicalWrite ? "activeVehicleId" : "activeVehicleChange"] = "vehicle_cart";
        var result = Compose(before, current);
        Assert.NotEmpty(result.Issues);
        Assert.Empty(ResourceRepairPacketBuilder.Build(result.Issues));
        Assert.True(ResourceRepairPacketBuilder.RequiresFailClosedRollback(result.Issues));
    }

    [Fact]
    public void AuthoringGuidance_PreservesCanonicalSelectionAndExplainsExactTransientSelection()
    {
        foreach (var path in new[] { "Rules/Block_2.txt", "Rules/Block_CLI_Operations.txt",
                     "Examples/E_Block_10.V.txt", "BookOfEternityClient/game_master_daemon.ps1" })
        {
            var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path));
            Assert.Contains("activeVehicleId is client-owned", text, StringComparison.Ordinal);
            Assert.Contains("same-turn vehicleRef", text, StringComparison.Ordinal);
            Assert.Contains("null requires no Active vehicle", text, StringComparison.Ordinal);
            Assert.Contains("does not implicitly activate or park", text, StringComparison.Ordinal);
            Assert.Contains("selected vehicle must have explicit hasFullAccess=true", text, StringComparison.Ordinal);
            Assert.Contains("cannot be bypassed by ownership", text, StringComparison.Ordinal);
        }
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
        ["inventory"] = new JsonArray(),
        ["capacity"] = 4,
        ["volume"] = 20,
        ["owner"] = new JsonObject { ["ownerType"] = "Player", ["ownerId"] = "player_current", ["ownerName"] = "Current player" },
        ["controller"] = new JsonObject { ["actorType"] = "player", ["actorId"] = "player_current" },
        ["authorizedUsers"] = new JsonArray(),
        ["hasFullAccess"] = true,
        ["currentLocationId"] = availability == "Parked" ? "location_market" : null
    };
}
