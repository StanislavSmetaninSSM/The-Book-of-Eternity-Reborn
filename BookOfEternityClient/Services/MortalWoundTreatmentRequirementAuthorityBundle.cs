using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundTreatmentRequirementAuthorityBundleResult(
    bool IsValid,
    IReadOnlyList<ValidationIssue> Issues,
    MortalWoundTreatmentRequirementAuthorityBundle? Authority);

internal sealed record MortalWoundCourseRequirementAuthorityResult(
    string Status,
    IReadOnlyList<ValidationIssue> Issues,
    MortalWoundTreatmentRequirementAuthorityBundle? Authority);

internal sealed class MortalWoundTreatmentRequirementAuthorityBundle
{
    private const string BundleDomain =
        "book_of_eternity.mortal_wound_treatment.requirement_bundle";

    [JsonConstructor]
    private MortalWoundTreatmentRequirementAuthorityBundle(
        string mode,
        string contextFingerprint,
        string acceptedStateFingerprint,
        string routeFingerprint,
        string? courseId,
        int? courseMilestoneOrdinal,
        string? courseCoordinateFingerprint,
        string? courseRequirementStatus,
        string? interruptionReason,
        IReadOnlyList<MortalWoundTreatmentRequirementScopeAuthority> scopes,
        string authorityFingerprint)
    {
        Mode = mode;
        ContextFingerprint = contextFingerprint;
        AcceptedStateFingerprint = acceptedStateFingerprint;
        RouteFingerprint = routeFingerprint;
        CourseId = courseId;
        CourseMilestoneOrdinal = courseMilestoneOrdinal;
        CourseCoordinateFingerprint = courseCoordinateFingerprint;
        CourseRequirementStatus = courseRequirementStatus;
        InterruptionReason = interruptionReason;
        Scopes = Freeze(scopes);
        AuthorityFingerprint = authorityFingerprint;
    }

    public string Mode { get; }
    public string ContextFingerprint { get; }
    public string AcceptedStateFingerprint { get; }
    public string RouteFingerprint { get; }
    public string? CourseId { get; }
    public int? CourseMilestoneOrdinal { get; }
    public string? CourseCoordinateFingerprint { get; }
    public string? CourseRequirementStatus { get; }
    public string? InterruptionReason { get; }
    public IReadOnlyList<MortalWoundTreatmentRequirementScopeAuthority> Scopes { get; }
    public string AuthorityFingerprint { get; }

    internal static MortalWoundTreatmentRequirementAuthorityBundleResult CreateForProcedure(
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState,
        MortalWoundTreatmentAttemptCoordinates? coordinates,
        WoundMaterializationEnvelope? before) =>
        CreateForNonCourse("procedure", acceptedState, coordinates, before);

    internal static MortalWoundTreatmentRequirementAuthorityBundleResult CreateForGuaranteed(
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState,
        MortalWoundTreatmentAttemptCoordinates? coordinates,
        WoundMaterializationEnvelope? before) =>
        CreateForNonCourse("guaranteed", acceptedState, coordinates, before);

    internal static MortalWoundCourseRequirementAuthorityResult CreateForCourseMilestone(
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState,
        MortalWoundTreatmentAttemptCoordinates? coordinates,
        WoundMaterializationEnvelope? before,
        WoundHistoryParseResult? history,
        MortalWoundCourseModeAuthority? courseMode)
    {
        var issues = new List<ValidationIssue>();
        if (!TrySelectRoute(
                "course",
                acceptedState,
                coordinates,
                before,
                issues,
                out var rawRoute,
                out var routeFingerprint) ||
            history is null ||
            courseMode is null ||
            acceptedState is null ||
            coordinates is null ||
            before is null ||
            !acceptedState.MatchesCompleteHistory(history) ||
            !courseMode.GameTimeAuthority.Matches(acceptedState, coordinates) ||
            !string.Equals(
                courseMode.CoordinatesFingerprint,
                coordinates.CoordinatesFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                courseMode.AcceptedStateFingerprint,
                coordinates.AcceptedStateFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                courseMode.CourseStartAuthority.CourseId,
                courseMode.CourseId,
                StringComparison.Ordinal) ||
            !string.Equals(
                courseMode.CourseStartAuthority.RouteId,
                coordinates.RouteId,
                StringComparison.Ordinal) ||
            !string.Equals(
                courseMode.CourseStartAuthority.RouteFingerprint,
                routeFingerprint,
                StringComparison.Ordinal) ||
            courseMode.WindowDisposition is not ("ready" or "deadline_exceeded") ||
            courseMode.MilestoneOrdinal == 1 &&
            (before.Care.ActiveCourseId is not null ||
             !string.Equals(
                 courseMode.CourseStartAuthority.StartingWoundFingerprint,
                 acceptedState.WoundFingerprint,
                 StringComparison.Ordinal) ||
             !acceptedState.MatchesCurrentWound(
                 courseMode.CourseStartAuthority.StartingWound)) ||
            courseMode.MilestoneOrdinal > 1 &&
            !string.Equals(
                before.Care.ActiveCourseId,
                courseMode.CourseId,
                StringComparison.Ordinal))
        {
            if (issues.Count == 0)
            {
                AddIssue(
                    issues,
                    "treatmentAttempt.requirements",
                    "mortal_wound_treatment_requirement_course_authority_invalid",
                    "the exact current history and matching complete course-mode authority",
                    "missing, foreign, stale, or mismatched authority");
            }
            return InvalidCourse(issues);
        }

        if (!TrySelectMilestoneRequirements(
                rawRoute!,
                courseMode.MilestoneOrdinal,
                out var milestoneRoute))
        {
            AddIssue(
                issues,
                "treatmentAttempt.requirements.courseMilestoneOrdinal",
                "mortal_wound_treatment_requirement_course_milestone_invalid",
                "one exact authored milestone matching course mode",
                courseMode.MilestoneOrdinal.ToString(CultureInfo.InvariantCulture));
            return InvalidCourse(issues);
        }

        var context = acceptedState.RequirementContext;
        var snapshot = acceptedState.RequirementSnapshot;
        var commonResult = MortalWoundTreatmentAuthority.ResolveRequirements(
            rawRoute!,
            context,
            snapshot);
        var milestoneResult = MortalWoundTreatmentAuthority.ResolveRequirements(
            milestoneRoute!,
            context,
            snapshot);
        var aggregateQuantities = new Dictionary<string, long>(StringComparer.Ordinal);
        if (!TryBuildScope(
                "common",
                null,
                rawRoute!,
                context,
                snapshot,
                commonResult,
                allowTrustedAbsence: true,
                aggregateQuantities,
                issues,
                out var commonScope) ||
            !TryBuildScope(
                "course_milestone",
                courseMode.MilestoneOrdinal,
                milestoneRoute!,
                context,
                snapshot,
                milestoneResult,
                allowTrustedAbsence: true,
                aggregateQuantities,
                issues,
                out var milestoneScope))
        {
            return InvalidCourse(issues);
        }

        var status = commonScope!.Status == "Satisfied" &&
                     milestoneScope!.Status == "Satisfied"
            ? "Satisfied"
            : "Unsatisfied";
        var interruptionReason = courseMode.WindowDisposition switch
        {
            "deadline_exceeded" => "deadline_exceeded",
            "ready" when status == "Unsatisfied" && courseMode.MilestoneOrdinal > 1 =>
                "requirements_unsatisfied",
            _ => null
        };
        var authority = Create(
            "course",
            coordinates,
            routeFingerprint!,
            courseMode.CourseId,
            courseMode.MilestoneOrdinal,
            courseMode.CourseCoordinateFingerprint,
            status,
            interruptionReason,
            new[] { commonScope!, milestoneScope! });
        return new MortalWoundCourseRequirementAuthorityResult(
            status,
            Array.Empty<ValidationIssue>(),
            authority);
    }

    private static MortalWoundTreatmentRequirementAuthorityBundleResult CreateForNonCourse(
        string mode,
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState,
        MortalWoundTreatmentAttemptCoordinates? coordinates,
        WoundMaterializationEnvelope? before)
    {
        var issues = new List<ValidationIssue>();
        if (!TrySelectRoute(
                mode,
                acceptedState,
                coordinates,
                before,
                issues,
                out var route,
                out var routeFingerprint) ||
            acceptedState is null ||
            coordinates is null)
        {
            return InvalidNonCourse(issues);
        }

        var context = acceptedState.RequirementContext;
        var snapshot = acceptedState.RequirementSnapshot;
        var resolved = MortalWoundTreatmentAuthority.ResolveRequirements(
            route!,
            context,
            snapshot);
        if (!resolved.Success)
        {
            return new MortalWoundTreatmentRequirementAuthorityBundleResult(
                false,
                Freeze(resolved.Issues),
                null);
        }

        if (!TryBuildScope(
                "common",
                null,
                route!,
                context,
                snapshot,
                resolved,
                allowTrustedAbsence: false,
                new Dictionary<string, long>(StringComparer.Ordinal),
                issues,
                out var scope))
        {
            return InvalidNonCourse(issues);
        }

        var authority = Create(
            mode,
            coordinates,
            routeFingerprint!,
            null,
            null,
            null,
            null,
            null,
            new[] { scope! });
        return new MortalWoundTreatmentRequirementAuthorityBundleResult(
            true,
            Array.Empty<ValidationIssue>(),
            authority);
    }

