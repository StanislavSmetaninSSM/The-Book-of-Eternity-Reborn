using System.Collections.ObjectModel;
using System.Globalization;

namespace BookOfEternityClient.Services;

internal sealed partial class MortalWoundTreatmentAttemptRequestResult
{
    internal static MortalWoundTreatmentAttemptRequestResult Valid(
        MortalWoundTreatmentAttemptRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new MortalWoundTreatmentAttemptRequestResult(
            true,
            Array.Empty<ValidationIssue>(),
            request);
    }

    internal static MortalWoundTreatmentAttemptRequestResult Invalid(
        IEnumerable<ValidationIssue> issues) => new(
        false,
        issues,
        null);

    internal static MortalWoundTreatmentAttemptRequestResult Invalid(
        ValidationIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return Invalid(new[] { issue });
    }
}

internal sealed partial class MortalWoundTreatmentAttemptRequest
{
    private const string FingerprintDomain =
        "book_of_eternity.mortal_wound_treatment.attempt_request";

    internal static MortalWoundTreatmentAttemptRequest Create(
        MortalWoundTreatmentPlanner.RequestMintCapability mintCapability,
        string mode,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        int? milestoneOrdinal,
        WoundMaterializationEnvelope routeSourceWound,
        MortalWoundTreatmentModeAuthority modeAuthority,
        MortalWoundTreatmentRequirementAuthorityBundle requirementAuthority,
        MortalWoundTreatmentResourceReservationAuthority resourceAuthority)
    {
        MortalWoundTreatmentPlanner.RequestMintCapability.RequireAuthority(mintCapability);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        ArgumentNullException.ThrowIfNull(coordinates);
        ArgumentNullException.ThrowIfNull(routeSourceWound);
        ArgumentNullException.ThrowIfNull(modeAuthority);
        ArgumentNullException.ThrowIfNull(requirementAuthority);
        ArgumentNullException.ThrowIfNull(resourceAuthority);

        var detachedSourceResult = WoundMaterializationContract.Parse(
            WoundMaterializationContract.SerializeCanonical(routeSourceWound),
            "treatmentAttempt.request.routeSourceWound");
        if (!detachedSourceResult.IsValid || detachedSourceResult.Wound is null)
        {
            throw new InvalidOperationException(
                "The treatment route source wound must survive canonical detachment.");
        }
        var detachedSource = detachedSourceResult.Wound;
        var routeSourceWoundFingerprint =
            WoundIdentityState.ComputeSemanticFingerprint(detachedSource);
        if (!string.Equals(
                routeSourceWoundFingerprint,
                coordinates.ExpectedBeforeFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                MortalWoundTreatmentRouteFingerprint.Compute(
                    detachedSource,
                    coordinates.RouteId),
                requirementAuthority.RouteFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The treatment route source wound does not match the sealed coordinates and route.");
        }

        return new MortalWoundTreatmentAttemptRequest(
            mode,
            coordinates,
            milestoneOrdinal,
            detachedSource,
            routeSourceWoundFingerprint,
            modeAuthority,
            requirementAuthority,
            resourceAuthority,
            ComputeFingerprint(
                mode,
                coordinates,
                milestoneOrdinal,
                routeSourceWoundFingerprint,
                modeAuthority,
                requirementAuthority,
                resourceAuthority));
    }

    internal static string ComputeFingerprint(
        string mode,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        int? milestoneOrdinal,
        string routeSourceWoundFingerprint,
        MortalWoundTreatmentModeAuthority modeAuthority,
        MortalWoundTreatmentRequirementAuthorityBundle requirementAuthority,
        MortalWoundTreatmentResourceReservationAuthority resourceAuthority) =>
        WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                FingerprintDomain,
                "1",
                mode,
                coordinates.CoordinatesFingerprint,
                milestoneOrdinal?.ToString(CultureInfo.InvariantCulture),
                routeSourceWoundFingerprint,
                ModeAuthorityFingerprint(modeAuthority),
                requirementAuthority.AuthorityFingerprint,
                resourceAuthority.AuthorityFingerprint
            });

    private static string ModeAuthorityFingerprint(
        MortalWoundTreatmentModeAuthority authority) => authority switch
    {
        MortalWoundProcedureCheckAuthority procedure => procedure.AuthorityFingerprint,
        MortalWoundCourseModeAuthority course => course.AuthorityFingerprint,
        MortalWoundTreatmentCapabilityProof proof => proof.ProofFingerprint,
        _ => throw new ArgumentException(
            "Unsupported Mortal wound-treatment mode authority.",
            nameof(authority))
    };

    internal static MortalWoundTreatmentAttemptRequest? RestoreDetached(
        string mode,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        int? milestoneOrdinal,
        WoundMaterializationEnvelope routeSourceWound,
        string routeSourceWoundFingerprint,
        MortalWoundTreatmentModeAuthority modeAuthority,
        MortalWoundTreatmentRequirementAuthorityBundle requirementAuthority,
        MortalWoundTreatmentResourceReservationAuthority resourceAuthority,
        string requestFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        ArgumentNullException.ThrowIfNull(coordinates);
        ArgumentNullException.ThrowIfNull(routeSourceWound);
        ArgumentException.ThrowIfNullOrWhiteSpace(routeSourceWoundFingerprint);
        ArgumentNullException.ThrowIfNull(modeAuthority);
        ArgumentNullException.ThrowIfNull(requirementAuthority);
        ArgumentNullException.ThrowIfNull(resourceAuthority);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestFingerprint);

        var restored = new MortalWoundTreatmentAttemptRequest(
            mode,
            coordinates,
            milestoneOrdinal,
            routeSourceWound,
            routeSourceWoundFingerprint,
            modeAuthority,
            requirementAuthority,
            resourceAuthority,
            requestFingerprint);
        return restored.HasMatchingFingerprint() ? restored : null;
    }

    internal MortalWoundTreatmentAttemptRequest? AttachRestoredProcedureAuthority(
        MortalWoundProcedureCheckAuthority procedureAuthority)
    {
        ArgumentNullException.ThrowIfNull(procedureAuthority);
        if (!string.Equals(Mode, "procedure", StringComparison.Ordinal) ||
            ModeAuthority is not MortalWoundProcedureCheckAuthority detached ||
            !string.Equals(
                detached.AuthorityFingerprint,
                procedureAuthority.AuthorityFingerprint,
                StringComparison.Ordinal))
        {
            return null;
        }
        return RestoreDetached(
            Mode,
            Coordinates,
            MilestoneOrdinal,
            RouteSourceWound,
            RouteSourceWoundFingerprint,
            procedureAuthority,
            RequirementAuthority,
            ResourceAuthority,
            RequestFingerprint);
    }
}

