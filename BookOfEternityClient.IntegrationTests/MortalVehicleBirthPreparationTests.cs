using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Ordinary pending-turn repetition must retain vehicle allocation DATA, not publication rights.</summary>
public sealed class MortalVehicleBirthPreparationTests
{
    [Fact]
    public async Task RepeatedOrdinaryAdmission_KeepsExactVehicleIdentityAndCompanion()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await CanonicalResourceQuartetTestFixture.CommitFreshBootstrapAsync(context.FileSystem);
        await context.WriteExactJsonAsync(StorageTransportMoveService.VehiclesPath,
            new JsonObject { ["vehicles"] = new JsonArray() }.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);
        var raw = BirthRoot().ToJsonString();
        await context.WriteExactJsonAsync(StorageTransportMoveService.VehiclesPath, raw);
        var first = await Admit(context);
        var second = await Admit(context);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Companion, second.Companion);
        Assert.Equal(raw, (await context.ReadJsonAsync(StorageTransportMoveService.VehiclesPath))!.ToJsonString());
    }

    private static async Task<(string Id, string Companion)> Admit(ResourceMaterializationTestContext context)
    {
        var issues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var handoff = await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(context.FileSystem);
        Assert.NotNull(handoff);
        var plan = Assert.IsType<AcceptedMechanicsPlan>(handoff.Result.Plan);
        var owner = Assert.Single(plan.OwnerAuthority.Entries.Values, e => e.Key.OwnerKind == ResourceOwnerKind.Vehicle);
        var companion = plan.OwnerCompanionAfterImages[StorageTransportMoveService.VehiclesPath];
        return (owner.Key.ResourceOwnerId, companion.ToJsonString());
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
