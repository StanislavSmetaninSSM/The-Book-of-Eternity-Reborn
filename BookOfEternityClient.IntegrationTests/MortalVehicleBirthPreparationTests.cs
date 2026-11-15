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

    [Theory]
    [InlineData("raw")]
    [InlineData("snapshot")]
    [InlineData("session")]
    [InlineData("invalidate")]
    [InlineData("generation")]
    [InlineData("before")]
    [InlineData("failed")]
    public async Task Preparation_ChangedBindingDoesNotReuseAllocation(string change)
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var before = new JsonObject { ["vehicles"] = new JsonArray() };
        var raw = BirthRoot();
        var first = AcceptedTurnAuthorityRegistry.PrepareMortalVehicleBirths(context.FileSystem, lease,
            "session-a", "snapshot-a", before, raw);
        Assert.True(first.IsValid);
        var same = AcceptedTurnAuthorityRegistry.PrepareMortalVehicleBirths(context.FileSystem, lease,
            "session-a", "snapshot-a", before, raw);
        Assert.Same(first, same);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        if (change == "raw") raw["UpdateVehicles"]![0]!["name"] = "Changed cart";
        if (change == "invalidate") AcceptedTurnAuthorityRegistry.InvalidateAcceptedTurnValidated(context.FileSystem, lease);
        if (change == "generation") context.FileSystem.RotateSessionGeneration(lease);
        if (change == "before")
        {
            var existing = new JsonObject { ["vehicleId"] = "vehicle_existing" };
            before["vehicles"]!.AsArray().Add(existing);
            raw["vehicles"]!.AsArray().Add(existing.DeepClone());
        }
        if (change == "failed")
        {
            raw["UpdateVehicles"]![0]!["capacity"] = -1;
            var failure = AcceptedTurnAuthorityRegistry.PrepareMortalVehicleBirths(context.FileSystem, lease,
                "session-a", "snapshot-a", before, raw);
            Assert.False(failure.IsValid);
            Assert.Null(failure.Preparation);
            raw["UpdateVehicles"]![0]!["capacity"] = 12;
        }
        var next = AcceptedTurnAuthorityRegistry.PrepareMortalVehicleBirths(context.FileSystem, lease,
            change == "session" ? "session-b" : "session-a",
            change == "snapshot" ? "snapshot-b" : "snapshot-a", before, raw);
        Assert.True(next.IsValid);
        Assert.True(first.Preparation!.TryResolve("new_cart", out var previousId));
        Assert.True(next.Preparation!.TryResolve("new_cart", out var nextId));
        Assert.NotEqual(previousId, nextId);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    [Fact]
    public void Preparation_RejectsAllMalformedBirthsBeforeAnyAllocation()
    {
        var raw = BirthRoot();
        var second = raw["UpdateVehicles"]![0]!.DeepClone().AsObject();
        second["vehicleRef"] = "another_cart";
        second.Remove("capacity");
        raw["UpdateVehicles"]!.AsArray().Add(second);
        var serialized = raw.ToJsonString();
        var factory = new CountingFactory();
        var result = MortalVehicleBirthPreparation.Prepare(new JsonObject { ["vehicles"] = new JsonArray() }, raw, factory);
        Assert.False(result.IsValid);
        Assert.Null(result.Preparation);
        Assert.Equal(0, factory.Calls);
        Assert.Equal(serialized, raw.ToJsonString());
    }

    private sealed class CountingFactory : VehicleIdentityFactory
    {
        internal int Calls { get; private set; }
        internal override string CreateVehicleId() => "vehicle_count_" + ++Calls;
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
