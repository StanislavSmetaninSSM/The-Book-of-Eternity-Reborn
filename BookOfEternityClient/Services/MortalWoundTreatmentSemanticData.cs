using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

/// <summary>
/// Evaluates retained treatment agreement DATA separately from live publication authority.
/// </summary>
internal sealed partial class MortalWoundTreatmentSemanticData
{
    private readonly MortalWoundTreatmentOutcomePreparation _outcome;
    private readonly MortalWoundTreatmentResolution _resolution;
    private readonly Func<OperationStructuralSnapshot?> _capture;
    private static readonly OperationComputation<bool> OutcomeAgreement = new(
        "TreatmentSemantic.OutcomeAgreement", 1, value => value);
    private static readonly OperationComputation<string?> FinalizationFingerprint = new(
        "TreatmentSemantic.FinalizationFingerprint", 1, value => value);

    /// <summary>
    /// Retains the exact detached outcome and resolution for their original semantic computations.
    /// </summary>
    /// <param name="outcome">
    /// The privately retained outcome whose original agreement predicate is evaluated.
    /// </param>
    /// <param name="resolution">
    /// The actual resolution and request companions, including externally reachable DATA.
    /// </param>
    internal MortalWoundTreatmentSemanticData(MortalWoundTreatmentOutcomePreparation outcome,
        MortalWoundTreatmentResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(resolution);
        _outcome = outcome;
        _resolution = resolution;
        _capture = CaptureCurrent;
    }

    /// <summary>
    /// Reuses outcome agreement DATA only while the complete supported structural input remains equal.
    /// </summary>
    /// <param name="operation">
    /// The optional whole-operation cache context; <see langword="null"/> preserves the original uncached computation.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the original retained-outcome agreement succeeds; otherwise <see langword="false"/>.
    /// </returns>
    internal bool ReadOutcomeAgreement(ValidationOperation? operation) =>
        Read(operation, OutcomeAgreement, () => _outcome.AgreesWith(_resolution));

    /// <summary>
    /// Reuses the checked resource finalization fingerprint as DATA without retaining a live permission.
    /// </summary>
    /// <param name="operation">
    /// The optional whole-operation cache context; <see langword="null"/> preserves the original uncached computation.
    /// </param>
    /// <returns>
    /// The original checked finalization fingerprint, or <see langword="null"/> for the original refusal.
    /// </returns>
    internal string? ReadFinalizationFingerprint(ValidationOperation? operation) =>
        Read(operation, FinalizationFingerprint, () =>
            MortalWoundTreatmentResourceComposer.Finalize(_resolution).Finalization?.FinalizationFingerprint);

    /// <summary>
    /// Applies the existing operation protocol to one immutable semantic result and its complete structural dependency.
    /// </summary>
    /// <typeparam name="TResult">
    /// Immutable scalar result retained by the registered computation.
    /// </typeparam>
    /// <param name="operation">
    /// The owning temporary context, or <see langword="null"/> for an unchanged direct source call.
    /// </param>
    /// <param name="computation">
    /// The exact algorithm and scalar copy descriptor.
    /// </param>
    /// <param name="compute">
    /// The original deterministic source using this owner's retained inputs.
    /// </param>
    /// <returns>
    /// The original computed or structurally current retained result, including <see langword="null"/> and <see langword="false"/>.
    /// </returns>
    private TResult Read<TResult>(ValidationOperation? operation, OperationComputation<TResult> computation,
        Func<TResult> compute)
    {
        if (operation is null) return compute();
        var dependency = operation.GetStructuralDependency(this, _capture);
        return operation.GetOrCompute(computation, this, [dependency], compute);
    }

    /// <summary>
    /// Captures supported detached guaranteed stabilization DATA without invoking domain validation or live I/O.
    /// </summary>
    /// <returns>
    /// Exact ordered current values and companion identities, or <see langword="null"/> for an unaudited graph shape.
    /// </returns>
    private OperationStructuralSnapshot? CaptureCurrent()
    {
        var request = _resolution.RequestAuthority;
        // These closed cases have no private deterioration preparation or selected graph
        // aliased through an intent. Other game cases keep their original computation.
        if (_resolution.Mode != "guaranteed" || request.Mode != "guaranteed" ||
            request.ModeAuthority is not MortalWoundTreatmentCapabilityProof ||
            _resolution.ModeEvidence is not MortalWoundGuaranteedModeEvidence ||
            _resolution.CriticalReactionIntent is not null ||
            _resolution.DeclaredResult.Count != 1 ||
            _resolution.DeclaredResult[0] is not MortalWoundStabilizeOperation ||
            _resolution.OutcomeIntents.Count != 1 ||
            _resolution.OutcomeIntents[0] is not MortalWoundStabilizeOutcomeIntent) return null;
        var wound = request.RouteSourceWound;
        var owned = wound.Consequences.OwnedEffectSources;
        var values = new List<string?>
        {
            JsonSerializer.Serialize(_resolution, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
            JsonSerializer.Serialize(owned, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
            JsonSerializer.Serialize(owned.DefinitionFacts, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)
        };
        values.AddRange(wound.Treatment.DiagnosisPaths.Select(path => path.SourcePath));
        values.AddRange(wound.Treatment.Routes.Select(route => route.SourcePath));
        // Public JSON omits these provenance values. They are immutable get-only
        // values on exact claim companions, but remain explicit in the DATA capture.
        foreach (var claim in request.ResourceAuthority.Claims)
        {
            values.Add(claim.AvailableQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture));
            values.Add(claim.BindingFingerprint);
        }
        return new(values,
        [
            _outcome, _resolution, request, request.Coordinates, request.ModeAuthority,
            request.RequirementAuthority, request.ResourceAuthority, _resolution.ModeEvidence,
            _resolution.RequirementAuthority, _resolution.ResourceAuthority
        ]);
    }
}
