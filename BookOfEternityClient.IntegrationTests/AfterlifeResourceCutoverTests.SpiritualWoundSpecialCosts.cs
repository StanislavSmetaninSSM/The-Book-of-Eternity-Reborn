using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Pays an owned wound burden before recovery and preserves both operations in the completed resource history.
    /// </summary>
    /// <param name="player">
    /// Whether the recovering wounded actor is the player rather than the opposition.
    /// </param>
    /// <param name="failed">
    /// Whether the player's recovery fails and must still pay the burden.
    /// </param>
    /// <param name="reactionGain">
    /// Optional same-coordinate Spend reaction: true gains one point, false spends one; null adds no resource reaction.
    /// </param>
    [Theory]
    [InlineData(true, false, null)]
    [InlineData(true, true, null)]
    [InlineData(false, false, null)]
    [InlineData(true, false, true)]
    [InlineData(true, false, false)]
    public Task OriginalSpiritualGeneration_RecoveryPaysWoundBeforeGain(bool player, bool failed, bool? reactionGain) =>
        VerifyWoundedRecoveryAsync(player, failed, reactionGain);

    /// <summary>
    /// Rejects missing or false recovery payment evidence and prevents paying a wound from future recovery.
    /// </summary>
    /// <param name="player">
    /// Whether the invalid payment belongs to the player rather than the opposition.
    /// </param>
    /// <param name="variant">
    /// The affordability or cost-evidence defect to introduce in the later recovery.
    /// </param>
    [Theory]
    [InlineData(true, "insufficient")]
    [InlineData(false, "insufficient")]
    [InlineData(true, "wrong_cost")]
    [InlineData(false, "wrong_cost")]
    [InlineData(true, "missing_audit")]
    [InlineData(false, "missing_audit")]
    public Task OriginalSpiritualGeneration_RecoveryRejectsUnpaidWound(bool player, string variant) =>
        VerifyWoundedRecoveryAsync(player, failed: false, reactionGain: null, variant: variant);

    /// <summary>
    /// Limits opposed recovery to one point after paying the wound, even when the net balance does not increase.
    /// </summary>
    /// <param name="player">
    /// Whether the wounded recovering participant is the player rather than opposition.
    /// </param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task OriginalSpiritualGeneration_OpposedRecoveryPaysBeforeLimitedGain(bool player) =>
        VerifyWoundedRecoveryAsync(player, failed: false, reactionGain: null, variant: "opposed");

    /// <summary>
    /// Corrects a future recovery after a new wound changes its payment and action-only result.
    /// </summary>
    /// <param name="player">
    /// Whether the changed future recovery belongs to the player rather than opposition.
    /// </param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task OriginalSpiritualGeneration_RecoveryCorrectsDependentCost(bool player) =>
        VerifyWoundedRecoveryAsync(player, failed: false, reactionGain: null, variant: "dependent");

    /// <summary>
    /// Allows correction when payment is already updated but recovery still uses the old pre-payment arithmetic.
    /// </summary>
    /// <param name="player">
    /// Whether the stale recovery result belongs to the player rather than opposition.
    /// </param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task OriginalSpiritualGeneration_RecoveryCorrectsDependentGain(bool player) =>
        VerifyWoundedRecoveryAsync(player, failed: false, reactionGain: null, variant: "dependent_delta");

    /// <summary>
    /// Runs recovery through the signed capture, private decision adapter, common reduction and cold replay.
    /// </summary>
    /// <param name="player">
    /// Whether recovery belongs to the newly wounded player rather than opposition.
    /// </param>
    /// <param name="failed">
    /// Whether the player recovery yields no gain after payment.
    /// </param>
    /// <param name="reactionGain">
    /// Optional independent resource reaction; null preserves the ordinary sequence.
    /// </param>
    /// <param name="variant">
    /// Optional opposed, dependent-correction or invalid-input variant; null uses ordinary recovery.
    /// </param>
    /// <returns>
    /// A task completing after the selected signed scenario has been checked.
    /// </returns>
    private async Task VerifyWoundedRecoveryAsync(bool player, bool failed, bool? reactionGain, string? variant = null)
    {
        var insufficient = variant == "insufficient";
        var rejected = variant is "insufficient" or "wrong_cost" or "missing_audit";
        var opposed = variant == "opposed";
        var dependent = variant is "dependent" or "dependent_delta";
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync,
            signedDice: player ? [5, 15, 12, 8] : null,
            playerCurrent: insufficient && player ? 3m : 6m,
            oppositionCurrent: insufficient && !player ? 3m : 6m);
        if (reactionGain.HasValue)
        {
            await SeedOriginalPrefixActionPointEffectAsync(context, reactionGain.Value, bounded: false);
            await context.CaptureValidatedPendingSnapshotAsync(turn: 42, currentRealm: "Chaos Sea",
                preGeneratedDices1d20: [5, 15, 12, 8]);
        }
        await WriteCompleteConflictFrameExchangeAsync(context);
        if (player)
        {
            var initial = await ReadProjectedSourceContinuationCandidateAsync(context);
            var active = initial["activeConflict"]!;
            var exchange = active["exchangeLog"]![0]!;
            exchange["outcome"] = "setback";
            exchange["after"]!["playerSideStrain"] = "strained";
            exchange["after"]!["oppositionSideStrain"] = "clear";
            var dice = exchange["diceAudit"]!;
            dice["diceUsed"]![0]!["value"] = 5;
            dice["diceUsed"]![1]!["value"] = 15;
            dice["playerTotal"] = 5;
            dice["oppositionTotal"] = 15;
            dice["margin"] = -10;
            dice["outcomeBand"] = "decisive_opposition_success";
            active["playerSideStrain"] = "strained";
            active["oppositionSideStrain"] = "clear";
            if (reactionGain.HasValue)
            {
                // No initial player Spend: the original art's one use belongs to the later wound payment.
                exchange["operationType"] = "recover_spiritual_power";
                exchange["matchupAudit"]!["playerOperation"] = "recover_spiritual_power";
                exchange["matchupAudit"]!["primaryResolutionLane"] = "recover_spiritual_power";
                exchange["matchupAudit"]!["riskProfile"] = "recovery_timing";
                exchange["actionCostAudit"]!["player"] = CostAudit("recover_spiritual_power", 0, 6, 6);
            }
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, initial.ToJsonString());
        }
        if (insufficient)
        {
            var initial = await ReadProjectedSourceContinuationCandidateAsync(context);
            var cost = initial["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]![player ? "player" : "opposition"]!;
            cost["before"] = 3;
            cost["after"] = 0;
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, initial.ToJsonString());
        }
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        var candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
        var conflict = candidate["activeConflict"]!;
        var second = conflict["exchangeLog"]![1]!.AsObject();
        second["after"] = second["before"]!.DeepClone();
        second["outcome"] = player && !failed && !opposed ? "success" : "no_effect";
        if (!player || failed || opposed)
            second.Remove("diceAudit");
        var playerOperation = opposed && !player ? "pressure" : "recover_spiritual_power";
        var oppositionOperation = opposed && player ? "pressure" : "recover_spiritual_power";
        second["operationType"] = playerOperation;
        second["matchupAudit"]!["playerOperation"] = playerOperation;
        second["matchupAudit"]!["oppositionOperation"] = oppositionOperation;
        second["matchupAudit"]!["primaryResolutionLane"] = playerOperation;
        second["matchupAudit"]!["riskProfile"] = playerOperation == "pressure" ? "offensive_pressure" : "recovery_timing";
        var recoveryBefore = insufficient ? 0 : reactionGain.HasValue ? 6 : 3;
        var postPayment = recoveryBefore - 1;
        var after = failed ? postPayment : Math.Min(6, postPayment + (opposed ? 1 : 3));
        var recoveryAmount = after - postPayment;
        var recoveryAudit = CostAudit("recover_spiritual_power", 1, recoveryBefore, after);
        recoveryAudit["baseCost"] = 0;
        recoveryAudit["minCost"] = 0;
        second["actionCostAudit"] = new JsonObject
        {
            [player ? "player" : "opposition"] = recoveryAudit,
            [player ? "opposition" : "player"] = opposed ? CostAudit("pressure", 3, 3, 0) :
                CostAudit("recover_spiritual_power", 0, 3, player ? 6 : 3)
        };
        if (variant is "wrong_cost" or "dependent" or "dependent_delta")
        {
            recoveryAudit["effectiveCost"] = variant == "dependent_delta" ? 1 : 0;
            recoveryAudit["after"] = Math.Min(6, recoveryBefore + 3);
        }
        if (variant == "missing_audit")
            second["actionCostAudit"]!.AsObject().Remove(player ? "player" : "opposition");
        foreach (var side in new[] { "playerSideStrain", "oppositionSideStrain" })
            conflict[side] = second["after"]![side]!.DeepClone();
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
        await context.WriteExactBytesAsync(ProjectionNarrativePath,
            Encoding.UTF8.GetBytes("{\"response\":\"Духовный обмен завершён.\"}"));
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        _ = context.FileSystem.GetOrCreateSessionGeneration(lease);
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
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
        using var offerSession = offered.Session!;
        var decision = JsonNode.Parse(OriginalSpiritualWoundDecision(offerSession.Offer!.OpportunityRef,
            "spiritual_action_cost_burden", "recover_spiritual_power", player ? "player" : "guardian").GetRawText())!;
        var definition = decision["proposal"]!["consequenceDefinitions"]![0]!["definition"]!;
        definition["triggers"] = new JsonArray(new JsonObject
        {
            ["triggerId"] = "own_recovery_payment", ["eventType"] = "resource_spent", ["priority"] = 100,
            ["componentIds"] = new JsonArray("component_001"), ["consumeUses"] = true,
            ["resolutionMode"] = "deterministic"
        });
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "uses", ["initialUses"] = 2,
            ["consumingEventTypes"] = new JsonArray("resource_spent")
        };
        var checkpointBefore = !rejected && !dependent ? null :
            await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath);
        var pendingBefore = !rejected && !dependent ? null :
            await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundDecisionPendingState.StatePath);
        var resourcesBefore = !rejected && !dependent ? null :
            await context.FileSystem.ReadFileBytesAsync(lease, ResourceMaterializationContract.StatePath);
        var inserted = await offerSession.SubmitDecisionAsync(lease,
            JsonSerializer.SerializeToElement(decision),
            player ? "Чужое давление надломило волю души." : "Чужое давление надломило волю хранителя.");
        var selectedCheckpoint = !rejected && !dependent ? null :
            await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath);
        var selectedCommand = !rejected && !dependent ? null :
            await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
        if (rejected || dependent)
        {
            var retainsSelection = dependent || variant == "wrong_cost" || !player && variant == "missing_audit";
            if (retainsSelection)
            {
                var original = JsonNode.Parse(checkpointBefore!)!["checkpoint"]!;
                var selected = JsonNode.Parse(selectedCheckpoint!)!["checkpoint"]!;
                foreach (var field in new[] { "allocations", "advances", "committedAdvance", "expectedPendingPacketFingerprint" })
                    Assert.True(JsonNode.DeepEquals(original[field], selected[field]), field);
                var submission = Assert.IsType<JsonObject>(selected["pendingSubmission"]);
                Assert.Equal(original["committedAdvance"]!.GetValue<int>(),
                    submission["priorCommittedAdvance"]!.GetValue<int>());
                Assert.Equal("materialize", submission["stagedDecision"]!["decision"]!.GetValue<string>());
                Assert.Equal(decision["opportunityRef"]!.GetValue<string>(),
                    submission["stagedDecision"]!["opportunityRef"]!.GetValue<string>());
                Assert.Equal(selectedCommand,
                    Convert.FromBase64String(submission["command"]!["contentBase64"]!.GetValue<string>()));
                Assert.NotEmpty(submission["allocations"]!.AsArray());
            }
            else
                Assert.Equal(checkpointBefore, selectedCheckpoint);
            Assert.Equal(pendingBefore, await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundDecisionPendingState.StatePath));
            Assert.Equal(resourcesBefore, await context.FileSystem.ReadFileBytesAsync(lease,
                ResourceMaterializationContract.StatePath));
        }
        if (dependent)
        {
            Assert.True(inserted.Disposition == "dependent_continuation",
                inserted.Disposition + Environment.NewLine + string.Join(Environment.NewLine, inserted.Issues));
            recoveryAudit["effectiveCost"] = 1;
            recoveryAudit["after"] = after;
            await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath,
                Encoding.UTF8.GetBytes(candidate.ToJsonString()));
            await context.FileSystem.WriteFileAtomicBytesAsync(lease, ProjectionNarrativePath,
                Encoding.UTF8.GetBytes("{\"response\":\"Рана затруднила восстановление сил.\",\"timestamp\":\"2026-01-01T00:00:00Z\"}"));
            offerSession.Dispose();
            var corrected = await adapter.OpenC2PrivateSessionAsync(lease);
            AssertNoConflictFrameErrors(corrected.Issues);
            Assert.Equal("dependent_continuation", corrected.Disposition);
            using var correctionSession = corrected.Session!;
            Assert.Null(correctionSession.Offer);
            Assert.Equal(selectedCheckpoint, await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundCaptureCheckpointState.StatePath));
            Assert.Equal(selectedCommand, await context.FileSystem.ReadFileBytesAsync(lease,
                AcceptedMechanicsPlan.WoundCommandPath));
            inserted = await correctionSession.ResumeDependentContinuationAsync(lease);
        }
        if (rejected)
        {
            Assert.NotEqual("completed_unpublished", inserted.Disposition);
            Assert.Equal(selectedCheckpoint,
                await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath));
            Assert.Equal(selectedCommand, await context.FileSystem.ReadFileBytesAsync(lease,
                AcceptedMechanicsPlan.WoundCommandPath));
            if (insufficient)
                Assert.Contains(inserted.Issues, value => value.Code == (player
                    ? "afterlife_conflict_action_points_insufficient" : "afterlife_conflict_opposition_action_points_insufficient"));
            else if (variant == "wrong_cost")
                Assert.Contains(inserted.Issues, value => value.Code == (player
                    ? "afterlife_conflict_action_cost_mismatch" : "afterlife_conflict_opposition_action_cost_mismatch"));
            inserted.Session?.Dispose();
            return;
        }
        AssertNoConflictFrameErrors(inserted.Issues);
        Assert.Equal("completed_unpublished", inserted.Disposition);
        if (dependent)
        {
            Assert.Null(JsonNode.Parse((await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundCaptureCheckpointState.StatePath))!)!["checkpoint"]!["pendingSubmission"]);
            Assert.Equal(selectedCommand, await context.FileSystem.ReadFileBytesAsync(lease,
                AcceptedMechanicsPlan.WoundCommandPath));
        }
        using var terminal = inserted.Session!;
        var reduced = await terminal.ReduceCompletedDecisionsAsync(lease);
        Assert.True(reduced.Success, string.Join(Environment.NewLine, reduced.Issues));
        var ordinary = reduced.Reduction!;
        var insertion = Assert.Single(ordinary.LiveWoundCompletion!.Insertions);
        var root = Assert.Single(insertion.Wound.Consequences.OwnedEffectSources.RootBindings);
        if (reactionGain == false)
            // Both the action payment and the independent Spend reaction consume a use.
            Assert.DoesNotContain(ordinary.Effects!.ActiveEffects,
                value => value["effectId"]!.GetValue<string>() == root.EffectId);
        else
        {
            var effect = Assert.Single(ordinary.Effects!.ActiveEffects,
                value => value["effectId"]!.GetValue<string>() == root.EffectId);
            // Guardian effects retain actor ownership; opposition spends its distinct pooled side coordinate.
            Assert.Equal(player ? 1 : 2, effect["lifetime"]!["remainingUses"]!.GetValue<int>());
        }
        var owner = player ? ResourceOwnerKind.AfterlifeActor : ResourceOwnerKind.AfterlifeConflictSide;
        var transitions = ordinary.Resources.AppliedTransitions.Where(value =>
            value.OriginId == "exchange_source_second" && value.Coordinate.OwnerKind == owner).ToArray();
        Assert.Equal(failed ? 1 : 2, transitions.Length);
        Assert.Equal(ResourceTransitionOperation.Spend, transitions[0].Operation);
        Assert.Equal(1m, transitions[0].AppliedAmount);
        Assert.Equal(recoveryBefore, transitions[0].BeforeState!.Current);
        Assert.Equal(postPayment, transitions[0].AfterState!.Current);
        var gainBefore = postPayment + (reactionGain.HasValue ? reactionGain.Value ? 1 : -1 : 0);
        var actualAfter = Math.Min(6, gainBefore + recoveryAmount);
        if (!failed)
        {
            Assert.Equal(ResourceTransitionOperation.Gain, transitions[1].Operation);
            Assert.Equal(recoveryAmount, transitions[1].RequestedAmount);
            Assert.Equal(actualAfter - gainBefore, transitions[1].AppliedAmount);
            Assert.Equal(gainBefore, transitions[1].BeforeState!.Current);
            Assert.Equal(actualAfter, transitions[1].AfterState!.Current);
        }
        if (reactionGain.HasValue)
        {
            var history = ordinary.Resources.AppliedTransitions.Where(value => value.Coordinate.OwnerKind == owner).ToArray();
            Assert.Equal(3, history.Length);
            Assert.Same(transitions[0], history[0]);
            Assert.Same(transitions[1], history[2]);
            Assert.Equal(reactionGain.Value ? ResourceTransitionOperation.Gain : ResourceTransitionOperation.Spend,
                history[1].Operation);
            Assert.Equal(1m, history[1].AppliedAmount);
            Assert.Equal(postPayment, history[1].BeforeState!.Current);
            Assert.Equal(gainBefore, history[1].AfterState!.Current);
        }
        var planned = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
            AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(ordinary, ordinary.Resources));
        Assert.Empty(planned.Issues);
        Assert.Equal(actualAfter, ResolvePlannedActionPoints(planned.Plan!, owner).Current);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(ordinary.Resources.HistoryAfterImage!.ToCanonicalJson()),
            planned.Plan!.HistoryAfterImage));
        terminal.Dispose();
        offerSession.Dispose();
        var replayValidator = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var reopened = await replayValidator.OpenC2PrivateSessionAsync(lease);
        AssertNoConflictFrameErrors(reopened.Issues);
        Assert.Equal("completed_unpublished", reopened.Disposition);
        using var replaySession = reopened.Session!;
        var replayed = await replaySession.ReduceCompletedDecisionsAsync(lease);
        Assert.True(replayed.Success, string.Join(Environment.NewLine, replayed.Issues));
        var replayPlan = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
            AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(
                replayed.Reduction!, replayed.Reduction!.Resources));
        Assert.Empty(replayPlan.Issues);
        Assert.True(JsonNode.DeepEquals(planned.Plan.StateAfterImage, replayPlan.Plan!.StateAfterImage));
        Assert.True(JsonNode.DeepEquals(planned.Plan.HistoryAfterImage, replayPlan.Plan.HistoryAfterImage));
        Assert.True(JsonNode.DeepEquals(reduced.ReceiptAfterImage, replayed.ReceiptAfterImage));
    }
}
