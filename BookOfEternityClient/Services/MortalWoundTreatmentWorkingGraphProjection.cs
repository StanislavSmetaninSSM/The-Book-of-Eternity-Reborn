using System.Collections.Immutable;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundTreatmentWorkingScalars(
    WoundSeverity Severity, WoundCare Care, WoundRecovery Recovery, int SlotBudget);
internal sealed record MortalWoundTreatmentWorkingGraphSimulation(
    bool IsApplicable, bool Improved, MortalWoundTreatmentWorkingGraphProjection? WorkingGraph);
internal sealed record MortalWoundTreatmentPreparedGraphOperationResult(
    bool IsApplicable, bool Improved, MortalWoundTreatmentWorkingGraphProjection? After);
internal delegate MortalWoundTreatmentPreparedGraphOperationResult
    MortalWoundTreatmentPreparedGraphOperationReducer(
        MortalWoundTreatmentWorkingGraphProjection before,
        WoundWorkingOperationAddress address, MortalWoundTreatmentOperation operation);

/// <summary>
/// Detached reference view over the existing definition language. The baseline owns
/// unchanged metadata only; Graph is the sole current topology, and SlotBudget is
/// independent of rank. No unresolved reference can escape as a runtime identity.
/// </summary>
internal sealed class MortalWoundTreatmentWorkingGraphProjection
{
    private readonly WoundMaterializationEnvelope _baseline;
    internal MortalWoundTreatmentWorkingScalars Scalars { get; }
    internal WoundWorkingOwnedGraph Graph { get; }
    internal int OriginalTransitionTurn => _baseline.LastTransition.Turn;

    private MortalWoundTreatmentWorkingGraphProjection(WoundMaterializationEnvelope baseline,
        MortalWoundTreatmentWorkingScalars scalars, WoundWorkingOwnedGraph graph)
    {
        _baseline = baseline;
        Scalars = scalars with
        {
            Severity = scalars.Severity with { }, Care = scalars.Care with { },
            Recovery = scalars.Recovery with
            {
                Blockers = scalars.Recovery.Blockers.ToImmutableArray(),
                DeteriorationPolicy = scalars.Recovery.DeteriorationPolicy?.Clone(),
                RecoveryAnchor = scalars.Recovery.RecoveryAnchor is { } anchor ? anchor with { } : null,
                DeteriorationAnchor = scalars.Recovery.DeteriorationAnchor is { } deterioration ? deterioration with { } : null
            }
        };
        Graph = new WoundWorkingOwnedGraph(
            graph.Complications.Select(static complication => complication with
            {
                OwnedRoots = complication.OwnedRoots.ToImmutableArray()
            }).ToImmutableArray(),
            graph.Definitions.Select(static definition => definition with { Definition = definition.Definition.Clone() }).ToImmutableArray(),
            graph.Roots.Select(static root => root with { }).ToImmutableArray(),
            graph.Entries.Select(static entry => entry with { }).ToImmutableArray());
    }

    internal static MortalWoundTreatmentWorkingGraphProjection? FromCanonical(WoundMaterializationEnvelope? candidate)
    {
        if (candidate is null) return null;
        try
        {
            var parsed = WoundMaterializationContract.Parse(WoundMaterializationContract.SerializeCanonical(candidate),
                "treatmentAttempt.workingWound");
            if (!parsed.IsValid || parsed.Wound is null) return null;
            var baseline = WoundAcceptedTurnData.CloneWound(parsed.Wound)!;
            return new(baseline, new(baseline.Severity, baseline.Care, baseline.Recovery, baseline.Consequences.SlotBudget),
                WoundMaterializationContract.ImportOwnedGraph(baseline.Complications, baseline.Consequences));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or JsonException or OverflowException)
        {
            return null;
        }
    }

