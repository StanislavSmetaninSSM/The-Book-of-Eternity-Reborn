using System.Collections.ObjectModel;
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
    private readonly EffectAcceptedTurnPlanner.EffectResourceTriggerExecution[]
        _initialTriggerExecutions;

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
        IReadOnlyList<EffectAcceptedTurnPlanner.EffectResourceTriggerExecution>?
            InitialTriggerExecutions = null)
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
        ArgumentNullException.ThrowIfNull(Mutations);
        _mutations = Mutations.Select(Clone).ToArray();
        _capacityTransitions = CapacityTransitions?.Select(value =>
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(value.Coordinate);
            ArgumentNullException.ThrowIfNull(value.SourceEvidence);
            return value;
        }).ToArray() ?? Array.Empty<ResourceCapacityIntent>();
        _initialTriggerExecutions = (InitialTriggerExecutions ??
            Array.Empty<EffectAcceptedTurnPlanner.EffectResourceTriggerExecution>())
            .Select(Clone)
            .ToArray();
    }

    internal int Turn { get; }
    internal int ExecutionSequenceOffset { get; }
    internal ResourceDefinitionCatalog Definitions { get; }
    internal ResourceStateLedger State { get; }
    internal ResourceHistoryState History { get; }
    internal ResourceMutationSourceCatalog Sources { get; }
    internal ResourceEventMutationResolver? EventMutationResolver { get; }
    internal IReadOnlyList<EffectAcceptedTurnPlanner.EffectResourceTriggerExecution>
        InitialTriggerExecutions =>
        Array.AsReadOnly(_initialTriggerExecutions.Select(Clone).ToArray());
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

    private static EffectAcceptedTurnPlanner.EffectResourceTriggerExecution Clone(
        EffectAcceptedTurnPlanner.EffectResourceTriggerExecution value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(value.MutationKeys);
        return value with { MutationKeys = value.MutationKeys.ToArray() };
    }
}

internal sealed record AcceptedMechanicsPlannerStatistics(
    int HistorySeedCount,
    int HistoryAppendCount,
    int HistoryFreezeCount);

internal sealed class AcceptedMechanicsResourcePlanningResult
{
    private readonly ResourceAppliedEvent[] _events;
    private readonly ResourceTransition[] _appliedTransitions;
    private readonly ResourceTransition[] _replayTransitions;
    private readonly EffectAcceptedTurnPlanner.EffectResourceTriggerExecution[]
        _resourceTriggerExecutions;
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
            resourceTriggerExecutions = null)
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
                MutationKeys = execution.MutationKeys.ToArray()
            })
            .ToArray();
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
                MutationKeys = execution.MutationKeys.ToArray()
            })
            .ToArray());
    internal IReadOnlyList<ValidationIssue> Issues =>
        Array.AsReadOnly(_issues.ToArray());
    internal AcceptedMechanicsPlannerStatistics Statistics { get; }
    internal bool IsValid =>
        StateAfterImage != null && HistoryAfterImage != null && Issues.Count == 0;
}

internal sealed class AcceptedMechanicsIdentityFactory
{
    private readonly Func<Guid> _guidFactory;

    internal AcceptedMechanicsIdentityFactory(Func<Guid>? guidFactory = null) =>
        _guidFactory = guidFactory ?? Guid.NewGuid;

    internal string CreateOperationId() =>
        "resource_operation_" + _guidFactory().ToString("N");

    internal string CreateTransitionId() =>
        "resource_transition_" + _guidFactory().ToString("N");
}

internal static class AcceptedMechanicsPlanner
{
    private sealed record PendingBoundaryDecision(
        bool AwaitingReceipt,
        ResourcePendingResolutionState? StateAfterImage,
        JsonObject? SafeGmPacket,
        IReadOnlyList<ResourceMutationSourceExport> SourceExports,
        IReadOnlyList<ResourceMutationIntent> Mutations,
        IReadOnlyList<EffectAcceptedTurnPlanner.EffectResourceTriggerExecution>
            GraphTriggerExecutions,
        IReadOnlyList<EffectAcceptedTurnPlanner.EffectResourceTriggerExecution>
            PostGraphTriggerExecutions,
        IReadOnlyList<ValidationIssue> Issues)
    {
        internal bool IsValid => Issues.Count == 0;
    }

