namespace BookOfEternityClient.Services;

internal static partial class AcceptedMechanicsPlanner
{
    private sealed partial class ResourceExecutionState
    {
        /// <summary>
        /// Replaces unobserved future work of a superseded wound generation and adds the new roots to unfinished producers.
        /// Retained objects, execution evidence, identities, use budgets and the accepted transcript remain unchanged.
        /// </summary>
        /// <param name="input">
        /// Actual original resource input owned by the paused executor.
        /// </param>
        /// <param name="identityFactory">
        /// Same identity allocator used for original resource preparation.
        /// </param>
        /// <param name="session">
        /// Executor holding the exact insertion registration gate.
        /// </param>
        /// <param name="insertion">
        /// Actual installed insertion at the current ordinary checkpoint.
        /// </param>
        /// <returns>
        /// Empty after a valid graph replacement; diagnostics otherwise require the owner to fault after allocation.
        /// </returns>
        internal IReadOnlyList<ValidationIssue> RefreshUnfinishedWoundCandidates(AcceptedMechanicsResourceInput input,
            AcceptedMechanicsIdentityFactory identityFactory, ResourceExecutionSession session,
            EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundInsertion insertion)
        {
            var routing = session.Routing;
            if (session.IsLive || routing == null ||
                !session.OwnsRegisteredWoundInstallation(routing, insertion) || !routing.HasInstalled(insertion))
                return Issue("mortal_wound_future_candidate_owner_mismatch",
                    "the actual installed insertion under its fixed resource registration gate", "foreign or stale");

            var retainedPreparation = graphPreparation;
            if (!insertion.IsCreation)
            {
                var validation = new List<ValidationIssue>();
                var installed = routing.PrepareWoundInsertion(insertion, validation);
                if (installed == null || validation.Count != 0)
                    return validation.Count != 0 ? validation : Issue("mortal_wound_future_candidate_owner_mismatch",
                        "the authenticated installed wound routing image", "unavailable");
                var identities = EffectIdentityState.Parse(
                    System.Text.Json.JsonSerializer.SerializeToElement(installed.State.EffectIdentity),
                    EffectAcceptedTurnPlan.IdentityIndexPath);
                if (identities.State == null || identities.Issues.Count != 0)
                    return identities.Issues;
                var wound = insertion.Wound;
                var groupIds = identities.State.ResolveSourceGroup(new(wound.Owner.Realm, "wound", wound.WoundId))
                    .Select(value => value.EffectId).ToHashSet(StringComparer.Ordinal);
                var retirement = PrepareFutureWoundCandidateRetirement(groupIds);
                if (!retirement.IsValid)
                    return retirement.Issues;
                retainedPreparation = retirement.Preparation!;
            }

            var retainedMutations = retainedPreparation.Mutations;
            var unfinished = retainedMutations.Where(value => !completedOperationIds.Contains(value.OperationId)).ToArray();
            var revisitIds = unfinished.Select(value => value.OperationId).ToHashSet(StringComparer.Ordinal);
            var revisitKeys = unfinished.Select(value => value.Intent.Key).ToHashSet();
            var catalog = ResourceMutationSourceCatalog.Create(retainedPreparation.SourceExports);
            if (!catalog.IsValid || catalog.Catalog == null)
                return catalog.Issues;
            var refreshInput = new AcceptedMechanicsResourceInput(input.Turn, input.Definitions, input.State,
                input.History, catalog.Catalog, Array.Empty<ResourceMutationIntent>(),
                EventMutationResolver: (resourceEvent, producer) => revisitKeys.Contains(producer)
                    ? routing.ResolveInsertedWoundRoots(resourceEvent, producer, insertion)
                    : routing.Resolve(resourceEvent, producer), EffectPlanAuthority: input.EffectPlanAuthority);
            var expanded = PrepareCompleteResourceGraph(refreshInput, retainedMutations, identityFactory,
                identityRegistry, retainedPreparation, completedOperationIds, revisitIds);
            AddGraphWork(expanded.Work);
            if (!expanded.IsValid)
                return expanded.Issues;
            var seedIssues = routing.ValidateCandidateSeeds(expanded.TriggerCandidates);
            if (seedIssues.Count != 0)
                return seedIssues;
            var authorities = ValidatePendingCandidateAuthorities(expanded.TriggerCandidates,
                expanded.Mutations, expanded.SourceExports, metrics);
            if (!authorities.IsValid)
                return authorities.Issues;

            var stagedByOperationId = expanded.Mutations.ToDictionary(value => value.OperationId,
                StringComparer.Ordinal);
            var stagedOperationIdByKey = expanded.Mutations.ToDictionary(value => value.Intent.Key,
                value => value.OperationId);
            if (producedEvents.Keys.Any(operationId => !stagedByOperationId.ContainsKey(operationId)) ||
                remainingOperationsByBoundary.Values.SelectMany(value => value)
                    .Any(operationId => !stagedByOperationId.ContainsKey(operationId)))
                return Issue("mortal_wound_future_candidate_retirement_observed",
                    "all observed and boundary-owned operations retained", "retirement removed execution evidence");
            var stagedCandidatesByProducerEvent = expanded.TriggerCandidates
                .Where(candidate => candidate.Producer != null)
                .GroupBy(candidate => (Producer: candidate.Producer!, candidate.Activation.Identity.EventKind))
                .ToDictionary(group => group.Key,
                    group => (IReadOnlyList<EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>)group.ToArray());
            var stagedScheduler = expanded.Graph!.CreateExecutionScheduler(
                node => RequirementsSatisfied(stagedByOperationId[node.OperationId], producedEvents),
                completedOperationIds);

            graphPreparation = expanded;
            preparedMutations = expanded.Mutations;
            candidateAuthorities = authorities;
            byOperationId = stagedByOperationId;
            operationIdByKey = stagedOperationIdByKey;
            candidatesByProducerEvent = stagedCandidatesByProducerEvent;
            scheduler = stagedScheduler;
            metrics.MutationDescriptorCount = preparedMutations.Count;
            metrics.GraphNodeDescriptorCount = expanded.Graph.OrderedNodes.Count;
            metrics.MaximumTriggerDepth = expanded.Graph.MaximumDepth;
            return Array.Empty<ValidationIssue>();
        }

