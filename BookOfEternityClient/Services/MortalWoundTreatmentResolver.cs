using System.Collections.ObjectModel;

namespace BookOfEternityClient.Services;

internal abstract class MortalWoundTreatmentModeAuthority
{
    private protected MortalWoundTreatmentModeAuthority()
    {
    }
}

internal abstract class MortalWoundTreatmentModeEvidence
{
    private protected MortalWoundTreatmentModeEvidence()
    {
    }
}

internal sealed partial class MortalWoundTreatmentAttemptRequestResult
{
    private readonly ReadOnlyCollection<ValidationIssue> _issues;

    private MortalWoundTreatmentAttemptRequestResult(
        bool isValid,
        IEnumerable<ValidationIssue> issues,
        MortalWoundTreatmentAttemptRequest? request)
    {
        IsValid = isValid;
        _issues = MortalWoundTreatmentShellDetachment.Freeze(issues);
        Request = request;
    }

    public bool IsValid { get; }
    public IReadOnlyList<ValidationIssue> Issues => _issues;
    public MortalWoundTreatmentAttemptRequest? Request { get; }
}

internal sealed partial class MortalWoundTreatmentAttemptRequest
{
    private MortalWoundTreatmentAttemptRequest(
        string mode,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        int? milestoneOrdinal,
        MortalWoundTreatmentModeAuthority modeAuthority,
        MortalWoundTreatmentRequirementAuthorityBundle requirementAuthority,
        MortalWoundTreatmentResourceReservationAuthority resourceAuthority,
        string requestFingerprint)
    {
        Mode = mode;
        Coordinates = coordinates;
        MilestoneOrdinal = milestoneOrdinal;
        ModeAuthority = modeAuthority;
        RequirementAuthority = requirementAuthority;
        ResourceAuthority = resourceAuthority;
        RequestFingerprint = requestFingerprint;
    }

    public string Mode { get; }
    public MortalWoundTreatmentAttemptCoordinates Coordinates { get; }
    public int? MilestoneOrdinal { get; }
    public MortalWoundTreatmentModeAuthority ModeAuthority { get; }
    public MortalWoundTreatmentRequirementAuthorityBundle RequirementAuthority { get; }
    public MortalWoundTreatmentResourceReservationAuthority ResourceAuthority { get; }
    public string RequestFingerprint { get; }
}

internal sealed partial class MortalWoundTreatmentResolutionResult
{
    private readonly ReadOnlyCollection<ValidationIssue> _issues;

    private MortalWoundTreatmentResolutionResult(
        string disposition,
        IEnumerable<ValidationIssue> issues,
        MortalWoundTreatmentResolution? resolution,
        MortalWoundTreatmentReceipt? replayReceipt)
    {
        Disposition = disposition;
        _issues = MortalWoundTreatmentShellDetachment.Freeze(issues);
        Resolution = resolution;
        ReplayReceipt = replayReceipt;
    }

    public string Disposition { get; }
    public IReadOnlyList<ValidationIssue> Issues => _issues;
    public MortalWoundTreatmentResolution? Resolution { get; }
    public MortalWoundTreatmentReceipt? ReplayReceipt { get; }
}

internal sealed partial class MortalWoundTreatmentResolution
{
    private readonly ReadOnlyCollection<MortalWoundTreatmentOperation> _declaredResult;
    private readonly ReadOnlyCollection<MortalWoundTreatmentOutcomeIntent> _outcomeIntents;

