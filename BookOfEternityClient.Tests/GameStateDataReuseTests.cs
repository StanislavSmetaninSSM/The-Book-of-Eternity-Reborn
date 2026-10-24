using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises selected GameState source lifetimes using tiny independent real file fixtures.
/// </summary>
/// <param name="output">
/// Receives every collected timing record and the verified owned cleanup result.
/// </param>
public sealed class GameStateDataReuseTests(ITestOutputHelper output)
{
    /// <summary>
    /// Coalesces eight original implicit requests into one initial source and one fresh final proof.
    /// </summary>
    /// <param name="synchronous">
    /// Whether to exercise the original synchronous text reader.
    /// </param>
    /// <returns>
    /// Completion after the real wrapper returns all original text and opens only two sources.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EightImplicitReadsUseOneInitialAndOneFinalSource(bool synchronous)
    {
        using var fixture = new Fixture(output);
        await fixture.InitializeGenerationAsync();
        await SessionOperationContext.RunParticipatingCurrentSessionAsync(fixture.Files, () =>
            fixture.Files.ReadGameStateDataAsync(async () =>
            {
                fixture.Recording = true;
                for (var index = 0; index < 8; index++)
                    Assert.Equal("original", synchronous
                        ? fixture.Files.ReadFileSync("present.txt")
                        : await fixture.Files.ReadFileAsync("present.txt"));
                return true;
            }));
        Assert.Equal(2, fixture.Reads("present.txt"));
    }

