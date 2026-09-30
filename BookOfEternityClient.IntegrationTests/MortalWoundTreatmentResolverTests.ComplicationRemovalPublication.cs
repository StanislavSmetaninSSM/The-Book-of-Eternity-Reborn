using System.Text.Json.Nodes;
using System.Reflection;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    private static ResolverScenario CreateComplicationRemovalPublicationScenario()
    {
        var scenario = ConfigureCourseConsequenceEnvelope(
            CreateScalarCoursePublicationScenario(), true);
        var definitions = scenario.Before["consequences"]!["ownedEffectSources"]!["definitions"]!.AsArray();
        var passive = WoundContractTestData.CreateOwnedEffectDefinition(
            scenario.Before["woundId"]!.GetValue<string>(), "mortal_world",
            "definition_t066_course_base", "characteristic_modifier");
        passive["triggers"] = new JsonArray();
        passive["components"]![0]!["payload"]!["value"] = -1;
        definitions[0] = passive;
        scenario.Before["consequences"]!["entries"]![0]!["profileKey"] = "characteristic_modifier";
        scenario.Before["treatment"]!["routes"]![0]!["outcomes"]![0]!["result"] =
            new JsonArray(new JsonObject
            {
                ["kind"] = "remove_complication",
                ["complicationId"] = "complication_t066_course_pressure"
            });
        return scenario with
        {
            OperationKey = "operation_t070_complication_removal",
            ExpectedIntentCount = 1,
            History = CreateCurrentWoundHistory(scenario.Before),
            SeedCanonicalWoundEffects = true
        };
    }

    [Fact]
    public void ComplicationRemovalPublication_RemovesOnlyOwnedRootsAndPreservesBaseConsequence()
    {
        var scenario = CreateComplicationRemovalPublicationScenario();
        using var fixture = AcceptedStateFixture.Create(scenario);
        var before = fixture.ReadCurrentWound();
        var carrierBefore = fixture.ReadPlayerEffectCarrier();
        var indexBefore = fixture.ReadEffectIdentityIndex();
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "course", scenario.OperationKey, scenario.RouteId),
            "selective complication removal");
        ComposeAndPublishCoordinatedTreatment(fixture, flow);
        var after = fixture.ReadCurrentWound();
        Assert.Empty(after.Complications);
        Assert.Equal("effect_t066_course_base",
            Assert.Single(after.Consequences.OwnedEffectSources.RootBindings).EffectId);
        Assert.Single(after.Consequences.Entries);
        Assert.True(JsonNode.DeepEquals(CanonicalWoundRoot(before)["severity"], CanonicalWoundRoot(after)["severity"]));
        Assert.True(JsonNode.DeepEquals(CanonicalWoundRoot(before)["recovery"], CanonicalWoundRoot(after)["recovery"]));
        var carrierAfter = fixture.ReadPlayerEffectCarrier();
        var indexAfter = fixture.ReadEffectIdentityIndex();
        Assert.True(JsonNode.DeepEquals(
            carrierBefore["activeEffects"]!.AsArray().Single(row => row!["effectId"]!.GetValue<string>() == "effect_t066_course_base"),
            carrierAfter["activeEffects"]!.AsArray().Single(row => row!["effectId"]!.GetValue<string>() == "effect_t066_course_base")));
        Assert.True(JsonNode.DeepEquals(
            indexBefore["entries"]!.AsArray().Single(row => row!["effectId"]!.GetValue<string>() == "effect_t066_course_base"),
            indexAfter["entries"]!.AsArray().Single(row => row!["effectId"]!.GetValue<string>() == "effect_t066_course_base")));
        Assert.Equal(indexBefore["entries"]!.AsArray().Count, indexAfter["entries"]!.AsArray().Count);
        foreach (var effectId in new[] { "effect_t066_course_complication_a", "effect_t066_course_complication_b" })
        {
            Assert.DoesNotContain(carrierAfter["activeEffects"]!.AsArray(), row => row!["effectId"]!.GetValue<string>() == effectId);
            var prior = indexBefore["entries"]!.AsArray().Single(row => row!["effectId"]!.GetValue<string>() == effectId)!;
            var terminal = indexAfter["entries"]!.AsArray().Single(row => row!["effectId"]!.GetValue<string>() == effectId)!;
            Assert.Equal("expired", terminal["state"]!.GetValue<string>());
            Assert.Equal(prior["transitions"]!.AsArray().Count + 1, terminal["transitions"]!.AsArray().Count);
            for (var ordinal = 0; ordinal < prior["transitions"]!.AsArray().Count; ordinal++)
                Assert.True(JsonNode.DeepEquals(prior["transitions"]![ordinal], terminal["transitions"]![ordinal]));
        }
        Assert.Equal(7, fixture.ReadPlayerItemCount("antibiotic_dose"));
        Assert.Null(after.Care.ActiveCourseId);
        Assert.Equal(scenario.RouteId, Assert.Single(after.Treatment.CompletedRouteIds));
        var treatment = Assert.Single(fixture.ReadCurrentHistory().State!.Transitions, row => row.Kind == "treat");
        AssertClosedTreatmentReceipt(treatment.TreatmentResult!.Receipt, flow.Request, flow.Resolution);
        fixture.RestartForReplay();
        var tree = CaptureResolverFixtureTree(fixture.Root);
        var replay = ProbePublishedTreatment(fixture, flow.Request);
        Assert.Equal("ExactReplay", Convert.ToString(ReadRequiredProperty(replay, "Status")));
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
    }

    private static ResolverScenario CreateRemovalLineageScenario(
        bool effectless, string rootState = "active", string childState = "active",
        WoundLineageSeedDefect defect = WoundLineageSeedDefect.None)
    {
        var scenario = CreateComplicationRemovalPublicationScenario();
        var wound = scenario.Before;
        var woundId = wound["woundId"]!.GetValue<string>();
        const string rootId = "effect_t070_removal_reaction";
        const string childId = "effect_t070_removal_child";
        const string rootKey = "definition_t070_removal_reaction";
        const string childKey = "definition_t070_removal_child";
        var root = WoundContractTestData.CreateApplyDefinitionRoot(woundId, "mortal_world", rootKey, childKey);
        var child = WoundContractTestData.CreateOwnedEffectDefinition(woundId, "mortal_world", childKey, "wound_consequence");
        child["triggers"] = new JsonArray();
        var definitions = new JsonArray(root, child);
        var bindings = new JsonArray(WoundContractTestData.CreateRootBinding(rootId, rootKey));
        var entries = new JsonArray(new JsonObject
        {
            ["slot"] = 1, ["profileKey"] = "event_reaction", ["effectId"] = rootId,
            ["readableSummary"] = "Связанная реакция раны."
        });
        var seed = new List<WoundLineageSeedEntry>
        {
            new(rootId, rootKey, null, rootState),
            new(childId, childKey, rootId, childState)
        };
        if (!effectless)
        {
            definitions.Add(wound["consequences"]!["ownedEffectSources"]!["definitions"]![0]!.DeepClone());
            bindings.Add(WoundContractTestData.CreateRootBinding("effect_t066_course_base", "definition_t066_course_base"));
            var entry = wound["consequences"]!["entries"]![0]!.DeepClone();
            entry["slot"] = 2;
            entries.Add(entry);
            seed.Add(new("effect_t066_course_base", "definition_t066_course_base", null, "active"));
        }
        wound["complications"]![0]!["ownedEffectIds"] = effectless ? new JsonArray() : new JsonArray(rootId);
        wound["consequences"]!["ownedEffectSources"] = new JsonObject
        {
            ["definitions"] = definitions, ["rootBindings"] = bindings
        };
        wound["consequences"]!["entries"] = entries;
        wound["consequences"]!["slotsUsed"] = entries.Count;
        return scenario with { History = CreateCurrentWoundHistory(wound), LineageSeed = new(seed, defect) };
    }

    [Theory]
    [InlineData("active")]
    [InlineData("suspended")]
    public void ComplicationRemovalPublication_SelectedLineageRetiresOnlyLiveDescendants(string childState) =>
        AssertRemovalLineagePublication(CreateRemovalLineageScenario(false, childState: childState),
            new[] { "effect_t070_removal_reaction", "effect_t070_removal_child" });

    [Fact]
    public void ComplicationRemovalPublication_TerminalOwnedRootRetiresLiveDescendantWithoutDuplicatingHistory() =>
        AssertRemovalLineagePublication(CreateRemovalLineageScenario(false, rootState: "removed"),
            new[] { "effect_t070_removal_child" });

    [Fact]
    public void ComplicationRemovalPublication_EffectlessRemovalPreservesUnrelatedReactionLineage() =>
        AssertRemovalLineagePublication(CreateRemovalLineageScenario(true), Array.Empty<string>());

    private static void AssertRemovalLineagePublication(ResolverScenario scenario, string[] retiredIds)
    {
        using var fixture = AcceptedStateFixture.Create(scenario);
        var carrierBefore = fixture.ReadPlayerEffectCarrier();
        var indexBefore = fixture.ReadEffectIdentityIndex();
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "course", scenario.OperationKey, scenario.RouteId),
            "accepted initial lineage removal");
        WoundPreparedAcceptedTurnPlan? prepared = null;
        CaptureTreatmentPreparedAtFinalization(fixture, value => prepared = value);
        ComposeAndPublishCoordinatedTreatment(fixture, flow);
        var batch = Assert.Single(prepared!.EffectOperationBatches);
        Assert.Empty(batch.RootApplications);
        Assert.Equal(retiredIds.OrderBy(id => id, StringComparer.Ordinal),
            batch.TerminalOperations.Select(row => row.EffectId));
        var carrierAfter = fixture.ReadPlayerEffectCarrier();
        var indexAfter = fixture.ReadEffectIdentityIndex();
        Assert.Equal(indexBefore["entries"]!.AsArray().Count, indexAfter["entries"]!.AsArray().Count);
        foreach (var prior in indexBefore["entries"]!.AsArray())
        {
            var id = prior!["effectId"]!.GetValue<string>();
            var current = indexAfter["entries"]!.AsArray().Single(row => row!["effectId"]!.GetValue<string>() == id)!;
            if (retiredIds.Contains(id, StringComparer.Ordinal))
            {
                Assert.Equal("expired", current["state"]!.GetValue<string>());
                Assert.Equal(prior["transitions"]!.AsArray().Count + 1, current["transitions"]!.AsArray().Count);
                for (var ordinal = 0; ordinal < prior["transitions"]!.AsArray().Count; ordinal++)
                    Assert.True(JsonNode.DeepEquals(prior["transitions"]![ordinal], current["transitions"]![ordinal]));
            }
            else Assert.True(JsonNode.DeepEquals(prior, current));
        }
        var expectedCarrier = carrierBefore.DeepClone();
        foreach (var row in expectedCarrier["activeEffects"]!.AsArray().Where(row =>
                     retiredIds.Contains(row!["effectId"]!.GetValue<string>(), StringComparer.Ordinal)).ToArray())
            expectedCarrier["activeEffects"]!.AsArray().Remove(row);
        Assert.True(JsonNode.DeepEquals(expectedCarrier, carrierAfter));
        Assert.Empty(fixture.ReadCurrentWound().Complications);
    }

    [Theory]
    [InlineData(false, "accepted_mechanics_wound_lineage_create_invalid")]
    [InlineData(true, "accepted_mechanics_wound_lineage_foreign_child")]
    public void ComplicationRemovalPublication_LineageDisagreementRejectsBeforeWrites(bool foreign, string expectedCode)
    {
        var scenario = CreateRemovalLineageScenario(false, childState: foreign ? "removed" : "active",
            defect: foreign ? WoundLineageSeedDefect.TerminalForeignChild : WoundLineageSeedDefect.MultipleFirstCreateParents);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "course", scenario.OperationKey, scenario.RouteId), "invalid accepted initial lineage");
        var tree = CaptureResolverFixtureTree(fixture.Root);
        var rejected = ComposeResourcePublicationResult(fixture, flow);
        Assert.False(rejected.IsValid);
        Assert.Null(rejected.Plan);
        Assert.Contains(rejected.Issues, issue => issue.Code == expectedCode);
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
    }

    private static ResolverScenario CreateEffectlessRemovalScenario()
    {
        var scenario = CreateComplicationRemovalPublicationScenario();
        var consequences = scenario.Before["consequences"]!;
        var sources = consequences["ownedEffectSources"]!;
        sources["definitions"] = new JsonArray(sources["definitions"]![0]!.DeepClone());
        sources["rootBindings"] = new JsonArray(sources["rootBindings"]![0]!.DeepClone());
        consequences["entries"] = new JsonArray(consequences["entries"]![0]!.DeepClone());
        consequences["slotsUsed"] = 1;
        scenario.Before["complications"]![0]!["ownedEffectIds"] = new JsonArray();
        return scenario with { History = CreateCurrentWoundHistory(scenario.Before) };
    }

    [Fact]
    public void ComplicationRemovalPublication_EffectlessComplicationStillRequiresAuthenticatedEmptyBatch()
    {
        var scenario = CreateEffectlessRemovalScenario();
        using var fixture = AcceptedStateFixture.Create(scenario);
        var carrier = fixture.ReadPlayerEffectCarrier();
        var identity = fixture.ReadEffectIdentityIndex();
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "course", scenario.OperationKey, scenario.RouteId), "effectless removal");
        var plan = ComposeCoordinatedTreatmentPlan(fixture, flow);
        var bundle = Assert.IsType<AcceptedMechanicsWoundStageBundle>(plan.WoundStageBundle);
        var prepared = bundle.PreparedPlan;
        var batch = Assert.Single(prepared.EffectOperationBatches);
        Assert.Empty(batch.RootApplications);
        Assert.Empty(batch.TerminalOperations);
        Assert.Equal("effect_t066_course_base", Assert.Single(batch.RootLineageAuthority).EffectId);
        Assert.True(WoundAcceptedTurnPlanner.TryReadTreatmentContinuation(prepared.TreatmentContinuationAuthority!, out var continuation));
        Assert.NotNull(continuation.RematerializationAuthority);
        var missingBatch = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(continuation.OutcomePreparation,
            continuation.Resolution, null, new Dictionary<string, EffectAcceptedApplicationResult>());
        Assert.Contains(missingBatch.Issues, issue => issue.Code == "mortal_wound_treatment_outcome_effect_handoff_mismatch" &&
            issue.Expected == "one authenticated treatment graph batch and exact application map (including empty)");
        var tree = CaptureResolverFixtureTree(fixture.Root);
        foreach (var mutation in new[] { "missing_batch", "missing_lineage", "changed_lineage", "foreign_proof", "missing_proof_and_batch" })
        {
            var authority = prepared.TreatmentContinuationAuthority!;
            var batches = prepared.EffectOperationBatches.ToArray();
            if (mutation is "missing_batch" or "missing_proof_and_batch") batches = Array.Empty<WoundEffectOperationBatch>();
            if (mutation is "missing_lineage" or "changed_lineage")
                batches = new[] { ResealRemovalBatch(batch, lineage: mutation == "missing_lineage"
                    ? Array.Empty<WoundRootLineageAuthorityRow>()
                    : batch.RootLineageAuthority.Select(row => row with { OwnershipDomain = WoundRootOwnershipDomain.ForComplication("foreign_complication") }).ToArray()) };
            if (mutation is "foreign_proof" or "missing_proof_and_batch")
            {
                authority = typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(authority, null)!;
                authority.GetType().GetField("_rematerializationAuthority", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .SetValue(authority, mutation == "missing_proof_and_batch" ? null : continuation.RematerializationAuthority! with { AttemptId = "foreign_attempt" });
            }
            var forged = mutation == "missing_proof_and_batch"
                ? new WoundPreparedAcceptedTurnPlan(prepared.Binding, prepared.BindingFingerprint,
                    prepared.InputFingerprint, prepared.WoundPreparationFingerprint, prepared.AllocatedWoundIds,
                    prepared.AllocatedTransitionIds, prepared.PreparedWounds, batches, prepared.BaselineAuthority, authority)
                : ResealRemovalPrepared(prepared, batches, authority);
            var validated = WoundAcceptedTurnPlanCache.ValidatePreparedResult(bundle.Input,
                WoundAcceptedTurnFingerprints.ComputeInput(bundle.Input),
                new WoundAcceptedTurnPreparationResult(forged, Array.Empty<ValidationIssue>()));
            Assert.False(validated.Success, mutation);
            Assert.NotEmpty(validated.Issues);
            var forgedBundle = typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(bundle, null)!;
            typeof(AcceptedMechanicsWoundStageBundle).GetField("_preparedPlan", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(forgedBundle, forged);
            Assert.False(WoundAcceptedTurnPlanner.TreatmentContinuationFinalPlanAgrees(authority,
                (AcceptedMechanicsWoundStageBundle)forgedBundle), mutation);
            AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        }
        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan)) publication.CompleteAtFullPipelineEnd();
        Assert.Empty(fixture.ReadCurrentWound().Complications);
        Assert.True(JsonNode.DeepEquals(carrier, fixture.ReadPlayerEffectCarrier()));
        Assert.True(JsonNode.DeepEquals(identity, fixture.ReadEffectIdentityIndex()));
    }

    private static WoundEffectOperationBatch ResealRemovalBatch(WoundEffectOperationBatch source,
        IReadOnlyList<WoundRootLineageAuthorityRow>? lineage = null,
        IReadOnlyList<WoundTerminalEffectOperation>? terminals = null,
        WoundEffectSourceExport? export = null,
        IReadOnlyList<WoundRootEffectApplication>? applications = null)
    {
        var provisional = new WoundEffectOperationBatch(source.LocalWoundRef, source.PreparedWoundId,
            export ?? source.SourceExport, applications ?? source.RootApplications, terminals ?? source.TerminalOperations,
            lineage ?? source.RootLineageAuthority, string.Empty, source.TransitionAuthority);
        return new WoundEffectOperationBatch(provisional.LocalWoundRef, provisional.PreparedWoundId,
            provisional.SourceExport, provisional.RootApplications, provisional.TerminalOperations,
            provisional.RootLineageAuthority, WoundAcceptedTurnFingerprints.ComputeSourceExport(provisional), provisional.TransitionAuthority);
    }

    private static WoundPreparedAcceptedTurnPlan ResealRemovalPrepared(WoundPreparedAcceptedTurnPlan source,
        IReadOnlyList<WoundEffectOperationBatch> batches, object authority)
    {
        var provisional = new WoundPreparedAcceptedTurnPlan(source.Binding, source.BindingFingerprint,
            source.InputFingerprint, string.Empty, source.AllocatedWoundIds, source.AllocatedTransitionIds,
            source.PreparedWounds, batches, source.BaselineAuthority, authority);
        return new WoundPreparedAcceptedTurnPlan(provisional.Binding, provisional.BindingFingerprint,
            provisional.InputFingerprint, WoundAcceptedTurnFingerprints.ComputePreparation(provisional),
            provisional.AllocatedWoundIds, provisional.AllocatedTransitionIds, provisional.PreparedWounds,
            provisional.EffectOperationBatches, provisional.BaselineAuthority, authority);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ComplicationRemovalPublication_MixedReductionRematerializesOnlySurvivingCoordinates(bool noSurvivor, bool alreadyTerminal)
    {
        {
            var scenario = CreateComplicationRemovalPublicationScenario();
            scenario.Before["treatment"]!["routes"]![0]!["outcomes"]![0]!["result"]!.AsArray()
                .Add(new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 2 });
            if (noSurvivor)
                scenario.Before["complications"]![0]!["ownedEffectIds"]!.AsArray().Add("effect_t066_course_base");
            scenario = scenario with { History = CreateCurrentWoundHistory(scenario.Before), ExpectedIntentCount = 2 };
            if (alreadyTerminal)
                scenario = scenario with { LineageSeed = new WoundLineageSeed(
                    scenario.Before["consequences"]!["ownedEffectSources"]!["rootBindings"]!.AsArray()
                        .Select(root => new WoundLineageSeedEntry(root!["effectId"]!.GetValue<string>(),
                            root["definitionKey"]!.GetValue<string>(), null, "removed")).ToArray()) };
            using var fixture = AcceptedStateFixture.Create(scenario);
            var before = fixture.ReadCurrentWound();
            var initialIndex = fixture.ReadEffectIdentityIndex();
            Assert.Equal("untreated", before.Care.State);
            Assert.Contains("not_stabilized", before.Recovery.Blockers);
            var flow = PersistAndRehydrateTreatmentPublication(fixture,
                ResolveCurrentTreatment(fixture, "course", scenario.OperationKey, scenario.RouteId), "removal and reduction");
            var plan = ComposeCoordinatedTreatmentPlan(fixture, flow);
            var batch = Assert.Single(plan.WoundStageBundle!.PreparedPlan.EffectOperationBatches);
            Assert.Equal(alreadyTerminal ? 0 : 3, batch.TerminalOperations.Count);
            Assert.Equal(noSurvivor ? 0 : 1, batch.RootApplications.Count);
            Assert.Equal(noSurvivor ? 0 : 1, batch.SourceExport.Definitions.Count);
            Assert.Equal(noSurvivor ? 0 : 1, batch.RootLineageAuthority.Count);
            if (!noSurvivor)
            {
                var application = Assert.Single(batch.RootApplications);
                Assert.Equal("effect_t066_course_base", application.PriorRootEffectId);
                Assert.Equal("definition_t066_course_base", application.DefinitionKey);
            }
            using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan)) publication.CompleteAtFullPipelineEnd();
            var after = fixture.ReadCurrentWound();
            Assert.Equal(1, after.Severity.Rank);
            Assert.Equal("active", after.Lifecycle);
            Assert.Empty(after.Complications);
            Assert.Equal(noSurvivor ? 0 : 1, after.Consequences.OwnedEffectSources.RootBindings.Count);
            Assert.Empty(before.Consequences.OwnedEffectSources.RootBindings.Select(row => row.EffectId)
                .Intersect(after.Consequences.OwnedEffectSources.RootBindings.Select(row => row.EffectId)));
            if (alreadyTerminal)
                foreach (var prior in initialIndex["entries"]!.AsArray())
                    Assert.True(JsonNode.DeepEquals(prior, fixture.ReadEffectIdentityIndex()["entries"]!.AsArray()
                        .Single(row => row!["effectId"]!.GetValue<string>() == prior!["effectId"]!.GetValue<string>())));
            if (!noSurvivor)
            {
                var createdId = Assert.Single(after.Consequences.OwnedEffectSources.RootBindings).EffectId;
                var created = fixture.ReadEffectIdentityIndex()["entries"]!.AsArray()
                    .Single(row => row!["effectId"]!.GetValue<string>() == createdId)!;
                Assert.Equal("effect_t066_course_base", Assert.Single(created["transitions"]![0]!["sourceEffectIds"]!.AsArray())!.GetValue<string>());
            }
        }
    }

    [Fact]
    public void ComplicationRemovalPublication_PostWriteFailureRestoresWoundEffectsResourcesAndHistoryThenRetriesOnce()
    {
        var scenario = CreateComplicationRemovalPublicationScenario();
        var fault = new ResourcePublicationFailureInjection();
        using var fixture = AcceptedStateFixture.Create(scenario, new FileSystemManagerHooks
        { BeforeCanonicalMutationAsync = fault.BeforeCanonicalMutationAsync });
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "course", scenario.OperationKey, scenario.RouteId), "removal rollback");
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
        Assert.Equal(7, fixture.ReadPlayerItemCount("antibiotic_dose"));
        Assert.Empty(fixture.ReadCurrentWound().Complications);
        Assert.Single(fixture.ReadCurrentHistory().State!.Transitions, row => row.Kind == "treat");
        foreach (var id in new[] { "effect_t066_course_complication_a", "effect_t066_course_complication_b" })
            Assert.Single(fixture.ReadEffectIdentityIndex()["entries"]!.AsArray()
                .Single(row => row!["effectId"]!.GetValue<string>() == id)!["transitions"]!.AsArray(),
                row => row!["kind"]!.GetValue<string>() == "expire");
        fixture.RestartForReplay();
        var publishedTree = CaptureResolverFixtureTree(fixture.Root);
        Assert.Equal("ExactReplay", Convert.ToString(ReadRequiredProperty(ProbePublishedTreatment(fixture, flow.Request), "Status")));
        AssertResolverFixtureTreeUnchanged(fixture.Root, publishedTree);
    }

    private static JsonArray RemovalOperations(string shape) => new(shape.Split(',').Select(token => (JsonNode)(token switch
    {
        "s" => new JsonObject { ["kind"] = "stabilize" },
        "a" => new JsonObject { ["kind"] = "add_recovery", ["points"] = 1 },
        "r1" or "r2" => new JsonObject { ["kind"] = "reduce_severity", ["steps"] = token == "r1" ? 1 : 2 },
        _ => new JsonObject { ["kind"] = "remove_complication", ["complicationId"] = token switch
        { "m" => "complication_t066_course_pressure", "n" => "complication_t070_second", _ => "complication_missing" } }
    })).ToArray());

    [Fact]
    public void ComplicationRemovalPublication_PlainReductionRetainsTerminalParentThroughRollbackAndColdReplay()
    {
        const string priorId = "effect_t070_recovery_characteristic";
        var scenario = CreateRecoveryPublicationScenario("procedure", "success", "r1") with
        {
            LineageSeed = new WoundLineageSeed(new[]
            {
                new WoundLineageSeedEntry(priorId, "definition_t070_recovery_characteristic", null, "removed")
            })
        };
        var fault = new ResourcePublicationFailureInjection();
        using var fixture = AcceptedStateFixture.Create(scenario, new FileSystemManagerHooks
        { BeforeCanonicalMutationAsync = fault.BeforeCanonicalMutationAsync });
        var prior = Assert.Single(fixture.ReadEffectIdentityIndex()["entries"]!.AsArray())!.DeepClone();
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId), "plain terminal-root reduction");
        var plan = ComposeCoordinatedTreatmentPlan(fixture, flow);
        var batch = Assert.Single(plan.WoundStageBundle!.PreparedPlan.EffectOperationBatches);
        Assert.Empty(batch.TerminalOperations);
        Assert.Equal(priorId, Assert.Single(batch.RootApplications).PriorRootEffectId);
        var tree = CaptureResolverFixtureTree(fixture.Root);
        fault.Arm(WoundHistoryState.HistoryPath, fixture.TargetCarrierPath,
            ReadCanonicalBytes(fixture, fixture.TargetCarrierPath), fixture.FileSystem);
        Assert.Throws<CanonicalStateWriteException>(() => PublishCachedResourcePlanOpen(fixture, flow, plan));
        Assert.True(fault.Fired);
        Assert.True(fault.ObservedEarlierResourceWrite);
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan)) publication.CompleteAtFullPipelineEnd();
        var wound = fixture.ReadCurrentWound();
        Assert.Equal(2, wound.Severity.Rank);
        var freshId = Assert.Single(wound.Consequences.OwnedEffectSources.RootBindings).EffectId;
        Assert.NotEqual(priorId, freshId);
        var entries = fixture.ReadEffectIdentityIndex()["entries"]!.AsArray();
        Assert.True(JsonNode.DeepEquals(prior, entries.Single(row => row!["effectId"]!.GetValue<string>() == priorId)));
        Assert.Equal(priorId, Assert.Single(entries.Single(row => row!["effectId"]!.GetValue<string>() == freshId)!
            ["transitions"]![0]!["sourceEffectIds"]!.AsArray())!.GetValue<string>());
        Assert.Equal(1, fixture.ReadNpcItemCount("sterile_thread"));
        AssertClosedTreatmentReceipt(Assert.Single(fixture.ReadCurrentHistory().State!.Transitions,
            row => row.Kind == "treat").TreatmentResult!.Receipt, flow.Request, flow.Resolution);
        fixture.RestartForReplay();
        var published = CaptureResolverFixtureTree(fixture.Root);
        Assert.Equal("ExactReplay", Convert.ToString(ReadRequiredProperty(ProbePublishedTreatment(fixture, flow.Request), "Status")));
        AssertResolverFixtureTreeUnchanged(fixture.Root, published);
    }

    private static ResolverScenario CreateRemovalModeScenario(string mode, string category, string shape)
    {
        var scenario = mode == "course" ? CreateComplicationRemovalPublicationScenario() :
            CreateOrderedReductionScenario(mode, category, "r1", shape.Split(',').Length);
        if (mode != "course")
        {
            var source = CreateComplicationRemovalPublicationScenario().Before;
            scenario.Before["consequences"] = source["consequences"]!.DeepClone();
            scenario.Before["complications"] = source["complications"]!.DeepClone();
            // Keep the current wound's real source identity; only the local graph shape is reused.
            foreach (var definition in scenario.Before["consequences"]!["ownedEffectSources"]!["definitions"]!.AsArray())
                definition!["links"]![0]!["targetId"] = scenario.Before["woundId"]!.DeepClone();
            if (mode == "procedure" && category == "success")
                scenario.AcceptedState["acceptedDice"] = new JsonArray(19, 7);
        }
        foreach (var band in scenario.Before["treatment"]!["routes"]![0]!["outcomes"]!.AsArray())
            band!["result"] = RemovalOperations(shape);
        return scenario with { History = CreateCurrentWoundHistory(scenario.Before), ExpectedIntentCount = shape.Split(',').Length };
    }

    [Theory]
    [InlineData("procedure", "success", "m,a", 0)]
    [InlineData("procedure", "partial_success", "m,s", 0)]
    [InlineData("procedure", "failed_attempt", "m", 0)]
    [InlineData("guaranteed", "success", "m", 0)]
    [InlineData("course", "success", "m,s", 0)]
    [InlineData("procedure", "success", "a,m,r2", 0)]
    [InlineData("procedure", "failed_attempt", "m", 1)]
    [InlineData("procedure", "success", "m", 20)]
    public void ComplicationRemovalPublication_AllSupportedModesKeepResourceAndCompletionRules(string mode, string category, string shape, int naturalRoll)
    {
        {
            var scenario = CreateRemovalModeScenario(mode, category, shape);
            if (naturalRoll != 0)
            {
                scenario.AcceptedState["acceptedDice"] = new JsonArray(naturalRoll, 7);
                scenario = scenario with { SeedFateEffectId = "effect_fate_shield_older" };
            }
            using var fixture = AcceptedStateFixture.Create(scenario);
            var flow = PersistAndRehydrateTreatmentPublication(fixture,
                ResolveCurrentTreatment(fixture, mode, scenario.OperationKey, scenario.RouteId), "selected removal mode");
            var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
            var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
            Assert.Equal(category, resolution.ResultCategory);
            if (mode == "guaranteed") Assert.IsType<MortalWoundTreatmentCapabilityProof>(request.ModeAuthority);
            if (mode == "procedure")
                Assert.Equal(category == "failed_attempt" ? new[] { 0, 1 } : new[] { 0 },
                    Assert.IsType<MortalWoundProcedureCheckAuthority>(request.ModeAuthority).SourceIndices);
            if (naturalRoll == 1)
            {
                Assert.NotNull(resolution.CriticalReactionIntent);
                Assert.Equal("effect_fate_shield_older", Assert.IsType<MortalWoundProcedureCheckAuthority>(request.ModeAuthority)
                    .PreparedCriticalReaction!.EffectId);
            }
            else Assert.Null(resolution.CriticalReactionIntent);
            if (mode == "guaranteed") ComposeAndPublishTreatment(fixture, flow);
            else ComposeAndPublishCoordinatedTreatment(fixture, flow);
            if (naturalRoll != 0)
                Assert.Equal(naturalRoll == 1 ? new[] { "effect_fate_shield_newer" } : new[] { "effect_fate_shield_older", "effect_fate_shield_newer" },
                    fixture.ReadActivePlayerEffectIds().Where(id => id.StartsWith("effect_fate_shield_", StringComparison.Ordinal)));
            var wound = fixture.ReadCurrentWound();
            Assert.Empty(wound.Complications);
            Assert.Equal(shape.Contains('a') ? 1L : 0L, wound.Recovery.CurrentStepProgress);
            Assert.Equal(shape.Contains('s') ? "stabilized" : "untreated", wound.Care.State);
            Assert.Equal(shape.Contains("r2", StringComparison.Ordinal) ? 1 : 3, wound.Severity.Rank);
            Assert.Equal(category == "success" ? new[] { scenario.RouteId } : Array.Empty<string>(), wound.Treatment.CompletedRouteIds);
            Assert.Equal(mode == "procedure" ? 1 : 2, fixture.ReadNpcItemCount("sterile_thread"));
            Assert.Equal(mode == "course" ? 7 : 8, fixture.ReadPlayerItemCount("antibiotic_dose"));
            Assert.Null(wound.Care.ActiveCourseId);
            AssertClosedTreatmentReceipt(Assert.Single(fixture.ReadCurrentHistory().State!.Transitions,
                row => row.Kind == "treat").TreatmentResult!.Receipt, flow.Request, flow.Resolution);
        }

        if (mode != "course") return;
        var active = CreateComplicationRemovalPublicationScenario();
        active.Before["treatment"]!["routes"]![0]!["outcomes"] = new JsonArray(
            CourseMilestone(1, 0, "active", RemovalOperations("m")),
            CourseMilestone(2, 480, "completed", RemovalOperations("s")));
        active.Before["treatment"]!["routes"]![0]!["resourcePolicy"]!["mutations"] = new JsonArray(CourseMutation(1), CourseMutation(2));
        active = active with { History = CreateCurrentWoundHistory(active.Before) };
        using (var fixture = AcceptedStateFixture.Create(active))
        {
            ComposeAndPublishTreatment(fixture, ResolveCurrentTreatment(fixture, "course", active.OperationKey, active.RouteId));
            Assert.NotNull(fixture.ReadCurrentWound().Care.ActiveCourseId);
            Assert.Empty(fixture.ReadCurrentWound().Treatment.CompletedRouteIds);
            Assert.Empty(fixture.ReadCurrentWound().Complications);
            fixture.PrepareNextTurn(43, 480, "removal_course_final");
            ComposeAndPublishTreatment(fixture, ResolveCurrentTreatment(fixture, "course", active.OperationKey + "_final", active.RouteId));
            Assert.Null(fixture.ReadCurrentWound().Care.ActiveCourseId);
            Assert.Equal(6, fixture.ReadPlayerItemCount("antibiotic_dose"));
        }

        var during = CreateComplicationRemovalPublicationScenario();
        during.Before["treatment"]!["routes"]![0]!["outcomes"] = new JsonArray(
            CourseMilestone(1, 0, "active", new JsonArray()),
            CourseMilestone(2, 480, "completed", RemovalOperations("s")));
        during.Before["treatment"]!["routes"]![0]!["resourcePolicy"]!["mutations"] = new JsonArray(CourseMutation(1), CourseMutation(2));
        var guarantee = StrictGuaranteedRoute();
        guarantee["outcomes"]![0]!["result"] = RemovalOperations("m");
        during.Before["treatment"]!["routes"]!.AsArray().Add(guarantee);
        during.Before["treatment"]!["knownRouteIds"]!.AsArray().Add("guaranteed_v1");
        during = during with { History = CreateCurrentWoundHistory(during.Before) };
        using (var fixture = AcceptedStateFixture.Create(during))
        {
            ComposeAndPublishTreatment(fixture, ResolveCurrentTreatment(fixture, "course", during.OperationKey, during.RouteId));
            var courseId = fixture.ReadCurrentWound().Care.ActiveCourseId;
            Assert.NotNull(courseId);
            fixture.PrepareNextTurn(43, 240, "removal_during_course");
            ComposeAndPublishTreatment(fixture, ResolveCurrentTreatment(fixture, "guaranteed", during.OperationKey + "_other", "guaranteed_v1"));
            Assert.Empty(fixture.ReadCurrentWound().Complications);
            Assert.Equal(courseId, fixture.ReadCurrentWound().Care.ActiveCourseId);
            Assert.Equal(7, fixture.ReadPlayerItemCount("antibiotic_dose"));
        }
    }

    [Fact]
    public void ComplicationRemovalPublication_MultipleComplicationsAndOrderingRemainExact()
    {
        foreach (var shape in new[] { "m", "n", "m,n", "n,m", "m,n,r2", "r2,m,n", "m,m", "missing" })
        {
            var scenario = CreateRemovalModeScenario("procedure", "success", shape);
            var first = scenario.Before["complications"]![0]!;
            first["ownedEffectIds"] = new JsonArray("effect_t066_course_complication_a");
            var second = first.DeepClone();
            second["complicationId"] = "complication_t070_second";
            second["ownedEffectIds"] = new JsonArray("effect_t066_course_complication_b");
            scenario.Before["complications"]!.AsArray().Add(second);
            foreach (var definition in scenario.Before["consequences"]!["ownedEffectSources"]!["definitions"]!.AsArray())
                definition!["triggers"] = new JsonArray();
            scenario = scenario with { History = CreateCurrentWoundHistory(scenario.Before) };
            using var fixture = AcceptedStateFixture.Create(scenario);
            if (shape is "r2,m,n" or "m,m" or "missing")
            {
                var state = (MortalWoundTreatmentAcceptedStateAuthority)fixture.GetAcceptedState();
                var tree = CaptureResolverFixtureTree(fixture.Root);
                var rejected = MortalWoundTreatmentPlanner.PrepareProcedureRequest(state, fixture.ReadCurrentHistory(),
                    fixture.ReadCurrentWound(), scenario.OperationKey, scenario.RouteId, fixture.AcceptedEventRef(state));
                Assert.False(rejected.IsValid);
                Assert.Null(rejected.Request);
                AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
                continue;
            }
            var beforeCarrier = fixture.ReadPlayerEffectCarrier();
            var beforeIndex = fixture.ReadEffectIdentityIndex();
            var flow = PersistAndRehydrateTreatmentPublication(fixture,
                ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId), "ordered exact removals");
            var resolution = (MortalWoundTreatmentResolution)flow.Resolution;
            Assert.Equal(shape.Split(',').Where(token => token is "m" or "n")
                .Select(token => token == "m" ? "complication_t066_course_pressure" : "complication_t070_second"),
                resolution.OutcomeIntents.OfType<MortalWoundRemoveComplicationOutcomeIntent>().Select(intent => intent.ComplicationId));
            ComposeAndPublishCoordinatedTreatment(fixture, flow);
            var wound = fixture.ReadCurrentWound();
            Assert.Equal(shape is "m" or "n" ? 1 : 0, wound.Complications.Count);
            if (shape is "m" or "n")
            {
                var retainedId = shape == "m" ? "effect_t066_course_complication_b" : "effect_t066_course_complication_a";
                Assert.Equal(retainedId, Assert.Single(Assert.Single(wound.Complications).OwnedEffectIds));
                Assert.Contains(wound.Consequences.OwnedEffectSources.RootBindings, row => row.EffectId == retainedId);
                Assert.True(JsonNode.DeepEquals(
                    beforeCarrier["activeEffects"]!.AsArray().Single(row => row!["effectId"]!.GetValue<string>() == retainedId),
                    fixture.ReadPlayerEffectCarrier()["activeEffects"]!.AsArray().Single(row => row!["effectId"]!.GetValue<string>() == retainedId)));
                Assert.True(JsonNode.DeepEquals(
                    beforeIndex["entries"]!.AsArray().Single(row => row!["effectId"]!.GetValue<string>() == retainedId),
                    fixture.ReadEffectIdentityIndex()["entries"]!.AsArray().Single(row => row!["effectId"]!.GetValue<string>() == retainedId)));
                Assert.Equal(beforeIndex["entries"]!.AsArray().Count, fixture.ReadEffectIdentityIndex()["entries"]!.AsArray().Count);
            }
        }
    }

    [Fact]
    public void ComplicationRemovalPublication_ChangedProjectionOrBatchCannotBorrowAcceptedAuthority()
    {
        var scenario = CreateRemovalModeScenario("course", "success", "m,a");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "course", scenario.OperationKey, scenario.RouteId), "removal tamper proof");
        var plan = ComposeCoordinatedTreatmentPlan(fixture, flow);
        var bundle = plan.WoundStageBundle!;
        var prepared = bundle.PreparedPlan;
        var batch = Assert.Single(prepared.EffectOperationBatches);
        Assert.True(WoundAcceptedTurnPlanner.TryReadTreatmentContinuation(prepared.TreatmentContinuationAuthority!, out var continuation));
        var preparation = continuation.OutcomePreparation;
        var resolution = (MortalWoundTreatmentResolution)flow.Resolution;
        var request = (MortalWoundTreatmentAttemptRequest)flow.Request;
        var accepted = (MortalWoundTreatmentAcceptedStateAuthority)flow.AcceptedState;
        Assert.True(MortalWoundTreatmentSeverityRematerializationPlanner.Agrees(bundle.Input,
            preparation.DetachedCopy(), request.RequestFingerprint, resolution.ResolutionAuthorityFingerprint,
            resolution.ResultFingerprint, request.Coordinates.AttemptId, request.Coordinates.OperationKey,
            request.Coordinates.ExpectedBeforeFingerprint, WoundAcceptedTurnData.CloneOperationBatch(batch),
            continuation.RematerializationAuthority! with { }));
        Assert.True(MortalWoundTreatmentOutcomePublicationPlanner.Finalize(preparation.DetachedCopy(), resolution,
            batch, new Dictionary<string, EffectAcceptedApplicationResult>()).IsValid);
        var tree = CaptureResolverFixtureTree(fixture.Root);
        var removal = Assert.IsType<MortalWoundRemoveComplicationOutcomeIntent>(resolution.OutcomeIntents[0]);
        foreach (var axis in new[] { "id", "ordinal", "kind", "order", "provisional_graph" })
        {
            var detached = preparation.DetachedCopy();
            var changed = resolution;
            if (axis == "provisional_graph")
            {
                var provisional = (WoundMaterializationEnvelope)typeof(MortalWoundTreatmentOutcomePreparation)
                    .GetField("_provisionalAfter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(detached)!;
                SetCourseTestBackingField(provisional.Consequences, "SlotsUsed", 2);
            }
            else
            {
                var intents = resolution.OutcomeIntents.ToArray();
                intents[0] = axis == "kind" ? MortalWoundStabilizeOutcomeIntent.Create(0,
                    removal.DeclaredOperationFingerprint, removal.IntentFingerprint) :
                    MortalWoundRemoveComplicationOutcomeIntent.Create(axis == "ordinal" ? 1 : 0,
                        removal.DeclaredOperationFingerprint, removal.IntentFingerprint,
                        axis == "id" ? "complication_missing" : removal.ComplicationId);
                if (axis == "order") Array.Reverse(intents);
                changed = axis == "id" ? ResealRecoveryResolution(resolution, request, intents) :
                    CloneResolutionWithIntentsUnchecked(resolution, intents);
                var rejected = MortalWoundTreatmentOutcomePublicationPlanner.Prepare(accepted, request, changed, accepted.CurrentGameMinute);
                Assert.False(rejected.IsValid, axis);
                Assert.Null(rejected.Preparation);
            }
            var final = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(detached, changed, batch,
                new Dictionary<string, EffectAcceptedApplicationResult>());
            Assert.False(final.IsValid, axis);
            Assert.Null(final.After);
            AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        }
        foreach (var axis in new[] { "source_definition", "causal_event", "missing_terminal", "extra_terminal", "reordered_terminal", "foreign_terminal", "retained_ownership" })
        {
            var export = batch.SourceExport;
            var definitions = export.Definitions.ToArray();
            if (axis == "source_definition")
            {
                var definition = definitions[0].Definition;
                definition["display"]!["description"] = "changed";
                definitions[0] = new WoundEffectSourceDefinition(definitions[0].DefinitionKey, definition);
            }
            export = new WoundEffectSourceExport(export.SchemaVersion, export.Kind, export.SourceId, export.SourceRef,
                export.State, export.Materializable, export.Realm, export.Owner,
                axis == "causal_event" ? preparation.Before.Severity.LastChangeEventRef : export.CausalEventRef,
                export.EventSemanticFingerprint, export.OpportunityId, export.OpportunityAuthorityFingerprint, definitions);
            var terminals = batch.TerminalOperations.ToArray();
            if (axis == "missing_terminal") terminals = terminals.Skip(1).ToArray();
            if (axis == "extra_terminal") terminals = terminals.Append(terminals[0]).ToArray();
            if (axis == "reordered_terminal") Array.Reverse(terminals);
            if (axis == "foreign_terminal") SetCourseTestBackingField(terminals[0], "EffectId", "effect_foreign_terminal");
            var lineage = batch.RootLineageAuthority.ToArray();
            if (axis == "retained_ownership") lineage[0] = lineage[0] with { OwnershipDomain = WoundRootOwnershipDomain.ForComplication("foreign_complication") };
            var changedBatch = ResealRemovalBatch(batch, lineage, terminals, export);
            Assert.False(MortalWoundTreatmentSeverityRematerializationPlanner.Agrees(bundle.Input, preparation,
                request.RequestFingerprint, resolution.ResolutionAuthorityFingerprint, resolution.ResultFingerprint,
                request.Coordinates.AttemptId, request.Coordinates.OperationKey, request.Coordinates.ExpectedBeforeFingerprint,
                changedBatch, continuation.RematerializationAuthority!), axis);
            var forged = ResealRemovalPrepared(prepared, new[] { changedBatch }, prepared.TreatmentContinuationAuthority!);
            Assert.False(WoundAcceptedTurnPlanCache.ValidatePreparedResult(bundle.Input,
                WoundAcceptedTurnFingerprints.ComputeInput(bundle.Input),
                new WoundAcceptedTurnPreparationResult(forged, Array.Empty<ValidationIssue>())).Success, axis);
            AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        }
        Assert.False(MortalWoundTreatmentSeverityRematerializationPlanner.Agrees(bundle.Input, preparation,
            request.RequestFingerprint, resolution.ResolutionAuthorityFingerprint, resolution.ResultFingerprint,
            request.Coordinates.AttemptId, request.Coordinates.OperationKey, request.Coordinates.ExpectedBeforeFingerprint,
            batch, continuation.RematerializationAuthority! with { ProjectionFingerprint = "foreign_projection" }));

        var mixedScenario = CreateRemovalModeScenario("course", "success", "m,r2");
        using var mixedFixture = AcceptedStateFixture.Create(mixedScenario);
        var mixedFlow = PersistAndRehydrateTreatmentPublication(mixedFixture,
            ResolveCurrentTreatment(mixedFixture, "course", mixedScenario.OperationKey, mixedScenario.RouteId), "mixed predecessor tamper");
        var mixedBundle = ComposeCoordinatedTreatmentPlan(mixedFixture, mixedFlow).WoundStageBundle!;
        var mixedBatch = Assert.Single(mixedBundle.PreparedPlan.EffectOperationBatches);
        Assert.True(WoundAcceptedTurnPlanner.TryReadTreatmentContinuation(mixedBundle.PreparedPlan.TreatmentContinuationAuthority!, out var mixedContinuation));
        var mixedTree = CaptureResolverFixtureTree(mixedFixture.Root);
        foreach (var axis in new[] { "missing", "extra", "removed_predecessor", "foreign_predecessor" })
        {
            var applications = mixedBatch.RootApplications.ToArray();
            if (axis == "missing") applications = Array.Empty<WoundRootEffectApplication>();
            if (axis == "extra") applications = applications.Append(applications[0]).ToArray();
            if (axis.EndsWith("predecessor", StringComparison.Ordinal))
                SetCourseTestBackingField(applications[0], "PriorRootEffectId",
                    axis == "removed_predecessor" ? "effect_t066_course_complication_a" : "effect_foreign_predecessor");
            var forged = ResealRemovalPrepared(mixedBundle.PreparedPlan,
                new[] { ResealRemovalBatch(mixedBatch, applications: applications) }, mixedBundle.PreparedPlan.TreatmentContinuationAuthority!);
            Assert.False(WoundAcceptedTurnPlanCache.ValidatePreparedResult(mixedBundle.Input,
                WoundAcceptedTurnFingerprints.ComputeInput(mixedBundle.Input),
                new WoundAcceptedTurnPreparationResult(forged, Array.Empty<ValidationIssue>())).Success, axis);
            AssertResolverFixtureTreeUnchanged(mixedFixture.Root, mixedTree);
        }
        var unexpectedApplication = ResealRemovalBatch(batch, applications: mixedBatch.RootApplications);
        Assert.False(MortalWoundTreatmentOutcomePublicationPlanner.Finalize(preparation, resolution, unexpectedApplication,
            new Dictionary<string, EffectAcceptedApplicationResult>()).IsValid);
        Assert.False(MortalWoundTreatmentSeverityRematerializationPlanner.Agrees(bundle.Input,
            mixedContinuation.OutcomePreparation, request.RequestFingerprint, resolution.ResolutionAuthorityFingerprint,
            resolution.ResultFingerprint, request.Coordinates.AttemptId, request.Coordinates.OperationKey,
            request.Coordinates.ExpectedBeforeFingerprint, batch, mixedContinuation.RematerializationAuthority!));
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
    }
}
