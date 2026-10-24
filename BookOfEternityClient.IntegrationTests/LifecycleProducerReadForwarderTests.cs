using System.Text.Json;
using BookOfEternityClient.Core;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises the actual adopted lifecycle fixture without creating a game world.
/// </summary>
/// <param name="output">
/// Receives complete passive counts and the owned cleanup result.
/// </param>
public sealed class LifecycleProducerReadForwarderTests(ITestOutputHelper output)
{
    /// <summary>
    /// Demonstrates fewer source reads and live cache hits only with explicitly absent hooks.
    /// </summary>
    /// <param name="synchronous">
    /// Whether requests use the original synchronous reader.
    /// </param>
    /// <returns>
    /// Completion after both actual fixture modes return identical source text.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyForwardersControlActualReadAndDirectoryReuse(bool synchronous)
    {
        foreach (var forwarded in new[] { true, false })
        {
            using var fixture = new Fixture(output, forwarded);
            await fixture.InitializeAsync();
            var before = fixture.Files.CaptureAdmissionDirectoryStatistics();
            await fixture.ReadEightAsync(synchronous);
            var directories = fixture.Files.CaptureAdmissionDirectoryStatistics() - before;
            var control = fixture.Files.CaptureControlGameStateStatistics().ClientOwnedControlFiles;
            Assert.Equal(forwarded ? 8 : 2, fixture.Observers.ReadCount("present.txt"));
            Assert.Equal(forwarded ? 0 : 7, control.FastSourceHits);
            if (forwarded) Assert.Equal(0, directories.Invocations);
            else
            {
                Assert.True(directories.Invocations > 0);
                Assert.True(directories.Hits > 0);
            }
            output.WriteLine(JsonSerializer.Serialize(new { forwarded, synchronous, directories, control }));
        }
    }

    /// <summary>
    /// Preserves late observers and initial/returned ordering for default fixture callers.
    /// </summary>
    /// <returns>
    /// Completion after eight observed reads retain the original callback order.
    /// </returns>
    [Fact]
    public async Task DefaultFixtureRetainsLateCallbackOrder()
    {
        using var fixture = new Fixture(output, null);
        await fixture.InitializeAsync();
        var calls = new List<string>();
        fixture.Observers.InitialObserver = path => { if (path == "present.txt") calls.Add("initial"); return Task.CompletedTask; };
        fixture.Observers.ReturnedObserver = path => { if (path == "present.txt") calls.Add("returned"); return Task.CompletedTask; };
        await fixture.ReadEightAsync();
        Assert.Equal(Enumerable.Range(0, 8).SelectMany(_ => new[] { "initial", "returned" }), calls);
    }

    /// <summary>
    /// Refuses late callback attachment instead of silently allowing an unobserved mutation.
    /// </summary>
    /// <returns>
    /// Completion after both rejected assignments leave the cache-eligible fixture unchanged.
    /// </returns>
    [Fact]
    public async Task DisabledObserversRejectLateAttachmentWithoutChangingState()
    {
        using var fixture = new Fixture(output, false);
        await fixture.InitializeAsync();
        Assert.Throws<InvalidOperationException>(() => fixture.Observers.InitialObserver = _ => Task.CompletedTask);
        Assert.Throws<InvalidOperationException>(() => fixture.Observers.ReturnedObserver = _ => Task.CompletedTask);
        Assert.Null(fixture.Observers.InitialObserver);
        Assert.Null(fixture.Observers.ReturnedObserver);
        fixture.Observers.InitialObserver = null;
        fixture.Observers.ReturnedObserver = null;
        await fixture.ReadEightAsync();
        Assert.Equal(2, fixture.Observers.ReadCount("present.txt"));
    }

