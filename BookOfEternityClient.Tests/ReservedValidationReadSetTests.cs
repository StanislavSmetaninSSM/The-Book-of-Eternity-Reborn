using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises bounded validation reads on tiny real files without preparing a game world.
/// </summary>
/// <param name="output">
/// Receiver for complete observations and owned fixture closing evidence.
/// </param>
public sealed class ReservedValidationReadSetTests(ITestOutputHelper output)
{
    /// <summary>
    /// Replays all retained identities into a different phase, keeping family and explicit lease mode separate.
    /// </summary>
    [Fact]
    public async Task ObservationMetadataReplaysExactReaderIdentitiesIntoFreshExitProof()
    {
        using var fixture = new Fixture(output);
        var files = fixture.Create();
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        FileSystemManager.ValidationReadObservations observations;
        await using (var first = files.TryBeginReservedValidationReadSet(lease))
        {
            Assert.NotNull(first);
            await first.OpenAsync();
            Assert.Equal("original", files.ReadFileSync(lease, "present.txt"));
            Assert.Equal("original", files.ReadFileSync("present.txt"));
            Assert.Null(await files.ReadFileAsync(lease, "missing.txt"));
            Assert.Null(await files.ReadFileAsync("missing.txt"));
            observations = first.CaptureObservations();
            Assert.Equal(4, observations.Count);
            await first.CompleteAsync();
        }
        fixture.Recording = true;
        await using (var second = files.TryBeginReservedValidationReadSet(lease))
        {
            Assert.NotNull(second);
            await second.OpenAsync();
            Assert.True(await second.MatchesObservationsAsync(observations));
            Assert.Equal(4, second.CaptureObservations().Count);
            await second.CompleteAsync();
        }
        Assert.Equal(8, fixture.PhysicalReads);
    }

    /// <summary>
    /// Requires only first and exit source observations for repeated consumers of four distinct read identities.
    /// </summary>
    /// <param name="reserved">
    /// Whether to use the bounded read set or measure the same original fresh reader workload.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedConsumersReadEachSourceOnlyAtEntryAndExit(bool reserved)
    {
        using var fixture = new Fixture(output);
        var files = fixture.Create();
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        fixture.Recording = true;
        using var timingContext = files.EnterOperationTimingContext();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        await using var readSet = reserved ? files.TryBeginReservedValidationReadSet(lease) : null;
        if (reserved) { Assert.NotNull(readSet); await readSet.OpenAsync(); }
        for (var i = 0; i < 12; i++)
        {
            Assert.Equal("original", files.ReadFileSync(lease, "present.txt"));
            Assert.Null(files.ReadFileSync(lease, "missing.txt"));
            Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
            Assert.Null(await files.ReadFileAsync(lease, "missing.txt"));
        }
        if (readSet != null) await readSet.CompleteAsync();
        output.WriteLine(JsonSerializer.Serialize(new { Reserved = reserved, Requests = 48,
            PhysicalReads = fixture.PhysicalReads, Seconds = clock.Elapsed.TotalSeconds }));
        Assert.Equal(reserved ? 8 : 48, fixture.PhysicalReads);
    }

    /// <summary>
    /// Refuses changed content, absence, timestamp and exact generation bytes before a successful result can escape.
    /// </summary>
    /// <param name="change">
    /// The source condition deliberately changed after the first read.
    /// </param>
    [Theory]
    [InlineData("bytes")]
    [InlineData("same-time-bytes")]
    [InlineData("delete")]
    [InlineData("appearance")]
    [InlineData("timestamp")]
    [InlineData("generation-bytes")]
    public async Task FreshExitProofRefusesSourceDrift(string change)
    {
        using var fixture = new Fixture(output);
        var files = fixture.Create();
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        await using var readSet = files.TryBeginReservedValidationReadSet(lease);
        Assert.NotNull(readSet);
        await readSet.OpenAsync();
        Assert.Equal("original", files.ReadFileSync(lease, "present.txt"));
        Assert.Null(files.ReadFileSync(lease, "missing.txt"));
        var path = files.ResolvePath("present.txt");
        var timestamp = File.GetLastWriteTimeUtc(path);
        switch (change)
        {
            case "bytes": File.WriteAllText(path, "modified"); break;
            case "same-time-bytes": File.WriteAllText(path, "modified"); File.SetLastWriteTimeUtc(path, timestamp); break;
            case "delete": File.Delete(path); break;
            case "appearance": File.WriteAllText(files.ResolvePath("missing.txt"), "appeared"); break;
            case "timestamp": File.SetLastWriteTimeUtc(path, timestamp.AddMinutes(1)); break;
            case "generation-bytes": File.AppendAllText(files.SessionGenerationPath, "\n"); break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => readSet.CompleteAsync());
    }

