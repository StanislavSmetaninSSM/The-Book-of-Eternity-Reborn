using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises replay and scoped export through the actual accepted item cache.
/// </summary>
public sealed class SpiritualWoundItemIdentityTests
{
    /// <summary>
    /// Treats an irreversibly revoked spiritual registration as vacant without changing its retained fence or evidence.
    /// </summary>
    [Fact]
    public void VacancyProbe_RevokedSpiritualFactoryIsReadOnlyAndCannotExposeOldOwners()
    {
        var cache = new MortalItemAcceptedTurnAuthority.Cache();
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var factory = new SpiritualWoundItemIdentityFactory(journal, new MortalItemIdentityFactory());
        Register(cache, factory);
        Assert.Single(cache.GetOwners("session", "snapshot", factory));
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var fenceField = typeof(MortalItemAcceptedTurnAuthority.Cache).GetField("_validatedFence", flags)!;
        var factoryField = typeof(MortalItemAcceptedTurnAuthority.Cache).GetField("_identityFactory", flags)!;
        var validatedField = typeof(MortalItemAcceptedTurnAuthority.Cache).GetField("_validated", flags)!;
        var fence = fenceField.GetValue(cache);
        var vacancyProbe = new MortalItemIdentityFactory();
        Assert.False(cache.CanBeginSpiritualOriginalIntake(vacancyProbe));

        journal.Invalidate();

        Assert.False(factory.IsHealthy);
        Assert.True(cache.CanBeginSpiritualOriginalIntake(vacancyProbe));
        Assert.True(cache.CanBeginSpiritualOriginalIntake(vacancyProbe));
        Assert.Same(fence, fenceField.GetValue(cache));
        Assert.Same(factory, factoryField.GetValue(cache));
        Assert.True(Assert.IsType<bool>(validatedField.GetValue(cache)));
        Assert.Empty(cache.GetOwners("session", "snapshot", factory));
        Assert.Empty(cache.GetSources("session", "snapshot", factory));
        Assert.False(cache.TryCaptureNormalizationSnapshot("session", "snapshot", 42, out _));
        Assert.Throws<InvalidOperationException>(() => Register(cache, factory));
    }

    /// <summary>
    /// Preserves an unrelated custom factory whose temporary health failure does not permanently revoke its ownership.
    /// </summary>
    [Fact]
    public void VacancyProbe_UnhealthyCustomFactoryStillBlocksAndCanRecoverItsOwner()
    {
        var cache = new MortalItemAcceptedTurnAuthority.Cache();
        var factory = new RecoverableHealthFactory();
        Register(cache, factory);
        var owner = Assert.Single(cache.GetOwners("session", "snapshot", factory));
        var vacancyProbe = new MortalItemIdentityFactory();
        Assert.False(cache.CanBeginSpiritualOriginalIntake(vacancyProbe));

        factory.Healthy = false;

        Assert.False(cache.CanBeginSpiritualOriginalIntake(vacancyProbe));
        Assert.Empty(cache.GetOwners("session", "snapshot", factory));
        factory.Healthy = true;
        Assert.Equal(owner.ItemId, Assert.Single(cache.GetOwners("session", "snapshot", factory)).ItemId);
        Assert.False(cache.CanBeginSpiritualOriginalIntake(vacancyProbe));
    }

