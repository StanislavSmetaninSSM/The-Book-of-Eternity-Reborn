using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalWoundTreatmentWorkingGraphProjectionTests
{
    [Fact]
    public void GraphProjection_CanonicalImportExportIsExactAndDetached()
    {
        var original = ReadCanonicalWound();
        var symptoms = original.Display.VisibleSymptoms.ToList();
        var borrowed = original with { Display = original.Display with { VisibleSymptoms = symptoms } };
        var expected = WoundMaterializationContract.SerializeCanonical(borrowed);
        var graph = Assert.IsType<MortalWoundTreatmentWorkingGraphProjection>(
            MortalWoundTreatmentWorkingGraphProjection.FromCanonical(borrowed));
        symptoms.Add("caller changed the borrowed list");
        Assert.True(graph.TryExportExisting(out var exported));
        Assert.Equal(expected, WoundMaterializationContract.SerializeCanonical(exported!));
        var blockers = graph.Scalars.Recovery.Blockers;
        Assert.IsType<ImmutableArray<string>>(blockers);
        Assert.All(graph.Graph.Roots, root => Assert.Equal(new WoundWorkingOperationAddress(-1, -1), root.Reference.Address));
        var forgedExisting = graph.Graph.Roots[0] with
        {
            Reference = graph.Graph.Roots[0].Reference with { Address = new(0, 0) }
        };
        Assert.False(graph.WithGraph(graph.Graph with { Roots = graph.Graph.Roots.SetItem(0, forgedExisting) })
            .TryExportExisting(out _));
    }

    [Fact]
    public void GraphProjection_UnresolvedGraphCannotExportCanonicalCoordinates()
    {
        var graph = MortalWoundTreatmentWorkingGraphProjection.FromCanonical(ReadCanonicalWound())!;
        Assert.True(graph.TryAppendComplication(Draft("new_complication"), WoundWorkingReferenceOrigin.DirectAddition,
            new WoundWorkingOperationAddress(0, 0), out var changed));
        Assert.Empty(changed!.ValidateGraph("workingGraph.after"));
        Assert.Equal(graph.Graph.Complications.Length + 1, changed.Graph.Complications.Length);
        Assert.False(changed.TryExportExisting(out var exported));
        Assert.Null(exported);
    }

    [Theory]
    [InlineData("remove,reduce,add", true, 2)]
    [InlineData("add,reduce,remove", false, 0)]
    [InlineData("reduce,remove,add", true, 2)]
    [InlineData("remove,add,reduce", true, 2)]
    [InlineData("reduce,add,remove", false, 0)]
    [InlineData("remove,reduce,add", true, 2, true)]
    [InlineData("reduce,remove,add", false, 0, true)]
    public void GraphProjection_OrderedAddRemoveReduceUsesOneCurrentGraph(string order, bool applicable, int slots, bool child = false)
    {
        var before = ReadRankThreeWound(child);
        var operations = order.Split(',').Select(token => token switch
        {
            "remove" => (MortalWoundTreatmentOperation)new MortalWoundRemoveComplicationOperation("old_complication"),
            "reduce" => new MortalWoundReduceSeverityOperation(1),
            "add" => new MortalWoundAddComplicationOperation(Draft("new", "new_definition")),
            _ => throw new InvalidOperationException()
        }).ToImmutableArray();
        var result = MortalWoundTreatmentWorkingWoundSimulator.SimulateGraph(before, new[] { operations }, Append);
        Assert.Equal(applicable, result.IsApplicable);
        if (!applicable) { Assert.Null(result.WorkingGraph); return; }
        Assert.Equal(slots, result.WorkingGraph!.Graph.Entries.Length);
        Assert.Equal(2, result.WorkingGraph.Scalars.Severity.Rank);
        Assert.Equal(2, result.WorkingGraph.Scalars.SlotBudget);
        Assert.Single(result.WorkingGraph.Graph.Complications);
        Assert.DoesNotContain(result.WorkingGraph.Graph.Roots, root => root.Reference.Value == "effect_wound_test_pain");
        if (child)
            Assert.DoesNotContain(result.WorkingGraph.Graph.Definitions, definition =>
                definition.Definition.GetProperty("definitionKey").GetString() == "old_child_definition");
        Assert.True(result.Improved);
    }

    [Theory]
    [InlineData("same", "different", false, true)]
    [InlineData("same", "different", true, true)]
    [InlineData("same", "same", true, false)]
    [InlineData("same", "sаme", true, false)]
    public void GraphProjection_ReferenceNamespacesDoNotHideSemanticKeyCollisions(
        string firstKey, string secondKey, bool policy, bool expected)
    {
        var graph = MortalWoundTreatmentWorkingGraphProjection.FromCanonical(ReadRankThreeWound())!;
        Assert.True(graph.TryRemoveExistingComplication("old_complication", out graph));
        Assert.True(graph!.TryAppendComplication(Draft("local", firstKey), WoundWorkingReferenceOrigin.DirectAddition,
            new WoundWorkingOperationAddress(0, 0), out graph));
        var origin = policy ? WoundWorkingReferenceOrigin.PolicyAddition : WoundWorkingReferenceOrigin.DirectAddition;
        var resultIndex = policy ? 0 : 1;
        var accepted = graph!.TryAppendComplication(Draft("local", secondKey), origin,
            new WoundWorkingOperationAddress(resultIndex, 1), out var after);
        var fragment = WoundResponseInputComposer.ConvertTreatmentComplicationGraph(Draft("local", secondKey),
            ReadRankThreeWound().WoundId, origin, new(resultIndex, 1));
        var union = graph.WithGraph(new(graph.Graph.Complications.AddRange(fragment.Complications),
            graph.Graph.Definitions.AddRange(fragment.Definitions), graph.Graph.Roots.AddRange(fragment.Roots),
            graph.Graph.Entries.AddRange(fragment.Entries.Select(entry => entry with { Slot = entry.Slot + graph.Graph.Entries.Length }))));
        Assert.True(expected == accepted, string.Join(" | ", union.ValidateGraph("union").Select(issue =>
            $"{issue.Code}: {issue.FilePath}: {issue.Expected}: {issue.Actual}")));
        if (expected) Assert.Equal(3, after!.Graph.Entries.Length);
        else Assert.Null(after);
        Assert.False(graph.TryAppendComplication(Draft("local"), WoundWorkingReferenceOrigin.DirectAddition,
            new WoundWorkingOperationAddress(0, 2), out _));
        Assert.False(graph.TryAppendComplication(Draft("lоcal"), WoundWorkingReferenceOrigin.DirectAddition,
            new WoundWorkingOperationAddress(0, 2), out _));
    }

    [Theory]
    [InlineData(16, true)]
    [InlineData(17, false)]
    public void GraphProjection_WholeGraphCapsAndOwnershipMatchCanonicalRules(int complications, bool expected)
    {
        var graph = MortalWoundTreatmentWorkingGraphProjection.FromCanonical(ReadCanonicalWound())!;
        for (var index = 0; index < complications; index++)
        {
            var applicable = graph.TryAppendComplication(Draft($"added_{index}"), WoundWorkingReferenceOrigin.DirectAddition,
                new WoundWorkingOperationAddress(index, 0), out var after);
            if (index == complications - 1) Assert.Equal(expected, applicable);
            if (applicable) graph = after!;
        }
        var stolen = graph.Graph.Complications[0] with { OwnedRoots = ImmutableArray.Create(graph.Graph.Roots[0].Reference) };
        var duplicateOwner = graph.Graph.Complications[1] with { OwnedRoots = stolen.OwnedRoots };
        var invalid = graph.WithGraph(graph.Graph with
        {
            Complications = graph.Graph.Complications.SetItem(0, stolen).SetItem(1, duplicateOwner)
        });
        Assert.Contains(invalid.ValidateGraph("graph"), issue => issue.Code == "wound_materialization_effect_binding_invalid");
        Assert.NotEmpty(graph.WithScalars(graph.Scalars with { SlotBudget = 1 }).ValidateGraph("graph"));
        foreach (var malformed in new[]
                 {
                     graph.WithGraph(graph.Graph with { Definitions = Enumerable.Repeat(graph.Graph.Definitions[0], 6).ToImmutableArray() }),
                     graph.WithGraph(graph.Graph with { Roots = Enumerable.Repeat(graph.Graph.Roots[0], 6).ToImmutableArray() }),
                     graph.WithGraph(graph.Graph with { Entries = Enumerable.Repeat(graph.Graph.Entries[0], 5).ToImmutableArray() }),
                     graph.WithScalars(graph.Scalars with { SlotBudget = 5 }),
                     graph.WithScalars(graph.Scalars with { Severity = graph.Scalars.Severity with { Rank = 0 } })
                 })
            Assert.NotEmpty(malformed.ValidateGraph("bounded"));

        var fourSlots = WoundContractTestData.CreateActiveWound();
        var woundId = fourSlots["woundId"]!.GetValue<string>();
        fourSlots["severity"]!["rank"] = 4;
        fourSlots["severity"]!["value"] = "IV";
        fourSlots["severity"]!["maximumAtCreation"] = "IV";
        fourSlots["consequences"]!["slotBudget"] = 4;
        fourSlots["consequences"]!["slotsUsed"] = 4;
        var profiles = new[] { "action_control", "resistance_modifier", "periodic_damage", "characteristic_modifier", "wound_consequence" };
        fourSlots["consequences"]!["ownedEffectSources"] = WoundContractTestData.CreateOwnedEffectSourcesForTarget(woundId,
            "mortal_world", "player", profiles.Select((profile, index) => ($"root_{index}", $"definition_{index}", profile)).ToArray());
        fourSlots["consequences"]!["entries"] = new JsonArray(profiles.Take(4).Select((profile, index) => (JsonNode)new JsonObject
        {
            ["slot"] = index + 1, ["profileKey"] = profile, ["effectId"] = $"root_{index}", ["readableSummary"] = "Последствие раны."
        }).ToArray());
        var boundary = MortalWoundTreatmentWorkingGraphProjection.FromCanonical(Parse(fourSlots))!;
        Assert.Equal(5, boundary.Graph.Definitions.Length);
        Assert.Equal(5, boundary.Graph.Roots.Length);
        Assert.Equal(4, boundary.Graph.Entries.Length);
        Assert.Empty(boundary.ValidateGraph("complete_boundary"));
        var marker = Draft("marker_complication") with
        {
            ConsequenceDefinitions = ImmutableArray.Create(new WoundConsequenceDefinitionProposalDraft("new_marker",
                MarkerProposal(), new WoundConsequenceRootProposalDraft("complication", "marker_complication",
                    ImmutableArray<WoundConsequenceSlotProposalDraft>.Empty)))
        };
        Assert.False(boundary.TryAppendComplication(marker, WoundWorkingReferenceOrigin.DirectAddition, new(0, 0), out _));

        var small = MortalWoundTreatmentWorkingGraphProjection.FromCanonical(ReadCanonicalWound())!;
        var blankSummary = Draft("bad_summary", "bad_summary_definition");
        blankSummary = blankSummary with { ConsequenceDefinitions = blankSummary.ConsequenceDefinitions.Select(definition => definition with
        {
            Root = definition.Root! with { Slots = ImmutableArray.Create(new WoundConsequenceSlotProposalDraft("action_control", "")) }
        }).ToImmutableArray() };
        var room = MortalWoundTreatmentWorkingGraphProjection.FromCanonical(ReadRankThreeWound())!;
        Assert.True(room.TryRemoveExistingComplication("old_complication", out room));
        Assert.False(room!.TryAppendComplication(blankSummary, WoundWorkingReferenceOrigin.DirectAddition, new(0, 0), out _));
        Assert.True(small.TryAppendComplication(marker, WoundWorkingReferenceOrigin.DirectAddition, new(0, 0), out var oneMarker));
        var secondMarkerDefinition = JsonNode.Parse(MarkerProposal().GetRawText())!.AsObject();
        secondMarkerDefinition["definitionKey"] = "second_marker_definition";
        secondMarkerDefinition["stacking"]!["stackKey"] = "second_marker_stack";
        var secondMarker = marker with { Complication = marker.Complication with { ComplicationRef = "second_marker" },
            ConsequenceDefinitions = marker.ConsequenceDefinitions.Select(definition => definition with
            {
                DefinitionRef = "second_marker_definition", Definition = JsonSerializer.SerializeToElement(secondMarkerDefinition),
                Root = definition.Root! with { ComplicationRef = "second_marker" }
            }).ToImmutableArray() };
        Assert.False(oneMarker!.TryAppendComplication(secondMarker, WoundWorkingReferenceOrigin.DirectAddition, new(0, 1), out _));
        var added = WoundResponseInputComposer.ConvertTreatmentComplicationGraph(secondMarker, woundId,
            WoundWorkingReferenceOrigin.DirectAddition, new(0, 1));
        var duplicateMarkerGraph = oneMarker.WithGraph(oneMarker.Graph with
        {
            Complications = oneMarker.Graph.Complications.AddRange(added.Complications),
            Definitions = oneMarker.Graph.Definitions.AddRange(added.Definitions), Roots = oneMarker.Graph.Roots.AddRange(added.Roots)
        });
        Assert.Contains(duplicateMarkerGraph.ValidateGraph("marker_union"), issue =>
            issue.Expected == "at most one wound_consequence marker in the complete graph");
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void GraphProjection_ReductionMatchesCanonicalProjectWithoutPruning(int steps, bool expected)
    {
        var before = ReadRankThreeWound();
        var canonical = MortalWoundTreatmentSeverityReductionPlanner.Project(before, steps, before.Severity.LastChangeEventRef);
        var graph = MortalWoundTreatmentWorkingWoundSimulator.SimulateGraph(before,
            new[] { ImmutableArray.Create<MortalWoundTreatmentOperation>(new MortalWoundReduceSeverityOperation(steps)) }, Append);
        Assert.Equal(expected, canonical.IsValid);
        Assert.Equal(canonical.IsValid, graph.IsApplicable);
        if (!expected) return;
        Assert.True(graph.WorkingGraph!.TryExportExisting(out var after));
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(canonical.Projection!.ProvisionalAfter),
            WoundMaterializationContract.SerializeCanonical(after!));
    }

    [Fact]
    public void GraphProjection_CanonicalFacadeRetainsPreparedCallbackContract()
    {
        var before = ReadCanonicalWound();
        var results = new[] { ImmutableArray.Create<MortalWoundTreatmentOperation>(new MortalWoundApplyDeteriorationOperation("policy")) };
        Assert.False(MortalWoundTreatmentWorkingWoundSimulator.Simulate(before, results).IsApplicable);
        var calls = 0;
        var expected = before with { Severity = before.Severity with { Rank = 3, Value = "III" } };
        var simulation = MortalWoundTreatmentWorkingWoundSimulator.Simulate(before, results, (working, operation) =>
        {
            calls++;
            Assert.Equal(WoundMaterializationContract.SerializeCanonical(before), WoundMaterializationContract.SerializeCanonical(working));
            Assert.IsType<MortalWoundApplyDeteriorationOperation>(operation);
            return new MortalWoundTreatmentPreparedOperationResult(true, false, expected);
        });
        Assert.True(simulation.IsApplicable);
        Assert.Equal(1, calls);
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(expected), WoundMaterializationContract.SerializeCanonical(simulation.WorkingWound!));
        Assert.False(simulation.Improved);
    }

    private static MortalWoundTreatmentPreparedGraphOperationResult Append(MortalWoundTreatmentWorkingGraphProjection before,
        WoundWorkingOperationAddress address, MortalWoundTreatmentOperation operation)
    {
        var applicable = before.TryAppendComplication(((MortalWoundAddComplicationOperation)operation).ComplicationDraft,
            WoundWorkingReferenceOrigin.DirectAddition, address, out var after);
        return new(applicable, false, after);
    }

    private static MortalWoundComplicationProposalDraft Draft(string localRef, string? key = null)
    {
        var definitions = ImmutableArray<WoundConsequenceDefinitionProposalDraft>.Empty;
        if (key is not null)
        {
            var source = WoundContractTestData.CreateActiveWound();
            var definition = source["consequences"]!["ownedEffectSources"]!["definitions"]![1]!.DeepClone().AsObject();
            definition["definitionKey"] = key;
            definition["stacking"]!["stackKey"] = "stack_" + key;
            definition["links"] = new JsonArray();
            definition["components"]![0]!["payload"]!["action"] = key == "different" ? "use_item" : "movement";
            definitions = ImmutableArray.Create(new WoundConsequenceDefinitionProposalDraft("definition_local",
                JsonSerializer.SerializeToElement(definition), new WoundConsequenceRootProposalDraft("complication", localRef,
                    ImmutableArray.Create(new WoundConsequenceSlotProposalDraft("action_control", "Ограничивает движения.")))));
        }
        return new(new WoundComplicationProposalDraft(localRef, "impairment", "active", "Натяжение края раны", 1,
            "known_to_player"), definitions);
    }

    private static JsonElement MarkerProposal()
    {
        var definition = WoundContractTestData.CreateOwnedEffectDefinition("wound_test_torn_side", "mortal_world",
            "new_marker_definition", "wound_consequence");
        definition["links"] = new JsonArray();
        definition["components"]![0]!["payload"]!.AsObject().Remove("woundId");
        return JsonSerializer.SerializeToElement(definition);
    }

    private static WoundMaterializationEnvelope ReadRankThreeWound(bool child = false)
    {
        var source = WoundContractTestData.CreateActiveWound();
        source["severity"]!["rank"] = 3;
        source["severity"]!["value"] = "III";
        source["severity"]!["maximumAtCreation"] = "III";
        source["consequences"]!["slotBudget"] = 3;
        source["complications"] = new JsonArray(new JsonObject
        {
            ["complicationId"] = "old_complication", ["kind"] = "impairment", ["state"] = "active",
            ["displayName"] = "Старое осложнение", ["treatmentDifficultyModifier"] = 1,
            ["ownedEffectIds"] = new JsonArray("effect_wound_test_pain"), ["visibility"] = "known_to_player"
        });
        if (child)
        {
            var woundId = source["woundId"]!.GetValue<string>();
            var definitions = source["consequences"]!["ownedEffectSources"]!["definitions"]!.AsArray();
            definitions[1] = WoundContractTestData.CreateApplyDefinitionRoot(woundId, "mortal_world",
                "definition_wound_test_pain", "old_child_definition");
            definitions.Add(WoundContractTestData.CreateOwnedEffectDefinition(woundId, "mortal_world", "old_child_definition", "action_control"));
            source["consequences"]!["entries"]![1]!["profileKey"] = "event_reaction";
            source["consequences"]!["entries"]!.AsArray().Add(new JsonObject
            {
                ["slot"] = 3, ["profileKey"] = "action_control", ["effectId"] = "effect_wound_test_pain", ["readableSummary"] = "Дочернее последствие."
            });
            source["consequences"]!["slotsUsed"] = 3;
        }
        return Parse(source);
    }

    private static WoundMaterializationEnvelope ReadCanonicalWound() => Parse(WoundContractTestData.CreateActiveWound());
    private static WoundMaterializationEnvelope Parse(JsonObject source)
    {
        var parsed = WoundMaterializationContract.Parse(source.ToJsonString(), "workingGraph.before");
        Assert.True(parsed.IsValid, string.Join(" | ", parsed.Issues.Select(issue => issue.Code)));
        return Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
    }
}
