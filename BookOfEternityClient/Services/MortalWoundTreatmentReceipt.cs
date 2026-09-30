using System.Globalization;

namespace BookOfEternityClient.Services;

internal sealed partial class MortalWoundTreatmentAttemptCoordinates
{
    internal MortalWoundTreatmentAttemptCoordinates DetachedCopy() => new(
        SchemaVersion,
        SessionId,
        SessionGeneration,
        RequestId,
        SnapshotToken,
        OperationKey,
        AttemptId,
        WoundId,
        RouteId,
        ExpectedBeforeFingerprint,
        EventRef,
        EventKind,
        EventAuthorityId,
        EventSemanticFingerprint,
        Turn,
        Realm,
        ProviderKind,
        ProviderId,
        TargetKind,
        TargetId,
        LocationId,
        ContextFingerprint,
        AcceptedStateFingerprint,
        CoordinatesFingerprint);
}

internal sealed partial class MortalWoundProcedureModeEvidence
{
    internal MortalWoundProcedureModeEvidence DetachedCopy() => new(
        RollMode,
        RollActorKind,
        RollActorId,
        SourceIndices,
        SourceRolls,
        SelectedSourceIndex,
        NaturalRoll,
        Modifier,
        Total,
        BaseDifficulty,
        ComplicationDifficultyModifier,
        EffectiveDifficulty,
        Margin,
        OriginalOutcome,
        ResolvedOutcome,
        SelectedBandId,
        SelectedOutcomeIndex,
        ReactionEffectId,
        ReactionTriggerId,
        ReactionFingerprint,
        AcceptedRollFingerprint);
}

internal sealed partial class MortalWoundCourseModeEvidence
{
    internal MortalWoundCourseModeEvidence DetachedCopy() => new(
        CourseId,
        MilestoneOrdinal,
        CourseStartedAtGameTimeMinutes,
        ResolvedAtGameTimeMinutes,
        ClockEvidenceFingerprint,
        CourseDisposition);
}

internal sealed partial class MortalWoundGuaranteedModeEvidence
{
    internal MortalWoundGuaranteedModeEvidence DetachedCopy() => new(
        CapabilityRef,
        ActorRole,
        SkillId,
        SourceSemanticFingerprint,
        CapabilityProofFingerprint);
}

internal sealed partial class MortalWoundTreatmentReceipt
{
    internal static MortalWoundTreatmentReceipt Create(
        MortalWoundTreatmentResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        var coordinates = resolution.Coordinates.DetachedCopy();
        var evidence = DetachEvidence(resolution.ModeEvidence);
        var receiptFingerprint = ComputeFingerprint(
            resolution.Mode,
            coordinates,
            resolution.AttemptDisposition,
            resolution.ResultCategory,
            resolution.SelectedOutcomeIndex,
            resolution.Interruption,
            resolution.DeclaredResult,
            resolution.ConsumptionTrigger,
            resolution.CourseId,
            resolution.CourseMilestoneOrdinal,
            resolution.CourseDisposition,
            resolution.RequirementAuthority.AuthorityFingerprint,
            resolution.ResourceAuthority.AuthorityFingerprint,
            evidence,
            resolution.RouteFingerprint,
            resolution.ResolutionAuthorityFingerprint,
            resolution.RequestFingerprint,
            resolution.ResultFingerprint,
            resolution.RouteCompletion);
        return new MortalWoundTreatmentReceipt(
            resolution.Mode,
            coordinates,
            resolution.AttemptDisposition,
            resolution.ResultCategory,
            resolution.SelectedOutcomeIndex,
            resolution.Interruption,
            resolution.DeclaredResult,
            resolution.ConsumptionTrigger,
            resolution.CourseId,
            resolution.CourseMilestoneOrdinal,
            resolution.CourseDisposition,
            resolution.RequirementAuthority.AuthorityFingerprint,
            resolution.ResourceAuthority.AuthorityFingerprint,
            evidence,
            resolution.RouteFingerprint,
            resolution.ResolutionAuthorityFingerprint,
            resolution.RequestFingerprint,
            resolution.ResultFingerprint,
            resolution.RouteCompletion,
            receiptFingerprint);
    }

