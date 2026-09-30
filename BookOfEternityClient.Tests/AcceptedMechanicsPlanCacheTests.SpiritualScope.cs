using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AcceptedMechanicsPlanCacheTests
{
    /// <summary>
    /// Requires fresh replay scopes to rebuild actual effects and revoke dependent common plans.
    /// </summary>
    [Fact]
    public void SpiritualScope_RegistryReplaysOrdinaryEffectsAndInvalidatesCommon()
    {
        var state = AcceptedTurnStateHarness.Create();
        var input = EffectAcceptedTurnPlanCacheTests.CreateSkillScopeInput("exact");
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var factory = new SpiritualWoundEffectIdentityFactory(journal, new EffectIdentityFactory());
        var first = state.GetOrBuildEffectValidated(input, factory);
        Assert.True(first.Success, string.Join(Environment.NewLine, first.Issues));
        var rows = journal.Export().ToJsonString();
        Assert.NotEqual("[]", rows);
        Assert.True(state.GetOrBuildCommonValidated(ValidCommonInput()).Success);
        Assert.True(state.GetOrBuildEffectValidated(input, factory).Success);
        Assert.True(state.TryPeekCommonValidated(out _, out _));

        var replay = SpiritualWoundReplayJournal.CreateReplay(rows);
        var second = state.GetOrBuildEffectValidated(input,
            new SpiritualWoundEffectIdentityFactory(replay, new EffectIdentityFactory()));
        Assert.True(second.Success, string.Join(Environment.NewLine, second.Issues));
        Assert.Equal(rows, replay.Export().ToJsonString());
        Assert.Equal(first.Plan!.InputFingerprint, second.Plan!.InputFingerprint);
        Assert.Equal(first.Plan.IdentityIndexAfterImage.ToJsonString(), second.Plan.IdentityIndexAfterImage.ToJsonString());
        Assert.False(state.TryPeekCommonValidated(out _, out _));
    }

    /// <summary>
    /// Keeps replayed data equal while replacing the authority token of a wound effect stage.
    /// </summary>
    [Fact]
    public void SpiritualScope_RegistryReplayReplacesWoundStageAuthority()
    {
        var state = AcceptedTurnStateHarness.Create();
        var prepared = AssertPrepared(state.GetOrBuildWoundPrepared(
            WoundEffectBatchPlannerTests.CreateInputForAcceptedCache()));
        var input = WoundEffectBatchPlannerTests.CreateEffectInputForAcceptedCache(prepared);
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var factory = new SpiritualWoundEffectIdentityFactory(journal, new EffectIdentityFactory());
        var first = state.GetOrBuildWoundEffectValidated(prepared, input, factory);
        Assert.True(first.Success, string.Join(Environment.NewLine, first.Issues));
        var rows = journal.Export().ToJsonString();
        Assert.NotEqual("[]", rows);
        Assert.True(state.GetOrBuildWoundFinal(prepared, first).Success);
        Assert.True(state.GetOrBuildCommonValidated(ValidCommonInput()).Success);
        Assert.True(state.GetOrBuildWoundEffectValidated(prepared, input, factory).Success);
        Assert.True(state.TryPeekWoundFinal(out _));
        Assert.True(state.TryPeekCommonValidated(out _, out _));

        var replay = SpiritualWoundReplayJournal.CreateReplay(rows);
        var second = state.GetOrBuildWoundEffectValidated(prepared, input,
            new SpiritualWoundEffectIdentityFactory(replay, new EffectIdentityFactory()));
        Assert.True(second.Success, string.Join(Environment.NewLine, second.Issues));
        Assert.Equal(rows, replay.Export().ToJsonString());
        Assert.Equal(first.Plan!.EffectAcceptedTurnPlanFingerprint, second.Plan!.EffectAcceptedTurnPlanFingerprint);
        Assert.False(state.TryPeekWoundFinal(out _));
        Assert.False(state.TryPeekCommonValidated(out _, out _));
        Assert.True(state.GetOrBuildWoundFinal(prepared, second).Success);
        var stale = state.GetOrBuildWoundFinal(prepared, first);
        Assert.False(stale.Success);
        Assert.Equal("wound_plan_effect_binding_mismatch", Assert.Single(stale.Issues).Code);
    }
}
