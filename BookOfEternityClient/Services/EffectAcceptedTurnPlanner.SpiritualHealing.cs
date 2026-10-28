using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class EffectAcceptedTurnPlanner
{
    private sealed class SpiritualHealingExecutionProof
    {
        private readonly AfterlifeWoundHealingPlanner.Continuation _owner;
        private readonly string _preparation;
        private readonly WoundExecutedBoundary[] _stages;
        private string? _payload;
        internal SpiritualHealingExecutionProof(AfterlifeWoundHealingPlanner.Continuation owner,
            WoundPreparedAcceptedTurnPlan prepared, IReadOnlyList<WoundExecutedBoundary> stages)
        {
            _owner = owner; _preparation = prepared.WoundPreparationFingerprint;
            _stages = stages.Select(s => s.DetachedCopy()).ToArray();
            Fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[]
            {
                "book_of_eternity.spiritual_healing.effect_execution", "1", owner.Fingerprint, _preparation,
                JsonSerializer.Serialize(_stages.Select(s => new
                {
                    Before = WoundMaterializationContract.SerializeCanonical(s.Before),
                    After = WoundMaterializationContract.SerializeCanonical(s.After), s.OperationKey, s.EventRef, s.TickKey,
                    s.Applications, s.Terminations
                }))
            });
        }
        internal string Fingerprint { get; }
        internal void Seal(EffectAcceptedTurnPlan plan)
        {
            if (_payload is not null || !ReferenceEquals(plan.SpiritualHealingExecutionAuthority, this))
                throw new InvalidDataException("Only the actual spiritual executor may seal its effect result once.");
            _payload = WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(plan);
        }
        internal bool Agrees(WoundPreparedAcceptedTurnPlan prepared, EffectAcceptedTurnPlan plan) =>
            ReferenceEquals(prepared.SpiritualHealingContinuationAuthority, _owner) && _owner.PreparedAgrees(prepared) &&
            prepared.WoundPreparationFingerprint == _preparation && ReferenceEquals(plan.SpiritualHealingExecutionAuthority, this) &&
            _payload is not null && _payload == WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(plan);
        internal IReadOnlyList<WoundExecutedBoundary> Read() => _stages.Select(s => s.DetachedCopy()).ToArray();
    }

    internal static string? GetSpiritualHealingExecutionFingerprint(EffectAcceptedTurnPlan plan) =>
        (plan.SpiritualHealingExecutionAuthority as SpiritualHealingExecutionProof)?.Fingerprint;

    internal static bool TryReadSpiritualHealingExecution(WoundPreparedAcceptedTurnPlan prepared,
        EffectAcceptedTurnPlan plan, out IReadOnlyList<WoundExecutedBoundary> stages)
    {
        stages = Array.Empty<WoundExecutedBoundary>();
        if (plan.SpiritualHealingExecutionAuthority is not SpiritualHealingExecutionProof proof || !proof.Agrees(prepared, plan))
            return false;
        stages = proof.Read();
        return stages.Count != 0;
    }

    private static EffectAcceptedTurnPlanningResult BuildSpiritualHealing(EffectAcceptedTurnInput input,
        WoundPreparedAcceptedTurnPlan prepared, EffectIdentityFactory identityFactory)
    {
        try
        {
            if (prepared.SpiritualHealingContinuationAuthority is not AfterlifeWoundHealingPlanner.Continuation owner ||
                !owner.PreparedAgrees(prepared) || !WoundAcceptedTurnPlannerCore.EffectInputAgreesWithPrepared(prepared, input) ||
                !owner.EffectInputAgrees(prepared, input))
                return Failed(new List<ValidationIssue> { SpiritualHealingIssue("Foreign or changed spiritual effect preparation.") });
            owner.RequireExecutionCurrent();
            return BuildCore(input, WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, input), identityFactory,
                Array.Empty<WoundApplicationRequest>(), Array.Empty<WoundTerminalRequest>(), spiritualHealingPrepared: prepared);
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException &&
            failure is (InvalidDataException or InvalidOperationException or ArgumentException or JsonException or OverflowException))
        {
            return Failed(new List<ValidationIssue> { SpiritualHealingIssue(failure.Message) });
        }
    }

    private static SpiritualHealingExecutionProof? ExecuteSpiritualHealing(EffectAcceptedTurnInput input,
        WoundPreparedAcceptedTurnPlan prepared, CarrierWorkspace workspace, EffectIdentityHistoryOwner identityRoot,
        EffectIdentityFactory identityFactory, int turn, List<string> effectIds, List<string> transitionIds,
        List<JsonObject> activeEffects, HashSet<string> processedEvents, List<EffectSourceAuthorityEntry> usedSources,
        List<EffectTargetKey> usedTargets, List<WoundApplicationRootEffectBinding> rootBindings,
        List<ValidationIssue> issues, out EffectSourceAuthority finalSources)
    {
        finalSources = input.SourceAuthority;
        if (prepared.SpiritualHealingContinuationAuthority is not AfterlifeWoundHealingPlanner.Continuation owner || !owner.PreparedAgrees(prepared))
        { issues.Add(SpiritualHealingIssue("Missing actual spiritual continuation.")); return null; }
        var current = owner.Wounds.ToDictionary(w => w.WoundId, StringComparer.Ordinal);
        var executed = new List<WoundExecutedBoundary>();
        var stages = owner.Stages;
        for (var index = 0; index < stages.Count; index++)
        {
            var stage = stages[index];
            var before = current[stage.WoundId];
            var graph = owner.ResolveGraph(before, stage);
            var changed = stage.SeverityRank != before.Severity.Rank;
            var facts = ExecuteWoundBoundaryEffects(input, before, graph, stage.SeverityRank,
                new(stage.TickKey, index, owner.EventRef, stage.OperationKey, owner.Fingerprint, stage.Kind,
                    owner.IsNatural ? "Естественное восстановление" : "Духовное исцеление", prepared.Binding.AcceptedEvents, false), changed, stage.Kind == "heal",
                workspace, identityRoot, identityFactory, turn, effectIds, transitionIds, activeEffects,
                processedEvents, usedSources, usedTargets, rootBindings, issues);
            if (facts is null || issues.Count != 0) return null;
            var after = before with
            {
                Severity = changed ? before.Severity with { Rank = stage.SeverityRank,
                    Value = new[] { "", "I", "II", "III", "IV" }[stage.SeverityRank], LastChangeEventRef = owner.EventRef } : before.Severity,
                Care = stage.Kind == "treat" ? before.Care with { LastAttemptId = stage.AttemptId } : before.Care,
                Recovery = stage.Kind == "heal" ? before.Recovery : before.Recovery with
                {
                    CurrentStepProgress = stage.Progress, CurrentStepThreshold = 2 * stage.SeverityRank,
                    LastTickKey = stage.Kind == "recover" ? stage.TickKey : before.Recovery.LastTickKey
                },
                Complications = facts.Complications, Consequences = facts.Consequences,
                LastTransition = new(stage.TransitionId, checked(before.LastTransition.Ordinal + 1), turn, stage.Kind)
            };
            if (stage.Kind == "heal") after = after with
            { Lifecycle = "healed", Care = before.Care with { State = "healed", ActiveCourseId = null } };
            var beforeSeal = WoundIdentityState.ComputeSemanticFingerprint(before);
            var afterSeal = WoundIdentityState.ComputeSemanticFingerprint(after);
            var outcome = new WoundDeclaredTransitionOutcome(after.Severity.Rank, after.Care.State,
                after.Recovery.CurrentStepProgress, stage.PreparesHeal, stage.Kind == "treat", false,
                after.Complications.Select(c => c.ComplicationId).ToArray(),
                after.Consequences.OwnedEffectSources.RootBindings.Select(r => r.EffectId).Order(StringComparer.Ordinal).ToArray(),
                after.Recovery.Blockers, after.Treatment.CompletedRouteIds);
            WoundTransitionEvidence evidence = stage.Kind switch
            {
                "treat" => new WoundTreatmentEvidence(owner.Fingerprint, beforeSeal, afterSeal, "spiritual_healing", stage.AttemptId!, outcome),
                "recover" => new WoundRecoveryEvidence(owner.Fingerprint, beforeSeal, afterSeal, stage.TickKey,
                    "safe_afterlife_cycle", outcome, SpiritualContinuationAuthority: owner.CreateRecoveryConsumptionProof(index, before, after)),
                "heal" => new WoundHealingEvidence(owner.Fingerprint, beforeSeal, afterSeal, stage.OperationKey, null, Array.Empty<WoundLegacyDeclaration>()),
                _ => throw new InvalidDataException("Unknown spiritual stage.")
            };
            var reduction = WoundTransitionReducer.Reduce(new(stage.Kind, stage.TransitionId, stage.OperationKey,
                owner.EventRef, turn, before, after, evidence));
            issues.AddRange(reduction.Issues);
            if (!reduction.IsValid) return null;
            var actual = new WoundExecutedBoundary(before, reduction.ProposedAfter!, stage.OperationKey,
                owner.EventRef, stage.Kind == "treat" ? before.Recovery.LastTickKey ?? stage.TickKey : stage.TickKey,
                facts.Applications, facts.Terminations, reduction.Intents);
            owner.AcceptExecuted(actual); executed.Add(actual); current[stage.WoundId] = actual.After;
        }
        var roots = owner.SourceRoots.ToDictionary(p => p.Key, p => p.Value?.DeepClone(), StringComparer.Ordinal);
        foreach (var wound in current.Values)
        {
            if (roots[wound.Owner.CarrierPath] is not JsonObject carrier ||
                !WoundCarrierCollectionAuthority.TryResolve(carrier, wound.Owner, out var collection, out _))
                throw new InvalidDataException("Spiritual final wound carrier is absent.");
            var match = collection.Select((n, i) => (n, i)).Single(p => p.n?["woundId"]?.GetValue<string>() == wound.WoundId);
            if (wound.Lifecycle == "active") collection[match.i] = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(wound));
            else collection.RemoveAt(match.i);
        }
        finalSources = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(roots);
        issues.AddRange(finalSources.Issues);
        if (issues.Count != 0) return null;
        var changedGroups = current.Values.Select(w => new EffectIdentitySourceGroup(w.Owner.Realm, "wound", w.WoundId)).ToHashSet();
        usedSources.RemoveAll(s => changedGroups.Contains(new(s.Key.Realm, s.Key.Kind, s.Key.SourceId)));
        foreach (var wound in current.Values.Where(w => w.Lifecycle == "active"))
            foreach (var definition in wound.Consequences.OwnedEffectSources.Definitions)
            {
                var binding = finalSources.ResolveCanonicalBinding(new(wound.Owner.Realm, "wound", wound.WoundId,
                    definition.GetProperty("definitionKey").GetString()!), WoundMaterializationContract.ResolveEffectTargetKind(wound.Owner.OwnerKind));
                if (!binding.Success) throw new InvalidDataException("Spiritual final source did not resolve.");
                usedSources.Add(binding.Source!);
            }
        rootBindings.Clear();
        return new(owner, prepared, executed);
    }

    private static ValidationIssue SpiritualHealingIssue(string message) => new(AcceptedMechanicsPlan.WoundCommandPath,
        IssueSeverity.Error, message, code: "spiritual_healing_execution_invalid", section: "wound_materialization");
}
