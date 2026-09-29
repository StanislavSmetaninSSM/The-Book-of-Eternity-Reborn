using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectResourceTriggerRoutingScaleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalPrefix_ClosesRealEffectRoutingAndReceiptBeforeAnyExchange(bool bounded)
    {
        var fixture = CreateAcceptedEventBudgetFixture(remainingUses: 1, current: 5m,
            triggerSpecs: new[] { new BudgetTriggerSpec("prefix_damage", "resource_damaged", 10, true, false,
                ResolutionMode: bounded ? "bounded_receipt" : "deterministic") });
        var route = EffectAcceptedTurnPlanner.BaseResourceRouting.Capture(fixture.Plan, fixture.Owners, fixture.Definitions);
        var mutation = CreateBudgetMutation(fixture.Coordinate, "turn_43:prefix:1", ResourceOperation.Damage, 1m);
        var input = new AcceptedMechanicsResourceInput(43, fixture.Definitions, fixture.State,
            fixture.History, fixture.Sources, new[] { mutation });
        using var session = AcceptedMechanicsPlanner.BeginLiveResourceExecution(input, new LiveIntervalIdentityCounter().Factory, route);
        session.BindOriginalPendingContext(PendingContinuationInput(fixture));
        var step = session.AdvanceOriginalPrefix();
        if (bounded)
        {
            Assert.Null(step.OriginalPrefix);
            var wait = Assert.IsType<AcceptedMechanicsPlanner.PendingSpiritualResourceExchange>(step.PendingResource);
            Assert.Null(wait.ExchangeId);
            var packet = session.ReadPendingResourceRequest(wait);
            var rejected = session.ResumePendingResource(wait, PendingReceipts(packet, 999m));
            Assert.Null(rejected.Step);
            Assert.NotEmpty(rejected.Issues);
            Assert.Equal(packet.ToJsonString(), session.ReadPendingResourceRequest(wait).ToJsonString());
            Assert.Throws<InvalidOperationException>(() => session.Drain());
            step = session.ResumePendingResource(wait, PendingReceipts(packet, 2m)).Step!;
        }
        var prefix = Assert.IsType<AcceptedMechanicsPlanner.OriginalResourcePrefix>(step.OriginalPrefix);
        Assert.True(session.Owns(prefix));
        Assert.Equal(2, prefix.AppliedTransitions.Count);
        Assert.Single(prefix.EffectPrefix.AcceptedActivations);
        var snapshot = prefix.State.ToCanonicalJson();
        var result = session.Drain();
        Assert.True(result.IsValid, Format(result.Issues));
        Assert.Equal(prefix.AppliedTransitions, result.AppliedTransitions);
        Assert.Equal(snapshot, result.StateAfterImage!.ToCanonicalJson());
    }
}
