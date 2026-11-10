using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Core;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Qualifies complete detached named-event transport and real metadata reuse on independent small fixtures.
/// </summary>
public sealed class PassiveNamedTimingTransportTests(ITestOutputHelper output)
{
    /// <summary>
    /// Persists every original clock, scope and Unicode name without substituting transport time.
    /// </summary>
    [Fact]
    public void FullTraceRetainsOriginalEventsAndClosedAccounting()
    {
        using var capture = CreateCapture(nameof(FullTraceRetainsOriginalEventsAndClosedAccounting));
        capture.RequirePassiveTiming();
        var recorder = new OperationTimingAccumulator();
        using var window = recorder.StartWindow(captureEvents: true);
        const string name = "original \"timing\"\n日本語 🩹";
        for (var index = 0; index < 1100; index++) using (recorder.Measure(name)) { }
        window.Dispose();
        var events = window.DrainEvents();
        var beforeTransport = Stopwatch.GetTimestamp();
        foreach (var row in events) capture.Timings.ObserveRecorded(row);
        capture.CompletePassiveTiming(window.Capture(), window.CaptureEventStatus());
        capture.Finish();
        var rows = ReadTimingRows(capture.RecordsPath);
        Assert.Equal(2200, rows.Length);
        for (var index = 0; index < rows.Length; index++)
        {
            var actual = rows[index]; var original = events[index];
            Assert.Equal(name, actual.GetProperty("Operation").GetString());
            Assert.Equal(original.State, actual.GetProperty("State").GetString());
            Assert.Equal(original.Sequence, actual.GetProperty("PassiveSequence").GetInt64());
            Assert.Equal(original.ScopeId, actual.GetProperty("ScopeId").GetInt64());
            Assert.Equal(original.TimestampTicks, actual.GetProperty("TimestampTicks").GetInt64());
            Assert.True(actual.GetProperty("TimestampTicks").GetInt64() <= beforeTransport);
            Assert.Equal(original.ElapsedTicks, actual.GetProperty("ElapsedTicks").GetInt64());
            Assert.False(actual.GetProperty("Late").GetBoolean());
            Assert.Equal(original.ElapsedTicks * 1000d / Stopwatch.Frequency,
                actual.GetProperty("Milliseconds").GetDouble());
            Assert.Equal("sealed-passive-events-v1", actual.GetProperty("Transport").GetString());
        }
        using var report = JsonDocument.Parse(File.ReadAllText(capture.ReportPath));
        Assert.True(report.RootElement.GetProperty("DiagnosticComplete").GetBoolean());
        Assert.True(report.RootElement.GetProperty("PassiveTimingRequired").GetBoolean());
        var completion = report.RootElement.GetProperty("PassiveTimingCompletion");
        Assert.True(completion.GetProperty("Complete").GetBoolean());
        Assert.Equal(2200, completion.GetProperty("ExpectedRecords").GetInt64());
        Assert.Equal(2200, report.RootElement.GetProperty("TimingStored").GetInt64());
        AssertExactFileHash(capture, report.RootElement);
    }

    /// <summary>
    /// Refuses successful diagnostic completion when a declared full window has no final receipt.
    /// </summary>
    [Fact]
    public void MissingFinalWindowReceiptCannotReportSuccess()
    {
        var capture = CreateCapture(nameof(MissingFinalWindowReceiptCannotReportSuccess));
        capture.RequirePassiveTiming();
        Assert.Throws<AggregateException>(capture.Finish);
        using var report = JsonDocument.Parse(File.ReadAllText(capture.ReportPath));
        Assert.False(report.RootElement.GetProperty("DiagnosticComplete").GetBoolean());
        Assert.Throws<AggregateException>(capture.Finish);
    }

