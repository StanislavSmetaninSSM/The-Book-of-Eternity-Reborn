using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ResourcePendingResolutionTests
{
    /// <summary>
    /// Recreates complete pending authority with retained request IDs and the original timestamp.
    /// </summary>
    [Fact]
    public void SpiritualReplay_PendingOwnerPreservesIdsAndTime()
    {
        var drafts = new[] { Draft(), Draft(effectId: "effect_second", eventRef: "turn_42_effect_3", activationOrdinal: 4) };
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var factory = new SpiritualWoundResourceIdentityFactory(journal, new AcceptedMechanicsIdentityFactory());
        var time = factory.GetPendingCreatedAtUtc(drafts);
        var first = ResourcePendingResolutionState.CreatePending(null, drafts,
            ResourceDefinitionCatalog.CreateBuiltIn(), factory.CreatePendingRequestId, time);
        Assert.True(first.IsValid, Format(first.Issues));
        Assert.Equal(3, journal.Export().Count);
        var replay = SpiritualWoundReplayJournal.CreateReplay(journal.Export().ToJsonString());
        var restored = new SpiritualWoundResourceIdentityFactory(replay, new ForbiddenPendingAllocationFactory());
        var second = ResourcePendingResolutionState.CreatePending(null, drafts,
            ResourceDefinitionCatalog.CreateBuiltIn(), restored.CreatePendingRequestId, restored.GetPendingCreatedAtUtc(drafts));
        Assert.True(second.IsValid, Format(second.Issues));
        Assert.Equal(first.State!.ToCanonicalJson(), second.State!.ToCanonicalJson());
        Assert.Equal(first.SafeGmPacket!.ToJsonString(), second.SafeGmPacket!.ToJsonString());
        Assert.Equal(journal.Export().ToJsonString(), replay.Export().ToJsonString());
    }

    /// <summary>
    /// Prevents typed callbacks and captured caller aliases from rewriting already validated request evidence.
    /// </summary>
    [Fact]
    public void SpiritualReplay_PendingTypedAllocatorCannotMutateValidatedDrafts()
    {
        var drafts = new[] { Draft(), Draft(effectId: "effect_second", eventRef: "turn_42_effect_3", activationOrdinal: 4) };
        var result = ResourcePendingResolutionState.CreatePending(null, drafts,
            ResourceDefinitionCatalog.CreateBuiltIn(), observed =>
            {
                observed.Source["sourceId"] = "forged_callback_source";
                observed.Target["targetId"] = "forged_callback_target";
                drafts[1].Source["sourceId"] = "forged_original_source";
                drafts[1].Target["targetId"] = "forged_original_target";
                return "resource_resolution_" + Guid.NewGuid().ToString("N");
            }, DateTimeOffset.UtcNow);
        Assert.True(result.IsValid, Format(result.Issues));
        var json = result.State!.ToCanonicalJson();
        Assert.DoesNotContain("forged_", json, StringComparison.Ordinal);
        Assert.Contains("wound_test_torn_side", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Prevents the legacy zero-argument callback from mutating caller-owned request evidence.
    /// </summary>
    [Fact]
    public void SpiritualReplay_PendingLegacyAllocatorCannotMutateValidatedDrafts()
    {
        var draft = Draft();
        var result = ResourcePendingResolutionState.CreatePending(null, new[] { draft },
            ResourceDefinitionCatalog.CreateBuiltIn(), () =>
            {
                draft.Source["sourceId"] = "forged_legacy_source";
                return "resource_resolution_" + Guid.NewGuid().ToString("N");
            }, DateTimeOffset.UtcNow);
        Assert.True(result.IsValid, Format(result.Issues));
        Assert.DoesNotContain("forged_", result.State!.ToCanonicalJson(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Makes any fresh pending allocation or clock access during strict replay observable.
    /// </summary>
    private sealed class ForbiddenPendingAllocationFactory : AcceptedMechanicsIdentityFactory
    {
        /// <inheritdoc/>
        internal override string CreatePendingRequestId(ResourcePendingResolutionDraft draft) =>
            throw new NotSupportedException("Replay requested randomness.");

        /// <inheritdoc/>
        internal override DateTimeOffset GetPendingCreatedAtUtc(IReadOnlyList<ResourcePendingResolutionDraft> drafts) =>
            throw new NotSupportedException("Replay requested a fresh timestamp.");
    }
}