    private MortalWoundTreatmentResolution(
        string mode,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string attemptDisposition,
        string resultCategory,
        int? selectedOutcomeIndex,
        bool interruption,
        IEnumerable<MortalWoundTreatmentOperation> declaredResult,
        IEnumerable<MortalWoundTreatmentOutcomeIntent> outcomeIntents,
        MortalWoundCriticalReactionIntent? criticalReactionIntent,
        string consumptionTrigger,
        string? courseId,
        int? courseMilestoneOrdinal,
        string? courseDisposition,
        MortalWoundTreatmentAttemptRequest requestAuthority,
        MortalWoundTreatmentRequirementAuthorityBundle requirementAuthority,
        MortalWoundTreatmentResourceReservationAuthority resourceAuthority,
        MortalWoundTreatmentModeEvidence modeEvidence,
        string routeFingerprint,
        string resolutionAuthorityFingerprint,
        string requestFingerprint,
        string resultFingerprint,
        string routeCompletion)
    {
        Mode = mode;
        Coordinates = coordinates;
        AttemptDisposition = attemptDisposition;
        ResultCategory = resultCategory;
        SelectedOutcomeIndex = selectedOutcomeIndex;
        Interruption = interruption;
        _declaredResult = MortalWoundTreatmentShellDetachment.Freeze(declaredResult);
        _outcomeIntents = MortalWoundTreatmentShellDetachment.Freeze(outcomeIntents);
        CriticalReactionIntent = criticalReactionIntent;
        ConsumptionTrigger = consumptionTrigger;
        CourseId = courseId;
        CourseMilestoneOrdinal = courseMilestoneOrdinal;
        CourseDisposition = courseDisposition;
        RequestAuthority = requestAuthority;
        RequirementAuthority = requirementAuthority;
        ResourceAuthority = resourceAuthority;
        ModeEvidence = modeEvidence;
        RouteFingerprint = routeFingerprint;
        ResolutionAuthorityFingerprint = resolutionAuthorityFingerprint;
        RequestFingerprint = requestFingerprint;
        ResultFingerprint = resultFingerprint;
        RouteCompletion = routeCompletion;
    }

    public string Mode { get; }
    public MortalWoundTreatmentAttemptCoordinates Coordinates { get; }
    public string AttemptDisposition { get; }
    public string ResultCategory { get; }
    public int? SelectedOutcomeIndex { get; }
    public bool Interruption { get; }
    public IReadOnlyList<MortalWoundTreatmentOperation> DeclaredResult => _declaredResult;
    public IReadOnlyList<MortalWoundTreatmentOutcomeIntent> OutcomeIntents => _outcomeIntents;
    public MortalWoundCriticalReactionIntent? CriticalReactionIntent { get; }
    public string ConsumptionTrigger { get; }
    public string? CourseId { get; }
    public int? CourseMilestoneOrdinal { get; }
    public string? CourseDisposition { get; }
    public MortalWoundTreatmentAttemptRequest RequestAuthority { get; }
    public MortalWoundTreatmentRequirementAuthorityBundle RequirementAuthority { get; }
    public MortalWoundTreatmentResourceReservationAuthority ResourceAuthority { get; }
    public MortalWoundTreatmentModeEvidence ModeEvidence { get; }
    public string RouteFingerprint { get; }
    public string ResolutionAuthorityFingerprint { get; }
    public string RequestFingerprint { get; }
    public string ResultFingerprint { get; }
    public string RouteCompletion { get; }
}

internal sealed partial class MortalWoundTreatmentResourceReservationAuthority
{
    private MortalWoundTreatmentResourceReservationAuthority()
    {
    }
}

internal sealed partial class MortalWoundTreatmentReceipt
{
    private MortalWoundTreatmentReceipt()
    {
    }
}

