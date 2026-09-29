namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Selects this capture's actual effect draft for its exact current resource and source owners.
        /// </summary>
        /// <param name="resources">
        /// Resource executor requesting a read binding.
        /// </param>
        /// <param name="source">
        /// Source session requesting a read binding.
        /// </param>
        /// <returns>
        /// The owned draft, or null when the original ownership does not match.
        /// </returns>
        internal EffectAcceptedTurnPlanner.EffectAcceptedDraft? ReadOwnedWoundDraft(
            AcceptedMechanicsPlanner.ResourceExecutionSession resources, SpiritualWoundSourceSession source) =>
            OwnsMechanicsOwner(resources, source) ? _effects : null;

        /// <summary>
        /// Checks the exact draft and immutable original effect base used by a wound read.
        /// </summary>
        /// <param name="draft">
        /// Draft requesting its initial wound seed.
        /// </param>
        /// <param name="acceptedBase">
        /// Immutable effect base retained by that draft.
        /// </param>
        /// <returns>
        /// True for this live capture's exact draft and base; otherwise false.
        /// </returns>
        internal bool OwnsWoundReadBase(EffectAcceptedTurnPlanner.EffectAcceptedDraft draft,
            EffectAcceptedTurnPlan acceptedBase) => IsCurrentOwner && ReferenceEquals(_effects, draft) &&
            ReferenceEquals(_input.PlanningContext?.EffectPlan, acceptedBase);

        /// <summary>
        /// Reads the signed initial wound state, including the accepted initial stage, for the owned draft.
        /// </summary>
        /// <param name="draft">
        /// Actual effect draft requesting a version-zero seed.
        /// </param>
        /// <param name="acceptedBase">
        /// Exact original effect base retained by the draft.
        /// </param>
        /// <param name="issues">
        /// Receives ownership or initial-state validation failures.
        /// </param>
        /// <returns>
        /// Detached initial data, or null when ownership or initial-state validation fails.
        /// </returns>
        internal WoundOperationBeforeData? ReadInitialWoundReadSeed(
            EffectAcceptedTurnPlanner.EffectAcceptedDraft draft, EffectAcceptedTurnPlan acceptedBase,
            List<ValidationIssue> issues)
        {
            if (OwnsWoundReadBase(draft, acceptedBase))
                return ReadInitialWoundState(issues);
            issues.Add(SourceIssue(AfterlifeSpiritualConflictState.StatePath,
                "spiritual_wound_read_stale", "the exact live draft and original effect base"));
            return null;
        }
    }
}
