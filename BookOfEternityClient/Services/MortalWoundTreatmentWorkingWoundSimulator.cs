using System.Collections.Immutable;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundTreatmentWorkingWoundSimulation(
    bool IsApplicable, bool Improved, WoundMaterializationEnvelope? WorkingWound);
internal sealed record MortalWoundTreatmentPreparedOperationResult(
    bool IsApplicable, bool Improved, WoundMaterializationEnvelope? After);
internal delegate MortalWoundTreatmentPreparedOperationResult MortalWoundTreatmentPreparedOperationReducer(
    WoundMaterializationEnvelope before, MortalWoundTreatmentOperation operation);

/// <summary>
/// One ordered applicability loop over the complete current graph. The canonical
/// facade retains its callback and canonical after-images; symbolic procedure
/// admission uses the same operations without inventing runtime identities.
/// </summary>
internal static class MortalWoundTreatmentWorkingWoundSimulator
{
    internal static MortalWoundTreatmentWorkingWoundSimulation Simulate(
        WoundMaterializationEnvelope? startingWound,
        IEnumerable<ImmutableArray<MortalWoundTreatmentOperation>> declaredResults,
        MortalWoundTreatmentPreparedOperationReducer? preparedOperationReducer = null)
    {
        ArgumentNullException.ThrowIfNull(declaredResults);
        if (startingWound is null || startingWound.Lifecycle != "active" ||
            MortalWoundTreatmentWorkingGraphProjection.FromCanonical(startingWound) is not { } starting)
            return new(false, false, null);
        MortalWoundTreatmentPreparedGraphOperationReducer? adapter = preparedOperationReducer is null ? null :
            (before, _, operation) =>
            {
                if (!before.TryExportExisting(out var exported)) return new(false, false, null);
                var prepared = preparedOperationReducer(exported!, operation);
                var after = prepared.IsApplicable
                    ? MortalWoundTreatmentWorkingGraphProjection.FromCanonical(prepared.After) : null;
                return new(after is not null, prepared.Improved, after);
            };
        var simulation = SimulateCore(starting, declaredResults, adapter,
            static (before, reduction) =>
            {
                if (!before.TryExportExisting(out var exported)) return null;
                var projection = MortalWoundTreatmentSeverityReductionPlanner.Project(exported!,
                    reduction.Steps, before.Scalars.Severity.LastChangeEventRef);
                return projection.IsValid
                    ? MortalWoundTreatmentWorkingGraphProjection.FromCanonical(projection.Projection!.ProvisionalAfter) : null;
            },
            static candidate => candidate.TryExportExisting(out var exported)
                ? MortalWoundTreatmentWorkingGraphProjection.FromCanonical(exported) : null);
        if (!simulation.IsApplicable || simulation.WorkingGraph is null ||
            !simulation.WorkingGraph.TryExportExisting(out var wound)) return new(false, false, null);
        return new(true, simulation.Improved, wound);
    }

    internal static MortalWoundTreatmentWorkingGraphSimulation SimulateGraph(
        WoundMaterializationEnvelope? startingWound,
        IEnumerable<ImmutableArray<MortalWoundTreatmentOperation>> declaredResults,
        MortalWoundTreatmentPreparedGraphOperationReducer preparedOperationReducer)
    {
        ArgumentNullException.ThrowIfNull(declaredResults);
        if (startingWound is null || startingWound.Lifecycle != "active" ||
            MortalWoundTreatmentWorkingGraphProjection.FromCanonical(startingWound) is not { } starting)
            return NotApplicable();
        var result = SimulateCore(starting, declaredResults, preparedOperationReducer, ProjectGraphReduction,
            static candidate => candidate.ValidateGraph("mortalWoundTreatment.workingGraph").IsEmpty ? candidate : null);
        if (result.IsApplicable && result.WorkingGraph is { } final && startingWound.Owner.Realm == "mortal_world" &&
            startingWound.Classification.Domain == "physical" && final.Scalars.Severity.Rank == starting.Scalars.Severity.Rank)
        {
            var continuity = WoundSameRankOwnedSourceContinuity.Compare(
                starting.Graph.Roots.ToDictionary(row => row.DefinitionKey, row => row.Reference, StringComparer.Ordinal),
                final.Graph.Roots.ToDictionary(row => row.DefinitionKey, row => row.Reference, StringComparer.Ordinal),
                WoundMaterializationContract.BuildOwnedEffectDefinitionFacts(starting.Graph.Definitions.Select(row => row.Definition).ToArray())
                    .ToDictionary(row => row.DefinitionKey, StringComparer.Ordinal),
                WoundMaterializationContract.BuildOwnedEffectDefinitionFacts(final.Graph.Definitions.Select(row => row.Definition).ToArray())
                    .ToDictionary(row => row.DefinitionKey, StringComparer.Ordinal));
            if (continuity.RootRebinding is not null || continuity.ChangedDefinitionKey is not null) return NotApplicable();
        }
        return result;
    }

