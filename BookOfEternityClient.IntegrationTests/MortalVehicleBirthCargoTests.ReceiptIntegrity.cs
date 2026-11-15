using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalVehicleBirthCargoTests
{
    [Theory]
    [InlineData("rewrite")]
    [InlineData("delete")]
    [InlineData("inject")]
    public async Task BirthReceipt_RawMutationRefusesWithoutAnyPublication(string mutation)
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath, BirthRoot());
        Assert.DoesNotContain(await context.ValidateAcceptedTurnRawMaterializationAsync(), i => i.Severity == IssueSeverity.Error);
        await context.NormalizeAcceptedTurnAsync();
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var root = (await context.ReadJsonAsync(StorageTransportMoveService.VehiclesPath))!.AsObject();
        var receipts = root["birthReceipts"]!.AsArray();
        switch (mutation)
        {
            case "rewrite":
                receipts[0]!["sourceAuthority"]!["sessionId"] = "rewritten_session";
                receipts[0]!["seal"] = MortalVehicleBirthReceiptState.ComputeSeal(receipts[0]!.AsObject());
                break;
            case "delete": receipts.Clear(); break;
            case "inject": receipts.Add(receipts[0]!.DeepClone()); break;
        }
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath, root);
        var before = await context.CaptureExactBytesAsync(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);

        var issues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(issues, i => i.Severity == IssueSeverity.Error && i.Code == "resource_owner_vehicle_receipt_invalid");
        Assert.Empty(ResourceRepairPacketBuilder.Build(issues));
        Assert.True(ResourceRepairPacketBuilder.RequiresFailClosedRollback(issues));
        await context.AssertExactBytesAsync(before);
    }

    [Fact]
    public async Task BirthReceipt_LaterPartialUpdatePreservesExactCreationFacts()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath, BirthRoot());
        Assert.DoesNotContain(await context.ValidateAcceptedTurnRawMaterializationAsync(), i => i.Severity == IssueSeverity.Error);
        await context.NormalizeAcceptedTurnAsync();
        var root = (await context.ReadJsonAsync(StorageTransportMoveService.VehiclesPath))!.AsObject();
        var receipt = Assert.Single(root["birthReceipts"]!.AsArray())!.DeepClone();
        var id = root["vehicles"]![0]!["vehicleId"]!.GetValue<string>();
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        root["UpdateVehicles"] = new JsonArray(new JsonObject { ["vehicleId"] = id, ["name"] = "Renamed cart" });
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath, root);
        Assert.DoesNotContain(await context.ValidateAcceptedTurnRawMaterializationAsync(), i => i.Severity == IssueSeverity.Error);
        await context.NormalizeAcceptedTurnAsync();
        var after = (await context.ReadJsonAsync(StorageTransportMoveService.VehiclesPath))!.AsObject();
        Assert.Equal("Renamed cart", after["vehicles"]![0]!["name"]!.GetValue<string>());
        Assert.Equal("Cart", receipt["birthState"]!["name"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(receipt, Assert.Single(after["birthReceipts"]!.AsArray())));
    }

    [Fact]
    public async Task BirthReceipt_ExistingCargoMoveConsumerPreservesHistoryAndIdentity()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath, CargoBirth("new_cart"));
        Assert.DoesNotContain(await context.ValidateAcceptedTurnRawMaterializationAsync(), i => i.Severity == IssueSeverity.Error);
        await context.NormalizeAcceptedTurnAsync();
        var original = (await context.ReadJsonAsync(StorageTransportMoveService.VehiclesPath))!.AsObject();
        var receipt = original["birthReceipts"]!.DeepClone();
        var itemId = original["vehicles"]![0]!["inventory"]![0]!["itemId"]!.GetValue<string>();
        var options = await StorageTransportMoveService.ReadVehicleMoveContextAsync(context.FileSystem);
        Assert.True(options.Success, options.Message);
        var vehicle = Assert.Single(options.Vehicles);
        var item = Assert.Single(vehicle.Contents);
        var retrieve = await StorageTransportMoveService.MoveVehicleItemAsync(context.FileSystem,
            StorageTransportMoveService.DirectionRetrieve, vehicle.Key, item.Key);
        Assert.True(retrieve.Success, retrieve.Message);
        var after = (await context.ReadJsonAsync(StorageTransportMoveService.VehiclesPath))!.AsObject();
        Assert.Empty(after["vehicles"]![0]!["inventory"]!.AsArray());
        Assert.True(JsonNode.DeepEquals(receipt, after["birthReceipts"]));
        Assert.Empty(MortalVehicleBirthReceiptState.ValidateLocal(after));
        var inventory = await context.ReadJsonAsync(InventoryEquipmentService.ItemsPath);
        Assert.Contains(itemId, inventory!.ToJsonString(), StringComparison.Ordinal);
        var fresh = await StorageTransportMoveService.ReadVehicleMoveContextAsync(context.FileSystem);
        var carried = Assert.Single(fresh.InventoryItems, candidate => candidate.Identity == itemId);
        var deposit = await StorageTransportMoveService.MoveVehicleItemAsync(context.FileSystem,
            StorageTransportMoveService.DirectionDeposit, vehicle.Key, carried.Key);
        Assert.True(deposit.Success, deposit.Message);
        var returned = (await context.ReadJsonAsync(StorageTransportMoveService.VehiclesPath))!.AsObject();
        Assert.Equal(itemId, Assert.Single(returned["vehicles"]![0]!["inventory"]!.AsArray())!["itemId"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(receipt, returned["birthReceipts"]));
    }

    [Fact]
    public async Task BirthReceipt_ReplayedSourceAndNormalizationDoNotDuplicateIdentityOrResources()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath, CargoBirth("new_cart"));
        Assert.DoesNotContain(await context.ValidateAcceptedTurnRawMaterializationAsync(), i => i.Severity == IssueSeverity.Error);
        var first = await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(context.FileSystem);
        Assert.NotNull(first);
        var firstPlan = Assert.IsType<AcceptedMechanicsPlan>(first.Result.Plan);
        var id = Assert.Single(firstPlan.OwnerAuthority.Entries.Values, e => e.Key.OwnerKind == ResourceOwnerKind.Vehicle).Key.ResourceOwnerId;
        Assert.DoesNotContain(await context.ValidateAcceptedTurnRawMaterializationAsync(), i => i.Severity == IssueSeverity.Error);
        var replay = await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(context.FileSystem);
        Assert.NotNull(replay);
        var replayPlan = Assert.IsType<AcceptedMechanicsPlan>(replay.Result.Plan);
        Assert.Equal(id, Assert.Single(replayPlan.OwnerAuthority.Entries.Values, e => e.Key.OwnerKind == ResourceOwnerKind.Vehicle).Key.ResourceOwnerId);
        await context.NormalizeAcceptedTurnAsync();
        var before = await context.CaptureExactBytesAsync(new[] { StorageTransportMoveService.VehiclesPath,
            MortalItemIdentityState.StatePath, ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath });
        await context.NormalizeAcceptedTurnAsync();
        await context.AssertExactBytesAsync(before);
        var root = (await context.ReadJsonAsync(StorageTransportMoveService.VehiclesPath))!.AsObject();
        Assert.Equal(id, Assert.Single(root["vehicles"]!.AsArray())!["vehicleId"]!.GetValue<string>());
        Assert.Single(root["birthReceipts"]!.AsArray());
    }
}
