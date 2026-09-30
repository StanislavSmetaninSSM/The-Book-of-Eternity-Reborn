using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalSpiritualPrefix_ForgedDiceOrChangedPinnedCandidateCannotAdmit(bool mutateAfterCapture)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var candidate = await ReadSourceMissingCandidateAsync(context);
        candidate["activeConflict"]!["exchangeLog"]![0]!["diceAudit"]!["diceUsed"]![0]!["value"] = 14;
        if (!mutateAfterCapture)
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
        ValidationService.SpiritualOriginalTurnCapture capture;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
            AssertNoConflictFrameErrors(captured.Issues);
            capture = captured.Capture!;
            AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        }
        using (capture)
        {
            if (mutateAfterCapture)
                await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            var before = await context.FileSystem.ReadFileBytesAsync(lease, ResourceMaterializationContract.StatePath);
            var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
            var rejected = await capture.AdvanceNextResourceExchangeAsync(lease);
            Assert.Null(rejected.Step);
            Assert.Contains(rejected.Issues, issue => issue.Severity == IssueSeverity.Error);
            Assert.False(capture.IsCurrentOwner);
            Assert.False(source.IsCurrentOwner);
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
            Assert.Equal(before, await context.FileSystem.ReadFileBytesAsync(lease, ResourceMaterializationContract.StatePath));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task OriginalSpiritualPrefix_ReceiptWaitDefersAllSourceAdmission(int amount)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await SeedOriginalContinuationBoundedEffectAsync(context);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var before = await context.FileSystem.ReadFileBytesAsync(lease, ResourceMaterializationContract.StatePath);
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var first = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(first.Issues);
        var wait = Assert.IsType<AcceptedMechanicsPlanner.PendingSpiritualResourceExchange>(first.Step!.PendingResource);
        Assert.Empty(source.Sources);
        Assert.Empty(source.ClaimedDice);
        var packet = capture.ReadPendingResourceRequest(lease, wait);
        var request = Assert.Single(packet["requests"]!.AsArray())!;
        var receipts = new JsonArray(new JsonObject
        {
            ["requestId"] = request["requestId"]!.GetValue<string>(),
            ["resultKind"] = "resource_delta", ["amount"] = 99,
            ["reason"] = "Rejected outside the owned bound"
        });
        var rejected = await capture.ResumePendingResourceAsync(lease, wait, receipts);
        Assert.Null(rejected.Step);
        Assert.NotEmpty(rejected.Issues);
        Assert.Empty(source.Sources);
        Assert.Empty(source.ClaimedDice);
        Assert.Equal(packet.ToJsonString(), capture.ReadPendingResourceRequest(lease, wait).ToJsonString());
        receipts[0]!["amount"] = amount;
        var accepted = await capture.ResumePendingResourceAsync(lease, wait, receipts);
        AssertNoConflictFrameErrors(accepted.Issues);
        Assert.NotNull(accepted.Step!.OriginalPrefix);
        Assert.Null(accepted.Step.Interval);
        Assert.Empty(source.Sources);
        Assert.Empty(source.ClaimedDice);
        receipts[0]!["amount"] = 99;
        var retained = Assert.IsType<List<(AcceptedMechanicsPlanner.ResourceExecutionStep Step, JsonArray Receipts)>>(
            OriginalCaptureField(capture, "_originalPrefixReceipts"));
        Assert.Equal(amount, Assert.Single(retained).Receipts[0]!["amount"]!.GetValue<int>());
        var exchange = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(exchange.Issues);
        Assert.NotNull(exchange.Step!.Interval);
        Assert.True(source.Owns(Assert.Single(source.Sources)));
        Assert.Equal(new[] { 0, 1 }, source.ClaimedDice);
        var resources = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(OriginalCaptureField(capture, "_resources"));
        var final = resources.Drain();
        Assert.True(final.IsValid, string.Join(Environment.NewLine, final.Issues));
        Assert.Single(final.AppliedTransitions, value => value.Coordinate.ResourceKey == "soul_integrity" &&
            value.BeforeState!.Current == 10m && value.AfterState!.Current == 9m);
        Assert.Equal(before, await context.FileSystem.ReadFileBytesAsync(lease, ResourceMaterializationContract.StatePath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(1, 1)]
    public async Task OriginalSpiritualPrefix_DirectSpendBindsActualPrefixWithoutChangingSignedBaseline(int spend, int gain)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var root = await ReadSourceMissingCandidateAsync(context);
        var prefixValue = 6 - spend + gain;
        root["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!["player"]!["before"] = prefixValue;
        root["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!["player"]!["after"] = prefixValue - 3;
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        var commands = OriginalCaptureDefinitionAndActionPointCommand(spend, gain);
        await context.WriteExactJsonAsync(ResourceMaterializationContract.CommandPath, commands.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        if (prefixValue != 6)
        {
            var legacy = await InvokeOriginalCaptureAsync(context.Validator, "CaptureSpiritualOriginalTurnAsync", lease);
            Assert.Null(OriginalCaptureProperty(legacy, "Capture"));
            Assert.Contains(OriginalCaptureIssues(legacy), issue => issue.Severity == IssueSeverity.Error);
        }
        var result = await InvokeOriginalCaptureAsync(context.Validator, "CaptureSpiritualOriginalTurnWithPrefixAsync", lease);
        AssertNoConflictFrameErrors(OriginalCaptureIssues(result));
        var capture = OriginalCaptureProperty(result, "Capture")!;
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        var original = source.ReadOriginal(ResourceMaterializationContract.StatePath);
        Assert.Empty(source.Sources);
        Assert.Empty(source.ClaimedDice);
        Assert.Null((await source.PrepareContinuationAsync(lease)).Ticket);
        AssertNoConflictFrameErrors(Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(
            await InvokeOriginalCaptureAsync(capture, "BeginResourceExecutionAsync", lease)));
        Assert.Empty(source.Sources);
        var advanced = Assert.IsType<AcceptedMechanicsPlanner.ResourceContinuationResult>(
            await InvokeOriginalCaptureAsync(capture, "AdvanceNextResourceExchangeAsync", lease));
        AssertNoConflictFrameErrors(advanced.Issues);
        Assert.NotNull(advanced.Step!.Interval);
        Assert.True(source.Owns(Assert.Single(source.Sources)));
        Assert.Equal(original, source.ReadOriginal(ResourceMaterializationContract.StatePath));
        var resources = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(OriginalCaptureField(capture, "_resources"));
        var final = resources.Drain();
        Assert.True(final.IsValid, string.Join(Environment.NewLine, final.Issues));
        var player = final.AppliedTransitions.Where(value => value.Coordinate.ResourceOwnerId == "player_soul" &&
            value.Coordinate.ResourceKey == "spiritual_action_points").ToArray();
        Assert.Equal(gain == 0 ? new[] { 6m, (decimal)prefixValue } : new[] { 6m, 6m - spend, prefixValue },
            player.Select(value => value.BeforeState!.Current));
        Assert.Equal(gain == 0 ? new[] { (decimal)prefixValue, prefixValue - 3m } : new[] { 6m - spend, prefixValue, prefixValue - 3m },
            player.Select(value => value.AfterState!.Current));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalSpiritualPrefix_StaleAuditOrFailedPrefixCannotAdmitSource(bool failPrefix)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await context.WriteExactJsonAsync(ResourceMaterializationContract.CommandPath,
            OriginalCaptureDefinitionAndActionPointCommand(failPrefix ? 100m : 1m, 0m).ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var before = await context.FileSystem.ReadFileBytesAsync(lease, ResourceMaterializationContract.StatePath);
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var failed = await capture.AdvanceNextResourceExchangeAsync(lease);
        Assert.Null(failed.Step);
        Assert.Contains(failed.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.False(source.IsCurrentOwner);
        Assert.False(capture.IsCurrentOwner);
        Assert.False(EffectAcceptedTurnPlanAuthority.TryPeekValidated(context.FileSystem, lease, out _));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        Assert.Equal(before, await context.FileSystem.ReadFileBytesAsync(lease, ResourceMaterializationContract.StatePath));
    }

    [Fact]
    public async Task OriginalSpiritualPrefix_ForeignExecutorCannotBindTheSignedOwner()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        var input = Assert.IsType<AcceptedMechanicsInput>(OriginalCaptureField(capture, "_input"));
        var foreignResult = AcceptedMechanicsPlanner.BeginOriginalSpiritualResourceExecution(input);
        AssertNoConflictFrameErrors(foreignResult.Issues);
        using var foreign = foreignResult.Session!;
        var prefix = foreign.AdvanceOriginalPrefix().OriginalPrefix!;
        Assert.True(foreign.Owns(prefix));
        Assert.Contains(await source.BindOriginalPrefixAsync(lease, capture, prefix),
            issue => issue.Code == "spiritual_source_prefix_mismatch");
        Assert.Empty(source.Sources);
        Assert.Empty(source.ClaimedDice);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        Assert.NotNull((await capture.AdvanceNextResourceExchangeAsync(lease)).Step!.Interval);
        var owned = Assert.IsType<AcceptedMechanicsPlanner.OriginalResourcePrefix>(OriginalCaptureField(capture, "_originalPrefix"));
        Assert.Contains(await source.BindOriginalPrefixAsync(lease, capture, owned),
            issue => issue.Code == "spiritual_source_prefix_mismatch");
        var prepared = await source.PrepareContinuationAsync(lease);
        Assert.NotNull(prepared.Ticket);
        var invalid = source.BuildPreparedResourceBatches(lease, prepared.Ticket!, input.PlanningContext!.Owners, input.PlanningContext.State);
        Assert.Contains(invalid.Issues, issue => issue.Code == "spiritual_source_prefix_mismatch");
    }

    [Fact]
    public async Task OriginalSpiritualPrefix_MissingSideKeepsOrdinaryCostAndSignedClaims()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var full = await ReadSourceMissingCandidateAsync(context);
        full["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!["player"]!["before"] = 5;
        full["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!["player"]!["after"] = 2;
        var partial = full.DeepClone();
        partial["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!.AsObject().Remove("opposition");
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, partial.ToJsonString());
        await context.WriteExactJsonAsync(ResourceMaterializationContract.CommandPath,
            OriginalCaptureDefinitionAndActionPointCommand(1m, 0m).ToJsonString());
        ValidationService.SpiritualOriginalTurnCapture capture;
        AcceptedMechanicsPlanner.PendingSpiritualExchange wait;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
            AssertNoConflictFrameErrors(captured.Issues);
            capture = captured.Capture!;
            AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
            var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
            AssertNoConflictFrameErrors(advanced.Issues);
            wait = advanced.Step!.PendingExchange!;
            Assert.NotNull(wait);
        }
        using (capture)
        {
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, full.ToJsonString());
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            var completed = await capture.ResumeMissingAuditSideAsync(lease, wait);
            AssertNoConflictFrameErrors(completed.Issues);
            Assert.NotNull(completed.Step!.Interval);
            var resources = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(OriginalCaptureField(capture, "_resources"));
            var all = resources.Drain();
            Assert.True(all.IsValid, string.Join(Environment.NewLine, all.Issues));
            Assert.Single(all.AppliedTransitions, item => item.EventRef == "turn_42:resource:2");
            Assert.Equal(3, all.AppliedTransitions.Count);
        }
    }
}
