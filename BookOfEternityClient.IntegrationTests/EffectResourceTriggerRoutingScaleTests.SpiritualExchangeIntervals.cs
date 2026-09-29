using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectResourceTriggerRoutingScaleTests
{
    [Fact]
    public void SpiritualExchangeInterval_CausalDescendantsCloseAndUsesNeverReset()
    {
        var fixture = CreateAcceptedEventBudgetFixture(remainingUses: 2, current: 5m,
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec("live_damage", "resource_damaged", 10, true, false),
                new BudgetTriggerSpec("live_restore", "resource_restored", 20, true, false)
            });
        var routing = EffectAcceptedTurnPlanner.BaseResourceRouting.Capture(
            fixture.Plan, fixture.Owners, fixture.Definitions);
        var counter = new LiveIntervalIdentityCounter();
        using var session = AcceptedMechanicsPlanner.BeginLiveResourceExecution(
            LiveBudgetBaseline(fixture), counter.Factory, routing);
        var firstMutation = CreateBudgetMutation(fixture.Coordinate,
            "turn_43:live_budget:1", ResourceOperation.Damage, 1m);
        session.StageNextExchange(LiveBudgetBatch(fixture, firstMutation, 0, 5m));
        var first = session.AdvanceThroughExchange().Interval!;
        Assert.NotNull(first);
        Assert.True(first.AppliedTransitions.Count > 1);
        Assert.Equal(2, first.EffectAfter.AcceptedActivations.Count);
        Assert.Equal(first.EffectAfter.Boundaries.Count, first.EffectAfter.BoundaryCloses.Count);
        Assert.Equal(new[] { 2, 1 }, first.EffectAfter.AcceptedActivations
            .Select(value => value.Activation.Stamp.UsesBefore!.Value).ToArray());
        var current = first.AppliedTransitions.Last(transition =>
            ResourceCoordinateComparer.Instance.Equals(transition.Coordinate, fixture.Coordinate)).AfterState!.Current;
        var calls = counter.Calls;
        var secondMutation = CreateBudgetMutation(fixture.Coordinate,
            "turn_43:live_budget:2", ResourceOperation.Damage, 1m, new[] { firstMutation.Key });
        session.StageNextExchange(LiveBudgetBatch(fixture, secondMutation, 1, current));
        Assert.Equal(calls, counter.Calls);
        var second = session.AdvanceThroughExchange().Interval!;
        Assert.Single(second.AppliedTransitions);
        Assert.Equal(second.Activations.Start, second.Activations.End);
        var result = session.Drain();
        Assert.True(result.IsValid, Format(result.Issues));
        Assert.Equal(2, result.ResourceTriggerExecutions.Count);
        Assert.Equal(2, result.EffectBoundaryTranscript.AcceptedActivations.Count);
        Assert.Equal(first.AppliedTransitions.Select(value => value.TransitionId),
            result.AppliedTransitions.Take(first.AppliedTransitions.Count).Select(value => value.TransitionId));

        var fixedResult = AcceptedMechanicsPlanner.BuildResources(new AcceptedMechanicsResourceInput(
            43, fixture.Definitions, fixture.State, fixture.History, fixture.Sources,
            new[] { firstMutation, secondMutation }, EventMutationResolver: routing.Resolve,
            EffectPlanAuthority: routing.PlanAuthority), new LiveIntervalIdentityCounter().Factory);
        Assert.True(fixedResult.IsValid, Format(fixedResult.Issues));
        Assert.Equal(LiveTransitionSemantics(fixedResult), LiveTransitionSemantics(result));
        Assert.Equal(LiveCausalSemantics(fixedResult), LiveCausalSemantics(result));
        Assert.Equal(result.AppliedTransitions.Count,
            result.AppliedTransitions.Select(value => value.OperationId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(result.AppliedTransitions.Count,
            result.AppliedTransitions.Select(value => value.TransitionId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(fixedResult.AppliedTransitions.Count,
            fixedResult.AppliedTransitions.Select(value => value.TransitionId).Distinct(StringComparer.Ordinal).Count());
        var pairs = fixedResult.AppliedTransitions.Zip(result.AppliedTransitions).ToArray();
        Assert.All(pairs, pair => Assert.Equal(pair.First.EventRef, pair.Second.EventRef));
        Assert.Equal(pairs.Length, pairs.Select(pair => pair.First.OperationId).Distinct().Count());
        Assert.Equal(pairs.Length, pairs.Select(pair => pair.Second.OperationId).Distinct().Count());
    }

    [Fact]
    public void SpiritualExchangeInterval_UnusedBaseSeedExistsBeforeItsFirstLaterCandidate()
    {
        var fixture = CreateAcceptedEventBudgetFixture(remainingUses: 1, current: 4m,
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec("live_depleted", "resource_depleted", 10, true, false)
            });
        var route = EffectAcceptedTurnPlanner.BaseResourceRouting.Capture(
            fixture.Plan, fixture.Owners, fixture.Definitions);
        Assert.Equal(1, Assert.Single(route.CanonicalUseSeeds).RemainingUses);
        Assert.Throws<NotSupportedException>(() => ((IList<CanonicalEffectUseSeed>)route.CanonicalUseSeeds).Clear());
        var originalCarrier = fixture.Plan.ResourceTriggerCarriers;
        Assert.NotNull(originalCarrier);
        using var session = AcceptedMechanicsPlanner.BeginLiveResourceExecution(
            LiveBudgetBaseline(fixture), new LiveIntervalIdentityCounter().Factory, route);
        var restore = CreateBudgetMutation(fixture.Coordinate,
            "turn_43:live_seed:1", ResourceOperation.Restore, 1m);
        session.StageNextExchange(LiveBudgetBatch(fixture, restore, 0, 4m));
        Assert.Empty(session.AdvanceThroughExchange().Interval!.EffectAfter.AcceptedActivations);
        var damage = CreateBudgetMutation(fixture.Coordinate,
            "turn_43:live_seed:2", ResourceOperation.Damage, 5m, new[] { restore.Key });
        session.StageNextExchange(LiveBudgetBatch(fixture, damage, 1, 5m));
        var second = session.AdvanceThroughExchange().Interval!;
        Assert.Equal(1, Assert.Single(second.EffectAfter.AcceptedActivations).Activation.Stamp.UsesBefore);
        Assert.True(session.Drain().IsValid);
        Assert.Equal(1, Assert.Single(route.CanonicalUseSeeds).RemainingUses);
    }

    [Fact]
    public void SpiritualExchangeInterval_RealPendingReceiptCannotMintAnInterval()
    {
        var fixture = CreateAcceptedEventBudgetFixture(remainingUses: 1, current: 5m,
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec("live_pending", "resource_damaged", 10, true, false,
                    ResolutionMode: "bounded_receipt")
            });
        var route = EffectAcceptedTurnPlanner.BaseResourceRouting.Capture(
            fixture.Plan, fixture.Owners, fixture.Definitions);
        using var session = AcceptedMechanicsPlanner.BeginLiveResourceExecution(
            LiveBudgetBaseline(fixture), new LiveIntervalIdentityCounter().Factory, route);
        var damage = CreateBudgetMutation(fixture.Coordinate,
            "turn_43:live_pending:1", ResourceOperation.Damage, 1m);
        session.StageNextExchange(LiveBudgetBatch(fixture, damage, 0, 5m));
        var step = session.AdvanceThroughExchange();
        Assert.Null(step.Interval);
        Assert.Null(step.Result);
        Assert.NotEmpty(step.PendingResource!.RequiredOutputs);
        Assert.Equal(0, step.PendingResource.Statistics.HistoryFreezeCount);
        Assert.Null(session.Result);
        var output = step.PendingResource.RequiredOutputs[0];
        var originalSource = output.Source.ToJsonString();
        output.Source.Clear();
        Assert.Equal(originalSource, step.PendingResource.RequiredOutputs[0].Source.ToJsonString());
        Assert.Throws<InvalidOperationException>(() => session.Drain());
        Assert.Throws<InvalidOperationException>(() => session.StageNextExchange(
            LiveBudgetBatch(fixture, damage, 1, 4m)));
    }

    [Fact]
    public void SpiritualExchangeInterval_BaseRouteRejectsForeignDefinitionsWithoutAllocating()
    {
        var fixture = CreateAcceptedEventBudgetFixture(remainingUses: 1, current: 5m,
            triggerSpecs: new[] { new BudgetTriggerSpec("live_damage", "resource_damaged", 10, true, false) });
        var route = EffectAcceptedTurnPlanner.BaseResourceRouting.Capture(
            fixture.Plan, fixture.Owners, fixture.Definitions);
        var counter = new LiveIntervalIdentityCounter();
        Assert.Throws<ArgumentException>(() => AcceptedMechanicsPlanner.BeginLiveResourceExecution(
            new AcceptedMechanicsResourceInput(43, ResourceDefinitionCatalog.CreateBuiltIn(),
                fixture.State, fixture.History, fixture.Sources, Array.Empty<ResourceMutationIntent>()),
            counter.Factory, route));
        Assert.Equal(0, counter.Calls);
    }

    private static AcceptedMechanicsResourceInput LiveBudgetBaseline(AcceptedEventBudgetFixture fixture) =>
        new(43, fixture.Definitions, fixture.State, fixture.History, fixture.Sources,
            Array.Empty<ResourceMutationIntent>());

    private static AfterlifeSpiritualConflictResourceOutcome.ExchangeBatch LiveBudgetBatch(
        AcceptedEventBudgetFixture fixture, ResourceMutationIntent mutation, int ordinal, decimal before)
    {
        var adds = mutation.Source.Operation is ResourceOperation.Gain or ResourceOperation.Restore;
        return new AfterlifeSpiritualConflictResourceOutcome.ExchangeBatch(
            "live_budget_conflict", "live_budget_exchange_" + ordinal, ordinal,
            AfterlifeSpiritualConflictResourceOutcome.SideEvaluation.EvaluatedMutation,
            AfterlifeSpiritualConflictResourceOutcome.SideEvaluation.EvaluatedZero,
            fixture.Sources.Exports, new[] { mutation },
            new[]
            {
                new AfterlifeSpiritualConflictResourceOutcome.ExpectedTransition(
                    mutation.EventRef, mutation.Source.SourceKind, mutation.Source.SourceId,
                    mutation.Coordinate, Enum.Parse<ResourceTransitionOperation>(mutation.Source.Operation.ToString()),
                    before, before + (adds ? mutation.Amount : -mutation.Amount), mutation.Amount)
            });
    }

    private static string LiveTransitionSemantics(AcceptedMechanicsResourcePlanningResult result) =>
        JsonSerializer.Serialize(result.AppliedTransitions.Select(value => new
        {
            value.EventRef, value.OriginKind, value.OriginId, value.Phase, value.Priority,
            value.ExecutionSequence, value.Coordinate, value.Operation, value.RequestedAmount,
            value.AppliedAmount, value.Outcome, value.CapacityDisposition, value.BeforeState, value.AfterState, value.SourceEvidence,
            value.PolicyFingerprint, value.ReceiptId, value.Turn
        }));

    private static string LiveCausalSemantics(AcceptedMechanicsResourcePlanningResult result)
    {
        var operations = result.AppliedTransitions.Select((value, index) => (value.OperationId, index))
            .ToDictionary(value => value.OperationId, value => value.index, StringComparer.Ordinal);
        var transitions = result.AppliedTransitions.Select((value, index) => (value.TransitionId, index))
            .ToDictionary(value => value.TransitionId, value => value.index, StringComparer.Ordinal);
        var transcript = Assert.IsType<AcceptedEffectBoundaryTranscript>(
            result.EffectBoundaryTranscript);
        foreach (var closure in transcript.CausalClosures)
        {
            Assert.Equal(closure.OperationIds.Count,
                closure.OperationIds.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(closure.OperationIds.Count, closure.ReplayStableOperationKeys.Count);
            Assert.Equal(closure.ReplayStableOperationKeys.Count,
                closure.ReplayStableOperationKeys.Distinct(StringComparer.Ordinal).Count());
        }
        return JsonSerializer.Serialize(new
        {
            Events = result.Events.Select(value => new
            {
                value.EventKind, Operation = operations[value.OperationId], value.EventRef,
                value.Coordinate, value.Before, value.After, value.AppliedAmount,
                value.Turn, value.ExecutionSequence, value.SourceFingerprint
            }),
            Boundaries = transcript.Boundaries.Select(value => new
            {
                value.BoundaryOrdinal, value.ParentBoundaryOrdinal, value.Producer,
                value.EventKind, value.ProducerEventRef,
                ProducerTransition = value.ProducerTransitionId == null ? (int?)null :
                    transitions[value.ProducerTransitionId],
                value.ProducerExecutionSequence, value.ProducerMechanicsOrdinal,
                value.OpenMechanicsOrdinal
            }),
            Closes = transcript.BoundaryCloses.Select(value => new
            {
                value.Boundary.BoundaryOrdinal, value.MechanicsOrdinal
            }),
            CausalClosures = transcript.CausalClosures.Select(value => new
            {
                value.Boundary.BoundaryOrdinal,
                AppliedOperations = value.OperationIds.Where(operations.ContainsKey)
                    .Select(operation => operations[operation]).OrderBy(index => index),
                UnappliedOperationCount = value.OperationIds.Count(operation => !operations.ContainsKey(operation)),
                value.ReplayStableOperationKeys
            }),
            Activations = transcript.AcceptedActivations.Select(value => new
            {
                value.Boundary.BoundaryOrdinal, value.Activation, value.MechanicsOrdinal
            }),
            Rejections = transcript.RejectedActivations.Select(value => new
            {
                value.Boundary.BoundaryOrdinal, value.Candidate.Activation,
                value.Reason, value.BlockedAvailabilityEffectId
            }),
            Components = transcript.AppliedComponentEvidence.Select(value => new
            {
                value.Boundary.BoundaryOrdinal, value.Activation, value.Mutation,
                value.ComponentId, Transition = transitions[value.Transition.TransitionId],
                value.MechanicsOrdinal
            }),
            Mutations = transcript.ResourceMutations.Select(value => new
            {
                value.Mutation, Transition = transitions[value.Transition.TransitionId],
                value.ExecutionKind, value.MechanicsOrdinal
            }),
            Reservations = transcript.TerminalAvailabilityReservations.Select(value => new
            {
                value.Boundary.BoundaryOrdinal, value.Activation, value.Subject,
                value.Kind, value.ReactionFingerprint, value.ReplayStableOrderKey, value.MechanicsOrdinal
            })
        });
    }

    private sealed class LiveIntervalIdentityCounter
    {
        internal LiveIntervalIdentityCounter() =>
            Factory = new AcceptedMechanicsIdentityFactory(() => new Guid(++Calls, 0, 0, new byte[8]));
        internal int Calls;
        internal AcceptedMechanicsIdentityFactory Factory { get; }
    }
}
