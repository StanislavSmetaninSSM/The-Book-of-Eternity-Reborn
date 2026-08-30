using System.Collections;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public void PrerequisiteAuthority_CoordinatesAndGameTimeAreClosedAndDeterministic()
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = fixture.GetAcceptedState();
        var before = fixture.ReadCurrentWound();
        var eventRef = fixture.AcceptedEventRef(acceptedState);
        const string operationKey = "operation_t066_prerequisite_authority";

        var first = CreatePrerequisiteCoordinates(
            acceptedState,
            before,
            operationKey,
            scenario.RouteId,
            eventRef);
        var second = CreatePrerequisiteCoordinates(
            acceptedState,
            before,
            operationKey,
            scenario.RouteId,
            eventRef);

        AssertClosedProperties(first, new[]
        {
            "SchemaVersion", "SessionId", "SessionGeneration", "RequestId", "SnapshotToken",
            "OperationKey", "AttemptId", "WoundId", "RouteId", "ExpectedBeforeFingerprint",
            "EventRef", "EventKind", "EventAuthorityId", "EventSemanticFingerprint", "Turn",
            "Realm", "ProviderKind", "ProviderId", "TargetKind", "TargetId", "LocationId",
            "ContextFingerprint", "AcceptedStateFingerprint", "CoordinatesFingerprint"
        });
        Assert.Empty(first.GetType().GetConstructors());
        Assert.Equal(1, Convert.ToInt32(ReadRequiredProperty(first, "SchemaVersion")));
        Assert.Equal("session_t061", ReadRequiredProperty(first, "SessionId"));
        Assert.Equal("request_t061", ReadRequiredProperty(first, "RequestId"));
        Assert.Equal(operationKey, ReadRequiredProperty(first, "OperationKey"));
        Assert.Equal(before.WoundId, ReadRequiredProperty(first, "WoundId"));
        Assert.Equal(scenario.RouteId, ReadRequiredProperty(first, "RouteId"));
        Assert.Equal(eventRef, ReadRequiredProperty(first, "EventRef"));
        Assert.Equal("accepted_turn", ReadRequiredProperty(first, "EventKind"));
        Assert.Equal("turn_42", ReadRequiredProperty(first, "EventAuthorityId"));
        Assert.Equal(42, Convert.ToInt32(ReadRequiredProperty(first, "Turn")));
        Assert.Equal("mortal_world", ReadRequiredProperty(first, "Realm"));
        Assert.Equal("npc", ReadRequiredProperty(first, "ProviderKind"));
        Assert.Equal("field_medic_01", ReadRequiredProperty(first, "ProviderId"));
        Assert.Equal("player", ReadRequiredProperty(first, "TargetKind"));
        Assert.Equal("player_current", ReadRequiredProperty(first, "TargetId"));
        Assert.Equal("loc_field_clinic_001", ReadRequiredProperty(first, "LocationId"));
        foreach (var property in new[]
                 {
                     "ExpectedBeforeFingerprint", "EventSemanticFingerprint", "ContextFingerprint",
                     "AcceptedStateFingerprint", "CoordinatesFingerprint"
                 })
        {
            AssertAuthorityFingerprint(ReadRequiredProperty(first, property));
        }
        Assert.False(string.IsNullOrWhiteSpace(Convert.ToString(
            ReadRequiredProperty(first, "SessionGeneration"))));
        Assert.False(string.IsNullOrWhiteSpace(Convert.ToString(
            ReadRequiredProperty(first, "SnapshotToken"))));
        Assert.False(string.IsNullOrWhiteSpace(Convert.ToString(
            ReadRequiredProperty(first, "AttemptId"))));
        Assert.Equal(
            ReadRequiredProperty(first, "AttemptId"),
            ReadRequiredProperty(second, "AttemptId"));
        Assert.Equal(
            ReadRequiredProperty(first, "CoordinatesFingerprint"),
            ReadRequiredProperty(second, "CoordinatesFingerprint"));

        var gameTimeType = RequiredPrerequisiteType("MortalWoundGameTimeAuthority");
        var gameTimeResult = Invoke(
            ExactStaticMethod(gameTimeType, "Create", 2),
            new[] { acceptedState, first });
        AssertClosedProperties(gameTimeResult, new[] { "IsValid", "Issues", "Authority" });
        var gameTime = ReadValidTypedResult(
            gameTimeResult,
            "Authority",
            "prerequisite game-time authority");
        AssertClosedProperties(gameTime, new[]
        {
            "ClockKind", "SourcePath", "CurrentTimeInMinutes", "CoordinatesFingerprint",
            "AcceptedStateFingerprint", "AuthorityFingerprint"
        });
        Assert.Empty(gameTime.GetType().GetConstructors());
        Assert.Equal("world_time.currentTimeInMinutes", ReadRequiredProperty(gameTime, "ClockKind"));
        Assert.Equal("game_state/world/world_time.json", ReadRequiredProperty(gameTime, "SourcePath"));
        Assert.Equal(scenario.WorldMinute,
            Convert.ToInt64(ReadRequiredProperty(gameTime, "CurrentTimeInMinutes")));
        Assert.Equal(ReadRequiredProperty(first, "CoordinatesFingerprint"),
            ReadRequiredProperty(gameTime, "CoordinatesFingerprint"));
        Assert.Equal(ReadRequiredProperty(first, "AcceptedStateFingerprint"),
            ReadRequiredProperty(gameTime, "AcceptedStateFingerprint"));
        AssertAuthorityFingerprint(ReadRequiredProperty(gameTime, "AuthorityFingerprint"));
    }

    [Fact]
    public void PrerequisiteAuthority_CoordinatesRejectDetachedOrUnresolvedInputs()
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = fixture.GetAcceptedState();
        var before = fixture.ReadCurrentWound();
        var eventRef = fixture.AcceptedEventRef(acceptedState);
        var mismatchedBefore = before with { WoundId = "wound_detached_t066" };

        AssertInvalidPrerequisiteResult(InvokeCreateCoordinates(
            acceptedState,
            mismatchedBefore,
            "operation_t066_bad_before",
            scenario.RouteId,
            eventRef));
        AssertInvalidPrerequisiteResult(InvokeCreateCoordinates(
            acceptedState,
            before,
            " operation_t066_bad_key ",
            scenario.RouteId,
            eventRef));
        AssertInvalidPrerequisiteResult(InvokeCreateCoordinates(
            acceptedState,
            before,
            "operation_t066_bad_route",
            "missing_route_t066",
            eventRef));
        AssertInvalidPrerequisiteResult(InvokeCreateCoordinates(
            acceptedState,
            before,
            "operation_t066_bad_event",
            scenario.RouteId,
            "missing_event_t066"));

        fixture.ReleaseLeaseForExternalDistribution();
        try
        {
            var released = InvokeCreateCoordinates(
                acceptedState,
                before,
                "operation_t066_released_admission",
                scenario.RouteId,
                eventRef);
            AssertInvalidPrerequisiteResult(released);
            AssertPrerequisiteIssueCode(
                released,
                "mortal_wound_treatment_coordinates_accepted_state_invalid");
        }
        finally
        {
            fixture.ReacquireLeaseAfterExternalDistribution();
        }
    }

    [Fact]
    public void PrerequisiteAuthority_CoordinatesRejectOperationAlreadyInCompleteHistory()
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        var parsedBefore = WoundMaterializationContract.Parse(
            scenario.Before.ToJsonString(),
            "t066.historyCollision.before");
        Assert.True(parsedBefore.IsValid, DescribeIssues(parsedBefore.Issues));
        var before = Assert.IsType<WoundMaterializationEnvelope>(parsedBefore.Wound);
        var transition = WoundContractTestData.CreateTransition();
        transition["woundId"] = before.WoundId;
        transition["operationKey"] = "operation_t066_history_collision";
        transition["beforeFingerprint"] =
            WoundHistoryState.ComputeNonexistentBeforeFingerprint(before.WoundId);
        transition["afterFingerprint"] =
            WoundIdentityState.ComputeSemanticFingerprint(before);
        transition["sourceFingerprint"] = "sha256:" + new string('e', 64);
        transition["attemptId"] = null;
        scenario = scenario with
        {
            History = WoundContractTestData.CreateHistory(transition)
        };
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = fixture.GetAcceptedState();

        var result = InvokeCreateCoordinates(
            acceptedState,
            fixture.ReadCurrentWound(),
            "operation_t066_history_collision",
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));

        AssertInvalidPrerequisiteResult(result);
        AssertPrerequisiteIssueCode(
            result,
            "mortal_wound_treatment_coordinates_operation_key_conflict");
    }

    [Fact]
    public void PrerequisiteAuthority_GameTimeRejectsForeignCoordinatesAndReleasedAdmission()
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        using var firstFixture = AcceptedStateFixture.Create(scenario);
        using var secondFixture = AcceptedStateFixture.Create(scenario);
        var firstState = firstFixture.GetAcceptedState();
        var secondState = secondFixture.GetAcceptedState();
        var coordinates = CreatePrerequisiteCoordinates(
            firstState,
            firstFixture.ReadCurrentWound(),
            "operation_t066_foreign_coordinates",
            scenario.RouteId,
            firstFixture.AcceptedEventRef(firstState));
        var gameTimeType = RequiredPrerequisiteType("MortalWoundGameTimeAuthority");

        AssertInvalidPrerequisiteResult(Invoke(
            ExactStaticMethod(gameTimeType, "Create", 2),
            new[] { secondState, coordinates }),
            "Authority");

        firstFixture.ReleaseLeaseForExternalDistribution();
        AssertInvalidPrerequisiteResult(Invoke(
            ExactStaticMethod(gameTimeType, "Create", 2),
            new[] { firstState, coordinates }),
            "Authority");
        firstFixture.ReacquireLeaseAfterExternalDistribution();
    }

    [Fact]
    public void PrerequisiteAuthority_FirstCourseModeIsClosedDetachedAndDeterministic()
    {
        var scenario = ConfigureCourseConsequenceEnvelope(
            CreateScenario(
                "course_first_milestone_is_ready_at_inclusive_due_time",
                "course"),
            removeOwnedComplicationBeforeReduction: true);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = fixture.GetAcceptedState();
        var before = fixture.ReadCurrentWound();
        var coordinates = CreatePrerequisiteCoordinates(
            acceptedState,
            before,
            "operation_t066_first_course_mode",
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));
        var gameTimeType = RequiredPrerequisiteType("MortalWoundGameTimeAuthority");
        var gameTime = ReadValidTypedResult(
            Invoke(
                ExactStaticMethod(gameTimeType, "Create", 2),
                new[] { acceptedState, coordinates }),
            "Authority",
            "first-course game time");
        var courseType = RequiredPrerequisiteType("MortalWoundCourseModeAuthority");
        var create = ExactStaticMethod(courseType, "Create", 5);

        var firstResult = Invoke(
            create,
            new object[]
            {
                acceptedState,
                coordinates,
                before,
                fixture.ReadCurrentHistory(),
                gameTime
            });
        var secondResult = Invoke(
            create,
            new object[]
            {
                acceptedState,
                coordinates,
                before,
                fixture.ReadCurrentHistory(),
                gameTime
            });

        AssertClosedProperties(firstResult, new[] { "Disposition", "Issues", "Authority" });
        Assert.Equal("Ready", ReadRequiredProperty(firstResult, "Disposition"));
        Assert.Empty(AsObjects(ReadRequiredProperty(firstResult, "Issues")));
        var first = ReadRequiredProperty(firstResult, "Authority");
        var second = ReadRequiredProperty(secondResult, "Authority");
        AssertClosedProperties(first, new[]
        {
            "GameTimeAuthority", "CourseId", "MilestoneOrdinal", "DueAtGameTimeMinutes",
            "DeadlineAtGameTimeMinutes", "WindowDisposition", "CourseStartAuthority",
            "CourseCoordinateFingerprint", "CoordinatesFingerprint", "AcceptedStateFingerprint",
            "AuthorityFingerprint"
        });
        Assert.Empty(first.GetType().GetConstructors());
        Assert.Equal(1, Convert.ToInt32(ReadRequiredProperty(first, "MilestoneOrdinal")));
        Assert.Equal(scenario.WorldMinute,
            Convert.ToInt64(ReadRequiredProperty(first, "DueAtGameTimeMinutes")));
        Assert.Equal(scenario.WorldMinute + 600,
            Convert.ToInt64(ReadRequiredProperty(first, "DeadlineAtGameTimeMinutes")));
        Assert.Equal("ready", ReadRequiredProperty(first, "WindowDisposition"));
        Assert.Same(gameTime, ReadRequiredProperty(first, "GameTimeAuthority"));
        Assert.Equal(ReadRequiredProperty(coordinates, "CoordinatesFingerprint"),
            ReadRequiredProperty(first, "CoordinatesFingerprint"));
        Assert.Equal(ReadRequiredProperty(coordinates, "AcceptedStateFingerprint"),
            ReadRequiredProperty(first, "AcceptedStateFingerprint"));
        AssertAuthorityFingerprint(ReadRequiredProperty(first, "CourseCoordinateFingerprint"));
        AssertAuthorityFingerprint(ReadRequiredProperty(first, "AuthorityFingerprint"));

        var courseId = Assert.IsType<string>(ReadRequiredProperty(first, "CourseId"));
        Assert.True(ResourceMaterializationContract.IsExactIdentifier(courseId));
        Assert.Equal(courseId, ReadRequiredProperty(second, "CourseId"));
        Assert.Equal(ReadRequiredProperty(first, "CourseCoordinateFingerprint"),
            ReadRequiredProperty(second, "CourseCoordinateFingerprint"));
        Assert.Equal(ReadRequiredProperty(first, "AuthorityFingerprint"),
            ReadRequiredProperty(second, "AuthorityFingerprint"));

        var start = ReadRequiredProperty(first, "CourseStartAuthority");
        AssertClosedProperties(start, new[]
        {
            "CourseId", "RouteId", "RouteFingerprint", "StartingWound",
            "StartingWoundFingerprint", "StartedAtGameTimeMinutes",
            "AcceptedStateFingerprint", "CoordinatesFingerprint", "AuthorityFingerprint"
        });
        Assert.Empty(start.GetType().GetConstructors());
        Assert.Equal(courseId, ReadRequiredProperty(start, "CourseId"));
        Assert.Equal(scenario.RouteId, ReadRequiredProperty(start, "RouteId"));
        AssertAuthorityFingerprint(ReadRequiredProperty(start, "RouteFingerprint"));
        Assert.Equal(scenario.WorldMinute,
            Convert.ToInt64(ReadRequiredProperty(start, "StartedAtGameTimeMinutes")));
        Assert.Equal(ReadRequiredProperty(coordinates, "ExpectedBeforeFingerprint"),
            ReadRequiredProperty(start, "StartingWoundFingerprint"));
        Assert.Equal(ReadRequiredProperty(coordinates, "AcceptedStateFingerprint"),
            ReadRequiredProperty(start, "AcceptedStateFingerprint"));
        Assert.Equal(ReadRequiredProperty(coordinates, "CoordinatesFingerprint"),
            ReadRequiredProperty(start, "CoordinatesFingerprint"));
        AssertAuthorityFingerprint(ReadRequiredProperty(start, "AuthorityFingerprint"));
        var startingWound = Assert.IsType<WoundMaterializationEnvelope>(
            ReadRequiredProperty(start, "StartingWound"));
        Assert.NotSame(before, startingWound);
        Assert.Equal(
            WoundMaterializationContract.SerializeCanonical(before),
            WoundMaterializationContract.SerializeCanonical(startingWound));
    }

    [Theory]
    [InlineData(false, "InvalidAuthority")]
    [InlineData(true, "Ready")]
    public void PrerequisiteAuthority_CourseSequenceValidatesCompleteConsequenceEnvelope(
        bool removeOwnedComplicationBeforeReduction,
        string expectedDisposition)
    {
        var scenario = ConfigureCourseConsequenceEnvelope(
            CreateScenario(
                "course_first_milestone_is_ready_at_inclusive_due_time",
                "course"),
            removeOwnedComplicationBeforeReduction);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = fixture.GetAcceptedState();
        var before = fixture.ReadCurrentWound();
        var coordinates = CreatePrerequisiteCoordinates(
            acceptedState,
            before,
            "operation_t066_course_consequence_envelope_" +
            (removeOwnedComplicationBeforeReduction ? "aligned" : "overflow"),
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));
        var gameTime = ReadValidTypedResult(
            Invoke(
                ExactStaticMethod(
                    RequiredPrerequisiteType("MortalWoundGameTimeAuthority"),
                    "Create",
                    2),
                new[] { acceptedState, coordinates }),
            "Authority",
            "course consequence-envelope game time");

        var result = Invoke(
            ExactStaticMethod(
                RequiredPrerequisiteType("MortalWoundCourseModeAuthority"),
                "Create",
                5),
            new object[]
            {
                acceptedState,
                coordinates,
                before,
                fixture.ReadCurrentHistory(),
                gameTime
            });

        Assert.Equal(expectedDisposition, ReadRequiredProperty(result, "Disposition"));
        if (removeOwnedComplicationBeforeReduction)
        {
            Assert.NotNull(ReadPropertyAllowingNull(result, "Authority"));
            Assert.Empty(AsObjects(ReadRequiredProperty(result, "Issues")));
        }
        else
        {
            Assert.Null(ReadPropertyAllowingNull(result, "Authority"));
            AssertPrerequisiteIssueCode(
                result,
                "mortal_wound_treatment_course_sequence_inapplicable");
        }
    }

    [Fact]
    public void PrerequisiteAuthority_WorkingWoundSeparatesApplicabilityFromImprovement()
    {
        var parsed = WoundMaterializationContract.Parse(
            CreateScenario(
                    "procedure_disadvantage_uses_two_contiguous_dice",
                    "procedure")
                .Before
                .ToJsonString(),
            "t066.workingWound.noImprovement");
        Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));

        var simulation = MortalWoundTreatmentWorkingWoundSimulator.Simulate(
            Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound),
            new[]
            {
                ImmutableArray.Create<MortalWoundTreatmentOperation>(
                    new MortalWoundNoImprovementOperation())
            });

        Assert.True(simulation.IsApplicable);
        Assert.False(simulation.Improved);
        Assert.NotNull(simulation.WorkingWound);
    }

    [Fact]
    public void PrerequisiteAuthority_WorkingWoundReportsImprovingCourseAndGuaranteedResults()
    {
        var courseScenario = ConfigureCourseConsequenceEnvelope(
            CreateScenario(
                "course_first_milestone_is_ready_at_inclusive_due_time",
                "course"),
            removeOwnedComplicationBeforeReduction: true);
        var parsedCourse = WoundMaterializationContract.Parse(
            courseScenario.Before.ToJsonString(),
            "t066.workingWound.course");
        Assert.True(parsedCourse.IsValid, DescribeIssues(parsedCourse.Issues));
        var courseBefore = Assert.IsType<WoundMaterializationEnvelope>(parsedCourse.Wound);
        var typedCourse = MortalWoundTreatmentContract.ParseProjection(
            courseBefore.Treatment,
            "t066.workingWound.course.treatment",
            courseBefore.Owner.Realm,
            "player",
            courseBefore.Severity.Rank,
            courseBefore.Complications,
            courseBefore.Recovery.DeteriorationPolicy);
        Assert.True(typedCourse.IsValid, DescribeIssues(typedCourse.Issues));
        var courseRoute = Assert.IsType<MortalWoundCourseRouteDefinition>(
            Assert.Single(typedCourse.Treatment!.Routes));

        var course = MortalWoundTreatmentWorkingWoundSimulator.Simulate(
            courseBefore,
            courseRoute.Milestones.Select(static milestone => milestone.DeclaredResult));

        Assert.True(course.IsApplicable);
        Assert.True(course.Improved);
        Assert.NotNull(course.WorkingWound);

        var guaranteedScenario = CreateScenario(
            "guaranteed_current_capability_proof_stabilizes",
            "guaranteed");
        var parsedGuaranteed = WoundMaterializationContract.Parse(
            guaranteedScenario.Before.ToJsonString(),
            "t066.workingWound.guaranteed");
        Assert.True(parsedGuaranteed.IsValid, DescribeIssues(parsedGuaranteed.Issues));
        var guaranteedBefore = Assert.IsType<WoundMaterializationEnvelope>(
            parsedGuaranteed.Wound);
        var typedGuaranteed = MortalWoundTreatmentContract.ParseProjection(
            guaranteedBefore.Treatment,
            "t066.workingWound.guaranteed.treatment",
            guaranteedBefore.Owner.Realm,
            "player",
            guaranteedBefore.Severity.Rank,
            guaranteedBefore.Complications,
            guaranteedBefore.Recovery.DeteriorationPolicy);
        Assert.True(typedGuaranteed.IsValid, DescribeIssues(typedGuaranteed.Issues));
        var guaranteedRoute = Assert.IsType<MortalWoundGuaranteedRouteDefinition>(
            Assert.Single(typedGuaranteed.Treatment!.Routes));

        var guaranteed = MortalWoundTreatmentWorkingWoundSimulator.Simulate(
            guaranteedBefore,
            new[] { guaranteedRoute.Outcome.DeclaredResult });

        Assert.True(guaranteed.IsApplicable);
        Assert.True(guaranteed.Improved);
        Assert.NotNull(guaranteed.WorkingWound);
    }

    [Fact]
    public void PrerequisiteAuthority_WorkingWoundUsesPreparedAdverseOperationSeam()
    {
        var parsed = WoundMaterializationContract.Parse(
            CreateScenario(
                    "procedure_normal_uses_lowest_free_die",
                    "procedure")
                .Before
                .ToJsonString(),
            "t066.workingWound.preparedAdverse");
        Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
        var before = Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
        var results = new[]
        {
            ImmutableArray.Create<MortalWoundTreatmentOperation>(
                new MortalWoundApplyDeteriorationOperation("policy_t066_prepared"))
        };

        Assert.False(MortalWoundTreatmentWorkingWoundSimulator
            .Simulate(before, results)
            .IsApplicable);

        var prepared = MortalWoundTreatmentWorkingWoundSimulator.Simulate(
            before,
            results,
            static (working, _) => new MortalWoundTreatmentPreparedOperationResult(
                true,
                false,
                working));

        Assert.True(prepared.IsApplicable);
        Assert.False(prepared.Improved);
        Assert.NotNull(prepared.WorkingWound);
    }

    [Fact]
    public void PrerequisiteAuthority_CourseRejectsReleasedAcceptedState()
    {
        var scenario = ConfigureCourseConsequenceEnvelope(
            CreateScenario(
                "course_first_milestone_is_ready_at_inclusive_due_time",
                "course"),
            removeOwnedComplicationBeforeReduction: true);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = fixture.GetAcceptedState();
        var before = fixture.ReadCurrentWound();
        var coordinates = CreatePrerequisiteCoordinates(
            acceptedState,
            before,
            "operation_t066_course_released_state",
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));
        var gameTime = CreatePrerequisiteGameTime(acceptedState, coordinates);

        fixture.ReleaseLeaseForExternalDistribution();
        try
        {
            AssertInvalidCourseResult(
                InvokeCreateCourseMode(
                    acceptedState,
                    coordinates,
                    before,
                    fixture.ReadCurrentHistory(),
                    gameTime),
                "mortal_wound_treatment_course_authority_invalid");
        }
        finally
        {
            fixture.ReacquireLeaseAfterExternalDistribution();
        }
    }

    [Fact]
    public void PrerequisiteAuthority_CourseRejectsValidForeignHistory()
    {
        var scenario = ConfigureCourseConsequenceEnvelope(
            CreateScenario(
                "course_first_milestone_is_ready_at_inclusive_due_time",
                "course"),
            removeOwnedComplicationBeforeReduction: true);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = fixture.GetAcceptedState();
        var before = fixture.ReadCurrentWound();
        var coordinates = CreatePrerequisiteCoordinates(
            acceptedState,
            before,
            "operation_t066_course_foreign_history",
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));
        var foreignHistory = WoundHistoryState.Parse(
            CreateCurrentWoundHistory(scenario.Before).ToJsonString(),
            "t066.foreignCourseHistory");
        Assert.True(foreignHistory.IsValid, DescribeIssues(foreignHistory.Issues));

        AssertInvalidCourseResult(
            InvokeCreateCourseMode(
                acceptedState,
                coordinates,
                before,
                foreignHistory,
                CreatePrerequisiteGameTime(acceptedState, coordinates)),
            "mortal_wound_treatment_course_authority_invalid");
    }

    [Fact]
    public void PrerequisiteAuthority_CourseRejectsForeignGameTime()
    {
        var scenario = ConfigureCourseConsequenceEnvelope(
            CreateScenario(
                "course_first_milestone_is_ready_at_inclusive_due_time",
                "course"),
            removeOwnedComplicationBeforeReduction: true);
        using var firstFixture = AcceptedStateFixture.Create(scenario);
        using var secondFixture = AcceptedStateFixture.Create(scenario);
        var firstState = firstFixture.GetAcceptedState();
        var secondState = secondFixture.GetAcceptedState();
        var firstBefore = firstFixture.ReadCurrentWound();
        var firstCoordinates = CreatePrerequisiteCoordinates(
            firstState,
            firstBefore,
            "operation_t066_course_foreign_time_first",
            scenario.RouteId,
            firstFixture.AcceptedEventRef(firstState));
        var secondCoordinates = CreatePrerequisiteCoordinates(
            secondState,
            secondFixture.ReadCurrentWound(),
            "operation_t066_course_foreign_time_second",
            scenario.RouteId,
            secondFixture.AcceptedEventRef(secondState));

        AssertInvalidCourseResult(
            InvokeCreateCourseMode(
                firstState,
                firstCoordinates,
                firstBefore,
                firstFixture.ReadCurrentHistory(),
                CreatePrerequisiteGameTime(secondState, secondCoordinates)),
            "mortal_wound_treatment_course_authority_invalid");
    }

    [Fact]
    public void PrerequisiteAuthority_FirstCourseRejectsExistingActiveCourse()
    {
        var scenario = ConfigureCourseConsequenceEnvelope(
            CreateScenario(
                "course_first_milestone_is_ready_at_inclusive_due_time",
                "course"),
            removeOwnedComplicationBeforeReduction: true);
        var before = scenario.Before.DeepClone().AsObject();
        before["care"]!["activeCourseId"] = "mortal_wound_course_existing_t066";
        scenario = scenario with { Before = before };
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = fixture.GetAcceptedState();
        var current = fixture.ReadCurrentWound();
        var coordinates = CreatePrerequisiteCoordinates(
            acceptedState,
            current,
            "operation_t066_course_active_conflict",
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));

        AssertInvalidCourseResult(
            InvokeCreateCourseMode(
                acceptedState,
                coordinates,
                current,
                fixture.ReadCurrentHistory(),
                CreatePrerequisiteGameTime(acceptedState, coordinates)),
            "mortal_wound_treatment_course_start_conflict");
    }

    [Fact]
    public void PrerequisiteAuthority_FirstCourseRejectsDeadlineOverflow()
    {
        var scenario = ConfigureCourseConsequenceEnvelope(
            CreateScenario(
                "course_first_milestone_is_ready_at_inclusive_due_time",
                "course"),
            removeOwnedComplicationBeforeReduction: true);
        var before = scenario.Before.DeepClone().AsObject();
        before["treatment"]!["routes"]![0]!["resolution"]!["maximumGapMinutes"] =
            long.MaxValue;
        var acceptedStateInput = scenario.AcceptedState.DeepClone().AsObject();
        acceptedStateInput["worldMinute"] = 1L;
        scenario = scenario with
        {
            Before = before,
            AcceptedState = acceptedStateInput,
            WorldMinute = 1L
        };
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = fixture.GetAcceptedState();
        var current = fixture.ReadCurrentWound();
        var coordinates = CreatePrerequisiteCoordinates(
            acceptedState,
            current,
            "operation_t066_course_deadline_overflow",
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));

        AssertInvalidCourseResult(
            InvokeCreateCourseMode(
                acceptedState,
                coordinates,
                current,
                fixture.ReadCurrentHistory(),
                CreatePrerequisiteGameTime(acceptedState, coordinates)),
            "mortal_wound_treatment_course_start_invalid");
    }

    [Fact]
    public void PrerequisiteAuthority_FirstCourseDetectsDeterministicCourseIdCollision()
    {
        var scenario = ConfigureCourseConsequenceEnvelope(
            CreateScenario(
                "course_first_milestone_is_ready_at_inclusive_due_time",
                "course"),
            removeOwnedComplicationBeforeReduction: true);
        const string operationKey = "operation_t066_course_identity_collision";
        string courseId;
        using (var initialFixture = AcceptedStateFixture.Create(scenario))
        {
            var initialState = initialFixture.GetAcceptedState();
            var initialBefore = initialFixture.ReadCurrentWound();
            var initialCoordinates = CreatePrerequisiteCoordinates(
                initialState,
                initialBefore,
                operationKey,
                scenario.RouteId,
                initialFixture.AcceptedEventRef(initialState));
            var initialResult = InvokeCreateCourseMode(
                initialState,
                initialCoordinates,
                initialBefore,
                initialFixture.ReadCurrentHistory(),
                CreatePrerequisiteGameTime(initialState, initialCoordinates));
            Assert.Equal("Ready", ReadRequiredProperty(initialResult, "Disposition"));
            courseId = Assert.IsType<string>(ReadRequiredProperty(
                ReadRequiredProperty(initialResult, "Authority"),
                "CourseId"));
        }

        var collisionHistory = WoundHistoryState.Parse(
            CreateCurrentWoundHistory(scenario.Before, courseId).ToJsonString(),
            "t066.courseIdentityCollision.history");
        Assert.True(collisionHistory.IsValid, DescribeIssues(collisionHistory.Issues));
        var state = Assert.IsType<WoundHistoryState>(collisionHistory.State);
        var collision = Assert.Single(state.Transitions);
        Assert.Equal(courseId, collision.CourseId);
        Assert.Equal(1, collision.CourseMilestoneOrdinal);
        Assert.True(MortalWoundCourseModeAuthority.HasCourseIdentityConflict(
            state,
            courseId));
    }

    [Fact]
    public void PrerequisiteAuthority_CourseIdentityUsesCompleteAcceptedTurnScope()
    {
        var owner = new WoundOwnerCoordinate(
            "mortal_world",
            "player",
            "player_current",
            "game_state/player/player.json");
        var scope = new WoundAcceptedTurnIdentityScope(
            "session_t066_identity",
            "request_t066_identity",
            "snapshot_t066_identity",
            "mortal_world",
            42,
            "sha256:" + new string('a', 64),
            "turn_42:accepted_effect",
            "opportunity_t066_identity",
            owner,
            "treatment_course",
            "operation_t066_identity",
            "wound_t066_identity");
        var routeFingerprint = "sha256:" + new string('b', 64);

        var baseline = MortalWoundCourseModeAuthority.CreateCourseId(
            scope,
            routeFingerprint,
            1_260);
        Assert.Equal(
            baseline,
            MortalWoundCourseModeAuthority.CreateCourseId(
                scope with { Owner = owner with { } },
                routeFingerprint,
                1_260));
        Assert.True(ResourceMaterializationContract.IsExactIdentifier(baseline));

        var distinctScopes = new[]
        {
            scope with { RequestId = "request_t066_identity_changed" },
            scope with { SnapshotToken = "snapshot_t066_identity_changed" },
            scope with { Realm = "afterlife" },
            scope with { Turn = 43 },
            scope with { AcceptedEventsFingerprint = "sha256:" + new string('c', 64) },
            scope with { EventRef = "turn_42:accepted_effect_changed" },
            scope with
            {
                Owner = owner with
                {
                    CarrierPath = "game_state/player/player_changed.json"
                }
            }
        };
        foreach (var changed in distinctScopes)
        {
            Assert.NotEqual(
                baseline,
                MortalWoundCourseModeAuthority.CreateCourseId(
                    changed,
                    routeFingerprint,
                    1_260));
        }
    }

    [Fact]
    public void PrerequisiteAuthority_RouteFingerprintIgnoresAuthoringMetadataAndBindsMechanics()
    {
        var parsed = WoundMaterializationContract.Parse(
            CreateScenario(
                    "course_first_milestone_is_ready_at_inclusive_due_time",
                    "course")
                .Before
                .ToJsonString(),
            "t066.routeFingerprint.before");
        Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
        var before = Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
        var route = Assert.Single(before.Treatment.Routes);
        var renamed = before with
        {
            Treatment = before.Treatment with
            {
                Routes = new[] { route with { DisplayName = "Другое художественное имя" } }
            }
        };
        var hidden = before with
        {
            Treatment = before.Treatment with
            {
                Routes = new[] { route with { Visibility = "hidden" } }
            }
        };
        var relocated = before with
        {
            Treatment = before.Treatment with
            {
                Routes = new[]
                {
                    route with { SourcePath = "detached/authoring/path.json" }
                }
            }
        };

        var changedRequirementNode = JsonNode.Parse(
            route.Requirements[0].GetRawText())!.AsObject();
        changedRequirementNode["providerRef"] = "field_medic_02";
        var changedRequirement = before with
        {
            Treatment = before.Treatment with
            {
                Routes = new[]
                {
                    route with
                    {
                        Requirements = new[]
                        {
                            JsonSerializer.SerializeToElement(changedRequirementNode)
                        }
                    }
                }
            }
        };
        var changedResolutionNode = JsonNode.Parse(
            route.Resolution.GetRawText())!.AsObject();
        changedResolutionNode["maximumGapMinutes"] = 601;
        var changedResolution = before with
        {
            Treatment = before.Treatment with
            {
                Routes = new[]
                {
                    route with
                    {
                        Resolution = JsonSerializer.SerializeToElement(
                            changedResolutionNode)
                    }
                }
            }
        };
        var changedOutcomeNode = JsonNode.Parse(
            route.Outcomes[1].GetRawText())!.AsObject();
        changedOutcomeNode["afterMinutes"] = 481;
        var changedOutcome = before with
        {
            Treatment = before.Treatment with
            {
                Routes = new[]
                {
                    route with
                    {
                        Outcomes = new[]
                        {
                            route.Outcomes[0],
                            JsonSerializer.SerializeToElement(changedOutcomeNode),
                            route.Outcomes[2]
                        }
                    }
                }
            }
        };

        var baseline = MortalWoundTreatmentRouteFingerprint.Compute(before, route.RouteId);
        Assert.Equal(
            baseline,
            MortalWoundTreatmentRouteFingerprint.Compute(renamed, route.RouteId));
        Assert.Equal(
            baseline,
            MortalWoundTreatmentRouteFingerprint.Compute(relocated, route.RouteId));
        Assert.NotEqual(
            baseline,
            MortalWoundTreatmentRouteFingerprint.Compute(hidden, route.RouteId));
        Assert.NotEqual(
            baseline,
            MortalWoundTreatmentRouteFingerprint.Compute(changedRequirement, route.RouteId));
        Assert.NotEqual(
            baseline,
            MortalWoundTreatmentRouteFingerprint.Compute(changedResolution, route.RouteId));
        Assert.NotEqual(
            baseline,
            MortalWoundTreatmentRouteFingerprint.Compute(changedOutcome, route.RouteId));
    }

    [Theory]
    [InlineData("procedure_normal_uses_lowest_free_die", "procedure", "CreateForProcedure", 2)]
    [InlineData("guaranteed_current_capability_proof_stabilizes", "guaranteed", "CreateForGuaranteed", 1)]
    public void PrerequisiteAuthority_NonCourseRequirementBundleWrapsExactT060Rows(
        string scenarioName,
        string mode,
        string factoryName,
        int expectedBindings)
    {
        var scenario = CreateScenario(scenarioName, mode);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = fixture.GetAcceptedState();
        var before = fixture.ReadCurrentWound();
        var coordinates = CreatePrerequisiteCoordinates(
            acceptedState,
            before,
            "operation_t066_" + mode + "_requirement_bundle",
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));

        var result = Invoke(
            ExactStaticMethod(
                RequiredPrerequisiteType(
                    "MortalWoundTreatmentRequirementAuthorityBundle"),
                factoryName,
                3),
            new[] { acceptedState, coordinates, (object)before });

        AssertClosedProperties(result, new[] { "IsValid", "Issues", "Authority" });
        Assert.True(Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")));
        Assert.Empty(AsObjects(ReadRequiredProperty(result, "Issues")));
        var bundle = ReadRequiredProperty(result, "Authority");
        AssertClosedProperties(bundle, new[]
        {
            "Mode", "ContextFingerprint", "AcceptedStateFingerprint", "RouteFingerprint",
            "CourseId", "CourseMilestoneOrdinal", "CourseCoordinateFingerprint",
            "CourseRequirementStatus", "InterruptionReason", "Scopes",
            "AuthorityFingerprint"
        });
        Assert.Equal(mode, ReadRequiredProperty(bundle, "Mode"));
        Assert.Null(ReadPropertyAllowingNull(bundle, "CourseId"));
        Assert.Null(ReadPropertyAllowingNull(bundle, "CourseMilestoneOrdinal"));
        Assert.Null(ReadPropertyAllowingNull(bundle, "CourseCoordinateFingerprint"));
        Assert.Null(ReadPropertyAllowingNull(bundle, "CourseRequirementStatus"));
        Assert.Null(ReadPropertyAllowingNull(bundle, "InterruptionReason"));
        AssertAuthorityFingerprint(ReadRequiredProperty(bundle, "RouteFingerprint"));
        AssertAuthorityFingerprint(ReadRequiredProperty(bundle, "AuthorityFingerprint"));

        var scope = Assert.Single(AsObjects(ReadRequiredProperty(bundle, "Scopes")));
        AssertClosedProperties(scope, new[]
        {
            "Scope", "CourseMilestoneOrdinal", "Status", "Bindings",
            "FailureWitnesses", "AuthorityFingerprint"
        });
        Assert.Equal("common", ReadRequiredProperty(scope, "Scope"));
        Assert.Null(ReadPropertyAllowingNull(scope, "CourseMilestoneOrdinal"));
        Assert.Equal("Satisfied", ReadRequiredProperty(scope, "Status"));
        Assert.Empty(AsObjects(ReadRequiredProperty(scope, "FailureWitnesses")));
        Assert.Equal(expectedBindings,
            AsObjects(ReadRequiredProperty(scope, "Bindings")).Count());
        AssertAuthorityFingerprint(ReadRequiredProperty(scope, "AuthorityFingerprint"));
        foreach (var binding in AsObjects(ReadRequiredProperty(scope, "Bindings")))
        {
            AssertClosedProperties(binding, new[]
            {
                "RequirementIndex", "ResolvedRequirement", "SuccessWitness",
                "BindingFingerprint"
            });
            AssertAuthorityFingerprint(ReadRequiredProperty(binding, "BindingFingerprint"));
            var witness = ReadRequiredProperty(binding, "SuccessWitness");
            foreach (var property in new[]
                     {
                         "Scope", "RequirementIndex", "Kind", "AuthorityRef",
                         "SnapshotToken", "Realm", "WitnessFingerprint"
                     })
            {
                Assert.NotNull(witness.GetType().GetProperty(property));
            }
            Assert.Equal("common", ReadRequiredProperty(witness, "Scope"));
            AssertAuthorityFingerprint(ReadRequiredProperty(witness, "WitnessFingerprint"));
        }
    }

    [Fact]
    public void PrerequisiteAuthority_FirstCourseRequirementBundleClassifiesTrustedAbsence()
    {
        var scenario = ConfigureCourseConsequenceEnvelope(
            CreateScenario(
                "course_unsatisfied_dose_requirement_has_no_reservation",
                "course"),
            removeOwnedComplicationBeforeReduction: true);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = fixture.GetAcceptedState();
        var before = fixture.ReadCurrentWound();
        var coordinates = CreatePrerequisiteCoordinates(
            acceptedState,
            before,
            "operation_t066_course_requirement_absence",
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));
        var history = fixture.ReadCurrentHistory();
        var courseMode = ReadRequiredProperty(
            InvokeCreateCourseMode(
                acceptedState,
                coordinates,
                before,
                history,
                CreatePrerequisiteGameTime(acceptedState, coordinates)),
            "Authority");

        var result = Invoke(
            ExactStaticMethod(
                RequiredPrerequisiteType(
                    "MortalWoundTreatmentRequirementAuthorityBundle"),
                "CreateForCourseMilestone",
                5),
            new[] { acceptedState, coordinates, (object)before, history, courseMode });

        AssertClosedProperties(result, new[] { "Status", "Issues", "Authority" });
        Assert.True(
            string.Equals(
                Convert.ToString(ReadRequiredProperty(result, "Status")),
                "Unsatisfied",
                StringComparison.Ordinal),
            DescribeIssues(AsObjects(ReadRequiredProperty(result, "Issues"))
                .Select(Assert.IsType<ValidationIssue>)));
        Assert.Empty(AsObjects(ReadRequiredProperty(result, "Issues")));
        var bundle = ReadRequiredProperty(result, "Authority");
        Assert.Equal("Unsatisfied", ReadRequiredProperty(bundle, "CourseRequirementStatus"));
        var scopes = AsObjects(ReadRequiredProperty(bundle, "Scopes")).ToArray();
        Assert.Equal(2, scopes.Length);
        var common = Assert.Single(scopes, scope =>
            string.Equals(
                Convert.ToString(ReadRequiredProperty(scope, "Scope")),
                "common",
                StringComparison.Ordinal));
        var milestone = Assert.Single(scopes, scope =>
            string.Equals(
                Convert.ToString(ReadRequiredProperty(scope, "Scope")),
                "course_milestone",
                StringComparison.Ordinal));
        Assert.Equal("Satisfied", ReadRequiredProperty(common, "Status"));
        Assert.Equal("Unsatisfied", ReadRequiredProperty(milestone, "Status"));
        Assert.Single(AsObjects(ReadRequiredProperty(common, "Bindings")));
        Assert.Empty(AsObjects(ReadRequiredProperty(milestone, "Bindings")));
        var failure = Assert.Single(AsObjects(
            ReadRequiredProperty(milestone, "FailureWitnesses")));
        AssertClosedProperties(failure, new[]
        {
            "Scope", "RequirementIndex", "Kind", "AuthorityRef", "LossReason",
            "Observation", "WitnessFingerprint"
        });
        Assert.Equal("authority_absent", ReadRequiredProperty(failure, "LossReason"));
        Assert.Null(ReadPropertyAllowingNull(failure, "Observation"));
        AssertAuthorityFingerprint(ReadRequiredProperty(failure, "WitnessFingerprint"));
    }

    [Fact]
    public void PrerequisiteAuthority_CourseRequirementBundleRejectsCrossScopeQuantityOverbooking()
    {
        var scenario = ConfigureCourseCrossScopeItemOverbooking(
            ConfigureCourseConsequenceEnvelope(
                CreateScenario(
                    "course_first_milestone_is_ready_at_inclusive_due_time",
                    "course"),
                removeOwnedComplicationBeforeReduction: true));
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = fixture.GetAcceptedState();
        var before = fixture.ReadCurrentWound();
        var coordinates = CreatePrerequisiteCoordinates(
            acceptedState,
            before,
            "operation_t066_cross_scope_overbooking",
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));
        var history = fixture.ReadCurrentHistory();
        var courseMode = ReadRequiredProperty(
            InvokeCreateCourseMode(
                acceptedState,
                coordinates,
                before,
                history,
                CreatePrerequisiteGameTime(acceptedState, coordinates)),
            "Authority");

        var result = Invoke(
            ExactStaticMethod(
                RequiredPrerequisiteType(
                    "MortalWoundTreatmentRequirementAuthorityBundle"),
                "CreateForCourseMilestone",
                5),
            new[] { acceptedState, coordinates, (object)before, history, courseMode });

        Assert.Equal("Unsatisfied", ReadRequiredProperty(result, "Status"));
        Assert.Empty(AsObjects(ReadRequiredProperty(result, "Issues")));
        var bundle = ReadRequiredProperty(result, "Authority");
        var scopes = AsObjects(ReadRequiredProperty(bundle, "Scopes")).ToArray();
        var common = Assert.Single(scopes, scope =>
            string.Equals(
                Convert.ToString(ReadRequiredProperty(scope, "Scope")),
                "common",
                StringComparison.Ordinal));
        var milestone = Assert.Single(scopes, scope =>
            string.Equals(
                Convert.ToString(ReadRequiredProperty(scope, "Scope")),
                "course_milestone",
                StringComparison.Ordinal));
        Assert.Equal("Satisfied", ReadRequiredProperty(common, "Status"));
        Assert.Single(AsObjects(ReadRequiredProperty(common, "Bindings")));
        Assert.Equal("Unsatisfied", ReadRequiredProperty(milestone, "Status"));
        Assert.Empty(AsObjects(ReadRequiredProperty(milestone, "Bindings")));
        var failure = Assert.Single(AsObjects(
            ReadRequiredProperty(milestone, "FailureWitnesses")));
        Assert.Equal("quantity_insufficient", ReadRequiredProperty(failure, "LossReason"));
        var observation = Assert.IsType<MortalWoundTreatmentRequirementFailureObservation>(
            ReadRequiredProperty(failure, "Observation"));
        var evidence = Assert.IsType<MortalWoundItemQuantityRequirementEvidence>(
            observation.Evidence);
        Assert.Equal(1, evidence.RequestedQuantity);
        Assert.Equal(2, evidence.CumulativeRequestedQuantity);
        Assert.Equal(1, evidence.Count);
        Assert.Equal(1, evidence.AvailableCount);
    }

    [Theory]
    [InlineData(6, 6, "Unsatisfied", 12L)]
    [InlineData(5, 5, "Satisfied", 10L)]
    public void PrerequisiteAuthority_CourseResourceLedgerUsesOneCrossScopeExactBoundary(
        int commonQuantity,
        int milestoneQuantity,
        string expectedStatus,
        long expectedCumulativeQuantity)
    {
        var scenario = ConfigureCourseCrossScopeResourceQuantity(
            ConfigureCourseConsequenceEnvelope(
                CreateScenario(
                    "course_first_milestone_is_ready_at_inclusive_due_time",
                    "course"),
                removeOwnedComplicationBeforeReduction: true),
            commonQuantity,
            milestoneQuantity);
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerHealthForRequirementTest(10);
        var result = CreateCourseRequirementBundle(
            fixture,
            scenario,
            "operation_t066_cross_scope_resource_" + expectedStatus.ToLowerInvariant());

        Assert.Equal(expectedStatus, ReadRequiredProperty(result, "Status"));
        Assert.Empty(AsObjects(ReadRequiredProperty(result, "Issues")));
        var bundle = ReadRequiredProperty(result, "Authority");
        var scopes = AsObjects(ReadRequiredProperty(bundle, "Scopes")).ToArray();
        var common = Assert.Single(scopes, scope =>
            string.Equals(
                Convert.ToString(ReadRequiredProperty(scope, "Scope")),
                "common",
                StringComparison.Ordinal));
        var milestone = Assert.Single(scopes, scope =>
            string.Equals(
                Convert.ToString(ReadRequiredProperty(scope, "Scope")),
                "course_milestone",
                StringComparison.Ordinal));
        Assert.Equal("Satisfied", ReadRequiredProperty(common, "Status"));
        Assert.Single(AsObjects(ReadRequiredProperty(common, "Bindings")));

        if (string.Equals(expectedStatus, "Unsatisfied", StringComparison.Ordinal))
        {
            Assert.Equal("Unsatisfied", ReadRequiredProperty(milestone, "Status"));
            Assert.Empty(AsObjects(ReadRequiredProperty(milestone, "Bindings")));
            var failure = Assert.Single(AsObjects(
                ReadRequiredProperty(milestone, "FailureWitnesses")));
            Assert.Equal("quantity_insufficient", ReadRequiredProperty(failure, "LossReason"));
            var observation = Assert.IsType<MortalWoundTreatmentRequirementFailureObservation>(
                ReadRequiredProperty(failure, "Observation"));
            var evidence = Assert.IsType<MortalWoundResourceQuantityRequirementEvidence>(
                observation.Evidence);
            Assert.Equal(milestoneQuantity, evidence.RequestedQuantity);
            Assert.Equal(expectedCumulativeQuantity, evidence.CumulativeRequestedQuantity);
            Assert.Equal(10, evidence.CurrentValue);
            Assert.Equal(10, evidence.AvailableValue);
            return;
        }

        Assert.Equal("Satisfied", ReadRequiredProperty(milestone, "Status"));
        Assert.Empty(AsObjects(ReadRequiredProperty(milestone, "FailureWitnesses")));
        var binding = Assert.Single(AsObjects(ReadRequiredProperty(milestone, "Bindings")));
        var witness = ReadRequiredProperty(binding, "SuccessWitness");
        var successEvidence = Assert.IsType<MortalWoundResourceQuantityRequirementEvidence>(
            ReadRequiredProperty(witness, "Evidence"));
        Assert.Equal(milestoneQuantity, successEvidence.CumulativeRequestedQuantity);
        Assert.Equal(expectedCumulativeQuantity, successEvidence.AvailableValue);
    }

    [Theory]
    [InlineData("item_case")]
    [InlineData("item_unicode")]
    [InlineData("source_capability_case")]
    [InlineData("facility_case")]
    public void PrerequisiteAuthority_CourseFactoryRejectsConfusableCurrentReferenceAsInvalidAuthority(
        string caseName)
    {
        var scenario = ConfigureCourseConfusableCommonRequirement(
            ConfigureCourseConsequenceEnvelope(
                CreateScenario(
                    "course_first_milestone_is_ready_at_inclusive_due_time",
                    "course"),
                removeOwnedComplicationBeforeReduction: true),
            caseName);
        using var fixture = AcceptedStateFixture.Create(scenario);

        var result = CreateCourseRequirementBundle(
            fixture,
            scenario,
            "operation_t066_confusable_" + caseName);

        Assert.Equal("InvalidAuthority", ReadRequiredProperty(result, "Status"));
        Assert.Null(ReadPropertyAllowingNull(result, "Authority"));
        Assert.Contains(
            AsObjects(ReadRequiredProperty(result, "Issues"))
                .Select(Assert.IsType<ValidationIssue>),
            issue => string.Equals(
                issue.Code,
                "mortal_wound_treatment_requirement_failure_witness_invalid",
                StringComparison.Ordinal));
    }

    [Fact]
    public void PrerequisiteAuthority_RequirementBundleCreatesKindCompleteSuccessWitnesses()
    {
        var scenario = ConfigureAllRequirementKinds(
            CreateScenario(
                "procedure_advantage_uses_two_contiguous_dice",
                "procedure"));
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = fixture.GetAcceptedState();
        var before = fixture.ReadCurrentWound();
        var coordinates = CreatePrerequisiteCoordinates(
            acceptedState,
            before,
            "operation_t066_kind_complete_bundle",
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));

        var result = Invoke(
            ExactStaticMethod(
                RequiredPrerequisiteType(
                    "MortalWoundTreatmentRequirementAuthorityBundle"),
                "CreateForProcedure",
                3),
            new[] { acceptedState, coordinates, (object)before });
        var bundle = ReadValidTypedResult(
            result,
            "Authority",
            "kind-complete requirement bundle");
        var scope = Assert.Single(AsObjects(ReadRequiredProperty(bundle, "Scopes")));
        var bindings = AsObjects(ReadRequiredProperty(scope, "Bindings")).ToArray();
        Assert.Equal(11, bindings.Length);
        Assert.Equal(
            new[]
            {
                "item_quantity", "resource_quantity", "skill_tier",
                "source_capability", "provider", "consent", "facility",
                "location", "quest_state", "effect_state", "environment"
            },
            bindings.Select(binding => Convert.ToString(ReadRequiredProperty(
                ReadRequiredProperty(binding, "SuccessWitness"),
                "Kind"))));
        Assert.All(bindings, binding =>
        {
            var witness = ReadRequiredProperty(binding, "SuccessWitness");
            Assert.NotNull(ReadRequiredProperty(witness, "Evidence"));
            AssertAuthorityFingerprint(ReadRequiredProperty(witness, "WitnessFingerprint"));
        });
    }

    [Fact]
    public void PrerequisiteAuthority_SuccessWitnessFingerprintIgnoresRequirementPropertyOrder()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = fixture.GetAcceptedState();
        var context = Assert.IsType<MortalWoundTreatmentAuthority.Context>(
            ReadAcceptedStateMember(acceptedState, "RequirementContext"));
        var snapshot = Assert.IsType<MortalWoundTreatmentAuthority.Snapshot>(
            ReadAcceptedStateMember(acceptedState, "RequirementSnapshot"));
        var route = Assert.Single(fixture.ReadCurrentWound().Treatment.Routes);
        var resolved = MortalWoundTreatmentAuthority.ResolveRequirements(
            route,
            context,
            snapshot);
        Assert.True(resolved.Success, DescribeIssues(resolved.Issues));
        var row = Assert.Single(resolved.ResolvedRequirements, candidate =>
            candidate.RequirementIndex == 0);
        var canonicalOrder = CreatePrerequisiteRequirement(new JsonObject
        {
            ["kind"] = "item_quantity",
            ["itemRef"] = "sterile_thread",
            ["quantity"] = 1,
            ["ownerRole"] = "provider"
        });
        var reordered = CreatePrerequisiteRequirement(new JsonObject
        {
            ["ownerRole"] = "provider",
            ["quantity"] = 1,
            ["itemRef"] = "sterile_thread",
            ["kind"] = "item_quantity"
        });

        Assert.True(MortalWoundTreatmentRequirementWitnessFactory.TryCreateSuccess(
            "common",
            canonicalOrder,
            row,
            context,
            snapshot,
            new Dictionary<string, long>(StringComparer.Ordinal),
            out var canonicalWitness));
        Assert.True(MortalWoundTreatmentRequirementWitnessFactory.TryCreateSuccess(
            "common",
            reordered,
            row,
            context,
            snapshot,
            new Dictionary<string, long>(StringComparer.Ordinal),
            out var reorderedWitness));

        Assert.NotNull(canonicalWitness);
        Assert.NotNull(reorderedWitness);
        Assert.Equal(
            canonicalWitness.WitnessFingerprint,
            reorderedWitness.WitnessFingerprint);
    }

    [Fact]
    public void PrerequisiteAuthority_ProcedureBundleFingerprintIgnoresRequirementPropertyOrder()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = fixture.GetAcceptedState();
        var canonicalBefore = fixture.ReadCurrentWound();
        var coordinates = CreatePrerequisiteCoordinates(
            acceptedState,
            canonicalBefore,
            "operation_t066_reordered_requirement_bundle",
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));
        var reorderedRoot = scenario.Before.DeepClone().AsObject();
        reorderedRoot["treatment"]!["routes"]![0]!["requirements"]![0] =
            new JsonObject
            {
                ["ownerRole"] = "provider",
                ["quantity"] = 1,
                ["itemRef"] = "sterile_thread",
                ["kind"] = "item_quantity"
            };
        var parsedReordered = WoundMaterializationContract.Parse(
            reorderedRoot.ToJsonString(),
            "t066.reorderedRequirement.before");
        Assert.True(parsedReordered.IsValid, DescribeIssues(parsedReordered.Issues));
        var reorderedBefore = Assert.IsType<WoundMaterializationEnvelope>(
            parsedReordered.Wound);
        Assert.Equal(
            WoundIdentityState.ComputeSemanticFingerprint(canonicalBefore),
            WoundIdentityState.ComputeSemanticFingerprint(reorderedBefore));

        object Create(WoundMaterializationEnvelope before) => Invoke(
            ExactStaticMethod(
                RequiredPrerequisiteType(
                    "MortalWoundTreatmentRequirementAuthorityBundle"),
                "CreateForProcedure",
                3),
            new[] { acceptedState, coordinates, (object)before });

        var canonicalBundle = ReadValidTypedResult(
            Create(canonicalBefore),
            "Authority",
            "canonical requirement order bundle");
        var reorderedBundle = ReadValidTypedResult(
            Create(reorderedBefore),
            "Authority",
            "reordered requirement bundle");
        Assert.Equal(
            ReadRequiredProperty(canonicalBundle, "AuthorityFingerprint"),
            ReadRequiredProperty(reorderedBundle, "AuthorityFingerprint"));
        var canonicalScope = Assert.Single(AsObjects(
            ReadRequiredProperty(canonicalBundle, "Scopes")));
        var reorderedScope = Assert.Single(AsObjects(
            ReadRequiredProperty(reorderedBundle, "Scopes")));
        Assert.Equal(
            ReadRequiredProperty(canonicalScope, "AuthorityFingerprint"),
            ReadRequiredProperty(reorderedScope, "AuthorityFingerprint"));
        var canonicalBinding = AsObjects(
            ReadRequiredProperty(canonicalScope, "Bindings")).First();
        var reorderedBinding = AsObjects(
            ReadRequiredProperty(reorderedScope, "Bindings")).First();
        Assert.Equal(
            ReadRequiredProperty(
                ReadRequiredProperty(canonicalBinding, "SuccessWitness"),
                "WitnessFingerprint"),
            ReadRequiredProperty(
                ReadRequiredProperty(reorderedBinding, "SuccessWitness"),
                "WitnessFingerprint"));
    }

    [Theory]
    [InlineData("provider_unreachable", "provider_offscene")]
    [InlineData("facility_unavailable", "facility_unavailable")]
    [InlineData("consent_absent", "withdrawn_consent")]
    [InlineData("quantity_insufficient", "resource_zero_unavailable")]
    [InlineData("tier_insufficient", null)]
    [InlineData("state_mismatch", null)]
    public void PrerequisiteAuthority_CourseRequirementBundleSealsTrustedNegativeObservation(
        string expectedLossReason,
        string? canonicalMutation)
    {
        var scenario = ConfigureCourseCommonRequirement(
            ConfigureCourseConsequenceEnvelope(
                CreateScenario(
                    "course_first_milestone_is_ready_at_inclusive_due_time",
                    "course"),
                removeOwnedComplicationBeforeReduction: true),
            expectedLossReason);
        using var fixture = AcceptedStateFixture.Create(scenario);
        if (canonicalMutation is not null)
            fixture.ApplyExplicitNegativeCanonicalRow(canonicalMutation);
        var acceptedState = fixture.GetAcceptedState();
        if (string.Equals(expectedLossReason, "consent_absent", StringComparison.Ordinal))
        {
            var context = Assert.IsType<MortalWoundTreatmentAuthority.Context>(
                ReadAcceptedStateMember(acceptedState, "RequirementContext"));
            var snapshot = Assert.IsType<MortalWoundTreatmentAuthority.Snapshot>(
                ReadAcceptedStateMember(acceptedState, "RequirementSnapshot"));
            var provider = Assert.Single(snapshot.Actors, actor =>
                string.Equals(actor.ActorKind, context.ProviderKind, StringComparison.Ordinal) &&
                string.Equals(actor.ActorId, context.ProviderId, StringComparison.Ordinal));
            var consent = Assert.Single(provider.Consents, candidate =>
                string.Equals(
                    candidate.ConsentRef,
                    "consent_field_medic_player_01",
                    StringComparison.Ordinal));
            Assert.Equal("withdrawn", consent.Status);
            Assert.Equal(context.ProviderKind, consent.ProviderKind);
            Assert.Equal(context.ProviderId, consent.ProviderId);
            Assert.Equal(context.TargetKind, consent.TargetKind);
            Assert.Equal(context.TargetId, consent.TargetId);
        }
        var before = fixture.ReadCurrentWound();
        var coordinates = CreatePrerequisiteCoordinates(
            acceptedState,
            before,
            "operation_t066_negative_" + expectedLossReason,
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));
        var history = fixture.ReadCurrentHistory();
        var courseMode = ReadRequiredProperty(
            InvokeCreateCourseMode(
                acceptedState,
                coordinates,
                before,
                history,
                CreatePrerequisiteGameTime(acceptedState, coordinates)),
            "Authority");

        var result = Invoke(
            ExactStaticMethod(
                RequiredPrerequisiteType(
                    "MortalWoundTreatmentRequirementAuthorityBundle"),
                "CreateForCourseMilestone",
                5),
            new[] { acceptedState, coordinates, (object)before, history, courseMode });

        Assert.True(
            string.Equals(
                Convert.ToString(ReadRequiredProperty(result, "Status")),
                "Unsatisfied",
                StringComparison.Ordinal),
            DescribeIssues(AsObjects(ReadRequiredProperty(result, "Issues"))
                .Select(Assert.IsType<ValidationIssue>)));
        Assert.Empty(AsObjects(ReadRequiredProperty(result, "Issues")));
        var bundle = ReadRequiredProperty(result, "Authority");
        var common = Assert.Single(
            AsObjects(ReadRequiredProperty(bundle, "Scopes")),
            scope => string.Equals(
                Convert.ToString(ReadRequiredProperty(scope, "Scope")),
                "common",
                StringComparison.Ordinal));
        var failure = Assert.Single(AsObjects(
            ReadRequiredProperty(common, "FailureWitnesses")));
        Assert.Equal(expectedLossReason, ReadRequiredProperty(failure, "LossReason"));
        var observation = ReadRequiredProperty(failure, "Observation");
        Assert.NotNull(ReadRequiredProperty(observation, "Evidence"));
        AssertAuthorityFingerprint(ReadRequiredProperty(
            observation,
            "ObservationFingerprint"));
        AssertAuthorityFingerprint(ReadRequiredProperty(failure, "WitnessFingerprint"));
    }

    [Theory]
    [InlineData("quest_active_false", "retired", "inactive")]
    [InlineData("quest_retired", "retired", "retired")]
    [InlineData("skill_wrong_owner", "owner_unavailable", "owner_unavailable")]
    [InlineData("provider_wrong_location", "provider_unreachable", "wrong_location")]
    [InlineData("provider_not_present", "provider_unreachable", "actor_not_present")]
    [InlineData("facility_inactive", "facility_unavailable", "inactive")]
    [InlineData("consent_inactive", "consent_absent", "inactive")]
    [InlineData("consent_withdrawn", "consent_absent", "consent_absent")]
    [InlineData("location_wrong_location", "actor_not_present", "wrong_location")]
    [InlineData("location_not_present", "actor_not_present", "actor_not_present")]
    [InlineData("item_reserved", "reserved", "reserved")]
    [InlineData("resource_reserved", "reserved", "reserved")]
    [InlineData("resource_inactive", "inactive", "inactive")]
    public void PrerequisiteAuthority_FailureWitnessUsesClosedReasonValueMatrix(
        string caseName,
        string classifiedReason,
        string expectedReason)
    {
        var scenario = CreateFailureWitnessMatrixScenario(caseName);

        var created = MortalWoundTreatmentRequirementWitnessFactory.TryCreateFailure(
            "common",
            0,
            scenario.Requirement,
            classifiedReason,
            scenario.Context,
            scenario.Snapshot,
            new Dictionary<string, long>(StringComparer.Ordinal),
            out var failure);

        Assert.True(created);
        Assert.NotNull(failure);
        Assert.Equal(expectedReason, failure.LossReason);
        Assert.NotNull(failure.Observation);
        AssertAuthorityFingerprint(failure.Observation!.ObservationFingerprint);
        AssertAuthorityFingerprint(failure.WitnessFingerprint);
    }

    [Fact]
    public void PrerequisiteAuthority_ConfusableCurrentReferenceCannotBecomeTrustedAbsence()
    {
        var context = CreateFailureWitnessContext();
        var snapshot = CreateFailureWitnessSnapshot(items: new[]
        {
            new MortalWoundTreatmentAuthority.Item(
                "medicаl_kit",
                "Medical kit",
                "mortal_world",
                "player",
                "player_current",
                1,
                1,
                "available",
                "active",
                true)
        });
        var requirement = CreatePrerequisiteRequirement(new JsonObject
        {
            ["kind"] = "item_quantity",
            ["itemRef"] = "medical_kit",
            ["quantity"] = 1,
            ["ownerRole"] = "target"
        });

        var created = MortalWoundTreatmentRequirementWitnessFactory.TryCreateFailure(
            "common",
            0,
            requirement,
            "authority_absent",
            context,
            snapshot,
            new Dictionary<string, long>(StringComparer.Ordinal),
            out var failure);

        Assert.False(created);
        Assert.Null(failure);
    }

    [Fact]
    public void PrerequisiteAuthority_UnsupportedKindCannotBecomeTrustedAbsence()
    {
        var requirement = CreatePrerequisiteRequirement(new JsonObject
        {
            ["kind"] = "unknown_requirement",
            ["authorityRef"] = "missing_unknown_authority"
        });

        var created = MortalWoundTreatmentRequirementWitnessFactory.TryCreateFailure(
            "common",
            0,
            requirement,
            "authority_absent",
            CreateFailureWitnessContext(),
            CreateFailureWitnessSnapshot(),
            new Dictionary<string, long>(StringComparer.Ordinal),
            out var failure);

        Assert.False(created);
        Assert.Null(failure);
    }

    [Fact]
    public void PrerequisiteAuthority_ForeignRealmWrongOwnerCannotBecomeTrustedFailure()
    {
        var context = CreateFailureWitnessContext();
        var snapshot = CreateFailureWitnessSnapshot(items: new[]
        {
            new MortalWoundTreatmentAuthority.Item(
                "sterile_thread",
                "Sterile thread",
                "afterlife",
                "npc",
                "field_medic_01",
                1,
                1,
                "available",
                "active",
                true)
        });
        var requirement = CreatePrerequisiteRequirement(new JsonObject
        {
            ["kind"] = "item_quantity",
            ["itemRef"] = "sterile_thread",
            ["quantity"] = 1,
            ["ownerRole"] = "target"
        });

        var created = MortalWoundTreatmentRequirementWitnessFactory.TryCreateFailure(
            "common",
            0,
            requirement,
            "owner_unavailable",
            context,
            snapshot,
            new Dictionary<string, long>(StringComparer.Ordinal),
            out var failure);

        Assert.False(created);
        Assert.Null(failure);
    }

    [Fact]
    public void PrerequisiteAuthority_CourseRequirementBundleRejectsStaleHistoryAsInvalidAuthority()
    {
        var scenario = ConfigureCourseConsequenceEnvelope(
            CreateScenario(
                "course_first_milestone_is_ready_at_inclusive_due_time",
                "course"),
            removeOwnedComplicationBeforeReduction: true);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = fixture.GetAcceptedState();
        var before = fixture.ReadCurrentWound();
        var coordinates = CreatePrerequisiteCoordinates(
            acceptedState,
            before,
            "operation_t066_stale_requirement_history",
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));
        var currentHistory = fixture.ReadCurrentHistory();
        var courseMode = ReadRequiredProperty(
            InvokeCreateCourseMode(
                acceptedState,
                coordinates,
                before,
                currentHistory,
                CreatePrerequisiteGameTime(acceptedState, coordinates)),
            "Authority");
        var staleHistory = WoundHistoryState.Parse(
            CreateCurrentWoundHistory(scenario.Before).ToJsonString(),
            "t066.staleRequirementHistory");
        Assert.True(staleHistory.IsValid, DescribeIssues(staleHistory.Issues));

        var result = Invoke(
            ExactStaticMethod(
                RequiredPrerequisiteType(
                    "MortalWoundTreatmentRequirementAuthorityBundle"),
                "CreateForCourseMilestone",
                5),
            new[]
            {
                acceptedState,
                coordinates,
                (object)before,
                staleHistory,
                courseMode
            });

        Assert.Equal("InvalidAuthority", ReadRequiredProperty(result, "Status"));
        Assert.Null(ReadPropertyAllowingNull(result, "Authority"));
        Assert.NotEmpty(AsObjects(ReadRequiredProperty(result, "Issues")));
    }

    private static (
        JsonElement Requirement,
        MortalWoundTreatmentAuthority.Context Context,
        MortalWoundTreatmentAuthority.Snapshot Snapshot)
        CreateFailureWitnessMatrixScenario(string caseName)
    {
        const string realm = "mortal_world";
        const string currentLocationId = "loc_field_clinic_001";
        const string remoteLocationId = "loc_remote_clinic_001";
        var context = new MortalWoundTreatmentAuthority.Context(
            1,
            realm,
            "player",
            "player_current",
            "npc",
            "field_medic_01",
            currentLocationId);

        JsonElement Requirement(JsonObject value)
        {
            using var document = JsonDocument.Parse(value.ToJsonString());
            return document.RootElement.Clone();
        }

        MortalWoundTreatmentAuthority.Actor Provider(
            string locationId,
            bool reachable,
            IReadOnlyList<MortalWoundTreatmentAuthority.Consent>? consents = null) => new(
            "npc",
            "field_medic_01",
            "Field medic",
            realm,
            locationId,
            "active",
            true,
            reachable,
            Array.Empty<MortalWoundTreatmentAuthority.Skill>(),
            Array.Empty<MortalWoundTreatmentAuthority.Capability>(),
            consents ?? Array.Empty<MortalWoundTreatmentAuthority.Consent>());

        MortalWoundTreatmentAuthority.Location Location(
            string locationId,
            params MortalWoundTreatmentAuthority.ActorCoordinate[] actors) => new(
            locationId,
            locationId,
            realm,
            "active",
            true,
            Array.AsReadOnly(actors));

        MortalWoundTreatmentAuthority.Snapshot Snapshot(
            IReadOnlyList<MortalWoundTreatmentAuthority.Item>? items = null,
            IReadOnlyList<MortalWoundTreatmentAuthority.Resource>? resources = null,
            IReadOnlyList<MortalWoundTreatmentAuthority.Actor>? actors = null,
            IReadOnlyList<MortalWoundTreatmentAuthority.Facility>? facilities = null,
            IReadOnlyList<MortalWoundTreatmentAuthority.Location>? locations = null,
            IReadOnlyList<MortalWoundTreatmentAuthority.Quest>? quests = null) => new(
            1,
            "snapshot_t066_failure_matrix",
            items ?? Array.Empty<MortalWoundTreatmentAuthority.Item>(),
            resources ?? Array.Empty<MortalWoundTreatmentAuthority.Resource>(),
            actors ?? Array.Empty<MortalWoundTreatmentAuthority.Actor>(),
            facilities ?? Array.Empty<MortalWoundTreatmentAuthority.Facility>(),
            locations ?? Array.Empty<MortalWoundTreatmentAuthority.Location>(),
            quests ?? Array.Empty<MortalWoundTreatmentAuthority.Quest>(),
            Array.Empty<MortalWoundTreatmentAuthority.Effect>(),
            Array.Empty<MortalWoundTreatmentAuthority.EnvironmentState>());

        var target = new MortalWoundTreatmentAuthority.ActorCoordinate(
            "player",
            "player_current");
        var provider = new MortalWoundTreatmentAuthority.ActorCoordinate(
            "npc",
            "field_medic_01");
        return caseName switch
        {
            "quest_active_false" => (
                Requirement(new JsonObject
                {
                    ["kind"] = "quest_state",
                    ["questRef"] = "quest_field_clinic_intro",
                    ["requiredState"] = "Active"
                }),
                context,
                Snapshot(quests: new[]
                {
                    new MortalWoundTreatmentAuthority.Quest(
                        "quest_field_clinic_intro", "Quest", realm, "Active",
                        "active", false)
                })),
            "quest_retired" => (
                Requirement(new JsonObject
                {
                    ["kind"] = "quest_state",
                    ["questRef"] = "quest_field_clinic_intro",
                    ["requiredState"] = "Active"
                }),
                context,
                Snapshot(quests: new[]
                {
                    new MortalWoundTreatmentAuthority.Quest(
                        "quest_field_clinic_intro", "Quest", realm, "Active",
                        "retired", false)
                })),
            "skill_wrong_owner" => (
                Requirement(new JsonObject
                {
                    ["kind"] = "skill_tier",
                    ["capabilityRef"] = "field_medicine",
                    ["minimumTier"] = 2,
                    ["actorRole"] = "provider"
                }),
                context,
                Snapshot(
                    actors: new[]
                    {
                        new MortalWoundTreatmentAuthority.Actor(
                            "player", "player_current", "Player", realm,
                            currentLocationId, "active", true, true,
                            new[]
                            {
                                new MortalWoundTreatmentAuthority.Skill(
                                    "field_medicine", "Field medicine", 3,
                                    "active", true)
                            },
                            Array.Empty<MortalWoundTreatmentAuthority.Capability>(),
                            Array.Empty<MortalWoundTreatmentAuthority.Consent>())
                    },
                    locations: new[] { Location(currentLocationId, target) })),
            "provider_wrong_location" => (
                Requirement(new JsonObject
                {
                    ["kind"] = "provider",
                    ["providerRef"] = "field_medic_01"
                }),
                context,
                Snapshot(
                    actors: new[] { Provider(remoteLocationId, reachable: true) },
                    locations: new[]
                    {
                        Location(currentLocationId, target),
                        Location(remoteLocationId, provider)
                    })),
            "provider_not_present" => (
                Requirement(new JsonObject
                {
                    ["kind"] = "provider",
                    ["providerRef"] = "field_medic_01"
                }),
                context,
                Snapshot(
                    actors: new[] { Provider(currentLocationId, reachable: true) },
                    locations: new[] { Location(currentLocationId, target) })),
            "facility_inactive" => (
                Requirement(new JsonObject
                {
                    ["kind"] = "facility",
                    ["facilityRef"] = "clean_work_surface"
                }),
                context,
                Snapshot(
                    facilities: new[]
                    {
                        new MortalWoundTreatmentAuthority.Facility(
                            "clean_work_surface", "Facility", realm,
                            currentLocationId, "active", false, true)
                    },
                    locations: new[] { Location(currentLocationId, target, provider) })),
            "consent_inactive" => ConsentScenario(
                context,
                currentLocationId,
                status: "granted",
                active: false),
            "consent_withdrawn" => ConsentScenario(
                context,
                currentLocationId,
                status: "withdrawn",
                active: true),
            "location_wrong_location" => (
                Requirement(new JsonObject
                {
                    ["kind"] = "location",
                    ["locationRef"] = remoteLocationId,
                    ["targetRole"] = "target"
                }),
                context,
                Snapshot(locations: new[] { Location(remoteLocationId, target) })),
            "location_not_present" => (
                Requirement(new JsonObject
                {
                    ["kind"] = "location",
                    ["locationRef"] = currentLocationId,
                    ["targetRole"] = "target"
                }),
                context,
                Snapshot(locations: new[] { Location(currentLocationId, provider) })),
            "item_reserved" => (
                Requirement(new JsonObject
                {
                    ["kind"] = "item_quantity",
                    ["itemRef"] = "sterile_thread",
                    ["quantity"] = 1,
                    ["ownerRole"] = "provider"
                }),
                context,
                Snapshot(items: new[]
                {
                    new MortalWoundTreatmentAuthority.Item(
                        "sterile_thread", "Sterile thread", realm,
                        "npc", "field_medic_01", 2, 0,
                        "reserved", "active", true)
                })),
            "resource_reserved" => (
                Requirement(new JsonObject
                {
                    ["kind"] = "resource_quantity",
                    ["resourceRef"] = "health",
                    ["quantity"] = 1,
                    ["ownerRole"] = "target"
                }),
                context,
                Snapshot(resources: new[]
                {
                    new MortalWoundTreatmentAuthority.Resource(
                        "health", "Health", realm,
                        "player", "player_current", 10, 0,
                        "reserved", "active", true)
                })),
            "resource_inactive" => (
                Requirement(new JsonObject
                {
                    ["kind"] = "resource_quantity",
                    ["resourceRef"] = "health",
                    ["quantity"] = 1,
                    ["ownerRole"] = "target"
                }),
                context,
                Snapshot(resources: new[]
                {
                    new MortalWoundTreatmentAuthority.Resource(
                        "health", "Health", realm,
                        "player", "player_current", 10, 0,
                        "available", "active", false)
                })),
            _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, null)
        };

        (
            JsonElement Requirement,
            MortalWoundTreatmentAuthority.Context Context,
            MortalWoundTreatmentAuthority.Snapshot Snapshot) ConsentScenario(
            MortalWoundTreatmentAuthority.Context context,
            string locationId,
            string status,
            bool active)
        {
            var consent = new MortalWoundTreatmentAuthority.Consent(
                "consent_field_medic_player_01",
                "Consent",
                "npc",
                "field_medic_01",
                "player",
                "player_current",
                status,
                "active",
                active);
            var actor = Provider(locationId, reachable: true, new[] { consent });
            return (
                Requirement(new JsonObject
                {
                    ["kind"] = "consent",
                    ["consentRef"] = "consent_field_medic_player_01",
                    ["providerRef"] = "field_medic_01",
                    ["targetRef"] = "player_current"
                }),
                context,
                Snapshot(
                    actors: new[] { actor },
                    locations: new[]
                    {
                        Location(
                            locationId,
                            new MortalWoundTreatmentAuthority.ActorCoordinate(
                                "player",
                                "player_current"),
                            new MortalWoundTreatmentAuthority.ActorCoordinate(
                                "npc",
                                "field_medic_01"))
                    }));
        }
    }

    private static MortalWoundTreatmentAuthority.Context CreateFailureWitnessContext() => new(
        1,
        "mortal_world",
        "player",
        "player_current",
        "npc",
        "field_medic_01",
        "loc_field_clinic_001");

    private static MortalWoundTreatmentAuthority.Snapshot CreateFailureWitnessSnapshot(
        IReadOnlyList<MortalWoundTreatmentAuthority.Item>? items = null,
        IReadOnlyList<MortalWoundTreatmentAuthority.Resource>? resources = null,
        IReadOnlyList<MortalWoundTreatmentAuthority.Actor>? actors = null,
        IReadOnlyList<MortalWoundTreatmentAuthority.Facility>? facilities = null,
        IReadOnlyList<MortalWoundTreatmentAuthority.Location>? locations = null,
        IReadOnlyList<MortalWoundTreatmentAuthority.Quest>? quests = null,
        IReadOnlyList<MortalWoundTreatmentAuthority.Effect>? effects = null,
        IReadOnlyList<MortalWoundTreatmentAuthority.EnvironmentState>? environments = null) => new(
        1,
        "snapshot_t066_failure_guard",
        items ?? Array.Empty<MortalWoundTreatmentAuthority.Item>(),
        resources ?? Array.Empty<MortalWoundTreatmentAuthority.Resource>(),
        actors ?? Array.Empty<MortalWoundTreatmentAuthority.Actor>(),
        facilities ?? Array.Empty<MortalWoundTreatmentAuthority.Facility>(),
        locations ?? Array.Empty<MortalWoundTreatmentAuthority.Location>(),
        quests ?? Array.Empty<MortalWoundTreatmentAuthority.Quest>(),
        effects ?? Array.Empty<MortalWoundTreatmentAuthority.Effect>(),
        environments ?? Array.Empty<MortalWoundTreatmentAuthority.EnvironmentState>());

    private static JsonElement CreatePrerequisiteRequirement(JsonObject value)
    {
        using var document = JsonDocument.Parse(value.ToJsonString());
        return document.RootElement.Clone();
    }

    private static ResolverScenario ConfigureCourseCommonRequirement(
        ResolverScenario scenario,
        string lossReason)
    {
        var before = scenario.Before.DeepClone().AsObject();
        var route = before["treatment"]!["routes"]![0]!.AsObject();
        route["requirements"] = new JsonArray(lossReason switch
        {
            "provider_unreachable" => new JsonObject
            {
                ["kind"] = "provider",
                ["providerRef"] = "field_medic_01"
            },
            "facility_unavailable" => new JsonObject
            {
                ["kind"] = "facility",
                ["facilityRef"] = "clean_work_surface"
            },
            "consent_absent" => new JsonObject
            {
                ["kind"] = "consent",
                ["consentRef"] = "consent_field_medic_player_01",
                ["providerRef"] = "field_medic_01",
                ["targetRef"] = "player_current"
            },
            "quantity_insufficient" => new JsonObject
            {
                ["kind"] = "resource_quantity",
                ["resourceRef"] = "health",
                ["quantity"] = 1,
                ["ownerRole"] = "target"
            },
            "tier_insufficient" => new JsonObject
            {
                ["kind"] = "skill_tier",
                ["capabilityRef"] = "field_medicine",
                ["minimumTier"] = 4,
                ["actorRole"] = "target"
            },
            "state_mismatch" => new JsonObject
            {
                ["kind"] = "quest_state",
                ["questRef"] = "quest_field_clinic_intro",
                ["requiredState"] = "Active"
            },
            _ => throw new ArgumentOutOfRangeException(nameof(lossReason), lossReason, null)
        });
        return scenario with { Before = before };
    }

    private static ResolverScenario ConfigureCourseCrossScopeItemOverbooking(
        ResolverScenario scenario)
    {
        var before = scenario.Before.DeepClone().AsObject();
        before["treatment"]!["routes"]![0]!["requirements"] = new JsonArray(
            new JsonObject
            {
                ["kind"] = "item_quantity",
                ["itemRef"] = "antibiotic_dose",
                ["quantity"] = 1,
                ["ownerRole"] = "target"
            });
        var acceptedState = scenario.AcceptedState.DeepClone().AsObject();
        acceptedState["antibioticDoseCount"] = 1;
        return scenario with { Before = before, AcceptedState = acceptedState };
    }

    private static ResolverScenario ConfigureCourseCrossScopeResourceQuantity(
        ResolverScenario scenario,
        int commonQuantity,
        int milestoneQuantity)
    {
        var before = scenario.Before.DeepClone().AsObject();
        var route = before["treatment"]!["routes"]![0]!.AsObject();
        route["requirements"] = new JsonArray(new JsonObject
        {
            ["kind"] = "resource_quantity",
            ["resourceRef"] = "health",
            ["quantity"] = commonQuantity,
            ["ownerRole"] = "target"
        });
        route["outcomes"]![0]!["requirements"] = new JsonArray(new JsonObject
        {
            ["kind"] = "resource_quantity",
            ["resourceRef"] = "health",
            ["quantity"] = milestoneQuantity,
            ["ownerRole"] = "target"
        });
        return scenario with { Before = before };
    }

    private static ResolverScenario ConfigureCourseConfusableCommonRequirement(
        ResolverScenario scenario,
        string caseName)
    {
        var before = scenario.Before.DeepClone().AsObject();
        before["treatment"]!["routes"]![0]!["requirements"] = new JsonArray(
            caseName switch
            {
                "item_case" => new JsonObject
                {
                    ["kind"] = "item_quantity",
                    ["itemRef"] = "ANTIBIOTIC_DOSE",
                    ["quantity"] = 1,
                    ["ownerRole"] = "target"
                },
                "item_unicode" => new JsonObject
                {
                    ["kind"] = "item_quantity",
                    ["itemRef"] = "antibiotic_dos\u0435",
                    ["quantity"] = 1,
                    ["ownerRole"] = "target"
                },
                "source_capability_case" => new JsonObject
                {
                    ["kind"] = "source_capability",
                    ["capabilityRef"] = "EXACT_MATERIALIZED_HEALING_SOURCE",
                    ["actorRole"] = "provider"
                },
                "facility_case" => new JsonObject
                {
                    ["kind"] = "facility",
                    ["facilityRef"] = "CLEAN_WORK_SURFACE"
                },
                _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, null)
            });
        return scenario with { Before = before };
    }

    private static object CreateCourseRequirementBundle(
        AcceptedStateFixture fixture,
        ResolverScenario scenario,
        string operationKey)
    {
        var acceptedState = fixture.GetAcceptedState();
        var before = fixture.ReadCurrentWound();
        var coordinates = CreatePrerequisiteCoordinates(
            acceptedState,
            before,
            operationKey,
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));
        var history = fixture.ReadCurrentHistory();
        var courseMode = ReadRequiredProperty(
            InvokeCreateCourseMode(
                acceptedState,
                coordinates,
                before,
                history,
                CreatePrerequisiteGameTime(acceptedState, coordinates)),
            "Authority");

        return Invoke(
            ExactStaticMethod(
                RequiredPrerequisiteType(
                    "MortalWoundTreatmentRequirementAuthorityBundle"),
                "CreateForCourseMilestone",
                5),
            new[] { acceptedState, coordinates, (object)before, history, courseMode });
    }

    private sealed partial class AcceptedStateFixture
    {
        internal void SetCanonicalPlayerHealthForRequirementTest(int current)
        {
            WriteCanonicalPlayerHealthAuthority(FileSystem, current);
            PrepareFreshSnapshot("t066_health_" + current);
        }
    }

    private static ResolverScenario ConfigureAllRequirementKinds(
        ResolverScenario scenario)
    {
        var before = scenario.Before.DeepClone().AsObject();
        var route = before["treatment"]!["routes"]![0]!.AsObject();
        route["requirements"] = new JsonArray(
            new JsonObject
            {
                ["kind"] = "item_quantity",
                ["itemRef"] = "sterile_thread",
                ["quantity"] = 1,
                ["ownerRole"] = "provider"
            },
            new JsonObject
            {
                ["kind"] = "resource_quantity",
                ["resourceRef"] = "health",
                ["quantity"] = 1,
                ["ownerRole"] = "target"
            },
            new JsonObject
            {
                ["kind"] = "skill_tier",
                ["capabilityRef"] = "field_medicine",
                ["minimumTier"] = 2,
                ["actorRole"] = "target"
            },
            new JsonObject
            {
                ["kind"] = "source_capability",
                ["capabilityRef"] = "exact_materialized_healing_source",
                ["actorRole"] = "provider"
            },
            new JsonObject
            {
                ["kind"] = "provider",
                ["providerRef"] = "field_medic_01"
            },
            new JsonObject
            {
                ["kind"] = "consent",
                ["consentRef"] = "consent_field_medic_player_01",
                ["providerRef"] = "field_medic_01",
                ["targetRef"] = "player_current"
            },
            new JsonObject
            {
                ["kind"] = "facility",
                ["facilityRef"] = "clean_work_surface"
            },
            new JsonObject
            {
                ["kind"] = "location",
                ["locationRef"] = "loc_field_clinic_001",
                ["targetRole"] = "target"
            },
            new JsonObject
            {
                ["kind"] = "quest_state",
                ["questRef"] = "quest_field_clinic_intro",
                ["requiredState"] = "Completed"
            },
            new JsonObject
            {
                ["kind"] = "effect_state",
                ["effectRef"] = "effect_roll_modifier_advantage",
                ["requiredState"] = "active",
                ["targetRole"] = "target"
            },
            new JsonObject
            {
                ["kind"] = "environment",
                ["environmentRef"] = "sterile_field",
                ["requiredState"] = "active"
            });
        route["resolution"]!["modifierSource"]!["requirementIndex"] = 2;
        return scenario with { Before = before };
    }

    private static ResolverScenario ConfigureCourseConsequenceEnvelope(
        ResolverScenario scenario,
        bool removeOwnedComplicationBeforeReduction)
    {
        var before = scenario.Before.DeepClone().AsObject();
        before["severity"]!["value"] = "III";
        before["severity"]!["rank"] = 3;
        before["severity"]!["maximumAtCreation"] = "III";
        before["consequences"]!["slotBudget"] = 3;
        before["consequences"]!["slotsUsed"] = 3;
        before["consequences"]!["ownedEffectSources"] =
            WoundContractTestData.CreateOwnedEffectSources(
                before["woundId"]!.GetValue<string>(),
                "mortal_world",
                ("effect_t066_course_base", "definition_t066_course_base", "periodic_damage"),
                ("effect_t066_course_complication_a", "definition_t066_course_complication_a", "action_control"),
                ("effect_t066_course_complication_b", "definition_t066_course_complication_b", "periodic_damage"));
        before["consequences"]!["entries"] = new JsonArray(
            new JsonObject
            {
                ["slot"] = 1,
                ["profileKey"] = "periodic_damage",
                ["effectId"] = "effect_t066_course_base",
                ["readableSummary"] = "Основное последствие раны сохраняется."
            },
            new JsonObject
            {
                ["slot"] = 2,
                ["profileKey"] = "action_control",
                ["effectId"] = "effect_t066_course_complication_a",
                ["readableSummary"] = "Осложнение ограничивает движение."
            },
            new JsonObject
            {
                ["slot"] = 3,
                ["profileKey"] = "periodic_damage",
                ["effectId"] = "effect_t066_course_complication_b",
                ["readableSummary"] = "Осложнение поддерживает кровотечение."
            });

        var result = new JsonArray();
        if (removeOwnedComplicationBeforeReduction)
        {
            const string complicationId = "complication_t066_course_pressure";
            before["complications"] = new JsonArray(new JsonObject
            {
                ["complicationId"] = complicationId,
                ["kind"] = "infection",
                ["state"] = "active",
                ["displayName"] = "Воспалённые края раны",
                ["treatmentDifficultyModifier"] = 1,
                ["ownedEffectIds"] = new JsonArray(
                    "effect_t066_course_complication_a",
                    "effect_t066_course_complication_b"),
                ["visibility"] = "known_to_player"
            });
            result.Add(new JsonObject
            {
                ["kind"] = "remove_complication",
                ["complicationId"] = complicationId
            });
        }
        else
        {
            before["complications"] = new JsonArray();
        }
        result.Add(new JsonObject
        {
            ["kind"] = "reduce_severity",
            ["steps"] = 2
        });

        var route = before["treatment"]!["routes"]![0]!.AsObject();
        route["resourcePolicy"]!["mutations"] = new JsonArray(CourseMutation(1));
        route["outcomes"] = new JsonArray(
            CourseMilestone(1, 0, "completed", result));

        return scenario with { Before = before };
    }

    private static object CreatePrerequisiteGameTime(
        object acceptedState,
        object coordinates) => ReadValidTypedResult(
        Invoke(
            ExactStaticMethod(
                RequiredPrerequisiteType("MortalWoundGameTimeAuthority"),
                "Create",
                2),
            new[] { acceptedState, coordinates }),
        "Authority",
        "prerequisite game-time authority");

    private static object InvokeCreateCourseMode(
        object acceptedState,
        object coordinates,
        WoundMaterializationEnvelope before,
        WoundHistoryParseResult history,
        object gameTime) => Invoke(
        ExactStaticMethod(
            RequiredPrerequisiteType("MortalWoundCourseModeAuthority"),
            "Create",
            5),
        new[] { acceptedState, coordinates, before, history, gameTime });

    private static void AssertInvalidCourseResult(object result, string expectedCode)
    {
        Assert.Equal("InvalidAuthority", ReadRequiredProperty(result, "Disposition"));
        Assert.Null(ReadPropertyAllowingNull(result, "Authority"));
        AssertPrerequisiteIssueCode(result, expectedCode);
    }

    private static JsonObject CreateCurrentWoundHistory(
        JsonObject beforeRoot,
        string? courseId = null)
    {
        var parsed = WoundMaterializationContract.Parse(
            beforeRoot.ToJsonString(),
            "t066.currentHistory.before");
        Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
        var before = Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
        var transition = WoundContractTestData.CreateTransition();
        transition["transitionId"] = before.LastTransition.TransitionId;
        transition["woundId"] = before.WoundId;
        transition["turn"] = before.LastTransition.Turn;
        transition["eventRef"] = before.Origin.EventRef;
        transition["operationKey"] = "operation_t066_current_wound_history";
        transition["beforeFingerprint"] =
            WoundHistoryState.ComputeNonexistentBeforeFingerprint(before.WoundId);
        transition["afterFingerprint"] =
            WoundIdentityState.ComputeSemanticFingerprint(before);
        transition["sourceFingerprint"] = "sha256:" + new string('e', 64);
        transition["courseId"] = courseId;
        transition["courseMilestoneOrdinal"] = courseId is null ? null : 1;
        return WoundContractTestData.CreateHistory(transition);
    }

    private static object CreatePrerequisiteCoordinates(
        object acceptedState,
        WoundMaterializationEnvelope before,
        string operationKey,
        string routeId,
        string eventRef)
    {
        var result = InvokeCreateCoordinates(
            acceptedState,
            before,
            operationKey,
            routeId,
            eventRef);
        AssertClosedProperties(result, new[] { "IsValid", "Issues", "Coordinates" });
        return ReadValidTypedResult(result, "Coordinates", operationKey + " coordinates");
    }

    private static object InvokeCreateCoordinates(
        object acceptedState,
        WoundMaterializationEnvelope before,
        string operationKey,
        string routeId,
        string eventRef)
    {
        var planner = RequiredPrerequisiteType("MortalWoundTreatmentPlanner");
        return Invoke(
            ExactStaticMethod(planner, "CreateAttemptCoordinates", 5),
            new object[] { acceptedState, before, operationKey, routeId, eventRef });
    }

    private static Type RequiredPrerequisiteType(string name) =>
        typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services." + name,
            throwOnError: false,
            ignoreCase: false) ??
        throw new Xunit.Sdk.XunitException($"T066 prerequisite authority requires {name}.");

    private static void AssertInvalidPrerequisiteResult(
        object result,
        string valueProperty = "Coordinates")
    {
        Assert.False(Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")));
        Assert.Null(ReadPropertyAllowingNull(result, valueProperty));
        Assert.NotEmpty(Assert.IsAssignableFrom<IEnumerable>(
            ReadRequiredProperty(result, "Issues")).Cast<object>());
    }

    private static void AssertPrerequisiteIssueCode(object result, string expectedCode)
    {
        var issues = Assert.IsAssignableFrom<IEnumerable>(
                ReadRequiredProperty(result, "Issues"))
            .Cast<object>()
            .Select(Assert.IsType<ValidationIssue>)
            .ToArray();
        Assert.Contains(issues, issue => string.Equals(
            issue.Code,
            expectedCode,
            StringComparison.Ordinal));
    }
}
