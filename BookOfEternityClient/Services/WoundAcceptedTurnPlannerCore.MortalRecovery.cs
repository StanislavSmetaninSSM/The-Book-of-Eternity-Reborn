using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnPlannerCore
{
    /// <summary>
    /// Finalizes an authenticated recovery execution into ordinary wound publication contributions and durable history.
    /// </summary>
    /// <param name="prepared">
    /// The exact privately admitted original recovery preparation and complete baselines.
    /// </param>
    /// <param name="accepted">
    /// The sealed effect execution containing every actual ordered recovery boundary.
    /// </param>
    /// <returns>
    /// A detached final wound plan when every stage and baseline agrees, otherwise issues without a partial plan or writes.
    /// </returns>
    private static WoundAcceptedTurnPlanningResult ComposeRecoveryContinuationFinalPlan(
        WoundPreparedAcceptedTurnPlan prepared, WoundEffectBatchAcceptedPlan accepted)
    {
        var boundary = "private_execution";
        try
        {
            if (!WoundAcceptedTurnPlanner.RecoveryContinuationPreparedAgrees(prepared) ||
                !WoundAcceptedTurnPlanner.TryReadRecoveryContinuation(prepared.RecoveryContinuationAuthority, out var continuation) ||
                !EffectAcceptedTurnPlanner.TryReadMortalRecoveryExecution(prepared, accepted.EffectPlan, out var executed) ||
                executed.Count == 0 || executed.Count != continuation.AllocatedTransitionIds.Count)
                return RecoveryFinalFailure(boundary, "missing or changed private preparation/execution");
            boundary = "original_baselines";
            var baseline = prepared.BaselineAuthority;
            var catalog = WoundCarrierCatalog.Build(baseline.PreTurnCarriers);
            var identityBefore = WoundIdentityState.Parse(baseline.PreTurnIdentityIndex.ToJsonString(), WoundIdentityState.StatePath);
            var historyBefore = WoundHistoryState.Parse(baseline.PreTurnHistory.ToJsonString(), WoundHistoryState.HistoryPath);
            var matches = catalog.Occurrences.Where(value => value.WoundId == continuation.Before.WoundId).ToArray();
            if (catalog.Issues.Count != 0 || identityBefore.State is null || historyBefore.State is null ||
                identityBefore.Issues.Count != 0 || historyBefore.Issues.Count != 0 || matches.Length != 1 ||
                historyBefore.State.ValidateAgreement(identityBefore.State, catalog).Count != 0 ||
                WoundMaterializationContract.SerializeCanonical(matches[0].Wound) !=
                    WoundMaterializationContract.SerializeCanonical(continuation.Before))
                return RecoveryFinalFailure(boundary, "original wound, identity, history or carrier differs");
            boundary = "executed_coordinates";
            var stages = executed.Select((value, index) =>
            {
                var before = value.Before;
                var after = value.After;
                if (value.TransitionId != continuation.AllocatedTransitionIds[index] ||
                    value.OperationKey != continuation.OperationKeys[index] || value.EventRef != continuation.EventRef ||
                    after.LastTransition.Turn != prepared.Binding.Turn ||
                    after.LastTransition.Ordinal != checked(continuation.Before.LastTransition.Ordinal + index + 1) ||
                    value.TickKey != after.Recovery.LastTickKey)
                    throw new InvalidDataException("Executed stage coordinates differ from the original private continuation.");
                return new MortalWoundRecoveryPublishedStage(value.TransitionId, value.Kind,
                    WoundIdentityState.ComputeSemanticFingerprint(before), WoundIdentityState.ComputeSemanticFingerprint(after),
                    value.Kind == "heal", after.LastTransition.Turn, value.EventRef, value.OperationKey, before, after);
            }).ToArray();
            boundary = "durable_result";
            var persisted = MortalWoundRecoveryPersistedResult.Create(continuation.AcceptedStateAuthority,
                prepared.Binding, continuation.Resolution, stages);
            var rows = new List<WoundHistoryTransition>();
            var intents = new List<WoundTransitionIntent>();
            for (var index = 0; index < stages.Length; index++)
            {
                var stage = stages[index];
                var original = executed[index].TransitionIntents.OfType<WoundTransitionHistoryIntent>().Single();
                if (original.TransitionId != stage.TransitionId || original.Kind != stage.Kind ||
                    original.OperationKey != stage.OperationKey || original.EventRef != stage.EventRef ||
                    original.BeforeFingerprint != stage.BeforeFingerprint || original.AfterFingerprint != stage.AfterFingerprint ||
                    original.WoundId != continuation.Before.WoundId || original.AttemptId is not null || original.Terminal != stage.Terminal)
                    throw new InvalidDataException("Executed reducer history intent differs from its stage snapshots.");
                var finalized = original with
                {
                    TickKey = executed[index].TickKey,
                    TransitionResult = index == 0 ? persisted : null
                };
                intents.AddRange(executed[index].TransitionIntents.Select(intent =>
                    intent is WoundTransitionHistoryIntent ? finalized : WoundAcceptedTurnData.CloneTransitionIntent(intent)));
                rows.Add(new(stage.TransitionId, continuation.Before.WoundId,
                    checked(historyBefore.State.NextOrdinal + index), stage.AfterWound.LastTransition.Ordinal,
                    stage.Kind, stage.Turn, stage.EventRef, stage.OperationKey, stage.BeforeFingerprint, stage.AfterFingerprint,
                    continuation.Resolution.AuthorityFingerprint, AttemptId: null, CourseId: null, CourseMilestoneOrdinal: null,
                    CycleKey: executed[index].TickKey, PaymentFingerprint: null,
                    WoundHistoryState.ComputeOutputFingerprint(stage.OperationKey, stage.EventRef, WoundAcceptedTurnPlanner.RecoveryPublicationSummary),
                    WoundAcceptedTurnPlanner.RecoveryPublicationSummary, stage.Terminal, index == 0 ? persisted : null));
            }
            boundary = "complete_history";
            var historyAfter = WoundHistoryState.CreateValidated(checked(historyBefore.State.NextOrdinal + rows.Count),
                historyBefore.State.Transitions.Concat(rows));
            if (historyAfter.State is null || historyAfter.Issues.Count != 0)
                return new(null, historyAfter.Issues.Select(CloneIssue).ToArray());
            // Parse the complete chain, rather than a partial primary row, so linked-stage and prefix checks run.
            var historyAfterJson = JsonNode.Parse(WoundHistoryState.SerializeCanonical(historyAfter.State))!.AsObject();
            historyAfter = WoundHistoryState.Parse(historyAfterJson.ToJsonString(), WoundHistoryState.HistoryPath);
            if (historyAfter.State is null || historyAfter.Issues.Count != 0)
                return new(null, historyAfter.Issues.Select(CloneIssue).ToArray());
            boundary = "final_carrier_identity";
            var final = stages[^1].AfterWound;
            var terminal = stages[^1].Terminal;
            var identityAfter = BuildRecoveryIdentityAfterImage(baseline.PreTurnIdentityIndex, continuation.Before, final, terminal);
            if (identityAfter.State is null || identityAfter.Issues.Count != 0)
                return new(null, identityAfter.Issues.Select(CloneIssue).ToArray());
            var identityAfterJson = JsonNode.Parse(WoundIdentityState.SerializeCanonical(identityAfter.State))!.AsObject();
            var contribution = new WoundCarrierContribution(final.Owner,
                ComputeWoundCollectionFingerprint(baseline.PreTurnCarriers, final.Owner),
                new[] { new WoundCarrierMutation(terminal ? "delete" : "update", final.WoundId,
                    continuation.Before, terminal ? null : final) });
            var contributions = new[] { contribution };
            var carriersAfter = BuildRecoveryCarrierAfterImage(baseline.PreTurnCarriers, continuation.Before, final, terminal);
            var afterCatalog = WoundCarrierCatalog.Build(carriersAfter);
            var agreement = historyAfter.State.ValidateAgreement(identityAfter.State, afterCatalog);
            if (afterCatalog.Issues.Count != 0 || agreement.Count != 0)
                return new(null, afterCatalog.Issues.Concat(agreement).Select(CloneIssue).ToArray());
            boundary = "ordinary_final_plan";
            var fingerprint = WoundAcceptedTurnFingerprints.ComputeFinal(prepared, accepted, contributions,
                identityAfterJson, historyAfterJson, intents);
            return new(new WoundAcceptedTurnPlan(prepared.Binding, prepared.BindingFingerprint, prepared.InputFingerprint,
                accepted.WoundPreparationFingerprint, accepted.EffectInputFingerprint, accepted.EffectAcceptedTurnPlanFingerprint,
                fingerprint, prepared.AllocatedWoundIds, prepared.AllocatedTransitionIds, contributions,
                identityAfterJson, historyAfterJson, intents), Array.Empty<ValidationIssue>());
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
            JsonException or NullReferenceException or OverflowException or InvalidDataException)
        {
            return RecoveryFinalFailure(boundary, exception.GetType().Name + ":" + exception.Message);
        }
    }

    /// <summary>
    /// Updates exactly the selected identity, preserving every unrelated identity row.
    /// </summary>
    /// <param name="baseline">
    /// The complete original identity index.
    /// </param>
    /// <param name="before">
    /// The exact active selected wound before the evaluation.
    /// </param>
    /// <param name="after">
    /// The actual final executed wound, including a healed snapshot for a terminal result.
    /// </param>
    /// <param name="terminal">
    /// Whether the final stage is the separate terminal heal.
    /// </param>
    /// <returns>
    /// The strictly parsed identity index with exact final status, transition and semantic seal.
    /// </returns>
    private static WoundIdentityParseResult BuildRecoveryIdentityAfterImage(JsonObject baseline,
        WoundMaterializationEnvelope before, WoundMaterializationEnvelope after, bool terminal)
    {
        var parsed = BuildIdentityAfterImage(baseline, new[] { new FinalizedWoundTransition(before, after) });
        if (parsed.State is null || parsed.Issues.Count != 0 || !terminal)
            return parsed;
        var candidate = JsonNode.Parse(WoundIdentityState.SerializeCanonical(parsed.State))!.AsObject();
        var row = candidate["entries"]!.AsArray().OfType<JsonObject>().Single(value => value["woundId"]!.GetValue<string>() == after.WoundId);
        row["status"] = "healed";
        row["terminalTransitionId"] = after.LastTransition.TransitionId;
        return WoundIdentityState.Parse(candidate.ToJsonString(), WoundIdentityState.StatePath);
    }

    /// <summary>
    /// Applies the single original-to-final carrier change using the ordinary typed mutation protocol.
    /// </summary>
    /// <param name="baseline">
    /// The complete original carrier roots, including unrelated data.
    /// </param>
    /// <param name="before">
    /// The exact original selected wound before-image.
    /// </param>
    /// <param name="after">
    /// The final executed wound retained by durable history.
    /// </param>
    /// <param name="terminal">
    /// Whether to remove the wound from the active collection rather than replace it.
    /// </param>
    /// <returns>
    /// Detached final carrier roots; no canonical file is written.
    /// </returns>
    private static WoundCarrierCatalogInput BuildRecoveryCarrierAfterImage(WoundCarrierCatalogInput baseline,
        WoundMaterializationEnvelope before, WoundMaterializationEnvelope after, bool terminal)
    {
        if (!terminal)
            return ApplyFinalWounds(baseline, new[] { new FinalizedWoundTransition(before, after) });
        var result = new WoundCarrierCatalogInput(baseline.PlayerWounds?.DeepClone().AsObject(),
            baseline.NpcWounds?.DeepClone().AsObject(), baseline.EnemyCombatants?.DeepClone().AsObject(),
            baseline.AllyCombatants?.DeepClone().AsObject(), baseline.AfterlifeProfiles?.DeepClone().AsObject());
        var collection = WoundCarrierCollectionAuthority.Resolve(result, before.Owner);
        var issue = AcceptedMechanicsCarrierAssembler.ApplyMutation(collection, before.Owner,
            new WoundCarrierMutation("delete", before.WoundId, before, null));
        if (issue is not null)
            throw new InvalidDataException(issue.Code + ":" + issue.Actual);
        return result;
    }

    /// <summary>
    /// Reports a closed finalization failure without retaining a partially finalized plan.
    /// </summary>
    /// <param name="boundary">
    /// The semantic finalization boundary that rejected the execution.
    /// </param>
    /// <param name="actual">
    /// The precise rejected state or exception reason.
    /// </param>
    /// <returns>
    /// A failed planning result with no final plan.
    /// </returns>
    private static WoundAcceptedTurnPlanningResult RecoveryFinalFailure(string boundary, string actual) =>
        FailedFinal("wound_plan_recovery_continuation_invalid",
            "Recovery must finalize from its one original source and actual sealed execution.",
            "complete original recovery chain and ordinary publication contributions", boundary + ":" + actual);
}
