namespace BookOfEternityClient.Services;

internal static partial class AcceptedMechanicsCarrierAssembler
{
    /// <summary>
    /// Adapts actual sealed treatment stages to shared ordered membership checks.
    /// A selected retired prefix, an independent legacy and a live joint wound
    /// retain distinct source/ownership joins; no recovery stage is manufactured.
    /// </summary>
    /// <param name="finalPlan">Genuine completed common effect plan.</param>
    /// <param name="woundPlan">Actual final wound mutations.</param>
    /// <param name="prepared">Private terminal or joint preparation.</param>
    /// <param name="accepted">Complete accepted result vectors, including joint acquisition.</param>
    /// <param name="stagedPlan">Sealed actual treatment execution.</param>
    /// <param name="turn">Accepted turn from the immutable wound input.</param>
    /// <param name="treatment">Genuine terminal family view from that preparation.</param>
    /// <param name="stagedCatalog">Canonical staged runtime carriers.</param>
    /// <param name="runtimeCatalog">Canonical final runtime carriers.</param>
    /// <param name="publicationCatalog">Canonical final publication carriers.</param>
    /// <param name="stagedIdentities">Canonical staged identities.</param>
    /// <param name="finalIdentities">Canonical final identities.</param>
    /// <returns>Null for exact complete membership; otherwise a structured refusal.</returns>
    private static ValidationIssue? ValidateTerminalTreatmentEffectMembership(EffectAcceptedTurnPlan finalPlan,
        WoundAcceptedTurnPlan woundPlan, WoundPreparedAcceptedTurnPlan prepared,
        WoundEffectBatchAcceptedPlan accepted, EffectAcceptedTurnPlan stagedPlan, int turn,
        WoundAcceptedTurnPlanner.TerminalTreatmentView treatment, EffectCarrierCatalog stagedCatalog,
        EffectCarrierCatalog runtimeCatalog, EffectCarrierCatalog publicationCatalog,
        EffectIdentityState stagedIdentities, EffectIdentityState finalIdentities)
    {
        if (!EffectAcceptedTurnPlanner.TryReadMortalTreatmentExecution(prepared, stagedPlan, out var stages) ||
            stages.Count != 2 || !finalPlan.IsAcceptedBoundaryComplete ||
            finalPlan.AcceptedBoundaryBasePlanFingerprint != WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(stagedPlan) ||
            finalPlan.AcceptedBoundaryFinalPlanFingerprint != WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(finalPlan))
            return RecoveryMembershipFailure("missing terminal treatment execution or changed common completion ancestry");
        var roots = prepared.EffectOperationBatches.SelectMany(batch => batch.RootApplications.Select(root => (Batch: batch, Root: root))).ToArray();
        var legacyBatches = treatment.Legacies.EffectOperationBatches;
        var finalWounds = woundPlan.CarrierContributions.SelectMany(row => row.Mutations)
            .Select(row => row.AfterWound).Where(row => row is not null).Select(row => row!).ToArray();
        var creations = new List<OrderedEffectMembershipCreation>();
        for (var index = 0; index < stages.Count; index++)
        foreach (var result in stages[index].Applications)
        {
            var matches = roots.Where(row => row.Root.ApplicationRef == result.ApplicationRef).ToArray();
            if (matches.Length != 1)
                return RecoveryMembershipFailure("terminal application has no unique sealed prepared root: " + result.ApplicationRef);
            var (batch, root) = matches[0]; var source = batch.SourceExport;
            if (root.ExpectedSourceKey != result.SourceKey || root.ExpectedTargetKey != result.TargetKey ||
                root.ExpectedCarrierCoordinate != result.CarrierCoordinate || source.Realm != result.SourceKey.Realm ||
                source.Kind != result.SourceKey.Kind || source.SourceId != result.SourceKey.SourceId ||
                root.ExpectedComponentCount != result.Materialization.ComponentCount ||
                root.ExpectedMaterializationFingerprint != result.Materialization.MaterializationFingerprint ||
                !root.SlotBindings.SequenceEqual(result.Materialization.SlotBindings))
                return RecoveryMembershipFailure("terminal application differs from its complete prepared root: " + result.ApplicationRef);

            var selectedPrefix = source.Kind == "wound" && source.SourceId == treatment.Outcome.Before.WoundId &&
                treatment.TreatmentBatch is { } prefix && batch.SourceExportFingerprint == prefix.SourceExportFingerprint;
            var legacy = source.Kind == "wound_legacy" && legacyBatches.Count(expected =>
                expected.PreparedWoundId == batch.PreparedWoundId && expected.SourceExportFingerprint == batch.SourceExportFingerprint) == 1;
            var acquisition = source.Kind == "wound" && source.SourceId != treatment.Outcome.Before.WoundId &&
                prepared.JointRepairContinuationAuthority is not null && batch.TransitionAuthority.TransitionKind is "create" or "worsen";
            if (!selectedPrefix && !legacy && !acquisition)
                return RecoveryMembershipFailure("terminal application has a foreign source family: " + result.ApplicationRef);
            WoundMaterializationEnvelope? finalWound = null;
            if (!legacy)
            {
                var current = finalWounds.Where(wound => wound.WoundId == result.SourceKey.SourceId).ToArray();
                if (current.Length > 1 || acquisition && current.Length != 1)
                    return RecoveryMembershipFailure("joint/current wound membership is missing or ambiguous: " + result.ApplicationRef);
                finalWound = current.SingleOrDefault();
            }
            creations.Add(new(index, result, root.PriorRootEffectId, finalWound));
        }
        return ValidateOrderedEffectMembership(finalPlan, stagedPlan, prepared, accepted, creations,
            stages.SelectMany((stage, index) => stage.Terminations.Select(result => (Result: result, Index: index))).ToArray(),
            turn, stagedCatalog, runtimeCatalog, publicationCatalog, stagedIdentities, finalIdentities);
    }
}
