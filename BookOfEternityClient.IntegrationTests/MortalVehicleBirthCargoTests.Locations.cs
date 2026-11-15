using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalVehicleBirthCargoTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BirthLocation_RejectsUnknownParkedTarget(bool cargo)
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        var root = ParkedBirth(cargo, "missing_location");
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath, root);
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        var before = await context.CaptureExactBytesAsync(new[]
        { StorageTransportMoveService.VehiclesPath, ResourceMaterializationContract.StatePath,
          ResourceMaterializationContract.HistoryPath, MortalItemIdentityState.StatePath });

        var issues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error &&
            issue.Code == "resource_owner_vehicle_birth_location_unknown" &&
            issue.FilePath == StorageTransportMoveService.VehiclesPath + ".UpdateVehicles[0].currentLocationId");
        await context.AssertExactBytesAsync(before);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task BirthLocation_PublishesExactCanonicalOrSameTurnTarget(bool cargo, bool sameTurn)
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await ArrangeVehicleLocationBaseline(context, sameTurn);
        var root = ParkedBirth(cargo, sameTurn
            ? MortalLocationTestFixture.LocationInitialId : MortalLocationTestFixture.LocationId);
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath, root);
        Assert.DoesNotContain(await context.ValidateAcceptedTurnRawMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);

        await context.NormalizeAcceptedTurnAsync();

        var map = (await context.ReadJsonAsync(MortalLocationMaterializationContract.WorldMapPath))!;
        var acceptedLocation = Assert.Single(map["locations"]!.AsArray().OfType<JsonObject>(),
            location => location["materializationReceipt"]?["initialId"]?.GetValue<string>() ==
                MortalLocationTestFixture.LocationInitialId);
        var permanentId = acceptedLocation["locationId"]!.GetValue<string>();
        Assert.NotEqual(MortalLocationTestFixture.LocationInitialId, permanentId);
        var vehicles = (await context.ReadJsonAsync(StorageTransportMoveService.VehiclesPath))!;
        Assert.Equal(permanentId, Assert.Single(vehicles["vehicles"]!.AsArray())!["currentLocationId"]!.GetValue<string>());
        Assert.NotNull(acceptedLocation["materializationReceipt"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BirthLocation_RejectsMapDriftBeforePublication(bool cargo)
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await ArrangeVehicleLocationBaseline(context, sameTurn: false);
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath,
            ParkedBirth(cargo, MortalLocationTestFixture.LocationId));
        Assert.DoesNotContain(await context.ValidateAcceptedTurnRawMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        var map = (await context.ReadJsonAsync(MortalLocationMaterializationContract.WorldMapPath))!;
        map["locations"]![0]!["name"] = "Changed after admission";
        await context.WriteJsonAsync(MortalLocationMaterializationContract.WorldMapPath, map);
        var before = await context.CaptureExactBytesAsync(new[]
        { StorageTransportMoveService.VehiclesPath, MortalLocationMaterializationContract.WorldMapPath,
          ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
          MortalItemIdentityState.StatePath });

        await Assert.ThrowsAnyAsync<InvalidDataException>(() => context.NormalizeAcceptedTurnWithIssuesAsync());

        await context.AssertExactBytesAsync(before);
    }

    private static JsonObject ParkedBirth(bool cargo, string locationId)
    {
        var root = cargo ? CargoBirth("new_cart") : BirthRoot();
        root.Remove("activeVehicleChange");
        root["UpdateVehicles"]![0]!["availability"] = "Parked";
        root["UpdateVehicles"]![0]!["currentLocationId"] = locationId;
        return root;
    }

    private static async Task ArrangeVehicleLocationBaseline(
        MortalItemMaterializationTestContext context, bool sameTurn)
    {
        await context.ArrangeEmptyMortalTurnAsync();
        var canonical = sameTurn
            ? MortalLocationTestFixture.CreateCanonicalLocationWithIdentity("location_anchor", "Anchor")
            : MortalLocationTestFixture.CreateCanonicalLocation();
        var map = MortalLocationTestFixture.CreateWorldMap(canonical);
        await context.WriteJsonAsync(MortalLocationMaterializationContract.CurrentLocationPath,
            MortalLocationTestFixture.CreateCurrentProjection(canonical));
        await context.WriteJsonAsync(MortalLocationMaterializationContract.WorldMapPath, map);
        await context.WriteJsonAsync(MortalLocationIdentityState.StatePath, MortalLocationTestFixture.CreateIdentityIndex(canonical));
        var access = MortalStorageAccessProjection.Build(map, null);
        Assert.True(access.IsValid);
        await context.WriteJsonAsync(MortalStorageAccessProjection.StatePath, access.Root!);
        await context.CaptureValidatedPendingSnapshotAsync();
        if (sameTurn)
        {
            map["worldMapUpdates"] = new JsonObject
            {
                ["newLocations"] = new JsonArray(MortalLocationTestFixture.CreateRawLocation()),
                ["newLinks"] = new JsonArray()
            };
            await context.WriteJsonAsync(MortalLocationMaterializationContract.WorldMapPath, map);
        }
    }
}
