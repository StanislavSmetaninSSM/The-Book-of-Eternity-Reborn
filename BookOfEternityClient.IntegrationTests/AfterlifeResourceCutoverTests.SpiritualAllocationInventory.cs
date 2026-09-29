using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Proves the signed spiritual capture reaches ordinary vehicle allocation before any publication.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualCapture_AllocationInventoryIncludesOrdinaryVehicleCreation()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await context.WriteExactJsonAsync(StorageTransportMoveService.VehiclesPath, new JsonObject
        {
            ["vehicles"] = new JsonArray(),
            ["UpdateVehicles"] = new JsonArray(new JsonObject
            {
                ["vehicleRef"] = "vehicle_ref_inventory_probe",
                ["name"] = "Телега",
                ["availability"] = "Parked",
                ["currentLocationId"] = "location_market",
                ["resourceMaterialization"] = new JsonObject
                {
                    ["resources"] = new JsonArray(new JsonObject
                    {
                        ["resourceKey"] = "health", ["maximum"] = 140
                    })
                }
            })
        }.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await InvokeOriginalCaptureAsync(context.Validator, "CaptureSpiritualOriginalTurnAsync", lease);
        AssertNoConflictFrameErrors(OriginalCaptureIssues(captured));
        var capture = OriginalCaptureProperty(captured, "Capture")!;
        var input = Assert.IsType<AcceptedMechanicsInput>(OriginalCaptureField(capture, "_input"));
        var vehicles = input.PlanningContext!.OwnerCompanionAfterImages[StorageTransportMoveService.VehiclesPath];
        var vehicle = Assert.Single(vehicles["vehicles"]!.AsArray())!;
        var identity = vehicle["vehicleId"]!.GetValue<string>();
        Assert.StartsWith("vehicle_", identity);
        Assert.True(Guid.TryParseExact(identity["vehicle_".Length..], "N", out _));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }
}