    /// <summary>
    /// Reuses sources after a preceding physical phase closes inside the same live participating session.
    /// </summary>
    /// <returns>
    /// Completion after the closed physical frame cannot hide its still-live participating owner.
    /// </returns>
    [Fact]
    public async Task ClosedPriorPhaseLeaseDoesNotHideLiveParticipatingOwner()
    {
        using var fixture = new Fixture(output);
        await fixture.InitializeGenerationAsync();
        await SessionOperationContext.RunParticipatingCurrentSessionAsync(fixture.Files, async () =>
        {
            await using (var prior = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            {
                Assert.Equal("original", await fixture.Files.ReadFileAsync(prior, "present.txt"));
            }
            fixture.Recording = true;
            await fixture.Files.ReadGameStateDataAsync(async () =>
            {
                for (var index = 0; index < 8; index++)
                    Assert.Equal("original", await fixture.Files.ReadFileAsync("present.txt"));
                return true;
            });
        });
        Assert.Equal(2, fixture.Reads("present.txt"));
        Assert.Equal(1, fixture.TimingBegins("GameStateData.Phase"));
        Assert.Equal(0, fixture.TimingBegins("GameStateData.Fallback"));
    }

    /// <summary>
    /// Retains an observed absence once and checks it freshly at completion.
    /// </summary>
    /// <param name="synchronous">
    /// Whether the absent source uses the synchronous reader family.
    /// </param>
    /// <returns>
    /// Completion after two real absence probes and no opened source file.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EightAbsentReadsUseOneInitialAndOneFinalProbe(bool synchronous)
    {
        using var fixture = new Fixture(output);
        await fixture.InitializeGenerationAsync();
        var timer = synchronous ? "OrdinaryRead.Sync.Total" : "OrdinaryRead.Async.Total";
        var before = fixture.TimingBegins(timer);
        await SessionOperationContext.RunParticipatingCurrentSessionAsync(fixture.Files, () =>
            fixture.Files.ReadGameStateDataAsync(async () =>
            {
                fixture.Recording = true;
                for (var index = 0; index < 8; index++)
                    Assert.Null(synchronous ? fixture.Files.ReadFileSync("missing.txt")
                        : await fixture.Files.ReadFileAsync("missing.txt"));
                return true;
            }));
        Assert.Equal(2, fixture.TimingBegins(timer) - before);
        Assert.Equal(0, fixture.Reads("missing.txt"));
        Assert.False(File.Exists(fixture.RawPath("missing.txt")));
    }

    /// <summary>
    /// Keeps the original synchronous, asynchronous, implicit and declared identities distinct.
    /// </summary>
    /// <returns>
    /// Completion after four real source identities have independent initial and final reads.
    /// </returns>
    [Fact]
    public async Task ExistingServicePreservesFourReaderIdentitiesAndSnapshotCopies()
    {
        using var fixture = new Fixture(output);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        fixture.Files.GetOrCreateSessionGeneration(lease);
        using var operation = fixture.Files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        operation.Observe(lease);
        fixture.Recording = true;
        for (var index = 0; index < 8; index++)
        {
            Assert.Equal("original", fixture.Files.ReadFileSync("present.txt"));
            Assert.Equal("original", await fixture.Files.ReadFileAsync("present.txt"));
            Assert.Equal("original", fixture.Files.ReadFileSync(lease, "present.txt"));
            Assert.Equal("original", await fixture.Files.ReadFileAsync(lease, "present.txt"));
            var snapshot = await fixture.Files.ReadFileSnapshotAsync(lease, "present.txt");
            Assert.NotNull(snapshot);
            Assert.Equal("original", Encoding.UTF8.GetString(snapshot.Content));
            snapshot.Content[0] = 0;
        }
        Assert.Equal(4, operation.CaptureState().Entries);
        await operation.CompleteAsync();
        Assert.Equal(8, fixture.Reads("present.txt"));
        Assert.Equal("original", File.ReadAllText(fixture.RawPath("present.txt")));
    }

    /// <summary>
    /// Returns a defensive byte copy from each implicit DATA hit.
    /// </summary>
    /// <param name="synchronous">
    /// Whether the ordinary synchronous byte API supplies each request.
    /// </param>
    /// <returns>
    /// Completion after caller mutations cannot alter later DATA or the actual file.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImplicitBytesCannotMutateRetainedData(bool synchronous)
    {
        using var fixture = new Fixture(output);
        await fixture.InitializeGenerationAsync();
        await SessionOperationContext.RunParticipatingCurrentSessionAsync(fixture.Files, () =>
            fixture.Files.ReadGameStateDataAsync(async () =>
            {
                fixture.Recording = true;
                for (var index = 0; index < 8; index++)
                {
                    var bytes = synchronous ? fixture.Files.ReadFileBytesSync("present.txt")
                        : await fixture.Files.ReadFileBytesAsync("present.txt");
                    Assert.NotNull(bytes);
                    Assert.Equal("original", Encoding.UTF8.GetString(bytes));
                    bytes[0] = 0;
                }
                return true;
            }));
        Assert.Equal(2, fixture.Reads("present.txt"));
        Assert.Equal("original", File.ReadAllText(fixture.RawPath("present.txt")));
    }

    /// <summary>
    /// Ends retained DATA at each wrapper call and observes a supported write in the next call.
    /// </summary>
    /// <param name="writeBetween">
    /// Whether the original atomic writer changes the source between independent calls.
    /// </param>
    /// <returns>
    /// Completion after both calls independently obtain their first source and fresh final proof.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NextCallReadsFreshSources(bool writeBetween)
    {
        using var fixture = new Fixture(output);
        await fixture.InitializeGenerationAsync();
        fixture.Recording = true;
        for (var call = 0; call < 2; call++)
        {
            if (call == 1 && writeBetween)
            {
                fixture.Recording = false;
                await fixture.Files.WriteFileAtomicAsync("present.txt", "changed");
                fixture.Recording = true;
            }
            var expected = call == 1 && writeBetween ? "changed" : "original";
            await SessionOperationContext.RunParticipatingCurrentSessionAsync(fixture.Files, () =>
                fixture.Files.ReadGameStateDataAsync(async () =>
                {
                    for (var index = 0; index < 8; index++)
                        Assert.Equal(expected, await fixture.Files.ReadFileAsync("present.txt"));
                    return true;
                }));
            Assert.Equal((call + 1) * 2, fixture.Reads("present.txt"));
        }
    }

    /// <summary>
    /// Rejects untracked byte, absence or exact generation drift before the wrapper can return success.
    /// </summary>
    /// <param name="change">
    /// The raw source change introduced after repeated genuine reads.
    /// </param>
    /// <returns>
    /// Completion after the changed source prevents the apparent successful result.
    /// </returns>
    [Theory]
    [InlineData("same-time-bytes")]
    [InlineData("appearance")]
    [InlineData("generation")]
    public async Task RawDriftCannotEscapeSourceCompletion(string change)
    {
        using var fixture = new Fixture(output);
        await fixture.InitializeGenerationAsync();
        var timestamp = File.GetLastWriteTimeUtc(fixture.RawPath("present.txt"));
        var failure = await Record.ExceptionAsync(() =>
            SessionOperationContext.RunParticipatingCurrentSessionAsync(fixture.Files, () =>
                fixture.Files.ReadGameStateDataAsync(async () =>
                {
                    for (var index = 0; index < 8; index++)
                    {
                        Assert.Equal("original", await fixture.Files.ReadFileAsync("present.txt"));
                        Assert.Null(await fixture.Files.ReadFileAsync("missing.txt"));
                    }
                    switch (change)
                    {
                        case "same-time-bytes":
                            File.WriteAllText(fixture.RawPath("present.txt"), "modified");
                            File.SetLastWriteTimeUtc(fixture.RawPath("present.txt"), timestamp);
                            break;
                        case "appearance":
                            File.WriteAllText(fixture.RawPath("missing.txt"), "new");
                            break;
                        case "generation":
                            File.AppendAllText(fixture.Files.SessionGenerationPath, "\n");
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(change));
                    }
                    return true;
                })));
        Assert.NotNull(failure);
        if (change == "generation")
            Assert.True(failure is InvalidDataException or SessionReplacedException, failure.ToString());
        else
            Assert.IsType<InvalidDataException>(failure);
    }