internal static partial class MortalWoundTreatmentPlanner
{
    private const string RequestIssuePath = "treatmentAttempt.request";
    private static readonly RequestMintCapability RequestMint = new();

    internal sealed class RequestMintCapability
    {
        internal RequestMintCapability()
        {
        }

        internal static void RequireAuthority(RequestMintCapability? capability)
        {
            if (!ReferenceEquals(capability, RequestMint))
            {
                throw new InvalidOperationException(
                    "A treatment request can only be minted by its mode-specific sealer.");
            }
        }
    }

    internal static MortalWoundTreatmentAttemptRequestResult SealProcedureRequest(
        MortalWoundTreatmentAttemptCoordinates? coordinates,
        MortalWoundProcedureCheckAuthority? modeAuthority,
        MortalWoundTreatmentRequirementAuthorityBundle? requirementAuthority,
        MortalWoundTreatmentResourceReservationAuthority? resourceAuthority,
        WoundMaterializationEnvelope? routeSourceWound) =>
        SealRequest(
            "procedure",
            coordinates,
            null,
            routeSourceWound,
            modeAuthority,
            requirementAuthority,
            resourceAuthority);

    internal static MortalWoundTreatmentAttemptRequestResult SealCourseMilestoneRequest(
        MortalWoundTreatmentAttemptCoordinates? coordinates,
        int milestoneOrdinal,
        MortalWoundCourseModeAuthority? modeAuthority,
        MortalWoundTreatmentRequirementAuthorityBundle? requirementAuthority,
        MortalWoundTreatmentResourceReservationAuthority? resourceAuthority,
        WoundMaterializationEnvelope? routeSourceWound) =>
        SealRequest(
            "course",
            coordinates,
            milestoneOrdinal,
            routeSourceWound,
            modeAuthority,
            requirementAuthority,
            resourceAuthority);

