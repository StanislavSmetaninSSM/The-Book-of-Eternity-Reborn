using BookOfEternityClient.Services;
using BookOfEternityClient.Services.Caching;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises the validation coordinator through interchangeable real cache mechanisms.
/// </summary>
public sealed class OperationValidationStorageTests
{
    /// <summary>
    /// Revalidates source versions before TTL and releases expiring storage on operation close.
    /// </summary>
    [Fact]
    public async Task MemoryRetentionNeverExtendsVersionOrOperationValidity()
    {
        var clock = new ManualCacheTimeProvider();
        var service = new DefaultOperationValidationService(() =>
            new MemoryCache<ValidationOperation.EntryKey, ValidationOperation.Entry>(
                new CacheExpirationPolicy(TimeSpan.FromSeconds(20), null, TimeSpan.FromSeconds(1)), clock));
        var dependency = new VersionedDependency();
        var rule = new OperationComputation<int>("memory-version", 1, v => v);
        var input = new IdentityOnlyInput();
        var calls = 0;
        Task<int> Compute(CancellationToken _) => Task.FromResult(++calls);
        using (var operation = service.BeginOperation())
        {
            Assert.Equal(1, await operation.GetOrComputeAsync(rule, input, [dependency], Compute));
            Assert.Equal(1, await operation.GetOrComputeAsync(rule, input, [dependency], Compute));
            dependency.Change();
            Assert.Equal(2, await operation.GetOrComputeAsync(rule, input, [dependency], Compute));
            clock.Advance(TimeSpan.FromSeconds(20));
            Assert.Equal(3, await operation.GetOrComputeAsync(rule, input, [dependency], Compute));
            Assert.Equal(1, clock.ActiveTimers);
        }
        Assert.Equal(0, clock.ActiveTimers);
        using (var next = service.BeginOperation())
            Assert.Equal(4, await next.GetOrComputeAsync(rule, input, [dependency], Compute));
        Assert.Equal(0, clock.ActiveTimers);
    }

    /// <summary>
    /// Verifies envelope identities even when a configured backend comparer merges logical keys.
    /// </summary>
    [Fact]
    public async Task BackendKeyCollisionCannotReturnAnotherInputsResult()
    {
        var service = new DefaultOperationValidationService(() =>
            new DictionaryCache<ValidationOperation.EntryKey, ValidationOperation.Entry>(new MergingComparer()));
        using var operation = service.BeginOperation();
        var rule = new OperationComputation<int>("collision", 1, v => v);
        var first = new object();
        var second = new object();
        Assert.Equal(1, await operation.GetOrComputeAsync(rule, first, [], _ => Task.FromResult(1)));
        Assert.Equal(2, await operation.GetOrComputeAsync(rule, second, [], _ => Task.FromResult(2)));
        Assert.Equal(1, await operation.GetOrComputeAsync(rule, first, [], _ => Task.FromResult(1)));
    }

    /// <summary>
    /// Selects no retention through the same production computation path.
    /// </summary>
    [Fact]
    public async Task NoCacheExecutesEveryRequestWithoutChangingResults()
    {
        var calls = 0;
        OperationValidationService service = new DefaultOperationValidationService(() =>
            new NoCache<ValidationOperation.EntryKey, ValidationOperation.Entry>());
        using var operation = service.BeginOperation();
        var computation = new OperationComputation<int>("no-cache", 1, v => v);
        var input = new object();
        for (var i = 0; i < 8; i++)
            Assert.Equal(7, await operation.GetOrComputeAsync(computation, input, [], _ => Task.FromResult(++calls > 0 ? 7 : 0)));
        Assert.Equal(8, calls);
        Assert.Equal(0, operation.CaptureState().Entries);
    }

    /// <summary>
    /// Uses inherited storage hooks outside the coordinator monitor and clears them on close.
    /// </summary>
    [Fact]
    public async Task BackendHooksRunOutsideCoordinatorMonitor()
    {
        var cache = new ObservedCache();
        var service = new DefaultOperationValidationService(() => cache);
        using var operation = service.BeginOperation();
        cache.Observe = () => Assert.False(operation.IsStateMonitorHeld);
        var computation = new OperationComputation<int>("hook", 1, v => v);
        var input = new object();
        var calls = 0;
        for (var i = 0; i < 2; i++)
            Assert.Equal(7, await operation.GetOrComputeAsync(computation, input, [], _ => { calls++; return Task.FromResult(7); }));
        Assert.Equal(1, calls);
        Assert.True(cache.Reads >= 2);
        Assert.Equal(1, operation.CaptureState().Entries);
        using (operation.ForceFresh()) Assert.Equal(0, operation.CaptureState().Entries);
        operation.Dispose();
        Assert.True(operation.CaptureState().Closed);
        Assert.Throws<ObjectDisposedException>(() => cache.Set(default, null!));
    }

    /// <summary>
    /// Refuses backend aliasing without clearing the existing operation's retained success.
    /// </summary>
    [Fact]
    public async Task DuplicateBackendRefusesWithoutClearingOriginalOwner()
    {
        var cache = new DictionaryCache<ValidationOperation.EntryKey, ValidationOperation.Entry>(new ValidationOperation.EntryKeyComparer());
        var service = new DefaultOperationValidationService(() => cache);
        using var operation = service.BeginOperation();
        var rule = new OperationComputation<int>("exclusive", 1, v => v);
        var input = new object();
        var calls = 0;
        await operation.GetOrComputeAsync(rule, input, [], _ => Task.FromResult(++calls));
        ValidationOperation? duplicate = null;
        try { Assert.Throws<InvalidOperationException>(() => duplicate = service.BeginOperation()); }
        finally { duplicate?.Dispose(); }
        Assert.Equal(1, await operation.GetOrComputeAsync(rule, input, [], _ => Task.FromResult(++calls)));
        Assert.Equal(1, calls);
    }