        /// <summary>
        /// Builds a graph image without the unobserved future branch owned by a superseded wound source group.
        /// </summary>
        /// <param name="sourceGroupEffectIds">
        /// Exact effect identities belonging to all generations of the replaced wound source.
        /// </param>
        /// <returns>
        /// A staged retained graph image, or diagnostics when removal would rewrite observed execution state.
        /// </returns>
        private WoundCandidateRetirementResult PrepareFutureWoundCandidateRetirement(
            IReadOnlySet<string> sourceGroupEffectIds)
        {
            var preparedByKey = graphPreparation.Mutations.ToDictionary(value => value.Intent.Key);
            var preparedByOperationId = graphPreparation.Mutations.ToDictionary(value => value.OperationId,
                StringComparer.Ordinal);
            var protectedOperationIds = new HashSet<string>(completedOperationIds, StringComparer.Ordinal);
            protectedOperationIds.UnionWith(producedEvents.Keys);
            protectedOperationIds.UnionWith(events.Select(value => value.OperationId));
            protectedOperationIds.UnionWith(appliedTransitions.Select(value => value.OperationId));
            protectedOperationIds.UnionWith(replayTransitions.Select(value => value.OperationId));
            protectedOperationIds.UnionWith(remainingOperationsByBoundary.Values.SelectMany(value => value));
            var protectedKeys = appliedTriggerMutationKeys.Values.SelectMany(value => value).ToHashSet();
            foreach (var operationId in protectedOperationIds)
                if (preparedByOperationId.TryGetValue(operationId, out var prepared))
                    protectedKeys.Add(prepared.Intent.Key);

            bool CandidateIsProtected(EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate candidate) =>
                observedCandidates.Contains(candidate) ||
                candidate.Producer != null && protectedKeys.Contains(candidate.Producer) ||
                candidate.PlannedMutationKeys.Any(protectedKeys.Contains);

            var removedCandidates = new HashSet<EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>(
                ReferenceEqualityComparer.Instance);
            var removedKeys = new HashSet<ResourceOperationKey>();
            foreach (var candidate in graphPreparation.TriggerCandidates)
            {
                if (!sourceGroupEffectIds.Contains(candidate.Activation.Identity.EffectId) ||
                    CandidateIsProtected(candidate))
                    continue;
                removedCandidates.Add(candidate);
                removedKeys.UnionWith(candidate.PlannedMutationKeys);
            }

            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var candidate in graphPreparation.TriggerCandidates)
                {
                    if (removedCandidates.Contains(candidate) || candidate.Producer == null ||
                        !removedKeys.Contains(candidate.Producer))
                        continue;
                    if (CandidateIsProtected(candidate))
                        return WoundCandidateRetirementResult.Failure(Issue(
                            "mortal_wound_future_candidate_retirement_observed",
                            "no observed candidate or operation below the retired wound branch",
                            candidate.Activation.Identity.EffectId));
                    removedCandidates.Add(candidate);
                    removedKeys.UnionWith(candidate.PlannedMutationKeys);
                    changed = true;
                }
            }

            foreach (var key in removedKeys)
            {
                if (protectedKeys.Contains(key) || !preparedByKey.ContainsKey(key) ||
                    !graphPreparation.TriggerCandidatesByMutation.TryGetValue(key, out var candidate) ||
                    !removedCandidates.Contains(candidate))
                    return WoundCandidateRetirementResult.Failure(Issue(
                        "mortal_wound_future_candidate_retirement_invalid",
                        "each retired operation owned by one unobserved retired candidate",
                        Describe(key)));
            }