    /// <summary>
    /// Keeps cached source buffers private and preserves lease-free, explicit and asynchronous identities.
    /// </summary>
    [Fact]
    public async Task BuffersAreDetachedAndReadContractsRemainDistinct()
    {
        using var fixture = new Fixture(output);
        var files = fixture.Create();
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        fixture.Recording = true;
        await using var readSet = files.TryBeginReservedValidationReadSet(lease);
        Assert.NotNull(readSet); await readSet.OpenAsync();
        var bytes = files.ReadFileBytesSync(lease, "present.txt")!;
        bytes[0] = 0;
        Assert.Equal("original", files.ReadFileSync(lease, "present.txt"));
        Assert.Equal("original", files.ReadFileSync("present.txt"));
        Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        await readSet.CompleteAsync();
        Assert.Equal(6, fixture.PhysicalReads);
    }

    /// <summary>
    /// Preserves protected reader families and explicit versus lease-free semantics with real native handles.
    /// </summary>
    /// <param name="asynchronous">
    /// Whether to use asynchronous native source reads instead of synchronous ones.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProtectedReadersRetainPhysicalValidationAndDistinctLeaseModes(bool asynchronous)
    {
        using var fixture = new Fixture(output);
        var physical = 0;
        var files = fixture.Create(record => new FileSystemManagerHooks
        {
            OperationTimingObserver = record,
            CanonicalReadActivityObserver = _ => physical++
        });
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        await using var readSet = files.TryBeginReservedValidationReadSet(lease);
        Assert.NotNull(readSet); await readSet.OpenAsync();
        var family = asynchronous ? FileSystemManager.ValidationSnapshotFamily.ProtectedAsync :
            FileSystemManager.ValidationSnapshotFamily.ProtectedSync;
        for (var i = 0; i < 8; i++)
            foreach (var declared in new FileSystemManager.CanonicalWriteLease?[] { lease, null })
            {
                var snapshot = asynchronous
                    ? await readSet.ReadAsync("present.txt", family, declared, CancellationToken.None)
                    : readSet.Read("present.txt", family, declared);
                Assert.Equal("original", FileSystemManager.DecodeFileBytes(snapshot!.Content));
            }
        await readSet.CompleteAsync();
        Assert.Equal(4, physical);
    }