    private static bool TrySelectRoute(
        string expectedMode,
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState,
        MortalWoundTreatmentAttemptCoordinates? coordinates,
        WoundMaterializationEnvelope? before,
        ICollection<ValidationIssue> issues,
        out WoundTreatmentRoute? route,
        out string? routeFingerprint)
    {
        route = null;
        routeFingerprint = null;
        if (acceptedState is null ||
            coordinates is null ||
            before is null ||
            !acceptedState.HasCurrentAdmissionAuthority() ||
            !coordinates.MatchesAcceptedState(acceptedState) ||
            !acceptedState.MatchesCurrentWound(before))
        {
            AddIssue(
                issues,
                "treatmentAttempt.requirements",
                "mortal_wound_treatment_requirement_authority_invalid",
                "one current accepted state with matching coordinates and wound",
                "missing, released, foreign, or stale authority");
            return false;
        }

        var rawMatches = before.Treatment.Routes.Where(candidate => string.Equals(
                candidate.RouteId,
                coordinates.RouteId,
                StringComparison.Ordinal))
            .ToArray();
        var typedMatches = acceptedState.TreatmentDefinition.Routes.Where(candidate =>
                string.Equals(
                    candidate.RouteId,
                    coordinates.RouteId,
                    StringComparison.Ordinal))
            .ToArray();
        if (rawMatches.Length != 1 ||
            typedMatches.Length != 1 ||
            !string.Equals(rawMatches[0].Mode, expectedMode, StringComparison.Ordinal) ||
            !string.Equals(typedMatches[0].Mode, expectedMode, StringComparison.Ordinal))
        {
            AddIssue(
                issues,
                "treatmentAttempt.requirements.routeId",
                "mortal_wound_treatment_requirement_route_invalid",
                $"one exact current {expectedMode} route",
                coordinates.RouteId);
            return false;
        }

        try
        {
            route = rawMatches[0];
            routeFingerprint = MortalWoundTreatmentRouteFingerprint.Compute(
                before,
                coordinates.RouteId);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException or
                                           JsonException)
        {
            AddIssue(
                issues,
                "treatmentAttempt.requirements.routeId",
                "mortal_wound_treatment_requirement_route_invalid",
                "one canonical route with a recomputable mechanical fingerprint",
                exception.GetType().Name);
            return false;
        }
    }

    private static bool TrySelectMilestoneRequirements(
        WoundTreatmentRoute route,
        int milestoneOrdinal,
        out WoundTreatmentRoute? milestoneRoute)
    {
        milestoneRoute = null;
        var matches = route.Outcomes
            .Select((outcome, index) => (Outcome: outcome, Index: index))
            .Where(pair =>
                pair.Outcome.ValueKind == JsonValueKind.Object &&
                pair.Outcome.TryGetProperty("ordinal", out var ordinal) &&
                ordinal.ValueKind == JsonValueKind.Number &&
                ordinal.TryGetInt32(out var value) &&
                value == milestoneOrdinal)
            .ToArray();
        if (matches.Length != 1 ||
            !matches[0].Outcome.TryGetProperty("requirements", out var requirements) ||
            requirements.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var sourcePath = string.IsNullOrEmpty(route.SourcePath)
            ? $"wound.treatment.routes[0].outcomes[{matches[0].Index}]"
            : $"{route.SourcePath}.outcomes[{matches[0].Index}]";
        milestoneRoute = route with
        {
            Requirements = requirements.EnumerateArray()
                .Select(static requirement => requirement.Clone())
                .ToArray(),
            SourcePath = sourcePath
        };
        return true;
    }

    private static bool TryBuildScope(
        string scope,
        int? milestoneOrdinal,
        WoundTreatmentRoute route,
        MortalWoundTreatmentAuthority.Context context,
        MortalWoundTreatmentAuthority.Snapshot snapshot,
        MortalWoundRequirementAuthorityResult result,
        bool allowTrustedAbsence,
        IDictionary<string, long> aggregateQuantities,
        ICollection<ValidationIssue> issues,
        out MortalWoundTreatmentRequirementScopeAuthority? authority)
    {
        authority = null;
        var rows = result.ResolvedRequirements.ToDictionary(
            static row => row.RequirementIndex);
        var bindings = new List<MortalWoundTreatmentRequirementBinding>();
        var failures = new List<MortalWoundTreatmentRequirementFailureWitness>();
        var cumulative = new Dictionary<string, long>(StringComparer.Ordinal);
        if (!IssuesDescribeOnlyMissingRequirements(route, rows.Keys, result.Issues))
        {
            foreach (var issue in result.Issues)
                issues.Add(issue);
            return false;
        }
        for (var index = 0; index < route.Requirements.Count; index++)
        {
            var requirement = route.Requirements[index];
            if (rows.TryGetValue(index, out var row))
            {
                if (!MortalWoundTreatmentRequirementWitnessFactory.TryCreateSuccess(
                        scope,
                        requirement,
                        row,
                        context,
                        snapshot,
                        cumulative,
                        out var witness) ||
                    witness is null ||
                    !string.Equals(
                        row.AuthorityFingerprint,
                        MortalWoundTreatmentAuthority.RecomputeResolvedRequirementFingerprint(
                            row,
                            context,
                            requirement,
                            witness.T060MechanicalFields),
                        StringComparison.Ordinal))
                {
                    AddIssue(
                        issues,
                        $"treatmentAttempt.requirements.{scope}[{index}]",
                        "mortal_wound_treatment_requirement_success_witness_invalid",
                        "one exact T060 row reconstructed from complete current evidence",
                        row.AuthorityFingerprint);
                    return false;
                }
                if (!MortalWoundTreatmentRequirementWitnessFactory.TryCreateAggregateFailure(
                        scope,
                        index,
                        requirement,
                        row,
                        witness,
                        context,
                        snapshot,
                        aggregateQuantities,
                        out var aggregateFailure))
                {
                    AddIssue(
                        issues,
                        $"treatmentAttempt.requirements.{scope}[{index}]",
                        "mortal_wound_treatment_requirement_aggregate_witness_invalid",
                        "one deterministic cross-scope quantity ledger decision",
                        row.AuthorityFingerprint);
                    return false;
                }
                if (aggregateFailure is not null)
                {
                    failures.Add(aggregateFailure);
                    continue;
                }
                bindings.Add(MortalWoundTreatmentRequirementBinding.Create(
                    row,
                    witness));
                continue;
            }

            if (!allowTrustedAbsence ||
                !TryClassifyFailureReason(
                    route,
                    index,
                    result.Issues,
                    out var lossReason))
            {
                foreach (var issue in result.Issues)
                    issues.Add(issue);
                if (result.Issues.Count == 0)
                {
                    AddIssue(
                        issues,
                        $"treatmentAttempt.requirements.{scope}[{index}]",
                        "mortal_wound_treatment_requirement_classifier_invalid",
                        "one resolved row or typed trustworthy failure",
                        "neither was produced");
                }
                return false;
            }

            if (!MortalWoundTreatmentRequirementWitnessFactory.TryCreateFailure(
                    scope,
                    index,
                    requirement,
                    lossReason!,
                    context,
                    snapshot,
                    cumulative,
                    out var failure) ||
                failure is null)
            {
                AddIssue(
                    issues,
                    $"treatmentAttempt.requirements.{scope}[{index}]",
                    "mortal_wound_treatment_requirement_failure_witness_invalid",
                    "one closed failure witness whose observation proves its loss reason",
                    lossReason ?? "missing");
                return false;
            }
            failures.Add(failure);
        }

        if (bindings.Count + failures.Count != route.Requirements.Count)
            return false;
        var status = failures.Count == 0 ? "Satisfied" : "Unsatisfied";
        authority = MortalWoundTreatmentRequirementScopeAuthority.Create(
            scope,
            milestoneOrdinal,
            status,
            bindings,
            failures);
        return true;
    }

    private static bool TryClassifyFailureReason(
        WoundTreatmentRoute route,
        int requirementIndex,
        IReadOnlyList<ValidationIssue> issues,
        out string? lossReason)
    {
        lossReason = null;
        var root = string.IsNullOrEmpty(route.SourcePath)
            ? "wound.treatment.routes[0].requirements"
            : route.SourcePath + ".requirements";
        var path = $"{root}[{requirementIndex}]";
        var relevant = issues.Where(issue =>
                !string.IsNullOrEmpty(issue.FilePath) &&
                (string.Equals(issue.FilePath, path, StringComparison.Ordinal) ||
                 issue.FilePath.StartsWith(path + ".", StringComparison.Ordinal)))
            .ToArray();
        if (relevant.Length == 0)
            return false;

        var reasons = relevant
            .Select(static issue => ClassifyFailureReason(issue.Code))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (reasons.Length != 1 || reasons[0] is null)
            return false;
        lossReason = reasons[0];
        return true;
    }

    private static bool IssuesDescribeOnlyMissingRequirements(
        WoundTreatmentRoute route,
        IEnumerable<int> resolvedIndexes,
        IReadOnlyList<ValidationIssue> issues)
    {
        var resolved = resolvedIndexes.ToHashSet();
        var root = string.IsNullOrEmpty(route.SourcePath)
            ? "wound.treatment.routes[0].requirements"
            : route.SourcePath + ".requirements";
        foreach (var issue in issues)
        {
            if (ClassifyFailureReason(issue.Code) is null ||
                string.IsNullOrEmpty(issue.FilePath))
            {
                return false;
            }
            var matches = Enumerable.Range(0, route.Requirements.Count)
                .Where(index =>
                {
                    var path = $"{root}[{index}]";
                    return string.Equals(issue.FilePath, path, StringComparison.Ordinal) ||
                           issue.FilePath.StartsWith(path + ".", StringComparison.Ordinal);
                })
                .ToArray();
            if (matches.Length != 1 || resolved.Contains(matches[0]))
                return false;
        }
        return true;
    }

    private static string? ClassifyFailureReason(string? code) => code switch
    {
        "mortal_wound_requirement_reference_unresolved" => "authority_absent",
        "mortal_wound_requirement_item_quantity_insufficient" => "quantity_insufficient",
        "mortal_wound_requirement_resource_quantity_insufficient" => "quantity_insufficient",
        "mortal_wound_requirement_item_reserved" => "reserved",
        "mortal_wound_requirement_resource_reserved" => "reserved",
        "mortal_wound_requirement_skill_tier_insufficient" => "tier_insufficient",
        "mortal_wound_requirement_stale_reference" => "retired",
        "mortal_wound_requirement_item_unavailable" => "inactive",
        "mortal_wound_requirement_resource_unavailable" => "inactive",
        "mortal_wound_requirement_skill_inactive" => "inactive",
        "mortal_wound_requirement_capability_inactive" => "inactive",
        "mortal_wound_requirement_provider_inactive" => "inactive",
        "mortal_wound_requirement_state_mismatch" => "state_mismatch",
        "mortal_wound_requirement_wrong_owner" => "owner_unavailable",
        "mortal_wound_requirement_provider_unreachable" => "provider_unreachable",
        "mortal_wound_requirement_consent_missing" => "consent_absent",
        "mortal_wound_requirement_facility_unavailable" => "facility_unavailable",
        "mortal_wound_requirement_facility_location_mismatch" => "wrong_location",
        "mortal_wound_requirement_environment_location_mismatch" => "wrong_location",
        "mortal_wound_requirement_target_not_present" => "actor_not_present",
        _ => null
    };

    private static MortalWoundTreatmentRequirementAuthorityBundle Create(
        string mode,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string routeFingerprint,
        string? courseId,
        int? courseMilestoneOrdinal,
        string? courseCoordinateFingerprint,
        string? courseRequirementStatus,
        string? interruptionReason,
        IReadOnlyList<MortalWoundTreatmentRequirementScopeAuthority> scopes)
    {
        var fields = new List<string?>
        {
            BundleDomain,
            "1",
            mode,
            coordinates.ContextFingerprint,
            coordinates.AcceptedStateFingerprint,
            routeFingerprint,
            courseId,
            courseMilestoneOrdinal?.ToString(CultureInfo.InvariantCulture),
            courseCoordinateFingerprint,
            courseRequirementStatus,
            interruptionReason,
            scopes.Count.ToString(CultureInfo.InvariantCulture)
        };
        foreach (var scope in scopes)
            fields.Add(scope.AuthorityFingerprint);
        return new MortalWoundTreatmentRequirementAuthorityBundle(
            mode,
            coordinates.ContextFingerprint,
            coordinates.AcceptedStateFingerprint,
            routeFingerprint,
            courseId,
            courseMilestoneOrdinal,
            courseCoordinateFingerprint,
            courseRequirementStatus,
            interruptionReason,
            scopes,
            WoundAcceptedTurnFingerprintWriter.Compute(fields));
    }

    private static MortalWoundTreatmentRequirementAuthorityBundleResult InvalidNonCourse(
        IEnumerable<ValidationIssue> issues) => new(false, Freeze(issues), null);

    private static MortalWoundCourseRequirementAuthorityResult InvalidCourse(
        IEnumerable<ValidationIssue> issues) => new(
        "InvalidAuthority",
        Freeze(issues),
        null);

    private static void AddIssue(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) => issues.Add(new ValidationIssue(
        path,
        IssueSeverity.Error,
        "The Mortal wound-treatment requirement authority cannot be trusted.",
        code: code,
        actor: "Client",
        section: "wound_materialization",
        expected: expected,
        actual: actual,
        repairHint:
        "Re-export the current accepted state and rebuild the requirement bundle from its exact route and coordinates."));

    private static IReadOnlyList<T> Freeze<T>(IEnumerable<T> values) =>
        Array.AsReadOnly(values.ToArray());
}

internal sealed class MortalWoundTreatmentRequirementScopeAuthority
{
    private const string Domain =
        "book_of_eternity.mortal_wound_treatment.requirement_scope";

