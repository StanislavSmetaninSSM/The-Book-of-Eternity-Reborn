using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnPlannerCore
{
    /// <summary>
    /// Assembles actual sealed treatment/heal execution. Global History order and
    /// per-wound ordinals stay distinct; no executed snapshot is rebased or edited.
    /// </summary>
    private static WoundAcceptedTurnPlanningResult ComposeTerminalTreatmentFinalPlan(
        WoundPreparedAcceptedTurnPlan prepared, WoundEffectBatchAcceptedPlan accepted)
    {
        var boundary = "private_execution";
        try
        {
            if (!WoundAcceptedTurnPlanner.TryReadTreatmentContinuation(prepared.TreatmentContinuationAuthority, out var continuation) ||
                !WoundAcceptedTurnPlanner.TreatmentContinuationPreparedAgrees(prepared) ||
                continuation.OutcomePreparation.ReadTerminalHealIntent() is not { } heal || continuation.LegacyPreparation is null ||
                !EffectAcceptedTurnPlanner.TryReadMortalTreatmentExecution(prepared, accepted.EffectPlan, out var stages) || stages.Count != 2)
                return TerminalTreatmentFailure(boundary, "missing or changed private preparation/execution");
            boundary = "original_baselines";
            var baseline = prepared.BaselineAuthority;
            var catalog = WoundCarrierCatalog.Build(baseline.PreTurnCarriers);
            var identities = WoundIdentityState.Parse(baseline.PreTurnIdentityIndex.ToJsonString(), WoundIdentityState.StatePath);
            var history = WoundHistoryState.Parse(baseline.PreTurnHistory.ToJsonString(), WoundHistoryState.HistoryPath);
            var originals = catalog.Occurrences.Where(row => row.WoundId == continuation.Before.WoundId).ToArray();
            if (catalog.Issues.Count != 0 || !identities.IsValid || !history.IsValid || originals.Length != 1 ||
                history.State!.ValidateAgreement(identities.State!, catalog).Count != 0 ||
                WoundMaterializationContract.SerializeCanonical(originals[0].Wound) != WoundMaterializationContract.SerializeCanonical(continuation.Before))
                return TerminalTreatmentFailure(boundary, "original wound/carrier/identity/History differs");
            boundary = "legacy_allocation";
            var groups = MortalWoundHealLegacyPlanner.Finalize(continuation.LegacyPreparation, prepared, accepted);
            var mechanical = continuation.LegacyPreparation.LegacyDraftBindings.Where(row => row.Kind == "mechanical_effect").ToArray();
            if (!groups.Select(row => row.LegacyId).SequenceEqual(mechanical.Select(row => row.LegacyId), StringComparer.Ordinal))
                return TerminalTreatmentFailure(boundary, "complete ordered independent legacy groups differ");
            boundary = "linked_stages";
            var rows = new List<WoundHistoryTransition>();
            var intents = new List<WoundTransitionIntent>();
            var coordinates = continuation.RequestAuthority.Coordinates;
            for (var index = 0; index < stages.Count; index++)
            {
                var stage = stages[index];
                var expectedBefore = index == 0 ? continuation.Before : stages[index - 1].After;
                var beforeFingerprint = WoundIdentityState.ComputeSemanticFingerprint(stage.Before);
                var afterFingerprint = WoundIdentityState.ComputeSemanticFingerprint(stage.After);
                var transition = index == 0 ? continuation.TransitionId : heal.HealChildCoordinates.TransitionId;
                var operation = index == 0 ? coordinates.OperationKey : heal.HealChildCoordinates.OperationKey;
                var eventRef = index == 0 ? coordinates.EventRef : heal.HealChildCoordinates.EventRef;
                var kind = index == 0 ? "treat" : "heal";
                if (WoundMaterializationContract.SerializeCanonical(stage.Before) != WoundMaterializationContract.SerializeCanonical(expectedBefore) ||
                    stage.After.WoundId != continuation.Before.WoundId || stage.After.Owner != continuation.Before.Owner ||
                    stage.After.LastTransition.TransitionId != transition || stage.After.LastTransition.Kind != kind ||
                    stage.After.LastTransition.Turn != prepared.Binding.Turn ||
                    stage.After.LastTransition.Ordinal != checked(continuation.Before.LastTransition.Ordinal + index + 1) ||
                    stage.OperationKey != operation || stage.EventRef != eventRef ||
                    (index == 0 ? stage.After.Lifecycle != "active" : stage.After.Lifecycle != "healed" || stage.After.Care.State != "healed"))
                    return TerminalTreatmentFailure(boundary, "exact chronological snapshots/coordinates differ");
                var source = stage.Intents.OfType<WoundTransitionHistoryIntent>().Single();
                if (source.TransitionId != transition || source.Kind != kind || source.OperationKey != operation ||
                    source.EventRef != eventRef || source.Turn != prepared.Binding.Turn || source.WoundId != continuation.Before.WoundId ||
                    source.BeforeFingerprint != beforeFingerprint || source.AfterFingerprint != afterFingerprint || source.Terminal != (index == 1) ||
                    source.AttemptId != (index == 0 ? coordinates.AttemptId : null) || source.TickKey is not null)
                    return TerminalTreatmentFailure(boundary, "reducer History intent differs from its executed stage");
                var result = index == 0 ? continuation.PersistedResult : null;
                intents.AddRange(stage.Intents.Select(intent => intent is WoundTransitionHistoryIntent
                    ? source with { TransitionResult = result } : WoundAcceptedTurnData.CloneTransitionIntent(intent)));
                rows.Add(new(transition, continuation.Before.WoundId, checked(history.State!.NextOrdinal + index),
                    stage.After.LastTransition.Ordinal, kind, prepared.Binding.Turn, eventRef, operation,
                    beforeFingerprint, afterFingerprint, continuation.RequestAuthority.RequestFingerprint,
                    index == 0 ? coordinates.AttemptId : null,
                    CourseId: index == 0 ? continuation.Resolution.CourseId : null,
                    CourseMilestoneOrdinal: index == 0 ? continuation.Resolution.CourseMilestoneOrdinal : null,
                    CycleKey: null, PaymentFingerprint: null,
                    WoundHistoryState.ComputeOutputFingerprint(operation, eventRef, WoundAcceptedTurnPlanner.TreatmentPublicationSummary),
                    WoundAcceptedTurnPlanner.TreatmentPublicationSummary, index == 1, result));
            }
            boundary = "complete_history";
            var complete = WoundHistoryState.CreateValidated(checked(history.State!.NextOrdinal + rows.Count), history.State.Transitions.Concat(rows));
            if (!complete.IsValid) return new(null, complete.Issues.Select(CloneIssue).ToArray());
            var historyAfter = JsonNode.Parse(WoundHistoryState.SerializeCanonical(complete.State!))!.AsObject();
            complete = WoundHistoryState.Parse(historyAfter.ToJsonString(), WoundHistoryState.HistoryPath);
            if (!complete.IsValid) return new(null, complete.Issues.Select(CloneIssue).ToArray());
            boundary = "terminal_carrier_identity";
            var final = stages[^1].After;
            // Existing pure terminal assembly helpers issue no recovery owner/tick.
            var identityAfter = BuildRecoveryIdentityAfterImage(baseline.PreTurnIdentityIndex, continuation.Before, final, terminal: true);
            if (!identityAfter.IsValid) return new(null, identityAfter.Issues.Select(CloneIssue).ToArray());
            var identityJson = JsonNode.Parse(WoundIdentityState.SerializeCanonical(identityAfter.State!))!.AsObject();
            var carriersAfter = BuildRecoveryCarrierAfterImage(baseline.PreTurnCarriers, continuation.Before, final, terminal: true);
            var afterCatalog = WoundCarrierCatalog.Build(carriersAfter);
            var agreement = complete.State!.ValidateAgreement(identityAfter.State!, afterCatalog);
            if (afterCatalog.Issues.Count != 0 || agreement.Count != 0)
                return new(null, afterCatalog.Issues.Concat(agreement).Select(CloneIssue).ToArray());
            var contributions = new[] { new WoundCarrierContribution(final.Owner,
                ComputeWoundCollectionFingerprint(baseline.PreTurnCarriers, final.Owner),
                new[] { new WoundCarrierMutation("delete", final.WoundId, continuation.Before, null) }) };
            boundary = "final_fingerprint";
            var fingerprint = WoundAcceptedTurnFingerprints.ComputeFinal(prepared, accepted, contributions, identityJson, historyAfter, intents);
            return new(new WoundAcceptedTurnPlan(prepared.Binding, prepared.BindingFingerprint, prepared.InputFingerprint,
                accepted.WoundPreparationFingerprint, accepted.EffectInputFingerprint, accepted.EffectAcceptedTurnPlanFingerprint,
                fingerprint, prepared.AllocatedWoundIds, prepared.AllocatedTransitionIds, contributions, identityJson, historyAfter, intents),
                Array.Empty<ValidationIssue>());
        }
        catch (Exception failure) when (failure is ArgumentException or InvalidOperationException or InvalidDataException or JsonException or OverflowException or NullReferenceException)
        { return TerminalTreatmentFailure(boundary, failure.Message); }
    }

    /// <summary>
    /// Checks the complete terminal contour against the exact private executed
    /// snapshots before output/publication accepts an otherwise well-formed bundle.
    /// </summary>
    internal static bool TerminalTreatmentFinalPlanAgrees(AcceptedMechanicsWoundStageBundle bundle)
    {
        var rebuilt = ComposeTerminalTreatmentFinalPlan(bundle.PreparedPlan, bundle.EffectBatchPlan);
        if (!rebuilt.Success || rebuilt.Plan is null) return false;
        var actual = bundle.FinalPlan;
        var fingerprint = WoundAcceptedTurnFingerprints.ComputeFinal(bundle.PreparedPlan, bundle.EffectBatchPlan,
            actual.CarrierContributions, actual.IdentityIndexAfterImage, actual.HistoryAfterImage, actual.TransitionIntents);
        return fingerprint == actual.WoundFinalPlanFingerprint && fingerprint == rebuilt.Plan.WoundFinalPlanFingerprint;
    }

    private static WoundAcceptedTurnPlanningResult TerminalTreatmentFailure(string boundary, string actual) =>
        FailedFinal("wound_plan_terminal_treatment_invalid", "Terminal treatment must preserve its original and actual executed chain.",
            "exact treat/heal/legacy History, terminal identity and carrier removal", boundary + ":" + actual);
}
