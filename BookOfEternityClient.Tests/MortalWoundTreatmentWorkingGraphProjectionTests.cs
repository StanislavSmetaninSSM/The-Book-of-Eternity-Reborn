using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalWoundTreatmentWorkingGraphProjectionTests
{
    [Theory]
    [InlineData(3, 3, "i,r", true)]
    [InlineData(3, 3, "r,i", false)]
    [InlineData(2, 1, "r,i", true)]
    [InlineData(2, 2, "r,i", false)]
    [InlineData(2, 2, "i,r", true)]
    [InlineData(2, 1, "i,r", false)]
    public void PolicyProjection_FinalUnchangedRankPreservesOriginalBudget(int rank, int budget, string sequence, bool expected)
    {
        var source = WoundContractTestData.CreateActiveWound();
        source["severity"]!["rank"] = rank;
        source["severity"]!["value"] = rank == 2 ? "II" : "III";
        source["consequences"]!["slotBudget"] = budget;
        source["consequences"]!["slotsUsed"] = 1;
        source["consequences"]!["entries"] = new JsonArray(source["consequences"]!["entries"]![0]!.DeepClone());
        var owned = source["consequences"]!["ownedEffectSources"]!;
        owned["definitions"] = new JsonArray(owned["definitions"]![0]!.DeepClone());
        owned["rootBindings"] = new JsonArray(owned["rootBindings"]![0]!.DeepClone());
        var before = Parse(source);
        var operations = sequence.Split(',').Select(token => token == "i"
            ? (MortalWoundTreatmentOperation)new MortalWoundApplyDeteriorationOperation("policy")
            : new MortalWoundReduceSeverityOperation(1)).ToImmutableArray();
        var result = MortalWoundTreatmentWorkingWoundSimulator.SimulateGraph(before, new[] { operations },
            (working, address, _) => MortalWoundTreatmentPolicyGraphProjection.Project(working, address,
                MortalWoundDeteriorationResultKind.IncreaseSeverity, null));
        Assert.Equal(expected, result.IsApplicable);
        if (expected)
        {
            Assert.Equal(rank, result.WorkingGraph!.Scalars.Severity.Rank);
            Assert.Equal(budget, result.WorkingGraph.Scalars.SlotBudget);
            Assert.Equal(before.Consequences.OwnedEffectSources.RootBindings.Select(row => row.EffectId),
                result.WorkingGraph.Graph.Roots.Select(row => row.Reference.Value));
        }
    }

    [Fact]
    public void PolicyProjection_IncreasePreservesBudgetCareAndRecovery()
    {
        var before = MortalWoundTreatmentWorkingGraphProjection.FromCanonical(ReadCanonicalWound())!;
        var result = MortalWoundTreatmentPolicyGraphProjection.Project(before, new(0, 0),
            MortalWoundDeteriorationResultKind.IncreaseSeverity, null);
        Assert.True(result.IsApplicable);
        Assert.False(result.Improved);
        Assert.Equal(before.Scalars.Severity.Rank + 1, result.After!.Scalars.Severity.Rank);
        Assert.Equal(before.Scalars.SlotBudget, result.After.Scalars.SlotBudget);
        Assert.Equal(before.Scalars.Care, result.After.Scalars.Care);
        Assert.Equal(before.Scalars.Recovery, result.After.Scalars.Recovery);
    }

    [Theory]
    [InlineData(4, "IncreaseSeverity", false)]
    [InlineData(2, "IncreaseSeverity", true)]
    [InlineData(2, "AddComplication", false)]
    [InlineData(2, "DeathContour", false)]
    public void PolicyProjection_InvalidTypedPacketRejects(int rank, string kind, bool draft)
    {
        var source = WoundContractTestData.CreateActiveWound();
        source["severity"]!["rank"] = rank;
        source["severity"]!["value"] = rank == 4 ? "IV" : "II";
        var before = MortalWoundTreatmentWorkingGraphProjection.FromCanonical(Parse(source))!;
        Assert.False(MortalWoundTreatmentPolicyGraphProjection.Project(before, new(0, 0), Enum.Parse<MortalWoundDeteriorationResultKind>(kind),
            draft ? Draft("policy") : null).IsApplicable);
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    public void GraphProjection_FinalSameRankRootContinuityRejectsReusedRemovedKey(bool changedBody, bool reduce, bool freshKey, bool expected)
    {
        var before = ReadRankThreeWound();
        var rootId = Assert.Single(before.Complications, row => row.ComplicationId == "old_complication").OwnedEffectIds[0];
        var key = before.Consequences.OwnedEffectSources.RootBindings.Single(row => row.EffectId == rootId).DefinitionKey;
        var body = JsonNode.Parse(before.Consequences.OwnedEffectSources.Definitions.Single(row => row.GetProperty("definitionKey").GetString() == key).GetRawText())!.AsObject();
        body["links"] = new JsonArray();
        if (freshKey) body["definitionKey"] = key + "_fresh";
        if (changedBody) body["display"]!["name"] = "Changed reused source";
        var entries = before.Consequences.Entries.Where(row => row.EffectId == rootId).ToArray();
        var draft = new MortalWoundComplicationProposalDraft(new("replacement", "pain", "active", "Replacement", 1, "known_to_player"),
            ImmutableArray.Create(new WoundConsequenceDefinitionProposalDraft("replacement_root", JsonSerializer.SerializeToElement(body),
                new("complication", "replacement", entries.Select(row => new WoundConsequenceSlotProposalDraft(row.ProfileKey, row.ReadableSummary)).ToImmutableArray()))));
        var operations = ImmutableArray.Create<MortalWoundTreatmentOperation>(new MortalWoundRemoveComplicationOperation("old_complication"),
            new MortalWoundAddComplicationOperation(draft));
        if (reduce) operations = operations.Add(new MortalWoundReduceSeverityOperation(1));
        Assert.Equal(expected, MortalWoundTreatmentWorkingWoundSimulator.SimulateGraph(before, new[] { operations }, Append).IsApplicable);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GraphProjection_SharedContinuityChecksReappearingNonRootBody(bool changed)
    {
        var fact = new WoundOwnedEffectDefinitionFact("child", "canonical child", ImmutableArray<string>.Empty, false);
        var emptyRoots = new Dictionary<string, WoundWorkingReference>();
        var comparison = WoundSameRankOwnedSourceContinuity.Compare(emptyRoots, emptyRoots,
            new Dictionary<string, WoundOwnedEffectDefinitionFact> { ["child"] = fact },
            new Dictionary<string, WoundOwnedEffectDefinitionFact> { ["child"] = fact with { CanonicalJson = changed ? "changed child" : fact.CanonicalJson } });
        Assert.Null(comparison.RootRebinding);
        Assert.Equal(changed ? "child" : null, comparison.ChangedDefinitionKey);
    }
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void GraphProjection_RetainedReactionDraftMatchesCanonicalReduction(bool retainedPolicy, bool reaction)
    {
        var before = RetainedDraftWound(retainedPolicy, reaction);
        var canonical = MortalWoundTreatmentSeverityReductionPlanner.Project(before, 1, before.Severity.LastChangeEventRef);
        Assert.Equal(!reaction, canonical.IsValid);
        if (retainedPolicy && reaction)
            Assert.Contains(canonical.Issues, issue => IsRetainedRankIssue(issue,
                "mortalWoundTreatment.severityReductionProjection.provisionalAfter.recovery.deteriorationPolicy.result.complicationDraft.consequenceDefinitions[0].definition.components[0].payload.definitionKey",
                "mortal_wound_deterioration_policy_invalid"));
        var operations = new[] { ImmutableArray.Create<MortalWoundTreatmentOperation>(new MortalWoundReduceSeverityOperation(1)) };
        Assert.Equal(!reaction, MortalWoundTreatmentWorkingWoundSimulator.Simulate(before, operations).IsApplicable);
        var graph = MortalWoundTreatmentWorkingGraphProjection.FromCanonical(before)!;
        var destination = graph.WithScalars(graph.Scalars with
        {
            Severity = graph.Scalars.Severity with { Rank = 2, Value = "II" }, SlotBudget = 2
        });
        var issues = destination.ValidateGraph("retainedGraph");
        if (reaction)
        {
            var path = retainedPolicy
                ? "retainedGraph.recovery.deteriorationPolicy.result.complicationDraft.consequenceDefinitions[0].definition.components[0].payload.definitionKey"
                : "treatmentAttempt.workingWound.treatment.routes[0].outcomes[2].result[0].complicationDraft.consequenceDefinitions[0].definition.components[0].payload.definitionKey";
            Assert.Contains(issues, issue => IsRetainedRankIssue(issue, path,
                retainedPolicy ? "mortal_wound_deterioration_policy_invalid" : "wound_materialization_invalid_field"));
        }
        else Assert.Empty(issues);
        Assert.Equal(!reaction, MortalWoundTreatmentWorkingWoundSimulator.SimulateGraph(before, operations, Append).IsApplicable);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GraphProjection_SymbolicAdditionDoesNotSkipRetainedRankValidation(bool retainedPolicy)
    {
        var before = RetainedDraftWound(retainedPolicy, true);
        var graph = MortalWoundTreatmentWorkingGraphProjection.FromCanonical(before)!;
        Assert.True(graph.TryAppendComplication(Draft("unresolved"), WoundWorkingReferenceOrigin.DirectAddition, new(0, 0), out var added));
        Assert.False(added!.TryExportExisting(out _));
        var lowered = added.WithScalars(added.Scalars with
        {
            Severity = added.Scalars.Severity with { Rank = 2, Value = "II" }, SlotBudget = 2
        });
        Assert.Contains(lowered.ValidateGraph("symbolicGraph"), issue =>
            issue.Expected == "wound-owned apply_definition only at severity III or IV" && issue.Actual == "2");
        var operations = ImmutableArray.Create<MortalWoundTreatmentOperation>(
            new MortalWoundAddComplicationOperation(Draft("unresolved")), new MortalWoundReduceSeverityOperation(1));
        Assert.False(MortalWoundTreatmentWorkingWoundSimulator.SimulateGraph(before, new[] { operations }, Append).IsApplicable);
    }

    [Fact]
    public void GraphProjection_RemovalPreservesCanonicalDiagnosisValidation()
    {
        var source = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(ReadRankThreeWound()))!.AsObject();
        source["complications"]![0]!["ownedEffectIds"] = new JsonArray();
        source["treatment"]!["diagnosisPaths"] = new JsonArray(new JsonObject
        {
            ["diagnosisPathId"] = "retained_complication_fact", ["displayName"] = "Проверить известное осложнение",
            ["visibility"] = "known_to_player", ["requiresKnownFacts"] = new JsonArray("complication:old_complication"),
            ["requirements"] = new JsonArray(), ["check"] = new JsonObject(),
            ["reveals"] = new JsonArray("complication:old_complication"), ["failurePolicy"] = "no_reveal"
        });
        var before = Parse(source);
        var rejectedExport = Assert.Throws<InvalidOperationException>(() =>
            MortalWoundTreatmentWorkingWoundSimulator.TryRemoveComplication(before, "old_complication", out _));
        Assert.Equal("Cannot serialize an invalid Mortal wound treatment projection.", rejectedExport.Message);
        var operations = new[] { ImmutableArray.Create<MortalWoundTreatmentOperation>(new MortalWoundRemoveComplicationOperation("old_complication")) };
        Assert.False(MortalWoundTreatmentWorkingWoundSimulator.Simulate(before, operations).IsApplicable);
        var canonicalRemoved = source.DeepClone().AsObject();
        canonicalRemoved["complications"] = new JsonArray();
        Assert.Contains(WoundMaterializationContract.Parse(canonicalRemoved.ToJsonString(), "removed").Issues,
            issue => issue.Code == "wound_treatment_diagnosis_fact_unknown" &&
                issue.FilePath == "removed.treatment.diagnosisPaths[0].requiresKnownFacts[0]");
        var graph = MortalWoundTreatmentWorkingGraphProjection.FromCanonical(before)!;
        Assert.True(graph.TryAppendComplication(Draft("seed_retained"), WoundWorkingReferenceOrigin.DirectAddition, new(0, 0), out _));
        Assert.True(graph.TryRemoveExistingComplication("old_complication", out var removed));
        Assert.Contains(removed!.ValidateGraph("removed"), issue => issue.Code == "wound_treatment_diagnosis_fact_unknown");
        Assert.False(MortalWoundTreatmentWorkingWoundSimulator.SimulateGraph(before, operations, Append).IsApplicable);
        var replacement = WoundResponseInputComposer.ConvertTreatmentComplicationGraph(Draft("old_complication"),
            before.WoundId, WoundWorkingReferenceOrigin.DirectAddition, new(0, 1));
        var symbolicReplacement = removed.WithGraph(removed.Graph with
        {
            Complications = removed.Graph.Complications.AddRange(replacement.Complications)
        });
        Assert.Contains(symbolicReplacement.ValidateGraph("symbolicReplacement"),
            issue => issue.Code == "wound_treatment_diagnosis_fact_unknown");
        source["treatment"]!["diagnosisPaths"] = new JsonArray();
        var unreferenced = Parse(source);
        Assert.True(MortalWoundTreatmentWorkingWoundSimulator.Simulate(unreferenced, operations).IsApplicable);
        Assert.True(MortalWoundTreatmentWorkingWoundSimulator.SimulateGraph(unreferenced, operations, Append).IsApplicable);
    }

    private static bool IsRetainedRankIssue(ValidationIssue issue, string path, string code) =>
        issue.FilePath == path && issue.Code == code &&
        issue.Expected == "wound-owned apply_definition only at severity III or IV" && issue.Actual == "2";

    private static WoundMaterializationEnvelope RetainedDraftWound(bool retainedPolicy, bool reaction)
    {
        var source = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(ReadRankThreeWound()))!.AsObject();
        var definitions = WoundContractTestData.CreateRootBoundReactionComplicationDefinitions("replace");
        if (!reaction)
        {
            var single = definitions[1]!.DeepClone().AsObject();
            single["definition"]!["stacking"]!["policy"] = "independent";
            definitions = new JsonArray(single);
        }
        var addition = new JsonObject
        {
            ["kind"] = "add_complication", ["complicationDraft"] = new JsonObject
            {
                ["complications"] = new JsonArray(new JsonObject
                {
                    ["complicationRef"] = "irritation", ["kind"] = "pain", ["state"] = "active",
                    ["displayName"] = "Раздражение раны", ["treatmentDifficultyModifier"] = 1, ["visibility"] = "known_to_player"
                }),
                ["consequenceDefinitions"] = definitions
            }
        };
        if (retainedPolicy)
            source["recovery"]!["deteriorationPolicy"] = new JsonObject
            {
                ["policyRef"] = "untreated_infection", ["unmetConditions"] = new JsonArray("not_stabilized"),
                ["graceMinutes"] = 30L, ["cadenceMinutes"] = 10L, ["result"] = addition
            };
        else source["treatment"]!["routes"]![0]!["outcomes"]![2]!["result"] = new JsonArray(addition);
        return Parse(source);
    }

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
