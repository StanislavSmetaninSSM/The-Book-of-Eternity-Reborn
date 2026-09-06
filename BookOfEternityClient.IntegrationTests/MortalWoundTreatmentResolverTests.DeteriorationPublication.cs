using System.Collections.Immutable;
using System.Text.Json.Nodes;
using System.Reflection;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public void DeteriorationPublication_PrivateAuthority_ActualApplicationAndFullRematerializerHandoffRejectTampering()
    {
        var scenario = CreateMultiRootPolicyPreparationScenario();
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId), "policy actual effect handoff");
        var bundle = ComposeCoordinatedTreatmentPlan(fixture, flow).WoundStageBundle!;
        var prepared = bundle.PreparedPlan;
        var originalAuthority = prepared.TreatmentContinuationAuthority!;
        Assert.True(WoundAcceptedTurnPlanner.TryReadTreatmentContinuation(originalAuthority, out var continuation));
        Assert.True(WoundAcceptedTurnPlanner.TreatmentContinuationFinalPlanAgrees(originalAuthority, bundle));
        var batch = Assert.Single(prepared.EffectOperationBatches);
        var applications = bundle.EffectBatchPlan.ApplicationResults.ToArray();
        Assert.Equal(2, applications.Length);
        var tree = CaptureResolverFixtureTree(fixture.Root);
        foreach (var axis in new[] { "missing", "extra", "duplicate_ref", "borrowed", "source", "old_id", "duplicate_id", "slot", "materialization" })
        {
            var rows = applications.ToArray();
            if (axis == "missing") rows = rows.Skip(1).ToArray();
            if (axis == "extra") rows = rows.Append(rows[0] with { ApplicationRef = "foreign" }).ToArray();
            if (axis == "duplicate_ref") rows[1] = rows[1] with { ApplicationRef = rows[0].ApplicationRef };
            if (axis == "borrowed") rows[0] = rows[1] with { ApplicationRef = rows[0].ApplicationRef };
            if (axis == "source") rows[0] = rows[0] with { SourceKey = rows[0].SourceKey with { DefinitionKey = "foreign" } };
            if (axis == "old_id") rows[0] = rows[0] with { EffectId = prepared.PreparedWounds[0].Consequences.OwnedEffectSources.RootBindings[0].EffectId };
            if (axis == "duplicate_id") rows[1] = rows[1] with { EffectId = rows[0].EffectId };
            if (axis == "slot") rows[0] = rows[0] with { Materialization = rows[0].Materialization with
                { SlotBindings = rows[0].Materialization.SlotBindings.Select(slot => slot with { Slot = slot.Slot + 1 }).ToArray() } };
            if (axis == "materialization") rows[0] = rows[0] with { Materialization = rows[0].Materialization with
                { MaterializationFingerprint = "sha256:foreign" } };
            var effect = ClonePolicyField(bundle.EffectBatchPlan, "_applicationResults", rows);
            var changed = ClonePolicyField(bundle, "_effectBatchPlan", effect);
            Assert.False(WoundAcceptedTurnPlanner.TreatmentContinuationFinalPlanAgrees(originalAuthority, changed), axis);
            AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        }
        var proof = continuation.RematerializationAuthority!;
        var properties = proof.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(string)).ToArray();
        Assert.Equal(13, properties.Length);
        foreach (var property in properties)
        {
            var changedProof = ClonePolicyField(proof, "<" + property.Name + ">k__BackingField", property.GetValue(proof) + "_foreign");
            Assert.False(MortalWoundTreatmentSeverityRematerializationPlanner.Agrees(bundle.Input,
                continuation.OutcomePreparation, continuation.Resolution.RequestFingerprint,
                continuation.Resolution.ResolutionAuthorityFingerprint, continuation.Resolution.ResultFingerprint,
                continuation.Resolution.Coordinates.AttemptId, continuation.Resolution.Coordinates.OperationKey,
                continuation.Resolution.Coordinates.ExpectedBeforeFingerprint, batch, changedProof), property.Name);
            var authority = ClonePolicyField(originalAuthority, "_rematerializationAuthority", changedProof);
            var changedPrepared = ClonePolicyField(prepared, "_treatmentContinuationAuthority", authority);
            var changed = ClonePolicyField(bundle, "_preparedPlan", changedPrepared);
            Assert.False(WoundAcceptedTurnPlanner.TreatmentContinuationFinalPlanAgrees(authority, changed), property.Name);
            AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        }
        foreach (var axis in new[] { "root_delete", "root_reorder", "lineage_change" })
        {
            var changedPrepared = ResealTreatmentPreparedTopology(prepared, axis);
            var ids = new AdditionCountingIdentityFactory();
            Assert.False(WoundEffectBatchPlanner.Build(changedPrepared, bundle.EffectBatchPlan.EffectInput, ids).Success, axis);
            Assert.Equal(0, ids.EffectCalls);
            Assert.Equal(0, ids.TransitionCalls);
            Assert.False(WoundAcceptedTurnPlanner.TreatmentContinuationFinalPlanAgrees(originalAuthority,
                ClonePolicyField(bundle, "_preparedPlan", changedPrepared)), axis);
            AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        }
        Assert.True(WoundAcceptedTurnPlanner.TreatmentContinuationFinalPlanAgrees(originalAuthority, bundle));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeteriorationPublication_SkillAuthority_RetainedSelectorIsNotForeignNewAuthority(bool newSelector)
    {
        var scenario = CreatePolicyPreparationScenario("add_complication", !newSelector);
        if (newSelector)
        {
            var policy = scenario.Before["recovery"]!["deteriorationPolicy"]!.DeepClone();
            scenario = CreateRemovalModeScenario("procedure", "failed_attempt", "m");
            scenario.Before["recovery"]!["deteriorationPolicy"] = policy;
            scenario.Before["severity"]!["rank"] = 4;
            scenario.Before["severity"]!["value"] = "IV";
            scenario.Before["consequences"]!["slotBudget"] = 4;
            scenario.Before["treatment"]!["routes"]![0]!["outcomes"]!.AsArray().OfType<JsonObject>()
                .Single(row => row["category"]!.GetValue<string>() == "failed_attempt")["result"]!.AsArray()
                .Add(new JsonObject { ["kind"] = "apply_deterioration", ["policyRef"] = "policy_preparation" });
            scenario = scenario with { ExpectedIntentCount = 2 };
        }
        var component = EffectMaterializationTestFixture.CreateDefinition("roll_modifier")["components"]!.DeepClone();
        component[0]!["payload"] = new JsonObject
        {
            ["operations"] = new JsonArray("skill_check"), ["contribution"] = "disadvantage",
            ["scope"] = new JsonObject { ["kind"] = "skill", ["skillId"] = "skill_grip" }
        };
        var retained = scenario.Before["consequences"]!["ownedEffectSources"]!["definitions"]![newSelector ? 1 : 0]!;
        retained["components"] = component.DeepClone();
        retained["triggers"] = new JsonArray();
        scenario.Before["consequences"]!["entries"]![newSelector ? 1 : 0]!["profileKey"] = "roll_modifier";
        if (newSelector)
        {
            var draft = scenario.Before["recovery"]!["deteriorationPolicy"]!["result"]!["complicationDraft"]!;
            draft["consequenceDefinitions"]![0]!["definition"]!["components"] = component.DeepClone();
            draft["consequenceDefinitions"]![0]!["definition"]!["triggers"] = new JsonArray();
            draft["consequenceDefinitions"]![0]!["root"]!["slots"]![0]!["profileKey"] = "roll_modifier";
            // The original selector cannot authorize its replacement. Keeping both would
            // correctly reject their duplicate mechanical coordinate before resolution.
            component[0]!["payload"]!["scope"]!["skillId"] = "skill_field_medicine_01";
            retained["components"] = component.DeepClone();
            draft["consequenceDefinitions"]![0]!["definition"]!["components"] = component.DeepClone();
        }
        scenario = PrepareProcedurePublicationScenario(scenario);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId), "retained policy selector");
        var plan = ComposeCoordinatedTreatmentPlan(fixture, flow);
        var bundle = plan.WoundStageBundle!;
        var before = fixture.ReadCurrentWound();
        var originalId = before.Consequences.OwnedEffectSources.RootBindings[0].EffectId;
        var oldIndex = fixture.ReadEffectIdentityIndex()["entries"]!.AsArray().Single(row => row!["effectId"]!.GetValue<string>() == originalId)!.DeepClone();
        var tree = CaptureResolverFixtureTree(fixture.Root);
        var result = WoundEffectBatchPlanner.Build(bundle.PreparedPlan, bundle.EffectBatchPlan.EffectInput with
        { SkillScopeAuthority = CreateAcceptedTreatmentSkillScopeAuthority("none") }, new AdditionCountingIdentityFactory());
        Assert.Equal(!newSelector, result.Success);
        if (newSelector)
        {
            Assert.Contains(result.Issues, issue => issue.FilePath == PreparedPolicyAt(flow, 1).WoundSourcePath +
                ".recovery.deteriorationPolicy.result.complicationDraft.consequenceDefinitions[0].definition.components[0].payload.scope.skillId");
            var batch = Assert.Single(bundle.PreparedPlan.EffectOperationBatches);
            Assert.Null(Assert.Single(batch.RootApplications).PriorRootEffectId);
            Assert.Contains(batch.TerminalOperations, row => row.EffectId == "effect_t066_course_complication_a");
            foreach (var missing in new[] { "neither", "offered", "current" })
            {
                IReadOnlyDictionary<string, JsonNode?> Roots(bool empty) => new Dictionary<string, JsonNode?>
                {
                    ["game_state/player/skills_active.json"] = empty ? new JsonArray() : new JsonArray(new JsonObject
                    { ["skillId"] = "skill_field_medicine_01", ["name"] = "Medicine", ["active"] = true })
                };
                var input = bundle.EffectBatchPlan.EffectInput with { SkillScopeAuthority = EffectRollSkillScopeAuthority.Build(new(
                    Roots(missing == "offered"), Roots(missing == "current"))) };
                var ids = new AdditionCountingIdentityFactory();
                var checkedResult = WoundEffectBatchPlanner.Build(bundle.PreparedPlan, input, ids);
                Assert.Equal(missing == "neither", checkedResult.Success);
                if (missing != "neither")
                {
                    Assert.Equal(0, ids.EffectCalls);
                    Assert.Equal(0, ids.TransitionCalls);
                }
                AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
            }
        }
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        if (!newSelector)
        {
            using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan)) publication.CompleteAtFullPipelineEnd();
            Assert.True(JsonNode.DeepEquals(oldIndex, fixture.ReadEffectIdentityIndex()["entries"]!.AsArray()
                .Single(row => row!["effectId"]!.GetValue<string>() == originalId)));
        }
        else
        {
            using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan)) publication.CompleteAtFullPipelineEnd();
            var after = fixture.ReadCurrentWound();
            var added = Assert.Single(after.Complications, row => row.ComplicationId == PreparedPolicyAt(flow, 1).ComplicationBinding!.ComplicationId);
            var newRootId = Assert.Single(added.OwnedEffectIds);
            Assert.Contains(after.Consequences.OwnedEffectSources.RootBindings, row => row.EffectId == newRootId);
            Assert.Contains(fixture.ReadActivePlayerEffectIds(), id => id == newRootId);
            var index = fixture.ReadEffectIdentityIndex()["entries"]!.AsArray();
            Assert.Equal("expired", index.Single(row => row!["effectId"]!.GetValue<string>() == "effect_t066_course_complication_a")!["state"]!.GetValue<string>());
            var created = index.Single(row => row!["effectId"]!.GetValue<string>() == newRootId)!;
            Assert.Equal("create", created["transitions"]![0]!["kind"]!.GetValue<string>());
            Assert.Empty(created["transitions"]![0]!["sourceEffectIds"]!.AsArray());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeteriorationPublication_TaggedGraph_RemovalAndPolicyKeepSelectiveLineage(bool terminalRoot)
    {
        var scenario = CreateRemovalModeScenario("procedure", "failed_attempt", "m");
        var lineage = CreateRemovalLineageScenario(false, rootState: terminalRoot ? "removed" : "active");
        scenario.Before["consequences"] = lineage.Before["consequences"]!.DeepClone();
        scenario.Before["complications"] = lineage.Before["complications"]!.DeepClone();
        foreach (var definition in scenario.Before["consequences"]!["ownedEffectSources"]!["definitions"]!.AsArray())
            definition!["links"]![0]!["targetId"] = scenario.Before["woundId"]!.DeepClone();
        scenario.Before["recovery"]!["deteriorationPolicy"] =
            CreatePolicyPreparationScenario("add_complication").Before["recovery"]!["deteriorationPolicy"]!.DeepClone();
        scenario.Before["recovery"]!["deteriorationPolicy"]!["result"] = AdditionOperation("replacement");
        scenario.Before["treatment"]!["routes"]![0]!["outcomes"]!.AsArray().OfType<JsonObject>()
            .Single(row => row["category"]!.GetValue<string>() == "failed_attempt")["result"]!.AsArray().Add(new JsonObject { ["kind"] = "apply_deterioration", ["policyRef"] = "policy_preparation" });
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
    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    public void DeteriorationPublication_Lifecycle_NaturalExtremes(int die)
    {
        var scenario = CreateScenario("procedure_player_natural_one_reserves_oldest_fate_shield", "procedure");
        scenario.Before["recovery"]!["deteriorationPolicy"] =
            CreatePolicyPreparationScenario("add_complication", true).Before["recovery"]!["deteriorationPolicy"]!.DeepClone();
        scenario.AcceptedState["acceptedDice"] = new JsonArray(die, 17);
        foreach (var band in scenario.Before["treatment"]!["routes"]![0]!["outcomes"]!.AsArray().OfType<JsonObject>()
                     .Where(row => row["category"]!.GetValue<string>() == "failed_attempt"))
            band["result"] = new JsonArray(new JsonObject { ["kind"] = "apply_deterioration", ["policyRef"] = "policy_preparation" });
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
        if (die == 1) Assert.Contains(resolution.OutcomeIntents, row => row is MortalWoundApplyDeteriorationOutcomeIntent);
    }
    [Theory]
    [InlineData("increase_severity")]
    [InlineData("add_complication")]
    public void DeteriorationPublication_Lifecycle_PostWriteRollbackRetryAndColdReplay(string kind)
    {
        var scenario = CreatePolicyPreparationScenario(kind);
        var fault = new ResourcePublicationFailureInjection();
        using var fixture = AcceptedStateFixture.Create(scenario, new FileSystemManagerHooks
        { BeforeCanonicalMutationAsync = fault.BeforeCanonicalMutationAsync });
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId), "addition rollback");
        var plan = ComposeCoordinatedTreatmentPlan(fixture, flow);
        var count = fixture.ReadNpcItemCount("sterile_thread");
        var tree = CaptureResolverFixtureTree(fixture.Root);
        fault.Arm(WoundHistoryState.HistoryPath, fixture.TargetCarrierPath,
            ReadCanonicalBytes(fixture, fixture.TargetCarrierPath), fixture.FileSystem);
        var exception = Assert.Throws<CanonicalStateWriteException>(() => PublishCachedResourcePlanOpen(fixture, flow, plan));
        Assert.Equal(WoundHistoryState.HistoryPath, exception.RelativePath);
        Assert.True(fault.Fired);
        Assert.True(fault.ObservedEarlierResourceWrite);
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan)) publication.CompleteAtFullPipelineEnd();
        if (kind == "add_complication")
            Assert.Equal(PreparedPolicyAt(flow, 0).ComplicationBinding!.ComplicationId, Assert.Single(fixture.ReadCurrentWound().Complications).ComplicationId);
        else Assert.Equal(4, fixture.ReadCurrentWound().Severity.Rank);
        Assert.Equal(count - 1, fixture.ReadNpcItemCount("sterile_thread"));
        Assert.Single(fixture.ReadCurrentHistory().State!.Transitions, row => row.Kind == "treat");
        fixture.RestartForReplay();
        var publishedTree = CaptureResolverFixtureTree(fixture.Root);
        Assert.Equal("ExactReplay", Convert.ToString(ReadRequiredProperty(ProbePublishedTreatment(fixture, flow.Request), "Status")));
        AssertResolverFixtureTreeUnchanged(fixture.Root, publishedTree);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeteriorationPublication_SkillAuthority_ExactRootAndChildPolicyPath(bool childOnly)
    {
        var scenario = CreatePolicyPreparationScenario("add_complication");
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
        scenario.Before["recovery"]!["deteriorationPolicy"]!["result"] = operation;
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
        foreach (var axis in new[] { "origin", "address", "mapped_id", "components_path", "rank_change", "worsening", "topology", "definition_order" })
        {
            Assert.True(WoundAcceptedTurnPlanner.TryGetTreatmentSelectedGraph(bundle.PreparedPlan, out var copy));
            var addition = copy!.Additions[0];
            if (axis is "origin" or "address" or "mapped_id")
            {
                var changed = axis == "mapped_id"
                    ? addition with { Binding = ClonePolicyField(addition.Binding, "<ComplicationId>k__BackingField", "foreign") }
                    : addition with { Reference = axis == "origin"
                        ? addition.Reference with { Origin = WoundWorkingReferenceOrigin.DirectAddition }
                        : addition.Reference with { Address = new(99, 99) } };
                SetPolicyField(copy, "<Additions>k__BackingField", copy.Additions.SetItem(0, changed));
            }
            else if (axis == "components_path") SetPolicyField(copy, "<NewDefinitionOrigins>k__BackingField",
                copy.NewDefinitionOrigins.SetItem(0, copy.NewDefinitionOrigins[0] with { ComponentsPath = "foreign.path" }));
            else if (axis == "rank_change") SetPolicyField(copy, "<FinalSeverityChanged>k__BackingField", !copy.FinalSeverityChanged);
            else if (axis == "worsening") SetPolicyField(copy, "<AllowsWorsening>k__BackingField", !copy.AllowsWorsening);
            else if (axis == "topology") SetPolicyField(copy, "<FinalRoots>k__BackingField", ImmutableArray<MortalWoundTreatmentSelectedRoot>.Empty);
            else
            {
                var working = (MortalWoundTreatmentWorkingGraphProjection)typeof(MortalWoundTreatmentSelectedGraphCompilation)
                    .GetField("_working", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(copy)!;
                SetPolicyField(copy, "_working", working.WithGraph(working.Graph with { Definitions = working.Graph.Definitions.Reverse().ToImmutableArray() }));
            }
            Assert.True(WoundAcceptedTurnPlanner.TryGetTreatmentSelectedGraph(bundle.PreparedPlan, out var fresh));
            Assert.NotSame(copy, fresh);
            Assert.Equal(originalGraph.Fingerprint, fresh!.Fingerprint);
            Assert.Equal(originalGraph.FinalSeverityChanged, fresh.FinalSeverityChanged);
            Assert.Equal(originalGraph.AllowsWorsening, fresh.AllowsWorsening);
            Assert.Equal(originalGraph.NewDefinitionOrigins.ToArray(), fresh.NewDefinitionOrigins.ToArray());
            Assert.Equal(originalGraph.Additions.Select(row => row.Reference), fresh.Additions.Select(row => row.Reference));
            Assert.Equal(originalGraph.Additions.Select(row => row.Binding.ComplicationId), fresh.Additions.Select(row => row.Binding.ComplicationId));
            Assert.Equal(originalGraph.FinalRoots.Select(row => row.Reference), fresh.FinalRoots.Select(row => row.Reference));
            Assert.Equal(originalGraph.FinalGraph.Definitions.Select(row => row.Definition.GetRawText()), fresh.FinalGraph.Definitions.Select(row => row.Definition.GetRawText()));
        }
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
                    issue.FilePath == PreparedPolicyAt(flow, 0).WoundSourcePath + $".recovery.deteriorationPolicy.result.complicationDraft.consequenceDefinitions[{(childOnly ? 1 : 0)}].definition.components[0].payload.scope.skillId");
            }
            AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        }
    }
    // Selected publication exercises the real accepted treatment transaction.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeteriorationPublication_PrivateAuthority_BorrowedOrChangedPolicyPacketRejects(bool changeBody)
    {
        var scenario = CreatePolicyPreparationScenario("increase_severity");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId);
        var other = CreatePolicyPreparationScenario("increase_severity") with { OperationKey = "operation_foreign_policy" };
        if (changeBody) other.Before["recovery"]!["deteriorationPolicy"]!["graceMinutes"] = 31L;
        other = PrepareProcedurePublicationScenario(other);
        using var foreignFixture = AcceptedStateFixture.Create(other);
        var foreign = ResolveCurrentTreatment(foreignFixture, "procedure", other.OperationKey, other.RouteId);
        var resolution = (MortalWoundTreatmentResolution)flow.Resolution;
        var original = (MortalWoundApplyDeteriorationOutcomeIntent)Assert.Single(resolution.OutcomeIntents);
        var forged = ClonePolicyField(original, "_preparedDeterioration", PreparedPolicyAt(foreign, 0));
        var state = (MortalWoundTreatmentAcceptedStateAuthority)flow.AcceptedState;
        var tree = CaptureResolverFixtureTree(fixture.Root);
        var rejected = MortalWoundTreatmentOutcomePublicationPlanner.Prepare(state,
            (MortalWoundTreatmentAttemptRequest)flow.Request, CloneResolutionWithIntentsUnchecked(resolution, new[] { forged }), state.CurrentGameMinute);
        Assert.False(rejected.IsValid);
        Assert.Contains(rejected.Issues, issue => issue.Code == "mortal_wound_treatment_publication_slice_unsupported");
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
    }

    [Fact]
    public void DeteriorationPublication_PrivateAuthority_StoredSealAndFinalProofRejectTampering()
    {
        var scenario = CreatePolicyPreparationScenario("increase_severity");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId), "policy final proof");
        var bundle = ComposeCoordinatedTreatmentPlan(fixture, flow).WoundStageBundle!;
        var originalAuthority = bundle.PreparedPlan.TreatmentContinuationAuthority!;
        Assert.True(WoundAcceptedTurnPlanner.TreatmentContinuationFinalPlanAgrees(originalAuthority, bundle));
        Assert.True(WoundAcceptedTurnPlanner.TryReadTreatmentContinuation(originalAuthority, out var continuation));
        var tree = CaptureResolverFixtureTree(fixture.Root);
        foreach (var axis in new[] { "graph_seal", "policy_packet", "terminal_delete", "terminal_change", "lineage_change" })
        {
            WoundPreparedAcceptedTurnPlan forged;
            if (axis is "graph_seal" or "policy_packet")
            {
                var preparation = continuation.OutcomePreparation.DetachedCopy();
                if (axis == "graph_seal") SetPolicyField(preparation, "_selectedGraphFingerprint", "sha256:foreign");
                else
                {
                    var selected = (MortalWoundTreatmentResolution)typeof(MortalWoundTreatmentOutcomePreparation)
                        .GetField("_selectedResolution", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(preparation)!;
                    var intent = (MortalWoundApplyDeteriorationOutcomeIntent)Assert.Single(selected.OutcomeIntents);
                    var packet = ClonePolicyField(PreparedPolicyAt(flow, 0), "_fingerprint", "sha256:foreign");
                    var changed = CloneResolutionWithIntentsUnchecked(selected,
                        new[] { ClonePolicyField(intent, "_preparedDeterioration", packet) });
                    SetPolicyField(preparation, "_selectedResolution", changed);
                }
                var authority = ClonePolicyField(originalAuthority, "_outcomePreparation", preparation);
                // Keep the copied seal stale: minting a new seal correctly rejects corrupt private authority.
                forged = ClonePolicyField(bundle.PreparedPlan, "_treatmentContinuationAuthority", authority);
            }
            else forged = ResealTreatmentPreparedTopology(bundle.PreparedPlan, axis);
            var changedBundle = ClonePolicyField(bundle, "_preparedPlan", forged);
            Assert.False(WoundAcceptedTurnPlanner.TreatmentContinuationFinalPlanAgrees(forged.TreatmentContinuationAuthority!, changedBundle), axis);
            AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        }
        Assert.True(WoundAcceptedTurnPlanner.TreatmentContinuationFinalPlanAgrees(originalAuthority, bundle));
    }

    [Fact]
    public void DeteriorationPublication_Lifecycle_PartialRecoveryAndPolicyRemainNonterminal()
    {
        var scenario = CreateRecoveryPublicationScenario("procedure", "partial_success", "a1");
        scenario.Before["recovery"]!["deteriorationPolicy"] =
            CreatePolicyPreparationScenario("increase_severity").Before["recovery"]!["deteriorationPolicy"]!.DeepClone();
        scenario.Before["treatment"]!["routes"]![0]!["outcomes"]!.AsArray().OfType<JsonObject>()
            .Single(row => row["category"]!.GetValue<string>() == "partial_success")["result"]!.AsArray()
            .Add(new JsonObject { ["kind"] = "apply_deterioration", ["policyRef"] = "policy_preparation" });
        scenario = PrepareProcedurePublicationScenario(scenario with { ExpectedIntentCount = 2 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        var before = fixture.ReadCurrentWound();
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId), "partial policy recovery");
        var resolution = (MortalWoundTreatmentResolution)flow.Resolution;
        Assert.Equal("partial_success", resolution.ResultCategory);
        Assert.Equal("None", resolution.RouteCompletion);
        Assert.Equal(1, PreparedPolicyAt(flow, 1).OperationOrdinal);
        ComposeAndPublishCoordinatedTreatment(fixture, flow);
        var after = fixture.ReadCurrentWound();
        Assert.Equal(before.Recovery.CurrentStepProgress + 1, after.Recovery.CurrentStepProgress);
        Assert.Equal(before.Severity.Rank + 1, after.Severity.Rank);
        Assert.Equal(before.Treatment.CompletedRouteIds, after.Treatment.CompletedRouteIds);
        AssertClosedTreatmentReceipt(Assert.Single(fixture.ReadCurrentHistory().State!.Transitions,
            row => row.Kind == "treat").TreatmentResult!.Receipt, flow.Request, flow.Resolution);
    }

    [Fact]
    public void DeteriorationPublication_Lifecycle_InterruptedCourseDoesNotConsumeDose()
    {
        var scenario = CreateScalarCoursePublicationScenario();
        scenario.Before["recovery"]!["deteriorationPolicy"] =
            CreatePolicyPreparationScenario("increase_severity").Before["recovery"]!["deteriorationPolicy"]!.DeepClone();
        scenario.Before["treatment"]!["routes"]![0]!["interruption"]!["result"] = new JsonArray(
            new JsonObject { ["kind"] = "apply_deterioration", ["policyRef"] = "policy_preparation" });
        scenario = PrepareProcedurePublicationScenario(scenario);
        using var fixture = AcceptedStateFixture.Create(scenario);
        ComposeAndPublishTreatment(fixture, ResolveCurrentTreatment(fixture, "course", scenario.OperationKey, scenario.RouteId));
        fixture.PrepareNextTurn(43, 1_081, "selected_policy_course_interruption");
        var before = fixture.ReadCurrentWound();
        var count = fixture.ReadPlayerItemCount("antibiotic_dose");
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "course", scenario.OperationKey + "_interrupt", scenario.RouteId), "policy course interruption");
        var resolution = (MortalWoundTreatmentResolution)flow.Resolution;
        Assert.True(resolution.Interruption);
        Assert.Equal("interrupted", resolution.CourseDisposition);
        Assert.Equal(0, PreparedPolicyAt(flow, 0).OperationOrdinal);
        Assert.Equal("not_required", ((MortalWoundTreatmentAttemptRequest)flow.Request).ResourceAuthority.ReservationDisposition);
        ComposeAndPublishTreatment(fixture, flow);
        var after = fixture.ReadCurrentWound();
        Assert.Null(after.Care.ActiveCourseId);
        Assert.Equal(before.Severity.Rank + 1, after.Severity.Rank);
        Assert.Equal(before.Recovery.CurrentStepProgress, after.Recovery.CurrentStepProgress);
        Assert.Equal(count, fixture.ReadPlayerItemCount("antibiotic_dose"));
        Assert.Equal(2, fixture.ReadCurrentHistory().State!.Transitions.Count(row => row.Kind == "treat"));
    }

    [Fact]
    public void DeteriorationPublication_TaggedGraph_DirectAndPolicyLocalNamesStayDistinct()
    {
        var scenario = CreatePolicyPreparationScenario("add_complication");
        var direct = AdditionOperation("policy_preparation");
        var definition = direct["complicationDraft"]!["consequenceDefinitions"]![0]!["definition"]!;
        definition["definitionKey"] = "definition_distinct_direct";
        definition["stacking"]!["stackKey"] = "stack_distinct_direct";
        definition["components"]![0]!["payload"]!["action"] = "use_item";
        scenario.Before["treatment"]!["routes"]![0]!["outcomes"]!.AsArray().OfType<JsonObject>()
            .Single(row => row["category"]!.GetValue<string>() == "failed_attempt")["result"]!.AsArray().Insert(0, direct);
        scenario = PrepareProcedurePublicationScenario(scenario with { ExpectedIntentCount = 2 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId), "distinct tagged local refs");
        var resolution = (MortalWoundTreatmentResolution)flow.Resolution;
        var directIntent = (MortalWoundAddComplicationOutcomeIntent)resolution.OutcomeIntents[0];
        var policy = PreparedPolicyAt(flow, 1);
        var plan = ComposeCoordinatedTreatmentPlan(fixture, flow);
        Assert.True(WoundAcceptedTurnPlanner.TryGetTreatmentSelectedGraph(plan.WoundStageBundle!.PreparedPlan, out var graph));
        Assert.Equal(new[] { WoundWorkingReferenceOrigin.DirectAddition, WoundWorkingReferenceOrigin.PolicyAddition }, graph!.Additions.Select(row => row.Reference.Origin));
        Assert.Equal(new[] { new WoundWorkingOperationAddress(0, 0), new WoundWorkingOperationAddress(0, 1) }, graph.Additions.Select(row => row.Reference.Address));
        Assert.All(graph.Additions, row => Assert.Equal("policy_preparation", row.Reference.Value));
        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan)) publication.CompleteAtFullPipelineEnd();
        Assert.Equal(new[] { directIntent.ComplicationId, policy.ComplicationBinding!.ComplicationId },
            fixture.ReadCurrentWound().Complications.Select(row => row.ComplicationId));
    }

    [Fact]
    public void DeteriorationPublication_TaggedGraph_RepeatedEffectfulPolicyRejectsBeforeClaim()
    {
        var scenario = CreatePolicyPreparationScenario("add_complication");
        var selected = scenario.Before["treatment"]!["routes"]![0]!["outcomes"]!.AsArray().OfType<JsonObject>()
            .Single(row => row["category"]!.GetValue<string>() == "failed_attempt")["result"]!.AsArray();
        selected.Add(selected[0]!.DeepClone());
        scenario = PrepareProcedurePublicationScenario(scenario with { ExpectedIntentCount = 2 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        var state = (MortalWoundTreatmentAcceptedStateAuthority)fixture.GetAcceptedState();
        var tree = CaptureResolverFixtureTree(fixture.Root);
        var request = MortalWoundTreatmentPlanner.PrepareProcedureRequest(state, fixture.ReadCurrentHistory(), fixture.ReadCurrentWound(),
            scenario.OperationKey, scenario.RouteId, fixture.AcceptedEventRef(state));
        Assert.False(request.IsValid);
        Assert.Contains(request.Issues, issue => issue.Code == "mortal_wound_treatment_procedure_band_inapplicable");
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
    }

    [Theory]
    [InlineData("i")]
    [InlineData("r1,i")]
    public void DeteriorationPublication_FinalRank_OriginalFourNeverBorrowsIntermediateReduction(string sequence)
    {
        var scenario = PolicySequenceScenario(4, 3, sequence);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var state = (MortalWoundTreatmentAcceptedStateAuthority)fixture.GetAcceptedState();
        var tree = CaptureResolverFixtureTree(fixture.Root);
        var request = MortalWoundTreatmentPlanner.PrepareProcedureRequest(state, fixture.ReadCurrentHistory(), fixture.ReadCurrentWound(),
            scenario.OperationKey, scenario.RouteId, fixture.AcceptedEventRef(state));
        Assert.False(request.IsValid);
        Assert.Contains(request.Issues, issue => issue.Code == "mortal_wound_treatment_procedure_band_inapplicable");
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
    }

    [Fact]
    public void DeteriorationPublication_PrivateAuthority_CorruptPacketsRejectWithoutCopyExceptions()
    {
        var scenario = CreatePolicyPreparationScenario("add_complication");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId);
        var resolution = (MortalWoundTreatmentResolution)flow.Resolution;
        var request = (MortalWoundTreatmentAttemptRequest)flow.Request;
        var accepted = (MortalWoundTreatmentAcceptedStateAuthority)flow.AcceptedState;
        var intent = (MortalWoundApplyDeteriorationOutcomeIntent)Assert.Single(resolution.OutcomeIntents);
        var prepared = PreparedPolicyAt(flow, 0);
        var binding = prepared.ComplicationBinding!;
        var tree = CaptureResolverFixtureTree(fixture.Root);
        var corrupt = new[]
        {
            ClonePolicyField(prepared, "_policy", null),
            ClonePolicyField(prepared, "_draft", null),
            ClonePolicyField(prepared, "_binding", null),
            ClonePolicyField(prepared, "_fingerprint", null),
            ClonePolicyField(prepared, "_woundSourcePath", "foreign.path"),
            ClonePolicyField(prepared, "_ordinal", 1),
            ClonePolicyField(prepared, "_binding", ClonePolicyField(binding,
                "<DefinitionReferenceBindings>k__BackingField", default(ImmutableArray<MortalWoundTreatmentReferenceBinding>))),
            ClonePolicyField(prepared, "_binding", ClonePolicyField(binding,
                "<ApplicationReferenceBindings>k__BackingField", default(ImmutableArray<MortalWoundTreatmentReferenceBinding>))),
            ClonePolicyField(prepared, "_binding", ClonePolicyField(binding,
                "<Roots>k__BackingField", default(ImmutableArray<MortalWoundTreatmentComplicationRootBinding>))),
            ClonePolicyField(prepared, "_binding", ClonePolicyField(binding,
                "<ComplicationId>k__BackingField", "forged_complication")),
            ClonePolicyField(prepared, "_policy", prepared.Policy with { GraceMinutes = prepared.Policy.GraceMinutes + 1 })
        };
        foreach (var packet in corrupt)
        {
            var forged = ClonePolicyField(intent, "_preparedDeterioration", packet);
            var changed = CloneResolutionWithIntentsUnchecked(resolution, new[] { forged });
            var error = Record.Exception(() =>
            {
                var result = MortalWoundTreatmentOutcomePublicationPlanner.Prepare(accepted, request, changed, accepted.CurrentGameMinute);
                Assert.False(result.IsValid);
                Assert.Contains(result.Issues, issue => issue.Code == "mortal_wound_treatment_publication_slice_unsupported");
            });
            Assert.Null(error);
            AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        }
    }

    private static ResolverScenario PolicySequenceScenario(int rank, int budget, string sequence)
    {
        var scenario = CreatePolicyPreparationScenario("increase_severity");
        scenario.Before["severity"]!["rank"] = rank;
        scenario.Before["severity"]!["value"] = new[] { "", "I", "II", "III", "IV" }[rank];
        scenario.Before["consequences"]!["slotBudget"] = budget;
        foreach (var band in scenario.Before["treatment"]!["routes"]![0]!["outcomes"]!.AsArray().OfType<JsonObject>())
            band["result"] = band["category"]!.GetValue<string>() == "failed_attempt"
                ? new JsonArray(sequence.Split(',').Select(token => (JsonNode)(token == "i"
                    ? new JsonObject { ["kind"] = "apply_deterioration", ["policyRef"] = "policy_preparation" }
                    : new JsonObject { ["kind"] = "reduce_severity", ["steps"] = token == "r2" ? 2 : 1 })).ToArray())
                : new JsonArray(new JsonObject { ["kind"] = "stabilize" });
        return PrepareProcedurePublicationScenario(scenario with { ExpectedIntentCount = sequence.Split(',').Length });
    }

    [Theory]
    [InlineData(1, 1, "i", 2, 1)]
    [InlineData(2, 2, "i", 3, 2)]
    [InlineData(3, 3, "i", 4, 3)]
    [InlineData(3, 3, "i,r1", 3, 3)]
    [InlineData(2, 1, "r1,i", 2, 1)]
    [InlineData(2, 2, "i,r1", 2, 2)]
    [InlineData(3, 3, "i,r2", 2, 2)]
    public void DeteriorationPublication_FinalRank_OneFinalGeneration(int rank, int budget,
        string sequence, int expectedRank, int expectedBudget)
    {
        var scenario = PolicySequenceScenario(rank, budget, sequence);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var before = fixture.ReadCurrentWound();
        var originalRoot = Assert.Single(before.Consequences.OwnedEffectSources.RootBindings).EffectId;
        var indexBefore = fixture.ReadEffectIdentityIndex()["entries"]!.AsArray()
            .Single(row => row!["effectId"]!.GetValue<string>() == originalRoot)!.ToJsonString();
        var carrierBefore = fixture.ReadPlayerEffectCarrier()["activeEffects"]!.AsArray()
            .Single(row => row!["effectId"]!.GetValue<string>() == originalRoot)!.DeepClone();
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId), "one final policy rank");
        var plan = ComposeCoordinatedTreatmentPlan(fixture, flow);
        var batch = Assert.Single(plan.WoundStageBundle!.PreparedPlan.EffectOperationBatches);
        Assert.Equal(rank == expectedRank ? 0 : 1, batch.RootApplications.Count);
        Assert.Equal(rank == expectedRank ? 0 : 1, batch.TerminalOperations.Count);
        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan)) publication.CompleteAtFullPipelineEnd();
        var after = fixture.ReadCurrentWound();
        Assert.Equal(expectedRank, after.Severity.Rank);
        Assert.Equal(expectedBudget, after.Consequences.SlotBudget);
        Assert.Equal(before.Severity.MaximumAtCreation, after.Severity.MaximumAtCreation);
        Assert.Equal(before.Recovery.CurrentStepProgress, after.Recovery.CurrentStepProgress);
        Assert.Equal(before.Care.State, after.Care.State);
        if (rank == expectedRank)
        {
            Assert.Equal(before.Severity, after.Severity);
            Assert.Equal(originalRoot, Assert.Single(after.Consequences.OwnedEffectSources.RootBindings).EffectId);
            Assert.Equal(indexBefore, fixture.ReadEffectIdentityIndex()["entries"]!.AsArray()
                .Single(row => row!["effectId"]!.GetValue<string>() == originalRoot)!.ToJsonString());
            Assert.True(JsonNode.DeepEquals(carrierBefore, fixture.ReadPlayerEffectCarrier()["activeEffects"]!.AsArray()
                .Single(row => row!["effectId"]!.GetValue<string>() == originalRoot)));
        }
        else Assert.NotEqual(originalRoot, Assert.Single(after.Consequences.OwnedEffectSources.RootBindings).EffectId);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DeteriorationPublication_TaggedGraph_ExactPreparedIdentities(bool effectless, bool repeat)
    {
        var scenario = CreatePolicyPreparationScenario("add_complication", effectless);
        if (repeat)
        {
            var selected = scenario.Before["treatment"]!["routes"]![0]!["outcomes"]!.AsArray().OfType<JsonObject>()
                .Single(row => row["category"]!.GetValue<string>() == "failed_attempt")["result"]!.AsArray();
            selected.Add(selected[0]!.DeepClone());
            scenario = PrepareProcedurePublicationScenario(scenario with { ExpectedIntentCount = 2 });
        }
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId), "tagged policy graph");
        var expected = Enumerable.Range(0, repeat ? 2 : 1).Select(i => PreparedPolicyAt(flow, i).ComplicationBinding!.ComplicationId).ToArray();
        var plan = ComposeCoordinatedTreatmentPlan(fixture, flow);
        var batch = Assert.Single(plan.WoundStageBundle!.PreparedPlan.EffectOperationBatches);
        Assert.Equal(effectless ? 0 : 1, batch.RootApplications.Count);
        Assert.Empty(batch.TerminalOperations);
        Assert.All(batch.RootApplications, root => Assert.Null(root.PriorRootEffectId));
        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan)) publication.CompleteAtFullPipelineEnd();
        Assert.Equal(expected, fixture.ReadCurrentWound().Complications.Select(row => row.ComplicationId));
        fixture.RestartForReplay();
        var replayTree = CaptureResolverFixtureTree(fixture.Root);
        Assert.Equal("ExactReplay", Convert.ToString(ReadRequiredProperty(ProbePublishedTreatment(fixture, flow.Request), "Status")));
        AssertResolverFixtureTreeUnchanged(fixture.Root, replayTree);
    }

    [Fact]
    public void DeteriorationPublication_PrivateAuthority_PublicHashOnlyIntentRejects()
    {
        var scenario = CreatePolicyPreparationScenario("add_complication", true);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId);
        var resolution = (MortalWoundTreatmentResolution)flow.Resolution;
        var request = (MortalWoundTreatmentAttemptRequest)flow.Request;
        var accepted = (MortalWoundTreatmentAcceptedStateAuthority)flow.AcceptedState;
        var intent = Assert.IsType<MortalWoundApplyDeteriorationOutcomeIntent>(Assert.Single(resolution.OutcomeIntents));
        var forged = MortalWoundApplyDeteriorationOutcomeIntent.Create(intent.OperationOrdinal,
            intent.DeclaredOperationFingerprint, intent.IntentFingerprint, intent.PolicyRef,
            intent.DeteriorationAuthorityFingerprint);
        var tree = CaptureResolverFixtureTree(fixture.Root);
        var result = MortalWoundTreatmentOutcomePublicationPlanner.Prepare(accepted, request,
            CloneResolutionWithIntentsUnchecked(resolution, new[] { forged }), accepted.CurrentGameMinute);
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "mortal_wound_treatment_publication_slice_unsupported");
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
    }

    [Theory]
    [InlineData("increase_severity", false)]
    [InlineData("add_complication", true)]
    public void DeteriorationPublication_RealSelectedPolicyPublishes(string kind, bool effectless)
    {
        var scenario = CreatePolicyPreparationScenario(kind, effectless);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var before = fixture.ReadCurrentWound();
        var count = fixture.ReadNpcItemCount("sterile_thread");
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId),
            "selected non-death policy");
        var prepared = PreparedPolicyAt(flow, 0);
        var plan = ComposeCoordinatedTreatmentPlan(fixture, flow);
        var batch = Assert.Single(plan.WoundStageBundle!.PreparedPlan.EffectOperationBatches);
        if (effectless)
        {
            Assert.Empty(batch.RootApplications);
            Assert.Empty(batch.TerminalOperations);
        }
        else
        {
            Assert.Single(batch.RootApplications);
            Assert.Single(batch.TerminalOperations);
        }
        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan))
            publication.CompleteAtFullPipelineEnd();
        var after = fixture.ReadCurrentWound();
        Assert.Equal(kind == "increase_severity" ? before.Severity.Rank + 1 : before.Severity.Rank,
            after.Severity.Rank);
        if (effectless)
            Assert.Equal(prepared.ComplicationBinding!.ComplicationId,
                Assert.Single(after.Complications).ComplicationId);
        Assert.Equal(count - 1, fixture.ReadNpcItemCount("sterile_thread"));
        var transition = Assert.Single(fixture.ReadCurrentHistory().State!.Transitions,
            row => row.Kind == "treat");
        AssertClosedTreatmentReceipt(transition.TreatmentResult!.Receipt, flow.Request, flow.Resolution);
    }
}
