using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class EffectAcceptedTurnPlanner
{
    internal sealed partial class BaseResourceRouting
    {
        private EffectMechanicsSnapshot? _originalMechanics;

        /// <summary>
        /// Gets the retained routing epoch identity; serialization cannot recreate it.
        /// </summary>
        internal object RoutingEpoch => (object?)_currentWoundInsertion ?? _plan;

        /// <summary>
        /// Installs a validated insertion only during its actual resource owner's registered insertion transaction.
        /// </summary>
        /// <param name="resources">
        /// Resource owner holding its exclusive mutation gate with the exact insertion registered.
        /// </param>
        /// <param name="insertion">
        /// Current draft insertion whose source, carrier, index and mechanics views are installed together.
        /// </param>
        /// <param name="issues">
        /// Receives ownership and consistent-view validation failures.
        /// </param>
        /// <returns>
        /// True after installing the actual view or accepting an exact retry; false on rejection.
        /// </returns>
        internal bool InstallWoundInsertion(AcceptedMechanicsPlanner.ResourceExecutionSession resources,
            EffectAcceptedDraft.EffectDraftWoundInsertion insertion, List<ValidationIssue> issues)
        {
            if (!resources.OwnsRegisteredWoundInstallation(this, insertion))
            {
                Add(issues, "acceptedTurn.wounds", "spiritual_wound_routing_owner_mismatch",
                    "actual resource owner's registered insertion transaction", "unavailable");
                return false;
            }
            var prepared = PrepareWoundInsertion(insertion, issues);
            if (prepared == null || issues.Count != 0)
                return false;
            if (HasInstalled(insertion))
                return true;
            foreach (var seed in prepared.Index.CanonicalUseSeeds)
                if (_registeredSeeds.TryGetValue(seed.EffectId, out var retained) && retained != seed)
                {
                    Add(issues, "acceptedTurn.wounds", "spiritual_wound_routing_seed_changed",
                        "unchanged historical canonical seed", seed.EffectId);
                    return false;
                }
            foreach (var seed in prepared.Index.CanonicalUseSeeds)
                _registeredSeeds.TryAdd(seed.EffectId, seed);
            _currentWoundRouting = prepared;
            _currentWoundInsertion = insertion;
            return true;
        }

        /// <summary>
        /// Checks whether the exact insertion supplies the currently installed routing view.
        /// </summary>
        /// <param name="insertion">
        /// Retained insertion to compare by identity.
        /// </param>
        /// <returns>
        /// True only for the currently installed insertion.
        /// </returns>
        internal bool HasInstalled(EffectAcceptedDraft.EffectDraftWoundInsertion insertion) =>
            ReferenceEquals(_currentWoundInsertion, insertion) &&
            ReferenceEquals(_currentWoundRouting?.Insertion, insertion);

        /// <summary>
        /// Reads the current owner-installed wound source and lineage authority for a later local dependency cut.
        /// </summary>
        /// <param name="sources">
        /// Receives the current source catalog, including definitions introduced by prior insertions.
        /// </param>
        /// <param name="roots">
        /// Receives current application-root bindings after superseded generation roots were removed.
        /// </param>
        /// <param name="lineage">
        /// Receives lineage built from the same installed carrier and identity epoch.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when an inserted routing epoch is installed; otherwise, <see langword="false"/>.
        /// </returns>
        internal bool TryReadCurrentWoundReactionView(out EffectSourceAuthority sources,
            out IReadOnlyList<WoundApplicationRootEffectBinding> roots, out WoundReactionLineageAuthority lineage)
        {
            if (_currentWoundRouting == null || _currentWoundInsertion == null ||
                !ReferenceEquals(_currentWoundRouting.Insertion, _currentWoundInsertion))
            {
                sources = null!;
                roots = Array.Empty<WoundApplicationRootEffectBinding>();
                lineage = null!;
                return false;
            }
            sources = _currentWoundRouting.Sources;
            roots = _currentWoundRouting.Roots;
            lineage = _currentWoundRouting.Lineage;
            return true;
        }

        /// <summary>
        /// Projects spiritual mechanics from one consistent installed epoch against signed conflict membership.
        /// </summary>
        /// <param name="conflict">
        /// Detached signed conflict root supplied by the actual source owner.
        /// </param>
        /// <returns>
        /// Detached contributions or issues from the original or installed current view.
        /// </returns>
        internal SpiritualWoundConflictContributionProjection ProjectSpiritualMechanics(JsonObject conflict)
        {
            var mechanics = _currentWoundRouting?.Mechanics ?? (_originalMechanics ??=
                EffectMechanicsSnapshot.Build(new EffectMechanicsInput(_plan.ResourceTriggerCarriers,
                    _plan.IdentityIndexAfterImage, _plan.SkillScopeAuthority)));
            return SpiritualWoundConflictContributionProjector.Project(mechanics,
                _currentWoundRouting?.Sources ?? _plan.SourceAuthority, _plan.TargetAuthority, conflict);
        }
    }
}
