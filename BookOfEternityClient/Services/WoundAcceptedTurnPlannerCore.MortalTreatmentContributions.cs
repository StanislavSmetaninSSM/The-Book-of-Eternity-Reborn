using System.Text.Json;

namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnPlannerCore
{
    /// <summary>
    /// Reduces only the actual sealed treatment/heal execution into typed facts.
    /// The joint caller supplies its global insertion ordinal after acquisition;
    /// per-wound ordinals remain those of the immutable executed snapshots.
    /// </summary>
    private static WoundTransitionContributionsResult ReduceTerminalTreatmentContribution(
        WoundPreparedAcceptedTurnPlan prepared, WoundEffectBatchAcceptedPlan accepted,
        WoundAcceptedTurnPlanner.TerminalTreatmentView continuation, int historyOrdinal)
    {
        var boundary = "private_execution";
        try
        {
            if (!EffectAcceptedTurnPlanner.TryReadMortalTreatmentExecution(prepared, accepted.EffectPlan, out var stages) ||
                stages.Count != 2 || continuation.Outcome.ReadTerminalHealIntent() is not { } heal)
                return new(null, TerminalTreatmentFailure(boundary, "missing or changed private execution").Issues);
            boundary = "legacy_allocation";
            var groups = MortalWoundHealLegacyPlanner.Finalize(continuation.Legacies, prepared, accepted);
            var mechanical = continuation.Legacies.LegacyDraftBindings.Where(row => row.Kind == "mechanical_effect").ToArray();
            if (!groups.Select(row => row.LegacyId).SequenceEqual(mechanical.Select(row => row.LegacyId), StringComparer.Ordinal))
                return new(null, TerminalTreatmentFailure(boundary, "complete ordered independent legacy groups differ").Issues);
            boundary = "linked_stages";
            var rows = new List<WoundHistoryTransition>();
            var intents = new List<WoundTransitionIntent>();
            var coordinates = continuation.Request.Coordinates;
            for (var index = 0; index < stages.Count; index++)
            {
                var stage = stages[index];
                var expectedBefore = index == 0 ? continuation.Outcome.Before : stages[index - 1].After;
                var beforeFingerprint = WoundIdentityState.ComputeSemanticFingerprint(stage.Before);
                var afterFingerprint = WoundIdentityState.ComputeSemanticFingerprint(stage.After);
                var transition = index == 0 ? continuation.Outcome.TransitionId : heal.HealChildCoordinates.TransitionId;
                var operation = index == 0 ? coordinates.OperationKey : heal.HealChildCoordinates.OperationKey;
                var eventRef = index == 0 ? coordinates.EventRef : heal.HealChildCoordinates.EventRef;
                var kind = index == 0 ? "treat" : "heal";
                if (WoundMaterializationContract.SerializeCanonical(stage.Before) != WoundMaterializationContract.SerializeCanonical(expectedBefore) ||
                    stage.After.WoundId != continuation.Outcome.Before.WoundId || stage.After.Owner != continuation.Outcome.Before.Owner ||
                    stage.After.LastTransition.TransitionId != transition || stage.After.LastTransition.Kind != kind ||
                    stage.After.LastTransition.Turn != prepared.Binding.Turn ||
                    stage.After.LastTransition.Ordinal != checked(continuation.Outcome.Before.LastTransition.Ordinal + index + 1) ||
                    stage.OperationKey != operation || stage.EventRef != eventRef ||
                    (index == 0 ? stage.After.Lifecycle != "active" : stage.After.Lifecycle != "healed" || stage.After.Care.State != "healed"))
                    return new(null, TerminalTreatmentFailure(boundary, "exact chronological snapshots/coordinates differ").Issues);
                var source = stage.Intents.OfType<WoundTransitionHistoryIntent>().Single();
                if (source.TransitionId != transition || source.Kind != kind || source.OperationKey != operation ||
                    source.EventRef != eventRef || source.Turn != prepared.Binding.Turn || source.WoundId != continuation.Outcome.Before.WoundId ||
                    source.BeforeFingerprint != beforeFingerprint || source.AfterFingerprint != afterFingerprint || source.Terminal != (index == 1) ||
                    source.AttemptId != (index == 0 ? coordinates.AttemptId : null) || source.TickKey is not null)
                    return new(null, TerminalTreatmentFailure(boundary, "reducer History intent differs from its executed stage").Issues);
                var result = index == 0 ? continuation.PersistedResult : null;
                intents.AddRange(stage.Intents.Select(intent => intent is WoundTransitionHistoryIntent
                    ? source with { TransitionResult = result } : WoundAcceptedTurnData.CloneTransitionIntent(intent)));
                rows.Add(new(transition, continuation.Outcome.Before.WoundId, checked(historyOrdinal + index),
                    stage.After.LastTransition.Ordinal, kind, prepared.Binding.Turn, eventRef, operation,
                    beforeFingerprint, afterFingerprint, continuation.Request.RequestFingerprint,
                    index == 0 ? coordinates.AttemptId : null,
                    CourseId: index == 0 ? continuation.Resolution.CourseId : null,
                    CourseMilestoneOrdinal: index == 0 ? continuation.Resolution.CourseMilestoneOrdinal : null,
                    CycleKey: null, PaymentFingerprint: null,
                    WoundHistoryState.ComputeOutputFingerprint(operation, eventRef, WoundAcceptedTurnPlanner.TreatmentPublicationSummary),
                    WoundAcceptedTurnPlanner.TreatmentPublicationSummary, index == 1, result));
            }
            return new(new WoundTransitionContributions(
                new[] { new FinalizedWoundTransition(continuation.Outcome.Before, stages[^1].After) },
                intents, rows), Array.Empty<ValidationIssue>());
        }
        catch (Exception failure) when (failure is ArgumentException or InvalidOperationException or InvalidDataException or JsonException or OverflowException or NullReferenceException)
        { return new(null, TerminalTreatmentFailure(boundary, failure.Message).Issues); }
    }
}
