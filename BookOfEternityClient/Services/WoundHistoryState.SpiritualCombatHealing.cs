namespace BookOfEternityClient.Services;

internal sealed partial class WoundHistoryState
{
    private static void ValidateCombatHealingReplayAgreement(IEnumerable<WoundHistoryTransition> transitions,
        string path, ICollection<ValidationIssue> issues)
    {
        var results = transitions.Where(r => r.TransitionResult is SpiritualCombatHealingPersistedResult).ToArray();
        if (results.Length == 0) return;
        var rows = transitions.ToLookup(r => r.TransitionId, StringComparer.Ordinal);
        foreach (var primary in results)
        {
            var result = (SpiritualCombatHealingPersistedResult)primary.TransitionResult!;
            var stages = result.Stages;
            if (stages.Count == 0 || primary.Kind != "treat" || primary.TransitionId != stages[0].TransitionId)
            { AddIssue(issues, path, "spiritual_combat_healing_history_invalid", "one exact combat primary treat row", primary.TransitionId); continue; }
            foreach (var stage in stages)
            {
                var matching = rows[stage.TransitionId].ToArray();
                if (matching.Length != 1 || matching[0] is not { } row || row.WoundId != stage.After.WoundId ||
                    row.Kind != stage.Kind || row.Turn != stage.After.LastTransition.Turn || row.EventRef != stage.EventRef ||
                    row.OperationKey != stage.OperationKey || row.WoundTransitionOrdinal != stage.After.LastTransition.Ordinal ||
                    row.BeforeFingerprint != WoundIdentityState.ComputeSemanticFingerprint(stage.Before) ||
                    row.AfterFingerprint != WoundIdentityState.ComputeSemanticFingerprint(stage.After) ||
                    row.SourceFingerprint != result.SourceFingerprint || row.AttemptId != stage.AttemptId || row.CycleKey is not null ||
                    row.Terminal != stage.Terminal || row.CourseId is not null || row.CourseMilestoneOrdinal is not null ||
                    row.PaymentFingerprint is not null || row.TransitionId != primary.TransitionId && row.TransitionResult is not null)
                    AddIssue(issues, path, "spiritual_combat_healing_history_invalid", "complete exact combat stages without a natural cycle", stage.TransitionId);
            }
        }
    }
}
