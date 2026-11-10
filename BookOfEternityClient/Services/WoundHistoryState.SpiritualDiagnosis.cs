namespace BookOfEternityClient.Services;

internal sealed partial class WoundHistoryState
{
    private static void ValidateSpiritualDiagnosisReplayAgreement(IEnumerable<WoundHistoryTransition> transitions,
        string path, ICollection<ValidationIssue> issues)
    {
        var diagnoses = transitions.Where(r => r.TransitionResult is SpiritualDiagnosisPersistedResult).ToArray();
        if (diagnoses.Length == 0) return;
        var rows = transitions.ToArray();
        // One operation-local index. Historical DATA does not grant a live cycle.
        var byId = rows.ToLookup(r => r.TransitionId, StringComparer.Ordinal);
        var naturalResults = rows.Where(r => r.TransitionResult is SpiritualHealingPersistedResult { IsNatural: true })
            .ToLookup(r => ((SpiritualHealingPersistedResult)r.TransitionResult!).BindingIdentity);
        foreach (var primary in diagnoses)
        {
            try
            {
                var result = (SpiritualDiagnosisPersistedResult)primary.TransitionResult!;
                var stage = result.DiagnosisStage;
                var naturals = naturalResults[result.BindingIdentity].ToArray();
                if (naturals.Length != 1) throw new InvalidDataException("Diagnosis requires exactly one linked natural result in its exact turn.");
                var naturalPrimary = naturals[0];
                var natural = (SpiritualHealingPersistedResult)naturalPrimary.TransitionResult!;
                result.RequireNatural(natural);
                if (byId[stage.TransitionId].Count() != 1 || primary.TransitionId != stage.TransitionId || primary.WoundId != stage.After.WoundId ||
                    primary.Kind != "diagnose" || primary.Turn != stage.After.LastTransition.Turn || primary.EventRef != stage.EventRef ||
                    primary.OperationKey != stage.OperationKey || primary.WoundTransitionOrdinal != stage.After.LastTransition.Ordinal ||
                    primary.BeforeFingerprint != WoundIdentityState.ComputeSemanticFingerprint(stage.Before) ||
                    primary.AfterFingerprint != WoundIdentityState.ComputeSemanticFingerprint(stage.After) ||
                    primary.AttemptId != stage.AttemptId || primary.CycleKey is not null || primary.Terminal ||
                    primary.PaymentFingerprint is not null || primary.CourseId is not null || primary.CourseMilestoneOrdinal is not null ||
                    naturalPrimary.SourceFingerprint != primary.SourceFingerprint || naturalPrimary.Ordinal != primary.Ordinal + 1)
                    throw new InvalidDataException("Diagnosis knowledge row differs from its exact retained result or natural boundary.");
                var ordered = natural.Stages.Select(s => byId[s.TransitionId].Single()).ToArray();
                if (!ordered.Select(r => r.Ordinal).SequenceEqual(Enumerable.Range(primary.Ordinal + 1, ordered.Length)))
                    throw new InvalidDataException("Diagnosis natural suffix was omitted, duplicated, reordered or interleaved.");
            }
            catch (Exception failure) when (failure is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException)
            { AddIssue(issues, path, "spiritual_diagnosis_history_invalid", "exact knowledge row plus one complete natural suffix", failure.Message); }
        }
    }
}
