using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Explicit effective access, rather than owner prose, governs active selection.</summary>
public sealed class MortalVehicleExplicitAccessTests
{
    /// <summary>Retaining an Active permanent identity must still require current boolean true access.</summary>
    [Theory]
    [InlineData("false")]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("\"true\"")]
    [InlineData("1")]
    public void Compose_RetainedActiveVehicleRequiresExplicitTrueAccess(string accessJson)
    {
        var vehicle = Vehicle("vehicle_cart", "Active");
        ApplyAccess(vehicle, accessJson);
        var before = Root(vehicle);
        before["activeVehicleId"] = "vehicle_cart";
        AssertRejectedWithoutMutation(before, before.DeepClone().AsObject());
    }

    /// <summary>A valid same-turn birth cannot become selected when its explicit access is false.</summary>
    [Fact]
    public void Compose_SameTurnRefCannotSelectRestrictedActiveVehicle()
    {
        var before = Root();
        var current = BirthRoot();
        current["UpdateVehicles"]![0]!["hasFullAccess"] = false;
        AssertRejectedWithoutMutation(before, current);
    }

    /// <summary>Explicit true remains usable for existing selection and allocated same-turn identities.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Compose_ExplicitTrueAccessAllowsActiveSelection(bool sameTurn)
    {
        var before = sameTurn ? Root() : Root(Vehicle("vehicle_cart", "Active"));
        if (!sameTurn)
            before["activeVehicleId"] = "vehicle_cart";
        var current = sameTurn ? BirthRoot() : before.DeepClone().AsObject();
        current["activeVehicleChange"] = sameTurn ? "new_cart" : "vehicle_cart";
        var result = Compose(before, current);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        var owner = Assert.Single(result.Authority!.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.Vehicle);
        Assert.Equal(new[] { "health" }, owner.ResourceCapabilities);
        Assert.Equal(sameTurn, owner.SameTurn);
        var after = result.OwnerCompanionAfterImages[StorageTransportMoveService.VehiclesPath];
        Assert.Equal(owner.Key.ResourceOwnerId, after["activeVehicleId"]!.GetValue<string>());
        Assert.False(after.ContainsKey("activeVehicleChange"));
        if (sameTurn)
            Assert.Equal("health", Assert.Single(result.CapacityDrafts).Coordinate.ResourceKey);
    }

    /// <summary>Restricted vehicles may remain parked; selection denial is not a blanket ownership ban.</summary>
    [Fact]
    public void Compose_RestrictedParkedVehicleWithoutSelectionRemainsValid()
    {
        var vehicle = Vehicle("vehicle_cart", "Parked");
        vehicle["hasFullAccess"] = false;
        var before = Root(vehicle);
        var result = Compose(before, before.DeepClone().AsObject());
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        Assert.Empty(result.OwnerCompanionAfterImages);
        Assert.Single(result.Authority!.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.Vehicle);
    }

    private static void AssertRejectedWithoutMutation(JsonObject before, JsonObject current)
    {
        var beforeJson = before.ToJsonString();
        var currentJson = current.ToJsonString();
        var result = Compose(before, current);
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.Null(result.Authority);
        Assert.Empty(result.OwnerCompanionAfterImages);
        Assert.True(ResourceRepairPacketBuilder.RequiresFailClosedRollback(result.Issues));
        Assert.Equal(beforeJson, before.ToJsonString());
        Assert.Equal(currentJson, current.ToJsonString());
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
        ["vehicles"] = new JsonArray(vehicles.Select(vehicle => (JsonNode)vehicle).ToArray()),
        ["activeVehicleId"] = null
    };

    private static JsonObject BirthRoot()
    {
        var vehicle = Vehicle("unused", "Active");
        vehicle.Remove("vehicleId");
        vehicle["vehicleRef"] = "new_cart";
        vehicle["resourceMaterialization"] = new JsonObject
        {
            ["resources"] = new JsonArray(new JsonObject { ["resourceKey"] = "health", ["maximum"] = 100 })
        };
        var root = Root();
        root["UpdateVehicles"] = new JsonArray(vehicle);
        root["activeVehicleChange"] = "new_cart";
        return root;
    }

    private static void ApplyAccess(JsonObject vehicle, string accessJson)
    {
        if (accessJson == "missing")
            vehicle.Remove("hasFullAccess");
        else
            vehicle["hasFullAccess"] = JsonNode.Parse(accessJson);
    }

    private static JsonObject Vehicle(string id, string availability) => new()
    {
        ["vehicleId"] = id,
        ["name"] = "Cart",
        ["description"] = "A serviceable cart used to carry supplies.",
        ["image_prompt"] = "A wooden supply cart with iron-rimmed wheels",
        ["type"] = "Vehicle",
        ["isSentient"] = false,
        ["availability"] = availability,
        ["currentLocationId"] = availability == "Parked" ? "location_market" : null,
        ["speedBonus"] = 0,
        ["actions"] = new JsonArray(),
        ["resistances"] = new JsonArray(),
        ["inventory"] = new JsonArray(),
        ["capacity"] = 4,
        ["volume"] = 20,
        ["owner"] = new JsonObject { ["ownerType"] = "Player", ["ownerId"] = "player_current", ["ownerName"] = "Current player" },
        ["controller"] = new JsonObject { ["actorType"] = "player", ["actorId"] = "player_current" },
        ["authorizedUsers"] = new JsonArray(),
        ["hasFullAccess"] = true
    };
}