    [JsonConstructor]
    private MortalWoundTreatmentRequirementScopeAuthority(
        string scope,
        int? courseMilestoneOrdinal,
        string status,
        IReadOnlyList<MortalWoundTreatmentRequirementBinding> bindings,
        IReadOnlyList<MortalWoundTreatmentRequirementFailureWitness> failureWitnesses,
        string authorityFingerprint)
    {
        Scope = scope;
        CourseMilestoneOrdinal = courseMilestoneOrdinal;
        Status = status;
        Bindings = Array.AsReadOnly(bindings.ToArray());
        FailureWitnesses = Array.AsReadOnly(failureWitnesses.ToArray());
        AuthorityFingerprint = authorityFingerprint;
    }

    public string Scope { get; }
    public int? CourseMilestoneOrdinal { get; }
    public string Status { get; }
    public IReadOnlyList<MortalWoundTreatmentRequirementBinding> Bindings { get; }
    public IReadOnlyList<MortalWoundTreatmentRequirementFailureWitness> FailureWitnesses { get; }
    public string AuthorityFingerprint { get; }

    internal static MortalWoundTreatmentRequirementScopeAuthority Create(
        string scope,
        int? courseMilestoneOrdinal,
        string status,
        IReadOnlyList<MortalWoundTreatmentRequirementBinding> bindings,
        IReadOnlyList<MortalWoundTreatmentRequirementFailureWitness> failures)
    {
        var fields = new List<string?>
        {
            Domain,
            "1",
            scope,
            courseMilestoneOrdinal?.ToString(CultureInfo.InvariantCulture),
            status,
            bindings.Count.ToString(CultureInfo.InvariantCulture)
        };
        fields.AddRange(bindings.Select(static binding => binding.BindingFingerprint));
        fields.Add(failures.Count.ToString(CultureInfo.InvariantCulture));
        fields.AddRange(failures.Select(static failure => failure.WitnessFingerprint));
        return new MortalWoundTreatmentRequirementScopeAuthority(
            scope,
            courseMilestoneOrdinal,
            status,
            bindings,
            failures,
            WoundAcceptedTurnFingerprintWriter.Compute(fields));
    }
}

internal sealed class MortalWoundTreatmentRequirementBinding
{
    private const string Domain =
        "book_of_eternity.mortal_wound_treatment.requirement_binding";

    [JsonConstructor]
    private MortalWoundTreatmentRequirementBinding(
        int requirementIndex,
        MortalWoundResolvedRequirement resolvedRequirement,
        MortalWoundTreatmentRequirementSuccessWitness successWitness,
        string bindingFingerprint)
    {
        RequirementIndex = requirementIndex;
        ResolvedRequirement = resolvedRequirement with { };
        SuccessWitness = successWitness;
        BindingFingerprint = bindingFingerprint;
    }

    public int RequirementIndex { get; }
    public MortalWoundResolvedRequirement ResolvedRequirement { get; }
    public MortalWoundTreatmentRequirementSuccessWitness SuccessWitness { get; }
    public string BindingFingerprint { get; }

    internal static MortalWoundTreatmentRequirementBinding Create(
        MortalWoundResolvedRequirement row,
        MortalWoundTreatmentRequirementSuccessWitness witness) => new(
        row.RequirementIndex,
        row,
        witness,
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            Domain,
            "1",
            witness.Scope,
            row.RequirementIndex.ToString(CultureInfo.InvariantCulture),
            row.AuthorityFingerprint,
            witness.WitnessFingerprint
        }));
}

[JsonPolymorphic]
[JsonDerivedType(typeof(MortalWoundItemQuantityRequirementEvidence))]
[JsonDerivedType(typeof(MortalWoundResourceQuantityRequirementEvidence))]
[JsonDerivedType(typeof(MortalWoundSkillTierRequirementEvidence))]
[JsonDerivedType(typeof(MortalWoundSourceCapabilityRequirementEvidence))]
[JsonDerivedType(typeof(MortalWoundProviderRequirementEvidence))]
[JsonDerivedType(typeof(MortalWoundConsentRequirementEvidence))]
[JsonDerivedType(typeof(MortalWoundFacilityRequirementEvidence))]
[JsonDerivedType(typeof(MortalWoundLocationRequirementEvidence))]
[JsonDerivedType(typeof(MortalWoundQuestStateRequirementEvidence))]
[JsonDerivedType(typeof(MortalWoundEffectStateRequirementEvidence))]
[JsonDerivedType(typeof(MortalWoundEnvironmentRequirementEvidence))]
internal abstract record MortalWoundTreatmentRequirementEvidence(string Kind);

internal sealed record MortalWoundItemQuantityRequirementEvidence(
    string OwnerRole,
    int RequestedQuantity,
    int Count,
    int AvailableCount,
    string ReservationState,
    string Lifecycle,
    bool Active,
    long CumulativeRequestedQuantity) :
    MortalWoundTreatmentRequirementEvidence("item_quantity");

internal sealed record MortalWoundResourceQuantityRequirementEvidence(
    string OwnerRole,
    int RequestedQuantity,
    int CurrentValue,
    int AvailableValue,
    string ReservationState,
    string Lifecycle,
    bool Active,
    long CumulativeRequestedQuantity) :
    MortalWoundTreatmentRequirementEvidence("resource_quantity");

internal sealed record MortalWoundSkillTierRequirementEvidence(
    string ActorRole,
    int MinimumTier,
    int CurrentTier,
    string ActorCurrentLocationId,
    string RequiredLocationId,
    string ActorLifecycle,
    bool ActorActive,
    bool ActorReachable,
    bool ActorPresent,
    string SkillLifecycle,
    bool SkillActive) : MortalWoundTreatmentRequirementEvidence("skill_tier");

internal sealed record MortalWoundSourceCapabilityRequirementEvidence(
    string ActorRole,
    string ActorCurrentLocationId,
    string RequiredLocationId,
    string ActorLifecycle,
    bool ActorActive,
    bool ActorReachable,
    bool ActorPresent,
    string CapabilityLifecycle,
    bool CapabilityActive) : MortalWoundTreatmentRequirementEvidence("source_capability");

internal sealed record MortalWoundProviderRequirementEvidence(
    string CurrentLocationId,
    string RequiredLocationId,
    string Lifecycle,
    bool Active,
    bool Reachable,
    bool Present) : MortalWoundTreatmentRequirementEvidence("provider");

internal sealed record MortalWoundConsentRequirementEvidence(
    string RequiredProviderRef,
    string RequiredTargetRef,
    string ProviderCurrentLocationId,
    string RequiredLocationId,
    string ProviderLifecycle,
    bool ProviderActive,
    bool ProviderReachable,
    bool ProviderPresent,
    string Status,
    string Lifecycle,
    bool Active) : MortalWoundTreatmentRequirementEvidence("consent");

internal sealed record MortalWoundFacilityRequirementEvidence(
    string FacilityLocationId,
    string RequiredLocationId,
    string Lifecycle,
    bool Active,
    bool Available,
    string RequiredActorKind,
    string RequiredActorId,
    bool RequiredActorPresent) : MortalWoundTreatmentRequirementEvidence("facility");

internal sealed record MortalWoundLocationRequirementEvidence(
    string TargetRole,
    string AuthorityLocationId,
    string RequiredLocationId,
    string Lifecycle,
    bool Active,
    bool TargetPresent,
    IReadOnlyList<MortalWoundTreatmentAuthority.ActorCoordinate> PresentActors) :
    MortalWoundTreatmentRequirementEvidence("location");

internal sealed record MortalWoundQuestStateRequirementEvidence(
    string RequiredState,
    string CurrentState,
    string Lifecycle,
    bool Active) : MortalWoundTreatmentRequirementEvidence("quest_state");