internal sealed partial class MortalWoundProcedureModeEvidence :
    MortalWoundTreatmentModeEvidence
{
    private readonly ReadOnlyCollection<int> _sourceIndices;
    private readonly ReadOnlyCollection<int> _sourceRolls;

    private MortalWoundProcedureModeEvidence(
        string rollMode,
        string rollActorKind,
        string rollActorId,
        IEnumerable<int> sourceIndices,
        IEnumerable<int> sourceRolls,
        int selectedSourceIndex,
        int naturalRoll,
        int modifier,
        long total,
        int baseDifficulty,
        int complicationDifficultyModifier,
        int effectiveDifficulty,
        long margin,
        string originalOutcome,
        string resolvedOutcome,
        string selectedBandId,
        int selectedOutcomeIndex,
        string? reactionEffectId,
        string? reactionTriggerId,
        string? reactionFingerprint,
        string acceptedRollFingerprint)
    {
        RollMode = rollMode;
        RollActorKind = rollActorKind;
        RollActorId = rollActorId;
        _sourceIndices = MortalWoundTreatmentShellDetachment.Freeze(sourceIndices);
        _sourceRolls = MortalWoundTreatmentShellDetachment.Freeze(sourceRolls);
        SelectedSourceIndex = selectedSourceIndex;
        NaturalRoll = naturalRoll;
        Modifier = modifier;
        Total = total;
        BaseDifficulty = baseDifficulty;
        ComplicationDifficultyModifier = complicationDifficultyModifier;
        EffectiveDifficulty = effectiveDifficulty;
        Margin = margin;
        OriginalOutcome = originalOutcome;
        ResolvedOutcome = resolvedOutcome;
        SelectedBandId = selectedBandId;
        SelectedOutcomeIndex = selectedOutcomeIndex;
        ReactionEffectId = reactionEffectId;
        ReactionTriggerId = reactionTriggerId;
        ReactionFingerprint = reactionFingerprint;
        AcceptedRollFingerprint = acceptedRollFingerprint;
    }

    public string RollMode { get; }
    public string RollActorKind { get; }
    public string RollActorId { get; }
    public IReadOnlyList<int> SourceIndices => _sourceIndices;
    public IReadOnlyList<int> SourceRolls => _sourceRolls;
    public int SelectedSourceIndex { get; }
    public int NaturalRoll { get; }
    public int Modifier { get; }
    public long Total { get; }
    public int BaseDifficulty { get; }
    public int ComplicationDifficultyModifier { get; }
    public int EffectiveDifficulty { get; }
    public long Margin { get; }
    public string OriginalOutcome { get; }
    public string ResolvedOutcome { get; }
    public string SelectedBandId { get; }
    public int SelectedOutcomeIndex { get; }
    public string? ReactionEffectId { get; }
    public string? ReactionTriggerId { get; }
    public string? ReactionFingerprint { get; }
    public string AcceptedRollFingerprint { get; }
}

internal sealed partial class MortalWoundCourseModeEvidence :
    MortalWoundTreatmentModeEvidence
{
    private MortalWoundCourseModeEvidence(
        string courseId,
        int milestoneOrdinal,
        long courseStartedAtGameTimeMinutes,
        long resolvedAtGameTimeMinutes,
        string clockEvidenceFingerprint,
        string courseDisposition)
    {
        CourseId = courseId;
        MilestoneOrdinal = milestoneOrdinal;
        CourseStartedAtGameTimeMinutes = courseStartedAtGameTimeMinutes;
        ResolvedAtGameTimeMinutes = resolvedAtGameTimeMinutes;
        ClockEvidenceFingerprint = clockEvidenceFingerprint;
        CourseDisposition = courseDisposition;
    }

    public string CourseId { get; }
    public int MilestoneOrdinal { get; }
    public long CourseStartedAtGameTimeMinutes { get; }
    public long ResolvedAtGameTimeMinutes { get; }
    public string ClockEvidenceFingerprint { get; }
    public string CourseDisposition { get; }
}

internal sealed partial class MortalWoundGuaranteedModeEvidence :
    MortalWoundTreatmentModeEvidence
{
    private MortalWoundGuaranteedModeEvidence(
        string capabilityRef,
        string actorRole,
        string skillId,
        string sourceSemanticFingerprint,
        string capabilityProofFingerprint)
    {
        CapabilityRef = capabilityRef;
        ActorRole = actorRole;
        SkillId = skillId;
        SourceSemanticFingerprint = sourceSemanticFingerprint;
        CapabilityProofFingerprint = capabilityProofFingerprint;
    }

    public string CapabilityRef { get; }
    public string ActorRole { get; }
    public string SkillId { get; }
    public string SourceSemanticFingerprint { get; }
    public string CapabilityProofFingerprint { get; }
}

