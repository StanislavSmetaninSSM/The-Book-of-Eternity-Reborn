using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks bounded ordinary-stage output without starting a game or observer process.
/// </summary>
public sealed partial class JointInterfaceCrashStageReportTests
{
    /// <summary>
    /// Counter intervals distinguish repeated rollback observations from other reads and mutation attempts.
    /// </summary>
    [Fact]
    public void ReadCounters_PreserveIntervalCountsAndResetAtActualBoundary()
    {
        var snapshots = new List<JointSourceReadSnapshot>();
        var counter = new JointSourceReadCounter(snapshots.Add);
        counter.InitialRead("game_state/a.json.rollback.request");
        counter.InitialRead("game_state/a.json.rollback.request");
        counter.InitialRead("game_state/b.json.rollback.request");
        counter.InitialRead("game_state/a.json");
        counter.ReturnedRead(); counter.Mutation();
        counter.Advance("post-validated");
        Assert.Equal(new JointSourceReadSnapshot("before-capture", 4, 1, 3, 2, 1, 1, false), snapshots[^1]);
        counter.InitialRead("game_state/a.json.rollback.request");
        counter.Complete();
        Assert.Equal(new JointSourceReadSnapshot("post-validated", 1, 0, 1, 1, 0, 0, false), snapshots[^1]);
    }

    /// <summary>
    /// Path retention and emitted aggregates stay bounded even when callback counts grow.
    /// </summary>
    [Fact]
    public void ReadCounters_BoundPathMemoryAndAggregateEmissions()
    {
        var snapshots = new List<JointSourceReadSnapshot>();
        var counter = new JointSourceReadCounter(snapshots.Add);
        for (var i = 0; i < 20000; i++) counter.InitialRead("x.rollback." + i);
        counter.InitialRead(new string('x', 600) + ".rollback.y");
        counter.Complete();
        Assert.InRange(snapshots.Count, 1, 9);
        var final = snapshots[^1];
        Assert.Equal(20001, final.InitialReads);
        Assert.Equal(20001, final.RollbackReads);
        Assert.Equal(1024, final.UniqueRollbackPaths);
        Assert.Equal(0, final.RepeatedRollbackReads);
        Assert.True(final.PathLimitReached);
    }

