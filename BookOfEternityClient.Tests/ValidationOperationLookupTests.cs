using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises noncomputing lookup against the real operation service and its ownership protocol.
/// </summary>
public sealed class ValidationOperationLookupTests
{
    /// <summary>
    /// Keeps misses empty and observes a later real computation without another source call.
    /// </summary>
    /// <returns>
    /// Completion after one source supplies a result and all subsequent lookups use it.
    /// </returns>
    [Fact]
    public async Task MissDoesNotComputeAndLaterHitUsesTheExistingEntry()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<int>("lookup.value", 1, value => value);
        var key = new object();
        Assert.False((await operation.TryGetAsync(rule, key, [])).Found);
        Assert.Equal(new ValidationOperationState(0, 0, false), operation.CaptureState());
        var sources = 0;
        Assert.Equal(19, await operation.GetOrComputeAsync(rule, key, [], _ => { sources++; return Task.FromResult(19); }));
        for (var i = 0; i < 8; i++)
        {
            var hit = await operation.TryGetAsync(rule, key, []);
            Assert.True(hit.Found);
            Assert.Equal(19, hit.Value);
        }
        Assert.Equal(1, sources);
    }

    /// <summary>
    /// Distinguishes stored defaults from missing entries.
    /// </summary>
    /// <param name="value">
    /// Literal immutable value to retain, including null, false and zero.
    /// </param>
    /// <returns>
    /// Completion after lookup reports presence independently of the stored value.
    /// </returns>
    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(0)]
    public async Task NullFalseAndZeroArePresent(object? value)
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<object?>("lookup.defaults", 1, item => item);
        var key = new object();
        await operation.GetOrComputeAsync(rule, key, [], _ => Task.FromResult(value));
        var hit = await operation.TryGetAsync(rule, key, []);
        Assert.True(hit.Found);
        Assert.Equal(value, hit.Value);
    }

    /// <summary>
    /// Detaches every selected mutable value while leaving copy callbacks outside the service monitor.
    /// </summary>
    /// <returns>
    /// Completion after mutations of returned buffers cannot alter the stored bytes.
    /// </returns>
    [Fact]
    public async Task LookupCopiesOutsideTheCoordinatorMonitor()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<byte[]>("lookup.copy", 1, bytes =>
        {
            Assert.False(operation.IsStateMonitorHeld);
            return (byte[])bytes.Clone();
        });
        var key = new object();
        var first = await operation.GetOrComputeAsync(rule, key, [], _ => Task.FromResult(new byte[] { 7, 11 }));
        first[0] = 88;
        var hit = await operation.TryGetAsync(rule, key, []);
        Assert.True(hit.Found);
        Assert.Equal(new byte[] { 7, 11 }, hit.Value);
        hit.Value[1] = 99;
        Assert.Equal(new byte[] { 7, 11 }, (await operation.TryGetAsync(rule, key, [])).Value);
    }

    /// <summary>
    /// Refuses stale versions and different declarations without invoking a replacement source.
    /// </summary>
    /// <returns>
    /// Completion after a later actual computation establishes the new version.
    /// </returns>
    [Fact]
    public async Task ChangedVersionAndDeclarationAreMisses()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<int>("lookup.version", 1, value => value);
        var key = new object();
        var dependency = new Dependency();
        await operation.GetOrComputeAsync(rule, key, [dependency], _ => Task.FromResult(7));
        Assert.False((await operation.TryGetAsync(rule, key, [new Dependency()])).Found);
        dependency.Advance();
        Assert.False((await operation.TryGetAsync(rule, key, [dependency])).Found);
        Assert.Equal(13, await operation.GetOrComputeAsync(rule, key, [dependency], _ => Task.FromResult(13)));
        Assert.Equal(13, (await operation.TryGetAsync(rule, key, [dependency])).Value);
    }

    /// <summary>
    /// Registers a selected child's dependencies on its computing parent.
    /// </summary>
    /// <returns>
    /// Completion after changed child DATA makes the previously computed parent ineligible.
    /// </returns>
    [Fact]
    public async Task ChildHitPropagatesDependenciesToParent()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var child = new OperationComputation<int>("lookup.child", 1, value => value);
        var parent = new OperationComputation<int>("lookup.parent", 1, value => value);
        var childKey = new object(); var parentKey = new object(); var dependency = new Dependency();
        await operation.GetOrComputeAsync(child, childKey, [dependency], _ => Task.FromResult(5));
        Assert.Equal(10, await operation.GetOrComputeAsync(parent, parentKey, [], async _ =>
        {
            var hit = await operation.TryGetAsync(child, childKey, [dependency]);
            Assert.True(hit.Found);
            return hit.Value * 2;
        }));
        dependency.Advance();
        Assert.False((await operation.TryGetAsync(parent, parentKey, [])).Found);
    }

    /// <summary>
    /// Clears prior values through the original fresh scope and never restores them afterward.
    /// </summary>
    /// <returns>
    /// Completion after both the fresh interval and its successor observe a miss.
    /// </returns>
    [Fact]
    public async Task FreshScopeNeverExposesPriorEntries()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<int>("lookup.fresh", 1, value => value); var key = new object();
        await operation.GetOrComputeAsync(rule, key, [], _ => Task.FromResult(7));
        using (operation.ForceFresh()) Assert.False((await operation.TryGetAsync(rule, key, [])).Found);
        Assert.False((await operation.TryGetAsync(rule, key, [])).Found);
    }

    /// <summary>
    /// Refuses cancellation and disposal rather than turning them into ordinary cache misses.
    /// </summary>
    /// <returns>
    /// Completion after both refusals retain their original exception categories.
    /// </returns>
    [Fact]
    public async Task CancelledAndClosedLookupRefuse()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<int>("lookup.close", 1, value => value); var key = new object();
        await operation.GetOrComputeAsync(rule, key, [], _ => Task.FromResult(7));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.TryGetAsync(rule, key, [], new CancellationToken(true)));
        operation.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => operation.TryGetAsync(rule, key, []));
    }

    /// <summary>
    /// Refuses a sibling lookup while another source computation remains pending.
    /// </summary>
    /// <returns>
    /// Completion after the source and its refusal are both settled before disposal.
    /// </returns>
    [Fact]
    public async Task PendingSourceExcludesAnotherLookup()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<int>("lookup.overlap", 1, value => value);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = operation.GetOrComputeAsync(rule, new object(), [], async _ => { await release.Task; return 7; });
        Exception? refusal;
        try { refusal = await Record.ExceptionAsync(() => operation.TryGetAsync(rule, new object(), [])); }
        finally { release.TrySetResult(); await Record.ExceptionAsync(async () => await first); }
        Assert.IsType<InvalidOperationException>(refusal);
        Assert.Equal(0, operation.CaptureState().Entries);
    }

    /// <summary>
    /// Invalidates selected values when the registered copy policy fails.
    /// </summary>
    /// <returns>
    /// Completion after the original error propagates and no payload remains reusable.
    /// </returns>
    [Fact]
    public async Task CopyFailureRetiresCachedValues()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var fail = false;
        var rule = new OperationComputation<int>("lookup.copy-failure", 1, value => fail ? throw new IOException("owned copy failure") : value);
        var key = new object(); await operation.GetOrComputeAsync(rule, key, [], _ => Task.FromResult(7));
        fail = true;
        await Assert.ThrowsAsync<IOException>(() => operation.TryGetAsync(rule, key, []));
        Assert.Equal(0, operation.CaptureState().Entries);
    }

    /// <summary>
    /// Supplies an independently versioned domain to the actual coordinator.
    /// </summary>
    private sealed class Dependency : IOperationDependency
    {
        private object _version = new();

        /// <summary>
        /// Advances the fixture domain without reusing an earlier token.
        /// </summary>
        internal void Advance() => _version = new object();

        /// <summary>
        /// Captures the current fixture token without filesystem work.
        /// </summary>
        /// <param name="version">
        /// The current stable token.
        /// </param>
        /// <returns>
        /// True because this fixture domain is eligible until its version changes.
        /// </returns>
        public bool TryCaptureVersion(out object? version) { version = _version; return true; }
    }
}
