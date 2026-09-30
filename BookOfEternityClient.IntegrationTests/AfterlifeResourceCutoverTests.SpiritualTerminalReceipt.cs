using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Pays the last signed exchange before retiring its opposition owner and seals one replayable closure.
    /// </summary>
    /// <param name="materialize">
    /// Whether the real opposition opportunity creates a persistent guardian wound.
    /// </param>
    /// <param name="reaction">
    /// Whether a signed finite-use effect reacts to the player's terminal Spend.
    /// </param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task OriginalSpiritualC3Terminal_PaysExchangeBeforeRetirementAndSealsClosure(
        bool materialize, bool reaction)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        if (reaction)
        {
            await SeedOriginalPrefixActionPointEffectAsync(context, gain: true, bounded: false);
            await context.CaptureValidatedPendingSnapshotAsync(turn: 42, currentRealm: "Chaos Sea",
                preGeneratedDices1d20: [15, 5, 12, 8]);
        }
        await CommitInitialC2PairAsync(context, ResolveTerminalFixture);

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var validator = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var opened = await validator.OpenC2PrivateSessionAsync(lease);
        AssertNoConflictFrameErrors(opened.Issues);
        Assert.Equal("offer", opened.Disposition);
        using var offer = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        var completed = await offer.SubmitDecisionAsync(lease,
            materialize ? OriginalSpiritualWoundDecision(offer.Offer!.OpportunityRef) :
            JsonSerializer.SerializeToElement(new
            {
                opportunityRef = offer.Offer!.OpportunityRef, decision = "none"
            }), materialize ? "Чужое давление надломило волю хранителя." : null);
        AssertNoConflictFrameErrors(completed.Issues);
        Assert.Equal("completed_unpublished", completed.Disposition);
        using var terminal = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(completed.Session);
        var physical = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in new[]
        {
            AfterlifeSpiritualConflictState.StatePath, AfterlifeEntityProfileState.StatePath,
            ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
            SpiritualWoundOpportunityReceiptState.StatePath, WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath, EffectAcceptedTurnPlan.IdentityIndexPath,
            SpiritualWoundDecisionPendingState.StatePath, SpiritualWoundCaptureCheckpointState.StatePath
        })
            physical.Add(path, await context.FileSystem.ReadFileBytesAsync(lease, path));

        var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(
            OriginalCaptureField(terminal, "_capture"));
        var allocationsBefore = capture.ReadAllocationJournal(lease).ToJsonString();
        var cursorBefore = capture.ReadAllocationCursor(lease);
        var reduced = await terminal.ReduceCompletedDecisionsAsync(lease);
        Assert.True(reduced.Success, string.Join(Environment.NewLine, reduced.Issues));
        var ordinary = reduced.Reduction!;
        var costs = ordinary.Resources.AppliedTransitions.Where(value =>
            value.OriginId == "exchange_conflict_frame_42" &&
            value.Operation == ResourceTransitionOperation.Spend).ToArray();
        Assert.Equal(2, costs.Length);
        Assert.All(costs, value => Assert.Equal(3m, value.AfterState!.Current));
        var retirement = Assert.Single(ordinary.Resources.AppliedTransitions,
            value => value.Operation == ResourceTransitionOperation.Retire &&
                     value.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeConflictSide);
        Assert.All(costs, value => Assert.True(value.ExecutionSequence < retirement.ExecutionSequence));
        var receipt = reduced.ReceiptAfterImage!;
        var instance = Assert.Single(receipt["instances"]!.AsArray())!;
        var closure = Assert.Single(receipt["closures"]!.AsArray())!;
        Assert.Equal(instance["instanceId"]!.GetValue<string>(), closure["instanceId"]!.GetValue<string>());
        Assert.Equal(42, closure["terminalTurn"]!.GetValue<int>());
        Assert.Single(receipt["sources"]!.AsArray());
        Assert.Equal(materialize ? "materialize" : "none",
            Assert.Single(receipt["decisions"]!.AsArray())!["decision"]!.GetValue<string>());
        var planned = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
            AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(ordinary, ordinary.Resources));
        Assert.Empty(planned.Issues);
        Assert.True(JsonNode.DeepEquals(receipt,
            planned.Plan!.OwnerCompanionAfterImages[SpiritualWoundOpportunityReceiptState.StatePath]));
        var finalConflict = planned.Plan.EffectCarrierAfterImages.TryGetValue(
            AfterlifeSpiritualConflictState.StatePath, out var effectConflict) ? effectConflict :
            planned.Plan.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath];
        Assert.Null(finalConflict["activeConflict"]);
        if (materialize)
        {
            var insertion = Assert.Single(ordinary.LiveWoundCompletion!.Insertions);
            var profiles = planned.Plan.WoundCarrierAfterImages[AfterlifeEntityProfileState.StatePath];
            var guardian = Assert.Single(profiles["profiles"]!.AsArray(),
                value => value!["actorId"]!.GetValue<string>() == "guardian_frame")!;
            Assert.Contains(insertion.Wound.WoundId, guardian["activeWounds"]!.ToJsonString());
            var effect = Assert.Single(guardian["activeEffects"]!.AsArray(),
                value => value!["source"]!["sourceId"]!.GetValue<string>() == insertion.Wound.WoundId)!;
            var effectId = effect["effectId"]!.GetValue<string>();
            Assert.Contains(planned.Plan.EffectIdentityAfterImage["entries"]!.AsArray(),
                value => value!["effectId"]!.GetValue<string>() == effectId &&
                         value["state"]!.GetValue<string>() == "active");
            Assert.True(JsonNode.DeepEquals(insertion.ReducedState.Identity, planned.Plan.WoundIdentityAfterImage));
            Assert.True(JsonNode.DeepEquals(insertion.ReducedState.History, planned.Plan.WoundHistoryAfterImage));
        }
        if (reaction)
        {
            var gain = Assert.Single(ordinary.Resources.AppliedTransitions,
                value => value.Operation == ResourceTransitionOperation.Gain);
            Assert.Equal(4m, gain.AfterState!.Current);
            Assert.True(costs.Single(value => value.Coordinate.ResourceOwnerId == "player_soul").ExecutionSequence < gain.ExecutionSequence);
            Assert.True(gain.ExecutionSequence < retirement.ExecutionSequence);
            Assert.NotEmpty(ordinary.Resources.EffectBoundaryTranscript!.AcceptedActivations);
        }

        var resources = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(OriginalCaptureField(capture, "_resources"));
        var preparation = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture.PreparedTerminal>(resources.TerminalPreparation);
        var originalContext = preparation.OriginalInput.PlanningContext!;
        using (var foreign = new AcceptedMechanicsPlanner.ResourceExecutionSession(
                   new AcceptedMechanicsResourceInput(42, originalContext.Definitions, originalContext.State,
                       originalContext.History, originalContext.Sources, [], []),
                   new AcceptedMechanicsIdentityFactory(), live: true, terminal: preparation))
        {
            var rejected = foreign.Drain();
            Assert.False(rejected.IsValid);
            Assert.Contains(rejected.Issues, issue => issue.Code == "spiritual_terminal_owner_mismatch");
            Assert.Empty(rejected.AppliedTransitions);
        }
        var clonedInput = preparation.OriginalInput.WithPlanningContext(originalContext);
        var foreignInput = AcceptedMechanicsPlanner.BeginOriginalSpiritualResourceExecution(clonedInput, preparation);
        Assert.Null(foreignInput.Session);
        Assert.Contains(foreignInput.Issues, issue => issue.Code == "spiritual_terminal_input_mismatch");
        Assert.False(preparation.FinalOwners.Resolve(new ResourceOwnerRequest(preparation.Owner.Realm,
            preparation.Owner.OwnerKind, "spiritual_action_points", preparation.Owner.ResourceOwnerId, null)).Success);
        Assert.Single(preparation.ExecutionOwners.Entries.Keys.Except(preparation.FinalOwners.Entries.Keys));
        var raw = await capture.CompleteOrdinaryReductionAsync(lease);
        Assert.True(raw.Success);
        Assert.Same(raw.Reduction, (await capture.CompleteOrdinaryReductionAsync(lease)).Reduction);
        Assert.Equal(allocationsBefore, capture.ReadAllocationJournal(lease).ToJsonString());
        Assert.Equal(cursorBefore, capture.ReadAllocationCursor(lease));
        Assert.False(raw.Reduction!.HasLiveDecisionReceipt);
        var bypass = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
            AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(raw.Reduction, raw.Reduction.Resources));
        Assert.Null(bypass.Plan);
        Assert.Contains(bypass.Issues, issue => issue.Code == "spiritual_live_wound_receipt_join_required");

        terminal.Dispose();
        var reopened = await new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance)
            .OpenC2PrivateSessionAsync(lease);
        AssertNoConflictFrameErrors(reopened.Issues);
        Assert.Equal("completed_unpublished", reopened.Disposition);
        using var cold = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(reopened.Session);
        var replay = await cold.ReduceCompletedDecisionsAsync(lease);
        Assert.True(replay.Success, string.Join(Environment.NewLine, replay.Issues));
        Assert.True(JsonNode.DeepEquals(receipt, replay.ReceiptAfterImage));
        Assert.Equal(ordinary.Resources.StateAfterImage!.ToCanonicalJson(),
            replay.Reduction!.Resources.StateAfterImage!.ToCanonicalJson());
        Assert.Equal(ordinary.Resources.HistoryAfterImage!.ToCanonicalJson(),
            replay.Reduction.Resources.HistoryAfterImage!.ToCanonicalJson());
        var coldPlan = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
            AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(replay.Reduction, replay.Reduction.Resources));
        Assert.Empty(coldPlan.Issues);
        Assert.True(JsonNode.DeepEquals(planned.Plan.EffectIdentityAfterImage, coldPlan.Plan!.EffectIdentityAfterImage),
            "warm=" + planned.Plan.EffectIdentityAfterImage + " cold=" + coldPlan.Plan.EffectIdentityAfterImage);
        Assert.Equal(planned.Plan.PreparedPlanFingerprint, coldPlan.Plan.PreparedPlanFingerprint);
        if (materialize)
        {
            Assert.True(JsonNode.DeepEquals(planned.Plan.WoundCarrierAfterImages[AfterlifeEntityProfileState.StatePath],
                coldPlan.Plan.WoundCarrierAfterImages[AfterlifeEntityProfileState.StatePath]));
            Assert.True(JsonNode.DeepEquals(planned.Plan.WoundHistoryAfterImage, coldPlan.Plan.WoundHistoryAfterImage));
            Assert.True(JsonNode.DeepEquals(planned.Plan.WoundIdentityAfterImage, coldPlan.Plan.WoundIdentityAfterImage));
        }
        var coldCapture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(OriginalCaptureField(cold, "_capture"));
        var rawCold = await coldCapture.CompleteOrdinaryReductionAsync(lease);
        var signedCold = coldCapture.ReadVerifiedSignedC1Origin(lease);
        Assert.Throws<InvalidOperationException>(() => rawCold.Reduction!.WithOwnerCompanionAfterImage(
            SpiritualWoundOpportunityReceiptState.StatePath, signedCold.Receipt, receipt, reduced.ReceiptProof));
        var duplicate = replay.ReceiptAfterImage!.DeepClone().AsObject();
        duplicate["closures"]!.AsArray().Add(duplicate["closures"]![0]!.DeepClone());
        Assert.Throws<InvalidOperationException>(() => rawCold.Reduction!.WithOwnerCompanionAfterImage(
            SpiritualWoundOpportunityReceiptState.StatePath, signedCold.Receipt, duplicate, replay.ReceiptProof));
        Assert.Throws<InvalidOperationException>(() => rawCold.Reduction!.WithOwnerCompanionAfterImage(
            SpiritualWoundOpportunityReceiptState.StatePath, signedCold.Receipt, replay.ReceiptAfterImage!, null));
        var rejoined = rawCold.Reduction!.WithOwnerCompanionAfterImage(
            SpiritualWoundOpportunityReceiptState.StatePath, signedCold.Receipt, replay.ReceiptAfterImage!, replay.ReceiptProof);
        Assert.True(rejoined.HasLiveDecisionReceipt);
        foreach (var pair in physical)
            Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
    }

    /// <summary>
    /// Resolves the genuine fixture's final exchange through the existing terminal update contract.
    /// </summary>
    /// <param name="root">
    /// Projected original draft containing its one new exchange.
    /// </param>
    private static void ResolveTerminalFixture(JsonObject root)
    {
        var exchange = root["activeConflict"]!["exchangeLog"]!.AsArray().Last()!.DeepClone();
        var resolution = new JsonObject
        {
            ["conflictId"] = "conflict_resource_cost", ["operationType"] = "pressure",
            ["resolvedAtTurn"] = 42, ["resolvedAtUtc"] = "2026-01-01T00:00:00Z",
            ["guardianId"] = "guardian_frame", ["playerOutcome"] = "won",
            ["diceAudit"] = exchange!["diceAudit"]!.DeepClone(), ["terminalExchange"] = exchange,
            ["summary"] = "Душа завершила духовное противостояние."
        };
        var resolved = AfterlifeSpiritualConflictState.ApplyUpdate(root,
            new JsonObject { ["mode"] = "resolve", ["resolution"] = resolution });
        Assert.Null(resolved["activeConflict"]);
        root.Clear();
        foreach (var pair in resolved)
            root[pair.Key] = pair.Value?.DeepClone();
    }

    /// <summary>
    /// Reuses a display identifier after ordinary recent-history pruning while retaining the real earlier closure.
    /// </summary>
    /// <param name="mutation">
    /// Optional deletion or reorder of a retained historical sibling; <see langword="null"/>
    /// exercises the exact allowed oldest-row pruning.
    /// </param>
    [Theory]
    [InlineData(null)]
    [InlineData("delete")]
    [InlineData("reorder")]
    public async Task OriginalSpiritualC3Terminal_ReusedDisplayCreatesDistinctInstanceAndPreservesHistory(string? mutation)
    {
        JsonObject firstReceipt;
        JsonObject firstConflict;
        await using (var first = await CreateCompleteConflictFrameContextAsync(
                         seedOriginalInputs: SeedOriginalIntakeBaselinesAsync))
        {
            await CommitInitialC2PairAsync(first, ResolveTerminalFixture);
            await using var lease = await first.FileSystem.AcquireCanonicalWriteLeaseAsync();
            var reduced = await ReduceTerminalNoneAsync(first, lease);
            firstReceipt = reduced.ReceiptAfterImage!.DeepClone().AsObject();
            firstConflict = reduced.Reduction!.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]
                .DeepClone().AsObject();
        }

        // The new signed baseline admits a later existing conflict. Its receipt is actual C3 output,
        // not a manufactured closure or permission to execute a same-turn start contour.
        await using var next = await CreateCompleteConflictFrameContextAsync(seedOriginalInputs: async context =>
        {
            await SeedOriginalIntakeBaselinesAsync(context);
            await context.WriteExactJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath, firstReceipt.ToJsonString());
            var fresh = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
            var started = AfterlifeSpiritualConflictState.ApplyUpdate(firstConflict, new JsonObject
            {
                ["mode"] = "start", ["conflictState"] = fresh["activeConflict"]!.DeepClone()
            });
            Assert.Null(started["lastInvalidUpdate"]);
            for (var index = 1; index < 20; index++)
            {
                var historical = firstConflict["recentConflicts"]![0]!.DeepClone();
                historical!["conflictId"] = "archived_terminal_" + index;
                historical["terminalExchange"]!["exchangeId"] = "archived_exchange_" + index;
                started["recentConflicts"]!.AsArray().Add(historical);
            }
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, started.ToJsonString());
        });
        await next.CaptureValidatedPendingSnapshotAsync(turn: 43, currentRealm: "Chaos Sea",
            preGeneratedDices1d20: [15, 5, 12, 8], sessionId: "terminal_reused_session_43",
            requestId: "terminal_reused_request_43");
        await WriteCompleteConflictFrameExchangeAsync(next);
        var root = await ReadProjectedSourceContinuationCandidateAsync(next);
        {
            var exchange = root["activeConflict"]!["exchangeLog"]!.AsArray().Single()!;
            exchange["exchangeId"] = "exchange_terminal_reuse_43";
            exchange["turnNumber"] = 43;
            ResolveTerminalFixture(root);
            root["recentConflicts"]!.AsArray().Last()!["resolvedAtTurn"] = 43;
        }
        Assert.Equal(20, root["recentConflicts"]!.AsArray().Count);
        Assert.DoesNotContain(root["recentConflicts"]!.AsArray(), value =>
            value!["terminalExchange"]!["exchangeId"]!.GetValue<string>() == "exchange_conflict_frame_42");
        var recent = root["recentConflicts"]!.AsArray();
        if (mutation == "delete")
            recent.RemoveAt(0);
        if (mutation == "reorder")
        {
            var first = recent[0]!.DeepClone();
            recent[0] = recent[1]!.DeepClone();
            recent[1] = first;
        }
        await next.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await next.WriteExactBytesAsync(ProjectionNarrativePath,
            System.Text.Encoding.UTF8.GetBytes("{\"response\":\"Новый конфликт завершён.\"}"));
        AssertNoConflictFrameErrors(await next.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await next.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var nextLease = await next.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await next.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(nextLease);
        AssertNoConflictFrameErrors(captured.Issues);
        using (var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(captured.Capture))
        {
            var beginIssues = await capture.BeginResourceExecutionAsync(nextLease);
            if (mutation != null)
            {
                var rejected = beginIssues.Count != 0 ? beginIssues :
                    (await capture.AdvanceNextResourceExchangeAsync(nextLease)).Issues;
                Assert.Contains(rejected, issue => issue.Severity == IssueSeverity.Error);
                Assert.Null(await next.FileSystem.ReadFileBytesAsync(nextLease, SpiritualWoundCaptureCheckpointState.StatePath));
                Assert.Equal(firstReceipt.ToJsonString(),
                    (await next.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!.ToJsonString());
                return;
            }
            AssertNoConflictFrameErrors(beginIssues);
            var step = await capture.AdvanceNextResourceExchangeAsync(nextLease);
            AssertNoConflictFrameErrors(step.Issues);
            var committed = await capture.CommitC2FirstTransportAsync(nextLease, step.Step!.Interval!);
            AssertNoConflictFrameErrors(committed.Issues);
            Assert.Equal("committed", committed.Disposition);
        }
        var later = await ReduceTerminalNoneAsync(next, nextLease);
        var receipt = later.ReceiptAfterImage!;
        Assert.Equal(2, receipt["instances"]!.AsArray().Count);
        Assert.Equal(2, receipt["closures"]!.AsArray().Count);
        Assert.Equal(receipt["instances"]![0]!["displayConflictId"]!.GetValue<string>(),
            receipt["instances"]![1]!["displayConflictId"]!.GetValue<string>());
        Assert.NotEqual(receipt["instances"]![0]!["instanceId"]!.GetValue<string>(),
            receipt["instances"]![1]!["instanceId"]!.GetValue<string>());
        foreach (var collection in new[] { "instances", "closures", "sources", "decisions" })
            Assert.True(JsonNode.DeepEquals(firstReceipt[collection]![0], receipt[collection]![0]));
        Assert.Equal(43, receipt["closures"]![1]!["terminalTurn"]!.GetValue<int>());
        var plan = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
            AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(later.Reduction!, later.Reduction!.Resources));
        Assert.Empty(plan.Issues);
        Assert.Equal(firstReceipt.ToJsonString(),
            (await next.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!.ToJsonString());
    }

    /// <summary>
    /// Reopens and declines a genuine terminal offer, then reduces it without publication.
    /// </summary>
    /// <param name="context">
    /// Signed fixture containing a committed C2 pair.
    /// </param>
    /// <param name="lease">
    /// Active lease for the fixture's current generation.
    /// </param>
    /// <returns>
    /// Actual completed C3 receipt and ordinary mechanics.
    /// </returns>
    private static async Task<ValidationService.SpiritualC3DecisionReductionResult> ReduceTerminalNoneAsync(
        ResourceMaterializationTestContext context, FileSystemManager.CanonicalWriteLease lease)
    {
        var validator = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var opened = await validator.OpenC2PrivateSessionAsync(lease);
        AssertNoConflictFrameErrors(opened.Issues);
        using var offer = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        var completed = await offer.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(new
        {
            opportunityRef = offer.Offer!.OpportunityRef, decision = "none"
        }), null);
        AssertNoConflictFrameErrors(completed.Issues);
        Assert.Equal("completed_unpublished", completed.Disposition);
        using var terminal = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(completed.Session);
        var reduced = await terminal.ReduceCompletedDecisionsAsync(lease);
        Assert.True(reduced.Success, string.Join(Environment.NewLine, reduced.Issues));
        return reduced;
    }

    /// <summary>
    /// Rejects malformed reward evidence before any terminal preparation grants execution authority.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC3Terminal_RewardValidationStillBlocksProducer()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        var root = await ReadProjectedSourceContinuationCandidateAsync(context);
        ResolveTerminalFixture(root);
        root["recentConflicts"]![0]!["rewardAudit"] = new JsonArray();
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await context.WriteExactBytesAsync(ProjectionNarrativePath,
            System.Text.Encoding.UTF8.GetBytes("{\"response\":\"Конфликт завершён.\"}"));
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        var before = await context.FileSystem.ReadFileBytesAsync(ResourceMaterializationContract.StatePath);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(captured.Capture);
        var issues = await capture.BeginResourceExecutionAsync(lease);
        Assert.Contains(issues, issue => issue.Code == "afterlife_conflict_reward_invalid_audit_shape");
        Assert.Null(OriginalCaptureField(capture, "_resources"));
        Assert.Equal(before, await context.FileSystem.ReadFileBytesAsync(lease, ResourceMaterializationContract.StatePath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }
}
