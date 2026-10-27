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
            var catalog = WoundCarrierCatalog.Build(baseline.PreTurnCarriers);
            var identity = WoundIdentityState.Parse(baseline.PreTurnIdentityIndex.ToJsonString(), WoundIdentityState.StatePath);
            var history = WoundHistoryState.Parse(baseline.PreTurnHistory.ToJsonString(), WoundHistoryState.HistoryPath);
            if (catalog.Issues.Count != 0 || !identity.IsValid || !history.IsValid ||
                history.State!.ValidateAgreement(identity.State!, catalog).Count != 0)
                throw new InvalidDataException("Spiritual finalization lost its original canonical agreement.");
            var persisted = SpiritualHealingPersistedResult.Create(owner, executed);
            var published = persisted.Stages;
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
                var primary = stage.Kind == "treat";
                var historyIntent = raw with { TickKey = stage.CycleKey, TransitionResult = primary ? persisted : null };
                intents.AddRange(executed[index].TransitionIntents.Select(i => i is WoundTransitionHistoryIntent ? historyIntent :
                    WoundAcceptedTurnData.CloneTransitionIntent(i)));
                const string summary = "Духовная рана: лечение и естественное восстановление за один безопасный цикл.";
                rows.Add(new(stage.TransitionId, stage.After.WoundId, checked(history.State.NextOrdinal + index),
                    stage.After.LastTransition.Ordinal, stage.Kind, prepared.Binding.Turn, stage.EventRef, stage.OperationKey,
                    raw.BeforeFingerprint, raw.AfterFingerprint, owner.Fingerprint, stage.AttemptId, null, null, stage.CycleKey,
                    null, WoundHistoryState.ComputeOutputFingerprint(stage.OperationKey, stage.EventRef, summary), summary,
                    stage.Terminal, primary ? persisted : null));
            }
            var afterHistory = WoundHistoryState.CreateValidated(checked(history.State.NextOrdinal + rows.Count), history.State.Transitions.Concat(rows));
            if (!afterHistory.IsValid) return new(null, afterHistory.Issues);
            var historyJson = JsonNode.Parse(WoundHistoryState.SerializeCanonical(afterHistory.State!))!.AsObject();
            afterHistory = WoundHistoryState.Parse(historyJson.ToJsonString(), WoundHistoryState.HistoryPath);
            if (!afterHistory.IsValid) return new(null, afterHistory.Issues);
            var identityJson = baseline.PreTurnIdentityIndex;
            var carriers = baseline.PreTurnCarriers;
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
                    ComputeWoundCollectionFingerprint(baseline.PreTurnCarriers, first.Owner), new[] { mutation }));
                else contributions[existing] = new(first.Owner, contributions[existing].ExpectedWoundCollectionFingerprint,
                    contributions[existing].Mutations.Append(mutation).ToArray());
            }
            var finalCatalog = WoundCarrierCatalog.Build(carriers);
            var finalIdentity = WoundIdentityState.Parse(identityJson.ToJsonString(), WoundIdentityState.StatePath);
            if (finalCatalog.Issues.Count != 0 || !finalIdentity.IsValid)
                return new(null, finalCatalog.Issues.Concat(finalIdentity.Issues).ToArray());
            var agreement = afterHistory.State!.ValidateAgreement(finalIdentity.State!, finalCatalog);
            if (agreement.Count != 0) return new(null, agreement);
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
}
