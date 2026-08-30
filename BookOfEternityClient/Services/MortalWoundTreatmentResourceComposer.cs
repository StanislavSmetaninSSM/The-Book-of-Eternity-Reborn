using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal sealed class MortalWoundTreatmentResourcePreparationResult
{
    private readonly ReadOnlyCollection<ValidationIssue> _issues;

    private MortalWoundTreatmentResourcePreparationResult(
        bool isValid,
        IEnumerable<ValidationIssue> issues,
        MortalWoundTreatmentResourceReservationAuthority? authority)
    {
        IsValid = isValid;
        _issues = MortalWoundTreatmentShellDetachment.Freeze(issues);
        Authority = authority;
    }

    public bool IsValid { get; }
    public IReadOnlyList<ValidationIssue> Issues => _issues;
    public MortalWoundTreatmentResourceReservationAuthority? Authority { get; }

    internal static MortalWoundTreatmentResourcePreparationResult Valid(
        MortalWoundTreatmentResourceReservationAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
        return new MortalWoundTreatmentResourcePreparationResult(
            true,
            Array.Empty<ValidationIssue>(),
            authority);
    }

    internal static MortalWoundTreatmentResourcePreparationResult Invalid(
        IEnumerable<ValidationIssue> issues) => new(false, issues, null);
}

internal sealed class MortalWoundTreatmentResourceClaim
{
    private MortalWoundTreatmentResourceClaim(
        string scope,
        int requirementIndex,
        string kind,
        string authorityRef,
        string realm,
        string ownerKind,
        string ownerId,
        int quantity,
        string successWitnessFingerprint,
        string claimFingerprint)
    {
        Scope = scope;
        RequirementIndex = requirementIndex;
        Kind = kind;
        AuthorityRef = authorityRef;
        Realm = realm;
        OwnerKind = ownerKind;
        OwnerId = ownerId;
        Quantity = quantity;
        SuccessWitnessFingerprint = successWitnessFingerprint;
        ClaimFingerprint = claimFingerprint;
    }

    public string Scope { get; }
    public int RequirementIndex { get; }
    public string Kind { get; }
    public string AuthorityRef { get; }
    public string Realm { get; }
    public string OwnerKind { get; }
    public string OwnerId { get; }
    public int Quantity { get; }
    public string SuccessWitnessFingerprint { get; }
    public string ClaimFingerprint { get; }
}

internal sealed partial class MortalWoundTreatmentResourceReservationAuthority
{
    private readonly ReadOnlyCollection<MortalWoundTreatmentResourceClaim> _claims;

    private MortalWoundTreatmentResourceReservationAuthority(
        string reservationDisposition,
        string? reservationId,
        string coordinatesFingerprint,
        string acceptedStateFingerprint,
        string routeFingerprint,
        string? courseId,
        int? courseMilestoneOrdinal,
        string? courseCoordinateFingerprint,
        string requirementAuthorityFingerprint,
        MortalWoundTreatmentResourcePolicy policy,
        IEnumerable<MortalWoundTreatmentResourceClaim> claims,
        string authorityFingerprint)
    {
        ReservationDisposition = reservationDisposition;
        ReservationId = reservationId;
        CoordinatesFingerprint = coordinatesFingerprint;
        AcceptedStateFingerprint = acceptedStateFingerprint;
        RouteFingerprint = routeFingerprint;
        CourseId = courseId;
        CourseMilestoneOrdinal = courseMilestoneOrdinal;
        CourseCoordinateFingerprint = courseCoordinateFingerprint;
        RequirementAuthorityFingerprint = requirementAuthorityFingerprint;
        Policy = ClonePolicy(policy);
        _claims = MortalWoundTreatmentShellDetachment.Freeze(claims);
        AuthorityFingerprint = authorityFingerprint;
    }

    public string ReservationDisposition { get; }
    public string? ReservationId { get; }
    public string CoordinatesFingerprint { get; }
    public string AcceptedStateFingerprint { get; }
    public string RouteFingerprint { get; }
    public string? CourseId { get; }
    public int? CourseMilestoneOrdinal { get; }
    public string? CourseCoordinateFingerprint { get; }
    public string RequirementAuthorityFingerprint { get; }
    public MortalWoundTreatmentResourcePolicy Policy { get; }
    public IReadOnlyList<MortalWoundTreatmentResourceClaim> Claims => _claims;
    public string AuthorityFingerprint { get; }