    /// <summary>
    /// Keeps open, late and undrained windows explicitly incomplete.
    /// </summary>
    /// <param name="condition">The original incomplete state presented at closing.</param>
    [Theory]
    [InlineData("open")]
    [InlineData("late")]
    [InlineData("undrained")]
    public void IncompleteWindowCannotBecomeQualifiedCapture(string condition)
    {
        var capture = CreateCapture(nameof(IncompleteWindowCannotBecomeQualifiedCapture));
        capture.RequirePassiveTiming();
        var recorder = new OperationTimingAccumulator();
        using var window = recorder.StartWindow(captureEvents: true);
        var scope = Assert.IsAssignableFrom<IDisposable>(recorder.Measure("incomplete"));
        if (condition == "undrained") scope.Dispose();
        window.Dispose();
        if (condition == "late") scope.Dispose();
        if (condition != "undrained")
            foreach (var row in window.DrainEvents()) capture.Timings.ObserveRecorded(row);
        capture.CompletePassiveTiming(window.Capture(), window.CaptureEventStatus());
        // Preserve the late tail too; a previously invalid receipt cannot be overwritten by later success.
        scope.Dispose();
        foreach (var row in window.DrainEvents()) capture.Timings.ObserveRecorded(row);
        Assert.Throws<AggregateException>(capture.Finish);
        using var report = JsonDocument.Parse(File.ReadAllText(capture.ReportPath));
        Assert.False(report.RootElement.GetProperty("DiagnosticComplete").GetBoolean());
        Assert.False(report.RootElement.GetProperty("PassiveTimingCompletion").GetProperty("Complete").GetBoolean());
        Assert.Throws<InvalidOperationException>(() => capture.CompletePassiveTiming(window.Capture(), window.CaptureEventStatus()));
    }

    /// <summary>
    /// Defers storage failure until after the exact functional failure has remained primary.
    /// </summary>
    [Fact]
    public void FailedWriterCannotMaskBodyFailureOrReportComplete()
    {
        var capture = CreateCapture(nameof(FailedWriterCannotMaskBodyFailureOrReportComplete), failedWriter: true);
        capture.RequirePassiveTiming();
        var recorder = new OperationTimingAccumulator();
        using var window = recorder.StartWindow(captureEvents: true);
        var primary = new InvalidDataException("original functional failure");
        Action body = () => { using var scope = recorder.Measure("body"); throw primary; };
        Assert.Same(primary, Record.Exception(body));
        window.Dispose();
        foreach (var row in window.DrainEvents()) capture.Timings.ObserveRecorded(row);
        capture.CompletePassiveTiming(window.Capture(), window.CaptureEventStatus());
        Assert.Equal(2, capture.Timings.Attempted);
        Assert.Equal(0, capture.Timings.Stored);
        Assert.Contains(capture.Timings.GetFailures(), row => row.Error is ObjectDisposedException);
        Assert.Throws<AggregateException>(capture.Finish);
        using var report = JsonDocument.Parse(File.ReadAllText(capture.ReportPath));
        Assert.False(report.RootElement.GetProperty("DiagnosticComplete").GetBoolean());
    }

    /// <summary>
    /// Removes actual repeated metadata queries without removing original file sources or fresh guards.
    /// </summary>
    /// <param name="synchronous">Whether the original typed source uses its sync reader family.</param>
    /// <returns>Completion after legacy, aggregate and full-event modes agree on source DATA and fresh fences.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FullEventsPreserveCachesAndReduceRealMetadataAgainstLegacy(bool synchronous)
    {
        async Task<Counts> Run(string mode)
        {
            using var capture = CreateCapture("metadata-" + mode);
            var ends = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
            var measuring = false;
            var recorder = mode == "legacy" ? null : new OperationTimingAccumulator();
            Action<string, string, TimeSpan>? legacy = mode == "legacy" ? (name, state, elapsed) =>
            {
                if (!measuring) return;
                if (state == "end") ends.AddOrUpdate(name, 1, (_, value) => value + 1);
                capture.Timings.Observe(name, state, elapsed);
            } : null;
            using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false, recorder, legacy);
            await fixture.InitializeAsync();
            var before = fixture.Files.CaptureAdmissionDirectoryStatistics();
            using var window = recorder?.StartWindow(captureEvents: mode == "full");
            if (mode == "full") capture.RequirePassiveTiming();
            measuring = true;
            await fixture.ReadEightAsync(synchronous);
            measuring = false;
            window?.Dispose();
            if (window is not null)
            {
                foreach (var row in window.Capture().Rows) ends[row.Name] = row.Completed;
                Assert.True(window.Capture().Complete);
                if (mode == "full")
                {
                    foreach (var row in window.DrainEvents()) capture.Timings.ObserveRecorded(row);
                    capture.CompletePassiveTiming(window.Capture(), window.CaptureEventStatus());
                }
            }
            var counts = new Counts(ends.GetValueOrDefault("TrustedScope.Probe.GetAttributes"),
                ends.GetValueOrDefault("MainAdmission.Validate.Guard"),
                ends.GetValueOrDefault("Lease.Validate.Total"), fixture.Observers.ReadCount("present.txt"),
                fixture.Files.CaptureAdmissionDirectoryStatistics() - before);
            Assert.Equal("original", File.ReadAllText(fixture.RawPath));
            Assert.Equal(2, counts.FileSources);
            capture.Finish();
            return counts;
        }

