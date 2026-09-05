using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// RED boundary for T061.  These rows deliberately own no proof, requirement,
/// reservation, dice, Fate, receipt, or fingerprint construction.  A future
/// fixture must obtain accepted state through the canonical registry/lease path;
/// the reflection adapter below then drives only the public-to-assembly planner
/// factories and mode resolver entries documented by the treatment contract.
/// </summary>
[Trait("Category", "RegressionIntegration")]
public sealed partial class MortalWoundTreatmentResolverTests
{
    private const string PlannerTypeName =
        "BookOfEternityClient.Services.MortalWoundTreatmentPlanner";

    public static IEnumerable<object[]> ProcedureRows => Rows(
        "procedure_normal_uses_lowest_free_die",
        "procedure_advantage_uses_two_contiguous_dice",
        "procedure_disadvantage_uses_two_contiguous_dice",
        "procedure_player_natural_one_reserves_oldest_fate_shield");

    public static IEnumerable<object[]> CourseRows => Rows(
        "course_first_milestone_is_ready_at_inclusive_due_time",
        "course_unsatisfied_dose_requirement_has_no_reservation");

    public static IEnumerable<object[]> GuaranteedRows => Rows(
        "guaranteed_current_capability_proof_stabilizes",
        "guaranteed_severity_one_heal_has_empty_legacy_array");

    public static IEnumerable<object[]> CombatTreatmentRows => new[]
    {
        new object[] { "combatant", "combatant_wounded_01", "procedure" },
        new object[] { "combatant", "combatant_wounded_01", "course" },
        new object[] { "combatant_member", "combatant_member_wounded_01", "procedure" },
        new object[] { "combatant_member", "combatant_member_wounded_01", "course" }
    };

    public static IEnumerable<object[]> CourseWindowRows => new[]
    {
        new object[] { 479L, "TooEarly", null! },
        new object[] { 480L, "Ready", "ready" },
        new object[] { 1_080L, "Ready", "ready" },
        new object[] { 1_081L, "DeadlineExceeded", "deadline_exceeded" }
    };

    public static IEnumerable<object[]> ProcedureBoundaryRows => new[]
    {
        new object[] { "natural_twenty_overrides_numeric_failure", 20, 30, false, false, "success", 0 },
        new object[] { "player_natural_one_without_fate_uses_worst_band", 1, 1, false, false, "failed_attempt", 3 },
        new object[] { "npc_natural_one_ignores_player_fate", 1, 1, true, true, "failed_attempt", 3 },
        new object[] { "margin_negative_one", 11, 15, false, false, "failed_attempt", 2 },
        new object[] { "margin_zero", 12, 15, false, false, "partial_success", 1 },
        new object[] { "margin_four", 16, 15, false, false, "partial_success", 1 },
        new object[] { "margin_five", 17, 15, false, false, "success", 0 }
    };

    [Fact]
    public void IndependentControl_ExistingWoundAndEmptyHistoryRemainParseable()
    {
        var wound = WoundMaterializationContract.Parse(
            WoundContractTestData.CreateActiveWound().ToJsonString(),
            "wound");
        var history = WoundHistoryState.Parse(
            WoundContractTestData.CreateHistory().ToJsonString(),
            "history");

        Assert.True(wound.IsValid, DescribeIssues(wound.Issues));
        Assert.NotNull(wound.Wound);
        Assert.True(history.IsValid, DescribeIssues(history.Issues));
        Assert.NotNull(history.State);

    }

    [Theory]
    [InlineData("procedure")]
    [InlineData("course")]
    [InlineData("guaranteed")]
    public void FixtureControl_StrictModeSpecificRouteAndEmptyHistoryParse(string mode)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        var route = mode switch
        {
            "procedure" => StrictProcedureRoute(),
            "course" => StrictCourseRoute(),
            "guaranteed" => StrictGuaranteedRoute(),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
        wound["treatment"]!["routes"] = new JsonArray(route);
        wound["treatment"]!["knownRouteIds"] = new JsonArray(route["routeId"]!.DeepClone());
        var parsed = WoundMaterializationContract.Parse(wound.ToJsonString(), "wound");
        var history = WoundHistoryState.Parse(WoundContractTestData.CreateHistory().ToJsonString(), "history");
        Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
        Assert.True(history.IsValid, DescribeIssues(history.Issues));
        Assert.Equal(mode, Assert.Single(parsed.Wound!.Treatment.Routes).Mode);
    }

    [Theory]
    [MemberData(nameof(SemanticRows))]
    public void FixtureControl_EachRetainedScenarioHasAValidAndDistinctSemanticInput(
        string scenario,
        string mode)
    {
        var descriptor = CreateScenario(scenario, mode);
        var wound = WoundMaterializationContract.Parse(descriptor.Before.ToJsonString(), "wound");
        var history = WoundHistoryState.Parse(descriptor.History.ToJsonString(), "history");

        Assert.True(wound.IsValid, DescribeIssues(wound.Issues));
        Assert.True(history.IsValid, DescribeIssues(history.Issues));
        Assert.Equal(mode, Assert.Single(wound.Wound!.Treatment.Routes).Mode);
        Assert.Equal(descriptor.RouteId, descriptor.Before["treatment"]!["knownRouteIds"]![0]!.GetValue<string>());
        Assert.Equal(descriptor.RollMode, descriptor.AcceptedState["rollMode"]!.GetValue<string>());
        Assert.Equal(descriptor.WorldMinute, descriptor.AcceptedState["worldMinute"]!.GetValue<long>());
        Assert.Equal(descriptor.RequirementsAvailable, descriptor.AcceptedState["requirementsAvailable"]!.GetValue<bool>());
        Assert.Equal(descriptor.ExpectedNaturalRoll,
            descriptor.AcceptedState["acceptedDice"]!.AsArray()[descriptor.ExpectedSelectedSourceIndex]!.GetValue<int>());
        if (scenario.Contains("severity_one", StringComparison.Ordinal))
        {
            Assert.Equal("I", descriptor.Before["severity"]!["value"]!.GetValue<string>());
            Assert.Empty(descriptor.Before["treatment"]!["routes"]![0]!["outcomes"]![0]!["result"]![0]!["legacies"]!.AsArray());
        }
    }

    [Theory]
    [MemberData(nameof(CourseRows))]
    public void FixtureControl_CanonicalItemCarrierRootsExposeOnlyTheCurrentCourseDose(
        string scenario)
    {
        var descriptor = CreateScenario(scenario, "course");
        var sterileThread = CreateCanonicalStack("sterile_thread", 2);
        var dose = descriptor.RequirementsAvailable
            ? CreateCanonicalStack("antibiotic_dose", 8)
            : null;
        var catalog = MortalItemCarrierCatalog.Build(new MortalItemCarrierCatalogInput(
            PlayerInventoryRoot(dose),
            NpcCoreRoot(sterileThread),
            null,
            null,
            null,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal)));