    internal static MortalWoundTreatmentResourceReservationAuthority CreateNotRequired(
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string routeFingerprint,
        MortalWoundTreatmentRequirementAuthorityBundle requirementAuthority,
        MortalWoundTreatmentResourcePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(coordinates);
        ArgumentNullException.ThrowIfNull(requirementAuthority);
        ArgumentNullException.ThrowIfNull(policy);
        var detachedPolicy = ClonePolicy(policy);
        var authorityFingerprint = ComputeFingerprint(
            "not_required",
            reservationId: null,
            coordinates.CoordinatesFingerprint,
            coordinates.AcceptedStateFingerprint,
            routeFingerprint,
            requirementAuthority.CourseId,
            requirementAuthority.CourseMilestoneOrdinal,
            requirementAuthority.CourseCoordinateFingerprint,
            requirementAuthority.AuthorityFingerprint,
            detachedPolicy,
            Array.Empty<MortalWoundTreatmentResourceClaim>());
        return new MortalWoundTreatmentResourceReservationAuthority(
            "not_required",
            null,
            coordinates.CoordinatesFingerprint,
            coordinates.AcceptedStateFingerprint,
            routeFingerprint,
            requirementAuthority.CourseId,
            requirementAuthority.CourseMilestoneOrdinal,
            requirementAuthority.CourseCoordinateFingerprint,
            requirementAuthority.AuthorityFingerprint,
            detachedPolicy,
            Array.Empty<MortalWoundTreatmentResourceClaim>(),
            authorityFingerprint);
    }

    private static string ComputeFingerprint(
        string disposition,
        string? reservationId,
        string coordinatesFingerprint,
        string acceptedStateFingerprint,
        string routeFingerprint,
        string? courseId,
        int? courseMilestoneOrdinal,
        string? courseCoordinateFingerprint,
        string requirementAuthorityFingerprint,
        MortalWoundTreatmentResourcePolicy policy,
        IReadOnlyList<MortalWoundTreatmentResourceClaim> claims)
    {
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.resource_reservation_authority",
            "1",
            disposition,
            reservationId,
            coordinatesFingerprint,
            acceptedStateFingerprint,
            routeFingerprint,
            courseId,
            courseMilestoneOrdinal?.ToString(CultureInfo.InvariantCulture),
            courseCoordinateFingerprint,
            requirementAuthorityFingerprint,
            MortalWoundTreatmentResourceComposer.ComputePolicyFingerprint(policy),
            claims.Count.ToString(CultureInfo.InvariantCulture)
        };
        fields.AddRange(claims.Select(static claim => claim.ClaimFingerprint));
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static MortalWoundTreatmentResourcePolicy ClonePolicy(
        MortalWoundTreatmentResourcePolicy policy) => new(
        policy.ReserveBeforeResolution,
        policy.ConsumeOn.ToImmutableArray(),
        policy.RefundOn.ToImmutableArray(),
        policy.Mutations.Select(static mutation => mutation with { }).ToImmutableArray());
}

internal static class MortalWoundTreatmentResourceComposer
{
    private const string IssuePath = "treatmentAttempt.resources";

    internal static MortalWoundTreatmentResourcePreparationResult PrepareProcedure(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        WoundMaterializationEnvelope before,
        MortalWoundTreatmentRequirementAuthorityBundle requirementAuthority,
        MortalWoundProcedureCheckAuthority modeAuthority) => Prepare(
        "procedure",
        acceptedState,
        coordinates,
        before,
        requirementAuthority,
        modeAuthority);

    internal static MortalWoundTreatmentResourcePreparationResult PrepareCourse(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        WoundMaterializationEnvelope before,
        MortalWoundTreatmentRequirementAuthorityBundle requirementAuthority,
        MortalWoundCourseModeAuthority modeAuthority) => Prepare(
        "course",
        acceptedState,
        coordinates,
        before,
        requirementAuthority,
        modeAuthority);

    internal static MortalWoundTreatmentResourcePreparationResult PrepareGuaranteed(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        WoundMaterializationEnvelope before,
        MortalWoundTreatmentRequirementAuthorityBundle requirementAuthority,
        MortalWoundTreatmentCapabilityProof modeAuthority) => Prepare(
        "guaranteed",
        acceptedState,
        coordinates,
        before,
        requirementAuthority,
        modeAuthority);

