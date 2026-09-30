using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Exports effective definitions and transition evidence from the current resource owner prefix.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualResourceCandidatePrefix_TracksOnlyClosedExecution()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var firstStep = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(firstStep.Issues);
        var firstInterval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(
            firstStep.Step?.Interval);

        var first = capture.ReadClosedResourceCandidatePrefix(lease, firstInterval);
        Assert.NotNull(JsonNode.Parse(first.DefinitionsJson));
        Assert.NotNull(JsonNode.Parse(first.OwnerAuthorityJson));
        Assert.Equal(capture.ReadClosedResourcePrefix(lease, firstInterval).StateJson, first.StateJson);
        Assert.Equal(capture.ReadClosedResourcePrefix(lease, firstInterval).HistoryJson, first.HistoryJson);
        Assert.NotEmpty(first.AppliedTransitions.Concat(first.ReplayTransitions));

        var secondStep = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(secondStep.Issues);
        var secondInterval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(
            secondStep.Step?.Interval);
        Assert.Throws<InvalidOperationException>(() => capture.ReadClosedResourceCandidatePrefix(
            lease, firstInterval));
        var second = capture.ReadClosedResourceCandidatePrefix(lease, secondInterval);
        Assert.True(second.AppliedTransitions.Count + second.ReplayTransitions.Count >
            first.AppliedTransitions.Count + first.ReplayTransitions.Count);
        Assert.Equal(first.DefinitionsJson, second.DefinitionsJson);
    }

    /// <summary>
    /// Reads the owned first resource prefix without freezing the later exchange or its history.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualResourcePrefixView_RemainsOpenForLaterExchange()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var firstStep = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(firstStep.Issues);
        var firstInterval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(
            firstStep.Step?.Interval);
        var executor = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(
            OriginalCaptureField(capture, "_resources"));
        var executionState = OriginalCaptureField(executor, "_state");
        Assert.NotNull(executionState);
        var history = Assert.IsType<ResourceHistoryWorkingSet>(OriginalCaptureField(
            executionState, "workingHistory"));
        var freezeCount = history.FreezeCount;
        var rebuildCount = history.WholeHistoryRebuildCount;

        var first = capture.ReadClosedResourcePrefix(lease, firstInterval);
        Assert.NotNull(JsonNode.Parse(first.StateJson));
        Assert.NotNull(JsonNode.Parse(first.HistoryJson));
        Assert.Equal(first.StateJson, capture.ReadClosedResourcePrefix(lease, firstInterval).StateJson);
        Assert.Equal(freezeCount, history.FreezeCount);
        Assert.Equal(rebuildCount, history.WholeHistoryRebuildCount);

        var secondStep = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(secondStep.Issues);
        var secondInterval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(
            secondStep.Step?.Interval);
        Assert.Throws<InvalidOperationException>(() => capture.ReadClosedResourcePrefix(lease, firstInterval));
        var second = capture.ReadClosedResourcePrefix(lease, secondInterval);
        Assert.NotEqual(first.HistoryJson, second.HistoryJson);
        capture.Dispose();
        Assert.Throws<ObjectDisposedException>(() => capture.ReadClosedResourcePrefix(lease, secondInterval));
    }

    /// <summary>
    /// Rejects a prior closed prefix while the next exchange awaits its missing audit side.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualResourcePrefixView_RejectsPendingNextExchange()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        var conflict = await ReadProjectedSourceContinuationCandidateAsync(context);
        conflict["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!.AsObject()
            .Remove("opposition");
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath,
            conflict.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var first = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(first.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(first.Step?.Interval);
        Assert.NotNull(capture.ReadClosedResourcePrefix(lease, interval));

        var next = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(next.Issues);
        Assert.NotNull(next.Step?.PendingExchange);
        Assert.Throws<InvalidOperationException>(() => capture.ReadClosedResourcePrefix(lease, interval));
    }
}
