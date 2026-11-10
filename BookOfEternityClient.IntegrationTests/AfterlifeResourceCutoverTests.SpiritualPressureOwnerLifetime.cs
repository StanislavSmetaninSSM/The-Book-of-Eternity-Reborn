using System.Diagnostics;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Serializes two genuine comparisons and refuses an in-flight or queued cache return after actual capture disposal.
    /// Physical-read barriers exercise real gates; they do not issue proof, alter signed DATA or introduce cancellation support.
    /// </summary>
    [Fact]
    public async Task SpiritualExistingPressure_OrdinaryComparisonSerializesOverlapAndRejectsInFlightDisposal()
    {
        var prepared = await PreparePressureOrdinaryOwnerTemplateAsync();
        var overlapBarrier = new PressureOrdinaryReadBarrier();
        var disposalBarrier = new PressureOrdinaryReadBarrier();
        var contexts = new List<ResourceMaterializationTestContext>();
        try
        {
            var overlap = await MaterializePressureOrdinaryOwnerCopyAsync(prepared, "overlap", overlapBarrier.Hooks);
            contexts.Add(overlap.Context);
            var disposal = await MaterializePressureOrdinaryOwnerCopyAsync(prepared, "disposal", disposalBarrier.Hooks);
            contexts.Add(disposal.Context);
            Assert.Equal(3, new[] { prepared.Generation, overlap.Generation, disposal.Generation }
                .Distinct(StringComparer.Ordinal).Count());

            await using (var lease = await overlap.Context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                using var capture = await BeginPressureOrdinaryOwnerAsync(overlap, lease, "overlap");
                var files = CaptureExactSpiritualFiles(overlap.Context.FileSystem.GameSessionPath);
                var generation = File.ReadAllBytes(overlap.Context.FileSystem.SessionGenerationPath);
                var before = await ReadPressureOwnerSnapshotAsync(capture, lease, "overlap_before");
                AssertPressureOrdinaryFrontier(before, overlap, capture, lease);
                var warm = await capture.ReadExistingPressureOrdinaryComparisonAsync(lease);
                AssertNoConflictFrameErrors(warm.Issues);
                Assert.False(warm.PrefixBlocked);
                Assert.NotNull(warm.Comparison);
                AssertPressureOrdinaryComparison(warm.Comparison, overlap, files);

                overlapBarrier.Arm();
                var first = capture.ReadExistingPressureOrdinaryComparisonAsync(lease);
                Task<ValidationService.SpiritualOrdinaryPressureComparisonResult>? second = null;
                var overlapStarted = Stopwatch.GetTimestamp();
                try
                {
                    await overlapBarrier.WaitUntilEnteredAsync();
                    second = capture.ReadExistingPressureOrdinaryComparisonAsync(lease);
                    Assert.False(first.IsCompleted);
                    Assert.False(second.IsCompleted); // The first caller still holds the actual capture gate.
                    overlapBarrier.Release();
                    var results = await Task.WhenAll(first, second);
                    foreach (var result in results)
                    {
                        AssertNoConflictFrameErrors(result.Issues);
                        Assert.False(result.PrefixBlocked);
                        Assert.NotNull(result.Comparison);
                        AssertPressureOrdinaryComparison(result.Comparison, overlap, files);
                    }
                    Assert.Equal(results[0].Comparison!.OrdinaryExchangeJson, results[1].Comparison!.OrdinaryExchangeJson);
                }
                finally
                {
                    overlapBarrier.Release();
                    await DrainPressureOwnerReadsAsync(first, second);
                    WritePressureOwnerTiming("ordinary_overlap", "two_serial_callers", overlapStarted);
                }
                Assert.True(capture.IsCurrentOwner);
                Assert.Equal(0, capture.ReadExistingStrainReuseCounts().ActualEvaluations);
                // Arbitrary filesystem hooks may choose a fresh fallback; the hookless fixture owns the hit-count proof.
                AssertPressureOrdinaryFrontiersEqual(before,
                    await ReadPressureOwnerSnapshotAsync(capture, lease, "overlap_after"));
                AssertPressureOrdinaryFrontier(before, overlap, capture, lease);
                AssertExistingPressureInputInert(overlap.Context, lease, files, generation);
            }

            await using (var lease = await disposal.Context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                using var capture = await BeginPressureOrdinaryOwnerAsync(disposal, lease, "in_flight_disposal");
                var files = CaptureExactSpiritualFiles(disposal.Context.FileSystem.GameSessionPath);
                var generation = File.ReadAllBytes(disposal.Context.FileSystem.SessionGenerationPath);
                var before = await ReadPressureOwnerSnapshotAsync(capture, lease, "in_flight_disposal_before");
                AssertPressureOrdinaryFrontier(before, disposal, capture, lease);
                var warm = await capture.ReadExistingPressureOrdinaryComparisonAsync(lease);
                AssertNoConflictFrameErrors(warm.Issues);
                Assert.False(warm.PrefixBlocked);
                Assert.NotNull(warm.Comparison);
                AssertPressureOrdinaryComparison(warm.Comparison, disposal, files);
                AssertPressureOrdinaryFrontiersEqual(before,
                    await ReadPressureOwnerSnapshotAsync(capture, lease, "in_flight_disposal_after_warm"));
                var warmCounts = capture.ReadExistingStrainReuseCounts();

                disposalBarrier.Arm();
                var inFlight = capture.ReadExistingPressureOrdinaryComparisonAsync(lease);
                Task<ValidationService.SpiritualOrdinaryPressureComparisonResult>? queued = null;
                var disposalStarted = Stopwatch.GetTimestamp();
                try
                {
                    await disposalBarrier.WaitUntilEnteredAsync();
                    queued = capture.ReadExistingPressureOrdinaryComparisonAsync(lease);
                    Assert.False(inFlight.IsCompleted);
                    Assert.False(queued.IsCompleted);
                    capture.Dispose();
                    Assert.False(capture.IsCurrentOwner);
                    disposalBarrier.Release();
                    await Assert.ThrowsAsync<ObjectDisposedException>(() => inFlight);
                    await Assert.ThrowsAsync<ObjectDisposedException>(() => queued);
                }
                finally
                {
                    disposalBarrier.Release();
                    await DrainPressureOwnerReadsAsync(inFlight, queued);
                    WritePressureOwnerTiming("ordinary_in_flight_disposal", "in_flight_and_queued", disposalStarted);
                }
                // The barrier was the physical raw-input check before another ordinary computation/cache lookup.
                var disposedCounts = capture.ReadExistingStrainReuseCounts();
                Assert.Equal(warmCounts.OrdinaryEvaluations, disposedCounts.OrdinaryEvaluations);
                Assert.Equal(warmCounts.ActualEvaluations, disposedCounts.ActualEvaluations);
                await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                    capture.ReadExistingPressureOrdinaryComparisonAsync(lease));
                await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                    capture.ReadExistingPressureOrdinaryProbeStateAsync(lease));
                AssertExistingPressureInputInert(disposal.Context, lease, files, generation);
                foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                             SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
                    Assert.Null(await disposal.Context.FileSystem.ReadFileBytesAsync(lease, path));
            }
        }
        finally
        {
            overlapBarrier.Release();
            disposalBarrier.Release();
            var cleanupFailures = new List<string>();
            foreach (var context in contexts.AsEnumerable().Reverse())
            {
                var started = Stopwatch.GetTimestamp();
                try { await context.DisposeAsync(); }
                catch (Exception error) { cleanupFailures.Add(context.RootPath + ": " + error.Message); }
                finally { WritePressureOwnerTiming("lifetime_owned_cleanup", context.RootPath, started); }
                if (Directory.Exists(context.RootPath)) cleanupFailures.Add(context.RootPath + ": root remains");
            }
            Assert.Empty(cleanupFailures);
        }
    }

    /// <summary>Observes and drains every started read before lease/root cleanup, without treating cleanup as a success assertion.</summary>
    /// <param name="reads">Actual comparison tasks, including a null slot when setup failed before the second invocation.</param>
    private static async Task DrainPressureOwnerReadsAsync(params Task?[] reads)
    {
        foreach (var read in reads)
            if (read is not null)
                _ = await Record.ExceptionAsync(() => read);
    }

    /// <summary>Blocks one real physical ordinary-conflict read with a bounded barrier and no state mutation or proof authority.</summary>
    private sealed class PressureOrdinaryReadBarrier
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _armed;

        /// <summary>Creates dependencies attached only while constructing this copy's fresh filesystem.</summary>
        internal PressureOrdinaryReadBarrier() => Hooks = new FileSystemManagerHooks
            { BeforeCanonicalReadOpenAsync = BeforeReadAsync };

        /// <summary>Gets this copy's ordinary physical-read scheduling dependency, never a proof/evaluation callback.</summary>
        internal FileSystemManagerHooks Hooks { get; }

        /// <summary>Arms one read only after genuine prefix preparation and a successful warm comparison.</summary>
        internal void Arm() => Assert.Equal(0, Interlocked.CompareExchange(ref _armed, 1, 0));

        /// <summary>Waits for the actual physical-read boundary, bounded independently of the category's protective ceiling.</summary>
        /// <returns>Completion only after the read is held inside the real capture operation.</returns>
        internal Task WaitUntilEnteredAsync() => _entered.Task.WaitAsync(TimeSpan.FromSeconds(15));

        /// <summary>Always permits owned tasks to leave the barrier before any lease or fixture root is disposed.</summary>
        internal void Release() => _release.TrySetResult();

        /// <summary>Holds exactly the normalized live conflict path, not its signed snapshot image or another source.</summary>
        /// <param name="path">Actual canonical read path supplied by the existing filesystem.</param>
        /// <returns>Completion after release, or a bounded fixture failure if release is missing.</returns>
        private async Task BeforeReadAsync(string path)
        {
            if (path.Replace('\\', '/') != AfterlifeSpiritualConflictState.StatePath ||
                Interlocked.CompareExchange(ref _armed, 0, 1) != 1) return;
            _entered.TrySetResult();
            await _release.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
    }
}
