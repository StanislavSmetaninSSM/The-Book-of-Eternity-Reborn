using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks retained location allocation through the accepted turn planner and its shared cache.
/// </summary>
public sealed class SpiritualWoundLocationIdentityTests
{
    /// <summary>
    /// Reuses one healthy attempt without duplicate rows and replays the same owner IDs in a new attempt.
    /// </summary>
    [Fact]
    public void LocationPlannerReplaysAcrossAttemptFactories()
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var next = 0;
        var underlying = new MortalLocationIdentityFactory(() =>
            Guid.Parse($"00000000-0000-0000-0000-{++next:x12}"));
        var factory = new SpiritualWoundLocationIdentityFactory(journal, underlying);
        var cache = new MortalLocationAcceptedTurnPlanCache();
        var input = CreateLocationInput();

        var first = cache.GetOrBuild(input, factory);
        Assert.True(first.Success);
        Assert.Same(first, cache.GetOrBuild(input, factory));
        Assert.Equal(2, journal.Export().Count);
        Assert.StartsWith("loc_", first.Plan!.LocationIdsByInitialId[MortalLocationTestFixture.LocationInitialId]);

        var replay = SpiritualWoundReplayJournal.CreateReplay(journal.Export().ToJsonString());
        var replayFactory = new SpiritualWoundLocationIdentityFactory(
            replay,
            new MortalLocationIdentityFactory(() => throw new InvalidOperationException("Replay allocated.")));
        var second = cache.GetOrBuild(input, replayFactory);

        Assert.True(second.Success);
        Assert.NotSame(first, second);
        Assert.Equal(first.Plan.LocationIdsByInitialId[MortalLocationTestFixture.LocationInitialId],
            second.Plan!.LocationIdsByInitialId[MortalLocationTestFixture.LocationInitialId]);
        Assert.Equal(first.Plan.FinalIdentityIndex.ToJsonString(), second.Plan.FinalIdentityIndex.ToJsonString());
        Assert.Equal(2, replay.Export().Count);
    }

    /// <summary>
    /// Rejects a changed admitted materialization coordinate during strict replay.
    /// </summary>
    [Fact]
    public void LocationPlannerRejectsChangedCausalMaterialization()
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var factory = new SpiritualWoundLocationIdentityFactory(
            journal,
            new MortalLocationIdentityFactory(() => Guid.Parse("00000000-0000-0000-0000-000000000001")));
        var cache = new MortalLocationAcceptedTurnPlanCache();
        var input = CreateLocationInput();
        Assert.True(cache.GetOrBuild(input, factory).Success);

        var replay = SpiritualWoundReplayJournal.CreateReplay(journal.Export().ToJsonString());
        var replayFactory = new SpiritualWoundLocationIdentityFactory(
            replay,
            new MortalLocationIdentityFactory(() => throw new InvalidOperationException("Replay allocated.")));
        var changed = CreateLocationInput();
        changed.RawWorldMapUpdates!["newLocations"]![0]!["materialization"]!["materializationId"] =
            "mlocmat_changed";

        Assert.Throws<InvalidOperationException>(() => cache.GetOrBuild(changed, replayFactory));
        Assert.False(replayFactory.IsHealthy);
        Assert.False(replay.IsHealthy);
    }

    /// <summary>
    /// Rejects a faulted attempt even when the requested plan is already cached.
    /// </summary>
    [Fact]
    public void LocationCacheRejectsFaultedFactoryOnHit()
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var factory = new SpiritualWoundLocationIdentityFactory(
            journal,
            new MortalLocationIdentityFactory(() => Guid.NewGuid()));
        var cache = new MortalLocationAcceptedTurnPlanCache();
        var input = CreateLocationInput();
        Assert.True(cache.GetOrBuild(input, factory).Success);

        Assert.Throws<InvalidOperationException>(() => factory.CreateLocationId());
        Assert.False(factory.IsHealthy);
        Assert.Throws<InvalidOperationException>(() => cache.GetOrBuild(input, factory));
    }

    /// <summary>
    /// Creates one valid raw location admission for a pure planning attempt.
    /// </summary>
    /// <returns>
    /// Detached input with one world map creation and empty canonical baselines.
    /// </returns>
    private static MortalLocationAcceptedTurnInput CreateLocationInput() => new(
        MortalLocationTestFixture.CreateWorldMap(),
        null,
        MortalLocationIdentityState.CreateEmptyRoot(),
        null,
        new JsonObject
        {
            ["newLocations"] = new JsonArray(MortalLocationTestFixture.CreateRawLocation())
        },
        42);
}