    internal static MortalWoundTreatmentAttemptRequestResult SealGuaranteedRequest(
        MortalWoundTreatmentAttemptCoordinates? coordinates,
        MortalWoundTreatmentCapabilityProof? modeAuthority,
        MortalWoundTreatmentRequirementAuthorityBundle? requirementAuthority,
        MortalWoundTreatmentResourceReservationAuthority? resourceAuthority,
        WoundMaterializationEnvelope? routeSourceWound) =>
        SealRequest(
            "guaranteed",
            coordinates,
            null,
            routeSourceWound,
            modeAuthority,
            requirementAuthority,
            resourceAuthority);

    internal static MortalWoundTreatmentAttemptRequestResult PrepareProcedureRequest(
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState,
        WoundHistoryParseResult? history,
        WoundMaterializationEnvelope? before,
        string? operationKey,
        string? routeId,
        string? eventRef)
    {
        if (!TryPrepareCoordinates(
                "procedure",
                acceptedState,
                history,
                before,
                operationKey,
                routeId,
                eventRef,
                out var coordinates,
                out var route,
                out var failure))
        {
            return failure!;
        }

        var requirementResult =
            MortalWoundTreatmentRequirementAuthorityBundle.CreateForProcedure(
                acceptedState,
                coordinates,
                before);
        if (!requirementResult.IsValid || requirementResult.Authority is null)
            return MortalWoundTreatmentAttemptRequestResult.Invalid(
                requirementResult.Issues);

        if (route is not MortalWoundProcedureRouteDefinition procedureRoute)
        {
            return InvalidRequest(
                "mortal_wound_treatment_request_route_mode_mismatch",
                "one exact current procedure route",
                route?.Mode ?? "missing route");
        }

        var applicabilityIssues = ValidateProcedureApplicability(
            before!,
            procedureRoute,
            acceptedState!,
            coordinates!);
        if (applicabilityIssues.Count != 0)
            return MortalWoundTreatmentAttemptRequestResult.Invalid(
                applicabilityIssues);

        var modeResult = MortalWoundProcedureCheckAuthority.Create(
            coordinates,
            procedureRoute,
            before,
            requirementResult.Authority,
            acceptedState);
        if (!modeResult.IsValid || modeResult.Authority is null)
            return MortalWoundTreatmentAttemptRequestResult.Invalid(modeResult.Issues);

        var resourceResult = MortalWoundTreatmentResourceComposer.PrepareProcedure(
            acceptedState!,
            coordinates!,
            before!,
            requirementResult.Authority,
            modeResult.Authority);
        if (!resourceResult.IsValid || resourceResult.Authority is null)
        {
            modeResult.Authority.RollbackNewProvisionalReservations(acceptedState!);
            return MortalWoundTreatmentAttemptRequestResult.Invalid(resourceResult.Issues);
        }

        var sealedResult = SealProcedureRequest(
            coordinates,
            modeResult.Authority,
            requirementResult.Authority,
            resourceResult.Authority,
            before);
        if (!sealedResult.IsValid)
        {
            MortalWoundTreatmentResourceComposer.RollbackNew(resourceResult);
            modeResult.Authority.RollbackNewProvisionalReservations(acceptedState!);
        }
        return sealedResult;
    }

