namespace BookOfEternityClient.Services.Caching;

/// <summary>
/// Defines typed source operations independently of a cache mechanism.
/// </summary>
/// <typeparam name="TKey">
/// The source key type.
/// </typeparam>
/// <typeparam name="TValue">
/// The domain value type.
/// </typeparam>
public abstract class RepositoryService<TKey, TValue> where TKey : notnull
{
    /// <summary>
    /// Reads a detached record through the domain's source and cache rules.
    /// </summary>
    /// <param name="key">
    /// Exact domain key.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation observed around source/cache work.
    /// </param>
    /// <returns>
    /// Detached present value or explicit absence.
    /// </returns>
    public abstract Task<CacheLookup<TValue>> ReadAsync(TKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves to the actual source and invalidates previous cached data.
    /// </summary>
    /// <param name="key">
    /// Exact domain key.
    /// </param>
    /// <param name="value">
    /// Value submitted through the domain's defensive copy policy.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation observed around source work.
    /// </param>
    /// <returns>
    /// Completion of the source save; later reads observe its normalized data.
    /// </returns>
    public abstract Task SaveAsync(TKey key, TValue value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes from the actual source and invalidates previous cached data.
    /// </summary>
    /// <param name="key">
    /// Exact domain key.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation observed around source work.
    /// </param>
    /// <returns>
    /// Completion of the original source deletion.
    /// </returns>
    public abstract Task DeleteAsync(TKey key, CancellationToken cancellationToken = default);
}

/// <summary>
/// Combines a real repository, borrowed storage and a domain copy policy.
/// </summary>
/// <typeparam name="TKey">
/// The source key type.
/// </typeparam>
/// <typeparam name="TValue">
/// The source value type, including legitimate null/default values.
/// </typeparam>
public sealed class DefaultRepositoryService<TKey, TValue> : RepositoryService<TKey, TValue> where TKey : notnull
{
    private readonly ICacheRepository<TKey, TValue> _repository;
    private readonly IServiceCacheStorage<TKey, TValue> _storage;
    private readonly Func<TValue, TValue> _copy;

    /// <summary>
    /// Creates a service without taking ownership of the source or storage lifetime.
    /// </summary>
    /// <param name="repository">
    /// Actual domain source, whose writes retain their original integrity rules.
    /// </param>
    /// <param name="storage">
    /// Borrowed owner-controlled cache and shared source serialization gate.
    /// </param>
    /// <param name="copy">
    /// Detaches mutable values, or returns immutable/permitted null values unchanged.
    /// </param>
    public DefaultRepositoryService(ICacheRepository<TKey, TValue> repository,
        IServiceCacheStorage<TKey, TValue> storage, Func<TValue, TValue> copy)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(copy);
        _repository = repository;
        _storage = storage;
        _copy = copy;
    }

    /// <inheritdoc />
    public override Task<CacheLookup<TValue>> ReadAsync(TKey key, CancellationToken cancellationToken = default) =>
        _repository.ReadAsync(key, cancellationToken);

    /// <inheritdoc />
    public override Task SaveAsync(TKey key, TValue value, CancellationToken cancellationToken = default) =>
        _repository.SaveAsync(key, value, cancellationToken);

    /// <inheritdoc />
    public override Task DeleteAsync(TKey key, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(key, cancellationToken);
}
