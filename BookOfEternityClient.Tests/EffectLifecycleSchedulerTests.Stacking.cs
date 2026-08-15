using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectLifecycleSchedulerTests
{
    [Fact]
    public void ResolveApplication_IndependentCreatesBoundedInstancesWithoutMutatingInput()
    {
        var definition = CreateDefinition("independent", maxStacks: 2);
        var first = CreateExisting("independent", 1, "effect_independent_a");
        var before = first.DeepClone();

        var create = EffectLifecycleScheduler.ResolveApplication(new EffectStackApplicationInput(
            new[] { first },
            definition,
            definition["components"]!.AsArray(),
            CreateInitialLifetime(definition),
            "turn_43:accepted_effect",
            EmptyEvents));

        Assert.True(create.Success);
        Assert.Equal("create", create.Outcome);
        Assert.True(create.CreatesNewIdentity);
        Assert.False(create.TerminatesExisting);
        Assert.Equal(1, create.NewEffectStacking["maxStacks"]!.GetValue<int>());
        Assert.Equal(1, create.NewEffectStacking["currentStacks"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(before, first));

        var second = CreateExisting("independent", 1, "effect_independent_b");
        var atLimit = EffectLifecycleScheduler.ResolveApplication(new EffectStackApplicationInput(
            new[] { first, second },
            definition,
            definition["components"]!.AsArray(),
            CreateInitialLifetime(definition),
            "turn_44:accepted_effect",
            EmptyEvents));

        Assert.True(atLimit.Success);
        Assert.Equal("no_change", atLimit.Outcome);
        Assert.False(atLimit.CreatesNewIdentity);
        Assert.Equal("effect_independent_a", atLimit.ExistingEffectId);
        Assert.NotNull(atLimit.UpdatedExistingEffect);
    }

    [Fact]
    public void ResolveApplication_StackIncrementsToMaximumAndThenUsesDeclaredMaximumPolicy()
    {
        var definition = CreateDefinition("stack", maxStacks: 3);
        var existing = CreateExisting("stack", 2);

        var increment = Resolve(definition, existing, "turn_43:accepted_effect");

        Assert.True(increment.Success);
        Assert.Equal("stack", increment.Outcome);
        Assert.Equal(3, increment.UpdatedExistingEffect!["stacking"]!["currentStacks"]!.GetValue<int>());
        Assert.False(increment.CreatesNewIdentity);

        existing["stacking"]!["currentStacks"] = 3;
        var atMaximum = Resolve(definition, existing, "turn_44:accepted_effect");

        Assert.True(atMaximum.Success);
        Assert.Equal("no_change", atMaximum.Outcome);
        Assert.Equal(3, atMaximum.UpdatedExistingEffect!["stacking"]!["currentStacks"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("reset", 2, 3)]
    [InlineData("extend", 2, 5)]
    public void ResolveApplication_RefreshPreservesIdentityAndUsesSourceLifetimeRule(
        string refreshMode,
        int existingTurns,
        int expectedTurns)
    {
        var definition = CreateDefinition("refresh", maxStacks: 1, refreshMode: refreshMode);
        var existing = CreateExisting("refresh", 1, maxStacks: 1);
        existing["stacking"]!["refreshMode"] = refreshMode;
        existing["lifetime"]!["remainingTurns"] = existingTurns;

        var result = Resolve(definition, existing, "turn_43:accepted_effect");

        Assert.True(result.Success);
        Assert.Equal("refresh", result.Outcome);
        Assert.False(result.CreatesNewIdentity);
        Assert.Equal(
            EffectMaterializationTestFixture.EffectId,
            result.ExistingEffectId);
        Assert.Equal(
            expectedTurns,
            result.UpdatedExistingEffect!["lifetime"]!["remainingTurns"]!.GetValue<int>());
    }

    [Fact]
    public void ResolveApplication_ReplaceTerminatesOldAndRequiresOneNewIdentity()
    {
        var definition = CreateDefinition("replace", maxStacks: 1);
        var existing = CreateExisting("replace", 1, maxStacks: 1);

        var result = Resolve(definition, existing, "turn_43:accepted_effect");

        Assert.True(result.Success);
        Assert.Equal("replace", result.Outcome);
        Assert.True(result.CreatesNewIdentity);
        Assert.True(result.TerminatesExisting);
        Assert.Equal(EffectMaterializationTestFixture.EffectId, result.ExistingEffectId);
    }

    [Theory]
    [InlineData("sum", 7d)]
    [InlineData("minimum", 3d)]
    [InlineData("maximum", 4d)]
    public void ResolveApplication_MergeUsesRegisteredNumericReducer(
        string mergeRule,
        double expectedAmount)
    {
        var definition = CreateDefinition("merge", maxStacks: 3, mergeRule: mergeRule);
        definition["components"]![0]!["payload"]!["amount"] = 4;
        var existing = CreateExisting("merge", 1);
        existing["stacking"]!["mergeRule"] = mergeRule;

        var result = Resolve(definition, existing, "turn_43:accepted_effect");

        Assert.True(result.Success);
        Assert.Equal("merge", result.Outcome);
        Assert.Equal(2, result.UpdatedExistingEffect!["stacking"]!["currentStacks"]!.GetValue<int>());
        Assert.Equal(
            expectedAmount,
            result.UpdatedExistingEffect["components"]![0]!["payload"]!["amount"]!.GetValue<double>());
    }

    [Fact]
    public void ResolveApplication_MergeRejectsNumericOverflowAndIncompatibleComponents()
    {
        var definition = CreateDefinition("merge", maxStacks: 3, mergeRule: "sum");
        definition["components"]![0]!["payload"]!["amount"] = 1.7e308;
        var existing = CreateExisting("merge", 1);
        existing["stacking"]!["mergeRule"] = "sum";
        existing["components"]![0]!["payload"]!["amount"] = 1.7e308;

        var overflow = Resolve(definition, existing, "turn_43:accepted_effect");

        Assert.False(overflow.Success);
        Assert.Contains(overflow.Issues, issue => issue.Code == "effect_lifecycle_merge_overflow");

        definition["components"]![0]!["componentId"] = "component_other";
        var incompatible = Resolve(definition, existing, "turn_44:accepted_effect");
        Assert.False(incompatible.Success);
        Assert.Contains(incompatible.Issues, issue =>
            issue.Code == "effect_lifecycle_merge_component_mismatch");
    }

    [Fact]
    public void ResolveApplication_RejectsPolicyConflictAndProcessedEventReplay()
    {
        var definition = CreateDefinition("stack", maxStacks: 3);
        var existing = CreateExisting("refresh", 1);
        existing["stacking"]!["refreshMode"] = "reset";

        var conflict = Resolve(definition, existing, "turn_43:accepted_effect");

        Assert.False(conflict.Success);
        Assert.Contains(conflict.Issues, issue => issue.Code == "effect_lifecycle_stack_policy_conflict");

        existing = CreateExisting("stack", 1);
        var replay = EffectLifecycleScheduler.ResolveApplication(new EffectStackApplicationInput(
            new[] { existing },
            definition,
            definition["components"]!.AsArray(),
            CreateInitialLifetime(definition),
            "turn_43:accepted_effect",
            new HashSet<string>(StringComparer.Ordinal) { "turn_43:accepted_effect" }));

        Assert.False(replay.Success);
        Assert.Contains(replay.Issues, issue => issue.Code == "effect_lifecycle_event_replay");
    }

    [Theory]
    [InlineData("refresh", "reset", "extend")]
    [InlineData("merge", "sum", "maximum")]
    public void ResolveApplication_RejectsConflictingSourceReducerDetails(
        string policy,
        string sourceRule,
        string existingRule)
    {
        var definition = policy == "refresh"
            ? CreateDefinition(policy, maxStacks: 1, refreshMode: sourceRule)
            : CreateDefinition(policy, maxStacks: 3, mergeRule: sourceRule);
        var existing = CreateExisting(
            policy,
            currentStacks: 1,
            maxStacks: policy == "refresh" ? 1 : 3);
        existing["stacking"]![policy == "refresh" ? "refreshMode" : "mergeRule"] =
            existingRule;

        var result = Resolve(definition, existing, "turn_43:accepted_effect");

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "effect_lifecycle_stack_policy_conflict");
    }

    private static readonly IReadOnlySet<string> EmptyEvents =
        new HashSet<string>(StringComparer.Ordinal);

    private static EffectStackApplicationResult Resolve(
        JsonObject definition,
        JsonObject existing,
        string eventRef) =>
        EffectLifecycleScheduler.ResolveApplication(new EffectStackApplicationInput(
            new[] { existing },
            definition,
            definition["components"]!.AsArray(),
            CreateInitialLifetime(definition),
            eventRef,
            EmptyEvents));

    private static JsonObject CreateDefinition(
        string policy,
        int maxStacks,
        string? refreshMode = null,
        string? mergeRule = null)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["stacking"] = new JsonObject
        {
            ["stackKey"] = "bleeding",
            ["policy"] = policy,
            ["maxStacks"] = maxStacks,
            ["atMaximum"] = "no_change",
            ["refreshMode"] = refreshMode,
            ["mergeRule"] = mergeRule
        };
        return definition;
    }

    private static JsonObject CreateExisting(
        string policy,
        int currentStacks,
        string effectId = EffectMaterializationTestFixture.EffectId,
        int maxStacks = 3)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["effectId"] = effectId;
        effect["stacking"]!["policy"] = policy;
        effect["stacking"]!["currentStacks"] = currentStacks;
        effect["stacking"]!["maxStacks"] = policy == "independent" ? 1 : maxStacks;
        effect["stacking"]!["refreshMode"] = null;
        effect["stacking"]!["mergeRule"] = null;
        return effect;
    }

    private static JsonObject CreateInitialLifetime(JsonObject definition)
    {
        var policy = definition["lifetime"]!.AsObject();
        return new JsonObject
        {
            ["mode"] = "turns",
            ["remainingTurns"] = policy["initialTurns"]!.DeepClone(),
            ["advancePhase"] = policy["advancePhase"]!.DeepClone()
        };
    }
}
