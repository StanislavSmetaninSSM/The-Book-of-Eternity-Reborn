using System.Collections.Immutable;

namespace BookOfEternityClient.Services;

internal abstract record GmTreatmentRouteDraft(
    string RouteId,
    string DisplayName,
    string Visibility,
    ImmutableArray<MortalWoundTreatmentRequirement> Requirements,
    MortalWoundTreatmentResourcePolicy ResourcePolicy,
    string SourcePath)
{
    internal abstract string Mode { get; }
}

internal sealed record GmProcedureRouteDraft(
    string RouteId,
    string DisplayName,
    string Visibility,
    ImmutableArray<MortalWoundTreatmentRequirement> Requirements,
    MortalWoundTreatmentResourcePolicy ResourcePolicy,
    MortalWoundProcedureResolution Resolution,
    ImmutableArray<GmProcedureBandDraft> Bands,
    string SourcePath) : GmTreatmentRouteDraft(
        RouteId, DisplayName, Visibility, Requirements, ResourcePolicy, SourcePath)
{
    internal override string Mode => "procedure";
}

internal sealed record GmCourseRouteDraft(
    string RouteId,
    string DisplayName,
    string Visibility,
    ImmutableArray<MortalWoundTreatmentRequirement> Requirements,
    MortalWoundTreatmentResourcePolicy ResourcePolicy,
    MortalWoundCourseResolution Resolution,
    ImmutableArray<GmCourseMilestoneDraft> Milestones,
    GmCategoryResultDraft Interruption,
    string SourcePath) : GmTreatmentRouteDraft(
        RouteId, DisplayName, Visibility, Requirements, ResourcePolicy, SourcePath)
{
    internal override string Mode => "course";
}

internal sealed record GmGuaranteedRouteDraft(
    string RouteId,
    string DisplayName,
    string Visibility,
    ImmutableArray<MortalWoundTreatmentRequirement> Requirements,
    MortalWoundTreatmentResourcePolicy ResourcePolicy,
    MortalWoundGuaranteedResolution Resolution,
    GmCategoryResultDraft Outcome,
    string SourcePath) : GmTreatmentRouteDraft(
        RouteId, DisplayName, Visibility, Requirements, ResourcePolicy, SourcePath)
{
    internal override string Mode => "guaranteed";
}

internal sealed record GmProcedureBandDraft(
    string BandId,
    long? MinimumMargin,
    long? MaximumMargin,
    string Category,
    ImmutableArray<GmTreatmentOperationDraft> DeclaredResult);

internal sealed record GmCourseMilestoneDraft(
    int Ordinal,
    long AfterMinutes,
    ImmutableArray<MortalWoundTreatmentRequirement> Requirements,
    string Category,
    string Completion,
    ImmutableArray<GmTreatmentOperationDraft> DeclaredResult);

internal sealed record GmCategoryResultDraft(
    string Category,
    ImmutableArray<GmTreatmentOperationDraft> DeclaredResult);

internal abstract record GmTreatmentOperationDraft
{
    internal abstract string Kind { get; }
}

internal sealed record GmNoImprovementDraft : GmTreatmentOperationDraft
{
    internal override string Kind => "no_improvement";
}

internal sealed record GmStabilizeDraft : GmTreatmentOperationDraft
{
    internal override string Kind => "stabilize";
}

internal sealed record GmAddRecoveryDraft(int Points) : GmTreatmentOperationDraft
{
    internal override string Kind => "add_recovery";
}

internal sealed record GmReduceSeverityDraft(int Steps) : GmTreatmentOperationDraft
{
    internal override string Kind => "reduce_severity";
}

internal sealed record GmRemoveComplicationDraft(string ComplicationRef) : GmTreatmentOperationDraft
{
    internal override string Kind => "remove_complication";
}

internal sealed record GmAddComplicationDraft(
    MortalWoundComplicationProposalDraft ComplicationDraft) : GmTreatmentOperationDraft
{
    internal override string Kind => "add_complication";
}

internal sealed record GmApplyDeteriorationDraft(string PolicyRef) : GmTreatmentOperationDraft
{
    internal override string Kind => "apply_deterioration";
}

internal sealed record GmHealDraft(
    ImmutableArray<MortalWoundHealLegacyDraft> Legacies) : GmTreatmentOperationDraft
{
    internal override string Kind => "heal";
}

internal sealed record GmTreatmentRouteShapeParseResult(
    bool IsValid,
    ImmutableArray<ValidationIssue> Issues,
    GmTreatmentRouteDraft? Route);
