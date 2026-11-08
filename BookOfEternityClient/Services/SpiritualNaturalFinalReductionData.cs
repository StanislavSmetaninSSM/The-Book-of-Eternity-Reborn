namespace BookOfEternityClient.Services;

/// <summary>Owns successful pure natural-reduction DATA through the existing operation cache, never authority.</summary>
internal sealed class SpiritualNaturalFinalReductionData : IDisposable
{
    private static readonly OperationComputation<WoundAcceptedTurnPlanningResult> Reduction =
        new("SpiritualNatural.FinalReduction", 1, value => value);
    private readonly object _gate = new();
    private Func<OperationStructuralSnapshot?>? _capture;
    private ValidationOperation? _operation;
    private bool _closed;

    /// <summary>Retains the exact owner's complete synchronous DATA capture policy.</summary>
    /// <param name="capture">Complete immutable values and companions, or null when reuse is unsupported.</param>
    internal SpiritualNaturalFinalReductionData(Func<OperationStructuralSnapshot?> capture) =>
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));

    /// <summary>Reuses a detached successful reduction only after its caller repeats all original admission checks.</summary>
    /// <param name="admittedInput">The freshly verified complete preparation, baseline and effect seals and companions.</param>
    /// <param name="compute">The original pure reducer, with unchanged failure results and exceptions.</param>
    /// <returns>An immutable envelope whose plan and issues are independently detached on access.</returns>
    internal WoundAcceptedTurnPlanningResult Read(OperationStructuralSnapshot admittedInput,
        Func<WoundAcceptedTurnPlanningResult> compute)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(admittedInput);
            ArgumentNullException.ThrowIfNull(compute);
            ValidationOperation operation;
            Func<OperationStructuralSnapshot?> capture;
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_closed, this);
                operation = _operation ??= new DefaultOperationValidationService().BeginOperation();
                capture = _capture!;
            }
            var dependency = operation.GetStructuralDependency(this, capture);
            var input = operation.GetStructuralInputIdentity(this, admittedInput);
            return operation.GetOrCompute(Reduction, input, [dependency], () =>
            {
                var result = compute();
                if (!result.Success) throw new ReductionRefused(result);
                return result;
            });
        }
        catch (ReductionRefused refusal) { Invalidate(); return refusal.Result; }
        catch { Invalidate(); throw; }
    }

    /// <summary>Retires every retained value after failure or a change, so restoring old inputs cannot resurrect it.</summary>
    internal void Invalidate()
    {
        ValidationOperation? operation;
        lock (_gate) { operation = _operation; _operation = null; }
        operation?.Dispose();
    }

    /// <summary>Permanently closes this owner and drops its operation-owned payloads.</summary>
    public void Dispose()
    {
        lock (_gate) { _closed = true; _capture = null; }
        Invalidate();
    }

    /// <summary>Carries an original refusal across cache invalidation without retaining a failed value.</summary>
    /// <param name="result">The original detached refusal envelope.</param>
    private sealed class ReductionRefused(WoundAcceptedTurnPlanningResult result) : Exception
    {
        /// <summary>Gets the unchanged refusal to return after invalidation.</summary>
        internal WoundAcceptedTurnPlanningResult Result { get; } = result;
    }
}