            var retainedCandidates = graphPreparation.TriggerCandidates
                .Where(candidate => !removedCandidates.Contains(candidate)).ToArray();
            var retainedMutations = graphPreparation.Mutations
                .Where(mutation => !removedKeys.Contains(mutation.Intent.Key)).ToArray();
            foreach (var mutation in retainedMutations)
            {
                if (mutation.Intent.Dependencies.Any(removedKeys.Contains) ||
                    mutation.Intent.EventRequirements.Any(requirement => removedKeys.Contains(requirement.Producer)))
                    return WoundCandidateRetirementResult.Failure(Issue(
                        "mortal_wound_future_candidate_retirement_dependency",
                        "no retained operation depends on a retired wound operation",
                        Describe(mutation.Intent.Key)));
            }
            foreach (var candidate in retainedCandidates)
            {
                if (candidate.Producer != null && removedKeys.Contains(candidate.Producer) ||
                    candidate.PlannedMutationKeys.Any(removedKeys.Contains))
                    return WoundCandidateRetirementResult.Failure(Issue(
                        "mortal_wound_future_candidate_retirement_dependency",
                        "no retained candidate references a retired wound operation",
                        candidate.Activation.Identity.EffectId));
            }

            var retainedCandidateSet = retainedCandidates.ToHashSet(ReferenceEqualityComparer.Instance);
            var retainedCandidatesByMutation = graphPreparation.TriggerCandidatesByMutation
                .Where(pair => !removedKeys.Contains(pair.Key) && retainedCandidateSet.Contains(pair.Value))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            if (retainedCandidatesByMutation.Count != retainedCandidates.Sum(value => value.PlannedMutationKeys.Count))
                return WoundCandidateRetirementResult.Failure(Issue(
                    "mortal_wound_future_candidate_retirement_invalid",
                    "one retained candidate owner for every retained planned mutation",
                    retainedCandidatesByMutation.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));

            var removedSourceKeys = removedCandidates.SelectMany(candidate => candidate.Origin.SourceExports)
                .Select(source => (source.SourceKind, source.SourceId)).ToHashSet();
            var retainedSourceKeys = retainedCandidates.SelectMany(candidate => candidate.Origin.SourceExports)
                .Select(source => (source.SourceKind, source.SourceId))
                .Concat(retainedMutations.Select(mutation =>
                    (mutation.Intent.Source.SourceKind, mutation.Intent.Source.SourceId)))
                .ToHashSet();
            var retainedSources = graphPreparation.SourceExports.Where(source =>
                !removedSourceKeys.Contains((source.SourceKind, source.SourceId)) ||
                retainedSourceKeys.Contains((source.SourceKind, source.SourceId))).ToArray();
            var retainedGraph = BuildGraph(retainedMutations, completedOperationIds);
            if (!retainedGraph.IsValid)
                return WoundCandidateRetirementResult.Failure(retainedGraph.Issues);
            return WoundCandidateRetirementResult.Success(graphPreparation with
            {
                Mutations = retainedMutations,
                Graph = retainedGraph.Graph,
                TriggerCandidates = retainedCandidates,
                TriggerCandidatesByMutation = retainedCandidatesByMutation,
                SourceExports = retainedSources
            });
        }

        /// <summary>
        /// Carries either a fully validated retained graph image or the diagnostics that prevented retirement.
        /// </summary>
        /// <param name="Preparation">
        /// Staged graph image that preserves all observed work; <see langword="null"/> when validation failed.
        /// </param>
        /// <param name="Issues">
        /// Validation diagnostics. The list is empty when <paramref name="Preparation"/> is available.
        /// </param>
        private sealed record WoundCandidateRetirementResult(
            CompleteResourceGraphPreparation? Preparation,
            IReadOnlyList<ValidationIssue> Issues)
        {
            /// <summary>
            /// Gets whether the result contains one staged graph image and no diagnostics.
            /// </summary>
            internal bool IsValid => Preparation != null && Issues.Count == 0;

            /// <summary>
            /// Creates a successful result for <paramref name="preparation"/>.
            /// </summary>
            /// <param name="preparation">
            /// Fully validated retained graph image.
            /// </param>
            /// <returns>
            /// A successful retirement result with no diagnostics.
            /// </returns>
            internal static WoundCandidateRetirementResult Success(CompleteResourceGraphPreparation preparation) =>
                new(preparation, Array.Empty<ValidationIssue>());

            /// <summary>
            /// Creates a failed result from <paramref name="issues"/> without a staged graph image.
            /// </summary>
            /// <param name="issues">
            /// Diagnostics that prevented safe retirement.
            /// </param>
            /// <returns>
            /// A failed retirement result carrying the supplied diagnostics.
            /// </returns>
            internal static WoundCandidateRetirementResult Failure(IReadOnlyList<ValidationIssue> issues) =>
                new(null, issues);
        }
    }
}