        var legacy = await Run("legacy");
        var aggregate = await Run("aggregate");
        var full = await Run("full");
        Assert.True(legacy.Attributes > 0);
        Assert.True(full.Attributes < legacy.Attributes);
        Assert.Equal(aggregate, full);
        Assert.True(full.MainGuards > 0);
        Assert.True(full.LeaseGuards > 0);
        Assert.Equal(legacy.MainGuards, full.MainGuards);
        Assert.Equal(legacy.LeaseGuards, full.LeaseGuards);
        Assert.Equal(0, legacy.Directories.Invocations);
        Assert.True(full.Directories.Invocations > 0);
        Assert.True(full.Directories.Hits > 0);
        output.WriteLine(JsonSerializer.Serialize(new { synchronous, Legacy = legacy, Aggregate = aggregate, Full = full }));
    }

    /// <summary>
    /// Keeps the existing same-timestamp drift refusal while collecting every original event.
    /// </summary>
    /// <returns>Completion after the failed read and its complete diagnostic window remain distinct.</returns>
    [Fact]
    public async Task FullEventsRetainFreshDriftRefusal()
    {
        using var capture = CreateCapture(nameof(FullEventsRetainFreshDriftRefusal));
        capture.RequirePassiveTiming();
        var recorder = new OperationTimingAccumulator();
        using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false, recorder);
        await fixture.InitializeAsync();
        using var window = recorder.StartWindow(captureEvents: true);
        var timestamp = File.GetLastWriteTimeUtc(fixture.RawPath);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.ReadEightAsync(after: () =>
        {
            File.WriteAllText(fixture.RawPath, "changed!");
            File.SetLastWriteTimeUtc(fixture.RawPath, timestamp);
        }));
        window.Dispose();
        foreach (var row in window.DrainEvents()) capture.Timings.ObserveRecorded(row);
        capture.CompletePassiveTiming(window.Capture(), window.CaptureEventStatus());
        capture.Finish();
        Assert.Equal("changed!", File.ReadAllText(fixture.RawPath));
        Assert.Equal(2, fixture.Observers.ReadCount("present.txt"));
    }

    private sealed record Counts(long Attributes, long MainGuards, long LeaseGuards, long FileSources,
        AdmissionDirectoryStatistics Directories);

    private JointBundleDiagnosticOutput CreateCapture(string name, bool failedWriter = false) =>
        new(name, output, disposeWriterForControl: failedWriter,
            repositoryRootForControl: Path.Combine(Environment.GetEnvironmentVariable("BOE_TEST_DIAGNOSTIC_ROOT")
                ?? Path.GetTempPath(), "passive-named-controls", Guid.NewGuid().ToString("N")));

    private static JsonElement[] ReadTimingRows(string path) => File.ReadLines(path).Select(line =>
    {
        using var parsed = JsonDocument.Parse(line);
        return parsed.RootElement.Clone();
    }).Where(row => row.TryGetProperty("Boundary", out var boundary) && boundary.GetString() == "joint_operation_timing").ToArray();

    private static void AssertExactFileHash(JointBundleDiagnosticOutput capture, JsonElement report)
    {
        var bytes = File.ReadAllBytes(capture.RecordsPath);
        Assert.Equal(bytes.LongLength, report.GetProperty("StoredBytes").GetInt64());
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)), report.GetProperty("Sha256").GetString());
    }
}
