using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public void ProcedureReduction_DestinationSlotOverflowRejectsBeforeDieClaim()
    {
        var scenario = CreateDestinationReductionScenario(
            "destination_slot_overflow",
            "periodic_damage",
            "action_control",
            "resistance_modifier");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            fixture.GetAcceptedState());
        var before = fixture.ReadCurrentWound();
        var route = Assert.IsType<MortalWoundProcedureRouteDefinition>(
            Assert.Single(acceptedState.TreatmentDefinition.Routes));
        var success = Assert.Single(
            route.Bands,
            static band => string.Equals(band.Category, "success", StringComparison.Ordinal));

        var simulation = MortalWoundTreatmentWorkingWoundSimulator.Simulate(
            before,
            new[] { success.DeclaredResult });
        var prepared = MortalWoundTreatmentPlanner.PrepareProcedureRequest(
            acceptedState,
            fixture.ReadCurrentHistory(),
            before,
            scenario.OperationKey,
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));

        Assert.False(simulation.IsApplicable);
        Assert.Null(simulation.WorkingWound);
        Assert.False(prepared.IsValid);
        Assert.Null(prepared.Request);
        Assert.Contains(
            prepared.Issues,
            static issue => string.Equals(
                issue.Code,
                "mortal_wound_treatment_procedure_band_inapplicable",
                StringComparison.Ordinal));
    }

    [Fact]
    public void ProcedureReduction_DestinationPowerOverflowRejectsBeforeDieClaim()
    {
        var scenario = CreateDestinationReductionScenario(
            "destination_power_overflow",
            "action_control");
        var component = scenario.Before["consequences"]!["ownedEffectSources"]!
            ["definitions"]![0]!["components"]![0]!.AsObject();
        component["payload"]!["operation"] = "forbid";
        scenario = scenario with
        {
            History = CreateCurrentWoundHistory(scenario.Before)
        };
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            fixture.GetAcceptedState());
        var before = fixture.ReadCurrentWound();
        var route = Assert.IsType<MortalWoundProcedureRouteDefinition>(
            Assert.Single(acceptedState.TreatmentDefinition.Routes));
        var success = Assert.Single(
            route.Bands,
            static band => string.Equals(band.Category, "success", StringComparison.Ordinal));

        var simulation = MortalWoundTreatmentWorkingWoundSimulator.Simulate(
            before,
            new[] { success.DeclaredResult });
        var prepared = MortalWoundTreatmentPlanner.PrepareProcedureRequest(
            acceptedState,
            fixture.ReadCurrentHistory(),
            before,
            scenario.OperationKey,
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));

        Assert.False(simulation.IsApplicable);
        Assert.Null(simulation.WorkingWound);
        Assert.False(prepared.IsValid);
        Assert.Null(prepared.Request);
        Assert.Contains(
            prepared.Issues,
            static issue => string.Equals(
                issue.Code,
                "mortal_wound_treatment_procedure_band_inapplicable",
                StringComparison.Ordinal));
    }

    [Fact]
    public void ProcedureReduction_RejectedBandLeavesLowestFreeDieForNextLegalRoute()
    {
        var scenario = CreateDestinationReductionScenario(
            "rejected_band_die_reuse",
            "action_control");
        var component = scenario.Before["consequences"]!["ownedEffectSources"]!
            ["definitions"]![0]!["components"]![0]!.AsObject();
        component["payload"]!["operation"] = "forbid";
        var invalidRoute = scenario.Before["treatment"]!["routes"]![0]!.AsObject();
        var legalRoute = invalidRoute.DeepClone().AsObject();
        var legalRouteId = scenario.RouteId + "_legal_stabilization";
        legalRoute["routeId"] = legalRouteId;
        legalRoute["outcomes"]![0]!["result"] = new JsonArray(new JsonObject
        {
            ["kind"] = "stabilize"
        });
        scenario.Before["treatment"]!["routes"] = new JsonArray(
            invalidRoute.DeepClone(),
            legalRoute);
        scenario.Before["treatment"]!["knownRouteIds"] = new JsonArray(
            scenario.RouteId,
            legalRouteId);
        scenario = scenario with
        {
            History = CreateCurrentWoundHistory(scenario.Before)
        };

        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            fixture.GetAcceptedState());
        var before = fixture.ReadCurrentWound();
        var history = fixture.ReadCurrentHistory();
        var rejected = MortalWoundTreatmentPlanner.PrepareProcedureRequest(
            acceptedState,
            history,
            before,
            scenario.OperationKey + "_rejected",
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));
        var legal = MortalWoundTreatmentPlanner.PrepareProcedureRequest(
            acceptedState,
            history,
            before,
            scenario.OperationKey + "_legal",
            legalRouteId,
            fixture.AcceptedEventRef(acceptedState));

        Assert.False(rejected.IsValid);
        Assert.Null(rejected.Request);
        Assert.True(legal.IsValid, DescribeIssues(legal.Issues));
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(legal.Request);
        var procedure = Assert.IsType<MortalWoundProcedureCheckAuthority>(
            request.ModeAuthority);
        Assert.Equal(new[] { 0 }, procedure.SourceIndices);
    }

    [Fact]
    public void ProcedureRepeatedStabilization_PublishesWithoutAppendingCompletedRouteAgain()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        var route = scenario.Before["treatment"]!["routes"]![0]!.AsObject();
        route["outcomes"]![0]!["result"] = new JsonArray(new JsonObject
        {
            ["kind"] = "stabilize"
        });
        scenario.Before["treatment"]!["completedRouteIds"] = new JsonArray(
            scenario.RouteId);
        scenario = scenario with
        {
            OperationKey = scenario.OperationKey + "_repeated_stabilization",
            ExpectedIntentCount = 1,
            History = CreateCurrentWoundHistory(scenario.Before),
            SeedCanonicalWoundEffects = true
        };

        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateProcedurePublication(
            fixture,
            ResolveCurrentTreatment(
                fixture,
                "procedure",
                scenario.OperationKey,
                scenario.RouteId),
            "repeated procedure stabilization");
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        Assert.Equal("success", resolution.ResultCategory);
        Assert.Equal("None", resolution.RouteCompletion);

        ComposeAndPublishCoordinatedProcedureTreatment(fixture, flow);

        var wound = fixture.ReadCurrentWound();
        Assert.Equal("stabilized", wound.Care.State);
        Assert.Equal(new[] { scenario.RouteId }, wound.Treatment.CompletedRouteIds);
    }

    [Fact]
    public void ProcedureRepeatedStabilization_RejectsResealedFalseAppendOnceCompletion()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        var route = scenario.Before["treatment"]!["routes"]![0]!.AsObject();
        route["outcomes"]![0]!["result"] = new JsonArray(new JsonObject
        {
            ["kind"] = "stabilize"
        });
        scenario.Before["treatment"]!["completedRouteIds"] = new JsonArray(
            scenario.RouteId);
        scenario = scenario with
        {
            OperationKey = scenario.OperationKey + "_false_append_once",
            ExpectedIntentCount = 1,
            History = CreateCurrentWoundHistory(scenario.Before),
            SeedCanonicalWoundEffects = true
        };

        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey,
            scenario.RouteId);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            flow.AcceptedState);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        Assert.Equal("None", resolution.RouteCompletion);
        var falseCompletion = MortalWoundTreatmentResolution.Create(
            resolution.Mode,
            resolution.Coordinates,
            resolution.AttemptDisposition,
            resolution.ResultCategory,
            resolution.SelectedOutcomeIndex,
            resolution.Interruption,
            resolution.DeclaredResult,
            resolution.OutcomeIntents,
            resolution.CriticalReactionIntent,
            resolution.ConsumptionTrigger,
            resolution.CourseId,
            resolution.CourseMilestoneOrdinal,
            resolution.CourseDisposition,
            request,
            resolution.ModeEvidence,
            resolution.RouteFingerprint,
            "AppendOnce");

        var rejected = MortalWoundTreatmentOutcomePublicationPlanner.Compose(
            acceptedState,
            request,
            falseCompletion,
            acceptedState.CurrentGameMinute);

        Assert.False(rejected.IsValid);
        Assert.Null(rejected.After);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem,
            fixture.Lease));
    }

    [Fact]
    public async Task GuaranteedRepeatedStabilization_PublishesWithoutAppendingCompletedRouteAgain()
    {
        var scenario = CreateScenario(
            "guaranteed_current_capability_proof_stabilizes",
            "guaranteed");
        scenario.Before["treatment"]!["completedRouteIds"] = new JsonArray(
            scenario.RouteId);
        scenario = scenario with
        {
            OperationKey = scenario.OperationKey + "_repeated_stabilization",
            History = CreateCurrentWoundHistory(scenario.Before),
            SeedCanonicalWoundEffects = true
        };

        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "repeated_guaranteed_stabilization");
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        Assert.Equal("success", resolution.ResultCategory);
        Assert.Equal("None", resolution.RouteCompletion);
        var composition = ComposeResourcePublicationResult(fixture, flow);
        Assert.True(composition.IsValid, DescribeIssues(composition.Issues));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(composition.Plan);

        fixture.ReleaseLeaseForExternalDistribution();
        AcceptedTurnCanonicalStateRefresh.Result published;
        try
        {
            published = await AcceptedTurnCanonicalStateRefresh
                .NormalizeAndValidateWithPlanAsync(
                    fixture.FileSystem,
                    new CanonicalStateNormalizer(
                        fixture.FileSystem,
                        Microsoft.Extensions.Logging.Abstractions.NullLogger<
                            CanonicalStateNormalizer>.Instance),
                    new ValidationService(
                        fixture.FileSystem,
                        Microsoft.Extensions.Logging.Abstractions.NullLogger<
                            ValidationService>.Instance),
                    new Dictionary<string, string>(StringComparer.Ordinal));
        }
        finally
        {
            fixture.ReacquireLeaseAfterExternalDistribution();
        }

        Assert.Same(plan, published.MechanicsPlan);
        Assert.Null(published.TreatmentResourcePublicationTransaction);
        Assert.DoesNotContain(
            published.Issues,
            static issue => issue.Severity == IssueSeverity.Error);
        var wound = fixture.ReadCurrentWound();
        Assert.Equal("stabilized", wound.Care.State);
        Assert.Equal(new[] { scenario.RouteId }, wound.Treatment.CompletedRouteIds);
    }

    [Theory]
    [InlineData("stabilize")]
    [InlineData("no_improvement")]
    public void ProcedurePartialSuccessSingleton_PublishesItsDeclaredScalarOutcome(
        string operationKind)
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        var route = scenario.Before["treatment"]!["routes"]![0]!.AsObject();
        route["outcomes"]![1]!["result"] = new JsonArray(new JsonObject
        {
            ["kind"] = operationKind
        });
        scenario.AcceptedState["acceptedDice"] = new JsonArray(13, 7);
        scenario = scenario with
        {
            OperationKey = scenario.OperationKey + "_partial_" + operationKind,
            ExpectedCategory = "partial_success",
            ExpectedNaturalRoll = 13,
            ExpectedIntentCount = 1,
            History = CreateCurrentWoundHistory(scenario.Before),
            SeedCanonicalWoundEffects = true
        };

        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateProcedurePublication(
            fixture,
            ResolveCurrentTreatment(
                fixture,
                "procedure",
                scenario.OperationKey,
                scenario.RouteId),
            "partial-success " + operationKind);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        Assert.Equal("partial_success", resolution.ResultCategory);
        Assert.Equal("None", resolution.RouteCompletion);
        Assert.Collection(
            resolution.OutcomeIntents,
            intent => Assert.Equal(operationKind, intent.Kind));

        ComposeAndPublishCoordinatedProcedureTreatment(fixture, flow);

        var wound = fixture.ReadCurrentWound();
        Assert.Empty(wound.Treatment.CompletedRouteIds);
        Assert.Equal(request.Coordinates.AttemptId, wound.Care.LastAttemptId);
        Assert.Equal(
            operationKind == "stabilize" ? "stabilized" : "untreated",
            wound.Care.State);
        var transition = Assert.Single(
            fixture.ReadCurrentHistory().State!.Transitions,
            static row => string.Equals(row.Kind, "treat", StringComparison.Ordinal));
        Assert.Equal(
            "partial_success",
            transition.TreatmentResult!.Receipt.ResultCategory);
    }

    [Theory]
    [InlineData("remove_sealed_reaction")]
    [InlineData("retarget_sealed_reaction")]
    [InlineData("append_second_reaction")]
    public void ProcedureTreatmentLifecycleAgreement_RejectsAlteredCompleteOrderedArray(
        string alteration)
    {
        var scenario = PrepareProcedurePublicationScenario(CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure"));
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateProcedurePublication(
            fixture,
            ResolveCurrentTreatment(
                fixture,
                "procedure",
                scenario.OperationKey + "_lifecycle_" + alteration,
                scenario.RouteId),
            "treatment lifecycle " + alteration);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            flow.AcceptedState);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        var plan = ComposeCoordinatedProcedurePlan(fixture, flow);
        var original = Assert.IsType<AcceptedMechanicsWoundStageBundle>(
            plan.WoundStageBundle);
        var prepared = original.PreparedPlan;
        var authority = prepared.TreatmentContinuationAuthority;
        Assert.NotNull(authority);
        Assert.True(WoundAcceptedTurnPlanner.TryReadTreatmentContinuation(
            authority!,
            out var continuation));
        Assert.True(WoundAcceptedTurnPlanner.TreatmentContinuationPublicationAgrees(
            authority!,
            original,
            acceptedState,
            request,
            resolution,
            continuation.SemanticFingerprint));

        var originalEffectInput = original.EffectBatchPlan.EffectInput;
        var eventInput = originalEffectInput.EventInput.DeepClone().AsObject();
        var lifecycle = Assert.IsType<JsonArray>(eventInput["lifecycleEvents"]);
        var baseEventIndex = lifecycle
            .Select(static (value, index) => (value, index))
            .First(static pair => pair.value is JsonObject value &&
                string.Equals(
                    value["phase"]?.GetValue<string>(),
                    "owner_turn_end",
                    StringComparison.Ordinal))
            .index;
        var reaction = Assert.Single(lifecycle.OfType<JsonObject>(), static value =>
            string.Equals(
                value["phase"]?.GetValue<string>(),
                "owner_critical_failure",
                StringComparison.Ordinal));
        Assert.True(baseEventIndex < lifecycle.IndexOf(reaction));
        switch (alteration)
        {
            case "remove_sealed_reaction":
                lifecycle.Remove(reaction);
                break;
            case "retarget_sealed_reaction":
                reaction["effectId"] = "effect_fate_shield_newer";
                break;
            case "append_second_reaction":
                var second = reaction.DeepClone().AsObject();
                second["eventRef"] =
                    reaction["eventRef"]!.GetValue<string>() + ":forged_second";
                second["effectId"] = "effect_fate_shield_newer";
                lifecycle.Add(second);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(alteration), alteration, null);
        }

        var alteredInput = originalEffectInput with { EventInput = eventInput };
        var ordinaryCache = new EffectAcceptedTurnPlanCache();
        var ordinary = ordinaryCache.GetOrBuildWoundValidated(
            prepared,
            alteredInput,
            out _);
        Assert.True(ordinary.Success, DescribeIssues(ordinary.Issues));
        var accepted = WoundEffectBatchPlanner.AcceptEffectResult(
            prepared,
            alteredInput,
            ordinary);
        Assert.True(accepted.Success, DescribeIssues(accepted.Issues));
        Assert.True(AcceptedTurnAuthorityRegistry.TryPeekEffectValidated(
            fixture.FileSystem,
            fixture.Lease,
            out var genericBeforeRejection));
        RearmTreatmentPublicationReservationForEffectStageTest(
            fixture,
            continuation);
        var stageRejection = WoundAcceptedTurnPlanAuthority
            .GetOrBuildTreatmentContinuationEffectValidated(
                fixture.FileSystem,
                fixture.Lease,
                prepared,
                alteredInput,
                authority!,
                continuation.ReservationAuthority);
        Assert.False(stageRejection.Success);
        Assert.Null(stageRejection.Plan);
        Assert.Equal(
            "mortal_wound_treatment_publication_lifecycle_mismatch",
            Assert.Single(stageRejection.Issues).Code);
        Assert.True(AcceptedTurnAuthorityRegistry.TryPeekEffectValidated(
            fixture.FileSystem,
            fixture.Lease,
            out var genericAfterRejection));
        Assert.Same(genericBeforeRejection.Plan, genericAfterRejection.Plan);
        AcceptedTurnAuthorityRegistry.AbortMortalWoundTreatmentPublication(
            fixture.FileSystem,
            fixture.Lease,
            continuation.ReservationAuthority);
        var final = WoundAcceptedTurnPlanner.Finalize(prepared, accepted);
        Assert.True(final.Success, DescribeIssues(final.Issues));
        var altered = new AcceptedMechanicsWoundStageBundle(
            original.Input,
            prepared,
            accepted.Plan!,
            final.Plan!);

        Assert.False(WoundAcceptedTurnPlanner.TreatmentContinuationPublicationAgrees(
            authority!,
            altered,
            acceptedState,
            request,
            resolution,
            continuation.SemanticFingerprint));
    }

    [Fact]
    public void ProcedureTreatmentAuthority_RejectsInjectedValidDispelWithOriginalLifecycle()
    {
        var scenario = PrepareProcedurePublicationScenario(CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure"));
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateProcedurePublication(
            fixture,
            ResolveCurrentTreatment(
                fixture,
                "procedure",
                scenario.OperationKey + "_injected_valid_dispel",
                scenario.RouteId),
            "injected valid dispel");
        var plan = ComposeCoordinatedProcedurePlan(fixture, flow);
        var original = Assert.IsType<AcceptedMechanicsWoundStageBundle>(
            plan.WoundStageBundle);
        var commands = original.EffectBatchPlan.EffectInput.RawCommands
            .DeepClone()
            .AsObject();
        commands["effectChanges"] = new JsonArray(new JsonObject
        {
            ["operation"] = "dispel",
            ["effectId"] = "effect_fate_shield_newer",
            ["target"] = new JsonObject
            {
                ["kind"] = "player",
                ["targetId"] = "player_current"
            },
            ["authority"] = new JsonObject
            {
                ["kind"] = "fate",
                ["authorityId"] = "turn_42"
            },
            ["eventRef"] = new JsonObject
            {
                ["kind"] = "accepted_turn",
                ["authorityId"] = "turn_42"
            },
            ["reason"] =
                "A detached caller must not add an otherwise valid Fate dispel."
        });
        var alteredInput = original.EffectBatchPlan.EffectInput with
        {
            RawCommands = commands
        };
        Assert.Equal(
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                original.EffectBatchPlan.EffectInput.EventInput["lifecycleEvents"]!),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                alteredInput.EventInput["lifecycleEvents"]!));

        AssertTreatmentEffectInputRejectedAtStageAndFinal(
            fixture,
            flow,
            original,
            alteredInput);
    }

    [Fact]
    public void ProcedureTreatmentAuthority_RejectsCoTamperedCarrierBaselineAndLifecycle()
    {
        var scenario = PrepareProcedurePublicationScenario(CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure"));
        using var fixture = AcceptedStateFixture.Create(scenario);
        SeedPassiveNpcEffectForLifecycle(fixture);
        var flow = PersistAndRehydrateProcedurePublication(
            fixture,
            ResolveCurrentTreatment(
                fixture,
                "procedure",
                scenario.OperationKey + "_co_tampered_carrier_lifecycle",
                scenario.RouteId),
            "co-tampered carrier baseline and lifecycle");
        var plan = ComposeCoordinatedProcedurePlan(fixture, flow);
        var original = Assert.IsType<AcceptedMechanicsWoundStageBundle>(
            plan.WoundStageBundle);
        var originalInput = original.EffectBatchPlan.EffectInput;
        var acceptedCarriers = Assert.IsType<EffectCarrierCatalogInput>(
            originalInput.AcceptedCarrierBaselines);
        var npcs = Assert.IsType<JsonObject>(acceptedCarriers.NpcEffects)
            .DeepClone()
            .AsObject();
        var npc = Assert.Single(
            npcs["entries"]!.AsArray().OfType<JsonObject>(),
            static row => string.Equals(
                row["NPCId"]?.GetValue<string>(),
                "field_medic_01",
                StringComparison.Ordinal));
        Assert.NotEmpty(npc["activeEffects"]!.AsArray());
        npc["activeEffects"] = new JsonArray();
        var eventInput = originalInput.EventInput.DeepClone().AsObject();
        var lifecycle = Assert.IsType<JsonArray>(eventInput["lifecycleEvents"]);
        var removedLifecycle = lifecycle
            .OfType<JsonObject>()
            .Where(static row => string.Equals(
                row["target"]?["kind"]?.GetValue<string>(),
                "npc",
                StringComparison.Ordinal) && string.Equals(
                row["target"]?["targetId"]?.GetValue<string>(),
                "field_medic_01",
                StringComparison.Ordinal))
            .ToArray();
        Assert.Single(removedLifecycle);
        lifecycle.Remove(removedLifecycle[0]);
        var alteredInput = originalInput with
        {
            AcceptedCarrierBaselines = acceptedCarriers with
            {
                NpcEffects = npcs
            },
            EventInput = eventInput
        };

        AssertTreatmentEffectInputRejectedAtStageAndFinal(
            fixture,
            flow,
            original,
            alteredInput);
    }

    private static void AssertTreatmentEffectInputRejectedAtStageAndFinal(
        AcceptedStateFixture fixture,
        TreatmentFlow flow,
        AcceptedMechanicsWoundStageBundle original,
        EffectAcceptedTurnInput alteredInput)
    {
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            flow.AcceptedState);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        var prepared = original.PreparedPlan;
        var authority = prepared.TreatmentContinuationAuthority;
        Assert.NotNull(authority);
        Assert.True(WoundAcceptedTurnPlanner.TryReadTreatmentContinuation(
            authority!,
            out var continuation));
        var ordinaryCache = new EffectAcceptedTurnPlanCache();
        var ordinary = ordinaryCache.GetOrBuildWoundValidated(
            prepared,
            alteredInput,
            out _);
        Assert.True(ordinary.Success, DescribeIssues(ordinary.Issues));
        var accepted = WoundEffectBatchPlanner.AcceptEffectResult(
            prepared,
            alteredInput,
            ordinary);
        Assert.True(accepted.Success, DescribeIssues(accepted.Issues));
        var final = WoundAcceptedTurnPlanner.Finalize(prepared, accepted);
        Assert.True(final.Success, DescribeIssues(final.Issues));
        var altered = new AcceptedMechanicsWoundStageBundle(
            original.Input,
            prepared,
            accepted.Plan!,
            final.Plan!);

        Assert.True(AcceptedTurnAuthorityRegistry.TryPeekEffectValidated(
            fixture.FileSystem,
            fixture.Lease,
            out var genericBeforeRejection));
        RearmTreatmentPublicationReservationForEffectStageTest(
            fixture,
            continuation);
        var stage = WoundAcceptedTurnPlanAuthority
            .GetOrBuildTreatmentContinuationEffectValidated(
                fixture.FileSystem,
                fixture.Lease,
                prepared,
                alteredInput,
                authority!,
                continuation.ReservationAuthority);
        var finalAgrees = WoundAcceptedTurnPlanner
            .TreatmentContinuationPublicationAgrees(
                authority!,
                altered,
                acceptedState,
                request,
                resolution,
                continuation.SemanticFingerprint);

        Assert.Equal(
            "stage=False;final=False",
            $"stage={stage.Success};final={finalAgrees}");
        Assert.Null(stage.Plan);
        Assert.Equal(
            "mortal_wound_treatment_publication_lifecycle_mismatch",
            Assert.Single(stage.Issues).Code);
        Assert.True(AcceptedTurnAuthorityRegistry.TryPeekEffectValidated(
            fixture.FileSystem,
            fixture.Lease,
            out var genericAfterRejection));
        Assert.Same(genericBeforeRejection.Plan, genericAfterRejection.Plan);
        AcceptedTurnAuthorityRegistry.AbortMortalWoundTreatmentPublication(
            fixture.FileSystem,
            fixture.Lease,
            continuation.ReservationAuthority);
    }

    private static void SeedPassiveNpcEffectForLifecycle(
        AcceptedStateFixture fixture)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            "npc",
            "characteristic_modifier");
        effect["effectId"] = "effect_field_medic_passive_focus";
        effect["target"]!["targetId"] = "field_medic_01";
        effect["source"] = new JsonObject
        {
            ["kind"] = "skill",
            ["sourceId"] = "skill_field_medicine_01",
            ["definitionKey"] = "t070b5-field-medic-passive-focus"
        };
        effect["chronology"]!["createdAtTurn"] = 40;
        effect["chronology"]!["createdEventRef"] =
            "turn_40:field_medic_passive_focus";
        effect["chronology"]!["lastTransitionId"] =
            "effect_transition_field_medic_passive_focus";
        effect["chronology"]!["lastTransitionTurn"] = 40;
        var npcRoot = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["entries"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = "field_medic_01",
                ["activeEffects"] = new JsonArray(effect.DeepClone())
            })
        };
        File.WriteAllText(
            fixture.FileSystem.ResolvePath(EffectCarrierCatalog.NpcPath),
            npcRoot.ToJsonString());
        var identityRoot = JsonNode.Parse(File.ReadAllText(
            fixture.FileSystem.ResolvePath(EffectIdentityState.StatePath)))!
            .AsObject();
        var newIdentity = Assert.IsType<JsonObject>(Assert.Single(
            EffectMaterializationTestFixture.CreateIdentityIndex(effect)
                ["entries"]!
                .AsArray()));
        newIdentity["createdAtTurn"] = 40;
        var transition = Assert.IsType<JsonObject>(Assert.Single(
            newIdentity["transitions"]!.AsArray()));
        transition["transitionId"] =
            "effect_transition_field_medic_passive_focus";
        transition["turn"] = 40;
        transition["eventRef"] = "turn_40:field_medic_passive_focus";
        identityRoot["entries"]!.AsArray().Add(newIdentity.DeepClone());
        File.WriteAllText(
            fixture.FileSystem.ResolvePath(EffectIdentityState.StatePath),
            identityRoot.ToJsonString());
    }

    private static void RearmTreatmentPublicationReservationForEffectStageTest(
        AcceptedStateFixture fixture,
        WoundAcceptedTurnPlanner.TreatmentContinuationView continuation)
    {
        AcceptedTurnAuthorityRegistry.InvalidateCommonValidated(
            fixture.FileSystem,
            fixture.Lease);
        var getState = typeof(AcceptedTurnAuthorityRegistry).GetMethod(
            "GetState",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(getState);
        var state = getState.Invoke(
            null,
            new object[] { fixture.FileSystem, fixture.Lease });
        Assert.NotNull(state);
        var stateType = state.GetType();
        var reservation = stateType.GetField(
            "_mortalWoundTreatmentPublicationReservation",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var fingerprint = stateType.GetField(
            "_mortalWoundTreatmentReservedFingerprint",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(reservation);
        Assert.NotNull(fingerprint);
        reservation.SetValue(state, continuation.ReservationAuthority);
        fingerprint.SetValue(state, continuation.SemanticFingerprint);
    }

    [Fact]
    public void ProcedureFreshFateDuplicate_ProvisionalRollbackCannotCorruptConfirmedPlan()
    {
        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        var route = scenario.Before["treatment"]!["routes"]![0]!.AsObject();
        var skillRequirement = route["requirements"]![1]!.DeepClone();
        route["requirements"] = new JsonArray(skillRequirement);
        route["resourcePolicy"] = Policy(new JsonArray(), new JsonArray());
        route["resolution"]!["modifierSource"]!["requirementIndex"] = 0;
        scenario = scenario with
        {
            OperationKey = scenario.OperationKey + "_fresh_cached_duplicate",
            ExpectedIntentCount = 1,
            History = CreateCurrentWoundHistory(scenario.Before),
            SeedCanonicalWoundEffects = true
        };
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey,
            scenario.RouteId);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            flow.AcceptedState);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var procedure = Assert.IsType<MortalWoundProcedureCheckAuthority>(
            request.ModeAuthority);
        Assert.True(request.HasNewProvisionalClaimCleanup);
        var confirmation = ConfirmTreatmentResources(
            fixture,
            new[] { request });
        Assert.True(confirmation.IsValid, DescribeIssues(confirmation.Issues));
        var first = ComposeResourcePublicationResult(fixture, flow);
        Assert.True(first.IsValid, DescribeIssues(first.Issues));
        var originalPlan = Assert.IsType<AcceptedMechanicsPlan>(first.Plan);
        Assert.True(AcceptedTurnAuthorityRegistry
            .HasLiveMortalWoundProcedureReservationAgreement(
                fixture.FileSystem,
                fixture.Lease,
                acceptedState,
                procedure));
        var legacyReport = new JsonObject
        {
            ["eventType"] = "owner_critical_failure",
            ["target"] = new JsonObject
            {
                ["kind"] = "player",
                ["targetId"] = "player_current"
            },
            ["evidence"] = new JsonObject
            {
                ["kind"] = "mortal_action_roll",
                ["rollMode"] = "normal",
                ["diceIndexes"] = new JsonArray(0),
                ["selectedIndex"] = 0,
                ["selectedValue"] = 1,
                ["originalOutcome"] = "critical_failure",
                ["resolvedOutcome"] = "failure"
            },
            ["reason"] = "An exact cached plan must retain its claims."
        };
        using var reportDocument = JsonDocument.Parse(legacyReport.ToJsonString());

        var duplicate = ComposeResourcePublicationResult(
            fixture,
            flow,
            new GameResponse
            {
                EffectEventReports =
                    new[] { reportDocument.RootElement.Clone() }
            });

        Assert.False(duplicate.IsValid);
        Assert.Null(duplicate.Plan);
        Assert.Equal(
            "wound_treatment_fate_reaction_cross_surface_duplicate",
            Assert.Single(duplicate.Issues).Code);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out _,
            out var retained));
        Assert.True(retained.Success, DescribeIssues(retained.Issues));
        Assert.Same(originalPlan, retained.Plan);
        Assert.True(AcceptedTurnAuthorityRegistry
            .HasLiveMortalWoundProcedureReservationAgreement(
                fixture.FileSystem,
                fixture.Lease,
                acceptedState,
                procedure));
    }

    [Fact]
    public void ProcedureSingletonStabilization_PublishesWoundAnchorsRouteAndHistoryOnce()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        var route = scenario.Before["treatment"]!["routes"]![0]!.AsObject();
        route["outcomes"]![0]!["result"] = new JsonArray(new JsonObject
        {
            ["kind"] = "stabilize"
        });
        scenario = scenario with
        {
            OperationKey = scenario.OperationKey + "_singleton_stabilization",
            ExpectedIntentCount = 1,
            History = CreateCurrentWoundHistory(scenario.Before),
            SeedCanonicalWoundEffects = true
        };

        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateProcedurePublication(
            fixture,
            ResolveCurrentTreatment(
                fixture,
                "procedure",
                scenario.OperationKey,
                scenario.RouteId),
            "singleton stabilization");
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        Assert.Equal("success", resolution.ResultCategory);
        Assert.Equal("AppendOnce", resolution.RouteCompletion);
        Assert.Collection(
            resolution.OutcomeIntents,
            static intent => Assert.Equal("stabilize", intent.Kind));

        ComposeAndPublishCoordinatedProcedureTreatment(fixture, flow);

        var wound = fixture.ReadCurrentWound();
        Assert.Equal("stabilized", wound.Care.State);
        Assert.Equal(request.Coordinates.Turn, wound.Care.StabilizedAtTurn);
        Assert.Equal(request.Coordinates.AttemptId, wound.Care.LastAttemptId);
        Assert.Contains(request.Coordinates.RouteId, wound.Treatment.CompletedRouteIds);
        Assert.DoesNotContain("not_stabilized", wound.Recovery.Blockers);
        var recoveryAnchor = Assert.IsType<WoundRecoveryAnchor>(
            wound.Recovery.RecoveryAnchor);
        Assert.Equal("stabilization", recoveryAnchor.AnchorKind);
        Assert.Equal(1_260, recoveryAnchor.AnchorMinute);
        Assert.Equal(wound.LastTransition.TransitionId, recoveryAnchor.AnchorTransitionId);
        Assert.Null(wound.Recovery.DeteriorationAnchor);

        var transition = Assert.Single(
            fixture.ReadCurrentHistory().State!.Transitions,
            static row => string.Equals(row.Kind, "treat", StringComparison.Ordinal));
        Assert.Equal(wound.LastTransition.TransitionId, transition.TransitionId);
        Assert.Equal(request.Coordinates.OperationKey, transition.OperationKey);
        Assert.Equal(request.Coordinates.AttemptId, transition.AttemptId);
        Assert.Equal(request.RequestFingerprint, transition.SourceFingerprint);
        Assert.False(transition.Terminal);
        Assert.Equal("success", transition.TreatmentResult!.Receipt.ResultCategory);
    }

    [Fact]
    public void ProcedureNoImprovement_PublishesAttemptHistoryWithoutInventingImprovement()
    {
        var scenario = CreateScenario(
            "procedure_disadvantage_uses_two_contiguous_dice",
            "procedure");
        scenario = scenario with
        {
            OperationKey =
                "operation_t070_b5_procedure_no_improvement_publication",
            History = CreateCurrentWoundHistory(scenario.Before),
            SeedCanonicalWoundEffects = true
        };
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateProcedurePublication(
            fixture,
            ResolveCurrentTreatment(
                fixture,
                "procedure",
                scenario.OperationKey,
                scenario.RouteId),
            "no improvement");
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        Assert.Equal("failed_attempt", resolution.ResultCategory);
        Assert.Equal("None", resolution.RouteCompletion);
        Assert.Collection(
            resolution.OutcomeIntents,
            static intent => Assert.Equal("no_improvement", intent.Kind));
        var before = CanonicalWoundRoot(flow.Before);

        ComposeAndPublishCoordinatedProcedureTreatment(fixture, flow);

        var wound = fixture.ReadCurrentWound();
        var after = CanonicalWoundRoot(wound);
        AssertCanonicalWoundMembersUnchanged(
            before,
            after,
            "severity",
            "complications",
            "consequences",
            "treatment",
            "recovery",
            "relations");
        var expectedCare = before["care"]!.DeepClone().AsObject();
        expectedCare["lastAttemptId"] = request.Coordinates.AttemptId;
        Assert.True(
            JsonNode.DeepEquals(expectedCare, after["care"]),
            "A no-improvement result may record the accepted attempt but must not invent care progress.");

        var transition = Assert.Single(
            fixture.ReadCurrentHistory().State!.Transitions,
            static row => string.Equals(row.Kind, "treat", StringComparison.Ordinal));
        var treatmentResult = Assert.IsType<MortalWoundTreatmentPersistedResult>(
            transition.TreatmentResult);
        Assert.Equal(request.Coordinates.AttemptId, transition.AttemptId);
        Assert.False(transition.Terminal);
        Assert.Equal(
            "failed_attempt",
            treatmentResult.Receipt.ResultCategory);
        Assert.Equal(
            "AcceptedTerminal",
            treatmentResult.Receipt.AttemptDisposition);
    }

    [Theory]
    [InlineData("combatant", "combatant_wounded_01")]
    [InlineData("combatant_member", "combatant_member_wounded_01")]
    public void ProcedureNoImprovement_CombatantCarriersPublishOnlyTheExactTarget(
        string targetKind,
        string targetId)
    {
        var scenario = CreateCombatNoImprovementProcedureScenario(
            targetKind,
            targetId);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var targetBefore = ReadCanonicalBytes(fixture, fixture.TargetCarrierPath);
        var playerBefore = ReadCanonicalBytes(
            fixture,
            WoundCarrierCatalog.PlayerPath);
        var flow = PersistAndRehydrateProcedurePublication(
            fixture,
            ResolveCurrentTreatment(
                fixture,
                "procedure",
                scenario.OperationKey,
                scenario.RouteId),
            targetKind + " no-improvement procedure");
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        Assert.Equal("failed_attempt", resolution.ResultCategory);
        Assert.Collection(
            resolution.OutcomeIntents,
            static intent => Assert.Equal("no_improvement", intent.Kind));

        ComposeAndPublishCoordinatedProcedureTreatment(fixture, flow);

        Assert.False(targetBefore.SequenceEqual(
            ReadCanonicalBytes(fixture, fixture.TargetCarrierPath)));
        Assert.Equal(
            playerBefore,
            ReadCanonicalBytes(fixture, WoundCarrierCatalog.PlayerPath));
        var after = fixture.AssertCurrentWoundCoordinate(
            targetKind,
            targetId,
            fixture.TargetCarrierPath);
        Assert.Equal(flow.Before.Severity.Rank, after.Severity.Rank);
        Assert.Equal(request.Coordinates.AttemptId, after.Care.LastAttemptId);
        Assert.Single(
            fixture.ReadCurrentHistory().State!.Transitions,
            static row => string.Equals(
                row.Kind,
                "treat",
                StringComparison.Ordinal));
    }

    [Fact]
    public void ProcedureNoResourceAttempt_StillRequiresCoordinatedTransaction()
    {
        var scenario = CreateNoResourceNoImprovementProcedureScenario();
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateProcedurePublication(
            fixture,
            ResolveCurrentTreatment(
                fixture,
                "procedure",
                scenario.OperationKey,
                scenario.RouteId),
            "no-resource procedure");
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        Assert.Equal("not_required", request.ResourceAuthority.ReservationDisposition);
        Assert.Null(request.ResourceAuthority.ReservationId);
        Assert.Empty(request.ResourceAuthority.Claims);

        var plan = ComposeCoordinatedProcedurePlan(fixture, flow);
        using var publication = PublishCachedResourcePlanOpen(
            fixture,
            flow,
            plan,
            requiresConfirmedResourceHold: false);

        Assert.NotNull(publication.TransactionToken);
        publication.CompleteAtFullPipelineEnd();
        Assert.Single(
            fixture.ReadCurrentHistory().State!.Transitions,
            static row => string.Equals(row.Kind, "treat", StringComparison.Ordinal));
    }

    [Fact]
    public void ProcedureScalarOutcomePlanner_IsDeterministicWriteFreeAndRejectsChangedCompletion()
    {
        var scenario = CreateScenario(
            "procedure_disadvantage_uses_two_contiguous_dice",
            "procedure");
        scenario = scenario with
        {
            History = CreateCurrentWoundHistory(scenario.Before),
            SeedCanonicalWoundEffects = true,
            OperationKey = "operation_t070_b5_scalar_outcome_planner"
        };
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey,
            scenario.RouteId);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            flow.AcceptedState);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        var carrierBefore = ReadCanonicalBytes(fixture, fixture.TargetCarrierPath);
        var identityBefore = ReadCanonicalBytes(fixture, WoundIdentityState.StatePath);
        var historyBefore = ReadCanonicalBytes(fixture, WoundHistoryState.HistoryPath);

        var first = MortalWoundTreatmentOutcomePublicationPlanner.Compose(
            acceptedState,
            request,
            resolution,
            acceptedState.CurrentGameMinute);
        var second = MortalWoundTreatmentOutcomePublicationPlanner.Compose(
            acceptedState,
            request,
            resolution,
            acceptedState.CurrentGameMinute);

        Assert.True(first.IsValid, DescribeIssues(first.Issues));
        Assert.True(second.IsValid, DescribeIssues(second.Issues));
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(first.TransitionId, second.TransitionId);
        Assert.Equal(
            WoundMaterializationContract.SerializeCanonical(first.After!),
            WoundMaterializationContract.SerializeCanonical(second.After!));
        Assert.Equal(carrierBefore, ReadCanonicalBytes(fixture, fixture.TargetCarrierPath));
        Assert.Equal(identityBefore, ReadCanonicalBytes(fixture, WoundIdentityState.StatePath));
        Assert.Equal(historyBefore, ReadCanonicalBytes(fixture, WoundHistoryState.HistoryPath));

        var changedCompletion = MortalWoundTreatmentResolution.Create(
            resolution.Mode,
            resolution.Coordinates,
            resolution.AttemptDisposition,
            resolution.ResultCategory,
            resolution.SelectedOutcomeIndex,
            resolution.Interruption,
            resolution.DeclaredResult,
            resolution.OutcomeIntents,
            resolution.CriticalReactionIntent,
            resolution.ConsumptionTrigger,
            resolution.CourseId,
            resolution.CourseMilestoneOrdinal,
            resolution.CourseDisposition,
            request,
            resolution.ModeEvidence,
            resolution.RouteFingerprint,
            "AppendOnce");
        var rejected = MortalWoundTreatmentOutcomePublicationPlanner.Compose(
            acceptedState,
            request,
            changedCompletion,
            acceptedState.CurrentGameMinute);

        Assert.False(rejected.IsValid);
        Assert.Null(rejected.After);
        Assert.Contains(
            rejected.Issues,
            static issue => issue.Code ==
                "mortal_wound_treatment_publication_slice_unsupported");
        Assert.Equal(carrierBefore, ReadCanonicalBytes(fixture, fixture.TargetCarrierPath));
        Assert.Equal(identityBefore, ReadCanonicalBytes(fixture, WoundIdentityState.StatePath));
        Assert.Equal(historyBefore, ReadCanonicalBytes(fixture, WoundHistoryState.HistoryPath));
    }

    private static ResolverScenario CreateNoResourceNoImprovementProcedureScenario()
    {
        var scenario = CreateScenario(
            "procedure_disadvantage_uses_two_contiguous_dice",
            "procedure");
        var route = scenario.Before["treatment"]!["routes"]![0]!.AsObject();
        var skillRequirement = route["requirements"]![1]!.DeepClone();
        route["requirements"] = new JsonArray(skillRequirement);
        route["resourcePolicy"] = Policy(new JsonArray(), new JsonArray());
        route["resolution"]!["modifierSource"]!["requirementIndex"] = 0;
        return scenario with
        {
            OperationKey = "operation_t070_b5_no_resource_procedure",
            ExpectedIntentCount = 1,
            History = CreateCurrentWoundHistory(scenario.Before),
            SeedCanonicalWoundEffects = true
        };
    }

    private static ResolverScenario CreateDestinationReductionScenario(
        string suffix,
        params string[] profiles)
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        scenario.Before["severity"]!["value"] = "III";
        scenario.Before["severity"]!["rank"] = 3;
        scenario.Before["severity"]!["maximumAtCreation"] = "III";
        scenario.Before["consequences"]!["slotBudget"] = 3;
        scenario.Before["consequences"]!["slotsUsed"] = profiles.Length;
        var roots = profiles.Select((profile, index) => (
            EffectId: $"effect_destination_{index + 1}",
            DefinitionKey: $"definition_destination_{index + 1}",
            Profile: profile)).ToArray();
        scenario.Before["consequences"]!["ownedEffectSources"] =
            WoundContractTestData.CreateOwnedEffectSourcesForTarget(
                scenario.Before["woundId"]!.GetValue<string>(),
                "mortal_world",
                "player",
                roots);
        scenario.Before["consequences"]!["entries"] = new JsonArray(
            profiles.Select((profile, index) => (JsonNode)new JsonObject
            {
                ["slot"] = index + 1,
                ["profileKey"] = profile,
                ["effectId"] = roots[index].EffectId,
                ["readableSummary"] = $"Destination projection slot {index + 1}."
            }).ToArray());
        var route = scenario.Before["treatment"]!["routes"]![0]!.AsObject();
        route["outcomes"]![0]!["result"] = new JsonArray(new JsonObject
        {
            ["kind"] = "reduce_severity",
            ["steps"] = 1
        });
        return scenario with
        {
            OperationKey = scenario.OperationKey + "_" + suffix,
            ExpectedIntentCount = 1,
            History = CreateCurrentWoundHistory(scenario.Before)
        };
    }

    private static ResolverScenario CreateCombatNoImprovementProcedureScenario(
        string targetKind,
        string targetId)
    {
        var scenario = CreateScenario(
            "procedure_disadvantage_uses_two_contiguous_dice",
            "procedure");
        var carrierPath = AcceptedStateFixture.ResolveTargetCarrierPath(targetKind);
        var before = WoundContractTestData.CreateActiveWound(
            woundId: scenario.Before["woundId"]!.GetValue<string>(),
            ownerKind: targetKind,
            ownerId: targetId,
            carrierPath: carrierPath);
        before["consequences"]!["ownedEffectSources"]!["definitions"]!
            .AsArray().RemoveAt(1);
        before["consequences"]!["ownedEffectSources"]!["rootBindings"]!
            .AsArray().RemoveAt(1);
        before["consequences"]!["entries"]!.AsArray().RemoveAt(1);
        before["consequences"]!["slotsUsed"] = 1;
        var route = scenario.Before["treatment"]!["routes"]![0]!
            .DeepClone()
            .AsObject();
        route["requirements"] = new JsonArray(new JsonObject
        {
            ["kind"] = "skill_tier",
            ["capabilityRef"] = "field_medicine",
            ["minimumTier"] = 2,
            ["actorRole"] = "provider"
        });
        route["resourcePolicy"] = Policy(new JsonArray(), new JsonArray());
        route["resolution"]!["modifierSource"] = new JsonObject
        {
            ["kind"] = "resolved_skill_tier",
            ["requirementIndex"] = 0
        };
        before["treatment"]!["routes"] = new JsonArray(route);
        before["treatment"]!["knownRouteIds"] = new JsonArray(
            route["routeId"]!.DeepClone());
        var acceptedState = scenario.AcceptedState.DeepClone().AsObject();
        acceptedState["targetKind"] = targetKind;
        acceptedState["targetId"] = targetId;
        acceptedState["skillRows"] = new JsonArray(
            "skill_field_medicine_npc_01");
        return scenario with
        {
            AcceptedState = acceptedState,
            Before = before,
            OperationKey =
                "operation_t070_b5_no_improvement_" + targetKind,
            History = CreateCurrentWoundHistory(before),
            SeedCanonicalWoundEffects = true
        };
    }

    private static ResolverScenario PrepareProcedurePublicationScenario(
        ResolverScenario scenario) => scenario with
    {
        History = CreateCurrentWoundHistory(scenario.Before),
        SeedCanonicalWoundEffects = true
    };

    private static TreatmentFlow PersistAndRehydrateProcedurePublication(
        AcceptedStateFixture fixture,
        TreatmentFlow initial,
        string boundary)
    {
        PersistTreatmentCommand(
            fixture,
            ComposeTreatmentCommand(
                initial,
                "The accepted procedure is persisted before canonical publication."));
        var initialRequest = Assert.IsType<MortalWoundTreatmentAttemptRequest>(
            initial.Request);
        var restored = Assert.Single(
            AssertValidPersistedCatalog(
                RestoreCurrentPersistedTreatmentCatalog(fixture),
                boundary + " persisted procedure"),
            candidate => string.Equals(
                Assert.IsType<MortalWoundTreatmentAttemptRequest>(candidate)
                    .RequestFingerprint,
                initialRequest.RequestFingerprint,
                StringComparison.Ordinal));
        var rehydrated = RehydratePersistedTreatment(
            fixture,
            "procedure",
            restored);
        Assert.Equal(
            CanonicalValue(initial.Request),
            CanonicalValue(rehydrated.Request));
        Assert.Equal(
            CanonicalValue(initial.Resolution),
            CanonicalValue(rehydrated.Resolution));
        return rehydrated;
    }

    private static AcceptedMechanicsPlan ComposeAndPublishCoordinatedProcedureTreatment(
        AcceptedStateFixture fixture,
        TreatmentFlow flow,
        GameResponse? proposal = null)
    {
        var plan = ComposeCoordinatedProcedurePlan(fixture, flow, proposal);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        using var publication = PublishCachedResourcePlanOpen(
            fixture,
            flow,
            plan,
            requiresConfirmedResourceHold: string.Equals(
                request.ResourceAuthority.ReservationDisposition,
                "held",
                StringComparison.Ordinal));
        publication.CompleteAtFullPipelineEnd();
        return plan;
    }

    private static AcceptedMechanicsPlan ComposeCoordinatedProcedurePlan(
        AcceptedStateFixture fixture,
        TreatmentFlow flow,
        GameResponse? proposal = null)
    {
        var result = ComposeResourcePublicationResult(fixture, flow, proposal);
        Assert.True(result.IsValid, DescribeIssues(result.Issues));
        Assert.Empty(result.Issues);
        return Assert.IsType<AcceptedMechanicsPlan>(result.Plan);
    }

    private static JsonObject CanonicalWoundRoot(WoundMaterializationEnvelope wound) =>
        JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(wound))!
            .AsObject();

    private static void AssertCanonicalWoundMembersUnchanged(
        JsonObject before,
        JsonObject after,
        params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            Assert.True(
                JsonNode.DeepEquals(before[propertyName], after[propertyName]),
                $"No-improvement publication changed wound.{propertyName}.");
        }
    }
}
