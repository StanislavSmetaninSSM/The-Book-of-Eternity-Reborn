using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundCourseModeAuthorityResult(
    string Disposition,
    IReadOnlyList<ValidationIssue> Issues,
    MortalWoundCourseModeAuthority? Authority);

internal sealed class MortalWoundCourseStartAuthority
{
    private const string AuthorityDomain =
        "book_of_eternity.mortal_wound_treatment.course_start_authority";

    [System.Text.Json.Serialization.JsonConstructor]
    private MortalWoundCourseStartAuthority(
        string courseId,
        string routeId,
        string routeFingerprint,
        WoundMaterializationEnvelope startingWound,
        string startingWoundFingerprint,
        long startedAtGameTimeMinutes,
        string acceptedStateFingerprint,
        string coordinatesFingerprint,
        string authorityFingerprint)
    {
        CourseId = courseId;
        RouteId = routeId;
        RouteFingerprint = routeFingerprint;
        StartingWound = startingWound;
        StartingWoundFingerprint = startingWoundFingerprint;
        StartedAtGameTimeMinutes = startedAtGameTimeMinutes;
        AcceptedStateFingerprint = acceptedStateFingerprint;
        CoordinatesFingerprint = coordinatesFingerprint;
        AuthorityFingerprint = authorityFingerprint;
    }

    public string CourseId { get; }
    public string RouteId { get; }
    public string RouteFingerprint { get; }
    public WoundMaterializationEnvelope StartingWound { get; }
    public string StartingWoundFingerprint { get; }
    public long StartedAtGameTimeMinutes { get; }
    public string AcceptedStateFingerprint { get; }
    public string CoordinatesFingerprint { get; }
    public string AuthorityFingerprint { get; }

    internal MortalWoundCourseStartAuthority RestoreDetachedStartingWound(
        WoundMaterializationEnvelope startingWound)
    {
        ArgumentNullException.ThrowIfNull(startingWound);
        return new MortalWoundCourseStartAuthority(
            CourseId,
            RouteId,
            RouteFingerprint,
            startingWound,
            StartingWoundFingerprint,
            StartedAtGameTimeMinutes,
            AcceptedStateFingerprint,
            CoordinatesFingerprint,
            AuthorityFingerprint);
    }

    internal static MortalWoundCourseStartAuthority Create(
        string courseId,
        string routeId,
        string routeFingerprint,
        WoundMaterializationEnvelope startingWound,
        long startedAtGameTimeMinutes,
        MortalWoundTreatmentAttemptCoordinates coordinates)
    {
        var startingWoundFingerprint =
            WoundIdentityState.ComputeSemanticFingerprint(startingWound);
        var authorityFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                AuthorityDomain,
                "1",
                courseId,
                routeId,
                routeFingerprint,
                WoundMaterializationContract.SerializeCanonical(startingWound),
                startingWoundFingerprint,
                startedAtGameTimeMinutes.ToString(CultureInfo.InvariantCulture),
                coordinates.AcceptedStateFingerprint,
                coordinates.CoordinatesFingerprint
            });
        return new MortalWoundCourseStartAuthority(
            courseId,
            routeId,
            routeFingerprint,
            startingWound,
            startingWoundFingerprint,
            startedAtGameTimeMinutes,
            coordinates.AcceptedStateFingerprint,
            coordinates.CoordinatesFingerprint,
            authorityFingerprint);
    }
}

