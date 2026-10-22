namespace BookOfEternityClient.Services.Caching;

/// <summary>
/// Distinguishes source absence from present values, including null and defaults.
/// </summary>
/// <typeparam name="TValue">
/// The source value type.
/// </typeparam>
/// <param name="Found">
/// Whether the source contains the requested record.
/// </param>
/// <param name="Value">
/// The actual value when found; ignored when the source is absent.
/// </param>
public readonly record struct CacheLookup<TValue>(bool Found, TValue Value);

/// <summary>
/// Reads and changes a domain's real source independently of its cache.
/// </summary>
/// <typeparam name="TKey">
/// The domain's non-null key type.
/// </typeparam>
/// <typeparam name="TValue">
/// The original source value type.
/// </typeparam>
public interface ICacheRepository<TKey, TValue> where TKey : notnull
{
    /// <summary>
    /// Reads the actual source record.
    /// </summary>
    /// <param name="key">
    /// Exact domain key.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation for source work.
    /// </param>
    /// <returns>
    /// Source presence and value, with absence distinct from a present null.
    /// </returns>
    Task<CacheLookup<TValue>> ReadAsync(TKey key, CancellationToken cancellationToken);

    /// <summary>
    /// Saves through the original source, including its normalization and integrity rules.
    /// </summary>
    /// <param name="key">
    /// Exact domain key.
    /// </param>
    /// <param name="value">
    /// Detached value submitted to the source.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation for source work; a cancelled write may still require cache invalidation.
    /// </param>
    /// <returns>
    /// Completion of the source save, or its original failure.
    /// </returns>
    Task SaveAsync(TKey key, TValue value, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes through the original source.
    /// </summary>
    /// <param name="key">
    /// Exact domain key.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation for source work.
    /// </param>
    /// <returns>
    /// Completion of deletion, or its original failure.
    /// </returns>
    Task DeleteAsync(TKey key, CancellationToken cancellationToken);
}
