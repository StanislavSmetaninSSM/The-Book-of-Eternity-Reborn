using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualWoundProjectionClockTests
{
    /// <summary>
    /// Prevents the former component-injection constructor from corrupting another healthy clock.
    /// </summary>
    [Fact]
    public void Speculation_ForeignClosedTokenCannotOverwriteHealthyMemo()
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var clock = new SpiritualWoundProjectionClock(journal, new CountingClock());
        var expected = Project("legacy", clock);
        var rows = journal.Export().ToJsonString();
        var foreign = SpiritualWoundReplayJournal.CreateAppend("[]");
        var foreignToken = foreign.BeginSpeculation();
        foreignToken.Dispose();
        // Exercise the old assembly-visible injection route if it ever reappears.
        var constructor = typeof(SpiritualWoundProjectionClock.Speculation).GetConstructor(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, new[] { typeof(SpiritualWoundProjectionClock),
                typeof(SpiritualWoundReplayJournal.Speculation), typeof(Dictionary<string, DateTimeOffset>) }, null);
        if (constructor is not null)
        {
            try
            {
                using var forged = (IDisposable)constructor.Invoke(new object[]
                    { clock, foreignToken, new Dictionary<string, DateTimeOffset>() });
            }
            catch (System.Reflection.TargetInvocationException error)
                when (error.InnerException is InvalidOperationException or ArgumentException) { }
        }
        Assert.Equal(expected, Project("legacy", clock));
        Assert.Equal(rows, journal.Export().ToJsonString());
    }

    /// <summary>
    /// Discards rejected projection time while preserving prior memo entries and accepted cold replay.
    /// </summary>
    [Fact]
    public void Speculation_RejectedProjectionDoesNotEnterAcceptedReplay()
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var ordinary = new CountingClock();
        var clock = new SpiritualWoundProjectionClock(journal, ordinary);
        var baseline = Project("legacy", clock);
        using (clock.BeginSpeculation())
        {
            Project("resolve", clock);
            Assert.Throws<InvalidOperationException>(() => journal.Export());
        }
        Assert.Single(journal.Export());
        Assert.Equal(baseline, Project("legacy", clock));
        string accepted;
        using (var attempt = clock.BeginSpeculation())
        {
            accepted = Project("archive", clock);
            attempt.Commit();
        }
        Assert.Equal(3, ordinary.Calls);
        var rows = journal.Export().ToJsonString();
        var replay = SpiritualWoundReplayJournal.CreateReplay(rows);
        var replayClock = new SpiritualWoundProjectionClock(replay, new CountingClock { Reject = true });
        Assert.Equal(baseline, Project("legacy", replayClock));
        Assert.Equal(accepted, Project("archive", replayClock));
        Assert.Equal(rows, replay.Export().ToJsonString());
    }

    /// <summary>
    /// Restores both the consumed retained prefix and memo, allowing an exact retry with no clock callback.
    /// </summary>
    [Fact]
    public void Speculation_ReplayRollbackRestoresCursorAndMemo()
    {
        var record = SpiritualWoundReplayJournal.CreateAppend("[]");
        var originalClock = new SpiritualWoundProjectionClock(record, new CountingClock());
        var expected = Project("resolve", originalClock);
        var rows = record.Export().ToJsonString();
        var replay = SpiritualWoundReplayJournal.CreateReplay(rows);
        var clock = new SpiritualWoundProjectionClock(replay, new CountingClock { Reject = true });
        using (clock.BeginSpeculation()) Assert.Equal(expected, Project("resolve", clock));
        Assert.Throws<InvalidOperationException>(() => replay.Export());
        using (var attempt = clock.BeginSpeculation())
        {
            Assert.Equal(expected, Project("resolve", clock));
            attempt.Commit();
        }
        Assert.Equal(rows, replay.Export().ToJsonString());
    }

    /// <summary>
    /// Removes rejected memo entries and identity indexes so retry must create retained rows again.
    /// </summary>
    [Fact]
    public void Speculation_AppendRetryRestoresAllIndexes()
    {
        const string identity = "effect_0123456789abcdef0123456789abcdef";
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var ordinary = new CountingClock();
        var clock = new SpiritualWoundProjectionClock(journal, ordinary);
        string rejected;
        using (clock.BeginSpeculation())
        {
            rejected = Project("resolve", clock);
            journal.Request("effect", "effects", "event:1/root:0", () => identity);
        }
        Assert.Empty(journal.Export());
        using (var attempt = clock.BeginSpeculation())
        {
            Assert.NotEqual(rejected, Project("resolve", clock));
            Assert.Equal(identity, journal.Request("effect", "effects", "event:1/root:0", () => identity));
            attempt.Commit();
        }
        Assert.Equal(2, ordinary.Calls);
        Assert.Equal(2, journal.Export().Count);
    }

    /// <summary>
    /// Leaves callback faults permanent even when an uncommitted scope is cleaned up.
    /// </summary>
    [Fact]
    public void Speculation_DisposeNeverRevivesFaultedJournal()
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var clock = new SpiritualWoundProjectionClock(journal, new CountingClock { Reject = true });
        using (var attempt = clock.BeginSpeculation())
        {
            Assert.Throws<NotSupportedException>(() => Project("resolve", clock));
            Assert.Throws<InvalidOperationException>(() => attempt.Commit());
        }
        Assert.Throws<InvalidOperationException>(() => journal.Export());
        Assert.Throws<InvalidOperationException>(() => clock.BeginSpeculation());
    }

    /// <summary>
    /// Keeps stale cleanup harmless while a newer scope owns the same journal.
    /// </summary>
    [Fact]
    public void Speculation_CompletedCleanupCannotRollbackNewScope()
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var clock = new SpiritualWoundProjectionClock(journal, new CountingClock());
        var first = clock.BeginSpeculation();
        Project("legacy", clock);
        first.Commit();
        using (var second = clock.BeginSpeculation())
        {
            Project("archive", clock);
            first.Dispose();
            first.Dispose();
            second.Commit();
        }
        Assert.Equal(2, journal.Export().Count);
    }

    /// <summary>
    /// Rejects nested ownership and reused commit tokens without reviving an attempt.
    /// </summary>
    /// <param name="nested">
    /// <see langword="true"/> attempts nesting; otherwise the same token commits twice.
    /// </param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Speculation_InvalidScopeUseFaultsAttempt(bool nested)
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var clock = new SpiritualWoundProjectionClock(journal, new CountingClock());
        using var scope = clock.BeginSpeculation();
        if (nested) Assert.Throws<InvalidOperationException>(() => clock.BeginSpeculation());
        else
        {
            scope.Commit();
            Assert.Throws<InvalidOperationException>(() => scope.Commit());
        }
        Assert.Throws<InvalidOperationException>(() => journal.Export());
    }

    /// <summary>
    /// Allows admission only through an active token belonging to the exact clock, without faulting a foreign owner.
    /// </summary>
    [Fact]
    public void Speculation_AdmissionGuardRejectsForeignAndClosedTokensWithoutDamagingOwner()
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var clock = new SpiritualWoundProjectionClock(journal, new CountingClock());
        var foreignJournal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var foreignClock = new SpiritualWoundProjectionClock(foreignJournal, new CountingClock());
        using var scope = clock.BeginSpeculation();
        using var foreign = foreignClock.BeginSpeculation();

        var guard = typeof(SpiritualWoundProjectionClock.Speculation).GetMethod(
            "EnsureActiveFor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(guard);
        Assert.Throws<ArgumentNullException>(() => InvokeGuard(guard, scope, null!));
        Assert.Throws<InvalidOperationException>(() => InvokeGuard(guard, scope, foreignClock));
        InvokeGuard(guard, scope, clock);
        InvokeGuard(guard, foreign, foreignClock);
        scope.Commit();
        Assert.Throws<InvalidOperationException>(() => InvokeGuard(guard, scope, clock));
        using var disposed = clock.BeginSpeculation();
        disposed.Dispose();
        Assert.Throws<InvalidOperationException>(() => InvokeGuard(guard, disposed, clock));
        foreign.Dispose();
        Assert.Empty(journal.Export());
        Assert.Empty(foreignJournal.Export());
    }

    /// <summary>
    /// Invokes the scope guard and exposes its original failure for ownership assertions.
    /// </summary>
    /// <param name="guard">
    /// Reflected scope-ownership guard.
    /// </param>
    /// <param name="scope">
    /// Scope whose ownership and lifetime are checked.
    /// </param>
    /// <param name="expected">
    /// Clock expected to own the scope.
    /// </param>
    private static void InvokeGuard(System.Reflection.MethodInfo guard,
        SpiritualWoundProjectionClock.Speculation scope, SpiritualWoundProjectionClock expected)
    {
        try { guard.Invoke(scope, [expected]); }
        catch (System.Reflection.TargetInvocationException exception) when (exception.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
