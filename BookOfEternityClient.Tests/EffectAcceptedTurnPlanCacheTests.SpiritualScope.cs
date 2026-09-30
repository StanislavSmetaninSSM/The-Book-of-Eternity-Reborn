using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectAcceptedTurnPlanCacheTests
{
    /// <summary>
    /// Requires fresh allocation scopes to replay actual construction instead of borrowing another scope's cache hit.
    /// </summary>
    [Fact]
    public void SpiritualScope_FreshReplayRebuildsWhileSameFactoryReuses()
    {
        var input = CreateSkillScopeInput("exact");
        var cache = new EffectAcceptedTurnPlanCache();
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var factory = new SpiritualWoundEffectIdentityFactory(journal, new EffectIdentityFactory());
        var first = cache.GetOrBuildValidated(input, out var reused, factory);
        Assert.True(first.Success, string.Join(Environment.NewLine, first.Issues));
        Assert.False(reused);
        var rows = journal.Export().ToJsonString();
        Assert.NotEqual("[]", rows);
        Assert.Same(first, cache.GetOrBuildValidated(input, out reused, factory));
        Assert.True(reused);
        Assert.Equal(rows, journal.Export().ToJsonString());

        var replay = SpiritualWoundReplayJournal.CreateReplay(rows);
        var replayFactory = new SpiritualWoundEffectIdentityFactory(replay, new EffectIdentityFactory());
        var second = cache.GetOrBuildValidated(input, out reused, replayFactory);
        Assert.False(reused);
        Assert.NotSame(first, second);
        Assert.True(second.Success, string.Join(Environment.NewLine, second.Issues));
        Assert.Equal(rows, replay.Export().ToJsonString());
        Assert.Equal(first.Plan!.InputFingerprint, second.Plan!.InputFingerprint);
        Assert.Equal(first.Plan.IdentityIndexAfterImage.ToJsonString(), second.Plan.IdentityIndexAfterImage.ToJsonString());
        Assert.True(cache.TryPeekValidated(out var retained));
        Assert.Same(second, retained);

        var ordinary = cache.GetOrBuildValidated(input, out reused);
        Assert.False(reused);
        Assert.True(ordinary.Success);
        Assert.Same(ordinary, cache.GetOrBuildValidated(input, out reused));
        Assert.True(reused);
        cache.InvalidateAll();
        Assert.False(cache.TryPeekValidated(out _));
        Assert.True(cache.GetOrBuildValidated(input, out reused).Success);
        Assert.False(reused);
    }
}
