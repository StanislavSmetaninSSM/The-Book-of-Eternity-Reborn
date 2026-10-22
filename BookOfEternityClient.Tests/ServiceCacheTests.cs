using BookOfEternityClient.Services.Caching;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises interchangeable cache mechanisms using independent in-memory state.
/// </summary>
public sealed class ServiceCacheTests
{
    /// <summary>
    /// Distinguishes cache absence from stored default values.
    /// </summary>
    /// <param name="backend">
    /// The retaining mechanism selected for this independent case.
    /// </param>
    [Theory]
    [InlineData("dictionary")]
    [InlineData("memory")]
    public void RetainingBackendsPreserveFalseZeroAndNull(string backend)
    {
        using ServiceCache<string, object?> cache = backend == "dictionary"
            ? new DictionaryCache<string, object?>()
            : new MemoryCache<string, object?>(Policy(10, null), new ManualCacheTimeProvider());
        Assert.False(cache.TryGet("missing", out _));
        cache.Set("false", false);
        cache.Set("zero", 0);
        cache.Set("null", null);
        Assert.True(cache.TryGet("false", out var boolean));
        Assert.Equal(false, boolean);
        Assert.True(cache.TryGet("zero", out var number));
        Assert.Equal(0, number);
        Assert.True(cache.TryGet("null", out var nullable));
        Assert.Null(nullable);
        Assert.Equal(3, cache.Count);
    }

    /// <summary>
    /// Applies the requested comparer to replacement, lookup and deletion.
    /// </summary>
    [Fact]
    public void DictionaryUsesComparerAndReturnsDetachedSnapshots()
    {
        using var cache = new DictionaryCache<string, int>(StringComparer.OrdinalIgnoreCase);
        cache.Set("Name", 1);
        cache.Set("NAME", 2);
        Assert.Equal(1, cache.Count);
        Assert.True(cache.TryGet("name", out var value));
        Assert.Equal(2, value);
        var snapshot = cache.GetSnapshot();
        snapshot[0] = new("other", 9);
        Assert.True(cache.TryGet("name", out value));
        Assert.Equal(2, value);
        Assert.True(cache.Remove("nAmE"));
        Assert.False(cache.Remove("name"));
        cache.Set("a", 3);
        cache.Clear();
        Assert.Empty(cache.GetSnapshot());
        Assert.Equal(0, cache.Count);
    }

    /// <summary>
    /// Gives callers the same lifecycle surface without retaining submitted values.
    /// </summary>
    [Fact]
    public void NoCacheNeverRetainsValues()
    {
        using var cache = new NoCache<string, int>();
        cache.Set("a", 1);
        Assert.False(cache.TryGet("a", out _));
        Assert.False(cache.Remove("a"));
        Assert.Equal(0, cache.Count);
        Assert.Empty(cache.GetSnapshot());
        cache.Clear();
    }

    /// <summary>
    /// Routes public operations through inherited synchronous storage hooks.
    /// </summary>
    [Fact]
    public void DerivedCacheReceivesEveryStorageOperation()
    {
        var cache = new RecordingCache();
        cache.Set("a", 4);
        Assert.True(cache.TryGet("a", out var value));
        Assert.Equal(4, value);
        Assert.Single(cache.GetSnapshot());
        Assert.Equal(1, cache.Count);
        Assert.True(cache.Remove("a"));
        cache.Clear();
        cache.Dispose();
        cache.Dispose();
        Assert.Contains("set", cache.Calls);
        Assert.Contains("get", cache.Calls);
        Assert.Contains("remove", cache.Calls);
        Assert.Contains("clear", cache.Calls);
        Assert.Equal(2, cache.Calls.Count(call => call == "snapshot"));
        Assert.Equal(1, cache.Calls.Count(call => call == "dispose"));
    }

