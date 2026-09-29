using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises ordinary vehicle owner allocation through the spiritual replay journal.
/// </summary>
public sealed class SpiritualWoundVehicleIdentityTests
{
    /// <summary>
    /// Reconstructs the actual companion image without invoking a new random allocator.
    /// </summary>
    [Fact]
    public void ActualOwnerReplaysExactVehicleImage()
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var first = Compose(new SpiritualWoundVehicleIdentityFactory(journal, new VehicleIdentityFactory()));
        Assert.True(first.IsValid, string.Join(Environment.NewLine, first.Issues));
        Assert.Equal("vehicle", Assert.Single(journal.Export())!["kind"]!.GetValue<string>());
        var rows = journal.Export().ToJsonString();
        var replay = SpiritualWoundReplayJournal.CreateReplay(rows);
        var second = Compose(new SpiritualWoundVehicleIdentityFactory(replay, new NoAllocationFactory()));
        Assert.True(second.IsValid, string.Join(Environment.NewLine, second.Issues));
        Assert.Equal(first.Authority!.Fingerprint, second.Authority!.Fingerprint);
        Assert.Equal(first.OwnerCompanionAfterImages[StorageTransportMoveService.VehiclesPath].ToJsonString(),
            second.OwnerCompanionAfterImages[StorageTransportMoveService.VehiclesPath].ToJsonString());
        Assert.Equal(rows, replay.Export().ToJsonString());
    }

    /// <summary>
    /// Rejects another admitted reference and permanently revokes the changed replay attempt.
    /// </summary>
    [Fact]
    public void ChangedReferenceRejectsReplay()
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        Assert.True(Compose(new SpiritualWoundVehicleIdentityFactory(journal, new VehicleIdentityFactory())).IsValid);
        var replay = SpiritualWoundReplayJournal.CreateReplay(journal.Export().ToJsonString());
        Assert.Throws<InvalidOperationException>(() => Compose(
            new SpiritualWoundVehicleIdentityFactory(replay, new NoAllocationFactory()), "vehicle_other"));
        Assert.Throws<InvalidOperationException>(() => replay.Export());
    }

    /// <summary>
    /// Prohibits allocations that lack an admitted causal reference.
    /// </summary>
    [Fact]
    public void UnboundCallRevokesJournal()
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var factory = new SpiritualWoundVehicleIdentityFactory(journal, new NoAllocationFactory());
        Assert.Throws<InvalidOperationException>(() => factory.CreateVehicleId());
        Assert.Throws<InvalidOperationException>(() => journal.Export());
    }

    /// <summary>
    /// Invokes the real composer with a valid same-turn proposal and an empty pre-turn vehicle collection.
    /// </summary>
    /// <param name="factory">
    /// Ordinary or journal-backed allocation policy supplied to the real owner.
    /// </param>
    /// <param name="reference">
    /// Exact same-turn vehicle reference, distinct from a permanent identity.
    /// </param>
    /// <returns>
    /// Actual owner composition result without filesystem or publication authority.
    /// </returns>
    private static ResourceOwnerCompositionResult Compose(VehicleIdentityFactory factory,
        string reference = "vehicle_ref_probe")
    {
        var before = new JsonObject { ["vehicles"] = new JsonArray() };
        var accepted = before.DeepClone().AsObject();
        accepted["UpdateVehicles"] = new JsonArray(new JsonObject
        {
            ["vehicleRef"] = reference, ["name"] = "Телега", ["availability"] = "Parked",
            ["currentLocationId"] = "location_market",
            ["resourceMaterialization"] = new JsonObject
            {
                ["resources"] = new JsonArray(new JsonObject
                {
                    ["resourceKey"] = "health", ["maximum"] = 140
                })
            }
        });
        return MortalResourceOwnerComposer.Compose(new MortalResourceOwnerCompositionInput(
            ResourceDefinitionCatalog.CreateBuiltIn(), Roots(before), Roots(accepted)),
            new CombatantIdentityFactory(), factory);
    }

    /// <summary>
    /// Supplies empty non-vehicle owner roots for the isolated ordinary composer.
    /// </summary>
    /// <param name="vehicles">
    /// Vehicle root retained by the detached composition input.
    /// </param>
    /// <returns>
    /// Complete minimal owner roots.
    /// </returns>
    private static MortalResourceOwnerRoots Roots(JsonObject vehicles) => new(
        new JsonObject { ["NPCsInScene"] = new JsonArray() },
        new JsonObject { ["enemiesData"] = new JsonArray() },
        new JsonObject { ["alliesData"] = new JsonArray() }, vehicles);

    /// <summary>
    /// Fails if strict replay invokes the ordinary allocation policy.
    /// </summary>
    private sealed class NoAllocationFactory : VehicleIdentityFactory
    {
        /// <inheritdoc/>
        internal override string CreateVehicleId() => throw new NotSupportedException("Replay allocated.");
    }
}