    /// <summary>
    /// Replays real owner identities on the same cache while isolating ordinary consumers.
    /// </summary>
    [Fact]
    public void ActualItemOwnerReplaysAndRequiresExpectedFactory()
    {
        var cache = new MortalItemAcceptedTurnAuthority.Cache();
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var factory = new SpiritualWoundItemIdentityFactory(journal, new MortalItemIdentityFactory());
        Register(cache, factory);
        var first = Assert.Single(cache.GetOwners("session", "snapshot", factory));
        Assert.StartsWith("itm_", first.ItemId);
        Assert.Empty(cache.GetOwners("session", "snapshot"));
        Assert.Empty(cache.GetSources("session", "snapshot"));
        Assert.False(cache.HasValidated);
        Assert.False(cache.TryCaptureNormalizationSnapshot("session", "snapshot", 42, out _));
        var rows = journal.Export().ToJsonString();
        Assert.Single(journal.Export());
        Register(cache, factory);
        Assert.Equal(rows, journal.Export().ToJsonString());
        var replay = SpiritualWoundReplayJournal.CreateReplay(rows);
        var next = new SpiritualWoundItemIdentityFactory(replay, new NoAllocationFactory());
        Register(cache, next);
        Assert.Equal(first.ItemId, Assert.Single(cache.GetOwners("session", "snapshot", next)).ItemId);
        Assert.Empty(cache.GetOwners("session", "snapshot", factory));
        Assert.Equal(rows, replay.Export().ToJsonString());
        journal.Invalidate();
        cache.InvalidateValidated(factory);
        Assert.Single(cache.GetOwners("session", "snapshot", next));
        replay.Invalidate();
        Assert.Empty(cache.GetOwners("session", "snapshot", next));
        Assert.Empty(cache.GetSources("session", "snapshot", next));
        Assert.Throws<InvalidOperationException>(() => Register(cache, next));
    }

    /// <summary>
    /// Rejects changed source, request or turn even when the ordinary item fingerprint is unchanged.
    /// </summary>
    /// <param name="change">
    /// Causal coordinate to change without replacing the candidate payload.
    /// </param>
    [Theory]
    [InlineData("route")]
    [InlineData("source")]
    [InlineData("request")]
    [InlineData("turn")]
    public void SameAttemptCacheHitCannotSkipChangedCause(string change)
    {
        var cache = new MortalItemAcceptedTurnAuthority.Cache();
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        Register(cache, new SpiritualWoundItemIdentityFactory(journal, new MortalItemIdentityFactory()));
        var replay = SpiritualWoundReplayJournal.CreateReplay(journal.Export().ToJsonString());
        var factory = new SpiritualWoundItemIdentityFactory(replay, new NoAllocationFactory());
        Register(cache, factory);
        Assert.Throws<InvalidOperationException>(() => Register(cache, factory, change));
        Assert.False(replay.IsHealthy);
        Assert.Empty(cache.GetOwners("session", "snapshot", factory));
    }

    /// <summary>
    /// Journals the successful collision retry once and refuses a retained collision on replay.
    /// </summary>
    [Fact]
    public void CollisionRetriesStayInsideOneJournalRequest()
    {
        var key = Key();
        const string occupied = "itm_0123456789abcdef0123456789abcdef";
        const string free = "itm_1123456789abcdef0123456789abcdef";
        var values = new Queue<string>(new[] { occupied, free });
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var factory = new SpiritualWoundItemIdentityFactory(journal, new SequenceFactory(values));
        Assert.Equal(free, factory.CreateItemId(key, new HashSet<string> { occupied }));
        Assert.Single(journal.Export());
        Assert.Empty(values);
        var replay = SpiritualWoundReplayJournal.CreateReplay(journal.Export().ToJsonString());
        Assert.Throws<InvalidOperationException>(() => new SpiritualWoundItemIdentityFactory(
            replay, new NoAllocationFactory()).CreateItemId(key, new HashSet<string> { free }));
        Assert.False(replay.IsHealthy);
    }

    /// <summary>
    /// Prevents an overridable allocator from mutating the owner's causal lists or collision inventory.
    /// </summary>
    [Fact]
    public void AllocationCallbackReceivesFrozenDetachedEvidence()
    {
        var key = Key();
        var path = new[] { "container_original" };
        var sources = new[] { "itm_source_original" };
        var destination = new[] { "destination_original" };
        key = key with { Carrier = key.Carrier with { ContainerPath = path },
            Route = key.Route with { SourceItemIds = sources,
                Destination = key.Route.Destination with { ContainerPath = destination } } };
        var known = new HashSet<string> { "itm_original" };
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var factory = new SpiritualWoundItemIdentityFactory(journal, new MutatingFactory());
        factory.CreateItemId(key, known);
        Assert.Equal("container_original", Assert.Single(path));
        Assert.Equal("itm_source_original", Assert.Single(sources));
        Assert.Equal("destination_original", Assert.Single(destination));
        Assert.Equal("itm_original", Assert.Single(known));
    }

