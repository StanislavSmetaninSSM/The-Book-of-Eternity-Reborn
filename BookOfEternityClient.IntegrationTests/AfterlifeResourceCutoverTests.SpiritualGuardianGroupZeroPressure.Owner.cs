using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Exercises the actual owner, ordinary DATA reuse, one actual advance, C1 no offer and first C2 no_offer for the complete low-margin N2 group.</summary>
    /// <param name="window">Genuine independent signed/current fixture with original tiers and exact ordinary raw input already proved.</param>
    /// <returns>The same already checked owned interval; returning it adds no computation or authority.</returns>
    private async Task<AcceptedMechanicsPlanner.SpiritualExchangeInterval> AssertGuardianGroupZeroPressureActualAndC1Async(GuardianGroupZeroPressureWindow window)
    {
        var capture = window.Capture; var lease = window.Lease;
        var before = capture.ReadExistingStrainReuseCounts(); var sourceBefore = capture.ReadExistingPressureGroupComputationCounts();
        Assert.Equal((0, 0, 0, 0), (before.Queries, before.SignedParentReads, before.OrdinaryEvaluations, before.ActualEvaluations));
        Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(0, 0, 0, 0, 0), sourceBefore.CurrentBatch);
        Assert.Equal(sourceBefore.CurrentBatch, sourceBefore.SignedBatch); Assert.Equal(0, sourceBefore.ResourceBatchComputations);
        var comparison = await Query("cold");
        var cold = capture.ReadExistingStrainReuseCounts(); var sourceCold = capture.ReadExistingPressureGroupComputationCounts();
        Assert.Equal((before.Queries + 1, before.SignedParentReads + 2, before.OrdinaryEvaluations + 1, before.ActualEvaluations),
            (cold.Queries, cold.SignedParentReads, cold.OrdinaryEvaluations, cold.ActualEvaluations));
        Assert.True(cold.FreshInputChecks > before.FreshInputChecks); Assert.True(cold.FreshDependencyChecks > before.FreshDependencyChecks);
        Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(1, 1, 1, 1, 2), sourceCold.CurrentBatch);
        Assert.Equal(sourceCold.CurrentBatch, sourceCold.SignedBatch); Assert.Equal(1, sourceCold.ResourceBatchComputations);
        var detached = comparison.ReadOrdinaryExchange();
        detached[SpiritualWoundFailedAction.Property]!["actorId"] = "guardian_unrelated";
        detached["after"]!["oppositionSideStrain"] = "broken";
        var repeated = await Query("warm");
        var warm = capture.ReadExistingStrainReuseCounts();
        Assert.Equal(cold.Queries + 1, warm.Queries);
        Assert.Equal((cold.SignedParentReads, cold.OrdinaryEvaluations, cold.ActualEvaluations),
            (warm.SignedParentReads, warm.OrdinaryEvaluations, warm.ActualEvaluations));
        Assert.True(warm.FreshInputChecks > cold.FreshInputChecks); Assert.True(warm.FreshDependencyChecks > cold.FreshDependencyChecks);
        Assert.Equal(sourceCold, capture.ReadExistingPressureGroupComputationCounts());
        Assert.Equal(comparison.OrdinaryExchangeJson, repeated.OrdinaryExchangeJson);
        Assert.Equal(comparison.OrdinaryCalculation, repeated.OrdinaryCalculation);
        Assert.Equal(comparison.ExpectedTransitions, repeated.ExpectedTransitions);
        Assert.Equal((comparison.RawInputFingerprint, comparison.ExchangePointer), (repeated.RawInputFingerprint, repeated.ExchangePointer));
        var inventory = capture.ReadC1ImageInventory(lease);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_zero_lawful_ordinary_before_actual", window.Name, window.CapturePhase,
            parentRanks = new[] { 3, 4 }, window.Data.Resilience, ordinary = window.Data.OrdinaryCalculation, actual = window.Data.ActualCalculation,
            comparison.OrdinaryExchangeJson, comparison.RawInputFingerprint, comparison.ExchangePointer,
            counters = ReadGroupZeroMeasurementCounters(capture) }));
        var advanced = await MeasureGuardianGroupZeroPhaseAsync("guardian_group_zero_actual_admission", window.Name,
            () => capture.AdvanceNextResourceExchangeAsync(lease));
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_zero_actual_desired_success", window.Name, window.CapturePhase,
            advanced.Issues, intervalReturned = advanced.Step?.Interval is not null,
            counters = ReadGroupZeroMeasurementCounters(capture) }));
        RequireGuardianJourneyIssues(window.Name, "guardian_group_zero_actual_admission", advanced.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step?.Interval);
        Assert.True(capture.IsCurrentOwner); Assert.Equal((0, "exchange_guardian_group_46"), (interval.Ordinal, interval.ExchangeId));
        Assert.Empty(interval.ReplayTransitions); Assert.Equal(2, interval.AppliedTransitions.Count);
        foreach (var expected in comparison.ExpectedTransitions)
        {
            var spend = Assert.Single(interval.AppliedTransitions, row => row.Coordinate == expected.Coordinate);
            var maximum = expected.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor ? 10m : 9m;
            Assert.Equal(ResourceTransitionOperation.Spend, spend.Operation);
            Assert.Equal((46, 3m, 3m, 3m, 0m, maximum, maximum),
                (spend.Turn, spend.RequestedAmount, spend.AppliedAmount, spend.BeforeState!.Current,
                    spend.AfterState!.Current, spend.BeforeState.Maximum, spend.AfterState.Maximum));
        }
        var afterActual = capture.ReadExistingStrainReuseCounts();
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_zero_after_actual", window.Name, window.CapturePhase,
            counters = ReadGroupZeroMeasurementCounters(capture) }));
        Assert.Equal((warm.SignedParentReads, warm.OrdinaryEvaluations, warm.ActualEvaluations + 1),
            (afterActual.SignedParentReads, afterActual.OrdinaryEvaluations, afterActual.ActualEvaluations));
        Assert.True(afterActual.FreshInputChecks > warm.FreshInputChecks); Assert.True(afterActual.FreshDependencyChecks > warm.FreshDependencyChecks);
        Assert.Equal(sourceCold with { ResourceBatchComputations = sourceCold.ResourceBatchComputations + 1 },
            capture.ReadExistingPressureGroupComputationCounts());
        var closed = Assert.Single(capture.ReadClosedExchangeEvidence(lease));
        Assert.Equal((interval.ConflictId, interval.ExchangeId, 0), (closed.ConflictId, closed.ExchangeId, closed.Ordinal));
        Assert.Equal(new[] { 0, 1 }, closed.DiceClaims.Select(row => row.SourceIndex));
        Assert.Equal(new[] { 8, 4 }, closed.DiceClaims.Select(row => row.Value));
        Assert.True(JsonNode.DeepEquals(window.Data.Actual, JsonNode.Parse(closed.ExchangeJson)));
        var source = Assert.Single(closed.Sources);
        Assert.Equal(("activeConflict.exchangeLog[0]", "opposition", "pressure", "player_soul:player_soul", "guardian:" + SpiritualGuardianAttributionTestData.GuardianId),
            (source.Coordinate, source.AffectedSide, source.Operation, source.ActingActor, source.AffectedActor));
        Assert.Equal(window.Data.ActualCalculation, source.Calculation);
        Assert.Null(source.OriginalSpecialArtJson); Assert.Null(source.GuaranteedSeverityRank); Assert.Null(source.RetraumaWoundId);
        Assert.True(JsonNode.DeepEquals(window.Data.Actual, JsonNode.Parse(source.ExchangeJson)));
        var proof = JsonNode.Parse(Assert.IsType<string>(source.StrainPreimageJson))!.AsObject();
        var expectedProof = JsonNode.Parse(window.ExpectedProofJson)!.AsObject();
        Assert.True(JsonNode.DeepEquals(expectedProof, proof));
        var prefix = capture.ReadClosedResourceCandidatePrefix(lease, interval);
        Assert.True(JsonNode.DeepEquals(window.ActualRoot, prefix.CompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]));
        var restored = window.ActualRoot;
        restored["activeConflict"]!["exchangeLog"]![0]!["after"]!["oppositionSideStrain"] = "fractured";
        restored["activeConflict"]!["oppositionSideStrain"] = "fractured";
        Assert.True(JsonNode.DeepEquals(window.OrdinaryRoot, restored));
        var candidate = await capture.ReadC1CandidateOwnerImagesAsync(lease, interval);
        Assert.True(JsonNode.DeepEquals(window.ActualRoot, ReadGuardianAttributionJson(candidate[AfterlifeSpiritualConflictState.StatePath].Bytes!)));
        foreach (var path in new[] { WoundIdentityState.StatePath, WoundHistoryState.HistoryPath })
            Assert.Equal(window.Files[path], candidate.TryGetValue(path, out var image) ? image.Bytes : inventory.BeforeImages[path].Bytes);
        var profileImage = candidate.TryGetValue(AfterlifeEntityProfileState.StatePath, out var profiles)
            ? profiles.Bytes : inventory.BeforeImages[AfterlifeEntityProfileState.StatePath].Bytes;
        var guardian = ReadGuardianAttributionProfile(ReadGuardianAttributionJson(profileImage!));
        var beforeGuardian = ReadGuardianAttributionProfile(ReadGuardianAttributionJson(window.Files[AfterlifeEntityProfileState.StatePath]));
        for (var index = 0; index < window.Data.Retained.Effects.Count; index++)
            foreach (var preserved in new[] { "components", "triggers", "links", "stacking", "source", "target", "display", "state" })
                Assert.True(JsonNode.DeepEquals(beforeGuardian["activeEffects"]![index]![preserved],
                    guardian["activeEffects"]![index]![preserved]), preserved);
        Assert.True(JsonNode.DeepEquals(new JsonArray(window.Data.Retained.Parents.Select(row => row.DeepClone()).ToArray()), guardian["activeWounds"]));
        var projected = AfterlifeConflictActionPointProjectionService.Resolve(prefix.DefinitionsJson, prefix.StateJson, window.ActualRoot["activeConflict"]!.AsObject());
        RequireGuardianJourneyIssues(window.Name, "guardian_group_zero_actual_ap", projected.Issues);
        Assert.Equal((0m, 0m), (projected.Projection!.Player.Current, projected.Projection.Opposition.Current));
        var offered = await MeasureGuardianGroupZeroPhaseAsync("guardian_group_zero_first_c1", window.Name,
            () => capture.ComposeC1FirstOfferAsync(lease, interval));
        RequireGuardianJourneyIssues(window.Name, "guardian_group_zero_first_c1", offered.Issues);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_zero_after_c1", window.Name, window.CapturePhase,
            counters = ReadGroupZeroMeasurementCounters(capture) }));
        Assert.Equal(0, source.Calculation.MaximumSeverityRank);
        Assert.Null(offered.Pending); Assert.Null(offered.ProposedInstanceRow);
        var checkpoint = await MeasureGuardianGroupZeroPhaseAsync("guardian_group_zero_first_c2_draft", window.Name,
            () => capture.ComposeC2FirstCheckpointDraftAsync(lease, interval));
        RequireGuardianJourneyIssues(window.Name, "guardian_group_zero_first_c2_draft", checkpoint.Issues);
        Assert.Null(checkpoint.Pending); Assert.Null(checkpoint.Checkpoint);
        var transport = await MeasureGuardianGroupZeroPhaseAsync("guardian_group_zero_first_c2_no_offer", window.Name,
            () => capture.CommitC2FirstTransportAsync(lease, interval));
        RequireGuardianJourneyIssues(window.Name, "guardian_group_zero_first_c2_no_offer", transport.Issues);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_zero_no_offer_result", window.Name, window.CapturePhase,
            transport.Issues, transport.Disposition, pendingPresent = transport.Pending is not null,
            checkpointPresent = transport.Checkpoint is not null, counters = ReadGroupZeroMeasurementCounters(capture) }));
        Assert.Equal("no_offer", transport.Disposition); Assert.Null(transport.Pending); Assert.Null(transport.Checkpoint);
        Assert.Single(capture.ReadClosedExchangeEvidence(lease));
        AssertExistingPressureInputInert(window.Context, lease, window.Files, window.Generation);
        AssertPressureNoPrivatePair(window.Context, lease);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(window.Context.FileSystem, lease));
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(window.Context.FileSystem, lease, out _));
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_zero_actual_c1_contract_complete", window.Name, window.CapturePhase,
            parentRanks = new[] { 3, 4 }, window.Data.Resilience, expectedMaximum = window.Data.ActualCalculation.MaximumSeverityRank,
            noOffer = offered.Pending is null, proof, counters = ReadGroupZeroMeasurementCounters(capture) }));

        return interval;

        /// <summary>Queries the genuine ordinary owner while preserving the exact frontier and reporting the full per-phase result.</summary>
        /// <param name="phase">The first cold computation or same-owner warm reuse label.</param>
        /// <returns>Detached ordinary DATA issued by the current owner; no authority is fabricated.</returns>
        async Task<ValidationService.SpiritualOrdinaryPressureComparisonData> Query(string phase)
        {
            var beforeQuery = ReadGroupZeroMeasurementCounters(capture);
            var result = await ExecutePressureGroupComparisonStagesAsync(
                () => MeasureGuardianGroupZeroPhaseAsync("guardian_group_zero_ordinary_" + phase, window.Name,
                    () => capture.ReadExistingPressureOrdinaryComparisonAsync(lease)), () => capture.IsCurrentOwner,
                async () =>
                {
                    AssertPressureOrdinaryFrontiersEqual(window.Frontier, await ReadGuardianGroupZeroSnapshotAsync(capture, lease, window.Name + "_" + phase));
                    AssertExistingPressureInputInert(window.Context, lease, window.Files, window.Generation);
                    AssertPressureNoPrivatePair(window.Context, lease);
                });
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_zero_ordinary_desired_success", query = phase, window.Name, window.CapturePhase,
                parentRanks = new[] { 3, 4 }, window.Data.Resilience, result.Issues, result.PrefixBlocked, comparisonReturned = result.Comparison is not null,
                beforeQuery, afterQuery = ReadGroupZeroMeasurementCounters(capture) }));
            RequireGuardianJourneyIssues(window.Name, "guardian_group_zero_ordinary_" + phase, result.Issues);
            Assert.False(result.PrefixBlocked); Assert.True(capture.IsCurrentOwner);
            var value = Assert.IsType<ValidationService.SpiritualOrdinaryPressureComparisonData>(result.Comparison);
            Assert.True(JsonNode.DeepEquals(window.Data.Ordinary, value.ReadOrdinaryExchange()));
            Assert.Equal(window.Data.OrdinaryCalculation, value.OrdinaryCalculation);
            Assert.Equal(new CanonicalBeforeImage(true, window.RawOrdinaryBytes).Fingerprint, value.RawInputFingerprint);
            Assert.Equal("/activeConflict/exchangeLog/0", value.ExchangePointer); Assert.Equal(2, value.ExpectedTransitions.Count);
            foreach (var kind in new[] { ResourceOwnerKind.AfterlifeActor, ResourceOwnerKind.AfterlifeConflictSide })
            {
                var expected = Assert.Single(value.ExpectedTransitions, row => row.Coordinate.OwnerKind == kind);
                var side = kind == ResourceOwnerKind.AfterlifeActor ? "player" : "opposition";
                var ownerId = side == "player" ? "player_soul" : window.OrdinaryRoot["activeConflict"]!
                    [AfterlifeEntityProfileState.ResourceOwnerBindingsProperty]!["opposition"]!["resourceOwnerId"]!.GetValue<string>();
                Assert.Equal(new AfterlifeSpiritualConflictResourceOutcome.ExpectedTransition(
                    "turn_46:afterlife_conflict:exchange_guardian_group_46:" + side, "afterlife_cost", "exchange_guardian_group_46",
                    new("chaos_sea", kind, ownerId, "spiritual_action_points"), ResourceTransitionOperation.Spend, 3m, 0m, 3m, 0m), expected);
            }
            return value;
        }
    }
}
