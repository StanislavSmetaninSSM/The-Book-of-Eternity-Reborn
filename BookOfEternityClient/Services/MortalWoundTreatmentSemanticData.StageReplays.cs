namespace BookOfEternityClient.Services;

internal sealed partial class MortalWoundTreatmentSemanticData
{
    /// <summary>
    /// Evaluates the initial uncached deterministic preparation replay source.
    /// </summary>
    /// <param name="operation">
    /// The eligible operation, or null when the privately retained input contour is unsupported.
    /// </param>
    /// <param name="compute">
    /// The original replay over this continuation's privately owned acquisition input.
    /// </param>
    /// <returns>
    /// The expected preparation fingerprint, or null for the original replay refusal.
    /// </returns>
    internal string? ReadPreparationFingerprint(ValidationOperation? operation, Func<string?> compute) => compute();

    /// <summary>
    /// Evaluates the initial uncached pure reduction after the original fresh handoff and joint admission.
    /// </summary>
    /// <param name="operation">
    /// The eligible operation, or null when the privately retained input contour is unsupported.
    /// </param>
    /// <param name="admittedInput">
    /// Already freshly verified preparation, baseline and effect DATA seals with their actual companions.
    /// </param>
    /// <param name="compute">
    /// The original pure reduction using only the admitted detached input for this invocation.
    /// </param>
    /// <returns>
    /// The original detached final result, including unchanged refusal issues.
    /// </returns>
    internal WoundAcceptedTurnPlanningResult ReadFinalReduction(ValidationOperation? operation,
        OperationStructuralSnapshot admittedInput, Func<WoundAcceptedTurnPlanningResult> compute) => compute();
}