internal sealed record MortalWoundEffectStateRequirementEvidence(
    string RequiredState,
    string CurrentState,
    string Lifecycle,
    bool Active) : MortalWoundTreatmentRequirementEvidence("effect_state");

internal sealed record MortalWoundEnvironmentRequirementEvidence(
    string RequiredState,
    string CurrentState,
    string CurrentLocationId,
    string RequiredLocationId,
    string Lifecycle,
    bool Active) : MortalWoundTreatmentRequirementEvidence("environment");

internal sealed class MortalWoundTreatmentRequirementSuccessWitness
{
    internal MortalWoundTreatmentRequirementSuccessWitness(
        string scope,
        int requirementIndex,
        string kind,
        string authorityRef,
        string snapshotToken,
        string realm,
        string? ownerKind,
        string? ownerId,
        string? providerKind,
        string? providerId,
        string? targetKind,
        string? targetId,
        string? locationId,
        MortalWoundTreatmentRequirementEvidence evidence,
        IReadOnlyList<string?> t060MechanicalFields,
        string witnessFingerprint)
    {
        Scope = scope;
        RequirementIndex = requirementIndex;
        Kind = kind;
        AuthorityRef = authorityRef;
        SnapshotToken = snapshotToken;
        Realm = realm;
        OwnerKind = ownerKind;
        OwnerId = ownerId;
        ProviderKind = providerKind;
        ProviderId = providerId;
        TargetKind = targetKind;
        TargetId = targetId;
        LocationId = locationId;
        Evidence = evidence;
        T060MechanicalFields = Array.AsReadOnly(t060MechanicalFields.ToArray());
        WitnessFingerprint = witnessFingerprint;
    }

    [JsonConstructor]
    private MortalWoundTreatmentRequirementSuccessWitness(
        string scope,
        int requirementIndex,
        string kind,
        string authorityRef,
        string snapshotToken,
        string realm,
        string? ownerKind,
        string? ownerId,
        string? providerKind,
        string? providerId,
        string? targetKind,
        string? targetId,
        string? locationId,
        MortalWoundTreatmentRequirementEvidence evidence,
        string witnessFingerprint)
        : this(
            scope,
            requirementIndex,
            kind,
            authorityRef,
            snapshotToken,
            realm,
            ownerKind,
            ownerId,
            providerKind,
            providerId,
            targetKind,
            targetId,
            locationId,
            evidence,
            Array.Empty<string?>(),
            witnessFingerprint)
    {
    }

    public string Scope { get; }
    public int RequirementIndex { get; }
    public string Kind { get; }
    public string AuthorityRef { get; }
    public string SnapshotToken { get; }
    public string Realm { get; }
    public string? OwnerKind { get; }
    public string? OwnerId { get; }
    public string? ProviderKind { get; }
    public string? ProviderId { get; }
    public string? TargetKind { get; }
    public string? TargetId { get; }
    public string? LocationId { get; }
    public MortalWoundTreatmentRequirementEvidence Evidence { get; }
    public string WitnessFingerprint { get; }
    internal IReadOnlyList<string?> T060MechanicalFields { get; }
}

internal sealed class MortalWoundTreatmentRequirementFailureObservation
{
    [JsonConstructor]
    internal MortalWoundTreatmentRequirementFailureObservation(
        string snapshotToken,
        string realm,
        string? ownerKind,
        string? ownerId,
        string? providerKind,
        string? providerId,
        string? targetKind,
        string? targetId,
        string? locationId,
        MortalWoundTreatmentRequirementEvidence evidence,
        string observationFingerprint)
    {
        SnapshotToken = snapshotToken;
        Realm = realm;
        OwnerKind = ownerKind;
        OwnerId = ownerId;
        ProviderKind = providerKind;
        ProviderId = providerId;
        TargetKind = targetKind;
        TargetId = targetId;
        LocationId = locationId;
        Evidence = evidence;
        ObservationFingerprint = observationFingerprint;
    }

    public string SnapshotToken { get; }
    public string Realm { get; }
    public string? OwnerKind { get; }
    public string? OwnerId { get; }
    public string? ProviderKind { get; }
    public string? ProviderId { get; }
    public string? TargetKind { get; }
    public string? TargetId { get; }
    public string? LocationId { get; }
    public MortalWoundTreatmentRequirementEvidence Evidence { get; }
    public string ObservationFingerprint { get; }
}

internal sealed class MortalWoundTreatmentRequirementFailureWitness
{
    private const string Domain =
        "book_of_eternity.mortal_wound_treatment.requirement_failure_witness";

    [JsonConstructor]
    private MortalWoundTreatmentRequirementFailureWitness(
        string scope,
        int requirementIndex,
        string kind,
        string authorityRef,
        string lossReason,
        MortalWoundTreatmentRequirementFailureObservation? observation,
        string witnessFingerprint)
    {
        Scope = scope;
        RequirementIndex = requirementIndex;
        Kind = kind;
        AuthorityRef = authorityRef;
        LossReason = lossReason;
        Observation = observation;
        WitnessFingerprint = witnessFingerprint;
    }

    public string Scope { get; }
    public int RequirementIndex { get; }
    public string Kind { get; }
    public string AuthorityRef { get; }
    public string LossReason { get; }
    public MortalWoundTreatmentRequirementFailureObservation? Observation { get; }
    public string WitnessFingerprint { get; }

    internal static MortalWoundTreatmentRequirementFailureWitness CreateAbsent(
        string scope,
        int requirementIndex,
        string kind,
        string authorityRef) => Create(
        scope,
        requirementIndex,
        kind,
        authorityRef,
        "authority_absent",
        null);

    internal static MortalWoundTreatmentRequirementFailureWitness Create(
        string scope,
        int requirementIndex,
        string kind,
        string authorityRef,
        string lossReason,
        MortalWoundTreatmentRequirementFailureObservation? observation) => new(
        scope,
        requirementIndex,
        kind,
        authorityRef,
        lossReason,
        observation,
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            Domain,
            "1",
            scope,
            requirementIndex.ToString(CultureInfo.InvariantCulture),
            kind,
            authorityRef,
            lossReason,
            observation?.ObservationFingerprint
        }));
}

internal static class MortalWoundTreatmentRequirementWitnessFactory
{
    private const string WitnessDomain =
        "book_of_eternity.mortal_wound_treatment.requirement_success_witness";
    private const string ObservationDomain =
        "book_of_eternity.mortal_wound_treatment.requirement_failure_observation";

    internal static bool TryCreateFailure(
        string scope,
        int requirementIndex,
        JsonElement requirement,
        string lossReason,
        MortalWoundTreatmentAuthority.Context context,
        MortalWoundTreatmentAuthority.Snapshot snapshot,
        IDictionary<string, long> cumulativeQuantities,
        out MortalWoundTreatmentRequirementFailureWitness? failure)
    {
        failure = null;
        var kind = String(requirement, "kind");
        if (!IsSupportedKind(kind))
            return false;
        var authorityRef = ReadAuthorityRef(requirement, kind);
        if (string.Equals(lossReason, "authority_absent", StringComparison.Ordinal))
        {
            if (string.IsNullOrEmpty(authorityRef) ||
                HasExactOrConfusableReference(snapshot, kind, authorityRef))
            {
                return false;
            }
            failure = MortalWoundTreatmentRequirementFailureWitness.CreateAbsent(
                scope,
                requirementIndex,
                kind,
                authorityRef);
            return true;
        }

        if (!TryCreateFailureRow(
                requirementIndex,
                requirement,
                kind,
                authorityRef,
                context,
                snapshot,
                out var row) ||
            row is null ||
            !TryCreateEvidence(
                requirement,
                row,
                context,
                snapshot,
                cumulativeQuantities,
                out var evidence,
                out var mechanicalFields) ||
            evidence is null ||
            mechanicalFields is null ||
            !string.Equals(row.Realm, context.Realm, StringComparison.Ordinal))
        {
            return false;
        }

        lossReason = NormalizeLossReason(lossReason, evidence, row, context);
        return EvidenceProvesLoss(lossReason, evidence, row, requirement, context) &&
               TryCreateObservedFailure(
                   scope,
                   requirementIndex,
                   kind,
                   authorityRef,
                   lossReason,
                   snapshot,
                   row,
                   evidence,
                   mechanicalFields,
                   out failure);
    }

    internal static bool TryCreateAggregateFailure(
        string scope,
        int requirementIndex,
        JsonElement requirement,
        MortalWoundResolvedRequirement row,
        MortalWoundTreatmentRequirementSuccessWitness witness,
        MortalWoundTreatmentAuthority.Context context,
        MortalWoundTreatmentAuthority.Snapshot snapshot,
        IDictionary<string, long> aggregateQuantities,
        out MortalWoundTreatmentRequirementFailureWitness? failure)
    {
        failure = null;
        if (row.Kind is not ("item_quantity" or "resource_quantity"))
            return true;

        var cumulative = AddCumulative(aggregateQuantities, row);
        MortalWoundTreatmentRequirementEvidence evidence = witness.Evidence switch
        {
            MortalWoundItemQuantityRequirementEvidence item => item with
            {
                CumulativeRequestedQuantity = cumulative
            },
            MortalWoundResourceQuantityRequirementEvidence resource => resource with
            {
                CumulativeRequestedQuantity = cumulative
            },
            _ => null!
        };
        if (evidence is null)
            return false;
        if (!EvidenceProvesLoss(
                "quantity_insufficient",
                evidence,
                row,
                requirement,
                context))
        {
            return true;
        }

        var mechanicalFields = witness.T060MechanicalFields.ToArray();
        if (mechanicalFields.Length == 0)
            return false;
        mechanicalFields[^1] = Number(cumulative);
        return TryCreateObservedFailure(
            scope,
            requirementIndex,
            row.Kind,
            row.AuthorityRef,
            "quantity_insufficient",
            snapshot,
            row,
            evidence,
            mechanicalFields,
            out failure);
    }

