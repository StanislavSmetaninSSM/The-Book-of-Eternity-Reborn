using System.Text;
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
    /// Encodes the first current exchange explicitly and the remaining exchanges in a replacement carrier.
    /// </summary>
    /// <param name="projected">
    /// Detached direct conflict root with the complete current exchange log.
    /// </param>
    /// <returns>
    /// A raw exchange update whose duplicate first replacement row is ignored by ordinary composition.
    /// </returns>
    private static JsonObject WrapC2DependentConflict(JsonObject projected)
    {
        var replacement = projected["activeConflict"]!.DeepClone().AsObject();
        replacement.Remove("combatConditions");
        return new JsonObject
        {
            [AfterlifeSpiritualConflictState.ResponseField] = new JsonObject
            {
                ["mode"] = "exchange",
                ["exchange"] = projected["activeConflict"]!["exchangeLog"]![0]!.DeepClone(),
                ["activeConflictAfter"] = replacement
            }
        };
    }

    /// <summary>
    /// Completes a later original exchange before saving the current wound decision.
    /// </summary>
    /// <param name="materialize">
    /// Whether the first decision changes the later exchange's required action cost.
    /// </param>
    /// <param name="intermediateZero">
    /// Whether a harmless exchange precedes a later positive source.
    /// </param>
    /// <param name="throughAdapter">
    /// Whether the private GM adapter composes and transports the decision.
    /// </param>
    /// <param name="immutableCostMismatch">
    /// Whether the later original exchange has a cost base that GM continuation cannot edit.
    /// </param>
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, true)]
    public async Task OriginalSpiritualC2SavedDecision_ExecutesDependentNextExchangeAfterWound(
        bool materialize, bool intermediateZero, bool throughAdapter,
        bool immutableCostMismatch)
    {
        await ExecuteC2DependentExchangeAsync(materialize, intermediateZero, throughAdapter,
            immutableCostMismatch, inspectContext: false);
    }

    /// <summary>
    /// Keeps the saved correction frontier stable after a valid correction and cold reopen.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2DependentContext_PreservesBaselineAfterCorrection()
    {
        await ExecuteC2DependentExchangeAsync(materialize: true, intermediateZero: false,
            throughAdapter: true, immutableCostMismatch: false, inspectContext: true);
    }

    /// <summary>
    /// Maps cost dependencies to the actual raw carrier and preserves frozen siblings after correction.
    /// </summary>
    /// <param name="wrapper">
    /// Whether replacement rows follow an explicit exchange and contain its ignored duplicate.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalSpiritualC2DependentContext_BindsExactRawCostFields(bool wrapper)
    {
        await ExecuteC2DependentExchangeAsync(materialize: true, intermediateZero: false,
            throughAdapter: true, immutableCostMismatch: false, inspectContext: true,
            inspectPermissions: true, wrapper: wrapper);
    }

    /// <summary>
    /// Exercises genuine original capture, optional selection and subsequent exchange execution.
    /// </summary>
    /// <param name="materialize">
    /// Whether the first decision adds a wound affecting the later exchange.
    /// </param>
    /// <param name="intermediateZero">
    /// Whether a harmless exchange separates two positive opportunities.
    /// </param>
    /// <param name="throughAdapter">
    /// Whether to use the private GM session instead of the direct owner path.
    /// </param>
    /// <param name="immutableCostMismatch">
    /// Whether the later exchange contains an uncorrectable frozen base cost.
    /// </param>
    /// <param name="inspectContext">
    /// Whether to verify stable baseline diagnostics across a corrected cold reopen.
    /// </param>
    /// <param name="inspectPermissions">
    /// Whether to verify exact raw cost fields and refusal of an unrelated correction.
    /// </param>
    /// <param name="wrapper">
    /// Whether the raw draft uses an explicit exchange plus replacement log.
    /// </param>
    private static async Task ExecuteC2DependentExchangeAsync(bool materialize,
        bool intermediateZero, bool throughAdapter, bool immutableCostMismatch, bool inspectContext,
        bool inspectPermissions = false, bool wrapper = false)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync,
            signedDice: intermediateZero ? [15, 5, 15, 5] : null);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        var corrected = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = corrected["activeConflict"]!.AsObject();
        var second = active["exchangeLog"]![1]!.AsObject();
        var third = intermediateZero ? second.DeepClone().AsObject() : null;
        second["outcome"] = "no_effect";
        second["after"] = second["before"]!.DeepClone();
        second.Remove("diceAudit");
        if (intermediateZero)
        {
            second["operationType"] = "recover_spiritual_power";
            second["matchupAudit"]!["playerOperation"] = "recover_spiritual_power";
            second["matchupAudit"]!["oppositionOperation"] = "recover_spiritual_power";
            second["matchupAudit"]!["primaryResolutionLane"] = "recover_spiritual_power";
            second["matchupAudit"]!["riskProfile"] = "recovery_timing";
            second["actionCostAudit"] = new JsonObject
            {
                ["player"] = CostAudit("recover_spiritual_power", 0m, 3m, 3m),
                ["opposition"] = CostAudit("recover_spiritual_power", 0m, 3m, 6m)
            };
            third!["exchangeId"] = "exchange_source_third";
            third["before"] = second["after"]!.DeepClone();
            third["after"] = third["before"]!.DeepClone();
            third["after"]!["oppositionSideStrain"] = "fractured";
            third["actionCostAudit"]!["opposition"]!["before"] = 6;
            third["actionCostAudit"]!["opposition"]!["after"] = 3;
            third["diceAudit"]!["diceUsed"]![0]!["value"] = 15;
            third["diceAudit"]!["diceUsed"]![1]!["value"] = 5;
            third["diceAudit"]!["playerTotal"] = 15;
            third["diceAudit"]!["oppositionTotal"] = 5;
            third["diceAudit"]!["margin"] = 10;
            third["diceAudit"]!["outcomeBand"] = "decisive_player_success";
            active["exchangeLog"]!.AsArray().Add(third);
            active["oppositionSideStrain"] = "fractured";
        }
        else
        {
            second["matchupAudit"]!["oppositionOperation"] = "guard";
            var cost = second["actionCostAudit"]!["opposition"]!;
            cost["operationType"] = "guard";
            cost["baseCost"] = 2;
            cost["effectiveCost"] = 3;
            cost["after"] = 0;
            active["oppositionSideStrain"] = second["before"]!["oppositionSideStrain"]!.DeepClone();
        }
        var original = corrected.DeepClone().AsObject();
        if (!intermediateZero)
        {
            original["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["opposition"]!["effectiveCost"] = 2;
            original["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["opposition"]!["after"] = 1;
            if (immutableCostMismatch)
                original["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["opposition"]!["baseCost"] = 4;
        }
        if (wrapper)
        {
            original = WrapC2DependentConflict(original);
            corrected = WrapC2DependentConflict(corrected);
        }
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath,
            original.ToJsonString());
        await WriteOriginalIntakeDraftAsync(context);
        const string originalNarrative = "{\"response\":\"Духовный обмен завершён.\"}";
        const string correctedNarrative =
            "{\"response\":\"Хранитель удержал удар, но чужое давление надломило его волю.\",\"timestamp\":\"2026-01-01T00:00:00Z\"}";
        await context.WriteExactBytesAsync(ProjectionNarrativePath,
            Encoding.UTF8.GetBytes(originalNarrative));
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var warm = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await warm.BeginResourceExecutionAsync(lease));
        var first = await warm.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(first.Issues);
        var committed = await warm.CommitC2FirstTransportAsync(lease, first.Step!.Interval!);
        AssertNoConflictFrameErrors(committed.Issues);
        warm.Dispose();
        if (throughAdapter)
        {
            var adapter = new ValidationService(context.FileSystem,
                NullLogger<ValidationService>.Instance);
            var opened = await adapter.OpenC2PrivateSessionAsync(lease);
            Assert.Equal("offer", opened.Disposition);
            using var offeredSession = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(
                opened.Session);
            var safeOffer = offeredSession.Offer!;
            var response = materialize
                ? OriginalSpiritualWoundDecision(safeOffer.OpportunityRef,
                    "spiritual_action_cost_burden", "guard")
                : JsonSerializer.SerializeToElement(new
                {
                    opportunityRef = safeOffer.OpportunityRef,
                    decision = "none"
                });
            ValidationService.SpiritualC2PrivateSession? retriedSession = null;
            ValidationService.SpiritualC2DependentContext? originalContext = null;
            if (materialize)
            {
                var checkpointBeforeCorrection = await context.FileSystem.ReadFileBytesAsync(lease,
                    SpiritualWoundCaptureCheckpointState.StatePath);
                var pendingBeforeCorrection = await context.FileSystem.ReadFileBytesAsync(lease,
                    SpiritualWoundDecisionPendingState.StatePath);
                var dependent = await offeredSession.SubmitDecisionAsync(lease, response,
                    "Чужое давление надломило волю хранителя.");
                if (immutableCostMismatch)
                {
                    Assert.Equal("blocked", dependent.Disposition);
                    Assert.Equal(checkpointBeforeCorrection,
                        await context.FileSystem.ReadFileBytesAsync(lease,
                            SpiritualWoundCaptureCheckpointState.StatePath));
                    return;
                }
                Assert.True(dependent.Disposition == "dependent_continuation",
                    string.Join(Environment.NewLine, dependent.Issues));
                var priorCheckpoint = JsonNode.Parse(Encoding.UTF8.GetString(
                    checkpointBeforeCorrection!))!["checkpoint"]!.AsObject();
                var submittedCheckpoint = JsonNode.Parse(Encoding.UTF8.GetString(
                    await context.FileSystem.ReadFileBytesAsync(lease,
                        SpiritualWoundCaptureCheckpointState.StatePath) ?? []))!["checkpoint"]!.AsObject();
                var submission = Assert.IsType<JsonObject>(submittedCheckpoint["pendingSubmission"]);
                Assert.Equal(pendingBeforeCorrection,
                    await context.FileSystem.ReadFileBytesAsync(lease,
                        SpiritualWoundDecisionPendingState.StatePath));
                foreach (var field in new[] { "allocations", "advances", "committedAdvance",
                             "expectedPendingPacketFingerprint" })
                    Assert.True(JsonNode.DeepEquals(priorCheckpoint[field], submittedCheckpoint[field]),
                        $"Staging the selected wound changed committed field {field}.");
                Assert.Equal(priorCheckpoint["committedAdvance"]!.GetValue<int>(),
                    submission["priorCommittedAdvance"]!.GetValue<int>());
                Assert.Equal(priorCheckpoint["expectedPendingPacketFingerprint"]!.GetValue<string>(),
                    submission["priorPendingPacketFingerprint"]!.GetValue<string>());
                Assert.Equal("materialize", submission["stagedDecision"]!["decision"]!.GetValue<string>());
                Assert.Equal(safeOffer.OpportunityRef,
                    submission["stagedDecision"]!["opportunityRef"]!.GetValue<string>());
                Assert.Equal(await context.FileSystem.ReadFileBytesAsync(lease,
                        AcceptedMechanicsPlan.WoundCommandPath),
                    Convert.FromBase64String(submission["command"]!["contentBase64"]!.GetValue<string>()));
                Assert.NotEmpty(submission["allocations"]!.AsArray());
                var coldAdapter = new ValidationService(context.FileSystem,
                    NullLogger<ValidationService>.Instance);
                var selectedAgain = await coldAdapter.OpenC2PrivateSessionAsync(lease);
                Assert.Equal("dependent_continuation", selectedAgain.Disposition);
                using (var selectedSession = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(
                           selectedAgain.Session))
                {
                    Assert.Null(selectedSession.Offer);
                    if (inspectContext)
                    {
                        originalContext = Assert.IsType<ValidationService.SpiritualC2DependentContext>(
                            await selectedSession.ReadDependentContextAsync(lease));
                        Assert.NotEmpty(originalContext.BaselineIssues);
                        Assert.NotEmpty(originalContext.CurrentIssues);
                        if (inspectPermissions)
                        {
                            var prefix = wrapper
                                ? "/afterlifeSpiritualConflictUpdate/activeConflictAfter/exchangeLog/1"
                                : "/activeConflict/exchangeLog/1";
                            Assert.Equal(new[] { prefix + "/actionCostAudit/opposition/after",
                                    prefix + "/actionCostAudit/opposition/effectiveCost" },
                                originalContext.DependentDraftFields.Select(field => field.JsonPointer));
                            Assert.All(originalContext.DependentDraftFields, field =>
                                Assert.Equal(AfterlifeSpiritualConflictState.StatePath, field.Path));
                        }
                    }
                    var replaced = await selectedSession.SubmitDecisionAsync(lease,
                        JsonSerializer.SerializeToElement(new
                        {
                            opportunityRef = safeOffer.OpportunityRef,
                            decision = "none"
                        }), null);
                    Assert.Equal("blocked", replaced.Disposition);
                    Assert.True(JsonNode.DeepEquals(submittedCheckpoint,
                        JsonNode.Parse(Encoding.UTF8.GetString(await context.FileSystem.ReadFileBytesAsync(lease,
                            SpiritualWoundCaptureCheckpointState.StatePath) ?? []))!["checkpoint"]));
                }
                await context.FileSystem.WriteFileAtomicBytesAsync(lease,
                    AfterlifeSpiritualConflictState.StatePath,
                    Encoding.UTF8.GetBytes(corrected.ToJsonString()));
                await context.FileSystem.WriteFileAtomicBytesAsync(lease,
                    ProjectionNarrativePath, Encoding.UTF8.GetBytes(correctedNarrative));
                var reopenedAdapter = await adapter.OpenC2PrivateSessionAsync(lease);
                Assert.Equal("dependent_continuation", reopenedAdapter.Disposition);
                retriedSession = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(
                    reopenedAdapter.Session);
                Assert.Null(retriedSession.Offer);
                if (inspectContext)
                {
                    var correctedContext = Assert.IsType<ValidationService.SpiritualC2DependentContext>(
                        await retriedSession.ReadDependentContextAsync(lease));
                    Assert.Equal(originalContext!.ContinuationId, correctedContext.ContinuationId);
                    Assert.Equal(originalContext.BaselineIssues.Select(issue => (issue.Code, issue.FilePath)),
                        correctedContext.BaselineIssues.Select(issue => (issue.Code, issue.FilePath)));
                    Assert.Empty(correctedContext.CurrentIssues);
                    if (inspectPermissions)
                    {
                        Assert.Equal(originalContext.DependentDraftFields, correctedContext.DependentDraftFields);
                        retriedSession.Dispose();
                        var damaged = corrected.DeepClone().AsObject();
                        if (wrapper)
                            damaged[AfterlifeSpiritualConflictState.ResponseField]!["activeConflictAfter"]!
                                ["exchangeLog"]![0]!["unrelatedWitness"] = "changed ignored row";
                        else
                            damaged["unrelatedWitness"] = "changed root sibling";
                        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
                            AfterlifeSpiritualConflictState.StatePath,
                            Encoding.UTF8.GetBytes(damaged.ToJsonString()));
                        var rejected = await adapter.OpenC2PrivateSessionAsync(lease);
                        using (var damagedSession = rejected.Session)
                        {
                            Assert.True(damagedSession is null ||
                                await damagedSession.ReadDependentContextAsync(lease) is null,
                                "An unrelated raw sibling must not gain correction permission.");
                        }
                        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
                            AfterlifeSpiritualConflictState.StatePath,
                            Encoding.UTF8.GetBytes(corrected.ToJsonString()));
                        var restored = await adapter.OpenC2PrivateSessionAsync(lease);
                        Assert.Equal("dependent_continuation", restored.Disposition);
                        retriedSession = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(restored.Session);
                    }
                }
            }
            var adapted = retriedSession is not null
                ? await retriedSession.ResumeDependentContinuationAsync(lease)
                : await offeredSession.SubmitDecisionAsync(lease, response, null);
            AssertNoConflictFrameErrors(adapted.Issues);
            Assert.Equal(intermediateZero ? "offer" : "completed_unpublished",
                adapted.Disposition);
            using var continued = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(
                adapted.Session);
            Assert.Equal(intermediateZero, continued.Offer is not null);
            if (inspectContext)
            {
                Assert.Null(await retriedSession!.ReadDependentContextAsync(lease));
                Assert.Null(await continued.ReadDependentContextAsync(lease));
            }
            var saved = JsonNode.Parse(Encoding.UTF8.GetString((await context.FileSystem.ReadFileBytesAsync(
                lease, SpiritualWoundCaptureCheckpointState.StatePath))!))!["checkpoint"]!;
            Assert.Equal(1, saved["committedAdvance"]!.GetValue<int>());
            Assert.Null(saved["pendingSubmission"]);
            if (materialize && !intermediateZero)
            {
                var reduced = await continued.ReduceCompletedDecisionsAsync(lease);
                Assert.True(reduced.Success, string.Join(Environment.NewLine,
                    reduced.Issues.Select(issue => $"{issue.Code}: {issue}")));
                var ordinary = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(
                    reduced.Reduction);
                var secondCost = Assert.Single(ordinary.Resources.AppliedTransitions,
                    transition => transition.OriginId == "exchange_source_second" &&
                        transition.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeConflictSide &&
                        transition.Operation == ResourceTransitionOperation.Spend);
                Assert.Equal(3m, secondCost.AppliedAmount);
                Assert.Equal(0m, secondCost.AfterState!.Current);
                var insertion = Assert.Single(Assert.IsType<SpiritualLiveWoundCompletion>(
                    ordinary.LiveWoundCompletion).Insertions);
                var receipt = Assert.Single(reduced.ReceiptAfterImage!["decisions"]!.AsArray())!;
                Assert.Equal("materialize", receipt["decision"]!.GetValue<string>());
                Assert.Equal(insertion.Wound.WoundId, receipt["woundId"]!.GetValue<string>());
                Assert.Equal(insertion.Wound.LastTransition.TransitionId,
                    receipt["transitionId"]!.GetValue<string>());
                var planned = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
                    AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(
                        ordinary, ordinary.Resources));
                Assert.Empty(planned.Issues);
                var plan = Assert.IsType<AcceptedMechanicsPlan>(planned.Plan);
                Assert.True(JsonNode.DeepEquals(
                    JsonNode.Parse(ordinary.Resources.StateAfterImage!.ToCanonicalJson()),
                    plan.StateAfterImage));
                Assert.True(JsonNode.DeepEquals(
                    JsonNode.Parse(ordinary.Resources.HistoryAfterImage!.ToCanonicalJson()),
                    plan.HistoryAfterImage));
            }
            retriedSession?.Dispose();
            return;
        }
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var classified = await fresh.ClassifyInitialSpiritualPendingAsync(lease);
        Assert.Equal("match", classified.Disposition);
        using var unrepaired = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(classified.Capture);
        var next = await unrepaired.ReadC2NextSourceAsync(lease);
        AssertNoConflictFrameErrors(next.Issues);
        var admitted = await unrepaired.AdmitWoundSourceAsync(lease, next.Interval!, next.Source!);
        AssertNoConflictFrameErrors(admitted.Issues);
        var offered = await unrepaired.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(offered.Issues);
        var decision = materialize
            ? OriginalSpiritualWoundDecision(offered.Opportunity!.PublicRef,
                "spiritual_action_cost_burden", "guard")
            : JsonSerializer.SerializeToElement(new
            {
                opportunityRef = offered.Opportunity!.PublicRef,
                decision = "none"
            });
        var command = WoundResponseInputComposer.Compose(offered.Binding!,
            [offered.Opportunity], [decision], materialize
                ? "Чужое давление надломило волю хранителя." : null, []);
        Assert.True(command.Success);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            AcceptedMechanicsPlan.WoundCommandPath,
            Encoding.UTF8.GetBytes(command.CommandRoot!.ToJsonString()));
        ValidationService.SpiritualOriginalTurnCapture cold = unrepaired;
        if (materialize)
        {
            var checkpointBeforeCorrection = await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundCaptureCheckpointState.StatePath);
            var premature = await unrepaired.AdvanceC2NextDecisionDraftAsync(lease);
            Assert.Null(premature.Checkpoint);
            Assert.NotEmpty(premature.Issues);
            Assert.Equal(checkpointBeforeCorrection,
                await context.FileSystem.ReadFileBytesAsync(lease,
                    SpiritualWoundCaptureCheckpointState.StatePath));
            unrepaired.Dispose();
            await context.FileSystem.WriteFileAtomicBytesAsync(lease,
                AfterlifeSpiritualConflictState.StatePath,
                Encoding.UTF8.GetBytes(corrected.ToJsonString(new JsonSerializerOptions
                {
                    WriteIndented = true
                })));
            await context.FileSystem.WriteFileAtomicBytesAsync(lease,
                ProjectionNarrativePath, Encoding.UTF8.GetBytes(correctedNarrative));
            var correctedPair = await fresh.ClassifyInitialSpiritualPendingAsync(lease);
            Assert.Equal("match", correctedPair.Disposition);
            cold = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(
                correctedPair.Capture);
        }
        var staged = await cold.AdvanceC2NextDecisionDraftAsync(lease);

        AssertNoConflictFrameErrors(staged.Issues);
        Assert.Equal(1, staged.Checkpoint!.CommittedAdvance);
        var packet = JsonNode.Parse(SpiritualWoundDecisionPendingState.SerializeCanonical(staged.Pending!))!
            ["pending"]!.AsObject();
        Assert.Equal(intermediateZero ? 3 : 2,
            packet["cursor"]!["exchangeOrdinal"]!.GetValue<int>());
        Assert.Equal(intermediateZero ? 3 : 2,
            cold.ReadClosedExchangeEvidence(lease).Count);
        if (intermediateZero)
            Assert.True(packet["cursor"]!["nextSourceOrdinal"]!.GetValue<int>() <
                packet["sources"]!.AsArray().Count);
        if (intermediateZero)
        {
            var secondInterval = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionStep>(
                OriginalCaptureField(cold, "_lastResourceStep")).Interval!;
            var ownerImages = await cold.ReadC1CandidateOwnerImagesAsync(lease, secondInterval);
            var conflictCandidate = packet["candidateAfterImages"]!.AsArray().Single(row =>
                row!["path"]!.GetValue<string>() == AfterlifeSpiritualConflictState.StatePath)!;
            Assert.Equal(Convert.ToBase64String(ownerImages[AfterlifeSpiritualConflictState.StatePath].Bytes!),
                conflictCandidate["contentBase64"]!.GetValue<string>());
            Assert.Null(OriginalCaptureField(cold, "_completedOrdinaryReduction"));
        }
        else
        {
            var completedReduction = await cold.CompleteOrdinaryReductionAsync(lease);
            AssertNoConflictFrameErrors(completedReduction.Issues);
            Assert.Contains(completedReduction.Reduction!.Resources.AppliedTransitions,
                transition => transition.OriginId == "exchange_source_second");
        }
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes(
                materialize ? correctedNarrative : originalNarrative)),
            packet["preservedDraft"]!["contentBase64"]!.GetValue<string>());
        cold.Dispose();
        var transportValidator = new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance);
        var transportPair = await transportValidator.ClassifyInitialSpiritualPendingAsync(lease);
        Assert.Equal("match", transportPair.Disposition);
        using var transportOwner = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(
            transportPair.Capture);
        var transported = await transportOwner.CommitC2SavedTransportAsync(lease);
        AssertNoConflictFrameErrors(transported.Issues);
        Assert.Equal("committed", transported.Disposition);
        Assert.Equal(staged.Checkpoint!.CommittedAdvance, transported.Checkpoint!.CommittedAdvance);
        transportOwner.Dispose();
        context.FileSystem.DeleteFile(lease, SpiritualWoundDecisionPendingState.StatePath);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            AfterlifeSpiritualConflictState.StatePath, Encoding.UTF8.GetBytes(original.ToJsonString()));
        var reopened = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var replayed = await reopened.ReplaySavedSpiritualCheckpointAsync(lease);

        AssertNoConflictFrameErrors(replayed.Issues);
        using var replayCapture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(replayed.Capture);
        Assert.Equal(transported.Pending!.PacketFingerprint, replayed.Pending!.PacketFingerprint);
        Assert.Equal(intermediateZero ? 3 : 2,
            replayCapture.ReadClosedExchangeEvidence(lease).Count);
        if (intermediateZero)
        {
            replayCapture.Dispose();
            await context.FileSystem.WriteFileAtomicBytesAsync(lease,
                SpiritualWoundDecisionPendingState.StatePath,
                Encoding.UTF8.GetBytes(
                    SpiritualWoundDecisionPendingState.SerializeCanonical(replayed.Pending!)));
            var matched = await reopened.ClassifySpiritualPendingAsync(lease);
            Assert.True(matched.Disposition == "match",
                string.Join(Environment.NewLine, matched.Issues));
            using var offerOwner = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(
                matched.Capture);
            var thirdOffer = await offerOwner.ReadC2NextSourceAsync(lease);
            AssertNoConflictFrameErrors(thirdOffer.Issues);
            Assert.Equal("exchange_source_third", thirdOffer.Source!.ExchangeId);
        }
    }
}
