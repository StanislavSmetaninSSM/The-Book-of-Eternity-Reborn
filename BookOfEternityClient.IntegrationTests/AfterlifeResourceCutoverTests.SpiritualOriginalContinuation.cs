using System.Collections;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    [Fact]
    public async Task OriginalSpiritualContinuation_MissingSideUsesSignedTicketAndSameResourceOwner()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var full = await ReadSourceMissingCandidateAsync(context);
        var partial = full.DeepClone().AsObject();
        partial["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!.AsObject().Remove("opposition");
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, partial.ToJsonString());
        object capture;
        AcceptedMechanicsPlanner.PendingSpiritualExchange wait;
        ValidationService.SpiritualWoundSourceSession source;
        AcceptedMechanicsPlanner.ResourceExecutionSession resources;
        string binding;
        long revision;
        ResourceTransition[] known;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            (capture, wait, source, resources) = await BeginOriginalMissingWaitAsync(context, lease);
            binding = source.BuildInputBinding().ToJsonString();
            revision = source.ContinuationRevision;
            Assert.Empty(source.Sources);
            Assert.Empty(source.ClaimedDice);
            known = OriginalContinuationApplied(resources);
            Assert.Single(known);
            Assert.Equal("exchange_conflict_frame_42", known[0].OriginId);
            // OLD has already captured a real signed source and executed the
            // known-side cost before the missing common API assertion below.
        }
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, full.ToJsonString());
        await using var continuation = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = await ResumeOriginalMissingAsync(capture, continuation, wait);
        AssertNoConflictFrameErrors(result.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(result.Step!.Interval);
        Assert.True(resources.Owns(interval));
        Assert.Same(resources, OriginalCaptureField(capture, "_resources"));
        Assert.Same(source, OriginalCaptureField(capture, "_source"));
        Assert.Equal(revision + 1, source.ContinuationRevision);
        Assert.NotEqual(binding, source.BuildInputBinding().ToJsonString());
        Assert.Equal(new[] { 0, 1 }, source.ClaimedDice);
        Assert.True(source.Owns(Assert.Single(source.Sources)));
        Assert.Contains(known[0], interval.AppliedTransitions);
        var added = Assert.Single(interval.AppliedTransitions, value => value.EventRef.EndsWith(":opposition"));
        Assert.StartsWith("spiritual_resume_", added.OriginId);
        var consumed = Assert.Single(Assert.IsAssignableFrom<IEnumerable>(
            OriginalCaptureField(capture, "_consumedContinuations")).Cast<object>());
        Assert.Same(result.Step, OriginalCaptureProperty(consumed, "AcceptedStep"));
        var prepared = OriginalCaptureProperty(consumed, "MissingSide")!;
        Assert.Same(wait, OriginalCaptureProperty(prepared, "Expected"));
        Assert.Same(resources, OriginalCaptureProperty(prepared, "Owner"));
        var aliases = Assert.IsAssignableFrom<IReadOnlyDictionary<ResourceOperationKey, ResourceOperationKey>>(
            OriginalCaptureProperty(prepared, "Aliases"));
        Assert.Equal(added.OriginId, Assert.Single(aliases).Value.OriginId);
        Assert.NotNull(OriginalCaptureProperty(consumed, "SourceTicket"));
        var stale = await ResumeOriginalMissingAsync(capture, continuation, wait);
        Assert.Null(stale.Step);
        Assert.NotEmpty(stale.Issues);
        Assert.Equal(revision + 1, source.ContinuationRevision);
        Assert.Equal(interval.AppliedTransitions, OriginalContinuationApplied(resources));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, continuation));
    }

    [Theory]
    [InlineData("known_side")]
    [InlineData("forged_zero")]
    [InlineData("foreign_wait")]
    public async Task OriginalSpiritualContinuation_InvalidMissingEvidenceLeavesBothPrefixesRetryable(string change)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var full = await ReadSourceMissingCandidateAsync(context);
        var partial = full.DeepClone().AsObject();
        partial["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!.AsObject().Remove("opposition");
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, partial.ToJsonString());
        object capture;
        AcceptedMechanicsPlanner.PendingSpiritualExchange wait;
        ValidationService.SpiritualWoundSourceSession source;
        AcceptedMechanicsPlanner.ResourceExecutionSession resources;
        string binding;
        long revision;
        ResourceTransition[] known;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            (capture, wait, source, resources) = await BeginOriginalMissingWaitAsync(context, lease);
            binding = source.BuildInputBinding().ToJsonString();
            revision = source.ContinuationRevision;
            known = OriginalContinuationApplied(resources);
        }
        var bad = full.DeepClone().AsObject();
        if (change == "known_side")
            bad["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!["player"]!["operationType"] = "guard";
        if (change == "forged_zero")
        {
            bad["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!["opposition"]!["effectiveCost"] = 0;
            bad["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!["opposition"]!["after"] = 6;
        }
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, bad.ToJsonString());
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var rejected = await ResumeOriginalMissingAsync(capture, lease,
                change == "foreign_wait" ? wait with { } : wait);
            Assert.Null(rejected.Step);
            Assert.NotEmpty(rejected.Issues);
            Assert.Equal(binding, source.BuildInputBinding().ToJsonString());
            Assert.Equal(revision, source.ContinuationRevision);
            Assert.Empty(source.ClaimedDice);
            Assert.Equal(known, OriginalContinuationApplied(resources));
            Assert.True(source.IsCurrentOwner);
            Assert.Same(wait, Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionStep>(
                OriginalCaptureField(capture, "_lastResourceStep")).PendingExchange);
        }
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, full.ToJsonString());
        await using var repairedLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var repaired = await ResumeOriginalMissingAsync(capture, repairedLease, wait);
        AssertNoConflictFrameErrors(repaired.Issues);
        Assert.Equal(2, repaired.Step!.Interval!.AppliedTransitions.Count);
        Assert.Equal(revision + 1, source.ContinuationRevision);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, repairedLease));
    }

    [Fact]
    public async Task OriginalSpiritualContinuation_TwoMissingFillsKeepFirstAliasAndDelaySourceDice()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var full = await ReadSourceMissingCandidateAsync(context);
        var empty = full.DeepClone().AsObject();
        empty["activeConflict"]!["exchangeLog"]![0]!.AsObject().Remove("actionCostAudit");
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, empty.ToJsonString());
        object capture;
        AcceptedMechanicsPlanner.PendingSpiritualExchange wait;
        ValidationService.SpiritualWoundSourceSession source;
        AcceptedMechanicsPlanner.ResourceExecutionSession resources;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            (capture, wait, source, resources) = await BeginOriginalMissingWaitAsync(context, lease);
        var player = full.DeepClone().AsObject();
        player["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!.AsObject().Remove("opposition");
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, player.ToJsonString());
        ResourceTransition first;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var partial = await ResumeOriginalMissingAsync(capture, lease, wait);
            AssertNoConflictFrameErrors(partial.Issues);
            var next = Assert.IsType<AcceptedMechanicsPlanner.PendingSpiritualExchange>(partial.Step!.PendingExchange);
            Assert.NotSame(wait, next);
            wait = next;
            first = Assert.Single(OriginalContinuationApplied(resources));
            Assert.StartsWith("spiritual_resume_", first.OriginId);
            Assert.Empty(source.Sources);
            Assert.Empty(source.ClaimedDice);
        }
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, full.ToJsonString());
        await using var completeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var complete = await ResumeOriginalMissingAsync(capture, completeLease, wait);
        AssertNoConflictFrameErrors(complete.Issues);
        Assert.Contains(first, complete.Step!.Interval!.AppliedTransitions);
        Assert.Equal(2, complete.Step.Interval.AppliedTransitions.Select(value => value.OriginId).Distinct().Count());
        Assert.Equal(2, Assert.IsAssignableFrom<IEnumerable>(OriginalCaptureField(capture,
            "_consumedContinuations")).Cast<object>().Count());
        Assert.Equal(new[] { 0, 1 }, source.ClaimedDice);
        Assert.True(source.Owns(Assert.Single(source.Sources)));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, completeLease));
    }

    [Fact]
    public async Task OriginalSpiritualContinuation_LateMissingResourceFailureRevokesWithoutSourceCommit()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var full = await ReadSourceMissingCandidateAsync(context);
        var partial = full.DeepClone().AsObject();
        partial["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!.AsObject().Remove("player");
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, partial.ToJsonString());
        await context.WriteExactJsonAsync(ResourceMaterializationContract.CommandPath,
            OriginalCaptureDefinitionAndActionPointCommand(4m, 0m).ToJsonString());
        object capture;
        AcceptedMechanicsPlanner.PendingSpiritualExchange wait;
        ValidationService.SpiritualWoundSourceSession source;
        AcceptedMechanicsPlanner.ResourceExecutionSession resources;
        long revision;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            (capture, wait, source, resources) = await BeginOriginalMissingWaitAsync(context, lease);
            Assert.Equal(2, OriginalContinuationApplied(resources).Length);
            revision = source.ContinuationRevision;
        }
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, full.ToJsonString());
        await using var continuation = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var stateBefore = await context.FileSystem.ReadFileBytesAsync(continuation, ResourceMaterializationContract.StatePath);
        var historyBefore = await context.FileSystem.ReadFileBytesAsync(continuation, ResourceMaterializationContract.HistoryPath);
        var failed = await ResumeOriginalMissingAsync(capture, continuation, wait);
        Assert.Null(failed.Step);
        Assert.Contains(failed.Issues, issue => issue.Code == "resource_mutation_below_minimum");
        Assert.Equal(revision, source.ContinuationRevision);
        Assert.Empty(source.ClaimedDice);
        Assert.False(source.IsCurrentOwner);
        Assert.False(Assert.IsType<bool>(OriginalCaptureProperty(capture, "IsCurrentOwner")));
        Assert.False(EffectAcceptedTurnPlanAuthority.TryPeekValidated(context.FileSystem, continuation, out _));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, continuation));
        Assert.Equal(stateBefore, await context.FileSystem.ReadFileBytesAsync(continuation, ResourceMaterializationContract.StatePath));
        Assert.Equal(historyBefore, await context.FileSystem.ReadFileBytesAsync(continuation, ResourceMaterializationContract.HistoryPath));
        Assert.False(resources.Result!.IsValid);
        Assert.Null(resources.Result.StateAfterImage);
    }

    [Fact]
    public async Task OriginalSpiritualContinuation_CommandFileEditStillRevokesOwnedWait()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var partial = await ReadSourceMissingCandidateAsync(context);
        partial["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!.AsObject().Remove("opposition");
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, partial.ToJsonString());
        object capture;
        AcceptedMechanicsPlanner.PendingSpiritualExchange wait;
        ValidationService.SpiritualWoundSourceSession source;
        AcceptedMechanicsPlanner.ResourceExecutionSession resources;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            (capture, wait, source, resources) = await BeginOriginalMissingWaitAsync(context, lease);
        await context.WriteExactJsonAsync(EffectAcceptedTurnPlan.CommandPath,
            new JsonObject { ["resourceResolutionReceipts"] = new JsonArray() }.ToJsonString());
        await using var continuation = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var failed = await ResumeOriginalMissingAsync(capture, continuation, wait);
        Assert.Null(failed.Step);
        Assert.Contains(failed.Issues, issue => issue.Code == "spiritual_original_input_changed");
        Assert.False(source.IsCurrentOwner);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, continuation));
    }

    private static async Task<(object Capture, AcceptedMechanicsPlanner.PendingSpiritualExchange Wait,
        ValidationService.SpiritualWoundSourceSession Source,
        AcceptedMechanicsPlanner.ResourceExecutionSession Resources)> BeginOriginalMissingWaitAsync(
        ResourceMaterializationTestContext context, FileSystemManager.CanonicalWriteLease lease)
    {
        var captured = await InvokeOriginalCaptureAsync(context.Validator, "CaptureSpiritualOriginalTurnAsync", lease);
        AssertNoConflictFrameErrors(OriginalCaptureIssues(captured));
        var capture = OriginalCaptureProperty(captured, "Capture")!;
        Assert.NotNull(capture);
        var begun = await InvokeOriginalCaptureAsync(capture, "BeginResourceExecutionAsync", lease);
        AssertNoConflictFrameErrors(Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(begun));
        var result = Assert.IsType<AcceptedMechanicsPlanner.ResourceContinuationResult>(
            await InvokeOriginalCaptureAsync(capture, "AdvanceNextResourceExchangeAsync", lease));
        AssertNoConflictFrameErrors(result.Issues);
        var wait = Assert.IsType<AcceptedMechanicsPlanner.PendingSpiritualExchange>(result.Step!.PendingExchange);
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        var resources = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(OriginalCaptureField(capture, "_resources"));
        Assert.True(source.IsCurrentOwner);
        return (capture, wait, source, resources);
    }

    private static async Task<AcceptedMechanicsPlanner.ResourceContinuationResult> ResumeOriginalMissingAsync(
        object capture, FileSystemManager.CanonicalWriteLease lease,
        AcceptedMechanicsPlanner.PendingSpiritualExchange wait) =>
        Assert.IsType<AcceptedMechanicsPlanner.ResourceContinuationResult>(
            await InvokeOriginalCaptureAsync(capture, "ResumeMissingAuditSideAsync", lease, wait));

    private static ResourceTransition[] OriginalContinuationApplied(AcceptedMechanicsPlanner.ResourceExecutionSession resources) =>
        Assert.IsAssignableFrom<IReadOnlyList<ResourceTransition>>(OriginalCaptureField(
            OriginalCaptureField(resources, "_state")!, "appliedTransitions")).ToArray();
}
