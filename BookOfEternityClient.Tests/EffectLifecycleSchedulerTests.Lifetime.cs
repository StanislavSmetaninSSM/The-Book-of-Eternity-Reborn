using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectLifecycleSchedulerTests
{
    [Fact]
    public void AdvanceLifetime_TurnsAdvanceOnceOnlyAtExactPhaseAndExpireAtOne()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["lifetime"]!["remainingTurns"] = 2;

        var wrongPhase = Advance(effect, new EffectLifecycleEvent(
            "turn_43:start",
            43,
            Phase: "owner_turn_start"));
        Assert.True(wrongPhase.Success);
        Assert.Equal("no_change", wrongPhase.Outcome);
        Assert.Equal(2, wrongPhase.UpdatedEffect!["lifetime"]!["remainingTurns"]!.GetValue<int>());

        var advanced = Advance(effect, new EffectLifecycleEvent(
            "turn_43:end",
            43,
            Phase: "owner_turn_end"));
        Assert.True(advanced.Success);
        Assert.Equal("advance", advanced.Outcome);
        Assert.Equal(1, advanced.UpdatedEffect!["lifetime"]!["remainingTurns"]!.GetValue<int>());

        effect["lifetime"]!["remainingTurns"] = 1;
        var expired = Advance(effect, new EffectLifecycleEvent(
            "turn_44:end",
            44,
            Phase: "owner_turn_end"));
        Assert.True(expired.Success);
        Assert.Equal("expire", expired.Outcome);
        Assert.Null(expired.UpdatedEffect);
    }

    [Fact]
    public void AdvanceLifetime_UsesConsumesOnlyDeclaredTriggerAndExpiresAtOne()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["triggers"]![0]!["triggerId"] = "trigger_consume";
        effect["triggers"]![0]!["eventType"] = "owner_turn_end";
        effect["triggers"]![0]!["consumeUses"] = true;
        effect["lifetime"] = new JsonObject
        {
            ["mode"] = "uses",
            ["remainingUses"] = 1,
            ["consumingTriggerIds"] = new JsonArray("trigger_consume")
        };

        var unrelated = Advance(effect, new EffectLifecycleEvent(
            "turn_43:other",
            43,
            Phase: "owner_turn_end",
            TriggerId: "trigger_other"));
        Assert.True(unrelated.Success);
        Assert.Equal("no_change", unrelated.Outcome);

        var consumed = Advance(effect, new EffectLifecycleEvent(
            "turn_43:consume",
            43,
            Phase: "owner_turn_end",
            TriggerId: "trigger_consume"));
        Assert.True(consumed.Success);
        Assert.Equal("expire", consumed.Outcome);
    }

    [Fact]
    public void AdvanceLifetime_UsesRequiresExactDeclaredTriggerEventType()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["triggers"]![0]!["triggerId"] = "trigger_resource_depleted";
        effect["triggers"]![0]!["eventType"] = "resource_depleted";
        effect["triggers"]![0]!["consumeUses"] = true;
        effect["lifetime"] = new JsonObject
        {
            ["mode"] = "uses",
            ["remainingUses"] = 2,
            ["consumingTriggerIds"] = new JsonArray("trigger_resource_depleted")
        };

        var wrongEvent = Advance(effect, new EffectLifecycleEvent(
            "turn_43:resource_filled:trigger_resource_depleted",
            43,
            Phase: "resource_filled",
            TriggerId: "trigger_resource_depleted"));

        Assert.True(wrongEvent.Success);
        Assert.Equal("no_change", wrongEvent.Outcome);
        Assert.Equal(
            2,
            wrongEvent.UpdatedEffect!["lifetime"]!["remainingUses"]!.GetValue<int>());
    }

    [Fact]
    public void AdvanceLifetime_ExactResourceEventUseExpiresForTerminalCleanupAndRejectsReplay()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["triggers"]![0]!["triggerId"] = "trigger_resource_depleted";
        effect["triggers"]![0]!["eventType"] = "resource_depleted";
        effect["triggers"]![0]!["consumeUses"] = true;
        effect["lifetime"] = new JsonObject
        {
            ["mode"] = "uses",
            ["remainingUses"] = 1,
            ["consumingTriggerIds"] = new JsonArray("trigger_resource_depleted")
        };
        const string eventRef = "turn_43:resource_depleted:trigger_resource_depleted";
        var lifecycleEvent = new EffectLifecycleEvent(
            eventRef,
            43,
            Phase: "resource_depleted",
            TriggerId: "trigger_resource_depleted");

        var expired = Advance(effect, lifecycleEvent);
        var replay = EffectLifecycleScheduler.AdvanceLifetime(
            new EffectLifetimeReductionInput(
                effect,
                lifecycleEvent,
                new HashSet<string>(StringComparer.Ordinal) { eventRef }));

        Assert.True(expired.Success);
        Assert.Equal("expire", expired.Outcome);
        Assert.Null(expired.UpdatedEffect);
        Assert.False(replay.Success);
        Assert.Contains(replay.Issues, issue =>
            issue.Code == "effect_lifecycle_event_replay");
    }

    [Theory]
    [InlineData(99, "no_change")]
    [InlineData(100, "expire")]
    [InlineData(101, "expire")]
    public void AdvanceLifetime_UntilTimeExpiresAtEqualityOrLater(long currentTime, string expected)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["lifetime"] = new JsonObject
        {
            ["mode"] = "until_time",
            ["deadline"] = 100L
        };

        var result = Advance(effect, new EffectLifecycleEvent(
            $"turn_43:time:{currentTime}",
            43,
            CurrentTime: currentTime));

        Assert.True(result.Success);
        Assert.Equal(expected, result.Outcome);
    }

    [Fact]
    public void AdvanceLifetime_SceneUsesExactSceneAndDeclaredSuspendResumePolicy()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["lifetime"] = new JsonObject
        {
            ["mode"] = "scene",
            ["sceneId"] = "scene_exact",
            ["onSceneExit"] = "suspend"
        };

        var active = Advance(effect, new EffectLifecycleEvent(
            "turn_43:scene",
            43,
            CurrentSceneId: "scene_exact"));
        Assert.Equal("no_change", active.Outcome);

        var suspended = Advance(effect, new EffectLifecycleEvent(
            "turn_44:scene_exit",
            44,
            CurrentSceneId: "scene_other",
            SceneClosed: true));
        Assert.True(suspended.Success);
        Assert.Equal("suspend", suspended.Outcome);
        Assert.Equal("suspended", suspended.UpdatedEffect!["state"]!.GetValue<string>());

        var resumed = Advance(suspended.UpdatedEffect, new EffectLifecycleEvent(
            "turn_45:scene_resume",
            45,
            CurrentSceneId: "scene_exact"));
        Assert.True(resumed.Success);
        Assert.Equal("resume", resumed.Outcome);
        Assert.Equal("active", resumed.UpdatedEffect!["state"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("source_bound", false, "suspend")]
    [InlineData("source_bound", false, "expire")]
    [InlineData("condition_bound", false, "suspend")]
    [InlineData("condition_bound", false, "expire")]
    public void AdvanceLifetime_BoundModesUseValidatedBooleanAuthority(
        string mode,
        bool satisfied,
        string lossPolicy)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["lifetime"] = mode == "source_bound"
            ? new JsonObject
            {
                ["mode"] = mode,
                ["linkKind"] = "wound",
                ["targetId"] = "wound_test_torn_side",
                ["activePredicate"] = "active",
                ["onSourceLoss"] = lossPolicy
            }
            : new JsonObject
            {
                ["mode"] = mode,
                ["conditionKey"] = "condition_test_registered",
                ["operands"] = new JsonObject { ["threshold"] = 1 },
                ["onConditionLoss"] = lossPolicy
            };

        var result = Advance(effect, new EffectLifecycleEvent(
            "turn_43:bound",
            43,
            SourceSatisfied: mode == "source_bound" ? satisfied : null,
            ConditionSatisfied: mode == "condition_bound" ? satisfied : null));

        Assert.True(result.Success);
        Assert.Equal(lossPolicy, result.Outcome);
        Assert.Equal(
            lossPolicy == "expire" ? null : "suspended",
            result.UpdatedEffect?["state"]?.GetValue<string>());
    }

    [Theory]
    [InlineData("permanent")]
    [InlineData("manual")]
    public void AdvanceLifetime_AuthorizedPersistentModesDoNotUseNumericSentinels(string mode)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["lifetime"] = mode == "permanent"
            ? new JsonObject { ["mode"] = mode }
            : new JsonObject
            {
                ["mode"] = mode,
                ["authorities"] = new JsonArray("physical_treatment")
            };

        var result = Advance(effect, new EffectLifecycleEvent(
            "turn_43:persistent",
            43));

        Assert.True(result.Success);
        Assert.Equal("no_change", result.Outcome);
    }

    [Theory]
    [InlineData("suspend", "suspend")]
    [InlineData("expire", "expire")]
    public void AdvanceLifetime_RealmExitUsesExactSourceLossPolicy(
        string policy,
        string expected)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["removal"]!["onSourceLoss"] = policy;

        var result = Advance(effect, new EffectLifecycleEvent(
            "turn_43:realm_exit",
            43,
            CurrentRealm: "chaos_sea"));

        Assert.True(result.Success);
        Assert.Equal(expected, result.Outcome);
    }

    [Fact]
    public void AdvanceLifetime_RejectsReplayUnknownModeAndNumericSentinel()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        var replay = EffectLifecycleScheduler.AdvanceLifetime(new EffectLifetimeReductionInput(
            effect,
            new EffectLifecycleEvent("turn_43:end", 43, Phase: "owner_turn_end"),
            new HashSet<string>(StringComparer.Ordinal) { "turn_43:end" }));
        Assert.False(replay.Success);
        Assert.Contains(replay.Issues, issue => issue.Code == "effect_lifecycle_event_replay");

        effect["lifetime"]!["mode"] = "forever_in_prose";
        var unknown = Advance(effect, new EffectLifecycleEvent("turn_44:end", 44));
        Assert.False(unknown.Success);
        Assert.Contains(unknown.Issues, issue => issue.Code == "effect_lifecycle_mode_invalid");

        effect["lifetime"] = new JsonObject
        {
            ["mode"] = "turns",
            ["remainingTurns"] = 999,
            ["advancePhase"] = "owner_turn_end"
        };
        var sentinel = Advance(effect, new EffectLifecycleEvent(
            "turn_45:end",
            45,
            Phase: "owner_turn_end"));
        Assert.False(sentinel.Success);
        Assert.Contains(sentinel.Issues, issue => issue.Code == "effect_lifecycle_numeric_sentinel_forbidden");
    }

    private static EffectLifetimeReductionResult Advance(
        JsonObject effect,
        EffectLifecycleEvent lifecycleEvent) =>
        EffectLifecycleScheduler.AdvanceLifetime(new EffectLifetimeReductionInput(
            effect,
            lifecycleEvent,
            EmptyEvents));
}
