using System.Collections.Immutable;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundTreatmentWorkingWoundSimulation(
    bool IsApplicable,
    bool Improved,
    WoundMaterializationEnvelope? WorkingWound);

internal sealed record MortalWoundTreatmentPreparedOperationResult(
    bool IsApplicable,
    bool Improved,
    WoundMaterializationEnvelope? After);

internal delegate MortalWoundTreatmentPreparedOperationResult
    MortalWoundTreatmentPreparedOperationReducer(
        WoundMaterializationEnvelope before,
        MortalWoundTreatmentOperation operation);

/// <summary>
/// Pure applicability reducer for authored treatment results.  It deliberately keeps
/// the complete typed wound envelope while operations are evaluated, so a scalar
/// severity change cannot become authority for an impossible consequence/source graph.
/// Permanent transition/effect identities remain the responsibility of the accepted
/// treatment planner; this reducer only proves that a complete canonical after-image
/// can exist for every intermediate operation.
/// </summary>
internal static class MortalWoundTreatmentWorkingWoundSimulator
{
    internal static MortalWoundTreatmentWorkingWoundSimulation Simulate(
        WoundMaterializationEnvelope? startingWound,
        IEnumerable<ImmutableArray<MortalWoundTreatmentOperation>> declaredResults,
        MortalWoundTreatmentPreparedOperationReducer? preparedOperationReducer = null)
    {
        ArgumentNullException.ThrowIfNull(declaredResults);
        if (startingWound is null ||
            !string.Equals(startingWound.Lifecycle, "active", StringComparison.Ordinal))
        {
            return NotApplicable();
        }

        if (!TryRoundTrip(startingWound, out var working))
            return NotApplicable();

        var improved = false;
        var terminal = false;
        try
        {
            foreach (var result in declaredResults)
            {
                if (result.IsDefault)
                    return NotApplicable();
                if (terminal && result.Length != 0)
                    return NotApplicable();

                for (var index = 0; index < result.Length; index++)
                {
                    if (!TryApply(
                            working,
                            result[index],
                            result.Length,
                            preparedOperationReducer,
                            ref terminal,
                            out var after,
                            out var operationImproved))
                    {
                        return NotApplicable();
                    }

                    working = after;
                    improved |= operationImproved;
                }
            }
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException or
                                           JsonException or
                                           OverflowException)
        {
            return NotApplicable();
        }

        return new MortalWoundTreatmentWorkingWoundSimulation(
            true,
            improved,
            working);
    }

    private static bool TryApply(
        WoundMaterializationEnvelope before,
        MortalWoundTreatmentOperation? operation,
        int resultLength,
        MortalWoundTreatmentPreparedOperationReducer? preparedOperationReducer,
        ref bool terminal,
        out WoundMaterializationEnvelope after,
        out bool improved)
    {
        after = before;
        improved = false;
        if (operation is null || terminal)
            return false;

        WoundMaterializationEnvelope candidate;
        switch (operation)
        {
            case MortalWoundNoImprovementOperation:
                return resultLength == 1;

            case MortalWoundStabilizeOperation:
                if (before.Care.State is "stabilized" or "healed")
                    return false;
                candidate = before with
                {
                    Care = before.Care with
                    {
                        State = "stabilized",
                        StabilizedAtTurn = before.Care.StabilizedAtTurn ??
                                           before.LastTransition.Turn
                    },
                    Recovery = before.Recovery with
                    {
                        Blockers = before.Recovery.Blockers
                            .Where(static blocker => !string.Equals(
                                blocker,
                                "not_stabilized",
                                StringComparison.Ordinal))
                            .ToImmutableArray()
                    }
                };
                improved = true;
                break;

            case MortalWoundAddRecoveryOperation addRecovery:
                candidate = before with
                {
                    Recovery = before.Recovery with
                    {
                        CurrentStepProgress = checked(
                            before.Recovery.CurrentStepProgress + addRecovery.Points)
                    }
                };
                improved = addRecovery.Points > 0;
                break;

            case MortalWoundReduceSeverityOperation reduceSeverity:
            {
                var projection = MortalWoundTreatmentSeverityReductionPlanner.Project(
                    before,
                    reduceSeverity.Steps,
                    before.Severity.LastChangeEventRef);
                if (!projection.IsValid || projection.Projection is null)
                    return false;
                candidate = projection.Projection.ProvisionalAfter;
                improved = candidate.Severity.Rank < before.Severity.Rank;
                break;
            }

            case MortalWoundRemoveComplicationOperation removeComplication:
                if (!TryRemoveComplication(before, removeComplication.ComplicationId, out candidate))
                    return false;
                improved = true;
                break;

            case MortalWoundHealOperation:
                if (before.Severity.Rank != 1)
                    return false;
                terminal = true;
                after = before;
                improved = true;
                return true;

            case MortalWoundAddComplicationOperation:
            case MortalWoundApplyDeteriorationOperation:
                if (preparedOperationReducer is null)
                    return false;
                var prepared = preparedOperationReducer(before, operation);
                if (!prepared.IsApplicable || prepared.After is null ||
                    !TryRoundTrip(prepared.After, out after))
                {
                    return false;
                }
                improved = prepared.Improved;
                return true;

            default:
                return false;
        }

        if (!TryRoundTrip(candidate, out after))
        {
            improved = false;
            return false;
        }

        return true;
    }

