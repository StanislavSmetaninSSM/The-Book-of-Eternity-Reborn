using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>First-birth semantic admission through the real owner composer, not a detached validator.</summary>
public sealed class MortalVehicleBirthContractTests
{
    [Fact]
    public void Compose_CompleteVehicleBirthRetainsSemanticsAndAllocatesHealthOwner()
    {
        var before = EmptyRoot();
        var current = BirthRoot();
        var beforeJson = before.ToJsonString();
        var currentJson = current.ToJsonString();
        var result = Compose(before, current);

        Assert.Empty(result.Issues);
        var authority = Assert.IsType<ResourceOwnerAuthority>(result.Authority);
        var owner = Assert.Single(authority.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.Vehicle);
        Assert.True(owner.SameTurn);
        Assert.Equal("new_cart", owner.SameTurnRef);
        Assert.Equal(new[] { "health" }, owner.ResourceCapabilities);
        var after = result.OwnerCompanionAfterImages[StorageTransportMoveService.VehiclesPath];
        var vehicle = Assert.Single(after["vehicles"]!.AsArray())!.AsObject();
        Assert.Equal(owner.Key.ResourceOwnerId, vehicle["vehicleId"]!.GetValue<string>());
        Assert.Equal(owner.Key.ResourceOwnerId, after["activeVehicleId"]!.GetValue<string>());
        foreach (var field in new[] { "capacity", "volume", "owner", "controller", "authorizedUsers", "hasFullAccess" })
            Assert.True(JsonNode.DeepEquals(current["UpdateVehicles"]![0]![field], vehicle[field]), field);
        Assert.False(vehicle.ContainsKey("vehicleRef"));
        Assert.False(vehicle.ContainsKey("resourceMaterialization"));
        Assert.Equal(beforeJson, before.ToJsonString());
        Assert.Equal(currentJson, current.ToJsonString());
    }

    [Theory]
    [InlineData("capacity")]
    [InlineData("volume")]
    [InlineData("owner")]
    [InlineData("controller")]
    [InlineData("authorizedUsers")]
    [InlineData("hasFullAccess")]
    public void Compose_RejectsMissingBirthSemanticField(string field)
    {
        var current = BirthRoot();
        current["UpdateVehicles"]![0]!.AsObject().Remove(field);
        AssertRejectedWithoutMutation(current);
    }

    [Theory]
    [InlineData("capacity", "-1")]
    [InlineData("capacity", "0.5")]
    [InlineData("capacity", "2147483648")]
    [InlineData("capacity", "\"12\"")]
    [InlineData("capacity", "null")]
    [InlineData("capacity", "true")]
    [InlineData("volume", "-0.5")]
    [InlineData("volume", "\"100\"")]
    [InlineData("volume", "null")]
    [InlineData("volume", "true")]
    [InlineData("volume", "1e400")]
    public void Compose_RejectsInvalidCargoLimitNumber(string field, string rawJson)
    {
        var current = BirthRoot();
        // 1e400 is valid JSON syntax but not a finite double; avoid non-JSON NaN literals.
        current["UpdateVehicles"]![0]![field] = JsonNode.Parse(rawJson);
        AssertRejectedWithoutMutation(current);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("controller")]
    [InlineData("authorizedUser")]
    public void Compose_RejectsUnresolvedPlayerAuthority(string mutation)
    {
        var current = BirthRoot();
        var vehicle = current["UpdateVehicles"]![0]!.AsObject();
        switch (mutation)
        {
            case "owner":
                vehicle["owner"]!["ownerId"] = "player_unknown";
                break;
            case "controller":
                vehicle["controller"]!["actorId"] = "player_unknown";
                break;
            case "authorizedUser":
                vehicle["authorizedUsers"]!.AsArray().Add(new JsonObject
                {
                    ["playerId"] = "player_unknown", ["playerName"] = "Unknown player"
                });
                break;
            default:
                throw new InvalidOperationException("Unknown test mutation.");
        }
        AssertRejectedWithoutMutation(current);
    }

    private static void AssertRejectedWithoutMutation(JsonObject current)
    {
        var before = EmptyRoot();
        var beforeJson = before.ToJsonString();
        var currentJson = current.ToJsonString();
        var result = Compose(before, current);
        Assert.Contains(result.Issues, issue => issue.Code == "resource_owner_vehicle_birth_invalid");
        Assert.Null(result.Authority);
        Assert.Empty(result.OwnerCompanionAfterImages);
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

    private static JsonObject EmptyRoot() => new() { ["vehicles"] = new JsonArray() };

    private static JsonObject BirthRoot() => JsonNode.Parse("""
        {
          "vehicles": [],
          "activeVehicleChange": "new_cart",
          "UpdateVehicles": [{
            "vehicleRef": "new_cart",
            "name": "Cart",
            "description": "A serviceable cart used to carry supplies.",
            "image_prompt": "A wooden supply cart with iron-rimmed wheels",
            "type": "Vehicle",
            "isSentient": false,
            "availability": "Active",
            "currentLocationId": null,
            "speedBonus": 0,
            "actions": [],
            "resistances": [],
            "inventory": [],
            "capacity": 12,
            "volume": 100.0,
            "owner": { "ownerType": "Player", "ownerId": "player_current", "ownerName": "Current player" },
            "controller": { "actorType": "player", "actorId": "player_current" },
            "authorizedUsers": [],
            "hasFullAccess": true,
            "resourceMaterialization": { "resources": [{ "resourceKey": "health", "maximum": 100 }] }
          }]
        }
        """)!.AsObject();
}
