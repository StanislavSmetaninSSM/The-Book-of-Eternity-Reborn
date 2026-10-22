namespace BookOfEternityClient.Services.Caching;

/// <summary>
/// Owns one cache mechanism and an independently controlled storage lifetime.
/// </summary>
/// <typeparam name="TKey">
/// The cache key type.
/// </typeparam>
/// <typeparam name="TValue">
/// The retained value type.
/// </typeparam>
public sealed class ServiceCacheStorage<TKey, TValue> : IServiceCacheStorage<TKey, TValue> where TKey : notnull
{
    /// <summary>
    /// Claims exclusive ownership of a newly supplied cache backend.
    /// </summary>
    /// <param name="cache">
    /// Unclaimed backend; a repeated claim must not clear its original owner's data.
    /// </param>
    public ServiceCacheStorage(ServiceCache<TKey, TValue> cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        cache.ClaimStorageOwnership();
        Cache = cache;
    }

    /// <inheritdoc />
    public ServiceCache<TKey, TValue> Cache { get; }

    /// <inheritdoc />
    public Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken = default) => action(cancellationToken);

    /// <summary>
    /// Claims this storage for exactly one validation operation, excluding later reuse.
    /// </summary>
    internal void ClaimValidationOperation() { }

    /// <summary>
    /// Stops retaining cache payloads without disposing caller-owned values or repositories.
    /// </summary>
    public void Dispose() => Cache.Dispose();
}