    /// <summary>
    /// Preserves original fresh reads when an installed callback may mutate source state.
    /// </summary>
    /// <returns>
    /// Completion after a mid-call raw change is observed by every subsequent original read.
    /// </returns>
    [Fact]
    public async Task MutableReadHookKeepsTheOriginalFreshRoute()
    {
        using var fixture = new Fixture(output, mutableReadHook: true);
        await fixture.InitializeGenerationAsync();
        fixture.Recording = true;
        await SessionOperationContext.RunParticipatingCurrentSessionAsync(fixture.Files, () =>
            fixture.Files.ReadGameStateDataAsync(async () =>
            {
                for (var index = 0; index < 8; index++)
                {
                    if (index == 1) File.WriteAllText(fixture.RawPath("present.txt"), "changed");
                    Assert.Equal(index == 0 ? "original" : "changed",
                        await fixture.Files.ReadFileAsync("present.txt"));
                }
                return true;
            }));
        Assert.Equal(8, fixture.Reads("present.txt"));
        Assert.Equal(0, fixture.TimingBegins("GameStateData.Phase"));
    }

    /// <summary>
    /// Keeps an unbound ordinary read usable without manufacturing a session generation for reuse.
    /// </summary>
    /// <returns>
    /// Completion after eight fresh original sources and no generation file creation.
    /// </returns>
    [Fact]
    public async Task UnboundFallbackDoesNotCreateGeneration()
    {
        using var fixture = new Fixture(output);
        fixture.Recording = true;
        Assert.False(File.Exists(fixture.Files.SessionGenerationPath));
        await fixture.Files.ReadGameStateDataAsync(async () =>
        {
            for (var index = 0; index < 8; index++)
                Assert.Equal("original", await fixture.Files.ReadFileAsync("present.txt"));
            return true;
        });
        Assert.Equal(8, fixture.Reads("present.txt"));
        Assert.False(File.Exists(fixture.Files.SessionGenerationPath));
        Assert.Equal(0, fixture.TimingBegins("GameStateData.Phase"));
    }

    /// <summary>
    /// Leaves an ambient lease with its original caller and retains the unsupported fresh route.
    /// </summary>
    /// <returns>
    /// Completion after the caller can still perform a genuine write with its same active lease.
    /// </returns>
    [Fact]
    public async Task AmbientFallbackDoesNotDisposeTheCallersLease()
    {
        using var fixture = new Fixture(output);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var generation = fixture.Files.GetOrCreateSessionGeneration(lease);
        fixture.Recording = true;
        await SessionOperationContext.RunBoundAsync(fixture.Files, generation, lease, () =>
            fixture.Files.ReadGameStateDataAsync(async () =>
            {
                for (var index = 0; index < 8; index++)
                    Assert.Equal("original", await fixture.Files.ReadFileAsync("present.txt"));
                return true;
            }));
        Assert.Equal(8, fixture.Reads("present.txt"));
        Assert.True(lease.IsActive);
        Assert.Equal(0, fixture.TimingBegins("GameStateData.Phase"));
        await fixture.Files.WriteFileAtomicAsync(lease, "caller-effect.txt", "owned");
        Assert.Equal("owned", File.ReadAllText(fixture.RawPath("caller-effect.txt")));
    }

