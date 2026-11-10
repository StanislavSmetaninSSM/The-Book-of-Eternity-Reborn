using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointInterfaceCrashStageReportTests
{
    /// <summary>
    /// Every real enum retains its name, transition and independent child identity.
    /// </summary>
    [Fact]
    public void ResumeCheckpoints_NameEveryActualBoundaryWithIdentity()
    {
        using var writer = new StringWriter();
        var activity = new JointInterfaceResumeActivity(Create(writer));
        foreach (var point in Enum.GetValues<SessionFinalizationCheckpoint>())
        {
            activity.Checkpoint(point, true); activity.Checkpoint(point, false);
        }
        var records = ResumeRecords(writer);
        Assert.Equal(20, records.Length);
        foreach (var (point, i) in Enum.GetValues<SessionFinalizationCheckpoint>().Select((p, i) => (p, i)))
        {
            Assert.Equal(point.ToString(), records[2*i].GetProperty("Checkpoint").GetString());
            Assert.Equal(point.ToString(), records[2*i+1].GetProperty("Checkpoint").GetString());
            Assert.Equal("finalization-before", records[2*i].GetProperty("Stage").GetString());
            Assert.Equal("finalization-after", records[2*i+1].GetProperty("Stage").GetString());
            Assert.Equal("enter", records[2*i].GetProperty("Transition").GetString());
            Assert.Equal("return", records[2*i+1].GetProperty("Transition").GetString());
            Assert.Equal(new string('1',32), records[2*i].GetProperty("RunId").GetString());
            Assert.Equal(new string('2',32), records[2*i].GetProperty("Nonce").GetString());
            Assert.Equal(42, records[2*i].GetProperty("Pid").GetInt32());
        }
    }

    /// <summary>
    /// Read, lock and publication populations remain separate and reset at a named boundary.
    /// </summary>
    [Fact]
    public void ResumeActivity_SeparatesHookPopulationsAndCheckpointIntervals()
    {
        using var writer = new StringWriter();
        var activity = new JointInterfaceResumeActivity(Create(writer), () => TimeSpan.Zero);
        foreach (var kind in Enum.GetValues<JointResumeObservation>()) activity.Observe(kind);
        activity.Publication(TrustedLocalPublicationPhase.IntentPublished, false);
        activity.Publication(TrustedLocalPublicationPhase.MemberRestored, true);
        activity.Checkpoint(SessionFinalizationCheckpoint.MortalJointCustodyCaptureCompletedBeforePreparation, true);
        var prior = ResumeRecords(writer).Where(x => x.TryGetProperty("ResumeActivity", out _)).Last().GetProperty("ResumeActivity");
        foreach (var name in new[] { "CanonicalInitial", "ExactInitial", "CanonicalOpened", "CanonicalContended", "MainContended", "Publications", "Recoveries" })
            Assert.Equal(1, prior.GetProperty(name).GetInt64());
        Assert.Equal("before-finalization", prior.GetProperty("Phase").GetString());
        Assert.Equal("IntentPublished", prior.GetProperty("LastPublication").GetString());
        Assert.Equal("MemberRestored", prior.GetProperty("LastRecovery").GetString());
        activity.Observe(JointResumeObservation.ExactInitial); activity.Complete();
        var current = ResumeRecords(writer).Last().GetProperty("ResumeActivity");
        Assert.Equal("MortalJointCustodyCaptureCompletedBeforePreparation", current.GetProperty("Phase").GetString());
        Assert.Equal(1, current.GetProperty("ExactInitial").GetInt64());
        Assert.Equal(0, current.GetProperty("CanonicalInitial").GetInt64());
        Assert.Equal(0, current.GetProperty("Publications").GetInt64());
    }

    /// <summary>
    /// Event-driven samples continue after large read counts without timers or count thresholds.
    /// </summary>
    [Fact]
    public void ResumeActivity_SamplesLateEventsAfterLargeReadCounts()
    {
        using var writer = new StringWriter(); var now = TimeSpan.Zero;
        var activity = new JointInterfaceResumeActivity(Create(writer), () => now);
        for (var i=0; i<20000; i++) activity.Observe(JointResumeObservation.ExactInitial);
        Assert.Single(ResumeRecords(writer));
        now = TimeSpan.FromSeconds(14); activity.Observe(JointResumeObservation.ExactInitial);
        Assert.Single(ResumeRecords(writer));
        now = TimeSpan.FromSeconds(15); activity.Observe(JointResumeObservation.ExactInitial);
        Assert.Equal(2, ResumeRecords(writer).Length);
        now = TimeSpan.FromSeconds(210); activity.Observe(JointResumeObservation.ExactInitial);
        Assert.Equal(20003, ResumeRecords(writer).Last().GetProperty("ResumeActivity").GetProperty("ExactInitial").GetInt64());
        Assert.Equal(3, ResumeRecords(writer).Length);
    }

    /// <summary>
    /// Activity continues beyond its former quota and preserves the final cumulative sample and terminal record.
    /// </summary>
    [Fact]
    public void ResumeActivity_ContinuesBeyondFormerQuotasThroughCompletion()
    {
        using var writer = new StringWriter(); var now = TimeSpan.Zero; var report = Create(writer);
        var activity = new JointInterfaceResumeActivity(report, () => now);
        for (var i=0; i<100; i++) { now = TimeSpan.FromSeconds(i*15); activity.Observe(JointResumeObservation.CanonicalInitial); }
        activity.Complete(); report.Observe("exit", "return");
        var records = ResumeRecords(writer);
        Assert.Equal(102, records.Length);
        Assert.All(records[..^1], record => Assert.False(record.GetProperty("ResumeActivity").GetProperty("Limited").GetBoolean()));
        Assert.Equal(100, records[^2].GetProperty("ResumeActivity").GetProperty("CanonicalInitial").GetInt64());
        Assert.Equal("exit", records[^1].GetProperty("Stage").GetString());
        Assert.Equal("return", records[^1].GetProperty("Transition").GetString());
        Assert.Null(report.OutputFailure);
    }

    /// <summary>
    /// Broken output cannot replace the failure being unwound by the game call.
    /// </summary>
    [Fact]
    public void ResumeActivity_OutputFailurePreservesPrimaryException()
    {
        using var writer = new FailingWriter(new IOException("output")); var report = Create(writer);
        var activity = new JointInterfaceResumeActivity(report);
        var primary = new InvalidOperationException("game");
        var actual = Record.Exception((Action)(() => { try { throw primary; } finally { activity.Observe(JointResumeObservation.CanonicalContended); activity.Checkpoint(SessionFinalizationCheckpoint.LateTerminalAndIdleOperationBound, true); } }));
        Assert.Same(primary, actual); Assert.Equal(nameof(IOException), report.OutputFailure);
    }

    /// <summary>
    /// Parses copied scalar records without retaining JsonDocument ownership.
    /// </summary>
    /// <param name="writer">
    /// The isolated in-memory output sink.
    /// </param>
    /// <returns>
    /// All complete JSONL values written by this child reporter.
    /// </returns>
    private static JsonElement[] ResumeRecords(StringWriter writer) => writer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => JsonSerializer.Deserialize<JsonElement>(line)).ToArray();
}
