using BookOfEternityClient.Services.Caching;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises the production repository service with independent in-memory sources.
/// </summary>
public sealed class RepositoryCacheServiceTests
{
    /// <summary>
    /// Loads once and separates source, retained and returned mutable values.
    /// </summary>
    [Fact]
    public async Task ReadsReuseDetachedValuesAndServicesBorrowStorage()
    {
        using var storage = new ServiceCacheStorage<string, int[]>(new DictionaryCache<string, int[]>());
        var repository = new ArrayRepository { Value = [1, 2] };
        RepositoryService<string, int[]> first = new DefaultRepositoryService<string, int[]>(repository, storage, a => a.ToArray());
        var result = await first.ReadAsync("a");
        Assert.True(result.Found);
        result.Value[0] = 9;
        repository.Value[1] = 8;
        var second = new DefaultRepositoryService<string, int[]>(repository, storage, a => a.ToArray());
        Assert.Equal(new[] { 1, 2 }, (await second.ReadAsync("a")).Value);
        Assert.Equal(1, repository.Reads);
        Assert.Equal(1, storage.Cache.Count);
    }

    /// <summary>
    /// Distinguishes present <see langword="null"/> from absence and does not retain missing source data.
    /// </summary>
    [Fact]
    public async Task NullIsCachedAndAbsenceIsReloaded()
    {
        using var storage = new ServiceCacheStorage<string, string?>(new DictionaryCache<string, string?>());
        var calls = 0;
        var repository = new StringRepository { Read = _ => Task.FromResult(new CacheLookup<string?>(++calls < 3, null)) };
        var service = new DefaultRepositoryService<string, string?>(repository, storage, v => v);
        Assert.True((await service.ReadAsync("null")).Found);
        Assert.True((await service.ReadAsync("null")).Found);
        Assert.Equal(1, calls);
        storage.Cache.Clear();
        Assert.True((await service.ReadAsync("again")).Found);
        storage.Cache.Clear();
        Assert.False((await service.ReadAsync("absent")).Found);
        Assert.False((await service.ReadAsync("absent")).Found);
        Assert.Equal(4, calls);
    }

    /// <summary>
    /// Performs actual source writes and reads normalized source data after invalidation.
    /// </summary>
    [Fact]
    public async Task SaveAndDeleteInvalidateActualSourceResults()
    {
        using var storage = new ServiceCacheStorage<string, int[]>(new DictionaryCache<string, int[]>());
        var repository = new ArrayRepository { Value = [1] };
        var service = new DefaultRepositoryService<string, int[]>(repository, storage, a => a.ToArray());
        await service.ReadAsync("a");
        Assert.True(storage.Cache.TryGet("a", out _));
        var submitted = new[] { 4 };
        await service.SaveAsync("a", submitted);
        submitted[0] = 100;
        Assert.False(storage.Cache.TryGet("a", out _));
        Assert.Equal(new[] { 5 }, (await service.ReadAsync("a")).Value);
        Assert.Equal(1, repository.Saves);
        await service.DeleteAsync("a");
        Assert.False(storage.Cache.TryGet("a", out _));
        Assert.False((await service.ReadAsync("a")).Found);
        Assert.Equal(1, repository.Deletes);
    }

    /// <summary>
    /// Clears an earlier result even when a real source write changes data then fails.
    /// </summary>
    /// <param name="delete">
    /// Whether the failing source mutation deletes instead of saves the record.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartialSourceFailureNeverLeavesPreviousSuccess(bool delete)
    {
        using var storage = new ServiceCacheStorage<string, int[]>(new DictionaryCache<string, int[]>());
        var expected = new IOException("partial source change");
        var repository = new ArrayRepository { Value = [1], WriteError = expected };
        var service = new DefaultRepositoryService<string, int[]>(repository, storage, a => a.ToArray());
        await service.ReadAsync("a");
        Assert.True(storage.Cache.TryGet("a", out _));
        var error = await Assert.ThrowsAsync<IOException>(() => delete ? service.DeleteAsync("a") : service.SaveAsync("a", [4]));
        Assert.Same(expected, error);
        Assert.False(storage.Cache.TryGet("a", out _));
        var result = await service.ReadAsync("a");
        Assert.Equal(!delete, result.Found);
        if (!delete) Assert.Equal(new[] { 5 }, result.Value);
    }

