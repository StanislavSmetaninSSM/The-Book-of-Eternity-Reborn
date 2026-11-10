namespace BookOfEternityClient.Services.Caching;

/// <summary>
/// Retains comparer-selected entries until explicitly removed or the owner closes the cache.
/// </summary>
/// <typeparam name="TKey">
/// The non-<see langword="null"/> key type.
/// </typeparam>
/// <typeparam name="TValue">
/// The caller-owned value type.
/// </typeparam>
public sealed class DictionaryCache<TKey, TValue> : ServiceCache<TKey, TValue> where TKey : notnull
{
    private Dictionary<TKey, TValue> _values;

    /// <summary>
    /// Creates independently owned dictionary storage.
    /// </summary>
    /// <param name="comparer">
    /// The key comparer, or <see langword="null"/> for default equality.
    /// </param>
    public DictionaryCache(IEqualityComparer<TKey>? comparer = null) : base(comparer)
    {
        _values = new Dictionary<TKey, TValue>(Comparer);
    }

    /// <inheritdoc />
    protected override bool TryGetCore(TKey key, out TValue value) => _values.TryGetValue(key, out value!);

    /// <inheritdoc />
    protected override void SetCore(TKey key, TValue value) => _values[key] = value;

    /// <inheritdoc />
    protected override bool RemoveCore(TKey key) => _values.Remove(key);

    /// <inheritdoc />
    protected override void ClearCore() => _values = new Dictionary<TKey, TValue>(Comparer);

    /// <inheritdoc />
    protected override KeyValuePair<TKey, TValue>[] GetSnapshotCore() => _values.ToArray();
}
