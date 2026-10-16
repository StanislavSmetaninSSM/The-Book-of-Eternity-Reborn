using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectAcceptedTurnPlannerTests
{
    [Theory]
    [InlineData("advance")]
    [InlineData("no_change")]
    [InlineData("no_eligible")]
    public void LifecycleLedger_ActualCompletedReductionRetainsBothPassesAndActualDisposition(string scenario)
    {
        var fixture = CreateOrdinaryReductionFixture(effects: true, configureEffectInput: input =>
        {
            if (scenario == "no_change")
            {
                var source = input.EventInput["lifecycleEvents"]![0]!;
                source["phase"] = "owner_damaged";
                source["triggerId"] = "on_owner_damaged";
                source["eventRef"] = "turn_42:effect:on_owner_damaged:1";
                source["causalEventRef"] = "turn_42:owner_damaged:1";
            }
            if (scenario == "no_eligible")
                return input with
                {
                    PreTurnCarriers = input.PreTurnCarriers! with
                    {
                        PlayerEffects = new JsonObject
                        {
                            ["schemaVersion"] = 1, ["activeEffects"] = new JsonArray()
                        }
                    },
                    PreTurnIdentityIndex = EffectMaterializationTestFixture.CreateIdentityIndex()
                };
            return input;
        });
        var reduction = AcceptedMechanicsPlanner.ReduceAcceptedPlan(fixture.Input,
            AcceptedMechanicsPlanFingerprints.ComputePlanningInput(fixture.Input));
        Assert.Equal(AcceptedMechanicsPlanner.AcceptedMechanicsReductionKind.CompletedOrdinaryReduction,
            reduction.Kind);
        var completed = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(reduction.Completed);
        Assert.True(completed.Resources.IsValid);
        Assert.True(completed.Resources.EffectBoundaryTranscript!.IsComplete);
        Assert.True(completed.Effects!.IsAcceptedBoundaryComplete);
        Assert.Equal(1, fixture.Outcome.ProjectCalls);
        var assembled = AcceptedMechanicsPlanner.CompleteAcceptedReduction(reduction);
        Assert.True(assembled.Success, DescribeOrdinaryReductionIssues(assembled.Issues));
        Assert.Same(completed.Effects, assembled.Plan!.EffectPlan);
        var allocations = (fixture.ResourceAllocations, fixture.Effects.EffectCalls, fixture.Effects.TransitionCalls);
        var payload = WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(completed.Effects);

        // Semantic RED only after the real old planner, both lifetime phases,
        // resource completion and final common assembly succeeded.
        var property = typeof(EffectAcceptedTurnPlan).GetProperty("LifecycleExecutionLedger",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);
        var ledger = property!.GetValue(completed.Effects);
        Assert.NotNull(ledger);
        JsonObject Export(object value)
        {
            var method = value.GetType().GetMethod("ToJsonObject",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(method);
            return Assert.IsType<JsonObject>(method!.Invoke(value, null));
        }
        var image = Export(ledger!);
        var exact = image.ToJsonString();
        Assert.Equal(1, image["version"]!.GetValue<int>());
        var passes = Assert.IsType<JsonArray>(image["passes"]);
        Assert.Equal(new[] { "bound_continuations", "final_lifetime" },
            passes.Select(pass => pass!["kind"]!.GetValue<string>()));
        var sourceEvents = Assert.IsType<JsonArray>(fixture.Input.PlanningContext!.EffectPlan!.EventInput["lifecycleEvents"]);
        foreach (var pass in passes)
        {
            Assert.Equal(sourceEvents.Count, pass!["sourceEventCount"]!.GetValue<int>());
            var events = Assert.IsType<JsonArray>(pass["events"]);
            Assert.Equal(sourceEvents.Count, events.Count);
            for (var ordinal = 0; ordinal < sourceEvents.Count; ordinal++)
            {
                Assert.Equal(ordinal, events[ordinal]!["inputOrdinal"]!.GetValue<int>());
                Assert.Equal(sourceEvents[ordinal]!["eventRef"]!.GetValue<string>(),
                    events[ordinal]!["sourceEventRef"]!.GetValue<string>());
            }
        }
        var finalEvent = Assert.Single(Assert.IsType<JsonArray>(passes[1]!["events"]));
        var subjects = Assert.IsType<JsonArray>(finalEvent!["subjects"]);
        if (scenario == "no_eligible")
        {
            Assert.Empty(subjects);
            Assert.Equal("no_eligible", finalEvent["disposition"]!.GetValue<string>());
        }
        else
        {
            var subject = Assert.Single(subjects);
            Assert.Equal("evaluated", finalEvent["disposition"]!.GetValue<string>());
            Assert.Equal(scenario, subject!["outcome"]!.GetValue<string>());
            Assert.NotNull(subject["schedulerEvent"]);
        }
        image["passes"] = new JsonArray();
        Assert.Equal(exact, Export(ledger!).ToJsonString());
        var copied = EffectAcceptedTurnPlan.DetachedCopyOf(completed.Effects);
        var copiedLedger = property.GetValue(copied);
        Assert.NotNull(copiedLedger);
        Assert.Equal(exact, Export(copiedLedger!).ToJsonString());
        Assert.Equal(payload, WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(completed.Effects));
        Assert.Equal(allocations,
            (fixture.ResourceAllocations, fixture.Effects.EffectCalls, fixture.Effects.TransitionCalls));
        Assert.Same(assembled, AcceptedMechanicsPlanner.CompleteAcceptedReduction(reduction));
        Assert.Equal(1, fixture.Outcome.ProjectCalls);
    }
}
