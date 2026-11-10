namespace BookOfEternityClient.Services.Caching;

/// <summary>
/// Owns configured cache retention separately from the services borrowing it.
/// </summary>
/// <typeparam name="TKey">
/// The cache key type.
/// </typeparam>
/// <typeparam name="TValue">
/// The retained value type.
/// </typeparam>
public interface IServiceCacheStorage<TKey, TValue> : IDisposable where TKey : notnull
{
    /// <summary>
    /// Gets the configured cache mechanism for this storage's lifetime.
    /// </summary>
    ServiceCache<TKey, TValue> Cache { get; }

    /// <summary>
    /// Serializes source work across services borrowing this storage without holding a monitor across awaits.
    /// </summary>
    /// <typeparam name="TResult">
    /// The action result type.
    /// </typeparam>
    /// <param name="action">
    /// Source/cache work; recursive access to this same gate is refused.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation for gate acquisition and the action.
    /// </param>
    /// <returns>
    /// The action result only while this owner remains open and cancellation has not been requested.
    /// </returns>
    Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken = default);
}
