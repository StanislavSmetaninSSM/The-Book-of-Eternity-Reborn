namespace BookOfEternityClient.Services;

internal sealed partial class WoundHistoryState
{
    private readonly record struct DiagnosisEpoch(string WoundId, string Kind, string? Condition, long Minute, string Transition);

    private static void ValidateMortalDiagnosisReplayAgreement(IEnumerable<WoundHistoryTransition> transitions,
        string path, ICollection<ValidationIssue> issues)
    {
        var rows = transitions.ToArray();
        if (!rows.Any(value => value.TransitionResult is MortalWoundDiagnosisPersistedResult)) return;
        // One operation-local, duplicate-preserving index. No historical DATA
        // can rebind a live owner or grant publication/cleanup permission.
        var byId = rows.ToLookup(value => value.TransitionId, StringComparer.Ordinal);
        var lastByWound = new Dictionary<string, WoundHistoryTransition>(StringComparer.Ordinal);
        var consumed = new Dictionary<DiagnosisEpoch, long>();
        var ticks = rows.Where(value => value.TransitionResult is MortalWoundRecoveryPersistedResult)
            .Select(value => ((MortalWoundRecoveryPersistedResult)value.TransitionResult!).Resolution.TickKey).ToHashSet(StringComparer.Ordinal);
        for (var index = 0; index < rows.Length; index++)
        {
            var primary = rows[index];
            if (primary.TransitionResult is MortalWoundDiagnosisPersistedResult diagnosis)
            {
                try
                {
                    var original = diagnosis.Original; var knowledge = diagnosis.KnowledgeAfter;
                    var coordinates = diagnosis.Request.Coordinates;
                    var prefix = new WoundHistoryState(index + 1, rows.Take(index));
                    // The original hash domain covers the complete prefix; it
                    // cannot be replaced by a last-row or selected-wound digest.
                    var historySeal = WoundAcceptedTurnFingerprintWriter.Compute(new[] { "mortal_wound_treatment_history", "1", SerializeCanonical(prefix) });
                    if (diagnosis.Source.HistoryNextOrdinal != index + 1 || diagnosis.Source.HistoryFingerprint != historySeal ||
                        primary.TransitionId != knowledge.LastTransition.TransitionId || primary.Kind != "diagnose" ||
                        primary.WoundId != original.WoundId || primary.OperationKey != coordinates.OperationKey ||
                        primary.AttemptId != coordinates.AttemptId || primary.EventRef != coordinates.EventRef || primary.Turn != coordinates.Turn ||
                        primary.BeforeFingerprint != WoundIdentityState.ComputeSemanticFingerprint(original) ||
                        primary.AfterFingerprint != WoundIdentityState.ComputeSemanticFingerprint(knowledge) ||
                        primary.WoundTransitionOrdinal != knowledge.LastTransition.Ordinal || primary.SourceFingerprint != diagnosis.WorkingFingerprint ||
                        primary.CycleKey is not null || primary.PaymentFingerprint is not null || primary.CourseId is not null ||
                        primary.CourseMilestoneOrdinal is not null || primary.Terminal)
                        throw new InvalidDataException("Diagnosis knowledge row differs from its original prefix, request or complete retained result.");
                    var offset = 1;
                    foreach (var member in diagnosis.Recoveries)
                    {
                        if (!lastByWound.TryGetValue(member.Original.WoundId, out var prior) ||
                            prior.TransitionId != member.Original.LastTransition.TransitionId ||
                            prior.AfterFingerprint != WoundIdentityState.ComputeSemanticFingerprint(member.Original))
                            throw new InvalidDataException("Diagnosis original cohort does not match complete prior History.");
                        if (member.Original.Recovery.RecoveryAnchor is not { } epoch || byId[epoch.AnchorTransitionId].Count() != 1 ||
                            byId[epoch.AnchorTransitionId].Single() is not { } allocation || allocation.Ordinal >= primary.Ordinal ||
                            allocation.WoundId != member.Original.WoundId || !IsRecoveryEpochAllocation(allocation, epoch))
                            throw new InvalidDataException("Diagnosis recovery epoch was not allocated by its exact original prefix.");
                        var recoveryKey = new DiagnosisEpoch(member.Original.WoundId, epoch.AnchorKind, null, epoch.AnchorMinute, epoch.AnchorTransitionId);
                        if (member.ConsumedRecovery != consumed.GetValueOrDefault(recoveryKey))
                            throw new InvalidDataException("Diagnosis repeated or omitted recovery cadence consumption.");
                        if (member.Original.Recovery.DeteriorationAnchor is { } condition)
                        {
                            var key = new DiagnosisEpoch(member.Original.WoundId, "condition", condition.ConditionKey, condition.AnchorMinute, condition.AnchorTransitionId);
                            if (byId[condition.AnchorTransitionId].Count() != 1 || byId[condition.AnchorTransitionId].Single() is not { } allocationRow ||
                                allocationRow.Ordinal >= primary.Ordinal || allocationRow.WoundId != member.Original.WoundId || allocationRow.Kind is not ("create" or "worsen") ||
                                member.ConsumedDeterioration != consumed.GetValueOrDefault(key))
                                throw new InvalidDataException("Diagnosis condition epoch/consumption differs from its exact original prefix.");
                        }
                        else if (member.ConsumedDeterioration != 0) throw new InvalidDataException("Diagnosis has orphan deterioration consumption.");
                        if (member.Stages.Length != 0 && !ticks.Add(member.Resolution.TickKey)) throw new InvalidDataException("Diagnosis repeated an accepted ordinary recovery tick.");
                        foreach (var stage in member.Stages)
                        {
                            var linked = index + offset++;
                            if (linked >= rows.Length || !MatchesRecoveryStage(rows[linked], stage, member.Resolution.AuthorityFingerprint) ||
                                rows[linked].Ordinal != primary.Ordinal + offset - 1 || rows[linked].AttemptId is not null ||
                                rows[linked].CourseId is not null || rows[linked].CourseMilestoneOrdinal is not null ||
                                rows[linked].PaymentFingerprint is not null || rows[linked].TransitionResult is not null)
                                throw new InvalidDataException("Diagnosis ordinary recovery stages were omitted, changed, reordered or interleaved.");
                        }
                    }
                }
                catch (Exception failure) when (failure is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException)
                { AddIssue(issues, path, "mortal_wound_diagnosis_history_invalid", "one exact knowledge row and complete ordered ordinary recovery group", failure.Message); }
            }
            AccumulateDiagnosisEpochs(primary, consumed);
            lastByWound[primary.WoundId] = primary;
        }
    }

