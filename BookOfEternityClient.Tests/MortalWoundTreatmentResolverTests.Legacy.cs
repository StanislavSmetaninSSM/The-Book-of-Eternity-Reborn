using System.Reflection;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public void LegacyPreparation_UsesTheResolvedProductionHealIntentAndProducesOneMechanicalBatch()
    {
        var planner = RequireOutcomeResolver();
        var legacyPlanner = RequireLegacyPlanner();
        var scenario = CreateOutcomeIntentScenario(GuaranteedHealCase());
        var before = WoundMaterializationContract.Parse(scenario.Before.ToJsonString(), "wound");
        var history = WoundHistoryState.Parse(scenario.History.ToJsonString(), "history");
        Assert.True(before.IsValid, DescribeIssues(before.Issues));
        Assert.True(history.IsValid, DescribeIssues(history.Issues));

        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = fixture.GetAcceptedState();
        var request = ReadValidTypedResult(Invoke(
            ExactStaticMethod(planner, "PrepareGuaranteedRequest", 6),
            new object?[]
            {
                acceptedState, history, before.Wound!, scenario.OperationKey, scenario.RouteId, scenario.EventRef
            }), "Request", "legacy guaranteed request");
        var resolved = Invoke(ExactStaticMethod(planner, "CreateGuaranteedAttempt", 4), new object?[]
        {
            request, history, before.Wound, acceptedState
        });
        AssertResolvedOutcomeIntentPair(resolved, GuaranteedHealCase());
        var resolution = ReadRequiredProperty(resolved, "Resolution");
        Assert.NotNull(resolution);

        var preparationResult = Invoke(ExactStaticMethod(legacyPlanner, "Prepare", 3), new object?[]
        {
            fixture.Binding, resolution, before.Wound
        });
        var preparation = ReadValidTypedResult(preparationResult, "Preparation", "legacy preparation");
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
        var legacyPlanner = RequireLegacyPlanner();
        var finalize = ExactStaticMethod(legacyPlanner, "Finalize", 2);
        Assert.Equal("MortalWoundHealLegacyFinalizationResult", finalize.ReturnType.Name);
        Assert.Equal("MortalWoundHealLegacyPreparation", finalize.GetParameters()[0].ParameterType.Name);
        Assert.Equal("EffectAcceptedTurnPlan", finalize.GetParameters()[1].ParameterType.Name);

        // T070 must feed this method the accepted #1535 plan created from the immutable
        // preparation.  This suite deliberately does not author an effect-result map or
        // an accepted plan: a future production composition coordinator is the only
        // legal source for the second argument.
        AssertClosedResultType(finalize.ReturnType, "Finalization");
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
            Convert.ToString(ReadRequiredProperty(binding, "LegacyId")),
            typedBatch.LocalWoundRef);
        Assert.False(string.IsNullOrWhiteSpace(typedBatch.PreparedWoundId));
        var source = typedBatch.SourceExport;
        Assert.Equal("wound_legacy", source.Kind);
        Assert.Equal(
            Convert.ToString(ReadRequiredProperty(binding, "LegacyId")),
            source.SourceId);
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
}
