namespace BookOfEternityClient.Services;

/// <summary>
/// Orders resource validation and fresh entry/exit checks around detached QTE preparation.
/// </summary>
internal static class JointRepairValidationCoordinator
{
    /// <summary>
    /// Validates the resource seal, recomputes its QTE contribution and compares its companions.
    /// </summary>
    /// <typeparam name="T">
    /// The detached contribution returned by the QTE recomputation.
    /// </typeparam>
    /// <param name="validateSeal">
    /// The current resource seal check; failure prevents every subsequent operation.
    /// </param>
    /// <param name="recomputeQte">
    /// The recomputation that performs its own fresh baseline checks at entry and exit.
    /// </param>
    /// <param name="compareCompanions">
    /// The exact companion comparison, invoked only after successful recomputation.
    /// </param>
    /// <returns>
    /// Completion after comparison, propagating the first failure without subsequent operations.
    /// </returns>
    internal static async Task ValidateResourceAsync<T>(Action validateSeal,
        Func<Task<T>> recomputeQte, Action<T> compareCompanions)
    {
        validateSeal();
        var contribution = await recomputeQte();
        compareCompanions(contribution);
    }

    /// <summary>
    /// Prepares detached data between fresh baseline checks, returning only after the exit check.
    /// </summary>
    /// <typeparam name="TPrepared">
    /// The detached preparation result, which grants no publication authority.
    /// </typeparam>
    /// <param name="validateBaseline">
    /// The fresh current-source check invoked at entry and, after successful preparation, at exit.
    /// </param>
    /// <param name="compute">
    /// The preparation performed after entry; failure skips the exit check as well as result return.
    /// </param>
    /// <returns>
    /// The prepared data after both checks succeed; the first failure propagates unchanged.
    /// </returns>
    internal static async Task<TPrepared> RecomputeWithBaselineAsync<TPrepared>(Func<Task> validateBaseline,
        Func<Task<TPrepared>> compute)
    {
        await validateBaseline();
        var prepared = await compute();
        await validateBaseline();
        return prepared;
    }
}