    /// <summary>
    /// Preserves an independently fresh protocol even when its suspended outer DATA owner has failed.
    /// </summary>
    /// <returns>
    /// Completion after eight original reads without reopening or closing the caller's failed owner.
    /// </returns>
    [Fact]
    public async Task SuspendedFailedOuterOwnerRetainsTheFreshFallback()
    {
        using var fixture = new Fixture(output);
        await fixture.InitializeGenerationAsync();
        using var operation = fixture.Files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        operation.Fail();
        using (operation.Suspend())
        {
            fixture.Recording = true;
            await SessionOperationContext.RunParticipatingCurrentSessionAsync(fixture.Files, () =>
                fixture.Files.ReadGameStateDataAsync(async () =>
                {
                    for (var index = 0; index < 8; index++)
                        Assert.Equal("original", await fixture.Files.ReadFileAsync("present.txt"));
                    return true;
                }));
            Assert.True(operation.IsSuspended);
            Assert.False(operation.CaptureState().Closed);
        }
        Assert.Equal(8, fixture.Reads("present.txt"));
        Assert.Equal(0, fixture.TimingBegins("GameStateData.Phase"));
        Assert.Equal(0, operation.CaptureState().Entries);
    }

    /// <summary>
    /// Preserves the body exception while changed sources unwind and releases owned resources for the next call.
    /// </summary>
    /// <returns>
    /// Completion after the exact primary exception and a fresh successful following lifetime.
    /// </returns>
    [Fact]
    public async Task PrimaryFailureRetiresDataAndReleasesOwnedLease()
    {
        using var fixture = new Fixture(output);
        await fixture.InitializeGenerationAsync();
        fixture.Recording = true;
        var primary = new InvalidOperationException("original body failure");
        var failure = await Record.ExceptionAsync(() =>
            SessionOperationContext.RunParticipatingCurrentSessionAsync(fixture.Files, () =>
                fixture.Files.ReadGameStateDataAsync<bool>(async () =>
                {
                    for (var index = 0; index < 8; index++)
                        Assert.Equal("original", await fixture.Files.ReadFileAsync("present.txt"));
                    File.WriteAllText(fixture.RawPath("present.txt"), "modified");
                    throw primary;
                })));
        Assert.Same(primary, failure);
        var sourcesBeforeNext = fixture.Reads("present.txt");
        await SessionOperationContext.RunParticipatingCurrentSessionAsync(fixture.Files, () =>
            fixture.Files.ReadGameStateDataAsync(async () =>
            {
                for (var index = 0; index < 8; index++)
                    Assert.Equal("modified", await fixture.Files.ReadFileAsync("present.txt"));
                return true;
            }));
        Assert.Equal(2, fixture.Reads("present.txt") - sourcesBeforeNext);
    }

    /// <summary>
    /// Executes actual selected Meta validation and preserves every ordered issue field against fresh fallback.
    /// </summary>
    /// <param name="invalidShape">
    /// Whether the tiny selected file contains two independently invalid top-level fields.
    /// </param>
    /// <returns>
    /// Completion after actual phase admission, unchanged issues and a fresh changed selection.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectedMetaPhasePreservesOrderedIssuesAndNextSelection(bool invalidShape)
    {
        using var cached = new Fixture(output);
        using var fresh = new Fixture(output, mutableReadHook: true);
        var path = AfterlifeSpiritualConflictState.StatePath;
        var content = invalidShape ? "{\"schemaVersion\":1,\"unexpectedA\":1,\"unexpectedB\":2}" : "{}";
        cached.WriteRaw(path, content);
        fresh.WriteRaw(path, content);
        await cached.InitializeGenerationAsync();
        await fresh.InitializeGenerationAsync();
        var cachedValidator = new ValidationService(cached.Files, NullLogger<ValidationService>.Instance);
        var freshValidator = new ValidationService(fresh.Files, NullLogger<ValidationService>.Instance);
        var selection = new GameStateValidationSelection(GameStateValidationPhase.MetaMiscStateFiles, [path]);
        cached.Recording = true;
        fresh.Recording = true;
        var actual = await SessionOperationContext.RunParticipatingCurrentSessionAsync(cached.Files,
            () => cachedValidator.ValidateGameStateAsync(selection));
        var expected = await SessionOperationContext.RunParticipatingCurrentSessionAsync(fresh.Files,
            () => freshValidator.ValidateGameStateAsync(selection));
        Assert.Equal(IssueImages(expected), IssueImages(actual));
        if (invalidShape)
        {
            Assert.Contains(actual, issue => issue.Code == "flexible_state_unknown_top_level_key" && issue.Actual == "unexpectedA");
            Assert.Contains(actual, issue => issue.Code == "strict_state_unknown_top_level_key" && issue.Actual == "unexpectedB");
        }
        else Assert.Empty(actual);
        Assert.Equal(1, cached.TimingBegins("GameStateData.Phase"));
        Assert.Equal(0, fresh.TimingBegins("GameStateData.Phase"));
        Assert.True(cached.Reads(path) >= 2);
        Assert.True(fresh.Reads(path) >= 2);

        var otherPath = "game_state/misc/multipliers.json";
        cached.WriteRaw(otherPath, "{\"multipliers\":[1,1,1,1,1]}");
        cached.WriteRaw(path, "{broken");
        var changedSelection = new GameStateValidationSelection(GameStateValidationPhase.MetaMiscStateFiles, [otherPath]);
        var next = await SessionOperationContext.RunParticipatingCurrentSessionAsync(cached.Files,
            () => cachedValidator.ValidateGameStateAsync(changedSelection));
        Assert.Empty(next);
        Assert.Equal(2, cached.TimingBegins("GameStateData.Phase"));
        var changed = await SessionOperationContext.RunParticipatingCurrentSessionAsync(cached.Files,
            () => cachedValidator.ValidateGameStateAsync(selection));
        Assert.Contains(changed, issue => issue.Code == "flexible_state_invalid_json");
        Assert.Contains(changed, issue => issue.Code == "strict_state_invalid_json");
        Assert.Equal(3, cached.TimingBegins("GameStateData.Phase"));
    }

