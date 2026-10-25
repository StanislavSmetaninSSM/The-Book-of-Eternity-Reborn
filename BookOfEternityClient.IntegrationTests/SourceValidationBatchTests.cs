using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Measures bounded directory DATA with real source readers and independent tiny sessions.
/// </summary>
/// <param name="output">
/// Receives uncapped named measurements and owned fixture cleanup evidence.
/// </param>
public sealed partial class SourceValidationBatchTests(ITestOutputHelper output)
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

    /// <summary>
    /// Requires an independent fresh directory epoch and original source comparison before success.
    /// </summary>
    /// <param name="reserved">
    /// Whether the exact reservation owns the original source observations.
    /// </param>
    /// <param name="change">
    /// Raw same-timestamp byte change, appearance, disappearance or ancestor type replacement.
    /// </param>
    /// <returns>
    /// Completion after both fresh-loop implementations reject changed source data.
    /// </returns>
    [Theory]
    [InlineData(false, "bytes")]
    [InlineData(false, "appearance")]
    [InlineData(false, "disappearance")]
    [InlineData(false, "ancestor")]
    [InlineData(true, "bytes")]
    [InlineData(true, "appearance")]
    [InlineData(true, "disappearance")]
    [InlineData(true, "ancestor")]
    public async Task ExitEpochRejectsRawSourceDrift(bool reserved, string change)
    {
        using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false);
        await fixture.InitializeAsync();
        var files = fixture.Files;
        Directory.CreateDirectory(Path.Combine(files.GameSessionPath, "child"));
        var relative = change == "ancestor" ? "child/data.txt" : "data.txt";
        var path = Path.Combine(files.GameSessionPath, relative);
        if (change != "appearance") File.WriteAllText(path, "original");
        var stamp = File.GetLastWriteTimeUtc(path);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        using var owner = reserved ? null : files.BeginSourceDataOperationIfNeeded();
        await using var set = reserved ? files.TryBeginReservedValidationReadSet(lease) : null;
        if (reserved) { Assert.NotNull(set); await set.OpenAsync(); }
        else Assert.NotNull(owner);
        Assert.Equal(change == "appearance" ? null : "original", await files.ReadFileAsync(lease, relative));
        Assert.True(files.CaptureSourceValidationBatchStatistics().Hits > 0);
        if (change == "bytes") { File.WriteAllText(path, "changed!"); File.SetLastWriteTimeUtc(path, stamp); }
        else if (change == "appearance") File.WriteAllText(path, "appeared");
        else if (change == "disappearance") File.Delete(path);
        else
        {
            File.Delete(path);
            Directory.Delete(Path.GetDirectoryName(path)!);
            File.WriteAllText(Path.GetDirectoryName(path)!, "now a file");
        }
        var failure = await Record.ExceptionAsync(() => reserved ? set!.CompleteAsync() : owner!.CompleteAsync());
        Assert.NotNull(failure);
        Assert.IsAssignableFrom<IOException>(failure);
    }

    /// <summary>
    /// Prevents initial directory DATA from surviving arbitrary consumer code between two source calls.
    /// </summary>
    /// <returns>
    /// Completion after a changed ancestor is freshly refused by the next initial read.
    /// </returns>
    [Fact]
    public async Task InitialBatchClosesBeforeReturningToConsumer()
    {
        using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false);
        await fixture.InitializeAsync();
        var files = fixture.Files;
        var directory = Path.Combine(files.GameSessionPath, "first");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "data.txt"), "original");
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        using var owner = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(owner);
        Assert.Equal("original", await files.ReadFileAsync(lease, "first/data.txt"));
        Assert.True(files.CaptureSourceValidationBatchStatistics().Hits > 0);
        File.Delete(Path.Combine(directory, "data.txt")); Directory.Delete(directory);
        File.WriteAllText(directory, "now a file");
        await Assert.ThrowsAsync<InvalidDataException>(() => files.ReadFileAsync(lease, "first/other.txt"));
    }

    /// <summary>
    /// Keeps arbitrary activity callbacks and functional read forwarders on the fresh directory route.
    /// </summary>
    /// <param name="activityOnly">
    /// Whether to install only the arbitrary activity receiver rather than the existing functional forwarders.
    /// </param>
    /// <returns>
    /// Completion after source counting and original fallback are verified.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ArbitraryCallbacksKeepFreshDirectoryRoute(bool activityOnly)
    {
        using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, !activityOnly);
        await fixture.InitializeAsync();
        var calls = 0;
        var files = activityOnly ? new FileSystemManager(fixture.Files.BasePath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks { CanonicalReadActivityObserver = _ => calls++ }) : fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        using var owner = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(owner);
        Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        await owner.CompleteAsync();
        Assert.Equal(0, files.CaptureSourceValidationBatchStatistics().Invocations);
        if (activityOnly) Assert.True(calls >= 2);
    }
}
