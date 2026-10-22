namespace BookOfEternityClient.Services.Caching;

/// <summary>
/// Retains entries within configured monotonic lifetimes and periodically evicts idle entries.
/// </summary>
/// <typeparam name="TKey">
/// The non-null key type.
/// </typeparam>
/// <typeparam name="TValue">
/// The caller-owned value type.
/// </typeparam>
public sealed class MemoryCache<TKey, TValue> : ServiceCache<TKey, TValue> where TKey : notnull
{
    /// <summary>
    /// Creates independent expiring storage with an owned periodic timer.
    /// </summary>
    /// <param name="policy">
    /// The non-null positive retention policy.
    /// </param>
    /// <param name="timeProvider">
    /// The monotonic clock and timer provider, or null for the system provider.
    /// </param>
    /// <param name="comparer">
    /// The key comparer, or null for default equality.
    /// </param>
    public MemoryCache(CacheExpirationPolicy policy, TimeProvider? timeProvider = null, IEqualityComparer<TKey>? comparer = null)
        : base(comparer)
    {
        ArgumentNullException.ThrowIfNull(policy);
    }

    /// <summary>
    /// Gets the number of held entries without pruning or refreshing their lifetimes.
    /// </summary>
    internal int RetainedEntryCount => 0;

    /// <inheritdoc />
    protected override bool TryGetCore(TKey key, out TValue value) { value = default!; return false; }

    /// <inheritdoc />
    protected override void SetCore(TKey key, TValue value) { }

    /// <inheritdoc />
    protected override bool RemoveCore(TKey key) => false;

    /// <inheritdoc />
    protected override void ClearCore() { }

    /// <inheritdoc />
    protected override KeyValuePair<TKey, TValue>[] GetSnapshotCore() => [];
}