        Assert.Empty(catalog.Issues);
        Assert.Single(catalog.ByItemId["sterile_thread"]);
        Assert.Equal(descriptor.RequirementsAvailable,
            catalog.ByItemId.ContainsKey("antibiotic_dose"));
    }

    [Fact]
    public void FixtureControl_CourseStartIsFirstMilestoneAtItsInclusiveDueTime()
    {
        var descriptor = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        var wound = WoundMaterializationContract.Parse(descriptor.Before.ToJsonString(), "wound");
        var history = WoundHistoryState.Parse(descriptor.History.ToJsonString(), "history");

        Assert.True(wound.IsValid, DescribeIssues(wound.Issues));
        Assert.True(history.IsValid, DescribeIssues(history.Issues));
        Assert.Null(wound.Wound!.Care.ActiveCourseId);
        Assert.Empty(history.State!.Transitions);
        Assert.Equal(1, history.State.NextOrdinal);
        var first = descriptor.Before["treatment"]!["routes"]![0]!["outcomes"]![0]!.AsObject();
        Assert.Equal(1, first["ordinal"]!.GetValue<int>());
        Assert.Equal(0L, first["afterMinutes"]!.GetValue<long>());
        Assert.Equal(0L, descriptor.WorldMinute);
    }

    [Theory]
    [MemberData(nameof(CourseWindowRows))]
    public void CourseContinuation_ReconstructsInclusiveWindowAfterPublishedStartAndRestart(
        long minute,
        string expectedDisposition,
        string? expectedWindow)
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var first = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_start",
            scenario.RouteId);
        Assert.Equal(1, Convert.ToInt32(ReadRequiredProperty(first.Request, "MilestoneOrdinal")));
        ComposeAndPublishTreatment(fixture, first);
        var activeCourseId = fixture.ReadCurrentWound().Care.ActiveCourseId;
        Assert.False(string.IsNullOrWhiteSpace(activeCourseId));

        fixture.RestartForReplay();
        fixture.PrepareNextTurn(43, minute, "course_window");
        var probe = InspectCourseMode(
            fixture,
            "operation_t061_course_window",
            scenario.RouteId);
        Assert.Equal(expectedDisposition,
            Convert.ToString(ReadRequiredProperty(probe.Result, "Disposition")));
        if (expectedWindow is null)
        {
            Assert.Null(ReadPropertyAllowingNull(probe.Result, "Authority"));
            Assert.Empty(AsObjects(ReadRequiredProperty(probe.Result, "Issues")));
            return;
        }

        var authority = ReadRequiredProperty(probe.Result, "Authority");
        Assert.Equal(2, Convert.ToInt32(ReadRequiredProperty(authority, "MilestoneOrdinal")));
        Assert.Equal(480L, Convert.ToInt64(ReadRequiredProperty(authority, "DueAtGameTimeMinutes")));
        Assert.Equal(1_080L, Convert.ToInt64(ReadRequiredProperty(authority, "DeadlineAtGameTimeMinutes")));
        Assert.Equal(expectedWindow, Convert.ToString(ReadRequiredProperty(authority, "WindowDisposition")));
        Assert.Equal(activeCourseId, Convert.ToString(ReadRequiredProperty(authority, "CourseId")));
    }

    [Fact]
    public void CourseHighLevelPreparation_TooEarlyCreatesNoRequestOrReservation()
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        scenario.AcceptedState["antibioticDoseCount"] = 2;
        const string fallbackRouteId = "procedure_t061_after_too_early_course";
        var fallbackRoute = StrictProcedureRoute();
        fallbackRoute["routeId"] = fallbackRouteId;
        fallbackRoute["requirements"]![0]!["itemRef"] = "antibiotic_dose";
        fallbackRoute["requirements"]![0]!["ownerRole"] = "target";
        scenario.Before["treatment"]!["routes"]!.AsArray().Add(fallbackRoute);
        scenario.Before["treatment"]!["knownRouteIds"]!.AsArray().Add(fallbackRouteId);
        using var fixture = AcceptedStateFixture.Create(scenario);
        ComposeAndPublishTreatment(fixture, ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_start",
            scenario.RouteId));
        Assert.Equal(1, fixture.ReadPlayerItemCount("antibiotic_dose"));
        fixture.PrepareNextTurn(43, 479, "course_too_early_high_level");
        var acceptedState = fixture.GetAcceptedState();
        var tree = CaptureResolverFixtureTree(fixture.Root);
        var result = Invoke(
            ExactStaticMethod(RequireOutcomeResolver(), "PrepareCourseMilestoneRequest", 6),
            new object?[]
            {
                acceptedState,
                fixture.ReadCurrentHistory(),
                fixture.ReadCurrentWound(),
                scenario.OperationKey + "_too_early_high_level",
                scenario.RouteId,
                fixture.AcceptedEventRef(acceptedState)
            });

        AssertInvalidTypedResult(result, "Request", "too-early high-level course preparation");
        Assert.NotEmpty(AsObjects(ReadRequiredProperty(result, "Issues"))
            .Select(Assert.IsType<ValidationIssue>));
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);

        var fallback = Invoke(
            ExactStaticMethod(RequireOutcomeResolver(), "PrepareProcedureRequest", 6),
            new object?[]
            {
                acceptedState,
                fixture.ReadCurrentHistory(),
                fixture.ReadCurrentWound(),
                scenario.OperationKey + "_after_too_early",
                fallbackRouteId,
                fixture.AcceptedEventRef(acceptedState)
            });
        var fallbackRequest = ReadValidTypedResult(
            fallback,
            "Request",
            "procedure after too-early course rejection");
        Assert.Equal(new[] { 0 }, ReadIntSequence(ReadRequiredProperty(
            ReadRequiredProperty(fallbackRequest, "ModeAuthority"),
            "SourceIndices")));
        AssertSingleHeldClaim(fallbackRequest, "antibiotic_dose");
    }

    [Fact]
    public void CourseContinuation_ReconstructsOrdinalsAcrossRestartsAndCompletionClearsPointer()
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        using var fixture = AcceptedStateFixture.Create(scenario);

        var first = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_ordinal_1",
            scenario.RouteId);
        AssertCourseAcceptedResolution(first, 1, "active", interruption: false);
        ComposeAndPublishTreatment(fixture, first);
        Assert.Equal(7, fixture.ReadPlayerItemCount("antibiotic_dose"));
        fixture.AssertItemIdentityIndexValid();

        fixture.RestartForReplay();
        fixture.PrepareNextTurn(43, 480, "course_ordinal_2");
        var second = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_ordinal_2",
            scenario.RouteId);
        AssertCourseAcceptedResolution(second, 2, "active", interruption: false);
        ComposeAndPublishTreatment(fixture, second);
        Assert.Equal(6, fixture.ReadPlayerItemCount("antibiotic_dose"));
        fixture.AssertItemIdentityIndexValid();

        fixture.RestartForReplay();
        fixture.PrepareNextTurn(44, 960, "course_ordinal_3");
        var third = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_ordinal_3",
            scenario.RouteId);
        AssertCourseAcceptedResolution(third, 3, "completed", interruption: false);
        Assert.Equal("AppendOnce", Convert.ToString(ReadRequiredProperty(
            third.Resolution,
            "RouteCompletion")));
        ComposeAndPublishTreatment(fixture, third);
        Assert.Equal(5, fixture.ReadPlayerItemCount("antibiotic_dose"));
        fixture.AssertItemIdentityIndexValid();

        var carrier = JsonNode.Parse(File.ReadAllText(fixture.FileSystem.ResolvePath(
            WoundCarrierCatalog.PlayerPath)))!.AsObject();
        Assert.Empty(carrier["activeWounds"]!.AsArray());
        var exact = ProbePublishedTreatment(fixture, third.Request);
        Assert.Equal("ExactReplay", Convert.ToString(ReadRequiredProperty(exact, "Status")));
        var receipt = ReadRequiredProperty(exact, "Receipt");
        AssertClosedTreatmentReceipt(receipt, third.Request, third.Resolution);
        Assert.Equal("completed", Convert.ToString(ReadRequiredProperty(receipt, "CourseDisposition")));
        Assert.Equal("AppendOnce", Convert.ToString(ReadRequiredProperty(receipt, "RouteCompletion")));
    }

    [Fact]
    public void CourseTrustedUnsatisfiedInterruptionClearsPointerWithoutCompletingRoute()
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var first = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_start",
            scenario.RouteId);
        ComposeAndPublishTreatment(fixture, first);
        Assert.Equal(7, fixture.ReadPlayerItemCount("antibiotic_dose"));
        fixture.RemoveCurrentPlayerDose();
        fixture.PrepareNextTurn(43, 480, "course_unsatisfied");

        var interrupted = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_unsatisfied",
            scenario.RouteId);
        AssertCourseAcceptedResolution(interrupted, 2, "interrupted", interruption: true);
        AssertCourseInterruptionRequest(
            interrupted,
            expectedMilestoneOrdinal: 2,
            expectedReason: "requirements_unsatisfied",
            expectedWindow: "ready");
        var bundle = ReadRequiredProperty(interrupted.Request, "RequirementAuthority");
        Assert.Equal("Unsatisfied", Convert.ToString(ReadRequiredProperty(
            bundle,
            "CourseRequirementStatus")));
        Assert.Equal("requirements_unsatisfied", Convert.ToString(ReadRequiredProperty(
            bundle,
            "InterruptionReason")));
        Assert.Equal("None", Convert.ToString(ReadRequiredProperty(
            interrupted.Resolution,
            "RouteCompletion")));
        Assert.Equal("none", Convert.ToString(ReadRequiredProperty(
            interrupted.Resolution,
            "ConsumptionTrigger")));
        ComposeAndPublishTreatment(fixture, interrupted);
        Assert.Equal(0, fixture.ReadPlayerItemCount("antibiotic_dose"));
        fixture.AssertItemIdentityIndexValid();

        var wound = fixture.ReadCurrentWound();
        Assert.Null(wound.Care.ActiveCourseId);
        Assert.DoesNotContain(scenario.RouteId, wound.Treatment.CompletedRouteIds);
        var exact = ProbePublishedTreatment(fixture, interrupted.Request);
        Assert.Equal("ExactReplay", Convert.ToString(ReadRequiredProperty(exact, "Status")));
        AssertClosedTreatmentReceipt(
            ReadRequiredProperty(exact, "Receipt"),
            interrupted.Request,
            interrupted.Resolution);
    }

    [Fact]
    public void CourseDeadlineDominatesSimultaneousTrustedUnsatisfiedRequirement()
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        using var fixture = AcceptedStateFixture.Create(scenario);
        ComposeAndPublishTreatment(fixture, ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_start",
            scenario.RouteId));
        Assert.Equal(7, fixture.ReadPlayerItemCount("antibiotic_dose"));
        fixture.RemoveCurrentPlayerDose();
        fixture.PrepareNextTurn(43, 1_081, "course_deadline_unsatisfied");

        var interrupted = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_deadline_unsatisfied",
            scenario.RouteId);
        AssertCourseAcceptedResolution(interrupted, 2, "interrupted", interruption: true);
        AssertCourseInterruptionRequest(
            interrupted,
            expectedMilestoneOrdinal: 2,
            expectedReason: "deadline_exceeded",
            expectedWindow: "deadline_exceeded");
        var bundle = ReadRequiredProperty(interrupted.Request, "RequirementAuthority");
        Assert.Equal("Unsatisfied", Convert.ToString(ReadRequiredProperty(
            bundle,
            "CourseRequirementStatus")));
        Assert.Equal("deadline_exceeded", Convert.ToString(ReadRequiredProperty(
            bundle,
            "InterruptionReason")));
        Assert.Equal("deadline_exceeded", Convert.ToString(ReadRequiredProperty(
            ReadRequiredProperty(interrupted.Request, "ModeAuthority"),
            "WindowDisposition")));
        ComposeAndPublishTreatment(fixture, interrupted);
        Assert.Equal(0, fixture.ReadPlayerItemCount("antibiotic_dose"));
        fixture.AssertItemIdentityIndexValid();
        Assert.Null(fixture.ReadCurrentWound().Care.ActiveCourseId);
    }

    [Fact]
    public void CourseRequirementClassifier_StaleParsedHistoryIsInvalidAuthorityNotUnsatisfied()
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        using var fixture = AcceptedStateFixture.Create(scenario);
        ComposeAndPublishTreatment(fixture, ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_start",
            scenario.RouteId));
        fixture.PrepareNextTurn(43, 480, "course_invalid_authority");
        var probe = InspectCourseMode(
            fixture,
            scenario.OperationKey + "_invalid_authority",
            scenario.RouteId);
        Assert.Equal("Ready", Convert.ToString(ReadRequiredProperty(probe.Result, "Disposition")));

        // A parseable pre-course history is still stale after ordinal one was
        // published. It cannot be reinterpreted as a trustworthy missing dose.
        var staleHistory = WoundHistoryState.Parse(
            WoundContractTestData.CreateHistory().ToJsonString(),
            WoundHistoryState.HistoryPath);
        Assert.True(staleHistory.IsValid, DescribeIssues(staleHistory.Issues));
        var bundleType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentRequirementAuthorityBundle",
            false,
            false);
        Assert.NotNull(bundleType);
        var classified = Invoke(
            ExactStaticMethod(bundleType!, "CreateForCourseMilestone", 5),
            new[]
            {
                probe.AcceptedState,
                probe.Coordinates,
                (object)probe.Before,
                staleHistory,
                ReadRequiredProperty(probe.Result, "Authority")
            });
        Assert.Equal("InvalidAuthority", Convert.ToString(ReadRequiredProperty(classified, "Status")));
        Assert.Null(ReadPropertyAllowingNull(classified, "Authority"));
        Assert.NotEmpty(AsObjects(ReadRequiredProperty(classified, "Issues"))
            .Select(Assert.IsType<ValidationIssue>));
    }

    [Fact]
    public void CourseHighLevelPreparation_StaleHistoryRejectsBeforeResourceReservation()
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        scenario.AcceptedState["antibioticDoseCount"] = 2;
        using var fixture = AcceptedStateFixture.Create(scenario);
        ComposeAndPublishTreatment(fixture, ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_start",
            scenario.RouteId));
        Assert.Equal(1, fixture.ReadPlayerItemCount("antibiotic_dose"));
        fixture.PrepareNextTurn(43, 480, "course_stale_high_level");
        var acceptedState = fixture.GetAcceptedState();
        var staleHistory = WoundHistoryState.Parse(
            WoundContractTestData.CreateHistory().ToJsonString(),
            WoundHistoryState.HistoryPath);
        Assert.True(staleHistory.IsValid, DescribeIssues(staleHistory.Issues));
        var tree = CaptureResolverFixtureTree(fixture.Root);
        var result = Invoke(
            ExactStaticMethod(RequireOutcomeResolver(), "PrepareCourseMilestoneRequest", 6),
            new object?[]
            {
                acceptedState,
                staleHistory,
                fixture.ReadCurrentWound(),
                scenario.OperationKey + "_stale_high_level",
                scenario.RouteId,
                fixture.AcceptedEventRef(acceptedState)
            });

        AssertInvalidTypedResult(result, "Request", "stale high-level course history");
        Assert.NotEmpty(AsObjects(ReadRequiredProperty(result, "Issues"))
            .Select(Assert.IsType<ValidationIssue>));
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);

        var recovered = Invoke(
            ExactStaticMethod(RequireOutcomeResolver(), "PrepareCourseMilestoneRequest", 6),
            new object?[]
            {
                acceptedState,
                fixture.ReadCurrentHistory(),
                fixture.ReadCurrentWound(),
                scenario.OperationKey + "_after_stale_history",
                scenario.RouteId,
                fixture.AcceptedEventRef(acceptedState)
            });
        var recoveredRequest = ReadValidTypedResult(
            recovered,
            "Request",
            "course retry after stale-history rejection");
        Assert.Equal(2, Convert.ToInt32(ReadRequiredProperty(
            recoveredRequest,
            "MilestoneOrdinal")));
        AssertSingleHeldClaim(recoveredRequest, "antibiotic_dose");
    }

    [Fact]
    public void CourseContinuation_CoalescesExactRetryButRejectsIndependentCourseIdMilestoneCollision()
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        using var fixture = AcceptedStateFixture.Create(scenario);
        ComposeAndPublishTreatment(fixture, ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_start",
            scenario.RouteId));
        fixture.PrepareNextTurn(43, 480, "course_coordinate_collision");
        var first = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_milestone_two_first",
            scenario.RouteId);
        var firstCoordinates = ReadRequiredProperty(first.Request, "Coordinates");
        var firstCourseAuthority = ReadRequiredProperty(first.Request, "ModeAuthority");
        Assert.Equal(
            fixture.ReadCurrentWound().Care.ActiveCourseId,
            Convert.ToString(ReadRequiredProperty(firstCourseAuthority, "CourseId")));
        Assert.Equal(2, Convert.ToInt32(ReadRequiredProperty(
            firstCourseAuthority,
            "MilestoneOrdinal")));

        var exactRetry = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_milestone_two_first",
            scenario.RouteId);
        Assert.Equal(CanonicalValue(first.Request), CanonicalValue(exactRetry.Request));
        Assert.Equal(CanonicalValue(first.Resolution), CanonicalValue(exactRetry.Resolution));

        var acceptedState = fixture.GetAcceptedState();
        var tree = CaptureResolverFixtureTree(fixture.Root);
        var conflictingOperationKey = scenario.OperationKey + "_milestone_two_conflict";
        Assert.NotEqual(
            Convert.ToString(ReadRequiredProperty(firstCoordinates, "OperationKey")),
            conflictingOperationKey);
        var conflict = Invoke(
            ExactStaticMethod(RequireOutcomeResolver(), "PrepareCourseMilestoneRequest", 6),
            new object?[]
            {
                acceptedState,
                fixture.ReadCurrentHistory(),
                fixture.ReadCurrentWound(),
                conflictingOperationKey,
                scenario.RouteId,
                fixture.AcceptedEventRef(acceptedState)
            });
        AssertInvalidTypedResult(conflict, "Request", "course milestone coordinate conflict");
        Assert.NotEmpty(AsObjects(ReadRequiredProperty(conflict, "Issues"))
            .Select(Assert.IsType<ValidationIssue>));
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
    }

    [Fact]
    public void CourseStart_RejectsSecondActiveCourseWithExactPointerDiagnostic()
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        var secondRoute = scenario.Before["treatment"]!["routes"]![0]!
            .DeepClone()
            .AsObject();
        const string secondRouteId = "course_t061_parallel_collision";
        secondRoute["routeId"] = secondRouteId;
        scenario.Before["treatment"]!["routes"]!.AsArray().Add(secondRoute);
        scenario.Before["treatment"]!["knownRouteIds"]!.AsArray().Add(secondRouteId);
        var parsed = WoundMaterializationContract.Parse(
            scenario.Before.ToJsonString(),
            "parallelCourseFixture");
        Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));

        using var fixture = AcceptedStateFixture.Create(scenario);
        ComposeAndPublishTreatment(fixture, ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_first_course",
            scenario.RouteId));
        fixture.PrepareNextTurn(43, 0, "parallel_course_collision");
        var planner = typeof(WoundMaterializationContract).Assembly.GetType(
            PlannerTypeName,
            false,
            false);
        Assert.NotNull(planner);
        var acceptedState = fixture.GetAcceptedState();
        var tree = CaptureResolverFixtureTree(fixture.Root);
        var result = Invoke(
            ExactStaticMethod(planner!, "PrepareCourseMilestoneRequest", 6),
            new object?[]
            {
                acceptedState,
                fixture.ReadCurrentHistory(),
                fixture.ReadCurrentWound(),
                scenario.OperationKey + "_second_course",
                secondRouteId,
                fixture.AcceptedEventRef(acceptedState)
            });
        AssertInvalidTypedResult(result, "Request", "second active course");
        Assert.NotEmpty(AsObjects(ReadRequiredProperty(result, "Issues"))
            .Select(Assert.IsType<ValidationIssue>));
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
    }

    [Fact]
    public void CourseStart_CheckedDeadlineOverflowRejectsBeforeCourseIdentityOrReservation()
    {
        var minute = long.MaxValue - 300;
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        scenario.AcceptedState["worldMinute"] = minute;
        scenario.AcceptedState["antibioticDoseCount"] = 1;
        const string safeRouteId = "course_t061_after_deadline_overflow";
        var safeRoute = scenario.Before["treatment"]!["routes"]![0]!
            .DeepClone()
            .AsObject();
        safeRoute["routeId"] = safeRouteId;
        safeRoute["resolution"]!["maximumGapMinutes"] = 0;
        safeRoute["outcomes"]![1]!["afterMinutes"] = 1;
        safeRoute["outcomes"]![2]!["afterMinutes"] = 2;
        scenario.Before["treatment"]!["routes"]!.AsArray().Add(safeRoute);
        scenario.Before["treatment"]!["knownRouteIds"]!.AsArray().Add(safeRouteId);
        scenario = scenario with { WorldMinute = minute };
        using var fixture = AcceptedStateFixture.Create(scenario);
        var planner = typeof(WoundMaterializationContract).Assembly.GetType(
            PlannerTypeName,
            false,
            false);
        Assert.NotNull(planner);
        var acceptedState = fixture.GetAcceptedState();
        var tree = CaptureResolverFixtureTree(fixture.Root);
        var result = Invoke(
            ExactStaticMethod(planner!, "PrepareCourseMilestoneRequest", 6),
            new object?[]
            {
                acceptedState,
                fixture.ReadCurrentHistory(),
                fixture.ReadCurrentWound(),
                scenario.OperationKey + "_overflow",
                scenario.RouteId,
                fixture.AcceptedEventRef(acceptedState)
            });
        AssertInvalidTypedResult(result, "Request", "checked course deadline overflow");
        Assert.NotEmpty(AsObjects(ReadRequiredProperty(result, "Issues"))
            .Select(Assert.IsType<ValidationIssue>));
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);

        var recovered = Invoke(
            ExactStaticMethod(planner!, "PrepareCourseMilestoneRequest", 6),
            new object?[]
            {
                acceptedState,
                fixture.ReadCurrentHistory(),
                fixture.ReadCurrentWound(),
                scenario.OperationKey + "_after_overflow",
                safeRouteId,
                fixture.AcceptedEventRef(acceptedState)
            });
        var recoveredRequest = ReadValidTypedResult(
            recovered,
            "Request",
            "course start after checked-overflow rejection");
        Assert.Equal(1, Convert.ToInt32(ReadRequiredProperty(
            recoveredRequest,
            "MilestoneOrdinal")));
        AssertSingleHeldClaim(recoveredRequest, "antibiotic_dose");
    }

    [Fact]
    public void FixtureControl_PlayerAndProviderSkillsUseProductionValidActiveSkillShapes()
    {
        using var player = JsonDocument.Parse(
            CreateTreatmentSkill("skill_field_medicine_01", "field_medicine").ToJsonString());
        using var provider = JsonDocument.Parse(
            CreateTreatmentSkill("skill_guaranteed_care_01", "exact_materialized_healing_source").ToJsonString());

        Assert.True(ValidationService.IsProductionValidMortalActiveSkill(player.RootElement));
        Assert.True(ValidationService.IsProductionValidMortalActiveSkill(provider.RootElement));
    }

    [Fact]
    public void AcceptedStateExport_UsesParsedSelectionAndCanonicalRootsToDeriveItsProductionBinding()
    {
        // T066 is the one admission boundary for a treatment attempt.  This control
        // deliberately prepares a real pending turn instead of supplying either a
        // WoundAcceptedTurnBinding or an accepted-event/fingerprint surrogate.
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        var exported = fixture.ExportCurrent();

        Assert.Equal("MortalWoundTreatmentAcceptedStateAuthorityResult", exported.GetType().Name);
        AssertClosedProperties(exported, new[] { "IsValid", "Issues", "Authority" });
        var acceptedState = ReadValidTypedResult(exported, "Authority", "T066 accepted-state export");
        Assert.Equal("MortalWoundTreatmentAcceptedStateAuthority", acceptedState.GetType().Name);
        var binding = Assert.IsType<WoundAcceptedTurnBinding>(
            ReadAcceptedStateMember(acceptedState, "Binding"));
        Assert.False(string.IsNullOrWhiteSpace(binding.SessionId));
        Assert.False(string.IsNullOrWhiteSpace(binding.RequestId));
        Assert.False(string.IsNullOrWhiteSpace(binding.SnapshotToken));
        Assert.Equal("mortal_world", binding.Realm);
        Assert.True(binding.Turn > 0);
        Assert.NotEmpty(binding.AcceptedEvents);
        Assert.False(string.IsNullOrWhiteSpace(binding.AcceptedEventsFingerprint));
        Assert.DoesNotContain(
            acceptedState.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public),
            static property => property.PropertyType == typeof(JsonObject) ||
                               property.PropertyType == typeof(JsonArray) ||
                               property.Name.Contains("Dice", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AcceptedStateExport_ProjectsOffSceneUpdateNpcAsUnreachable()
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        fixture.MoveProviderOffScene();

        var snapshot = fixture.GetRequirementSnapshot();

        var provider = Assert.Single(snapshot.Actors, actor =>
            actor.ActorKind == "npc" && actor.ActorId == "field_medic_01");
        Assert.False(provider.Reachable);
        Assert.DoesNotContain(snapshot.Locations.Single().PresentActors, actor =>
            actor.ActorKind == "npc" && actor.ActorId == "field_medic_01");
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Failed")]
    public void AcceptedStateExport_ProjectsTerminalRegularQuestStatusAsQueryable(string status)
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));

        fixture.SetRegularQuestStatus(status);

        var quest = Assert.Single(fixture.GetRequirementSnapshot().Quests);

        Assert.Equal("quest_field_clinic_intro", quest.QuestId);
        Assert.Equal(status, quest.State);
        Assert.Equal("active", quest.Lifecycle);
        Assert.True(quest.Active);
    }

    [Fact]
    public void AcceptedStateExport_OmitsCurrentSkillWithoutCanonicalMastery()
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        fixture.RemovePlayerSkillMastery();

        var player = Assert.Single(fixture.GetRequirementSnapshot().Actors, actor =>
            actor.ActorKind == "player");

        Assert.DoesNotContain(player.Skills, skill =>
            skill.CapabilityRef == "skill_field_medicine_01");
        Assert.Contains(player.Skills, skill =>
            skill.CapabilityRef == "skill_patient_observation_01");
    }

    [Fact]
    public void AcceptedStateExport_AllowsIdlessOrdinarySkillSiblingWithoutInventingTier()
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        fixture.AddIdlessOrdinarySkillSibling();

        var player = Assert.Single(fixture.GetRequirementSnapshot().Actors, actor =>
            actor.ActorKind == "player");

        Assert.DoesNotContain(player.Skills, skill => skill.DisplayName == "Ordinary observation");
    }

    [Fact]
    public void AcceptedStateExport_RejectsRetiredIdentityWithLiveItemOccurrence()
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        fixture.RetireIdentityForLiveItem();

        var result = fixture.ExportCurrent();

        Assert.False(Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")));
        Assert.Contains(
            AsObjects(ReadRequiredProperty(result, "Issues")).Select(Assert.IsType<ValidationIssue>),
            issue => issue.Code == "mortal_wound_treatment_accepted_state_item_identity_mismatch");
    }

    [Fact]
    public void AcceptedStateExport_AcceptsExactCrossCarrierNpcMirrorOnce()
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        fixture.AddExactProviderCrossCarrierMirrorWithInventory();

        var snapshot = fixture.GetRequirementSnapshot();

        var provider = Assert.Single(snapshot.Actors, actor =>
            actor.ActorKind == "npc" && actor.ActorId == "field_medic_01");
        Assert.True(provider.Reachable);
        var providerItem = Assert.Single(snapshot.Items, item =>
            item.OwnerKind == "npc" && item.OwnerId == "field_medic_01");
        Assert.Equal("sterile_thread", providerItem.ItemId);
    }

    [Fact]
    public void AcceptedStateExport_IgnoresCompatibilityNpcCapabilityLookalike()
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        fixture.AddCompatibilityNpcCapabilityLookalike();

        var provider = Assert.Single(fixture.GetRequirementSnapshot().Actors, actor =>
            actor.ActorKind == "npc" && actor.ActorId == "field_medic_01");

        Assert.DoesNotContain(provider.Capabilities, capability =>
            capability.CapabilityRef == "forged_compatibility_capability");
    }

    [Fact]
    public void AcceptedStateExport_AcceptsCanonicalNpcTargetCarrierAgreement()
    {
        using var fixture = AcceptedStateFixture.Create(CreateNpcTreatmentScenario());

        var authority = fixture.GetAcceptedState();
        var wound = Assert.IsType<WoundMaterializationEnvelope>(
            ReadAcceptedStateMember(authority, "CurrentWound"));

        Assert.Equal("npc", wound.Owner.OwnerKind);
        Assert.Equal("field_medic_01", wound.Owner.OwnerId);
        Assert.Contains(fixture.GetRequirementSnapshot().Actors, actor =>
            actor.ActorKind == "npc" && actor.ActorId == "field_medic_01");
    }

    [Fact]
    public void AcceptedStateProjection_MapsOnlyCheckedMortalActorIntegerResources()
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        Assert.True(definitions.TryResolveExact("health", out var health));
        Assert.NotNull(health);
        var decimalDefinition = health! with
        {
            ResourceKey = "focus_decimal",
            DisplayName = "Focus decimal",
            NumericKind = ResourceNumericKind.Decimal,
            Quantum = 0.1m,
            Materialization = health.Materialization with
            {
                DefinitionId = "resource_definition_focus_decimal",
                Seal = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
            }
        };
        definitions = definitions.With(decimalDefinition);
        var binding = new ResourceCapacityBinding(
            ResourceCapacityKind.InstanceFixed,
            "fixture_capacity",
            "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var chronology = new ResourceChronology(
            1,
            "turn_1:resource:1",
            "transition_fixture",
            "turn_1:resource:1",
            1);
        ResourceStateEntry Entry(
            string realm,
            ResourceOwnerKind ownerKind,
            string ownerId,
            string resourceKey,
            decimal current,
            ResourceLifecycleState state = ResourceLifecycleState.Active) =>
            new(
                new ResourceCoordinate(realm, ownerKind, ownerId, resourceKey),
                current,
                Math.Max(current, 10m),
                binding,
                state,
                chronology);
        var ledger = new ResourceStateLedger(new[]
        {
            Entry("mortal_world", ResourceOwnerKind.Player, "player_current", "health", 7m),
            Entry("mortal_world", ResourceOwnerKind.CombatGroupMember, "member_01", "health", 3m,
                ResourceLifecycleState.Suspended),
            Entry("mortal_world", ResourceOwnerKind.Item, "item_01", "charges", 4m),
            Entry("mortal_world", ResourceOwnerKind.Player, "player_current", "focus_decimal", 1.5m),
            Entry("mortal_world", ResourceOwnerKind.Npc, "npc_negative", "health", -1m),
            Entry("mortal_world", ResourceOwnerKind.Npc, "npc_large", "health", (decimal)int.MaxValue + 1m),
            Entry("chaos_sea", ResourceOwnerKind.Player, "player_current", "health", 5m)
        });

        var projected = MortalWoundTreatmentAcceptedCanonicalProjection.ProjectResources(
            definitions,
            ledger);

        var player = Assert.Single(projected, row => row.OwnerKind == "player");
        Assert.Equal(7, player.CurrentValue);
        Assert.Equal(7, player.AvailableValue);
        Assert.Equal("available", player.ReservationState);
        Assert.Equal("active", player.Lifecycle);
        Assert.True(player.Active);
        var member = Assert.Single(projected, row => row.OwnerKind == "combatant_member");
        Assert.Equal("member_01", member.OwnerId);
        Assert.Equal(3, member.CurrentValue);
        Assert.Equal(0, member.AvailableValue);
        Assert.Equal("available", member.ReservationState);
        Assert.Equal("active", member.Lifecycle);
        Assert.False(member.Active);
        Assert.Equal(2, projected.Count);
    }

    [Fact]
    public void AcceptedStateExport_RejectsStalePersistedResourceOwnerAuthority()
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        fixture.CorruptPersistedResourceOwnerAuthority();

        var result = fixture.ExportCurrent();

        Assert.False(Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")));
        Assert.Contains(
            AsObjects(ReadRequiredProperty(result, "Issues")).Select(Assert.IsType<ValidationIssue>),
            issue => issue.Code == "resource_owner_authority_root_stale");
    }

    [Fact]
    public void AcceptedStateExport_ValidatesButOmitsOffscreenAndVehicleItemSiblings()
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        fixture.AddCanonicalNonActorItemSiblings();

        var snapshot = fixture.GetRequirementSnapshot();

        Assert.DoesNotContain(snapshot.Items, item =>
            item.ItemId is "item_offscreen_t066" or "item_vehicle_t066");
        Assert.Contains(snapshot.Items, item => item.OwnerKind == "player");
        Assert.Contains(snapshot.Items, item => item.OwnerKind == "npc");
    }

    [Fact]
    public void AcceptedStateExport_RejectsAReleasedCanonicalWriteLease()
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        fixture.ReleaseLeaseForExternalDistribution();

        var result = fixture.ExportCurrent();

        Assert.False(Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")));
        Assert.Null(ReadPropertyAllowingNull(result, "Authority"));
        Assert.NotEmpty(AsObjects(ReadRequiredProperty(result, "Issues")));
        fixture.ReacquireLeaseAfterExternalDistribution();
    }

    [Theory]
    [InlineData("stale_snapshot")]
    [InlineData("carrier_identity_history_mismatch")]
    [InlineData("ambiguous_actor_coordinate")]
    [InlineData("malformed_required_root")]
    public void AcceptedStateExport_RejectsUntrustedCanonicalSourceFaults(string mutation)
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        fixture.ApplyUntrustedSourceMutation(mutation);

        var result = fixture.ExportCurrent();

        Assert.False(Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")));
        Assert.Null(ReadPropertyAllowingNull(result, "Authority"));
        Assert.NotEmpty(AsObjects(ReadRequiredProperty(result, "Issues")));
    }

    [Theory]
    [InlineData("withdrawn_consent")]
    [InlineData("resource_unavailable")]
    public void AcceptedStateExport_RetainsTrustedNegativePredicateEvidence(string mutation)
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        fixture.ApplyTrustedNegativePredicateMutation(mutation);

        var result = fixture.ExportCurrent();

        Assert.True(Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")),
            DescribeIssues(AsObjects(ReadRequiredProperty(result, "Issues"))
                .Select(Assert.IsType<ValidationIssue>)));
        Assert.NotNull(ReadRequiredProperty(result, "Authority"));
    }

    [Fact]
    public void AcceptedStateExport_ReturnsDetachedStateAfterPersistedRootMutation()
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        var result = fixture.ExportCurrent();
        var authority = ReadValidTypedResult(result, "Authority", "detached accepted state");
        var before = Assert.IsType<WoundMaterializationEnvelope>(
            ReadAcceptedStateMember(authority, "CurrentWound"));

        fixture.MutateLiveCarrierDisplayOnly();

        var detached = Assert.IsType<WoundMaterializationEnvelope>(
            ReadAcceptedStateMember(authority, "CurrentWound"));
        Assert.Equal(
            WoundIdentityState.ComputeSemanticFingerprint(before),
            WoundIdentityState.ComputeSemanticFingerprint(detached));
        Assert.NotEqual(before.Display.Name, fixture.ReadPersistedWoundDisplayName());
    }

    [Fact]
    public void AcceptedStateExport_AcceptsEmptyLegalPreTreatmentHistory()
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));

        var authority = fixture.GetAcceptedState();

        Assert.Empty(Assert.IsType<WoundHistoryState>(
            ReadAcceptedStateMember(authority, "History")).Transitions);
    }

    [Theory]
    [InlineData("latest_after_fingerprint")]
    [InlineData("latest_transition_ordinal")]
    public void AcceptedStateExport_RejectsNonEmptyHistoryThatDisagreesWithExactCarrierAndIdentity(
        string mutation)
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        fixture.ApplyHistoryAgreementMutation(mutation);

        var result = fixture.ExportCurrent();

        Assert.False(Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")));
        Assert.Null(ReadPropertyAllowingNull(result, "Authority"));
        Assert.NotEmpty(AsObjects(ReadRequiredProperty(result, "Issues")));
    }

    [Fact]
    public void AcceptedStateExport_RejectsForeignOrphanHistoryChain()
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        fixture.ApplyHistoryAgreementMutation("foreign_orphan_chain");

        var result = fixture.ExportCurrent();

        Assert.False(Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")));
        Assert.Null(ReadPropertyAllowingNull(result, "Authority"));
        Assert.NotEmpty(AsObjects(ReadRequiredProperty(result, "Issues")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AcceptedStateExport_ReplacesCachedAuthorityWhenLiveLeaseOwnerChanges(
        bool replaceFileSystemManager)
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        var first = fixture.GetAcceptedState();

        fixture.ReplaceLeaseWithoutGenerationRotation(replaceFileSystemManager);
        var second = fixture.GetAcceptedState();

        Assert.NotSame(first, second);
        fixture.AssertAuthorityBoundToCurrentLease(second);
    }

    [Theory]
    [InlineData("npc_current_location")]
    [InlineData("resource_current")]
    [InlineData("resource_state")]
    [InlineData("resource_capacity_binding")]
    public void AcceptedStateExport_RejectsMissingRequiredCanonicalRowField(string field)
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        fixture.RemoveRequiredCanonicalRowField(field);

        var result = fixture.ExportCurrent();

        Assert.False(Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")));
        Assert.Null(ReadPropertyAllowingNull(result, "Authority"));
        Assert.NotEmpty(AsObjects(ReadRequiredProperty(result, "Issues")));
    }

    [Theory]
    [InlineData("provider_offscene")]
    [InlineData("facility_unavailable")]
    [InlineData("withdrawn_consent")]
    [InlineData("resource_zero_unavailable")]
    public void AcceptedStateExport_RetainsExplicitStructurallyValidNegativeRows(string mutation)
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        fixture.ApplyExplicitNegativeCanonicalRow(mutation);

        var result = fixture.ExportCurrent();

        Assert.True(Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")),
            DescribeIssues(AsObjects(ReadRequiredProperty(result, "Issues"))
                .Select(Assert.IsType<ValidationIssue>)));
    }

    [Theory]
    [InlineData("npc")]
    [InlineData("location")]
    [InlineData("resource_state")]
    public void AcceptedStateExport_RejectsNonObjectRequiredCollectionRows(string collection)
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        fixture.AddNonObjectRequiredCollectionRow(collection);

        var result = fixture.ExportCurrent();

        Assert.False(Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")));
        Assert.Null(ReadPropertyAllowingNull(result, "Authority"));
        Assert.NotEmpty(AsObjects(ReadRequiredProperty(result, "Issues")));
    }

    [Theory]
    [InlineData(LiveTurnPreparationService.PendingTurnSnapshotManifestPath)]
    [InlineData(LiveTurnPreparationService.TurnRequestPath)]
    public void AcceptedStateExport_ReadsValidatedManifestAndRequestExactlyOnce(string path)
    {
        var interposition = new AcceptedStateReadInterposition(path);
        using var fixture = AcceptedStateFixture.Create(
            CreateScenario("procedure_normal_uses_lowest_free_die", "procedure"),
            interposition.Hooks);
        interposition.Arm();

        var result = fixture.ExportCurrent();

        Assert.True(Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")),
            DescribeIssues(AsObjects(ReadRequiredProperty(result, "Issues"))
                .Select(Assert.IsType<ValidationIssue>)));
        Assert.Equal(1, interposition.ReadCount);
    }

    [Fact]
    public void AcceptedStateExport_FingerprintsMechanicalStateWithoutDisplayProse()
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        var baseline = fixture.GetAcceptedState();

        fixture.ApplyDisplayOnlyAcceptedSourceMutation();
        var displayOnly = fixture.GetAcceptedState();

        foreach (var fingerprint in new[]
                 {
                     "ItemResourceFingerprint",
                     "ActorLocationFingerprint",
                     "PlayerCapabilityCatalogFingerprint",
                     "NpcCapabilityCatalogFingerprint",
                     "SkillSourceFingerprint"
                 })
        {
            Assert.Equal(
                ReadAcceptedStateMember(baseline, fingerprint),
                ReadAcceptedStateMember(displayOnly, fingerprint));
        }
        Assert.NotEqual(
            ReadAcceptedStateMember(baseline, "ContextFingerprint"),
            ReadAcceptedStateMember(displayOnly, "ContextFingerprint"));
        Assert.NotEqual(
            ReadAcceptedStateMember(baseline, "AcceptedStateFingerprint"),
            ReadAcceptedStateMember(displayOnly, "AcceptedStateFingerprint"));

        fixture.ApplyExplicitNegativeCanonicalRow("facility_unavailable");
        var mechanical = fixture.GetAcceptedState();
        Assert.NotEqual(
            ReadAcceptedStateMember(displayOnly, "ActorLocationFingerprint"),
            ReadAcceptedStateMember(mechanical, "ActorLocationFingerprint"));
        Assert.NotEqual(
            ReadAcceptedStateMember(displayOnly, "AcceptedStateFingerprint"),
            ReadAcceptedStateMember(mechanical, "AcceptedStateFingerprint"));
    }

    [Fact]
    public void AcceptedStateExport_RejectsCopiedParsedContextWithUnsupportedSchemaVersion()
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));

        var result = fixture.ExportWithContextSchemaVersion(2);

        Assert.False(Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")));
        Assert.Null(ReadPropertyAllowingNull(result, "Authority"));
        Assert.NotEmpty(AsObjects(ReadRequiredProperty(result, "Issues")));
    }

    [Theory]
    [InlineData("direct_construction")]
    [InlineData("copied_source_path")]
    [InlineData("copied_schema_version")]
    public void AcceptedStateExport_RejectsContextWithoutExactParserProvenance(string mutation)
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));

        var result = fixture.ExportWithForgedContext(mutation);

        Assert.False(Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")));
        Assert.Null(ReadPropertyAllowingNull(result, "Authority"));
        Assert.Contains(
            AsObjects(ReadRequiredProperty(result, "Issues")).Select(Assert.IsType<ValidationIssue>),
            issue => issue.Code == "mortal_wound_treatment_accepted_state_context_provenance_invalid");
    }

    [Fact]
    public void ContextParserProvenance_ExposesNoCallableSealSurface()
    {
        var contextType = typeof(MortalWoundTreatmentAuthority.Context);

        Assert.DoesNotContain(
            contextType.GetMethods(BindingFlags.Instance | BindingFlags.Static |
                                   BindingFlags.Public | BindingFlags.NonPublic),
            method => method.Name.Contains("SealParserProvenance", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("empty")]
    [InlineData("out_of_range")]
    public void AcceptedStateExport_RejectsInvalidSignedAcceptedDiceEvidence(string mutation)
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        fixture.ApplySignedAcceptedDiceMutation(mutation);

        var result = fixture.ExportCurrent();

        Assert.False(Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")));
        Assert.Null(ReadPropertyAllowingNull(result, "Authority"));
        Assert.Contains(
            AsObjects(ReadRequiredProperty(result, "Issues"))
                .Select(Assert.IsType<ValidationIssue>),
            issue => issue.Code == "mortal_wound_treatment_accepted_event_missing");
    }

    [Theory]
    [MemberData(nameof(ProcedureRows))]
    public void PrepareProcedureRequest_ResolvesOnlyThroughLeaseBoundAcceptedState(
        string scenario) =>
        ExecutePreparedFlow(CreateScenario(scenario, "procedure"));

    [Theory]
    [MemberData(nameof(ProcedureBoundaryRows))]
    public void ProcedureResolution_UsesExactCriticalAndMarginBoundaries(
        string name,
        int naturalRoll,
        int difficulty,
        bool fixedZero,
        bool seedPlayerFate,
        string expectedCategory,
        int expectedBandIndex)
    {
        var scenario = CreateProcedureBoundaryScenario(
            name,
            naturalRoll,
            difficulty,
            fixedZero,
            seedPlayerFate,
            expectedCategory,
            expectedBandIndex);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey,
            scenario.RouteId);

        Assert.Equal(expectedCategory,
            Convert.ToString(ReadRequiredProperty(flow.Resolution, "ResultCategory")));
        Assert.Equal(expectedBandIndex,
            Convert.ToInt32(ReadRequiredProperty(flow.Resolution, "SelectedOutcomeIndex")));
        AssertProcedureCheckAuthority(
            ReadRequiredProperty(flow.Request, "ModeAuthority"),
            scenario);
        AssertPreparedCriticalReaction(flow.Request, flow.AcceptedState, scenario);
        AssertProcedureResolutionEvidence(
            ReadRequiredProperty(flow.Resolution, "ModeEvidence"),
            ReadRequiredProperty(flow.Request, "ModeAuthority"),
            flow.Resolution,
            scenario);
    }

    [Fact]
    public void ProcedurePreparation_CheckedDifficultyOverflowRejectsWithoutReservationOrWrite()
    {
        var scenario = CreateProcedureBoundaryScenario(
            "effective_difficulty_overflow",
            naturalRoll: 1,
            difficulty: int.MaxValue,
            fixedZero: false,
            seedPlayerFate: true,
            expectedCategory: "failed_attempt",
            expectedBandIndex: 3);
        scenario.AcceptedState["sterileThreadCount"] = 1;
        const string safeRouteId = "procedure_t061_after_difficulty_overflow";
        var safeRoute = scenario.Before["treatment"]!["routes"]![0]!
            .DeepClone()
            .AsObject();
        safeRoute["routeId"] = safeRouteId;
        safeRoute["resolution"]!["difficulty"] = 10;
        scenario.Before["treatment"]!["routes"]!.AsArray().Add(safeRoute);
        scenario.Before["treatment"]!["knownRouteIds"]!.AsArray().Add(safeRouteId);
        scenario.Before["complications"] = new JsonArray(new JsonObject
        {
            ["complicationId"] = "overflow_pressure_01",
            ["kind"] = "pain",
            ["state"] = "active",
            ["displayName"] = "Overflow pressure",
            ["treatmentDifficultyModifier"] = 1,
            ["ownedEffectIds"] = new JsonArray(),
            ["visibility"] = "known_to_player"
        });
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = fixture.GetAcceptedState();
        var before = fixture.ReadCurrentWound();
        var history = fixture.ReadCurrentHistory();
        var treeBefore = CaptureResolverFixtureTree(fixture.Root);
        var planner = RequireOutcomeResolver();
        var result = Invoke(
            ExactStaticMethod(planner, "PrepareProcedureRequest", 6),
            new object?[]
            {
                acceptedState,
                history,
                before,
                scenario.OperationKey,
                scenario.RouteId,
                fixture.AcceptedEventRef(acceptedState)
            });

        AssertInvalidTypedResult(result, "Request", "checked procedure difficulty overflow");
        Assert.NotEmpty(AsObjects(ReadRequiredProperty(result, "Issues"))
            .Select(Assert.IsType<ValidationIssue>));
        AssertResolverFixtureTreeUnchanged(fixture.Root, treeBefore);

        var recovered = Invoke(
            ExactStaticMethod(planner, "PrepareProcedureRequest", 6),
            new object?[]
            {
                acceptedState,
                history,
                before,
                scenario.OperationKey + "_after_overflow",
                safeRouteId,
                fixture.AcceptedEventRef(acceptedState)
            });
        var recoveredRequest = ReadValidTypedResult(
            recovered,
            "Request",
            "procedure after checked-difficulty rejection");
        Assert.Equal(new[] { 0 }, ReadIntSequence(ReadRequiredProperty(
            ReadRequiredProperty(recoveredRequest, "ModeAuthority"),
            "SourceIndices")));
        Assert.Equal(
            "effect_fate_shield_older",
            ReadPreparedFateEffectId(recoveredRequest));
        AssertSingleHeldClaim(recoveredRequest, "sterile_thread");
    }

    [Fact]
    public void ProcedureReservations_CoalesceExactRetryAndAdvanceDiceAndFateWithinAcceptedInput()
    {
        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        scenario.AcceptedState["acceptedDice"] = new JsonArray(1, 1, 17);
        using var fixture = AcceptedStateFixture.Create(scenario);

        var first = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_first",
            scenario.RouteId);
        Assert.Equal(new[] { 0 }, ReadIntSequence(ReadRequiredProperty(
            ReadRequiredProperty(first.Request, "ModeAuthority"),
            "SourceIndices")));
        Assert.Equal("effect_fate_shield_older", Convert.ToString(ReadRequiredProperty(
            ReadRequiredProperty(
                ReadRequiredProperty(first.Request, "ModeAuthority"),
                "PreparedCriticalReaction"),
            "EffectId")));

        var firstRetry = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_first",
            scenario.RouteId);
        Assert.Equal(CanonicalValue(first.Request), CanonicalValue(firstRetry.Request));
        Assert.Equal(CanonicalValue(first.Resolution), CanonicalValue(firstRetry.Resolution));

        var second = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_second",
            scenario.RouteId);
        Assert.Equal(new[] { 1 }, ReadIntSequence(ReadRequiredProperty(
            ReadRequiredProperty(second.Request, "ModeAuthority"),
            "SourceIndices")));
        Assert.Equal("effect_fate_shield_newer", Convert.ToString(ReadRequiredProperty(
            ReadRequiredProperty(
                ReadRequiredProperty(second.Request, "ModeAuthority"),
                "PreparedCriticalReaction"),
            "EffectId")));
        Assert.NotEqual(
            ReadRequiredProperty(first.Request, "RequestFingerprint"),
            ReadRequiredProperty(second.Request, "RequestFingerprint"));

        var secondRetry = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_second",
            scenario.RouteId);
        Assert.Equal(CanonicalValue(second.Request), CanonicalValue(secondRetry.Request));
        Assert.Equal(CanonicalValue(second.Resolution), CanonicalValue(secondRetry.Resolution));
    }

    [Fact]
    public void ProcedureFailedAttempt_ConsumesExactlyItsDeclaredQuantityOnce()
    {
        var scenario = PrepareProcedurePublicationScenario(CreateScenario(
            "procedure_disadvantage_uses_two_contiguous_dice",
            "procedure"));
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = PersistAndRehydrateProcedurePublication(
            fixture,
            ResolveCurrentTreatment(
                fixture,
                "procedure",
                scenario.OperationKey + "_failed_consumption",
                scenario.RouteId),
            "failed-attempt declared consumption");
        Assert.Equal("failed_attempt", Convert.ToString(ReadRequiredProperty(
            flow.Resolution,
            "ResultCategory")));
        ComposeAndPublishCoordinatedProcedureTreatment(fixture, flow);
        Assert.Equal(1, fixture.ReadNpcItemCount("sterile_thread"));
        fixture.AssertItemIdentityIndexValid();
        Assert.Equal("active", fixture.ReadCurrentWound().Lifecycle);
        var historyRoot = JsonNode.Parse(File.ReadAllText(fixture.FileSystem.ResolvePath(
            WoundHistoryState.HistoryPath)))!.AsObject();
        var treatmentRow = Assert.Single(historyRoot["transitions"]!.AsArray(), row =>
            string.Equals(
                row!["kind"]!.GetValue<string>(),
                "treat",
                StringComparison.Ordinal));
        Assert.False(treatmentRow!["terminal"]!.GetValue<bool>());
        Assert.Equal("accepted_terminal",
            treatmentRow["transitionResult"]!["attemptDisposition"]!.GetValue<string>());
        Assert.DoesNotContain(historyRoot["transitions"]!.AsArray(), row =>
            string.Equals(
                row!["kind"]!.GetValue<string>(),
                "heal",
                StringComparison.Ordinal));

        fixture.RestartForReplay();
        var history = fixture.ReadCurrentHistory();
        var tree = CaptureResolverFixtureTree(fixture.Root);
        var replay = Invoke(
            ExactStaticMethod(RequireOutcomeResolver(), "CreateProcedureAttempt", 4),
            new object?[] { flow.Request, history, null, null });
        Assert.Equal("ExactReplay", Convert.ToString(ReadRequiredProperty(replay, "Disposition")));
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        Assert.Equal(1, fixture.ReadNpcItemCount("sterile_thread"));
        fixture.AssertItemIdentityIndexValid();
    }

    [Theory]
    [InlineData("procedure_normal_uses_lowest_free_die", "success", true)]
    [InlineData("procedure_disadvantage_uses_two_contiguous_dice", "none", false)]
    public void ProcedureFinalization_ConsumesOnlySelectedSupplyAndReleasesEveryOtherHeldClaim(
        string scenarioName,
        string expectedTrigger,
        bool consumesSupply)
    {
        var scenario = CreateScenario(scenarioName, "procedure");
        scenario.Before["severity"]!["value"] = "III";
        scenario.Before["severity"]!["rank"] = 3;
        scenario.Before["severity"]!["maximumAtCreation"] = "III";
        scenario.Before["consequences"]!["slotBudget"] = 3;
        scenario.AcceptedState["sterileThreadCount"] = consumesSupply ? 2 : 1;
        scenario.AcceptedState["reusableToolCount"] = 1;
        var route = scenario.Before["treatment"]!["routes"]![0]!.AsObject();
        route["outcomes"]![0]!["result"] = new JsonArray(new JsonObject
        {
            ["kind"] = "reduce_severity",
            ["steps"] = 1
        });
        route["requirements"]!.AsArray().Insert(1, new JsonObject
        {
            ["kind"] = "item_quantity",
            ["itemRef"] = "reusable_field_kit",
            ["quantity"] = 1,
            ["ownerRole"] = "provider"
        });
        route["resolution"]!["modifierSource"]!["requirementIndex"] = 2;
        if (!consumesSupply)
            route["resourcePolicy"]!["consumeOn"] = new JsonArray("success");
        scenario = PrepareProcedurePublicationScenario(
            scenario with { ExpectedIntentCount = 1 });

        using var fixture = AcceptedStateFixture.Create(scenario);
        var priorRootIds = fixture.ReadCurrentWound()
            .Consequences.OwnedEffectSources.RootBindings
            .Select(static binding => binding.EffectId)
            .ToArray();
        Assert.Equal(
            consumesSupply ? 2 : 1,
            fixture.ReadNpcItemCount("sterile_thread"));
        Assert.Equal(1, fixture.ReadNpcItemCount("reusable_field_kit"));
        var flow = PersistAndRehydrateProcedurePublication(
            fixture,
            ResolveCurrentTreatment(
                fixture,
                "procedure",
                scenario.OperationKey + "_resource_release",
                scenario.RouteId),
            "selected-supply finalization");
        Assert.Equal(expectedTrigger, Convert.ToString(ReadRequiredProperty(
            flow.Resolution,
            "ConsumptionTrigger")));
        Assert.Equal(
            consumesSupply ? "success" : "failed_attempt",
            Convert.ToString(ReadRequiredProperty(flow.Resolution, "ResultCategory")));
        AssertHeldRequirementClaims(
            flow.Request,
            "reusable_field_kit",
            "sterile_thread");

        ComposeAndPublishCoordinatedProcedureTreatment(fixture, flow);

        var publishedWound = fixture.ReadCurrentWound();
        var publishedRootIds = publishedWound.Consequences.OwnedEffectSources
            .RootBindings.Select(static binding => binding.EffectId).ToArray();
        if (consumesSupply)
        {
            Assert.Equal("II", publishedWound.Severity.Value);
            Assert.Equal(2, publishedWound.Severity.Rank);
            Assert.NotEmpty(publishedRootIds);
            Assert.Empty(priorRootIds.Intersect(
                publishedRootIds,
                StringComparer.Ordinal));
            Assert.Empty(priorRootIds
                .Select(MortalLocationIdentityState.BuildConfusableKey)
                .Intersect(
                    publishedRootIds.Select(
                        MortalLocationIdentityState.BuildConfusableKey),
                    StringComparer.Ordinal));
            Assert.Equal(
                Enumerable.Range(
                    1,
                    publishedWound.Consequences.Entries.Count),
                publishedWound.Consequences.Entries.Select(
                    static entry => entry.Slot));
        }
        else
        {
            Assert.Equal("III", publishedWound.Severity.Value);
            Assert.Equal(3, publishedWound.Severity.Rank);
        }
        Assert.Single(
            fixture.ReadCurrentHistory().State!.Transitions,
            static row => string.Equals(
                row.Kind,
                "treat",
                StringComparison.Ordinal));
        Assert.Equal(1, fixture.ReadNpcItemCount("sterile_thread"));
        Assert.Equal(1, fixture.ReadNpcItemCount("reusable_field_kit"));
        fixture.AssertItemIdentityIndexValid();

        fixture.PrepareNextTurn(
            43,
            1_260,
            consumesSupply ? "reusable_tool_after_consumption" : "release_only_retry",
            consumesSupply ? new[] { 17 } : new[] { 4, 19 });
        var fresh = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_fresh_resource_reclaim",
            scenario.RouteId);
        AssertHeldRequirementClaims(
            fresh.Request,
            "reusable_field_kit",
            "sterile_thread");
        Assert.NotEqual(
            Convert.ToString(ReadRequiredProperty(
                ReadRequiredProperty(flow.Request, "ResourceAuthority"),
                "ReservationId")),
            Convert.ToString(ReadRequiredProperty(
                ReadRequiredProperty(fresh.Request, "ResourceAuthority"),
                "ReservationId")));
    }

    [Fact]
    public void ProcedureFateReaction_ConsumesOldestAtomicallyThenExposesNextShield()
    {
        var scenario = PrepareProcedurePublicationScenario(CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure"));
        using var fixture = AcceptedStateFixture.Create(scenario);
        var first = PersistAndRehydrateProcedurePublication(
            fixture,
            ResolveCurrentTreatment(
                fixture,
                "procedure",
                scenario.OperationKey + "_publish_first_fate",
                scenario.RouteId),
            "first Fate reaction");
        ComposeAndPublishCoordinatedProcedureTreatment(fixture, first);
        Assert.Equal(
            new[] { "effect_fate_shield_newer" },
            fixture.ReadActivePlayerEffectIds().Where(static effectId =>
                effectId.StartsWith("effect_fate_shield_", StringComparison.Ordinal)));
        Assert.Equal(1, fixture.ReadNpcItemCount("sterile_thread"));

        fixture.PrepareNextTurn(
            43,
            1_261,
            "publish_second_fate",
            new[] { 1, 17 });
        var second = PersistAndRehydrateProcedurePublication(
            fixture,
            ResolveCurrentTreatment(
                fixture,
                "procedure",
                scenario.OperationKey + "_publish_second_fate",
                scenario.RouteId),
            "second Fate reaction");
        Assert.Equal("effect_fate_shield_newer", Convert.ToString(ReadRequiredProperty(
            ReadRequiredProperty(
                ReadRequiredProperty(second.Request, "ModeAuthority"),
                "PreparedCriticalReaction"),
            "EffectId")));
        ComposeAndPublishCoordinatedProcedureTreatment(fixture, second);
        Assert.DoesNotContain(
            fixture.ReadActivePlayerEffectIds(),
            static effectId => effectId.StartsWith(
                "effect_fate_shield_",
                StringComparison.Ordinal));
        Assert.Equal(0, fixture.ReadNpcItemCount("sterile_thread"));
        fixture.AssertItemIdentityIndexValid();
    }

    [Fact]
    public void ProcedureFateReaction_TypedAndLegacyReportDuplicateRejectsBeforePublication()
    {
        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        scenario.AcceptedState["sterileThreadCount"] = 1;
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_cross_surface_duplicate",
            scenario.RouteId);
        var report = new JsonObject
        {
            ["eventType"] = "owner_critical_failure",
            ["target"] = new JsonObject
            {
                ["kind"] = "player",
                ["targetId"] = "player_current"
            },
            ["evidence"] = new JsonObject
            {
                ["kind"] = "mortal_action_roll",
                ["rollMode"] = "normal",
                ["diceIndexes"] = new JsonArray(0),
                ["selectedIndex"] = 0,
                ["selectedValue"] = 1,
                ["originalOutcome"] = "critical_failure",
                ["resolvedOutcome"] = "failure"
            },
            ["reason"] = "Legacy Fate report must not duplicate typed treatment."
        };
        using var reportDocument = JsonDocument.Parse(report.ToJsonString());
        var proposal = new GameResponse
        {
            EffectEventReports = new[] { reportDocument.RootElement.Clone() }
        };
        var tree = CaptureResolverFixtureTree(fixture.Root);
        var planner = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.WoundAcceptedTurnPlanner",
            false,
            false);
        Assert.NotNull(planner);
        var result = Invoke(
            ExactStaticMethod(planner!, "ComposeMortalWoundTreatmentPublication", 6),
            new object?[]
            {
                fixture.FileSystem,
                fixture.Lease,
                proposal,
                flow.AcceptedState,
                flow.Request,
                flow.Resolution
            });

        AssertInvalidTypedResult(result, "Plan", "typed/legacy Fate duplicate");
        Assert.Contains(
            AsObjects(ReadRequiredProperty(result, "Issues")).Select(Assert.IsType<ValidationIssue>),
            static issue => string.Equals(
                issue.Code,
                "wound_treatment_fate_reaction_cross_surface_duplicate",
                StringComparison.Ordinal));
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        Assert.Equal(
            new[] { "effect_fate_shield_older", "effect_fate_shield_newer" },
            fixture.ReadActivePlayerEffectIds());
        Assert.Equal(1, fixture.ReadNpcItemCount("sterile_thread"));

        var coordinates = ReadRequiredProperty(flow.Request, "Coordinates");
        var probe = InvokeInstance(
            ExactInstanceMethod(typeof(WoundHistoryParseResult), "ProbeTreatmentAttempt", 3),
            fixture.ReadCurrentHistory(),
            new object?[]
            {
                ReadRequiredProperty(coordinates, "OperationKey"),
                ReadRequiredProperty(coordinates, "AttemptId"),
                ReadRequiredProperty(flow.Request, "RequestFingerprint")
            });
        Assert.Equal("NotFound", Convert.ToString(ReadRequiredProperty(probe, "Status")));
        var replacement = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_after_rejected_duplicate",
            scenario.RouteId);
        Assert.Equal(new[] { 0 }, ReadIntSequence(ReadRequiredProperty(
            ReadRequiredProperty(replacement.Request, "ModeAuthority"),
            "SourceIndices")));
        Assert.Equal("effect_fate_shield_older", Convert.ToString(ReadRequiredProperty(
            ReadRequiredProperty(
                ReadRequiredProperty(replacement.Request, "ModeAuthority"),
                "PreparedCriticalReaction"),
            "EffectId")));
    }

    [Theory]
    [MemberData(nameof(CourseRows))]
    public void PrepareCourseMilestoneRequest_ResolvesOnlyThroughLeaseBoundAcceptedState(
        string scenario) =>
        ExecutePreparedFlow(CreateScenario(scenario, "course"));

    [Theory]
    [MemberData(nameof(GuaranteedRows))]
    public void PrepareGuaranteedRequest_ResolvesOnlyThroughLeaseBoundAcceptedState(
        string scenario) =>
        ExecutePreparedFlow(CreateScenario(scenario, "guaranteed"));

    [Theory]
    [MemberData(nameof(CombatTreatmentRows))]
    public void CombatTarget_ProcedureAndCourseResolveAndPublishAgainstTheExactAcceptedCarrier(
        string targetKind,
        string targetId,
        string mode)
    {
        var scenario = CreateCombatTreatmentScenario(targetKind, targetId, mode);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var targetCarrierPath = fixture.TargetCarrierPath;
        var targetCarrierBefore = File.ReadAllBytes(
            fixture.FileSystem.ResolvePath(targetCarrierPath));
        var playerCarrierBefore = File.ReadAllBytes(
            fixture.FileSystem.ResolvePath(WoundCarrierCatalog.PlayerPath));

        var flow = ResolveCurrentTreatment(
            fixture,
            mode,
            scenario.OperationKey + "_" + targetKind,
            scenario.RouteId);
        var coordinates = ReadRequiredProperty(flow.Request, "Coordinates");
        Assert.Equal(targetKind, Convert.ToString(ReadRequiredProperty(
            coordinates,
            "TargetKind")));
        Assert.Equal(targetId, Convert.ToString(ReadRequiredProperty(
            coordinates,
            "TargetId")));
        Assert.Equal("npc", Convert.ToString(ReadRequiredProperty(
            coordinates,
            "ProviderKind")));
        Assert.Equal("field_medic_01", Convert.ToString(ReadRequiredProperty(
            coordinates,
            "ProviderId")));
        fixture.AssertCurrentWoundCoordinate(targetKind, targetId, targetCarrierPath);

        ComposeAndPublishTreatment(fixture, flow);

        Assert.False(targetCarrierBefore.SequenceEqual(File.ReadAllBytes(
            fixture.FileSystem.ResolvePath(targetCarrierPath))));
        Assert.Equal(
            playerCarrierBefore,
            File.ReadAllBytes(fixture.FileSystem.ResolvePath(
                WoundCarrierCatalog.PlayerPath)));
        var after = fixture.AssertCurrentWoundCoordinate(
            targetKind,
            targetId,
            targetCarrierPath);
        if (string.Equals(mode, "course", StringComparison.Ordinal))
            Assert.False(string.IsNullOrWhiteSpace(after.Care.ActiveCourseId));
        else
            Assert.True(after.Severity.Rank < flow.Before.Severity.Rank);

        var history = JsonNode.Parse(File.ReadAllText(fixture.FileSystem.ResolvePath(
            WoundHistoryState.HistoryPath)))!.AsObject();
        Assert.Single(history["transitions"]!.AsArray(), row => string.Equals(
            row!["kind"]!.GetValue<string>(),
            "treat",
            StringComparison.Ordinal));

        fixture.RestartForReplay();
        fixture.AssertCurrentWoundCoordinate(targetKind, targetId, targetCarrierPath);
        Assert.True(fixture.ReadCurrentHistory().IsValid);
    }

    private static IEnumerable<object[]> Rows(params string[] values) =>
        values.Select(static value => new object[] { value });

    public static IEnumerable<object[]> SemanticRows =>
        ProcedureRows.Select(row => new object[] { (string)row[0], "procedure" })
            .Concat(CourseRows.Select(row => new object[] { (string)row[0], "course" }))
            .Concat(GuaranteedRows.Select(row => new object[] { (string)row[0], "guaranteed" }));

    private static ResolverScenario CreateCombatTreatmentScenario(
        string targetKind,
        string targetId,
        string mode)
    {
        var scenarioName = string.Equals(mode, "procedure", StringComparison.Ordinal)
            ? "procedure_normal_uses_lowest_free_die"
            : "course_first_milestone_is_ready_at_inclusive_due_time";
        var scenario = CreateScenario(scenarioName, mode);
        var carrierPath = AcceptedStateFixture.ResolveTargetCarrierPath(targetKind);
        var before = WoundContractTestData.CreateActiveWound(
            woundId: scenario.Before["woundId"]!.GetValue<string>(),
            ownerKind: targetKind,
            ownerId: targetId,
            carrierPath: carrierPath);
        var route = scenario.Before["treatment"]!["routes"]![0]!
            .DeepClone()
            .AsObject();
        if (string.Equals(mode, "procedure", StringComparison.Ordinal))
        {
            route["requirements"] = new JsonArray(new JsonObject
            {
                ["kind"] = "skill_tier",
                ["capabilityRef"] = "field_medicine",
                ["minimumTier"] = 2,
                ["actorRole"] = "provider"
            });
            route["resourcePolicy"] = Policy(new JsonArray(), new JsonArray());
            route["resolution"]!["modifierSource"] = new JsonObject
            {
                ["kind"] = "resolved_skill_tier",
                ["requirementIndex"] = 0
            };
        }
        else
        {
            route["resourcePolicy"] = Policy(new JsonArray(), new JsonArray());
            foreach (var milestone in route["outcomes"]!.AsArray())
                milestone!["requirements"] = new JsonArray();
        }
        before["treatment"]!["routes"] = new JsonArray(route);
        before["treatment"]!["knownRouteIds"] = new JsonArray(
            route["routeId"]!.DeepClone());
        var acceptedState = scenario.AcceptedState.DeepClone().AsObject();
        acceptedState["targetKind"] = targetKind;
        acceptedState["targetId"] = targetId;
        acceptedState["skillRows"] = new JsonArray("skill_field_medicine_npc_01");
        return scenario with
        {
            AcceptedState = acceptedState,
            Before = before,
            OperationKey = scenario.OperationKey + "_combat_target"
        };
    }

    private static ResolverScenario CreateNpcTreatmentScenario()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        var before = WoundContractTestData.CreateActiveWound(
            woundId: scenario.Before["woundId"]!.GetValue<string>(),
            ownerKind: "npc",
            ownerId: "field_medic_01",
            carrierPath: WoundCarrierCatalog.NpcPath);
        before["treatment"] = scenario.Before["treatment"]!.DeepClone();
        var accepted = scenario.AcceptedState.DeepClone().AsObject();
        accepted["targetKind"] = "npc";
        accepted["targetId"] = "field_medic_01";
        return scenario with
        {
            AcceptedState = accepted,
            Before = before,
            OperationKey = scenario.OperationKey + "_npc_target"
        };
    }

    private static ResolverScenario CreateScenario(string name, string mode)
    {
        var before = WoundContractTestData.CreateActiveWound();
        // The retained treatment routes may lower a severity-II wound to severity I.
        // Keep the fixture's complete consequence graph within that selected
        // after-state's one-slot physical envelope so the production all-band
        // applicability check proves a genuinely canonical transition.
        before["consequences"]!["ownedEffectSources"]!["definitions"]!
            .AsArray().RemoveAt(1);
        before["consequences"]!["ownedEffectSources"]!["rootBindings"]!
            .AsArray().RemoveAt(1);
        before["consequences"]!["entries"]!.AsArray().RemoveAt(1);
        before["consequences"]!["slotsUsed"] = 1;
        var route = before["treatment"]!["routes"]![0]!.DeepClone().AsObject();
        ConfigureModeRoute(route, mode, name);
        before["treatment"]!["routes"] = new JsonArray(route);
        before["treatment"]!["knownRouteIds"] = new JsonArray(route["routeId"]!.DeepClone());
        var acceptedState = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["sessionId"] = "session_t061",
            ["requestId"] = "request_t061",
            ["snapshotToken"] = "snapshot_t061",
            ["turn"] = 42,
            ["providerKind"] = "npc",
            ["providerId"] = "field_medic_01",
            ["targetKind"] = "player",
            ["targetId"] = "player_current",
            ["locationId"] = "loc_field_clinic_001",
            ["worldMinute"] = 1_260L,
            ["woundId"] = "wound_test_torn_side",
            // The only event exposed by the production empty-effect input is the
            // accepted-turn event composed by EffectAcceptedTurnInputComposer.
            // Scenario identity belongs to the operation/route and player action;
            // tests must not invent a parallel accepted-event namespace.
            ["eventRef"] = "turn_42:accepted_effect",
            ["acceptedDice"] = new JsonArray(4, 17, 1, 20),
            ["skillRows"] = new JsonArray("skill_field_medicine_01"),
            ["canonicalReads"] = new JsonArray(
                "wound_carriers",
                "wound_identity_index",
                "wound_history",
                "world_time",
                "turn_request",
                "effect_carriers",
                "effect_identity_index",
                "skills")
        };

        // Each row has a distinct production input coordinate.  These are fixture
        // sources only; the planner must read/validate their canonical equivalents
        // itself and must not accept this object as authority.
        acceptedState["operationLabel"] = name;
        acceptedState["requestedMode"] = mode;
        var semantic = name switch
        {
            "procedure_normal_uses_lowest_free_die" => new ScenarioSemantics("normal", new[] { 0 }, 0, 17, 1_260, true, "Resolved", "success", 2, null),
            "procedure_advantage_uses_two_contiguous_dice" => new ScenarioSemantics("advantage", new[] { 0, 1 }, 1, 19, 1_260, true, "Resolved", "success", 2, null),
            "procedure_disadvantage_uses_two_contiguous_dice" => new ScenarioSemantics("disadvantage", new[] { 0, 1 }, 0, 4, 1_260, true, "Resolved", "failed_attempt", 1, null),
            "procedure_player_natural_one_reserves_oldest_fate_shield" => new ScenarioSemantics("normal", new[] { 0 }, 0, 1, 1_260, true, "Resolved", "failed_attempt", 1, "effect_fate_shield_older"),
            "course_first_milestone_is_ready_at_inclusive_due_time" => new ScenarioSemantics("normal", new[] { 0 }, 0, 17, 0, true, "Resolved", "success", 0, null),
            "course_unsatisfied_dose_requirement_has_no_reservation" => new ScenarioSemantics("normal", new[] { 0 }, 0, 17, 0, false, "PreparationRejected", null, 0, null),
            "guaranteed_current_capability_proof_stabilizes" => new ScenarioSemantics("normal", new[] { 0 }, 0, 17, 1_260, true, "Resolved", "success", 1, null),
            "guaranteed_severity_one_heal_has_empty_legacy_array" => new ScenarioSemantics("normal", new[] { 0 }, 0, 17, 1_260, true, "Resolved", "success", 1, null),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Retained scenarios must have concrete semantics.")
        };
        if (name == "procedure_player_natural_one_reserves_oldest_fate_shield")
        {
            route["outcomes"] = new JsonArray(
                Band("success", 5, null, "success", new JsonArray(
                    new JsonObject { ["kind"] = "stabilize" },
                    new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 })),
                Band("partial", 0, 4, "partial_success", new JsonArray(
                    new JsonObject { ["kind"] = "stabilize" },
                    new JsonObject { ["kind"] = "add_recovery", ["points"] = 1 })),
                Band("failed", -4, -1, "failed_attempt", new JsonArray(
                    new JsonObject { ["kind"] = "no_improvement" })),
                Band("critical", null, -5, "failed_attempt", new JsonArray(
                    new JsonObject { ["kind"] = "no_improvement" })));
        }
        acceptedState["rollMode"] = semantic.RollMode;
        acceptedState["acceptedDice"] = semantic.RollMode switch
        {
            "normal" when name.Contains("natural_one", StringComparison.Ordinal) => new JsonArray(1, 17),
            "normal" => new JsonArray(17, 4, 1, 20),
            _ => new JsonArray(4, 19, 7)
        };
        acceptedState["worldMinute"] = semantic.WorldMinute;
        acceptedState["requirementsAvailable"] = semantic.RequirementsAvailable;
        if (name == "guaranteed_severity_one_heal_has_empty_legacy_array")
        {
            before["severity"]!["value"] = "I";
            before["severity"]!["rank"] = 1;
            before["severity"]!["maximumAtCreation"] = "I";
            before["consequences"]!["slotBudget"] = 1;
            before["consequences"]!["slotsUsed"] = 1;
            route["outcomes"]![0]!["result"] = new JsonArray(new JsonObject
            {
                ["kind"] = "heal",
                ["legacies"] = new JsonArray()
            });
        }

        return new ResolverScenario(
            name,
            mode,
            acceptedState,
            CreateHistoryFor(name, mode),
            before,
            "operation_t061_" + name,
            route["routeId"]!.GetValue<string>(),
            "turn_42:accepted_effect",
            semantic.ExpectedBoundary,
            semantic.ExpectedCategory,
            semantic.RollMode,
            semantic.SourceIndices,
            semantic.SelectedSourceIndex,
            semantic.NaturalRoll,
            semantic.WorldMinute,
            semantic.RequirementsAvailable,
            semantic.ExpectedIntentCount,
            semantic.ExpectedFateEffectId,
            semantic.ExpectedFateEffectId);
    }

    private static ResolverScenario CreateProcedureBoundaryScenario(
        string name,
        int naturalRoll,
        int difficulty,
        bool fixedZero,
        bool seedPlayerFate,
        string expectedCategory,
        int expectedBandIndex)
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        var route = scenario.Before["treatment"]!["routes"]![0]!.AsObject();
        route["resolution"]!["difficulty"] = difficulty;
        if (fixedZero)
            route["resolution"]!["modifierSource"] = new JsonObject { ["kind"] = "fixed_zero" };
        if (naturalRoll == 1)
        {
            route["outcomes"] = new JsonArray(
                Band("success", 5, null, "success", new JsonArray(
                    new JsonObject { ["kind"] = "stabilize" },
                    new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 })),
                Band("partial", 0, 4, "partial_success", new JsonArray(
                    new JsonObject { ["kind"] = "stabilize" },
                    new JsonObject { ["kind"] = "add_recovery", ["points"] = 1 })),
                Band("failed", -4, -1, "failed_attempt", new JsonArray(
                    new JsonObject { ["kind"] = "no_improvement" })),
                Band("critical", null, -5, "failed_attempt", new JsonArray(
                    new JsonObject { ["kind"] = "no_improvement" })));
        }
        scenario.AcceptedState["acceptedDice"] = new JsonArray(naturalRoll, 7, 13);
        scenario.AcceptedState["operationLabel"] = name;
        var expectedIntentCount = route["outcomes"]![expectedBandIndex]!["result"]!.AsArray().Count;
        return scenario with
        {
            Name = name,
            OperationKey = "operation_t061_" + name,
            ExpectedCategory = expectedCategory,
            ExpectedSourceIndices = new[] { 0 },
            ExpectedSelectedSourceIndex = 0,
            ExpectedNaturalRoll = naturalRoll,
            ExpectedIntentCount = expectedIntentCount,
            ExpectedFateEffectId = null,
            SeedFateEffectId = seedPlayerFate ? "effect_fate_shield_older" : null
        };
    }

    private static void ConfigureModeRoute(JsonObject route, string mode, string name)
    {
        var strict = mode switch
        {
            "procedure" => StrictProcedureRoute(),
            "course" => StrictCourseRoute(),
            "guaranteed" => StrictGuaranteedRoute(),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
        route.Clear();
        foreach (var pair in strict)
            route[pair.Key] = pair.Value?.DeepClone();
        route["routeId"] = mode + "_t061_" + name;
    }

    private static JsonObject StrictProcedureRoute() => new()
    {
        ["routeId"] = "procedure_v1", ["displayName"] = "Procedure", ["visibility"] = "known_to_player", ["mode"] = "procedure",
        ["requirements"] = new JsonArray(
            new JsonObject { ["kind"] = "item_quantity", ["itemRef"] = "sterile_thread", ["quantity"] = 1, ["ownerRole"] = "provider" },
            // The procedure is performed on the player target.  The provider still
            // supplies the sterile thread, while the target's accepted skill is the
            // only legal source of the player-owned d20/Fate reaction below.
            new JsonObject { ["kind"] = "skill_tier", ["capabilityRef"] = "field_medicine", ["minimumTier"] = 2, ["actorRole"] = "target" }),
        ["resourcePolicy"] = Policy(new JsonArray("success", "partial_success", "failed_attempt"), new JsonArray(new JsonObject { ["kind"] = "consume_requirement", ["scope"] = "common", ["milestoneOrdinal"] = null, ["requirementIndex"] = 0 })),
        ["resolution"] = new JsonObject { ["formulaKey"] = "mortal_wound_procedure_v1", ["difficulty"] = 15, ["rollSource"] = "accepted_d20", ["criticalPolicy"] = "natural_20_first_natural_1_last", ["modifierSource"] = new JsonObject { ["kind"] = "resolved_skill_tier", ["requirementIndex"] = 1 } },
        ["outcomes"] = new JsonArray(
            Band("success", 5, null, "success", new JsonArray(new JsonObject { ["kind"] = "stabilize" }, new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 })),
            Band("partial", 0, 4, "partial_success", new JsonArray(new JsonObject { ["kind"] = "stabilize" }, new JsonObject { ["kind"] = "add_recovery", ["points"] = 1 })),
            Band("failed", null, -1, "failed_attempt", new JsonArray(new JsonObject { ["kind"] = "no_improvement" }))),
        ["interruption"] = null
    };

    private static JsonObject StrictCourseRoute() => new()
    {
        ["routeId"] = "course_v1", ["displayName"] = "Course", ["visibility"] = "known_to_player", ["mode"] = "course",
        ["requirements"] = new JsonArray(new JsonObject { ["kind"] = "provider", ["providerRef"] = "field_medic_01" }),
        ["resourcePolicy"] = Policy(new JsonArray("success"), new JsonArray(CourseMutation(1), CourseMutation(2), CourseMutation(3))),
        ["resolution"] = new JsonObject { ["clockKind"] = "world_time.currentTimeInMinutes", ["maximumGapMinutes"] = 600 },
        ["outcomes"] = new JsonArray(
            CourseMilestone(1, 0, "active", new JsonArray()),
            CourseMilestone(2, 480, "active", new JsonArray(new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 })),
            CourseMilestone(3, 960, "completed", new JsonArray(new JsonObject { ["kind"] = "heal", ["legacies"] = new JsonArray() }))),
        ["interruption"] = new JsonObject { ["category"] = "failed_attempt", ["result"] = new JsonArray(new JsonObject { ["kind"] = "no_improvement" }) }
    };

    private static JsonObject StrictGuaranteedRoute() => new()
    {
        ["routeId"] = "guaranteed_v1", ["displayName"] = "Guaranteed", ["visibility"] = "known_to_player", ["mode"] = "guaranteed",
        ["requirements"] = new JsonArray(new JsonObject { ["kind"] = "source_capability", ["capabilityRef"] = "exact_materialized_healing_source", ["actorRole"] = "provider" }),
        ["resourcePolicy"] = Policy(new JsonArray("success"), new JsonArray()),
        ["resolution"] = new JsonObject { ["capabilityRef"] = "exact_materialized_healing_source", ["actorRole"] = "provider" },
        ["outcomes"] = new JsonArray(new JsonObject { ["category"] = "success", ["result"] = new JsonArray(new JsonObject { ["kind"] = "stabilize" }) }), ["interruption"] = null
    };

    private static JsonObject Policy(JsonArray consumeOn, JsonArray mutations) => new() { ["reserveBeforeResolution"] = true, ["consumeOn"] = consumeOn, ["refundOn"] = new JsonArray("cancelled", "validation_failed", "rolled_back"), ["mutations"] = mutations };
    private static JsonObject Band(string id, int? minimum, int? maximum, string category, JsonArray result) => new() { ["bandId"] = id, ["minimumMargin"] = minimum, ["maximumMargin"] = maximum, ["category"] = category, ["result"] = result };
    private static JsonObject CourseMutation(int ordinal) => new() { ["kind"] = "consume_requirement", ["scope"] = "course_milestone", ["milestoneOrdinal"] = ordinal, ["requirementIndex"] = 0 };
    private static JsonObject CourseMilestone(int ordinal, long afterMinutes, string completion, JsonArray result) => new() { ["ordinal"] = ordinal, ["afterMinutes"] = afterMinutes, ["requirements"] = new JsonArray(new JsonObject { ["kind"] = "item_quantity", ["itemRef"] = "antibiotic_dose", ["quantity"] = 1, ["ownerRole"] = "target" }), ["category"] = "success", ["completion"] = completion, ["result"] = result };
    private static JsonObject CreateCanonicalStack(string itemId, int count)
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot(itemId);
        item["count"] = count;
        item["quality"] = "Rare";
        item["rarity"] = "Rare";
        MortalItemTestFixture.ResealCanonical(item);
        return item;
    }

    private static JsonObject PlayerInventoryRoot(JsonObject? dose) => new()
    {
        ["items"] = dose == null ? new JsonArray() : new JsonArray(dose)
    };

    private static JsonObject CreateItemIdentityIndex(
        JsonObject sterileThread,
        JsonObject? reusableTool,
        JsonObject? dose)
    {
        var carriers = new List<(
            JsonObject Item,
            string Kind,
            string OwnerId,
            string? ContainerId)>
        {
            (sterileThread, "npc_inventory", "field_medic_01", null)
        };
        if (reusableTool is not null)
            carriers.Add((reusableTool, "npc_inventory", "field_medic_01", null));
        if (dose is not null)
            carriers.Add((dose, "player_inventory", "player", null));
        return MortalItemTestFixture.CreateIndexForCarriers(carriers.ToArray());
    }

    private static void WriteCanonicalResourceAuthority(
        FileSystemManager fileSystem,
        string? treatmentTargetKind = null,
        string? treatmentTargetId = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(fileSystem.ResolvePath(
            ResourceMaterializationContract.DefinitionsPath))!);
        var bootstrap = ResourceBootstrapStateBuilder.BuildMortalPlayer(
            incarnationNumber: 1,
            turn: 1,
            permanentStrength: 10,
            permanentConstitution: 10,
            permanentIntelligence: 10,
            permanentWisdom: 10,
            permanentFaith: 10);
        Assert.True(bootstrap.IsValid, DescribeIssues(bootstrap.Issues));
        var definitions = Assert.IsType<ResourceDefinitionCatalog>(bootstrap.Definitions);
        var state = Assert.IsType<ResourceStateLedger>(bootstrap.State);
        var history = Assert.IsType<ResourceHistoryState>(bootstrap.History);
        if (treatmentTargetKind is "combatant" or "combatant_member")
        {
            Assert.False(string.IsNullOrWhiteSpace(treatmentTargetId));
            (state, history) = AddCanonicalCombatTargetHealth(
                definitions,
                state,
                history,
                treatmentTargetKind,
                treatmentTargetId!);
        }
        File.WriteAllText(
            fileSystem.ResolvePath(ResourceMaterializationContract.DefinitionsPath),
            definitions.ToCanonicalJson());
        File.WriteAllText(
            fileSystem.ResolvePath(ResourceMaterializationContract.StatePath),
            state.ToCanonicalJson());
        File.WriteAllText(
            fileSystem.ResolvePath(ResourceMaterializationContract.HistoryPath),
            history.ToCanonicalJson());

        var composed = CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
                definitions,
                path => Task.FromResult<string?>(File.Exists(fileSystem.ResolvePath(path))
                    ? File.ReadAllText(fileSystem.ResolvePath(path))
                    : null),
                state,
                history,
                CanonicalResourceOwnerAuthorityPurpose.ExplicitBootstrap)
            .GetAwaiter()
            .GetResult();
        Assert.True(composed.IsValid, DescribeIssues(composed.Issues));
        Assert.False(string.IsNullOrWhiteSpace(composed.CanonicalAuthorityJson));
        File.WriteAllText(
            fileSystem.ResolvePath(CanonicalResourceOwnerAuthorityComposer.AuthorityPath),
            composed.CanonicalAuthorityJson);
    }

    private static (ResourceStateLedger State, ResourceHistoryState History)
        AddCanonicalCombatTargetHealth(
            ResourceDefinitionCatalog definitions,
            ResourceStateLedger state,
            ResourceHistoryState history,
            string targetKind,
            string targetId)
    {
        const string authorityFingerprint =
            "sha256:cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
        const string policyFingerprint =
            "sha256:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
        var ownerKind = targetKind == "combatant_member"
            ? ResourceOwnerKind.CombatGroupMember
            : ResourceOwnerKind.Combatant;
        Assert.True(definitions.TryResolveExact("health", out var definition));
        var coordinate = new ResourceCoordinate(
            "mortal_world",
            ownerKind,
            targetId,
            "health");
        var binding = new ResourceCapacityBinding(
            definition!.CapacityPolicy.Kind,
            definition.CapacityPolicy.FormulaKey!,
            authorityFingerprint);
        var snapshot = new ResourceStateSnapshot(
            100,
            100,
            binding,
            ResourceLifecycleState.Active);
        var suffix = targetKind + "_" + targetId;
        var executionSequence = history.Transitions
            .Where(static row => row.Turn == 1)
            .Select(static row => row.ExecutionSequence)
            .DefaultIfEmpty(-1)
            .Max() + 1;
        var transition = new ResourceTransition(
            "transition_t070_health_initialize_" + suffix,
            "operation_t070_health_initialize_" + suffix,
            "turn_1:t070_resource_bootstrap:" + suffix,
            "owner_materialization",
            "t070_" + suffix,
            ResourceMutationPhase.RegisteredSystemOutcome,
            40,
            executionSequence,
            coordinate,
            ResourceTransitionOperation.Initialize,
            0,
            0,
            ResourceTransitionOutcome.Applied,
            ResourceCapacityDisposition.InitializeFromDefinition,
            null,
            snapshot,
            new ResourceSourceEvidence(
                "owner_materialization",
                "t070_" + suffix,
                authorityFingerprint),
            policyFingerprint,
            null,
            1);
        var historyResult = ResourceHistoryState.CreateValidated(
            history.Transitions.Append(transition),
            definitions);
        Assert.True(historyResult.IsValid, DescribeIssues(historyResult.Issues));
        var updatedState = new ResourceStateLedger(
            state.Entries.Append(new ResourceStateEntry(
                coordinate,
                snapshot.Current,
                snapshot.Maximum,
                snapshot.CapacityBinding,
                snapshot.State,
                new ResourceChronology(
                    1,
                    transition.EventRef,
                    transition.TransitionId,
                    transition.EventRef,
                    1))));
        Assert.Empty(historyResult.History!.ValidateStateAgreement(updatedState));
        return (updatedState, historyResult.History);
    }

    private static void WriteCanonicalPlayerHealthAuthority(
        FileSystemManager fileSystem,
        int current)
    {
        const string fingerprintA =
            "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string fingerprintB =
            "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        Assert.True(definitions.TryResolveExact("health", out var definition));
        var coordinate = new ResourceCoordinate(
            "mortal_world",
            ResourceOwnerKind.Player,
            "player_current",
            "health");
        var binding = new ResourceCapacityBinding(
            definition!.CapacityPolicy.Kind,
            definition.CapacityPolicy.FormulaKey!,
            fingerprintA);
        var initialized = new ResourceStateSnapshot(
            10,
            10,
            binding,
            ResourceLifecycleState.Active);
        var initialize = new ResourceTransition(
            "transition_health_initialize",
            "operation_health_initialize",
            "turn_1:resource:1",
            "bootstrap_materialization",
            "mortal_incarnation_1",
            ResourceMutationPhase.RegisteredSystemOutcome,
            40,
            0,
            coordinate,
            ResourceTransitionOperation.Initialize,
            0,
            0,
            ResourceTransitionOutcome.Applied,
            ResourceCapacityDisposition.InitializeFromDefinition,
            null,
            initialized,
            new ResourceSourceEvidence("bootstrap_materialization", "mortal_incarnation_1", fingerprintA),
            fingerprintA,
            null,
            1);
        var transitions = new List<ResourceTransition> { initialize };
        ResourceTransition? latest = null;
        if (current != 10)
        {
            latest = new ResourceTransition(
                "transition_health_damage",
                "operation_health_damage",
                "turn_2:resource:1",
                "combat_outcome",
                "wound_fixture_damage",
                ResourceMutationPhase.DirectOutcome,
                100,
                0,
                coordinate,
                ResourceTransitionOperation.Damage,
                10 - current,
                10 - current,
                ResourceTransitionOutcome.Applied,
                null,
                initialized,
                initialized with { Current = current },
                new ResourceSourceEvidence("combat_outcome", "wound_fixture_damage", fingerprintB),
                fingerprintB,
                null,
                2);
            transitions.Add(latest);
        }
        var historyResult = ResourceHistoryState.CreateValidated(transitions, definitions);
        Assert.True(historyResult.IsValid, DescribeIssues(historyResult.Issues));
        var state = new ResourceStateLedger(new[]
        {
            new ResourceStateEntry(
                coordinate,
                current,
                10,
                binding,
                ResourceLifecycleState.Active,
                new ResourceChronology(
                    1,
                    initialize.EventRef,
                    (latest ?? initialize).TransitionId,
                    (latest ?? initialize).EventRef,
                    (latest ?? initialize).Turn))
        });
        Assert.Empty(historyResult.History!.ValidateStateAgreement(state));
        File.WriteAllText(
            fileSystem.ResolvePath(ResourceMaterializationContract.DefinitionsPath),
            definitions.ToCanonicalJson());
        File.WriteAllText(
            fileSystem.ResolvePath(ResourceMaterializationContract.StatePath),
            state.ToCanonicalJson());
        File.WriteAllText(
            fileSystem.ResolvePath(ResourceMaterializationContract.HistoryPath),
            historyResult.History.ToCanonicalJson());
        var composed = CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
                definitions,
                path => Task.FromResult<string?>(File.Exists(fileSystem.ResolvePath(path))
                    ? File.ReadAllText(fileSystem.ResolvePath(path))
                    : null),
                state,
                historyResult.History,
                CanonicalResourceOwnerAuthorityPurpose.ExplicitBootstrap)
            .GetAwaiter()
            .GetResult();
        Assert.True(composed.IsValid, DescribeIssues(composed.Issues));
        File.WriteAllText(
            fileSystem.ResolvePath(CanonicalResourceOwnerAuthorityComposer.AuthorityPath),
            composed.CanonicalAuthorityJson);
    }

    private static JsonObject NpcCoreRoot(params JsonObject[] inventory)
    {
        var actor = MortalActorTestFixtures.CreateActor(
            "field_medic_01",
            "loc_field_clinic_001",
            "Field clinic");
        actor["inventory"] = new JsonArray(inventory.Cast<JsonNode?>().ToArray());

        // MortalItemCarrierCatalog deliberately accepts only UpdateNPCs and
        // NPCsInScene for canonical NPC inventories.  Keep the item in the latter
        // rather than an ignored compatibility section.
        return new JsonObject
        {
            ["NPCsInScene"] = new JsonArray(actor)
        };
    }

    private static JsonObject CreateTreatmentSkill(string skillId, string capabilityRef) => new()
    {
        // This is the smallest current production-valid active-skill envelope
        // from ActorMaterializationContractTests, extended only by T060's
        // materialized treatment capability surface.
        ["skillId"] = skillId,
        ["displayName"] = "Field Medicine",
        ["currentMasteryLevel"] = 3,
        ["skillName"] = "Field Medicine",
        ["skillDescription"] = "Provides precise field care under pressure.",
        ["rarity"] = "Common",
        ["actionCost"] = "Main",
        ["combatEffect"] = new JsonObject
        {
            ["isActivatedEffect"] = true,
            ["actionName"] = "Field treatment",
            ["effects"] = new JsonArray(new JsonObject
            {
                ["effectType"] = "Damage",
                ["value"] = "10%",
                ["targetType"] = "Enemy",
                ["effectDescription"] = "A controlled intervention.",
                ["poiseDamage"] = "5%"
            })
        },
        ["mortalWoundTreatmentCapabilities"] = new JsonArray(new JsonObject
        {
            ["schemaVersion"] = 1,
            ["capabilityRef"] = capabilityRef,
            ["woundDomain"] = "physical",
            ["minimumSeverityRank"] = 1,
            ["maximumSeverityRank"] = 4,
            ["operationLimits"] = new JsonObject
            {
                ["mayStabilize"] = true,
                ["maximumRecoveryPoints"] = 2,
                ["maximumSeverityReductionSteps"] = 1,
                ["removableComplicationKinds"] = new JsonArray("infection"),
                ["mayHealAtSeverityI"] = true,
                ["maximumCosmeticHealLegacies"] = 1,
                ["maximumMechanicalEffectHealLegacies"] = 4
            }
        })
    };

    private static JsonObject CreateHistoryFor(string name, string mode)
    {
        return WoundContractTestData.CreateHistory();
    }

    private static (JsonObject Carrier, JsonObject IdentityIndex)
        CreateCanonicalPlayerEffectState(ResolverScenario scenario)
    {
        var effects = new List<JsonObject>();
        if (scenario.RollMode is "advantage" or "disadvantage")
        {
            var roll = EffectMaterializationTestFixture.CreateCanonicalEffect(
                "player",
                "roll_modifier");
            roll["effectId"] = "effect_roll_modifier_" + scenario.RollMode;
            roll["display"]!["name"] = "T061 " + scenario.RollMode;
            roll["source"] = new JsonObject
            {
                ["kind"] = "skill",
                ["sourceId"] = "skill_field_medicine_01",
                ["definitionKey"] = "t061-treatment-roll-" + scenario.RollMode
            };
            roll["components"]![0]!["payload"] =
                EffectMaterializationTestFixture.CreateBroadRollModifierPayload(
                    scenario.RollMode,
                    "skill_check");
            effects.Add(roll);
        }

        if (scenario.SeedFateEffectId is not null)
        {
            // Two independently materialized shields are required to prove that
            // the shared production arbiter selects accepted chronology's oldest
            // eligible carrier rather than merely accepting the only candidate.
            effects.Add(CreateCanonicalFateShield(scenario.SeedFateEffectId));
            effects.Add(CreateCanonicalFateShield("effect_fate_shield_newer"));
        }

        for (var ordinal = 0; ordinal < effects.Count; ordinal++)
        {
            var eventRef = $"turn_{30 + ordinal}:t061_effect_seed:{ordinal + 1}";
            var transitionId = $"effect_transition_t061_seed_{ordinal + 1}";
            effects[ordinal]["chronology"]!["createdAtTurn"] = 30 + ordinal;
            effects[ordinal]["chronology"]!["createdEventRef"] = eventRef;
            effects[ordinal]["chronology"]!["lastTransitionId"] = transitionId;
            effects[ordinal]["chronology"]!["lastTransitionTurn"] = 30 + ordinal;
        }

        if (scenario.SeedCanonicalWoundEffects)
            effects.AddRange(CreateCanonicalWoundEffects(scenario.Before));

        var index = EffectMaterializationTestFixture.CreateIdentityIndex(effects.ToArray());
        var entries = index["entries"]!.AsArray().OfType<JsonObject>().ToArray();
        for (var ordinal = 0; ordinal < entries.Length; ordinal++)
        {
            var eventRef = effects[ordinal]["chronology"]!["createdEventRef"]!.GetValue<string>();
            var transitionId = effects[ordinal]["chronology"]!["lastTransitionId"]!.GetValue<string>();
            var createdAtTurn = effects[ordinal]["chronology"]!["createdAtTurn"]!.GetValue<int>();
            entries[ordinal]["createdAtTurn"] = createdAtTurn;
            var transition = entries[ordinal]["transitions"]![0]!.AsObject();
            transition["transitionId"] = transitionId;
            transition["turn"] = createdAtTurn;
            transition["eventRef"] = eventRef;
        }

        return (
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray(
                    effects.Select(static effect => (JsonNode)effect).ToArray())
            },
            index);
    }

    private static IReadOnlyList<JsonObject> CreateCanonicalWoundEffects(
        JsonObject woundRoot)
    {
        var parsed = WoundMaterializationContract.Parse(
            woundRoot.ToJsonString(),
            "t070.resourcePublication.woundEffectSeed");
        Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
        var wound = Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
        var definitions = wound.Consequences.OwnedEffectSources.Definitions
            .Select(static definition =>
                JsonNode.Parse(definition.GetRawText())!.AsObject())
            .ToDictionary(
                static definition =>
                    definition["definitionKey"]!.GetValue<string>(),
                StringComparer.Ordinal);
        var targetKind = wound.Owner.OwnerKind switch
        {
            "player" or "player_soul" => "player",
            "npc" => "npc",
            "combatant" or "combatant_member" => "combatant",
            "guardian" => "guardian",
            "resident" => "resident",
            "radiant_actor" => "radiant_actor",
            "afterlife_actor" => "afterlife_actor",
            _ => throw new ArgumentOutOfRangeException(
                nameof(woundRoot),
                wound.Owner.OwnerKind,
                "Unsupported wound owner kind for canonical effect seeding.")
        };
        var effects = new List<JsonObject>();
        foreach (var (binding, index) in wound.Consequences.OwnedEffectSources
                     .RootBindings.Select((value, index) => (value, index)))
        {
            var definition = definitions[binding.DefinitionKey];
            var profile = definition["components"]![0]!["profile"]!
                .GetValue<string>();
            var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
                targetKind,
                profile);
            effect["effectId"] = binding.EffectId;
            effect["realm"] = wound.Owner.Realm;
            effect["target"] = new JsonObject
            {
                ["kind"] = targetKind,
                ["targetId"] = wound.Owner.OwnerId
            };
            effect["display"] = definition["display"]!.DeepClone();
            effect["source"] = new JsonObject
            {
                ["kind"] = "wound",
                ["sourceId"] = wound.WoundId,
                ["definitionKey"] = binding.DefinitionKey
            };
            effect["components"] = definition["components"]!.DeepClone();
            effect["lifetime"] = new JsonObject
            {
                ["mode"] = "source_bound",
                ["linkKind"] = "wound",
                ["targetId"] = wound.WoundId,
                ["activePredicate"] =
                    definition["lifetime"]!["activePredicate"]!.DeepClone(),
                ["onSourceLoss"] =
                    definition["lifetime"]!["onSourceLoss"]!.DeepClone()
            };
            effect["stacking"] = new JsonObject
            {
                ["stackKey"] =
                    definition["stacking"]!["stackKey"]!.DeepClone(),
                ["policy"] = definition["stacking"]!["policy"]!.DeepClone(),
                ["maxStacks"] =
                    definition["stacking"]!["maxStacks"]!.DeepClone(),
                ["currentStacks"] = 1,
                ["refreshMode"] =
                    definition["stacking"]!["refreshMode"]?.DeepClone(),
                ["mergeRule"] =
                    definition["stacking"]!["mergeRule"]?.DeepClone()
            };
            effect["triggers"] = definition["triggers"]!.DeepClone();
            effect["removal"] = definition["removal"]!.DeepClone();
            effect["links"] = definition["links"]!.DeepClone();
            effect["chronology"] = new JsonObject
            {
                ["createdAtTurn"] = wound.Origin.CreatedAtTurn,
                ["createdEventRef"] =
                    $"{wound.Origin.EventRef}:effect:{index + 1:D3}",
                ["lastTransitionId"] = "effect_transition_" + binding.EffectId,
                ["lastTransitionTurn"] = wound.Origin.CreatedAtTurn
            };
            effects.Add(effect);
        }
        return effects;
    }

    private static JsonObject CreateCanonicalFateShield(string effectId)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            "player",
            "event_reaction");
        effect["effectId"] = effectId;
        effect["display"] = new JsonObject
        {
            ["name"] = "Щит Судьбы",
            ["description"] =
                "Чернила Судьбы смягчают следующий критический провал до обычного провала.",
            ["category"] = "buff",
            ["visibility"] = "visible",
            ["sourceLabel"] = "Чернильное Перо"
        };
        effect["source"] = new JsonObject
        {
            ["kind"] = EffectBuiltInSourceCatalog.FateShieldSourceKind,
            ["sourceId"] = EffectBuiltInSourceCatalog.FateShieldSourceId,
            ["definitionKey"] = EffectBuiltInSourceCatalog.FateShieldDefinitionKey
        };
        effect["components"] = new JsonArray(new JsonObject
        {
            ["componentId"] = "fate_shield_reaction",
            ["profile"] = "event_reaction",
            ["priority"] = -100,
            ["payload"] = new JsonObject
            {
                ["eventType"] = "owner_critical_failure",
                ["resultKind"] = "event_outcome",
                ["originalOutcome"] = "critical_failure",
                ["resolvedOutcome"] = "failure",
                ["dependency"] = "before_current_event",
                ["maxExpansion"] = 1
            }
        });
        effect["lifetime"] = new JsonObject
        {
            ["mode"] = "uses",
            ["remainingUses"] = 1,
            ["consumingTriggerIds"] = new JsonArray("fate_shield_on_critical_failure"),
            ["displayText"] = "До следующего критического провала"
        };
        effect["stacking"] = new JsonObject
        {
            ["stackKey"] = "ink-feather-fate-shield",
            ["policy"] = "independent",
            // The source definition permits ten independent instances, while each
            // materialized independent occurrence owns exactly one local stack.
            ["maxStacks"] = 1,
            ["currentStacks"] = 1,
            ["refreshMode"] = null,
            ["mergeRule"] = null
        };
        effect["triggers"] = new JsonArray(new JsonObject
        {
            ["triggerId"] = "fate_shield_on_critical_failure",
            ["eventType"] = "owner_critical_failure",
            ["priority"] = -100,
            ["componentIds"] = new JsonArray("fate_shield_reaction"),
            ["consumeUses"] = true,
            ["resolutionMode"] = "deterministic"
        });
        effect["removal"] = new JsonObject
        {
            ["dispelCategories"] = new JsonArray("fate"),
            ["cureKinds"] = new JsonArray(),
            ["onSourceLoss"] = "no_change",
            ["onConditionLoss"] = null,
            ["manualAuthorities"] = new JsonArray()
        };
        effect["links"] = new JsonArray();
        return effect;
    }

    private static void ExecutePreparedFlow(ResolverScenario scenario)
    {
        // The absent planner must stay the uniform current RED boundary.  Once it
        // exists, every descriptor reaches a strict mode-specific wound/history.
        var planner = typeof(WoundMaterializationContract).Assembly.GetType(
            PlannerTypeName,
            throwOnError: false,
            ignoreCase: false);
        Assert.True(planner is not null,
            $"T061 planner is absent; scenario '{scenario.Name}' cannot enter the production-only accepted-state path.");

        // Establish concrete scenario inputs after the current RED seam.  In
        // particular, no raw authority object, die choice, proof, request, receipt,
        // bundle, reservation, or fingerprint is test-constructed here.
        var before = WoundMaterializationContract.Parse(
            scenario.Before.ToJsonString(),
            "wound");
        var history = WoundHistoryState.Parse(
            scenario.History.ToJsonString(),
            "history");
        Assert.True(before.IsValid, DescribeIssues(before.Issues));
        Assert.NotNull(before.Wound);
        Assert.True(history.IsValid, DescribeIssues(history.Issues));
        Assert.NotNull(history.State);
        Assert.Equal("mortal_world", scenario.AcceptedState["realm"]!.GetValue<string>());
        Assert.Equal("physical", before.Wound!.Classification.Domain);
        Assert.Equal(scenario.EventRef, scenario.AcceptedState["eventRef"]!.GetValue<string>());

        var factoryName = scenario.Mode switch
        {
            "procedure" => "PrepareProcedureRequest",
            "course" => "PrepareCourseMilestoneRequest",
            "guaranteed" => "PrepareGuaranteedRequest",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario.Mode), scenario.Mode, null)
        };
        var resolverName = scenario.Mode switch
        {
            "procedure" => "CreateProcedureAttempt",
            "course" => "CreateCourseMilestoneAttempt",
            "guaranteed" => "CreateGuaranteedAttempt",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario.Mode), scenario.Mode, null)
        };
        var factory = ExactStaticMethod(planner, factoryName, 6);
        Assert.Equal("MortalWoundTreatmentAttemptRequestResult", factory.ReturnType.Name);
        Assert.Equal(typeof(string), factory.GetParameters()[3].ParameterType);
        Assert.Equal(typeof(string), factory.GetParameters()[4].ParameterType);
        Assert.Equal(typeof(string), factory.GetParameters()[5].ParameterType);
        Assert.False(typeof(JsonNode).IsAssignableFrom(factory.GetParameters()[0].ParameterType));
        Assert.False(typeof(JsonNode).IsAssignableFrom(factory.GetParameters()[1].ParameterType));
        Assert.False(typeof(JsonNode).IsAssignableFrom(factory.GetParameters()[2].ParameterType));
        AssertFutureTypedHandoffs(planner, scenario.Mode);

        // The fixture passes real canonical sources to the registry and obtains the
        // opaque accepted state under one lease.  It does not create an authority,
        // request, proof, witness, reservation, receipt, or fingerprint itself.
        using var fixture = AcceptedStateFixture.Create(scenario);
        if (scenario.Mode == "procedure")
        {
            fixture.AssertUnchangedT060RequirementResolution(
                Assert.Single(before.Wound!.Treatment.Routes));
        }
        var acceptedState = fixture.GetAcceptedState();
        fixture.AssertCanonicalScenarioAuthority(acceptedState, scenario);
        var acceptedEventRef = fixture.AcceptedEventRef(acceptedState);
        if (scenario.Name == "course_unsatisfied_dose_requirement_has_no_reservation")
        {
            AssertCourseUnsatisfiedRequirementSeam(
                planner,
                acceptedState,
                history,
                before.Wound!,
                scenario,
                acceptedEventRef);
        }
        var preparedResult = Invoke(factory, new object?[]
        {
            acceptedState,
            history,
            before.Wound,
            scenario.OperationKey,
            scenario.RouteId,
            acceptedEventRef
        });
        if (scenario.ExpectedBoundary == "PreparationRejected")
        {
            AssertInvalidTypedResult(preparedResult, "Request", scenario.Name + " request");
            return;
        }

        var request = ReadValidTypedResult(preparedResult, "Request", scenario.Name + " request");
        if (scenario.Mode == "procedure")
        {
            AssertProcedureCheckAuthority(
                ReadRequiredProperty(request, "ModeAuthority"),
                scenario);
            AssertPreparedCriticalReaction(request, acceptedState, scenario);
        }
        var resolver = ExactStaticMethod(planner, resolverName, 4);
        var resolution = Invoke(resolver, new[]
        {
            request,
            (object?)history,
            before.Wound,
            acceptedState
        });
        AssertResolutionShape(resolution, scenario, request);
    }

    private static TreatmentFlow ResolveCurrentTreatment(
        AcceptedStateFixture fixture,
        string mode,
        string operationKey,
        string routeId)
    {
        var planner = typeof(WoundMaterializationContract).Assembly.GetType(
            PlannerTypeName,
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(planner);
        var before = fixture.ReadCurrentWound();
        var history = fixture.ReadCurrentHistory();
        var acceptedState = fixture.GetAcceptedState();
        var eventRef = fixture.AcceptedEventRef(acceptedState);
        var factory = ExactStaticMethod(planner!, mode switch
        {
            "procedure" => "PrepareProcedureRequest",
            "course" => "PrepareCourseMilestoneRequest",
            "guaranteed" => "PrepareGuaranteedRequest",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        }, 6);
        var requestResult = Invoke(factory, new object?[]
        {
            acceptedState,
            history,
            before,
            operationKey,
            routeId,
            eventRef
        });
        var request = ReadValidTypedResult(
            requestResult,
            "Request",
            operationKey + " request");
        var resolution = Invoke(ExactStaticMethod(planner, mode switch
        {
            "procedure" => "CreateProcedureAttempt",
            "course" => "CreateCourseMilestoneAttempt",
            "guaranteed" => "CreateGuaranteedAttempt",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        }, 4), new object?[] { request, history, before, acceptedState });
        Assert.Equal("Resolved", Convert.ToString(ReadRequiredProperty(resolution, "Disposition")));
        Assert.Empty(Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            ReadRequiredProperty(resolution, "Issues")).Cast<object>());
        var resolved = ReadRequiredProperty(resolution, "Resolution");
        return new TreatmentFlow(acceptedState, request, resolved, before, history);
    }

    private static AcceptedMechanicsPlan ComposeAndPublishTreatment(
        AcceptedStateFixture fixture,
        TreatmentFlow flow,
        GameResponse? proposal = null)
    {
        var planner = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.WoundAcceptedTurnPlanner",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(planner);
        var compose = ExactStaticMethod(
            planner!,
            "ComposeMortalWoundTreatmentPublication",
            6);
        var result = Invoke(compose, new object?[]
        {
            fixture.FileSystem,
            fixture.Lease,
            proposal ?? new GameResponse(),
            flow.AcceptedState,
            flow.Request,
            flow.Resolution
        });
        var plan = Assert.IsType<AcceptedMechanicsPlan>(
            ReadValidTypedResult(result, "Plan", "T070 treatment publication"));
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out _,
            out var cached));
        Assert.True(cached.Success, DescribeIssues(cached.Issues));
        Assert.Same(plan, cached.Plan);
        var published = new CanonicalStateNormalizer(
                fixture.FileSystem,
                NullLogger<CanonicalStateNormalizer>.Instance)
            .BindTo(fixture.Lease)
            .NormalizeAcceptedMechanicsAsync(backups: null)
            .GetAwaiter()
            .GetResult();
        Assert.Same(plan, published);
        return plan;
    }

    private static CourseModeProbe InspectCourseMode(
        AcceptedStateFixture fixture,
        string operationKey,
        string routeId)
    {
        var planner = typeof(WoundMaterializationContract).Assembly.GetType(
            PlannerTypeName,
            false,
            false);
        Assert.NotNull(planner);
        var before = fixture.ReadCurrentWound();
        var history = fixture.ReadCurrentHistory();
        var acceptedState = fixture.GetAcceptedState();
        var coordinates = ReadValidTypedResult(
            Invoke(ExactStaticMethod(planner!, "CreateAttemptCoordinates", 5),
                new object?[]
                {
                    acceptedState,
                    before,
                    operationKey,
                    routeId,
                    fixture.AcceptedEventRef(acceptedState)
                }),
            "Coordinates",
            operationKey + " coordinates");
        var gameTimeType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundGameTimeAuthority",
            false,
            false);
        Assert.NotNull(gameTimeType);
        var gameTime = ReadValidTypedResult(
            Invoke(ExactStaticMethod(gameTimeType!, "Create", 2),
                new[] { acceptedState, coordinates }),
            "Authority",
            operationKey + " game time");
        var courseType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundCourseModeAuthority",
            false,
            false);
        Assert.NotNull(courseType);
        var result = Invoke(
            ExactStaticMethod(courseType!, "Create", 5),
            new[] { acceptedState, coordinates, (object)before, history, gameTime });
        AssertClosedProperties(result, new[] { "Disposition", "Issues", "Authority" });
        return new CourseModeProbe(
            acceptedState,
            coordinates,
            before,
            history,
            result);
    }

    private static void AssertCourseAcceptedResolution(
        TreatmentFlow flow,
        int ordinal,
        string disposition,
        bool interruption)
    {
        Assert.Equal(ordinal, Convert.ToInt32(ReadRequiredProperty(
            flow.Request,
            "MilestoneOrdinal")));
        Assert.Equal(ordinal, Convert.ToInt32(ReadRequiredProperty(
            flow.Resolution,
            "CourseMilestoneOrdinal")));
        Assert.Equal(disposition, Convert.ToString(ReadRequiredProperty(
            flow.Resolution,
            "CourseDisposition")));
        Assert.Equal(interruption, Assert.IsType<bool>(ReadRequiredProperty(
            flow.Resolution,
            "Interruption")));
    }

    private static void AssertCourseInterruptionRequest(
        TreatmentFlow flow,
        int expectedMilestoneOrdinal,
        string expectedReason,
        string expectedWindow)
    {
        var coordinates = ReadRequiredProperty(flow.Request, "Coordinates");
        var bundle = ReadRequiredProperty(flow.Request, "RequirementAuthority");
        AssertCourseUnsatisfiedBundle(
            bundle,
            coordinates,
            expectedMilestoneOrdinal,
            expectedReason);
        var modeAuthority = ReadRequiredProperty(flow.Request, "ModeAuthority");
        Assert.Equal(expectedMilestoneOrdinal,
            Convert.ToInt32(ReadRequiredProperty(modeAuthority, "MilestoneOrdinal")));
        Assert.Equal(expectedWindow,
            ReadRequiredProperty(modeAuthority, "WindowDisposition"));
        Assert.Equal(ReadRequiredProperty(modeAuthority, "CourseId"),
            ReadRequiredProperty(bundle, "CourseId"));
        Assert.Equal(ReadRequiredProperty(modeAuthority, "CourseCoordinateFingerprint"),
            ReadRequiredProperty(bundle, "CourseCoordinateFingerprint"));

        var resource = ReadRequiredProperty(flow.Request, "ResourceAuthority");
        AssertClosedProperties(resource, new[]
        {
            "ReservationDisposition", "ReservationId", "CoordinatesFingerprint",
            "AcceptedStateFingerprint", "RouteFingerprint", "CourseId", "CourseMilestoneOrdinal",
            "CourseCoordinateFingerprint", "RequirementAuthorityFingerprint", "Policy", "Claims",
            "AuthorityFingerprint"
        });
        Assert.Equal("not_required", ReadRequiredProperty(resource, "ReservationDisposition"));
        Assert.Null(ReadPropertyAllowingNull(resource, "ReservationId"));
        Assert.Empty(AsObjects(ReadRequiredProperty(resource, "Claims")));
        Assert.Equal(ReadRequiredProperty(coordinates, "CoordinatesFingerprint"),
            ReadRequiredProperty(resource, "CoordinatesFingerprint"));
        Assert.Equal(ReadRequiredProperty(coordinates, "AcceptedStateFingerprint"),
            ReadRequiredProperty(resource, "AcceptedStateFingerprint"));
        Assert.Equal(ReadRequiredProperty(bundle, "RouteFingerprint"),
            ReadRequiredProperty(resource, "RouteFingerprint"));
        Assert.Equal(ReadRequiredProperty(bundle, "AuthorityFingerprint"),
            ReadRequiredProperty(resource, "RequirementAuthorityFingerprint"));
        Assert.Equal(ReadRequiredProperty(bundle, "CourseId"),
            ReadRequiredProperty(resource, "CourseId"));
        Assert.Equal(ReadRequiredProperty(bundle, "CourseMilestoneOrdinal"),
            ReadRequiredProperty(resource, "CourseMilestoneOrdinal"));
        Assert.Equal(ReadRequiredProperty(bundle, "CourseCoordinateFingerprint"),
            ReadRequiredProperty(resource, "CourseCoordinateFingerprint"));
        var policy = ReadRequiredProperty(resource, "Policy");
        AssertClosedProperties(policy, new[]
        {
            "ReserveBeforeResolution", "ConsumeOn", "RefundOn", "Mutations"
        });
        Assert.True(Assert.IsType<bool>(ReadRequiredProperty(policy, "ReserveBeforeResolution")));
        Assert.Equal(new[] { "success" },
            AsObjects(ReadRequiredProperty(policy, "ConsumeOn")).Select(Convert.ToString));
        Assert.Equal(new[] { "cancelled", "validation_failed", "rolled_back" },
            AsObjects(ReadRequiredProperty(policy, "RefundOn")).Select(Convert.ToString));
        var mutations = AsObjects(ReadRequiredProperty(policy, "Mutations"));
        Assert.Equal(3, mutations.Length);
        for (var index = 0; index < mutations.Length; index++)
        {
            AssertClosedProperties(mutations[index], new[]
            {
                "Kind", "Scope", "MilestoneOrdinal", "RequirementIndex"
            });
            Assert.Equal("consume_requirement", ReadRequiredProperty(mutations[index], "Kind"));
            Assert.Equal("course_milestone", ReadRequiredProperty(mutations[index], "Scope"));
            Assert.Equal(index + 1, Convert.ToInt32(
                ReadRequiredProperty(mutations[index], "MilestoneOrdinal")));
            Assert.Equal(0, Convert.ToInt32(
                ReadRequiredProperty(mutations[index], "RequirementIndex")));
        }
        AssertAuthorityFingerprint(ReadRequiredProperty(resource, "AuthorityFingerprint"));
        Assert.Equal(
            CanonicalValue(bundle),
            CanonicalValue(ReadRequiredProperty(flow.Resolution, "RequirementAuthority")));
        Assert.Equal(
            CanonicalValue(resource),
            CanonicalValue(ReadRequiredProperty(flow.Resolution, "ResourceAuthority")));
    }

    private static object ProbePublishedTreatment(
        AcceptedStateFixture fixture,
        object request)
    {
        fixture.RestartForReplay();
        var coordinates = ReadRequiredProperty(request, "Coordinates");
        var history = fixture.ReadCurrentHistory();
        return InvokeInstance(
            ExactInstanceMethod(typeof(WoundHistoryParseResult), "ProbeTreatmentAttempt", 3),
            history,
            new[]
            {
                ReadRequiredProperty(coordinates, "OperationKey"),
                ReadRequiredProperty(coordinates, "AttemptId"),
                ReadRequiredProperty(request, "RequestFingerprint")
            });
    }

    private static void AssertPreparedCriticalReaction(
        object request,
        object acceptedState,
        ResolverScenario scenario)
    {
        var catalogType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.EffectAcceptedEventReportCatalog",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(catalogType);
        var result = Invoke(
            ExactStaticMethod(catalogType, "ResolvePreparedMortalWoundCriticalReaction", 2),
            new[] { request, acceptedState });
        AssertClosedProperties(result, new[] { "IsValid", "Issues", "Intent" });
        Assert.True(Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")));
        Assert.Empty(Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            ReadRequiredProperty(result, "Issues")).Cast<object>());
        var intent = ReadPropertyAllowingNull(result, "Intent");
        if (scenario.ExpectedFateEffectId == null)
        {
            Assert.Null(intent);
            return;
        }

        Assert.NotNull(intent);
        AssertClosedProperties(intent!, new[]
        {
            "EventType", "EventRef", "CausalEventRef", "Turn", "Realm", "TargetKind", "TargetId",
            "EffectId", "TriggerId", "AcceptedEffectFingerprint", "PreparedReactionFingerprint",
            "RequestFingerprint", "IntentFingerprint"
        });
        Assert.Equal("owner_critical_failure", Convert.ToString(ReadRequiredProperty(intent, "EventType")));
        Assert.Equal("mortal_world", Convert.ToString(ReadRequiredProperty(intent, "Realm")));
        Assert.Equal("player", Convert.ToString(ReadRequiredProperty(intent, "TargetKind")));
        Assert.Equal("player_current", Convert.ToString(ReadRequiredProperty(intent, "TargetId")));
        Assert.Equal(scenario.ExpectedFateEffectId, Convert.ToString(ReadRequiredProperty(intent, "EffectId")));
        Assert.Equal("fate_shield_on_critical_failure", Convert.ToString(ReadRequiredProperty(intent, "TriggerId")));
        Assert.False(string.IsNullOrWhiteSpace(
            Convert.ToString(ReadRequiredProperty(intent, "AcceptedEffectFingerprint"))));
        Assert.False(string.IsNullOrWhiteSpace(
            Convert.ToString(ReadRequiredProperty(intent, "PreparedReactionFingerprint"))));
    }

    private static void AssertCourseUnsatisfiedRequirementSeam(
        Type planner,
        object acceptedState,
        WoundHistoryParseResult history,
        WoundMaterializationEnvelope before,
        ResolverScenario scenario,
        string acceptedEventRef)
    {
        // This follows the production course chain instead of inferring a missing
        // dose from the high-level preparation failure.  Each authority is created
        // by its owning future factory; no coordinates, clock, bundle, or claim is
        // test-constructed.
        var coordinates = ReadValidTypedResult(
            Invoke(ExactStaticMethod(planner, "CreateAttemptCoordinates", 5), new object?[]
            {
                acceptedState, before, scenario.OperationKey, scenario.RouteId, acceptedEventRef
            }),
            "Coordinates",
            scenario.Name + " coordinates");
        var gameTimeType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundGameTimeAuthority",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(gameTimeType);
        var gameTime = ReadValidTypedResult(
            Invoke(ExactStaticMethod(gameTimeType, "Create", 2), new[] { acceptedState, coordinates }),
            "Authority",
            scenario.Name + " game time");
        var courseModeType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundCourseModeAuthority",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(courseModeType);
        var courseMode = AssertReadyCourseModeAuthority(
            Invoke(ExactStaticMethod(courseModeType, "Create", 5), new[]
            {
                acceptedState, coordinates, (object)before, history, gameTime
            }),
            scenario);
        var bundleType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentRequirementAuthorityBundle",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(bundleType);
        var requirement = Invoke(
            ExactStaticMethod(bundleType, "CreateForCourseMilestone", 5),
            new[] { acceptedState, coordinates, (object)before, history, courseMode });
        AssertClosedProperties(requirement, new[] { "Status", "Issues", "Authority" });
        Assert.Equal("Unsatisfied", Convert.ToString(ReadRequiredProperty(requirement, "Status")));
        var bundle = ReadPropertyAllowingNull(requirement, "Authority");
        Assert.NotNull(bundle);
        AssertCourseUnsatisfiedBundle(
            bundle!,
            coordinates,
            expectedMilestoneOrdinal: 1,
            expectedInterruptionReason: null);

        var resourceComposer = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentResourceComposer",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(resourceComposer);
        var preparation = Invoke(ExactStaticMethod(resourceComposer, "PrepareCourse", 5), new[]
        {
            acceptedState, coordinates, (object)before, bundle, courseMode
        });
        // At first milestone, trustworthy Unsatisfied must reject before a
        // reservation exists.  The null authority therefore proves no claim/ID can
        // be observed or persisted, while the prior status assertion rules out
        // InvalidAuthority accidentally taking this branch.
        AssertInvalidTypedResult(preparation, "Authority", scenario.Name + " no-reservation preparation");
    }

    private static object AssertReadyCourseModeAuthority(object result, ResolverScenario scenario)
    {
        AssertClosedProperties(result, new[] { "Disposition", "Issues", "Authority" });
        Assert.Equal("Ready", Convert.ToString(ReadRequiredProperty(result, "Disposition")));
        Assert.Empty(Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            ReadRequiredProperty(result, "Issues")).Cast<object>());
        var authority = ReadPropertyAllowingNull(result, "Authority");
        Assert.NotNull(authority);
        AssertClosedProperties(authority!, new[]
        {
            "GameTimeAuthority", "CourseId", "MilestoneOrdinal", "DueAtGameTimeMinutes",
            "DeadlineAtGameTimeMinutes", "WindowDisposition", "CourseStartAuthority",
            "CourseCoordinateFingerprint", "CoordinatesFingerprint", "AcceptedStateFingerprint",
            "AuthorityFingerprint"
        });
        Assert.NotNull(ReadRequiredProperty(authority, "CourseId"));
        Assert.Equal(1, Convert.ToInt32(ReadRequiredProperty(authority, "MilestoneOrdinal")));
        Assert.Equal(scenario.WorldMinute,
            Convert.ToInt64(ReadRequiredProperty(authority, "DueAtGameTimeMinutes")));
        Assert.Equal("ready", Convert.ToString(ReadRequiredProperty(authority, "WindowDisposition")));
        var start = ReadRequiredProperty(authority, "CourseStartAuthority");
        Assert.NotNull(start);
        Assert.NotNull(ReadRequiredProperty(start, "StartingWound"));
        Assert.False(string.IsNullOrWhiteSpace(
            Convert.ToString(ReadRequiredProperty(start, "AuthorityFingerprint"))));
        Assert.False(string.IsNullOrWhiteSpace(
            Convert.ToString(ReadRequiredProperty(authority, "CourseCoordinateFingerprint"))));
        Assert.False(string.IsNullOrWhiteSpace(
            Convert.ToString(ReadRequiredProperty(authority, "AuthorityFingerprint"))));
        return authority!;
    }

    private static void AssertCourseUnsatisfiedBundle(
        object bundle,
        object coordinates,
        int expectedMilestoneOrdinal,
        string? expectedInterruptionReason)
    {
        AssertClosedProperties(bundle, new[]
        {
            "Mode", "ContextFingerprint", "AcceptedStateFingerprint", "RouteFingerprint", "CourseId",
            "CourseMilestoneOrdinal", "CourseCoordinateFingerprint", "CourseRequirementStatus",
            "InterruptionReason", "Scopes", "AuthorityFingerprint"
        });
        Assert.Equal("course", Convert.ToString(ReadRequiredProperty(bundle, "Mode")));
        Assert.Equal("Unsatisfied", Convert.ToString(
            ReadRequiredProperty(bundle, "CourseRequirementStatus")));
        Assert.Equal(expectedInterruptionReason,
            ReadPropertyAllowingNull(bundle, "InterruptionReason"));
        Assert.Equal(ReadRequiredProperty(coordinates, "ContextFingerprint"),
            ReadRequiredProperty(bundle, "ContextFingerprint"));
        Assert.Equal(ReadRequiredProperty(coordinates, "AcceptedStateFingerprint"),
            ReadRequiredProperty(bundle, "AcceptedStateFingerprint"));
        AssertAuthorityFingerprint(ReadRequiredProperty(bundle, "RouteFingerprint"));
        AssertAuthorityFingerprint(ReadRequiredProperty(bundle, "AuthorityFingerprint"));
        Assert.NotNull(ReadRequiredProperty(bundle, "CourseId"));
        Assert.Equal(expectedMilestoneOrdinal,
            Convert.ToInt32(ReadRequiredProperty(bundle, "CourseMilestoneOrdinal")));
        AssertAuthorityFingerprint(ReadRequiredProperty(bundle, "CourseCoordinateFingerprint"));
        var scopes = Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            ReadRequiredProperty(bundle, "Scopes")).Cast<object>().ToArray();
        Assert.Equal(2, scopes.Length);
        var common = Assert.Single(scopes, scope =>
            string.Equals(Convert.ToString(ReadRequiredProperty(scope, "Scope")), "common", StringComparison.Ordinal));
        var milestone = Assert.Single(scopes, scope =>
            string.Equals(Convert.ToString(ReadRequiredProperty(scope, "Scope")), "course_milestone", StringComparison.Ordinal));
        Assert.Equal("Satisfied", Convert.ToString(ReadRequiredProperty(common, "Status")));
        Assert.Equal("Unsatisfied", Convert.ToString(ReadRequiredProperty(milestone, "Status")));
        Assert.Null(ReadPropertyAllowingNull(common, "CourseMilestoneOrdinal"));
        Assert.Equal(expectedMilestoneOrdinal,
            Convert.ToInt32(ReadRequiredProperty(milestone, "CourseMilestoneOrdinal")));
        Assert.Empty(AsObjects(ReadRequiredProperty(common, "FailureWitnesses")));
        var commonBinding = Assert.Single(AsObjects(ReadRequiredProperty(common, "Bindings")));
        AssertRequirementBinding(
            commonBinding,
            "common",
            0,
            "provider",
            "field_medic_01",
            coordinates);
        Assert.Empty(AsObjects(ReadRequiredProperty(milestone, "Bindings")));
        var failure = Assert.Single(AsObjects(ReadRequiredProperty(
            milestone,
            "FailureWitnesses")));
        AssertClosedProperties(failure, new[]
        {
            "Scope", "RequirementIndex", "Kind", "AuthorityRef", "LossReason",
            "Observation", "WitnessFingerprint"
        });
        Assert.Equal("course_milestone", ReadRequiredProperty(failure, "Scope"));
        Assert.Equal(0, Convert.ToInt32(ReadRequiredProperty(failure, "RequirementIndex")));
        Assert.Equal("item_quantity", ReadRequiredProperty(failure, "Kind"));
        Assert.Equal("antibiotic_dose", ReadRequiredProperty(failure, "AuthorityRef"));
        Assert.Equal("authority_absent", ReadRequiredProperty(failure, "LossReason"));
        Assert.Null(ReadPropertyAllowingNull(failure, "Observation"));
        AssertAuthorityFingerprint(ReadRequiredProperty(failure, "WitnessFingerprint"));
        AssertAuthorityFingerprint(ReadRequiredProperty(common, "AuthorityFingerprint"));
        AssertAuthorityFingerprint(ReadRequiredProperty(milestone, "AuthorityFingerprint"));
    }

    private static MethodInfo ExactStaticMethod(Type type, string name, int parameterCount) =>
        Assert.Single(
            type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            candidate => candidate.Name == name && candidate.GetParameters().Length == parameterCount);

    private static MethodInfo ExactInstanceMethod(Type type, string name, int parameterCount) =>
        Assert.Single(
            type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            candidate => candidate.Name == name && candidate.GetParameters().Length == parameterCount);

    private static void AssertFutureTypedHandoffs(Type planner, string mode)
    {
        var t060 = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentAuthority",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(t060);
        var unchangedRequirements = ExactStaticMethod(t060, "ResolveRequirements", 3);
        Assert.Equal(typeof(WoundTreatmentRoute), unchangedRequirements.GetParameters()[0].ParameterType);
        Assert.False(typeof(JsonNode).IsAssignableFrom(unchangedRequirements.GetParameters()[1].ParameterType));
        Assert.False(typeof(JsonNode).IsAssignableFrom(unchangedRequirements.GetParameters()[2].ParameterType));
        var sealName = mode switch
        {
            "procedure" => "SealProcedureRequest",
            "course" => "SealCourseMilestoneRequest",
            "guaranteed" => "SealGuaranteedRequest",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
        var seal = ExactStaticMethod(planner, sealName, mode == "course" ? 6 : 5);
        Assert.Equal("MortalWoundTreatmentAttemptRequestResult", seal.ReturnType.Name);
        Assert.Equal(
            typeof(WoundMaterializationEnvelope),
            seal.GetParameters()[^1].ParameterType);

        var bundleType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentRequirementAuthorityBundle",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(bundleType);
        var bundleName = mode switch
        {
            "procedure" => "CreateForProcedure",
            "course" => "CreateForCourseMilestone",
            "guaranteed" => "CreateForGuaranteed",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
        var bundle = ExactStaticMethod(bundleType, bundleName, mode == "course" ? 5 : 3);
        Assert.False(typeof(JsonNode).IsAssignableFrom(bundle.GetParameters()[0].ParameterType));

        var historyProbe = ExactInstanceMethod(
            typeof(WoundHistoryParseResult),
            "ProbeTreatmentAttempt",
            3);
        Assert.Equal("MortalWoundTreatmentReplayProbeResult", historyProbe.ReturnType.Name);

        var reactionCatalog = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.EffectAcceptedEventReportCatalog",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(reactionCatalog);
        var reaction = ExactStaticMethod(
            reactionCatalog,
            "ResolvePreparedMortalWoundCriticalReaction",
            2);
        Assert.Equal("MortalWoundCriticalReactionResolutionResult", reaction.ReturnType.Name);
        var compose = ExactStaticMethod(reactionCatalog, "Compose", 5);
        Assert.Equal(typeof(JsonNode), compose.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(int), compose.GetParameters()[1].ParameterType);
        Assert.Equal(typeof(string), compose.GetParameters()[2].ParameterType);

        var resourceComposer = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentResourceComposer",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(resourceComposer);
        var resourceName = mode switch
        {
            "procedure" => "PrepareProcedure",
            "course" => "PrepareCourse",
            "guaranteed" => "PrepareGuaranteed",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
        Assert.Equal(
            "MortalWoundTreatmentResourcePreparationResult",
            ExactStaticMethod(resourceComposer, resourceName, 5).ReturnType.Name);
    }

    private static object Invoke(MethodInfo method, object?[] arguments)
    {
        try
        {
            var result = method.Invoke(null, arguments);
            Assert.NotNull(result);
            return result;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    private static object InvokeInstance(
        MethodInfo method,
        object instance,
        object?[] arguments)
    {
        try
        {
            var result = method.Invoke(instance, arguments);
            Assert.NotNull(result);
            return result;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    private static object ReadValidTypedResult(object result, string propertyName, string boundary)
    {
        Assert.Equal(
            new[] { "IsValid", "Issues", propertyName }.OrderBy(static value => value),
            result.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.GetIndexParameters().Length == 0)
                .Select(static property => property.Name)
                .OrderBy(static value => value));
        var valid = Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid"));
        var issues = ReadRequiredProperty(result, "Issues") as System.Collections.IEnumerable;
        Assert.NotNull(issues);
        var diagnostics = issues.Cast<object>()
            .Select(issue => issue is ValidationIssue typed
                ? $"{typed.Code}: expected={typed.Expected}; actual={typed.Actual}"
                : issue.ToString())
            .ToArray();
        Assert.True(
            valid,
            $"{boundary} was rejected by the production boundary.{Environment.NewLine}" +
            string.Join(Environment.NewLine, diagnostics));
        Assert.Empty(issues.Cast<object>());
        return ReadRequiredProperty(result, propertyName);
    }

    private static void AssertInvalidTypedResult(object result, string propertyName, string boundary)
    {
        Assert.Equal(
            new[] { "IsValid", "Issues", propertyName }.OrderBy(static value => value),
            result.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.GetIndexParameters().Length == 0)
                .Select(static property => property.Name)
                .OrderBy(static value => value));
        Assert.False(Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")),
            $"{boundary} unexpectedly crossed its explicit rejection boundary.");
        var issues = Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            ReadRequiredProperty(result, "Issues")).Cast<object>().ToArray();
        Assert.NotEmpty(issues);
        Assert.Null(ReadPropertyAllowingNull(result, propertyName));
    }

    private static void AssertResolutionShape(
        object result,
        ResolverScenario scenario,
        object request)
    {
        Assert.Equal(
            new[] { "Disposition", "Issues", "ReplayReceipt", "Resolution" },
            result.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.GetIndexParameters().Length == 0)
                .Select(static property => property.Name)
                .OrderBy(static value => value));
        var disposition = Assert.IsType<string>(ReadRequiredProperty(result, "Disposition"));
        var issues = Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            ReadRequiredProperty(result, "Issues")).Cast<object>().ToArray();
        Assert.Empty(issues);
        Assert.Equal("Resolved", disposition);
        Assert.NotNull(ReadPropertyAllowingNull(result, "Resolution"));
        Assert.Null(ReadPropertyAllowingNull(result, "ReplayReceipt"));
        var resolved = ReadRequiredProperty(result, "Resolution");
        AssertClosedProperties(resolved, new[]
        {
            "Mode", "Coordinates", "AttemptDisposition", "ResultCategory", "SelectedOutcomeIndex",
            "Interruption", "DeclaredResult", "OutcomeIntents", "CriticalReactionIntent",
            "ConsumptionTrigger", "CourseId", "CourseMilestoneOrdinal", "CourseDisposition",
            "RequestAuthority", "RequirementAuthority", "ResourceAuthority", "ModeEvidence",
            "RouteFingerprint", "ResolutionAuthorityFingerprint", "RequestFingerprint",
            "ResultFingerprint", "RouteCompletion"
        });
        Assert.Equal(scenario.Mode, Assert.IsType<string>(ReadRequiredProperty(resolved, "Mode")));
        Assert.Equal("AcceptedTerminal", Assert.IsType<string>(ReadRequiredProperty(resolved, "AttemptDisposition")));
        Assert.Equal(scenario.ExpectedCategory,
            Assert.IsType<string>(ReadRequiredProperty(resolved, "ResultCategory")));
        if (scenario.Name == "procedure_player_natural_one_reserves_oldest_fate_shield")
            Assert.Equal(2, Convert.ToInt32(ReadRequiredProperty(resolved, "SelectedOutcomeIndex")));
        var resolvedRequest = ReadRequiredProperty(resolved, "RequestAuthority");
        Assert.Equal(request.GetType(), resolvedRequest.GetType());
        Assert.Equal(CanonicalValue(request), CanonicalValue(resolvedRequest));
        AssertCompleteRequestBundle(resolvedRequest, scenario);
        Assert.Equal(
            CanonicalValue(ReadRequiredProperty(request, "Coordinates")),
            CanonicalValue(ReadRequiredProperty(resolved, "Coordinates")));
        Assert.Equal(
            CanonicalValue(ReadRequiredProperty(request, "RequirementAuthority")),
            CanonicalValue(ReadRequiredProperty(resolved, "RequirementAuthority")));
        Assert.Equal(
            CanonicalValue(ReadRequiredProperty(request, "ResourceAuthority")),
            CanonicalValue(ReadRequiredProperty(resolved, "ResourceAuthority")));
        Assert.Equal(
            ReadRequiredProperty(request, "RequestFingerprint"),
            ReadRequiredProperty(resolved, "RequestFingerprint"));
        Assert.Equal(
            ReadRequiredProperty(ReadRequiredProperty(request, "RequirementAuthority"), "RouteFingerprint"),
            ReadRequiredProperty(resolved, "RouteFingerprint"));
        Assert.Equal(
            ReadPropertyAllowingNull(request, "MilestoneOrdinal"),
            ReadPropertyAllowingNull(resolved, "CourseMilestoneOrdinal"));
        if (scenario.Mode == "procedure")
            AssertProcedureResolutionEvidence(
                ReadRequiredProperty(resolved, "ModeEvidence"),
                ReadRequiredProperty(request, "ModeAuthority"),
                resolved,
                scenario);
        var intents = Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            ReadRequiredProperty(resolved, "OutcomeIntents")).Cast<object>().ToArray();
        Assert.Equal(scenario.ExpectedIntentCount, intents.Length);
        Assert.All(intents, intent =>
        {
            Assert.NotNull(ReadPropertyAllowingNull(intent, "IntentFingerprint"));
            Assert.NotNull(ReadPropertyAllowingNull(intent, "Kind"));
        });
    }

    private static void AssertCompleteRequestBundle(object request, ResolverScenario scenario)
    {
        AssertClosedProperties(request, new[]
        {
            "Mode", "Coordinates", "MilestoneOrdinal", "RouteSourceWound",
            "RouteSourceWoundFingerprint", "ModeAuthority", "RequirementAuthority",
            "ResourceAuthority", "RequestFingerprint"
        });
        Assert.Equal(scenario.Mode, Assert.IsType<string>(ReadRequiredProperty(request, "Mode")));
        var coordinates = ReadRequiredProperty(request, "Coordinates");
        AssertTreatmentCoordinates(coordinates, scenario);
        var modeAuthority = ReadRequiredProperty(request, "ModeAuthority");
        var requestFingerprint = ReadRequiredProperty(request, "RequestFingerprint");
        AssertAuthorityFingerprint(requestFingerprint);
        var routeSourceWound = Assert.IsType<WoundMaterializationEnvelope>(
            ReadRequiredProperty(request, "RouteSourceWound"));
        var routeSourceWoundFingerprint = Assert.IsType<string>(
            ReadRequiredProperty(request, "RouteSourceWoundFingerprint"));
        Assert.Equal(
            ReadRequiredProperty(coordinates, "ExpectedBeforeFingerprint"),
            routeSourceWoundFingerprint);
        Assert.Equal(
            routeSourceWoundFingerprint,
            WoundIdentityState.ComputeSemanticFingerprint(routeSourceWound));
        var bundle = ReadRequiredProperty(request, "RequirementAuthority");
        AssertClosedProperties(bundle, new[]
        {
            "Mode", "ContextFingerprint", "AcceptedStateFingerprint", "RouteFingerprint", "CourseId",
            "CourseMilestoneOrdinal", "CourseCoordinateFingerprint", "CourseRequirementStatus",
            "InterruptionReason", "Scopes", "AuthorityFingerprint"
        });
        Assert.Equal(scenario.Mode, Assert.IsType<string>(ReadRequiredProperty(bundle, "Mode")));
        Assert.Equal(
            ReadRequiredProperty(coordinates, "ContextFingerprint"),
            ReadRequiredProperty(bundle, "ContextFingerprint"));
        Assert.Equal(
            ReadRequiredProperty(coordinates, "AcceptedStateFingerprint"),
            ReadRequiredProperty(bundle, "AcceptedStateFingerprint"));
        AssertAuthorityFingerprint(ReadRequiredProperty(bundle, "RouteFingerprint"));
        AssertAuthorityFingerprint(ReadRequiredProperty(bundle, "AuthorityFingerprint"));
        var scopes = Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            ReadRequiredProperty(bundle, "Scopes")).Cast<object>().ToArray();
        Assert.Equal(scenario.Mode == "course" ? 2 : 1, scopes.Length);
        var expectedScopes = scenario.Mode == "course"
            ? new[] { "common", "course_milestone" }
            : new[] { "common" };
        Assert.Equal(expectedScopes, scopes.Select(scope =>
            Convert.ToString(ReadRequiredProperty(scope, "Scope"))));
        foreach (var scope in scopes)
        {
            AssertClosedProperties(scope, new[]
            {
                "Scope", "CourseMilestoneOrdinal", "Status", "Bindings", "FailureWitnesses",
                "AuthorityFingerprint"
            });
            var scopeName = Assert.IsType<string>(ReadRequiredProperty(scope, "Scope"));
            Assert.Equal("Satisfied", ReadRequiredProperty(scope, "Status"));
            Assert.Empty(AsObjects(ReadRequiredProperty(scope, "FailureWitnesses")));
            AssertAuthorityFingerprint(ReadRequiredProperty(scope, "AuthorityFingerprint"));
            var expectedBindings = (scenario.Mode, scopeName) switch
            {
                ("procedure", "common") => new[]
                {
                    (Index: 0, Kind: "item_quantity", Ref: "sterile_thread"),
                    (Index: 1, Kind: "skill_tier", Ref: "field_medicine")
                },
                ("course", "common") => new[]
                {
                    (Index: 0, Kind: "provider", Ref: "field_medic_01")
                },
                ("course", "course_milestone") => new[]
                {
                    (Index: 0, Kind: "item_quantity", Ref: "antibiotic_dose")
                },
                ("guaranteed", "common") => new[]
                {
                    (Index: 0, Kind: "source_capability", Ref: "exact_materialized_healing_source")
                },
                _ => throw new Xunit.Sdk.XunitException(
                    $"Unexpected treatment requirement scope {scenario.Mode}/{scopeName}.")
            };
            var bindings = AsObjects(ReadRequiredProperty(scope, "Bindings"));
            Assert.Equal(expectedBindings.Length, bindings.Length);
            for (var index = 0; index < bindings.Length; index++)
            {
                AssertRequirementBinding(
                    bindings[index],
                    scopeName,
                    expectedBindings[index].Index,
                    expectedBindings[index].Kind,
                    expectedBindings[index].Ref,
                    coordinates);
            }
        }

        var resource = ReadRequiredProperty(request, "ResourceAuthority");
        AssertResourceAuthority(resource, bundle, coordinates, scopes, scenario);
        if (scenario.Mode == "course")
        {
            Assert.Equal(1, Convert.ToInt32(ReadRequiredProperty(request, "MilestoneOrdinal")));
            Assert.Equal("Satisfied", ReadRequiredProperty(bundle, "CourseRequirementStatus"));
            Assert.Null(ReadPropertyAllowingNull(bundle, "InterruptionReason"));
            AssertCourseModeAuthority(modeAuthority, coordinates, bundle, scenario);
        }
        else
        {
            Assert.Null(ReadPropertyAllowingNull(request, "MilestoneOrdinal"));
            Assert.Null(ReadPropertyAllowingNull(bundle, "CourseId"));
            Assert.Null(ReadPropertyAllowingNull(bundle, "CourseMilestoneOrdinal"));
            Assert.Null(ReadPropertyAllowingNull(bundle, "CourseCoordinateFingerprint"));
            Assert.Null(ReadPropertyAllowingNull(bundle, "CourseRequirementStatus"));
            Assert.Null(ReadPropertyAllowingNull(bundle, "InterruptionReason"));
            if (scenario.Mode == "procedure")
                AssertProcedureCheckAuthority(modeAuthority, scenario);
            else
                AssertGuaranteedModeAuthority(modeAuthority, coordinates);
        }
    }

    private static void AssertTreatmentCoordinates(object coordinates, ResolverScenario scenario)
    {
        AssertClosedProperties(coordinates, new[]
        {
            "SchemaVersion", "SessionId", "SessionGeneration", "RequestId", "SnapshotToken",
            "OperationKey", "AttemptId", "WoundId", "RouteId", "ExpectedBeforeFingerprint",
            "EventRef", "EventKind", "EventAuthorityId", "EventSemanticFingerprint", "Turn",
            "Realm", "ProviderKind", "ProviderId", "TargetKind", "TargetId", "LocationId",
            "ContextFingerprint", "AcceptedStateFingerprint", "CoordinatesFingerprint"
        });
        Assert.Equal(1, Convert.ToInt32(ReadRequiredProperty(coordinates, "SchemaVersion")));
        Assert.Equal("session_t061", ReadRequiredProperty(coordinates, "SessionId"));
        Assert.False(string.IsNullOrWhiteSpace(Convert.ToString(
            ReadRequiredProperty(coordinates, "SessionGeneration"))));
        Assert.Equal("request_t061", ReadRequiredProperty(coordinates, "RequestId"));
        Assert.False(string.IsNullOrWhiteSpace(Convert.ToString(
            ReadRequiredProperty(coordinates, "SnapshotToken"))));
        Assert.Equal(scenario.OperationKey, ReadRequiredProperty(coordinates, "OperationKey"));
        Assert.False(string.IsNullOrWhiteSpace(Convert.ToString(
            ReadRequiredProperty(coordinates, "AttemptId"))));
        Assert.Equal("wound_test_torn_side", ReadRequiredProperty(coordinates, "WoundId"));
        Assert.Equal(scenario.RouteId, ReadRequiredProperty(coordinates, "RouteId"));
        Assert.Equal(scenario.EventRef, ReadRequiredProperty(coordinates, "EventRef"));
        Assert.Equal("accepted_turn", ReadRequiredProperty(coordinates, "EventKind"));
        Assert.Equal("turn_42", ReadRequiredProperty(coordinates, "EventAuthorityId"));
        Assert.Equal(42, Convert.ToInt32(ReadRequiredProperty(coordinates, "Turn")));
        Assert.Equal("mortal_world", ReadRequiredProperty(coordinates, "Realm"));
        Assert.Equal("npc", ReadRequiredProperty(coordinates, "ProviderKind"));
        Assert.Equal("field_medic_01", ReadRequiredProperty(coordinates, "ProviderId"));
        Assert.Equal("player", ReadRequiredProperty(coordinates, "TargetKind"));
        Assert.Equal("player_current", ReadRequiredProperty(coordinates, "TargetId"));
        Assert.Equal("loc_field_clinic_001", ReadRequiredProperty(coordinates, "LocationId"));
        foreach (var property in new[]
                 {
                     "ExpectedBeforeFingerprint", "EventSemanticFingerprint", "ContextFingerprint",
                     "AcceptedStateFingerprint", "CoordinatesFingerprint"
                 })
            AssertAuthorityFingerprint(ReadRequiredProperty(coordinates, property));
    }

    private static void AssertRequirementBinding(
        object binding,
        string scope,
        int requirementIndex,
        string kind,
        string authorityRef,
        object coordinates)
    {
        AssertClosedProperties(binding, new[]
        {
            "RequirementIndex", "ResolvedRequirement", "SuccessWitness", "BindingFingerprint"
        });
        Assert.Equal(requirementIndex, Convert.ToInt32(
            ReadRequiredProperty(binding, "RequirementIndex")));
        AssertAuthorityFingerprint(ReadRequiredProperty(binding, "BindingFingerprint"));

        var row = ReadRequiredProperty(binding, "ResolvedRequirement");
        AssertClosedProperties(row, new[]
        {
            "AuthorityFingerprint", "AuthorityRef", "CurrentState", "CurrentTier", "Kind", "LocationId",
            "MinimumTier", "OwnerId", "OwnerKind", "ProviderId", "ProviderKind", "Realm",
            "RequestedQuantity", "RequirementIndex", "TargetId", "TargetKind"
        });
        Assert.Equal(requirementIndex, Convert.ToInt32(
            ReadRequiredProperty(row, "RequirementIndex")));
        Assert.Equal(kind, ReadRequiredProperty(row, "Kind"));
        Assert.Equal(authorityRef, ReadRequiredProperty(row, "AuthorityRef"));
        Assert.Equal("mortal_world", ReadRequiredProperty(row, "Realm"));
        AssertAuthorityFingerprint(ReadRequiredProperty(row, "AuthorityFingerprint"));

        var witness = ReadRequiredProperty(binding, "SuccessWitness");
        Assert.False(witness is JsonNode or JsonDocument or JsonElement);
        foreach (var property in new[]
                 {
                     "Scope", "RequirementIndex", "Kind", "AuthorityRef", "SnapshotToken", "Realm",
                     "WitnessFingerprint"
                 })
            Assert.NotNull(witness.GetType().GetProperty(
                property,
                BindingFlags.Instance | BindingFlags.Public));
        Assert.Equal(scope, ReadRequiredProperty(witness, "Scope"));
        Assert.Equal(requirementIndex, Convert.ToInt32(
            ReadRequiredProperty(witness, "RequirementIndex")));
        Assert.Equal(kind, ReadRequiredProperty(witness, "Kind"));
        Assert.Equal(authorityRef, ReadRequiredProperty(witness, "AuthorityRef"));
        Assert.Equal("mortal_world", ReadRequiredProperty(witness, "Realm"));
        Assert.Equal(
            ReadRequiredProperty(coordinates, "SnapshotToken"),
            ReadRequiredProperty(witness, "SnapshotToken"));
        AssertAuthorityFingerprint(ReadRequiredProperty(witness, "WitnessFingerprint"));
    }

    private static void AssertResourceAuthority(
        object resource,
        object bundle,
        object coordinates,
        IReadOnlyList<object> scopes,
        ResolverScenario scenario)
    {
        AssertClosedProperties(resource, new[]
        {
            "ReservationDisposition", "ReservationId", "CoordinatesFingerprint",
            "AcceptedStateFingerprint", "RouteFingerprint", "CourseId", "CourseMilestoneOrdinal",
            "CourseCoordinateFingerprint", "RequirementAuthorityFingerprint", "Policy", "Claims",
            "AuthorityFingerprint"
        });
        Assert.Equal(
            ReadRequiredProperty(coordinates, "CoordinatesFingerprint"),
            ReadRequiredProperty(resource, "CoordinatesFingerprint"));
        Assert.Equal(
            ReadRequiredProperty(coordinates, "AcceptedStateFingerprint"),
            ReadRequiredProperty(resource, "AcceptedStateFingerprint"));
        Assert.Equal(
            ReadRequiredProperty(bundle, "RouteFingerprint"),
            ReadRequiredProperty(resource, "RouteFingerprint"));
        Assert.Equal(
            ReadRequiredProperty(bundle, "AuthorityFingerprint"),
            ReadRequiredProperty(resource, "RequirementAuthorityFingerprint"));
        AssertAuthorityFingerprint(ReadRequiredProperty(resource, "AuthorityFingerprint"));

        var policy = ReadRequiredProperty(resource, "Policy");
        AssertClosedProperties(policy, new[]
        {
            "ReserveBeforeResolution", "ConsumeOn", "RefundOn", "Mutations"
        });
        Assert.True(Assert.IsType<bool>(ReadRequiredProperty(policy, "ReserveBeforeResolution")));
        var expectedConsumeOn = scenario.Mode switch
        {
            "procedure" => new[] { "success", "partial_success", "failed_attempt" },
            "course" or "guaranteed" => new[] { "success" },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario.Mode), scenario.Mode, null)
        };
        Assert.Equal(expectedConsumeOn, AsObjects(ReadRequiredProperty(policy, "ConsumeOn"))
            .Select(Convert.ToString));
        Assert.Equal(
            new[] { "cancelled", "validation_failed", "rolled_back" },
            AsObjects(ReadRequiredProperty(policy, "RefundOn")).Select(Convert.ToString));
        var mutations = AsObjects(ReadRequiredProperty(policy, "Mutations"));
        Assert.Equal(scenario.Mode switch
        {
            "procedure" => 1,
            "course" => 3,
            "guaranteed" => 0,
            _ => -1
        }, mutations.Length);
        for (var index = 0; index < mutations.Length; index++)
        {
            var mutation = mutations[index];
            AssertClosedProperties(mutation, new[]
            {
                "Kind", "Scope", "MilestoneOrdinal", "RequirementIndex"
            });
            Assert.Equal("consume_requirement", ReadRequiredProperty(mutation, "Kind"));
            Assert.Equal(scenario.Mode == "course" ? "course_milestone" : "common",
                ReadRequiredProperty(mutation, "Scope"));
            Assert.Equal(0, Convert.ToInt32(ReadRequiredProperty(mutation, "RequirementIndex")));
            if (scenario.Mode == "course")
                Assert.Equal(index + 1, Convert.ToInt32(
                    ReadRequiredProperty(mutation, "MilestoneOrdinal")));
            else
                Assert.Null(ReadPropertyAllowingNull(mutation, "MilestoneOrdinal"));
        }

        var claims = AsObjects(ReadRequiredProperty(resource, "Claims"));
        var expectedClaimCount = scenario.Mode == "guaranteed" ? 0 : 1;
        Assert.Equal(expectedClaimCount, claims.Length);
        Assert.Equal(expectedClaimCount == 0 ? "not_required" : "held",
            ReadRequiredProperty(resource, "ReservationDisposition"));
        if (expectedClaimCount == 0)
        {
            Assert.Null(ReadPropertyAllowingNull(resource, "ReservationId"));
        }
        else
        {
            Assert.False(string.IsNullOrWhiteSpace(Convert.ToString(
                ReadRequiredProperty(resource, "ReservationId"))));
            var claim = Assert.Single(claims);
            AssertClosedProperties(claim, new[]
            {
                "Scope", "RequirementIndex", "Kind", "AuthorityRef", "Realm", "OwnerKind",
                "OwnerId", "Quantity", "SuccessWitnessFingerprint", "ClaimFingerprint"
            });
            var claimScope = scenario.Mode == "course" ? "course_milestone" : "common";
            var claimRef = scenario.Mode == "course" ? "antibiotic_dose" : "sterile_thread";
            Assert.Equal(claimScope, ReadRequiredProperty(claim, "Scope"));
            Assert.Equal(0, Convert.ToInt32(ReadRequiredProperty(claim, "RequirementIndex")));
            Assert.Equal("item_quantity", ReadRequiredProperty(claim, "Kind"));
            Assert.Equal(claimRef, ReadRequiredProperty(claim, "AuthorityRef"));
            Assert.Equal("mortal_world", ReadRequiredProperty(claim, "Realm"));
            Assert.Equal(
                scenario.Mode == "course" ? "player" : "npc",
                ReadRequiredProperty(claim, "OwnerKind"));
            Assert.Equal(
                scenario.Mode == "course" ? "player_current" : "field_medic_01",
                ReadRequiredProperty(claim, "OwnerId"));
            Assert.Equal(1, Convert.ToInt32(ReadRequiredProperty(claim, "Quantity")));
            AssertAuthorityFingerprint(ReadRequiredProperty(claim, "SuccessWitnessFingerprint"));
            AssertAuthorityFingerprint(ReadRequiredProperty(claim, "ClaimFingerprint"));

            var scope = Assert.Single(scopes, candidate => string.Equals(
                Convert.ToString(ReadRequiredProperty(candidate, "Scope")),
                claimScope,
                StringComparison.Ordinal));
            var binding = Assert.Single(AsObjects(ReadRequiredProperty(scope, "Bindings")), candidate =>
                Convert.ToInt32(ReadRequiredProperty(candidate, "RequirementIndex")) == 0);
            var witness = ReadRequiredProperty(binding, "SuccessWitness");
            Assert.Equal(
                ReadRequiredProperty(witness, "WitnessFingerprint"),
                ReadRequiredProperty(claim, "SuccessWitnessFingerprint"));
        }

        if (scenario.Mode == "course")
        {
            Assert.Equal(ReadRequiredProperty(bundle, "CourseId"),
                ReadRequiredProperty(resource, "CourseId"));
            Assert.Equal(ReadRequiredProperty(bundle, "CourseMilestoneOrdinal"),
                ReadRequiredProperty(resource, "CourseMilestoneOrdinal"));
            Assert.Equal(ReadRequiredProperty(bundle, "CourseCoordinateFingerprint"),
                ReadRequiredProperty(resource, "CourseCoordinateFingerprint"));
        }
        else
        {
            Assert.Null(ReadPropertyAllowingNull(resource, "CourseId"));
            Assert.Null(ReadPropertyAllowingNull(resource, "CourseMilestoneOrdinal"));
            Assert.Null(ReadPropertyAllowingNull(resource, "CourseCoordinateFingerprint"));
        }
    }

    private static void AssertSingleHeldClaim(object request, string expectedAuthorityRef)
    {
        var resource = ReadRequiredProperty(request, "ResourceAuthority");
        Assert.Equal("held", Convert.ToString(ReadRequiredProperty(
            resource,
            "ReservationDisposition")));
        Assert.False(string.IsNullOrWhiteSpace(Convert.ToString(ReadRequiredProperty(
            resource,
            "ReservationId"))));

        var claim = Assert.Single(AsObjects(ReadRequiredProperty(resource, "Claims")));
        Assert.Equal(expectedAuthorityRef, Convert.ToString(ReadRequiredProperty(
            claim,
            "AuthorityRef")));
        Assert.Equal(1, Convert.ToInt32(ReadRequiredProperty(claim, "Quantity")));
        AssertAuthorityFingerprint(ReadRequiredProperty(claim, "SuccessWitnessFingerprint"));
        AssertAuthorityFingerprint(ReadRequiredProperty(claim, "ClaimFingerprint"));
    }

    private static void AssertHeldRequirementClaims(
        object request,
        params string[] expectedAuthorityRefs)
    {
        var resource = ReadRequiredProperty(request, "ResourceAuthority");
        Assert.Equal("held", Convert.ToString(ReadRequiredProperty(
            resource,
            "ReservationDisposition")));
        Assert.False(string.IsNullOrWhiteSpace(Convert.ToString(ReadRequiredProperty(
            resource,
            "ReservationId"))));
        var claims = AsObjects(ReadRequiredProperty(resource, "Claims"));
        Assert.Equal(
            expectedAuthorityRefs.OrderBy(static value => value, StringComparer.Ordinal),
            claims.Select(claim => Convert.ToString(ReadRequiredProperty(
                    claim,
                    "AuthorityRef"))!)
                .OrderBy(static value => value, StringComparer.Ordinal));
        Assert.All(claims, claim =>
        {
            Assert.Equal(1, Convert.ToInt32(ReadRequiredProperty(claim, "Quantity")));
            AssertAuthorityFingerprint(ReadRequiredProperty(
                claim,
                "SuccessWitnessFingerprint"));
            AssertAuthorityFingerprint(ReadRequiredProperty(claim, "ClaimFingerprint"));
        });
    }

    private static void AssertCourseModeAuthority(
        object authority,
        object coordinates,
        object bundle,
        ResolverScenario scenario)
    {
        AssertClosedProperties(authority, new[]
        {
            "GameTimeAuthority", "CourseId", "MilestoneOrdinal", "DueAtGameTimeMinutes",
            "DeadlineAtGameTimeMinutes", "WindowDisposition", "CourseStartAuthority",
            "CourseCoordinateFingerprint", "CoordinatesFingerprint", "AcceptedStateFingerprint",
            "AuthorityFingerprint"
        });
        Assert.Equal(1, Convert.ToInt32(ReadRequiredProperty(authority, "MilestoneOrdinal")));
        Assert.Equal(scenario.WorldMinute,
            Convert.ToInt64(ReadRequiredProperty(authority, "DueAtGameTimeMinutes")));
        Assert.Equal(scenario.WorldMinute + 600,
            Convert.ToInt64(ReadRequiredProperty(authority, "DeadlineAtGameTimeMinutes")));
        Assert.Equal("ready", ReadRequiredProperty(authority, "WindowDisposition"));
        Assert.Equal(ReadRequiredProperty(coordinates, "CoordinatesFingerprint"),
            ReadRequiredProperty(authority, "CoordinatesFingerprint"));
        Assert.Equal(ReadRequiredProperty(coordinates, "AcceptedStateFingerprint"),
            ReadRequiredProperty(authority, "AcceptedStateFingerprint"));
        Assert.Equal(ReadRequiredProperty(authority, "CourseId"), ReadRequiredProperty(bundle, "CourseId"));
        Assert.Equal(ReadRequiredProperty(authority, "CourseCoordinateFingerprint"),
            ReadRequiredProperty(bundle, "CourseCoordinateFingerprint"));
        AssertAuthorityFingerprint(ReadRequiredProperty(authority, "AuthorityFingerprint"));

        var gameTime = ReadRequiredProperty(authority, "GameTimeAuthority");
        AssertClosedProperties(gameTime, new[]
        {
            "ClockKind", "SourcePath", "CurrentTimeInMinutes", "CoordinatesFingerprint",
            "AcceptedStateFingerprint", "AuthorityFingerprint"
        });
        Assert.Equal("world_time.currentTimeInMinutes", ReadRequiredProperty(gameTime, "ClockKind"));
        Assert.Equal("game_state/world/world_time.json", ReadRequiredProperty(gameTime, "SourcePath"));
        Assert.Equal(scenario.WorldMinute,
            Convert.ToInt64(ReadRequiredProperty(gameTime, "CurrentTimeInMinutes")));
        Assert.Equal(ReadRequiredProperty(coordinates, "CoordinatesFingerprint"),
            ReadRequiredProperty(gameTime, "CoordinatesFingerprint"));
        Assert.Equal(ReadRequiredProperty(coordinates, "AcceptedStateFingerprint"),
            ReadRequiredProperty(gameTime, "AcceptedStateFingerprint"));
        AssertAuthorityFingerprint(ReadRequiredProperty(gameTime, "AuthorityFingerprint"));

        var start = ReadRequiredProperty(authority, "CourseStartAuthority");
        AssertClosedProperties(start, new[]
        {
            "CourseId", "RouteId", "RouteFingerprint", "StartingWound", "StartingWoundFingerprint",
            "StartedAtGameTimeMinutes", "AcceptedStateFingerprint", "CoordinatesFingerprint",
            "AuthorityFingerprint"
        });
        Assert.Equal(ReadRequiredProperty(authority, "CourseId"), ReadRequiredProperty(start, "CourseId"));
        Assert.Equal(scenario.RouteId, ReadRequiredProperty(start, "RouteId"));
        Assert.Equal(ReadRequiredProperty(bundle, "RouteFingerprint"),
            ReadRequiredProperty(start, "RouteFingerprint"));
        Assert.Equal(scenario.WorldMinute,
            Convert.ToInt64(ReadRequiredProperty(start, "StartedAtGameTimeMinutes")));
        Assert.Equal(ReadRequiredProperty(coordinates, "AcceptedStateFingerprint"),
            ReadRequiredProperty(start, "AcceptedStateFingerprint"));
        Assert.Equal(ReadRequiredProperty(coordinates, "CoordinatesFingerprint"),
            ReadRequiredProperty(start, "CoordinatesFingerprint"));
        Assert.IsType<WoundMaterializationEnvelope>(ReadRequiredProperty(start, "StartingWound"));
        AssertAuthorityFingerprint(ReadRequiredProperty(start, "StartingWoundFingerprint"));
        AssertAuthorityFingerprint(ReadRequiredProperty(start, "AuthorityFingerprint"));
    }

    private static void AssertGuaranteedModeAuthority(object authority, object coordinates)
    {
        AssertClosedProperties(authority, new[]
        {
            "SnapshotToken", "SourcePath", "OwnerKind", "OwnerId", "SkillKind", "SkillId",
            "CapabilityRef", "WoundDomain", "MinimumSeverityRank", "MaximumSeverityRank",
            "OperationLimits", "ContextFingerprint", "AcceptedStateFingerprint",
            "CoordinatesFingerprint", "SourceSemanticFingerprint", "ProofFingerprint"
        });
        Assert.Equal(ReadRequiredProperty(coordinates, "SnapshotToken"),
            ReadRequiredProperty(authority, "SnapshotToken"));
        Assert.Equal("game_state/npcs/npc_core.json", ReadRequiredProperty(authority, "SourcePath"));
        Assert.Equal("npc", ReadRequiredProperty(authority, "OwnerKind"));
        Assert.Equal("field_medic_01", ReadRequiredProperty(authority, "OwnerId"));
        Assert.Equal("active", ReadRequiredProperty(authority, "SkillKind"));
        Assert.Equal("skill_guaranteed_care_01", ReadRequiredProperty(authority, "SkillId"));
        Assert.Equal("exact_materialized_healing_source", ReadRequiredProperty(authority, "CapabilityRef"));
        Assert.Equal("physical", ReadRequiredProperty(authority, "WoundDomain"));
        Assert.Equal(ReadRequiredProperty(coordinates, "ContextFingerprint"),
            ReadRequiredProperty(authority, "ContextFingerprint"));
        Assert.Equal(ReadRequiredProperty(coordinates, "AcceptedStateFingerprint"),
            ReadRequiredProperty(authority, "AcceptedStateFingerprint"));
        Assert.Equal(ReadRequiredProperty(coordinates, "CoordinatesFingerprint"),
            ReadRequiredProperty(authority, "CoordinatesFingerprint"));
        var limits = ReadRequiredProperty(authority, "OperationLimits");
        AssertClosedProperties(limits, new[]
        {
            "MayHealAtSeverityI", "MayStabilize", "MaximumCosmeticHealLegacies",
            "MaximumMechanicalEffectHealLegacies", "MaximumRecoveryPoints",
            "MaximumSeverityReductionSteps", "RemovableComplicationKinds"
        });
        Assert.True(Assert.IsType<bool>(ReadRequiredProperty(limits, "MayHealAtSeverityI")));
        Assert.True(Assert.IsType<bool>(ReadRequiredProperty(limits, "MayStabilize")));
        Assert.Equal(1, Convert.ToInt32(ReadRequiredProperty(limits, "MaximumCosmeticHealLegacies")));
        Assert.Equal(4, Convert.ToInt32(ReadRequiredProperty(limits, "MaximumMechanicalEffectHealLegacies")));
        Assert.Equal(2, Convert.ToInt32(ReadRequiredProperty(limits, "MaximumRecoveryPoints")));
        Assert.Equal(1, Convert.ToInt32(ReadRequiredProperty(limits, "MaximumSeverityReductionSteps")));
        Assert.Equal(new[] { "infection" },
            AsObjects(ReadRequiredProperty(limits, "RemovableComplicationKinds"))
                .Select(Convert.ToString));
        AssertAuthorityFingerprint(ReadRequiredProperty(authority, "SourceSemanticFingerprint"));
        AssertAuthorityFingerprint(ReadRequiredProperty(authority, "ProofFingerprint"));
    }

    private static void AssertAuthorityFingerprint(object value) =>
        Assert.Matches("^sha256:[0-9a-f]{64}$", Assert.IsType<string>(value));

    private static void AssertProcedureCheckAuthority(object authority, ResolverScenario scenario)
    {
        AssertClosedProperties(authority, new[]
        {
            "SourcePath", "RollMode", "RollActorKind", "RollActorId", "RollContributions",
            "SourceIndices", "SourceRolls", "SelectedSourceIndex", "NaturalRoll", "Modifier",
            "ComplicationDifficultyModifier", "EffectiveDifficulty", "RequirementAuthorityFingerprint",
            "CoordinatesFingerprint", "AcceptedStateFingerprint", "PreparedCriticalReaction",
            "AuthorityFingerprint"
        });
        Assert.Equal("input/turn_request.json",
            Convert.ToString(ReadRequiredProperty(authority, "SourcePath")));
        Assert.Equal(scenario.RollMode, Convert.ToString(ReadRequiredProperty(authority, "RollMode")));
        var modifierKind = scenario.Before["treatment"]!["routes"]![0]!["resolution"]!["modifierSource"]!["kind"]!
            .GetValue<string>();
        Assert.Equal(modifierKind == "fixed_zero" ? "npc" : "player",
            Convert.ToString(ReadRequiredProperty(authority, "RollActorKind")));
        Assert.Equal(modifierKind == "fixed_zero" ? "field_medic_01" : "player_current",
            Convert.ToString(ReadRequiredProperty(authority, "RollActorId")));
        var contributions = Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            ReadRequiredProperty(authority, "RollContributions")).Cast<object>().ToArray();
        if (scenario.RollMode == "normal")
        {
            Assert.Empty(contributions);
        }
        else
        {
            var contribution = Assert.Single(contributions);
            AssertClosedProperties(contribution, new[] { "EffectId", "ComponentId", "Contribution" });
            Assert.Equal("effect_roll_modifier_" + scenario.RollMode,
                Convert.ToString(ReadRequiredProperty(contribution, "EffectId")));
            Assert.Equal("component_001",
                Convert.ToString(ReadRequiredProperty(contribution, "ComponentId")));
            Assert.Equal(scenario.RollMode,
                Convert.ToString(ReadRequiredProperty(contribution, "Contribution")));
        }
        Assert.Equal(scenario.ExpectedSourceIndices,
            ReadIntSequence(ReadRequiredProperty(authority, "SourceIndices")));
        var acceptedDice = scenario.AcceptedState["acceptedDice"]!.AsArray();
        Assert.Equal(
            scenario.ExpectedSourceIndices.Select(index => acceptedDice[index]!.GetValue<int>()),
            ReadIntSequence(ReadRequiredProperty(authority, "SourceRolls")));
        Assert.Equal(scenario.ExpectedSelectedSourceIndex,
            Convert.ToInt32(ReadRequiredProperty(authority, "SelectedSourceIndex")));
        Assert.Equal(scenario.ExpectedNaturalRoll,
            Convert.ToInt32(ReadRequiredProperty(authority, "NaturalRoll")));
        var prepared = ReadPropertyAllowingNull(authority, "PreparedCriticalReaction");
        if (scenario.ExpectedFateEffectId == null)
        {
            Assert.Null(prepared);
            return;
        }

        Assert.NotNull(prepared);
        AssertClosedProperties(prepared!, new[]
        {
            "EffectId", "TriggerId", "AcceptedEffectFingerprint", "PreparedReactionFingerprint"
        });
        Assert.Equal(scenario.ExpectedFateEffectId,
            Convert.ToString(ReadRequiredProperty(prepared!, "EffectId")));
        Assert.Equal("fate_shield_on_critical_failure",
            Convert.ToString(ReadRequiredProperty(prepared, "TriggerId")));
        Assert.False(string.IsNullOrWhiteSpace(
            Convert.ToString(ReadRequiredProperty(prepared, "AcceptedEffectFingerprint"))));
        Assert.False(string.IsNullOrWhiteSpace(
            Convert.ToString(ReadRequiredProperty(prepared, "PreparedReactionFingerprint"))));
    }

    private static void AssertProcedureResolutionEvidence(
        object evidence,
        object preparedAuthority,
        object resolution,
        ResolverScenario scenario)
    {
        AssertClosedProperties(evidence, new[]
        {
            "RollMode", "RollActorKind", "RollActorId", "SourceIndices", "SourceRolls",
            "SelectedSourceIndex", "NaturalRoll", "Modifier", "Total", "BaseDifficulty",
            "ComplicationDifficultyModifier", "EffectiveDifficulty", "Margin",
            "OriginalOutcome", "ResolvedOutcome", "SelectedBandId", "SelectedOutcomeIndex",
            "ReactionEffectId", "ReactionTriggerId", "ReactionFingerprint",
            "AcceptedRollFingerprint"
        });

        foreach (var property in new[]
                 {
                     "RollMode", "RollActorKind", "RollActorId", "SourceIndices", "SourceRolls",
                     "SelectedSourceIndex", "NaturalRoll", "Modifier",
                     "ComplicationDifficultyModifier", "EffectiveDifficulty"
                 })
        {
            Assert.Equal(
                CanonicalValue(ReadRequiredProperty(preparedAuthority, property)),
                CanonicalValue(ReadRequiredProperty(evidence, property)));
        }

        var naturalRoll = Convert.ToInt32(ReadRequiredProperty(evidence, "NaturalRoll"));
        var modifier = Convert.ToInt32(ReadRequiredProperty(evidence, "Modifier"));
        var total = checked(naturalRoll + modifier);
        var baseDifficulty = scenario.Before["treatment"]!["routes"]![0]!["resolution"]!["difficulty"]!
            .GetValue<int>();
        var complicationModifier = Convert.ToInt32(
            ReadRequiredProperty(evidence, "ComplicationDifficultyModifier"));
        var effectiveDifficulty = checked(baseDifficulty + complicationModifier);
        Assert.Equal(total, Convert.ToInt32(ReadRequiredProperty(evidence, "Total")));
        Assert.Equal(baseDifficulty, Convert.ToInt32(ReadRequiredProperty(evidence, "BaseDifficulty")));
        Assert.Equal(effectiveDifficulty,
            Convert.ToInt32(ReadRequiredProperty(evidence, "EffectiveDifficulty")));
        Assert.Equal(checked(total - effectiveDifficulty),
            Convert.ToInt32(ReadRequiredProperty(evidence, "Margin")));

        var expectedOriginal = naturalRoll switch
        {
            20 => "critical_success",
            1 => "critical_failure",
            _ => "ordinary"
        };
        var expectedResolved = naturalRoll == 1 && scenario.ExpectedFateEffectId is not null
            ? "failure"
            : expectedOriginal;
        Assert.Equal(expectedOriginal, Convert.ToString(ReadRequiredProperty(evidence, "OriginalOutcome")));
        Assert.Equal(expectedResolved, Convert.ToString(ReadRequiredProperty(evidence, "ResolvedOutcome")));

        var selectedIndex = Convert.ToInt32(ReadRequiredProperty(resolution, "SelectedOutcomeIndex"));
        Assert.Equal(selectedIndex,
            Convert.ToInt32(ReadRequiredProperty(evidence, "SelectedOutcomeIndex")));
        var selectedBand = scenario.Before["treatment"]!["routes"]![0]!["outcomes"]![selectedIndex]!;
        Assert.Equal(selectedBand["bandId"]!.GetValue<string>(),
            Convert.ToString(ReadRequiredProperty(evidence, "SelectedBandId")));
        Assert.Equal(selectedBand["category"]!.GetValue<string>(),
            Convert.ToString(ReadRequiredProperty(resolution, "ResultCategory")));

        if (scenario.ExpectedFateEffectId is null)
        {
            Assert.Null(ReadPropertyAllowingNull(evidence, "ReactionEffectId"));
            Assert.Null(ReadPropertyAllowingNull(evidence, "ReactionTriggerId"));
            Assert.Null(ReadPropertyAllowingNull(evidence, "ReactionFingerprint"));
            Assert.Null(ReadPropertyAllowingNull(resolution, "CriticalReactionIntent"));
        }
        else
        {
            Assert.Equal(scenario.ExpectedFateEffectId,
                Convert.ToString(ReadRequiredProperty(evidence, "ReactionEffectId")));
            Assert.Equal("fate_shield_on_critical_failure",
                Convert.ToString(ReadRequiredProperty(evidence, "ReactionTriggerId")));
            AssertAuthorityFingerprint(ReadRequiredProperty(evidence, "ReactionFingerprint"));
            var reactionIntent = ReadRequiredProperty(resolution, "CriticalReactionIntent");
            Assert.Equal(
                ReadRequiredProperty(reactionIntent, "IntentFingerprint"),
                ReadRequiredProperty(evidence, "ReactionFingerprint"));
        }
        AssertAuthorityFingerprint(ReadRequiredProperty(evidence, "AcceptedRollFingerprint"));
    }

    private static int[] ReadIntSequence(object value) =>
        Assert.IsAssignableFrom<System.Collections.IEnumerable>(value)
            .Cast<object>()
            .Select(Convert.ToInt32)
            .ToArray();

    private static void AssertClosedProperties(object value, IEnumerable<string> expected) =>
        Assert.Equal(
            expected.OrderBy(static name => name),
            value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.GetIndexParameters().Length == 0)
                .Select(static property => property.Name)
                .OrderBy(static name => name));

    private static object ReadRequiredProperty(object instance, string name)
    {
        var value = ReadPropertyAllowingNull(instance, name);
        Assert.NotNull(value);
        return value;
    }

    private static object ReadAcceptedStateMember(object acceptedState, string name)
    {
        var property = acceptedState.GetType().GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        var value = property.GetValue(acceptedState);
        Assert.NotNull(value);
        return value;
    }

    private static object ReadRequiredProperty(object?[] arguments, int index, string boundary)
    {
        var value = arguments[index];
        Assert.NotNull(value);
        return value;
    }

    private static object? ReadPropertyAllowingNull(object instance, string name)
    {
        var property = instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        return property.GetValue(instance);
    }

    private static string DescribeIssues(IEnumerable<ValidationIssue> issues) =>
        string.Join(" | ", issues.Select(issue =>
            $"{issue.Code}@{issue.FilePath}: {issue.Actual}"));

    private sealed record ResolverScenario(
        string Name,
        string Mode,
        JsonObject AcceptedState,
        JsonObject History,
        JsonObject Before,
        string OperationKey,
        string RouteId,
        string EventRef,
        string ExpectedBoundary,
        string? ExpectedCategory,
        string RollMode,
        int[] ExpectedSourceIndices,
        int ExpectedSelectedSourceIndex,
        int ExpectedNaturalRoll,
        long WorldMinute,
        bool RequirementsAvailable,
        int ExpectedIntentCount,
        string? ExpectedFateEffectId,
        string? SeedFateEffectId,
        bool SeedCanonicalWoundEffects = false);

    private sealed record ScenarioSemantics(
        string RollMode,
        int[] SourceIndices,
        int SelectedSourceIndex,
        int NaturalRoll,
        long WorldMinute,
        bool RequirementsAvailable,
        string ExpectedBoundary,
        string? ExpectedCategory,
        int ExpectedIntentCount,
        string? ExpectedFateEffectId);

    private sealed record TreatmentFlow(
        object AcceptedState,
        object Request,
        object Resolution,
        WoundMaterializationEnvelope Before,
        WoundHistoryParseResult History);

    private sealed record CourseModeProbe(
        object AcceptedState,
        object Coordinates,
        WoundMaterializationEnvelope Before,
        WoundHistoryParseResult History,
        object Result);

    private sealed class AcceptedStateReadInterposition
    {
        private readonly string _path;
        private bool _armed;

        internal AcceptedStateReadInterposition(string path)
        {
            _path = path;
            Hooks = new FileSystemManagerHooks
            {
                AfterCanonicalReadInitialValidationAsync = observedPath =>
                {
                    if (_armed && string.Equals(observedPath, _path, StringComparison.Ordinal))
                    {
                        ReadCount++;
                        if (ReadCount > 1)
                            throw new IOException("A validated accepted-state source was reread live.");
                    }
                    return Task.CompletedTask;
                }
            };
        }

        internal FileSystemManagerHooks Hooks { get; }
        internal int ReadCount { get; private set; }
        internal void Arm() => _armed = true;
    }

    private sealed partial class AcceptedStateFixture : IDisposable
    {
        private const string NpcCorePath = "game_state/npcs/npc_core.json";
        private const string PlayerInventoryPath = "game_state/inventory/items.json";
        private const string ItemIdentityPath = "game_state/inventory/item_identity_index.json";

        private AcceptedStateFixture(
            string root,
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease lease,
            object treatmentContext,
            string woundId,
            string targetKind,
            string targetId,
            string targetCarrierPath)
        {
            Root = root;
            FileSystem = fileSystem;
            Lease = lease;
            TreatmentContext = treatmentContext;
            WoundId = woundId;
            TargetKind = targetKind;
            TargetId = targetId;
            TargetCarrierPath = targetCarrierPath;
        }

        internal string Root { get; }
        internal FileSystemManager FileSystem { get; private set; }
        internal FileSystemManager.CanonicalWriteLease Lease { get; private set; }
        private object TreatmentContext { get; }
        internal string WoundId { get; }
        internal string TargetKind { get; }
        internal string TargetId { get; }
        internal string TargetCarrierPath { get; }

        internal static AcceptedStateFixture Create(
            ResolverScenario scenario,
            FileSystemManagerHooks? hooks = null)
        {
            var root = Path.Combine(Path.GetTempPath(), "boe-t061-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var fileSystem = new FileSystemManager(
                root,
                NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance,
                hooks);
            fileSystem.EnsureDirectoryStructure();
            var targetKind = scenario.AcceptedState["targetKind"]!.GetValue<string>();
            var targetId = scenario.AcceptedState["targetId"]!.GetValue<string>();
            var targetCarrierPath = ResolveTargetCarrierPath(targetKind);
            FileSystemManager.CanonicalWriteLease? lease = null;
            try
            {
                foreach (var relativePath in new[]
                         {
                             "game_state/world/world_time.json",
                             "game_state/world/current_location.json",
                             "game_state/meta/soul_state.json",
                             "input/turn_request.json",
                             WoundIdentityState.StatePath,
                              WoundHistoryState.HistoryPath,
                              WoundCarrierCatalog.PlayerPath,
                              WoundCarrierCatalog.EnemiesPath,
                              WoundCarrierCatalog.AlliesPath,
                              EffectCarrierCatalog.PlayerPath,
                             EffectIdentityState.StatePath,
                             "game_state/inventory/items.json",
                             "game_state/inventory/item_identity_index.json"
                         })
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(
                        fileSystem.ResolvePath(relativePath))!);
                }
                WriteTargetWoundCarrier(
                    fileSystem,
                    targetKind,
                    targetId,
                    targetCarrierPath,
                    scenario.Before);
                var parsedBefore = WoundMaterializationContract.Parse(
                    scenario.Before.ToJsonString(),
                    targetCarrierPath + ".activeWounds[0]");
                Assert.True(parsedBefore.IsValid, DescribeIssues(parsedBefore.Issues));
                var woundFingerprint = WoundIdentityState.ComputeSemanticFingerprint(
                    Assert.IsType<WoundMaterializationEnvelope>(parsedBefore.Wound));
                File.WriteAllText(
                    fileSystem.ResolvePath(WoundIdentityState.StatePath),
                    WoundContractTestData.CreateIdentityIndex(
                        WoundContractTestData.CreateIdentityEntry(
                            woundId: scenario.Before["woundId"]!.GetValue<string>(),
                            ownerKind: targetKind,
                            ownerId: targetId,
                            carrierPath: targetCarrierPath,
                            semanticFingerprint: woundFingerprint)).ToJsonString());
                File.WriteAllText(
                    fileSystem.ResolvePath(WoundHistoryState.HistoryPath),
                    scenario.History.ToJsonString());
                var playerTreatmentSkill = CreateTreatmentSkill(
                    "skill_field_medicine_01",
                    "field_medicine");
                if (scenario.RollMode is "advantage" or "disadvantage")
                {
                    var rollDefinition = EffectMaterializationTestFixture
                        .CreateDefinition("roll_modifier");
                    rollDefinition["definitionKey"] =
                        "t061-treatment-roll-" + scenario.RollMode;
                    rollDefinition["display"]!["name"] =
                        "T061 " + scenario.RollMode;
                    rollDefinition["components"]![0]!["payload"] =
                        EffectMaterializationTestFixture.CreateBroadRollModifierPayload(
                            scenario.RollMode,
                            "skill_check");
                    playerTreatmentSkill["activeEffectDefinitions"] =
                        new JsonArray(rollDefinition);
                }
                File.WriteAllText(
                    fileSystem.ResolvePath("game_state/player/skills_active.json"),
                    new JsonObject
                    {
                        ["activeSkillChanges"] = new JsonArray(playerTreatmentSkill)
                    }.ToJsonString());
                File.WriteAllText(
                    fileSystem.ResolvePath("game_state/player/skills_passive.json"),
                    new JsonObject
                    {
                        ["passiveSkillChanges"] = new JsonArray(
                            CreateProductionPassiveSkill())
                    }.ToJsonString());
                File.WriteAllText(
                    fileSystem.ResolvePath("game_state/player/skill_mastery.json"),
                    new JsonObject
                    {
                        ["skillMasteryChanges"] = new JsonArray(new JsonObject
                        {
                            ["skillName"] = "Field Medicine",
                            ["newMasteryLevel"] = 3,
                            ["newCurrentMasteryProgress"] = 0,
                            ["newMasteryProgressNeeded"] = 100,
                            ["masteryLeveledUp"] = false
                        })
                    }.ToJsonString());
                var sterileThread = CreateCanonicalStack(
                    "sterile_thread",
                    scenario.AcceptedState["sterileThreadCount"]?.GetValue<int>() ?? 2);
                var dose = scenario.RequirementsAvailable
                    ? CreateCanonicalStack(
                        "antibiotic_dose",
                        scenario.AcceptedState["antibioticDoseCount"]?.GetValue<int>() ?? 8)
                    : null;
                var reusableTool = scenario.AcceptedState["reusableToolCount"] is null
                    ? null
                    : CreateCanonicalStack(
                        "reusable_field_kit",
                        scenario.AcceptedState["reusableToolCount"]!.GetValue<int>());
                var npcCore = reusableTool is null
                    ? NpcCoreRoot(sterileThread)
                    : NpcCoreRoot(sterileThread, reusableTool);
                var medic = npcCore["NPCsInScene"]![0]!.AsObject();
                medic["displayName"] = "Field medic";
                medic["currentLocationId"] = "loc_field_clinic_001";
                medic["activeSkills"] = new JsonArray(
                    CreateTreatmentSkill("skill_guaranteed_care_01", "exact_materialized_healing_source"),
                    CreateTreatmentSkill("skill_field_medicine_npc_01", "field_medicine"));
                medic["passiveSkills"] = new JsonArray();
                File.WriteAllText(
                    fileSystem.ResolvePath("game_state/npcs/npc_core.json"),
                    npcCore.ToJsonString());
                if (reusableTool is not null)
                {
                    File.WriteAllText(
                        fileSystem.ResolvePath("game_state/npcs/npc_inventory.json"),
                        new JsonObject
                        {
                            ["NPCInventoryAdds"] = new JsonArray()
                        }.ToJsonString());
                }
                File.WriteAllText(
                    fileSystem.ResolvePath("game_state/world/world_time.json"),
                    new JsonObject
                    {
                        ["schemaVersion"] = 1,
                        ["currentTimeInMinutes"] = scenario.AcceptedState["worldMinute"]!.DeepClone()
                    }.ToJsonString());
                var location = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
                    "loc_field_clinic_001",
                    "T061 field clinic");
                location["customStates"] = new JsonArray(
                    new JsonObject
                    {
                        ["kind"] = "mortal_wound_treatment_facility",
                        ["schemaVersion"] = 1,
                        ["facilityId"] = "clean_work_surface",
                        ["displayName"] = "Clean work surface",
                        ["available"] = true
                    },
                    new JsonObject
                    {
                        ["kind"] = "mortal_wound_treatment_environment",
                        ["schemaVersion"] = 1,
                        ["environmentId"] = "sterile_field",
                        ["displayName"] = "Sterile field",
                        ["state"] = "active"
                    },
                    new JsonObject
                    {
                        ["kind"] = "mortal_wound_treatment_consent",
                        ["schemaVersion"] = 1,
                        ["consentRef"] = "consent_field_medic_player_01",
                        ["displayName"] = "Field treatment consent",
                        ["providerKind"] = "npc",
                        ["providerId"] = "field_medic_01",
                        ["targetKind"] = targetKind,
                        ["targetId"] = targetId,
                        ["status"] = "granted"
                    });
                location["materialization"]!["sections"]!["customStates"] = new JsonObject
                {
                    ["disposition"] = "populated",
                    ["reason"] = null
                };
                MortalLocationTestFixture.ResealCanonicalLocation(location);
                File.WriteAllText(
                    fileSystem.ResolvePath(MortalLocationMaterializationContract.WorldMapPath),
                    MortalLocationTestFixture.CreateWorldMap(location).ToJsonString());
                File.WriteAllText(
                    fileSystem.ResolvePath(MortalLocationMaterializationContract.CurrentLocationPath),
                    MortalLocationTestFixture.CreateCurrentProjection(location).ToJsonString());
                File.WriteAllText(
                    fileSystem.ResolvePath(MortalLocationIdentityState.StatePath),
                    MortalLocationTestFixture.CreateIdentityIndex(location).ToJsonString());
                File.WriteAllText(
                    fileSystem.ResolvePath("game_state/meta/soul_state.json"),
                    new JsonObject { ["currentRealm"] = "Mortal World" }.ToJsonString());
                File.WriteAllText(
                    fileSystem.ResolvePath("game_state/inventory/items.json"),
                    PlayerInventoryRoot(dose).ToJsonString());
                File.WriteAllText(
                    fileSystem.ResolvePath("game_state/inventory/item_identity_index.json"),
                    CreateItemIdentityIndex(sterileThread, reusableTool, dose).ToJsonString());
                File.WriteAllText(
                    fileSystem.ResolvePath("game_state/quests/regular_quests.json"),
                    new JsonObject
                    {
                        ["quests"] = new JsonArray(CreateProductionRegularQuest())
                    }.ToJsonString());
                WriteCanonicalResourceAuthority(
                    fileSystem,
                    targetKind,
                    targetId);
                var effectState = CreateCanonicalPlayerEffectState(scenario);
                RelocateCanonicalCombatEffects(
                    fileSystem,
                    targetKind,
                    targetId,
                    targetCarrierPath,
                    effectState.Carrier,
                    effectState.IdentityIndex);
                File.WriteAllText(
                    fileSystem.ResolvePath(EffectCarrierCatalog.PlayerPath),
                    effectState.Carrier.ToJsonString());
                File.WriteAllText(
                    fileSystem.ResolvePath(EffectIdentityState.StatePath),
                    effectState.IdentityIndex.ToJsonString());
                var preparedTurn = new LiveTurnPreparationService(fileSystem)
                    .PrepareAsync(new LiveTurnPreparationOptions
                    {
                        SessionId = scenario.AcceptedState["sessionId"]!.GetValue<string>(),
                        RequestId = scenario.AcceptedState["requestId"]!.GetValue<string>(),
                        TurnNumber = scenario.AcceptedState["turn"]!.GetValue<int>(),
                        PlayerAction =
                            $"Treat wound {scenario.Before["woundId"]!.GetValue<string>()} " +
                            $"through route {scenario.RouteId}; operation {scenario.OperationKey}.",
                        CurrentRealm = "Mortal World",
                        PreGeneratedDices1d20 = scenario.AcceptedState["acceptedDice"]!
                            .AsArray()
                            .Select(static die => die!.GetValue<int>())
                            .ToArray()
                    })
                    .GetAwaiter()
                    .GetResult();
                Assert.Equal("input/turn_request.json", preparedTurn.TurnRequestPath);
                Assert.Equal(
                    scenario.AcceptedState["sessionId"]!.GetValue<string>(),
                    preparedTurn.SessionId);
                Assert.Equal(
                    scenario.AcceptedState["requestId"]!.GetValue<string>(),
                    preparedTurn.RequestId);
                lease = fileSystem.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
                var effectSnapshot = EffectMechanicsSnapshot.LoadAsync(fileSystem, lease)
                    .GetAwaiter()
                    .GetResult();
                Assert.True(effectSnapshot.IsAccepted, DescribeIssues(effectSnapshot.Issues));
                Assert.Equal(
                    effectState.IdentityIndex["entries"]!.AsArray().Count,
                    effectSnapshot.Effects.Count);
                var treatmentAuthority = typeof(WoundMaterializationContract).Assembly.GetType(
                    "BookOfEternityClient.Services.MortalWoundTreatmentAuthority",
                    throwOnError: false,
                    ignoreCase: false);
                Assert.NotNull(treatmentAuthority);
                var parsedContext = Invoke(
                    ExactStaticMethod(treatmentAuthority!, "ParseContext", 2),
                    new object?[]
                    {
                        new JsonObject
                        {
                            ["schemaVersion"] = 1,
                            ["realm"] = "mortal_world",
                            ["targetKind"] = targetKind,
                            ["targetId"] = targetId,
                            ["providerKind"] = "npc",
                            ["providerId"] = "field_medic_01",
                            ["currentLocationId"] = "loc_field_clinic_001"
                        }.ToJsonString(),
                        "treatmentContext"
                    });
                var context = ReadValidTypedResult(
                    parsedContext,
                    "Context",
                    "production treatment context");
                return new AcceptedStateFixture(
                    root,
                    fileSystem,
                    lease,
                    context,
                    scenario.Before["woundId"]!.GetValue<string>(),
                    targetKind,
                    targetId,
                    targetCarrierPath);
            }
            catch
            {
                lease?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                Directory.Delete(root, recursive: true);
                throw;
            }
        }

        private static void WriteTargetWoundCarrier(
            FileSystemManager fileSystem,
            string targetKind,
            string targetId,
            string targetCarrierPath,
            JsonObject wound)
        {
            if (string.Equals(targetKind, "player", StringComparison.Ordinal))
            {
                File.WriteAllText(
                    fileSystem.ResolvePath(WoundCarrierCatalog.PlayerPath),
                    WoundContractTestData.CreatePlayerCarrier(wound).ToJsonString());
                return;
            }

            if (string.Equals(targetKind, "npc", StringComparison.Ordinal))
            {
                File.WriteAllText(
                    fileSystem.ResolvePath(WoundCarrierCatalog.PlayerPath),
                    WoundContractTestData.CreatePlayerCarrier().ToJsonString());
                File.WriteAllText(
                    fileSystem.ResolvePath(WoundCarrierCatalog.NpcPath),
                    new JsonObject
                    {
                        ["schemaVersion"] = 1,
                        ["entries"] = new JsonArray(new JsonObject
                        {
                            ["npcId"] = targetId,
                            ["activeWounds"] = new JsonArray(wound.DeepClone())
                        })
                    }.ToJsonString());
                return;
            }

            File.WriteAllText(
                fileSystem.ResolvePath(WoundCarrierCatalog.PlayerPath),
                WoundContractTestData.CreatePlayerCarrier().ToJsonString());
            var combatant = string.Equals(
                targetKind,
                "combatant_member",
                StringComparison.Ordinal)
                ? CreateProductionCombatGroup(
                    "combatant_group_t061",
                    targetId,
                    wound)
                : CreateProductionCombatant(targetId, wound);
            var collectionName = string.Equals(
                targetKind,
                "combatant_member",
                StringComparison.Ordinal)
                ? "alliesData"
                : "enemiesData";
            File.WriteAllText(
                fileSystem.ResolvePath(targetCarrierPath),
                new JsonObject
                {
                    [collectionName] = new JsonArray(combatant)
                }.ToJsonString());
        }

        private static void RelocateCanonicalCombatEffects(
            FileSystemManager fileSystem,
            string targetKind,
            string targetId,
            string targetCarrierPath,
            JsonObject playerEffectCarrier,
            JsonObject identityIndex)
        {
            if (targetKind is not ("combatant" or "combatant_member"))
                return;

            var playerEffects = playerEffectCarrier["activeEffects"]!.AsArray();
            var targetEffects = playerEffects
                .OfType<JsonObject>()
                .Where(effect =>
                    string.Equals(
                        effect["target"]?["kind"]?.GetValue<string>(),
                        "combatant",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        effect["target"]?["targetId"]?.GetValue<string>(),
                        targetId,
                        StringComparison.Ordinal))
                .Select(static effect => effect.DeepClone().AsObject())
                .ToArray();
            if (targetEffects.Length == 0)
                return;

            for (var index = playerEffects.Count - 1; index >= 0; index--)
            {
                if (playerEffects[index] is JsonObject effect &&
                    targetEffects.Any(candidate => string.Equals(
                        candidate["effectId"]!.GetValue<string>(),
                        effect["effectId"]!.GetValue<string>(),
                        StringComparison.Ordinal)))
                {
                    playerEffects.RemoveAt(index);
                }
            }

            var carrier = JsonNode.Parse(File.ReadAllText(
                fileSystem.ResolvePath(targetCarrierPath)))!.AsObject();
            var combatants = carrier[
                targetKind == "combatant_member"
                    ? "alliesData"
                    : "enemiesData"]!.AsArray();
            var owner = targetKind == "combatant_member"
                ? combatants
                    .OfType<JsonObject>()
                    .SelectMany(static combatant =>
                        combatant["members"]?.AsArray().OfType<JsonObject>() ??
                        Enumerable.Empty<JsonObject>())
                    .Single(member => string.Equals(
                        member["memberId"]?.GetValue<string>(),
                        targetId,
                        StringComparison.Ordinal))
                : combatants
                    .OfType<JsonObject>()
                    .Single(combatant => string.Equals(
                        combatant["combatantId"]?.GetValue<string>(),
                        targetId,
                        StringComparison.Ordinal));
            var identityEntries = identityIndex["entries"]!.AsArray()
                .OfType<JsonObject>()
                .ToDictionary(
                    static entry => entry["effectId"]!.GetValue<string>(),
                    StringComparer.Ordinal);

            foreach (var effect in targetEffects)
            {
                var collection = string.Equals(
                    effect["display"]?["category"]?.GetValue<string>(),
                    "buff",
                    StringComparison.Ordinal)
                    ? "activeBuffs"
                    : "activeDebuffs";
                if (owner[collection] is not JsonArray collectionRows)
                {
                    collectionRows = new JsonArray();
                    owner[collection] = collectionRows;
                }
                collectionRows.Add(effect);

                var effectId = effect["effectId"]!.GetValue<string>();
                var identityOwner = identityEntries[effectId]["owner"]!.AsObject();
                identityOwner["carrierPath"] = targetCarrierPath;
                identityOwner["collection"] = collection;
            }

            File.WriteAllText(
                fileSystem.ResolvePath(targetCarrierPath),
                carrier.ToJsonString());
        }

        internal object ExportCurrent()
        {
            var authorityType = typeof(WoundMaterializationContract).Assembly.GetType(
                "BookOfEternityClient.Services.MortalWoundTreatmentAcceptedStateAuthority",
                throwOnError: false,
                ignoreCase: false);
            Assert.NotNull(authorityType);
            var export = ExactStaticMethod(authorityType, "ExportCurrent", 4);
            Assert.Equal(typeof(FileSystemManager), export.GetParameters()[0].ParameterType);
            Assert.Equal(typeof(FileSystemManager.CanonicalWriteLease), export.GetParameters()[1].ParameterType);
            Assert.Equal(TreatmentContext.GetType(), export.GetParameters()[2].ParameterType);
            Assert.Equal(typeof(string), export.GetParameters()[3].ParameterType);
            return Invoke(export, new object?[]
            {
                FileSystem,
                Lease,
                TreatmentContext,
                WoundId
            });
        }

        internal object GetAcceptedState() =>
            ReadValidTypedResult(ExportCurrent(), "Authority", "T066 accepted-state export");

        internal MortalWoundTreatmentAuthority.Snapshot GetRequirementSnapshot() =>
            Assert.IsType<MortalWoundTreatmentAuthority.Snapshot>(
                ReadAcceptedStateMember(GetAcceptedState(), "RequirementSnapshot"));

        internal void MoveProviderOffScene()
        {
            var root = ReadObject(NpcCorePath);
            var provider = Assert.IsType<JsonObject>(
                Assert.Single(root["NPCsInScene"]!.AsArray())).DeepClone().AsObject();
            root["NPCsInScene"] = new JsonArray();
            provider["currentLocationId"] = "loc_remote_clinic_001";
            root["UpdateNPCs"] = new JsonArray(provider);
            WriteObject(NpcCorePath, root);
            RewriteCanonicalLocation(location =>
            {
                var states = location["customStates"]!.AsArray();
                for (var index = states.Count - 1; index >= 0; index--)
                {
                    if (states[index]?["kind"]?.GetValue<string>() ==
                        "mortal_wound_treatment_consent")
                        states.RemoveAt(index);
                }
            });
            PrepareFreshSnapshot("off_scene_provider");
        }

        internal void RemovePlayerSkillMastery()
        {
            var root = ReadObject("game_state/player/skill_mastery.json");
            root["skillMasteryChanges"] = new JsonArray();
            WriteObject("game_state/player/skill_mastery.json", root);
            PrepareFreshSnapshot("skill_without_mastery");
        }

        internal void SetRegularQuestStatus(string status)
        {
            var root = ReadObject("game_state/quests/regular_quests.json");
            root["quests"]![0]!["status"] = status;
            WriteObject("game_state/quests/regular_quests.json", root);
            PrepareFreshSnapshot("quest_" + status.ToLowerInvariant());
        }

        internal void AddIdlessOrdinarySkillSibling()
        {
            var root = ReadObject("game_state/player/skills_active.json");
            var sibling = root["activeSkillChanges"]![0]!.DeepClone().AsObject();
            sibling.Remove("skillId");
            sibling.Remove("mortalWoundTreatmentCapabilities");
            sibling["skillName"] = "Ordinary observation";
            sibling["displayName"] = "Ordinary observation";
            root["activeSkillChanges"]!.AsArray().Add(sibling);
            WriteObject("game_state/player/skills_active.json", root);
            PrepareFreshSnapshot("idless_ordinary_skill");
        }

        internal void RetireIdentityForLiveItem()
        {
            var root = ReadObject(ItemIdentityPath);
            var entry = Assert.IsType<JsonObject>(Assert.Single(
                root["entries"]!.AsArray(),
                candidate => candidate?["itemId"]?.GetValue<string>() == "sterile_thread"));
            entry["state"] = "destroyed";
            entry["currentCarrier"] = null;
            WriteObject(ItemIdentityPath, root);
            PrepareFreshSnapshot("retired_live_item");
        }

        internal void AddExactProviderCrossCarrierMirrorWithInventory()
        {
            var root = ReadObject(NpcCorePath);
            var provider = Assert.IsType<JsonObject>(Assert.Single(root["NPCsInScene"]!.AsArray()));
            root["UpdateNPCs"] = new JsonArray(provider.DeepClone());
            WriteObject(NpcCorePath, root);
            PrepareFreshSnapshot("exact_npc_cross_carrier_mirror");
        }

        internal void AddCompatibilityNpcCapabilityLookalike()
        {
            var root = ReadObject(NpcCorePath);
            var lookalike = root["NPCsInScene"]![0]!.DeepClone().AsObject();
            lookalike["activeSkills"]![0]!["mortalWoundTreatmentCapabilities"]![0]!["capabilityRef"] =
                "forged_compatibility_capability";
            root["NPCs"] = new JsonArray(lookalike);
            WriteObject(NpcCorePath, root);
            PrepareFreshSnapshot("compatibility_npc_lookalike");
        }

        internal void CorruptPersistedResourceOwnerAuthority()
        {
            var authority = ReadObject(CanonicalResourceOwnerAuthorityComposer.AuthorityPath);
            authority["schemaVersion"] = 99;
            WriteObject(CanonicalResourceOwnerAuthorityComposer.AuthorityPath, authority);
            PrepareFreshSnapshot("stale_resource_owner_authority");
        }

        internal void AddCanonicalNonActorItemSiblings()
        {
            var offscreen = MortalItemTestFixture.CreateCanonicalRoot("item_offscreen_t066");
            var vehicle = MortalItemTestFixture.CreateCanonicalRoot("item_vehicle_t066");
            WriteObject(
                MortalLocationStorageContentsState.StatePath,
                MortalLocationStorageContentsState.BuildCanonicalRoot(
                    new Dictionary<MortalLocationStorageKey, JsonArray>
                    {
                        [new MortalLocationStorageKey(
                            "loc_remote_t066",
                            "storage_remote_t066")] = new JsonArray(offscreen.DeepClone())
                    }));
            WriteObject(
                StorageTransportMoveService.VehiclesPath,
                new JsonObject
                {
                    ["vehicles"] = new JsonArray(new JsonObject
                    {
                        ["vehicleId"] = "vehicle_t066",
                        ["inventory"] = new JsonArray(vehicle.DeepClone())
                    })
                });
            var identity = ReadObject(ItemIdentityPath);
            var offscreenIdentity = MortalItemTestFixture.CreateIndexForCarrier(
                offscreen,
                "location_storage",
                "loc_remote_t066",
                "storage_remote_t066");
            var vehicleIdentity = MortalItemTestFixture.CreateIndexForCarrier(
                vehicle,
                "vehicle_inventory",
                "vehicle_t066");
            identity["entries"]!.AsArray().Add(
                offscreenIdentity["entries"]![0]!.DeepClone());
            identity["entries"]!.AsArray().Add(
                vehicleIdentity["entries"]![0]!.DeepClone());
            WriteObject(ItemIdentityPath, identity);
            WriteCanonicalResourceAuthority(FileSystem);
            PrepareFreshSnapshot("non_actor_item_siblings");
        }

        private void RewriteCanonicalLocation(Action<JsonObject> mutation)
        {
            var map = ReadObject(MortalLocationMaterializationContract.WorldMapPath);
            var location = Assert.IsType<JsonObject>(Assert.Single(map["locations"]!.AsArray()));
            mutation(location);
            MortalLocationTestFixture.ResealCanonicalLocation(location);
            WriteObject(MortalLocationMaterializationContract.WorldMapPath, map);
            WriteObject(
                MortalLocationMaterializationContract.CurrentLocationPath,
                MortalLocationTestFixture.CreateCurrentProjection(location));
            WriteObject(
                MortalLocationIdentityState.StatePath,
                MortalLocationTestFixture.CreateIdentityIndex(location));
        }

        internal string AcceptedEventRef(object acceptedState) =>
            Assert.Single(Assert.IsType<WoundAcceptedTurnBinding>(
                ReadAcceptedStateMember(acceptedState, "Binding")).AcceptedEvents).EventRef;

        internal void ReleaseLeaseForExternalDistribution() =>
            Lease.DisposeAsync().AsTask().GetAwaiter().GetResult();

        internal void ReacquireLeaseAfterExternalDistribution() =>
            Lease = FileSystem.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();

        internal void ReplaceLeaseWithoutGenerationRotation(bool replaceFileSystemManager)
        {
            Lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            if (replaceFileSystemManager)
            {
                FileSystem = new FileSystemManager(
                    Root,
                    NullLogger<FileSystemManager>.Instance);
                FileSystem.EnsureDirectoryStructure();
            }
            Lease = FileSystem.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
        }

        internal void AssertAuthorityBoundToCurrentLease(object authority)
        {
            var bindingCheck = authority.GetType().GetMethod(
                "IsLeaseBoundTo",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(bindingCheck);
            Assert.True(Assert.IsType<bool>(bindingCheck!.Invoke(
                authority,
                new object?[] { FileSystem, Lease })));
        }

        internal object ExportWithContextSchemaVersion(int schemaVersion)
        {
            var current = Assert.IsType<MortalWoundTreatmentAuthority.Context>(TreatmentContext);
            var altered = current with { SchemaVersion = schemaVersion };
            var authorityType = typeof(WoundMaterializationContract).Assembly.GetType(
                "BookOfEternityClient.Services.MortalWoundTreatmentAcceptedStateAuthority",
                throwOnError: true,
                ignoreCase: false)!;
            return Invoke(
                ExactStaticMethod(authorityType, "ExportCurrent", 4),
                new object?[] { FileSystem, Lease, altered, WoundId });
        }

        internal object ExportWithForgedContext(string mutation)
        {
            var current = Assert.IsType<MortalWoundTreatmentAuthority.Context>(TreatmentContext);
            var altered = mutation switch
            {
                "direct_construction" => new MortalWoundTreatmentAuthority.Context(
                    current.SchemaVersion,
                    current.Realm,
                    current.TargetKind,
                    current.TargetId,
                    current.ProviderKind,
                    current.ProviderId,
                    current.CurrentLocationId)
                {
                    SourcePath = current.SourcePath
                },
                "copied_source_path" => current with { SourcePath = "forged/treatment_context.json" },
                "copied_schema_version" => current with { SchemaVersion = 2 },
                _ => throw new ArgumentOutOfRangeException(nameof(mutation))
            };
            var authorityType = typeof(WoundMaterializationContract).Assembly.GetType(
                "BookOfEternityClient.Services.MortalWoundTreatmentAcceptedStateAuthority",
                throwOnError: true,
                ignoreCase: false)!;
            return Invoke(
                ExactStaticMethod(authorityType, "ExportCurrent", 4),
                new object?[] { FileSystem, Lease, altered, WoundId });
        }

        internal void ApplyHistoryAgreementMutation(string mutation)
        {
            var carrier = JsonNode.Parse(File.ReadAllText(FileSystem.ResolvePath(
                TargetCarrierPath)))!.AsObject();
            var wound = FindPersistedWound(carrier);
            var parsed = WoundMaterializationContract.Parse(
                wound.ToJsonString(),
                TargetCarrierPath + ".activeWounds[0]");
            Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
            var afterFingerprint = WoundIdentityState.ComputeSemanticFingerprint(parsed.Wound!);
            var transition = WoundContractTestData.CreateTransition();
            transition["woundId"] = WoundId;
            transition["beforeFingerprint"] =
                WoundHistoryState.ComputeNonexistentBeforeFingerprint(WoundId);
            transition["afterFingerprint"] = afterFingerprint;
            transition["sourceFingerprint"] = "sha256:" + new string('e', 64);
            transition["attemptId"] = null;

            switch (mutation)
            {
                case "latest_after_fingerprint":
                    transition["afterFingerprint"] = "sha256:" + new string('d', 64);
                    break;
                case "latest_transition_ordinal":
                {
                    wound["lastTransition"]!["transitionId"] = "wound_transition_test_002";
                    wound["lastTransition"]!["kind"] = "worsen";
                    wound["lastTransition"]!["ordinal"] = 2;
                    File.WriteAllText(FileSystem.ResolvePath(TargetCarrierPath), carrier.ToJsonString());
                    parsed = WoundMaterializationContract.Parse(
                        wound.ToJsonString(),
                        TargetCarrierPath + ".activeWounds[0]");
                    Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
                    afterFingerprint = WoundIdentityState.ComputeSemanticFingerprint(parsed.Wound!);
                    transition["afterFingerprint"] = afterFingerprint;
                    var identity = JsonNode.Parse(File.ReadAllText(FileSystem.ResolvePath(
                        WoundIdentityState.StatePath)))!.AsObject();
                    identity["entries"]![0]!["lastTransitionOrdinal"] = 2;
                    identity["entries"]![0]!["semanticFingerprint"] = afterFingerprint;
                    File.WriteAllText(
                        FileSystem.ResolvePath(WoundIdentityState.StatePath),
                        identity.ToJsonString());
                    break;
                }
                case "foreign_orphan_chain":
                    transition["woundId"] = "wound_foreign_orphan_001";
                    transition["beforeFingerprint"] =
                        WoundHistoryState.ComputeNonexistentBeforeFingerprint(
                            "wound_foreign_orphan_001");
                    transition["afterFingerprint"] = "sha256:" + new string('c', 64);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
            }

            File.WriteAllText(
                FileSystem.ResolvePath(WoundHistoryState.HistoryPath),
                WoundContractTestData.CreateHistory(transition).ToJsonString());
            PrepareFreshSnapshot("history_" + mutation);
        }

        internal void RemoveRequiredCanonicalRowField(string field)
        {
            if (string.Equals(field, "npc_current_location", StringComparison.Ordinal))
            {
                var root = ReadObject(NpcCorePath);
                root["NPCsInScene"]![0]!.AsObject().Remove("currentLocationId");
                WriteObject(NpcCorePath, root);
            }
            else
            {
                var root = ReadObject(ResourceMaterializationContract.StatePath);
                root["entries"]![0]!.AsObject().Remove(field switch
                {
                    "resource_current" => "current",
                    "resource_state" => "state",
                    "resource_capacity_binding" => "capacityBinding",
                    _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
                });
                WriteObject(ResourceMaterializationContract.StatePath, root);
            }
            PrepareFreshSnapshot("missing_" + field);
        }

        internal void ApplyExplicitNegativeCanonicalRow(string mutation)
        {
            switch (mutation)
            {
                case "provider_offscene":
                    MoveProviderOffScene();
                    return;
                case "facility_unavailable":
                    RewriteCanonicalLocation(location =>
                    {
                        var facility = Assert.Single(location["customStates"]!.AsArray(), row =>
                            row?["kind"]?.GetValue<string>() ==
                            "mortal_wound_treatment_facility");
                        facility!["available"] = false;
                    });
                    break;
                case "withdrawn_consent":
                    RewriteCanonicalLocation(location =>
                    {
                        var consent = Assert.Single(location["customStates"]!.AsArray(), row =>
                            row?["kind"]?.GetValue<string>() ==
                            "mortal_wound_treatment_consent");
                        consent!["status"] = "withdrawn";
                    });
                    break;
                case "resource_zero_unavailable":
                    WriteCanonicalPlayerHealthAuthority(FileSystem, current: 0);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
            }
            PrepareFreshSnapshot(mutation);
        }

        internal void AddNonObjectRequiredCollectionRow(string collection)
        {
            switch (collection)
            {
                case "npc":
                {
                    var root = ReadObject(NpcCorePath);
                    root["NPCsInScene"]!.AsArray().Add("invalid-row");
                    WriteObject(NpcCorePath, root);
                    break;
                }
                case "location":
                {
                    var map = ReadObject(MortalLocationMaterializationContract.WorldMapPath);
                    map["locations"]!.AsArray().Add("invalid-row");
                    WriteObject(MortalLocationMaterializationContract.WorldMapPath, map);
                    break;
                }
                case "resource_state":
                {
                    var root = ReadObject(ResourceMaterializationContract.StatePath);
                    root["entries"]!.AsArray().Add("invalid-row");
                    WriteObject(ResourceMaterializationContract.StatePath, root);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(collection), collection, null);
            }
            PrepareFreshSnapshot("non_object_" + collection);
        }

        internal void ApplyDisplayOnlyAcceptedSourceMutation()
        {
            var npc = ReadObject(NpcCorePath);
            var provider = npc["NPCsInScene"]![0]!.AsObject();
            provider["displayName"] = "Renamed field medic";
            provider["activeSkills"]![0]!["displayName"] = "Renamed guaranteed care";
            WriteObject(NpcCorePath, npc);

            RewriteCanonicalLocation(location =>
            {
                location["name"] = "Renamed field clinic";
                foreach (var state in location["customStates"]!.AsArray().OfType<JsonObject>())
                    state["displayName"] = "Renamed " + state["kind"]!.GetValue<string>();
            });

            var inventory = ReadObject(PlayerInventoryPath);
            if (inventory["items"] is JsonArray items && items.Count > 0)
            {
                items[0]!["displayName"] = "Renamed antibiotic dose";
                MortalItemTestFixture.ResealCanonical(items[0]!.AsObject());
            }
            WriteObject(PlayerInventoryPath, inventory);
            PrepareFreshSnapshot("display_only", preserveRequestId: true);
        }

        internal void ApplySignedAcceptedDiceMutation(string mutation)
        {
            var manifest = JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(
                File.ReadAllText(FileSystem.ResolvePath(
                    LiveTurnPreparationService.PendingTurnSnapshotManifestPath)),
                LiveTurnPreparationService.ManifestJsonOptions)!;
            manifest.PreGeneratedDices1d20 = mutation switch
            {
                "null" => null,
                "empty" => Array.Empty<int>(),
                "out_of_range" => new[] { 21 },
                _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null)
            };
            manifest.ManifestPayloadHash = PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(
                manifest,
                LiveTurnPreparationService.ManifestHashJsonOptions,
                static value => value.ManifestPayloadHash,
                static (value, hash) => value.ManifestPayloadHash = hash);
            var authority = PendingTurnSnapshotAuthority.CreateDetachedAuthorityJson(
                manifest,
                LiveTurnPreparationService.ManifestHashJsonOptions,
                static value => value.ManifestPayloadHash,
                static (value, hash) => value.ManifestPayloadHash = hash,
                static value => value.SessionId,
                static value => value.RequestId,
                static value => value.TurnNumber,
                static value => value.Files,
                static value => value.SnapshotFileHashes,
                static value => value.ClientOwnedValidationHashes,
                static value => value.RollbackBaselineFiles,
                static value => value.SourceLabel,
                static value => value.RollbackBackups,
                FileSystem.ReadFileBytesSync,
                hashSnapshotBytesExactly: true);
            File.WriteAllText(
                FileSystem.ResolvePath(LiveTurnPreparationService.PendingTurnSnapshotManifestPath),
                JsonSerializer.Serialize(manifest, LiveTurnPreparationService.ManifestJsonOptions));
            File.WriteAllText(
                FileSystem.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath),
                authority);
        }

        private JsonObject ReadObject(string relativePath) =>
            JsonNode.Parse(File.ReadAllText(FileSystem.ResolvePath(relativePath)))!.AsObject();

        private void WriteObject(string relativePath, JsonObject root) =>
            File.WriteAllText(FileSystem.ResolvePath(relativePath), root.ToJsonString());

        internal void ApplyUntrustedSourceMutation(string mutation)
        {
            switch (mutation)
            {
                case "stale_snapshot":
                {
                    var request = JsonNode.Parse(File.ReadAllText(FileSystem.ResolvePath(
                        LiveTurnPreparationService.TurnRequestPath)))!.AsObject();
                    request["requestId"] = "request_t061_stale_snapshot";
                    File.WriteAllText(
                        FileSystem.ResolvePath(LiveTurnPreparationService.TurnRequestPath),
                        request.ToJsonString());
                    return;
                }
                case "carrier_identity_history_mismatch":
                {
                    var carrier = JsonNode.Parse(File.ReadAllText(FileSystem.ResolvePath(
                        TargetCarrierPath)))!.AsObject();
                    var wound = FindPersistedWound(carrier);
                    wound["display"]!["name"] = "Changed without identity/history";
                    File.WriteAllText(FileSystem.ResolvePath(TargetCarrierPath), carrier.ToJsonString());
                    PrepareFreshSnapshot("carrier_mismatch");
                    return;
                }
                case "ambiguous_actor_coordinate":
                {
                    var npc = JsonNode.Parse(File.ReadAllText(FileSystem.ResolvePath(
                        "game_state/npcs/npc_core.json")))!.AsObject();
                    npc["NPCsInScene"]!.AsArray().Add(
                        npc["NPCsInScene"]![0]!.DeepClone());
                    File.WriteAllText(
                        FileSystem.ResolvePath("game_state/npcs/npc_core.json"),
                        npc.ToJsonString());
                    PrepareFreshSnapshot("ambiguous_actor");
                    return;
                }
                case "malformed_required_root":
                    File.WriteAllText(
                        FileSystem.ResolvePath("game_state/world/current_location.json"),
                        "[]");
                    PrepareFreshSnapshot("malformed_location");
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
            }
        }

        internal void ApplyTrustedNegativePredicateMutation(string mutation)
        {
            switch (mutation)
            {
                case "withdrawn_consent":
                    RewriteCanonicalLocation(location =>
                    {
                        var consent = Assert.Single(location["customStates"]!.AsArray(), row =>
                            row?["kind"]?.GetValue<string>() ==
                            "mortal_wound_treatment_consent");
                        consent!["status"] = "withdrawn";
                    });
                    break;
                case "resource_unavailable":
                    WriteCanonicalPlayerHealthAuthority(FileSystem, current: 0);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
            }

            PrepareFreshSnapshot(mutation);
        }

        internal void MutateLiveCarrierDisplayOnly()
        {
            var carrier = JsonNode.Parse(File.ReadAllText(FileSystem.ResolvePath(
                TargetCarrierPath)))!.AsObject();
            FindPersistedWound(carrier)["display"]!["name"] = "Persisted mutation";
            File.WriteAllText(FileSystem.ResolvePath(TargetCarrierPath), carrier.ToJsonString());
        }

        internal string ReadPersistedWoundDisplayName()
        {
            var carrier = JsonNode.Parse(File.ReadAllText(FileSystem.ResolvePath(
                TargetCarrierPath)))!.AsObject();
            return FindPersistedWound(carrier)["display"]!["name"]!.GetValue<string>();
        }

        private JsonObject FindPersistedWound(JsonObject carrier)
        {
            if (string.Equals(TargetKind, "player", StringComparison.Ordinal))
                return carrier["activeWounds"]![0]!.AsObject();
            if (string.Equals(TargetKind, "combatant_member", StringComparison.Ordinal))
                return carrier["alliesData"]![0]!["members"]![0]!["activeWounds"]![0]!.AsObject();
            return carrier["enemiesData"]![0]!["activeWounds"]![0]!.AsObject();
        }

        internal void PrepareFreshSnapshot(string label, bool preserveRequestId = false)
        {
            Lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            var request = JsonNode.Parse(File.ReadAllText(FileSystem.ResolvePath(
                LiveTurnPreparationService.TurnRequestPath)))!.AsObject();
            var prepared = new LiveTurnPreparationService(FileSystem).PrepareAsync(
                new LiveTurnPreparationOptions
                {
                    SessionId = request["sessionId"]!.GetValue<string>(),
                    RequestId = request["requestId"]!.GetValue<string>() +
                        (preserveRequestId ? string.Empty : "_" + label),
                    TurnNumber = request["turnNumber"]!.GetValue<int>(),
                    PlayerAction = "Refresh accepted-state authority for " + label + ".",
                    CurrentRealm = "Mortal World",
                    PreGeneratedDices1d20 = request["preGeneratedDices1d20"]!.AsArray()
                        .Select(static value => value!.GetValue<int>())
                        .ToArray()
                }).GetAwaiter().GetResult();
            Assert.Equal(request["turnNumber"]!.GetValue<int>(), prepared.TurnNumber);
            Lease = FileSystem.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
        }

        internal AcceptedStateFixture AttachColdRoot(
            string root,
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease lease) =>
            new(
                root,
                fileSystem,
                lease,
                TreatmentContext,
                WoundId,
                TargetKind,
                TargetId,
                TargetCarrierPath);

        internal void AssertCanonicalScenarioAuthority(
            object acceptedState,
            ResolverScenario scenario)
        {
            var binding = Assert.IsType<WoundAcceptedTurnBinding>(
                ReadAcceptedStateMember(acceptedState, "Binding"));
            var acceptedEvent = Assert.Single(binding.AcceptedEvents);
            Assert.Equal(scenario.EventRef, acceptedEvent.EventRef);
            Assert.Equal("accepted_turn", acceptedEvent.Kind);
            Assert.Equal("turn_42", acceptedEvent.AuthorityId);
            Assert.Equal(
                WoundAcceptedEventSetFingerprint.Compute(binding.AcceptedEvents),
                binding.AcceptedEventsFingerprint);

            var manifest = JsonNode.Parse(File.ReadAllText(FileSystem.ResolvePath(
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath)))!.AsObject();
            Assert.Equal(
                manifest["manifestPayloadHash"]!.GetValue<string>(),
                binding.SnapshotToken);

            var effects = EffectMechanicsSnapshot.LoadAsync(FileSystem, Lease)
                .GetAwaiter()
                .GetResult();
            Assert.True(effects.IsAccepted, DescribeIssues(effects.Issues));
            if (scenario.RollMode is "advantage" or "disadvantage")
            {
                Assert.Contains(effects.Components, component =>
                    string.Equals(component.EffectId,
                        "effect_roll_modifier_" + scenario.RollMode,
                        StringComparison.Ordinal) &&
                    string.Equals(component.Profile, "roll_modifier", StringComparison.Ordinal));
            }
            if (scenario.SeedFateEffectId is not null)
            {
                Assert.Contains(effects.Effects, effect => string.Equals(
                    effect.EffectId,
                    scenario.SeedFateEffectId,
                    StringComparison.Ordinal));
                Assert.Contains(effects.Effects, effect => string.Equals(
                    effect.EffectId,
                    "effect_fate_shield_newer",
                    StringComparison.Ordinal));
            }
        }

        internal void AssertUnchangedT060RequirementResolution(WoundTreatmentRoute route)
        {
            var authorityType = typeof(WoundMaterializationContract).Assembly.GetType(
                "BookOfEternityClient.Services.MortalWoundTreatmentAuthority",
                throwOnError: false,
                ignoreCase: false);
            Assert.NotNull(authorityType);
            var acceptedState = GetAcceptedState();
            var context = ReadAcceptedStateMember(acceptedState, "RequirementContext");
            var snapshot = ReadAcceptedStateMember(acceptedState, "RequirementSnapshot");
            var result = Invoke(ExactStaticMethod(authorityType, "ResolveRequirements", 3),
                new[] { (object)route, context, snapshot });
            Assert.True(Assert.IsType<bool>(ReadRequiredProperty(result, "Success")),
                "The canonical procedure fixture must satisfy the unchanged T060 authority.");
        }

        internal WoundMaterializationEnvelope ReadCurrentWound()
            => AssertCurrentWoundCoordinate(TargetKind, TargetId, TargetCarrierPath);

        internal WoundMaterializationEnvelope AssertCurrentWoundCoordinate(
            string expectedTargetKind,
            string expectedTargetId,
            string expectedCarrierPath)
        {
            var catalog = WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(
                PlayerWounds: ReadOptionalJsonObject(WoundCarrierCatalog.PlayerPath),
                NpcWounds: ReadOptionalJsonObject(WoundCarrierCatalog.NpcPath),
                EnemyCombatants: ReadOptionalJsonObject(WoundCarrierCatalog.EnemiesPath),
                AllyCombatants: ReadOptionalJsonObject(WoundCarrierCatalog.AlliesPath),
                AfterlifeProfiles: null));
            Assert.Empty(catalog.Issues);
            Assert.True(catalog.TryResolveOne(WoundId, out var occurrence));
            Assert.Equal(expectedTargetKind, occurrence.Coordinate.OwnerKind);
            Assert.Equal(expectedTargetId, occurrence.Coordinate.OwnerId);
            Assert.Equal(expectedCarrierPath, occurrence.Coordinate.CarrierPath);
            Assert.Equal(expectedCarrierPath, occurrence.FilePath);
            return occurrence.Wound;
        }

        internal static string ResolveTargetCarrierPath(string targetKind) => targetKind switch
        {
            "player" => WoundCarrierCatalog.PlayerPath,
            "npc" => WoundCarrierCatalog.NpcPath,
            "combatant" => WoundCarrierCatalog.EnemiesPath,
            "combatant_member" => WoundCarrierCatalog.AlliesPath,
            _ => throw new ArgumentOutOfRangeException(
                nameof(targetKind),
                targetKind,
                "The T061 resolver fixture supports canonical player and combat targets only.")
        };

        private JsonObject? ReadOptionalJsonObject(string relativePath)
        {
            var path = FileSystem.ResolvePath(relativePath);
            return File.Exists(path)
                ? JsonNode.Parse(File.ReadAllText(path))!.AsObject()
                : null;
        }

        internal WoundHistoryParseResult ReadCurrentHistory()
        {
            var parsed = WoundHistoryState.Parse(
                File.ReadAllText(FileSystem.ResolvePath(WoundHistoryState.HistoryPath)),
                WoundHistoryState.HistoryPath);
            Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
            Assert.NotNull(parsed.State);
            return parsed;
        }

        internal int ReadPlayerItemCount(string itemId)
        {
            var root = JsonNode.Parse(File.ReadAllText(FileSystem.ResolvePath(
                "game_state/inventory/items.json")))!.AsObject();
            var items = root["items"]!.AsArray()
                .Where(candidate => string.Equals(
                    candidate!["itemId"]!.GetValue<string>(),
                    itemId,
                    StringComparison.Ordinal))
                .ToArray();
            Assert.True(items.Length <= 1);
            return items.Length == 0 ? 0 : items[0]!["count"]!.GetValue<int>();
        }

        internal int ReadNpcItemCount(string itemId)
        {
            var root = JsonNode.Parse(File.ReadAllText(FileSystem.ResolvePath(
                "game_state/npcs/npc_core.json")))!.AsObject();
            var npc = Assert.Single(root["NPCsInScene"]!.AsArray(), candidate => string.Equals(
                candidate!["NPCId"]!.GetValue<string>(),
                "field_medic_01",
                StringComparison.Ordinal));
            var items = npc!["inventory"]!.AsArray()
                .Where(candidate => string.Equals(
                    candidate!["itemId"]!.GetValue<string>(),
                    itemId,
                    StringComparison.Ordinal))
                .ToArray();
            Assert.True(items.Length <= 1);
            return items.Length == 0 ? 0 : items[0]!["count"]!.GetValue<int>();
        }

        internal string[] ReadActivePlayerEffectIds()
        {
            var root = JsonNode.Parse(File.ReadAllText(FileSystem.ResolvePath(
                EffectCarrierCatalog.PlayerPath)))!.AsObject();
            return root["activeEffects"]!.AsArray()
                .Select(effect => effect!["effectId"]!.GetValue<string>())
                .ToArray();
        }

        internal void AssertItemIdentityIndexValid()
        {
            var parsed = MortalItemIdentityState.Parse(File.ReadAllText(FileSystem.ResolvePath(
                MortalItemIdentityState.StatePath)));
            Assert.Empty(parsed.Issues);
        }

        internal void PrepareNextTurn(
            int turn,
            long worldMinute,
            string operationLabel,
            IReadOnlyList<int>? acceptedDice = null)
        {
            Lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            File.WriteAllText(
                FileSystem.ResolvePath("game_state/world/world_time.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["currentTimeInMinutes"] = worldMinute
                }.ToJsonString());
            var prepared = new LiveTurnPreparationService(FileSystem).PrepareAsync(
                new LiveTurnPreparationOptions
                {
                    SessionId = "session_t061",
                    RequestId = $"request_t061_{turn}_{operationLabel}",
                    TurnNumber = turn,
                    PlayerAction = $"Continue mortal wound treatment: {operationLabel}.",
                    CurrentRealm = "Mortal World",
                    PreGeneratedDices1d20 = acceptedDice?.ToArray() ?? new[] { 17, 4, 1, 20 }
                }).GetAwaiter().GetResult();
            Assert.Equal(turn, prepared.TurnNumber);
            Lease = FileSystem.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
        }

        internal void RemoveCurrentPlayerDose()
        {
            var inventory = JsonNode.Parse(File.ReadAllText(FileSystem.ResolvePath(
                "game_state/inventory/items.json")))!.AsObject();
            inventory["items"] = new JsonArray();
            File.WriteAllText(
                FileSystem.ResolvePath("game_state/inventory/items.json"),
                inventory.ToJsonString());
            var npc = JsonNode.Parse(File.ReadAllText(FileSystem.ResolvePath(
                "game_state/npcs/npc_core.json")))!.AsObject();
            var sterile = npc["NPCsInScene"]![0]!["inventory"]![0]!.AsObject();
            File.WriteAllText(
                FileSystem.ResolvePath("game_state/inventory/item_identity_index.json"),
                MortalItemTestFixture.CreateIndexForCarrier(
                    sterile,
                    "npc_inventory",
                    "field_medic_01").ToJsonString());
        }

        internal void RestartForReplay()
        {
            Lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            FileSystem = new FileSystemManager(Root, NullLogger<FileSystemManager>.Instance);
            FileSystem.EnsureDirectoryStructure();
            Lease = FileSystem.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            Lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Directory.Delete(Root, recursive: true);
        }
    }
}
