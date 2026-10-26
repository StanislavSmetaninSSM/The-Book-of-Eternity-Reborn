namespace BookOfEternityClient.Services;

internal sealed partial class MortalWoundTreatmentSemanticData
{
    private static readonly OperationComputation<ExpectedPreparationReplayProjection> PreparationProjection = new(
        "JointSemantic.ExpectedPreparationProjection", 1, value => value);
    // The sealed result owns private copies and exports independent plan/issue
    // copies. Sharing that immutable envelope cannot export its retained graphs.
    private static readonly OperationComputation<WoundAcceptedTurnPlanningResult> FinalReduction = new(
        "JointSemantic.FinalReduction", 1, value => value);

    /// <summary>Shares the full successful expected preparation DATA within the existing operation.</summary>
    /// <param name="operation">The existing eligible operation, or null for the original source.</param>
    /// <param name="admittedInput">The complete fixed input DATA and private companion identities.</param>
    /// <param name="compute">The original source; refusal and exceptions are never retained.</param>
    /// <returns>The immutable expected vector or original refusal.</returns>
    internal ExpectedPreparationReplayProjection? ReadPreparationProjection(ValidationOperation? operation,
        OperationStructuralSnapshot admittedInput, Func<ExpectedPreparationReplayProjection?> compute)
    {
        if (operation is null) return compute();
        var dependency = operation.GetStructuralDependency(this, _capture);
        var input = operation.GetStructuralInputIdentity(this, admittedInput);
        try
        {
            return operation.GetOrCompute(PreparationProjection, input, [dependency],
                () => compute() ?? throw new StageReplayRefusedException(null));
        }
        catch (StageReplayRefusedException) { return null; }
    }

    /// <summary>Invalidates eligible DATA and executes the original source in its original order.</summary>
    /// <param name="operation">The actual serial operation; null preserves the ordinary path.</param>
    /// <param name="compute">The unchanged source, including original issues and exceptions.</param>
    /// <returns>The source result without retaining moved-preflight conclusions.</returns>
    internal static ExpectedPreparationReplayProjection? ReadPreparationProjectionFallback(ValidationOperation? operation,
        Func<ExpectedPreparationReplayProjection?> compute)
    {
        if (operation is null) return compute();
        var fresh = operation.ForceFresh(); // protocol errors remain visible
        try
        {
            var result = compute();
            fresh.Dispose();
            return result;
        }
        catch
        {
            try { fresh.Dispose(); } catch { /* Original source/protocol failure keeps precedence. */ }
            throw;
        }
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
