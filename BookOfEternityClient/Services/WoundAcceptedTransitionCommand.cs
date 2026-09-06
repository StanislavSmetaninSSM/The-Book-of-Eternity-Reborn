namespace BookOfEternityClient.Services;

internal abstract record WoundAcceptedTransitionCommandDraft(
    string CommandRef, string OperationKey, string? FinalSceneText)
{
    internal abstract string TransitionKind { get; }
}

internal sealed record WoundDiagnosisCommandAuthority(
    string CommandRef, string OperationKey, string AttemptId, string WoundId,
    string DiagnosisPathId, string ExpectedBeforeFingerprint, string PathFingerprint,
    string RequirementAuthorityFingerprint, string CheckResultFingerprint,
    string AuthorityFingerprint);

internal sealed record WoundAlternativeTreatmentCommandAuthority(
    string AuthoringRequestRef, string RequestAuthorityFingerprint, string OperationKey,
    string WoundId, string EventRef, string AddedRouteId, string? AddedDiagnosisPathId,
    string ExpectedBeforeFingerprint, string ExpectedAfterFingerprint,
    string RouteFingerprint, string? DiagnosisPathFingerprint,
    string EvidenceAuthorityFingerprint, string RequirementAuthorityFingerprint,
    string AuthorityFingerprint);

internal sealed record WoundDiagnosisCommandDraft(
    string CommandRef, string OperationKey, string? FinalSceneText,
    WoundDiagnosisCommandAuthority Authority, WoundDiagnosisTransitionResult Result)
    : WoundAcceptedTransitionCommandDraft(CommandRef, OperationKey, FinalSceneText)
{
    internal override string TransitionKind => "diagnose";
}

internal sealed record WoundAlternativeTreatmentCommandDraft(
    string CommandRef, string OperationKey, string? FinalSceneText,
    WoundAlternativeTreatmentCommandAuthority Authority,
    MortalWoundTreatmentRouteDefinition Route, MortalWoundDiagnosisPathDefinition? DiagnosisPath,
    WoundAlternativeTreatmentTransitionResult Result)
    : WoundAcceptedTransitionCommandDraft(CommandRef, OperationKey, FinalSceneText)
{
    internal override string TransitionKind => "author_alternative_treatment";
}
