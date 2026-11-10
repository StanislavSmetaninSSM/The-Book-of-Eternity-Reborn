namespace BookOfEternityClient.Services;

internal static partial class AcceptedMechanicsPlanner
{
    internal sealed partial class ResourceExecutionSession
    {
        internal sealed partial class SpiritualMechanicsContext
        {
            /// <summary>
            /// Reads the bounded pressure projection's carrier exclusion through the exact live resource owner.
            /// This does not predict triggers, expose carrier roots or alter ordinary non-pressure routing.
            /// </summary>
            /// <returns>
            /// True when original conflict-carrier participation prevents the pressure projection;
            /// stale, foreign or staged ownership throws instead of returning an admissible answer.
            /// </returns>
            internal bool HasPressureConflictCarrierCollision()
            {
                _owner.Enter();
                try
                {
                    _owner.EnsureUsable();
                    if (!IsCurrent || !_owner._live || _owner._active != null ||
                        _owner._staged != null || _owner._pendingExchange ||
                        _owner._pendingInput?.PlanningContext is not { } planning ||
                        _owner._routing is not { } routing ||
                        !routing.OwnsContext(planning.EffectPlan, planning.Owners) ||
                        !routing.OwnsDefinitions(_owner._originalResourceInput.Definitions) ||
                        !_owner.OwnsOriginalCompletionOwners(_woundDraft, _source))
                        throw new InvalidOperationException(
                            "Pressure carrier exclusion requires the exact current unstaged resource and effect owners.");
                    return routing.HasPressureConflictCarrierCollision();
                }
                finally { _owner.Exit(); }
            }
        }
    }
}