    private static bool TryCreateObservedFailure(
        string scope,
        int requirementIndex,
        string kind,
        string authorityRef,
        string lossReason,
        MortalWoundTreatmentAuthority.Snapshot snapshot,
        MortalWoundResolvedRequirement row,
        MortalWoundTreatmentRequirementEvidence evidence,
        IReadOnlyList<string?> mechanicalFields,
        out MortalWoundTreatmentRequirementFailureWitness? failure)
    {
        failure = null;
        var observationFields = new List<string?>
        {
            ObservationDomain,
            "1",
            snapshot.SnapshotToken,
            row.Realm,
            row.OwnerKind,
            row.OwnerId,
            row.ProviderKind,
            row.ProviderId,
            row.TargetKind,
            row.TargetId,
            row.LocationId,
            evidence.Kind,
            mechanicalFields.Count.ToString(CultureInfo.InvariantCulture)
        };
        observationFields.AddRange(mechanicalFields);
        var evidenceFields = EvidenceFingerprintFields(evidence);
        observationFields.Add(evidenceFields.Count.ToString(CultureInfo.InvariantCulture));
        observationFields.AddRange(evidenceFields);
        var observation = new MortalWoundTreatmentRequirementFailureObservation(
            snapshot.SnapshotToken,
            row.Realm,
            row.OwnerKind,
            row.OwnerId,
            row.ProviderKind,
            row.ProviderId,
            row.TargetKind,
            row.TargetId,
            row.LocationId,
            evidence,
            WoundAcceptedTurnFingerprintWriter.Compute(observationFields));
        failure = MortalWoundTreatmentRequirementFailureWitness.Create(
            scope,
            requirementIndex,
            kind,
            authorityRef,
            lossReason,
            observation);
        return true;
    }

    internal static bool TryCreateSuccess(
        string scope,
        JsonElement requirement,
        MortalWoundResolvedRequirement row,
        MortalWoundTreatmentAuthority.Context context,
        MortalWoundTreatmentAuthority.Snapshot snapshot,
        IDictionary<string, long> cumulativeQuantities,
        out MortalWoundTreatmentRequirementSuccessWitness? witness)
    {
        witness = null;
        if (!TryCreateEvidence(
                requirement,
                row,
                context,
                snapshot,
                cumulativeQuantities,
                out var evidence,
                out var mechanicalFields) ||
            evidence is null ||
            mechanicalFields is null)
        {
            return false;
        }

        var fields = new List<string?>
        {
            WitnessDomain,
            "1",
            scope,
            row.RequirementIndex.ToString(CultureInfo.InvariantCulture),
            row.Kind,
            row.AuthorityRef,
            snapshot.SnapshotToken,
            row.Realm,
            row.OwnerKind,
            row.OwnerId,
            row.ProviderKind,
            row.ProviderId,
            row.TargetKind,
            row.TargetId,
            row.LocationId,
            evidence.Kind,
            mechanicalFields.Count.ToString(CultureInfo.InvariantCulture)
        };
        fields.AddRange(mechanicalFields);
        var evidenceFields = EvidenceFingerprintFields(evidence);
        fields.Add(evidenceFields.Count.ToString(CultureInfo.InvariantCulture));
        fields.AddRange(evidenceFields);
        witness = new MortalWoundTreatmentRequirementSuccessWitness(
            scope,
            row.RequirementIndex,
            row.Kind,
            row.AuthorityRef,
            snapshot.SnapshotToken,
            row.Realm,
            row.OwnerKind,
            row.OwnerId,
            row.ProviderKind,
            row.ProviderId,
            row.TargetKind,
            row.TargetId,
            row.LocationId,
            evidence,
            mechanicalFields,
            WoundAcceptedTurnFingerprintWriter.Compute(fields));
        return true;
    }

    private static bool TryCreateEvidence(
        JsonElement requirement,
        MortalWoundResolvedRequirement row,
        MortalWoundTreatmentAuthority.Context context,
        MortalWoundTreatmentAuthority.Snapshot snapshot,
        IDictionary<string, long> cumulativeQuantities,
        out MortalWoundTreatmentRequirementEvidence? evidence,
        out IReadOnlyList<string?>? mechanicalFields)
    {
        evidence = null;
        mechanicalFields = null;
        switch (row.Kind)
        {
            case "item_quantity":
            {
                var item = Single(snapshot.Items.Where(candidate =>
                    SameOwner(candidate.OwnerKind, candidate.OwnerId, row) &&
                    string.Equals(candidate.ItemId, row.AuthorityRef, StringComparison.Ordinal)));
                if (item is null || row.RequestedQuantity is null)
                    return false;
                var cumulative = AddCumulative(cumulativeQuantities, row);
                evidence = new MortalWoundItemQuantityRequirementEvidence(
                    String(requirement, "ownerRole"),
                    row.RequestedQuantity.Value,
                    item.Count,
                    item.AvailableCount,
                    item.ReservationState,
                    item.Lifecycle,
                    item.Active,
                    cumulative);
                mechanicalFields = new string?[]
                {
                    item.ItemId, item.Realm, item.OwnerKind, item.OwnerId,
                    Number(item.Count), Number(item.AvailableCount), item.ReservationState,
                    item.Lifecycle, Boolean(item.Active), Number(cumulative)
                };
                return true;
            }
            case "resource_quantity":
            {
                var resource = Single(snapshot.Resources.Where(candidate =>
                    SameOwner(candidate.OwnerKind, candidate.OwnerId, row) &&
                    string.Equals(candidate.ResourceRef, row.AuthorityRef, StringComparison.Ordinal)));
                if (resource is null || row.RequestedQuantity is null)
                    return false;
                var cumulative = AddCumulative(cumulativeQuantities, row);
                evidence = new MortalWoundResourceQuantityRequirementEvidence(
                    String(requirement, "ownerRole"),
                    row.RequestedQuantity.Value,
                    resource.CurrentValue,
                    resource.AvailableValue,
                    resource.ReservationState,
                    resource.Lifecycle,
                    resource.Active,
                    cumulative);
                mechanicalFields = new string?[]
                {
                    resource.ResourceRef, resource.Realm, resource.OwnerKind, resource.OwnerId,
                    Number(resource.CurrentValue), Number(resource.AvailableValue),
                    resource.ReservationState, resource.Lifecycle, Boolean(resource.Active),
                    Number(cumulative)
                };
                return true;
            }
            case "skill_tier":
            {
                var actor = FindActor(snapshot, row.OwnerKind, row.OwnerId);
                var skill = actor is null
                    ? null
                    : Single(actor.Skills.Where(candidate => string.Equals(
                        candidate.CapabilityRef,
                        row.AuthorityRef,
                        StringComparison.Ordinal)));
                if (actor is null || skill is null || row.MinimumTier is null ||
                    row.CurrentTier is null)
                {
                    return false;
                }
                var present = IsPresent(snapshot, context.CurrentLocationId, actor);
                evidence = new MortalWoundSkillTierRequirementEvidence(
                    String(requirement, "actorRole"),
                    row.MinimumTier.Value,
                    row.CurrentTier.Value,
                    actor.CurrentLocationId,
                    context.CurrentLocationId,
                    actor.Lifecycle,
                    actor.Active,
                    actor.Reachable,
                    present,
                    skill.Lifecycle,
                    skill.Active);
                mechanicalFields = ActorFields(actor).Concat(new string?[]
                {
                    skill.CapabilityRef, Number(skill.Tier), skill.Lifecycle,
                    Boolean(skill.Active)
                }).ToArray();
                return true;
            }
            case "source_capability":
            {
                var actor = FindActor(snapshot, row.OwnerKind, row.OwnerId);
                var capability = actor is null
                    ? null
                    : Single(actor.Capabilities.Where(candidate => string.Equals(
                        candidate.CapabilityRef,
                        row.AuthorityRef,
                        StringComparison.Ordinal)));
                if (actor is null || capability is null)
                    return false;
                var present = IsPresent(snapshot, context.CurrentLocationId, actor);
                evidence = new MortalWoundSourceCapabilityRequirementEvidence(
                    String(requirement, "actorRole"),
                    actor.CurrentLocationId,
                    context.CurrentLocationId,
                    actor.Lifecycle,
                    actor.Active,
                    actor.Reachable,
                    present,
                    capability.Lifecycle,
                    capability.Active);
                mechanicalFields = ActorFields(actor).Concat(new string?[]
                {
                    capability.CapabilityRef, capability.Lifecycle,
                    Boolean(capability.Active)
                }).ToArray();
                return true;
            }
            case "provider":
            {
                var actor = FindActor(snapshot, row.ProviderKind, row.ProviderId);
                if (actor is null)
                    return false;
                var present = IsPresent(snapshot, context.CurrentLocationId, actor);
                evidence = new MortalWoundProviderRequirementEvidence(
                    actor.CurrentLocationId,
                    context.CurrentLocationId,
                    actor.Lifecycle,
                    actor.Active,
                    actor.Reachable,
                    present);
                mechanicalFields = ActorFields(actor)
                    .Concat(new[] { Boolean(present) })
                    .ToArray();
                return true;
            }
            default:
                return TryCreateRemainingEvidence(
                    requirement,
                    row,
                    context,
                    snapshot,
                    out evidence,
                    out mechanicalFields);
        }
    }

