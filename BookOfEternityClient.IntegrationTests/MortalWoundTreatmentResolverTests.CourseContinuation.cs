using System.Collections.Immutable;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Theory]
    [InlineData(479L, "TooEarly", null)]
    [InlineData(480L, "Ready", "ready")]
    [InlineData(1_080L, "Ready", "ready")]
    [InlineData(1_081L, "DeadlineExceeded", "deadline_exceeded")]
    public void CourseContinuation_TypedHistoryUsesInclusiveStartRelativeWindow(
        long minute,
        string expectedDisposition,
        string? expectedWindowDisposition)
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var first = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_window_start_" + minute,
            scenario.RouteId);
        var firstCourse = Assert.IsType<MortalWoundCourseModeAuthority>(
            Assert.IsType<MortalWoundTreatmentAttemptRequest>(first.Request).ModeAuthority);
        InstallPublishedCourseStart(fixture, first);

        fixture.RestartForReplay();
        fixture.PrepareNextTurn(43, minute, "typed_course_window_" + minute);
        var probe = InspectCourseMode(
            fixture,
            scenario.OperationKey + "_typed_window_" + minute,
            scenario.RouteId);
        var result = Assert.IsType<MortalWoundCourseModeAuthorityResult>(probe.Result);

        Assert.Equal(expectedDisposition, result.Disposition);
        Assert.Empty(result.Issues);
        if (expectedWindowDisposition is null)
        {
            Assert.Null(result.Authority);
            return;
        }

        var authority = Assert.IsType<MortalWoundCourseModeAuthority>(result.Authority);
        Assert.Equal(firstCourse.CourseId, authority.CourseId);
        Assert.Equal(2, authority.MilestoneOrdinal);
        Assert.Equal(480, authority.DueAtGameTimeMinutes);
        Assert.Equal(1_080, authority.DeadlineAtGameTimeMinutes);
        Assert.Equal(expectedWindowDisposition, authority.WindowDisposition);
        Assert.Equal(
            firstCourse.CourseStartAuthority.AuthorityFingerprint,
            authority.CourseStartAuthority.AuthorityFingerprint);
    }

    [Fact]
    public void CourseContinuation_TypedHistoryColdRestartPreparesOrdinalTwo()
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        using var fixture = AcceptedStateFixture.Create(scenario);

        var first = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_typed_start",
            scenario.RouteId);
        var firstRequest = Assert.IsType<MortalWoundTreatmentAttemptRequest>(first.Request);
        var firstCourse = Assert.IsType<MortalWoundCourseModeAuthority>(
            firstRequest.ModeAuthority);
        InstallPublishedCourseStart(fixture, first);

        fixture.RestartForReplay();
        fixture.PrepareNextTurn(43, 480, "typed_course_ordinal_2");
        var second = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_typed_ordinal_2",
            scenario.RouteId);
        var secondRequest = Assert.IsType<MortalWoundTreatmentAttemptRequest>(second.Request);
        var secondCourse = Assert.IsType<MortalWoundCourseModeAuthority>(
            secondRequest.ModeAuthority);

        Assert.Equal(2, secondRequest.MilestoneOrdinal);
        Assert.Equal(firstCourse.CourseId, secondCourse.CourseId);
        Assert.Equal(2, secondCourse.MilestoneOrdinal);
        Assert.Equal(480, secondCourse.DueAtGameTimeMinutes);
        Assert.Equal(1_080, secondCourse.DeadlineAtGameTimeMinutes);
        Assert.Equal("ready", secondCourse.WindowDisposition);
        Assert.Equal(
            firstCourse.CourseStartAuthority.AuthorityFingerprint,
            secondCourse.CourseStartAuthority.AuthorityFingerprint);
        Assert.Equal(
            firstCourse.CourseStartAuthority.StartingWoundFingerprint,
            secondCourse.CourseStartAuthority.StartingWoundFingerprint);
        AssertCourseAcceptedResolution(second, 2, "active", interruption: false);
    }

    [Theory]
    [InlineData(false, 1_081L, "deadline_exceeded", "deadline_exceeded")]
    [InlineData(true, 480L, "requirements_unsatisfied", "ready")]
    public void CourseContinuation_InterruptionReservesNoCurrentOrFutureDose(
        bool removeDose,
        long minute,
        string expectedReason,
        string expectedWindow)
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var first = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_interruption_start_" + expectedReason,
            scenario.RouteId);
        InstallPublishedCourseStart(fixture, first);
        if (removeDose)
            fixture.RemoveCurrentPlayerDose();
        fixture.RestartForReplay();
        fixture.PrepareNextTurn(43, minute, "typed_course_" + expectedReason);

        var interrupted = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_" + expectedReason,
            scenario.RouteId);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(
            interrupted.Request);

        AssertCourseAcceptedResolution(interrupted, 2, "interrupted", interruption: true);
        Assert.Equal(expectedReason, request.RequirementAuthority.InterruptionReason);
        Assert.Equal(
            removeDose ? "Unsatisfied" : "Satisfied",
            request.RequirementAuthority.CourseRequirementStatus);
        Assert.Equal(
            expectedWindow,
            Assert.IsType<MortalWoundCourseModeAuthority>(request.ModeAuthority)
                .WindowDisposition);
        Assert.Equal("not_required", request.ResourceAuthority.ReservationDisposition);
        Assert.Null(request.ResourceAuthority.ReservationId);
        Assert.Empty(request.ResourceAuthority.Claims);
    }

    [Fact]
    public void CourseContinuation_TypedHistoryReconstructsOrdinalThreeAndCompletion()
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var first = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_ordinal_3_start",
            scenario.RouteId);
        InstallPublishedCourseStart(fixture, first);

        fixture.RestartForReplay();
        fixture.PrepareNextTurn(43, 480, "typed_course_ordinal_2_before_3");
        var second = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_ordinal_2_before_3",
            scenario.RouteId);
        AssertCourseAcceptedResolution(second, 2, "active", interruption: false);
        InstallPublishedActiveCourseMilestone(fixture, second, "ordinal_2_before_3");

        fixture.RestartForReplay();
        fixture.PrepareNextTurn(44, 960, "typed_course_ordinal_3");
        var third = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_typed_ordinal_3",
            scenario.RouteId);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(third.Request);
        var authority = Assert.IsType<MortalWoundCourseModeAuthority>(
            request.ModeAuthority);

        Assert.Equal(3, request.MilestoneOrdinal);
        Assert.Equal(3, authority.MilestoneOrdinal);
        Assert.Equal(960, authority.DueAtGameTimeMinutes);
        Assert.Equal(1_560, authority.DeadlineAtGameTimeMinutes);
        Assert.Equal("ready", authority.WindowDisposition);
        AssertCourseAcceptedResolution(third, 3, "completed", interruption: false);
        Assert.Equal("AppendOnce", Assert.IsType<MortalWoundTreatmentResolution>(
            third.Resolution).RouteCompletion);
    }

    [Fact]
    public void CourseContinuation_InvalidHistoryWinsBeforeActiveCourseReconstruction()
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var first = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_invalid_history_start",
            scenario.RouteId);
        InstallPublishedCourseStart(fixture, first);
        fixture.PrepareNextTurn(43, 480, "typed_course_invalid_history");
        var validProbe = InspectCourseMode(
            fixture,
            scenario.OperationKey + "_valid_probe_before_invalid_history",
            scenario.RouteId);
        var validResult = Assert.IsType<MortalWoundCourseModeAuthorityResult>(
            validProbe.Result);
        var validAuthority = Assert.IsType<MortalWoundCourseModeAuthority>(
            validResult.Authority);
        var invalidHistory = WoundHistoryState.Parse(
            "{",
            WoundHistoryState.HistoryPath);
        Assert.False(invalidHistory.IsValid);

        var result = MortalWoundCourseModeAuthority.Create(
            Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
                validProbe.AcceptedState),
            Assert.IsType<MortalWoundTreatmentAttemptCoordinates>(validProbe.Coordinates),
            validProbe.Before,
            invalidHistory,
            validAuthority.GameTimeAuthority);

        Assert.Equal("InvalidAuthority", result.Disposition);
        Assert.Null(result.Authority);
        Assert.Contains(result.Issues, issue =>
            string.Equals(issue.Code, "wound_history_invalid_json",
                StringComparison.Ordinal));
        Assert.DoesNotContain(result.Issues, issue =>
            string.Equals(issue.Code, "mortal_wound_treatment_course_start_conflict",
                StringComparison.Ordinal));
    }

    [Fact]
    public void CourseContinuation_ActivePointerWithoutTypedStartHistoryFailsClosed()
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var before = fixture.ReadCurrentWound();
        var unbacked = before with
        {
            Care = before.Care with
            {
                ActiveCourseId = "mortal_wound_course_unbacked_t067",
                LastAttemptId = "wound_treatment_attempt_unbacked_t067"
            }
        };
        InstallCurrentWoundAndIdentity(fixture, unbacked);
        fixture.PrepareNextTurn(43, 480, "typed_course_missing_start_history");

        var probe = InspectCourseMode(
            fixture,
            scenario.OperationKey + "_missing_start_history",
            scenario.RouteId);
        var result = Assert.IsType<MortalWoundCourseModeAuthorityResult>(probe.Result);

        Assert.Equal("InvalidAuthority", result.Disposition);
        Assert.Null(result.Authority);
        Assert.Contains(result.Issues, issue => string.Equals(
            issue.Code,
            "mortal_wound_treatment_course_history_missing",
            StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, "mortal_wound_treatment_course_history_conflict")]
    [InlineData(true, "mortal_wound_treatment_course_second_course_conflict")]
    public void CourseContinuation_RejectsIncompleteOrSecondCourseHistory(
        bool secondCourse,
        string expectedCode)
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var first = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_history_conflict_start_" + secondCourse,
            scenario.RouteId);
        var firstCourse = Assert.IsType<MortalWoundCourseModeAuthority>(
            Assert.IsType<MortalWoundTreatmentAttemptRequest>(first.Request).ModeAuthority);
        InstallPublishedCourseStart(fixture, first);
        AppendUnsealedCourseCoordinate(
            fixture,
            secondCourse
                ? "mortal_wound_course_parallel_t067"
                : firstCourse.CourseId,
            secondCourse ? 1 : 2,
            secondCourse ? "diagnose" : "treat",
            secondCourse ? "parallel" : "missing_payload");
        fixture.PrepareNextTurn(43, 480, "typed_course_history_conflict_" + secondCourse);

        var probe = InspectCourseMode(
            fixture,
            scenario.OperationKey + "_history_conflict_probe_" + secondCourse,
            scenario.RouteId);
        var result = Assert.IsType<MortalWoundCourseModeAuthorityResult>(probe.Result);

        Assert.Equal("InvalidAuthority", result.Disposition);
        Assert.Null(result.Authority);
        Assert.Contains(result.Issues, issue =>
            string.Equals(issue.Code, expectedCode, StringComparison.Ordinal));
    }

    private static WoundMaterializationEnvelope InstallPublishedCourseStart(
        AcceptedStateFixture fixture,
        TreatmentFlow flow)
    {
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        var course = Assert.IsType<MortalWoundCourseModeAuthority>(request.ModeAuthority);
        Assert.Equal(1, course.MilestoneOrdinal);
        Assert.Equal("active", resolution.CourseDisposition);
        Assert.Empty(resolution.DeclaredResult);

        const string transitionId = "wound_transition_t067_course_typed_ordinal_1";
        var after = flow.Before with
        {
            Care = flow.Before.Care with
            {
                ActiveCourseId = course.CourseId,
                LastAttemptId = request.Coordinates.AttemptId
            },
            LastTransition = new WoundLastTransition(
                transitionId,
                flow.Before.LastTransition.Ordinal + 1,
                request.Coordinates.Turn,
                "treat")
        };
        var beforeFingerprint = WoundIdentityState.ComputeSemanticFingerprint(flow.Before);
        var afterFingerprint = WoundIdentityState.ComputeSemanticFingerprint(after);
        var createSummary = "The accepted wound existed before the course started.";
        var treatmentSummary = "The accepted first course milestone started the course.";
        var history = WoundHistoryState.CreateValidated(
            3,
            new[]
            {
                new WoundHistoryTransition(
                    flow.Before.LastTransition.TransitionId,
                    flow.Before.WoundId,
                    1,
                    1,
                    "create",
                    flow.Before.Origin.CreatedAtTurn,
                    flow.Before.Origin.EventRef,
                    "operation_t067_course_typed_create",
                    WoundHistoryState.ComputeNonexistentBeforeFingerprint(flow.Before.WoundId),
                    beforeFingerprint,
                    request.Coordinates.AcceptedStateFingerprint,
                    null,
                    null,
                    null,
                    null,
                    null,
                    WoundHistoryState.ComputeOutputFingerprint(
                        "operation_t067_course_typed_create",
                        flow.Before.Origin.EventRef,
                        createSummary),
                    createSummary,
                    false),
                new WoundHistoryTransition(
                    transitionId,
                    flow.Before.WoundId,
                    2,
                    2,
                    "treat",
                    request.Coordinates.Turn,
                    request.Coordinates.EventRef,
                    request.Coordinates.OperationKey,
                    beforeFingerprint,
                    afterFingerprint,
                    request.RequestFingerprint,
                    request.Coordinates.AttemptId,
                    course.CourseId,
                    1,
                    null,
                    null,
                    WoundHistoryState.ComputeOutputFingerprint(
                        request.Coordinates.OperationKey,
                        request.Coordinates.EventRef,
                        treatmentSummary),
                    treatmentSummary,
                    false,
                    MortalWoundTreatmentPersistedResult.Create(resolution))
            });
        Assert.True(history.IsValid, DescribeIssues(history.Issues));

        InstallCurrentWoundAndIdentity(fixture, after);
        File.WriteAllText(
            fixture.FileSystem.ResolvePath(WoundHistoryState.HistoryPath),
            WoundHistoryState.SerializeCanonical(history.State!));
        return after;
    }

    private static void AppendUnsealedCourseCoordinate(
        AcceptedStateFixture fixture,
        string courseId,
        int milestoneOrdinal,
        string kind,
        string suffix)
    {
        var before = fixture.ReadCurrentWound();
        var history = fixture.ReadCurrentHistory();
        var transitionId = "wound_transition_t067_course_" + suffix;
        var after = before with
        {
            LastTransition = new WoundLastTransition(
                transitionId,
                before.LastTransition.Ordinal + 1,
                before.LastTransition.Turn,
                kind)
        };
        var beforeFingerprint = WoundIdentityState.ComputeSemanticFingerprint(before);
        var afterFingerprint = WoundIdentityState.ComputeSemanticFingerprint(after);
        var summary = "A parseable but untrusted course coordinate was retained.";
        WoundTransitionResult? transitionResult = null;
        if (kind == "diagnose")
        {
            var diagnosisPathId = "diagnosis_t067_course_" + suffix;
            var unsealedResult = new JsonObject
            {
                ["kind"] = "diagnose",
                ["diagnosisPathId"] = diagnosisPathId,
                ["result"] = "failure",
                ["revealedFacts"] = new JsonArray()
            };
            transitionResult = new WoundDiagnosisTransitionResult(
                diagnosisPathId,
                "failure",
                Array.Empty<string>(),
                WoundHistoryState.ComputeTransitionResultFingerprint(unsealedResult));
        }
        var transition = new WoundHistoryTransition(
            transitionId,
            before.WoundId,
            history.State!.NextOrdinal,
            after.LastTransition.Ordinal,
            kind,
            after.LastTransition.Turn,
            before.Origin.EventRef,
            "operation_t067_course_" + suffix,
            beforeFingerprint,
            afterFingerprint,
            history.State.Transitions[^1].SourceFingerprint,
            null,
            courseId,
            milestoneOrdinal,
            null,
            null,
            WoundHistoryState.ComputeOutputFingerprint(
                "operation_t067_course_" + suffix,
                before.Origin.EventRef,
                summary),
            summary,
            false,
            transitionResult);
        var updatedHistory = WoundHistoryState.CreateValidated(
            history.State.NextOrdinal + 1,
            history.State.Transitions.Append(transition));
        Assert.True(updatedHistory.IsValid, DescribeIssues(updatedHistory.Issues));

        InstallCurrentWoundAndIdentity(fixture, after);
        File.WriteAllText(
            fixture.FileSystem.ResolvePath(WoundHistoryState.HistoryPath),
            WoundHistoryState.SerializeCanonical(updatedHistory.State!));
    }

    private static void InstallPublishedActiveCourseMilestone(
        AcceptedStateFixture fixture,
        TreatmentFlow flow,
        string suffix)
    {
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        Assert.Equal("active", resolution.CourseDisposition);
        Assert.False(resolution.Interruption);
        Assert.NotNull(resolution.CourseId);
        Assert.NotNull(resolution.CourseMilestoneOrdinal);
        var simulation = MortalWoundTreatmentWorkingWoundSimulator.Simulate(
            flow.Before,
            new[] { resolution.DeclaredResult.ToImmutableArray() });
        Assert.True(simulation.IsApplicable);
        Assert.True(simulation.Improved);
        var working = Assert.IsType<WoundMaterializationEnvelope>(simulation.WorkingWound);
        var transitionId = "wound_transition_t067_course_" + suffix;
        var after = working with
        {
            Care = working.Care with
            {
                ActiveCourseId = resolution.CourseId,
                LastAttemptId = request.Coordinates.AttemptId
            },
            LastTransition = new WoundLastTransition(
                transitionId,
                flow.Before.LastTransition.Ordinal + 1,
                request.Coordinates.Turn,
                "treat")
        };
        var beforeFingerprint = WoundIdentityState.ComputeSemanticFingerprint(flow.Before);
        Assert.Equal(request.Coordinates.ExpectedBeforeFingerprint, beforeFingerprint);
        var afterFingerprint = WoundIdentityState.ComputeSemanticFingerprint(after);
        var history = fixture.ReadCurrentHistory();
        var summary = "The accepted active course milestone advanced the course.";
        var transition = new WoundHistoryTransition(
            transitionId,
            after.WoundId,
            history.State!.NextOrdinal,
            after.LastTransition.Ordinal,
            "treat",
            request.Coordinates.Turn,
            request.Coordinates.EventRef,
            request.Coordinates.OperationKey,
            beforeFingerprint,
            afterFingerprint,
            request.RequestFingerprint,
            request.Coordinates.AttemptId,
            resolution.CourseId,
            resolution.CourseMilestoneOrdinal,
            null,
            null,
            WoundHistoryState.ComputeOutputFingerprint(
                request.Coordinates.OperationKey,
                request.Coordinates.EventRef,
                summary),
            summary,
            false,
            MortalWoundTreatmentPersistedResult.Create(resolution));
        var updatedHistory = WoundHistoryState.CreateValidated(
            history.State.NextOrdinal + 1,
            history.State.Transitions.Append(transition));
        Assert.True(updatedHistory.IsValid, DescribeIssues(updatedHistory.Issues));

        InstallCurrentWoundAndIdentity(fixture, after);
        File.WriteAllText(
            fixture.FileSystem.ResolvePath(WoundHistoryState.HistoryPath),
            WoundHistoryState.SerializeCanonical(updatedHistory.State!));
    }

    private static void InstallCurrentWoundAndIdentity(
        AcceptedStateFixture fixture,
        WoundMaterializationEnvelope wound)
    {
        var woundFingerprint = WoundIdentityState.ComputeSemanticFingerprint(wound);
        var carrierPath = fixture.FileSystem.ResolvePath(fixture.TargetCarrierPath);
        var carrier = JsonNode.Parse(File.ReadAllText(carrierPath))!.AsObject();
        var activeWounds = carrier["activeWounds"]!.AsArray();
        var woundIndex = activeWounds
            .Select((candidate, index) => (candidate, index))
            .Single(pair => string.Equals(
                pair.candidate!["woundId"]!.GetValue<string>(),
                wound.WoundId,
                StringComparison.Ordinal))
            .index;
        activeWounds[woundIndex] = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(wound));
        File.WriteAllText(carrierPath, carrier.ToJsonString());

        File.WriteAllText(
            fixture.FileSystem.ResolvePath(WoundIdentityState.StatePath),
            WoundContractTestData.CreateIdentityIndex(
                WoundContractTestData.CreateIdentityEntry(
                    woundId: wound.WoundId,
                    realm: wound.Owner.Realm,
                    ownerKind: wound.Owner.OwnerKind,
                    ownerId: wound.Owner.OwnerId,
                    carrierPath: wound.Owner.CarrierPath,
                    domain: wound.Classification.Domain,
                    status: "active",
                    createdAtTurn: wound.Origin.CreatedAtTurn,
                    createdEventRef: wound.Origin.EventRef,
                    lastTransitionOrdinal: wound.LastTransition.Ordinal,
                    semanticFingerprint: woundFingerprint)).ToJsonString());
    }
}
