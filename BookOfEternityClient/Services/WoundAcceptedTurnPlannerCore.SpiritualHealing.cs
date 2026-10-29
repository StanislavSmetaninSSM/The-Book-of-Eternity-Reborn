using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnPlannerCore
{
    private static WoundAcceptedTurnPlanningResult ComposeSpiritualHealingFinalPlan(
        WoundPreparedAcceptedTurnPlan prepared, WoundEffectBatchAcceptedPlan accepted)
    {
        if (prepared.SpiritualHealingContinuationAuthority is not AfterlifeWoundHealingPlanner.Continuation owner ||
            !owner.PreparedAgrees(prepared) || !EffectAcceptedTurnPlanner.TryReadSpiritualHealingExecution(prepared,
                accepted.EffectPlan, out var executed) || executed.Count != owner.Stages.Count)
            return FailedFinal("spiritual_healing_execution_invalid", "Missing exact spiritual execution.", "private ordered execution", "foreign or changed stage");
        try
        {
            var baseline = prepared.BaselineAuthority;
            var reduced = ReduceExecutedSpiritualState(new(baseline.PreTurnCarriers, baseline.PreTurnIdentityIndex,
                baseline.PreTurnHistory, null, null), prepared.Binding.Turn, owner.Fingerprint, executed,
                () => { var result = SpiritualHealingPersistedResult.Create(owner, executed); return (result, result.Stages); },
                owner.IsNatural ? "Духовная рана: естественное восстановление за один безопасный цикл."
                    : owner.HasServicePayment ? $"Духовная рана: оплаченная попытка лечения ({owner.ServiceAmount} Чернильных Перьев) и один безопасный цикл."
                    : "Духовная рана: лечение и естественное восстановление за один безопасный цикл.");
            if (reduced.State is not { } state) return new(null, reduced.Issues);
            var identityJson = state.Identity; var historyJson = state.History;
            var contributions = state.Contributions; var intents = state.Intents;
            var seal = WoundAcceptedTurnFingerprints.ComputeFinal(prepared, accepted, contributions,
                identityJson, historyJson, intents);
            return new(new WoundAcceptedTurnPlan(prepared.Binding, prepared.BindingFingerprint, prepared.InputFingerprint,
                accepted.WoundPreparationFingerprint, accepted.EffectInputFingerprint, accepted.EffectAcceptedTurnPlanFingerprint,
                seal, prepared.AllocatedWoundIds, prepared.AllocatedTransitionIds, contributions, identityJson, historyJson, intents),
                Array.Empty<ValidationIssue>());
        }
        catch (Exception failure) when (failure is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException)
        {
            return FailedFinal("spiritual_healing_finalization_invalid", "Spiritual healing cannot finalize partially.",
                "one complete exact stage cohort", failure.Message);
        }
    }
    internal static WoundStateReductionResult ReduceCombatHealingState(WoundPreparedAcceptedTurnPlan prepared,
        EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundBeforeAuthority before,
        IReadOnlyList<WoundExecutedBoundary> executed)
    {
        if (!before.IsCurrent || prepared.CombatHealingAuthority is not { } selection ||
            !selection.PreparedAgrees(prepared) || !before.MatchesPrepared(prepared) || executed.Count != selection.Stages.Count)
            return FailedReduction("spiritual_combat_healing_execution_invalid", "Missing actual combat draft execution.", "exact current private combat stage chain", "foreign or changed");
        return ReduceExecutedSpiritualState(before.Data, prepared.Binding.Turn, selection.SourceFingerprint, executed,
            () => { var result = SpiritualCombatHealingPersistedResult.Create(selection, executed); return (result, result.Stages); },
            "Духовная рана: лечение в духовном бою без безопасного цикла.");
    }

    private static WoundStateReductionResult ReduceExecutedSpiritualState(WoundOperationBeforeData operationBefore,
        int turn, string sourceFingerprint, IReadOnlyList<WoundExecutedBoundary> executed,
        Func<(WoundTransitionResult Result, IReadOnlyList<SpiritualHealingPublishedStage> Stages)> createResult, string summary)
    {
        try
        {
            var catalog = WoundCarrierCatalog.Build(operationBefore.WoundCarriers!);
            var identity = WoundIdentityState.Parse(operationBefore.WoundIdentity!.ToJsonString(), WoundIdentityState.StatePath);
            var history = WoundHistoryState.Parse(operationBefore.WoundHistory!.ToJsonString(), WoundHistoryState.HistoryPath);
            if (catalog.Issues.Count != 0 || !identity.IsValid || !history.IsValid ||
                history.State!.ValidateAgreement(identity.State!, catalog).Count != 0)
                throw new InvalidDataException("Spiritual finalization lost its original canonical agreement.");
            var (persisted, published) = createResult();
            var rows = new List<WoundHistoryTransition>();
            var intents = new List<WoundTransitionIntent>();
            for (var index = 0; index < published.Count; index++)
            {
                var stage = published[index];
                var raw = executed[index].TransitionIntents.OfType<WoundTransitionHistoryIntent>().Single();
                if (raw.TransitionId != stage.TransitionId || raw.OperationKey != stage.OperationKey ||
                    raw.BeforeFingerprint != WoundIdentityState.ComputeSemanticFingerprint(stage.Before) ||
                    raw.AfterFingerprint != WoundIdentityState.ComputeSemanticFingerprint(stage.After))
                    throw new InvalidDataException("Spiritual stage history differs from its actual reducer.");
                var primary = index == 0;
                var historyIntent = raw with { TickKey = stage.CycleKey, TransitionResult = primary ? persisted : null };
                intents.AddRange(executed[index].TransitionIntents.Select(i => i is WoundTransitionHistoryIntent ? historyIntent :
                    WoundAcceptedTurnData.CloneTransitionIntent(i)));
                rows.Add(new(stage.TransitionId, stage.After.WoundId, checked(history.State.NextOrdinal + index),
                    stage.After.LastTransition.Ordinal, stage.Kind, turn, stage.EventRef, stage.OperationKey,
                    raw.BeforeFingerprint, raw.AfterFingerprint, sourceFingerprint, stage.AttemptId, null, null, stage.CycleKey,
                    null, WoundHistoryState.ComputeOutputFingerprint(stage.OperationKey, stage.EventRef, summary), summary,
                    stage.Terminal, primary ? persisted : null));
            }
            var afterHistory = WoundHistoryState.CreateValidated(checked(history.State.NextOrdinal + rows.Count), history.State.Transitions.Concat(rows));
            if (!afterHistory.IsValid) return new(null, afterHistory.Issues);
            var historyJson = JsonNode.Parse(WoundHistoryState.SerializeCanonical(afterHistory.State!))!.AsObject();
            afterHistory = WoundHistoryState.Parse(historyJson.ToJsonString(), WoundHistoryState.HistoryPath);
            if (!afterHistory.IsValid) return new(null, afterHistory.Issues);
            var identityJson = operationBefore.WoundIdentity!;
            var carriers = operationBefore.WoundCarriers!;
            var contributions = new List<WoundCarrierContribution>();
            foreach (var group in published.GroupBy(s => s.Before.WoundId))
            {
                var first = group.First().Before; var final = group.Last().After; var terminal = final.Lifecycle == "healed";
                var nextIdentity = BuildRecoveryIdentityAfterImage(identityJson, first, final, terminal);
                if (!nextIdentity.IsValid) return new(null, nextIdentity.Issues);
                identityJson = JsonNode.Parse(WoundIdentityState.SerializeCanonical(nextIdentity.State!))!.AsObject();
                carriers = BuildRecoveryCarrierAfterImage(carriers, first, final, terminal);
                var existing = contributions.FindIndex(c => c.Owner == first.Owner);
                var mutation = new WoundCarrierMutation(terminal ? "delete" : "update", first.WoundId, first, terminal ? null : final);
                if (existing < 0) contributions.Add(new(first.Owner,
                    ComputeWoundCollectionFingerprint(operationBefore.WoundCarriers!, first.Owner), new[] { mutation }));
                else contributions[existing] = new(first.Owner, contributions[existing].ExpectedWoundCollectionFingerprint,
                    contributions[existing].Mutations.Append(mutation).ToArray());
            }
            var finalCatalog = WoundCarrierCatalog.Build(carriers);
            var finalIdentity = WoundIdentityState.Parse(identityJson.ToJsonString(), WoundIdentityState.StatePath);
            if (finalCatalog.Issues.Count != 0 || !finalIdentity.IsValid)
                return new(null, finalCatalog.Issues.Concat(finalIdentity.Issues).ToArray());
            var agreement = afterHistory.State!.ValidateAgreement(finalIdentity.State!, finalCatalog);
            if (agreement.Count != 0) return new(null, agreement);
            return new(new WoundStateReduction(carriers, identityJson, historyJson, contributions, intents), Array.Empty<ValidationIssue>());
        }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException)
        {
            return FailedReduction("spiritual_healing_finalization_invalid", "Spiritual healing cannot finalize partially.",
                "one complete exact stage cohort", error.Message);
        }
    }

}