    private static MortalWoundTreatmentResourcePreparationResult Prepare(
        string mode,
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState,
        MortalWoundTreatmentAttemptCoordinates? coordinates,
        WoundMaterializationEnvelope? before,
        MortalWoundTreatmentRequirementAuthorityBundle? requirementAuthority,
        MortalWoundTreatmentModeAuthority? modeAuthority)
    {
        var issues = new List<ValidationIssue>();
        if (!TryResolveCurrentRoute(
                mode,
                acceptedState,
                coordinates,
                before,
                issues,
                out var route,
                out var routeFingerprint) ||
            acceptedState is null ||
            coordinates is null ||
            before is null ||
            requirementAuthority is null ||
            modeAuthority is null)
        {
            return MortalWoundTreatmentResourcePreparationResult.Invalid(issues);
        }

        if (!TryValidateRequirementAuthority(
                route!,
                coordinates,
                requirementAuthority,
                routeFingerprint!,
                issues) ||
            !TryValidateModeAuthority(
                mode,
                acceptedState,
                coordinates,
                before,
                route!,
                routeFingerprint!,
                requirementAuthority,
                modeAuthority,
                issues) ||
            !TryValidatePolicy(route!, issues))
        {
            return MortalWoundTreatmentResourcePreparationResult.Invalid(issues);
        }

        if (mode == "course" &&
            string.Equals(
                requirementAuthority.CourseRequirementStatus,
                "Unsatisfied",
                StringComparison.Ordinal))
        {
            AddIssue(
                issues,
                "mortal_wound_treatment_resource_requirements_unsatisfied",
                "one satisfied current first-course milestone",
                "trusted unsatisfied requirement bundle");
            return MortalWoundTreatmentResourcePreparationResult.Invalid(issues);
        }

        if (requirementAuthority.Scopes.Any(static scope =>
                scope.Bindings.Any(static binding =>
                    binding.ResolvedRequirement.Kind is
                        "item_quantity" or "resource_quantity")))
        {
            AddIssue(
                issues,
                "mortal_wound_treatment_resource_claim_preparation_required",
                "sealed quantity-claim preparation",
                "quantity requirements are not yet reserved");
            return MortalWoundTreatmentResourcePreparationResult.Invalid(issues);
        }

        return MortalWoundTreatmentResourcePreparationResult.Valid(
            MortalWoundTreatmentResourceReservationAuthority.CreateNotRequired(
                coordinates,
                routeFingerprint!,
                requirementAuthority,
                route!.ResourcePolicy));
    }

