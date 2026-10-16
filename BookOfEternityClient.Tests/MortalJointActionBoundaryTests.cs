using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

// Calculation controls only. These values do not prove original/G or authorize publication.
public sealed class MortalJointActionBoundaryTests
{
    private static MortalJointActionSource Source() => new("generation", "session", "parent-request", "parent-snapshot", 43, "Mortal World", 2);
    private static byte[] Bytes(string json) => Encoding.UTF8.GetBytes(json);
    private static MortalJointRequestCoordinates Child() => MortalJointActionBoundary.DescribeContinuation(
        MortalJointActionBoundary.DescribeAction(Source()), "child-request", "child-snapshot");

    [Fact]
    public void Continuation_NewRequestRetainsOriginalGameActionAndTurn()
    {
        var source = Source();
        var action = MortalJointActionBoundary.DescribeAction(source);
        var child = MortalJointActionBoundary.DescribeContinuation(action, "child-request", "child-snapshot");
        var retry = MortalJointActionBoundary.DescribeContinuation(action, "child-request", "child-snapshot");
        Assert.Equal(43, child.GameTurn);
        Assert.Equal(action.ActionId, child.ActionId);
        Assert.Equal(child, retry);
        Assert.Equal(1, child.SegmentOrdinal);
        Assert.NotEqual(source.RequestId, child.RequestId);
        Assert.Equal(source, child.Source);
    }

    [Theory]
    [InlineData("request")]
    [InlineData("snapshot")]
    public void Continuation_CannotImpersonateParentRequestOrSnapshot(string reused)
    {
        var action = MortalJointActionBoundary.DescribeAction(Source());
        Assert.Throws<InvalidDataException>(() => MortalJointActionBoundary.DescribeContinuation(action,
            reused == "request" ? Source().RequestId : "child-request",
            reused == "snapshot" ? Source().SnapshotToken : "child-snapshot"));
    }

    [Fact]
    public void ActionIdentity_BindsFullOriginalCoordinates()
    {
        var source = Source();
        var baseline = MortalJointActionBoundary.DescribeAction(source).ActionId;
        MortalJointActionSource[] changed = [source with { Generation = "other" }, source with { SessionId = "other" },
            source with { RequestId = "other" }, source with { SnapshotToken = "other" }, source with { Turn = 44 },
            source with { Realm = "Chaos Sea" }, source with { Incarnation = 3 }];
        Assert.All(changed, row => Assert.NotEqual(baseline, MortalJointActionBoundary.DescribeAction(row).ActionId));
    }

    [Fact]
    public void ConsumedClosure_DoesNotTickOldOrNewTargetsButKeepsRealExpiryEvent()
    {
        var child = Child();
        EffectLifecycleEvent[] proposed = [new("parent-close", 43, Phase: "owner_turn_end"),
            new("new-target-close", 43, Phase: "owner_turn_end"), new("elapsed-5min", 43, CurrentTime: 105)];
        var admitted = MortalJointActionBoundary.CalculateLifecycleEvents(child, MortalJointClosureState.Consumed, proposed);
        var actual = Assert.Single(admitted);
        Assert.Equal("elapsed-5min", actual.EventRef);
        var turns = EffectMaterializationTestFixture.CreateCanonicalEffect();
        turns["lifetime"]!["remainingTurns"] = 2;
        var reduced = EffectLifecycleScheduler.AdvanceLifetime(new(turns, actual, new HashSet<string>()));
        Assert.True(reduced.Success);
        Assert.Equal(2, reduced.UpdatedEffect!["lifetime"]!["remainingTurns"]!.GetValue<int>());
        var timed = EffectMaterializationTestFixture.CreateCanonicalEffect();
        timed["lifetime"] = new JsonObject { ["mode"] = "until_time", ["deadline"] = 105L };
        var expired = EffectLifecycleScheduler.AdvanceLifetime(new(timed, actual, new HashSet<string>()));
        Assert.True(expired.Success);
        Assert.Equal("expire", expired.Outcome);
    }

    [Fact]
    public void PendingClosure_KeepsRealTurnEndAndSchedulerRejectsReplay()
    {
        var actual = Assert.Single(MortalJointActionBoundary.CalculateLifecycleEvents(Child(), MortalJointClosureState.Pending,
            [new("actual-close", 43, Phase: "owner_turn_end")]));
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["lifetime"]!["remainingTurns"] = 2;
        var reduced = EffectLifecycleScheduler.AdvanceLifetime(new(effect, actual, new HashSet<string>()));
        Assert.True(reduced.Success);
        Assert.Equal(1, reduced.UpdatedEffect!["lifetime"]!["remainingTurns"]!.GetValue<int>());
        var replay = EffectLifecycleScheduler.AdvanceLifetime(new(effect, actual, new HashSet<string> { actual.EventRef }));
        Assert.False(replay.Success);
        Assert.Contains(replay.Issues, issue => issue.Code == "effect_lifecycle_event_replay");
    }

    [Fact]
    public void ConsumedClosure_PreservesRealSceneAndSourceLoss()
    {
        var sourceLoss = new EffectLifecycleEvent("source-loss", 43, SourceSatisfied: false);
        var sceneExit = new EffectLifecycleEvent("scene-exit", 43, CurrentSceneId: "after", SceneClosed: true);
        var admitted = MortalJointActionBoundary.CalculateLifecycleEvents(Child(), MortalJointClosureState.Consumed, [sourceLoss, sceneExit]);
        Assert.Equal(new[] { sourceLoss, sceneExit }, admitted);
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["lifetime"] = new JsonObject { ["mode"] = "scene", ["sceneId"] = "before", ["onSceneExit"] = "expire" };
        var result = EffectLifecycleScheduler.AdvanceLifetime(new(effect, admitted[1], new HashSet<string>()));
        Assert.True(result.Success);
        Assert.Equal("expire", result.Outcome);
    }

