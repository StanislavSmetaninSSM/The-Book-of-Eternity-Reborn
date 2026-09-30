namespace BookOfEternityClient.Services;

internal static partial class EffectAcceptedTurnPlanner
{
    // Immutable-base routing data only. This owns no draft or completion authority.
    internal sealed partial class BaseResourceRouting
    {
        private readonly EffectAcceptedTurnPlan _plan;
        private readonly ResourceOwnerAuthority _owners;
        private readonly ResourceDefinitionCatalog _definitions;
        private readonly IReadOnlyList<CanonicalEffectUseSeed> _seeds;
        private WoundRoutingPreparation? _currentWoundRouting;
        private EffectAcceptedDraft.EffectDraftWoundInsertion? _currentWoundInsertion;
        private readonly Dictionary<string, CanonicalEffectUseSeed> _registeredSeeds = new(StringComparer.Ordinal);

        private BaseResourceRouting(
            EffectAcceptedTurnPlan plan,
            ResourceOwnerAuthority owners,
            ResourceDefinitionCatalog definitions)
        {
            _plan = plan;
            _owners = owners;
            _definitions = definitions;
            _seeds = plan.ResourceTriggerIndex.CanonicalUseSeeds;
            foreach (var seed in _seeds)
                _registeredSeeds.Add(seed.EffectId, seed);
        }

        internal static BaseResourceRouting Capture(
            EffectAcceptedTurnPlan plan,
            ResourceOwnerAuthority owners,
            ResourceDefinitionCatalog definitions)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(owners);
            ArgumentNullException.ThrowIfNull(definitions);
            if (plan.IsAcceptedBoundaryComplete)
                throw new ArgumentException("Live base routing requires the original uncompleted effect plan.", nameof(plan));
            if (plan.ResourceTriggerIndex.Issues.Count != 0)
                throw new ArgumentException("Live base routing requires the valid retained effect index.", nameof(plan));
            return new BaseResourceRouting(plan, owners, definitions);
        }

        internal EffectAcceptedPlanAuthorityStamp PlanAuthority =>
            AcceptedMechanicsPlanner.CreateEffectPlanAuthority(_plan);

        internal IReadOnlyList<CanonicalEffectUseSeed> CanonicalUseSeeds =>
            Array.AsReadOnly(_seeds.ToArray());

        internal bool OwnsContext(EffectAcceptedTurnPlan? plan, ResourceOwnerAuthority owners) =>
            ReferenceEquals(_plan, plan) && ReferenceEquals(_owners, owners);

        internal bool OwnsDefinitions(ResourceDefinitionCatalog definitions) =>
            ReferenceEquals(_definitions, definitions);

        internal EffectPeriodicResourceResolution ResolveInitial() =>
            ResolveDuePeriodicResourceMutations(_plan, _owners, _definitions);

        internal EffectPeriodicResourceResolution Resolve(
            ResourceAppliedEvent resourceEvent,
            ResourceOperationKey producer) =>
            ResolveResourceEventMutationCandidates(_plan, resourceEvent, producer, _owners, _definitions,
                _currentWoundRouting);

        /// <summary>
        /// Resolves only the exact inserted roots against a previously prepared future producer.
        /// </summary>
        /// <param name="resourceEvent">
        /// Potential event of the still-unfinished retained producer.
        /// </param>
        /// <param name="producer">
        /// Exact prepared producer key retained by the resource owner.
        /// </param>
        /// <param name="insertion">
        /// Actual currently installed insertion; stale and copied insertions are rejected.
        /// </param>
        /// <returns>
        /// New root candidates and their genuine resource consequences, without repeating old candidates.
        /// </returns>
        internal EffectPeriodicResourceResolution ResolveInsertedWoundRoots(ResourceAppliedEvent resourceEvent,
            ResourceOperationKey producer, EffectAcceptedDraft.EffectDraftWoundInsertion insertion)
        {
            if (!ReferenceEquals(_currentWoundInsertion, insertion))
                throw new InvalidOperationException("Only the exact installed wound can extend future candidates.");
            return ResolveResourceEventMutationCandidates(_plan, resourceEvent, producer, _owners, _definitions,
                _currentWoundRouting, insertion.Applications.Select(value => value.EffectId).ToHashSet(StringComparer.Ordinal));
        }

        internal IReadOnlyList<ValidationIssue> ValidateCandidateSeeds(
            IReadOnlyList<EffectResourceTriggerCandidate> candidates)
        {
            var seeds = _registeredSeeds;
            var issues = new List<ValidationIssue>();
            foreach (var candidate in candidates)
            {
                if (seeds.TryGetValue(candidate.Activation.Identity.EffectId, out var seed)
                    ? candidate.UseSeed != seed
                    : candidate.UseSeed != null || candidate.Activation.ConsumesUse)
                {
                    ResourceMaterializationContract.AddIssue(issues,
                        ResourceMaterializationContract.CommandPath,
                        "effect_live_base_use_seed_mismatch",
                        "the exact original indexed canonical use seed",
                        candidate.Activation.Identity.EffectId);
                }
            }
            return issues;
        }
    }
}
