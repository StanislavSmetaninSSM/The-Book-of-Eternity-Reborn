using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class MortalWoundTreatmentCompletion
{
    private static WoundMaterializationEnvelope ReconstructFinalWound(
        MortalWoundTreatmentPersistedResult.GuidedComparison comparison, CurrentCanonicalAgreement current)
    {
        var preparation = comparison.Preparation;
        var before = preparation.Before;
        preparation.TryRecomposeSelectedGraph(out var selected);
        WoundMaterializationEnvelope assembled;
        if (selected is not null || preparation.SeverityReduction is { })
        {
            var projection = preparation.SeverityReduction;
            var roots = selected is not null ? selected.FinalRoots : projection!.Roots.Select(row =>
                new MortalWoundTreatmentSelectedRoot(WoundWorkingReference.Existing(row.PriorEffectId),
                    row.DefinitionKey, row.OwnershipDomain, row.Slots.ToImmutableArray(), row.PriorEffectId, null)).ToImmutableArray();
            var definitions = (selected is not null
                    ? selected.FinalGraph.Definitions.Select(row => row.Definition)
                    : projection!.Before.Consequences.OwnedEffectSources.Definitions)
                .Select(value =>
                {
                    var definition = JsonNode.Parse(value.GetRawText())!.AsObject();
                    return new WoundEffectSourceDefinition(definition["definitionKey"]!.GetValue<string>(), definition);
                }).ToArray();
            var rows = new List<WoundAcceptedTurnPlannerCore.WoundFinalRootAssemblyRow>();
            var allocatedOrdinal = 0;
            foreach (var root in roots)
            {
                var allocates = selected is null || selected.FinalSeverityChanged || root.AdditionBinding is not null;
                var id = root.OriginalEffectId;
                if (allocates)
                {
                    allocatedOrdinal++;
                    var definition = definitions.Single(value => value.DefinitionKey == root.DefinitionKey).Definition;
                    id = RequireAllocatedRoot(before, root.DefinitionKey, definition, allocatedOrdinal,
                        comparison.Resolution.Coordinates.EventRef, comparison.Resolution.Coordinates.Turn, current);
                }
                if (string.IsNullOrWhiteSpace(id)) throw Invalid("A finalized root lost its original or durable allocation identity.");
                rows.Add(new(id, root.DefinitionKey, root.OwnershipDomain, root.Slots));
            }
            if (selected is null)
            {
                var slot = 0;
                rows = rows.OrderBy(row => row.EffectId, StringComparer.Ordinal).Select(row => row with
                { Slots = row.Slots.Select(value => value with { Slot = ++slot }).ToImmutableArray() }).ToList();
            }
            var provisional = preparation.ProvisionalAfter;
            var scalars = selected?.FinalScalars ?? new WoundFinalAssemblyScalars(provisional.Severity,
                provisional.Care, provisional.Recovery, provisional.Treatment, provisional.LastTransition, provisional.Consequences.SlotBudget);
            var complications = selected is null ? provisional.Complications : selected.FinalGraph.Complications.Select(row =>
                new WoundComplication(row.Reference.Origin == WoundWorkingReferenceOrigin.Existing ? row.Reference.Value :
                    selected.Additions.Single(add => add.Reference == row.Reference).Binding.ComplicationId,
                    row.Kind, row.State, row.DisplayName, row.TreatmentDifficultyModifier,
                    ImmutableArray<string>.Empty, row.Visibility)).ToArray();
            var final = WoundAcceptedTurnPlannerCore.BuildFinalWoundCore(before, scalars, complications, definitions, rows);
            if (final.Wound is null || final.Issues.Count != 0) throw Invalid("The complete selected final wound cannot be assembled.");
            assembled = final.Wound;
        }
        else assembled = preparation.ProvisionalAfter;

        var resolution = comparison.Resolution;
        var coordinates = resolution.Coordinates;
        var reduction = WoundTransitionReducer.Reduce(new WoundTransitionRequest("treat", preparation.TransitionId,
            coordinates.OperationKey, coordinates.EventRef, coordinates.Turn, before, assembled,
            new WoundTreatmentEvidence(resolution.ResolutionAuthorityFingerprint,
                WoundIdentityState.ComputeSemanticFingerprint(before), WoundIdentityState.ComputeSemanticFingerprint(assembled),
                coordinates.RouteId, coordinates.AttemptId,
                MortalWoundTreatmentOutcomePublicationPlanner.CreateDeclaredOutcome(assembled, selected, preparation.PreparesTerminalHeal))));
        if (!reduction.IsValid || reduction.ProposedAfter is null)
            throw Invalid("The final treatment transition does not agree with its declared mode outcome.");
        return reduction.ProposedAfter;
    }

    private static string RequireAllocatedRoot(WoundMaterializationEnvelope before, string definitionKey,
        JsonObject definition, int ordinal, string causalEvent, int turn, CurrentCanonicalAgreement current)
    {
        var creationEvent = WoundEffectOperationEventRef.Create(causalEvent, 1, ordinal, "apply");
        if (!WoundEffectCarrierAdapter.TryCreateTargetKey(before.Owner, out var target) ||
            !WoundEffectCarrierAdapter.TryCreateCarrierCoordinate(before.Owner, target, definition, out var carrier))
            throw Invalid("The selected root has no canonical target/carrier.");
        var matches = current.EffectIndex.Entries.Where(entry => entry.CreatedAtTurn == turn &&
            entry.Transitions.Any(row => row.Kind == "create" && row.Turn == turn && row.EventRef == creationEvent)).ToArray();
        if (matches.Length != 1) throw Invalid("The selected root's exact durable allocation is missing or ambiguous.");
        var entry = matches[0];
        var creation = entry.Transitions.Single(row => row.Kind == "create" && row.EventRef == creationEvent);
        if (entry.Realm != before.Owner.Realm || entry.State != "active" ||
            entry.Source["kind"]?.GetValue<string>() != "wound" || entry.Source["sourceId"]?.GetValue<string>() != before.WoundId ||
            entry.Source["definitionKey"]?.GetValue<string>() != definitionKey ||
            entry.Target["kind"]?.GetValue<string>() != target.Kind || entry.Target["targetId"]?.GetValue<string>() != target.TargetId ||
            creation.SourceEffectIds.Count != 0 || !creation.ResultEffectIds.SequenceEqual(new[] { entry.EffectId }, StringComparer.Ordinal) ||
            creation.ReceiptId is not null || !current.Effects.TryResolveOne(entry.EffectId, out var occurrence) || occurrence.Coordinate != carrier ||
            occurrence.Effect["chronology"] is not JsonObject chronology || chronology["createdEventRef"]?.GetValue<string>() != creationEvent ||
            chronology["causalEventRef"]?.GetValue<string>() != causalEvent || chronology["createdAtTurn"]?.GetValue<int>() != turn ||
            occurrence.Effect["components"] is not JsonArray actualComponents || definition["components"] is not JsonArray expectedComponents)
            throw Invalid("The durable root allocation is foreign to the selected source, target or chronology.");
        var source = new EffectSourceKey(before.Owner.Realm, "wound", before.WoundId, definitionKey);
        if (WoundEffectMaterializationFingerprint.Compute(source, definition["schemaVersion"]!.GetValue<int>(), new JsonObject(), actualComponents) !=
            WoundEffectMaterializationFingerprint.Compute(source, definition["schemaVersion"]!.GetValue<int>(), new JsonObject(), expectedComponents))
            throw Invalid("The allocated root materialization differs from the declared selected definition.");
        return entry.EffectId;
    }
}