internal abstract class MortalWoundTreatmentOutcomeIntent
{
    private protected MortalWoundTreatmentOutcomeIntent(
        int operationOrdinal,
        string kind,
        string declaredOperationFingerprint,
        string intentFingerprint)
    {
        OperationOrdinal = operationOrdinal;
        Kind = kind;
        DeclaredOperationFingerprint = declaredOperationFingerprint;
        IntentFingerprint = intentFingerprint;
    }

    public int OperationOrdinal { get; }
    public string Kind { get; }
    public string DeclaredOperationFingerprint { get; }
    public string IntentFingerprint { get; }
}

internal sealed partial class MortalWoundNoImprovementOutcomeIntent :
    MortalWoundTreatmentOutcomeIntent
{
    private MortalWoundNoImprovementOutcomeIntent(
        int operationOrdinal,
        string kind,
        string declaredOperationFingerprint,
        string intentFingerprint)
        : base(operationOrdinal, kind, declaredOperationFingerprint, intentFingerprint)
    {
    }
}

internal sealed partial class MortalWoundStabilizeOutcomeIntent :
    MortalWoundTreatmentOutcomeIntent
{
    private MortalWoundStabilizeOutcomeIntent(
        int operationOrdinal,
        string kind,
        string declaredOperationFingerprint,
        string intentFingerprint)
        : base(operationOrdinal, kind, declaredOperationFingerprint, intentFingerprint)
    {
    }
}

internal sealed partial class MortalWoundAddRecoveryOutcomeIntent :
    MortalWoundTreatmentOutcomeIntent
{
    private MortalWoundAddRecoveryOutcomeIntent(
        int operationOrdinal,
        string kind,
        string declaredOperationFingerprint,
        string intentFingerprint,
        int points)
        : base(operationOrdinal, kind, declaredOperationFingerprint, intentFingerprint)
    {
        Points = points;
    }

    public int Points { get; }
}

internal sealed partial class MortalWoundReduceSeverityOutcomeIntent :
    MortalWoundTreatmentOutcomeIntent
{
    private MortalWoundReduceSeverityOutcomeIntent(
        int operationOrdinal,
        string kind,
        string declaredOperationFingerprint,
        string intentFingerprint,
        int steps)
        : base(operationOrdinal, kind, declaredOperationFingerprint, intentFingerprint)
    {
        Steps = steps;
    }

    public int Steps { get; }
}

internal sealed partial class MortalWoundRemoveComplicationOutcomeIntent :
    MortalWoundTreatmentOutcomeIntent
{
    private MortalWoundRemoveComplicationOutcomeIntent(
        int operationOrdinal,
        string kind,
        string declaredOperationFingerprint,
        string intentFingerprint,
        string complicationId)
        : base(operationOrdinal, kind, declaredOperationFingerprint, intentFingerprint)
    {
        ComplicationId = complicationId;
    }

    public string ComplicationId { get; }
}

internal sealed partial class MortalWoundApplyDeteriorationOutcomeIntent :
    MortalWoundTreatmentOutcomeIntent
{
    private MortalWoundApplyDeteriorationOutcomeIntent(
        int operationOrdinal,
        string kind,
        string declaredOperationFingerprint,
        string intentFingerprint,
        string policyRef,
        string deteriorationAuthorityFingerprint)
        : base(operationOrdinal, kind, declaredOperationFingerprint, intentFingerprint)
    {
        PolicyRef = policyRef;
        DeteriorationAuthorityFingerprint = deteriorationAuthorityFingerprint;
    }

    public string PolicyRef { get; }
    public string DeteriorationAuthorityFingerprint { get; }
}