    /// <summary>
    /// Excludes a timing receiver's captured descendant from the functional DATA lifetime.
    /// </summary>
    /// <returns>
    /// Completion after the original timing callback's descendant reads freshly after wrapper disposal.
    /// </returns>
    [Fact]
    public async Task PassiveTimingDescendantDoesNotJoinFunctionalData()
    {
        using var fixture = new Fixture(output);
        await fixture.InitializeGenerationAsync();
        fixture.WriteRaw("passive.txt", "passive");
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<string?>? passive = null;
        fixture.OnTiming = (name, boundary) =>
        {
            if (name == "OrdinaryRead.Async.Total" && boundary == "begin")
                passive ??= Task.Run(async () =>
                {
                    await release.Task;
                    return await fixture.Files.ReadFileAsync("passive.txt");
                });
        };
        fixture.Recording = true;
        await SessionOperationContext.RunParticipatingCurrentSessionAsync(fixture.Files, async () =>
        {
            try
            {
                await fixture.Files.ReadGameStateDataAsync(async () =>
                {
                    for (var index = 0; index < 8; index++)
                        Assert.Equal("original", await fixture.Files.ReadFileAsync("present.txt"));
                    return true;
                });
            }
            finally { release.TrySetResult(); }
            Assert.NotNull(passive);
            Assert.Equal("passive", await passive);
        });
        Assert.Equal(2, fixture.Reads("present.txt"));
        Assert.Equal(1, fixture.Reads("passive.txt"));
    }

    /// <summary>
    /// Captures all issue data in its original sequence without reducing the comparison to display text.
    /// </summary>
    /// <param name="issues">
    /// The actual ordered issue sequence emitted by a validator invocation.
    /// </param>
    /// <returns>
    /// One complete deterministic image per issue, preserving original ordering.
    /// </returns>
    internal static string[] IssueImages(IEnumerable<ValidationIssue> issues) => issues.Select(issue =>
        JsonSerializer.Serialize(new
        {
            issue.FilePath, issue.Severity, issue.Message, issue.Category, issue.Code, issue.Actor,
            issue.Section, issue.Expected, issue.Actual, issue.RepairHint, issue.RepairTargetFiles
        })).ToArray();

    /// <summary>
    /// Owns only a nonce-marked root, original filesystem manager and uncapped passive observations.
    /// </summary>
    internal sealed class Fixture : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _nonce = Guid.NewGuid().ToString("N");
        private readonly ConcurrentDictionary<string, int> _reads = new(StringComparer.Ordinal);
        private readonly ConcurrentQueue<(string Name, string Boundary, double Milliseconds)> _timings = new();

