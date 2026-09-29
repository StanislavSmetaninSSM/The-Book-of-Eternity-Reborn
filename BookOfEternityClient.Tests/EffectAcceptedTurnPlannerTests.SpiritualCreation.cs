using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectAcceptedTurnPlannerTests
{
    /// <summary>
    /// Requires real planner creation sites to use retained definition identities and pending clocks.
    /// </summary>
    /// <param name="contour">
    /// Ordinary definition, original executor definition or bounded pending preparation.
    /// </param>
    [Theory]
    [InlineData("ordinary_definition")]
    [InlineData("original_definition")]
    [InlineData("pending")]
    public void SpiritualReplay_ActualResourceCreationSitesUseJournal(string contour)
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var fixture = CreateOrdinaryReductionFixture(effects: contour != "ordinary_definition",
            bounded: contour == "pending", createDefinition: contour != "pending");
        var first = SpiritualCreationImage(contour, fixture.Input, journal);
        var rows = journal.Export();
        Assert.Contains(rows, row => row!["kind"]!.GetValue<string>() ==
            (contour == "pending" ? "utc_time" : "resource_definition"));
        Assert.Contains(rows, row => row!["kind"]!.GetValue<string>() ==
            (contour == "pending" ? "resource_resolution" : "resource_definition_seal"));
        var replay = SpiritualWoundReplayJournal.CreateReplay(rows.ToJsonString());
        Assert.Equal(first, SpiritualCreationImage(contour, fixture.Input, replay));
        Assert.Equal(rows.ToJsonString(), replay.Export().ToJsonString());
    }

    /// <summary>
    /// Runs a fresh unpublished owner reduction over the same retained baseline with a journal adapter.
    /// </summary>
    /// <param name="contour">
    /// The definition or pending path to exercise.
    /// </param>
    /// <param name="input">
    /// Original input, including the already allocated pre-turn bootstrap history.
    /// </param>
    /// <param name="journal">
    /// Attempt's recording or strict replay stream.
    /// </param>
    /// <returns>
    /// Full retained definition or pending image for exact comparison.
    /// </returns>
    private static string SpiritualCreationImage(string contour, AcceptedMechanicsInput input,
        SpiritualWoundReplayJournal journal)
    {
        var context = input.PlanningContext!;
        var factory = new SpiritualWoundResourceIdentityFactory(journal, new AcceptedMechanicsIdentityFactory());
        var rebound = input.WithPlanningContext(new AcceptedMechanicsPlanningContext(
            context.DefinitionRoot, context.Definitions, context.State, context.History, context.Owners,
            context.Sources, context.Commands, context.EffectIdentityRoot, context.EffectPlan,
            context.CapacityTransitions, context.OwnerCapacityDrafts, context.TerminalOwners,
            context.OwnerCompanionAfterImages, context.OwnerTransitions, context.RegisteredSystemOutcomes,
            context.PendingResolutionState, factory, context.EffectIdentityFactory, context.ExecutionSequenceOffset,
            context.WoundStageBundle, context.WoundAnchorPlan, context.DirectWoundPublicationAuthority,
            context.TreatmentResourcePublicationAuthority));
        if (contour == "original_definition")
        {
            var begun = AcceptedMechanicsPlanner.BeginOriginalMortalResourceExecution(rebound);
            Assert.Empty(begun.Issues);
            using var session = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(begun.Session);
            var baseline = Assert.IsType<AcceptedMechanicsResourceInput>(session.GetType()
                .GetField("_originalResourceInput", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(session));
            return baseline.Definitions.ToCanonicalJson();
        }
        var reduction = AcceptedMechanicsPlanner.ReduceAcceptedPlan(rebound,
            AcceptedMechanicsPlanFingerprints.ComputePlanningInput(rebound));
        Assert.Empty(reduction.Issues);
        if (contour == "ordinary_definition")
        {
            Assert.NotNull(reduction.Completed);
            return reduction.Completed.Definitions.ToCanonicalJson();
        }
        Assert.Equal(AcceptedMechanicsPlanner.AcceptedMechanicsReductionKind.AwaitingResourceReceipt, reduction.Kind);
        var completed = AcceptedMechanicsPlanner.CompleteAcceptedReduction(reduction);
        Assert.True(completed.Success, DescribeOrdinaryReductionIssues(completed.Issues));
        return completed.Plan!.PendingAfterImages[ResourcePendingResolutionState.PendingPath]!.ToJsonString();
    }
}
