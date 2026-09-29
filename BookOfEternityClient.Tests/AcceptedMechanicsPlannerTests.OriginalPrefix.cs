using System.Reflection;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AcceptedMechanicsPlannerTests
{
    [Fact]
    public void OriginalPrefix_ClosesOrdinaryWorkWithoutReplay()
    {
        var input = SessionOrdinaryInput();
        var controlCounter = new SessionAllocationCounter();
        var control = AcceptedMechanicsPlanner.BuildResources(input, controlCounter.Factory);
        Assert.True(control.IsValid, SessionIssues(control));
        Assert.NotEmpty(control.AppliedTransitions);
        var counter = new SessionAllocationCounter();
        using var session = AcceptedMechanicsPlanner.BeginLiveResourceExecution(input, counter.Factory);
        var method = session.GetType().GetMethod("AdvanceOriginalPrefix", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var step = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionStep>(method!.Invoke(session, null));
        Assert.Null(step.Result);
        Assert.Null(step.Interval);
        Assert.Null(step.PendingResource);
        var property = step.GetType().GetProperty("OriginalPrefix", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);
        Assert.NotNull(property!.GetValue(step));
        var prefix = step.OriginalPrefix!;
        Assert.True(session.Owns(prefix));
        Assert.Equal(control.StateAfterImage!.ToCanonicalJson(), prefix.State.ToCanonicalJson());
        Assert.Equal(control.AppliedTransitions, prefix.AppliedTransitions);
        Assert.Throws<InvalidOperationException>(() => session.AdvanceOriginalPrefix());
        var calls = counter.Calls;
        Assert.Equal(SessionResultImage(control), SessionResultImage(session.Drain()));
        Assert.Equal(calls, counter.Calls);
        Assert.Equal(controlCounter.Ids, counter.Ids);
    }

    [Fact]
    public void OriginalPrefix_ExchangeContinuesWithExactOwnerAndNoReplay()
    {
        var input = SessionOrdinaryInput();
        var ordinary = new AcceptedMechanicsResourceInput(2, input.Definitions, input.State,
            input.History, input.Sources, new[] { input.Mutations[0] });
        using var session = AcceptedMechanicsPlanner.BeginLiveResourceExecution(ordinary, new SessionAllocationCounter().Factory);
        var prefix = session.AdvanceOriginalPrefix().OriginalPrefix!;
        using var foreign = AcceptedMechanicsPlanner.BeginLiveResourceExecution(ordinary, new SessionAllocationCounter().Factory);
        Assert.False(foreign.Owns(prefix));
        var suffixInput = new AcceptedMechanicsResourceInput(2, input.Definitions, input.State,
            input.History, input.Sources, new[] { input.Mutations[1] });
        session.StageNextExchange(SessionLiveBatch(suffixInput, 0, 9m));
        Assert.Single(session.AdvanceThroughExchange().Interval!.AppliedTransitions);
        var result = session.Drain();
        Assert.True(result.IsValid, SessionIssues(result));
        Assert.Equal(2, result.AppliedTransitions.Count);
        Assert.Equal(9m, Assert.Single(prefix.State.Entries).Current);
        Assert.Equal(8m, Assert.Single(result.StateAfterImage!.Entries).Current);
        Assert.Single(prefix.AppliedTransitions);
        Assert.Equal(prefix.AppliedTransitions[0], result.AppliedTransitions[0]);
        session.Dispose();
        Assert.False(session.Owns(prefix));
    }

    [Fact]
    public void OriginalPrefix_RejectsFixedAndAlreadyStagedCalls()
    {
        var input = SessionOrdinaryInput();
        using var fixedSession = AcceptedMechanicsPlanner.BeginResourceExecution(input, new SessionAllocationCounter().Factory);
        Assert.Throws<InvalidOperationException>(() => fixedSession.AdvanceOriginalPrefix());
        using var live = AcceptedMechanicsPlanner.BeginLiveResourceExecution(SessionLiveBaseline(input), new SessionAllocationCounter().Factory);
        live.StageNextExchange(SessionLiveBatch(input, 0, 10m));
        Assert.Throws<InvalidOperationException>(() => live.AdvanceOriginalPrefix());
        Assert.NotNull(live.AdvanceThroughExchange().Interval);
        Assert.Throws<InvalidOperationException>(() => live.AdvanceOriginalPrefix());
    }

    [Fact]
    public void OriginalPrefix_EmptyPrefixIsOwnedButFailedPrefixIsNotProduced()
    {
        var input = SessionOrdinaryInput();
        using var empty = AcceptedMechanicsPlanner.BeginLiveResourceExecution(SessionLiveBaseline(input), new SessionAllocationCounter().Factory);
        var prefix = empty.AdvanceOriginalPrefix().OriginalPrefix!;
        Assert.True(empty.Owns(prefix));
        Assert.Empty(prefix.AppliedTransitions);
        Assert.Empty(prefix.Events);
        using var failed = AcceptedMechanicsPlanner.BeginLiveResourceExecution(SessionOrdinaryInput(100m), new SessionAllocationCounter().Factory);
        var step = failed.AdvanceOriginalPrefix();
        Assert.Null(step.OriginalPrefix);
        Assert.False(step.Result!.IsValid);
    }

    [Fact]
    public void OriginalPrefix_LaterFailedExchangeRevokesOwnership()
    {
        var input = SessionOrdinaryInput(100m);
        var ordinary = new AcceptedMechanicsResourceInput(2, input.Definitions, input.State,
            input.History, input.Sources, new[] { input.Mutations[0] });
        using var session = AcceptedMechanicsPlanner.BeginLiveResourceExecution(ordinary, new SessionAllocationCounter().Factory);
        var prefix = session.AdvanceOriginalPrefix().OriginalPrefix!;
        var forged = new AcceptedMechanicsPlanner.OriginalResourcePrefix(prefix.State,
            prefix.AppliedTransitions, prefix.ReplayTransitions, prefix.Events, prefix.EffectPrefix);
        Assert.False(session.Owns(forged));
        var suffix = new AcceptedMechanicsResourceInput(2, input.Definitions, input.State,
            input.History, input.Sources, new[] { input.Mutations[1] });
        session.StageNextExchange(SessionLiveBatch(suffix, 0, 9m));
        var failed = session.AdvanceThroughExchange();
        Assert.False(failed.Result!.IsValid);
        Assert.False(session.Owns(prefix));
    }
}
