using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Revokes previously issued source tickets when their shared allocation journal faults.
    /// </summary>
    [Fact]
    public async Task SourceAllocationFaultRevokesPreviouslyIssuedTicket()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]", requireScope: true);
        var clock = new SpiritualWoundProjectionClock(journal, new RejectSourceProbeClock());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var acquired = await context.Validator.BeginLiveSpiritualWoundSourceSessionAsync(
            lease, projectionClock: clock);
        AssertNoConflictFrameErrors(acquired.Issues);
        var owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(acquired.Session);
        var prepared = await owner.PrepareContinuationAsync(lease);
        AssertNoConflictFrameErrors(prepared.Issues);
        Assert.NotNull(prepared.Ticket);
        Assert.Throws<InvalidOperationException>(() => journal.Request("effect", "effects", "outside",
            () => "effect_0123456789abcdef0123456789abcdef"));
        Assert.False(owner.IsCurrentOwner);
        Assert.False(owner.Owns(Assert.Single(owner.Sources)));
        var committed = owner.CommitPreparedContinuation(lease, prepared.Ticket!);
        Assert.Null(committed.Session);
        Assert.Contains(committed.Issues, issue => issue.Code == "spiritual_source_session_revoked");
    }

    /// <summary>
    /// Discards a prospective terminal closure without consuming the next accepted identity during replay.
    /// </summary>
    [Fact]
    public async Task SourceProbeDiscardsTerminalProjectionDuringStrictReplay()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        const string identity = "effect_0123456789abcdef0123456789abcdef";
        var record = SpiritualWoundReplayJournal.CreateAppend("[]");
        record.Request("effect", "effects", "next/root", () => identity);
        var rows = record.Export().ToJsonString();
        var replay = SpiritualWoundReplayJournal.CreateReplay(rows);
        var clock = new SpiritualWoundProjectionClock(replay, new RejectSourceProbeClock());
        ValidationService.SpiritualWoundSourceSession owner;
        string retained;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var acquired = await context.Validator.BeginLiveSpiritualWoundSourceSessionAsync(
                lease, projectionClock: clock);
            AssertNoConflictFrameErrors(acquired.Issues);
            owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(acquired.Session);
            retained = owner.BuildInputBinding().ToJsonString();
        }
        var root = await ReadProjectedSourceContinuationCandidateAsync(context);
        root[AfterlifeSpiritualConflictState.ResponseField] = new JsonObject
        {
            ["mode"] = "repair_cancel",
            ["resolution"] = new JsonObject { ["resolvedAtTurn"] = 42 }
        };
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await using var continuation = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var canonical = await context.FileSystem.ReadFileBytesAsync(
            continuation, AfterlifeSpiritualConflictState.StatePath);
        var source = Assert.Single(owner.Sources);
        var dice = owner.ClaimedDice.ToArray();
        AssertNoConflictFrameErrors(await owner.CheckContinuationInputsAsync(continuation));
        AssertNoConflictFrameErrors(await owner.CheckContinuationInputsAsync(continuation));
        Assert.Equal(retained, owner.BuildInputBinding().ToJsonString());
        Assert.Empty(owner.PendingRequirements);
        Assert.Equal(0, owner.ContinuationRevision);
        Assert.Same(source, Assert.Single(owner.Sources));
        Assert.Equal(dice, owner.ClaimedDice);
        Assert.Equal(canonical, await context.FileSystem.ReadFileBytesAsync(
            continuation, AfterlifeSpiritualConflictState.StatePath));
        Assert.Equal(identity, replay.Request("effect", "effects", "next/root",
            () => throw new InvalidOperationException("Replay allocated an identity.")));
        Assert.Equal(rows, replay.Export().ToJsonString());
        var acceptedAttempt = await owner.PrepareContinuationAsync(continuation);
        Assert.Null(acceptedAttempt.Ticket);
        Assert.Contains(acceptedAttempt.Issues, issue => issue.Code == "spiritual_source_continuation_invalid");
        Assert.Throws<InvalidOperationException>(() => replay.Export());
    }

    /// <summary>
    /// Rejects ordinary time reads by a discarded source projection.
    /// </summary>
    private sealed class RejectSourceProbeClock : AcceptedTurnProjectionClock
    {
        /// <inheritdoc/>
        internal override DateTimeOffset GetUtcNow(AcceptedTurnProjectionTimeKind role, JsonObject evidence) =>
            throw new InvalidOperationException("A source probe read ordinary time.");
    }
}
