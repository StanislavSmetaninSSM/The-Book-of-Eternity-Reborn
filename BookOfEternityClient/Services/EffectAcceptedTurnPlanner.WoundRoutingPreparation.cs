namespace BookOfEternityClient.Services;

internal static partial class EffectAcceptedTurnPlanner
{
    internal sealed partial class BaseResourceRouting
    {
        private readonly Dictionary<EffectAcceptedDraft.EffectDraftWoundInsertion, WoundRoutingPreparation>
            _woundPreparations = new();

        /// <summary>
        /// Reads all original allocated instance identities, including retired and unmetered instances.
        /// </summary>
        /// <returns>
        /// Detached original identities used to reject registration of an existing instance.
        /// </returns>
        internal IReadOnlyList<string> ReadOriginalInstanceIds() =>
            Array.AsReadOnly(ParseIdentity(_plan.IdentityIndexAfterImage).State!.Entries
                .Select(entry => entry.EffectId).ToArray());

        /// <summary>
        /// Validates one current insertion's routing and mechanics images without installing them.
        /// The original base plan, resource routing and use budgets remain unchanged.
        /// </summary>
        /// <param name="insertion">
        /// Exact registered insertion of this routing owner's original effect base.
        /// </param>
        /// <param name="issues">
        /// Receives ownership, source graph, carrier, identity or mechanics failures.
        /// </param>
        /// <returns>
        /// A retained preparation for future owner registration, or <see langword="null"/> on rejection.
        /// </returns>
        internal WoundRoutingPreparation? PrepareWoundInsertion(
            EffectAcceptedDraft.EffectDraftWoundInsertion insertion, List<ValidationIssue> issues) =>
            WoundRoutingPreparation.Prepare(this, insertion, issues);

        /// <summary>
        /// Retains one validated insertion-time view before the resource owner installs its routing version.
        /// It grants no exchange continuation or common publication authority.
        /// </summary>
        internal sealed class WoundRoutingPreparation
        {
            /// <summary>
            /// Retains the consistent source, carrier and mechanics projections derived from an owned insertion.
            /// </summary>
            /// <param name="state">
            /// Immutable actual insertion-time wound and effect images.
            /// </param>
            /// <param name="sources">
            /// Validated combined source catalog preserving original exports and grants.
            /// </param>
            /// <param name="roots">
            /// Original and newly created application root bindings.
            /// </param>
            /// <param name="index">
            /// Trigger index built from the same effect carriers.
            /// </param>
            /// <param name="lineage">
            /// Wound lineage validated against the same identity and carrier images.
            /// </param>
            /// <param name="mechanics">
            /// Mechanical components projected from those effect images.
            /// </param>
            /// <param name="insertion">
            /// Exact retained draft insertion that produced every projection in this preparation.
            /// </param>
            private WoundRoutingPreparation(WoundOperationBeforeData state, EffectSourceAuthority sources,
                IReadOnlyList<WoundApplicationRootEffectBinding> roots, EffectResourceTriggerIndex index,
                WoundReactionLineageAuthority lineage, EffectMechanicsSnapshot mechanics,
                EffectAcceptedDraft.EffectDraftWoundInsertion insertion)
            {
                State = state;
                Sources = sources;
                Roots = Array.AsReadOnly(roots.ToArray());
                Index = index;
                Lineage = lineage;
                Mechanics = mechanics;
                Insertion = insertion;
            }

            /// <summary>
            /// Gets the immutable insertion-time snapshots with detached JSON accessors.
            /// </summary>
            internal WoundOperationBeforeData State { get; }
            /// <summary>
            /// Gets the source catalog composed for these images.
            /// </summary>
            internal EffectSourceAuthority Sources { get; }
            /// <summary>
            /// Gets the original and inserted application root bindings.
            /// </summary>
            internal IReadOnlyList<WoundApplicationRootEffectBinding> Roots { get; }
            /// <summary>
            /// Gets the trigger index for these effect carriers.
            /// </summary>
            internal EffectResourceTriggerIndex Index { get; }
            /// <summary>
            /// Gets the validated wound lineage for this view.
            /// </summary>
            internal WoundReactionLineageAuthority Lineage { get; }
            /// <summary>
            /// Gets mechanical contributions from the same carrier and identity images.
            /// </summary>
            internal EffectMechanicsSnapshot Mechanics { get; }
            /// <summary>
            /// Gets the exact retained insertion that produced this composite routing view.
            /// </summary>
            internal EffectAcceptedDraft.EffectDraftWoundInsertion Insertion { get; }

