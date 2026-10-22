using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises whole-operation source reuse and effect fences on independent ordinary files.
/// </summary>
/// <param name="output">
/// Receives every actual source/timing observation and nonce-owned cleanup result.
/// </param>
public sealed class SourceDataOperationTests(ITestOutputHelper output)
{
    /// <summary>
    /// Reuses detached bytes throughout one owner and performs only one fresh final source read.
    /// </summary>
    /// <param name="synchronous">
    /// Whether the original synchronous rather than asynchronous reader supplies the source.
    /// </param>
    /// <returns>
    /// Completion after real physical read reduction and unchanged source bytes.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EightRequestsUseOneInitialAndOneFinalSource(bool synchronous)
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        fixture.Recording = true;
        for (var i = 0; i < 8; i++)
        {
            var bytes = synchronous ? files.ReadFileBytesSync(lease, "present.txt")
                : await files.ReadFileBytesAsync(lease, "present.txt");
            Assert.Equal("original", Encoding.UTF8.GetString(bytes!));
            bytes![0] = 0;
        }
        await operation.CompleteAsync();
        output.WriteLine(JsonSerializer.Serialize(new { synchronous, fixture.PresentSources }));
        Assert.Equal(2, fixture.PresentSources);
        Assert.Equal("original", File.ReadAllText(fixture.PresentPath));
    }

    /// <summary>
    /// Shares sources between independent nested read plans instead of closing one read set per plan.
    /// </summary>
    /// <param name="bound">
    /// Whether the plans run through the actual participating session context.
    /// </param>
    /// <returns>
    /// Completion after two full nested plans share one first and one final source.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TwoNestedPlansShareTheOwningSourceLifetime(bool bound)
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var generation = files.GetOrCreateSessionGeneration(lease);
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        fixture.Recording = true;
        async Task<bool> Plans()
        {
            for (var plan = 0; plan < 2; plan++)
                Assert.True(await files.ReadStableRecoveryDataAsync(lease, async () =>
                {
                    for (var read = 0; read < 8; read++) Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
                    return true;
                }));
            return true;
        }
        if (bound) Assert.True(await SessionOperationContext.RunBoundAsync(files, generation, lease, Plans));
        else Assert.True(await Plans());
        await operation.CompleteAsync();
        output.WriteLine(JsonSerializer.Serialize(new { bound, fixture.PresentSources }));
        Assert.Equal(2, fixture.PresentSources);
    }

    /// <summary>
    /// Refuses raw bytes, absence, namespace and generation changes before successful owner completion.
    /// </summary>
    /// <param name="change">
    /// The external untracked change introduced after actual source observations.
    /// </param>
    /// <returns>
    /// Completion after no owner success escapes the fresh source fence.
    /// </returns>
    [Theory]
    [InlineData("same-time-bytes")]
    [InlineData("appearance")]
    [InlineData("namespace")]
    [InlineData("generation")]
    public async Task RawDriftCannotEscapeSuccessfulCompletion(string change)
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        var timestamp = File.GetLastWriteTimeUtc(fixture.PresentPath);
        Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        Assert.Null(await files.ReadFileAsync(lease, "missing.txt"));
        switch (change)
        {
            case "same-time-bytes": File.WriteAllText(fixture.PresentPath, "modified"); File.SetLastWriteTimeUtc(fixture.PresentPath, timestamp); break;
            case "appearance": File.WriteAllText(Path.Combine(fixture.Root, "game_session", "missing.txt"), "new"); break;
            case "namespace": File.Delete(fixture.PresentPath); Directory.CreateDirectory(fixture.PresentPath); break;
            case "generation": File.AppendAllText(files.SessionGenerationPath, "\n"); break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        await Assert.ThrowsAsync<InvalidDataException>(operation.CompleteAsync);
        Assert.False(File.Exists(Path.Combine(fixture.Root, "game_session", "effect.txt")));
    }

    /// <summary>
    /// Blocks a real writer before effects when its cached sources have not passed a fresh fence.
    /// </summary>
    /// <returns>
    /// Completion after the actual public writer refuses and creates no target file.
    /// </returns>
    [Fact]
    public async Task ActualWriterRefusesUnverifiedSourceData()
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => files.WriteFileAtomicAsync(lease, "effect.txt", "forbidden"));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "game_session", "effect.txt")));
        Assert.Equal("original", File.ReadAllText(fixture.PresentPath));
    }

    /// <summary>
    /// Allows an explicitly verified real writer and observes its new bytes in the next DATA segment.
    /// </summary>
    /// <returns>
    /// Completion after old bytes cannot be returned after the supported mutation.
    /// </returns>
    [Fact]
    public async Task VerifiedRealWriterRetiresEarlierData()
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        using (operation)
        {
            Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
            await operation.VerifyAsync(lease);
            await files.WriteFileAtomicAsync(lease, "present.txt", "changed");
            Assert.Equal("changed", await files.ReadFileAsync(lease, "present.txt"));
            await operation.CompleteAsync();
        }
        Assert.Equal(0, operation.CaptureState().Entries);
        Assert.True(operation.CaptureState().Closed);
    }

    /// <summary>
    /// Refuses a captured child after owner disposal even while the child's original physical lease is still live.
    /// </summary>
    /// <returns>
    /// Completion after child settlement and closed empty general storage.
    /// </returns>
    [Fact]
    public async Task CapturedChildCannotUseClosedOperation()
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var child = Task.Run(async () => { await release.Task; return await files.ReadFileAsync(lease, "present.txt"); });
        operation.Dispose();
        release.TrySetResult();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => { await child; });
        Assert.Equal(0, operation.CaptureState().Entries);
        Assert.True(operation.CaptureState().Closed);
    }

    /// <summary>
    /// Keeps source reads fresh while an unrelated live sibling borrows the original access.
    /// </summary>
    /// <returns>
    /// Completion after eight sibling-conflicted physical reads and later first/final reuse.
    /// </returns>
    [Fact]
    public async Task ExistingSiblingKeepsSourcesFreshUntilItCloses()
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sibling = Task.Run(async () =>
        {
            await using var frame = files.BeginParticipatingMainAdmission();
            try { await frame.AcquireAsync(); ready.TrySetResult(); await release.Task; }
            catch (Exception error) { ready.TrySetException(error); throw; }
        });
        fixture.Recording = true;
        try
        {
            await ready.Task;
            for (var i = 0; i < 8; i++) Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
            Assert.Equal(8, fixture.PresentSources);
            Assert.Equal(0, operation.CaptureState().Entries);
        }
        finally { release.TrySetResult(); await sibling; }
        for (var i = 0; i < 8; i++) Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        await operation.CompleteAsync();
        Assert.Equal(10, fixture.PresentSources);
    }

    /// <summary>
    /// Prevents a newly admitted descendant from consuming another entry's verified receipt.
    /// </summary>
    /// <returns>
    /// Completion after an actual writer refuses before creating its target.
    /// </returns>
    [Fact]
    public async Task AnotherEntryCannotConsumeVerifiedReceipt()
    {
        using var fixture = new SourceOperationFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        await operation.VerifyAsync(lease);
        await using var entry = files.BeginParticipatingMainAdmission();
        await entry.AcquireAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => files.WriteFileAtomicAsync(lease, "effect.txt", "forbidden"));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "game_session", "effect.txt")));
    }

    /// <summary>
    /// Owns tiny real files, actual source counters and complete passive timing output.
    /// </summary>
    private sealed class SourceOperationFixture : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _nonce = Guid.NewGuid().ToString("N");
        private readonly ConcurrentQueue<(string Name, string Boundary, double Milliseconds)> _rows = new();
        private int _presentSources;

        /// <summary>
        /// Creates one nonce-qualified directory with an ordinary present file and a passive receiver.
        /// </summary>
        /// <param name="output">
        /// Receiver for every timing and final exact cleanup observation.
        /// </param>
        internal SourceOperationFixture(ITestOutputHelper output)
        {
            _output = output;
            Root = Path.Combine(Path.GetTempPath(), "boe-source-operation-" + _nonce);
            Directory.CreateDirectory(Path.Combine(Root, "game_session"));
            File.WriteAllText(Path.Combine(Root, ".fixture-owner"), _nonce);
            File.WriteAllText(PresentPath, "original");
            Files = new FileSystemManager(Root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    CanonicalReadActivityObserver = path =>
                    {
                        if (Recording && path == "present.txt") Interlocked.Increment(ref _presentSources);
                    },
                    OperationTimingObserver = (name, boundary, elapsed) => _rows.Enqueue((name, boundary, elapsed.TotalMilliseconds))
                });
        }

        /// <summary>
        /// Gets the exclusively owned physical root.
        /// </summary>
        internal string Root { get; }

        /// <summary>
        /// Gets the actual ordinary source file name.
        /// </summary>
        internal string PresentPath => Path.Combine(Root, "game_session", "present.txt");

        /// <summary>
        /// Gets the production manager issuing every real lease and source read.
        /// </summary>
        internal FileSystemManager Files { get; }

        /// <summary>
        /// Gets or sets whether actual opened present-file sources contribute to the counter.
        /// </summary>
        internal bool Recording { get; set; }

        /// <summary>
        /// Gets the number of actual present-file read activity events while recording.
        /// </summary>
        internal int PresentSources => Volatile.Read(ref _presentSources);

        /// <summary>
        /// Deletes only the verified owned root and emits all observations without a record quota.
        /// </summary>
        public void Dispose()
        {
            try
            {
                Assert.Equal("boe-source-operation-" + _nonce, Path.GetFileName(Root));
                Assert.Equal(_nonce, File.ReadAllText(Path.Combine(Root, ".fixture-owner")));
                new TrustedLocalFileScope([Path.GetTempPath()]).DeleteOwnedTree(Root);
                Assert.False(Directory.Exists(Root));
            }
            finally
            {
                foreach (var row in _rows) _output.WriteLine(JsonSerializer.Serialize(new { row.Name, row.Boundary, row.Milliseconds }));
                _output.WriteLine(JsonSerializer.Serialize(new { Root, Removed = !Directory.Exists(Root) }));
            }
        }
    }
}