internal sealed class MortalWoundCourseModeAuthority : MortalWoundTreatmentModeAuthority
{
    private const string CourseIdentityDomain =
        "book_of_eternity.mortal_wound_treatment.course_identity";
    private const string CourseCoordinateDomain =
        "book_of_eternity.mortal_wound_treatment.course_coordinates";
    private const string AuthorityDomain =
        "book_of_eternity.mortal_wound_treatment.course_mode_authority";
    [System.Text.Json.Serialization.JsonConstructor]
    private MortalWoundCourseModeAuthority(
        MortalWoundGameTimeAuthority gameTimeAuthority,
        string courseId,
        int milestoneOrdinal,
        long dueAtGameTimeMinutes,
        long deadlineAtGameTimeMinutes,
        string windowDisposition,
        MortalWoundCourseStartAuthority courseStartAuthority,
        string courseCoordinateFingerprint,
        string coordinatesFingerprint,
        string acceptedStateFingerprint,
        string authorityFingerprint)
    {
        GameTimeAuthority = gameTimeAuthority;
        CourseId = courseId;
        MilestoneOrdinal = milestoneOrdinal;
        DueAtGameTimeMinutes = dueAtGameTimeMinutes;
        DeadlineAtGameTimeMinutes = deadlineAtGameTimeMinutes;
        WindowDisposition = windowDisposition;
        CourseStartAuthority = courseStartAuthority;
        CourseCoordinateFingerprint = courseCoordinateFingerprint;
        CoordinatesFingerprint = coordinatesFingerprint;
        AcceptedStateFingerprint = acceptedStateFingerprint;
        AuthorityFingerprint = authorityFingerprint;
    }

    public MortalWoundGameTimeAuthority GameTimeAuthority { get; }
    public string CourseId { get; }
    public int MilestoneOrdinal { get; }
    public long DueAtGameTimeMinutes { get; }
    public long DeadlineAtGameTimeMinutes { get; }
    public string WindowDisposition { get; }
    public MortalWoundCourseStartAuthority CourseStartAuthority { get; }
    public string CourseCoordinateFingerprint { get; }
    public string CoordinatesFingerprint { get; }
    public string AcceptedStateFingerprint { get; }
    public string AuthorityFingerprint { get; }

    internal MortalWoundCourseModeAuthority RestoreDetachedStartingWound(
        WoundMaterializationEnvelope startingWound) => new(
        GameTimeAuthority,
        CourseId,
        MilestoneOrdinal,
        DueAtGameTimeMinutes,
        DeadlineAtGameTimeMinutes,
        WindowDisposition,
        CourseStartAuthority.RestoreDetachedStartingWound(startingWound),
        CourseCoordinateFingerprint,
        CoordinatesFingerprint,
        AcceptedStateFingerprint,
        AuthorityFingerprint);