internal sealed partial class MortalWoundAddComplicationOutcomeIntent :
    MortalWoundTreatmentOutcomeIntent
{
    private readonly ReadOnlyCollection<MortalWoundTreatmentReferenceBinding>
        _definitionReferenceBindings;
    private readonly ReadOnlyCollection<MortalWoundTreatmentReferenceBinding>
        _applicationReferenceBindings;

    private MortalWoundAddComplicationOutcomeIntent(
        int operationOrdinal,
        string kind,
        string declaredOperationFingerprint,
        string intentFingerprint,
        string complicationRef,
        string complicationId,
        IEnumerable<MortalWoundTreatmentReferenceBinding> definitionReferenceBindings,
        IEnumerable<MortalWoundTreatmentReferenceBinding> applicationReferenceBindings,
        string preparationFingerprint)
        : base(operationOrdinal, kind, declaredOperationFingerprint, intentFingerprint)
    {
        ComplicationRef = complicationRef;
        ComplicationId = complicationId;
        _definitionReferenceBindings = MortalWoundTreatmentShellDetachment.Freeze(
            definitionReferenceBindings);
        _applicationReferenceBindings = MortalWoundTreatmentShellDetachment.Freeze(
            applicationReferenceBindings);
        PreparationFingerprint = preparationFingerprint;
    }

    public string ComplicationRef { get; }
    public string ComplicationId { get; }
    public IReadOnlyList<MortalWoundTreatmentReferenceBinding> DefinitionReferenceBindings =>
        _definitionReferenceBindings;
    public IReadOnlyList<MortalWoundTreatmentReferenceBinding> ApplicationReferenceBindings =>
        _applicationReferenceBindings;
    public string PreparationFingerprint { get; }
}

internal sealed partial class MortalWoundHealOutcomeIntent :
    MortalWoundTreatmentOutcomeIntent
{
    private readonly ReadOnlyCollection<MortalWoundTreatmentLegacySeedBinding>
        _legacySeedBindings;

    private MortalWoundHealOutcomeIntent(
        int operationOrdinal,
        string kind,
        string declaredOperationFingerprint,
        string intentFingerprint,
        MortalWoundHealChildCoordinates healChildCoordinates,
        IEnumerable<MortalWoundTreatmentLegacySeedBinding> legacySeedBindings,
        string preparationFingerprint)
        : base(operationOrdinal, kind, declaredOperationFingerprint, intentFingerprint)
    {
        HealChildCoordinates = healChildCoordinates;
        _legacySeedBindings = MortalWoundTreatmentShellDetachment.Freeze(legacySeedBindings);
        PreparationFingerprint = preparationFingerprint;
    }

    public MortalWoundHealChildCoordinates HealChildCoordinates { get; }
    public IReadOnlyList<MortalWoundTreatmentLegacySeedBinding> LegacySeedBindings =>
        _legacySeedBindings;
    public string PreparationFingerprint { get; }
}

internal sealed partial class MortalWoundTreatmentReferenceBinding
{
    private MortalWoundTreatmentReferenceBinding(string localRef, string namespacedRef)
    {
        LocalRef = localRef;
        NamespacedRef = namespacedRef;
    }

    public string LocalRef { get; }
    public string NamespacedRef { get; }
}

internal sealed partial class MortalWoundHealChildCoordinates
{
    private MortalWoundHealChildCoordinates(
        string operationKey,
        string transitionId,
        string eventRef,
        string causalEventRef)
    {
        OperationKey = operationKey;
        TransitionId = transitionId;
        EventRef = eventRef;
        CausalEventRef = causalEventRef;
    }

    public string OperationKey { get; }
    public string TransitionId { get; }
    public string EventRef { get; }
    public string CausalEventRef { get; }
}

internal sealed partial class MortalWoundTreatmentLegacySeedBinding
{
    private readonly ReadOnlyCollection<MortalWoundTreatmentReferenceBinding>
        _definitionReferenceBindings;
    private readonly ReadOnlyCollection<MortalWoundTreatmentReferenceBinding>
        _applicationReferenceBindings;