    /// <summary>
    /// Retains primary callback errors and allows a fresh following operation after clearing them.
    /// </summary>
    /// <param name="returned">
    /// Whether the error originates in the returned rather than initial observer.
    /// </param>
    /// <returns>
    /// Completion after the exact callback exception and fresh subsequent reads.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ForwardedErrorsRemainPrimaryAndCloseTheOperation(bool returned)
    {
        using var fixture = new Fixture(output, true);
        await fixture.InitializeAsync();
        var failure = new IOException("owned fixture callback failure");
        Func<string, Task> callback = path => path == "present.txt" ? Task.FromException(failure) : Task.CompletedTask;
        if (returned) fixture.Observers.ReturnedObserver = callback;
        else fixture.Observers.InitialObserver = callback;
        Assert.Same(failure, await Record.ExceptionAsync(() => fixture.ReadEightAsync()));
        fixture.Observers.InitialObserver = null;
        fixture.Observers.ReturnedObserver = null;
        File.WriteAllText(fixture.RawPath, "changed!");
        await fixture.ReadEightAsync(expected: "changed!");
    }

    /// <summary>
    /// Preserves asynchronous read observation for the existing race-test contract.
    /// </summary>
    /// <returns>
    /// Completion after a callback explicitly releases the pending read.
    /// </returns>
    [Fact]
    public async Task ForwardedCallbackCanHoldAndReleaseRead()
    {
        using var fixture = new Fixture(output, true);
        await fixture.InitializeAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Observers.InitialObserver = async path =>
        {
            if (path != "present.txt") return;
            entered.TrySetResult();
            await release.Task;
        };
        var read = fixture.ReadEightAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(read.IsCompleted);
        }
        finally { release.TrySetResult(); }
        await read.WaitAsync(TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Retains the real fresh exit fence against same-length bytes with a restored timestamp.
    /// </summary>
    /// <returns>
    /// Completion after the original validator refuses changed source data.
    /// </returns>
    [Fact]
    public async Task DisabledForwardersStillRejectSameTimestampByteDrift()
    {
        using var fixture = new Fixture(output, false);
        await fixture.InitializeAsync();
        var timestamp = File.GetLastWriteTimeUtc(fixture.RawPath);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.ReadEightAsync(after: () =>
        {
            File.WriteAllText(fixture.RawPath, "changed!");
            File.SetLastWriteTimeUtc(fixture.RawPath, timestamp);
        }));
    }

    /// <summary>
    /// Clears source DATA after successful and failed operations rather than retaining stale text.
    /// </summary>
    /// <param name="failBody">
    /// Whether the first read body throws before its normal exit fence.
    /// </param>
    /// <returns>
    /// Completion after a new operation reads the replacement bytes.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisabledForwardersDoNotReuseDataAcrossOperationClose(bool failBody)
    {
        using var fixture = new Fixture(output, false);
        await fixture.InitializeAsync();
        var failure = new IOException("owned body failure");
        if (failBody)
            Assert.Same(failure, await Record.ExceptionAsync(() => fixture.ReadEightAsync(after: () => throw failure)));
        else await fixture.ReadEightAsync();
        File.WriteAllText(fixture.RawPath, "changed!");
        await fixture.ReadEightAsync(expected: "changed!");
        if (!failBody) Assert.Equal(4, fixture.Observers.ReadCount("present.txt"));
    }

    /// <summary>
    /// Owns a tiny nonce-marked root adopted by the actual lifecycle constructor.
    /// </summary>
    internal sealed class Fixture : IDisposable
    {
        private readonly string _nonce = Guid.NewGuid().ToString("N");
        private readonly string _root;
        private readonly ITestOutputHelper _output;
        private readonly GameEngineTurnLifecycleTests _lifecycle;

