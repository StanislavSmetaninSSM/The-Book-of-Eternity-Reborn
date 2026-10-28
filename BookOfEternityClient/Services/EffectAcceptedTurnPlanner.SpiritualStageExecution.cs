using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class EffectAcceptedTurnPlanner
{
    // Lexical domain coordinates only. Actual safe/combat owners call this
    // private shared boundary executor with their own authenticated source.
    private sealed record SpiritualStageExecutionContext(string EventRef, string Fingerprint,
        string Summary, string TreatmentKind, IReadOnlyList<WoundAcceptedEventAuthority> AcceptedEvents,
        Func<int, WoundMaterializationEnvelope, WoundMaterializationEnvelope, object>? RecoveryProof);

    private static WoundExecutedBoundary? ExecuteSpiritualWoundStage(EffectAcceptedTurnInput input,
        WoundMaterializationEnvelope before, WoundWorkingOwnedGraph graph, SpiritualWoundStage stage, int index,
        SpiritualStageExecutionContext domain, CarrierWorkspace workspace, EffectIdentityHistoryOwner identityRoot,
        EffectIdentityFactory identityFactory, int turn, List<string> effectIds, List<string> transitionIds,
        List<JsonObject> activeEffects, HashSet<string> processedEvents, List<EffectSourceAuthorityEntry> usedSources,
        List<EffectTargetKey> usedTargets, List<WoundApplicationRootEffectBinding> rootBindings, List<ValidationIssue> issues)
    {
        if (domain.RecoveryProof is null && stage.Kind is not ("treat" or "heal"))
            throw new InvalidDataException("Combat healing cannot execute a natural stage.");
        var changed = stage.SeverityRank != before.Severity.Rank;
        var facts = ExecuteWoundBoundaryEffects(input, before, graph, stage.SeverityRank,
            new(stage.TickKey, index, domain.EventRef, stage.OperationKey, domain.Fingerprint, stage.Kind,
                domain.Summary, domain.AcceptedEvents, false), changed, stage.Kind == "heal",
            workspace, identityRoot, identityFactory, turn, effectIds, transitionIds, activeEffects,
            processedEvents, usedSources, usedTargets, rootBindings, issues);
        if (facts is null || issues.Count != 0) return null;
        var after = before with
        {
            Severity = changed ? before.Severity with { Rank = stage.SeverityRank,
                Value = new[] { "", "I", "II", "III", "IV" }[stage.SeverityRank], LastChangeEventRef = domain.EventRef } : before.Severity,
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
            "treat" => new WoundTreatmentEvidence(domain.Fingerprint, beforeSeal, afterSeal, domain.TreatmentKind, stage.AttemptId!, outcome),
            "recover" => new WoundRecoveryEvidence(domain.Fingerprint, beforeSeal, afterSeal, stage.TickKey,
                "safe_afterlife_cycle", outcome, SpiritualContinuationAuthority: domain.RecoveryProof?.Invoke(index, before, after) ?? throw new InvalidDataException("Combat cannot consume a natural recovery tick.")),
            "heal" => new WoundHealingEvidence(domain.Fingerprint, beforeSeal, afterSeal, stage.OperationKey, null, Array.Empty<WoundLegacyDeclaration>()),
            _ => throw new InvalidDataException("Unknown spiritual stage.")
        };
        var reduction = WoundTransitionReducer.Reduce(new(stage.Kind, stage.TransitionId, stage.OperationKey,
            domain.EventRef, turn, before, after, evidence));
        issues.AddRange(reduction.Issues);
        if (!reduction.IsValid) return null;
        var actual = new WoundExecutedBoundary(before, reduction.ProposedAfter!, stage.OperationKey,
            domain.EventRef, stage.Kind == "treat" ? before.Recovery.LastTickKey ?? stage.TickKey : stage.TickKey,
            facts.Applications, facts.Terminations, reduction.Intents);
        if (domain.RecoveryProof is null && actual.After.Recovery.LastTickKey != before.Recovery.LastTickKey)
            throw new InvalidDataException("Combat healing cannot change the last natural tick.");
        return actual;
    }
}
