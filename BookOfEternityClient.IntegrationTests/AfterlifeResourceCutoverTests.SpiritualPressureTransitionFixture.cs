using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    private const string PressureTransitionWoundId = "wound_retained_pressure_owner_3";
    private const string PressureTransitionEffectId = "effect_retained_pressure_owner_3";
    private const string PressureTransitionOrdinaryId = "exchange_pressure_transition_46_e1";
    private const string PressureTransitionScene = "Чужое давление надломило волю души.";

    private sealed record PressureTransitionWindow(string Name, ResourceMaterializationTestContext Context,
        FileSystemManager.CanonicalWriteLease Lease, ValidationService.SpiritualOriginalTurnCapture Capture,
        ValidationService.SpiritualOriginalTurnCapture.WoundSourceAdmission Admission,
        ValidationService.SpiritualOriginalTurnCapture.WoundSelection Selection,
        AcceptedMechanicsPlanner.SpiritualExchangeInterval Interval, JsonElement Decision,
        JsonObject Ordinary, JsonObject Conflict, string ResourceDefinitions, RetainedPressureGroupData Data);

    private sealed record PressureTransitionBaseline(IReadOnlyDictionary<string, byte[]> Files, byte[] Generation,
        ValidationService.SpiritualClosedPressureComparisonProbeState State);

    /// <summary>Independent complete signed two-exchange candidate, one E0 advance and registered I→II selection per Fact.</summary>
    /// <param name="trigger">The independently authored third-root activation kind.</param>
    /// <param name="work">The test body using only this fixture's live owners.</param>
    /// <param name="expectedActualEvaluations">Explicit actual-source validation count; null preserves the two legacy test contours.</param>
    /// <param name="revokesCapture">Whether this negative/lifetime test intentionally closes or revokes its capture.</param>
    /// <returns>Completion after the body, exact actual-work accounting and owned cleanup.</returns>
    private async Task WithPressureTransitionAsync(SpiritualPressureThirdGuardTrigger trigger,
        Func<PressureTransitionWindow, Task> work, int? expectedActualEvaluations = null, bool revokesCapture = false)
    {
        var name = trigger == SpiritualPressureThirdGuardTrigger.TurnEnd ? "group_routing_transition" : "group_cut_transition";
        ResourceMaterializationTestContext? context = null;
        var started = Stopwatch.GetTimestamp();
        var data = SpiritualPressureGroupOwnerCaseData.Create(2, thirdGuard: trigger);
        try
        {
            context = await CreateCompleteConflictFrameContextAsync(playerCurrent: 10m, oppositionCurrent: 6m,
                spiritFocusTier: 3, oppositionMaximum: 9m,
                initializeContext: allocated => { context = allocated; return Task.CompletedTask; },
                seedOriginalInputs: async fixture =>
                {
                    await SeedOriginalIntakeBaselinesAsync(fixture);
                    await SeedRetainedPressureGroupDataAsync(fixture, data);
                    var root = (await fixture.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!.AsObject();
                    root["activeConflict"]!["playerSideStrain"] = "strained";
                    await fixture.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
                },
                captureOriginalSnapshot: fixture => fixture.CaptureValidatedPendingSnapshotAsync(turn: 46,
                    currentRealm: "Chaos Sea", preGeneratedDices1d20: [4, 15, 3, 15],
                    sessionId: "session_" + name, requestId: "request_" + name));
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            var signed = await AssertPressureTransitionSignedInputsAsync(context, lease, data, trigger, name);
            var conflict = JsonNode.Parse(signed.ReadRequiredBytes(AfterlifeSpiritualConflictState.StatePath))!.AsObject();
            var active = conflict["activeConflict"]!.AsObject();
            Assert.Empty(active["exchangeLog"]!.AsArray());
            Assert.Equal(("strained", "clear", "contested"), (active["playerSideStrain"]!.GetValue<string>(),
                active["oppositionSideStrain"]!.GetValue<string>(), active["conflictPosition"]!.GetValue<string>()));
            Assert.Null(active["controlState"]); Assert.Null(active["tempoAdvantage"]);
            var e0 = SpiritualRetainedPressureGroupTestData.CreateExchange("exchange_pressure_transition_46_e0", 46,
                "strained", "fractured", 4, 15);
            e0["operationType"] = "guard";
            e0["summary"] = "Душа защищается от давления Хранителя.";
            e0["matchupAudit"]!["playerOperation"] = "guard";
            e0["matchupAudit"]!["primaryResolutionLane"] = "guard";
            e0["matchupAudit"]!["riskProfile"] = "safe_defense";
            e0["matchupAudit"]!["matchupRationale"] = "Защита смягчает входящее давление до одного шага надлома.";
            e0["incomingAction"]!["spiritualWoundTarget"] = new JsonObject
            {
                ["actorType"] = "player_soul", ["actorId"] = "player_soul", ["retraumaWoundRef"] = PressureTransitionWoundId
            };
            e0["actionCostAudit"]!["player"]!["operationType"] = "guard";
            e0["actionCostAudit"]!["player"]!["baseCost"] = 2;
            e0["actionCostAudit"]!["player"]!["effectiveCost"] = 7;
            e0["actionCostAudit"]!["player"]!["before"] = 10;
            e0["actionCostAudit"]!["player"]!["after"] = 3;
            e0["actionCostAudit"]!["opposition"]!["before"] = 6;
            e0["actionCostAudit"]!["opposition"]!["after"] = 3;
            var e1 = SpiritualRetainedPressureGroupTestData.CreateExchange(PressureTransitionOrdinaryId, 46,
                "fractured", "overwhelmed", 3, 15);
            e1["diceAudit"]!["diceUsed"]![0]!["sourceIndex"] = 2;
            e1["diceAudit"]!["diceUsed"]![1]!["sourceIndex"] = 3;
            AssertPressureTransitionExchangePremises(e0, e1, signed);
            active["exchangeLog"] = new JsonArray(e0.DeepClone(), e1.DeepClone());
            foreach (var pair in e1["after"]!.AsObject()) active[pair.Key] = pair.Value?.DeepClone();
            await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath,
                Encoding.UTF8.GetBytes(conflict.ToJsonString()));
            await context.FileSystem.WriteFileAtomicBytesAsync(lease, ProjectionNarrativePath, Encoding.UTF8.GetBytes(new JsonObject
            {
                ["response"] = PressureTransitionScene, ["timestamp"] = "2026-11-05T00:00:00Z"
            }.ToJsonString()));
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(lease));
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease));
            var definitions = Encoding.UTF8.GetString(signed.ReadRequiredBytes(ResourceMaterializationContract.DefinitionsPath));
            var copy = new PressureOrdinaryOwnerCopy(context,
                Assert.IsType<string>(context.FileSystem.ReadExistingSessionGeneration(lease)), e1, conflict, definitions);
            WritePressureOwnerTiming("transition_signed_preparation", name, started);
            using var capture = await BeginPressureOrdinaryOwnerAsync(copy, lease, name);
            var filesBeforeE0 = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
            var generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
            try
            {
                // Keep the original reader's ordinal-zero contract and reject the new positive-closed contour here.
                var original = await ReadPressureOwnerSnapshotAsync(capture, lease, name + "_ordinal_zero");
                var originalCounts = capture.ReadExistingStrainReuseCounts();
                Assert.Equal(0, originalCounts.ActualEvaluations);
                var originalWork = capture.ReadExistingPressureGroupComputationCounts();
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await capture.ReadClosedPressureComparisonProbeStateAsync(lease));
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await capture.AdmitLatestClosedWoundSourceAsync(lease, "player"));
                AssertPressureOrdinaryFrontiersEqual(original,
                    await ReadPressureOwnerSnapshotAsync(capture, lease, name + "_ordinal_zero_after_refusal"));
                Assert.Equal(originalCounts, capture.ReadExistingStrainReuseCounts());
                Assert.Equal(originalWork, capture.ReadExistingPressureGroupComputationCounts());
                AssertExistingPressureInputInert(context, lease, filesBeforeE0, generation);

                var closed = await MeasurePressureOwnerPhaseAsync("transition_e0_close", name,
                    () => capture.AdvanceNextResourceExchangeAsync(lease));
                AssertNoConflictFrameErrors(closed.Issues);
                var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(closed.Step?.Interval);
                Assert.Null(closed.Step!.PendingExchange); Assert.Null(closed.Step.PendingResource);
                Assert.Equal((0, "exchange_pressure_transition_46_e0"), (interval.Ordinal, interval.ExchangeId));
                var evidence = Assert.Single(capture.ReadClosedExchangeEvidence(lease));
                Assert.Equal((0, interval.ExchangeId), (evidence.Ordinal, evidence.ExchangeId));
                Assert.True(JsonNode.DeepEquals(e0, JsonNode.Parse(evidence.ExchangeJson)));
                Assert.Equal(new[] { 0, 1 }, evidence.DiceClaims.Select(row => row.SourceIndex));
                Assert.Equal(new[] { 4, 15 }, evidence.DiceClaims.Select(row => row.Value));
                var sourceCopy = Assert.Single(evidence.Sources);
                Assert.Equal(("player", "guardian:guardian_frame", "player_soul:player_soul", "pressure", PressureTransitionWoundId),
                    (sourceCopy.AffectedSide, sourceCopy.ActingActor, sourceCopy.AffectedActor, sourceCopy.Operation, sourceCopy.RetraumaWoundId));
                Assert.Equal((1, 2, 0, 13L, 2), (sourceCopy.Calculation.PreviousStrainRank, sourceCopy.Calculation.NewStrainRank,
                    sourceCopy.Calculation.ExtraJumpSteps, sourceCopy.Calculation.TraumaPressure, sourceCopy.Calculation.MaximumSeverityRank));
                Assert.Null(sourceCopy.StrainPreimageJson);
                var thirdActivation = Assert.Single(interval.EffectAfter.AcceptedActivations,
                    row => row.Activation.Stamp.Identity.EffectId == PressureTransitionEffectId);
                Assert.Equal(trigger == SpiritualPressureThirdGuardTrigger.TurnEnd
                        ? ("on_owner_turn_end", "owner_turn_end") : ("own_ap_spent", "resource_spent"),
                    (thirdActivation.Activation.Stamp.Identity.TriggerId, thirdActivation.Activation.Stamp.Identity.EventKind));
                Assert.False(thirdActivation.Activation.Stamp.ConsumesUse);
                Assert.Null(thirdActivation.Activation.Stamp.UsesBefore);
                Assert.Null(thirdActivation.Activation.UsesAfter);
                Assert.DoesNotContain(interval.EffectAfter.ReleasedReactions,
                    row => row.Reaction.EffectId == PressureTransitionEffectId);
                var beforeClosedProbeRefusal = capture.ReadExistingStrainReuseCounts();
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await capture.ReadExistingPressureOrdinaryProbeStateAsync(lease));
                Assert.Equal(beforeClosedProbeRefusal, capture.ReadExistingStrainReuseCounts());
                var admissionResult = await MeasurePressureOwnerPhaseAsync("transition_admission", name,
                    () => capture.AdmitLatestClosedWoundSourceAsync(lease, "player"));
                AssertNoConflictFrameErrors(admissionResult.Issues);
                var admission = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture.WoundSourceAdmission>(admissionResult.Admission);
                Assert.Same(interval, admission.Interval);
                Assert.NotSame(sourceCopy, admission.Source);
                Assert.Equal(sourceCopy, admission.Source);
                var retry = await capture.AdmitLatestClosedWoundSourceAsync(lease, "player");
                AssertNoConflictFrameErrors(retry.Issues); Assert.Same(admission, retry.Admission);
                var opportunity = await capture.ReadWoundOpportunityAsync(lease, admission);
                AssertNoConflictFrameErrors(opportunity.Issues);
                Assert.NotNull(opportunity.Opportunity);
                Assert.Equal(2, opportunity.Opportunity.MaximumSeverityRank);
                var target = opportunity.Opportunity.WorseningTarget!;
                Assert.Equal((PressureTransitionWoundId, 1), (target.Wound.WoundId, target.Wound.Severity.Rank));
                var decision = CreatePressureTransitionDecision(opportunity.Opportunity.PublicRef);
                var selectionIssues = new List<ValidationIssue>();
                var selection = await MeasurePressureOwnerPhaseAsync("transition_register_selection", name, async () =>
                    await ValidationService.SpiritualOriginalTurnCapture.WoundSelection.SelectAsync(capture, lease,
                        admission, decision, PressureTransitionScene, selectionIssues));
                AssertNoConflictFrameErrors(selectionIssues);
                Assert.NotNull(selection); Assert.True(selection.IsCurrentFor(admission.Draft));
                Assert.Equal((0L, 0L), (admission.Draft.WoundReadVersion, admission.Draft.DependencyCutVersion));
                Assert.False(admission.Draft.HasPendingWoundIntegration);
                // Actual E0 and admitted wound selection validate the real source; this counter is cumulative.
                var selectedCounts = capture.ReadExistingStrainReuseCounts();
                Assert.True(selectedCounts.ActualEvaluations > originalCounts.ActualEvaluations);
                var window = new PressureTransitionWindow(name, context, lease, capture, admission, selection,
                    interval, decision, e1, conflict, definitions, data);
                var beforeGuards = await CapturePressureTransitionBaselineAsync(window, "before_surface_guards");
                foreach (var side in new[] { "", "Player", "OPPOSITION", "opposition", "guardian" })
                {
                    try
                    {
                        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                            await capture.AdmitLatestClosedWoundSourceAsync(lease, side));
                    }
                    finally { await AssertPressureTransitionInertAsync(window, beforeGuards); }
                }
                try
                {
                    var detached = await capture.AdmitWoundSourceAsync(lease, interval, sourceCopy);
                    Assert.Null(detached.Admission);
                    Assert.Contains(detached.Issues, issue => issue.Code == "spiritual_wound_frontier_mismatch");
                }
                finally { await AssertPressureTransitionInertAsync(window, beforeGuards); }
                try
                {
                    var selectedRetry = await capture.AdmitLatestClosedWoundSourceAsync(lease, "player");
                    AssertNoConflictFrameErrors(selectedRetry.Issues);
                    Assert.Same(admission, selectedRetry.Admission);
                    var beforeSelectedProbeRefusal = capture.ReadExistingStrainReuseCounts();
                    await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                        await capture.ReadExistingPressureOrdinaryProbeStateAsync(lease));
                    Assert.Equal(beforeSelectedProbeRefusal, capture.ReadExistingStrainReuseCounts());
                    await AssertPressureTransitionForeignLeaseAsync(window);
                }
                finally { await AssertPressureTransitionInertAsync(window, beforeGuards); }
                var actualBeforeWork = capture.ReadExistingStrainReuseCounts().ActualEvaluations;
                await work(window);
                // Materialization and its exact retry each retain their fresh source validation.
                // Ordinary comparisons and diagnostic request/currentness reads do not evaluate actual work.
                Assert.Equal(actualBeforeWork + (expectedActualEvaluations ??
                    (trigger == SpiritualPressureThirdGuardTrigger.TurnEnd ? 1 : 0)),
                    capture.ReadExistingStrainReuseCounts().ActualEvaluations);
                Assert.Equal(!revokesCapture, capture.IsCurrentOwner);
                if (!revokesCapture) Assert.Single(capture.ReadClosedExchangeEvidence(lease));
                AssertPressureNoPrivatePair(context, lease);
            }
            finally
            {
                var disposalFiles = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                capture.Dispose();
                await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                    await capture.ReadClosedPressureComparisonProbeStateAsync(lease));
                await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                    await capture.AdmitLatestClosedWoundSourceAsync(lease, "player"));
                AssertExistingPressureInputInert(context, lease, disposalFiles, generation);
            }
        }
        finally
        {
            if (context is not null)
            {
                var cleanup = Stopwatch.GetTimestamp();
                await context.DisposeAsync();
                WritePressureOwnerTiming("transition_owned_cleanup", name, cleanup);
                Assert.False(Directory.Exists(context.RootPath));
            }
        }
    }

    /// <summary>Authors a rank-II worsening that preserves the independent retained pressure wound's classification and recovery mode.</summary>
    /// <param name="opportunityRef">The current original owner's admitted public opportunity reference.</param>
    /// <returns>A complete symbolic worsening decision with retained classification and blockers.</returns>
    private static JsonElement CreatePressureTransitionDecision(string opportunityRef)
    {
        var decision = JsonNode.Parse(OriginalSpiritualWoundDecision(opportunityRef,
            "spiritual_action_cost_burden", "guard", "player").GetRawText())!.AsObject();
        // This is worsening of the authored retained pressure wound, not a fresh-wound template.
        decision["proposal"]!["classification"] = new JsonObject
        {
            ["woundType"] = "Трещина духовной целостности",
            ["locationProfile"] = new JsonObject
            {
                ["kind"] = "spiritual_axis", ["readableLocus"] = "воля и духовное равновесие",
                ["authorityKind"] = "spiritual_axis", ["authorityRef"] = "will_and_balance", ["affectedSide"] = "self"
            }
        };
        decision["proposal"]!["severity"] = "II";
        decision["proposal"]!["recovery"]!["mode"] = "progressive";
        decision["proposal"]!["recovery"]!["blockers"] = new JsonArray();
        decision["proposal"]!["recovery"]!["currentStepThreshold"] = 4;
        var definitions = decision["proposal"]!["consequenceDefinitions"]!.AsArray();
        var extra = definitions[0]!.DeepClone();
        extra["definitionRef"] = "maneuver_burden";
        extra["definition"]!["definitionKey"] = "maneuver_burden";
        extra["definition"]!["stacking"]!["stackKey"] = "stack_maneuver_burden";
        extra["definition"]!["components"]![0]!["payload"]!["operation"] = "maneuver";
        definitions.Add(extra);
        return JsonSerializer.SerializeToElement(decision);
    }

    /// <summary>Checks the whole candidate's literal shape, joined states, dice, source arithmetic and costs before intake.</summary>
    private static void AssertPressureTransitionExchangePremises(JsonObject e0, JsonObject e1, PendingTurnSnapshotReadAuthority signed)
    {
        Assert.Equal(new[] { 4, 15, 3, 15 }, signed.AcceptedD20EventValues);
        Assert.True(JsonNode.DeepEquals(e0["after"], e1["before"]));
        Assert.Equal(("guard", "setback", "pressure", "guard", "safe_defense"),
            (e0["operationType"]!.GetValue<string>(), e0["outcome"]!.GetValue<string>(), e0["incomingAction"]!["operationType"]!.GetValue<string>(),
                e0["matchupAudit"]!["primaryResolutionLane"]!.GetValue<string>(), e0["matchupAudit"]!["riskProfile"]!.GetValue<string>()));
        foreach (var (exchange, ordinal, trauma, previous, next) in new[] { (e0, 0, 13L, "strained", "fractured"), (e1, 1, 16L, "fractured", "overwhelmed") })
        {
            var issues = new List<ValidationIssue>();
            ValidationService.ValidateRetainedSpiritualExchangeShape(exchange, "fixture.transition", issues);
            ValidationService.ValidateSpiritualWoundSourceActionShape(exchange, "fixture.transition", issues);
            ValidationService.ValidateRetainedSpiritualExchangeRules(exchange, issues);
            var evidence = new JsonObject { ["acceptedD20Values"] = new JsonArray(4, 15, 3, 15) };
            ValidationService.ValidateRetainedSpiritualDiceAudit(exchange, evidence, issues);
            AssertNoConflictFrameErrors(issues);
            Assert.Equal((46, 46), (exchange["turnNumber"]!.GetValue<int>(), exchange["exchangeAtTurn"]!.GetValue<int>()));
            var dice = exchange["diceAudit"]!;
            Assert.Equal(new[] { ordinal * 2, ordinal * 2 + 1 }, dice["diceUsed"]!.AsArray().Select(row => row!["sourceIndex"]!.GetValue<int>()));
            Assert.Equal(new[] { ordinal == 0 ? 4 : 3, 15 }, dice["diceUsed"]!.AsArray().Select(row => row!["value"]!.GetValue<int>()));
            Assert.Equal((ordinal == 0 ? -11 : -12, "decisive_opposition_success"),
                (dice["margin"]!.GetValue<int>(), dice["outcomeBand"]!.GetValue<string>()));
            Assert.Empty(dice["modifierBreakdown"]!["player"]!.AsArray());
            Assert.Empty(dice["modifierBreakdown"]!["opposition"]!.AsArray());
            Assert.Equal((previous, next), (exchange["before"]!["playerSideStrain"]!.GetValue<string>(), exchange["after"]!["playerSideStrain"]!.GetValue<string>()));
            foreach (var state in new[] { "before", "after" })
            {
                Assert.Equal("clear", exchange[state]!["oppositionSideStrain"]!.GetValue<string>());
                Assert.Equal("contested", exchange[state]!["conflictPosition"]!.GetValue<string>());
                Assert.Null(exchange[state]!["controlState"]); Assert.Null(exchange[state]!["tempoAdvantage"]);
            }
            var calculation = SpiritualWoundOpportunityMath.Calculate(new(ordinal == 0 ? 11 : 12, 0, 0, previous, next, "hostile", 4))!;
            Assert.Equal((trauma, 2, 0), (calculation.TraumaPressure, calculation.MaximumSeverityRank, calculation.ExtraJumpSteps));
            foreach (var side in new[] { "player", "opposition" })
            {
                var cost = exchange["actionCostAudit"]![side]!;
                var guard = ordinal == 0 && side == "player";
                Assert.Equal((guard ? "guard" : "pressure", guard ? 2 : 3, 1, 0, guard ? 7 : 3),
                    (cost["operationType"]!.GetValue<string>(), cost["baseCost"]!.GetValue<int>(), cost["minCost"]!.GetValue<int>(),
                        cost["artTier"]!.GetValue<int>(), cost["effectiveCost"]!.GetValue<int>()));
                Assert.Equal((ordinal == 0 ? side == "player" ? 10 : 6 : 3, ordinal == 0 ? 3 : 0, side == "player" ? 10 : 9),
                    (cost["before"]!.GetValue<int>(), cost["after"]!.GetValue<int>(), cost["max"]!.GetValue<int>()));
            }
        }
        Assert.Null(e0["spiritualWoundTarget"]); Assert.Null(e1["spiritualWoundTarget"]);
        Assert.Null(e1["incomingAction"]!["spiritualWoundTarget"]);
    }

    /// <summary>Pure reads themselves preserve all counters, full allocation journal and physical bytes/generation.</summary>
    private async Task<PressureTransitionBaseline> CapturePressureTransitionBaselineAsync(PressureTransitionWindow window, string phase)
    {
        var files = CaptureExactSpiritualFiles(window.Context.FileSystem.GameSessionPath);
        var generation = File.ReadAllBytes(window.Context.FileSystem.SessionGenerationPath);
        var counts = window.Capture.ReadExistingStrainReuseCounts();
        var work = window.Capture.ReadExistingPressureGroupComputationCounts();
        var state = await MeasurePressureOwnerPhaseAsync("transition_observe_" + phase, window.Name,
            () => window.Capture.ReadClosedPressureComparisonProbeStateAsync(window.Lease));
        Assert.Equal(counts, window.Capture.ReadExistingStrainReuseCounts());
        Assert.Equal(work, window.Capture.ReadExistingPressureGroupComputationCounts());
        AssertPressureTransitionClosedFrontier(window, state);
        AssertExistingPressureInputInert(window.Context, window.Lease, files, generation);
        return new(files, generation, state);
    }

    private async Task AssertPressureTransitionInertAsync(PressureTransitionWindow window, PressureTransitionBaseline baseline)
    {
        var after = await CapturePressureTransitionBaselineAsync(window, "inertness");
        Assert.Equal(baseline.State, after.State);
        AssertExistingPressureInputInert(window.Context, window.Lease, baseline.Files, baseline.Generation);
        AssertPressureNoPrivatePair(window.Context, window.Lease);
    }

    private static void AssertPressureTransitionClosedFrontier(PressureTransitionWindow window,
        ValidationService.SpiritualClosedPressureComparisonProbeState state)
    {
        Assert.Equal(1, state.Frontier.NextOrdinal);
        Assert.Equal(new ValidationService.SpiritualPressureProjectionInventory(0, 0, 0), state.Projections);
        Assert.Equal(window.Admission.Draft.WoundReadVersion, state.WoundVersion);
        Assert.Equal(window.Admission.Draft.DependencyCutVersion, state.DependencyCutVersion);
        Assert.Equal(window.Admission.SourceRevision, state.SourceRevision);
        Assert.False(window.Admission.Draft.HasPendingWoundIntegration);
        Assert.NotEmpty(state.CaptureStateJson); Assert.NotEmpty(state.DraftStateJson);
        Assert.StartsWith("sha256:", state.RoutingStateFingerprint);
        var source = JsonNode.Parse(state.Frontier.SourceStateJson)!;
        Assert.Single(source["sources"]!.AsArray()); Assert.Single(source["checkedExchanges"]!.AsArray());
        Assert.Equal(new[] { 0, 1 }, source["claimedDice"]!.AsArray().Select(row => row!.GetValue<int>()));
        Assert.Empty(source["pendingRequirements"]!.AsArray());
        Assert.Equal(window.Capture.ReadAllocationCursor(window.Lease), state.Frontier.AllocationCursor);
        Assert.True(JsonNode.DeepEquals(window.Capture.ReadAllocationJournal(window.Lease), JsonNode.Parse(state.Frontier.AllocationJournalJson)));
        var closed = Assert.Single(window.Capture.ReadClosedExchangeEvidence(window.Lease));
        Assert.Equal((0, window.Interval.ExchangeId), (closed.Ordinal, closed.ExchangeId));
        Assert.Equal(new[] { 0, 1 }, closed.DiceClaims.Select(row => row.SourceIndex));
        Assert.Equal(new[] { 4, 15 }, closed.DiceClaims.Select(row => row.Value));
        var balances = AfterlifeConflictActionPointProjectionService.Resolve(window.ResourceDefinitions,
            state.Frontier.ResourceStateJson, window.Conflict["activeConflict"]!.AsObject());
        AssertNoConflictFrameErrors(balances.Issues);
        Assert.Equal((3m, 10m, 3m, 9m), (balances.Projection!.Player.Current, balances.Projection.Player.Maximum,
            balances.Projection.Opposition.Current, balances.Projection.Opposition.Maximum));
    }

    private static async Task AssertPressureTransitionForeignLeaseAsync(PressureTransitionWindow window)
    {
        var foreign = await ResourceMaterializationTestContext.CreateAsync();
        try
        {
            await using var lease = await foreign.FileSystem.AcquireCanonicalWriteLeaseAsync();
            var files = CaptureExactSpiritualFiles(foreign.FileSystem.GameSessionPath);
            var generation = File.ReadAllBytes(foreign.FileSystem.SessionGenerationPath);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await window.Capture.ReadClosedPressureComparisonProbeStateAsync(lease));
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await window.Capture.AdmitLatestClosedWoundSourceAsync(lease, "player"));
            AssertExistingPressureInputInert(foreign, lease, files, generation);
        }
        finally
        {
            await foreign.DisposeAsync();
            Assert.False(Directory.Exists(foreign.RootPath));
        }
    }
}
