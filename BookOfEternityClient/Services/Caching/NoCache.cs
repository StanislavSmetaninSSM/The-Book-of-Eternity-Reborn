namespace BookOfEternityClient.Services.Caching;

/// <summary>
/// Provides the cache lifecycle contract while retaining no submitted entries.
/// </summary>
/// <typeparam name="TKey">
/// The non-null key type.
/// </typeparam>
/// <typeparam name="TValue">
/// The caller-owned value type.
/// </typeparam>
public sealed class NoCache<TKey, TValue> : ServiceCache<TKey, TValue> where TKey : notnull
{
    /// <summary>
    /// Creates a backend that always reports cache misses.
    /// </summary>
    /// <param name="comparer">
    /// The key comparer, or <see langword="null"/> for default equality.
    /// </param>
    public NoCache(IEqualityComparer<TKey>? comparer = null) : base(comparer) { }

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