        /// <summary>
        /// Constructs only source bytes; no game world or engine is initialized.
        /// </summary>
        /// <param name="output">
        /// Receives complete source counts and exact owned cleanup confirmation.
        /// </param>
        /// <param name="forwarded">
        /// Explicit hook mode, or <see langword="null"/> to exercise the existing constructor default.
        /// </param>
        /// <param name="passiveTimings">
        /// Optional sealed accumulator whose measurements must preserve cache eligibility.
        /// </param>
        /// <param name="legacyTimings">
        /// Optional arbitrary timing observer whose existing fallback contract remains unchanged.
        /// </param>
        internal Fixture(ITestOutputHelper output, bool? forwarded,
            OperationTimingAccumulator? passiveTimings = null, Action<string, string, TimeSpan>? legacyTimings = null)
        {
            _output = output;
            _root = Path.Combine(Path.GetTempPath(), "boe-read-forwarders-" + _nonce);
            Directory.CreateDirectory(Path.Combine(_root, "game_session"));
            File.WriteAllText(Path.Combine(_root, ".fixture-owner"), _nonce);
            File.WriteAllText(RawPath, "original");
            _lifecycle = new GameEngineTurnLifecycleTests(output, _root,
                operationTimingObserver: legacyTimings,
                readObservers: forwarded.HasValue ? new LifecycleFixtureReadObservers(forwarded.Value) : null,
                passiveOperationTimings: passiveTimings);
        }

        /// <summary>
        /// Gets the fixture's real manager, without a substitute authority.
        /// </summary>
        internal FileSystemManager Files => _lifecycle.DiagnosticFiles;

        /// <summary>
        /// Gets the holder actually installed by the lifecycle constructor.
        /// </summary>
        internal LifecycleFixtureReadObservers Observers => _lifecycle.DiagnosticReadObservers;

        /// <summary>
        /// Gets the one owned raw path used for deliberate source changes.
        /// </summary>
        internal string RawPath => Path.Combine(_root, "game_session", "present.txt");

        /// <summary>
        /// Publishes the real generation under the original canonical lease.
        /// </summary>
        /// <returns>
        /// Completion after the lease is released.
        /// </returns>
        internal async Task InitializeAsync()
        {
            await using var lease = await Files.AcquireCanonicalWriteLeaseAsync();
            Files.GetOrCreateSessionGeneration(lease);
        }

        /// <summary>
        /// Reads the same source through a genuine participating session and control phase.
        /// </summary>
        /// <param name="synchronous">
        /// Whether to use synchronous source reads.
        /// </param>
        /// <param name="expected">
        /// Exact text expected from all eight requests.
        /// </param>
        /// <param name="after">
        /// Optional deliberate mutation or failure before the original exit fence.
        /// </param>
        /// <returns>
        /// Completion after the phase and session close, including their original fences.
        /// </returns>
        internal Task ReadEightAsync(bool synchronous = false, string expected = "original", Action? after = null) =>
            SessionOperationContext.RunParticipatingCurrentSessionAsync(Files, () =>
                Files.ReadControlGameStateDataAsync(realm: false, originalOnly: false, async () =>
                {
                    for (var index = 0; index < 8; index++)
                        Assert.Equal(expected, synchronous ? Files.ReadFileSync("present.txt") : await Files.ReadFileAsync("present.txt"));
                    after?.Invoke();
                }));

        /// <summary>
        /// Verifies ownership before deleting only this fixture and emits every passive count.
        /// </summary>
        public void Dispose()
        {
            Assert.Equal(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "boe-read-forwarders-" + _nonce)), _root);
            Assert.Equal(_nonce, File.ReadAllText(Path.Combine(_root, ".fixture-owner")));
            new TrustedLocalFileScope([Path.GetTempPath()]).DeleteOwnedTree(_root);
            _lifecycle.Dispose();
            Assert.False(Directory.Exists(_root));
            foreach (var row in Observers.CaptureSources())
                _output.WriteLine(JsonSerializer.Serialize(new { Path = row.Key, Sources = row.Value }));
            _output.WriteLine(JsonSerializer.Serialize(new { Root = _root, Removed = !Directory.Exists(_root) }));
        }
    }
}
