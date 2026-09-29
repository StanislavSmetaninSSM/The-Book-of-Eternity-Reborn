using System.Collections.ObjectModel;
using System.Globalization;

namespace BookOfEternityClient.Services;

internal enum EffectReactionReleaseStage
{
    BeforeCurrentEvent,
    AfterComponent,
    AfterCurrentEvent
}

internal enum EffectTerminalAvailabilityReservationKind
{
    LastUse,
    AfterCurrentReaction
}

internal sealed record EffectAcceptedPlanAuthorityStamp(
    string InputFingerprint,
    string CarrierAuthorityFingerprint,
    string SourceAuthorityFingerprint,
    string TargetAuthorityFingerprint,
    string SkillScopeAuthorityFingerprint);

internal sealed record EffectEventBoundaryStamp(
    long BoundaryOrdinal,
    long? ParentBoundaryOrdinal,
    ResourceOperationKey? Producer,
    string EventKind,
    string ProducerEventRef,
    string? ProducerTransitionId,
    int? ProducerExecutionSequence,
    long ProducerMechanicsOrdinal,
    long OpenMechanicsOrdinal,
    string CandidateBatchFingerprint);

internal sealed record EffectEventBoundaryCloseStamp(
    EffectEventBoundaryStamp Boundary,
    long MechanicsOrdinal);

internal sealed record EffectBoundaryCausalClosureStamp(
    EffectEventBoundaryStamp Boundary,
    IReadOnlyList<string> OperationIds,
    IReadOnlyList<string> ReplayStableOperationKeys,
    string Fingerprint);

internal sealed record AcceptedEffectBoundaryActivation(
    EffectEventBoundaryStamp Boundary,
    AcceptedEffectActivation Activation,
    EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate Candidate,
    string CandidateFingerprint,
    long MechanicsOrdinal);

internal sealed record RejectedEffectBoundaryActivation(
    EffectEventBoundaryStamp Boundary,
    EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate Candidate,
    string CandidateFingerprint,
    EffectActivationRejectionReason Reason,
    string? BlockedAvailabilityEffectId);

internal sealed record AppliedEffectComponentEvidence(
    EffectEventBoundaryStamp Boundary,
    EffectActivationCandidateIdentity Activation,
    ResourceOperationKey Mutation,
    string ComponentId,
    ResourceTransition Transition,
    long MechanicsOrdinal);

internal enum ResourceMutationExecutionKind
{
    Applied,
    Replay
}

internal sealed record AcceptedResourceMutationEvidence(
    ResourceOperationKey Mutation,
    ResourceTransition Transition,
    ResourceMutationExecutionKind ExecutionKind,
    long MechanicsOrdinal);

internal sealed record ReleasedEffectReaction(
    EffectEventBoundaryStamp Boundary,
    AcceptedEffectActivationTranscriptStamp Activation,
    EffectReactionExecution Reaction,
    EffectReactionReleaseStage Stage,
    long MechanicsOrdinal);

internal sealed record EffectTerminalAvailabilityReservation(
    EffectEventBoundaryStamp Boundary,
    AcceptedEffectActivationTranscriptStamp Activation,
    EffectReplayIdentity Subject,
    EffectTerminalAvailabilityReservationKind Kind,
    string? ReactionFingerprint,
    string? ReplayStableOrderKey,
    long MechanicsOrdinal);

internal sealed record EffectBoundaryTranscriptIssue(
    string Code,
    string Expected,
    string Actual);

internal sealed record AcceptedEffectBoundaryTranscriptResult(
    AcceptedEffectBoundaryTranscript? Transcript,
    IReadOnlyList<EffectBoundaryTranscriptIssue> Issues)
{
    internal bool IsValid => Transcript != null && Issues.Count == 0;
}

internal sealed record AcceptedEffectBoundaryPrefixResult(
    AcceptedEffectBoundaryTranscript.ClosedPrefix? Prefix,
    IReadOnlyList<EffectBoundaryTranscriptIssue> Issues)
{
    internal bool IsValid => Prefix != null && Issues.Count == 0;
}

internal sealed class AcceptedEffectBoundaryTranscript
{
    private readonly EffectEventBoundaryStamp[] _boundaries;
    private readonly EffectEventBoundaryCloseStamp[] _boundaryCloses;
    private readonly EffectBoundaryCausalClosureStamp[] _causalClosures;
    private readonly AcceptedEffectBoundaryActivation[] _acceptedActivations;
    private readonly RejectedEffectBoundaryActivation[] _rejectedActivations;
    private readonly AppliedEffectComponentEvidence[] _appliedComponentEvidence;
    private readonly AcceptedResourceMutationEvidence[] _resourceMutations;
    private readonly ReleasedEffectReaction[] _releasedReactions;
    private readonly EffectTerminalAvailabilityReservation[]
        _terminalAvailabilityReservations;
    private readonly Dictionary<
        EffectReactionExpansionKey,
        EffectReactionExpansionUsage> _expansionUsage;
    private readonly EffectAcceptedPlanAuthorityStamp? _planAuthority;
    private readonly long? _pendingFrontierBoundaryOrdinal;