    /// <summary>
    /// Rejects every public storage operation after the owner closes the backend.
    /// </summary>
    /// <param name="backend">
    /// The dictionary, disabled or expiring backend to close.
    /// </param>
    [Theory]
    [InlineData("dictionary")]
    [InlineData("none")]
    [InlineData("memory")]
    public void DisposedBackendsRefusePublicOperations(string backend)
    {
        using var cache = CreateCache(backend);
        cache.Set("a", 1);
        cache.Dispose();
        Assert.Throws<ObjectDisposedException>(() => cache.TryGet("a", out _));
        Assert.Throws<ObjectDisposedException>(() => cache.Set("a", 2));
        Assert.Throws<ObjectDisposedException>(() => cache.Remove("a"));
        Assert.Throws<ObjectDisposedException>(() => cache.Clear());
        Assert.Throws<ObjectDisposedException>(() => cache.GetSnapshot());
        Assert.Throws<ObjectDisposedException>(() => _ = cache.Count);
    }

    /// <summary>
    /// Rejects a second storage owner without disturbing the first owner's data.
    /// </summary>
    [Fact]
    public void StorageOwnershipIsClaimedOnlyOnce()
    {
        using var cache = new DictionaryCache<string, int>();
        cache.ClaimStorageOwnership();
        cache.Set("a", 4);
        Assert.Throws<InvalidOperationException>(cache.ClaimStorageOwnership);
        Assert.True(cache.TryGet("a", out var value));
        Assert.Equal(4, value);
        cache.Dispose();
        Assert.Throws<ObjectDisposedException>(cache.ClaimStorageOwnership);
    }

