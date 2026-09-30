using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed partial class MortalOriginalTurnCapture
    {
        /// <summary>
        /// Checks that a current wound read belongs to this locked original capture and exact executor boundary.
        /// </summary>
        /// <param name="draft">
        /// Actual shared draft requesting the read.
        /// </param>
        /// <param name="plan">
        /// Original accepted effect plan retained by that draft.
        /// </param>
        /// <param name="resources">
        /// Actual executor paused at the supplied checkpoint.
        /// </param>
        /// <param name="checkpoint">
        /// Exact current closed resource checkpoint.
        /// </param>
        /// <returns>
        /// True for the locked current capture and its exact owners; otherwise false.
        /// </returns>
        internal bool OwnsWoundReadBase(EffectAcceptedTurnPlanner.EffectAcceptedDraft draft,
            EffectAcceptedTurnPlan plan, AcceptedMechanicsPlanner.ResourceExecutionSession resources,
            AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint checkpoint) =>
            IsCurrentOwner && _gate.CurrentCount == 0 && ReferenceEquals(_effects, draft) &&
            ReferenceEquals(_input.PlanningContext!.EffectPlan, plan) && ReferenceEquals(_resources, resources) &&
            resources.OwnsCurrentCheckpoint(checkpoint);

        /// <summary>
        /// Reads detached original wound baselines after the draft has authenticated this capture's ownership.
        /// </summary>
        /// <returns>
        /// Original wound images; current effect images are supplied separately by the actual draft.
        /// </returns>
        internal WoundOperationBeforeData ReadInitialWoundReadSeed() =>
            new(_selection.Input.PreTurnCarriers, _selection.Input.PreTurnIdentityIndex,
                _selection.Input.PreTurnHistory, null, null);

        internal sealed partial class WoundSelection
        {
            /// <summary>
            /// Recomposes one frozen original decision against the actual current wound generation without changing signed rows.
            /// </summary>
            /// <param name="owner">
            /// Locked original capture whose retained input freshness was checked before this call.
            /// </param>
            /// <param name="checkpoint">
            /// Exact current resource checkpoint selected for insertion.
            /// </param>
            /// <param name="row">
            /// Detached row selected internally from the capture's authenticated original commands.
            /// </param>
            /// <param name="issues">
            /// Receives current-state, proposal or binding validation failures.
            /// </param>
            /// <returns>
            /// A concrete selection ready for owner registration, or null on any validation failure.
            /// </returns>
            private static WoundSelection? SelectCurrent(MortalOriginalTurnCapture owner,
                AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint checkpoint,
                WoundResponseCommandDraft row, List<ValidationIssue> issues)
            {
                var current = owner._effects.ReadCurrentWoundView(owner, owner._resources!, checkpoint,
                    owner._effects.WoundReadVersion, owner._effects.DependencyCutVersion, issues);
                if (current == null || issues.Count != 0)
                    return null;
                var opportunity = row.Opportunity;
                if (opportunity.WorseningTarget is { } originalTarget)
                {
                    var catalog = WoundCarrierCatalog.Build(current.WoundCarriers!);
                    issues.AddRange(catalog.Issues);
                    if (!catalog.TryResolveOne(originalTarget.Wound.WoundId, out var target) ||
                        target.Wound.Owner != opportunity.Owner || target.Wound.Classification.Domain != opportunity.Domain)
                    {
                        issues.Add(WoundIssue(AcceptedMechanicsPlan.WoundCommandPath,
                            "mortal_wound_current_target_mismatch", "the original target in the owned current wound view", "missing or changed"));
                        return null;
                    }
                    // Only the current owned draft can supply this replacement target.
                    // OriginalCommands and all PreTurn images remain signed-original data.
                    opportunity = opportunity with
                    {
                        WorseningTarget = new(target.Wound, originalTarget.CauseKind,
                            WoundIdentityState.ComputeSemanticFingerprint(target.Wound))
                    };
                    opportunity = opportunity with
                    {
                        AuthorityFingerprint = WoundOpportunityAuthority.RecomputeAuthorityFingerprint(opportunity)
                    };
                }
                var original = owner._selection.Input;
                var composed = WoundResponseInputComposer.Compose(original.Binding, new[] { opportunity },
                    new[] { row.Decision }, row.FinalSceneText, Array.Empty<WoundOpportunityDecisionReceipt>());
                issues.AddRange(composed.Issues);
                if (!composed.Success || composed.Transitions.Count != 1)
                    return null;
                var plan = owner._input.PlanningContext!.EffectPlan!;
                issues.AddRange(WoundAcceptedTurnPlanner.BindAcceptedSourceTargets(original.Binding,
                    composed.MaterializedOpportunities, plan.TargetAuthority).Issues);
                issues.AddRange(WoundResponseInputComposer.ValidateSkillScopes(original.Binding,
                    new[] { new WoundResponseCommandDraft(opportunity, row.Decision, row.FinalSceneText) },
                    composed.Transitions, plan.SkillScopeAuthority, out var locations));
                var input = new WoundAcceptedTurnInput(original.Binding, composed.MaterializedOpportunities,
                    composed.Transitions, original.PreTurnCarriers, original.PreTurnIdentityIndex,
                    original.PreTurnHistory, original.PreTurnEffectCarriers, original.PreTurnEffectIdentityIndex);
                issues.AddRange(WoundAcceptedTurnPlannerCore.ValidateInput(input, current).Issues);
                return issues.Count == 0 ? new(owner, checkpoint, input, locations) : null;
            }
        }
    }
}
