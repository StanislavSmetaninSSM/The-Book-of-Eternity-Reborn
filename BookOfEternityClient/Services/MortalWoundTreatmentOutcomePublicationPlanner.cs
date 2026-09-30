using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record WoundFinalAssemblyScalars(WoundSeverity Severity, WoundCare Care,
    WoundRecovery Recovery, WoundTreatment Treatment, WoundLastTransition LastTransition, int SlotBudget);
internal sealed record MortalWoundTreatmentSelectedRoot(WoundWorkingReference Reference, string DefinitionKey,
    WoundRootOwnershipDomain OwnershipDomain, ImmutableArray<WoundEffectSlotAgreement> Slots,
    string? OriginalEffectId, MortalWoundTreatmentComplicationRootBinding? AdditionBinding);
internal sealed record MortalWoundTreatmentSelectedAddition(WoundWorkingReference Reference,
    MortalWoundTreatmentComplicationBindingPreparation Binding);
internal sealed record MortalWoundTreatmentSelectedDefinitionOrigin(int OperationOrdinal, int DefinitionOrdinal,
    string DefinitionKey, string MappedDefinitionRef, string ComponentsPath);

internal sealed class MortalWoundTreatmentSelectedGraphCompilation
{
    private readonly WoundMaterializationEnvelope _before;
    private readonly MortalWoundTreatmentWorkingGraphProjection _working;
    private readonly WoundFinalAssemblyScalars _scalars;
    private MortalWoundTreatmentSelectedGraphCompilation(WoundMaterializationEnvelope before,
        MortalWoundTreatmentWorkingGraphProjection working, WoundFinalAssemblyScalars scalars,
        ImmutableArray<MortalWoundTreatmentSelectedRoot> roots,
        ImmutableArray<MortalWoundTreatmentSelectedAddition> additions,
        ImmutableArray<MortalWoundTreatmentSelectedDefinitionOrigin> origins,
        ImmutableArray<string> removals, ImmutableArray<string> terminals, bool finalSeverityChanged, bool allowsWorsening, string fingerprint)
    {
        _before = WoundAcceptedTurnData.CloneWound(before)!;
        _working = working.WithGraph(working.Graph);
        _scalars = scalars;
        FinalRoots = roots;
        Additions = additions;
        NewDefinitionOrigins = origins;
        OrderedRemovalIds = removals;
        SelectedTerminalRootIds = terminals;
        FinalSeverityChanged = finalSeverityChanged;
        AllowsWorsening = allowsWorsening;
        Fingerprint = fingerprint;
    }
    internal WoundMaterializationEnvelope Before => WoundAcceptedTurnData.CloneWound(_before)!;
    internal WoundFinalAssemblyScalars FinalScalars => _scalars with
    {
        Recovery = _scalars.Recovery with { DeteriorationPolicy = _scalars.Recovery.DeteriorationPolicy?.Clone() },
        Treatment = WoundAcceptedTurnData.CloneWound(_before)!.Treatment with
        { CompletedRouteIds = _scalars.Treatment.CompletedRouteIds.ToImmutableArray() }
    };
    internal WoundWorkingOwnedGraph FinalGraph => _working.WithGraph(_working.Graph).Graph;
    internal ImmutableArray<MortalWoundTreatmentSelectedRoot> FinalRoots { get; }
    internal ImmutableArray<MortalWoundTreatmentSelectedAddition> Additions { get; }
    internal ImmutableArray<MortalWoundTreatmentSelectedDefinitionOrigin> NewDefinitionOrigins { get; }
    internal ImmutableArray<string> OrderedRemovalIds { get; }
    internal ImmutableArray<string> SelectedTerminalRootIds { get; }
    internal bool FinalSeverityChanged { get; }
    internal bool AllowsWorsening { get; }
    internal string Fingerprint { get; }
    internal static MortalWoundTreatmentSelectedGraphCompilation Create(WoundMaterializationEnvelope before,
        MortalWoundTreatmentWorkingGraphProjection working, WoundFinalAssemblyScalars scalars,
        ImmutableArray<MortalWoundTreatmentSelectedRoot> roots,
        ImmutableArray<MortalWoundTreatmentSelectedAddition> additions,
        ImmutableArray<MortalWoundTreatmentSelectedDefinitionOrigin> origins,
        ImmutableArray<string> removals, ImmutableArray<string> terminals, bool finalSeverityChanged, bool allowsWorsening, string fingerprint) =>
        new(before, working, scalars, roots, additions, origins, removals, terminals, finalSeverityChanged, allowsWorsening, fingerprint);
}
internal sealed record MortalWoundTreatmentSelectedGraphCompilationResult(
    MortalWoundTreatmentSelectedGraphCompilation? Compilation, IReadOnlyList<ValidationIssue> Issues);

internal sealed class MortalWoundTreatmentOutcomePreparation
{
    private readonly WoundMaterializationEnvelope _before;
    private readonly WoundMaterializationEnvelope _provisionalAfter;
    private readonly MortalWoundTreatmentSeverityReductionProjection? _severityReduction;
    private readonly ImmutableArray<IntentSeal> _intentSeals;
    private readonly SelectionSeal _selection;
    private readonly string _treatmentEventRef;
    private readonly MortalWoundTreatmentResolution? _selectedResolution;
    private readonly string? _selectedGraphFingerprint;

    internal MortalWoundTreatmentOutcomePreparation(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope provisionalAfter,
        string transitionId,
        MortalWoundTreatmentSeverityReductionProjection? severityReduction,
        string requestFingerprint,
        string resultFingerprint,
        string resolutionAuthorityFingerprint,
        IReadOnlyList<MortalWoundTreatmentOutcomeIntent> orderedIntents,
        string routeCompletion,
        string resultCategory,
        int? selectedOutcomeIndex,
        long currentGameMinute,
        string fingerprint,
        MortalWoundTreatmentResolution resolution)
    {
        _before = WoundAcceptedTurnData.CloneWound(before)!;
        _provisionalAfter = WoundAcceptedTurnData.CloneWound(provisionalAfter)!;
        TransitionId = transitionId;
        _severityReduction = CloneProjection(severityReduction);
        RequestFingerprint = requestFingerprint;
        ResultFingerprint = resultFingerprint;
        ResolutionAuthorityFingerprint = resolutionAuthorityFingerprint;
        _intentSeals = orderedIntents.Select((intent, index) => IntentSeal.From(intent,
            resolution.DeclaredResult[index], resolution.RequestAuthority)).ToImmutableArray();
        RouteCompletion = routeCompletion;
        ResultCategory = resultCategory;
        SelectedOutcomeIndex = selectedOutcomeIndex;
        CurrentGameMinute = currentGameMinute;
        Fingerprint = fingerprint;
        _selection = SelectionSeal.From(resolution);
        _treatmentEventRef = resolution.Coordinates.EventRef;
        if (orderedIntents.Any(MortalWoundTreatmentOutcomePublicationPlanner.UsesSelectedGraph))
        {
            _selectedResolution = MortalWoundTreatmentOutcomePublicationPlanner.DetachSelectedResolution(resolution);
            var compiled = MortalWoundTreatmentOutcomePublicationPlanner.CompileSelectedGraph(_before,
                _selectedResolution.RequestAuthority, _selectedResolution, transitionId, currentGameMinute);
            _selectedGraphFingerprint = compiled.Compilation?.Fingerprint ??
                throw new InvalidOperationException("Selected complication graph cannot be reconstructed.");
        }
    }

    internal WoundMaterializationEnvelope Before =>
        WoundAcceptedTurnData.CloneWound(_before)!;
    internal WoundMaterializationEnvelope ProvisionalAfter =>
        WoundAcceptedTurnData.CloneWound(_provisionalAfter)!;
    internal string TransitionId { get; }
    internal MortalWoundTreatmentSeverityReductionProjection? SeverityReduction =>
        CloneProjection(_severityReduction);
    internal string Fingerprint { get; }

    private string RequestFingerprint { get; }
    private string ResultFingerprint { get; }
    private string ResolutionAuthorityFingerprint { get; }
    private string RouteCompletion { get; }
    private string ResultCategory { get; }
    private int? SelectedOutcomeIndex { get; }
    private long CurrentGameMinute { get; }

    private MortalWoundTreatmentOutcomePreparation(MortalWoundTreatmentOutcomePreparation source)
    {
        _before = WoundAcceptedTurnData.CloneWound(source._before)!;
        _provisionalAfter = WoundAcceptedTurnData.CloneWound(source._provisionalAfter)!;
        _severityReduction = CloneProjection(source._severityReduction);
        _intentSeals = source._intentSeals;
        _selection = source._selection;
        _treatmentEventRef = source._treatmentEventRef;
        TransitionId = source.TransitionId;
        RequestFingerprint = source.RequestFingerprint;
        ResultFingerprint = source.ResultFingerprint;
        ResolutionAuthorityFingerprint = source.ResolutionAuthorityFingerprint;
        RouteCompletion = source.RouteCompletion;
        ResultCategory = source.ResultCategory;
        SelectedOutcomeIndex = source.SelectedOutcomeIndex;
        CurrentGameMinute = source.CurrentGameMinute;
        Fingerprint = source.Fingerprint;
        _selectedResolution = source._selectedResolution is null ? null :
            MortalWoundTreatmentOutcomePublicationPlanner.DetachSelectedResolution(source._selectedResolution);
        _selectedGraphFingerprint = source._selectedGraphFingerprint;
    }

    internal MortalWoundTreatmentOutcomePreparation DetachedCopy() => new(this);

    internal string GetTreatmentEventRef() => _treatmentEventRef;

    internal WoundMaterializationEnvelope GetPreparedCarrierImage() =>
        _selectedResolution is null ? ProvisionalAfter : Before;

