using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks capture journal scope enforcement independently of gameplay authority.
/// </summary>
public sealed class SpiritualWoundAllocationScopeTests
{
    private const string Identity = "effect_0123456789abcdef0123456789abcdef";

    /// <summary>
    /// Completes combined cleanup after an allocation callback unsuccessfully tries to dispose it early.
    /// </summary>
    /// <param name="probe">
    /// Whether cleanup belongs to a discarded conflict probe instead of an ordinary combined scope.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GuardedCombinedCleanupRetriesAfterCallback(bool probe)
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]", requireScope: true);
        var clock = new SpiritualWoundProjectionClock(journal, new AcceptedTurnProjectionClock());
        using IDisposable scope = probe ? clock.BeginConflictValidationProbe(Array.Empty<JsonNode?>()) :
            clock.BeginSpeculation();
        clock.GetUtcNow(AcceptedTurnProjectionTimeKind.ConflictResolution, new JsonObject());
        Assert.Throws<InvalidOperationException>(() => journal.Request("effect", "effects", "root", () =>
        {
            try { scope.Dispose(); }
            catch (InvalidOperationException) { }
            return Identity;
        }));
        scope.Dispose();
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic;
        Assert.Null(typeof(SpiritualWoundReplayJournal).GetField("_activeSpeculation", flags)!.GetValue(journal));
        Assert.Null(typeof(SpiritualWoundProjectionClock).GetField("_activeConflictProbe", flags)!.GetValue(clock));
        Assert.Empty(Assert.IsType<Dictionary<string, DateTimeOffset>>(
            typeof(SpiritualWoundProjectionClock).GetField("_values", flags)!.GetValue(clock)));
        Assert.Throws<InvalidOperationException>(() => journal.Export());
    }

    /// <summary>
    /// Restores a discarded guarded replay cursor and commits its later accepted retry.
    /// </summary>
    [Fact]
    public void GuardedReplayRollsBackThenCommitsInsideScopes()
    {
        var record = SpiritualWoundReplayJournal.CreateAppend("[]");
        record.Request("effect", "effects", "root", () => Identity);
        var rows = record.Export().ToJsonString();
        var replay = SpiritualWoundReplayJournal.CreateReplay(rows, requireScope: true);
        using (replay.BeginSpeculation())
            Assert.Equal(Identity, replay.Request("effect", "effects", "root",
                () => throw new InvalidOperationException("Replay called allocator.")));
        Assert.Throws<InvalidOperationException>(() => replay.Export());
        using (var scope = replay.BeginSpeculation())
        {
            Assert.Equal(Identity, replay.Request("effect", "effects", "root",
                () => throw new InvalidOperationException("Replay called allocator.")));
            scope.Commit();
        }
        Assert.Equal(rows, replay.Export().ToJsonString());
    }

    /// <summary>
    /// Rejects both new allocation and retained cursor consumption outside a capture operation.
    /// </summary>
    /// <param name="replay">
    /// Whether the tested request consumes a previously recorded row.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GuardedRequestRequiresScope(bool replay)
    {
        var record = SpiritualWoundReplayJournal.CreateAppend("[]");
        record.Request("effect", "effects", "root", () => Identity);
        var journal = replay
            ? SpiritualWoundReplayJournal.CreateReplay(record.Export().ToJsonString(), requireScope: true)
            : SpiritualWoundReplayJournal.CreateAppend("[]", requireScope: true);
        var calls = 0;
        Assert.Throws<InvalidOperationException>(() => journal.Request(
            "effect", "effects", "root", () => { calls++; return Identity; }));
        Assert.Equal(0, calls);
        Assert.Throws<InvalidOperationException>(() => journal.BeginSpeculation());
        Assert.Throws<InvalidOperationException>(() => journal.Export());
    }

    /// <summary>
    /// Allows accepted memo reads outside a scope while rejecting a newly requested time.
    /// </summary>
    [Fact]
    public void GuardedClockRetainsMemoButRequiresScopeForNewTime()
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]", requireScope: true);
        var clock = new SpiritualWoundProjectionClock(journal, new AcceptedTurnProjectionClock());
        var evidence = new JsonObject { ["conflictId"] = "first" };
        DateTimeOffset accepted;
        using (var scope = clock.BeginSpeculation())
        {
            accepted = clock.GetUtcNow(AcceptedTurnProjectionTimeKind.ConflictResolution, evidence);
            scope.Commit();
        }
        Assert.Equal(accepted, clock.GetUtcNow(AcceptedTurnProjectionTimeKind.ConflictResolution, evidence));
        Assert.Single(journal.Export());
        Assert.Throws<InvalidOperationException>(() => clock.GetUtcNow(
            AcceptedTurnProjectionTimeKind.ConflictResolution, new JsonObject { ["conflictId"] = "second" }));
        Assert.Throws<InvalidOperationException>(() => clock.GetUtcNow(
            AcceptedTurnProjectionTimeKind.ConflictResolution, evidence));
    }

    /// <summary>
    /// Prevents allocation callbacks from closing or reentering their owning transaction.
    /// </summary>
    /// <param name="action">
    /// Callback action attempting to alter the active request lifetime.
    /// </param>
    [Theory]
    [InlineData("commit")]
    [InlineData("dispose")]
    [InlineData("request")]
    public void GuardedCallbackCannotChangeRequestLifetime(string action)
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]", requireScope: true);
        using var scope = journal.BeginSpeculation();
        Assert.Throws<InvalidOperationException>(() => journal.Request("effect", "effects", "root", () =>
        {
            try
            {
                if (action == "commit") scope.Commit();
                else if (action == "dispose") scope.Dispose();
                else journal.Request("effect", "effects", "nested",
                    () => "effect_1123456789abcdef0123456789abcdef");
            }
            catch (InvalidOperationException) { }
            return Identity;
        }));
        Assert.Throws<InvalidOperationException>(() => journal.Export());
    }
}
