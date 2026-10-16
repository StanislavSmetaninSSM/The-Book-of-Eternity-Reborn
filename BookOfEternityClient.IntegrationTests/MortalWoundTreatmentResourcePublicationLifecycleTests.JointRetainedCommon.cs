using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task GuaranteedTreatment_ActualCommonCandidateUsesPrivatelyRetainedCompleteCalculation()
    {
        var observed = new List<AcceptedMechanicsPlan?>();
        // All existing full-union, actual CREATE/TREAT/charge, progression,
        // story/turn, cleanup and inert-repeat assertions execute before these.
        await RunJointCompletionControlAsync(nonzeroProgression: true,
            observeSettledCommonPlan: observed.Add);
        var plan = Assert.IsType<AcceptedMechanicsPlan>(Assert.Single(observed));
        var authority = Assert.IsType<WoundAcceptedTurnPlanner.MortalJointRepairCommonAuthority>(
            plan.JointRepairCommonAuthority);
        var calculationProperty = authority.GetType().GetProperty("RetainedCommonCalculation",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(calculationProperty);
        var calculation = calculationProperty!.GetValue(authority);
        Assert.NotNull(calculation);
        Assert.Same(calculation, calculationProperty.GetValue(authority));

        void ExactRoot(string name, JsonObject? expected)
        {
            var actual = ReadCalculationProperty<JsonObject?>(calculation!, name);
            Assert.Equal(expected?.ToJsonString(), actual?.ToJsonString());
            if (actual is not null)
            {
                actual["test_returned_root_mutation"] = true;
                Assert.Equal(expected?.ToJsonString(),
                    ReadCalculationProperty<JsonObject?>(calculation!, name)?.ToJsonString());
            }
        }
        void ExactMap(string name, IReadOnlyDictionary<string, JsonObject> expected)
        {
            var actual = ReadCalculationProperty<IReadOnlyDictionary<string, JsonObject>>(calculation!, name);
            Assert.Equal(expected.Keys.OrderBy(static p => p, StringComparer.Ordinal),
                actual.Keys.OrderBy(static p => p, StringComparer.Ordinal));
            foreach (var pair in expected)
                Assert.Equal(pair.Value.ToJsonString(), actual[pair.Key].ToJsonString());
            if (actual.Count > 0)
            {
                actual.First().Value["test_returned_root_mutation"] = true;
                var repeated = ReadCalculationProperty<IReadOnlyDictionary<string, JsonObject>>(calculation!, name);
                foreach (var pair in expected)
                    Assert.Equal(pair.Value.ToJsonString(), repeated[pair.Key].ToJsonString());
            }
        }
        ExactRoot("DefinitionAfterImage", plan.DefinitionAfterImage);
        ExactRoot("StateAfterImage", plan.StateAfterImage);
        ExactRoot("HistoryAfterImage", plan.HistoryAfterImage);
        ExactRoot("EffectIdentityAfterImage", plan.EffectIdentityAfterImage);
        ExactRoot("WoundIdentityAfterImage", plan.WoundIdentityAfterImage);
        ExactRoot("WoundHistoryAfterImage", plan.WoundHistoryAfterImage);
        ExactMap("EffectCarrierAfterImages", plan.EffectCarrierAfterImages);
        ExactMap("OwnerCompanionAfterImages", plan.OwnerCompanionAfterImages);
        ExactMap("WoundCarrierAfterImages", plan.WoundCarrierAfterImages);
        Assert.Equal(plan.TouchedPaths.OrderBy(static p => p, StringComparer.Ordinal),
            ReadCalculationProperty<IReadOnlyList<string>>(calculation!, "TouchedPaths")
                .OrderBy(static p => p, StringComparer.Ordinal));
        Assert.Equal(plan.ConsumedPaths.OrderBy(static p => p, StringComparer.Ordinal),
            ReadCalculationProperty<IReadOnlyList<string>>(calculation!, "ConsumedPaths")
                .OrderBy(static p => p, StringComparer.Ordinal));
        Assert.Same(plan.EffectPlan, ReadCalculationProperty<EffectAcceptedTurnPlan>(calculation!, "Effects"));
        var resources = ReadCalculationProperty<AcceptedMechanicsResourcePlanningResult>(calculation!, "Resources");
        Assert.Equal(plan.ResourceEvents, resources.Events.OrderBy(static e => e.Turn)
            .ThenBy(static e => e.ExecutionSequence).ThenBy(static e => e.EventKind, StringComparer.Ordinal)
            .ThenBy(static e => e.OperationId, StringComparer.Ordinal));
        Assert.NotEmpty(resources.AppliedTransitions);
        Assert.True(resources.EffectBoundaryTranscript!.IsComplete);
        Assert.True(plan.EffectPlan!.IsAcceptedBoundaryComplete);
        WritePhysicalInjuryDiagnostic("fr063_joint_actual_retained_common_calculation", new
        {
            RetainedByActualCommonAuthority = true,
            ExactQuartetEffectWoundCompanionAndPathSets = true,
            DetachedReturnedRoots = true, SameCompletedEffectResult = true,
            ResourceAppliedTransitions = resources.AppliedTransitions.Count,
            ActualResourceEvents = resources.Events.Count,
            plan.InputFingerprint, plan.PreparedPlanFingerprint,
            Scope = "actual v2 retained calculation/output preservation; no active S/B or pre-seal drift/foreign-owner proof"
        });
    }

    private static T ReadCalculationProperty<T>(object calculation, string name)
    {
        var property = calculation.GetType().GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        var value = property!.GetValue(calculation);
        return value is null ? default! : Assert.IsAssignableFrom<T>(value);
    }
}
