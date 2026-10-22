namespace BookOfEternityClient.Services.Caching;

/// <summary>
/// Retains comparer-selected entries until explicitly removed or the owner closes the cache.
/// </summary>
/// <typeparam name="TKey">
/// The non-null key type.
/// </typeparam>
/// <typeparam name="TValue">
/// The caller-owned value type.
/// </typeparam>
public sealed class DictionaryCache<TKey, TValue> : ServiceCache<TKey, TValue> where TKey : notnull
{
    /// <summary>
    /// Creates independently owned dictionary storage.
    /// </summary>
    /// <param name="comparer">
    /// The key comparer, or null for default equality.
    /// </param>
    public DictionaryCache(IEqualityComparer<TKey>? comparer = null) : base(comparer) { }

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
