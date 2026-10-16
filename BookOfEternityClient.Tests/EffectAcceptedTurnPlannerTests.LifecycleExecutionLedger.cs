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
        Assert.True(reduction.Kind == AcceptedMechanicsPlanner.AcceptedMechanicsReductionKind.CompletedOrdinaryReduction,
            reduction.Kind + ": " + DescribeOrdinaryReductionIssues(reduction.Issues));
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
        var owned = Assert.IsType<EffectLifecycleExecutionLedger>(ledger);
        Assert.True(owned.IsComplete);
        Assert.True(owned.MatchesInitialPlan(fixture.Input.PlanningContext!.EffectPlan!));
        Assert.Equal(0, fixture.Effects.EffectCalls);
        Assert.Equal(scenario == "advance" ? 2 : 0, fixture.Effects.TransitionCalls);
        if (scenario == "advance")
        {
            var newIds = completed.Effects.AllocatedTransitionIds.ToHashSet(StringComparer.Ordinal);
            var actual = completed.Effects.IdentityIndexAfterImage["entries"]!.AsArray()
                .SelectMany(entry => entry!["transitions"]!.AsArray())
                .Where(row => newIds.Contains(row!["transitionId"]!.GetValue<string>())).ToArray();
            Assert.Equal(new[] { "trigger", "consume" }, actual.Select(row => row!["kind"]!.GetValue<string>()));
            Assert.Equal(actual[1]!["transitionId"]!.GetValue<string>(),
                Assert.Single(Assert.Single(finalEvent["subjects"]!.AsArray())!["transitionRefs"]!.AsArray())!.GetValue<string>());
        }
    }

    [Theory]
    [InlineData("turn_expire")]
    [InlineData("causal_created")]
    [InlineData("uses_mode")]
    [InlineData("bound_active")]
    [InlineData("bound_expire")]
    [InlineData("until_time_null")]
    [InlineData("npc_advance")]
    [InlineData("ordered_events")]
    [InlineData("empty_source")]
    [InlineData("multi_target")]
    public void LifecycleLedger_RetainsActualRosterExclusionsAndPassChanges(string scenario)
    {
        var fixture = CreateOrdinaryReductionFixture(effects: true,
            configureEffectInput: input => ConfigureLifecycleLedgerScenario(input, scenario),
            includeConfiguredCarrierBeforeImages: scenario is "npc_advance" or "multi_target");
        var completed = ReduceLifecycleLedgerFixture(fixture);
        var initial = fixture.Input.PlanningContext!.EffectPlan!;
        var ledger = Assert.IsType<EffectLifecycleExecutionLedger>(completed.Effects!.LifecycleExecutionLedger);
        Assert.True(ledger.IsComplete);
        Assert.True(ledger.MatchesInitialPlan(initial));
        var image = ledger.ToJsonObject();
        var passes = image["passes"]!.AsArray();
        Assert.Equal(2, passes.Count);
        var source = initial.EventInput["lifecycleEvents"]!.AsArray();
        for (var passIndex = 0; passIndex < 2; passIndex++)
        {
            var pass = passes[passIndex]!;
            Assert.Equal(passIndex == 0 ? "bound_continuations" : "final_lifetime", pass["kind"]!.GetValue<string>());
            var rows = pass["events"]!.AsArray();
            Assert.Equal(source.Count, pass["sourceEventCount"]!.GetValue<int>());
            Assert.Equal(source.Count, rows.Count);
            for (var ordinal = 0; ordinal < source.Count; ordinal++)
            {
                Assert.Equal(ordinal, rows[ordinal]!["inputOrdinal"]!.GetValue<int>());
                Assert.Equal(source[ordinal]!["eventRef"]!.GetValue<string>(), rows[ordinal]!["sourceEventRef"]!.GetValue<string>());
                Assert.Equal(source[ordinal]!["target"]!.ToJsonString(), rows[ordinal]!["parsedAuthority"]!["target"]!.ToJsonString());
                Assert.NotEmpty(rows[ordinal]!["rosterRef"]!.GetValue<string>());
            }
        }
        if (scenario == "empty_source")
        {
            Assert.All(passes, pass => Assert.Empty(pass!["events"]!.AsArray()));
            Assert.All(passes, pass => Assert.Null(pass!["initialRoster"]));
            Assert.All(passes, pass => Assert.Null(pass!["initialRosterRef"]));
            Assert.Equal(0, fixture.Effects.TransitionCalls);
            return;
        }
        var bound = passes[0]!["events"]![0]!;
        var final = passes[1]!["events"]![0]!;
        var boundSubject = Assert.Single(bound["subjects"]!.AsArray())!;
        if (scenario == "bound_expire")
        {
            Assert.Equal("expire", boundSubject["outcome"]!.GetValue<string>());
            Assert.False(boundSubject["schedulerEvent"]!["sourceSatisfied"]!.GetValue<bool>());
            Assert.Empty(final["subjects"]!.AsArray());
            Assert.Empty(final["roster"]!.AsArray());
            Assert.Equal("no_eligible", final["disposition"]!.GetValue<string>());
        }
        else
        {
            var subject = Assert.Single(final["subjects"]!.AsArray())!;
            if (scenario is "causal_created" or "uses_mode")
            {
                Assert.Equal(scenario, subject["disposition"]!.GetValue<string>());
                Assert.Equal(scenario, boundSubject["disposition"]!.GetValue<string>());
                Assert.Equal("no_eligible", final["disposition"]!.GetValue<string>());
                Assert.Null(subject["schedulerEvent"]);
                Assert.Equal(0, fixture.Effects.TransitionCalls);
            }
            else if (scenario == "bound_active")
            {
                Assert.Equal("other_pass", subject["disposition"]!.GetValue<string>());
                Assert.Equal("no_change", boundSubject["outcome"]!.GetValue<string>());
                Assert.True(boundSubject["schedulerEvent"]!["sourceSatisfied"]!.GetValue<bool>());
                Assert.Equal("source_catalog", boundSubject["resolvedSourceWitness"]!["kind"]!.GetValue<string>());
                Assert.Equal(initial.SourceAuthority.CanonicalFingerprint,
                    boundSubject["resolvedSourceWitness"]!["catalogFingerprint"]!.GetValue<string>());
            }
            else
            {
                Assert.Equal(scenario == "turn_expire" ? "expire" : scenario == "until_time_null" ? "no_change" : "advance",
                    subject["outcome"]!.GetValue<string>());
                if (scenario == "until_time_null")
                {
                    Assert.Null(subject["schedulerEvent"]!["currentTime"]);
                    Assert.Empty(subject["transitionRefs"]!.AsArray());
                    Assert.Equal(50L, subject["afterImage"]!["lifetime"]!["deadline"]!.GetValue<long>());
                }
                if (scenario == "npc_advance")
                {
                    Assert.Equal("npc", subject["coordinate"]!["kind"]!.GetValue<string>());
                    Assert.Equal(EffectCarrierCatalog.NpcPath, subject["filePath"]!.GetValue<string>());
                    Assert.Equal(2, subject["afterImage"]!["lifetime"]!["remainingTurns"]!.GetValue<int>());
                }
                if (scenario == "turn_expire")
                {
                    Assert.Null(subject["afterImage"]);
                    Assert.Single(subject["transitionRefs"]!.AsArray());
                }
                if (scenario == "ordered_events")
                {
                    var second = Assert.Single(passes[1]!["events"]![1]!["subjects"]!.AsArray())!;
                    Assert.Equal("no_change", second["outcome"]!.GetValue<string>());
                    Assert.Equal(3, subject["beforeImage"]!["lifetime"]!["remainingTurns"]!.GetValue<int>());
                    Assert.Equal(2, second["beforeImage"]!["lifetime"]!["remainingTurns"]!.GetValue<int>());
                    Assert.NotEqual(final["rosterRef"]!.GetValue<string>(), passes[1]!["events"]![1]!["rosterRef"]!.GetValue<string>());
                }
                if (scenario == "multi_target")
                {
                    foreach (var pass in passes)
                    {
                        var full = pass!["initialRoster"]!.AsArray();
                        Assert.Equal(2, full.Count);
                        Assert.Equal(new[] { "npc", "player" }, full.Select(row =>
                            row!["coordinate"]!["kind"]!.GetValue<string>()).OrderBy(value => value, StringComparer.Ordinal));
                        Assert.NotEmpty(pass["initialRosterRef"]!.GetValue<string>());
                        foreach (var row in pass["events"]!.AsArray())
                        {
                            var match = Assert.Single(row!["roster"]!.AsArray())!;
                            var actual = Assert.Single(row["subjects"]!.AsArray())!;
                            Assert.Equal(row["parsedAuthority"]!["target"]!["kind"]!.GetValue<string>(),
                                match["coordinate"]!["kind"]!.GetValue<string>());
                            Assert.Equal(match["effectId"]!.GetValue<string>(), actual["effectId"]!.GetValue<string>());
                        }
                    }
                    var second = Assert.Single(passes[1]!["events"]![1]!["subjects"]!.AsArray())!;
                    Assert.Equal("npc", second["coordinate"]!["kind"]!.GetValue<string>());
                    Assert.Equal("advance", second["outcome"]!.GetValue<string>());
                    Assert.Equal(3, second["beforeImage"]!["lifetime"]!["remainingTurns"]!.GetValue<int>());
                    Assert.Equal(2, second["afterImage"]!["lifetime"]!["remainingTurns"]!.GetValue<int>());
                }
            }
        }
        Assert.Equal(0, fixture.Effects.EffectCalls);
        if (scenario is "npc_advance" or "multi_target")
        {
            var actualBefore = fixture.Input.BeforeImages[EffectCarrierCatalog.NpcPath];
            Assert.True(actualBefore.Existed);
            Assert.Equal(initial.CarrierBeforeImages[EffectCarrierCatalog.NpcPath]!.ToJsonString(),
                System.Text.Encoding.UTF8.GetString(actualBefore.Bytes!));
        }
        Assert.Equal(scenario == "multi_target" ? 2 : scenario is "turn_expire" or "bound_expire" or "npc_advance" or "ordered_events" ? 1 : 0,
            fixture.Effects.TransitionCalls);
        var copy = EffectAcceptedTurnPlan.DetachedCopyOf(completed.Effects);
        Assert.Same(ledger, copy.LifecycleExecutionLedger);
        Assert.Equal(ledger.Fingerprint, copy.LifecycleExecutionLedger!.Fingerprint);
    }

    [Fact]
    public void LifecycleLedger_RejectsForeignIssuerAndIdenticalForeignExecution()
    {
        var fixture = CreateOrdinaryReductionFixture(effects: true);
        var completed = ReduceLifecycleLedgerFixture(fixture);
        var ledger = Assert.IsType<EffectLifecycleExecutionLedger>(completed.Effects!.LifecycleExecutionLedger);
        Assert.True(ledger.MatchesInitialPlan(fixture.Input.PlanningContext!.EffectPlan!));
        var foreign = CreateOrdinaryReductionFixture(effects: true);
        Assert.Equal(WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(fixture.Input.PlanningContext.EffectPlan!),
            WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(foreign.Input.PlanningContext!.EffectPlan!));
        Assert.False(ledger.MatchesInitialPlan(foreign.Input.PlanningContext.EffectPlan!));
        Assert.Throws<InvalidOperationException>(() => new EffectLifecycleExecutionLedger(new object(), ledger.ToJsonObject()));
        Assert.Throws<InvalidOperationException>(() => ledger.AppendFinalPass(new object(),
            ledger.ToJsonObject()["passes"]![1]!.AsObject()));
        Assert.Throws<InvalidDataException>(() => ledger.BindPlan(null, "foreign_payload"));
        var fingerprint = ledger.Fingerprint;
        ledger.ToJsonObject()["passes"]![0]!["events"]!.AsArray().Clear();
        Assert.Equal(fingerprint, ledger.Fingerprint);
    }

    [Fact]
    public void LifecycleLedger_MissingFirstPassIsNeverSynthesizedByCommonCompletion()
    {
        var fixture = CreateOrdinaryReductionFixture(effects: true, configureEffectPlan: plan =>
        {
            Assert.NotNull(plan.LifecycleExecutionLedger);
            var without = new EffectAcceptedTurnPlan(plan.InputFingerprint, plan.CarrierAuthorityFingerprint,
                plan.SourceAuthorityFingerprint, plan.TargetAuthorityFingerprint, plan.AllocatedCombatantIds,
                plan.AllocatedEffectIds, plan.AllocatedTransitionIds, plan.Sources, plan.Targets,
                plan.SourceBindings, plan.DeferredReactions, plan.ReactionExpansionCount, plan.ReactionExpansionUsage,
                plan.ActiveEffects, plan.ResourceTriggerCarriers, plan.SourceAuthority, plan.TargetAuthority,
                plan.EventInput, plan.CarrierBeforeImages, plan.CarrierAfterImages, plan.IdentityIndexBeforeImage,
                plan.IdentityIndexAfterImage, plan.TouchedPaths, plan.DeletedPaths,
                acceptedCarrierBaselines: plan.AcceptedCarrierBaselines,
                woundApplicationRootEffectBindings: plan.WoundApplicationRootEffectBindings,
                skillScopeAuthority: plan.SkillScopeAuthority);
            Assert.Equal(WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(plan),
                WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(without));
            return without;
        });
        Assert.Null(fixture.Input.PlanningContext!.EffectPlan!.LifecycleExecutionLedger);
        var completed = ReduceLifecycleLedgerFixture(fixture);
        Assert.True(completed.Effects!.IsAcceptedBoundaryComplete);
        Assert.Null(completed.Effects.LifecycleExecutionLedger);
    }

    private static AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction ReduceLifecycleLedgerFixture(
        OrdinaryReductionFixture fixture)
    {
        var reduction = AcceptedMechanicsPlanner.ReduceAcceptedPlan(fixture.Input,
            AcceptedMechanicsPlanFingerprints.ComputePlanningInput(fixture.Input));
        Assert.True(reduction.Kind == AcceptedMechanicsPlanner.AcceptedMechanicsReductionKind.CompletedOrdinaryReduction,
            reduction.Kind + ": " + DescribeOrdinaryReductionIssues(reduction.Issues));
        var completed = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(reduction.Completed);
        Assert.True(completed.Resources.IsValid);
        Assert.True(completed.Resources.EffectBoundaryTranscript!.IsComplete);
        var assembled = AcceptedMechanicsPlanner.CompleteAcceptedReduction(reduction);
        Assert.True(assembled.Success, DescribeOrdinaryReductionIssues(assembled.Issues));
        Assert.Same(completed.Effects, assembled.Plan!.EffectPlan);
        Assert.Equal(1, fixture.Outcome.ProjectCalls);
        return completed;
    }

    private static EffectAcceptedTurnInput ConfigureLifecycleLedgerScenario(EffectAcceptedTurnInput input, string scenario)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition("characteristic_modifier");
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(profile: "characteristic_modifier");
        definition["triggers"] = new JsonArray();
        effect["triggers"] = new JsonArray();
        var events = input.EventInput.DeepClone().AsObject();
        var source = events["lifecycleEvents"]![0]!;
        if (scenario == "turn_expire") effect["lifetime"]!["remainingTurns"] = 1;
        if (scenario == "causal_created") effect["chronology"]!["createdEventRef"] = source["causalEventRef"]!.DeepClone();
        if (scenario is "bound_active" or "bound_expire")
        {
            definition["lifetime"] = new JsonObject
            {
                ["mode"] = "source_bound", ["activePredicate"] = "active", ["onSourceLoss"] = "expire"
            };
            effect["lifetime"] = new JsonObject
            {
                ["mode"] = "source_bound", ["linkKind"] = "quest", ["targetId"] = "quest_effect_reaction_test",
                ["activePredicate"] = "active", ["onSourceLoss"] = "expire"
            };
            if (scenario == "bound_expire") source["sourceSatisfied"] = false;
        }
        if (scenario == "until_time_null")
        {
            definition["lifetime"] = new JsonObject
            {
                ["mode"] = "until_time", ["duration"] = 5,
                ["timeAuthority"] = EffectSourceDefinitionContract.CanonicalWorldTimeAuthority
            };
            effect["lifetime"] = new JsonObject { ["mode"] = "until_time", ["deadline"] = 50L };
        }
        if (scenario == "uses_mode")
        {
            definition["lifetime"] = new JsonObject
            {
                ["mode"] = "uses", ["initialUses"] = 3, ["consumingEventTypes"] = new JsonArray("owner_turn_end")
            };
            var trigger = EffectMaterializationTestFixture.CreateDefinition()["triggers"]![0]!.DeepClone();
            trigger["consumeUses"] = true;
            definition["triggers"]!.AsArray().Add(trigger.DeepClone());
            effect["triggers"]!.AsArray().Add(trigger);
            effect["lifetime"] = new JsonObject
            {
                ["mode"] = "uses", ["remainingUses"] = 3, ["consumingTriggerIds"] = new JsonArray("on_owner_turn_end")
            };
            // Exercise the lifetime exclusion without a matching consuming trigger.
            source["phase"] = "owner_damaged"; source["triggerId"] = "on_owner_damaged";
            source["eventRef"] = "turn_42:effect:on_owner_damaged:1";
            source["causalEventRef"] = "turn_42:owner_damaged:1";
        }
        if (scenario == "ordered_events")
        {
            var second = source.DeepClone();
            second["eventRef"] = "turn_42:aaa_damage:1";
            second["causalEventRef"] = "turn_42:owner_damaged:2";
            second["phase"] = "owner_damaged"; second["triggerId"] = "on_owner_damaged";
            events["lifecycleEvents"]!.AsArray().Add(second);
        }
        if (scenario == "empty_source") events["lifecycleEvents"] = new JsonArray();
        var rebuilt = CreateReactionInput(effect, definition) with { EventInput = events };
        if (scenario == "multi_target")
        {
            var npc = effect.DeepClone().AsObject();
            npc["effectId"] = "effect_test_npc_ledger";
            npc["target"]!["kind"] = "npc"; npc["target"]!["targetId"] = "npc_test_healer";
            npc["chronology"]!["lastTransitionId"] = "effect_transition_npc_ledger_apply";
            npc["chronology"]!["createdEventRef"] = "turn_42:npc_ledger:created";
            var npcEvent = source.DeepClone();
            npcEvent["eventRef"] = "turn_42:effect:npc_turn_end:1";
            npcEvent["causalEventRef"] = "turn_42:npc_turn_end:1";
            npcEvent["effectId"] = npc["effectId"]!.DeepClone();
            npcEvent["target"] = npc["target"]!.DeepClone();
            events["lifecycleEvents"]!.AsArray().Add(npcEvent);
            var identity = EffectMaterializationTestFixture.CreateIdentityIndex(effect, npc);
            var npcHistory = identity["entries"]![1]!["transitions"]![0]!;
            npcHistory["transitionId"] = npc["chronology"]!["lastTransitionId"]!.DeepClone();
            npcHistory["eventRef"] = npc["chronology"]!["createdEventRef"]!.DeepClone();
            return rebuilt with
            {
                TargetAuthority = EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
                    new[] { new EffectTargetExport("mortal_world", "player", "player_current", SameTurn: false),
                        new EffectTargetExport("mortal_world", "npc", "npc_test_healer", SameTurn: false) },
                    Array.Empty<EffectTargetExport>(), new HashSet<string>(StringComparer.Ordinal), CombatantIdentities: null)),
                PreTurnCarriers = rebuilt.PreTurnCarriers! with { NpcEffects = new JsonObject
                {
                    ["schemaVersion"] = 1, ["entries"] = new JsonArray(new JsonObject
                    {
                        ["NPCId"] = "npc_test_healer", ["activeEffects"] = new JsonArray(npc.DeepClone())
                    })
                } },
                PreTurnIdentityIndex = identity
            };
        }
        if (scenario != "npc_advance") return rebuilt;
        effect["target"]!["kind"] = "npc"; effect["target"]!["targetId"] = "npc_test_healer";
        source["target"] = effect["target"]!.DeepClone();
        return rebuilt with
        {
            TargetAuthority = EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
                new[] { new EffectTargetExport("mortal_world", "npc", "npc_test_healer", SameTurn: false) },
                Array.Empty<EffectTargetExport>(), new HashSet<string>(StringComparer.Ordinal), CombatantIdentities: null)),
            PreTurnCarriers = new EffectCarrierCatalogInput(null, new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = "npc_test_healer", ["activeEffects"] = new JsonArray(effect.DeepClone())
                })
            }, null, null, null, null),
            PreTurnIdentityIndex = EffectMaterializationTestFixture.CreateIdentityIndex(effect)
        };
    }
}
