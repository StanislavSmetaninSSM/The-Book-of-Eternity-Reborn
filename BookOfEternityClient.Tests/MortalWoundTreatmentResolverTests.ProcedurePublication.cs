using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public void ProcedureSingletonStabilization_PublishesWoundAnchorsRouteAndHistoryOnce()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        var route = scenario.Before["treatment"]!["routes"]![0]!.AsObject();
        route["outcomes"]![0]!["result"] = new JsonArray(new JsonObject
        {
            ["kind"] = "stabilize"
        });
        scenario = scenario with
        {
            OperationKey = scenario.OperationKey + "_singleton_stabilization",
            ExpectedIntentCount = 1,
            History = CreateCurrentWoundHistory(scenario.Before),
            SeedCanonicalWoundEffects = true
        };

        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateProcedurePublication(
            fixture,
            ResolveCurrentTreatment(
                fixture,
                "procedure",
                scenario.OperationKey,
                scenario.RouteId),
            "singleton stabilization");
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        Assert.Equal("success", resolution.ResultCategory);
        Assert.Equal("AppendOnce", resolution.RouteCompletion);
        Assert.Collection(
            resolution.OutcomeIntents,
            static intent => Assert.Equal("stabilize", intent.Kind));

        ComposeAndPublishCoordinatedProcedureTreatment(fixture, flow);

        var wound = fixture.ReadCurrentWound();
        Assert.Equal("stabilized", wound.Care.State);
        Assert.Equal(request.Coordinates.Turn, wound.Care.StabilizedAtTurn);
        Assert.Equal(request.Coordinates.AttemptId, wound.Care.LastAttemptId);
        Assert.Contains(request.Coordinates.RouteId, wound.Treatment.CompletedRouteIds);
        Assert.DoesNotContain("not_stabilized", wound.Recovery.Blockers);
        var recoveryAnchor = Assert.IsType<WoundRecoveryAnchor>(
            wound.Recovery.RecoveryAnchor);
        Assert.Equal("stabilization", recoveryAnchor.AnchorKind);
        Assert.Equal(1_260, recoveryAnchor.AnchorMinute);
        Assert.Equal(wound.LastTransition.TransitionId, recoveryAnchor.AnchorTransitionId);
        Assert.Null(wound.Recovery.DeteriorationAnchor);

        var transition = Assert.Single(
            fixture.ReadCurrentHistory().State!.Transitions,
            static row => string.Equals(row.Kind, "treat", StringComparison.Ordinal));
        Assert.Equal(wound.LastTransition.TransitionId, transition.TransitionId);
        Assert.Equal(request.Coordinates.OperationKey, transition.OperationKey);
        Assert.Equal(request.Coordinates.AttemptId, transition.AttemptId);
        Assert.Equal(request.RequestFingerprint, transition.SourceFingerprint);
        Assert.False(transition.Terminal);
        Assert.Equal("success", transition.TreatmentResult!.Receipt.ResultCategory);
    }

    [Fact]
    public void ProcedureNoImprovement_PublishesAttemptHistoryWithoutInventingImprovement()
    {
        var scenario = CreateScenario(
            "procedure_disadvantage_uses_two_contiguous_dice",
            "procedure");
        scenario = scenario with
        {
            OperationKey =
                "operation_t070_b5_procedure_no_improvement_publication",
            History = CreateCurrentWoundHistory(scenario.Before),
            SeedCanonicalWoundEffects = true
        };
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateProcedurePublication(
            fixture,
            ResolveCurrentTreatment(
                fixture,
                "procedure",
                scenario.OperationKey,
                scenario.RouteId),
            "no improvement");
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        Assert.Equal("failed_attempt", resolution.ResultCategory);
        Assert.Equal("None", resolution.RouteCompletion);
        Assert.Collection(
            resolution.OutcomeIntents,
            static intent => Assert.Equal("no_improvement", intent.Kind));
        var before = CanonicalWoundRoot(flow.Before);

        ComposeAndPublishCoordinatedProcedureTreatment(fixture, flow);

        var wound = fixture.ReadCurrentWound();
        var after = CanonicalWoundRoot(wound);
        AssertCanonicalWoundMembersUnchanged(
            before,
            after,
            "severity",
            "complications",
            "consequences",
            "treatment",
            "recovery",
            "relations");
        var expectedCare = before["care"]!.DeepClone().AsObject();
        expectedCare["lastAttemptId"] = request.Coordinates.AttemptId;
        Assert.True(
            JsonNode.DeepEquals(expectedCare, after["care"]),
            "A no-improvement result may record the accepted attempt but must not invent care progress.");

        var transition = Assert.Single(
            fixture.ReadCurrentHistory().State!.Transitions,
            static row => string.Equals(row.Kind, "treat", StringComparison.Ordinal));
        var treatmentResult = Assert.IsType<MortalWoundTreatmentPersistedResult>(
            transition.TreatmentResult);
        Assert.Equal(request.Coordinates.AttemptId, transition.AttemptId);
        Assert.False(transition.Terminal);
        Assert.Equal(
            "failed_attempt",
            treatmentResult.Receipt.ResultCategory);
        Assert.Equal(
            "AcceptedTerminal",
            treatmentResult.Receipt.AttemptDisposition);
    }

    [Fact]
    public void ProcedureNoResourceAttempt_StillRequiresCoordinatedTransaction()
    {
        var scenario = CreateNoResourceNoImprovementProcedureScenario();
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateProcedurePublication(
            fixture,
            ResolveCurrentTreatment(
                fixture,
                "procedure",
                scenario.OperationKey,
                scenario.RouteId),
            "no-resource procedure");
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        Assert.Equal("not_required", request.ResourceAuthority.ReservationDisposition);
        Assert.Null(request.ResourceAuthority.ReservationId);
        Assert.Empty(request.ResourceAuthority.Claims);

        var plan = ComposeCoordinatedProcedurePlan(fixture, flow);
        using var publication = PublishCachedResourcePlanOpen(
            fixture,
            flow,
            plan,
            requiresConfirmedResourceHold: false);

        Assert.NotNull(publication.TransactionToken);
        publication.CompleteAtFullPipelineEnd();
        Assert.Single(
            fixture.ReadCurrentHistory().State!.Transitions,
            static row => string.Equals(row.Kind, "treat", StringComparison.Ordinal));
    }

    [Fact]
    public void ProcedureScalarOutcomePlanner_IsDeterministicWriteFreeAndRejectsChangedCompletion()
    {
        var scenario = CreateScenario(
            "procedure_disadvantage_uses_two_contiguous_dice",
            "procedure");
        scenario = scenario with
        {
            History = CreateCurrentWoundHistory(scenario.Before),
            SeedCanonicalWoundEffects = true,
            OperationKey = "operation_t070_b5_scalar_outcome_planner"
        };
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey,
            scenario.RouteId);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            flow.AcceptedState);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        var carrierBefore = ReadCanonicalBytes(fixture, fixture.TargetCarrierPath);
        var identityBefore = ReadCanonicalBytes(fixture, WoundIdentityState.StatePath);
        var historyBefore = ReadCanonicalBytes(fixture, WoundHistoryState.HistoryPath);

        var first = MortalWoundTreatmentOutcomePublicationPlanner.Compose(
            acceptedState,
            request,
            resolution,
            acceptedState.CurrentGameMinute);
        var second = MortalWoundTreatmentOutcomePublicationPlanner.Compose(
            acceptedState,
            request,
            resolution,
            acceptedState.CurrentGameMinute);

        Assert.True(first.IsValid, DescribeIssues(first.Issues));
        Assert.True(second.IsValid, DescribeIssues(second.Issues));
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(first.TransitionId, second.TransitionId);
        Assert.Equal(
            WoundMaterializationContract.SerializeCanonical(first.After!),
            WoundMaterializationContract.SerializeCanonical(second.After!));
        Assert.Equal(carrierBefore, ReadCanonicalBytes(fixture, fixture.TargetCarrierPath));
        Assert.Equal(identityBefore, ReadCanonicalBytes(fixture, WoundIdentityState.StatePath));
        Assert.Equal(historyBefore, ReadCanonicalBytes(fixture, WoundHistoryState.HistoryPath));

        var changedCompletion = MortalWoundTreatmentResolution.Create(
            resolution.Mode,
            resolution.Coordinates,
            resolution.AttemptDisposition,
            resolution.ResultCategory,
            resolution.SelectedOutcomeIndex,
            resolution.Interruption,
            resolution.DeclaredResult,
            resolution.OutcomeIntents,
            resolution.CriticalReactionIntent,
            resolution.ConsumptionTrigger,
            resolution.CourseId,
            resolution.CourseMilestoneOrdinal,
            resolution.CourseDisposition,
            request,
            resolution.ModeEvidence,
            resolution.RouteFingerprint,
            "AppendOnce");
        var rejected = MortalWoundTreatmentOutcomePublicationPlanner.Compose(
            acceptedState,
            request,
            changedCompletion,
            acceptedState.CurrentGameMinute);

        Assert.False(rejected.IsValid);
        Assert.Null(rejected.After);
        Assert.Contains(
            rejected.Issues,
            static issue => issue.Code ==
                "mortal_wound_treatment_publication_slice_unsupported");
        Assert.Equal(carrierBefore, ReadCanonicalBytes(fixture, fixture.TargetCarrierPath));
        Assert.Equal(identityBefore, ReadCanonicalBytes(fixture, WoundIdentityState.StatePath));
        Assert.Equal(historyBefore, ReadCanonicalBytes(fixture, WoundHistoryState.HistoryPath));
    }

    private static ResolverScenario CreateNoResourceNoImprovementProcedureScenario()
    {
        var scenario = CreateScenario(
            "procedure_disadvantage_uses_two_contiguous_dice",
            "procedure");
        var route = scenario.Before["treatment"]!["routes"]![0]!.AsObject();
        var skillRequirement = route["requirements"]![1]!.DeepClone();
        route["requirements"] = new JsonArray(skillRequirement);
        route["resourcePolicy"] = Policy(new JsonArray(), new JsonArray());
        route["resolution"]!["modifierSource"]!["requirementIndex"] = 0;
        return scenario with
        {
            OperationKey = "operation_t070_b5_no_resource_procedure",
            ExpectedIntentCount = 1,
            History = CreateCurrentWoundHistory(scenario.Before),
            SeedCanonicalWoundEffects = true
        };
    }

    private static ResolverScenario PrepareProcedurePublicationScenario(
        ResolverScenario scenario) => scenario with
    {
        History = CreateCurrentWoundHistory(scenario.Before),
        SeedCanonicalWoundEffects = true
    };

    private static TreatmentFlow PersistAndRehydrateProcedurePublication(
        AcceptedStateFixture fixture,
        TreatmentFlow initial,
        string boundary)
    {
        PersistTreatmentCommand(
            fixture,
            ComposeTreatmentCommand(
                initial,
                "The accepted procedure is persisted before canonical publication."));
        var initialRequest = Assert.IsType<MortalWoundTreatmentAttemptRequest>(
            initial.Request);
        var restored = Assert.Single(
            AssertValidPersistedCatalog(
                RestoreCurrentPersistedTreatmentCatalog(fixture),
                boundary + " persisted procedure"),
            candidate => string.Equals(
                Assert.IsType<MortalWoundTreatmentAttemptRequest>(candidate)
                    .RequestFingerprint,
                initialRequest.RequestFingerprint,
                StringComparison.Ordinal));
        var rehydrated = RehydratePersistedTreatment(
            fixture,
            "procedure",
            restored);
        Assert.Equal(
            CanonicalValue(initial.Request),
            CanonicalValue(rehydrated.Request));
        Assert.Equal(
            CanonicalValue(initial.Resolution),
            CanonicalValue(rehydrated.Resolution));
        return rehydrated;
    }

    private static AcceptedMechanicsPlan ComposeAndPublishCoordinatedProcedureTreatment(
        AcceptedStateFixture fixture,
        TreatmentFlow flow,
        GameResponse? proposal = null)
    {
        var plan = ComposeCoordinatedProcedurePlan(fixture, flow, proposal);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        using var publication = PublishCachedResourcePlanOpen(
            fixture,
            flow,
            plan,
            requiresConfirmedResourceHold: string.Equals(
                request.ResourceAuthority.ReservationDisposition,
                "held",
                StringComparison.Ordinal));
        publication.CompleteAtFullPipelineEnd();
        return plan;
    }

    private static AcceptedMechanicsPlan ComposeCoordinatedProcedurePlan(
        AcceptedStateFixture fixture,
        TreatmentFlow flow,
        GameResponse? proposal = null)
    {
        var result = ComposeResourcePublicationResult(fixture, flow, proposal);
        Assert.True(result.IsValid, DescribeIssues(result.Issues));
        Assert.Empty(result.Issues);
        return Assert.IsType<AcceptedMechanicsPlan>(result.Plan);
    }

    private static JsonObject CanonicalWoundRoot(WoundMaterializationEnvelope wound) =>
        JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(wound))!
            .AsObject();

    private static void AssertCanonicalWoundMembersUnchanged(
        JsonObject before,
        JsonObject after,
        params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            Assert.True(
                JsonNode.DeepEquals(before[propertyName], after[propertyName]),
                $"No-improvement publication changed wound.{propertyName}.");
        }
    }
}
