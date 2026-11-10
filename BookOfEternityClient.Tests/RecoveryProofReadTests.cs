using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises the stable proof-read lifecycle on independent real files and original leases.
/// </summary>
/// <param name="output">
/// Receives every timing record and the measured source/exit counts without a quota.
/// </param>
public sealed class RecoveryProofReadTests(ITestOutputHelper output)
{
    /// <summary>
    /// Requires repeated reads to use one initial source and one fresh exit source.
    /// </summary>
    /// <param name="synchronous">
    /// Whether the plan uses the original synchronous rather than asynchronous reader family.
    /// </param>
    /// <returns>
    /// Completion after source reduction, detached copies and real attribute comparison.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EightReadsUseFirstAndFreshExitRatherThanEightSources(bool synchronous)
    {
        using var fixture = new ProofReadFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        fixture.Recording = true;
        var baselineStart = fixture.Attributes;
        for (var i = 0; i < 8; i++)
            Assert.Equal("original", Encoding.UTF8.GetString((synchronous
                ? files.ReadFileBytesSync(lease, "present.txt")
                : await files.ReadFileBytesAsync(lease, "present.txt"))!));
        var baselineAttributes = fixture.Attributes - baselineStart;
        var start = fixture.Attributes;
        var result = await files.ReadStableRecoveryDataAsync(lease, async () =>
        {
            for (var i = 0; i < 8; i++)
            {
                var bytes = synchronous ? files.ReadFileBytesSync(lease, "present.txt")
                    : await files.ReadFileBytesAsync(lease, "present.txt");
                Assert.Equal("original", Encoding.UTF8.GetString(bytes!));
                bytes![0] = 0; // A returned buffer must not change a subsequent observation.
            }
            return 17;
        });
        var attributes = fixture.Attributes - start;
        output.WriteLine(JsonSerializer.Serialize(new { synchronous, BaselineAttributes = baselineAttributes,
            StableAttributes = attributes, fixture.Counts }));
        Assert.Equal(17, result);
        Assert.Equal(1, fixture.Count("ValidationReadSet.Source" + (synchronous ? "Sync" : "Async")));
        Assert.Equal(7, fixture.Count("ValidationReadSet.Reuse" + (synchronous ? "Sync" : "Async")));
        Assert.Equal(1, fixture.Count("ValidationReadSet.ExitSource"));
        Assert.True(attributes < baselineAttributes, $"Stable attributes {attributes} must be below fresh {baselineAttributes}.");
        Assert.Equal("original", File.ReadAllText(Path.Combine(fixture.Root, "game_session", "present.txt")));
    }

