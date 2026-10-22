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
    private readonly object _lifecycle = new();
    private readonly SemaphoreSlim _serial = new(1, 1);
    private readonly AsyncLocal<bool> _sourceFlow = new();
    private int _pending;
    private bool _closed;
    private bool _sourceUsed;
    private bool _validationClaimed;
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
    public async Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_sourceFlow.Value)
            throw new InvalidOperationException("A source callback cannot recursively acquire its own cache storage.");
        lock (_lifecycle)
        {
            RequireOpen();
            if (_validationClaimed)
                throw new InvalidOperationException("Validation-owned storage cannot also serve repository requests.");
            _sourceUsed = true;
            _pending++;
        }
        var held = false;
        try
        {
            await _serial.WaitAsync(cancellationToken).ConfigureAwait(false);
            held = true;
            lock (_lifecycle) RequireOpen();
            cancellationToken.ThrowIfCancellationRequested();
            _sourceFlow.Value = true;
            var result = await action(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            lock (_lifecycle) RequireOpen();
            return result;
        }
        finally
        {
            _sourceFlow.Value = false;
            if (held) _serial.Release();
            lock (_lifecycle)
            {
                _pending--;
                if (_closed && _pending == 0) _serial.Dispose();
            }
        }
    }

    /// <summary>
    /// Claims this storage for exactly one validation operation, excluding later reuse.
    /// </summary>
    internal void ClaimValidationOperation()
    {
        lock (_lifecycle)
        {
            RequireOpen();
            if (_validationClaimed || _sourceUsed)
                throw new InvalidOperationException("A validation operation requires its own unused cache storage.");
            _validationClaimed = true;
        }
        if (Cache.Count != 0)
            throw new InvalidOperationException("A new validation operation cannot inherit earlier cache payloads.");
    }

    /// <summary>
    /// Stops retaining cache payloads without disposing caller-owned values or repositories.
    /// </summary>
    public void Dispose()
    {
        bool disposeSerial;
        lock (_lifecycle)
        {
            if (_closed) return;
            _closed = true;
            disposeSerial = _pending == 0;
        }
        try { Cache.Dispose(); }
        finally { if (disposeSerial) _serial.Dispose(); }
    }

    /// <summary>
    /// Refuses source use after owner close while the lifecycle monitor is held.
    /// </summary>
    private void RequireOpen() => ObjectDisposedException.ThrowIf(_closed, this);
}
