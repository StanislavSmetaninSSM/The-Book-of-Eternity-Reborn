using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectResourceTriggerRoutingScaleTests
{
    private const string FingerprintA =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string FingerprintB =
        "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    public static TheoryData<bool, string, bool, string>
        ReactionUseProjectionMatrix
    {
        get
        {
            var data = new TheoryData<bool, string, bool, string>();
            foreach (var consumesUse in new[] { false, true })
            foreach (var dependency in new[]
                     {
                         "before_current_event",
                         "after_component",
                         "after_current_event"
                     })
            foreach (var lastUse in new[] { false, true })
            foreach (var resultKind in new[] { "event_outcome", "suspend" })
                data.Add(consumesUse, dependency, lastUse, resultKind);
            return data;
        }
    }

    [Fact]
    public void Planner_IncrementalEffectSourceRoutingScalesWithResolvedCandidates()
    {
        var one = PlanExpandingTriggers(128);
        var two = PlanExpandingTriggers(256);

        Assert.True(one.IsValid, Format(one.Issues));
        Assert.True(two.IsValid, Format(two.Issues));
        Assert.Equal(256, one.Statistics.MutationDescriptorCount);
        Assert.Equal(512, two.Statistics.MutationDescriptorCount);
        Assert.Equal(1, one.Statistics.SourceAuthoritySeedCount);
        Assert.Equal(1, two.Statistics.SourceAuthoritySeedCount);
        Assert.Equal(128, one.Statistics.SourceAuthorityAddVisitCount);
        Assert.Equal(256, two.Statistics.SourceAuthorityAddVisitCount);
        Assert.Equal(128, one.Statistics.SourceAuthorityResolveLookupCount);
        Assert.Equal(256, two.Statistics.SourceAuthorityResolveLookupCount);
        Assert.Equal(1, one.Statistics.SourceAuthorityFreezeCount);
        Assert.Equal(1, two.Statistics.SourceAuthorityFreezeCount);
        Assert.Equal(512, one.Statistics.EffectTriggerIndexLookupCount);
        Assert.Equal(1_024, two.Statistics.EffectTriggerIndexLookupCount);
        Assert.Equal(128, one.Statistics.EffectTriggerCandidateVisitCount);
        Assert.Equal(256, two.Statistics.EffectTriggerCandidateVisitCount);
        Assert.True(
            two.Statistics.TotalWorkUnits <= one.Statistics.TotalWorkUnits * 2.5,
            $"Expected near-linear real trigger/source-routing work, but " +
            $"{one.Statistics.TotalWorkUnits} units became " +
            $"{two.Statistics.TotalWorkUnits}.");
    }

    [Fact]
    public void Planner_RealEffectPlanIndexesOwnerBoundTriggersWithLinearWork()
    {
        var one = PlanRealEffectTriggers(32);
        var two = PlanRealEffectTriggers(64);

        Assert.True(one.Result.IsValid, Format(one.Result.Issues));
        Assert.True(two.Result.IsValid, Format(two.Result.Issues));
        Assert.Equal(1, one.Plan.ResourceTriggerIndex.Statistics.CatalogBuildCount);
        Assert.Equal(1, two.Plan.ResourceTriggerIndex.Statistics.CatalogBuildCount);
        Assert.Equal(32, one.Plan.ResourceTriggerIndex.Statistics.OccurrenceVisitCount);
        Assert.Equal(64, two.Plan.ResourceTriggerIndex.Statistics.OccurrenceVisitCount);
        Assert.Equal(32, one.Plan.ResourceTriggerIndex.Statistics.TriggerDescriptorVisitCount);
        Assert.Equal(64, two.Plan.ResourceTriggerIndex.Statistics.TriggerDescriptorVisitCount);
        Assert.Equal(128, one.Result.Statistics.EffectTriggerIndexLookupCount);
        Assert.Equal(256, two.Result.Statistics.EffectTriggerIndexLookupCount);
        Assert.Equal(32, one.Result.Statistics.EffectTriggerCandidateVisitCount);
        Assert.Equal(64, two.Result.Statistics.EffectTriggerCandidateVisitCount);
        Assert.Equal(32, one.Result.Statistics.EffectSourceBindingIndexLookupCount);
        Assert.Equal(64, two.Result.Statistics.EffectSourceBindingIndexLookupCount);
        Assert.Equal(32, one.Result.Statistics.EffectSourceBindingCandidateVisitCount);
        Assert.Equal(64, two.Result.Statistics.EffectSourceBindingCandidateVisitCount);
        Assert.Equal(32, one.Result.Statistics.SourceAuthorityAddVisitCount);
        Assert.Equal(64, two.Result.Statistics.SourceAuthorityAddVisitCount);
        Assert.Equal(1, one.Result.Statistics.SourceAuthorityFreezeCount);
        Assert.Equal(1, two.Result.Statistics.SourceAuthorityFreezeCount);
        Assert.Equal(64, one.Result.Statistics.GraphNodeDescriptorCount);
        Assert.Equal(128, two.Result.Statistics.GraphNodeDescriptorCount);
        var oneTotalWork = one.Result.Statistics.TotalWorkUnits +
            one.Plan.ResourceTriggerIndex.Statistics.TotalWorkUnits;
        var twoTotalWork = two.Result.Statistics.TotalWorkUnits +
            two.Plan.ResourceTriggerIndex.Statistics.TotalWorkUnits;
        Assert.True(
            twoTotalWork <= oneTotalWork * 2.5,
            $"Expected near-linear indexed EffectAcceptedTurnPlan work, but " +
            $"{oneTotalWork} units became {twoTotalWork}.");
    }

    [Fact]
    public void Planner_ExactLifecycleRoutingScalesWithExactEffectsAndIsDeterministic()
    {
        var one = ResolveExactLifecycleTriggers(32, reverseCarriers: false);
        var two = ResolveExactLifecycleTriggers(64, reverseCarriers: false);
        var reversed = ResolveExactLifecycleTriggers(64, reverseCarriers: true);

        Assert.True(one.Resolution.IsValid, Format(one.Resolution.Issues));
        Assert.True(two.Resolution.IsValid, Format(two.Resolution.Issues));
        Assert.True(reversed.Resolution.IsValid, Format(reversed.Resolution.Issues));
        Assert.Equal(32, one.Resolution.Mutations.Count);
        Assert.Equal(64, two.Resolution.Mutations.Count);
        Assert.Equal(32, one.Resolution.Work.IndexLookupCount);
        Assert.Equal(64, two.Resolution.Work.IndexLookupCount);
        Assert.Equal(32, one.Resolution.Work.CandidateVisitCount);
        Assert.Equal(64, two.Resolution.Work.CandidateVisitCount);
        Assert.Equal(
            DescribeLifecycleResolution(two.Resolution),
            DescribeLifecycleResolution(reversed.Resolution));
    }

    [Fact]
    public void Plan_ResourceTriggerIndexReturnsReadOnlyDeepClonedOccurrences()
    {
        var routed = ResolveExactLifecycleTriggers(2, reverseCarriers: false);
        var target = new EffectTargetKey(
            "mortal_world",
            "npc",
            "npc_exact_lifecycle_scale");
        var candidates = routed.Plan.ResourceTriggerIndex.ResolveLifecycleEvent(
            target,
            "owner_turn_end");
        var exactCandidates = routed.Plan.ResourceTriggerIndex.ResolveLifecycleEvent(
            target,
            "owner_turn_end",
            "effect_exact_lifecycle_0000");

        var first = Assert.Single(candidates, candidate =>
            string.Equals(
                candidate.Occurrence.EffectId,
                "effect_exact_lifecycle_0000",
                StringComparison.Ordinal));
        Assert.Equal(
            candidates
                .Where(candidate => string.Equals(
                    candidate.EffectId,
                    "effect_exact_lifecycle_0000",
                    StringComparison.Ordinal))
                .Select(static candidate => candidate.TriggerId),
            exactCandidates.Select(static candidate => candidate.TriggerId));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<EffectAcceptedTurnPlanner.IndexedResourceTrigger>)candidates)[0] = first);

        var callerOccurrence = first.Occurrence;
        callerOccurrence.Effect["state"] = "removed";
        callerOccurrence.Effect["effectId"] = "tampered_effect";
        callerOccurrence.Effect["target"]!["targetId"] = "tampered_target";

        var repeated = Assert.Single(
            routed.Plan.ResourceTriggerIndex.ResolveLifecycleEvent(
                target,
                "owner_turn_end"),
            candidate => string.Equals(
                candidate.Occurrence.EffectId,
                "effect_exact_lifecycle_0000",
                StringComparison.Ordinal));
        var repeatedOccurrence = repeated.Occurrence;
        Assert.Equal("active", repeatedOccurrence.Effect["state"]!.GetValue<string>());
        Assert.Equal(
            "effect_exact_lifecycle_0000",
            repeatedOccurrence.Effect["effectId"]!.GetValue<string>());
        Assert.Equal(
            "npc_exact_lifecycle_scale",
            repeatedOccurrence.Effect["target"]!["targetId"]!.GetValue<string>());

        var resourcePlan = PlanRealEffectTriggers(1).Plan;
        var coordinate = new ResourceCoordinate(
            "mortal_world",
            ResourceOwnerKind.Npc,
            "npc_effect_trigger_scale_0000",
            "health");
        var resourceCandidates = resourcePlan.ResourceTriggerIndex.ResolveResourceEvent(
            coordinate,
            "resource_damaged");
        var resourceCandidate = Assert.Single(resourceCandidates);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<EffectAcceptedTurnPlanner.IndexedResourceTrigger>)resourceCandidates)[0] =
                resourceCandidate);
        resourceCandidate.Occurrence.Effect["state"] = "removed";
        Assert.Equal(
            "active",
            Assert.Single(resourcePlan.ResourceTriggerIndex.ResolveResourceEvent(
                    coordinate,
                    "resource_damaged"))
                .Occurrence.Effect["state"]!.GetValue<string>());
    }

    [Fact]
    public void Plan_ResourceTriggerIndexBuildsOneOccurrenceSnapshotForManyTriggers()
    {
        var one = CreateManyTriggerIndex(32);
        var two = CreateManyTriggerIndex(64);

        Assert.Empty(one.Issues);
        Assert.Empty(two.Issues);
        Assert.Equal(1, one.Statistics.OccurrenceVisitCount);
        Assert.Equal(1, two.Statistics.OccurrenceVisitCount);
        Assert.Equal(1, one.Statistics.OccurrenceSnapshotBuildCount);
        Assert.Equal(1, two.Statistics.OccurrenceSnapshotBuildCount);
        Assert.Equal(32, one.Statistics.TriggerDescriptorVisitCount);
        Assert.Equal(64, two.Statistics.TriggerDescriptorVisitCount);
        Assert.Equal(35, one.Statistics.TotalWorkUnits);
        Assert.Equal(67, two.Statistics.TotalWorkUnits);
        Assert.True(
            two.Statistics.TotalWorkUnits <= one.Statistics.TotalWorkUnits * 2,
            $"Expected one shared occurrence snapshot and linear trigger work, but " +
            $"{one.Statistics.TotalWorkUnits} units became " +
            $"{two.Statistics.TotalWorkUnits}.");
    }

    [Fact]
    public void Plan_ReplacementTargetAmbiguityUsesMeteredLinearBucketWork()
    {
        var one = CreateAmbiguousReplacementTargetIndex(32);
        var two = CreateAmbiguousReplacementTargetIndex(64);

        Assert.Empty(one.Issues);
        Assert.Empty(two.Issues);
        Assert.Equal(32, one.Statistics.OccurrenceVisitCount);
        Assert.Equal(64, two.Statistics.OccurrenceVisitCount);
        Assert.Equal(32, one.Statistics.ReplacementTargetOccurrenceVisitCount);
        Assert.Equal(64, two.Statistics.ReplacementTargetOccurrenceVisitCount);
        Assert.Equal(128, one.Statistics.TotalWorkUnits);
        Assert.Equal(256, two.Statistics.TotalWorkUnits);
        Assert.Equal(
            one.Statistics.TotalWorkUnits * 2,
            two.Statistics.TotalWorkUnits);
    }

    [Fact]
    public void Planner_OneEffectManySameEventTriggersUseLinearExactRuntimeRouting()
    {
        var one = ResolveOneEffectManySameEventTriggers(
            triggerCount: 32,
            reverseTriggers: false,
            lifecycleTriggerId: null,
            remainingUses: 32,
            includeReaction: true);
        var two = ResolveOneEffectManySameEventTriggers(
            triggerCount: 64,
            reverseTriggers: false,
            lifecycleTriggerId: null,
            remainingUses: 64,
            includeReaction: true);
        var reversed = ResolveOneEffectManySameEventTriggers(
            triggerCount: 64,
            reverseTriggers: true,
            lifecycleTriggerId: null,
            remainingUses: 64,
            includeReaction: true);

        foreach (var result in new[] { one, two, reversed })
        {
            Assert.True(
                result.LifecycleResolution.IsValid,
                Format(result.LifecycleResolution.Issues));
            Assert.True(
                result.ResourceResolution.IsValid,
                Format(result.ResourceResolution.Issues));
            Assert.Equal(
                result.TriggerCount,
                result.LifecycleResolution.Mutations.Count);
            Assert.Equal(
                result.TriggerCount,
                result.ResourceResolution.Mutations.Count);
            Assert.Equal(
                result.TriggerCount,
                result.LifecycleResolution.TriggerExecutions.Count);
            Assert.Equal(
                result.TriggerCount,
                result.ResourceResolution.TriggerExecutions.Count);
            Assert.Equal(
                result.TriggerCount,
                result.LifecycleResolution.TriggerCandidates.Count);
            Assert.Equal(
                result.TriggerCount,
                result.ResourceResolution.TriggerCandidates.Count);
            Assert.All(
                result.ResourceResolution.TriggerCandidates
                    .SelectMany(static candidate => candidate.ReactionOutputs),
                static execution => Assert.Equal(
                    "apply_definition",
                    execution.ResultKind));
            var expectedBudgets = Enumerable.Repeat(
                    (int?)result.TriggerCount,
                    result.TriggerCount)
                .ToArray();
            Assert.Equal(
                expectedBudgets,
                result.LifecycleResolution.TriggerExecutions
                    .Select(static execution => execution.RemainingUseBudget));
            Assert.Equal(
                expectedBudgets,
                result.ResourceResolution.TriggerExecutions
                    .Select(static execution => execution.RemainingUseBudget));
            Assert.All(
                result.LifecycleResolution.TriggerExecutions
                    .Concat(result.ResourceResolution.TriggerExecutions),
                execution =>
                {
                    Assert.Equal(
                        new[] { "component_many_trigger_periodic" },
                        execution.ComponentIds);
                });
            Assert.All(
                result.LifecycleResolution.TriggerCandidates
                    .Concat(result.ResourceResolution.TriggerCandidates),
                candidate =>
                {
                    Assert.True(candidate.Activation.ConsumesUse);
                    Assert.Equal(
                        result.TriggerCount,
                        candidate.UseSeed?.RemainingUses);
                });
            Assert.Empty(result.LifecycleResolution.ReactionExecutions);
            Assert.Empty(result.ResourceResolution.ReactionExecutions);
        }

        AssertExactRuntimeWork(
            one.LifecycleResolution.Work,
            triggerCount: 32,
            sourceBindingBorrowsPerTrigger: 1,
            componentLookupsPerTrigger: 2,
            selectedComponentVisitsPerTrigger: 2);
        AssertExactRuntimeWork(
            two.LifecycleResolution.Work,
            triggerCount: 64,
            sourceBindingBorrowsPerTrigger: 1,
            componentLookupsPerTrigger: 2,
            selectedComponentVisitsPerTrigger: 2);
        AssertExactRuntimeWork(
            one.ResourceResolution.Work,
            triggerCount: 32,
            sourceBindingBorrowsPerTrigger: 2,
            componentLookupsPerTrigger: 4,
            selectedComponentVisitsPerTrigger: 4);
        AssertExactRuntimeWork(
            two.ResourceResolution.Work,
            triggerCount: 64,
            sourceBindingBorrowsPerTrigger: 2,
            componentLookupsPerTrigger: 4,
            selectedComponentVisitsPerTrigger: 4);
        Assert.True(
            two.LifecycleResolution.Work.TotalWorkUnits <=
            one.LifecycleResolution.Work.TotalWorkUnits * 2.5,
            $"Expected near-linear lifecycle routing work, but " +
            $"{one.LifecycleResolution.Work.TotalWorkUnits} units became " +
            $"{two.LifecycleResolution.Work.TotalWorkUnits}.");
        Assert.True(
            two.ResourceResolution.Work.TotalWorkUnits <=
            one.ResourceResolution.Work.TotalWorkUnits * 2.5,
            $"Expected near-linear resource-event routing work, but " +
            $"{one.ResourceResolution.Work.TotalWorkUnits} units became " +
            $"{two.ResourceResolution.Work.TotalWorkUnits}.");
        Assert.Equal(
            DescribeLifecycleResolution(two.LifecycleResolution),
            DescribeLifecycleResolution(reversed.LifecycleResolution));
        Assert.Equal(
            DescribeResourceResolution(two.ResourceResolution),
            DescribeResourceResolution(reversed.ResourceResolution));
    }

    [Fact]
    public void Planner_ExactLifecycleTriggerRoutesOnlyItsExactDescriptor()
    {
        var result = ResolveOneEffectManySameEventTriggers(
            triggerCount: 32,
            reverseTriggers: false,
            lifecycleTriggerId: "trigger_many_0000",
            remainingUses: 32,
            includeReaction: false);

        Assert.True(
            result.LifecycleResolution.IsValid,
            Format(result.LifecycleResolution.Issues));
        var execution = Assert.Single(result.LifecycleResolution.TriggerExecutions);
        Assert.Equal("trigger_many_0000", execution.TriggerId);
        Assert.Single(result.LifecycleResolution.Mutations);
        Assert.Equal(1, result.LifecycleResolution.Work.CandidateVisitCount);
    }

    [Fact]
    public void Planner_UseBudgetMaterializesAllCandidatesWithOneCanonicalSeed()
    {
        var result = ResolveOneEffectManySameEventTriggers(
            triggerCount: 32,
            reverseTriggers: false,
            lifecycleTriggerId: null,
            remainingUses: 7,
            includeReaction: true,
            reactionMaxExpansion: 32,
            reactionResultKind: "suspend");

        Assert.True(
            result.ResourceResolution.IsValid,
            Format(result.ResourceResolution.Issues));
        Assert.Equal(32, result.ResourceResolution.Mutations.Count);
        Assert.Equal(32, result.ResourceResolution.TriggerExecutions.Count);
        Assert.Equal(32, result.ResourceResolution.TriggerCandidates.Count);
        Assert.Empty(result.ResourceResolution.ReactionExecutions);
        var reactions = result.ResourceResolution.TriggerCandidates
            .SelectMany(static candidate => candidate.ReactionOutputs)
            .ToArray();
        Assert.Equal(32, reactions.Length);
        Assert.Equal(
            32,
            reactions
                .Select(static reaction => reaction.EventRef)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Equal(
            Enumerable.Repeat((int?)7, 32),
            result.ResourceResolution.TriggerExecutions
                .Select(static execution => execution.RemainingUseBudget));
        Assert.Equal(
            result.ResourceResolution.TriggerCandidates
                .Select(static candidate => candidate.Activation.Identity.TriggerId),
            reactions.Select(static reaction => reaction.TriggerId));
        Assert.All(
            result.ResourceResolution.TriggerCandidates,
            static candidate =>
            {
                Assert.True(candidate.Activation.ConsumesUse);
                Assert.Equal(7, candidate.UseSeed?.RemainingUses);
            });
    }

    [Fact]
    public void ResolverCandidate_ResourceEventMaterializesReactionOnlyAndMutationCandidatesWithoutSpendingUses()
    {
        var fixture = CreateAcceptedEventBudgetFixture(
            remainingUses: 1,
            current: 10m,
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec(
                    "trigger_candidate_10_reaction_only",
                    "resource_damaged",
                    Priority: 10,
                    IncludePeriodic: false,
                    IncludeReaction: true),
                new BudgetTriggerSpec(
                    "trigger_candidate_20_mutation",
                    "resource_damaged",
                    Priority: 20,
                    IncludePeriodic: true,
                    IncludeReaction: false)
            });
        var producer = CreateDamageProducer(
            fixture.Coordinate,
            "candidate_materialization");

        var resolution = EffectAcceptedTurnPlanner.ResolveResourceEventMutations(
            fixture.Plan,
            producer.Event,
            producer.Key,
            fixture.Owners,
            fixture.Definitions);

        Assert.True(resolution.IsValid, Format(resolution.Issues));
        Assert.Equal(2, resolution.TriggerCandidates.Count);
        var reactionOnly = resolution.TriggerCandidates.Single(candidate =>
            string.Equals(
                candidate.Activation.Identity.TriggerId,
                "trigger_candidate_10_reaction_only",
                StringComparison.Ordinal));
        var mutation = resolution.TriggerCandidates.Single(candidate =>
            string.Equals(
                candidate.Activation.Identity.TriggerId,
                "trigger_candidate_20_mutation",
                StringComparison.Ordinal));
        Assert.True(reactionOnly.Activation.ConsumesUse);
        Assert.Equal(10, reactionOnly.Activation.Priority);
        Assert.Equal(producer.Event.EventRef, reactionOnly.Activation.Identity.TriggerEventRef);
        Assert.NotEqual(
            producer.Event.EventRef,
            reactionOnly.Activation.Identity.EventRef);
        Assert.Equal(1, reactionOnly.UseSeed?.RemainingUses);
        Assert.Equal(producer.Key, reactionOnly.Producer);
        Assert.Empty(reactionOnly.PlannedMutationKeys);
        Assert.Empty(reactionOnly.PlannedComponentIds);
        Assert.Empty(reactionOnly.PendingOutputs);
        Assert.Single(reactionOnly.ReactionOutputs);
        Assert.True(mutation.Activation.ConsumesUse);
        Assert.Equal(20, mutation.Activation.Priority);
        Assert.Equal(1, mutation.UseSeed?.RemainingUses);
        Assert.Equal(producer.Key, mutation.Producer);
        Assert.Single(mutation.PlannedMutationKeys);
        Assert.Equal(
            new[] { "component_budget_periodic" },
            mutation.PlannedComponentIds);
        Assert.Single(mutation.PlannedComponentIdsByMutation);
        Assert.Empty(mutation.PendingOutputs);
        Assert.Empty(mutation.ReactionOutputs);
        Assert.Empty(resolution.PendingResolutions);
        Assert.Empty(resolution.ReactionExecutions);
        Assert.Equal(
            new int?[] { 1 },
            resolution.TriggerExecutions.Select(static execution =>
                execution.RemainingUseBudget));
    }

    [Fact]
    public void ResolverCandidate_NonConsumingUsesTriggerStillCarriesCanonicalSeed()
    {
        var fixture = CreateAcceptedEventBudgetFixture(
            remainingUses: 3,
            current: 10m,
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec(
                    "trigger_candidate_non_consuming",
                    "resource_damaged",
                    Priority: 10,
                    IncludePeriodic: true,
                    IncludeReaction: false,
                    ConsumesUse: false),
                new BudgetTriggerSpec(
                    "trigger_candidate_consuming_control",
                    "resource_spent",
                    Priority: 20,
                    IncludePeriodic: true,
                    IncludeReaction: false)
            });
        var producer = CreateDamageProducer(
            fixture.Coordinate,
            "candidate_non_consuming");

        var resolution = EffectAcceptedTurnPlanner.ResolveResourceEventMutations(
            fixture.Plan,
            producer.Event,
            producer.Key,
            fixture.Owners,
            fixture.Definitions);

        Assert.True(resolution.IsValid, Format(resolution.Issues));
        var candidate = Assert.Single(resolution.TriggerCandidates);
        Assert.False(candidate.Activation.ConsumesUse);
        Assert.Equal("effect_accepted_event_budget", candidate.UseSeed?.EffectId);
        Assert.Equal(3, candidate.UseSeed?.RemainingUses);
        Assert.Equal(producer.Key, candidate.Producer);
        var compatibilityExecution = Assert.Single(resolution.TriggerExecutions);
        Assert.Null(compatibilityExecution.RemainingUseBudget);
    }

    [Fact]
    public void ResolverCandidate_DueLifecycleReactionOnlyCarriesDeferredOutputAndCanonicalSeed()
    {
        var fixture = CreateAcceptedEventBudgetFixture(
            remainingUses: 2,
            current: 10m,
            reactionResultKind: "event_outcome",
            includeExactLifecycleEvent: true,
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec(
                    "trigger_candidate_due_reaction_only",
                    "owner_critical_failure",
                    Priority: 10,
                    IncludePeriodic: false,
                    IncludeReaction: true)
            });

        var resolution = EffectAcceptedTurnPlanner.ResolveDuePeriodicResourceMutations(
            fixture.Plan,
            fixture.Owners,
            fixture.Definitions);

        Assert.True(resolution.IsValid, Format(resolution.Issues));
        var candidate = Assert.Single(resolution.TriggerCandidates);
        Assert.Equal(
            "trigger_candidate_due_reaction_only",
            candidate.Activation.Identity.TriggerId);
        Assert.Equal(
            "turn_43:budget:due_lifecycle",
            candidate.Activation.Identity.TriggerEventRef);
        Assert.NotEqual(
            candidate.Activation.Identity.TriggerEventRef,
            candidate.Activation.Identity.EventRef);
        Assert.True(candidate.Activation.ConsumesUse);
        Assert.Equal(2, candidate.UseSeed?.RemainingUses);
        Assert.Null(candidate.Producer);
        Assert.Empty(candidate.PlannedMutationKeys);
        Assert.Empty(candidate.PendingOutputs);
        var reaction = Assert.Single(candidate.ReactionOutputs);
        Assert.Equal("event_outcome", reaction.ResultKind);
        Assert.Empty(resolution.ReactionExecutions);
    }

    [Fact]
    public void AcceptedEventArbiter_UsesTwoSelectsFirstTwoOfThreeActualTriggers()
    {
        var fixture = CreateAcceptedEventBudgetFixture(
            remainingUses: 2,
            current: 10m,
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec(
                    "trigger_budget_30_third",
                    "resource_damaged",
                    Priority: 30,
                    IncludePeriodic: true,
                    IncludeReaction: false),
                new BudgetTriggerSpec(
                    "trigger_budget_10_first",
                    "resource_damaged",
                    Priority: 10,
                    IncludePeriodic: true,
                    IncludeReaction: false),
                new BudgetTriggerSpec(
                    "trigger_budget_20_second",
                    "resource_damaged",
                    Priority: 20,
                    IncludePeriodic: true,
                    IncludeReaction: false)
            });
        var rootDamage = CreateBudgetMutation(
            fixture.Coordinate,
            "turn_43:budget:three_actual",
            ResourceOperation.Damage,
            amount: 6m);

        var result = BuildAcceptedEventBudgetPlan(
            fixture,
            new[] { rootDamage });

        Assert.True(result.Resources.IsValid, Format(result.Resources.Issues));
        Assert.Equal(
            new[]
            {
                "trigger_budget_10_first",
                "trigger_budget_20_second"
            },
            result.Resources.ResourceTriggerExecutions
                .Select(static execution => execution.TriggerId));
        Assert.Equal(
            new int?[] { 2, 1 },
            result.Resources.ResourceTriggerExecutions
                .Select(static execution => execution.RemainingUseBudget));
        Assert.Equal(
            2,
            result.Resources.AppliedTransitions.Count(static transition =>
                transition.Phase == ResourceMutationPhase.EffectTrigger));
        var finalized = Assert.IsType<EffectAcceptedTurnPlanningResult>(
            result.Finalized);
        Assert.True(finalized.Success, Format(finalized.Issues));
        Assert.Empty(finalized.Plan!.ActiveEffects);
        var identityHistory = finalized.Plan.IdentityIndexAfterImage.ToJsonString();
        Assert.Contains(
            "trigger_budget_10_first",
            identityHistory,
            StringComparison.Ordinal);
        Assert.Contains(
            "trigger_budget_20_second",
            identityHistory,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "trigger_budget_30_third",
            identityHistory,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptedBoundary_TwoConsumingSameStackReplacementsRecordBothUsesBeforeOriginalReplace()
    {
        const string replacementDefinitionKey =
            "budget_multi_activation_replacement";
        var replacementDefinition = EffectMaterializationTestFixture
            .CreateDefinition("periodic_restore");
        replacementDefinition["definitionKey"] = replacementDefinitionKey;
        replacementDefinition["stacking"]!["policy"] = "replace";
        replacementDefinition["stacking"]!["maxStacks"] = 1;
        replacementDefinition["stacking"]!["atMaximum"] = "no_change";
        var fixture = CreateAcceptedEventBudgetFixture(
            remainingUses: 2,
            current: 10m,
            reactionResultKind: "apply_definition",
            applyDefinitionSource: replacementDefinition,
            distinctReactionPerTrigger: true,
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec(
                    "trigger_budget_replace_10_first",
                    "resource_damaged",
                    Priority: 10,
                    IncludePeriodic: false,
                    IncludeReaction: true),
                new BudgetTriggerSpec(
                    "trigger_budget_replace_20_second",
                    "resource_damaged",
                    Priority: 20,
                    IncludePeriodic: false,
                    IncludeReaction: true)
            });
        var rootDamage = CreateBudgetMutation(
            fixture.Coordinate,
            "turn_43:budget:two_consuming_replacements",
            ResourceOperation.Damage,
            amount: 2m);

        var result = BuildAcceptedEventBudgetPlan(
            fixture,
            new[] { rootDamage });

        Assert.True(result.Resources.IsValid, Format(result.Resources.Issues));
        var transcript = result.Resources.EffectBoundaryTranscript;
        var accepted = transcript.AcceptedActivations
            .OrderBy(static activation =>
                activation.Activation.Stamp.ActivationOrdinal)
            .ToArray();
        Assert.Equal(2, accepted.Length);
        Assert.Single(accepted
            .Select(static activation => activation.Boundary.BoundaryOrdinal)
            .Distinct());
        Assert.Equal(
            new int?[] { 2, 1 },
            accepted.Select(static activation =>
                activation.Activation.Stamp.UsesBefore));
        Assert.All(
            transcript.ReleasedReactions,
            release =>
            {
                Assert.Equal("apply_definition", release.Reaction.ResultKind);
                Assert.Equal(
                    replacementDefinitionKey,
                    release.Reaction.DownstreamSourceKey?.DefinitionKey);
            });
        Assert.Equal(2, transcript.ReleasedReactions.Count);

        var finalized = Assert.IsType<EffectAcceptedTurnPlanningResult>(
            result.Finalized);
        Assert.True(finalized.Success, Format(finalized.Issues));
        var finalizedPlan = Assert.IsType<EffectAcceptedTurnPlan>(finalized.Plan);
        var identities = finalizedPlan.IdentityIndexAfterImage["entries"]!
            .AsArray()
            .OfType<JsonObject>()
            .ToArray();
        Assert.Equal(3, identities.Length);
        var oldEffectId = Assert.Single(accepted
            .Select(static activation =>
                activation.Activation.Stamp.Identity.EffectId)
            .Distinct(StringComparer.Ordinal));
        var oldIdentity = Assert.Single(identities, identity => string.Equals(
            identity["effectId"]!.GetValue<string>(),
            oldEffectId,
            StringComparison.Ordinal));
        Assert.Equal("replaced", oldIdentity["state"]!.GetValue<string>());
        var oldTransitions = oldIdentity["transitions"]!.AsArray();
        var terminalOldTransitions = oldTransitions
            .TakeLast(3)
            .Select(static transition => transition!.AsObject())
            .ToArray();
        Assert.Equal(
            new[] { "consume", "consume", "replace" },
            terminalOldTransitions.Select(static transition =>
                transition["kind"]!.GetValue<string>()));
        Assert.Equal(
            accepted.Select(static activation =>
                activation.Activation.Stamp.Identity.EventRef),
            terminalOldTransitions.Take(2).Select(static transition =>
                transition["eventRef"]!.GetValue<string>()));

        var intermediateEffectId = Assert.Single(
            terminalOldTransitions[^1]["resultEffectIds"]!.AsArray())!
            .GetValue<string>();
        var intermediateIdentity = Assert.Single(
            identities,
            identity => string.Equals(
                identity["effectId"]!.GetValue<string>(),
                intermediateEffectId,
                StringComparison.Ordinal));
        Assert.Equal(
            "replaced",
            intermediateIdentity["state"]!.GetValue<string>());
        var intermediateReplace = intermediateIdentity["transitions"]!
            .AsArray()[^1]!
            .AsObject();
        Assert.Equal("replace", intermediateReplace["kind"]!.GetValue<string>());
        var finalEffectId = Assert.Single(
            intermediateReplace["resultEffectIds"]!.AsArray())!
            .GetValue<string>();
        var finalIdentity = Assert.Single(identities, identity => string.Equals(
            identity["effectId"]!.GetValue<string>(),
            finalEffectId,
            StringComparison.Ordinal));
        Assert.Equal("active", finalIdentity["state"]!.GetValue<string>());

        var activeEffects = finalizedPlan.ActiveEffects;
        Assert.DoesNotContain(activeEffects, effect => string.Equals(
            effect["effectId"]?.GetValue<string>(),
            oldEffectId,
            StringComparison.Ordinal));
        Assert.DoesNotContain(activeEffects, effect => string.Equals(
            effect["effectId"]?.GetValue<string>(),
            intermediateEffectId,
            StringComparison.Ordinal));
        var finalEffect = Assert.Single(activeEffects);
        Assert.Equal(finalEffectId, finalEffect["effectId"]!.GetValue<string>());
        Assert.Equal(
            replacementDefinitionKey,
            finalEffect["source"]!["definitionKey"]!.GetValue<string>());
        Assert.Equal("turns", finalEffect["lifetime"]!["mode"]!.GetValue<string>());
        Assert.Equal(3, finalEffect["lifetime"]!["remainingTurns"]!.GetValue<int>());
    }

    [Fact]
    public void AcceptedBoundary_DifferentOwnersCannotReplaceSameFrozenTargetBeforeAllocation()
    {
        const string targetEffectId = "effect_budget_competing_replace_target";
        const string targetTransitionId =
            "effect_transition_budget_competing_replace_target";
        const string replacementDefinitionKey =
            "budget_competing_target_replacement";
        const string replacementStackKey =
            "budget-competing-target-replacement-stack";
        var replacementDefinition = EffectMaterializationTestFixture
            .CreateDefinition("periodic_restore");
        replacementDefinition["definitionKey"] = replacementDefinitionKey;
        replacementDefinition["stacking"]!["stackKey"] = replacementStackKey;
        replacementDefinition["stacking"]!["policy"] = "replace";
        replacementDefinition["stacking"]!["maxStacks"] = 1;
        replacementDefinition["stacking"]!["atMaximum"] = "no_change";
        var replacementTarget = EffectMaterializationTestFixture
            .CreateCanonicalEffect("npc", "periodic_restore");
        replacementTarget["effectId"] = targetEffectId;
        replacementTarget["target"]!["targetId"] =
            "npc_accepted_event_budget";
        replacementTarget["source"]!["sourceId"] =
            "wound_accepted_event_budget";
        replacementTarget["source"]!["definitionKey"] =
            replacementDefinitionKey;
        replacementTarget["stacking"]!["stackKey"] = replacementStackKey;
        replacementTarget["stacking"]!["policy"] = "replace";
        replacementTarget["stacking"]!["maxStacks"] = 1;
        replacementTarget["chronology"]!["lastTransitionId"] =
            targetTransitionId;
        var fixture = CreateAcceptedEventBudgetFixture(
            remainingUses: 3,
            current: 10m,
            reactionResultKind: "apply_definition",
            reactionDependency: "after_current_event",
            applyDefinitionSource: replacementDefinition,
            frozenReplacementTargetEffect: replacementTarget,
            includeCompetingReactionOwner: true,
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec(
                    "trigger_budget_competing_target_replace",
                    "resource_damaged",
                    Priority: 10,
                    IncludePeriodic: false,
                    IncludeReaction: true)
            });
        var rootDamage = CreateBudgetMutation(
            fixture.Coordinate,
            "turn_43:budget:competing_target_replace",
            ResourceOperation.Damage,
            amount: 1m);
        var identityFactory = new CountingEffectIdentityFactory();

        var result = BuildAcceptedEventBudgetPlan(
            fixture,
            new[] { rootDamage },
            identityFactory);

        Assert.True(result.Resources.IsValid, Format(result.Resources.Issues));
        var transcript = result.Resources.EffectBoundaryTranscript;
        var accepted = transcript.AcceptedActivations.ToArray();
        Assert.Equal(2, accepted.Length);
        Assert.Equal(
            2,
            accepted.Select(static value =>
                value.Activation.Stamp.Identity.EffectId)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Single(accepted.Select(static value =>
            value.Boundary.BoundaryOrdinal).Distinct());
        var released = transcript.ReleasedReactions.ToArray();
        Assert.Equal(2, released.Length);
        Assert.All(
            released,
            release => Assert.Equal(
                new EffectReplayIdentity(
                    targetEffectId,
                    new ResourcePendingAuthorityBinding(
                        "permanent",
                        targetEffectId)),
                release.Reaction.ReplacementTarget));
        Assert.Equal(
            2,
            transcript.TerminalAvailabilityReservations.Count(value =>
                value.Kind ==
                    EffectTerminalAvailabilityReservationKind
                        .AfterCurrentReaction &&
                value.Subject == new EffectReplayIdentity(
                    targetEffectId,
                    new ResourcePendingAuthorityBinding(
                        "permanent",
                        targetEffectId))));
        var finalized = Assert.IsType<EffectAcceptedTurnPlanningResult>(
            result.Finalized);
        Assert.False(finalized.Success);
        Assert.Null(finalized.Plan);
        var issue = Assert.Single(finalized.Issues);
        Assert.Equal(
            "effect_reaction_replacement_batch_conflict",
            issue.Code);
        Assert.Contains(targetEffectId, issue.Actual, StringComparison.Ordinal);
        Assert.Equal(0, identityFactory.EffectCalls);
        Assert.Equal(0, identityFactory.TransitionCalls);
    }

    [Fact]
    public void AcceptedBoundary_MultipleReplacementsCannotRetargetFrozenEmptyCoordinateBeforeAllocation()
    {
        const string replacementDefinitionKey =
            "budget_competing_empty_replacement";
        const string replacementStackKey =
            "budget-competing-empty-replacement-stack";
        var replacementDefinition = EffectMaterializationTestFixture
            .CreateDefinition("periodic_restore");
        replacementDefinition["definitionKey"] = replacementDefinitionKey;
        replacementDefinition["stacking"]!["stackKey"] = replacementStackKey;
        replacementDefinition["stacking"]!["policy"] = "replace";
        replacementDefinition["stacking"]!["maxStacks"] = 1;
        replacementDefinition["stacking"]!["atMaximum"] = "no_change";
        var fixture = CreateAcceptedEventBudgetFixture(
            remainingUses: 3,
            current: 10m,
            reactionResultKind: "apply_definition",
            reactionDependency: "after_current_event",
            applyDefinitionSource: replacementDefinition,
            distinctReactionPerTrigger: true,
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec(
                    "trigger_budget_empty_replace_first",
                    "resource_damaged",
                    Priority: 10,
                    IncludePeriodic: false,
                    IncludeReaction: true),
                new BudgetTriggerSpec(
                    "trigger_budget_empty_replace_second",
                    "resource_damaged",
                    Priority: 20,
                    IncludePeriodic: false,
                    IncludeReaction: true)
            });
        var rootDamage = CreateBudgetMutation(
            fixture.Coordinate,
            "turn_43:budget:competing_empty_replace",
            ResourceOperation.Damage,
            amount: 1m);
        var identityFactory = new CountingEffectIdentityFactory();

        var result = BuildAcceptedEventBudgetPlan(
            fixture,
            new[] { rootDamage },
            identityFactory);

        Assert.True(result.Resources.IsValid, Format(result.Resources.Issues));
        var transcript = result.Resources.EffectBoundaryTranscript;
        var accepted = transcript.AcceptedActivations.ToArray();
        Assert.Equal(2, accepted.Length);
        Assert.Single(
            accepted.Select(static value =>
                value.Activation.Stamp.Identity.EffectId)
                .Distinct(StringComparer.Ordinal));
        Assert.Single(accepted.Select(static value =>
            value.Boundary.BoundaryOrdinal).Distinct());
        var released = transcript.ReleasedReactions.ToArray();
        Assert.Equal(2, released.Length);
        Assert.All(released, static release =>
            Assert.Null(release.Reaction.ReplacementTarget));
        Assert.All(
            released,
            release => Assert.Equal(
                replacementDefinitionKey,
                release.Reaction.DownstreamSourceKey?.DefinitionKey));
        Assert.Empty(transcript.TerminalAvailabilityReservations);
        var finalized = Assert.IsType<EffectAcceptedTurnPlanningResult>(
            result.Finalized);
        Assert.False(finalized.Success);
        Assert.Null(finalized.Plan);
        var issue = Assert.Single(finalized.Issues);
        Assert.Equal(
            "effect_reaction_replacement_batch_conflict",
            issue.Code);
        Assert.Contains(
            replacementStackKey,
            issue.Actual,
            StringComparison.Ordinal);
        Assert.Equal(0, identityFactory.EffectCalls);
        Assert.Equal(0, identityFactory.TransitionCalls);
    }

    [Fact]
    public void AcceptedBoundary_NonReplacementCannotOccupyFrozenEmptyCoordinateBeforeAllocation()
    {
        const string sharedStackKey =
            "budget-non-replace-before-empty-replacement-stack";
        const string stackingDefinitionKey =
            "budget_non_replace_before_empty_replacement";
        const string replacementDefinitionKey =
            "budget_replace_after_empty_coordinate";
        var stackingDefinition = EffectMaterializationTestFixture
            .CreateDefinition("periodic_restore");
        stackingDefinition["definitionKey"] = stackingDefinitionKey;
        stackingDefinition["stacking"]!["stackKey"] = sharedStackKey;
        stackingDefinition["stacking"]!["policy"] = "stack";
        stackingDefinition["stacking"]!["maxStacks"] = 3;
        stackingDefinition["stacking"]!["atMaximum"] = "no_change";
        var replacementDefinition = EffectMaterializationTestFixture
            .CreateDefinition("periodic_restore");
        replacementDefinition["definitionKey"] = replacementDefinitionKey;
        replacementDefinition["stacking"]!["stackKey"] = sharedStackKey;
        replacementDefinition["stacking"]!["policy"] = "replace";
        replacementDefinition["stacking"]!["maxStacks"] = 1;
        replacementDefinition["stacking"]!["atMaximum"] = "no_change";
        var fixture = CreateAcceptedEventBudgetFixture(
            remainingUses: 3,
            current: 10m,
            reactionResultKind: "apply_definition",
            reactionDependency: "after_current_event",
            siblingReactionResultKind: "apply_definition",
            applyDefinitionSource: stackingDefinition,
            siblingApplyDefinitionSource: replacementDefinition,
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec(
                    "trigger_budget_non_replace_before_empty_replacement",
                    "resource_damaged",
                    Priority: 10,
                    IncludePeriodic: false,
                    IncludeReaction: true)
            });
        var rootDamage = CreateBudgetMutation(
            fixture.Coordinate,
            "turn_43:budget:non_replace_before_empty_replacement",
            ResourceOperation.Damage,
            amount: 1m);
        var identityFactory = new CountingEffectIdentityFactory();

        var result = BuildAcceptedEventBudgetPlan(
            fixture,
            new[] { rootDamage },
            identityFactory);

        Assert.True(result.Resources.IsValid, Format(result.Resources.Issues));
        var released = result.Resources.EffectBoundaryTranscript
            .ReleasedReactions
            .ToArray();
        Assert.Equal(2, released.Length);
        var replacement = Assert.Single(released, release =>
            string.Equals(
                release.Reaction.DownstreamSourceKey?.DefinitionKey,
                replacementDefinitionKey,
                StringComparison.Ordinal));
        Assert.Null(replacement.Reaction.ReplacementTarget);
        var finalized = Assert.IsType<EffectAcceptedTurnPlanningResult>(
            result.Finalized);
        Assert.False(finalized.Success);
        Assert.Null(finalized.Plan);
        Assert.Contains(
            finalized.Issues,
            static issue => issue.Code ==
                "effect_reaction_replacement_batch_conflict");
        Assert.Equal(0, identityFactory.EffectCalls);
        Assert.Equal(0, identityFactory.TransitionCalls);
    }

    [Fact]
    public void AcceptedBoundary_StackPolicyConflictBeforeReplacementFailsBeforeAllocation()
    {
        const string targetEffectId =
            "effect_budget_policy_conflict_replace_target";
        const string targetTransitionId =
            "effect_transition_budget_policy_conflict_replace_target";
        const string sharedStackKey =
            "budget-policy-conflict-before-replacement-stack";
        const string refreshDefinitionKey =
            "budget_refresh_before_replacement_conflict";
        const string replacementDefinitionKey =
            "budget_policy_conflict_replacement";
        var refreshDefinition = EffectMaterializationTestFixture
            .CreateDefinition("periodic_restore");
        refreshDefinition["definitionKey"] = refreshDefinitionKey;
        refreshDefinition["stacking"]!["stackKey"] = sharedStackKey;
        refreshDefinition["stacking"]!["policy"] = "refresh";
        refreshDefinition["stacking"]!["maxStacks"] = 1;
        refreshDefinition["stacking"]!["atMaximum"] = "no_change";
        refreshDefinition["stacking"]!["refreshMode"] = "reset";
        var replacementDefinition = EffectMaterializationTestFixture
            .CreateDefinition("periodic_restore");
        replacementDefinition["definitionKey"] = replacementDefinitionKey;
        replacementDefinition["stacking"]!["stackKey"] = sharedStackKey;
        replacementDefinition["stacking"]!["policy"] = "replace";
        replacementDefinition["stacking"]!["maxStacks"] = 1;
        replacementDefinition["stacking"]!["atMaximum"] = "no_change";
        var replacementTarget = EffectMaterializationTestFixture
            .CreateCanonicalEffect("npc", "periodic_restore");
        replacementTarget["effectId"] = targetEffectId;
        replacementTarget["target"]!["targetId"] =
            "npc_accepted_event_budget";
        replacementTarget["source"]!["sourceId"] =
            "wound_accepted_event_budget";
        replacementTarget["source"]!["definitionKey"] =
            replacementDefinitionKey;
        replacementTarget["stacking"]!["stackKey"] = sharedStackKey;
        replacementTarget["stacking"]!["policy"] = "replace";
        replacementTarget["stacking"]!["maxStacks"] = 1;
        replacementTarget["chronology"]!["lastTransitionId"] =
            targetTransitionId;
        var fixture = CreateAcceptedEventBudgetFixture(
            remainingUses: 3,
            current: 10m,
            reactionResultKind: "apply_definition",
            reactionDependency: "after_current_event",
            siblingReactionResultKind: "apply_definition",
            applyDefinitionSource: refreshDefinition,
            siblingApplyDefinitionSource: replacementDefinition,
            frozenReplacementTargetEffect: replacementTarget,
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec(
                    "trigger_budget_policy_conflict_before_replacement",
                    "resource_damaged",
                    Priority: 10,
                    IncludePeriodic: false,
                    IncludeReaction: true)
            });
        var rootDamage = CreateBudgetMutation(
            fixture.Coordinate,
            "turn_43:budget:policy_conflict_before_replacement",
            ResourceOperation.Damage,
            amount: 1m);
        var identityFactory = new CountingEffectIdentityFactory();

        var result = BuildAcceptedEventBudgetPlan(
            fixture,
            new[] { rootDamage },
            identityFactory);

        Assert.True(result.Resources.IsValid, Format(result.Resources.Issues));
        var replacement = Assert.Single(
            result.Resources.EffectBoundaryTranscript.ReleasedReactions,
            release => string.Equals(
                release.Reaction.DownstreamSourceKey?.DefinitionKey,
                replacementDefinitionKey,
                StringComparison.Ordinal));
        Assert.Equal(
            new EffectReplayIdentity(
                targetEffectId,
                new ResourcePendingAuthorityBinding(
                    "permanent",
                    targetEffectId)),
            replacement.Reaction.ReplacementTarget);
        var finalized = Assert.IsType<EffectAcceptedTurnPlanningResult>(
            result.Finalized);
        Assert.False(finalized.Success);
        Assert.Null(finalized.Plan);
        Assert.Contains(
            finalized.Issues,
            static issue => issue.Code ==
                "effect_reaction_replacement_batch_conflict");
        Assert.Equal(0, identityFactory.EffectCalls);
        Assert.Equal(0, identityFactory.TransitionCalls);
    }

    [Fact]
    public void AcceptedBoundary_CompatibleStackBeforeReplacementPreservesSiblingAndSucceeds()
    {
        const string targetEffectId =
            "effect_budget_stack_before_replace_target";
        const string targetTransitionId =
            "effect_transition_budget_stack_before_replace_target";
        const string sharedStackKey =
            "budget-compatible-stack-before-replacement";
        const string stackingDefinitionKey =
            "budget_compatible_stack_before_replacement";
        const string replacementDefinitionKey =
            "budget_replacement_after_compatible_stack";
        var stackingDefinition = EffectMaterializationTestFixture
            .CreateDefinition("periodic_restore");
        stackingDefinition["definitionKey"] = stackingDefinitionKey;
        stackingDefinition["stacking"]!["stackKey"] = sharedStackKey;
        stackingDefinition["stacking"]!["policy"] = "stack";
        stackingDefinition["stacking"]!["maxStacks"] = 3;
        stackingDefinition["stacking"]!["atMaximum"] = "no_change";
        var replacementDefinition = EffectMaterializationTestFixture
            .CreateDefinition("periodic_restore");
        replacementDefinition["definitionKey"] = replacementDefinitionKey;
        replacementDefinition["stacking"]!["stackKey"] = sharedStackKey;
        replacementDefinition["stacking"]!["policy"] = "replace";
        replacementDefinition["stacking"]!["maxStacks"] = 1;
        replacementDefinition["stacking"]!["atMaximum"] = "no_change";
        var replacementTarget = EffectMaterializationTestFixture
            .CreateCanonicalEffect("npc", "periodic_restore");
        replacementTarget["effectId"] = targetEffectId;
        replacementTarget["target"]!["targetId"] =
            "npc_accepted_event_budget";
        replacementTarget["source"]!["sourceId"] =
            "wound_accepted_event_budget";
        replacementTarget["source"]!["definitionKey"] =
            stackingDefinitionKey;
        replacementTarget["stacking"]!["stackKey"] = sharedStackKey;
        replacementTarget["stacking"]!["policy"] = "stack";
        replacementTarget["stacking"]!["maxStacks"] = 3;
        replacementTarget["chronology"]!["lastTransitionId"] =
            targetTransitionId;
        var fixture = CreateAcceptedEventBudgetFixture(
            remainingUses: 3,
            current: 10m,
            reactionResultKind: "apply_definition",
            reactionDependency: "after_current_event",
            siblingReactionResultKind: "apply_definition",
            applyDefinitionSource: stackingDefinition,
            siblingApplyDefinitionSource: replacementDefinition,
            frozenReplacementTargetEffect: replacementTarget,
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec(
                    "trigger_budget_compatible_stack_before_replacement",
                    "resource_damaged",
                    Priority: 10,
                    IncludePeriodic: false,
                    IncludeReaction: true)
            });
        var rootDamage = CreateBudgetMutation(
            fixture.Coordinate,
            "turn_43:budget:compatible_stack_before_replacement",
            ResourceOperation.Damage,
            amount: 1m);

        var result = BuildAcceptedEventBudgetPlan(
            fixture,
            new[] { rootDamage });

        Assert.True(result.Resources.IsValid, Format(result.Resources.Issues));
        var finalized = Assert.IsType<EffectAcceptedTurnPlanningResult>(
            result.Finalized);
        Assert.True(finalized.Success, Format(finalized.Issues));
        var finalizedPlan = Assert.IsType<EffectAcceptedTurnPlan>(finalized.Plan);
        var targetIdentity = Assert.Single(
            finalizedPlan.IdentityIndexAfterImage["entries"]!
                .AsArray()
                .OfType<JsonObject>(),
            identity => string.Equals(
                identity["effectId"]?.GetValue<string>(),
                targetEffectId,
                StringComparison.Ordinal));
        Assert.Equal("replaced", targetIdentity["state"]!.GetValue<string>());
        Assert.Equal(
            new[] { "stack", "replace" },
            targetIdentity["transitions"]!
                .AsArray()
                .TakeLast(2)
                .Select(static transition =>
                    transition!["kind"]!.GetValue<string>()));
        Assert.DoesNotContain(
            finalizedPlan.ActiveEffects,
            effect => string.Equals(
                effect["effectId"]?.GetValue<string>(),
                targetEffectId,
                StringComparison.Ordinal));
        Assert.Contains(
            finalizedPlan.ActiveEffects,
            effect => string.Equals(
                effect["source"]?["definitionKey"]?.GetValue<string>(),
                replacementDefinitionKey,
                StringComparison.Ordinal));
    }

    [Fact]
    public void AcceptedBoundary_AfterCurrentReplacementReservationBlocksLaterRootBoundary()
    {
        const string replacementDefinitionKey =
            "budget_after_current_replacement";
        var replacementDefinition = EffectMaterializationTestFixture
            .CreateDefinition("periodic_restore");
        replacementDefinition["definitionKey"] = replacementDefinitionKey;
        replacementDefinition["stacking"]!["policy"] = "replace";
        replacementDefinition["stacking"]!["maxStacks"] = 1;
        replacementDefinition["stacking"]!["atMaximum"] = "no_change";
        var fixture = CreateAcceptedEventBudgetFixture(
            remainingUses: 3,
            current: 10m,
            reactionResultKind: "apply_definition",
            reactionDependency: "after_current_event",
            applyDefinitionSource: replacementDefinition,
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec(
                    "trigger_budget_after_current_replace",
                    "resource_damaged",
                    Priority: 10,
                    IncludePeriodic: false,
                    IncludeReaction: true)
            });
        var firstDamage = CreateBudgetMutation(
            fixture.Coordinate,
            "turn_43:budget:after_current_replace:01",
            ResourceOperation.Damage,
            amount: 1m);
        var secondDamage = CreateBudgetMutation(
            fixture.Coordinate,
            "turn_43:budget:after_current_replace:02",
            ResourceOperation.Damage,
            amount: 1m);

        var result = BuildAcceptedEventBudgetPlan(
            fixture,
            new[] { firstDamage, secondDamage });

        Assert.True(result.Resources.IsValid, Format(result.Resources.Issues));
        var transcript = result.Resources.EffectBoundaryTranscript;
        var accepted = Assert.Single(transcript.AcceptedActivations);
        var oldEffectId = accepted.Activation.Stamp.Identity.EffectId;
        var reservation = Assert.Single(
            transcript.TerminalAvailabilityReservations,
            static value => value.Kind ==
                EffectTerminalAvailabilityReservationKind.AfterCurrentReaction);
        Assert.Equal(oldEffectId, reservation.Subject.EffectId);
        Assert.Equal(
            new ResourcePendingAuthorityBinding("permanent", oldEffectId),
            reservation.Subject.Authority);
        var released = Assert.Single(transcript.ReleasedReactions);
        Assert.Equal(
            EffectReactionReleaseStage.AfterCurrentEvent,
            released.Stage);
        Assert.Equal(oldEffectId, released.Reaction.ReplacementTargetEffectId);
        var rejected = Assert.Single(
            transcript.RejectedActivations,
            rejection => string.Equals(
                rejection.Candidate.Activation.Identity.EffectId,
                oldEffectId,
                StringComparison.Ordinal));
        Assert.Equal(
            EffectActivationRejectionReason.EffectTerminal,
            rejected.Reason);
        Assert.True(
            rejected.Boundary.BoundaryOrdinal >
            accepted.Boundary.BoundaryOrdinal);

        var finalized = Assert.IsType<EffectAcceptedTurnPlanningResult>(
            result.Finalized);
        Assert.True(finalized.Success, Format(finalized.Issues));
        var finalizedPlan = Assert.IsType<EffectAcceptedTurnPlan>(finalized.Plan);
        Assert.DoesNotContain(finalizedPlan.ActiveEffects, effect => string.Equals(
            effect["effectId"]?.GetValue<string>(),
            oldEffectId,
            StringComparison.Ordinal));
        var replacement = Assert.Single(finalizedPlan.ActiveEffects);
        Assert.Equal(
            replacementDefinitionKey,
            replacement["source"]!["definitionKey"]!.GetValue<string>());
    }

    [Fact]
    public void AcceptedBoundary_DistinctReplacementTargetReservationRejectsLaterCandidateBeforeSecondRelease()
    {
        const string ownerEffectId = "effect_accepted_event_budget";
        const string targetEffectId = "effect_budget_distinct_replace_target";
        const string targetTransitionId =
            "effect_transition_budget_distinct_replace_target";
        const string replacementDefinitionKey =
            "budget_distinct_target_replacement";
        const string replacementStackKey =
            "budget-distinct-target-replacement-stack";
        var replacementDefinition = EffectMaterializationTestFixture
            .CreateDefinition("periodic_restore");
        replacementDefinition["definitionKey"] = replacementDefinitionKey;
        replacementDefinition["stacking"]!["stackKey"] = replacementStackKey;
        replacementDefinition["stacking"]!["policy"] = "replace";
        replacementDefinition["stacking"]!["maxStacks"] = 1;
        replacementDefinition["stacking"]!["atMaximum"] = "no_change";
        var replacementTarget = EffectMaterializationTestFixture
            .CreateCanonicalEffect("npc", "periodic_restore");
        replacementTarget["effectId"] = targetEffectId;
        replacementTarget["target"]!["targetId"] =
            "npc_accepted_event_budget";
        replacementTarget["source"]!["sourceId"] =
            "wound_accepted_event_budget";
        replacementTarget["source"]!["definitionKey"] =
            replacementDefinitionKey;
        replacementTarget["stacking"]!["stackKey"] = replacementStackKey;
        replacementTarget["stacking"]!["policy"] = "replace";
        replacementTarget["stacking"]!["maxStacks"] = 1;
        replacementTarget["chronology"]!["lastTransitionId"] =
            targetTransitionId;
        var fixture = CreateAcceptedEventBudgetFixture(
            remainingUses: 3,
            current: 10m,
            reactionResultKind: "apply_definition",
            reactionDependency: "after_current_event",
            applyDefinitionSource: replacementDefinition,
            frozenReplacementTargetEffect: replacementTarget,
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec(
                    "trigger_budget_distinct_target_replace",
                    "resource_damaged",
                    Priority: 10,
                    IncludePeriodic: false,
                    IncludeReaction: true)
            });
        var firstDamage = CreateBudgetMutation(
            fixture.Coordinate,
            "turn_43:budget:distinct_target_replace:01",
            ResourceOperation.Damage,
            amount: 1m);
        var secondDamage = CreateBudgetMutation(
            fixture.Coordinate,
            "turn_43:budget:distinct_target_replace:02",
            ResourceOperation.Damage,
            amount: 1m);

        var result = BuildAcceptedEventBudgetPlan(
            fixture,
            new[] { firstDamage, secondDamage });

        Assert.True(result.Resources.IsValid, Format(result.Resources.Issues));
        var transcript = result.Resources.EffectBoundaryTranscript;
        Assert.All(
            transcript.AcceptedActivations
                .Select(static accepted => accepted.Candidate)
                .Concat(transcript.RejectedActivations.Select(
                    static rejected => rejected.Candidate)),
            candidate => Assert.Equal(
                targetEffectId,
                Assert.Single(candidate.ReactionOutputs)
                    .ReplacementTargetEffectId));
        var accepted = Assert.Single(transcript.AcceptedActivations);
        Assert.Equal(ownerEffectId, accepted.Activation.Stamp.Identity.EffectId);
        var reservation = Assert.Single(
            transcript.TerminalAvailabilityReservations,
            static value => value.Kind ==
                EffectTerminalAvailabilityReservationKind.AfterCurrentReaction);
        Assert.Equal(targetEffectId, reservation.Subject.EffectId);
        Assert.Equal(
            new ResourcePendingAuthorityBinding("permanent", targetEffectId),
            reservation.Subject.Authority);
        var rejected = Assert.Single(transcript.RejectedActivations);
        Assert.Equal(ownerEffectId, rejected.Candidate.Activation.Identity.EffectId);
        Assert.Equal(
            targetEffectId,
            Assert.Single(rejected.Candidate.ReactionOutputs)
                .ReplacementTargetEffectId);
        Assert.Equal(
            EffectActivationRejectionReason.EffectTerminal,
            rejected.Reason);
        Assert.True(
            rejected.Boundary.BoundaryOrdinal >
            accepted.Boundary.BoundaryOrdinal);
        var released = Assert.Single(transcript.ReleasedReactions);
        Assert.Equal(targetEffectId, released.Reaction.ReplacementTargetEffectId);
        Assert.Equal(
            EffectReactionReleaseStage.AfterCurrentEvent,
            released.Stage);

        var finalized = Assert.IsType<EffectAcceptedTurnPlanningResult>(
            result.Finalized);
        Assert.True(finalized.Success, Format(finalized.Issues));
        var finalizedPlan = Assert.IsType<EffectAcceptedTurnPlan>(finalized.Plan);
        Assert.Contains(finalizedPlan.ActiveEffects, effect => string.Equals(
            effect["effectId"]?.GetValue<string>(),
            ownerEffectId,
            StringComparison.Ordinal));
        Assert.DoesNotContain(finalizedPlan.ActiveEffects, effect => string.Equals(
            effect["effectId"]?.GetValue<string>(),
            targetEffectId,
            StringComparison.Ordinal));
        var replacement = Assert.Single(
            finalizedPlan.ActiveEffects,
            effect => string.Equals(
                effect["source"]?["definitionKey"]?.GetValue<string>(),
                replacementDefinitionKey,
                StringComparison.Ordinal));
        var replacementEffectId = replacement["effectId"]!.GetValue<string>();
        Assert.NotEqual(targetEffectId, replacementEffectId);
        var targetIdentity = Assert.Single(
            finalizedPlan.IdentityIndexAfterImage["entries"]!
                .AsArray()
                .OfType<JsonObject>(),
            identity => string.Equals(
                identity["effectId"]?.GetValue<string>(),
                targetEffectId,
                StringComparison.Ordinal));
        Assert.Equal("replaced", targetIdentity["state"]!.GetValue<string>());
        var replaceTransition = Assert.Single(
            targetIdentity["transitions"]!
                .AsArray()
                .OfType<JsonObject>(),
            transition => string.Equals(
                transition["kind"]?.GetValue<string>(),
                "replace",
                StringComparison.Ordinal));
        Assert.Equal(
            replacementEffectId,
            Assert.Single(replaceTransition["resultEffectIds"]!.AsArray())!
                .GetValue<string>());
    }

    [Theory]
    [MemberData(nameof(ReactionUseProjectionMatrix))]
    public void AcceptedBoundary_UseProjectionMatrixPreservesReleasedReactionAndTerminalOrder(
        bool consumesUse,
        string dependency,
        bool lastUse,
        string resultKind)
    {
        var remainingUses = lastUse ? 1 : 2;
        var eventKind = string.Equals(
            resultKind,
            "event_outcome",
            StringComparison.Ordinal)
                ? "owner_critical_failure"
                : "resource_damaged";
        var triggerSpecs = new List<BudgetTriggerSpec>
        {
            new(
                "trigger_budget_projection_matrix",
                eventKind,
                Priority: 10,
                IncludePeriodic: true,
                IncludeReaction: true,
                ConsumesUse: consumesUse)
        };
        if (!consumesUse)
        {
            triggerSpecs.Add(new BudgetTriggerSpec(
                "trigger_budget_projection_matrix_consuming_control",
                "resource_spent",
                Priority: 20,
                IncludePeriodic: true,
                IncludeReaction: false));
        }
        var fixture = CreateAcceptedEventBudgetFixture(
            remainingUses,
            current: 5m,
            reactionResultKind: resultKind,
            reactionDependency: dependency,
            reactionAfterComponentId: string.Equals(
                dependency,
                "after_component",
                StringComparison.Ordinal)
                    ? "component_budget_periodic"
                    : null,
            includeExactLifecycleEvent: string.Equals(
                resultKind,
                "event_outcome",
                StringComparison.Ordinal),
            triggerSpecs: triggerSpecs.ToArray());

        var result = string.Equals(
            resultKind,
            "event_outcome",
            StringComparison.Ordinal)
                ? BuildAcceptedEventBudgetPlan(
                    fixture,
                    EffectAcceptedTurnPlanner.ResolveDuePeriodicResourceMutations(
                        fixture.Plan,
                        fixture.Owners,
                        fixture.Definitions))
                : BuildAcceptedEventBudgetPlan(
                    fixture,
                    new[]
                    {
                        CreateBudgetMutation(
                            fixture.Coordinate,
                            "turn_43:budget:projection_matrix",
                            ResourceOperation.Damage,
                            amount: 1m)
                    });

        Assert.True(result.Resources.IsValid, Format(result.Resources.Issues));
        var accepted = Assert.Single(
            result.Resources.EffectBoundaryTranscript.AcceptedActivations);
        Assert.Equal(consumesUse, accepted.Activation.Stamp.ConsumesUse);
        Assert.Equal(
            consumesUse ? remainingUses : null,
            accepted.Activation.Stamp.UsesBefore);
        Assert.Equal(
            consumesUse ? remainingUses - 1 : null,
            accepted.Activation.UsesAfter);
        Assert.Equal(
            consumesUse && lastUse,
            accepted.Activation.EffectTerminal);
        var release = Assert.Single(
            result.Resources.EffectBoundaryTranscript.ReleasedReactions);
        Assert.Equal(
            dependency switch
            {
                "before_current_event" =>
                    EffectReactionReleaseStage.BeforeCurrentEvent,
                "after_component" =>
                    EffectReactionReleaseStage.AfterComponent,
                "after_current_event" =>
                    EffectReactionReleaseStage.AfterCurrentEvent,
                _ => throw new ArgumentOutOfRangeException(nameof(dependency))
            },
            release.Stage);
        Assert.Equal(resultKind, release.Reaction.ResultKind);
        if (string.Equals(
                dependency,
                "after_component",
                StringComparison.Ordinal))
        {
            Assert.Single(
                result.Resources.EffectBoundaryTranscript
                    .AppliedComponentEvidence,
                evidence => string.Equals(
                    evidence.ComponentId,
                    "component_budget_periodic",
                    StringComparison.Ordinal));
        }

        var finalized = Assert.IsType<EffectAcceptedTurnPlanningResult>(
            result.Finalized);
        Assert.True(finalized.Success, Format(finalized.Issues));
        var plan = Assert.IsType<EffectAcceptedTurnPlan>(finalized.Plan);
        Assert.Equal(1, plan.ReactionExpansionCount);
        var identity = Assert.Single(
            plan.IdentityIndexAfterImage["entries"]!.AsArray())!.AsObject();
        Assert.Contains(
            release.Reaction.EventRef,
            identity.ToJsonString(),
            StringComparison.Ordinal);
        var expectedState = consumesUse && lastUse
            ? "expired"
            : string.Equals(resultKind, "suspend", StringComparison.Ordinal)
                ? "suspended"
                : "active";
        Assert.Equal(expectedState, identity["state"]!.GetValue<string>());
        if (string.Equals(expectedState, "expired", StringComparison.Ordinal))
        {
            Assert.Empty(plan.ActiveEffects);
        }
        else
        {
            var active = Assert.Single(plan.ActiveEffects);
            Assert.Equal(expectedState, active["state"]!.GetValue<string>());
            Assert.Equal(
                consumesUse ? remainingUses - 1 : remainingUses,
                active["lifetime"]!["remainingUses"]!.GetValue<int>());
        }
    }

    [Fact]
    public void AcceptedEventArbiter_UnrealizedBoundaryDoesNotConsumeLaterActualEvent()
    {
        var fixture = CreateAcceptedEventBudgetFixture(
            remainingUses: 1,
            current: 5m,
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec(
                    "trigger_budget_10_unrealized_filled",
                    "resource_filled",
                    Priority: 10,
                    IncludePeriodic: true,
                    IncludeReaction: false),
                new BudgetTriggerSpec(
                    "trigger_budget_20_actual_damaged",
                    "resource_damaged",
                    Priority: 20,
                    IncludePeriodic: true,
                    IncludeReaction: false)
            });
        var restore = CreateBudgetMutation(
            fixture.Coordinate,
            "turn_43:budget:unrealized_filled",
            ResourceOperation.Restore,
            amount: 1m);
        var damage = CreateBudgetMutation(
            fixture.Coordinate,
            "turn_43:budget:later_actual_damage",
            ResourceOperation.Damage,
            amount: 1m,
            dependencies: new[] { restore.Key });

        var result = BuildAcceptedEventBudgetPlan(
            fixture,
            new[] { restore, damage });

        Assert.True(result.Resources.IsValid, Format(result.Resources.Issues));
        var execution = Assert.Single(result.Resources.ResourceTriggerExecutions);
        Assert.Equal("trigger_budget_20_actual_damaged", execution.TriggerId);
        Assert.Equal(1, execution.RemainingUseBudget);
        Assert.Equal(
            1,
            result.Resources.AppliedTransitions.Count(static transition =>
                transition.Phase == ResourceMutationPhase.EffectTrigger));
        var finalized = Assert.IsType<EffectAcceptedTurnPlanningResult>(
            result.Finalized);
        Assert.True(finalized.Success, Format(finalized.Issues));
        Assert.Empty(finalized.Plan!.ActiveEffects);
        var identityHistory = finalized.Plan.IdentityIndexAfterImage.ToJsonString();
        Assert.Contains(execution.EventRef, identityHistory, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "trigger_budget_10_unrealized_filled",
            identityHistory,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptedEventArbiter_ReactionOnlyCompetesWithMutationForFinalUse()
    {
        var fixture = CreateAcceptedEventBudgetFixture(
            remainingUses: 1,
            current: 10m,
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec(
                    "trigger_budget_10_reaction",
                    "resource_damaged",
                    Priority: 10,
                    IncludePeriodic: false,
                    IncludeReaction: true),
                new BudgetTriggerSpec(
                    "trigger_budget_20_mutation",
                    "resource_damaged",
                    Priority: 20,
                    IncludePeriodic: true,
                    IncludeReaction: false)
            });
        var rootDamage = CreateBudgetMutation(
            fixture.Coordinate,
            "turn_43:budget:reaction_competes",
            ResourceOperation.Damage,
            amount: 2m);

        var result = BuildAcceptedEventBudgetPlan(
            fixture,
            new[] { rootDamage });

        Assert.True(result.Resources.IsValid, Format(result.Resources.Issues));
        var execution = Assert.Single(result.Resources.ResourceTriggerExecutions);
        Assert.Equal("trigger_budget_10_reaction", execution.TriggerId);
        Assert.Empty(execution.MutationKeys);
        Assert.Equal(1, execution.RemainingUseBudget);
        Assert.DoesNotContain(
            result.Resources.AppliedTransitions,
            static transition =>
                transition.Phase == ResourceMutationPhase.EffectTrigger);
        var reaction = Assert.Single(result.AcceptedReactions);
        Assert.Equal(execution.TriggerId, reaction.TriggerId);
        Assert.Equal("suspend", reaction.ResultKind);
        var finalized = Assert.IsType<EffectAcceptedTurnPlanningResult>(
            result.Finalized);
        Assert.True(finalized.Success, Format(finalized.Issues));
        Assert.Empty(finalized.Plan!.ActiveEffects);
        var identityHistory = finalized.Plan.IdentityIndexAfterImage.ToJsonString();
        Assert.Contains(execution.EventRef, identityHistory, StringComparison.Ordinal);
        Assert.Contains(reaction.EventRef, identityHistory, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "trigger_budget_20_mutation",
            identityHistory,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptedEventArbiterBoundary_ReducerSkippedPredecessorConsumesUseWithoutPublishingAfterComponentReaction()
    {
        var fixture = CreateAcceptedEventBudgetFixture(
            remainingUses: 1,
            current: 10m,
            reactionDependency: "after_component",
            reactionAfterComponentId: "component_budget_periodic",
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec(
                    "trigger_budget_replayed_predecessor",
                    "resource_spent",
                    Priority: 10,
                    IncludePeriodic: true,
                    IncludeReaction: true)
            });
        var producerBaseline = AddBudgetProducerResource(fixture);
        fixture = producerBaseline.Fixture;
        var rootSpend = CreateBudgetMutation(
            producerBaseline.Coordinate,
            "turn_43:budget:replayed_predecessor",
            ResourceOperation.Spend,
            amount: 2m);
        var replaySeeded = SeedExactEffectMutationReplay(fixture, rootSpend);
        var materialized = EffectAcceptedTurnPlanner.ResolveResourceEventMutations(
            replaySeeded.Plan,
            new ResourceAppliedEvent(
                "resource_spent",
                "operation_budget_replayed_predecessor_probe",
                rootSpend.EventRef,
                rootSpend.Coordinate,
                Before: 10m,
                After: 8m,
                AppliedAmount: 2m,
                Turn: 43,
                ExecutionSequence: 0,
                SourceFingerprint: FingerprintB),
            rootSpend.Key,
            replaySeeded.Owners,
            replaySeeded.Definitions);

        Assert.True(materialized.IsValid, Format(materialized.Issues));
        var materializedCandidate = Assert.Single(materialized.TriggerCandidates);
        Assert.True(materializedCandidate.Activation.ConsumesUse);
        Assert.Equal(1, materializedCandidate.UseSeed?.RemainingUses);
        Assert.Equal(rootSpend.Key, materializedCandidate.Producer);
        Assert.Single(materializedCandidate.PlannedMutationKeys);
        Assert.Single(materializedCandidate.ReactionOutputs);

        var result = BuildAcceptedEventBudgetPlan(
            replaySeeded,
            new[] { rootSpend });

        Assert.True(result.Resources.IsValid, Format(result.Resources.Issues));
        Assert.Contains(
            result.Resources.ReplayTransitions,
            static transition =>
                transition.Phase == ResourceMutationPhase.EffectTrigger);
        var execution = Assert.Single(result.Resources.ResourceTriggerExecutions);
        Assert.Equal("trigger_budget_replayed_predecessor", execution.TriggerId);
        Assert.Equal(1, execution.RemainingUseBudget);
        Assert.Empty(execution.MutationKeys);
        Assert.Empty(execution.ComponentIds ?? Array.Empty<string>());
        Assert.DoesNotContain(
            result.Resources.AppliedTransitions,
            static transition =>
                transition.Phase == ResourceMutationPhase.EffectTrigger);
        Assert.Empty(result.AcceptedReactions);

        var finalized = Assert.IsType<EffectAcceptedTurnPlanningResult>(
            result.Finalized);
        Assert.True(finalized.Success, Format(finalized.Issues));
        Assert.Empty(finalized.Plan!.ActiveEffects);
        Assert.Equal(0, finalized.Plan.ReactionExpansionCount);
        var identityHistory = finalized.Plan.IdentityIndexAfterImage.ToJsonString();
        Assert.Contains(execution.EventRef, identityHistory, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "turn_43:budget:replayed_predecessor:reaction:" +
            "effect_accepted_event_budget:" +
            "trigger_budget_replayed_predecessor:" +
            "component_budget_reaction",
            identityHistory,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptedEventArbiterBoundary_ProductionGeneratedChildWithUnmetPredecessorConsumesUseWithoutPublishingOutputs()
    {
        var fixture = CreateAcceptedEventBudgetFixture(
            remainingUses: 2,
            current: 5m,
            reactionResultKind: "trigger_component",
            reactionDependency: "after_component",
            reactionAfterComponentId: "component_budget_periodic",
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec(
                    "trigger_budget_unmet_predecessor",
                    "resource_spent",
                    Priority: 10,
                    IncludePeriodic: true,
                    IncludeReaction: true)
            });
        var producerBaseline = AddBudgetProducerResource(fixture);
        fixture = producerBaseline.Fixture;
        var rootSpend = CreateBudgetMutation(
            producerBaseline.Coordinate,
            "turn_43:budget:unmet_predecessor",
            ResourceOperation.Spend,
            amount: 2m);
        var materialized = EffectAcceptedTurnPlanner.ResolveResourceEventMutations(
            fixture.Plan,
            new ResourceAppliedEvent(
                "resource_spent",
                "operation_budget_unmet_predecessor_probe",
                rootSpend.EventRef,
                rootSpend.Coordinate,
                Before: 10m,
                After: 8m,
                AppliedAmount: 2m,
                Turn: 43,
                ExecutionSequence: 0,
                SourceFingerprint: FingerprintB),
            rootSpend.Key,
            fixture.Owners,
            fixture.Definitions);

        Assert.True(materialized.IsValid, Format(materialized.Issues));
        var materializedCandidate = Assert.Single(materialized.TriggerCandidates);
        Assert.True(materializedCandidate.Activation.ConsumesUse);
        Assert.Equal(2, materializedCandidate.UseSeed?.RemainingUses);
        Assert.Equal(rootSpend.Key, materializedCandidate.Producer);
        Assert.Equal(2, materializedCandidate.PlannedMutationKeys.Count);
        Assert.Empty(materializedCandidate.PendingOutputs);
        var suppressedReaction = Assert.Single(
            materializedCandidate.ReactionOutputs);
        Assert.Equal("trigger_component", suppressedReaction.ResultKind);
        Assert.Equal("after_component", suppressedReaction.Dependency);
        Assert.Equal(
            "component_budget_periodic",
            suppressedReaction.AfterComponentId);

        var predecessorKey = Assert.Single(
            materializedCandidate.PlannedComponentIdsByMutation,
            static component => string.Equals(
                component.Value,
                "component_budget_periodic",
                StringComparison.Ordinal)).Key;
        var childKey = Assert.Single(
            materializedCandidate.PlannedComponentIdsByMutation,
            static component => string.Equals(
                component.Value,
                "component_budget_dependent_periodic",
                StringComparison.Ordinal)).Key;
        var child = Assert.Single(
            materialized.Mutations,
            mutation => mutation.Key == childKey);
        Assert.Equal(new[] { predecessorKey }, child.Dependencies);
        var predecessorRequirement = Assert.Single(
            child.EventRequirements,
            requirement => requirement.Producer == predecessorKey);
        Assert.Equal("resource_restored", predecessorRequirement.EventKind);
        var producerRequirement = Assert.Single(
            child.EventRequirements,
            requirement => requirement.Producer == rootSpend.Key);
        Assert.Equal("resource_spent", producerRequirement.EventKind);

        var replaySeeded = SeedExactEffectMutationReplay(
            fixture,
            rootSpend,
            "component_budget_periodic");
        var result = BuildAcceptedEventBudgetPlan(
            replaySeeded,
            new[] { rootSpend });
        var resources = result.Resources;

        Assert.True(resources.IsValid, Format(resources.Issues));
        var accepted = Assert.Single(
            resources.EffectBoundaryTranscript.AcceptedActivations);
        Assert.Equal(
            "trigger_budget_unmet_predecessor",
            accepted.Activation.Stamp.Identity.TriggerId);
        Assert.Equal(2, accepted.Activation.Stamp.UsesBefore);
        Assert.Equal(1, accepted.Activation.UsesAfter);
        Assert.False(accepted.Activation.EffectTerminal);
        var execution = Assert.Single(resources.ResourceTriggerExecutions);
        Assert.Equal(2, execution.RemainingUseBudget);
        Assert.Empty(execution.MutationKeys);
        Assert.Empty(execution.ComponentIds ?? Array.Empty<string>());
        Assert.Single(
            resources.ReplayTransitions,
            static transition =>
                transition.Phase == ResourceMutationPhase.EffectTrigger);
        Assert.DoesNotContain(
            resources.AppliedTransitions,
            static transition =>
                transition.Phase == ResourceMutationPhase.EffectTrigger);
        Assert.Equal(
            8m,
            resources.StateAfterImage!.Entries.Single(entry =>
                entry.Coordinate == replaySeeded.Coordinate).Current);
        Assert.Empty(
            resources.EffectBoundaryTranscript.AppliedComponentEvidence);
        Assert.Empty(resources.EffectBoundaryTranscript.ReleasedReactions);
        Assert.Empty(resources.AcceptedReactionExecutions);
        Assert.Empty(resources.AcceptedPendingResolutions);
        Assert.Equal(0, resources.EffectBoundaryTranscript.ExpansionCount);
        Assert.True(resources.EffectBoundaryTranscript.IsComplete);

        var finalized = EffectAcceptedTurnPlanner.CompleteAcceptedBoundaryTranscript(
            replaySeeded.Plan,
            resources.EffectBoundaryTranscript,
            new EffectIdentityFactory());

        Assert.True(finalized.Success, Format(finalized.Issues));
        Assert.Equal(0, finalized.Plan!.ReactionExpansionCount);
        var active = Assert.Single(finalized.Plan.ActiveEffects);
        Assert.Equal(1, active["lifetime"]!["remainingUses"]!.GetValue<int>());
        var identityHistory = finalized.Plan.IdentityIndexAfterImage.ToJsonString();
        Assert.Contains(
            accepted.Activation.Stamp.Identity.EventRef,
            identityHistory,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            suppressedReaction.EventRef,
            identityHistory,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptedBoundary_AfterComponentSameStackReplaceRetiresOldEffectBeforeNestedResourceEvent()
    {
        const string oldEffectId = "effect_replace_nested_old";
        const string siblingEffectId = "effect_replace_nested_sibling";
        const string outerTriggerId = "trigger_replace_nested_outer";
        const string oldNestedTriggerId = "trigger_replace_nested_old_observer";
        const string siblingNestedTriggerId = "trigger_replace_nested_sibling_observer";
        const string replacementDefinitionKey = "replace_nested_replacement";
        var fixture = CreateAfterComponentReplacementNestedTraversalFixture();
        var rootDamage = CreateBudgetMutation(
            fixture.Coordinate,
            "turn_43:replace_nested:root_damage",
            ResourceOperation.Damage,
            amount: 2m);

        var result = BuildAcceptedEventBudgetPlan(fixture, new[] { rootDamage });

        Assert.True(result.Resources.IsValid, Format(result.Resources.Issues));
        var transcript = result.Resources.EffectBoundaryTranscript;
        var releasedReplacement = Assert.Single(
            transcript.ReleasedReactions,
            static release => string.Equals(
                release.Reaction.ResultKind,
                "apply_definition",
                StringComparison.Ordinal));
        Assert.Equal(EffectReactionReleaseStage.AfterComponent, releasedReplacement.Stage);
        Assert.Equal(oldEffectId, releasedReplacement.Reaction.EffectId);
        Assert.Equal(
            replacementDefinitionKey,
            releasedReplacement.Reaction.DownstreamSourceKey?.DefinitionKey);

        Assert.Contains(
            transcript.AcceptedActivations,
            activation => string.Equals(
                    activation.Activation.Stamp.Identity.EffectId,
                    oldEffectId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    activation.Activation.Stamp.Identity.TriggerId,
                    outerTriggerId,
                    StringComparison.Ordinal));
        Assert.Contains(
            transcript.AcceptedActivations,
            activation => string.Equals(
                    activation.Activation.Stamp.Identity.EffectId,
                    siblingEffectId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    activation.Activation.Stamp.Identity.TriggerId,
                    siblingNestedTriggerId,
                    StringComparison.Ordinal));
        Assert.DoesNotContain(
            transcript.AcceptedActivations,
            activation => string.Equals(
                    activation.Activation.Stamp.Identity.EffectId,
                    oldEffectId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    activation.Activation.Stamp.Identity.TriggerId,
                    oldNestedTriggerId,
                    StringComparison.Ordinal));
        var rejectedOldNested = Assert.Single(
            transcript.RejectedActivations,
            rejection => string.Equals(
                    rejection.Candidate.Activation.Identity.EffectId,
                    oldEffectId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    rejection.Candidate.Activation.Identity.TriggerId,
                    oldNestedTriggerId,
                    StringComparison.Ordinal));
        Assert.Equal(
            EffectActivationRejectionReason.EffectTerminal,
            rejectedOldNested.Reason);

        var finalized = Assert.IsType<EffectAcceptedTurnPlanningResult>(
            result.Finalized);
        Assert.True(finalized.Success, Format(finalized.Issues));
        var finalizedPlan = Assert.IsType<EffectAcceptedTurnPlan>(finalized.Plan);
        var activeEffects = finalizedPlan.ActiveEffects;
        var replacement = Assert.Single(activeEffects, effect => string.Equals(
            effect["source"]?["definitionKey"]?.GetValue<string>(),
            replacementDefinitionKey,
            StringComparison.Ordinal));
        var replacementEffectId = replacement["effectId"]!.GetValue<string>();
        Assert.NotEqual(oldEffectId, replacementEffectId);
        Assert.Contains(activeEffects, effect => string.Equals(
            effect["effectId"]?.GetValue<string>(),
            siblingEffectId,
            StringComparison.Ordinal));
        Assert.DoesNotContain(activeEffects, effect => string.Equals(
            effect["effectId"]?.GetValue<string>(),
            oldEffectId,
            StringComparison.Ordinal));
        Assert.DoesNotContain(
            transcript.AcceptedActivations,
            activation => string.Equals(
                activation.Activation.Stamp.Identity.EffectId,
                replacementEffectId,
                StringComparison.Ordinal));

        var nextInput = new EffectAcceptedTurnInput(
            "session_replace_nested_next",
            "snapshot_replace_nested_next",
            EffectMaterializationTestFixture.CreateCommandRoot(),
            finalizedPlan.SourceAuthority,
            finalizedPlan.TargetAuthority,
            new JsonObject
            {
                ["turn"] = 44,
                ["events"] = new JsonArray(new JsonObject
                {
                    ["kind"] = "accepted_turn",
                    ["authorityId"] = "turn_44",
                    ["eventRef"] = "turn_44:accepted_replace_nested"
                }),
                ["lifecycleEvents"] = new JsonArray(new JsonObject
                {
                    ["eventRef"] = "turn_44:lifecycle:replacement_owner_turn_end:1",
                    ["causalEventRef"] = "turn_44:replacement_owner_turn_end:1",
                    ["turn"] = 44,
                    ["phase"] = "owner_turn_end",
                    ["realm"] = "mortal_world",
                    ["target"] = new JsonObject
                    {
                        ["kind"] = "player",
                        ["targetId"] = "player_current"
                    },
                    ["effectId"] = replacementEffectId,
                    ["triggerId"] = "on_owner_turn_end"
                })
            },
            PreTurnCarriers: finalizedPlan.ResourceTriggerCarriers,
            PreTurnIdentityIndex: finalizedPlan.IdentityIndexAfterImage);
        var next = new EffectAcceptedTurnPlanCache().GetOrBuild(nextInput);

        Assert.True(next.Success, Format(next.Issues));
        var nextPlan = Assert.IsType<EffectAcceptedTurnPlan>(next.Plan);
        var nextDue = EffectAcceptedTurnPlanner.ResolveDuePeriodicResourceMutations(
            nextPlan,
            ResourceOwnerAuthority.CreateCurrentPlayerAuthority(
                fixture.Definitions),
            fixture.Definitions);

        Assert.True(nextDue.IsValid, Format(nextDue.Issues));
        var nextCandidate = Assert.Single(nextDue.TriggerCandidates);
        Assert.Equal(replacementEffectId, nextCandidate.Activation.Identity.EffectId);
        Assert.DoesNotContain(
            nextDue.TriggerCandidates,
            candidate => string.Equals(
                candidate.Activation.Identity.EffectId,
                oldEffectId,
                StringComparison.Ordinal));
    }

    [Fact]
    public void AcceptedEventArbiterBoundary_DeniedPendingCandidatePublishesNoOutput()
    {
        var fixture = CreateAcceptedEventBudgetFixture(
            remainingUses: 1,
            current: 5m,
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec(
                    "trigger_budget_10_accepted_mutation",
                    "resource_damaged",
                    Priority: 10,
                    IncludePeriodic: true,
                    IncludeReaction: false),
                new BudgetTriggerSpec(
                    "trigger_budget_20_denied_pending",
                    "resource_damaged",
                    Priority: 20,
                    IncludePeriodic: true,
                    IncludeReaction: false,
                    ResolutionMode: "bounded_receipt")
            });
        var rootDamage = CreateBudgetMutation(
            fixture.Coordinate,
            "turn_43:budget:denied_pending",
            ResourceOperation.Damage,
            amount: 1m);
        var materialized = EffectAcceptedTurnPlanner.ResolveResourceEventMutations(
            fixture.Plan,
            new ResourceAppliedEvent(
                "resource_damaged",
                "operation_budget_denied_pending_probe",
                rootDamage.EventRef,
                rootDamage.Coordinate,
                Before: 5m,
                After: 4m,
                AppliedAmount: 1m,
                Turn: 43,
                ExecutionSequence: 0,
                SourceFingerprint: FingerprintB),
            rootDamage.Key,
            fixture.Owners,
            fixture.Definitions);

        Assert.True(materialized.IsValid, Format(materialized.Issues));
        var materializedPendingCandidate = Assert.Single(
            materialized.TriggerCandidates,
            static candidate => string.Equals(
                candidate.Activation.Identity.TriggerId,
                "trigger_budget_20_denied_pending",
                StringComparison.Ordinal));
        Assert.Empty(materializedPendingCandidate.PlannedMutationKeys);
        Assert.Empty(materializedPendingCandidate.ReactionOutputs);
        Assert.Single(materializedPendingCandidate.PendingOutputs);

        var result = BuildAcceptedEventBudgetPlan(
            fixture,
            new[] { rootDamage });

        Assert.True(result.Resources.IsValid, Format(result.Resources.Issues));
        var accepted = Assert.Single(
            result.Resources.EffectBoundaryTranscript.AcceptedActivations);
        Assert.Equal(
            "trigger_budget_10_accepted_mutation",
            accepted.Activation.Stamp.Identity.TriggerId);
        var rejected = Assert.Single(
            result.Resources.EffectBoundaryTranscript.RejectedActivations);
        Assert.Equal(
            "trigger_budget_20_denied_pending",
            rejected.Candidate.Activation.Identity.TriggerId);
        Assert.Single(rejected.Candidate.PendingOutputs);
        Assert.Empty(rejected.Candidate.PlannedMutationKeys);
        Assert.Empty(rejected.Candidate.ReactionOutputs);
        Assert.Empty(result.Resources.AcceptedPendingResolutions);
        Assert.Empty(result.Resources.AcceptedReactionExecutions);
        Assert.Empty(result.Resources.EffectBoundaryTranscript.ReleasedReactions);
        Assert.Null(
            result.Resources.EffectBoundaryTranscript.PendingFrontierBoundaryOrdinal);
        Assert.True(result.Resources.EffectBoundaryTranscript.IsComplete);
        Assert.Equal(0, result.Resources.EffectBoundaryTranscript.ExpansionCount);
        Assert.Single(
            result.Resources.AppliedTransitions,
            static transition =>
                transition.Phase == ResourceMutationPhase.EffectTrigger);
        Assert.Equal(
            "trigger_budget_10_accepted_mutation",
            Assert.Single(result.Resources.ResourceTriggerExecutions).TriggerId);

        var finalized = Assert.IsType<EffectAcceptedTurnPlanningResult>(
            result.Finalized);
        Assert.True(finalized.Success, Format(finalized.Issues));
        Assert.Empty(finalized.Plan!.ActiveEffects);
        Assert.Equal(0, finalized.Plan.ReactionExpansionCount);
        var identityHistory = finalized.Plan.IdentityIndexAfterImage.ToJsonString();
        Assert.Contains(
            accepted.Activation.Stamp.Identity.EventRef,
            identityHistory,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            rejected.Candidate.Activation.Identity.EventRef,
            identityHistory,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "trigger_budget_20_denied_pending",
            identityHistory,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("before_current_event")]
    [InlineData("after_current_event")]
    public void AcceptedEventArbiterBoundary_ReplayedChildPreservesUnconditionalBoundaryReaction(
        string reactionDependency)
    {
        var fixture = CreateAcceptedEventBudgetFixture(
            remainingUses: 1,
            current: 10m,
            reactionDependency: reactionDependency,
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec(
                    "trigger_budget_replayed_boundary",
                    "resource_spent",
                    Priority: 10,
                    IncludePeriodic: true,
                    IncludeReaction: true)
            });
        var producerBaseline = AddBudgetProducerResource(fixture);
        fixture = producerBaseline.Fixture;
        var rootSpend = CreateBudgetMutation(
            producerBaseline.Coordinate,
            "turn_43:budget:replayed_boundary:" + reactionDependency,
            ResourceOperation.Spend,
            amount: 2m);
        var replaySeeded = SeedExactEffectMutationReplay(fixture, rootSpend);

        var result = BuildAcceptedEventBudgetPlan(
            replaySeeded,
            new[] { rootSpend });

        Assert.True(result.Resources.IsValid, Format(result.Resources.Issues));
        Assert.Single(
            result.Resources.ReplayTransitions,
            static transition =>
                transition.Phase == ResourceMutationPhase.EffectTrigger);
        var execution = Assert.Single(result.Resources.ResourceTriggerExecutions);
        Assert.Equal(1, execution.RemainingUseBudget);
        Assert.Empty(execution.MutationKeys);
        Assert.Empty(execution.ComponentIds ?? Array.Empty<string>());
        var released = Assert.Single(
            result.Resources.EffectBoundaryTranscript.ReleasedReactions);
        Assert.Single(result.Resources.AcceptedReactionExecutions);
        Assert.Empty(result.Resources.AcceptedPendingResolutions);

        var finalized = Assert.IsType<EffectAcceptedTurnPlanningResult>(
            result.Finalized);
        Assert.True(finalized.Success, Format(finalized.Issues));
        Assert.Equal(1, finalized.Plan!.ReactionExpansionCount);
        Assert.Contains(
            released.Reaction.EventRef,
            finalized.Plan.IdentityIndexAfterImage.ToJsonString(),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("before_current_event")]
    [InlineData("after_current_event")]
    public void AcceptedEventArbiterBoundary_ZeroAppliedChildPreservesUnconditionalBoundaryReaction(
        string reactionDependency)
    {
        var fixture = CreateAcceptedEventBudgetFixture(
            remainingUses: 1,
            current: 10m,
            reactionDependency: reactionDependency,
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec(
                    "trigger_budget_zero_applied_boundary",
                    "resource_spent",
                    Priority: 10,
                    IncludePeriodic: true,
                    IncludeReaction: true)
            });
        var producerBaseline = AddBudgetProducerResource(fixture);
        fixture = producerBaseline.Fixture;
        var rootSpend = CreateBudgetMutation(
            producerBaseline.Coordinate,
            "turn_43:budget:zero_applied_boundary:" + reactionDependency,
            ResourceOperation.Spend,
            amount: 2m);

        var result = BuildAcceptedEventBudgetPlan(fixture, new[] { rootSpend });

        Assert.True(result.Resources.IsValid, Format(result.Resources.Issues));
        Assert.Single(
            result.Resources.AppliedTransitions,
            static transition =>
                transition.Phase == ResourceMutationPhase.EffectTrigger &&
                transition.AppliedAmount == 0m);
        var execution = Assert.Single(result.Resources.ResourceTriggerExecutions);
        Assert.Empty(execution.MutationKeys);
        Assert.Empty(execution.ComponentIds ?? Array.Empty<string>());
        var released = Assert.Single(
            result.Resources.EffectBoundaryTranscript.ReleasedReactions);
        Assert.Single(result.Resources.AcceptedReactionExecutions);
        Assert.Empty(result.Resources.AcceptedPendingResolutions);

        var finalized = Assert.IsType<EffectAcceptedTurnPlanningResult>(
            result.Finalized);
        Assert.True(finalized.Success, Format(finalized.Issues));
        Assert.Equal(1, finalized.Plan!.ReactionExpansionCount);
        Assert.Contains(
            released.Reaction.EventRef,
            finalized.Plan.IdentityIndexAfterImage.ToJsonString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptedEventArbiterBoundary_DueLifecycleReactionOnlyConsumesUseOnceAcrossFinalize()
    {
        var fixture = CreateAcceptedEventBudgetFixture(
            remainingUses: 2,
            current: 10m,
            reactionResultKind: "event_outcome",
            includeExactLifecycleEvent: true,
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec(
                    "trigger_budget_due_reaction_only",
                    "owner_critical_failure",
                    Priority: 10,
                    IncludePeriodic: false,
                    IncludeReaction: true)
            });

        var due = EffectAcceptedTurnPlanner.ResolveDuePeriodicResourceMutations(
            fixture.Plan,
            fixture.Owners,
            fixture.Definitions);

        Assert.True(due.IsValid, Format(due.Issues));
        Assert.Empty(due.Mutations);
        Assert.Empty(due.PendingResolutions);
        var candidate = Assert.Single(due.TriggerCandidates);
        Assert.True(candidate.Activation.ConsumesUse);
        Assert.Equal(2, candidate.UseSeed?.RemainingUses);
        Assert.Null(candidate.Producer);
        Assert.Empty(candidate.PlannedMutationKeys);
        Assert.Single(candidate.ReactionOutputs);
        var execution = Assert.Single(due.TriggerExecutions);
        Assert.Equal("trigger_budget_due_reaction_only", execution.TriggerId);
        Assert.Equal(2, execution.RemainingUseBudget);
        Assert.Empty(execution.MutationKeys);
        var deferredReaction = Assert.Single(fixture.Plan.DeferredReactions);

        var sourceResult = ResourceMutationSourceCatalog.Create(
            fixture.Sources.Exports.Concat(due.SourceExports));
        Assert.Empty(sourceResult.Issues);
        var resources = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 43,
                Definitions: fixture.Definitions,
                State: fixture.State,
                History: fixture.History,
                Sources: Assert.IsType<ResourceMutationSourceCatalog>(
                    sourceResult.Catalog),
                Mutations: due.Mutations,
                InitialTriggerCandidates: due.TriggerCandidates,
                InitialEffectResolutionWork: due.Work,
                EffectPlanAuthority:
                    AcceptedMechanicsPlanner.CreateEffectPlanAuthority(
                        fixture.Plan)),
            IdentityFactory());
        Assert.True(resources.IsValid, Format(resources.Issues));
        var releasedReaction = Assert.Single(
            resources.EffectBoundaryTranscript.ReleasedReactions);
        Assert.Equal(deferredReaction.EventRef, releasedReaction.Reaction.EventRef);

        var finalized = EffectAcceptedTurnPlanner.CompleteAcceptedBoundaryTranscript(
            fixture.Plan,
            resources.EffectBoundaryTranscript,
            new EffectIdentityFactory());

        Assert.True(finalized.Success, Format(finalized.Issues));
        var active = Assert.Single(finalized.Plan!.ActiveEffects);
        Assert.Equal(
            1,
            active["lifetime"]?["remainingUses"]?.GetValue<int>());
        var identityEntry = Assert.Single(finalized.Plan.IdentityIndexAfterImage["entries"]!
            .AsArray()
            .OfType<JsonObject>());
        var transitions = identityEntry["transitions"]!.AsArray()
            .OfType<JsonObject>()
            .ToArray();
        Assert.Single(transitions, transition =>
            string.Equals(
                transition["eventRef"]?.GetValue<string>(),
                execution.EventRef,
                StringComparison.Ordinal));
        Assert.Single(transitions, transition =>
            string.Equals(
                transition["eventRef"]?.GetValue<string>(),
                deferredReaction.EventRef,
                StringComparison.Ordinal));
        Assert.DoesNotContain(transitions, transition =>
            string.Equals(
                transition["eventRef"]?.GetValue<string>(),
                "turn_43:budget:due_lifecycle:effect_accepted_event_budget",
                StringComparison.Ordinal));
    }

    [Fact]
    public void AcceptedEventArbiterBoundary_ExpansionIsChargedOnlyForReleasedLifecycleReaction()
    {
        var fixture = CreateAcceptedEventBudgetFixture(
            remainingUses: 1,
            current: 10m,
            reactionResultKind: "remove",
            includeExactLifecycleEvent: true,
            exactLifecycleEventCount: 2,
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec(
                    "trigger_budget_release_expansion",
                    "owner_critical_failure",
                    Priority: 10,
                    IncludePeriodic: false,
                    IncludeReaction: true)
            });
        var due = EffectAcceptedTurnPlanner.ResolveDuePeriodicResourceMutations(
            fixture.Plan,
            fixture.Owners,
            fixture.Definitions);
        Assert.True(due.IsValid, Format(due.Issues));
        Assert.Equal(2, due.TriggerCandidates.Count);
        Assert.Equal(
            2,
            due.TriggerCandidates.Sum(static candidate =>
                candidate.ReactionOutputs.Count));

        var sources = ResourceMutationSourceCatalog.Create(
            fixture.Sources.Exports.Concat(due.SourceExports));
        Assert.Empty(sources.Issues);
        var resources = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 43,
                Definitions: fixture.Definitions,
                State: fixture.State,
                History: fixture.History,
                Sources: Assert.IsType<ResourceMutationSourceCatalog>(
                    sources.Catalog),
                Mutations: due.Mutations,
                InitialTriggerCandidates: due.TriggerCandidates,
                InitialEffectResolutionWork: due.Work,
                EffectPlanAuthority:
                    AcceptedMechanicsPlanner.CreateEffectPlanAuthority(
                        fixture.Plan)),
            IdentityFactory());

        Assert.True(resources.IsValid, Format(resources.Issues));
        Assert.Single(resources.EffectBoundaryTranscript.AcceptedActivations);
        Assert.Single(resources.EffectBoundaryTranscript.ReleasedReactions);
        Assert.Equal(1, resources.EffectBoundaryTranscript.ExpansionCount);
        var finalized = EffectAcceptedTurnPlanner.CompleteAcceptedBoundaryTranscript(
            fixture.Plan,
            resources.EffectBoundaryTranscript,
            new EffectIdentityFactory());
        Assert.True(finalized.Success, Format(finalized.Issues));
        Assert.Empty(finalized.Plan!.ActiveEffects);
        Assert.Equal(1, finalized.Plan.ReactionExpansionCount);
    }

    [Theory]
    [InlineData("remove", "suspend")]
    [InlineData("suspend", "remove")]
    [InlineData("remove", "remove")]
    public void AcceptedEventArbiterBoundary_RemovePreservesFrozenTerminalSiblingsAndWinsTerminalFold(
        string firstResultKind,
        string secondResultKind)
    {
        var fixture = CreateAcceptedEventBudgetFixture(
            remainingUses: 2,
            current: 10m,
            reactionResultKind: firstResultKind,
            siblingReactionResultKind: secondResultKind,
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec(
                    "trigger_budget_terminal_siblings",
                    "resource_damaged",
                    Priority: 10,
                    IncludePeriodic: false,
                    IncludeReaction: true)
            });
        var rootDamage = CreateBudgetMutation(
            fixture.Coordinate,
            "turn_43:budget:terminal_siblings",
            ResourceOperation.Damage,
            amount: 2m);

        var result = BuildAcceptedEventBudgetPlan(
            fixture,
            new[] { rootDamage });

        Assert.True(result.Resources.IsValid, Format(result.Resources.Issues));
        Assert.Equal(
            new[] { firstResultKind, secondResultKind },
            result.AcceptedReactions.Select(static reaction =>
                reaction.ResultKind));
        var finalized = Assert.IsType<EffectAcceptedTurnPlanningResult>(
            result.Finalized);
        Assert.True(finalized.Success, Format(finalized.Issues));
        Assert.Empty(finalized.Plan!.ActiveEffects);
        var identityEntry = Assert.Single(finalized.Plan.IdentityIndexAfterImage
            ["entries"]!.AsArray().OfType<JsonObject>());
        Assert.Equal("removed", identityEntry["state"]?.GetValue<string>());
        var transitions = identityEntry["transitions"]!.AsArray()
            .OfType<JsonObject>()
            .ToArray();
        var activation = Assert.Single(
            result.Resources.ResourceTriggerExecutions);
        Assert.Contains(transitions, transition => string.Equals(
            transition["eventRef"]?.GetValue<string>(),
            activation.EventRef,
            StringComparison.Ordinal));
        var winningRemove = result.AcceptedReactions.First(reaction =>
            string.Equals(
                reaction.ResultKind,
                "remove",
                StringComparison.Ordinal));
        var terminal = Assert.Single(transitions, transition =>
            string.Equals(
                transition["kind"]?.GetValue<string>(),
                "remove",
                StringComparison.Ordinal));
        Assert.Equal(
            winningRemove.EventRef,
            terminal["eventRef"]?.GetValue<string>());
    }

    [Fact]
    public void Planner_CrossTriggerReactionIdentityIsExactAndExpansionIsFullyCounted()
    {
        var result = ResolveOneEffectManySameEventTriggers(
            triggerCount: 32,
            reverseTriggers: false,
            lifecycleTriggerId: null,
            remainingUses: 32,
            includeReaction: true,
            reactionMaxExpansion: 1);

        Assert.True(
            result.ResourceResolution.IsValid,
            Format(result.ResourceResolution.Issues));
        var reactions = result.ResourceResolution.TriggerCandidates
            .SelectMany(static candidate => candidate.ReactionOutputs)
            .ToArray();
        Assert.Equal(32, reactions.Length);
        Assert.Empty(result.ResourceResolution.ReactionExecutions);
        Assert.Equal(
            32,
            reactions
                .Select(static reaction => reaction.EventRef)
                .Distinct(StringComparer.Ordinal)
                .Count());

        var baseline = CreateBudgetResourceBaseline(
            result.Definitions,
            result.Coordinate,
            current: 10m);
        var rootDamage = CreateBudgetMutation(
            result.Coordinate,
            "turn_43:many_runtime_producer",
            ResourceOperation.Damage,
            amount: 1m);
        var resources = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 43,
                Definitions: result.Definitions,
                State: baseline.State,
                History: baseline.History,
                Sources: baseline.Sources,
                Mutations: new[] { rootDamage },
                EventMutationResolver: (resourceEvent, producer) =>
                    EffectAcceptedTurnPlanner.ResolveResourceEventMutations(
                        result.Plan,
                        resourceEvent,
                        producer,
                        result.Owners,
                        result.Definitions),
                EffectPlanAuthority:
                    AcceptedMechanicsPlanner.CreateEffectPlanAuthority(
                        result.Plan)),
            IdentityFactory());

        Assert.False(resources.IsValid);
        Assert.Contains(
            resources.Issues,
            static issue => string.Equals(
                issue.Code,
                "effect_reaction_expansion_exceeded",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            resources.Issues,
            static issue => string.Equals(
                issue.Code,
                "effect_reaction_event_duplicate",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Planner_InvalidIndexedTriggerFailsClosedBeforeHotTraversal()
    {
        EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution? resolution = null;

        var exception = Record.Exception(() =>
        {
            var result = ResolveOneEffectManySameEventTriggers(
                triggerCount: 1,
                reverseTriggers: false,
                lifecycleTriggerId: null,
                remainingUses: 1,
                includeReaction: false,
                corruptCanonicalTrigger: true,
                scaleSourceDefinition: false);
            resolution = result.ResourceResolution;
        });

        Assert.Null(exception);
        Assert.NotNull(resolution);
        Assert.False(resolution!.IsValid);
        Assert.Contains(
            resolution.Issues,
            static issue => string.Equals(
                issue.Code,
                "effect_materialization_invalid_field",
                StringComparison.Ordinal));
        Assert.Empty(resolution.Mutations);
        Assert.Empty(resolution.ReactionExecutions);
    }

    [Fact]
    public void Plan_HotRoutingDescriptorIsPrivateAndCannotLeakSharedJson()
    {
        var method = typeof(EffectAcceptedTurnPlanner.IndexedResourceTrigger)
            .GetMethod(
                "OpenRoutingDescriptor",
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);

        Assert.NotNull(method);
        Assert.True(method!.IsPrivate);
        Assert.Null(typeof(EffectAcceptedTurnPlanner.IndexedResourceTrigger)
            .GetMethod(
                "CloneOccurrenceForRouting",
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic));
    }

    [Fact]
    public void Plan_ResourceTriggerIndexPreservesFullOrderAndExactFilteredOrder()
    {
        var forward = CreateOrderingTriggerIndex(reverseCarriers: false);
        var reversed = CreateOrderingTriggerIndex(reverseCarriers: true);
        var target = new EffectTargetKey(
            "mortal_world",
            "npc",
            "npc_trigger_order");
        var expected = new[]
        {
            "effect_trigger_order_a/10/trigger_z",
            "effect_trigger_order_a/20/trigger_a",
            "effect_trigger_order_b/5/trigger_y",
            "effect_trigger_order_b/30/trigger_b"
        };

        Assert.Empty(forward.Issues);
        Assert.Empty(reversed.Issues);
        var forwardGeneral = forward.ResolveLifecycleEvent(
            target,
            "owner_turn_end");
        var reversedGeneral = reversed.ResolveLifecycleEvent(
            target,
            "owner_turn_end");
        Assert.Equal(expected, DescribeIndexedTriggers(forwardGeneral));
        Assert.Equal(expected, DescribeIndexedTriggers(reversedGeneral));

        foreach (var effectId in new[]
                 {
                     "effect_trigger_order_a",
                     "effect_trigger_order_b"
                 })
        {
            var filteredGeneral = forwardGeneral
                .Where(candidate => string.Equals(
                    candidate.EffectId,
                    effectId,
                    StringComparison.Ordinal));
            Assert.Equal(
                DescribeIndexedTriggers(filteredGeneral),
                DescribeIndexedTriggers(forward.ResolveLifecycleEvent(
                    target,
                    "owner_turn_end",
                    effectId)));
            Assert.Equal(
                DescribeIndexedTriggers(filteredGeneral),
                DescribeIndexedTriggers(reversed.ResolveLifecycleEvent(
                    target,
                    "owner_turn_end",
                    effectId)));
        }
    }

    [Fact]
    public void Plan_ResourceTriggerIndexIssuesAreReadOnly()
    {
        var index = EffectAcceptedTurnPlanner.CreateResourceTriggerIndex(
            new EffectCarrierCatalogInput(
                null,
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["entries"] = new JsonArray(new JsonObject
                    {
                        ["NPCId"] = "npc_invalid_trigger_index",
                        ["activeEffects"] = "invalid"
                    })
                },
                null,
                null,
                null,
                null),
            CreateNpcTargetAuthority(
                "npc_invalid_trigger_index"));

        var issues = index.Issues;
        Assert.NotEmpty(issues);
        var mutableView = Assert.IsAssignableFrom<IList<ValidationIssue>>(issues);
        Assert.Throws<NotSupportedException>(() => mutableView[0] = issues[0]);
    }

    [Fact]
    public void Plan_SourceBindingPredicatesAreDefensiveOrdinalCopies()
    {
        var originalPredicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "source_ready"
        };
        var routed = PlanRealEffectTriggers(1, originalPredicates);
        var key = new EffectSourceKey(
            "mortal_world",
            "wound",
            "wound_trigger_scale",
            EffectMaterializationTestFixture.DefinitionKey);

        originalPredicates.Add("tampered_original");
        var listPredicates = Assert.IsType<HashSet<string>>(
            Assert.Single(routed.Plan.SourceBindings).SatisfiedPredicates);
        var listUsesOrdinalComparison = listPredicates.Add("SOURCE_READY");
        listPredicates.Add("tampered_list");
        Assert.True(routed.Plan.TryResolveSourceBinding(key, out var resolved));
        var resolvedPredicates = Assert.IsType<HashSet<string>>(
            resolved!.SatisfiedPredicates);
        var resolvedUsesOrdinalComparison = resolvedPredicates.Add("SOURCE_READY");
        resolvedPredicates.Add("tampered_resolve");

        Assert.True(listUsesOrdinalComparison);
        Assert.True(resolvedUsesOrdinalComparison);
        Assert.Equal(
            new[] { "source_ready" },
            Assert.Single(routed.Plan.SourceBindings).SatisfiedPredicates
                .OrderBy(static value => value, StringComparer.Ordinal));
        Assert.True(routed.Plan.TryResolveSourceBinding(key, out var repeated));
        Assert.Equal(
            new[] { "source_ready" },
            repeated!.SatisfiedPredicates
                .OrderBy(static value => value, StringComparer.Ordinal));
    }

    [Fact]
    public void ResourceMutationSourceCatalog_NullRequestPreservesArgumentNullContract()
    {
        var baseline = CreateBaseline(1);
        Assert.True(baseline.Definitions.TryResolveExact(
            baseline.Coordinate.ResourceKey,
            out var definition));

        var exception = Assert.Throws<ArgumentNullException>(() =>
            baseline.Sources.Resolve(null!, definition!));

        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public void BuildResources_AggregatesInitialLifecycleAndGraphRoutingWorkOnSuccess()
    {
        var baseline = CreateBaseline(4);
        var initialWork = new EffectAcceptedTurnPlanner.EffectResourceResolutionWork(
            IndexLookupCount: 2,
            CandidateVisitCount: 3,
            SourceBindingIndexLookupCount: 5,
            SourceBindingCandidateVisitCount: 7,
            RoutingDescriptorAccessCount: 9,
            OccurrenceCloneCount: 11,
            FullEffectValidationPassCount: 13,
            TriggerArrayVisitCount: 17,
            ComponentIndexLookupCount: 19,
            SelectedComponentVisitCount: 23,
            PendingCandidateFingerprintOutputVisitCount: 47);
        var graphWork = new EffectAcceptedTurnPlanner.EffectResourceResolutionWork(
            IndexLookupCount: 1,
            CandidateVisitCount: 4,
            SourceBindingIndexLookupCount: 6,
            SourceBindingCandidateVisitCount: 8,
            RoutingDescriptorAccessCount: 27,
            OccurrenceCloneCount: 29,
            FullEffectValidationPassCount: 31,
            TriggerArrayVisitCount: 37,
            ComponentIndexLookupCount: 41,
            SelectedComponentVisitCount: 43,
            PendingCandidateFingerprintOutputVisitCount: 53);
        var resolverCalls = 0;

        var result = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 2,
                Definitions: baseline.Definitions,
                State: baseline.State,
                History: baseline.History,
                Sources: baseline.Sources,
                Mutations: new[]
                {
                    CreateScaleMutation(
                        baseline.Coordinate,
                        "turn_2:aggregate_routing_success")
                },
                EventMutationResolver: (_, _) =>
                {
                    resolverCalls++;
                    return EmptyResolution(graphWork);
                },
                InitialEffectResolutionWork: initialWork),
            IdentityFactory());

        Assert.True(result.IsValid, Format(result.Issues));
        Assert.Equal(2, resolverCalls);
        Assert.Equal(4, result.Statistics.EffectTriggerIndexLookupCount);
        Assert.Equal(11, result.Statistics.EffectTriggerCandidateVisitCount);
        Assert.Equal(17, result.Statistics.EffectSourceBindingIndexLookupCount);
        Assert.Equal(23, result.Statistics.EffectSourceBindingCandidateVisitCount);
        Assert.Equal(63, result.Statistics.EffectRoutingDescriptorAccessCount);
        Assert.Equal(69, result.Statistics.EffectOccurrenceCloneCount);
        Assert.Equal(75, result.Statistics.EffectFullValidationPassCount);
        Assert.Equal(91, result.Statistics.EffectTriggerArrayVisitCount);
        Assert.Equal(101, result.Statistics.EffectComponentIndexLookupCount);
        Assert.Equal(109, result.Statistics.EffectSelectedComponentVisitCount);
        Assert.Equal(
            153,
            result.Statistics.PendingCandidateFingerprintOutputVisitCount);
        Assert.Equal(1, result.Statistics.SourceAuthoritySeedCount);
        Assert.Equal(1, result.Statistics.SourceAuthorityFreezeCount);
    }

    [Fact]
    public void BuildResources_PreservesAccumulatedRoutingWorkWhenResolverFails()
    {
        var baseline = CreateBaseline(4);
        var initialWork = new EffectAcceptedTurnPlanner.EffectResourceResolutionWork(
            IndexLookupCount: 2,
            CandidateVisitCount: 3,
            SourceBindingIndexLookupCount: 5,
            SourceBindingCandidateVisitCount: 7,
            RoutingDescriptorAccessCount: 9,
            OccurrenceCloneCount: 11,
            FullEffectValidationPassCount: 13,
            TriggerArrayVisitCount: 17,
            ComponentIndexLookupCount: 19,
            SelectedComponentVisitCount: 23,
            PendingCandidateFingerprintOutputVisitCount: 47);
        var failureWork = new EffectAcceptedTurnPlanner.EffectResourceResolutionWork(
            IndexLookupCount: 11,
            CandidateVisitCount: 13,
            SourceBindingIndexLookupCount: 17,
            SourceBindingCandidateVisitCount: 19,
            RoutingDescriptorAccessCount: 27,
            OccurrenceCloneCount: 29,
            FullEffectValidationPassCount: 31,
            TriggerArrayVisitCount: 37,
            ComponentIndexLookupCount: 41,
            SelectedComponentVisitCount: 43,
            PendingCandidateFingerprintOutputVisitCount: 53);
        var failure = new ValidationIssue(
            "effect.resourceTriggers",
            IssueSeverity.Error,
            "Forced routing failure.",
            code: "forced_effect_routing_failure");

        var result = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 2,
                Definitions: baseline.Definitions,
                State: baseline.State,
                History: baseline.History,
                Sources: baseline.Sources,
                Mutations: new[]
                {
                    CreateScaleMutation(
                        baseline.Coordinate,
                        "turn_2:aggregate_routing_failure")
                },
                EventMutationResolver: (_, _) =>
                    new EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution(
                        Array.Empty<ResourceMutationSourceExport>(),
                        Array.Empty<ResourceMutationIntent>(),
                        new[] { failure })
                    {
                        Work = failureWork
                    },
                InitialEffectResolutionWork: initialWork),
            IdentityFactory());

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "forced_effect_routing_failure");
        Assert.Equal(13, result.Statistics.EffectTriggerIndexLookupCount);
        Assert.Equal(16, result.Statistics.EffectTriggerCandidateVisitCount);
        Assert.Equal(22, result.Statistics.EffectSourceBindingIndexLookupCount);
        Assert.Equal(26, result.Statistics.EffectSourceBindingCandidateVisitCount);
        Assert.Equal(36, result.Statistics.EffectRoutingDescriptorAccessCount);
        Assert.Equal(40, result.Statistics.EffectOccurrenceCloneCount);
        Assert.Equal(44, result.Statistics.EffectFullValidationPassCount);
        Assert.Equal(54, result.Statistics.EffectTriggerArrayVisitCount);
        Assert.Equal(60, result.Statistics.EffectComponentIndexLookupCount);
        Assert.Equal(66, result.Statistics.EffectSelectedComponentVisitCount);
        Assert.Equal(
            100,
            result.Statistics.PendingCandidateFingerprintOutputVisitCount);
        Assert.Equal(1, result.Statistics.SourceAuthoritySeedCount);
        Assert.Equal(0, result.Statistics.SourceAuthorityFreezeCount);
    }

    private static AcceptedMechanicsResourcePlanningResult PlanExpandingTriggers(
        int count)
    {
        var baseline = CreateBaseline(count);
        var mutations = Enumerable.Range(0, count)
            .Select(index => new ResourceMutationIntent(
                EventRef: "turn_2:trigger_scale_spend:" + Suffix(index),
                Coordinate: baseline.Coordinate,
                Amount: 1m,
                Source: new ResourceMutationSourceRequest(
                    "registered_system_outcome",
                    "trigger_scale_system",
                    ResourceOperation.Spend),
                Dependencies: Array.Empty<ResourceOperationKey>(),
                EventRequirements: Array.Empty<ResourceMutationEventRequirement>(),
                ReceiptId: null))
            .ToArray();

        EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution Resolve(
            ResourceAppliedEvent resourceEvent,
            ResourceOperationKey producer)
        {
            if (!string.Equals(
                    resourceEvent.EventKind,
                    "resource_spent",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    producer.OriginKind,
                    "registered_system_outcome",
                    StringComparison.Ordinal))
            {
                return EmptyResolution(candidateVisits: 0);
            }

            var suffix = producer.EventRef[(producer.EventRef.LastIndexOf(':') + 1)..];
            var sourceId = "effect_component_trigger_scale_" + suffix;
            var source = new ResourceMutationSourceExport(
                "effect_component",
                sourceId,
                FingerprintB,
                ResourceMutationSourceState.Active,
                SameTurn: false,
                new ResourceOwnerKey(
                    baseline.Coordinate.Realm,
                    baseline.Coordinate.OwnerKind,
                    baseline.Coordinate.ResourceOwnerId));
            var mutation = new ResourceMutationIntent(
                EventRef: "turn_2:trigger_scale_gain:" + suffix,
                Coordinate: baseline.Coordinate,
                Amount: 1m,
                Source: new ResourceMutationSourceRequest(
                    source.SourceKind,
                    source.SourceId,
                    ResourceOperation.Gain),
                Dependencies: Array.Empty<ResourceOperationKey>(),
                EventRequirements: new[]
                {
                    new ResourceMutationEventRequirement(
                        producer,
                        resourceEvent.EventKind)
                },
                ReceiptId: null);
            var componentMap = new Dictionary<ResourceOperationKey, string>
            {
                [mutation.Key] = "component_trigger_scale"
            };
            var activation = new EffectActivationCandidateIdentity(
                "effect_trigger_scale_" + suffix,
                "on_resource_spent",
                resourceEvent.EventKind,
                mutation.EventRef,
                producer.EventRef);
            return new EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution(
                new[] { source },
                new[] { mutation },
                Array.Empty<ValidationIssue>())
            {
                TriggerCandidates = new[]
                {
                    new EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate(
                        new EffectActivationCandidate(
                            activation,
                            Priority: 100,
                            ConsumesUse: false,
                            EffectAuthority: new ResourcePendingAuthorityBinding(
                                "permanent",
                                activation.EffectId)),
                        useSeed: null,
                        producer,
                        new[] { mutation.Key },
                        new[] { "component_trigger_scale" },
                        componentMap,
                        Array.Empty<EffectAcceptedTurnPlanner
                            .EffectBoundedResourceResolution>(),
                        Array.Empty<EffectReactionExecution>(),
                        new EffectAcceptedTurnPlanner
                            .EffectResourceCandidateOrigin(
                                new[] { mutation },
                                new[] { "component_trigger_scale" },
                                componentMap,
                                new[] { source }))
                },
                TriggerExecutions = new[]
                {
                    new EffectAcceptedTurnPlanner.EffectResourceTriggerExecution(
                        "effect_trigger_scale_" + suffix,
                        "on_resource_spent",
                        resourceEvent.EventKind,
                        mutation.EventRef,
                        new[] { mutation.Key },
                        RemainingUseBudget: null,
                        ComponentIds: new[] { "component_trigger_scale" },
                        TriggerEventRef: producer.EventRef,
                        ComponentIdsByMutation: componentMap)
                },
                Work = new EffectAcceptedTurnPlanner.EffectResourceResolutionWork(
                    IndexLookupCount: 1,
                    CandidateVisitCount: 1)
            };
        }

        return AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 2,
                Definitions: baseline.Definitions,
                State: baseline.State,
                History: baseline.History,
                Sources: baseline.Sources,
                Mutations: mutations,
                EventMutationResolver: Resolve),
            IdentityFactory());
    }

    private static EffectAcceptedTurnPlanner.EffectResourceTriggerIndex
        CreateManyTriggerIndex(int triggerCount)
    {
        var npcId = "npc_many_trigger_scale";
        var effect = CreateIndexedEffect(
            "effect_many_trigger_scale",
            npcId,
            Enumerable.Range(0, triggerCount)
                .Select(index => CreateIndexedTrigger(
                    "trigger_scale_" + Suffix(index),
                    priority: index))
                .ToArray());
        return EffectAcceptedTurnPlanner.CreateResourceTriggerIndex(
            CreateNpcCarriers(npcId, new[] { effect }),
            CreateNpcTargetAuthority(npcId));
    }

    private static EffectAcceptedTurnPlanner.EffectResourceTriggerIndex
        CreateAmbiguousReplacementTargetIndex(int effectCount)
    {
        const string npcId = "npc_replacement_target_scale";
        var effects = Enumerable.Range(0, effectCount)
            .Select(index =>
            {
                var suffix = Suffix(index);
                var effect = CreateIndexedEffect(
                    "effect_replacement_target_scale_" + suffix,
                    npcId,
                    new[]
                    {
                        CreateIndexedTrigger(
                            "trigger_replacement_target_scale_" + suffix,
                            priority: index)
                    });
                effect["source"]!["sourceId"] =
                    "wound_replacement_target_scale";
                effect["stacking"]!["stackKey"] =
                    "replacement-target-shared-coordinate";
                effect["stacking"]!["policy"] = "independent";
                effect["stacking"]!["maxStacks"] = 1;
                return effect;
            })
            .ToArray();
        return EffectAcceptedTurnPlanner.CreateResourceTriggerIndex(
            CreateNpcCarriers(npcId, effects),
            CreateNpcTargetAuthority(npcId));
    }

    private static OneEffectManyTriggerResolutionResult
        ResolveOneEffectManySameEventTriggers(
        int triggerCount,
        bool reverseTriggers,
        string? lifecycleTriggerId,
        int remainingUses,
        bool includeReaction,
        int? reactionMaxExpansion = null,
        bool corruptCanonicalTrigger = false,
        bool scaleSourceDefinition = true,
        string reactionResultKind = "apply_definition",
        bool includePeriodic = true)
    {
        const string npcId = "npc_many_runtime_triggers";
        const string effectId = "effect_many_runtime_triggers";
        const string sourceId = "wound_many_runtime_triggers";
        const string eventKind = "resource_damaged";
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var definition = EffectMaterializationTestFixture.CreateDefinition(
            "periodic_restore");
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            "npc",
            "periodic_restore");
        effect["effectId"] = effectId;
        effect["target"]!["targetId"] = npcId;
        effect["source"]!["sourceId"] = sourceId;
        effect["chronology"]!["createdEventRef"] =
            "turn_42:many_runtime_triggers";
        effect["chronology"]!["lastTransitionId"] =
            "effect_transition_many_runtime_triggers";
        var periodic = effect["components"]![0]!.DeepClone().AsObject();
        periodic["componentId"] = "component_many_trigger_periodic";
        var reactionPayload = new JsonObject
        {
            ["eventType"] = eventKind,
            ["resultKind"] = reactionResultKind,
            ["dependency"] = "before_current_event",
            ["maxExpansion"] = reactionMaxExpansion ?? triggerCount
        };
        if (string.Equals(
                reactionResultKind,
                "apply_definition",
                StringComparison.Ordinal))
        {
            reactionPayload["definitionKey"] =
                EffectMaterializationTestFixture.DefinitionKey;
            reactionPayload["parameters"] = new JsonObject
            {
                ["amount"] = 1
            };
        }
        var reaction = new JsonObject
        {
            ["componentId"] = "component_many_trigger_reaction",
            ["profile"] = "event_reaction",
            ["priority"] = 50,
            ["payload"] = reactionPayload
        };
        effect["components"] = includeReaction && includePeriodic
            ? new JsonArray(periodic, reaction)
            : includeReaction
                ? new JsonArray(reaction)
                : new JsonArray(periodic);
        var orderedTriggers = Enumerable.Range(0, triggerCount)
            .Select(index => new JsonObject
            {
                ["triggerId"] = "trigger_many_" + Suffix(index),
                ["eventType"] = eventKind,
                ["priority"] = triggerCount - index,
                ["componentIds"] = includeReaction && includePeriodic
                    ? new JsonArray(
                        "component_many_trigger_periodic",
                        "component_many_trigger_reaction")
                    : includeReaction
                        ? new JsonArray("component_many_trigger_reaction")
                        : new JsonArray("component_many_trigger_periodic"),
                ["consumeUses"] = true,
                ["resolutionMode"] = "deterministic"
            })
            .ToArray();
        effect["triggers"] = new JsonArray((reverseTriggers
                ? orderedTriggers.Reverse()
                : orderedTriggers)
            .Select(static trigger => (JsonNode)trigger.DeepClone())
            .ToArray());
        effect["lifetime"] = new JsonObject
        {
            ["mode"] = "uses",
            ["remainingUses"] = remainingUses,
            ["consumingTriggerIds"] = new JsonArray(Enumerable.Range(
                    0,
                    triggerCount)
                .Select(index => (JsonNode)("trigger_many_" + Suffix(index)))
                .ToArray()),
            ["displayText"] = $"{remainingUses} exact uses remain"
        };
        if (scaleSourceDefinition)
        {
            if (includeReaction && !includePeriodic)
            {
                definition["components"] = new JsonArray(reaction.DeepClone());
                definition["triggers"] = effect["triggers"]!.DeepClone();
                definition["parameterBounds"] = new JsonObject();
            }
            else
            {
                definition["components"] = new JsonArray(periodic.DeepClone());
                definition["triggers"] = new JsonArray(effect["triggers"]!
                    .AsArray()
                    .Select(trigger =>
                    {
                        var sourceTrigger = trigger!.DeepClone().AsObject();
                        sourceTrigger["componentIds"] =
                            new JsonArray("component_many_trigger_periodic");
                        return (JsonNode)sourceTrigger;
                    })
                    .ToArray());
            }
            definition["lifetime"] = new JsonObject
            {
                ["mode"] = "uses",
                ["initialUses"] = remainingUses,
                ["consumingEventTypes"] = new JsonArray(eventKind)
            };
        }

        if (corruptCanonicalTrigger)
        {
            effect["triggers"]![0]!["resolutionMode"] = 42;
        }

        var carriers = CreateNpcCarriers(npcId, new[] { effect });
        var sourceAuthority = EffectSourceAuthority.Build(
            new EffectSourceAuthorityInput(
                new[]
                {
                    new EffectSourceExport(
                        "mortal_world",
                        "wound",
                        sourceId,
                        new JsonArray(definition.DeepClone()),
                        Materializable: true,
                        Active: true,
                        SameTurn: false)
                },
                Array.Empty<EffectSourceExport>(),
                new HashSet<string>(StringComparer.Ordinal)));
        Assert.Empty(sourceAuthority.Issues);
        var targetAuthority = CreateNpcTargetAuthority(npcId);
        var binding = sourceAuthority.ResolveCanonicalBinding(
            new EffectSourceKey(
                "mortal_world",
                "wound",
                sourceId,
                EffectMaterializationTestFixture.DefinitionKey),
            "npc");
        Assert.True(binding.Success, Format(binding.Issues));
        var eventInput = new JsonObject
        {
            ["turn"] = 43,
            ["events"] = new JsonArray(new JsonObject
            {
                ["kind"] = "accepted_turn",
                ["authorityId"] = "turn_43",
                ["eventRef"] = "turn_43:accepted"
            }),
            ["lifecycleEvents"] = new JsonArray(new JsonObject
            {
                ["eventRef"] = "turn_43:many_runtime_lifecycle",
                ["turn"] = 43,
                ["phase"] = eventKind,
                ["realm"] = "mortal_world",
                ["target"] = new JsonObject
                {
                    ["kind"] = "npc",
                    ["targetId"] = npcId
                },
                ["effectId"] = effectId,
                ["triggerId"] = lifecycleTriggerId
            })
        };
        var plan = new EffectAcceptedTurnPlan(
            "many-runtime-input",
            "many-runtime-carriers",
            sourceAuthority.Fingerprint,
            targetAuthority.Fingerprint,
            Array.Empty<string>(),
            new[] { effectId },
            Array.Empty<string>(),
            Array.Empty<EffectSourceKey>(),
            Array.Empty<EffectTargetKey>(),
            new[] { binding.Source! },
            Array.Empty<EffectReactionExecution>(),
            reactionExpansionCount: 0,
            new Dictionary<
                EffectReactionExpansionKey,
                EffectReactionExpansionUsage>(),
            new[] { effect },
            carriers,
            sourceAuthority,
            targetAuthority,
            eventInput,
            new Dictionary<string, JsonObject?>(),
            new Dictionary<string, JsonObject>(),
            identityIndexBeforeImage: null,
            EffectMaterializationTestFixture.CreateIdentityIndex(effect),
            Array.Empty<string>(),
            Array.Empty<string>());
        var ownerKey = new ResourceOwnerKey(
            "mortal_world",
            ResourceOwnerKind.Npc,
            npcId);
        var owners = ResourceOwnerAuthority.Build(new ResourceOwnerAuthorityInput(
            new[]
            {
                new ResourceOwnerExport(
                    ownerKey,
                    ResourceOwnerLifecycle.Active,
                    SameTurn: false,
                    OwnerRef: null,
                    BoundNpcId: npcId,
                    new HashSet<string>(StringComparer.Ordinal) { "health" },
                    FingerprintA)
            },
            Array.Empty<ResourceOwnerExport>(),
            Array.Empty<ResourceOwnerKey>()));
        Assert.Empty(owners.Issues);
        var coordinate = new ResourceCoordinate(
            ownerKey.Realm,
            ownerKey.OwnerKind,
            ownerKey.ResourceOwnerId,
            "health");
        var producer = new ResourceOperationKey(
            "turn_43:many_runtime_producer",
            "registered_system_outcome",
            "many_runtime_producer",
            coordinate,
            ResourceOperation.Damage);
        var producerEvent = new ResourceAppliedEvent(
            eventKind,
            "resource_operation_many_runtime_producer",
            producer.EventRef,
            coordinate,
            Before: 10m,
            After: 9m,
            AppliedAmount: 1m,
            Turn: 43,
            ExecutionSequence: 0,
            SourceFingerprint: FingerprintA);
        return new OneEffectManyTriggerResolutionResult(
            triggerCount,
            plan,
            owners,
            definitions,
            coordinate,
            EffectAcceptedTurnPlanner.ResolveDuePeriodicResourceMutations(
                plan,
                owners,
                definitions),
            EffectAcceptedTurnPlanner.ResolveResourceEventMutations(
                plan,
                producerEvent,
                producer,
                owners,
                definitions));
    }

    private static AcceptedEventBudgetResult BuildAcceptedEventBudgetPlan(
        AcceptedEventBudgetFixture fixture,
        IReadOnlyList<ResourceMutationIntent> mutations,
        EffectIdentityFactory? finalizationFactory = null)
    {
        return BuildAcceptedEventBudgetPlan(
            fixture,
            mutations,
            fixture.Sources,
            Array.Empty<
                EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>(),
            EffectAcceptedTurnPlanner.EffectResourceResolutionWork.Empty,
            finalizationFactory);
    }

    private static AcceptedEventBudgetResult BuildAcceptedEventBudgetPlan(
        AcceptedEventBudgetFixture fixture,
        EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution initial)
    {
        Assert.True(initial.IsValid, Format(initial.Issues));
        var sources = ResourceMutationSourceCatalog.Create(
            fixture.Sources.Exports.Concat(initial.SourceExports));
        Assert.True(sources.IsValid, Format(sources.Issues));
        return BuildAcceptedEventBudgetPlan(
            fixture,
            initial.Mutations,
            Assert.IsType<ResourceMutationSourceCatalog>(sources.Catalog),
            initial.TriggerCandidates,
            initial.Work,
            finalizationFactory: null);
    }

    private static AcceptedEventBudgetResult BuildAcceptedEventBudgetPlan(
        AcceptedEventBudgetFixture fixture,
        IReadOnlyList<ResourceMutationIntent> mutations,
        ResourceMutationSourceCatalog sources,
        IReadOnlyList<
            EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>
            initialTriggerCandidates,
        EffectAcceptedTurnPlanner.EffectResourceResolutionWork initialWork,
        EffectIdentityFactory? finalizationFactory)
    {
        EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution Resolve(
            ResourceAppliedEvent resourceEvent,
            ResourceOperationKey producer)
        {
            return EffectAcceptedTurnPlanner.ResolveResourceEventMutations(
                fixture.Plan,
                resourceEvent,
                producer,
                fixture.Owners,
                fixture.Definitions);
        }

        var resources = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 43,
                Definitions: fixture.Definitions,
                State: fixture.State,
                History: fixture.History,
                Sources: sources,
                Mutations: mutations,
                EventMutationResolver: Resolve,
                InitialTriggerCandidates: initialTriggerCandidates,
                InitialEffectResolutionWork: initialWork,
                EffectPlanAuthority:
                    AcceptedMechanicsPlanner.CreateEffectPlanAuthority(
                        fixture.Plan)),
            IdentityFactory());
        if (!resources.IsValid)
        {
            return new AcceptedEventBudgetResult(
                resources,
                Array.Empty<EffectReactionExecution>(),
                Finalized: null);
        }

        var acceptedReactions = resources.EffectBoundaryTranscript
            .ReleasedReactions
            .OrderBy(static released => released.MechanicsOrdinal)
            .Select(static released => released.Reaction)
            .ToArray();
        var finalized = EffectAcceptedTurnPlanner.CompleteAcceptedBoundaryTranscript(
            fixture.Plan,
            resources.EffectBoundaryTranscript,
            finalizationFactory ?? new EffectIdentityFactory());
        return new AcceptedEventBudgetResult(
            resources,
            acceptedReactions,
            finalized);
    }

    private static AcceptedEventBudgetFixture SeedExactEffectMutationReplay(
        AcceptedEventBudgetFixture fixture,
        ResourceMutationIntent producer,
        string? componentId = null)
    {
        var producerEvent = new ResourceAppliedEvent(
            BudgetEventKind(producer.Source.Operation),
            "operation_budget_replay_probe",
            producer.EventRef,
            producer.Coordinate,
            Before: 10m,
            After: 8m,
            AppliedAmount: 2m,
            Turn: 43,
            ExecutionSequence: 0,
            SourceFingerprint: FingerprintB);
        var expansion = EffectAcceptedTurnPlanner.ResolveResourceEventMutations(
            fixture.Plan,
            producerEvent,
            producer.Key,
            fixture.Owners,
            fixture.Definitions);
        Assert.True(expansion.IsValid, Format(expansion.Issues));
        var expansionCandidate = Assert.Single(expansion.TriggerCandidates);
        var selected = componentId == null
            ? Assert.Single(expansion.Mutations)
            : Assert.Single(
                expansion.Mutations,
                mutation =>
                    expansionCandidate.PlannedComponentIdsByMutation.TryGetValue(
                        mutation.Key,
                        out var selectedComponentId) &&
                    string.Equals(
                        selectedComponentId,
                        componentId,
                        StringComparison.Ordinal));
        var predecessor = selected with
        {
            Dependencies = Array.Empty<ResourceOperationKey>(),
            EventRequirements = Array.Empty<ResourceMutationEventRequirement>()
        };
        Assert.NotEqual(producer.Coordinate, predecessor.Coordinate);
        Assert.Single(expansionCandidate.ReactionOutputs);
        var sourceResult = ResourceMutationSourceCatalog.Create(
            fixture.Sources.Exports.Concat(expansion.SourceExports));
        Assert.True(sourceResult.IsValid, Format(sourceResult.Issues));
        var seeded = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 43,
                Definitions: fixture.Definitions,
                State: fixture.State,
                History: fixture.History,
                Sources: sourceResult.Catalog!,
                Mutations: new[] { predecessor }),
            IdentityFactory(seed: 100));
        Assert.True(seeded.IsValid, Format(seeded.Issues));
        var seededTransition = Assert.Single(seeded.AppliedTransitions);
        Assert.Equal(ResourceMutationPhase.EffectTrigger, seededTransition.Phase);
        var indexedTransition = seededTransition with { ExecutionSequence = 1 };
        var indexedHistory = ResourceHistoryState.CreateValidated(
            fixture.History.Transitions.Append(indexedTransition),
            fixture.Definitions);
        Assert.True(indexedHistory.IsValid, Format(indexedHistory.Issues));
        Assert.Empty(indexedHistory.History!.ValidateStateAgreement(
            seeded.StateAfterImage!));
        return fixture with
        {
            State = seeded.StateAfterImage!,
            History = indexedHistory.History!,
            Sources = sourceResult.Catalog!
        };
    }

    private static BudgetProducerBaseline AddBudgetProducerResource(
        AcceptedEventBudgetFixture fixture)
    {
        var coordinate = fixture.Coordinate with { ResourceKey = "energy" };
        var executionSequence = fixture.History.Transitions
            .Where(static transition => transition.Turn == 1)
            .Select(static transition => transition.ExecutionSequence)
            .DefaultIfEmpty(-1)
            .Max() + 1;
        Assert.True(fixture.Definitions.TryResolveExact("energy", out var definition));
        var binding = new ResourceCapacityBinding(
            definition!.CapacityPolicy.Kind,
            definition.CapacityPolicy.FormulaKey ?? "energy_capacity_test",
            FingerprintA);
        var snapshot = new ResourceStateSnapshot(
            Current: 10m,
            Maximum: 10m,
            binding,
            ResourceLifecycleState.Active);
        var initialize = new ResourceTransition(
            TransitionId: "transition_budget_energy_initialize",
            OperationId: "operation_budget_energy_initialize",
            EventRef: "turn_1:budget:energy_initialize",
            OriginKind: "owner_materialization",
            OriginId: coordinate.ResourceOwnerId,
            Phase: ResourceMutationPhase.RegisteredSystemOutcome,
            Priority: 50,
            ExecutionSequence: executionSequence,
            Coordinate: coordinate,
            Operation: ResourceTransitionOperation.Initialize,
            RequestedAmount: 0m,
            AppliedAmount: 0m,
            Outcome: ResourceTransitionOutcome.Applied,
            CapacityDisposition:
                ResourceCapacityDisposition.InitializeFromDefinition,
            BeforeState: null,
            AfterState: snapshot,
            SourceEvidence: new ResourceSourceEvidence(
                "owner_materialization",
                coordinate.ResourceOwnerId,
                FingerprintA),
            PolicyFingerprint: FingerprintA,
            ReceiptId: null,
            Turn: 1);
        var historyResult = ResourceHistoryState.CreateValidated(
            fixture.History.Transitions.Append(initialize),
            fixture.Definitions);
        Assert.True(historyResult.IsValid, Format(historyResult.Issues));
        var state = new ResourceStateLedger(fixture.State.Entries.Append(
            new ResourceStateEntry(
                coordinate,
                snapshot.Current,
                snapshot.Maximum,
                binding,
                ResourceLifecycleState.Active,
                new ResourceChronology(
                    CreatedAtTurn: 1,
                    CreatedEventRef: initialize.EventRef,
                    LastTransitionId: initialize.TransitionId,
                    LastEventRef: initialize.EventRef,
                    LastTransitionTurn: 1))));
        var ownerInput = fixture.Owners.ExportInput();
        var owners = ResourceOwnerAuthority.Build(new ResourceOwnerAuthorityInput(
            ownerInput.PreTurnOwners.Select(owner => owner with
            {
                ResourceCapabilities = new HashSet<string>(
                    owner.ResourceCapabilities,
                    StringComparer.Ordinal)
                {
                    "energy"
                }
            }).ToArray(),
            ownerInput.SameTurnOwners,
            ownerInput.HistoricalOwners));
        Assert.Empty(owners.Issues);
        Assert.Empty(historyResult.History!.ValidateStateAgreement(state));
        Assert.Empty(owners.ValidateCanonicalAgreement(
            state,
            historyResult.History));
        return new BudgetProducerBaseline(
            fixture with
            {
                Owners = owners,
                State = state,
                History = historyResult.History!
            },
            coordinate);
    }

    private static string BudgetEventKind(ResourceOperation operation) =>
        operation switch
        {
            ResourceOperation.Damage => "resource_damaged",
            ResourceOperation.Restore => "resource_restored",
            ResourceOperation.Spend => "resource_spent",
            ResourceOperation.Gain => "resource_gained",
            _ => throw new ArgumentOutOfRangeException(
                nameof(operation),
                operation,
                "Unsupported budget test operation.")
        };

    private static AcceptedEventBudgetFixture
        CreateAfterComponentReplacementNestedTraversalFixture()
    {
        const string oldEffectId = "effect_replace_nested_old";
        const string siblingEffectId = "effect_replace_nested_sibling";
        const string rootSourceId = "wound_replace_nested_root";
        const string siblingSourceId = "wound_replace_nested_sibling";
        const string rootDefinitionKey = "replace_nested_root";
        const string siblingDefinitionKey = "replace_nested_sibling";
        const string replacementDefinitionKey = "replace_nested_replacement";
        const string sharedStackKey = "replace-nested-shared-stack";
        const string siblingStackKey = "replace-nested-sibling-stack";
        const string periodicComponentId = "component_replace_nested_restore";
        const string replacementComponentId = "component_replace_nested_apply";
        const string oldNestedComponentId = "component_replace_nested_old_observer";
        const string siblingNestedComponentId =
            "component_replace_nested_sibling_observer";
        const string outerTriggerId = "trigger_replace_nested_outer";
        const string oldNestedTriggerId = "trigger_replace_nested_old_observer";
        const string siblingNestedTriggerId =
            "trigger_replace_nested_sibling_observer";

        static JsonObject CreateObserverComponent(
            string componentId,
            int priority) =>
            new()
            {
                ["componentId"] = componentId,
                ["profile"] = "action_control",
                ["priority"] = priority,
                ["payload"] = new JsonObject
                {
                    ["action"] = "movement",
                    ["operation"] = "restrict"
                }
            };

        var rootDefinition = EffectMaterializationTestFixture.CreateDefinition(
            "periodic_restore");
        rootDefinition["definitionKey"] = rootDefinitionKey;
        rootDefinition["stacking"]!["stackKey"] = sharedStackKey;
        var periodic = rootDefinition["components"]![0]!
            .DeepClone()
            .AsObject();
        periodic["componentId"] = periodicComponentId;
        periodic["priority"] = 100;
        periodic["payload"]!["amount"] = 3;
        var replaceReaction = new JsonObject
        {
            ["componentId"] = replacementComponentId,
            ["profile"] = "event_reaction",
            ["priority"] = 50,
            ["payload"] = new JsonObject
            {
                ["eventType"] = "resource_damaged",
                ["resultKind"] = "apply_definition",
                ["dependency"] = "after_component",
                ["afterComponentId"] = periodicComponentId,
                ["maxExpansion"] = 2,
                ["definitionKey"] = replacementDefinitionKey,
                ["parameters"] = new JsonObject
                {
                    ["amount"] = 3
                }
            }
        };
        var oldNestedReaction = CreateObserverComponent(
            oldNestedComponentId,
            priority: 70);
        rootDefinition["components"] = new JsonArray(
            periodic.DeepClone(),
            replaceReaction.DeepClone(),
            oldNestedReaction.DeepClone());
        rootDefinition["triggers"] = new JsonArray(
            new JsonObject
            {
                ["triggerId"] = outerTriggerId,
                ["eventType"] = "resource_damaged",
                ["priority"] = 10,
                ["componentIds"] = new JsonArray(
                    periodicComponentId,
                    replacementComponentId),
                ["consumeUses"] = false,
                ["resolutionMode"] = "deterministic"
            },
            new JsonObject
            {
                ["triggerId"] = oldNestedTriggerId,
                ["eventType"] = "resource_restored",
                ["priority"] = 20,
                ["componentIds"] = new JsonArray(oldNestedComponentId),
                ["consumeUses"] = false,
                ["resolutionMode"] = "deterministic"
            });

        var replacementDefinition = EffectMaterializationTestFixture
            .CreateDefinition("periodic_restore");
        replacementDefinition["definitionKey"] = replacementDefinitionKey;
        replacementDefinition["stacking"]!["stackKey"] = sharedStackKey;
        replacementDefinition["stacking"]!["policy"] = "replace";
        replacementDefinition["stacking"]!["maxStacks"] = 1;
        replacementDefinition["stacking"]!["atMaximum"] = "no_change";

        var siblingDefinition = EffectMaterializationTestFixture.CreateDefinition(
            "action_control");
        siblingDefinition["definitionKey"] = siblingDefinitionKey;
        siblingDefinition["stacking"]!["stackKey"] = siblingStackKey;
        var siblingNestedReaction = CreateObserverComponent(
            siblingNestedComponentId,
            priority: 80);
        siblingDefinition["components"] = new JsonArray(
            siblingNestedReaction.DeepClone());
        siblingDefinition["triggers"] = new JsonArray(new JsonObject
        {
            ["triggerId"] = siblingNestedTriggerId,
            ["eventType"] = "resource_restored",
            ["priority"] = 30,
            ["componentIds"] = new JsonArray(siblingNestedComponentId),
            ["consumeUses"] = false,
            ["resolutionMode"] = "deterministic"
        });

        var oldEffect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            "player",
            "periodic_restore");
        oldEffect["effectId"] = oldEffectId;
        oldEffect["source"]!["sourceId"] = rootSourceId;
        oldEffect["source"]!["definitionKey"] = rootDefinitionKey;
        oldEffect["stacking"]!["stackKey"] = sharedStackKey;
        oldEffect["components"] = rootDefinition["components"]!.DeepClone();
        oldEffect["triggers"] = rootDefinition["triggers"]!.DeepClone();

        var siblingEffect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            "player",
            "action_control");
        siblingEffect["effectId"] = siblingEffectId;
        siblingEffect["source"]!["sourceId"] = siblingSourceId;
        siblingEffect["source"]!["definitionKey"] = siblingDefinitionKey;
        siblingEffect["stacking"]!["stackKey"] = siblingStackKey;
        siblingEffect["components"] = siblingDefinition["components"]!.DeepClone();
        siblingEffect["triggers"] = siblingDefinition["triggers"]!.DeepClone();
        const string siblingTransitionId =
            "effect_transition_replace_nested_sibling_create";
        siblingEffect["chronology"]!["lastTransitionId"] = siblingTransitionId;

        var sourceAuthority = EffectSourceAuthority.Build(
            new EffectSourceAuthorityInput(
                new[]
                {
                    new EffectSourceExport(
                        "mortal_world",
                        "wound",
                        rootSourceId,
                        new JsonArray(
                            rootDefinition.DeepClone(),
                            replacementDefinition.DeepClone()),
                        Materializable: true,
                        Active: true,
                        SameTurn: false),
                    new EffectSourceExport(
                        "mortal_world",
                        "wound",
                        siblingSourceId,
                        new JsonArray(siblingDefinition.DeepClone()),
                        Materializable: true,
                        Active: true,
                        SameTurn: false)
                },
                Array.Empty<EffectSourceExport>(),
                new HashSet<string>(StringComparer.Ordinal)));
        Assert.Empty(sourceAuthority.Issues);
        var targetAuthority = EffectTargetAuthority.Build(
            new EffectTargetAuthorityInput(
                new[]
                {
                    new EffectTargetExport(
                        "mortal_world",
                        "player",
                        "player_current",
                        SameTurn: false)
                },
                Array.Empty<EffectTargetExport>(),
                new HashSet<string>(StringComparer.Ordinal),
                CombatantIdentities: null));
        Assert.Empty(targetAuthority.Issues);
        var identityIndex = EffectMaterializationTestFixture.CreateIdentityIndex(
            oldEffect,
            siblingEffect);
        var siblingIdentity = Assert.Single(
            identityIndex["entries"]!
                .AsArray()
                .OfType<JsonObject>(),
            entry => string.Equals(
                entry["effectId"]?.GetValue<string>(),
                siblingEffectId,
                StringComparison.Ordinal));
        siblingIdentity["transitions"]![0]!["transitionId"] = siblingTransitionId;
        siblingIdentity["transitions"]![0]!["eventRef"] =
            "turn_42:wound_replace_nested_sibling_opened";
        var carriers = new EffectCarrierCatalogInput(
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray(
                    oldEffect.DeepClone(),
                    siblingEffect.DeepClone())
            },
            null,
            null,
            null,
            null,
            null);
        var eventInput = new JsonObject
        {
            ["turn"] = 43,
            ["events"] = new JsonArray(new JsonObject
            {
                ["kind"] = "accepted_turn",
                ["authorityId"] = "turn_43",
                ["eventRef"] = "turn_43:accepted_replace_nested"
            }),
            ["lifecycleEvents"] = new JsonArray()
        };
        var rootBinding = sourceAuthority.ResolveCanonicalBinding(
            new EffectSourceKey(
                "mortal_world",
                "wound",
                rootSourceId,
                rootDefinitionKey),
            "player");
        var siblingBinding = sourceAuthority.ResolveCanonicalBinding(
            new EffectSourceKey(
                "mortal_world",
                "wound",
                siblingSourceId,
                siblingDefinitionKey),
            "player");
        Assert.True(rootBinding.Success, Format(rootBinding.Issues));
        Assert.True(siblingBinding.Success, Format(siblingBinding.Issues));
        var reactionPlan = EffectReactionExecutor.Plan(
            eventInput,
            sourceAuthority,
            carriers);
        Assert.True(reactionPlan.Success, Format(reactionPlan.Issues));
        var reactionExpansionUsage = EffectReactionExecutor.CreateExpansionUsage(
            reactionPlan.Executions);
        var plan = new EffectAcceptedTurnPlan(
            "replace-nested-input",
            "replace-nested-carriers",
            sourceAuthority.Fingerprint,
            targetAuthority.Fingerprint,
            Array.Empty<string>(),
            new[] { oldEffectId, siblingEffectId },
            Array.Empty<string>(),
            Array.Empty<EffectSourceKey>(),
            Array.Empty<EffectTargetKey>(),
            new[] { rootBinding.Source!, siblingBinding.Source! },
            reactionPlan.Executions,
            reactionPlan.Executions.Count,
            reactionExpansionUsage,
            new[] { oldEffect, siblingEffect },
            carriers,
            sourceAuthority,
            targetAuthority,
            eventInput,
            new Dictionary<string, JsonObject?>(),
            new Dictionary<string, JsonObject>(),
            identityIndexBeforeImage: null,
            identityIndex,
            Array.Empty<string>(),
            Array.Empty<string>());

        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var owners = ResourceOwnerAuthority.CreateCurrentPlayerAuthority(definitions);
        var coordinate = new ResourceCoordinate(
            "mortal_world",
            ResourceOwnerKind.Player,
            "player_current",
            "health");
        var baseline = CreateBudgetResourceBaseline(
            definitions,
            coordinate,
            current: 5m);
        Assert.Empty(owners.ValidateCanonicalAgreement(
            baseline.State,
            baseline.History));
        return new AcceptedEventBudgetFixture(
            plan,
            owners,
            definitions,
            coordinate,
            baseline.State,
            baseline.History,
            baseline.Sources);
    }

    private static JsonObject CreateBudgetIdentityIndex(
        IReadOnlyList<JsonObject> effects)
    {
        var identityIndex = EffectMaterializationTestFixture.CreateIdentityIndex(
            effects.ToArray());
        foreach (var effect in effects.Skip(1))
        {
            var effectId = effect["effectId"]!.GetValue<string>();
            var identity = Assert.Single(
                identityIndex["entries"]!.AsArray().OfType<JsonObject>(),
                entry => string.Equals(
                    entry["effectId"]?.GetValue<string>(),
                    effectId,
                    StringComparison.Ordinal));
            identity["transitions"]![0]!["transitionId"] =
                effect["chronology"]!["lastTransitionId"]!.GetValue<string>();
            identity["transitions"]![0]!["eventRef"] =
                "turn_42:accepted_event_budget:identity:" + effectId;
        }
        return identityIndex;
    }

    private static AcceptedEventBudgetFixture CreateAcceptedEventBudgetFixture(
        int remainingUses,
        decimal current,
        string reactionResultKind = "suspend",
        string reactionDependency = "before_current_event",
        string? reactionAfterComponentId = null,
        bool includeExactLifecycleEvent = false,
        int exactLifecycleEventCount = 1,
        string? siblingReactionResultKind = null,
        JsonObject? applyDefinitionSource = null,
        JsonObject? siblingApplyDefinitionSource = null,
        bool distinctReactionPerTrigger = false,
        JsonObject? frozenReplacementTargetEffect = null,
        bool includeCompetingReactionOwner = false,
        params BudgetTriggerSpec[] triggerSpecs)
    {
        Assert.True(remainingUses > 0);
        Assert.NotEmpty(triggerSpecs);
        Assert.InRange(current, 0m, 10m);
        const string npcId = "npc_accepted_event_budget";
        const string effectId = "effect_accepted_event_budget";
        const string sourceId = "wound_accepted_event_budget";
        const string periodicComponentId = "component_budget_periodic";
        const string dependentPeriodicComponentId =
            "component_budget_dependent_periodic";
        const string reactionComponentId = "component_budget_reaction";

        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var definition = EffectMaterializationTestFixture.CreateDefinition(
            "periodic_restore");
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            "npc",
            "periodic_restore");
        effect["effectId"] = effectId;
        effect["target"]!["targetId"] = npcId;
        effect["source"]!["sourceId"] = sourceId;
        effect["chronology"]!["createdEventRef"] =
            "turn_42:accepted_event_budget";
        effect["chronology"]!["lastTransitionId"] =
            "effect_transition_accepted_event_budget";

        var periodic = effect["components"]![0]!.DeepClone().AsObject();
        periodic["componentId"] = periodicComponentId;
        periodic["payload"]!["amount"] = 3;
        var dependentPeriodic = string.Equals(
                reactionResultKind,
                "trigger_component",
                StringComparison.Ordinal)
            ? EffectMaterializationTestFixture
                .CreateDefinition("periodic_damage")["components"]![0]!
                .DeepClone()
                .AsObject()
            : null;
        if (dependentPeriodic != null)
        {
            dependentPeriodic["componentId"] = dependentPeriodicComponentId;
            dependentPeriodic["payload"]!["amount"] = 3;
        }
        var reactionEventKinds = triggerSpecs
            .Where(static trigger => trigger.IncludeReaction)
            .Select(static trigger => trigger.EventKind)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.True(reactionEventKinds.Length <= 1);
        JsonObject CreateReaction(
            string componentId,
            string resultKind,
            int priority,
            JsonObject? definitionSource = null)
        {
            var payload = new JsonObject
            {
                ["eventType"] = reactionEventKinds.FirstOrDefault() ??
                    "resource_damaged",
                ["resultKind"] = resultKind,
                ["dependency"] = reactionDependency,
                ["maxExpansion"] = string.Equals(
                    resultKind,
                    "apply_definition",
                    StringComparison.Ordinal)
                    ? Math.Max(triggerSpecs.Length, 2)
                    : triggerSpecs.Length
            };
            if (reactionAfterComponentId != null)
                payload["afterComponentId"] = reactionAfterComponentId;
            if (string.Equals(
                    resultKind,
                    "trigger_component",
                    StringComparison.Ordinal))
            {
                Assert.NotNull(dependentPeriodic);
                payload["componentId"] = dependentPeriodicComponentId;
            }
            if (string.Equals(
                    resultKind,
                    "event_outcome",
                    StringComparison.Ordinal))
            {
                payload["originalOutcome"] = "critical_failure";
                payload["resolvedOutcome"] = "failure";
            }
            if (string.Equals(
                    resultKind,
                    "apply_definition",
                    StringComparison.Ordinal))
            {
                definitionSource ??= applyDefinitionSource;
                Assert.NotNull(definitionSource);
                payload["definitionKey"] = definitionSource!["definitionKey"]!
                    .GetValue<string>();
                payload["parameters"] = new JsonObject
                {
                    ["amount"] = 3
                };
            }
            return new JsonObject
            {
                ["componentId"] = componentId,
                ["profile"] = "event_reaction",
                ["priority"] = priority,
                ["payload"] = payload
            };
        }
        var reaction = CreateReaction(
            reactionComponentId,
            reactionResultKind,
            priority: 50);
        Assert.False(distinctReactionPerTrigger && siblingReactionResultKind != null);
        var distinctReactionsByTriggerId = distinctReactionPerTrigger
            ? triggerSpecs
                .Where(static trigger => trigger.IncludeReaction)
                .Select((trigger, index) => (
                    trigger.TriggerId,
                    Reaction: CreateReaction(
                        reactionComponentId + "_" +
                        (index + 1).ToString(
                            System.Globalization.CultureInfo.InvariantCulture),
                        reactionResultKind,
                        priority: 50 + index)))
                .ToDictionary(
                    static value => value.TriggerId,
                    static value => value.Reaction,
                    StringComparer.Ordinal)
            : new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        const string siblingReactionComponentId =
            "component_budget_sibling_reaction";
        var siblingReaction = siblingReactionResultKind == null
            ? null
            : CreateReaction(
                siblingReactionComponentId,
                siblingReactionResultKind,
                priority: 60,
                siblingApplyDefinitionSource);
        var components = new JsonArray();
        if (triggerSpecs.Any(static trigger => trigger.IncludePeriodic))
            components.Add(periodic.DeepClone());
        if (dependentPeriodic != null)
            components.Add(dependentPeriodic.DeepClone());
        if (triggerSpecs.Any(static trigger => trigger.IncludeReaction))
        {
            if (distinctReactionPerTrigger)
            {
                foreach (var distinctReaction in distinctReactionsByTriggerId
                             .Values)
                {
                    components.Add(distinctReaction.DeepClone());
                }
            }
            else
            {
                components.Add(reaction.DeepClone());
                if (siblingReaction != null)
                    components.Add(siblingReaction.DeepClone());
            }
        }
        effect["components"] = components;

        var triggers = new JsonArray(triggerSpecs.Select(trigger =>
        {
            var componentIds = new JsonArray();
            if (trigger.IncludePeriodic)
                componentIds.Add(periodicComponentId);
            if (trigger.IncludeReaction)
            {
                if (distinctReactionPerTrigger)
                {
                    componentIds.Add(
                        distinctReactionsByTriggerId[trigger.TriggerId]
                            ["componentId"]!.GetValue<string>());
                }
                else
                {
                    componentIds.Add(reactionComponentId);
                    if (siblingReaction != null)
                        componentIds.Add(siblingReactionComponentId);
                }
            }
            return (JsonNode)new JsonObject
            {
                ["triggerId"] = trigger.TriggerId,
                ["eventType"] = trigger.EventKind,
                ["priority"] = trigger.Priority,
                ["componentIds"] = componentIds,
                ["consumeUses"] = trigger.ConsumesUse,
                ["resolutionMode"] = trigger.ResolutionMode
            };
        }).ToArray());
        effect["triggers"] = triggers;
        effect["lifetime"] = new JsonObject
        {
            ["mode"] = "uses",
            ["remainingUses"] = remainingUses,
            ["consumingTriggerIds"] = new JsonArray(triggerSpecs
                .Where(static trigger => trigger.ConsumesUse)
                .Select(static trigger => (JsonNode)trigger.TriggerId)
                .ToArray()),
            ["displayText"] = $"{remainingUses} accepted uses remain"
        };

        definition["components"] = components.DeepClone();
        definition["triggers"] = triggers.DeepClone();
        if (!triggerSpecs.Any(static trigger => trigger.IncludePeriodic))
            definition["parameterBounds"] = new JsonObject();
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "uses",
            ["initialUses"] = remainingUses,
            ["consumingEventTypes"] = new JsonArray(triggerSpecs
                .Where(static trigger => trigger.ConsumesUse)
                .Select(static trigger => trigger.EventKind)
                .Distinct(StringComparer.Ordinal)
                .Select(static eventKind => (JsonNode)eventKind)
                .ToArray())
        };

        const string competingEffectId =
            "effect_accepted_event_budget_competing";
        const string competingDefinitionKey =
            "accepted_event_budget_competing_owner";
        const string competingStackKey =
            "accepted-event-budget-competing-owner-stack";
        JsonObject? competingEffect = null;
        JsonObject? competingDefinition = null;
        if (includeCompetingReactionOwner)
        {
            competingEffect = effect.DeepClone().AsObject();
            competingEffect["effectId"] = competingEffectId;
            competingEffect["source"]!["definitionKey"] =
                competingDefinitionKey;
            competingEffect["stacking"]!["stackKey"] = competingStackKey;
            competingEffect["chronology"]!["createdEventRef"] =
                "turn_42:accepted_event_budget:competing";
            competingEffect["chronology"]!["lastTransitionId"] =
                "effect_transition_accepted_event_budget_competing";
            competingDefinition = definition.DeepClone().AsObject();
            competingDefinition["definitionKey"] = competingDefinitionKey;
            competingDefinition["stacking"]!["stackKey"] = competingStackKey;
        }
        var activeEffects = new List<JsonObject> { effect };
        if (competingEffect != null)
            activeEffects.Add(competingEffect);
        if (frozenReplacementTargetEffect != null)
            activeEffects.Add(frozenReplacementTargetEffect);
        var carriers = CreateNpcCarriers(npcId, activeEffects);
        var sourceDefinitions = new JsonArray(definition.DeepClone());
        if (competingDefinition != null)
            sourceDefinitions.Add(competingDefinition.DeepClone());
        if (applyDefinitionSource != null)
            sourceDefinitions.Add(applyDefinitionSource.DeepClone());
        if (siblingApplyDefinitionSource != null)
            sourceDefinitions.Add(siblingApplyDefinitionSource.DeepClone());
        var sourceAuthority = EffectSourceAuthority.Build(
            new EffectSourceAuthorityInput(
                new[]
                {
                    new EffectSourceExport(
                        "mortal_world",
                        "wound",
                        sourceId,
                        sourceDefinitions,
                        Materializable: true,
                        Active: true,
                        SameTurn: false)
                },
                Array.Empty<EffectSourceExport>(),
                new HashSet<string>(StringComparer.Ordinal)));
        Assert.Empty(sourceAuthority.Issues);
        var targetAuthority = CreateNpcTargetAuthority(npcId);
        var binding = sourceAuthority.ResolveCanonicalBinding(
            new EffectSourceKey(
                "mortal_world",
                "wound",
                sourceId,
                EffectMaterializationTestFixture.DefinitionKey),
            "npc");
        Assert.True(binding.Success, Format(binding.Issues));
        var sourceBindings = new List<EffectSourceAuthorityEntry>
        {
            binding.Source!
        };
        if (competingDefinition != null)
        {
            var competingBinding = sourceAuthority.ResolveCanonicalBinding(
                new EffectSourceKey(
                    "mortal_world",
                    "wound",
                    sourceId,
                    competingDefinitionKey),
                "npc");
            Assert.True(
                competingBinding.Success,
                Format(competingBinding.Issues));
            sourceBindings.Add(competingBinding.Source!);
        }
        if (frozenReplacementTargetEffect != null)
        {
            var replacementBinding = sourceAuthority.ResolveCanonicalBinding(
                new EffectSourceKey(
                    "mortal_world",
                    frozenReplacementTargetEffect["source"]!["kind"]!
                        .GetValue<string>(),
                    frozenReplacementTargetEffect["source"]!["sourceId"]!
                        .GetValue<string>(),
                    frozenReplacementTargetEffect["source"]!["definitionKey"]!
                        .GetValue<string>()),
                "npc");
            Assert.True(
                replacementBinding.Success,
                Format(replacementBinding.Issues));
            sourceBindings.Add(replacementBinding.Source!);
        }
        var lifecycleEvents = new JsonArray();
        if (includeExactLifecycleEvent)
        {
            Assert.True(exactLifecycleEventCount > 0);
            var trigger = Assert.Single(
                triggerSpecs,
                static candidate => candidate.IncludeReaction);
            for (var eventIndex = 0;
                 eventIndex < exactLifecycleEventCount;
                 eventIndex++)
            {
                var suffix = eventIndex == 0
                    ? string.Empty
                    : ":" + (eventIndex + 1).ToString(
                        System.Globalization.CultureInfo.InvariantCulture);
                lifecycleEvents.Add(new JsonObject
                {
                    ["eventRef"] = "turn_43:budget:due_lifecycle" + suffix,
                    ["causalEventRef"] =
                        "turn_43:budget:lifecycle_cause" + suffix,
                    ["turn"] = 43,
                    ["phase"] = trigger.EventKind,
                    ["realm"] = "mortal_world",
                    ["target"] = new JsonObject
                    {
                        ["kind"] = "npc",
                        ["targetId"] = npcId
                    },
                    ["effectId"] = effectId,
                    ["triggerId"] = trigger.TriggerId
                });
            }
        }
        var eventInput = new JsonObject
        {
            ["turn"] = 43,
            ["events"] = new JsonArray(new JsonObject
            {
                ["kind"] = "accepted_turn",
                ["authorityId"] = "turn_43",
                ["eventRef"] = "turn_43:accepted"
            }),
            ["lifecycleEvents"] = lifecycleEvents
        };
        var reactionPlan = EffectReactionExecutor.Plan(
            eventInput,
            sourceAuthority,
            carriers);
        Assert.True(reactionPlan.Success, Format(reactionPlan.Issues));
        var reactionExpansionUsage = EffectReactionExecutor.CreateExpansionUsage(
            reactionPlan.Executions);
        var plan = new EffectAcceptedTurnPlan(
            "accepted-event-budget-input",
            "accepted-event-budget-carriers",
            sourceAuthority.Fingerprint,
            targetAuthority.Fingerprint,
            Array.Empty<string>(),
            includeCompetingReactionOwner
                ? new[] { effectId, competingEffectId }
                : new[] { effectId },
            Array.Empty<string>(),
            Array.Empty<EffectSourceKey>(),
            Array.Empty<EffectTargetKey>(),
            sourceBindings,
            reactionPlan.Executions,
            reactionExpansionCount: reactionPlan.Executions.Count,
            reactionExpansionUsage,
            activeEffects,
            carriers,
            sourceAuthority,
            targetAuthority,
            eventInput,
            new Dictionary<string, JsonObject?>(),
            new Dictionary<string, JsonObject>(),
            identityIndexBeforeImage: null,
            CreateBudgetIdentityIndex(activeEffects),
            Array.Empty<string>(),
            Array.Empty<string>());

        var ownerKey = new ResourceOwnerKey(
            "mortal_world",
            ResourceOwnerKind.Npc,
            npcId);
        var owners = ResourceOwnerAuthority.Build(new ResourceOwnerAuthorityInput(
            new[]
            {
                new ResourceOwnerExport(
                    ownerKey,
                    ResourceOwnerLifecycle.Active,
                    SameTurn: false,
                    OwnerRef: null,
                    BoundNpcId: npcId,
                    new HashSet<string>(StringComparer.Ordinal) { "health" },
                    FingerprintA)
            },
            Array.Empty<ResourceOwnerExport>(),
            Array.Empty<ResourceOwnerKey>()));
        Assert.Empty(owners.Issues);
        var coordinate = new ResourceCoordinate(
            ownerKey.Realm,
            ownerKey.OwnerKind,
            ownerKey.ResourceOwnerId,
            "health");
        var resourceBaseline = CreateBudgetResourceBaseline(
            definitions,
            coordinate,
            current);
        Assert.Empty(owners.ValidateCanonicalAgreement(
            resourceBaseline.State,
            resourceBaseline.History));
        return new AcceptedEventBudgetFixture(
            plan,
            owners,
            definitions,
            coordinate,
            resourceBaseline.State,
            resourceBaseline.History,
            resourceBaseline.Sources);
    }

    private static BudgetResourceBaseline CreateBudgetResourceBaseline(
        ResourceDefinitionCatalog definitions,
        ResourceCoordinate coordinate,
        decimal current)
    {
        Assert.True(definitions.TryResolveExact("health", out var definition));
        var binding = new ResourceCapacityBinding(
            definition!.CapacityPolicy.Kind,
            definition.CapacityPolicy.FormulaKey ?? "health_capacity_test",
            FingerprintA);
        var initializedSnapshot = new ResourceStateSnapshot(
            Current: 10m,
            Maximum: 10m,
            binding,
            ResourceLifecycleState.Active);
        var initialize = new ResourceTransition(
            TransitionId: "transition_budget_initialize",
            OperationId: "operation_budget_initialize",
            EventRef: "turn_1:budget:initialize",
            OriginKind: "owner_materialization",
            OriginId: coordinate.ResourceOwnerId,
            Phase: ResourceMutationPhase.RegisteredSystemOutcome,
            Priority: 50,
            ExecutionSequence: 0,
            Coordinate: coordinate,
            Operation: ResourceTransitionOperation.Initialize,
            RequestedAmount: 0m,
            AppliedAmount: 0m,
            Outcome: ResourceTransitionOutcome.Applied,
            CapacityDisposition: ResourceCapacityDisposition.InitializeFromDefinition,
            BeforeState: null,
            AfterState: initializedSnapshot,
            SourceEvidence: new ResourceSourceEvidence(
                "owner_materialization",
                coordinate.ResourceOwnerId,
                FingerprintA),
            PolicyFingerprint: FingerprintA,
            ReceiptId: null,
            Turn: 1);
        var transitions = new List<ResourceTransition> { initialize };
        ResourceTransition? latest = null;
        if (current != initializedSnapshot.Current)
        {
            latest = new ResourceTransition(
                TransitionId: "transition_budget_baseline_damage",
                OperationId: "operation_budget_baseline_damage",
                EventRef: "turn_1:budget:baseline_damage",
                OriginKind: "combat_outcome",
                OriginId: "budget_baseline_damage",
                Phase: ResourceMutationPhase.DirectOutcome,
                Priority: 100,
                ExecutionSequence: 1,
                Coordinate: coordinate,
                Operation: ResourceTransitionOperation.Damage,
                RequestedAmount: initializedSnapshot.Current - current,
                AppliedAmount: initializedSnapshot.Current - current,
                Outcome: ResourceTransitionOutcome.Applied,
                CapacityDisposition: null,
                BeforeState: initializedSnapshot,
                AfterState: initializedSnapshot with { Current = current },
                SourceEvidence: new ResourceSourceEvidence(
                    "combat_outcome",
                    "budget_baseline_damage",
                    FingerprintB),
                PolicyFingerprint: FingerprintB,
                ReceiptId: null,
                Turn: 1);
            transitions.Add(latest);
        }
        var history = ResourceHistoryState.CreateValidated(
            transitions,
            definitions);
        Assert.True(history.IsValid, Format(history.Issues));
        var state = new ResourceStateLedger(new[]
        {
            new ResourceStateEntry(
                coordinate,
                current,
                initializedSnapshot.Maximum,
                binding,
                ResourceLifecycleState.Active,
                new ResourceChronology(
                    CreatedAtTurn: 1,
                    CreatedEventRef: initialize.EventRef,
                    LastTransitionId: (latest ?? initialize).TransitionId,
                    LastEventRef: (latest ?? initialize).EventRef,
                    LastTransitionTurn: 1))
        });
        Assert.Empty(history.History!.ValidateStateAgreement(state));
        var sources = ResourceMutationSourceCatalog.Create(new[]
        {
            new ResourceMutationSourceExport(
                "registered_system_outcome",
                "budget_root_source",
                FingerprintB,
                ResourceMutationSourceState.Active,
                SameTurn: false)
        });
        Assert.True(sources.IsValid, Format(sources.Issues));
        return new BudgetResourceBaseline(
            state,
            history.History,
            sources.Catalog!);
    }

    private static ResourceMutationIntent CreateBudgetMutation(
        ResourceCoordinate coordinate,
        string eventRef,
        ResourceOperation operation,
        decimal amount,
        IReadOnlyList<ResourceOperationKey>? dependencies = null) =>
        new(
            eventRef,
            coordinate,
            amount,
            new ResourceMutationSourceRequest(
                "registered_system_outcome",
                "budget_root_source",
                operation),
            dependencies ?? Array.Empty<ResourceOperationKey>(),
            Array.Empty<ResourceMutationEventRequirement>(),
            ReceiptId: null);

    private static string AcceptedActivationKey(
        string effectId,
        string triggerId,
        string eventKind,
        string? triggerEventRef) =>
        string.Join(
            "\0",
            effectId,
            triggerId,
            eventKind,
            triggerEventRef ?? string.Empty);

    private static void AssertExactRuntimeWork(
        EffectAcceptedTurnPlanner.EffectResourceResolutionWork work,
        int triggerCount,
        int sourceBindingBorrowsPerTrigger,
        int componentLookupsPerTrigger,
        int selectedComponentVisitsPerTrigger)
    {
        Assert.Equal((long)triggerCount, work.RoutingDescriptorAccessCount);
        Assert.Equal(0L, work.OccurrenceCloneCount);
        Assert.Equal(0L, work.FullEffectValidationPassCount);
        Assert.Equal(0L, work.TriggerArrayVisitCount);
        Assert.Equal(
            (long)componentLookupsPerTrigger * triggerCount,
            work.ComponentIndexLookupCount);
        Assert.Equal(
            (long)selectedComponentVisitsPerTrigger * triggerCount,
            work.SelectedComponentVisitCount);
        Assert.Equal(
            (long)sourceBindingBorrowsPerTrigger * triggerCount,
            ReadRequiredWorkMetric(work, "SourceBindingBorrowCount"));
        Assert.Equal(
            0L,
            ReadRequiredWorkMetric(work, "SourceBindingDefinitionCloneCount"));
        Assert.Equal(
            0L,
            ReadRequiredWorkMetric(
                work,
                "SourceBindingDefinitionCloneNodeCount"));
    }

    private static (ResourceOperationKey Key, ResourceAppliedEvent Event)
        CreateDamageProducer(
        ResourceCoordinate coordinate,
        string suffix)
    {
        var key = new ResourceOperationKey(
            "turn_43:" + suffix,
            "registered_system_outcome",
            suffix,
            coordinate,
            ResourceOperation.Damage);
        return (
            key,
            new ResourceAppliedEvent(
                "resource_damaged",
                "resource_operation_" + suffix,
                key.EventRef,
                coordinate,
                Before: 10m,
                After: 9m,
                AppliedAmount: 1m,
                Turn: 43,
                ExecutionSequence: 0,
                SourceFingerprint: FingerprintA));
    }

    private static long ReadRequiredWorkMetric(
        EffectAcceptedTurnPlanner.EffectResourceResolutionWork work,
        string propertyName)
    {
        var property = work.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.NonPublic);
        Assert.NotNull(property);
        return Convert.ToInt64(
            property!.GetValue(work),
            CultureInfo.InvariantCulture);
    }

    private static string[] DescribeResourceResolution(
        EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution resolution) =>
        resolution.TriggerCandidates
            .Select(candidate => string.Join(
                "\0",
                candidate.Activation.Identity.EffectId,
                candidate.Activation.Identity.TriggerId,
                candidate.Activation.Identity.EventKind,
                candidate.Activation.Identity.EventRef,
                candidate.UseSeed?.RemainingUses.ToString(
                    CultureInfo.InvariantCulture) ?? "none",
                string.Join(",", candidate.PlannedComponentIds)))
            .Concat(resolution.TriggerCandidates
                .SelectMany(static candidate => candidate.ReactionOutputs)
                .Select(execution => string.Join(
                "\0",
                "reaction",
                execution.EffectId,
                execution.TriggerId,
                execution.ComponentId,
                execution.ResultKind)))
            .ToArray();

    private static EffectAcceptedTurnPlanner.EffectResourceTriggerIndex
        CreateOrderingTriggerIndex(bool reverseCarriers)
    {
        var npcId = "npc_trigger_order";
        var effects = new[]
        {
            CreateIndexedEffect(
                "effect_trigger_order_a",
                npcId,
                new[]
                {
                    CreateIndexedTrigger("trigger_a", priority: 20),
                    CreateIndexedTrigger("trigger_z", priority: 10)
                }),
            CreateIndexedEffect(
                "effect_trigger_order_b",
                npcId,
                new[]
                {
                    CreateIndexedTrigger("trigger_b", priority: 30),
                    CreateIndexedTrigger("trigger_y", priority: 5)
                })
        };
        return EffectAcceptedTurnPlanner.CreateResourceTriggerIndex(
            CreateNpcCarriers(
                npcId,
                reverseCarriers ? effects.Reverse().ToArray() : effects),
            CreateNpcTargetAuthority(npcId));
    }

    private static JsonObject CreateIndexedEffect(
        string effectId,
        string npcId,
        IReadOnlyList<JsonObject> triggers)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            "npc",
            "periodic_restore");
        effect["effectId"] = effectId;
        effect["target"]!["targetId"] = npcId;
        effect["triggers"] = new JsonArray(triggers
            .Select(static trigger => (JsonNode)trigger.DeepClone())
            .ToArray());
        return effect;
    }

    private static JsonObject CreateIndexedTrigger(
        string triggerId,
        int priority) =>
        new()
        {
            ["triggerId"] = triggerId,
            ["eventType"] = "owner_turn_end",
            ["priority"] = priority,
            ["componentIds"] = new JsonArray("component_001"),
            ["consumeUses"] = false,
            ["resolutionMode"] = "deterministic"
        };

    private static EffectCarrierCatalogInput CreateNpcCarriers(
        string npcId,
        IReadOnlyList<JsonObject> effects) =>
        new(
            null,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = npcId,
                    ["activeEffects"] = new JsonArray(effects
                        .Select(static effect => (JsonNode)effect.DeepClone())
                        .ToArray())
                })
            },
            null,
            null,
            null,
            null);

    private static EffectTargetAuthority CreateNpcTargetAuthority(string npcId)
    {
        var authority = EffectTargetAuthority.Build(
            new EffectTargetAuthorityInput(
                new[]
                {
                    new EffectTargetExport(
                        "mortal_world",
                        "npc",
                        npcId,
                        SameTurn: false)
                },
                Array.Empty<EffectTargetExport>(),
                new HashSet<string>(StringComparer.Ordinal),
                CombatantIdentities: null));
        Assert.Empty(authority.Issues);
        return authority;
    }

    private static string[] DescribeIndexedTriggers(
        IEnumerable<EffectAcceptedTurnPlanner.IndexedResourceTrigger> triggers) =>
        triggers.Select(static trigger => string.Join(
            "/",
            trigger.EffectId,
            trigger.Priority.ToString(CultureInfo.InvariantCulture),
            trigger.TriggerId)).ToArray();

    private static ExactLifecycleTriggerResult ResolveExactLifecycleTriggers(
        int count,
        bool reverseCarriers)
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var effectDefinition = EffectMaterializationTestFixture.CreateDefinition(
            "periodic_restore");
        var npcId = "npc_exact_lifecycle_scale";
        var effects = Enumerable.Range(0, count)
            .Select(index =>
            {
                var suffix = Suffix(index);
                var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
                    "npc",
                    "periodic_restore");
                effect["effectId"] = "effect_exact_lifecycle_" + suffix;
                effect["target"]!["targetId"] = npcId;
                effect["source"]!["sourceId"] = "wound_exact_lifecycle_scale";
                effect["components"] = effectDefinition["components"]!.DeepClone();
                effect["triggers"] = effectDefinition["triggers"]!.DeepClone();
                effect["chronology"]!["createdEventRef"] =
                    "turn_42:effect_exact_lifecycle:" + suffix;
                effect["chronology"]!["lastTransitionId"] =
                    "effect_transition_exact_lifecycle_" + suffix;
                return effect;
            })
            .ToArray();
        var carrierEffects = reverseCarriers
            ? effects.Reverse().ToArray()
            : effects;
        var carriers = new EffectCarrierCatalogInput(
            null,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = npcId,
                    ["activeEffects"] = new JsonArray(carrierEffects
                        .Select(static effect => (JsonNode)effect.DeepClone())
                        .ToArray())
                })
            },
            null,
            null,
            null,
            null);
        var lifecycleEvents = new JsonArray(effects
            .Select((effect, index) => (JsonNode)new JsonObject
            {
                ["eventRef"] = "turn_43:lifecycle_exact:" + Suffix(index),
                ["turn"] = 43,
                ["phase"] = "owner_turn_end",
                ["realm"] = "mortal_world",
                ["target"] = new JsonObject
                {
                    ["kind"] = "npc",
                    ["targetId"] = npcId
                },
                ["effectId"] = effect["effectId"]!.GetValue<string>(),
                ["triggerId"] = "on_owner_turn_end"
            })
            .ToArray());
        var eventInput = new JsonObject
        {
            ["turn"] = 43,
            ["events"] = new JsonArray(new JsonObject
            {
                ["kind"] = "accepted_turn",
                ["authorityId"] = "turn_43",
                ["eventRef"] = "turn_43:accepted"
            }),
            ["lifecycleEvents"] = lifecycleEvents
        };
        var sourceAuthority = EffectSourceAuthority.Build(
            new EffectSourceAuthorityInput(
                new[]
                {
                    new EffectSourceExport(
                        "mortal_world",
                        "wound",
                        "wound_exact_lifecycle_scale",
                        new JsonArray(effectDefinition.DeepClone()),
                        Materializable: true,
                        Active: true,
                        SameTurn: false)
                },
                Array.Empty<EffectSourceExport>(),
                new HashSet<string>(StringComparer.Ordinal)));
        Assert.Empty(sourceAuthority.Issues);
        var targetAuthority = EffectTargetAuthority.Build(
            new EffectTargetAuthorityInput(
                new[]
                {
                    new EffectTargetExport(
                        "mortal_world",
                        "npc",
                        npcId,
                        SameTurn: false)
                },
                Array.Empty<EffectTargetExport>(),
                new HashSet<string>(StringComparer.Ordinal),
                CombatantIdentities: null));
        Assert.Empty(targetAuthority.Issues);
        var binding = sourceAuthority.ResolveCanonicalBinding(
            new EffectSourceKey(
                "mortal_world",
                "wound",
                "wound_exact_lifecycle_scale",
                EffectMaterializationTestFixture.DefinitionKey),
            "npc");
        Assert.True(binding.Success, Format(binding.Issues));
        var plan = new EffectAcceptedTurnPlan(
            "exact-lifecycle-input",
            "exact-lifecycle-carriers",
            sourceAuthority.Fingerprint,
            targetAuthority.Fingerprint,
            Array.Empty<string>(),
            effects.Select(static effect =>
                effect["effectId"]!.GetValue<string>()).ToArray(),
            Array.Empty<string>(),
            Array.Empty<EffectSourceKey>(),
            Array.Empty<EffectTargetKey>(),
            new[] { binding.Source! },
            Array.Empty<EffectReactionExecution>(),
            reactionExpansionCount: 0,
            new Dictionary<
                EffectReactionExpansionKey,
                EffectReactionExpansionUsage>(),
            effects,
            carriers,
            sourceAuthority,
            targetAuthority,
            eventInput,
            new Dictionary<string, JsonObject?>(),
            new Dictionary<string, JsonObject>(),
            identityIndexBeforeImage: null,
            EffectMaterializationTestFixture.CreateIdentityIndex(effects),
            Array.Empty<string>(),
            Array.Empty<string>());
        var ownerKey = new ResourceOwnerKey(
            "mortal_world",
            ResourceOwnerKind.Npc,
            npcId);
        var owners = ResourceOwnerAuthority.Build(new ResourceOwnerAuthorityInput(
            new[]
            {
                new ResourceOwnerExport(
                    ownerKey,
                    ResourceOwnerLifecycle.Active,
                    SameTurn: false,
                    OwnerRef: null,
                    BoundNpcId: npcId,
                    new HashSet<string>(StringComparer.Ordinal) { "health" },
                    FingerprintA)
            },
            Array.Empty<ResourceOwnerExport>(),
            Array.Empty<ResourceOwnerKey>()));
        Assert.Empty(owners.Issues);

        var resolution = EffectAcceptedTurnPlanner.ResolveDuePeriodicResourceMutations(
            plan,
            owners,
            definitions);
        return new ExactLifecycleTriggerResult(plan, resolution);
    }

    private static string[] DescribeLifecycleResolution(
        EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution resolution) =>
        resolution.TriggerExecutions
            .Select(execution => string.Join(
                "\0",
                execution.EffectId,
                execution.TriggerId,
                execution.EventKind,
                execution.EventRef,
                string.Join(",", execution.MutationKeys
                    .Select(static key => key.EventRef))))
            .ToArray();

    private static RealEffectTriggerResult PlanRealEffectTriggers(
        int count,
        IReadOnlySet<string>? satisfiedPredicates = null)
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        Assert.True(definitions.TryResolveExact("health", out var healthDefinition));
        var effectDefinition = EffectMaterializationTestFixture.CreateDefinition(
            "periodic_restore");
        effectDefinition["triggers"]![0]!["triggerId"] = "on_resource_damaged";
        effectDefinition["triggers"]![0]!["eventType"] = "resource_damaged";

        var effects = new List<JsonObject>(count);
        var npcEntries = new JsonArray();
        var targetExports = new List<EffectTargetExport>(count);
        var ownerExports = new List<ResourceOwnerExport>(count);
        var stateEntries = new List<ResourceStateEntry>(count);
        var historyEntries = new List<ResourceTransition>(count);
        var mutations = new List<ResourceMutationIntent>(count);
        for (var index = 0; index < count; index++)
        {
            var suffix = Suffix(index);
            var npcId = "npc_effect_trigger_scale_" + suffix;
            var effectId = "effect_trigger_scale_" + suffix;
            var effectEventRef = "turn_42:effect_trigger_scale:" + suffix;
            var effectTransitionId = "effect_transition_trigger_scale_" + suffix;
            var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
                "npc",
                "periodic_restore");
            effect["effectId"] = effectId;
            effect["target"]!["targetId"] = npcId;
            effect["source"]!["sourceId"] = "wound_trigger_scale";
            effect["components"] = effectDefinition["components"]!.DeepClone();
            effect["triggers"] = effectDefinition["triggers"]!.DeepClone();
            effect["chronology"]!["createdAtTurn"] = 42;
            effect["chronology"]!["createdEventRef"] = effectEventRef;
            effect["chronology"]!["lastTransitionId"] = effectTransitionId;
            effect["chronology"]!["lastTransitionTurn"] = 42;
            effects.Add(effect);
            npcEntries.Add(new JsonObject
            {
                ["NPCId"] = npcId,
                ["activeEffects"] = new JsonArray(effect.DeepClone())
            });
            targetExports.Add(new EffectTargetExport(
                "mortal_world",
                "npc",
                npcId,
                SameTurn: false));

            var ownerKey = new ResourceOwnerKey(
                "mortal_world",
                ResourceOwnerKind.Npc,
                npcId);
            ownerExports.Add(new ResourceOwnerExport(
                ownerKey,
                ResourceOwnerLifecycle.Active,
                SameTurn: false,
                OwnerRef: null,
                BoundNpcId: npcId,
                new HashSet<string>(StringComparer.Ordinal) { "health" },
                FingerprintA));
            var coordinate = new ResourceCoordinate(
                ownerKey.Realm,
                ownerKey.OwnerKind,
                ownerKey.ResourceOwnerId,
                "health");
            var capacityResult = ResolvedResourceCapacity.Resolve(
                healthDefinition!,
                coordinate,
                new RegisteredFormulaCapacityInput(
                    new MaterializedOwnerCapacityFormulaInput(
                        new ResourceFormulaOwner(
                            ownerKey.Realm,
                            ownerKey.OwnerKind,
                            ownerKey.ResourceOwnerId),
                        FingerprintA,
                        AcceptedMaximum: 10m)),
                instanceAuthorityKey: null,
                includeInitialization: true);
            Assert.True(capacityResult.IsValid, Format(capacityResult.Issues));
            var capacity = capacityResult.Capacity!;
            var snapshot = new ResourceStateSnapshot(
                Current: capacity.Initialization!.Current,
                capacity.Maximum,
                capacity.Binding,
                ResourceLifecycleState.Active);
            var transition = new ResourceTransition(
                TransitionId: "resource_transition_trigger_scale_" + suffix,
                OperationId: "resource_operation_trigger_scale_" + suffix,
                EventRef: "turn_42:resource_initialize:" + suffix,
                OriginKind: "owner_materialization",
                OriginId: npcId,
                Phase: ResourceMutationPhase.RegisteredSystemOutcome,
                Priority: 40,
                ExecutionSequence: index,
                Coordinate: coordinate,
                Operation: ResourceTransitionOperation.Initialize,
                RequestedAmount: 0m,
                AppliedAmount: 0m,
                Outcome: ResourceTransitionOutcome.Applied,
                CapacityDisposition: ResourceCapacityDisposition.InitializeFromDefinition,
                BeforeState: null,
                AfterState: snapshot,
                SourceEvidence: new ResourceSourceEvidence(
                    "owner_materialization",
                    npcId,
                    FingerprintA),
                PolicyFingerprint: capacity.Initialization!.AuthorityFingerprint,
                ReceiptId: null,
                Turn: 42);
            historyEntries.Add(transition);
            stateEntries.Add(new ResourceStateEntry(
                coordinate,
                snapshot.Current,
                snapshot.Maximum,
                snapshot.CapacityBinding,
                snapshot.State,
                new ResourceChronology(
                    CreatedAtTurn: 42,
                    CreatedEventRef: transition.EventRef,
                    LastTransitionId: transition.TransitionId,
                    LastEventRef: transition.EventRef,
                    LastTransitionTurn: 42)));
            mutations.Add(new ResourceMutationIntent(
                EventRef: "turn_43:resource_damage:" + suffix,
                Coordinate: coordinate,
                Amount: 1m,
                Source: new ResourceMutationSourceRequest(
                    "registered_system_outcome",
                    "real_trigger_scale_system",
                    ResourceOperation.Damage),
                Dependencies: Array.Empty<ResourceOperationKey>(),
                EventRequirements: Array.Empty<ResourceMutationEventRequirement>(),
                ReceiptId: null));
        }

        var identity = EffectMaterializationTestFixture.CreateIdentityIndex(
            effects.ToArray());
        var identityEntries = identity["entries"]!.AsArray();
        for (var index = 0; index < identityEntries.Count; index++)
        {
            var suffix = Suffix(index);
            var entry = identityEntries[index]!.AsObject();
            entry["transitions"]![0]!["transitionId"] =
                "effect_transition_trigger_scale_" + suffix;
            entry["transitions"]![0]!["eventRef"] =
                "turn_42:effect_trigger_scale:" + suffix;
        }

        var sourceAuthority = EffectSourceAuthority.Build(
            new EffectSourceAuthorityInput(
                new[]
                {
                    new EffectSourceExport(
                        "mortal_world",
                        "wound",
                        "wound_trigger_scale",
                        new JsonArray(effectDefinition.DeepClone()),
                        Materializable: true,
                        Active: true,
                        SameTurn: false)
                },
                Array.Empty<EffectSourceExport>(),
                new HashSet<string>(StringComparer.Ordinal)));
        Assert.Empty(sourceAuthority.Issues);
        var targetAuthority = EffectTargetAuthority.Build(
            new EffectTargetAuthorityInput(
                targetExports,
                Array.Empty<EffectTargetExport>(),
                new HashSet<string>(StringComparer.Ordinal),
                CombatantIdentities: null));
        Assert.Empty(targetAuthority.Issues);
        var planResult = EffectAcceptedTurnPlanner.Build(
            new EffectAcceptedTurnInput(
                "session_real_trigger_scale",
                "snapshot_real_trigger_scale",
                EffectMaterializationTestFixture.CreateCommandRoot(),
                sourceAuthority,
                targetAuthority,
                new JsonObject
                {
                    ["turn"] = 43,
                    ["events"] = new JsonArray(new JsonObject
                    {
                        ["kind"] = "accepted_turn",
                        ["authorityId"] = "turn_43",
                        ["eventRef"] = "turn_43:accepted"
                    }),
                    ["lifecycleEvents"] = new JsonArray()
                },
                PreTurnCarriers: new EffectCarrierCatalogInput(
                    null,
                    new JsonObject
                    {
                        ["schemaVersion"] = 1,
                        ["entries"] = npcEntries
                    },
                    null,
                    null,
                    null,
                    null),
                PreTurnIdentityIndex: identity),
            "real-effect-trigger-scale-plan",
            new EffectIdentityFactory());
        Assert.Empty(planResult.Issues);
        var builtPlan = Assert.IsType<EffectAcceptedTurnPlan>(planResult.Plan);
        var bindingKey = new EffectSourceKey(
            "mortal_world",
            "wound",
            "wound_trigger_scale",
            EffectMaterializationTestFixture.DefinitionKey);
        var sourceBinding = sourceAuthority.ResolveCanonicalBinding(
            bindingKey,
            "npc");
        Assert.True(sourceBinding.Success, Format(sourceBinding.Issues));
        var planSourceBinding = sourceBinding.Source! with
        {
            SatisfiedPredicates = satisfiedPredicates ??
                sourceBinding.Source.SatisfiedPredicates
        };
        var plan = new EffectAcceptedTurnPlan(
            builtPlan.InputFingerprint,
            builtPlan.CarrierAuthorityFingerprint,
            builtPlan.SourceAuthorityFingerprint,
            builtPlan.TargetAuthorityFingerprint,
            builtPlan.AllocatedCombatantIds,
            builtPlan.AllocatedEffectIds,
            builtPlan.AllocatedTransitionIds,
            builtPlan.Sources,
            builtPlan.Targets,
            new[] { planSourceBinding },
            builtPlan.DeferredReactions,
            builtPlan.ReactionExpansionCount,
            builtPlan.ReactionExpansionUsage,
            builtPlan.ActiveEffects,
            builtPlan.ResourceTriggerCarriers,
            builtPlan.SourceAuthority,
            builtPlan.TargetAuthority,
            builtPlan.EventInput,
            builtPlan.CarrierBeforeImages,
            builtPlan.CarrierAfterImages,
            builtPlan.IdentityIndexBeforeImage,
            builtPlan.IdentityIndexAfterImage,
            builtPlan.TouchedPaths,
            builtPlan.DeletedPaths);
        Assert.True(plan.TryResolveSourceBinding(bindingKey, out var firstBinding));
        firstBinding!.Definition["definitionKey"] = "tampered_by_consumer";
        Assert.True(plan.TryResolveSourceBinding(bindingKey, out var repeatedBinding));
        Assert.Equal(
            EffectMaterializationTestFixture.DefinitionKey,
            repeatedBinding!.Definition["definitionKey"]!.GetValue<string>());
        var owners = ResourceOwnerAuthority.Build(new ResourceOwnerAuthorityInput(
            ownerExports,
            Array.Empty<ResourceOwnerExport>(),
            Array.Empty<ResourceOwnerKey>()));
        Assert.Empty(owners.Issues);
        var historyResult = ResourceHistoryState.CreateValidated(
            historyEntries,
            definitions);
        Assert.True(historyResult.IsValid, Format(historyResult.Issues));
        var state = new ResourceStateLedger(stateEntries);
        Assert.Empty(historyResult.History!.ValidateStateAgreement(state));
        var sourceCatalog = ResourceMutationSourceCatalog.Create(new[]
        {
            new ResourceMutationSourceExport(
                "registered_system_outcome",
                "real_trigger_scale_system",
                FingerprintA,
                ResourceMutationSourceState.Active,
                SameTurn: false)
        });
        Assert.True(sourceCatalog.IsValid, Format(sourceCatalog.Issues));

        var result = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 43,
                Definitions: definitions,
                State: state,
                History: historyResult.History,
                Sources: sourceCatalog.Catalog!,
                Mutations: mutations,
                EventMutationResolver: (resourceEvent, producer) =>
                    EffectAcceptedTurnPlanner.ResolveResourceEventMutations(
                        plan,
                        resourceEvent,
                        producer,
                        owners,
                        definitions)),
            IdentityFactory());
        return new RealEffectTriggerResult(plan, result);
    }

    private static EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution
        EmptyResolution(int candidateVisits) =>
        new(
            Array.Empty<ResourceMutationSourceExport>(),
            Array.Empty<ResourceMutationIntent>(),
            Array.Empty<ValidationIssue>())
        {
            Work = new EffectAcceptedTurnPlanner.EffectResourceResolutionWork(
                IndexLookupCount: 1,
                CandidateVisitCount: candidateVisits)
        };

    private static EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution
        EmptyResolution(
            EffectAcceptedTurnPlanner.EffectResourceResolutionWork work) =>
        new(
            Array.Empty<ResourceMutationSourceExport>(),
            Array.Empty<ResourceMutationIntent>(),
            Array.Empty<ValidationIssue>())
        {
            Work = work
        };

    private static ResourceMutationIntent CreateScaleMutation(
        ResourceCoordinate coordinate,
        string eventRef) =>
        new(
            eventRef,
            coordinate,
            Amount: 1m,
            new ResourceMutationSourceRequest(
                "registered_system_outcome",
                "trigger_scale_system",
                ResourceOperation.Spend),
            Array.Empty<ResourceOperationKey>(),
            Array.Empty<ResourceMutationEventRequirement>(),
            ReceiptId: null);

    private static TriggerBaseline CreateBaseline(int count)
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var coordinate = new ResourceCoordinate(
            "mortal_world",
            ResourceOwnerKind.Item,
            "trigger_scale_item",
            "charges");
        var maximum = count + 2m;
        var binding = new ResourceCapacityBinding(
            ResourceCapacityKind.InstanceFixed,
            "trigger_scale_capacity",
            FingerprintA);
        var snapshot = new ResourceStateSnapshot(
            maximum,
            maximum,
            binding,
            ResourceLifecycleState.Active);
        var transition = new ResourceTransition(
            TransitionId: "trigger_scale_initialize_transition",
            OperationId: "trigger_scale_initialize_operation",
            EventRef: "turn_1:trigger_scale_initialize",
            OriginKind: "setting_materialization",
            OriginId: coordinate.ResourceOwnerId,
            Phase: ResourceMutationPhase.RegisteredSystemOutcome,
            Priority: 50,
            ExecutionSequence: 0,
            Coordinate: coordinate,
            Operation: ResourceTransitionOperation.Initialize,
            RequestedAmount: 0m,
            AppliedAmount: 0m,
            Outcome: ResourceTransitionOutcome.Applied,
            CapacityDisposition: ResourceCapacityDisposition.InitializeFromDefinition,
            BeforeState: null,
            AfterState: snapshot,
            SourceEvidence: new ResourceSourceEvidence(
                "setting_materialization",
                coordinate.ResourceOwnerId,
                FingerprintA),
            PolicyFingerprint: FingerprintA,
            ReceiptId: null,
            Turn: 1);
        var history = ResourceHistoryState.CreateValidated(
            new[] { transition },
            definitions);
        Assert.True(history.IsValid, Format(history.Issues));
        var state = new ResourceStateLedger(new[]
        {
            new ResourceStateEntry(
                coordinate,
                snapshot.Current,
                snapshot.Maximum,
                binding,
                snapshot.State,
                new ResourceChronology(
                    CreatedAtTurn: 1,
                    CreatedEventRef: transition.EventRef,
                    LastTransitionId: transition.TransitionId,
                    LastEventRef: transition.EventRef,
                    LastTransitionTurn: 1))
        });
        Assert.Empty(history.History!.ValidateStateAgreement(state));
        var sourceResult = ResourceMutationSourceCatalog.Create(new[]
        {
            new ResourceMutationSourceExport(
                "registered_system_outcome",
                "trigger_scale_system",
                FingerprintA,
                ResourceMutationSourceState.Active,
                SameTurn: false)
        });
        Assert.True(sourceResult.IsValid, Format(sourceResult.Issues));
        return new TriggerBaseline(
            definitions,
            state,
            history.History,
            sourceResult.Catalog!,
            coordinate);
    }

    private static AcceptedMechanicsIdentityFactory IdentityFactory(int seed = 1)
    {
        var next = seed;
        return new AcceptedMechanicsIdentityFactory(() =>
            new Guid(next++, 0, 0, new byte[8]));
    }

    private static string Suffix(int index) =>
        index.ToString("D4", CultureInfo.InvariantCulture);

    private static string Format(IEnumerable<ValidationIssue> issues) =>
        string.Join(Environment.NewLine, issues.Select(static issue =>
            $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}"));

    private sealed record TriggerBaseline(
        ResourceDefinitionCatalog Definitions,
        ResourceStateLedger State,
        ResourceHistoryState History,
        ResourceMutationSourceCatalog Sources,
        ResourceCoordinate Coordinate);

    private sealed record BudgetTriggerSpec(
        string TriggerId,
        string EventKind,
        int Priority,
        bool IncludePeriodic,
        bool IncludeReaction,
        bool ConsumesUse = true,
        string ResolutionMode = "deterministic");

    private sealed record BudgetResourceBaseline(
        ResourceStateLedger State,
        ResourceHistoryState History,
        ResourceMutationSourceCatalog Sources);

    private sealed record BudgetProducerBaseline(
        AcceptedEventBudgetFixture Fixture,
        ResourceCoordinate Coordinate);

    private sealed record AcceptedEventBudgetFixture(
        EffectAcceptedTurnPlan Plan,
        ResourceOwnerAuthority Owners,
        ResourceDefinitionCatalog Definitions,
        ResourceCoordinate Coordinate,
        ResourceStateLedger State,
        ResourceHistoryState History,
        ResourceMutationSourceCatalog Sources);

    private sealed record AcceptedEventBudgetResult(
        AcceptedMechanicsResourcePlanningResult Resources,
        IReadOnlyList<EffectReactionExecution> AcceptedReactions,
        EffectAcceptedTurnPlanningResult? Finalized);

    private sealed class CountingEffectIdentityFactory : EffectIdentityFactory
    {
        internal int EffectCalls { get; private set; }

        internal int TransitionCalls { get; private set; }

        internal override string CreateEffectId()
        {
            EffectCalls++;
            return "effect_counted_" + EffectCalls.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
        }

        internal override string CreateTransitionId()
        {
            TransitionCalls++;
            return "effect_transition_counted_" + TransitionCalls.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private sealed record RealEffectTriggerResult(
        EffectAcceptedTurnPlan Plan,
        AcceptedMechanicsResourcePlanningResult Result);

    private sealed record ExactLifecycleTriggerResult(
        EffectAcceptedTurnPlan Plan,
        EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution Resolution);

    private sealed record OneEffectManyTriggerResolutionResult(
        int TriggerCount,
        EffectAcceptedTurnPlan Plan,
        ResourceOwnerAuthority Owners,
        ResourceDefinitionCatalog Definitions,
        ResourceCoordinate Coordinate,
        EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution LifecycleResolution,
        EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution ResourceResolution);
}
