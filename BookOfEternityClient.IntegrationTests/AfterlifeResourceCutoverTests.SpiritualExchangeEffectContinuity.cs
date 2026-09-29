using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task OriginalSpiritualContinuation_ActualEffectGainDefinesNextExchangeBefore(
        bool observedSuffix, bool missingSide, bool staleBefore)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await SeedOriginalPrefixActionPointEffectAsync(context, gain: true, bounded: false);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42, currentRealm: "Chaos Sea",
            preGeneratedDices1d20: [15, 5, 12, 8]);
        await WriteCompleteConflictFrameExchangeAsync(context);
        var firstCandidate = await ReadProjectedSourceContinuationCandidateAsync(context);
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        var complete = await ReadProjectedSourceContinuationCandidateAsync(context);
        var cost = complete["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["player"]!;
        cost["before"] = 4;
        cost["after"] = 1;
        var candidate = complete.DeepClone().AsObject();
        if (staleBefore)
        {
            candidate["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["player"]!["before"] = 3;
            candidate["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["player"]!["after"] = 0;
        }
        if (missingSide)
            candidate["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!.AsObject().Remove("player");
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath,
            (observedSuffix ? candidate : firstCandidate).ToJsonString());
        ValidationService.SpiritualOriginalTurnCapture capture;
        AcceptedMechanicsPlanner.ResourceExecutionSession resources;
        byte[] canonicalBefore;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            canonicalBefore = (await context.FileSystem.ReadFileBytesAsync(lease, ResourceMaterializationContract.StatePath))!;
            var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
            AssertNoConflictFrameErrors(captured.Issues);
            capture = captured.Capture!;
            AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
            var first = await capture.AdvanceNextResourceExchangeAsync(lease);
            AssertNoConflictFrameErrors(first.Issues);
            Assert.Equal(0, first.Step!.Interval!.Ordinal);
            resources = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(OriginalCaptureField(capture, "_resources"));
        }
        using (capture)
        {
            if (!observedSuffix)
                await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
            await using var nextLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            if (!observedSuffix && !missingSide && !staleBefore)
            {
                var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
                var input = Assert.IsType<AcceptedMechanicsInput>(OriginalCaptureField(capture, "_input"));
                var prefix = Assert.IsType<AcceptedMechanicsPlanner.OriginalResourcePrefix>(OriginalCaptureField(source, "_resourcePrefix"));
                var retained = Assert.IsType<SortedDictionary<int, AfterlifeSpiritualConflictResourceOutcome.ExchangeBatch>>(
                    OriginalCaptureField(source, "_resourceBatches"));
                var projectionIssues = new List<ValidationIssue>();
                var frontier = resources.CaptureSpiritualMechanics(capture, source, projectionIssues)!;
                AssertNoConflictFrameErrors(projectionIssues);
                var aliased = complete.DeepClone().AsObject();
                aliased["activeConflict"]!["exchangeLog"]![1]!["exchangeId"] = "EXCHANGE_CONFLICT_FRAME_42";
                foreach (var side in new[] { "player", "opposition" })
                {
                    var zero = aliased["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]![side]!;
                    zero["effectiveCost"] = 0;
                    zero["before"] = side == "player" ? 4 : 3;
                    zero["after"] = side == "player" ? 4 : 3;
                }
                var rejectedAlias = AfterlifeSpiritualConflictResourceOutcome.TryCreate(42,
                    source.ReadMechanicsConflict(capture), aliased, input.PlanningContext!.Owners,
                    prefix.State, frontier, retained.Values.ToArray());
                Assert.False(rejectedAlias.IsValid);
                Assert.Contains(rejectedAlias.Issues, issue => issue.Code == "afterlife_conflict_resource_exchange_identity_ambiguous");
                var legacy = AfterlifeSpiritualConflictResourceOutcome.TryCreate(42,
                    source.ReadMechanicsConflict(capture), firstCandidate, input.PlanningContext.Owners, prefix.State);
                Assert.True(legacy.IsValid);
                Assert.Throws<InvalidOperationException>(() => AfterlifeSpiritualConflictResourceOutcome.TryCreate(42,
                    source.ReadMechanicsConflict(capture), complete, input.PlanningContext.Owners,
                    prefix.State, frontier, legacy.Exchanges));
            }
            var second = await capture.AdvanceNextResourceExchangeAsync(nextLease);
            if (staleBefore)
            {
                Assert.Null(second.Step);
                Assert.Contains(second.Issues, issue => issue.Code == "afterlife_conflict_action_cost_sequence_mismatch");
                Assert.Equal(canonicalBefore, await context.FileSystem.ReadFileBytesAsync(nextLease, ResourceMaterializationContract.StatePath));
                return;
            }
            AssertNoConflictFrameErrors(second.Issues);
            if (missingSide)
            {
                var wait = Assert.IsType<AcceptedMechanicsPlanner.PendingSpiritualExchange>(second.Step!.PendingExchange);
                await nextLease.DisposeAsync();
                await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, complete.ToJsonString());
                await using var resumedLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
                var resumed = await capture.ResumeMissingAuditSideAsync(resumedLease, wait);
                AssertNoConflictFrameErrors(resumed.Issues);
                Assert.Equal(1, resumed.Step!.Interval!.Ordinal);
                Assert.Equal(canonicalBefore, await context.FileSystem.ReadFileBytesAsync(resumedLease, ResourceMaterializationContract.StatePath));
            }
            else
            {
                Assert.Equal(1, second.Step!.Interval!.Ordinal);
                Assert.Equal(canonicalBefore, await context.FileSystem.ReadFileBytesAsync(nextLease, ResourceMaterializationContract.StatePath));
            }
            var transitions = OriginalContinuationApplied(resources).Where(transition =>
                transition.Coordinate.ResourceOwnerId == "player_soul" &&
                transition.Coordinate.ResourceKey == "spiritual_action_points").ToArray();
            Assert.Equal(new[] { 6m, 3m, 4m }, transitions.Select(transition => transition.BeforeState!.Current));
            Assert.Equal(new[] { 3m, 4m, 1m }, transitions.Select(transition => transition.AfterState!.Current));
        }
    }
}
