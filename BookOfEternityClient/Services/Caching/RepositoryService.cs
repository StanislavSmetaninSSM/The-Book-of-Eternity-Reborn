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
    public override async Task<CacheLookup<TValue>> ReadAsync(TKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        try
        {
            return await _storage.ExecuteAsync(async token =>
            {
                if (_storage.Cache.TryGet(key, out var retained))
                    return new CacheLookup<TValue>(true, _copy(retained));
                var source = await _repository.ReadAsync(key, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (!source.Found) return new CacheLookup<TValue>(false, default!);
                var stored = _copy(source.Value);
                var result = _copy(stored);
                token.ThrowIfCancellationRequested();
                _storage.Cache.Set(key, stored);
                return new CacheLookup<TValue>(true, result);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            InvalidatePreservingFailure(key, all: false);
            throw;
        }
    }

    /// <inheritdoc />
    public override Task SaveAsync(TKey key, TValue value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        return WriteAsync(key, token => _repository.SaveAsync(key, _copy(value), token), cancellationToken);
    }

    /// <inheritdoc />
    public override Task DeleteAsync(TKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        return WriteAsync(key, token => _repository.DeleteAsync(key, token), cancellationToken);
    }

    /// <summary>
    /// Invalidates before and after an original source change, including partial failure.
    /// </summary>
    /// <param name="key">
    /// Exact key used if invalidation must be reported through a failure cleanup path.
    /// </param>
    /// <param name="write">
    /// Actual source operation invoked outside a cache monitor.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation for source serialization and the source operation.
    /// </param>
    /// <returns>
    /// Completion of source work and invalidation, preserving an original source failure.
    /// </returns>
    private async Task WriteAsync(TKey key, Func<CancellationToken, Task> write, CancellationToken cancellationToken)
    {
        await _storage.ExecuteAsync(async token =>
        {
            try
            {
                _storage.Cache.Clear();
                await write(token).ConfigureAwait(false);
                _storage.Cache.Clear();
            }
            catch
            {
                InvalidatePreservingFailure(key, all: true);
                throw;
            }
            return 0;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes potentially stale data without replacing the exception already being propagated.
    /// </summary>
    /// <param name="key">
    /// Failed load's exact key, ignored for whole-storage invalidation.
    /// </param>
    /// <param name="all">
    /// Whether a source mutation requires clearing all dependent entries in this storage.
    /// </param>
    private void InvalidatePreservingFailure(TKey key, bool all)
    {
        try
        {
            if (all) _storage.Cache.Clear();
            else _storage.Cache.Remove(key);
        }
        catch
        {
            try { _storage.Dispose(); }
            catch { /* Owner is already closed; preserve the original source/cache failure. */ }
        }
    }
}