    internal bool TryRecomposeSelectedGraph(out MortalWoundTreatmentSelectedGraphCompilation? compilation)
    {
        compilation = null;
        if (_selectedResolution is null) return false;
        var result = MortalWoundTreatmentOutcomePublicationPlanner.CompileSelectedGraph(_before,
            _selectedResolution.RequestAuthority, _selectedResolution, TransitionId, CurrentGameMinute);
        if (result.Compilation is null || result.Issues.Count != 0 ||
            result.Compilation.Fingerprint != _selectedGraphFingerprint) return false;
        compilation = result.Compilation;
        return true;
    }

    internal string[] GetOrderedRemovalIds() => _intentSeals
        .Where(static seal => seal.Kind == "remove_complication")
        .Select(static seal => seal.ComplicationId!)
        .ToArray();

    internal bool AgreesWith(MortalWoundTreatmentResolution resolution)
    {
        if (!MortalWoundTreatmentOutcomePublicationPlanner.DetachedSelectionAgrees(
                resolution, CurrentGameMinute) ||
            _selection != SelectionSeal.From(resolution) ||
            !string.Equals(_treatmentEventRef, resolution.Coordinates.EventRef,
                StringComparison.Ordinal) ||
            !CanonicalWoundsEqual(_before, resolution.RequestAuthority.RouteSourceWound) ||
            !string.Equals(RequestFingerprint, resolution.RequestFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(ResultFingerprint, resolution.ResultFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(ResolutionAuthorityFingerprint,
                resolution.ResolutionAuthorityFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(RouteCompletion, resolution.RouteCompletion,
                StringComparison.Ordinal) ||
            !string.Equals(ResultCategory, resolution.ResultCategory,
                StringComparison.Ordinal) ||
            SelectedOutcomeIndex != resolution.SelectedOutcomeIndex ||
            _intentSeals.Length != resolution.OutcomeIntents.Count)
        {
            return false;
        }

        for (var index = 0; index < _intentSeals.Length; index++)
        {
            if (!_intentSeals[index].AgreesWith(resolution.OutcomeIntents[index], resolution.DeclaredResult[index], resolution.RequestAuthority))
                return false;
        }

        try
        {
            if (_selectedResolution is not null)
            {
                if (!TryRecomposeSelectedGraph(out var selected) ||
                    !MortalWoundTreatmentOutcomePublicationPlanner.OrderedIntentsAgree(
                        resolution.RequestAuthority, resolution.DeclaredResult, _selectedResolution.OutcomeIntents, resolution.OutcomeIntents)) return false;
                var expected = MortalWoundTreatmentOutcomePublicationPlanner.CompileSelectedGraph(_before,
                    resolution.RequestAuthority, resolution, TransitionId, CurrentGameMinute);
                return expected.Compilation is not null && expected.Issues.Count == 0 &&
                    expected.Compilation.Fingerprint == selected!.Fingerprint &&
                    CanonicalWoundsEqual(_before, _provisionalAfter) && _severityReduction is null &&
                    Fingerprint == MortalWoundTreatmentOutcomePublicationPlanner.ComputePreparationFingerprint(
                        _before, _before, resolution, TransitionId, CurrentGameMinute, selected.Fingerprint);
            }
            var reductions = _intentSeals
                .Where(static seal => seal.Kind == "reduce_severity")
                .ToArray();
            if (reductions.Any(static seal =>
                    seal.ReductionSteps is not (>= 1 and <= 2)))
            {
                return false;
            }
            var aggregateReductionSteps = reductions.Aggregate(
                0,
                static (sum, seal) => checked(sum + seal.ReductionSteps!.Value));
            if ((reductions.Length == 0) != (_severityReduction is null) ||
                aggregateReductionSteps is < 0 or > 2)
            {
                return false;
            }

            {
                var expectedScalar =
                    MortalWoundTreatmentOutcomePublicationPlanner.CreateScalarShell(
                        _before,
                        resolution.RequestAuthority,
                        resolution,
                        TransitionId,
                        CurrentGameMinute,
                        resolution.OutcomeIntents);
                var expectedBeforeProjection = _severityReduction?.Before ??
                    _provisionalAfter;
                if (!CanonicalWoundsEqual(expectedScalar, expectedBeforeProjection))
                    return false;
            }

            if (_severityReduction is not null)
            {
                if (_severityReduction.Steps != aggregateReductionSteps ||
                    !CanonicalWoundsEqual(
                        _severityReduction.ProvisionalAfter,
                        _provisionalAfter))
                {
                    return false;
                }
                var recomposed = MortalWoundTreatmentSeverityReductionPlanner.Project(
                    _severityReduction.Before,
                    aggregateReductionSteps,
                    _severityReduction.ProvisionalAfter.Severity.LastChangeEventRef);
                if (!recomposed.IsValid || recomposed.Projection is null ||
                    !ProjectionsEqual(_severityReduction, recomposed.Projection))
                {
                    return false;
                }
            }

            return string.Equals(
                Fingerprint,
                MortalWoundTreatmentOutcomePublicationPlanner
                    .ComputePreparationFingerprint(
                        _before,
                        _provisionalAfter,
                        resolution,
                        TransitionId,
                        CurrentGameMinute,
                        _severityReduction?.Fingerprint),
                StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException or
                                           OverflowException)
        {
            return false;
        }
    }

    private static bool CanonicalWoundsEqual(
        WoundMaterializationEnvelope left,
        WoundMaterializationEnvelope right) => string.Equals(
        WoundMaterializationContract.SerializeCanonical(left),
        WoundMaterializationContract.SerializeCanonical(right),
        StringComparison.Ordinal);

    private static bool ProjectionsEqual(
        MortalWoundTreatmentSeverityReductionProjection left,
        MortalWoundTreatmentSeverityReductionProjection right)
    {
        if (left.Steps != right.Steps ||
            !string.Equals(left.Fingerprint, right.Fingerprint,
                StringComparison.Ordinal) ||
            !CanonicalWoundsEqual(left.Before, right.Before) ||
            !CanonicalWoundsEqual(left.ProvisionalAfter, right.ProvisionalAfter))
        {
            return false;
        }
        var leftRoots = left.Roots;
        var rightRoots = right.Roots;
        if (leftRoots.Count != rightRoots.Count)
            return false;
        for (var rootIndex = 0; rootIndex < leftRoots.Count; rootIndex++)
        {
            var leftRoot = leftRoots[rootIndex];
            var rightRoot = rightRoots[rootIndex];
            if (!string.Equals(leftRoot.PriorEffectId, rightRoot.PriorEffectId,
                    StringComparison.Ordinal) ||
                !string.Equals(leftRoot.DefinitionKey, rightRoot.DefinitionKey,
                    StringComparison.Ordinal) ||
                !string.Equals(leftRoot.OwnershipDomain.Kind,
                    rightRoot.OwnershipDomain.Kind, StringComparison.Ordinal) ||
                !string.Equals(leftRoot.OwnershipDomain.ComplicationId,
                    rightRoot.OwnershipDomain.ComplicationId,
                    StringComparison.Ordinal) ||
                leftRoot.Slots.Count != rightRoot.Slots.Count)
            {
                return false;
            }
            for (var slotIndex = 0; slotIndex < leftRoot.Slots.Count; slotIndex++)
            {
                if (leftRoot.Slots[slotIndex] != rightRoot.Slots[slotIndex])
                    return false;
            }
        }
        return true;
    }

    private static MortalWoundTreatmentSeverityReductionProjection? CloneProjection(
        MortalWoundTreatmentSeverityReductionProjection? projection) =>
        projection is null
            ? null
            : new MortalWoundTreatmentSeverityReductionProjection(
                projection.Before,
                projection.ProvisionalAfter,
                projection.Steps,
                projection.Roots,
                projection.Fingerprint);

    private sealed record IntentSeal(
        int OperationOrdinal,
        string Kind,
        string DeclaredOperationFingerprint,
        string IntentFingerprint,
        int? ReductionSteps,
        int? RecoveryPoints,
        string? ComplicationId,
        AdditionSeal? Addition,
        PolicySeal? Policy)
    {
        internal static IntentSeal From(MortalWoundTreatmentOutcomeIntent intent,
            MortalWoundTreatmentOperation operation, MortalWoundTreatmentAttemptRequest request) => new(
            intent.OperationOrdinal,
            intent.Kind,
            intent.DeclaredOperationFingerprint,
            intent.IntentFingerprint,
            (intent as MortalWoundReduceSeverityOutcomeIntent)?.Steps,
            (intent as MortalWoundAddRecoveryOutcomeIntent)?.Points,
            (intent as MortalWoundRemoveComplicationOutcomeIntent)?.ComplicationId,
            operation is MortalWoundAddComplicationOperation add ? AdditionSeal.From(request.RequestFingerprint, intent.OperationOrdinal, add) : null,
            operation is MortalWoundApplyDeteriorationOperation policy &&
                intent is MortalWoundApplyDeteriorationOutcomeIntent selected &&
                selected.TryGetPreparedDeterioration(request, policy, out var prepared) && prepared is not null
                    ? new PolicySeal(prepared) : null);

        internal bool AgreesWith(MortalWoundTreatmentOutcomeIntent intent, MortalWoundTreatmentOperation operation, MortalWoundTreatmentAttemptRequest request) =>
            OperationOrdinal == intent.OperationOrdinal &&
            string.Equals(Kind, intent.Kind, StringComparison.Ordinal) &&
            string.Equals(DeclaredOperationFingerprint,
                intent.DeclaredOperationFingerprint, StringComparison.Ordinal) &&
            string.Equals(IntentFingerprint, intent.IntentFingerprint,
                StringComparison.Ordinal) &&
            ReductionSteps ==
                (intent as MortalWoundReduceSeverityOutcomeIntent)?.Steps &&
            RecoveryPoints == (intent as MortalWoundAddRecoveryOutcomeIntent)?.Points &&
            string.Equals(ComplicationId,
                (intent as MortalWoundRemoveComplicationOutcomeIntent)?.ComplicationId,
                StringComparison.Ordinal) &&
            (Addition is null ? intent is not MortalWoundAddComplicationOutcomeIntent && operation is not MortalWoundAddComplicationOperation
                : Addition.AgreesWith(request.RequestFingerprint, OperationOrdinal, operation, intent)) &&
            (Policy is null ? intent is not MortalWoundApplyDeteriorationOutcomeIntent &&
                operation is not MortalWoundApplyDeteriorationOperation : Policy.AgreesWith(request, operation, intent));

    }

    private sealed record PolicySeal(MortalWoundTreatmentDeteriorationPreparation Prepared)
    {
        internal bool AgreesWith(MortalWoundTreatmentAttemptRequest request,
            MortalWoundTreatmentOperation operation, MortalWoundTreatmentOutcomeIntent intent)
        {
            if (operation is not MortalWoundApplyDeteriorationOperation policy ||
                intent is not MortalWoundApplyDeteriorationOutcomeIntent selected ||
                !selected.TryGetPreparedDeterioration(request, policy, out var incoming) ||
                incoming is null || !Prepared.AgreesWith(request, policy, selected)) return false;
            return incoming.Fingerprint == Prepared.Fingerprint;
        }
    }

    private sealed record AdditionSeal(MortalWoundComplicationProposalDraft Draft,
        MortalWoundTreatmentComplicationBindingPreparation Binding)
    {
        internal static AdditionSeal From(string requestFingerprint, int ordinal, MortalWoundAddComplicationOperation operation)
        {
            var draft = MortalWoundTreatmentOutcomePublicationPlanner.DetachComplicationDraft(operation.ComplicationDraft);
            return new(draft, MortalWoundTreatmentOutcomeIntentComposer.PrepareComplicationBindings(requestFingerprint, ordinal,
                draft, MortalWoundTreatmentOutcomeIntentComposer.DeclaredFingerprint(ordinal, new MortalWoundAddComplicationOperation(draft))));
        }
        internal bool AgreesWith(string requestFingerprint, int ordinal, MortalWoundTreatmentOperation operation,
            MortalWoundTreatmentOutcomeIntent intent)
        {
            if (operation is not MortalWoundAddComplicationOperation add || intent is not MortalWoundAddComplicationOutcomeIntent addition ||
                JsonSerializer.Serialize(Draft) != JsonSerializer.Serialize(add.ComplicationDraft)) return false;
            var expected = From(requestFingerprint, ordinal, add).Binding;
            return MortalWoundTreatmentOutcomePublicationPlanner.AdditionBindingAgrees(Binding, addition) &&
                MortalWoundTreatmentOutcomePublicationPlanner.AdditionBindingAgrees(expected, addition) &&
                Binding.Roots.SequenceEqual(expected.Roots);
        }
    }

    private sealed record SelectionSeal(
        string Mode, string AttemptDisposition, bool Interruption,
        string ConsumptionTrigger, string? CourseId, int? CourseMilestoneOrdinal,
        string? CourseDisposition, string RouteFingerprint, string? ModeEvidenceFingerprint)
    {
        internal static SelectionSeal From(MortalWoundTreatmentResolution resolution)
        {
            MortalWoundTreatmentResolution.TryRecomputeModeEvidenceFingerprint(resolution, out var evidence);
            return new(resolution.Mode, resolution.AttemptDisposition, resolution.Interruption,
                resolution.ConsumptionTrigger, resolution.CourseId, resolution.CourseMilestoneOrdinal,
                resolution.CourseDisposition, resolution.RouteFingerprint, evidence);
        }
    }
}

internal sealed record MortalWoundTreatmentOutcomePreparationResult(
    MortalWoundTreatmentOutcomePreparation? Preparation,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Preparation is not null && Issues.Count == 0;
}

internal sealed record MortalWoundTreatmentOutcomePublicationResult(
    WoundMaterializationEnvelope? After,
    WoundDeclaredTransitionOutcome? DeclaredOutcome,
    string? TransitionId,
    string? Fingerprint,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid =>
        After is not null &&
        DeclaredOutcome is not null &&
        TransitionId is not null &&
        Fingerprint is not null &&
        Issues.Count == 0;
}

internal static partial class MortalWoundTreatmentOutcomePublicationPlanner
{
    private const string IssuePath = "treatmentPublication.outcome";
    private const string PreparationDomain =
        "book_of_eternity.mortal_wound_treatment.outcome_preparation";
    private const string PublicationDomain =
        "book_of_eternity.mortal_wound_treatment.outcome_publication";

    internal static bool UsesSelectedGraph(MortalWoundTreatmentOutcomeIntent intent) =>
        intent is MortalWoundAddComplicationOutcomeIntent or MortalWoundApplyDeteriorationOutcomeIntent;

    internal static bool RequiresEffectBatch(MortalWoundTreatmentOutcomePreparation preparation) =>
        preparation.SeverityReduction is not null || preparation.GetOrderedRemovalIds().Length != 0 ||
        preparation.TryRecomposeSelectedGraph(out _);

    internal static MortalWoundTreatmentResolution DetachSelectedResolution(MortalWoundTreatmentResolution source)
    {
        var declared = source.DeclaredResult.Select(operation => operation is MortalWoundAddComplicationOperation add
            ? new MortalWoundAddComplicationOperation(DetachComplicationDraft(add.ComplicationDraft)) : operation).ToImmutableArray();
        var intents = source.OutcomeIntents.Select((intent, ordinal) => intent is MortalWoundAddComplicationOutcomeIntent add
            ? MortalWoundAddComplicationOutcomeIntent.Create(add.OperationOrdinal, add.DeclaredOperationFingerprint,
                add.IntentFingerprint, add.ComplicationRef, add.ComplicationId,
                add.DefinitionReferenceBindings.Select(row => MortalWoundTreatmentReferenceBinding.Create(row.LocalRef, row.NamespacedRef)),
                add.ApplicationReferenceBindings.Select(row => MortalWoundTreatmentReferenceBinding.Create(row.LocalRef, row.NamespacedRef)),
                add.PreparationFingerprint) : intent is MortalWoundApplyDeteriorationOutcomeIntent policy
                    ? source.DeclaredResult[ordinal] is MortalWoundApplyDeteriorationOperation operation &&
                        policy.TryGetPreparedDeterioration(source.RequestAuthority, operation, out _)
                        ? policy.DetachedCopy() : throw new InvalidOperationException("Invalid selected policy packet.")
                    : intent).ToImmutableArray();
        return MortalWoundTreatmentResolution.Create(source.Mode, source.Coordinates, source.AttemptDisposition,
            source.ResultCategory, source.SelectedOutcomeIndex, source.Interruption, declared, intents,
            source.CriticalReactionIntent, source.ConsumptionTrigger, source.CourseId, source.CourseMilestoneOrdinal,
            source.CourseDisposition, source.RequestAuthority, source.ModeEvidence, source.RouteFingerprint, source.RouteCompletion);
    }

    internal static MortalWoundComplicationProposalDraft DetachComplicationDraft(MortalWoundComplicationProposalDraft draft) => draft with
    {
        Complication = draft.Complication with { },
        ConsequenceDefinitions = draft.ConsequenceDefinitions.Select(row => row with
        {
            Definition = row.Definition.Clone(),
            Root = row.Root is null ? null : row.Root with { Slots = row.Root.Slots.Select(slot => slot with { }).ToImmutableArray() }
        }).ToImmutableArray()
    };

    internal static MortalWoundTreatmentSelectedGraphCompilationResult CompileSelectedGraph(
        WoundMaterializationEnvelope before, MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResolution resolution, string transitionId, long currentGameMinute)
    {
        try
        {
            if (!DetachedSelectionAgrees(resolution, currentGameMinute) ||
                request.RequestFingerprint != resolution.RequestFingerprint ||
                transitionId != CreateTransitionId(resolution) ||
                WoundMaterializationContract.SerializeCanonical(before) !=
                WoundMaterializationContract.SerializeCanonical(request.RouteSourceWound) ||
                resolution.DeclaredResult.Count != resolution.OutcomeIntents.Count)
                throw new InvalidOperationException("Selected graph authority mismatch.");
            var additions = ImmutableArray.CreateBuilder<MortalWoundTreatmentSelectedAddition>();
            var byReference = new Dictionary<WoundWorkingReference, MortalWoundTreatmentComplicationBindingPreparation>();
            var policies = new Dictionary<int, MortalWoundTreatmentDeteriorationPreparation>();
            var origins = ImmutableArray.CreateBuilder<MortalWoundTreatmentSelectedDefinitionOrigin>();
            for (var i = 0; i < resolution.DeclaredResult.Count; i++)
            {
                var operation = resolution.DeclaredResult[i];
                var intent = resolution.OutcomeIntents[i];
                var declared = MortalWoundTreatmentOutcomeIntentComposer.DeclaredFingerprint(i, operation);
                if (intent.OperationOrdinal != i || intent.Kind != operation.Kind || intent.DeclaredOperationFingerprint != declared)
                    throw new InvalidOperationException("Selected declared operation changed.");
                MortalWoundComplicationProposalDraft? draft;
                MortalWoundTreatmentComplicationBindingPreparation? binding;
                WoundWorkingReferenceOrigin origin;
                string componentsPrefix;
                if (operation is MortalWoundAddComplicationOperation add)
                {
                    draft = add.ComplicationDraft;
                    binding = MortalWoundTreatmentOutcomeIntentComposer.PrepareComplicationBindings(
                        request.RequestFingerprint, i, draft, declared);
                    if (intent is not MortalWoundAddComplicationOutcomeIntent addition || !AdditionBindingAgrees(binding, addition))
                        throw new InvalidOperationException("Selected T067 binding changed.");
                    origin = WoundWorkingReferenceOrigin.DirectAddition;
                    componentsPrefix = $"treatmentPublication.outcome.declaredResult[{i}].complicationDraft";
                }
                else if (operation is MortalWoundApplyDeteriorationOperation policy)
                {
                    if (intent is not MortalWoundApplyDeteriorationOutcomeIntent selected ||
                        !selected.TryGetPreparedDeterioration(request, policy, out var prepared) || prepared is null ||
                        prepared.Policy.ResultKind is not (MortalWoundDeteriorationResultKind.IncreaseSeverity or
                            MortalWoundDeteriorationResultKind.AddComplication))
                        throw new InvalidOperationException("Selected private policy preparation changed.");
                    policies.Add(i, prepared);
                    draft = prepared.Draft;
                    binding = prepared.ComplicationBinding;
                    if (prepared.Policy.ResultKind == MortalWoundDeteriorationResultKind.IncreaseSeverity) continue;
                    origin = WoundWorkingReferenceOrigin.PolicyAddition;
                    componentsPrefix = prepared.WoundSourcePath + ".recovery.deteriorationPolicy.result.complicationDraft";
                }
                else continue;
                if (draft is null || binding is null)
                    throw new InvalidOperationException("Missing selected complication packet.");
                var reference = new WoundWorkingReference(origin, new(0, i), draft.Complication.ComplicationRef);
                additions.Add(new(reference, binding));
                byReference.Add(reference, binding);
                for (var j = 0; j < draft.ConsequenceDefinitions.Length; j++)
                    origins.Add(new(i, j, draft.ConsequenceDefinitions[j].Definition.GetProperty("definitionKey").GetString()!,
                        binding.DefinitionReferenceBindings[j].NamespacedRef,
                        componentsPrefix + $".consequenceDefinitions[{j}].definition.components"));
            }
            if ((additions.Count == 0 && policies.Count == 0) ||
                !ExactAndConfusableUnique(additions.Select(row => row.Binding.ComplicationId)) ||
                !ExactAndConfusableUnique(additions.SelectMany(row => row.Binding.Roots).Select(row => row.OperationKey)))
                throw new InvalidOperationException("Missing or colliding selected additions.");
            var metadata = CreatePublicationMetadata(before, request, resolution, transitionId);
            var simulation = MortalWoundTreatmentWorkingWoundSimulator.SimulateGraph(before,
                new[] { resolution.DeclaredResult.ToImmutableArray() },
                (working, address, operation) =>
                {
                    if (operation is MortalWoundAddComplicationOperation add)
                        return working.TryAppendComplication(add.ComplicationDraft,
                            WoundWorkingReferenceOrigin.DirectAddition, address, out var after)
                            ? new(true, false, after) : new(false, false, null);
                    return address.ResultIndex == 0 && operation is MortalWoundApplyDeteriorationOperation &&
                        policies.TryGetValue(address.OperationIndex, out var prepared)
                            ? MortalWoundTreatmentPolicyGraphProjection.Project(working, address, prepared.Policy.ResultKind, prepared.Draft)
                            : new(false, false, null);
                });
            if (!simulation.IsApplicable || simulation.WorkingGraph is not { } working)
                throw new InvalidOperationException("Selected graph is not applicable.");
            var scalars = DecorateSelectedGraphScalars(working, metadata, resolution, currentGameMinute);
            working = working.WithScalars(new(scalars.Severity, scalars.Care, scalars.Recovery, scalars.SlotBudget));
            var validation = working.ValidateGraph(IssuePath + ".selectedGraph");
            if (!validation.IsEmpty) return new(null, validation);
            string ComplicationId(WoundWorkingReference reference) => reference.Origin == WoundWorkingReferenceOrigin.Existing
                ? reference.Value : byReference.TryGetValue(reference, out var binding)
                    ? binding.ComplicationId : throw new InvalidOperationException("Foreign complication coordinate.");
            var roots = working.Graph.Roots.Select(root =>
            {
                var owners = working.Graph.Complications.Where(row => row.OwnedRoots.Contains(root.Reference)).ToArray();
                var domain = owners.Length == 0 ? WoundRootOwnershipDomain.BaseWound : owners.Length == 1
                    ? WoundRootOwnershipDomain.ForComplication(ComplicationId(owners[0].Reference))
                    : throw new InvalidOperationException("Ambiguous root owner.");
                MortalWoundTreatmentComplicationRootBinding? binding = null;
                if (root.Reference.Origin != WoundWorkingReferenceOrigin.Existing)
                {
                    var addition = additions.Single(row => row.Reference.Origin == root.Reference.Origin &&
                        row.Reference.Address == root.Reference.Address);
                    binding = addition.Binding.Roots.Single(row => row.LocalDefinitionRef == root.Reference.Value);
                }
                return new MortalWoundTreatmentSelectedRoot(root.Reference, root.DefinitionKey, domain,
                    working.Graph.Entries.Where(entry => entry.Root == root.Reference).Select(entry =>
                        new WoundEffectSlotAgreement(entry.Slot, entry.ProfileKey, entry.ReadableSummary)).ToImmutableArray(),
                    binding is null ? root.Reference.Value : null, binding);
            }).ToImmutableArray();
            var finalSeverityChanged = working.Scalars.Severity.Rank != before.Severity.Rank;
            var allowsWorsening = working.Scalars.Severity.Rank > before.Severity.Rank &&
                policies.Values.Any(prepared => prepared.Policy.ResultKind == MortalWoundDeteriorationResultKind.IncreaseSeverity);
            var removals = resolution.DeclaredResult.OfType<MortalWoundRemoveComplicationOperation>()
                .Select(row => row.ComplicationId).ToImmutableArray();
            var terminals = (finalSeverityChanged ? before.Consequences.OwnedEffectSources.RootBindings.Select(row => row.EffectId)
                : removals.SelectMany(id => before.Complications.Single(row => row.ComplicationId == id).OwnedEffectIds)).ToImmutableArray();
            var fields = new List<string?> { "book_of_eternity.mortal_wound_treatment.selected_graph", "1",
                WoundMaterializationContract.SerializeCanonical(before), request.RequestFingerprint, resolution.ResultFingerprint,
                transitionId, JsonSerializer.Serialize(scalars), JsonSerializer.Serialize(working.Graph), JsonSerializer.Serialize(roots),
                JsonSerializer.Serialize(origins), JsonSerializer.Serialize(removals), JsonSerializer.Serialize(terminals), finalSeverityChanged.ToString(), allowsWorsening.ToString() };
            foreach (var policy in policies.OrderBy(row => row.Key))
            {
                fields.Add(policy.Key.ToString(CultureInfo.InvariantCulture));
                fields.Add(policy.Value.Fingerprint);
            }
            foreach (var addition in additions)
            {
                fields.Add(JsonSerializer.Serialize(addition.Reference));
                var binding = addition.Binding;
                fields.Add(binding.ComplicationRef); fields.Add(binding.ComplicationId); fields.Add(binding.PreparationFingerprint);
                fields.AddRange(binding.DefinitionReferenceBindings.SelectMany(row => new[] { row.LocalRef, row.NamespacedRef }));
                fields.AddRange(binding.ApplicationReferenceBindings.SelectMany(row => new[] { row.LocalRef, row.NamespacedRef }));
                fields.Add(JsonSerializer.Serialize(binding.Roots));
            }
            return new(MortalWoundTreatmentSelectedGraphCompilation.Create(before, working, scalars, roots,
                additions.ToImmutable(), origins.ToImmutable(), removals, terminals, finalSeverityChanged, allowsWorsening,
                WoundAcceptedTurnFingerprintWriter.Compute(fields)), Array.Empty<ValidationIssue>());
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or JsonException or OverflowException)
        {
            return new(null, new[] { Issue("mortal_wound_treatment_selected_graph_mismatch",
                "one exact authenticated selected complication graph", exception.Message) });
        }
    }

    internal static bool AdditionBindingAgrees(MortalWoundTreatmentComplicationBindingPreparation binding,
        MortalWoundAddComplicationOutcomeIntent intent) =>
        binding.ComplicationRef == intent.ComplicationRef && binding.ComplicationId == intent.ComplicationId &&
        binding.PreparationFingerprint == intent.PreparationFingerprint &&
        BindingsEqual(binding.DefinitionReferenceBindings, intent.DefinitionReferenceBindings) &&
        BindingsEqual(binding.ApplicationReferenceBindings, intent.ApplicationReferenceBindings);

    private static bool BindingsEqual(IReadOnlyList<MortalWoundTreatmentReferenceBinding> left,
        IReadOnlyList<MortalWoundTreatmentReferenceBinding> right) => left.Count == right.Count &&
        left.Zip(right).All(pair => pair.First.LocalRef == pair.Second.LocalRef && pair.First.NamespacedRef == pair.Second.NamespacedRef);

    internal static MortalWoundTreatmentOutcomePublicationResult Compose(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResolution resolution,
        long currentGameMinute)
    {
        var prepared = Prepare(acceptedState, request, resolution, currentGameMinute);
        return prepared.IsValid
            ? Finalize(
                prepared.Preparation!,
                resolution,
                null,
                ImmutableDictionary<string, EffectAcceptedApplicationResult>.Empty)
            : Invalid(prepared.Issues);
    }

    internal static MortalWoundTreatmentOutcomePreparationResult Prepare(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResolution resolution,
        long currentGameMinute)
    {
        ArgumentNullException.ThrowIfNull(acceptedState);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(resolution);

        var issues = ValidateCommon(
            acceptedState,
            request,
            resolution,
            currentGameMinute,
            out var orderedIntents);
        if (issues.Count != 0 || orderedIntents is null)
            return InvalidPreparation(issues);

        try
        {
            var transitionId = CreateTransitionId(resolution);
            var before = request.RouteSourceWound;
            if (orderedIntents.Any(MortalWoundTreatmentOutcomePublicationPlanner.UsesSelectedGraph))
            {
                var selected = CompileSelectedGraph(before, request, resolution, transitionId, currentGameMinute);
                if (selected.Compilation is null) return InvalidPreparation(selected.Issues);
                return new(new MortalWoundTreatmentOutcomePreparation(before, before, transitionId, null,
                    resolution.RequestFingerprint, resolution.ResultFingerprint, resolution.ResolutionAuthorityFingerprint,
                    orderedIntents, resolution.RouteCompletion, resolution.ResultCategory, resolution.SelectedOutcomeIndex,
                    currentGameMinute, ComputePreparationFingerprint(before, before, resolution, transitionId,
                        currentGameMinute, selected.Compilation.Fingerprint), resolution), Array.Empty<ValidationIssue>());
            }
            var scalarAfter = CreateScalarShell(
                before,
                request,
                resolution,
                transitionId,
                currentGameMinute,
                orderedIntents);

            var aggregateReductionSteps = 0;
            foreach (var intent in orderedIntents)
            {
                switch (intent)
                {
                    case MortalWoundNoImprovementOutcomeIntent:
                        break;
                    case MortalWoundStabilizeOutcomeIntent:
                        break;
                    case MortalWoundAddRecoveryOutcomeIntent:
                        break;
                    case MortalWoundRemoveComplicationOutcomeIntent:
                        break;
                    case MortalWoundReduceSeverityOutcomeIntent reduction:
                        aggregateReductionSteps = checked(
                            aggregateReductionSteps + reduction.Steps);
                        break;
                    default:
                        throw new InvalidOperationException(
                            "Validated treatment intent is not registered.");
                }
            }

            MortalWoundTreatmentSeverityReductionProjection? severityReduction = null;
            WoundMaterializationEnvelope provisionalAfter;
            if (aggregateReductionSteps == 0)
            {
                var parsed = WoundMaterializationContract.Parse(
                    WoundMaterializationContract.SerializeCanonical(scalarAfter),
                    IssuePath + ".provisionalAfter");
                if (!parsed.IsValid || parsed.Wound is null)
                    return InvalidPreparation(parsed.Issues);
                provisionalAfter = parsed.Wound;
            }
            else
            {
                var projected = MortalWoundTreatmentSeverityReductionPlanner.Project(
                    scalarAfter,
                    aggregateReductionSteps,
                    request.Coordinates.EventRef);
                if (!projected.IsValid || projected.Projection is null)
                    return InvalidPreparation(projected.Issues);
                severityReduction = projected.Projection;
                provisionalAfter = severityReduction.ProvisionalAfter;
            }

            var fingerprint = ComputePreparationFingerprint(
                before,
                provisionalAfter,
                resolution,
                transitionId,
                currentGameMinute,
                severityReduction?.Fingerprint);
            return new MortalWoundTreatmentOutcomePreparationResult(
                new MortalWoundTreatmentOutcomePreparation(
                    before,
                    provisionalAfter,
                    transitionId,
                    severityReduction,
                    resolution.RequestFingerprint,
                    resolution.ResultFingerprint,
                    resolution.ResolutionAuthorityFingerprint,
                    orderedIntents,
                    resolution.RouteCompletion,
                    resolution.ResultCategory,
                    resolution.SelectedOutcomeIndex,
                    currentGameMinute,
                    fingerprint,
                    resolution),
                Array.Empty<ValidationIssue>());
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException or
                                           OverflowException)
        {
            return InvalidPreparation(new[]
            {
                Issue(
                    "mortal_wound_treatment_publication_slice_unsupported",
                    "one canonical ordered treatment outcome preparation",
                    exception.GetType().Name)
            });
        }
    }

    internal static MortalWoundTreatmentOutcomePublicationResult Finalize(
        MortalWoundTreatmentOutcomePreparation preparation,
        MortalWoundTreatmentResolution resolution,
        WoundEffectOperationBatch? rematerializationBatch,
        IReadOnlyDictionary<string, EffectAcceptedApplicationResult> applicationByRef)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(applicationByRef);

        if (!preparation.AgreesWith(resolution))
        {
            return Invalid(new[]
            {
                Issue(
                    "mortal_wound_treatment_outcome_preparation_mismatch",
                    "the exact sealed ordered outcome preparation and resolution",
                    "changed preparation or resolution")
            });
        }
        var hasReduction = resolution.OutcomeIntents.Any(
            static intent => intent is MortalWoundReduceSeverityOutcomeIntent);
        WoundMaterializationEnvelope after;
        if (preparation.TryRecomposeSelectedGraph(out var selectedGraph))
        {
            if (rematerializationBatch is null || !SelectedGraphEffectHandoffAgrees(preparation, selectedGraph!,
                    rematerializationBatch, applicationByRef))
                return Invalid(new[] { Issue("mortal_wound_treatment_outcome_effect_handoff_mismatch",
                    "one authenticated treatment graph batch and exact application map (including empty)",
                    $"batch={(rematerializationBatch is null ? "null" : "present")};applications={applicationByRef.Count}") });
            var finalized = WoundAcceptedTurnPlannerCore.BuildFinalTreatmentWound(selectedGraph!, rematerializationBatch, applicationByRef);
            if (finalized.Wound is null || finalized.Issues.Count != 0) return Invalid(finalized.Issues);
            after = finalized.Wound;
        }
        else if (hasReduction)
        {
            if (rematerializationBatch is null ||
                preparation.SeverityReduction is null ||
                !ReductionEffectHandoffAgrees(
                    preparation,
                    rematerializationBatch,
                    applicationByRef))
            {
                return Invalid(new[]
                {
                    Issue(
                        "mortal_wound_treatment_outcome_effect_handoff_mismatch",
                        "one exact authenticated rematerialization batch and its ordered accepted application results",
                        $"batch={(rematerializationBatch is null ? "null" : "present")};applications={applicationByRef.Count}")
                });
            }

            var finalized = WoundAcceptedTurnPlannerCore.BuildFinalWound(
                preparation.ProvisionalAfter,
                rematerializationBatch,
                applicationByRef);
            if (finalized.Wound is null || finalized.Issues.Count != 0)
            {
                return Invalid(new[]
                {
                    Issue(
                        "mortal_wound_treatment_outcome_final_wound_mismatch",
                        "one canonical lower-severity wound reconstructed from the authenticated effect result",
                        string.Join(",", finalized.Issues.Select(
                            static issue => issue.Code)))
                });
            }
            after = finalized.Wound;
        }
        else
        {
            var requiresRemovalBatch = RequiresEffectBatch(preparation);
            if (applicationByRef.Count != 0 ||
                (requiresRemovalBatch
                    ? rematerializationBatch is null || !RemovalEffectHandoffAgrees(preparation, rematerializationBatch)
                    : rematerializationBatch is not null))
            {
                return Invalid(new[]
                {
                    Issue(
                        "mortal_wound_treatment_outcome_effect_handoff_mismatch",
                        requiresRemovalBatch ? "one authenticated treatment graph batch and exact application map (including empty)"
                            : "null rematerialization batch and empty application map for an unchanged-severity result",
                        $"batch={(rematerializationBatch is null ? "null" : "present")};applications={applicationByRef.Count}")
                });
            }

            var provisionalAfter = preparation.ProvisionalAfter;
            var parsed = WoundMaterializationContract.Parse(
                WoundMaterializationContract.SerializeCanonical(provisionalAfter),
                IssuePath + ".afterWound");
            if (!parsed.IsValid || parsed.Wound is null ||
                !string.Equals(
                    WoundMaterializationContract.SerializeCanonical(parsed.Wound),
                    WoundMaterializationContract.SerializeCanonical(provisionalAfter),
                    StringComparison.Ordinal))
            {
                return Invalid(parsed.Issues.Count == 0
                    ? new[]
                    {
                        Issue(
                            "mortal_wound_treatment_outcome_final_wound_mismatch",
                            "canonical equality with the sealed provisional after-image",
                            "changed final wound")
                    }
                    : parsed.Issues);
            }
            after = parsed.Wound;
        }

        var declaredOutcome = CreateDeclaredOutcome(after, selectedGraph);
        var fingerprint = ComputePublicationFingerprint(
            preparation,
            after,
            resolution,
            declaredOutcome);
        return new MortalWoundTreatmentOutcomePublicationResult(
            after,
            declaredOutcome,
            preparation.TransitionId,
            fingerprint,
            Array.Empty<ValidationIssue>());
    }

    private static bool RemovalEffectHandoffAgrees(
        MortalWoundTreatmentOutcomePreparation preparation,
        WoundEffectOperationBatch batch)
    {
        var after = preparation.ProvisionalAfter;
        var source = batch.SourceExport;
        var definitions = after.Consequences.OwnedEffectSources.Definitions;
        return batch.RootApplications.Count == 0 &&
            string.Equals(batch.PreparedWoundId, after.WoundId, StringComparison.Ordinal) &&
            source.Kind == "wound" && source.State == "active" && !source.Materializable &&
            source.Owner == after.Owner && source.Realm == after.Owner.Realm &&
            string.Equals(source.SourceId, after.WoundId, StringComparison.Ordinal) &&
            string.Equals(source.SourceRef, batch.LocalWoundRef, StringComparison.Ordinal) &&
            string.Equals(source.CausalEventRef, preparation.GetTreatmentEventRef(), StringComparison.Ordinal) &&
            string.Equals(batch.SourceExportFingerprint,
                WoundAcceptedTurnFingerprints.ComputeSourceExport(batch), StringComparison.Ordinal) &&
            source.Definitions.Count == definitions.Count &&
            source.Definitions.Zip(definitions).All(static pair => string.Equals(
                WoundAcceptedTurnFingerprintWriter.CanonicalJson(pair.First.Definition),
                WoundAcceptedTurnFingerprintWriter.CanonicalJson(JsonNode.Parse(pair.Second.GetRawText())),
                StringComparison.Ordinal));
    }

    private static bool SelectedGraphEffectHandoffAgrees(MortalWoundTreatmentOutcomePreparation preparation,
        MortalWoundTreatmentSelectedGraphCompilation compilation, WoundEffectOperationBatch batch,
        IReadOnlyDictionary<string, EffectAcceptedApplicationResult> results)
    {
        // Full terminal/descendant closure needs the original effect carriers and index.
        // The existing rematerializer agreement authenticates it at the outer final
        // continuation boundary; this detached check owns selected graph/result shape.
        var roots = compilation.FinalRoots.Where(row => compilation.FinalSeverityChanged || row.AdditionBinding is not null).ToArray();
        var definitions = compilation.FinalGraph.Definitions;
        var expectedLineage = roots.Select((root, index) => new WoundRootLineageAuthorityRow(
            index < batch.RootApplications.Count ? batch.RootApplications[index].ApplicationRef : null,
            null, root.DefinitionKey, root.OwnershipDomain)).ToList();
        if (!compilation.FinalSeverityChanged)
            expectedLineage.AddRange(compilation.FinalRoots.Where(row => row.OriginalEffectId is not null)
                .OrderBy(row => row.OriginalEffectId, StringComparer.Ordinal).ThenBy(row => row.DefinitionKey, StringComparer.Ordinal)
                .Select(row => new WoundRootLineageAuthorityRow(null, row.OriginalEffectId, row.DefinitionKey, row.OwnershipDomain)));
        if (batch.RootApplications.Count != roots.Length || results.Count != roots.Length ||
            !batch.RootLineageAuthority.SequenceEqual(expectedLineage) ||
            batch.TransitionAuthority.TransitionKind != compilation.FinalScalars.LastTransition.Kind ||
            !results.Keys.SequenceEqual(batch.RootApplications.Select(row => row.ApplicationRef), StringComparer.Ordinal) ||
            batch.PreparedWoundId != compilation.Before.WoundId || batch.SourceExport.SourceId != compilation.Before.WoundId ||
            batch.SourceExport.Owner != compilation.Before.Owner || batch.SourceExport.Kind != "wound" ||
            batch.SourceExport.State != "active" || batch.SourceExport.Materializable ||
            batch.SourceExport.CausalEventRef != preparation.GetTreatmentEventRef() ||
            batch.SourceExportFingerprint != WoundAcceptedTurnFingerprints.ComputeSourceExport(batch) ||
            batch.SourceExport.Definitions.Count != definitions.Length ||
            !batch.SourceExport.Definitions.Zip(definitions).All(pair =>
                WoundAcceptedTurnFingerprintWriter.CanonicalJson(pair.First.Definition) ==
                WoundAcceptedTurnFingerprintWriter.CanonicalJson(JsonNode.Parse(pair.Second.Definition.GetRawText())))) return false;
        for (var i = 0; i < roots.Length; i++)
        {
            var root = roots[i];
            var application = batch.RootApplications[i];
            if (application.DefinitionKey != root.DefinitionKey || application.OwnershipDomain != root.OwnershipDomain ||
                application.PriorRootEffectId != root.OriginalEffectId ||
                !application.SlotBindings.SequenceEqual(root.Slots) ||
                (root.AdditionBinding is not null && (application.ApplicationRef != root.AdditionBinding.ApplicationRef ||
                    application.OperationKey != root.AdditionBinding.OperationKey)) ||
                !results.TryGetValue(application.ApplicationRef, out var result) || !ApplicationResultAgrees(application, result) ||
                !result.Materialization.SlotBindings.SequenceEqual(root.Slots)) return false;
        }
        return ExactAndConfusableUnique(results.Values.Select(row => row.EffectId)
            .Concat(compilation.Before.Consequences.OwnedEffectSources.RootBindings.Select(row => row.EffectId))) &&
            ExactAndConfusableUnique(results.Values.Select(row => row.CreateTransitionId));
    }

    private static bool ReductionEffectHandoffAgrees(
        MortalWoundTreatmentOutcomePreparation preparation,
        WoundEffectOperationBatch batch,
        IReadOnlyDictionary<string, EffectAcceptedApplicationResult> applicationByRef)
    {
        var projection = preparation.SeverityReduction!;
        var roots = batch.RootApplications;
        var results = new List<EffectAcceptedApplicationResult>(roots.Count);
        if (!string.Equals(
                batch.PreparedWoundId,
                preparation.ProvisionalAfter.WoundId,
                StringComparison.Ordinal) ||
            !string.Equals(
                batch.SourceExport.SourceId,
                preparation.Before.WoundId,
                StringComparison.Ordinal) ||
            !string.Equals(
                batch.SourceExportFingerprint,
                WoundAcceptedTurnFingerprints.ComputeSourceExport(batch),
                StringComparison.Ordinal) ||
            roots.Count != projection.Roots.Count ||
            applicationByRef.Count != roots.Count ||
            !applicationByRef.Keys.SequenceEqual(
                roots.Select(static root => root.ApplicationRef),
                StringComparer.Ordinal))
        {
            return false;
        }

        var exportedDefinitions = batch.SourceExport.Definitions;
        var sealedDefinitions = projection.Before.Consequences
            .OwnedEffectSources.Definitions;
        if (exportedDefinitions.Count != sealedDefinitions.Count)
            return false;
        for (var index = 0; index < exportedDefinitions.Count; index++)
        {
            if (!string.Equals(
                    WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                        exportedDefinitions[index].Definition),
                    WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                        JsonNode.Parse(sealedDefinitions[index].GetRawText())),
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        for (var index = 0; index < roots.Count; index++)
        {
            var root = roots[index];
            var projected = projection.Roots[index];
            if (!applicationByRef.TryGetValue(root.ApplicationRef, out var result) ||
                result is null ||
                !string.Equals(
                    root.PriorRootEffectId,
                    projected.PriorEffectId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    root.DefinitionKey,
                    projected.DefinitionKey,
                    StringComparison.Ordinal) ||
                root.OwnershipDomain != projected.OwnershipDomain ||
                !root.SlotBindings.SequenceEqual(projected.Slots) ||
                !ApplicationResultAgrees(root, result))
            {
                return false;
            }
            results.Add(result);
        }

        if (!ExactAndConfusableUnique(
                results.Select(static result => result.EffectId)) ||
            !ExactAndConfusableUnique(
                results.Select(static result => result.CreateTransitionId)))
        {
            return false;
        }
        var priorIds = preparation.Before.Consequences.OwnedEffectSources
            .RootBindings.Select(static binding => binding.EffectId).ToArray();
        var priorAliases = priorIds.Select(
                MortalLocationIdentityState.BuildConfusableKey)
            .ToHashSet(StringComparer.Ordinal);
        if (results.Any(result => priorIds.Contains(
                                     result.EffectId,
                                     StringComparer.Ordinal) ||
                                 priorAliases.Contains(
                                     MortalLocationIdentityState.BuildConfusableKey(
                                         result.EffectId))))
        {
            return false;
        }

        var nextSlot = 1;
        foreach (var result in results.OrderBy(
                     static result => result.EffectId,
                     StringComparer.Ordinal))
        {
            foreach (var slot in result.Materialization.SlotBindings)
            {
                if (slot.Slot != nextSlot++)
                    return false;
            }
        }
        return true;
    }

    private static bool ApplicationResultAgrees(
        WoundRootEffectApplication root,
        EffectAcceptedApplicationResult result)
    {
        if (result.Materialization is null)
            return false;
        var slots = result.Materialization.SlotBindings;
        var expectedSlots = root.SlotBindings;
        if (slots is null ||
            !string.Equals(
                result.ApplicationRef,
                root.ApplicationRef,
                StringComparison.Ordinal) ||
            !string.Equals(
                result.Disposition,
                "created_new_identity",
                StringComparison.Ordinal) ||
            !ResourceMaterializationContract.IsExactIdentifier(result.EffectId) ||
            !ResourceMaterializationContract.IsExactIdentifier(
                result.CreateTransitionId) ||
            !string.Equals(
                result.CreatedEventRef,
                WoundEffectOperationEventRef.Create(
                    root.CausalEventRef,
                    root.MechanicsOrdinal,
                    root.OperationOrdinal,
                    root.OperationKind),
                StringComparison.Ordinal) ||
            !string.Equals(
                result.CausalEventRef,
                root.CausalEventRef,
                StringComparison.Ordinal) ||
            result.SourceKey != root.ExpectedSourceKey ||
            result.TargetKey != root.ExpectedTargetKey ||
            result.CarrierCoordinate != root.ExpectedCarrierCoordinate ||
            result.Materialization.ComponentCount != root.ExpectedComponentCount ||
            !string.Equals(
                result.Materialization.MaterializationFingerprint,
                root.ExpectedMaterializationFingerprint,
                StringComparison.Ordinal) ||
            slots.Count != expectedSlots.Count)
        {
            return false;
        }
        for (var index = 0; index < slots.Count; index++)
        {
            if (!string.Equals(
                    slots[index].ProfileKey,
                    expectedSlots[index].ProfileKey,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    slots[index].ReadableSummary,
                    expectedSlots[index].ReadableSummary,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private static bool ExactAndConfusableUnique(IEnumerable<string> values)
    {
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var confusable = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (!ResourceMaterializationContract.IsExactIdentifier(value) ||
                !exact.Add(value) ||
                !confusable.Add(
                    MortalLocationIdentityState.BuildConfusableKey(value)))
            {
                return false;
            }
        }
        return true;
    }

    internal static string ComputePreparationFingerprint(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope provisionalAfter,
        MortalWoundTreatmentResolution resolution,
        string transitionId,
        long currentGameMinute,
        string? severityReductionFingerprint)
    {
        MortalWoundTreatmentResolution.TryRecomputeModeEvidenceFingerprint(resolution, out var modeEvidenceFingerprint);
        var fields = new List<string?>
        {
            PreparationDomain,
            "4",
            WoundMaterializationContract.SerializeCanonical(before),
            WoundMaterializationContract.SerializeCanonical(provisionalAfter),
            resolution.RequestFingerprint,
            resolution.ResultFingerprint,
            resolution.ResolutionAuthorityFingerprint,
            resolution.Coordinates.EventRef,
            resolution.Mode,
            resolution.AttemptDisposition,
            resolution.Interruption.ToString(CultureInfo.InvariantCulture),
            resolution.ConsumptionTrigger,
            resolution.CourseId,
            resolution.CourseMilestoneOrdinal?.ToString(CultureInfo.InvariantCulture),
            resolution.CourseDisposition,
            resolution.RouteFingerprint,
            modeEvidenceFingerprint,
            transitionId,
            resolution.RouteCompletion,
            resolution.ResultCategory,
            resolution.SelectedOutcomeIndex?.ToString(CultureInfo.InvariantCulture),
            currentGameMinute.ToString(CultureInfo.InvariantCulture),
            severityReductionFingerprint,
            resolution.OutcomeIntents.Count.ToString(CultureInfo.InvariantCulture)
        };
        for (var index = 0; index < resolution.OutcomeIntents.Count; index++)
        {
            var intent = resolution.OutcomeIntents[index];
            fields.Add(index.ToString(CultureInfo.InvariantCulture));
            fields.Add(intent.OperationOrdinal.ToString(CultureInfo.InvariantCulture));
            fields.Add(intent.Kind);
            fields.Add(intent.DeclaredOperationFingerprint);
            fields.Add(intent.IntentFingerprint);
            fields.Add((intent as MortalWoundReduceSeverityOutcomeIntent)?.Steps
                .ToString(CultureInfo.InvariantCulture));
            fields.Add((intent as MortalWoundAddRecoveryOutcomeIntent)?.Points
                .ToString(CultureInfo.InvariantCulture));
            fields.Add((intent as MortalWoundRemoveComplicationOutcomeIntent)?.ComplicationId);
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    internal static string ComputePublicationFingerprint(
        MortalWoundTreatmentOutcomePreparation preparation,
        WoundMaterializationEnvelope acceptedAfter,
        MortalWoundTreatmentResolution resolution,
        WoundDeclaredTransitionOutcome declaredOutcome)
    {
        var fields = new List<string?>
        {
            PublicationDomain,
            "2",
            preparation.Fingerprint,
            WoundMaterializationContract.SerializeCanonical(acceptedAfter),
            resolution.RequestFingerprint,
            resolution.ResultFingerprint,
            resolution.ResolutionAuthorityFingerprint,
            preparation.TransitionId,
            declaredOutcome.ResultingSeverityRank.ToString(CultureInfo.InvariantCulture),
            declaredOutcome.ResultingCareState,
            declaredOutcome.ResultingRecoveryProgress.ToString(CultureInfo.InvariantCulture),
            declaredOutcome.Heals.ToString(),
            declaredOutcome.TerminalAttempt.ToString(),
            declaredOutcome.AllowsWorsening.ToString()
        };
        Append(fields, declaredOutcome.ResultingComplicationIds);
        Append(fields, declaredOutcome.ResultingEffectIds);
        Append(fields, declaredOutcome.ResultingRecoveryBlockers);
        Append(fields, declaredOutcome.ResultingCompletedRouteIds);
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static IReadOnlyList<ValidationIssue> ValidateCommon(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResolution resolution,
        long currentGameMinute,
        out IReadOnlyList<MortalWoundTreatmentOutcomeIntent>? recomposedIntents)
    {
        recomposedIntents = null;
        var failedAxes = new List<string>();
        if (!acceptedState.HasCurrentAdmissionAuthority())
            failedAxes.Add("accepted_state");
        if (!MortalWoundTreatmentDetachedSealValidator.IsValid(request) ||
            resolution.RequestAuthority is null ||
            !MortalWoundTreatmentDetachedSealValidator.IsValid(resolution.RequestAuthority) ||
            !string.Equals(resolution.RequestFingerprint, request.RequestFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                resolution.RequestAuthority?.RequestFingerprint,
                request.RequestFingerprint,
                StringComparison.Ordinal))
        {
            failedAxes.Add("request");
        }
        if (!request.Coordinates.MatchesAcceptedState(acceptedState) ||
            !string.Equals(resolution.Coordinates.CoordinatesFingerprint,
                request.Coordinates.CoordinatesFingerprint, StringComparison.Ordinal) ||
            currentGameMinute != acceptedState.CurrentGameMinute)
        {
            failedAxes.Add("coordinates");
        }
        if (!acceptedState.MatchesCurrentWound(request.RouteSourceWound) ||
            !string.Equals(request.RouteSourceWoundFingerprint,
                request.Coordinates.ExpectedBeforeFingerprint,
                StringComparison.Ordinal))
        {
            failedAxes.Add("before_wound");
        }
        if (!string.Equals(request.Mode, resolution.Mode, StringComparison.Ordinal) ||
            request.Mode is not ("guaranteed" or "procedure" or "course"))
        {
            failedAxes.Add("mode");
        }
        if (!string.Equals(resolution.AttemptDisposition, "AcceptedTerminal",
                StringComparison.Ordinal))
        {
            failedAxes.Add("attempt_disposition");
        }
        if (request.Mode != "course" && (resolution.Interruption || resolution.CourseId is not null ||
            resolution.CourseMilestoneOrdinal is not null ||
            resolution.CourseDisposition is not null))
        {
            failedAxes.Add("course");
        }

        if (!DetachedSelectionAgrees(resolution, currentGameMinute) ||
            !TryProjectActiveCourseId(request.RouteSourceWound, request, resolution, out _))
        {
            failedAxes.Add("resolution_seal");
        }

        var recomposed = MortalWoundTreatmentOutcomeIntentComposer.TryCompose(
            request,
            resolution.DeclaredResult,
            acceptedState,
            out var expectedIntents,
            out _);
        if (!recomposed ||
            !OrderedIntentsAgree(request, resolution.DeclaredResult, resolution.OutcomeIntents, expectedIntents) ||
            !HasSupportedGrammar(resolution, expectedIntents))
        {
            failedAxes.Add("outcome");
        }
        else
        {
            recomposedIntents = expectedIntents.ToArray();
        }

        if (request.Mode is "guaranteed" or "course" &&
            resolution.CriticalReactionIntent is not null)
        {
            failedAxes.Add("critical_reaction");
        }
        if (failedAxes.Count == 0)
            return Array.Empty<ValidationIssue>();

        recomposedIntents = null;
        return new[]
        {
            Issue(
                "mortal_wound_treatment_publication_slice_unsupported",
                "one accepted-terminal guaranteed/procedure/course ordered scalar treatment result",
                string.Join(',', failedAxes))
        };
    }

    internal static bool OrderedIntentsAgree(
        MortalWoundTreatmentAttemptRequest request,
        IReadOnlyList<MortalWoundTreatmentOperation> declared,
        IReadOnlyList<MortalWoundTreatmentOutcomeIntent> actual,
        IReadOnlyList<MortalWoundTreatmentOutcomeIntent> expected)
    {
        if (actual.Count != expected.Count || declared.Count != actual.Count)
            return false;
        for (var index = 0; index < actual.Count; index++)
        {
            var left = actual[index];
            var right = expected[index];
            if (left is MortalWoundApplyDeteriorationOutcomeIntent leftPolicy &&
                (right is not MortalWoundApplyDeteriorationOutcomeIntent rightPolicy ||
                 declared[index] is not MortalWoundApplyDeteriorationOperation operation ||
                 !leftPolicy.TryGetPreparedDeterioration(request, operation, out var leftPrepared) ||
                 !rightPolicy.TryGetPreparedDeterioration(request, operation, out var rightPrepared) ||
                 leftPrepared is null || rightPrepared is null ||
                 leftPrepared.Fingerprint != rightPrepared.Fingerprint)) return false;
            if (left.GetType() != right.GetType() ||
                left.OperationOrdinal != right.OperationOrdinal ||
                left.OperationOrdinal != index ||
                !string.Equals(left.Kind, right.Kind, StringComparison.Ordinal) ||
                !string.Equals(left.DeclaredOperationFingerprint,
                    right.DeclaredOperationFingerprint, StringComparison.Ordinal) ||
                !string.Equals(left.IntentFingerprint, right.IntentFingerprint,
                    StringComparison.Ordinal) ||
                (left as MortalWoundReduceSeverityOutcomeIntent)?.Steps !=
                (right as MortalWoundReduceSeverityOutcomeIntent)?.Steps ||
                (left as MortalWoundAddRecoveryOutcomeIntent)?.Points !=
                (right as MortalWoundAddRecoveryOutcomeIntent)?.Points ||
                !string.Equals((left as MortalWoundRemoveComplicationOutcomeIntent)?.ComplicationId,
                    (right as MortalWoundRemoveComplicationOutcomeIntent)?.ComplicationId,
                    StringComparison.Ordinal) ||
                (left is MortalWoundAddComplicationOutcomeIntent leftAdd &&
                 (right is not MortalWoundAddComplicationOutcomeIntent rightAdd ||
                  leftAdd.ComplicationRef != rightAdd.ComplicationRef || leftAdd.ComplicationId != rightAdd.ComplicationId ||
                  leftAdd.PreparationFingerprint != rightAdd.PreparationFingerprint ||
                  !BindingsEqual(leftAdd.DefinitionReferenceBindings, rightAdd.DefinitionReferenceBindings) ||
                  !BindingsEqual(leftAdd.ApplicationReferenceBindings, rightAdd.ApplicationReferenceBindings))))
            {
                return false;
            }
        }
        return true;
    }

    private static bool HasSupportedGrammar(
        MortalWoundTreatmentResolution resolution, IReadOnlyList<MortalWoundTreatmentOutcomeIntent> intents)
    {
        var mode = resolution.Mode;
        var interruption = resolution.Interruption;
        var courseDisposition = resolution.CourseDisposition;
        if (intents.Any(UsesSelectedGraph))
        {
            if (!(mode == "procedure" && resolution.ResultCategory is "partial_success" or "failed_attempt") &&
                !(mode == "course" && interruption && courseDisposition == "interrupted")) return false;
            if (intents.Count != resolution.DeclaredResult.Count) return false;
            for (var i = 0; i < intents.Count; i++)
                if (intents[i] is MortalWoundApplyDeteriorationOutcomeIntent policy &&
                    (resolution.DeclaredResult[i] is not MortalWoundApplyDeteriorationOperation operation ||
                     !policy.TryGetPreparedDeterioration(resolution.RequestAuthority, operation, out var prepared) ||
                     prepared is null || prepared.Policy.ResultKind is not
                         (MortalWoundDeteriorationResultKind.IncreaseSeverity or MortalWoundDeteriorationResultKind.AddComplication)))
                    return false;
            return intents.Count <= MortalWoundTreatmentContract.MaxOutcomeOperations &&
                HasPositiveScalarGrammar(intents.Where(static intent => !UsesSelectedGraph(intent)).ToArray(), allowEmpty: true);
        }
        if (mode == "course")
        {
            if (interruption)
                return courseDisposition == "interrupted" && intents.Count == 1 &&
                    intents[0] is MortalWoundNoImprovementOutcomeIntent;
            return courseDisposition switch
            {
                "active" => intents.Count == 0 || HasPositiveScalarGrammar(intents),
                "completed" => HasPositiveScalarGrammar(intents),
                _ => false
            };
        }
        return (intents.Count == 1 && intents[0] is MortalWoundNoImprovementOutcomeIntent) ||
            HasPositiveScalarGrammar(intents);
    }

    private static bool HasPositiveScalarGrammar(
        IReadOnlyList<MortalWoundTreatmentOutcomeIntent> intents, bool allowEmpty = false)
    {
        if (allowEmpty && intents.Count == 0) return true;
        if (intents.Count is < 1 or > MortalWoundTreatmentContract.MaxOutcomeOperations)
            return false;
        var stabilizations = 0;
        var reductionSteps = 0;
        foreach (var intent in intents)
        {
            switch (intent)
            {
                case MortalWoundStabilizeOutcomeIntent:
                    if (++stabilizations > 1) return false;
                    break;
                case MortalWoundAddRecoveryOutcomeIntent recovery when recovery.Points > 0:
                    break;
                case MortalWoundRemoveComplicationOutcomeIntent removal
                    when ResourceMaterializationContract.IsExactIdentifier(removal.ComplicationId):
                    break;
                case MortalWoundReduceSeverityOutcomeIntent reduction
                    when reduction.Steps is >= 1 and <= 2:
                    reductionSteps = checked(reductionSteps + reduction.Steps);
                    if (reductionSteps > 2) return false;
                    break;
                default:
                    return false;
            }
        }
        return true;
    }

    private static WoundMaterializationEnvelope ApplyStabilization(
        WoundMaterializationEnvelope after,
        string transitionId,
        long currentGameMinute)
    {
        var blockers = after.Recovery.Blockers
            .Where(static blocker => !string.Equals(
                blocker, "not_stabilized", StringComparison.Ordinal))
            .ToImmutableArray();
        var anchors = ApplyStabilizationAnchors(after.Care, after.Recovery, after.LastTransition, currentGameMinute);
        return after with
        {
            Care = anchors.Care with
            {
                State = "stabilized",
                StabilizedAtTurn = after.LastTransition.Turn
            },
            Recovery = anchors.Recovery with
            {
                Blockers = blockers
            }
        };
    }

    internal static WoundMaterializationEnvelope CreateScalarShell(
        WoundMaterializationEnvelope before,
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResolution resolution,
        string transitionId,
        long currentGameMinute,
        IReadOnlyList<MortalWoundTreatmentOutcomeIntent> orderedIntents)
    {
        var metadata = CreatePublicationMetadata(before, request, resolution, transitionId);
        var scalarAfter = before with
        {
            Care = before.Care with { LastAttemptId = metadata.LastAttemptId, ActiveCourseId = metadata.ActiveCourseId },
            Treatment = metadata.Treatment, LastTransition = metadata.LastTransition
        };
        foreach (var intent in orderedIntents)
        {
            if (intent is MortalWoundStabilizeOutcomeIntent)
                scalarAfter = ApplyStabilization(scalarAfter, transitionId, currentGameMinute);
            else if (intent is MortalWoundAddRecoveryOutcomeIntent recovery)
                scalarAfter = scalarAfter with { Recovery = scalarAfter.Recovery with
                { CurrentStepProgress = checked(scalarAfter.Recovery.CurrentStepProgress + recovery.Points) } };
            else if (intent is MortalWoundRemoveComplicationOutcomeIntent removal &&
                !MortalWoundTreatmentWorkingWoundSimulator.TryRemoveComplication(scalarAfter, removal.ComplicationId, out scalarAfter))
                throw new InvalidOperationException("The exact selected complication is not applicable.");
        }
        return scalarAfter;
    }

    private sealed record TreatmentPublicationMetadata(string LastAttemptId, string? ActiveCourseId,
        WoundTreatment Treatment, WoundLastTransition LastTransition);

    private static TreatmentPublicationMetadata CreatePublicationMetadata(WoundMaterializationEnvelope before,
        MortalWoundTreatmentAttemptRequest request, MortalWoundTreatmentResolution resolution, string transitionId)
    {
        if (!TryProjectActiveCourseId(before, request, resolution, out var activeCourseId))
            throw new InvalidOperationException("The selected course does not own the active pointer.");
        var completedRoutes = resolution.RouteCompletion switch
        {
            "AppendOnce" => before.Treatment.CompletedRouteIds
                .Append(request.Coordinates.RouteId)
                .Distinct(StringComparer.Ordinal)
                .ToImmutableArray(),
            "None" => before.Treatment.CompletedRouteIds.ToImmutableArray(),
            _ => throw new InvalidOperationException(
                "Validated treatment route completion is not closed.")
        };
        return new(request.Coordinates.AttemptId, activeCourseId,
            before.Treatment with { CompletedRouteIds = completedRoutes },
            new WoundLastTransition(transitionId, checked(before.LastTransition.Ordinal + 1), request.Coordinates.Turn, "treat"));
    }

    private static (WoundCare Care, WoundRecovery Recovery) ApplyStabilizationAnchors(WoundCare care,
        WoundRecovery recovery, WoundLastTransition selectedTransition, long currentGameMinute) =>
        (care with { StabilizedAtTurn = selectedTransition.Turn }, recovery with
        {
            RecoveryAnchor = new WoundRecoveryAnchor("stabilization", currentGameMinute, selectedTransition.TransitionId),
            DeteriorationAnchor = recovery.DeteriorationAnchor is { ConditionKey: "not_stabilized" } ? null : recovery.DeteriorationAnchor
        });

    private static WoundFinalAssemblyScalars DecorateSelectedGraphScalars(MortalWoundTreatmentWorkingGraphProjection working,
        TreatmentPublicationMetadata metadata, MortalWoundTreatmentResolution resolution, long currentGameMinute)
    {
        var care = working.Scalars.Care with { LastAttemptId = metadata.LastAttemptId, ActiveCourseId = metadata.ActiveCourseId };
        var recovery = working.Scalars.Recovery;
        if (resolution.OutcomeIntents.Any(static intent => intent is MortalWoundStabilizeOutcomeIntent))
            (care, recovery) = ApplyStabilizationAnchors(care, recovery, metadata.LastTransition, currentGameMinute);
        var originalSeverity = resolution.RequestAuthority.RouteSourceWound.Severity;
        var severity = working.Scalars.Severity with
        {
            LastChangeEventRef = working.Scalars.Severity.Rank == originalSeverity.Rank
                ? originalSeverity.LastChangeEventRef : resolution.Coordinates.EventRef
        };
        return new(severity, care, recovery, metadata.Treatment, metadata.LastTransition, working.Scalars.SlotBudget);
    }

    private static WoundDeclaredTransitionOutcome CreateDeclaredOutcome(
        WoundMaterializationEnvelope after, MortalWoundTreatmentSelectedGraphCompilation? selectedGraph) => new(
        after.Severity.Rank,
        after.Care.State,
        after.Recovery.CurrentStepProgress,
        Heals: false,
        TerminalAttempt: true,
        AllowsWorsening: selectedGraph?.AllowsWorsening == true,
        after.Complications.Select(static value => value.ComplicationId).ToArray(),
        after.Consequences.OwnedEffectSources.RootBindings
            .Select(static value => value.EffectId)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray(),
        after.Recovery.Blockers.ToArray(),
        after.Treatment.CompletedRouteIds.ToArray());

    internal static string CreateTransitionId(
        MortalWoundTreatmentResolution resolution) =>
        "wound_transition_" + WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                "book_of_eternity.wound.treatment_transition_identity",
                "1",
                resolution.Coordinates.SessionGeneration,
                resolution.Coordinates.SessionId,
                resolution.Coordinates.RequestId,
                resolution.Coordinates.SnapshotToken,
                resolution.Coordinates.OperationKey,
                resolution.Coordinates.AttemptId,
                resolution.RequestFingerprint,
                resolution.ResultFingerprint
            })["sha256:".Length..];

    private static void Append(
        ICollection<string?> fields,
        IReadOnlyList<string> values)
    {
        fields.Add(values.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var value in values)
            fields.Add(value);
    }

    private static MortalWoundTreatmentOutcomePreparationResult InvalidPreparation(
        IReadOnlyList<ValidationIssue> issues) => new(null, issues.ToArray());

    private static MortalWoundTreatmentOutcomePublicationResult Invalid(
        IReadOnlyList<ValidationIssue> issues) => new(
        null, null, null, null, issues.ToArray());

    private static ValidationIssue Issue(
        string code,
        string expected,
        string actual) => new(
        IssuePath,
        IssueSeverity.Error,
        "The accepted Mortal wound treatment outcome could not be published.",
        code: code,
        actor: "Client",
        section: "wound_materialization",
        expected: expected,
        actual: actual,
        repairHint:
            "Reuse the exact sealed accepted terminal treatment while its authority remains current.");
}
