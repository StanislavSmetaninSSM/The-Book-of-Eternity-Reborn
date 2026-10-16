using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectAcceptedTurnPlannerTests
{
    [Fact]
    public void CommonBoundary_RealDiscoveredBoundedResultCannotBecomeCompletedEmpty()
    {
        var fixture = CreateOrdinaryReductionFixture(effects: true, bounded: true);
        var reduction = AcceptedMechanicsPlanner.ReduceAcceptedPlan(fixture.Input,
            AcceptedMechanicsPlanFingerprints.ComputePlanningInput(fixture.Input));
        Assert.Equal(AcceptedMechanicsPlanner.AcceptedMechanicsReductionKind.AwaitingResourceReceipt,
            reduction.Kind);
        Assert.Null(reduction.Completed);
        Assert.NotEmpty(reduction.SelectedResources!.AcceptedPendingResolutions);
        Assert.Contains(AcceptedMechanicsPlanner.ValidateUnboundedCommonBoundary(
                fixture.Input.SessionId, fixture.Input.RequestId, fixture.Input.Turn,
                null, new JsonObject(), reduction.SelectedResources.AcceptedPendingResolutions),
            issue => issue.Code == "resource_pending_candidates_not_unbounded");
        Assert.Equal(0, fixture.Outcome.ProjectCalls);
    }
}
