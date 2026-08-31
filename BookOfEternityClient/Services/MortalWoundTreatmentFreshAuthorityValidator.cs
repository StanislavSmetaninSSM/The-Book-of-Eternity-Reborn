using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class MortalWoundTreatmentFreshAuthorityValidator
{
    internal static string? FindMismatch(
        MortalWoundTreatmentAttemptRequest request,
        WoundHistoryParseResult history,
        WoundMaterializationEnvelope before,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentRouteDefinition route)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(acceptedState);
        ArgumentNullException.ThrowIfNull(route);

        try
        {
            return request.Mode switch
            {
                "procedure" when
                    route is MortalWoundProcedureRouteDefinition procedureRoute &&
                    request.ModeAuthority is MortalWoundProcedureCheckAuthority procedure =>
                    FindProcedureMismatch(
                        request,
                        before,
                        acceptedState,
                        procedureRoute,
                        procedure),
                "guaranteed" when
                    route is MortalWoundGuaranteedRouteDefinition guaranteedRoute &&
                    request.ModeAuthority is MortalWoundTreatmentCapabilityProof proof =>
                    FindGuaranteedMismatch(
                        request,
                        before,
                        acceptedState,
                        guaranteedRoute,
                        proof),
                "course" when
                    route is MortalWoundCourseRouteDefinition &&
                    request.ModeAuthority is MortalWoundCourseModeAuthority course =>
                    FindCourseMismatch(
                        request,
                        history,
                        before,
                        acceptedState,
                        course),
                _ => "mode_authority"
            };
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException or
                                           OverflowException or
                                           System.Text.Json.JsonException)
        {
            return exception.GetType().Name;
        }
    }

    private static string? FindProcedureMismatch(
        MortalWoundTreatmentAttemptRequest request,
        WoundMaterializationEnvelope before,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundProcedureRouteDefinition route,
        MortalWoundProcedureCheckAuthority procedure)
    {
        if (MortalWoundTreatmentPlanner.ValidateProcedureApplicability(
                before,
                route,
                acceptedState,
                request.Coordinates).Count != 0)
        {
            return "procedure_applicability";
        }
        var requirement =
            MortalWoundTreatmentRequirementAuthorityBundle.CreateForProcedure(
                acceptedState,
                request.Coordinates,
                before);
        if (!requirement.IsValid || requirement.Authority is null ||
            !SemanticEquals(requirement.Authority, request.RequirementAuthority))
        {
            return "requirement_authority";
        }
        return procedure.MatchesFreshAcceptedState(
            acceptedState,
            request.Coordinates,
            route,
            before,
            requirement.Authority)
            ? null
            : "procedure_authority";
    }

    private static string? FindGuaranteedMismatch(
        MortalWoundTreatmentAttemptRequest request,
        WoundMaterializationEnvelope before,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundGuaranteedRouteDefinition route,
        MortalWoundTreatmentCapabilityProof proof)
    {
        if (MortalWoundTreatmentPlanner.ValidateGuaranteedApplicability(
                before,
                route).Count != 0)
        {
            return "guaranteed_applicability";
        }
        var requirement =
            MortalWoundTreatmentRequirementAuthorityBundle.CreateForGuaranteed(
                acceptedState,
                request.Coordinates,
                before);
        if (!requirement.IsValid || requirement.Authority is null ||
            !SemanticEquals(requirement.Authority, request.RequirementAuthority))
        {
            return "requirement_authority";
        }
        var freshProof = MortalWoundTreatmentCapabilityAuthority.ExportCurrent(
            acceptedState,
            request.Coordinates,
            route.Resolution.CapabilityRef,
            route.Resolution.ActorRole);
        return freshProof.IsValid &&
               freshProof.Proof is not null &&
               SemanticEquals(freshProof.Proof, proof)
            ? null
            : "capability_proof";
    }

    private static string? FindCourseMismatch(
        MortalWoundTreatmentAttemptRequest request,
        WoundHistoryParseResult history,
        WoundMaterializationEnvelope before,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundCourseModeAuthority course)
    {
        var gameTime = MortalWoundGameTimeAuthority.Create(
            acceptedState,
            request.Coordinates);
        if (!gameTime.IsValid || gameTime.Authority is null)
            return "game_time_authority";
        var freshCourse = MortalWoundCourseModeAuthority.Create(
            acceptedState,
            request.Coordinates,
            before,
            history,
            gameTime.Authority);
        if (freshCourse.Authority is null ||
            !SemanticEquals(freshCourse.Authority, course))
        {
            return "course_authority";
        }
        var requirement =
            MortalWoundTreatmentRequirementAuthorityBundle.CreateForCourseMilestone(
                acceptedState,
                request.Coordinates,
                before,
                history,
                freshCourse.Authority);
        return requirement.Authority is not null &&
               SemanticEquals(requirement.Authority, request.RequirementAuthority)
            ? null
            : "requirement_authority";
    }

    private static bool SemanticEquals(object left, object right)
    {
        var leftNode = WoundResponseInputComposer
            .SerializeMortalWoundTreatmentValue(left) as JsonObject;
        var rightNode = WoundResponseInputComposer
            .SerializeMortalWoundTreatmentValue(right) as JsonObject;
        return leftNode is not null &&
               rightNode is not null &&
               MortalWoundTreatmentCommandCodec.SemanticJsonEquals(
                   leftNode,
                   rightNode);
    }
}
