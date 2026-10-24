using BookOfEternityClient.Core;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Verifies passive timing with the real cache-eligible manager and independent windows.
/// </summary>
/// <param name="output">
/// Receives uncapped source counts and owned fixture cleanup results.
/// </param>
public sealed class PassiveRecoveryTimingTests(ITestOutputHelper output)
{
    /// <summary>
    /// Records real sections while preserving ordinary DATA and directory reuse.
    /// </summary>
    /// <param name="synchronous">
    /// Whether source requests use the synchronous reader.
    /// </param>
    /// <returns>
    /// Completion after the ordinary two-source, seven-hit phase and closed counters.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealPhaseRetainsCachesAndRecordsNamedWork(bool synchronous)
    {
        var recorder = new OperationTimingAccumulator();
        using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false, recorder);
        await fixture.InitializeAsync();
        var before = fixture.Files.CaptureAdmissionDirectoryStatistics();
        using var window = recorder.StartWindow();
        await fixture.ReadEightAsync(synchronous);
        window.Dispose();
        Assert.Equal(2, fixture.Observers.ReadCount("present.txt"));
        Assert.Equal(7, fixture.Files.CaptureControlGameStateStatistics().ClientOwnedControlFiles.FastSourceHits);
        var counts = fixture.Files.CaptureAdmissionDirectoryStatistics() - before;
        Assert.Equal(57, counts.Invocations);
        Assert.Equal(456, counts.Hits);
        var snapshot = window.Capture();
        Assert.True(snapshot.Complete);
        Assert.Contains(snapshot.Rows, row => row.Name == "GameStateData.Phase" && row.Completed == 1);
        Assert.Contains(snapshot.Rows, row => row.Name == "MainAdmission.Validate.Guard");
        Assert.All(snapshot.Rows, row => Assert.Equal(row.Started, row.Completed));
    }

    /// <summary>
    /// Restores and suppresses synchronous receiver context without installing a callback.
    /// </summary>
    /// <returns>
    /// Completion after a named pure computation is recorded only in its selected context.
    /// </returns>
    [Fact]
    public async Task StaticContextSelectsRecorderAndNullContextSuppressesIt()
    {
        var recorder = new OperationTimingAccumulator();
        using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false, recorder);
        await fixture.InitializeAsync();
        using var window = recorder.StartWindow();
        using (fixture.Files.EnterOperationTimingContext())
        {
            using (FileSystemManager.MeasureCurrentOperation("outer-selected")) { }
            using (FileSystemManager.EnterOperationTimingContext(null))
                Assert.Null(FileSystemManager.MeasureCurrentOperation("suppressed"));
            using (FileSystemManager.MeasureCurrentOperation("restored")) { }
        }
        window.Dispose();
        Assert.Equal(new[] { "outer-selected", "restored" }, window.Capture().Rows.Select(row => row.Name));
    }

    /// <summary>
    /// Preserves the arbitrary timing observer's original directory fallback.
    /// </summary>
    /// <returns>
    /// Completion after callback observation wins over passive accumulation.
    /// </returns>
    [Fact]
    public async Task LegacyTimingCallbackKeepsOriginalFallback()
    {
        var recorder = new OperationTimingAccumulator();
        var callbacks = 0;
        using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false, recorder,
            (_, _, _) => Interlocked.Increment(ref callbacks));
        await fixture.InitializeAsync();
        var before = fixture.Files.CaptureAdmissionDirectoryStatistics();
        using var window = recorder.StartWindow();
        await fixture.ReadEightAsync();
        window.Dispose();
        Assert.True(callbacks > 0);
        Assert.Equal(0, (fixture.Files.CaptureAdmissionDirectoryStatistics() - before).Invocations);
        Assert.Empty(window.Capture().Rows);
    }

    /// <summary>
    /// Preserves arbitrary read callbacks and their fresh source route with a passive recorder.
    /// </summary>
    /// <returns>
    /// Completion after eight observed original reads without directory contexts.
    /// </returns>
    [Fact]
    public async Task ReadCallbacksStillDisableSourceReuse()
    {
        var recorder = new OperationTimingAccumulator();
        using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, true, recorder);
        await fixture.InitializeAsync();
        using var window = recorder.StartWindow();
        await fixture.ReadEightAsync();
        window.Dispose();
        Assert.Equal(8, fixture.Observers.ReadCount("present.txt"));
        Assert.Equal(0, fixture.Files.CaptureAdmissionDirectoryStatistics().Invocations);
    }

    /// <summary>
    /// Keeps the original fresh byte fence and next-operation lifetime while recording.
    /// </summary>
    /// <returns>
    /// Completion after same-timestamp drift is refused and a new window reads replacement bytes.
    /// </returns>
    [Fact]
    public async Task ActiveRecorderPreservesByteFenceAndFailureClose()
    {
        var recorder = new OperationTimingAccumulator();
        using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false, recorder);
        await fixture.InitializeAsync();
        var timestamp = File.GetLastWriteTimeUtc(fixture.RawPath);
        using (var first = recorder.StartWindow())
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.ReadEightAsync(after: () =>
            {
                File.WriteAllText(fixture.RawPath, "changed!");
                File.SetLastWriteTimeUtc(fixture.RawPath, timestamp);
            }));
            first.Dispose();
            Assert.True(first.Capture().Complete);
        }
        using var second = recorder.StartWindow();
        await fixture.ReadEightAsync(expected: "changed!");
        second.Dispose();
        Assert.True(second.Capture().Complete);
        Assert.Equal(4, fixture.Observers.ReadCount("present.txt"));
    }

    /// <summary>
    /// Leaves measurement disabled outside a window and closes each scope once.
    /// </summary>
    [Fact]
    public void DefaultOffAndIdempotentScopeCompletion()
    {
        var recorder = new OperationTimingAccumulator();
        Assert.Null(recorder.Measure("outside"));
        using var window = recorder.StartWindow();
        var scope = recorder.Measure("inside");
        Assert.NotNull(scope);
        scope.Dispose(); scope.Dispose();
        window.Dispose(); window.Dispose();
        Assert.Null(recorder.Measure("outside"));
        var row = Assert.Single(window.Capture().Rows);
        Assert.Equal(1, row.Started); Assert.Equal(1, row.Completed);
        Assert.True(window.Capture().Complete);
    }

    /// <summary>
    /// Reports late completion without contaminating a following independent window.
    /// </summary>
    [Fact]
    public void LateScopeCannotBecomeACompleteWindowOrEnterNextWindow()
    {
        var recorder = new OperationTimingAccumulator();
        var first = recorder.StartWindow();
        var scope = recorder.Measure("late")!;
        first.Dispose();
        Assert.Equal(1, first.Capture().OpenScopes);
        using var second = recorder.StartWindow();
        first.Dispose(); scope.Dispose();
        Assert.False(first.Capture().Complete);
        Assert.Equal(1, first.Capture().LateCompletions);
        using (recorder.Measure("second")) { }
        second.Dispose();
        Assert.True(second.Capture().Complete);
        Assert.Equal("second", Assert.Single(second.Capture().Rows).Name);
    }

    /// <summary>
    /// Rejects overlap without closing the existing window or replacing a body failure.
    /// </summary>
    [Fact]
    public void OverlapRefusalAndBodyFailurePreserveTheExistingWindow()
    {
        var recorder = new OperationTimingAccumulator();
        using var window = recorder.StartWindow();
        Assert.Throws<InvalidOperationException>(() => recorder.StartWindow());
        var failure = new IOException("original body failure");
        Action body = () => { using var scope = recorder.Measure("failure"); throw failure; };
        Assert.Same(failure, Record.Exception(body));
        window.Dispose();
        Assert.True(window.Capture().Complete);
        Assert.Equal(1, Assert.Single(window.Capture().Rows).Completed);
    }

    /// <summary>
    /// Retains every distinct name beyond the former diagnostic limit and detaches snapshots.
    /// </summary>
    [Fact]
    public void EveryNameIsRetainedAndCapturedArraysAreDetached()
    {
        var recorder = new OperationTimingAccumulator();
        using var window = recorder.StartWindow();
        for (var index = 0; index < 1500; index++) using (recorder.Measure("section-" + index)) { }
        window.Dispose();
        var snapshot = window.Capture();
        Assert.Equal(1500, snapshot.Rows.Length);
        snapshot.Rows[0] = new("changed-copy", 0, 0, 0, 0);
        Assert.DoesNotContain(window.Capture().Rows, row => row.Name == "changed-copy");
    }

    /// <summary>
    /// Serializes concurrent counter updates without flowing authority or mutable game fixtures.
    /// </summary>
    /// <returns>
    /// Completion after every independent worker scope contributes exactly once.
    /// </returns>
    [Fact]
    public async Task ConcurrentScopeCompletionsAreFullyCounted()
    {
        var recorder = new OperationTimingAccumulator();
        using var window = recorder.StartWindow();
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            for (var index = 0; index < 100; index++) using (recorder.Measure("concurrent")) { }
        })));
        window.Dispose();
        var row = Assert.Single(window.Capture().Rows);
        Assert.Equal(1600, row.Started); Assert.Equal(1600, row.Completed);
        Assert.True(window.Capture().Complete);
    }
}
