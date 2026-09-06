namespace BookOfEternityClient.Services;

internal static partial class MortalWoundTreatmentOutcomePublicationPlanner
{
    // Detached admission and finalization share one selected-route derivation. This
    // helper never looks up live state or publishes any part of a treatment.
    internal static bool DetachedSelectionAgrees(
        MortalWoundTreatmentResolution resolution, long currentGameMinute)
    {
        var request = resolution.RequestAuthority;
        if (request is null || !MortalWoundTreatmentDetachedSealValidator.IsValid(request) ||
            request.Mode != resolution.Mode ||
            resolution.Mode is not ("guaranteed" or "procedure" or "course") ||
            resolution.AttemptDisposition != "AcceptedTerminal" ||
            request.RequestFingerprint != resolution.RequestFingerprint ||
            request.Coordinates.CoordinatesFingerprint != resolution.Coordinates.CoordinatesFingerprint ||
            !MortalWoundTreatmentDetachedSealValidator.TryGetRoute(request, out var route) || route is null)
            return false;
        if (resolution.Mode == "course")
        {
            if (request.ModeAuthority is not MortalWoundCourseModeAuthority authority ||
                resolution.ModeEvidence is not MortalWoundCourseModeEvidence evidence ||
                request.MilestoneOrdinal != authority.MilestoneOrdinal ||
                resolution.CourseMilestoneOrdinal != authority.MilestoneOrdinal ||
                evidence.MilestoneOrdinal != authority.MilestoneOrdinal ||
                resolution.CourseId != authority.CourseId || evidence.CourseId != authority.CourseId ||
                evidence.CourseDisposition != resolution.CourseDisposition ||
                evidence.ResolvedAtGameTimeMinutes != currentGameMinute ||
                resolution.RouteFingerprint != authority.CourseStartAuthority.RouteFingerprint ||
                resolution.CriticalReactionIntent is not null)
                return false;
        }
        else if (resolution.Interruption || resolution.CourseId is not null ||
            resolution.CourseMilestoneOrdinal is not null || resolution.CourseDisposition is not null ||
            (resolution.Mode == "guaranteed" && resolution.CriticalReactionIntent is not null))
            return false;
        if (resolution.RouteFingerprint != request.RequirementAuthority.RouteFingerprint ||
            resolution.RouteCompletion != MortalWoundTreatmentPlanner.DeriveRouteCompletion(
                request.RouteSourceWound, route, resolution.ResultCategory,
                resolution.Interruption, resolution.CourseDisposition) ||
            resolution.ConsumptionTrigger != MortalWoundTreatmentPlanner.DeriveConsumptionTrigger(
                route, resolution.ResultCategory, resolution.Interruption) ||
            !TryProjectActiveCourseId(request.RouteSourceWound, request, resolution, out _) ||
            !HasSupportedGrammar(resolution.Mode, resolution.Interruption,
                resolution.CourseDisposition, resolution.OutcomeIntents) ||
            !MortalWoundTreatmentResolution.TryRecomputeModeEvidenceFingerprint(resolution, out var evidenceFingerprint))
            return false;
        return resolution.ResolutionAuthorityFingerprint ==
            MortalWoundTreatmentResolution.ComputeResolutionAuthorityFingerprint(
                resolution.RequestFingerprint, resolution.Mode, resolution.AttemptDisposition,
                resolution.ResultCategory, resolution.SelectedOutcomeIndex, resolution.Interruption,
                resolution.ConsumptionTrigger, resolution.CourseId, resolution.CourseMilestoneOrdinal,
                resolution.CourseDisposition, resolution.RouteFingerprint, resolution.RouteCompletion,
                resolution.CriticalReactionIntent?.IntentFingerprint, evidenceFingerprint!) &&
            resolution.ResultFingerprint == MortalWoundTreatmentResolution.ComputeResultFingerprint(
                resolution.ResolutionAuthorityFingerprint, resolution.DeclaredResult);
    }

    private static bool TryProjectActiveCourseId(
        WoundMaterializationEnvelope before,
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResolution resolution,
        out string? projected)
    {
        projected = before.Care.ActiveCourseId;
        if (resolution.Mode != "course")
            return resolution.CourseId is null && resolution.CourseMilestoneOrdinal is null &&
                resolution.CourseDisposition is null;
        if (request.ModeAuthority is not MortalWoundCourseModeAuthority authority ||
            request.MilestoneOrdinal != authority.MilestoneOrdinal ||
            resolution.CourseMilestoneOrdinal != authority.MilestoneOrdinal ||
            !string.Equals(resolution.CourseId, authority.CourseId, StringComparison.Ordinal))
            return false;
        var first = authority.MilestoneOrdinal == 1;
        var ownsPointer = string.Equals(before.Care.ActiveCourseId,
            authority.CourseId, StringComparison.Ordinal);
        switch (resolution.CourseDisposition)
        {
            case "active" when !resolution.Interruption &&
                    (first ? before.Care.ActiveCourseId is null : ownsPointer):
                projected = authority.CourseId;
                return true;
            case "completed" when !resolution.Interruption &&
                    (first ? before.Care.ActiveCourseId is null : ownsPointer):
                projected = null;
                return true;
            case "interrupted" when resolution.Interruption &&
                    authority.MilestoneOrdinal > 1 && ownsPointer:
                projected = null;
                return true;
            default:
                return false;
        }
    }
}