    private static void AccumulateDiagnosisEpochs(WoundHistoryTransition row, IDictionary<DiagnosisEpoch, long> consumed)
    {
        if (row.TransitionResult is MortalWoundRecoveryPersistedResult ordinary)
        {
            var saved = ordinary.RecoveryConsumption;
            consumed[new(row.WoundId, saved.EpochKind, saved.ConditionKey, saved.AnchorMinute, saved.AnchorTransitionId)] = saved.ConsumedAfter;
            if (ordinary.DeteriorationConsumption is { } condition)
                consumed[new(row.WoundId, "condition", condition.ConditionKey, condition.AnchorMinute, condition.AnchorTransitionId)] = condition.ConsumedAfter;
        }
        else if (row.TransitionResult is MortalWoundDiagnosisPersistedResult diagnosis)
        {
            foreach (var member in diagnosis.Recoveries)
            {
                if (member.Original.Recovery.RecoveryAnchor is { } epoch)
                    consumed[new(member.Original.WoundId, epoch.AnchorKind, null, epoch.AnchorMinute, epoch.AnchorTransitionId)] =
                        checked(member.ConsumedRecovery + (member.Resolution.TransitionIntents.OfType<MortalWoundRecoveryProgressIntent>().Any() ? member.Resolution.ElapsedCadences : 0));
                if (member.Original.Recovery.DeteriorationAnchor is { } condition)
                    consumed[new(member.Original.WoundId, "condition", condition.ConditionKey, condition.AnchorMinute, condition.AnchorTransitionId)] =
                        checked(member.ConsumedDeterioration + member.Resolution.ElapsedDeteriorationCadences);
            }
        }
    }
}
