using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Reads only the source owner's executed exchange prefix while the original draft contains a later exchange.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualExecutedConflictPrefix_ExcludesUnexecutedDraftSuffix()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var first = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(first.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(first.Step?.Interval);

        var executed = capture.ReadExecutedConflictPrefix(lease, interval);
        var active = Assert.IsType<JsonObject>(executed["activeConflict"]);
        var log = Assert.IsType<JsonArray>(active["exchangeLog"]);
        Assert.Single(log);
        Assert.Equal("exchange_conflict_frame_42", (string?)log[0]?["exchangeId"]);
        active["conflictId"] = "mutated_detached_copy";
        Assert.NotEqual("mutated_detached_copy", (string?)capture.ReadExecutedConflictPrefix(
            lease, interval)["activeConflict"]?["conflictId"]);
        Assert.Equal(2, Assert.IsType<JsonArray>((await context.ReadJsonAsync(
            AfterlifeSpiritualConflictState.StatePath))!["activeConflict"]!["exchangeLog"]).Count);

        var second = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(second.Issues);
        var nextInterval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(second.Step?.Interval);
        Assert.Throws<InvalidOperationException>(() => capture.ReadExecutedConflictPrefix(lease, interval));
        Assert.Equal(2, Assert.IsType<JsonArray>(capture.ReadExecutedConflictPrefix(
            lease, nextInterval)["activeConflict"]!["exchangeLog"]).Count);
    }
}
