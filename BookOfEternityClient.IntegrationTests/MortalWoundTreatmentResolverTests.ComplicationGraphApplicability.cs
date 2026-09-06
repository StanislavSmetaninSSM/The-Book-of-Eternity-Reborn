using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ComplicationGraphApplicability_RetainedRankValidationRejectsBeforeDieClaim(bool retainedPolicy)
    {
        var scenario = CreateDestinationReductionScenario("retained_rank", "resistance_modifier");
        var addition = GraphEffectlessAddition("irritation");
        addition["complicationDraft"]!["complications"]![0]!["treatmentDifficultyModifier"] = 1;
        addition["complicationDraft"]!["consequenceDefinitions"] =
            WoundContractTestData.CreateRootBoundReactionComplicationDefinitions("replace");
        var route = scenario.Before["treatment"]!["routes"]![0]!.AsObject();
        if (retainedPolicy)
            scenario.Before["recovery"]!["deteriorationPolicy"] = new JsonObject
            {
                ["policyRef"] = "untreated_infection", ["unmetConditions"] = new JsonArray("not_stabilized"),
                ["graceMinutes"] = 30L, ["cadenceMinutes"] = 10L, ["result"] = addition
            };
        else route["outcomes"]![2]!["result"] = new JsonArray(addition);
        var legalRoute = route.DeepClone().AsObject();
        var legalRouteId = scenario.RouteId + "_legal";
        legalRoute["routeId"] = legalRouteId;
        legalRoute["outcomes"]![0]!["result"] = new JsonArray(new JsonObject
        {
            ["kind"] = "add_recovery", ["points"] = 1
        });
        scenario.Before["treatment"]!["routes"] = new JsonArray(route.DeepClone(), legalRoute);
        scenario.Before["treatment"]!["knownRouteIds"] = new JsonArray(scenario.RouteId, legalRouteId);
        var baseline = WoundMaterializationContract.Parse(scenario.Before.ToJsonString(), "retainedBaseline");
        Assert.True(baseline.IsValid, DescribeIssues(baseline.Issues));
        scenario = scenario with { History = CreateCurrentWoundHistory(scenario.Before) };
        using var fixture = AcceptedStateFixture.Create(scenario);
        var state = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(fixture.GetAcceptedState());
        var before = fixture.ReadCurrentWound();
        var history = fixture.ReadCurrentHistory();
        var unchanged = CaptureResolverFixtureTree(fixture.Root);
        var rejected = MortalWoundTreatmentPlanner.PrepareProcedureRequest(state, history, before,
            scenario.OperationKey + "_rejected", scenario.RouteId, fixture.AcceptedEventRef(state));
        Assert.False(rejected.IsValid);
        Assert.Null(rejected.Request);
        Assert.Contains(rejected.Issues, issue => issue.Code == "mortal_wound_treatment_procedure_band_inapplicable");
        AssertResolverFixtureTreeUnchanged(fixture.Root, unchanged);
        var legal = MortalWoundTreatmentPlanner.PrepareProcedureRequest(state, history, before,
            scenario.OperationKey + "_legal", legalRouteId, fixture.AcceptedEventRef(state));
        Assert.True(legal.IsValid, DescribeIssues(legal.Issues));
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(legal.Request);
        Assert.Equal(new[] { 0 }, Assert.IsType<MortalWoundProcedureCheckAuthority>(request.ModeAuthority).SourceIndices);
        AssertSingleHeldClaim(request, "sterile_thread");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ComplicationGraphApplicability_CumulativeOverflowRejectsBeforeDieClaim(bool fateShield)
    {
        var scenario = CreateScenario(fateShield ? "procedure_player_natural_one_reserves_oldest_fate_shield"
            : "procedure_normal_uses_lowest_free_die", "procedure");
        scenario.Before["complications"] = new JsonArray(Enumerable.Range(0, 15)
            .Select(index => (JsonNode)new JsonObject
            {
                ["complicationId"] = $"complication_existing_{index}",
                ["kind"] = "impairment", ["state"] = "active",
                ["displayName"] = "Существующее осложнение",
                ["treatmentDifficultyModifier"] = 0,
                ["ownedEffectIds"] = new JsonArray(), ["visibility"] = "known_to_player"
            }).ToArray());
        var route = scenario.Before["treatment"]!["routes"]![0]!.AsObject();
        var legalRoute = route.DeepClone().AsObject();
        var legalRouteId = scenario.RouteId + "_legal";
        legalRoute["routeId"] = legalRouteId;
        route["outcomes"]![2]!["result"] = new JsonArray(
            GraphEffectlessAddition("first_local"), GraphEffectlessAddition("second_local"));
        scenario.Before["treatment"]!["routes"] = new JsonArray(route.DeepClone(), legalRoute);
        scenario.Before["treatment"]!["knownRouteIds"] = new JsonArray(scenario.RouteId, legalRouteId);
        scenario = scenario with { History = CreateCurrentWoundHistory(scenario.Before) };

        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(fixture.GetAcceptedState());
        var before = fixture.ReadCurrentWound();
        var history = fixture.ReadCurrentHistory();
        var unchanged = CaptureResolverFixtureTree(fixture.Root);
        var rejected = MortalWoundTreatmentPlanner.PrepareProcedureRequest(acceptedState, history,
            before, scenario.OperationKey + "_rejected", scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));
        Assert.False(rejected.IsValid);
        Assert.Null(rejected.Request);
        Assert.Contains(rejected.Issues, issue => issue.Code == "mortal_wound_treatment_procedure_band_inapplicable");
        AssertResolverFixtureTreeUnchanged(fixture.Root, unchanged);
        var legal = MortalWoundTreatmentPlanner.PrepareProcedureRequest(acceptedState, history,
            before, scenario.OperationKey + "_legal", legalRouteId,
            fixture.AcceptedEventRef(acceptedState));
        Assert.True(legal.IsValid, DescribeIssues(legal.Issues));
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(legal.Request);
        var check = Assert.IsType<MortalWoundProcedureCheckAuthority>(request.ModeAuthority);
        Assert.Equal(new[] { 0 }, check.SourceIndices);
        AssertSingleHeldClaim(request, "sterile_thread");
        if (fateShield)
            Assert.Equal(scenario.ExpectedFateEffectId, check.PreparedCriticalReaction!.EffectId);
    }

    [Theory]
    [InlineData("remove,reduce,add", true)]
    [InlineData("add,reduce,remove", false)]
    [InlineData("reduce,remove,add", true)]
    [InlineData("reduce,add,remove", false)]
    public void ComplicationGraphApplicability_InterleavedRemoveReduceAddUsesAuthoredOrder(string order, bool expected)
    {
        var scenario = CreateDestinationReductionScenario("graph_order_" + order.Replace(',', '_'), "action_control", "resistance_modifier");
        scenario.Before["complications"] = new JsonArray(new JsonObject
        {
            ["complicationId"] = "old_graph_complication", ["kind"] = "impairment", ["state"] = "active",
            ["displayName"] = "Старое осложнение", ["treatmentDifficultyModifier"] = 0,
            ["ownedEffectIds"] = new JsonArray("effect_destination_2"), ["visibility"] = "known_to_player"
        });
        var operations = new JsonArray(order.Split(',').Select(token => (JsonNode)(token switch
        {
            "remove" => new JsonObject { ["kind"] = "remove_complication", ["complicationId"] = "old_graph_complication" },
            "reduce" => new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 },
            "add" => GraphEffectfulAddition("new_graph_complication", "new_graph_definition"),
            _ => throw new InvalidOperationException()
        })).ToArray());
        scenario.Before["treatment"]!["routes"]![0]!["outcomes"]![2]!["result"] = operations;
        scenario = scenario with { History = CreateCurrentWoundHistory(scenario.Before) };
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(fixture.GetAcceptedState());
        var request = MortalWoundTreatmentPlanner.PrepareProcedureRequest(acceptedState, fixture.ReadCurrentHistory(),
            fixture.ReadCurrentWound(), scenario.OperationKey, scenario.RouteId, fixture.AcceptedEventRef(acceptedState));
        Assert.True(expected == request.IsValid, DescribeIssues(request.Issues));
        if (expected)
            Assert.Equal(new[] { 0 }, Assert.IsType<MortalWoundProcedureCheckAuthority>(request.Request!.ModeAuthority).SourceIndices);
        else
        {
            Assert.Null(request.Request);
            Assert.Contains(request.Issues, issue => issue.Code == "mortal_wound_treatment_procedure_band_inapplicable");
        }
    }

    [Theory]
    [InlineData("add_complication", false, 4, 4, true)]
    [InlineData("add_complication", true, 4, 4, true)]
    [InlineData("add_complication", false, 3, 3, false)]
    [InlineData("add_complication", true, 3, 3, false)]
    [InlineData("increase_severity", false, 3, 3, true)]
    [InlineData("increase_severity", true, 3, 3, true)]
    [InlineData("death_contour", false, 4, 4, true)]
    [InlineData("death_contour", true, 4, 4, true)]
    [InlineData("collision", false, 4, 4, false)]
    [InlineData("collision", true, 4, 4, false)]
    [InlineData("baseline_gate", false, 3, 3, false)]
    [InlineData("selected_direct", false, 3, 3, true)]
    public void ComplicationGraphApplicability_PreservesTypedPolicyAndDeathSiblingBands(
        string kind, bool directFirst, int rank, int budget, bool expected)
    {
        var scenario = CreateDestinationReductionScenario("graph_policy_" + kind + "_" + directFirst,
            "action_control", "resistance_modifier");
        scenario.Before["severity"]!["rank"] = rank;
        scenario.Before["severity"]!["value"] = rank == 4 ? "IV" : "III";
        scenario.Before["severity"]!["maximumAtCreation"] = rank == 4 ? "IV" : "III";
        scenario.Before["consequences"]!["slotBudget"] = budget;
        var policyResult = kind is "add_complication" or "collision" or "baseline_gate"
            ? GraphEffectfulAddition("shared_local", "policy_graph_definition")
            : new JsonObject { ["kind"] = kind == "selected_direct" ? "increase_severity" : kind };
        scenario.Before["recovery"]!["deteriorationPolicy"] = new JsonObject
        {
            ["policyRef"] = "untreated_infection", ["unmetConditions"] = new JsonArray("not_stabilized"),
            ["graceMinutes"] = 30L, ["cadenceMinutes"] = 10L, ["result"] = policyResult
        };
        var policyOperation = new JsonObject { ["kind"] = "apply_deterioration", ["policyRef"] = "untreated_infection" };
        var direct = GraphEffectfulAddition("shared_local", kind == "collision" ? "policy_graph_definition" : "direct_graph_definition");
        var route = scenario.Before["treatment"]!["routes"]![0]!.AsObject();
        route["outcomes"]![2]!["result"] = directFirst
            ? new JsonArray(direct, policyOperation) : new JsonArray(policyOperation, direct);
        if (kind == "baseline_gate")
        {
            scenario.Before["complications"] = new JsonArray(Enumerable.Range(0, 16).Select(index => (JsonNode)new JsonObject
            {
                ["complicationId"] = $"baseline_{index}", ["kind"] = "impairment", ["state"] = "active",
                ["displayName"] = "Исходное осложнение", ["treatmentDifficultyModifier"] = 0,
                ["ownedEffectIds"] = new JsonArray(), ["visibility"] = "known_to_player"
            }).ToArray());
            route["outcomes"]![2]!["result"] = new JsonArray(new JsonObject
            {
                ["kind"] = "remove_complication", ["complicationId"] = "baseline_0"
            }, policyOperation.DeepClone());
        }
        if (kind == "selected_direct")
        {
            scenario.AcceptedState["acceptedDice"] = new JsonArray(10, 7);
            var directBand = route["outcomes"]![2]!.AsObject();
            directBand["minimumMargin"] = -4;
            directBand["result"] = new JsonArray(direct.DeepClone());
            var policyBand = directBand.DeepClone().AsObject();
            policyBand["bandId"] = "lower_failed_policy";
            policyBand["minimumMargin"] = null;
            policyBand["maximumMargin"] = -5;
            policyBand["result"] = new JsonArray(policyOperation.DeepClone());
            route["outcomes"]!.AsArray().Add(policyBand);
        }
        scenario = scenario with { History = CreateCurrentWoundHistory(scenario.Before) };
        using var fixture = AcceptedStateFixture.Create(scenario);
        var state = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(fixture.GetAcceptedState());
        var before = fixture.ReadCurrentWound();
        var prepared = MortalWoundTreatmentPlanner.PrepareProcedureRequest(state, fixture.ReadCurrentHistory(), before,
            scenario.OperationKey, scenario.RouteId, fixture.AcceptedEventRef(state));
        Assert.True(expected == prepared.IsValid, DescribeIssues(prepared.Issues));
        if (!expected) { Assert.Null(prepared.Request); return; }
        var coordinates = CreateDeteriorationCoordinates(fixture, state, scenario, scenario.OperationKey);
        var parsed = MortalWoundTreatmentContract.ParseProjection(before.Treatment, "graphPolicy", before.Owner.Realm,
            "player", before.Severity.Rank, before.Complications, before.Recovery.DeteriorationPolicy);
        Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
        var typedRoute = Assert.IsType<MortalWoundProcedureRouteDefinition>(Assert.Single(parsed.Treatment!.Routes));
        var failed = typedRoute.Bands.First(band => band.Category == "failed_attempt");
        var reducer = typeof(MortalWoundTreatmentPlanner).GetMethod("PrepareComplexGraphApplicability",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var simulation = MortalWoundTreatmentWorkingWoundSimulator.SimulateGraph(before, new[] { failed.DeclaredResult },
            (working, address, operation) => Assert.IsType<MortalWoundTreatmentPreparedGraphOperationResult>(
                reducer.Invoke(null, new object[] { working, address, operation, state, coordinates })));
        Assert.True(simulation.IsApplicable);
        Assert.False(simulation.Improved);
        Assert.Equal(kind == "add_complication" ? 4 : 3, simulation.WorkingGraph!.Graph.Entries.Length);
        Assert.Equal(budget, simulation.WorkingGraph.Scalars.SlotBudget);
        Assert.Equal(kind == "increase_severity" ? rank + 1 : rank, simulation.WorkingGraph.Scalars.Severity.Rank);
        Assert.Equal(before.Care, simulation.WorkingGraph.Scalars.Care);
        Assert.Equal(before.Severity.LastChangeEventRef, simulation.WorkingGraph.Scalars.Severity.LastChangeEventRef);
        Assert.False(simulation.WorkingGraph.TryExportExisting(out _));
        Assert.Contains(simulation.WorkingGraph.Graph.Complications, row => row.Reference.Origin == WoundWorkingReferenceOrigin.DirectAddition);
        if (kind == "add_complication")
            Assert.Contains(simulation.WorkingGraph.Graph.Complications, row => row.Reference.Origin == WoundWorkingReferenceOrigin.PolicyAddition);
        var missingPolicy = Assert.IsType<MortalWoundTreatmentPreparedGraphOperationResult>(reducer.Invoke(null,
            new object[] { simulation.WorkingGraph, new WoundWorkingOperationAddress(0, 7),
                new MortalWoundApplyDeteriorationOperation("missing_policy"), state, coordinates }));
        Assert.False(missingPolicy.IsApplicable);
        Assert.Null(missingPolicy.After);
        if (kind == "selected_direct")
        {
            var resolution = MortalWoundTreatmentPlanner.CreateProcedureAttempt(prepared.Request!,
                fixture.ReadCurrentHistory(), before, state);
            Assert.Equal("Resolved", resolution.Disposition);
            Assert.Empty(resolution.Issues);
            Assert.IsType<MortalWoundAddComplicationOperation>(Assert.Single(resolution.Resolution!.DeclaredResult));
            Assert.Equal(0, Assert.Single(resolution.Resolution.OutcomeIntents).OperationOrdinal);
        }
    }

    private static JsonObject GraphEffectfulAddition(string localRef, string definitionKey)
    {
        var operation = GraphEffectlessAddition(localRef);
        operation["complicationDraft"]!["complications"]![0]!["treatmentDifficultyModifier"] = 1;
        var definition = WoundContractTestData.CreateOwnedEffectDefinition("wound_test_torn_side", "mortal_world",
            definitionKey, "action_control");
        definition["links"] = new JsonArray();
        // Separate legal mechanics avoid conflating reference namespaces with a duplicated control coordinate.
        definition["components"]![0]!["payload"]!["action"] = definitionKey.StartsWith("policy", StringComparison.Ordinal) ? "cast" : "use_item";
        operation["complicationDraft"]!["consequenceDefinitions"] = new JsonArray(new JsonObject
        {
            ["definitionRef"] = "shared_definition_local", ["definition"] = definition,
            ["root"] = new JsonObject
            {
                ["ownership"] = new JsonObject { ["kind"] = "complication", ["complicationRef"] = localRef },
                ["slots"] = new JsonArray(new JsonObject { ["profileKey"] = "action_control", ["readableSummary"] = "Осложнение ограничивает действие." })
            }
        });
        return operation;
    }

    private static JsonObject GraphEffectlessAddition(string localRef) => new()
    {
        ["kind"] = "add_complication",
        ["complicationDraft"] = new JsonObject
        {
            ["complications"] = new JsonArray(new JsonObject
            {
                ["complicationRef"] = localRef, ["kind"] = "impairment", ["state"] = "active",
                ["displayName"] = "Натяжение края раны", ["treatmentDifficultyModifier"] = 0,
                ["visibility"] = "known_to_player"
            }),
            ["consequenceDefinitions"] = new JsonArray()
        }
    };
}
