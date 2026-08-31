using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public void HistoryPersistenceContract_TypedResultRoundTripsAndClassifiesReplayBeforeLiveState()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_history_replay",
            scenario.RouteId);
        var history = CreatePersistedTreatmentHistory(flow, "history_replay");
        var canonical = WoundHistoryState.SerializeCanonical(history.State!);

        var serialized = JsonNode.Parse(canonical)!.AsObject();
        var result = Assert.Single(serialized["transitions"]!.AsArray(), row =>
            string.Equals(row!["kind"]!.GetValue<string>(), "treat",
                StringComparison.Ordinal))!["transitionResult"]!.AsObject();
        Assert.Equal(
            new[]
            {
                "attemptDisposition", "consumptionTrigger", "declaredResult", "interruption",
                "kind", "mode", "modeEvidence", "receiptFingerprint", "requestAuthority",
                "resolutionAuthorityFingerprint", "resultCategory", "resultFingerprint",
                "routeCompletion", "routeFingerprint", "routeId", "selectedOutcomeIndex"
            },
            result.Select(static pair => pair.Key)
                .OrderBy(static value => value, StringComparer.Ordinal));

        var reparsed = WoundHistoryState.Parse(canonical, WoundHistoryState.HistoryPath);
        Assert.True(reparsed.IsValid, DescribeIssues(reparsed.Issues));
        Assert.Equal(canonical, WoundHistoryState.SerializeCanonical(reparsed.State!));
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var coordinates = request.Coordinates;

        var exact = reparsed.ProbeTreatmentAttempt(
            coordinates.OperationKey,
            coordinates.AttemptId,
            request.RequestFingerprint);
        Assert.Equal("ExactReplay", exact.Status);
        Assert.Empty(exact.Issues);
        Assert.NotNull(exact.Request);
        Assert.NotNull(exact.Receipt);
        Assert.NotSame(request, exact.Request);
        Assert.Equal(CanonicalValue(request), CanonicalValue(exact.Request));
        AssertClosedTreatmentReceipt(exact.Receipt!, exact.Request!, flow.Resolution);

        var notFound = reparsed.ProbeTreatmentAttempt(
            "operation_t067_history_absent",
            "attempt_t067_history_absent",
            "sha256:" + new string('a', 64));
        Assert.Equal("NotFound", notFound.Status);
        Assert.Empty(notFound.Issues);
        Assert.Null(notFound.Request);
        Assert.Null(notFound.Receipt);

        var conflict = reparsed.ProbeTreatmentAttempt(
            coordinates.OperationKey,
            coordinates.AttemptId,
            "sha256:" + new string('b', 64));
        Assert.Equal("Conflict", conflict.Status);
        Assert.NotEmpty(conflict.Issues);
        Assert.Null(conflict.Request);
        Assert.Null(conflict.Receipt);
    }

    [Fact]
    public void HistoryPersistenceContract_ResultTamperMakesHistoryInvalidAndDominatesProbe()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_history_tamper",
            scenario.RouteId);
        var history = CreatePersistedTreatmentHistory(flow, "history_tamper");
        var root = JsonNode.Parse(WoundHistoryState.SerializeCanonical(history.State!))!
            .AsObject();
        var treatment = Assert.Single(root["transitions"]!.AsArray(), row =>
            string.Equals(row!["kind"]!.GetValue<string>(), "treat",
                StringComparison.Ordinal))!.AsObject();
        treatment["transitionResult"]!["receiptFingerprint"] =
            "sha256:" + new string('c', 64);
        treatment["transitionResult"]!["unknownResultAuthority"] = true;

        var invalid = WoundHistoryState.Parse(
            root.ToJsonString(),
            WoundHistoryState.HistoryPath);
        Assert.False(invalid.IsValid);
        Assert.Null(invalid.State);
        Assert.NotEmpty(invalid.Issues);

        var probe = invalid.ProbeTreatmentAttempt(
            "operation_t067_unrelated",
            "attempt_t067_unrelated",
            "sha256:" + new string('d', 64));
        Assert.Equal("InvalidHistory", probe.Status);
        Assert.Equal(invalid.Issues, probe.Issues);
        Assert.Null(probe.Request);
        Assert.Null(probe.Receipt);
    }

    [Theory]
    [InlineData(
        "procedure_normal_uses_lowest_free_die",
        "procedure",
        "CreateProcedureAttempt")]
    [InlineData(
        "course_first_milestone_is_ready_at_inclusive_due_time",
        "course",
        "CreateCourseMilestoneAttempt")]
    [InlineData(
        "guaranteed_current_capability_proof_stabilizes",
        "guaranteed",
        "CreateGuaranteedAttempt")]
    public void HistoryPersistenceContract_ModeReducerReturnsExactReplayBeforeLiveState(
        string scenarioName,
        string mode,
        string reducerName)
    {
        var scenario = CreateScenario(scenarioName, mode);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            mode,
            scenario.OperationKey + "_reducer_replay",
            scenario.RouteId);
        var history = CreatePersistedTreatmentHistory(flow, mode + "_reducer_replay");
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var probe = history.ProbeTreatmentAttempt(
            request.Coordinates.OperationKey,
            request.Coordinates.AttemptId,
            request.RequestFingerprint);
        Assert.Equal("ExactReplay", probe.Status);
        Assert.NotNull(probe.Request);
        Assert.NotNull(probe.Receipt);

        var result = Invoke(
            ExactStaticMethod(RequireOutcomeResolver(), reducerName, 4),
            new object?[] { probe.Request, history, null, null });

        Assert.Equal(
            "ExactReplay",
            Convert.ToString(ReadRequiredProperty(result, "Disposition")));
        Assert.Empty(AsObjects(ReadRequiredProperty(result, "Issues")));
        Assert.Null(ReadPropertyAllowingNull(result, "Resolution"));
        Assert.Equal(
            CanonicalValue(probe.Receipt),
            CanonicalValue(ReadRequiredProperty(result, "ReplayReceipt")));
    }

    [Theory]
    [InlineData("CreateProcedureAttempt")]
    [InlineData("CreateCourseMilestoneAttempt")]
    [InlineData("CreateGuaranteedAttempt")]
    public void HistoryPersistenceContract_InvalidHistoryDominatesMissingFreshAuthority(
        string reducerName)
    {
        var invalid = WoundHistoryState.Parse(
            "{}",
            WoundHistoryState.HistoryPath);
        Assert.False(invalid.IsValid);
        Assert.NotEmpty(invalid.Issues);

        var result = Invoke(
            ExactStaticMethod(RequireOutcomeResolver(), reducerName, 4),
            new object?[] { null, invalid, null, null });

        Assert.Equal(
            "Rejected",
            Convert.ToString(ReadRequiredProperty(result, "Disposition")));
        Assert.Equal(
            invalid.Issues.Select(CanonicalValue),
            AsObjects(ReadRequiredProperty(result, "Issues"))
                .Select(Assert.IsType<ValidationIssue>)
                .Select(CanonicalValue));
        Assert.Null(ReadPropertyAllowingNull(result, "Resolution"));
        Assert.Null(ReadPropertyAllowingNull(result, "ReplayReceipt"));
    }

    [Theory]
    [InlineData("CreateProcedureAttempt")]
    [InlineData("CreateCourseMilestoneAttempt")]
    [InlineData("CreateGuaranteedAttempt")]
    public void HistoryPersistenceContract_ReusedCoordinateConflictsBeforeLiveAuthority(
        string reducerName)
    {
        const string operationKey = "operation_t067_reducer_replay_conflict";
        var acceptedScenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        var changedScenario = CreateScenario(
            "procedure_disadvantage_uses_two_contiguous_dice",
            "procedure");
        using var acceptedFixture = AcceptedStateFixture.Create(acceptedScenario);
        using var changedFixture = AcceptedStateFixture.Create(changedScenario);
        var accepted = ResolveCurrentTreatment(
            acceptedFixture,
            "procedure",
            operationKey,
            acceptedScenario.RouteId);
        var changed = ResolveCurrentTreatment(
            changedFixture,
            "procedure",
            operationKey,
            changedScenario.RouteId);
        Assert.NotEqual(
            ReadRequiredProperty(accepted.Request, "RequestFingerprint"),
            ReadRequiredProperty(changed.Request, "RequestFingerprint"));
        var history = CreatePersistedTreatmentHistory(
            accepted,
            "reducer_replay_conflict");

        var result = Invoke(
            ExactStaticMethod(RequireOutcomeResolver(), reducerName, 4),
            new object?[] { changed.Request, history, null, null });

        Assert.Equal(
            "Conflict",
            Convert.ToString(ReadRequiredProperty(result, "Disposition")));
        Assert.NotEmpty(AsObjects(ReadRequiredProperty(result, "Issues"))
            .Select(Assert.IsType<ValidationIssue>));
        Assert.Null(ReadPropertyAllowingNull(result, "Resolution"));
        Assert.Null(ReadPropertyAllowingNull(result, "ReplayReceipt"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HistoryPersistenceReviewContract_CourseMilestonesRequireContiguousTopologyBeforeReplay(
        bool retainFirstMilestone)
    {
        var topology = retainFirstMilestone ? "missing_middle" : "missing_first";
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var first = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_history_topology_1_" + topology,
            scenario.RouteId);
        InstallPublishedCourseStart(fixture, first);
        var firstHistory = fixture.ReadCurrentHistory();

        fixture.RestartForReplay();
        fixture.PrepareNextTurn(
            43,
            480,
            "history_topology_ordinal_2_" + topology);
        var second = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_history_topology_2_" + topology,
            scenario.RouteId);

        WoundHistoryParseResult discontinuous;
        TreatmentFlow replayCandidate;
        if (!retainFirstMilestone)
        {
            discontinuous = CreatePersistedTreatmentHistory(
                second,
                "history_topology_missing_1");
            replayCandidate = second;
        }
        else
        {
            InstallPublishedActiveCourseMilestone(
                fixture,
                second,
                "history_topology_ordinal_2_before_gap");
            fixture.RestartForReplay();
            fixture.PrepareNextTurn(44, 960, "history_topology_ordinal_3");
            var third = ResolveCurrentTreatment(
                fixture,
                "course",
                scenario.OperationKey + "_history_topology_3",
                scenario.RouteId);
            var thirdRequest = Assert.IsType<MortalWoundTreatmentAttemptRequest>(
                third.Request);
            var firstRows = Assert.IsType<WoundHistoryState>(firstHistory.State)
                .Transitions;
            discontinuous = WoundHistoryState.CreateValidated(
                4,
                new[]
                {
                    firstRows[0],
                    firstRows[1] with
                    {
                        AfterFingerprint =
                            thirdRequest.Coordinates.ExpectedBeforeFingerprint
                    },
                    CreatePersistedTreatmentTransition(
                        third,
                        "history_topology_missing_2",
                        ordinal: 3,
                        woundTransitionOrdinal: 3)
                });
            replayCandidate = third;
        }

        Assert.False(discontinuous.IsValid);
        Assert.Null(discontinuous.State);
        Assert.Contains(discontinuous.Issues, issue => string.Equals(
            issue.Code,
            "wound_history_treatment_course_ordinal_discontinuity",
            StringComparison.Ordinal));
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(
            replayCandidate.Request);
        var probe = discontinuous.ProbeTreatmentAttempt(
            request.Coordinates.OperationKey,
            request.Coordinates.AttemptId,
            request.RequestFingerprint);
        Assert.Equal("InvalidHistory", probe.Status);
        Assert.Equal(discontinuous.Issues, probe.Issues);
        Assert.Null(probe.Request);
        Assert.Null(probe.Receipt);
    }

    [Fact]
    public void HistoryPersistenceReviewContract_AcceptedTreatmentEventRefIsGloballyUniqueBeforeReplay()
    {
        var firstScenario = CreateScenarioWithHistoryWound(
            "procedure_normal_uses_lowest_free_die",
            "wound_t067_history_event_first");
        var secondScenario = CreateScenarioWithHistoryWound(
            "procedure_disadvantage_uses_two_contiguous_dice",
            "wound_t067_history_event_second");
        using var firstFixture = AcceptedStateFixture.Create(firstScenario);
        using var secondFixture = AcceptedStateFixture.Create(secondScenario);
        var first = ResolveCurrentTreatment(
            firstFixture,
            "procedure",
            "operation_t067_history_event_first",
            firstScenario.RouteId);
        var second = ResolveCurrentTreatment(
            secondFixture,
            "procedure",
            "operation_t067_history_event_second",
            secondScenario.RouteId);
        var firstRequest = Assert.IsType<MortalWoundTreatmentAttemptRequest>(
            first.Request);
        var secondRequest = Assert.IsType<MortalWoundTreatmentAttemptRequest>(
            second.Request);
        Assert.Equal(
            firstRequest.Coordinates.EventRef,
            secondRequest.Coordinates.EventRef);

        var duplicate = CreatePersistedTreatmentHistory(
            (first, "history_event_first"),
            (second, "history_event_second"));

        Assert.False(duplicate.IsValid);
        Assert.Null(duplicate.State);
        Assert.Contains(duplicate.Issues, issue => string.Equals(
            issue.Code,
            "wound_history_duplicate_treatment_event_ref",
            StringComparison.Ordinal));
        foreach (var request in new[] { firstRequest, secondRequest })
        {
            var probe = duplicate.ProbeTreatmentAttempt(
                request.Coordinates.OperationKey,
                request.Coordinates.AttemptId,
                request.RequestFingerprint);
            Assert.Equal("InvalidHistory", probe.Status);
            Assert.Null(probe.Request);
            Assert.Null(probe.Receipt);
        }
    }

    [Fact]
    public void HistoryPersistenceContract_CatalogUsesOrdinalHistoryAndCoalescesExactCommandCopy()
    {
        var firstScenario = CreateScenarioWithHistoryWound(
            "procedure_normal_uses_lowest_free_die",
            "wound_t067_history_first");
        var secondScenario = CreateScenarioWithHistoryWound(
            "procedure_disadvantage_uses_two_contiguous_dice",
            "wound_t067_history_second",
            turn: 43);
        using var firstFixture = AcceptedStateFixture.Create(firstScenario);
        using var secondFixture = AcceptedStateFixture.Create(secondScenario);
        var first = ResolveCurrentTreatment(
            firstFixture,
            "procedure",
            "operation_t067_history_first",
            firstScenario.RouteId);
        var second = ResolveCurrentTreatment(
            secondFixture,
            "procedure",
            "operation_t067_history_second",
            secondScenario.RouteId);
        var history = CreatePersistedTreatmentHistory(
            (first, "history_first"),
            (second, "history_second"));

        var historyOnly = AssertValidPersistedCatalog(
            ParsePersistedRequestCatalog(null, null, history),
            "ordinal treatment history catalog");
        Assert.Equal(2, historyOnly.Length);
        Assert.Equal(CanonicalValue(first.Request), CanonicalValue(historyOnly[0]));
        Assert.Equal(CanonicalValue(second.Request), CanonicalValue(historyOnly[1]));

        var command = ComposeTreatmentCommand(
            first,
            "The exact accepted request remains present until command cleanup.");
        var coalesced = AssertValidPersistedCatalog(
            ParsePersistedRequestCatalog(command.Root, null, history),
            "history plus exact command copy");
        Assert.Equal(2, coalesced.Length);
        Assert.Equal(CanonicalValue(first.Request), CanonicalValue(coalesced[0]));
        Assert.Equal(CanonicalValue(second.Request), CanonicalValue(coalesced[1]));
    }

    [Fact]
    public void HistoryPersistenceContract_CatalogRejectsCoordinateReuseWithDifferentRequest()
    {
        const string reusedOperationKey = "operation_t067_history_collision";
        var acceptedScenario = CreateScenarioWithHistoryWound(
            "procedure_normal_uses_lowest_free_die",
            "wound_t067_history_accepted");
        var foreignScenario = CreateScenarioWithHistoryWound(
            "procedure_disadvantage_uses_two_contiguous_dice",
            "wound_t067_history_foreign");
        using var acceptedFixture = AcceptedStateFixture.Create(acceptedScenario);
        using var foreignFixture = AcceptedStateFixture.Create(foreignScenario);
        var accepted = ResolveCurrentTreatment(
            acceptedFixture,
            "procedure",
            reusedOperationKey,
            acceptedScenario.RouteId);
        var foreign = ResolveCurrentTreatment(
            foreignFixture,
            "procedure",
            reusedOperationKey,
            foreignScenario.RouteId);
        Assert.NotEqual(
            ReadRequiredProperty(accepted.Request, "RequestFingerprint"),
            ReadRequiredProperty(foreign.Request, "RequestFingerprint"));
        var history = CreatePersistedTreatmentHistory(accepted, "history_accepted");
        var foreignCommand = ComposeTreatmentCommand(
            foreign,
            "A changed request cannot reuse an accepted history coordinate.");

        AssertInvalidPersistedCatalog(
            ParsePersistedRequestCatalog(foreignCommand.Root, null, history),
            "history coordinate collision");
    }

    [Fact]
    public void HistoryPersistenceContract_CommandComposerRejectsTextBeyondParserLimit()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_scene_bound",
            scenario.RouteId);
        var binding = Assert.IsType<WoundAcceptedTurnBinding>(
            ReadAcceptedStateMember(flow.AcceptedState, "Binding"));
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);

        var accepted = WoundResponseInputComposer.ComposeMortalWoundTreatmentCommandRoot(
            binding,
            resolution,
            new string('a', 8_192));
        Assert.True(WoundResponseInputComposer.ParseCommandRoot(
            System.Text.Json.JsonSerializer.SerializeToElement(accepted)).Success);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WoundResponseInputComposer.ComposeMortalWoundTreatmentCommandRoot(
                binding,
                resolution,
                new string('a', 8_193)));
    }

    private static WoundHistoryParseResult CreatePersistedTreatmentHistory(
        TreatmentFlow flow,
        string transitionSuffix) =>
        CreatePersistedTreatmentHistory((flow, transitionSuffix));

    private static WoundHistoryParseResult CreatePersistedTreatmentHistory(
        params (TreatmentFlow Flow, string TransitionSuffix)[] accepted)
    {
        var rows = new List<WoundHistoryTransition>(accepted.Length * 2);
        foreach (var entry in accepted)
        {
            var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(entry.Flow.Request);
            var resolution = Assert.IsType<MortalWoundTreatmentResolution>(
                entry.Flow.Resolution);
            var before = entry.Flow.Before;
            var beforeFingerprint = WoundIdentityState.ComputeSemanticFingerprint(before);
            var createSummary = "The accepted wound existed before treatment preparation.";
            rows.Add(new WoundHistoryTransition(
                before.LastTransition.TransitionId,
                before.WoundId,
                rows.Count + 1,
                1,
                "create",
                before.Origin.CreatedAtTurn,
                before.Origin.EventRef,
                "operation_t067_create_" + entry.TransitionSuffix,
                WoundHistoryState.ComputeNonexistentBeforeFingerprint(before.WoundId),
                beforeFingerprint,
                request.Coordinates.AcceptedStateFingerprint,
                null,
                null,
                null,
                null,
                null,
                WoundHistoryState.ComputeOutputFingerprint(
                    "operation_t067_create_" + entry.TransitionSuffix,
                    before.Origin.EventRef,
                    createSummary),
                createSummary,
                false));

            var treatmentSummary = "The accepted treatment result is retained for replay.";
            rows.Add(new WoundHistoryTransition(
                "wound_transition_t067_" + entry.TransitionSuffix,
                before.WoundId,
                rows.Count + 1,
                2,
                "treat",
                request.Coordinates.Turn,
                request.Coordinates.EventRef,
                request.Coordinates.OperationKey,
                request.Coordinates.ExpectedBeforeFingerprint,
                request.Coordinates.ExpectedBeforeFingerprint,
                request.RequestFingerprint,
                request.Coordinates.AttemptId,
                resolution.CourseId,
                resolution.CourseMilestoneOrdinal,
                null,
                null,
                WoundHistoryState.ComputeOutputFingerprint(
                    request.Coordinates.OperationKey,
                    request.Coordinates.EventRef,
                    treatmentSummary),
                treatmentSummary,
                false,
                MortalWoundTreatmentPersistedResult.Create(resolution)));
        }

        return WoundHistoryState.CreateValidated(rows.Count + 1, rows);
    }

    private static WoundHistoryTransition CreatePersistedTreatmentTransition(
        TreatmentFlow flow,
        string transitionSuffix,
        int ordinal,
        int woundTransitionOrdinal)
    {
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        var summary = "The accepted treatment result is retained for replay.";
        return new WoundHistoryTransition(
            "wound_transition_t067_" + transitionSuffix,
            flow.Before.WoundId,
            ordinal,
            woundTransitionOrdinal,
            "treat",
            request.Coordinates.Turn,
            request.Coordinates.EventRef,
            request.Coordinates.OperationKey,
            request.Coordinates.ExpectedBeforeFingerprint,
            request.Coordinates.ExpectedBeforeFingerprint,
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
    }

    private static ResolverScenario CreateScenarioWithHistoryWound(
        string scenarioName,
        string woundId,
        int turn = 42)
    {
        var source = CreateScenario(scenarioName, "procedure");
        var before = WoundContractTestData.CreateActiveWound(woundId: woundId);
        before["consequences"]!["ownedEffectSources"]!["definitions"]!
            .AsArray().RemoveAt(1);
        before["consequences"]!["ownedEffectSources"]!["rootBindings"]!
            .AsArray().RemoveAt(1);
        before["consequences"]!["entries"]!.AsArray().RemoveAt(1);
        before["consequences"]!["slotsUsed"] = 1;
        before["treatment"] = source.Before["treatment"]!.DeepClone();
        before["lastTransition"]!["transitionId"] =
            "wound_transition_t067_create_" + woundId;
        var acceptedState = source.AcceptedState.DeepClone().AsObject();
        acceptedState["woundId"] = woundId;
        acceptedState["turn"] = turn;
        acceptedState["requestId"] = "request_t061_history_" + turn;
        acceptedState["eventRef"] = $"turn_{turn}:accepted_effect";
        return source with
        {
            Before = before,
            AcceptedState = acceptedState,
            History = WoundContractTestData.CreateHistory(),
            EventRef = $"turn_{turn}:accepted_effect"
        };
    }
}
