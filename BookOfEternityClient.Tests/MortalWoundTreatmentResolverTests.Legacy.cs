using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
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

    [Fact]
    public void LegacyPreparation_AllCosmeticLegaciesProduceNoEffectBatchOrApplicationGroup()
    {
        var scenario = CreateOutcomeIntentScenario(GuaranteedHealCase());
        scenario.Before["treatment"]!["routes"]![0]!["outcomes"]![0]!["result"]![1]!["legacies"] =
            new System.Text.Json.Nodes.JsonArray(
                new System.Text.Json.Nodes.JsonObject
                {
                    ["localLegacyRef"] = "t061_cosmetic_a",
                    ["kind"] = "cosmetic",
                    ["readableSummary"] = "First lasting scar."
                });
        using var fixture = AcceptedStateFixture.Create(scenario);
        var pipeline = BuildLegacyPipeline(fixture, scenario);
        Assert.Single(AsObjects(ReadRequiredProperty(
            pipeline.Preparation,
            "LegacyDraftBindings")));
        Assert.Empty(AsObjects(ReadRequiredProperty(
            pipeline.Preparation,
            "EffectOperationBatches")));

        var finalization = FinalizeLegacy(pipeline.Preparation, pipeline.AcceptedEffectPlan);
        Assert.Single(AsObjects(ReadRequiredProperty(finalization, "LegacyBindings")));
        Assert.Single(AsObjects(ReadRequiredProperty(finalization, "HistoryIntents")));
        Assert.Empty(AsObjects(ReadRequiredProperty(finalization, "ApplicationResults")));
    }

    [Fact]
    public void LegacyFinalization_MultipleMechanicalLegaciesKeepOrderedIndependentResultGroups()
    {
        var scenario = CreateOutcomeIntentScenario(GuaranteedHealCase());
        scenario.Before["treatment"]!["routes"]![0]!["outcomes"]![0]!["result"]![1]!["legacies"] =
            new System.Text.Json.Nodes.JsonArray(
                CreateMechanicalLegacyDraft("t061_mechanical_a", "t061_application_a"),
                CreateMechanicalLegacyDraft("t061_mechanical_b", "t061_application_b"));
        using var fixture = AcceptedStateFixture.Create(scenario);
        var pipeline = BuildLegacyPipeline(fixture, scenario);
        var batches = AsObjects(ReadRequiredProperty(
            pipeline.Preparation,
            "EffectOperationBatches"));
        Assert.Equal(2, batches.Length);
        var finalization = FinalizeLegacy(pipeline.Preparation, pipeline.AcceptedEffectPlan);
        var bindings = AsObjects(ReadRequiredProperty(finalization, "LegacyBindings"));
        var groups = AsObjects(ReadRequiredProperty(finalization, "ApplicationResults"));
        Assert.Equal(2, bindings.Length);
        Assert.Equal(2, groups.Length);
        for (var ordinal = 0; ordinal < groups.Length; ordinal++)
        {
            Assert.Equal(ordinal, Convert.ToInt32(ReadRequiredProperty(groups[ordinal], "LegacyOrdinal")));
            Assert.Equal(
                Convert.ToString(ReadRequiredProperty(bindings[ordinal], "LegacyId")),
                Convert.ToString(ReadRequiredProperty(groups[ordinal], "LegacyId")));
            Assert.Equal(
                Assert.IsType<WoundEffectOperationBatch>(batches[ordinal]).SourceExportFingerprint,
                Convert.ToString(ReadRequiredProperty(groups[ordinal], "SourceExportFingerprint")));
            Assert.Single(AsObjects(ReadRequiredProperty(groups[ordinal], "Results")));
            AssertAuthorityFingerprint(ReadRequiredProperty(groups[ordinal], "ResultGroupFingerprint"));
        }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("reordered")]
    [InlineData("merged")]
    [InlineData("split")]
    public void LegacyFinalization_RejectsProductionPlanWithDifferentMechanicalBatchTopology(
        string topology)
    {
        var baselineScenario = CreateLegacyTopologyScenario("baseline");
        var changedScenario = CreateLegacyTopologyScenario(topology);
        using var baselineFixture = AcceptedStateFixture.Create(baselineScenario);
        using var changedFixture = AcceptedStateFixture.Create(changedScenario);
        var baseline = BuildLegacyPipeline(
            baselineFixture,
            baselineScenario,
            operationKey: baselineScenario.OperationKey + "_topology_baseline");
        var changed = BuildLegacyPipeline(
            changedFixture,
            changedScenario,
            operationKey: changedScenario.OperationKey + "_topology_" + topology);
        AssertLegacyPreparationTopology(
            baseline.Preparation,
            ExpectedLegacyTopology("baseline"));
        AssertLegacyPreparationTopology(
            changed.Preparation,
            ExpectedLegacyTopology(topology));
        Assert.True(changed.AcceptedEffectPlan.IsAcceptedBoundaryComplete);

        var rejected = Invoke(
            ExactStaticMethod(RequireLegacyPlanner(), "Finalize", 2),
            new object?[] { baseline.Preparation, changed.AcceptedEffectPlan });
        AssertInvalidTypedResult(
            rejected,
            "Finalization",
            $"{topology} production legacy batch topology");
        Assert.Contains(
            AsObjects(ReadRequiredProperty(rejected, "Issues"))
                .Select(Assert.IsType<ValidationIssue>),
            static issue => string.Equals(
                issue.Code,
                "mortal_wound_legacy_topology_mismatch",
                StringComparison.Ordinal));
    }

    [Fact]
    public void LegacyFinalization_ForeignRequestWithSameTopologyIsNotMisreportedAsTopologyMismatch()
    {
        var scenario = CreateLegacyTopologyScenario("baseline");
        using var baselineFixture = AcceptedStateFixture.Create(scenario);
        using var foreignFixture = AcceptedStateFixture.Create(scenario);
        var baseline = BuildLegacyPipeline(
            baselineFixture,
            scenario,
            operationKey: scenario.OperationKey + "_same_topology_baseline");
        var foreign = BuildLegacyPipeline(
            foreignFixture,
            scenario,
            operationKey: scenario.OperationKey + "_same_topology_foreign");
        var expected = ExpectedLegacyTopology("baseline");
        AssertLegacyPreparationTopology(baseline.Preparation, expected);
        AssertLegacyPreparationTopology(foreign.Preparation, expected);
        Assert.True(foreign.AcceptedEffectPlan.IsAcceptedBoundaryComplete);

        var rejected = Invoke(
            ExactStaticMethod(RequireLegacyPlanner(), "Finalize", 2),
            new object?[] { baseline.Preparation, foreign.AcceptedEffectPlan });
        AssertInvalidTypedResult(rejected, "Finalization", "foreign request with same topology");
        Assert.DoesNotContain(
            AsObjects(ReadRequiredProperty(rejected, "Issues"))
                .Select(Assert.IsType<ValidationIssue>),
            static issue => string.Equals(
                issue.Code,
                "mortal_wound_legacy_topology_mismatch",
                StringComparison.Ordinal));
    }

    [Fact]
    public void LegacyPublication_PersistsProvenanceAcrossColdRestartAfterLaterMechanicalEffectRemoval()
    {
        var scenario = CreateOutcomeIntentScenario(GuaranteedHealCase());
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "guaranteed",
            scenario.OperationKey + "_durable_legacies",
            scenario.RouteId);
        ComposeAndPublishTreatment(fixture, flow);
        var carrier = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(
            fixture.FileSystem.ResolvePath(WoundCarrierCatalog.PlayerPath)))!.AsObject();
        Assert.Empty(carrier["activeWounds"]!.AsArray());

        var historyPath = fixture.FileSystem.ResolvePath(WoundHistoryState.HistoryPath);
        var historyBytes = File.ReadAllBytes(historyPath);
        var history = System.Text.Json.Nodes.JsonNode.Parse(
            System.Text.Encoding.UTF8.GetString(historyBytes))!.AsObject();
        var legacyRows = history["transitions"]!.AsArray()
            .Where(row => string.Equals(
                row!["kind"]!.GetValue<string>(),
                "legacy",
                StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, legacyRows.Length);
        var healRow = Assert.Single(history["transitions"]!.AsArray(), row => string.Equals(
            row!["kind"]!.GetValue<string>(),
            "heal",
            StringComparison.Ordinal));
        Assert.True(healRow!["terminal"]!.GetValue<bool>());
        var terminalTransitionId = healRow["transitionId"]!.GetValue<string>();
        Assert.Equal(2, legacyRows.Select(row => row!["operationKey"]!.GetValue<string>())
            .Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(2, legacyRows.Select(row => row!["eventRef"]!.GetValue<string>())
            .Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(
            Convert.ToString(ReadRequiredProperty(
                ReadRequiredProperty(flow.Request, "Coordinates"),
                "OperationKey")),
            legacyRows.Select(row => row!["operationKey"]!.GetValue<string>()));
        Assert.All(legacyRows, row =>
        {
            Assert.False(row!["terminal"]!.GetValue<bool>());
            Assert.Equal("legacy", row["transitionResult"]!["kind"]!.GetValue<string>());
            Assert.Equal("wound_test_torn_side",
                row["transitionResult"]!["woundId"]!.GetValue<string>());
            Assert.Equal(terminalTransitionId,
                row["transitionResult"]!["terminalTransitionId"]!.GetValue<string>());
            AssertAuthorityFingerprint(row["transitionResult"]!["resultFingerprint"]!.GetValue<string>());
        });
        var mechanical = Assert.Single(legacyRows, row => string.Equals(
            row!["transitionResult"]!["legacyKind"]!.GetValue<string>(),
            "mechanical_effect",
            StringComparison.Ordinal));
        var mechanicalHistoryBeforeRemoval = mechanical!.DeepClone();
        Assert.Equal("wound_legacy",
            mechanical!["transitionResult"]!["sourceKind"]!.GetValue<string>());
        Assert.Equal(
            mechanical["transitionResult"]!["legacyId"]!.GetValue<string>(),
            mechanical["transitionResult"]!["sourceId"]!.GetValue<string>());
        var applicationResults = mechanical["transitionResult"]!["applicationResults"]!.AsArray();
        Assert.NotEmpty(applicationResults);
        var legacyId = mechanical["transitionResult"]!["legacyId"]!.GetValue<string>();
        var legacyEffectIds = applicationResults
            .Select(application => application!["effectId"]!.GetValue<string>())
            .ToArray();
        Assert.All(applicationResults, application =>
        {
            Assert.Equal(
                new[] { "applicationRef", "effectId", "materializationFingerprint" },
                application!.AsObject().Select(static pair => pair.Key).OrderBy(static key => key));
            Assert.False(string.IsNullOrWhiteSpace(application["applicationRef"]!.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(application["effectId"]!.GetValue<string>()));
            AssertAuthorityFingerprint(application["materializationFingerprint"]!.GetValue<string>());
        });
        AssertAuthorityFingerprint(
            mechanical["transitionResult"]!["sourceExportFingerprint"]!.GetValue<string>());
        AssertAuthorityFingerprint(
            mechanical["transitionResult"]!["effectPlanFingerprint"]!.GetValue<string>());

        var playerEffects = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(
            fixture.FileSystem.ResolvePath(EffectCarrierCatalog.PlayerPath)))!.AsObject();
        var activeLegacyEffects = playerEffects["activeEffects"]!.AsArray()
            .OfType<System.Text.Json.Nodes.JsonObject>()
            .Where(effect => legacyEffectIds.Contains(
                effect["effectId"]!.GetValue<string>(),
                StringComparer.Ordinal))
            .ToArray();
        Assert.Equal(legacyEffectIds.Length, activeLegacyEffects.Length);
        Assert.All(activeLegacyEffects, effect =>
        {
            Assert.Equal("wound_legacy", effect["source"]!["kind"]!.GetValue<string>());
            Assert.Equal(legacyId, effect["source"]!["sourceId"]!.GetValue<string>());
        });

        Assert.Contains(
            WoundHistoryState.HistoryPath,
            EffectAcceptedTurnInputComposer.SourceAuthorityPaths,
            StringComparer.Ordinal);
        var sourceRoots = EffectAcceptedTurnInputComposer.SourceAuthorityPaths
            .ToDictionary(
                static path => path,
                path => File.Exists(fixture.FileSystem.ResolvePath(path))
                    ? System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(
                        fixture.FileSystem.ResolvePath(path)))
                    : null,
                StringComparer.Ordinal);
        var reloadedSources = EffectAcceptedTurnInputComposer
            .BuildCanonicalSourceAuthority(sourceRoots);
        Assert.Empty(reloadedSources.Issues);
        Assert.All(activeLegacyEffects, effect =>
        {
            var resolution = reloadedSources.ResolveCanonicalBinding(
                new EffectSourceKey(
                    "mortal_world",
                    "wound_legacy",
                    legacyId,
                    effect["source"]!["definitionKey"]!.GetValue<string>()),
                "player");
            Assert.True(resolution.Success, DescribeIssues(resolution.Issues));
        });

        var terminalEffectTransitionId = PublishLegacyTerminalEffectRemoval(
            fixture,
            activeLegacyEffects.Select(effect => effect["effectId"]!.GetValue<string>())
                .ToArray());

        Assert.Equal(historyBytes, File.ReadAllBytes(historyPath));
        var effectsAfterRemoval = JsonNode.Parse(File.ReadAllText(
            fixture.FileSystem.ResolvePath(EffectCarrierCatalog.PlayerPath)))!.AsObject();
        Assert.DoesNotContain(
            effectsAfterRemoval["activeEffects"]!.AsArray(),
            effect => legacyEffectIds.Contains(
                effect!["effectId"]!.GetValue<string>(),
                StringComparer.Ordinal));
        var identityAfterRemoval = JsonNode.Parse(File.ReadAllText(
            fixture.FileSystem.ResolvePath(EffectIdentityState.StatePath)))!.AsObject();
        Assert.All(legacyEffectIds, effectId =>
        {
            var entry = Assert.Single(identityAfterRemoval["entries"]!.AsArray(), candidate =>
                string.Equals(
                    candidate!["effectId"]!.GetValue<string>(),
                    effectId,
                    StringComparison.Ordinal));
            Assert.Equal("dispelled", entry!["state"]!.GetValue<string>());
            Assert.Equal(
                "dispel",
                entry["transitions"]!.AsArray()[^1]!["kind"]!.GetValue<string>());
            Assert.Equal(
                terminalEffectTransitionId,
                entry["transitions"]!.AsArray()[^1]!["transitionId"]!.GetValue<string>());
        });

        using var coldFixture = CreateColdRootCopy(fixture);
        Assert.NotSame(
            fixture.FileSystem.CanonicalRootAuthorityIdentity,
            coldFixture.FileSystem.CanonicalRootAuthorityIdentity);
        var coldHistoryPath = coldFixture.FileSystem.ResolvePath(
            WoundHistoryState.HistoryPath);
        Assert.Equal(historyBytes, File.ReadAllBytes(coldHistoryPath));
        Assert.True(coldFixture.ReadCurrentHistory().IsValid);
        var coldHistory = JsonNode.Parse(File.ReadAllText(coldHistoryPath))!.AsObject();
        var coldMechanical = Assert.Single(
            coldHistory["transitions"]!.AsArray(),
            row => string.Equals(
                row!["kind"]!.GetValue<string>(),
                "legacy",
                StringComparison.Ordinal) &&
                string.Equals(
                    row["transitionResult"]!["legacyKind"]!.GetValue<string>(),
                    "mechanical_effect",
                    StringComparison.Ordinal));
        Assert.True(JsonNode.DeepEquals(
            mechanicalHistoryBeforeRemoval,
            coldMechanical));
        var coldEffects = JsonNode.Parse(File.ReadAllText(
            coldFixture.FileSystem.ResolvePath(EffectCarrierCatalog.PlayerPath)))!.AsObject();
        Assert.DoesNotContain(
            coldEffects["activeEffects"]!.AsArray(),
            effect => legacyEffectIds.Contains(
                effect!["effectId"]!.GetValue<string>(),
                StringComparer.Ordinal));
        var coldIdentity = JsonNode.Parse(File.ReadAllText(
            coldFixture.FileSystem.ResolvePath(EffectIdentityState.StatePath)))!.AsObject();
        Assert.All(legacyEffectIds, effectId =>
        {
            var entry = Assert.Single(coldIdentity["entries"]!.AsArray(), candidate =>
                string.Equals(
                    candidate!["effectId"]!.GetValue<string>(),
                    effectId,
                    StringComparison.Ordinal));
            Assert.Equal("dispelled", entry!["state"]!.GetValue<string>());
            Assert.Equal(
                terminalEffectTransitionId,
                entry["transitions"]!.AsArray()[^1]!["transitionId"]!.GetValue<string>());
        });
        var coldSourceRoots = EffectAcceptedTurnInputComposer.SourceAuthorityPaths
            .ToDictionary(
                static path => path,
                path => File.Exists(coldFixture.FileSystem.ResolvePath(path))
                    ? JsonNode.Parse(File.ReadAllText(
                        coldFixture.FileSystem.ResolvePath(path)))
                    : null,
                StringComparer.Ordinal);
        var coldSources = EffectAcceptedTurnInputComposer
            .BuildCanonicalSourceAuthority(coldSourceRoots);
        Assert.Empty(coldSources.Issues);
        Assert.All(activeLegacyEffects, effect =>
        {
            var resolution = coldSources.ResolveCanonicalBinding(
                new EffectSourceKey(
                    "mortal_world",
                    "wound_legacy",
                    legacyId,
                    effect["source"]!["definitionKey"]!.GetValue<string>()),
                "player");
            Assert.True(resolution.Success, DescribeIssues(resolution.Issues));
        });
    }

    [Fact]
    public void LegacyFinalization_RejectsAcceptedPlanFromAnotherSealedTreatmentRequest()
    {
        var scenario = CreateOutcomeIntentScenario(GuaranteedHealCase());
        using var expectedFixture = AcceptedStateFixture.Create(scenario);
        using var foreignFixture = AcceptedStateFixture.Create(scenario);
        var expected = BuildLegacyPipeline(
            expectedFixture,
            scenario,
            scenario.RouteId,
            scenario.OperationKey + "_expected");
        var foreign = BuildLegacyPipeline(
            foreignFixture,
            scenario,
            scenario.RouteId,
            scenario.OperationKey + "_foreign");

        var legacyPlanner = RequireLegacyPlanner();
        var rejected = Invoke(
            ExactStaticMethod(legacyPlanner, "Finalize", 2),
            new[] { expected.Preparation, foreign.AcceptedEffectPlan });
        AssertInvalidTypedResult(rejected, "Finalization", "foreign legacy effect plan");
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

    private static string PublishLegacyTerminalEffectRemoval(
        AcceptedStateFixture fixture,
        IReadOnlyList<string> effectIds)
    {
        var effectId = Assert.Single(effectIds);
        fixture.PrepareNextTurn(43, 1_260, "legacy_effect_removal");
        var terminalCommand = new JsonObject
        {
            ["operation"] = "dispel",
            ["effectId"] = effectId,
            ["target"] = new JsonObject
            {
                ["kind"] = "player",
                ["targetId"] = "player_current"
            },
            ["authority"] = new JsonObject
            {
                ["kind"] = "physical_treatment",
                ["authorityId"] = "turn_43"
            },
            ["eventRef"] = new JsonObject
            {
                ["kind"] = "accepted_turn",
                ["authorityId"] = "turn_43"
            },
            ["reason"] = "Follow-up care removes the healed wound's remaining legacy effect."
        };
        var response = new GameResponse
        {
            Response = "Follow-up care removes the remaining mechanical legacy.",
            EffectChanges = new[] { JsonSerializer.SerializeToElement(terminalCommand) },
            EffectResolutionReceipts = Array.Empty<JsonElement>(),
            EffectEventReports = Array.Empty<JsonElement>()
        };

        fixture.ReleaseLeaseForExternalDistribution();
        try
        {
            var modified = new StateDistributor(
                    fixture.FileSystem,
                    NullLogger<StateDistributor>.Instance)
                .DistributeAsync(response)
                .GetAwaiter()
                .GetResult();
            Assert.Contains(
                EffectAcceptedTurnPlan.CommandPath,
                modified,
                StringComparer.Ordinal);
            var issues = new ValidationService(
                    fixture.FileSystem,
                    NullLogger<ValidationService>.Instance)
                .ValidateAcceptedTurnRawResourceMaterializationAsync()
                .GetAwaiter()
                .GetResult();
            Assert.DoesNotContain(
                issues,
                static issue => issue.Severity == IssueSeverity.Error);
        }
        finally
        {
            fixture.ReacquireLeaseAfterExternalDistribution();
        }

        var published = new CanonicalStateNormalizer(
                fixture.FileSystem,
                NullLogger<CanonicalStateNormalizer>.Instance)
            .BindTo(fixture.Lease)
            .NormalizeAcceptedMechanicsAsync(backups: null)
            .GetAwaiter()
            .GetResult();
        var plan = Assert.IsType<AcceptedMechanicsPlan>(published);
        Assert.NotNull(plan.EffectPlan);
        var transitionId = Assert.Single(plan.EffectPlan.AllocatedTransitionIds);
        Assert.False(fixture.FileSystem.FileExists(EffectAcceptedTurnPlan.CommandPath));
        return transitionId;
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

    private static ResolverScenario CreateLegacyTopologyScenario(string topology)
    {
        var scenario = CreateOutcomeIntentScenario(GuaranteedHealCase());
        scenario.Before["treatment"]!["routes"]![0]!["outcomes"]![0]!["result"]![1]!["legacies"] =
            topology switch
            {
                "baseline" => new System.Text.Json.Nodes.JsonArray(
                    CreateMechanicalLegacyDraft(
                        "t061_topology_a",
                        "t061_topology_a1",
                        "t061_topology_a2"),
                    CreateMechanicalLegacyDraft(
                        "t061_topology_b",
                        "t061_topology_b1",
                        "t061_topology_b2")),
                "missing" => new System.Text.Json.Nodes.JsonArray(
                    CreateMechanicalLegacyDraft(
                        "t061_topology_a",
                        "t061_topology_a1",
                        "t061_topology_a2")),
                "extra" => new System.Text.Json.Nodes.JsonArray(
                    CreateMechanicalLegacyDraft(
                        "t061_topology_a",
                        "t061_topology_a1",
                        "t061_topology_a2"),
                    CreateMechanicalLegacyDraft(
                        "t061_topology_b",
                        "t061_topology_b1",
                        "t061_topology_b2"),
                    CreateMechanicalLegacyDraft(
                        "t061_topology_c",
                        "t061_topology_c1")),
                "reordered" => new System.Text.Json.Nodes.JsonArray(
                    CreateMechanicalLegacyDraft(
                        "t061_topology_b",
                        "t061_topology_b1",
                        "t061_topology_b2"),
                    CreateMechanicalLegacyDraft(
                        "t061_topology_a",
                        "t061_topology_a1",
                        "t061_topology_a2")),
                "merged" => new System.Text.Json.Nodes.JsonArray(
                    CreateMechanicalLegacyDraft(
                        "t061_topology_a",
                        "t061_topology_a1",
                        "t061_topology_a2",
                        "t061_topology_b1",
                        "t061_topology_b2")),
                "split" => new System.Text.Json.Nodes.JsonArray(
                    CreateMechanicalLegacyDraft(
                        "t061_topology_a_first",
                        "t061_topology_a1"),
                    CreateMechanicalLegacyDraft(
                        "t061_topology_a_second",
                        "t061_topology_a2"),
                    CreateMechanicalLegacyDraft(
                        "t061_topology_b",
                        "t061_topology_b1",
                        "t061_topology_b2")),
                _ => throw new ArgumentOutOfRangeException(nameof(topology), topology, null)
            };
        return scenario;
    }

    private static (string LocalRef, string[] LocalApplicationRefs)[] ExpectedLegacyTopology(
        string topology) => topology switch
        {
            "baseline" => new[]
            {
                ("t061_topology_a", new[] { "t061_topology_a1", "t061_topology_a2" }),
                ("t061_topology_b", new[] { "t061_topology_b1", "t061_topology_b2" })
            },
            "missing" => new[]
            {
                ("t061_topology_a", new[] { "t061_topology_a1", "t061_topology_a2" })
            },
            "extra" => new[]
            {
                ("t061_topology_a", new[] { "t061_topology_a1", "t061_topology_a2" }),
                ("t061_topology_b", new[] { "t061_topology_b1", "t061_topology_b2" }),
                ("t061_topology_c", new[] { "t061_topology_c1" })
            },
            "reordered" => new[]
            {
                ("t061_topology_b", new[] { "t061_topology_b1", "t061_topology_b2" }),
                ("t061_topology_a", new[] { "t061_topology_a1", "t061_topology_a2" })
            },
            "merged" => new[]
            {
                ("t061_topology_a", new[]
                {
                    "t061_topology_a1", "t061_topology_a2",
                    "t061_topology_b1", "t061_topology_b2"
                })
            },
            "split" => new[]
            {
                ("t061_topology_a_first", new[] { "t061_topology_a1" }),
                ("t061_topology_a_second", new[] { "t061_topology_a2" }),
                ("t061_topology_b", new[] { "t061_topology_b1", "t061_topology_b2" })
            },
            _ => throw new ArgumentOutOfRangeException(nameof(topology), topology, null)
        };

    private static void AssertLegacyPreparationTopology(
        object preparation,
        IReadOnlyList<(string LocalRef, string[] LocalApplicationRefs)> expected)
    {
        var batches = AsObjects(ReadRequiredProperty(preparation, "EffectOperationBatches"))
            .Select(Assert.IsType<WoundEffectOperationBatch>)
            .ToArray();
        var bindings = AsObjects(ReadRequiredProperty(preparation, "LegacyDraftBindings"));
        Assert.Equal(expected.Count, batches.Length);
        Assert.Equal(expected.Count, bindings.Length);
        for (var index = 0; index < expected.Count; index++)
        {
            var binding = bindings[index];
            var applicationBindings = AsObjects(ReadRequiredProperty(
                binding,
                "ApplicationReferenceBindings"));
            Assert.Equal(expected[index].LocalRef, Convert.ToString(
                ReadRequiredProperty(binding, "LocalLegacyRef")));
            Assert.Equal(
                expected[index].LocalApplicationRefs,
                applicationBindings.Select(value => Convert.ToString(
                    ReadRequiredProperty(value, "LocalRef"))));
            Assert.Equal(expected[index].LocalRef, batches[index].LocalWoundRef);
            Assert.Equal(
                applicationBindings.Select(value => Convert.ToString(
                    ReadRequiredProperty(value, "NamespacedRef"))),
                batches[index].RootApplications.Select(static value => value.ApplicationRef));
        }
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
        Assert.Equal("active", source.State);
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
