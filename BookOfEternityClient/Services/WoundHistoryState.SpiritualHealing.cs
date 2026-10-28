namespace BookOfEternityClient.Services;

internal sealed partial class WoundHistoryState
{
    private static void ValidateSpiritualHealingReplayAgreement(IEnumerable<WoundHistoryTransition> transitions,
        string path, ICollection<ValidationIssue> issues)
    {
        var rows = transitions.ToArray();
        // Operation-local DATA index only when this current family exists. Other
        // history reads retain their prior allocation/work; duplicates stay visible.
        var grants = rows.Any(r => r.TransitionResult is SpiritualHealingPersistedResult { Mode: "aid" })
            ? rows.Select(r => r.TransitionResult).OfType<SpiritualHealingPersistedResult>()
                .Where(r => r.IsNatural && r.SchemaVersion is 3 or 4).ToLookup(r => r.ResultFingerprint, StringComparer.Ordinal)
            : null;
        foreach (var primary in rows.Where(r => r.TransitionResult is SpiritualHealingPersistedResult))
        {
            var result = (SpiritualHealingPersistedResult)primary.TransitionResult!;
            var stages = result.Stages;
            if (result.Mode == "aid" && grants![result.AidGrantFingerprint!].Count(result.AidGrantAgrees) != 1)
                AddIssue(issues, path, "spiritual_aid_grant_history_invalid", "one exact previous accepted participation result", primary.TransitionId);
            if (stages.Count == 0 || primary.TransitionId != stages[0].TransitionId ||
                (result.IsNatural ? primary.Kind != "recover" || primary.AttemptId is not null
                    : primary.Kind != "treat" || primary.AttemptId is null))
            { AddIssue(issues, path, "spiritual_healing_history_invalid", "one exact primary spiritual cycle row", primary.TransitionId); continue; }
            foreach (var stage in stages)
            {
                var matching = rows.Where(r => r.TransitionId == stage.TransitionId).ToArray();
                if (matching.Length != 1 || matching[0] is not { } row || row.WoundId != stage.After.WoundId ||
                    row.Kind != stage.Kind || row.Turn != stage.After.LastTransition.Turn || row.EventRef != stage.EventRef ||
                    row.OperationKey != stage.OperationKey || row.WoundTransitionOrdinal != stage.After.LastTransition.Ordinal ||
                    row.BeforeFingerprint != WoundIdentityState.ComputeSemanticFingerprint(stage.Before) ||
                    row.AfterFingerprint != WoundIdentityState.ComputeSemanticFingerprint(stage.After) ||
                    row.AttemptId != stage.AttemptId || row.CycleKey != stage.CycleKey || row.Terminal != stage.Terminal ||
                    row.SourceFingerprint != primary.SourceFingerprint || row.PaymentFingerprint is not null ||
                    row.CourseId is not null || row.CourseMilestoneOrdinal is not null ||
                    row.TransitionId != primary.TransitionId && row.TransitionResult is not null)
                    AddIssue(issues, path, "spiritual_healing_history_invalid", "complete exact spiritual stage cohort", stage.TransitionId);
            }
        }
    }
}