    private static MortalWoundTreatmentWorkingGraphSimulation SimulateCore(
        MortalWoundTreatmentWorkingGraphProjection starting,
        IEnumerable<ImmutableArray<MortalWoundTreatmentOperation>> declaredResults,
        MortalWoundTreatmentPreparedGraphOperationReducer? prepareComplex,
        Func<MortalWoundTreatmentWorkingGraphProjection, MortalWoundReduceSeverityOperation,
            MortalWoundTreatmentWorkingGraphProjection?> projectReduction,
        Func<MortalWoundTreatmentWorkingGraphProjection, MortalWoundTreatmentWorkingGraphProjection?> normalizeAfter)
    {
        var working = starting;
        var improved = false;
        var terminal = false;
        var resultIndex = 0;
        try
        {
            foreach (var result in declaredResults)
            {
                if (result.IsDefault || (terminal && result.Length != 0)) return NotApplicable();
                for (var index = 0; index < result.Length; index++)
                {
                    if (!TryApply(working, new(resultIndex, index), result[index], result.Length,
                            prepareComplex, projectReduction, ref terminal, out var candidate, out var operationImproved))
                        return NotApplicable();
                    var after = normalizeAfter(candidate);
                    if (after is null) return NotApplicable();
                    working = after;
                    improved |= operationImproved;
                }
                resultIndex = checked(resultIndex + 1);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or JsonException or OverflowException)
        {
            return NotApplicable();
        }
        return new(true, improved, working);
    }

    private static bool TryApply(MortalWoundTreatmentWorkingGraphProjection before,
        WoundWorkingOperationAddress address, MortalWoundTreatmentOperation? operation, int resultLength,
        MortalWoundTreatmentPreparedGraphOperationReducer? prepareComplex,
        Func<MortalWoundTreatmentWorkingGraphProjection, MortalWoundReduceSeverityOperation,
            MortalWoundTreatmentWorkingGraphProjection?> projectReduction,
        ref bool terminal, out MortalWoundTreatmentWorkingGraphProjection after, out bool improved)
    {
        after = before;
        improved = false;
        if (operation is null || terminal) return false;
        var scalars = before.Scalars;
        switch (operation)
        {
            case MortalWoundNoImprovementOperation:
                return resultLength == 1;
            case MortalWoundStabilizeOperation:
                if (scalars.Care.State is "stabilized" or "healed") return false;
                after = before.WithScalars(scalars with
                {
                    Care = scalars.Care with { State = "stabilized",
                        StabilizedAtTurn = scalars.Care.StabilizedAtTurn ?? before.OriginalTransitionTurn },
                    Recovery = scalars.Recovery with { Blockers = scalars.Recovery.Blockers
                        .Where(static blocker => !string.Equals(blocker, "not_stabilized", StringComparison.Ordinal)).ToImmutableArray() }
                });
                improved = true;
                return true;
            case MortalWoundAddRecoveryOperation addRecovery:
                after = before.WithScalars(scalars with { Recovery = scalars.Recovery with
                {
                    CurrentStepProgress = checked(scalars.Recovery.CurrentStepProgress + addRecovery.Points)
                }});
                improved = addRecovery.Points > 0;
                return true;
            case MortalWoundReduceSeverityOperation reduction:
                var projected = projectReduction(before, reduction);
                if (projected is null) return false;
                after = projected;
                improved = after.Scalars.Severity.Rank < scalars.Severity.Rank;
                return true;
            case MortalWoundRemoveComplicationOperation removal:
                if (!before.TryRemoveExistingComplication(removal.ComplicationId, out var removed)) return false;
                after = removed!;
                improved = true;
                return true;
            case MortalWoundHealOperation:
                if (scalars.Severity.Rank != 1) return false;
                terminal = true;
                improved = true;
                return true;
            case MortalWoundAddComplicationOperation:
            case MortalWoundApplyDeteriorationOperation:
                if (prepareComplex is null) return false;
                var prepared = prepareComplex(before, address, operation);
                if (!prepared.IsApplicable || prepared.After is null) return false;
                after = prepared.After;
                improved = prepared.Improved;
                return true;
            default:
                return false;
        }
    }

    private static MortalWoundTreatmentWorkingGraphProjection? ProjectGraphReduction(
        MortalWoundTreatmentWorkingGraphProjection before, MortalWoundReduceSeverityOperation reduction)
    {
        var issues = new List<ValidationIssue>();
        if (!MortalWoundTreatmentSeverityReductionPlanner.ValidateReductionArguments(reduction.Steps,
                before.Scalars.Severity.LastChangeEventRef, issues) ||
            !MortalWoundTreatmentSeverityReductionPlanner.TryGetReductionRank(before.Scalars.Severity.Rank,
                reduction.Steps, out var rank, issues)) return null;
        // Validate tagged provenance before producing the injective detached adapter table.
        if (!before.ValidateGraph("mortalWoundTreatment.workingGraph").IsEmpty) return null;
        var detached = WoundPersistedConsequenceEnvelopeAdapter.AdaptWorkingGraph(before.Graph,
            "mortalWoundTreatment.severityReductionProjection.before.consequences");
        var validation = MortalWoundTreatmentSeverityReductionPlanner.ValidateDestinationGraph(rank,
            detached.Definitions, detached.Roots, before.Graph.Entries.Length, issues);
        if (!validation.IsValid) return null;
        var scalars = MortalWoundTreatmentSeverityReductionPlanner.ApplyReductionScalars(before.Scalars.Severity,
            rank, before.Scalars.Severity.LastChangeEventRef);
        return before.WithScalars(before.Scalars with { Severity = scalars.Severity, SlotBudget = scalars.SlotBudget });
    }

    internal static bool TryRemoveComplication(WoundMaterializationEnvelope before, string complicationId,
        out WoundMaterializationEnvelope after)
    {
        after = before;
        var graph = MortalWoundTreatmentWorkingGraphProjection.FromCanonical(before);
        if (graph is null || !graph.TryRemoveExistingComplication(complicationId, out var removed) ||
            !removed!.TryExportExisting(out var exported)) return false;
        after = exported!;
        return true;
    }

    private static MortalWoundTreatmentWorkingGraphSimulation NotApplicable() => new(false, false, null);
}
