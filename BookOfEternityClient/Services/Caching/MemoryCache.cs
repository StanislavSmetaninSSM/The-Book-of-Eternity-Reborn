namespace BookOfEternityClient.Services.Caching;

/// <summary>
/// Retains entries within configured monotonic lifetimes and periodically evicts idle entries.
/// </summary>
/// <typeparam name="TKey">
/// The non-<see langword="null"/> key type.
/// </typeparam>
/// <typeparam name="TValue">
/// The caller-owned value type.
/// </typeparam>
public sealed class MemoryCache<TKey, TValue> : ServiceCache<TKey, TValue> where TKey : notnull
{
    private readonly CacheExpirationPolicy _policy;
    private readonly TimeProvider _timeProvider;
    private readonly ITimer _timer;
    private Dictionary<TKey, CacheEntry> _values;

    /// <summary>
    /// Creates independent expiring storage with an owned periodic timer.
    /// </summary>
    /// <param name="policy">
    /// The non-<see langword="null"/> positive retention policy.
    /// </param>
    /// <param name="timeProvider">
    /// The monotonic clock and timer provider, or <see langword="null"/> for the system provider.
    /// </param>
    /// <param name="comparer">
    /// The key comparer, or <see langword="null"/> for default equality.
    /// </param>
    public MemoryCache(CacheExpirationPolicy policy, TimeProvider? timeProvider = null, IEqualityComparer<TKey>? comparer = null)
        : base(comparer)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _policy = policy;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _values = new Dictionary<TKey, CacheEntry>(Comparer);
        // Scheduling granularity/range does not change timestamp-based entry lifetimes.
        var interval = TimeSpan.FromMilliseconds(Math.Clamp(Math.Ceiling(policy.SweepInterval.TotalMilliseconds),
            1d, 4_294_967_294d));
        _timer = _timeProvider.CreateTimer(static state => ((MemoryCache<TKey, TValue>)state!).Sweep(),
            this, interval, interval);
    }

    /// <summary>
    /// Gets the number of held entries without pruning or refreshing their lifetimes.
    /// </summary>
    internal int RetainedEntryCount
    {
        get
        {
            lock (SyncRoot) return _values.Count;
        }
    }

    /// <inheritdoc />
    protected override bool TryGetCore(TKey key, out TValue value)
    {
        var now = _timeProvider.GetTimestamp();
        if (_values.TryGetValue(key, out var entry))
        {
            if (!IsExpired(entry, now))
            {
                entry.LastAccessTimestamp = now;
                value = entry.Value;
                return true;
            }
            _values.Remove(key);
        }
        value = default!;
        return false;
    }

    /// <inheritdoc />
    protected override void SetCore(TKey key, TValue value) =>
        _values[key] = new CacheEntry(value, _timeProvider.GetTimestamp());

    /// <inheritdoc />
    protected override bool RemoveCore(TKey key) =>
        _values.Remove(key, out var entry) && !IsExpired(entry, _timeProvider.GetTimestamp());

    /// <inheritdoc />
    protected override void ClearCore() => _values = new Dictionary<TKey, CacheEntry>(Comparer);

    /// <inheritdoc />
    protected override KeyValuePair<TKey, TValue>[] GetSnapshotCore()
    {
        RemoveExpired(_timeProvider.GetTimestamp());
        return _values.Select(pair => new KeyValuePair<TKey, TValue>(pair.Key, pair.Value.Value)).ToArray();
    }

    /// <inheritdoc />
    protected override void DisposeCore() => _timer.Dispose();

    /// <summary>
    /// Determines whether either configured lifetime has reached its exact boundary.
    /// </summary>
    /// <param name="entry">
    /// The retained value and its original and last-read timestamps.
    /// </param>
    /// <param name="now">
    /// The current timestamp from the configured monotonic clock.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when either configured lifetime has elapsed; otherwise, <see langword="false"/>.
    /// </returns>
    private bool IsExpired(CacheEntry entry, long now) =>
        (_policy.AbsoluteLifetime is { } absolute && _timeProvider.GetElapsedTime(entry.CreatedTimestamp, now) >= absolute)
        || (_policy.SlidingLifetime is { } sliding && _timeProvider.GetElapsedTime(entry.LastAccessTimestamp, now) >= sliding);

    /// <summary>
    /// Releases expired references without modifying surviving entries' access timestamps.
    /// </summary>
    /// <param name="now">
    /// The monotonic timestamp shared by this expiration pass.
    /// </param>
    private void RemoveExpired(long now)
    {
        foreach (var key in _values.Where(pair => IsExpired(pair.Value, now)).Select(pair => pair.Key).ToArray())
            _values.Remove(key);
    }

    /// <summary>
    /// Evicts idle entries while sharing the same gate as caller-initiated operations.
    /// </summary>
    private void Sweep()
    {
        lock (SyncRoot)
        {
            if (IsDisposed) return;
            RemoveExpired(_timeProvider.GetTimestamp());
        }
    }

    /// <summary>
    /// Holds one caller-owned value and the timestamps defining its retention lifetime.
    /// </summary>
    private sealed class CacheEntry
    {
        /// <summary>
        /// Creates an entry whose absolute and sliding lifetimes both start now.
        /// </summary>
        /// <param name="value">
        /// The caller-owned value, including its default or <see langword="null"/> when supported by its type.
        /// </param>
        /// <param name="createdTimestamp">
        /// The insertion timestamp from the configured monotonic clock.
        /// </param>
        internal CacheEntry(TValue value, long createdTimestamp)
        {
            Value = value;
            CreatedTimestamp = createdTimestamp;
            LastAccessTimestamp = createdTimestamp;
        }

        /// <summary>
        /// Gets the retained value without taking ownership of its disposable resources.
        /// </summary>
        internal TValue Value { get; }

        /// <summary>
        /// Gets the insertion timestamp, unchanged by successful lookups.
        /// </summary>
        internal long CreatedTimestamp { get; }

        /// <summary>
        /// Gets or sets the timestamp of the last successful lookup or insertion.
        /// </summary>
        internal long LastAccessTimestamp { get; set; }
    }
}