    /// <summary>
    /// Registers one admitted new item through the real cache with independent source binding.
    /// </summary>
    /// <param name="cache">
    /// Cache whose identity reuse and export fences are exercised.
    /// </param>
    /// <param name="factory">
    /// Exact attempt factory expected by private consumers.
    /// </param>
    /// <param name="change">
    /// Optional causal coordinate to alter while retaining the ordinary candidate fingerprint.
    /// </param>
    private static void Register(MortalItemAcceptedTurnAuthority.Cache cache,
        MortalItemIdentityFactory factory, string? change = null)
    {
        var key = Key();
        var route = key.Route with
        {
            AuthorityId = change == "route" ? "other" : key.Route.AuthorityId,
            SourceItemIds = change == "source" ? new[] { "itm_source_other" } : key.Route.SourceItemIds
        };
        cache.Register("session", "snapshot", "same-candidate-fingerprint",
            new[] { new MortalItemAcceptedTurnAuthority.NewCandidate(key.CreationRef,
                MortalItemTestFixture.CreateRawRoot(), key.FilePath, key.JsonPath, key.Carrier, true, false) },
            Array.Empty<MortalItemAcceptedTurnAuthority.StableCandidate>(), Array.Empty<string>(),
            new Dictionary<string, MortalItemRouteAuthority> { [key.CreationRef] = route }, null,
            new Dictionary<string, JsonNode?>(), new Dictionary<string, JsonNode?>(), factory,
            change == "request" ? "other" : key.RequestId, change == "turn" ? 43 : key.Turn);
    }

    /// <summary>
    /// Creates the exact admitted cause shared by the pure owner tests.
    /// </summary>
    /// <returns>
    /// A detached typed item allocation coordinate.
    /// </returns>
    private static MortalItemAllocationKey Key()
    {
        var carrier = new MortalItemCarrierCoordinate("player_inventory", "player", null, Array.Empty<string>());
        return new("session", "snapshot", "request", 42, "new_item_test", 42,
            "game_state/player/items.json", "items.UpdateInventory[0]", carrier,
            new MortalItemRouteAuthority("player_acquisition", "turn_outcome", "turn_42", carrier, Array.Empty<string>()));
    }

    /// <summary>
    /// Fails whenever strict replay calls the ordinary random allocator.
    /// </summary>
    private sealed class NoAllocationFactory : MortalItemIdentityFactory
    {
        /// <inheritdoc/>
        internal override string CreateItemId() => throw new InvalidOperationException("Replay allocated.");
    }

    /// <summary>
    /// Supplies a deterministic ordinary collision sequence.
    /// </summary>
    /// <param name="values">
    /// Remaining allocator values consumed in order.
    /// </param>
    private sealed class SequenceFactory(Queue<string> values) : MortalItemIdentityFactory
    {
        /// <inheritdoc/>
        internal override string CreateItemId() => values.Dequeue();
    }

    /// <summary>
    /// Models a custom allocator whose health can recover without replacing its accepted registration.
    /// </summary>
    private sealed class RecoverableHealthFactory : MortalItemIdentityFactory
    {
        /// <summary>
        /// Gets or sets the current health returned to the accepted item cache.
        /// </summary>
        internal bool Healthy { get; set; } = true;

        /// <inheritdoc/>
        internal override bool IsHealthy => Healthy;
    }

    /// <summary>
    /// Attempts mutation through collection interfaces exposed to an ordinary allocator override.
    /// </summary>
    private sealed class MutatingFactory : MortalItemIdentityFactory
    {
        /// <inheritdoc/>
        internal override string CreateItemId(MortalItemAllocationKey? key, IReadOnlySet<string> knownIds)
        {
            foreach (var values in new[] { key!.Carrier.ContainerPath,
                         key.Route.SourceItemIds, key.Route.Destination.ContainerPath })
            {
                if (values is not IList<string> list) continue;
                try { list[0] = "changed"; }
                catch (NotSupportedException) { }
            }
            if (knownIds is ISet<string> mutable)
            {
                try { mutable.Clear(); }
                catch (NotSupportedException) { }
            }
            return "itm_1123456789abcdef0123456789abcdef";
        }
    }
}
