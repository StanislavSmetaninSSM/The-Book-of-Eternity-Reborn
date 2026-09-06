using System.Collections.Immutable;

namespace BookOfEternityClient.Services;

internal sealed record WoundAlternativeTreatmentResponseDraft(
    string AuthoringRequestRef,
    string Decision,
    GmTreatmentRouteDraft? Route,
    MortalWoundDiagnosisPathDefinition? DiagnosisPath);

internal sealed record WoundAlternativeTreatmentResponseParseResult(
    bool IsValid,
    ImmutableArray<ValidationIssue> Issues,
    ImmutableArray<WoundAlternativeTreatmentResponseDraft> Drafts);
