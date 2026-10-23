namespace BookOfEternityClient.Services;

/// <summary>
/// Evaluates retained treatment agreement DATA separately from live publication authority.
/// </summary>
internal sealed class MortalWoundTreatmentSemanticData
{
    private readonly MortalWoundTreatmentOutcomePreparation _outcome;
    private readonly MortalWoundTreatmentResolution _resolution;

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
    }

    /// <summary>
    /// Evaluates the original outcome agreement through the initial uncached source seam.
    /// </summary>
    /// <param name="operation">
    /// The optional whole-operation cache context; the initial causal seam still executes the source.
    /// </param>
    /// <returns>
    /// True when the original retained-outcome agreement succeeds; otherwise false.
    /// </returns>
    internal bool ReadOutcomeAgreement(ValidationOperation? operation) => _outcome.AgreesWith(_resolution);

    /// <summary>
    /// Evaluates the original resource finalization through the initial uncached source seam.
    /// </summary>
    /// <param name="operation">
    /// The optional whole-operation cache context; the initial causal seam still executes the source.
    /// </param>
    /// <returns>
    /// The original checked finalization fingerprint, or null for the original refusal.
    /// </returns>
    internal string? ReadFinalizationFingerprint(ValidationOperation? operation) =>
        MortalWoundTreatmentResourceComposer.Finalize(_resolution).Finalization?.FinalizationFingerprint;
}
