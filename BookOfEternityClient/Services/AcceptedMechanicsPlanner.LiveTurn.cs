namespace BookOfEternityClient.Services;

internal static partial class AcceptedMechanicsPlanner
{
    internal sealed record ResourceOrdinalRange(int Start, int End);

    // Detached resource/effect observations. Missing B1 source proof cannot be inferred from these ranges.
    internal sealed class SpiritualExchangeInterval
    {
        private readonly ResourceTransition[] _applied;
        private readonly ResourceTransition[] _replay;
        private readonly ResourceAppliedEvent[] _events;

        internal SpiritualExchangeInterval(
            AfterlifeSpiritualConflictResourceOutcome.ExchangeBatch batch,
            int appliedStart, int replayStart, int eventStart,
            IReadOnlyList<ResourceTransition> applied, IReadOnlyList<ResourceTransition> replay,
            IReadOnlyList<ResourceAppliedEvent> events,
            AcceptedEffectBoundaryTranscript.ClosedPrefix before,
            AcceptedEffectBoundaryTranscript.ClosedPrefix after)
        {
            ConflictId = batch.ConflictId;
            ExchangeId = batch.ExchangeId;
            Ordinal = batch.Ordinal;
            AppliedRange = new(appliedStart, applied.Count);
            ReplayRange = new(replayStart, replay.Count);
            EventRange = new(eventStart, events.Count);
            _applied = applied.Skip(appliedStart).ToArray();
            _replay = replay.Skip(replayStart).ToArray();
            _events = events.Skip(eventStart).ToArray();
            EffectBefore = before;
            EffectAfter = after;
        }

        internal string ConflictId { get; }
        internal string ExchangeId { get; }
        internal int Ordinal { get; }
        internal ResourceOrdinalRange AppliedRange { get; }
        internal ResourceOrdinalRange ReplayRange { get; }
        internal ResourceOrdinalRange EventRange { get; }
        internal IReadOnlyList<ResourceTransition> AppliedTransitions => Array.AsReadOnly(_applied.ToArray());
        internal IReadOnlyList<ResourceTransition> ReplayTransitions => Array.AsReadOnly(_replay.ToArray());
        internal IReadOnlyList<ResourceAppliedEvent> Events => Array.AsReadOnly(_events.ToArray());
        internal AcceptedEffectBoundaryTranscript.ClosedPrefix EffectBefore { get; }
        internal AcceptedEffectBoundaryTranscript.ClosedPrefix EffectAfter { get; }
        internal ResourceOrdinalRange Boundaries => new(EffectBefore.Boundaries.Count, EffectAfter.Boundaries.Count);
        internal ResourceOrdinalRange BoundaryCloses => new(EffectBefore.BoundaryCloses.Count, EffectAfter.BoundaryCloses.Count);
        internal ResourceOrdinalRange CausalClosures => new(EffectBefore.CausalClosures.Count, EffectAfter.CausalClosures.Count);
        internal ResourceOrdinalRange Activations => new(EffectBefore.AcceptedActivations.Count, EffectAfter.AcceptedActivations.Count);
        internal ResourceOrdinalRange Rejections => new(EffectBefore.RejectedActivations.Count, EffectAfter.RejectedActivations.Count);
        internal ResourceOrdinalRange Components => new(EffectBefore.AppliedComponentEvidence.Count, EffectAfter.AppliedComponentEvidence.Count);
        internal ResourceOrdinalRange ResourceMutations => new(EffectBefore.ResourceMutations.Count, EffectAfter.ResourceMutations.Count);
        internal ResourceOrdinalRange Releases => new(EffectBefore.ReleasedReactions.Count, EffectAfter.ReleasedReactions.Count);
        internal ResourceOrdinalRange TerminalReservations => new(
            EffectBefore.TerminalAvailabilityReservations.Count, EffectAfter.TerminalAvailabilityReservations.Count);
    }

    internal sealed record PendingSpiritualExchange(
        string ConflictId, string ExchangeId, int Ordinal, IReadOnlyList<string> MissingAuditSides);

    internal sealed class PendingSpiritualResourceExchange
    {
        private readonly EffectAcceptedTurnPlanner.EffectBoundedResourceResolution[] _outputs;

