using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnPlannerCore
{
    private static WoundAcceptedTurnPlanningResult ComposeMortalDiagnosisFinalPlan(
        WoundPreparedAcceptedTurnPlan prepared, WoundEffectBatchAcceptedPlan accepted)
    {
        try
        {
            var owner = prepared.MortalDiagnosisAuthority;
            if (owner is null || !owner.PreparedAgrees(prepared) ||
                !EffectAcceptedTurnPlanner.TryReadMortalDiagnosisExecution(prepared, accepted.EffectPlan, out var execution))
                throw new InvalidDataException("Missing actual diagnosis preparation or sealed execution.");
            owner.RequireCurrent();
            var baseline = prepared.BaselineAuthority;
            var history = WoundHistoryState.Parse(baseline.PreTurnHistory.ToJsonString(), WoundHistoryState.HistoryPath);
            if (!history.IsValid) return new(null, history.Issues);
            var knowledge = execution[0];
            if (knowledge.Kind != "diagnose" || knowledge.OperationKey != owner.Outcome.Request.Coordinates.OperationKey ||
                WoundMaterializationContract.SerializeCanonical(knowledge.Before) != WoundMaterializationContract.SerializeCanonical(owner.Owner.CurrentWound) ||
                WoundMaterializationContract.SerializeCanonical(knowledge.After) != WoundMaterializationContract.SerializeCanonical(owner.Outcome.KnowledgeAfter))
                throw new InvalidDataException("Diagnosis knowledge execution differs from the complete retained procedure result.");
            var grouped = execution.Skip(1).ToLookup(stage => stage.Before.WoundId, StringComparer.Ordinal);
            var published = owner.Working.Members.ToDictionary(member => member.Original.WoundId, member =>
                (IReadOnlyList<MortalWoundRecoveryPublishedStage>)grouped[member.Original.WoundId].Select(stage =>
                    new MortalWoundRecoveryPublishedStage(stage.TransitionId, stage.Kind,
                        WoundIdentityState.ComputeSemanticFingerprint(stage.Before), WoundIdentityState.ComputeSemanticFingerprint(stage.After),
                        stage.Kind == "heal", stage.After.LastTransition.Turn, stage.EventRef, stage.OperationKey, stage.Before, stage.After)).ToArray(), StringComparer.Ordinal);
            var result = MortalWoundDiagnosisPersistedResult.CreateComparisonEvidence(owner.Owner, owner.Outcome,
                owner.Working, owner.Progression, published);
            var rows = new List<WoundHistoryTransition>();
            var intents = new List<WoundTransitionIntent>();
            var memberByWound = owner.Working.Members.ToDictionary(member => member.Original.WoundId, StringComparer.Ordinal);
            for (var index = 0; index < execution.Count; index++)
            {
                var stage = execution[index];
                var after = stage.After;
                var originalIntent = stage.TransitionIntents.OfType<WoundTransitionHistoryIntent>().Single();
                var diagnostic = index == 0;
                var finalized = originalIntent with
                {
                    TickKey = diagnostic ? null : stage.TickKey,
                    TransitionResult = diagnostic ? result : null
                };
                intents.AddRange(stage.TransitionIntents.Select(intent => intent is WoundTransitionHistoryIntent
                    ? finalized : WoundAcceptedTurnData.CloneTransitionIntent(intent)));
                var source = diagnostic ? result.WorkingFingerprint : memberByWound[after.WoundId].Resolution.AuthorityFingerprint;
                var summary = diagnostic ? WoundAcceptedTurnPlanner.DiagnosisPublicationSummary : WoundAcceptedTurnPlanner.RecoveryPublicationSummary;
                rows.Add(new(stage.TransitionId, after.WoundId, checked(history.State!.NextOrdinal + index), after.LastTransition.Ordinal,
                    stage.Kind, after.LastTransition.Turn, stage.EventRef, stage.OperationKey,
                    WoundIdentityState.ComputeSemanticFingerprint(stage.Before), WoundIdentityState.ComputeSemanticFingerprint(after), source,
                    diagnostic ? owner.Outcome.Request.Coordinates.AttemptId : null, null, null, diagnostic ? null : stage.TickKey,
                    null, WoundHistoryState.ComputeOutputFingerprint(stage.OperationKey, stage.EventRef, summary), summary, stage.Kind == "heal",
                    diagnostic ? result : null));
            }
            var complete = WoundHistoryState.CreateValidated(checked(history.State!.NextOrdinal + rows.Count), history.State.Transitions.Concat(rows));
            if (!complete.IsValid) return new(null, complete.Issues);
            var historyJson = JsonNode.Parse(WoundHistoryState.SerializeCanonical(complete.State!))!.AsObject();
            complete = WoundHistoryState.Parse(historyJson.ToJsonString(), WoundHistoryState.HistoryPath);
            if (!complete.IsValid) return new(null, complete.Issues);
            var identities = baseline.PreTurnIdentityIndex.DeepClone().AsObject();
            var carriers = baseline.PreTurnCarriers;
            var afterCarriers = new WoundCarrierCatalogInput(carriers.PlayerWounds?.DeepClone().AsObject(),
                carriers.NpcWounds?.DeepClone().AsObject(), carriers.EnemyCombatants?.DeepClone().AsObject(),
                carriers.AllyCombatants?.DeepClone().AsObject(), carriers.AfterlifeProfiles?.DeepClone().AsObject());
            var mutations = new List<(WoundOwnerCoordinate Owner, WoundCarrierMutation Mutation)>();
            foreach (var member in owner.Working.Members)
            {
                var final = grouped[member.Original.WoundId].LastOrDefault()?.After ?? member.Working;
                if (WoundMaterializationContract.SerializeCanonical(final) == WoundMaterializationContract.SerializeCanonical(member.Original)) continue;
                var terminal = final.Lifecycle == "healed";
                var identity = BuildRecoveryIdentityAfterImage(identities, member.Original, final, terminal);
                if (identity.State is null || identity.Issues.Count != 0) return new(null, identity.Issues);
                identities = JsonNode.Parse(WoundIdentityState.SerializeCanonical(identity.State))!.AsObject();
                var mutation = new WoundCarrierMutation(terminal ? "delete" : "update", final.WoundId, member.Original, terminal ? null : final);
                var collection = WoundCarrierCollectionAuthority.Resolve(afterCarriers, member.Original.Owner);
                var issue = AcceptedMechanicsCarrierAssembler.ApplyMutation(collection, member.Original.Owner, mutation);
                if (issue is not null) return new(null, new[] { issue });
                mutations.Add((member.Original.Owner, mutation));
            }
            var catalog = WoundCarrierCatalog.Build(afterCarriers);
            var parsedIdentities = WoundIdentityState.Parse(identities.ToJsonString(), WoundIdentityState.StatePath);
            if (catalog.Issues.Count != 0 || parsedIdentities.State is null || parsedIdentities.Issues.Count != 0)
                return new(null, catalog.Issues.Concat(parsedIdentities.Issues).ToArray());
            var agreement = complete.State!.ValidateAgreement(parsedIdentities.State, catalog);
            if (agreement.Count != 0) return new(null, agreement);
            var contributions = mutations.GroupBy(value => value.Owner).Select(group => new WoundCarrierContribution(group.Key,
                ComputeWoundCollectionFingerprint(carriers, group.Key), group.Select(value => value.Mutation).ToArray())).ToArray();
            var fingerprint = WoundAcceptedTurnFingerprints.ComputeFinal(prepared, accepted, contributions, identities, historyJson, intents);
            return new(new WoundAcceptedTurnPlan(prepared.Binding, prepared.BindingFingerprint, prepared.InputFingerprint,
                accepted.WoundPreparationFingerprint, accepted.EffectInputFingerprint, accepted.EffectAcceptedTurnPlanFingerprint,
                fingerprint, prepared.AllocatedWoundIds, prepared.AllocatedTransitionIds, contributions, identities, historyJson, intents), Array.Empty<ValidationIssue>());
        }
        catch (Exception failure) when (failure is InvalidDataException or InvalidOperationException or ArgumentException or JsonException or OverflowException)
        {
            return FailedFinal("mortal_diagnosis_finalization_invalid", "Diagnosis cannot finalize a partial knowledge/recovery cohort.",
                "one actual complete diagnosis execution and original History", failure.Message);
        }
    }
}
