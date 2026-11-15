using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalVehicleBirthCargoTests
{
    [Fact]
    public async Task SameTurnVehicleBirth_SealsCargoWithPermanentCarrierAndHealth()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath, CargoBirth("new_cart"));

        await context.NormalizeAcceptedTurnWithValidatedCommonPlanAsync();

        var root = (await context.ReadJsonAsync(StorageTransportMoveService.VehiclesPath))!.AsObject();
        Assert.False(root.ContainsKey("UpdateVehicles"));
        var vehicle = Assert.Single(root["vehicles"]!.AsArray())!.AsObject();
        var vehicleId = vehicle["vehicleId"]!.GetValue<string>();
        Assert.False(string.IsNullOrWhiteSpace(vehicleId));
        Assert.NotEqual("new_cart", vehicleId);
        Assert.Equal(vehicleId, root["activeVehicleId"]!.GetValue<string>());
        Assert.False(vehicle.ContainsKey("vehicleRef"));
        Assert.False(vehicle.ContainsKey("resourceMaterialization"));
        var cargo = Assert.Single(vehicle["inventory"]!.AsArray())!.AsObject();
        var itemId = cargo["itemId"]!.GetValue<string>();
        Assert.Equal(itemId, cargo["existedId"]!.GetValue<string>());
        Assert.NotNull(cargo["materializationReceipt"]);
        Assert.False(cargo.ContainsKey("creationRef"));
        var index = MortalItemIdentityState.Parse(
            (await context.ReadJsonAsync(MortalItemIdentityState.StatePath))!.AsObject());
        Assert.Empty(index.Issues);
        Assert.True(index.EntriesByItemId.TryGetValue(itemId, out var entry));
        var carrier = entry!["currentCarrier"]!.AsObject();
        Assert.Equal("vehicle_inventory", carrier["kind"]!.GetValue<string>());
        Assert.Equal(vehicleId, carrier["ownerId"]!.GetValue<string>());
        Assert.Null(carrier["containerId"]);
        Assert.Empty(carrier["containerPath"]!.AsArray());
        Assert.DoesNotContain(
            await context.Validator.ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);

        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            (await context.ReadJsonAsync(ResourceMaterializationContract.DefinitionsPath))!.ToJsonString(),
            allowMissingPristine: false);
        Assert.NotNull(definitions.Catalog);
        var state = ResourceStateContract.ParseCanonical(
            (await context.ReadJsonAsync(ResourceMaterializationContract.StatePath))!.ToJsonString(),
            definitions.Catalog!, allowMissingPristine: false);
        Assert.True(state.IsValid, string.Join(Environment.NewLine, state.Issues));
        var health = Assert.Single(state.Ledger!.Entries,
            value => value.Coordinate.ResourceOwnerId == vehicleId && value.Coordinate.ResourceKey == "health");
        Assert.Equal(100m, health.Current);
        Assert.Equal(100m, health.Maximum);
    }

    [Fact]
    public async Task SameTurnVehicleCargo_ForeignSourceRefIsRejectedWithoutRawMutation()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath, CargoBirth("foreign_cart"));
        var before = await context.CaptureExactBytesAsync(new[]
        {
            StorageTransportMoveService.VehiclesPath,
            MortalItemIdentityState.StatePath,
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath
        });

        var issues = await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync();

        Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error &&
            issue.Code == "mortal_item_materialization_route_authority_mismatch");
        await context.AssertExactBytesAsync(before);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("string")]
    [InlineData("mixedScalar")]
    public async Task RawVehicleCargoAdmission_RejectsMalformedInventoryWithoutMutation(string shape)
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        var root = CargoBirth("new_cart");
        var vehicle = root["UpdateVehicles"]![0]!.AsObject();
        switch (shape)
        {
            case "missing":
                vehicle.Remove("inventory");
                break;
            case "null":
                vehicle["inventory"] = null;
                break;
            case "string":
                vehicle["inventory"] = "not an inventory array";
                break;
            case "mixedScalar":
                vehicle["inventory"]!.AsArray().Add(17);
                break;
            default:
                throw new InvalidOperationException("Unknown inventory-shape case.");
        }
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath, root);
        var before = await context.CaptureExactBytesAsync(new[]
        {
            StorageTransportMoveService.VehiclesPath,
            MortalItemIdentityState.StatePath,
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath
        });

        var issues = await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync();

        var expectedPath = StorageTransportMoveService.VehiclesPath +
            ".UpdateVehicles[0].inventory" + (shape == "mixedScalar" ? "[1]" : string.Empty);
        Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error &&
            issue.Code == "resource_owner_vehicle_birth_invalid" &&
            string.Equals(issue.FilePath, expectedPath, StringComparison.Ordinal));
        await context.AssertExactBytesAsync(before);
    }

    [Fact]
    public async Task CargoResourceAdmission_RejectsItemIndexDriftAfterItemAdmission()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath, CargoBirth("new_cart"));
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        var index = (await context.ReadJsonAsync(MortalItemIdentityState.StatePath))!.AsObject();
        index["unacceptedDrift"] = true;
        await context.WriteJsonAsync(MortalItemIdentityState.StatePath, index);
        var before = await context.CaptureExactBytesAsync(new[]
        {
            StorageTransportMoveService.VehiclesPath, MortalItemIdentityState.StatePath,
            ResourceMaterializationContract.StatePath
        });
        var issues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error &&
            issue.Code == "resource_owner_vehicle_cargo_projection_invalid");
        await context.AssertExactBytesAsync(before);
    }

    [Fact]
    public async Task CargoPublication_RejectsItemIndexDriftAfterResourceAdmissionBeforeWrites()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath, CargoBirth("new_cart"));
        Assert.DoesNotContain(await context.ValidateAcceptedTurnRawMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        var index = (await context.ReadJsonAsync(MortalItemIdentityState.StatePath))!.AsObject();
        index["unacceptedDrift"] = true;
        await context.WriteJsonAsync(MortalItemIdentityState.StatePath, index);
        var before = await context.CaptureExactBytesAsync(new[]
        {
            StorageTransportMoveService.VehiclesPath, MortalItemIdentityState.StatePath,
            ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath, CanonicalResourceOwnerAuthorityComposer.AuthorityPath
        });
        await Assert.ThrowsAnyAsync<InvalidDataException>(() => context.NormalizeAcceptedTurnWithIssuesAsync());
        await context.AssertExactBytesAsync(before);
    }

    private static JsonObject CargoBirth(string authorityId)
    {
        var root = BirthRoot();
        root["UpdateVehicles"]![0]!["inventory"]!.AsArray().Add(
            MortalItemTestFixture.CreateRawRoot(
                route: "new_vehicle_inventory", authorityKind: "new_vehicle", authorityId: authorityId,
                sourceTurn: 42, creationRef: "cargo_birth_item", materializationId: "cargo_birth_materialization"));
        return root;
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
