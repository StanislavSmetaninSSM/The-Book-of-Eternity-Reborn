using System.Text;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Actual independent readers qualify passive observation, never engine or publication authority.</summary>
public sealed partial class LifecycleSealedReadObservationTests
{
    private const string Source = "game_state/meta/achievements.json";
    private readonly ITestOutputHelper _output;

    /// <summary>Receives complete per-case source and owned-root receipts.</summary>
    /// <param name="output">The actual test output receiver.</param>
    public LifecycleSealedReadObservationTests(ITestOutputHelper output) => _output = output;

    /// <summary>Fails only the physical-source reduction until the shared positive construction policy becomes hook-free.</summary>
    [Fact]
    public async Task EightActualReadsKeepAllDataAndReuseOneInitialAndOneFreshSource()
    {
        using var fixture = new ReadFixture(LifecycleFixtureReadObservers.CreateForSealedSourceObservation(), _output);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        fixture.Files.GetOrCreateSessionGeneration(lease);
        using var window = fixture.Timings.StartWindow();
        using var observed = fixture.Reads.CanonicalSources.BeginObservation(fixture.Files, window);
        using var owner = fixture.Files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(owner);
        var returned = 0;
        for (var index = 0; index < 8; index++)
        {
            var bytes = await fixture.Files.ReadFileBytesAsync(lease, Source);
            Assert.Equal(fixture.Bytes, bytes);
            bytes![0] ^= 1; // exported result cannot corrupt retained source DATA
            returned++;
        }
        await owner.CompleteAsync();
        observed.CloseCapture();
        var captured = observed.Capture();
        Assert.Equal(8, returned);
        Assert.True(captured.Available, captured.Fault);
        Assert.True(captured.Closed);
        Assert.DoesNotContain(captured.Events, row => row.Kind is CanonicalReadEventKind.Fault or CanonicalReadEventKind.Cancelled);
        Assert.All(captured.Events, row => Assert.Equal(CanonicalReadSourcePhase.Ordinary, row.Phase));
        Assert.DoesNotContain(captured.Events, row => row.Kind is CanonicalReadEventKind.PublicInitialAttemptReturned or CanonicalReadEventKind.PublicFinalReturned);
        var physical = captured.Events.Where(row => row.Kind == CanonicalReadEventKind.InitialOpened && row.Path == Source).ToArray();
        Assert.Equal(fixture.Reads.ReadCount(Source), physical.LongLength);
        var snapshots = captured.Events.Where(row => row.Kind == CanonicalReadEventKind.PhysicalSnapshot && row.Path == Source).ToArray();
        Assert.Equal(physical.Length, snapshots.Length);
        Assert.All(snapshots, row => Assert.Equal(fixture.Bytes, row.Bytes));
        Assert.All(physical, row => Assert.Contains(captured.Events, completed => completed.ReadId == row.ReadId &&
            completed.Kind == CanonicalReadEventKind.ReaderReturned));
        _output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { returned, PhysicalSources = physical.Length, captured }));
        // RED prerequisites above must pass. The original nonnull hook projection
        // yields eight actual sources; GREEN changes only shared construction policy.
        Assert.Equal(2, physical.Length);
    }

    /// <summary>Public attempt/final returns and direct leased reads remain distinct on both original reader families.</summary>
    /// <param name="synchronous">Whether to use the actual synchronous family.</param>
    /// <param name="leased">Whether the caller owns an explicit lease.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task OriginalReturnsAndActualStreamBytesStayDistinct(bool synchronous, bool leased)
    {
        using var fixture = new ReadFixture(new LifecycleFixtureReadObservers(false, false), _output);
        using var window = fixture.Timings.StartWindow();
        using var observed = fixture.Reads.CanonicalSources.BeginObservation(fixture.Files, window);
        byte[]? bytes;
        if (leased)
        {
            await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
            bytes = synchronous ? fixture.Files.ReadFileBytesSync(lease, Source) : await fixture.Files.ReadFileBytesAsync(lease, Source);
        }
        else bytes = synchronous ? fixture.Files.ReadFileBytesSync(Source) : await fixture.Files.ReadFileBytesAsync(Source);
        Assert.Equal(fixture.Bytes, bytes);
        observed.CloseCapture();
        var capture = observed.Capture();
        Assert.True(capture.Available, capture.Fault);
        Assert.Single(capture.Events.Where(row => row.Kind == CanonicalReadEventKind.InitialOpened));
        Assert.Equal(fixture.Bytes, Assert.Single(capture.Events.Where(row => row.Kind == CanonicalReadEventKind.PhysicalSnapshot)).Bytes);
        Assert.Contains(capture.Events, row => row.Kind == CanonicalReadEventKind.ReaderReturned);
        Assert.Equal(leased ? 0 : 1, capture.Events.Count(row => row.Kind == CanonicalReadEventKind.PublicInitialAttemptReturned));
        Assert.Equal(leased ? 0 : 1, capture.Events.Count(row => row.Kind == CanonicalReadEventKind.PublicFinalReturned));
        var exported = capture.Events.First(row => row.Bytes is not null).Bytes!;
        exported[0] ^= 1;
        Assert.Equal(fixture.Bytes, observed.Capture().Events.First(row => row.Bytes is not null).Bytes);
    }

    /// <summary>Cut/reset and foreign or disposed observation owners cannot share an active epoch.</summary>
    [Fact]
    public async Task ObservationEpochAndBindingAreNotImportable()
    {
        using var fixture = new ReadFixture(new LifecycleFixtureReadObservers(false, false), _output);
        using var other = new ReadFixture(new LifecycleFixtureReadObservers(false, false), _output);
        using var window = fixture.Timings.StartWindow();
        using var foreign = other.Timings.StartWindow();
        Assert.Throws<InvalidOperationException>(() => fixture.Reads.CanonicalSources.BeginObservation(other.Files, window));
        Assert.Throws<InvalidOperationException>(() => fixture.Reads.CanonicalSources.BeginObservation(fixture.Files, foreign));
        var observed = fixture.Reads.CanonicalSources.BeginObservation(fixture.Files, window);
        Assert.Throws<InvalidOperationException>(() => fixture.Reads.CanonicalSources.BeginObservation(fixture.Files, window));
        await fixture.Files.ReadFileAsync(Source);
        observed.CloseCapture();
        var closed = observed.Capture();
        await fixture.Files.ReadFileAsync(Source);
        Assert.Equal(closed.Events.Length, observed.Capture().Events.Length);
        observed.Dispose(); observed.Dispose();
        Assert.True(observed.Capture().Disposed); Assert.Empty(observed.Capture().Events);
        using var next = fixture.Reads.CanonicalSources.BeginObservation(fixture.Files, window);
        Assert.Empty(next.Capture().Events);
        await fixture.Files.ReadFileAsync(Source);
        Assert.NotEmpty(next.Capture().Events);
    }

    /// <summary>Read and close callbacks preserve the ordinary default and reject late attachment only in the explicit opt-in.</summary>
    [Fact]
    public async Task ReadAndCloseForwardingPoliciesRemainIndependent()
    {
        var oldPassive = new LifecycleFixtureReadObservers(false);
        Assert.Null(oldPassive.AfterInitialValidationAsync); Assert.Null(oldPassive.AfterReadAttemptAsync);
        Assert.NotNull(oldPassive.AfterSessionClosingAsync);
        var closed = 0;
        oldPassive.ClosingObserver = () => { closed++; return Task.CompletedTask; };
        await oldPassive.AfterSessionClosingAsync!(); Assert.Equal(1, closed);
        var selected = new LifecycleFixtureReadObservers(false, false);
        Assert.Null(selected.AfterSessionClosingAsync);
        Assert.Throws<InvalidOperationException>(() => selected.InitialObserver = _ => Task.CompletedTask);
        Assert.Throws<InvalidOperationException>(() => selected.ReturnedObserver = _ => Task.CompletedTask);
        Assert.Throws<InvalidOperationException>(() => selected.ClosingObserver = () => Task.CompletedTask);
        var normal = new LifecycleFixtureReadObservers();
        Assert.NotNull(normal.AfterInitialValidationAsync); Assert.NotNull(normal.AfterReadAttemptAsync);
        Assert.NotNull(normal.AfterSessionClosingAsync);
    }

    /// <summary>Only the closing hook varies; actual recovery source work and reuse prove its independent eligibility boundary.</summary>
    [Fact]
    public async Task ClosingForwardingIsAnIndependentRecoveryEligibilityBoundary()
    {
        async Task<int> Sources(bool closing)
        {
            using var fixture = new ReadFixture(new LifecycleFixtureReadObservers(false, closing), _output);
            await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
            fixture.Files.GetOrCreateSessionGeneration(lease);
            using var owner = fixture.Files.BeginRecoveryValidation(lease);
            var rule = new OperationValidationRule("sealed-observer-close-control", 1);
            var input = new object(); var sources = 0;
            for (var index = 0; index < 3; index++)
                await fixture.Files.ValidateRecoveryDataAsync(rule, input, lease, () => { sources++; return Task.CompletedTask; });
            return sources;
        }
        Assert.Equal(3, await Sources(true));
        Assert.Equal(1, await Sources(false));
    }

    private sealed class ReadFixture : IDisposable
    {
        private readonly ITestOutputHelper _output;
        internal readonly string Root = Path.Combine(Path.GetTempPath(), "boe-sealed-read-" + Guid.NewGuid().ToString("N"));
        internal readonly byte[] Bytes = Encoding.UTF8.GetBytes("original");
        internal LifecycleFixtureReadObservers Reads { get; }
        internal OperationTimingAccumulator Timings { get; } = new();
        internal FileSystemManager Files { get; }
        internal ReadFixture(LifecycleFixtureReadObservers reads, ITestOutputHelper output, FileSystemManagerHooks? faults = null)
        {
            _output = output; Reads = reads;
            Directory.CreateDirectory(Path.Combine(Root, "game_session", "game_state", "meta"));
            File.WriteAllBytes(Path.Combine(Root, "game_session", Source), Bytes);
            Files = new FileSystemManager(Root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
                new FileSystemManagerHooks
                {
                    PassiveCanonicalReads = reads.CanonicalSources, PassiveOperationTimings = Timings,
                    AfterCanonicalReadInitialValidationAsync = reads.AfterInitialValidationAsync,
                    AfterCanonicalReadAttemptAsync = reads.AfterReadAttemptAsync,
                    SessionOperationClosingAsync = reads.AfterSessionClosingAsync,
                    BeforeCanonicalReadOpenAsync = faults?.BeforeCanonicalReadOpenAsync
                });
        }
        public void Dispose()
        {
            Directory.Delete(Root, recursive: true);
            _output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { Root, Removed = !Directory.Exists(Root) }));
        }
    }
}
