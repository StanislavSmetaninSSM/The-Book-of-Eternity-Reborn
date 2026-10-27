using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class WoundResponseInputComposer
{
    // Reuses the existing closed consequenceDefinitions language. The caller supplies all wound/scalar identity.
    internal static WoundWorkingOwnedGraph ParseSpiritualHealingGraph(JsonElement definitions,
        WoundMaterializationEnvelope wound, int rank, string stageRef,
        IReadOnlyDictionary<string, string> complicationRefs)
    {
        if (definitions.ValueKind != JsonValueKind.Array || definitions.GetArrayLength() > WoundMaterializationContract.MaxOwnedEffectDefinitions)
            throw new InvalidDataException("Spiritual healing requires bounded consequenceDefinitions.");
        var issues = new List<ValidationIssue>();
        var complications = complicationRefs.ToDictionary(p => p.Key, p => new ParsedComplicationProposal(p.Key,
            p.Value, new JsonObject()), StringComparer.Ordinal);
        var parsed = ParseDefinitions(definitions.EnumerateArray().Select(e => e.Clone()).ToArray(),
            "spiritualHealingGraphs.consequenceDefinitions", wound.WoundId, stageRef, complications, issues);
        if (issues.Count != 0) throw new InvalidDataException(string.Join(";", issues.Select(i => i.Code + ":" + i.FilePath)));
        var address = new WoundWorkingOperationAddress(0, rank);
        WoundWorkingReference Ref(string value) => new(WoundWorkingReferenceOrigin.DirectAddition, address, value);
        var roots = parsed.Where(p => p.Root is not null).Select(p =>
            new WoundWorkingRoot(Ref(p.Root!.LocalApplicationRef), p.Definition["definitionKey"]!.GetValue<string>())).ToImmutableArray();
        var entries = parsed.SelectMany(p => p.Slots.Select(s => new WoundWorkingEntry(s.Slot, s.ProfileKey,
            Ref(s.LocalApplicationRef), s.ReadableSummary))).ToImmutableArray();
        var graph = new WoundWorkingOwnedGraph(wound.Complications.Select(c => new WoundWorkingComplication(
            WoundWorkingReference.Existing(c.ComplicationId), c.Kind, c.State, c.DisplayName, c.TreatmentDifficultyModifier,
            parsed.Where(p => p.Root?.OwnershipDomain == WoundRootOwnershipDomain.ForComplication(c.ComplicationId))
                .Select(p => Ref(p.Root!.LocalApplicationRef)).ToImmutableArray(), c.Visibility)).ToImmutableArray(),
            parsed.Select(p => new WoundWorkingDefinition(Ref(p.LocalEffectRef), JsonSerializer.SerializeToElement(
                BindDefinitionToLocalWound(p.Definition, wound.WoundId)))).ToImmutableArray(), roots, entries);
        var severity = wound.Severity with { Rank = rank, Value = new[] { "", "I", "II", "III", "IV" }[rank] };
        var context = new WoundWorkingGraphContext(wound.WoundId, wound.Lifecycle, wound.Owner, wound.Classification,
            severity, wound.Care, wound.Recovery with { CurrentStepThreshold = 2 * rank }, rank, entries.Length);
        WoundMaterializationContract.ValidateWorkingGraphBoundary(context, graph, "spiritualHealingGraphs", issues);
        WoundMaterializationContract.ValidateCommonOwnedDefinitions(JsonSerializer.SerializeToElement(
            graph.Definitions.Select(d => d.Definition).ToArray()), "spiritualHealingGraphs.definitions", wound.Owner.Realm, issues);
        WoundMaterializationContract.ValidateOwnedGraph(context, graph, "spiritualHealingGraphs", entries.Length, issues);
        if (issues.Count != 0) throw new InvalidDataException(string.Join(";", issues.Select(i => i.Code + ":" + i.FilePath)));
        return graph;
    }

    internal static WoundWorkingOwnedGraph BindSpiritualHealingGraphPredecessors(WoundWorkingOwnedGraph graph,
        WoundMaterializationEnvelope before)
    {
        var prior = WoundAcceptedTurnPlannerCore.BuildPriorRootCoordinateMap(before);
        var references = graph.Roots.ToDictionary(r => r.Reference, r =>
        {
            var complication = graph.Complications.SingleOrDefault(c => c.OwnedRoots.Contains(r.Reference));
            var domain = complication is null ? WoundRootOwnershipDomain.BaseWound :
                WoundRootOwnershipDomain.ForComplication(complication.Reference.Value);
            return prior.TryGetValue((r.DefinitionKey, domain.Kind, domain.ComplicationId), out var id)
                ? WoundWorkingReference.Existing(id) : r.Reference;
        });
        return graph with
        {
            Roots = graph.Roots.Select(r => r with { Reference = references[r.Reference] }).ToImmutableArray(),
            Entries = graph.Entries.Select(e => e with { Root = references[e.Root] }).ToImmutableArray(),
            Complications = graph.Complications.Select(c => c with
            { OwnedRoots = c.OwnedRoots.Select(r => references[r]).ToImmutableArray() }).ToImmutableArray()
        };
    }
}
