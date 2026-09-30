using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class AcceptedMechanicsCarrierAssembler
{
    /// <summary>
    /// Validates final common-boundary membership against the private ordered recovery
    /// execution, retaining retired generations as terminal history and checking surviving
    /// creations with the same progression constraints as ordinary wound applications.
    /// </summary>
    /// <param name="finalPlan">
    /// Completed common effect plan descended from the retained initial effect plan.
    /// </param>
    /// <param name="bundle">
    /// Original sealed preparation, effect execution and wound result bundle.
    /// </param>
    /// <param name="stagedCatalog">
    /// Validated runtime carrier catalog after the ordered recovery execution.
    /// </param>
    /// <param name="runtimeCatalog">
    /// Validated runtime carrier catalog after common-boundary completion.
    /// </param>
    /// <param name="publicationCatalog">
    /// Validated final publication carrier catalog.
    /// </param>
    /// <param name="stagedIdentities">
    /// Validated identity state retained by the original private execution.
    /// </param>
    /// <param name="finalIdentities">
    /// Validated final common-boundary identity state.
    /// </param>
    /// <returns>
    /// Null when every actual creation and retirement agrees; otherwise a publication failure.
    /// </returns>
    private static ValidationIssue? ValidateMortalRecoveryEffectMembership(
        EffectAcceptedTurnPlan finalPlan, AcceptedMechanicsWoundStageBundle bundle,
        EffectCarrierCatalog stagedCatalog, EffectCarrierCatalog runtimeCatalog,
        EffectCarrierCatalog publicationCatalog, EffectIdentityState stagedIdentities,
        EffectIdentityState finalIdentities)
    {
        var stagedPlan = bundle.EffectBatchPlan.EffectPlan;
        var prepared = bundle.PreparedPlan;
        if (!EffectAcceptedTurnPlanner.TryReadMortalRecoveryExecution(prepared, stagedPlan, out var stages) ||
            stages.Count == 0 || !finalPlan.IsAcceptedBoundaryComplete ||
            finalPlan.AcceptedBoundaryBasePlanFingerprint !=
                WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(stagedPlan) ||
            finalPlan.AcceptedBoundaryFinalPlanFingerprint !=
                WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(finalPlan))
            return RecoveryMembershipFailure("missing private execution or changed common completion ancestry");

        var applications = stages.SelectMany(stage => stage.Applications).ToArray();
        var terminations = stages.SelectMany(stage => stage.Terminations).ToArray();
        var expectedFingerprint = WoundAcceptedTurnFingerprints.ComputeEffectPlan(prepared,
            bundle.EffectBatchPlan.EffectInput, stagedPlan, applications, terminations);
        if (expectedFingerprint != bundle.EffectBatchPlan.EffectAcceptedTurnPlanFingerprint ||
            expectedFingerprint != WoundAcceptedTurnFingerprints.ComputeEffectPlan(prepared,
                bundle.EffectBatchPlan.EffectInput, stagedPlan,
                bundle.EffectBatchPlan.ApplicationResults, bundle.EffectBatchPlan.TerminationResults))
            return RecoveryMembershipFailure("flattened effect results differ from their private ordered execution");

        var terminalStages = stages.SelectMany((stage, index) =>
            stage.Terminations.Select(result => (Result: result, Index: index))).ToArray();
        if (applications.Select(result => result.EffectId).Distinct(StringComparer.Ordinal).Count() != applications.Length ||
            applications.Select(result => result.ApplicationRef).Distinct(StringComparer.Ordinal).Count() != applications.Length ||
            terminalStages.Select(row => row.Result.EffectId).Distinct(StringComparer.Ordinal).Count() != terminalStages.Length)
            return RecoveryMembershipFailure("duplicate creation or retirement in the retained execution");
        var retirementByEffect = terminalStages.ToDictionary(row => row.Result.EffectId, StringComparer.Ordinal);
        var turn = bundle.Input.Binding.Turn;

        // These identities were terminal before the common draft began. Completion may
        // neither resurrect them nor append additional history to a completed retirement.
        foreach (var result in terminations)
        {
            if (stagedCatalog.Occurrences.Any(row => row.EffectId == result.EffectId) ||
                runtimeCatalog.Occurrences.Any(row => row.EffectId == result.EffectId) ||
                publicationCatalog.Occurrences.Any(row => row.EffectId == result.EffectId) ||
                stagedPlan.ActiveEffects.Any(effect => ExactString(effect["effectId"], result.EffectId)) ||
                finalPlan.ActiveEffects.Any(effect => ExactString(effect["effectId"], result.EffectId)) ||
                !stagedIdentities.TryGetEntry(result.EffectId, out var stagedIdentity) ||
                !finalIdentities.TryGetEntry(result.EffectId, out var finalIdentity) ||
                !JsonNode.DeepEquals(stagedIdentity.Raw, finalIdentity.Raw) ||
                result.Disposition != "expired" || finalIdentity.State != "expired" ||
                !RecoveryIdentityCoordinatesAgree(finalIdentity, result.SourceKey, result.TargetKey, result.CarrierCoordinate) ||
                !stagedPlan.AllocatedTransitionIds.Contains(result.TerminalTransitionId, StringComparer.Ordinal) ||
                !finalPlan.AllocatedTransitionIds.Contains(result.TerminalTransitionId, StringComparer.Ordinal))
                return RecoveryMembershipFailure("retired effect is absent, changed or resurrected: " + result.EffectId);
            var terminal = finalIdentity.Transitions[^1];
            if (terminal.TransitionId != result.TerminalTransitionId || terminal.Kind != "expire" ||
                terminal.Turn != turn || terminal.EventRef != result.OperationRef ||
                terminal.EventRef != result.TransitionEventRef || terminal.ReceiptId is not null ||
                !terminal.SourceEffectIds.SequenceEqual(new[] { result.EffectId }, StringComparer.Ordinal) ||
                terminal.ResultEffectIds.Count != 0)
                return RecoveryMembershipFailure("retirement chronology changed: " + result.EffectId);
        }

        var finalWound = stages[^1].After;
        for (var stageIndex = 0; stageIndex < stages.Count; stageIndex++)
        {
            var stage = stages[stageIndex];
            foreach (var result in stage.Applications)
            {
                string? predecessor = null;
                if (stage.Before.Severity.Rank != stage.After.Severity.Rank)
                {
                    var predecessors = stage.Before.Consequences.OwnedEffectSources.RootBindings
                        .Where(root => root.DefinitionKey == result.SourceKey.DefinitionKey).ToArray();
                    if (predecessors.Length != 1)
                        return RecoveryMembershipFailure("generation has no unique private predecessor: " + result.EffectId);
                    predecessor = predecessors[0].EffectId;
                }
                if (!stagedIdentities.TryGetEntry(result.EffectId, out var stagedIdentity) ||
                    !finalIdentities.TryGetEntry(result.EffectId, out var identity) ||
                    result.Disposition != "created_new_identity" ||
                    !stagedPlan.AllocatedEffectIds.Contains(result.EffectId, StringComparer.Ordinal) ||
                    !stagedPlan.AllocatedTransitionIds.Contains(result.CreateTransitionId, StringComparer.Ordinal) ||
                    !finalPlan.AllocatedEffectIds.Contains(result.EffectId, StringComparer.Ordinal) ||
                    !finalPlan.AllocatedTransitionIds.Contains(result.CreateTransitionId, StringComparer.Ordinal) ||
                    !RecoveryIdentityCoordinatesAgree(identity, result.SourceKey, result.TargetKey, result.CarrierCoordinate) ||
                    identity.CreatedAtTurn != turn ||
                    !RecoveryCreateHistoryAgrees(identity, result, turn, predecessor))
                    return RecoveryMembershipFailure("created identity or generation provenance changed: " + result.EffectId);

                if (retirementByEffect.TryGetValue(result.EffectId, out var retirement))
                {
                    if (retirement.Index <= stageIndex)
                        return RecoveryMembershipFailure("creation does not precede its actual retirement: " + result.EffectId);
                    // The complete unchanged terminal identity was checked above, including
                    // this first-create transition and its later genuine retirement.
                    continue;
                }

                var activeMatches = finalPlan.ActiveEffects.Where(effect =>
                    ExactString(effect["effectId"], result.EffectId)).ToArray();
                if (finalWound.Lifecycle != "active" ||
                    finalWound.Consequences.OwnedEffectSources.RootBindings.Count(root =>
                        root.EffectId == result.EffectId && root.DefinitionKey == result.SourceKey.DefinitionKey) != 1 ||
                    !stagedCatalog.TryResolveOne(result.EffectId, out var staged) ||
                    !runtimeCatalog.TryResolveOne(result.EffectId, out var runtime) ||
                    !publicationCatalog.TryResolveOne(result.EffectId, out var publication) ||
                    staged.Coordinate != result.CarrierCoordinate || runtime.Coordinate != result.CarrierCoordinate ||
                    publication.Coordinate != result.CarrierCoordinate || activeMatches.Length != 1 ||
                    !JsonNode.DeepEquals(runtime.Effect, publication.Effect) ||
                    !JsonNode.DeepEquals(runtime.Effect, activeMatches[0]) ||
                    !EffectIdentityFieldsAgree(staged.Effect, runtime.Effect, result) ||
                    !EffectProgressAgrees(staged.Effect, runtime.Effect, stagedIdentity, identity, turn) ||
                    !WoundAcceptedTurnPlannerCore.IdentityAgrees(identity, runtime.Effect, runtime.Coordinate, turn) ||
                    !CreateEvidenceAgrees(identity, runtime.Effect, result, turn, predecessor))
                    return RecoveryMembershipFailure("surviving creation changed incompatibly: " + result.EffectId);
            }
        }
        return null;
    }

    /// <summary>
    /// Matches a retained identity to exact source, target and carrier coordinates.
    /// </summary>
    /// <param name="identity">
    /// Canonical active or terminal identity being validated.
    /// </param>
    /// <param name="source">
    /// Source key recorded by the actual private execution.
    /// </param>
    /// <param name="target">
    /// Target key recorded by the actual private execution.
    /// </param>
    /// <param name="coordinate">
    /// Actual carrier coordinate retained at application or retirement.
    /// </param>
    /// <returns>
    /// True only when all coordinates agree with the canonical identity header.
    /// </returns>
    private static bool RecoveryIdentityCoordinatesAgree(EffectIdentityEntry identity,
        EffectSourceKey source, EffectTargetKey target, EffectCarrierCoordinate coordinate) =>
        identity.Realm == source.Realm && source.Realm == target.Realm &&
        SourceAgrees(identity.Source, source) && TargetAgrees(identity.Target, target) &&
        WoundEffectTerminalOperationPlanner.TryCreateExpectedIdentityOwner(target, coordinate, out var owner) &&
        identity.Owner == owner && identity.StackCoordinate.Realm == source.Realm &&
        identity.StackCoordinate.SourceKind == source.Kind && identity.StackCoordinate.SourceId == source.SourceId &&
        identity.StackCoordinate.TargetKind == target.Kind && identity.StackCoordinate.TargetId == target.TargetId;

    /// <summary>
    /// Validates the immutable first-create evidence of a live or retired generation.
    /// </summary>
    /// <param name="identity">
    /// Canonical identity retaining its original create transition.
    /// </param>
    /// <param name="result">
    /// Actual application result authenticated by the private execution proof.
    /// </param>
    /// <param name="turn">
    /// Accepted turn shared by the ordered execution.
    /// </param>
    /// <param name="predecessor">
    /// Exact previous root for rematerialization, or null for a new complication root.
    /// </param>
    /// <returns>
    /// True when the first transition exactly matches the actual creation and predecessor.
    /// </returns>
    private static bool RecoveryCreateHistoryAgrees(EffectIdentityEntry identity,
        EffectAcceptedApplicationResult result, int turn, string? predecessor)
    {
        var create = identity.Transitions[0];
        return create.TransitionId == result.CreateTransitionId && create.Kind == "create" &&
            create.Turn == turn && create.EventRef == result.CreatedEventRef && create.ReceiptId is null &&
            create.SourceEffectIds.SequenceEqual(predecessor is null ? Array.Empty<string>() : new[] { predecessor }, StringComparer.Ordinal) &&
            create.ResultEffectIds.SequenceEqual(new[] { result.EffectId }, StringComparer.Ordinal);
    }

    /// <summary>
    /// Creates a structured rejection at the common wound carrier boundary.
    /// </summary>
    /// <param name="actual">
    /// Specific private-proof or membership mismatch.
    /// </param>
    /// <returns>
    /// An issue that prevents the coordinated publication.
    /// </returns>
    private static ValidationIssue RecoveryMembershipFailure(string actual) => Issue(
        "acceptedMechanics.woundCarrierAssembly", "accepted_mechanics_wound_final_effect_membership_invalid",
        "The final common effect plan does not retain its exact ordered recovery execution.",
        "authenticated surviving creations, unchanged terminal retirements and sealed completion ancestry", actual);
}
