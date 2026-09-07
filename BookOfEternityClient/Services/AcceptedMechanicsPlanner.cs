using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record ResourceMutationEventRequirement(
    ResourceOperationKey Producer,
    string EventKind);

internal sealed record ResourceLossRecoveryPolicy(int RecoveryPercent);

internal sealed record ResourceMutationResultConstraint(
    decimal? RejectBelow,
    decimal? RejectAbove);

internal delegate EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution
    ResourceEventMutationResolver(
        ResourceAppliedEvent producerEvent,
        ResourceOperationKey producer);

internal sealed record ResourceMutationIntent(
    string EventRef,
    ResourceCoordinate Coordinate,
    decimal Amount,
    ResourceMutationSourceRequest Source,
    IReadOnlyList<ResourceOperationKey> Dependencies,
    IReadOnlyList<ResourceMutationEventRequirement> EventRequirements,
    string? ReceiptId,
    ResourceLossRecoveryPolicy? DerivedAmount = null,
    ResourceMutationResultConstraint? ResultConstraint = null)
{
    internal ResourceOperationKey Key => new(
        EventRef,
        Source.SourceKind,
        Source.SourceId,
        Coordinate,
        Source.Operation);
}

internal sealed record ResourceCapacityIntent(
    string EventRef,
    string OriginKind,
    string OriginId,
    ResourceCoordinate Coordinate,
    ResourceCapacityOperation Operation,
    ResolvedResourceCapacity? ResolvedCapacity,
    ResourceCurrentDisposition? CurrentDisposition,
    ResourceMutationPhase Phase,
    int Priority,
    ResourceSourceEvidence SourceEvidence,
    string PolicyFingerprint,
    string? ReceiptId)
{
    internal ResourceCapacityOperationKey Key => new(
        EventRef,
        OriginKind,
        OriginId,
        Coordinate,
        Operation);
}

internal sealed record ResourceCapacityOperationKey(
    string EventRef,
    string OriginKind,
    string OriginId,
    ResourceCoordinate Coordinate,
    ResourceCapacityOperation Operation);

internal sealed class AcceptedMechanicsResourceInput
{
    private readonly ResourceMutationIntent[] _mutations;
    private readonly ResourceCapacityIntent[] _capacityTransitions;
    private readonly EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate[]
        _initialTriggerCandidates;

    internal AcceptedMechanicsResourceInput(
        int Turn,
        ResourceDefinitionCatalog Definitions,
        ResourceStateLedger State,
        ResourceHistoryState History,
        ResourceMutationSourceCatalog Sources,
        IReadOnlyList<ResourceMutationIntent> Mutations,
        IReadOnlyList<ResourceCapacityIntent>? CapacityTransitions = null,
        int ExecutionSequenceOffset = 0,
        ResourceEventMutationResolver? EventMutationResolver = null,
        IReadOnlyList<EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>?
            InitialTriggerCandidates = null,
        EffectAcceptedTurnPlanner.EffectResourceResolutionWork?
            InitialEffectResolutionWork = null,
        EffectAcceptedPlanAuthorityStamp? EffectPlanAuthority = null)
    {
        if (Turn <= 0)
            throw new ArgumentOutOfRangeException(nameof(Turn));
        if (ExecutionSequenceOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(ExecutionSequenceOffset));
        this.Turn = Turn;
        this.ExecutionSequenceOffset = ExecutionSequenceOffset;
        this.Definitions = Definitions ?? throw new ArgumentNullException(nameof(Definitions));
        this.State = State ?? throw new ArgumentNullException(nameof(State));
        this.History = History ?? throw new ArgumentNullException(nameof(History));
        this.Sources = Sources ?? throw new ArgumentNullException(nameof(Sources));
        this.EventMutationResolver = EventMutationResolver;
        this.InitialEffectResolutionWork = InitialEffectResolutionWork ??
            EffectAcceptedTurnPlanner.EffectResourceResolutionWork.Empty;
        this.EffectPlanAuthority = EffectPlanAuthority is null
            ? null
            : EffectPlanAuthority with { };
        ArgumentNullException.ThrowIfNull(Mutations);
        _mutations = Mutations.Select(Clone).ToArray();
        _capacityTransitions = CapacityTransitions?.Select(value =>
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(value.Coordinate);
            ArgumentNullException.ThrowIfNull(value.SourceEvidence);
            return value;
        }).ToArray() ?? Array.Empty<ResourceCapacityIntent>();
        _initialTriggerCandidates = (InitialTriggerCandidates ??
            Array.Empty<EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>())
            .ToArray();
    }

    internal int Turn { get; }
    internal int ExecutionSequenceOffset { get; }
    internal ResourceDefinitionCatalog Definitions { get; }
    internal ResourceStateLedger State { get; }
    internal ResourceHistoryState History { get; }
    internal ResourceMutationSourceCatalog Sources { get; }
    internal ResourceEventMutationResolver? EventMutationResolver { get; }
    internal EffectAcceptedPlanAuthorityStamp? EffectPlanAuthority { get; }
    internal EffectAcceptedTurnPlanner.EffectResourceResolutionWork
        InitialEffectResolutionWork { get; }
    internal IReadOnlyList<EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>
        InitialTriggerCandidates =>
        Array.AsReadOnly(_initialTriggerCandidates.ToArray());
    internal IReadOnlyList<ResourceMutationIntent> Mutations =>
        Array.AsReadOnly(_mutations.Select(Clone).ToArray());
    internal IReadOnlyList<ResourceCapacityIntent> CapacityTransitions =>
        Array.AsReadOnly(_capacityTransitions.ToArray());

    private static ResourceMutationIntent Clone(ResourceMutationIntent value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(value.Coordinate);
        ArgumentNullException.ThrowIfNull(value.Source);
        ArgumentNullException.ThrowIfNull(value.Dependencies);
        ArgumentNullException.ThrowIfNull(value.EventRequirements);
        foreach (var dependency in value.Dependencies)
            ArgumentNullException.ThrowIfNull(dependency);
        foreach (var requirement in value.EventRequirements)
        {
            ArgumentNullException.ThrowIfNull(requirement);
            ArgumentNullException.ThrowIfNull(requirement.Producer);
        }
        return value with
        {
            Dependencies = value.Dependencies.ToArray(),
            EventRequirements = value.EventRequirements.ToArray()
        };
    }

}

internal sealed record AcceptedMechanicsPlannerStatistics(
    int HistorySeedCount,
    int HistoryAppendCount,
    int HistoryFreezeCount,
    int CapacityDescriptorCount,
    int MutationDescriptorCount,
    int GraphNodeDescriptorCount,
    int MaximumTriggerDepth,
    long SchedulingDescriptorVisitCount,
    int HistoryReplayIdentityLookupCount,
    long HistoryWorkUnits,
    int EffectTriggerIndexLookupCount,
    long EffectTriggerCandidateVisitCount,
    int EffectSourceBindingIndexLookupCount,
    long EffectSourceBindingCandidateVisitCount,
    long EffectRoutingDescriptorAccessCount,
    long EffectOccurrenceCloneCount,
    long EffectFullValidationPassCount,
    long EffectTriggerArrayVisitCount,
    long EffectComponentIndexLookupCount,
    long EffectSelectedComponentVisitCount,
    long PendingCandidateFingerprintOutputVisitCount,
    long PendingProjectionDependencyVisitCount,
    long PendingTranscriptPrefixStampVisitCount,
    int SourceAuthoritySeedCount,
    int SourceAuthorityAddVisitCount,
    int SourceAuthorityResolveLookupCount,
    int SourceAuthorityFreezeCount)
{
    internal long TotalWorkUnits =>
        (long)CapacityDescriptorCount +
        MutationDescriptorCount +
        GraphNodeDescriptorCount +
        SchedulingDescriptorVisitCount +
        HistoryWorkUnits +
        EffectTriggerIndexLookupCount +
        EffectTriggerCandidateVisitCount +
        EffectSourceBindingIndexLookupCount +
        EffectSourceBindingCandidateVisitCount +
        EffectRoutingDescriptorAccessCount +
        EffectOccurrenceCloneCount +
        EffectFullValidationPassCount +
        EffectTriggerArrayVisitCount +
        EffectComponentIndexLookupCount +
        EffectSelectedComponentVisitCount +
        PendingCandidateFingerprintOutputVisitCount +
        PendingProjectionDependencyVisitCount +
        PendingTranscriptPrefixStampVisitCount +
        SourceAuthoritySeedCount +
        SourceAuthorityAddVisitCount +
        SourceAuthorityResolveLookupCount +
        SourceAuthorityFreezeCount;
}

internal sealed record AcceptedEffectBoundedResourceResolution(
    EffectAcceptedTurnPlanner.EffectBoundedResourceResolution Resolution,
    ResourcePendingCausalAuthority CausalAuthority);

internal sealed class AcceptedMechanicsResourcePlanningResult
{
    private readonly ResourceAppliedEvent[] _events;
    private readonly ResourceTransition[] _appliedTransitions;
    private readonly ResourceTransition[] _replayTransitions;
    private readonly EffectAcceptedTurnPlanner.EffectResourceTriggerExecution[]
        _resourceTriggerExecutions;
    private readonly AcceptedEffectBoundedResourceResolution[]
        _acceptedPendingResolutions;
    private readonly EffectReactionExecution[] _acceptedReactionExecutions;
    private readonly string[] _acceptedResolvedPendingRequestIds;
    private readonly ValidationIssue[] _issues;

    internal AcceptedMechanicsResourcePlanningResult(
        ResourceStateLedger? stateAfterImage,
        ResourceHistoryState? historyAfterImage,
        IReadOnlyList<ResourceAppliedEvent> events,
        IReadOnlyList<ResourceTransition> appliedTransitions,
        IReadOnlyList<ResourceTransition> replayTransitions,
        IReadOnlyList<ValidationIssue> issues,
        AcceptedMechanicsPlannerStatistics statistics,
        IReadOnlyList<EffectAcceptedTurnPlanner.EffectResourceTriggerExecution>?
            resourceTriggerExecutions = null,
        IReadOnlyList<AcceptedEffectBoundedResourceResolution>?
            acceptedPendingResolutions = null,
        IReadOnlyList<EffectReactionExecution>? acceptedReactionExecutions = null,
        IReadOnlyList<string>? acceptedResolvedPendingRequestIds = null,
        AcceptedEffectBoundaryTranscript? effectBoundaryTranscript = null)
    {
        StateAfterImage = stateAfterImage;
        HistoryAfterImage = historyAfterImage;
        _events = (events ?? throw new ArgumentNullException(nameof(events))).ToArray();
        _appliedTransitions = (appliedTransitions ??
            throw new ArgumentNullException(nameof(appliedTransitions))).ToArray();
        _replayTransitions = (replayTransitions ??
            throw new ArgumentNullException(nameof(replayTransitions))).ToArray();
        _resourceTriggerExecutions = (resourceTriggerExecutions ??
            Array.Empty<EffectAcceptedTurnPlanner.EffectResourceTriggerExecution>())
            .Select(static execution => execution with
            {
                MutationKeys = execution.MutationKeys.ToArray(),
                ComponentIds = execution.ComponentIds?.ToArray(),
                ComponentIdsByMutation = execution.ComponentIdsByMutation == null
                    ? null
                    : new Dictionary<ResourceOperationKey, string>(
                        execution.ComponentIdsByMutation)
            })
            .ToArray();
        _acceptedPendingResolutions = (acceptedPendingResolutions ??
            Array.Empty<AcceptedEffectBoundedResourceResolution>())
            .Select(Clone)
            .ToArray();
        _acceptedReactionExecutions = (acceptedReactionExecutions ??
            Array.Empty<EffectReactionExecution>())
            .Select(Clone)
            .ToArray();
        _acceptedResolvedPendingRequestIds =
            (acceptedResolvedPendingRequestIds ?? Array.Empty<string>())
            .ToArray();
        EffectBoundaryTranscript = effectBoundaryTranscript;
        _issues = (issues ?? throw new ArgumentNullException(nameof(issues))).ToArray();
        Statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
    }

    internal ResourceStateLedger? StateAfterImage { get; }
    internal ResourceHistoryState? HistoryAfterImage { get; }
    internal IReadOnlyList<ResourceAppliedEvent> Events =>
        Array.AsReadOnly(_events.ToArray());
    internal IReadOnlyList<ResourceTransition> AppliedTransitions =>
        Array.AsReadOnly(_appliedTransitions.ToArray());
    internal IReadOnlyList<ResourceTransition> ReplayTransitions =>
        Array.AsReadOnly(_replayTransitions.ToArray());
    internal IReadOnlyList<EffectAcceptedTurnPlanner.EffectResourceTriggerExecution>
        ResourceTriggerExecutions =>
        Array.AsReadOnly(_resourceTriggerExecutions
            .Select(static execution => execution with
            {
                MutationKeys = execution.MutationKeys.ToArray(),
                ComponentIds = execution.ComponentIds?.ToArray(),
                ComponentIdsByMutation = execution.ComponentIdsByMutation == null
                    ? null
                    : new Dictionary<ResourceOperationKey, string>(
                        execution.ComponentIdsByMutation)
            })
            .ToArray());
    internal IReadOnlyList<AcceptedEffectBoundedResourceResolution>
        AcceptedPendingResolutions =>
        Array.AsReadOnly(_acceptedPendingResolutions.Select(Clone).ToArray());
    internal IReadOnlyList<EffectReactionExecution> AcceptedReactionExecutions =>
        Array.AsReadOnly(_acceptedReactionExecutions.Select(Clone).ToArray());
    internal IReadOnlyList<string> AcceptedResolvedPendingRequestIds =>
        Array.AsReadOnly(_acceptedResolvedPendingRequestIds.ToArray());
    internal AcceptedEffectBoundaryTranscript? EffectBoundaryTranscript { get; }
    internal IReadOnlyList<ValidationIssue> Issues =>
        Array.AsReadOnly(_issues.ToArray());
    internal AcceptedMechanicsPlannerStatistics Statistics { get; }
    [MemberNotNullWhen(
        true,
        nameof(StateAfterImage),
        nameof(HistoryAfterImage),
        nameof(EffectBoundaryTranscript))]
    internal bool IsValid =>
        StateAfterImage != null &&
        HistoryAfterImage != null &&
        EffectBoundaryTranscript != null &&
        Issues.Count == 0;

    private static AcceptedEffectBoundedResourceResolution Clone(
        AcceptedEffectBoundedResourceResolution value) =>
        value with
        {
            Resolution = value.Resolution with
            {
                Source = value.Resolution.Source.DeepClone().AsObject(),
                Target = value.Resolution.Target.DeepClone().AsObject(),
                Dependencies = value.Resolution.Dependencies.ToArray(),
                EventRequirements = value.Resolution.EventRequirements.ToArray()
            },
            CausalAuthority = value.CausalAuthority with { }
        };

    private static EffectReactionExecution Clone(EffectReactionExecution value) =>
        value with
        {
            DownstreamSource = value.DownstreamSource == null
                ? null
                : value.DownstreamSource with
                {
                    Definition = value.DownstreamSource.Definition.DeepClone().AsObject()
                },
            Parameters = value.Parameters?.DeepClone().AsObject()
        };
}

internal class AcceptedMechanicsIdentityFactory
{
    private readonly Func<Guid> _guidFactory;

    internal AcceptedMechanicsIdentityFactory(Func<Guid>? guidFactory = null) =>
        _guidFactory = guidFactory ?? Guid.NewGuid;

    internal virtual string CreateOperationId() =>
        "resource_operation_" + _guidFactory().ToString("N");

    internal virtual string CreateTransitionId() =>
        "resource_transition_" + _guidFactory().ToString("N");

    internal virtual string CreateOperationId(ResourceMutationIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        return CreateOperationId();
    }

    internal virtual string CreateTransitionId(ResourceMutationIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        return CreateTransitionId();
    }

    internal virtual string CreateOperationId(ResourceCapacityIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        return CreateOperationId();
    }

    internal virtual string CreateTransitionId(ResourceCapacityIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        return CreateTransitionId();
    }
}

internal sealed class AcceptedMechanicsCarrierCompositionResult
{
    private readonly Dictionary<string, JsonObject> _effectCarrierAfterImages;
    private readonly Dictionary<string, JsonObject> _ownerCompanionAfterImages;
    private readonly ValidationIssue[] _issues;
    private readonly AcceptedMechanicsWoundPublication? _woundPublication;

    private AcceptedMechanicsCarrierCompositionResult(
        IReadOnlyDictionary<string, JsonObject> effectCarrierAfterImages,
        IReadOnlyDictionary<string, JsonObject> ownerCompanionAfterImages,
        AcceptedMechanicsWoundPublication? woundPublication,
        IReadOnlyList<ValidationIssue> issues)
    {
        _effectCarrierAfterImages = CloneRoots(effectCarrierAfterImages);
        _ownerCompanionAfterImages = CloneRoots(ownerCompanionAfterImages);
        _woundPublication = woundPublication?.DetachedCopy();
        _issues = (issues ?? throw new ArgumentNullException(nameof(issues)))
            .Select(static issue => new ValidationIssue(
                issue.FilePath,
                issue.Severity,
                issue.Message,
                issue.Code,
                issue.Actor,
                issue.Section,
                issue.Expected,
                issue.Actual,
                issue.RepairHint,
                issue.Category,
                issue.RepairTargetFiles))
            .ToArray();
    }

    internal static AcceptedMechanicsCarrierCompositionResult CreateValidated(
        IReadOnlyDictionary<string, JsonObject> effectCarrierAfterImages,
        IReadOnlyDictionary<string, JsonObject> ownerCompanionAfterImages,
        AcceptedMechanicsWoundPublication? woundPublication,
        IReadOnlyList<ValidationIssue> issues,
        AcceptedMechanicsCarrierAssembler.ValidatedPublicationProof proof)
    {
        if (!AcceptedMechanicsCarrierAssembler.IsPublicationProof(proof))
        {
            throw new ArgumentException(
                "Only the typed accepted-mechanics carrier assembler may create a carrier composition.",
                nameof(proof));
        }
        return new AcceptedMechanicsCarrierCompositionResult(
            effectCarrierAfterImages,
            ownerCompanionAfterImages,
            woundPublication,
            issues);
    }

    internal IReadOnlyDictionary<string, JsonObject> EffectCarrierAfterImages =>
        CloneRoots(_effectCarrierAfterImages);

    internal IReadOnlyDictionary<string, JsonObject> OwnerCompanionAfterImages =>
        CloneRoots(_ownerCompanionAfterImages);

    internal AcceptedMechanicsWoundPublication? WoundPublication =>
        _woundPublication?.DetachedCopy();

    internal IReadOnlyList<ValidationIssue> Issues =>
        Array.AsReadOnly(_issues.ToArray());

    internal bool Success => _issues.Length == 0;

    private static Dictionary<string, JsonObject> CloneRoots(
        IReadOnlyDictionary<string, JsonObject> roots) =>
        (roots ?? throw new ArgumentNullException(nameof(roots))).ToDictionary(
            static pair => pair.Key,
            static pair => (pair.Value ?? throw new ArgumentNullException(
                nameof(roots))).DeepClone().AsObject(),
            StringComparer.Ordinal);
}

internal static class AcceptedMechanicsCarrierAssembler
{
    internal sealed class ValidatedPublicationProof
    {
        private ValidatedPublicationProof()
        {
        }

        internal static ValidatedPublicationProof Create() => new();
    }

    private static readonly ValidatedPublicationProof PublicationProof =
        ValidatedPublicationProof.Create();

    internal static bool IsPublicationProof(
        ValidatedPublicationProof? proof) =>
        ReferenceEquals(PublicationProof, proof);

    internal static IReadOnlyList<ValidationIssue> ValidateInitialEffectPlan(
        EffectAcceptedTurnPlan? effectPlan,
        AcceptedMechanicsWoundStageBundle woundStages)
    {
        ArgumentNullException.ThrowIfNull(woundStages);
        try
        {
            var stageEffectPlan = woundStages.EffectBatchPlan.EffectPlan;
            if (effectPlan is not null &&
                string.Equals(
                    WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(
                        effectPlan),
                    WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(
                        stageEffectPlan),
                    StringComparison.Ordinal))
            {
                return Array.Empty<ValidationIssue>();
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or
                JsonException or NullReferenceException)
        {
            return new[]
            {
                Issue(
                    "acceptedMechanics.woundCarrierAssembly",
                    "accepted_mechanics_wound_effect_plan_mismatch",
                    "The common effect plan could not be matched to the sealed wound effect stage.",
                    "one exact common and wound-stage effect plan",
                    exception.GetType().Name)
            };
        }

        return new[]
        {
            Issue(
                "acceptedMechanics.woundCarrierAssembly",
                "accepted_mechanics_wound_effect_plan_mismatch",
                "The common effect plan does not equal the sealed wound effect stage.",
                "one exact common and wound-stage effect plan",
                effectPlan is null ? "missing" : "different payload")
        };
    }

    internal static AcceptedMechanicsCarrierCompositionResult Compose(
        EffectAcceptedTurnPlan? effectPlan,
        IReadOnlyDictionary<string, JsonObject> ownerCompanionAfterImages,
        AcceptedMechanicsWoundStageBundle? woundStages,
        MortalWoundCanonicalAnchorPlan? woundAnchorPlan = null)
    {
        ArgumentNullException.ThrowIfNull(ownerCompanionAfterImages);

        try
        {
            var effectRoots = CloneRoots(
                effectPlan?.CarrierAfterImages ??
                new Dictionary<string, JsonObject>(StringComparer.Ordinal));
            var ownerRoots = CloneRoots(ownerCompanionAfterImages);
            var acceptedEffectBaselines = effectPlan?.AcceptedCarrierBaselines;
            foreach (var pair in effectRoots.OrderBy(
                         static pair => pair.Key,
                         StringComparer.Ordinal))
            {
                var acceptedBaseline = GetEffectRoot(
                    acceptedEffectBaselines!,
                    pair.Key);
                if (acceptedBaseline is null)
                {
                    if (!IsValidPristineEffectRoot(pair.Key, pair.Value))
                    {
                        return Failed(
                            "accepted_mechanics_effect_baseline_missing",
                            "An effect after-image has no accepted baseline or registered pristine initialization shape.",
                            "one exact accepted carrier baseline or player/NPC pristine effect root",
                            pair.Key);
                    }
                    continue;
                }
                if (!NonEffectFieldsAgree(
                        pair.Key,
                        acceptedBaseline,
                        pair.Value))
                {
                    return Failed(
                        "accepted_mechanics_effect_owner_delta_invalid",
                        "The effect after-image changed fields outside its registered effect collections.",
                        "the exact accepted root plus effect-collection changes only",
                        pair.Key);
                }
            }
            foreach (var path in effectRoots.Keys
                         .Intersect(ownerRoots.Keys, StringComparer.Ordinal)
                         .OrderBy(static value => value, StringComparer.Ordinal)
                         .ToArray())
            {
                var acceptedBaseline = GetEffectRoot(
                    acceptedEffectBaselines!,
                    path);
                if (acceptedBaseline is null ||
                    !JsonNode.DeepEquals(acceptedBaseline, ownerRoots[path]))
                {
                    return Failed(
                        "accepted_mechanics_effect_owner_baseline_mismatch",
                        "The effect after-image was not built on the exact typed owner after-image.",
                        path,
                        acceptedBaseline is null
                            ? "unregistered effect carrier path"
                            : "different owner baseline");
                }
                ownerRoots.Remove(path);
            }

            if (woundStages is null)
            {
                if (woundAnchorPlan is not null)
                {
                    return Failed(
                        "accepted_mechanics_wound_anchor_authority_mismatch",
                        "A canonical wound anchor plan has no wound stage bundle.",
                        "no anchor plan without one exact wound stage bundle",
                        woundAnchorPlan.Fingerprint);
                }
                return AcceptedMechanicsCarrierCompositionResult.CreateValidated(
                    effectRoots,
                    ownerRoots,
                    null,
                    Array.Empty<ValidationIssue>(),
                    PublicationProof);
            }
            if (woundAnchorPlan is not null &&
                !woundAnchorPlan.AgreesWith(woundStages))
            {
                return Failed(
                    "accepted_mechanics_wound_anchor_authority_mismatch",
                    "The canonical wound anchor plan does not bind this sealed wound stage bundle.",
                    woundStages.BundleFingerprint,
                    woundAnchorPlan.WoundStageBundleFingerprint);
            }
            if (effectPlan is null)
            {
                return Failed(
                    "accepted_mechanics_wound_effect_plan_mismatch",
                    "A wound transaction has no final common effect plan.",
                    "one finalized effect plan",
                    "missing");
            }
            var membershipFailure = ValidateFinalWoundEffectMembership(
                effectPlan,
                woundStages);
            if (membershipFailure is not null)
                return Failed(membershipFailure);
            if (!effectPlan.IsAcceptedBoundaryComplete ||
                !string.Equals(
                    effectPlan.AcceptedBoundaryBasePlanFingerprint,
                    WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(
                        woundStages.EffectBatchPlan.EffectPlan),
                    StringComparison.Ordinal) ||
                !string.Equals(
                    effectPlan.AcceptedBoundaryFinalPlanFingerprint,
                    WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(
                        effectPlan),
                    StringComparison.Ordinal))
            {
                return Failed(
                    "accepted_mechanics_wound_effect_completion_required",
                    "Wound carrier publication requires the typed completed effect boundary result.",
                    "one effect plan produced by CompleteAcceptedBoundaryTranscript",
                    "raw or reconstructed effect plan");
            }

            var input = woundStages.Input;
            var final = woundStages.FinalPlan;
            var woundRoots = new Dictionary<string, JsonObject>(
                StringComparer.Ordinal);
            var contributionOwners = new HashSet<WoundOwnerCoordinate>();
            var projectedAnchorWounds = new Dictionary<
                string,
                (WoundMaterializationEnvelope Before, WoundMaterializationEnvelope After)>(
                StringComparer.Ordinal);
            foreach (var pathGroup in final.CarrierContributions
                         .GroupBy(static contribution =>
                             contribution.Owner.CarrierPath,
                             StringComparer.Ordinal)
                         .OrderBy(static group => group.Key, StringComparer.Ordinal))
            {
                var path = pathGroup.Key;
                var baseline = WoundCarrierCollectionAuthority.GetRoot(
                    input.PreTurnCarriers,
                    path);
                if (baseline is null)
                {
                    return Failed(
                        "accepted_mechanics_wound_carrier_missing",
                        "The sealed pre-turn wound carrier root is absent.",
                        path,
                        "missing");
                }

                var selected = effectRoots.TryGetValue(path, out var effectRoot)
                    ? effectRoot.DeepClone().AsObject()
                    : ownerRoots.TryGetValue(path, out var ownerRoot)
                        ? ownerRoot.DeepClone().AsObject()
                        : baseline.DeepClone().AsObject();
                foreach (var contribution in pathGroup
                             .OrderBy(static value => value.Owner.Realm,
                                 StringComparer.Ordinal)
                             .ThenBy(static value => value.Owner.OwnerKind,
                                 StringComparer.Ordinal)
                             .ThenBy(static value => value.Owner.OwnerId,
                                 StringComparer.Ordinal))
                {
                    var owner = contribution.Owner;
                    if (!contributionOwners.Add(owner))
                    {
                        return Failed(
                            "accepted_mechanics_wound_owner_contribution_duplicate",
                            "A wound owner coordinate has more than one typed root contribution.",
                            "one contribution per exact owner coordinate",
                            DescribeOwner(owner));
                    }

                    if (!TryResolveWoundCollection(
                            baseline,
                            owner,
                            out var baselineCollection,
                            out var baselineFailure))
                    {
                        return Failed(baselineFailure!);
                    }
                    var expected = ComputeWoundCollectionFingerprint(
                        owner,
                        baselineCollection!);
                    if (!string.Equals(
                            expected,
                            contribution.ExpectedWoundCollectionFingerprint,
                            StringComparison.Ordinal))
                    {
                        return Failed(
                            "accepted_mechanics_wound_contribution_stale",
                            "The wound contribution does not bind the sealed pre-turn wound collection.",
                            expected,
                            contribution.ExpectedWoundCollectionFingerprint);
                    }

                    if (!TryResolveWoundCollection(
                            selected,
                            owner,
                            out var selectedCollection,
                            out var selectedFailure))
                    {
                        return Failed(selectedFailure!);
                    }
                    var selectedFingerprint = ComputeWoundCollectionFingerprint(
                        owner,
                        selectedCollection!);
                    if (!string.Equals(
                            expected,
                            selectedFingerprint,
                            StringComparison.Ordinal))
                    {
                        return Failed(
                            "accepted_mechanics_wound_collection_stale",
                            "Another same-root producer changed the wound collection before typed wound assembly.",
                            expected,
                            selectedFingerprint);
                    }

                    var mutationIds = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var mutation in contribution.Mutations)
                    {
                        if (!mutationIds.Add(mutation.WoundId))
                        {
                            return Failed(
                                "accepted_mechanics_wound_mutation_duplicate",
                                "One wound contribution mutates the same wound identity more than once.",
                                "one mutation per woundId",
                                mutation.WoundId);
                        }
                        var effectiveMutation = mutation;
                        if (woundAnchorPlan is not null &&
                            woundAnchorPlan.TryGetAllocation(
                                mutation.WoundId,
                                out var allocation))
                        {
                            if (!string.Equals(
                                    mutation.Operation,
                                    "add",
                                    StringComparison.Ordinal) ||
                                mutation.BeforeWound is not null ||
                                mutation.AfterWound is not { } unanchored ||
                                !string.Equals(
                                    unanchored.LastTransition.TransitionId,
                                    allocation.TransitionId,
                                    StringComparison.Ordinal) ||
                                !string.Equals(
                                    unanchored.LastTransition.Kind,
                                    "create",
                                    StringComparison.Ordinal) ||
                                !string.Equals(
                                    unanchored.Classification.Domain,
                                    "physical",
                                    StringComparison.Ordinal) ||
                                unanchored.Recovery.RecoveryAnchor is not null ||
                                unanchored.Recovery.DeteriorationAnchor is not null)
                            {
                                return Failed(
                                    "accepted_mechanics_wound_anchor_projection_invalid",
                                    "A canonical anchor allocation does not select one unanchored physical create.",
                                    allocation.WoundId + "/" + allocation.TransitionId,
                                    mutation.Operation + "/" + mutation.WoundId);
                            }
                            var anchored = unanchored with
                            {
                                Recovery = unanchored.Recovery with
                                {
                                    RecoveryAnchor = allocation.RecoveryAnchor with { },
                                    DeteriorationAnchor =
                                        allocation.DeteriorationAnchor is { } deterioration
                                            ? deterioration with { }
                                            : null
                                }
                            };
                            if (!projectedAnchorWounds.TryAdd(
                                    mutation.WoundId,
                                    (unanchored, anchored)))
                            {
                                return Failed(
                                    "accepted_mechanics_wound_anchor_projection_duplicate",
                                    "A canonical anchor plan selected one wound more than once.",
                                    "one allocation per newly created physical wound",
                                    mutation.WoundId);
                            }
                            effectiveMutation = new WoundCarrierMutation(
                                mutation.Operation,
                                mutation.WoundId,
                                beforeWound: null,
                                anchored);
                        }
                        var mutationFailure = ApplyMutation(
                            selectedCollection,
                            owner,
                            effectiveMutation);
                        if (mutationFailure is not null)
                            return Failed(mutationFailure);
                    }
                }

                woundRoots.Add(path, selected);
                effectRoots.Remove(path);
                ownerRoots.Remove(path);
            }

            if (woundAnchorPlan is not null &&
                projectedAnchorWounds.Count != woundAnchorPlan.Allocations.Count)
            {
                return Failed(
                    "accepted_mechanics_wound_anchor_projection_incomplete",
                    "Not every sealed canonical anchor allocation selected one final wound mutation.",
                    woundAnchorPlan.Allocations.Count.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                    projectedAnchorWounds.Count.ToString(
                        System.Globalization.CultureInfo.InvariantCulture));
            }

            var assembledCarriers = WoundAcceptedTurnData.CloneWoundCarriers(
                input.PreTurnCarriers)!;
            foreach (var pair in effectRoots
                         .Concat(ownerRoots)
                         .Concat(woundRoots)
                         .Where(pair =>
                             WoundCarrierCollectionAuthority.IsRegisteredPath(
                                 pair.Key)))
            {
                assembledCarriers = WoundCarrierCollectionAuthority.WithRoot(
                    assembledCarriers,
                    pair.Key,
                    pair.Value);
            }
            var assembledCatalog = WoundCarrierCatalog.Build(assembledCarriers);
            var projectedIdentity = final.IdentityIndexAfterImage;
            var projectedHistory = final.HistoryAfterImage;
            if (woundAnchorPlan is not null)
            {
                var projectionFailure = ProjectAnchorAgreementRoots(
                    projectedIdentity,
                    projectedHistory,
                    projectedAnchorWounds);
                if (projectionFailure is not null)
                    return Failed(projectionFailure);
            }
            var identity = WoundIdentityState.Parse(
                projectedIdentity.ToJsonString(),
                WoundIdentityState.StatePath);
            var history = WoundHistoryState.Parse(
                projectedHistory.ToJsonString(),
                WoundHistoryState.HistoryPath);
            var agreementIssues = new List<ValidationIssue>();
            agreementIssues.AddRange(assembledCatalog.Issues);
            agreementIssues.AddRange(identity.Issues);
            agreementIssues.AddRange(history.Issues);
            if (identity.State is not null && history.State is not null &&
                assembledCatalog.Issues.Count == 0)
            {
                agreementIssues.AddRange(history.State.ValidateAgreement(
                    identity.State,
                    assembledCatalog));
            }
            if (agreementIssues.Count != 0 || identity.State is null ||
                history.State is null)
            {
                return Failed(
                    "accepted_mechanics_wound_publication_agreement_invalid",
                    "The composed wound carriers do not agree with final wound identity and history.",
                    "one canonical agreeing carrier, identity, and history result",
                    string.Join(
                        ",",
                        agreementIssues.Select(static issue => issue.Code ??
                            issue.FilePath)));
            }

            var publication = AcceptedMechanicsWoundPublication.CreateValidated(
                woundRoots,
                projectedIdentity,
                projectedHistory,
                woundStages.BundleFingerprint,
                WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(
                    effectPlan),
                woundAnchorPlan?.Fingerprint,
                PublicationProof);
            return AcceptedMechanicsCarrierCompositionResult.CreateValidated(
                effectRoots,
                ownerRoots,
                publication,
                Array.Empty<ValidationIssue>(),
                PublicationProof);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or
                JsonException or NullReferenceException)
        {
            return Failed(
                "accepted_mechanics_wound_root_assembly_invalid",
                "The detached wound carrier contributions could not be assembled atomically.",
                "one exact typed same-root composition",
                exception.GetType().Name);
        }
    }

    private static ValidationIssue? ProjectAnchorAgreementRoots(
        JsonObject identityAfterImage,
        JsonObject historyAfterImage,
        IReadOnlyDictionary<
            string,
            (WoundMaterializationEnvelope Before, WoundMaterializationEnvelope After)>
            projectedWounds)
    {
        ArgumentNullException.ThrowIfNull(identityAfterImage);
        ArgumentNullException.ThrowIfNull(historyAfterImage);
        ArgumentNullException.ThrowIfNull(projectedWounds);
        var entries = identityAfterImage["entries"] as JsonArray;
        var transitions = historyAfterImage["transitions"] as JsonArray;
        if (entries is null || transitions is null)
        {
            return Issue(
                WoundIdentityState.StatePath,
                "accepted_mechanics_wound_anchor_agreement_root_invalid",
                "Canonical wound anchor projection requires identity entries and history transitions arrays.",
                "strict canonical identity and history after-images",
                "missing or malformed arrays");
        }

        foreach (var projection in projectedWounds.OrderBy(
                     static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            var woundId = projection.Key;
            var beforeFingerprint = WoundIdentityState.ComputeSemanticFingerprint(
                projection.Value.Before);
            var afterFingerprint = WoundIdentityState.ComputeSemanticFingerprint(
                projection.Value.After);
            var identityMatches = entries
                .OfType<JsonObject>()
                .Where(entry => string.Equals(
                    entry["woundId"]?.GetValue<string>(),
                    woundId,
                    StringComparison.Ordinal))
                .ToArray();
            var transitionId = projection.Value.After.LastTransition.TransitionId;
            var historyMatches = transitions
                .OfType<JsonObject>()
                .Where(transition => string.Equals(
                    transition["woundId"]?.GetValue<string>(),
                    woundId,
                    StringComparison.Ordinal) &&
                    string.Equals(
                        transition["transitionId"]?.GetValue<string>(),
                        transitionId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        transition["kind"]?.GetValue<string>(),
                        "create",
                        StringComparison.Ordinal))
                .ToArray();
            if (identityMatches.Length != 1 ||
                historyMatches.Length != 1 ||
                !string.Equals(
                    identityMatches[0]["semanticFingerprint"]?.GetValue<string>(),
                    beforeFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    historyMatches[0]["afterFingerprint"]?.GetValue<string>(),
                    beforeFingerprint,
                    StringComparison.Ordinal))
            {
                return Issue(
                    WoundIdentityState.StatePath,
                    "accepted_mechanics_wound_anchor_agreement_mismatch",
                    "Canonical anchor projection does not match the sealed unanchored identity/history result.",
                    woundId + "/" + transitionId + "/" + beforeFingerprint,
                    $"identity={identityMatches.Length}; history={historyMatches.Length}");
            }

            identityMatches[0]["semanticFingerprint"] = afterFingerprint;
            historyMatches[0]["afterFingerprint"] = afterFingerprint;
        }

        return null;
    }

    private static ValidationIssue? ApplyMutation(
        JsonArray collection,
        WoundOwnerCoordinate owner,
        WoundCarrierMutation mutation)
    {
        var matches = collection
            .Select((node, index) => (Node: node, Index: index))
            .Where(value => value.Node is JsonObject wound &&
                string.Equals(
                    wound["woundId"]?.GetValue<string>(),
                    mutation.WoundId,
                    StringComparison.Ordinal))
            .ToArray();
        var before = mutation.BeforeWound;
        var after = mutation.AfterWound;
        if (after is not null &&
            (!string.Equals(after.WoundId, mutation.WoundId, StringComparison.Ordinal) ||
             after.Owner != owner))
        {
            return Issue(
                owner.CarrierPath,
                "accepted_mechanics_wound_mutation_owner_mismatch",
                "A typed wound mutation after-image does not match its owner coordinate.",
                DescribeOwner(owner),
                after.WoundId + "/" + DescribeOwner(after.Owner));
        }
        if (before is not null &&
            (!string.Equals(before.WoundId, mutation.WoundId, StringComparison.Ordinal) ||
             before.Owner != owner))
        {
            return Issue(
                owner.CarrierPath,
                "accepted_mechanics_wound_mutation_owner_mismatch",
                "A typed wound mutation before-image does not match its owner coordinate.",
                DescribeOwner(owner),
                before.WoundId + "/" + DescribeOwner(before.Owner));
        }

        switch (mutation.Operation)
        {
            case "add" when before is null && after is not null && matches.Length == 0:
                collection.Add(ParseWound(after));
                return null;
            case "update" when before is not null && after is not null &&
                                    matches.Length == 1 &&
                                    WoundCarrierCollectionAuthority.MatchesSemanticBeforeImage(
                                        matches[0].Node,
                                        before,
                                        owner,
                                        owner.CarrierPath + ".activeWounds.beforeImage"):
                collection[matches[0].Index] = ParseWound(after);
                return null;
            case "delete" when before is not null && after is null &&
                                    matches.Length == 1 &&
                                    WoundCarrierCollectionAuthority.MatchesSemanticBeforeImage(
                                        matches[0].Node,
                                        before,
                                        owner,
                                        owner.CarrierPath + ".activeWounds.beforeImage"):
                collection.RemoveAt(matches[0].Index);
                return null;
            default:
                return Issue(
                    owner.CarrierPath,
                    "accepted_mechanics_wound_mutation_conflict",
                    "A typed wound mutation does not agree with the exact selected carrier collection.",
                    "canonical add, update, or delete with exact before/after state",
                    mutation.Operation + "/" + mutation.WoundId +
                    $"/matches={matches.Length}");
        }
    }

    private static ValidationIssue? ValidateFinalWoundEffectMembership(
        EffectAcceptedTurnPlan finalEffectPlan,
        AcceptedMechanicsWoundStageBundle woundStages)
    {
        var stagedEffectPlan = woundStages.EffectBatchPlan.EffectPlan;
        if (!FinalEffectAuthorityAgrees(stagedEffectPlan, finalEffectPlan))
        {
            return Issue(
                "acceptedMechanics.woundCarrierAssembly",
                "accepted_mechanics_wound_final_effect_authority_mismatch",
                "The finalized effect plan does not descend from the sealed wound effect authority.",
                "exact immutable input, authority, baseline, before-image, and event fields",
                "foreign or changed final effect authority");
        }
        var stagedCatalog = EffectCarrierCatalog.Build(
            stagedEffectPlan.ResourceTriggerCarriers);
        var finalRuntimeCatalog = EffectCarrierCatalog.Build(
            finalEffectPlan.ResourceTriggerCarriers);
        var finalPublicationCatalog = EffectCarrierCatalog.Build(
            CreateEffectCarrierInput(finalEffectPlan.CarrierAfterImages));
        EffectIdentityParseResult? stagedIdentityParse = null;
        EffectIdentityParseResult? identityParse = null;
        if (woundStages.EffectBatchPlan.ApplicationResults.Count != 0 ||
            woundStages.EffectBatchPlan.TerminationResults.Count != 0)
        {
            using var stagedDocument = JsonDocument.Parse(
                stagedEffectPlan.IdentityIndexAfterImage.ToJsonString());
            stagedIdentityParse = EffectIdentityState.Parse(
                stagedDocument.RootElement,
                EffectIdentityState.StatePath);
            using var document = JsonDocument.Parse(
                finalEffectPlan.IdentityIndexAfterImage.ToJsonString());
            identityParse = EffectIdentityState.Parse(
                document.RootElement,
                EffectIdentityState.StatePath);
        }
        if (stagedCatalog.Issues.Count != 0 ||
            finalRuntimeCatalog.Issues.Count != 0 ||
            finalPublicationCatalog.Issues.Count != 0 ||
            stagedIdentityParse is { State: null } ||
            stagedIdentityParse?.Issues.Count > 0 ||
            identityParse is { State: null } ||
            identityParse?.Issues.Count > 0)
        {
            return Issue(
                "acceptedMechanics.woundCarrierAssembly",
                "accepted_mechanics_wound_final_effect_membership_invalid",
                "The staged or final effect publication evidence is not canonical.",
                "valid staged/runtime/publication carrier catalogs and final effect identity",
                string.Join(
                    ",",
                        stagedCatalog.Issues
                        .Concat(finalRuntimeCatalog.Issues)
                        .Concat(finalPublicationCatalog.Issues)
                        .Concat(stagedIdentityParse?.Issues ??
                            Array.Empty<ValidationIssue>())
                        .Concat(identityParse?.Issues ??
                            Array.Empty<ValidationIssue>())
                        .Select(static issue => issue.Code ?? issue.FilePath)));
        }

        foreach (var result in woundStages.EffectBatchPlan.ApplicationResults)
        {
            var preparedRoots = woundStages.PreparedPlan.EffectOperationBatches
                .SelectMany(static batch => batch.RootApplications)
                .Where(root => string.Equals(
                    root.ApplicationRef,
                    result.ApplicationRef,
                    StringComparison.Ordinal))
                .ToArray();
            var activeMatches = finalEffectPlan.ActiveEffects
                .Where(effect => ExactString(effect["effectId"], result.EffectId))
                .ToArray();
            if (preparedRoots.Length != 1 ||
                !stagedCatalog.TryResolveOne(result.EffectId, out var staged) ||
                !finalRuntimeCatalog.TryResolveOne(
                    result.EffectId,
                    out var finalRuntime) ||
                !finalPublicationCatalog.TryResolveOne(
                    result.EffectId,
                    out var finalPublication) ||
                staged.Coordinate != result.CarrierCoordinate ||
                finalRuntime.Coordinate != result.CarrierCoordinate ||
                finalPublication.Coordinate != result.CarrierCoordinate ||
                activeMatches.Length != 1 ||
                !JsonNode.DeepEquals(
                    finalRuntime.Effect,
                    finalPublication.Effect) ||
                !JsonNode.DeepEquals(finalRuntime.Effect, activeMatches[0]) ||
                !EffectIdentityFieldsAgree(
                    staged.Effect,
                    finalRuntime.Effect,
                    result) ||
                stagedIdentityParse?.State is null ||
                !stagedIdentityParse.State.TryGetEntry(
                    result.EffectId,
                    out var stagedIdentity) ||
                identityParse?.State is null ||
                !identityParse.State.TryGetEntry(
                    result.EffectId,
                    out var identity) ||
                !EffectProgressAgrees(
                    staged.Effect,
                    finalRuntime.Effect,
                    stagedIdentity,
                    identity,
                    woundStages.Input.Binding.Turn) ||
                !WoundAcceptedTurnPlannerCore.IdentityAgrees(
                    identity,
                    finalRuntime.Effect,
                    finalRuntime.Coordinate,
                    woundStages.Input.Binding.Turn) ||
                !CreateEvidenceAgrees(
                    identity,
                    finalRuntime.Effect,
                    result,
                    woundStages.Input.Binding.Turn,
                    preparedRoots[0].PriorRootEffectId) ||
                !finalEffectPlan.AllocatedEffectIds.Contains(
                    result.EffectId,
                    StringComparer.Ordinal) ||
                !finalEffectPlan.AllocatedTransitionIds.Contains(
                    result.CreateTransitionId,
                    StringComparer.Ordinal))
            {
                return Issue(
                    "acceptedMechanics.woundCarrierAssembly",
                    "accepted_mechanics_wound_final_effect_membership_invalid",
                    "A newly materialized wound effect is absent or changed incompatibly in the final effect plan.",
                    "the exact created wound effect identity, source, target, carrier, components, and links",
                    result.ApplicationRef + "/" + result.EffectId);
            }
        }

        return null;
    }

    private static bool EffectProgressAgrees(
        JsonObject staged,
        JsonObject final,
        EffectIdentityEntry stagedIdentity,
        EffectIdentityEntry finalIdentity,
        int acceptedTurn)
    {
        var immutableFields = new[]
        {
            "schemaVersion",
            "entityKind",
            "effectId",
            "state",
            "realm",
            "target",
            "display",
            "source",
            "components",
            "stacking",
            "triggers",
            "removal",
            "links"
        };
        if (immutableFields.Any(field =>
                !JsonNode.DeepEquals(staged[field], final[field])) ||
            staged["lifetime"] is not JsonObject stagedLifetime ||
            final["lifetime"] is not JsonObject finalLifetime ||
            staged["chronology"] is not JsonObject stagedChronology ||
            final["chronology"] is not JsonObject finalChronology)
        {
            return false;
        }

        var stagedLifetimePolicy = stagedLifetime.DeepClone().AsObject();
        var finalLifetimePolicy = finalLifetime.DeepClone().AsObject();
        stagedLifetimePolicy.Remove("remainingTurns");
        stagedLifetimePolicy.Remove("remainingUses");
        finalLifetimePolicy.Remove("remainingTurns");
        finalLifetimePolicy.Remove("remainingUses");
        if (!JsonNode.DeepEquals(
                   stagedLifetimePolicy,
                   finalLifetimePolicy) ||
            !JsonNode.DeepEquals(
                   stagedChronology["createdAtTurn"],
                   finalChronology["createdAtTurn"]) ||
            !JsonNode.DeepEquals(
                   stagedChronology["createdEventRef"],
                   finalChronology["createdEventRef"]) ||
            !JsonNode.DeepEquals(
                   stagedChronology["causalEventRef"],
                   finalChronology["causalEventRef"]) ||
            !IdentityTransitionPrefixAgrees(stagedIdentity, finalIdentity))
        {
            return false;
        }

        var addedTransitions = finalIdentity.Transitions
            .Skip(stagedIdentity.Transitions.Count)
            .ToArray();
        if (addedTransitions.Any(transition =>
                transition.Turn != acceptedTurn ||
                transition.SourceEffectIds.Count != 1 ||
                transition.ResultEffectIds.Count != 1 ||
                !string.Equals(
                    transition.SourceEffectIds[0],
                    stagedIdentity.EffectId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    transition.ResultEffectIds[0],
                    stagedIdentity.EffectId,
                    StringComparison.Ordinal) ||
                transition.ReceiptId is not null ||
                transition.Kind is not ("trigger" or "consume" or "suspend" or
                    "resume")))
        {
            return false;
        }

        if (!TryReadOptionalPositiveInt(
                stagedLifetime["remainingTurns"],
                out var stagedTurns) ||
            !TryReadOptionalPositiveInt(
                finalLifetime["remainingTurns"],
                out var finalTurns) ||
            !TryReadOptionalPositiveInt(
                stagedLifetime["remainingUses"],
                out var stagedUses) ||
            !TryReadOptionalPositiveInt(
                finalLifetime["remainingUses"],
                out var finalUses))
        {
            return false;
        }
        var consumed = addedTransitions.Count(static transition =>
            string.Equals(transition.Kind, "consume", StringComparison.Ordinal));
        var turnDelta = CounterDelta(stagedTurns, finalTurns);
        var useDelta = CounterDelta(stagedUses, finalUses);
        if (turnDelta < 0 || useDelta < 0 || turnDelta + useDelta != consumed)
            return false;

        var last = finalIdentity.Transitions[^1];
        return ExactString(
                   finalChronology["lastTransitionId"],
                   last.TransitionId) &&
               ExactInt(finalChronology["lastTransitionTurn"], last.Turn);
    }

    private static bool IdentityTransitionPrefixAgrees(
        EffectIdentityEntry staged,
        EffectIdentityEntry final)
    {
        if (!string.Equals(
                staged.EffectId,
                final.EffectId,
                StringComparison.Ordinal) ||
            staged.Transitions.Count > final.Transitions.Count)
        {
            return false;
        }
        for (var index = 0; index < staged.Transitions.Count; index++)
        {
            if (!JsonNode.DeepEquals(
                    staged.Transitions[index].Raw,
                    final.Transitions[index].Raw))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryReadOptionalPositiveInt(
        JsonNode? node,
        out int? value)
    {
        if (node is null)
        {
            value = null;
            return true;
        }
        if (node is JsonValue json &&
            json.TryGetValue<int>(out var parsed) && parsed > 0)
        {
            value = parsed;
            return true;
        }
        value = null;
        return false;
    }

    private static int CounterDelta(int? before, int? after)
    {
        if (!before.HasValue || !after.HasValue)
            return before == after ? 0 : -1;
        return before.Value - after.Value;
    }

    private static bool FinalEffectAuthorityAgrees(
        EffectAcceptedTurnPlan staged,
        EffectAcceptedTurnPlan final) =>
        string.Equals(
            staged.InputFingerprint,
            final.InputFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            staged.CarrierAuthorityFingerprint,
            final.CarrierAuthorityFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            staged.SourceAuthorityFingerprint,
            final.SourceAuthorityFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            staged.TargetAuthorityFingerprint,
            final.TargetAuthorityFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            staged.SkillScopeAuthority?.Fingerprint ?? "none",
            final.SkillScopeAuthority?.Fingerprint ?? "none",
            StringComparison.Ordinal) &&
        string.Equals(
            staged.SourceAuthority.CanonicalFingerprint,
            final.SourceAuthority.CanonicalFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            staged.SourceAuthority.Fingerprint,
            final.SourceAuthority.Fingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            staged.TargetAuthority.CanonicalFingerprint,
            final.TargetAuthority.CanonicalFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            staged.TargetAuthority.Fingerprint,
            final.TargetAuthority.Fingerprint,
            StringComparison.Ordinal) &&
        staged.AllocatedCombatantIds.SequenceEqual(
            final.AllocatedCombatantIds,
            StringComparer.Ordinal) &&
        JsonNode.DeepEquals(staged.EventInput, final.EventInput) &&
        JsonNode.DeepEquals(
            staged.IdentityIndexBeforeImage,
            final.IdentityIndexBeforeImage) &&
        staged.DeletedPaths.SequenceEqual(
            final.DeletedPaths,
            StringComparer.Ordinal) &&
        NullableObjectMapsEqual(
            staged.CarrierBeforeImages,
            final.CarrierBeforeImages) &&
        EffectCarrierInputsEqual(
            staged.AcceptedCarrierBaselines,
            final.AcceptedCarrierBaselines);

    private static bool NullableObjectMapsEqual(
        IReadOnlyDictionary<string, JsonObject?> first,
        IReadOnlyDictionary<string, JsonObject?> second) =>
        first.Count == second.Count &&
        first.All(pair => second.TryGetValue(pair.Key, out var value) &&
            JsonNode.DeepEquals(pair.Value, value));

    private static bool EffectCarrierInputsEqual(
        EffectCarrierCatalogInput first,
        EffectCarrierCatalogInput second) =>
        JsonNode.DeepEquals(first.PlayerEffects, second.PlayerEffects) &&
        JsonNode.DeepEquals(first.NpcEffects, second.NpcEffects) &&
        JsonNode.DeepEquals(
            first.EnemyCombatants,
            second.EnemyCombatants) &&
        JsonNode.DeepEquals(
            first.AllyCombatants,
            second.AllyCombatants) &&
        JsonNode.DeepEquals(
            first.AfterlifeProfiles,
            second.AfterlifeProfiles) &&
        JsonNode.DeepEquals(
            first.SpiritualConflict,
            second.SpiritualConflict);

    private static bool EffectIdentityFieldsAgree(
        JsonObject staged,
        JsonObject final,
        EffectAcceptedApplicationResult result) =>
        ExactString(staged["effectId"], result.EffectId) &&
        ExactString(final["effectId"], result.EffectId) &&
        string.Equals(
            result.SourceKey.Realm,
            result.TargetKey.Realm,
            StringComparison.Ordinal) &&
        ExactString(staged["realm"], result.SourceKey.Realm) &&
        ExactString(final["realm"], result.SourceKey.Realm) &&
        staged["source"] is JsonObject stagedSource &&
        final["source"] is JsonObject finalSource &&
        SourceAgrees(stagedSource, result.SourceKey) &&
        SourceAgrees(finalSource, result.SourceKey) &&
        staged["target"] is JsonObject stagedTarget &&
        final["target"] is JsonObject finalTarget &&
        TargetAgrees(stagedTarget, result.TargetKey) &&
        TargetAgrees(finalTarget, result.TargetKey);

    private static bool CreateEvidenceAgrees(
        EffectIdentityEntry identity,
        JsonObject effect,
        EffectAcceptedApplicationResult result,
        int acceptedTurn,
        string? priorRootEffectId)
    {
        if (effect["chronology"] is not JsonObject chronology ||
            !ExactInt(chronology["createdAtTurn"], acceptedTurn) ||
            !ExactString(
                chronology["createdEventRef"],
                result.CreatedEventRef) ||
            !ExactString(
                chronology["causalEventRef"],
                result.CausalEventRef))
        {
            return false;
        }

        var creates = identity.Transitions.Where(transition =>
                string.Equals(
                    transition.TransitionId,
                    result.CreateTransitionId,
                    StringComparison.Ordinal) &&
                string.Equals(transition.Kind, "create", StringComparison.Ordinal) &&
                transition.Turn == acceptedTurn &&
                string.Equals(
                    transition.EventRef,
                    result.CreatedEventRef,
                    StringComparison.Ordinal) &&
                transition.SourceEffectIds.SequenceEqual(
                    priorRootEffectId is null
                        ? Array.Empty<string>()
                        : new[] { priorRootEffectId },
                    StringComparer.Ordinal) &&
                transition.ResultEffectIds.Count == 1 &&
                string.Equals(
                    transition.ResultEffectIds[0],
                    result.EffectId,
                    StringComparison.Ordinal) &&
                transition.ReceiptId is null)
            .ToArray();
        return creates.Length == 1;
    }

    private static bool SourceAgrees(
        JsonObject source,
        EffectSourceKey key) =>
        ExactString(source["kind"], key.Kind) &&
        ExactString(source["sourceId"], key.SourceId) &&
        ExactString(source["definitionKey"], key.DefinitionKey);

    private static bool TargetAgrees(
        JsonObject target,
        EffectTargetKey key) =>
        ExactString(target["kind"], key.Kind) &&
        ExactString(target["targetId"], key.TargetId);

    private static bool ExactString(JsonNode? node, string expected) =>
        node is JsonValue value &&
        value.TryGetValue<string>(out var actual) &&
        string.Equals(actual, expected, StringComparison.Ordinal);

    private static bool ExactInt(JsonNode? node, int expected) =>
        node is JsonValue value &&
        value.TryGetValue<int>(out var actual) &&
        actual == expected;

    private static EffectCarrierCatalogInput CreateEffectCarrierInput(
        IReadOnlyDictionary<string, JsonObject> roots)
    {
        JsonObject? Read(string path) =>
            roots.TryGetValue(path, out var root)
                ? root.DeepClone().AsObject()
                : null;

        return new EffectCarrierCatalogInput(
            Read(EffectCarrierCatalog.PlayerPath),
            Read(EffectCarrierCatalog.NpcPath),
            Read(EffectCarrierCatalog.EnemiesPath),
            Read(EffectCarrierCatalog.AlliesPath),
            Read(EffectCarrierCatalog.AfterlifeProfilesPath),
            Read(EffectCarrierCatalog.SpiritualConflictPath));
    }

    private static bool NonEffectFieldsAgree(
        string path,
        JsonObject baseline,
        JsonObject afterImage)
    {
        var expected = baseline.DeepClone().AsObject();
        var actual = afterImage.DeepClone().AsObject();
        if (!RemoveEffectCollections(path, expected) ||
            !RemoveEffectCollections(path, actual))
        {
            return false;
        }
        return JsonNode.DeepEquals(expected, actual);
    }

    private static bool IsValidPristineEffectRoot(
        string path,
        JsonObject afterImage)
    {
        var projection = afterImage.DeepClone().AsObject();
        if (!RemoveEffectCollections(path, projection))
            return false;
        if (string.Equals(
                path,
                EffectCarrierCatalog.PlayerPath,
                StringComparison.Ordinal))
        {
            return projection.Count == 1 &&
                   ExactInt(
                       projection["schemaVersion"],
                       EffectMaterializationContract.SchemaVersion);
        }
        if (!string.Equals(
                path,
                EffectCarrierCatalog.NpcPath,
                StringComparison.Ordinal) ||
            !ExactInt(
                projection["schemaVersion"],
                EffectMaterializationContract.SchemaVersion) ||
            projection["entries"] is not JsonArray entries ||
            projection.Count != 2)
        {
            return false;
        }
        return entries.OfType<JsonObject>().All(static entry =>
            entry.Count == 1 &&
            entry["NPCId"] is JsonValue value &&
            value.TryGetValue<string>(out var npcId) &&
            ResourceMaterializationContract.IsExactIdentifier(npcId));
    }

    private static bool RemoveEffectCollections(
        string path,
        JsonObject root)
    {
        switch (path)
        {
            case EffectCarrierCatalog.PlayerPath:
                root.Remove("activeEffects");
                return true;
            case EffectCarrierCatalog.NpcPath:
                if (root["entries"] is not JsonArray entries)
                    return false;
                foreach (var entry in entries.OfType<JsonObject>())
                    entry.Remove("activeEffects");
                return true;
            case EffectCarrierCatalog.EnemiesPath:
                return RemoveCombatEffectCollections(root, "enemiesData");
            case EffectCarrierCatalog.AlliesPath:
                return RemoveCombatEffectCollections(root, "alliesData");
            case EffectCarrierCatalog.AfterlifeProfilesPath:
                if (root["profiles"] is not JsonArray profiles)
                    return false;
                foreach (var profile in profiles.OfType<JsonObject>())
                    profile.Remove("activeEffects");
                return true;
            case EffectCarrierCatalog.SpiritualConflictPath:
                if (root["activeConflict"] is JsonObject conflict)
                    conflict.Remove("combatConditions");
                return true;
            default:
                return false;
        }
    }

    private static bool RemoveCombatEffectCollections(
        JsonObject root,
        string collectionName)
    {
        if (root[collectionName] is not JsonArray combatants)
            return false;
        foreach (var combatant in combatants.OfType<JsonObject>())
        {
            combatant.Remove("activeBuffs");
            combatant.Remove("activeDebuffs");
            if (combatant["members"] is not JsonArray members)
                continue;
            foreach (var member in members.OfType<JsonObject>())
            {
                member.Remove("activeBuffs");
                member.Remove("activeDebuffs");
            }
        }
        return true;
    }

    private static JsonObject ParseWound(WoundMaterializationEnvelope wound) =>
        JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(wound))!
            .AsObject();

    private static string ComputeWoundCollectionFingerprint(
        WoundOwnerCoordinate owner,
        JsonArray collection) =>
        WoundCarrierCollectionAuthority.ComputeFingerprint(owner, collection);

    private static bool TryResolveWoundCollection(
        JsonObject root,
        WoundOwnerCoordinate owner,
        [NotNullWhen(true)] out JsonArray? collection,
        [NotNullWhen(false)] out ValidationIssue? failure)
    {
        if (WoundCarrierCollectionAuthority.TryResolve(
                root,
                owner,
                out collection,
                out var reason))
        {
            failure = null;
            return true;
        }

        failure = Issue(
            owner.CarrierPath,
            "accepted_mechanics_wound_owner_resolution_invalid",
            "The typed wound owner does not resolve to one exact activeWounds slot.",
            "one registered exact owner collection",
            DescribeOwner(owner) + "/" + reason);
        return false;
    }

    private static JsonObject? GetEffectRoot(
        EffectCarrierCatalogInput carriers,
        string path) => path switch
        {
            EffectCarrierCatalog.PlayerPath => carriers.PlayerEffects,
            EffectCarrierCatalog.NpcPath => carriers.NpcEffects,
            EffectCarrierCatalog.EnemiesPath => carriers.EnemyCombatants,
            EffectCarrierCatalog.AlliesPath => carriers.AllyCombatants,
            EffectCarrierCatalog.AfterlifeProfilesPath => carriers.AfterlifeProfiles,
            EffectCarrierCatalog.SpiritualConflictPath => carriers.SpiritualConflict,
            _ => null
        };

    private static Dictionary<string, JsonObject> CloneRoots(
        IReadOnlyDictionary<string, JsonObject> roots) =>
        roots.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DeepClone().AsObject(),
            StringComparer.Ordinal);

    private static AcceptedMechanicsCarrierCompositionResult Failed(
        ValidationIssue issue) =>
        AcceptedMechanicsCarrierCompositionResult.CreateValidated(
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            null,
            new[] { issue },
            PublicationProof);

    private static AcceptedMechanicsCarrierCompositionResult Failed(
        string code,
        string message,
        string expected,
        string actual) =>
        Failed(Issue(
            "acceptedMechanics.woundCarrierAssembly",
            code,
            message,
            expected,
            actual));

    private static ValidationIssue Issue(
        string path,
        string code,
        string message,
        string expected,
        string actual) =>
        new(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            actor: "Client",
            section: "wound_materialization",
            expected: expected,
            actual: actual,
            repairHint:
                "Rebuild one accepted common plan from the exact sealed before-images and typed wound/effect/owner contributions.");

    private static string DescribeOwner(WoundOwnerCoordinate owner) =>
        owner.Realm + "/" + owner.OwnerKind + "/" + owner.OwnerId + "/" +
        owner.CarrierPath;
}

internal sealed record WoundEffectLineageWorkStatistics(
    int SourceGroupIdentityCount,
    int VisitedIdentityCount);

internal sealed class WoundEffectLineagePlanningResult
{
    private readonly string[] _closureEffectIds;
    private readonly string[] _activeOrSuspendedEffectIds;
    private readonly IReadOnlyDictionary<string, WoundRootOwnershipDomain>
        _ownershipByEffectId;
    private readonly IReadOnlyDictionary<string, WoundRootOwnershipDomain>
        _ownershipByDefinitionKey;
    private readonly ValidationIssue[] _issues;

    internal WoundEffectLineagePlanningResult(
        IReadOnlyList<string> closureEffectIds,
        IReadOnlyList<string> activeOrSuspendedEffectIds,
        IReadOnlyDictionary<string, WoundRootOwnershipDomain> ownershipByEffectId,
        IReadOnlyDictionary<string, WoundRootOwnershipDomain>
            ownershipByDefinitionKey,
        WoundEffectLineageWorkStatistics work,
        IReadOnlyList<ValidationIssue> issues)
    {
        _closureEffectIds = (closureEffectIds ??
            throw new ArgumentNullException(nameof(closureEffectIds))).ToArray();
        _activeOrSuspendedEffectIds = (activeOrSuspendedEffectIds ??
            throw new ArgumentNullException(nameof(activeOrSuspendedEffectIds)))
            .ToArray();
        _ownershipByEffectId = (ownershipByEffectId ??
            throw new ArgumentNullException(nameof(ownershipByEffectId)))
            .ToDictionary(
                static pair => pair.Key,
                static pair => new WoundRootOwnershipDomain(
                    pair.Value.Kind,
                    pair.Value.ComplicationId),
                StringComparer.Ordinal);
        _ownershipByDefinitionKey = (ownershipByDefinitionKey ??
            throw new ArgumentNullException(nameof(ownershipByDefinitionKey)))
            .ToDictionary(
                static pair => pair.Key,
                static pair => new WoundRootOwnershipDomain(
                    pair.Value.Kind,
                    pair.Value.ComplicationId),
                StringComparer.Ordinal);
        Work = work ?? throw new ArgumentNullException(nameof(work));
        _issues = (issues ?? throw new ArgumentNullException(nameof(issues)))
            .ToArray();
    }

    internal bool Success => _issues.Length == 0;

    internal IReadOnlyList<string> ClosureEffectIds =>
        Array.AsReadOnly(_closureEffectIds.ToArray());

    internal IReadOnlyList<string> ActiveOrSuspendedEffectIds =>
        Array.AsReadOnly(_activeOrSuspendedEffectIds.ToArray());

    internal IReadOnlyDictionary<string, WoundRootOwnershipDomain>
        OwnershipByEffectId => new ReadOnlyDictionary<
            string,
            WoundRootOwnershipDomain>(
                _ownershipByEffectId.ToDictionary(
                    static pair => pair.Key,
                    static pair => new WoundRootOwnershipDomain(
                        pair.Value.Kind,
                        pair.Value.ComplicationId),
                    StringComparer.Ordinal));

    internal IReadOnlyDictionary<string, WoundRootOwnershipDomain>
        OwnershipByDefinitionKey => new ReadOnlyDictionary<
            string,
            WoundRootOwnershipDomain>(
                _ownershipByDefinitionKey.ToDictionary(
                    static pair => pair.Key,
                    static pair => new WoundRootOwnershipDomain(
                        pair.Value.Kind,
                        pair.Value.ComplicationId),
                    StringComparer.Ordinal));

    internal WoundEffectLineageWorkStatistics Work { get; }

    internal IReadOnlyList<ValidationIssue> Issues =>
        Array.AsReadOnly(_issues.ToArray());
}

// One original-wound lineage pass per independent generation-planning stage.
// Canonical root membership is stricter than membership in the historical source group.
internal sealed class WoundRootGenerationAuthority
{
    private readonly WoundMaterializationEnvelope _before;
    private readonly EffectCarrierCatalog _catalog;
    private readonly EffectIdentityState _identities;
    private readonly IReadOnlyDictionary<string, (string DefinitionKey, WoundRootOwnershipDomain Domain)> _roots;
    internal WoundRootGenerationAuthority(WoundMaterializationEnvelope before,
        EffectCarrierCatalog catalog, EffectIdentityState identities)
    {
        _before = before;
        _catalog = catalog;
        _identities = identities;
        var lineage = WoundEffectLineagePlanner.Plan(before, identities);
        Issues = catalog.Issues.Concat(lineage.Issues).ToArray();
        var ownership = lineage.OwnershipByEffectId;
        _roots = before.Consequences.OwnedEffectSources.RootBindings
            .Where(root => ownership.ContainsKey(root.EffectId))
            .ToDictionary(root => root.EffectId,
                root => (root.DefinitionKey, ownership[root.EffectId]), StringComparer.Ordinal);
    }

    internal IReadOnlyList<ValidationIssue> Issues { get; }
    internal bool IsTerminalRoot(string effectId) => _roots.ContainsKey(effectId) &&
        _identities.TryGetEntry(effectId, out var identity) &&
        identity.State is "expired" or "dispelled" or "removed" or "replaced";

    internal bool Agrees(string effectId, EffectSourceKey expectedSource,
        EffectTargetKey expectedTarget, EffectCarrierCoordinate expectedCarrier,
        JsonObject definition, WoundRootOwnershipDomain domain)
    {
        if (Issues.Count != 0 || !_roots.TryGetValue(effectId, out var root) ||
            root.Domain != domain || root.DefinitionKey != expectedSource.DefinitionKey ||
            expectedSource != new EffectSourceKey(_before.Owner.Realm, "wound", _before.WoundId, root.DefinitionKey) ||
            !_identities.TryGetEntry(effectId, out var identity))
            return false;
        if (identity.State is "active" or "suspended")
            return WoundAcceptedTurnPlannerCore.PriorRootHasExactGenerationAuthority(
                effectId, expectedSource, expectedTarget, expectedCarrier, definition, _catalog, _identities);
        return IsTerminalRoot(effectId) &&
            !_catalog.Occurrences.Any(row => string.Equals(row.EffectId, effectId, StringComparison.Ordinal)) &&
            WoundEffectLineagePlanner.IdentityAuthorityAgrees(
                _before, identity, definition, expectedTarget, expectedCarrier);
    }
}

internal static class WoundEffectLineagePlanner
{
    private const int MaxIssues = 20;

    internal static WoundEffectLineagePlanningResult Plan(
        WoundMaterializationEnvelope wound,
        EffectIdentityState identities,
        IReadOnlyList<string>? selectedRootEffectIds = null)
    {
        ArgumentNullException.ThrowIfNull(wound);
        ArgumentNullException.ThrowIfNull(identities);

        var issues = new List<ValidationIssue>();
        var sourceGroup = new EffectIdentitySourceGroup(
            wound.Owner.Realm,
            "wound",
            wound.WoundId);
        var definitionsByKey = wound.Consequences.OwnedEffectSources
            .DefinitionFacts
            .ToDictionary(
                static definition => definition.DefinitionKey,
                StringComparer.Ordinal);
        var rootsById = wound.Consequences.OwnedEffectSources.RootBindings
            .ToDictionary(
                static binding => binding.EffectId,
                StringComparer.Ordinal);
        var rootDomains = BuildRootDomains(wound, rootsById, issues);
        var definitionDomains = BuildDefinitionDomains(
            wound,
            rootsById,
            rootDomains,
            definitionsByKey,
            issues);
        var selectedRoots = ResolveSelectedRoots(
            selectedRootEffectIds,
            rootsById,
            issues);
        var analyzerDefinitions = definitionDomains
            .Where(pair => definitionsByKey.ContainsKey(pair.Key))
            .ToDictionary(
                static pair => pair.Key,
                pair => new WoundEffectIdentityLineageDefinition(
                    pair.Key,
                    definitionsByKey[pair.Key].ApplyDefinitionTargets
                        .ToHashSet(StringComparer.Ordinal),
                    pair.Value),
                StringComparer.Ordinal);
        var analyzerRoots = rootsById.Values
            .Where(binding => rootDomains.ContainsKey(binding.EffectId))
            .Select(binding => new WoundEffectIdentityLineageRoot(
                binding.EffectId,
                binding.DefinitionKey,
                rootDomains[binding.EffectId]))
            .ToArray();
        var analysis = WoundEffectIdentityLineageAnalyzer.Analyze(
            sourceGroup,
            identities,
            analyzerRoots,
            analyzerDefinitions,
            WoundEffectLineageDiagnosticProfile.AcceptedMechanics);
        issues.AddRange(analysis.Issues);

        foreach (var entry in analysis.CurrentEntries.Concat(
                     analysis.RetiredEntries))
        {
            var definitionKey = ReadDefinitionKey(entry);
            if (!definitionsByKey.TryGetValue(definitionKey, out var definition) ||
                IdentityAuthorityAgrees(wound, entry, definition))
            {
                continue;
            }
            AddIssue(
                issues,
                "acceptedMechanics.woundEffectLineage." + entry.EffectId,
                "accepted_mechanics_wound_lineage_identity_authority_mismatch",
                "Every current or retired wound-lineage identity must retain the exact wound owner, target, carrier owner, and stack coordinate derived from its definition.",
                wound.Owner.ToString(),
                entry.Owner + "/" + entry.StackCoordinate);
        }

        var origins = analysis.CurrentOriginRootByEffectId;
        var closure = origins
            .Where(pair => selectedRoots.Contains(pair.Value))
            .Select(static pair => pair.Key)
            .OrderBy(static effectId => effectId, StringComparer.Ordinal)
            .ToArray();
        var activeClosure = closure
            .Where(effectId => identities.TryGetEntry(effectId, out var entry) &&
                               IsActiveOrSuspended(entry))
            .ToArray();
        var selectedOwnership = closure.ToDictionary(
            static effectId => effectId,
            effectId => analysis.CurrentOwnershipByEffectId[effectId],
            StringComparer.Ordinal);
        return new WoundEffectLineagePlanningResult(
            closure,
            activeClosure,
            selectedOwnership,
            definitionDomains,
            new WoundEffectLineageWorkStatistics(
                analysis.SourceGroupIdentityCount,
                analysis.VisitedCurrentIdentityCount),
            issues.Take(MaxIssues).ToArray());
    }

    private static Dictionary<string, WoundRootOwnershipDomain>
        BuildDefinitionDomains(
            WoundMaterializationEnvelope wound,
            IReadOnlyDictionary<string, WoundRootEffectBinding> rootsById,
            IReadOnlyDictionary<string, WoundRootOwnershipDomain> rootDomains,
            IReadOnlyDictionary<string, WoundOwnedEffectDefinitionFact>
                definitionsByKey,
            ICollection<ValidationIssue> issues)
    {
        var result = new Dictionary<string, WoundRootOwnershipDomain>(
            StringComparer.Ordinal);
        var ambiguous = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in rootsById.Values.OrderBy(
                     static binding => binding.EffectId,
                     StringComparer.Ordinal))
        {
            if (!rootDomains.TryGetValue(root.EffectId, out var domain))
                continue;
            var queue = new Queue<string>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            queue.Enqueue(root.DefinitionKey);
            while (queue.Count != 0)
            {
                var definitionKey = queue.Dequeue();
                if (!visited.Add(definitionKey))
                    continue;
                if (!definitionsByKey.TryGetValue(definitionKey, out var definition))
                    continue;
                if (result.TryGetValue(definitionKey, out var existingDomain) &&
                    existingDomain != domain)
                {
                    if (ambiguous.Add(definitionKey))
                    {
                        AddIssue(
                            issues,
                            "acceptedMechanics.woundEffectLineage.definitionGraph",
                            "accepted_mechanics_wound_lineage_cross_domain",
                            "Every reachable wound definition must belong to one exact base-wound or complication domain before reaction execution.",
                            DescribeDomain(existingDomain),
                            DescribeDomain(domain) + "/" + definitionKey);
                    }
                }
                else
                {
                    result[definitionKey] = domain;
                }

                foreach (var target in definition.ApplyDefinitionTargets)
                    queue.Enqueue(target);
            }
        }
        foreach (var definitionKey in ambiguous)
            result.Remove(definitionKey);
        return result;
    }

    private static Dictionary<string, WoundRootOwnershipDomain> BuildRootDomains(
        WoundMaterializationEnvelope wound,
        IReadOnlyDictionary<string, WoundRootEffectBinding> rootsById,
        ICollection<ValidationIssue> issues)
    {
        var result = rootsById.Keys.ToDictionary(
            static effectId => effectId,
            static _ => WoundRootOwnershipDomain.BaseWound,
            StringComparer.Ordinal);
        foreach (var complication in wound.Complications)
        {
            foreach (var effectId in complication.OwnedEffectIds)
            {
                if (!result.ContainsKey(effectId))
                {
                    AddIssue(
                        issues,
                        "acceptedMechanics.woundEffectLineage.complications",
                        "accepted_mechanics_wound_lineage_complication_root_missing",
                        "Every complication-owned effect must be one current root binding.",
                        "current root effectId",
                        effectId);
                    continue;
                }
                var domain = WoundRootOwnershipDomain.ForComplication(
                    complication.ComplicationId);
                if (result[effectId] != WoundRootOwnershipDomain.BaseWound)
                {
                    AddIssue(
                        issues,
                        "acceptedMechanics.woundEffectLineage.complications",
                        "accepted_mechanics_wound_lineage_cross_domain",
                        "A wound root may belong to only one complication ownership domain.",
                        DescribeDomain(result[effectId]),
                        DescribeDomain(domain));
                    continue;
                }
                result[effectId] = domain;
            }
        }
        return result;
    }

    private static HashSet<string> ResolveSelectedRoots(
        IReadOnlyList<string>? selectedRootEffectIds,
        IReadOnlyDictionary<string, WoundRootEffectBinding> rootsById,
        ICollection<ValidationIssue> issues)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var effectId in selectedRootEffectIds ?? rootsById.Keys.ToArray())
        {
            if (!ResourceMaterializationContract.IsExactIdentifier(effectId) ||
                !result.Add(effectId))
            {
                AddIssue(
                    issues,
                    "acceptedMechanics.woundEffectLineage.selectedRoots",
                    "accepted_mechanics_wound_lineage_selection_invalid",
                    "Unique exact current wound root effectIds.",
                    "unique current root",
                    effectId ?? "null");
                continue;
            }
            if (!rootsById.ContainsKey(effectId))
            {
                AddIssue(
                    issues,
                    "acceptedMechanics.woundEffectLineage.selectedRoots",
                    "accepted_mechanics_wound_lineage_selection_invalid",
                    "Only exact current wound root bindings may select a lineage closure.",
                    string.Join(",", rootsById.Keys.OrderBy(static value => value, StringComparer.Ordinal)),
                    effectId);
            }
        }
        return result;
    }

    private static void ValidateActiveMembership(
        IReadOnlyList<EffectIdentityEntry> sourceMembers,
        IReadOnlyDictionary<string, WoundOwnedEffectDefinitionFact> definitionsByKey,
        IReadOnlyDictionary<string, WoundRootEffectBinding> rootsById,
        ICollection<ValidationIssue> issues)
    {
        var active = sourceMembers.Where(IsActiveOrSuspended).ToArray();
        if (active.Length > WoundMaterializationContract.MaxOwnedEffectDefinitions)
        {
            AddIssue(
                issues,
                "acceptedMechanics.woundEffectLineage",
                "accepted_mechanics_wound_lineage_active_bound_exceeded",
                "At most one simultaneous source member per bounded wound definition.",
                WoundMaterializationContract.MaxOwnedEffectDefinitions.ToString(),
                active.Length.ToString());
        }
        foreach (var entry in active)
        {
            var definitionKey = ReadDefinitionKey(entry);
            if (!definitionsByKey.ContainsKey(definitionKey))
            {
                AddIssue(
                    issues,
                    "acceptedMechanics.woundEffectLineage",
                    "accepted_mechanics_wound_lineage_definition_missing",
                    "Every active or suspended wound-source identity must resolve in the current persisted graph.",
                    "current definitionKey",
                    definitionKey);
            }
        }
        foreach (var duplicate in active.GroupBy(
                     ReadDefinitionKey,
                     StringComparer.Ordinal)
                 .Where(static group => group.Count() > 1))
        {
            AddIssue(
                issues,
                "acceptedMechanics.woundEffectLineage",
                "accepted_mechanics_wound_lineage_active_duplicate",
                "At most one active or suspended identity may occupy each wound definition coordinate.",
                "one identity",
                string.Join(",", duplicate.Select(static entry => entry.EffectId)));
        }

        foreach (var root in rootsById.Values)
        {
            if (!definitionsByKey.ContainsKey(root.DefinitionKey))
            {
                AddIssue(
                    issues,
                    "acceptedMechanics.woundEffectLineage.rootBindings",
                    "accepted_mechanics_wound_lineage_definition_missing",
                    "Every current wound root binding must resolve in the persisted source graph.",
                    "current definitionKey",
                    root.DefinitionKey);
            }
        }
    }

    private static void ValidateCurrentDefinitionAcyclicity(
        IReadOnlyList<EffectIdentityEntry> sourceMembers,
        IReadOnlyDictionary<string, WoundOwnedEffectDefinitionFact> definitionsByKey,
        ICollection<ValidationIssue> issues)
    {
        var currentMembers = sourceMembers
            .Where(entry => definitionsByKey.ContainsKey(ReadDefinitionKey(entry)))
            .ToDictionary(
                static entry => entry.EffectId,
                StringComparer.Ordinal);
        var indegree = currentMembers.Keys.ToDictionary(
            static effectId => effectId,
            static _ => 0,
            StringComparer.Ordinal);
        var children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var entry in currentMembers.Values)
        {
            var creates = entry.Transitions.Where(static transition =>
                    string.Equals(transition.Kind, "create", StringComparison.Ordinal))
                .ToArray();
            if (creates.Length != 1 ||
                !string.Equals(
                    entry.Transitions[0].TransitionId,
                    creates[0].TransitionId,
                    StringComparison.Ordinal) ||
                creates[0].ResultEffectIds.Count != 1 ||
                !string.Equals(
                    creates[0].ResultEffectIds[0],
                    entry.EffectId,
                    StringComparison.Ordinal) ||
                creates[0].SourceEffectIds.Count > 1 ||
                creates[0].ReceiptId is not null)
            {
                AddIssue(
                    issues,
                    "acceptedMechanics.woundEffectLineage." + entry.EffectId,
                    "accepted_mechanics_wound_lineage_create_invalid",
                    "Every identity whose definition remains current must retain exactly one first create transition with self result and no receipt.",
                    "one first create transition",
                    creates.Length + " create transition(s)");
                continue;
            }
            foreach (var parent in creates[0].SourceEffectIds)
            {
                if (!currentMembers.ContainsKey(parent))
                    continue;
                if (!children.TryGetValue(parent, out var values))
                {
                    values = new List<string>();
                    children.Add(parent, values);
                }
                values.Add(entry.EffectId);
                indegree[entry.EffectId]++;
            }
        }

        var ready = new Queue<string>(indegree
            .Where(static pair => pair.Value == 0)
            .Select(static pair => pair.Key)
            .OrderBy(static effectId => effectId, StringComparer.Ordinal));
        var processed = 0;
        while (ready.Count != 0)
        {
            var effectId = ready.Dequeue();
            processed++;
            if (!children.TryGetValue(effectId, out var descendants))
                continue;
            foreach (var child in descendants)
            {
                indegree[child]--;
                if (indegree[child] == 0)
                    ready.Enqueue(child);
            }
        }
        if (processed == currentMembers.Count)
            return;

        AddIssue(
            issues,
            "acceptedMechanics.woundEffectLineage",
            "accepted_mechanics_wound_lineage_cycle",
            "First-create causal ownership within the current wound definition graph must be acyclic.",
            "acyclic first-create lineage",
            string.Join(",", indegree
                .Where(static pair => pair.Value > 0)
                .Select(static pair => pair.Key)
                .OrderBy(static effectId => effectId, StringComparer.Ordinal)));
    }

    private static bool ValidateCreateEvidence(
        EffectIdentityEntry entry,
        IReadOnlyList<string> expectedParents,
        ICollection<ValidationIssue> issues)
    {
        var creates = entry.Transitions.Where(static transition =>
                string.Equals(transition.Kind, "create", StringComparison.Ordinal))
            .ToArray();
        var valid = creates.Length == 1 &&
                    ReferenceEquals(creates[0], entry.Transitions[0]) &&
                    creates[0].SourceEffectIds.SequenceEqual(
                        expectedParents,
                        StringComparer.Ordinal) &&
                    creates[0].ResultEffectIds.Count == 1 &&
                    string.Equals(
                        creates[0].ResultEffectIds[0],
                        entry.EffectId,
                        StringComparison.Ordinal) &&
                    creates[0].ReceiptId is null;
        if (valid)
            return true;

        AddIssue(
            issues,
            "acceptedMechanics.woundEffectLineage." + entry.EffectId,
            "accepted_mechanics_wound_lineage_create_invalid",
            "Ownership lineage requires one first create transition with exact causal parents, self result, and no receipt.",
            expectedParents.Count == 0
                ? "direct root with no causal parent"
                : "exact causal parent " + expectedParents[0],
            creates.Length == 0
                ? "missing create"
                : creates.Length + " create transition(s); parents=" +
                  string.Join(",", creates[0].SourceEffectIds));
        return false;
    }

    private static bool IsExactSourceGroup(
        EffectIdentityEntry entry,
        EffectIdentitySourceGroup group) =>
        string.Equals(entry.Realm, group.Realm, StringComparison.Ordinal) &&
        string.Equals(
            entry.Source["kind"]!.GetValue<string>(),
            group.Kind,
            StringComparison.Ordinal) &&
        string.Equals(
            entry.Source["sourceId"]!.GetValue<string>(),
            group.SourceId,
            StringComparison.Ordinal);

    private static bool IsActiveOrSuspended(EffectIdentityEntry entry) =>
        entry.State is "active" or "suspended";

    private static string ReadDefinitionKey(EffectIdentityEntry entry) =>
        entry.Source["definitionKey"]!.GetValue<string>();

    private static bool IdentityAuthorityAgrees(
        WoundMaterializationEnvelope wound,
        EffectIdentityEntry identity,
        WoundOwnedEffectDefinitionFact definitionFact) =>
        JsonNode.Parse(definitionFact.CanonicalJson) is JsonObject definition &&
        IdentityAuthorityAgrees(wound, identity, definition);

    internal static bool IdentityAuthorityAgrees(
        WoundMaterializationEnvelope wound,
        EffectIdentityEntry identity,
        JsonObject definition,
        EffectTargetKey? suppliedTarget = null,
        EffectCarrierCoordinate? suppliedCoordinate = null)
    {
        if (!WoundEffectCarrierAdapter.TryCreateTargetKey(
                wound.Owner,
                out var expectedTarget) ||
            !WoundEffectCarrierAdapter.TryCreateCarrierCoordinate(
                wound.Owner,
                expectedTarget,
                definition,
                out var expectedCoordinate) ||
            suppliedTarget is not null && suppliedTarget != expectedTarget ||
            suppliedCoordinate is not null && suppliedCoordinate != expectedCoordinate ||
            !WoundEffectTerminalOperationPlanner.TryCreateExpectedIdentityOwner(
                expectedTarget,
                expectedCoordinate,
                out var expectedOwner) ||
            definition["stacking"] is not JsonObject stacking ||
            stacking["stackKey"] is not JsonValue stackKeyNode ||
            !stackKeyNode.TryGetValue<string>(out var stackKey) ||
            !ResourceMaterializationContract.IsExactIdentifier(stackKey))
        {
            return false;
        }

        var expectedStack = new EffectStackCoordinate(
            expectedTarget.Realm,
            expectedTarget.Kind,
            expectedTarget.TargetId,
            "wound",
            wound.WoundId,
            stackKey);
        return identity.Owner == expectedOwner &&
               identity.StackCoordinate == expectedStack &&
               string.Equals(
                   identity.Realm,
                   expectedTarget.Realm,
                   StringComparison.Ordinal) &&
               identity.Target["kind"] is JsonValue kindNode &&
               kindNode.TryGetValue<string>(out var targetKind) &&
               string.Equals(
                   targetKind,
                   expectedTarget.Kind,
                   StringComparison.Ordinal) &&
               identity.Target["targetId"] is JsonValue targetIdNode &&
               targetIdNode.TryGetValue<string>(out var targetId) &&
               string.Equals(
                   targetId,
                   expectedTarget.TargetId,
                   StringComparison.Ordinal);
    }

    private static string DescribeSourceGroup(EffectIdentitySourceGroup group) =>
        group.Realm + "/" + group.Kind + "/" + group.SourceId;

    private static string DescribeSourceGroup(EffectIdentityEntry entry) =>
        entry.Realm + "/" + entry.Source["kind"]!.GetValue<string>() + "/" +
        entry.Source["sourceId"]!.GetValue<string>();

    private static string DescribeDomain(WoundRootOwnershipDomain domain) =>
        domain.Kind + "/" + (domain.ComplicationId ?? "-");

    private static void AddIssue(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string message,
        string expected,
        string actual)
    {
        if (issues.Count >= MaxIssues)
            return;
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            actor: "Client",
            section: "wound_materialization",
            expected: expected,
            actual: actual,
            repairHint:
                "Restore the exact client-owned first-create lineage for this wound source group before retrying the accepted mutation."));
    }

    private sealed record LineageVisit(
        EffectIdentityEntry Entry,
        string OriginRootEffectId,
        WoundRootOwnershipDomain Domain);
}

internal sealed class WoundEffectTerminalOperationPlanningResult
{
    private readonly WoundTerminalEffectOperation[] _operations;
    private readonly ValidationIssue[] _issues;

    internal WoundEffectTerminalOperationPlanningResult(
        IReadOnlyList<WoundTerminalEffectOperation> operations,
        WoundEffectLineageWorkStatistics lineageWork,
        IReadOnlyList<ValidationIssue> issues)
    {
        _operations = (operations ??
            throw new ArgumentNullException(nameof(operations)))
            .Select(WoundAcceptedTurnData.CloneTerminalOperation)
            .ToArray();
        LineageWork = lineageWork ??
            throw new ArgumentNullException(nameof(lineageWork));
        _issues = (issues ?? throw new ArgumentNullException(nameof(issues)))
            .ToArray();
    }

    internal bool Success => _issues.Length == 0;

    internal IReadOnlyList<WoundTerminalEffectOperation> Operations =>
        Array.AsReadOnly(_operations
            .Select(WoundAcceptedTurnData.CloneTerminalOperation)
            .ToArray());

    internal WoundEffectLineageWorkStatistics LineageWork { get; }

    internal IReadOnlyList<ValidationIssue> Issues =>
        Array.AsReadOnly(_issues.ToArray());
}

internal static class WoundEffectTerminalOperationPlanner
{
    private const int MaxIssues = 20;
    private const string FingerprintVersion = "1";

    internal static WoundEffectTerminalOperationPlanningResult Plan(
        WoundMaterializationEnvelope wound,
        EffectCarrierCatalogInput carriers,
        EffectIdentityState identities,
        IReadOnlyList<string> selectedRootEffectIds,
        string causalEventRef,
        int mechanicsOrdinal,
        int operationOrdinalOffset,
        string operationKey)
    {
        ArgumentNullException.ThrowIfNull(wound);
        ArgumentNullException.ThrowIfNull(carriers);
        ArgumentNullException.ThrowIfNull(identities);
        ArgumentNullException.ThrowIfNull(selectedRootEffectIds);

        var lineage = WoundEffectLineagePlanner.Plan(
            wound,
            identities,
            selectedRootEffectIds);
        var issues = lineage.Issues.ToList();
        if (!ResourceMaterializationContract.IsExactIdentifier(causalEventRef) ||
            mechanicsOrdinal <= 0 ||
            operationOrdinalOffset < 0 ||
            !ResourceMaterializationContract.IsExactIdentifier(operationKey))
        {
            AddIssue(
                issues,
                "acceptedMechanics.woundTerminalOperations",
                "accepted_mechanics_wound_terminal_request_invalid",
                "A wound terminal request must carry one exact causal event, positive mechanics ordinal, non-negative operation offset, and exact operation key.",
                "closed terminal request authority",
                $"{causalEventRef}/{mechanicsOrdinal}/{operationOrdinalOffset}/{operationKey}");
        }

        var catalog = EffectCarrierCatalog.Build(carriers);
        if (catalog.Issues.Count != 0)
        {
            AddIssue(
                issues,
                "acceptedMechanics.woundTerminalOperations.carriers",
                "accepted_mechanics_wound_terminal_carrier_catalog_invalid",
                "Terminal planning requires one canonical and globally unique pre-mutation effect carrier catalog.",
                "valid canonical carrier catalog",
                string.Join(",", catalog.Issues.Select(static issue =>
                    issue.Code)));
        }

        if (!WoundEffectCarrierAdapter.TryCreateTargetKey(
                wound.Owner,
                out var expectedTarget))
        {
            AddIssue(
                issues,
                "acceptedMechanics.woundTerminalOperations.owner",
                "accepted_mechanics_wound_terminal_owner_invalid",
                "A wound terminal request must resolve its wound owner to one exact effect target.",
                "closed wound-owner target",
                wound.Owner.ToString());
        }

        if (issues.Count != 0)
        {
            return Failed(lineage.Work, issues);
        }

        var definitions = wound.Consequences.OwnedEffectSources.DefinitionFacts
            .ToDictionary(
                static definition => definition.DefinitionKey,
                StringComparer.Ordinal);
        var sourceGroup = new EffectIdentitySourceGroup(
            wound.Owner.Realm,
            "wound",
            wound.WoundId);
        foreach (var identity in identities.ResolveSourceGroup(sourceGroup)
                     .Where(static entry => entry.State is "active" or "suspended"))
        {
            if (ActiveOccurrenceAndIdentityAgree(
                    wound,
                    expectedTarget,
                    definitions,
                    catalog,
                    identity))
            {
                continue;
            }

            AddOccurrenceMismatch(
                issues,
                identity.EffectId,
                "unresolved full-lineage occurrence");
        }
        if (issues.Count != 0)
            return Failed(lineage.Work, issues);

        var ownershipByEffect = lineage.OwnershipByEffectId;
        var operations = new List<WoundTerminalEffectOperation>();
        var operationIndex = 0;
        foreach (var effectId in lineage.ActiveOrSuspendedEffectIds)
        {
            operationIndex++;
            if (!identities.TryGetEntry(effectId, out var identity) ||
                !catalog.TryResolveOne(effectId, out var occurrence) ||
                !TryReadString(identity.Source, "definitionKey", out var definitionKey) ||
                !definitions.TryGetValue(definitionKey, out var definitionFact) ||
                JsonNode.Parse(definitionFact.CanonicalJson) is not JsonObject definition ||
                !WoundEffectCarrierAdapter.TryCreateCarrierCoordinate(
                    wound.Owner,
                    expectedTarget,
                    definition,
                    out var expectedCoordinate) ||
                !TryCreateExpectedIdentityOwner(
                    expectedTarget,
                    expectedCoordinate,
                    out var expectedOwner) ||
                !TryReadEffectStackKey(occurrence.Effect, out var stackKey))
            {
                AddOccurrenceMismatch(issues, effectId, "unresolved sealed occurrence");
                continue;
            }

            var expectedSource = new EffectSourceKey(
                wound.Owner.Realm,
                "wound",
                wound.WoundId,
                definitionKey);
            var expectedStack = new EffectStackCoordinate(
                expectedTarget.Realm,
                expectedTarget.Kind,
                expectedTarget.TargetId,
                expectedSource.Kind,
                expectedSource.SourceId,
                stackKey);
            if (!OccurrenceAndIdentityAgree(
                    occurrence,
                    identity,
                    expectedSource,
                    expectedTarget,
                    expectedCoordinate,
                    expectedOwner,
                    expectedStack))
            {
                AddOccurrenceMismatch(
                    issues,
                    effectId,
                    occurrence.JsonPath);
                continue;
            }

            if (!ownershipByEffect.TryGetValue(effectId, out var ownership))
            {
                AddOccurrenceMismatch(issues, effectId, "missing lineage domain");
                continue;
            }

            int operationOrdinal;
            try
            {
                operationOrdinal = checked(operationOrdinalOffset + operationIndex);
            }
            catch (OverflowException)
            {
                AddIssue(
                    issues,
                    "acceptedMechanics.woundTerminalOperations",
                    "accepted_mechanics_wound_terminal_request_invalid",
                    "The derived operation ordinal must remain a positive Int32 value.",
                    "positive Int32 operation ordinal",
                    "overflow");
                continue;
            }

            var effectFingerprint = ComputeEffectFingerprint(occurrence);
            var identityFingerprint = ComputeIdentityFingerprint(identity);
            var operationRef = CreateOperationRef(
                causalEventRef,
                mechanicsOrdinal,
                operationOrdinal,
                "expire");
            operations.Add(new WoundTerminalEffectOperation(
                operationRef,
                operationKey,
                effectId,
                mechanicsOrdinal,
                operationOrdinal,
                "expire",
                causalEventRef,
                expectedSource,
                expectedTarget,
                expectedCoordinate,
                occurrence.FilePath,
                occurrence.JsonPath,
                expectedOwner,
                expectedStack,
                effectFingerprint,
                identityFingerprint,
                ownership));
        }

        return issues.Count == 0
            ? new WoundEffectTerminalOperationPlanningResult(
                operations,
                lineage.Work,
                issues)
            : Failed(lineage.Work, issues);
    }

    internal static string ComputeEffectFingerprint(
        EffectCarrierOccurrence occurrence) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.wound.terminal_effect_occurrence",
            FingerprintVersion,
            occurrence.EffectId,
            occurrence.FilePath,
            occurrence.JsonPath,
            occurrence.Coordinate.Kind,
            occurrence.Coordinate.OwnerId,
            occurrence.Coordinate.Path,
            occurrence.Coordinate.Category,
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(occurrence.Effect)
        });

    internal static string ComputeIdentityFingerprint(
        EffectIdentityEntry identity) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.wound.terminal_effect_identity",
            FingerprintVersion,
            identity.EffectId,
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(identity.Raw)
        });

    internal static bool OccurrenceAndIdentityAgree(
        EffectCarrierOccurrence occurrence,
        EffectIdentityEntry identity,
        EffectSourceKey expectedSource,
        EffectTargetKey expectedTarget,
        EffectCarrierCoordinate expectedCoordinate,
        EffectIdentityOwner expectedOwner,
        EffectStackCoordinate expectedStack)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        ArgumentNullException.ThrowIfNull(identity);
        return string.Equals(
                   occurrence.EffectId,
                   identity.EffectId,
                   StringComparison.Ordinal) &&
               occurrence.Coordinate == expectedCoordinate &&
               string.Equals(
                   occurrence.FilePath,
                   expectedCoordinate.Path,
                   StringComparison.Ordinal) &&
               identity.Owner == expectedOwner &&
               identity.StackCoordinate == expectedStack &&
               string.Equals(identity.Realm, expectedTarget.Realm,
                   StringComparison.Ordinal) &&
               identity.State is "active" or "suspended" &&
               EffectHasExactAuthority(
                   occurrence.Effect,
                   identity,
                   expectedSource,
                   expectedTarget,
                   expectedStack.StackKey);
    }

    private static bool ActiveOccurrenceAndIdentityAgree(
        WoundMaterializationEnvelope wound,
        EffectTargetKey expectedTarget,
        IReadOnlyDictionary<string, WoundOwnedEffectDefinitionFact> definitions,
        EffectCarrierCatalog catalog,
        EffectIdentityEntry identity)
    {
        if (!catalog.TryResolveOne(identity.EffectId, out var occurrence) ||
            !TryReadString(
                identity.Source,
                "definitionKey",
                out var definitionKey) ||
            !definitions.TryGetValue(definitionKey, out var definitionFact) ||
            JsonNode.Parse(definitionFact.CanonicalJson) is not
                JsonObject definition ||
            !WoundEffectCarrierAdapter.TryCreateCarrierCoordinate(
                wound.Owner,
                expectedTarget,
                definition,
                out var expectedCoordinate) ||
            !TryCreateExpectedIdentityOwner(
                expectedTarget,
                expectedCoordinate,
                out var expectedOwner) ||
            !TryReadEffectStackKey(occurrence.Effect, out var stackKey))
        {
            return false;
        }

        var expectedSource = new EffectSourceKey(
            wound.Owner.Realm,
            "wound",
            wound.WoundId,
            definitionKey);
        var expectedStack = new EffectStackCoordinate(
            expectedTarget.Realm,
            expectedTarget.Kind,
            expectedTarget.TargetId,
            expectedSource.Kind,
            expectedSource.SourceId,
            stackKey);
        return OccurrenceAndIdentityAgree(
            occurrence,
            identity,
            expectedSource,
            expectedTarget,
            expectedCoordinate,
            expectedOwner,
            expectedStack);
    }

    private static bool EffectHasExactAuthority(
        JsonObject effect,
        EffectIdentityEntry identity,
        EffectSourceKey expectedSource,
        EffectTargetKey expectedTarget,
        string expectedStackKey)
    {
        var firstTransition = identity.Transitions.FirstOrDefault();
        var lastTransition = identity.Transitions.LastOrDefault();
        return
        TryReadString(effect, "effectId", out var effectId) &&
        string.Equals(effectId, identity.EffectId, StringComparison.Ordinal) &&
        TryReadString(effect, "state", out var state) &&
        string.Equals(state, identity.State, StringComparison.Ordinal) &&
        TryReadString(effect, "realm", out var realm) &&
        string.Equals(realm, expectedTarget.Realm, StringComparison.Ordinal) &&
        effect["target"] is JsonObject effectTarget &&
        HasExactTarget(effectTarget, expectedTarget) &&
        HasExactTarget(identity.Target, expectedTarget) &&
        effect["source"] is JsonObject effectSource &&
        HasExactSource(effectSource, expectedSource) &&
        HasExactSource(identity.Source, expectedSource) &&
        TryReadEffectStackKey(effect, out var stackKey) &&
        string.Equals(stackKey, expectedStackKey, StringComparison.Ordinal) &&
        effect["chronology"] is JsonObject chronology &&
        TryReadInt(chronology, "createdAtTurn", out var createdAtTurn) &&
        identity.CreatedAtTurn == createdAtTurn &&
        firstTransition is not null &&
        TryReadString(chronology, "createdEventRef", out var createdEventRef) &&
        string.Equals(
            firstTransition.EventRef,
            createdEventRef,
            StringComparison.Ordinal) &&
        lastTransition is not null &&
        TryReadString(
            chronology,
            "lastTransitionId",
            out var lastTransitionId) &&
        string.Equals(
            lastTransition.TransitionId,
            lastTransitionId,
            StringComparison.Ordinal) &&
        TryReadInt(
            chronology,
            "lastTransitionTurn",
            out var lastTransitionTurn) &&
        lastTransition.Turn == lastTransitionTurn;
    }

    private static bool HasExactTarget(
        JsonObject target,
        EffectTargetKey expected) =>
        target.Count == 2 &&
        TryReadString(target, "kind", out var kind) &&
        TryReadString(target, "targetId", out var targetId) &&
        string.Equals(kind, expected.Kind, StringComparison.Ordinal) &&
        string.Equals(targetId, expected.TargetId, StringComparison.Ordinal);

    private static bool HasExactSource(
        JsonObject source,
        EffectSourceKey expected) =>
        source.Count == 3 &&
        TryReadString(source, "kind", out var kind) &&
        TryReadString(source, "sourceId", out var sourceId) &&
        TryReadString(source, "definitionKey", out var definitionKey) &&
        string.Equals(kind, expected.Kind, StringComparison.Ordinal) &&
        string.Equals(sourceId, expected.SourceId, StringComparison.Ordinal) &&
        string.Equals(
            definitionKey,
            expected.DefinitionKey,
            StringComparison.Ordinal);

    private static bool TryReadEffectStackKey(
        JsonObject effect,
        [NotNullWhen(true)] out string? stackKey)
    {
        stackKey = null;
        return effect["stacking"] is JsonObject stacking &&
               TryReadString(stacking, "stackKey", out stackKey);
    }

    private static bool TryReadString(
        JsonObject value,
        string property,
        [NotNullWhen(true)] out string? result)
    {
        result = value[property] is JsonValue node &&
                 node.TryGetValue<string>(out var candidate) &&
                 ResourceMaterializationContract.IsExactIdentifier(candidate)
            ? candidate
            : null;
        return result is not null;
    }

    private static bool TryReadInt(
        JsonObject value,
        string property,
        out int result)
    {
        result = 0;
        return value[property] is JsonValue node &&
               node.TryGetValue<int>(out result);
    }

    internal static bool TryCreateExpectedIdentityOwner(
        EffectTargetKey target,
        EffectCarrierCoordinate coordinate,
        [NotNullWhen(true)] out EffectIdentityOwner? owner)
    {
        var collection = coordinate.Kind switch
        {
            "player" or "npc" or "afterlife_profile" => "activeEffects",
            "combatant" when coordinate.Category == "buff" => "activeBuffs",
            "combatant" when coordinate.Category == "debuff" => "activeDebuffs",
            _ => string.Empty
        };
        owner = collection.Length == 0
            ? null
            : new EffectIdentityOwner(
                target.Kind,
                target.TargetId,
                coordinate.Path,
                collection);
        return owner is not null;
    }

    private static string CreateOperationRef(
        string causalEventRef,
        int mechanicsOrdinal,
        int operationOrdinal,
        string operationKind) =>
        WoundEffectOperationEventRef.Create(
            causalEventRef,
            mechanicsOrdinal,
            operationOrdinal,
            operationKind);

    private static WoundEffectTerminalOperationPlanningResult Failed(
        WoundEffectLineageWorkStatistics work,
        IReadOnlyList<ValidationIssue> issues) =>
        new(
            Array.Empty<WoundTerminalEffectOperation>(),
            work,
            issues);

    private static void AddOccurrenceMismatch(
        ICollection<ValidationIssue> issues,
        string effectId,
        string actual) =>
        AddIssue(
            issues,
            "acceptedMechanics.woundTerminalOperations." + effectId,
            "accepted_mechanics_wound_terminal_occurrence_mismatch",
            "A terminal wound operation must bind one exact pre-mutation carrier occurrence to its exact identity row, wound owner, target, source, stack, and lineage domain.",
            "exact sealed carrier and identity agreement",
            actual);

    private static void AddIssue(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string message,
        string expected,
        string actual)
    {
        if (issues.Count >= MaxIssues)
            return;
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            actor: "Client",
            section: "wound_materialization",
            expected: expected,
            actual: actual,
            repairHint:
                "Restore the exact sealed pre-mutation wound effect carrier and identity lineage before retrying the accepted wound mutation."));
    }
}

internal static class AcceptedMechanicsPlanner
{
    internal sealed class PendingPublicationProof
    {
        internal PendingPublicationProof()
        {
        }
    }

    private static readonly PendingPublicationProof PendingProof =
        new();

    internal static bool IsPendingPublicationProof(
        PendingPublicationProof? proof) => ReferenceEquals(PendingProof, proof);

    /// <summary>
    /// Projects only explicitly registered Mortal wound producers against the exact
    /// finalized resource/effect result. Publication of the returned batches belongs to
    /// the later common-plan occurrence stage.
    /// </summary>
    internal static MortalWoundOccurrenceProducerPlanningResult
        ReduceMortalWoundOccurrenceProducers(
            AcceptedMechanicsResourcePlanningResult resourceResult,
            IReadOnlyList<IMortalWoundOccurrenceProducerDraft> registeredProducers)
    {
        ArgumentNullException.ThrowIfNull(resourceResult);
        ArgumentNullException.ThrowIfNull(registeredProducers);
        var results = new List<MortalWoundOccurrenceCandidateReductionResult>();
        var batches = new List<MortalWoundOccurrenceCandidateBatch>();
        var issues = new List<ValidationIssue>();
        foreach (var producer in registeredProducers)
        {
            if (producer is null)
            {
                issues.Add(new ValidationIssue(
                    "mortalWoundProducers",
                    IssueSeverity.Error,
                    "A registered Mortal wound producer is missing.",
                    code: "mortal_wound_producer_registration_invalid",
                    actor: "Client",
                    section: "mortal_wound_occurrence_producer",
                    expected: "one non-null registered typed producer",
                    actual: "null",
                    repairHint:
                    "Repair the client-owned producer registration before retrying the accepted result."));
                continue;
            }

            var reduced = MortalWoundOccurrenceCandidateReducer.ReduceRegistered(
                producer,
                resourceResult);
            results.Add(reduced);
            issues.AddRange(reduced.Issues);
            if (reduced.Success && reduced.Batch is not null)
                batches.Add(reduced.Batch);
        }

        return issues.Count == 0
            ? new MortalWoundOccurrenceProducerPlanningResult(
                results,
                batches,
                Array.Empty<ValidationIssue>())
            : new MortalWoundOccurrenceProducerPlanningResult(
                results,
                Array.Empty<MortalWoundOccurrenceCandidateBatch>(),
                issues);
    }

    private sealed record PendingBoundaryDecision(
        bool AwaitingReceipt,
        ResourcePendingResolutionState? StateAfterImage,
        JsonObject? SafeGmPacket,
        IReadOnlyList<ValidationIssue> Issues,
        IReadOnlyList<ResourcePendingResolvedBinding>? ResolvedBindings = null,
        bool IsTerminalSemanticReplay = false)
    {
        internal bool IsValid => Issues.Count == 0;
    }

    private sealed class ResolvedPendingReplaySession
    {
        private readonly Dictionary<string, ResourcePendingResolvedBinding>
            _bindingsByStaticAuthority = new(StringComparer.Ordinal);
        private readonly HashSet<string> _staticallyBoundRequestIds = new(
            StringComparer.Ordinal);
        private readonly List<ValidationIssue> _issues = new();

        internal ResolvedPendingReplaySession(
            IReadOnlyList<ResourcePendingResolvedBinding> bindings)
        {
            ArgumentNullException.ThrowIfNull(bindings);
            var maximumWave = -1;
            foreach (var binding in bindings)
            {
                ArgumentNullException.ThrowIfNull(binding);
                var clone = binding.DeepClone();
                var key = PendingReplayStaticKey(
                    clone.RequestAuthority.CausalAuthority,
                    clone.RequestAuthority.EffectAuthority);
                if (!_bindingsByStaticAuthority.TryAdd(key, clone))
                {
                    _issues.AddRange(Issue(
                        "resource_pending_causal_replay_ambiguous",
                        "one exact terminal binding per causal candidate output",
                        clone.RequestId));
                }
                maximumWave = Math.Max(
                    maximumWave,
                    clone.RequestAuthority.CausalAuthority.WaveOrdinal);
            }
            if (maximumWave == int.MaxValue)
            {
                _issues.AddRange(Issue(
                    "resource_pending_causal_wave_exhausted",
                    "pending wave ordinal smaller than Int32.MaxValue",
                    maximumWave.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)));
                NextWaveOrdinal = int.MaxValue;
            }
            else
            {
                NextWaveOrdinal = maximumWave + 1;
            }
        }

        internal int NextWaveOrdinal { get; }

        internal EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution Bind(
            EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution resolution)
        {
            ArgumentNullException.ThrowIfNull(resolution);
            var fingerprintMetrics = new PlannerWorkMetrics();
            EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution WithWork(
                EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution value) =>
                value with
                {
                    Work = value.Work with
                    {
                        PendingCandidateFingerprintOutputVisitCount =
                            value.Work
                                .PendingCandidateFingerprintOutputVisitCount +
                            fingerprintMetrics
                                .PendingCandidateFingerprintOutputVisitCount,
                        PendingProjectionDependencyVisitCount =
                            value.Work
                                .PendingProjectionDependencyVisitCount +
                            fingerprintMetrics
                                .PendingProjectionDependencyVisitCount
                    }
                };
            if (!resolution.IsValid || _issues.Count != 0)
            {
                return WithWork(resolution with
                {
                    Issues = resolution.Issues.Concat(_issues).ToArray()
                });
            }

            var mutations = resolution.Mutations.ToList();
            var originMutationsByKey = new Dictionary<
                ResourceOperationKey,
                ResourceMutationIntent>();
            foreach (var mutation in resolution.Mutations)
            {
                if (!originMutationsByKey.TryAdd(mutation.Key, mutation))
                {
                    _issues.AddRange(Issue(
                        "resource_planner_duplicate_operation",
                        "one mutation per exact replay key",
                        Describe(mutation.Key)));
                }
            }
            var mutationKeys = originMutationsByKey.Keys.ToHashSet();
            var sources = new Dictionary<
                (string SourceKind, string SourceId),
                ResourceMutationSourceExport>();
            foreach (var source in resolution.SourceExports)
            {
                var sourceKey = (source.SourceKind, source.SourceId);
                if (!sources.TryAdd(sourceKey, source))
                {
                    _issues.AddRange(Issue(
                        "resource_source_duplicate_exact",
                        "one exact source export per source kind and identity",
                        source.SourceKind + "/" + source.SourceId));
                }
            }
            if (_issues.Count != 0)
            {
                return WithWork(resolution with
                {
                    Issues = resolution.Issues.Concat(_issues).ToArray()
                });
            }
            var componentIdsByMutation = resolution.ComponentIdsByMutation
                .ToDictionary(static pair => pair.Key, static pair => pair.Value);
            var candidates = new List<
                EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>();
            foreach (var candidate in resolution.TriggerCandidates)
            {
                var pendingOutputs = candidate.PendingOutputs;
                var reactionOutputs = candidate.ReactionOutputs;
                var material = ResolveCandidateMaterialFingerprint(
                    candidate,
                    originMutationsByKey,
                    sources,
                    fingerprintMetrics);
                if (!material.IsValid ||
                    candidate.CausalMaterialFingerprint != null &&
                    !string.Equals(
                        candidate.CausalMaterialFingerprint,
                        material.Fingerprint,
                        StringComparison.Ordinal))
                {
                    _issues.AddRange(material.Issues);
                    if (material.IsValid)
                    {
                        _issues.AddRange(Issue(
                            "resource_pending_candidate_origin_mismatch",
                            "the cached causal material fingerprint",
                            DescribeActivation(candidate.Activation.Identity)));
                    }
                    return WithWork(resolution with
                    {
                        Issues = resolution.Issues.Concat(_issues).ToArray()
                    });
                }
                var fingerprint = CreateCandidateFingerprint(
                    candidate,
                    material.Fingerprint!,
                    pendingOutputs,
                    reactionOutputs,
                    fingerprintMetrics);
                if (candidate.CandidateFingerprint != null &&
                    !string.Equals(
                        candidate.CandidateFingerprint,
                        fingerprint,
                        StringComparison.Ordinal))
                {
                    _issues.AddRange(Issue(
                        "resource_pending_candidate_origin_mismatch",
                        "the cached complete candidate fingerprint",
                        DescribeActivation(candidate.Activation.Identity)));
                    return WithWork(resolution with
                    {
                        Issues = resolution.Issues.Concat(_issues).ToArray()
                    });
                }
                var resolvedByComponent = new Dictionary<
                    string,
                    ResourcePendingResolvedBinding>(StringComparer.Ordinal);
                foreach (var output in pendingOutputs)
                {
                    var staticKey = PendingReplayStaticKey(
                        candidate,
                        output,
                        fingerprint);
                    if (!_bindingsByStaticAuthority.TryGetValue(
                            staticKey,
                            out var binding))
                    {
                        continue;
                    }
                    if (!PendingCandidateMatches(
                            binding.RequestAuthority,
                            output) ||
                        !PendingEffectIdentityMatches(
                            binding.RequestAuthority.EffectId,
                            output.EffectId,
                            output.EffectAuthority) ||
                        !_staticallyBoundRequestIds.Add(binding.RequestId) ||
                        !resolvedByComponent.TryAdd(
                            output.ComponentId,
                            binding.DeepClone()))
                    {
                        _issues.AddRange(Issue(
                            "resource_pending_causal_replay_mismatch",
                            "one exact terminal binding for the immutable candidate output",
                            binding.RequestId));
                    }
                }

                var projected = ProjectResolvedPendingMutations(
                    candidate,
                    pendingOutputs,
                    resolvedByComponent,
                    _issues,
                    fingerprintMetrics);
                foreach (var projection in projected)
                {
                    if (!mutationKeys.Add(projection.Mutation.Key))
                    {
                        _issues.AddRange(Issue(
                            "resource_planner_duplicate_operation",
                            "one mutation per exact replay key",
                            Describe(projection.Mutation.Key)));
                        continue;
                    }
                    mutations.Add(projection.Mutation);
                    componentIdsByMutation[projection.Mutation.Key] =
                        projection.ComponentId;
                    var sourceKey = (
                        projection.Source.SourceKind,
                        projection.Source.SourceId);
                    if (sources.TryGetValue(sourceKey, out var existingSource))
                    {
                        if (existingSource != projection.Source)
                        {
                            _issues.AddRange(Issue(
                                "effect_resource_source_conflict",
                                "one exact source policy per pending receipt",
                                projection.Source.SourceKind + "/" +
                                projection.Source.SourceId));
                        }
                    }
                    else
                    {
                        sources.Add(sourceKey, projection.Source);
                    }
                }

                if (_issues.Count != 0)
                {
                    return WithWork(resolution with
                    {
                        Issues = resolution.Issues.Concat(_issues).ToArray()
                    });
                }

                var projectedKeys = projected
                    .Select(static projection => projection.Mutation.Key)
                    .ToArray();
                var projectedComponents = projected
                    .Select(static projection => projection.ComponentId)
                    .ToArray();
                var candidateComponentMap = candidate.PlannedComponentIdsByMutation
                    .ToDictionary(static pair => pair.Key, static pair => pair.Value);
                foreach (var projection in projected)
                {
                    if (!candidateComponentMap.TryAdd(
                            projection.Mutation.Key,
                            projection.ComponentId))
                    {
                        _issues.AddRange(Issue(
                            "resource_planner_duplicate_operation",
                            "one component binding per exact replay key",
                            Describe(projection.Mutation.Key)));
                    }
                }
                if (_issues.Count != 0)
                {
                    return WithWork(resolution with
                    {
                        Issues = resolution.Issues.Concat(_issues).ToArray()
                    });
                }
                candidates.Add(
                    new EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate(
                        candidate.Activation,
                        candidate.UseSeed,
                        candidate.Producer,
                        candidate.PlannedMutationKeys
                            .Concat(projectedKeys)
                            .ToArray(),
                        candidate.PlannedComponentIds
                            .Concat(projectedComponents)
                            .Distinct(StringComparer.Ordinal)
                            .ToArray(),
                        candidateComponentMap,
                        pendingOutputs,
                        reactionOutputs,
                        candidate.Origin,
                        candidateFingerprint: fingerprint,
                        resolvedPendingBindings: resolvedByComponent,
                        pendingWaveOrdinal: NextWaveOrdinal,
                        causalMaterialFingerprint: material.Fingerprint,
                        effectAuthority: candidate.EffectAuthority));
            }

            return WithWork(resolution with
            {
                SourceExports = sources.Values.ToArray(),
                Mutations = mutations,
                Issues = resolution.Issues.Concat(_issues).ToArray(),
                TriggerCandidates = candidates,
                ComponentIdsByMutation = componentIdsByMutation
            });
        }

        internal IReadOnlyList<ValidationIssue> ValidateComplete(
            IReadOnlyList<string> causallyAcceptedRequestIds)
        {
            ArgumentNullException.ThrowIfNull(causallyAcceptedRequestIds);
            var accepted = causallyAcceptedRequestIds.ToHashSet(
                StringComparer.Ordinal);
            var known = _bindingsByStaticAuthority.Values
                .Select(static binding => binding.RequestId)
                .ToHashSet(StringComparer.Ordinal);
            var unexpected = accepted
                .Where(requestId => !known.Contains(requestId))
                .SelectMany(requestId => Issue(
                    "resource_pending_causal_replay_unexpected",
                    "only a statically bound terminal request may be acknowledged",
                    requestId));
            var missing = _bindingsByStaticAuthority.Values
                .Where(binding => !accepted.Contains(binding.RequestId))
                .SelectMany(binding => Issue(
                    "resource_pending_causal_replay_unresolved",
                    "the exact terminal binding reached during immutable graph replay",
                    binding.RequestId));
            return _issues.Concat(unexpected).Concat(missing).ToArray();
        }
    }

    private sealed record ResolvedPendingMutationProjection(
        string ComponentId,
        ResourceMutationIntent Mutation,
        ResourceMutationSourceExport Source);

    internal enum AcceptedMechanicsReductionKind
    {
        Rejected,
        AwaitingResourceReceipt,
        CompletedOrdinaryReduction
    }

    // Non-publishable reduction result. This is not a source witness or a resumable
    // graph cursor. Only CompleteAcceptedReduction constructs the ordinary final plan.
    internal sealed class AcceptedMechanicsReduction
    {
        private readonly Lazy<AcceptedMechanicsPlanningResult> _completion;
        private readonly ValidationIssue[] _issues;

        private AcceptedMechanicsReduction(
            AcceptedMechanicsReductionKind kind,
            CompletedOrdinaryMechanicsReduction? completed,
            AcceptedMechanicsResourcePlanningResult? discoveryResources,
            AcceptedMechanicsResourcePlanningResult? selectedResources,
            IEnumerable<ValidationIssue> issues,
            Func<AcceptedMechanicsPlanningResult> completion)
        {
            Kind = kind;
            Completed = completed;
            DiscoveryResources = discoveryResources;
            SelectedResources = selectedResources;
            _issues = issues.ToArray();
            _completion = new Lazy<AcceptedMechanicsPlanningResult>(
                completion, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
        }

        internal AcceptedMechanicsReductionKind Kind { get; }
        internal CompletedOrdinaryMechanicsReduction? Completed { get; }
        // Observation is not source admission. In waiting outcomes these transcripts
        // retain the attempted wave, not a completed candidate eligible for publication.
        internal AcceptedMechanicsResourcePlanningResult? DiscoveryResources { get; }
        internal AcceptedMechanicsResourcePlanningResult? SelectedResources { get; }
        internal IReadOnlyList<ValidationIssue> Issues => Array.AsReadOnly(_issues.ToArray());

        internal AcceptedMechanicsPlanningResult Complete() => _completion.Value;

        internal static AcceptedMechanicsReduction Rejected(IEnumerable<ValidationIssue> issues)
        {
            var detachedIssues = issues.ToArray();
            return new(AcceptedMechanicsReductionKind.Rejected, null, null, null,
                detachedIssues, () => new AcceptedMechanicsPlanningResult(null, detachedIssues));
        }

        internal static AcceptedMechanicsReduction Awaiting(
            AcceptedMechanicsInput input,
            string inputFingerprint,
            AcceptedMechanicsPlanningContext context,
            ResourcePendingResolutionState pendingState,
            JsonObject? safeGmPacket,
            EffectAcceptedTurnPlan? effectPlan,
            AcceptedMechanicsResourcePlanningResult discoveryResources,
            AcceptedMechanicsResourcePlanningResult selectedResources)
        {
            var captured = CaptureAssemblyInput(input, context);
            var packet = safeGmPacket?.DeepClone().AsObject();
            return new(AcceptedMechanicsReductionKind.AwaitingResourceReceipt,
                null, discoveryResources, selectedResources, Array.Empty<ValidationIssue>(),
                () => BuildAwaitingReceiptPlan(
                    captured, inputFingerprint, captured.PlanningContext!,
                    new PendingBoundaryDecision(true, pendingState, packet,
                        Array.Empty<ValidationIssue>()), effectPlan));
        }

        internal static AcceptedMechanicsReduction CompletedOrdinary(
            CompletedOrdinaryMechanicsReduction completed,
            AcceptedMechanicsResourcePlanningResult discoveryResources) =>
            new(AcceptedMechanicsReductionKind.CompletedOrdinaryReduction,
                completed, discoveryResources, completed.Resources, Array.Empty<ValidationIssue>(),
                () => AssembleCompletedAcceptedReduction(completed));
    }

    internal sealed class CompletedOrdinaryMechanicsReduction
    {
        private readonly Dictionary<string, JsonObject> _ownerCompanionAfterImages;
        private readonly AcceptedMechanicsOwnerTransition[] _ownerTransitions;
        private readonly JsonObject? _pendingAfterImage;

        internal CompletedOrdinaryMechanicsReduction(
            AcceptedMechanicsInput input,
            string inputFingerprint,
            AcceptedMechanicsPlanningContext context,
            ResourceDefinitionCatalog definitions,
            AcceptedMechanicsResourcePlanningResult resources,
            EffectAcceptedTurnPlan? effects,
            ResourcePendingResolutionState? pendingAfterImage,
            IReadOnlyDictionary<string, JsonObject> ownerCompanionAfterImages,
            IReadOnlyList<AcceptedMechanicsOwnerTransition> ownerTransitions)
        {
            Input = CaptureAssemblyInput(input, context);
            InputFingerprint = inputFingerprint;
            Definitions = definitions;
            Resources = resources;
            Effects = effects;
            _pendingAfterImage = pendingAfterImage?.ToCanonicalRoot().DeepClone().AsObject();
            _ownerCompanionAfterImages = ownerCompanionAfterImages.ToDictionary(
                static pair => pair.Key, static pair => pair.Value.DeepClone().AsObject(),
                StringComparer.Ordinal);
            _ownerTransitions = ownerTransitions.Select(static value => value.Clone()).ToArray();
        }

        internal AcceptedMechanicsInput Input { get; }
        internal string InputFingerprint { get; }
        internal ResourceDefinitionCatalog Definitions { get; }
        internal AcceptedMechanicsResourcePlanningResult Resources { get; }
        internal EffectAcceptedTurnPlan? Effects { get; }
        internal JsonObject? PendingAfterImage => _pendingAfterImage?.DeepClone().AsObject();
        internal IReadOnlyDictionary<string, JsonObject> OwnerCompanionAfterImages =>
            new ReadOnlyDictionary<string, JsonObject>(_ownerCompanionAfterImages.ToDictionary(
                static pair => pair.Key, static pair => pair.Value.DeepClone().AsObject(),
                StringComparer.Ordinal));
        internal IReadOnlyList<AcceptedMechanicsOwnerTransition> OwnerTransitions =>
            Array.AsReadOnly(_ownerTransitions.Select(static value => value.Clone()).ToArray());
    }

    private static AcceptedMechanicsInput CaptureAssemblyInput(
        AcceptedMechanicsInput input, AcceptedMechanicsPlanningContext context) =>
        input.WithPlanningContext(new AcceptedMechanicsPlanningContext(
            context.DefinitionRoot, context.Definitions, context.State, context.History,
            context.Owners, context.Sources, context.Commands, context.EffectIdentityRoot,
            context.EffectPlan, context.CapacityTransitions, context.OwnerCapacityDrafts,
            context.TerminalOwners, context.OwnerCompanionAfterImages, context.OwnerTransitions,
            registeredSystemOutcomes: Array.Empty<IResourceRegisteredSystemOutcomeDraft>(),
            pendingResolutionState: context.PendingResolutionState,
            resourceIdentityFactory: null,
            effectIdentityFactory: null,
            executionSequenceOffset: context.ExecutionSequenceOffset,
            woundStageBundle: context.WoundStageBundle,
            woundAnchorPlan: context.WoundAnchorPlan,
            directWoundPublicationAuthority: context.DirectWoundPublicationAuthority,
            treatmentResourcePublicationAuthority: context.TreatmentResourcePublicationAuthority));

    internal static AcceptedMechanicsPlanningResult BuildAcceptedPlan(
        AcceptedMechanicsInput input,
        string inputFingerprint) =>
        CompleteAcceptedReduction(ReduceAcceptedPlan(input, inputFingerprint));

    internal static AcceptedMechanicsPlanningResult CompleteAcceptedReduction(
        AcceptedMechanicsReduction reduction)
    {
        ArgumentNullException.ThrowIfNull(reduction);
        return reduction.Complete();
    }

    internal static AcceptedMechanicsReduction ReduceAcceptedPlan(
        AcceptedMechanicsInput input,
        string inputFingerprint)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFingerprint);
        var context = input.PlanningContext;
        if (context == null)
        {
            return AcceptedMechanicsReduction.Rejected(Issue(
                    "accepted_mechanics_planning_context_missing",
                    "validated typed mechanics planning context",
                    "missing"));
        }

        var woundStages = context.WoundStageBundle;
        if (woundStages is not null)
        {
            var woundEffectAgreement =
                AcceptedMechanicsCarrierAssembler.ValidateInitialEffectPlan(
                    context.EffectPlan,
                    woundStages);
            if (woundEffectAgreement.Count != 0)
            {
                return AcceptedMechanicsReduction.Rejected(woundEffectAgreement);
            }
        }

        var issues = new List<ValidationIssue>();
        var definitions = context.Definitions;
        var sameTurnDefinitions = new Dictionary<string, ResourceDefinition>(
            StringComparer.Ordinal);
        if (context.Commands.DefinitionCreations.Count != 0)
        {
            var definitionBatch =
                ResourceDefinitionCatalog.BeginMaterializationBatch(
                    definitions);
            foreach (var creation in context.Commands.DefinitionCreations
                         .OrderBy(static value => value.CommandOrdinal))
            {
                using var proposal = JsonDocument.Parse(
                    creation.Definition.ToJsonString());
                var materialized = definitionBatch.MaterializeProposal(
                    proposal.RootElement,
                    input.Turn,
                    creation.EventRef,
                    static () => new ResourceDefinitionIdentity(
                        "resource_definition_" + Guid.NewGuid().ToString("N"),
                        "resource_definition_seal_" + Guid.NewGuid().ToString("N")));
                issues.AddRange(materialized.Issues);
                if (!materialized.IsValid || materialized.Definition == null)
                    continue;
                sameTurnDefinitions.Add(
                    creation.DefinitionRef,
                    materialized.Definition);
            }
            definitions = definitionBatch.Freeze();
        }

        var capacityTransitions = ComposeCapacityTransitions(
            input,
            context,
            definitions,
            sameTurnDefinitions,
            issues);
        var effectPlan = context.EffectPlan;
        var basePeriodicResolution = effectPlan == null
            ? new EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution(
                Array.Empty<ResourceMutationSourceExport>(),
                Array.Empty<ResourceMutationIntent>(),
                Array.Empty<ValidationIssue>())
            : EffectAcceptedTurnPlanner.ResolveDuePeriodicResourceMutations(
                effectPlan,
                context.Owners,
                definitions);
        var priorResolvedBindings = context.PendingResolutionState is
            { Requests.Count: > 0 } activePendingState
            ? SelectPendingReplayBindings(activePendingState)
            : Array.Empty<ResourcePendingResolvedBinding>();
        var discoveryReplay = new ResolvedPendingReplaySession(
            priorResolvedBindings);
        var periodicResolution = discoveryReplay.Bind(basePeriodicResolution);
        issues.AddRange(periodicResolution.Issues);
        var sources = context.Sources;
        if (periodicResolution.SourceExports.Count != 0)
        {
            var sourceResult = ResourceMutationSourceCatalog.Create(
                context.Sources.Exports.Concat(periodicResolution.SourceExports));
            issues.AddRange(sourceResult.Issues);
            if (sourceResult.Catalog != null)
                sources = sourceResult.Catalog;
        }
        var ordinaryMutations = ComposeOrdinaryMutations(
            input,
            context,
            definitions,
            issues)
            .Concat(context.RegisteredSystemOutcomes.SelectMany(static outcome => outcome.Mutations))
            .ToArray();
        var mutations = ordinaryMutations
            .Concat(periodicResolution.Mutations)
            .ToArray();
        if (issues.Count != 0)
            return AcceptedMechanicsReduction.Rejected(issues);

        EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution ResolveAndCollect(
            ResourceAppliedEvent resourceEvent,
            ResourceOperationKey producer)
        {
            var expansion = EffectAcceptedTurnPlanner.ResolveResourceEventMutations(
                effectPlan!,
                resourceEvent,
                producer,
                context.Owners,
                definitions);
            return discoveryReplay.Bind(expansion);
        }

        var discoveryResourceResult = BuildResources(
            new AcceptedMechanicsResourceInput(
                input.Turn,
                definitions,
                context.State,
                context.History,
                sources,
                mutations,
                capacityTransitions,
                ExecutionSequenceOffset: context.ExecutionSequenceOffset,
                EventMutationResolver: effectPlan == null
                    ? null
                    : ResolveAndCollect,
                InitialTriggerCandidates: periodicResolution.TriggerCandidates,
                InitialEffectResolutionWork: periodicResolution.Work,
                EffectPlanAuthority: effectPlan == null
                    ? null
                    : CreateEffectPlanAuthority(effectPlan)),
            context.ResourceIdentityFactory ??
            new AcceptedMechanicsIdentityFactory());
        if (!discoveryResourceResult.IsValid ||
            discoveryResourceResult.StateAfterImage == null ||
            discoveryResourceResult.HistoryAfterImage == null)
        {
            return AcceptedMechanicsReduction.Rejected(discoveryResourceResult.Issues);
        }
        var discoveryReplayIssues = discoveryReplay.ValidateComplete(
            discoveryResourceResult.AcceptedResolvedPendingRequestIds);
        if (discoveryReplayIssues.Count != 0)
        {
            return AcceptedMechanicsReduction.Rejected(discoveryReplayIssues);
        }

        var pendingDecision = ResolvePendingBoundary(
            input,
            context,
            definitions,
            discoveryResourceResult.AcceptedPendingResolutions,
            issues);
        if (!pendingDecision.IsValid)
            return AcceptedMechanicsReduction.Rejected(pendingDecision.Issues);
        if (pendingDecision.IsTerminalSemanticReplay &&
            (discoveryResourceResult.AppliedTransitions.Count != 0 ||
             discoveryResourceResult.Events.Count != 0 ||
             discoveryResourceResult.ResourceTriggerExecutions.Count != 0 ||
             discoveryResourceResult.AcceptedPendingResolutions.Count != 0 ||
             discoveryResourceResult.AcceptedReactionExecutions.Count != 0))
        {
            return AcceptedMechanicsReduction.Rejected(Issue(
                    "resource_pending_terminal_replay_conflict",
                    "an exact semantic replay with no newly applied mechanics",
                    "new mechanics were accepted"));
        }
        if (pendingDecision.AwaitingReceipt)
        {
            return AcceptedMechanicsReduction.Awaiting(
                input, inputFingerprint, context,
                pendingDecision.StateAfterImage!, pendingDecision.SafeGmPacket, effectPlan,
                discoveryResourceResult, discoveryResourceResult);
        }

        var resourceResult = discoveryResourceResult;
        if (pendingDecision.ResolvedBindings is { } resolvedBindings)
        {
            var actualReplay = new ResolvedPendingReplaySession(
                resolvedBindings);
            var actualPeriodicResolution = actualReplay.Bind(
                basePeriodicResolution);
            if (!actualPeriodicResolution.IsValid)
            {
                return AcceptedMechanicsReduction.Rejected(actualPeriodicResolution.Issues);
            }
            var actualSourcesResult = ResourceMutationSourceCatalog.Create(
                context.Sources.Exports.Concat(
                    actualPeriodicResolution.SourceExports));
            if (actualSourcesResult.Catalog == null ||
                actualSourcesResult.Issues.Count != 0)
            {
                return AcceptedMechanicsReduction.Rejected(actualSourcesResult.Issues);
            }
            sources = actualSourcesResult.Catalog;
            EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution ResolveActual(
                ResourceAppliedEvent resourceEvent,
                ResourceOperationKey producer)
            {
                var expansion = EffectAcceptedTurnPlanner.ResolveResourceEventMutations(
                    effectPlan!,
                    resourceEvent,
                    producer,
                    context.Owners,
                    definitions);
                return actualReplay.Bind(expansion);
            }
            resourceResult = BuildResources(
                new AcceptedMechanicsResourceInput(
                    input.Turn,
                    definitions,
                    context.State,
                    context.History,
                    sources,
                    ordinaryMutations
                        .Concat(actualPeriodicResolution.Mutations)
                        .ToArray(),
                    capacityTransitions,
                    ExecutionSequenceOffset: context.ExecutionSequenceOffset,
                    EventMutationResolver: effectPlan == null ? null : ResolveActual,
                    InitialTriggerCandidates:
                        actualPeriodicResolution.TriggerCandidates,
                    InitialEffectResolutionWork:
                        actualPeriodicResolution.Work,
                    EffectPlanAuthority: effectPlan == null
                        ? null
                        : CreateEffectPlanAuthority(effectPlan)),
                context.ResourceIdentityFactory ??
                new AcceptedMechanicsIdentityFactory());
            if (!resourceResult.IsValid ||
                resourceResult.StateAfterImage == null ||
                resourceResult.HistoryAfterImage == null)
            {
                return AcceptedMechanicsReduction.Rejected(resourceResult.Issues);
            }
            var actualReplayIssues = actualReplay.ValidateComplete(
                resourceResult.AcceptedResolvedPendingRequestIds);
            if (actualReplayIssues.Count != 0)
            {
                return AcceptedMechanicsReduction.Rejected(actualReplayIssues);
            }
            if (resourceResult.AcceptedPendingResolutions.Count != 0)
            {
                var nextPendingDecision = ResolvePendingBoundary(
                    input,
                    context,
                    definitions,
                    resourceResult.AcceptedPendingResolutions,
                    issues,
                    pendingStateOverride: pendingDecision.StateAfterImage,
                    receiptsOverride: new JsonArray());
                if (!nextPendingDecision.IsValid)
                {
                    return AcceptedMechanicsReduction.Rejected(nextPendingDecision.Issues);
                }
                if (!nextPendingDecision.AwaitingReceipt)
                {
                    return AcceptedMechanicsReduction.Rejected(Issue(
                            "resource_pending_next_wave_missing",
                            "one exact next pending wave for newly accepted bounded outputs",
                            "not-created"));
                }
                return AcceptedMechanicsReduction.Awaiting(
                    input, inputFingerprint, context,
                    nextPendingDecision.StateAfterImage!, nextPendingDecision.SafeGmPacket, effectPlan,
                    discoveryResourceResult, resourceResult);
            }
        }
        if (effectPlan != null)
        {
            var finalizedEffects =
                EffectAcceptedTurnPlanner.CompleteAcceptedBoundaryTranscript(
                effectPlan,
                resourceResult.EffectBoundaryTranscript!,
                context.EffectIdentityFactory ?? new EffectIdentityFactory());
            if (!finalizedEffects.Success || finalizedEffects.Plan == null)
            {
                return AcceptedMechanicsReduction.Rejected(finalizedEffects.Issues);
            }
            effectPlan = finalizedEffects.Plan;
        }
        var ownerAgreementIssues = context.Owners.ValidateCanonicalAgreement(
            resourceResult.StateAfterImage,
            resourceResult.HistoryAfterImage);
        if (ownerAgreementIssues.Count != 0)
            return AcceptedMechanicsReduction.Rejected(ownerAgreementIssues);

        var ownerCompanionAfterImages = context.OwnerCompanionAfterImages
            .ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.DeepClone().AsObject(),
                StringComparer.Ordinal);
        var ownerTransitions = context.OwnerTransitions
            .Select(static value => value.Clone())
            .ToList();
        foreach (var outcome in context.RegisteredSystemOutcomes)
        {
            var projected = outcome.Project(resourceResult);
            issues.AddRange(projected.Issues);
            foreach (var pair in projected.CompanionAfterImages)
            {
                if (!ownerCompanionAfterImages.TryAdd(
                        pair.Key,
                        pair.Value.DeepClone().AsObject()))
                {
                    issues.AddRange(Issue(
                        "resource_registered_outcome_afterimage_conflict",
                        "one exact owner/companion after-image producer per path",
                        pair.Key));
                }
            }
            ownerTransitions.AddRange(projected.OwnerTransitions.Select(
                static value => value.Clone()));
        }
        if (issues.Count != 0)
            return AcceptedMechanicsReduction.Rejected(issues);
        return AcceptedMechanicsReduction.CompletedOrdinary(
            new CompletedOrdinaryMechanicsReduction(
                input, inputFingerprint, context, definitions, resourceResult, effectPlan,
                pendingDecision.StateAfterImage, ownerCompanionAfterImages, ownerTransitions),
            discoveryResourceResult);
    }

    private static AcceptedMechanicsPlanningResult AssembleCompletedAcceptedReduction(
        CompletedOrdinaryMechanicsReduction completed)
    {
        var input = completed.Input;
        var inputFingerprint = completed.InputFingerprint;
        var context = input.PlanningContext!;
        var resourceResult = completed.Resources;
        var effectPlan = completed.Effects;
        var woundStages = context.WoundStageBundle;
        var definitionAfterImage = completed.Definitions.ToCanonicalRoot();
        var stateAfterImage = JsonNode.Parse(resourceResult.StateAfterImage!.ToCanonicalJson())!.AsObject();
        var historyAfterImage = JsonNode.Parse(resourceResult.HistoryAfterImage!.ToCanonicalJson())!.AsObject();
        var pendingAfterImage = completed.PendingAfterImage;
        var ownerCompanionAfterImages = completed.OwnerCompanionAfterImages.ToDictionary(
            static pair => pair.Key, static pair => pair.Value.DeepClone().AsObject(), StringComparer.Ordinal);
        var ownerTransitions = completed.OwnerTransitions.Select(static value => value.Clone()).ToList();
        var issues = new List<ValidationIssue>();
        var carrierComposition = AcceptedMechanicsCarrierAssembler.Compose(
            effectPlan,
            ownerCompanionAfterImages,
            woundStages,
            context.WoundAnchorPlan);
        if (!carrierComposition.Success)
        {
            return new AcceptedMechanicsPlanningResult(
                null,
                carrierComposition.Issues);
        }
        var carriers = carrierComposition.EffectCarrierAfterImages.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DeepClone().AsObject(),
            StringComparer.Ordinal);
        ownerCompanionAfterImages =
            carrierComposition.OwnerCompanionAfterImages.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.DeepClone().AsObject(),
                StringComparer.Ordinal);
        var woundPublication = carrierComposition.WoundPublication;
        foreach (var transitionPath in ownerTransitions
                     .Select(static value => value.Path)
                     .Distinct(StringComparer.Ordinal))
        {
            if (ownerCompanionAfterImages.ContainsKey(transitionPath) ||
                carriers.ContainsKey(transitionPath) ||
                (woundPublication?.CarrierAfterImages.ContainsKey(transitionPath) ??
                 false))
            {
                issues.AddRange(Issue(
                    "accepted_mechanics_owner_transition_path_conflict",
                    "one typed owner transition or one whole-root after-image producer per path",
                    transitionPath));
            }
        }
        if (issues.Count != 0)
            return new AcceptedMechanicsPlanningResult(null, issues);
        var effectIdentity = effectPlan?.IdentityIndexAfterImage ??
            context.EffectIdentityRoot;
        var touched = new HashSet<string>(StringComparer.Ordinal)
        {
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            EffectAcceptedTurnPlan.IdentityIndexPath
        };
        if (!context.Commands.IsMissing)
            touched.Add(ResourceMaterializationContract.CommandPath);
        if (effectPlan != null)
        {
            touched.UnionWith(effectPlan.TouchedPaths);
            touched.UnionWith(effectPlan.DeletedPaths);
        }
        if (woundPublication is not null)
        {
            touched.Add(AcceptedMechanicsPlan.WoundCommandPath);
            touched.UnionWith(woundPublication.CarrierAfterImages.Keys);
            touched.Add(WoundIdentityState.StatePath);
            touched.Add(WoundHistoryState.HistoryPath);
        }
        touched.UnionWith(ownerCompanionAfterImages.Keys);
        touched.UnionWith(ownerTransitions.Select(static value => value.Path));
        if (pendingAfterImage != null)
            touched.Add(ResourcePendingResolutionState.PendingPath);
        var consumed = new HashSet<string>(StringComparer.Ordinal);
        if (!context.Commands.IsMissing)
            consumed.Add(ResourceMaterializationContract.CommandPath);
        if (effectPlan != null)
            consumed.UnionWith(effectPlan.DeletedPaths);
        if (woundPublication is not null)
            consumed.Add(AcceptedMechanicsPlan.WoundCommandPath);

        return new AcceptedMechanicsPlanningResult(
            new AcceptedMechanicsPlan(
                inputFingerprint,
                definitionAfterImage,
                stateAfterImage,
                historyAfterImage,
                carriers,
                effectIdentity,
                pendingAfterImage == null
                    ? new Dictionary<string, JsonObject?>()
                    : new Dictionary<string, JsonObject?>
                    {
                        [ResourcePendingResolutionState.PendingPath] =
                            pendingAfterImage
                    },
                ownerCompanionAfterImages,
                input.BeforeImages,
                touched.ToArray(),
                consumed.ToArray(),
                input.AuthorityFingerprints,
                resourceResult.Events,
                new ResourceProjectionInput(
                    definitionAfterImage,
                    stateAfterImage,
                    historyAfterImage,
                    context.Owners.Fingerprint),
                context.Owners,
                effectPlan,
                ownerTransitions: ownerTransitions,
                woundStageBundle: woundStages,
                carrierComposition: woundStages is null
                    ? null
                    : carrierComposition,
                directWoundPublicationAuthority:
                    context.DirectWoundPublicationAuthority,
                treatmentResourcePublicationAuthority:
                    context.TreatmentResourcePublicationAuthority),
            Array.Empty<ValidationIssue>());
    }

    private static PendingBoundaryDecision ResolvePendingBoundary(
        AcceptedMechanicsInput input,
        AcceptedMechanicsPlanningContext context,
        ResourceDefinitionCatalog definitions,
        IReadOnlyList<AcceptedEffectBoundedResourceResolution>
            rawCandidates,
        List<ValidationIssue> planningIssues,
        ResourcePendingResolutionState? pendingStateOverride = null,
        JsonArray? receiptsOverride = null)
    {
        var issues = new List<ValidationIssue>();
        var fullTurnFingerprint = CreatePendingFullTurnFingerprint(input);
        var semanticTurnFingerprint =
            CreatePendingSemanticTurnFingerprint(input);
        var acceptedCandidatesByKey = new Dictionary<
            string,
            AcceptedEffectBoundedResourceResolution>(StringComparer.Ordinal);
        foreach (var candidate in rawCandidates)
        {
            var key = PendingCandidateKey(candidate.Resolution);
            if (!acceptedCandidatesByKey.TryAdd(key, candidate))
            {
                issues.AddRange(Issue(
                    "resource_pending_candidate_duplicate",
                    "one exact accepted bounded candidate per causal component",
                    key.Replace('\0', '/')));
            }
        }
        var candidates = NormalizePendingCandidates(
            rawCandidates.Select(static candidate => candidate.Resolution).ToArray(),
            issues);
        var effectCommands = input.EffectCommands;
        var receipts = receiptsOverride?.DeepClone().AsArray() ??
            effectCommands["effectResolutionReceipts"] as JsonArray;
        if (receiptsOverride == null &&
            effectCommands.ContainsKey("effectResolutionReceipts") &&
            receipts == null)
        {
            issues.AddRange(Issue(
                "resource_pending_receipts_invalid",
                "effectResolutionReceipts array",
                effectCommands["effectResolutionReceipts"]?.ToJsonString() ?? "null"));
        }
        receipts ??= new JsonArray();
        if (issues.Count != 0)
            return FailedPendingDecision(issues);

        var pendingState = pendingStateOverride ?? context.PendingResolutionState;
        if (pendingState == null && receipts.Count != 0)
        {
            issues.AddRange(Issue(
                "resource_pending_receipt_without_state",
                "one exact canonical pending-resolution state",
                "missing"));
            return FailedPendingDecision(issues);
        }

        var activeBindings = new List<(
            ResourcePendingRequest Request,
            EffectAcceptedTurnPlanner.EffectBoundedResourceResolution Candidate)>();
        if (pendingState is { Requests.Count: > 0 })
        {
            var unused = candidates.ToList();
            foreach (var request in pendingState.Requests)
            {
                var matches = unused
                    .Where(candidate =>
                        PendingCandidateMatches(request, candidate) &&
                        acceptedCandidatesByKey.TryGetValue(
                            PendingCandidateKey(candidate),
                            out var acceptedCandidate) &&
                        PendingCausalAuthorityMatches(
                            request.CausalAuthority,
                            acceptedCandidate.CausalAuthority,
                            request.EffectAuthority))
                    .ToArray();
                if (matches.Length != 1)
                {
                    var semanticMatches = unused
                        .Where(candidate =>
                            PendingCandidateMatches(request, candidate))
                        .ToArray();
                    var changedAuthorityFields = semanticMatches.Length == 1 &&
                        acceptedCandidatesByKey.TryGetValue(
                            PendingCandidateKey(semanticMatches[0]),
                            out var recomputedCandidate)
                            ? DescribePendingCausalAuthorityDifference(
                                request.CausalAuthority,
                                recomputedCandidate.CausalAuthority)
                            : "candidate-set";
                    issues.AddRange(Issue(
                        "resource_pending_authority_changed",
                        "one exact recomputed bounded effect authority for pending request",
                        request.RequestId + ";changed=" +
                        changedAuthorityFields));
                    continue;
                }
                activeBindings.Add((request, matches[0]));
                unused.Remove(matches[0]);
            }
            if (unused.Count != 0 ||
                activeBindings.Count != pendingState.Requests.Count)
            {
                issues.AddRange(Issue(
                    "resource_pending_companion_set_changed",
                    "same complete bounded request candidate set on full-turn resubmission",
                    $"pending={pendingState.Requests.Count};recomputed={candidates.Count}"));
            }
            if (issues.Count != 0)
                return FailedPendingDecision(issues);

            var resolved = pendingState.Resolve(
                receipts,
                new ResourcePendingResolutionContext(
                    input.SessionId,
                    input.RequestId,
                    input.Turn,
                    fullTurnFingerprint),
                definitions);
            if (!resolved.IsValid || resolved.StateAfterImage == null)
                return FailedPendingDecision(resolved.Issues);
            return BuildResolvedPendingDecision(
                resolved,
                activeBindings,
                issues);
        }

        if (pendingState != null && receipts.Count != 0)
        {
            var matchingTerminals = pendingState.TerminalReceipts
                .Where(terminal =>
                    string.Equals(
                        terminal.SessionId,
                        input.SessionId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        terminal.AcceptedRequestId,
                        input.RequestId,
                        StringComparison.Ordinal) &&
                    terminal.RequestTurn == input.Turn)
                .ToArray();
            var terminalFingerprints = matchingTerminals
                .Select(static terminal => terminal.FullTurnFingerprint)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var terminalSemanticFingerprints = matchingTerminals
                .Select(static terminal =>
                    terminal.RequestAuthority.SemanticTurnFingerprint)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (matchingTerminals.Length == 0 ||
                terminalFingerprints.Length != 1 ||
                terminalSemanticFingerprints.Length != 1 ||
                !string.Equals(
                    terminalSemanticFingerprints[0],
                    semanticTurnFingerprint,
                    StringComparison.Ordinal) ||
                candidates.Count != 0)
            {
                return FailedPendingDecision(Issue(
                    "resource_pending_terminal_replay_conflict",
                    "the exact terminal command envelope with no newly accepted bounded candidates",
                    $"terminals={matchingTerminals.Length};fingerprints={terminalFingerprints.Length};semanticFingerprints={terminalSemanticFingerprints.Length};stored={terminalSemanticFingerprints.FirstOrDefault() ?? "missing"};current={semanticTurnFingerprint};candidates={candidates.Count}"));
            }

            var replay = pendingState.Resolve(
                receipts,
                new ResourcePendingResolutionContext(
                    input.SessionId,
                    input.RequestId,
                    input.Turn,
                    terminalFingerprints[0]),
                definitions);
            if (!replay.IsValid || replay.StateAfterImage == null)
                return FailedPendingDecision(replay.Issues);
            if (replay.ReplayedTerminalReceipts.Count == 0 ||
                replay.ReplayedTerminalReceipts.Count != receipts.Count)
            {
                return FailedPendingDecision(Issue(
                    "resource_pending_terminal_replay_incomplete",
                    "a non-empty exact terminal receipt subset for the original accepted turn",
                    $"submitted={receipts.Count};replayed={replay.ReplayedTerminalReceipts.Count}"));
            }
            return new PendingBoundaryDecision(
                AwaitingReceipt: false,
                replay.StateAfterImage,
                SafeGmPacket: null,
                Issues: Array.Empty<ValidationIssue>(),
                IsTerminalSemanticReplay: true);
        }

        if (pendingState != null && pendingStateOverride == null)
        {
            var hasTerminalReplayAuthority = pendingState.TerminalReceipts.Any(
                terminal =>
                    string.Equals(terminal.SessionId, input.SessionId, StringComparison.Ordinal) &&
                    string.Equals(
                        terminal.AcceptedRequestId,
                        input.RequestId,
                        StringComparison.Ordinal) &&
                    terminal.RequestTurn == input.Turn);
            if (hasTerminalReplayAuthority)
            {
                return FailedPendingDecision(Issue(
                    "resource_pending_terminal_replay_incomplete",
                    "the complete terminal receipt set for semantic replay",
                    "effectResolutionReceipts missing"));
            }
        }

        if (candidates.Count == 0)
        {
            return new PendingBoundaryDecision(
                AwaitingReceipt: false,
                StateAfterImage: null,
                SafeGmPacket: null,
                Issues: Array.Empty<ValidationIssue>());
        }

        var drafts = candidates.Select(candidate =>
            new ResourcePendingResolutionDraft(
                "bounded_receipt",
                input.SessionId,
                input.RequestId,
                input.Turn,
                candidate.EventRef,
                candidate.EffectId,
                candidate.EffectAuthority,
                candidate.Source,
                candidate.SourceAuthority,
                candidate.Target,
                candidate.TargetAuthority,
                candidate.TriggerId,
                candidate.Coordinate,
                candidate.ResourceAuthority,
                candidate.Operation,
                candidate.MinimumAmount,
                candidate.MaximumAmount,
                candidate.SourceAuthorityFingerprint,
                candidate.PolicyFingerprint,
                fullTurnFingerprint,
                semanticTurnFingerprint,
                candidate.SafeSourceLabel,
                candidate.SafeTargetLabel,
                candidate.SafeResourceLabel,
                candidate.SafeOperationLabel,
                acceptedCandidatesByKey[PendingCandidateKey(candidate)]
                    .CausalAuthority)).ToArray();
        var created = ResourcePendingResolutionState.CreatePending(
            pendingState?.ToCanonicalJson(),
            drafts,
            definitions,
            static () => "resource_resolution_" + Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow);
        if (!created.IsValid || created.State == null || created.SafeGmPacket == null)
            return FailedPendingDecision(created.Issues);
        planningIssues.AddRange(created.Issues);
        return new PendingBoundaryDecision(
            AwaitingReceipt: true,
            created.State,
            created.SafeGmPacket,
            Issues: Array.Empty<ValidationIssue>());
    }

    private static PendingBoundaryDecision BuildResolvedPendingDecision(
        ResourcePendingResolutionResult resolved,
        IReadOnlyList<(
            ResourcePendingRequest Request,
            EffectAcceptedTurnPlanner.EffectBoundedResourceResolution Candidate)> bindings,
        List<ValidationIssue> issues)
    {
        if (bindings.Count == 0)
        {
            return FailedPendingDecision(Issue(
                "resource_pending_receipt_binding_missing",
                "at least one resolved active pending request",
                "none"));
        }
        var activeScope = bindings[0].Request;
        var replayBindings = resolved.ResolvedPendingBindings
            .Where(binding =>
                string.Equals(
                    binding.RequestAuthority.SessionId,
                    activeScope.SessionId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    binding.RequestAuthority.AcceptedRequestId,
                    activeScope.AcceptedRequestId,
                    StringComparison.Ordinal) &&
                binding.RequestAuthority.RequestTurn == activeScope.RequestTurn &&
                string.Equals(
                    binding.RequestAuthority.FullTurnFingerprint,
                    activeScope.FullTurnFingerprint,
                    StringComparison.Ordinal))
            .ToArray();
        var resolvedByRequest = new Dictionary<
            string,
            ResourcePendingResolvedBinding>(StringComparer.Ordinal);
        foreach (var binding in replayBindings)
        {
            if (!resolvedByRequest.TryAdd(binding.RequestId, binding))
            {
                issues.AddRange(Issue(
                    "resource_pending_receipt_binding_duplicate",
                    "one exact typed terminal binding per bounded request",
                    binding.RequestId));
            }
        }

        foreach (var (request, _) in bindings)
        {
            if (!resolvedByRequest.TryGetValue(request.RequestId, out var binding) ||
                !string.Equals(
                    binding.RequestAuthority.ReplayFingerprint,
                    request.ReplayFingerprint,
                    StringComparison.Ordinal) ||
                binding.RequestAuthority.CausalAuthority != request.CausalAuthority)
            {
                issues.AddRange(Issue(
                    "resource_pending_receipt_binding_missing",
                    "the exact typed terminal binding for every resolved active request",
                    request.RequestId));
            }
        }
        if (issues.Count != 0)
            return FailedPendingDecision(issues);

        return new PendingBoundaryDecision(
            AwaitingReceipt: false,
            resolved.StateAfterImage,
            SafeGmPacket: null,
            Issues: Array.Empty<ValidationIssue>(),
            ResolvedBindings: replayBindings);
    }

    private static IReadOnlyList<
        EffectAcceptedTurnPlanner.EffectBoundedResourceResolution>
        NormalizePendingCandidates(
            IReadOnlyList<EffectAcceptedTurnPlanner.EffectBoundedResourceResolution>
                candidates,
            List<ValidationIssue> issues)
    {
        var result = new List<EffectAcceptedTurnPlanner.EffectBoundedResourceResolution>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in candidates
                     .OrderBy(static value => value.EventRef, StringComparer.Ordinal)
                     .ThenBy(static value => value.EffectId, StringComparer.Ordinal)
                     .ThenBy(static value => value.TriggerId, StringComparer.Ordinal)
                     .ThenBy(static value => value.Coordinate.Realm, StringComparer.Ordinal)
                     .ThenBy(static value => value.Coordinate.ResourceOwnerId, StringComparer.Ordinal)
                     .ThenBy(static value => value.Coordinate.ResourceKey, StringComparer.Ordinal)
                     .ThenBy(static value => value.SourceAuthorityFingerprint, StringComparer.Ordinal))
        {
            var key = PendingCandidateKey(candidate);
            if (!keys.Add(key))
            {
                issues.AddRange(Issue(
                    "resource_pending_candidate_duplicate",
                    "one exact bounded request candidate per effect/trigger/resource authority",
                    key.Replace('\0', '/')));
                continue;
            }
            result.Add(candidate);
        }
        return result;
    }

    private static string PendingCandidateKey(
        EffectAcceptedTurnPlanner.EffectBoundedResourceResolution candidate) =>
        string.Join(
            "\0",
            candidate.EventRef,
            candidate.EffectId,
            candidate.TriggerId,
            candidate.ComponentId,
            candidate.AfterComponentId ?? "null",
            candidate.Coordinate.Realm,
            ((int)candidate.Coordinate.OwnerKind).ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            candidate.Coordinate.ResourceOwnerId,
            candidate.Coordinate.ResourceKey,
            ((int)candidate.Operation).ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            candidate.SourceAuthorityFingerprint,
            candidate.PolicyFingerprint);

    private static bool PendingCandidateMatches(
        ResourcePendingRequest request,
        EffectAcceptedTurnPlanner.EffectBoundedResourceResolution candidate) =>
        string.Equals(request.EventRef, candidate.EventRef, StringComparison.Ordinal) &&
        request.EffectAuthority == candidate.EffectAuthority &&
        request.SourceAuthority == candidate.SourceAuthority &&
        PendingSourceMatches(
            request.Source,
            candidate.Source,
            request.SourceAuthority) &&
        request.TargetAuthority == candidate.TargetAuthority &&
        PendingTargetMatches(
            request.Target,
            candidate.Target,
            request.TargetAuthority) &&
        string.Equals(request.TriggerId, candidate.TriggerId, StringComparison.Ordinal) &&
        request.ResourceAuthority == candidate.ResourceAuthority &&
        PendingCoordinateMatches(
            request.Coordinate,
            candidate.Coordinate,
            request.ResourceAuthority) &&
        request.Operation == candidate.Operation &&
        request.MinimumAmount == candidate.MinimumAmount &&
        request.MaximumAmount == candidate.MaximumAmount &&
        string.Equals(
            request.SourceAuthorityFingerprint,
            candidate.SourceAuthorityFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            request.PolicyFingerprint,
            candidate.PolicyFingerprint,
            StringComparison.Ordinal);

    private static bool PendingCausalAuthorityMatches(
        ResourcePendingCausalAuthority expected,
        ResourcePendingCausalAuthority actual,
        ResourcePendingAuthorityBinding effectAuthority)
    {
        if (expected == actual)
            return true;
        if (!string.Equals(
                effectAuthority.BindingKind,
                "accepted_application",
                StringComparison.Ordinal))
        {
            return false;
        }
        return expected == actual with { EffectId = expected.EffectId };
    }

    private static bool PendingEffectIdentityMatches(
        string expectedEffectId,
        string actualEffectId,
        ResourcePendingAuthorityBinding effectAuthority) =>
        string.Equals(
            effectAuthority.BindingKind,
            "accepted_application",
            StringComparison.Ordinal) ||
        string.Equals(
            expectedEffectId,
            actualEffectId,
            StringComparison.Ordinal);

    private static string PendingReplayEffectKey(
        string effectId,
        ResourcePendingAuthorityBinding effectAuthority) =>
        EffectAcceptedTurnPlanner.CreatePendingEffectReplayIdentity(
            effectId,
            effectAuthority);

    private static string DescribePendingCausalAuthorityDifference(
        ResourcePendingCausalAuthority expected,
        ResourcePendingCausalAuthority actual)
    {
        var changed = new List<string>();
        if (!string.Equals(expected.EffectId, actual.EffectId, StringComparison.Ordinal))
            changed.Add(nameof(expected.EffectId));
        if (!string.Equals(expected.TriggerId, actual.TriggerId, StringComparison.Ordinal))
            changed.Add(nameof(expected.TriggerId));
        if (!string.Equals(
                expected.ActivationEventRef,
                actual.ActivationEventRef,
                StringComparison.Ordinal))
        {
            changed.Add(nameof(expected.ActivationEventRef));
        }
        if (!string.Equals(
                expected.TriggerEventRef,
                actual.TriggerEventRef,
                StringComparison.Ordinal))
        {
            changed.Add(nameof(expected.TriggerEventRef));
        }
        if (!string.Equals(
                expected.ResourceProducerOperationKey,
                actual.ResourceProducerOperationKey,
                StringComparison.Ordinal))
        {
            changed.Add(nameof(expected.ResourceProducerOperationKey));
        }
        if (expected.Priority != actual.Priority)
            changed.Add(nameof(expected.Priority));
        if (expected.ActivationOrdinal != actual.ActivationOrdinal)
            changed.Add(nameof(expected.ActivationOrdinal));
        if (expected.ConsumesUse != actual.ConsumesUse)
            changed.Add(nameof(expected.ConsumesUse));
        if (expected.UsesBefore != actual.UsesBefore)
            changed.Add(nameof(expected.UsesBefore));
        if (!string.Equals(
                expected.ComponentId,
                actual.ComponentId,
                StringComparison.Ordinal))
        {
            changed.Add(nameof(expected.ComponentId));
        }
        if (!string.Equals(
                expected.AfterComponentId,
                actual.AfterComponentId,
                StringComparison.Ordinal))
        {
            changed.Add(nameof(expected.AfterComponentId));
        }
        if (!string.Equals(
                expected.CandidateFingerprint,
                actual.CandidateFingerprint,
                StringComparison.Ordinal))
        {
            changed.Add(nameof(expected.CandidateFingerprint));
        }
        if (!string.Equals(
                expected.TranscriptPrefixFingerprint,
                actual.TranscriptPrefixFingerprint,
                StringComparison.Ordinal))
        {
            changed.Add(nameof(expected.TranscriptPrefixFingerprint));
        }
        if (expected.WaveOrdinal != actual.WaveOrdinal)
            changed.Add(nameof(expected.WaveOrdinal));
        return changed.Count == 0 ? "none" : string.Join(",", changed);
    }

    private static string PendingReplayStaticKey(
        ResourcePendingCausalAuthority authority,
        ResourcePendingAuthorityBinding effectAuthority) =>
        string.Join(
            "\0",
            PendingReplayEffectKey(authority.EffectId, effectAuthority),
            authority.TriggerId,
            authority.ActivationEventRef,
            authority.TriggerEventRef,
            authority.ResourceProducerOperationKey ?? "null",
            authority.Priority.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            authority.ConsumesUse ? "true" : "false",
            authority.ComponentId,
            authority.AfterComponentId ?? "null",
            authority.CandidateFingerprint);

    private static IReadOnlyList<ResourcePendingResolvedBinding>
        SelectPendingReplayBindings(ResourcePendingResolutionState state)
    {
        var active = state.Requests[0];
        return state.ResolvedPendingBindings
            .Where(binding =>
                string.Equals(
                    binding.RequestAuthority.SessionId,
                    active.SessionId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    binding.RequestAuthority.AcceptedRequestId,
                    active.AcceptedRequestId,
                    StringComparison.Ordinal) &&
                binding.RequestAuthority.RequestTurn == active.RequestTurn &&
                string.Equals(
                    binding.RequestAuthority.FullTurnFingerprint,
                    active.FullTurnFingerprint,
                    StringComparison.Ordinal))
            .ToArray();
    }

    private static string PendingReplayStaticKey(
        EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate candidate,
        EffectAcceptedTurnPlanner.EffectBoundedResourceResolution output,
        string candidateFingerprint) =>
        string.Join(
            "\0",
            PendingReplayEffectKey(
                candidate.Activation.Identity.EffectId,
                output.EffectAuthority),
            candidate.Activation.Identity.TriggerId,
            candidate.Activation.Identity.EventRef,
            candidate.Activation.Identity.TriggerEventRef,
            candidate.Producer == null
                ? "null"
                : CreateStableProducerOperationKey(candidate.Producer),
            candidate.Activation.Priority.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            candidate.Activation.ConsumesUse ? "true" : "false",
            output.ComponentId,
            output.AfterComponentId ?? "null",
            candidateFingerprint);

    private static IReadOnlyList<ResolvedPendingMutationProjection>
        ProjectResolvedPendingMutations(
        EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate candidate,
        IReadOnlyList<EffectAcceptedTurnPlanner.EffectBoundedResourceResolution>
            outputs,
        IReadOnlyDictionary<string, ResourcePendingResolvedBinding>
            resolvedByComponent,
        List<ValidationIssue> issues,
        PlannerWorkMetrics? metrics = null)
    {
        if (resolvedByComponent.Count == 0)
            return Array.Empty<ResolvedPendingMutationProjection>();

        var outputByComponent = new Dictionary<
            string,
            EffectAcceptedTurnPlanner.EffectBoundedResourceResolution>(
                StringComparer.Ordinal);
        foreach (var output in outputs)
        {
            if (!outputByComponent.TryAdd(output.ComponentId, output))
            {
                issues.AddRange(Issue(
                    "resource_pending_component_binding_ambiguous",
                    "one exact bounded output per component in one activation",
                    output.ComponentId));
            }
        }
        if (issues.Count != 0)
            return Array.Empty<ResolvedPendingMutationProjection>();

        var plannedKeysByComponent = new Dictionary<
            string,
            List<ResourceOperationKey>>(StringComparer.Ordinal);
        foreach (var pair in candidate.PlannedComponentIdsByMutation)
        {
            if (!plannedKeysByComponent.TryGetValue(
                    pair.Value,
                    out var plannedKeys))
            {
                plannedKeys = new List<ResourceOperationKey>();
                plannedKeysByComponent.Add(pair.Value, plannedKeys);
            }
            plannedKeys.Add(pair.Key);
        }

        var orderedComponentIds = resolvedByComponent.Keys.ToArray();
        var resolvedComponentIds = orderedComponentIds.ToHashSet(
            StringComparer.Ordinal);
        var completed = new Dictionary<
            string,
            ResolvedPendingMutationProjection?>(StringComparer.Ordinal);
        var terminalWithoutProjection = new HashSet<string>(
            StringComparer.Ordinal);
        var projectedSources = new Dictionary<
            string,
            ResourceMutationSourceExport>(StringComparer.Ordinal);
        var internalPredecessors = new Dictionary<string, string>(
            StringComparer.Ordinal);
        var externalPredecessors = new Dictionary<
            string,
            ResourceOperationKey>(StringComparer.Ordinal);
        var dependents = orderedComponentIds.ToDictionary(
            static componentId => componentId,
            static _ => new List<string>(),
            StringComparer.Ordinal);
        var indegrees = orderedComponentIds.ToDictionary(
            static componentId => componentId,
            static _ => 0,
            StringComparer.Ordinal);

        foreach (var componentId in orderedComponentIds)
        {
            if (metrics != null)
                metrics.PendingProjectionDependencyVisitCount++;
            if (!outputByComponent.TryGetValue(componentId, out var output))
            {
                issues.AddRange(Issue(
                    "resource_pending_causal_replay_mismatch",
                    "one exact bounded output for every terminal binding",
                    componentId));
                terminalWithoutProjection.Add(componentId);
                continue;
            }

            var binding = resolvedByComponent[componentId];
            if (string.Equals(
                    binding.ResultKind,
                    "narrated_no_state_change",
                    StringComparison.Ordinal))
            {
                terminalWithoutProjection.Add(componentId);
                continue;
            }
            if (!string.Equals(
                    binding.ResultKind,
                    "resource_delta",
                    StringComparison.Ordinal) ||
                !binding.Amount.HasValue ||
                !ResourcePendingResolutionState.TryProjectMutationSourceExport(
                    binding,
                    out var source) ||
                source == null)
            {
                issues.AddRange(Issue(
                    "resource_pending_causal_replay_mismatch",
                    "one typed narrated-no-change or resource-delta terminal binding",
                    binding.RequestId));
                terminalWithoutProjection.Add(componentId);
                continue;
            }
            projectedSources.Add(componentId, source);

            if (output.AfterComponentId is not { } predecessorId)
                continue;
            if (metrics != null)
                metrics.PendingProjectionDependencyVisitCount++;
            if (outputByComponent.ContainsKey(predecessorId))
            {
                if (!resolvedComponentIds.Contains(predecessorId))
                {
                    issues.AddRange(Issue(
                        "resource_pending_component_dependency_unresolved",
                        "the exact resolved predecessor binding in the same activation",
                        predecessorId));
                    terminalWithoutProjection.Add(componentId);
                    continue;
                }
                internalPredecessors.Add(componentId, predecessorId);
                indegrees[componentId] = 1;
                dependents[predecessorId].Add(componentId);
                continue;
            }

            if (!plannedKeysByComponent.TryGetValue(
                    predecessorId,
                    out var predecessorKeys) ||
                predecessorKeys.Count != 1)
            {
                issues.AddRange(Issue(
                    "resource_pending_component_dependency_unresolved",
                    "one exact deterministic predecessor in the same activation",
                    predecessorId));
                terminalWithoutProjection.Add(componentId);
                continue;
            }
            externalPredecessors.Add(componentId, predecessorKeys[0]);
        }

        var ready = new Queue<string>(orderedComponentIds.Where(componentId =>
            indegrees[componentId] == 0));
        while (ready.TryDequeue(out var componentId))
        {
            ResolvedPendingMutationProjection? projection = null;
            if (!terminalWithoutProjection.Contains(componentId) &&
                outputByComponent.TryGetValue(componentId, out var output))
            {
                ResourceOperationKey? predecessorKey = null;
                if (internalPredecessors.TryGetValue(
                        componentId,
                        out var predecessorId))
                {
                    if (!completed.TryGetValue(
                            predecessorId,
                            out var predecessorProjection))
                    {
                        issues.AddRange(Issue(
                            "resource_pending_component_dependency_unresolved",
                            "the completed predecessor projection in the indexed dependency graph",
                            predecessorId));
                        terminalWithoutProjection.Add(componentId);
                    }
                    else if (predecessorProjection != null)
                    {
                        predecessorKey = predecessorProjection.Mutation.Key;
                    }
                    else
                    {
                        terminalWithoutProjection.Add(componentId);
                    }
                }
                else if (externalPredecessors.TryGetValue(
                             componentId,
                             out var externalPredecessor))
                {
                    predecessorKey = externalPredecessor;
                }

                if (!terminalWithoutProjection.Contains(componentId))
                {
                    var binding = resolvedByComponent[componentId];
                    var source = projectedSources[componentId];
                    var dependencies = output.Dependencies.ToList();
                    var eventRequirements = output.EventRequirements.ToList();
                    if (candidate.Producer is { } producer)
                    {
                        dependencies.Add(producer);
                        eventRequirements.Add(
                            new ResourceMutationEventRequirement(
                                producer,
                                candidate.Activation.Identity.EventKind));
                    }
                    if (predecessorKey != null)
                    {
                        dependencies.Add(predecessorKey);
                        eventRequirements.Add(
                            new ResourceMutationEventRequirement(
                                predecessorKey,
                                PrimaryResourceEvent(
                                    predecessorKey.Operation)));
                    }
                    var mutation = new ResourceMutationIntent(
                        output.EventRef,
                        output.Coordinate with { },
                        binding.Amount!.Value,
                        new ResourceMutationSourceRequest(
                            source.SourceKind,
                            source.SourceId,
                            output.Operation),
                        dependencies.Distinct().ToArray(),
                        eventRequirements.Distinct().ToArray(),
                        binding.RequestId,
                        ResultConstraint: output.ResultConstraint);
                    projection = new ResolvedPendingMutationProjection(
                        componentId,
                        mutation,
                        source);
                }
            }
            completed.Add(componentId, projection);

            foreach (var dependent in dependents[componentId])
            {
                indegrees[dependent]--;
                if (indegrees[dependent] == 0)
                    ready.Enqueue(dependent);
            }
        }

        var remaining = orderedComponentIds
            .Where(componentId => !completed.ContainsKey(componentId))
            .ToArray();
        if (remaining.Length != 0)
        {
            issues.AddRange(Issue(
                "resource_pending_component_dependency_cycle",
                "one finite acyclic bounded-component dependency graph",
                string.Join(",", remaining.OrderBy(
                    static value => value,
                    StringComparer.Ordinal))));
        }

        return completed.Values
            .Where(static projection => projection != null)
            .Select(static projection => projection!)
            .ToArray();
    }

    private static bool PendingSourceMatches(
        JsonObject expected,
        JsonObject actual,
        ResourcePendingAuthorityBinding binding)
    {
        if (string.Equals(binding.BindingKind, "permanent", StringComparison.Ordinal))
            return JsonNode.DeepEquals(expected, actual);
        return string.Equals(
                   expected["kind"]?.GetValue<string>(),
                   actual["kind"]?.GetValue<string>(),
                   StringComparison.Ordinal) &&
               string.Equals(
                   expected["definitionKey"]?.GetValue<string>(),
                   actual["definitionKey"]?.GetValue<string>(),
                   StringComparison.Ordinal);
    }

    private static bool PendingTargetMatches(
        JsonObject expected,
        JsonObject actual,
        ResourcePendingAuthorityBinding binding)
    {
        if (string.Equals(binding.BindingKind, "permanent", StringComparison.Ordinal))
            return JsonNode.DeepEquals(expected, actual);
        return string.Equals(
            expected["kind"]?.GetValue<string>(),
            actual["kind"]?.GetValue<string>(),
            StringComparison.Ordinal);
    }

    private static bool PendingCoordinateMatches(
        ResourceCoordinate expected,
        ResourceCoordinate actual,
        ResourcePendingAuthorityBinding binding) =>
        string.Equals(expected.Realm, actual.Realm, StringComparison.Ordinal) &&
        expected.OwnerKind == actual.OwnerKind &&
        string.Equals(expected.ResourceKey, actual.ResourceKey, StringComparison.Ordinal) &&
        (!string.Equals(binding.BindingKind, "permanent", StringComparison.Ordinal) ||
         string.Equals(
             expected.ResourceOwnerId,
             actual.ResourceOwnerId,
             StringComparison.Ordinal));

    internal static string CreatePendingFullTurnFingerprint(
        AcceptedMechanicsInput input)
    {
        var effectCommands = input.EffectCommands;
        effectCommands["effectResolutionReceipts"] = new JsonArray();
        using var builder = new ResourceFingerprintBuilder(
            "accepted-mechanics-pending-full-turn-v4");
        builder.Append(input.SessionId);
        builder.Append(input.RequestId);
        builder.Append(input.Realm);
        builder.Append(input.Turn);
        builder.Append(input.AcceptedEvents.ToJsonString());
        builder.Append(input.ResourceCommands.ToJsonString());
        builder.Append(effectCommands.ToJsonString());
        builder.Append(input.InternalInputs.ToJsonString());
        builder.Append(input.AuthorityFingerprints.Definitions);
        builder.Append(input.AuthorityFingerprints.Owners);
        builder.Append(input.AuthorityFingerprints.ResourceState);
        builder.Append(input.AuthorityFingerprints.ResourceHistory);
        builder.Append(input.AuthorityFingerprints.EffectSources);
        builder.Append(input.AuthorityFingerprints.EffectTargets);
        builder.Append(input.AuthorityFingerprints.EffectCarriers);
        builder.Append(input.AuthorityFingerprints.EffectIdentityIndex);
        builder.Append(input.AuthorityFingerprints.AcceptedEvents);
        builder.Append(input.AuthorityFingerprints.InternalInputs);
        AppendPendingFullWoundBinding(builder, input);
        return builder.Build();
    }

    internal static string CreatePendingSemanticTurnFingerprint(
        AcceptedMechanicsInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var effectCommands = input.EffectCommands;
        effectCommands["effectResolutionReceipts"] = new JsonArray();
        using var builder = new ResourceFingerprintBuilder(
            "accepted-mechanics-pending-semantic-turn-v3");
        builder.Append(input.SessionId);
        builder.Append(input.RequestId);
        builder.Append(input.Realm);
        builder.Append(input.Turn);
        builder.Append(input.AcceptedEvents.ToJsonString());
        builder.Append(input.ResourceCommands.ToJsonString());
        builder.Append(effectCommands.ToJsonString());
        AppendPendingWoundCommands(builder, input);
        return builder.Build();
    }

    private static void AppendPendingFullWoundBinding(
        ResourceFingerprintBuilder builder,
        AcceptedMechanicsInput input)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(input);

        AppendPendingWoundCommands(builder, input);
        var woundInput = input.WoundInput;
        builder.Append(woundInput is null ? "wound-input-absent" : "wound-input-present");
        if (woundInput is not null)
            builder.Append(WoundAcceptedTurnFingerprints.ComputeInput(woundInput));

        var woundStages = input.PlanningContext?.WoundStageBundle;
        builder.Append(woundStages is null ? "wound-stages-absent" : "wound-stages-present");
        if (woundStages is not null)
        {
            builder.Append(woundStages.InputFingerprint);
            builder.Append(woundStages.BundleFingerprint);
        }
        builder.Append(input.AuthorityFingerprints.WoundCarriers);
        builder.Append(input.AuthorityFingerprints.WoundIdentityIndex);
        builder.Append(input.AuthorityFingerprints.WoundHistory);
    }

    private static void AppendPendingWoundCommands(
        ResourceFingerprintBuilder builder,
        AcceptedMechanicsInput input)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(input);

        var woundCommands = input.WoundCommands;
        builder.Append(
            woundCommands is null
                ? "wound-commands-absent"
                : "wound-commands-present");
        if (woundCommands is not null)
            builder.Append(woundCommands.ToJsonString());
    }

    private static AcceptedMechanicsPlanningResult BuildAwaitingReceiptPlan(
        AcceptedMechanicsInput input,
        string inputFingerprint,
        AcceptedMechanicsPlanningContext context,
        PendingBoundaryDecision pending,
        EffectAcceptedTurnPlan? effectPlan)
    {
        var stateRoot = JsonNode.Parse(context.State.ToCanonicalJson())!.AsObject();
        var historyRoot = JsonNode.Parse(context.History.ToCanonicalJson())!.AsObject();
        var touched = new[]
        {
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            EffectAcceptedTurnPlan.IdentityIndexPath,
            ResourcePendingResolutionState.PendingPath
        };
        var pendingAfterImages = new Dictionary<string, JsonObject?>
        {
            [ResourcePendingResolutionState.PendingPath] =
                pending.StateAfterImage!.ToCanonicalRoot()
        };
        return new AcceptedMechanicsPlanningResult(
            new AcceptedMechanicsPlan(
                inputFingerprint,
                context.DefinitionRoot,
                stateRoot,
                historyRoot,
                new Dictionary<string, JsonObject>(StringComparer.Ordinal),
                context.EffectIdentityRoot,
                pendingAfterImages,
                new Dictionary<string, JsonObject>(StringComparer.Ordinal),
                input.BeforeImages,
                touched,
                Array.Empty<string>(),
                input.AuthorityFingerprints,
                Array.Empty<ResourceAppliedEvent>(),
                new ResourceProjectionInput(
                    context.DefinitionRoot,
                    stateRoot,
                    historyRoot,
                    context.Owners.Fingerprint),
                context.Owners,
                effectPlan,
                ownerTransitions: null,
                pendingGmPacket: pending.SafeGmPacket,
                pendingPublicationAuthority:
                    new AcceptedMechanicsPendingPublicationAuthority(
                        input,
                        pending.StateAfterImage!.ToCanonicalRoot(),
                        PendingProof),
                woundStageBundle: context.WoundStageBundle),
            Array.Empty<ValidationIssue>());
    }

    private static PendingBoundaryDecision FailedPendingDecision(
        IEnumerable<ValidationIssue> issues) =>
        new(
            AwaitingReceipt: false,
            StateAfterImage: null,
            SafeGmPacket: null,
            Issues: issues.ToArray());

    private static IReadOnlyList<ResourceCapacityIntent> ComposeCapacityTransitions(
        AcceptedMechanicsInput input,
        AcceptedMechanicsPlanningContext context,
        ResourceDefinitionCatalog definitions,
        IReadOnlyDictionary<string, ResourceDefinition> sameTurnDefinitions,
        List<ValidationIssue> issues)
    {
        var result = ComposeOwnerCapacityTransitions(input, context, issues).ToList();
        result.AddRange(context.RegisteredSystemOutcomes
            .OfType<IResourceRegisteredSystemCapacityDraft>()
            .SelectMany(static draft => draft.CapacityTransitions));
        var supplied = context.CapacityTransitions.ToArray();
        var consumed = new HashSet<int>();
        foreach (var command in context.Commands.CapacityChanges
                     .OrderBy(static value => value.CommandOrdinal))
        {
            var definition = ResolveCapacityDefinition(
                command,
                definitions,
                sameTurnDefinitions,
                issues);
            if (definition == null)
                continue;
            var owner = ResolveOwner(input.Realm, command.Target, definition, context, issues);
            if (owner == null)
                continue;
            var coordinate = new ResourceCoordinate(
                owner.Key.Realm,
                owner.Key.OwnerKind,
                owner.Key.ResourceOwnerId,
                definition.ResourceKey);
            if (result.Any(intent => intent.Coordinate == coordinate &&
                                     intent.Operation == ResourceCapacityOperation.Initialize))
            {
                AddIssue(
                    issues,
                    "resource_owner_materialization_capacity_conflict",
                    "new-owner capacity supplied only by its resourceMaterialization envelope",
                    command.EventRef);
                continue;
            }

            var matches = supplied
                .Select((value, index) => (value, index))
                .Where(candidate => !consumed.Contains(candidate.index) &&
                    CapacityIntentMatchesCommand(candidate.value, command, coordinate))
                .ToArray();
            if (matches.Length > 1)
            {
                AddIssue(
                    issues,
                    "resource_capacity_adapter_ambiguous",
                    "one exact validated capacity adapter intent",
                    command.EventRef);
                continue;
            }
            if (matches.Length == 1)
            {
                var candidate = matches[0];
                if (!ValidateCapacityIntentAgainstCommand(
                        candidate.value,
                        command,
                        definition,
                        issues))
                {
                    continue;
                }
                consumed.Add(candidate.index);
                result.Add(candidate.value);
                continue;
            }

            var setting = BuildSettingCapacityIntent(
                command,
                definition,
                owner,
                coordinate,
                issues);
            if (setting != null)
                result.Add(setting);
        }

        foreach (var index in Enumerable.Range(0, supplied.Length))
        {
            if (!consumed.Contains(index))
            {
                AddIssue(
                    issues,
                    "resource_capacity_adapter_unbound",
                    "every validated capacity adapter intent bound to one exact accepted command",
                    supplied[index].EventRef);
            }
        }
        foreach (var duplicate in result
                     .GroupBy(static intent => intent.Key)
                     .Where(static group => group.Count() > 1))
        {
            AddIssue(
                issues,
                "resource_planner_duplicate_operation",
                "one capacity transition per exact replay key",
                Describe(duplicate.Key));
        }
        return result;
    }

    private static IReadOnlyList<ResourceCapacityIntent> ComposeOwnerCapacityTransitions(
        AcceptedMechanicsInput input,
        AcceptedMechanicsPlanningContext context,
        List<ValidationIssue> issues) =>
        ComposeOwnerCapacityTransitions(
            input.Turn,
            context.Owners,
            context.State,
            context.OwnerCapacityDrafts,
            context.TerminalOwners,
            issues);

    internal static IReadOnlyList<ResourceCapacityIntent> ComposeOwnerCapacityTransitions(
        int turn,
        ResourceOwnerAuthority owners,
        ResourceStateLedger state,
        IReadOnlyList<ResourceOwnerCapacityDraft> ownerCapacityDrafts,
        IReadOnlyList<ResourceOwnerKey> terminalOwners,
        List<ValidationIssue> issues,
        ResourceAuthorityWorkMeter? workMeter = null)
    {
        if (turn <= 0)
            throw new ArgumentOutOfRangeException(nameof(turn));
        ArgumentNullException.ThrowIfNull(owners);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(ownerCapacityDrafts);
        ArgumentNullException.ThrowIfNull(terminalOwners);
        ArgumentNullException.ThrowIfNull(issues);
        var result = new List<ResourceCapacityIntent>();
        var ordinal = 0;
        foreach (var draft in ownerCapacityDrafts
                     .OrderBy(static value => value.Coordinate.Realm, StringComparer.Ordinal)
                     .ThenBy(static value => value.Coordinate.OwnerKind)
                     .ThenBy(static value => value.Coordinate.ResourceOwnerId, StringComparer.Ordinal)
                     .ThenBy(static value => value.Coordinate.ResourceKey, StringComparer.Ordinal))
        {
            var ownerKey = new ResourceOwnerKey(
                draft.Coordinate.Realm,
                draft.Coordinate.OwnerKind,
                draft.Coordinate.ResourceOwnerId);
            if (!owners.Entries.TryGetValue(ownerKey, out var owner) ||
                !owner.ResourceCapabilities.Contains(draft.Coordinate.ResourceKey) ||
                !draft.ResolvedCapacity.IsValid ||
                draft.ResolvedCapacity.Capacity?.Initialization == null ||
                !IsAuthorizedOwnerCapacitySource(owner, draft.SourceEvidence))
            {
                AddIssue(
                    issues,
                    "resource_owner_materialization_authority_invalid",
                    "one exact owner, capability, registered lifecycle source, capacity, and initialization authority",
                    Describe(draft.Coordinate));
                continue;
            }

            var capacity = draft.ResolvedCapacity.Capacity;
            var hasCurrent = state.TryResolveExact(
                draft.Coordinate,
                out var current) && current != null;
            if (hasCurrent &&
                current!.Maximum == capacity.Maximum &&
                current.CapacityBinding == capacity.Binding)
            {
                continue;
            }

            ordinal++;
            var operation = hasCurrent
                ? ResourceCapacityOperation.Reconfigure
                : ResourceCapacityOperation.Initialize;
            var resolvedCapacity = hasCurrent
                ? capacity.AsReconfiguration()
                : capacity;
            var disposition = hasCurrent
                ? ResourceCurrentDisposition.ClampToNewMaximum
                : ResourceCurrentDisposition.InitializeFromDefinition;
            result.Add(new ResourceCapacityIntent(
                $"turn_{turn}:resource_owner_{operation.ToString().ToLowerInvariant()}:{ordinal}",
                OriginKind: draft.SourceEvidence.SourceKind,
                OriginId: draft.SourceEvidence.SourceId,
                draft.Coordinate,
                operation,
                resolvedCapacity,
                disposition,
                ResourceMutationPhase.RegisteredSystemOutcome,
                Priority: 40,
                draft.SourceEvidence,
                hasCurrent
                    ? capacity.Binding.AuthorityFingerprint
                    : capacity.Initialization.AuthorityFingerprint,
                ReceiptId: null));

            if (!hasCurrent &&
                !owner.IsResourceCapabilityActive(draft.Coordinate.ResourceKey))
            {
                ordinal++;
                using var fingerprint = new ResourceFingerprintBuilder(
                    "resource-owner-initial-lifecycle-v1");
                fingerprint.Append(owners.Fingerprint);
                fingerprint.Append(owner.AuthorityFingerprint);
                ResourceStateContract.AppendCoordinate(fingerprint, draft.Coordinate);
                fingerprint.Append(capacity.Initialization.AuthorityFingerprint);
                fingerprint.Append(ResourceCapacityOperation.Suspend.ToString());
                var authorityFingerprint = fingerprint.Build();
                result.Add(new ResourceCapacityIntent(
                    $"turn_{turn}:resource_owner_suspend_initialized:{ordinal}",
                    OriginKind: "owner_lifecycle",
                    OriginId: ownerKey.ResourceOwnerId,
                    draft.Coordinate,
                    ResourceCapacityOperation.Suspend,
                    ResolvedCapacity: null,
                    CurrentDisposition: null,
                    ResourceMutationPhase.RegisteredSystemOutcome,
                    Priority: 80,
                    new ResourceSourceEvidence(
                        "owner_lifecycle",
                        ownerKey.ResourceOwnerId,
                        authorityFingerprint),
                    authorityFingerprint,
                    ReceiptId: null));
            }
        }

        Dictionary<ResourceOwnerKey, List<ResourceStateEntry>>? entriesByTerminalOwner =
            terminalOwners.Count == 0
                ? null
                : new Dictionary<ResourceOwnerKey, List<ResourceStateEntry>>();
        // The ledger is already in canonical coordinate order, so each indexed owner list
        // inherits deterministic resource-key order without another sort.
        foreach (var entry in state.Entries)
        {
            var ownerKey = new ResourceOwnerKey(
                entry.Coordinate.Realm,
                entry.Coordinate.OwnerKind,
                entry.Coordinate.ResourceOwnerId);
            if (entriesByTerminalOwner != null)
            {
                workMeter?.VisitOwnerTerminalStateIndex();
                if (!entriesByTerminalOwner.TryGetValue(ownerKey, out var ownerEntries))
                {
                    ownerEntries = new List<ResourceStateEntry>();
                    entriesByTerminalOwner.Add(ownerKey, ownerEntries);
                }
                ownerEntries.Add(entry);
            }
            if (!owners.Entries.TryGetValue(ownerKey, out var owner) ||
                owner.Lifecycle == ResourceOwnerLifecycle.Terminal)
            {
                continue;
            }

            var desiredState = owner.IsResourceCapabilityActive(
                entry.Coordinate.ResourceKey)
                ? ResourceLifecycleState.Active
                : ResourceLifecycleState.Suspended;
            if (entry.State == desiredState)
                continue;

            var operation = desiredState == ResourceLifecycleState.Active
                ? ResourceCapacityOperation.Resume
                : ResourceCapacityOperation.Suspend;
            ordinal++;
            using var fingerprint = new ResourceFingerprintBuilder(
                "resource-owner-lifecycle-v1");
            fingerprint.Append(owners.Fingerprint);
            fingerprint.Append(owner.AuthorityFingerprint);
            ResourceStateContract.AppendCoordinate(fingerprint, entry.Coordinate);
            fingerprint.Append(entry.Chronology.LastTransitionId);
            fingerprint.Append(operation.ToString());
            var authorityFingerprint = fingerprint.Build();
            result.Add(new ResourceCapacityIntent(
                $"turn_{turn}:resource_owner_{operation.ToString().ToLowerInvariant()}:{ordinal}",
                OriginKind: "owner_lifecycle",
                OriginId: ownerKey.ResourceOwnerId,
                entry.Coordinate,
                operation,
                ResolvedCapacity: null,
                CurrentDisposition: null,
                ResourceMutationPhase.RegisteredSystemOutcome,
                Priority: 80,
                new ResourceSourceEvidence(
                    "owner_lifecycle",
                    ownerKey.ResourceOwnerId,
                    authorityFingerprint),
                authorityFingerprint,
                ReceiptId: null));
        }

        foreach (var terminal in terminalOwners
                     .OrderBy(static value => value.Realm, StringComparer.Ordinal)
                     .ThenBy(static value => value.OwnerKind)
                     .ThenBy(static value => value.ResourceOwnerId, StringComparer.Ordinal))
        {
            workMeter?.LookupOwnerTerminal();
            if (entriesByTerminalOwner == null ||
                !entriesByTerminalOwner.TryGetValue(terminal, out var terminalEntries))
            {
                continue;
            }
            foreach (var entry in terminalEntries)
            {
                workMeter?.VisitOwnerTerminalEntry();
                ordinal++;
                using var fingerprint = new ResourceFingerprintBuilder(
                    "mortal-resource-owner-terminal-v1");
                fingerprint.Append(owners.Fingerprint);
                fingerprint.Append(terminal.Realm);
                fingerprint.Append(ResourceDefinitionCatalog.GetOwnerKindToken(
                    terminal.OwnerKind));
                fingerprint.Append(terminal.ResourceOwnerId);
                ResourceStateContract.AppendCoordinate(fingerprint, entry.Coordinate);
                fingerprint.Append(entry.Chronology.LastTransitionId);
                var authorityFingerprint = fingerprint.Build();
                result.Add(new ResourceCapacityIntent(
                    $"turn_{turn}:resource_owner_retire:{ordinal}",
                    OriginKind: "owner_lifecycle",
                    OriginId: terminal.ResourceOwnerId,
                    entry.Coordinate,
                    ResourceCapacityOperation.Retire,
                    ResolvedCapacity: null,
                    CurrentDisposition: null,
                    ResourceMutationPhase.RegisteredSystemOutcome,
                    Priority: 90,
                    new ResourceSourceEvidence(
                        "owner_lifecycle",
                        terminal.ResourceOwnerId,
                        authorityFingerprint),
                    authorityFingerprint,
                    ReceiptId: null));
            }
        }

        return result;
    }

    private static bool IsAuthorizedOwnerCapacitySource(
        ResourceOwnerAuthorityEntry owner,
        ResourceSourceEvidence source) =>
        string.Equals(source.SourceKind, "owner_materialization", StringComparison.Ordinal)
            ? owner.SameTurn &&
              owner.SameTurnRef != null &&
              string.Equals(
                  owner.SameTurnRef,
                  source.SourceId,
                  StringComparison.Ordinal)
            : (string.Equals(
                   source.SourceKind,
                   "owner_capacity_cycle",
                   StringComparison.Ordinal) ||
               string.Equals(
                   source.SourceKind,
                   "owner_capacity_state",
                   StringComparison.Ordinal)) &&
              ResourceMaterializationContract.IsExactIdentifier(source.SourceId);

    private static IReadOnlyList<ResourceMutationIntent> ComposeOrdinaryMutations(
        AcceptedMechanicsInput input,
        AcceptedMechanicsPlanningContext context,
        ResourceDefinitionCatalog definitions,
        List<ValidationIssue> issues)
    {
        var result = new List<ResourceMutationIntent>();
        foreach (var command in context.Commands.ResourceChanges
                     .OrderBy(static value => value.CommandOrdinal))
        {
            if (!definitions.TryResolveExact(command.ResourceKey, out var definition) ||
                definition == null)
            {
                AddIssue(
                    issues,
                    "resource_planner_definition_unknown",
                    "one exact sealed resource definition",
                    command.ResourceKey);
                continue;
            }
            var owner = ResolveOwner(input.Realm, command.Target, definition, context, issues);
            if (owner == null)
                continue;
            result.Add(new ResourceMutationIntent(
                command.EventRef,
                new ResourceCoordinate(
                    owner.Key.Realm,
                    owner.Key.OwnerKind,
                    owner.Key.ResourceOwnerId,
                    definition.ResourceKey),
                command.Amount,
                new ResourceMutationSourceRequest(
                    command.Source.Kind,
                    command.Source.SourceId,
                    command.Operation),
                Array.Empty<ResourceOperationKey>(),
                Array.Empty<ResourceMutationEventRequirement>(),
                ReceiptId: null));
        }
        return result;
    }

    private static ResourceDefinition? ResolveCapacityDefinition(
        ResourceCapacityCommand command,
        ResourceDefinitionCatalog definitions,
        IReadOnlyDictionary<string, ResourceDefinition> sameTurnDefinitions,
        List<ValidationIssue> issues)
    {
        ResourceDefinition? definition;
        if (command.ResourceDefinitionRef != null)
        {
            sameTurnDefinitions.TryGetValue(command.ResourceDefinitionRef, out definition);
        }
        else
        {
            definitions.TryResolveExact(command.ResourceKey!, out definition);
        }
        if (definition != null)
            return definition;
        AddIssue(
            issues,
            command.ResourceDefinitionRef != null
                ? "resource_command_definition_ref_unknown"
                : "resource_planner_definition_unknown",
            command.ResourceDefinitionRef != null
                ? "one exact same-turn accepted definitionRef"
                : "one exact sealed resource definition",
            command.ResourceDefinitionRef ?? command.ResourceKey ?? "missing");
        return null;
    }

    private static ResourceOwnerAuthorityEntry? ResolveOwner(
        string realm,
        ResourceCommandTarget target,
        ResourceDefinition definition,
        AcceptedMechanicsPlanningContext context,
        List<ValidationIssue> issues)
    {
        var resolution = context.Owners.Resolve(new ResourceOwnerRequest(
            realm,
            target.OwnerKind,
            definition.ResourceKey,
            target.TargetId,
            target.TargetRef));
        issues.AddRange(resolution.Issues);
        return resolution.Entry;
    }

    private static bool CapacityIntentMatchesCommand(
        ResourceCapacityIntent intent,
        ResourceCapacityCommand command,
        ResourceCoordinate coordinate) =>
        string.Equals(intent.EventRef, command.EventRef, StringComparison.Ordinal) &&
        string.Equals(intent.OriginKind, command.Source.Kind, StringComparison.Ordinal) &&
        string.Equals(intent.OriginId, command.Source.SourceId, StringComparison.Ordinal) &&
        intent.Coordinate == coordinate &&
        intent.Operation == command.Operation;

    private static bool ValidateCapacityIntentAgainstCommand(
        ResourceCapacityIntent intent,
        ResourceCapacityCommand command,
        ResourceDefinition definition,
        List<ValidationIssue> issues)
    {
        var valid = intent.CurrentDisposition == command.CurrentDisposition;
        if (command.Operation is ResourceCapacityOperation.Initialize or
            ResourceCapacityOperation.Reconfigure)
        {
            valid &= intent.ResolvedCapacity != null;
            valid &= definition.CapacityPolicy.Kind switch
            {
                ResourceCapacityKind.DefinitionFixed =>
                    command.Capacity == null &&
                    intent.ResolvedCapacity?.Binding.Kind ==
                        ResourceCapacityKind.DefinitionFixed,
                ResourceCapacityKind.InstanceFixed =>
                    command.Capacity is
                    {
                        Kind: ResourceCapacityKind.InstanceFixed,
                        Maximum: not null
                    } &&
                    intent.ResolvedCapacity?.Binding.Kind ==
                        ResourceCapacityKind.InstanceFixed &&
                    intent.ResolvedCapacity.Maximum == command.Capacity.Maximum,
                ResourceCapacityKind.RegisteredFormula =>
                    command.Capacity is
                    {
                        Kind: ResourceCapacityKind.RegisteredFormula,
                        FormulaKey: not null
                    } &&
                    string.Equals(
                        command.Capacity.FormulaKey,
                        definition.CapacityPolicy.FormulaKey,
                        StringComparison.Ordinal) &&
                    intent.ResolvedCapacity?.Binding.Kind ==
                        ResourceCapacityKind.RegisteredFormula,
                _ => false
            };
        }
        else
        {
            valid &= command.Capacity == null && intent.ResolvedCapacity == null;
        }
        if (valid)
            return true;
        AddIssue(
            issues,
            "resource_capacity_adapter_mismatch",
            "validated adapter intent exactly matches command capacity and disposition",
            command.EventRef);
        return false;
    }

    private static ResourceCapacityIntent? BuildSettingCapacityIntent(
        ResourceCapacityCommand command,
        ResourceDefinition definition,
        ResourceOwnerAuthorityEntry owner,
        ResourceCoordinate coordinate,
        List<ValidationIssue> issues)
    {
        if (command.Operation != ResourceCapacityOperation.Initialize ||
            command.ResourceDefinitionRef == null ||
            !string.Equals(command.Source.Kind, "setting_materialization", StringComparison.Ordinal) ||
            !string.Equals(
                command.Source.SourceId,
                command.ResourceDefinitionRef,
                StringComparison.Ordinal))
        {
            AddIssue(
                issues,
                "resource_capacity_source_unknown",
                "exact validated owner/capacity adapter or same-turn setting initialization source",
                command.Source.Kind + "/" + command.Source.SourceId);
            return null;
        }

        var formulaOwner = new ResourceFormulaOwner(
            coordinate.Realm,
            coordinate.OwnerKind,
            coordinate.ResourceOwnerId);
        ResourceCapacityInput? capacityInput = definition.CapacityPolicy.Kind switch
        {
            ResourceCapacityKind.DefinitionFixed when command.Capacity == null =>
                new DefinitionFixedCapacityInput(formulaOwner),
            ResourceCapacityKind.InstanceFixed when command.Capacity is
            {
                Kind: ResourceCapacityKind.InstanceFixed,
                Maximum: not null
            } => new InstanceFixedCapacityInput(
                formulaOwner,
                command.Capacity.Maximum.Value,
                CreateSettingCapacityFingerprint(command, definition, owner)),
            _ => null
        };
        if (capacityInput == null)
        {
            AddIssue(
                issues,
                definition.CapacityPolicy.Kind == ResourceCapacityKind.RegisteredFormula
                    ? "resource_capacity_formula_authority_missing"
                    : "resource_command_capacity_policy_mismatch",
                "command capacity shape and typed owner authority required by sealed definition",
                command.EventRef);
            return null;
        }

        var resolved = ResolvedResourceCapacity.Resolve(
            definition,
            coordinate,
            capacityInput,
            instanceAuthorityKey: definition.CapacityPolicy.Kind ==
                ResourceCapacityKind.InstanceFixed
                ? command.EventRef
                : null,
            includeInitialization: true);
        if (!resolved.IsValid || resolved.Capacity == null)
        {
            issues.AddRange(resolved.Issues);
            return null;
        }
        var sourceFingerprint = CreateSettingCapacityFingerprint(command, definition, owner);
        return new ResourceCapacityIntent(
            command.EventRef,
            command.Source.Kind,
            command.Source.SourceId,
            coordinate,
            command.Operation,
            resolved.Capacity,
            command.CurrentDisposition,
            ResourceMutationPhase.RegisteredSystemOutcome,
            Priority: 50,
            new ResourceSourceEvidence(
                command.Source.Kind,
                command.Source.SourceId,
                sourceFingerprint),
            resolved.Capacity.Initialization!.AuthorityFingerprint,
            ReceiptId: null);
    }

    private static string CreateSettingCapacityFingerprint(
        ResourceCapacityCommand command,
        ResourceDefinition definition,
        ResourceOwnerAuthorityEntry owner)
    {
        var text = string.Join(
            "\0",
            "setting-resource-capacity-v1",
            command.EventRef,
            command.Source.Kind,
            command.Source.SourceId,
            owner.AuthorityFingerprint,
            owner.Key.Realm,
            ResourceDefinitionCatalog.GetOwnerKindToken(owner.Key.OwnerKind),
            owner.Key.ResourceOwnerId,
            definition.ResourceKey,
            definition.Materialization.DefinitionId,
            definition.Materialization.Seal,
            command.Capacity?.Kind.ToString() ?? "definition_fixed",
            command.Capacity?.Maximum?.ToString(
                System.Globalization.CultureInfo.InvariantCulture) ?? "null",
            command.Capacity?.FormulaKey ?? "null");
        return "sha256:" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    // Owns one prepared graph execution. Checkpoints are observations, not source authority.
    internal sealed class ResourceExecutionSession : IDisposable
    {
        private IEnumerator<ResourceExecutionStep>? _execution;
        private int _busy;
        private bool _disposed;
        private bool _faulted;
        private bool _stopAtClosedBoundary;

        internal ResourceExecutionSession(
            AcceptedMechanicsResourceInput input,
            AcceptedMechanicsIdentityFactory identityFactory)
        {
            _execution = ExecuteResourceSession(input, identityFactory, this).GetEnumerator();
        }

        internal AcceptedMechanicsResourcePlanningResult? Result { get; private set; }
        internal bool StopAtClosedBoundary => _stopAtClosedBoundary;
        internal int CheckpointCount { get; private set; }

        internal ResourceExecutionStep AdvanceToClosedBoundary() => Advance(stopAtClosedBoundary: true);

        internal AcceptedMechanicsResourcePlanningResult Drain()
        {
            var step = Advance(stopAtClosedBoundary: false);
            return step.Result ?? throw new InvalidOperationException(
                "An uninterrupted resource drain must terminate with a result.");
        }

        private ResourceExecutionStep Advance(bool stopAtClosedBoundary)
        {
            if (System.Threading.Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
                throw new InvalidOperationException("Resource execution cannot be re-entered.");
            try
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_faulted)
                    throw new InvalidOperationException("Faulted resource execution cannot resume.");
                if (Result != null)
                    throw new InvalidOperationException("Completed resource execution cannot resume.");
                _stopAtClosedBoundary = stopAtClosedBoundary;
                try
                {
                    if (_execution == null || !_execution.MoveNext())
                        throw new InvalidOperationException("Resource execution ended without a result.");
                    var step = _execution.Current;
                    if (step.Checkpoint != null)
                        CheckpointCount++;
                    if (step.Result != null)
                    {
                        Result = step.Result;
                        _execution.Dispose();
                        _execution = null;
                    }
                    return step;
                }
                catch
                {
                    _faulted = true;
                    _execution?.Dispose();
                    _execution = null;
                    throw;
                }
            }
            finally
            {
                System.Threading.Volatile.Write(ref _busy, 0);
            }
        }

        public void Dispose()
        {
            if (System.Threading.Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
                throw new InvalidOperationException("Active resource execution cannot be disposed.");
            try
            {
                if (_disposed)
                    return;
                _disposed = true;
                _execution?.Dispose();
                _execution = null;
            }
            finally
            {
                System.Threading.Volatile.Write(ref _busy, 0);
            }
        }
    }

    internal sealed class ResourceExecutionStep
    {
        private ResourceExecutionStep(
            ResourceClosedBoundaryCheckpoint? checkpoint,
            AcceptedMechanicsResourcePlanningResult? result)
        {
            Checkpoint = checkpoint;
            Result = result;
        }

        internal ResourceClosedBoundaryCheckpoint? Checkpoint { get; }
        internal AcceptedMechanicsResourcePlanningResult? Result { get; }
        internal static ResourceExecutionStep Paused(ResourceClosedBoundaryCheckpoint checkpoint) =>
            new(checkpoint ?? throw new ArgumentNullException(nameof(checkpoint)), null);
        internal static ResourceExecutionStep Finished(AcceptedMechanicsResourcePlanningResult result) =>
            new(null, result ?? throw new ArgumentNullException(nameof(result)));
    }

    // A closed resource causal frontier only: no completed effect plan/transcript,
    // pending receipt, accepted source seal, common plan or publication authority.
    internal sealed class ResourceClosedBoundaryCheckpoint
    {
        private readonly ResourceTransition[] _pendingHistoryTransitions;
        private readonly ResourceTransition[] _appliedTransitions;
        private readonly ResourceTransition[] _replayTransitions;
        private readonly ResourceAppliedEvent[] _events;
        private readonly long[] _closedEffectBoundaryOrdinals;

        internal ResourceClosedBoundaryCheckpoint(
            ResourceOperationKey lastCompletedOperation,
            int nextExecutionSequence,
            ResourceDefinitionCatalog definitions,
            ResourceStateLedger state,
            ResourceHistoryState historyBaseline,
            IReadOnlyList<ResourceTransition> pendingHistoryTransitions,
            IReadOnlyList<ResourceTransition> appliedTransitions,
            IReadOnlyList<ResourceTransition> replayTransitions,
            IReadOnlyList<ResourceAppliedEvent> events,
            IEnumerable<long> closedEffectBoundaryOrdinals,
            AcceptedMechanicsPlannerStatistics statistics,
            AcceptedEffectBoundaryTranscript.ClosedPrefix effectPrefix)
        {
            LastCompletedOperation = lastCompletedOperation;
            NextExecutionSequence = nextExecutionSequence;
            Definitions = definitions;
            State = state;
            HistoryBaseline = historyBaseline;
            _pendingHistoryTransitions = pendingHistoryTransitions.ToArray();
            _appliedTransitions = appliedTransitions.ToArray();
            _replayTransitions = replayTransitions.ToArray();
            _events = events.ToArray();
            _closedEffectBoundaryOrdinals = closedEffectBoundaryOrdinals.OrderBy(value => value).ToArray();
            Statistics = statistics;
            EffectPrefix = effectPrefix;
        }

        internal ResourceOperationKey LastCompletedOperation { get; }
        internal int NextExecutionSequence { get; }
        internal ResourceDefinitionCatalog Definitions { get; }
        internal ResourceStateLedger State { get; }
        internal ResourceHistoryState HistoryBaseline { get; }
        internal IReadOnlyList<ResourceTransition> PendingHistoryTransitions =>
            Array.AsReadOnly(_pendingHistoryTransitions.ToArray());
        internal IReadOnlyList<ResourceTransition> AppliedTransitions =>
            Array.AsReadOnly(_appliedTransitions.ToArray());
        internal IReadOnlyList<ResourceTransition> ReplayTransitions =>
            Array.AsReadOnly(_replayTransitions.ToArray());
        internal IReadOnlyList<ResourceAppliedEvent> Events => Array.AsReadOnly(_events.ToArray());
        internal IReadOnlyList<long> ClosedEffectBoundaryOrdinals =>
            Array.AsReadOnly(_closedEffectBoundaryOrdinals.ToArray());
        internal AcceptedMechanicsPlannerStatistics Statistics { get; }
        internal AcceptedEffectBoundaryTranscript.ClosedPrefix EffectPrefix { get; }
    }

    internal static ResourceExecutionSession BeginResourceExecution(
        AcceptedMechanicsResourceInput input,
        AcceptedMechanicsIdentityFactory identityFactory)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(identityFactory);
        return new ResourceExecutionSession(input, identityFactory);
    }

    internal static AcceptedMechanicsResourcePlanningResult BuildResources(
        AcceptedMechanicsResourceInput input,
        AcceptedMechanicsIdentityFactory identityFactory)
    {
        using var session = BeginResourceExecution(input, identityFactory);
        return session.Drain();
    }

    private static IEnumerable<ResourceExecutionStep> ExecuteResourceSession(
        AcceptedMechanicsResourceInput input,
        AcceptedMechanicsIdentityFactory identityFactory,
        ResourceExecutionSession session)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(identityFactory);
        var metrics = new PlannerWorkMetrics
        {
            CapacityDescriptorCount = input.CapacityTransitions.Count,
            MutationDescriptorCount = input.Mutations.Count,
            EffectTriggerIndexLookupCount =
                input.InitialEffectResolutionWork.IndexLookupCount,
            EffectTriggerCandidateVisitCount =
                input.InitialEffectResolutionWork.CandidateVisitCount,
            EffectSourceBindingIndexLookupCount =
                input.InitialEffectResolutionWork.SourceBindingIndexLookupCount,
            EffectSourceBindingCandidateVisitCount =
                input.InitialEffectResolutionWork.SourceBindingCandidateVisitCount,
            EffectRoutingDescriptorAccessCount =
                input.InitialEffectResolutionWork.RoutingDescriptorAccessCount,
            EffectOccurrenceCloneCount =
                input.InitialEffectResolutionWork.OccurrenceCloneCount,
            EffectFullValidationPassCount =
                input.InitialEffectResolutionWork.FullEffectValidationPassCount,
            EffectTriggerArrayVisitCount =
                input.InitialEffectResolutionWork.TriggerArrayVisitCount,
            EffectComponentIndexLookupCount =
                input.InitialEffectResolutionWork.ComponentIndexLookupCount,
            EffectSelectedComponentVisitCount =
                input.InitialEffectResolutionWork.SelectedComponentVisitCount,
            PendingCandidateFingerprintOutputVisitCount =
                input.InitialEffectResolutionWork
                    .PendingCandidateFingerprintOutputVisitCount,
            PendingProjectionDependencyVisitCount =
                input.InitialEffectResolutionWork
                    .PendingProjectionDependencyVisitCount
        };
        var agreementIssues = input.History.ValidateStateAgreement(input.State);
        if (agreementIssues.Count != 0)
        {
            yield return ResourceExecutionStep.Finished(Failure(agreementIssues, Statistics(metrics: metrics)));
            yield break;
        }
        if (input.CapacityTransitions.Count >
            ResourceMaterializationContract.MaxCapacityTransitionsPerTurn)
        {
            yield return ResourceExecutionStep.Finished(Failure(
                Issue(
                    "resource_planner_capacity_limit_exceeded",
                    $"at most {ResourceMaterializationContract.MaxCapacityTransitionsPerTurn} capacity transitions",
                    input.CapacityTransitions.Count.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)),
                Statistics(metrics: metrics)));
            yield break;
        }

        var identityRegistry = new AllocatedIdentityRegistry();
        var capacityPreparation = PrepareCapacityTransitions(
            input.CapacityTransitions,
            identityFactory,
            identityRegistry);
        if (capacityPreparation.Issues.Count != 0)
        {
            yield return ResourceExecutionStep.Finished(Failure(
                capacityPreparation.Issues,
                Statistics(metrics: metrics)));
            yield break;
        }
        var preparation = PrepareMutations(
            input,
            identityFactory,
            identityRegistry);
        if (preparation.Issues.Count != 0)
        {
            yield return ResourceExecutionStep.Finished(Failure(preparation.Issues, Statistics(metrics: metrics)));
            yield break;
        }
        var preparedMutations = preparation.Mutations;
        if (preparedMutations.Count(static value =>
                value.Route.Phase != ResourceMutationPhase.EffectTrigger) >
            ResourceMaterializationContract.MaxMutationsBeforeTriggers)
        {
            yield return ResourceExecutionStep.Finished(Failure(
                Issue(
                    "resource_planner_mutation_limit_exceeded",
                    $"at most {ResourceMaterializationContract.MaxMutationsBeforeTriggers} pre-trigger mutations",
                    preparedMutations.Count(static value =>
                        value.Route.Phase != ResourceMutationPhase.EffectTrigger).ToString(
                            System.Globalization.CultureInfo.InvariantCulture)),
                Statistics(metrics: metrics)));
            yield break;
        }

        var graphPreparation = PrepareCompleteResourceGraph(
            input,
            preparedMutations,
            identityFactory,
            identityRegistry);
        metrics.EffectTriggerIndexLookupCount +=
            graphPreparation.Work.EffectTriggerIndexLookupCount;
        metrics.EffectTriggerCandidateVisitCount +=
            graphPreparation.Work.EffectTriggerCandidateVisitCount;
        metrics.EffectSourceBindingIndexLookupCount +=
            graphPreparation.Work.EffectSourceBindingIndexLookupCount;
        metrics.EffectSourceBindingCandidateVisitCount +=
            graphPreparation.Work.EffectSourceBindingCandidateVisitCount;
        metrics.EffectRoutingDescriptorAccessCount +=
            graphPreparation.Work.EffectRoutingDescriptorAccessCount;
        metrics.EffectOccurrenceCloneCount +=
            graphPreparation.Work.EffectOccurrenceCloneCount;
        metrics.EffectFullValidationPassCount +=
            graphPreparation.Work.EffectFullValidationPassCount;
        metrics.EffectTriggerArrayVisitCount +=
            graphPreparation.Work.EffectTriggerArrayVisitCount;
        metrics.EffectComponentIndexLookupCount +=
            graphPreparation.Work.EffectComponentIndexLookupCount;
        metrics.EffectSelectedComponentVisitCount +=
            graphPreparation.Work.EffectSelectedComponentVisitCount;
        metrics.PendingCandidateFingerprintOutputVisitCount +=
            graphPreparation.Work.PendingCandidateFingerprintOutputVisitCount;
        metrics.PendingProjectionDependencyVisitCount +=
            graphPreparation.Work.PendingProjectionDependencyVisitCount;
        metrics.SourceAuthoritySeedCount =
            graphPreparation.Work.SourceAuthoritySeedCount;
        metrics.SourceAuthorityAddVisitCount =
            graphPreparation.Work.SourceAuthorityAddVisitCount;
        metrics.SourceAuthorityResolveLookupCount =
            graphPreparation.Work.SourceAuthorityResolveLookupCount;
        metrics.SourceAuthorityFreezeCount =
            graphPreparation.Work.SourceAuthorityFreezeCount;
        if (!graphPreparation.IsValid)
        {
            yield return ResourceExecutionStep.Finished(Failure(
                graphPreparation.Issues,
                Statistics(metrics: metrics)));
            yield break;
        }
        preparedMutations = graphPreparation.Mutations;
        metrics.MutationDescriptorCount = preparedMutations.Count;
        metrics.GraphNodeDescriptorCount = graphPreparation.Graph!.OrderedNodes.Count;
        metrics.MaximumTriggerDepth = graphPreparation.Graph.MaximumDepth;
        var candidateAuthorities = ValidatePendingCandidateAuthorities(
            graphPreparation.TriggerCandidates,
            preparedMutations,
            graphPreparation.SourceExports,
            metrics);
        if (!candidateAuthorities.IsValid)
        {
            yield return ResourceExecutionStep.Finished(Failure(
                candidateAuthorities.Issues,
                Statistics(metrics: metrics)));
            yield break;
        }
        var workingLedger = new ResourceWorkingLedger(input.State.Entries);
        var workingHistory = new ResourceHistoryWorkingSet(input.History);
        var events = new List<ResourceAppliedEvent>();
        var appliedTransitions = new List<ResourceTransition>();
        var replayTransitions = new List<ResourceTransition>();
        var executionSequence = input.ExecutionSequenceOffset;

        foreach (var capacity in capacityPreparation.Transitions)
        {
            var result = ResourceMutationReducer.ApplyCapacityTransition(
                workingLedger,
                workingHistory,
                new AuthorizedResourceCapacityTransition(
                    capacity.TransitionId,
                    capacity.OperationId,
                    capacity.Intent.EventRef,
                    capacity.Intent.OriginKind,
                    capacity.Intent.OriginId,
                    capacity.Intent.Coordinate,
                    capacity.Intent.Operation,
                    capacity.Intent.ResolvedCapacity,
                    capacity.Intent.CurrentDisposition,
                    capacity.Intent.Phase,
                    capacity.Intent.Priority,
                    executionSequence++,
                    capacity.Intent.SourceEvidence,
                    capacity.Intent.PolicyFingerprint,
                    capacity.Intent.ReceiptId,
                    input.Turn),
                input.Definitions);
            if (!result.IsValid)
            {
                yield return ResourceExecutionStep.Finished(Failure(
                    result.Issues,
                    Statistics(workingHistory, metrics)));
                yield break;
            }
            workingLedger = result.WorkingLedger!;
            if (result.Transition != null)
                appliedTransitions.Add(result.Transition);
            else
                replayTransitions.Add(result.ReplayTransition!);
        }

        if (workingLedger.Count > ResourceMaterializationContract.MaxLiveEntries)
        {
            yield return ResourceExecutionStep.Finished(Failure(
                Issue(
                    "resource_state_limit_exceeded",
                    $"at most {ResourceMaterializationContract.MaxLiveEntries} live entries",
                    workingLedger.Count.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)),
                Statistics(workingHistory, metrics)));
            yield break;
        }

        var directBaselineState = workingLedger.Freeze();
        ResourceStateLedger? postDirectState = null;

        var byOperationId = preparedMutations.ToDictionary(
            static value => value.OperationId,
            StringComparer.Ordinal);
        var operationIdByKey = preparedMutations.ToDictionary(
            static value => value.Intent.Key,
            static value => value.OperationId);
        var effectTranscriptBuilder =
            new AcceptedEffectBoundaryTranscript.Builder(
                input.EffectPlanAuthority);
        var producedEvents = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var appliedTriggerMutationKeys = new Dictionary<
            EffectActivationCandidateIdentity,
            HashSet<ResourceOperationKey>>();
        var acceptedCandidates = new List<(
            EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate Candidate,
            AcceptedEffectActivation Activation)>();
        var acceptedCandidateIdentities = new HashSet<
            EffectActivationCandidateIdentity>();
        var acceptedBoundaryByIdentity = new Dictionary<
            EffectActivationCandidateIdentity,
            EffectEventBoundaryStamp>();
        var acceptedActivationByIdentity = new Dictionary<
            EffectActivationCandidateIdentity,
            AcceptedEffectActivation>();
        var acceptedByBoundary = new Dictionary<
            long,
            List<(
                EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate Candidate,
                AcceptedEffectActivation Activation,
                EffectEventBoundaryStamp Boundary)>>();
        var remainingOperationsByBoundary = new Dictionary<
            long,
            HashSet<string>>();
        var completedOperationIds = new HashSet<string>(StringComparer.Ordinal);
        var parentBoundaryByOrdinal = new Dictionary<long, long?>();
        var closedBoundaries = new HashSet<long>();
        var unresolvedPendingBoundaries = new HashSet<long>();
        var useSeeds = new Dictionary<string, CanonicalEffectUseSeed>(StringComparer.Ordinal);
        foreach (var candidate in graphPreparation.TriggerCandidates)
        {
            if (candidate.UseSeed == null)
                continue;
            if (!string.Equals(
                    candidate.UseSeed.EffectId,
                    candidate.Activation.Identity.EffectId,
                    StringComparison.Ordinal) ||
                useSeeds.TryGetValue(candidate.UseSeed.EffectId, out var existingSeed) &&
                existingSeed.RemainingUses != candidate.UseSeed.RemainingUses)
            {
                yield return ResourceExecutionStep.Finished(Failure(
                    Issue(
                        "effect_use_seed_conflict",
                        "one exact canonical remaining-use seed per effect",
                        DescribeActivation(candidate.Activation.Identity)),
                    Statistics(workingHistory, metrics)));
                yield break;
            }
            useSeeds.TryAdd(candidate.UseSeed.EffectId, candidate.UseSeed);
        }
        var initializedArbiter = AcceptedEffectUseArbiter.Initialize(
            useSeeds.Values
                .OrderBy(static seed => seed.EffectId, StringComparer.Ordinal)
                .ToArray());
        if (!initializedArbiter.IsValid || initializedArbiter.Arbiter == null)
        {
            yield return ResourceExecutionStep.Finished(Failure(
                ArbiterIssues(initializedArbiter.Issues),
                Statistics(workingHistory, metrics)));
            yield break;
        }
        var arbiter = initializedArbiter.Arbiter;
        var candidatesByProducerEvent = graphPreparation.TriggerCandidates
            .Where(static candidate => candidate.Producer != null)
            .GroupBy(candidate => (
                Producer: candidate.Producer!,
                candidate.Activation.Identity.EventKind))
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<
                    EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>)
                    group.ToArray());

        bool HasOpenChildBoundary(long boundaryOrdinal) =>
            parentBoundaryByOrdinal.Any(pair =>
                pair.Value == boundaryOrdinal &&
                !closedBoundaries.Contains(pair.Key));

        IReadOnlyList<ValidationIssue> CloseReadyBoundaries()
        {
            var issues = new List<ValidationIssue>();
            while (true)
            {
                var boundaryOrdinal = remainingOperationsByBoundary
                    .Where(pair => pair.Value.Count == 0 &&
                                   !closedBoundaries.Contains(pair.Key) &&
                                   !unresolvedPendingBoundaries.Contains(
                                       pair.Key) &&
                                   !HasOpenChildBoundary(pair.Key))
                    .Select(static pair => pair.Key)
                    .OrderByDescending(static value => value)
                    .Cast<long?>()
                    .FirstOrDefault();
                if (!boundaryOrdinal.HasValue)
                    break;
                var boundaryToClose = effectTranscriptBuilder
                    .FindBoundary(boundaryOrdinal.Value);
                if (boundaryToClose == null)
                {
                    issues.AddRange(Issue(
                        "effect_boundary_close_invalid",
                        "one exact open boundary",
                        boundaryOrdinal.Value.ToString(
                            System.Globalization.CultureInfo.InvariantCulture)));
                    break;
                }
                if (!acceptedByBoundary.TryGetValue(
                        boundaryOrdinal.Value,
                        out var acceptedForBoundary))
                {
                    var emptyCloseIssue =
                        effectTranscriptBuilder.CloseBoundary(boundaryToClose);
                    if (emptyCloseIssue != null)
                    {
                        issues.AddRange(Issue(
                            emptyCloseIssue.Code,
                            emptyCloseIssue.Expected,
                            emptyCloseIssue.Actual));
                        break;
                    }
                    closedBoundaries.Add(boundaryOrdinal.Value);
                    continue;
                }
                foreach (var accepted in acceptedForBoundary
                             .OrderBy(static value =>
                                  value.Activation.Stamp.ActivationOrdinal))
                {
                    foreach (var reaction in accepted.Candidate.ReactionOutputs
                                 .Where(static reaction => string.Equals(
                                     reaction.Dependency,
                                     "after_current_event",
                                     StringComparison.Ordinal))
                                 .OrderBy(static reaction =>
                                     reaction.ComponentPriority)
                                 .ThenBy(
                                     static reaction => reaction.ComponentId,
                                     StringComparer.Ordinal)
                                 .ThenBy(
                                     static reaction => reaction.EventRef,
                                     StringComparer.Ordinal))
                    {
                        var releaseIssue = effectTranscriptBuilder.TryRelease(
                            accepted.Boundary,
                            accepted.Activation,
                            reaction,
                            EffectReactionReleaseStage.AfterCurrentEvent);
                        if (releaseIssue != null)
                        {
                            issues.AddRange(Issue(
                                releaseIssue.Code,
                                releaseIssue.Expected,
                                releaseIssue.Actual));
                        }
                    }
                }
                var closeIssue =
                    effectTranscriptBuilder.CloseBoundary(boundaryToClose);
                if (closeIssue != null)
                {
                    issues.AddRange(Issue(
                        closeIssue.Code,
                        closeIssue.Expected,
                        closeIssue.Actual));
                    break;
                }
                closedBoundaries.Add(boundaryOrdinal.Value);
            }
            return issues;
        }

        IReadOnlyList<ValidationIssue> CompleteCausalOperation(string operationId)
        {
            if (!completedOperationIds.Add(operationId))
            {
                return Issue(
                    "effect_boundary_causal_operation_invalid",
                    "each causal operation completes exactly once",
                    operationId);
            }
            effectTranscriptBuilder.CompleteCausalOperation(operationId);
            foreach (var remaining in remainingOperationsByBoundary.Values)
                remaining.Remove(operationId);
            return CloseReadyBoundaries();
        }

        long[] ReadyPendingFrontiers() =>
            unresolvedPendingBoundaries
                .Where(boundaryOrdinal =>
                    !closedBoundaries.Contains(boundaryOrdinal) &&
                    remainingOperationsByBoundary.TryGetValue(
                        boundaryOrdinal,
                        out var remaining) &&
                    remaining.Count == 0 &&
                    !HasOpenChildBoundary(boundaryOrdinal))
                .OrderByDescending(BoundaryDepth)
                .ThenByDescending(static value => value)
                .ToArray();

        IReadOnlyList<ValidationIssue> ArbitrateBoundary(
            IReadOnlyList<EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>
                candidates,
            long? parentBoundaryOrdinal,
            ResourceOperationKey? producer,
            string eventKind,
            string producerEventRef,
            string? producerTransitionId,
            int? producerExecutionSequence,
            long producerMechanicsOrdinal)
        {
            if (parentBoundaryOrdinal is { } parentOrdinal &&
                (producer == null ||
                 !operationIdByKey.TryGetValue(
                     producer,
                     out var producerOperationId) ||
                 !remainingOperationsByBoundary.TryGetValue(
                     parentOrdinal,
                     out var parentRemaining) ||
                 closedBoundaries.Contains(parentOrdinal) ||
                 !parentRemaining.Contains(producerOperationId)))
            {
                return Issue(
                    "effect_boundary_parent_causality_invalid",
                    "the selected open causal lane contains the exact producer operation",
                    parentOrdinal.ToString(
                        System.Globalization.CultureInfo.InvariantCulture));
            }
            var boundary = effectTranscriptBuilder.OpenBoundary(
                parentBoundaryOrdinal,
                producer,
                eventKind,
                producerEventRef,
                producerTransitionId,
                producerExecutionSequence,
                producerMechanicsOrdinal,
                candidates);
            parentBoundaryByOrdinal.Add(
                boundary.BoundaryOrdinal,
                parentBoundaryOrdinal);
            var partitionIssues = new List<ValidationIssue>();
            var eligibleCandidates = new List<
                EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>();
            foreach (var candidate in candidates)
            {
                if (!effectTranscriptBuilder.TryResolveUnavailableEffectId(
                        candidate,
                        out var blockedAvailabilityEffectId))
                {
                    eligibleCandidates.Add(candidate);
                    continue;
                }
                var rejectedIssue = effectTranscriptBuilder.RecordRejected(
                    boundary,
                    candidate,
                    EffectActivationRejectionReason.EffectTerminal,
                    blockedAvailabilityEffectId);
                if (rejectedIssue != null)
                {
                    partitionIssues.AddRange(Issue(
                        rejectedIssue.Code,
                        rejectedIssue.Expected,
                        rejectedIssue.Actual));
                }
            }
            var eligible = eligibleCandidates.ToArray();
            if (eligible.Length == 0)
            {
                var bindIssue = effectTranscriptBuilder.BindCausalClosure(
                    boundary,
                    Array.Empty<string>(),
                    new Dictionary<string, string>(StringComparer.Ordinal));
                if (bindIssue != null)
                {
                    partitionIssues.AddRange(Issue(
                        bindIssue.Code,
                        bindIssue.Expected,
                        bindIssue.Actual));
                }
                var closeIssue = effectTranscriptBuilder.CloseBoundary(boundary);
                if (closeIssue != null)
                {
                    partitionIssues.AddRange(Issue(
                        closeIssue.Code,
                        closeIssue.Expected,
                        closeIssue.Actual));
                }
                else
                {
                    closedBoundaries.Add(boundary.BoundaryOrdinal);
                }
                return partitionIssues;
            }
            var byIdentity = eligible.ToDictionary(
                static candidate => candidate.Activation.Identity);
            var result = arbiter.Arbitrate(
                eligible.Select(static candidate => candidate.Activation).ToArray());
            if (!result.IsValid)
                return ArbiterIssues(result.Issues);
            foreach (var rejected in result.RejectedEvidence)
            {
                var rejectedIssue = effectTranscriptBuilder.RecordRejected(
                    boundary,
                    byIdentity[rejected.Identity],
                    rejected.Reason,
                    rejected.Reason ==
                        EffectActivationRejectionReason.EffectTerminal
                        ? rejected.Identity.EffectId
                        : null);
                if (rejectedIssue != null)
                {
                    partitionIssues.AddRange(Issue(
                        rejectedIssue.Code,
                        rejectedIssue.Expected,
                        rejectedIssue.Actual));
                }
            }
            var acceptedForBoundary = new List<(
                EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate Candidate,
                AcceptedEffectActivation Activation,
                EffectEventBoundaryStamp Boundary)>();
            foreach (var activation in result.AcceptedActivations)
            {
                var candidate = byIdentity[activation.Stamp.Identity];
                acceptedCandidateIdentities.Add(activation.Stamp.Identity);
                acceptedCandidates.Add((candidate, activation));
                acceptedBoundaryByIdentity.Add(activation.Stamp.Identity, boundary);
                acceptedActivationByIdentity.Add(activation.Stamp.Identity, activation);
                effectTranscriptBuilder.RecordAccepted(
                    boundary,
                    activation,
                    candidate);
                acceptedForBoundary.Add((candidate, activation, boundary));
            }
            acceptedByBoundary.Add(boundary.BoundaryOrdinal, acceptedForBoundary);
            var causalRootOperationIds = new List<string>();
            foreach (var accepted in acceptedForBoundary)
            {
                if (accepted.Candidate.PendingOutputs.Any(output =>
                        output.AfterComponentId == null &&
                        !accepted.Candidate.TryResolvePendingBinding(
                            output.ComponentId,
                            out _)))
                {
                    unresolvedPendingBoundaries.Add(
                        boundary.BoundaryOrdinal);
                }
                foreach (var mutationKey in accepted.Candidate.PlannedMutationKeys)
                {
                    if (!operationIdByKey.TryGetValue(
                            mutationKey,
                            out var operationId))
                    {
                        return Issue(
                            "effect_boundary_causal_operation_unresolved",
                            "every accepted candidate mutation in the prepared graph",
                            Describe(mutationKey));
                    }
                    causalRootOperationIds.Add(operationId);
                }
            }
            var causalClosure = graphPreparation.Graph!
                .ExpandCausalOperationClosure(causalRootOperationIds)
                .ToHashSet(StringComparer.Ordinal);
            var replayStableCausalOperationKeys = causalClosure.ToDictionary(
                static operationId => operationId,
                operationId => CreateStableProducerOperationKey(
                    byOperationId[operationId].Intent.Key),
                StringComparer.Ordinal);
            remainingOperationsByBoundary.Add(
                boundary.BoundaryOrdinal,
                causalClosure
                    .Where(operationId =>
                        !completedOperationIds.Contains(operationId))
                    .ToHashSet(StringComparer.Ordinal));
            var causalBindIssue = effectTranscriptBuilder.BindCausalClosure(
                boundary,
                causalClosure,
                replayStableCausalOperationKeys);
            if (causalBindIssue != null)
            {
                partitionIssues.AddRange(Issue(
                    causalBindIssue.Code,
                    causalBindIssue.Expected,
                    causalBindIssue.Actual));
            }

            var releaseIssues = new List<ValidationIssue>(partitionIssues);
            foreach (var activation in result.AcceptedActivations
                         .OrderBy(static value => value.Stamp.ActivationOrdinal))
            {
                var candidate = byIdentity[activation.Stamp.Identity];
                foreach (var reaction in candidate.ReactionOutputs
                             .Where(static reaction => string.Equals(
                                 reaction.Dependency,
                                 "before_current_event",
                                 StringComparison.Ordinal))
                             .OrderBy(static reaction => reaction.ComponentPriority)
                             .ThenBy(
                                 static reaction => reaction.ComponentId,
                                 StringComparer.Ordinal)
                             .ThenBy(
                                 static reaction => reaction.EventRef,
                                 StringComparer.Ordinal))
                {
                    var releaseIssue = effectTranscriptBuilder.TryRelease(
                        boundary,
                        activation,
                        reaction,
                        EffectReactionReleaseStage.BeforeCurrentEvent);
                    if (releaseIssue != null)
                    {
                        releaseIssues.AddRange(Issue(
                            releaseIssue.Code,
                            releaseIssue.Expected,
                            releaseIssue.Actual));
                    }
                }
            }
            foreach (var accepted in acceptedForBoundary.Where(static value =>
                         value.Activation.EffectTerminal ||
                         value.Candidate.ReactionOutputs.Any(
                             ReservesTerminalAvailabilityAtAcceptance)))
            {
                var reservationIssue =
                    effectTranscriptBuilder.ReserveTerminalAvailability(
                        accepted.Boundary,
                        accepted.Activation,
                        accepted.Candidate);
                if (reservationIssue != null)
                {
                    releaseIssues.AddRange(Issue(
                        reservationIssue.Code,
                        reservationIssue.Expected,
                        reservationIssue.Actual));
                }
            }
            releaseIssues.AddRange(CloseReadyBoundaries());
            return releaseIssues;
        }

        foreach (var boundary in graphPreparation.TriggerCandidates
                     .Where(static candidate => candidate.Producer == null)
                     .GroupBy(candidate => (
                         candidate.Activation.Identity.TriggerEventRef,
                         candidate.Activation.Identity.EventKind))
                     .OrderBy(
                         static group => group.Key.TriggerEventRef,
                         StringComparer.Ordinal)
                     .ThenBy(
                         static group => group.Key.EventKind,
                         StringComparer.Ordinal))
        {
            var pendingBoundaryCount = unresolvedPendingBoundaries.Count;
            var initialIssues = ArbitrateBoundary(
                boundary.ToArray(),
                parentBoundaryOrdinal: null,
                producer: null,
                boundary.Key.EventKind,
                boundary.Key.TriggerEventRef,
                producerTransitionId: null,
                producerExecutionSequence: null,
                producerMechanicsOrdinal: -1);
            if (initialIssues.Count != 0)
            {
                yield return ResourceExecutionStep.Finished(Failure(initialIssues, Statistics(workingHistory, metrics)));
                yield break;
            }
            if (unresolvedPendingBoundaries.Count != pendingBoundaryCount)
                break;
        }

        int BoundaryDepth(long boundaryOrdinal)
        {
            var depth = 0;
            var cursor = boundaryOrdinal;
            while (parentBoundaryByOrdinal.TryGetValue(cursor, out var parent) &&
                   parent is { } parentOrdinal)
            {
                depth++;
                cursor = parentOrdinal;
            }
            return depth;
        }

        HashSet<long> BoundaryAncestorChain(long boundaryOrdinal)
        {
            var chain = new HashSet<long>();
            var cursor = (long?)boundaryOrdinal;
            while (cursor is { } current && chain.Add(current))
            {
                cursor = parentBoundaryByOrdinal.TryGetValue(
                    current,
                    out var parent)
                    ? parent
                    : null;
            }
            return chain;
        }

        IReadOnlyList<(long BoundaryOrdinal, IReadOnlySet<string> OperationIds)>
            CurrentCausalLanes(IReadOnlySet<long>? suspendedBoundaries = null) =>
            remainingOperationsByBoundary
                .Where(pair => pair.Value.Count != 0 &&
                               !closedBoundaries.Contains(pair.Key) &&
                               (suspendedBoundaries == null ||
                                !suspendedBoundaries.Contains(pair.Key)) &&
                               !HasOpenChildBoundary(pair.Key))
                .OrderByDescending(pair => BoundaryDepth(pair.Key))
                .ThenByDescending(static pair => pair.Key)
                .Select(static pair =>
                    (pair.Key, (IReadOnlySet<string>)pair.Value))
                .ToArray();

        var scheduler = graphPreparation.Graph!.CreateExecutionScheduler(node =>
            RequirementsSatisfied(byOperationId[node.OperationId], producedEvents));

        bool TryTakeNextCausal(
            IReadOnlySet<long>? suspendedBoundaries,
            out ResourceTriggerGraphNode node,
            out bool shouldExecute,
            out long? boundaryOrdinal)
        {
            var lanes = CurrentCausalLanes(suspendedBoundaries);
            if (lanes.Count == 0)
            {
                if (suspendedBoundaries is { Count: > 0 })
                {
                    node = null!;
                    shouldExecute = false;
                    boundaryOrdinal = null;
                    return false;
                }
                boundaryOrdinal = null;
                return scheduler.TryTakeNext(out node, out shouldExecute);
            }
            foreach (var lane in lanes)
            {
                if (!scheduler.TryTakeNext(
                        lane.OperationIds,
                        out node,
                        out shouldExecute))
                {
                    continue;
                }
                boundaryOrdinal = lane.BoundaryOrdinal;
                return true;
            }
            node = null!;
            shouldExecute = false;
            boundaryOrdinal = null;
            return false;
        }

        long? pendingFrontier;
        ResourceOperationKey? completedAtBoundary = null;
        while (true)
        {
            if (session.StopAtClosedBoundary &&
                completedAtBoundary != null &&
                unresolvedPendingBoundaries.Count == 0 &&
                parentBoundaryByOrdinal.Keys.All(closedBoundaries.Contains))
            {
                var prefix = effectTranscriptBuilder.CaptureClosedPrefix();
                if (!prefix.IsValid || prefix.Prefix == null)
                {
                    yield return ResourceExecutionStep.Finished(Failure(
                        prefix.Issues.SelectMany(issue => Issue(
                            issue.Code, issue.Expected, issue.Actual)).ToArray(),
                        Statistics(workingHistory, metrics)));
                    yield break;
                }
                var checkpoint = new ResourceClosedBoundaryCheckpoint(
                    completedAtBoundary, executionSequence, input.Definitions,
                    workingLedger.Freeze(), input.History, workingHistory.PendingTransitions,
                    appliedTransitions, replayTransitions, events, closedBoundaries,
                    Statistics(workingHistory, metrics), prefix.Prefix);
                completedAtBoundary = null;
                yield return ResourceExecutionStep.Paused(checkpoint);
            }
            var readyPendingFrontiers = ReadyPendingFrontiers();
            if (readyPendingFrontiers.Length > 1)
            {
                yield return ResourceExecutionStep.Finished(Failure(
                    Issue(
                        "effect_boundary_pending_frontier_ambiguous",
                        "one exact ready pending leaf",
                        string.Join(",", readyPendingFrontiers)),
                    Statistics(workingHistory, metrics)));
                yield break;
            }
            pendingFrontier = readyPendingFrontiers
                .Select(static value => (long?)value)
                .SingleOrDefault();
            var suspendedBoundaries = pendingFrontier is { } frontierOrdinal
                ? BoundaryAncestorChain(frontierOrdinal)
                : null;
            if (!TryTakeNextCausal(
                    suspendedBoundaries,
                   out var node,
                   out var shouldExecute,
                   out var selectedBoundaryOrdinal))
            {
                break;
            }
            metrics.SchedulingDescriptorVisitCount++;
            var prepared = byOperationId[node.OperationId];
            if (!shouldExecute)
            {
                producedEvents[prepared.OperationId] = new HashSet<string>(
                    StringComparer.Ordinal);
                var closeIssues = CompleteCausalOperation(prepared.OperationId);
                if (closeIssues.Count != 0)
                {
                    yield return ResourceExecutionStep.Finished(Failure(
                        closeIssues,
                        Statistics(workingHistory, metrics)));
                    yield break;
                }
                scheduler.Complete(node);
                completedAtBoundary = prepared.Intent.Key;
                continue;
            }

            EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate? triggerCandidate = null;
            if (graphPreparation.TriggerCandidatesByMutation.TryGetValue(
                    prepared.Intent.Key,
                    out triggerCandidate) &&
                !acceptedCandidateIdentities.Contains(
                    triggerCandidate.Activation.Identity))
            {
                producedEvents[prepared.OperationId] = new HashSet<string>(
                    StringComparer.Ordinal);
                var closeIssues = CompleteCausalOperation(prepared.OperationId);
                if (closeIssues.Count != 0)
                {
                    yield return ResourceExecutionStep.Finished(Failure(
                        closeIssues,
                        Statistics(workingHistory, metrics)));
                    yield break;
                }
                scheduler.Complete(node);
                completedAtBoundary = prepared.Intent.Key;
                continue;
            }

            var mutation = prepared.Intent;
            if (prepared.Route.Phase >= ResourceMutationPhase.RegisteredSystemOutcome &&
                postDirectState == null)
            {
                postDirectState = workingLedger.Freeze();
            }
            var amountResult = ResolveMutationAmount(
                workingHistory,
                input.Definitions,
                directBaselineState,
                postDirectState,
                prepared);
            if (amountResult.Issues.Count != 0)
            {
                yield return ResourceExecutionStep.Finished(Failure(
                    amountResult.Issues,
                    Statistics(workingHistory, metrics)));
                yield break;
            }
            if (!amountResult.ShouldApply)
            {
                producedEvents[prepared.OperationId] = new HashSet<string>(
                    StringComparer.Ordinal);
                var closeIssues = CompleteCausalOperation(prepared.OperationId);
                if (closeIssues.Count != 0)
                {
                    yield return ResourceExecutionStep.Finished(Failure(
                        closeIssues,
                        Statistics(workingHistory, metrics)));
                    yield break;
                }
                scheduler.Complete(node);
                completedAtBoundary = prepared.Intent.Key;
                continue;
            }

            var result = ResourceMutationReducer.Reduce(
                workingLedger,
                workingHistory,
                new AuthorizedResourceMutation(
                    prepared.TransitionId,
                    prepared.OperationId,
                    mutation.EventRef,
                    mutation.Source.SourceKind,
                    mutation.Source.SourceId,
                    mutation.Coordinate,
                    mutation.Source.Operation,
                    amountResult.Amount,
                    prepared.Route.Phase,
                    prepared.Route.Priority,
                    executionSequence++,
                    prepared.Route.PolicyBinding,
                    mutation.Dependencies,
                    prepared.Route.SourceEvidence,
                    mutation.ReceiptId,
                    input.Turn,
                    mutation.ResultConstraint),
                input.Definitions);
            if (!result.IsValid)
            {
                yield return ResourceExecutionStep.Finished(Failure(
                    result.Issues,
                    Statistics(workingHistory, metrics)));
                yield break;
            }
            workingLedger = result.WorkingLedger!;
            if (result.Transition != null)
            {
                EffectEventBoundaryStamp? acceptedBoundary = null;
                AcceptedEffectActivation? acceptedActivation = null;
                string? appliedComponentId = null;
                var hasAppliedTriggerOutput = triggerCandidate != null &&
                    result.Transition.AppliedAmount != 0m;
                if (hasAppliedTriggerOutput)
                {
                    if (!acceptedBoundaryByIdentity.TryGetValue(
                            triggerCandidate!.Activation.Identity,
                            out acceptedBoundary) ||
                        !acceptedActivationByIdentity.TryGetValue(
                            triggerCandidate.Activation.Identity,
                            out acceptedActivation) ||
                        !triggerCandidate.PlannedComponentIdsByMutation.TryGetValue(
                            prepared.Intent.Key,
                            out appliedComponentId))
                    {
                        yield return ResourceExecutionStep.Finished(Failure(
                            Issue(
                                "effect_boundary_component_evidence_invalid",
                                "one accepted boundary and component for every nonzero trigger mutation",
                                DescribeActivation(
                                    triggerCandidate.Activation.Identity)),
                            Statistics(workingHistory, metrics)));
                        yield break;
                    }
                }
                var mutationMechanicsOrdinal =
                    effectTranscriptBuilder.RecordMutationExecution();
                appliedTransitions.Add(result.Transition);
                effectTranscriptBuilder.RecordResourceMutation(
                    prepared.Intent.Key,
                    result.Transition,
                    ResourceMutationExecutionKind.Applied,
                    mutationMechanicsOrdinal);
                if (hasAppliedTriggerOutput)
                {
                    if (!appliedTriggerMutationKeys.TryGetValue(
                            triggerCandidate!.Activation.Identity,
                            out var appliedKeys))
                    {
                        appliedKeys = new HashSet<ResourceOperationKey>();
                        appliedTriggerMutationKeys.Add(
                            triggerCandidate.Activation.Identity,
                            appliedKeys);
                    }
                    appliedKeys.Add(prepared.Intent.Key);
                    effectTranscriptBuilder.RecordAppliedComponent(
                        acceptedBoundary!,
                        acceptedActivation!,
                        prepared.Intent.Key,
                        appliedComponentId!,
                        result.Transition,
                        mutationMechanicsOrdinal);
                    if (triggerCandidate.PendingOutputs.Any(output =>
                            string.Equals(
                                output.AfterComponentId,
                                appliedComponentId,
                                StringComparison.Ordinal) &&
                            !triggerCandidate.TryResolvePendingBinding(
                                output.ComponentId,
                                out _)))
                    {
                        unresolvedPendingBoundaries.Add(
                            acceptedBoundary!.BoundaryOrdinal);
                    }
                    foreach (var reaction in triggerCandidate.ReactionOutputs
                                 .Where(reaction => string.Equals(
                                     reaction.Dependency,
                                     "after_component",
                                     StringComparison.Ordinal) &&
                                     string.Equals(
                                         reaction.AfterComponentId,
                                         appliedComponentId,
                                         StringComparison.Ordinal))
                                 .OrderBy(static reaction =>
                                     reaction.ComponentPriority)
                                 .ThenBy(
                                     static reaction => reaction.ComponentId,
                                     StringComparer.Ordinal)
                                 .ThenBy(
                                     static reaction => reaction.EventRef,
                                     StringComparer.Ordinal))
                    {
                        var releaseIssue = effectTranscriptBuilder.TryRelease(
                            acceptedBoundary!,
                            acceptedActivation!,
                            reaction,
                            EffectReactionReleaseStage.AfterComponent);
                        if (releaseIssue != null)
                        {
                            yield return ResourceExecutionStep.Finished(Failure(
                                Issue(
                                    releaseIssue.Code,
                                    releaseIssue.Expected,
                                    releaseIssue.Actual),
                                Statistics(workingHistory, metrics)));
                            yield break;
                        }
                    }
                }
                events.AddRange(result.Events);
                producedEvents[prepared.OperationId] = result.Events
                    .Select(static value => value.EventKind)
                    .ToHashSet(StringComparer.Ordinal);
                foreach (var resourceEvent in result.Events)
                {
                    if (!candidatesByProducerEvent.TryGetValue(
                            (prepared.Intent.Key, resourceEvent.EventKind),
                            out var boundaryCandidates))
                    {
                        continue;
                    }
                    var boundaryIssues = ArbitrateBoundary(
                        boundaryCandidates,
                        selectedBoundaryOrdinal,
                        prepared.Intent.Key,
                        resourceEvent.EventKind,
                        resourceEvent.EventRef,
                        result.Transition.TransitionId,
                        result.Transition.ExecutionSequence,
                        mutationMechanicsOrdinal);
                    if (boundaryIssues.Count != 0)
                    {
                        yield return ResourceExecutionStep.Finished(Failure(
                            boundaryIssues,
                            Statistics(workingHistory, metrics)));
                        yield break;
                    }
                }
            }
            else
            {
                var replayMechanicsOrdinal =
                    effectTranscriptBuilder.RecordMutationExecution();
                var replayTransition = result.ReplayTransition!;
                replayTransitions.Add(replayTransition);
                effectTranscriptBuilder.RecordResourceMutation(
                    prepared.Intent.Key,
                    replayTransition,
                    ResourceMutationExecutionKind.Replay,
                    replayMechanicsOrdinal);
                producedEvents[prepared.OperationId] = new HashSet<string>(StringComparer.Ordinal);
            }
            var completionIssues = CompleteCausalOperation(prepared.OperationId);
            if (completionIssues.Count != 0)
            {
                yield return ResourceExecutionStep.Finished(Failure(
                    completionIssues,
                    Statistics(workingHistory, metrics)));
                yield break;
            }
            scheduler.Complete(node);
            completedAtBoundary = prepared.Intent.Key;
        }
        if (scheduler.HasPendingNodes && pendingFrontier == null)
        {
            yield return ResourceExecutionStep.Finished(Failure(
                Issue(
                    "effect_boundary_causal_scheduler_blocked",
                    "one ready operation inside the innermost open causal boundary",
                    string.Join(
                        ",",
                        CurrentCausalLanes()
                            .SelectMany(static lane => lane.OperationIds)
                            .Distinct(StringComparer.Ordinal)
                            .OrderBy(static operationId =>
                                operationId,
                                StringComparer.Ordinal))),
                Statistics(workingHistory, metrics)));
            yield break;
        }
        if (pendingFrontier is { } pendingBoundaryOrdinal)
        {
            var pendingBoundary = effectTranscriptBuilder.FindBoundary(
                pendingBoundaryOrdinal);
            var frontierIssue = pendingBoundary == null
                ? new EffectBoundaryTranscriptIssue(
                    "effect_boundary_pending_frontier_invalid",
                    "one exact open pending boundary",
                    pendingBoundaryOrdinal.ToString(
                        System.Globalization.CultureInfo.InvariantCulture))
                : effectTranscriptBuilder.SealPendingFrontier(pendingBoundary);
            if (frontierIssue != null)
            {
                yield return ResourceExecutionStep.Finished(Failure(
                    Issue(
                        frontierIssue.Code,
                        frontierIssue.Expected,
                        frontierIssue.Actual),
                    Statistics(workingHistory, metrics)));
                yield break;
            }
        }
        else
        {
            effectTranscriptBuilder.SealUseProjection();
        }
        var effectBoundaryTranscriptResult = effectTranscriptBuilder.Freeze();
        if (!effectBoundaryTranscriptResult.IsValid ||
            effectBoundaryTranscriptResult.Transcript == null)
        {
            yield return ResourceExecutionStep.Finished(Failure(
                effectBoundaryTranscriptResult.Issues.SelectMany(issue => Issue(
                    issue.Code,
                    issue.Expected,
                    issue.Actual)),
                Statistics(workingHistory, metrics)));
            yield break;
        }
        var effectBoundaryTranscript =
            effectBoundaryTranscriptResult.Transcript;

        var stateAfterImage = workingLedger.Freeze();
        var frozen = workingHistory.Freeze(input.Definitions);
        if (!frozen.IsValid || frozen.History == null)
        {
            yield return ResourceExecutionStep.Finished(Failure(
                frozen.Issues,
                Statistics(workingHistory, metrics)));
            yield break;
        }
        var finalAgreement = frozen.History.ValidateStateAgreement(stateAfterImage);
        if (finalAgreement.Count != 0)
        {
            yield return ResourceExecutionStep.Finished(Failure(
                finalAgreement,
                Statistics(workingHistory, metrics)));
            yield break;
        }

        var resourceTriggerExecutions = acceptedCandidates
            .OrderBy(static accepted =>
                accepted.Activation.Stamp.ActivationOrdinal)
            .Select(accepted =>
            {
                var candidate = accepted.Candidate;
                var stamp = accepted.Activation.Stamp;
                var appliedKeys = appliedTriggerMutationKeys.TryGetValue(
                    stamp.Identity,
                    out var storedKeys)
                    ? storedKeys
                    : new HashSet<ResourceOperationKey>();
                var componentMap = candidate.PlannedComponentIdsByMutation
                    .Where(component => appliedKeys.Contains(component.Key))
                    .ToDictionary(
                        static component => component.Key,
                        static component => component.Value);
                var componentIds = componentMap.Values
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static value => value, StringComparer.Ordinal)
                    .ToArray();
                return new EffectAcceptedTurnPlanner.EffectResourceTriggerExecution(
                    stamp.Identity.EffectId,
                    stamp.Identity.TriggerId,
                    stamp.Identity.EventKind,
                    stamp.Identity.EventRef,
                    appliedKeys
                        .OrderBy(static key => key.EventRef, StringComparer.Ordinal)
                        .ThenBy(static key => key.OriginKind, StringComparer.Ordinal)
                        .ThenBy(static key => key.OriginId, StringComparer.Ordinal)
                        .ToArray(),
                    stamp.UsesBefore,
                    componentIds,
                    stamp.Identity.TriggerEventRef,
                    componentMap);
            })
            .ToArray();
        var appliedComponentsByIdentity = resourceTriggerExecutions.ToDictionary(
            static execution => new EffectActivationCandidateIdentity(
                execution.EffectId,
                execution.TriggerId,
                execution.EventKind,
                execution.EventRef,
                execution.TriggerEventRef!),
            static execution => (execution.ComponentIds ?? Array.Empty<string>())
                .ToHashSet(StringComparer.Ordinal));
        var acceptedPendingResolutions = new List<
            AcceptedEffectBoundedResourceResolution>();
        var acceptedReactionExecutions = effectBoundaryTranscript
            .ReleasedReactions
            .OrderBy(static released => released.MechanicsOrdinal)
            .Select(static released => released.Reaction)
            .ToList();
        var acceptedResolvedPendingRequestIds = new HashSet<string>(
            StringComparer.Ordinal);
        var orderedAcceptedCandidates = acceptedCandidates
            .OrderBy(static accepted =>
                accepted.Activation.Stamp.ActivationOrdinal)
            .ToArray();
        var transcriptPrefixFingerprints =
            effectBoundaryTranscript.CreateActivationPrefixFingerprints();
        metrics.PendingTranscriptPrefixStampVisitCount +=
            transcriptPrefixFingerprints.Count;
        foreach (var accepted in orderedAcceptedCandidates)
        {
            var stamp = accepted.Activation.Stamp;
            var candidate = accepted.Candidate;
            var appliedComponents = appliedComponentsByIdentity[stamp.Identity];
            var pendingOutputs = candidate.PendingOutputs;
            var publishesUnresolvedPending = effectBoundaryTranscript
                    .PendingFrontierBoundaryOrdinal is not { } frontierOrdinal ||
                acceptedBoundaryByIdentity[stamp.Identity].BoundaryOrdinal ==
                    frontierOrdinal;
            string? candidateFingerprint = null;
            if (pendingOutputs.Count != 0)
            {
                if (!candidateAuthorities.Authorities.TryGetValue(
                        stamp.Identity,
                        out var candidateAuthority))
                {
                    yield return ResourceExecutionStep.Finished(Failure(
                        Issue(
                            "resource_pending_candidate_authority_missing",
                            "one prevalidated immutable authority for every pending candidate",
                            DescribeActivation(candidate.Activation.Identity)),
                        Statistics(workingHistory, metrics)));
                    yield break;
                }
                candidateFingerprint = candidateAuthority.CandidateFingerprint;
            }
            var transcriptPrefixFingerprint =
                transcriptPrefixFingerprints[stamp.ActivationOrdinal];
            foreach (var pending in pendingOutputs)
            {
                var hasResolvedBinding =
                    candidate.TryResolvePendingBinding(
                        pending.ComponentId,
                        out var resolvedBinding);
                var causalAuthority = CreatePendingCausalAuthority(
                    candidate,
                    stamp,
                    pending,
                    candidateFingerprint!,
                    transcriptPrefixFingerprint,
                    hasResolvedBinding
                        ? resolvedBinding.RequestAuthority.CausalAuthority
                            .WaveOrdinal
                        : candidate.PendingWaveOrdinal);
                if (hasResolvedBinding)
                {
                    if (!PendingCausalAuthorityMatches(
                            resolvedBinding.RequestAuthority.CausalAuthority,
                            causalAuthority,
                            pending.EffectAuthority))
                    {
                        yield return ResourceExecutionStep.Finished(Failure(
                            Issue(
                                "resource_pending_causal_replay_mismatch",
                                "the exact accepted activation transcript stamp for the terminal binding",
                                resolvedBinding.RequestId),
                            Statistics(workingHistory, metrics)));
                        yield break;
                    }
                    if (!acceptedResolvedPendingRequestIds.Add(
                            resolvedBinding.RequestId))
                    {
                        yield return ResourceExecutionStep.Finished(Failure(
                            Issue(
                                "resource_pending_causal_replay_duplicate",
                                "one causal acceptance per terminal binding",
                                resolvedBinding.RequestId),
                            Statistics(workingHistory, metrics)));
                        yield break;
                    }
                }
                if (pending.AfterComponentId is { } predecessorId &&
                    !appliedComponents.Contains(predecessorId))
                {
                    continue;
                }
                if (hasResolvedBinding)
                    continue;
                if (!publishesUnresolvedPending)
                    continue;
                acceptedPendingResolutions.Add(
                    new AcceptedEffectBoundedResourceResolution(
                        pending,
                        causalAuthority));
            }
        }

        yield return ResourceExecutionStep.Finished(new AcceptedMechanicsResourcePlanningResult(
            stateAfterImage,
            frozen.History,
            events,
            appliedTransitions,
            replayTransitions,
            Array.Empty<ValidationIssue>(),
            Statistics(workingHistory, metrics),
            resourceTriggerExecutions,
            acceptedPendingResolutions,
            acceptedReactionExecutions,
            acceptedResolvedPendingRequestIds.ToArray(),
            effectBoundaryTranscript));
        yield break;
    }

    private static CompleteResourceGraphPreparation PrepareCompleteResourceGraph(
        AcceptedMechanicsResourceInput input,
        IReadOnlyList<PreparedMutation> initialMutations,
        AcceptedMechanicsIdentityFactory identityFactory,
        AllocatedIdentityRegistry identityRegistry)
    {
        var initialGraph = BuildGraph(initialMutations);
        if (!initialGraph.IsValid)
            return CompleteGraphFailure(initialGraph.Issues);

        var prepared = initialMutations.ToList();
        var preparedByKey = prepared.ToDictionary(static value => value.Intent.Key);
        var preparedByOperationId = prepared.ToDictionary(
            static value => value.OperationId,
            StringComparer.Ordinal);
        var triggerCandidates = input.InitialTriggerCandidates.ToList();
        var triggerCandidatesByIdentity = new Dictionary<
            EffectActivationCandidateIdentity,
            EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>();
        var triggerCandidatesByMutation = new Dictionary<
            ResourceOperationKey,
            EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>();
        foreach (var candidate in input.InitialTriggerCandidates)
        {
            if (candidate.Producer != null ||
                !triggerCandidatesByIdentity.TryAdd(
                    candidate.Activation.Identity,
                    candidate))
            {
                return CompleteGraphFailure(Issue(
                    "effect_resource_trigger_execution_invalid",
                    "one unique lifecycle candidate without a resource producer",
                    DescribeActivation(candidate.Activation.Identity)));
            }
            foreach (var key in candidate.PlannedMutationKeys)
            {
                if (!preparedByKey.ContainsKey(key) ||
                    !triggerCandidatesByMutation.TryAdd(key, candidate))
                {
                    return CompleteGraphFailure(Issue(
                        "effect_resource_trigger_execution_invalid",
                        "one exact lifecycle candidate for every planned mutation",
                        Describe(key)));
                }
            }
        }

        var operationLineages = new Dictionary<
            string,
            Dictionary<string, int>>(StringComparer.Ordinal);
        foreach (var node in initialGraph.Graph!.OrderedNodes)
        {
            var mutation = preparedByOperationId[node.OperationId];
            var lineage = MergeParentLineages(mutation, operationLineages);
            if (triggerCandidatesByMutation.TryGetValue(
                    mutation.Intent.Key,
                    out var candidate))
            {
                var lineageDecision = AdvanceTriggerLineage(lineage, candidate);
                if (lineageDecision.Issues.Count != 0)
                    return CompleteGraphFailure(lineageDecision.Issues);
                lineage = lineageDecision.Lineage;
            }
            operationLineages.Add(mutation.OperationId, lineage);
        }

        if (input.EventMutationResolver == null)
        {
            return new CompleteResourceGraphPreparation(
                prepared,
                initialGraph.Graph,
                triggerCandidates,
                triggerCandidatesByMutation,
                input.Sources.Exports,
                Array.Empty<ValidationIssue>(),
                ResourceGraphExpansionWork.Empty);
        }

        var sourceSnapshot = input.Sources.Exports;
        var sourceExports = sourceSnapshot.ToDictionary(
            static source => (source.SourceKind, source.SourceId));
        var sourceAliases = sourceSnapshot
            .Select(static source =>
                source.SourceKind + "\0" +
                ResourceMaterializationContract.BuildConfusableKey(source.SourceId))
            .ToHashSet(StringComparer.Ordinal);
        var effectTriggerIndexLookupCount = 0;
        long effectTriggerCandidateVisitCount = 0;
        var effectSourceBindingIndexLookupCount = 0;
        long effectSourceBindingCandidateVisitCount = 0;
        long effectRoutingDescriptorAccessCount = 0;
        long effectOccurrenceCloneCount = 0;
        long effectFullValidationPassCount = 0;
        long effectTriggerArrayVisitCount = 0;
        long effectComponentIndexLookupCount = 0;
        long effectSelectedComponentVisitCount = 0;
        long pendingCandidateFingerprintOutputVisitCount = 0;
        long pendingProjectionDependencyVisitCount = 0;
        var sourceAuthorityAddVisitCount = 0;
        var sourceAuthorityResolveLookupCount = 0;
        var sourceAuthorityFreezeCount = 0;

        ResourceGraphExpansionWork SnapshotWork() =>
            new(
                effectTriggerIndexLookupCount,
                effectTriggerCandidateVisitCount,
                effectSourceBindingIndexLookupCount,
                effectSourceBindingCandidateVisitCount,
                effectRoutingDescriptorAccessCount,
                effectOccurrenceCloneCount,
                effectFullValidationPassCount,
                effectTriggerArrayVisitCount,
                effectComponentIndexLookupCount,
                effectSelectedComponentVisitCount,
                pendingCandidateFingerprintOutputVisitCount,
                pendingProjectionDependencyVisitCount,
                sourceSnapshot.Count,
                sourceAuthorityAddVisitCount,
                sourceAuthorityResolveLookupCount,
                sourceAuthorityFreezeCount);

        CompleteResourceGraphPreparation FailureWithWork(
            IEnumerable<ValidationIssue> failureIssues) =>
            CompleteGraphFailure(failureIssues, SnapshotWork());

        ResourceMutationSourceResolution ResolveWorkingSource(
            ResourceMutationSourceRequest request,
            ResourceDefinition definition,
            ResourceCoordinate? target)
        {
            sourceAuthorityResolveLookupCount++;
            sourceExports.TryGetValue(
                (request.SourceKind, request.SourceId),
                out var source);
            return ResourceMutationSourceCatalog.ResolveExport(
                request,
                definition,
                target,
                source);
        }

        var frontier = new Queue<PreparedMutation>(
            initialGraph.Graph.OrderedNodes.Select(node =>
                preparedByOperationId[node.OperationId]));
        while (frontier.Count != 0)
        {
            var producer = frontier.Dequeue();
            triggerCandidatesByMutation.TryGetValue(
                producer.Intent.Key,
                out var producerCandidate);
            foreach (var eventKind in PotentialResourceEventKinds(
                         producer.Intent,
                         producerCandidate))
            {
                var potentialEvent = new ResourceAppliedEvent(
                    eventKind,
                    producer.OperationId,
                    producer.Intent.EventRef,
                    producer.Intent.Coordinate,
                    Before: 0m,
                    After: 0m,
                    AppliedAmount: 0m,
                    Turn: input.Turn,
                    ExecutionSequence: 0,
                    SourceFingerprint: producer.Route.SourceEvidence.AuthorityFingerprint);
                var expansion = input.EventMutationResolver(
                    potentialEvent,
                    producer.Intent.Key);
                effectTriggerIndexLookupCount += expansion.Work.IndexLookupCount;
                effectTriggerCandidateVisitCount += expansion.Work.CandidateVisitCount;
                effectSourceBindingIndexLookupCount +=
                    expansion.Work.SourceBindingIndexLookupCount;
                effectSourceBindingCandidateVisitCount +=
                    expansion.Work.SourceBindingCandidateVisitCount;
                effectRoutingDescriptorAccessCount +=
                    expansion.Work.RoutingDescriptorAccessCount;
                effectOccurrenceCloneCount += expansion.Work.OccurrenceCloneCount;
                effectFullValidationPassCount +=
                    expansion.Work.FullEffectValidationPassCount;
                effectTriggerArrayVisitCount +=
                    expansion.Work.TriggerArrayVisitCount;
                effectComponentIndexLookupCount +=
                    expansion.Work.ComponentIndexLookupCount;
                effectSelectedComponentVisitCount +=
                    expansion.Work.SelectedComponentVisitCount;
                pendingCandidateFingerprintOutputVisitCount +=
                    expansion.Work
                        .PendingCandidateFingerprintOutputVisitCount;
                pendingProjectionDependencyVisitCount +=
                    expansion.Work.PendingProjectionDependencyVisitCount;
                if (!expansion.IsValid)
                    return FailureWithWork(expansion.Issues);

                var expansionKeys = new HashSet<ResourceOperationKey>();
                foreach (var mutation in expansion.Mutations)
                {
                    if (!expansionKeys.Add(mutation.Key) ||
                        preparedByKey.ContainsKey(mutation.Key))
                    {
                        return FailureWithWork(Issue(
                            "resource_planner_duplicate_operation",
                            "one mutation per exact replay key",
                            Describe(mutation.Key)));
                    }
                    if (!mutation.EventRequirements.Any(requirement =>
                            requirement.Producer == producer.Intent.Key &&
                            string.Equals(
                                requirement.EventKind,
                                eventKind,
                                StringComparison.Ordinal)))
                    {
                        return FailureWithWork(Issue(
                            "effect_resource_trigger_execution_invalid",
                            "every expanded mutation bound to its exact producer event",
                            Describe(mutation.Key)));
                    }
                }

                var candidateByMutation = new Dictionary<
                    ResourceOperationKey,
                    EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>();
                var lineageByCandidate = new Dictionary<
                    EffectActivationCandidateIdentity,
                    TriggerLineageDecision>();
                foreach (var candidate in expansion.TriggerCandidates)
                {
                    if (candidate.Producer != producer.Intent.Key ||
                        !string.Equals(
                            candidate.Activation.Identity.EventKind,
                            eventKind,
                            StringComparison.Ordinal) ||
                        !string.Equals(
                            candidate.Activation.Identity.TriggerEventRef,
                            producer.Intent.EventRef,
                            StringComparison.Ordinal) ||
                        !triggerCandidatesByIdentity.TryAdd(
                            candidate.Activation.Identity,
                            candidate))
                    {
                        return FailureWithWork(Issue(
                            "effect_resource_trigger_execution_invalid",
                            "one unique candidate bound to its exact producer event",
                            DescribeActivation(candidate.Activation.Identity)));
                    }

                    var decision = AdvanceTriggerLineage(
                        operationLineages[producer.OperationId],
                        candidate);
                    if (decision.Issues.Count != 0)
                        return FailureWithWork(decision.Issues);
                    lineageByCandidate.Add(candidate.Activation.Identity, decision);
                    triggerCandidates.Add(candidate);
                    foreach (var key in candidate.PlannedMutationKeys)
                    {
                        if (!expansionKeys.Contains(key) ||
                            !candidateByMutation.TryAdd(key, candidate))
                        {
                            return FailureWithWork(Issue(
                                "effect_resource_trigger_execution_invalid",
                                "one exact candidate per expanded mutation",
                                Describe(key)));
                        }
                    }
                }

                var missingActivation = expansion.Mutations.FirstOrDefault(mutation =>
                    !candidateByMutation.ContainsKey(mutation.Key));
                if (missingActivation != null)
                {
                    return FailureWithWork(Issue(
                        "effect_resource_trigger_execution_missing",
                        "one exact candidate for every expanded mutation",
                        Describe(missingActivation.Key)));
                }
                if (preparedByKey.Count + expansion.Mutations.Count >
                    ResourceMaterializationContract.MaxTriggerNodes)
                {
                    return FailureWithWork(Issue(
                        "resource_graph_node_limit_exceeded",
                        $"at most {ResourceMaterializationContract.MaxTriggerNodes} graph nodes",
                        (preparedByKey.Count + expansion.Mutations.Count)
                            .ToString(System.Globalization.CultureInfo.InvariantCulture)));
                }

                foreach (var source in expansion.SourceExports)
                {
                    sourceAuthorityAddVisitCount++;
                    var sourceKey = (source.SourceKind, source.SourceId);
                    if (sourceExports.TryGetValue(sourceKey, out var existing))
                    {
                        if (existing != source)
                        {
                            return FailureWithWork(Issue(
                                "effect_resource_source_conflict",
                                "one exact source policy per effect/trigger/component",
                                source.SourceKind + "/" + source.SourceId));
                        }
                        continue;
                    }
                    var sourceIssues = ResourceMutationSourceCatalog.ValidateExport(source);
                    if (sourceIssues.Count != 0)
                        return FailureWithWork(sourceIssues);
                    var sourceAlias = source.SourceKind + "\0" +
                        ResourceMaterializationContract.BuildConfusableKey(
                            source.SourceId);
                    if (!sourceAliases.Add(sourceAlias))
                    {
                        return FailureWithWork(Issue(
                            "resource_source_duplicate_confusable",
                            "case/confusable-unique source authority per kind",
                            source.SourceKind + "/" + source.SourceId));
                    }
                    if (sourceExports.Count >=
                        ResourceMaterializationContract.MaxLiveEntries)
                    {
                        return FailureWithWork(Issue(
                            "resource_source_limit_exceeded",
                            $"at most {ResourceMaterializationContract.MaxLiveEntries} source exports",
                            (sourceExports.Count + 1).ToString(
                                System.Globalization.CultureInfo.InvariantCulture)));
                    }
                    sourceExports.Add(sourceKey, source);
                }

                if (expansion.Mutations.Count == 0)
                    continue;

                var additional = PrepareMutations(
                    new AcceptedMechanicsResourceInput(
                        input.Turn,
                        input.Definitions,
                        input.State,
                        input.History,
                        input.Sources,
                        expansion.Mutations),
                    identityFactory,
                    identityRegistry,
                    ResolveWorkingSource);
                if (additional.Issues.Count != 0)
                    return FailureWithWork(additional.Issues);
                foreach (var extra in additional.Mutations)
                {
                    var candidate = candidateByMutation[extra.Intent.Key];
                    var decision = lineageByCandidate[candidate.Activation.Identity];
                    prepared.Add(extra);
                    preparedByKey.Add(extra.Intent.Key, extra);
                    preparedByOperationId.Add(extra.OperationId, extra);
                    operationLineages.Add(
                        extra.OperationId,
                        new Dictionary<string, int>(decision.Lineage, StringComparer.Ordinal));
                    triggerCandidatesByMutation.Add(extra.Intent.Key, candidate);
                    if (decision.Allowed)
                        frontier.Enqueue(extra);
                }
            }
        }

        sourceAuthorityFreezeCount++;
        var frozenSources = ResourceMutationSourceCatalog.Create(sourceExports.Values);
        if (!frozenSources.IsValid || frozenSources.Catalog == null)
            return FailureWithWork(frozenSources.Issues);

        var graph = BuildGraph(prepared);
        var work = SnapshotWork();
        return graph.IsValid
            ? new CompleteResourceGraphPreparation(
                prepared,
                graph.Graph,
                triggerCandidates,
                triggerCandidatesByMutation,
                frozenSources.Catalog.Exports,
                Array.Empty<ValidationIssue>(),
                work)
            : FailureWithWork(graph.Issues);
    }

    private static Dictionary<string, int> MergeParentLineages(
        PreparedMutation mutation,
        IReadOnlyDictionary<string, Dictionary<string, int>> operationLineages)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var parentId in mutation.DependencyOperationIds.Values
                     .Distinct(StringComparer.Ordinal))
        {
            foreach (var pair in operationLineages[parentId])
            {
                result[pair.Key] = Math.Max(result.GetValueOrDefault(pair.Key), pair.Value);
            }
        }
        return result;
    }

    private static TriggerLineageDecision AdvanceTriggerLineage(
        IReadOnlyDictionary<string, int> parentLineage,
        EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate candidate)
    {
        var lineage = new Dictionary<string, int>(parentLineage, StringComparer.Ordinal);
        var exactActivationKey = "activation\0" +
            DescribeActivation(candidate.Activation.Identity, separator: "\0");
        if (lineage.ContainsKey(exactActivationKey))
        {
            return new TriggerLineageDecision(
                Allowed: true,
                lineage,
                Array.Empty<ValidationIssue>());
        }

        var activationKey = TriggerSemanticKey(candidate);
        if (lineage.TryGetValue(activationKey, out var priorActivations))
        {
            if (candidate.Activation.ConsumesUse &&
                candidate.UseSeed is { RemainingUses: > 0 } useSeed)
            {
                if (priorActivations >= useSeed.RemainingUses)
                {
                    return new TriggerLineageDecision(
                        Allowed: false,
                        lineage,
                        Array.Empty<ValidationIssue>());
                }
                lineage[activationKey] = priorActivations + 1;
                lineage.Add(exactActivationKey, 1);
                return new TriggerLineageDecision(
                    Allowed: true,
                    lineage,
                    Array.Empty<ValidationIssue>());
            }
            return new TriggerLineageDecision(
                Allowed: false,
                lineage,
                Issue(
                    "resource_graph_cycle",
                    "one acyclic effect/resource trigger lineage",
                    activationKey.Replace('\0', '/')));
        }
        lineage.Add(activationKey, 1);
        lineage.Add(exactActivationKey, 1);
        return new TriggerLineageDecision(
            Allowed: true,
            lineage,
            Array.Empty<ValidationIssue>());
    }

    private static IReadOnlyList<string> PotentialResourceEventKinds(
        ResourceMutationIntent mutation,
        EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate?
            producerCandidate)
    {
        if (producerCandidate?.Producer is { } parent &&
            parent.Coordinate == mutation.Coordinate &&
            ((string.Equals(
                  producerCandidate.Activation.Identity.EventKind,
                  "resource_filled",
                  StringComparison.Ordinal) &&
              mutation.Source.Operation is
                  ResourceOperation.Restore or ResourceOperation.Gain) ||
             (string.Equals(
                  producerCandidate.Activation.Identity.EventKind,
                  "resource_depleted",
                  StringComparison.Ordinal) &&
              mutation.Source.Operation is
                  ResourceOperation.Damage or ResourceOperation.Spend)))
        {
            return Array.Empty<string>();
        }

        return mutation.Source.Operation switch
        {
            ResourceOperation.Damage =>
                new[] { "resource_damaged", "resource_depleted" },
            ResourceOperation.Restore =>
                new[] { "resource_restored", "resource_filled" },
            ResourceOperation.Spend =>
                new[] { "resource_spent", "resource_depleted" },
            ResourceOperation.Gain =>
                new[] { "resource_gained", "resource_filled" },
            _ => throw new ArgumentOutOfRangeException(
                nameof(mutation),
                mutation.Source.Operation,
                null)
        };
    }

    private static string TriggerSemanticKey(
        EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate candidate) =>
        string.Join(
            "\0",
            candidate.Activation.Identity.EffectId,
            candidate.Activation.Identity.TriggerId,
            candidate.Activation.Identity.EventKind);

    private static CompleteResourceGraphPreparation CompleteGraphFailure(
        IEnumerable<ValidationIssue> issues,
        ResourceGraphExpansionWork? work = null) =>
        new(
            Array.Empty<PreparedMutation>(),
            null,
            Array.Empty<EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>(),
            new Dictionary<
                ResourceOperationKey,
                EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>(),
            Array.Empty<ResourceMutationSourceExport>(),
            issues.ToArray(),
            work ?? ResourceGraphExpansionWork.Empty);

    internal static EffectAcceptedPlanAuthorityStamp CreateEffectPlanAuthority(
        EffectAcceptedTurnPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new EffectAcceptedPlanAuthorityStamp(
            plan.InputFingerprint,
            plan.CarrierAuthorityFingerprint,
            plan.SourceAuthorityFingerprint,
            plan.TargetAuthorityFingerprint,
            plan.SkillScopeAuthority?.Fingerprint ?? "none");
    }

    private static string DescribeActivation(
        EffectActivationCandidateIdentity identity,
        string separator = "/") =>
        string.Join(
            separator,
            identity.EffectId,
            identity.TriggerId,
            identity.EventKind,
            identity.EventRef,
            identity.TriggerEventRef);

    private static ResourcePendingCausalAuthority CreatePendingCausalAuthority(
        EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate candidate,
        AcceptedEffectActivationTranscriptStamp stamp,
        EffectAcceptedTurnPlanner.EffectBoundedResourceResolution resolution,
        string candidateFingerprint,
        string transcriptPrefixFingerprint,
        int waveOrdinal) =>
        new(
            stamp.Identity.EffectId,
            stamp.Identity.TriggerId,
            stamp.Identity.EventRef,
            stamp.Identity.TriggerEventRef,
            candidate.Producer == null
                ? null
                : CreateStableProducerOperationKey(candidate.Producer),
            stamp.Priority,
            stamp.ActivationOrdinal,
            stamp.ConsumesUse,
            stamp.UsesBefore,
            resolution.ComponentId,
            resolution.AfterComponentId,
            candidateFingerprint,
            transcriptPrefixFingerprint,
            waveOrdinal);

    internal static string CreateStableProducerOperationKey(
        ResourceOperationKey producer)
    {
        using var builder = new ResourceFingerprintBuilder(
            "resource-producer-operation-key-v3");
        AppendResourceOperationKey(builder, producer);
        var fingerprint = builder.Build();
        return "resource_operation_key_" + fingerprint["sha256:".Length..];
    }

    private static PendingCandidateAuthorityValidationResult
        ValidatePendingCandidateAuthorities(
            IReadOnlyList<
                EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>
                candidates,
            IReadOnlyList<PreparedMutation> mutations,
            IReadOnlyList<ResourceMutationSourceExport> sources,
            PlannerWorkMetrics metrics)
    {
        var issues = new List<ValidationIssue>();
        var mutationsByKey = new Dictionary<
            ResourceOperationKey,
            ResourceMutationIntent>();
        foreach (var prepared in mutations)
        {
            if (!mutationsByKey.TryAdd(prepared.Intent.Key, prepared.Intent))
            {
                issues.AddRange(Issue(
                    "resource_planner_duplicate_operation",
                    "one mutation per exact replay key",
                    Describe(prepared.Intent.Key)));
            }
        }

        var sourcesByKey = new Dictionary<
            (string SourceKind, string SourceId),
            ResourceMutationSourceExport>();
        foreach (var source in sources)
        {
            if (!sourcesByKey.TryAdd(
                    (source.SourceKind, source.SourceId),
                    source))
            {
                issues.AddRange(Issue(
                    "resource_source_duplicate_exact",
                    "one exact source export per source kind and identity",
                    source.SourceKind + "/" + source.SourceId));
            }
        }

        var authorities = new Dictionary<
            EffectActivationCandidateIdentity,
            ValidatedPendingCandidateAuthority>();
        if (issues.Count != 0)
        {
            return new PendingCandidateAuthorityValidationResult(
                authorities,
                issues);
        }

        foreach (var candidate in candidates)
        {
            var pendingOutputs = candidate.PendingOutputs;
            if (pendingOutputs.Count == 0)
                continue;

            var material = ResolveCandidateMaterialFingerprint(
                candidate,
                mutationsByKey,
                sourcesByKey,
                metrics);
            if (!material.IsValid)
            {
                issues.AddRange(material.Issues);
                continue;
            }
            if (candidate.CausalMaterialFingerprint != null &&
                !string.Equals(
                    candidate.CausalMaterialFingerprint,
                    material.Fingerprint,
                    StringComparison.Ordinal))
            {
                issues.AddRange(Issue(
                    "resource_pending_candidate_origin_mismatch",
                    "the cached causal material fingerprint",
                    DescribeActivation(candidate.Activation.Identity)));
                continue;
            }

            var candidateFingerprint = CreateCandidateFingerprint(
                candidate,
                material.Fingerprint!,
                pendingOutputs,
                candidate.ReactionOutputs,
                metrics);
            if (candidate.CandidateFingerprint != null &&
                !string.Equals(
                    candidate.CandidateFingerprint,
                    candidateFingerprint,
                    StringComparison.Ordinal))
            {
                issues.AddRange(Issue(
                    "resource_pending_candidate_origin_mismatch",
                    "the cached complete candidate fingerprint",
                    DescribeActivation(candidate.Activation.Identity)));
                continue;
            }
            if (!authorities.TryAdd(
                    candidate.Activation.Identity,
                    new ValidatedPendingCandidateAuthority(
                        material.Fingerprint!,
                        candidateFingerprint)))
            {
                issues.AddRange(Issue(
                    "resource_pending_candidate_authority_duplicate",
                    "one prevalidated immutable authority per pending candidate",
                    DescribeActivation(candidate.Activation.Identity)));
            }
        }

        return new PendingCandidateAuthorityValidationResult(
            authorities,
            issues);
    }

    internal static string CreateCandidateFingerprint(
        EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var material = ResolveCandidateMaterialFingerprint(candidate);
        if (!material.IsValid)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                material.Issues.Select(static issue =>
                    issue.Code + ": expected=" + issue.Expected +
                    ";actual=" + issue.Actual)));
        }
        return CreateCandidateFingerprint(
            candidate,
            material.Fingerprint!,
            candidate.PendingOutputs,
            candidate.ReactionOutputs,
            metrics: null);
    }

    private static string CreateCandidateFingerprint(
        EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate candidate,
        string causalMaterialFingerprint,
        IReadOnlyList<
            EffectAcceptedTurnPlanner.EffectBoundedResourceResolution>
            pendingOutputs,
        IReadOnlyList<EffectReactionExecution> reactionOutputs,
        PlannerWorkMetrics? metrics)
    {
        using var builder = new ResourceFingerprintBuilder(
            "effect-activation-candidate-v6");
        AppendReplayStableActivationIdentity(
            builder,
            candidate.Activation.Identity,
            candidate.EffectAuthority);
        builder.Append(candidate.Activation.Priority);
        builder.Append(candidate.Activation.ConsumesUse);
        builder.Append(candidate.UseSeed != null);
        if (candidate.UseSeed != null)
        {
            AppendReplayStableEffectIdentity(
                builder,
                candidate.UseSeed.EffectId,
                candidate.EffectAuthority);
            builder.Append(candidate.UseSeed.RemainingUses);
        }
        builder.Append(candidate.Producer != null);
        if (candidate.Producer != null)
            AppendResourceOperationKey(builder, candidate.Producer);
        builder.Append(causalMaterialFingerprint);

        var pendingFingerprints = new string[pendingOutputs.Count];
        for (var index = 0; index < pendingOutputs.Count; index++)
        {
            pendingFingerprints[index] =
                CreatePendingCandidateOutputFingerprint(pendingOutputs[index]);
            if (metrics != null)
                metrics.PendingCandidateFingerprintOutputVisitCount++;
        }
        AppendOrderedFingerprints(
            builder,
            "pending-outputs",
            pendingFingerprints);
        AppendOrderedFingerprints(
            builder,
            "reaction-outputs",
            reactionOutputs.Select(output =>
                CreateReplayStableReactionCandidateOutputFingerprint(
                    output,
                    candidate.Activation.Identity.EffectId,
                    candidate.EffectAuthority)));
        return builder.Build();
    }

    private static CandidateMaterialFingerprintResult
        ResolveCandidateMaterialFingerprint(
            EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate candidate,
            IReadOnlyDictionary<ResourceOperationKey, ResourceMutationIntent>?
                actualMutationsByKey = null,
        IReadOnlyDictionary<
                (string SourceKind, string SourceId),
                ResourceMutationSourceExport>? actualSourcesByKey = null,
            PlannerWorkMetrics? metrics = null)
    {
        var issues = new List<ValidationIssue>();
        var origin = candidate.Origin;
        var originMutations = new Dictionary<
            ResourceOperationKey,
            ResourceMutationIntent>();
        foreach (var mutation in origin.Mutations)
        {
            if (!originMutations.TryAdd(mutation.Key, mutation))
            {
                issues.AddRange(Issue(
                    "resource_pending_candidate_origin_duplicate",
                    "one exact origin mutation per operation key",
                    Describe(mutation.Key)));
            }
        }

        var originSources = new Dictionary<
            (string SourceKind, string SourceId),
            ResourceMutationSourceExport>();
        foreach (var source in origin.SourceExports)
        {
            var sourceKey = (source.SourceKind, source.SourceId);
            if (!originSources.TryAdd(sourceKey, source))
            {
                issues.AddRange(Issue(
                    "resource_pending_candidate_origin_duplicate",
                    "one exact referenced source export per source identity",
                    source.SourceKind + "/" + source.SourceId));
            }
        }

        var originMutationKeys = originMutations.Keys.ToHashSet();
        var originMapKeys = origin.ComponentIdsByMutation.Keys.ToHashSet();
        if (!originMutationKeys.SetEquals(originMapKeys))
        {
            issues.AddRange(Issue(
                "resource_pending_candidate_origin_mismatch",
                "origin mutation keys equal origin component-map keys",
                DescribeActivation(candidate.Activation.Identity)));
        }
        var mappedComponentIds = origin.ComponentIdsByMutation.Values
            .ToHashSet(StringComparer.Ordinal);
        if (!mappedComponentIds.SetEquals(origin.ComponentIds) ||
            origin.ComponentIds.Count != mappedComponentIds.Count)
        {
            issues.AddRange(Issue(
                "resource_pending_candidate_origin_mismatch",
                "origin component ids equal the distinct origin component-map values",
                DescribeActivation(candidate.Activation.Identity)));
        }

        var referencedSources = originMutations.Values
            .Select(static mutation => (
                mutation.Source.SourceKind,
                mutation.Source.SourceId))
            .ToHashSet();
        if (!referencedSources.SetEquals(originSources.Keys))
        {
            issues.AddRange(Issue(
                "resource_pending_candidate_origin_mismatch",
                "exactly the source exports referenced by origin mutations",
                DescribeActivation(candidate.Activation.Identity)));
        }

        var runtimeMutationKeys = candidate.PlannedMutationKeys.ToHashSet();
        var runtimeMapKeys = candidate.PlannedComponentIdsByMutation.Keys
            .ToHashSet();
        var runtimeMappedComponentIds = candidate
            .PlannedComponentIdsByMutation.Values
            .ToHashSet(StringComparer.Ordinal);
        if (!runtimeMutationKeys.SetEquals(runtimeMapKeys) ||
            !runtimeMappedComponentIds.SetEquals(
                candidate.PlannedComponentIds) ||
            candidate.PlannedComponentIds.Count !=
                runtimeMappedComponentIds.Count)
        {
            issues.AddRange(Issue(
                "resource_pending_candidate_origin_mismatch",
                "one exact runtime component binding per planned mutation",
                DescribeActivation(candidate.Activation.Identity)));
        }
        if (!originMutationKeys.IsSubsetOf(runtimeMutationKeys) ||
            candidate.CandidateFingerprint == null &&
            !originMutationKeys.SetEquals(runtimeMutationKeys))
        {
            issues.AddRange(Issue(
                "resource_pending_candidate_origin_mismatch",
                "an unbound exact origin plan or a bound receipt-only extension",
                DescribeActivation(candidate.Activation.Identity)));
        }
        if (candidate.CandidateFingerprint != null)
        {
            var resolvedByComponent = new Dictionary<
                string,
                ResourcePendingResolvedBinding>(StringComparer.Ordinal);
            var pendingOutputs = candidate.PendingOutputs;
            foreach (var output in pendingOutputs)
            {
                if (candidate.TryResolvePendingBinding(
                        output.ComponentId,
                        out var binding))
                {
                    resolvedByComponent.TryAdd(output.ComponentId, binding);
                }
            }
            var projectionIssues = new List<ValidationIssue>();
            var expectedProjections = ProjectResolvedPendingMutations(
                candidate,
                pendingOutputs,
                resolvedByComponent,
                projectionIssues,
                metrics);
            issues.AddRange(projectionIssues);
            var expectedByKey = expectedProjections.ToDictionary(
                static projection => projection.Mutation.Key);
            var extraKeys = runtimeMutationKeys
                .Except(originMutationKeys)
                .ToHashSet();
            if (!extraKeys.SetEquals(expectedByKey.Keys))
            {
                issues.AddRange(Issue(
                    "resource_pending_candidate_origin_mismatch",
                    "exactly the terminal-receipt projections extend a bound origin plan",
                    DescribeActivation(candidate.Activation.Identity)));
            }
            foreach (var expected in expectedProjections)
            {
                var changedProjectionFields = new List<string>();
                if (!candidate.PlannedComponentIdsByMutation.TryGetValue(
                        expected.Mutation.Key,
                        out var runtimeComponentId) ||
                    !string.Equals(
                        runtimeComponentId,
                        expected.ComponentId,
                        StringComparison.Ordinal))
                {
                    changedProjectionFields.Add("ComponentBinding");
                }
                if (actualMutationsByKey != null || actualSourcesByKey != null)
                {
                    ResourceMutationIntent? actualMutation = null;
                    if (actualMutationsByKey == null ||
                        !actualMutationsByKey.TryGetValue(
                            expected.Mutation.Key,
                            out actualMutation))
                    {
                        changedProjectionFields.Add("RuntimeMutation");
                    }
                    ResourceMutationSourceExport? actualSource = null;
                    if (actualMutation != null &&
                        (actualSourcesByKey == null ||
                         !actualSourcesByKey.TryGetValue(
                             (
                                 actualMutation.Source.SourceKind,
                                 actualMutation.Source.SourceId),
                             out actualSource)))
                    {
                        changedProjectionFields.Add("RuntimeSource");
                    }
                    if (actualMutation != null && actualSource != null &&
                        !string.Equals(
                            CreateMutationIntentFingerprint(
                                actualMutation,
                                actualSource),
                            CreateMutationIntentFingerprint(
                                expected.Mutation,
                                expected.Source),
                            StringComparison.Ordinal))
                    {
                        changedProjectionFields.AddRange(
                            DescribeMutationIntentDifference(
                                expected.Mutation,
                                expected.Source,
                                actualMutation,
                                actualSource));
                    }
                }
                if (changedProjectionFields.Count != 0)
                {
                    issues.AddRange(Issue(
                        "resource_pending_candidate_origin_mismatch",
                        "only an exact terminal-receipt projection may extend a bound origin plan",
                        Describe(expected.Mutation.Key) + ";changed=" +
                        string.Join(",", changedProjectionFields.Distinct(
                            StringComparer.Ordinal))));
                }
            }
        }
        foreach (var originComponent in origin.ComponentIdsByMutation)
        {
            if (!candidate.PlannedComponentIdsByMutation.TryGetValue(
                    originComponent.Key,
                    out var runtimeComponentId) ||
                !string.Equals(
                    runtimeComponentId,
                    originComponent.Value,
                    StringComparison.Ordinal))
            {
                issues.AddRange(Issue(
                    "resource_pending_candidate_origin_mismatch",
                    "the immutable origin mutation-to-component binding",
                    Describe(originComponent.Key)));
            }
        }
        if (candidate.CandidateFingerprint == null &&
            (candidate.PlannedComponentIdsByMutation.Count !=
                 origin.ComponentIdsByMutation.Count ||
             !candidate.PlannedComponentIds.ToHashSet(StringComparer.Ordinal)
                 .SetEquals(origin.ComponentIds)))
        {
            issues.AddRange(Issue(
                "resource_pending_candidate_origin_mismatch",
                "the exact unbound origin component plan",
                DescribeActivation(candidate.Activation.Identity)));
        }

        if (issues.Count != 0)
            return new CandidateMaterialFingerprintResult(null, issues);

        var originFingerprint = CreateCandidateMaterialFingerprint(
            originMutations.Values,
            origin.ComponentIds,
            origin.ComponentIdsByMutation,
            originSources);
        if (actualMutationsByKey != null && actualSourcesByKey != null)
        {
            var actualMutations = new List<ResourceMutationIntent>();
            var actualSources = new Dictionary<
                (string SourceKind, string SourceId),
                ResourceMutationSourceExport>();
            foreach (var originMutation in originMutations.Values)
            {
                if (!actualMutationsByKey.TryGetValue(
                        originMutation.Key,
                        out var actualMutation) ||
                    !actualSourcesByKey.TryGetValue(
                        (
                            originMutation.Source.SourceKind,
                            originMutation.Source.SourceId),
                        out var actualSource))
                {
                    issues.AddRange(Issue(
                        "resource_pending_candidate_origin_mismatch",
                        "the exact origin mutation and source in the runtime graph",
                        Describe(originMutation.Key)));
                    continue;
                }
                actualMutations.Add(actualMutation);
                actualSources.TryAdd(
                    (actualSource.SourceKind, actualSource.SourceId),
                    actualSource);
            }
            if (issues.Count == 0)
            {
                var actualFingerprint = CreateCandidateMaterialFingerprint(
                    actualMutations,
                    origin.ComponentIds,
                    origin.ComponentIdsByMutation,
                    actualSources);
                if (!string.Equals(
                        actualFingerprint,
                        originFingerprint,
                        StringComparison.Ordinal))
                {
                    issues.AddRange(Issue(
                        "resource_pending_candidate_origin_mismatch",
                        "runtime mutation and source semantics equal the immutable origin payload",
                        DescribeActivation(candidate.Activation.Identity)));
                }
            }
        }

        return issues.Count == 0
            ? new CandidateMaterialFingerprintResult(
                originFingerprint,
                Array.Empty<ValidationIssue>())
            : new CandidateMaterialFingerprintResult(null, issues);
    }

    private static string CreateCandidateMaterialFingerprint(
        IEnumerable<ResourceMutationIntent> mutations,
        IEnumerable<string> componentIds,
        IReadOnlyDictionary<ResourceOperationKey, string> componentMap,
        IReadOnlyDictionary<
            (string SourceKind, string SourceId),
            ResourceMutationSourceExport> sources)
    {
        using var builder = new ResourceFingerprintBuilder(
            "effect-activation-candidate-origin-v1");
        AppendOrderedFingerprints(
            builder,
            "mutation-semantics",
            mutations.Select(mutation => CreateMutationIntentFingerprint(
                mutation,
                sources[(
                    mutation.Source.SourceKind,
                    mutation.Source.SourceId)])));
        AppendOrderedStrings(builder, "component-ids", componentIds);
        AppendOrderedFingerprints(
            builder,
            "component-map",
            componentMap.Select(static pair =>
                CreateComponentMapFingerprint(pair.Key, pair.Value)));
        return builder.Build();
    }

    private static string CreateMutationIntentFingerprint(
        ResourceMutationIntent mutation,
        ResourceMutationSourceExport source)
    {
        using var builder = new ResourceFingerprintBuilder(
            "effect-activation-origin-mutation-v1");
        AppendResourceOperationKey(builder, mutation.Key);
        builder.Append(mutation.Amount);
        AppendOrderedFingerprints(
            builder,
            "dependencies",
            mutation.Dependencies.Select(CreateResourceOperationKeyFingerprint));
        AppendOrderedFingerprints(
            builder,
            "event-requirements",
            mutation.EventRequirements.Select(
                CreateEventRequirementFingerprint));
        AppendNullableString(builder, mutation.ReceiptId);
        builder.Append(mutation.DerivedAmount != null);
        if (mutation.DerivedAmount != null)
            builder.Append(mutation.DerivedAmount.RecoveryPercent);
        builder.Append(mutation.ResultConstraint != null);
        if (mutation.ResultConstraint != null)
        {
            AppendNullableDecimal(builder, mutation.ResultConstraint.RejectBelow);
            AppendNullableDecimal(builder, mutation.ResultConstraint.RejectAbove);
        }
        builder.Append(source.SourceKind);
        builder.Append(source.SourceId);
        builder.Append(source.AuthorityFingerprint);
        builder.Append(source.State.ToString());
        builder.Append(source.SameTurn);
        builder.Append(source.BoundOwner != null);
        if (source.BoundOwner != null)
        {
            builder.Append(source.BoundOwner.Realm);
            builder.Append(ResourceDefinitionCatalog.GetOwnerKindToken(
                source.BoundOwner.OwnerKind));
            builder.Append(source.BoundOwner.ResourceOwnerId);
        }
        return builder.Build();
    }

    private static IReadOnlyList<string> DescribeMutationIntentDifference(
        ResourceMutationIntent expectedMutation,
        ResourceMutationSourceExport expectedSource,
        ResourceMutationIntent actualMutation,
        ResourceMutationSourceExport actualSource)
    {
        var changed = new List<string>();
        if (expectedMutation.Key != actualMutation.Key)
            changed.Add(nameof(ResourceMutationIntent.Key));
        if (expectedMutation.Amount != actualMutation.Amount)
            changed.Add(nameof(ResourceMutationIntent.Amount));
        if (!OrderedFingerprintsEqual(
                expectedMutation.Dependencies.Select(
                    CreateResourceOperationKeyFingerprint),
                actualMutation.Dependencies.Select(
                    CreateResourceOperationKeyFingerprint)))
        {
            changed.Add(nameof(ResourceMutationIntent.Dependencies));
        }
        if (!OrderedFingerprintsEqual(
                expectedMutation.EventRequirements.Select(
                    CreateEventRequirementFingerprint),
                actualMutation.EventRequirements.Select(
                    CreateEventRequirementFingerprint)))
        {
            changed.Add(nameof(ResourceMutationIntent.EventRequirements));
        }
        if (!string.Equals(
                expectedMutation.ReceiptId,
                actualMutation.ReceiptId,
                StringComparison.Ordinal))
        {
            changed.Add(nameof(ResourceMutationIntent.ReceiptId));
        }
        if (expectedMutation.DerivedAmount != actualMutation.DerivedAmount)
            changed.Add(nameof(ResourceMutationIntent.DerivedAmount));
        if (expectedMutation.ResultConstraint != actualMutation.ResultConstraint)
            changed.Add(nameof(ResourceMutationIntent.ResultConstraint));
        if (!string.Equals(
                expectedSource.SourceKind,
                actualSource.SourceKind,
                StringComparison.Ordinal))
        {
            changed.Add(nameof(ResourceMutationSourceExport.SourceKind));
        }
        if (!string.Equals(
                expectedSource.SourceId,
                actualSource.SourceId,
                StringComparison.Ordinal))
        {
            changed.Add(nameof(ResourceMutationSourceExport.SourceId));
        }
        if (!string.Equals(
                expectedSource.AuthorityFingerprint,
                actualSource.AuthorityFingerprint,
                StringComparison.Ordinal))
        {
            changed.Add(nameof(ResourceMutationSourceExport.AuthorityFingerprint));
        }
        if (expectedSource.State != actualSource.State)
            changed.Add(nameof(ResourceMutationSourceExport.State));
        if (expectedSource.SameTurn != actualSource.SameTurn)
            changed.Add(nameof(ResourceMutationSourceExport.SameTurn));
        if (expectedSource.BoundOwner != actualSource.BoundOwner)
            changed.Add(nameof(ResourceMutationSourceExport.BoundOwner));
        return changed.Count == 0
            ? new[] { "FingerprintDomain" }
            : changed;
    }

    private static bool OrderedFingerprintsEqual(
        IEnumerable<string> expected,
        IEnumerable<string> actual) =>
        expected.OrderBy(static value => value, StringComparer.Ordinal)
            .SequenceEqual(
                actual.OrderBy(static value => value, StringComparer.Ordinal),
                StringComparer.Ordinal);

    private static string CreatePendingCandidateOutputFingerprint(
        EffectAcceptedTurnPlanner.EffectBoundedResourceResolution output)
    {
        using var builder = new ResourceFingerprintBuilder(
            "effect-activation-pending-output-v2");
        builder.Append(output.EventRef);
        AppendReplayStableEffectIdentity(
            builder,
            output.EffectId,
            output.EffectAuthority);
        AppendAuthorityBinding(builder, output.EffectAuthority);
        builder.Append(CanonicalFingerprintJson(output.Source));
        AppendAuthorityBinding(builder, output.SourceAuthority);
        builder.Append(CanonicalFingerprintJson(output.Target));
        AppendAuthorityBinding(builder, output.TargetAuthority);
        builder.Append(output.TriggerId);
        builder.Append(output.ComponentId);
        builder.Append(output.TriggerEventRef);
        builder.Append(output.EventKind);
        AppendResourceCoordinate(builder, output.Coordinate);
        AppendAuthorityBinding(builder, output.ResourceAuthority);
        builder.Append(output.Operation.ToString());
        builder.Append(output.MinimumAmount);
        builder.Append(output.MaximumAmount);
        builder.Append(output.SourceAuthorityFingerprint);
        builder.Append(output.PolicyFingerprint);
        AppendOrderedFingerprints(
            builder,
            "dependencies",
            output.Dependencies.Select(CreateResourceOperationKeyFingerprint));
        AppendOrderedFingerprints(
            builder,
            "event-requirements",
            output.EventRequirements.Select(
                CreateEventRequirementFingerprint));
        builder.Append(output.ResultConstraint != null);
        if (output.ResultConstraint != null)
        {
            AppendNullableDecimal(
                builder,
                output.ResultConstraint.RejectBelow);
            AppendNullableDecimal(
                builder,
                output.ResultConstraint.RejectAbove);
        }
        AppendNullableInt(builder, output.RemainingUseBudget);
        builder.Append(output.SafeSourceLabel);
        builder.Append(output.SafeTargetLabel);
        builder.Append(output.SafeResourceLabel);
        builder.Append(output.SafeOperationLabel);
        AppendNullableString(builder, output.AfterComponentId);
        return builder.Build();
    }

    internal static string CreateReactionCandidateOutputFingerprint(
        EffectReactionExecution output) =>
        CreateReactionCandidateOutputFingerprintCore(
            output,
            currentEffectId: null,
            effectAuthority: null);

    internal static string CreateReplayStableReactionCandidateOutputFingerprint(
        EffectReactionExecution output,
        string currentEffectId,
        ResourcePendingAuthorityBinding effectAuthority) =>
        CreateReactionCandidateOutputFingerprintCore(
            output,
            currentEffectId,
            effectAuthority);

    private static string CreateReactionCandidateOutputFingerprintCore(
        EffectReactionExecution output,
        string? currentEffectId,
        ResourcePendingAuthorityBinding? effectAuthority)
    {
        using var builder = new ResourceFingerprintBuilder(
            effectAuthority == null
                ? "effect-activation-reaction-output-v4"
                : "effect-activation-reaction-output-rebind-v2");
        builder.Append(output.EventRef);
        builder.Append(output.TriggerEventRef);
        builder.Append(output.CausalEventRef);
        builder.Append(output.Turn);
        builder.Append(output.EventKind);
        builder.Append(output.Target.Realm);
        builder.Append(output.Target.Kind);
        builder.Append(output.Target.TargetId);
        if (effectAuthority == null || currentEffectId == null)
        {
            builder.Append(output.EffectId);
        }
        else
        {
            AppendReplayStableEffectIdentity(
                builder,
                output.EffectId,
                effectAuthority);
        }
        builder.Append(output.TriggerId);
        builder.Append(output.ComponentId);
        builder.Append(output.ComponentPriority);
        builder.Append(output.ResultKind);
        builder.Append(output.Dependency);
        AppendNullableString(builder, output.AfterComponentId);
        builder.Append(output.MaxExpansion);
        AppendEffectSourceAuthority(builder, output.DownstreamSource);
        builder.Append(CanonicalFingerprintJson(output.Parameters));
        AppendEffectSourceKey(builder, output.DownstreamSourceKey);
        if (output.ReplacementTarget == null)
        {
            AppendNullableString(builder, null);
        }
        else
        {
            builder.Append(true);
            if (effectAuthority == null)
            {
                builder.Append(output.ReplacementTarget.EffectId);
                AppendAuthorityBinding(
                    builder,
                    output.ReplacementTarget.Authority);
            }
            else
            {
                AppendReplayStableEffectIdentity(
                    builder,
                    output.ReplacementTarget.EffectId,
                    output.ReplacementTarget.Authority);
            }
        }
        return builder.Build();
    }

    private static string CreateComponentMapFingerprint(
        ResourceOperationKey key,
        string componentId)
    {
        using var builder = new ResourceFingerprintBuilder(
            "effect-activation-component-map-v1");
        AppendResourceOperationKey(builder, key);
        builder.Append(componentId);
        return builder.Build();
    }

    private static string CreateEventRequirementFingerprint(
        ResourceMutationEventRequirement requirement)
    {
        using var builder = new ResourceFingerprintBuilder(
            "effect-activation-event-requirement-v1");
        AppendResourceOperationKey(builder, requirement.Producer);
        builder.Append(requirement.EventKind);
        return builder.Build();
    }

    private static string CreateResourceOperationKeyFingerprint(
        ResourceOperationKey key)
    {
        using var builder = new ResourceFingerprintBuilder(
            "resource-operation-key-v1");
        AppendResourceOperationKey(builder, key);
        return builder.Build();
    }

    private static void AppendResourceOperationKey(
        ResourceFingerprintBuilder builder,
        ResourceOperationKey key)
    {
        builder.Append(key.EventRef);
        builder.Append(key.OriginKind);
        builder.Append(key.OriginId);
        AppendResourceCoordinate(builder, key.Coordinate);
        builder.Append(key.Operation.ToString());
    }

    private static void AppendResourceCoordinate(
        ResourceFingerprintBuilder builder,
        ResourceCoordinate coordinate)
    {
        builder.Append(coordinate.Realm);
        builder.Append(ResourceDefinitionCatalog.GetOwnerKindToken(
            coordinate.OwnerKind));
        builder.Append(coordinate.ResourceOwnerId);
        builder.Append(coordinate.ResourceKey);
    }

    private static void AppendActivationIdentity(
        ResourceFingerprintBuilder builder,
        EffectActivationCandidateIdentity identity)
    {
        builder.Append(identity.EffectId);
        builder.Append(identity.TriggerId);
        builder.Append(identity.EventKind);
        builder.Append(identity.EventRef);
        builder.Append(identity.TriggerEventRef);
    }

    internal static void AppendReplayStableActivationIdentity(
        ResourceFingerprintBuilder builder,
        EffectActivationCandidateIdentity identity,
        ResourcePendingAuthorityBinding effectAuthority)
    {
        AppendReplayStableEffectIdentity(
            builder,
            identity.EffectId,
            effectAuthority);
        builder.Append(identity.TriggerId);
        builder.Append(identity.EventKind);
        builder.Append(identity.EventRef);
        builder.Append(identity.TriggerEventRef);
    }

    internal static void AppendReplayStableEffectIdentity(
        ResourceFingerprintBuilder builder,
        string effectId,
        ResourcePendingAuthorityBinding effectAuthority)
    {
        EffectAcceptedTurnPlanner.AppendPendingEffectReplayIdentity(
            builder,
            effectId,
            effectAuthority);
    }

    private static void AppendAuthorityBinding(
        ResourceFingerprintBuilder builder,
        ResourcePendingAuthorityBinding binding)
    {
        builder.Append(binding.BindingKind);
        builder.Append(binding.AuthorityId);
    }

    private static void AppendEffectSourceKey(
        ResourceFingerprintBuilder builder,
        EffectSourceKey? key)
    {
        builder.Append(key != null);
        if (key == null)
            return;
        builder.Append(key.Realm);
        builder.Append(key.Kind);
        builder.Append(key.SourceId);
        builder.Append(key.DefinitionKey);
    }

    private static void AppendEffectSourceAuthority(
        ResourceFingerprintBuilder builder,
        EffectSourceAuthorityEntry? source)
    {
        builder.Append(source != null);
        if (source == null)
            return;
        AppendEffectSourceKey(builder, source.Key);
        builder.Append(CanonicalFingerprintJson(source.Definition));
        builder.Append(source.Materializable);
        builder.Append(source.Active);
        builder.Append(source.SameTurn);
        AppendNullableString(builder, source.SourceRef);
        AppendOrderedStrings(
            builder,
            "satisfied-predicates",
            source.SatisfiedPredicates);
        AppendNullableString(
            builder,
            source.RequiredApplicationAuthority);
    }

    private static void AppendOrderedFingerprints(
        ResourceFingerprintBuilder builder,
        string section,
        IEnumerable<string> fingerprints)
    {
        var ordered = fingerprints
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        builder.Append(section);
        builder.Append(ordered.Length);
        foreach (var fingerprint in ordered)
            builder.Append(fingerprint);
    }

    private static void AppendOrderedStrings(
        ResourceFingerprintBuilder builder,
        string section,
        IEnumerable<string> values)
    {
        var ordered = values
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        builder.Append(section);
        builder.Append(ordered.Length);
        foreach (var value in ordered)
            builder.Append(value);
    }

    private static void AppendNullableString(
        ResourceFingerprintBuilder builder,
        string? value)
    {
        builder.Append(value != null);
        if (value != null)
            builder.Append(value);
    }

    private static void AppendNullableInt(
        ResourceFingerprintBuilder builder,
        int? value)
    {
        builder.Append(value.HasValue);
        if (value.HasValue)
            builder.Append(value.Value);
    }

    private static void AppendNullableDecimal(
        ResourceFingerprintBuilder builder,
        decimal? value)
    {
        builder.Append(value.HasValue);
        if (value.HasValue)
            builder.Append(value.Value);
    }

    private static string CanonicalFingerprintJson(JsonNode? node) =>
        node switch
        {
            null => "null",
            JsonObject value => "{" + string.Join(
                ",",
                value.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair =>
                        JsonSerializer.Serialize(pair.Key) + ":" +
                        CanonicalFingerprintJson(pair.Value))) + "}",
            JsonArray value => "[" + string.Join(
                ",",
                value.Select(CanonicalFingerprintJson)) + "]",
            _ => node.ToJsonString()
        };

    private static DerivedMutationAmountResult ResolveMutationAmount(
        ResourceHistoryWorkingSet history,
        ResourceDefinitionCatalog definitions,
        ResourceStateLedger directBaselineState,
        ResourceStateLedger? postDirectState,
        PreparedMutation prepared)
    {
        var mutation = prepared.Intent;
        if (mutation.DerivedAmount == null)
            return new DerivedMutationAmountResult(mutation.Amount, ShouldApply: true, Array.Empty<ValidationIssue>());

        if (history.TryResolveReplayIdentity(
                mutation.EventRef,
                mutation.Source.SourceKind,
                mutation.Source.SourceId,
                mutation.Coordinate,
                ToTransitionOperation(mutation.Source.Operation),
                out var prior) &&
            prior != null)
        {
            return new DerivedMutationAmountResult(prior.RequestedAmount, ShouldApply: true, Array.Empty<ValidationIssue>());
        }

        if (postDirectState == null ||
            !definitions.TryResolveExact(mutation.Coordinate.ResourceKey, out var definition) ||
            definition == null ||
            !directBaselineState.TryResolveExact(mutation.Coordinate, out var before) ||
            before == null ||
            !postDirectState.TryResolveExact(mutation.Coordinate, out var afterDirect) ||
            afterDirect == null)
        {
            return DerivedAmountFailure(
                "resource_planner_derived_amount_coordinate_missing",
                "one exact active coordinate and definition before and after direct phases",
                Describe(mutation.Key));
        }

        var policy = mutation.DerivedAmount;
        if (mutation.Amount != 0m ||
            policy.RecoveryPercent is <= 0 or > 100 ||
            prepared.Route.Phase != ResourceMutationPhase.RegisteredSystemOutcome ||
            !string.Equals(mutation.Source.SourceKind, "registered_system_outcome", StringComparison.Ordinal) ||
            mutation.Source.Operation is not (ResourceOperation.Gain or ResourceOperation.Restore) ||
            definition.NumericKind != ResourceNumericKind.Integer ||
            definition.Quantum != 1m ||
            before.State != ResourceLifecycleState.Active ||
            afterDirect.State != ResourceLifecycleState.Active ||
            before.Maximum != afterDirect.Maximum ||
            before.CapacityBinding != afterDirect.CapacityBinding)
        {
            return DerivedAmountFailure(
                "resource_planner_derived_amount_policy_invalid",
                "registered-system gain/restore with zero placeholder, recovery 1..100, and one unchanged active integer quantum-1 coordinate",
                Describe(mutation.Key));
        }

        if (!ResourceMaterializationContract.TrySubtractExact(
                before.Current,
                afterDirect.Current,
                out var directLoss))
        {
            return DerivedAmountFailure(
                "resource_planner_derived_amount_loss_unrepresentable",
                "exact representable pre-direct minus post-direct current",
                Describe(mutation.Key));
        }
        if (directLoss <= 0m)
            return new DerivedMutationAmountResult(0m, ShouldApply: false, Array.Empty<ValidationIssue>());
        if (!ResourceMaterializationContract.TryFloorPercentageOfIntegral(
                directLoss,
                policy.RecoveryPercent,
                out var amount))
        {
            return DerivedAmountFailure(
                "resource_planner_derived_amount_unrepresentable",
                "exact integral loss and bounded floor percentage",
                Describe(mutation.Key));
        }
        return new DerivedMutationAmountResult(
            amount,
            ShouldApply: amount > 0m,
            Array.Empty<ValidationIssue>());
    }

    private static DerivedMutationAmountResult DerivedAmountFailure(
        string code,
        string expected,
        string actual) =>
        new(
            0m,
            ShouldApply: false,
            new[]
            {
                new ValidationIssue(
                    ResourceMaterializationContract.CommandPath,
                    IssueSeverity.Error,
                    "A derived registered resource outcome could not be resolved exactly.",
                    code: code,
                    section: "resource_planner",
                    expected: expected,
                    actual: actual,
                    repairHint: "Restore the sealed resource inputs and retry the accepted turn.")
            });

    private static ResourceTransitionOperation ToTransitionOperation(ResourceOperation operation) =>
        operation switch
        {
            ResourceOperation.Damage => ResourceTransitionOperation.Damage,
            ResourceOperation.Restore => ResourceTransitionOperation.Restore,
            ResourceOperation.Spend => ResourceTransitionOperation.Spend,
            ResourceOperation.Gain => ResourceTransitionOperation.Gain,
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

    private static PreparationResult PrepareMutations(
        AcceptedMechanicsResourceInput input,
        AcceptedMechanicsIdentityFactory identityFactory,
        AllocatedIdentityRegistry identityRegistry,
        Func<
            ResourceMutationSourceRequest,
            ResourceDefinition,
            ResourceCoordinate?,
            ResourceMutationSourceResolution>? sourceResolver = null)
    {
        var issues = new List<ValidationIssue>();
        var unresolved = new List<UnresolvedMutation>();
        var keys = new HashSet<ResourceOperationKey>();
        foreach (var mutation in input.Mutations)
        {
            if (!keys.Add(mutation.Key))
            {
                AddIssue(
                    issues,
                    "resource_planner_duplicate_operation",
                    "one mutation per exact replay key",
                    Describe(mutation.Key));
                continue;
            }
            if (!input.Definitions.TryResolveExact(
                    mutation.Coordinate.ResourceKey,
                    out var definition) ||
                definition == null)
            {
                AddIssue(
                    issues,
                    "resource_planner_definition_unknown",
                    "one exact sealed resource definition",
                    mutation.Coordinate.ResourceKey);
                continue;
            }

            var source = sourceResolver == null
                ? input.Sources.Resolve(
                    mutation.Source,
                    definition,
                    mutation.Coordinate)
                : sourceResolver(
                    mutation.Source,
                    definition,
                    mutation.Coordinate);
            if (!source.IsValid || source.Route == null)
            {
                issues.AddRange(source.Issues);
                continue;
            }
            unresolved.Add(new UnresolvedMutation(
                mutation,
                BindResultConstraint(source.Route, mutation.ResultConstraint)));
        }
        if (issues.Count != 0)
            return new PreparationResult(Array.Empty<PreparedMutation>(), issues);

        var ordered = unresolved
            .OrderBy(static value => value.Route.Phase)
            .ThenBy(static value => value.Route.Priority)
            .ThenBy(static value => value.Intent.Source.SourceId, StringComparer.Ordinal)
            .ThenBy(static value => value.Intent.EventRef, StringComparer.Ordinal)
            .ThenBy(static value => value.Intent.Coordinate.Realm, StringComparer.Ordinal)
            .ThenBy(static value => value.Intent.Coordinate.OwnerKind)
            .ThenBy(static value => value.Intent.Coordinate.ResourceOwnerId, StringComparer.Ordinal)
            .ThenBy(static value => value.Intent.Coordinate.ResourceKey, StringComparer.Ordinal)
            .ThenBy(static value => value.Intent.Source.Operation)
            .ToArray();
        var prepared = new List<PreparedMutation>(ordered.Length);
        foreach (var value in ordered)
        {
            var operationId = identityFactory.CreateOperationId(value.Intent);
            var transitionId = identityFactory.CreateTransitionId(value.Intent);
            identityRegistry.ValidateOperation(operationId, issues);
            identityRegistry.ValidateTransition(transitionId, issues);
            prepared.Add(new PreparedMutation(
                value.Intent,
                value.Route,
                operationId,
                transitionId));
        }

        return issues.Count == 0
            ? new PreparationResult(prepared, Array.Empty<ValidationIssue>())
            : new PreparationResult(Array.Empty<PreparedMutation>(), issues);
    }

    private static ResourceAuthorizedSourceRoute BindResultConstraint(
        ResourceAuthorizedSourceRoute route,
        ResourceMutationResultConstraint? constraint)
    {
        if (constraint == null)
            return route;

        using var fingerprint = new ResourceFingerprintBuilder(
            "resource-result-constraint-policy-v1");
        fingerprint.Append(route.PolicyBinding.AuthorityFingerprint);
        fingerprint.Append(constraint.RejectBelow.HasValue);
        if (constraint.RejectBelow.HasValue)
            fingerprint.Append(constraint.RejectBelow.Value);
        fingerprint.Append(constraint.RejectAbove.HasValue);
        if (constraint.RejectAbove.HasValue)
            fingerprint.Append(constraint.RejectAbove.Value);
        return route with
        {
            PolicyBinding = route.PolicyBinding with
            {
                AuthorityFingerprint = fingerprint.Build()
            }
        };
    }

    private static ResourceTriggerGraphResult BuildGraph(
        IReadOnlyList<PreparedMutation> mutations)
    {
        var issues = new List<ValidationIssue>();
        var byKey = mutations.ToDictionary(static value => value.Intent.Key);
        var nodes = new List<ResourceTriggerGraphNode>(mutations.Count);
        foreach (var mutation in mutations)
        {
            var dependencies = new List<string>();
            foreach (var dependency in mutation.Intent.Dependencies)
            {
                if (!byKey.TryGetValue(dependency, out var producer))
                {
                    AddIssue(
                        issues,
                        "resource_graph_dependency_missing",
                        "one exact existing dependency operation",
                        Describe(dependency));
                    continue;
                }
                mutation.DependencyOperationIds[dependency] = producer.OperationId;
                dependencies.Add(producer.OperationId);
            }

            var eventRequirements = new List<ResourceEventRequirement>();
            foreach (var requirement in mutation.Intent.EventRequirements)
            {
                if (!byKey.TryGetValue(requirement.Producer, out var producer))
                {
                    AddIssue(
                        issues,
                        "resource_graph_dependency_missing",
                        "one exact existing event producer operation",
                        Describe(requirement.Producer));
                    continue;
                }
                mutation.DependencyOperationIds[requirement.Producer] = producer.OperationId;
                eventRequirements.Add(new ResourceEventRequirement(
                    producer.OperationId,
                    requirement.EventKind));
            }

            nodes.Add(new ResourceTriggerGraphNode(
                mutation.OperationId,
                mutation.Route.Phase,
                mutation.Route.Priority,
                mutation.Intent.Source.SourceId,
                CreateStableMutationOrderKey(mutation.Intent.Key),
                mutation.OperationId,
                dependencies,
                eventRequirements));
        }

        return issues.Count == 0
            ? ResourceTriggerGraph.Build(nodes)
            : new ResourceTriggerGraphResult(null, issues);
    }

    private static CapacityPreparationResult PrepareCapacityTransitions(
        IReadOnlyList<ResourceCapacityIntent> transitions,
        AcceptedMechanicsIdentityFactory identityFactory,
        AllocatedIdentityRegistry identityRegistry)
    {
        var issues = new List<ValidationIssue>();
        var keys = new HashSet<ResourceCapacityOperationKey>();
        foreach (var transition in transitions)
        {
            if (!keys.Add(transition.Key))
            {
                AddIssue(
                    issues,
                    "resource_planner_duplicate_operation",
                    "one capacity transition per exact replay key",
                    Describe(transition.Key));
            }
        }
        if (issues.Count != 0)
            return new CapacityPreparationResult(Array.Empty<PreparedCapacity>(), issues);

        var prepared = transitions
            .OrderBy(static value => value.Phase)
            .ThenBy(static value => value.Priority)
            .ThenBy(static value => value.OriginId, StringComparer.Ordinal)
            .ThenBy(static value => value.EventRef, StringComparer.Ordinal)
            .ThenBy(static value => value.Coordinate.Realm, StringComparer.Ordinal)
            .ThenBy(static value => value.Coordinate.OwnerKind)
            .ThenBy(static value => value.Coordinate.ResourceOwnerId, StringComparer.Ordinal)
            .ThenBy(static value => value.Coordinate.ResourceKey, StringComparer.Ordinal)
            .ThenBy(static value => value.Operation)
            .Select(value => new PreparedCapacity(
                value,
                identityFactory.CreateOperationId(value),
                identityFactory.CreateTransitionId(value)))
            .ToArray();
        foreach (var value in prepared)
        {
            identityRegistry.ValidateOperation(value.OperationId, issues);
            identityRegistry.ValidateTransition(value.TransitionId, issues);
        }
        return issues.Count == 0
            ? new CapacityPreparationResult(prepared, Array.Empty<ValidationIssue>())
            : new CapacityPreparationResult(Array.Empty<PreparedCapacity>(), issues);
    }

    private static bool RequirementsSatisfied(
        PreparedMutation mutation,
        IReadOnlyDictionary<string, HashSet<string>> producedEvents)
    {
        foreach (var requirement in mutation.Intent.EventRequirements)
        {
            var producer = mutation.DependencyOperationIds[requirement.Producer];
            if (!producedEvents.TryGetValue(producer, out var events) ||
                !events.Contains(requirement.EventKind))
            {
                return false;
            }
        }
        return true;
    }

    private static void ValidateAllocatedIdentity(
        string value,
        string label,
        HashSet<string> exact,
        HashSet<string> aliases,
        List<ValidationIssue> issues)
    {
        var alias = ResourceMaterializationContract.BuildConfusableKey(value);
        if (!ResourceMaterializationContract.IsExactIdentifier(value) ||
            !exact.Add(value) ||
            !aliases.Add(alias))
        {
            AddIssue(
                issues,
                $"resource_planner_{label}_identity_invalid",
                $"one exact/confusable-unique client-owned {label} identity",
                value);
        }
    }

    private static AcceptedMechanicsResourcePlanningResult Failure(
        IEnumerable<ValidationIssue> issues,
        AcceptedMechanicsPlannerStatistics statistics) =>
        new(
            null,
            null,
            Array.Empty<ResourceAppliedEvent>(),
            Array.Empty<ResourceTransition>(),
            Array.Empty<ResourceTransition>(),
            issues.ToArray(),
            statistics);

    private static IReadOnlyList<ValidationIssue> Issue(
        string code,
        string expected,
        string actual)
    {
        var issues = new List<ValidationIssue>();
        AddIssue(issues, code, expected, actual);
        return issues;
    }

    private static IReadOnlyList<ValidationIssue> ArbiterIssues(
        IReadOnlyList<AcceptedEffectUseArbiterIssue> arbiterIssues)
    {
        var issues = new List<ValidationIssue>(arbiterIssues.Count);
        foreach (var arbiterIssue in arbiterIssues)
        {
            AddIssue(
                issues,
                arbiterIssue.Code,
                "one coherent accepted-effect activation transcript",
                arbiterIssue.Message);
        }
        return issues;
    }

    private static AcceptedMechanicsPlannerStatistics Statistics(
        ResourceHistoryWorkingSet? history = null,
        PlannerWorkMetrics? metrics = null) =>
        new(
            history?.BaselineSeedCount ?? 0,
            history?.IncrementalAppendCount ?? 0,
            history?.FreezeCount ?? 0,
            metrics?.CapacityDescriptorCount ?? 0,
            metrics?.MutationDescriptorCount ?? 0,
            metrics?.GraphNodeDescriptorCount ?? 0,
            metrics?.MaximumTriggerDepth ?? 0,
            metrics?.SchedulingDescriptorVisitCount ?? 0,
            history?.ReplayIdentityLookupCount ?? 0,
            history?.TotalWorkUnits ?? 0,
            metrics?.EffectTriggerIndexLookupCount ?? 0,
            metrics?.EffectTriggerCandidateVisitCount ?? 0,
            metrics?.EffectSourceBindingIndexLookupCount ?? 0,
            metrics?.EffectSourceBindingCandidateVisitCount ?? 0,
            metrics?.EffectRoutingDescriptorAccessCount ?? 0,
            metrics?.EffectOccurrenceCloneCount ?? 0,
            metrics?.EffectFullValidationPassCount ?? 0,
            metrics?.EffectTriggerArrayVisitCount ?? 0,
            metrics?.EffectComponentIndexLookupCount ?? 0,
            metrics?.EffectSelectedComponentVisitCount ?? 0,
            metrics?.PendingCandidateFingerprintOutputVisitCount ?? 0,
            metrics?.PendingProjectionDependencyVisitCount ?? 0,
            metrics?.PendingTranscriptPrefixStampVisitCount ?? 0,
            metrics?.SourceAuthoritySeedCount ?? 0,
            metrics?.SourceAuthorityAddVisitCount ?? 0,
            metrics?.SourceAuthorityResolveLookupCount ?? 0,
            metrics?.SourceAuthorityFreezeCount ?? 0);

    private static string PrimaryResourceEvent(ResourceOperation operation) =>
        operation switch
        {
            ResourceOperation.Damage => "resource_damaged",
            ResourceOperation.Restore => "resource_restored",
            ResourceOperation.Spend => "resource_spent",
            ResourceOperation.Gain => "resource_gained",
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };

    private static bool ReservesTerminalAvailabilityAtAcceptance(
        EffectReactionExecution reaction) =>
        string.Equals(
            reaction.Dependency,
            "after_current_event",
            StringComparison.Ordinal) &&
        AcceptedEffectBoundaryTranscript.ResolveTerminalAvailabilityEffectId(
            reaction) != null;

    private static string Describe(ResourceOperationKey key) =>
        $"{key.EventRef}/{key.OriginKind}/{key.OriginId}/" +
        $"{key.Coordinate.Realm}/" +
        $"{ResourceDefinitionCatalog.GetOwnerKindToken(key.Coordinate.OwnerKind)}/" +
        $"{key.Coordinate.ResourceOwnerId}/" +
        $"{key.Coordinate.ResourceKey}/{key.Operation}";

    private static string CreateStableMutationOrderKey(ResourceOperationKey key) =>
        string.Join(
            "\0",
            key.EventRef,
            key.OriginKind,
            key.OriginId,
            key.Coordinate.Realm,
            key.Coordinate.OwnerKind.ToString(),
            key.Coordinate.ResourceOwnerId,
            key.Coordinate.ResourceKey,
            key.Operation.ToString());

    private static string Describe(ResourceCapacityOperationKey key) =>
        $"{key.EventRef}/{key.OriginKind}/{key.OriginId}/" +
        $"{key.Coordinate.Realm}/" +
        $"{ResourceDefinitionCatalog.GetOwnerKindToken(key.Coordinate.OwnerKind)}/" +
        $"{key.Coordinate.ResourceOwnerId}/" +
        $"{key.Coordinate.ResourceKey}/{key.Operation}";

    private static string Describe(ResourceCoordinate coordinate) =>
        $"{coordinate.Realm}/" +
        $"{ResourceDefinitionCatalog.GetOwnerKindToken(coordinate.OwnerKind)}/" +
        $"{coordinate.ResourceOwnerId}/{coordinate.ResourceKey}";

    private static void AddIssue(
        List<ValidationIssue> issues,
        string code,
        string expected,
        string actual) =>
        ResourceMaterializationContract.AddIssue(
            issues,
            ResourceMaterializationContract.CommandPath,
            code,
            expected,
            actual);

    private sealed record UnresolvedMutation(
        ResourceMutationIntent Intent,
        ResourceAuthorizedSourceRoute Route);

    private sealed record DerivedMutationAmountResult(
        decimal Amount,
        bool ShouldApply,
        IReadOnlyList<ValidationIssue> Issues);

    private sealed class PreparedMutation
    {
        internal PreparedMutation(
            ResourceMutationIntent intent,
            ResourceAuthorizedSourceRoute route,
            string operationId,
            string transitionId)
        {
            Intent = intent;
            Route = route;
            OperationId = operationId;
            TransitionId = transitionId;
            DependencyOperationIds = new Dictionary<ResourceOperationKey, string>();
        }

        internal ResourceMutationIntent Intent { get; }
        internal ResourceAuthorizedSourceRoute Route { get; }
        internal string OperationId { get; }
        internal string TransitionId { get; }
        internal Dictionary<ResourceOperationKey, string> DependencyOperationIds { get; }
    }

    private sealed record PreparedCapacity(
        ResourceCapacityIntent Intent,
        string OperationId,
        string TransitionId);

    private sealed class AllocatedIdentityRegistry
    {
        private readonly HashSet<string> _operationIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _operationAliases = new(StringComparer.Ordinal);
        private readonly HashSet<string> _transitionIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _transitionAliases = new(StringComparer.Ordinal);

        internal void ValidateOperation(
            string value,
            List<ValidationIssue> issues) =>
            ValidateAllocatedIdentity(
                value,
                "operation",
                _operationIds,
                _operationAliases,
                issues);

        internal void ValidateTransition(
            string value,
            List<ValidationIssue> issues) =>
            ValidateAllocatedIdentity(
                value,
                "transition",
                _transitionIds,
                _transitionAliases,
                issues);
    }

    private sealed record CapacityPreparationResult(
        IReadOnlyList<PreparedCapacity> Transitions,
        IReadOnlyList<ValidationIssue> Issues);

    private sealed record PreparationResult(
        IReadOnlyList<PreparedMutation> Mutations,
        IReadOnlyList<ValidationIssue> Issues);

    private sealed record TriggerLineageDecision(
        bool Allowed,
        Dictionary<string, int> Lineage,
        IReadOnlyList<ValidationIssue> Issues);

    private sealed record CandidateMaterialFingerprintResult(
        string? Fingerprint,
        IReadOnlyList<ValidationIssue> Issues)
    {
        internal bool IsValid => Fingerprint != null && Issues.Count == 0;
    }

    private sealed record ValidatedPendingCandidateAuthority(
        string CausalMaterialFingerprint,
        string CandidateFingerprint);

    private sealed record PendingCandidateAuthorityValidationResult(
        IReadOnlyDictionary<
            EffectActivationCandidateIdentity,
            ValidatedPendingCandidateAuthority> Authorities,
        IReadOnlyList<ValidationIssue> Issues)
    {
        internal bool IsValid => Issues.Count == 0;
    }

    private sealed record ResourceGraphExpansionWork(
        int EffectTriggerIndexLookupCount,
        long EffectTriggerCandidateVisitCount,
        int EffectSourceBindingIndexLookupCount,
        long EffectSourceBindingCandidateVisitCount,
        long EffectRoutingDescriptorAccessCount,
        long EffectOccurrenceCloneCount,
        long EffectFullValidationPassCount,
        long EffectTriggerArrayVisitCount,
        long EffectComponentIndexLookupCount,
        long EffectSelectedComponentVisitCount,
        long PendingCandidateFingerprintOutputVisitCount,
        long PendingProjectionDependencyVisitCount,
        int SourceAuthoritySeedCount,
        int SourceAuthorityAddVisitCount,
        int SourceAuthorityResolveLookupCount,
        int SourceAuthorityFreezeCount)
    {
        internal static ResourceGraphExpansionWork Empty { get; } =
            new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    private sealed record CompleteResourceGraphPreparation(
        IReadOnlyList<PreparedMutation> Mutations,
        ResourceTriggerGraph? Graph,
        IReadOnlyList<EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>
            TriggerCandidates,
        IReadOnlyDictionary<
            ResourceOperationKey,
            EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>
            TriggerCandidatesByMutation,
        IReadOnlyList<ResourceMutationSourceExport> SourceExports,
        IReadOnlyList<ValidationIssue> Issues,
        ResourceGraphExpansionWork Work)
    {
        internal bool IsValid => Graph != null && Issues.Count == 0;
    }

    private sealed class PlannerWorkMetrics
    {
        internal int CapacityDescriptorCount { get; set; }
        internal int MutationDescriptorCount { get; set; }
        internal int GraphNodeDescriptorCount { get; set; }
        internal int MaximumTriggerDepth { get; set; }
        internal long SchedulingDescriptorVisitCount { get; set; }
        internal int EffectTriggerIndexLookupCount { get; set; }
        internal long EffectTriggerCandidateVisitCount { get; set; }
        internal int EffectSourceBindingIndexLookupCount { get; set; }
        internal long EffectSourceBindingCandidateVisitCount { get; set; }
        internal long EffectRoutingDescriptorAccessCount { get; set; }
        internal long EffectOccurrenceCloneCount { get; set; }
        internal long EffectFullValidationPassCount { get; set; }
        internal long EffectTriggerArrayVisitCount { get; set; }
        internal long EffectComponentIndexLookupCount { get; set; }
        internal long EffectSelectedComponentVisitCount { get; set; }
        internal long PendingCandidateFingerprintOutputVisitCount { get; set; }
        internal long PendingProjectionDependencyVisitCount { get; set; }
        internal long PendingTranscriptPrefixStampVisitCount { get; set; }
        internal int SourceAuthoritySeedCount { get; set; }
        internal int SourceAuthorityAddVisitCount { get; set; }
        internal int SourceAuthorityResolveLookupCount { get; set; }
        internal int SourceAuthorityFreezeCount { get; set; }
    }
}

internal sealed record ResourceEventRequirement(
    string ProducerNodeId,
    string EventKind);

internal sealed record ResourceTriggerGraphNode(
    string NodeId,
    ResourceMutationPhase Phase,
    int Priority,
    string OriginId,
    string SemanticOrderKey,
    string OperationId,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<ResourceEventRequirement> EventRequirements);

internal sealed record ResourceTriggerGraphResult(
    ResourceTriggerGraph? Graph,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Graph != null && Issues.Count == 0;
}

internal sealed class ResourceTriggerGraph
{
    private static readonly IComparer<ResourceTriggerGraphNode> ReadyComparer =
        Comparer<ResourceTriggerGraphNode>.Create(static (left, right) =>
        {
            var comparison = left.Phase.CompareTo(right.Phase);
            if (comparison != 0)
                return comparison;

            comparison = left.Priority.CompareTo(right.Priority);
            if (comparison != 0)
                return comparison;

            comparison = string.CompareOrdinal(left.OriginId, right.OriginId);
            if (comparison != 0)
                return comparison;

            comparison = string.CompareOrdinal(
                left.SemanticOrderKey,
                right.SemanticOrderKey);
            if (comparison != 0)
                return comparison;

            comparison = string.CompareOrdinal(left.OperationId, right.OperationId);
            return comparison != 0
                ? comparison
                : string.CompareOrdinal(left.NodeId, right.NodeId);
        });

    private readonly IReadOnlyDictionary<string, ResourceTriggerGraphNode> _byId;
    private readonly IReadOnlyDictionary<string, HashSet<string>> _children;
    private readonly IReadOnlyDictionary<string, HashSet<string>> _parents;
    private readonly IReadOnlyDictionary<string, int> _initialIndegrees;

    private ResourceTriggerGraph(
        IReadOnlyList<ResourceTriggerGraphNode> orderedNodes,
        int maximumDepth,
        IReadOnlyDictionary<string, ResourceTriggerGraphNode> byId,
        IReadOnlyDictionary<string, HashSet<string>> children,
        IReadOnlyDictionary<string, HashSet<string>> parents,
        IReadOnlyDictionary<string, int> initialIndegrees)
    {
        OrderedNodes = orderedNodes;
        MaximumDepth = maximumDepth;
        _byId = byId;
        _children = children;
        _parents = parents;
        _initialIndegrees = initialIndegrees;
    }

    internal IReadOnlyList<ResourceTriggerGraphNode> OrderedNodes { get; }
    internal int MaximumDepth { get; }

    internal ResourceTriggerGraphExecutionScheduler CreateExecutionScheduler(
        Func<ResourceTriggerGraphNode, bool> requirementsSatisfied) =>
        new(
            _byId,
            _children,
            _initialIndegrees,
            ReadyComparer,
            requirementsSatisfied);

    internal IReadOnlySet<string> ExpandCausalOperationClosure(
        IEnumerable<string> rootOperationIds)
    {
        ArgumentNullException.ThrowIfNull(rootOperationIds);
        var nodeIdByOperationId = _byId.Values.ToDictionary(
            static node => node.OperationId,
            static node => node.NodeId,
            StringComparer.Ordinal);
        var closure = new HashSet<string>(StringComparer.Ordinal);
        var frontier = new Queue<string>();
        foreach (var operationId in rootOperationIds
                     .Distinct(StringComparer.Ordinal))
        {
            if (!nodeIdByOperationId.TryGetValue(operationId, out var nodeId))
                continue;
            if (closure.Add(operationId))
                frontier.Enqueue(nodeId);
        }

        var prerequisiteFrontier = new Queue<string>(frontier);
        while (prerequisiteFrontier.Count != 0)
        {
            var nodeId = prerequisiteFrontier.Dequeue();
            foreach (var parentId in _parents[nodeId])
            {
                var parent = _byId[parentId];
                if (closure.Add(parent.OperationId))
                    prerequisiteFrontier.Enqueue(parentId);
            }
        }

        while (frontier.Count != 0)
        {
            var nodeId = frontier.Dequeue();
            foreach (var childId in _children[nodeId])
            {
                var child = _byId[childId];
                if (closure.Add(child.OperationId))
                    frontier.Enqueue(childId);
            }
        }
        return closure;
    }

    internal static ResourceTriggerGraphResult Build(
        IEnumerable<ResourceTriggerGraphNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var candidates = nodes.ToArray();
        var issues = new List<ValidationIssue>();
        if (candidates.Length > ResourceMaterializationContract.MaxTriggerNodes)
        {
            AddIssue(
                issues,
                "resource_graph_node_limit_exceeded",
                $"at most {ResourceMaterializationContract.MaxTriggerNodes} graph nodes",
                candidates.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return Invalid(issues);
        }

        var byId = new Dictionary<string, ResourceTriggerGraphNode>(StringComparer.Ordinal);
        var confusableIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            if (!ValidateNodeShape(candidate, issues))
                continue;

            if (!byId.TryAdd(candidate.NodeId, candidate) ||
                !confusableIds.Add(
                    ResourceMaterializationContract.BuildConfusableKey(candidate.NodeId)))
            {
                AddIssue(
                    issues,
                    "resource_graph_duplicate_node",
                    "one exact/confusable node identity",
                    candidate.NodeId);
            }
        }

        if (issues.Count != 0)
            return Invalid(issues);

        var parents = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var children = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            parents[candidate.NodeId] = new HashSet<string>(StringComparer.Ordinal);
            children[candidate.NodeId] = new HashSet<string>(StringComparer.Ordinal);
        }

        foreach (var candidate in candidates)
        {
            foreach (var dependency in candidate.Dependencies)
            {
                RegisterDependency(
                    candidate,
                    dependency,
                    byId,
                    parents,
                    children,
                    issues);
            }

            foreach (var requirement in candidate.EventRequirements)
            {
                if (!EffectEventTypeCatalog.IsResourceEvent(requirement.EventKind))
                {
                    AddIssue(
                        issues,
                        "resource_graph_event_invalid",
                        "one closed resource event kind",
                        requirement.EventKind ?? "null");
                    continue;
                }

                RegisterDependency(
                    candidate,
                    requirement.ProducerNodeId,
                    byId,
                    parents,
                    children,
                    issues);
            }
        }

        if (issues.Count != 0)
            return Invalid(issues);

        var depths = new Dictionary<string, int>(StringComparer.Ordinal);
        var indegrees = parents.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.Count,
            StringComparer.Ordinal);
        var ready = new SortedSet<ResourceTriggerGraphNode>(ReadyComparer);
        foreach (var candidate in candidates)
        {
            if (indegrees[candidate.NodeId] == 0)
                ready.Add(candidate);
        }

        var ordered = new List<ResourceTriggerGraphNode>(candidates.Length);
        var maximumDepth = 0;
        while (ready.Count != 0)
        {
            var current = ready.Min!;
            ready.Remove(current);
            var currentDepth = parents[current.NodeId].Count == 0
                ? 1
                : parents[current.NodeId].Max(parent => depths[parent]) + 1;
            depths[current.NodeId] = currentDepth;
            maximumDepth = Math.Max(maximumDepth, currentDepth);
            ordered.Add(current);

            foreach (var childId in children[current.NodeId])
            {
                if (--indegrees[childId] == 0)
                    ready.Add(byId[childId]);
            }
        }

        if (ordered.Count != candidates.Length)
        {
            AddIssue(
                issues,
                "resource_graph_cycle",
                "one acyclic resource dependency graph",
                $"{candidates.Length - ordered.Count} cyclic node(s)");
            return Invalid(issues);
        }

        if (maximumDepth > ResourceMaterializationContract.MaxTriggerDepth)
        {
            AddIssue(
                issues,
                "resource_graph_depth_limit_exceeded",
                $"maximum graph depth {ResourceMaterializationContract.MaxTriggerDepth}",
                maximumDepth.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return Invalid(issues);
        }

        return new ResourceTriggerGraphResult(
            new ResourceTriggerGraph(
                new ReadOnlyCollection<ResourceTriggerGraphNode>(ordered),
                maximumDepth,
                new ReadOnlyDictionary<string, ResourceTriggerGraphNode>(byId),
                new ReadOnlyDictionary<string, HashSet<string>>(children),
                new ReadOnlyDictionary<string, HashSet<string>>(parents),
                new ReadOnlyDictionary<string, int>(parents.ToDictionary(
                    static pair => pair.Key,
                    static pair => pair.Value.Count,
                    StringComparer.Ordinal))),
            Array.Empty<ValidationIssue>());
    }

    private static bool ValidateNodeShape(
        ResourceTriggerGraphNode? candidate,
        List<ValidationIssue> issues)
    {
        if (candidate == null)
        {
            AddIssue(
                issues,
                "resource_graph_node_invalid",
                "one non-null graph node",
                "null");
            return false;
        }

        var valid = true;
        valid &= RequireIdentifier(candidate.NodeId, "nodeId", issues);
        valid &= RequireIdentifier(candidate.OriginId, "originId", issues);
        valid &= RequireIdentifier(candidate.OperationId, "operationId", issues);
        if (!Enum.IsDefined(candidate.Phase) || candidate.Priority < 0)
        {
            AddIssue(
                issues,
                "resource_graph_node_invalid",
                "one registered phase and non-negative priority",
                $"phase={candidate.Phase};priority={candidate.Priority}");
            valid = false;
        }

        if (candidate.Dependencies == null || candidate.EventRequirements == null)
        {
            AddIssue(
                issues,
                "resource_graph_node_invalid",
                "present dependency and event requirement collections",
                "null collection");
            return false;
        }

        foreach (var dependency in candidate.Dependencies)
            valid &= RequireIdentifier(dependency, "dependency", issues);
        foreach (var requirement in candidate.EventRequirements)
        {
            if (requirement == null)
            {
                AddIssue(
                    issues,
                    "resource_graph_event_invalid",
                    "one non-null resource event requirement",
                    "null");
                valid = false;
                continue;
            }

            valid &= RequireIdentifier(
                requirement.ProducerNodeId,
                "event producer nodeId",
                issues);
            valid &= RequireIdentifier(requirement.EventKind, "event kind", issues);
        }

        return valid;
    }

    private static bool RequireIdentifier(
        string? value,
        string label,
        List<ValidationIssue> issues)
    {
        if (ResourceMaterializationContract.IsExactIdentifier(value))
            return true;

        AddIssue(
            issues,
            "resource_graph_node_invalid",
            $"one exact {label}",
            value ?? "null");
        return false;
    }

    private static void RegisterDependency(
        ResourceTriggerGraphNode candidate,
        string dependencyId,
        IReadOnlyDictionary<string, ResourceTriggerGraphNode> byId,
        IReadOnlyDictionary<string, HashSet<string>> parents,
        IReadOnlyDictionary<string, HashSet<string>> children,
        List<ValidationIssue> issues)
    {
        if (!byId.TryGetValue(dependencyId, out var dependency))
        {
            AddIssue(
                issues,
                "resource_graph_dependency_missing",
                "one exact existing dependency nodeId",
                dependencyId);
            return;
        }

        if (candidate.Phase < dependency.Phase)
        {
            AddIssue(
                issues,
                "resource_graph_phase_inversion",
                "dependency phase not later than dependent phase",
                $"{dependency.NodeId}:{dependency.Phase}->{candidate.NodeId}:{candidate.Phase}");
            return;
        }

        if (parents[candidate.NodeId].Add(dependencyId))
            children[dependencyId].Add(candidate.NodeId);
    }

    private static ResourceTriggerGraphResult Invalid(
        List<ValidationIssue> issues) =>
        new(null, new ReadOnlyCollection<ValidationIssue>(issues));

    private static void AddIssue(
        List<ValidationIssue> issues,
        string code,
        string expected,
        string actual) =>
        ResourceMaterializationContract.AddIssue(
            issues,
            ResourceMaterializationContract.CommandPath + ".triggerGraph",
            code,
            expected,
            actual);
}

internal sealed class ResourceTriggerGraphExecutionScheduler
{
    private readonly IReadOnlyDictionary<string, ResourceTriggerGraphNode> _byId;
    private readonly IReadOnlyDictionary<string, HashSet<string>> _children;
    private readonly Dictionary<string, int> _indegrees;
    private readonly SortedSet<ResourceTriggerGraphNode> _skipped;
    private readonly SortedSet<ResourceTriggerGraphNode> _runnable;
    private readonly Func<ResourceTriggerGraphNode, bool> _requirementsSatisfied;
    private ResourceTriggerGraphNode? _inFlight;

    internal ResourceTriggerGraphExecutionScheduler(
        IReadOnlyDictionary<string, ResourceTriggerGraphNode> byId,
        IReadOnlyDictionary<string, HashSet<string>> children,
        IReadOnlyDictionary<string, int> initialIndegrees,
        IComparer<ResourceTriggerGraphNode> readyComparer,
        Func<ResourceTriggerGraphNode, bool> requirementsSatisfied)
    {
        _byId = byId ?? throw new ArgumentNullException(nameof(byId));
        _children = children ?? throw new ArgumentNullException(nameof(children));
        ArgumentNullException.ThrowIfNull(initialIndegrees);
        ArgumentNullException.ThrowIfNull(readyComparer);
        _requirementsSatisfied = requirementsSatisfied ??
            throw new ArgumentNullException(nameof(requirementsSatisfied));
        _indegrees = initialIndegrees.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value,
            StringComparer.Ordinal);
        _skipped = new SortedSet<ResourceTriggerGraphNode>(readyComparer);
        _runnable = new SortedSet<ResourceTriggerGraphNode>(readyComparer);
        foreach (var pair in _indegrees)
        {
            if (pair.Value == 0)
                AddReady(_byId[pair.Key]);
        }
    }

    internal bool TryTakeNext(
        out ResourceTriggerGraphNode node,
        out bool shouldExecute) =>
        TryTakeNext(
            preferredOperationIds: null,
            out node,
            out shouldExecute);

    internal bool TryTakeNext(
        IReadOnlySet<string>? preferredOperationIds,
        out ResourceTriggerGraphNode node,
        out bool shouldExecute)
    {
        if (_inFlight != null)
        {
            throw new InvalidOperationException(
                "The current resource graph node must be completed before taking another.");
        }

        if (preferredOperationIds is { Count: > 0 })
        {
            if (TryTakePreferred(
                    _skipped,
                    preferredOperationIds,
                    out node))
            {
                shouldExecute = false;
            }
            else if (TryTakePreferred(
                         _runnable,
                         preferredOperationIds,
                         out node))
            {
                shouldExecute = true;
            }
            else
            {
                node = null!;
                shouldExecute = false;
                return false;
            }
        }
        else if (_skipped.Count != 0)
        {
            node = _skipped.Min!;
            _skipped.Remove(node);
            shouldExecute = false;
        }
        else if (_runnable.Count != 0)
        {
            node = _runnable.Min!;
            _runnable.Remove(node);
            shouldExecute = true;
        }
        else
        {
            node = null!;
            shouldExecute = false;
            return false;
        }

        _inFlight = node;
        return true;
    }

    internal bool HasPendingNodes =>
        _inFlight != null || _skipped.Count != 0 || _runnable.Count != 0;

    internal void Complete(ResourceTriggerGraphNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (_inFlight == null ||
            !string.Equals(_inFlight.NodeId, node.NodeId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Only the current resource graph node can be completed.");
        }

        _inFlight = null;
        foreach (var childId in _children[node.NodeId])
        {
            var remaining = --_indegrees[childId];
            if (remaining == 0)
                AddReady(_byId[childId]);
        }
    }

    private void AddReady(ResourceTriggerGraphNode node)
    {
        if (_requirementsSatisfied(node))
            _runnable.Add(node);
        else
            _skipped.Add(node);
    }

    private static bool TryTakePreferred(
        SortedSet<ResourceTriggerGraphNode> ready,
        IReadOnlySet<string> preferredOperationIds,
        out ResourceTriggerGraphNode node)
    {
        var candidate = ready.FirstOrDefault(value =>
            preferredOperationIds.Contains(value.OperationId));
        if (candidate == null)
        {
            node = null!;
            return false;
        }
        ready.Remove(candidate);
        node = candidate;
        return true;
    }
}