    /// <summary>
    /// Refuses raw source changes before any computed result can escape the plan.
    /// </summary>
    /// <param name="change">
    /// Byte, timestamp, absence, generation or namespace drift injected without the tracked writer.
    /// </param>
    /// <returns>
    /// Completion after a real fresh-exit refusal and release of the exact owned reservation.
    /// </returns>
    [Theory]
    [InlineData("same-time-bytes")]
    [InlineData("timestamp")]
    [InlineData("appearance")]
    [InlineData("generation")]
    [InlineData("namespace")]
    public async Task FreshExitRejectsRawDriftBeforeReturning(string change)
    {
        using var fixture = new ProofReadFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        var path = Path.Combine(fixture.Root, "game_session", "present.txt");
        var timestamp = File.GetLastWriteTimeUtc(path);
        var returned = false;
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await files.ReadStableRecoveryDataAsync(lease, async () =>
            {
                Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
                Assert.Null(await files.ReadFileAsync(lease, "missing.txt"));
                switch (change)
                {
                    case "same-time-bytes": File.WriteAllText(path, "modified"); File.SetLastWriteTimeUtc(path, timestamp); break;
                    case "timestamp": File.SetLastWriteTimeUtc(path, timestamp.AddMinutes(1)); break;
                    case "appearance": File.WriteAllText(Path.Combine(fixture.Root, "game_session", "missing.txt"), "new"); break;
                    case "generation": File.AppendAllText(files.SessionGenerationPath, "\n"); break;
                    case "namespace": File.Delete(path); Directory.CreateDirectory(path); break;
                    default: throw new ArgumentOutOfRangeException(nameof(change));
                }
                return 42;
            });
            returned = true;
        });
        Assert.False(returned);
        Assert.Equal(1, fixture.Count("ValidationReadSet.ExitProof"));
    }

    /// <summary>
    /// Leaves arbitrary functional read hooks on the original fresh source path.
    /// </summary>
    /// <returns>
    /// Completion after all eight hook invocations and no retained source set.
    /// </returns>
    [Fact]
    public async Task UnsupportedReadHookKeepsEightFreshObservations()
    {
        var hooks = 0;
        using var fixture = new ProofReadFixture(output, () => Interlocked.Increment(ref hooks));
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        hooks = 0;
        await files.ReadStableRecoveryDataAsync(lease, async () =>
        {
            for (var i = 0; i < 8; i++) Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
            return true;
        });
        Assert.Equal(8, hooks);
        Assert.Equal(0, fixture.Count("ValidationReadSet.Open"));
    }

    /// <summary>
    /// Preserves a plan failure and releases its reservation before a later supported write.
    /// </summary>
    /// <returns>
    /// Completion after observing the same exception instance and a successful post-failure write.
    /// </returns>
    [Fact]
    public async Task PlanFailureDoesNotLeakReservationOrMaskPrimaryFailure()
    {
        using var fixture = new ProofReadFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        var expected = new InvalidDataException("deliberate source-plan refusal");
        var actual = await Assert.ThrowsAsync<InvalidDataException>(() => files.ReadStableRecoveryDataAsync<int>(lease, async () =>
        {
            Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
            throw expected;
        }));
        Assert.Same(expected, actual);
        Assert.Equal(0, fixture.Count("ValidationReadSet.ExitProof"));
        await files.WriteFileAtomicAsync(lease, "after.txt", "released");
        Assert.Equal("released", await files.ReadFileAsync(lease, "after.txt"));
    }

    /// <summary>
    /// Keeps an enclosing read-set owner responsible for completion and disposal.
    /// </summary>
    /// <returns>
    /// Completion after inner reuse followed by the original outer fresh exit proof.
    /// </returns>
    [Fact]
    public async Task NestedPlanBorrowsExistingRoutingWithoutClosingOuterOwner()
    {
        using var fixture = new ProofReadFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        var scope = files.TryBeginReservedValidationReadSet(lease);
        Assert.NotNull(scope);
        await using (scope)
        {
            await scope.OpenAsync();
            await files.ReadStableRecoveryDataAsync(lease, async () =>
            {
                Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
                return true;
            });
            Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
            Assert.Equal(0, fixture.Count("ValidationReadSet.ExitProof"));
            await scope.CompleteAsync();
        }
        Assert.Equal(1, fixture.Count("ValidationReadSet.Open"));
        Assert.Equal(1, fixture.Count("ValidationReadSet.ExitProof"));
    }

    /// <summary>
    /// Uses the exact participating entry frame while retaining its original physical lease ancestor.
    /// </summary>
    /// <param name="depth">
    /// One or two nested participating session frames above the same physical owner.
    /// </param>
    /// <returns>
    /// Completion after one source-set lifetime with repeated DATA and a fresh exit proof.
    /// </returns>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task BoundSessionEntryRetainsExactParticipatingAncestry(int depth)
    {
        using var fixture = new ProofReadFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var generation = files.GetOrCreateSessionGeneration(lease);
        Task<int> Bound(int remaining) => remaining == 0
            ? files.ReadStableRecoveryDataAsync(lease, async () =>
            {
                for (var i = 0; i < 8; i++) Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
                return 19;
            })
            : SessionOperationContext.RunBoundAsync(files, generation, lease, () => Bound(remaining - 1));
        Assert.Equal(19, await Bound(depth));
        Assert.Equal(1, fixture.Count("ValidationReadSet.Open"));
        Assert.Equal(1, fixture.Count("ValidationReadSet.SourceAsync"));
        Assert.Equal(7, fixture.Count("ValidationReadSet.ReuseAsync"));
        Assert.Equal(1, fixture.Count("ValidationReadSet.ExitProof"));
    }

    /// <summary>
    /// Refuses a second real manager's writer while this helper reserves the shared root.
    /// </summary>
    /// <param name="bound">
    /// Whether to execute inside the actual participating session frame rather than directly under the lease.
    /// </param>
    /// <returns>
    /// Completion after temporary writer refusal, unchanged source bytes and a later successful writer.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OtherManagerCannotWriteDuringReservedPlan(bool bound)
    {
        using var fixture = new ProofReadFixture(output);
        var files = fixture.Files;
        var other = new FileSystemManager(fixture.Root, NullLogger<FileSystemManager>.Instance);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var generation = files.GetOrCreateSessionGeneration(lease);
        Task<bool> Read() => files.ReadStableRecoveryDataAsync(lease, async () =>
        {
            Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
            var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => other.WriteFileAtomicAsync("other.txt", "forbidden"));
            Assert.Contains("reserv", refusal.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
            return true;
        });
        if (bound) Assert.True(await SessionOperationContext.RunBoundAsync(files, generation, lease, Read));
        else Assert.True(await Read());
        Assert.Equal(1, fixture.Count("ValidationReadSet.Open"));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "game_session", "other.txt")));
        await files.WriteFileAtomicAsync(lease, "after.txt", "reservation released");
        await lease.DisposeAsync();
        await other.WriteFileAtomicAsync("other.txt", "released");
        Assert.Equal("released", File.ReadAllText(Path.Combine(fixture.Root, "game_session", "other.txt")));
    }

    /// <summary>
    /// Keeps an existing sibling admission on the original fresh path until that sibling closes.
    /// </summary>
    /// <returns>
    /// Completion after fallback during sibling ownership and normal reserved reuse after its release.
    /// </returns>
    [Fact]
    public async Task ExistingSiblingFlowKeepsReadsFreshUntilItCloses()
    {
        using var fixture = new ProofReadFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sibling = Task.Run(async () =>
        {
            await using var frame = files.BeginParticipatingMainAdmission();
            try
            {
                await frame.AcquireAsync();
                ready.TrySetResult();
                await release.Task;
            }
            catch (Exception error) { ready.TrySetException(error); throw; }
        });
        async Task<bool> Read()
        {
            for (var i = 0; i < 8; i++) Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
            return true;
        }
        try
        {
            await ready.Task;
            Assert.True(await files.ReadStableRecoveryDataAsync(lease, Read));
            Assert.Equal(0, fixture.Count("ValidationReadSet.Open"));
        }
        finally { release.TrySetResult(); await sibling; }
        Assert.True(await files.ReadStableRecoveryDataAsync(lease, Read));
        Assert.Equal(1, fixture.Count("ValidationReadSet.Open"));
        Assert.Equal(1, fixture.Count("ValidationReadSet.SourceAsync"));
        Assert.Equal(7, fixture.Count("ValidationReadSet.ReuseAsync"));
        Assert.Equal(1, fixture.Count("ValidationReadSet.ExitSource"));
    }

    /// <summary>
    /// Refuses closing the exact participating entry before changing its closing state.
    /// </summary>
    /// <returns>
    /// Completion after an atomic closing refusal, successful source exit and normal later closing.
    /// </returns>
    [Fact]
    public async Task BoundEntryCannotBeginClosingDuringReservedPlan()
    {
        using var fixture = new ProofReadFixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        await using var entry = files.BeginParticipatingMainAdmission();
        await entry.AcquireAsync();
        Assert.True(await files.ReadStableRecoveryDataAsync(lease, async () =>
        {
            Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
            var refusal = Assert.Throws<InvalidOperationException>(entry.BeginClosing);
            Assert.Contains("validation", refusal.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(entry.BoundClosing);
            Assert.False(entry.Closed);
            Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
            return true;
        }));
        Assert.Equal(1, fixture.Count("ValidationReadSet.Open"));
        Assert.Equal(1, fixture.Count("ValidationReadSet.ExitSource"));
        entry.BeginClosing();
        Assert.True(entry.BoundClosing);
    }

    /// <summary>
    /// Owns only one nonce-qualified temporary directory and complete timing observations.
    /// </summary>
    private sealed class ProofReadFixture : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly ConcurrentQueue<(string Name, string Boundary, double Milliseconds)> _events = new();
        private readonly string _nonce = Guid.NewGuid().ToString("N");
        private int _attributes;

        /// <summary>
        /// Creates ordinary present/absent files without a game or shared mutable fixture.
        /// </summary>
        /// <param name="output">
        /// Receiver for all observations and exact ownership closing evidence.
        /// </param>
        /// <param name="beforeRead">
        /// Optional functional read hook; null leaves the ordinary eligible source route.
        /// </param>
        internal ProofReadFixture(ITestOutputHelper output, Action? beforeRead = null)
        {
            _output = output;
            Root = Path.Combine(Path.GetTempPath(), "boe-proof-reads-" + _nonce);
            Directory.CreateDirectory(Path.Combine(Root, "game_session"));
            File.WriteAllText(Path.Combine(Root, ".fixture-owner"), _nonce);
            File.WriteAllText(Path.Combine(Root, "game_session", "present.txt"), "original");
            Files = new FileSystemManager(Root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    OperationTimingObserver = (name, boundary, elapsed) =>
                    {
                        _events.Enqueue((name, boundary, elapsed.TotalMilliseconds));
                        if (Recording && name == "TrustedScope.Probe.GetAttributes" && boundary == "end")
                            Interlocked.Increment(ref _attributes);
                    },
                    BeforeCanonicalReadOpenAsync = beforeRead is null ? null : _ => { beforeRead(); return Task.CompletedTask; }
                });
        }

        /// <summary>
        /// Gets this fixture's exclusively owned physical root.
        /// </summary>
        internal string Root { get; }

        /// <summary>
        /// Gets the real manager used by every test source operation.
        /// </summary>
        internal FileSystemManager Files { get; }

        /// <summary>
        /// Gets or sets whether attribute probes contribute to measured counters.
        /// </summary>
        internal bool Recording { get; set; }

        /// <summary>
        /// Gets the number of actual completed attribute probes while recording.
        /// </summary>
        internal int Attributes => Volatile.Read(ref _attributes);

        /// <summary>
        /// Gets all named source, request, reuse and exit event counts.
        /// </summary>
        internal Dictionary<string, int> Counts => _events.Where(row => row.Boundary == "begin" && row.Name.StartsWith("ValidationReadSet.", StringComparison.Ordinal))
            .GroupBy(row => row.Name).ToDictionary(group => group.Key, group => group.Count());

        /// <summary>
        /// Counts an exact observed operation entry without inferring source work from requests.
        /// </summary>
        /// <param name="name">
        /// Exact operation timing name.
        /// </param>
        /// <returns>
        /// Number of actual begin records for that operation.
        /// </returns>
        internal int Count(string name) => _events.Count(row => row.Name == name && row.Boundary == "begin");

        /// <summary>
        /// Emits complete observations and deletes only this verified nonce-qualified fixture.
        /// </summary>
        public void Dispose()
        {
            try
            {
                Assert.Equal("boe-proof-reads-" + _nonce, Path.GetFileName(Root));
                Assert.Equal(_nonce, File.ReadAllText(Path.Combine(Root, ".fixture-owner")));
                new TrustedLocalFileScope([Path.GetTempPath()]).DeleteOwnedTree(Root);
                Assert.False(Directory.Exists(Root));
            }
            finally
            {
                foreach (var row in _events) _output.WriteLine(JsonSerializer.Serialize(new { row.Name, row.Boundary, row.Milliseconds }));
                _output.WriteLine(JsonSerializer.Serialize(new { Root, Removed = !Directory.Exists(Root) }));
            }
        }
    }
}