    private static bool TryCreateRemainingEvidence(
        JsonElement requirement,
        MortalWoundResolvedRequirement row,
        MortalWoundTreatmentAuthority.Context context,
        MortalWoundTreatmentAuthority.Snapshot snapshot,
        out MortalWoundTreatmentRequirementEvidence? evidence,
        out IReadOnlyList<string?>? mechanicalFields)
    {
        evidence = null;
        mechanicalFields = null;
        switch (row.Kind)
        {
            case "consent":
            {
                var actor = FindActor(snapshot, row.ProviderKind, row.ProviderId);
                var consent = actor is null
                    ? null
                    : Single(actor.Consents.Where(candidate => string.Equals(
                        candidate.ConsentRef,
                        row.AuthorityRef,
                        StringComparison.Ordinal)));
                if (actor is null || consent is null)
                    return false;
                var present = IsPresent(snapshot, context.CurrentLocationId, actor);
                evidence = new MortalWoundConsentRequirementEvidence(
                    String(requirement, "providerRef"),
                    String(requirement, "targetRef"),
                    actor.CurrentLocationId,
                    context.CurrentLocationId,
                    actor.Lifecycle,
                    actor.Active,
                    actor.Reachable,
                    present,
                    consent.Status,
                    consent.Lifecycle,
                    consent.Active);
                mechanicalFields = ActorFields(actor).Concat(new string?[]
                {
                    consent.ConsentRef, consent.ProviderKind, consent.ProviderId,
                    consent.TargetKind, consent.TargetId, consent.Status,
                    consent.Lifecycle, Boolean(consent.Active)
                }).ToArray();
                return true;
            }
            case "facility":
            {
                var facility = Single(snapshot.Facilities.Where(candidate => string.Equals(
                    candidate.FacilityId,
                    row.AuthorityRef,
                    StringComparison.Ordinal)));
                if (facility is null)
                    return false;
                var targetPresent = IsPresent(
                    snapshot,
                    context.CurrentLocationId,
                    context.TargetKind,
                    context.TargetId);
                evidence = new MortalWoundFacilityRequirementEvidence(
                    facility.LocationId,
                    context.CurrentLocationId,
                    facility.Lifecycle,
                    facility.Active,
                    facility.Available,
                    context.TargetKind,
                    context.TargetId,
                    targetPresent);
                mechanicalFields = new string?[]
                {
                    facility.FacilityId, facility.Realm, facility.LocationId,
                    facility.Lifecycle, Boolean(facility.Active),
                    Boolean(facility.Available)
                };
                return true;
            }
            case "location":
            {
                var location = Single(snapshot.Locations.Where(candidate => string.Equals(
                    candidate.LocationId,
                    row.AuthorityRef,
                    StringComparison.Ordinal)));
                if (location is null)
                    return false;
                var present = location.PresentActors.Any(actor =>
                    string.Equals(actor.ActorKind, context.TargetKind, StringComparison.Ordinal) &&
                    string.Equals(actor.ActorId, context.TargetId, StringComparison.Ordinal));
                var actors = Array.AsReadOnly(location.PresentActors
                    .Select(static actor => actor with { })
                    .ToArray());
                evidence = new MortalWoundLocationRequirementEvidence(
                    String(requirement, "targetRole"),
                    location.LocationId,
                    context.CurrentLocationId,
                    location.Lifecycle,
                    location.Active,
                    present,
                    actors);
                var fields = new List<string?>
                {
                    location.LocationId, location.Realm, location.Lifecycle,
                    Boolean(location.Active), Number(location.PresentActors.Count)
                };
                foreach (var actor in location.PresentActors)
                {
                    fields.Add(actor.ActorKind);
                    fields.Add(actor.ActorId);
                }
                mechanicalFields = fields;
                return true;
            }
            case "quest_state":
            {
                var quest = Single(snapshot.Quests.Where(candidate => string.Equals(
                    candidate.QuestId,
                    row.AuthorityRef,
                    StringComparison.Ordinal)));
                if (quest is null)
                    return false;
                evidence = new MortalWoundQuestStateRequirementEvidence(
                    String(requirement, "requiredState"),
                    quest.State,
                    quest.Lifecycle,
                    quest.Active);
                mechanicalFields = new string?[]
                {
                    quest.QuestId, quest.Realm, quest.State, quest.Lifecycle,
                    Boolean(quest.Active)
                };
                return true;
            }
            case "effect_state":
            {
                var effect = Single(snapshot.Effects.Where(candidate => string.Equals(
                    candidate.EffectId,
                    row.AuthorityRef,
                    StringComparison.Ordinal)));
                if (effect is null)
                    return false;
                evidence = new MortalWoundEffectStateRequirementEvidence(
                    String(requirement, "requiredState"),
                    effect.State,
                    effect.Lifecycle,
                    effect.Active);
                mechanicalFields = new string?[]
                {
                    effect.EffectId, effect.Realm, effect.TargetKind, effect.TargetId,
                    effect.State, effect.Lifecycle, Boolean(effect.Active)
                };
                return true;
            }
            case "environment":
            {
                var environment = Single(snapshot.Environments.Where(candidate => string.Equals(
                    candidate.EnvironmentId,
                    row.AuthorityRef,
                    StringComparison.Ordinal)));
                if (environment is null)
                    return false;
                evidence = new MortalWoundEnvironmentRequirementEvidence(
                    String(requirement, "requiredState"),
                    environment.State,
                    environment.LocationId,
                    context.CurrentLocationId,
                    environment.Lifecycle,
                    environment.Active);
                mechanicalFields = new string?[]
                {
                    environment.EnvironmentId, environment.Realm,
                    environment.LocationId, environment.State,
                    environment.Lifecycle, Boolean(environment.Active)
                };
                return true;
            }
            default:
                return false;
        }
    }

    private static IReadOnlyList<string?> EvidenceFingerprintFields(
        MortalWoundTreatmentRequirementEvidence evidence)
    {
        var fields = new List<string?> { evidence.Kind };
        switch (evidence)
        {
            case MortalWoundItemQuantityRequirementEvidence item:
                fields.AddRange(new string?[]
                {
                    item.OwnerRole, Number(item.RequestedQuantity), Number(item.Count),
                    Number(item.AvailableCount), item.ReservationState, item.Lifecycle,
                    Boolean(item.Active), Number(item.CumulativeRequestedQuantity)
                });
                break;
            case MortalWoundResourceQuantityRequirementEvidence resource:
                fields.AddRange(new string?[]
                {
                    resource.OwnerRole, Number(resource.RequestedQuantity),
                    Number(resource.CurrentValue), Number(resource.AvailableValue),
                    resource.ReservationState, resource.Lifecycle,
                    Boolean(resource.Active), Number(resource.CumulativeRequestedQuantity)
                });
                break;
            case MortalWoundSkillTierRequirementEvidence skill:
                fields.AddRange(new string?[]
                {
                    skill.ActorRole, Number(skill.MinimumTier), Number(skill.CurrentTier),
                    skill.ActorCurrentLocationId, skill.RequiredLocationId,
                    skill.ActorLifecycle, Boolean(skill.ActorActive),
                    Boolean(skill.ActorReachable), Boolean(skill.ActorPresent),
                    skill.SkillLifecycle, Boolean(skill.SkillActive)
                });
                break;
            case MortalWoundSourceCapabilityRequirementEvidence capability:
                fields.AddRange(new string?[]
                {
                    capability.ActorRole, capability.ActorCurrentLocationId,
                    capability.RequiredLocationId, capability.ActorLifecycle,
                    Boolean(capability.ActorActive), Boolean(capability.ActorReachable),
                    Boolean(capability.ActorPresent), capability.CapabilityLifecycle,
                    Boolean(capability.CapabilityActive)
                });
                break;
            case MortalWoundProviderRequirementEvidence provider:
                fields.AddRange(new string?[]
                {
                    provider.CurrentLocationId, provider.RequiredLocationId,
                    provider.Lifecycle, Boolean(provider.Active),
                    Boolean(provider.Reachable), Boolean(provider.Present)
                });
                break;
            case MortalWoundConsentRequirementEvidence consent:
                fields.AddRange(new string?[]
                {
                    consent.RequiredProviderRef, consent.RequiredTargetRef,
                    consent.ProviderCurrentLocationId, consent.RequiredLocationId,
                    consent.ProviderLifecycle, Boolean(consent.ProviderActive),
                    Boolean(consent.ProviderReachable), Boolean(consent.ProviderPresent),
                    consent.Status, consent.Lifecycle, Boolean(consent.Active)
                });
                break;
            case MortalWoundFacilityRequirementEvidence facility:
                fields.AddRange(new string?[]
                {
                    facility.FacilityLocationId, facility.RequiredLocationId,
                    facility.Lifecycle, Boolean(facility.Active),
                    Boolean(facility.Available), facility.RequiredActorKind,
                    facility.RequiredActorId, Boolean(facility.RequiredActorPresent)
                });
                break;
            case MortalWoundLocationRequirementEvidence location:
                fields.AddRange(new string?[]
                {
                    location.TargetRole, location.AuthorityLocationId,
                    location.RequiredLocationId, location.Lifecycle,
                    Boolean(location.Active), Boolean(location.TargetPresent),
                    Number(location.PresentActors.Count)
                });
                foreach (var actor in location.PresentActors)
                {
                    fields.Add(actor.ActorKind);
                    fields.Add(actor.ActorId);
                }
                break;
            case MortalWoundQuestStateRequirementEvidence quest:
                fields.AddRange(new string?[]
                {
                    quest.RequiredState, quest.CurrentState, quest.Lifecycle,
                    Boolean(quest.Active)
                });
                break;
            case MortalWoundEffectStateRequirementEvidence effect:
                fields.AddRange(new string?[]
                {
                    effect.RequiredState, effect.CurrentState, effect.Lifecycle,
                    Boolean(effect.Active)
                });
                break;
            case MortalWoundEnvironmentRequirementEvidence environment:
                fields.AddRange(new string?[]
                {
                    environment.RequiredState, environment.CurrentState,
                    environment.CurrentLocationId, environment.RequiredLocationId,
                    environment.Lifecycle, Boolean(environment.Active)
                });
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported Mortal wound-treatment evidence kind '{evidence.Kind}'.");
        }
        return fields;
    }

