namespace BookOfEternityClient.Services;

/// <summary>
/// Owns temporary computation results for one caller-defined operation.
/// </summary>
internal sealed class ValidationOperation : IDisposable
{
    private bool _closed;

    /// <summary>
    /// Creates an initially empty operation context.
    /// </summary>
    internal ValidationOperation() { }

    /// <summary>
    /// Computes a typed result under the operation's explicit input and dependency contract.
    /// </summary>
    /// <typeparam name="TResult">
    /// The computation result type, which may legitimately contain its default value.
    /// </typeparam>
    /// <param name="computation">
    /// Stable registered algorithm and copy policy.
    /// </param>
    /// <param name="inputIdentity">
    /// Stable identity covering the complete input and policy; mutable inputs need live validation or versions.
    /// </param>
    /// <param name="dependencies">
    /// Complete ordered direct provider/domain declarations, or an empty list for immutable input alone.
    /// </param>
    /// <param name="compute">
    /// Original computation invoked when a reusable value is unavailable.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation observed before and after the original computation.
    /// </param>
    /// <returns>
    /// A detached result of the original typed computation.
    /// </returns>
    internal async Task<TResult> GetOrComputeAsync<TResult>(OperationComputation<TResult> computation,
        object inputIdentity, IReadOnlyList<IOperationDependency> dependencies,
        Func<CancellationToken, Task<TResult>> compute, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_closed, this);
        ArgumentNullException.ThrowIfNull(computation);
        ArgumentNullException.ThrowIfNull(inputIdentity);
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(compute);
        cancellationToken.ThrowIfCancellationRequested();
        // Fresh-only baseline for the observed causal RED; no reuse is implemented yet.
        var value = await compute(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_closed, this);
        return computation.Copy(value);
    }

    /// <summary>
    /// Records only a validation that completes normally without a refusal.
    /// </summary>
    /// <param name="rule">
    /// Stable identity of the throw-on-failure validation.
    /// </param>
    /// <param name="inputIdentity">
    /// Complete stable identity of its inputs and policy.
    /// </param>
    /// <param name="dependencies">
    /// Complete ordered direct dependency declarations.
    /// </param>
    /// <param name="validate">
    /// Actual validation, which must throw on refusal rather than return a discarded Boolean.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation observed before and after validation.
    /// </param>
    /// <returns>
    /// A task completing only after a successful validation result is obtained.
    /// </returns>
    internal async Task ValidateAsync(OperationValidationRule rule, object inputIdentity,
        IReadOnlyList<IOperationDependency> dependencies, Func<CancellationToken, Task> validate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(validate);
        await GetOrComputeAsync(rule.Computation, inputIdentity, dependencies, async token =>
        {
            await validate(token);
            return new ValidationSucceeded();
        }, cancellationToken);
    }

    /// <summary>
    /// Closes the context so subsequent requests cannot use it.
    /// </summary>
    public void Dispose() => _closed = true;
}
