using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises completed-proof DATA inside the real trusted-local prepared publication callback.
/// </summary>
/// <param name="output">
/// Receives every passive timing record and the verified nonce-owned cleanup result.
/// </param>
public sealed class CleanupProofDataReuseTests(ITestOutputHelper output)
{
    /// <summary>
    /// Coalesces eight same-family proof reads after actual recovery without changing publication images.
    /// </summary>
    /// <param name="synchronous">
    /// Whether the callback uses the original synchronous byte reader rather than its asynchronous family.
    /// </param>
    /// <returns>
    /// Completion after committed target bytes, retained bytes and two actual proof source reads are verified.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreparedProofUsesOneInitialAndOneFreshCompletionSource(bool synchronous)
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        var callbacks = 0;
        var outcome = await files.PublishLocalFilesAsync(lease,
            [new CanonicalLocalFileChange("present.txt", Encoding.UTF8.GetBytes("original"), Encoding.UTF8.GetBytes("published"))],
            validatePreparedNamespace: async () =>
            {
                callbacks++;
                files.ResolveBackupPublicationRecovery(lease);
                fixture.Recording = true;
                try
                {
                    var result = await files.ReadCleanupCompletedProofDataAsync(lease, async () =>
                    {
                        for (var index = 0; index < 8; index++)
                        {
                            var bytes = synchronous ? files.ReadFileBytesSync(lease, "present.txt")
                                : await files.ReadFileBytesAsync(lease, "present.txt");
                            Assert.NotNull(bytes);
                            Assert.Equal("original", Encoding.UTF8.GetString(bytes));
                            bytes[0] = 0;
                        }
                        return 17;
                    });
                    Assert.Equal(17, result);
                }
                finally { fixture.Recording = false; }
            },
            reusePreparedProofData: true);

