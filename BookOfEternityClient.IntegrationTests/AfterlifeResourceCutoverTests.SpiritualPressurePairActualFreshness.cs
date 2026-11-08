using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>A warm ordinary comparison never authorizes actual after the second physical parent disappears.</summary>
    [Fact]
    public Task SpiritualPressurePair_WarmSecondParentDeletionRejectsActual() =>
        AssertPressurePairWarmParentDeletionAsync(new("pair_actual_delete_second"));

    /// <summary>Reuses the actual freshness boundary with a complete pre-sign rank configuration, not relabeled retained bytes.</summary>
    private Task AssertPressurePairWarmParentDeletionAsync(PressureGroupCase spec) =>
        WithPressureGroupCaseAsync(spec, async window =>
        {
            var ordinary = PairActualOrdinaryTwoCalculation();
            await AssertPressurePairOrdinaryPremiseAsync(window, ordinary);
            var cold = window.Capture.ReadExistingStrainReuseCounts();
            var coldSources = window.Capture.ReadExistingPressureGroupComputationCounts();
            await AssertPressurePairOrdinaryPremiseAsync(window, ordinary);
            var counts = window.Capture.ReadExistingStrainReuseCounts();
            var sources = window.Capture.ReadExistingPressureGroupComputationCounts();
            Assert.Equal((cold.SignedParentReads, cold.OrdinaryEvaluations, cold.ActualEvaluations),
                (counts.SignedParentReads, counts.OrdinaryEvaluations, counts.ActualEvaluations));
            Assert.Equal(coldSources, sources);
            Assert.True(counts.FreshInputChecks > cold.FreshInputChecks);
            Assert.True(counts.FreshDependencyChecks > cold.FreshDependencyChecks);
            Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(1, 1, 1, 1, 2), sources.CurrentBatch);
            Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(1, 1, 1, 1, 2), sources.SignedBatch);
            Assert.Equal(1, sources.ResourceBatchComputations);
            var path = AfterlifeEntityProfileState.StatePath;
            var oldBytes = (await window.Copy.Context.FileSystem.ReadFileBytesAsync(window.Lease, path))!;
            var profiles = JsonNode.Parse(oldBytes)!.AsObject();
            var player = Assert.Single(profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray(), row =>
                row!["actorType"]!.GetValue<string>() == "player_soul" && row["actorId"]!.GetValue<string>() == "player_soul")!;
            var wounds = player["activeWounds"]!.AsArray();
            Assert.Equal(2, wounds.Count);
            Assert.Equal("wound_retained_pressure_owner_2", wounds[1]!["woundId"]!.GetValue<string>());
            wounds.RemoveAt(1);
            var changed = Encoding.UTF8.GetBytes(profiles.ToJsonString());
            Assert.False(oldBytes.AsSpan().SequenceEqual(changed));
            await window.Copy.Context.FileSystem.WriteFileAtomicBytesAsync(window.Lease, path, changed);
            var edited = CaptureExactSpiritualFiles(window.Copy.Context.FileSystem.GameSessionPath);

            var refused = await window.Capture.AdvanceNextResourceExchangeAsync(window.Lease);

            Assert.Null(refused.Step);
            AssertPressurePairInputDriftIssue(refused.Issues, path);
            AssertPressurePairNoNewComputations(window, counts, sources);
            await AssertPressurePairTerminalAfterInputDriftAsync(window, path, oldBytes, edited);
        });

    /// <summary>Both exact strain rows project while a selected parent's independently audited pressure cost and roll still apply.</summary>
    [Fact]
    public Task SpiritualPressurePair_SelectedParentCostAndRollRemainApplicable() =>
        WithPressureGroupCaseAsync(new("pair_actual_selected_siblings", PlayerBefore: 5, PlayerCost: 5,
            SelectedCost: true, PressureRoll: true, AdvanceExpected: true, CompletePressureRollAudit: true), async window =>
        {
            var admitted = await AssertPressurePairSuccessfulActualPremiseAsync(window);
            Assert.Equal(5m, Assert.Single(admitted.Prefix.AppliedTransitions,
                row => row.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor).AppliedAmount);
            Assert.Equal(3m, Assert.Single(admitted.Prefix.AppliedTransitions,
                row => row.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeConflictSide).AppliedAmount);
            Assert.True(window.Capture.IsCurrentOwner);
            AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        });

    /// <summary>A signed third cost-only pressure wound may compare but cannot obtain pair actual authority.</summary>
    [Fact]
    public Task SpiritualPressurePair_ThirdCostOnlyWoundRefusesActual() =>
        WithPressureGroupCaseAsync(new("pair_actual_third_cost", PlayerBefore: 4, PlayerCost: 4,
            OtherCost: true), async window =>
        {
            await AssertPressurePairOrdinaryPremiseAsync(window, PairActualOrdinaryTwoCalculation());
            await AssertPressureGroupActualGuardAsync(window,
                "actual existing pressure excludes another pressure wound source");
        });

    /// <summary>Each raw original input is rechecked after real pair admission, before missing-decision work can mask drift.</summary>
    [Theory]
    [InlineData(AfterlifeSpiritualConflictState.StatePath)]
    [InlineData(LiveTurnPreparationService.TurnRequestPath)]
    public Task SpiritualPressurePair_AdmittedOriginalInputDriftRevokesBeforeCompletion(string path) =>
        WithPressureGroupCaseAsync(new("pair_actual_admitted_drift_" +
            (path == AfterlifeSpiritualConflictState.StatePath ? "conflict" : "request"), AdvanceExpected: true), async window =>
        {
            await AssertPressurePairSuccessfulActualPremiseAsync(window);
            await AssertPressurePairCompletionInputDriftAsync(window, path);
        });

    /// <summary>Real none and twice-completed warm reduction do not allow a cached success to escape after raw input drift.</summary>
    [Theory]
    [InlineData(AfterlifeSpiritualConflictState.StatePath)]
    [InlineData(LiveTurnPreparationService.TurnRequestPath)]
    public Task SpiritualPressurePair_CachedWarmCompletionRejectsOriginalInputDrift(string path) =>
        WithPressureGroupCaseAsync(new("pair_actual_completed_drift_" +
            (path == AfterlifeSpiritualConflictState.StatePath ? "conflict" : "request"), AdvanceExpected: true), async window =>
        {
            var admitted = await AssertPressurePairSuccessfulActualPremiseAsync(window);
            var accepted = await window.Capture.AdmitLatestClosedWoundSourceAsync(window.Lease, "player");
            AssertNoConflictFrameErrors(accepted.Issues);
            var admission = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture.WoundSourceAdmission>(accepted.Admission);
            Assert.True(window.Capture.OwnsWoundAdmission(admission));
            var offered = await window.Capture.ReadWoundOpportunityAsync(window.Lease, admission);
            AssertNoConflictFrameErrors(offered.Issues);
            Assert.Null(offered.Satisfaction);
            var opportunity = Assert.IsType<WoundOpportunityAuthority>(offered.Opportunity);
            var none = await window.Capture.MaterializeWoundAsync(window.Lease, admission,
                JsonSerializer.SerializeToElement(new { opportunityRef = opportunity.PublicRef, decision = "none" }), null);
            AssertNoConflictFrameErrors(none.Issues);
            Assert.Null(none.Wound);
            var completed = await window.Capture.CompleteOrdinaryReductionAsync(window.Lease);
            AssertNoConflictFrameErrors(completed.Issues);
            Assert.True(completed.Success);
            var reduction = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(completed.Reduction);
            Assert.True(reduction.RequiresSatisfactionReceipt);
            Assert.False(reduction.HasLiveDecisionReceipt);
            Assert.False(reduction.HasLiveWoundWork);
            Assert.True(JsonNode.DeepEquals(admitted.ActualRoot,
                reduction.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(admitted.Prefix.StateJson),
                JsonNode.Parse(reduction.Resources.StateAfterImage!.ToCanonicalJson())));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(admitted.Prefix.HistoryJson),
                JsonNode.Parse(reduction.Resources.HistoryAfterImage!.ToCanonicalJson())));
            Assert.Empty(reduction.Resources.ReplayTransitions);
            Assert.Equal(admitted.Prefix.AppliedTransitions, reduction.Resources.AppliedTransitions);
            var effects = await window.Capture.CompleteEffectsAsync(window.Lease);
            AssertNoConflictFrameErrors(effects.Issues);
            Assert.True(effects.Success);
            Assert.Same(reduction.Effects, effects.Plan);
            Assert.True(effects.Plan!.IsAcceptedBoundaryComplete);
            Assert.False(effects.Plan.HasPressureConflictCarrierCollision());
            AssertPressurePairCompletedParents(window, effects.Plan, reduction.Resources);
            var detached = ReadPressurePairCompletedOrdinaryData(reduction);
            var beforeCached = window.Capture.ReadExistingStrainReuseCounts();
            var repeated = await window.Capture.CompleteOrdinaryReductionAsync(window.Lease);
            AssertNoConflictFrameErrors(repeated.Issues);
            Assert.True(repeated.Success);
            Assert.Same(completed, repeated);
            Assert.Same(reduction, repeated.Reduction);
            Assert.Equal(detached, ReadPressurePairCompletedOrdinaryData(repeated.Reduction!));
            Assert.True(window.Capture.ReadExistingStrainReuseCounts().FreshInputChecks > beforeCached.FreshInputChecks);
            Assert.True(window.Capture.IsCurrentOwner);
            AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
            AssertPressureNoPrivatePair(window.Copy.Context, window.Lease);
            await AssertPressurePairCompletionInputDriftAsync(window, path);
        });

    /// <summary>Independent full ordinary-II arithmetic shared by the new supported pair controls.</summary>
    private static SpiritualWoundCalculation PairActualOrdinaryTwoCalculation() =>
        new(new(8, 0, 0, "clear", "fractured", "hostile", 4), 0, 2, 1, 13, 2, 2, 4, true, 2);

    /// <summary>Requires the real complete ordinary comparison, exact literal calculation and unchanged unpaid owners.</summary>
    private async Task<ValidationService.SpiritualOrdinaryPressureComparisonData> AssertPressurePairOrdinaryPremiseAsync(
        PressureGroupWindow window, SpiritualWoundCalculation expected)
    {
        Assert.Equal(2, window.Spec.Parents);
        Assert.Equal(0, window.Frontier.NextOrdinal);
        var result = await ReadPressureGroupComparisonAsync(window);
        AssertPressureGroupComparison(window, result);
        var comparison = Assert.IsType<ValidationService.SpiritualOrdinaryPressureComparisonData>(result.Comparison);
        Assert.Equal(expected, comparison.OrdinaryCalculation);
        Assert.Equal(1, window.Capture.ReadExistingStrainReuseCounts().OrdinaryEvaluations);
        Assert.Equal(0, window.Capture.ReadExistingStrainReuseCounts().ActualEvaluations);
        Assert.True(window.Capture.IsCurrentOwner);
        return comparison;
    }

    /// <summary>Detached actual admission witnesses, never a replacement for current source or resource ownership.</summary>
    private sealed record PressurePairAdmittedData(JsonObject ActualRoot,
        AcceptedMechanicsPlanner.SpiritualResourceCandidatePrefix Prefix);

    /// <summary>
    /// Proves a genuine desired-success pair admission with complete independent exchange, dice, costs,
    /// v4 source, two-path full-root delta, current C1 images and exact ordinary/actual computation counts.
    /// </summary>
    private async Task<PressurePairAdmittedData> AssertPressurePairSuccessfulActualPremiseAsync(PressureGroupWindow window)
    {
        var siblingCost = window.Spec.Ranks?.Second == 4 ? 6 : 5;
        Assert.Equal(new PressureGroupCase(window.Spec.Name, PlayerBefore: window.Spec.CompletePressureRollAudit ? siblingCost : 3,
            PlayerCost: window.Spec.CompletePressureRollAudit ? siblingCost : 3, SelectedCost: window.Spec.CompletePressureRollAudit,
            PressureRoll: window.Spec.CompletePressureRollAudit, Ranks: window.Spec.Ranks, AdvanceExpected: true,
            CompletePressureRollAudit: window.Spec.CompletePressureRollAudit), window.Spec);
        var expectedOrdinary = ReadPressurePairAdmissionExchange("fractured");
        if (window.Spec.CompletePressureRollAudit)
        {
            expectedOrdinary["actionCostAudit"]!["player"]!["effectiveCost"] = siblingCost;
            expectedOrdinary["actionCostAudit"]!["player"]!["before"] = siblingCost;
            var dice = expectedOrdinary["diceAudit"]!;
            dice["diceUsed"]![0]!["selection"] = "selected";
            dice["diceUsed"]!.AsArray().Add(new JsonObject
            {
                ["side"] = "player", ["sourceIndex"] = 2, ["sides"] = 20,
                ["value"] = 8, ["selection"] = "discarded"
            });
            dice["rollMode"] = new JsonObject
            {
                ["player"] = new JsonObject
                {
                    ["effectiveMode"] = "disadvantage", ["advantageSources"] = new JsonArray(),
                    ["disadvantageSources"] = new JsonArray("Надлом второй раны мешает давлению души.")
                }
            };
        }
        Assert.True(JsonNode.DeepEquals(expectedOrdinary, window.Copy.Ordinary));
        var expectedActual = expectedOrdinary.DeepClone().AsObject();
        expectedActual["after"]!["playerSideStrain"] = "broken";
        var expectedActualCalculation = new SpiritualWoundCalculation(
            new(8, 0, 0, "clear", "broken", "hostile", 4), 0, 4, 3, 23, 4, 4, 4, true, 4);
        var comparison = await AssertPressurePairOrdinaryPremiseAsync(window, PairActualOrdinaryTwoCalculation());
        var before = window.Capture.ReadExistingStrainReuseCounts();
        var sources = window.Capture.ReadExistingPressureGroupComputationCounts();
        Assert.Equal((2, 1, 0), (before.SignedParentReads, before.OrdinaryEvaluations, before.ActualEvaluations));
        Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(1, 1, 1, 1, 2), sources.CurrentBatch);
        Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(1, 1, 1, 1, 2), sources.SignedBatch);
        Assert.Equal(1, sources.ResourceBatchComputations);
        var beforeC1 = window.Capture.ReadC1ImageInventory(window.Lease);
        Assert.Null(window.Capture.ReadExistingPressureActualRefusalObservation());
        await window.Capture.RequestExistingPressureActualRefusalObservationAsync(window.Lease);
        Assert.Null(window.Capture.ReadExistingPressureActualRefusalObservation());
        Assert.Equal(before, window.Capture.ReadExistingStrainReuseCounts());
        Assert.Equal(sources, window.Capture.ReadExistingPressureGroupComputationCounts());
        AssertPressureOrdinaryFrontiersEqual(window.Frontier,
            await window.Capture.ReadExistingPressureOrdinaryProbeStateAsync(window.Lease));

        var advanced = await MeasurePressureOwnerPhaseAsync("pair_actual_freshness_admission", window.Spec.Name,
            () => window.Capture.AdvanceNextResourceExchangeAsync(window.Lease));

        var succeeded = advanced.Issues.Count == 0 &&
            advanced.Step?.Interval is AcceptedMechanicsPlanner.SpiritualExchangeInterval;
        Exception? observationFailure = null;
        if (!succeeded)
            observationFailure = await Record.ExceptionAsync(async () =>
            {
                Assert.Null(advanced.Step);
                await AssertPressurePairRefusalObservationAsync(window, advanced.Issues, sources);
            });
        try
        {
            // Original Issues and unconditional desired success stay primary even if refusal observation fails.
            RequireGuardianJourneyIssues(window.Spec.Name, "pair_actual_freshness_admission", advanced.Issues);
            Assert.True(succeeded,
                "The pair with complete selected-parent cost and disadvantage evidence must admit one actual IV interval, paying the exact ordinary costs once.");
        }
        catch (Exception primary)
        {
            if (observationFailure is not null) RetainPressureFixtureFailure(primary, "pair_actual_refusal_observation", observationFailure);
            throw;
        }
        Assert.Null(observationFailure);
        AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        AssertPressureNoPrivatePair(window.Copy.Context, window.Lease);
        AssertNoConflictFrameErrors(advanced.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step?.Interval);
        Assert.Equal((0, "exchange_pressure_group_46"), (interval.Ordinal, interval.ExchangeId));
        Assert.True(window.Capture.IsCurrentOwner);
        Assert.Null(window.Capture.ReadExistingPressureActualRefusalObservation());
        Assert.Empty(interval.ReplayTransitions);
        Assert.Equal(2, interval.AppliedTransitions.Count);
        Assert.Equal(4, interval.Events.Count);
        foreach (var expected in comparison.ExpectedTransitions)
        {
            var spend = Assert.Single(interval.AppliedTransitions, row => row.Coordinate == expected.Coordinate);
            var maximum = expected.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor ? 10m : 9m;
            Assert.Equal(ResourceTransitionOperation.Spend, spend.Operation);
            Assert.Equal((46, expected.Amount, expected.Amount, expected.Before, expected.After, maximum, maximum),
                (spend.Turn, spend.RequestedAmount, spend.AppliedAmount, spend.BeforeState!.Current,
                    spend.AfterState!.Current, spend.BeforeState.Maximum, spend.AfterState.Maximum));
            var spent = Assert.Single(interval.Events, row => row.Coordinate == expected.Coordinate && row.EventKind == "resource_spent");
            Assert.Equal((46, expected.Before, expected.After, expected.Amount),
                (spent.Turn, spent.Before, spent.After, spent.AppliedAmount));
            Assert.Single(interval.Events, row => row.Coordinate == expected.Coordinate && row.EventKind == "resource_depleted");
        }
        var closed = Assert.Single(window.Capture.ReadClosedExchangeEvidence(window.Lease));
        Assert.Equal((interval.ConflictId, interval.ExchangeId, 0), (closed.ConflictId, closed.ExchangeId, closed.Ordinal));
        var claims = closed.DiceClaims.OrderBy(row => row.SourceIndex).ToArray();
        Assert.Equal(window.Spec.CompletePressureRollAudit ? new[] { 0, 1, 2 } : [0, 1], claims.Select(row => row.SourceIndex));
        Assert.Equal(window.Spec.CompletePressureRollAudit ? new[] { 4, 12, 8 } : [4, 12], claims.Select(row => row.Value));
        Assert.Equal(claims.Length, claims.Select(row => row.SourceIndex).Distinct().Count());
        Assert.True(JsonNode.DeepEquals(expectedActual, JsonNode.Parse(closed.ExchangeJson)));
        var source = Assert.Single(closed.Sources);
        Assert.Equal(("activeConflict.exchangeLog[0]", "player", "pressure", "guardian:guardian_frame", "player_soul:player_soul"),
            (source.Coordinate, source.AffectedSide, source.Operation, source.ActingActor, source.AffectedActor));
        Assert.Null(source.GuaranteedSeverityRank);
        Assert.Null(source.RetraumaWoundId);
        Assert.Equal(expectedActualCalculation, source.Calculation);
        Assert.True(JsonNode.DeepEquals(expectedActual, JsonNode.Parse(source.ExchangeJson)));
        AssertPressurePairV4Source(window, source, PairActualOrdinaryTwoCalculation(), expectedOrdinary);
        var expectedRoot = window.Copy.Conflict.DeepClone().AsObject();
        expectedRoot["activeConflict"]!["exchangeLog"]![0] = expectedActual.DeepClone();
        expectedRoot["activeConflict"]!["playerSideStrain"] = "broken";
        var prefix = window.Capture.ReadClosedResourceCandidatePrefix(window.Lease, interval);
        Assert.True(JsonNode.DeepEquals(expectedRoot, prefix.CompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]));
        var restored = prefix.CompanionAfterImages[AfterlifeSpiritualConflictState.StatePath].DeepClone();
        restored["activeConflict"]!["exchangeLog"]![0]!["after"]!["playerSideStrain"] = "fractured";
        restored["activeConflict"]!["playerSideStrain"] = "fractured";
        Assert.True(JsonNode.DeepEquals(window.Copy.Conflict, restored));
        var candidate = await window.Capture.ReadC1CandidateOwnerImagesAsync(window.Lease, interval);
        Assert.True(JsonNode.DeepEquals(expectedRoot, JsonNode.Parse(candidate[AfterlifeSpiritualConflictState.StatePath].Bytes!)));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(prefix.StateJson), JsonNode.Parse(candidate[ResourceMaterializationContract.StatePath].Bytes!)));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(prefix.HistoryJson), JsonNode.Parse(candidate[ResourceMaterializationContract.HistoryPath].Bytes!)));
        AssertPressurePairC1ParentsAndInventory(window, beforeC1, candidate);
        var projected = AfterlifeConflictActionPointProjectionService.Resolve(prefix.DefinitionsJson,
            prefix.StateJson, expectedRoot["activeConflict"]!.AsObject());
        AssertNoConflictFrameErrors(projected.Issues);
        Assert.Equal((0m, 0m), (projected.Projection!.Player.Current, projected.Projection.Opposition.Current));
        var after = window.Capture.ReadExistingStrainReuseCounts();
        Assert.Equal((2, 1, 1), (after.SignedParentReads, after.OrdinaryEvaluations, after.ActualEvaluations));
        Assert.True(after.FreshInputChecks > before.FreshInputChecks);
        Assert.True(after.FreshDependencyChecks > before.FreshDependencyChecks);
        Assert.Equal(sources with { ResourceBatchComputations = 2 }, window.Capture.ReadExistingPressureGroupComputationCounts());
        AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        AssertPressureNoPrivatePair(window.Copy.Context, window.Lease);
        await AssertPressurePairFirstOfferAsync(window, interval, source, PairActualOrdinaryTwoCalculation(), expectedOrdinary);
        return new(expectedRoot, prefix);
    }

    /// <summary>
    /// Reads the production C1 writer's detached first packet and checks its complete source witness and
    /// image inventories. This creates no C2 files, decision, C3 receipt or publication authority.
    /// Call only after the exact admission computation checkpoint, before any none/materialize decision.
    /// </summary>
    private async Task AssertPressurePairFirstOfferAsync(PressureGroupWindow window,
        AcceptedMechanicsPlanner.SpiritualExchangeInterval interval, ValidationService.PreparedSpiritualSource source,
        SpiritualWoundCalculation expectedOrdinaryCalculation, JsonObject expectedOrdinary)
    {
        var before = window.Capture.ReadExistingStrainReuseCounts();
        var beforeSources = window.Capture.ReadExistingPressureGroupComputationCounts();
        var admitted = await window.Capture.AdmitLatestClosedWoundSourceAsync(window.Lease, "player");
        AssertNoConflictFrameErrors(admitted.Issues);
        var admission = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture.WoundSourceAdmission>(admitted.Admission);
        Assert.True(window.Capture.OwnsWoundAdmission(admission));
        Assert.Same(interval, admission.Interval);
        Assert.Equal(source, admission.Source);
        var signed = window.Capture.ReadVerifiedSignedC1Origin(window.Lease);
        var inventory = window.Capture.ReadC1ImageInventory(window.Lease);
        var candidate = await window.Capture.ReadC1CandidateOwnerImagesAsync(window.Lease, interval);
        var offered = await window.Capture.ComposeC1FirstOfferAsync(window.Lease, interval);
        AssertNoConflictFrameErrors(offered.Issues);
        var pending = Assert.IsType<SpiritualWoundDecisionPendingState>(offered.Pending);
        var packet = JsonNode.Parse(SpiritualWoundDecisionPendingState.SerializeCanonical(pending))!["pending"]!.AsObject();
        var manifest = JsonNode.Parse(window.Files[LiveTurnPreparationService.PendingTurnSnapshotManifestPath])!;
        Assert.Equal(("session_" + window.Spec.Name, "request_" + window.Spec.Name, 46, "chaos_sea"),
            (packet["sessionId"]!.GetValue<string>(), packet["requestId"]!.GetValue<string>(),
                packet["turn"]!.GetValue<int>(), packet["realm"]!.GetValue<string>()));
        Assert.Equal(manifest["manifestPayloadHash"]!.GetValue<string>(), packet["snapshotToken"]!.GetValue<string>());
        Assert.Equal(signed.SnapshotFingerprint, packet["originalSnapshotFingerprint"]!.GetValue<string>());
        Assert.Equal((1, 0, 0), (packet["cursor"]!["exchangeOrdinal"]!.GetValue<int>(),
            packet["cursor"]!["waveOrdinal"]!.GetValue<int>(), packet["cursor"]!["nextSourceOrdinal"]!.GetValue<int>()));
        Assert.Empty(packet["stagedDecisions"]!.AsArray());
        var witness = Assert.Single(packet["sources"]!.AsArray())!.AsObject();
        var expectedActual = expectedOrdinary.DeepClone().AsObject();
        var expectedActualCalculation = ExpectedPressurePairActual(window.Spec);
        var actualStrain = expectedActualCalculation.Input.NewStrain!;
        expectedActual["after"]!["playerSideStrain"] = actualStrain;
        var actualRoot = window.Copy.Conflict.DeepClone().AsObject();
        actualRoot["activeConflict"]!["exchangeLog"]![0] = expectedActual.DeepClone();
        actualRoot["activeConflict"]!["playerSideStrain"] = actualStrain;
        Assert.Equal(expectedActualCalculation, SpiritualWoundSourceWitness.ValidateAndReadStrainData(witness, out var strain));
        Assert.NotNull(strain);
        Assert.Equal(4, strain.Version);
        Assert.Equal(expectedOrdinaryCalculation, strain.OrdinaryCalculation);
        Assert.Equal(expectedActualCalculation, strain.ActualCalculation);
        Assert.True(JsonNode.DeepEquals(expectedActual, strain.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(expectedOrdinary["after"], strain.ReadOrdinaryAfter(expectedActual)));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(source.StrainPreimageJson!), witness["strainPreimage"]));
        // The independent pre-sign envelopes were already compared with the owner-issued closed payload.
        Assert.Equal(window.ExpectedParentJson.Count, strain.Parents.Count);
        for (var index = 0; index < strain.Parents.Count; index++)
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(window.ExpectedParentJson[index]),
                JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(strain.Parents[index].ParentWound))));
        Assert.Equal((source.Coordinate, source.ConflictId, source.ExchangeId, "player", "pressure", 0, 0),
            (witness["coordinate"]!.GetValue<string>(), witness["conflictId"]!.GetValue<string>(),
                witness["exchangeId"]!.GetValue<string>(), witness["affectedSide"]!.GetValue<string>(),
                witness["operation"]!.GetValue<string>(), witness["sourceOrdinal"]!.GetValue<int>(), witness["exchangeOrdinal"]!.GetValue<int>()));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("{\"actorKind\":\"guardian\",\"actorId\":\"guardian_frame\"}"), witness["actingActor"]));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("{\"actorKind\":\"player_soul\",\"actorId\":\"player_soul\"}"), witness["affectedActor"]));
        var evidence = witness["turnEvidence"]!.AsObject();
        foreach (var field in new[] { "sessionId", "requestId", "snapshotToken", "turn", "realm", "originalSnapshotFingerprint", "bounds" })
            Assert.True(JsonNode.DeepEquals(packet[field], evidence[field]), field);
        var dice = window.Spec.CompletePressureRollAudit ? new[] { 4, 12, 8 } : [window.Spec.PlayerDie, 12];
        Assert.Equal((1, 2, dice.Length, inventory.RegisteredPaths.Count),
            (packet["bounds"]!["exchangeCount"]!.GetValue<int>(), packet["bounds"]!["sourceSlotCount"]!.GetValue<int>(),
                packet["bounds"]!["diceCount"]!.GetValue<int>(), packet["bounds"]!["imagePathCount"]!.GetValue<int>()));
        Assert.Equal(dice, evidence["acceptedD20Values"]!.AsArray().Select(row => row!.GetValue<int>()));
        Assert.Equal(Enumerable.Range(0, dice.Length), witness["selectedDiceIndices"]!.AsArray().Select(row => row!.GetValue<int>()));
        Assert.True(JsonNode.DeepEquals(packet["diceClaims"], evidence["diceClaims"]));
        Assert.Equal(dice, evidence["diceClaims"]!.AsArray().Select(row => row!["value"]!.GetValue<int>()));
        Assert.Equal(Enumerable.Range(0, dice.Length), evidence["diceClaims"]!.AsArray().Select(row => row!["sourceIndex"]!.GetValue<int>()));
        Assert.Equal(WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.spiritual_closed_resource_result", "1", interval.ConflictId, interval.ExchangeId, "0",
            JsonSerializer.Serialize(interval.AppliedTransitions), JsonSerializer.Serialize(interval.ReplayTransitions), JsonSerializer.Serialize(interval.Events)
        }), witness["resourceResultFingerprint"]!.GetValue<string>());
        Assert.Equal(WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.spiritual_closed_effect_prefix", "1",
            admission.Draft.ReadBasePlanPayloadFingerprint(), interval.EffectAfter.Fingerprint
        }), witness["effectPlanFingerprint"]!.GetValue<string>());
        Assert.Equal(SpiritualWoundStateJson.Hash(JsonNode.Parse(signed.Conflict.Bytes!)!.AsObject(), "closed_conflict"),
            witness["conflictBeforeFingerprint"]!.GetValue<string>());
        Assert.Equal(SpiritualWoundStateJson.Hash(actualRoot, "closed_conflict"), witness["conflictAfterFingerprint"]!.GetValue<string>());
        Assert.Equal(SpiritualWoundStateJson.Hash(new JsonObject { ["dangerMode"] = "hostile" }, "danger_declaration"),
            witness["dangerDeclarationFingerprint"]!.GetValue<string>());
        AssertPressurePairPacketImages(packet["beforeImages"]!.AsArray(), inventory.RegisteredPaths,
            inventory.BeforeImages, "original_before");
        var expectedCandidates = inventory.BeforeImages.ToDictionary(row => row.Key, row => row.Value, StringComparer.Ordinal);
        foreach (var row in candidate) expectedCandidates[row.Key] = row.Value;
        AssertPressurePairPacketImages(packet["candidateAfterImages"]!.AsArray(), inventory.RegisteredPaths,
            expectedCandidates, "candidate_after");
        var after = window.Capture.ReadExistingStrainReuseCounts();
        Assert.Equal(before.OrdinaryEvaluations, after.OrdinaryEvaluations);
        Assert.Equal(before.SignedParentReads, after.SignedParentReads);
        Assert.True(window.Capture.IsCurrentOwner);
        AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        AssertPressureNoPrivatePair(window.Copy.Context, window.Lease);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "pair_actual_detached_c1_source", window.Spec.Name,
            beforeOrdinaryEvaluations = before.OrdinaryEvaluations, afterOrdinaryEvaluations = after.OrdinaryEvaluations,
            beforeActualEvaluations = before.ActualEvaluations, afterActualEvaluations = after.ActualEvaluations,
            beforeFreshInputChecks = before.FreshInputChecks, afterFreshInputChecks = after.FreshInputChecks,
            beforeFreshDependencyChecks = before.FreshDependencyChecks, afterFreshDependencyChecks = after.FreshDependencyChecks,
            beforeSources, afterSources = window.Capture.ReadExistingPressureGroupComputationCounts(),
            physicalInputInert = true, privatePairAbsent = true, publicationQualified = false }));
    }

    /// <summary>Checks every closed C1 image row's role, presence, exact bytes and fingerprint against the owner inventory.</summary>
    private static void AssertPressurePairPacketImages(JsonArray rows, IReadOnlyList<string> registered,
        IReadOnlyDictionary<string, CanonicalBeforeImage> expected, string role)
    {
        Assert.Equal(registered.OrderBy(path => path, StringComparer.Ordinal),
            rows.Select(row => row!["path"]!.GetValue<string>()).OrderBy(path => path, StringComparer.Ordinal));
        foreach (var path in registered)
        {
            var row = Assert.Single(rows, item => item!["path"]!.GetValue<string>() == path)!;
            var image = expected[path];
            Assert.Equal(role, row["role"]!.GetValue<string>());
            Assert.Equal(image.Existed, row["existed"]!.GetValue<bool>());
            Assert.Equal(image.Bytes is null ? null : Convert.ToBase64String(image.Bytes), row["contentBase64"]?.GetValue<string>());
            Assert.Equal(image.Fingerprint, row["contentFingerprint"]!.GetValue<string>());
        }
    }

    /// <summary>Checks frozen registered images, both complete parents and independently expected full C1 carrier roots.</summary>
    private static void AssertPressurePairC1ParentsAndInventory(PressureGroupWindow window,
        ValidationService.SpiritualC1ImageInventory before,
        IReadOnlyDictionary<string, CanonicalBeforeImage> candidate)
    {
        var after = window.Capture.ReadC1ImageInventory(window.Lease);
        Assert.Equal(before.RegisteredPaths, after.RegisteredPaths);
        Assert.Equal(before.BeforeImages.Keys.OrderBy(path => path, StringComparer.Ordinal),
            after.BeforeImages.Keys.OrderBy(path => path, StringComparer.Ordinal));
        foreach (var path in before.RegisteredPaths)
        {
            Assert.True(before.BeforeImages.ContainsKey(path), path);
            Assert.Equal(before.BeforeImages[path].Existed, after.BeforeImages[path].Existed);
            Assert.Equal(before.BeforeImages[path].Bytes, after.BeforeImages[path].Bytes);
            Assert.Equal(before.BeforeImages[path].Fingerprint, after.BeforeImages[path].Fingerprint);
        }
        var profiles = JsonNode.Parse(candidate[AfterlifeEntityProfileState.StatePath].Bytes!)!;
        var player = Assert.Single(profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray(), row =>
            row!["actorType"]!.GetValue<string>() == "player_soul" && row["actorId"]!.GetValue<string>() == "player_soul")!;
        Assert.Equal(2, player["activeWounds"]!.AsArray().Count);
        foreach (var expectedJson in window.ExpectedParentJson)
        {
            var expected = JsonNode.Parse(expectedJson)!;
            Assert.True(JsonNode.DeepEquals(expected, Assert.Single(player["activeWounds"]!.AsArray(), row =>
                row!["woundId"]!.GetValue<string>() == expected["woundId"]!.GetValue<string>())));
        }
        foreach (var path in new[] { AfterlifeEntityProfileState.StatePath, WoundIdentityState.StatePath,
                     WoundHistoryState.HistoryPath, EffectIdentityState.StatePath })
        {
            // Physical input remains unpublished. Accepted profile canonicalization supplies schema 1
            // and empty guardian effect/wound collections; every other complete field remains unchanged.
            var baseline = window.Files.Single(row => row.Key.Replace('\\', '/') == path).Value;
            var expectedRoot = JsonNode.Parse(baseline)!.DeepClone();
            if (path == AfterlifeEntityProfileState.StatePath)
            {
                Assert.False(expectedRoot.AsObject().ContainsKey("schemaVersion"));
                expectedRoot["schemaVersion"] = 1;
                var expectedProfiles = expectedRoot[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray();
                Assert.Equal(2, expectedProfiles.Count);
                var expectedPlayer = Assert.Single(expectedProfiles, row =>
                    row!["actorType"]!.GetValue<string>() == "player_soul" && row["actorId"]!.GetValue<string>() == "player_soul")!;
                Assert.Equal("Chaos Sea", expectedPlayer["realm"]!.GetValue<string>());
                var expectedGuardian = Assert.Single(expectedProfiles, row =>
                    row!["actorType"]!.GetValue<string>() == "guardian" && row["actorId"]!.GetValue<string>() == "guardian_frame")!.AsObject();
                Assert.Equal("Chaos Sea", expectedGuardian["realm"]!.GetValue<string>());
                Assert.False(expectedGuardian.ContainsKey("activeWounds"));
                Assert.False(expectedGuardian.ContainsKey("activeEffects"));
                expectedGuardian["activeWounds"] = new JsonArray();
                expectedGuardian["activeEffects"] = new JsonArray();
            }
            Assert.True(candidate[path].Existed);
            var comparisonMessage = path == AfterlifeEntityProfileState.StatePath
                ? CapturePressurePairC1ProfileDiagnostic(window.Spec.Name, path, baseline, expectedRoot, candidate[path].Bytes!)
                : path;
            Assert.True(JsonNode.DeepEquals(expectedRoot, JsonNode.Parse(candidate[path].Bytes!)), comparisonMessage);
        }
    }

    /// <summary>Preserves exact comparison operands outside the checkout without changing either operand or the oracle.</summary>
    /// <param name="label">Independent retained-DATA case label.</param>
    /// <param name="path">Canonical profile path compared by the unchanged owner test.</param>
    /// <param name="baseline">Exact unpublished physical bytes retained before actual admission.</param>
    /// <param name="expected">Independently authored expected root, never modified here.</param>
    /// <param name="actualBytes">Exact bytes returned by the real C1 owner, never modified here.</param>
    /// <returns>The canonical path, external manifest location and complete structural differences for failure output.</returns>
    private static string CapturePressurePairC1ProfileDiagnostic(string label, string path,
        byte[] baseline, JsonNode expected, byte[] actualBytes)
    {
        var configured = Environment.GetEnvironmentVariable("BOE_TEST_DIAGNOSTIC_ROOT");
        if (configured is null)
            throw new InvalidDataException("C1 profile diagnosis requires an explicit external diagnostic root.");
        var root = TestDiagnosticPaths.ResolveRoot(configured);
        var relative = Path.GetRelativePath(TestRepoPaths.RepoRoot, root);
        if (relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !Path.IsPathRooted(relative))
            throw new InvalidDataException("C1 profile diagnosis must remain outside the checkout.");
        var directory = Path.Combine(root, "c1-profile-comparison", Guid.NewGuid().ToString("N"));
        if (Directory.Exists(directory) || File.Exists(directory))
            throw new IOException("C1 profile diagnostic target already exists; no overwrite or retry.");
        Directory.CreateDirectory(directory);
        var actual = JsonNode.Parse(actualBytes);
        var differences = new JsonArray();
        AppendPressurePairJsonDifferences(expected, true, actual, true, "", differences);
        var baselineDifferences = new JsonArray();
        AppendPressurePairJsonDifferences(JsonNode.Parse(baseline), true, actual, true, "", baselineDifferences);
        var documents = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["physical-baseline.json"] = baseline,
            ["expected.json"] = Encoding.UTF8.GetBytes(expected.ToJsonString()),
            ["actual.json"] = actualBytes,
            ["differences.json"] = Encoding.UTF8.GetBytes(new JsonObject
            {
                ["expectedToActual"] = differences.DeepClone(),
                ["baselineToActual"] = baselineDifferences
            }.ToJsonString())
        };
        var manifest = new JsonObject
        {
            ["case"] = label, ["canonicalPath"] = path,
            ["captureKind"] = "real_owner_c1_comparison_operands",
            ["comparisonEqual"] = JsonNode.DeepEquals(expected, actual), ["files"] = new JsonArray()
        };
        foreach (var pair in documents)
        {
            var output = Path.Combine(directory, pair.Key);
            using (var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.Write(pair.Value);
            manifest["files"]!.AsArray().Add(new JsonObject
            {
                ["name"] = pair.Key, ["bytes"] = pair.Value.Length,
                ["sha256"] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pair.Value)).ToLowerInvariant()
            });
        }
        var manifestPath = Path.Combine(directory, "manifest.json");
        using (var stream = new FileStream(manifestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            stream.Write(Encoding.UTF8.GetBytes(manifest.ToJsonString()));
        return path + "\nDiagnostic manifest: " + manifestPath + "\nExpected-to-actual differences: " + differences.ToJsonString();
    }

    /// <summary>Records every differing JSON pointer with explicit presence, type and complete values; never normalizes DATA.</summary>
    private static void AppendPressurePairJsonDifferences(JsonNode? expected, bool expectedPresent,
        JsonNode? actual, bool actualPresent, string pointer, JsonArray differences)
    {
        if (expectedPresent == actualPresent && JsonNode.DeepEquals(expected, actual))
            return;
        if (expectedPresent && actualPresent && expected is JsonObject expectedObject && actual is JsonObject actualObject)
        {
            foreach (var key in expectedObject.Select(row => row.Key).Union(actualObject.Select(row => row.Key), StringComparer.Ordinal)
                         .OrderBy(key => key, StringComparer.Ordinal))
            {
                var hasExpected = expectedObject.TryGetPropertyValue(key, out var expectedValue);
                var hasActual = actualObject.TryGetPropertyValue(key, out var actualValue);
                AppendPressurePairJsonDifferences(expectedValue, hasExpected, actualValue, hasActual,
                    pointer + "/" + key.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal), differences);
            }
            return;
        }
        if (expectedPresent && actualPresent && expected is JsonArray expectedArray && actual is JsonArray actualArray)
        {
            if (expectedArray.Count != actualArray.Count)
                differences.Add(new JsonObject { ["path"] = pointer, ["kind"] = "array_length",
                    ["expectedLength"] = expectedArray.Count, ["actualLength"] = actualArray.Count });
            for (var index = 0; index < Math.Max(expectedArray.Count, actualArray.Count); index++)
                AppendPressurePairJsonDifferences(index < expectedArray.Count ? expectedArray[index] : null, index < expectedArray.Count,
                    index < actualArray.Count ? actualArray[index] : null, index < actualArray.Count, pointer + "/" + index, differences);
            return;
        }
        differences.Add(new JsonObject
        {
            ["path"] = pointer, ["kind"] = "value_or_presence", ["expectedPresent"] = expectedPresent, ["actualPresent"] = actualPresent,
            ["expectedType"] = expectedPresent ? expected?.GetValueKind().ToString() ?? "Null" : "missing",
            ["actualType"] = actualPresent ? actual?.GetValueKind().ToString() ?? "Null" : "missing",
            ["expectedValue"] = expected?.DeepClone(), ["actualValue"] = actual?.DeepClone()
        });
    }

    /// <summary>Checks unchanged parent wounds plus complete effect-plan owner/trigger completion without fabricating C3.</summary>
    private static void AssertPressurePairCompletedParents(PressureGroupWindow window, EffectAcceptedTurnPlan effects,
        AcceptedMechanicsResourcePlanningResult resources)
    {
        Assert.Equal(2, window.ExpectedParentJson.Count);
        var beforeIdentity = JsonNode.Parse(window.Files[EffectIdentityState.StatePath])!.AsObject();
        var identity = effects.IdentityIndexAfterImage;
        var restoredIdentity = identity.DeepClone().AsObject();
        var baselineProfiles = JsonNode.Parse(window.Files[AfterlifeEntityProfileState.StatePath])!;
        var baselinePlayer = Assert.Single(baselineProfiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray(), row =>
            row!["actorType"]!.GetValue<string>() == "player_soul" && row["actorId"]!.GetValue<string>() == "player_soul")!;
        var transcript = Assert.IsType<AcceptedEffectBoundaryTranscript>(resources.EffectBoundaryTranscript);
        var executions = resources.ResourceTriggerExecutions;
        var acceptedActivations = transcript.AcceptedActivations;
        var profiles = effects.CarrierAfterImages.TryGetValue(AfterlifeEntityProfileState.StatePath, out var changed)
            ? changed : effects.AcceptedCarrierBaselines.AfterlifeProfiles!;
        var player = Assert.Single(profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray(), row =>
            row!["actorType"]!.GetValue<string>() == "player_soul" && row["actorId"]!.GetValue<string>() == "player_soul")!;
        Assert.Equal(2, player["activeWounds"]!.AsArray().Count);
        foreach (var expectedJson in window.ExpectedParentJson)
        {
            var expected = JsonNode.Parse(expectedJson)!;
            Assert.True(JsonNode.DeepEquals(expected, Assert.Single(player["activeWounds"]!.AsArray(), row =>
                row!["woundId"]!.GetValue<string>() == expected["woundId"]!.GetValue<string>())));
            var root = expected["consequences"]!["ownedEffectSources"]!["rootBindings"]![0]!["effectId"]!.GetValue<string>();
            var effect = Assert.Single(player["activeEffects"]!.AsArray(), row => row!["effectId"]!.GetValue<string>() == root)!;
            Assert.Equal("active", effect["state"]!.GetValue<string>());
            Assert.Equal(46, effect["chronology"]!["lastTransitionTurn"]!.GetValue<int>());
            var baselineEffect = Assert.Single(baselinePlayer["activeEffects"]!.AsArray(), row => row!["effectId"]!.GetValue<string>() == root)!;
            var restoredEffect = effect.DeepClone();
            restoredEffect["chronology"] = baselineEffect["chronology"]!.DeepClone();
            Assert.True(JsonNode.DeepEquals(baselineEffect, restoredEffect));
            var entry = Assert.Single(identity["entries"]!.AsArray(), row => row!["effectId"]!.GetValue<string>() == root)!;
            var priorEntry = Assert.Single(beforeIdentity["entries"]!.AsArray(), row => row!["effectId"]!.GetValue<string>() == root)!;
            var transitions = entry["transitions"]!.AsArray();
            Assert.Equal(priorEntry["transitions"]!.AsArray().Count + 1, transitions.Count);
            for (var ordinal = 0; ordinal < transitions.Count - 1; ordinal++)
                Assert.True(JsonNode.DeepEquals(priorEntry["transitions"]![ordinal], transitions[ordinal]));
            var last = transitions[^1]!;
            Assert.Equal(("trigger", 46), (last["kind"]!.GetValue<string>(), last["turn"]!.GetValue<int>()));
            Assert.Equal(last["transitionId"]!.GetValue<string>(), effect["chronology"]!["lastTransitionId"]!.GetValue<string>());
            var execution = Assert.Single(executions, row => row.EffectId == root);
            Assert.Equal(("on_owner_turn_end", "owner_turn_end"), (execution.TriggerId, execution.EventKind));
            Assert.Null(execution.RemainingUseBudget);
            Assert.Equal(execution.EventRef, last["eventRef"]!.GetValue<string>());
            var accepted = Assert.Single(acceptedActivations, row => row.Activation.Stamp.Identity.EffectId == root);
            Assert.Equal(new EffectActivationCandidateIdentity(root, execution.TriggerId, execution.EventKind,
                execution.EventRef, execution.TriggerEventRef!), accepted.Activation.Stamp.Identity);
            Assert.False(accepted.Activation.Stamp.ConsumesUse);
            Assert.Null(accepted.Activation.Stamp.UsesBefore);
            Assert.Null(accepted.Activation.UsesAfter);
            var restoredEntry = Assert.Single(restoredIdentity["entries"]!.AsArray(), row => row!["effectId"]!.GetValue<string>() == root)!;
            restoredEntry["transitions"] = priorEntry["transitions"]!.DeepClone();
        }
        Assert.True(JsonNode.DeepEquals(beforeIdentity, restoredIdentity));
    }

    /// <summary>Captures full detached resources, companions and effect images to compare a cached warm result.</summary>
    private static string ReadPressurePairCompletedOrdinaryData(AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction reduction) =>
        JsonSerializer.Serialize(new
        {
            reduction.InputFingerprint, reduction.RequiresSatisfactionReceipt, reduction.HasLiveDecisionReceipt, reduction.HasLiveWoundWork,
            state = reduction.Resources.StateAfterImage!.ToCanonicalJson(),
            history = reduction.Resources.HistoryAfterImage!.ToCanonicalJson(),
            reduction.Resources.Events, reduction.Resources.AppliedTransitions, reduction.Resources.ReplayTransitions,
            reduction.Resources.ResourceTriggerExecutions,
            transcriptFingerprint = reduction.Resources.EffectBoundaryTranscript!.Fingerprint,
            companions = reduction.OwnerCompanionAfterImages.OrderBy(row => row.Key, StringComparer.Ordinal)
                .Select(row => new { path = row.Key, json = row.Value.ToJsonString() }).ToArray(),
            effects = reduction.Effects!.CarrierAfterImages.OrderBy(row => row.Key, StringComparer.Ordinal)
                .Select(row => new { path = row.Key, json = row.Value.ToJsonString() }).ToArray(),
            identity = reduction.Effects.IdentityIndexAfterImage.ToJsonString(),
            effectFingerprint = WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(reduction.Effects)
        });

    /// <summary>Mutates exactly one raw input only after admission/completion, then checks terminal rejection before any computation.</summary>
    private async Task AssertPressurePairCompletionInputDriftAsync(PressureGroupWindow window, string path)
    {
        var counts = window.Capture.ReadExistingStrainReuseCounts();
        var sources = window.Capture.ReadExistingPressureGroupComputationCounts();
        var oldBytes = (await window.Copy.Context.FileSystem.ReadFileBytesAsync(window.Lease, path))!;
        await window.Copy.Context.FileSystem.WriteFileAtomicBytesAsync(window.Lease, path,
            oldBytes.Concat(Encoding.UTF8.GetBytes(" \n")).ToArray());
        var edited = CaptureExactSpiritualFiles(window.Copy.Context.FileSystem.GameSessionPath);
        var refused = await window.Capture.CompleteOrdinaryReductionAsync(window.Lease);
        Assert.False(refused.Success);
        Assert.Null(refused.Reduction);
        AssertPressurePairInputDriftIssue(refused.Issues, path);
        AssertPressurePairNoNewComputations(window, counts, sources);
        await AssertPressurePairTerminalAfterInputDriftAsync(window, path, oldBytes, edited);
    }

    /// <summary>Requires the exact changed-input code/path rather than a missing decision, stale cache or unrelated owner refusal.</summary>
    private static void AssertPressurePairInputDriftIssue(IReadOnlyList<ValidationIssue> issues, string path)
    {
        var issue = Assert.Single(issues);
        Assert.Equal("spiritual_original_input_changed", issue.Code);
        Assert.Equal(path, issue.FilePath);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
    }

    /// <summary>Freshness refusal adds no evaluator, signed-view or resource computation after the intentional edit.</summary>
    private static void AssertPressurePairNoNewComputations(PressureGroupWindow window,
        (int Queries, int SignedParentReads, int OrdinaryEvaluations, int ActualEvaluations, int FreshInputChecks, int FreshDependencyChecks) before,
        ValidationService.SpiritualPressureGroupComputationCounts sources)
    {
        var after = window.Capture.ReadExistingStrainReuseCounts();
        Assert.Equal((before.SignedParentReads, before.OrdinaryEvaluations, before.ActualEvaluations),
            (after.SignedParentReads, after.OrdinaryEvaluations, after.ActualEvaluations));
        Assert.Equal(sources, window.Capture.ReadExistingPressureGroupComputationCounts());
    }

    /// <summary>Preserves edited bytes/absence/generation and proves restored original bytes cannot revive the disposed owner.</summary>
    private static async Task AssertPressurePairTerminalAfterInputDriftAsync(PressureGroupWindow window,
        string path, byte[] original, IReadOnlyDictionary<string, byte[]> edited)
    {
        Assert.False(window.Capture.IsCurrentOwner);
        AssertExistingPressureInputInert(window.Copy.Context, window.Lease, edited, window.Generation);
        AssertPressureNoPrivatePair(window.Copy.Context, window.Lease);
        Assert.Throws<ObjectDisposedException>(() => window.Capture.ReadClosedExchangeEvidence(window.Lease));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await window.Capture.CompleteOrdinaryReductionAsync(window.Lease));
        await window.Copy.Context.FileSystem.WriteFileAtomicBytesAsync(window.Lease, path, original);
        AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await window.Capture.CompleteOrdinaryReductionAsync(window.Lease));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await window.Capture.AdvanceNextResourceExchangeAsync(window.Lease));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await window.Capture.ReadExistingPressureOrdinaryComparisonAsync(window.Lease));
        Assert.False(window.Capture.IsCurrentOwner);
        AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        AssertPressureNoPrivatePair(window.Copy.Context, window.Lease);
    }
}
