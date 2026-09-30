using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    [Theory]
    [InlineData("repair")]
    [InlineData("append")]
    [InlineData("accepted_prefix")]
    [InlineData("future_coordinate")]
    [InlineData("future_operation")]
    [InlineData("future_actor")]
    [InlineData("future_die")]
    public async Task OriginalSpiritualFrontier_OnlyUnacceptedSuffixCanBeRevised(string change)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var firstOnly = await ReadProjectedSourceContinuationCandidateAsync(context);
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        var corrected = await ReadProjectedSourceContinuationCandidateAsync(context);
        var initial = corrected.DeepClone();
        initial["activeConflict"]!["exchangeLog"]![1]!["diceAudit"]!["margin"] = 999;
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath,
            (change == "append" ? firstOnly : initial).ToJsonString());
        ValidationService.SpiritualOriginalTurnCapture capture;
        ValidationService.SpiritualWoundSourceSession owner;
        ValidationService.PreparedSpiritualSource firstSource;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
            AssertNoConflictFrameErrors(captured.Issues);
            capture = captured.Capture!;
            AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
            var first = await capture.AdvanceNextResourceExchangeAsync(lease);
            AssertNoConflictFrameErrors(first.Issues);
            Assert.NotNull(first.Step?.Interval);
            owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
            firstSource = Assert.Single(owner.Sources);
        }
        using (capture)
        {
            if (change == "accepted_prefix")
                corrected["activeConflict"]!["exchangeLog"]![0]!["outcome"] = "failure";
            if (change == "future_coordinate")
                corrected["activeConflict"]!["exchangeLog"]![1]!["exchangeId"] = "rebound_future";
            if (change == "future_operation")
                corrected["activeConflict"]!["exchangeLog"]![1]!["operationType"] = "guard";
            if (change == "future_actor")
                corrected["activeConflict"]!["exchangeLog"]![1]!["incomingAction"] =
                    new System.Text.Json.Nodes.JsonObject { ["actorId"] = "another_guardian" };
            if (change == "future_die")
                corrected["activeConflict"]!["exchangeLog"]![1]!["diceAudit"]!["diceUsed"]![0]!["sourceIndex"] = 0;
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, corrected.ToJsonString());
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            var freshness = await owner.PrepareContinuationAsync(lease);
            if (change.StartsWith("future_", StringComparison.Ordinal))
                Assert.Null(freshness.Ticket);
            var next = await capture.AdvanceNextResourceExchangeAsync(lease);
            if (change is "repair" or "append")
            {
                AssertNoConflictFrameErrors(next.Issues);
                Assert.Equal(1, next.Step!.Interval!.Ordinal);
                Assert.Equal(2, owner.Sources.Count);
                Assert.Equal(new[] { 0, 1, 2, 3 }, owner.ClaimedDice);
            }
            else
            {
                Assert.NotEmpty(next.Issues);
                Assert.Null(next.Step);
                Assert.Single(owner.Sources);
                Assert.Equal(new[] { 0, 1 }, owner.ClaimedDice);
            }
            Assert.Same(firstSource, owner.Sources[0]);
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        }
    }

    [Fact]
    public async Task OriginalSpiritualFrontier_MissingSideCompletionDoesNotValidateFutureExchange()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        var full = await ReadProjectedSourceContinuationCandidateAsync(context);
        full["activeConflict"]!["exchangeLog"]![1]!["diceAudit"]!["margin"] = 999;
        var partial = full.DeepClone();
        partial["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!.AsObject().Remove("opposition");
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, partial.ToJsonString());
        ValidationService.SpiritualOriginalTurnCapture capture;
        AcceptedMechanicsPlanner.PendingSpiritualExchange wait;
        ValidationService.SpiritualWoundSourceSession source;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
            AssertNoConflictFrameErrors(captured.Issues);
            capture = captured.Capture!;
            AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
            var first = await capture.AdvanceNextResourceExchangeAsync(lease);
            AssertNoConflictFrameErrors(first.Issues);
            wait = Assert.IsType<AcceptedMechanicsPlanner.PendingSpiritualExchange>(first.Step!.PendingExchange);
            Assert.Empty(capture.ReadClosedExchangeEvidence(lease));
            source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
            Assert.Empty(source.Sources);
            Assert.Empty(source.ClaimedDice);
        }
        using (capture)
        {
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, full.ToJsonString());
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            var completed = await capture.ResumeMissingAuditSideAsync(lease, wait);
            AssertNoConflictFrameErrors(completed.Issues);
            Assert.Equal(0, completed.Step!.Interval!.Ordinal);
            var closedEvidence = Assert.Single(capture.ReadClosedExchangeEvidence(lease));
            Assert.Equal(completed.Step.Interval.ExchangeId, closedEvidence.ExchangeId);
            Assert.Equal(new[] { 0, 1 }, closedEvidence.DiceClaims.Select(claim => claim.SourceIndex));
            var acceptedSource = Assert.Single(source.Sources);
            Assert.Equal(new[] { 0, 1 }, source.ClaimedDice);
            var second = await capture.AdvanceNextResourceExchangeAsync(lease);
            Assert.Null(second.Step);
            Assert.NotEmpty(second.Issues);
            Assert.Same(acceptedSource, Assert.Single(source.Sources));
            Assert.Equal(new[] { 0, 1 }, source.ClaimedDice);
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        }
    }

    [Fact]
    public async Task OriginalSpiritualFrontier_CompletedExecutorCannotMintNextTicket()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        AssertNoConflictFrameErrors((await capture.AdvanceNextResourceExchangeAsync(lease)).Issues);
        var resource = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(OriginalCaptureField(capture, "_resources"));
        Assert.True(resource.Drain().IsValid);
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        var next = await ValidationService.SpiritualWoundSourceSession.PreparedContinuation.PrepareAsync(source, lease, capture);
        Assert.Null(next.Ticket);
        Assert.Contains(next.Issues, issue => issue.Code == "spiritual_source_frontier_owner_mismatch");
    }
}
