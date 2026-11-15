using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Vehicle birth completeness must be enforced at the actual meta-state admission boundary.</summary>
public sealed class MortalVehicleBirthAdmissionTests
{
    [Fact]
    public async Task MetaMiscValidation_AcceptsCompleteRawVehicleBirth()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await context.WriteExactJsonAsync(StorageTransportMoveService.VehiclesPath,
            BirthRoot().ToJsonString());

        var issues = await context.Validator.ValidateGameStateAsync(
            GameStateValidationPhase.MetaMiscStateFiles);

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
    }

    [Theory]
    [InlineData("capacity")]
    [InlineData("volume")]
    [InlineData("owner")]
    [InlineData("controller")]
    [InlineData("authorizedUsers")]
    [InlineData("hasFullAccess")]
    public async Task MetaMiscValidation_RejectsMissingVehicleBirthField(string field)
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var root = BirthRoot();
        root["UpdateVehicles"]![0]!.AsObject().Remove(field);
        await context.WriteExactJsonAsync(StorageTransportMoveService.VehiclesPath,
            root.ToJsonString());

        var issues = await context.Validator.ValidateGameStateAsync(
            GameStateValidationPhase.MetaMiscStateFiles);

        Assert.Contains(issues, issue =>
            issue.Code == "resource_owner_vehicle_birth_invalid" &&
            issue.Severity == IssueSeverity.Error &&
            issue.FilePath.EndsWith("." + field, StringComparison.Ordinal));
    }

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