    private AcceptedEffectBoundaryTranscript(
        EffectAcceptedPlanAuthorityStamp? planAuthority,
        IReadOnlyList<EffectEventBoundaryStamp> boundaries,
        IReadOnlyList<EffectEventBoundaryCloseStamp> boundaryCloses,
        IReadOnlyList<EffectBoundaryCausalClosureStamp> causalClosures,
        IReadOnlyList<AcceptedEffectBoundaryActivation> acceptedActivations,
        IReadOnlyList<RejectedEffectBoundaryActivation> rejectedActivations,
        IReadOnlyList<AppliedEffectComponentEvidence> appliedComponentEvidence,
        IReadOnlyList<AcceptedResourceMutationEvidence> resourceMutations,
        IReadOnlyList<ReleasedEffectReaction> releasedReactions,
        IReadOnlyList<EffectTerminalAvailabilityReservation>
            terminalAvailabilityReservations,
        IReadOnlyDictionary<
            EffectReactionExpansionKey,
            EffectReactionExpansionUsage> expansionUsage,
        long? useProjectionOrdinal,
        long? pendingFrontierBoundaryOrdinal)
    {
        _planAuthority = planAuthority is null ? null : planAuthority with { };
        _boundaries = boundaries.ToArray();
        _boundaryCloses = boundaryCloses.ToArray();
        _causalClosures = causalClosures.Select(Clone).ToArray();
        _acceptedActivations = acceptedActivations.ToArray();
        _rejectedActivations = rejectedActivations.ToArray();
        _appliedComponentEvidence = appliedComponentEvidence.ToArray();
        _resourceMutations = resourceMutations.ToArray();
        _releasedReactions = releasedReactions
            .Select(static released => released with
            {
                Reaction = CloneReaction(released.Reaction)
            })
            .ToArray();
        _terminalAvailabilityReservations =
            terminalAvailabilityReservations.ToArray();
        _expansionUsage = expansionUsage.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value);
        UseProjectionOrdinal = useProjectionOrdinal;
        _pendingFrontierBoundaryOrdinal = pendingFrontierBoundaryOrdinal;
        Fingerprint = CreateFingerprint(
            _planAuthority,
            _boundaries,
            _boundaryCloses,
            _causalClosures,
            _acceptedActivations,
            _rejectedActivations,
            _appliedComponentEvidence,
            _resourceMutations,
            _releasedReactions,
            _terminalAvailabilityReservations,
            _expansionUsage,
            useProjectionOrdinal,
            pendingFrontierBoundaryOrdinal);
    }

    internal EffectAcceptedPlanAuthorityStamp? PlanAuthority =>
        _planAuthority is null ? null : _planAuthority with { };

    internal IReadOnlyList<EffectEventBoundaryStamp> Boundaries =>
        Array.AsReadOnly(_boundaries.ToArray());

    internal IReadOnlyList<EffectEventBoundaryCloseStamp> BoundaryCloses =>
        Array.AsReadOnly(_boundaryCloses.ToArray());

    internal IReadOnlyList<EffectBoundaryCausalClosureStamp> CausalClosures =>
        Array.AsReadOnly(_causalClosures.Select(Clone).ToArray());

    internal IReadOnlyList<AcceptedEffectBoundaryActivation> AcceptedActivations =>
        Array.AsReadOnly(_acceptedActivations.ToArray());

    internal IReadOnlyList<RejectedEffectBoundaryActivation> RejectedActivations =>
        Array.AsReadOnly(_rejectedActivations.ToArray());

    internal IReadOnlyList<AppliedEffectComponentEvidence> AppliedComponentEvidence =>
        Array.AsReadOnly(_appliedComponentEvidence.ToArray());

    internal IReadOnlyList<AcceptedResourceMutationEvidence> ResourceMutations =>
        Array.AsReadOnly(_resourceMutations.ToArray());

    internal IReadOnlyList<ReleasedEffectReaction> ReleasedReactions =>
        Array.AsReadOnly(_releasedReactions
            .Select(static released => released with
            {
                Reaction = CloneReaction(released.Reaction)
            })
            .ToArray());

    internal IReadOnlyList<EffectTerminalAvailabilityReservation>
        TerminalAvailabilityReservations =>
        Array.AsReadOnly(_terminalAvailabilityReservations.ToArray());

    internal IReadOnlyDictionary<
        EffectReactionExpansionKey,
        EffectReactionExpansionUsage> ExpansionUsage =>
        new ReadOnlyDictionary<
            EffectReactionExpansionKey,
            EffectReactionExpansionUsage>(
            _expansionUsage.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value));

    internal int ExpansionCount => _expansionUsage.Values.Sum(
        static usage => usage.Count);

    internal long? UseProjectionOrdinal { get; }

    internal long? PendingFrontierBoundaryOrdinal =>
        _pendingFrontierBoundaryOrdinal;

    internal bool IsComplete =>
        _pendingFrontierBoundaryOrdinal == null && UseProjectionOrdinal != null;

    internal string Fingerprint { get; }

    internal IReadOnlyDictionary<long, string>
        CreateActivationPrefixFingerprints()
    {
        var acceptedByIdentity = _acceptedActivations.ToDictionary(
            static accepted => accepted.Activation.Stamp.Identity,
            static accepted => accepted);
        var events = new List<(long Ordinal, string Fingerprint)>();
        foreach (var boundary in _boundaries)
        {
            using var builder = new ResourceFingerprintBuilder(
                "effect-boundary-prefix-open-v3");
            AppendLong(builder, boundary.BoundaryOrdinal);
            AppendNullable(builder, boundary.ParentBoundaryOrdinal);
            AppendOperationKey(builder, boundary.Producer);
            builder.Append(boundary.EventKind);
            builder.Append(boundary.ProducerEventRef);
            AppendNullable(builder, boundary.ProducerExecutionSequence);
            AppendLong(builder, boundary.ProducerMechanicsOrdinal);
            builder.Append(boundary.CandidateBatchFingerprint);
            events.Add((boundary.OpenMechanicsOrdinal, builder.Build()));
        }
        foreach (var accepted in _acceptedActivations)
        {
            using var builder = new ResourceFingerprintBuilder(
                "effect-boundary-prefix-accepted-v2");
            AppendLong(builder, accepted.Boundary.BoundaryOrdinal);
            AcceptedMechanicsPlanner.AppendReplayStableActivationIdentity(
                builder,
                accepted.Activation.Stamp.Identity,
                accepted.Candidate.EffectAuthority);
            builder.Append(accepted.Activation.Stamp.Priority);
            builder.Append(accepted.Activation.Stamp.ConsumesUse);
            AppendNullable(builder, accepted.Activation.Stamp.UsesBefore);
            AppendLong(builder, accepted.Activation.Stamp.ActivationOrdinal);
            AppendNullable(builder, accepted.Activation.UsesAfter);
            builder.Append(accepted.Activation.EffectTerminal);
            builder.Append(accepted.CandidateFingerprint);
            events.Add((accepted.MechanicsOrdinal, builder.Build()));
        }
        foreach (var reservation in _terminalAvailabilityReservations)
        {
            using var builder = new ResourceFingerprintBuilder(
                "effect-boundary-prefix-terminal-reservation-v4");
            AppendLong(builder, reservation.Boundary.BoundaryOrdinal);
            acceptedByIdentity.TryGetValue(
                reservation.Activation.Identity,
                out var reservedActivation);
            if (reservedActivation == null)
            {
                AppendActivationIdentity(
                    builder,
                    reservation.Activation.Identity);
            }
            else
            {
                AcceptedMechanicsPlanner.AppendReplayStableActivationIdentity(
                    builder,
                    reservation.Activation.Identity,
                    reservedActivation.Candidate.EffectAuthority);
            }
            AppendLong(builder, reservation.Activation.ActivationOrdinal);
            AcceptedMechanicsPlanner.AppendReplayStableEffectIdentity(
                builder,
                reservation.Subject.EffectId,
                reservation.Subject.Authority);
            builder.Append(reservation.Kind.ToString());
            AppendNullable(builder, reservation.ReplayStableOrderKey);
            events.Add((reservation.MechanicsOrdinal, builder.Build()));
        }
        foreach (var mutation in _resourceMutations)
        {
            using var builder = new ResourceFingerprintBuilder(
                "effect-boundary-prefix-mutation-v1");
            AppendOperationKey(builder, mutation.Mutation);
            builder.Append(CreateReplayStableTransitionFingerprint(
                mutation.Transition));
            builder.Append(mutation.ExecutionKind.ToString());
            var evidence = _appliedComponentEvidence
                .Where(value => value.MechanicsOrdinal == mutation.MechanicsOrdinal)
                .OrderBy(static value => value.ComponentId, StringComparer.Ordinal)
                .ThenBy(value => acceptedByIdentity.TryGetValue(
                        value.Activation,
                        out var accepted)
                    ? accepted.Activation.Stamp.ActivationOrdinal
                    : long.MaxValue)
                .ToArray();
            builder.Append(evidence.Length);
            foreach (var applied in evidence)
            {
                AppendLong(builder, applied.Boundary.BoundaryOrdinal);
                if (acceptedByIdentity.TryGetValue(
                        applied.Activation,
                        out var accepted))
                {
                    AcceptedMechanicsPlanner
                        .AppendReplayStableActivationIdentity(
                            builder,
                            applied.Activation,
                            accepted.Candidate.EffectAuthority);
                }
                else
                {
                    AppendActivationIdentity(builder, applied.Activation);
                }
                builder.Append(applied.ComponentId);
            }
            events.Add((mutation.MechanicsOrdinal, builder.Build()));
        }
        foreach (var release in _releasedReactions)
        {
            using var builder = new ResourceFingerprintBuilder(
                "effect-boundary-prefix-release-v2");
            AppendLong(builder, release.Boundary.BoundaryOrdinal);
            if (acceptedByIdentity.TryGetValue(
                    release.Activation.Identity,
                    out var accepted))
            {
                AcceptedMechanicsPlanner.AppendReplayStableActivationIdentity(
                    builder,
                    release.Activation.Identity,
                    accepted.Candidate.EffectAuthority);
                builder.Append(AcceptedMechanicsPlanner
                    .CreateReplayStableReactionCandidateOutputFingerprint(
                        release.Reaction,
                        accepted.Activation.Stamp.Identity.EffectId,
                        accepted.Candidate.EffectAuthority));
            }
            else
            {
                AppendActivationIdentity(builder, release.Activation.Identity);
                builder.Append(AcceptedMechanicsPlanner
                    .CreateReactionCandidateOutputFingerprint(
                        release.Reaction));
            }
            builder.Append(release.Stage.ToString());
            events.Add((release.MechanicsOrdinal, builder.Build()));
        }
        foreach (var close in _boundaryCloses)
        {
            using var builder = new ResourceFingerprintBuilder(
                "effect-boundary-prefix-close-v1");
            AppendLong(builder, close.Boundary.BoundaryOrdinal);
            var closure = _causalClosures.Single(value =>
                value.Boundary == close.Boundary);
            builder.Append(closure.Fingerprint);
            events.Add((close.MechanicsOrdinal, builder.Build()));
        }

        var result = new Dictionary<long, string>();
        string? priorPrefix = null;
        foreach (var mechanicsEvent in events
                     .OrderBy(static value => value.Ordinal))
        {
            using var builder = new ResourceFingerprintBuilder(
                "effect-boundary-transcript-prefix-v1");
            AppendNullable(builder, priorPrefix);
            AppendLong(builder, mechanicsEvent.Ordinal);
            builder.Append(mechanicsEvent.Fingerprint);
            priorPrefix = builder.Build();
            var accepted = _acceptedActivations.FirstOrDefault(value =>
                value.MechanicsOrdinal == mechanicsEvent.Ordinal);
            if (accepted != null)
            {
                result.Add(
                    accepted.Activation.Stamp.ActivationOrdinal,
                    priorPrefix);
            }
        }
        return new ReadOnlyDictionary<long, string>(result);
    }

    private static EffectBoundaryCausalClosureStamp Clone(
        EffectBoundaryCausalClosureStamp value) =>
        value with
        {
            OperationIds = Array.AsReadOnly(value.OperationIds.ToArray()),
            ReplayStableOperationKeys = Array.AsReadOnly(
                value.ReplayStableOperationKeys.ToArray())
        };

    private static EffectReactionExecution CloneReaction(
        EffectReactionExecution value) =>
        value with
        {
            DownstreamSource = value.DownstreamSource is { } source
                ? source with
                {
                    Definition = source.Definition.DeepClone().AsObject()
                }
                : null,
            Parameters = value.Parameters?.DeepClone().AsObject()
        };

    private static string CreateFingerprint(
        EffectAcceptedPlanAuthorityStamp? planAuthority,
        IReadOnlyList<EffectEventBoundaryStamp> boundaries,
        IReadOnlyList<EffectEventBoundaryCloseStamp> boundaryCloses,
        IReadOnlyList<EffectBoundaryCausalClosureStamp> causalClosures,
        IReadOnlyList<AcceptedEffectBoundaryActivation> activations,
        IReadOnlyList<RejectedEffectBoundaryActivation> rejectedActivations,
        IReadOnlyList<AppliedEffectComponentEvidence> evidence,
        IReadOnlyList<AcceptedResourceMutationEvidence> resourceMutations,
        IReadOnlyList<ReleasedEffectReaction> releases,
        IReadOnlyList<EffectTerminalAvailabilityReservation>
            terminalAvailabilityReservations,
        IReadOnlyDictionary<
            EffectReactionExpansionKey,
            EffectReactionExpansionUsage> expansion,
        long? useProjectionOrdinal,
        long? pendingFrontierBoundaryOrdinal)
    {
        using var material = new ResourceFingerprintBuilder(
            "effect-boundary-transcript-v8");
        material.Append(planAuthority != null);
        if (planAuthority != null)
        {
            material.Append(planAuthority.InputFingerprint);
            material.Append(planAuthority.CarrierAuthorityFingerprint);
            material.Append(planAuthority.SourceAuthorityFingerprint);
            material.Append(planAuthority.TargetAuthorityFingerprint);
            material.Append(planAuthority.SkillScopeAuthorityFingerprint);
        }
        material.Append(boundaries.Count);
        foreach (var boundary in boundaries)
        {
            AppendLong(material, boundary.BoundaryOrdinal);
            AppendNullable(material, boundary.ParentBoundaryOrdinal);
            AppendOperationKey(material, boundary.Producer);
            material.Append(boundary.EventKind);
            material.Append(boundary.ProducerEventRef);
            AppendNullable(material, boundary.ProducerTransitionId);
            AppendNullable(material, boundary.ProducerExecutionSequence);
            AppendLong(material, boundary.ProducerMechanicsOrdinal);
            AppendLong(material, boundary.OpenMechanicsOrdinal);
            material.Append(boundary.CandidateBatchFingerprint);
        }
        material.Append(boundaryCloses.Count);
        foreach (var close in boundaryCloses)
        {
            AppendLong(material, close.Boundary.BoundaryOrdinal);
            AppendLong(material, close.MechanicsOrdinal);
        }
        material.Append(causalClosures.Count);
        foreach (var closure in causalClosures)
        {
            AppendLong(material, closure.Boundary.BoundaryOrdinal);
            material.Append(closure.Fingerprint);
            material.Append(closure.OperationIds.Count);
            foreach (var operationId in closure.OperationIds)
                material.Append(operationId);
            material.Append(closure.ReplayStableOperationKeys.Count);
            foreach (var operationKey in closure.ReplayStableOperationKeys)
                material.Append(operationKey);
        }
        material.Append(activations.Count);
        foreach (var activation in activations)
        {
            AppendLong(material, activation.Boundary.BoundaryOrdinal);
            AppendActivationIdentity(
                material,
                activation.Activation.Stamp.Identity);
            material.Append(activation.Activation.Stamp.Priority);
            material.Append(activation.Activation.Stamp.ConsumesUse);
            AppendNullable(material, activation.Activation.Stamp.UsesBefore);
            AppendLong(
                material,
                activation.Activation.Stamp.ActivationOrdinal);
            AppendNullable(material, activation.Activation.UsesAfter);
            material.Append(activation.Activation.EffectTerminal);
            material.Append(activation.CandidateFingerprint);
            AppendLong(material, activation.MechanicsOrdinal);
        }
        material.Append(rejectedActivations.Count);
        foreach (var rejected in rejectedActivations)
        {
            AppendLong(material, rejected.Boundary.BoundaryOrdinal);
            AppendActivationIdentity(material, rejected.Candidate.Activation.Identity);
            material.Append(rejected.CandidateFingerprint);
            material.Append(rejected.Reason.ToString());
            AppendNullable(material, rejected.BlockedAvailabilityEffectId);
        }
        material.Append(evidence.Count);
        foreach (var applied in evidence)
        {
            AppendLong(material, applied.Boundary.BoundaryOrdinal);
            AppendActivationIdentity(material, applied.Activation);
            AppendOperationKey(material, applied.Mutation);
            material.Append(applied.ComponentId);
            material.Append(CreateTransitionFingerprint(applied.Transition));
            AppendLong(material, applied.MechanicsOrdinal);
        }
        material.Append(resourceMutations.Count);
        foreach (var mutation in resourceMutations)
        {
            AppendOperationKey(material, mutation.Mutation);
            material.Append(CreateTransitionFingerprint(mutation.Transition));
            material.Append(mutation.ExecutionKind.ToString());
            AppendLong(material, mutation.MechanicsOrdinal);
        }
        material.Append(releases.Count);
        foreach (var release in releases)
        {
            AppendLong(material, release.Boundary.BoundaryOrdinal);
            AppendActivationIdentity(material, release.Activation.Identity);
            AppendLong(material, release.Activation.ActivationOrdinal);
            material.Append(
                AcceptedMechanicsPlanner
                    .CreateReactionCandidateOutputFingerprint(release.Reaction));
            material.Append(release.Stage.ToString());
            AppendLong(material, release.MechanicsOrdinal);
        }
        material.Append(terminalAvailabilityReservations.Count);
        foreach (var reservation in terminalAvailabilityReservations)
        {
            AppendLong(material, reservation.Boundary.BoundaryOrdinal);
            AppendActivationIdentity(material, reservation.Activation.Identity);
            AppendLong(material, reservation.Activation.ActivationOrdinal);
            material.Append(reservation.Subject.EffectId);
            material.Append(reservation.Subject.Authority.BindingKind);
            material.Append(reservation.Subject.Authority.AuthorityId);
            material.Append(reservation.Kind.ToString());
            AppendNullable(material, reservation.ReactionFingerprint);
            AppendNullable(material, reservation.ReplayStableOrderKey);
            AppendLong(material, reservation.MechanicsOrdinal);
        }
        material.Append(expansion.Count);
        foreach (var pair in expansion
                     .OrderBy(static pair => pair.Key.EffectId, StringComparer.Ordinal)
                     .ThenBy(static pair => pair.Key.ComponentId, StringComparer.Ordinal))
        {
            material.Append(pair.Key.EffectId);
            material.Append(pair.Key.ComponentId);
            material.Append(pair.Value.Count);
            material.Append(pair.Value.Maximum);
        }
        AppendNullable(material, useProjectionOrdinal);
        AppendNullable(material, pendingFrontierBoundaryOrdinal);
        return material.Build();
    }

    private static string CreateTransitionFingerprint(ResourceTransition value)
    {
        using var builder = new ResourceFingerprintBuilder(
            "effect-boundary-resource-transition-v1");
        builder.Append(value.TransitionId);
        builder.Append(value.OperationId);
        builder.Append(value.EventRef);
        builder.Append(value.OriginKind);
        builder.Append(value.OriginId);
        builder.Append(value.Phase.ToString());
        builder.Append(value.Priority);
        builder.Append(value.ExecutionSequence);
        AppendCoordinate(builder, value.Coordinate);
        builder.Append(value.Operation.ToString());
        builder.Append(value.RequestedAmount);
        builder.Append(value.AppliedAmount);
        builder.Append(value.Outcome.ToString());
        AppendNullable(builder, value.CapacityDisposition?.ToString());
        AppendSnapshot(builder, value.BeforeState);
        AppendSnapshot(builder, value.AfterState);
        builder.Append(value.SourceEvidence.SourceKind);
        builder.Append(value.SourceEvidence.SourceId);
        builder.Append(value.SourceEvidence.AuthorityFingerprint);
        builder.Append(value.PolicyFingerprint);
        AppendNullable(builder, value.ReceiptId);
        builder.Append(value.Turn);
        return builder.Build();
    }

    private static string CreateReplayStableTransitionFingerprint(
        ResourceTransition value)
    {
        using var builder = new ResourceFingerprintBuilder(
            "effect-boundary-replay-stable-resource-transition-v1");
        builder.Append(value.EventRef);
        builder.Append(value.OriginKind);
        builder.Append(value.OriginId);
        builder.Append(value.Phase.ToString());
        builder.Append(value.Priority);
        builder.Append(value.ExecutionSequence);
        AppendCoordinate(builder, value.Coordinate);
        builder.Append(value.Operation.ToString());
        builder.Append(value.RequestedAmount);
        builder.Append(value.AppliedAmount);
        builder.Append(value.Outcome.ToString());
        AppendNullable(builder, value.CapacityDisposition?.ToString());
        AppendSnapshot(builder, value.BeforeState);
        AppendSnapshot(builder, value.AfterState);
        builder.Append(value.SourceEvidence.SourceKind);
        builder.Append(value.SourceEvidence.SourceId);
        builder.Append(value.SourceEvidence.AuthorityFingerprint);
        builder.Append(value.PolicyFingerprint);
        AppendNullable(builder, value.ReceiptId);
        builder.Append(value.Turn);
        return builder.Build();
    }

    private static void AppendSnapshot(
        ResourceFingerprintBuilder builder,
        ResourceStateSnapshot? value)
    {
        builder.Append(value != null);
        if (value == null)
            return;
        builder.Append(value.Current);
        builder.Append(value.Maximum);
        builder.Append(value.CapacityBinding.Kind.ToString());
        builder.Append(value.CapacityBinding.AuthorityKey);
        builder.Append(value.CapacityBinding.AuthorityFingerprint);
        builder.Append(value.State.ToString());
    }

    private static void AppendOperationKey(
        ResourceFingerprintBuilder builder,
        ResourceOperationKey? value)
    {
        builder.Append(value != null);
        if (value == null)
            return;
        builder.Append(value.EventRef);
        builder.Append(value.OriginKind);
        builder.Append(value.OriginId);
        AppendCoordinate(builder, value.Coordinate);
        builder.Append(value.Operation.ToString());
    }

    private static void AppendCoordinate(
        ResourceFingerprintBuilder builder,
        ResourceCoordinate value)
    {
        builder.Append(value.Realm);
        builder.Append(ResourceDefinitionCatalog.GetOwnerKindToken(value.OwnerKind));
        builder.Append(value.ResourceOwnerId);
        builder.Append(value.ResourceKey);
    }

    private static void AppendActivationIdentity(
        ResourceFingerprintBuilder builder,
        EffectActivationCandidateIdentity value)
    {
        builder.Append(value.EffectId);
        builder.Append(value.TriggerId);
        builder.Append(value.EventKind);
        builder.Append(value.EventRef);
        builder.Append(value.TriggerEventRef);
    }

    private static void AppendNullable(
        ResourceFingerprintBuilder builder,
        string? value)
    {
        builder.Append(value != null);
        if (value != null)
            builder.Append(value);
    }

    private static void AppendNullable(
        ResourceFingerprintBuilder builder,
        int? value)
    {
        builder.Append(value.HasValue);
        if (value.HasValue)
            builder.Append(value.Value);
    }

    private static void AppendNullable(
        ResourceFingerprintBuilder builder,
        long? value)
    {
        builder.Append(value.HasValue);
        if (value.HasValue)
            AppendLong(builder, value.Value);
    }

    private static void AppendLong(
        ResourceFingerprintBuilder builder,
        long value) =>
        builder.Append(value.ToString(CultureInfo.InvariantCulture));

    private static string CreateCausalClosureFingerprint(
        EffectEventBoundaryStamp boundary,
        IEnumerable<string> replayStableOperationKeys)
    {
        var ordered = replayStableOperationKeys
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        using var builder = new ResourceFingerprintBuilder(
            "effect-boundary-causal-operation-batch-v3");
        AppendLong(builder, boundary.BoundaryOrdinal);
        AppendNullable(builder, boundary.ParentBoundaryOrdinal);
        builder.Append(ordered.Length);
        foreach (var operationKey in ordered)
            builder.Append(operationKey);
        return builder.Build();
    }

    internal static string? ResolveTerminalAvailabilityEffectId(
        EffectReactionExecution reaction)
    {
        ArgumentNullException.ThrowIfNull(reaction);
        if (string.Equals(
                reaction.ResultKind,
                "remove",
                StringComparison.Ordinal) ||
            string.Equals(
                reaction.ResultKind,
                "suspend",
                StringComparison.Ordinal))
        {
            return reaction.EffectId;
        }
        return EffectReactionResultCatalog.TryResolve(
                   reaction.ResultKind,
                   out var descriptor) &&
               descriptor.Behavior ==
                   EffectReactionResultBehavior.ApplyDefinition &&
               !string.IsNullOrEmpty(reaction.ReplacementTargetEffectId) &&
               string.Equals(
                   reaction.ReplacementTargetEffectId,
                   reaction.ReplacementTargetEffectId.Trim(),
                   StringComparison.Ordinal)
            ? reaction.ReplacementTargetEffectId
            : null;
    }

    private static EffectReplayIdentity? ResolveTerminalAvailabilitySubject(
        EffectReactionExecution reaction,
        EffectReplayIdentity owner)
    {
        ArgumentNullException.ThrowIfNull(reaction);
        ArgumentNullException.ThrowIfNull(owner);
        if (!string.Equals(
                reaction.EffectId,
                owner.EffectId,
                StringComparison.Ordinal))
        {
            return null;
        }
        if (string.Equals(
                reaction.ResultKind,
                "remove",
                StringComparison.Ordinal) ||
            string.Equals(
                reaction.ResultKind,
                "suspend",
                StringComparison.Ordinal))
        {
            return owner;
        }
        return EffectReactionResultCatalog.TryResolve(
                   reaction.ResultKind,
                   out var descriptor) &&
               descriptor.Behavior ==
                   EffectReactionResultBehavior.ApplyDefinition
            ? reaction.ReplacementTarget
            : null;
    }

    private static bool CandidateClaimsAvailabilityEffectId(
        EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate candidate,
        string effectId)
    {
        if (string.Equals(
                candidate.Activation.Identity.EffectId,
                effectId,
                StringComparison.Ordinal))
        {
            return true;
        }
        return candidate.ReactionOutputs.Any(reaction => string.Equals(
            ResolveTerminalAvailabilityEffectId(reaction),
            effectId,
            StringComparison.Ordinal));
    }

    internal sealed class ClosedPrefix
    {
        private readonly AcceptedEffectBoundaryTranscript _image;

        internal ClosedPrefix(AcceptedEffectBoundaryTranscript image)
        {
            ArgumentNullException.ThrowIfNull(image);
            if (image.UseProjectionOrdinal != null ||
                image.PendingFrontierBoundaryOrdinal != null)
            {
                throw new ArgumentException(
                    "A closed prefix cannot carry a terminal or pending seal.",
                    nameof(image));
            }
            _image = image;
        }

        internal EffectAcceptedPlanAuthorityStamp? PlanAuthority => _image.PlanAuthority;
        internal string Fingerprint => _image.Fingerprint;
        internal IReadOnlyList<EffectEventBoundaryStamp> Boundaries => _image.Boundaries;
        internal IReadOnlyList<EffectEventBoundaryCloseStamp> BoundaryCloses => _image.BoundaryCloses;
        internal IReadOnlyList<EffectBoundaryCausalClosureStamp> CausalClosures => _image.CausalClosures;
        internal IReadOnlyList<AcceptedEffectBoundaryActivation> AcceptedActivations => _image.AcceptedActivations;
        internal IReadOnlyList<RejectedEffectBoundaryActivation> RejectedActivations => _image.RejectedActivations;
        internal IReadOnlyList<AppliedEffectComponentEvidence> AppliedComponentEvidence => _image.AppliedComponentEvidence;
        internal IReadOnlyList<AcceptedResourceMutationEvidence> ResourceMutations => _image.ResourceMutations;
        internal IReadOnlyList<ReleasedEffectReaction> ReleasedReactions => _image.ReleasedReactions;
        internal IReadOnlyList<EffectTerminalAvailabilityReservation> TerminalAvailabilityReservations =>
            _image.TerminalAvailabilityReservations;
        internal IReadOnlyDictionary<EffectReactionExpansionKey, EffectReactionExpansionUsage> ExpansionUsage =>
            _image.ExpansionUsage;

        // Evidence lookup, not a source/instance-existence or general eligibility check.
        internal bool TryGetLastConsumedUseBudget(EffectReplayIdentity subject, out int remainingUses)
        {
            ArgumentNullException.ThrowIfNull(subject);
            var accepted = _image._acceptedActivations
                .Where(value =>
                    value.Activation.Stamp.ConsumesUse &&
                    string.Equals(value.Activation.Stamp.Identity.EffectId, subject.EffectId, StringComparison.Ordinal) &&
                    value.Activation.Stamp.EffectAuthority == subject.Authority)
                .OrderByDescending(static value => value.Activation.Stamp.ActivationOrdinal)
                .FirstOrDefault();
            if (accepted?.Activation.UsesAfter is { } remaining)
            {
                remainingUses = remaining;
                return true;
            }
            remainingUses = default;
            return false;
        }

        // A positive answer reports exact terminal evidence, not a newly minted effect identity.
        internal bool HasTerminalAvailabilityEvidence(EffectReplayIdentity subject)
        {
            ArgumentNullException.ThrowIfNull(subject);
            return _image._terminalAvailabilityReservations.Any(value => value.Subject == subject) ||
                _image._releasedReactions.Any(value =>
                    ResolveTerminalAvailabilitySubject(
                        value.Reaction,
                        new EffectReplayIdentity(
                            value.Activation.Identity.EffectId,
                            value.Activation.EffectAuthority)) == subject);
        }
    }

    internal sealed class Builder
    {
    private enum ValidationBoundary
    {
        Terminal,
        ClosedPrefix,
        PendingPrefix
    }

    private readonly EffectAcceptedPlanAuthorityStamp? _planAuthority;
    private readonly List<EffectEventBoundaryStamp> _boundaries = new();
    private readonly List<EffectEventBoundaryCloseStamp> _boundaryCloses = new();
    private readonly List<EffectBoundaryCausalClosureStamp> _causalClosures = new();
    private readonly List<AcceptedEffectBoundaryActivation> _accepted = new();
    private readonly List<RejectedEffectBoundaryActivation> _rejected = new();
    private readonly List<AppliedEffectComponentEvidence> _applied = new();
    private readonly List<AcceptedResourceMutationEvidence> _resourceMutations = new();
    private readonly List<ReleasedEffectReaction> _released = new();
    private readonly List<EffectTerminalAvailabilityReservation>
        _terminalReservations = new();
    private readonly Dictionary<
        EffectReactionExpansionKey,
        EffectReactionExpansionUsage> _expansion = new();
    private readonly HashSet<string> _releasedEventRefs = new(StringComparer.Ordinal);
    private readonly HashSet<string> _unavailableEffects = new(StringComparer.Ordinal);
    private readonly HashSet<long> _closedBoundaries = new();
    private readonly HashSet<string> _completedCausalOperations = new(
        StringComparer.Ordinal);
    private readonly Dictionary<long, CandidateAuthority[]> _candidateAuthority = new();
    private readonly Dictionary<long, HashSet<string>> _remainingCausalOperations = new();
    private readonly Dictionary<long, Dictionary<string, string>> _causalOperationKeys = new();
    private readonly List<EffectBoundaryTranscriptIssue> _issues = new();
    private AcceptedEffectBoundaryTranscriptResult? _frozenResult;
    private long _nextMechanicsOrdinal;
    private long _nextBoundaryOrdinal;
    private long? _useProjectionOrdinal;
    private long? _pendingFrontierBoundaryOrdinal;

    internal Builder(EffectAcceptedPlanAuthorityStamp? planAuthority = null)
    {
        _planAuthority = planAuthority is null ? null : planAuthority with { };
    }

    internal long RecordMutationExecution()
    {
        EnsureMutable();
        return _nextMechanicsOrdinal++;
    }

    internal EffectEventBoundaryStamp? FindBoundary(long boundaryOrdinal) =>
        _boundaries.FirstOrDefault(value =>
            value.BoundaryOrdinal == boundaryOrdinal);

    internal EffectEventBoundaryStamp OpenBoundary(
        long? parentBoundaryOrdinal,
        ResourceOperationKey? producer,
        string eventKind,
        string producerEventRef,
        string? producerTransitionId,
        int? producerExecutionSequence,
        long producerMechanicsOrdinal,
        IReadOnlyList<EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>
            candidates)
    {
        EnsureMutable();
        if (parentBoundaryOrdinal is { } parentOrdinal &&
            (_boundaries.All(value =>
                 value.BoundaryOrdinal != parentOrdinal) ||
             _closedBoundaries.Contains(parentOrdinal) ||
             producer == null))
        {
            _issues.Add(new EffectBoundaryTranscriptIssue(
                "effect_boundary_parent_invalid",
                "one earlier open causal parent for a nested producer boundary",
                parentOrdinal.ToString(CultureInfo.InvariantCulture)));
        }
        var frozenCandidates = (candidates ??
                Array.Empty<EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>())
            .Select(FreezeCandidate)
            .ToArray();
        using var fingerprintBuilder = new ResourceFingerprintBuilder(
            "effect-event-boundary-candidate-batch-v1");
        fingerprintBuilder.Append(frozenCandidates.Length);
        foreach (var candidate in frozenCandidates
                     .OrderBy(static value => value.Fingerprint, StringComparer.Ordinal))
        {
            fingerprintBuilder.Append(candidate.Fingerprint);
        }
        var boundary = new EffectEventBoundaryStamp(
            _nextBoundaryOrdinal++,
            parentBoundaryOrdinal,
            producer,
            eventKind,
            producerEventRef,
            producerTransitionId,
            producerExecutionSequence,
            producerMechanicsOrdinal,
            _nextMechanicsOrdinal++,
            fingerprintBuilder.Build());
        _boundaries.Add(boundary);
        _candidateAuthority.Add(boundary.BoundaryOrdinal, frozenCandidates);
        return boundary;
    }

    internal void RecordAccepted(
        EffectEventBoundaryStamp boundary,
        AcceptedEffectActivation activation,
        EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate candidate)
    {
        EnsureMutable();
        var authority = boundary == null || candidate == null
            ? null
            : FindCandidateAuthority(boundary, candidate);
        if (authority == null || activation == null || candidate == null ||
            candidate.Activation.Identity != activation.Stamp.Identity ||
            candidate.Activation.Priority != activation.Stamp.Priority ||
            candidate.Activation.ConsumesUse != activation.Stamp.ConsumesUse)
        {
            _issues.Add(new EffectBoundaryTranscriptIssue(
                "effect_boundary_candidate_authority_invalid",
                "one exact member of the frozen boundary candidate batch",
                activation == null
                    ? "null activation"
                    : Describe(activation.Stamp.Identity)));
            return;
        }
        var accepted = new AcceptedEffectBoundaryActivation(
            boundary!,
            activation,
            candidate,
            authority?.Fingerprint ?? "invalid",
            _nextMechanicsOrdinal++);
        _accepted.Add(accepted);
    }

    internal EffectBoundaryTranscriptIssue? RecordRejected(
        EffectEventBoundaryStamp boundary,
        EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate candidate,
        EffectActivationRejectionReason reason,
        string? blockedAvailabilityEffectId = null)
    {
        EnsureMutable();
        var authority = FindCandidateAuthority(boundary, candidate);
        if (authority == null ||
            _accepted.Any(value =>
                value.Boundary == boundary &&
                ReferenceEquals(value.Candidate, candidate)) ||
            _rejected.Any(value =>
                value.Boundary == boundary &&
                ReferenceEquals(value.Candidate, candidate)))
        {
            return new EffectBoundaryTranscriptIssue(
                "effect_boundary_candidate_partition_invalid",
                "one exact frozen candidate accepted or rejected once",
                candidate == null
                    ? "null"
                    : Describe(candidate.Activation.Identity));
        }
        _rejected.Add(new RejectedEffectBoundaryActivation(
            boundary,
            candidate,
            authority.Fingerprint,
            reason,
            reason == EffectActivationRejectionReason.EffectTerminal
                ? blockedAvailabilityEffectId ??
                  candidate.Activation.Identity.EffectId
                : blockedAvailabilityEffectId));
        return null;
    }

    internal EffectBoundaryTranscriptIssue? BindCausalClosure(
        EffectEventBoundaryStamp boundary,
        IEnumerable<string> operationIds,
        IReadOnlyDictionary<string, string> replayStableOperationKeysById)
    {
        EnsureMutable();
        if (boundary == null || operationIds == null ||
            replayStableOperationKeysById == null ||
            !_boundaries.Contains(boundary) ||
            _remainingCausalOperations.ContainsKey(boundary.BoundaryOrdinal))
        {
            return new EffectBoundaryTranscriptIssue(
                "effect_boundary_causal_closure_invalid",
                "one exact causal operation closure per open boundary",
                boundary == null
                    ? "null"
                    : boundary.BoundaryOrdinal.ToString(CultureInfo.InvariantCulture));
        }
        var ordered = operationIds
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(string.IsNullOrWhiteSpace) ||
            ordered.Distinct(StringComparer.Ordinal).Count() != ordered.Length ||
            replayStableOperationKeysById.Count != ordered.Length ||
            ordered.Any(operationId =>
                !replayStableOperationKeysById.TryGetValue(
                    operationId,
                    out var operationKey) ||
                string.IsNullOrWhiteSpace(operationKey) ||
                !string.Equals(
                    operationKey,
                    operationKey.Trim(),
                    StringComparison.Ordinal)))
        {
            return new EffectBoundaryTranscriptIssue(
                "effect_boundary_causal_closure_invalid",
                "unique exact causal operation identities",
                string.Join(",", ordered));
        }
        var replayStableOperationKeys = ordered
            .Select(operationId =>
                replayStableOperationKeysById[operationId])
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        if (replayStableOperationKeys
                .Distinct(StringComparer.Ordinal)
                .Count() != replayStableOperationKeys.Length)
        {
            return new EffectBoundaryTranscriptIssue(
                "effect_boundary_causal_closure_invalid",
                "one unique replay-stable key per causal operation",
                string.Join(",", replayStableOperationKeys));
        }
        _causalOperationKeys.Add(boundary.BoundaryOrdinal,
            replayStableOperationKeysById.ToDictionary(
                static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal));
        _causalClosures.Add(new EffectBoundaryCausalClosureStamp(
            boundary,
            Array.AsReadOnly(ordered),
            Array.AsReadOnly(replayStableOperationKeys),
            CreateCausalClosureFingerprint(
                boundary,
                replayStableOperationKeys)));
        _remainingCausalOperations.Add(
            boundary.BoundaryOrdinal,
            ordered
                .Where(operationId =>
                    !_completedCausalOperations.Contains(operationId))
                .ToHashSet(StringComparer.Ordinal));
        return null;
    }

    internal void CompleteCausalOperation(string operationId)
    {
        EnsureMutable();
        if (string.IsNullOrWhiteSpace(operationId))
        {
            _issues.Add(new EffectBoundaryTranscriptIssue(
                "effect_boundary_causal_operation_invalid",
                "one exact completed operation identity",
                operationId ?? "null"));
            return;
        }
        if (!_completedCausalOperations.Add(operationId))
        {
            _issues.Add(new EffectBoundaryTranscriptIssue(
                "effect_boundary_causal_operation_invalid",
                "each causal operation completes exactly once",
                operationId));
            return;
        }
        foreach (var remaining in _remainingCausalOperations.Values)
            remaining.Remove(operationId);
    }

    internal EffectBoundaryTranscriptIssue? CloseBoundary(
        EffectEventBoundaryStamp boundary)
    {
        EnsureMutable();
        if (boundary == null ||
            !_boundaries.Contains(boundary) ||
            !_remainingCausalOperations.TryGetValue(
                boundary.BoundaryOrdinal,
                out var remaining) ||
            remaining.Count != 0 ||
            _boundaries.Any(value =>
                value.ParentBoundaryOrdinal == boundary.BoundaryOrdinal &&
                !_closedBoundaries.Contains(value.BoundaryOrdinal)) ||
            !_closedBoundaries.Add(boundary.BoundaryOrdinal))
        {
            return new EffectBoundaryTranscriptIssue(
                "effect_boundary_close_invalid",
                "one close after the exact causal closure completed",
                boundary == null
                    ? "null"
                    : boundary.BoundaryOrdinal.ToString(CultureInfo.InvariantCulture));
        }
        _boundaryCloses.Add(new EffectEventBoundaryCloseStamp(
            boundary,
            _nextMechanicsOrdinal++));
        return null;
    }

    internal AppliedEffectComponentEvidence RecordAppliedComponent(
        EffectEventBoundaryStamp boundary,
        AcceptedEffectActivation activation,
        ResourceOperationKey mutation,
        string componentId,
        ResourceTransition transition,
        long mutationMechanicsOrdinal)
    {
        EnsureMutable();
        var evidence = new AppliedEffectComponentEvidence(
            boundary,
            activation.Stamp.Identity,
            mutation,
            componentId,
            transition,
            mutationMechanicsOrdinal);
        _applied.Add(evidence);
        return evidence;
    }

    internal void RecordResourceMutation(
        ResourceOperationKey mutation,
        ResourceTransition transition,
        ResourceMutationExecutionKind executionKind,
        long mutationMechanicsOrdinal)
    {
        EnsureMutable();
        _resourceMutations.Add(new AcceptedResourceMutationEvidence(
            mutation,
            transition,
            executionKind,
            mutationMechanicsOrdinal));
    }

    internal EffectBoundaryTranscriptIssue? TryRelease(
        EffectEventBoundaryStamp boundary,
        AcceptedEffectActivation activation,
        EffectReactionExecution reaction,
        EffectReactionReleaseStage stage)
    {
        EnsureMutable();
        if (boundary == null || activation == null || reaction == null)
        {
            return new EffectBoundaryTranscriptIssue(
                "effect_reaction_release_authority_invalid",
                "one non-null frozen boundary, activation, and candidate reaction",
                "null");
        }
        if (!MatchesStage(reaction.Dependency, stage))
        {
            return new EffectBoundaryTranscriptIssue(
                "effect_reaction_release_stage_invalid",
                reaction.Dependency,
                stage.ToString());
        }
        if (!MatchesActivation(reaction, activation.Stamp.Identity))
        {
            return new EffectBoundaryTranscriptIssue(
                "effect_reaction_activation_authority_invalid",
                Describe(activation.Stamp.Identity),
                reaction.EventRef);
        }
        var accepted = _accepted.FirstOrDefault(value =>
            value.Boundary == boundary &&
            value.Activation.Stamp == activation.Stamp);
        var reactionFingerprint = AcceptedMechanicsPlanner
            .CreateReactionCandidateOutputFingerprint(reaction);
        if (accepted == null ||
            !accepted.Candidate.ReactionOutputs.Any(candidateReaction =>
                string.Equals(
                    AcceptedMechanicsPlanner
                        .CreateReactionCandidateOutputFingerprint(candidateReaction),
                    reactionFingerprint,
                    StringComparison.Ordinal)))
        {
            return new EffectBoundaryTranscriptIssue(
                "effect_reaction_candidate_authority_invalid",
                "one exact reaction from the accepted frozen candidate",
                reaction.EventRef);
        }
        if (_releasedEventRefs.Contains(reaction.EventRef))
        {
            return new EffectBoundaryTranscriptIssue(
                "effect_reaction_event_duplicate",
                "one released reaction per exact event identity",
                reaction.EventRef);
        }
        var isClosed = _closedBoundaries.Contains(boundary.BoundaryOrdinal);
        if (isClosed)
        {
            return new EffectBoundaryTranscriptIssue(
                "effect_reaction_release_boundary_phase_invalid",
                "release while the exact boundary is still open",
                reaction.EventRef);
        }
        if (stage == EffectReactionReleaseStage.AfterComponent &&
            !_applied.Any(evidence =>
                evidence.Boundary == boundary &&
                evidence.Activation == activation.Stamp.Identity &&
                string.Equals(
                    evidence.ComponentId,
                    reaction.AfterComponentId,
                    StringComparison.Ordinal) &&
                evidence.Transition.AppliedAmount != 0m))
        {
            return new EffectBoundaryTranscriptIssue(
                "effect_reaction_after_component_evidence_missing",
                "one exact nonzero applied predecessor component in the same boundary",
                reaction.AfterComponentId ?? "null");
        }
        if (stage == EffectReactionReleaseStage.AfterCurrentEvent &&
            (!_remainingCausalOperations.TryGetValue(
                 boundary.BoundaryOrdinal,
                 out var remaining) ||
             remaining.Count != 0 ||
             _boundaries.Any(value =>
                 value.ParentBoundaryOrdinal == boundary.BoundaryOrdinal &&
                 !_closedBoundaries.Contains(value.BoundaryOrdinal))))
        {
            return new EffectBoundaryTranscriptIssue(
                "effect_reaction_release_causal_boundary_incomplete",
                "the exact causal closure completed before after-current release",
                reaction.EventRef);
        }
        var key = new EffectReactionExpansionKey(
            reaction.EffectId,
            reaction.ComponentId);
        if (_expansion.TryGetValue(key, out var usage))
        {
            if (usage.Maximum != reaction.MaxExpansion)
            {
                return new EffectBoundaryTranscriptIssue(
                    "effect_reaction_expansion_policy_conflict",
                    usage.Maximum.ToString(),
                    reaction.MaxExpansion.ToString());
            }
            if (usage.Count >= usage.Maximum)
            {
                return new EffectBoundaryTranscriptIssue(
                    "effect_reaction_expansion_exceeded",
                    $"at most {usage.Maximum} releases",
                    reaction.EventRef);
            }
        }
        else if (reaction.MaxExpansion <= 0)
        {
            return new EffectBoundaryTranscriptIssue(
                "effect_reaction_expansion_policy_invalid",
                "positive source-owned maxExpansion",
                reaction.MaxExpansion.ToString());
        }
        if (_released.Count >= EffectReactionContract.MaximumExpansion)
        {
            return new EffectBoundaryTranscriptIssue(
                "effect_reaction_expansion_exceeded",
                $"at most {EffectReactionContract.MaximumExpansion} released reactions",
                reaction.EventRef);
        }

        _releasedEventRefs.Add(reaction.EventRef);
        _expansion[key] = usage == null
            ? new EffectReactionExpansionUsage(1, reaction.MaxExpansion)
            : usage with { Count = usage.Count + 1 };
        _released.Add(new ReleasedEffectReaction(
            boundary,
            activation.Stamp,
            reaction,
            stage,
            _nextMechanicsOrdinal++));
        var unavailableEffectId = ResolveTerminalAvailabilityEffectId(reaction);
        if (unavailableEffectId != null)
            _unavailableEffects.Add(unavailableEffectId);
        return null;
    }

    internal bool IsAvailable(string effectId) =>
        !_unavailableEffects.Contains(effectId);

    internal bool TryResolveUnavailableEffectId(
        EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate candidate,
        out string effectId)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        effectId = candidate.Activation.Identity.EffectId;
        if (!IsAvailable(effectId))
            return true;
        foreach (var reaction in candidate.ReactionOutputs)
        {
            var targetEffectId = ResolveTerminalAvailabilityEffectId(reaction);
            if (targetEffectId != null &&
                !IsAvailable(targetEffectId))
            {
                effectId = targetEffectId;
                return true;
            }
        }
        effectId = string.Empty;
        return false;
    }

    internal EffectBoundaryTranscriptIssue? ReserveTerminalAvailability(
        EffectEventBoundaryStamp boundary,
        AcceptedEffectActivation activation,
        EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate candidate)
    {
        EnsureMutable();
        var accepted = _accepted.FirstOrDefault(value =>
            value.Boundary == boundary &&
            value.Activation.Stamp == activation.Stamp &&
            ReferenceEquals(value.Candidate, candidate));
        if (accepted == null)
        {
            return new EffectBoundaryTranscriptIssue(
                "effect_terminal_reservation_authority_invalid",
                "one exact accepted frozen candidate",
                Describe(activation.Stamp.Identity));
        }
        if (_terminalReservations.Any(value =>
                value.Boundary == boundary &&
                value.Activation == activation.Stamp))
        {
            return new EffectBoundaryTranscriptIssue(
                "effect_terminal_reservation_duplicate",
                "one terminal reservation batch per accepted activation",
                Describe(activation.Stamp.Identity));
        }

        var ownerSubject = new EffectReplayIdentity(
            candidate.Activation.Identity.EffectId,
            candidate.EffectAuthority);
        var reactionReservations = candidate.ReactionOutputs
            .Where(static reaction =>
                string.Equals(
                    reaction.Dependency,
                    "after_current_event",
                    StringComparison.Ordinal))
            .Select(reaction => (
                Reaction: reaction,
                Subject: ResolveTerminalAvailabilitySubject(
                    reaction,
                    ownerSubject),
                Fingerprint: AcceptedMechanicsPlanner
                    .CreateReactionCandidateOutputFingerprint(reaction),
                ReplayStableOrderKey: AcceptedMechanicsPlanner
                    .CreateReplayStableReactionCandidateOutputFingerprint(
                        reaction,
                        candidate.Activation.Identity.EffectId,
                        candidate.EffectAuthority)))
            .Where(static value => value.Subject != null)
            .Select(static value => (
                value.Reaction,
                Subject: value.Subject!,
                value.Fingerprint,
                value.ReplayStableOrderKey))
            .ToArray();
        var duplicateOrderKey = reactionReservations
            .GroupBy(
                static value => value.ReplayStableOrderKey,
                StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() != 1);
        if (duplicateOrderKey != null)
        {
            return new EffectBoundaryTranscriptIssue(
                "effect_terminal_reservation_order_key_duplicate",
                "one unique replay-stable order key per terminal reaction reservation",
                duplicateOrderKey.Key);
        }
        reactionReservations = reactionReservations
            .OrderBy(static value => value.Reaction.ComponentPriority)
            .ThenBy(
                static value => value.Reaction.ComponentId,
                StringComparer.Ordinal)
            .ThenBy(
                static value => value.ReplayStableOrderKey,
                StringComparer.Ordinal)
            .ToArray();
        if (!activation.EffectTerminal && reactionReservations.Length == 0)
        {
            return new EffectBoundaryTranscriptIssue(
                "effect_terminal_reservation_authority_invalid",
                "a last-use activation or unconditional deferred terminal reaction",
                Describe(activation.Stamp.Identity));
        }

        if (activation.EffectTerminal)
        {
            _terminalReservations.Add(new EffectTerminalAvailabilityReservation(
                boundary,
                activation.Stamp,
                ownerSubject,
                EffectTerminalAvailabilityReservationKind.LastUse,
                ReactionFingerprint: null,
                ReplayStableOrderKey: null,
                _nextMechanicsOrdinal++));
        }
        foreach (var reactionReservation in reactionReservations)
        {
            _terminalReservations.Add(new EffectTerminalAvailabilityReservation(
                boundary,
                activation.Stamp,
                reactionReservation.Subject,
                EffectTerminalAvailabilityReservationKind.AfterCurrentReaction,
                reactionReservation.Fingerprint,
                reactionReservation.ReplayStableOrderKey,
                _nextMechanicsOrdinal++));
        }
        if (activation.EffectTerminal)
            _unavailableEffects.Add(ownerSubject.EffectId);
        foreach (var reactionReservation in reactionReservations)
            _unavailableEffects.Add(reactionReservation.Subject.EffectId);
        return null;
    }

    internal long SealUseProjection()
    {
        EnsureMutable();
        var openBoundaries = _boundaries
            .Where(value => !_closedBoundaries.Contains(value.BoundaryOrdinal))
            .Select(static value => value.BoundaryOrdinal)
            .OrderBy(static value => value)
            .ToArray();
        if (_pendingFrontierBoundaryOrdinal != null || openBoundaries.Length != 0)
        {
            _issues.Add(new EffectBoundaryTranscriptIssue(
                "effect_boundary_terminal_projection_invalid",
                "a fully closed transcript with no pending frontier",
                _pendingFrontierBoundaryOrdinal?.ToString(
                    CultureInfo.InvariantCulture) ??
                string.Join(",", openBoundaries)));
            return _nextMechanicsOrdinal;
        }
        _useProjectionOrdinal ??= _nextMechanicsOrdinal++;
        return _useProjectionOrdinal.Value;
    }

    internal EffectBoundaryTranscriptIssue? SealPendingFrontier(
        EffectEventBoundaryStamp boundary)
    {
        var issue = ValidatePendingFrontier(boundary);
        if (issue != null)
            return issue;
        _pendingFrontierBoundaryOrdinal = boundary.BoundaryOrdinal;
        return null;
    }

    private EffectBoundaryTranscriptIssue? ValidatePendingFrontier(
        EffectEventBoundaryStamp boundary)
    {
        EnsureMutable();
        var openBoundaryOrdinals = _boundaries
            .Where(value => !_closedBoundaries.Contains(value.BoundaryOrdinal))
            .Select(static value => value.BoundaryOrdinal)
            .ToHashSet();
        var pendingOpenChain = new HashSet<long>();
        EffectEventBoundaryStamp? cursor = boundary;
        while (cursor != null && pendingOpenChain.Add(cursor.BoundaryOrdinal))
        {
            cursor = cursor.ParentBoundaryOrdinal is { } parentOrdinal
                ? FindBoundary(parentOrdinal)
                : null;
        }
        if (boundary == null ||
            !_boundaries.Contains(boundary) ||
            _closedBoundaries.Contains(boundary.BoundaryOrdinal) ||
            !_remainingCausalOperations.TryGetValue(
                boundary.BoundaryOrdinal,
             out var remaining) ||
            remaining.Count != 0 ||
            _boundaries.Any(value =>
                value.ParentBoundaryOrdinal == boundary.BoundaryOrdinal &&
                !_closedBoundaries.Contains(value.BoundaryOrdinal)) ||
            !openBoundaryOrdinals.SetEquals(pendingOpenChain) ||
            !_accepted.Any(value =>
                value.Boundary == boundary &&
                HasUnresolvedPendingOutput(value)) ||
            _pendingFrontierBoundaryOrdinal != null ||
            _useProjectionOrdinal != null)
        {
            return new EffectBoundaryTranscriptIssue(
                "effect_boundary_pending_frontier_invalid",
                "one open causally complete boundary and no terminal projection",
                boundary == null
                    ? "null"
                    : boundary.BoundaryOrdinal.ToString(CultureInfo.InvariantCulture));
        }
        return null;
    }

    private AcceptedEffectBoundaryTranscript CaptureImage(long? pendingFrontier = null) =>
        new(
            _planAuthority,
            _boundaries,
            _boundaryCloses,
            _causalClosures,
            _accepted,
            _rejected,
            _applied,
            _resourceMutations,
            _released,
            _terminalReservations,
            _expansion,
            _useProjectionOrdinal,
            pendingFrontier ?? _pendingFrontierBoundaryOrdinal);

    // This snapshot describes a real open frontier; it neither seals nor freezes
    // the retained builder. The execution owner retains its identity for resume.
    internal AcceptedEffectBoundaryTranscriptResult CapturePendingFrontier(
        EffectEventBoundaryStamp boundary)
    {
        var issue = ValidatePendingFrontier(boundary);
        if (issue != null)
            return new AcceptedEffectBoundaryTranscriptResult(null,
                Array.AsReadOnly(new[] { issue }));
        var issues = _issues.Concat(Validate(
            ValidationBoundary.PendingPrefix, boundary.BoundaryOrdinal)).ToArray();
        return issues.Length == 0
            ? new AcceptedEffectBoundaryTranscriptResult(
                CaptureImage(boundary.BoundaryOrdinal),
                Array.Empty<EffectBoundaryTranscriptIssue>())
            : new AcceptedEffectBoundaryTranscriptResult(null, Array.AsReadOnly(issues));
    }

    // Only the execution owner's validated receipt extension calls this operation.
    // Full mappings, rather than separately sorted ID/key arrays, preserve every
    // original operation's association while appending genuinely new descendants.
    internal EffectBoundaryTranscriptIssue? ExtendOpenCausalClosure(
        EffectEventBoundaryStamp boundary,
        IReadOnlyDictionary<string, string> operationKeys)
    {
        EnsureMutable();
        if (boundary == null || operationKeys == null ||
            !_boundaries.Contains(boundary) ||
            _closedBoundaries.Contains(boundary.BoundaryOrdinal) ||
            _pendingFrontierBoundaryOrdinal != null || _useProjectionOrdinal != null ||
            !_causalOperationKeys.TryGetValue(boundary.BoundaryOrdinal, out var original) ||
            !_remainingCausalOperations.TryGetValue(boundary.BoundaryOrdinal, out var remaining))
            return Issue("effect_boundary_causal_extension_invalid",
                "one retained unsealed open boundary", "foreign, closed or sealed");
        if (operationKeys.Any(pair => string.IsNullOrWhiteSpace(pair.Key) ||
                string.IsNullOrWhiteSpace(pair.Value) ||
                !string.Equals(pair.Value, pair.Value.Trim(), StringComparison.Ordinal)) ||
            operationKeys.Values.Distinct(StringComparer.Ordinal).Count() != operationKeys.Count ||
            original.Any(pair => !operationKeys.TryGetValue(pair.Key, out var key) ||
                !string.Equals(pair.Value, key, StringComparison.Ordinal)))
            return Issue("effect_boundary_causal_extension_invalid",
                "an injective extension preserving each existing operation and stable key",
                boundary.BoundaryOrdinal.ToString(CultureInfo.InvariantCulture));
        var closureIndex = _causalClosures.FindIndex(value => value.Boundary == boundary);
        if (closureIndex < 0)
            return Issue("effect_boundary_causal_extension_invalid",
                "the existing closure of the retained boundary", "missing");
        if (operationKeys.Count == original.Count)
            return null;
        var ids = operationKeys.Keys.OrderBy(static value => value, StringComparer.Ordinal).ToArray();
        var keys = operationKeys.Values.OrderBy(static value => value, StringComparer.Ordinal).ToArray();
        var appended = ids.Where(id => !original.ContainsKey(id)).ToArray();
        var retainedKeys = operationKeys.ToDictionary(
            static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        _causalClosures[closureIndex] = new EffectBoundaryCausalClosureStamp(
            boundary, Array.AsReadOnly(ids), Array.AsReadOnly(keys),
            CreateCausalClosureFingerprint(boundary, keys));
        _causalOperationKeys[boundary.BoundaryOrdinal] = retainedKeys;
        remaining.UnionWith(appended.Where(id => !_completedCausalOperations.Contains(id)));
        return null;
    }

    internal AcceptedEffectBoundaryPrefixResult CaptureClosedPrefix()
    {
        EnsureMutable();
        if (_useProjectionOrdinal != null || _pendingFrontierBoundaryOrdinal != null)
        {
            return new AcceptedEffectBoundaryPrefixResult(
                null,
                Array.AsReadOnly(new[]
                {
                    new EffectBoundaryTranscriptIssue(
                        "effect_boundary_prefix_sealed",
                        "an unsealed closed causal prefix",
                        _useProjectionOrdinal != null ? "terminal" : "pending")
                }));
        }
        var issues = _issues.Concat(Validate(ValidationBoundary.ClosedPrefix)).ToArray();
        return issues.Length == 0
            ? new AcceptedEffectBoundaryPrefixResult(
                new ClosedPrefix(CaptureImage()),
                Array.Empty<EffectBoundaryTranscriptIssue>())
            : new AcceptedEffectBoundaryPrefixResult(null, Array.AsReadOnly(issues));
    }

    internal AcceptedEffectBoundaryTranscriptResult Freeze()
    {
        if (_frozenResult != null)
            return _frozenResult;
        var issues = _issues
            .Concat(Validate(ValidationBoundary.Terminal))
            .ToArray();
        if (issues.Length != 0)
        {
            _frozenResult = new AcceptedEffectBoundaryTranscriptResult(
                null,
                Array.AsReadOnly(issues));
            return _frozenResult;
        }
        _frozenResult = new AcceptedEffectBoundaryTranscriptResult(
            CaptureImage(),
            Array.Empty<EffectBoundaryTranscriptIssue>());
        return _frozenResult;
    }

    private void EnsureMutable()
    {
        if (_frozenResult != null)
        {
            throw new InvalidOperationException(
                "The accepted effect boundary transcript builder is already frozen.");
        }
    }

    private CandidateAuthority FreezeCandidate(
        EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate candidate)
    {
        if (candidate == null)
        {
            _issues.Add(new EffectBoundaryTranscriptIssue(
                "effect_boundary_candidate_authority_invalid",
                "one non-null pure candidate",
                "null"));
            return new CandidateAuthority(null!, "invalid");
        }
        try
        {
            return new CandidateAuthority(
                candidate,
                AcceptedMechanicsPlanner.CreateCandidateFingerprint(candidate));
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            _issues.Add(new EffectBoundaryTranscriptIssue(
                "effect_boundary_candidate_authority_invalid",
                "one valid immutable candidate fingerprint",
                exception.Message));
            return new CandidateAuthority(candidate, "invalid");
        }
    }

    private CandidateAuthority? FindCandidateAuthority(
        EffectEventBoundaryStamp? boundary,
        EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate? candidate)
    {
        if (boundary == null || candidate == null ||
            !_candidateAuthority.TryGetValue(
                boundary.BoundaryOrdinal,
                out var candidates))
        {
            return null;
        }
        return candidates.FirstOrDefault(value =>
            ReferenceEquals(value.Candidate, candidate));
    }

    internal EffectBoundaryTranscriptIssue? BindPendingCandidateExtension(
        EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate original,
        EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate bound)
    {
        EnsureMutable();
        var acceptedIndex = _accepted.FindIndex(value => ReferenceEquals(value.Candidate, original));
        if (acceptedIndex < 0 || bound == null ||
            _pendingFrontierBoundaryOrdinal != null || _useProjectionOrdinal != null)
            return Issue("effect_boundary_pending_extension_invalid",
                "one retained accepted candidate in an unsealed builder", "missing or sealed");
        var accepted = _accepted[acceptedIndex];
        if (_closedBoundaries.Contains(accepted.Boundary.BoundaryOrdinal) ||
            !_candidateAuthority.TryGetValue(accepted.Boundary.BoundaryOrdinal, out var authorities))
            return Issue("effect_boundary_pending_extension_invalid",
                "the original candidate of an open boundary", "closed or missing");
        var authorityIndex = Array.FindIndex(authorities,
            value => ReferenceEquals(value.Candidate, original));
        if (authorityIndex < 0)
            return Issue("effect_boundary_pending_extension_invalid",
                "the original frozen candidate reference", "foreign");
        string fingerprint;
        try { fingerprint = AcceptedMechanicsPlanner.CreateCandidateFingerprint(bound); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Issue("effect_boundary_pending_extension_invalid",
                "an exact receipt-only extension of the immutable candidate origin", exception.Message);
        }
        if (!string.Equals(fingerprint, accepted.CandidateFingerprint, StringComparison.Ordinal) ||
            !string.Equals(bound.CandidateFingerprint, fingerprint, StringComparison.Ordinal) ||
            original.Activation != bound.Activation || original.UseSeed != bound.UseSeed ||
            original.EffectAuthority != bound.EffectAuthority || original.Producer != bound.Producer ||
            original.PlannedComponentIdsByMutation.Any(pair =>
                !bound.PlannedComponentIdsByMutation.TryGetValue(pair.Key, out var component) ||
                !string.Equals(pair.Value, component, StringComparison.Ordinal)))
            return Issue("effect_boundary_pending_extension_invalid",
                "unchanged accepted activation, origin and previous component bindings", "changed");
        foreach (var output in original.PendingOutputs)
        {
            if (!original.TryResolvePendingBinding(output.ComponentId, out var previous))
                continue;
            if (!bound.TryResolvePendingBinding(output.ComponentId, out var next) ||
                previous.RequestId != next.RequestId ||
                previous.RequestAuthority.ReplayFingerprint != next.RequestAuthority.ReplayFingerprint ||
                previous.ReceiptFingerprint != next.ReceiptFingerprint ||
                previous.ResultKind != next.ResultKind || previous.Amount != next.Amount ||
                previous.Reason != next.Reason || previous.ResolvedAtTurn != next.ResolvedAtTurn)
                return Issue("effect_boundary_pending_extension_invalid",
                    "every previously accepted terminal binding unchanged", output.ComponentId);
        }
        authorities[authorityIndex] = authorities[authorityIndex] with { Candidate = bound };
        _accepted[acceptedIndex] = accepted with { Candidate = bound };
        return null;
    }

    private bool HasUnresolvedPendingOutput(
        AcceptedEffectBoundaryActivation accepted) =>
        accepted.Candidate.PendingOutputs.Any(output =>
            !accepted.Candidate.TryResolvePendingBinding(
                output.ComponentId,
                out _) &&
            (output.AfterComponentId == null ||
             _applied.Any(evidence =>
                 evidence.Boundary == accepted.Boundary &&
                 evidence.Activation == accepted.Activation.Stamp.Identity &&
                 string.Equals(
                     evidence.ComponentId,
                     output.AfterComponentId,
                     StringComparison.Ordinal) &&
                 evidence.Transition.AppliedAmount != 0m)));

    private sealed record CandidateAuthority(
        EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate Candidate,
        string Fingerprint);

    private IReadOnlyList<EffectBoundaryTranscriptIssue> Validate(
        ValidationBoundary validationBoundary, long? pendingFrontierOverride = null)
    {
        var issues = new List<EffectBoundaryTranscriptIssue>();
        if (_planAuthority != null &&
            (!IsExactPlanAuthorityToken(_planAuthority.InputFingerprint) ||
             !IsExactPlanAuthorityToken(
                 _planAuthority.CarrierAuthorityFingerprint) ||
             !IsExactPlanAuthorityToken(
                 _planAuthority.SourceAuthorityFingerprint) ||
             !IsExactPlanAuthorityToken(
                 _planAuthority.TargetAuthorityFingerprint) ||
             !IsExactPlanAuthorityToken(
                 _planAuthority.SkillScopeAuthorityFingerprint)))
        {
            issues.Add(Issue(
                "effect_boundary_plan_authority_invalid",
                "one exact input identity and four accepted-effect authority fingerprints including skill scope",
                "invalid"));
        }
        var closesByBoundary = new Dictionary<long, EffectEventBoundaryCloseStamp>();
        var pendingFrontier = pendingFrontierOverride ?? _pendingFrontierBoundaryOrdinal;
        if (pendingFrontier != null &&
            _boundaries.All(value =>
                value.BoundaryOrdinal != pendingFrontier.Value))
        {
            issues.Add(Issue(
                "effect_boundary_pending_frontier_invalid",
                "one exact actual-event boundary",
                pendingFrontier.Value.ToString(CultureInfo.InvariantCulture)));
        }
        for (var index = 0; index < _boundaries.Count; index++)
        {
            var boundary = _boundaries[index];
            if (boundary.BoundaryOrdinal != index ||
                boundary.ParentBoundaryOrdinal is { } parentOrdinal &&
                (parentOrdinal < 0 ||
                 parentOrdinal >= boundary.BoundaryOrdinal) ||
                boundary.OpenMechanicsOrdinal < 0 ||
                boundary.ProducerMechanicsOrdinal >=
                boundary.OpenMechanicsOrdinal ||
                string.IsNullOrWhiteSpace(boundary.EventKind) ||
                string.IsNullOrWhiteSpace(boundary.ProducerEventRef) ||
                !ResourceMaterializationContract.IsAuthorityFingerprint(
                    boundary.CandidateBatchFingerprint))
            {
                issues.Add(Issue(
                    "effect_boundary_stamp_invalid",
                    "sequential exact boundary identity and causal open order",
                    boundary.BoundaryOrdinal.ToString(CultureInfo.InvariantCulture)));
            }
            if (!_candidateAuthority.TryGetValue(
                    boundary.BoundaryOrdinal,
                    out var authority) ||
                authority.Any(static value =>
                    value.Candidate == null ||
                    !ResourceMaterializationContract.IsAuthorityFingerprint(
                        value.Fingerprint)) ||
                authority.Select(static value => value.Fingerprint)
                    .Distinct(StringComparer.Ordinal).Count() != authority.Length)
            {
                issues.Add(Issue(
                    "effect_boundary_candidate_batch_invalid",
                    "one unique immutable authority entry per discovered candidate",
                    boundary.BoundaryOrdinal.ToString(CultureInfo.InvariantCulture)));
            }
        }
        foreach (var close in _boundaryCloses)
        {
            if (close == null ||
                !_boundaries.Contains(close.Boundary) ||
                close.MechanicsOrdinal <= close.Boundary.OpenMechanicsOrdinal ||
                !closesByBoundary.TryAdd(
                    close.Boundary.BoundaryOrdinal,
                    close))
            {
                issues.Add(Issue(
                    "effect_boundary_close_invalid",
                    "one monotonic exact close per open boundary",
                    close?.Boundary.BoundaryOrdinal.ToString(
                        CultureInfo.InvariantCulture) ?? "null"));
            }
        }
        var boundariesByOrdinal = _boundaries.ToDictionary(
            static boundary => boundary.BoundaryOrdinal);
        var openBoundaryOrdinals = _boundaries
            .Where(boundary =>
                !closesByBoundary.ContainsKey(boundary.BoundaryOrdinal))
            .Select(static boundary => boundary.BoundaryOrdinal)
            .ToHashSet();
        var pendingOpenChain = new HashSet<long>();
        if (pendingFrontier is { } frontierOrdinal &&
            boundariesByOrdinal.TryGetValue(frontierOrdinal, out var frontier))
        {
            EffectEventBoundaryStamp? cursor = frontier;
            while (cursor != null &&
                   pendingOpenChain.Add(cursor.BoundaryOrdinal))
            {
                cursor = cursor.ParentBoundaryOrdinal is { } parentOrdinal &&
                         boundariesByOrdinal.TryGetValue(
                             parentOrdinal,
                             out var parent)
                    ? parent
                    : null;
            }
            if (!openBoundaryOrdinals.SetEquals(pendingOpenChain))
            {
                issues.Add(Issue(
                    "effect_boundary_pending_chain_invalid",
                    "the pending frontier and its exact open ancestor chain",
                    string.Join(",", openBoundaryOrdinals
                        .OrderBy(static value => value))));
            }
        }
        var unresolvedPendingBoundaryOrdinals = _accepted
            .Where(HasUnresolvedPendingOutput)
            .Select(static value => value.Boundary.BoundaryOrdinal)
            .ToHashSet();
        if (pendingFrontier is { } expectedPendingFrontier)
        {
            if (!unresolvedPendingBoundaryOrdinals.Contains(
                    expectedPendingFrontier) ||
                unresolvedPendingBoundaryOrdinals.Any(boundaryOrdinal =>
                    !pendingOpenChain.Contains(boundaryOrdinal)))
            {
                issues.Add(Issue(
                    "effect_boundary_pending_frontier_invalid",
                    "the deepest unresolved accepted pending boundary and only its unresolved ancestors",
                    string.Join(",", unresolvedPendingBoundaryOrdinals
                        .OrderBy(static value => value))));
            }
        }
        else if (unresolvedPendingBoundaryOrdinals.Count != 0)
        {
            issues.Add(Issue(
                "effect_boundary_pending_frontier_invalid",
                "no unresolved accepted pending output in a complete transcript",
                string.Join(",", unresolvedPendingBoundaryOrdinals
                    .OrderBy(static value => value))));
        }
        foreach (var boundary in _boundaries)
        {
            if (boundary.ParentBoundaryOrdinal is not { } parentOrdinal)
                continue;
            if (!boundariesByOrdinal.TryGetValue(parentOrdinal, out var parent) ||
                boundary.Producer == null)
            {
                issues.Add(Issue(
                    "effect_boundary_parent_invalid",
                    "one earlier causal parent for each nested producer boundary",
                    boundary.BoundaryOrdinal.ToString(CultureInfo.InvariantCulture)));
                continue;
            }
            closesByBoundary.TryGetValue(parentOrdinal, out var parentClose);
            closesByBoundary.TryGetValue(
                boundary.BoundaryOrdinal,
                out var childClose);
            if (parentClose != null &&
                (parentClose.MechanicsOrdinal <= boundary.OpenMechanicsOrdinal ||
                 childClose == null ||
                 parentClose.MechanicsOrdinal <= childClose.MechanicsOrdinal))
            {
                issues.Add(Issue(
                    "effect_boundary_parent_close_order_invalid",
                    "a parent remains open through its child and closes after the child",
                    boundary.BoundaryOrdinal.ToString(CultureInfo.InvariantCulture)));
            }
        }
        foreach (var boundary in _boundaries)
        {
            if (!closesByBoundary.ContainsKey(boundary.BoundaryOrdinal) &&
                !pendingOpenChain.Contains(boundary.BoundaryOrdinal))
            {
                issues.Add(Issue(
                    "effect_boundary_close_missing",
                    "one explicit close for every actual event boundary",
                    boundary.BoundaryOrdinal.ToString(CultureInfo.InvariantCulture)));
            }
            if (boundary.Producer != null &&
                !_resourceMutations.Any(mutation =>
                    mutation.Mutation == boundary.Producer &&
                    mutation.ExecutionKind ==
                    ResourceMutationExecutionKind.Applied &&
                    mutation.MechanicsOrdinal ==
                    boundary.ProducerMechanicsOrdinal &&
                    string.Equals(
                        mutation.Transition.TransitionId,
                        boundary.ProducerTransitionId,
                        StringComparison.Ordinal) &&
                    mutation.Transition.ExecutionSequence ==
                    boundary.ProducerExecutionSequence))
            {
                issues.Add(Issue(
                    "effect_boundary_producer_authority_invalid",
                    "the exact applied producer transition and mechanics ordinal",
                    boundary.ProducerEventRef));
            }
            var closures = _causalClosures.Where(value =>
                    value.Boundary == boundary)
                .ToArray();
            var closure = closures.SingleOrDefault();
            var hasRemainingCausalOperations =
                _remainingCausalOperations.TryGetValue(
                    boundary.BoundaryOrdinal,
                    out var remaining);
            var remainingCount = remaining?.Count;
            if (closures.Length != 1 ||
                !ResourceMaterializationContract.IsAuthorityFingerprint(
                    closure?.Fingerprint) ||
                closure == null ||
                closure.OperationIds.Count !=
                    closure.ReplayStableOperationKeys.Count ||
                closure.ReplayStableOperationKeys.Any(
                    string.IsNullOrWhiteSpace) ||
                closure.ReplayStableOperationKeys
                    .Distinct(StringComparer.Ordinal)
                    .Count() != closure.ReplayStableOperationKeys.Count ||
                !string.Equals(
                    closure.Fingerprint,
                    CreateCausalClosureFingerprint(
                        boundary,
                        closure.ReplayStableOperationKeys),
                    StringComparison.Ordinal) ||
                !hasRemainingCausalOperations ||
                remainingCount != 0 &&
                (!pendingOpenChain.Contains(boundary.BoundaryOrdinal) ||
                 boundary.BoundaryOrdinal == pendingFrontier))
            {
                issues.Add(Issue(
                    "effect_boundary_causal_closure_invalid",
                    "one sealed and completed causal operation batch per boundary",
                    boundary.BoundaryOrdinal.ToString(CultureInfo.InvariantCulture) +
                    ";closures=" + closures.Length.ToString(
                        CultureInfo.InvariantCulture) +
                    ";remaining=" + (remaining == null
                        ? "missing"
                        : string.Join(",", remaining.OrderBy(
                            static value => value,
                            StringComparer.Ordinal)))));
            }
            if (boundary.ParentBoundaryOrdinal is { } parentOrdinal &&
                boundary.Producer != null)
            {
                var producerEvidence = _resourceMutations.SingleOrDefault(
                    mutation =>
                        mutation.Mutation == boundary.Producer &&
                        mutation.ExecutionKind ==
                            ResourceMutationExecutionKind.Applied &&
                        mutation.MechanicsOrdinal ==
                            boundary.ProducerMechanicsOrdinal);
                var parentClosure = _causalClosures.SingleOrDefault(value =>
                    value.Boundary.BoundaryOrdinal == parentOrdinal);
                if (producerEvidence == null ||
                    parentClosure == null ||
                    !parentClosure.OperationIds.Contains(
                        producerEvidence.Transition.OperationId,
                        StringComparer.Ordinal))
                {
                    issues.Add(Issue(
                        "effect_boundary_parent_causality_invalid",
                        "the child producer operation belongs to the exact parent causal closure",
                        boundary.BoundaryOrdinal.ToString(
                            CultureInfo.InvariantCulture)));
                }
            }
            if (_candidateAuthority.TryGetValue(
                    boundary.BoundaryOrdinal,
                    out var frozenCandidates))
            {
                var frozen = frozenCandidates
                    .Select(static value => value.Fingerprint)
                    .ToHashSet(StringComparer.Ordinal);
                var partition = _accepted
                    .Where(value => value.Boundary == boundary)
                    .Select(static value => value.CandidateFingerprint)
                    .Concat(_rejected
                        .Where(value => value.Boundary == boundary)
                        .Select(static value => value.CandidateFingerprint))
                    .ToArray();
                if (partition.Length != frozen.Count ||
                    partition.Distinct(StringComparer.Ordinal).Count() !=
                    partition.Length ||
                    !frozen.SetEquals(partition))
                {
                    issues.Add(Issue(
                        "effect_boundary_candidate_partition_invalid",
                        "accepted and rejected evidence exactly partitions the frozen batch",
                        boundary.BoundaryOrdinal.ToString(
                            CultureInfo.InvariantCulture)));
                }
            }
        }

        var acceptedOrdinals = new HashSet<long>();
        foreach (var accepted in _accepted)
        {
            var stamp = accepted.Activation.Stamp;
            var closeFound = closesByBoundary.TryGetValue(
                accepted.Boundary.BoundaryOrdinal,
                out var close);
            var isPendingOpenBoundary = pendingOpenChain.Contains(
                accepted.Boundary.BoundaryOrdinal);
            var authority = FindCandidateAuthority(
                accepted.Boundary,
                accepted.Candidate);
            if (!_boundaries.Contains(accepted.Boundary) ||
                !closeFound && !isPendingOpenBoundary ||
                accepted.MechanicsOrdinal <=
                accepted.Boundary.OpenMechanicsOrdinal ||
                closeFound && accepted.MechanicsOrdinal >=
                close!.MechanicsOrdinal ||
                authority == null ||
                !string.Equals(
                    authority.Fingerprint,
                    accepted.CandidateFingerprint,
                    StringComparison.Ordinal) ||
                accepted.Candidate.Activation.Identity != stamp.Identity ||
                accepted.Candidate.Activation.Priority != stamp.Priority ||
                accepted.Candidate.Activation.ConsumesUse != stamp.ConsumesUse ||
                !acceptedOrdinals.Add(stamp.ActivationOrdinal))
            {
                issues.Add(Issue(
                    "effect_boundary_activation_authority_invalid",
                    "one exact frozen candidate accepted inside its open boundary",
                    Describe(stamp.Identity)));
            }
            if (stamp.ConsumesUse)
            {
                if (stamp.UsesBefore is not > 0 ||
                    accepted.Activation.UsesAfter != stamp.UsesBefore - 1 ||
                    accepted.Activation.EffectTerminal !=
                    (accepted.Activation.UsesAfter == 0))
                {
                    issues.Add(Issue(
                        "effect_boundary_use_projection_invalid",
                        "usesAfter = usesBefore - 1 and terminal exactly at zero",
                        Describe(stamp.Identity)));
                }
            }
            else if (stamp.UsesBefore != null ||
                     accepted.Activation.UsesAfter != null ||
                     accepted.Activation.EffectTerminal)
            {
                issues.Add(Issue(
                    "effect_boundary_use_projection_invalid",
                "non-consuming activation has no use budget or use terminal",
                Describe(stamp.Identity)));
            }
        }
        var actualReservationKeys = new HashSet<(
            long BoundaryOrdinal,
            long ActivationOrdinal,
            EffectReplayIdentity Subject,
            EffectTerminalAvailabilityReservationKind Kind,
            string? ReactionFingerprint,
            string? ReplayStableOrderKey)>();
        foreach (var reservation in _terminalReservations)
        {
            var accepted = _accepted.FirstOrDefault(value =>
                value.Boundary == reservation.Boundary &&
                value.Activation.Stamp == reservation.Activation);
            var ownerSubject = accepted == null
                ? null
                : new EffectReplayIdentity(
                    accepted.Activation.Stamp.Identity.EffectId,
                    accepted.Candidate.EffectAuthority);
            closesByBoundary.TryGetValue(
                reservation.Boundary.BoundaryOrdinal,
                out var close);
            var acceptedBatchEnd = _accepted
                .Where(value => value.Boundary == reservation.Boundary)
                .Select(static value => value.MechanicsOrdinal)
                .DefaultIfEmpty(-1)
                .Max();
            var hasExactAuthority = accepted != null &&
                reservation.MechanicsOrdinal > acceptedBatchEnd &&
                (close == null ||
                 reservation.MechanicsOrdinal < close.MechanicsOrdinal) &&
                (reservation.Kind switch
                {
                    EffectTerminalAvailabilityReservationKind.LastUse =>
                        accepted.Activation.EffectTerminal &&
                        reservation.Subject == ownerSubject &&
                        reservation.ReactionFingerprint == null &&
                        reservation.ReplayStableOrderKey == null,
                    EffectTerminalAvailabilityReservationKind
                            .AfterCurrentReaction =>
                        reservation.ReactionFingerprint != null &&
                        reservation.ReplayStableOrderKey != null &&
                        accepted.Candidate.ReactionOutputs.Any(reaction =>
                            string.Equals(
                                reaction.Dependency,
                                "after_current_event",
                                StringComparison.Ordinal) &&
                            ResolveTerminalAvailabilitySubject(
                                reaction,
                                ownerSubject!) == reservation.Subject &&
                            string.Equals(
                                AcceptedMechanicsPlanner
                                    .CreateReactionCandidateOutputFingerprint(
                                        reaction),
                                reservation.ReactionFingerprint,
                                StringComparison.Ordinal) &&
                            string.Equals(
                                AcceptedMechanicsPlanner
                                    .CreateReplayStableReactionCandidateOutputFingerprint(
                                        reaction,
                                        accepted.Activation.Stamp.Identity.EffectId,
                                        accepted.Candidate.EffectAuthority),
                                reservation.ReplayStableOrderKey,
                                StringComparison.Ordinal)),
                    _ => false
                });
            if (!hasExactAuthority ||
                !actualReservationKeys.Add((
                    reservation.Boundary.BoundaryOrdinal,
                    reservation.Activation.ActivationOrdinal,
                    reservation.Subject,
                    reservation.Kind,
                    reservation.ReactionFingerprint,
                    reservation.ReplayStableOrderKey)))
            {
                issues.Add(Issue(
                    "effect_terminal_reservation_authority_invalid",
                    "one unique accepted last-use or after-current terminal authority",
                    reservation.Activation.Identity.EffectId));
            }
        }
        var expectedReservationKeys = new HashSet<(
            long BoundaryOrdinal,
            long ActivationOrdinal,
            EffectReplayIdentity Subject,
            EffectTerminalAvailabilityReservationKind Kind,
            string? ReactionFingerprint,
            string? ReplayStableOrderKey)>();
        foreach (var accepted in _accepted)
        {
            var ownerSubject = new EffectReplayIdentity(
                accepted.Activation.Stamp.Identity.EffectId,
                accepted.Candidate.EffectAuthority);
            if (accepted.Activation.EffectTerminal)
            {
                expectedReservationKeys.Add((
                    accepted.Boundary.BoundaryOrdinal,
                    accepted.Activation.Stamp.ActivationOrdinal,
                    ownerSubject,
                    EffectTerminalAvailabilityReservationKind.LastUse,
                    null,
                    null));
            }
            foreach (var reaction in accepted.Candidate.ReactionOutputs.Where(
                         static reaction =>
                             string.Equals(
                                 reaction.Dependency,
                                 "after_current_event",
                                 StringComparison.Ordinal)))
            {
                var subject = ResolveTerminalAvailabilitySubject(
                    reaction,
                    ownerSubject);
                if (subject == null)
                    continue;
                expectedReservationKeys.Add((
                    accepted.Boundary.BoundaryOrdinal,
                    accepted.Activation.Stamp.ActivationOrdinal,
                    subject,
                    EffectTerminalAvailabilityReservationKind
                        .AfterCurrentReaction,
                    AcceptedMechanicsPlanner
                        .CreateReactionCandidateOutputFingerprint(reaction),
                    AcceptedMechanicsPlanner
                        .CreateReplayStableReactionCandidateOutputFingerprint(
                            reaction,
                            accepted.Activation.Stamp.Identity.EffectId,
                            accepted.Candidate.EffectAuthority)));
            }
        }
        var hasDuplicateReplayOrderKey = _terminalReservations
            .Where(static reservation =>
                reservation.Kind ==
                    EffectTerminalAvailabilityReservationKind
                        .AfterCurrentReaction)
            .GroupBy(reservation => (
                reservation.Boundary.BoundaryOrdinal,
                reservation.Activation.ActivationOrdinal,
                reservation.ReplayStableOrderKey))
            .Any(static group => group.Count() != 1);
        if (actualReservationKeys.Count != _terminalReservations.Count ||
            !actualReservationKeys.SetEquals(expectedReservationKeys) ||
            hasDuplicateReplayOrderKey)
        {
            issues.Add(Issue(
                "effect_terminal_reservation_set_invalid",
                "exactly the accepted last-use and after-current terminal authorities",
                _terminalReservations.Count.ToString(CultureInfo.InvariantCulture)));
        }
        foreach (var rejected in _rejected)
        {
            var authority = FindCandidateAuthority(
                rejected.Boundary,
                rejected.Candidate);
            if (!_boundaries.Contains(rejected.Boundary) ||
                authority == null ||
                !string.Equals(
                    authority.Fingerprint,
                    rejected.CandidateFingerprint,
                    StringComparison.Ordinal) ||
                !Enum.IsDefined(rejected.Reason))
            {
                issues.Add(Issue(
                    "effect_boundary_rejected_candidate_invalid",
                    "one exact frozen rejected candidate and closed reason",
                    rejected.CandidateFingerprint));
            }
            if (rejected.Reason == EffectActivationRejectionReason.EffectTerminal)
            {
                var effectId = rejected.BlockedAvailabilityEffectId;
                if (string.IsNullOrEmpty(effectId) ||
                    !string.Equals(
                        effectId,
                        effectId.Trim(),
                        StringComparison.Ordinal) ||
                    !CandidateClaimsAvailabilityEffectId(
                        rejected.Candidate,
                        effectId))
                {
                    issues.Add(Issue(
                        "effect_boundary_terminal_rejection_subject_invalid",
                        "one exact activation or frozen terminal-reaction availability subject",
                        effectId ?? "null"));
                    continue;
                }
                var hasReservationAuthority = _terminalReservations.Any(
                    reservation =>
                        string.Equals(
                            reservation.Subject.EffectId,
                            effectId,
                            StringComparison.Ordinal) &&
                        (reservation.Boundary.BoundaryOrdinal <
                             rejected.Boundary.BoundaryOrdinal ||
                         reservation.Boundary == rejected.Boundary &&
                         string.Equals(
                             effectId,
                             rejected.Candidate.Activation.Identity.EffectId,
                             StringComparison.Ordinal) &&
                         reservation.Kind ==
                             EffectTerminalAvailabilityReservationKind.LastUse &&
                         AcceptedActivationPrecedesCandidate(
                             reservation.Activation,
                             rejected.Candidate.Activation)));
                var hasEarlierConditionalRelease = _released.Any(release =>
                    release.MechanicsOrdinal <
                        rejected.Boundary.OpenMechanicsOrdinal &&
                    string.Equals(
                        ResolveTerminalAvailabilityEffectId(release.Reaction),
                        effectId,
                        StringComparison.Ordinal));
                if (!hasReservationAuthority &&
                    !hasEarlierConditionalRelease)
                {
                    issues.Add(Issue(
                        "effect_boundary_terminal_rejection_authority_missing",
                        "an earlier boundary reservation, same-batch earlier last-use reservation, or earlier conditional terminal release",
                        Describe(rejected.Candidate.Activation.Identity)));
                }
            }
            else if (rejected.BlockedAvailabilityEffectId != null)
            {
                issues.Add(Issue(
                    "effect_boundary_terminal_rejection_subject_invalid",
                    "no availability subject for a non-terminal rejection",
                    rejected.BlockedAvailabilityEffectId));
            }
        }
        var orderedActivationOrdinals = _accepted
            .Select(static value => value.Activation.Stamp.ActivationOrdinal)
            .OrderBy(static value => value)
            .ToArray();
        if (orderedActivationOrdinals.Where((value, index) => value != index).Any())
        {
            issues.Add(Issue(
                "effect_boundary_activation_ordinal_invalid",
                "zero-based contiguous accepted activation ordinals",
                string.Join(",", orderedActivationOrdinals)));
        }

        var mutationKeys = new HashSet<ResourceOperationKey>();
        var transitionIds = new HashSet<string>(StringComparer.Ordinal);
        var mutationOrdinals = new HashSet<long>();
        foreach (var mutation in _resourceMutations)
        {
            if (mutation == null || mutation.Mutation == null ||
                mutation.Transition == null ||
                !mutationKeys.Add(mutation.Mutation) ||
                !transitionIds.Add(mutation.Transition.TransitionId) ||
                !mutationOrdinals.Add(mutation.MechanicsOrdinal) ||
                mutation.MechanicsOrdinal < 0 ||
                !TransitionMatchesMutation(
                    mutation.Transition,
                    mutation.Mutation))
            {
                issues.Add(Issue(
                    "effect_boundary_resource_mutation_invalid",
                    "one exact applied transition per mutation and mechanics ordinal",
                    mutation?.Mutation == null
                        ? "null"
                        : Describe(mutation.Mutation)));
            }
        }

        var appliedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var evidence in _applied)
        {
            var accepted = _accepted.FirstOrDefault(value =>
                value.Boundary == evidence.Boundary &&
                value.Activation.Stamp.Identity == evidence.Activation);
            var mutation = _resourceMutations.FirstOrDefault(value =>
                value.Mutation == evidence.Mutation &&
                value.ExecutionKind == ResourceMutationExecutionKind.Applied);
            var evidenceKey = Describe(evidence.Activation) + "\0" +
                              Describe(evidence.Mutation) + "\0" +
                              evidence.ComponentId;
            if (accepted == null || mutation == null ||
                !accepted.Candidate.PlannedComponentIdsByMutation.TryGetValue(
                    evidence.Mutation,
                    out var componentId) ||
                !string.Equals(
                    componentId,
                    evidence.ComponentId,
                    StringComparison.Ordinal) ||
                evidence.Transition != mutation.Transition ||
                evidence.MechanicsOrdinal != mutation.MechanicsOrdinal ||
                evidence.Transition.AppliedAmount == 0m ||
                !appliedKeys.Add(evidenceKey))
            {
                issues.Add(Issue(
                    "effect_boundary_component_evidence_invalid",
                    "one exact nonzero applied planned component transition",
                    evidenceKey));
            }
        }

        var releaseEventRefs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var release in _released)
        {
            var accepted = _accepted.FirstOrDefault(value =>
                value.Boundary == release.Boundary &&
                value.Activation.Stamp == release.Activation);
            var closeFound = closesByBoundary.TryGetValue(
                release.Boundary.BoundaryOrdinal,
                out var close);
            var isPendingOpenBoundary = pendingOpenChain.Contains(
                release.Boundary.BoundaryOrdinal);
            var candidateReaction = accepted?.Candidate.ReactionOutputs.Any(value =>
                string.Equals(
                    AcceptedMechanicsPlanner
                        .CreateReactionCandidateOutputFingerprint(value),
                    AcceptedMechanicsPlanner
                        .CreateReactionCandidateOutputFingerprint(release.Reaction),
                    StringComparison.Ordinal)) == true;
            var ordered = accepted != null &&
                (closeFound || isPendingOpenBoundary) &&
                release.MechanicsOrdinal > accepted.MechanicsOrdinal &&
                release.Stage switch
                {
                    EffectReactionReleaseStage.BeforeCurrentEvent =>
                        !closeFound ||
                        release.MechanicsOrdinal < close!.MechanicsOrdinal,
                    EffectReactionReleaseStage.AfterComponent =>
                        (!closeFound ||
                         release.MechanicsOrdinal < close!.MechanicsOrdinal) &&
                        _applied.Any(evidence =>
                            evidence.Boundary == release.Boundary &&
                            evidence.Activation == release.Activation.Identity &&
                            string.Equals(
                                evidence.ComponentId,
                                release.Reaction.AfterComponentId,
                                StringComparison.Ordinal) &&
                            evidence.Transition.AppliedAmount != 0m &&
                            evidence.MechanicsOrdinal < release.MechanicsOrdinal),
                    EffectReactionReleaseStage.AfterCurrentEvent =>
                        closeFound &&
                        release.MechanicsOrdinal < close!.MechanicsOrdinal &&
                        _boundaries
                            .Where(boundary =>
                                boundary.ParentBoundaryOrdinal ==
                                    release.Boundary.BoundaryOrdinal)
                            .All(child =>
                                closesByBoundary.TryGetValue(
                                    child.BoundaryOrdinal,
                                    out var childClose) &&
                                childClose.MechanicsOrdinal <
                                    release.MechanicsOrdinal),
                    _ => false
                };
            if (accepted == null || !candidateReaction ||
                !MatchesStage(release.Reaction.Dependency, release.Stage) ||
                !MatchesActivation(
                    release.Reaction,
                    release.Activation.Identity) ||
                !releaseEventRefs.Add(release.Reaction.EventRef) ||
                !ordered)
            {
                issues.Add(Issue(
                    "effect_boundary_reaction_release_invalid",
                    "one exact frozen candidate released at its causal stage",
                    release.Reaction.EventRef));
            }
        }

        foreach (var accepted in _accepted)
        {
            foreach (var reaction in accepted.Candidate.ReactionOutputs)
            {
                var fingerprint = AcceptedMechanicsPlanner
                    .CreateReactionCandidateOutputFingerprint(reaction);
                var isBeforeCurrent = string.Equals(
                    reaction.Dependency,
                    "before_current_event",
                    StringComparison.Ordinal);
                var isAfterCurrent = string.Equals(
                    reaction.Dependency,
                    "after_current_event",
                    StringComparison.Ordinal);
                var expected = isBeforeCurrent ||
                               isAfterCurrent &&
                               !pendingOpenChain.Contains(
                                   accepted.Boundary.BoundaryOrdinal) ||
                               !isBeforeCurrent &&
                               !isAfterCurrent &&
                               _applied.Any(evidence =>
                                   evidence.Boundary == accepted.Boundary &&
                                   evidence.Activation ==
                                       accepted.Activation.Stamp.Identity &&
                                   string.Equals(
                                       evidence.ComponentId,
                                       reaction.AfterComponentId,
                                       StringComparison.Ordinal) &&
                                   evidence.Transition.AppliedAmount != 0m);
                var actualCount = _released.Count(release =>
                    release.Boundary == accepted.Boundary &&
                    release.Activation == accepted.Activation.Stamp &&
                    string.Equals(
                        AcceptedMechanicsPlanner
                            .CreateReactionCandidateOutputFingerprint(
                                release.Reaction),
                        fingerprint,
                        StringComparison.Ordinal));
                if (actualCount != (expected ? 1 : 0))
                {
                    issues.Add(Issue(
                        "effect_boundary_reaction_release_set_invalid",
                        expected
                            ? "exactly one released frozen candidate"
                            : "no release without exact nonzero predecessor evidence",
                        reaction.EventRef));
                }
            }
        }

        var expectedExpansion = new Dictionary<
            EffectReactionExpansionKey,
            EffectReactionExpansionUsage>();
        foreach (var group in _released.GroupBy(static release =>
                     new EffectReactionExpansionKey(
                         release.Reaction.EffectId,
                         release.Reaction.ComponentId)))
        {
            var maxima = group
                .Select(static value => value.Reaction.MaxExpansion)
                .Distinct()
                .ToArray();
            if (maxima.Length != 1)
            {
                issues.Add(Issue(
                    "effect_boundary_expansion_policy_conflict",
                    "one source-owned maximum per reaction component",
                    group.Key.EffectId + "/" + group.Key.ComponentId));
                continue;
            }
            expectedExpansion.Add(
                group.Key,
                new EffectReactionExpansionUsage(group.Count(), maxima[0]));
        }
        if (_expansion.Count != expectedExpansion.Count ||
            expectedExpansion.Any(pair =>
                !_expansion.TryGetValue(pair.Key, out var usage) ||
                usage != pair.Value || usage.Count > usage.Maximum))
        {
            issues.Add(Issue(
                "effect_boundary_expansion_authority_invalid",
                "exact release-derived per-component expansion usage",
                _released.Count.ToString(CultureInfo.InvariantCulture)));
        }

        foreach (var terminal in _released.Where(static release =>
                     ResolveTerminalAvailabilityEffectId(release.Reaction) != null))
        {
            var unavailableEffectId = ResolveTerminalAvailabilityEffectId(
                terminal.Reaction)!;
            if (_accepted.Any(accepted =>
                    accepted.Boundary.BoundaryOrdinal >
                    terminal.Boundary.BoundaryOrdinal &&
                    CandidateClaimsAvailabilityEffectId(
                        accepted.Candidate,
                        unavailableEffectId)))
            {
                issues.Add(Issue(
                    "effect_boundary_terminal_eligibility_invalid",
                    "terminal release blocks the same effect in later boundaries",
                    unavailableEffectId));
            }
        }
        foreach (var terminal in _terminalReservations)
        {
            if (_accepted.Any(accepted =>
                    accepted.Boundary.BoundaryOrdinal >
                        terminal.Boundary.BoundaryOrdinal &&
                    CandidateClaimsAvailabilityEffectId(
                        accepted.Candidate,
                        terminal.Subject.EffectId)))
            {
                issues.Add(Issue(
                    "effect_boundary_terminal_eligibility_invalid",
                    "terminal reservation blocks the same effect in later boundaries",
                    terminal.Subject.EffectId));
            }
        }

        if (pendingFrontier != null)
        {
            if (_useProjectionOrdinal != null ||
                closesByBoundary.ContainsKey(pendingFrontier.Value))
            {
                issues.Add(Issue(
                    "effect_boundary_pending_frontier_invalid",
                    "one open pending frontier and no use projection",
                    pendingFrontier.Value.ToString(CultureInfo.InvariantCulture)));
            }
        }
        else if (_useProjectionOrdinal == null &&
                 validationBoundary == ValidationBoundary.Terminal)
        {
            issues.Add(Issue(
                "effect_boundary_use_projection_missing",
                "one terminal use/lifetime projection ordinal",
                "null"));
        }
        if (_useProjectionOrdinal != null || pendingFrontier != null ||
            validationBoundary == ValidationBoundary.ClosedPrefix)
        {
            var mechanicsOrdinals = _boundaries
                .Select(static value => value.OpenMechanicsOrdinal)
                .Concat(_boundaryCloses.Select(static value => value.MechanicsOrdinal))
                .Concat(_accepted.Select(static value => value.MechanicsOrdinal))
                .Concat(_terminalReservations.Select(
                    static value => value.MechanicsOrdinal))
                .Concat(_resourceMutations.Select(static value => value.MechanicsOrdinal))
                .Concat(_released.Select(static value => value.MechanicsOrdinal))
                .Concat(_useProjectionOrdinal is { } projectionOrdinal
                    ? new[] { projectionOrdinal }
                    : Array.Empty<long>())
                .OrderBy(static value => value)
                .ToArray();
            if (mechanicsOrdinals.Distinct().Count() != mechanicsOrdinals.Length ||
                mechanicsOrdinals.Where((value, index) => value != index).Any() ||
                _useProjectionOrdinal != null &&
                mechanicsOrdinals[^1] != _useProjectionOrdinal.Value)
            {
                issues.Add(Issue(
                    "effect_boundary_mechanics_order_invalid",
                    "zero-based contiguous unique authority ordinals ending in use projection",
                    string.Join(",", mechanicsOrdinals)));
            }
            if ((validationBoundary is ValidationBoundary.ClosedPrefix or ValidationBoundary.PendingPrefix) &&
                mechanicsOrdinals.LongLength != _nextMechanicsOrdinal)
            {
                issues.Add(Issue(
                    "effect_boundary_prefix_frontier_invalid",
                    "one observed mechanics ordinal for every allocated prefix ordinal",
                    "observed=" + mechanicsOrdinals.LongLength.ToString(CultureInfo.InvariantCulture) +
                    ";next=" + _nextMechanicsOrdinal.ToString(CultureInfo.InvariantCulture)));
            }
        }
        return issues;
    }

    private static bool IsExactPlanAuthorityToken(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool TransitionMatchesMutation(
        ResourceTransition transition,
        ResourceOperationKey mutation) =>
        string.Equals(
            transition.EventRef,
            mutation.EventRef,
            StringComparison.Ordinal) &&
        string.Equals(
            transition.OriginKind,
            mutation.OriginKind,
            StringComparison.Ordinal) &&
        string.Equals(
            transition.OriginId,
            mutation.OriginId,
            StringComparison.Ordinal) &&
        transition.Coordinate == mutation.Coordinate &&
        transition.Operation == (mutation.Operation switch
        {
            ResourceOperation.Damage => ResourceTransitionOperation.Damage,
            ResourceOperation.Restore => ResourceTransitionOperation.Restore,
            ResourceOperation.Spend => ResourceTransitionOperation.Spend,
            ResourceOperation.Gain => ResourceTransitionOperation.Gain,
            _ => (ResourceTransitionOperation)(-1)
        });

    private static EffectBoundaryTranscriptIssue Issue(
        string code,
        string expected,
        string actual) =>
        new(code, expected, actual);

    private static bool MatchesStage(
        string dependency,
        EffectReactionReleaseStage stage) => stage switch
        {
            EffectReactionReleaseStage.BeforeCurrentEvent =>
                string.Equals(
                    dependency,
                    "before_current_event",
                    StringComparison.Ordinal),
            EffectReactionReleaseStage.AfterComponent =>
                string.Equals(
                    dependency,
                    "after_component",
                    StringComparison.Ordinal),
            EffectReactionReleaseStage.AfterCurrentEvent =>
                string.Equals(
                    dependency,
                    "after_current_event",
                    StringComparison.Ordinal),
            _ => false
        };

    private static bool MatchesActivation(
        EffectReactionExecution reaction,
        EffectActivationCandidateIdentity identity) =>
        string.Equals(reaction.EffectId, identity.EffectId, StringComparison.Ordinal) &&
        string.Equals(reaction.TriggerId, identity.TriggerId, StringComparison.Ordinal) &&
        string.Equals(reaction.EventKind, identity.EventKind, StringComparison.Ordinal) &&
        string.Equals(
            reaction.TriggerEventRef,
            identity.TriggerEventRef,
            StringComparison.Ordinal);

    private static bool AcceptedActivationPrecedesCandidate(
        AcceptedEffectActivationTranscriptStamp accepted,
        EffectActivationCandidate rejected)
    {
        var comparison = accepted.Priority.CompareTo(rejected.Priority);
        if (comparison != 0)
            return comparison < 0;
        comparison = string.CompareOrdinal(
            accepted.Identity.TriggerId,
            rejected.Identity.TriggerId);
        if (comparison != 0)
            return comparison < 0;
        comparison = string.CompareOrdinal(
            accepted.Identity.EffectId,
            rejected.Identity.EffectId);
        if (comparison != 0)
            return comparison < 0;
        comparison = string.CompareOrdinal(
            accepted.Identity.EventKind,
            rejected.Identity.EventKind);
        if (comparison != 0)
            return comparison < 0;
        comparison = string.CompareOrdinal(
            accepted.Identity.EventRef,
            rejected.Identity.EventRef);
        return comparison != 0
            ? comparison < 0
            : string.CompareOrdinal(
                accepted.Identity.TriggerEventRef,
                rejected.Identity.TriggerEventRef) < 0;
    }

    private static string Describe(EffectActivationCandidateIdentity identity) =>
        string.Join(
            "/",
            identity.EffectId,
            identity.TriggerId,
            identity.EventKind,
            identity.EventRef,
            identity.TriggerEventRef);

    private static string Describe(ResourceOperationKey key) =>
        string.Join(
            "/",
            key.EventRef,
            key.OriginKind,
            key.OriginId,
            key.Coordinate.Realm,
            ResourceDefinitionCatalog.GetOwnerKindToken(
                key.Coordinate.OwnerKind),
            key.Coordinate.ResourceOwnerId,
            key.Coordinate.ResourceKey,
            key.Operation);
    }
}