    /// <summary>
    /// Rejects lifetimes and sweep intervals that cannot define positive retention.
    /// </summary>
    [Fact]
    public void ExpirationPolicyRequiresPositiveConfiguredDurations()
    {
        var second = TimeSpan.FromSeconds(1);
        Assert.Throws<ArgumentException>(() => new CacheExpirationPolicy(null, null, second));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CacheExpirationPolicy(TimeSpan.Zero, null, second));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CacheExpirationPolicy(null, -second, second));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CacheExpirationPolicy(second, null, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CacheExpirationPolicy(second, null, -second));
        var policy = new CacheExpirationPolicy(second, second + second, second);
        Assert.Equal(second, policy.AbsoluteLifetime);
        Assert.Equal(second + second, policy.SlidingLifetime);
        Assert.Equal(second, policy.SweepInterval);
    }

    /// <summary>
    /// Expires an absolute entry at the exact boundary even after a successful read.
    /// </summary>
    [Fact]
    public void AbsoluteLifetimeDoesNotSlide()
    {
        var clock = new ManualCacheTimeProvider();
        using var cache = new MemoryCache<string, int>(Policy(10, null), clock);
        cache.Set("a", 7);
        clock.Advance(TimeSpan.FromSeconds(9));
        Assert.True(cache.TryGet("a", out _));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.False(cache.TryGet("a", out _));
        Assert.Equal(0, cache.Count);
    }

    /// <summary>
    /// Renews sliding retention only when a lookup successfully finds the entry.
    /// </summary>
    [Fact]
    public void SuccessfulLookupRenewsSlidingLifetime()
    {
        var clock = new ManualCacheTimeProvider();
        using var cache = new MemoryCache<string, int>(Policy(null, 10), clock);
        cache.Set("a", 7);
        clock.Advance(TimeSpan.FromSeconds(8));
        Assert.True(cache.TryGet("a", out _));
        clock.Advance(TimeSpan.FromSeconds(8));
        Assert.True(cache.TryGet("a", out _));
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.False(cache.TryGet("a", out _));
    }

    /// <summary>
    /// Bounds sliding renewals by the original absolute expiration deadline.
    /// </summary>
    [Fact]
    public void CombinedLifetimeUsesTheEarlierDeadline()
    {
        var clock = new ManualCacheTimeProvider();
        using var cache = new MemoryCache<string, int>(Policy(12, 5), clock);
        cache.Set("active", 1);
        cache.Set("idle", 2);
        clock.Advance(TimeSpan.FromSeconds(4));
        Assert.True(cache.TryGet("active", out _));
        clock.Advance(TimeSpan.FromSeconds(4));
        Assert.False(cache.TryGet("idle", out _));
        Assert.True(cache.TryGet("active", out _));
        clock.Advance(TimeSpan.FromSeconds(4));
        Assert.False(cache.TryGet("active", out _));
    }

    /// <summary>
    /// Starts both lifetimes again when a comparer-equivalent key is replaced.
    /// </summary>
    [Fact]
    public void ReplacementResetsLifetimesAndUsesComparer()
    {
        var clock = new ManualCacheTimeProvider();
        using var cache = new MemoryCache<string, int>(Policy(10, 6), clock, StringComparer.OrdinalIgnoreCase);
        cache.Set("Key", 1);
        clock.Advance(TimeSpan.FromSeconds(5));
        cache.Set("KEY", 2);
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.True(cache.TryGet("key", out var value));
        Assert.Equal(2, value);
        Assert.Equal(1, cache.Count);
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.False(cache.TryGet("key", out _));
    }

    /// <summary>
    /// Prunes expired entries without extending retention during inspection.
    /// </summary>
    [Fact]
    public void SnapshotAndCountDoNotRenewSlidingLifetime()
    {
        var clock = new ManualCacheTimeProvider();
        using var cache = new MemoryCache<string, int>(Policy(null, 10), clock);
        cache.Set("a", 1);
        clock.Advance(TimeSpan.FromSeconds(9));
        Assert.Single(cache.GetSnapshot());
        Assert.Equal(1, cache.Count);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Empty(cache.GetSnapshot());
        Assert.Equal(0, cache.Count);
    }

    /// <summary>
    /// Evicts idle references through the owned timer and cancels it on disposal.
    /// </summary>
    [Fact]
    public void IdleTimerEvictsWithoutAReadAndDisposalStopsTheTimer()
    {
        var clock = new ManualCacheTimeProvider();
        var cache = new MemoryCache<string, object>(Policy(2, null, 1), clock);
        cache.Set("retained", new object());
        Assert.Equal(1, clock.ActiveTimers);
        Assert.Equal(1, cache.RetainedEntryCount);
        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.True(clock.FiredCallbacks > 0);
        Assert.Equal(0, cache.RetainedEntryCount);
        Assert.Empty(cache.GetSnapshot());
        cache.Set("new", new object());
        cache.Dispose();
        Assert.Equal(0, clock.ActiveTimers);
        Assert.Equal(0, cache.RetainedEntryCount);
        var callbacks = clock.FiredCallbacks;
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(callbacks, clock.FiredCallbacks);
    }

    /// <summary>
    /// Returns a backend with a fresh owner for lifecycle contract cases.
    /// </summary>
    /// <param name="backend">
    /// The mechanism name.
    /// </param>
    /// <returns>
    /// A cache whose resources belong to the current test.
    /// </returns>
    private static ServiceCache<string, int> CreateCache(string backend) => backend switch
    {
        "dictionary" => new DictionaryCache<string, int>(),
        "none" => new NoCache<string, int>(),
        _ => new MemoryCache<string, int>(Policy(10, null), new ManualCacheTimeProvider())
    };

    /// <summary>
    /// Creates a policy from seconds without involving wall-clock time.
    /// </summary>
    /// <param name="absolute">
    /// The absolute lifetime in seconds, or no absolute limit.
    /// </param>
    /// <param name="sliding">
    /// The sliding lifetime in seconds, or no sliding limit.
    /// </param>
    /// <param name="sweep">
    /// The periodic sweep interval in seconds.
    /// </param>
    /// <returns>
    /// The policy used by one independent test cache.
    /// </returns>
    private static CacheExpirationPolicy Policy(int? absolute, int? sliding, int sweep = 60) =>
        new(absolute.HasValue ? TimeSpan.FromSeconds(absolute.Value) : null,
            sliding.HasValue ? TimeSpan.FromSeconds(sliding.Value) : null, TimeSpan.FromSeconds(sweep));

    /// <summary>
    /// Records inherited hook dispatch while retaining values for the extension test.
    /// </summary>
    private sealed class RecordingCache : ServiceCache<string, int>
    {
        private readonly Dictionary<string, int> _values = new();

        /// <summary>
        /// Gets the sequence of storage hooks invoked by the public surface.
        /// </summary>
        internal List<string> Calls { get; } = [];

        /// <inheritdoc />
        protected override bool TryGetCore(string key, out int value)
        {
            Calls.Add("get");
            return _values.TryGetValue(key, out value);
        }

        /// <inheritdoc />
        protected override void SetCore(string key, int value)
        {
            Calls.Add("set");
            _values[key] = value;
        }

        /// <inheritdoc />
        protected override bool RemoveCore(string key)
        {
            Calls.Add("remove");
            return _values.Remove(key);
        }

        /// <inheritdoc />
        protected override void ClearCore()
        {
            Calls.Add("clear");
            _values.Clear();
        }

        /// <inheritdoc />
        protected override KeyValuePair<string, int>[] GetSnapshotCore()
        {
            Calls.Add("snapshot");
            return _values.ToArray();
        }

        /// <inheritdoc />
        protected override void DisposeCore() => Calls.Add("dispose");
    }
}

