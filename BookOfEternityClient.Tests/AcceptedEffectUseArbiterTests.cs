using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AcceptedEffectUseArbiterTests
{
    [Fact]
    public void Arbitrate_OrdersCandidatesByPriorityTriggerIdAndEffectId()
    {
        var arbiter = Initialize();
        var priorityTwenty = Candidate(
            "effect_z",
            "trigger_a",
            "event_priority_twenty",
            priority: 20);
        var triggerB = Candidate(
            "effect_b",
            "trigger_b",
            "event_trigger_b",
            priority: 10);
        var effectZ = Candidate(
            "effect_z",
            "trigger_a",
            "event_effect_z",
            priority: 10);
        var effectA = Candidate(
            "effect_a",
            "trigger_a",
            "event_effect_a",
            priority: 10);

        var result = arbiter.Arbitrate(new[]
        {
            priorityTwenty,
            triggerB,
            effectZ,
            effectA
        });

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
        Assert.Empty(result.RejectedEvidence);
        Assert.Equal(
            new[]
            {
                effectA.Identity,
                effectZ.Identity,
                triggerB.Identity,
                priorityTwenty.Identity
            },
            result.AcceptedActivations.Select(static activation => activation.Stamp.Identity));
        Assert.Equal(
            new long[] { 0, 1, 2, 3 },
            result.AcceptedActivations.Select(
                static activation => activation.Stamp.ActivationOrdinal));
    }

    [Fact]
    public void Arbitrate_OrdersAcceptedApplicationsByTypedAuthorityNotRandomEffectId()
    {
        var arbiter = Initialize();
        var authorityFirst = new EffectActivationCandidate(
            Identity(
                "effect_random_z",
                "trigger_same",
                "event_authority_first"),
            Priority: 10,
            ConsumesUse: false,
            EffectAuthority: new ResourcePendingAuthorityBinding(
                "accepted_application",
                "turn_2:application:a"));
        var authoritySecond = new EffectActivationCandidate(
            Identity(
                "effect_random_a",
                "trigger_same",
                "event_authority_second"),
            Priority: 10,
            ConsumesUse: false,
            EffectAuthority: new ResourcePendingAuthorityBinding(
                "accepted_application",
                "turn_2:application:z"));

        var result = arbiter.Arbitrate(new[]
        {
            authoritySecond,
            authorityFirst
        });

        Assert.True(result.IsValid);
        Assert.Equal(
            new[] { authorityFirst.Identity, authoritySecond.Identity },
            result.AcceptedActivations.Select(
                static activation => activation.Stamp.Identity));
    }

    [Fact]
    public void Arbitrate_SameAcceptedApplicationAuthorityWithDifferentRawEffectIdsFailsClosedAsDuplicate()
    {
        var arbiter = Initialize();
        var first = new EffectActivationCandidate(
            Identity(
                "effect_random_a",
                "trigger_same",
                "event_same",
                triggerEventRef: "producer_same"),
            Priority: 10,
            ConsumesUse: false,
            EffectAuthority: AcceptedApplicationAuthority(
                "turn_2:application:stable"));
        var duplicate = first with
        {
            Identity = first.Identity with { EffectId = "effect_random_b" },
            Priority = 20
        };

        var result = arbiter.Arbitrate(new[] { duplicate, first });

        Assert.False(result.IsValid);
        Assert.Empty(result.AcceptedActivations);
        Assert.Empty(result.RejectedEvidence);
        Assert.Contains(
            result.Issues,
            issue => issue.Code ==
                     AcceptedEffectUseArbiterIssueCodes.DuplicateActivation);
        Assert.Empty(arbiter.AcceptedTranscript);

        var retry = arbiter.Arbitrate(new[] { first });
        var accepted = Assert.Single(retry.AcceptedActivations);
        Assert.Equal(0, accepted.Stamp.ActivationOrdinal);
    }

    [Fact]
    public void Arbitrate_SameAcceptedApplicationActivationAcrossCallsUsesTypedDuplicateKey()
    {
        var arbiter = Initialize();
        var first = new EffectActivationCandidate(
            Identity(
                "effect_random_a",
                "trigger_same",
                "event_same",
                triggerEventRef: "producer_same"),
            Priority: 10,
            ConsumesUse: false,
            EffectAuthority: AcceptedApplicationAuthority(
                "turn_2:application:stable"));
        var duplicate = first with
        {
            Identity = first.Identity with { EffectId = "effect_random_b" }
        };
        Assert.Single(arbiter.Arbitrate(new[] { first }).AcceptedActivations);

        var result = arbiter.Arbitrate(new[] { duplicate });

        Assert.False(result.IsValid);
        Assert.Empty(result.AcceptedActivations);
        Assert.Contains(
            result.Issues,
            issue => issue.Code ==
                     AcceptedEffectUseArbiterIssueCodes.DuplicateActivation);
        Assert.Single(arbiter.AcceptedTranscript);
    }

    [Theory]
    [InlineData("same_turn_ref", "turn_2:application:a")]
    [InlineData("permanent", "effect_other")]
    public void Arbitrate_RejectsInvalidTypedEffectAuthority(
        string bindingKind,
        string authorityId)
    {
        var arbiter = Initialize();
        var candidate = new EffectActivationCandidate(
            Identity(
                "effect_current",
                "trigger_invalid_authority",
                "event_invalid_authority"),
            Priority: 10,
            ConsumesUse: false,
            EffectAuthority: new ResourcePendingAuthorityBinding(
                bindingKind,
                authorityId));

        var result = arbiter.Arbitrate(new[] { candidate });

        Assert.False(result.IsValid);
        Assert.Empty(result.AcceptedActivations);
        Assert.Contains(
            result.Issues,
            issue => issue.Code ==
                     AcceptedEffectUseArbiterIssueCodes.InvalidActivationIdentity);
    }

    [Fact]
    public void Arbitrate_ConsumesStrictBudgetsAndTerminalRejectsLaterCandidates()
    {
        var arbiter = Initialize(new CanonicalEffectUseSeed("effect_uses", 2));
        var observeBeforeTerminal = Candidate(
            "effect_uses",
            "trigger_observe",
            "event_observe",
            priority: 5);
        var firstUse = Candidate(
            "effect_uses",
            "trigger_use_a",
            "event_use_a",
            priority: 10,
            consumesUse: true);
        var finalUse = Candidate(
            "effect_uses",
            "trigger_use_b",
            "event_use_b",
            priority: 20,
            consumesUse: true);
        var observeAfterTerminal = Candidate(
            "effect_uses",
            "trigger_observe_late",
            "event_observe_late",
            priority: 30);

        var result = arbiter.Arbitrate(new[]
        {
            observeAfterTerminal,
            finalUse,
            observeBeforeTerminal,
            firstUse
        });

        Assert.True(result.IsValid);
        Assert.Collection(
            result.AcceptedActivations,
            activation =>
            {
                Assert.Equal(observeBeforeTerminal.Identity, activation.Stamp.Identity);
                Assert.Equal(0, activation.Stamp.ActivationOrdinal);
                Assert.False(activation.Stamp.ConsumesUse);
                Assert.Null(activation.Stamp.UsesBefore);
                Assert.Null(activation.UsesAfter);
                Assert.False(activation.EffectTerminal);
            },
            activation =>
            {
                Assert.Equal(firstUse.Identity, activation.Stamp.Identity);
                Assert.Equal(1, activation.Stamp.ActivationOrdinal);
                Assert.Equal(2, activation.Stamp.UsesBefore);
                Assert.Equal(1, activation.UsesAfter);
                Assert.False(activation.EffectTerminal);
            },
            activation =>
            {
                Assert.Equal(finalUse.Identity, activation.Stamp.Identity);
                Assert.Equal(2, activation.Stamp.ActivationOrdinal);
                Assert.Equal(1, activation.Stamp.UsesBefore);
                Assert.Equal(0, activation.UsesAfter);
                Assert.True(activation.EffectTerminal);
            });
        var rejected = Assert.Single(result.RejectedEvidence);
        Assert.Equal(observeAfterTerminal.Identity, rejected.Identity);
        Assert.Equal(
            EffectActivationRejectionReason.EffectTerminal,
            rejected.Reason);
        Assert.True(arbiter.TryGetRemainingUses("effect_uses", out var remaining));
        Assert.Equal(0, remaining);
    }

    [Fact]
    public void Arbitrate_ExactDuplicateFailsClosedWithoutChangingLedger()
    {
        var arbiter = Initialize(new CanonicalEffectUseSeed("effect_duplicate", 1));
        var identity = Identity(
            "effect_duplicate",
            "trigger_duplicate",
            "event_duplicate");
        var firstOutput = new EffectActivationCandidate(
            identity,
            Priority: 10,
            ConsumesUse: true,
            EffectAuthority: PermanentAuthority(identity.EffectId));
        var secondOutput = new EffectActivationCandidate(
            identity,
            Priority: 20,
            ConsumesUse: true,
            EffectAuthority: PermanentAuthority(identity.EffectId));

        var duplicate = arbiter.Arbitrate(new[] { firstOutput, secondOutput });

        Assert.False(duplicate.IsValid);
        Assert.Empty(duplicate.AcceptedActivations);
        Assert.Empty(duplicate.RejectedEvidence);
        Assert.Contains(
            duplicate.Issues,
            issue => issue.Code ==
                     AcceptedEffectUseArbiterIssueCodes.DuplicateActivation);
        Assert.Empty(arbiter.AcceptedTranscript);
        Assert.True(arbiter.TryGetRemainingUses("effect_duplicate", out var remaining));
        Assert.Equal(1, remaining);

        var singleActivation = arbiter.Arbitrate(new[] { firstOutput });

        var accepted = Assert.Single(singleActivation.AcceptedActivations);
        Assert.Equal(0, accepted.Stamp.ActivationOrdinal);
        Assert.Equal(1, accepted.Stamp.UsesBefore);
        Assert.Equal(0, accepted.UsesAfter);
    }

    [Fact]
    public void Arbitrate_ResultAndTranscriptCollectionsCannotBeMutated()
    {
        var arbiter = Initialize();
        var candidate = Candidate(
            "effect_read_only",
            "trigger_read_only",
            "event_read_only",
            priority: 10);

        var result = arbiter.Arbitrate(new[] { candidate });

        AssertReadOnly(result.AcceptedActivations);
        AssertReadOnly(result.RejectedEvidence);
        AssertReadOnly(result.Issues);
        AssertReadOnly(arbiter.AcceptedTranscript);
    }

    [Fact]
    public void Arbitrate_IdentityUsesEventRefAndTriggerEventRefIndependently()
    {
        var arbiter = Initialize();
        var first = new EffectActivationCandidate(
            new EffectActivationCandidateIdentity(
                "effect_identity",
                "trigger_identity",
                "resource_damaged",
                "activation_a",
                "producer_a"),
            Priority: 10,
            ConsumesUse: false,
            EffectAuthority: PermanentAuthority("effect_identity"));
        var changedProducer = first with
        {
            Identity = first.Identity with { TriggerEventRef = "producer_b" }
        };
        var changedActivation = first with
        {
            Identity = first.Identity with { EventRef = "activation_b" }
        };

        var result = arbiter.Arbitrate(new[]
        {
            changedActivation,
            changedProducer,
            first
        });

        Assert.True(result.IsValid);
        Assert.Equal(
            new[]
            {
                first.Identity,
                changedProducer.Identity,
                changedActivation.Identity
            },
            result.AcceptedActivations.Select(
                static activation => activation.Stamp.Identity));
    }

    [Fact]
    public void Arbitrate_TerminalStateRejectsLaterBatchWithoutAdvancingOrdinal()
    {
        var arbiter = Initialize(new CanonicalEffectUseSeed("effect_terminal_batch", 1));
        var finalUse = Candidate(
            "effect_terminal_batch",
            "trigger_final_use",
            "event_final_use",
            priority: 10,
            consumesUse: true);
        var first = Assert.Single(
            arbiter.Arbitrate(new[] { finalUse }).AcceptedActivations);
        var lateObserver = Candidate(
            "effect_terminal_batch",
            "trigger_late_observer",
            "event_late_observer",
            priority: 5);

        var rejectedResult = arbiter.Arbitrate(new[] { lateObserver });

        Assert.True(rejectedResult.IsValid);
        Assert.Empty(rejectedResult.AcceptedActivations);
        Assert.Equal(
            EffectActivationRejectionReason.EffectTerminal,
            Assert.Single(rejectedResult.RejectedEvidence).Reason);
        Assert.Equal(new[] { first.Stamp }, arbiter.AcceptedTranscript);

        var otherEffect = Candidate(
            "effect_other",
            "trigger_other",
            "event_other",
            priority: 1);
        var laterAccepted = Assert.Single(
            arbiter.Arbitrate(new[] { otherEffect }).AcceptedActivations);
        Assert.Equal(1, laterAccepted.Stamp.ActivationOrdinal);
    }

    [Fact]
    public void Arbitrate_ReplayedActivationFailsClosedWithoutConsumingAnotherUse()
    {
        var arbiter = Initialize(new CanonicalEffectUseSeed("effect_replay", 2));
        var first = Candidate(
            "effect_replay",
            "trigger_first",
            "event_first",
            priority: 10,
            consumesUse: true);
        var later = Candidate(
            "effect_replay",
            "trigger_later",
            "event_later",
            priority: 20,
            consumesUse: true);
        Assert.Single(arbiter.Arbitrate(new[] { first }).AcceptedActivations);

        var replay = arbiter.Arbitrate(new[] { later, first });

        Assert.False(replay.IsValid);
        Assert.Empty(replay.AcceptedActivations);
        Assert.Contains(
            replay.Issues,
            issue => issue.Code ==
                     AcceptedEffectUseArbiterIssueCodes.DuplicateActivation);
        Assert.True(arbiter.TryGetRemainingUses("effect_replay", out var remaining));
        Assert.Equal(1, remaining);

        var acceptedLater = Assert.Single(
            arbiter.Arbitrate(new[] { later }).AcceptedActivations);
        Assert.Equal(1, acceptedLater.Stamp.ActivationOrdinal);
        Assert.Equal(1, acceptedLater.Stamp.UsesBefore);
    }

    [Fact]
    public void Initialize_ReplaysPriorLifecycleActivationsInStrictBudgetOrder()
    {
        var lifecycleA = Stamp(
            "effect_lifecycle",
            "trigger_lifecycle_a",
            "lifecycle_event_a",
            priority: 5,
            consumesUse: true,
            usesBefore: 3,
            activationOrdinal: 0,
            eventKind: "owner_turn_end");
        var lifecycleB = Stamp(
            "effect_lifecycle",
            "trigger_lifecycle_b",
            "lifecycle_event_b",
            priority: 10,
            consumesUse: true,
            usesBefore: 2,
            activationOrdinal: 1,
            eventKind: "owner_turn_end");
        var arbiter = Initialize(
            new[] { new CanonicalEffectUseSeed("effect_lifecycle", 3) },
            new[] { lifecycleA, lifecycleB });

        var resource = Candidate(
            "effect_lifecycle",
            "trigger_resource",
            "resource_event",
            priority: 20,
            consumesUse: true,
            eventKind: "resource_damaged");
        var result = arbiter.Arbitrate(new[] { resource });

        var accepted = Assert.Single(result.AcceptedActivations);
        Assert.Equal(2, accepted.Stamp.ActivationOrdinal);
        Assert.Equal(1, accepted.Stamp.UsesBefore);
        Assert.Equal(0, accepted.UsesAfter);
        Assert.Equal(
            new[] { lifecycleA, lifecycleB, accepted.Stamp },
            arbiter.AcceptedTranscript);
    }

    [Fact]
    public void Initialize_ReplayedAcceptedApplicationUsesTypedDuplicateKey()
    {
        var authority = AcceptedApplicationAuthority(
            "turn_2:application:stable");
        var replayed = Stamp(
            "effect_random_a",
            "trigger_same",
            "event_same",
            priority: 10,
            consumesUse: false,
            usesBefore: null,
            activationOrdinal: 0,
            effectAuthority: authority);
        var arbiter = Initialize(
            Array.Empty<CanonicalEffectUseSeed>(),
            new[] { replayed });
        var duplicate = new EffectActivationCandidate(
            Identity(
                "effect_random_b",
                "trigger_same",
                "event_same"),
            Priority: 20,
            ConsumesUse: false,
            EffectAuthority: authority);

        var result = arbiter.Arbitrate(new[] { duplicate });

        Assert.False(result.IsValid);
        Assert.Empty(result.AcceptedActivations);
        Assert.Contains(
            result.Issues,
            issue => issue.Code ==
                     AcceptedEffectUseArbiterIssueCodes.DuplicateActivation);
        Assert.Equal(new[] { replayed }, arbiter.AcceptedTranscript);
    }

    [Fact]
    public void Initialize_RejectsReplayBudgetGapInsteadOfClamping()
    {
        var replay = new[]
        {
            Stamp(
                "effect_gap",
                "trigger_first",
                "event_first",
                priority: 10,
                consumesUse: true,
                usesBefore: 3,
                activationOrdinal: 0),
            Stamp(
                "effect_gap",
                "trigger_gap",
                "event_gap",
                priority: 20,
                consumesUse: true,
                usesBefore: 1,
                activationOrdinal: 1)
        };

        var result = AcceptedEffectUseArbiter.Initialize(
            new[] { new CanonicalEffectUseSeed("effect_gap", 3) },
            replay);

        Assert.False(result.IsValid);
        Assert.Null(result.Arbiter);
        Assert.Contains(
            result.Issues,
            issue => issue.Code ==
                     AcceptedEffectUseArbiterIssueCodes.ReplayBudgetMismatch);
    }

    [Fact]
    public void Initialize_RejectsAcceptedReplayAfterEffectBecameTerminal()
    {
        var replay = new[]
        {
            Stamp(
                "effect_terminal",
                "trigger_final_use",
                "event_final_use",
                priority: 10,
                consumesUse: true,
                usesBefore: 1,
                activationOrdinal: 0),
            Stamp(
                "effect_terminal",
                "trigger_late_observer",
                "event_late_observer",
                priority: 20,
                consumesUse: false,
                usesBefore: null,
                activationOrdinal: 1)
        };

        var result = AcceptedEffectUseArbiter.Initialize(
            new[] { new CanonicalEffectUseSeed("effect_terminal", 1) },
            replay);

        Assert.False(result.IsValid);
        Assert.Null(result.Arbiter);
        Assert.Contains(
            result.Issues,
            issue => issue.Code ==
                     AcceptedEffectUseArbiterIssueCodes.ReplayAfterTerminal);
    }

    [Fact]
    public void Initialize_RejectsReplayActivationOrdinalGap()
    {
        var replay = new[]
        {
            Stamp(
                "effect_ordinal",
                "trigger_first",
                "event_first",
                priority: 10,
                consumesUse: false,
                usesBefore: null,
                activationOrdinal: 0),
            Stamp(
                "effect_ordinal",
                "trigger_gap",
                "event_gap",
                priority: 20,
                consumesUse: false,
                usesBefore: null,
                activationOrdinal: 2)
        };

        var result = AcceptedEffectUseArbiter.Initialize(
            Array.Empty<CanonicalEffectUseSeed>(),
            replay);

        Assert.False(result.IsValid);
        Assert.Null(result.Arbiter);
        Assert.Contains(
            result.Issues,
            issue => issue.Code ==
                     AcceptedEffectUseArbiterIssueCodes.ReplayOrdinalMismatch);
    }

    [Fact]
    public void Arbitrate_ZeroOutputCandidateStillCreatesAcceptedActivationEvidence()
    {
        var arbiter = Initialize(new CanonicalEffectUseSeed("effect_noop", 1));
        var candidate = Candidate(
            "effect_noop",
            "trigger_noop",
            "event_noop",
            priority: 10,
            consumesUse: true);

        var result = arbiter.Arbitrate(new[] { candidate });

        var accepted = Assert.Single(result.AcceptedActivations);
        Assert.Equal(candidate.Identity, accepted.Stamp.Identity);
        Assert.Equal(0, accepted.Stamp.ActivationOrdinal);
        Assert.Equal(1, accepted.Stamp.UsesBefore);
        Assert.Equal(0, accepted.UsesAfter);
        Assert.Single(arbiter.AcceptedTranscript);
    }

    [Fact]
    public void Arbitrate_ConsumingCandidateWithoutCanonicalSeedFailsClosed()
    {
        var arbiter = Initialize();
        var observer = Candidate(
            "effect_observer",
            "trigger_observer",
            "event_observer",
            priority: 5);
        var invalidConsumer = Candidate(
            "effect_missing_seed",
            "trigger_consumer",
            "event_consumer",
            priority: 10,
            consumesUse: true);

        var result = arbiter.Arbitrate(new[] { observer, invalidConsumer });

        Assert.False(result.IsValid);
        Assert.Empty(result.AcceptedActivations);
        Assert.Empty(arbiter.AcceptedTranscript);
        Assert.Contains(
            result.Issues,
            issue => issue.Code ==
                     AcceptedEffectUseArbiterIssueCodes.MissingUseSeed);
    }

    private static AcceptedEffectUseArbiter Initialize(
        params CanonicalEffectUseSeed[] seeds) =>
        Initialize(seeds, Array.Empty<AcceptedEffectActivationTranscriptStamp>());

    private static AcceptedEffectUseArbiter Initialize(
        IReadOnlyList<CanonicalEffectUseSeed> seeds,
        IReadOnlyList<AcceptedEffectActivationTranscriptStamp> replay)
    {
        var result = AcceptedEffectUseArbiter.Initialize(seeds, replay);
        Assert.True(
            result.IsValid,
            string.Join(
                Environment.NewLine,
                result.Issues.Select(static issue => $"{issue.Code}: {issue.Message}")));
        return Assert.IsType<AcceptedEffectUseArbiter>(result.Arbiter);
    }

    private static EffectActivationCandidate Candidate(
        string effectId,
        string triggerId,
        string eventRef,
        int priority,
        bool consumesUse = false,
        string eventKind = "resource_damaged") =>
        new(
            Identity(effectId, triggerId, eventRef, eventKind),
            priority,
            consumesUse,
            PermanentAuthority(effectId));

    private static ResourcePendingAuthorityBinding PermanentAuthority(
        string effectId) =>
        new("permanent", effectId);

    private static ResourcePendingAuthorityBinding AcceptedApplicationAuthority(
        string eventRef) =>
        new("accepted_application", eventRef);

    private static EffectActivationCandidateIdentity Identity(
        string effectId,
        string triggerId,
        string eventRef,
        string eventKind = "resource_damaged",
        string? triggerEventRef = null) =>
        new(
            effectId,
            triggerId,
            eventKind,
            eventRef,
            triggerEventRef ?? eventRef + ":producer");

    private static AcceptedEffectActivationTranscriptStamp Stamp(
        string effectId,
        string triggerId,
        string eventRef,
        int priority,
        bool consumesUse,
        int? usesBefore,
        long activationOrdinal,
        string eventKind = "resource_damaged",
        ResourcePendingAuthorityBinding? effectAuthority = null) =>
        new(
            Identity(effectId, triggerId, eventRef, eventKind),
            effectAuthority ?? PermanentAuthority(effectId),
            priority,
            consumesUse,
            usesBefore,
            activationOrdinal);

    private static void AssertReadOnly<T>(IReadOnlyList<T> values)
    {
        Assert.False(values is T[]);
        if (values is IList<T> list)
        {
            Assert.True(list.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => list.Clear());
        }
    }
}