    private static MortalWoundTreatmentWorkingWoundSimulation NotApplicable() =>
        new(false, false, null);

    private static bool TryRemoveComplication(
        WoundMaterializationEnvelope before,
        string complicationId,
        out WoundMaterializationEnvelope after)
    {
        after = before;
        var matches = before.Complications.Where(complication => string.Equals(
                complication.ComplicationId,
                complicationId,
                StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
            return false;

        var removedRoots = matches[0].OwnedEffectIds.ToHashSet(StringComparer.Ordinal);
        var retainedBindings = before.Consequences.OwnedEffectSources.RootBindings
            .Where(binding => !removedRoots.Contains(binding.EffectId))
            .ToImmutableArray();
        var retainedDefinitionKeys = ReachableDefinitionKeys(
            retainedBindings,
            before.Consequences.OwnedEffectSources.DefinitionFacts);
        var retainedDefinitions = before.Consequences.OwnedEffectSources.Definitions
            .Where(definition =>
                definition.ValueKind == JsonValueKind.Object &&
                definition.TryGetProperty("definitionKey", out var key) &&
                key.ValueKind == JsonValueKind.String &&
                retainedDefinitionKeys.Contains(key.GetString()!))
            .Select(static definition => definition.Clone())
            .ToImmutableArray();
        var retainedFacts = before.Consequences.OwnedEffectSources.DefinitionFacts
            .Where(fact => retainedDefinitionKeys.Contains(fact.DefinitionKey))
            .ToImmutableArray();
        var retainedEntries = before.Consequences.Entries
            .Where(entry => !removedRoots.Contains(entry.EffectId))
            .Select((entry, index) => entry with { Slot = index + 1 })
            .ToImmutableArray();

        after = before with
        {
            Complications = before.Complications
                .Where(complication => !string.Equals(
                    complication.ComplicationId,
                    complicationId,
                    StringComparison.Ordinal))
                .ToImmutableArray(),
            Consequences = before.Consequences with
            {
                SlotsUsed = retainedEntries.Length,
                Entries = retainedEntries,
                OwnedEffectSources = new WoundOwnedEffectSources(
                    retainedDefinitions,
                    retainedBindings)
                {
                    DefinitionFacts = retainedFacts
                }
            }
        };
        return true;
    }

    private static HashSet<string> ReachableDefinitionKeys(
        IEnumerable<WoundRootEffectBinding> roots,
        IEnumerable<WoundOwnedEffectDefinitionFact> facts)
    {
        var byKey = facts.ToDictionary(
            static fact => fact.DefinitionKey,
            StringComparer.Ordinal);
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(roots.Select(static root => root.DefinitionKey));
        while (pending.Count != 0)
        {
            var definitionKey = pending.Pop();
            if (!reachable.Add(definitionKey) ||
                !byKey.TryGetValue(definitionKey, out var fact))
            {
                continue;
            }

            foreach (var target in fact.ApplyDefinitionTargets)
                pending.Push(target);
        }

        return reachable;
    }

    private static bool TryRoundTrip(
        WoundMaterializationEnvelope candidate,
        out WoundMaterializationEnvelope parsedWound)
    {
        parsedWound = candidate;
        var parsed = WoundMaterializationContract.Parse(
            WoundMaterializationContract.SerializeCanonical(candidate),
            "treatmentAttempt.workingWound");
        if (!parsed.IsValid || parsed.Wound is null)
            return false;
        parsedWound = parsed.Wound;
        return true;
    }
}