/// <summary>
/// Advances monotonic time and owned timers synchronously for memory-only fixtures.
/// </summary>
internal sealed class ManualCacheTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];
    private long _timestamp;

    /// <inheritdoc />
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc />
    public override long GetTimestamp() => _timestamp;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_timestamp);

    /// <summary>
    /// Gets the number of timers that have not been disposed.
    /// </summary>
    internal int ActiveTimers => _timers.Count(timer => !timer.IsDisposed);

    /// <summary>
    /// Gets the number of timer callbacks executed during explicit advances.
    /// </summary>
    internal int FiredCallbacks { get; private set; }

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var timer = new ManualTimer(this, callback, state);
        _timers.Add(timer);
        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>
    /// Advances time and invokes every due timer in timestamp order.
    /// </summary>
    /// <param name="elapsed">
    /// A nonnegative amount of monotonic time to advance.
    /// </param>
    internal void Advance(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        var target = checked(_timestamp + elapsed.Ticks);
        while (_timers.Where(timer => !timer.IsDisposed && timer.NextDue <= target)
                   .OrderBy(timer => timer.NextDue).FirstOrDefault() is { } due)
        {
            _timestamp = due.NextDue;
            due.ScheduleNext();
            FiredCallbacks++;
            due.Invoke();
        }
        _timestamp = target;
    }

    /// <summary>
    /// Holds one deterministic callback and its independently disposable schedule.
    /// </summary>
    private sealed class ManualTimer : ITimer
    {
        private readonly ManualCacheTimeProvider _owner;
        private readonly TimerCallback _callback;
        private readonly object? _state;
        private long _period;

        /// <summary>
        /// Creates an initially unscheduled timer.
        /// </summary>
        /// <param name="owner">
        /// The clock supplying the current monotonic timestamp.
        /// </param>
        /// <param name="callback">
        /// The callback to invoke when due.
        /// </param>
        /// <param name="state">
        /// The optional callback payload, preserved unchanged.
        /// </param>
        internal ManualTimer(ManualCacheTimeProvider owner, TimerCallback callback, object? state)
        {
            _owner = owner;
            _callback = callback;
            _state = state;
        }

        /// <summary>
        /// Gets whether disposal has permanently disabled this timer.
        /// </summary>
        internal bool IsDisposed { get; private set; }

        /// <summary>
        /// Gets the next due timestamp, or the maximum value when disabled.
        /// </summary>
        internal long NextDue { get; private set; } = long.MaxValue;

        /// <inheritdoc />
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (IsDisposed) return false;
            NextDue = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : checked(_owner._timestamp + dueTime.Ticks);
            _period = period == Timeout.InfiniteTimeSpan ? 0 : period.Ticks;
            return true;
        }

        /// <summary>
        /// Advances the periodic schedule before user callback execution.
        /// </summary>
        internal void ScheduleNext() => NextDue = _period > 0 ? checked(NextDue + _period) : long.MaxValue;

        /// <summary>
        /// Invokes the callback outside any fixture monitor.
        /// </summary>
        internal void Invoke() => _callback(_state);

        /// <inheritdoc />
        public void Dispose()
        {
            IsDisposed = true;
            NextDue = long.MaxValue;
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