    internal static MortalWoundTreatmentAttemptRequestResult PrepareCourseMilestoneRequest(
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState,
        WoundHistoryParseResult? history,
        WoundMaterializationEnvelope? before,
        string? operationKey,
        string? routeId,
        string? eventRef)
    {
        if (!TryPrepareCoordinates(
                "course",
                acceptedState,
                history,
                before,
                operationKey,
                routeId,
                eventRef,
                out var coordinates,
                out _,
                out var failure))
        {
            return failure!;
        }

        var timeResult = MortalWoundGameTimeAuthority.Create(
            acceptedState,
            coordinates);
        if (!timeResult.IsValid || timeResult.Authority is null)
            return MortalWoundTreatmentAttemptRequestResult.Invalid(timeResult.Issues);

        var modeResult = MortalWoundCourseModeAuthority.Create(
            acceptedState,
            coordinates,
            before,
            history,
            timeResult.Authority);
        if (modeResult.Authority is null)
        {
            return modeResult.Issues.Count != 0
                ? MortalWoundTreatmentAttemptRequestResult.Invalid(modeResult.Issues)
                : InvalidRequest(
                    "mortal_wound_treatment_course_not_ready",
                    "one ready or deadline-exceeded course milestone",
                    modeResult.Disposition);
        }

        var requirementResult =
            MortalWoundTreatmentRequirementAuthorityBundle.CreateForCourseMilestone(
                acceptedState,
                coordinates,
                before,
                history,
                modeResult.Authority);
        if (string.Equals(
                requirementResult.Status,
                "InvalidAuthority",
                StringComparison.Ordinal) ||
            requirementResult.Authority is null)
        {
            return MortalWoundTreatmentAttemptRequestResult.Invalid(
                requirementResult.Issues);
        }

        if (string.Equals(
                requirementResult.Status,
                "Unsatisfied",
                StringComparison.Ordinal) &&
            before!.Care.ActiveCourseId is null)
        {
            return InvalidRequest(
                "mortal_wound_treatment_course_start_requirements_unsatisfied",
                "satisfied first-course milestone requirements",
                "trusted unsatisfied requirement bundle");
        }

        var resourceResult = MortalWoundTreatmentResourceComposer.PrepareCourse(
            acceptedState!,
            coordinates!,
            before!,
            requirementResult.Authority,
            modeResult.Authority);
        if (!resourceResult.IsValid || resourceResult.Authority is null)
            return MortalWoundTreatmentAttemptRequestResult.Invalid(resourceResult.Issues);

        var sealedResult = SealCourseMilestoneRequest(
            coordinates,
            modeResult.Authority.MilestoneOrdinal,
            modeResult.Authority,
            requirementResult.Authority,
            resourceResult.Authority,
            before);
        if (!sealedResult.IsValid)
            MortalWoundTreatmentResourceComposer.RollbackNew(resourceResult);
        return sealedResult;
    }

