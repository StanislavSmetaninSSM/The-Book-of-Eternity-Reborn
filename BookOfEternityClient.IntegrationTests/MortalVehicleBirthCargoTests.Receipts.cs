using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalVehicleBirthCargoTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BirthReceipt_PublishesCompleteResolvedVehicleAndSourceEvidence(bool cargo)
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath,
            cargo ? CargoBirth("new_cart") : BirthRoot());
        Assert.DoesNotContain(await context.ValidateAcceptedTurnRawMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);

        await context.NormalizeAcceptedTurnAsync();

        var root = (await context.ReadJsonAsync(StorageTransportMoveService.VehiclesPath))!;
        var vehicle = Assert.Single(root["vehicles"]!.AsArray())!.AsObject();
        var receipts = Assert.IsType<JsonArray>(root["birthReceipts"]);
        var receipt = Assert.IsType<JsonObject>(Assert.Single(receipts));
        Assert.Equal(1, receipt["schemaVersion"]!.GetValue<int>());
        Assert.Equal(vehicle["vehicleId"]!.GetValue<string>(), receipt["vehicleId"]!.GetValue<string>());
        Assert.Equal("new_cart", receipt["vehicleRef"]!.GetValue<string>());
        Assert.Equal(42, receipt["sourceTurn"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(vehicle, receipt["birthState"]));
        foreach (var field in new[] { "sessionId", "requestId", "snapshotToken" })
            Assert.False(string.IsNullOrWhiteSpace(receipt["sourceAuthority"]![field]!.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(receipt["seal"]!.GetValue<string>()));
        var health = Assert.Single(receipt["resourceInitialization"]!.AsArray())!;
        Assert.Equal("health", health["resourceKey"]!.GetValue<string>());
        Assert.Equal(100, health["maximum"]!.GetValue<int>());
        Assert.False(receipt["birthState"]!.AsObject().ContainsKey("vehicleRef"));
        Assert.False(receipt["birthState"]!.AsObject().ContainsKey("resourceMaterialization"));
        Assert.Equal(cargo ? 1 : 0, receipt["birthState"]!["inventory"]!.AsArray().Count);
    }
}
