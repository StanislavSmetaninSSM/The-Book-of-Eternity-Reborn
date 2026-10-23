namespace BookOfEternityClient.Services;

internal sealed partial class MortalWoundTreatmentSemanticData
{
    private static readonly OperationComputation<string> PreparationFingerprint = new(
        "JointSemantic.ExpectedPreparation", 1, value => value);
    // The sealed result owns private copies and exports independent plan/issue
    // copies. Sharing that immutable envelope cannot export its retained graphs.
    private static readonly OperationComputation<WoundAcceptedTurnPlanningResult> FinalReduction = new(
        "JointSemantic.FinalReduction", 1, value => value);

    /// <summary>
    /// Reuses a successful deterministic preparation seal under the complete retained semantic input guard.
    /// </summary>
    /// <param name="operation">
    /// The eligible operation, or <see langword="null"/> when the privately retained input contour is unsupported.
    /// </param>
    /// <param name="compute">
    /// The original replay over this continuation's privately owned acquisition input.
    /// </param>
    /// <returns>
    /// The expected preparation fingerprint, or <see langword="null"/> for the original replay refusal.
    /// </returns>
    internal string? ReadPreparationFingerprint(ValidationOperation? operation, Func<string?> compute)
    {
        if (operation is null) return compute();
        try
        {
            return Read(operation, PreparationFingerprint,
                () => compute() ?? throw new StageReplayRefusedException(null));
        }
        catch (StageReplayRefusedException) { return null; }
    }

    /// <summary>
    /// Reuses successful pure reduction DATA after the original fresh handoff and joint admission.
    /// </summary>
    /// <param name="operation">
    /// The eligible operation, or <see langword="null"/> when the privately retained input contour is unsupported.
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
        OperationStructuralSnapshot admittedInput, Func<WoundAcceptedTurnPlanningResult> compute)
    {
        if (operation is null) return compute();
        var dependency = operation.GetStructuralDependency(this, _capture);
        var input = operation.GetStructuralInputIdentity(this, admittedInput);
        try
        {
            return operation.GetOrCompute(FinalReduction, input, [dependency], () =>
            {
                var result = compute();
                if (!result.Success) throw new StageReplayRefusedException(result);
                return result;
            });
        }
        catch (StageReplayRefusedException error) when (error.Result is not null) { return error.Result; }
    }

    /// <summary>
    /// Carries an original replay refusal across the cache's failure invalidation boundary without retaining it.
    /// </summary>
    private sealed class StageReplayRefusedException : Exception
    {
        /// <summary>
        /// Retains the original final refusal when present; a <see langword="null"/> value represents preparation refusal.
        /// </summary>
        /// <param name="result">
        /// The original detached final refusal envelope, or <see langword="null"/> for an unavailable preparation seal.
        /// </param>
        internal StageReplayRefusedException(WoundAcceptedTurnPlanningResult? result) => Result = result;

        /// <summary>
        /// Gets the original detached final refusal, or <see langword="null"/> for preparation refusal.
        /// </summary>
        internal WoundAcceptedTurnPlanningResult? Result { get; }
    }
}
