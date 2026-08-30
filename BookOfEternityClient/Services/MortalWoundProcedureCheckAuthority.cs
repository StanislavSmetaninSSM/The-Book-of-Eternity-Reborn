using System.Collections.ObjectModel;

namespace BookOfEternityClient.Services;

internal sealed partial class MortalWoundProcedureCheckAuthorityResult
{
    private readonly ReadOnlyCollection<ValidationIssue> _issues;

    private MortalWoundProcedureCheckAuthorityResult(
        bool isValid,
        IEnumerable<ValidationIssue> issues,
        MortalWoundProcedureCheckAuthority? authority)
    {
        IsValid = isValid;
        _issues = MortalWoundTreatmentShellDetachment.Freeze(issues);
        Authority = authority;
    }

    public bool IsValid { get; }
    public IReadOnlyList<ValidationIssue> Issues => _issues;
    public MortalWoundProcedureCheckAuthority? Authority { get; }

    internal static MortalWoundProcedureCheckAuthorityResult Invalid(
        ValidationIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return new MortalWoundProcedureCheckAuthorityResult(
            false,
            new[] { issue },
            null);
    }
}

internal sealed partial class MortalWoundProcedureCheckAuthority :
    MortalWoundTreatmentModeAuthority
{
    private readonly ReadOnlyCollection<MortalWoundProcedureRollContribution>
        _rollContributions;
    private readonly ReadOnlyCollection<int> _sourceIndices;
    private readonly ReadOnlyCollection<int> _sourceRolls;

    private MortalWoundProcedureCheckAuthority(
        string sourcePath,
        string rollMode,
        string rollActorKind,
        string rollActorId,
        IEnumerable<MortalWoundProcedureRollContribution> rollContributions,
        IEnumerable<int> sourceIndices,
        IEnumerable<int> sourceRolls,
        int selectedSourceIndex,
        int naturalRoll,
        int modifier,
        int complicationDifficultyModifier,
        int effectiveDifficulty,
        string requirementAuthorityFingerprint,
        string coordinatesFingerprint,
        string acceptedStateFingerprint,
        MortalWoundPreparedCriticalReaction? preparedCriticalReaction,
        string authorityFingerprint)
    {
        SourcePath = sourcePath;
        RollMode = rollMode;
        RollActorKind = rollActorKind;
        RollActorId = rollActorId;
        _rollContributions = MortalWoundTreatmentShellDetachment.Freeze(rollContributions);
        _sourceIndices = MortalWoundTreatmentShellDetachment.Freeze(sourceIndices);
        _sourceRolls = MortalWoundTreatmentShellDetachment.Freeze(sourceRolls);
        SelectedSourceIndex = selectedSourceIndex;
        NaturalRoll = naturalRoll;
        Modifier = modifier;
        ComplicationDifficultyModifier = complicationDifficultyModifier;
        EffectiveDifficulty = effectiveDifficulty;
        RequirementAuthorityFingerprint = requirementAuthorityFingerprint;
        CoordinatesFingerprint = coordinatesFingerprint;
        AcceptedStateFingerprint = acceptedStateFingerprint;
        PreparedCriticalReaction = preparedCriticalReaction;
        AuthorityFingerprint = authorityFingerprint;
    }

    public string SourcePath { get; }
    public string RollMode { get; }
    public string RollActorKind { get; }
    public string RollActorId { get; }
    public IReadOnlyList<MortalWoundProcedureRollContribution> RollContributions =>
        _rollContributions;
    public IReadOnlyList<int> SourceIndices => _sourceIndices;
    public IReadOnlyList<int> SourceRolls => _sourceRolls;
    public int SelectedSourceIndex { get; }
    public int NaturalRoll { get; }
    public int Modifier { get; }
    public int ComplicationDifficultyModifier { get; }
    public int EffectiveDifficulty { get; }
    public string RequirementAuthorityFingerprint { get; }
    public string CoordinatesFingerprint { get; }
    public string AcceptedStateFingerprint { get; }
    public MortalWoundPreparedCriticalReaction? PreparedCriticalReaction { get; }
    public string AuthorityFingerprint { get; }

    internal static MortalWoundProcedureCheckAuthorityResult Create(
        MortalWoundTreatmentAttemptCoordinates? coordinates,
        MortalWoundProcedureRouteDefinition? route,
        WoundMaterializationEnvelope? before,
        MortalWoundTreatmentRequirementAuthorityBundle? requirementAuthority,
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState) =>
        MortalWoundProcedureCheckAuthorityResult.Invalid(
            new ValidationIssue(
                LiveTurnPreparationService.TurnRequestPath,
                IssueSeverity.Error,
                "Procedure-check authority is not available until the dice/Fate authority slice is installed.",
                code: "wound_treatment_procedure_authority_unavailable",
                section: "MortalWoundTreatmentProcedureAuthority",
                expected: "completed T067-A procedure dice/Fate authority",
                actual: "structural shell only"));

    internal bool ReleaseProvisionalReservations(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState) => false;
}

internal sealed partial class MortalWoundProcedureRollContribution
{
    private MortalWoundProcedureRollContribution(
        string effectId,
        string componentId,
        string contribution)
    {
        EffectId = effectId;
        ComponentId = componentId;
        Contribution = contribution;
    }

    public string EffectId { get; }
    public string ComponentId { get; }
    public string Contribution { get; }
}

internal sealed partial class MortalWoundPreparedCriticalReaction
{
    private MortalWoundPreparedCriticalReaction(
        string effectId,
        string triggerId,
        string acceptedEffectFingerprint,
        string preparedReactionFingerprint)
    {
        EffectId = effectId;
        TriggerId = triggerId;
        AcceptedEffectFingerprint = acceptedEffectFingerprint;
        PreparedReactionFingerprint = preparedReactionFingerprint;
    }

    public string EffectId { get; }
    public string TriggerId { get; }
    public string AcceptedEffectFingerprint { get; }
    public string PreparedReactionFingerprint { get; }
}
