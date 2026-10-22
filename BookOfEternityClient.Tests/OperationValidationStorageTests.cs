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
