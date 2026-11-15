using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalVehicleBirthCargoTests
{
    [Fact]
    public async Task BirthAccess_RestrictedActiveIsRejectedBeforePublication()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        var root = BirthRoot();
        root["UpdateVehicles"]![0]!["hasFullAccess"] = false;
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath, root);
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        var before = await context.CaptureExactBytesAsync(new[]
        { StorageTransportMoveService.VehiclesPath, ResourceMaterializationContract.StatePath,
          ResourceMaterializationContract.HistoryPath, MortalItemIdentityState.StatePath });

        var issues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error &&
            issue.Code == "resource_owner_vehicle_active_selection_invalid");
        await context.AssertExactBytesAsync(before);
    }

    [Fact]
    public async Task BirthAccess_RestrictedParkedPublishesWithoutMoveAccess()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await ArrangeVehicleLocationBaseline(context, sameTurn: false);
        var root = ParkedBirth(false, MortalLocationTestFixture.LocationId);
        root["UpdateVehicles"]![0]!["hasFullAccess"] = false;
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath, root);
        Assert.DoesNotContain(await context.ValidateAcceptedTurnRawMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);

        await context.NormalizeAcceptedTurnAsync();

        var accepted = (await context.ReadJsonAsync(StorageTransportMoveService.VehiclesPath))!;
        var vehicle = Assert.Single(accepted["vehicles"]!.AsArray())!;
        Assert.False(vehicle["hasFullAccess"]!.GetValue<bool>());
        Assert.Null(accepted["activeVehicleId"]);
        var move = await StorageTransportMoveService.ReadVehicleMoveContextAsync(context.FileSystem);
        Assert.True(move.Success, move.Message);
        Assert.Empty(move.Vehicles);
    }
}