        Assert.Equal(TrustedLocalPublicationDisposition.Committed, outcome.Disposition);
        Assert.Null(outcome.Failure);
        Assert.Equal(1, callbacks);
        Assert.True(lease.IsActive);
        Assert.Equal(Encoding.UTF8.GetBytes("published"), File.ReadAllBytes(fixture.PresentPath));
        Assert.Equal(Encoding.UTF8.GetBytes("retained"), File.ReadAllBytes(fixture.RetainedPath));
        Assert.Equal(Encoding.UTF8.GetBytes("published"), await files.ReadFileBytesAsync(lease, "present.txt"));
        Assert.Equal(Encoding.UTF8.GetBytes("retained"), await files.ReadFileBytesAsync(lease, "retained.txt"));
        Assert.Equal(2, fixture.ProofSources);
    }

    /// <summary>
    /// Preserves fresh original reads without the opt-in or with an extra active mutation identity.
    /// </summary>
    /// <param name="extraMutation">
    /// Whether a genuine third mutation is held while the opted-in helper executes.
    /// </param>
    /// <returns>
    /// Completion after a committed publication and eight independent original proof sources.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnsupportedPublisherWindowKeepsEightFreshSources(bool extraMutation)
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        var outcome = await fixture.PublishAsync(lease, async () =>
        {
            using var mutation = extraMutation ? files.BeginVerifiedSourceEffect(lease) : null;
            Assert.True(await fixture.MeasureAsync(() => files.ReadCleanupCompletedProofDataAsync(lease, async () =>
            {
                for (var index = 0; index < 8; index++)
                    Assert.Equal("retained", await files.ReadFileAsync(lease, "retained.txt"));
                return true;
            })));
        }, optIn: extraMutation);
        AssertCommitted(outcome);
        Assert.Equal("published", File.ReadAllText(fixture.PresentPath));
        Assert.Equal("retained", File.ReadAllText(fixture.RetainedPath));
        Assert.Equal(8, fixture.ProofSources);
    }

    /// <summary>
    /// Creates a fresh temporary service after each real recovery rather than retaining prior window DATA.
    /// </summary>
    /// <returns>
    /// Completion after two independent windows observe their own source bytes and fresh completion proofs.
    /// </returns>
    [Fact]
    public async Task SequentialWindowsReadFreshSourcesAfterRecovery()
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        var outcome = await fixture.PublishAsync(lease, async () =>
        {
            for (var window = 0; window < 2; window++)
            {
                if (window == 1)
                {
                    File.WriteAllText(fixture.RetainedPath, "modified");
                    files.ResolveBackupPublicationRecovery(lease);
                }
                var expected = window == 0 ? "retained" : "modified";
                var before = fixture.ProofSources;
                Assert.True(await fixture.MeasureAsync(() => files.ReadCleanupCompletedProofDataAsync(lease, async () =>
                {
                    for (var index = 0; index < 8; index++)
                        Assert.Equal(expected, await files.ReadFileAsync(lease, "retained.txt"));
                    return true;
                })));
                Assert.Equal(2, fixture.ProofSources - before);
            }
        });
        AssertCommitted(outcome);
        Assert.Equal("published", File.ReadAllText(fixture.PresentPath));
        Assert.Equal("modified", File.ReadAllText(fixture.RetainedPath));
        Assert.Equal(4, fixture.ProofSources);
    }

    /// <summary>
    /// Rejects changed retained bytes, newly present sources and replaced generation before target publication.
    /// </summary>
    /// <param name="change">
    /// The raw source change introduced after the repeated original reads.
    /// </param>
    /// <returns>
    /// Completion after fresh source refusal leaves the declared target at its original image.
    /// </returns>
    [Theory]
    [InlineData("same-time-bytes")]
    [InlineData("appearance")]
    [InlineData("generation")]
    public async Task RawDriftRefusesBeforePublication(string change)
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var generation = files.GetOrCreateSessionGeneration(lease);
        var timestamp = File.GetLastWriteTimeUtc(fixture.RetainedPath);
        var escaped = false;
        var failure = await Record.ExceptionAsync(() => fixture.PublishAsync(lease, async () =>
        {
            await fixture.MeasureAsync(() => files.ReadCleanupCompletedProofDataAsync(lease, async () =>
            {
                for (var index = 0; index < 8; index++)
                {
                    if (change == "appearance")
                        Assert.Null(await files.ReadFileBytesAsync(lease, "missing.txt"));
                    else
                        Assert.Equal("retained", await files.ReadFileAsync(lease, "retained.txt"));
                }
                switch (change)
                {
                    case "same-time-bytes":
                        File.WriteAllText(fixture.RetainedPath, "modified");
                        File.SetLastWriteTimeUtc(fixture.RetainedPath, timestamp);
                        break;
                    case "appearance":
                        File.WriteAllText(Path.Combine(fixture.Root, "game_session", "missing.txt"), "appeared");
                        break;
                    case "generation":
                        var generationText = File.ReadAllText(files.SessionGenerationPath);
                        Assert.Contains(generation, generationText, StringComparison.Ordinal);
                        File.WriteAllText(files.SessionGenerationPath,
                            generationText.Replace(generation, Guid.NewGuid().ToString("N"), StringComparison.Ordinal));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(change));
                }
                return true;
            }));
            escaped = true;
        }));
        Assert.IsType<InvalidDataException>(failure);
        Assert.False(escaped);
        Assert.Equal("original", File.ReadAllText(fixture.PresentPath));
        Assert.True(lease.IsActive);
    }

    /// <summary>
    /// Detects a root mutation that begins and ends during the window even when source bytes remain equal.
    /// </summary>
    /// <returns>
    /// Completion after the changed epoch prevents cached DATA and target publication from succeeding.
    /// </returns>
    [Fact]
    public async Task InterveningRootMutationCannotRestoreWindowValidity()
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        var transitioned = false;
        var failure = await Record.ExceptionAsync(() => fixture.PublishAsync(lease, async () =>
        {
            await fixture.MeasureAsync(() => files.ReadCleanupCompletedProofDataAsync(lease, async () =>
            {
                Assert.Equal("retained", await files.ReadFileAsync(lease, "retained.txt"));
                var mutation = new object();
                files.CanonicalRootAuthorityIdentity.ValidationEpoch.Begin(mutation);
                files.CanonicalRootAuthorityIdentity.ValidationEpoch.End(mutation);
                transitioned = true;
                Assert.Equal("retained", await files.ReadFileAsync(lease, "retained.txt"));
                return true;
            }));
        }));
        Assert.True(transitioned);
        Assert.True(failure is InvalidDataException or InvalidOperationException, failure?.ToString());
        Assert.Equal("original", File.ReadAllText(fixture.PresentPath));
        Assert.Equal("retained", File.ReadAllText(fixture.RetainedPath));
    }

    /// <summary>
    /// Refuses a real supported writer while its original access is reserved for proof reading.
    /// </summary>
    /// <returns>
    /// Completion after neither the attempted effect nor the enclosing target publication occurs.
    /// </returns>
    [Fact]
    public async Task ActiveWindowRejectsSupportedWriter()
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        var failure = await Record.ExceptionAsync(() => fixture.PublishAsync(lease, async () =>
        {
            await fixture.MeasureAsync(() => files.ReadCleanupCompletedProofDataAsync(lease, async () =>
            {
                Assert.Equal("retained", await files.ReadFileAsync(lease, "retained.txt"));
                await files.WriteFileAtomicAsync(lease, "forbidden.txt", "effect");
                return true;
            }));
        }));
        Assert.IsType<InvalidOperationException>(failure);
        Assert.False(File.Exists(Path.Combine(fixture.Root, "game_session", "forbidden.txt")));
        Assert.Equal("original", File.ReadAllText(fixture.PresentPath));
    }

    /// <summary>
    /// Preserves cancellation for an already observed implicit asynchronous byte source.
    /// </summary>
    /// <returns>
    /// Completion after the cancelled consumer prevents target publication and releases its read window.
    /// </returns>
    [Fact]
    public async Task CancelledHitCannotReturnPreviouslyReadData()
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.PublishAsync(lease, async () =>
        {
            await fixture.MeasureAsync(() => files.ReadCleanupCompletedProofDataAsync(lease, async () =>
            {
                Assert.Equal(Encoding.UTF8.GetBytes("retained"),
                    await files.ReadFileBytesAsync("retained.txt", CancellationToken.None));
                cancellation.Cancel();
                await files.ReadFileBytesAsync("retained.txt", cancellation.Token);
                return true;
            }));
        }));
        Assert.Equal("original", File.ReadAllText(fixture.PresentPath));
        Assert.True(lease.IsActive);
    }

    /// <summary>
    /// Releases a failed window without masking its original exception or retaining its source DATA.
    /// </summary>
    /// <returns>
    /// Completion after a following recovered window obtains the changed source and a fresh proof.
    /// </returns>
    [Fact]
    public async Task OriginalExceptionAndFreshNextWindowSurviveCleanup()
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        var primary = new InvalidDataException("original proof failure");
        var outcome = await fixture.PublishAsync(lease, async () =>
        {
            var failure = await Record.ExceptionAsync(() => fixture.MeasureAsync(() =>
                files.ReadCleanupCompletedProofDataAsync<bool>(lease, async () =>
                {
                    Assert.Equal("retained", await files.ReadFileAsync(lease, "retained.txt"));
                    throw primary;
                })));
            Assert.Same(primary, failure);
            File.WriteAllText(fixture.RetainedPath, "modified");
            files.ResolveBackupPublicationRecovery(lease);
            var before = fixture.ProofSources;
            Assert.True(await fixture.MeasureAsync(() => files.ReadCleanupCompletedProofDataAsync(lease, async () =>
            {
                for (var index = 0; index < 8; index++)
                    Assert.Equal("modified", await files.ReadFileAsync(lease, "retained.txt"));
                return true;
            })));
            Assert.Equal(2, fixture.ProofSources - before);
        });
        AssertCommitted(outcome);
        Assert.Equal("published", File.ReadAllText(fixture.PresentPath));
        Assert.Equal("modified", File.ReadAllText(fixture.RetainedPath));
    }

    /// <summary>
    /// Leaves post-publication readback fresh after the proof service has closed.
    /// </summary>
    /// <returns>
    /// Completion after the original byte reader observes retained-file changes made after publication.
    /// </returns>
    [Fact]
    public async Task PostPublicationReadbackCannotReuseRetainedBeforeImage()
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        var outcome = await fixture.PublishAsync(lease, async () =>
        {
            await fixture.MeasureAsync(() => files.ReadCleanupCompletedProofDataAsync(lease, async () =>
            {
                for (var index = 0; index < 8; index++)
                    Assert.Equal("retained", await files.ReadFileAsync(lease, "retained.txt"));
                return true;
            }));
        });
        AssertCommitted(outcome);
        Assert.Equal(2, fixture.ProofSources);
        File.WriteAllText(fixture.RetainedPath, "modified");
        Assert.Equal(Encoding.UTF8.GetBytes("modified"), await files.ReadFileBytesAsync(lease, "retained.txt"));
        Assert.Equal("published", File.ReadAllText(fixture.PresentPath));
    }

    /// <summary>
    /// Uses an independent proof service without reopening or importing a suspended failed outer DATA owner.
    /// </summary>
    /// <returns>
    /// Completion after genuine publication, two proof sources and unchanged outer ownership.
    /// </returns>
    [Fact]
    public async Task SuspendedFailedOuterOperationCannotSupplyWindowData()
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        operation.Fail();
        using (operation.Suspend())
        {
            var outcome = await fixture.PublishAsync(lease, async () =>
            {
                await fixture.MeasureAsync(() => files.ReadCleanupCompletedProofDataAsync(lease, async () =>
                {
                    for (var index = 0; index < 8; index++)
                        Assert.Equal("retained", await files.ReadFileAsync(lease, "retained.txt"));
                    return true;
                }));
            });
            AssertCommitted(outcome);
            Assert.True(operation.IsSuspended);
            Assert.False(operation.CaptureState().Closed);
            Assert.Equal(0, operation.CaptureState().Entries);
        }
        Assert.Equal(2, fixture.ProofSources);
        Assert.Equal("published", File.ReadAllText(fixture.PresentPath));
    }

    /// <summary>
    /// Rejects physical lease teardown during the read window and preserves the caller's live lease.
    /// </summary>
    /// <returns>
    /// Completion after original source reading and target publication succeed following the atomic refusal.
    /// </returns>
    [Fact]
    public async Task WindowRejectsPrematureLeaseClose()
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        var outcome = await fixture.PublishAsync(lease, async () =>
        {
            await fixture.MeasureAsync(() => files.ReadCleanupCompletedProofDataAsync(lease, async () =>
            {
                Assert.Equal("retained", await files.ReadFileAsync(lease, "retained.txt"));
                await Assert.ThrowsAsync<InvalidOperationException>(() => lease.DisposeAsync().AsTask());
                Assert.True(lease.IsActive);
                for (var index = 0; index < 7; index++)
                    Assert.Equal("retained", await files.ReadFileAsync(lease, "retained.txt"));
                return true;
            }));
        });
        AssertCommitted(outcome);
        Assert.True(lease.IsActive);
        Assert.Equal(2, fixture.ProofSources);
        Assert.Equal("published", File.ReadAllText(fixture.PresentPath));
    }

    /// <summary>
    /// Starts a fresh proof window for an actual compensated transient publication retry.
    /// </summary>
    /// <returns>
    /// Completion after both attempts authenticate their current retained image and only the final target commits.
    /// </returns>
    [Fact]
    public async Task PublicationRetryCannotReuseEarlierAttemptData()
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        var injected = false;
        var callbacks = 0;
        fixture.OnPublication = (phase, _) =>
        {
            if (injected || phase != TrustedLocalPublicationPhase.IntentPublished) return;
            injected = true;
            File.WriteAllText(fixture.RetainedPath, "modified");
            throw new IOException("Transient actual publisher attempt refusal", unchecked((int)0x80070020));
        };
        var outcome = await fixture.PublishAsync(lease, async () =>
        {
            callbacks++;
            var expected = callbacks == 1 ? "retained" : "modified";
            await fixture.MeasureAsync(() => files.ReadCleanupCompletedProofDataAsync(lease, async () =>
            {
                for (var index = 0; index < 8; index++)
                    Assert.Equal(expected, await files.ReadFileAsync(lease, "retained.txt"));
                return true;
            }));
        });
        AssertCommitted(outcome);
        Assert.True(injected);
        Assert.Equal(2, callbacks);
        Assert.Equal(4, fixture.ProofSources);
        Assert.Equal("published", File.ReadAllText(fixture.PresentPath));
        Assert.Equal("modified", File.ReadAllText(fixture.RetainedPath));
    }

    /// <summary>
    /// Requires the actual publisher's committed outcome without converting a failure into successful validation.
    /// </summary>
    /// <param name="outcome">
    /// The original outcome returned by the real trusted-local publisher.
    /// </param>
    private static void AssertCommitted(TrustedLocalPublicationOutcome outcome)
    {
        Assert.Equal(TrustedLocalPublicationDisposition.Committed, outcome.Disposition);
        Assert.Null(outcome.Failure);
    }

    /// <summary>
    /// Owns independent ordinary files and records actual uncached source boundaries without functional hooks.
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _nonce = Guid.NewGuid().ToString("N");
        private readonly ConcurrentQueue<(string Name, string Boundary, double Milliseconds)> _timings = new();
        private int _proofSources;

        /// <summary>
        /// Creates one publication member and one retained image on an exclusively owned root.
        /// </summary>
        /// <param name="output">
        /// Receives all observations after exact owned cleanup, without a diagnostic quota.
        /// </param>
        internal Fixture(ITestOutputHelper output)
        {
            _output = output;
            Root = Path.Combine(Path.GetTempPath(), "boe-cleanup-proof-data-" + _nonce);
            Directory.CreateDirectory(Path.Combine(Root, "game_session"));
            File.WriteAllText(Path.Combine(Root, ".fixture-owner"), _nonce);
            File.WriteAllBytes(PresentPath, Encoding.UTF8.GetBytes("original"));
            File.WriteAllBytes(RetainedPath, Encoding.UTF8.GetBytes("retained"));
            Files = new FileSystemManager(Root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    OperationTimingObserver = (name, boundary, elapsed) =>
                    {
                        _timings.Enqueue((name, boundary, elapsed.TotalMilliseconds));
                        if (Recording && boundary == "begin" &&
                            name is "OrdinaryRead.Sync.Total" or "OrdinaryRead.Async.Total")
                            Interlocked.Increment(ref _proofSources);
                    },
                    LocalPublicationObserver = (phase, ordinal) => OnPublication?.Invoke(phase, ordinal)
                });
        }

        /// <summary>
        /// Gets the exclusively owned nonce-marked root.
        /// </summary>
        internal string Root { get; }

        /// <summary>
        /// Gets the actual declared ordinary publication member.
        /// </summary>
        internal string PresentPath => Path.Combine(Root, "game_session", "present.txt");

        /// <summary>
        /// Gets the independent file whose exact retained bytes must survive publication.
        /// </summary>
        internal string RetainedPath => Path.Combine(Root, "game_session", "retained.txt");

        /// <summary>
        /// Gets the production manager issuing the original lease, recovery and publication.
        /// </summary>
        internal FileSystemManager Files { get; }

        /// <summary>
        /// Gets or sets whether ordinary physical-source timers belong to the selected proof window.
        /// </summary>
        internal bool Recording { get; set; }

        /// <summary>
        /// Gets or sets the original publication scheduling hook used only for the actual retry control.
        /// </summary>
        internal Action<TrustedLocalPublicationPhase, int>? OnPublication { get; set; }

        /// <summary>
        /// Gets the complete number of actual ordinary source reads during the selected proof helper.
        /// </summary>
        internal int ProofSources => Volatile.Read(ref _proofSources);

        /// <summary>
        /// Measures only the selected helper so recovery and the publisher's own before-image reads remain separate.
        /// </summary>
        /// <typeparam name="TResult">
        /// The original result returned by the actual read helper.
        /// </typeparam>
        /// <param name="read">
        /// The real helper invocation whose physical ordinary source reads are counted.
        /// </param>
        /// <returns>
        /// The original helper result after measurement routing has been restored.
        /// </returns>
        internal async Task<TResult> MeasureAsync<TResult>(Func<Task<TResult>> read)
        {
            var previous = Recording;
            Recording = true;
            try { return await read(); }
            finally { Recording = previous; }
        }

        /// <summary>
        /// Invokes the real one-member publisher and performs original recovery before the supplied proof callback.
        /// </summary>
        /// <param name="lease">
        /// The actual caller-owned canonical lease retained through publication and callback cleanup.
        /// </param>
        /// <param name="callback">
        /// Original prepared-namespace test work executed only after real recovery returns.
        /// </param>
        /// <param name="optIn">
        /// Whether the actual publisher issues its selected proof-window context; defaults to enabled.
        /// </param>
        /// <returns>
        /// The original trusted-local outcome, with pre-publication callback exceptions left unchanged.
        /// </returns>
        internal Task<TrustedLocalPublicationOutcome> PublishAsync(FileSystemManager.CanonicalWriteLease lease,
            Func<Task> callback, bool optIn = true) => Files.PublishLocalFilesAsync(lease,
            [new CanonicalLocalFileChange("present.txt", Encoding.UTF8.GetBytes("original"), Encoding.UTF8.GetBytes("published"))],
            validatePreparedNamespace: async () =>
            {
                Files.ResolveBackupPublicationRecovery(lease);
                await callback();
            }, reusePreparedProofData: optIn);

        /// <summary>
        /// Deletes only the exact owned root and emits every collected timing record and source count.
        /// </summary>
        public void Dispose()
        {
            try
            {
                Assert.Equal("boe-cleanup-proof-data-" + _nonce, Path.GetFileName(Root));
                Assert.Equal(_nonce, File.ReadAllText(Path.Combine(Root, ".fixture-owner")));
                new TrustedLocalFileScope([Path.GetTempPath()]).DeleteOwnedTree(Root);
                Assert.False(Directory.Exists(Root));
            }
            finally
            {
                foreach (var row in _timings)
                    _output.WriteLine(JsonSerializer.Serialize(new { row.Name, row.Boundary, row.Milliseconds }));
                _output.WriteLine(JsonSerializer.Serialize(new { Root, ProofSources, Removed = !Directory.Exists(Root) }));
            }
        }
    }
}
