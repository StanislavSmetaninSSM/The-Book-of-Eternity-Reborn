using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using static BookOfEternityClient.Services.EffectAcceptedTurnPlanner;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectAcceptedTurnPlannerTests
{
    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public void DraftOwner_ReplacementRetainsExactWriteRangesAndRealCarrierEdits(bool consumes, int uses)
    {
        var fixture = CreateDraftOwnerFixture(consumes: consumes, uses: uses);
        using var draft = EffectAcceptedDraft.Begin(fixture.Plan, fixture.Factory);
        Assert.Empty(fixture.Factory.Ids);
        Assert.Empty(draft.Phases);
        var result = draft.Complete(fixture.Transcript);
        Assert.True(result.Success, IdentityOwnerIssues(result));
        Assert.Equal(Enum.GetValues<EffectDraftPhase>(), draft.Phases.Select(value => value.Phase));
        var reactionPhase = draft.Phases.Single(value => value.Phase == EffectDraftPhase.NonterminalReaction);
        var application = Assert.Single(reactionPhase.Applications);
        Assert.Equal("replace", application.Disposition);
        Assert.Equal(2, application.CarrierEdits.Count);
        var removed = application.CarrierEdits[0];
        var created = application.CarrierEdits[1];
        Assert.Equal(fixture.OldId, removed.EffectId);
        Assert.NotNull(removed.ReadBefore());
        Assert.Null(removed.ReadAfter());
        Assert.Equal(application.EffectId, created.EffectId);
        Assert.Null(created.ReadBefore());
        Assert.True(JsonNode.DeepEquals(application.ReadCreatedEffect(), created.ReadAfter()));
        Assert.Equal(application.Target, result.Plan!.Targets.Single());
        Assert.Equal(application.Carrier, removed.Carrier);
        Assert.Equal(application.Carrier, created.Carrier);
        Assert.Equal("draft_owner_child", application.Source.Key.DefinitionKey);
        Assert.Equal(EffectDraftApplicationProvenanceKind.Reaction, application.ProvenanceKind);
        Assert.Equal(fixture.OldId, application.ProducerEffectId);
        Assert.Equal(3, application.ReadParameters()!["amount"]!.GetValue<int>());
        Assert.Equal(application.EffectId, application.ResultIdentity!.EffectId);
        Assert.Equal(fixture.OldId, application.ReplacedIdentity!.EffectId);
        Assert.Equal(new[] { EffectIdentityWriteKind.AppendTransition, EffectIdentityWriteKind.CreateEntry },
            application.IdentityWrites.Select(value => value.Kind));
        Assert.Equal(new[] { EffectIdentityAllocationKind.Effect, EffectIdentityAllocationKind.Transition,
            EffectIdentityAllocationKind.Transition }, application.Allocations.Select(value => value.Kind));
        Assert.True(JsonNode.DeepEquals(
            application.ReadCreatedIdentity(), JsonNode.Parse(application.IdentityWrites[1].PayloadJson)));
        var agreementPhase = draft.Phases.Single(value => value.Phase == EffectDraftPhase.ReplacementAgreement);
        var agreement = Assert.Single(agreementPhase.ReplacementAgreements);
        Assert.Equal(application.ResultIdentity, agreement.Result);
        Assert.Equal(application.ReplacedIdentity, agreement.Replaced);
        Assert.Equal(2, agreement.Anchors.Count);
        Assert.Empty(agreementPhase.Allocations);
        Assert.Empty(agreementPhase.IdentityWrites);
        var consumption = draft.Phases.Single(value => value.Phase == EffectDraftPhase.ConsumingTrigger);
        if (consumes)
        {
            var write = Assert.Single(consumption.IdentityWrites);
            Assert.Equal(EffectIdentityWriteKind.InsertBeforeTransition, write.Kind);
            Assert.Equal(application.IdentityWrites[0].PayloadJson, write.AnchorJson);
            Assert.Empty(consumption.CarrierEdits); // the replaced carrier is never restored
            Assert.Equal(3, Assert.Single(consumption.Allocations).Ordinal);
        }
        else
        {
            Assert.Empty(consumption.IdentityWrites);
            var trigger = draft.Phases.Single(value => value.Phase == EffectDraftPhase.NonConsumingTrigger);
            Assert.Single(trigger.CarrierEdits);
            Assert.Single(trigger.IdentityWrites);
            Assert.Equal(0, Assert.Single(trigger.Allocations).Ordinal);
        }
        Assert.Equal(fixture.Factory.Ids, draft.Phases.SelectMany(value => value.Allocations)
            .Select(value => value.Identity));
        Assert.Equal(Enumerable.Range(0, fixture.Factory.Ids.Count).Select(value => (long)value),
            draft.Phases.SelectMany(value => value.Allocations).Select(value => value.Ordinal));
        Assert.True(JsonNode.DeepEquals(result.Plan.IdentityIndexAfterImage, draft.ReadIdentityIndex()));
        Assert.True(JsonNode.DeepEquals(result.Plan.ResourceTriggerCarriers.PlayerEffects,
            draft.ReadCarriers().PlayerEffects));
    }

    [Theory]
    [InlineData("stack", 1, "stack")]
    [InlineData("stack", 3, "no_change")]
    [InlineData("refresh", 1, "refresh")]
    [InlineData("merge", 1, "merge")]
    public void DraftOwner_NonCreateReceiptContainsActualUpdatedImage(string policy, int stacks, string disposition)
    {
        var fixture = CreateDraftOwnerFixture(nonCreatePolicy: policy, incumbentStacks: stacks);
        var before = fixture.Plan.ResourceTriggerCarriers.PlayerEffects!["activeEffects"]!.AsArray()
            .OfType<JsonObject>().Single(value => value["effectId"]!.GetValue<string>() == fixture.IncumbentId);
        using var draft = EffectAcceptedDraft.Begin(fixture.Plan, fixture.Factory);
        var result = draft.Complete(fixture.Transcript);
        Assert.True(result.Success, IdentityOwnerIssues(result));
        var phase = draft.Phases.Single(value => value.Phase == EffectDraftPhase.NonterminalReaction);
        var application = Assert.Single(phase.Applications);
        Assert.Equal(disposition, application.Disposition);
        Assert.Equal(fixture.IncumbentId, application.EffectId);
        Assert.Null(application.ReadCreatedEffect());
        Assert.Null(application.ReadCreatedIdentity());
        Assert.Null(application.ResultIdentity);
        Assert.Null(application.ReplacedIdentity);
        Assert.Equal(EffectDraftApplicationProvenanceKind.Reaction, application.ProvenanceKind);
        Assert.Equal(fixture.OldId, application.ProducerEffectId);
        var edit = Assert.Single(application.CarrierEdits);
        Assert.True(JsonNode.DeepEquals(before, edit.ReadBefore()));
        var after = result.Plan!.ResourceTriggerCarriers.PlayerEffects!["activeEffects"]!.AsArray()
            .OfType<JsonObject>().Single(value => value["effectId"]!.GetValue<string>() == fixture.IncumbentId);
        Assert.True(JsonNode.DeepEquals(after, edit.ReadAfter()));
        Assert.NotEqual(edit.BeforeJson, edit.AfterJson); // no_change still records its real stack chronology
        var write = Assert.Single(application.IdentityWrites);
        Assert.Equal(EffectIdentityWriteKind.AppendTransition, write.Kind);
        Assert.Equal(fixture.IncumbentId, write.EffectId);
        Assert.Equal(application.TransitionId, Assert.Single(application.Allocations).Identity);
        Assert.Equal(EffectIdentityAllocationKind.Transition, application.Allocations[0].Kind);
        Assert.Empty(draft.Phases.SelectMany(value => value.ReplacementAgreements));
        Assert.Single(phase.AddedSources);
        Assert.Equal(application.Source.Key, phase.AddedSources[0].Key);
        Assert.Equal(new[] { application.Target }, phase.AddedTargets);
    }

    [Fact]
    public void DraftOwner_ReadsAreDetachedCompletionAndDisposeAreOnceOnly()
    {
        var fixture = CreateDraftOwnerFixture(consumes: true);
        using var draft = EffectAcceptedDraft.Begin(fixture.Plan, fixture.Factory);
        Assert.Throws<InvalidOperationException>(() => draft.ReadCarriers());
        var baseline = fixture.Plan.IdentityIndexAfterImage.ToJsonString();
        var result = draft.Complete(fixture.Transcript);
        Assert.True(result.Success, IdentityOwnerIssues(result));
        var expectedIdentity = draft.ReadIdentityIndex().ToJsonString();
        var expectedCarrier = draft.ReadCarriers().PlayerEffects!.ToJsonString();
        var application = Assert.Single(draft.Phases.SelectMany(value => value.Applications));
        var expectedCreated = application.ReadCreatedEffect()!.ToJsonString();
        var expectedDefinition = application.Source.ReadDefinition().ToJsonString();
        draft.ReadIdentityIndex()["entries"]!.AsArray().Clear();
        draft.ReadCarriers().PlayerEffects!["activeEffects"]!.AsArray().Clear();
        result.Plan!.IdentityIndexAfterImage["entries"]!.AsArray().Clear();
        application.ReadCreatedEffect()!["state"] = "not_owner_state";
        application.ReadCreatedIdentity()!["state"] = "not_owner_state";
        application.CarrierEdits[1].ReadAfter()!["state"] = "not_owner_state";
        application.Source.ReadDefinition()["components"]!.AsArray().Clear();
        Assert.Equal(expectedIdentity, draft.ReadIdentityIndex().ToJsonString());
        Assert.Equal(expectedCarrier, draft.ReadCarriers().PlayerEffects!.ToJsonString());
        Assert.Equal(expectedCreated, application.ReadCreatedEffect()!.ToJsonString());
        Assert.Equal(expectedDefinition, application.Source.ReadDefinition().ToJsonString());
        Assert.Equal(baseline, fixture.Plan.IdentityIndexAfterImage.ToJsonString());
        var count = fixture.Factory.Ids.Count;
        Assert.Throws<InvalidOperationException>(() => draft.Complete(fixture.Transcript));
        Assert.Equal(count, fixture.Factory.Ids.Count);
        draft.Dispose();
        draft.Dispose();
        Assert.Throws<ObjectDisposedException>(() => draft.ReadIdentityIndex());
        Assert.Throws<ObjectDisposedException>(() => draft.Complete(fixture.Transcript));
        Assert.Equal(6, draft.Phases.Count); // immutable evidence stays inspectable
    }

    [Theory]
    [InlineData("collision", null, "effect_identity_duplicate_transition")]
    [InlineData("malformed", null, "effect_identity_invalid_field")]
    [InlineData("malformed", "", "effect_identity_invalid_field")]
    [InlineData("malformed", " ", "effect_identity_invalid_field")]
    public void DraftOwner_InvalidAllocatorRetainsOwningDiagnosticStage(
        string scenario, string? transitionId, string expectedCode)
    {
        var fixture = CreateDraftOwnerFixture(consumes: true,
            collideTransitions: scenario == "collision",
            malformedReplacementTransition: scenario == "malformed",
            replacementTransitionId: transitionId);
        using var draft = EffectAcceptedDraft.Begin(fixture.Plan, fixture.Factory);
        var result = draft.Complete(fixture.Transcript);
        Assert.False(result.Success);
        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, value => value.Code == expectedCode);
        Assert.DoesNotContain(result.Issues, value => value.Code == "effect_reaction_replacement_authority_invalid");
        Assert.Equal(new[] { "effect", "transition", "transition", "transition" }, fixture.Factory.Kinds);
        Assert.Equal(6, draft.Phases.Count); // late canonical validation, not an earlier receipt exception
        Assert.Throws<InvalidOperationException>(() => draft.ReadIdentityIndex());
        Assert.Throws<InvalidOperationException>(() => draft.Complete(fixture.Transcript));
    }

    [Fact]
    public void DraftOwner_AllocatorExceptionFaultsWithoutExportingFailedPhase()
    {
        var fixture = CreateDraftOwnerFixture(consumes: true);
        using var draft = EffectAcceptedDraft.Begin(fixture.Plan, new IdentityOwnerThrowingFactory());
        Assert.Throws<IOException>(() => draft.Complete(fixture.Transcript));
        var phase = Assert.Single(draft.Phases);
        Assert.Equal(EffectDraftPhase.NonConsumingTrigger, phase.Phase);
        Assert.Empty(phase.Allocations);
        Assert.Empty(phase.Applications);
        Assert.Throws<InvalidOperationException>(() => draft.ReadCarriers());
        Assert.Throws<InvalidOperationException>(() => draft.Complete(fixture.Transcript));
    }

    [Fact]
    public void DraftOwner_OriginalCompletionRejectionRemainsBeforeMaterialization()
    {
        var first = CreateDraftOwnerFixture(consumes: true);
        var other = CreateDraftOwnerFixture(consumes: false);
        using var draft = EffectAcceptedDraft.Begin(first.Plan, first.Factory);
        var result = draft.Complete(other.Transcript);
        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == "effect_boundary_transcript_plan_mismatch");
        Assert.Empty(draft.Phases);
        Assert.Empty(first.Factory.Ids);
        Assert.Throws<InvalidOperationException>(() => draft.Complete(first.Transcript));
    }

    [Fact]
    public void DraftOwner_J1RangesAreBoundedDetachedAndKeepReceiptLifetime()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        var baseline = EffectMaterializationTestFixture.CreateIdentityIndex(effect);
        var effectId = effect["effectId"]!.GetValue<string>();
        using var owner = new EffectIdentityHistoryOwner(baseline, new IdentityOwnerFactory());
        Assert.Equal(0, owner.WriteCount);
        Assert.Equal(0, owner.AllocationCount);
        Assert.Equal(0, owner.ReplacementAgreementCount);
        var empty = owner.ReadWritesFrom(0);
        var transition = IdentityOwnerTransition(owner.Factory.CreateTransitionId(),
            "trigger", "turn_42:draft_owner_range", effectId, effectId);
        Assert.True(owner.TryAppendTransition(effectId, "active", transition));
        var writes = owner.ReadWritesFrom(0);
        var allocations = owner.ReadAllocationsFrom(0);
        Assert.Empty(empty);
        Assert.Single(writes);
        Assert.Single(allocations);
        Assert.Empty(owner.ReadWritesFrom(owner.WriteCount));
        Assert.Empty(owner.ReadAllocationsFrom(owner.AllocationCount));
        Assert.Empty(owner.ReadReplacementAgreementsFrom(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => owner.ReadWritesFrom(-1));
        Assert.ThrowsAny<ArgumentException>(() => owner.ReadWritesFrom(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => owner.ReadAllocationsFrom(-1));
        Assert.ThrowsAny<ArgumentException>(() => owner.ReadAllocationsFrom(2));
        owner.Publish();
        owner.Dispose();
        Assert.Equal(1, owner.WriteCount);
        Assert.Equal(writes, owner.ReadWritesFrom(0));
        Assert.Equal(allocations, owner.ReadAllocationsFrom(0));
        Assert.Throws<ObjectDisposedException>(() => owner.ReadSnapshot());
    }
}
