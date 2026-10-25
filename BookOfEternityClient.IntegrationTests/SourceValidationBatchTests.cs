using System.Text.Json;
using BookOfEternityClient.Core;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Measures bounded directory DATA with real source readers and independent tiny sessions.
/// </summary>
/// <param name="output">
/// Receives uncapped named measurements and owned fixture cleanup evidence.
/// </param>
public sealed class SourceValidationBatchTests(ITestOutputHelper output)
{
    /// <summary>
    /// Requires real directory-query reduction while retaining all physical file and main checks.
    /// </summary>
    /// <param name="reserved">
    /// Whether the exact borrowed read set rather than the source operation owns the batch.
    /// </param>
    /// <param name="synchronous">
    /// Whether the original synchronous rather than asynchronous family reads each source.
    /// </param>
    /// <returns>
    /// Completion after both independent ten-file controls and their cleanup.
    /// </returns>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SourceAndExitBatchesReduceRealQueriesWithFreshSources(bool reserved, bool synchronous)
    {
        async Task<(long FirstQueries, long ExitQueries, long Main, long Native, long Reads)> Measure(bool enabled)
        {
            var recorder = new OperationTimingAccumulator();
            using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false, recorder);
            await fixture.InitializeAsync();
            var files = fixture.Files;
            var paths = Enumerable.Range(0, 10).Select(i => $"batch-{i}.txt").ToArray();
            foreach (var path in paths) File.WriteAllText(Path.Combine(files.GameSessionPath, path), path);
            var times = paths.Select(path => File.GetLastWriteTimeUtc(Path.Combine(files.GameSessionPath, path))).ToArray();
            files.SourceValidationBatchReuseEnabled = enabled;
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            using var owner = reserved ? null : files.BeginSourceDataOperationIfNeeded();
            await using var readSet = reserved ? files.TryBeginReservedValidationReadSet(lease) : null;
            if (reserved) { Assert.NotNull(readSet); await readSet.OpenAsync(); }
            else Assert.NotNull(owner);
            using var first = recorder.StartWindow();
            foreach (var path in paths)
                Assert.Equal(path, synchronous ? files.ReadFileSync(lease, path) : await files.ReadFileAsync(lease, path));
            first.Dispose();
            using var exit = recorder.StartWindow();
            if (reserved) await readSet!.CompleteAsync();
            else await owner!.CompleteAsync();
            exit.Dispose();
            var firstSnapshot = first.Capture();
            var exitSnapshot = exit.Capture();
            Assert.True(firstSnapshot.Complete);
            Assert.True(exitSnapshot.Complete);
            long Count(OperationTimingAccumulator.Snapshot snapshot, string name) => snapshot.Rows.Where(r => r.Name == name).Sum(r => r.Completed);
            var firstQueries = Count(firstSnapshot, "TrustedScope.Probe.GetAttributes");
            var exitQueries = Count(exitSnapshot, "TrustedScope.Probe.GetAttributes");
            var main = Count(firstSnapshot, "MainAdmission.Validate.GuardCheck") + Count(exitSnapshot, "MainAdmission.Validate.GuardCheck");
            var native = Count(firstSnapshot, "PhysicalFileAuthority.ValidateHandlePath") + Count(exitSnapshot, "PhysicalFileAuthority.ValidateHandlePath");
            var name = synchronous ? "OrdinaryRead.Sync.Total" : "OrdinaryRead.Async.Total";
            var reads = Count(firstSnapshot, name) + Count(exitSnapshot, name);
            Assert.Equal(20, reads);
            Assert.Equal(times, paths.Select(path => File.GetLastWriteTimeUtc(Path.Combine(files.GameSessionPath, path))).ToArray());
            foreach (var path in paths) Assert.Equal(path, File.ReadAllText(Path.Combine(files.GameSessionPath, path)));
            output.WriteLine(JsonSerializer.Serialize(new { enabled, reserved, synchronous, firstQueries, exitQueries, main, native, reads, firstSnapshot, exitSnapshot }));
            return (firstQueries, exitQueries, main, native, reads);
        }
        var original = await Measure(false);
        var batched = await Measure(true);
        Assert.True(batched.FirstQueries < original.FirstQueries, $"First source queries: off={original.FirstQueries}, on={batched.FirstQueries}.");
        Assert.True(batched.ExitQueries < original.ExitQueries, $"Exit source queries: off={original.ExitQueries}, on={batched.ExitQueries}.");
        Assert.Equal(original.Main, batched.Main);
        Assert.Equal(original.Native, batched.Native);
        Assert.Equal(original.Reads, batched.Reads);
        Assert.True(batched.Main > 0);
        Assert.True(batched.Native > 0);
    }
}
