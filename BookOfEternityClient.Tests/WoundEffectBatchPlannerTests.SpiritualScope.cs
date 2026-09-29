using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundEffectBatchPlannerTests
{
    /// <summary>
    /// Replays actual prepared wound construction through a new allocation scope on the same cache.
    /// </summary>
    [Fact]
    public void SpiritualScope_PreparedWoundCacheSeparatesAllocationFactories()
    {
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(CreateInput()));
        var input = CreateEffectInput(prepared);
        var cache = new EffectAcceptedTurnPlanCache();
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var factory = new SpiritualWoundEffectIdentityFactory(journal, new EffectIdentityFactory());
        var first = cache.GetOrBuildWoundValidated(prepared, input, out var reused, factory);
        Assert.True(first.Success, string.Join(Environment.NewLine, first.Issues));
        Assert.False(reused);
        var rows = journal.Export().ToJsonString();
        Assert.NotEqual("[]", rows);
        Assert.Same(first, cache.GetOrBuildWoundValidated(prepared, input, out reused, factory));
        Assert.True(reused);
        Assert.Equal(rows, journal.Export().ToJsonString());
        var replay = SpiritualWoundReplayJournal.CreateReplay(rows);
        var second = cache.GetOrBuildWoundValidated(prepared, input, out reused,
            new SpiritualWoundEffectIdentityFactory(replay, new EffectIdentityFactory()));
        Assert.False(reused);
        Assert.NotSame(first, second);
        Assert.True(second.Success, string.Join(Environment.NewLine, second.Issues));
        Assert.Equal(rows, replay.Export().ToJsonString());
        Assert.Equal(first.Plan!.InputFingerprint, second.Plan!.InputFingerprint);
        Assert.Equal(first.Plan.IdentityIndexAfterImage.ToJsonString(), second.Plan.IdentityIndexAfterImage.ToJsonString());
    }
}