    internal bool HasMatchingFingerprint()
    {
        try
        {
            return string.Equals(
                ReceiptFingerprint,
                ComputeFingerprint(
                    Mode,
                    Coordinates,
                    AttemptDisposition,
                    ResultCategory,
                    SelectedOutcomeIndex,
                    Interruption,
                    DeclaredResult,
                    ConsumptionTrigger,
                    CourseId,
                    CourseMilestoneOrdinal,
                    CourseDisposition,
                    RequirementAuthorityFingerprint,
                    ResourceAuthorityFingerprint,
                    ModeEvidence,
                    RouteFingerprint,
                    ResolutionAuthorityFingerprint,
                    RequestFingerprint,
                    ResultFingerprint,
                    RouteCompletion),
                StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException or
                                           OverflowException)
        {
            return false;
        }
    }

    private static string ComputeFingerprint(
        string mode,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string attemptDisposition,
        string resultCategory,
        int? selectedOutcomeIndex,
        bool interruption,
        IReadOnlyList<MortalWoundTreatmentOperation> declaredResult,
        string consumptionTrigger,
        string? courseId,
        int? courseMilestoneOrdinal,
        string? courseDisposition,
        string requirementAuthorityFingerprint,
        string resourceAuthorityFingerprint,
        MortalWoundTreatmentModeEvidence modeEvidence,
        string routeFingerprint,
        string resolutionAuthorityFingerprint,
        string requestFingerprint,
        string resultFingerprint,
        string routeCompletion)
        => ComputePersistedFingerprint(
            mode,
            coordinates.CoordinatesFingerprint,
            attemptDisposition,
            resultCategory,
            selectedOutcomeIndex,
            interruption,
            declaredResult,
            consumptionTrigger,
            courseId,
            courseMilestoneOrdinal,
            courseDisposition,
            requirementAuthorityFingerprint,
            resourceAuthorityFingerprint,
            ModeEvidenceFingerprint(modeEvidence),
            routeFingerprint,
            resolutionAuthorityFingerprint,
            requestFingerprint,
            resultFingerprint,
            routeCompletion);

    internal static string ComputePersistedFingerprint(
        string mode,
        string coordinatesFingerprint,
        string attemptDisposition,
        string resultCategory,
        int? selectedOutcomeIndex,
        bool interruption,
        IReadOnlyList<MortalWoundTreatmentOperation> declaredResult,
        string consumptionTrigger,
        string? courseId,
        int? courseMilestoneOrdinal,
        string? courseDisposition,
        string requirementAuthorityFingerprint,
        string resourceAuthorityFingerprint,
        string modeEvidenceFingerprint,
        string routeFingerprint,
        string resolutionAuthorityFingerprint,
        string requestFingerprint,
        string resultFingerprint,
        string routeCompletion)
    {
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.receipt",
            "1",
            mode,
            coordinatesFingerprint,
            attemptDisposition,
            resultCategory,
            selectedOutcomeIndex?.ToString(CultureInfo.InvariantCulture),
            interruption ? "true" : "false",
            consumptionTrigger,
            courseId,
            courseMilestoneOrdinal?.ToString(CultureInfo.InvariantCulture),
            courseDisposition,
            requirementAuthorityFingerprint,
            resourceAuthorityFingerprint,
            modeEvidenceFingerprint,
            routeFingerprint,
            resolutionAuthorityFingerprint,
            requestFingerprint,
            resultFingerprint,
            routeCompletion,
            declaredResult.Count.ToString(CultureInfo.InvariantCulture)
        };
        for (var ordinal = 0; ordinal < declaredResult.Count; ordinal++)
        {
            fields.Add(MortalWoundTreatmentOutcomeIntentComposer.DeclaredFingerprint(
                ordinal,
                declaredResult[ordinal]));
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    internal static MortalWoundTreatmentModeEvidence DetachEvidence(
        MortalWoundTreatmentModeEvidence evidence) => evidence switch
        {
            MortalWoundProcedureModeEvidence value => value.DetachedCopy(),
            MortalWoundCourseModeEvidence value => value.DetachedCopy(),
            MortalWoundGuaranteedModeEvidence value => value.DetachedCopy(),
            _ => throw new ArgumentException(
                "Unsupported Mortal wound-treatment evidence.",
                nameof(evidence))
        };

    internal static string ModeEvidenceFingerprint(
        MortalWoundTreatmentModeEvidence evidence) => evidence switch
        {
            MortalWoundProcedureModeEvidence value => value.AcceptedRollFingerprint,
            MortalWoundCourseModeEvidence value => value.ClockEvidenceFingerprint,
            MortalWoundGuaranteedModeEvidence value => value.CapabilityProofFingerprint,
            _ => throw new ArgumentException(
                "Unsupported Mortal wound-treatment evidence.",
                nameof(evidence))
        };
}