    internal bool TryExportExisting(out WoundMaterializationEnvelope? wound)
    {
        wound = null;
        if (Graph.Complications.Any(static row => !IsExistingCoordinate(row.Reference)) ||
            Graph.Definitions.Any(static row => !IsExistingCoordinate(row.Reference)) ||
            Graph.Roots.Any(static row => !IsExistingCoordinate(row.Reference)) ||
            Graph.Entries.Any(static row => !IsExistingCoordinate(row.Root)) ||
            Graph.Complications.Any(static row => row.OwnedRoots.Any(static root => !IsExistingCoordinate(root))))
            return false;
        var definitions = Graph.Definitions.Select(static row => row.Definition.Clone()).ToImmutableArray();
        var candidate = _baseline with
        {
            Severity = Scalars.Severity, Care = Scalars.Care, Recovery = Scalars.Recovery,
            Complications = Graph.Complications.Select(static complication => new WoundComplication(
                complication.Reference.Value, complication.Kind, complication.State, complication.DisplayName,
                complication.TreatmentDifficultyModifier, complication.OwnedRoots.Select(static root => root.Value).ToImmutableArray(),
                complication.Visibility)).ToImmutableArray(),
            Consequences = new WoundConsequences(Scalars.SlotBudget, Graph.Entries.Length,
                Graph.Entries.Select(static entry => new WoundConsequenceEntry(entry.Slot, entry.ProfileKey,
                    entry.Root.Value, entry.ReadableSummary)).ToImmutableArray())
            {
                OwnedEffectSources = new WoundOwnedEffectSources(definitions,
                    Graph.Roots.Select(static root => new WoundRootEffectBinding(root.Reference.Value, root.DefinitionKey)).ToImmutableArray())
                {
                    DefinitionFacts = WoundMaterializationContract.BuildOwnedEffectDefinitionFacts(definitions)
                }
            }
        };
        var parsed = WoundMaterializationContract.Parse(WoundMaterializationContract.SerializeCanonical(candidate),
            "treatmentAttempt.workingWound");
        if (!parsed.IsValid) return false;
        wound = parsed.Wound;
        return wound is not null;
    }

    private static bool IsExistingCoordinate(WoundWorkingReference reference) =>
        reference.Origin == WoundWorkingReferenceOrigin.Existing &&
        reference.Address == new WoundWorkingOperationAddress(-1, -1);

    internal MortalWoundTreatmentWorkingGraphProjection WithScalars(MortalWoundTreatmentWorkingScalars scalars) => new(_baseline, scalars, Graph);
    internal MortalWoundTreatmentWorkingGraphProjection WithGraph(WoundWorkingOwnedGraph graph) => new(_baseline, Scalars, graph);

    internal bool TryRemoveExistingComplication(string complicationId, out MortalWoundTreatmentWorkingGraphProjection? after)
    {
        after = null;
        var reference = WoundWorkingReference.Existing(complicationId);
        var matches = Graph.Complications.Where(complication => complication.Reference == reference).ToArray();
        if (matches.Length != 1) return false;
        var removed = matches[0].OwnedRoots.ToHashSet();
        var roots = Graph.Roots.Where(root => !removed.Contains(root.Reference)).ToImmutableArray();
        var facts = WoundMaterializationContract.BuildOwnedEffectDefinitionFacts(
            Graph.Definitions.Select(static definition => definition.Definition).ToArray())
            .ToDictionary(static fact => fact.DefinitionKey, StringComparer.Ordinal);
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(roots.Select(static root => root.DefinitionKey));
        while (pending.Count != 0)
        {
            var key = pending.Pop();
            if (!reachable.Add(key) || !facts.TryGetValue(key, out var fact)) continue;
            foreach (var target in fact.ApplyDefinitionTargets) pending.Push(target);
        }
        after = WithGraph(new(
            Graph.Complications.Where(complication => complication.Reference != reference).ToImmutableArray(),
            Graph.Definitions.Where(definition => definition.Definition.TryGetProperty("definitionKey", out var key) &&
                key.ValueKind == JsonValueKind.String && reachable.Contains(key.GetString()!)).ToImmutableArray(), roots,
            Graph.Entries.Where(entry => !removed.Contains(entry.Root)).Select((entry, index) => entry with { Slot = index + 1 }).ToImmutableArray()));
        return true;
    }

