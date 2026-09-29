using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectAcceptedTurnPlannerTests
{
    /// <summary>
    /// Replays the actual combatant owner without changing its consumed same-turn references.
    /// </summary>
    [Fact]
    public void SpiritualReplay_EffectCombatantOwnerKeepsIdentities()
    {
        var combatants = new JsonArray(new JsonObject { ["combatantRef"] = "combatant_ref_alpha" });
        var members = new JsonArray(new JsonObject { ["memberRef"] = "member_ref_alpha", ["name"] = "Первый" });
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var first = CombatantIdentityState.BuildNew(combatants, members,
            new SpiritualWoundEffectIdentityFactory(journal, new EffectIdentityFactory()));
        Assert.Empty(first.Issues);
        Assert.Equal(2, journal.Export().Count);
        var replay = SpiritualWoundReplayJournal.CreateReplay(journal.Export().ToJsonString());
        var second = CombatantIdentityState.BuildNew(combatants, members,
            new SpiritualWoundEffectIdentityFactory(replay, new EffectIdentityFactory()));
        Assert.Empty(second.Issues);
        Assert.Equal(first.RewrittenCombatants.ToJsonString(), second.RewrittenCombatants.ToJsonString());
        Assert.Equal(first.RewrittenMembers.ToJsonString(), second.RewrittenMembers.ToJsonString());
        Assert.Equal(first.State!.Fingerprint, second.State!.Fingerprint);
        Assert.Equal(journal.Export().ToJsonString(), replay.Export().ToJsonString());
    }

    /// <summary>
    /// Rejects typed reference allocation once the enclosing history owner is no longer mutable.
    /// </summary>
    /// <param name="dispose">
    /// Whether revocation occurs by disposal rather than publication.
    /// </param>
    /// <param name="member">
    /// Whether to allocate a member rather than a combatant.
    /// </param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SpiritualReplay_EffectRevokedWrapperCannotAllocateReferences(bool dispose, bool member)
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        using var owner = new EffectIdentityHistoryOwner(new JsonObject { ["entries"] = new JsonArray() },
            new SpiritualWoundEffectIdentityFactory(journal, new EffectIdentityFactory()));
        if (dispose) owner.Dispose();
        else owner.Publish();
        Assert.ThrowsAny<InvalidOperationException>(() => member
            ? owner.Factory.CreateMemberId("member_ref_alpha") : owner.Factory.CreateCombatantId("combatant_ref_alpha"));
        Assert.Empty(journal.Export());
    }

    /// <summary>
    /// Rebuilds actual effect creation with retained random identities and identical carrier state.
    /// </summary>
    [Fact]
    public void SpiritualReplay_EffectBaseCreationKeepsIdentities()
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var first = new EffectAcceptedTurnPlanCache(new SpiritualWoundEffectIdentityFactory(journal,
            new EffectIdentityFactory())).GetOrBuild(EffectAcceptedTurnPlanCacheTests.CreateSkillScopeInput("exact"));
        Assert.True(first.Success, IdentityOwnerIssues(first));
        Assert.NotEmpty(journal.Export());
        var replay = SpiritualWoundReplayJournal.CreateReplay(journal.Export().ToJsonString());
        var second = new EffectAcceptedTurnPlanCache(new SpiritualWoundEffectIdentityFactory(replay,
            new EffectIdentityFactory())).GetOrBuild(EffectAcceptedTurnPlanCacheTests.CreateSkillScopeInput("exact"));
        Assert.True(second.Success, IdentityOwnerIssues(second));
        Assert.Equal(first.Plan!.IdentityIndexAfterImage.ToJsonString(), second.Plan!.IdentityIndexAfterImage.ToJsonString());
        Assert.Equal(first.Plan.ResourceTriggerCarriers.PlayerEffects!.ToJsonString(),
            second.Plan.ResourceTriggerCarriers.PlayerEffects!.ToJsonString());
        Assert.Equal(journal.Export().ToJsonString(), replay.Export().ToJsonString());
    }

    /// <summary>
    /// Replays real replacement allocations including the original effect's consumption transition.
    /// </summary>
    /// <param name="consumes">
    /// Whether the original trigger consumes a use before its replacement is accepted.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SpiritualReplay_EffectReplacementKeepsHistoryAndSemanticSlots(bool consumes)
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var first = IdentityOwnerReplacement(consumes, allocationFactory:
            new SpiritualWoundEffectIdentityFactory(journal, new EffectIdentityFactory()));
        Assert.True(first.Result.Success, IdentityOwnerIssues(first.Result));
        var rows = journal.Export();
        Assert.Equal(4, rows.Count);
        var replay = SpiritualWoundReplayJournal.CreateReplay(rows.ToJsonString());
        var second = IdentityOwnerReplacement(consumes, allocationFactory:
            new SpiritualWoundEffectIdentityFactory(replay, new EffectIdentityFactory()));
        Assert.True(second.Result.Success, IdentityOwnerIssues(second.Result));
        Assert.Equal(first.Result.Plan!.IdentityIndexAfterImage.ToJsonString(),
            second.Result.Plan!.IdentityIndexAfterImage.ToJsonString());
        Assert.Equal(first.Result.Plan.ResourceTriggerCarriers.PlayerEffects!.ToJsonString(),
            second.Result.Plan.ResourceTriggerCarriers.PlayerEffects!.ToJsonString());
        Assert.Equal(rows.ToJsonString(), replay.Export().ToJsonString());
    }

    /// <summary>
    /// Preserves typed calls through the identity-history wrapper and rejects a changed subject.
    /// </summary>
    [Fact]
    public void SpiritualReplay_EffectOwnerForwardsTypedKeysAndReferences()
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        using var owner = new EffectIdentityHistoryOwner(new JsonObject { ["entries"] = new JsonArray() },
            new SpiritualWoundEffectIdentityFactory(journal, new EffectIdentityFactory()));
        var key = new EffectIdentityAllocationKey("event:1", "create", "player:1");
        owner.Factory.CreateEffectId(key);
        owner.Factory.CreateTransitionId(key);
        owner.Factory.CreateResolutionId(key);
        owner.Factory.CreateCombatantId("combatant_ref:1");
        owner.Factory.CreateMemberId("member_ref:1");
        Assert.Equal(3, owner.Allocations.Count);
        Assert.Equal(5, journal.Export().Count);
        var replay = SpiritualWoundReplayJournal.CreateReplay(journal.Export().ToJsonString());
        var factory = new SpiritualWoundEffectIdentityFactory(replay, new EffectIdentityFactory());
        Assert.Throws<InvalidOperationException>(() => factory.CreateEffectId(key with { SubjectId = "player:2" }));
        Assert.Throws<InvalidOperationException>(() => replay.Export());
    }

    /// <summary>
    /// Prevents an unbound allocation from escaping journal validation.
    /// </summary>
    [Fact]
    public void SpiritualReplay_EffectUnboundCallsFaultJournal()
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var factory = new SpiritualWoundEffectIdentityFactory(journal, new EffectIdentityFactory());
        Assert.Throws<InvalidOperationException>(() => factory.CreateEffectId());
        Assert.Throws<InvalidOperationException>(() => journal.Export());
    }

    /// <summary>
    /// Keeps inherited unbound combatant calls from bypassing a journaled history owner's factory.
    /// </summary>
    /// <param name="member">
    /// Whether to exercise the member rather than combatant allocation method.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SpiritualReplay_EffectWrapperRejectsUnboundCombatantCalls(bool member)
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        using var owner = new EffectIdentityHistoryOwner(new JsonObject { ["entries"] = new JsonArray() },
            new SpiritualWoundEffectIdentityFactory(journal, new EffectIdentityFactory()));
        Assert.Throws<InvalidOperationException>(() => member
            ? owner.Factory.CreateMemberId() : owner.Factory.CreateCombatantId());
        Assert.Throws<InvalidOperationException>(() => journal.Export());
    }
}
