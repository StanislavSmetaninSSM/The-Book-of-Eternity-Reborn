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

internal sealed class MortalWoundCourseModeAuthority
{
    private const string CourseIdentityDomain =
        "book_of_eternity.mortal_wound_treatment.course_identity";
    private const string CourseCoordinateDomain =
        "book_of_eternity.mortal_wound_treatment.course_coordinates";
    private const string AuthorityDomain =
        "book_of_eternity.mortal_wound_treatment.course_mode_authority";
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

    internal static MortalWoundCourseModeAuthorityResult Create(
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState,
        MortalWoundTreatmentAttemptCoordinates? coordinates,
        WoundMaterializationEnvelope? before,
        WoundHistoryParseResult? history,
        MortalWoundGameTimeAuthority? gameTimeAuthority)
    {
        var issues = new List<ValidationIssue>();
        if (acceptedState is null ||
            coordinates is null ||
            before is null ||
            history is null ||
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

        if (before.Care.ActiveCourseId is not null)
        {
            Add(
                issues,
                "treatmentAttempt.courseId",
                "mortal_wound_treatment_course_start_conflict",
                "no active course for ordinal-1 creation",
                before.Care.ActiveCourseId);
            return Invalid(issues);
        }

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
        long due;
        long deadline;
        try
        {
            var detached = WoundMaterializationContract.Parse(
                WoundMaterializationContract.SerializeCanonical(before),
                "treatmentAttempt.courseStart.startingWound");
            if (!detached.IsValid || detached.Wound is null)
                throw new InvalidOperationException("Canonical starting wound did not round-trip.");
            startingWound = detached.Wound;
            routeFingerprint = MortalWoundTreatmentRouteFingerprint.Compute(
                startingWound,
                coordinates.RouteId);
            due = checked(gameTimeAuthority.CurrentTimeInMinutes +
                          courseRoute.Milestones[0].AfterMinutes);
            deadline = checked(due + courseRoute.Resolution.MaximumGapMinutes);
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
                "detachable starting wound, exact route, and non-overflowing canonical window",
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
        var courseId = CreateCourseId(
            courseIdentityScope,
            routeFingerprint,
            gameTimeAuthority.CurrentTimeInMinutes);
        if (HasCourseIdentityConflict(history.State!, courseId))
        {
            Add(
                issues,
                "treatmentAttempt.courseId",
                "mortal_wound_treatment_course_id_conflict",
                "a deterministic course ID absent from complete history",
                courseId);
            return Invalid(issues);
        }

        var startAuthority = MortalWoundCourseStartAuthority.Create(
            courseId,
            coordinates.RouteId,
            routeFingerprint,
            startingWound,
            gameTimeAuthority.CurrentTimeInMinutes,
            coordinates);
        const int milestoneOrdinal = 1;
        var courseCoordinateFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                CourseCoordinateDomain,
                "1",
                courseId,
                milestoneOrdinal.ToString(CultureInfo.InvariantCulture),
                coordinates.WoundId,
                coordinates.ExpectedBeforeFingerprint,
                coordinates.RouteId,
                routeFingerprint,
                startAuthority.AuthorityFingerprint
            });
        const string windowDisposition = "ready";
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
            "Ready",
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