    internal static MortalWoundTreatmentAttemptRequestResult PrepareGuaranteedRequest(
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState,
        WoundHistoryParseResult? history,
        WoundMaterializationEnvelope? before,
        string? operationKey,
        string? routeId,
        string? eventRef)
    {
        if (!TryPrepareCoordinates(
                "guaranteed",
                acceptedState,
                history,
                before,
                operationKey,
                routeId,
                eventRef,
                out var coordinates,
                out var route,
                out var failure))
        {
            return failure!;
        }

        if (route is not MortalWoundGuaranteedRouteDefinition guaranteedRoute)
        {
            return InvalidRequest(
                "mortal_wound_treatment_request_route_mode_mismatch",
                "one exact current guaranteed route",
                route?.Mode ?? "missing route");
        }

        var applicabilityIssues = ValidateGuaranteedApplicability(
            before!,
            guaranteedRoute);
        if (applicabilityIssues.Count != 0)
            return MortalWoundTreatmentAttemptRequestResult.Invalid(applicabilityIssues);

        var requirementResult =
            MortalWoundTreatmentRequirementAuthorityBundle.CreateForGuaranteed(
                acceptedState,
                coordinates,
                before);
        if (!requirementResult.IsValid || requirementResult.Authority is null)
            return MortalWoundTreatmentAttemptRequestResult.Invalid(
                requirementResult.Issues);

        var proofResult = MortalWoundTreatmentCapabilityAuthority.ExportCurrent(
            acceptedState,
            coordinates,
            guaranteedRoute.Resolution.CapabilityRef,
            guaranteedRoute.Resolution.ActorRole);
        if (!proofResult.IsValid || proofResult.Proof is null)
            return MortalWoundTreatmentAttemptRequestResult.Invalid(proofResult.Issues);

        var resourceResult = MortalWoundTreatmentResourceComposer.PrepareGuaranteed(
            acceptedState!,
            coordinates!,
            before!,
            requirementResult.Authority,
            proofResult.Proof);
        if (!resourceResult.IsValid || resourceResult.Authority is null)
            return MortalWoundTreatmentAttemptRequestResult.Invalid(resourceResult.Issues);

        var sealedResult = SealGuaranteedRequest(
            coordinates,
            proofResult.Proof,
            requirementResult.Authority,
            resourceResult.Authority,
            before);
        if (!sealedResult.IsValid)
            MortalWoundTreatmentResourceComposer.RollbackNew(resourceResult);
        return sealedResult;
    }

    private static MortalWoundTreatmentAttemptRequestResult SealRequest(
        string mode,
        MortalWoundTreatmentAttemptCoordinates? coordinates,
        int? milestoneOrdinal,
        WoundMaterializationEnvelope? routeSourceWound,
        MortalWoundTreatmentModeAuthority? modeAuthority,
        MortalWoundTreatmentRequirementAuthorityBundle? requirementAuthority,
        MortalWoundTreatmentResourceReservationAuthority? resourceAuthority)
    {
        var issues = new List<ValidationIssue>();
        if (coordinates is null)
            AddRequestIssue(
                issues,
                "mortal_wound_treatment_request_coordinates_missing",
                "one complete sealed attempt coordinate",
                "missing coordinates");
        if (routeSourceWound is null)
            AddRequestIssue(
                issues,
                "mortal_wound_treatment_request_route_source_missing",
                "one canonical detached wound containing the selected route",
                "missing route source");
        if (modeAuthority is null)
            AddRequestIssue(
                issues,
                "mortal_wound_treatment_request_mode_authority_missing",
                $"one complete {mode} mode authority",
                "missing authority");
        if (requirementAuthority is null)
            AddRequestIssue(
                issues,
                "mortal_wound_treatment_request_requirement_authority_missing",
                "one complete matching requirement authority",
                "missing authority");
        if (resourceAuthority is null)
            AddRequestIssue(
                issues,
                "mortal_wound_treatment_request_resource_authority_missing",
                "one complete matching pre-resolution resource authority",
                "missing authority");
        if (issues.Count != 0)
            return MortalWoundTreatmentAttemptRequestResult.Invalid(issues);

        if (!MatchesRequestAuthorities(
                mode,
                coordinates!,
                milestoneOrdinal,
                modeAuthority!,
                requirementAuthority!,
                resourceAuthority!))
        {
            return InvalidRequest(
                "mortal_wound_treatment_request_authority_mismatch",
                "one exact mutually agreeing coordinate/mode/requirement/resource authority set",
                mode);
        }

        try
        {
            return MortalWoundTreatmentAttemptRequestResult.Valid(
                MortalWoundTreatmentAttemptRequest.Create(
                    RequestMint,
                    mode,
                    coordinates!,
                    milestoneOrdinal,
                    routeSourceWound!,
                    modeAuthority!,
                    requirementAuthority!,
                    resourceAuthority!));
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException or
                                           OverflowException)
        {
            return InvalidRequest(
                "mortal_wound_treatment_request_seal_invalid",
                "one recomputable complete request seal",
                exception.GetType().Name);
        }
    }

