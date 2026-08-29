using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public void LegacyPreparation_UsesTheResolvedProductionHealIntentAndProducesOneMechanicalBatch()
    {
        var scenario = CreateOutcomeIntentScenario(GuaranteedHealCase());
        using var fixture = AcceptedStateFixture.Create(scenario);
        var pipeline = BuildLegacyPipeline(fixture, scenario);
        var preparation = pipeline.Preparation;
        AssertClosedProperties(preparation, new[]
        {
            "LegacyDraftBindings", "EffectOperationBatches", "PreparationFingerprint"
        });

        var batches = AsObjects(ReadRequiredProperty(preparation, "EffectOperationBatches"));
        var bindings = AsObjects(ReadRequiredProperty(preparation, "LegacyDraftBindings"));
        Assert.Equal(2, bindings.Length);
        Assert.Single(batches);
        Assert.Equal("t061_mechanical", Convert.ToString(ReadRequiredProperty(bindings[1], "LocalLegacyRef")));
        AssertLegacyBindingHasExactBatchSurface(bindings[1]);
        AssertBatchAgreesWithMechanicalBinding(batches[0], bindings[1]);
    }

    [Fact]
    public void LegacyFinalization_RequiresTheExactProductionAcceptedEffectPlanAndReturnsOrderedGroups()
    {
        var scenario = CreateOutcomeIntentScenario(GuaranteedHealCase());
        using var fixture = AcceptedStateFixture.Create(scenario);
        var pipeline = BuildLegacyPipeline(fixture, scenario);
        var finalization = FinalizeLegacy(pipeline.Preparation, pipeline.AcceptedEffectPlan);

        AssertClosedProperties(finalization, new[]
        {
            "LegacyBindings", "HistoryIntents", "ApplicationResults",
            "EffectPlanFingerprint", "FinalizationFingerprint"
        });
        Assert.False(string.IsNullOrWhiteSpace(Convert.ToString(
            ReadRequiredProperty(finalization, "EffectPlanFingerprint"))));
        Assert.False(string.IsNullOrWhiteSpace(Convert.ToString(
            ReadRequiredProperty(finalization, "FinalizationFingerprint"))));
        var bindings = AsObjects(ReadRequiredProperty(finalization, "LegacyBindings"));
        var intents = AsObjects(ReadRequiredProperty(finalization, "HistoryIntents"));
        Assert.Equal(2, bindings.Length);
        Assert.Equal(2, intents.Length);
        Assert.All(intents, static intent => Assert.False(intent is System.Text.Json.Nodes.JsonNode));

        var group = Assert.Single(AsObjects(ReadRequiredProperty(finalization, "ApplicationResults")));
        AssertClosedProperties(group, new[]
        {
            "LegacyOrdinal", "LegacyId", "SourceExportFingerprint", "Results",
            "ResultGroupFingerprint"
        });
        Assert.Equal(1, Convert.ToInt32(ReadRequiredProperty(group, "LegacyOrdinal")));
        var mechanical = bindings[1];
        Assert.Equal(
            Convert.ToString(ReadRequiredProperty(mechanical, "LegacyId")),
            Convert.ToString(ReadRequiredProperty(group, "LegacyId")));
        Assert.False(string.IsNullOrWhiteSpace(Convert.ToString(
            ReadRequiredProperty(group, "ResultGroupFingerprint"))));
        var results = AsObjects(ReadRequiredProperty(group, "Results"));
        var application = Assert.Single(results);
        Assert.Equal("EffectAcceptedApplicationResult", application.GetType().Name);
        AssertClosedProperties(application, new[]
        {
            "ApplicationRef", "Disposition", "EffectId", "CreateTransitionId",
            "CreatedEventRef", "CausalEventRef", "SourceKey", "TargetKey",
            "CarrierCoordinate", "Materialization"
        });
        var applicationBinding = Assert.Single(AsObjects(ReadRequiredProperty(
            mechanical,
            "ApplicationReferenceBindings")));
        AssertClosedProperties(applicationBinding, new[] { "LocalRef", "NamespacedRef" });
        Assert.Equal(
            Convert.ToString(ReadRequiredProperty(applicationBinding, "NamespacedRef")),
            Convert.ToString(ReadRequiredProperty(application, "ApplicationRef")));
        Assert.NotEqual(
            Convert.ToString(ReadRequiredProperty(applicationBinding, "LocalRef")),
            Convert.ToString(ReadRequiredProperty(application, "ApplicationRef")));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("reordered")]
    [InlineData("merged")]
    [InlineData("split")]
    public void LegacyFinalization_RejectsProductionPlanWithDifferentBatchTopology(
        string mutation)
    {
        var (expectedDrafts, foreignDrafts) = CreateLegacyTopologyPair(mutation);
        var (scenario, foreignRouteId) = CreateLegacyTopologyScenario(
            expectedDrafts,
            foreignDrafts,
            mutation);
        // Both independent registries receive byte-identical canonical roots and
        // live-turn manifests.  The foreign accepted plan therefore differs only
        // by its production-resolved route/request topology, never by a detached
        // binding or a fixture-authored plan.
        using var expectedFixture = AcceptedStateFixture.Create(scenario);
        using var foreignFixture = AcceptedStateFixture.Create(scenario);
        var expected = BuildLegacyPipeline(
            expectedFixture,
            scenario,
            scenario.RouteId,
            scenario.OperationKey + "_expected_" + mutation);
        var foreign = BuildLegacyPipeline(
            foreignFixture,
            scenario,
            foreignRouteId,
            scenario.OperationKey + "_foreign_" + mutation);

        var legacyPlanner = RequireLegacyPlanner();
        var rejected = Invoke(
            ExactStaticMethod(legacyPlanner, "Finalize", 2),
            new[] { expected.Preparation, foreign.AcceptedEffectPlan });
        AssertInvalidTypedResult(rejected, "Finalization", "legacy topology " + mutation);
        var issue = Assert.Single(AsObjects(ReadRequiredProperty(rejected, "Issues"))
            .Select(Assert.IsType<ValidationIssue>));
        Assert.Equal("mortal_wound_heal_legacy_effect_topology_mismatch", issue.Code);
        Assert.Equal("treatment.legacyEffectPlan", issue.FilePath);
    }

    [Fact]
    public void LegacyFinalization_GetsItsAcceptedPlanOnlyThroughTheT070EffectInputComposerBridge()
    {
        var compose = ExactStaticMethod(
            typeof(EffectAcceptedTurnInputComposer),
            "ComposeMortalWoundLegacyBatches",
            2);
        Assert.Equal("EffectAcceptedTurnInputCompositionResult", compose.ReturnType.Name);
        Assert.Equal(typeof(EffectAcceptedTurnInput), compose.GetParameters()[0].ParameterType);
        Assert.Equal("MortalWoundHealLegacyPreparation", compose.GetParameters()[1].ParameterType.Name);
        AssertClosedResultType(compose.ReturnType, "Input");
    }

    private static LegacyPipeline BuildLegacyPipeline(
        AcceptedStateFixture fixture,
        ResolverScenario scenario,
        string? routeId = null,
        string? operationKey = null)
    {
        var flow = ResolveCurrentTreatment(
            fixture,
            "guaranteed",
            operationKey ?? scenario.OperationKey,
            routeId ?? scenario.RouteId);
        Assert.Equal("success", Convert.ToString(ReadRequiredProperty(
            flow.Resolution,
            "ResultCategory")));
        Assert.Equal(
            new[] { "remove_complication", "heal" },
            AsObjects(ReadRequiredProperty(flow.Resolution, "OutcomeIntents"))
                .Select(intent => Convert.ToString(ReadRequiredProperty(intent, "Kind"))));
        var acceptedState = flow.AcceptedState;
        var legacyPlanner = RequireLegacyPlanner();
        var preparation = ReadValidTypedResult(
            Invoke(ExactStaticMethod(legacyPlanner, "Prepare", 3), new object?[]
            {
                ReadAcceptedStateMember(acceptedState, "Binding"),
                flow.Resolution,
                flow.Before
            }),
            "Preparation",
            "legacy preparation");

        var baseEffectInput = ReadAcceptedStateMember(acceptedState, "EffectInput");
        Assert.IsType<EffectAcceptedTurnInput>(baseEffectInput);
        var composed = Invoke(
            ExactStaticMethod(
                typeof(EffectAcceptedTurnInputComposer),
                "ComposeMortalWoundLegacyBatches",
                2),
            new[] { baseEffectInput, preparation });
        var effectInput = Assert.IsType<EffectAcceptedTurnInput>(
            ReadValidTypedResult(composed, "Input", "legacy #1535 bridge"));
        var planned = AcceptedTurnAuthorityRegistry.GetOrBuildEffectValidated(
            fixture.FileSystem,
            fixture.Lease,
            effectInput);
        Assert.True(planned.Success, DescribeIssues(planned.Issues));
        var acceptedPlan = Assert.IsType<EffectAcceptedTurnPlan>(planned.Plan);
        return new LegacyPipeline(flow, preparation, effectInput, acceptedPlan);
    }

    private static object FinalizeLegacy(object preparation, object acceptedEffectPlan)
    {
        var legacyPlanner = RequireLegacyPlanner();
        var finalize = ExactStaticMethod(legacyPlanner, "Finalize", 2);
        Assert.Equal("MortalWoundHealLegacyFinalizationResult", finalize.ReturnType.Name);
        Assert.Equal("MortalWoundHealLegacyPreparation", finalize.GetParameters()[0].ParameterType.Name);
        Assert.Equal("EffectAcceptedTurnPlan", finalize.GetParameters()[1].ParameterType.Name);
        AssertClosedResultType(finalize.ReturnType, "Finalization");
        return ReadValidTypedResult(
            Invoke(finalize, new[] { preparation, acceptedEffectPlan }),
            "Finalization",
            "legacy finalization");
    }

    private static (ResolverScenario Scenario, string ForeignRouteId) CreateLegacyTopologyScenario(
        JsonArray expectedDrafts,
        JsonArray foreignDrafts,
        string suffix)
    {
        var scenario = CreateOutcomeIntentScenario(GuaranteedHealCase());
        var expectedRoute = scenario.Before["treatment"]!["routes"]![0]!.AsObject();
        expectedRoute["outcomes"]![0]!["result"]![1]!["legacies"] = expectedDrafts.DeepClone();
        var foreignRoute = expectedRoute.DeepClone().AsObject();
        var foreignRouteId = "guaranteed_t061_foreign_topology_" + suffix;
        foreignRoute["routeId"] = foreignRouteId;
        foreignRoute["outcomes"]![0]!["result"]![1]!["legacies"] = foreignDrafts.DeepClone();
        scenario.Before["treatment"]!["routes"]!.AsArray().Add(foreignRoute);
        scenario.Before["treatment"]!["knownRouteIds"]!.AsArray().Add(foreignRouteId);
        var parsed = WoundMaterializationContract.Parse(
            scenario.Before.ToJsonString(),
            "legacyTopology");
        Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
        return (scenario, foreignRouteId);
    }

    private static (JsonArray Expected, JsonArray Foreign) CreateLegacyTopologyPair(
        string mutation)
    {
        var oneA = CreateMechanicalLegacyDraft(
            "t061_legacy_a",
            "t061_application_a");
        var oneB = CreateMechanicalLegacyDraft(
            "t061_legacy_b",
            "t061_application_b");
        var merged = CreateMechanicalLegacyDraft(
            "t061_legacy_merged",
            "t061_application_a",
            "t061_application_b");
        return mutation switch
        {
            "missing" => (
                new JsonArray(oneA.DeepClone(), oneB.DeepClone()),
                new JsonArray(oneA.DeepClone())),
            "extra" => (
                new JsonArray(oneA.DeepClone()),
                new JsonArray(oneA.DeepClone(), oneB.DeepClone())),
            "reordered" => (
                new JsonArray(oneA.DeepClone(), oneB.DeepClone()),
                new JsonArray(oneB.DeepClone(), oneA.DeepClone())),
            "merged" => (
                new JsonArray(oneA.DeepClone(), oneB.DeepClone()),
                new JsonArray(merged.DeepClone())),
            "split" => (
                new JsonArray(merged.DeepClone()),
                new JsonArray(oneA.DeepClone(), oneB.DeepClone())),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null)
        };
    }

    private static OutcomeIntentCase GuaranteedHealCase() => new(
        "guaranteed_remove_then_heal",
        "guaranteed_severity_one_heal_has_empty_legacy_array",
        "guaranteed",
        "success",
        new[] { "remove_complication", "heal" });

    private static Type RequireLegacyPlanner()
    {
        var type = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundHealLegacyPlanner",
            throwOnError: false,
            ignoreCase: false);
        Assert.True(type is not null,
            "T070 MortalWoundHealLegacyPlanner is absent; T061 freezes its sole typed handoff.");
        return type!;
    }

    private static void AssertBatchAgreesWithMechanicalBinding(object batch, object binding)
    {
        var typedBatch = Assert.IsType<WoundEffectOperationBatch>(batch);
        Assert.Equal(
            new[]
            {
                "LocalWoundRef", "PreparedWoundId", "SourceExport", "RootApplications",
                "TerminalOperations", "RootLineageAuthority", "SourceExportFingerprint", "TransitionAuthority"
            }.OrderBy(static property => property),
            typeof(WoundEffectOperationBatch).GetProperties(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(static property => property.GetIndexParameters().Length == 0)
                .Select(static property => property.Name)
                .OrderBy(static property => property));
        Assert.Equal(
            Convert.ToString(ReadRequiredProperty(binding, "LocalLegacyRef")),
            typedBatch.LocalWoundRef);
        Assert.Equal(
            Convert.ToString(ReadRequiredProperty(binding, "LegacyId")),
            typedBatch.PreparedWoundId);
        var source = typedBatch.SourceExport;
        Assert.Equal("wound_legacy", source.Kind);
        Assert.Equal(
            Convert.ToString(ReadRequiredProperty(binding, "LegacyId")),
            source.SourceId);
        Assert.Equal(
            Convert.ToString(ReadRequiredProperty(binding, "LocalLegacyRef")),
            source.SourceRef);
        Assert.False(source.Materializable);
        Assert.True(string.Equals(source.State, "active", StringComparison.Ordinal));
        Assert.NotEmpty(typedBatch.RootApplications);
        Assert.NotNull(typedBatch.TransitionAuthority);
        Assert.False(string.IsNullOrWhiteSpace(typedBatch.SourceExportFingerprint));
    }

    private static void AssertLegacyBindingHasExactBatchSurface(object binding)
    {
        AssertClosedProperties(binding, new[]
        {
            "LegacyOrdinal", "LocalLegacyRef", "LegacyId", "Kind", "DefinitionReferenceBindings",
            "ApplicationReferenceBindings", "DeclaredLegacyFingerprint", "SeedFingerprint"
        });
    }

    private static void AssertClosedResultType(Type resultType, string nullableValue)
    {
        Assert.Equal(
            new[] { "IsValid", "Issues", nullableValue }.OrderBy(static value => value),
            resultType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.GetIndexParameters().Length == 0)
                .Select(static property => property.Name)
                .OrderBy(static value => value));
    }

    private sealed record LegacyPipeline(
        TreatmentFlow Flow,
        object Preparation,
        EffectAcceptedTurnInput EffectInput,
        EffectAcceptedTurnPlan AcceptedEffectPlan);
}
