using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AcceptedEffectBoundaryTranscriptTests
{
    [Fact]
    public void Builder_ParentAfterCurrentReleaseWaitsForOpenChildToClose()
    {
        var builder = new AcceptedEffectBoundaryTranscript.Builder();
        var identity = Identity("parent");
        var reaction = Reaction(identity);
        var candidate = Candidate(identity, reaction);
        var activation = Activation(identity);
        var parent = builder.OpenBoundary(
            parentBoundaryOrdinal: null,
            producer: null,
            identity.EventKind,
            identity.TriggerEventRef,
            producerTransitionId: null,
            producerExecutionSequence: null,
            producerMechanicsOrdinal: -1,
            new[] { candidate });
        builder.RecordAccepted(parent, activation, candidate);
        Assert.Null(BindEmpty(builder, parent));
        var child = builder.OpenBoundary(
            parent.BoundaryOrdinal,
            Producer(),
            "resource_damaged",
            "turn_2:boundary:child",
            "transition_child",
            producerExecutionSequence: 0,
            producerMechanicsOrdinal: builder.RecordMutationExecution(),
            Array.Empty<EffectAcceptedTurnPlanner
                .EffectResourceTriggerCandidate>());
        Assert.Null(BindEmpty(builder, child));

        var earlyRelease = builder.TryRelease(
            parent,
            activation,
            reaction,
            EffectReactionReleaseStage.AfterCurrentEvent);

        Assert.NotNull(earlyRelease);
        Assert.Equal(
            "effect_reaction_release_causal_boundary_incomplete",
            earlyRelease!.Code);
        var earlyClose = builder.CloseBoundary(parent);
        Assert.NotNull(earlyClose);
        Assert.Equal("effect_boundary_close_invalid", earlyClose!.Code);
        Assert.Null(builder.CloseBoundary(child));
        Assert.Null(builder.TryRelease(
            parent,
            activation,
            reaction,
            EffectReactionReleaseStage.AfterCurrentEvent));
        Assert.Null(builder.CloseBoundary(parent));
    }

    [Fact]
    public void Builder_PendingFrontierMustOwnAnUnresolvedAcceptedOutput()
    {
        var builder = new AcceptedEffectBoundaryTranscript.Builder();
        var boundary = builder.OpenBoundary(
            parentBoundaryOrdinal: null,
            producer: null,
            eventKind: "owner_turn_end",
            producerEventRef: "turn_2:boundary:no_pending",
            producerTransitionId: null,
            producerExecutionSequence: null,
            producerMechanicsOrdinal: -1,
            Array.Empty<EffectAcceptedTurnPlanner
                .EffectResourceTriggerCandidate>());
        Assert.Null(BindEmpty(builder, boundary));

        var issue = builder.SealPendingFrontier(boundary);

        Assert.NotNull(issue);
        Assert.Equal("effect_boundary_pending_frontier_invalid", issue!.Code);
    }

    [Fact]
    public void Builder_UseProjectionRejectsAnyOpenBoundary()
    {
        var builder = new AcceptedEffectBoundaryTranscript.Builder();
        var boundary = builder.OpenBoundary(
            parentBoundaryOrdinal: null,
            producer: null,
            eventKind: "owner_turn_end",
            producerEventRef: "turn_2:boundary:open",
            producerTransitionId: null,
            producerExecutionSequence: null,
            producerMechanicsOrdinal: -1,
            Array.Empty<EffectAcceptedTurnPlanner
                .EffectResourceTriggerCandidate>());
        Assert.Null(BindEmpty(builder, boundary));

        builder.SealUseProjection();
        var frozen = builder.Freeze();

        Assert.False(frozen.IsValid);
        Assert.Contains(
            frozen.Issues,
            issue => string.Equals(
                issue.Code,
                "effect_boundary_terminal_projection_invalid",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Builder_FirstFreezePermanentlySealsAllMutationMethods()
    {
        var builder = new AcceptedEffectBoundaryTranscript.Builder();
        builder.SealUseProjection();
        var frozen = builder.Freeze();

        Assert.True(frozen.IsValid);
        Assert.Throws<InvalidOperationException>(
            () => builder.RecordMutationExecution());
        Assert.Throws<InvalidOperationException>(
            () => builder.ReserveTerminalAvailability(null!, null!, null!));
        Assert.Same(frozen, builder.Freeze());
    }

    [Fact]
    public void Builder_TerminalRejectionRequiresEarlierAvailabilityAuthority()
    {
        var builder = new AcceptedEffectBoundaryTranscript.Builder();
        var identity = Identity("unproven_terminal");
        var candidate = Candidate(identity, Reaction(identity));
        var boundary = builder.OpenBoundary(
            parentBoundaryOrdinal: null,
            producer: null,
            identity.EventKind,
            identity.TriggerEventRef,
            producerTransitionId: null,
            producerExecutionSequence: null,
            producerMechanicsOrdinal: -1,
            new[] { candidate });
        Assert.Null(builder.RecordRejected(
            boundary,
            candidate,
            EffectActivationRejectionReason.EffectTerminal));
        Assert.Null(BindEmpty(builder, boundary));
        Assert.Null(builder.CloseBoundary(boundary));
        builder.SealUseProjection();

        var frozen = builder.Freeze();

        Assert.False(frozen.IsValid);
        Assert.Contains(
            frozen.Issues,
            issue => string.Equals(
                issue.Code,
                "effect_boundary_terminal_rejection_authority_missing",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Builder_TerminalRejectionSealsExactFrozenAvailabilitySubject()
    {
        var blockedOwner = BuildRejectedTargetTranscript(
            "effect_boundary_retarget_owner");
        var blockedTarget = BuildRejectedTargetTranscript(
            "effect_boundary_replacement_target");

        Assert.True(blockedOwner.IsValid, Format(blockedOwner.Issues));
        Assert.True(blockedTarget.IsValid, Format(blockedTarget.Issues));
        Assert.Equal(
            "effect_boundary_retarget_owner",
            Assert.Single(blockedOwner.Transcript!.RejectedActivations)
                .BlockedAvailabilityEffectId);
        Assert.Equal(
            "effect_boundary_replacement_target",
            Assert.Single(blockedTarget.Transcript!.RejectedActivations)
                .BlockedAvailabilityEffectId);
        Assert.NotEqual(
            blockedOwner.Transcript.Fingerprint,
            blockedTarget.Transcript.Fingerprint);

        var tampered = BuildRejectedTargetTranscript(
            "effect_boundary_unrelated");

        Assert.False(tampered.IsValid);
        Assert.Contains(
            tampered.Issues,
            issue => string.Equals(
                issue.Code,
                "effect_boundary_terminal_rejection_subject_invalid",
                StringComparison.Ordinal));
    }

    [Fact]
    public void TerminalReservationPrefix_RebindsTypedForeignTargetsAndOrdersByStableMechanics()
    {
        var first = BuildReservationReplayTranscript(
            "effect_replay_owner_a",
            "effect_replay_target_alpha_a",
            "effect_replay_target_beta_a",
            reverseReactionInput: false);
        var resubmitted = BuildReservationReplayTranscript(
            "effect_replay_owner_b",
            "effect_replay_target_alpha_b",
            "effect_replay_target_beta_b",
            reverseReactionInput: true);
        var drifted = BuildReservationReplayTranscript(
            "effect_replay_owner_b",
            "effect_replay_target_alpha_b",
            "effect_replay_target_beta_b",
            reverseReactionInput: true,
            alphaTargetAuthorityId:
                "turn_2:replay_target_alpha_application_drifted");

        Assert.NotEqual(first.Fingerprint, resubmitted.Fingerprint);
        Assert.Equal(
            first.CreateActivationPrefixFingerprints()[1],
            resubmitted.CreateActivationPrefixFingerprints()[1]);
        Assert.NotEqual(
            first.CreateActivationPrefixFingerprints()[1],
            drifted.CreateActivationPrefixFingerprints()[1]);
        Assert.Equal(
            new[]
            {
                "component_replay_reservation_alpha",
                "component_replay_reservation_beta"
            },
            ResolveReservationComponentOrder(first));
        Assert.Equal(
            ResolveReservationComponentOrder(first),
            ResolveReservationComponentOrder(resubmitted));
    }

    [Fact]
    public void ReserveTerminalAvailability_RejectsDuplicateReplayStableOrderKey()
    {
        var builder = new AcceptedEffectBoundaryTranscript.Builder();
        var identity = Identity("effect_duplicate_owner", "duplicate_owner");
        var targetAuthority = new ResourcePendingAuthorityBinding(
            "accepted_application",
            "turn_2:duplicate_target_application");
        var canonical = ReplacementReaction(
            identity,
            "effect_duplicate_target_a",
            targetAuthority) with
        {
            Dependency = "after_current_event"
        };
        var rebound = canonical with
        {
            ReplacementTarget = new EffectReplayIdentity(
                "effect_duplicate_target_b",
                targetAuthority)
        };
        var candidate = Candidate(
            identity,
            new[] { canonical, rebound },
            new ResourcePendingAuthorityBinding(
                "accepted_application",
                "turn_2:duplicate_owner_application"));
        var activation = Activation(identity);
        var boundary = builder.OpenBoundary(
            parentBoundaryOrdinal: null,
            producer: null,
            identity.EventKind,
            identity.TriggerEventRef,
            producerTransitionId: null,
            producerExecutionSequence: null,
            producerMechanicsOrdinal: -1,
            new[] { candidate });
        builder.RecordAccepted(boundary, activation, candidate);
        Assert.Null(BindEmpty(builder, boundary));

        var issue = builder.ReserveTerminalAvailability(
            boundary,
            activation,
            candidate);

        Assert.NotNull(issue);
        Assert.Equal(
            "effect_terminal_reservation_order_key_duplicate",
            issue!.Code);
    }

    private static AcceptedEffectBoundaryTranscriptResult
        BuildRejectedTargetTranscript(string blockedAvailabilityEffectId)
    {
        var builder = new AcceptedEffectBoundaryTranscript.Builder();
        RecordTerminalRelease(
            builder,
            effectId: "effect_boundary_replacement_target",
            suffix: "release_target",
            activationOrdinal: 0);
        RecordTerminalRelease(
            builder,
            effectId: "effect_boundary_retarget_owner",
            suffix: "release_owner",
            activationOrdinal: 1);

        var identity = Identity(
            "effect_boundary_retarget_owner",
            "retarget");
        var candidate = Candidate(
            identity,
            ReplacementReaction(
                identity,
                "effect_boundary_replacement_target"));
        var boundary = builder.OpenBoundary(
            parentBoundaryOrdinal: null,
            producer: null,
            identity.EventKind,
            identity.TriggerEventRef,
            producerTransitionId: null,
            producerExecutionSequence: null,
            producerMechanicsOrdinal: -1,
            new[] { candidate });
        Assert.Null(builder.RecordRejected(
            boundary,
            candidate,
            EffectActivationRejectionReason.EffectTerminal,
            blockedAvailabilityEffectId));
        Assert.Null(BindEmpty(builder, boundary));
        Assert.Null(builder.CloseBoundary(boundary));
        builder.SealUseProjection();
        return builder.Freeze();
    }

    private static AcceptedEffectBoundaryTranscript
        BuildReservationReplayTranscript(
            string ownerEffectId,
            string alphaTargetEffectId,
            string betaTargetEffectId,
            bool reverseReactionInput,
            string alphaTargetAuthorityId =
                "turn_2:replay_target_alpha_application")
    {
        var builder = new AcceptedEffectBoundaryTranscript.Builder();
        var identity = Identity(ownerEffectId, "replay_reservation_owner");
        var alpha = ReplacementReaction(
            identity,
            alphaTargetEffectId,
            new ResourcePendingAuthorityBinding(
                "accepted_application",
                alphaTargetAuthorityId)) with
        {
            EventRef = "turn_2:replay_reservation:reaction:alpha",
            ComponentId = "component_replay_reservation_alpha",
            ComponentPriority = 100,
            Dependency = "after_current_event"
        };
        var beta = ReplacementReaction(
            identity,
            betaTargetEffectId,
            new ResourcePendingAuthorityBinding(
                "accepted_application",
                "turn_2:replay_target_beta_application")) with
        {
            EventRef = "turn_2:replay_reservation:reaction:beta",
            ComponentId = "component_replay_reservation_beta",
            ComponentPriority = 100,
            Dependency = "after_current_event"
        };
        var reactions = reverseReactionInput
            ? new[] { beta, alpha }
            : new[] { alpha, beta };
        var candidate = Candidate(
            identity,
            reactions,
            new ResourcePendingAuthorityBinding(
                "accepted_application",
                "turn_2:replay_owner_application"));
        var activation = Activation(identity);
        var boundary = builder.OpenBoundary(
            parentBoundaryOrdinal: null,
            producer: null,
            identity.EventKind,
            identity.TriggerEventRef,
            producerTransitionId: null,
            producerExecutionSequence: null,
            producerMechanicsOrdinal: -1,
            new[] { candidate });
        builder.RecordAccepted(boundary, activation, candidate);
        Assert.Null(BindEmpty(builder, boundary));
        Assert.Null(builder.ReserveTerminalAvailability(
            boundary,
            activation,
            candidate));
        foreach (var reaction in reactions
                     .OrderBy(static value => value.ComponentPriority)
                     .ThenBy(
                         static value => value.ComponentId,
                         StringComparer.Ordinal))
        {
            Assert.Null(builder.TryRelease(
                boundary,
                activation,
                reaction,
                EffectReactionReleaseStage.AfterCurrentEvent));
        }
        Assert.Null(builder.CloseBoundary(boundary));

        var nextIdentity = Identity(
            "effect_replay_reservation_followup",
            "replay_reservation_followup");
        var nextCandidate = Candidate(
            nextIdentity,
            Array.Empty<EffectReactionExecution>(),
            new ResourcePendingAuthorityBinding(
                "permanent",
                nextIdentity.EffectId));
        var nextActivation = Activation(nextIdentity, activationOrdinal: 1);
        var nextBoundary = builder.OpenBoundary(
            parentBoundaryOrdinal: null,
            producer: null,
            nextIdentity.EventKind,
            nextIdentity.TriggerEventRef,
            producerTransitionId: null,
            producerExecutionSequence: null,
            producerMechanicsOrdinal: -1,
            new[] { nextCandidate });
        builder.RecordAccepted(nextBoundary, nextActivation, nextCandidate);
        Assert.Null(BindEmpty(builder, nextBoundary));
        Assert.Null(builder.CloseBoundary(nextBoundary));
        builder.SealUseProjection();
        var result = builder.Freeze();
        Assert.True(result.IsValid, Format(result.Issues));
        return result.Transcript!;
    }

    private static IReadOnlyList<string> ResolveReservationComponentOrder(
        AcceptedEffectBoundaryTranscript transcript)
    {
        var candidate = transcript.AcceptedActivations.Single(value =>
            value.Activation.Stamp.ActivationOrdinal == 0).Candidate;
        return transcript.TerminalAvailabilityReservations
            .OrderBy(static value => value.MechanicsOrdinal)
            .Select(reservation => candidate.ReactionOutputs.Single(reaction =>
                string.Equals(
                    AcceptedMechanicsPlanner
                        .CreateReactionCandidateOutputFingerprint(reaction),
                    reservation.ReactionFingerprint,
                    StringComparison.Ordinal)).ComponentId)
            .ToArray();
    }

    private static void RecordTerminalRelease(
        AcceptedEffectBoundaryTranscript.Builder builder,
        string effectId,
        string suffix,
        long activationOrdinal)
    {
        var identity = Identity(effectId, suffix);
        var reaction = TerminalReaction(identity);
        var candidate = Candidate(identity, reaction);
        var activation = Activation(identity, activationOrdinal);
        var boundary = builder.OpenBoundary(
            parentBoundaryOrdinal: null,
            producer: null,
            identity.EventKind,
            identity.TriggerEventRef,
            producerTransitionId: null,
            producerExecutionSequence: null,
            producerMechanicsOrdinal: -1,
            new[] { candidate });
        builder.RecordAccepted(boundary, activation, candidate);
        Assert.Null(BindEmpty(builder, boundary));
        Assert.Null(builder.TryRelease(
            boundary,
            activation,
            reaction,
            EffectReactionReleaseStage.BeforeCurrentEvent));
        Assert.Null(builder.CloseBoundary(boundary));
    }

    private static EffectBoundaryTranscriptIssue? BindEmpty(
        AcceptedEffectBoundaryTranscript.Builder builder,
        EffectEventBoundaryStamp boundary) =>
        builder.BindCausalClosure(
            boundary,
            Array.Empty<string>(),
            new Dictionary<string, string>(StringComparer.Ordinal));

    private static EffectActivationCandidateIdentity Identity(string suffix) =>
        Identity("effect_boundary_" + suffix, suffix);

    private static EffectActivationCandidateIdentity Identity(
        string effectId,
        string suffix) =>
        new(
            effectId,
            "trigger_boundary_" + suffix,
            "owner_turn_end",
            "turn_2:boundary:" + suffix + ":activation",
            "turn_2:boundary:" + suffix);

    private static AcceptedEffectActivation Activation(
        EffectActivationCandidateIdentity identity,
        long activationOrdinal = 0) =>
        new(
            new AcceptedEffectActivationTranscriptStamp(
                identity,
                EffectAuthority: new ResourcePendingAuthorityBinding(
                    "permanent",
                    identity.EffectId),
                Priority: 100,
                ConsumesUse: false,
                UsesBefore: null,
                ActivationOrdinal: activationOrdinal),
            UsesAfter: null,
            EffectTerminal: false);

    private static EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate
        Candidate(
            EffectActivationCandidateIdentity identity,
            EffectReactionExecution reaction) =>
        Candidate(
            identity,
            new[] { reaction },
            new ResourcePendingAuthorityBinding(
                "permanent",
                identity.EffectId));

    private static EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate
        Candidate(
            EffectActivationCandidateIdentity identity,
            IReadOnlyList<EffectReactionExecution> reactions,
            ResourcePendingAuthorityBinding effectAuthority) =>
        new(
            new EffectActivationCandidate(
                identity,
                Priority: 100,
                ConsumesUse: false,
                EffectAuthority: effectAuthority),
            useSeed: null,
            producer: null,
            plannedMutationKeys: Array.Empty<ResourceOperationKey>(),
            plannedComponentIds: Array.Empty<string>(),
            plannedComponentIdsByMutation:
                new Dictionary<ResourceOperationKey, string>(),
            pendingOutputs: Array.Empty<EffectAcceptedTurnPlanner
                .EffectBoundedResourceResolution>(),
            reactionOutputs: reactions,
            origin: EffectAcceptedTurnPlanner.EffectResourceCandidateOrigin.Empty,
            effectAuthority: effectAuthority);

    private static EffectReactionExecution Reaction(
        EffectActivationCandidateIdentity identity) =>
        new(
            EventRef: "turn_2:boundary:parent:reaction",
            TriggerEventRef: identity.TriggerEventRef,
            CausalEventRef: identity.TriggerEventRef,
            Turn: 2,
            EventKind: identity.EventKind,
            Target: new EffectTargetKey(
                "mortal_world",
                "player",
                "player_current"),
            EffectId: identity.EffectId,
            TriggerId: identity.TriggerId,
            ComponentId: "component_boundary_parent_reaction",
            ResultKind: "event_outcome",
            Dependency: "after_current_event",
            AfterComponentId: null,
            MaxExpansion: 1,
            DownstreamSource: null,
            Parameters: null);

    private static EffectReactionExecution TerminalReaction(
        EffectActivationCandidateIdentity identity) =>
        Reaction(identity) with
        {
            EventRef = identity.TriggerEventRef + ":remove",
            ComponentId = "component_" + identity.TriggerId + "_remove",
            ResultKind = "remove",
            Dependency = "before_current_event"
        };

    private static EffectReactionExecution ReplacementReaction(
        EffectActivationCandidateIdentity identity,
        string replacementTargetEffectId,
        ResourcePendingAuthorityBinding? replacementTargetAuthority = null) =>
        Reaction(identity) with
        {
            EventRef = identity.TriggerEventRef + ":replace",
            ComponentId = "component_" + identity.TriggerId + "_replace",
            ResultKind = "apply_definition",
            Dependency = "before_current_event",
            ReplacementTarget = new EffectReplayIdentity(
                replacementTargetEffectId,
                replacementTargetAuthority ??
                new ResourcePendingAuthorityBinding(
                    "permanent",
                    replacementTargetEffectId))
        };

    private static string Format(
        IReadOnlyList<EffectBoundaryTranscriptIssue> issues) =>
        string.Join(
            Environment.NewLine,
            issues.Select(static issue =>
                $"{issue.Code}: {issue.Expected}; actual={issue.Actual}"));

    private static ResourceOperationKey Producer() =>
        new(
            "turn_2:boundary:child:producer",
            "effect_component",
            "boundary_child_source",
            new ResourceCoordinate(
                "mortal_world",
                ResourceOwnerKind.Player,
                "player_current",
                "health"),
            ResourceOperation.Damage);
}
