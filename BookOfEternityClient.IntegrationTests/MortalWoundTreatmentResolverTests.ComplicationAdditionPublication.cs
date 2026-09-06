using System.Text.Json.Nodes;
using System.Collections.Immutable;
using System.Reflection;
using System.Text.RegularExpressions;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    private static JsonObject AdditionOperation(string localRef, bool effectless = false)
    {
        var draft = CreateEffectfulComplicationDraft(localRef);
        if (effectless) draft["consequenceDefinitions"] = new JsonArray();
        else
        {
            draft["consequenceDefinitions"]![0]!["definition"]!["definitionKey"] = "definition_" + localRef;
            draft["consequenceDefinitions"]![0]!["definition"]!["stacking"]!["stackKey"] = "stack_" + localRef;
            draft["consequenceDefinitions"]![0]!["definition"]!["components"]![0]!["payload"]!["action"] =
                localRef.EndsWith("_b", StringComparison.Ordinal) ? "use_item" : "movement";
        }
        return new() { ["kind"] = "add_complication", ["complicationDraft"] = draft };
    }

    [Fact]
    public void ComplicationAdditionPublication_PartialSuccessKeepsMixedApprovedOutcome()
    {
        var scenario = CreateRecoveryPublicationScenario("procedure", "partial_success", "a1");
        scenario.Before["treatment"]!["routes"]![0]!["outcomes"]!.AsArray().OfType<JsonObject>()
            .Single(row => row["category"]!.GetValue<string>() == "partial_success")["result"]!.AsArray().Add(AdditionOperation("partial_edge"));
        scenario = PrepareProcedurePublicationScenario(scenario with { ExpectedIntentCount = 2 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        var before = fixture.ReadCurrentWound();
        var resourceBefore = fixture.ReadNpcItemCount("sterile_thread");
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId), "mixed partial success");
        var resolution = (MortalWoundTreatmentResolution)flow.Resolution;
        Assert.Equal("partial_success", resolution.ResultCategory);
        Assert.Equal("None", resolution.RouteCompletion);
        ComposeAndPublishCoordinatedTreatment(fixture, flow);
        var after = fixture.ReadCurrentWound();
        Assert.Equal(before.Recovery.CurrentStepProgress + 1, after.Recovery.CurrentStepProgress);
        Assert.Equal(before.Severity, after.Severity);
        Assert.Contains(after.Consequences.OwnedEffectSources.RootBindings, row => row.EffectId == "effect_t070_recovery_characteristic");
        var added = Assert.IsType<MortalWoundAddComplicationOutcomeIntent>(resolution.OutcomeIntents[1]);
        var complication = Assert.Single(after.Complications, row => row.ComplicationId == added.ComplicationId);
        var created = fixture.ReadEffectIdentityIndex()["entries"]!.AsArray().Single(row => row!["effectId"]!.GetValue<string>() == Assert.Single(complication.OwnedEffectIds))!;
        Assert.Empty(created["transitions"]![0]!["sourceEffectIds"]!.AsArray());
        Assert.Equal(resourceBefore - 1, fixture.ReadNpcItemCount("sterile_thread"));
        var treatment = Assert.Single(fixture.ReadCurrentHistory().State!.Transitions, row => row.Kind == "treat");
        AssertClosedTreatmentReceipt(treatment.TreatmentResult!.Receipt, flow.Request, flow.Resolution);
    }

    [Theory]
    [InlineData("a,r")]
    [InlineData("r,a")]
    [InlineData("a,b")]
    public void ComplicationAdditionPublication_OrderedMixedGraphMaterializesOnce(string order)
    {
        var scenario = CreateAdditionPublicationScenario(false);
        var tokens = order.Split(',');
        scenario.Before["treatment"]!["routes"]![0]!["outcomes"]!.AsArray().OfType<JsonObject>()
            .Single(row => row["category"]!.GetValue<string>() == "failed_attempt")["result"] =
            new JsonArray(tokens.Select(token => (JsonNode)(token == "r"
                ? new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 }
                : AdditionOperation("mixed_" + token))).ToArray());
        scenario = PrepareProcedurePublicationScenario(scenario with { ExpectedIntentCount = tokens.Length });
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId), "ordered additions");
        var plan = ComposeCoordinatedTreatmentPlan(fixture, flow);
        var batch = Assert.Single(plan.WoundStageBundle!.PreparedPlan.EffectOperationBatches);
        var reduction = tokens.Contains("r");
        Assert.Equal(tokens.Count(token => token != "r") + (reduction ? 1 : 0), batch.RootApplications.Count);
        Assert.Equal(reduction ? 1 : 0, batch.TerminalOperations.Count);
        Assert.All(batch.RootApplications.Where(row => row.DefinitionKey.StartsWith("definition_mixed_", StringComparison.Ordinal)), row => Assert.Null(row.PriorRootEffectId));
        if (reduction) Assert.Equal("effect_t070_recovery_characteristic",
            Assert.Single(batch.RootApplications, row => row.DefinitionKey == "definition_t070_recovery_characteristic").PriorRootEffectId);
        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan)) publication.CompleteAtFullPipelineEnd();
        var after = fixture.ReadCurrentWound();
        Assert.Equal(reduction ? 2 : 3, after.Severity.Rank);
        Assert.Equal(tokens.Count(token => token != "r"), after.Complications.Count);
        Assert.Equal(Enumerable.Range(1, after.Consequences.Entries.Count), after.Consequences.Entries.Select(row => row.Slot));
        Assert.Single(fixture.ReadCurrentHistory().State!.Transitions, row => row.Kind == "treat");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ComplicationAdditionPublication_OrderedMixedGraphMaterializesOnce_SelectiveLineage(bool terminalRoot)
    {
        var scenario = CreateRemovalModeScenario("procedure", "failed_attempt", "m");
        var lineage = CreateRemovalLineageScenario(false, rootState: terminalRoot ? "removed" : "active");
        scenario.Before["consequences"] = lineage.Before["consequences"]!.DeepClone();
        scenario.Before["complications"] = lineage.Before["complications"]!.DeepClone();
        foreach (var definition in scenario.Before["consequences"]!["ownedEffectSources"]!["definitions"]!.AsArray())
            definition!["links"]![0]!["targetId"] = scenario.Before["woundId"]!.DeepClone();
        scenario.Before["treatment"]!["routes"]![0]!["outcomes"]!.AsArray().OfType<JsonObject>()
            .Single(row => row["category"]!.GetValue<string>() == "failed_attempt")["result"]!.AsArray().Add(AdditionOperation("replacement"));
        scenario = PrepareProcedurePublicationScenario(scenario with { LineageSeed = lineage.LineageSeed, ExpectedIntentCount = 2 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        var before = fixture.ReadCurrentWound();
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId), "selective lineage with addition");
        var plan = ComposeCoordinatedTreatmentPlan(fixture, flow);
        var batch = Assert.Single(plan.WoundStageBundle!.PreparedPlan.EffectOperationBatches);
        Assert.Equal((terminalRoot ? new[] { "effect_t070_removal_child" } : new[] { "effect_t070_removal_reaction", "effect_t070_removal_child" }).OrderBy(id => id, StringComparer.Ordinal),
            batch.TerminalOperations.Select(row => row.EffectId));
        Assert.Null(Assert.Single(batch.RootApplications).PriorRootEffectId);
        var bundle = plan.WoundStageBundle!;
        var tree = CaptureResolverFixtureTree(fixture.Root);
        foreach (var mutation in new[] { "terminal_delete", "terminal_change", "lineage_change" })
        {
            var forged = ResealTreatmentPreparedTopology(bundle.PreparedPlan, mutation);
            var changedBundle = (AcceptedMechanicsWoundStageBundle)typeof(object)
                .GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(bundle, null)!;
            typeof(AcceptedMechanicsWoundStageBundle).GetField("_preparedPlan", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(changedBundle, forged);
            Assert.False(WoundAcceptedTurnPlanner.TreatmentContinuationFinalPlanAgrees(forged.TreatmentContinuationAuthority!, changedBundle), mutation);
            Assert.True(WoundAcceptedTurnPlanner.TryReadTreatmentContinuation(forged.TreatmentContinuationAuthority!, out var continuation));
            Assert.False(WoundAcceptedTurnPlanner.TreatmentContinuationPublicationAgrees(forged.TreatmentContinuationAuthority!, changedBundle,
                (MortalWoundTreatmentAcceptedStateAuthority)flow.AcceptedState, (MortalWoundTreatmentAttemptRequest)flow.Request,
                (MortalWoundTreatmentResolution)flow.Resolution, continuation.SemanticFingerprint), mutation);
            AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        }
        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan)) publication.CompleteAtFullPipelineEnd();
        var after = fixture.ReadCurrentWound();
        Assert.Equal(before.Severity, after.Severity);
        Assert.Single(after.Complications);
        Assert.Contains(after.Consequences.OwnedEffectSources.RootBindings, row => row.EffectId == "effect_t066_course_base");
        Assert.DoesNotContain(after.Consequences.OwnedEffectSources.Definitions,
            row => row.GetProperty("definitionKey").GetString() == "definition_t070_removal_child");
    }

    [Fact]
    public void ComplicationAdditionPublication_PostWriteRollbackRetryAndColdReplay()
    {
        var scenario = CreateAdditionPublicationScenario(false);
        var fault = new ResourcePublicationFailureInjection();
        using var fixture = AcceptedStateFixture.Create(scenario, new FileSystemManagerHooks
        { BeforeCanonicalMutationAsync = fault.BeforeCanonicalMutationAsync });
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId), "addition rollback");
        var plan = ComposeCoordinatedTreatmentPlan(fixture, flow);
        var tree = CaptureResolverFixtureTree(fixture.Root);
        fault.Arm(WoundHistoryState.HistoryPath, fixture.TargetCarrierPath,
            ReadCanonicalBytes(fixture, fixture.TargetCarrierPath), fixture.FileSystem);
        var exception = Assert.Throws<CanonicalStateWriteException>(() => PublishCachedResourcePlanOpen(fixture, flow, plan));
        Assert.Equal(WoundHistoryState.HistoryPath, exception.RelativePath);
        Assert.True(fault.Fired);
        Assert.True(fault.ObservedEarlierResourceWrite);
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan)) publication.CompleteAtFullPipelineEnd();
        Assert.Single(fixture.ReadCurrentWound().Complications);
        Assert.Single(fixture.ReadCurrentHistory().State!.Transitions, row => row.Kind == "treat");
        fixture.RestartForReplay();
        var publishedTree = CaptureResolverFixtureTree(fixture.Root);
        Assert.Equal("ExactReplay", Convert.ToString(ReadRequiredProperty(ProbePublishedTreatment(fixture, flow.Request), "Status")));
        AssertResolverFixtureTreeUnchanged(fixture.Root, publishedTree);
    }

    [Fact]
    public void ComplicationAdditionPublication_RequiredEmptyBatchRejectsMissingOrBorrowedAuthority()
    {
        var scenario = CreateAdditionPublicationScenario(true);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId), "empty addition proof");
        var plan = ComposeCoordinatedTreatmentPlan(fixture, flow);
        var bundle = plan.WoundStageBundle!;
        var prepared = bundle.PreparedPlan;
        var batch = Assert.Single(prepared.EffectOperationBatches);
        Assert.True(WoundAcceptedTurnPlanner.TryReadTreatmentContinuation(prepared.TreatmentContinuationAuthority!, out var continuation));
        var missing = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(continuation.OutcomePreparation,
            continuation.Resolution, null, new Dictionary<string, EffectAcceptedApplicationResult>());
        Assert.Contains(missing.Issues, issue => issue.Code == "mortal_wound_treatment_outcome_effect_handoff_mismatch" &&
            issue.Expected == "one authenticated treatment graph batch and exact application map (including empty)");
        var tree = CaptureResolverFixtureTree(fixture.Root);
        foreach (var axis in new[] { "batch", "lineage", "foreign_proof" })
        {
            var authority = prepared.TreatmentContinuationAuthority!;
            var batches = axis == "batch" ? Array.Empty<WoundEffectOperationBatch>() : new[] { axis == "lineage"
                ? ResealRemovalBatch(batch, lineage: Array.Empty<WoundRootLineageAuthorityRow>()) : batch };
            if (axis == "foreign_proof")
            {
                authority = typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(authority, null)!;
                authority.GetType().GetField("_rematerializationAuthority", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .SetValue(authority, continuation.RematerializationAuthority! with { AttemptId = "foreign_attempt" });
            }
            var forged = ResealRemovalPrepared(prepared, batches, authority);
            var validation = WoundAcceptedTurnPlanCache.ValidatePreparedResult(bundle.Input,
                WoundAcceptedTurnFingerprints.ComputeInput(bundle.Input), new(forged, Array.Empty<ValidationIssue>()));
            Assert.False(validation.Success, axis);
            AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        }
    }
    private static ResolverScenario CreateAdditionPublicationScenario(bool effectless)
    {
        var scenario = CreateRecoveryPublicationScenario("procedure", "failed_attempt", "a1");
        var guide = File.ReadAllText(Path.Combine(FindRepositoryRootForB4SourceGuard(), "OtherGuides", "Wound_Materialization_Contract.md"));
        var worked = Regex.Match(guide, "mortal_wound_treatment_reduce_severity_v1" + @".*?```json\s*(?<json>.*?)```", RegexOptions.Singleline);
        Assert.True(worked.Success);
        var documented = JsonNode.Parse(worked.Groups["json"].Value)!;
        var draft = documented["routes"]![0]!["outcomes"]!.AsArray().Single(row => row!["category"]!.GetValue<string>() == "failed_attempt")!["result"]![0]!["complicationDraft"]!.DeepClone().AsObject();
        if (effectless) draft["consequenceDefinitions"] = new JsonArray();
        var failed = scenario.Before["treatment"]!["routes"]![0]!["outcomes"]!
            .AsArray().OfType<JsonObject>().Single(row =>
                row["category"]!.GetValue<string>() == "failed_attempt");
        failed["result"] = new JsonArray(new JsonObject
        {
            ["kind"] = "add_complication", ["complicationDraft"] = draft
        });
        return PrepareProcedurePublicationScenario(scenario with
        {
            OperationKey = "operation_t070_direct_add_" + (effectless ? "empty" : "effectful"),
            ExpectedIntentCount = 1
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ComplicationAdditionPublication_NewSkillRequiresFreshAuthorityWithoutCreationPacket(bool childOnly)
    {
        var scenario = CreateAdditionPublicationScenario(false);
        var operation = AdditionOperation("irritation");
        var definitions = operation["complicationDraft"]!["consequenceDefinitions"]!.AsArray();
        if (childOnly)
        {
            definitions = WoundContractTestData.CreateRootBoundReactionComplicationDefinitions("independent");
            definitions[1]!["root"] = null;
            definitions[0]!["root"]!["slots"]!.AsArray().Add(new JsonObject
            { ["profileKey"] = "roll_modifier", ["readableSummary"] = "Reachable skill-check disadvantage" });
            operation["complicationDraft"]!["consequenceDefinitions"] = definitions;
        }
        var selectedDefinition = definitions[childOnly ? 1 : 0]!["definition"]!;
        selectedDefinition["components"] = EffectMaterializationTestFixture.CreateDefinition("roll_modifier")["components"]!.DeepClone();
        selectedDefinition["components"]![0]!["payload"] = new JsonObject
        {
            ["operations"] = new JsonArray("skill_check"), ["contribution"] = "disadvantage",
            ["scope"] = new JsonObject { ["kind"] = "skill", ["skillId"] = "skill_field_medicine_01" }
        };
        selectedDefinition["triggers"] = new JsonArray();
        if (!childOnly) definitions[0]!["root"]!["slots"]![0]!["profileKey"] = "roll_modifier";
        scenario.Before["treatment"]!["routes"]![0]!["outcomes"]!.AsArray().OfType<JsonObject>()
            .Single(row => row["category"]!.GetValue<string>() == "failed_attempt")["result"] = new JsonArray(operation);
        scenario = PrepareProcedurePublicationScenario(scenario);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId), "fresh skill addition");
        var bundle = ComposeCoordinatedTreatmentPlan(fixture, flow).WoundStageBundle!;
        Assert.Null(bundle.EffectBatchPlan.EffectInput.WoundApplicationLocations);
        var tree = CaptureResolverFixtureTree(fixture.Root);
        Assert.True(WoundAcceptedTurnPlanner.TryGetTreatmentSelectedGraph(bundle.PreparedPlan, out var originalGraph));
        var expectedOrigins = originalGraph!.NewDefinitionOrigins;
        Assert.Contains(expectedOrigins, origin => origin.DefinitionOrdinal == (childOnly ? 1 : 0));
        foreach (var missing in new[] { true, false })
        {
            Assert.True(WoundAcceptedTurnPlanner.TryGetTreatmentSelectedGraph(bundle.PreparedPlan, out var detachedGraph));
            var forgedOrigins = missing ? ImmutableArray<MortalWoundTreatmentSelectedDefinitionOrigin>.Empty :
                expectedOrigins.Select(origin => origin with { OperationOrdinal = 99, MappedDefinitionRef = "foreign/borrowed" }).ToImmutableArray();
            typeof(MortalWoundTreatmentSelectedGraphCompilation).GetField("<NewDefinitionOrigins>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(detachedGraph, forgedOrigins);
            Assert.True(WoundAcceptedTurnPlanner.TryGetTreatmentSelectedGraph(bundle.PreparedPlan, out var recomposed));
            Assert.NotSame(detachedGraph, recomposed);
            Assert.Equal(expectedOrigins.ToArray(), recomposed!.NewDefinitionOrigins.ToArray());
        }
        foreach (var axis in new[] { "exact", "no_offered", "no_current", "disabled", "missing_authority" })
        {
            IReadOnlyDictionary<string, JsonNode?> Roots(bool missing, bool disabled = false) => new Dictionary<string, JsonNode?>
            {
                ["game_state/player/skills_active.json"] = missing ? new JsonArray() : new JsonArray(new JsonObject
                { ["skillId"] = "skill_field_medicine_01", ["name"] = "Medicine", ["active"] = !disabled })
            };
            var input = bundle.EffectBatchPlan.EffectInput with
            {
                SkillScopeAuthority = axis == "missing_authority" ? null : EffectRollSkillScopeAuthority.Build(new(
                    Roots(axis == "no_offered"), Roots(axis == "no_current", axis == "disabled")))
            };
            var ids = new AdditionCountingIdentityFactory();
            var result = WoundEffectBatchPlanner.Build(bundle.PreparedPlan, input, ids);
            Assert.Equal(axis == "exact", result.Success);
            if (axis != "exact")
            {
                Assert.Equal(0, ids.EffectCalls);
                Assert.Equal(0, ids.TransitionCalls);
                Assert.Contains(result.Issues, issue => issue.Code == "wound_materialization_effect_binding_invalid" &&
                    issue.FilePath == $"treatmentPublication.outcome.declaredResult[0].complicationDraft.consequenceDefinitions[{(childOnly ? 1 : 0)}].definition.components[0].payload.scope.skillId");
            }
            AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        }
    }

    private sealed class AdditionCountingIdentityFactory : EffectIdentityFactory
    {
        internal int EffectCalls { get; private set; }
        internal int TransitionCalls { get; private set; }
        internal override string CreateEffectId() { EffectCalls++; return base.CreateEffectId(); }
        internal override string CreateTransitionId() { TransitionCalls++; return base.CreateTransitionId(); }
    }

    [Fact]
    public void ComplicationAdditionPublication_InterruptedCourseAndCriticalFailureKeepApprovedRules()
    {
        var scenario = CreateScalarCoursePublicationScenario();
        scenario.Before["treatment"]!["routes"]![0]!["interruption"]!["result"] = new JsonArray(AdditionOperation("interrupted", true));
        scenario = scenario with { History = CreateCurrentWoundHistory(scenario.Before) };
        using var fixture = AcceptedStateFixture.Create(scenario);
        ComposeAndPublishTreatment(fixture, ResolveCurrentTreatment(fixture, "course", scenario.OperationKey + "_start", scenario.RouteId));
        fixture.PrepareNextTurn(43, 1_081, "selected_add_interruption");
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "course", scenario.OperationKey + "_interrupt", scenario.RouteId), "interrupted course addition");
        var resolution = (MortalWoundTreatmentResolution)flow.Resolution;
        Assert.True(resolution.Interruption);
        Assert.Equal("interrupted", resolution.CourseDisposition);
        var before = fixture.ReadCurrentWound();
        Assert.Equal("treat", before.LastTransition.Kind);
        ComposeAndPublishTreatment(fixture, flow);
        Assert.Null(fixture.ReadCurrentWound().Care.ActiveCourseId);
        Assert.Equal(before.Complications.Count + 1, fixture.ReadCurrentWound().Complications.Count);
        Assert.Equal(before.LastTransition.Ordinal + 1, fixture.ReadCurrentWound().LastTransition.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    public void ComplicationAdditionPublication_InterruptedCourseAndCriticalFailureKeepApprovedRules_NaturalExtremes(int die)
    {
        var scenario = CreateScenario("procedure_player_natural_one_reserves_oldest_fate_shield", "procedure");
        scenario.AcceptedState["acceptedDice"] = new JsonArray(die, 17);
        foreach (var band in scenario.Before["treatment"]!["routes"]![0]!["outcomes"]!.AsArray().OfType<JsonObject>()
                     .Where(row => row["category"]!.GetValue<string>() == "failed_attempt"))
            band["result"] = new JsonArray(AdditionOperation("natural_failure", true));
        scenario = PrepareProcedurePublicationScenario(scenario with { ExpectedIntentCount = die == 1 ? 1 : 2 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId), "natural extreme addition");
        var resolution = (MortalWoundTreatmentResolution)flow.Resolution;
        Assert.Equal(die == 1 ? "failed_attempt" : "success", resolution.ResultCategory);
        var plan = ComposeCoordinatedTreatmentPlan(fixture, flow);
        var lifecycle = plan.WoundStageBundle!.EffectBatchPlan.EffectInput.EventInput["lifecycleEvents"]!.AsArray();
        Assert.Equal(die == 1 ? 1 : 0, lifecycle.OfType<JsonObject>().Count(row => row["phase"]?.GetValue<string>() == "owner_critical_failure"));
        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan)) publication.CompleteAtFullPipelineEnd();
        Assert.Equal(die == 1 ? 1 : 0, fixture.ReadCurrentWound().Complications.Count);
        Assert.Equal(die == 1 ? new[] { "effect_fate_shield_newer" } : new[] { "effect_fate_shield_older", "effect_fate_shield_newer" },
            fixture.ReadActivePlayerEffectIds().Where(id => id.StartsWith("effect_fate_shield_", StringComparison.Ordinal)));
        if (die == 1) Assert.Contains(resolution.OutcomeIntents, row => row is MortalWoundAddComplicationOutcomeIntent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ComplicationAdditionPublication_InterruptedCourseAndCriticalFailureKeepApprovedRules_PolicySibling(bool selectedPolicy)
    {
        var scenario = CreateAdditionPublicationScenario(true);
        scenario.Before["severity"]!["rank"] = 4;
        scenario.Before["severity"]!["value"] = "IV";
        scenario.Before["severity"]!["maximumAtCreation"] = "IV";
        scenario.Before["consequences"]!["slotBudget"] = 4;
        scenario.Before["recovery"]!["deteriorationPolicy"] = new JsonObject
        { ["policyRef"] = "untreated_infection", ["unmetConditions"] = new JsonArray("not_stabilized"),
            ["graceMinutes"] = 30L, ["cadenceMinutes"] = 10L, ["result"] = new JsonObject { ["kind"] = "death_contour" } };
        var bands = scenario.Before["treatment"]!["routes"]![0]!["outcomes"]!.AsArray();
        var direct = bands.Single(row => row!["category"]!.GetValue<string>() == "failed_attempt")!;
        direct["minimumMargin"] = -99;
        var policy = new JsonObject { ["kind"] = "apply_deterioration", ["policyRef"] = "untreated_infection" };
        var sibling = direct.DeepClone();
        sibling["bandId"] = "extreme_policy_failure";
        sibling["minimumMargin"] = null;
        sibling["maximumMargin"] = -100;
        sibling["result"] = new JsonArray(policy.DeepClone());
        bands.Add(sibling);
        if (selectedPolicy) direct["result"]!.AsArray().Add(policy);
        scenario = PrepareProcedurePublicationScenario(scenario with { ExpectedIntentCount = selectedPolicy ? 2 : 1 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId), "policy sibling stays unfinished");
        var tree = CaptureResolverFixtureTree(fixture.Root);
        var accepted = (MortalWoundTreatmentAcceptedStateAuthority)flow.AcceptedState;
        var result = MortalWoundTreatmentOutcomePublicationPlanner.Prepare(accepted, (MortalWoundTreatmentAttemptRequest)flow.Request,
            (MortalWoundTreatmentResolution)flow.Resolution, accepted.CurrentGameMinute);
        Assert.Equal(!selectedPolicy, result.IsValid);
        if (selectedPolicy)
        {
            Assert.Contains(result.Issues, row => row.Code == "mortal_wound_treatment_publication_slice_unsupported");
            AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        }
        else
        {
            ComposeAndPublishCoordinatedTreatment(fixture, flow);
            Assert.Single(fixture.ReadCurrentWound().Complications);
            Assert.Equal("active", fixture.ReadCurrentWound().Lifecycle);
        }
    }

    [Theory]
    [InlineData("a,p,m,s,r")]
    [InlineData("s,m,a,r,p")]
    [InlineData("m,r,p,a")]
    public void ComplicationAdditionPublication_DecoratorsDoNotReapplyRecoveryOrRemoval(string order)
    {
        var scenario = CreateAdditionPublicationScenario(true);
        scenario.Before["recovery"]!["currentStepProgress"] = 1;
        scenario.Before["complications"] = new JsonArray(new JsonObject
        {
            ["complicationId"] = "original_effectless", ["kind"] = "pain", ["state"] = "active",
            ["displayName"] = "Original pain", ["treatmentDifficultyModifier"] = 0,
            ["visibility"] = "known_to_player", ["ownedEffectIds"] = new JsonArray()
        });
        var tokens = order.Split(',');
        scenario.Before["treatment"]!["routes"]![0]!["outcomes"]!.AsArray().OfType<JsonObject>()
            .Single(row => row["category"]!.GetValue<string>() == "failed_attempt")["result"] =
            new JsonArray(tokens.Select(token => (JsonNode)(token switch
            {
                "a" => AdditionOperation("decorated", true),
                "p" => new JsonObject { ["kind"] = "add_recovery", ["points"] = 2 },
                "m" => new JsonObject { ["kind"] = "remove_complication", ["complicationId"] = "original_effectless" },
                "s" => new JsonObject { ["kind"] = "stabilize" },
                _ => new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 }
            })).ToArray());
        scenario = PrepareProcedurePublicationScenario(scenario with { ExpectedIntentCount = tokens.Length });
        using var fixture = AcceptedStateFixture.Create(scenario);
        var before = fixture.ReadCurrentWound();
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId), "one scalar pass");
        ComposeAndPublishCoordinatedTreatment(fixture, flow);
        var after = fixture.ReadCurrentWound();
        Assert.Equal(3, after.Recovery.CurrentStepProgress);
        Assert.Equal(2, after.Severity.Rank);
        Assert.Single(after.Complications);
        Assert.DoesNotContain(after.Complications, row => row.ComplicationId == "original_effectless");
        Assert.Equal(before.LastTransition.Ordinal + 1, after.LastTransition.Ordinal);
        if (tokens.Contains("s"))
        {
            Assert.Equal("stabilized", after.Care.State);
            Assert.Equal(((MortalWoundTreatmentAttemptRequest)flow.Request).Coordinates.Turn, after.Care.StabilizedAtTurn);
            Assert.Equal(new WoundRecoveryAnchor("stabilization", ((MortalWoundTreatmentAcceptedStateAuthority)flow.AcceptedState).CurrentGameMinute,
                after.LastTransition.TransitionId), after.Recovery.RecoveryAnchor);
        }
        else
        {
            Assert.Equal(before.Recovery.RecoveryAnchor, after.Recovery.RecoveryAnchor);
            Assert.Equal(before.Recovery.DeteriorationAnchor, after.Recovery.DeteriorationAnchor);
        }
    }

    [Fact]
    public void ComplicationAdditionPublication_SelectedBindingAndGraphTamperRejects()
    {
        var scenario = CreateAdditionPublicationScenario(true);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId), "selected binding tamper");
        var plan = ComposeCoordinatedTreatmentPlan(fixture, flow);
        var bundle = plan.WoundStageBundle!;
        Assert.True(WoundAcceptedTurnPlanner.TryReadTreatmentContinuation(bundle.PreparedPlan.TreatmentContinuationAuthority!, out var continuation));
        var resolution = (MortalWoundTreatmentResolution)flow.Resolution;
        var request = (MortalWoundTreatmentAttemptRequest)flow.Request;
        var accepted = (MortalWoundTreatmentAcceptedStateAuthority)flow.AcceptedState;
        var addition = Assert.IsType<MortalWoundAddComplicationOutcomeIntent>(Assert.Single(resolution.OutcomeIntents));
        var tree = CaptureResolverFixtureTree(fixture.Root);
        foreach (var axis in new[] { "id", "ref", "ordinal", "map", "fingerprint" })
        {
            var changedIntent = MortalWoundAddComplicationOutcomeIntent.Create(axis == "ordinal" ? 1 : 0,
                addition.DeclaredOperationFingerprint, addition.IntentFingerprint,
                axis == "ref" ? "foreign_ref" : addition.ComplicationRef,
                axis == "id" ? "foreign_complication" : addition.ComplicationId,
                axis == "map" ? new[] { MortalWoundTreatmentReferenceBinding.Create("extra", "0/foreign/extra") } : addition.DefinitionReferenceBindings,
                addition.ApplicationReferenceBindings, axis == "fingerprint" ? "sha256:foreign" : addition.PreparationFingerprint);
            var changed = CloneResolutionWithIntentsUnchecked(resolution, new[] { changedIntent });
            Assert.False(MortalWoundTreatmentOutcomePublicationPlanner.Prepare(accepted, request, changed, accepted.CurrentGameMinute).IsValid, axis);
            Assert.False(continuation.OutcomePreparation.AgreesWith(changed), axis);
            AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        }
        var detached = continuation.OutcomePreparation.DetachedCopy();
        typeof(MortalWoundTreatmentOutcomePreparation).GetField("<TransitionId>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(detached, "foreign_transition");
        Assert.False(detached.AgreesWith(resolution));
        var changedDraft = CloneResolutionWithIntentsUnchecked(resolution, resolution.OutcomeIntents);
        var declaredAddition = Assert.IsType<MortalWoundAddComplicationOperation>(Assert.Single(resolution.DeclaredResult));
        var alteredDraft = declaredAddition.ComplicationDraft with
        { Complication = declaredAddition.ComplicationDraft.Complication with { DisplayName = "Forged full typed draft" } };
        typeof(MortalWoundTreatmentResolution).GetField("_declaredResult", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(changedDraft, Array.AsReadOnly<MortalWoundTreatmentOperation>(new[] { new MortalWoundAddComplicationOperation(alteredDraft) }));
        Assert.False(continuation.OutcomePreparation.AgreesWith(changedDraft));
        Assert.False(MortalWoundTreatmentOutcomePublicationPlanner.Prepare(accepted, request, changedDraft, accepted.CurrentGameMinute).IsValid);
        Assert.True(continuation.OutcomePreparation.AgreesWith(resolution));
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
    }

    [Fact]
    public void ComplicationAdditionPublication_SelectedBindingAndGraphTamperRejects_EffectHandoff()
    {
        var scenario = CreateAdditionPublicationScenario(false);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId), "effect handoff tamper");
        var bundle = ComposeCoordinatedTreatmentPlan(fixture, flow).WoundStageBundle!;
        var prepared = bundle.PreparedPlan;
        var batch = Assert.Single(prepared.EffectOperationBatches);
        var result = Assert.Single(bundle.EffectBatchPlan.ApplicationResults);
        Assert.True(WoundAcceptedTurnPlanner.TryReadTreatmentContinuation(prepared.TreatmentContinuationAuthority!, out var continuation));
        var tree = CaptureResolverFixtureTree(fixture.Root);
        var addition = Assert.IsType<MortalWoundAddComplicationOutcomeIntent>(Assert.Single(continuation.Resolution.OutcomeIntents));
        foreach (var axis in new[] { "missing", "borrowed", "extra" })
        {
            var bindings = axis == "missing" ? Array.Empty<MortalWoundTreatmentReferenceBinding>() :
                axis == "borrowed" ? addition.ApplicationReferenceBindings.Select(row =>
                    MortalWoundTreatmentReferenceBinding.Create(row.LocalRef, "1/foreign/root_application")).ToArray() :
                addition.ApplicationReferenceBindings.Append(MortalWoundTreatmentReferenceBinding.Create("extra", "0/extra/application")).ToArray();
            var changedIntent = MortalWoundAddComplicationOutcomeIntent.Create(addition.OperationOrdinal,
                addition.DeclaredOperationFingerprint, addition.IntentFingerprint, addition.ComplicationRef, addition.ComplicationId,
                addition.DefinitionReferenceBindings, bindings, addition.PreparationFingerprint);
            Assert.False(continuation.OutcomePreparation.AgreesWith(
                CloneResolutionWithIntentsUnchecked(continuation.Resolution, new[] { changedIntent })), axis);
        }
        var alteredRoot = Assert.Single(batch.RootApplications);
        typeof(WoundRootEffectApplication).GetField("<OperationKey>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(alteredRoot, "wound_root_operation_borrowed");
        var alteredBatch = ResealRemovalBatch(batch, applications: new[] { alteredRoot });
        var alteredPrepared = ResealRemovalPrepared(prepared, new[] { alteredBatch }, prepared.TreatmentContinuationAuthority!);
        var alteredIds = new AdditionCountingIdentityFactory();
        Assert.False(WoundEffectBatchPlanner.Build(alteredPrepared, bundle.EffectBatchPlan.EffectInput, alteredIds).Success);
        Assert.Equal(0, alteredIds.EffectCalls);
        Assert.Equal(0, alteredIds.TransitionCalls);
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        foreach (var axis in new[] { "missing", "extra", "old_id", "slot", "lineage", "ownership" })
        {
            var map = new Dictionary<string, EffectAcceptedApplicationResult> { [result.ApplicationRef] = result };
            var changedBatch = batch;
            if (axis == "missing") map.Clear();
            if (axis == "extra") map["foreign"] = result with { ApplicationRef = "foreign" };
            if (axis == "old_id") map[result.ApplicationRef] = result with { EffectId = "effect_t070_recovery_characteristic" };
            if (axis == "slot") map[result.ApplicationRef] = result with
            { Materialization = result.Materialization with { SlotBindings = result.Materialization.SlotBindings.Select(row => row with { Slot = row.Slot + 1 }).ToArray() } };
            if (axis == "lineage") changedBatch = ResealRemovalBatch(batch, lineage: Array.Empty<WoundRootLineageAuthorityRow>());
            if (axis == "ownership") changedBatch = ResealRemovalBatch(batch, lineage: batch.RootLineageAuthority.Select(row => row with
            { OwnershipDomain = WoundRootOwnershipDomain.ForComplication("foreign") }).ToArray());
            var finalized = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(continuation.OutcomePreparation, continuation.Resolution, changedBatch, map);
            Assert.False(finalized.IsValid, axis);
            AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        }
        foreach (var mutation in new[] { "root_delete", "lineage_change" })
        {
            var forged = ResealTreatmentPreparedTopology(prepared, mutation);
            var ids = new AdditionCountingIdentityFactory();
            Assert.False(WoundEffectBatchPlanner.Build(forged, bundle.EffectBatchPlan.EffectInput, ids).Success, mutation);
            Assert.Equal(0, ids.EffectCalls);
            Assert.Equal(0, ids.TransitionCalls);
            AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ComplicationAdditionPublication_FinalStampIsIndependentOfOriginalKind(bool previousTreatment)
    {
        var scenario = CreateAdditionPublicationScenario(true);
        var selectedRoute = scenario.RouteId;
        if (previousTreatment)
        {
            var routes = scenario.Before["treatment"]!["routes"]!.AsArray();
            var second = routes[0]!.DeepClone();
            selectedRoute += "_addition";
            second["routeId"] = selectedRoute;
            routes.Add(second);
            scenario.Before["treatment"]!["knownRouteIds"] = new JsonArray(scenario.RouteId, selectedRoute);
            routes[0]!["outcomes"]!.AsArray().Single(row => row!["category"]!.GetValue<string>() == "failed_attempt")!["result"] =
                new JsonArray(new JsonObject { ["kind"] = "no_improvement" });
            scenario = PrepareProcedurePublicationScenario(scenario);
        }
        using var fixture = AcceptedStateFixture.Create(scenario);
        if (previousTreatment)
        {
            var first = PersistAndRehydrateTreatmentPublication(fixture,
                ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey + "_first", scenario.RouteId), "actual preceding treatment");
            ComposeAndPublishCoordinatedTreatment(fixture, first);
            fixture.PrepareNextTurn(43, 1_300, "selected_stamp_after_treat");
        }
        var before = fixture.ReadCurrentWound();
        Assert.Equal(previousTreatment ? "treat" : "create", before.LastTransition.Kind);
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey + "_selected", selectedRoute), "independent selected final stamp");
        var plan = ComposeCoordinatedTreatmentPlan(fixture, flow);
        var bundle = plan.WoundStageBundle!;
        var prepared = bundle.PreparedPlan;
        Assert.True(WoundAcceptedTurnPlanner.TryGetTreatmentSelectedGraph(prepared, out var graph));
        Assert.Equal("treat", graph!.FinalScalars.LastTransition.Kind);
        Assert.Equal(before.LastTransition.Ordinal + 1, graph.FinalScalars.LastTransition.Ordinal);
        Assert.Equal(prepared.Binding.Turn, graph.FinalScalars.LastTransition.Turn);
        var tree = CaptureResolverFixtureTree(fixture.Root);
        foreach (var axis in new[] { "allocated_transition", "allocated_wound", "turn", "kind", "ordinal" })
        {
            var binding = axis == "turn" ? prepared.Binding with { Turn = prepared.Binding.Turn + 1 } : prepared.Binding;
            var batches = prepared.EffectOperationBatches;
            if (axis == "kind")
            {
                var batch = Assert.Single(batches);
                var altered = new WoundEffectOperationBatch(batch.LocalWoundRef, batch.PreparedWoundId, batch.SourceExport,
                    batch.RootApplications, batch.TerminalOperations, batch.RootLineageAuthority, string.Empty,
                    batch.TransitionAuthority with { TransitionKind = "create" });
                batches = new[] { ResealRemovalBatch(altered) };
            }
            var originalCarrier = Assert.Single(prepared.PreparedWounds);
            var carriers = axis == "ordinal" ? new[] { originalCarrier with
                { LastTransition = originalCarrier.LastTransition with { Ordinal = originalCarrier.LastTransition.Ordinal + 1 } } } : prepared.PreparedWounds;
            var forged = new WoundPreparedAcceptedTurnPlan(binding,
                WoundAcceptedTurnFingerprints.ComputeBinding(binding), prepared.InputFingerprint, string.Empty,
                axis == "allocated_wound" ? new[] { "foreign_wound" } : prepared.AllocatedWoundIds,
                axis == "allocated_transition" ? new[] { "foreign_transition" } : prepared.AllocatedTransitionIds,
                carriers, batches, prepared.BaselineAuthority, prepared.TreatmentContinuationAuthority);
            forged = ResealRemovalPrepared(forged, batches, forged.TreatmentContinuationAuthority!);
            Assert.False(WoundAcceptedTurnPlanner.TryGetTreatmentSelectedGraph(forged, out _), axis);
            var ids = new AdditionCountingIdentityFactory();
            Assert.False(WoundEffectBatchPlanner.Build(forged, bundle.EffectBatchPlan.EffectInput, ids).Success, axis);
            Assert.Equal(0, ids.EffectCalls);
            Assert.Equal(0, ids.TransitionCalls);
            AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        }
        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan))
            publication.CompleteAtFullPipelineEnd();
        Assert.Equal(graph.FinalScalars.LastTransition, fixture.ReadCurrentWound().LastTransition);
    }

    [Theory]
    [InlineData("not_stabilized", "a,s,p")]
    [InlineData("unsafe_environment", "s,p,a")]
    public void ComplicationAdditionPublication_StabilizationUsesSelectedStampAndMinute(string condition, string order)
    {
        var scenario = CreateAdditionPublicationScenario(true);
        scenario.Before["recovery"]!["blockers"] = new JsonArray("not_stabilized", "unsafe_environment");
        scenario.Before["recovery"]!["deteriorationAnchor"] = new JsonObject
        { ["conditionKey"] = condition, ["anchorMinute"] = 100L, ["anchorTransitionId"] = scenario.Before["lastTransition"]!["transitionId"]!.DeepClone() };
        scenario.Before["treatment"]!["routes"]![0]!["outcomes"]!.AsArray().Single(row => row!["category"]!.GetValue<string>() == "failed_attempt")!["result"] =
            new JsonArray(order.Split(',').Select(token => (JsonNode)(token switch
            {
                "a" => AdditionOperation("anchored", true), "s" => new JsonObject { ["kind"] = "stabilize" },
                _ => new JsonObject { ["kind"] = "add_recovery", ["points"] = 2 }
            })).ToArray());
        scenario = PrepareProcedurePublicationScenario(scenario with { ExpectedIntentCount = 3 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.PrepareNextTurn(43, 1_300, "selected_stabilization_stamp");
        var before = fixture.ReadCurrentWound();
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId), "selected stabilization anchors");
        ComposeAndPublishCoordinatedTreatment(fixture, flow);
        var after = fixture.ReadCurrentWound();
        Assert.NotEqual(before.LastTransition.Turn, after.LastTransition.Turn);
        Assert.Equal(43, after.Care.StabilizedAtTurn);
        Assert.Equal(new WoundRecoveryAnchor("stabilization", 1_300, after.LastTransition.TransitionId), after.Recovery.RecoveryAnchor);
        Assert.Equal(2, after.Recovery.CurrentStepProgress);
        Assert.Equal(condition == "not_stabilized" ? null : before.Recovery.DeteriorationAnchor, after.Recovery.DeteriorationAnchor);
        Assert.DoesNotContain("not_stabilized", after.Recovery.Blockers);
        Assert.Contains("unsafe_environment", after.Recovery.Blockers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ComplicationAdditionPublication_SameRankPreservesOldGraphAndRequiresBatch(bool effectless)
    {
        var scenario = CreateAdditionPublicationScenario(effectless);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var before = fixture.ReadCurrentWound();
        var oldCarrier = fixture.ReadPlayerEffectCarrier();
        var oldIndex = fixture.ReadEffectIdentityIndex();
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId),
            "direct addition");
        var plan = ComposeCoordinatedTreatmentPlan(fixture, flow);
        var prepared = plan.WoundStageBundle!.PreparedPlan;
        var batch = Assert.Single(prepared.EffectOperationBatches);
        Assert.Empty(batch.TerminalOperations);
        Assert.Equal(effectless ? 0 : 1, batch.RootApplications.Count);
        Assert.All(batch.RootApplications, root => Assert.Null(root.PriorRootEffectId));
        Assert.Equal("effect_t070_recovery_characteristic",
            Assert.Single(batch.RootLineageAuthority, row => row.EffectId is not null).EffectId);
        Assert.True(WoundAcceptedTurnPlanner.TryReadTreatmentContinuation(
            prepared.TreatmentContinuationAuthority!, out var continuation));
        Assert.NotNull(continuation.RematerializationAuthority);
        var acceptedApplications = plan.WoundStageBundle!.EffectBatchPlan.ApplicationResults;
        Assert.Equal(batch.RootApplications.Select(row => row.ApplicationRef), acceptedApplications.Select(row => row.ApplicationRef));
        if (effectless) Assert.Empty(acceptedApplications);
        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan))
            publication.CompleteAtFullPipelineEnd();
        var after = fixture.ReadCurrentWound();
        Assert.Equal(before.Severity, after.Severity);
        var added = Assert.IsType<MortalWoundAddComplicationOutcomeIntent>(
            Assert.Single(((MortalWoundTreatmentResolution)flow.Resolution).OutcomeIntents));
        Assert.Contains(after.Complications, row => row.ComplicationId == added.ComplicationId);
        Assert.Equal(before.Consequences.Entries, after.Consequences.Entries.Take(before.Consequences.Entries.Count));
        if (!effectless)
        {
            var application = Assert.Single(acceptedApplications);
            Assert.Equal(application.EffectId, Assert.Single(after.Complications.Single(row => row.ComplicationId == added.ComplicationId).OwnedEffectIds));
            Assert.Equal(before.Consequences.Entries.Count + 1, Assert.Single(application.Materialization.SlotBindings).Slot);
            var created = fixture.ReadEffectIdentityIndex()["entries"]!.AsArray().Single(row => row!["effectId"]!.GetValue<string>() == application.EffectId)!;
            Assert.Equal("create", created["transitions"]![0]!["kind"]!.GetValue<string>());
            Assert.Empty(created["transitions"]![0]!["sourceEffectIds"]!.AsArray());
        }
        var carrierAfter = fixture.ReadPlayerEffectCarrier();
        var indexAfter = fixture.ReadEffectIdentityIndex();
        const string retainedId = "effect_t070_recovery_characteristic";
        Assert.True(JsonNode.DeepEquals(
            oldCarrier["activeEffects"]!.AsArray().Single(row => row!["effectId"]!.GetValue<string>() == retainedId),
            carrierAfter["activeEffects"]!.AsArray().Single(row => row!["effectId"]!.GetValue<string>() == retainedId)));
        Assert.True(JsonNode.DeepEquals(
            oldIndex["entries"]!.AsArray().Single(row => row!["effectId"]!.GetValue<string>() == retainedId),
            indexAfter["entries"]!.AsArray().Single(row => row!["effectId"]!.GetValue<string>() == retainedId)));
        Assert.Equal(oldIndex["entries"]!.AsArray().Count + (effectless ? 0 : 1),
            indexAfter["entries"]!.AsArray().Count);
        if (effectless)
        {
            Assert.Equal(oldCarrier["activeEffects"]!.AsArray().Select(row => row!["effectId"]!.GetValue<string>()),
                carrierAfter["activeEffects"]!.AsArray().Select(row => row!["effectId"]!.GetValue<string>()));
            // This real failed-procedure fixture also owns a finite-duration disadvantage.
            // Its ordinary owner_turn_end tick is independent of the empty wound batch.
            const string modifierId = "effect_roll_modifier_disadvantage";
            var priorModifier = oldCarrier["activeEffects"]!.AsArray().Single(row => row!["effectId"]!.GetValue<string>() == modifierId)!;
            var modifier = carrierAfter["activeEffects"]!.AsArray().Single(row => row!["effectId"]!.GetValue<string>() == modifierId)!;
            Assert.Equal(3, priorModifier["lifetime"]!["remainingTurns"]!.GetValue<int>());
            Assert.Equal(2, modifier["lifetime"]!["remainingTurns"]!.GetValue<int>());
            var priorHistory = oldIndex["entries"]!.AsArray().Single(row => row!["effectId"]!.GetValue<string>() == modifierId)!["transitions"]!.AsArray();
            var modifierHistory = indexAfter["entries"]!.AsArray().Single(row => row!["effectId"]!.GetValue<string>() == modifierId)!["transitions"]!.AsArray();
            Assert.Equal(priorHistory.Count + 2, modifierHistory.Count);
            for (var i = 0; i < priorHistory.Count; i++) Assert.True(JsonNode.DeepEquals(priorHistory[i], modifierHistory[i]));
            Assert.Equal(new[] { "trigger", "consume" }, modifierHistory.Skip(priorHistory.Count).Select(row => row!["kind"]!.GetValue<string>()));
        }
        var treatment = Assert.Single(fixture.ReadCurrentHistory().State!.Transitions, row => row.Kind == "treat");
        AssertClosedTreatmentReceipt(treatment.TreatmentResult!.Receipt, flow.Request, flow.Resolution);
    }
}
