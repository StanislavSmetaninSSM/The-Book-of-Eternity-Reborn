using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class AcceptedMechanicsPlanner
{
    // Resource-layer continuation only. The common spiritual source owner must
    // still authorize its exchange, input provenance and generation before use.
    internal sealed record ResourceContinuationResult(
        ResourceExecutionStep? Step, IReadOnlyList<ValidationIssue> Issues);

    internal sealed partial class ResourceExecutionSession
    {
        private AcceptedMechanicsResourceInput _originalResourceInput = null!;
        private AcceptedMechanicsInput? _pendingInput;
        private ResourcePendingResolutionState? _livePendingState;
        private PendingSpiritualResourceExchange? _pendingResourceObservation;
        private JsonObject? _pendingPacket;
        private bool _started;

        internal void BindOriginalPendingContext(AcceptedMechanicsInput input)
        {
            ArgumentNullException.ThrowIfNull(input);
            Enter();
            try
            {
                EnsureUsable();
                var context = input.PlanningContext;
                if (!_live || _started || _pendingInput != null || context == null ||
                    input.Turn != _originalResourceInput.Turn ||
                    !ReferenceEquals(context.Definitions, _originalResourceInput.Definitions) ||
                    !ReferenceEquals(context.State, _originalResourceInput.State) ||
                    !ReferenceEquals(context.History, _originalResourceInput.History) ||
                    (_routing == null ? context.EffectPlan != null :
                        !_routing.OwnsContext(context.EffectPlan, context.Owners)) ||
                    context.PendingResolutionState is { Requests.Count: > 0 })
                    throw new InvalidOperationException(
                        "Bind the retained original resource/effect context once before execution.");
                _pendingInput = input;
                _livePendingState = context.PendingResolutionState;
            }
            finally { Exit(); }
        }

        internal JsonObject ReadPendingResourceRequest(PendingSpiritualResourceExchange expected)
        {
            Enter();
            try
            {
                EnsureUsable();
                if (!ReferenceEquals(expected, _pendingResourceObservation) || _pendingPacket == null)
                    throw new InvalidOperationException("Only the current owned resource wait has a request.");
                return _pendingPacket.DeepClone().AsObject();
            }
            finally { Exit(); }
        }

        internal ResourceContinuationResult ResumePendingResource(
            PendingSpiritualResourceExchange expected, JsonArray receipts)
        {
            ArgumentNullException.ThrowIfNull(expected);
            ArgumentNullException.ThrowIfNull(receipts);
            Enter();
            try
            {
                EnsureUsable();
                if (!_live || !_pendingExchange || _pendingInput == null ||
                    _livePendingState == null || !ReferenceEquals(expected, _pendingResourceObservation))
                    return new(null, Issue("resource_live_pending_owner_mismatch",
                        "the exact current wait of this configured retained session", "foreign or stale"));
                var issues = new List<ValidationIssue>();
                var decision = ResolvePendingBoundary(_pendingInput,
                    _pendingInput.PlanningContext!, _originalResourceInput.Definitions,
                    _state.PendingAcceptedOutputs, issues, _livePendingState, receipts);
                if (!decision.IsValid || decision.AwaitingReceipt || decision.StateAfterImage == null ||
                    decision.ResolvedBindings == null)
                    return new(null, decision.Issues.Count != 0 ? decision.Issues :
                        Issue("resource_live_pending_resolution_missing",
                            "all current requests resolved by exact validated receipts", "unresolved"));
                var prepared = _state.PrepareReceiptContinuation(decision.ResolvedBindings);
                if (prepared.Continuation == null)
                    return new(null, prepared.Issues);
                // Receipt shape, companions, old bindings and source projections have
                // all passed without advancing or allocating in this session.
                _state.StageReceiptContinuation(prepared.Continuation);
                _livePendingState = decision.StateAfterImage;
                _pendingResourceObservation = null;
                _pendingPacket = null;
                _pendingExchange = false;
                var step = MoveNextOwned();
                return new(step, step.Result?.Issues ?? Array.Empty<ValidationIssue>());
            }
            finally { Exit(); }
        }

        /// <summary>
        /// Revokes the previous checkpoint, advances the retained iterator and records only its actual yielded boundary.
        /// Iterator or pending-projection failures fault this owner and revoke checkpoint authority.
        /// </summary>
        /// <returns>
        /// The actual checkpoint, wait or final result produced by the retained execution.
        /// </returns>
        private ResourceExecutionStep MoveNextOwned()
        {
            _currentCheckpoint = null;
            try
            {
                _started = true;
                if (_execution == null || !_execution.MoveNext())
                    throw new InvalidOperationException("Resource execution ended without a result.");
                var step = _execution.Current;
                if (step.Checkpoint != null)
                {
                    CheckpointCount++;
                    _currentCheckpoint = step.Checkpoint;
                }
                if (step.PendingExchange != null || step.PendingResource != null)
                    _pendingExchange = true;
                if (step.PendingResource is { } pending)
                {
                    _pendingResourceObservation = pending;
                    if (_pendingInput != null)
                    {
                        var issues = new List<ValidationIssue>();
                        var decision = ResolvePendingBoundary(_pendingInput,
                            _pendingInput.PlanningContext!, _originalResourceInput.Definitions,
                            _state.PendingAcceptedOutputs, issues,
                            _livePendingState, new JsonArray());
                        if (!decision.IsValid || !decision.AwaitingReceipt ||
                            decision.StateAfterImage == null || decision.SafeGmPacket == null)
                            throw new InvalidOperationException(string.Join("; ",
                                decision.Issues.Select(issue => issue.Code + ": " + issue.Actual)));
                        _livePendingState = decision.StateAfterImage;
                        _pendingPacket = decision.SafeGmPacket.DeepClone().AsObject();
                    }
                }
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
                _currentCheckpoint = null;
                _execution?.Dispose();
                _execution = null;
                throw;
            }
        }
    }

    private sealed record LiveCandidateExtension(
        EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate Original,
        EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate Bound);

    private sealed record LiveReceiptContinuation(
        CompleteResourceGraphPreparation OriginalGraph,
        IReadOnlyList<LiveCandidateExtension> Extensions,
        ResourceMutationSourceCatalog Sources,
        IReadOnlyList<ResourceMutationIntent> NewMutations,
        int NextWaveOrdinal);

    private sealed partial class ResourceExecutionState
    {
        private AcceptedEffectBoundedResourceResolution[] _pendingAcceptedOutputs =
            Array.Empty<AcceptedEffectBoundedResourceResolution>();
        private LiveReceiptContinuation? _receiptContinuation;
        private int _livePendingWave;

        internal IReadOnlyList<AcceptedEffectBoundedResourceResolution> PendingAcceptedOutputs =>
            Array.AsReadOnly(_pendingAcceptedOutputs);

        private IReadOnlyList<ValidationIssue> CapturePendingOutputs(long boundaryOrdinal)
        {
            var boundary = effectTranscriptBuilder.FindBoundary(boundaryOrdinal);
            if (boundary == null)
                return Issue("resource_live_pending_frontier_missing", "the actual waiting boundary", "missing");
            var capture = effectTranscriptBuilder.CapturePendingFrontier(boundary);
            if (!capture.IsValid || capture.Transcript == null)
                return capture.Issues.SelectMany(issue => Issue(issue.Code, issue.Expected, issue.Actual)).ToArray();
            var prefixes = capture.Transcript.CreateActivationPrefixFingerprints();
            var pending = new List<AcceptedEffectBoundedResourceResolution>();
            foreach (var accepted in acceptedByBoundary[boundaryOrdinal]
                         .OrderBy(value => value.Activation.Stamp.ActivationOrdinal))
            {
                var candidate = accepted.Candidate;
                if (candidate.PendingOutputs.Count == 0)
                    continue;
                if (!candidateAuthorities.Authorities.TryGetValue(candidate.Activation.Identity, out var authority))
                    return Issue("resource_pending_candidate_authority_missing",
                        "the prevalidated original candidate", candidate.Activation.Identity.EffectId);
                foreach (var output in candidate.PendingOutputs)
                {
                    if (candidate.TryResolvePendingBinding(output.ComponentId, out _) ||
                        output.AfterComponentId is { } predecessor &&
                        !HasAppliedComponent(candidate, predecessor))
                        continue;
                    pending.Add(new AcceptedEffectBoundedResourceResolution(output,
                        CreatePendingCausalAuthority(candidate, accepted.Activation.Stamp, output,
                            authority.CandidateFingerprint,
                            prefixes[accepted.Activation.Stamp.ActivationOrdinal], _livePendingWave)));
                }
            }
            if (pending.Count == 0)
                return Issue("resource_live_pending_frontier_empty",
                    "at least one actual unresolved eligible output", boundaryOrdinal.ToString());
            _pendingAcceptedOutputs = pending.ToArray();
            return Array.Empty<ValidationIssue>();
        }

        private bool HasAppliedComponent(
            EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate candidate, string component) =>
            appliedTriggerMutationKeys.TryGetValue(candidate.Activation.Identity, out var keys) &&
            candidate.PlannedComponentIdsByMutation.Any(pair => keys.Contains(pair.Key) &&
                string.Equals(pair.Value, component, StringComparison.Ordinal));

        internal (LiveReceiptContinuation? Continuation, IReadOnlyList<ValidationIssue> Issues)
            PrepareReceiptContinuation(IReadOnlyList<ResourcePendingResolvedBinding> resolved)
        {
            var issues = new List<ValidationIssue>();
            var sources = graphPreparation.SourceExports.ToDictionary(value => (value.SourceKind, value.SourceId));
            var newMutations = new Dictionary<ResourceOperationKey, ResourceMutationIntent>();
            var extensions = new List<LiveCandidateExtension>();
            var wave = resolved.Count == 0 ? 0 : checked(resolved.Max(value =>
                value.RequestAuthority.CausalAuthority.WaveOrdinal) + 1);
            foreach (var accepted in acceptedCandidates)
            {
                var candidate = accepted.Candidate;
                if (candidate.PendingOutputs.Count == 0 || closedBoundaries.Contains(
                        acceptedBoundaryByIdentity[candidate.Activation.Identity].BoundaryOrdinal))
                    continue;
                if (!candidateAuthorities.Authorities.TryGetValue(
                        candidate.Activation.Identity,
                        out var authority))
                    return (null, Issue("resource_pending_candidate_authority_missing",
                        "the prevalidated original candidate", candidate.Activation.Identity.EffectId));
                var fingerprint = authority.CandidateFingerprint;
                var bindings = new Dictionary<string, ResourcePendingResolvedBinding>(StringComparer.Ordinal);
                foreach (var output in candidate.PendingOutputs)
                {
                    if (candidate.TryResolvePendingBinding(output.ComponentId, out var prior))
                        bindings.Add(output.ComponentId, prior);
                    var matches = resolved.Where(binding =>
                        PendingReplayStaticKey(candidate, output, fingerprint) ==
                        PendingReplayStaticKey(binding.RequestAuthority.CausalAuthority,
                            binding.RequestAuthority.EffectAuthority)).ToArray();
                    if (matches.Length > 1)
                        return (null, Issue("resource_pending_receipt_binding_duplicate",
                            "one terminal per accepted component", output.ComponentId));
                    if (matches.Length == 0)
                        continue;
                    var match = matches[0];
                    if (!PendingCandidateMatches(match.RequestAuthority, output) ||
                        !PendingEffectIdentityMatches(match.RequestAuthority.EffectId, output.EffectId,
                            output.EffectAuthority))
                        return (null, Issue("resource_pending_causal_replay_mismatch",
                            "the exact accepted candidate output", output.ComponentId));
                    if (bindings.TryGetValue(output.ComponentId, out var old) &&
                        (old.ReceiptFingerprint != match.ReceiptFingerprint ||
                         old.RequestAuthority.ReplayFingerprint != match.RequestAuthority.ReplayFingerprint))
                        return (null, Issue("resource_pending_causal_replay_mismatch",
                            "the previously resolved component unchanged", output.ComponentId));
                    bindings[output.ComponentId] = match.DeepClone();
                }
                var projections = ProjectResolvedPendingMutations(candidate, candidate.PendingOutputs, bindings, issues);
                if (issues.Count != 0)
                    return (null, issues);
                var components = candidate.PlannedComponentIdsByMutation.ToDictionary(pair => pair.Key, pair => pair.Value);
                foreach (var projection in projections)
                {
                    if (operationIdByKey.TryGetValue(projection.Mutation.Key, out var oldId))
                    {
                        var old = byOperationId[oldId].Intent;
                        if (!sources.TryGetValue((old.Source.SourceKind, old.Source.SourceId), out var oldSource) ||
                            CreateMutationIntentFingerprint(old, oldSource) !=
                            CreateMutationIntentFingerprint(projection.Mutation, projection.Source))
                            return (null, Issue("resource_pending_causal_replay_mismatch",
                                "previous projection semantics unchanged", projection.ComponentId));
                    }
                    else if (!newMutations.TryAdd(projection.Mutation.Key, projection.Mutation))
                        return (null, Issue("resource_planner_duplicate_operation",
                            "one new operation per receipt projection", projection.ComponentId));
                    components[projection.Mutation.Key] = projection.ComponentId;
                    var key = (projection.Source.SourceKind, projection.Source.SourceId);
                    if (sources.TryGetValue(key, out var priorSource) && priorSource != projection.Source)
                        return (null, Issue("effect_resource_source_conflict",
                            "one exact source per terminal receipt", projection.Source.SourceId));
                    sources[key] = projection.Source;
                }
                var bound = new EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate(
                    candidate.Activation, candidate.UseSeed, candidate.Producer,
                    components.Keys.ToArray(), components.Values.Distinct(StringComparer.Ordinal).ToArray(),
                    components, candidate.PendingOutputs, candidate.ReactionOutputs, candidate.Origin,
                    candidateFingerprint: fingerprint, resolvedPendingBindings: bindings,
                    pendingWaveOrdinal: wave, causalMaterialFingerprint: authority.CausalMaterialFingerprint,
                    effectAuthority: candidate.EffectAuthority);
                if (CreateCandidateFingerprint(bound) != fingerprint)
                    return (null, Issue("resource_pending_candidate_origin_mismatch",
                        "unchanged original candidate authority", candidate.Activation.Identity.EffectId));
                extensions.Add(new(candidate, bound));
            }
            var catalog = ResourceMutationSourceCatalog.Create(sources.Values);
            return !catalog.IsValid || catalog.Catalog == null
                ? (null, catalog.Issues)
                : (new LiveReceiptContinuation(graphPreparation, extensions, catalog.Catalog,
                    newMutations.Values.ToArray(), wave), Array.Empty<ValidationIssue>());
        }

        internal void StageReceiptContinuation(LiveReceiptContinuation continuation)
        {
            if (_receiptContinuation != null || !ReferenceEquals(continuation.OriginalGraph, graphPreparation))
                throw new InvalidOperationException("Receipt continuation no longer belongs to this frontier.");
            _receiptContinuation = continuation;
        }

        private IReadOnlyList<ValidationIssue> ApplyStagedReceiptContinuation(
            AcceptedMechanicsResourceInput input, AcceptedMechanicsIdentityFactory factory,
            ResourceExecutionSession session)
        {
            var continuation = _receiptContinuation;
            if (continuation == null || !ReferenceEquals(continuation.OriginalGraph, graphPreparation))
                return Issue("resource_live_pending_owner_mismatch",
                    "one validated continuation of this retained graph", "missing or stale");
            _receiptContinuation = null;
            var replacement = continuation.Extensions.ToDictionary(value => value.Original, value => value.Bound);
            var retainedCandidates = graphPreparation.TriggerCandidates.Select(candidate =>
                replacement.TryGetValue(candidate, out var bound) ? bound : candidate).ToArray();
            var candidateByMutation = retainedCandidates.SelectMany(candidate =>
                    candidate.PlannedMutationKeys.Select(key => (Key: key, Candidate: candidate)))
                .ToDictionary(pair => pair.Key, pair => pair.Candidate);
            var retained = graphPreparation with
            {
                TriggerCandidates = retainedCandidates,
                TriggerCandidatesByMutation = candidateByMutation
            };
            var deltaInput = new AcceptedMechanicsResourceInput(input.Turn,
                input.Definitions, input.State, input.History, continuation.Sources,
                continuation.NewMutations, EventMutationResolver: input.EventMutationResolver,
                EffectPlanAuthority: input.EffectPlanAuthority);
            var prepared = PrepareMutations(deltaInput, factory, identityRegistry);
            if (prepared.Issues.Count != 0)
                return prepared.Issues;
            var appended = PrepareCompleteResourceGraph(deltaInput,
                preparedMutations.Concat(prepared.Mutations).ToArray(), factory, identityRegistry, retained,
                completedOperationIds);
            AddGraphWork(appended.Work);
            if (!appended.IsValid)
                return appended.Issues;
            var seedIssues = session.Routing?.ValidateCandidateSeeds(appended.TriggerCandidates)
                ?? Array.Empty<ValidationIssue>();
            if (seedIssues.Count != 0)
                return seedIssues;
            var authorities = ValidatePendingCandidateAuthorities(appended.TriggerCandidates,
                appended.Mutations, appended.SourceExports, metrics);
            if (!authorities.IsValid)
                return authorities.Issues;
            foreach (var extension in continuation.Extensions)
            {
                var issue = effectTranscriptBuilder.BindPendingCandidateExtension(extension.Original, extension.Bound);
                if (issue != null)
                    return Issue(issue.Code, issue.Expected, issue.Actual);
                if (observedCandidates.Remove(extension.Original))
                    observedCandidates.Add(extension.Bound);
            }
            for (var index = 0; index < acceptedCandidates.Count; index++)
            {
                var accepted = acceptedCandidates[index];
                if (replacement.TryGetValue(accepted.Candidate, out var bound))
                    acceptedCandidates[index] = (bound, accepted.Activation);
            }
            foreach (var values in acceptedByBoundary.Values)
            {
                for (var index = 0; index < values.Count; index++)
                {
                    var accepted = values[index];
                    if (replacement.TryGetValue(accepted.Candidate, out var bound))
                        values[index] = (bound, accepted.Activation, accepted.Boundary);
                }
            }
            graphPreparation = appended;
            preparedMutations = appended.Mutations;
            candidateAuthorities = authorities;
            foreach (var mutation in preparedMutations)
            {
                if (byOperationId.TryAdd(mutation.OperationId, mutation))
                    operationIdByKey.Add(mutation.Intent.Key, mutation.OperationId);
            }
            foreach (var pair in acceptedByBoundary.Where(pair => !closedBoundaries.Contains(pair.Key)))
            {
                var roots = pair.Value.SelectMany(accepted => accepted.Candidate.PlannedMutationKeys)
                    .Select(key => operationIdByKey[key]).ToArray();
                var closure = appended.Graph!.ExpandCausalOperationClosure(roots).ToHashSet(StringComparer.Ordinal);
                var stableKeys = closure.ToDictionary(id => id,
                    id => CreateStableProducerOperationKey(byOperationId[id].Intent.Key), StringComparer.Ordinal);
                var boundary = effectTranscriptBuilder.FindBoundary(pair.Key)!;
                var issue = effectTranscriptBuilder.ExtendOpenCausalClosure(boundary, stableKeys);
                if (issue != null)
                    return Issue(issue.Code, issue.Expected, issue.Actual);
                remainingOperationsByBoundary[pair.Key].UnionWith(
                    closure.Where(id => !completedOperationIds.Contains(id)));
                if (pair.Value.Any(accepted => accepted.Candidate.PendingOutputs.Any(output =>
                        !accepted.Candidate.TryResolvePendingBinding(output.ComponentId, out _) &&
                        (output.AfterComponentId == null || HasAppliedComponent(accepted.Candidate, output.AfterComponentId)))))
                    unresolvedPendingBoundaries.Add(pair.Key);
                else
                    unresolvedPendingBoundaries.Remove(pair.Key);
            }
            candidatesByProducerEvent = appended.TriggerCandidates.Where(candidate => candidate.Producer != null)
                .GroupBy(candidate => (Producer: candidate.Producer!, candidate.Activation.Identity.EventKind))
                .ToDictionary(group => group.Key,
                    group => (IReadOnlyList<EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>)group.ToArray());
            metrics.MutationDescriptorCount = preparedMutations.Count;
            metrics.GraphNodeDescriptorCount = appended.Graph!.OrderedNodes.Count;
            metrics.MaximumTriggerDepth = appended.Graph.MaximumDepth;
            scheduler = appended.Graph.CreateExecutionScheduler(
                node => RequirementsSatisfied(byOperationId[node.OperationId], producedEvents), completedOperationIds);
            _livePendingWave = continuation.NextWaveOrdinal;
            _pendingAcceptedOutputs = Array.Empty<AcceptedEffectBoundedResourceResolution>();
            return Array.Empty<ValidationIssue>();
        }
    }
}