    internal bool TryAppendComplication(MortalWoundComplicationProposalDraft draft, WoundWorkingReferenceOrigin origin,
        WoundWorkingOperationAddress address, out MortalWoundTreatmentWorkingGraphProjection? after)
    {
        after = null;
        if (origin is not (WoundWorkingReferenceOrigin.DirectAddition or WoundWorkingReferenceOrigin.PolicyAddition) ||
            address.ResultIndex < 0 || address.OperationIndex < 0) return false;
        try
        {
            var fragment = WoundResponseInputComposer.ConvertTreatmentComplicationGraph(draft, _baseline.WoundId, origin, address);
            var addedEntries = fragment.Entries.Select(entry => entry with { Slot = checked(entry.Slot + Graph.Entries.Length) });
            var candidate = WithGraph(new(Graph.Complications.AddRange(fragment.Complications),
                Graph.Definitions.AddRange(fragment.Definitions), Graph.Roots.AddRange(fragment.Roots), Graph.Entries.AddRange(addedEntries)));
            if (!candidate.ValidateGraph("mortalWoundTreatment.workingGraph").IsEmpty) return false;
            after = candidate;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or JsonException or OverflowException)
        {
            return false;
        }
    }

    internal ImmutableArray<ValidationIssue> ValidateGraph(string path)
    {
        var issues = new List<ValidationIssue>();
        var context = new WoundWorkingGraphContext(_baseline.WoundId, _baseline.Lifecycle, _baseline.Owner,
            _baseline.Classification, Scalars.Severity, Scalars.Care, Scalars.Recovery, Scalars.SlotBudget, Graph.Entries.Length);
        if (!WoundMaterializationContract.ValidateWorkingGraphBoundary(context, Graph, path, issues)) return issues.ToImmutableArray();
        var retainedReferences = Graph.Complications
            .Where(row => IsExistingCoordinate(row.Reference))
            .Select(row => row.Reference).ToHashSet();
        var remainingCanonicalComplications = _baseline.Complications
            .Where(row => retainedReferences.Contains(WoundWorkingReference.Existing(row.ComplicationId)))
            .ToImmutableArray();
        var targetKind = WoundMaterializationContract.ResolveEffectTargetKind(_baseline.Owner.OwnerKind);
        WoundMaterializationContract.ValidateRetainedTreatmentProjection(_baseline.Treatment,
            path + ".treatment", _baseline.Classification.Domain, _baseline.Owner.Realm, targetKind,
            Scalars.Severity.Rank, remainingCanonicalComplications, Scalars.Recovery.DeteriorationPolicy, issues);
        WoundMaterializationContract.ValidateRetainedDeteriorationPolicy(Scalars.Recovery.DeteriorationPolicy,
            path + ".recovery.deteriorationPolicy", _baseline.Classification.Domain, _baseline.Owner.Realm,
            targetKind, Scalars.Severity.Rank, issues);
        WoundMaterializationContract.ValidateCommonOwnedDefinitions(JsonSerializer.SerializeToElement(
            Graph.Definitions.Select(static definition => definition.Definition).ToArray()),
            path + ".consequences.ownedEffectSources.definitions", _baseline.Owner.Realm, issues);
        WoundMaterializationContract.ValidateOwnedGraph(context, Graph, path + ".consequences.ownedEffectSources", Graph.Entries.Length, issues);
        if (issues.Count == 0)
        {
            var detached = WoundPersistedConsequenceEnvelopeAdapter.AdaptWorkingGraph(Graph, path + ".consequences");
            issues.AddRange(WoundPersistedConsequenceEnvelopeAdapter.ValidateDetached(Scalars.Severity.Rank,
                path + ".consequences", detached.Definitions, detached.Roots, true, Graph.Entries.Length).Issues);
        }
        return issues.ToImmutableArray();
    }
}