        /// <summary>
        /// Creates one ordinary source without constructing a world or game engine.
        /// </summary>
        /// <param name="output">
        /// Receives all passive observations after exact owned cleanup.
        /// </param>
        /// <param name="mutableReadHook">
        /// Whether to install a mutation-capable callback, requiring the original fresh fallback.
        /// </param>
        internal Fixture(ITestOutputHelper output, bool mutableReadHook = false)
        {
            _output = output;
            Root = Path.Combine(Path.GetTempPath(), "boe-game-state-data-" + _nonce);
            Directory.CreateDirectory(Path.Combine(Root, "game_session"));
            File.WriteAllText(Path.Combine(Root, ".fixture-owner"), _nonce);
            File.WriteAllText(Path.Combine(Root, "game_session", "present.txt"), "original");
            Files = new FileSystemManager(Root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    CanonicalReadActivityObserver = path =>
                    {
                        if (Recording) _reads.AddOrUpdate(path, 1, (_, count) => count + 1);
                    },
                    OperationTimingObserver = (name, boundary, elapsed) =>
                    {
                        _timings.Enqueue((name, boundary, elapsed.TotalMilliseconds));
                        OnTiming?.Invoke(name, boundary);
                    },
                    AfterCanonicalReadInitialValidationAsync = mutableReadHook ? _ => Task.CompletedTask : null
                });
        }

        /// <summary>
        /// Gets the verified exclusively owned physical root.
        /// </summary>
        internal string Root { get; }

        /// <summary>
        /// Gets the actual filesystem manager used for every admission and read.
        /// </summary>
        internal FileSystemManager Files { get; }

        /// <summary>
        /// Gets or sets whether actual source observations contribute to the counters.
        /// </summary>
        internal bool Recording { get; set; }

        /// <summary>
        /// Gets or sets the passive timing receiver whose captured descendants retain diagnostic routing.
        /// </summary>
        internal Action<string, string>? OnTiming { get; set; }

        /// <summary>
        /// Resolves a fixture-owned constant path for deliberate raw storage changes.
        /// </summary>
        /// <param name="path">
        /// A test-defined relative path under this fixture's session directory.
        /// </param>
        /// <returns>
        /// The absolute session path without performing a production read or mutation.
        /// </returns>
        internal string RawPath(string path) => Path.Combine(Root, "game_session", path);

        /// <summary>
        /// Writes a minimal raw fixture input and creates only its own parent directories.
        /// </summary>
        /// <param name="path">
        /// A test-defined relative path under the owned session.
        /// </param>
        /// <param name="content">
        /// Exact input text to place in the fixture before validation.
        /// </param>
        internal void WriteRaw(string path, string content)
        {
            var target = RawPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, content);
        }

        /// <summary>
        /// Counts all actual timer entries for a production boundary without limiting diagnostic collection.
        /// </summary>
        /// <param name="name">
        /// The exact production timing scope name.
        /// </param>
        /// <returns>
        /// The complete count of recorded begin boundaries.
        /// </returns>
        internal int TimingBegins(string name) => _timings.Count(row => row.Name == name && row.Boundary == "begin");

        /// <summary>
        /// Counts actual observed canonical source openings for an exact original relative spelling.
        /// </summary>
        /// <param name="path">
        /// The original relative spelling whose recorded observations are counted.
        /// </param>
        /// <returns>
        /// The complete number of observed reads, including zero for an unobserved path.
        /// </returns>
        internal int Reads(string path) => _reads.GetValueOrDefault(path);

        /// <summary>
        /// Establishes a genuine existing generation before the read-only wrapper is entered.
        /// </summary>
        /// <returns>
        /// Completion after the original generation publication and owned lease release.
        /// </returns>
        internal async Task InitializeGenerationAsync()
        {
            await using var lease = await Files.AcquireCanonicalWriteLeaseAsync();
            Files.GetOrCreateSessionGeneration(lease);
        }

        /// <summary>
        /// Deletes only the verified nonce-owned tree and writes every collected diagnostic record.
        /// </summary>
        public void Dispose()
        {
            try
            {
                Assert.Equal("boe-game-state-data-" + _nonce, Path.GetFileName(Root));
                Assert.Equal(_nonce, File.ReadAllText(Path.Combine(Root, ".fixture-owner")));
                new TrustedLocalFileScope([Path.GetTempPath()]).DeleteOwnedTree(Root);
                Assert.False(Directory.Exists(Root));
            }
            finally
            {
                foreach (var row in _timings)
                    _output.WriteLine(JsonSerializer.Serialize(new { row.Name, row.Boundary, row.Milliseconds }));
                foreach (var row in _reads)
                    _output.WriteLine(JsonSerializer.Serialize(new { Path = row.Key, Sources = row.Value }));
                _output.WriteLine(JsonSerializer.Serialize(new { Root, Removed = !Directory.Exists(Root) }));
            }
        }
    }
}
