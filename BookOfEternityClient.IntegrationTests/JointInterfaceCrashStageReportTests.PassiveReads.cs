using System.Diagnostics;
using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointInterfaceCrashStageReportTests
{
    /// <summary>
    /// Replays every queued event and the original due-sample populations without a record quota.
    /// </summary>
    [Fact]
    public void PassiveReadReplayUsesCapturedTimesAndKeepsEveryPopulation()
    {
        using var writer = new StringWriter();
        var activity = new JointInterfaceResumeActivity(Create(writer));
        var bindings = activity.CreateReadBindings(true);
        Assert.Null(bindings.CanonicalObserver); Assert.Null(bindings.ExactObserver);
        var queue = Assert.IsType<PassiveReadActivityAccumulator>(bindings.Passive);
        var start = Stopwatch.GetTimestamp();
        for (var i = 0; i < 20000; i++) queue.RecordForTests(PassiveReadActivityKind.ExactInitial, start);
        queue.RecordForTests(PassiveReadActivityKind.ExactInitial, start + 16 * Stopwatch.Frequency);
        queue.RecordForTests(PassiveReadActivityKind.CanonicalInitial, start + 32 * Stopwatch.Frequency);
        Assert.Empty(ResumeRecords(writer));
        activity.Complete();
        var rows = ResumeRecords(writer);
        Assert.Equal(4, rows.Length);
        Assert.Equal(1, rows[0].GetProperty("ResumeActivity").GetProperty("ExactInitial").GetInt64());
        Assert.Equal(20001, rows[1].GetProperty("ResumeActivity").GetProperty("ExactInitial").GetInt64());
        Assert.Equal(1, rows[2].GetProperty("ResumeActivity").GetProperty("CanonicalInitial").GetInt64());
        Assert.All(rows, row => Assert.False(row.GetProperty("ResumeActivity").GetProperty("Limited").GetBoolean()));
        Assert.Equal(rows.Select(row => row.GetProperty("ElapsedMilliseconds").GetDouble()).Order(),
            rows.Select(row => row.GetProperty("ElapsedMilliseconds").GetDouble()));
        Assert.Empty(queue.Drain());
    }

    /// <summary>
    /// Drains before entry resets the phase and before callback-return reporting.
    /// </summary>
    [Fact]
    public void PassiveReadReplayPartitionsBothCheckpointTransitions()
    {
        using var writer = new StringWriter();
        var activity = new JointInterfaceResumeActivity(Create(writer));
        var queue = activity.CreateReadBindings(true).Passive!;
        queue.Record(PassiveReadActivityKind.CanonicalInitial);
        var point = SessionFinalizationCheckpoint.MortalJointCustodyCaptureCompletedBeforePreparation;
        activity.Checkpoint(point, true);
        queue.Record(PassiveReadActivityKind.ExactInitial);
        activity.Checkpoint(point, false);
        var rows = ResumeRecords(writer);
        var before = rows.First(row => row.TryGetProperty("ResumeActivity", out _)).GetProperty("ResumeActivity");
        Assert.Equal("before-finalization", before.GetProperty("Phase").GetString());
        Assert.Equal(1, before.GetProperty("CanonicalInitial").GetInt64());
        var after = rows.Last(row => row.TryGetProperty("ResumeActivity", out _)).GetProperty("ResumeActivity");
        Assert.Equal(point.ToString(), after.GetProperty("Phase").GetString());
        Assert.Equal(1, after.GetProperty("ExactInitial").GetInt64());
        Assert.Equal(0, after.GetProperty("CanonicalInitial").GetInt64());
        Assert.Equal("finalization-after", rows[^1].GetProperty("Stage").GetString());
        Assert.Empty(queue.Drain());
    }

    /// <summary>
    /// Drains a late tail once and leaves an already completed empty tail unwritten.
    /// </summary>
    [Fact]
    public void PassiveReadTailCompletesOnceWithoutRepeatingAnEmptyTail()
    {
        using var writer = new StringWriter();
        var activity = new JointInterfaceResumeActivity(Create(writer));
        var queue = activity.CreateReadBindings(true).Passive!;
        queue.Record(PassiveReadActivityKind.CanonicalInitial);
        activity.Complete(); var prior = ResumeRecords(writer).Length;
        activity.CompletePendingReadTail(); Assert.Equal(prior, ResumeRecords(writer).Length);
        queue.Record(PassiveReadActivityKind.ExactInitial);
        activity.CompletePendingReadTail();
        Assert.Equal(prior + 1, ResumeRecords(writer).Length);
        activity.CompletePendingReadTail(); Assert.Equal(prior + 1, ResumeRecords(writer).Length);
        Assert.Equal(1, ResumeRecords(writer)[^1].GetProperty("ResumeActivity").GetProperty("ExactInitial").GetInt64());
    }

    /// <summary>
    /// Broken deferred output cannot replace the game error being unwound through final cleanup.
    /// </summary>
    [Fact]
    public void PassiveReadErrorTailPreservesPrimaryException()
    {
        using var writer = new FailingWriter(new IOException("output"));
        var report = Create(writer); var activity = new JointInterfaceResumeActivity(report);
        var queue = activity.CreateReadBindings(true).Passive!;
        queue.Record(PassiveReadActivityKind.CanonicalInitial);
        var primary = new InvalidOperationException("game");
        var actual = Record.Exception((Action)(() => { try { throw primary; } finally { activity.CompletePendingReadTail(); } }));
        Assert.Same(primary, actual); Assert.Equal(nameof(IOException), report.OutputFailure);
        Assert.Empty(queue.Drain());
    }

    /// <summary>
    /// Detaches event arrays exactly once and rejects undefined populations or backwards fixture times.
    /// </summary>
    [Fact]
    public void PassiveReadDrainIsDetachedAndExactlyOnce()
    {
        var queue = new PassiveReadActivityAccumulator();
        queue.RecordForTests(PassiveReadActivityKind.CanonicalInitial, 10);
        var first = queue.Drain(); Assert.Single(first); Assert.Empty(queue.Drain());
        first[0] = new(PassiveReadActivityKind.ExactInitial, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => queue.RecordForTests(PassiveReadActivityKind.ExactInitial, 9));
        Assert.Throws<ArgumentOutOfRangeException>(() => queue.RecordForTests((PassiveReadActivityKind)42, 11));
        queue.RecordForTests(PassiveReadActivityKind.CanonicalInitial, 11);
        Assert.Equal(new PassiveReadActivityEvent(PassiveReadActivityKind.CanonicalInitial, 11), Assert.Single(queue.Drain()));
    }

    /// <summary>
    /// Preserves every concurrently recorded event and its timestamp order across repeated drains.
    /// </summary>
    /// <returns>
    /// Completion after all finite producers settle and every event is accounted for once.
    /// </returns>
    [Fact]
    public async Task PassiveReadConcurrentDrainConservesEveryEvent()
    {
        var queue = new PassiveReadActivityAccumulator();
        var work = Task.WhenAll(Enumerable.Range(0, 4).Select(index => Task.Run(() =>
        {
            for (var i = 0; i < 2000; i++) queue.Record(index % 2 == 0 ? PassiveReadActivityKind.CanonicalInitial : PassiveReadActivityKind.ExactInitial);
        })));
        var events = new List<PassiveReadActivityEvent>();
        while (!work.IsCompleted) { events.AddRange(queue.Drain()); await Task.Yield(); }
        await work; events.AddRange(queue.Drain());
        Assert.Equal(8000, events.Count);
        Assert.Equal(4000, events.Count(row => row.Kind == PassiveReadActivityKind.CanonicalInitial));
        Assert.Equal(events.Select(row => row.Timestamp).Order(), events.Select(row => row.Timestamp));
        Assert.Empty(queue.Drain());
    }
}
