using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises typed recovery DATA dispatch against independent actual files and original leases.
/// </summary>
/// <param name="output">
/// Receiver for complete timing, source count and exact owned cleanup evidence.
/// </param>
public sealed class RecoveryProgressionDataTests(ITestOutputHelper output)
{
    /// <summary>
    /// Requires eight eligible requests to execute one real source body and its actual metadata work.
    /// </summary>
    /// <param name="reuse">
    /// Whether the production operation may retain successful DATA; false keeps all requests fresh.
    /// </param>
    /// <param name="expectedBodies">
    /// Literal expected source count for the eight requests.
    /// </param>
    /// <returns>
    /// Completion after comparing actual sources, completed attribute probes and operation counters.
    /// </returns>
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 8)]
    public async Task EightRealRequestsReduceSourcesAndCompletedAttributes(bool reuse, int expectedBodies)
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var scope = files.BeginRecoveryValidation(lease, reuse);
        var computation = BytesComputation();
        var input = new object();
        var bodies = 0;
        var firstAttributes = 0;
        fixture.Recording = true;
        for (var request = 0; request < 8; request++)
        {
            var result = await files.ComputeRecoveryDataAsync(computation, input, lease, async () =>
            {
                bodies++;
                Assert.Null(await files.ReadFileAsync(lease, "absent.txt"));
                return Encoding.UTF8.GetBytes(Assert.IsType<string>(await files.ReadFileAsync(lease, "present.txt")));
            });
            Assert.Equal("original", Encoding.UTF8.GetString(result));
            if (request == 0) firstAttributes = fixture.Attributes;
        }
        fixture.Evidence("EightRequests", bodies, scope, firstAttributes);
        Assert.Equal(expectedBodies, bodies);
        if (OperatingSystem.IsWindows()) Assert.True(firstAttributes > 0);
        Assert.Equal(reuse ? firstAttributes : firstAttributes * 8, fixture.Attributes);
        Assert.Equal(reuse ? 1 : 0, scope.CaptureState().Entries);
        var metrics = scope.CaptureMetrics();
        Assert.Equal(8, metrics.Requests);
        Assert.Equal(expectedBodies, metrics.Sources);
        Assert.Equal(8 - expectedBodies, metrics.Reuses);
        Assert.Equal(reuse ? 0 : 8, metrics.FreshFallbacks);
        Assert.Equal(0, metrics.ActiveMutations);
    }

    /// <summary>
    /// Preserves fresh no-journal recovery inspection without falsely invalidating unchanged DATA.
    /// </summary>
    /// <param name="recoverBetween">
    /// Whether an additional recovery occurs between DATA requests instead of before their first source.
    /// </param>
    /// <returns>
    /// Completion after one source serves requests before and after successful no-evidence recovery.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualNoEvidenceRecoveryPreservesDataBeforeAndBetweenRequests(bool recoverBetween)
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        files.ResolveBackupPublicationRecovery(lease);
        using var scope = files.BeginRecoveryValidationIfNeeded(lease);
        Assert.NotNull(scope);
        files.ResolveBackupPublicationRecovery(lease);
        var computation = BytesComputation();
        var input = new object();
        var bodies = 0;
        async Task<byte[]> Source()
        {
            bodies++;
            Assert.Null(await files.ReadFileAsync(lease, "absent.txt"));
            return Encoding.UTF8.GetBytes(Assert.IsType<string>(await files.ReadFileAsync(lease, "present.txt")));
        }
        fixture.Recording = true;
        Assert.Equal("original", Encoding.UTF8.GetString(await files.ComputeRecoveryDataAsync(computation, input, lease, Source)));
        if (recoverBetween) files.ResolveBackupPublicationRecovery(lease);
        await files.ComputeRecoveryDataAsync(computation, input, lease, Source);
        await files.ComputeRecoveryDataAsync(computation, input, lease, Source);
        fixture.Evidence("ActualRecovery", bodies, scope);
        Assert.Equal(1, bodies);
        var metrics = scope.CaptureMetrics();
        Assert.Equal(0, metrics.MutationBegins);
        Assert.Equal(metrics.MutationBegins, metrics.MutationEnds);
        Assert.Equal(0, metrics.ActiveMutations);
    }

    /// <summary>
    /// Invalidates typed results on changed, same-byte and failed supported write attempts.
    /// </summary>
    /// <param name="kind">
    /// The real changed, same-byte or wrong-type failed write to execute.
    /// </param>
    /// <returns>
    /// Completion after the next pair of requests executes one new source and returns current data.
    /// </returns>
    [Theory]
    [InlineData("changed")]
    [InlineData("same")]
    [InlineData("failed")]
    public async Task SupportedWritesInvalidateEvenWhenUnchangedOrFailed(string kind)
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        await files.WriteFileAtomicAsync(lease, "present.txt", "original");
        var originalBytes = File.ReadAllBytes(files.ResolvePath("present.txt"));
        using var scope = files.BeginRecoveryValidation(lease);
        var computation = BytesComputation();
        var input = new object();
        var bodies = 0;
        Task<byte[]> Compute() => files.ComputeRecoveryDataAsync(computation, input, lease, async () =>
        {
            bodies++;
            return Encoding.UTF8.GetBytes(Assert.IsType<string>(await files.ReadFileAsync(lease, "present.txt")));
        });
        await Compute(); await Compute();
        Assert.Equal(1, bodies);
        var expected = kind == "changed" ? "changed" : "original";
        if (kind == "failed")
            await Assert.ThrowsAnyAsync<Exception>(() => files.WriteFileAtomicAsync(lease, "directory", "invalid"));
        else
            await files.WriteFileAtomicAsync(lease, "present.txt", expected);
        if (kind != "changed") Assert.Equal(originalBytes, File.ReadAllBytes(files.ResolvePath("present.txt")));
        Assert.Equal(expected, Encoding.UTF8.GetString(await Compute()));
        await Compute();
        fixture.Evidence("Write-" + kind, bodies, scope);
        Assert.Equal(2, bodies);
        var metrics = scope.CaptureMetrics();
        Assert.True(metrics.MutationBegins > 0);
        Assert.Equal(metrics.MutationBegins, metrics.MutationEnds);
        Assert.Equal(0, metrics.ActiveMutations);
    }

    /// <summary>
    /// Retires observed absence after creation, deletion and supported namespace changes.
    /// </summary>
    /// <returns>
    /// Completion after four distinct source epochs return literal current file states.
    /// </returns>
    [Fact]
    public async Task AbsenceCreationDeletionAndNamespaceChangesRetireTypedResults()
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var scope = files.BeginRecoveryValidation(lease);
        var computation = new OperationComputation<string?>("ProgressionAbsence", 1, value => value);
        var input = new object();
        var bodies = 0;
        Task<string?> Compute() => files.ComputeRecoveryDataAsync(computation, input, lease, async () =>
        { bodies++; return await files.ReadFileAsync(lease, "absent.txt"); });
        Assert.Null(await Compute()); Assert.Null(await Compute()); Assert.Equal(1, bodies);
        await files.WriteFileAtomicAsync(lease, "absent.txt", "created");
        Assert.Equal("created", await Compute()); Assert.Equal("created", await Compute()); Assert.Equal(2, bodies);
        files.DeleteFile(lease, "absent.txt");
        Assert.Null(await Compute()); Assert.Null(await Compute()); Assert.Equal(3, bodies);
        files.EnsureDirectoryStructure(lease);
        Assert.Null(await Compute()); Assert.Null(await Compute()); Assert.Equal(4, bodies);
        fixture.Evidence("AbsenceAndNamespace", bodies, scope);
    }

    /// <summary>
    /// Shares root invalidation with a second manager's genuine MainAccess mutation reservation.
    /// </summary>
    /// <returns>
    /// Completion after observing the second manager's mutation and recomputing after its close.
    /// </returns>
    [Fact]
    public async Task AnotherManagersRealMutationRetiresTheOriginalManagersData()
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        var other = new FileSystemManager(fixture.Root, NullLogger<FileSystemManager>.Instance);
        Assert.Same(files.CanonicalRootAuthorityIdentity, other.CanonicalRootAuthorityIdentity);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        using var scope = files.BeginRecoveryValidation(lease);
        var computation = BytesComputation();
        var input = new object();
        var bodies = 0;
        Task<byte[]> Compute() => files.ComputeRecoveryDataAsync(computation, input, lease, async () =>
        { bodies++; return Encoding.UTF8.GetBytes(Assert.IsType<string>(await files.ReadFileAsync(lease, "present.txt"))); });
        await Compute(); await Compute(); Assert.Equal(1, bodies);
        using (var admission = other.BeginMainAdmission())
        {
            await admission.AcquireAsync();
            using (admission.BeginMutation())
            {
                File.WriteAllText(other.ResolvePath("present.txt"), "other-manager");
                Assert.Equal("other-manager", Encoding.UTF8.GetString(await Compute()));
            }
        }
        Assert.Equal("other-manager", Encoding.UTF8.GetString(await Compute()));
        await Compute();
        fixture.Evidence("OtherManager", bodies, scope);
        Assert.Equal(3, bodies);
        Assert.Equal(0, scope.CaptureMetrics().ActiveMutations);
    }

    /// <summary>
    /// Preserves rejection when a supported mutation crosses an awaited in-flight source computation.
    /// </summary>
    /// <returns>
    /// Completion after refusing the mixed-epoch source and keeping subsequent requests fresh.
    /// </returns>
    [Fact]
    public async Task AwaitedSourceMutationRejectsAndCannotRetainAResult()
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        using var scope = files.BeginRecoveryValidation(lease);
        var computation = BytesComputation();
        var input = new object();
        await Assert.ThrowsAsync<InvalidOperationException>(() => files.ComputeRecoveryDataAsync(computation, input, lease, async () =>
        {
            var before = Assert.IsType<string>(await files.ReadFileAsync(lease, "present.txt"));
            await Task.Yield();
            await files.WriteFileAtomicAsync(lease, "present.txt", "changed");
            return Encoding.UTF8.GetBytes(before);
        }));
        Assert.Equal(new ValidationOperationState(0, 0, true), scope.CaptureState());
        var bodies = 0;
        for (var request = 0; request < 2; request++)
            Assert.Equal("changed", Encoding.UTF8.GetString(await files.ComputeRecoveryDataAsync(computation, input, lease, async () =>
            { bodies++; return Encoding.UTF8.GetBytes(Assert.IsType<string>(await files.ReadFileAsync(lease, "present.txt"))); })));
        Assert.Equal(2, bodies);
        Assert.Equal(0, scope.CaptureState().Entries);
        fixture.Evidence("AwaitMutationRejected", bodies, scope);
    }

    /// <summary>
    /// Keeps a callback fresh across its await and never revives a pre-callback result.
    /// </summary>
    /// <returns>
    /// Completion after the callback's two fresh sources and a new reusable source afterward.
    /// </returns>
    [Fact]
    public async Task FreshCallbackAcrossAwaitCannotReviveEarlierSuccess()
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        using var scope = files.BeginRecoveryValidation(lease);
        var computation = BytesComputation();
        var input = new object();
        var bodies = 0;
        Task<byte[]> Compute() => files.ComputeRecoveryDataAsync(computation, input, lease, async () =>
        { bodies++; return Encoding.UTF8.GetBytes(Assert.IsType<string>(await files.ReadFileAsync(lease, "present.txt"))); });
        await Compute(); await Compute(); Assert.Equal(1, bodies);
        async Task Callback()
        {
            using var fresh = files.ForceFreshRecoveryValidation();
            await Compute();
            await Task.Yield();
            await Compute();
        }
        await Callback();
        Assert.Equal(3, bodies);
        await Compute(); await Compute();
        Assert.Equal(4, bodies);
        fixture.Evidence("FreshCallback", bodies, scope);
    }

    /// <summary>
    /// Excludes arbitrary functional read hooks from typed source reuse.
    /// </summary>
    /// <returns>
    /// Completion after every request reads the actual file and retains no DATA entry.
    /// </returns>
    [Fact]
    public async Task ArbitraryReadHookKeepsAllTypedRequestsFresh()
    {
        using var fixture = new Fixture(output, arbitraryReadHook: true);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        using var scope = files.BeginRecoveryValidation(lease);
        var computation = BytesComputation();
        var input = new object();
        var bodies = 0;
        for (var request = 0; request < 3; request++)
            Assert.Equal("original", Encoding.UTF8.GetString(await files.ComputeRecoveryDataAsync(computation, input, lease, async () =>
            { bodies++; return Encoding.UTF8.GetBytes(Assert.IsType<string>(await files.ReadFileAsync(lease, "present.txt"))); })));
        Assert.Equal(3, bodies);
        Assert.Equal(0, scope.CaptureState().Entries);
        fixture.Evidence("ArbitraryHook", bodies, scope);
    }

    /// <summary>
    /// Detaches retained and returned arrays while keeping equal-valued input objects distinct.
    /// </summary>
    /// <returns>
    /// Completion after source and caller mutations leave cached bytes intact and a new input executes source.
    /// </returns>
    [Fact]
    public async Task CopiesProtectRetainedDataAndExactInputIdentityControlsHits()
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        using var scope = files.BeginRecoveryValidation(lease);
        var computation = BytesComputation();
        var input = new InputIdentity(7);
        byte[]? source = null;
        var bodies = 0;
        async Task<byte[]> Source()
        {
            bodies++;
            source = Encoding.UTF8.GetBytes(Assert.IsType<string>(await files.ReadFileAsync(lease, "present.txt")));
            return source;
        }
        var first = await files.ComputeRecoveryDataAsync(computation, input, lease, Source);
        first[0] = (byte)'X';
        Assert.NotNull(source);
        source[1] = (byte)'Y';
        var second = await files.ComputeRecoveryDataAsync(computation, input, lease, Source);
        Assert.Equal("original", Encoding.UTF8.GetString(second));
        second[2] = (byte)'Z';
        Assert.Equal("original", Encoding.UTF8.GetString(await files.ComputeRecoveryDataAsync(computation, input, lease, Source)));
        Assert.Equal(1, bodies);
        Assert.Equal("original", Encoding.UTF8.GetString(await files.ComputeRecoveryDataAsync(computation, new InputIdentity(7), lease, Source)));
        Assert.Equal(2, bodies);
        fixture.Evidence("CopiesAndExactIdentity", bodies, scope);
    }

    /// <summary>
    /// Retires borrowed successes after source errors or explicit phase-reader failure notification.
    /// </summary>
    /// <param name="sourceError">
    /// Whether the failure originates in a new DATA source; false reports an existing outer phase failure.
    /// </param>
    /// <returns>
    /// Completion after preserving the primary failure and refusing to resurrect the earlier successful entry.
    /// </returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SourceAndPhaseFailureCannotResurrectBorrowedSuccess(bool sourceError)
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        using var parent = files.BeginRecoveryValidation(lease);
        Assert.Null(files.BeginRecoveryValidationIfNeeded(lease));
        var computation = BytesComputation();
        var input = new object();
        var bodies = 0;
        Task<byte[]> Compute() => files.ComputeRecoveryDataAsync(computation, input, lease, async () =>
        { bodies++; return Encoding.UTF8.GetBytes(Assert.IsType<string>(await files.ReadFileAsync(lease, "present.txt"))); });
        await Compute(); await Compute(); Assert.Equal(1, bodies);
        if (sourceError)
        {
            var expected = new InvalidDataException("Actual source refusal.");
            var actual = await Assert.ThrowsAsync<InvalidDataException>(() =>
                files.ComputeRecoveryDataAsync(computation, new object(), lease, async () =>
                { await files.ReadFileAsync(lease, "absent.txt"); throw expected; }));
            Assert.Same(expected, actual);
        }
        else files.InvalidateRecoveryValidationAfterFailure();
        Assert.Equal(new ValidationOperationState(0, 0, true), parent.CaptureState());
        await Compute(); await Compute();
        Assert.Equal(3, bodies);
        Assert.Equal(0, parent.CaptureState().Entries);
        fixture.Evidence("Failure-" + sourceError, bodies, parent);
    }

    /// <summary>
    /// Keeps borrowed ownership with its parent and closes owned results before any captured descendant can reuse them.
    /// </summary>
    /// <returns>
    /// Completion after success preserves the parent, disposal rejects late work and a new scope recomputes.
    /// </returns>
    [Fact]
    public async Task OwnedAndBorrowedLifetimesCloseOnlyTheOwningScope()
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var computation = BytesComputation();
        var input = new object();
        var bodies = 0;
        Task<byte[]> Compute() => files.ComputeRecoveryDataAsync(computation, input, lease, async () =>
        { bodies++; return Encoding.UTF8.GetBytes(Assert.IsType<string>(await files.ReadFileAsync(lease, "present.txt"))); });
        var owned = files.BeginRecoveryValidationIfNeeded(lease);
        Assert.NotNull(owned);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var descendant = Task.Run(async () =>
        {
            await release.Task;
            return await Record.ExceptionAsync(async () => { await Compute(); });
        });
        Exception? descendantFailure = null;
        try
        {
            await Compute();
            using (var borrowed = files.BeginRecoveryValidationIfNeeded(lease))
            { Assert.Null(borrowed); await Compute(); }
            Assert.False(owned.CaptureState().Closed);
            await Compute(); Assert.Equal(1, bodies);
        }
        finally
        {
            owned.Dispose();
            release.SetResult();
            descendantFailure = await descendant;
        }
        Assert.IsType<ObjectDisposedException>(descendantFailure);
        Assert.Equal(new ValidationOperationState(0, 0, true), owned.CaptureState());
        using var next = files.BeginRecoveryValidationIfNeeded(lease);
        Assert.NotNull(next);
        await Compute(); await Compute(); Assert.Equal(2, bodies);
        fixture.Evidence("OwnedBorrowedAndClosed", bodies, next);
    }

    /// <summary>
    /// Creates a payload-free stable descriptor whose copy policy detaches mutable byte arrays.
    /// </summary>
    /// <returns>
    /// A descriptor private to this independent test invocation.
    /// </returns>
    private static OperationComputation<byte[]> BytesComputation() => new("ProgressionData", 1, value => value.ToArray());

    /// <summary>
    /// Supplies equal-valued but distinct references to expose accidental value-keyed reuse.
    /// </summary>
    /// <param name="Value">
    /// Literal input value shared by two independently allocated identities.
    /// </param>
    private sealed record InputIdentity(int Value);

    /// <summary>
    /// Owns only a nonce-marked minimal filesystem and retains complete passive timing observations.
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _parent = Path.GetFullPath(Path.GetTempPath());
        private readonly string _nonce = Guid.NewGuid().ToString("N");
        private readonly ConcurrentQueue<(string Name, string Boundary, double Seconds)> _events = new();
        private int _attributes;

        /// <summary>
        /// Creates a present file, absent member and directory without any game or authority fixture.
        /// </summary>
        /// <param name="output">
        /// Receiver for every measured row and owned cleanup result; runner storage remains local diagnostics.
        /// </param>
        /// <param name="arbitraryReadHook">
        /// Whether to install an arbitrary functional callback that excludes reuse.
        /// </param>
        internal Fixture(ITestOutputHelper output, bool arbitraryReadHook = false)
        {
            var timer = Stopwatch.StartNew();
            _output = output;
            Root = Path.Combine(_parent, "boe-progression-data-" + _nonce);
            Directory.CreateDirectory(Path.Combine(Root, "game_session", "directory"));
            File.WriteAllText(Path.Combine(Root, ".fixture-owner"), _nonce);
            File.WriteAllText(Path.Combine(Root, "game_session", "present.txt"), "original");
            Files = new(Root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
                new FileSystemManagerHooks
                {
                    OperationTimingObserver = (name, boundary, elapsed) =>
                    {
                        _events.Enqueue((name, boundary, elapsed.TotalSeconds));
                        if (Recording && name == "TrustedScope.Probe.GetAttributes" && boundary == "end")
                            Interlocked.Increment(ref _attributes);
                    },
                    BeforeCanonicalReadOpenAsync = arbitraryReadHook ? _ => Task.CompletedTask : null
                });
            _events.Enqueue(("Fixture.Setup", "end", timer.Elapsed.TotalSeconds));
        }

        /// <summary>
        /// Gets this fixture's exact owned root, distinct from existing game saves and test result directories.
        /// </summary>
        internal string Root { get; }

        /// <summary>
        /// Gets the real manager used by every source and supported mutation.
        /// </summary>
        internal FileSystemManager Files { get; }

        /// <summary>
        /// Gets or sets whether completed attribute probes belong to the measured source interval.
        /// </summary>
        internal bool Recording { get; set; }

        /// <summary>
        /// Gets the actual completed attribute probes since measurement began.
        /// </summary>
        internal int Attributes => Volatile.Read(ref _attributes);

        /// <summary>
        /// Emits named real counts without deriving an expected result from the implementation.
        /// </summary>
        /// <param name="name">
        /// Scenario name used to associate complete timing rows with source counts.
        /// </param>
        /// <param name="bodies">
        /// Actual source body executions observed by this test.
        /// </param>
        /// <param name="scope">
        /// Exact operation whose metrics and retained state are being inspected.
        /// </param>
        /// <param name="firstAttributes">
        /// Completed first-source attribute probes when comparing repeated requests; null otherwise.
        /// </param>
        internal void Evidence(string name, int bodies, FileSystemManager.RecoveryValidationScope scope,
            int? firstAttributes = null) => _output.WriteLine(JsonSerializer.Serialize(new
            { Name = name, Bodies = bodies, Attributes, FirstAttributes = firstAttributes, Metrics = scope.CaptureMetrics() }));

        /// <summary>
        /// Removes only this exact owned fixture and emits all uncapped observations and cleanup evidence.
        /// </summary>
        public void Dispose()
        {
            var timer = Stopwatch.StartNew();
            try
            {
                if (Path.GetDirectoryName(Root) != Path.TrimEndingDirectorySeparator(_parent) ||
                    Path.GetFileName(Root) != "boe-progression-data-" + _nonce ||
                    File.ReadAllText(Path.Combine(Root, ".fixture-owner")) != _nonce)
                    throw new InvalidDataException("Fixture ownership changed; preserve its root.");
                new TrustedLocalFileScope([_parent]).DeleteOwnedTree(Root);
                Assert.False(Directory.Exists(Root));
            }
            finally
            {
                _events.Enqueue(("Fixture.Cleanup", "end", timer.Elapsed.TotalSeconds));
                foreach (var row in _events)
                    _output.WriteLine(JsonSerializer.Serialize(new { row.Name, row.Boundary, row.Seconds }));
                _output.WriteLine(JsonSerializer.Serialize(new { Root, Removed = !Directory.Exists(Root) }));
            }
        }
    }
}
