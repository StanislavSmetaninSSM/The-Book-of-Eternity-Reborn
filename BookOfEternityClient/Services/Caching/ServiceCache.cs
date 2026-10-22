namespace BookOfEternityClient.Services.Caching;

/// <summary>
/// Defines synchronized storage operations for a caller-owned temporary cache.
/// Backend hooks perform only synchronous memory work and do not call source services.
/// </summary>
/// <typeparam name="TKey">
/// The non-<see langword="null"/> key type.
/// </typeparam>
/// <typeparam name="TValue">
/// The stored value type; default and <see langword="null"/> values remain distinguishable from absence.
/// </typeparam>
public abstract class ServiceCache<TKey, TValue> : IDisposable where TKey : notnull
{
    private bool _storageClaimed;

    /// <summary>
    /// Initializes the key comparer used by a storage mechanism.
    /// </summary>
    /// <param name="comparer">
    /// The comparer, or <see langword="null"/> to use the default equality comparer.
    /// </param>
    protected ServiceCache(IEqualityComparer<TKey>? comparer = null) => Comparer = comparer ?? EqualityComparer<TKey>.Default;

    /// <summary>
    /// Gets the synchronization gate shared by public operations and backend callbacks.
    /// </summary>
    protected object SyncRoot { get; } = new();

    /// <summary>
    /// Gets the key equality comparer selected during construction.
    /// </summary>
    protected IEqualityComparer<TKey> Comparer { get; }

    /// <summary>
    /// Gets whether the owner has closed the cache; read under <see cref="SyncRoot"/>.
    /// </summary>
    protected bool IsDisposed { get; private set; }

    /// <summary>
    /// Gets the number of current entries without renewing sliding retention.
    /// </summary>
    public int Count
    {
        get
        {
            lock (SyncRoot)
            {
                ObjectDisposedException.ThrowIf(IsDisposed, this);
                return GetSnapshotCore().Length;
            }
        }
    }

    /// <summary>
    /// Looks up a value and reports presence independently from its value.
    /// </summary>
    /// <param name="key">
    /// The non-<see langword="null"/> key to find.
    /// </param>
    /// <param name="value">
    /// The retained value on success, or the default value when absent.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when an eligible entry exists; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGet(TKey key, out TValue value)
    {
        lock (SyncRoot)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            ArgumentNullException.ThrowIfNull(key);
            return TryGetCore(key, out value);
        }
    }

    /// <summary>
    /// Replaces a value without taking ownership of its disposable resources.
    /// </summary>
    /// <param name="key">
    /// The non-<see langword="null"/> key to insert or replace.
    /// </param>
    /// <param name="value">
    /// The value to retain, including default or <see langword="null"/> when supported by the value type.
    /// </param>
    public void Set(TKey key, TValue value)
    {
        lock (SyncRoot)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            ArgumentNullException.ThrowIfNull(key);
            SetCore(key, value);
        }
    }

    /// <summary>
    /// Removes the entry selected by the key comparer.
    /// </summary>
    /// <param name="key">
    /// The non-<see langword="null"/> key to remove.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when an eligible entry was removed; otherwise, <see langword="false"/>.
    /// </returns>
    public bool Remove(TKey key)
    {
        lock (SyncRoot)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            ArgumentNullException.ThrowIfNull(key);
            return RemoveCore(key);
        }
    }

    /// <summary>
    /// Releases all retained entries without disposing caller-owned keys or values.
    /// </summary>
    public void Clear()
    {
        lock (SyncRoot)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            ClearCore();
        }
    }

    /// <summary>
    /// Copies current key-value pairs without renewing sliding retention.
    /// </summary>
    /// <returns>
    /// A detached array containing eligible entries; values themselves are not copied.
    /// </returns>
    public KeyValuePair<TKey, TValue>[] GetSnapshot()
    {
        lock (SyncRoot)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return GetSnapshotCore();
        }
    }

    /// <summary>
    /// Claims this backend for one storage owner over its entire lifetime.
    /// </summary>
    internal void ClaimStorageOwnership()
    {
        lock (SyncRoot)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (_storageClaimed)
                throw new InvalidOperationException("The cache already belongs to a storage owner.");
            _storageClaimed = true;
        }
    }

    /// <summary>
    /// Closes the cache and releases its owned storage resources.
    /// </summary>
    public void Dispose()
    {
        lock (SyncRoot)
        {
            if (IsDisposed) return;
            IsDisposed = true;
            try
            {
                ClearCore();
            }
            catch
            {
                try { DisposeCore(); }
                catch { /* Preserve the primary storage cleanup failure. */ }
                throw;
            }
            DisposeCore();
        }
    }

    /// <summary>
    /// Looks up an entry synchronously while the cache synchronization gate is held.
    /// </summary>
    /// <param name="key">
    /// The non-<see langword="null"/> key to find.
    /// </param>
    /// <param name="value">
    /// The found value, or default when absent.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the entry is present; otherwise, <see langword="false"/>.
    /// </returns>
    protected abstract bool TryGetCore(TKey key, out TValue value);

    /// <summary>
    /// Inserts or replaces an entry synchronously while the cache gate is held.
    /// </summary>
    /// <param name="key">
    /// The non-<see langword="null"/> key to replace.
    /// </param>
    /// <param name="value">
    /// The value to retain without copying or disposing it.
    /// </param>
    protected abstract void SetCore(TKey key, TValue value);

    /// <summary>
    /// Removes an entry synchronously while the cache gate is held.
    /// </summary>
    /// <param name="key">
    /// The non-<see langword="null"/> key to remove.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when an eligible entry was removed; otherwise, <see langword="false"/>.
    /// </returns>
    protected abstract bool RemoveCore(TKey key);

    /// <summary>
    /// Drops all retained references synchronously while the cache gate is held.
    /// </summary>
    protected abstract void ClearCore();

    /// <summary>
    /// Copies eligible entries synchronously without renewing sliding retention.
    /// </summary>
    /// <returns>
    /// A detached entry array produced while the cache gate is held.
    /// </returns>
    protected abstract KeyValuePair<TKey, TValue>[] GetSnapshotCore();

    /// <summary>
    /// Releases additional backend resources while the closed cache gate is held.
    /// </summary>
    protected virtual void DisposeCore() { }
}