        internal PendingSpiritualResourceExchange(
            string? exchangeId, long boundaryOrdinal,
            IEnumerable<EffectAcceptedTurnPlanner.EffectBoundedResourceResolution> outputs,
            AcceptedMechanicsPlannerStatistics statistics)
        {
            ExchangeId = exchangeId;
            BoundaryOrdinal = boundaryOrdinal;
            _outputs = outputs.Select(Clone).ToArray();
            Statistics = statistics;
        }

        internal string? ExchangeId { get; }
        internal long BoundaryOrdinal { get; }
        internal AcceptedMechanicsPlannerStatistics Statistics { get; }
        internal IReadOnlyList<EffectAcceptedTurnPlanner.EffectBoundedResourceResolution> RequiredOutputs =>
            Array.AsReadOnly(_outputs.Select(Clone).ToArray());

        private static EffectAcceptedTurnPlanner.EffectBoundedResourceResolution Clone(
            EffectAcceptedTurnPlanner.EffectBoundedResourceResolution value) =>
            value with
            {
                Source = value.Source.DeepClone().AsObject(),
                Target = value.Target.DeepClone().AsObject(),
                Dependencies = value.Dependencies.ToArray(),
                EventRequirements = value.EventRequirements.ToArray()
            };
    }

    internal static ResourceExecutionSession BeginLiveResourceExecution(
        AcceptedMechanicsResourceInput baseline,
        AcceptedMechanicsIdentityFactory identityFactory,
        EffectAcceptedTurnPlanner.BaseResourceRouting? routing = null)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(identityFactory);
        return new ResourceExecutionSession(baseline, identityFactory, live: true, routing);
    }

    private sealed partial class ResourceExecutionState
    {
        private PlannerWorkMetrics metrics = null!;
        private AllocatedIdentityRegistry identityRegistry = null!;
        private IReadOnlyList<PreparedMutation> preparedMutations = null!;
        private CompleteResourceGraphPreparation graphPreparation = null!;
        private PendingCandidateAuthorityValidationResult candidateAuthorities = null!;
        private ResourceWorkingLedger workingLedger = null!;
        private ResourceHistoryWorkingSet workingHistory = null!;
        private List<ResourceAppliedEvent> events = null!;
        private List<ResourceTransition> appliedTransitions = null!;
        private List<ResourceTransition> replayTransitions = null!;
        private int executionSequence = 0;
        private ResourceStateLedger directBaselineState = null!;
        private ResourceStateLedger? postDirectState = null!;
        private Dictionary<string, PreparedMutation> byOperationId = null!;
        private Dictionary<ResourceOperationKey, string> operationIdByKey = null!;
        private AcceptedEffectBoundaryTranscript.Builder effectTranscriptBuilder = null!;
        private Dictionary<string, HashSet<string>> producedEvents = null!;
        private Dictionary<EffectActivationCandidateIdentity, HashSet<ResourceOperationKey>> appliedTriggerMutationKeys = null!;
        private List<(EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate Candidate, AcceptedEffectActivation Activation)> acceptedCandidates = null!;
        private HashSet<EffectActivationCandidateIdentity> acceptedCandidateIdentities = null!;
        private HashSet<EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate> observedCandidates = null!;
        private Dictionary<EffectActivationCandidateIdentity, EffectEventBoundaryStamp> acceptedBoundaryByIdentity = null!;
        private Dictionary<EffectActivationCandidateIdentity, AcceptedEffectActivation> acceptedActivationByIdentity = null!;
        private Dictionary<long, List<(EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate Candidate, AcceptedEffectActivation Activation, EffectEventBoundaryStamp Boundary)>> acceptedByBoundary = null!;
        private Dictionary<long, HashSet<string>> remainingOperationsByBoundary = null!;
        private HashSet<string> completedOperationIds = null!;
        private Dictionary<long, long?> parentBoundaryByOrdinal = null!;
        private HashSet<long> closedBoundaries = null!;
        private HashSet<long> unresolvedPendingBoundaries = null!;
        private Dictionary<string, CanonicalEffectUseSeed> useSeeds = null!;
        private AcceptedEffectUseArbiter arbiter = null!;
        private Dictionary<(ResourceOperationKey Producer, string EventKind), IReadOnlyList<EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>> candidatesByProducerEvent = null!;
        private ResourceTriggerGraphExecutionScheduler scheduler = null!;
        private readonly List<SpiritualExchangeInterval> intervals = new();
        private AfterlifeSpiritualConflictResourceOutcome.ExchangeBatch? activeBatch;
        private int intervalAppliedStart;
        private int intervalReplayStart;
        private int intervalEventStart;
        private AcceptedEffectBoundaryTranscript.ClosedPrefix? intervalEffectStart;

        internal bool Owns(SpiritualExchangeInterval interval) =>
            intervals.Any(value => ReferenceEquals(value, interval));

        internal IEnumerable<ResourceExecutionStep> Run(
            AcceptedMechanicsResourceInput input,
            AcceptedMechanicsIdentityFactory identityFactory,
            ResourceExecutionSession session)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(identityFactory);
            metrics = new PlannerWorkMetrics
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
            var terminal = session.TerminalPreparation;
            if (terminal != null && !terminal.Owns(session))
            {
                yield return ResourceExecutionStep.Finished(Failure(
                    Issue("spiritual_terminal_owner_mismatch", "exact original capture and executor", "foreign owner"),
                    Statistics(metrics: metrics)));
                yield break;
            }
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

            identityRegistry = new AllocatedIdentityRegistry();
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
            preparedMutations = preparation.Mutations;
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

            graphPreparation = PrepareCompleteResourceGraph(
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
            candidateAuthorities = ValidatePendingCandidateAuthorities(
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
            workingLedger = new ResourceWorkingLedger(input.State.Entries);
            workingHistory = new ResourceHistoryWorkingSet(input.History);
            events = new List<ResourceAppliedEvent>();
            appliedTransitions = new List<ResourceTransition>();
            replayTransitions = new List<ResourceTransition>();
            executionSequence = input.ExecutionSequenceOffset;

            var deferred = capacityPreparation.Transitions.Where(capacity =>
                terminal != null && capacity.Intent.Operation == ResourceCapacityOperation.Retire &&
                capacity.Intent.Coordinate.Realm == terminal.Owner.Realm &&
                capacity.Intent.Coordinate.OwnerKind == terminal.Owner.OwnerKind &&
                capacity.Intent.Coordinate.ResourceOwnerId == terminal.Owner.ResourceOwnerId).ToArray();
            if (terminal != null && deferred.Length != 1)
            {
                yield return ResourceExecutionStep.Finished(Failure(
                    Issue("spiritual_terminal_retirement_missing", "one exact original retirement", "missing or ambiguous"), Statistics(workingHistory, metrics)));
                yield break;
            }
            foreach (var capacity in capacityPreparation.Transitions.Except(deferred))
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

            directBaselineState = workingLedger.Freeze();
            postDirectState = null;

            byOperationId = preparedMutations.ToDictionary(
                static value => value.OperationId,
                StringComparer.Ordinal);
            operationIdByKey = preparedMutations.ToDictionary(
                static value => value.Intent.Key,
                static value => value.OperationId);
            effectTranscriptBuilder =
                new AcceptedEffectBoundaryTranscript.Builder(
                    input.EffectPlanAuthority);
            producedEvents = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            appliedTriggerMutationKeys = new Dictionary<
                EffectActivationCandidateIdentity,
                HashSet<ResourceOperationKey>>();
            acceptedCandidates = new List<(
                EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate Candidate,
                AcceptedEffectActivation Activation)>();
            acceptedCandidateIdentities = new HashSet<
                EffectActivationCandidateIdentity>();
            observedCandidates = new HashSet<
                EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>(ReferenceEqualityComparer.Instance);
            acceptedBoundaryByIdentity = new Dictionary<
                EffectActivationCandidateIdentity,
                EffectEventBoundaryStamp>();
            acceptedActivationByIdentity = new Dictionary<
                EffectActivationCandidateIdentity,
                AcceptedEffectActivation>();
            acceptedByBoundary = new Dictionary<
                long,
                List<(
                    EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate Candidate,
                    AcceptedEffectActivation Activation,
                    EffectEventBoundaryStamp Boundary)>>();
            remainingOperationsByBoundary = new Dictionary<
                long,
                HashSet<string>>();
            completedOperationIds = new HashSet<string>(StringComparer.Ordinal);
            parentBoundaryByOrdinal = new Dictionary<long, long?>();
            closedBoundaries = new HashSet<long>();
            unresolvedPendingBoundaries = new HashSet<long>();
            useSeeds = new Dictionary<string, CanonicalEffectUseSeed>(StringComparer.Ordinal);
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
            var baselineSeedIssues = session.Routing?.ValidateCandidateSeeds(graphPreparation.TriggerCandidates)
                ?? Array.Empty<ValidationIssue>();
            if (baselineSeedIssues.Count != 0)
            {
                yield return ResourceExecutionStep.Finished(Failure(baselineSeedIssues, Statistics(workingHistory, metrics)));
                yield break;
            }
            var initializedArbiter = AcceptedEffectUseArbiter.Initialize(
                (session.Routing?.CanonicalUseSeeds ?? useSeeds.Values.ToArray())
                    .OrderBy(static seed => seed.EffectId, StringComparer.Ordinal)
                    .ToArray());
            if (!initializedArbiter.IsValid || initializedArbiter.Arbiter == null)
            {
                yield return ResourceExecutionStep.Finished(Failure(
                    ArbiterIssues(initializedArbiter.Issues),
                    Statistics(workingHistory, metrics)));
                yield break;
            }
            arbiter = initializedArbiter.Arbiter;
            candidatesByProducerEvent = graphPreparation.TriggerCandidates
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
                    observedCandidates.Add(candidate);
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
                    var candidate = byIdentity[rejected.Identity];
                    var rejectedIssue = effectTranscriptBuilder.RecordRejected(
                        boundary,
                        candidate,
                        rejected.Reason,
                        rejected.Reason ==
                            EffectActivationRejectionReason.EffectTerminal
                            ? rejected.Identity.EffectId
                            : null);
                    observedCandidates.Add(candidate);
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
                    observedCandidates.Add(candidate);
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

            scheduler = graphPreparation.Graph!.CreateExecutionScheduler(node =>
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
            if (session.IsLive && pendingFrontier is { } livePendingBoundary)
            {
                var captureIssues = CapturePendingOutputs(livePendingBoundary);
                if (captureIssues.Count != 0)
                {
                    yield return ResourceExecutionStep.Finished(Failure(
                        captureIssues, Statistics(workingHistory, metrics)));
                    yield break;
                }
                var pendingOutputs = acceptedByBoundary[livePendingBoundary]
                    .SelectMany(value => value.Candidate.PendingOutputs.Where(output =>
                        !value.Candidate.TryResolvePendingBinding(output.ComponentId, out _) &&
                        (output.AfterComponentId == null ||
                         appliedTriggerMutationKeys.TryGetValue(value.Activation.Stamp.Identity, out var appliedKeys) &&
                         value.Candidate.PlannedComponentIdsByMutation.Any(component =>
                             appliedKeys.Contains(component.Key) &&
                             string.Equals(component.Value, output.AfterComponentId, StringComparison.Ordinal)))))
                    .ToArray();
                yield return ResourceExecutionStep.WaitingForResource(new PendingSpiritualResourceExchange(
                    activeBatch?.ExchangeId, livePendingBoundary, pendingOutputs, Statistics(workingHistory, metrics)));
                var continuationIssues = ApplyStagedReceiptContinuation(input, identityFactory, session);
                if (continuationIssues.Count == 0)
                    continuationIssues = CloseReadyBoundaries();
                if (continuationIssues.Count != 0)
                {
                    yield return ResourceExecutionStep.Finished(Failure(
                        continuationIssues, Statistics(workingHistory, metrics)));
                    yield break;
                }
                completedAtBoundary = null;
                continue;
            }
            if (!session.IsLive)
                break;

            if (activeBatch != null)
            {
                var witnessIssues = activeBatch.ValidateTransitions(
                    appliedTransitions.Skip(intervalAppliedStart).ToArray(),
                    replayTransitions.Skip(intervalReplayStart).ToArray());
                if (witnessIssues.Count != 0)
                {
                    yield return ResourceExecutionStep.Finished(Failure(witnessIssues, Statistics(workingHistory, metrics)));
                    yield break;
                }
                if (activeBatch.PendingSides.Count != 0)
                {
                    var wait = new PendingSpiritualExchange(
                        activeBatch.ConflictId, activeBatch.ExchangeId, activeBatch.Ordinal,
                        Array.AsReadOnly(activeBatch.PendingSides.ToArray()));
                    session.OwnMissingAuditWait(wait);
                    yield return ResourceExecutionStep.WaitingForExchange(wait);
                    var extension = session.TakeMissingAuditExtension();
                    var appendMissingIssues = AppendBatch(input, identityFactory, session, extension.Appended);
                    if (appendMissingIssues.Count != 0)
                    {
                        yield return ResourceExecutionStep.Finished(Failure(
                            appendMissingIssues, Statistics(workingHistory, metrics)));
                        yield break;
                    }
                    activeBatch = extension.Merged;
                    completedAtBoundary = null;
                    continue;
                }
                var closed = effectTranscriptBuilder.CaptureClosedPrefix();
                if (!closed.IsValid || closed.Prefix == null)
                {
                    yield return ResourceExecutionStep.Finished(Failure(
                        closed.Issues.SelectMany(issue => Issue(issue.Code, issue.Expected, issue.Actual)).ToArray(),
                        Statistics(workingHistory, metrics)));
                    yield break;
                }
                var interval = new SpiritualExchangeInterval(activeBatch,
                    intervalAppliedStart, intervalReplayStart, intervalEventStart,
                    appliedTransitions, replayTransitions, events, intervalEffectStart!, closed.Prefix);
                intervals.Add(interval);
                activeBatch = null;
                session.CloseExchange();
                if (session.StopAtExchange)
                    yield return ResourceExecutionStep.ExchangeClosed(interval);
            }
            if (session.OriginalPrefixRequested && _originalPrefix == null)
            {
                var prefix = effectTranscriptBuilder.CaptureClosedPrefix();
                if (!prefix.IsValid || prefix.Prefix == null)
                {
                    yield return ResourceExecutionStep.Finished(Failure(
                        prefix.Issues.SelectMany(issue => Issue(issue.Code, issue.Expected, issue.Actual)).ToArray(),
                        Statistics(workingHistory, metrics)));
                    yield break;
                }
                _originalPrefix = new OriginalResourcePrefix(workingLedger.Freeze(),
                    appliedTransitions, replayTransitions, events, prefix.Prefix);
                yield return ResourceExecutionStep.OriginalPrefixClosed(_originalPrefix);
            }
            var batch = session.TakeStaged();
            if (batch == null)
                break;
            var beforeBatch = effectTranscriptBuilder.CaptureClosedPrefix();
            if (!beforeBatch.IsValid || beforeBatch.Prefix == null)
            {
                yield return ResourceExecutionStep.Finished(Failure(
                    beforeBatch.Issues.SelectMany(issue => Issue(issue.Code, issue.Expected, issue.Actual)).ToArray(),
                    Statistics(workingHistory, metrics)));
                yield break;
            }
            activeBatch = batch;
            intervalAppliedStart = appliedTransitions.Count;
            intervalReplayStart = replayTransitions.Count;
            intervalEventStart = events.Count;
            intervalEffectStart = beforeBatch.Prefix;
            var appendIssues = AppendBatch(input, identityFactory, session, batch);
            if (appendIssues.Count != 0)
            {
                yield return ResourceExecutionStep.Finished(Failure(appendIssues, Statistics(workingHistory, metrics)));
                yield break;
            }
            completedAtBoundary = null;
            }
            if (terminal != null && !terminal.CanRetire(session))
            {
                yield return ResourceExecutionStep.Finished(Failure(
                    Issue("spiritual_terminal_exchange_incomplete", "closed terminal exchange and wound routing", "incomplete"), Statistics(workingHistory, metrics)));
                yield break;
            }
            foreach (var capacity in deferred)
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


        private IReadOnlyList<ValidationIssue> AppendBatch(
            AcceptedMechanicsResourceInput input,
            AcceptedMechanicsIdentityFactory identityFactory,
            ResourceExecutionSession session,
            AfterlifeSpiritualConflictResourceOutcome.ExchangeBatch batch)
        {
            var exports = graphPreparation.SourceExports.ToDictionary(
                source => (source.SourceKind, source.SourceId));
            foreach (var source in batch.Sources)
            {
                var key = (source.SourceKind, source.SourceId);
                if (exports.TryGetValue(key, out var prior) && prior != source)
                    return Issue("effect_resource_source_conflict",
                        "one exact source policy per effect/trigger/component",
                        source.SourceKind + "/" + source.SourceId);
                exports[key] = source;
            }
            var catalog = ResourceMutationSourceCatalog.Create(exports.Values);
            if (!catalog.IsValid || catalog.Catalog == null)
                return catalog.Issues;
            var existingKeys = operationIdByKey.Keys.ToHashSet();
            if (batch.Mutations.Any(mutation => existingKeys.Contains(mutation.Key)))
                return Issue("resource_planner_duplicate_operation",
                    "one mutation per exact replay key", batch.ExchangeId);

            var batchInput = new AcceptedMechanicsResourceInput(
                input.Turn, input.Definitions, input.State, input.History,
                catalog.Catalog, batch.Mutations, EventMutationResolver: input.EventMutationResolver,
                EffectPlanAuthority: input.EffectPlanAuthority);
            var prepared = PrepareMutations(batchInput, identityFactory, identityRegistry);
            if (prepared.Issues.Count != 0)
                return prepared.Issues;
            var all = preparedMutations.Concat(prepared.Mutations).ToArray();
            if (all.Count(value => value.Route.Phase != ResourceMutationPhase.EffectTrigger) >
                ResourceMaterializationContract.MaxMutationsBeforeTriggers)
                return Issue("resource_planner_mutation_limit_exceeded",
                    $"at most {ResourceMaterializationContract.MaxMutationsBeforeTriggers} pre-trigger mutations",
                    all.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var appended = PrepareCompleteResourceGraph(batchInput, all,
                identityFactory, identityRegistry, graphPreparation, completedOperationIds);
            AddGraphWork(appended.Work);
            if (!appended.IsValid)
                return appended.Issues;
            var seedIssues = session.Routing?.ValidateCandidateSeeds(appended.TriggerCandidates)
                ?? Array.Empty<ValidationIssue>();
            if (seedIssues.Count != 0)
                return seedIssues;
            var authorities = ValidatePendingCandidateAuthorities(
                appended.TriggerCandidates, appended.Mutations, appended.SourceExports, metrics);
            if (!authorities.IsValid)
                return authorities.Issues;

            graphPreparation = appended;
            preparedMutations = appended.Mutations;
            candidateAuthorities = authorities;
            foreach (var mutation in preparedMutations)
            {
                if (byOperationId.TryAdd(mutation.OperationId, mutation))
                    operationIdByKey.Add(mutation.Intent.Key, mutation.OperationId);
            }
            candidatesByProducerEvent = graphPreparation.TriggerCandidates
                .Where(candidate => candidate.Producer != null)
                .GroupBy(candidate => (Producer: candidate.Producer!, candidate.Activation.Identity.EventKind))
                .ToDictionary(group => group.Key,
                    group => (IReadOnlyList<EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>)group.ToArray());
            metrics.MutationDescriptorCount = preparedMutations.Count;
            metrics.GraphNodeDescriptorCount = appended.Graph!.OrderedNodes.Count;
            metrics.MaximumTriggerDepth = appended.Graph.MaximumDepth;
            scheduler = appended.Graph.CreateExecutionScheduler(
                node => RequirementsSatisfied(byOperationId[node.OperationId], producedEvents),
                completedOperationIds);
            return Array.Empty<ValidationIssue>();
        }

        private void AddGraphWork(ResourceGraphExpansionWork work)
        {
            metrics.EffectTriggerIndexLookupCount += work.EffectTriggerIndexLookupCount;
            metrics.EffectTriggerCandidateVisitCount += work.EffectTriggerCandidateVisitCount;
            metrics.EffectSourceBindingIndexLookupCount += work.EffectSourceBindingIndexLookupCount;
            metrics.EffectSourceBindingCandidateVisitCount += work.EffectSourceBindingCandidateVisitCount;
            metrics.EffectRoutingDescriptorAccessCount += work.EffectRoutingDescriptorAccessCount;
            metrics.EffectOccurrenceCloneCount += work.EffectOccurrenceCloneCount;
            metrics.EffectFullValidationPassCount += work.EffectFullValidationPassCount;
            metrics.EffectTriggerArrayVisitCount += work.EffectTriggerArrayVisitCount;
            metrics.EffectComponentIndexLookupCount += work.EffectComponentIndexLookupCount;
            metrics.EffectSelectedComponentVisitCount += work.EffectSelectedComponentVisitCount;
            metrics.PendingCandidateFingerprintOutputVisitCount += work.PendingCandidateFingerprintOutputVisitCount;
            metrics.PendingProjectionDependencyVisitCount += work.PendingProjectionDependencyVisitCount;
            metrics.SourceAuthoritySeedCount += work.SourceAuthoritySeedCount;
            metrics.SourceAuthorityAddVisitCount += work.SourceAuthorityAddVisitCount;
            metrics.SourceAuthorityResolveLookupCount += work.SourceAuthorityResolveLookupCount;
            metrics.SourceAuthorityFreezeCount += work.SourceAuthorityFreezeCount;
        }

    }
}
