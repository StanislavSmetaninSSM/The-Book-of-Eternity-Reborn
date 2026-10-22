using System.Collections.Concurrent;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises real filesystem DATA reuse without constructing a game world or authority graph.
/// </summary>
/// <param name="output">
/// Receiver for every measured section and owned fixture closing result.
/// </param>
public sealed class RecoveryFileValidationTests(ITestOutputHelper output)
{
    /// <summary>
    /// Compares eight real source requests with and without the production reuse path.
    /// </summary>
    /// <param name="name">
    /// Distinct stable algorithm name used by the two production consumers.
    /// </param>
    /// <param name="reuse">
    /// Whether the same source body may reuse an eligible successful fact.
    /// </param>
    /// <param name="expectedBodies">
    /// Expected real source executions, independently of request count.
    /// </param>
    [Theory]
    [InlineData("Mechanical", true, 1)]
    [InlineData("Continuation", true, 1)]
    [InlineData("Mechanical", false, 8)]
    [InlineData("Continuation", false, 8)]
    public async Task EightRealRequestsReuseOnlySourceData(string name, bool reuse, int expectedBodies)
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var scope = files.BeginRecoveryValidation(lease, reuse);
        var rule = new OperationValidationRule(name, 1);
        var input = new object();
        var bodies = 0;
        var firstAttributes = 0;
        fixture.Recording = true;
        for (var i = 0; i < 8; i++)
        {
            await files.ValidateRecoveryDataAsync(rule, input, lease, async () =>
            {
                bodies++;
                Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
                Assert.Null(await files.ReadFileAsync(lease, "absent.txt"));
            });
            if (i == 0) firstAttributes = fixture.Attributes;
        }
        output.WriteLine(JsonSerializer.Serialize(new { name, reuse, Requests = 8, Bodies = bodies,
            Attributes = fixture.Attributes, FirstAttributes = firstAttributes, State = scope.CaptureState() }));
        Assert.Equal(expectedBodies, bodies);
        if (OperatingSystem.IsWindows()) Assert.True(firstAttributes > 0);
        Assert.Equal(reuse ? firstAttributes : firstAttributes * 8, fixture.Attributes);
        Assert.Equal(reuse ? 1 : 0, scope.CaptureState().Entries);
    }

    /// <summary>
    /// Invalidates successes for changed, same-byte and failed supported writes.
    /// </summary>
    /// <param name="kind">
    /// Changed, same-byte or wrong-type failing writer to exercise.
    /// </param>
    [Theory]
    [InlineData("changed")]
    [InlineData("same")]
    [InlineData("failed")]
    public async Task EveryWriteAttemptInvalidatesIncludingNoOpAndFailure(string kind)
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var scope = files.BeginRecoveryValidation(lease);
        var rule = new OperationValidationRule("Mechanical", 1); var input = new object(); var bodies = 0;
        var expected = "original";
        Task Validate() => files.ValidateRecoveryDataAsync(rule, input, lease, async () =>
        { bodies++; Assert.Equal(expected, await files.ReadFileAsync(lease, "present.txt")); });
        await Validate(); await Validate();
        Assert.Equal(1, bodies);
        if (kind == "failed")
            await Assert.ThrowsAnyAsync<Exception>(() => files.WriteFileAtomicAsync(lease, "directory", "bad"));
        else
        {
            expected = kind == "changed" ? "new" : "original";
            await files.WriteFileAtomicAsync(lease, "present.txt", expected);
        }
        await Validate(); await Validate();
        Assert.Equal(2, bodies);
    }

    /// <summary>
    /// Treats observed absence as source data invalidated by creation and deletion.
    /// </summary>
    [Fact]
    public async Task AbsenceCreateDeleteAndDirectoryChangesInvalidate()
    {
        using var fixture = new Fixture(output); var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var scope = files.BeginRecoveryValidation(lease);
        var rule = new OperationValidationRule("Continuation", 1); var input = new object(); var bodies = 0;
        string? expected = null;
        Task Validate() => files.ValidateRecoveryDataAsync(rule, input, lease, async () =>
        { bodies++; Assert.Equal(expected, await files.ReadFileAsync(lease, "absent.txt")); });
        await Validate(); await Validate(); Assert.Equal(1, bodies);
        await files.WriteFileAtomicAsync(lease, "absent.txt", "created"); expected = "created";
        await Validate(); Assert.Equal(2, bodies);
        files.DeleteFile(lease, "absent.txt"); expected = null;
        await Validate(); Assert.Equal(3, bodies);
        files.EnsureDirectoryStructure(lease);
        await Validate(); Assert.Equal(4, bodies);
    }

    /// <summary>
    /// Refuses reuse throughout an outer writer even when nested writers have already closed.
    /// </summary>
    [Fact]
    public async Task ActiveOuterMutationRunsFreshAndRetiresTheEarlierEpoch()
    {
        using var fixture = new Fixture(output); var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        using var scope = files.BeginRecoveryValidation(lease);
        var rule = new OperationValidationRule("Mechanical", 1); var input = new object(); var bodies = 0;
        Task Validate() => files.ValidateRecoveryDataAsync(rule, input, lease, async () =>
        { bodies++; Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt")); });
        await Validate(); await Validate(); Assert.Equal(1, bodies);
        using (lease.MainAdmission!.BeginMutation())
        {
            using (lease.MainAdmission.BeginMutation()) { }
            await Validate(); await Validate();
        }
        await Validate(); await Validate(); Assert.Equal(4, bodies);
    }

    /// <summary>
    /// Shares invalidation with a second manager's actual original MainAccess reservation.
    /// </summary>
    [Fact]
    public async Task AnotherManagerMutationCannotReuseTheFirstManagersFacts()
    {
        using var fixture = new Fixture(output); var files = fixture.Files;
        var other = new FileSystemManager(fixture.Root, NullLogger<FileSystemManager>.Instance);
        Assert.Same(files.CanonicalRootAuthorityIdentity, other.CanonicalRootAuthorityIdentity);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        using var scope = files.BeginRecoveryValidation(lease);
        var rule = new OperationValidationRule("Mechanical", 1); var input = new object(); var bodies = 0;
        Task Validate() => files.ValidateRecoveryDataAsync(rule, input, lease, async () =>
        { bodies++; Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt")); });
        await Validate(); await Validate(); Assert.Equal(1, bodies);
        using (var admission = other.BeginMainAdmission())
        {
            await admission.AcquireAsync();
            using (admission.BeginMutation()) { await Validate(); }
        }
        await Validate(); await Validate(); Assert.Equal(3, bodies);
    }

    /// <summary>
    /// Never inserts an observation whose root changed while its source body was running.
    /// </summary>
    [Fact]
    public async Task MutationDuringSourceRefusesTheInFlightReusableFact()
    {
        using var fixture = new Fixture(output); var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        using var scope = files.BeginRecoveryValidation(lease);
        var rule = new OperationValidationRule("Mechanical", 1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => files.ValidateRecoveryDataAsync(rule, new object(), lease, async () =>
        {
            Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
            await files.WriteFileAtomicAsync(lease, "present.txt", "changed");
        }));
        Assert.Equal(0, scope.CaptureState().Entries);
    }

    /// <summary>
    /// Executes all nested source bodies inside a fresh boundary and clears earlier successes afterward.
    /// </summary>
    [Fact]
    public async Task FreshBoundaryDoesNotRevivePreCallbackSuccesses()
    {
        using var fixture = new Fixture(output); var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        using var scope = files.BeginRecoveryValidation(lease);
        var rule = new OperationValidationRule("Mechanical", 1); var input = new object(); var bodies = 0;
        Task Validate() => files.ValidateRecoveryDataAsync(rule, input, lease, async () =>
        { bodies++; Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt")); });
        await Validate(); await Validate(); Assert.Equal(1, bodies);
        using (files.ForceFreshRecoveryValidation()) { await Validate(); await Validate(); }
        await Validate(); await Validate(); Assert.Equal(4, bodies);
    }

    /// <summary>
    /// Keeps a matching bounded QTE read reservation eligible without making it the fact identity.
    /// </summary>
    [Fact]
    public async Task SeparateReservedReadPhasesShareOnlyTheOuterSemanticFact()
    {
        using var fixture = new Fixture(output); var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var scope = files.BeginRecoveryValidation(lease);
        var rule = new OperationValidationRule("Mechanical", 1); var input = new object(); var bodies = 0;
        for (var i = 0; i < 2; i++)
        {
            await using var readSet = files.TryBeginReservedValidationReadSet(lease);
            Assert.NotNull(readSet); await readSet.OpenAsync();
            await files.ValidateRecoveryDataAsync(rule, input, lease, async () =>
            { bodies++; Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt")); });
            await readSet.CompleteAsync();
        }
        Assert.Equal(1, bodies);
    }

    /// <summary>
    /// Refuses a descendant that captured a scope before its whole operation closed.
    /// </summary>
    [Fact]
    public async Task ClosedScopeRejectsCapturedDescendantsAndReleasesData()
    {
        using var fixture = new Fixture(output); var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var scope = files.BeginRecoveryValidation(lease);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var child = Task.Run(async () =>
        {
            await gate.Task;
            await files.ValidateRecoveryDataAsync(new("Mechanical", 1), new object(), lease, () => Task.CompletedTask);
        });
        scope.Dispose(); gate.SetResult();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => child);
        Assert.Equal(new ValidationOperationState(0, 0, true), scope.CaptureState());
    }

    /// <summary>
    /// Keeps arbitrary read callbacks on the original uncached route.
    /// </summary>
    [Fact]
    public async Task ArbitraryReadHookExcludesReuse()
    {
        using var fixture = new Fixture(output, arbitraryReadHook: true); var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        using var scope = files.BeginRecoveryValidation(lease);
        var rule = new OperationValidationRule("Mechanical", 1); var input = new object(); var bodies = 0;
        for (var i = 0; i < 3; i++) await files.ValidateRecoveryDataAsync(rule, input, lease, async () =>
        { bodies++; Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt")); });
        Assert.Equal(3, bodies); Assert.Equal(0, scope.CaptureState().Entries);
    }

    /// <summary>
    /// Keeps the documented passive metadata receiver eligible without losing any of its real observations.
    /// </summary>
    [Fact]
    public async Task PassiveMetadataDiagnosticsDoNotDisableSourceReuse()
    {
        using var fixture = new Fixture(output, passiveMetadata: true); var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var scope = files.BeginRecoveryValidation(lease);
        var rule = new OperationValidationRule("Mechanical", 1); var input = new object(); var bodies = 0;
        for (var i = 0; i < 4; i++) await files.ValidateRecoveryDataAsync(rule, input, lease, async () =>
        { bodies++; Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt")); });
        Assert.Equal(1, bodies);
        if (OperatingSystem.IsWindows()) Assert.True(fixture.MetadataQueries > 0);
    }

    /// <summary>
    /// Keeps the original generation segment fresh after its in-memory generation revision changes.
    /// </summary>
    [Fact]
    public async Task GenerationRevisionChangeCannotReviveAnOldSegment()
    {
        using var fixture = new Fixture(output); var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var scope = files.BeginRecoveryValidation(lease);
        var rule = new OperationValidationRule("Mechanical", 1); var input = new object(); var bodies = 0;
        Task Validate() => files.ValidateRecoveryDataAsync(rule, input, lease, async () =>
        { bodies++; Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt")); });
        await Validate(); await Validate(); Assert.Equal(1, bodies);
        files.CanonicalRootAuthorityIdentity.AdvanceSessionGenerationRevision();
        await Validate(); await Validate(); Assert.Equal(3, bodies);
    }

    /// <summary>
    /// Keeps captured diagnostic descendants outside the functional coordinator without dropping observations.
    /// </summary>
    [Fact]
    public async Task PassiveDiagnosticDescendantCannotJoinOrPoisonTheOperation()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? diagnostic = null; FileSystemManager files = null!; FileSystemManager.CanonicalWriteLease lease = null!;
        var diagnosticBodies = 0;
        using var fixture = new Fixture(output, observation: (name, boundary) =>
        {
            if (name != "RecoveryValidation.Source.Mechanical" || boundary != "begin" || diagnostic is not null) return;
            diagnostic = Task.Run(async () =>
            {
                await release.Task;
                await files.ValidateRecoveryDataAsync(new("Diagnostic", 1), new object(), lease, async () =>
                { diagnosticBodies++; Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt")); });
            });
        });
        files = fixture.Files;
        await using var retainedLease = await files.AcquireCanonicalWriteLeaseAsync(); lease = retainedLease;
        using var scope = files.BeginRecoveryValidation(lease);
        var rule = new OperationValidationRule("Mechanical", 1); var input = new object(); var bodies = 0;
        Task Validate() => files.ValidateRecoveryDataAsync(rule, input, lease, async () =>
        { bodies++; Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt")); });
        fixture.Recording = true;
        await Validate(); await Validate();
        Assert.NotNull(diagnostic); release.SetResult(); await diagnostic;
        await Validate(); Assert.Equal(1, bodies); Assert.Equal(1, diagnosticBodies);
        Assert.Equal(1, scope.CaptureState().Entries);
    }

    /// <summary>
    /// Requires fresh source validation to reject raw drift at the publication fence.
    /// </summary>
    [Fact]
    public async Task FreshFenceRejectsPersistentExternalDrift()
    {
        using var fixture = new Fixture(output); var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        using var scope = files.BeginRecoveryValidation(lease);
        var rule = new OperationValidationRule("Mechanical", 1); var input = new object(); var bodies = 0;
        Task Validate() => files.ValidateRecoveryDataAsync(rule, input, lease, async () =>
        {
            bodies++;
            if (await files.ReadFileAsync(lease, "present.txt") != "original")
                throw new InvalidDataException("Actual source bytes changed.");
        });
        await Validate(); await Validate(); Assert.Equal(1, bodies);
        File.WriteAllText(files.ResolvePath("present.txt"), "external");
        using (files.ForceFreshRecoveryValidation())
            await Assert.ThrowsAsync<InvalidDataException>(Validate);
        Assert.Equal(2, bodies); Assert.True(scope.CaptureState().Closed);
    }

    /// <summary>
    /// Validates a genuine minimal issued original through the same successful-fact service without minting game authority.
    /// </summary>
    [Fact]
    public async Task GenuineIssuedOriginalEvidenceReusesOnlyItsSourceRecheck()
    {
        using var fixture = new Fixture(output); var files = fixture.Files;
        files.EnsureDirectoryStructure();
        const string path = "game_state/control/pending_mortal_wound_occurrences.json";
        await files.WriteFileAtomicAsync(path, "{\"schemaVersion\":1,\"occurrences\":[]}");
        await new LiveTurnPreparationService(files).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "recovery-cache-original", RequestId = "recovery-cache-request", TurnNumber = 7,
            CurrentRealm = "Mortal World", PlayerAction = "Проверить оригинал.", PreGeneratedDices1d20 = [3, 17]
        });
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var read = PendingTurnSnapshotReader.ReadObservedCurrent(files, lease, new[] { path });
        Assert.True(read.Success, JsonSerializer.Serialize(read.Issues));
        var original = Assert.IsType<PendingTurnSnapshotReadAuthority>(read.Snapshot);
        var evidence = Assert.IsType<PendingTurnSnapshotReader.OriginalReadEvidence>(read.Evidence);
        using var scope = files.BeginRecoveryValidation(lease);
        var rule = new OperationValidationRule("Mechanical", 1); var bodies = 0; var liveGuards = 0;
        for (var i = 0; i < 8; i++)
        {
            files.EnsureCanonicalWriteLeaseActive(lease);
            evidence.RequireOwner(files, lease, original); liveGuards++;
            await files.ValidateRecoveryDataAsync(rule, evidence, lease, () =>
            { bodies++; evidence.RequireCurrent(files, lease, original); return Task.CompletedTask; });
        }
        Assert.Equal(8, liveGuards); Assert.Equal(1, bodies);
        using (files.ForceFreshRecoveryValidation())
            await files.ValidateRecoveryDataAsync(rule, evidence, lease, () =>
            { bodies++; evidence.RequireCurrent(files, lease, original); return Task.CompletedTask; });
        Assert.Equal(2, bodies);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Task.Run(() => evidence.RequireOwner(
            new FileSystemManager(fixture.Root, NullLogger<FileSystemManager>.Instance), lease, original)));
    }

    /// <summary>
    /// Owns a tiny independent filesystem and emits all measured timing rows before safe removal.
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _parent = Path.GetFullPath(Path.GetTempPath());
        private readonly string _nonce = Guid.NewGuid().ToString("N");
        private readonly ConcurrentQueue<(string Name, string Boundary, double Seconds)> _events = new();
        private int _attributes;
        private int _metadataQueries;

        /// <summary>
        /// Creates only the real files needed by this test's source body.
        /// </summary>
        /// <param name="output">
        /// Receiver for complete timing observations and safe closing evidence.
        /// </param>
        /// <param name="arbitraryReadHook">
        /// Whether to install a functional read callback that must exclude reuse.
        /// </param>
        /// <param name="observation">
        /// Optional passive receiver called for each measured timing boundary; null adds no receiver.
        /// </param>
        /// <param name="passiveMetadata">
        /// Whether to record actual metadata queries through their documented passive receiver.
        /// </param>
        internal Fixture(ITestOutputHelper output, bool arbitraryReadHook = false,
            Action<string, string>? observation = null, bool passiveMetadata = false)
        {
            _output = output;
            Root = Path.Combine(_parent, "boe-recovery-cache-" + _nonce);
            Directory.CreateDirectory(Path.Combine(Root, "game_session", "directory"));
            File.WriteAllText(Path.Combine(Root, ".fixture-owner"), _nonce);
            File.WriteAllText(Path.Combine(Root, "game_session", "present.txt"), "original");
            Files = new(Root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
                new FileSystemManagerHooks
                {
                    OperationTimingObserver = (name, boundary, elapsed) =>
                    {
                        if (!Recording) return;
                        _events.Enqueue((name, boundary, elapsed.TotalSeconds));
                        if (name == "TrustedScope.Probe.GetAttributes" && boundary == "end")
                            Interlocked.Increment(ref _attributes);
                        observation?.Invoke(name, boundary);
                    },
                    BeforeCanonicalReadOpenAsync = arbitraryReadHook ? _ => Task.CompletedTask : null,
                    PathMetadataQueryObserver = passiveMetadata ? _ => Interlocked.Increment(ref _metadataQueries) : null
                });
        }

        /// <summary>
        /// Gets this test's exact owned root.
        /// </summary>
        internal string Root { get; }

        /// <summary>
        /// Gets the real manager with passive unbounded observations.
        /// </summary>
        internal FileSystemManager Files { get; }

        /// <summary>
        /// Gets or sets whether source measurement has started after fixture preparation.
        /// </summary>
        internal bool Recording { get; set; }

        /// <summary>
        /// Gets the number of actual completed attribute probes in measured source work.
        /// </summary>
        internal int Attributes => Volatile.Read(ref _attributes);

        /// <summary>
        /// Gets the number of actual passive metadata records delivered without a quota.
        /// </summary>
        internal int MetadataQueries => Volatile.Read(ref _metadataQueries);

        /// <summary>
        /// Emits every retained row and removes only the exact marked fixture tree.
        /// </summary>
        public void Dispose()
        {
            _output.WriteLine(JsonSerializer.Serialize(_events.Select(e => new { e.Name, e.Boundary, e.Seconds })));
            if (Path.GetDirectoryName(Root) != Path.TrimEndingDirectorySeparator(_parent) ||
                Path.GetFileName(Root) != "boe-recovery-cache-" + _nonce ||
                File.ReadAllText(Path.Combine(Root, ".fixture-owner")) != _nonce)
                throw new InvalidDataException("Fixture ownership changed; preserve its root.");
            new TrustedLocalFileScope([_parent]).DeleteOwnedTree(Root);
            _output.WriteLine(JsonSerializer.Serialize(new { Root, Removed = !Directory.Exists(Root) }));
            Assert.False(Directory.Exists(Root));
        }
    }
}