    private MortalWoundTreatmentLegacySeedBinding(
        int legacyOrdinal,
        string localLegacyRef,
        string legacyId,
        string kind,
        IEnumerable<MortalWoundTreatmentReferenceBinding> definitionReferenceBindings,
        IEnumerable<MortalWoundTreatmentReferenceBinding> applicationReferenceBindings,
        string declaredLegacyFingerprint,
        string seedFingerprint)
    {
        LegacyOrdinal = legacyOrdinal;
        LocalLegacyRef = localLegacyRef;
        LegacyId = legacyId;
        Kind = kind;
        _definitionReferenceBindings = MortalWoundTreatmentShellDetachment.Freeze(
            definitionReferenceBindings);
        _applicationReferenceBindings = MortalWoundTreatmentShellDetachment.Freeze(
            applicationReferenceBindings);
        DeclaredLegacyFingerprint = declaredLegacyFingerprint;
        SeedFingerprint = seedFingerprint;
    }

    public int LegacyOrdinal { get; }
    public string LocalLegacyRef { get; }
    public string LegacyId { get; }
    public string Kind { get; }
    public IReadOnlyList<MortalWoundTreatmentReferenceBinding> DefinitionReferenceBindings =>
        _definitionReferenceBindings;
    public IReadOnlyList<MortalWoundTreatmentReferenceBinding> ApplicationReferenceBindings =>
        _applicationReferenceBindings;
    public string DeclaredLegacyFingerprint { get; }
    public string SeedFingerprint { get; }
}

internal sealed partial class MortalWoundCriticalReactionResolutionResult
{
    private readonly ReadOnlyCollection<ValidationIssue> _issues;

    private MortalWoundCriticalReactionResolutionResult(
        bool isValid,
        IEnumerable<ValidationIssue> issues,
        MortalWoundCriticalReactionIntent? intent)
    {
        IsValid = isValid;
        _issues = MortalWoundTreatmentShellDetachment.Freeze(issues);
        Intent = intent;
    }

    public bool IsValid { get; }
    public IReadOnlyList<ValidationIssue> Issues => _issues;
    public MortalWoundCriticalReactionIntent? Intent { get; }
}

internal sealed partial class MortalWoundCriticalReactionIntent
{
    private MortalWoundCriticalReactionIntent(
        string eventType,
        string eventRef,
        string causalEventRef,
        int turn,
        string realm,
        string targetKind,
        string targetId,
        string effectId,
        string triggerId,
        string acceptedEffectFingerprint,
        string preparedReactionFingerprint,
        string requestFingerprint,
        string intentFingerprint)
    {
        EventType = eventType;
        EventRef = eventRef;
        CausalEventRef = causalEventRef;
        Turn = turn;
        Realm = realm;
        TargetKind = targetKind;
        TargetId = targetId;
        EffectId = effectId;
        TriggerId = triggerId;
        AcceptedEffectFingerprint = acceptedEffectFingerprint;
        PreparedReactionFingerprint = preparedReactionFingerprint;
        RequestFingerprint = requestFingerprint;
        IntentFingerprint = intentFingerprint;
    }

    public string EventType { get; }
    public string EventRef { get; }
    public string CausalEventRef { get; }
    public int Turn { get; }
    public string Realm { get; }
    public string TargetKind { get; }
    public string TargetId { get; }
    public string EffectId { get; }
    public string TriggerId { get; }
    public string AcceptedEffectFingerprint { get; }
    public string PreparedReactionFingerprint { get; }
    public string RequestFingerprint { get; }
    public string IntentFingerprint { get; }
}

internal static class MortalWoundTreatmentShellDetachment
{
    internal static ReadOnlyCollection<T> Freeze<T>(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new ReadOnlyCollection<T>(values.ToArray());
    }
}