    [Theory]
    [InlineData(42)]
    [InlineData(44)]
    public void LifecycleCalculation_RejectsForeignTurnInsteadOfSuppressingIt(int turn) =>
        Assert.Throws<InvalidDataException>(() => MortalJointActionBoundary.CalculateLifecycleEvents(Child(), MortalJointClosureState.Consumed,
            [new("foreign", turn, Phase: "owner_turn_end")]));

    [Fact]
    public void LifecycleCalculation_DuplicateInputIsNotAlreadyApplied() =>
        Assert.Throws<InvalidDataException>(() => MortalJointActionBoundary.CalculateLifecycleEvents(Child(), MortalJointClosureState.Pending,
            [new("same", 43), new("same", 43)]));

    [Fact]
    public void NoTimeCommand_DoesNotReapplyConsumedIncrementalTime()
    {
        var source = Bytes("{\"timeChange\":40}");
        var result = MortalJointActionBoundary.DescribeTime(source, source, MortalJointTimeCommand.None, new(140, false));
        Assert.Equal(MortalJointTimeKind.Unchanged, result.Kind);
        Assert.Equal(140, result.Progression.Minutes);
        Assert.Null(result.ElapsedMinutes);
        Assert.Null(result.EffectCurrentTime);
    }

    [Fact]
    public void IncrementalTime_WithoutAbsoluteEpochKeepsRealDeltaAndNullableEffectTime()
    {
        var result = MortalJointActionBoundary.DescribeTime(Bytes("{}"), Bytes("{\"timeChange\":5}"), MortalJointTimeCommand.Incremental, new(100, false));
        Assert.Equal(MortalJointTimeKind.Incremental, result.Kind);
        Assert.Equal(105, result.Progression.Minutes);
        Assert.False(result.Progression.HasUnresolvedAbsoluteOverride);
        Assert.Equal(5, result.ElapsedMinutes);
        Assert.Null(result.CanonicalBeforeMinutes);
        Assert.Null(result.EffectCurrentTime);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(50)]
    public void NumericAbsoluteTime_IsNotGuessedElapsedDuration(int minutes)
    {
        var result = MortalJointActionBoundary.DescribeTime(Bytes("{\"currentTimeInMinutes\":100}"),
            Bytes($"{{\"setWorldTime\":{{\"currentTimeInMinutes\":{minutes}}}}}"), MortalJointTimeCommand.Absolute, new(100, false));
        Assert.Equal(MortalJointTimeKind.NumericAbsolute, result.Kind);
        Assert.Equal(minutes, result.Progression.Minutes);
        Assert.Equal(minutes, result.EffectCurrentTime);
        Assert.Equal(100L, result.CanonicalBeforeMinutes);
        Assert.Null(result.ElapsedMinutes);
    }

    [Fact]
    public void CalendarOnlyAbsoluteTime_IsAcceptedUnresolvedWithActualNullEffectTime()
    {
        var before = Bytes("{\"currentTimeInMinutes\":100}");
        var after = Bytes("{\"setWorldTime\":{\"year\":10,\"monthName\":\"Moon\",\"dayOfMonth\":2,\"timeOfDay\":\"night\"}}");
        var result = MortalJointActionBoundary.DescribeTime(before, after, MortalJointTimeCommand.Absolute, new(170, false));
        Assert.Equal(MortalJointTimeKind.UnresolvedAbsolute, result.Kind);
        Assert.Equal(new WorldTimeResolutionResult(170, true), result.Progression);
        Assert.Null(result.EffectCurrentTime);
        Assert.Null(result.ElapsedMinutes);
        var timed = EffectMaterializationTestFixture.CreateCanonicalEffect();
        timed["lifetime"] = new JsonObject { ["mode"] = "until_time", ["deadline"] = 105L };
        var reduction = EffectLifecycleScheduler.AdvanceLifetime(new(timed,
            new("calendar-only", 43, CurrentTime: result.EffectCurrentTime), new HashSet<string>()));
        Assert.True(reduction.Success);
        Assert.Equal("no_change", reduction.Outcome);
        after[0] = 0;
        var detached = result.AfterBytes!;
        detached[0] = 0;
        Assert.Equal((byte)'{', result.AfterBytes![0]);
        Assert.Equal((byte)'{', result.BeforeBytes![0]);
    }

    [Fact]
    public void TimeProjection_PreservesDifferentActualProgressionAndEffectReaderPriorities()
    {
        var result = MortalJointActionBoundary.DescribeTime(null,
            Bytes("{\"currentTimeInMinutes\":450,\"setWorldTime\":{\"year\":10,\"monthName\":\"Moon\",\"dayOfMonth\":2,\"timeOfDay\":\"night\"}}"),
            MortalJointTimeCommand.Absolute, new(100, false));
        Assert.Equal(450, result.Progression.Minutes);
        Assert.False(result.Progression.HasUnresolvedAbsoluteOverride);
        Assert.Null(result.EffectCurrentTime);
        Assert.Null(result.ElapsedMinutes);
    }
}