    internal static AcceptedMechanicsPlanningResult BuildAcceptedPlan(
        AcceptedMechanicsInput input,
        string inputFingerprint)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFingerprint);
        var context = input.PlanningContext;
        if (context == null)
        {
            return new AcceptedMechanicsPlanningResult(
                null,
                Issue(
                    "accepted_mechanics_planning_context_missing",
                    "validated typed mechanics planning context",
                    "missing"));
        }

        var issues = new List<ValidationIssue>();
        var definitions = context.Definitions;
        var sameTurnDefinitions = new Dictionary<string, ResourceDefinition>(
            StringComparer.Ordinal);
        foreach (var creation in context.Commands.DefinitionCreations
                     .OrderBy(static value => value.CommandOrdinal))
        {
            using var proposal = JsonDocument.Parse(creation.Definition.ToJsonString());
            var materialized = ResourceDefinitionCatalog.MaterializeProposal(
                proposal.RootElement,
                definitions,
                input.Turn,
                creation.EventRef,
                static () => new ResourceDefinitionIdentity(
                    "resource_definition_" + Guid.NewGuid().ToString("N"),
                    "resource_definition_seal_" + Guid.NewGuid().ToString("N")));
            issues.AddRange(materialized.Issues);
            if (!materialized.IsValid || materialized.Definition == null)
                continue;
            definitions = definitions.With(materialized.Definition);
            sameTurnDefinitions.Add(creation.DefinitionRef, materialized.Definition);
        }

        var capacityTransitions = ComposeCapacityTransitions(
            input,
            context,
            definitions,
            sameTurnDefinitions,
            issues);
        var effectPlan = context.EffectPlan;
        var periodicResolution = effectPlan == null
            ? new EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution(
                Array.Empty<ResourceMutationSourceExport>(),
                Array.Empty<ResourceMutationIntent>(),
                Array.Empty<ValidationIssue>())
            : EffectAcceptedTurnPlanner.ResolveDuePeriodicResourceMutations(
                effectPlan,
                context.Owners,
                definitions);
        issues.AddRange(periodicResolution.Issues);
        var boundedResolutions = periodicResolution.PendingResolutions.ToList();
        var sources = context.Sources;
        if (periodicResolution.SourceExports.Count != 0)
        {
            var sourceResult = ResourceMutationSourceCatalog.Create(
                context.Sources.Exports.Concat(periodicResolution.SourceExports));
            issues.AddRange(sourceResult.Issues);
            if (sourceResult.Catalog != null)
                sources = sourceResult.Catalog;
        }
        var mutations = ComposeOrdinaryMutations(
            input,
            context,
            definitions,
            issues)
            .Concat(context.RegisteredSystemOutcomes.SelectMany(static outcome => outcome.Mutations))
            .Concat(periodicResolution.Mutations)
            .ToArray();
        if (issues.Count != 0)
            return new AcceptedMechanicsPlanningResult(null, issues);

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
            boundedResolutions.AddRange(expansion.PendingResolutions);
            return expansion;
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
                EventMutationResolver: effectPlan == null
                    ? null
                    : ResolveAndCollect,
                InitialTriggerExecutions: periodicResolution.TriggerExecutions),
            new AcceptedMechanicsIdentityFactory());
        if (!discoveryResourceResult.IsValid ||
            discoveryResourceResult.StateAfterImage == null ||
            discoveryResourceResult.HistoryAfterImage == null)
        {
            return new AcceptedMechanicsPlanningResult(
                null,
                discoveryResourceResult.Issues);
        }

        var pendingDecision = ResolvePendingBoundary(
            input,
            context,
            definitions,
            boundedResolutions,
            issues);
        if (!pendingDecision.IsValid)
            return new AcceptedMechanicsPlanningResult(null, pendingDecision.Issues);
        if (pendingDecision.AwaitingReceipt)
        {
            return BuildAwaitingReceiptPlan(
                input,
                inputFingerprint,
                context,
                pendingDecision,
                effectPlan);
        }

        var resourceResult = discoveryResourceResult;
        if (pendingDecision.Mutations.Count != 0)
        {
            var receiptSources = ResourceMutationSourceCatalog.Create(
                sources.Exports.Concat(pendingDecision.SourceExports));
            if (receiptSources.Catalog == null || receiptSources.Issues.Count != 0)
            {
                return new AcceptedMechanicsPlanningResult(
                    null,
                    receiptSources.Issues);
            }
            sources = receiptSources.Catalog;
            var actualBoundedResolutions = new List<
                EffectAcceptedTurnPlanner.EffectBoundedResourceResolution>();
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
                actualBoundedResolutions.AddRange(expansion.PendingResolutions);
                return expansion;
            }
            resourceResult = BuildResources(
                new AcceptedMechanicsResourceInput(
                    input.Turn,
                    definitions,
                    context.State,
                    context.History,
                    sources,
                    mutations.Concat(pendingDecision.Mutations).ToArray(),
                    capacityTransitions,
                    EventMutationResolver: effectPlan == null ? null : ResolveActual,
                    InitialTriggerExecutions: periodicResolution.TriggerExecutions
                        .Concat(pendingDecision.GraphTriggerExecutions)
                        .ToArray()),
                new AcceptedMechanicsIdentityFactory());
            if (!resourceResult.IsValid ||
                resourceResult.StateAfterImage == null ||
                resourceResult.HistoryAfterImage == null)
            {
                return new AcceptedMechanicsPlanningResult(null, resourceResult.Issues);
            }
        }
        if (effectPlan != null)
        {
            var finalizedEffects = EffectAcceptedTurnPlanner.FinalizeAfterResourceGraph(
                effectPlan,
                resourceResult.ResourceTriggerExecutions
                    .Concat(pendingDecision.PostGraphTriggerExecutions)
                    .ToArray(),
                new EffectIdentityFactory());
            if (!finalizedEffects.Success || finalizedEffects.Plan == null)
            {
                return new AcceptedMechanicsPlanningResult(
                    null,
                    finalizedEffects.Issues);
            }
            effectPlan = finalizedEffects.Plan;
        }
        var ownerAgreementIssues = context.Owners.ValidateCanonicalAgreement(
            resourceResult.StateAfterImage,
            resourceResult.HistoryAfterImage);
        if (ownerAgreementIssues.Count != 0)
            return new AcceptedMechanicsPlanningResult(null, ownerAgreementIssues);

        var definitionAfterImage = definitions.ToCanonicalRoot();
        var stateAfterImage = JsonNode.Parse(
            resourceResult.StateAfterImage.ToCanonicalJson())!.AsObject();
        var historyAfterImage = JsonNode.Parse(
            resourceResult.HistoryAfterImage.ToCanonicalJson())!.AsObject();
        var carriers = effectPlan?.CarrierAfterImages ??
            new Dictionary<string, JsonObject>(StringComparer.Ordinal);
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
            return new AcceptedMechanicsPlanningResult(null, issues);
        foreach (var carrierPath in carriers.Keys)
            ownerCompanionAfterImages.Remove(carrierPath);
        foreach (var transitionPath in ownerTransitions
                     .Select(static value => value.Path)
                     .Distinct(StringComparer.Ordinal))
        {
            if (ownerCompanionAfterImages.ContainsKey(transitionPath) ||
                carriers.ContainsKey(transitionPath))
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
            EffectAcceptedTurnPlan.IdentityIndexPath
        };
        if (!context.Commands.IsMissing)
            touched.Add(ResourceMaterializationContract.CommandPath);
        if (effectPlan != null)
        {
            touched.UnionWith(effectPlan.TouchedPaths);
            touched.UnionWith(effectPlan.DeletedPaths);
        }
        touched.UnionWith(ownerCompanionAfterImages.Keys);
        touched.UnionWith(ownerTransitions.Select(static value => value.Path));
        if (pendingDecision.StateAfterImage != null)
            touched.Add(ResourcePendingResolutionState.PendingPath);
        var consumed = new HashSet<string>(StringComparer.Ordinal);
        if (!context.Commands.IsMissing)
            consumed.Add(ResourceMaterializationContract.CommandPath);
        if (effectPlan != null)
            consumed.UnionWith(effectPlan.DeletedPaths);

        return new AcceptedMechanicsPlanningResult(
            new AcceptedMechanicsPlan(
                inputFingerprint,
                definitionAfterImage,
                stateAfterImage,
                historyAfterImage,
                carriers,
                effectIdentity,
                pendingDecision.StateAfterImage == null
                    ? new Dictionary<string, JsonObject?>()
                    : new Dictionary<string, JsonObject?>
                    {
                        [ResourcePendingResolutionState.PendingPath] =
                            pendingDecision.StateAfterImage.ToCanonicalRoot()
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
                ownerTransitions),
            Array.Empty<ValidationIssue>());
    }

    private static PendingBoundaryDecision ResolvePendingBoundary(
        AcceptedMechanicsInput input,
        AcceptedMechanicsPlanningContext context,
        ResourceDefinitionCatalog definitions,
        IReadOnlyList<EffectAcceptedTurnPlanner.EffectBoundedResourceResolution>
            rawCandidates,
        List<ValidationIssue> planningIssues)
    {
        var issues = new List<ValidationIssue>();
        var fullTurnFingerprint = CreatePendingFullTurnFingerprint(input);
        var candidates = NormalizePendingCandidates(rawCandidates, issues);
        var receipts = input.EffectCommands["effectResolutionReceipts"] as JsonArray;
        if (input.EffectCommands.ContainsKey("effectResolutionReceipts") && receipts == null)
        {
            issues.AddRange(Issue(
                "resource_pending_receipts_invalid",
                "effectResolutionReceipts array",
                input.EffectCommands["effectResolutionReceipts"]?.ToJsonString() ?? "null"));
        }
        receipts ??= new JsonArray();
        if (issues.Count != 0)
            return FailedPendingDecision(issues);

        var pendingState = context.PendingResolutionState;
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
                    .Where(candidate => PendingCandidateMatches(request, candidate))
                    .ToArray();
                if (matches.Length != 1)
                {
                    issues.AddRange(Issue(
                        "resource_pending_authority_changed",
                        "one exact recomputed bounded effect authority for pending request",
                        request.RequestId));
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
            var replay = pendingState.Resolve(
                receipts,
                new ResourcePendingResolutionContext(
                    input.SessionId,
                    input.RequestId,
                    input.Turn,
                    fullTurnFingerprint),
                definitions);
            if (!replay.IsValid || replay.StateAfterImage == null)
                return FailedPendingDecision(replay.Issues);
            return new PendingBoundaryDecision(
                AwaitingReceipt: false,
                replay.StateAfterImage,
                SafeGmPacket: null,
                replay.SourceExports,
                replay.Mutations,
                Array.Empty<EffectAcceptedTurnPlanner.EffectResourceTriggerExecution>(),
                Array.Empty<EffectAcceptedTurnPlanner.EffectResourceTriggerExecution>(),
                Array.Empty<ValidationIssue>());
        }

        if (pendingState != null)
        {
            var terminalEventRefs = pendingState.TerminalReceipts
                .Where(terminal =>
                    string.Equals(terminal.SessionId, input.SessionId, StringComparison.Ordinal) &&
                    string.Equals(
                        terminal.AcceptedRequestId,
                        input.RequestId,
                        StringComparison.Ordinal) &&
                    terminal.RequestTurn == input.Turn &&
                    string.Equals(
                        terminal.FullTurnFingerprint,
                        fullTurnFingerprint,
                        StringComparison.Ordinal))
                .Select(static terminal => terminal.EventRef)
                .ToHashSet(StringComparer.Ordinal);
            candidates = candidates
                .Where(candidate => !terminalEventRefs.Contains(candidate.EventRef))
                .ToArray();
        }

        if (candidates.Count == 0)
        {
            return new PendingBoundaryDecision(
                AwaitingReceipt: false,
                StateAfterImage: null,
                SafeGmPacket: null,
                Array.Empty<ResourceMutationSourceExport>(),
                Array.Empty<ResourceMutationIntent>(),
                Array.Empty<EffectAcceptedTurnPlanner.EffectResourceTriggerExecution>(),
                Array.Empty<EffectAcceptedTurnPlanner.EffectResourceTriggerExecution>(),
                Array.Empty<ValidationIssue>());
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
                candidate.SafeSourceLabel,
                candidate.SafeTargetLabel,
                candidate.SafeResourceLabel,
                candidate.SafeOperationLabel)).ToArray();
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
            Array.Empty<ResourceMutationSourceExport>(),
            Array.Empty<ResourceMutationIntent>(),
            Array.Empty<EffectAcceptedTurnPlanner.EffectResourceTriggerExecution>(),
            Array.Empty<EffectAcceptedTurnPlanner.EffectResourceTriggerExecution>(),
            Array.Empty<ValidationIssue>());
    }

    private static PendingBoundaryDecision BuildResolvedPendingDecision(
        ResourcePendingResolutionResult resolved,
        IReadOnlyList<(
            ResourcePendingRequest Request,
            EffectAcceptedTurnPlanner.EffectBoundedResourceResolution Candidate)> bindings,
        List<ValidationIssue> issues)
    {
        var bindingByRequest = bindings.ToDictionary(
            static value => value.Request.RequestId,
            static value => value.Candidate,
            StringComparer.Ordinal);
        var mutations = new List<ResourceMutationIntent>();
        foreach (var mutation in resolved.Mutations)
        {
            if (!bindingByRequest.TryGetValue(
                    mutation.Source.SourceId,
                    out var candidate))
            {
                issues.AddRange(Issue(
                    "resource_pending_receipt_binding_missing",
                    "one recomputed bounded candidate for every restored mutation",
                    mutation.Source.SourceId));
                continue;
            }
            mutations.Add(mutation with
            {
                EventRef = candidate.EventRef,
                Coordinate = candidate.Coordinate with { },
                Source = mutation.Source with
                {
                    Operation = candidate.Operation
                },
                Dependencies = candidate.Dependencies.ToArray(),
                EventRequirements = candidate.EventRequirements.ToArray(),
                ResultConstraint = candidate.ResultConstraint
            });
        }
        if (issues.Count != 0)
            return FailedPendingDecision(issues);

        var graphExecutions = new List<
            EffectAcceptedTurnPlanner.EffectResourceTriggerExecution>();
        var postGraphExecutions = new List<
            EffectAcceptedTurnPlanner.EffectResourceTriggerExecution>();
        foreach (var group in bindings.GroupBy(value => (
                     value.Candidate.EffectId,
                     value.Candidate.TriggerId,
                     value.Candidate.EventKind,
                     value.Candidate.EventRef)))
        {
            var budgets = group.Select(static value => value.Candidate.RemainingUseBudget)
                .Distinct()
                .ToArray();
            if (budgets.Length != 1)
            {
                issues.AddRange(Issue(
                    "resource_pending_trigger_budget_conflict",
                    "one exact remaining-use budget per bounded trigger activation",
                    group.Key.ToString()));
                continue;
            }
            var requestIds = group.Select(static value => value.Request.RequestId)
                .ToHashSet(StringComparer.Ordinal);
            var keys = mutations
                .Where(mutation => requestIds.Contains(mutation.Source.SourceId))
                .Select(static mutation => mutation.Key)
                .ToArray();
            var execution = new EffectAcceptedTurnPlanner.EffectResourceTriggerExecution(
                group.Key.EffectId,
                group.Key.TriggerId,
                group.Key.EventKind,
                group.Key.EventRef,
                keys,
                budgets[0]);
            if (keys.Length == 0)
                postGraphExecutions.Add(execution);
            else
                graphExecutions.Add(execution);
        }
        if (issues.Count != 0)
            return FailedPendingDecision(issues);

        return new PendingBoundaryDecision(
            AwaitingReceipt: false,
            resolved.StateAfterImage,
            SafeGmPacket: null,
            resolved.SourceExports,
            mutations,
            graphExecutions,
            postGraphExecutions,
            Array.Empty<ValidationIssue>());
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
            var key = string.Join(
                "\0",
                candidate.EventRef,
                candidate.EffectId,
                candidate.TriggerId,
                candidate.Coordinate.Realm,
                ((int)candidate.Coordinate.OwnerKind).ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                candidate.Coordinate.ResourceOwnerId,
                candidate.Coordinate.ResourceKey,
                ((int)candidate.Operation).ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                candidate.SourceAuthorityFingerprint,
                candidate.PolicyFingerprint);
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

    private static string CreatePendingFullTurnFingerprint(
        AcceptedMechanicsInput input)
    {
        var effectCommands = input.EffectCommands;
        effectCommands["effectResolutionReceipts"] = new JsonArray();
        using var builder = new ResourceFingerprintBuilder(
            "accepted-mechanics-pending-full-turn-v1");
        builder.Append(input.SessionId);
        builder.Append(input.RequestId);
        builder.Append(input.Realm);
        builder.Append(input.Turn);
        builder.Append(input.AcceptedEvents.ToJsonString());
        builder.Append(input.ResourceCommands.ToJsonString());
        builder.Append(effectCommands.ToJsonString());
        return builder.Build();
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
                pendingGmPacket: pending.SafeGmPacket),
            Array.Empty<ValidationIssue>());
    }

    private static PendingBoundaryDecision FailedPendingDecision(
        IEnumerable<ValidationIssue> issues) =>
        new(
            AwaitingReceipt: false,
            StateAfterImage: null,
            SafeGmPacket: null,
            Array.Empty<ResourceMutationSourceExport>(),
            Array.Empty<ResourceMutationIntent>(),
            Array.Empty<EffectAcceptedTurnPlanner.EffectResourceTriggerExecution>(),
            Array.Empty<EffectAcceptedTurnPlanner.EffectResourceTriggerExecution>(),
            issues.ToArray());

    private static IReadOnlyList<ResourceCapacityIntent> ComposeCapacityTransitions(
        AcceptedMechanicsInput input,
        AcceptedMechanicsPlanningContext context,
        ResourceDefinitionCatalog definitions,
        IReadOnlyDictionary<string, ResourceDefinition> sameTurnDefinitions,
        List<ValidationIssue> issues)
    {
        var result = ComposeOwnerCapacityTransitions(input, context, issues).ToList();
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
        List<ValidationIssue> issues)
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

        foreach (var entry in state.Entries
                     .OrderBy(static value => value.Coordinate.Realm, StringComparer.Ordinal)
                     .ThenBy(static value => value.Coordinate.OwnerKind)
                     .ThenBy(static value => value.Coordinate.ResourceOwnerId, StringComparer.Ordinal)
                     .ThenBy(static value => value.Coordinate.ResourceKey, StringComparer.Ordinal))
        {
            var ownerKey = new ResourceOwnerKey(
                entry.Coordinate.Realm,
                entry.Coordinate.OwnerKind,
                entry.Coordinate.ResourceOwnerId);
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
            foreach (var entry in state.Entries
                         .Where(entry =>
                             string.Equals(
                                 entry.Coordinate.Realm,
                                 terminal.Realm,
                                 StringComparison.Ordinal) &&
                             entry.Coordinate.OwnerKind == terminal.OwnerKind &&
                             string.Equals(
                                 entry.Coordinate.ResourceOwnerId,
                                 terminal.ResourceOwnerId,
                                 StringComparison.Ordinal))
                         .OrderBy(static entry => entry.Coordinate.ResourceKey, StringComparer.Ordinal))
            {
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

    internal static AcceptedMechanicsResourcePlanningResult BuildResources(
        AcceptedMechanicsResourceInput input,
        AcceptedMechanicsIdentityFactory identityFactory)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(identityFactory);
        var agreementIssues = input.History.ValidateStateAgreement(input.State);
        if (agreementIssues.Count != 0)
            return Failure(agreementIssues, Statistics());
        if (input.CapacityTransitions.Count >
            ResourceMaterializationContract.MaxCapacityTransitionsPerTurn)
        {
            return Failure(
                Issue(
                    "resource_planner_capacity_limit_exceeded",
                    $"at most {ResourceMaterializationContract.MaxCapacityTransitionsPerTurn} capacity transitions",
                    input.CapacityTransitions.Count.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)),
                Statistics());
        }

        var identityRegistry = new AllocatedIdentityRegistry();
        var capacityPreparation = PrepareCapacityTransitions(
            input.CapacityTransitions,
            identityFactory,
            identityRegistry);
        if (capacityPreparation.Issues.Count != 0)
            return Failure(capacityPreparation.Issues, Statistics());
        var preparation = PrepareMutations(
            input,
            identityFactory,
            identityRegistry);
        if (preparation.Issues.Count != 0)
            return Failure(preparation.Issues, Statistics());
        var preparedMutations = preparation.Mutations;
        if (preparedMutations.Count(static value =>
                value.Route.Phase != ResourceMutationPhase.EffectTrigger) >
            ResourceMaterializationContract.MaxMutationsBeforeTriggers)
        {
            return Failure(
                Issue(
                    "resource_planner_mutation_limit_exceeded",
                    $"at most {ResourceMaterializationContract.MaxMutationsBeforeTriggers} pre-trigger mutations",
                    preparedMutations.Count(static value =>
                        value.Route.Phase != ResourceMutationPhase.EffectTrigger).ToString(
                            System.Globalization.CultureInfo.InvariantCulture)),
                Statistics());
        }

        var graphPreparation = PrepareCompleteResourceGraph(
            input,
            preparedMutations,
            identityFactory,
            identityRegistry);
        if (!graphPreparation.IsValid)
            return Failure(graphPreparation.Issues, Statistics());
        preparedMutations = graphPreparation.Mutations;
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
                return Failure(result.Issues, Statistics(workingHistory));
            workingLedger = result.WorkingLedger!;
            if (result.Transition != null)
                appliedTransitions.Add(result.Transition);
            else
                replayTransitions.Add(result.ReplayTransition!);
        }

        var directBaselineState = workingLedger.Freeze();
        ResourceStateLedger? postDirectState = null;

        var preparedByKey = preparedMutations.ToDictionary(
            static value => value.Intent.Key);
        var byOperationId = preparedMutations.ToDictionary(
            static value => value.OperationId,
            StringComparer.Ordinal);
        var pending = preparedMutations.ToDictionary(
            static value => value.OperationId,
            StringComparer.Ordinal);
        var completed = new HashSet<string>(StringComparer.Ordinal);
        var producedEvents = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var resourceTriggerExecutions = new List<
            EffectAcceptedTurnPlanner.EffectResourceTriggerExecution>();
        var triggerExecutionByMutation = new Dictionary<
            ResourceOperationKey,
            EffectAcceptedTurnPlanner.EffectResourceTriggerExecution>(
                graphPreparation.TriggerExecutionsByMutation);
        var triggerActivationDecisions = new Dictionary<string, bool>(StringComparer.Ordinal);
        var reservedTriggerUses = new Dictionary<string, int>(StringComparer.Ordinal);
        var recordedTriggerActivations = new HashSet<string>(StringComparer.Ordinal);
        while (pending.Count != 0)
        {
            var ready = pending.Values
                .Where(candidate => candidate.DependencyOperationIds.Values
                    .All(completed.Contains))
                .OrderBy(static candidate => candidate.Route.Phase)
                .ThenBy(static candidate => candidate.Route.Priority)
                .ThenBy(
                    static candidate => candidate.Intent.Source.SourceId,
                    StringComparer.Ordinal)
                .ThenBy(static candidate => candidate.OperationId, StringComparer.Ordinal)
                .ToArray();
            if (ready.Length == 0)
            {
                return Failure(
                    Issue(
                        "resource_graph_cycle",
                        "one acyclic resource dependency graph",
                        $"{pending.Count} unresolved node(s)"),
                    Statistics(workingHistory));
            }

            var skipped = ready
                .Where(candidate => !RequirementsSatisfied(candidate, producedEvents))
                .ToArray();
            if (skipped.Length != 0)
            {
                foreach (var candidate in skipped)
                {
                    pending.Remove(candidate.OperationId);
                    completed.Add(candidate.OperationId);
                    producedEvents[candidate.OperationId] = new HashSet<string>(
                        StringComparer.Ordinal);
                }
                continue;
            }

            var prepared = ready[0];

            EffectAcceptedTurnPlanner.EffectResourceTriggerExecution? triggerExecution = null;
            string? triggerExecutionKey = null;
            if (triggerExecutionByMutation.TryGetValue(
                    prepared.Intent.Key,
                    out triggerExecution))
            {
                triggerExecutionKey = TriggerExecutionKey(triggerExecution);
                if (!triggerActivationDecisions.TryGetValue(
                        triggerExecutionKey,
                        out var activationAllowed))
                {
                    activationAllowed = true;
                    if (triggerExecution.RemainingUseBudget.HasValue)
                    {
                        var reserved = reservedTriggerUses.GetValueOrDefault(
                            triggerExecution.EffectId);
                        activationAllowed = reserved <
                            triggerExecution.RemainingUseBudget.Value;
                        if (activationAllowed)
                        {
                            reservedTriggerUses[triggerExecution.EffectId] = reserved + 1;
                        }
                    }
                    triggerActivationDecisions.Add(
                        triggerExecutionKey,
                        activationAllowed);
                }
                if (!activationAllowed)
                {
                    pending.Remove(prepared.OperationId);
                    completed.Add(prepared.OperationId);
                    producedEvents[prepared.OperationId] = new HashSet<string>(
                        StringComparer.Ordinal);
                    continue;
                }
            }

            var mutation = prepared.Intent;
            if (prepared.Route.Phase >= ResourceMutationPhase.RegisteredSystemOutcome &&
                postDirectState == null)
            {
                postDirectState = workingLedger.Freeze();
            }
            var amountResult = ResolveMutationAmount(
                input.History,
                input.Definitions,
                directBaselineState,
                postDirectState,
                prepared);
            if (amountResult.Issues.Count != 0)
                return Failure(amountResult.Issues, Statistics(workingHistory));
            if (!amountResult.ShouldApply)
            {
                pending.Remove(prepared.OperationId);
                completed.Add(prepared.OperationId);
                producedEvents[prepared.OperationId] = new HashSet<string>(
                    StringComparer.Ordinal);
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
                return Failure(result.Issues, Statistics(workingHistory));
            workingLedger = result.WorkingLedger!;
            if (result.Transition != null)
            {
                appliedTransitions.Add(result.Transition);
                if (triggerExecution != null &&
                    recordedTriggerActivations.Add(triggerExecutionKey!))
                {
                    resourceTriggerExecutions.Add(triggerExecution);
                }
                events.AddRange(result.Events);
                producedEvents[prepared.OperationId] = result.Events
                    .Select(static value => value.EventKind)
                    .ToHashSet(StringComparer.Ordinal);
            }
            else
            {
                replayTransitions.Add(result.ReplayTransition!);
                producedEvents[prepared.OperationId] = new HashSet<string>(StringComparer.Ordinal);
            }

            pending.Remove(prepared.OperationId);
            completed.Add(prepared.OperationId);
        }

        var stateAfterImage = workingLedger.Freeze();
        var frozen = workingHistory.Freeze(input.Definitions);
        if (!frozen.IsValid || frozen.History == null)
            return Failure(frozen.Issues, Statistics(workingHistory));
        var finalAgreement = frozen.History.ValidateStateAgreement(stateAfterImage);
        if (finalAgreement.Count != 0)
            return Failure(finalAgreement, Statistics(workingHistory));

        return new AcceptedMechanicsResourcePlanningResult(
            stateAfterImage,
            frozen.History,
            events,
            appliedTransitions,
            replayTransitions,
            Array.Empty<ValidationIssue>(),
            Statistics(workingHistory),
            resourceTriggerExecutions);
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
        var triggerExecutionsByMutation = new Dictionary<
            ResourceOperationKey,
            EffectAcceptedTurnPlanner.EffectResourceTriggerExecution>();
        var triggerUseBudgets = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var execution in input.InitialTriggerExecutions)
        {
            if (execution.MutationKeys.Count == 0)
            {
                return CompleteGraphFailure(Issue(
                    "effect_resource_trigger_execution_invalid",
                    "one or more exact mutation keys per trigger activation",
                    TriggerExecutionKey(execution)));
            }
            if (!TryRegisterTriggerUseBudget(execution, triggerUseBudgets))
            {
                return CompleteGraphFailure(Issue(
                    "effect_resource_trigger_budget_conflict",
                    "one exact positive remaining-use budget per effect",
                    TriggerExecutionKey(execution)));
            }
            foreach (var key in execution.MutationKeys)
            {
                if (!preparedByKey.ContainsKey(key) ||
                    !triggerExecutionsByMutation.TryAdd(key, execution))
                {
                    return CompleteGraphFailure(Issue(
                        "effect_resource_trigger_execution_invalid",
                        "one exact trigger activation for every initial trigger mutation",
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
            if (triggerExecutionsByMutation.TryGetValue(
                    mutation.Intent.Key,
                    out var execution))
            {
                var lineageDecision = AdvanceTriggerLineage(lineage, execution);
                if (lineageDecision.Issues.Count != 0)
                    return CompleteGraphFailure(lineageDecision.Issues);
                if (lineageDecision.Allowed)
                    lineage = lineageDecision.Lineage;
            }
            operationLineages.Add(mutation.OperationId, lineage);
        }

        if (input.EventMutationResolver == null)
        {
            return new CompleteResourceGraphPreparation(
                prepared,
                initialGraph.Graph,
                triggerExecutionsByMutation,
                Array.Empty<ValidationIssue>());
        }

        var sourceExports = input.Sources.Exports.ToDictionary(
            static source => (source.SourceKind, source.SourceId));
        var workingSources = input.Sources;
        var frontier = new Queue<PreparedMutation>(
            initialGraph.Graph.OrderedNodes.Select(node =>
                preparedByOperationId[node.OperationId]));
        while (frontier.Count != 0)
        {
            var producer = frontier.Dequeue();
            foreach (var eventKind in PotentialResourceEventKinds(
                         producer.Intent.Source.Operation))
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
                if (!expansion.IsValid)
                    return CompleteGraphFailure(expansion.Issues);
                if (expansion.Mutations.Count == 0)
                    continue;

                var expansionKeys = new HashSet<ResourceOperationKey>();
                foreach (var mutation in expansion.Mutations)
                {
                    if (!expansionKeys.Add(mutation.Key) ||
                        preparedByKey.ContainsKey(mutation.Key))
                    {
                        return CompleteGraphFailure(Issue(
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
                        return CompleteGraphFailure(Issue(
                            "effect_resource_trigger_execution_invalid",
                            "every expanded mutation bound to its exact producer event",
                            Describe(mutation.Key)));
                    }
                }

                var activationByMutation = new Dictionary<
                    ResourceOperationKey,
                    EffectAcceptedTurnPlanner.EffectResourceTriggerExecution>();
                var activationDecisions = new Dictionary<
                    string,
                    TriggerLineageDecision>(StringComparer.Ordinal);
                foreach (var execution in expansion.TriggerExecutions)
                {
                    if (execution.MutationKeys.Count == 0)
                    {
                        return CompleteGraphFailure(Issue(
                            "effect_resource_trigger_execution_invalid",
                            "one or more exact mutation keys per trigger activation",
                            TriggerExecutionKey(execution)));
                    }
                    if (!TryRegisterTriggerUseBudget(execution, triggerUseBudgets))
                    {
                        return CompleteGraphFailure(Issue(
                            "effect_resource_trigger_budget_conflict",
                            "one exact positive remaining-use budget per effect",
                            TriggerExecutionKey(execution)));
                    }

                    var executionKey = TriggerExecutionKey(execution);
                    if (!activationDecisions.TryGetValue(executionKey, out var decision))
                    {
                        decision = AdvanceTriggerLineage(
                            operationLineages[producer.OperationId],
                            execution);
                        if (decision.Issues.Count != 0)
                            return CompleteGraphFailure(decision.Issues);
                        activationDecisions.Add(executionKey, decision);
                    }
                    foreach (var key in execution.MutationKeys)
                    {
                        if (!expansionKeys.Contains(key) ||
                            !activationByMutation.TryAdd(key, execution))
                        {
                            return CompleteGraphFailure(Issue(
                                "effect_resource_trigger_execution_invalid",
                                "one exact trigger activation per expanded mutation",
                                Describe(key)));
                        }
                    }
                }

                var missingActivation = expansion.Mutations.FirstOrDefault(mutation =>
                    !activationByMutation.ContainsKey(mutation.Key));
                if (missingActivation != null)
                {
                    return CompleteGraphFailure(Issue(
                        "effect_resource_trigger_execution_missing",
                        "one exact trigger activation for every expanded mutation",
                        Describe(missingActivation.Key)));
                }
                var includedMutations = expansion.Mutations
                    .Where(mutation =>
                        activationByMutation.TryGetValue(mutation.Key, out var execution) &&
                        activationDecisions[TriggerExecutionKey(execution)].Allowed)
                    .ToArray();
                if (includedMutations.Length == 0)
                    continue;
                if (preparedByKey.Count + includedMutations.Length >
                    ResourceMaterializationContract.MaxTriggerNodes)
                {
                    return CompleteGraphFailure(Issue(
                        "resource_graph_node_limit_exceeded",
                        $"at most {ResourceMaterializationContract.MaxTriggerNodes} graph nodes",
                        (preparedByKey.Count + includedMutations.Length)
                            .ToString(System.Globalization.CultureInfo.InvariantCulture)));
                }

                foreach (var source in expansion.SourceExports)
                {
                    var sourceKey = (source.SourceKind, source.SourceId);
                    if (sourceExports.TryGetValue(sourceKey, out var existing))
                    {
                        if (existing != source)
                        {
                            return CompleteGraphFailure(Issue(
                                "effect_resource_source_conflict",
                                "one exact source policy per effect/trigger/component",
                                source.SourceKind + "/" + source.SourceId));
                        }
                        continue;
                    }
                    sourceExports.Add(sourceKey, source);
                }
                var sourceResult = ResourceMutationSourceCatalog.Create(sourceExports.Values);
                if (!sourceResult.IsValid || sourceResult.Catalog == null)
                    return CompleteGraphFailure(sourceResult.Issues);
                workingSources = sourceResult.Catalog;

                var additional = PrepareMutations(
                    new AcceptedMechanicsResourceInput(
                        input.Turn,
                        input.Definitions,
                        input.State,
                        input.History,
                        workingSources,
                        includedMutations),
                    identityFactory,
                    identityRegistry);
                if (additional.Issues.Count != 0)
                    return CompleteGraphFailure(additional.Issues);
                foreach (var extra in additional.Mutations)
                {
                    var activation = activationByMutation[extra.Intent.Key];
                    var decision = activationDecisions[TriggerExecutionKey(activation)];
                    prepared.Add(extra);
                    preparedByKey.Add(extra.Intent.Key, extra);
                    preparedByOperationId.Add(extra.OperationId, extra);
                    operationLineages.Add(
                        extra.OperationId,
                        new Dictionary<string, int>(decision.Lineage, StringComparer.Ordinal));
                    triggerExecutionsByMutation.Add(extra.Intent.Key, activation);
                    frontier.Enqueue(extra);
                }
            }
        }

        var graph = BuildGraph(prepared);
        return graph.IsValid
            ? new CompleteResourceGraphPreparation(
                prepared,
                graph.Graph,
                triggerExecutionsByMutation,
                Array.Empty<ValidationIssue>())
            : CompleteGraphFailure(graph.Issues);
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
        EffectAcceptedTurnPlanner.EffectResourceTriggerExecution execution)
    {
        var lineage = new Dictionary<string, int>(parentLineage, StringComparer.Ordinal);
        if (execution.RemainingUseBudget.HasValue)
        {
            var useKey = "uses\0" + execution.EffectId;
            var priorUses = lineage.GetValueOrDefault(useKey);
            if (priorUses >= execution.RemainingUseBudget.Value)
            {
                return new TriggerLineageDecision(
                    Allowed: false,
                    lineage,
                    Array.Empty<ValidationIssue>());
            }
            lineage[useKey] = priorUses + 1;
            return new TriggerLineageDecision(
                Allowed: true,
                lineage,
                Array.Empty<ValidationIssue>());
        }

        var activationKey = TriggerSemanticKey(execution);
        if (lineage.ContainsKey(activationKey))
        {
            return new TriggerLineageDecision(
                Allowed: false,
                lineage,
                Issue(
                    "resource_graph_cycle",
                    "one acyclic effect/resource trigger lineage",
                    activationKey.Replace('\0', '/')));
        }
        lineage.Add(activationKey, 1);
        return new TriggerLineageDecision(
            Allowed: true,
            lineage,
            Array.Empty<ValidationIssue>());
    }

    private static IReadOnlyList<string> PotentialResourceEventKinds(
        ResourceOperation operation) => operation switch
    {
        ResourceOperation.Damage => new[] { "resource_damaged", "resource_depleted" },
        ResourceOperation.Restore => new[] { "resource_restored", "resource_filled" },
        ResourceOperation.Spend => new[] { "resource_spent", "resource_depleted" },
        ResourceOperation.Gain => new[] { "resource_gained", "resource_filled" },
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
    };

    private static string TriggerSemanticKey(
        EffectAcceptedTurnPlanner.EffectResourceTriggerExecution execution) =>
        string.Join(
            "\0",
            execution.EffectId,
            execution.TriggerId,
            execution.EventKind);

    private static CompleteResourceGraphPreparation CompleteGraphFailure(
        IEnumerable<ValidationIssue> issues) =>
        new(
            Array.Empty<PreparedMutation>(),
            null,
            new Dictionary<
                ResourceOperationKey,
                EffectAcceptedTurnPlanner.EffectResourceTriggerExecution>(),
            issues.ToArray());

    private static string TriggerExecutionKey(
        EffectAcceptedTurnPlanner.EffectResourceTriggerExecution execution) =>
        string.Join(
            "\0",
            execution.EffectId,
            execution.TriggerId,
            execution.EventKind,
            execution.EventRef);

    private static bool TryRegisterTriggerUseBudget(
        EffectAcceptedTurnPlanner.EffectResourceTriggerExecution execution,
        Dictionary<string, int> budgets)
    {
        if (!execution.RemainingUseBudget.HasValue)
            return true;
        var budget = execution.RemainingUseBudget.Value;
        if (budget <= 0)
            return false;
        if (budgets.TryGetValue(execution.EffectId, out var existing))
            return existing == budget;
        budgets.Add(execution.EffectId, budget);
        return true;
    }

    private static DerivedMutationAmountResult ResolveMutationAmount(
        ResourceHistoryState baselineHistory,
        ResourceDefinitionCatalog definitions,
        ResourceStateLedger directBaselineState,
        ResourceStateLedger? postDirectState,
        PreparedMutation prepared)
    {
        var mutation = prepared.Intent;
        if (mutation.DerivedAmount == null)
            return new DerivedMutationAmountResult(mutation.Amount, ShouldApply: true, Array.Empty<ValidationIssue>());

        var prior = baselineHistory.Transitions.SingleOrDefault(transition =>
            string.Equals(transition.EventRef, mutation.EventRef, StringComparison.Ordinal) &&
            string.Equals(transition.OriginKind, mutation.Source.SourceKind, StringComparison.Ordinal) &&
            string.Equals(transition.OriginId, mutation.Source.SourceId, StringComparison.Ordinal) &&
            ResourceCoordinateComparer.Instance.Equals(transition.Coordinate, mutation.Coordinate) &&
            transition.Operation == ToTransitionOperation(mutation.Source.Operation));
        if (prior != null)
            return new DerivedMutationAmountResult(prior.RequestedAmount, ShouldApply: true, Array.Empty<ValidationIssue>());

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
        AllocatedIdentityRegistry identityRegistry)
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

            var source = input.Sources.Resolve(
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
            var operationId = identityFactory.CreateOperationId();
            var transitionId = identityFactory.CreateTransitionId();
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
                identityFactory.CreateOperationId(),
                identityFactory.CreateTransitionId()))
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

    private static AcceptedMechanicsPlannerStatistics Statistics(
        ResourceHistoryWorkingSet? history = null) =>
        new(
            history?.BaselineSeedCount ?? 0,
            history?.IncrementalAppendCount ?? 0,
            history?.FreezeCount ?? 0);

    private static string Describe(ResourceOperationKey key) =>
        $"{key.EventRef}/{key.OriginKind}/{key.OriginId}/" +
        $"{key.Coordinate.Realm}/{key.Coordinate.ResourceOwnerId}/" +
        $"{key.Coordinate.ResourceKey}/{key.Operation}";

    private static string Describe(ResourceCapacityOperationKey key) =>
        $"{key.EventRef}/{key.OriginKind}/{key.OriginId}/" +
        $"{key.Coordinate.Realm}/{key.Coordinate.ResourceOwnerId}/" +
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

    private sealed record CompleteResourceGraphPreparation(
        IReadOnlyList<PreparedMutation> Mutations,
        ResourceTriggerGraph? Graph,
        IReadOnlyDictionary<
            ResourceOperationKey,
            EffectAcceptedTurnPlanner.EffectResourceTriggerExecution>
            TriggerExecutionsByMutation,
        IReadOnlyList<ValidationIssue> Issues)
    {
        internal bool IsValid => Graph != null && Issues.Count == 0;
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

            comparison = string.CompareOrdinal(left.OperationId, right.OperationId);
            return comparison != 0
                ? comparison
                : string.CompareOrdinal(left.NodeId, right.NodeId);
        });

    private ResourceTriggerGraph(
        IReadOnlyList<ResourceTriggerGraphNode> orderedNodes,
        int maximumDepth)
    {
        OrderedNodes = orderedNodes;
        MaximumDepth = maximumDepth;
    }

    internal IReadOnlyList<ResourceTriggerGraphNode> OrderedNodes { get; }
    internal int MaximumDepth { get; }

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
                maximumDepth),
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