    private static bool TryResolveCurrentRoute(
        string mode,
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState,
        MortalWoundTreatmentAttemptCoordinates? coordinates,
        WoundMaterializationEnvelope? before,
        ICollection<ValidationIssue> issues,
        out MortalWoundTreatmentRouteDefinition? route,
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
                "mortal_wound_treatment_resource_authority_invalid",
                "one current accepted state with exact coordinates and wound",
                "missing, stale, foreign, or mismatched authority");
            return false;
        }

        var matches = acceptedState.TreatmentDefinition.Routes.Where(candidate =>
                string.Equals(candidate.RouteId, coordinates.RouteId, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1 ||
            !string.Equals(matches[0].Mode, mode, StringComparison.Ordinal))
        {
            AddIssue(
                issues,
                "mortal_wound_treatment_resource_route_invalid",
                $"one exact current {mode} route",
                coordinates.RouteId);
            return false;
        }

        try
        {
            routeFingerprint = MortalWoundTreatmentRouteFingerprint.Compute(
                before,
                coordinates.RouteId);
            route = matches[0];
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException or
                                           JsonException)
        {
            AddIssue(
                issues,
                "mortal_wound_treatment_resource_route_invalid",
                "one canonical route with a recomputable fingerprint",
                exception.GetType().Name);
            return false;
        }
    }

    private static bool TryValidateRequirementAuthority(
        MortalWoundTreatmentRouteDefinition route,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        MortalWoundTreatmentRequirementAuthorityBundle authority,
        string routeFingerprint,
        ICollection<ValidationIssue> issues)
    {
        var isCourse = string.Equals(route.Mode, "course", StringComparison.Ordinal);
        if (!string.Equals(authority.Mode, route.Mode, StringComparison.Ordinal) ||
            !string.Equals(
                authority.ContextFingerprint,
                coordinates.ContextFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                authority.AcceptedStateFingerprint,
                coordinates.AcceptedStateFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(authority.RouteFingerprint, routeFingerprint, StringComparison.Ordinal) ||
            (!isCourse &&
             (authority.CourseId is not null ||
              authority.CourseMilestoneOrdinal is not null ||
              authority.CourseCoordinateFingerprint is not null ||
              authority.CourseRequirementStatus is not null ||
              authority.InterruptionReason is not null)))
        {
            AddIssue(
                issues,
                "mortal_wound_treatment_resource_requirement_authority_invalid",
                "the complete matching current requirement bundle",
                authority.AuthorityFingerprint);
            return false;
        }

        var expectedScopes = isCourse ? 2 : 1;
        if (authority.Scopes.Count != expectedScopes ||
            !TryValidateScope(
                authority.Scopes[0],
                "common",
                null,
                route.Requirements,
                issues))
        {
            return false;
        }
        if (isCourse)
        {
            if (route is not MortalWoundCourseRouteDefinition courseRoute ||
                authority.CourseMilestoneOrdinal is not { } ordinal)
            {
                AddIssue(
                    issues,
                    "mortal_wound_treatment_resource_requirement_authority_invalid",
                    "one complete course coordinate and current milestone scope",
                    authority.AuthorityFingerprint);
                return false;
            }
            var milestones = courseRoute.Milestones.Where(milestone =>
                milestone.Ordinal == ordinal).ToArray();
            if (milestones.Length != 1 ||
                !TryValidateScope(
                    authority.Scopes[1],
                    "course_milestone",
                    ordinal,
                    milestones[0].Requirements,
                    issues))
            {
                return false;
            }
        }

        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.requirement_bundle",
            "1",
            authority.Mode,
            authority.ContextFingerprint,
            authority.AcceptedStateFingerprint,
            authority.RouteFingerprint,
            authority.CourseId,
            authority.CourseMilestoneOrdinal?.ToString(CultureInfo.InvariantCulture),
            authority.CourseCoordinateFingerprint,
            authority.CourseRequirementStatus,
            authority.InterruptionReason,
            authority.Scopes.Count.ToString(CultureInfo.InvariantCulture)
        };
        fields.AddRange(authority.Scopes.Select(static scope => scope.AuthorityFingerprint));
        if (!string.Equals(
                authority.AuthorityFingerprint,
                WoundAcceptedTurnFingerprintWriter.Compute(fields),
                StringComparison.Ordinal))
        {
            AddIssue(
                issues,
                "mortal_wound_treatment_resource_requirement_authority_invalid",
                "one recomputable complete requirement bundle seal",
                authority.AuthorityFingerprint);
            return false;
        }
        return true;
    }

    private static bool TryValidateScope(
        MortalWoundTreatmentRequirementScopeAuthority scope,
        string expectedScope,
        int? expectedMilestoneOrdinal,
        IReadOnlyList<MortalWoundTreatmentRequirement> requirements,
        ICollection<ValidationIssue> issues)
    {
        var expectedIndices = Enumerable.Range(0, requirements.Count).ToArray();
        var actualIndices = scope.Bindings.Select(static binding => binding.RequirementIndex)
            .Concat(scope.FailureWitnesses.Select(static failure => failure.RequirementIndex))
            .OrderBy(static index => index)
            .ToArray();
        var expectedStatus = scope.FailureWitnesses.Count == 0
            ? "Satisfied"
            : "Unsatisfied";
        if (!string.Equals(scope.Scope, expectedScope, StringComparison.Ordinal) ||
            scope.CourseMilestoneOrdinal != expectedMilestoneOrdinal ||
            !string.Equals(scope.Status, expectedStatus, StringComparison.Ordinal) ||
            !actualIndices.SequenceEqual(expectedIndices))
        {
            AddIssue(
                issues,
                "mortal_wound_treatment_resource_requirement_scope_invalid",
                $"one complete ordered {expectedScope} scope",
                scope.AuthorityFingerprint);
            return false;
        }

        foreach (var binding in scope.Bindings)
        {
            var row = binding.ResolvedRequirement;
            var witness = binding.SuccessWitness;
            var requirement = requirements[binding.RequirementIndex];
            var bindingFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
                new string?[]
                {
                    "book_of_eternity.mortal_wound_treatment.requirement_binding",
                    "1",
                    witness.Scope,
                    row.RequirementIndex.ToString(CultureInfo.InvariantCulture),
                    row.AuthorityFingerprint,
                    witness.WitnessFingerprint
                });
            if (row.RequirementIndex != binding.RequirementIndex ||
                witness.RequirementIndex != binding.RequirementIndex ||
                !string.Equals(row.Kind, requirement.Kind, StringComparison.Ordinal) ||
                !string.Equals(witness.Scope, expectedScope, StringComparison.Ordinal) ||
                !string.Equals(witness.Kind, row.Kind, StringComparison.Ordinal) ||
                !string.Equals(witness.AuthorityRef, row.AuthorityRef, StringComparison.Ordinal) ||
                !string.Equals(binding.BindingFingerprint, bindingFingerprint, StringComparison.Ordinal))
            {
                AddIssue(
                    issues,
                    "mortal_wound_treatment_resource_requirement_binding_invalid",
                    "one exact sealed current success binding",
                    binding.BindingFingerprint);
                return false;
            }
        }
        foreach (var failure in scope.FailureWitnesses)
        {
            if (!string.Equals(
                    failure.Kind,
                    requirements[failure.RequirementIndex].Kind,
                    StringComparison.Ordinal) ||
                !string.Equals(failure.Scope, expectedScope, StringComparison.Ordinal))
            {
                AddIssue(
                    issues,
                    "mortal_wound_treatment_resource_requirement_failure_invalid",
                    "one exact typed current failure witness",
                    failure.WitnessFingerprint);
                return false;
            }
        }

        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.requirement_scope",
            "1",
            scope.Scope,
            scope.CourseMilestoneOrdinal?.ToString(CultureInfo.InvariantCulture),
            scope.Status,
            scope.Bindings.Count.ToString(CultureInfo.InvariantCulture)
        };
        fields.AddRange(scope.Bindings.Select(static binding => binding.BindingFingerprint));
        fields.Add(scope.FailureWitnesses.Count.ToString(CultureInfo.InvariantCulture));
        fields.AddRange(scope.FailureWitnesses.Select(static failure => failure.WitnessFingerprint));
        if (!string.Equals(
                scope.AuthorityFingerprint,
                WoundAcceptedTurnFingerprintWriter.Compute(fields),
                StringComparison.Ordinal))
        {
            AddIssue(
                issues,
                "mortal_wound_treatment_resource_requirement_scope_invalid",
                "one recomputable current requirement scope seal",
                scope.AuthorityFingerprint);
            return false;
        }
        return true;
    }

    private static bool TryValidateModeAuthority(
        string mode,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        WoundMaterializationEnvelope before,
        MortalWoundTreatmentRouteDefinition route,
        string routeFingerprint,
        MortalWoundTreatmentRequirementAuthorityBundle requirementAuthority,
        MortalWoundTreatmentModeAuthority modeAuthority,
        ICollection<ValidationIssue> issues)
    {
        var valid = modeAuthority switch
        {
            MortalWoundProcedureCheckAuthority procedure
                when mode == "procedure" && route is MortalWoundProcedureRouteDefinition =>
                string.Equals(
                    procedure.CoordinatesFingerprint,
                    coordinates.CoordinatesFingerprint,
                    StringComparison.Ordinal) &&
                string.Equals(
                    procedure.AcceptedStateFingerprint,
                    coordinates.AcceptedStateFingerprint,
                    StringComparison.Ordinal) &&
                string.Equals(
                    procedure.RequirementAuthorityFingerprint,
                    requirementAuthority.AuthorityFingerprint,
                    StringComparison.Ordinal),
            MortalWoundCourseModeAuthority course
                when mode == "course" && route is MortalWoundCourseRouteDefinition =>
                course.GameTimeAuthority.Matches(acceptedState, coordinates) &&
                course.MilestoneOrdinal > 0 &&
                string.Equals(course.WindowDisposition, "ready", StringComparison.Ordinal) &&
                string.Equals(course.CoordinatesFingerprint, coordinates.CoordinatesFingerprint, StringComparison.Ordinal) &&
                string.Equals(course.AcceptedStateFingerprint, coordinates.AcceptedStateFingerprint, StringComparison.Ordinal) &&
                string.Equals(course.CourseId, requirementAuthority.CourseId, StringComparison.Ordinal) &&
                course.MilestoneOrdinal == requirementAuthority.CourseMilestoneOrdinal &&
                string.Equals(course.CourseCoordinateFingerprint, requirementAuthority.CourseCoordinateFingerprint, StringComparison.Ordinal) &&
                string.Equals(course.CourseStartAuthority.RouteId, coordinates.RouteId, StringComparison.Ordinal) &&
                string.Equals(course.CourseStartAuthority.RouteFingerprint, routeFingerprint, StringComparison.Ordinal) &&
                string.Equals(course.CourseStartAuthority.StartingWoundFingerprint, acceptedState.WoundFingerprint, StringComparison.Ordinal) &&
                acceptedState.MatchesCurrentWound(course.CourseStartAuthority.StartingWound),
            MortalWoundTreatmentCapabilityProof proof
                when mode == "guaranteed" && route is MortalWoundGuaranteedRouteDefinition guaranteed =>
                string.Equals(proof.CoordinatesFingerprint, coordinates.CoordinatesFingerprint, StringComparison.Ordinal) &&
                string.Equals(proof.AcceptedStateFingerprint, coordinates.AcceptedStateFingerprint, StringComparison.Ordinal) &&
                string.Equals(proof.ContextFingerprint, coordinates.ContextFingerprint, StringComparison.Ordinal) &&
                string.Equals(proof.CapabilityRef, guaranteed.Resolution.CapabilityRef, StringComparison.Ordinal) &&
                string.Equals(proof.WoundDomain, before.Classification.Domain, StringComparison.Ordinal) &&
                before.Severity.Rank >= proof.MinimumSeverityRank &&
                before.Severity.Rank <= proof.MaximumSeverityRank &&
                MatchesProofActor(proof, coordinates, guaranteed.Resolution.ActorRole),
            _ => false
        };
        if (!valid)
        {
            AddIssue(
                issues,
                "mortal_wound_treatment_resource_mode_authority_invalid",
                $"one exact current {mode} mode authority",
                modeAuthority.GetType().Name);
        }
        return valid;
    }

    private static bool MatchesProofActor(
        MortalWoundTreatmentCapabilityProof proof,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string actorRole) => actorRole switch
    {
        "provider" =>
            string.Equals(proof.OwnerKind, coordinates.ProviderKind, StringComparison.Ordinal) &&
            string.Equals(proof.OwnerId, coordinates.ProviderId, StringComparison.Ordinal),
        "target" =>
            string.Equals(proof.OwnerKind, coordinates.TargetKind, StringComparison.Ordinal) &&
            string.Equals(proof.OwnerId, coordinates.TargetId, StringComparison.Ordinal),
        _ => false
    };

    private static bool TryValidatePolicy(
        MortalWoundTreatmentRouteDefinition route,
        ICollection<ValidationIssue> issues)
    {
        var policy = route.ResourcePolicy;
        var allowedConsumeOn = route.Mode == "procedure"
            ? new[] { "success", "partial_success", "failed_attempt" }
            : new[] { "success" };
        var valid = policy.ReserveBeforeResolution &&
                    policy.ConsumeOn.Length <= allowedConsumeOn.Length &&
                    policy.ConsumeOn.Distinct(StringComparer.Ordinal).Count() ==
                        policy.ConsumeOn.Length &&
                    policy.ConsumeOn.All(value => allowedConsumeOn.Contains(
                        value,
                        StringComparer.Ordinal)) &&
                    policy.RefundOn.SequenceEqual(new[]
                    {
                        "cancelled", "validation_failed", "rolled_back"
                    });
        if (!valid)
        {
            AddIssue(
                issues,
                "mortal_wound_treatment_resource_policy_invalid",
                "one closed mode-legal reserve-before-resolution policy",
                ComputePolicyFingerprint(policy));
        }
        return valid;
    }

    internal static string ComputePolicyFingerprint(
        MortalWoundTreatmentResourcePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.resource_policy",
            "1",
            policy.ReserveBeforeResolution ? "true" : "false",
            policy.ConsumeOn.Length.ToString(CultureInfo.InvariantCulture)
        };
        fields.AddRange(policy.ConsumeOn);
        fields.Add(policy.RefundOn.Length.ToString(CultureInfo.InvariantCulture));
        fields.AddRange(policy.RefundOn);
        fields.Add(policy.Mutations.Length.ToString(CultureInfo.InvariantCulture));
        foreach (var mutation in policy.Mutations)
        {
            fields.Add(mutation.Kind);
            fields.Add(mutation.Scope);
            fields.Add(mutation.MilestoneOrdinal?.ToString(CultureInfo.InvariantCulture));
            fields.Add(mutation.RequirementIndex.ToString(CultureInfo.InvariantCulture));
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static void AddIssue(
        ICollection<ValidationIssue> issues,
        string code,
        string expected,
        string actual) => issues.Add(new ValidationIssue(
        IssuePath,
        IssueSeverity.Error,
        "The Mortal wound-treatment resource authority cannot be trusted.",
        code: code,
        actor: "Client",
        section: "wound_materialization",
        expected: expected,
        actual: actual,
        repairHint:
        "Recreate resource preparation from the current accepted state, exact route, complete requirement bundle, and matching mode authority."));
}