    internal static bool MatchesRequestAuthorities(
        string mode,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        int? milestoneOrdinal,
        MortalWoundTreatmentModeAuthority modeAuthority,
        MortalWoundTreatmentRequirementAuthorityBundle requirementAuthority,
        MortalWoundTreatmentResourceReservationAuthority resourceAuthority)
    {
        var common = string.Equals(requirementAuthority.Mode, mode, StringComparison.Ordinal) &&
                     string.Equals(
                         requirementAuthority.ContextFingerprint,
                         coordinates.ContextFingerprint,
                         StringComparison.Ordinal) &&
                     string.Equals(
                         requirementAuthority.AcceptedStateFingerprint,
                         coordinates.AcceptedStateFingerprint,
                         StringComparison.Ordinal) &&
                     string.Equals(
                         resourceAuthority.CoordinatesFingerprint,
                         coordinates.CoordinatesFingerprint,
                         StringComparison.Ordinal) &&
                     string.Equals(
                         resourceAuthority.AcceptedStateFingerprint,
                         coordinates.AcceptedStateFingerprint,
                         StringComparison.Ordinal) &&
                     string.Equals(
                         resourceAuthority.RouteFingerprint,
                         requirementAuthority.RouteFingerprint,
                         StringComparison.Ordinal) &&
                     string.Equals(
                         resourceAuthority.RequirementAuthorityFingerprint,
                         requirementAuthority.AuthorityFingerprint,
                         StringComparison.Ordinal);
        if (!common)
            return false;

        return modeAuthority switch
        {
            MortalWoundProcedureCheckAuthority procedure when mode == "procedure" =>
                milestoneOrdinal is null &&
                requirementAuthority.CourseId is null &&
                requirementAuthority.CourseMilestoneOrdinal is null &&
                resourceAuthority.CourseId is null &&
                resourceAuthority.CourseMilestoneOrdinal is null &&
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
            MortalWoundCourseModeAuthority course when mode == "course" =>
                milestoneOrdinal is > 0 &&
                milestoneOrdinal == course.MilestoneOrdinal &&
                milestoneOrdinal == requirementAuthority.CourseMilestoneOrdinal &&
                milestoneOrdinal == resourceAuthority.CourseMilestoneOrdinal &&
                string.Equals(course.CourseId, requirementAuthority.CourseId, StringComparison.Ordinal) &&
                string.Equals(course.CourseId, resourceAuthority.CourseId, StringComparison.Ordinal) &&
                string.Equals(
                    course.CourseCoordinateFingerprint,
                    requirementAuthority.CourseCoordinateFingerprint,
                    StringComparison.Ordinal) &&
                string.Equals(
                    course.CourseCoordinateFingerprint,
                    resourceAuthority.CourseCoordinateFingerprint,
                    StringComparison.Ordinal) &&
                string.Equals(
                    course.CoordinatesFingerprint,
                    coordinates.CoordinatesFingerprint,
                    StringComparison.Ordinal) &&
                string.Equals(
                    course.AcceptedStateFingerprint,
                    coordinates.AcceptedStateFingerprint,
                    StringComparison.Ordinal),
            MortalWoundTreatmentCapabilityProof proof when mode == "guaranteed" =>
                milestoneOrdinal is null &&
                requirementAuthority.CourseId is null &&
                requirementAuthority.CourseMilestoneOrdinal is null &&
                resourceAuthority.CourseId is null &&
                resourceAuthority.CourseMilestoneOrdinal is null &&
                string.Equals(
                    proof.CoordinatesFingerprint,
                    coordinates.CoordinatesFingerprint,
                    StringComparison.Ordinal) &&
                string.Equals(
                    proof.AcceptedStateFingerprint,
                    coordinates.AcceptedStateFingerprint,
                    StringComparison.Ordinal) &&
                string.Equals(
                    proof.ContextFingerprint,
                    coordinates.ContextFingerprint,
                    StringComparison.Ordinal),
            _ => false
        };
    }