    /// <summary>
    /// Refuses two contexts sharing the same storage wrapper even before backend disposal.
    /// </summary>
    [Fact]
    public void DuplicateOperationStorageRefuses()
    {
        var storage = new ServiceCacheStorage<ValidationOperation.EntryKey, ValidationOperation.Entry>(
            new DictionaryCache<ValidationOperation.EntryKey, ValidationOperation.Entry>(new ValidationOperation.EntryKeyComparer()));
        using var first = new ValidationOperation(storage);
        ValidationOperation? duplicate = null;
        try { Assert.Throws<InvalidOperationException>(() => duplicate = new ValidationOperation(storage)); }
        finally { duplicate?.Dispose(); }
        Assert.False(first.CaptureState().Closed);
    }

    /// <summary>
    /// Preserves the primary backend error even if clearing also fails and closes the context.
    /// </summary>
    [Fact]
    public async Task BackendFaultClosesWhenInvalidationFailsWithoutMaskingPrimaryError()
    {
        var primary = new IOException("lookup failure");
        var cache = new ObservedCache();
        var service = new DefaultOperationValidationService(() => cache);
        using var operation = service.BeginOperation();
        cache.BeforeRead = () => throw primary;
        cache.BeforeClear = () => throw new InvalidOperationException("secondary clear failure");
        var rule = new OperationComputation<int>("fault", 1, v => v);
        var error = await Assert.ThrowsAsync<IOException>(() => operation.GetOrComputeAsync(rule, new object(), [], _ => Task.FromResult(1)));
        Assert.Same(primary, error);
        Assert.True(operation.CaptureState().Closed);
        Assert.Equal(0, operation.CaptureState().Entries);
    }

    /// <summary>
    /// Supplies a nonreused memory token without touching a filesystem.
    /// </summary>
    private sealed class VersionedDependency : IOperationDependency
    {
        private object _token = new();
        /// <summary>
        /// Retires the previous version before any subsequent cache lookup.
        /// </summary>
        internal void Change() => _token = new();
        /// <inheritdoc />
        public bool TryCaptureVersion(out object? version) { version = _token; return true; }
    }

    /// <summary>
    /// Refuses content-based equality so a backend must preserve the coordinator's identity keys.
    /// </summary>
    private sealed class IdentityOnlyInput
    {
        /// <inheritdoc />
        public override bool Equals(object? obj) => throw new InvalidOperationException("Input equality must not be used.");
        /// <inheritdoc />
        public override int GetHashCode() => throw new InvalidOperationException("Input hashing must not be used.");
    }

    /// <summary>
    /// Deliberately collides backend keys to verify the coordinator's independent envelope guard.
    /// </summary>
    private sealed class MergingComparer : IEqualityComparer<ValidationOperation.EntryKey>
    {
        /// <inheritdoc />
        public bool Equals(ValidationOperation.EntryKey x, ValidationOperation.EntryKey y) => true;
        /// <inheritdoc />
        public int GetHashCode(ValidationOperation.EntryKey obj) => 0;
    }

    /// <summary>
    /// Provides observable inherited hooks while retaining actual entries independently.
    /// </summary>
    private sealed class ObservedCache : ServiceCache<ValidationOperation.EntryKey, ValidationOperation.Entry>
    {
        private readonly Dictionary<ValidationOperation.EntryKey, ValidationOperation.Entry> _values = new(new ValidationOperation.EntryKeyComparer());
        /// <summary>
        /// Observes whether storage was called under the coordinator's monitor.
        /// </summary>
        internal Action? Observe;
        /// <summary>
        /// Optionally fails a physical cache lookup.
        /// </summary>
        internal Action? BeforeRead;
        /// <summary>
        /// Optionally fails actual retained-entry clearing.
        /// </summary>
        internal Action? BeforeClear;
        /// <summary>
        /// Counts actual backend lookups.
        /// </summary>
        internal int Reads;
        /// <inheritdoc />
        protected override bool TryGetCore(ValidationOperation.EntryKey key, out ValidationOperation.Entry value)
        { Observe?.Invoke(); Reads++; BeforeRead?.Invoke(); return _values.TryGetValue(key, out value!); }
        /// <inheritdoc />
        protected override void SetCore(ValidationOperation.EntryKey key, ValidationOperation.Entry value)
        { Observe?.Invoke(); _values[key] = value; }
        /// <inheritdoc />
        protected override bool RemoveCore(ValidationOperation.EntryKey key)
        { Observe?.Invoke(); return _values.Remove(key); }
        /// <inheritdoc />
        protected override void ClearCore()
        { Observe?.Invoke(); BeforeClear?.Invoke(); _values.Clear(); }
        /// <inheritdoc />
        protected override KeyValuePair<ValidationOperation.EntryKey, ValidationOperation.Entry>[] GetSnapshotCore()
        { Observe?.Invoke(); return _values.ToArray(); }
    }
}