    /// <summary>
    /// Re-executes failed and cancelled loads rather than retaining their outcomes.
    /// </summary>
    [Fact]
    public async Task LoadErrorAndCancellationAreNotRetained()
    {
        using var storage = new ServiceCacheStorage<string, string?>(new DictionaryCache<string, string?>());
        var calls = 0;
        using var cancel = new CancellationTokenSource();
        var repository = new StringRepository { Read = _ =>
        {
            calls++;
            if (calls == 1) throw new IOException("read failed");
            if (calls == 2) cancel.Cancel();
            return Task.FromResult(new CacheLookup<string?>(true, "ok"));
        }};
        var service = new DefaultRepositoryService<string, string?>(repository, storage, v => v);
        await Assert.ThrowsAsync<IOException>(() => service.ReadAsync("a"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ReadAsync("a", cancel.Token));
        Assert.False(storage.Cache.TryGet("a", out _));
        Assert.Equal("ok", (await service.ReadAsync("a")).Value);
        Assert.Equal(3, calls);
    }

    /// <summary>
    /// Serializes source access across two service instances sharing one storage.
    /// </summary>
    [Fact]
    public async Task SharedStorageSerializesReadersAndWriters()
    {
        using var storage = new ServiceCacheStorage<string, string?>(new DictionaryCache<string, string?>());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        var writes = 0;
        var current = "before";
        var repository = new StringRepository { Read = async _ =>
        {
            reads++;
            var captured = current;
            entered.TrySetResult();
            await release.Task;
            return new(true, captured);
        }, Save = (_, value) => { writes++; current = value; return Task.CompletedTask; } };
        var first = new DefaultRepositoryService<string, string?>(repository, storage, v => v);
        var second = new DefaultRepositoryService<string, string?>(repository, storage, v => v);
        var read = first.ReadAsync("a");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var write = second.SaveAsync("a", "after");
        try { Assert.Equal(0, writes); }
        finally { release.TrySetResult(); await read; await write; }
        Assert.Equal("after", (await second.ReadAsync("a")).Value);
        Assert.Equal(2, reads);
    }

    /// <summary>
    /// Refuses a callback trying to wait on its own storage request.
    /// </summary>
    [Fact]
    public async Task RecursiveSourceCallbackRefusesInsteadOfWaiting()
    {
        using var storage = new ServiceCacheStorage<string, string?>(new DictionaryCache<string, string?>());
        var calls = 0;
        DefaultRepositoryService<string, string?>? service = null;
        var repository = new StringRepository { Read = async _ =>
            ++calls == 1 ? await service!.ReadAsync("nested") : new(true, "value") };
        service = new(repository, storage, v => v);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadAsync("a").WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, calls);
        Assert.Equal(0, storage.Cache.Count);
    }

    /// <summary>
    /// Closing the owner rejects both an active load and a queued request.
    /// </summary>
    [Fact]
    public async Task CloseDuringLoadAndWaitPreventsLateEscape()
    {
        var storage = new ServiceCacheStorage<string, string?>(new DictionaryCache<string, string?>());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var repository = new StringRepository { Read = async _ =>
        { calls++; entered.TrySetResult(); await release.Task; return new(true, "late"); } };
        var service = new DefaultRepositoryService<string, string?>(repository, storage, v => v);
        var active = service.ReadAsync("a");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var waiting = service.ReadAsync("b");
        storage.Dispose();
        release.TrySetResult();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => active.WaitAsync(TimeSpan.FromSeconds(5)));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, calls);
    }

    /// <summary>
    /// Provides a genuine independent mutable source whose writes normalize values.
    /// </summary>
    private sealed class ArrayRepository : ICacheRepository<string, int[]>
    {
        /// <summary>
        /// Holds actual source bytes represented by numbers, or absence.
        /// </summary>
        internal int[]? Value;
        /// <summary>
        /// Counts source reads independently of cache requests.
        /// </summary>
        internal int Reads;
        /// <summary>
        /// Counts actual saves.
        /// </summary>
        internal int Saves;
        /// <summary>
        /// Counts actual deletes.
        /// </summary>
        internal int Deletes;
        /// <summary>
        /// Optionally fails after a partial source write.
        /// </summary>
        internal Exception? WriteError;
        /// <inheritdoc />
        public Task<CacheLookup<int[]>> ReadAsync(string key, CancellationToken cancellationToken)
        { Reads++; return Task.FromResult(new CacheLookup<int[]>(Value is not null, Value!)); }
        /// <inheritdoc />
        public Task SaveAsync(string key, int[] value, CancellationToken cancellationToken)
        { Saves++; Value = value.Select(v => v + 1).ToArray(); if (WriteError is not null) throw WriteError; return Task.CompletedTask; }
        /// <inheritdoc />
        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        { Deletes++; Value = null; if (WriteError is not null) throw WriteError; return Task.CompletedTask; }
    }

    /// <summary>
    /// Exposes typed source callbacks for precise async barriers and failures.
    /// </summary>
    private sealed class StringRepository : ICacheRepository<string, string?>
    {
        /// <summary>
        /// Supplies each original source load.
        /// </summary>
        internal Func<CancellationToken, Task<CacheLookup<string?>>> Read = _ => Task.FromResult(new CacheLookup<string?>(false, null));
        /// <summary>
        /// Performs an original source save.
        /// </summary>
        internal Func<CancellationToken, string?, Task> Save = (_, _) => Task.CompletedTask;
        /// <inheritdoc />
        public Task<CacheLookup<string?>> ReadAsync(string key, CancellationToken cancellationToken) => Read(cancellationToken);
        /// <inheritdoc />
        public Task SaveAsync(string key, string? value, CancellationToken cancellationToken) => Save(cancellationToken, value);
        /// <inheritdoc />
        public Task DeleteAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