    private static bool TryPrepareCoordinates(
        string mode,
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState,
        WoundHistoryParseResult? history,
        WoundMaterializationEnvelope? before,
        string? operationKey,
        string? routeId,
        string? eventRef,
        out MortalWoundTreatmentAttemptCoordinates? coordinates,
        out MortalWoundTreatmentRouteDefinition? route,
        out MortalWoundTreatmentAttemptRequestResult? failure)
    {
        coordinates = null;
        route = null;
        failure = null;
        if (acceptedState is null ||
            history is null ||
            before is null ||
            !acceptedState.MatchesCompleteHistory(history))
        {
            failure = InvalidRequest(
                "mortal_wound_treatment_request_current_authority_invalid",
                "one current accepted state with its exact wound and complete history",
                "missing, stale, foreign, or mismatched authority");
            return false;
        }

        var routeMatches = acceptedState.TreatmentDefinition.Routes
            .Where(candidate => string.Equals(
                candidate.RouteId,
                routeId,
                StringComparison.Ordinal))
            .ToArray();
        if (routeMatches.Length != 1 ||
            !string.Equals(routeMatches[0].Mode, mode, StringComparison.Ordinal))
        {
            failure = InvalidRequest(
                "mortal_wound_treatment_request_route_mode_mismatch",
                $"one exact current {mode} route",
                routeId ?? "missing route");
            return false;
        }

        var coordinateResult = CreateAttemptCoordinates(
            acceptedState,
            before,
            operationKey,
            routeId,
            eventRef);
        if (!coordinateResult.IsValid || coordinateResult.Coordinates is null)
        {
            failure = MortalWoundTreatmentAttemptRequestResult.Invalid(
                coordinateResult.Issues);
            return false;
        }

        coordinates = coordinateResult.Coordinates;
        route = routeMatches[0];
        return true;
    }

    internal static IReadOnlyList<ValidationIssue> ValidateProcedureApplicability(
        WoundMaterializationEnvelope before,
        MortalWoundProcedureRouteDefinition route,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptCoordinates coordinates)
    {
        var issues = new List<ValidationIssue>();
        foreach (var band in route.Bands)
        {
            var simulation = MortalWoundTreatmentWorkingWoundSimulator.Simulate(
                before,
                new[] { band.DeclaredResult },
                (working, operation) => PrepareComplexApplicability(
                    working,
                    operation,
                    acceptedState,
                    coordinates));
            if (simulation.IsApplicable &&
                (!string.Equals(band.Category, "success", StringComparison.Ordinal) ||
                 simulation.Improved))
            {
                continue;
            }

            AddRequestIssue(
                issues,
                "mortal_wound_treatment_procedure_band_inapplicable",
                "every current procedure band applicable and every success band improving",
                band.BandId);
        }
        return new ReadOnlyCollection<ValidationIssue>(issues);
    }