            /// <summary>
            /// Validates actual insertion ownership before retaining or returning a prepared view.
            /// </summary>
            /// <param name="owner">
            /// Routing owner retaining the exact original base plan.
            /// </param>
            /// <param name="insertion">
            /// Actual current insertion receipt; copied and foreign receipts are rejected.
            /// </param>
            /// <param name="issues">
            /// Receives ownership and projection validation issues.
            /// </param>
            /// <returns>
            /// The retained valid view, or <see langword="null"/> when the insertion cannot be routed.
            /// </returns>
            internal static WoundRoutingPreparation? Prepare(BaseResourceRouting owner,
                EffectAcceptedDraft.EffectDraftWoundInsertion insertion, List<ValidationIssue> issues)
            {
                if (insertion is null || !insertion.TryReadRoutingState(owner._plan,
                        out var state, out var prepared, out var addedRoots))
                {
                    Add(issues, "acceptedTurn.wounds", "spiritual_wound_routing_insertion_mismatch",
                        "an exact current insertion of the retained effect base", "unavailable");
                    return null;
                }
                if (owner._woundPreparations.TryGetValue(insertion, out var retained))
                    return retained;
                var previousSources = owner._currentWoundRouting?.Sources ?? owner._plan.SourceAuthority;
                var sources = previousSources.WithNewPreparedWound(prepared!);
                issues.AddRange(sources.Issues);
                var carriers = state!.EffectCarriers!;
                var identity = state.EffectIdentity!;
                var parsed = ParseIdentity(identity);
                issues.AddRange(parsed.Issues);
                var catalog = EffectCarrierCatalog.Build(carriers);
                issues.AddRange(catalog.Issues);
                var replacedRootRefs = new HashSet<string>(StringComparer.Ordinal);
                foreach (var batch in prepared!.EffectOperationBatches.Where(batch => batch.TransitionAuthority.TransitionKind == "worsen"))
                    if (previousSources.TryResolveWoundGroup(
                            new EffectIdentitySourceGroup(prepared.Binding.Realm, "wound", batch.PreparedWoundId), out var previousGroup))
                        foreach (var row in previousGroup.ApplicationRootLineage)
                            if (row.ApplicationRef != null)
                                replacedRootRefs.Add(row.ApplicationRef);
                var roots = (owner._currentWoundRouting?.Roots ?? owner._plan.WoundApplicationRootEffectBindings)
                    .Where(root => !replacedRootRefs.Contains(root.ApplicationRef)).Concat(addedRoots).ToArray();
                if (issues.Count != 0 || parsed.State is null)
                    return null;
                var previousLineage = owner._currentWoundRouting?.Lineage ?? WoundReactionLineageAuthority.Build(
                    owner._plan.SourceAuthority, ParseIdentity(owner._plan.IdentityIndexAfterImage).State!,
                    EffectCarrierCatalog.Build(owner._plan.ResourceTriggerCarriers), owner._plan.WoundApplicationRootEffectBindings);
                var lineage = WoundReactionLineageAuthority.Build(sources, parsed.State, catalog, roots, previousLineage, insertion);
                issues.AddRange(lineage.Issues);
                var index = CreateResourceTriggerIndex(carriers, owner._plan.TargetAuthority);
                issues.AddRange(index.Issues);
                var mechanics = EffectMechanicsSnapshot.Build(new EffectMechanicsInput(
                    carriers, identity, owner._plan.SkillScopeAuthority));
                issues.AddRange(mechanics.Issues);
                if (issues.Count != 0 || !mechanics.IsAccepted)
                    return null;
                var result = new WoundRoutingPreparation(state, sources, roots, index, lineage, mechanics, insertion);
                owner._woundPreparations.Add(insertion, result);
                return result;
            }
        }
    }
}