    private static bool TryCreateFailureRow(
        int requirementIndex,
        JsonElement requirement,
        string kind,
        string authorityRef,
        MortalWoundTreatmentAuthority.Context context,
        MortalWoundTreatmentAuthority.Snapshot snapshot,
        out MortalWoundResolvedRequirement? row)
    {
        row = null;
        switch (kind)
        {
            case "item_quantity":
            {
                var expected = SelectRole(context, String(requirement, "ownerRole"));
                var item = SelectOwnedCandidate(
                    snapshot.Items.Where(candidate => string.Equals(
                        candidate.ItemId,
                        authorityRef,
                        StringComparison.Ordinal)),
                    expected,
                    static candidate => (candidate.OwnerKind, candidate.OwnerId));
                if (item is null)
                    return false;
                row = FailureRow(
                    requirementIndex,
                    kind,
                    authorityRef,
                    item.Realm,
                    ownerKind: item.OwnerKind,
                    ownerId: item.OwnerId,
                    requestedQuantity: Int32(requirement, "quantity"));
                return true;
            }
            case "resource_quantity":
            {
                var expected = SelectRole(context, String(requirement, "ownerRole"));
                var resource = SelectOwnedCandidate(
                    snapshot.Resources.Where(candidate => string.Equals(
                        candidate.ResourceRef,
                        authorityRef,
                        StringComparison.Ordinal)),
                    expected,
                    static candidate => (candidate.OwnerKind, candidate.OwnerId));
                if (resource is null)
                    return false;
                row = FailureRow(
                    requirementIndex,
                    kind,
                    authorityRef,
                    resource.Realm,
                    ownerKind: resource.OwnerKind,
                    ownerId: resource.OwnerId,
                    requestedQuantity: Int32(requirement, "quantity"));
                return true;
            }
            case "skill_tier":
            {
                var expected = SelectRole(context, String(requirement, "actorRole"));
                var matches = snapshot.Actors.SelectMany(actor => actor.Skills
                        .Where(skill => string.Equals(
                            skill.CapabilityRef,
                            authorityRef,
                            StringComparison.Ordinal))
                        .Select(skill => (Actor: actor, Skill: skill)))
                    .ToArray();
                var selected = SelectOwnedCandidate(
                    matches,
                    expected,
                    static candidate =>
                        (candidate.Actor.ActorKind, candidate.Actor.ActorId));
                if (selected.Actor is null || selected.Skill is null)
                    return false;
                row = FailureRow(
                    requirementIndex,
                    kind,
                    authorityRef,
                    selected.Actor.Realm,
                    ownerKind: selected.Actor.ActorKind,
                    ownerId: selected.Actor.ActorId,
                    minimumTier: Int32(requirement, "minimumTier"),
                    currentTier: selected.Skill.Tier);
                return true;
            }
            case "source_capability":
            {
                var expected = SelectRole(context, String(requirement, "actorRole"));
                var matches = snapshot.Actors.SelectMany(actor => actor.Capabilities
                        .Where(capability => string.Equals(
                            capability.CapabilityRef,
                            authorityRef,
                            StringComparison.Ordinal))
                        .Select(capability => (Actor: actor, Capability: capability)))
                    .ToArray();
                var selected = SelectOwnedCandidate(
                    matches,
                    expected,
                    static candidate =>
                        (candidate.Actor.ActorKind, candidate.Actor.ActorId));
                if (selected.Actor is null || selected.Capability is null)
                    return false;
                row = FailureRow(
                    requirementIndex,
                    kind,
                    authorityRef,
                    selected.Actor.Realm,
                    ownerKind: selected.Actor.ActorKind,
                    ownerId: selected.Actor.ActorId);
                return true;
            }
            case "provider":
            {
                var actor = Single(snapshot.Actors.Where(candidate => string.Equals(
                    candidate.ActorId,
                    authorityRef,
                    StringComparison.Ordinal)));
                if (actor is null)
                    return false;
                row = FailureRow(
                    requirementIndex,
                    kind,
                    authorityRef,
                    actor.Realm,
                    providerKind: actor.ActorKind,
                    providerId: actor.ActorId,
                    locationId: context.CurrentLocationId);
                return true;
            }
            case "consent":
            {
                var matches = snapshot.Actors.SelectMany(actor => actor.Consents
                        .Where(consent => string.Equals(
                            consent.ConsentRef,
                            authorityRef,
                            StringComparison.Ordinal))
                        .Select(consent => (Actor: actor, Consent: consent)))
                    .ToArray();
                if (matches.Length != 1)
                    return false;
                var selected = matches[0];
                row = FailureRow(
                    requirementIndex,
                    kind,
                    authorityRef,
                    selected.Actor.Realm,
                    providerKind: selected.Consent.ProviderKind,
                    providerId: selected.Consent.ProviderId,
                    targetKind: selected.Consent.TargetKind,
                    targetId: selected.Consent.TargetId,
                    currentState: selected.Consent.Status);
                return true;
            }
            case "facility":
            {
                var facility = Single(snapshot.Facilities.Where(candidate => string.Equals(
                    candidate.FacilityId,
                    authorityRef,
                    StringComparison.Ordinal)));
                if (facility is null)
                    return false;
                row = FailureRow(
                    requirementIndex,
                    kind,
                    authorityRef,
                    facility.Realm,
                    locationId: facility.LocationId);
                return true;
            }
            case "location":
            {
                var location = Single(snapshot.Locations.Where(candidate => string.Equals(
                    candidate.LocationId,
                    authorityRef,
                    StringComparison.Ordinal)));
                if (location is null)
                    return false;
                row = FailureRow(
                    requirementIndex,
                    kind,
                    authorityRef,
                    location.Realm,
                    targetKind: context.TargetKind,
                    targetId: context.TargetId,
                    locationId: location.LocationId);
                return true;
            }
            case "quest_state":
            {
                var quest = Single(snapshot.Quests.Where(candidate => string.Equals(
                    candidate.QuestId,
                    authorityRef,
                    StringComparison.Ordinal)));
                if (quest is null)
                    return false;
                row = FailureRow(
                    requirementIndex,
                    kind,
                    authorityRef,
                    quest.Realm,
                    currentState: quest.State);
                return true;
            }
            case "effect_state":
            {
                var effect = Single(snapshot.Effects.Where(candidate => string.Equals(
                    candidate.EffectId,
                    authorityRef,
                    StringComparison.Ordinal)));
                if (effect is null)
                    return false;
                row = FailureRow(
                    requirementIndex,
                    kind,
                    authorityRef,
                    effect.Realm,
                    targetKind: effect.TargetKind,
                    targetId: effect.TargetId,
                    currentState: effect.State);
                return true;
            }
            case "environment":
            {
                var environment = Single(snapshot.Environments.Where(candidate => string.Equals(
                    candidate.EnvironmentId,
                    authorityRef,
                    StringComparison.Ordinal)));
                if (environment is null)
                    return false;
                row = FailureRow(
                    requirementIndex,
                    kind,
                    authorityRef,
                    environment.Realm,
                    locationId: environment.LocationId,
                    currentState: environment.State);
                return true;
            }
            default:
                return false;
        }
    }

    private static bool EvidenceProvesLoss(
        string lossReason,
        MortalWoundTreatmentRequirementEvidence evidence,
        MortalWoundResolvedRequirement row,
        JsonElement requirement,
        MortalWoundTreatmentAuthority.Context context) => lossReason switch
    {
        "quantity_insufficient" => evidence switch
        {
            MortalWoundItemQuantityRequirementEvidence item =>
                item.CumulativeRequestedQuantity > item.Count ||
                item.CumulativeRequestedQuantity > item.AvailableCount,
            MortalWoundResourceQuantityRequirementEvidence resource =>
                resource.CumulativeRequestedQuantity > resource.CurrentValue ||
                resource.CumulativeRequestedQuantity > resource.AvailableValue,
            _ => false
        },
        "reserved" => evidence switch
        {
            MortalWoundItemQuantityRequirementEvidence item =>
                !string.Equals(item.ReservationState, "available", StringComparison.Ordinal),
            MortalWoundResourceQuantityRequirementEvidence resource =>
                !string.Equals(resource.ReservationState, "available", StringComparison.Ordinal),
            _ => false
        },
        "tier_insufficient" => evidence is MortalWoundSkillTierRequirementEvidence skill &&
                               skill.CurrentTier < skill.MinimumTier,
        "retired" => HasRetiredLifecycle(evidence),
        "inactive" => HasInactiveAuthority(evidence),
        "state_mismatch" => evidence switch
        {
            MortalWoundQuestStateRequirementEvidence state =>
                !string.Equals(state.CurrentState, state.RequiredState, StringComparison.Ordinal),
            MortalWoundEffectStateRequirementEvidence state =>
                !string.Equals(state.CurrentState, state.RequiredState, StringComparison.Ordinal),
            MortalWoundEnvironmentRequirementEvidence state =>
                !string.Equals(state.CurrentState, state.RequiredState, StringComparison.Ordinal),
            _ => false
        },
        "owner_unavailable" => SelectRole(
                                   context,
                                   String(
                                       requirement,
                                       row.Kind is "skill_tier" or "source_capability"
                                           ? "actorRole"
                                           : "ownerRole")) is { } expected &&
                               (!string.Equals(row.OwnerKind, expected.Kind, StringComparison.Ordinal) ||
                                !string.Equals(row.OwnerId, expected.Id, StringComparison.Ordinal)),
        "provider_unreachable" => evidence is MortalWoundProviderRequirementEvidence provider &&
                                  !provider.Reachable,
        "consent_absent" => evidence is MortalWoundConsentRequirementEvidence consent &&
                            !string.Equals(consent.Status, "granted", StringComparison.Ordinal),
        "facility_unavailable" => evidence is MortalWoundFacilityRequirementEvidence facility &&
                                  !facility.Available,
        "wrong_location" => evidence switch
        {
            MortalWoundProviderRequirementEvidence provider => !string.Equals(
                provider.CurrentLocationId,
                provider.RequiredLocationId,
                StringComparison.Ordinal),
            MortalWoundFacilityRequirementEvidence facility => !string.Equals(
                facility.FacilityLocationId,
                facility.RequiredLocationId,
                StringComparison.Ordinal),
            MortalWoundEnvironmentRequirementEvidence environment => !string.Equals(
                environment.CurrentLocationId,
                environment.RequiredLocationId,
                StringComparison.Ordinal),
            MortalWoundLocationRequirementEvidence => !string.Equals(
                row.LocationId,
                context.CurrentLocationId,
                StringComparison.Ordinal),
            _ => false
        },
        "actor_not_present" => evidence switch
        {
            MortalWoundProviderRequirementEvidence provider => !provider.Present,
            MortalWoundLocationRequirementEvidence location => !location.TargetPresent,
            _ => false
        },
        _ => false
    };

    private static string NormalizeLossReason(
        string classifiedReason,
        MortalWoundTreatmentRequirementEvidence evidence,
        MortalWoundResolvedRequirement row,
        MortalWoundTreatmentAuthority.Context context) => classifiedReason switch
    {
        "retired" when !HasRetiredLifecycle(evidence) && HasInactiveAuthority(evidence) =>
            "inactive",
        "consent_absent" when evidence is MortalWoundConsentRequirementEvidence consent &&
                              string.Equals(consent.Status, "granted", StringComparison.Ordinal) &&
                              (!consent.ProviderActive || !consent.Active) =>
            "inactive",
        "facility_unavailable" when evidence is MortalWoundFacilityRequirementEvidence facility &&
                                    !facility.Active =>
            "inactive",
        "provider_unreachable" when evidence is MortalWoundProviderRequirementEvidence provider &&
                                    provider.Reachable &&
                                    !string.Equals(
                                        provider.CurrentLocationId,
                                        provider.RequiredLocationId,
                                        StringComparison.Ordinal) =>
            "wrong_location",
        "provider_unreachable" when evidence is MortalWoundProviderRequirementEvidence provider &&
                                    provider.Reachable &&
                                    !provider.Present =>
            "actor_not_present",
        "actor_not_present" when evidence is MortalWoundLocationRequirementEvidence &&
                                 !string.Equals(
                                     row.LocationId,
                                     context.CurrentLocationId,
                                     StringComparison.Ordinal) =>
            "wrong_location",
        _ => classifiedReason
    };