    /// <summary>
    /// Repeated existing checkpoints emit only their first scalar boundary and ignore unrelated hooks.
    /// </summary>
    [Fact]
    public void SourceCheckpoints_RecordFirstBoundaryWithoutRepeatedPollNoise()
    {
        using var writer = new StringWriter();
        var report = Create(writer);
        for (var i = 0; i < 100; i++)
        {
            report.ObserveSourceCheckpoint(SessionFinalizationCheckpoint.TerminalWaitStarted);
            report.ObserveSourceCheckpoint(SessionFinalizationCheckpoint.TerminalSignalInspectionLeaseAcquired);
            report.ObserveSourceCheckpoint(SessionFinalizationCheckpoint.TerminalSignalSnapshotCapturedBeforeResolution);
            report.ObserveSourceCheckpoint(SessionFinalizationCheckpoint.AcceptedOutcomeValidatedBeforeMaterialization);
        }
        var stages = writer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonSerializer.Deserialize<JsonElement>(line).GetProperty("Stage").GetString()).ToArray();
        Assert.Equal(new[] { "source-inspection", "source-capture", "source-validated" }, stages);
        Assert.Null(report.OutputFailure);
    }

    /// <summary>
    /// The final available record reports the limit, with fixed newline and byte bounds.
    /// </summary>
    [Fact]
    public void RecordLimit_ReportsLimitWithinIndependentByteBounds()
    {
        using var writer = new StringWriter { NewLine = new string('z', 4096) };
        var report = Create(writer);
        for (var i = 0; i < 100; i++) report.Observe("fixture", "enter");
        var output = writer.ToString();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(64, lines.Length);
        Assert.True(Encoding.UTF8.GetByteCount(output) <= 131072);
        foreach (var line in lines) Assert.True(Encoding.UTF8.GetByteCount(line + "\n") <= 2048);
        using var final = JsonDocument.Parse(lines[^1]);
        Assert.Equal("limit", final.RootElement.GetProperty("Transition").GetString());
        Assert.Null(report.OutputFailure);
    }

    /// <summary>
    /// UTF8-expensive exception type names never produce an oversized stage record.
    /// </summary>
    [Fact]
    public void ErrorType_IsBoundedInCharactersAndEncodedBytes()
    {
        using var writer = new StringWriter();
        var report = Create(writer);
        report.Observe("error", "error", new string('\uffff', 1024));
        var line = Assert.Single(writer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.True(Encoding.UTF8.GetByteCount(line + "\n") <= 2048);
        using var document = JsonDocument.Parse(line);
        Assert.True(document.RootElement.GetProperty("ErrorType").GetString()!.Length <= 256);
    }

    /// <summary>
    /// Unknown stages report an observer failure instead of leaking arbitrary text into the stream.
    /// </summary>
    [Fact]
    public void UnknownStage_ProducesBoundedFailureMarker()
    {
        using var writer = new StringWriter();
        var report = Create(writer);
        report.Observe(new string('x', 10000), "enter");
        Assert.NotNull(report.OutputFailure);
        var line = Assert.Single(writer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.True(Encoding.UTF8.GetByteCount(line + "\n") <= 2048);
        using var document = JsonDocument.Parse(line);
        Assert.Equal("error", document.RootElement.GetProperty("Stage").GetString());
    }

    /// <summary>
    /// Stage records retain independent run/nonce identity and nondecreasing elapsed time.
    /// </summary>
    [Fact]
    public void IdentityAndElapsed_RemainExplicitAndMonotonic()
    {
        using var writer = new StringWriter();
        var report = Create(writer);
        report.Observe("fixture", "enter"); report.Observe("fixture", "return");
        var lines = writer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        using var first = JsonDocument.Parse(lines[0]);
        using var last = JsonDocument.Parse(lines[1]);
        Assert.Equal("crash5_stage_v1", first.RootElement.GetProperty("Boundary").GetString());
        Assert.Equal(new string('1', 32), first.RootElement.GetProperty("RunId").GetString());
        Assert.Equal(new string('2', 32), first.RootElement.GetProperty("Nonce").GetString());
        Assert.Equal(42, first.RootElement.GetProperty("Pid").GetInt32());
        Assert.Equal("windows-process-start-utc-ticks", first.RootElement.GetProperty("BirthKind").GetString());
        Assert.True(last.RootElement.GetProperty("ElapsedMilliseconds").GetDouble() >= first.RootElement.GetProperty("ElapsedMilliseconds").GetDouble());
    }

    /// <summary>
    /// Output failures are recorded without replacing an exception already unwinding the game call.
    /// </summary>
    /// <param name="disposed">
    /// Selects a disposed writer rather than a general I/O failure.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteFailure_DoesNotReplacePrimaryFailure(bool disposed)
    {
        Exception output = disposed ? new ObjectDisposedException("writer") : new IOException("write failed");
        using var writer = new FailingWriter(output);
        var report = Create(writer);
        var primary = new InvalidOperationException("game failure");
        var observed = Record.Exception((Action)(() =>
        {
            try { throw primary; }
            finally { report.Observe("error", "error", primary.GetType().Name); }
        }));
        Assert.Same(primary, observed);
        Assert.Equal(output.GetType().Name, report.OutputFailure);
    }

    /// <summary>
    /// Builds a reporter with fixed independently checked identity values.
    /// </summary>
    /// <param name="writer">
    /// The output sink under test.
    /// </param>
    /// <returns>
    /// A new reporter without shared mutable state.
    /// </returns>
    private static JointInterfaceCrashStageReport Create(TextWriter writer) => new(writer, new string('1', 32), new string('2', 32),
        new(42, DateTime.UnixEpoch, "windows-process-start-utc-ticks", null, 1000), 1);

    /// <summary>
    /// Fails at the real writer boundary to verify observer error precedence.
    /// </summary>
    /// <param name="error">
    /// The specific output failure to throw.
    /// </param>
    private sealed class FailingWriter(Exception error) : TextWriter
    {
        /// <inheritdoc/>
        public override Encoding Encoding => Encoding.UTF8;
        /// <inheritdoc/>
        public override void Write(string? value) => throw error;
        /// <inheritdoc/>
        public override void Flush() => throw new InvalidOperationException("The reporter must not flush or wait on an observer.");
    }
}