    internal static MortalWoundCourseModeAuthorityResult Create(
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState,
        MortalWoundTreatmentAttemptCoordinates? coordinates,
        WoundMaterializationEnvelope? before,
        WoundHistoryParseResult? history,
        MortalWoundGameTimeAuthority? gameTimeAuthority)
    {
        var issues = new List<ValidationIssue>();
        if (history is not { IsValid: true, State: not null })
        {
            if (history?.Issues.Count > 0)
                issues.AddRange(history.Issues);
            else
                Add(
                    issues,
                    "treatmentAttempt.courseMode.history",
                    "mortal_wound_treatment_course_history_invalid",
                    "one complete valid typed wound history",
                    history is null ? "missing" : "invalid history");
            return Invalid(issues);
        }

        if (acceptedState is null ||
            coordinates is null ||
            before is null ||
            gameTimeAuthority is null ||
            !coordinates.MatchesAcceptedState(acceptedState) ||
            !acceptedState.MatchesCurrentWound(before) ||
            !acceptedState.MatchesCompleteHistory(history) ||
            !gameTimeAuthority.Matches(acceptedState, coordinates))
        {
            Add(
                issues,
                "treatmentAttempt.courseMode",
                "mortal_wound_treatment_course_authority_invalid",
                "current accepted state, exact wound/history/coordinates, and its sealed game time",
                "missing, foreign, stale, or malformed authority");
            return Invalid(issues);
        }

        var routeMatches = acceptedState.TreatmentDefinition.Routes
            .Where(route => string.Equals(
                route.RouteId,
                coordinates.RouteId,
                StringComparison.Ordinal))
            .ToArray();
        if (routeMatches.Length != 1 ||
            routeMatches[0] is not MortalWoundCourseRouteDefinition courseRoute ||
            !string.Equals(
                courseRoute.Resolution.ClockKind,
                MortalWoundGameTimeAuthority.CanonicalClockKind,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                "treatmentAttempt.routeId",
                "mortal_wound_treatment_course_route_invalid",
                "one exact current course route using canonical world time",
                coordinates.RouteId);
            return Invalid(issues);
        }

        MortalWoundCourseStartAuthority startAuthority;
        string courseId;
        int milestoneOrdinal;
        if (before.Care.ActiveCourseId is null)
        {
            var courseSimulation = MortalWoundTreatmentWorkingWoundSimulator.Simulate(
                before,
                courseRoute.Milestones.Select(static milestone => milestone.DeclaredResult));
            if (!courseSimulation.IsApplicable || !courseSimulation.Improved)
            {
                Add(
                    issues,
                    "treatmentAttempt.courseMode",
                    "mortal_wound_treatment_course_sequence_inapplicable",
                    "one complete sequentially applicable course with an actual monotone improvement",
                    coordinates.RouteId);
                return Invalid(issues);
            }

            WoundMaterializationEnvelope startingWound;
            string routeFingerprint;
            try
            {
                var detached = WoundMaterializationContract.Parse(
                    WoundMaterializationContract.SerializeCanonical(before),
                    "treatmentAttempt.courseStart.startingWound");
                if (!detached.IsValid || detached.Wound is null)
                {
                    throw new InvalidOperationException(
                        "Canonical starting wound did not round-trip.");
                }
                startingWound = detached.Wound;
                routeFingerprint = MortalWoundTreatmentRouteFingerprint.Compute(
                    startingWound,
                    coordinates.RouteId);
            }
            catch (Exception exception) when (exception is ArgumentException or
                                               InvalidOperationException or
                                               JsonException or
                                               OverflowException)
            {
                Add(
                    issues,
                    "treatmentAttempt.courseMode",
                    "mortal_wound_treatment_course_start_invalid",
                    "detachable starting wound and exact course route",
                    exception.GetType().Name);
                return Invalid(issues);
            }

            var binding = acceptedState.Binding;
            var courseIdentityScope = new WoundAcceptedTurnIdentityScope(
                binding.SessionId,
                binding.RequestId,
                binding.SnapshotToken,
                binding.Realm,
                binding.Turn,
                binding.AcceptedEventsFingerprint,
                coordinates.EventRef,
                before.Origin.OpportunityId,
                before.Owner,
                "treatment_course",
                coordinates.OperationKey,
                before.WoundId);
            courseId = CreateCourseId(
                courseIdentityScope,
                routeFingerprint,
                gameTimeAuthority.CurrentTimeInMinutes);
            if (HasCourseIdentityConflict(history.State, courseId))
            {
                Add(
                    issues,
                    "treatmentAttempt.courseId",
                    "mortal_wound_treatment_course_id_conflict",
                    "a deterministic course ID absent from complete history",
                    courseId);
                return Invalid(issues);
            }

            startAuthority = MortalWoundCourseStartAuthority.Create(
                courseId,
                coordinates.RouteId,
                routeFingerprint,
                startingWound,
                gameTimeAuthority.CurrentTimeInMinutes,
                coordinates);
            milestoneOrdinal = 1;
        }
        else
        {
            try
            {
                if (!TryReconstructActiveCourse(
                        history.State,
                        before,
                        coordinates,
                        courseRoute,
                        issues,
                        out startAuthority,
                        out milestoneOrdinal))
                {
                    return Invalid(issues);
                }
            }
            catch (Exception exception) when (exception is ArgumentException or
                                               InvalidOperationException or
                                               JsonException or
                                               OverflowException)
            {
                Add(
                    issues,
                    "treatmentAttempt.courseMode.history",
                    "mortal_wound_treatment_course_history_invalid",
                    "one canonical typed active-course history",
                    exception.GetType().Name);
                return Invalid(issues);
            }
            courseId = startAuthority.CourseId;

            var remainingSimulation = MortalWoundTreatmentWorkingWoundSimulator.Simulate(
                before,
                courseRoute.Milestones
                    .Skip(milestoneOrdinal - 1)
                    .Select(static milestone => milestone.DeclaredResult));
            if (!remainingSimulation.IsApplicable || !remainingSimulation.Improved)
            {
                Add(
                    issues,
                    "treatmentAttempt.courseMode",
                    "mortal_wound_treatment_course_sequence_inapplicable",
                    "the remaining active course milestones are sequentially applicable and improve the current wound",
                    coordinates.RouteId);
                return Invalid(issues);
            }
        }

        long due;
        long deadline;
        try
        {
            due = checked(startAuthority!.StartedAtGameTimeMinutes +
                          courseRoute.Milestones[milestoneOrdinal - 1].AfterMinutes);
            deadline = checked(due + courseRoute.Resolution.MaximumGapMinutes);
        }
        catch (OverflowException)
        {
            Add(
                issues,
                "treatmentAttempt.courseMode",
                milestoneOrdinal == 1
                    ? "mortal_wound_treatment_course_start_invalid"
                    : "mortal_wound_treatment_course_window_invalid",
                "one non-overflowing canonical course window",
                milestoneOrdinal.ToString(CultureInfo.InvariantCulture));
            return Invalid(issues);
        }

        if (gameTimeAuthority.CurrentTimeInMinutes < due)
        {
            return new MortalWoundCourseModeAuthorityResult(
                "TooEarly",
                Array.Empty<ValidationIssue>(),
                null);
        }

        var courseCoordinateFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                CourseCoordinateDomain,
                "1",
                courseId,
                milestoneOrdinal.ToString(CultureInfo.InvariantCulture),
                coordinates.WoundId,
                startAuthority.StartingWoundFingerprint,
                coordinates.RouteId,
                startAuthority.RouteFingerprint,
                startAuthority.AuthorityFingerprint
            });
        var windowDisposition = gameTimeAuthority.CurrentTimeInMinutes <= deadline
            ? "ready"
            : "deadline_exceeded";
        var authorityFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                AuthorityDomain,
                "1",
                gameTimeAuthority.AuthorityFingerprint,
                courseId,
                milestoneOrdinal.ToString(CultureInfo.InvariantCulture),
                due.ToString(CultureInfo.InvariantCulture),
                deadline.ToString(CultureInfo.InvariantCulture),
                windowDisposition,
                startAuthority.AuthorityFingerprint,
                courseCoordinateFingerprint,
                coordinates.CoordinatesFingerprint,
                coordinates.AcceptedStateFingerprint
            });
        return new MortalWoundCourseModeAuthorityResult(
            windowDisposition == "ready" ? "Ready" : "DeadlineExceeded",
            Array.Empty<ValidationIssue>(),
            new MortalWoundCourseModeAuthority(
                gameTimeAuthority,
                courseId,
                milestoneOrdinal,
                due,
                deadline,
                windowDisposition,
                startAuthority,
                courseCoordinateFingerprint,
                coordinates.CoordinatesFingerprint,
                coordinates.AcceptedStateFingerprint,
                authorityFingerprint));
    }

    private static bool TryReconstructActiveCourse(
        WoundHistoryState history,
        WoundMaterializationEnvelope before,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        MortalWoundCourseRouteDefinition courseRoute,
        ICollection<ValidationIssue> issues,
        out MortalWoundCourseStartAuthority startAuthority,
        out int nextMilestoneOrdinal)
    {
        startAuthority = null!;
        nextMilestoneOrdinal = 0;
        var activeCourseId = before.Care.ActiveCourseId;
        if (!ResourceMaterializationContract.IsExactIdentifier(activeCourseId))
        {
            Add(
                issues,
                "treatmentAttempt.courseId",
                "mortal_wound_treatment_course_history_conflict",
                "one exact active course identifier",
                activeCourseId ?? "missing");
            return false;
        }

        var woundRows = history.Transitions
            .Where(transition => string.Equals(
                transition.WoundId,
                before.WoundId,
                StringComparison.Ordinal))
            .ToArray();
        var activeRows = woundRows
            .Where(transition => string.Equals(
                transition.CourseId,
                activeCourseId,
                StringComparison.Ordinal))
            .OrderBy(static transition => transition.CourseMilestoneOrdinal)
            .ToArray();
        if (activeRows.Length == 0)
        {
            Add(
                issues,
                "treatmentAttempt.courseId",
                "mortal_wound_treatment_course_start_conflict",
                "typed ordinal-one history for the existing active course, not a second course start",
                activeCourseId!);
            Add(
                issues,
                "treatmentAttempt.courseMode.history",
                "mortal_wound_treatment_course_history_missing",
                "typed contiguous history for the active course",
                activeCourseId!);
            return false;
        }

        var activeConfusable = ExactIdentifierConfusableKey.Build(activeCourseId!);
        if (woundRows.Any(transition =>
                transition.CourseId is { } existing &&
                !string.Equals(existing, activeCourseId, StringComparison.Ordinal) &&
                string.Equals(
                    ExactIdentifierConfusableKey.Build(existing),
                    activeConfusable,
                    StringComparison.Ordinal)))
        {
            Add(
                issues,
                "treatmentAttempt.courseMode.history",
                "mortal_wound_treatment_course_history_conflict",
                "one exact non-confusable active course identity",
                activeCourseId!);
            return false;
        }

        var firstGlobalOrdinal = activeRows[0].Ordinal;
        if (woundRows.Any(transition =>
                transition.Ordinal >= firstGlobalOrdinal &&
                transition.CourseId is { } existing &&
                !string.Equals(existing, activeCourseId, StringComparison.Ordinal)))
        {
            Add(
                issues,
                "treatmentAttempt.courseMode.history",
                "mortal_wound_treatment_course_second_course_conflict",
                "no second course coordinate after the active course start",
                activeCourseId!);
            return false;
        }

        for (var index = 0; index < activeRows.Length; index++)
        {
            var row = activeRows[index];
            var expectedOrdinal = index + 1;
            if (row.CourseMilestoneOrdinal != expectedOrdinal ||
                index > 0 && row.Ordinal <= activeRows[index - 1].Ordinal ||
                !string.Equals(row.Kind, "treat", StringComparison.Ordinal) ||
                row.Terminal ||
                row.TreatmentResult is not { } persisted ||
                persisted.Request is not { } request ||
                persisted.Receipt is not { } receipt ||
                !string.Equals(request.Mode, "course", StringComparison.Ordinal) ||
                request.MilestoneOrdinal != expectedOrdinal ||
                request.ModeAuthority is not MortalWoundCourseModeAuthority mode ||
                mode.MilestoneOrdinal != expectedOrdinal ||
                !string.Equals(mode.CourseId, activeCourseId, StringComparison.Ordinal) ||
                !string.Equals(receipt.CourseId, activeCourseId, StringComparison.Ordinal) ||
                receipt.CourseMilestoneOrdinal != expectedOrdinal ||
                receipt.Interruption ||
                request.RequirementAuthority.InterruptionReason is not null ||
                expectedOrdinal > courseRoute.Milestones.Length ||
                !string.Equals(
                    receipt.CourseDisposition,
                    courseRoute.Milestones[expectedOrdinal - 1].Completion,
                    StringComparison.Ordinal) ||
                !string.Equals(receipt.CourseDisposition, "active", StringComparison.Ordinal))
            {
                Add(
                    issues,
                    "treatmentAttempt.courseMode.history",
                    "mortal_wound_treatment_course_history_conflict",
                    "one contiguous typed non-interrupted active course prefix",
                    $"course={activeCourseId}; ordinal={expectedOrdinal}");
                return false;
            }

            if (expectedOrdinal == 1)
            {
                var start = mode.CourseStartAuthority;
                if (!string.Equals(start.CourseId, activeCourseId, StringComparison.Ordinal) ||
                    !string.Equals(start.RouteId, coordinates.RouteId, StringComparison.Ordinal) ||
                    start.StartingWound.Care.ActiveCourseId is not null ||
                    request.RouteSourceWound.Care.ActiveCourseId is not null ||
                    !string.Equals(
                        start.StartingWoundFingerprint,
                        request.RouteSourceWoundFingerprint,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        WoundMaterializationContract.SerializeCanonical(start.StartingWound),
                        WoundMaterializationContract.SerializeCanonical(request.RouteSourceWound),
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        start.AcceptedStateFingerprint,
                        request.Coordinates.AcceptedStateFingerprint,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        start.CoordinatesFingerprint,
                        request.Coordinates.CoordinatesFingerprint,
                        StringComparison.Ordinal) ||
                    start.StartedAtGameTimeMinutes !=
                    mode.GameTimeAuthority.CurrentTimeInMinutes)
                {
                    Add(
                        issues,
                        "treatmentAttempt.courseMode.history",
                        "mortal_wound_treatment_course_start_history_invalid",
                        "ordinal one with its exact complete course-start authority",
                        activeCourseId!);
                    return false;
                }
                startAuthority = start;
            }
            else if (!string.Equals(
                         mode.CourseStartAuthority.AuthorityFingerprint,
                         startAuthority.AuthorityFingerprint,
                         StringComparison.Ordinal))
            {
                Add(
                    issues,
                    "treatmentAttempt.courseMode.history",
                    "mortal_wound_treatment_course_start_history_invalid",
                    "every course milestone reuses the exact ordinal-one start authority",
                    activeCourseId!);
                return false;
            }
        }

        if (!string.Equals(startAuthority.RouteId, coordinates.RouteId,
                StringComparison.Ordinal) ||
            !string.Equals(
                startAuthority.RouteFingerprint,
                MortalWoundTreatmentRouteFingerprint.Compute(before, coordinates.RouteId),
                StringComparison.Ordinal) ||
            activeRows.Length >= courseRoute.Milestones.Length)
        {
            Add(
                issues,
                "treatmentAttempt.courseMode.history",
                "mortal_wound_treatment_course_history_conflict",
                "one current incomplete course using its original route authority",
                activeCourseId!);
            return false;
        }

        nextMilestoneOrdinal = activeRows.Length + 1;
        return true;
    }

    internal static string CreateCourseId(
        WoundAcceptedTurnIdentityScope identityScope,
        string routeFingerprint,
        long startedAtGameTimeMinutes) =>
        WoundAcceptedTurnIdentityWriter.Create(
            "mortal_wound_course",
            CourseIdentityDomain,
            identityScope,
            routeFingerprint,
            startedAtGameTimeMinutes.ToString(CultureInfo.InvariantCulture));

    internal static bool HasCourseIdentityConflict(
        WoundHistoryState history,
        string courseId)
    {
        var confusable = ExactIdentifierConfusableKey.Build(courseId);
        return history.Transitions.Any(transition =>
            transition.CourseId is { } existing && string.Equals(
                ExactIdentifierConfusableKey.Build(existing),
                confusable,
                StringComparison.Ordinal));
    }

    private static MortalWoundCourseModeAuthorityResult Invalid(
        IEnumerable<ValidationIssue> issues) => new(
        "InvalidAuthority",
        new ReadOnlyCollection<ValidationIssue>(issues.ToArray()),
        null);

    private static void Add(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) => issues.Add(new ValidationIssue(
        path,
        IssueSeverity.Error,
        "The Mortal wound-treatment course authority cannot be trusted.",
        code: code,
        actor: "Client",
        section: "wound_materialization",
        expected: expected,
        actual: actual,
        repairHint:
        "Recreate course authority from the current accepted state, exact complete history, and canonical game time."));
}