    private static bool HasRetiredLifecycle(
        MortalWoundTreatmentRequirementEvidence evidence) => evidence switch
    {
        MortalWoundItemQuantityRequirementEvidence value =>
            !string.Equals(value.Lifecycle, "active", StringComparison.Ordinal),
        MortalWoundResourceQuantityRequirementEvidence value =>
            !string.Equals(value.Lifecycle, "active", StringComparison.Ordinal),
        MortalWoundSkillTierRequirementEvidence value =>
            !string.Equals(value.ActorLifecycle, "active", StringComparison.Ordinal) ||
            !string.Equals(value.SkillLifecycle, "active", StringComparison.Ordinal),
        MortalWoundSourceCapabilityRequirementEvidence value =>
            !string.Equals(value.ActorLifecycle, "active", StringComparison.Ordinal) ||
            !string.Equals(value.CapabilityLifecycle, "active", StringComparison.Ordinal),
        MortalWoundProviderRequirementEvidence value =>
            !string.Equals(value.Lifecycle, "active", StringComparison.Ordinal),
        MortalWoundConsentRequirementEvidence value =>
            !string.Equals(value.ProviderLifecycle, "active", StringComparison.Ordinal) ||
            !string.Equals(value.Lifecycle, "active", StringComparison.Ordinal),
        MortalWoundFacilityRequirementEvidence value =>
            !string.Equals(value.Lifecycle, "active", StringComparison.Ordinal),
        MortalWoundLocationRequirementEvidence value =>
            !string.Equals(value.Lifecycle, "active", StringComparison.Ordinal),
        MortalWoundQuestStateRequirementEvidence value =>
            !string.Equals(value.Lifecycle, "active", StringComparison.Ordinal),
        MortalWoundEffectStateRequirementEvidence value =>
            !string.Equals(value.Lifecycle, "active", StringComparison.Ordinal),
        MortalWoundEnvironmentRequirementEvidence value =>
            !string.Equals(value.Lifecycle, "active", StringComparison.Ordinal),
        _ => false
    };

    private static bool HasInactiveAuthority(
        MortalWoundTreatmentRequirementEvidence evidence) => evidence switch
    {
        MortalWoundItemQuantityRequirementEvidence value => !value.Active,
        MortalWoundResourceQuantityRequirementEvidence value => !value.Active,
        MortalWoundSkillTierRequirementEvidence value =>
            !value.ActorActive || !value.SkillActive,
        MortalWoundSourceCapabilityRequirementEvidence value =>
            !value.ActorActive || !value.CapabilityActive,
        MortalWoundProviderRequirementEvidence value => !value.Active,
        MortalWoundConsentRequirementEvidence value =>
            !value.ProviderActive || !value.Active,
        MortalWoundFacilityRequirementEvidence value => !value.Active,
        MortalWoundLocationRequirementEvidence value => !value.Active,
        MortalWoundQuestStateRequirementEvidence value => !value.Active,
        MortalWoundEffectStateRequirementEvidence value => !value.Active,
        MortalWoundEnvironmentRequirementEvidence value => !value.Active,
        _ => false
    };

    private static MortalWoundResolvedRequirement FailureRow(
        int requirementIndex,
        string kind,
        string authorityRef,
        string realm,
        string? ownerKind = null,
        string? ownerId = null,
        string? providerKind = null,
        string? providerId = null,
        string? targetKind = null,
        string? targetId = null,
        string? locationId = null,
        int? requestedQuantity = null,
        int? minimumTier = null,
        int? currentTier = null,
        string? currentState = null) => new(
        requirementIndex,
        kind,
        authorityRef,
        realm,
        ownerKind,
        ownerId,
        providerKind,
        providerId,
        targetKind,
        targetId,
        locationId,
        requestedQuantity,
        minimumTier,
        currentTier,
        currentState,
        "sha256:" + new string('0', 64));

    private static (string Kind, string Id)? SelectRole(
        MortalWoundTreatmentAuthority.Context context,
        string role) => role switch
    {
        "provider" => (context.ProviderKind, context.ProviderId),
        "target" => (context.TargetKind, context.TargetId),
        _ => null
    };

    private static T? SelectOwnedCandidate<T>(
        IEnumerable<T> source,
        (string Kind, string Id)? expected,
        Func<T, (string Kind, string Id)> owner)
    {
        var values = source.ToArray();
        if (expected is { } coordinate)
        {
            var owned = values.Where(value =>
            {
                var actual = owner(value);
                return string.Equals(actual.Kind, coordinate.Kind, StringComparison.Ordinal) &&
                       string.Equals(actual.Id, coordinate.Id, StringComparison.Ordinal);
            }).ToArray();
            if (owned.Length == 1)
                return owned[0];
            if (owned.Length > 1)
                return default;
        }
        return values.Length == 1 ? values[0] : default;
    }

    private static string ReadAuthorityRef(JsonElement requirement, string kind)
    {
        var property = kind switch
        {
            "item_quantity" => "itemRef",
            "resource_quantity" => "resourceRef",
            "skill_tier" or "source_capability" => "capabilityRef",
            "provider" => "providerRef",
            "consent" => "consentRef",
            "facility" => "facilityRef",
            "location" => "locationRef",
            "quest_state" => "questRef",
            "effect_state" => "effectRef",
            "environment" => "environmentRef",
            _ => string.Empty
        };
        return !string.IsNullOrEmpty(property) &&
               requirement.TryGetProperty(property, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static bool IsSupportedKind(string kind) => kind is
        "item_quantity" or
        "resource_quantity" or
        "skill_tier" or
        "source_capability" or
        "provider" or
        "consent" or
        "facility" or
        "location" or
        "quest_state" or
        "effect_state" or
        "environment";

    private static bool HasExactOrConfusableReference(
        MortalWoundTreatmentAuthority.Snapshot snapshot,
        string kind,
        string authorityRef)
    {
        IEnumerable<string> candidates = kind switch
        {
            "item_quantity" => snapshot.Items.Select(static value => value.ItemId),
            "resource_quantity" => snapshot.Resources.Select(static value => value.ResourceRef),
            "skill_tier" => snapshot.Actors.SelectMany(static actor =>
                actor.Skills.Select(static value => value.CapabilityRef)),
            "source_capability" => snapshot.Actors.SelectMany(static actor =>
                actor.Capabilities.Select(static value => value.CapabilityRef)),
            "provider" => snapshot.Actors.Select(static value => value.ActorId),
            "consent" => snapshot.Actors.SelectMany(static actor =>
                actor.Consents.Select(static value => value.ConsentRef)),
            "facility" => snapshot.Facilities.Select(static value => value.FacilityId),
            "location" => snapshot.Locations.Select(static value => value.LocationId),
            "quest_state" => snapshot.Quests.Select(static value => value.QuestId),
            "effect_state" => snapshot.Effects.Select(static value => value.EffectId),
            "environment" => snapshot.Environments.Select(static value => value.EnvironmentId),
            _ => Array.Empty<string>()
        };
        var alias = ExactIdentifierConfusableKey.Build(authorityRef);
        return candidates.Any(candidate =>
            string.Equals(candidate, authorityRef, StringComparison.Ordinal) ||
            string.Equals(
                ExactIdentifierConfusableKey.Build(candidate),
                alias,
                StringComparison.Ordinal));
    }

    private static int Int32(JsonElement value, string property) =>
        value.GetProperty(property).GetInt32();

    private static long AddCumulative(
        IDictionary<string, long> cumulative,
        MortalWoundResolvedRequirement row)
    {
        var key = string.Join(
            "\u001f",
            row.Kind,
            row.AuthorityRef,
            row.OwnerKind,
            row.OwnerId);
        var value = checked((cumulative.TryGetValue(key, out var prior) ? prior : 0L) +
                            row.RequestedQuantity!.Value);
        cumulative[key] = value;
        return value;
    }

    private static MortalWoundTreatmentAuthority.Actor? FindActor(
        MortalWoundTreatmentAuthority.Snapshot snapshot,
        string? kind,
        string? id) => Single(snapshot.Actors.Where(actor =>
        string.Equals(actor.ActorKind, kind, StringComparison.Ordinal) &&
        string.Equals(actor.ActorId, id, StringComparison.Ordinal)));

    private static bool IsPresent(
        MortalWoundTreatmentAuthority.Snapshot snapshot,
        string locationId,
        MortalWoundTreatmentAuthority.Actor actor) =>
        IsPresent(snapshot, locationId, actor.ActorKind, actor.ActorId);

    private static bool IsPresent(
        MortalWoundTreatmentAuthority.Snapshot snapshot,
        string locationId,
        string actorKind,
        string actorId)
    {
        var location = Single(snapshot.Locations.Where(candidate => string.Equals(
            candidate.LocationId,
            locationId,
            StringComparison.Ordinal)));
        return location is not null && location.PresentActors.Any(actor =>
            string.Equals(actor.ActorKind, actorKind, StringComparison.Ordinal) &&
            string.Equals(actor.ActorId, actorId, StringComparison.Ordinal));
    }

    private static string?[] ActorFields(MortalWoundTreatmentAuthority.Actor actor) =>
        new string?[]
        {
            actor.ActorKind, actor.ActorId, actor.Realm, actor.CurrentLocationId,
            actor.Lifecycle, Boolean(actor.Active), Boolean(actor.Reachable)
        };

    private static bool SameOwner(
        string kind,
        string id,
        MortalWoundResolvedRequirement row) =>
        string.Equals(kind, row.OwnerKind, StringComparison.Ordinal) &&
        string.Equals(id, row.OwnerId, StringComparison.Ordinal);

    private static T? Single<T>(IEnumerable<T> values) where T : class
    {
        using var iterator = values.GetEnumerator();
        if (!iterator.MoveNext())
            return null;
        var value = iterator.Current;
        return iterator.MoveNext() ? null : value;
    }

    private static string String(JsonElement value, string property) =>
        value.GetProperty(property).GetString() ?? string.Empty;

    private static string Number(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string Number(long value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string Boolean(bool value) => value ? "true" : "false";
}