    private static MortalWoundTreatmentPreparedOperationResult PrepareComplexApplicability(
        WoundMaterializationEnvelope before,
        MortalWoundTreatmentOperation operation,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptCoordinates coordinates)
    {
        if (operation is MortalWoundAddComplicationOperation complication)
        {
            try
            {
                var additionalDefinitions = complication.ComplicationDraft
                    .ConsequenceDefinitions.Length;
                var additionalRoots = complication.ComplicationDraft.ConsequenceDefinitions
                    .Count(static definition => definition.Root is not null);
                var additionalSlots = complication.ComplicationDraft.ConsequenceDefinitions
                    .Where(static definition => definition.Root is not null)
                    .Sum(static definition => definition.Root!.Slots.Length);
                var applicable = checked(before.Complications.Count + 1) <=
                                     WoundMaterializationContract.MaxComplications &&
                                 checked(before.Consequences.SlotsUsed + additionalSlots) <=
                                     before.Consequences.SlotBudget &&
                                 checked(before.Consequences.OwnedEffectSources.Definitions.Count +
                                         additionalDefinitions) <=
                                     WoundMaterializationContract.MaxOwnedEffectDefinitions &&
                                 checked(before.Consequences.OwnedEffectSources.RootBindings.Count +
                                         additionalRoots) <=
                                     WoundMaterializationContract.MaxOwnedEffectRootBindings;
                return new MortalWoundTreatmentPreparedOperationResult(
                    applicable,
                    false,
                    applicable ? before : null);
            }
            catch (OverflowException)
            {
                return new MortalWoundTreatmentPreparedOperationResult(false, false, null);
            }
        }

        if (operation is not MortalWoundApplyDeteriorationOperation deterioration)
            return new MortalWoundTreatmentPreparedOperationResult(false, false, null);

        var authorityResult = MortalWoundDeteriorationPolicyAuthority.Create(
            acceptedState,
            coordinates,
            deterioration.PolicyRef);
        if (!authorityResult.IsValid || authorityResult.Authority is null)
            return new MortalWoundTreatmentPreparedOperationResult(false, false, null);

        var policy = authorityResult.Authority.Policy;
        if (policy.ResultKind != MortalWoundDeteriorationResultKind.IncreaseSeverity)
        {
            // The typed T069 authority has already proved the complete complication
            // capacity or death-contour applicability. Permanent identity and the
            // terminal handoff are deliberately allocated only for the selected row.
            return new MortalWoundTreatmentPreparedOperationResult(true, false, before);
        }

        var rank = checked(before.Severity.Rank + 1);
        var value = rank switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            4 => "IV",
            _ => string.Empty
        };
        if (value.Length == 0)
            return new MortalWoundTreatmentPreparedOperationResult(false, false, null);
        var after = before with
        {
            Severity = before.Severity with { Rank = rank, Value = value }
        };
        return new MortalWoundTreatmentPreparedOperationResult(true, false, after);
    }

    internal static IReadOnlyList<ValidationIssue> ValidateGuaranteedApplicability(
        WoundMaterializationEnvelope before,
        MortalWoundGuaranteedRouteDefinition route)
    {
        var simulation = MortalWoundTreatmentWorkingWoundSimulator.Simulate(
            before,
            new[] { route.Outcome.DeclaredResult });
        if (simulation.IsApplicable && simulation.Improved)
            return Array.Empty<ValidationIssue>();

        var issues = new List<ValidationIssue>();
        AddRequestIssue(
            issues,
            "mortal_wound_treatment_guaranteed_result_inapplicable",
            "one currently applicable and improving guaranteed result",
            route.RouteId);
        return new ReadOnlyCollection<ValidationIssue>(issues);
    }

    private static MortalWoundTreatmentAttemptRequestResult InvalidRequest(
        string code,
        string expected,
        string actual)
    {
        var issues = new List<ValidationIssue>();
        AddRequestIssue(issues, code, expected, actual);
        return MortalWoundTreatmentAttemptRequestResult.Invalid(issues);
    }

    private static void AddRequestIssue(
        ICollection<ValidationIssue> issues,
        string code,
        string expected,
        string actual) => issues.Add(new ValidationIssue(
        RequestIssuePath,
        IssueSeverity.Error,
        "The Mortal wound-treatment request cannot be trusted.",
        code: code,
        actor: "Client",
        section: "wound_materialization",
        expected: expected,
        actual: actual,
        repairHint:
        "Recreate treatment preparation from the current accepted state, exact complete history, wound, route, and client-owned authorities."));
}
