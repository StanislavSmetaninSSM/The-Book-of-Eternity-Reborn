using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AcceptedMechanicsPlanCacheTests
{
    [Fact]
    public void WoundPublicationWriteSet_IncludesTypedCarrierIdentityAndHistory()
    {
        var plan = BuildTypedWoundCarrierPublicationPlan();

        var writes = CanonicalStateNormalizer.ComposeWoundPublicationWrites(plan);

        Assert.Equal(3, writes.Count);
        Assert.True(JsonNode.DeepEquals(
            plan.WoundCarrierAfterImages[WoundCarrierCatalog.PlayerPath],
            writes[WoundCarrierCatalog.PlayerPath]));
        Assert.True(JsonNode.DeepEquals(
            plan.WoundIdentityAfterImage,
            writes[WoundIdentityState.StatePath]));
        Assert.True(JsonNode.DeepEquals(
            plan.WoundHistoryAfterImage,
            writes[WoundHistoryState.HistoryPath]));
    }

    private static AcceptedMechanicsPlan BuildTypedWoundCarrierPublicationPlan()
    {
        var woundInput = WoundEffectBatchPlannerTests
            .CreateNoMechanicsInputForAcceptedCache();
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(woundInput));
        var stages = CreateWoundCommonStages(
            woundInput,
            prepared,
            "normalizer_wound_publication");
        var baselineInput = CreateWoundCommonAcceptedInput(stages.Bundle);
        var baselineContext = baselineInput.PlanningContext!;
        var context = new AcceptedMechanicsPlanningContext(
            baselineContext.DefinitionRoot,
            baselineContext.Definitions,
            baselineContext.State,
            baselineContext.History,
            baselineContext.Owners,
            baselineContext.Sources,
            baselineContext.Commands,
            baselineContext.EffectIdentityRoot,
            stages.Effect.EffectPlan,
            baselineContext.CapacityTransitions,
            baselineContext.OwnerCapacityDrafts,
            baselineContext.TerminalOwners,
            baselineContext.OwnerCompanionAfterImages,
            baselineContext.OwnerTransitions,
            baselineContext.RegisteredSystemOutcomes,
            baselineContext.PendingResolutionState,
            baselineContext.ResourceIdentityFactory,
            baselineContext.EffectIdentityFactory,
            baselineContext.ExecutionSequenceOffset,
            stages.Bundle);
        var input = new AcceptedMechanicsInput(
            baselineInput.SessionId,
            baselineInput.RequestId,
            baselineInput.SnapshotToken,
            baselineInput.Realm,
            baselineInput.Turn,
            baselineInput.AcceptedEvents,
            baselineInput.ResourceCommands,
            baselineInput.EffectCommands,
            baselineInput.PendingInput,
            baselineInput.InternalInputs,
            baselineInput.AuthorityFingerprints,
            baselineInput.BeforeImages,
            baselineInput.ValidationIssues,
            context,
            baselineInput.WoundCommands,
            baselineInput.WoundInput);
        var result = AcceptedMechanicsPlanner.BuildAcceptedPlan(
            input,
            AcceptedMechanicsPlanFingerprints.ComputeInput(
                input.CreateBinding()));
        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Issues.Select(static issue =>
                $"{issue.Code}: {issue.Message}")));
        return Assert.IsType<AcceptedMechanicsPlan>(result.Plan);
    }
}
