using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SourceValidationBatchTests
{
    /// <summary>
    /// Exercises the actual resumer binding factory on tiny files before another full process benchmark.
    /// </summary>
    /// <param name="passive">
    /// Whether the ordinary passive route rather than the detailed callback fallback is selected.
    /// </param>
    /// <returns>
    /// Completion after positive ordinary batch coverage and complete read observations are verified.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualResumerReadBindingsKeepBatchEligibleAndReadCounts(bool passive)
    {
        using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false);
        await fixture.InitializeAsync();
        using var writer = new StringWriter();
        var report = new JointInterfaceCrashStageReport(writer, new string('1', 32), new string('2', 32),
            new(42, DateTime.UnixEpoch, "windows-process-start-utc-ticks", null, 1000), 0);
        var activity = new JointInterfaceResumeActivity(report);
        var bindings = activity.CreateReadBindings(passive);
        var files = new FileSystemManager(fixture.Files.BasePath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                CanonicalReadActivityObserver = bindings.CanonicalObserver,
                ExactPhysicalReadActivityObserver = bindings.ExactObserver,
                PassiveReadActivities = bindings.Passive
            });
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        using var owner = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(owner);
        Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        await owner.CompleteAsync();
        activity.Complete();
        var rows = writer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonDocument.Parse(line).RootElement.Clone()).ToArray();
        var last = rows.Last(row => row.TryGetProperty("ResumeActivity", out _)).GetProperty("ResumeActivity");
        Assert.True(last.GetProperty("CanonicalInitial").GetInt64() >= 2);
        Assert.False(last.GetProperty("Limited").GetBoolean());
        var counts = files.CaptureSourceValidationBatchStatistics();
        output.WriteLine(JsonSerializer.Serialize(new { passive, counts, rows }));
        if (passive)
        {
            Assert.True(counts.Invocations > 0, "Actual ordinary resumer bindings disabled the source batch.");
            Assert.True(counts.Hits > 0);
        }
        else Assert.Equal(0, counts.Invocations);
    }
}