    /// <summary>
    /// Preserves cancellation even for an already observed source and refuses success after that failed consumer.
    /// </summary>
    [Fact]
    public async Task CancelledReuseDoesNotReturnCachedContent()
    {
        using var fixture = new Fixture(output);
        var files = fixture.Create();
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        await using var readSet = files.TryBeginReservedValidationReadSet(lease);
        Assert.NotNull(readSet); await readSet.OpenAsync();
        await readSet.ReadAsync("present.txt", FileSystemManager.ValidationSnapshotFamily.OrdinaryAsync, lease, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => readSet.ReadAsync("present.txt",
            FileSystemManager.ValidationSnapshotFamily.OrdinaryAsync, lease, cancellation.Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => readSet.CompleteAsync());
    }

    /// <summary>
    /// Keeps the original resumer's passive activity and publication/lock observations eligible without inventing reads.
    /// </summary>
    [Fact]
    public async Task ResumerHookConfigurationRemainsEligibleAndPhysicalEventsStayTruthful()
    {
        using var fixture = new Fixture(output);
        var canonical = 0;
        var files = fixture.Create(record => new FileSystemManagerHooks
        {
            OperationTimingObserver = record,
            CanonicalReadActivityObserver = _ => canonical++,
            ExactPhysicalReadActivityObserver = _ => { },
            AfterCanonicalWriteLockOpenedAsync = () => Task.CompletedTask,
            CanonicalWriteLockContendedAsync = () => Task.CompletedTask,
            MainOwnerLockContendedAsync = () => Task.CompletedTask,
            LocalPublicationObserver = (_, _) => { },
            LocalPublicationRecoveryObserver = (_, _) => { }
        });
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        await using var readSet = files.TryBeginReservedValidationReadSet(lease);
        Assert.NotNull(readSet); await readSet.OpenAsync();
        for (var i = 0; i < 12; i++) Assert.Equal("original", files.ReadFileSync(lease, "present.txt"));
        Assert.Equal(1, canonical);
        await readSet.CompleteAsync();
        Assert.Equal(2, canonical);
    }

    /// <summary>
    /// Preserves the real strict signed-absence decision inside a read set, including refusal of a valid legacy original.
    /// </summary>
    /// <param name="signedAbsence">
    /// Whether the actual prepared original retains its signed optional-path presence map.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StrictSignedAbsenceMatchesTheFreshOriginalReader(bool signedAbsence)
    {
        using var fixture = new Fixture(output);
        var files = fixture.Create();
        files.EnsureDirectoryStructure();
        const string required = "game_state/control/pending_mortal_wound_occurrences.json";
        const string optional = "game_state/wounds/mortal_qte_decision_publication.json";
        await files.WriteFileAtomicAsync(required, "{\"schemaVersion\":1,\"occurrences\":[]}");
        await new LiveTurnPreparationService(files).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "reserved-absence-session", RequestId = "reserved-absence-request", TurnNumber = 42,
            CurrentRealm = "Mortal World", PlayerAction = "Check the original", PreGeneratedDices1d20 = [3, 17]
        });
        if (!signedAbsence)
        {
            var manifest = JsonNode.Parse(await files.ReadFileAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath)
                ?? throw new InvalidDataException("Prepared original manifest is missing."))!.AsObject();
            manifest.Remove("originalPathPresenceV1");
            manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
            await files.WriteFileAtomicAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath, manifest.ToJsonString());
            await PendingTurnSnapshotTestAuthority.WriteAuthorityForManifestAsync(files, manifest);
        }
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var weak = PendingTurnSnapshotReader.ReadCurrent(files, lease, new PendingTurnSnapshotPathSelection([required], [optional]));
        Assert.True(weak.Success, JsonSerializer.Serialize(weak.Issues));
        var strict = PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths([required], [optional]);
        var fresh = PendingTurnSnapshotReader.ReadCurrent(files, lease, strict);
        Assert.Equal(signedAbsence, fresh.Success);
        await using var readSet = files.TryBeginReservedValidationReadSet(lease);
        Assert.NotNull(readSet); await readSet.OpenAsync();
        for (var i = 0; i < 2; i++)
        {
            var current = PendingTurnSnapshotReader.ReadCurrent(files, lease, strict);
            Assert.Equal(fresh.Success, current.Success);
            Assert.Equal(JsonSerializer.Serialize(fresh.Issues), JsonSerializer.Serialize(current.Issues));
            if (signedAbsence) Assert.Contains(optional, current.Snapshot!.AbsentLogicalPaths);
        }
        await readSet.CompleteAsync();
    }

    /// <summary>
    /// Leaves unsupported entry on the original fresh path without nesting reservations or adopting mutations.
    /// </summary>
    /// <param name="mode">
    /// Existing reservation, mutation, pending decision or arbitrary read hook that makes entry unsupported.
    /// </param>
    [Theory]
    [InlineData("reservation")]
    [InlineData("mutation")]
    [InlineData("pending")]
    [InlineData("hook")]
    [InlineData("metadata-observer")]
    public async Task UnsupportedEntryRetainsFreshReaders(string mode)
    {
        using var fixture = new Fixture(output);
        var calls = 0;
        var files = fixture.Create(record => new FileSystemManagerHooks
        {
            OperationTimingObserver = record,
            AfterCanonicalReadInitialValidationAsync = mode == "hook" ? _ => { calls++; return Task.CompletedTask; } : null,
            PathMetadataQueryObserver = mode == "metadata-observer" ? _ => { } : null
        });
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        await using var outer = mode == "reservation" ? files.BeginBorrowedPendingSnapshotValidationRead(lease) : null;
        using var mutation = mode == "mutation" ? lease.MainAdmission!.BeginMutation() : null;
        if (mode == "pending") lease.PendingLocalDecision = new object();
        Assert.Null(files.TryBeginReservedValidationReadSet(lease));
        lease.PendingLocalDecision = null;
        fixture.Recording = true;
        Assert.Equal("original", files.ReadFileSync(lease, "present.txt"));
        File.WriteAllText(files.ResolvePath("present.txt"), "modified");
        Assert.Equal("modified", files.ReadFileSync(lease, "present.txt"));
        Assert.Equal(2, fixture.PhysicalReads);
        if (mode == "hook") Assert.Equal(2, calls);
    }

    /// <summary>
    /// Refuses participating writes and teardown during the reservation, then releases it without owning the lease.
    /// </summary>
    [Fact]
    public async Task ReservationExcludesMutationAndTeardownUntilDisposed()
    {
        using var fixture = new Fixture(output);
        var files = fixture.Create();
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        var readSet = files.TryBeginReservedValidationReadSet(lease);
        Assert.NotNull(readSet);
        try
        {
            await readSet.OpenAsync();
            Assert.Throws<InvalidOperationException>(() => lease.MainAdmission!.BeginMutation());
            await Assert.ThrowsAsync<InvalidOperationException>(() => lease.DisposeAsync().AsTask());
            Assert.Equal("original", files.ReadFileSync(lease, "present.txt"));
            await readSet.CompleteAsync();
        }
        finally { await readSet.DisposeAsync(); }
        Assert.True(lease.IsActive);
        using var mutation = lease.MainAdmission!.BeginMutation();
    }

    /// <summary>
    /// Does not allow a retired or foreign execution-flow consumer to reuse retained observations.
    /// </summary>
    /// <param name="mode">
    /// Whether to leave the exact flow or invoke a scope after its disposal.
    /// </param>
    [Theory]
    [InlineData("foreign-flow")]
    [InlineData("disposed")]
    public async Task ForeignOrRetiredScopesRefuse(string mode)
    {
        using var fixture = new Fixture(output);
        var files = fixture.Create();
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        var readSet = files.TryBeginReservedValidationReadSet(lease);
        Assert.NotNull(readSet);
        try
        {
            await readSet.OpenAsync();
            if (mode == "disposed") await readSet.DisposeAsync();
            Task action;
            using (ExecutionContext.SuppressFlow())
                action = Task.Run(() => readSet.Read("present.txt", FileSystemManager.ValidationSnapshotFamily.OrdinarySync, lease));
            await Assert.ThrowsAsync<InvalidOperationException>(() => action);
            if (mode != "disposed") await Assert.ThrowsAsync<InvalidOperationException>(() => readSet.CompleteAsync());
        }
        finally { await readSet.DisposeAsync(); }
        Assert.Equal("original", files.ReadFileSync(lease, "present.txt"));
    }

    /// <summary>
    /// Keeps passive receiver faults and synchronous or flowing asynchronous diagnostic reads outside the functional cache.
    /// </summary>
    /// <param name="mode">
    /// Timing, activity, throwing or asynchronous descendant observation to exercise.
    /// </param>
    [Theory]
    [InlineData("timing-throw")]
    [InlineData("timing-reenter")]
    [InlineData("activity-throw")]
    [InlineData("activity-reenter")]
    [InlineData("async-descendant")]
    public async Task PassiveObserversCannotPoisonFunctionalReads(string mode)
    {
        using var fixture = new Fixture(output);
        FileSystemManager? files = null;
        var active = false;
        var triggered = 0;
        Task? diagnostic = null;
        Exception? diagnosticError = null;
        var synchronousReadSucceeded = false;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Observe()
        {
            if (!active || Interlocked.Exchange(ref triggered, 1) != 0) return;
            if (mode.EndsWith("throw", StringComparison.Ordinal)) throw new IOException("passive receiver");
            if (mode == "async-descendant")
                diagnostic = Task.Run(async () => { await release.Task; Assert.Equal("original", files!.ReadFileSync("present.txt")); });
            else
            {
                try { Assert.Equal("original", files!.ReadFileSync("present.txt")); synchronousReadSucceeded = true; }
                catch (Exception error) { diagnosticError = error; throw; }
            }
        }
        files = fixture.Create(record => new FileSystemManagerHooks
        {
            OperationTimingObserver = (name, boundary, elapsed) =>
            {
                record(name, boundary, elapsed);
                if (mode.StartsWith("timing", StringComparison.Ordinal) || mode == "async-descendant") Observe();
            },
            CanonicalReadActivityObserver = _ => { if (mode.StartsWith("activity", StringComparison.Ordinal)) Observe(); },
            ExactPhysicalReadActivityObserver = _ => { }
        });
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        await using var readSet = files.TryBeginReservedValidationReadSet(lease);
        Assert.NotNull(readSet); await readSet.OpenAsync();
        active = true;
        Assert.Equal("original", files.ReadFileSync(lease, "present.txt"));
        release.TrySetResult();
        if (diagnostic != null) await diagnostic;
        Assert.Equal("original", files.ReadFileSync(lease, "present.txt"));
        await readSet.CompleteAsync();
        Assert.Equal(1, triggered);
        Assert.Null(diagnosticError);
        if (mode is "timing-reenter" or "activity-reenter") Assert.True(synchronousReadSucceeded);
    }

    /// <summary>
    /// Latches actual overlapping functional consumers and refuses both the in-flight result and final proof.
    /// </summary>
    [Fact]
    public async Task ConcurrentFunctionalReadersInvalidateThePhase()
    {
        using var fixture = new Fixture(output);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new ManualResetEventSlim();
        var active = false;
        var files = fixture.Create(record => new FileSystemManagerHooks
        {
            OperationTimingObserver = record,
            CanonicalReadActivityObserver = _ =>
            {
                if (!active) return;
                started.TrySetResult();
                if (!finished.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Contender did not settle.");
            }
        });
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        await using var readSet = files.TryBeginReservedValidationReadSet(lease);
        Assert.NotNull(readSet); await readSet.OpenAsync();
        var contender = Task.Run(async () =>
        {
            await started.Task;
            try { Assert.Throws<InvalidOperationException>(() => files.ReadFileSync(lease, "present.txt")); }
            finally { finished.Set(); }
        });
        active = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => files.ReadFileAsync(lease, "present.txt"));
        await contender;
        await Assert.ThrowsAsync<InvalidOperationException>(() => readSet.CompleteAsync());
        finished.Dispose();
    }

    /// <summary>
    /// Drains a still-running admitted read before releasing ownership after an overlapping owner completion fails.
    /// </summary>
    [Fact]
    public async Task OwnerCloseWaitsForAdmittedReadThenReleasesReservation()
    {
        using var fixture = new Fixture(output);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var active = false;
        var files = fixture.Create(record => new FileSystemManagerHooks
        {
            OperationTimingObserver = record,
            CanonicalReadActivityObserver = _ =>
            {
                if (!active) return;
                started.TrySetResult();
                release.Wait();
            }
        });
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        var readSet = files.TryBeginReservedValidationReadSet(lease);
        Assert.NotNull(readSet); await readSet.OpenAsync();
        active = true;
        var reader = Task.Run(() => files.ReadFileAsync(lease, "present.txt"));
        try
        {
            await started.Task;
            await Assert.ThrowsAsync<InvalidOperationException>(() => readSet.CompleteAsync());
            var closing = readSet.DisposeAsync().AsTask();
            Assert.False(closing.IsCompleted);
            Assert.Throws<InvalidOperationException>(() => lease.MainAdmission!.BeginMutation());
            release.Set();
            await Assert.ThrowsAsync<InvalidOperationException>(() => reader);
            await closing;
            Assert.True(lease.IsActive);
            using var mutation = lease.MainAdmission!.BeginMutation();
            Assert.Equal("original", files.ReadFileSync(lease, "present.txt"));
        }
        finally
        {
            release.Set();
            try { await reader; } catch (InvalidOperationException) { }
            await readSet.DisposeAsync();
        }
    }

    /// <summary>
    /// Owns an isolated marked root, exact timing observations and ordinary file payloads.
    /// </summary>
    /// <param name="output">
    /// Receiver for all observations, including unsuccessful cases.
    /// </param>
    private sealed class Fixture(ITestOutputHelper output) : IDisposable
    {
        private readonly string _nonce = Guid.NewGuid().ToString("N");
        private readonly string _parent = Path.GetFullPath("C:/Temp");
        private readonly ConcurrentQueue<(bool Measured, string Name, string Boundary, double Seconds)> _events = new();
        private string? _root;

        /// <summary>
        /// Gets or sets whether events belong to the measured consumer phase.
        /// </summary>
        internal bool Recording { get; set; }

        /// <summary>
        /// Gets the number of completed ordinary physical snapshot requests in the measured phase.
        /// </summary>
        internal int PhysicalReads => _events.Count(e => e.Measured && e.Boundary == "end" &&
            e.Name is "OrdinaryRead.Sync.Total" or "OrdinaryRead.Async.Total");

        /// <summary>
        /// Reconciles completed request/source timers and native admission probes from their actual nesting.
        /// </summary>
        /// <returns>
        /// Counts of requests, first and exit sources, reuses, added request-only guard/native probes and all recorded probes.
        /// Invalid timing nesting fails the control instead of silently dropping records.
        /// </returns>
        internal (int Requests, int FirstSources, int ExitSources, int Reuses, int AddedFullChecks,
            int AddedNativeProbes, int AllNativeProbes) CountRequestAdmissions()
        {
            var stack = new Stack<string>();
            var requests = 0; var first = 0; var exits = 0; var reuses = 0;
            var full = 0; var addedNative = 0; var allNative = 0;
            foreach (var item in _events)
            {
                if (item.Boundary == "begin") { stack.Push(item.Name); continue; }
                Assert.Equal("end", item.Boundary);
                Assert.NotEmpty(stack);
                Assert.Equal(item.Name, stack.Peek());
                if (item.Measured)
                {
                    if (item.Name is "ValidationReadSet.RequestSync" or "ValidationReadSet.RequestAsync") requests++;
                    if (item.Name is "ValidationReadSet.SourceSync" or "ValidationReadSet.SourceAsync") first++;
                    if (item.Name == "ValidationReadSet.ExitSource") exits++;
                    if (item.Name is "ValidationReadSet.ReuseSync" or "ValidationReadSet.ReuseAsync") reuses++;
                    if (item.Name == "TrustedScope.Probe.GetAttributes") allNative++;
                    if (stack.Any(n => n is "ValidationReadSet.RequestSync" or "ValidationReadSet.RequestAsync") &&
                        !stack.Any(n => n is "ValidationReadSet.SourceSync" or "ValidationReadSet.SourceAsync"))
                    {
                        if (item.Name == "MainAdmission.Validate.GuardCheck") full++;
                        if (item.Name == "TrustedScope.Probe.GetAttributes") addedNative++;
                    }
                }
                stack.Pop();
            }
            Assert.Empty(stack);
            return (requests, first, exits, reuses, full, addedNative, allNative);
        }

        /// <summary>
        /// Creates real ordinary files and a manager using passive timing observations only.
        /// </summary>
        /// <returns>
        /// The actual manager for this fixture's newly owned root.
        /// </returns>
        /// <param name="hooks">
        /// Optional typed hook factory receiving the complete timing recorder; null selects passive timing only.
        /// </param>
        internal FileSystemManager Create(Func<Action<string, string, TimeSpan>, FileSystemManagerHooks>? hooks = null)
        {
            _root = Path.Combine(_parent, "boe-reserved-read-" + _nonce);
            Directory.CreateDirectory(Path.Combine(_root, "game_session"));
            File.WriteAllText(Path.Combine(_root, ".fixture-owner"), _nonce);
            File.WriteAllText(Path.Combine(_root, "game_session", "present.txt"), "original");
            void Record(string name, string boundary, TimeSpan elapsed)
            { _events.Enqueue((Recording, name, boundary, elapsed.TotalSeconds)); }
            return new(_root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance,
                hooks?.Invoke(Record) ?? new FileSystemManagerHooks { OperationTimingObserver = Record });
        }

        /// <summary>
        /// Writes every retained observation and removes only the exact marked ordinary fixture tree.
        /// </summary>
        public void Dispose()
        {
            output.WriteLine(JsonSerializer.Serialize(_events.Select(e => new { e.Measured, e.Name, e.Boundary, e.Seconds })));
            if (_root == null) return;
            var scope = new TrustedLocalFileScope([_parent]);
            if (Path.GetDirectoryName(_root) != _parent ||
                Path.GetFileName(_root) != "boe-reserved-read-" + _nonce ||
                File.ReadAllText(scope.ValidateFile(Path.Combine(_root, ".fixture-owner"), false)) != _nonce)
                throw new InvalidDataException("Fixture ownership changed; root retained.");
            scope.DeleteOwnedTree(_root);
            output.WriteLine(JsonSerializer.Serialize(new { Root = _root, Closing = !Directory.Exists(_root) }));
            Assert.False(Directory.Exists(_root));
        }
    }
}
