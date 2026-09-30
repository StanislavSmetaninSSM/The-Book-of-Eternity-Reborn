using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    private static ResolverScenario CreateScalarCoursePublicationScenario()
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time", "course");
        scenario.Before["treatment"]!["routes"]![0]!["outcomes"]![2]!["result"] =
            new JsonArray(new JsonObject { ["kind"] = "stabilize" });
        return scenario with
        {
            OperationKey = "operation_t070_scalar_course",
            History = CreateCurrentWoundHistory(scenario.Before),
            SeedCanonicalWoundEffects = true
        };
    }

    [Fact]
    public void CourseScalarPublication_ThreeMilestonesRestartAndCompleteWithoutHealing()
    {
        var scenario = CreateScalarCoursePublicationScenario();
        using var fixture = AcceptedStateFixture.Create(scenario);
        string? courseId = null;
        for (var ordinal = 1; ordinal <= 3; ordinal++)
        {
            if (ordinal > 1)
            {
                fixture.RestartForReplay();
                fixture.PrepareNextTurn(41 + ordinal, (ordinal - 1) * 480,
                    "scalar_course_" + ordinal);
            }
            var flow = ResolveCurrentTreatment(fixture, "course",
                scenario.OperationKey + "_" + ordinal, scenario.RouteId);
            AssertCourseAcceptedResolution(flow, ordinal,
                ordinal == 3 ? "completed" : "active", interruption: false);
            var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
            courseId ??= resolution.CourseId;
            Assert.False(string.IsNullOrWhiteSpace(courseId));
            Assert.Equal(courseId, resolution.CourseId);
            Assert.Equal(ordinal == 3 ? "AppendOnce" : "None", resolution.RouteCompletion);
            ComposeAndPublishTreatment(fixture, flow);
            var wound = fixture.ReadCurrentWound();
            Assert.Equal(ordinal == 3 ? null : courseId, wound.Care.ActiveCourseId);
            Assert.Equal(ordinal == 1 ? 2 : 1, wound.Severity.Rank);
            Assert.Equal(8 - ordinal, fixture.ReadPlayerItemCount("antibiotic_dose"));
            fixture.AssertItemIdentityIndexValid();
            Assert.Equal(ordinal, fixture.ReadCurrentHistory().State!.Transitions
                .Count(static row => row.Kind == "treat"));
            var row = fixture.ReadCurrentHistory().State!.Transitions.Last();
            Assert.Equal(courseId, row.CourseId);
            Assert.Equal(ordinal, row.CourseMilestoneOrdinal);
            Assert.Equal(row.CourseId, row.TreatmentResult!.Receipt.CourseId);
            Assert.Equal(row.CourseMilestoneOrdinal, row.TreatmentResult.Receipt.CourseMilestoneOrdinal);
            var replay = ProbePublishedTreatment(fixture, flow.Request);
            Assert.Equal("ExactReplay", Convert.ToString(ReadRequiredProperty(replay, "Status")));
            AssertClosedTreatmentReceipt(ReadRequiredProperty(replay, "Receipt"),
                flow.Request, flow.Resolution);
        }
        fixture.RestartForReplay();
        Assert.Null(fixture.ReadCurrentWound().Care.ActiveCourseId);
        Assert.Equal(3, fixture.ReadCurrentHistory().State!.Transitions
            .Count(static row => row.Kind == "treat"));
    }

    [Fact]
    public void CourseScalarPublication_SingletonCompletedAppendsOnceWithoutLeavingActivePointer() =>
        AssertSingletonScalarCoursePublication(alreadyCompleted: false);

    [Fact]
    public void CourseScalarPublication_FinalAlreadyCompletedRouteDoesNotAppendAgain() =>
        AssertSingletonScalarCoursePublication(alreadyCompleted: true);

    private static void AssertSingletonScalarCoursePublication(bool alreadyCompleted)
    {
        var scenario = CreateScalarCoursePublicationScenario();
        var route = scenario.Before["treatment"]!["routes"]![0]!;
        route["outcomes"] = new JsonArray(CourseMilestone(1, 0, "completed",
            new JsonArray(new JsonObject { ["kind"] = "stabilize" })));
        route["resourcePolicy"]!["mutations"] = new JsonArray(CourseMutation(1));
        if (alreadyCompleted)
            scenario.Before["treatment"]!["completedRouteIds"] = new JsonArray(scenario.RouteId);
        scenario = scenario with { History = CreateCurrentWoundHistory(scenario.Before) };
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(fixture, "course", scenario.OperationKey, scenario.RouteId);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        Assert.Equal(alreadyCompleted ? "None" : "AppendOnce", resolution.RouteCompletion);
        ComposeAndPublishTreatment(fixture, flow);
        var wound = fixture.ReadCurrentWound();
        Assert.Null(wound.Care.ActiveCourseId);
        Assert.Equal("stabilized", wound.Care.State);
        Assert.Equal(scenario.RouteId, Assert.Single(wound.Treatment.CompletedRouteIds));
        Assert.Single(fixture.ReadCurrentHistory().State!.Transitions, static row => row.Kind == "treat");
        var tree = CaptureResolverFixtureTree(fixture.Root);
        var replay = ProbePublishedTreatment(fixture, flow.Request);
        Assert.Equal("ExactReplay", Convert.ToString(ReadRequiredProperty(replay, "Status")));
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
    }

    [Fact]
    public void CourseScalarPublication_ActivePositiveMilestonePreservesCourseAndRetainedEffectGraph()
    {
        var scenario = CreateScalarCoursePublicationScenario();
        scenario.Before["severity"]!["value"] = "III";
        scenario.Before["severity"]!["rank"] = 3;
        scenario.Before["severity"]!["maximumAtCreation"] = "III";
        scenario.Before["consequences"]!["slotBudget"] = 3;
        var route = scenario.Before["treatment"]!["routes"]![0]!;
        route["outcomes"] = new JsonArray(
            CourseMilestone(1, 0, "active", new JsonArray(
                new JsonObject { ["kind"] = "stabilize" },
                new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 })),
            CourseMilestone(2, 480, "completed", new JsonArray(new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 })));
        route["resourcePolicy"]!["mutations"] = new JsonArray(CourseMutation(1), CourseMutation(2));
        scenario = scenario with { History = CreateCurrentWoundHistory(scenario.Before) };
        using var fixture = AcceptedStateFixture.Create(scenario);
        var before = fixture.ReadCurrentWound();
        var first = ResolveCurrentTreatment(fixture, "course", scenario.OperationKey, scenario.RouteId);
        var courseId = Assert.IsType<MortalWoundTreatmentResolution>(first.Resolution).CourseId;
        ComposeAndPublishTreatment(fixture, first);
        var after = fixture.ReadCurrentWound();
        Assert.Equal(courseId, after.Care.ActiveCourseId);
        Assert.Equal(2, after.Severity.Rank);
        Assert.Equal("stabilized", after.Care.State);
        var priorRoots = before.Consequences.OwnedEffectSources.RootBindings;
        var roots = after.Consequences.OwnedEffectSources.RootBindings;
        Assert.Equal(priorRoots.Count, roots.Count);
        Assert.NotEmpty(roots);
        Assert.Equal(priorRoots.Select(root => root.DefinitionKey), roots.Select(root => root.DefinitionKey));
        Assert.Empty(priorRoots.Select(root => root.EffectId).Intersect(roots.Select(root => root.EffectId)));
        fixture.PrepareNextTurn(43, 480, "active_positive_final");
        var final = ResolveCurrentTreatment(fixture, "course", scenario.OperationKey + "_final", scenario.RouteId);
        ComposeAndPublishTreatment(fixture, final);
        Assert.Equal(1, fixture.ReadCurrentWound().Severity.Rank);
        Assert.Equal("stabilized", fixture.ReadCurrentWound().Care.State);
        Assert.Empty(roots.Select(root => root.EffectId).Intersect(
            fixture.ReadCurrentWound().Consequences.OwnedEffectSources.RootBindings.Select(root => root.EffectId)));
        Assert.Null(fixture.ReadCurrentWound().Care.ActiveCourseId);
        Assert.Equal(scenario.RouteId, Assert.Single(fixture.ReadCurrentWound().Treatment.CompletedRouteIds));
        Assert.Equal(6, fixture.ReadPlayerItemCount("antibiotic_dose"));
    }

    [Fact]
    public void CourseScalarPublication_NonCourseTreatmentPreservesTheExactActivePointer()
    {
        var scenario = CreateScalarCoursePublicationScenario();
        scenario.Before["severity"]!["value"] = "III";
        scenario.Before["severity"]!["rank"] = 3;
        scenario.Before["severity"]!["maximumAtCreation"] = "III";
        scenario.Before["consequences"]!["slotBudget"] = 3;
        scenario.Before["treatment"]!["routes"]![0]!["outcomes"]![2]!["result"] =
            new JsonArray(new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 });
        scenario.Before["treatment"]!["routes"]!.AsArray().Add(StrictGuaranteedRoute());
        scenario.Before["treatment"]!["knownRouteIds"]!.AsArray().Add("guaranteed_v1");
        scenario = scenario with { History = CreateCurrentWoundHistory(scenario.Before) };
        using var fixture = AcceptedStateFixture.Create(scenario);
        ComposeAndPublishTreatment(fixture, ResolveCurrentTreatment(
            fixture, "course", scenario.OperationKey, scenario.RouteId));
        var courseId = fixture.ReadCurrentWound().Care.ActiveCourseId;
        Assert.NotNull(courseId);
        fixture.PrepareNextTurn(43, 240, "during_course");
        ComposeAndPublishTreatment(fixture, ResolveCurrentTreatment(
            fixture, "guaranteed", scenario.OperationKey + "_guaranteed", "guaranteed_v1"));
        Assert.Equal(courseId, fixture.ReadCurrentWound().Care.ActiveCourseId);
        fixture.PrepareNextTurn(44, 480, "continue_after_other_treatment");
        AssertCourseAcceptedResolution(ResolveCurrentTreatment(fixture, "course",
            scenario.OperationKey + "_next", scenario.RouteId), 2, "active", interruption: false);
    }

    [Theory]
    [InlineData("course_id")]
    [InlineData("ordinal")]
    [InlineData("disposition")]
    [InlineData("interruption")]
    [InlineData("index")]
    [InlineData("category")]
    [InlineData("mode")]
    [InlineData("consumption")]
    [InlineData("route")]
    [InlineData("completion")]
    public void CourseScalarPublication_ChangedSelectionRejectsEvenWithFreshOuterResolutionSeal(string axis)
    {
        var scenario = CreateScalarCoursePublicationScenario();
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(fixture, "course", scenario.OperationKey, scenario.RouteId);
        var state = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(flow.AcceptedState);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        var prepared = MortalWoundTreatmentOutcomePublicationPlanner.Prepare(state, request, resolution, state.CurrentGameMinute);
        Assert.True(prepared.IsValid, DescribeIssues(prepared.Issues));
        var tree = CaptureResolverFixtureTree(fixture.Root);
        MortalWoundTreatmentResolution Alter() => MortalWoundTreatmentResolution.Create(
            axis == "mode" ? "procedure" : resolution.Mode, resolution.Coordinates,
            resolution.AttemptDisposition, axis == "category" ? "failed_attempt" : resolution.ResultCategory,
            axis == "index" ? 1 : resolution.SelectedOutcomeIndex,
            axis == "interruption" || resolution.Interruption,
            resolution.DeclaredResult, resolution.OutcomeIntents, resolution.CriticalReactionIntent,
            axis == "consumption" ? "none" : resolution.ConsumptionTrigger,
            axis == "course_id" ? resolution.CourseId + "_foreign" : resolution.CourseId,
            axis == "ordinal" ? 2 : resolution.CourseMilestoneOrdinal,
            axis == "disposition" ? "completed" : resolution.CourseDisposition,
            request, resolution.ModeEvidence,
            axis == "route" ? resolution.RouteFingerprint + "_foreign" : resolution.RouteFingerprint,
            axis == "completion" ? "AppendOnce" : resolution.RouteCompletion);
        MortalWoundTreatmentResolution altered;
        if (axis == "mode")
        {
            Assert.Throws<InvalidOperationException>(() => Alter());
            altered = CloneResolutionWithIntentsUnchecked(resolution, resolution.OutcomeIntents);
            SetCourseTestBackingField(altered, "Mode", "procedure");
        }
        else altered = Alter();
        var invalid = MortalWoundTreatmentOutcomePublicationPlanner.Prepare(state, request, altered, state.CurrentGameMinute);
        Assert.False(invalid.IsValid);
        Assert.Null(invalid.Preparation);
        Assert.Contains(invalid.Issues, static issue => issue.Code == "mortal_wound_treatment_publication_slice_unsupported");
        if (axis is "course_id" or "ordinal" or "route")
            Assert.False(MortalWoundTreatmentResolution.TryRecomputeModeEvidenceFingerprint(altered, out _));
        // Updating the formerly sufficient outer hashes is not selected-route authority.
        var resealedPreparation = prepared.Preparation!.DetachedCopy();
        SetCourseTestBackingField(resealedPreparation, "ResultFingerprint", altered.ResultFingerprint);
        SetCourseTestBackingField(resealedPreparation, "ResolutionAuthorityFingerprint", altered.ResolutionAuthorityFingerprint);
        SetCourseTestBackingField(resealedPreparation, "Fingerprint",
            MortalWoundTreatmentOutcomePublicationPlanner.ComputePreparationFingerprint(
                resealedPreparation.Before, resealedPreparation.ProvisionalAfter, altered,
                resealedPreparation.TransitionId, state.CurrentGameMinute, null));
        var finalized = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(resealedPreparation, altered,
            null, new Dictionary<string, EffectAcceptedApplicationResult>());
        Assert.False(finalized.IsValid);
        Assert.Null(finalized.After);
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
    }

    [Theory]
    [InlineData("before_pointer")]
    [InlineData("after_pointer")]
    [InlineData("evidence_course_id")]
    [InlineData("evidence_ordinal")]
    [InlineData("evidence_minute")]
    [InlineData("authority_course_id")]
    [InlineData("authority_ordinal")]
    [InlineData("start_route")]
    public void CourseScalarPublication_PreparationOwnsDetachedSelectionAndRejectsChangedPointer(string axis)
    {
        var scenario = CreateScalarCoursePublicationScenario();
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(fixture, "course", scenario.OperationKey, scenario.RouteId);
        var state = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(flow.AcceptedState);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        var tree = CaptureResolverFixtureTree(fixture.Root);
        var first = MortalWoundTreatmentOutcomePublicationPlanner.Prepare(state, request, resolution, state.CurrentGameMinute);
        var second = MortalWoundTreatmentOutcomePublicationPlanner.Prepare(state, request, resolution, state.CurrentGameMinute);
        Assert.True(first.IsValid, DescribeIssues(first.Issues));
        Assert.True(second.IsValid, DescribeIssues(second.Issues));
        Assert.Equal(first.Preparation!.Fingerprint, second.Preparation!.Fingerprint);
        var copy = first.Preparation.DetachedCopy();
        var final = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(copy, resolution, null,
            new Dictionary<string, EffectAcceptedApplicationResult>());
        Assert.True(final.IsValid, DescribeIssues(final.Issues));
        Assert.Equal(first.Preparation.ProvisionalAfter.Care.ActiveCourseId, final.After!.Care.ActiveCourseId);
        var authority = Assert.IsType<MortalWoundCourseModeAuthority>(request.ModeAuthority);
        if (axis == "before_pointer")
            SetCourseTestBackingField(request.RouteSourceWound.Care, "ActiveCourseId", "foreign_course");
        else if (axis == "after_pointer")
        {
            var internalAfter = (WoundMaterializationEnvelope)typeof(MortalWoundTreatmentOutcomePreparation)
                .GetField("_provisionalAfter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(copy)!;
            SetCourseTestBackingField(internalAfter.Care, "ActiveCourseId", "foreign_course");
        }
        else if (axis == "evidence_course_id") SetCourseTestBackingField(resolution.ModeEvidence, "CourseId", "foreign_course");
        else if (axis == "evidence_ordinal") SetCourseTestBackingField(resolution.ModeEvidence, "MilestoneOrdinal", 2);
        else if (axis == "evidence_minute") SetCourseTestBackingField(resolution.ModeEvidence, "ResolvedAtGameTimeMinutes", 1L);
        else if (axis == "authority_course_id") SetCourseTestBackingField(authority, "CourseId", "foreign_course");
        else if (axis == "authority_ordinal") SetCourseTestBackingField(authority, "MilestoneOrdinal", 2);
        else SetCourseTestBackingField(authority.CourseStartAuthority, "RouteFingerprint", "sha256:foreign");
        var resealed = MortalWoundTreatmentResolution.Create(resolution.Mode, resolution.Coordinates,
            resolution.AttemptDisposition, resolution.ResultCategory, resolution.SelectedOutcomeIndex,
            resolution.Interruption, resolution.DeclaredResult, resolution.OutcomeIntents, null,
            resolution.ConsumptionTrigger, resolution.CourseId, resolution.CourseMilestoneOrdinal,
            resolution.CourseDisposition, request, resolution.ModeEvidence, resolution.RouteFingerprint,
            resolution.RouteCompletion);
        SetCourseTestBackingField(copy, "Fingerprint",
            MortalWoundTreatmentOutcomePublicationPlanner.ComputePreparationFingerprint(
                copy.Before, copy.ProvisionalAfter, resealed, copy.TransitionId, state.CurrentGameMinute, null));
        var rejected = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(copy, resealed, null,
            new Dictionary<string, EffectAcceptedApplicationResult>());
        Assert.False(rejected.IsValid);
        Assert.Null(rejected.After);
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
    }

    [Theory]
    [InlineData("unexpected_reaction")]
    [InlineData("changed_selection")]
    [InlineData("foreign_state")]
    [InlineData("stale_state")]
    public void CourseScalarPublication_NoCriticalReactionRejectsChangedForeignOrStaleAuthority(string axis)
    {
        var scenario = CreateScalarCoursePublicationScenario();
        using var fixture = AcceptedStateFixture.Create(scenario);
        using var foreign = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(fixture, "course", scenario.OperationKey, scenario.RouteId);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        var state = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(flow.AcceptedState);
        if (axis == "unexpected_reaction")
        {
            var procedure = CreateScenario("procedure_player_natural_one_reserves_oldest_fate_shield", "procedure");
            using var procedureFixture = AcceptedStateFixture.Create(procedure);
            var procedureFlow = ResolveCurrentTreatment(procedureFixture, "procedure", procedure.OperationKey, procedure.RouteId);
            var reaction = Assert.IsType<MortalWoundTreatmentResolution>(procedureFlow.Resolution).CriticalReactionIntent;
            Assert.NotNull(reaction);
            SetCourseTestBackingField(resolution, "CriticalReactionIntent", reaction);
        }
        else if (axis == "changed_selection")
            resolution = MortalWoundTreatmentResolution.Create(resolution.Mode, resolution.Coordinates,
                resolution.AttemptDisposition, resolution.ResultCategory, resolution.SelectedOutcomeIndex,
                resolution.Interruption, resolution.DeclaredResult, resolution.OutcomeIntents, null,
                resolution.ConsumptionTrigger, resolution.CourseId + "_foreign", resolution.CourseMilestoneOrdinal,
                resolution.CourseDisposition, request, resolution.ModeEvidence, resolution.RouteFingerprint, resolution.RouteCompletion);
        else if (axis == "foreign_state")
            state = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(foreign.GetAcceptedState());
        else fixture.RestartForReplay();
        var tree = CaptureResolverFixtureTree(fixture.Root);
        var result = MortalWoundCriticalReactionPublicationPlanner.Compose(state, request, resolution);
        Assert.False(result.IsValid);
        Assert.Null(result.Fingerprint);
        Assert.Empty(result.LifecycleEvents);
        Assert.Contains(result.Issues, issue => issue.Code == (axis == "unexpected_reaction"
            ? "mortal_wound_treatment_critical_reaction_unexpected"
            : "mortal_wound_treatment_critical_reaction_publication_authority_invalid"));
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
    }

    private static void SetCourseTestBackingField(object value, string property, object replacement) =>
        value.GetType().GetField("<" + property + ">k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(value, replacement);

    [Theory]
    [InlineData("held", false)]
    [InlineData("held", true)]
    [InlineData("no_resource", false)]
    [InlineData("no_resource", true)]
    [InlineData("trusted_interruption", false)]
    [InlineData("trusted_interruption", true)]
    public void CourseScalarPublication_OnePendingMilestoneCoordinateSurvivesColdRecoveryAndExactRelease(
        string kind, bool cold)
    {
        var scenario = CreateScalarCoursePublicationScenario();
        if (kind == "no_resource")
        {
            var route = scenario.Before["treatment"]!["routes"]![0]!;
            route["resourcePolicy"] = Policy(new JsonArray(), new JsonArray());
            foreach (var milestone in route["outcomes"]!.AsArray())
                milestone!["requirements"] = new JsonArray();
            scenario = scenario with { History = CreateCurrentWoundHistory(scenario.Before) };
        }
        using var fixture = AcceptedStateFixture.Create(scenario);
        ComposeAndPublishTreatment(fixture, ResolveCurrentTreatment(
            fixture, "course", scenario.OperationKey + "_start", scenario.RouteId));
        fixture.PrepareNextTurn(43, kind == "trusted_interruption" ? 1_081 : 480,
            "coordinate_exclusivity_" + kind);
        var flow = ResolveCurrentTreatment(fixture, "course",
            scenario.OperationKey + "_pending", scenario.RouteId);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        Assert.Equal(kind == "held" ? "held" : "not_required",
            request.ResourceAuthority.ReservationDisposition);
        Assert.Equal(kind == "trusted_interruption",
            Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution).Interruption);
        if (!cold)
        {
            AssertCourseCoordinateReservationLifecycle(fixture, flow, scenario.RouteId);
            return;
        }
        flow = PersistAndRehydrateTreatmentPublication(fixture, flow, "pending course coordinate");
        using var recovered = CreateColdRootCopy(fixture);
        var restored = Assert.Single(AssertValidPersistedCatalog(
            RestoreCurrentPersistedTreatmentCatalog(recovered), "cold course coordinate"),
            candidate => Assert.IsType<MortalWoundTreatmentAttemptRequest>(candidate).RequestFingerprint ==
                request.RequestFingerprint);
        var rehydrated = RehydratePersistedTreatment(recovered, "course", restored);
        Assert.Equal(CanonicalValue(flow.Request), CanonicalValue(rehydrated.Request));
        Assert.Equal(CanonicalValue(flow.Resolution), CanonicalValue(rehydrated.Resolution));
        AssertCourseCoordinateReservationLifecycle(recovered, rehydrated, scenario.RouteId);
    }

    private static void AssertCourseCoordinateReservationLifecycle(
        AcceptedStateFixture fixture, TreatmentFlow flow, string routeId)
    {
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var state = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(flow.AcceptedState);
        var retry = ResolveCurrentTreatment(fixture, "course", request.Coordinates.OperationKey, routeId);
        Assert.Equal(CanonicalValue(flow.Request), CanonicalValue(retry.Request));
        Assert.Equal(CanonicalValue(flow.Resolution), CanonicalValue(retry.Resolution));
        var tree = CaptureResolverFixtureTree(fixture.Root);
        var conflict = MortalWoundTreatmentPlanner.PrepareCourseMilestoneRequest(state,
            fixture.ReadCurrentHistory(), fixture.ReadCurrentWound(),
            request.Coordinates.OperationKey + "_other", routeId, fixture.AcceptedEventRef(state));
        Assert.False(conflict.IsValid);
        Assert.Null(conflict.Request);
        Assert.Contains(conflict.Issues, issue =>
            issue.Code == "mortal_wound_treatment_resource_reservation_conflict");
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        var released = state.ReleaseTreatmentResources(ResourceLifecycleCapability(), new[] { request }, "cancelled");
        Assert.True(released.IsValid, DescribeIssues(released.Issues));
        Assert.Equal(1, released.ChangedCount);
        var repeated = state.ReleaseTreatmentResources(ResourceLifecycleCapability(), new[] { request }, "cancelled");
        Assert.True(repeated.IsValid, DescribeIssues(repeated.Issues));
        Assert.Equal(0, repeated.ChangedCount);
        var afterRelease = ResolveCurrentTreatment(fixture, "course", request.Coordinates.OperationKey, routeId);
        Assert.Equal(CanonicalValue(flow.Request), CanonicalValue(afterRelease.Request));
        Assert.Equal(CanonicalValue(flow.Resolution), CanonicalValue(afterRelease.Resolution));
        var releaseRetry = state.ReleaseTreatmentResources(ResourceLifecycleCapability(),
            new[] { Assert.IsType<MortalWoundTreatmentAttemptRequest>(afterRelease.Request) }, "cancelled");
        Assert.True(releaseRetry.IsValid, DescribeIssues(releaseRetry.Issues));
        Assert.Equal(1, releaseRetry.ChangedCount);
        var replacement = ResolveCurrentTreatment(fixture, "course", request.Coordinates.OperationKey + "_other", routeId);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(replacement.Resolution);
        Assert.Equal(Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution).CourseId, resolution.CourseId);
        Assert.Equal(2, resolution.CourseMilestoneOrdinal);
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
    }

    [Fact]
    public void CourseScalarPublication_PostWriteFailureRestoresPointerItemsEffectsAndHistoryThenRetriesOnce()
    {
        var fault = new ResourcePublicationFailureInjection();
        var scenario = CreateScalarCoursePublicationScenario();
        using var fixture = AcceptedStateFixture.Create(scenario, new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = fault.BeforeCanonicalMutationAsync
        });
        var flow = PersistAndRehydrateTreatmentPublication(fixture,
            ResolveCurrentTreatment(fixture, "course", scenario.OperationKey, scenario.RouteId), "course rollback");
        var plan = ComposeCoordinatedTreatmentPlan(fixture, flow);
        var tree = CaptureResolverFixtureTree(fixture.Root);
        fault.Arm(WoundHistoryState.HistoryPath, fixture.TargetCarrierPath,
            ReadCanonicalBytes(fixture, fixture.TargetCarrierPath), fixture.FileSystem);
        var exception = Assert.Throws<CanonicalStateWriteException>(() =>
            PublishCachedResourcePlanOpen(fixture, flow, plan));
        Assert.Equal(WoundHistoryState.HistoryPath, exception.RelativePath);
        Assert.True(fault.Fired);
        Assert.True(fault.ObservedEarlierResourceWrite);
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        using var publication = PublishCachedResourcePlanOpen(fixture, flow, plan);
        publication.CompleteAtFullPipelineEnd();
        Assert.NotNull(fixture.ReadCurrentWound().Care.ActiveCourseId);
        Assert.Equal(7, fixture.ReadPlayerItemCount("antibiotic_dose"));
        fixture.AssertItemIdentityIndexValid();
        Assert.Single(fixture.ReadCurrentHistory().State!.Transitions, static row => row.Kind == "treat");
    }
}
