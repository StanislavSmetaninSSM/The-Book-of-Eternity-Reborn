using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Preserves a saved wound choice while remaining ordinary exchanges require bounded cost corrections.
    /// </summary>
    /// <param name="recoveryBurden">
    /// True burdens both opposition recoveries; false burdens a guard before an unburdened recovery.
    /// </param>
    /// <param name="ambiguousRecovery">
    /// Whether the earlier recovery has two legal post-payment results and must not be chosen diagnostically.
    /// </param>
    /// <param name="stalePlayerAudit">
    /// Whether the guard cost initially masks stale player balances in the same exchange.
    /// </param>
    /// <param name="staleFinalPlayerAudit">
    /// Whether a cost error masks the final exchange's stale player balances.
    /// </param>
    /// <param name="singleRemaining">
    /// Whether the selected wound leaves only the second exchange to finish.
    /// </param>
    /// <returns>
    /// A task completing after read-only context checks, one saved continuation and exact cold-plan replay.
    /// </returns>
    [Theory]
    [InlineData(false, false, false, false, false)]
    [InlineData(true, false, false, false, false)]
    [InlineData(true, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(true, false, false, true, false)]
    [InlineData(false, false, true, false, true)]
    public async Task OriginalSpiritualC2DependentContext_ClosesOrdinaryCostsAcrossTwoExchanges(
        bool recoveryBurden, bool ambiguousRecovery, bool stalePlayerAudit,
        bool staleFinalPlayerAudit, bool singleRemaining)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        var candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = candidate["activeConflict"]!;
        var second = active["exchangeLog"]![1]!.AsObject();
        second["operationType"] = "recover_spiritual_power";
        second["outcome"] = "no_effect";
        second["after"] = second["before"]!.DeepClone();
        second.Remove("diceAudit");
        second["matchupAudit"]!["playerOperation"] = "recover_spiritual_power";
        second["matchupAudit"]!["oppositionOperation"] = recoveryBurden ? "recover_spiritual_power" : "guard";
        second["matchupAudit"]!["primaryResolutionLane"] = "recover_spiritual_power";
        second["matchupAudit"]!["riskProfile"] = "recovery_timing";
        second["actionCostAudit"] = new JsonObject
        {
            ["player"] = CostAudit("recover_spiritual_power", 0, 3, 3),
            ["opposition"] = recoveryBurden
                ? CostAudit("recover_spiritual_power", 0, 3, 6)
                : CostAudit("guard", 2, 3, 1)
        };
        var third = second.DeepClone().AsObject();
        third["exchangeId"] = "exchange_source_third";
        third["matchupAudit"]!["oppositionOperation"] = "recover_spiritual_power";
        third["actionCostAudit"]!["opposition"] = CostAudit("recover_spiritual_power", 0,
            recoveryBurden ? 6 : 1, recoveryBurden ? 6 : 4);
        active["exchangeLog"]!.AsArray().Add(third);
        if (stalePlayerAudit)
            second["actionCostAudit"]!["player"] = CostAudit("recover_spiritual_power", 0, 4, 4);
        if (staleFinalPlayerAudit)
            third["actionCostAudit"]!["player"] = CostAudit("recover_spiritual_power", 0, 4, 4);
        if (singleRemaining)
            active["exchangeLog"]!.AsArray().RemoveAt(2);
        if (ambiguousRecovery)
        {
            second["operationType"] = "pressure";
            second["matchupAudit"]!["playerOperation"] = "pressure";
            second["matchupAudit"]!["primaryResolutionLane"] = "pressure";
            second["matchupAudit"]!["riskProfile"] = "offensive_pressure";
            second["actionCostAudit"]!["player"] = CostAudit("pressure", 3, 3, 0);
            second["actionCostAudit"]!["opposition"]!["after"] = 4;
            third["actionCostAudit"]!["player"] = CostAudit("recover_spiritual_power", 0, 0, 0);
            third["actionCostAudit"]!["opposition"]!["before"] = 4;
        }
        active["oppositionSideStrain"] = second["after"]!["oppositionSideStrain"]!.DeepClone();
        var frozenExchanges = active["exchangeLog"]!.DeepClone();
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
        await WriteOriginalIntakeDraftAsync(context);
        await context.WriteExactBytesAsync(ProjectionNarrativePath,
            Encoding.UTF8.GetBytes("{\"response\":\"Духовный обмен завершён.\"}"));
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var first = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(first.Issues);
        var committed = await capture.CommitC2FirstTransportAsync(lease, first.Step!.Interval!);
        AssertNoConflictFrameErrors(committed.Issues);
        Assert.Equal("committed", committed.Disposition);
        capture.Dispose();

        var adapter = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var offered = await adapter.OpenC2PrivateSessionAsync(lease);
        AssertNoConflictFrameErrors(offered.Issues);
        Assert.Equal("offer", offered.Disposition);
        using var offeredSession = offered.Session!;
        var selected = await offeredSession.SubmitDecisionAsync(lease,
            OriginalSpiritualWoundDecision(offeredSession.Offer!.OpportunityRef,
                "spiritual_action_cost_burden", recoveryBurden ? "recover_spiritual_power" : "guard"),
            "Чужое давление надломило волю хранителя.");
        Assert.True(selected.Disposition == "dependent_continuation",
            selected.Disposition + Environment.NewLine + string.Join(Environment.NewLine, selected.Issues));
        selected.Session?.Dispose();
        offeredSession.Dispose();
        var retained = new Dictionary<string, byte[]?>();
        foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                     SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath,
                     ResourceMaterializationContract.StatePath, "input/turn_request.json" })
        {
            retained[path] = await context.FileSystem.ReadFileBytesAsync(lease, path);
            Assert.NotNull(retained[path]);
        }
        Assert.IsType<JsonObject>(JsonNode.Parse(retained[SpiritualWoundCaptureCheckpointState.StatePath]!)!
            ["checkpoint"]!["pendingSubmission"]);
        var expectedFields = new List<string>
        {
            "/activeConflict/exchangeLog/1/actionCostAudit/opposition/after",
            "/activeConflict/exchangeLog/1/actionCostAudit/opposition/effectiveCost",
            "/activeConflict/exchangeLog/2/actionCostAudit/opposition/after",
            "/activeConflict/exchangeLog/2/actionCostAudit/opposition/before"
        };
        if (recoveryBurden)
            expectedFields.Add("/activeConflict/exchangeLog/2/actionCostAudit/opposition/effectiveCost");
        if (stalePlayerAudit)
        {
            expectedFields.Add("/activeConflict/exchangeLog/1/actionCostAudit/player/after");
            expectedFields.Add("/activeConflict/exchangeLog/1/actionCostAudit/player/before");
        }
        if (staleFinalPlayerAudit)
        {
            expectedFields.Add("/activeConflict/exchangeLog/2/actionCostAudit/player/after");
            expectedFields.Add("/activeConflict/exchangeLog/2/actionCostAudit/player/before");
        }
        if (singleRemaining)
            expectedFields.RemoveAll(field => field.StartsWith("/activeConflict/exchangeLog/2/", StringComparison.Ordinal));
        expectedFields.Sort(StringComparer.Ordinal);
        ValidationService.SpiritualC2DependentContext? initialContext = null;
        ValidationService.SpiritualC2PrivateOpenResult? completed = null;
        var finalCorrection = singleRemaining ? 1 : 2;
        for (var correction = 0; correction <= finalCorrection; correction++)
        {
            if (correction == 1)
            {
                second["actionCostAudit"]!["opposition"]!["effectiveCost"] = recoveryBurden ? 1 : 3;
                second["actionCostAudit"]!["opposition"]!["after"] = recoveryBurden ? 5 : 0;
                if (stalePlayerAudit)
                {
                    second["actionCostAudit"]!["player"]!["before"] = 3;
                    second["actionCostAudit"]!["player"]!["after"] = 3;
                }
            }
            else if (correction == 2)
            {
                third["actionCostAudit"]!["opposition"]!["before"] = recoveryBurden ? 5 : 0;
                third["actionCostAudit"]!["opposition"]!["after"] = recoveryBurden ? 6 : 3;
                if (recoveryBurden)
                    third["actionCostAudit"]!["opposition"]!["effectiveCost"] = 1;
                if (staleFinalPlayerAudit)
                {
                    third["actionCostAudit"]!["player"]!["before"] = 3;
                    third["actionCostAudit"]!["player"]!["after"] = 3;
                }
            }
            if (correction != 0)
                await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath,
                    Encoding.UTF8.GetBytes(candidate.ToJsonString()));
            var draftBeforeRead = await context.FileSystem.ReadFileBytesAsync(lease,
                AfterlifeSpiritualConflictState.StatePath);
            var cold = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
            var opened = await cold.OpenC2PrivateSessionAsync(lease);
            Assert.Equal("dependent_continuation", opened.Disposition);
            using var session = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
            Assert.Null(session.Offer);
            var observedContext = await session.ReadDependentContextAsync(lease);
            if (ambiguousRecovery)
            {
                Assert.Equal(0, correction);
                Assert.Contains(opened.Issues, issue => issue.Code == "afterlife_conflict_opposition_action_cost_mismatch");
                Assert.Null(observedContext);
                foreach (var pair in retained)
                    Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
                Assert.Equal(draftBeforeRead, await context.FileSystem.ReadFileBytesAsync(lease,
                    AfterlifeSpiritualConflictState.StatePath));
                return;
            }
            var current = Assert.IsType<ValidationService.SpiritualC2DependentContext>(observedContext);
            Assert.Equal(expectedFields, current.DependentDraftFields.Select(field => field.JsonPointer));
            Assert.All(current.DependentDraftFields,
                field => Assert.Equal(AfterlifeSpiritualConflictState.StatePath, field.Path));
            if (initialContext is null)
            {
                initialContext = current;
                Assert.Contains(current.BaselineIssues, issue => issue.FilePath.Contains("exchangeLog[1]", StringComparison.Ordinal));
                if (!singleRemaining)
                    Assert.Contains(current.BaselineIssues, issue => issue.FilePath.Contains("exchangeLog[2]", StringComparison.Ordinal));
            }
            else
            {
                Assert.Equal(initialContext.ContinuationId, current.ContinuationId);
                Assert.Equal(initialContext.BaselineIssues.Select(issue => (issue.Code, issue.FilePath)),
                    current.BaselineIssues.Select(issue => (issue.Code, issue.FilePath)));
            }
            if (correction == 0)
                Assert.NotEmpty(current.CurrentIssues);
            else if (correction < finalCorrection)
            {
                Assert.Contains(current.CurrentIssues, issue =>
                    issue.FilePath.Contains("exchangeLog[2]", StringComparison.Ordinal) &&
                    issue.Code == (recoveryBurden ? "afterlife_conflict_opposition_action_cost_mismatch" :
                        "afterlife_conflict_action_cost_sequence_mismatch"));
                Assert.DoesNotContain(current.CurrentIssues,
                    issue => issue.FilePath.Contains("exchangeLog[1]", StringComparison.Ordinal));
            }
            else
                Assert.Empty(current.CurrentIssues);
            foreach (var pair in retained)
                Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
            Assert.Equal(draftBeforeRead, await context.FileSystem.ReadFileBytesAsync(lease,
                AfterlifeSpiritualConflictState.StatePath));
            if (correction == finalCorrection)
                completed = await session.ResumeDependentContinuationAsync(lease);
        }

        Assert.NotNull(completed);
        AssertNoConflictFrameErrors(completed.Issues);
        Assert.Equal("completed_unpublished", completed.Disposition);
        using var terminal = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(completed.Session);
        Assert.Null(terminal.Offer);
        var checkpoint = JsonNode.Parse((await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath))!)!["checkpoint"]!;
        Assert.Equal(1, checkpoint["committedAdvance"]!.GetValue<int>());
        Assert.Null(checkpoint["pendingSubmission"]);
        var reduced = await terminal.ReduceCompletedDecisionsAsync(lease);
        Assert.True(reduced.Success, string.Join(Environment.NewLine, reduced.Issues));
        var ordinary = reduced.Reduction!;
        Assert.Single(ordinary.LiveWoundCompletion!.Insertions);
        Assert.Single(reduced.ReceiptAfterImage!["decisions"]!.AsArray());
        var paid = ordinary.Resources.AppliedTransitions.Where(value =>
            value.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeConflictSide &&
            value.OriginId is "exchange_source_second" or "exchange_source_third").ToArray();
        var expectedPayments = recoveryBurden
            ? new[] { ("exchange_source_second", ResourceTransitionOperation.Spend, 1m, 3m, 2m),
                ("exchange_source_second", ResourceTransitionOperation.Gain, 3m, 2m, 5m),
                ("exchange_source_third", ResourceTransitionOperation.Spend, 1m, 5m, 4m),
                ("exchange_source_third", ResourceTransitionOperation.Gain, 2m, 4m, 6m) }
            : new[] { ("exchange_source_second", ResourceTransitionOperation.Spend, 3m, 3m, 0m),
                ("exchange_source_third", ResourceTransitionOperation.Gain, 3m, 0m, 3m) };
        if (singleRemaining)
            expectedPayments = expectedPayments.Where(payment => payment.Item1 == "exchange_source_second").ToArray();
        Assert.Equal(expectedPayments, paid.Select(value => (value.OriginId, value.Operation,
            value.AppliedAmount, value.BeforeState!.Current, value.AfterState!.Current)));
        var planned = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
            AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(ordinary, ordinary.Resources));
        Assert.Empty(planned.Issues);
        Assert.Equal(recoveryBurden ? 6m : singleRemaining ? 0m : 3m,
            ResolvePlannedActionPoints(planned.Plan!, ResourceOwnerKind.AfterlifeConflictSide).Current);
        Assert.Equal(3m, ResolvePlannedActionPoints(planned.Plan!, ResourceOwnerKind.AfterlifeActor).Current);
        var finalConflict = JsonNode.Parse((await context.FileSystem.ReadFileBytesAsync(lease,
            AfterlifeSpiritualConflictState.StatePath))!)!;
        var finalExchanges = finalConflict["activeConflict"]!["exchangeLog"]!.DeepClone();
        if (stalePlayerAudit)
            foreach (var field in new[] { "before", "after" })
            {
                frozenExchanges[1]!["actionCostAudit"]!["player"]!.AsObject().Remove(field);
                finalExchanges[1]!["actionCostAudit"]!["player"]!.AsObject().Remove(field);
            }
        if (staleFinalPlayerAudit)
            foreach (var field in new[] { "before", "after" })
            {
                frozenExchanges[2]!["actionCostAudit"]!["player"]!.AsObject().Remove(field);
                finalExchanges[2]!["actionCostAudit"]!["player"]!.AsObject().Remove(field);
            }
        foreach (var index in singleRemaining ? new[] { 1 } : new[] { 1, 2 })
            foreach (var field in new[] { "effectiveCost", "before", "after" })
            {
                frozenExchanges[index]!["actionCostAudit"]!["opposition"]!.AsObject().Remove(field);
                finalExchanges[index]!["actionCostAudit"]!["opposition"]!.AsObject().Remove(field);
            }
        Assert.True(JsonNode.DeepEquals(frozenExchanges, finalExchanges));
        foreach (var path in new[] { ResourceMaterializationContract.StatePath,
                     AcceptedMechanicsPlan.WoundCommandPath, "input/turn_request.json" })
            Assert.Equal(retained[path], await context.FileSystem.ReadFileBytesAsync(lease, path));
        terminal.Dispose();
        var replayOpen = await new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance)
            .OpenC2PrivateSessionAsync(lease);
        AssertNoConflictFrameErrors(replayOpen.Issues);
        Assert.Equal("completed_unpublished", replayOpen.Disposition);
        using var replaySession = replayOpen.Session!;
        var replay = await replaySession.ReduceCompletedDecisionsAsync(lease);
        Assert.True(replay.Success, string.Join(Environment.NewLine, replay.Issues));
        var replayPlan = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
            AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(replay.Reduction!, replay.Reduction!.Resources));
        Assert.Empty(replayPlan.Issues);
        Assert.True(JsonNode.DeepEquals(planned.Plan!.StateAfterImage, replayPlan.Plan!.StateAfterImage));
        Assert.True(JsonNode.DeepEquals(planned.Plan.HistoryAfterImage, replayPlan.Plan.HistoryAfterImage));
        Assert.True(JsonNode.DeepEquals(reduced.ReceiptAfterImage, replay.ReceiptAfterImage));
    }
}
